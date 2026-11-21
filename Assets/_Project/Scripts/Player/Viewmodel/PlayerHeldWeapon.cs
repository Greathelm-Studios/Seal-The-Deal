// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-14
//  Prompt    : Harvey: "have the actual playermodel follow the first person view model"
//  Modified  : Claude Fable 5.2, 2026-11-14 — the body's left arm joins the hold when the
//              weapon is held in both hands (Harvey: "add a left and right hand system and a
//              two handed system").
//  Modified  : Claude Fable 5.2, 2026-11-14 — the weapon in the hand can be swapped while playing,
//              for weapon code to drive (Harvey: "Take the best option and get it ready").
//  Modified  : Claude Fable 5.2, 2026-11-15 — in the body view the owner's gun is placed from the
//              eye that is looking, lifted and brought in (Harvey: "The arm definitely needs
//              to come out more").
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Added to the player's smoothed visual when the player spawns. Makes the body
//              everyone else sees (and the player's own shadow and mirror image) hold the
//              pistol out the way the first-person view does: same place in front of the eye,
//              same grip, pointing where the player aims. Separate from PlayerAnimator so the
//              gun arm can be removed or swapped without touching the body's animation.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Aims the body's right arm at the same grip point the first-person view uses, and keeps
    /// a copy of the first-person weapon in that hand.
    /// </summary>
    /// <remarks>
    /// <para><b>Ownership (AGENTS.md §3):</b> local to each client and synced by nothing. The
    /// only thing it needs from the network is where the player is aiming, which
    /// <see cref="PlayerMotor.AimDirection"/> already has on every machine from the input each
    /// tick runs on. Nothing is sent for it and nothing reads the arm back.</para>
    /// <para><b>One source for both views:</b> everything is read from the first-person arms
    /// prefab at spawn: the weapon model, where the hand sits on it, how far the fingers close
    /// and the elbow swing. The gun's place in front of the eye comes from the same
    /// <see cref="ViewmodelProfile"/>. Retune the view and the body follows.</para>
    /// <para><b>Where it sits in the frame:</b> twice. First after <see cref="PlayerAnimator"/>
    /// has written the clip pose and before <see cref="PlayerRagdoll"/> reads that pose as its
    /// target, so the ragdoll's arm is trying to hold the gun out and its bodies are where the
    /// arm is drawn. Then again straight after the ragdoll (<see cref="HeldWeaponSettle"/>),
    /// reading how far the ragdoll left the hand and the gun from the aimed pose. That gap is
    /// split in two: the part that stays put (the arm's steady sag under its own weight) is
    /// taken out, so at rest the arm is straight and the gun is on the aim; the part that
    /// changes (the lag, swing and bounce from moving) is kept and made bigger by the profile's
    /// swing value, so the arm flips about more than the ragdoll alone would throw it. WHY not
    /// leave it to the ragdoll alone: the arm sagged about 15 cm and the muzzle drooped some
    /// 25 degrees (seen in play). WHY not put it exactly on the aim: that pinned the wrist dead
    /// still in the air while the elbow and shoulder flopped round it, which Harvey rejected.</para>
    /// <para><b>The elbow is pinned:</b> both passes hang it down and a little out, in the
    /// direction the player aims, so the arm keeps one shape and only the hand moves, by the
    /// small limited swing.</para>
    /// <para><b>What differs from the view:</b> the body's shoulder is where the skeleton has
    /// it, not where the view pretends it is, so the elbow is more bent than in first person.
    /// The view's sway, bob, kick and reload are not shown; they are a few centimetres of
    /// motion nobody reads at a distance.</para>
    /// <para>WHY it is added at spawn (<see cref="Attach"/>) and is not on Player.prefab: that
    /// prefab is edited by hand and by other people's work, so nothing here writes to it (§4),
    /// and it is claimed on the workboard. Reading the arms prefab at spawn also means there is no
    /// second copy of the grip numbers to keep in step.</para>
    /// <para>WHY the weapon is a child of the hand bone and not placed in the world each frame:
    /// the ragdoll moves the arm after this runs, and a child goes with it.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(HoldOrder)]
    public sealed class PlayerHeldWeapon : MonoBehaviour
    {
        /// <summary>After PlayerAnimator (100), which writes the pose this overrides; before PlayerRagdoll (110), which reads it.</summary>
        const int HoldOrder = 105;

        /// <summary>
        /// The furthest the swing may carry the hand from the aimed wrist, in metres: sideways,
        /// up or down, and forward or back. WHY these: uncapped, a hard strafe reversal threw the
        /// hand 42 cm (measured in play), and at 30 and then 15 cm Harvey still found it swinging
        /// too far out. Sideways is the tightest, because that is the way the gun leaves the
        /// body's outline and pokes into walls and other players beside it; nothing checks the
        /// arm's final pose against the level, so the limit is what keeps it in.
        /// </summary>
        static readonly Vector3 MaxSwing = new(0.04f, 0.07f, 0.06f);

        /// <summary>
        /// Which way the body's elbow hangs, in aim space (right, up, forward): down and a little
        /// out to the gun's side. WHY fixed: left to follow the ragdoll's arm (after it) or the
        /// run clip's pumping arm (before it), the elbow flipped out wide and up over the
        /// shoulder from frame to frame while the hand hardly moved, which is what Harvey saw
        /// swinging in the mirror.
        /// </summary>
        static readonly Vector3 ElbowSide = new(0.45f, -1f, 0f);

        /// <summary>The furthest the swing may turn the gun away from the aim, in degrees. WHY 12: uncapped, the same strafe reversal turned the muzzle 114 degrees, back toward the player; 50 and then 25 were still too much.</summary>
        const float MaxTurn = 12f;

        /// <summary>
        /// Wider, slightly tucked-back elbow hint for the F5 body view, in camera space (right, up, forward).
        /// WHY: the third-person hint (0.45, -1, 0) pulls the forearm across the bottom-right under the crosshair;
        /// swinging the elbow further out and back keeps the sleeve along the right edge of the frame.
        /// </summary>
        static readonly Vector3 BodyViewElbowSide = new(0.85f, -1f, -0.15f);

        /// <summary>
        /// Asymmetric swing limits in camera space for the F5 body view, metres.
        /// WHY asymmetric: tight inward (-3 cm) and upward (+3.5 cm) so the arm never crosses the crosshair,
        /// while generous outward (+10 cm) and downward (-12 cm) so jumps, landings, and strafes still feel goofy and loose.
        /// </summary>
        static readonly Vector3 BodyViewSwingMin = new(-0.03f, -0.12f, -0.07f);
        static readonly Vector3 BodyViewSwingMax = new(0.10f, 0.035f, 0.07f);

        /// <summary>Generous floppy rotation cap in degrees for the F5 body view (double third-person's 12 degrees).</summary>
        const float BodyViewMaxTurn = 24f;

        /// <summary>Smooth cushion margin in metres around the F5 crosshair keep-out boundaries.</summary>
        const float BodyViewCushion = 0.04f;

        /// <summary>Central sightline box in camera space (metres) used to deflect an empty left hand out of the lens in F5.</summary>
        const float LeftArmClearX = 0.24f, LeftArmClearY = 0.22f, LeftArmFrontFade = 0.12f;

        /// <summary>Beyond this distance from the viewer, metres, remote players skip finger curling.</summary>
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
        /// <summary>The left arm, used when the weapon is held in both hands.</summary>
        readonly HandGrip _support = new();
        Vector3 _supportPosition;
        Quaternion _supportRotation;
        /// <summary>True when the held weapon has a seat for the left hand and the body has a left arm to put on it.</summary>
        bool _hasSupport;
        /// <summary>True when the body's left arm bones were found.</summary>
        bool _leftArm;
        Transform _leanPivot;
        Transform _gun;
        Vector3 _gripPosition;
        Quaternion _gripRotation;
        float _pivotHeight;
        /// <summary>The steady part of the gap between the ragdoll's hand and the aimed wrist, in metres in aim space (right, up, forward). Followed slowly, so only what stays put ends up in it.</summary>
        Vector3 _restOffset;
        /// <summary>The steady part of the gun's turn away from the aimed turn.</summary>
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
        /// <summary>A weapon asked for before <c>Start</c> had found the arm bones; held until then.</summary>
        WeaponHold _waiting;
        bool _hasWaiting;
        /// <summary>
        /// Every weapon copy made so far, by the model it was copied from. A weapon put away is
        /// switched off and kept, never destroyed, and caches its muzzle transform once.
        /// </summary>
        readonly Dictionary<Transform, GunCopy> _copies = new();

        /// <summary>The muzzle of the held weapon (child named "Muzzle", or the weapon root if none).</summary>
        public Transform Muzzle { get; private set; }

        /// <summary>
        /// Puts the gun arm on a player's body. Call once, when the player object wakes up.
        /// </summary>
        /// <param name="graphics">The player's smoothed visual, the object <see cref="PlayerAnimator"/> is on.</param>
        /// <param name="motor">That player's motor.</param>
        /// <param name="view">That player's viewmodel: says where the gun rests and whether it is held in both hands.</param>
        /// <param name="profile">The first-person view's tuning.</param>
        /// <param name="source">The first-person arms prefab the weapon, hand seat and grip settings are read from.</param>
        /// <returns>The new component. It finishes setting itself up in its own <c>Start</c>.</returns>
        public static PlayerHeldWeapon Attach(GameObject graphics, PlayerMotor motor, PlayerViewmodel view, ViewmodelProfile profile, ViewmodelArms source)
        {
            var held = graphics.AddComponent<PlayerHeldWeapon>();
            held._motor = motor;
            held._view = view;
            held._profile = profile;
            held._source = source;
            return held;
        }

        /// <summary>
        /// WHY Start and not Awake: the bones are found through the model's humanoid avatar,
        /// which is only ready once the Animator has set itself up, and the model is still in
        /// its T-pose here (an open hand), which the finger closing is measured from.
        /// <see cref="PlayerAnimator"/> takes the avatar off on the first frame, after every Start.
        /// </summary>
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

        /// <summary>
        /// Swaps the weapon in the body's hand for a copy of another model. Not for every
        /// frame: the first time a model is held it is copied. <see cref="PlayerViewmodel.Equip"/> calls
        /// this; weapon code should call that, so the view and the body change together.
        /// </summary>
        /// <param name="hold">The model and where each hand sits on it.</param>
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

        /// <summary>
        /// Bends the right arm onto the weapon's grip and seats the weapon in the hand.
        /// </summary>
        /// <param name="afterRagdoll">
        /// False before the ragdoll runs: the arm is put exactly on the aimed grip, as the
        /// target the ragdoll chases. True after it: the hand starts where the ragdoll left it
        /// and is only brought part of the way back, by the profile's body shares.
        /// </param>
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

            // Knocked about (or climbing in third person), the arm does what the rest of the body
            // does; the weapon stays in the hand because it is a child of it. In the F5 body view,
            // mantling stays guided using the viewmodel's climb drop/tilt so the arm never waves across the lens.
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
                    // Before the first input arrives, and if someone aims straight along the body's up.
                    if (Vector3.Cross(aim, up).sqrMagnitude < 1e-6f)
                        aim = transform.forward;
                    look = Quaternion.LookRotation(aim, up);
                    gunRotation = look * _view.RestTurn;
                    Vector3 gunPosition = eye + look * gunLocal;
                    // Hold the gun a little further out than the view does in third person. WHY: the body's real
                    // shoulder is nearer the gun than the one the view pretends to have, so at the
                    // view's distance the elbow hung bent.
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

            // Seat the weapon on the hand, not on the point the hand was sent to: looking
            // steeply up or down can put that point out of the arm's reach, and the weapon
            // must not float.
            _gun.SetPositionAndRotation(_grip.Hand.position - gunRotation * _gripPosition, gunRotation);

            // Held in both hands, the left hand goes to its seat on the weapon wherever the
            // right hand ended up carrying it. Held in one, the left arm is left to the clips
            // except in F5 where we deflect it only if it enters the central sightline.
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
