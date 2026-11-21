using System;
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>Client-local two-bone arm IK solver and finger curl pose for one hand (shared by viewmodel template and player body).</summary>
    [Serializable]
    public sealed class HandGrip
    {
        const float MinimumReach = 0.001f;
        const float MaximumReachShare = 0.999f;
        const int JointsPerFinger = 3;
        const float StraightArmEpsilon = 1e-12f;

        [Header("Arm (filled in by the greybox tool)")]
        [Tooltip("Ticked when this is the left arm. The fingers, thumb and elbow swing then close the mirrored way.")]
        [SerializeField] bool _left;

        [Tooltip("This arm's upper arm bone.")]
        [SerializeField] Transform _upperArm;

        [Tooltip("This arm's forearm bone.")]
        [SerializeField] Transform _lowerArm;

        [Tooltip("This arm's hand bone.")]
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

        Quaternion[] _curledHand;
        Vector3 _cachedGripCurl, _cachedTriggerCurl, _cachedThumbCurl;
        bool _cachedLeft;

        /// <summary>True when the three arm bones are assigned, so <see cref="Reach"/> can run.</summary>
        public bool IsRigged => _upperArm != null && _lowerArm != null && _hand != null;

        /// <summary>This arm's upper arm bone; the shoulder joint is at its position.</summary>
        public Transform UpperArm => _upperArm;

        /// <summary>This arm's hand bone.</summary>
        public Transform Hand => _hand;

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

        static readonly HumanBodyBones[] LeftTriggerBones =
        {
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
        };

        static readonly HumanBodyBones[] LeftGripBones =
        {
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
        };

        static readonly HumanBodyBones[] LeftThumbBones =
        {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
        };

        public bool IsLeft => _left;

        // Mirrored hand curls and swings in the opposite direction around the same bone axes.
        float Side => _left ? -1f : 1f;

        /// <summary>Binds arm/finger bones and measures T-pose open-hand rotations (call once at Start before avatar is cleared).</summary>
        public bool Bind(Animator humanoid, bool left = false)
        {
            if (humanoid == null || humanoid.avatar == null || !humanoid.avatar.isHuman)
                return false;

            _left = left;
            _upperArm = humanoid.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            _lowerArm = humanoid.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            _hand = humanoid.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform index = humanoid.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            Transform middle = humanoid.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            Transform little = humanoid.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            if (!IsRigged || index == null || middle == null || little == null)
                return false;

            // The same measure the greybox tool takes for the first-person arms: forward runs
            // wrist to middle knuckle, up runs little-finger knuckle to index knuckle.
            _knuckleFrame = Quaternion.Inverse(_hand.rotation)
                * Quaternion.LookRotation(middle.position - _hand.position, index.position - little.position);

            _triggerFinger = Joints(humanoid, left ? LeftTriggerBones : TriggerBones);
            _gripFingers = Joints(humanoid, left ? LeftGripBones : GripBones);
            _thumb = Joints(humanoid, left ? LeftThumbBones : ThumbBones);
            _openHand = new Quaternion[_triggerFinger.Length + _gripFingers.Length + _thumb.Length];
            int open = 0;
            open = ReadOpen(_triggerFinger, open);
            open = ReadOpen(_gripFingers, open);
            ReadOpen(_thumb, open);
            _curledHand = null;
            return true;
        }

        /// <summary>Copies finger curl and elbow swing tuning from another grip.</summary>
        public void CopyTuning(HandGrip from)
        {
            if (from == null)
                return;

            _elbowSwing = from._elbowSwing;
            _gripCurl = from._gripCurl;
            _triggerCurl = from._triggerCurl;
            _thumbCurl = from._thumbCurl;
            _curledHand = null;
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

        /// <summary>Solves the two-bone arm onto <paramref name="target"/>, orients the hand, and applies finger curls.</summary>
        public void Reach(Vector3 target, Quaternion targetRotation, Vector3 elbowToward = default, bool curlFingers = true)
        {
            if (!IsRigged)
                return;

            SolveArmBones(target, elbowToward);

            _hand.rotation = targetRotation * Quaternion.Inverse(_knuckleFrame);

            if (!curlFingers)
                return;

            EnsureCurledHand();
            int open = 0;
            open = Curl(_triggerFinger, open);
            open = Curl(_gripFingers, open);
            Curl(_thumb, open);
        }

        /// <summary>Solves only upper arm and forearm toward <paramref name="target"/> without turning the hand or curling fingers.</summary>
        public void ReachArm(Vector3 target, Vector3 elbowToward = default)
        {
            if (!IsRigged)
                return;

            SolveArmBones(target, elbowToward);
        }

        void SolveArmBones(Vector3 target, Vector3 elbowToward)
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
            _lowerArm.rotation = Quaternion.AngleAxis(elbowWanted - elbowNow, bend) * _lowerArm.rotation;

            _upperArm.rotation = Quaternion.FromToRotation(_hand.position - a, target - a) * _upperArm.rotation;

            Vector3 line = (target - a).normalized;
            if (elbowToward != Vector3.zero)
            {
                Vector3 elbowNowSide = Vector3.ProjectOnPlane(_lowerArm.position - a, line);
                Vector3 elbowWantedSide = Vector3.ProjectOnPlane(elbowToward, line);
                if (elbowNowSide.sqrMagnitude > StraightArmEpsilon && elbowWantedSide.sqrMagnitude > StraightArmEpsilon)
                    _upperArm.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(elbowNowSide, elbowWantedSide, line), line) * _upperArm.rotation;
            }
            else if (_elbowSwing != 0f)
            {
                _upperArm.rotation = Quaternion.AngleAxis(_elbowSwing * Side, line) * _upperArm.rotation;
            }
        }

        void EnsureCurledHand()
        {
            if (_openHand == null || _hand == null)
                return;
            if (_curledHand != null && _curledHand.Length == _openHand.Length
                && _cachedGripCurl == _gripCurl
                && _cachedTriggerCurl == _triggerCurl
                && _cachedThumbCurl == _thumbCurl
                && _cachedLeft == _left)
                return;

            if (_curledHand == null || _curledHand.Length != _openHand.Length)
                _curledHand = new Quaternion[_openHand.Length];
            _cachedGripCurl = _gripCurl;
            _cachedTriggerCurl = _triggerCurl;
            _cachedThumbCurl = _thumbCurl;
            _cachedLeft = _left;

            Vector3 knuckleAxis = _knuckleFrame * Vector3.up;
            Vector3 fingerAxis = _knuckleFrame * Vector3.forward;
            int open = 0;
            open = BakeCurl(_triggerFinger, _triggerCurl, knuckleAxis, open);
            open = BakeCurl(_gripFingers, _gripCurl, knuckleAxis, open);
            BakeCurl(_thumb, _thumbCurl, fingerAxis, open);
        }

        int BakeCurl(Transform[] joints, Vector3 curl, Vector3 handAxis, int open)
        {
            if (joints == null || _openHand == null)
                return open;

            Vector3 axis = handAxis;
            for (int i = 0; i < joints.Length && open < _openHand.Length; i++, open++)
            {
                Transform joint = joints[i];
                if (i % JointsPerFinger == 0)
                {
                    axis = handAxis;
                    if (joint != null && joint.parent != null && joint.parent != _hand)
                    {
                        Quaternion parentToHand = Quaternion.identity;
                        Transform t = joint.parent;
                        while (t != null && t != _hand)
                        {
                            parentToHand = t.localRotation * parentToHand;
                            t = t.parent;
                        }
                        if (t == _hand)
                            axis = Quaternion.Inverse(parentToHand) * handAxis;
                    }
                }

                Quaternion openRot = _openHand[open];
                _curledHand[open] = Quaternion.AngleAxis(-curl[i % JointsPerFinger] * Side, axis) * openRot;
                axis = Quaternion.Inverse(openRot) * axis;
            }

            return open;
        }

        int Curl(Transform[] joints, int open)
        {
            if (joints == null || _curledHand == null)
                return open;

            for (int i = 0; i < joints.Length && open < _curledHand.Length; i++, open++)
            {
                Transform joint = joints[i];
                if (joint != null)
                    joint.localRotation = _curledHand[open];
            }

            return open;
        }
    }
}
