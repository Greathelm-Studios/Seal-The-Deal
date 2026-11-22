using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Combat
{
    /// <summary>Pooled local shot tracer LineRenderer manager.</summary>
    public sealed class TracerPool : MonoBehaviour
    {
        enum FadeMode : byte
        {
            WidthOnly,
            PropertyBlockAlpha,
            VertexColor
        }

        struct Live
        {
            public LineRenderer Line;
            public LineRenderer Prefab;
            public float Left;
            public float Total;
            public float Width;
            public Color Colour;
            public FadeMode Mode;
            public Vector3 From;
            public Vector3 Direction;
            public float Distance;
            public float Speed;
            public float Length;
            public float Elapsed;
        }

        /// <summary>The far end of a tracer is this bright compared with the muzzle end.</summary>
        const float TailAlpha = 0.35f;
        const int MaxLiveTracers = 64;
        const int MaxPooledPerPrefab = 32;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int SurfaceId = Shader.PropertyToID("_Surface");

        static TracerPool _instance;

        readonly List<Live> _live = new(64);
        readonly Dictionary<LineRenderer, Stack<LineRenderer>> _free = new();
        readonly Dictionary<Material, FadeMode> _fadeModes = new();
        MaterialPropertyBlock _block;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
        }

        void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        void OnSceneUnloaded(Scene scene)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
                RecycleAt(i);
        }

        /// <summary>Draws one tracer from the muzzle to where the shot ended, for the given time in seconds.</summary>
        public static void Spawn(LineRenderer prefab, Vector3 from, Vector3 to, float seconds)
        {
            Spawn(prefab, from, to, seconds, 0f, 0f);
        }

        /// <summary>
        /// Flies a streak of the given length in metres from the muzzle to where the shot ended at the given
        /// speed in metres per second; it is gone once its tail reaches the end. A speed of 0 draws the whole
        /// line at once for the given time in seconds instead.
        /// </summary>
        public static void Spawn(LineRenderer prefab, Vector3 from, Vector3 to, float seconds, float speed, float length)
        {
            if (prefab == null || (seconds <= 0f && speed <= 0f))
                return;

            if (_instance == null)
            {
                var go = new GameObject("Tracers");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<TracerPool>();
            }

            _instance.Show(prefab, from, to, seconds, speed, length);
        }

        void Show(LineRenderer prefab, Vector3 from, Vector3 to, float seconds, float speed, float length)
        {
            if (!_free.TryGetValue(prefab, out Stack<LineRenderer> free))
            {
                free = new Stack<LineRenderer>();
                _free.Add(prefab, free);
            }

            while (_live.Count >= MaxLiveTracers)
                RecycleAt(0);

            LineRenderer line = free.Count > 0 ? free.Pop() : Instantiate(prefab, transform);
            line.positionCount = 2;
            line.useWorldSpace = true;
            Vector3 path = to - from;
            float distance = path.magnitude;
            bool travels = speed > 0f && distance > 0.01f;
            line.SetPosition(0, from);
            line.SetPosition(1, travels ? from : to);
            line.widthMultiplier = prefab.widthMultiplier;

            FadeMode mode = ModeFor(prefab.sharedMaterial, out Color baseColour);
            if (mode == FadeMode.VertexColor)
            {
                baseColour = prefab.startColor;
                line.startColor = baseColour;
                line.endColor = prefab.endColor;
            }
            else if (mode == FadeMode.PropertyBlockAlpha)
            {
                _block.Clear();
                _block.SetColor(BaseColorId, baseColour);
                line.SetPropertyBlock(_block);
            }

            line.gameObject.SetActive(true);
            var live = new Live { Line = line, Prefab = prefab, Left = seconds, Total = seconds, Width = prefab.widthMultiplier, Colour = baseColour, Mode = mode };
            if (travels)
            {
                live.From = from;
                live.Direction = path / distance;
                live.Distance = distance;
                live.Speed = speed;
                live.Length = Mathf.Max(0.05f, length);
                live.Left = live.Total = (distance + live.Length) / speed;
            }
            _live.Add(live);
        }

        FadeMode ModeFor(Material material, out Color baseColour)
        {
            if (material == null || !material.HasProperty(BaseColorId))
            {
                baseColour = Color.white;
                return FadeMode.VertexColor;
            }

            baseColour = material.GetColor(BaseColorId);
            if (_fadeModes.TryGetValue(material, out FadeMode cached))
                return cached;

            bool transparent = material.renderQueue >= 3000 || (material.HasProperty(SurfaceId) && material.GetFloat(SurfaceId) > 0.5f);
            // Opaque URP Unlit (Tracer.mat) ignores alpha and vertex colours, so shrinking width alone avoids gradient rebuilds.
            FadeMode mode = transparent ? FadeMode.PropertyBlockAlpha : FadeMode.WidthOnly;
            _fadeModes[material] = mode;
            return mode;
        }

        void Update()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Live live = _live[i];
                if (live.Line == null)
                {
                    RecycleAt(i);
                    continue;
                }

                live.Left -= Time.deltaTime;
                if (live.Speed > 0f)
                {
                    if (!Fly(ref live))
                    {
                        RecycleAt(i);
                        continue;
                    }

                    _live[i] = live;
                    continue;
                }

                if (live.Left > 0f)
                {
                    // Thins and fades as it goes, so a burst reads as streaks rather than a solid bar.
                    float left = live.Left / live.Total;
                    live.Line.widthMultiplier = live.Width * left;

                    if (live.Mode == FadeMode.PropertyBlockAlpha)
                    {
                        Color col = live.Colour;
                        col.a *= left;
                        _block.Clear();
                        _block.SetColor(BaseColorId, col);
                        live.Line.SetPropertyBlock(_block);
                    }
                    else if (live.Mode == FadeMode.VertexColor)
                    {
                        Color start = live.Colour;
                        start.a *= left;
                        Color end = start;
                        end.a *= TailAlpha;
                        live.Line.startColor = start;
                        live.Line.endColor = end;
                    }

                    _live[i] = live;
                    continue;
                }

                RecycleAt(i);
            }
        }

        /// <summary>Moves a flying streak on; false once its tail has reached the end.</summary>
        static bool Fly(ref Live live)
        {
            live.Elapsed += Time.deltaTime;
            float head = live.Speed * live.Elapsed;
            float tail = head - live.Length;
            if (tail >= live.Distance)
                return false;

            // Position 0 is the tail and 1 the head, so a texture bright at its right end reads as the round's glow.
            live.Line.SetPosition(0, live.From + live.Direction * Mathf.Clamp(tail, 0f, live.Distance));
            live.Line.SetPosition(1, live.From + live.Direction * Mathf.Min(head, live.Distance));
            return true;
        }

        void RecycleAt(int index)
        {
            Live live = _live[index];
            int last = _live.Count - 1;
            _live[index] = _live[last];
            _live.RemoveAt(last);

            if (live.Line == null)
                return;

            live.Line.gameObject.SetActive(false);
            if (_free.TryGetValue(live.Prefab, out Stack<LineRenderer> free) && free.Count < MaxPooledPerPrefab)
                free.Push(live.Line);
            else
                Destroy(live.Line.gameObject);
        }
    }
}
