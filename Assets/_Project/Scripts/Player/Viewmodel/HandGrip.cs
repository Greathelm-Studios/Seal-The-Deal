// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-14
//  Prompt    : Harvey: "have the actual playermodel follow the first person view model"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : One right arm holding one grip: the bones, the finger joints and the tuning,
//              and the solve that bends the arm onto a grip point and closes the hand. Lifted
//              out of ViewmodelArms so the first-person arm and the body other players see
//              hold the gun with the same code and the same numbers.
// -----------------------------------------------------------------------------
using System;
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// A right arm and hand that can be bent onto a grip. Serialized inside whichever
    /// component owns the skeleton (<see cref="ViewmodelArms"/> for the first-person arm,
    /// <see cref="PlayerHeldWeapon"/> for the body).
    /// </summary>
    /// <remarks>
    /// <para>Local to each client and never synced (AGENTS.md §3).</para>
    /// <para>WHY a plain serializable class and not a component: it has no life of its own.
    /// The owning component decides when in the frame the arm is solved (after its own pose
    /// source has written the bones) and where the grip is.</para>
    /// <para>WHY an analytic two-bone solve and not Humanoid IK or the Animation Rigging
    /// package: this rig cannot use a humanoid avatar while playing (CHARACTER_ANIMATION.md,
    /// "The clips"), and a package would be a new dependency for two bones.</para>
    /// </remarks>
    [Serializable]
    public sealed class HandGrip
    {
        /// <summary>Shortest reach in metres; keeps the solve defined when the target sits on the shoulder.</summary>
        const float MinimumReach = 0.001f;

        /// <summary>Share of the full arm length the solve may use; a dead-straight arm cannot choose an elbow direction.</summary>
        const float MaximumReachShare = 0.999f;

        /// <summary>Joints in one finger, base to tip.</summary>
        const int JointsPerFinger = 3;

        /// <summary>Squared length under which the three arm points count as in a line, in square metres squared.</summary>
        const float StraightArmEpsilon = 1e-12f;

        [Header("Arm (filled in by the greybox tool)")]
        [Tooltip("Right upper arm bone.")]
        [SerializeField] Transform _upperArm;

        [Tooltip("Right forearm bone.")]
        [SerializeField] Transform _lowerArm;

        [Tooltip("Right hand bone.")]
        [SerializeField] Transform _hand;

        [Tooltip("How the hand bone is turned relative to its own knuckles. Worked out by the greybox tool from the model; leave it alone.")]
        [SerializeField] Quaternion _knuckleFrame = Quaternion.identity;

        [Tooltip("How far the elbow is swung out to the side from where the pose underneath hangs it, in degrees. 0 leaves the elbow under the gun, which bends the wrist sharply; higher swings it outward so the forearm lines up behind the gun.")]
        [Range(-120f, 120f)] [SerializeField] float _elbowSwing = 20f;

        [Header("Grip")]
        [Tooltip("How far the middle, ring and little fingers close round the grip, in degrees at the knuckle, the middle joint and the last joint. 0 is a flat hand; about 90 at each is a tight fist. Raise the first two for a thinner grip.")]
        [SerializeField] Vector3 _gripCurl = new(80f, 95f, 50f);

        [Tooltip("How far the index finger closes, in degrees at the knuckle, the middle joint and the last joint. Less than the other fingers so it rests on the trigger.")]
        [SerializeField] Vector3 _triggerCurl = new(50f, 70f, 35f);

        [Tooltip("How far the thumb closes across the grip, in degrees at its three joints.")]
        [SerializeField] Vector3 _thumbCurl = new(45f, 35f, 25f);

        [Tooltip("Index finger joints, knuckle first. Filled in by the greybox tool.")]
        [SerializeField] Transform[] _triggerFinger;

        [Tooltip("Middle, ring and little finger joints, three each, knuckle first. Filled in by the greybox tool.")]
        [SerializeField] Transform[] _gripFingers;

        [Tooltip("Thumb joints, base first. Filled in by the greybox tool.")]
        [SerializeField] Transform[] _thumb;

        [Tooltip("Each finger joint's straight, open-hand turn, in the same order as the three lists above (index, grip fingers, thumb). Read from the model by the greybox tool; leave it alone.")]
        [SerializeField] Quaternion[] _openHand;

        /// <summary>True when the three arm bones are assigned, so <see cref="Reach"/> can run.</summary>
        public bool IsRigged => _upperArm != null && _lowerArm != null && _hand != null;

        /// <summary>The right upper arm bone; the shoulder joint is at its position.</summary>
        public Transform UpperArm => _upperArm;

        /// <summary>The right hand bone.</summary>
        public Transform Hand => _hand;

        /// <summary>The three joints of each finger, base first, as the humanoid avatar names them: index, then middle, ring and little, then thumb.</summary>
        static readonly HumanBodyBones[] TriggerBones =
        {
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
        };

        static readonly HumanBodyBones[] GripBones =
        {
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
        };

        static readonly HumanBodyBones[] ThumbBones =
        {
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
        };

        /// <summary>
        /// Finds the arm and finger bones on a humanoid model and measures the hand, for a
        /// skeleton that was not wired by the greybox tool (the player's body).
        /// </summary>
        /// <param name="humanoid">The model's Animator, with its humanoid avatar still on. The model must be in its T-pose: the open hand is read from it.</param>
        /// <returns>False when the model has no right arm or no index, middle and little finger knuckles; nothing usable was found.</returns>
        /// <remarks>Call once at spawn, never per frame: it asks the avatar for bones and allocates the joint lists.</remarks>
        public bool Bind(Animator humanoid)
        {
            if (humanoid == null || humanoid.avatar == null || !humanoid.avatar.isHuman)
                return false;

            _upperArm = humanoid.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _lowerArm = humanoid.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _hand = humanoid.GetBoneTransform(HumanBodyBones.RightHand);
            Transform index = humanoid.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            Transform middle = humanoid.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Transform little = humanoid.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            if (!IsRigged || index == null || middle == null || little == null)
                return false;

            // The same measure the greybox tool takes for the first-person arms: forward runs
            // wrist to middle knuckle, up runs little-finger knuckle to index knuckle.
            _knuckleFrame = Quaternion.Inverse(_hand.rotation)
                * Quaternion.LookRotation(middle.position - _hand.position, index.position - little.position);

            _triggerFinger = Joints(humanoid, TriggerBones);
            _gripFingers = Joints(humanoid, GripBones);
            _thumb = Joints(humanoid, ThumbBones);
            _openHand = new Quaternion[_triggerFinger.Length + _gripFingers.Length + _thumb.Length];
            int open = 0;
            open = ReadOpen(_triggerFinger, open);
            open = ReadOpen(_gripFingers, open);
            ReadOpen(_thumb, open);
            return true;
        }

        /// <summary>Takes the finger curls and elbow swing from another grip, so two arms hold the same way.</summary>
        /// <param name="from">The grip whose tuning to copy; its bones are not touched.</param>
        public void CopyTuning(HandGrip from)
        {
            if (from == null)
                return;

            _elbowSwing = from._elbowSwing;
            _gripCurl = from._gripCurl;
            _triggerCurl = from._triggerCurl;
            _thumbCurl = from._thumbCurl;
        }

        static Transform[] Joints(Animator humanoid, HumanBodyBones[] bones)
        {
            var joints = new Transform[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                joints[i] = humanoid.GetBoneTransform(bones[i]);
            return joints;
        }

        int ReadOpen(Transform[] joints, int open)
        {
            for (int i = 0; i < joints.Length; i++, open++)
                _openHand[open] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;
            return open;
        }

        /// <summary>
        /// Bends the arm so the wrist reaches a point, keeping the elbow side of the pose
        /// already on the bones (then swung by the elbow swing), turns the hand so its knuckles
        /// match the target's axes, and closes the fingers.
        /// </summary>
        /// <param name="target">World position the wrist should reach, in metres. Clamped to the arm's length.</param>
        /// <param name="targetRotation">World rotation the hand's knuckle frame should take: its forward is where the fingers point, its up runs from the little finger to the index finger.</param>
        /// <remarks>
        /// Law of cosines: first open or close the elbow to the target distance, then swing the
        /// whole arm about the shoulder onto the target. Ported from NorseFPS. Call it after
        /// whatever poses the skeleton this frame, or that pose overwrites it.
        /// </remarks>
        public void Reach(Vector3 target, Quaternion targetRotation)
        {
            if (!IsRigged)
                return;

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

            // Swing the elbow round the line from shoulder to wrist. The wrist is on that line,
            // so it stays on the grip; only where the elbow hangs changes.
            // WHY: the poses underneath hang the elbow straight under the gun, which brings the
            // forearm up at the hand steeply and cocks the wrist. Out to the side it comes in
            // behind the gun.
            if (_elbowSwing != 0f)
                _upperArm.rotation = Quaternion.AngleAxis(_elbowSwing, (target - a).normalized) * _upperArm.rotation;

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
        /// WHY from the open hand and not on top of the pose underneath: a clip's fingers are
        /// already partly closed, so adding to them would differ per clip and per frame.
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
