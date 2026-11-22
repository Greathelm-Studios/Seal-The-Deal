using System.Collections.Generic;
using CloseTheDeal.Audio;
using CloseTheDeal.Glass;
using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>Plays pooled 3D weapon/impact/glass audio and 2D local body feedback through SoundSystem.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponHolder), typeof(Health))]
    public sealed class CombatSounds : MonoBehaviour
    {
        [Tooltip("The shared combat sounds and their mixer bus. Each gun's shot and reload sounds are on the gun itself.")]
        [SerializeField] CombatSoundSet _set;

        const float MagazineOutAt = 0.15f;
        const float MagazineInAt = 0.6f;
        const float ReloadEndAt = 0.85f;
        const int CombatPriority = 64;

        static CombatSounds _glassVoice;
        static readonly List<CombatSounds> _active = new();

        WeaponHolder _holder;
        Health _health;
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
            if (_glassVoice != null)
                GlassPane.AnyBroken -= _glassVoice.OnPaneBroken;
            _glassVoice = null;
            _active.Clear();
        }

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

        void PlayAt(AudioClip clip, Vector3 point, bool vary)
        {
            if (clip == null || _set == null)
                return;

            float pitch = vary ? 1f + Random.Range(-_set.PitchVariance, _set.PitchVariance) : 1f;
            SoundSystem.PlayAt(
                clip,
                point,
                _set.Output,
                _set.Volume,
                _set.NearDistance,
                _set.FarDistance,
                pitch,
                CombatPriority,
                useOcclusion: true);
        }

        /// <summary>Your own body's sounds: flat, not placed in the world.</summary>
        void PlayFlat(AudioClip clip)
        {
            if (clip == null || _set == null)
                return;

            SoundSystem.PlayFlat(clip, _set.Output, _set.Volume);
        }
    }
}
