using System.Collections.Generic;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Plays one-shot particle effects (bullet impacts) at a point. Local visuals only, never
    /// networked. Effects are reused, so sustained fire allocates nothing once warmed up.
    /// </summary>
    public sealed class LocalEffects : MonoBehaviour
    {
        static LocalEffects _instance;

        readonly Dictionary<ParticleSystem, Stack<ParticleSystem>> _free = new();
        readonly List<ParticleSystem> _playing = new(32);
        readonly List<ParticleSystem> _playingPrefabs = new(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
        }

        /// <summary>Plays the effect at a point, facing along the given direction (for an impact, away from the surface).</summary>
        public static void Play(ParticleSystem prefab, Vector3 position, Vector3 facing)
        {
            if (prefab == null)
                return;

            if (_instance == null)
            {
                var go = new GameObject("Local Effects");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<LocalEffects>();
            }

            _instance.Spawn(prefab, position, facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : Quaternion.identity);
        }

        void Spawn(ParticleSystem prefab, Vector3 position, Quaternion rotation)
        {
            if (!_free.TryGetValue(prefab, out Stack<ParticleSystem> free))
            {
                free = new Stack<ParticleSystem>();
                _free.Add(prefab, free);
            }

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
                if (effect.IsAlive(true))
                    continue;

                effect.gameObject.SetActive(false);
                _free[_playingPrefabs[i]].Push(effect);
                int last = _playing.Count - 1;
                _playing[i] = _playing[last];
                _playingPrefabs[i] = _playingPrefabs[last];
                _playing.RemoveAt(last);
                _playingPrefabs.RemoveAt(last);
            }
        }
    }
}
