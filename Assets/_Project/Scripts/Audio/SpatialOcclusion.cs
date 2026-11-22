using UnityEngine;

namespace CloseTheDeal.Audio
{
    /// <summary>
    /// Dynamically muffles a persistent 3D AudioSource when walls, floors, or unbroken glass sit
    /// between it and the listener. Also provides a zero-allocation static sampler for one-shots.
    /// </summary>
    // Must sit after AudioSource (and after any OnAudioFilterRead component like VoiceSpeaker)
    // so AudioLowPassFilter filters the final audio signal rather than a carrier waveform.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class SpatialOcclusion : MonoBehaviour
    {
        const float ClearBypassHz = 21500f;
        const float MinOcclusionDistance = 0.35f;
        const float RayEndMargin = 0.15f;
        const int MaxHits = 8;

        static readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        static readonly float[] _hitDistances = new float[MaxHits];
        static int _wallMask;
        static bool _maskBuilt;
        static int _staggerCounter;

        AudioSource _source;
        AudioLowPassFilter _lowPass;
        float _baseVolume = 1f;
        float _currentGain = 1f;
        float _targetGain = 1f;
        float _currentCutoff = 22000f;
        float _targetCutoff = 22000f;
        float _nextCheckTime;
        bool _initialized;

        /// <summary>Unoccluded base volume controlled by the owning script (0 to 1).</summary>
        public float BaseVolume
        {
            get => _baseVolume;
            set
            {
                _baseVolume = Mathf.Clamp01(value);
                ApplyVolume();
            }
        }

        /// <summary>Current occlusion gain multiplier (1 = clear, lower = muffled).</summary>
        public float CurrentGain => _currentGain;

        /// <summary>Current low-pass cutoff in Hz.</summary>
        public float CurrentCutoffHz => _currentCutoff;

        void Awake()
        {
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (_initialized)
                return;

            _initialized = true;
            _source = GetComponent<AudioSource>();
            _baseVolume = _source.volume;

            // Reuse an existing filter or append one after AudioSource and any OnAudioFilterRead script.
            _lowPass = GetComponent<AudioLowPassFilter>();
            if (_lowPass == null)
                _lowPass = gameObject.AddComponent<AudioLowPassFilter>();

            _currentCutoff = SoundSystem.ClearCutoffHz;
            _targetCutoff = _currentCutoff;
            _lowPass.cutoffFrequency = _currentCutoff;
            _lowPass.lowpassResonanceQ = 1f;
            _lowPass.enabled = false;

            float interval = 1f / Mathf.Max(2f, SoundSystem.OcclusionCheckHz);
            _nextCheckTime = Time.unscaledTime + ((_staggerCounter++ & 7) * (interval * 0.125f));
        }

        /// <summary>Resets occlusion immediately to clear (for example when switching a source to 2D).</summary>
        public void ResetClear()
        {
            EnsureComponents();
            _currentGain = 1f;
            _targetGain = 1f;
            _currentCutoff = SoundSystem.ClearCutoffHz;
            _targetCutoff = _currentCutoff;
            if (_lowPass != null)
            {
                _lowPass.cutoffFrequency = _currentCutoff;
                _lowPass.enabled = false;
            }
            ApplyVolume();
        }

        /// <summary>Samples occlusion immediately without smoothing (useful when starting playback).</summary>
        public void SnapNow()
        {
            EnsureComponents();
            if (_source == null || _source.spatialBlend < 0.5f || !SoundSystem.OcclusionEnabled)
            {
                ResetClear();
                return;
            }

            if (SoundSystem.TryGetListenerPosition(out Vector3 listenerPos))
            {
                Sample(listenerPos, transform.position, out _targetGain, out _targetCutoff);
                _currentGain = _targetGain;
                _currentCutoff = _targetCutoff;
                ApplyFilter();
                ApplyVolume();
            }
        }

        void Update()
        {
            if (_source == null)
                return;

            if (_source.spatialBlend < 0.5f || !SoundSystem.OcclusionEnabled)
            {
                if (_lowPass != null && _lowPass.enabled || _currentGain < 0.999f)
                    ResetClear();
                return;
            }

            if (!_source.isPlaying)
                return;

            float now = Time.unscaledTime;
            if (now >= _nextCheckTime)
            {
                _nextCheckTime = now + 1f / Mathf.Max(2f, SoundSystem.OcclusionCheckHz);
                if (SoundSystem.TryGetListenerPosition(out Vector3 listenerPos))
                    Sample(listenerPos, transform.position, out _targetGain, out _targetCutoff);
                else
                {
                    _targetGain = 1f;
                    _targetCutoff = SoundSystem.ClearCutoffHz;
                }
            }

            float smooth = SoundSystem.OcclusionSmoothSeconds;
            float t = smooth > 0.001f ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / smooth) : 1f;
            _currentGain = Mathf.Lerp(_currentGain, _targetGain, t);
            _currentCutoff = Mathf.Lerp(_currentCutoff, _targetCutoff, t);

            ApplyFilter();
            ApplyVolume();
        }

        void ApplyFilter()
        {
            if (_lowPass == null)
                return;

            bool occluded = _currentCutoff < ClearBypassHz;
            if (_lowPass.enabled != occluded)
                _lowPass.enabled = occluded;
            if (occluded)
                _lowPass.cutoffFrequency = _currentCutoff;
        }

        void ApplyVolume()
        {
            if (_source != null)
                _source.volume = _baseVolume * _currentGain;
        }

        /// <summary>Zero-allocation raycast check returning occlusion gain and low-pass cutoff between two world points.</summary>
        public static void Sample(Vector3 listenerPos, Vector3 sourcePos, out float gain, out float cutoffHz)
        {
            float clearCutoff = SoundSystem.ClearCutoffHz;
            if (!SoundSystem.OcclusionEnabled)
            {
                gain = 1f;
                cutoffHz = clearCutoff;
                return;
            }

            Vector3 delta = sourcePos - listenerPos;
            float distance = delta.magnitude;
            if (distance <= MinOcclusionDistance)
            {
                gain = 1f;
                cutoffHz = clearCutoff;
                return;
            }

            int mask = GetWallMask();
            int hitCount = Physics.RaycastNonAlloc(
                listenerPos,
                delta / distance,
                _hits,
                distance - RayEndMargin,
                mask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
            {
                gain = 1f;
                cutoffHz = clearCutoff;
                return;
            }

            int barriers = CountDistinctBarriers(hitCount, SoundSystem.MinBarrierSeparation);
            if (barriers <= 0)
            {
                gain = 1f;
                cutoffHz = clearCutoff;
            }
            else if (barriers == 1)
            {
                gain = SoundSystem.SingleWallGain;
                cutoffHz = SoundSystem.SingleWallCutoffHz;
            }
            else
            {
                gain = SoundSystem.HeavyOcclusionGain;
                cutoffHz = SoundSystem.HeavyOcclusionCutoffHz;
            }
        }

        static int CountDistinctBarriers(int hitCount, float minSeparation)
        {
            int n = Mathf.Min(hitCount, MaxHits);
            for (int i = 0; i < n; i++)
                _hitDistances[i] = _hits[i].distance;

            // Tiny insertion sort (n <= 8) so double-sided wall faces merge into one barrier.
            for (int i = 1; i < n; i++)
            {
                float key = _hitDistances[i];
                int j = i - 1;
                while (j >= 0 && _hitDistances[j] > key)
                {
                    _hitDistances[j + 1] = _hitDistances[j];
                    j--;
                }
                _hitDistances[j + 1] = key;
            }

            int barriers = 1;
            float last = _hitDistances[0];
            for (int i = 1; i < n; i++)
            {
                if (_hitDistances[i] - last >= minSeparation)
                {
                    barriers++;
                    last = _hitDistances[i];
                    if (barriers >= 2)
                        break;
                }
            }

            return barriers;
        }

        static int GetWallMask()
        {
            if (_maskBuilt)
                return _wallMask;

            _maskBuilt = true;
            int exclude = 0;
            ExcludeLayer(ref exclude, "Player");
            ExcludeLayer(ref exclude, "Prop");
            ExcludeLayer(ref exclude, "Viewmodel");
            ExcludeLayer(ref exclude, "Ignore Raycast");
            ExcludeLayer(ref exclude, "UI");
            ExcludeLayer(ref exclude, "TransparentFX");
            _wallMask = ~exclude;
            return _wallMask;
        }

        static void ExcludeLayer(ref int mask, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
                mask |= 1 << layer;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _maskBuilt = false;
            _wallMask = 0;
            _staggerCounter = 0;
        }
    }
}
