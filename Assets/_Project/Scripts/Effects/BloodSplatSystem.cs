// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Sonnet 5.5 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "create a blood splatter shader that fits in within the games artstyle
//              that paints around objects and ontop of them when hit by these attacks."
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Listens for bodies being struck and throws cartoon blood onto the surfaces behind
//              them and the floor under them, using the "Close the Deal/Blood Splat" projector
//              shader. A pool of boxes, a cap on how many exist, a pop-in and a shrink-away.
//              It creates itself; no scene or prefab needs to know about it.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using CloseTheDeal.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloseTheDeal.Effects
{
    /// <summary>
    /// Pooled blood splat spawner. Hears <see cref="HitFeed.Struck"/> and, for bullets and melee
    /// hits, paints blood on the level behind and under the struck body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ownership (AGENTS.md §3):</b> purely cosmetic and local. Every machine hears the hit
    /// from <see cref="HitFeed"/> and draws its own splats; nothing is networked, no splat has a
    /// collider and nothing reads one back. Splats are not the same on two machines (the random
    /// seeds differ), which is fine because nothing depends on them.
    /// </para>
    /// <para>
    /// Each splat is a plain cube drawn with the projector shader, which paints whatever surface
    /// is inside the cube. WHY a cube and not a flat quad: the splat then wraps over the floor
    /// and desk in front of a wall instead of floating in front of it.
    /// </para>
    /// <para>
    /// Created by <see cref="Boot"/> as a hidden object that survives scene loads. Tuning is in
    /// <see cref="BloodProfile"/>; the profile and material are loaded from a Resources folder
    /// and fall back to defaults in code when missing.
    /// </para>
    /// </remarks>
    public sealed class BloodSplatSystem : MonoBehaviour
    {
        /// <summary>Resources name of the material; <c>Seal the Deal &gt; Art &gt; Build Blood Assets</c> writes it.</summary>
        const string MaterialResource = "BloodSplat";

        /// <summary>Resources name of the profile; the same menu item writes it.</summary>
        const string ProfileResource = "BloodProfile";

        const string ShaderName = "Close the Deal/Blood Splat";

        /// <summary>How many surfaces one ray can report. WHY 12: a spray through a cluttered office crosses several props before the wall; the nearest that is not the victim wins.</summary>
        const int RayBuffer = 12;

        /// <summary>
        /// Where in the box, along its depth, the surface sits: a fifth of the depth in front of the
        /// wall, the rest behind it. WHY: the box needs room in front for desks and legs that
        /// stand off the wall, and a margin behind for depth-buffer error.
        /// </summary>
        const float SurfaceOffsetFraction = 0.2f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int CutColorId = Shader.PropertyToID("_CutColor");
        static readonly int ParamsId = Shader.PropertyToID("_Params");

        static BloodSplatSystem instance;

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

        readonly List<Splat> live = new List<Splat>();
        readonly Stack<Splat> pool = new Stack<Splat>();
        readonly RaycastHit[] hits = new RaycastHit[RayBuffer];
        MaterialPropertyBlock block;
        Material material;
        Mesh cube;
        BloodProfile profile;
        int surfaceMask;

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

        void OnEnable() => HitFeed.Struck += OnStruck;

        void OnDisable() => HitFeed.Struck -= OnStruck;

        void OnDestroy()
        {
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
                    }
                }

                return profile;
            }
        }

        /// <summary>Everything a ray may hit: the level and its props, but not players (the victim's own body would eat every ray) and not UI or first-person arms.</summary>
        static int BuildSurfaceMask()
        {
            int mask = Physics.DefaultRaycastLayers;
            foreach (string layer in new[] { "Player", "Viewmodel", "UI" })
            {
                int index = LayerMask.NameToLayer(layer);
                if (index >= 0)
                    mask &= ~(1 << index);
            }

            return mask;
        }

        static Mesh BuildCube()
        {
            // WHY borrow a primitive's mesh: it is the one unit cube every build is sure to have,
            // and the primitive itself (with its collider) is thrown away at once.
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primitive);
            return mesh;
        }

        static Material LoadMaterial()
        {
            var loaded = Resources.Load<Material>(MaterialResource);
            if (loaded != null)
                return loaded;

            // The material asset is missing: make one from the shader. In a build the shader is
            // only present if something references it, so this path is for the Editor.
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[Blood] Neither the material '{MaterialResource}' in a Resources folder nor the shader '{ShaderName}' was found; no blood will draw. Run Seal the Deal > Art > Build Blood Assets.");
                return null;
            }

            return new Material(shader) { name = "BloodSplat (runtime)", enableInstancing = true, hideFlags = HideFlags.HideAndDontSave };
        }

        // ---- Spraying ----

        void Spray(in HitInfo hit, Transform victim)
        {
            if (material == null)
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
            best = default;
            int n = Physics.RaycastNonAlloc(origin, direction, hits, reach, surfaceMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = hits[i];
                if (h.distance >= nearest)
                    continue;
                // A ragdoll part can sit on another layer than the player's root; skip anything of the victim's.
                if (victim != null && h.collider.transform.IsChildOf(victim))
                    continue;

                nearest = h.distance;
                best = h;
                found = true;
            }

            return found;
        }

        // ---- Placing and the pool ----

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
            live.Add(s);
        }

        /// <summary>A splat to use: from the pool, new, or the oldest live one when the cap is reached.</summary>
        Splat Acquire(BloodProfile p)
        {
            while (live.Count >= p.MaxLiveSplats && live.Count > 0)
                Release(0);

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
            r.allowOcclusionWhenDynamic = false;
            return new Splat { Transform = go.transform, Renderer = r };
        }

        void Release(int index)
        {
            Splat s = live[index];
            live.RemoveAt(index);
            s.Renderer.enabled = false;
            pool.Push(s);
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
            if (live.Count == 0)
                return;

            BloodProfile p = Profile;
            float now = Time.time;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Splat s = live[i];
                float age = now - s.Born;
                if (age >= p.Lifetime + p.ShrinkTime)
                {
                    Release(i);
                    continue;
                }

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
