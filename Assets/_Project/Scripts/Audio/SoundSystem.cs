using System.Collections.Generic;
using CloseTheDeal.Player;
using CloseTheDeal.UI;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Audio
{
    /// <summary>
    /// Central spatial audio manager: owns the shared 3D/2D voice pool, mixer bus lookup,
    /// zero-reaching distance curves, occlusion defaults, and fallback AudioListener management.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoundSystem : MonoBehaviour
    {
        const string ProfileResourcePath = "Audio/SpatialAudioProfile";
        const float ClearBypassHz = 21500f;
        const float ListenerCheckInterval = 0.5f;

        /// <summary>Active SoundSystem singleton, created automatically before the first scene loads.</summary>
        public static SoundSystem Instance { get; private set; }

        /// <summary>Optional designer profile loaded from Resources/Audio/SpatialAudioProfile.</summary>
        public static SpatialAudioProfile Profile { get; private set; }

        public static bool OcclusionEnabled => Profile != null ? Profile.EnableOcclusion : true;
        public static bool SpatializerEnabled
        {
            get
            {
                if (Profile != null && !Profile.EnableSpatializerPlugin)
                    return false;
                if (string.IsNullOrEmpty(UnityEngine.AudioSettings.GetSpatializerPluginName()))
                    return false;
#if STEAMAUDIO_ENABLED
                // Fall back to built-in Unity 3D panning if Steam Audio's native library failed to load.
                if (SteamAudio.SteamAudioManager.Singleton == null || SteamAudio.SteamAudioManager.Context == null)
                    return false;
#endif
                return true;
            }
        }
        public static bool VoiceBilinearHrtf => Profile == null || Profile.VoiceBilinearHrtf;
        public static float DefaultSpread => Profile != null ? Profile.DefaultSpread : 20f;
        public static float VoiceSpread => Profile != null ? Profile.VoiceSpread : 25f;
        public static float ClearCutoffHz => Profile != null ? Profile.ClearCutoffHz : 22000f;
        public static float SingleWallCutoffHz => Profile != null ? Profile.SingleWallCutoffHz : 1600f;
        public static float HeavyOcclusionCutoffHz => Profile != null ? Profile.HeavyOcclusionCutoffHz : 650f;
        public static float SingleWallGain => Profile != null ? Profile.SingleWallGain : 0.6f;
        public static float HeavyOcclusionGain => Profile != null ? Profile.HeavyOcclusionGain : 0.28f;
        public static float OcclusionSmoothSeconds => Profile != null ? Profile.OcclusionSmoothSeconds : 0.12f;
        public static float OcclusionCheckHz => Profile != null ? Profile.OcclusionCheckHz : 12f;
        public static float MinBarrierSeparation => Profile != null ? Profile.MinBarrierSeparation : 0.25f;

        struct PooledVoice
        {
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public int Priority;
        }

        static readonly Dictionary<string, AudioMixerGroup> _mixerGroups = new();
        static readonly Dictionary<long, AnimationCurve> _rolloffCurves = new();
        static AudioMixer _mixer;

        PooledVoice[] _pool;
        int _nextVoice;
        AudioSource _flatSource;
        AudioListener _fallbackListener;
        AudioListener _sceneListener;
        Camera _mainCamera;
        float _nextListenerCheck;
        bool _sceneDirty = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            Ensure();
        }

        /// <summary>Returns the active SoundSystem, creating it if needed during Play Mode.</summary>
        public static SoundSystem Ensure()
        {
            if (Instance != null)
                return Instance;
            if (!Application.isPlaying)
                return null;

            Profile = Resources.Load<SpatialAudioProfile>(ProfileResourcePath);

            var go = new GameObject("SoundSystem");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<SoundSystem>();
            Instance.Initialize();
            return Instance;
        }

        void Initialize()
        {
            int poolSize = Profile != null ? Mathf.Clamp(Profile.PooledVoices, 8, 64) : 32;
            _pool = new PooledVoice[poolSize];

            var poolRoot = new GameObject("Spatial Pool");
            poolRoot.transform.SetParent(transform, false);
            for (int i = 0; i < poolSize; i++)
            {
                var child = new GameObject("Voice " + i);
                child.transform.SetParent(poolRoot.transform, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                Configure3D(source, 2f, 40f, 128, DefaultSpread, false);

                AudioLowPassFilter lowPass = child.AddComponent<AudioLowPassFilter>();
                lowPass.cutoffFrequency = ClearCutoffHz;
                lowPass.lowpassResonanceQ = 1f;
                lowPass.enabled = false;

                _pool[i] = new PooledVoice { Source = source, LowPass = lowPass, Priority = 128 };
            }

            _flatSource = gameObject.AddComponent<AudioSource>();
            _flatSource.playOnAwake = false;
            _flatSource.loop = false;
            _flatSource.spatialBlend = 0f;
            _flatSource.priority = 64;

            _fallbackListener = gameObject.AddComponent<AudioListener>();
            _fallbackListener.enabled = false;

            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneDirty = true;
        }

        void Start()
        {
            RefreshListener();
            if (_pool == null)
                return;

            bool hrtfReady = SpatializerEnabled;
            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i].Source.spatialize = hrtfReady;
                AttachSteamAudio(_pool[i].Source, bilinearHrtf: false);
            }
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
                Instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _sceneListener = null;
            _mainCamera = null;
            _sceneDirty = true;
        }

        void Update()
        {
            if (_sceneDirty || Time.unscaledTime >= _nextListenerCheck)
            {
                _sceneDirty = false;
                _nextListenerCheck = Time.unscaledTime + ListenerCheckInterval;
                RefreshListener();
            }

            if (_fallbackListener != null && _fallbackListener.enabled)
            {
                if (_mainCamera == null)
                    _mainCamera = Camera.main;
                if (_mainCamera != null)
                {
                    transform.SetPositionAndRotation(_mainCamera.transform.position, _mainCamera.transform.rotation);
                }
            }
        }

        void RefreshListener()
        {
            if (_sceneListener != null && (!_sceneListener.isActiveAndEnabled || _sceneListener == _fallbackListener))
                _sceneListener = null;

            if (_sceneListener == null)
            {
                AudioListener[] listeners = FindObjectsByType<AudioListener>();
                for (int i = 0; i < listeners.Length; i++)
                {
                    AudioListener candidate = listeners[i];
                    if (candidate != null && candidate != _fallbackListener && candidate.isActiveAndEnabled)
                    {
                        _sceneListener = candidate;
                        break;
                    }
                }
            }

            // Guarantee at least one active AudioListener in scenes like KitOffice or Lane_Title,
            // and turn the fallback off immediately whenever a real scene listener exists.
            bool wantFallback = _sceneListener == null;
            bool listenerChanged = _fallbackListener != null && _fallbackListener.enabled != wantFallback;
            if (_fallbackListener != null)
                _fallbackListener.enabled = wantFallback;

#if STEAMAUDIO_ENABLED
            if (listenerChanged && SteamAudio.SteamAudioManager.Singleton != null)
                SteamAudio.SteamAudioManager.NotifyAudioListenerChanged();
#endif
        }

        /// <summary>Returns the current active listener world position for distance culling and occlusion.</summary>
        public static bool TryGetListenerPosition(out Vector3 position)
        {
            if (PlayerCamera.Instance != null)
            {
                position = PlayerCamera.Instance.transform.position;
                return true;
            }

            SoundSystem sys = Instance;
            if (sys != null)
            {
                if (sys._sceneListener != null && sys._sceneListener.isActiveAndEnabled)
                {
                    position = sys._sceneListener.transform.position;
                    return true;
                }

                if (sys._mainCamera == null)
                    sys._mainCamera = Camera.main;
                if (sys._mainCamera != null)
                {
                    position = sys._mainCamera.transform.position;
                    return true;
                }
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                position = cam.transform.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        /// <summary>Finds and caches an AudioMixerGroup by name on the game's master mixer.</summary>
        public static AudioMixerGroup GetMixerGroup(string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
                return null;

            if (_mixerGroups.TryGetValue(groupName, out AudioMixerGroup cached) && cached != null)
                return cached;

            if (_mixer == null)
            {
                UiSoundSet set = UiSoundPlayer.Set;
                if (set != null && set.output != null)
                    _mixer = set.output.audioMixer;
            }

            if (_mixer == null)
                return null;

            AudioMixerGroup[] matches = _mixer.FindMatchingGroups(groupName);
            AudioMixerGroup found = matches.Length > 0 ? matches[0] : null;
            if (found != null)
                _mixerGroups[groupName] = found;
            return found;
        }

        /// <summary>Plays a 3D one-shot clip from the shared spatial pool with distance culling and wall/floor occlusion.</summary>
        public static void PlayAt(
            AudioClip clip,
            Vector3 position,
            AudioMixerGroup output = null,
            float volume = 1f,
            float nearDistance = 2f,
            float farDistance = 40f,
            float pitch = 1f,
            int priority = 128,
            bool useOcclusion = true)
        {
            if (clip == null || volume <= 0f)
                return;

            SoundSystem sys = Ensure();
            if (sys == null)
                return;

            bool hasListener = TryGetListenerPosition(out Vector3 listenerPos);
            if (hasListener && (position - listenerPos).sqrMagnitude > farDistance * farDistance)
                return;

            int index = sys.AcquireVoice(listenerPos, hasListener, priority);
            ref PooledVoice voice = ref sys._pool[index];
            voice.Priority = priority;

            AudioSource src = voice.Source;
            src.Stop();
            src.transform.position = position;
            src.outputAudioMixerGroup = output != null ? output : GetMixerGroup("SFX");
            ApplyDistanceCurve(src, nearDistance, farDistance);
            src.priority = priority;
            src.pitch = pitch;
            src.spatialize = SpatializerEnabled;

            float occlusionGain = 1f;
            float cutoffHz = ClearCutoffHz;
            if (useOcclusion && hasListener)
                SpatialOcclusion.Sample(listenerPos, position, out occlusionGain, out cutoffHz);

            bool occluded = cutoffHz < ClearBypassHz;
            if (voice.LowPass != null)
            {
                voice.LowPass.enabled = occluded;
                if (occluded)
                    voice.LowPass.cutoffFrequency = cutoffHz;
            }

            // Assign clip + Play() instead of PlayOneShot() so Steam Audio Spatializer processes
            // the clip cleanly and Unity does not warn "Only custom filters can be played".
            src.clip = clip;
            src.volume = volume * occlusionGain;
            src.Play();
        }

        /// <summary>Plays a 2D (non-spatial) one-shot clip for local player feedback (hurt, death, respawn).</summary>
        public static void PlayFlat(AudioClip clip, AudioMixerGroup output = null, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || volume <= 0f)
                return;

            SoundSystem sys = Ensure();
            if (sys == null || sys._flatSource == null)
                return;

            sys._flatSource.outputAudioMixerGroup = output != null ? output : GetMixerGroup("SFX");
            sys._flatSource.pitch = pitch;
            sys._flatSource.PlayOneShot(clip, volume);
        }

        /// <summary>Configures a persistent 3D AudioSource with zero-reaching rolloff and optional dynamic occlusion.</summary>
        public static SpatialOcclusion ConfigurePersistent3D(
            AudioSource source,
            AudioMixerGroup output,
            float nearDistance,
            float farDistance,
            int priority = 96,
            bool useOcclusion = true,
            float spread = -1f,
            bool postEffectsSpatialize = false)
        {
            if (source == null)
                return null;

            source.outputAudioMixerGroup = output != null ? output : GetMixerGroup("SFX");
            float actualSpread = spread >= 0f ? spread : DefaultSpread;
            Configure3D(source, nearDistance, farDistance, priority, actualSpread, postEffectsSpatialize);
            AttachSteamAudio(source, bilinearHrtf: postEffectsSpatialize && VoiceBilinearHrtf);

            if (!useOcclusion)
                return null;

            SpatialOcclusion occlusion = source.GetComponent<SpatialOcclusion>();
            if (occlusion == null)
                occlusion = source.gameObject.AddComponent<SpatialOcclusion>();
            return occlusion;
        }

        static void AttachSteamAudio(AudioSource source, bool bilinearHrtf)
        {
#if STEAMAUDIO_ENABLED
            if (source == null || !SpatializerEnabled || SteamAudio.SteamAudioManager.Singleton == null)
                return;

            SteamAudio.SteamAudioSource sa = source.GetComponent<SteamAudio.SteamAudioSource>();
            if (sa == null)
                sa = source.gameObject.AddComponent<SteamAudio.SteamAudioSource>();

            sa.directBinaural = true;
            sa.interpolation = bilinearHrtf
                ? SteamAudio.HRTFInterpolation.Bilinear
                : SteamAudio.HRTFInterpolation.Nearest;
            sa.distanceAttenuation = false;
            sa.occlusion = false;
            sa.transmission = false;
            sa.reflections = false;
            sa.pathing = false;
#endif
        }

        /// <summary>Applies consistent 3D spatial settings and a smooth rolloff curve that reaches true zero at farDistance.</summary>
        public static void Configure3D(
            AudioSource source,
            float nearDistance,
            float farDistance,
            int priority = 128,
            float spread = 20f,
            bool postEffectsSpatialize = false)
        {
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.spread = spread;
            source.priority = Mathf.Clamp(priority, 0, 256);
            source.spatialize = SpatializerEnabled;
            source.spatializePostEffects = SpatializerEnabled && postEffectsSpatialize;
            ApplyDistanceCurve(source, nearDistance, farDistance);
        }

        static void ApplyDistanceCurve(AudioSource source, float nearDistance, float farDistance)
        {
            float near = Mathf.Max(0.1f, nearDistance);
            float far = Mathf.Max(near + 0.5f, farDistance);

            if (source.rolloffMode == AudioRolloffMode.Custom
                && Mathf.Approximately(source.minDistance, near)
                && Mathf.Approximately(source.maxDistance, far))
            {
                return;
            }

            source.rolloffMode = AudioRolloffMode.Custom;
            source.minDistance = near;
            source.maxDistance = far;

            // Cache by quantized near/far distances so pooled voices never allocate curves on hot paths.
            long key = ((long)Mathf.RoundToInt(near * 100f) << 32) | (uint)Mathf.RoundToInt(far * 100f);
            if (!_rolloffCurves.TryGetValue(key, out AnimationCurve curve))
            {
                float nearT = Mathf.Clamp01(near / far);
                float midT = Mathf.Lerp(nearT, 1f, 0.45f);
                curve = new AnimationCurve(
                    new Keyframe(0f, 1f, 0f, 0f),
                    new Keyframe(nearT, 1f, 0f, -1.2f),
                    new Keyframe(midT, 0.35f, -0.9f, -0.7f),
                    new Keyframe(1f, 0f, -0.25f, 0f));
                _rolloffCurves[key] = curve;
            }
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
        }

        int AcquireVoice(Vector3 listenerPos, bool hasListener, int requestedPriority)
        {
            int count = _pool.Length;
            for (int i = 0; i < count; i++)
            {
                int idx = (_nextVoice + i) % count;
                if (!_pool[idx].Source.isPlaying)
                {
                    _nextVoice = (idx + 1) % count;
                    return idx;
                }
            }

            // All voices busy: steal the lowest-priority (highest priority number) farthest voice.
            int victim = _nextVoice;
            float worstScore = -1f;
            for (int i = 0; i < count; i++)
            {
                float distSqr = hasListener
                    ? (_pool[i].Source.transform.position - listenerPos).sqrMagnitude
                    : 1f;
                float priorityBias = _pool[i].Priority >= requestedPriority ? 10000f : 0f;
                float score = priorityBias + distSqr;
                if (score > worstScore)
                {
                    worstScore = score;
                    victim = i;
                }
            }

            _nextVoice = (victim + 1) % count;
            return victim;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            Profile = null;
            _mixer = null;
            _mixerGroups.Clear();
            _rolloffCurves.Clear();
        }
    }
}
