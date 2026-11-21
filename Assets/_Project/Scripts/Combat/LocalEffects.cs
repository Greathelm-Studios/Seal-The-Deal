using System.Collections.Generic;
using CloseTheDeal.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Plays one-shot particle effects (bullet impacts) at a point. Local visuals only, never
    /// networked. Effects are reused, so sustained fire allocates nothing once warmed up.
    /// </summary>
    public sealed class LocalEffects : MonoBehaviour
    {
        const int MaxPlaying = 32;
        const int MaxPooledPerPrefab = 24;
        const float MaxDistance = 60f;

        static LocalEffects _instance;
        static Camera _mainCamera;

        readonly Dictionary<ParticleSystem, Stack<ParticleSystem>> _free = new();
        readonly List<ParticleSystem> _playing = new(32);
        readonly List<ParticleSystem> _playingPrefabs = new(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _mainCamera = null;
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
            _mainCamera = null;
            for (int i = _playing.Count - 1; i >= 0; i--)
                RecycleAt(i);
        }

        /// <summary>Plays the effect at a point, facing along the given direction (for an impact, away from the surface).</summary>
        public static void Play(ParticleSystem prefab, Vector3 position, Vector3 facing)
        {
            if (prefab == null)
                return;

            if (TryCameraPosition(out Vector3 camPos) && (position - camPos).sqrMagnitude > MaxDistance * MaxDistance)
                return;

            if (_instance == null)
            {
                var go = new GameObject("Local Effects");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<LocalEffects>();
            }

            _instance.Spawn(prefab, position, facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : Quaternion.identity);
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

        void Spawn(ParticleSystem prefab, Vector3 position, Quaternion rotation)
        {
            if (!_free.TryGetValue(prefab, out Stack<ParticleSystem> free))
            {
                free = new Stack<ParticleSystem>();
                _free.Add(prefab, free);
            }

            while (_playing.Count >= MaxPlaying)
                RecycleAt(0);

            ParticleSystem effect = free.Count > 0 ? free.Pop() : Instantiate(prefab, transform);
            effect.transform.SetPositionAndRotation(position, rotation);
            effect.gameObject.SetActive(true);
            effect.Play(true);
            _playing.Add(effect);
            _playingPrefabs.Add(prefab);
        }

        void Update()
        {
            for (int i = _playing.Count - 1; i >= 0; i--)
            {
                ParticleSystem effect = _playing[i];
                if (effect != null && effect.IsAlive(true))
                    continue;

                RecycleAt(i);
            }
        }

        void RecycleAt(int index)
        {
            ParticleSystem effect = _playing[index];
            ParticleSystem prefab = _playingPrefabs[index];
            int last = _playing.Count - 1;
            _playing[index] = _playing[last];
            _playingPrefabs[index] = _playingPrefabs[last];
            _playing.RemoveAt(last);
            _playingPrefabs.RemoveAt(last);

            if (effect == null)
                return;

            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.gameObject.SetActive(false);

            if (_free.TryGetValue(prefab, out Stack<ParticleSystem> free) && free.Count < MaxPooledPerPrefab)
                free.Push(effect);
            else
                Destroy(effect.gameObject);
        }
    }
}
