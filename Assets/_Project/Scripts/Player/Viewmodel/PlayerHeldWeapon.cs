// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-14
//  Prompt    : Harvey: "have the actual playermodel follow the first person view model"
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
    /// from the shoulder the ragdoll actually produced, so the gun ends the frame exactly where
    /// the view has it. WHY the second pass: left to the ragdoll alone the arm sagged about
    /// 15 cm and the muzzle drooped some 25 degrees (seen in play), so the body's gun pointed
    /// at the floor in front of what the player was aiming at.</para>
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

        PlayerMotor _motor;
        ViewmodelProfile _profile;
        ViewmodelArms _source;

        readonly HandGrip _grip = new();
        Transform _leanPivot;
        Transform _gun;
        Renderer[] _gunRenderers;
        Vector3 _gripPosition;
        Quaternion _gripRotation;
        float _pivotHeight;
        bool _ready;
        /// <summary>Whether the weapon is currently drawn as a shadow only, as the owner's own body is.</summary>
        bool _shadowOnly;

        /// <summary>
        /// Puts the gun arm on a player's body. Call once, when the player object wakes up.
        /// </summary>
        /// <param name="graphics">The player's smoothed visual, the object <see cref="PlayerAnimator"/> is on.</param>
        /// <param name="motor">That player's motor.</param>
        /// <param name="profile">The first-person view's tuning.</param>
        /// <param name="source">The first-person arms prefab the weapon, hand seat and grip settings are read from.</param>
        /// <returns>The new component. It finishes setting itself up in its own <c>Start</c>.</returns>
        public static PlayerHeldWeapon Attach(GameObject graphics, PlayerMotor motor, ViewmodelProfile profile, ViewmodelArms source)
        {
            var held = graphics.AddComponent<PlayerHeldWeapon>();
            held._motor = motor;
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
            if (_motor == null || _profile == null || avatar == null || seat == null || weapon == null || !_grip.Bind(avatar))
            {
                Debug.LogWarning("[Viewmodel] The body cannot hold the weapon: the player, the view's tuning, the arms prefab's weapon and grip, or the model's right arm bones are missing.", this);
                enabled = false;
                return;
            }

            _grip.CopyTuning(_source.Grip);
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

            Hold();
        }

        /// <summary>
        /// Bends the right arm onto the weapon's grip in front of the eye and seats the weapon
        /// in the hand. Safe to call more than once a frame: it works from wherever the arm is.
        /// </summary>
        internal void Hold()
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

            Quaternion gunRotation = look * Quaternion.Euler(_profile.GunTurn);
            Vector3 gunPosition = eye + look * _profile.GunPosition;

            _grip.Reach(gunPosition + gunRotation * _gripPosition, gunRotation * _gripRotation);

            // Seat the weapon on the hand, not on the point the hand was sent to: looking
            // steeply up or down can put that point out of the arm's reach, and the weapon
            // must not float.
            _gun.SetPositionAndRotation(_grip.Hand.position - gunRotation * _gripPosition, gunRotation);
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
