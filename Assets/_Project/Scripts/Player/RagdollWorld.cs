// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-12
//  Prompt    : Alex chose "True ragdoll body" after several procedural passes felt stiff:
//              "look at TABS, gang beast"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The separate physics world every player's ragdoll lives in, and the copies of
//              nearby level colliders it needs so ragdolls have something to hit. Its own file
//              because it is shared by all ragdolls and is the one place that decides how that
//              world is kept apart from the game's networked physics.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// A private physics scene for visual ragdolls, stepped by this class and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a separate world (AGENTS.md §3):</b> the game's own physics scene is stepped by
    /// FishNet once a tick and then re-stepped several times whenever a client reconciles. A
    /// ragdoll in that scene would be simulated many times over, at the wrong speed, and its
    /// bodies could push the predicted player capsule and the host-simulated props. In a scene
    /// of its own a ragdoll is simulated exactly once per step here, and cannot touch anything
    /// that matters to the game. Nothing in this world is networked; each client runs its own.
    /// </para>
    /// <para>
    /// <b>The cost of that:</b> the level's colliders are in the other world, so this one keeps
    /// copies ("proxies") of whatever solid level geometry is near a ragdoll. Proxies are made
    /// when a collider first comes within reach and dropped when no ragdoll is near any more.
    /// Only box, sphere, capsule and mesh colliders are copied, and only as they stood when
    /// copied: a moving door or a thrown prop is not followed.
    /// </para>
    /// <para>
    /// Alternative considered: FishNet's <c>OfflineRigidbody</c>, which pauses a body during
    /// re-simulation. It would keep ragdolls in the game's scene and give real collision with
    /// props for free, but they would still step only once a tick (visibly stepping at a high
    /// frame rate) and would still be able to shove the predicted capsule.
    /// </para>
    /// <para>
    /// Main thread only. The step allocates nothing; making a proxy allocates once per collider.
    /// </para>
    /// </remarks>
    public static class RagdollWorld
    {
        /// <summary>
        /// Length of one physics step, seconds. WHY 1/120: twice the game's tick rate, so joint
        /// chains stay stable at the drive strengths the profile allows, and close enough to
        /// common frame rates that the leftover time blended away each frame is small.
        /// </summary>
        public const float StepTime = 1f / 120f;

        /// <summary>Most steps taken in one frame. WHY 4: covers frames down to 30 a second; a slower frame runs the ragdolls in slow motion rather than costing more.</summary>
        const int MaxStepsPerFrame = 4;

        /// <summary>How far around a ragdoll level colliders are copied, metres. WHY 3: a 1.8 m body's limbs reach about 1 m, plus what it can travel before the next refresh at sprint speed.</summary>
        const float ProxyRadius = 3f;

        /// <summary>Seconds between refreshes of the proxies around each ragdoll. WHY 0.2: at 9 m/s a player covers 1.8 m in that time, inside the radius above.</summary>
        const float ProxyRefresh = 0.2f;

        /// <summary>A proxy further than this from every ragdoll, metres, is dropped at the next sweep. Larger than <see cref="ProxyRadius"/> so a player pacing back and forth does not make and drop the same ones.</summary>
        const float ProxyKeepRadius = 8f;

        /// <summary>Seconds between sweeps for proxies no ragdoll is near.</summary>
        const float SweepInterval = 2f;

        struct ProxyEntry
        {
            public GameObject Proxy;
            public Bounds Bounds;
        }

        static Scene _scene;
        static PhysicsScene _physics;
        static readonly List<PlayerRagdoll> Ragdolls = new(8);
        static readonly Dictionary<Collider, ProxyEntry> Proxies = new(256);
        static readonly List<Collider> Stale = new(64);
        static readonly Collider[] Nearby = new Collider[256];
        static int _steppedFrame = -1;
        static float _leftover;
        static float _nextMatch;
        static float _nextSweep;

        /// <summary>How far between the last two steps the current frame falls, 0–1; ragdolls blend their bodies by this so motion does not step at the physics rate.</summary>
        public static float Blend { get; private set; } = 1f;

        /// <summary>The ragdoll scene, made on first use.</summary>
        static Scene Scene
        {
            get
            {
                if (!_scene.IsValid())
                {
                    _scene = SceneManager.CreateScene("Ragdolls", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                    _physics = _scene.GetPhysicsScene();
                }
                return _scene;
            }
        }

        /// <summary>Clears the statics when Play starts, so a second Play in the same Editor session (with domain reload off) does not reuse a dead scene.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _scene = default;
            _physics = default;
            Ragdolls.Clear();
            Proxies.Clear();
            Stale.Clear();
            _steppedFrame = -1;
            _leftover = 0f;
            _nextMatch = 0f;
            _nextSweep = 0f;
            Blend = 1f;
        }

        /// <summary>
        /// Moves a ragdoll's root object into the ragdoll world and starts stepping it.
        /// </summary>
        /// <param name="ragdoll">The ragdoll; its <see cref="PlayerRagdoll.BeforeStep"/> is called before every physics step.</param>
        /// <param name="root">The root object holding its rigidbodies. Must have no parent.</param>
        public static void Add(PlayerRagdoll ragdoll, GameObject root)
        {
            Scene target = Scene;
            if (root.scene != target)
                SceneManager.MoveGameObjectToScene(root, target);
            if (!Ragdolls.Contains(ragdoll))
                Ragdolls.Add(ragdoll);
        }

        /// <summary>Stops stepping a ragdoll. Unloads the ragdoll world when no ragdolls remain.</summary>
        public static void Remove(PlayerRagdoll ragdoll)
        {
            if (!Ragdolls.Remove(ragdoll) || Ragdolls.Count > 0)
                return;

            if (_scene.IsValid() && _scene.isLoaded && SceneManager.sceneCount > 1)
                SceneManager.UnloadSceneAsync(_scene);
            _scene = default;
            _physics = default;
            Proxies.Clear();
            Stale.Clear();
            _leftover = 0f;
            _nextMatch = 0f;
            _nextSweep = 0f;
        }

        /// <summary>
        /// Advances the ragdoll world by this frame's time. Safe to call from every ragdoll each
        /// frame: only the first call in a frame does anything.
        /// </summary>
        /// <param name="dt">Frame time, seconds.</param>
        public static void Step(float dt)
        {
            if (_steppedFrame == Time.frameCount || !_scene.IsValid())
                return;
            _steppedFrame = Time.frameCount;

            if (Time.time >= _nextMatch)
            {
                _nextMatch = Time.time + ProxyRefresh;
                MatchSwitchedOff();
            }
            _leftover = Mathf.Min(_leftover + dt, StepTime * MaxStepsPerFrame);
            while (_leftover >= StepTime)
            {
                for (int i = 0; i < Ragdolls.Count; i++)
                    Ragdolls[i].BeforeStep(StepTime);
                _physics.Simulate(StepTime);
                _leftover -= StepTime;
            }
            Blend = _leftover / StepTime;

            if (Time.time >= _nextSweep)
            {
                _nextSweep = Time.time + SweepInterval;
                Sweep();
            }
        }

        /// <summary>
        /// Copies the solid level colliders around a point into the ragdoll world, if they are
        /// not there already. Each ragdoll calls this for itself every <see cref="RefreshTime"/>.
        /// </summary>
        /// <param name="centre">Where the ragdoll is, world space.</param>
        /// <param name="solid">Which layers count as level geometry.</param>
        public static void CopyLevelAround(Vector3 centre, LayerMask solid)
        {
            int count = Physics.OverlapSphereNonAlloc(centre, ProxyRadius, Nearby, solid, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider source = Nearby[i];
                // Anything with a rigidbody moves; a copy would be left standing where it was.
                if (source.attachedRigidbody != null || Proxies.ContainsKey(source))
                    continue;
                Proxies.Add(source, new ProxyEntry { Proxy = MakeProxy(source), Bounds = source.bounds });
            }
        }

        /// <summary>Seconds between a ragdoll's calls to <see cref="CopyLevelAround"/>.</summary>
        public static float RefreshTime => ProxyRefresh;

        /// <summary>Makes a still copy of one collider in the ragdoll world, or null for an unsupported collider type.</summary>
        static GameObject MakeProxy(Collider source)
        {
            switch (source)
            {
                case BoxCollider _:
                case SphereCollider _:
                case CapsuleCollider _:
                    break;
                case MeshCollider mesh when mesh.sharedMesh != null:
                    break;
                default:
                    return null;
            }

            var proxy = new GameObject("Proxy");
            SceneManager.MoveGameObjectToScene(proxy, Scene);
            Transform from = source.transform;
            proxy.transform.SetPositionAndRotation(from.position, from.rotation);
            proxy.transform.localScale = from.lossyScale;

            switch (source)
            {
                case BoxCollider box:
                    var boxCopy = proxy.AddComponent<BoxCollider>();
                    boxCopy.center = box.center;
                    boxCopy.size = box.size;
                    break;
                case SphereCollider sphere:
                    var sphereCopy = proxy.AddComponent<SphereCollider>();
                    sphereCopy.center = sphere.center;
                    sphereCopy.radius = sphere.radius;
                    break;
                case CapsuleCollider capsule:
                    var capsuleCopy = proxy.AddComponent<CapsuleCollider>();
                    capsuleCopy.center = capsule.center;
                    capsuleCopy.radius = capsule.radius;
                    capsuleCopy.height = capsule.height;
                    capsuleCopy.direction = capsule.direction;
                    break;
                case MeshCollider mesh:
                    var meshCopy = proxy.AddComponent<MeshCollider>();
                    meshCopy.convex = mesh.convex;
                    meshCopy.cookingOptions = mesh.cookingOptions;
                    meshCopy.sharedMesh = mesh.sharedMesh;
                    break;
            }
            return proxy;
        }

        /// <summary>
        /// Switches each level copy off or on to match the collider it copies.
        /// Throttled to <see cref="ProxyRefresh"/> instead of checking every proxy every frame.
        /// </summary>
        static void MatchSwitchedOff()
        {
            foreach (KeyValuePair<Collider, ProxyEntry> pair in Proxies)
            {
                ProxyEntry entry = pair.Value;
                if (pair.Key == null || entry.Proxy == null)
                    continue;
                bool solid = pair.Key.enabled && pair.Key.gameObject.activeInHierarchy;
                if (entry.Proxy.activeSelf != solid)
                    entry.Proxy.SetActive(solid);
            }
        }

        /// <summary>Drops proxies whose collider is gone or that no ragdoll is near.</summary>
        static void Sweep()
        {
            Stale.Clear();
            foreach (KeyValuePair<Collider, ProxyEntry> pair in Proxies)
            {
                if (pair.Key == null)
                {
                    Stale.Add(pair.Key);
                    continue;
                }

                Bounds bounds = pair.Key.enabled && pair.Key.gameObject.activeInHierarchy
                    ? pair.Key.bounds
                    : pair.Value.Bounds;
                if (!NearAnyRagdoll(bounds))
                    Stale.Add(pair.Key);
            }

            for (int i = 0; i < Stale.Count; i++)
            {
                GameObject proxy = Proxies[Stale[i]].Proxy;
                if (proxy != null)
                    Object.Destroy(proxy);
                Proxies.Remove(Stale[i]);
            }
        }

        static bool NearAnyRagdoll(Bounds bounds)
        {
            for (int i = 0; i < Ragdolls.Count; i++)
            {
                if (bounds.SqrDistance(Ragdolls[i].Centre) < ProxyKeepRadius * ProxyKeepRadius)
                    return true;
            }
            return false;
        }
    }
}
