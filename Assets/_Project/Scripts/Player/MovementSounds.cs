using CloseTheDeal.Audio;
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Plays one body's movement sounds from its motor's events, on every machine: footsteps by
    /// distance travelled, jump, landing by fall speed, the slide's start, loop and stop, climbs and
    /// vaults, and (for the local player only) wind at speed. Your own body is heard flat; other
    /// players are heard where they are, with wall/floor occlusion via SoundSystem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovementSounds : MonoBehaviour
    {
        const float StepMinSpeed = 1f;
        const int MovementPriority = 144;

        PlayerMotor _motor;
        MovementSoundSet _set;
        AudioSource _oneShots;
        AudioSource _slideLoop;
        AudioSource _windLoop;
        SpatialOcclusion _oneShotOcclusion;
        SpatialOcclusion _slideOcclusion;
        float _stepDistance;
        int _lastStep = -1;

        public void Init(PlayerMotor motor, MovementSoundSet set)
        {
            _motor = motor;
            _set = set;
            _oneShots = NewSource("Movement one-shots", false, true, out _oneShotOcclusion);
            _slideLoop = NewSource("Movement slide loop", true, true, out _slideOcclusion);
            _slideLoop.clip = set.SlideLoop;
            _motor.Jumped += OnJumped;
            _motor.Landed += OnLanded;
            _motor.SlideChanged += OnSlideChanged;
            _motor.MantleStarted += OnMantleStarted;
        }

        void OnDestroy()
        {
            if (_motor == null)
                return;
            _motor.Jumped -= OnJumped;
            _motor.Landed -= OnLanded;
            _motor.SlideChanged -= OnSlideChanged;
            _motor.MantleStarted -= OnMantleStarted;
        }

        AudioSource NewSource(string name, bool loop, bool spatial, out SpatialOcclusion occlusion)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            if (spatial)
            {
                occlusion = SoundSystem.ConfigurePersistent3D(
                    source,
                    _set.Output,
                    2f,
                    _set.HearingDistance,
                    priority: MovementPriority,
                    useOcclusion: true);
            }
            else
            {
                source.outputAudioMixerGroup = _set.Output;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                occlusion = null;
            }
            return source;
        }

        /// <summary>Your own body is heard flat (it is where you are); others are placed in the world.</summary>
        bool IsLocal => _motor == PlayerMotor.Local;

        /// <summary>One of the variations at random, never the same as last time when there are several. Null when none.</summary>
        AudioClip Pick(AudioClip[] clips, ref int last)
        {
            if (clips == null || clips.Length == 0)
                return null;
            int pick = Random.Range(0, clips.Length);
            if (clips.Length > 1 && pick == last)
                pick = (pick + 1) % clips.Length;
            last = pick;
            return clips[pick];
        }

        int _lastJump = -1, _lastLand = -1, _lastClimb = -1;

        void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null || volume <= 0f)
                return;
            bool local = IsLocal;
            _oneShots.spatialBlend = local ? 0f : 1f;
            _oneShots.spatialize = !local && SoundSystem.SpatializerEnabled;
            if (local)
                _oneShotOcclusion?.ResetClear();
            else
                _oneShotOcclusion?.SnapNow();
            _oneShots.PlayOneShot(clip, _set.Volume * volume);
        }

        void OnJumped() => Play(Pick(_set.Jump, ref _lastJump));

        void OnLanded(float fallSpeed)
        {
            AudioClip[] set = fallSpeed >= _set.HeavyLandSpeed && _set.LandHeavy.Length > 0 ? _set.LandHeavy : _set.LandLight;
            AudioClip clip = Pick(set, ref _lastLand);
            Play(clip, Mathf.Clamp01(fallSpeed / _set.FullVolumeLandSpeed));
            _stepDistance = 0f;
        }

        void OnSlideChanged(bool sliding)
        {
            Play(sliding ? _set.SlideStart : _set.SlideStop);
            if (_slideLoop.clip == null)
                return;
            if (sliding)
            {
                bool local = IsLocal;
                _slideLoop.spatialBlend = local ? 0f : 1f;
                _slideLoop.spatialize = !local && SoundSystem.SpatializerEnabled;
                if (_slideOcclusion != null)
                {
                    _slideOcclusion.BaseVolume = _set.Volume;
                    if (local)
                        _slideOcclusion.ResetClear();
                    else
                        _slideOcclusion.SnapNow();
                }
                else
                {
                    _slideLoop.volume = _set.Volume;
                }
                _slideLoop.Play();
            }
            else
            {
                _slideLoop.Stop();
            }
        }

        void OnMantleStarted(MantleKind kind) => Play(Pick(kind == MantleKind.Vault ? _set.Vault : _set.Mantle, ref _lastClimb));

        /// <summary>Steps follow the distance travelled on foot, so they speed up and slow down with the body. No allocation.</summary>
        void Update()
        {
            if (_motor == null || !_motor.IsSpawned)
                return;

            PlayerAnimState state = _motor.AnimState;
            Footsteps(state);
            Wind(state);
        }

        void Footsteps(in PlayerAnimState state)
        {
            if (!state.Grounded || state.Sliding || state.PlanarSpeed < StepMinSpeed)
                return;

            MovementProfile profile = _motor.Profile;
            float pace = Mathf.InverseLerp(profile.WalkSpeed, profile.SprintSpeed, state.PlanarSpeed);
            float stride = Mathf.Lerp(_set.WalkStride, _set.RunStride, pace);
            _stepDistance += state.PlanarSpeed * Time.deltaTime;
            if (_stepDistance < stride)
                return;

            _stepDistance -= stride;
            AudioClip[] steps = pace > 0.5f && _set.FootstepRun.Length > 0 ? _set.FootstepRun : _set.FootstepWalk;
            if (steps.Length == 0)
                return;

            Play(Pick(steps, ref _lastStep), _set.FootstepVolume * Mathf.Lerp(0.7f, 1f, pace));
        }

        void Wind(in PlayerAnimState state)
        {
            if (!IsLocal || _set.WindLoop == null)
            {
                if (_windLoop != null && _windLoop.isPlaying)
                    _windLoop.Stop();
                return;
            }

            float level = Mathf.InverseLerp(_set.WindFromSpeed, _set.WindFullSpeed, state.PlanarSpeed);
            if (level <= 0f)
            {
                if (_windLoop != null && _windLoop.isPlaying)
                    _windLoop.Stop();
                return;
            }

            if (_windLoop == null)
            {
                _windLoop = NewSource("Movement wind", true, false, out _);
                _windLoop.clip = _set.WindLoop;
            }
            _windLoop.volume = _set.Volume * level;
            if (!_windLoop.isPlaying)
                _windLoop.Play();
        }
    }
}
