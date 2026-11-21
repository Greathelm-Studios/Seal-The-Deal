// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "Add a particle effect where blood splatters off of a player when
//              they're hit, from that spot and somewhat from their body."
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Throws drops of blood through the air off a struck body: most from the spot that
//              was hit, some from the rest of the body. One particle system for every hit in the
//              scene. It creates itself; no scene or prefab needs to know about it.
// -----------------------------------------------------------------------------
using CloseTheDeal.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Effects
{
    /// <summary>
    /// The blood in the air. Hears <see cref="HitFeed.Struck"/> and, for bullets and melee hits,
    /// bursts drops off the struck body. <see cref="BloodSplatSystem"/> paints where it lands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ownership (AGENTS.md §3):</b> purely cosmetic and local, like the splats. Every machine
    /// hears the hit and throws its own drops; nothing is networked and the drops touch nothing.
    /// </para>
    /// <para>
    /// WHY one shared particle system and not one per hit: every drop is the same square with
    /// its own place and speed, so one system draws all of them in one batch and a busy fight
    /// creates no objects. Alternative considered: drops that collide with the level and leave
    /// marks; the splat system already paints the surfaces, and world collision for particles
    /// costs physics queries for something that lasts half a second.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BloodDropBurst : MonoBehaviour
    {
        /// <summary>The drop material, by name inside a Resources folder. Written by Close the Deal > Art > Build Blood Assets.</summary>
        const string MaterialResource = "BloodDrop";

        /// <summary>The tuning asset shared with the splats, by name inside a Resources folder.</summary>
        const string ProfileResource = "BloodProfile";

        const string ShaderName = "Close the Deal/Blood Drop";

        /// <summary>The body's height range the "from the body" drops start in, metres above the victim's feet: about knee to shoulder on the 1.8 m character.</summary>
        const float BodyLow = 0.6f;
        const float BodyHigh = 1.5f;

        /// <summary>How far out from the body's centre line the "from the body" drops start, metres: about the surface of the torso.</summary>
        const float BodyRadius = 0.2f;

        /// <summary>How much of a drop's length comes from its speed, seconds. At 5 m/s a drop is drawn about 12 cm longer than it is wide.</summary>
        const float StretchPerSpeed = 0.025f;

        /// <summary>Drops that would start closer to this machine's eye than this, metres, are left out. WHY: in the body view the eye is in the struck body, and a 5 cm drop 20 cm from the lens fills a quarter of the screen.</summary>
        const float NearEye = 0.5f;

        /// <summary>Beyond this distance from the camera, in metres, drop bursts are skipped.</summary>
        const float MaxDropDistance = 60f;

        static BloodDropBurst instance;
        static Camera _mainCamera;
        static readonly int CutColorId = Shader.PropertyToID("_CutColor");

        ParticleSystem drops;
        Material material;
        BloodProfile profile;
        bool _ownsProfile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            _mainCamera = null;
        }

        /// <summary>Makes the system when a Play Mode or build session has loaded its first scene.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null)
                return;
            // No graphics device (a headless server or batch run): nothing could be seen.
            if (!Application.isPlaying || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;

            var go = new GameObject("[Blood Drops]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<BloodDropBurst>();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
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

            material = LoadMaterial();
            if (material != null)
            {
                material.SetColor(CutColorId, profile.CutColor);
                drops = BuildSystem();
            }
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
            if (drops != null)
                drops.Clear();
        }

        void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
                material = null;
            }
            if (_ownsProfile && profile != null)
            {
                Destroy(profile);
                profile = null;
            }
            _ownsProfile = false;
            if (instance == this)
                instance = null;
        }

        static Material LoadMaterial()
        {
            var loaded = Resources.Load<Material>(MaterialResource);
            if (loaded != null)
                return new Material(loaded) { name = "BloodDrop (runtime)", hideFlags = HideFlags.HideAndDontSave };

            // The material asset is missing: make one from the shader. In a build the shader is
            // only present if something references it, so this path is for the Editor.
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[Blood] Neither the material '{MaterialResource}' in a Resources folder nor the shader '{ShaderName}' was found; no drops will fly. Run Close the Deal > Art > Build Blood Assets.");
                return null;
            }

            return new Material(shader) { name = "BloodDrop (runtime)", hideFlags = HideFlags.HideAndDontSave };
        }

        /// <summary>One particle system that never emits by itself: every drop is placed by <see cref="Burst"/>.</summary>
        ParticleSystem BuildSystem()
        {
            // Inactive while it is set up, so the system does not start playing with its defaults.
            var holder = new GameObject("Drops");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);

            var system = holder.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(profile.MaxDropsPerHit * 6, 64);
            main.gravityModifier = profile.DropGravity;
            main.scalingMode = ParticleSystemScalingMode.Local;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            // Each drop shrinks to nothing over its life, so it never blinks out at full size.
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f)));

            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = StretchPerSpeed;
            renderer.lengthScale = 1f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            holder.SetActive(true);
            return system;
        }

        void OnStruck(HitEvent e)
        {
            // Props thrown and blasts hurt but do not bleed: blood is for gunshots and blows.
            if (e.Hit.Type == HitType.Bullet || e.Hit.Type == HitType.Melee)
                Burst(e.Hit, e.Victim);
        }

        /// <summary>Throws the drops for one hit.</summary>
        void Burst(in HitInfo hit, Transform victim)
        {
            if (drops == null)
                return;

            int maxParticles = drops.main.maxParticles;
            int remaining = maxParticles - drops.particleCount;
            if (remaining <= 0)
                return;

            bool hasEye = TryCameraPosition(out Vector3 eyePos);
            if (hasEye && (hit.Point - eyePos).sqrMagnitude > MaxDropDistance * MaxDropDistance)
                return;

            BloodProfile p = profile;
            bool melee = hit.Type == HitType.Melee;
            // A harder hit throws more; the square root keeps a 300 N·s hit from throwing fifteen times a pistol's.
            float strength = Mathf.Sqrt(Mathf.Max(hit.Impulse, 1f) / p.ReferenceImpulse) * (melee ? p.MeleeDropsBoost : 1f);
            Vector3 dir = hit.Direction.sqrMagnitude > 1e-4f ? hit.Direction.normalized : (victim != null ? victim.forward : Vector3.forward);
            float cone = Mathf.Min(p.DropSpread * (melee ? 2f : 1f), 85f);

            var drop = new ParticleSystem.EmitParams { startColor = p.BloodColor };

            int fromWound = Mathf.Min(Mathf.RoundToInt(p.DropsFromWound * strength), p.MaxDropsPerHit);
            for (int i = 0; i < fromWound && remaining > 0; i++)
            {
                // Most fly on the way the hit was going; a share sprays back at the attacker, slower and wider.
                bool back = Random.value < p.BackSpray;
                Vector3 way = back ? InCone(-dir, Mathf.Min(cone * 1.6f, 85f)) : InCone(dir, cone);
                float speed = p.DropSpeed * Random.Range(0.4f, 1f) * (back ? 0.5f : 1f);
                if (Emit(ref drop, hit.Point, way * speed, p, hasEye, eyePos))
                    remaining--;
            }

            if (victim == null || remaining <= 0)
                return;

            int fromBody = Mathf.Min(Mathf.RoundToInt(p.DropsFromBody * strength), p.MaxDropsPerHit - fromWound);
            for (int i = 0; i < fromBody && remaining > 0; i++)
            {
                // Shaken off the rest of the body: outward from its centre line, pushed a little
                // the way the hit went and a little up, at a fraction of the wound's speed.
                Vector2 ring = Random.insideUnitCircle.normalized;
                var outward = new Vector3(ring.x, 0f, ring.y);
                Vector3 from = victim.position + Vector3.up * Random.Range(BodyLow, BodyHigh) + outward * BodyRadius;
                Vector3 way = (outward + dir * 0.6f + Vector3.up * 0.5f).normalized;
                if (Emit(ref drop, from, way * (p.DropSpeed * Random.Range(0.2f, 0.5f)), p, hasEye, eyePos))
                    remaining--;
            }
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

        bool Emit(ref ParticleSystem.EmitParams drop, Vector3 position, Vector3 velocity, BloodProfile p, bool hasEye, Vector3 eyePos)
        {
            if (hasEye && (eyePos - position).sqrMagnitude < NearEye * NearEye)
                return false;

            drop.position = position;
            drop.velocity = velocity;
            drop.startSize = p.DropSize * Random.Range(0.5f, 1.3f);
            drop.startLifetime = p.DropLife * Random.Range(0.6f, 1.2f);
            drops.Emit(drop, 1);
            return true;
        }

        /// <summary>A random direction within <paramref name="degrees"/> of <paramref name="dir"/>, spread evenly over the cone's circle.</summary>
        static Vector3 InCone(Vector3 dir, float degrees)
        {
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            float tilt = degrees * Mathf.Sqrt(Random.value);
            return Quaternion.AngleAxis(Random.value * 360f, dir) * (Quaternion.AngleAxis(tilt, side) * dir);
        }
    }
}
