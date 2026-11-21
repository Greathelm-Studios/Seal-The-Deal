using System.Collections.Generic;
using CloseTheDeal.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Effects
{
    /// <summary>Pooled local blood splat projector listening to HitFeed.Struck.</summary>
    public sealed class BloodSplatSystem : MonoBehaviour
    {
        const string MaterialResource = "BloodSplat";
        const string ProfileResource = "BloodProfile";
        const string ShaderName = "Close the Deal/Blood Splat";
        const float MaxSplatDistance = 60f;
        const float SurfaceOffsetFraction = 0.2f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int CutColorId = Shader.PropertyToID("_CutColor");
        static readonly int ParamsId = Shader.PropertyToID("_Params");
        static readonly string[] ExcludedLayers = { "Player", "Ragdoll", "Viewmodel", "UI" };

        static BloodSplatSystem instance;
        static Camera _mainCamera;

        sealed class Splat
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public float Born;
            public float Size;
            public float Depth;
            public float Seed;
            public float Angle;
            public float Stretch;
            public bool Settled;
        }

        Splat[] ring;
        int ringHead;
        int ringCount;
        readonly Stack<Splat> pool = new Stack<Splat>();
        MaterialPropertyBlock block;
        Material material;
        Mesh cube;
        BloodProfile profile;
        int surfaceMask;
        bool _ownsMaterial;
        bool _ownsProfile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            _mainCamera = null;
        }

        /// <summary>Makes the system when a Play Mode or build session has loaded its first scene.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => Create();

        static BloodSplatSystem Create()
        {
            if (instance != null)
                return instance;
            // No graphics device (a headless server or batch run): nothing could be seen.
            if (!Application.isPlaying || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return null;

            var go = new GameObject("[Blood Splats]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<BloodSplatSystem>();
            return instance;
        }

        /// <summary>
        /// Throws blood for one hit, as if <see cref="HitFeed.Struck"/> had been raised for it.
        /// Used by the dev test menu; real hits arrive through the event.
        /// </summary>
        /// <param name="victim">The body that was struck, so rays ignore it; may be null.</param>
        public static void Splash(in HitInfo hit, Transform victim)
        {
            BloodSplatSystem system = Create();
            if (system != null)
                system.Spray(hit, victim);
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            block = new MaterialPropertyBlock();
            surfaceMask = BuildSurfaceMask();
            cube = BuildCube();
            material = LoadMaterial();
        }

        void OnEnable()
        {
            HitFeed.Struck += OnStruck;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        void OnDisable()
        {
            HitFeed.Struck -= OnStruck;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        void OnSceneUnloaded(Scene scene)
        {
            _mainCamera = null;
            ClearLive();
        }

        void OnDestroy()
        {
            if (_ownsMaterial && material != null)
            {
                Destroy(material);
                material = null;
            }
            if (_ownsProfile && profile != null)
            {
                Destroy(profile);
                profile = null;
            }
            _ownsMaterial = false;
            _ownsProfile = false;
            if (instance == this)
                instance = null;
        }

        void OnStruck(HitEvent e)
        {
            // Props thrown and blasts hurt but do not bleed: blood is for gunshots and blows.
            if (e.Hit.Type == HitType.Bullet || e.Hit.Type == HitType.Melee)
                Spray(e.Hit, e.Victim);
        }

        BloodProfile Profile
        {
            get
            {
                if (profile == null)
                {
                    profile = Resources.Load<BloodProfile>(ProfileResource);
                    if (profile == null)
                    {
                        // Defaults in code: the field initialisers in BloodProfile.
                        profile = ScriptableObject.CreateInstance<BloodProfile>();
                        profile.hideFlags = HideFlags.HideAndDontSave;
                        _ownsProfile = true;
                    }
                    else
                    {
                        _ownsProfile = false;
                    }
                }

                return profile;
            }
        }

        /// <summary>Everything a ray may hit: the level and its props, but not players/ragdolls (the victim's own body would eat every ray) and not UI or first-person arms.</summary>
        static int BuildSurfaceMask()
        {
            int mask = Physics.DefaultRaycastLayers;
            for (int i = 0; i < ExcludedLayers.Length; i++)
            {
                int index = LayerMask.NameToLayer(ExcludedLayers[i]);
                if (index >= 0)
                    mask &= ~(1 << index);
            }

            return mask;
        }

        static Mesh BuildCube()
        {
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primitive);
            return mesh;
        }

        Material LoadMaterial()
        {
            var loaded = Resources.Load<Material>(MaterialResource);
            if (loaded != null)
            {
                _ownsMaterial = false;
                return loaded;
            }

            // The material asset is missing: make one from the shader. In a build the shader is
            // only present if something references it, so this path is for the Editor.
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                _ownsMaterial = false;
                Debug.LogWarning($"[Blood] Neither the material '{MaterialResource}' in a Resources folder nor the shader '{ShaderName}' was found; no blood will draw. Run Close the Deal > Art > Build Blood Assets.");
                return null;
            }

            _ownsMaterial = true;
            return new Material(shader) { name = "BloodSplat (runtime)", enableInstancing = true, hideFlags = HideFlags.HideAndDontSave };
        }

        void Spray(in HitInfo hit, Transform victim)
        {
            if (material == null)
                return;

            if (TryCameraPosition(out Vector3 eyePos) && (hit.Point - eyePos).sqrMagnitude > MaxSplatDistance * MaxSplatDistance)
                return;

            BloodProfile p = Profile;
            bool melee = hit.Type == HitType.Melee;
            float impulse = Mathf.Max(hit.Impulse, 1f);

            Vector3 dir = hit.Direction.sqrMagnitude > 1e-4f ? hit.Direction.normalized : (victim != null ? victim.forward : Vector3.forward);

            float size = Mathf.Min(p.BaseSize * Mathf.Sqrt(impulse / p.ReferenceImpulse), p.MaxSize);
            float stretch = melee ? p.MeleeStretch : p.BulletStretch;

            int count = (melee ? p.MeleeSplats : p.BulletSplats) + Mathf.FloorToInt(p.ExtraSplatsPer100Impulse * impulse / 100f);
            count = Mathf.Min(count, p.MaxSplatsPerHit);
            float cone = Mathf.Min((melee ? p.MeleeCone : p.BulletCone) + p.ConeBonusPer100Impulse * impulse / 100f, 85f);

            for (int i = 0; i < count; i++)
            {
                // The first ray follows the shot exactly; the rest fan out inside the cone.
                Vector3 rayDir = i == 0 ? dir : InCone(dir, cone);
                if (Cast(hit.Point, rayDir, p.Reach, victim, out RaycastHit surface))
                    Place(surface.point, surface.normal, rayDir, size * Random.Range(1f - p.SizeVariation, 1f + p.SizeVariation), stretch, p);
            }

            if (p.FloorSize > 0f && Cast(hit.Point, Vector3.down, p.FloorReach, victim, out RaycastHit floor))
                Place(floor.point, floor.normal, dir, size * p.FloorSize * Random.Range(1f - p.SizeVariation, 1f + p.SizeVariation), stretch, p);
        }

        static bool TryCameraPosition(out Vector3 position)
        {
            if (PlayerCamera.Instance != null)
            {
                position = PlayerCamera.Instance.transform.position;
                return true;
            }

            if (_mainCamera == null)
                _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                position = _mainCamera.transform.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        /// <summary>A random direction within <paramref name="degrees"/> of <paramref name="dir"/>, spread evenly over the cone's circle.</summary>
        static Vector3 InCone(Vector3 dir, float degrees)
        {
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            float tilt = degrees * Mathf.Sqrt(Random.value);
            return Quaternion.AngleAxis(Random.value * 360f, dir) * (Quaternion.AngleAxis(tilt, side) * dir);
        }

        /// <summary>Nearest surface along the ray that is not part of the victim.</summary>
        bool Cast(Vector3 origin, Vector3 direction, float reach, Transform victim, out RaycastHit best)
        {
            if (!Physics.Raycast(origin, direction, out best, reach, surfaceMask, QueryTriggerInteraction.Ignore))
                return false;

            return victim == null || !best.collider.transform.IsChildOf(victim);
        }

        void Place(Vector3 point, Vector3 normal, Vector3 throwDir, float size, float stretch, BloodProfile p)
        {
            Splat s = Acquire(p);

            // The throw direction lying in the surface decides how the splat streaks. Thrown
            // straight into the surface there is no direction: a round splat in a random turn.
            Vector3 flat = Vector3.ProjectOnPlane(throwDir, normal);
            float flatLength = flat.magnitude;
            Vector3 upReference = Mathf.Abs(normal.y) < 0.85f ? Vector3.up : (flatLength > 0.05f ? flat / flatLength : Vector3.forward);

            // The box looks into the surface (its +Z is minus the normal).
            Quaternion rotation = Quaternion.LookRotation(-normal, upReference);
            s.Transform.SetPositionAndRotation(point + normal * (p.Depth * SurfaceOffsetFraction), rotation);

            if (flatLength > 0.05f)
            {
                Vector3 right = rotation * Vector3.right;
                Vector3 up = rotation * Vector3.up;
                s.Angle = Mathf.Atan2(Vector3.Dot(flat, up), Vector3.Dot(flat, right));
                s.Stretch = stretch * Mathf.Clamp01(0.35f + flatLength);
            }
            else
            {
                s.Angle = Random.value * Mathf.PI * 2f;
                s.Stretch = stretch * 0.3f;
            }

            s.Size = size;
            s.Depth = p.Depth;
            s.Seed = Random.Range(0f, 100f);
            s.Born = Time.time;
            s.Settled = false;

            s.Transform.localScale = new Vector3(0.001f, 0.001f, p.Depth);
            s.Renderer.enabled = true;
            Apply(s, 1f, p);

            int tail = (ringHead + ringCount) % ring.Length;
            ring[tail] = s;
            ringCount++;
        }

        void EnsureRingCapacity(int capacity)
        {
            int target = Mathf.Max(4, capacity);
            if (ring == null)
            {
                ring = new Splat[target];
                return;
            }

            if (ring.Length >= target)
                return;

            var grown = new Splat[target];
            for (int i = 0; i < ringCount; i++)
                grown[i] = ring[(ringHead + i) % ring.Length];
            ring = grown;
            ringHead = 0;
        }

        /// <summary>A splat to use: from the pool, new, or the oldest live one when the cap is reached.</summary>
        Splat Acquire(BloodProfile p)
        {
            int maxLive = Mathf.Max(4, p.MaxLiveSplats);
            EnsureRingCapacity(maxLive);

            if (ringCount >= maxLive)
            {
                Splat evicted = PopOldest();
                while (ringCount >= maxLive)
                    ReleaseOldest();
                return evicted;
            }

            if (pool.Count > 0)
                return pool.Pop();

            var go = new GameObject("Blood Splat");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = cube;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            r.allowOcclusionWhenDynamic = true;
            return new Splat { Transform = go.transform, Renderer = r };
        }

        Splat PopOldest()
        {
            Splat s = ring[ringHead];
            ring[ringHead] = null;
            ringHead = (ringHead + 1) % ring.Length;
            ringCount--;
            return s;
        }

        void ReleaseOldest()
        {
            Splat s = PopOldest();
            if (s == null)
                return;

            s.Renderer.enabled = false;
            pool.Push(s);
        }

        void ClearLive()
        {
            while (ringCount > 0)
                ReleaseOldest();
            ringHead = 0;
        }

        /// <summary>Writes one splat's look to its renderer. <paramref name="life"/> is 1 fresh, 0 gone.</summary>
        void Apply(Splat s, float life, BloodProfile p)
        {
            block.Clear();
            block.SetColor(ColorId, p.BloodColor);
            block.SetColor(CutColorId, p.CutColor);
            block.SetVector(ParamsId, new Vector4(s.Seed, s.Angle, s.Stretch, life));
            s.Renderer.SetPropertyBlock(block);
        }

        void Update()
        {
            if (ringCount == 0)
                return;

            BloodProfile p = Profile;
            float now = Time.time;
            float totalLife = p.Lifetime + p.ShrinkTime;

            // Live splats expire in FIFO order from ringHead.
            while (ringCount > 0 && now - ring[ringHead].Born >= totalLife)
                ReleaseOldest();

            for (int i = 0; i < ringCount; i++)
            {
                Splat s = ring[(ringHead + i) % ring.Length];
                float age = now - s.Born;

                if (!s.Settled)
                {
                    float t = p.PopTime > 0f ? Mathf.Clamp01(age / p.PopTime) : 1f;
                    float grow = PopCurve(t, p.PopOvershoot);
                    s.Transform.localScale = new Vector3(s.Size * grow, s.Size * grow, s.Depth);
                    s.Settled = t >= 1f;
                }

                if (age > p.Lifetime)
                    Apply(s, 1f - (age - p.Lifetime) / p.ShrinkTime, p);
            }
        }

        /// <summary>0 to 1 with an overshoot: swells past 1 and settles back. At <paramref name="overshoot"/> 0 it is a plain ease-out.</summary>
        static float PopCurve(float t, float overshoot)
        {
            // Ease-out-back. The constant is how far it swells; 12 x overshoot gives about that fraction over 1.
            float c1 = overshoot * 12f;
            float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
