using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloseTheDeal.Player
{
    /// <summary>Posed held weapon and arm IK on the player body (local-only, runs before and after <see cref="PlayerRagdoll"/>).</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(HoldOrder)]
    public sealed class PlayerHeldWeapon : MonoBehaviour
    {
        // Runs after PlayerAnimator (100) and before PlayerRagdoll (110); HeldWeaponSettle runs at 115.
        const int HoldOrder = 105;

        static readonly Vector3 MaxSwing = new(0.04f, 0.07f, 0.06f);
        static readonly Vector3 ElbowSide = new(0.45f, -1f, 0f);
        const float MaxTurn = 12f;
        static readonly Vector3 BodyViewElbowSide = new(0.65f, -1f, -0.25f);
        static readonly Vector3 BodyViewSwingMin = new(-0.02f, -0.045f, -0.035f);
        static readonly Vector3 BodyViewSwingMax = new(0.045f, 0.015f, 0.035f);
        const float BodyViewMaxTurn = 10f;
        const float BodyViewCushion = 0.03f;
        const float LeftArmClearX = 0.24f, LeftArmClearY = 0.22f, LeftArmFrontFade = 0.12f;
        const float RemoteCurlDistance = 12f;

        struct GunCopy
        {
            public Transform Gun;
            public Transform Muzzle;
        }

        PlayerMotor _motor;
        PlayerViewmodel _view;
        ViewmodelProfile _profile;
        ViewmodelArms _source;

        readonly HandGrip _grip = new();
        readonly HandGrip _support = new();
        Vector3 _supportPosition;
        Quaternion _supportRotation;
        bool _hasSupport;
        bool _leftArm;
        Transform _leanPivot;
        Transform _gun;
        Vector3 _gripPosition;
        Quaternion _gripRotation;
        float _pivotHeight;
        Vector3 _restOffset;
        Quaternion _restTurn = Quaternion.identity;
        bool _wasBodyView;
        Vector3 _bodyViewSwing;
        Quaternion _bodyViewSwingTurn = Quaternion.identity;
        int _cachedPoseFrame = -1;
        Vector3 _cachedEye;
        Quaternion _cachedLook;
        Vector3 _cachedGunLocal;
        Quaternion _cachedGunRotation;
        Vector3 _cachedWrist;
        bool _ready;
        WeaponHold _waiting;
        bool _hasWaiting;
        readonly Dictionary<Transform, GunCopy> _copies = new();

        /// <summary>The muzzle of the held weapon (child named "Muzzle", or the weapon root if none).</summary>
        public Transform Muzzle { get; private set; }

        /// <summary>Attaches <see cref="PlayerHeldWeapon"/> to the player's visual root at spawn.</summary>
        public static PlayerHeldWeapon Attach(GameObject graphics, PlayerMotor motor, PlayerViewmodel view, ViewmodelProfile profile, ViewmodelArms source)
        {
            var held = graphics.AddComponent<PlayerHeldWeapon>();
            held._motor = motor;
            held._view = view;
            held._profile = profile;
            held._source = source;
            return held;
        }

        // Start runs before PlayerAnimator clears the humanoid avatar on the first frame, while the rig is still in T-pose.
        void Start()
        {
            var avatar = GetComponentInChildren<Animator>();
            Transform seat = _source != null ? _source.GripTarget : null;
            Transform weapon = _source != null ? _source.HeldModel : null;
            if (_motor == null || _view == null || _profile == null || avatar == null || seat == null || weapon == null || !_grip.Bind(avatar))
            {
                Debug.LogWarning("[Viewmodel] The body cannot hold the weapon: the player, the view's tuning, the arms prefab's weapon and grip, or the model's right arm bones are missing.", this);
                enabled = false;
                return;
            }

            _grip.CopyTuning(_source.Grip);

            // The left hand's seat is optional: a prefab without one is simply one-handed.
            Transform supportSeat = _source.SupportTarget;
            _leftArm = _support.Bind(avatar, true);
            if (_leftArm)
                _support.CopyTuning(_source.SupportGrip);

            // The model hangs off the pivot the motor tilts for a lean; its height on the
            // prefab is fixed, a lean only slides it sideways.
            // The pivot is the child of this object that the model is somewhere under; the
            // Animator itself may sit deeper inside the model.
            _leanPivot = avatar.transform;
            while (_leanPivot.parent != null && _leanPivot.parent != transform)
                _leanPivot = _leanPivot.parent;
            _pivotHeight = _leanPivot.localPosition.y;

            gameObject.AddComponent<HeldWeaponSettle>().Follow(this);
            _ready = true;

            // What the arms prefab holds, unless weapon code has already asked for something else.
            if (_hasWaiting)
                Equip(_waiting);
            else if (supportSeat != null)
                Equip(new WeaponHold(weapon, seat.localPosition, seat.localRotation, supportSeat.localPosition, supportSeat.localRotation, _view.TwoHanded));
            else
                Equip(new WeaponHold(weapon, seat.localPosition, seat.localRotation.eulerAngles));
        }

        /// <summary>Swaps the weapon on the player's body; call <see cref="PlayerViewmodel.Equip"/> instead so view and body stay in sync.</summary>
        public void Equip(in WeaponHold hold)
        {
            if (hold.Model == null)
                return;

            if (!_ready)
            {
                // Start has not found the arm yet (or could not); it picks this up if it does.
                _waiting = hold;
                _hasWaiting = true;
                return;
            }

            if (_gun != null)
                _gun.gameObject.SetActive(false);

            _gripPosition = hold.GripPosition;
            _gripRotation = hold.GripRotation;
            _hasSupport = _leftArm && hold.HasSupport;
            _supportPosition = hold.SupportPosition;
            _supportRotation = hold.SupportRotation;

            if (!_copies.TryGetValue(hold.Model, out GunCopy copy) || copy.Gun == null)
            {
                _gun = Instantiate(hold.Model, _grip.Hand);
                _gun.name = "HeldWeapon";
                // The bones carry the rig's own scale, so the size is set against the world.
                float boneScale = _grip.Hand.lossyScale.x;
                _gun.localScale = hold.Model.lossyScale / (boneScale > 0f ? boneScale : 1f);
                foreach (Transform part in _gun.GetComponentsInChildren<Transform>(true))
                    part.gameObject.layer = gameObject.layer;
                Renderer[] renderers = _gun.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].shadowCastingMode = ShadowCastingMode.On;
                Transform muzzle = FindMuzzle(_gun);
                copy = new GunCopy { Gun = _gun, Muzzle = muzzle };
                _copies[hold.Model] = copy;
            }
            else
            {
                _gun = copy.Gun;
            }
            _gun.gameObject.SetActive(true);
            Muzzle = copy.Muzzle;

            // A different weapon hangs differently; let the steady sag be found again.
            _restOffset = Vector3.zero;
            _restTurn = Quaternion.identity;
            _bodyViewSwing = Vector3.zero;
            _bodyViewSwingTurn = Quaternion.identity;
        }

        static Transform FindMuzzle(Transform model)
        {
            if (model == null)
                return null;
            foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == "Muzzle")
                    return part;
            }
            return model;
        }

        void LateUpdate()
        {
            if (!_ready)
                return;

            Hold(false);
        }

        internal void Hold(bool afterRagdoll)
        {
            if (!_ready)
                return;

            PlayerCamera bodyEye = PlayerMotor.Local == _motor ? PlayerCamera.Instance : null;
            bool inBodyView = bodyEye != null;
            if (inBodyView != _wasBodyView)
            {
                _wasBodyView = inBodyView;
                _restOffset = Vector3.zero;
                _restTurn = Quaternion.identity;
                _bodyViewSwing = Vector3.zero;
                _bodyViewSwingTurn = Quaternion.identity;
                _cachedPoseFrame = -1;
            }

            // Keep body-view mantling guided so climbing drops the gun cleanly instead of waving across the lens.
            MoveState state = _motor.State;
            if (state != MoveState.Grounded && state != MoveState.Airborne && (!inBodyView || state != MoveState.Mantling))
                return;

            Vector3 eye;
            Quaternion look;
            Vector3 gunLocal;
            Quaternion gunRotation;
            Vector3 wrist;
            if (afterRagdoll && _cachedPoseFrame == Time.frameCount)
            {
                eye = _cachedEye;
                look = _cachedLook;
                gunLocal = _cachedGunLocal;
                gunRotation = _cachedGunRotation;
                wrist = _cachedWrist;
            }
            else
            {
                gunLocal = _view.RestPosition;
                if (inBodyView)
                {
                    eye = bodyEye.transform.position;
                    look = bodyEye.transform.rotation;
                    gunLocal += _profile.BodyViewGunShift + _view.BodyViewMotionOffset;
                    gunRotation = look * _view.BodyViewMotionRotation;
                    Vector3 gunPosition = eye + look * gunLocal;
                    wrist = gunPosition + gunRotation * _gripPosition;
                }
                else
                {
                    Vector3 up = _leanPivot.up;
                    eye = _leanPivot.position + up * (_motor.EyeHeight - _pivotHeight);
                    Vector3 aim = _motor.AimDirection;
                    if (Vector3.Cross(aim, up).sqrMagnitude < 1e-6f)
                        aim = transform.forward;
                    look = Quaternion.LookRotation(aim, up);
                    gunRotation = look * _view.RestTurn;
                    Vector3 gunPosition = eye + look * gunLocal;
                    wrist = gunPosition + gunRotation * _gripPosition + look * (Vector3.forward * _profile.BodyGunForward);
                }

                if (!afterRagdoll)
                {
                    _cachedEye = eye;
                    _cachedLook = look;
                    _cachedGunLocal = gunLocal;
                    _cachedGunRotation = gunRotation;
                    _cachedWrist = wrist;
                    _cachedPoseFrame = Time.frameCount;
                }
            }

            if (afterRagdoll)
            {
                // Where the ragdoll left the hand and the gun, measured from the aimed pose.
                // The weapon rode the ragdoll's arm as a child of the hand, so its turn now is
                // where that arm carried the muzzle.
                Vector3 offset = Quaternion.Inverse(look) * (_grip.Hand.position - wrist);
                Quaternion turn = Quaternion.Inverse(gunRotation) * _gun.rotation;

                // Followed slowly: what stays put (the sag) collects here, what moves does not.
                float follow = 1f - Mathf.Exp(-Time.deltaTime / _profile.BodySwingSettleSeconds);
                _restOffset = Vector3.Lerp(_restOffset, offset, follow);
                _restTurn = Quaternion.Slerp(_restTurn, turn, follow);

                if (inBodyView)
                {
                    float swing = _profile.BodyViewArmSwing;
                    Vector3 rawPos = (offset - _restOffset) * swing;
                    Vector3 targetSwing = new Vector3(
                        SoftClamp(rawPos.x, BodyViewSwingMin.x, BodyViewSwingMax.x),
                        SoftClamp(rawPos.y, BodyViewSwingMin.y, BodyViewSwingMax.y),
                        SoftClamp(rawPos.z, BodyViewSwingMin.z, BodyViewSwingMax.z));

                    (Quaternion.Inverse(_restTurn) * turn).ToAngleAxis(out float thrown, out Vector3 about);
                    if (thrown > 180f)
                        thrown -= 360f;
                    Quaternion targetSwingTurn = !float.IsNaN(about.x) && !float.IsInfinity(about.x)
                        ? Quaternion.AngleAxis(SoftClamp(thrown * swing, -BodyViewMaxTurn, BodyViewMaxTurn), about)
                        : Quaternion.identity;

                    float smooth = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(_profile.BodyViewSwingSmooth, 0.001f));
                    _bodyViewSwing = Vector3.Lerp(_bodyViewSwing, targetSwing, smooth);
                    _bodyViewSwingTurn = Quaternion.Slerp(_bodyViewSwingTurn, targetSwingTurn, smooth);

                    gunLocal += _bodyViewSwing;
                    gunLocal.x = SoftFloor(gunLocal.x, _profile.BodyViewKeepOutRight * (1f - _view.TwoHandBlend), BodyViewCushion);
                    gunLocal.y = SoftCeiling(gunLocal.y, -_profile.BodyViewKeepOutDown, BodyViewCushion);
                    gunLocal.z = SoftFloor(gunLocal.z, _profile.BodyViewMinForward, BodyViewCushion);

                    gunRotation *= _bodyViewSwingTurn;
                    Vector3 gunPosition = eye + look * gunLocal;
                    wrist = gunPosition + gunRotation * _gripPosition;
                }
                else
                {
                    float swing = _profile.BodyArmSwing;
                    Vector3 thrownBy = (offset - _restOffset) * swing;
                    thrownBy.x = Mathf.Clamp(thrownBy.x, -MaxSwing.x, MaxSwing.x);
                    thrownBy.y = Mathf.Clamp(thrownBy.y, -MaxSwing.y, MaxSwing.y);
                    thrownBy.z = Mathf.Clamp(thrownBy.z, -MaxSwing.z, MaxSwing.z);
                    wrist += look * thrownBy;
                    (Quaternion.Inverse(_restTurn) * turn).ToAngleAxis(out float thrown, out Vector3 about);
                    // ToAngleAxis gives 0 to 360; past 180 it is the short way round the other side.
                    if (thrown > 180f)
                        thrown -= 360f;
                    if (!float.IsNaN(about.x) && !float.IsInfinity(about.x))
                        gunRotation *= Quaternion.AngleAxis(Mathf.Clamp(thrown * swing, -MaxTurn, MaxTurn), about);
                }
            }

            bool curlFingers = false;
            if (afterRagdoll)
            {
                if (inBodyView)
                {
                    curlFingers = true;
                }
                else
                {
                    PlayerCamera cam = PlayerCamera.Instance;
                    curlFingers = cam == null || (transform.position - cam.transform.position).sqrMagnitude <= RemoteCurlDistance * RemoteCurlDistance;
                }
            }

            Vector3 elbowHint = inBodyView ? BodyViewElbowSide : ElbowSide;
            _grip.Reach(wrist, gunRotation * _gripRotation, look * elbowHint, curlFingers);

            // Seat the weapon on the actual hand transform so it never floats if steep pitch exceeds arm reach.
            _gun.SetPositionAndRotation(_grip.Hand.position - gunRotation * _gripPosition, gunRotation);

            if (_hasSupport && _view.TwoHanded)
            {
                Vector3 leftElbow = look * new Vector3(-elbowHint.x, elbowHint.y, elbowHint.z);
                _support.Reach(_gun.position + gunRotation * _supportPosition, gunRotation * _supportRotation, leftElbow, curlFingers);
            }
            else if (inBodyView && afterRagdoll && _leftArm)
            {
                KeepLeftArmClear(eye, look);
            }
        }

        void KeepLeftArmClear(Vector3 eye, Quaternion look)
        {
            Vector3 leftLocal = Quaternion.Inverse(look) * (_support.Hand.position - eye);
            if (leftLocal.z <= 0f || leftLocal.x <= -LeftArmClearX || leftLocal.y <= -LeftArmClearY)
                return;

            float front = Mathf.SmoothStep(0f, LeftArmFrontFade, leftLocal.z);
            float pushLeft = leftLocal.x - (-LeftArmClearX);
            float pushDown = leftLocal.y - (-LeftArmClearY);
            Vector3 deflected = leftLocal;
            if (pushLeft < pushDown)
                deflected.x = Mathf.Lerp(leftLocal.x, -LeftArmClearX, front);
            else
                deflected.y = Mathf.Lerp(leftLocal.y, -LeftArmClearY, front);

            Vector3 leftElbow = look * new Vector3(-BodyViewElbowSide.x, BodyViewElbowSide.y, BodyViewElbowSide.z);
            _support.ReachArm(eye + look * deflected, leftElbow);
        }

        static float SoftClamp(float value, float min, float max)
        {
            if (value < 0f && min < 0f)
                return min * (float)System.Math.Tanh(value / min);
            if (value > 0f && max > 0f)
                return max * (float)System.Math.Tanh(value / max);
            return value;
        }

        static float SoftFloor(float value, float floor, float margin)
        {
            float above = value - floor;
            if (above >= margin)
                return value;
            return floor + margin * Mathf.Exp((above - margin) / margin);
        }

        static float SoftCeiling(float value, float ceiling, float margin)
        {
            return -SoftFloor(-value, -ceiling, margin);
        }
    }
}
