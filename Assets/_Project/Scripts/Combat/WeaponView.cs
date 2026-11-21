using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>Local weapon model, view kick, shake, FOV punch, sway/bob, and death slump applied after PlayerCamera.</summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponHolder), typeof(Health))]
    public sealed class WeaponView : MonoBehaviour
    {
        [Tooltip("Where the gun sits on the body as other players see it: a point at the hand, facing forward. Leave empty and other players see tracers but no gun.")]
        [SerializeField] Transform _thirdPersonAnchor;

        [Tooltip("Tick to draw the gun model in the shooter's own view. Untick when a first-person arms model (PlayerViewmodel) shows the gun instead: tracers, impacts and the view's kick, shake and FOV punch stay; the muzzle flash and its light go with the gun model, so the arms model shows its own. Other players still see the gun on this player's body either way.")]
        [SerializeField] bool _drawHeldGun = true;

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

        struct CachedModel
        {
            public GameObject View;
            public Transform Muzzle;
            public GameObject Flash;
            public Vector3 FlashScale;
        }

        WeaponHolder _holder;
        Health _health;
        PlayerMotor _motor;
        Camera _camera;
        WeaponDefinition _weapon;
        GameObject _view;
        Transform _muzzle;
        Transform _externalMuzzle;
        readonly System.Collections.Generic.Dictionary<WeaponDefinition, CachedModel> _models = new();
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
        bool _reloading;
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
            if (_fovAdded != 0f && _camera != null && Mathf.Approximately(_camera.fieldOfView, _fovWritten))
                _camera.fieldOfView -= _fovAdded;
            _fovAdded = 0f;
            _fovWritten = 0f;
            _fov.Clear();
            ClearView();
        }

        void OnWeaponChanged(WeaponDefinition weapon, int slot)
        {
            bool firstPerson = _holder.IsOwner && PlayerCamera.Instance != null;
            if (weapon == _weapon && _view != null && firstPerson == _inFirstPerson)
                return;

            ClearView();
            _weapon = weapon;
            _inFirstPerson = firstPerson;
            _dipTarget = 0f;
            _reloading = false;

            Transform parent = firstPerson ? PlayerCamera.Instance.transform : _thirdPersonAnchor;
            if (ShouldDraw(weapon, firstPerson) && parent != null)
                SpawnView(weapon, parent, firstPerson);
        }

        /// <summary>Draws a gun that has a model, unless an arms model draws the first-person one.</summary>
        bool ShouldDraw(WeaponDefinition weapon, bool firstPerson)
        {
            return (_drawHeldGun || !firstPerson) && weapon != null && weapon.ViewPrefab != null;
        }

        /// <summary>Whether this view draws the gun model in the shooter's own view. Off when an arms model does it.</summary>
        public bool DrawsHeldGun => _drawHeldGun;

        /// <summary>
        /// For an arms model that draws the gun itself: where its muzzle is, so tracers leave from
        /// the gun the player sees rather than the eye. Pass null to clear. Only used while this
        /// view isn't drawing its own gun.
        /// </summary>
        public void SetExternalMuzzle(Transform muzzle)
        {
            _externalMuzzle = muzzle;
        }

        void SpawnView(WeaponDefinition weapon, Transform parent, bool firstPerson)
        {
            // Each gun's model is made once per player and reused, so switching costs no allocation or Find.
            if (!_models.TryGetValue(weapon, out CachedModel cached) || cached.View == null || cached.View.transform.parent != parent)
            {
                if (cached.View != null)
                    Destroy(cached.View);
                GameObject view = Instantiate(weapon.ViewPrefab, parent);
                Transform muzzle = view.transform.Find(MuzzleName);
                Transform flash = view.transform.Find(FlashName);
                cached = new CachedModel
                {
                    View = view,
                    Muzzle = muzzle,
                    Flash = flash != null ? flash.gameObject : null,
                    FlashScale = flash != null ? flash.localScale : Vector3.one
                };
                _models[weapon] = cached;
            }

            _view = cached.View;
            _muzzle = cached.Muzzle;
            _flash = cached.Flash;
            _flashScale = cached.FlashScale;

            _view.SetActive(true);
            _view.transform.localPosition = firstPerson ? weapon.ViewOffset : Vector3.zero;
            _view.transform.localRotation = Quaternion.identity;
            if (_flash != null)
                _flash.SetActive(false);

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
            Transform muzzle = _muzzle != null ? _muzzle : _externalMuzzle;
            Vector3 from = muzzle != null ? muzzle.position : shot.Origin;
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
            _reloading = true;
        }

        void OnReloadEnded()
        {
            _reloading = false;
        }

        /// <summary>Puts the gun in hand away. Its model is kept, hidden, for the next time that gun comes out.</summary>
        void ClearView()
        {
            if (_view != null)
                _view.SetActive(false);

            _view = null;
            _muzzle = null;
            _flash = null;
        }

        void OnDestroy()
        {
            foreach (CachedModel model in _models.Values)
            {
                if (model.View != null)
                    Destroy(model.View);
            }

            _models.Clear();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            SettleFlash(dt);

            if (!_inFirstPerson)
            {
                // Other players see a put-away gun as gone from the hand; skip first-person climb, dip and death math.
                bool remoteHeld = WeaponHolder.FireHeld != null && WeaponHolder.FireHeld(_holder);
                ShowView(!_health.IsDead && !remoteHeld);
                return;
            }

            float deathTarget = _health.IsDead ? 1f : 0f;
            SettleClimb(dt);
            SettleDip(dt);
            _death = Mathf.MoveTowards(_death, deathTarget, dt / _deathSeconds);
            ShowView(!_health.IsDead);

            Transform cam = PlayerCamera.Instance != null ? PlayerCamera.Instance.transform : null;
            if (cam == null)
                return;

            MoveCamera(cam, dt);
            if (_view != null && _weapon != null)
                PoseView(dt);
        }

        /// <summary>Down while reloading, and while the game holds fire (hands full, reception): the gun is put away. True while held.</summary>
        bool SettleDip(float dt)
        {
            bool held = WeaponHolder.FireHeld != null && WeaponHolder.FireHeld(_holder);
            _dipTarget = _reloading || held ? 1f : 0f;
            _dip = Mathf.MoveTowards(_dip, _dipTarget, dt / _dipSeconds);
            return held;
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
