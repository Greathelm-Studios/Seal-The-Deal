using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Glass
{
    /// <summary>
    /// Plays the shard burst where a pane breaks. Local visuals only, never networked: every
    /// machine throws its own shards from the same break. The bursts are reused, so a wall
    /// of windows going at once allocates nothing once the pool has warmed up.
    /// </summary>
    public sealed class GlassShards : MonoBehaviour
    {
        const int MaxActiveBursts = 32;
        const int MaxPooledPerPrefab = 24;

        static GlassShards _instance;

        readonly Dictionary<ParticleSystem, Stack<ParticleSystem>> _free = new();
        readonly List<ParticleSystem> _playing = new(16);
        readonly List<ParticleSystem> _playingPrefabs = new(16);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
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
            for (int i = _playing.Count - 1; i >= 0; i--)
                RecycleAt(i);
        }

        /// <summary>
        /// Throws shards from a pane. centre and facing place the pane (facing looks the way
        /// the shards should fly); size is its width, height and thickness in metres.
        /// </summary>
        public static void Play(GlassProfile profile, Vector3 centre, Quaternion facing, Vector3 size)
        {
            if (profile == null || profile.ShardEffect == null)
                return;

            if (_instance == null)
            {
                var go = new GameObject("Glass Shards");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<GlassShards>();
            }

            _instance.Burst(profile, centre, facing, size);
        }

        void Burst(GlassProfile profile, Vector3 centre, Quaternion facing, Vector3 size)
        {
            ParticleSystem prefab = profile.ShardEffect;
            if (!_free.TryGetValue(prefab, out Stack<ParticleSystem> free))
            {
                free = new Stack<ParticleSystem>();
                _free.Add(prefab, free);
            }

            while (_playing.Count >= MaxActiveBursts)
                RecycleAt(0);

            ParticleSystem burst = free.Count > 0 ? free.Pop() : Instantiate(prefab, transform);
            burst.transform.SetPositionAndRotation(centre, facing);
            burst.gameObject.SetActive(true);

            ParticleSystem.ShapeModule shape = burst.shape;
            shape.scale = size;
            ParticleSystem.MainModule main = burst.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(profile.ShardSpeed * 0.3f, profile.ShardSpeed);

            int count = Mathf.Min(profile.MaxShards, Mathf.CeilToInt(size.x * size.y * profile.ShardsPerSquareMetre));
            burst.Emit(count);

            _playing.Add(burst);
            _playingPrefabs.Add(prefab);
        }

        void Update()
        {
            for (int i = _playing.Count - 1; i >= 0; i--)
            {
                ParticleSystem burst = _playing[i];
                if (burst != null && burst.particleCount > 0)
                    continue;

                RecycleAt(i);
            }
        }

        void RecycleAt(int index)
        {
            ParticleSystem burst = _playing[index];
            ParticleSystem prefab = _playingPrefabs[index];
            int last = _playing.Count - 1;
            _playing[index] = _playing[last];
            _playingPrefabs[index] = _playingPrefabs[last];
            _playing.RemoveAt(last);
            _playingPrefabs.RemoveAt(last);

            if (burst == null)
                return;

            burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            burst.gameObject.SetActive(false);

            if (_free.TryGetValue(prefab, out Stack<ParticleSystem> free) && free.Count < MaxPooledPerPrefab)
                free.Push(burst);
            else
                Destroy(burst.gameObject);
        }
    }
}
