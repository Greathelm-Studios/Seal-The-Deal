// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-14
//  Prompt    : Harvey: "have the actual playermodel follow the first person view model"
//  Modified  : Claude Fable 5.2, 2026-11-14 — the body's left arm joins the hold when the
//              weapon is held in both hands (Harvey: "add a left and right hand system and a
//              two handed system").
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Added to the player's smoothed visual when the player spawns. Makes the body
//              everyone else sees (and the player's own shadow and mirror image) hold the
//              pistol out the way the first-person view does: same place in front of the eye,
//              same grip, pointing where the player aims. Separate from PlayerAnimator so the
//              gun arm can be removed or swapped without touching the body's animation.
// -----------------------------------------------------------------------------
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
    /// prefab has been edited by hand, so the greybox tool no longer rebuilds it (§4), and it
    /// is claimed on the workboard. Reading the arms prefab at spawn also means there is no
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

        PlayerMotor _motor;
        PlayerViewmodel _view;
        ViewmodelProfile _profile;
        ViewmodelArms _source;

        readonly HandGrip _grip = new();
        /// <summary>The left arm, used when the weapon is held in both hands.</summary>
        readonly HandGrip _support = new();
        Vector3 _supportPosition;
        Quaternion _supportRotation;
        /// <summary>True when the arms prefab has a seat for the left hand and the body has a left arm to put on it.</summary>
        bool _hasSupport;
        Transform _leanPivot;
        Transform _gun;
        Renderer[] _gunRenderers;
        Vector3 _gripPosition;
        Quaternion _gripRotation;
        float _pivotHeight;
        /// <summary>The steady part of the gap between the ragdoll's hand and the aimed wrist, in metres in aim space (right, up, forward). Followed slowly, so only what stays put ends up in it.</summary>
        Vector3 _restOffset;
        /// <summary>The steady part of the gun's turn away from the aimed turn.</summary>
        Quaternion _restTurn = Quaternion.identity;
        bool _ready;
        /// <summary>Whether the weapon is currently drawn as a shadow only, as the owner's own body is.</summary>
        bool _shadowOnly;

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
            _hasSupport = supportSeat != null && _support.Bind(avatar, true);
            if (_hasSupport)
            {
                _support.CopyTuning(_source.SupportGrip);
                _supportPosition = supportSeat.localPosition;
                _supportRotation = supportSeat.localRotation;
            }
            _gripPosition = seat.localPosition;
            _gripRotation = seat.localRotation;

            // The model hangs off the pivot the motor tilts for a lean; its height on the
            // prefab is fixed, a lean only slides it sideways.
            // The pivot is the child of this object that the model is somewhere under; the
            // Animator itself may sit deeper inside the model.
            _leanPivot = avatar.transform;
            while (_leanPivot.parent != null && _leanPivot.parent != transform)
                _leanPivot = _leanPivot.parent;
            _pivotHeight = _leanPivot.localPosition.y;

            _gun = Instantiate(weapon, _grip.Hand);
            _gun.name = "HeldWeapon";
            // The bones carry the rig's own scale, so the size is set against the world.
            float boneScale = _grip.Hand.lossyScale.x;
            _gun.localScale = weapon.lossyScale / (boneScale > 0f ? boneScale : 1f);
            foreach (Transform part in _gun.GetComponentsInChildren<Transform>(true))
                part.gameObject.layer = gameObject.layer;
            _gunRenderers = _gun.GetComponentsInChildren<Renderer>(true);
            gameObject.AddComponent<HeldWeaponSettle>().Follow(this);
            // The first-person copy casts no shadow (the body casts the player's); this one is the body's.
            SetShadowOnly(false);

            _ready = true;
        }

        void LateUpdate()
        {
            if (!_ready)
                return;

            // The owner sees their own body as a shadow only; the weapon in its hand goes with it.
            bool own = PlayerMotor.Local == _motor;
            if (own != _shadowOnly)
                SetShadowOnly(own);

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

            // Knocked about or climbing, the arm does what the rest of the body does; the
            // weapon stays in the hand because it is a child of it.
            MoveState state = _motor.State;
            if (state != MoveState.Grounded && state != MoveState.Airborne)
                return;

            Vector3 up = _leanPivot.up;
            Vector3 eye = _leanPivot.position + up * (_motor.EyeHeight - _pivotHeight);
            Vector3 aim = _motor.AimDirection;
            // Before the first input arrives, and if someone aims straight along the body's up.
            if (Vector3.Cross(aim, up).sqrMagnitude < 1e-6f)
                aim = transform.forward;
            Quaternion look = Quaternion.LookRotation(aim, up);

            Quaternion gunRotation = look * _view.RestTurn;
            Vector3 gunPosition = eye + look * _view.RestPosition;

            Vector3 wrist = gunPosition + gunRotation * _gripPosition;

            // Hold the gun a little further out than the view does. WHY: the body's real
            // shoulder is nearer the gun than the one the view pretends to have, so at the
            // view's distance the elbow hung bent.
            // WHY a fixed distance along the aim and not "until the arm is straight": that was
            // measured from the ragdoll's shoulder, and the ragdoll's whole torso sways, so the
            // hand was dragged up to 21 cm about with it (measured) however tight the limits
            // below were. From the eye, the hand stays put and the elbow takes up the sway.
            wrist += look * (Vector3.forward * _profile.BodyGunForward);

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

            _grip.Reach(wrist, gunRotation * _gripRotation, look * ElbowSide);

            // Seat the weapon on the hand, not on the point the hand was sent to: looking
            // steeply up or down can put that point out of the arm's reach, and the weapon
            // must not float.
            _gun.SetPositionAndRotation(_grip.Hand.position - gunRotation * _gripPosition, gunRotation);

            // Held in both hands, the left hand goes to its seat on the weapon wherever the
            // right hand ended up carrying it. Held in one, the left arm is left to the clips.
            if (_hasSupport && _view.TwoHanded)
            {
                Vector3 leftElbow = look * new Vector3(-ElbowSide.x, ElbowSide.y, ElbowSide.z);
                _support.Reach(_gun.position + gunRotation * _supportPosition, gunRotation * _supportRotation, leftElbow);
            }
        }

        void SetShadowOnly(bool shadowOnly)
        {
            _shadowOnly = shadowOnly;
            ShadowCastingMode mode = shadowOnly ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            for (int i = 0; i < _gunRenderers.Length; i++)
                _gunRenderers[i].shadowCastingMode = mode;
        }
    }
}
