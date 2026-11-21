using System.Collections.Generic;
using CloseTheDeal.Glass;
using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Plays a player's gun and body sounds on this machine: shots, dry fire, the reload's stages,
    /// the draw, where shots land, and (for your own player) hurt, death and respawn. Listens to
    /// WeaponHolder and Health, decides nothing, sends nothing. Voices are pooled, so a full-auto
    /// burst creates nothing. One per player, on the player root. Glass shatter is played by the
    /// first CombatSounds alive, once per pane.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponHolder), typeof(Health))]
    public sealed class CombatSounds : MonoBehaviour
    {
        [Tooltip("The shared combat sounds and their mixer bus. Each gun's shot and reload sounds are on the gun itself.")]
        [SerializeField] CombatSoundSet _set;

        /// <summary>Reload stage times as fractions of the reload: magazine out, magazine in, slide or charge.</summary>
        const float MagazineOutAt = 0.15f;
        const float MagazineInAt = 0.6f;
        const float ReloadEndAt = 0.85f;
        const int Voices = 32;

        static AudioSource[] _voices;
        static int _next;
        static CombatSounds _glassVoice;
        static Camera _mainCamera;
        static readonly List<CombatSounds> _active = new();

        WeaponHolder _holder;
        Health _health;
        AudioSource _flat;
        WeaponDefinition _reloadWeapon;
        float _reloadStart;
        float _reloadSeconds;
        int _reloadStage;

        void Awake()
        {
            _holder = GetComponent<WeaponHolder>();
            _health = GetComponent<Health>();
        }

        void OnEnable()
        {
            _holder.Fired += OnFired;
            _holder.DryFired += OnDryFired;
            _holder.ReloadStarted += OnReloadStarted;
            _holder.ReloadCancelled += StopReload;
            _holder.ReloadFinished += StopReload;
            _holder.WeaponChanged += OnWeaponChanged;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            _health.Respawned += OnRespawned;
            if (!_active.Contains(this))
                _active.Add(this);
            if (_glassVoice == null)
            {
                _glassVoice = this;
                GlassPane.AnyBroken += OnPaneBroken;
            }
        }

        void OnDisable()
        {
            _holder.Fired -= OnFired;
            _holder.DryFired -= OnDryFired;
            _holder.ReloadStarted -= OnReloadStarted;
            _holder.ReloadCancelled -= StopReload;
            _holder.ReloadFinished -= StopReload;
            _holder.WeaponChanged -= OnWeaponChanged;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            _health.Respawned -= OnRespawned;
            _active.Remove(this);
            if (_glassVoice == this)
            {
                GlassPane.AnyBroken -= OnPaneBroken;
                _glassVoice = null;
                if (_active.Count > 0)
                {
                    _glassVoice = _active[0];
                    GlassPane.AnyBroken += _glassVoice.OnPaneBroken;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _voices = null;
            _next = 0;
            if (_glassVoice != null)
                GlassPane.AnyBroken -= _glassVoice.OnPaneBroken;
            _glassVoice = null;
            _mainCamera = null;
            _active.Clear();
        }

        // ---- What the gun says ---------------------------------------------------------------

        void OnFired(ShotInfo shot)
        {
            if (shot.Weapon == null)
                return;

            PlayAt(shot.Weapon.ShotSound, shot.Origin, true);
            if (shot.Surface == ShotSurface.World)
                PlayAt(_set != null ? _set.ImpactWorld : null, shot.End, true);
            else if (shot.Surface == ShotSurface.Body)
                PlayAt(_set != null ? _set.ImpactBody : null, shot.End, true);
        }

        void OnDryFired()
        {
            WeaponDefinition weapon = _holder.ActiveWeapon;
            if (weapon != null)
                PlayAt(weapon.DryFireSound, transform.position + Vector3.up * 1.5f, false);
        }

        void OnWeaponChanged(WeaponDefinition weapon, int slot)
        {
            StopReload();
            if (weapon != null && _holder.isActiveAndEnabled)
                PlayAt(weapon.DrawSound, transform.position + Vector3.up * 1.4f, false);
        }

        void OnReloadStarted(float seconds)
        {
            _reloadWeapon = _holder.ActiveWeapon;
            _reloadStart = Time.time;
            _reloadSeconds = Mathf.Max(0.01f, seconds);
            _reloadStage = 0;
        }

        void StopReload()
        {
            _reloadWeapon = null;
        }

        /// <summary>Plays the reload's three stages at their share of the reload time. Idle unless a reload is running.</summary>
        void Update()
        {
            if (_reloadWeapon == null)
                return;

            float progress = (Time.time - _reloadStart) / _reloadSeconds;
            Vector3 hands = transform.position + Vector3.up * 1.3f;
            if (_reloadStage == 0 && progress >= MagazineOutAt)
                PlayStage(_reloadWeapon.MagazineOutSound, hands);
            else if (_reloadStage == 1 && progress >= MagazineInAt)
                PlayStage(_reloadWeapon.MagazineInSound, hands);
            else if (_reloadStage == 2 && progress >= ReloadEndAt)
            {
                PlayStage(_reloadWeapon.ReloadEndSound, hands);
                _reloadWeapon = null;
            }
        }

        void PlayStage(AudioClip clip, Vector3 at)
        {
            _reloadStage++;
            PlayAt(clip, at, false);
        }

        // ---- What the body says ----------------------------------------------------------------

        void OnDamaged(Health health, DamageInfo info)
        {
            if (_health.IsOwner && !info.Fatal)
                PlayFlat(_set != null ? _set.Hurt : null);
        }

        void OnDied(Health health, DamageInfo info)
        {
            StopReload();
            if (_health.IsOwner)
                PlayFlat(_set != null ? _set.Death : null);
        }

        void OnRespawned(Health health)
        {
            if (_health.IsOwner)
                PlayFlat(_set != null ? _set.Respawn : null);
        }

        void OnPaneBroken(GlassPane pane, Vector3 direction)
        {
            PlayAt(_set != null ? _set.GlassShatter : null, pane.Centre, true);
        }

        // ---- Voices ----------------------------------------------------------------------------

        void PlayAt(AudioClip clip, Vector3 point, bool vary)
        {
            if (clip == null || _set == null)
                return;

            bool hasListener = TryListenerPosition(out Vector3 listenerPos);
            if (hasListener && (point - listenerPos).sqrMagnitude > _set.FarDistance * _set.FarDistance)
                return;

            AudioSource voice = NextVoice(listenerPos, hasListener);
            voice.transform.position = point;
            voice.outputAudioMixerGroup = _set.Output;
            voice.minDistance = _set.NearDistance;
            voice.maxDistance = _set.FarDistance;
            voice.pitch = vary ? 1f + Random.Range(-_set.PitchVariance, _set.PitchVariance) : 1f;
            voice.PlayOneShot(clip, _set.Volume);
        }

        static bool TryListenerPosition(out Vector3 position)
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

        /// <summary>Your own body's sounds: flat, not placed in the world.</summary>
        void PlayFlat(AudioClip clip)
        {
            if (clip == null || _set == null)
                return;

            if (_flat == null)
            {
                _flat = gameObject.AddComponent<AudioSource>();
                _flat.playOnAwake = false;
                _flat.spatialBlend = 0f;
            }

            _flat.outputAudioMixerGroup = _set.Output;
            _flat.PlayOneShot(clip, _set.Volume);
        }

        static AudioSource NextVoice(Vector3 listenerPos, bool hasListener)
        {
            if (_voices == null || _voices[0] == null)
                CreateVoices();

            for (int i = 0; i < Voices; i++)
            {
                int idx = (_next + i) % Voices;
                if (!_voices[idx].isPlaying)
                {
                    _next = (idx + 1) % Voices;
                    return _voices[idx];
                }
            }

            if (hasListener)
            {
                int farthest = 0;
                float maxSqr = -1f;
                for (int i = 0; i < Voices; i++)
                {
                    float sqr = (_voices[i].transform.position - listenerPos).sqrMagnitude;
                    if (sqr > maxSqr)
                    {
                        maxSqr = sqr;
                        farthest = i;
                    }
                }
                _next = (farthest + 1) % Voices;
                return _voices[farthest];
            }

            AudioSource voice = _voices[_next];
            _next = (_next + 1) % Voices;
            return voice;
        }

        static void CreateVoices()
        {
            var holder = new GameObject("Combat Sounds") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(holder);
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var child = new GameObject("Voice " + i);
                child.transform.SetParent(holder.transform, false);
                AudioSource voice = child.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;
                voice.dopplerLevel = 0f;
                _voices[i] = voice;
            }
        }
    }
}
