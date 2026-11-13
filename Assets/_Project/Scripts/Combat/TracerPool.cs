using System.Collections.Generic;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Draws shot tracers and takes them away again. Local visuals only, never networked.
    /// Lines are reused, so a full-auto burst allocates nothing once the pool has warmed up.
    /// Makes itself on first use and lives for the whole session.
    /// </summary>
    public sealed class TracerPool : MonoBehaviour
    {
        struct Live
        {
            public LineRenderer Line;
            public LineRenderer Prefab;
            public float Left;
            public float Total;
            public float Width;
            public Color Colour;
        }

        /// <summary>The far end of a tracer is this bright compared with the muzzle end.</summary>
        const float TailAlpha = 0.35f;

        static TracerPool _instance;

        readonly List<Live> _live = new(64);
        readonly Dictionary<LineRenderer, Stack<LineRenderer>> _free = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
        }

        /// <summary>Draws one tracer from the muzzle to where the shot ended, for the given time in seconds.</summary>
        public static void Spawn(LineRenderer prefab, Vector3 from, Vector3 to, float seconds)
        {
            if (prefab == null || seconds <= 0f)
                return;

            if (_instance == null)
            {
                var go = new GameObject("Tracers");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<TracerPool>();
            }

            _instance.Show(prefab, from, to, seconds);
        }

        void Show(LineRenderer prefab, Vector3 from, Vector3 to, float seconds)
        {
            if (!_free.TryGetValue(prefab, out Stack<LineRenderer> free))
            {
                free = new Stack<LineRenderer>();
                _free.Add(prefab, free);
            }

            LineRenderer line = free.Count > 0 ? free.Pop() : Instantiate(prefab, transform);
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = prefab.widthMultiplier;
            // A reused line still has the faded colour of its last shot.
            line.startColor = prefab.startColor;
            line.endColor = prefab.endColor;
            line.gameObject.SetActive(true);

            _live.Add(new Live { Line = line, Prefab = prefab, Left = seconds, Total = seconds, Width = prefab.widthMultiplier, Colour = prefab.startColor });
        }

        void Update()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Live live = _live[i];
                live.Left -= Time.deltaTime;
                if (live.Left > 0f)
                {
                    // Thins and fades as it goes, so a burst reads as streaks rather than a solid bar.
                    float left = live.Left / live.Total;
                    live.Line.widthMultiplier = live.Width * left;
                    Color start = live.Colour;
                    start.a *= left;
                    Color end = start;
                    end.a *= TailAlpha;
                    live.Line.startColor = start;
                    live.Line.endColor = end;
                    _live[i] = live;
                    continue;
                }

                live.Line.gameObject.SetActive(false);
                _free[live.Prefab].Push(live.Line);
                _live[i] = _live[_live.Count - 1];
                _live.RemoveAt(_live.Count - 1);
            }
        }
    }
}
