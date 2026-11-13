// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "set up an fps viewmodel with the characters model we are using"
//  Modified  : Claude Fable 5.2, 2026-11-13 — rebuilt on the NorseFPS approach at Alex's
//              request: the gun is placed in the view and the right arm is bent to reach it
//              (two-bone solve ported from NorseFPS's ViewmodelArms.Solve); the left arm is
//              folded away. Replaces the first version, which played full-body clips and
//              hung the gun off the hand.
//  Modified  : Claude Fable 5.2, 2026-11-13 — the fingers are closed round the grip by
//              this component (Alex: the hand "doesnt wrap around the gun well"); the clip's
//              loose pistol hand was authored for a different gun and hung under ours.
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Sits on the first-person arms prefab. Owns the rig side of the viewmodel:
//              where the gun mount and shoulder are, and bending the arm so the hand stays on
//              the grip. Separate from PlayerViewmodel so the prefab can be posed and
//              previewed by itself: this half knows the skeleton, that half knows the player.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// The first-person arm: a copy of the character's own skeleton with only the arm geometry,
    /// its right hand held on the grip of whatever sits in the gun mount.
    /// </summary>
    /// <remarks>
    /// <para>Each frame, after the idle clip has posed the skeleton: the whole rig is slid so
    /// the right shoulder sits on the shoulder anchor, the left arm is folded to nothing, and
    /// the right arm is bent so its wrist lands on the grip target with the hand turned to
    /// match it. Whatever moves the gun mount (sway, kick, reload) therefore moves the hand
    /// with it, and the hand can never drift off the gun.</para>
    /// <para>Local to the owning client and never synced (AGENTS.md §3).</para>
    /// <para>WHY an analytic two-bone solve and not Humanoid IK or the Animation Rigging
    /// package: this rig cannot use a humanoid avatar (CHARACTER_ANIMATION.md, "The clips"),
    /// and a package would be a new dependency for two bones.</para>
    /// <para>WHY one-handed: NorseFPS settled on it after trying two hands; a support hand
    /// needs a second target fitted to every weapon, and any loose prop can be held later.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    // WHY: PlayerViewmodel (default order) moves the gun mount in its LateUpdate; the arm has
    // to be solved after that or the hand trails the gun by a frame.
    [DefaultExecutionOrder(SolveOrder)]
    public sealed class ViewmodelArms : MonoBehaviour
    {
        /// <summary>After default-order scripts, which place the gun mount for the frame.</summary>
        const int SolveOrder = 100;

        /// <summary>Shortest reach in metres; keeps the solve defined when the target sits on the shoulder.</summary>
        const float MinimumReach = 0.001f;

        /// <summary>Share of the full arm length the solve may use; a dead-straight arm cannot choose an elbow direction.</summary>
        const float MaximumReachShare = 0.999f;

        /// <summary>Joints in one finger, base to tip.</summary>
        const int JointsPerFinger = 3;

        /// <summary>Squared length under which the three arm points count as in a line, in square metres squared.</summary>
        const float StraightArmEpsilon = 1e-12f;

        [Header("Mounts (moved by PlayerViewmodel every frame)")]
        [Tooltip("What the held weapon is parented to. Its position and turn come from the viewmodel profile while playing.")]
        [SerializeField] Transform _gunMount;

        [Tooltip("Where the gun arm's shoulder is held. Its position comes from the viewmodel profile while playing.")]
        [SerializeField] Transform _shoulder;

        [Tooltip("Where the right wrist goes, as a child of the gun mount. Its blue axis is where the fingers point, its green axis runs from the little finger to the index finger. Move this to reseat the hand on the grip.")]
        [SerializeField] Transform _gripTarget;

        [Header("Rig (filled in by the greybox tool)")]
        [Tooltip("The Animator on the arms model.")]
        [SerializeField] Animator _animator;

        [Tooltip("The model object that is slid about so the shoulder stays on its anchor.")]
        [SerializeField] Transform _rig;

        [Tooltip("Right upper arm bone.")]
        [SerializeField] Transform _upperArm;

        [Tooltip("Right forearm bone.")]
        [SerializeField] Transform _lowerArm;

        [Tooltip("Right hand bone.")]
        [SerializeField] Transform _hand;

        [Tooltip("Left upper arm bone. Shrunk to nothing so the left arm never shows.")]
        [SerializeField] Transform _foldedUpperArm;

        [Tooltip("How the hand bone is turned relative to its own knuckles. Worked out by the greybox tool from the model; leave it alone.")]
        [SerializeField] Quaternion _knuckleFrame = Quaternion.identity;

        [Header("Grip")]
        [Tooltip("How far the middle, ring and little fingers close round the grip, in degrees at the knuckle, the middle joint and the last joint. 0 is a flat hand; about 90 at each is a tight fist. Raise the first two for a thinner grip.")]
        [SerializeField] Vector3 _gripCurl = new(55f, 85f, 45f);

        [Tooltip("How far the index finger closes, in degrees at the knuckle, the middle joint and the last joint. Less than the other fingers so it rests on the trigger.")]
        [SerializeField] Vector3 _triggerCurl = new(30f, 55f, 25f);

        [Tooltip("How far the thumb closes across the grip, in degrees at its three joints.")]
        [SerializeField] Vector3 _thumbCurl = new(20f, 20f, 15f);

        [Tooltip("Index finger joints, knuckle first. Filled in by the greybox tool.")]
        [SerializeField] Transform[] _triggerFinger;

        [Tooltip("Middle, ring and little finger joints, three each, knuckle first. Filled in by the greybox tool.")]
        [SerializeField] Transform[] _gripFingers;

        [Tooltip("Thumb joints, base first. Filled in by the greybox tool.")]
        [SerializeField] Transform[] _thumb;

        [Tooltip("Each finger joint's straight, open-hand turn, in the same order as the three lists above (index, grip fingers, thumb). Read from the model by the greybox tool; leave it alone.")]
        [SerializeField] Quaternion[] _openHand;

        [Header("Clip")]
        [Tooltip("Looping pose under the arm solve. It supplies the finger curl and which side the elbow bends to.")]
        [SerializeField] AnimationClip _idle;

        PlayableGraph _graph;
        Renderer[] _renderers;
        bool _visible = true;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);

            if (_animator == null || _idle == null)
            {
                Debug.LogWarning("[Viewmodel] The arms prefab has no Animator or idle clip. Run Close the Deal > Greybox > Rebuild Viewmodel.", this);
                return;
            }

            _graph = PlayableGraph.Create("ViewmodelArms");
            AnimationPlayableOutput.Create(_graph, "Arms", _animator).SetSourcePlayable(AnimationClipPlayable.Create(_graph, _idle));
            _graph.Play();
        }

        void OnDestroy()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }

        /// <summary>Places the gun mount and the shoulder for this frame.</summary>
        /// <param name="gunPosition">Gun mount position relative to this object (the eye), in metres.</param>
        /// <param name="gunRotation">Gun mount rotation relative to this object.</param>
        /// <param name="shoulderPosition">Shoulder anchor position relative to this object, in metres.</param>
        /// <remarks>Call before this component's LateUpdate (any default-order Update or LateUpdate), or follow it with <see cref="SolveArm"/>.</remarks>
        public void SetPose(Vector3 gunPosition, Quaternion gunRotation, Vector3 shoulderPosition)
        {
            if (_gunMount != null)
                _gunMount.SetLocalPositionAndRotation(gunPosition, gunRotation);
            if (_shoulder != null)
                _shoulder.localPosition = shoulderPosition;
        }

        /// <summary>Shows or hides every part of the arms without disabling anything, so the animation keeps running.</summary>
        /// <param name="visible">False stops the arms being drawn by whichever camera renders next.</param>
        public void SetVisible(bool visible)
        {
            if (_visible == visible)
                return;

            _visible = visible;
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].forceRenderingOff = !visible;
        }

        void LateUpdate()
        {
            SolveArm();
        }

        /// <summary>
        /// Puts the shoulder on its anchor, folds the left arm and bends the right arm onto the
        /// grip. Runs every frame from LateUpdate; public so an editor preview can pose the
        /// prefab without playing.
        /// </summary>
        public void SolveArm()
        {
            if (_rig == null || _upperArm == null || _lowerArm == null || _hand == null)
                return;

            // WHY every frame: the Animator rewrites the bones from the clip before this runs.
            if (_foldedUpperArm != null)
                _foldedUpperArm.localScale = Vector3.zero;

            if (_shoulder != null)
                _rig.position += _shoulder.position - _upperArm.position;

            if (_gripTarget != null)
                Reach(_gripTarget.position, _gripTarget.rotation);
        }

        /// <summary>
        /// Bends the arm so the wrist reaches a point, keeping the clip's elbow side, then turns
        /// the hand so its knuckles match the target's axes.
        /// </summary>
        /// <param name="target">World position the wrist should reach, in metres. Clamped to the arm's length.</param>
        /// <param name="targetRotation">World rotation the hand's knuckle frame should take.</param>
        /// <remarks>
        /// Law of cosines: first open or close the elbow to the target distance, then swing the
        /// whole arm about the shoulder onto the target. Ported from NorseFPS.
        /// </remarks>
        void Reach(Vector3 target, Quaternion targetRotation)
        {
            Vector3 a = _upperArm.position, b = _lowerArm.position, c = _hand.position;
            float upper = Vector3.Distance(a, b), lower = Vector3.Distance(b, c);
            float reach = Mathf.Clamp(Vector3.Distance(a, target), MinimumReach, (upper + lower) * MaximumReachShare);

            Vector3 bend = Vector3.Cross(c - a, b - a);
            if (bend.sqrMagnitude < StraightArmEpsilon)
                bend = Vector3.Cross(c - a, _upperArm.up);
            bend.Normalize();

            float elbowNow = Vector3.Angle(a - b, c - b);
            float elbowWanted = Mathf.Acos(Mathf.Clamp((upper * upper + lower * lower - reach * reach) / (2f * upper * lower), -1f, 1f)) * Mathf.Rad2Deg;
            // WHY the sign: with bend = (c - a) x (b - a), a positive turn about bend at the
            // elbow swings the forearm away from the upper arm, which opens the angle.
            _lowerArm.rotation = Quaternion.AngleAxis(elbowWanted - elbowNow, bend) * _lowerArm.rotation;

            _upperArm.rotation = Quaternion.FromToRotation(_hand.position - a, target - a) * _upperArm.rotation;

            _hand.rotation = targetRotation * Quaternion.Inverse(_knuckleFrame);

            // The fingers close toward the palm, which is a turn about the knuckle line (the
            // target's up axis); the thumb closes across the palm, about the finger direction.
            Vector3 knuckleLine = targetRotation * Vector3.up;
            Vector3 fingerLine = targetRotation * Vector3.forward;
            int open = 0;
            open = Curl(_triggerFinger, _triggerCurl, knuckleLine, open);
            open = Curl(_gripFingers, _gripCurl, knuckleLine, open);
            Curl(_thumb, _thumbCurl, fingerLine, open);
        }

        /// <summary>
        /// Sets a run of finger joints (three per finger, base first) from the open hand to a
        /// curl, each joint turned about one world axis.
        /// </summary>
        /// <param name="joints">Joints in threes; may be null or empty.</param>
        /// <param name="curl">Degrees at the first, second and third joint of each finger.</param>
        /// <param name="axis">World axis the joints turn about.</param>
        /// <param name="open">Index into the open-hand list of this run's first joint.</param>
        /// <returns>The open-hand index after this run.</returns>
        /// <remarks>
        /// WHY from the open hand and not on top of the clip: the clip's fingers are already
        /// partly closed for its own gun, so adding to them would differ per clip and per frame.
        /// </remarks>
        int Curl(Transform[] joints, Vector3 curl, Vector3 axis, int open)
        {
            if (joints == null || _openHand == null)
                return open;

            for (int i = 0; i < joints.Length && open < _openHand.Length; i++, open++)
            {
                Transform joint = joints[i];
                if (joint == null)
                    continue;

                // Parent before child, so each joint's turn carries the ones after it.
                joint.localRotation = _openHand[open];
                joint.rotation = Quaternion.AngleAxis(-curl[i % JointsPerFinger], axis) * joint.rotation;
            }

            return open;
        }
    }
}
