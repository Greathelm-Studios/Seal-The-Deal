using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Everything a gun looks like, on this machine only: the model in the hand, the muzzle
    /// flash, the tracer, the kick of the shooter's view, the dip of a reload and the slump
    /// of the camera when its player dies. Nothing here is networked or decides anything; it
    /// listens to WeaponHolder and Health. Runs after PlayerCamera has placed the camera, so
    /// the kick sits on top of the player's own look and moves the crosshair with it.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponHolder), typeof(Health))]
    public sealed class WeaponView : MonoBehaviour
    {
        [Tooltip("Where the gun sits on the body as other players see it: a point at the hand, facing forward. Leave empty and other players see tracers but no gun.")]
        [SerializeField] Transform _thirdPersonAnchor;

        [Header("Feel")]
        [Tooltip("How long the muzzle flash stays lit after a shot, in seconds.")]
        [Range(0.01f, 0.2f)] [SerializeField] float _flashSeconds = 0.05f;

        [Tooltip("How far the gun jumps back toward the shooter on each shot, in metres.")]
        [Range(0f, 0.2f)] [SerializeField] float _kickBack = 0.06f;

        [Tooltip("How fast the gun returns from that jump, in metres per second.")]
        [Min(0.1f)] [SerializeField] float _kickBackRecovery = 0.6f;

        [Tooltip("How far the gun drops out of view while reloading or being drawn, in metres.")]
        [Range(0f, 0.6f)] [SerializeField] float _dipDistance = 0.3f;

        [Tooltip("How long the gun takes to drop or come back up, in seconds.")]
        [Range(0.05f, 1f)] [SerializeField] float _dipSeconds = 0.18f;

        [Tooltip("The most the view can be kicked upward by a long burst, in degrees. A pattern that climbs past this stops climbing there.")]
        [Range(1f, 45f)] [SerializeField] float _maxKick = 14f;

        [Header("Death (the dead player's own view)")]
        [Tooltip("How far the view sinks toward the floor when its player dies, in metres.")]
        [Range(0f, 1.5f)] [SerializeField] float _deathDrop = 1f;

        [Tooltip("How far the view tips sideways when its player dies, in degrees.")]
        [Range(0f, 90f)] [SerializeField] float _deathRoll = 40f;

        [Tooltip("How long the view takes to slump on death and to right itself on respawn, in seconds.")]
        [Range(0.05f, 2f)] [SerializeField] float _deathSeconds = 0.35f;

        const string MuzzleName = "Muzzle";
        const string FlashName = "Flash";
        const float DryFireJolt = 0.3f;
        const float KickHoldIntervals = 1.5f;

        WeaponHolder _holder;
        Health _health;
        WeaponDefinition _weapon;
        GameObject _view;
        Transform _muzzle;
        GameObject _flash;
        bool _inFirstPerson;
        bool _viewShown = true;

        float _flashLeft;
        float _kickPitch;
        float _kickYaw;
        float _kickedBack;
        float _kickHeldUntil;
        float _dip;
        float _dipTarget;
        float _death;

        void Awake()
        {
            _holder = GetComponent<WeaponHolder>();
            _health = GetComponent<Health>();
        }

        void OnEnable()
        {
            _holder.WeaponChanged += OnWeaponChanged;
            _holder.Fired += OnFired;
            _holder.DryFired += OnDryFired;
            _holder.ReloadStarted += OnReloadStarted;
            _holder.ReloadFinished += OnReloadEnded;
            _holder.ReloadCancelled += OnReloadEnded;
        }

        void OnDisable()
        {
            _holder.WeaponChanged -= OnWeaponChanged;
            _holder.Fired -= OnFired;
            _holder.DryFired -= OnDryFired;
            _holder.ReloadStarted -= OnReloadStarted;
            _holder.ReloadFinished -= OnReloadEnded;
            _holder.ReloadCancelled -= OnReloadEnded;
            ClearView();
        }

        // ---- What the holder says ------------------------------------------------------------

        void OnWeaponChanged(WeaponDefinition weapon, int slot)
        {
            bool firstPerson = _holder.IsOwner && PlayerCamera.Instance != null;
            if (weapon == _weapon && _view != null && firstPerson == _inFirstPerson)
                return;

            ClearView();
            _weapon = weapon;
            _inFirstPerson = firstPerson;
            _dipTarget = 0f;

            Transform parent = firstPerson ? PlayerCamera.Instance.transform : _thirdPersonAnchor;
            if (weapon != null && weapon.ViewPrefab != null && parent != null)
                SpawnView(weapon, parent, firstPerson);
        }

        void SpawnView(WeaponDefinition weapon, Transform parent, bool firstPerson)
        {
            _view = Instantiate(weapon.ViewPrefab, parent);
            _view.transform.localPosition = firstPerson ? weapon.ViewOffset : Vector3.zero;
            _view.transform.localRotation = Quaternion.identity;
            _muzzle = _view.transform.Find(MuzzleName);
            Transform flash = _view.transform.Find(FlashName);
            _flash = flash != null ? flash.gameObject : null;
            if (_flash != null)
                _flash.SetActive(false);

            // A freshly drawn gun comes up from below.
            _dip = 1f;
            _viewShown = true;
        }

        void OnFired(ShotInfo shot)
        {
            Vector3 from = _muzzle != null ? _muzzle.position : shot.Origin;
            if (shot.Weapon != null)
                TracerPool.Spawn(shot.Weapon.TracerPrefab, from, shot.End, shot.Weapon.TracerSeconds);

            _flashLeft = _flashSeconds;
            if (_flash != null)
                _flash.SetActive(true);

            if (!shot.Predicted || shot.Weapon == null)
                return;

            Vector2 kick = shot.Weapon.KickFor(shot.ShotInBurst);
            _kickPitch = Mathf.Min(_maxKick, _kickPitch + kick.y);
            _kickYaw += kick.x;
            _kickedBack = _kickBack;
            // The view holds its kick for the length of a burst and only settles once the trigger rests.
            // Held exactly as long as the pattern carries on, so a burst that resumes picks up the pattern from the view it left.
            _kickHeldUntil = Time.time + Mathf.Max(shot.Weapon.ShotInterval * KickHoldIntervals, shot.Weapon.PatternResetSeconds);
        }

        void OnDryFired()
        {
            _kickedBack = _kickBack * DryFireJolt;
        }

        void OnReloadStarted(float seconds)
        {
            _dipTarget = 1f;
        }

        void OnReloadEnded()
        {
            _dipTarget = 0f;
        }

        void ClearView()
        {
            if (_view != null)
                Destroy(_view);

            _view = null;
            _muzzle = null;
            _flash = null;
        }

        // ---- Every frame ---------------------------------------------------------------------

        void LateUpdate()
        {
            float deathTarget = _health.IsDead ? 1f : 0f;
            if (IsAtRest(deathTarget))
                return;

            float dt = Time.deltaTime;
            Settle(dt, deathTarget);
            ShowView(!_health.IsDead);

            if (!_inFirstPerson)
                return;

            MoveCamera();
            if (_view != null)
                PoseView();
        }

        /// <summary>Nothing is moving and the camera needs no slump, so this frame has nothing to do.</summary>
        bool IsAtRest(float deathTarget)
        {
            bool kickDone = _kickPitch == 0f && _kickYaw == 0f && _kickedBack == 0f;
            bool poseDone = _flashLeft <= 0f && _dip == _dipTarget;
            return kickDone && poseDone && _death == 0f && deathTarget == 0f;
        }

        void Settle(float dt, float deathTarget)
        {
            if (_flashLeft > 0f)
            {
                _flashLeft -= dt;
                if (_flashLeft <= 0f && _flash != null)
                    _flash.SetActive(false);
            }

            if (Time.time >= _kickHeldUntil)
            {
                float recovery = _weapon != null ? _weapon.KickRecovery : 20f;
                _kickPitch = Mathf.MoveTowards(_kickPitch, 0f, recovery * dt);
                _kickYaw = Mathf.MoveTowards(_kickYaw, 0f, recovery * dt);
            }

            _kickedBack = Mathf.MoveTowards(_kickedBack, 0f, _kickBackRecovery * dt);
            _dip = Mathf.MoveTowards(_dip, _dipTarget, dt / _dipSeconds);
            _death = Mathf.MoveTowards(_death, deathTarget, dt / _deathSeconds);
        }

        void ShowView(bool show)
        {
            if (_view == null || show == _viewShown)
                return;

            _viewShown = show;
            _view.SetActive(show);
        }

        /// <summary>PlayerCamera rewrites the camera from the player's look every frame, so this never builds up.</summary>
        void MoveCamera()
        {
            if (_kickPitch == 0f && _kickYaw == 0f && _death == 0f)
                return;

            Transform cam = PlayerCamera.Instance != null ? PlayerCamera.Instance.transform : null;
            if (cam == null)
                return;

            Quaternion kick = Quaternion.Euler(-_kickPitch, _kickYaw, _deathRoll * _death);
            cam.SetPositionAndRotation(cam.position + Vector3.down * (_deathDrop * _death), cam.rotation * kick);
        }

        void PoseView()
        {
            Vector3 offset = _weapon.ViewOffset;
            offset.y -= _dipDistance * _dip;
            offset.z -= _kickedBack;
            _view.transform.localPosition = offset;
        }
    }
}
