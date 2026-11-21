using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Player
{
    /// <summary>Private 120 Hz physics scene for client-local visual ragdolls and nearby static level collider proxies.</summary>
    public static class RagdollWorld
    {
        public const float StepTime = 1f / 120f;
        const int MaxStepsPerFrame = 4;
        const float ProxyRadius = 3f;
        const float ProxyRefresh = 0.2f;
        const float ProxyKeepRadius = 8f;
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

        /// <summary>Interpolation factor (0..1) between the last two 120 Hz physics steps.</summary>
        public static float Blend { get; private set; } = 1f;

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

        // Clears statics when entering Play with domain reload disabled.
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

        /// <summary>Moves a parentless ragdoll root into the ragdoll scene and begins stepping it.</summary>
        public static void Add(PlayerRagdoll ragdoll, GameObject root)
        {
            Scene target = Scene;
            if (root.scene != target)
                SceneManager.MoveGameObjectToScene(root, target);
            if (!Ragdolls.Contains(ragdoll))
                Ragdolls.Add(ragdoll);
        }

        /// <summary>Stops stepping a ragdoll and unloads the ragdoll scene when empty.</summary>
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

        /// <summary>Advances the ragdoll world once per frame (first caller each frame steps it).</summary>
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

        /// <summary>Copies static level colliders near <paramref name="centre"/> into the ragdoll scene.</summary>
        public static void CopyLevelAround(Vector3 centre, LayerMask solid)
        {
            int count = Physics.OverlapSphereNonAlloc(centre, ProxyRadius, Nearby, solid, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider source = Nearby[i];
                // Skip dynamic colliders; static copies would freeze in place.
                if (source.attachedRigidbody != null || Proxies.ContainsKey(source))
                    continue;
                Proxies.Add(source, new ProxyEntry { Proxy = MakeProxy(source), Bounds = source.bounds });
            }
        }

        public static float RefreshTime => ProxyRefresh;

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
