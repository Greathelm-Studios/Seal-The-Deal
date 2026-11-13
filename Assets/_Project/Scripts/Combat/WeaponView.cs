using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Everything a gun looks like, on this machine only: the model in the hand and how it
    /// kicks, sways and bobs; the muzzle flash and its light; the tracer and the impact; the
    /// shooter's view kick, shake and field-of-view punch; the dip of a reload and the slump
    /// of the camera when its player dies. Nothing here is networked or decides anything; it
    /// listens to WeaponHolder and Health. Runs after PlayerCamera has placed the camera, so
    /// the kick sits on top of the player's own look and moves the crosshair with it.
    /// Per-gun feel lives on each WeaponDefinition; what is the same for every gun lives here.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponHolder), typeof(Health))]
    public sealed class WeaponView : MonoBehaviour
    {
        [Tooltip("Where the gun sits on the body as other players see it: a point at the hand, facing forward. Leave empty and other players see tracers but no gun.")]
        [SerializeField] Transform _thirdPersonAnchor;

        [Header("Reload and draw")]
        [Tooltip("How far the gun drops out of view while reloading or being drawn, in metres.")]
        [Range(0f, 0.6f)] [SerializeField] float _dipDistance = 0.3f;

        [Tooltip("How long the gun takes to drop or come back up, in seconds.")]
        [Range(0.05f, 1f)] [SerializeField] float _dipSeconds = 0.18f;

        [Tooltip("The most the recoil pattern can lift the view in one burst, in degrees. A pattern that climbs past this stops climbing there.")]
        [Range(1f, 45f)] [SerializeField] float _maxKick = 14f;

        [Header("Sway and bob (the gun in the shooter's own view)")]
        [Tooltip("How far the gun trails behind a turn of the view, in metres at a brisk flick. 0 = the gun is glued to the view.")]
        [Range(0f, 0.08f)] [SerializeField] float _swayDistance = 0.02f;

        [Tooltip("How far the gun rolls against a turn, in degrees at a brisk flick.")]
        [Range(0f, 15f)] [SerializeField] float _swayRoll = 3f;

        [Tooltip("How quickly the sway catches up with the view, in seconds. Lower = snappier.")]
        [Range(0.01f, 0.5f)] [SerializeField] float _swaySmoothing = 0.08f;

        [Tooltip("How far the gun bobs sideways at a sprint, in metres. Up and down is about two thirds of this, twice as fast.")]
        [Range(0f, 0.05f)] [SerializeField] float _bobDistance = 0.015f;

        [Tooltip("Bob steps per second at a sprint. Slower movement bobs slower and less.")]
        [Range(0.5f, 4f)] [SerializeField] float _bobRate = 1.9f;

        [Tooltip("Speed at which the bob is at its full size, in metres per second. Players sprint at 9.")]
        [Min(1f)] [SerializeField] float _bobFullSpeed = 9f;

        [Tooltip("How much bob is left while shooting, as a fraction. Low values give a steady firing pose: shots never start from a bobbing gun.")]
        [Range(0f, 1f)] [SerializeField] float _bobWhileFiring = 0.2f;

        [Tooltip("How long after a shot the gun stays in its steady firing pose, in seconds.")]
        [Range(0f, 1f)] [SerializeField] float _firingPoseSeconds = 0.25f;

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
        const float ShakeFrequency = 22f;
        const float SwayFlickDegreesPerSecond = 600f;
        const float FlashSizeJitter = 0.3f;

        WeaponHolder _holder;
        Health _health;
        PlayerMotor _motor;
        Camera _camera;
        WeaponDefinition _weapon;
        GameObject _view;
        Transform _muzzle;
        GameObject _flash;
        Vector3 _flashScale;
        bool _inFirstPerson;
        bool _viewShown = true;

        // The recoil pattern's climb: held through a burst, then settles.
        float _climbPitch;
        float _climbYaw;
        float _climbHeldUntil;

        // Per-shot kicks that come back on their own curves.
        DecayingImpulse _snap;
        DecayingImpulse _gunKick;
        DecayingImpulse _fov;
        float _snapSide;
        float _gunTwistSide;
        float _shakeAmount;
        float _shakeLeft;
        float _shakeTotal;
        float _shakeSeed;
        float _fovAdded;
        float _fovWritten;

        float _flashLeft;
        float _dip;
        float _dipTarget;
        float _death;
        float _firingPoseUntil;

        Vector2 _sway;
        float _lastYaw;
        float _bobPhase;
        float _bobWeight;

        void Awake()
        {
            _holder = GetComponent<WeaponHolder>();
            _health = GetComponent<Health>();
            _motor = GetComponent<PlayerMotor>();
            _shakeSeed = Random.value * 100f;
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
            {
                _flashScale = flash.localScale;
                _flash.SetActive(false);
            }

            // A freshly drawn gun comes up from below, and starts its sway from the current look.
            _dip = 1f;
            _sway = Vector2.zero;
            if (firstPerson)
                _lastYaw = PlayerCamera.Instance.Yaw;
            _viewShown = true;
            _gunKick.Clear();
        }

        void OnFired(ShotInfo shot)
        {
            WeaponDefinition weapon = shot.Weapon;
            Vector3 from = _muzzle != null ? _muzzle.position : shot.Origin;
            if (weapon == null)
                return;

            TracerPool.Spawn(weapon.TracerPrefab, from, shot.End, weapon.TracerSeconds);
            if (shot.HitSomething)
                LocalEffects.Play(weapon.ImpactEffect, shot.End, from - shot.End);

            Flash(weapon);
            if (shot.Predicted)
                KickOwnView(weapon, shot.ShotInBurst);
        }

        /// <summary>The flash, its light, a little different every shot.</summary>
        void Flash(WeaponDefinition weapon)
        {
            _flashLeft = weapon.FlashSeconds;
            if (_flash == null)
                return;

            float size = 1f + Random.Range(-FlashSizeJitter, FlashSizeJitter);
            _flash.transform.localScale = _flashScale * size;
            _flash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            _flash.SetActive(true);
        }

        void KickOwnView(WeaponDefinition weapon, int shotInBurst)
        {
            Vector2 climb = weapon.KickFor(shotInBurst);
            _climbPitch = Mathf.Min(_maxKick, _climbPitch + climb.y);
            _climbYaw += climb.x;
            // The climb holds exactly as long as the pattern carries on, so a burst that resumes picks up the pattern from the view it left.
            _climbHeldUntil = Time.time + Mathf.Max(weapon.ShotInterval * KickHoldIntervals, weapon.PatternResetSeconds);

            _snapSide = Random.Range(-1f, 1f);
            _snap.Add(1f, weapon.SnapRecoverySeconds, weapon.SnapCurve);
            _gunTwistSide = _snapSide;
            _gunKick.Add(1f, weapon.ViewRecoverySeconds, weapon.ViewCurve);
            _fov.Add(weapon.FovPunch, weapon.SnapRecoverySeconds, weapon.SnapCurve);

            // The stronger shake wins, so a long burst rattles evenly rather than building up.
            if (weapon.ShakeDegrees >= _shakeAmount * (_shakeTotal > 0f ? _shakeLeft / _shakeTotal : 0f))
            {
                _shakeAmount = weapon.ShakeDegrees;
                _shakeLeft = _shakeTotal = weapon.ShakeSeconds;
            }

            _firingPoseUntil = Time.time + Mathf.Max(_firingPoseSeconds, weapon.ShotInterval);
        }

        void OnDryFired()
        {
            if (_weapon != null)
                _gunKick.Add(DryFireJolt, _weapon.ViewRecoverySeconds, _weapon.ViewCurve);
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
            float dt = Time.deltaTime;
            float deathTarget = _health.IsDead ? 1f : 0f;
            SettleFlash(dt);
            SettleClimb(dt);
            _dip = Mathf.MoveTowards(_dip, _dipTarget, dt / _dipSeconds);
            _death = Mathf.MoveTowards(_death, deathTarget, dt / _deathSeconds);
            ShowView(!_health.IsDead);

            if (!_inFirstPerson)
                return;

            Transform cam = PlayerCamera.Instance != null ? PlayerCamera.Instance.transform : null;
            if (cam == null)
                return;

            MoveCamera(cam, dt);
            if (_view != null && _weapon != null)
                PoseView(dt);
        }

        void SettleFlash(float dt)
        {
            if (_flashLeft <= 0f)
                return;

            _flashLeft -= dt;
            if (_flashLeft <= 0f && _flash != null)
                _flash.SetActive(false);
        }

        void SettleClimb(float dt)
        {
            if (Time.time < _climbHeldUntil || (_climbPitch == 0f && _climbYaw == 0f))
                return;

            float recovery = _weapon != null ? _weapon.KickRecovery : 20f;
            _climbPitch = Mathf.MoveTowards(_climbPitch, 0f, recovery * dt);
            _climbYaw = Mathf.MoveTowards(_climbYaw, 0f, recovery * dt);
        }

        void ShowView(bool show)
        {
            if (_view == null || show == _viewShown)
                return;

            _viewShown = show;
            _view.SetActive(show);
        }

        /// <summary>PlayerCamera rewrites the camera from the player's look every frame, so none of this builds up.</summary>
        void MoveCamera(Transform cam, float dt)
        {
            float snap = _snap.Step(dt, _weapon != null ? _weapon.SnapCurve : null);
            float snapUp = _weapon != null ? _weapon.SnapUp * snap : 0f;
            float snapSide = _weapon != null ? _weapon.SnapSide * snap * _snapSide : 0f;
            Vector2 shake = Shake(dt);

            float pitch = _climbPitch + snapUp + shake.y;
            float yaw = _climbYaw + snapSide + shake.x;
            if (pitch != 0f || yaw != 0f || _death != 0f)
            {
                Quaternion kick = Quaternion.Euler(-pitch, yaw, _deathRoll * _death);
                cam.SetPositionAndRotation(cam.position + Vector3.down * (_deathDrop * _death), cam.rotation * kick);
            }

            PunchFov(cam, dt);
        }

        /// <summary>A rattle from two noise lanes that fades out over the shake time. Never moves the aim for long: it averages to nothing.</summary>
        Vector2 Shake(float dt)
        {
            if (_shakeLeft <= 0f)
                return Vector2.zero;

            _shakeLeft -= dt;
            float fade = Mathf.Clamp01(_shakeLeft / _shakeTotal);
            fade *= fade;
            float t = Time.time * ShakeFrequency;
            float x = (Mathf.PerlinNoise(_shakeSeed, t) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(t, _shakeSeed + 7f) - 0.5f) * 2f;
            return new Vector2(x, y) * (_shakeAmount * fade);
        }

        /// <summary>
        /// Widens the view for an instant on each shot. Adds on top of whatever set the field of
        /// view this frame (the movement code may widen it with speed), and takes its own
        /// addition back out when nothing else has written it since.
        /// </summary>
        void PunchFov(Transform cam, float dt)
        {
            float add = _fov.Step(dt, _weapon != null ? _weapon.SnapCurve : null);
            if (add == 0f && _fovAdded == 0f)
                return;

            if (_camera == null)
                _camera = cam.GetComponent<Camera>();
            Camera camera = _camera;
            float fov = camera.fieldOfView;
            float baseFov = Mathf.Approximately(fov, _fovWritten) ? fov - _fovAdded : fov;
            _fovAdded = add;
            _fovWritten = baseFov + add;
            camera.fieldOfView = _fovWritten;
        }

        void PoseView(float dt)
        {
            WeaponDefinition weapon = _weapon;
            float kick = _gunKick.Step(dt, weapon.ViewCurve);
            Vector3 offset = weapon.ViewOffset;
            offset.y -= _dipDistance * _dip;
            offset.z -= weapon.ViewPushBack * kick;

            UpdateSway(dt);
            offset += new Vector3(_sway.x, _sway.y, 0f) * _swayDistance;
            offset += Bob(dt);

            Quaternion rotation = Quaternion.Euler(-weapon.ViewFlip * kick, weapon.ViewTwist * kick * _gunTwistSide, weapon.ViewRoll * kick - _sway.x * _swayRoll);
            _view.transform.SetLocalPositionAndRotation(offset, rotation);
        }

        /// <summary>The gun trails a turn of the view and catches up, so quick flicks feel like moving a weight.</summary>
        void UpdateSway(float dt)
        {
            float yaw = PlayerCamera.Instance.Yaw;
            float turn = dt > 0f ? Mathf.DeltaAngle(_lastYaw, yaw) / dt : 0f;
            _lastYaw = yaw;

            float target = Mathf.Clamp(-turn / SwayFlickDegreesPerSecond, -1f, 1f);
            float blend = 1f - Mathf.Exp(-dt / _swaySmoothing);
            _sway.x = Mathf.Lerp(_sway.x, target, blend);
            _sway.y = Mathf.Lerp(_sway.y, 0f, blend);
        }

        /// <summary>A figure of eight with each step, scaled by speed, only on the ground, and nearly still while firing.</summary>
        Vector3 Bob(float dt)
        {
            float speed = _motor != null && _motor.Grounded ? _motor.PlanarSpeed : 0f;
            float strength = Mathf.Clamp01(speed / _bobFullSpeed);
            if (Time.time < _firingPoseUntil)
                strength *= _bobWhileFiring;

            _bobWeight = Mathf.MoveTowards(_bobWeight, strength, dt * 4f);
            if (_bobWeight <= 0f)
                return Vector3.zero;

            _bobPhase += dt * _bobRate * Mathf.PI * 2f * Mathf.Max(0.4f, strength);
            float side = Mathf.Sin(_bobPhase) * _bobDistance;
            float up = Mathf.Sin(_bobPhase * 2f) * _bobDistance * 0.66f;
            return new Vector3(side, up, 0f) * _bobWeight;
        }
    }
}
