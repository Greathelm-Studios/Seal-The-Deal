// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-12
//  Prompt    : Alex chose "True ragdoll body" after several procedural passes felt stiff:
//              "running animations and transitions are sloppy, the wobble is too tame now ...
//              look at TABS, gang beast"
//  Modified  : Claude Fable 5.2, 2026-11-12 — the body is carried along with part of the
//              player's acceleration (Alex: setting off and stopping "too floppy / wobbly")
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Gives the player a body made of real rigidbodies and joints that is driven
//              toward the pose PlayerAnimator writes, and shows that body instead of the pose.
//              Its own component so the pose (PlayerAnimator) and the physics that chases it
//              stay separate: the animator can be read and tuned without the physics, and this
//              can be removed again by deleting one component.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// An active ragdoll: eleven rigidbodies joined like a skeleton, whose joints are motors
    /// pulling toward the animator's pose (an animation clip's, or the procedural one), and
    /// whose hips are pulled along by the player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ownership (AGENTS.md §3):</b> local to each client and synced by nothing. It never
    /// moves the player: the predicted capsule on <see cref="PlayerMotor"/> still decides where
    /// the player is, and the ragdoll's hips are dragged after it on a spring. The bodies live in
    /// <see cref="RagdollWorld"/>, a physics scene of their own, so they cannot push the capsule
    /// or a prop and are not re-simulated when FishNet reconciles. Two clients will see the same
    /// player's limbs in different places; nothing reads them back.
    /// </para>
    /// <para>
    /// <b>Each frame:</b> the animator has just written its pose onto the skeleton. This reads
    /// that pose as targets, aims every joint's motor at it, steps the ragdoll world, then
    /// overwrites the skeleton with where the bodies actually are. The animator writes its pose
    /// afresh next frame, so nothing feeds back.
    /// </para>
    /// <para>
    /// <b>What is and is not physical:</b> the body hangs from its hips, which are held up and
    /// pulled along by a spring to the player; it does not balance on its feet, so it cannot
    /// fall over. Everything above and below the hips is free to lag, swing, sag and be knocked
    /// about. Knees and elbows are hinges, and the spine, neck and hips have limits; the
    /// shoulders are free. The ragdoll's own parts do not collide with each
    /// other (the arms would catch on the ribs every stride); they collide with the level and
    /// with other players' ragdolls.
    /// </para>
    /// <para>
    /// The bodies are built in <c>Start</c> from the model's T-pose, so the model must spawn in
    /// it. Runs after <see cref="PlayerAnimator"/>. Allocates nothing per frame.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class PlayerRagdoll : MonoBehaviour
    {
        /// <summary>After PlayerAnimator (100), which must have written this frame's pose first.</summary>
        const int ExecutionOrder = 110;

        const int Hips = 0, Chest = 1, Head = 2,
            LeftUpperArm = 3, LeftLowerArm = 4, RightUpperArm = 5, RightLowerArm = 6,
            LeftUpperLeg = 7, LeftLowerLeg = 8, RightUpperLeg = 9, RightLowerLeg = 10, PartCount = 11;

        /// <summary>
        /// Each part's mass, kilograms, in the order of the part numbers above. WHY these: only
        /// the proportions matter (the hips' spring is scaled by the total), and they are skewed
        /// on purpose. A real head is about 7% of a body; here it is 11%, and the head and chest together are
        /// a third, so the top of the body carries on when the legs stop. (At over half, which was tried first, the joints could not hold the body up through the motor's 45 m/s² starts.) That is the
        /// top-heavy feel asked for.
        /// </summary>
        static readonly float[] Masses = { 10f, 10f, 5f, 2f, 1.5f, 2f, 1.5f, 5f, 3.5f, 5f, 3.5f };

        /// <summary>Thickness of each part's collider, metres (a radius), in part order. Measured roughly off the 1.8 m model.</summary>
        static readonly float[] Radii = { 0.14f, 0.13f, 0.13f, 0.05f, 0.045f, 0.05f, 0.045f, 0.075f, 0.06f, 0.075f, 0.06f };

        /// <summary>How far the head's ball sits above the neck joint, metres, and how far a forearm's or shin's collider runs past the wrist or ankle to cover the hand or foot. Measured roughly off the model.</summary>
        const float HeadHeight = 0.13f, HandLength = 0.09f;

        /// <summary>
        /// A ragdoll whose hips are further than this from where the player's hips should be,
        /// metres, has been left behind by a teleport (a spawn, a big correction) and is put
        /// straight back on the pose. WHY 2.5: beyond anything the hips' spring stretches to in
        /// play, including a blast.
        /// </summary>
        const float SnapDistance = 2.5f;

        /// <summary>
        /// The most acceleration the carry passes on to the body, metres per second squared.
        /// WHY 150: over twice the motor's hardest braking (60), so every start and stop goes
        /// through whole, while a jump's launch (about 8 m/s in one tick, several hundred) and a
        /// correction's jolt are cut down and still read as a kick.
        /// </summary>
        const float MaxCarryPush = 150f;

        /// <summary>
        /// How many times harder the hips are to turn than their size and weight make them.
        /// WHY: the hips are the part the rest hangs from, held level by a spring rather than
        /// balanced on the legs. At their real resistance, the motors swinging 17 kg of legs
        /// through a full running stride rocked them some 50 degrees each way (measured in the
        /// run preview); a real pelvis is steadied by feet pushing on the ground, which these
        /// legs do not do. The spring that holds the hips level works on turning speed, not
        /// force, so it is unaffected. At 25 the rocking stays within about 2 degrees of the pose in
        /// the same preview; it is the only value tried, so there may be room to lower it for a
        /// looser pelvis.
        /// </summary>
        const float HipsSteadiness = 25f;

        /// <summary>
        /// Rigidbody settings for every part. Damping: a little air drag so limbs do not spin for
        /// ever. Solver passes: more than Unity's default 6 because a chain of driven joints
        /// needs them to stay together. Speed caps: stop one bad contact from firing a limb
        /// across the room. Picked from Unity's guidance for ragdolls, not measured.
        /// </summary>
        const float LinearDamping = 0.2f, AngularDamping = 1.5f, MaxAngularSpeed = 25f, MaxDepenetrationSpeed = 3f;
        const int SolverPasses = 14, SolverVelocityPasses = 4;

        /// <summary>
        /// Joint limits, degrees. Knees and elbows may go this far past straight and bend this
        /// far (a real knee manages about 140, an elbow 150); a hinge has this much play about its
        /// other axes (wide, because the pose's arm hangs a little out from the body and its bend
        /// is not exactly about the elbow's axis). The spine and neck bend this far any way from the T-pose; a thigh swings,
        /// twists and spreads this far. Taken from ordinary ranges of human joints, opened up a
        /// little so the body can still be thrown about.
        /// </summary>
        const float KneeStraight = 5f, KneeBent = 150f, ElbowStraight = 5f, ElbowBent = 155f, HingeSlack = 20f;
        const float SpineBend = 50f, NeckBend = 55f, HipSwing = 120f, HipTwist = 45f, HipSpread = 55f;

        [Header("References")]
        [Tooltip("Tuning for how strongly the ragdoll is pulled to its pose. Read at runtime, never written.")]
        [SerializeField] AnimationProfile _profile;

        [Tooltip("The animator that writes the pose this ragdoll chases, and says when the body should go limp.")]
        [SerializeField] PlayerAnimator _animator;

        [Tooltip("The character model's Animator. Only used once, at spawn, to find the skeleton's bones through its humanoid avatar.")]
        [SerializeField] Animator _avatar;

        [Tooltip("What the ragdoll bumps into: everything solid in the level, but not players.")]
        [SerializeField] LayerMask _solid = ~0;

        /// <summary>One rigidbody of the ragdoll and the bone it stands for.</summary>
        struct Part
        {
            /// <summary>The bone this part turns. Its rotation and the body's are the same.</summary>
            public Transform Bone;
            public Rigidbody Body;
            /// <summary>The joint to this part's parent; null for the hips.</summary>
            public ConfigurableJoint Joint;
            public int Parent;
            /// <summary>This part's rotation relative to its parent in the T-pose; the joint's motor is aimed relative to it.</summary>
            public Quaternion StartLocal;
            /// <summary>The joint's own frame (its hinge axis is this frame's X) relative to the body; the motor's target has to be given in it.</summary>
            public Quaternion Frame;
            /// <summary>Where the pose wants this part this frame, world space.</summary>
            public Vector3 TargetPosition;
            public Quaternion TargetRotation;
            /// <summary>Where the body was one physics step ago, for blending between steps.</summary>
            public Vector3 WasPosition;
            public Quaternion WasRotation;
        }

        readonly Part[] _parts = new Part[PartCount];
        GameObject _root;
        Transform _neck, _leftFoot, _rightFoot;
        /// <summary>T-pose local rotation of the neck, which this component puts back to rest each frame: it moves rigidly with the chest part, and the head part hinges on it.</summary>
        Quaternion _neckRest;
        float _totalMass;
        Vector3 _hipsTargetWas;
        Vector3 _hipsTargetSpeed;
        /// <summary>Where the player's smoothed visual was last frame, and how fast it was moving, metres and metres per second.</summary>
        Vector3 _playerWas, _playerSpeed;
        /// <summary>The share of the player's acceleration this frame that every part is pushed along with, metres per second squared.</summary>
        Vector3 _carryPush;
        float _nextLevelCopy;
        bool _built;
        bool _posed;

        /// <summary>Where the ragdoll is, world space; <see cref="RagdollWorld"/> keeps level geometry near it.</summary>
        public Vector3 Centre => _parts[Hips].TargetPosition;

        void Start()
        {
            if (_profile == null || _animator == null || _avatar == null || _avatar.avatar == null || !_avatar.avatar.isHuman)
            {
                Debug.LogError("[PlayerRagdoll] Needs a profile, the player's animator and a humanoid Animator; the character will show the plain pose.", this);
                enabled = false;
                return;
            }

            Build();
        }

        void OnDestroy()
        {
            if (!_built)
                return;
            RagdollWorld.Remove(this);
            if (_root != null)
                Destroy(_root);
        }

        /// <summary>Makes the eleven bodies and ten joints from the model's T-pose and moves them into the ragdoll world.</summary>
        void Build()
        {
            Transform Bone(HumanBodyBones id) => _avatar.GetBoneTransform(id);
            Transform spine = Bone(HumanBodyBones.Spine);
            _neck = Bone(HumanBodyBones.Neck);
            Transform head = Bone(HumanBodyBones.Head);
            Transform neckJoint = _neck != null ? _neck : head;
            _leftFoot = Bone(HumanBodyBones.LeftFoot);
            _rightFoot = Bone(HumanBodyBones.RightFoot);
            Transform leftHand = Bone(HumanBodyBones.LeftHand), rightHand = Bone(HumanBodyBones.RightHand);

            if (_neck != null) _neckRest = _neck.localRotation;

            _root = new GameObject("Ragdoll");
            var slippery = new PhysicsMaterial("Ragdoll")
            {
                dynamicFriction = 0.2f,
                staticFriction = 0.2f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            Vector3 up = _avatar.transform.up;
            // part, bone, parent, joint position, far end of the collider
            Make(Hips, Bone(HumanBodyBones.Hips), -1, Bone(HumanBodyBones.Hips).position, Bone(HumanBodyBones.Hips).position, slippery);
            Make(Chest, spine, Hips, spine.position, neckJoint.position, slippery);
            Make(Head, head, Chest, neckJoint.position, neckJoint.position + up * (HeadHeight * 2f), slippery);
            Make(LeftUpperArm, Bone(HumanBodyBones.LeftUpperArm), Chest, Bone(HumanBodyBones.LeftUpperArm).position, Bone(HumanBodyBones.LeftLowerArm).position, slippery);
            Make(LeftLowerArm, Bone(HumanBodyBones.LeftLowerArm), LeftUpperArm, Bone(HumanBodyBones.LeftLowerArm).position, Past(Bone(HumanBodyBones.LeftLowerArm).position, leftHand.position), slippery);
            Make(RightUpperArm, Bone(HumanBodyBones.RightUpperArm), Chest, Bone(HumanBodyBones.RightUpperArm).position, Bone(HumanBodyBones.RightLowerArm).position, slippery);
            Make(RightLowerArm, Bone(HumanBodyBones.RightLowerArm), RightUpperArm, Bone(HumanBodyBones.RightLowerArm).position, Past(Bone(HumanBodyBones.RightLowerArm).position, rightHand.position), slippery);
            Make(LeftUpperLeg, Bone(HumanBodyBones.LeftUpperLeg), Hips, Bone(HumanBodyBones.LeftUpperLeg).position, Bone(HumanBodyBones.LeftLowerLeg).position, slippery);
            Make(LeftLowerLeg, Bone(HumanBodyBones.LeftLowerLeg), LeftUpperLeg, Bone(HumanBodyBones.LeftLowerLeg).position, _leftFoot.position, slippery);
            Make(RightUpperLeg, Bone(HumanBodyBones.RightUpperLeg), Hips, Bone(HumanBodyBones.RightUpperLeg).position, Bone(HumanBodyBones.RightLowerLeg).position, slippery);
            Make(RightLowerLeg, Bone(HumanBodyBones.RightLowerLeg), RightUpperLeg, Bone(HumanBodyBones.RightLowerLeg).position, _rightFoot.position, slippery);

            // ---- How far each joint may turn from the T-pose. Knees and elbows are hinges: they
            // bend one way about one axis and barely at all about the others. The rest are ball
            // joints with a cone of movement. Shoulders are left free: an arm reaches almost
            // anywhere.
            Vector3 right = _avatar.transform.right, forward = _avatar.transform.forward;
            Hinge(LeftLowerLeg, right, up, KneeStraight, KneeBent, false);
            Hinge(RightLowerLeg, right, up, KneeStraight, KneeBent, false);
            // An arm in the T-pose bends forward at the elbow, about the up axis: a positive turn
            // for the left arm and a negative one for the right, so the right's limits are the
            // other way round. (Giving the right arm the opposite axis instead does not work:
            // seen in the run preview, its elbow stayed locked straight.)
            Hinge(LeftLowerArm, up, forward, ElbowStraight, ElbowBent, false);
            Hinge(RightLowerArm, up, forward, ElbowStraight, ElbowBent, true);
            Ball(Chest, SpineBend, SpineBend, SpineBend);
            Ball(Head, NeckBend, NeckBend, NeckBend);
            Ball(LeftUpperLeg, HipSwing, HipTwist, HipSpread);
            Ball(RightUpperLeg, HipSwing, HipTwist, HipSpread);

            // A body's own parts pass through each other; see the class remarks.
            Collider[] colliders = _root.GetComponentsInChildren<Collider>();
            for (int a = 0; a < colliders.Length; a++)
            {
                for (int b = a + 1; b < colliders.Length; b++)
                    Physics.IgnoreCollision(colliders[a], colliders[b]);
            }

            _parts[Hips].Body.inertiaTensor *= HipsSteadiness;
            _hipsTargetWas = _parts[Hips].Body.position;
            _playerWas = transform.position;
            RagdollWorld.Add(this, _root);
            _built = true;
        }

        /// <summary>
        /// Makes a joint a hinge: free to bend between two angles about one axis, and almost
        /// rigid about the others.
        /// </summary>
        /// <param name="index">The part whose joint to its parent this is.</param>
        /// <param name="axis">The hinge's axis, world space, in the T-pose.</param>
        /// <param name="across">Any direction at right angles to it, world space.</param>
        /// <param name="straight">How far past straight it may go, degrees.</param>
        /// <param name="bent">How far it may bend, degrees.</param>
        /// <param name="bendsBackward">False if bending is a positive turn about the axis, true if it is a negative one.</param>
        void Hinge(int index, Vector3 axis, Vector3 across, float straight, float bent, bool bendsBackward)
        {
            ref Part part = ref _parts[index];
            ConfigurableJoint link = part.Joint;
            Quaternion toLocal = Quaternion.Inverse(part.Body.rotation);
            link.axis = toLocal * axis;
            link.secondaryAxis = toLocal * across;
            part.Frame = FrameOf(link);
            link.angularXMotion = link.angularYMotion = link.angularZMotion = ConfigurableJointMotion.Limited;
            // WHY bending is the low side for a positive turn: Unity measures a ConfigurableJoint's
            // X angle the opposite way round to a turn of the body about the joint's axis. Checked
            // in the run preview; with the signs the other way the knees lock straight.
            link.lowAngularXLimit = new SoftJointLimit { limit = bendsBackward ? -straight : -bent };
            link.highAngularXLimit = new SoftJointLimit { limit = bendsBackward ? bent : straight };
            link.angularYLimit = link.angularZLimit = new SoftJointLimit { limit = HingeSlack };
        }

        /// <summary>
        /// Limits a ball joint to a cone about its T-pose direction.
        /// </summary>
        /// <param name="index">The part whose joint to its parent this is.</param>
        /// <param name="x">Degrees each way about the part's own X axis.</param>
        /// <param name="y">Degrees each way about its Y axis.</param>
        /// <param name="z">Degrees each way about its Z axis.</param>
        void Ball(int index, float x, float y, float z)
        {
            ConfigurableJoint link = _parts[index].Joint;
            link.angularXMotion = link.angularYMotion = link.angularZMotion = ConfigurableJointMotion.Limited;
            link.lowAngularXLimit = new SoftJointLimit { limit = -x };
            link.highAngularXLimit = new SoftJointLimit { limit = x };
            link.angularYLimit = new SoftJointLimit { limit = y };
            link.angularZLimit = new SoftJointLimit { limit = z };
        }

        /// <summary>
        /// The rotation from a body's own axes to its joint's axes.
        /// </summary>
        /// <remarks>
        /// A ConfigurableJoint's motor target is given in the joint's frame, whose X is the
        /// joint's axis and whose Y is close to its secondary axis.
        /// </remarks>
        static Quaternion FrameOf(ConfigurableJoint link)
        {
            Vector3 x = link.axis.normalized;
            Vector3 z = Vector3.Cross(x, link.secondaryAxis).normalized;
            Vector3 y = Vector3.Cross(z, x).normalized;
            return Quaternion.LookRotation(z, y);
        }

        /// <summary>A point a hand's length beyond <paramref name="to"/> on the line from <paramref name="from"/>.</summary>
        static Vector3 Past(Vector3 from, Vector3 to) => to + (to - from).normalized * HandLength;

        /// <summary>
        /// Makes one part: a rigidbody at a joint of the skeleton, turned the same way as its
        /// bone, with a capsule (or a ball, if it has no length) running to its far end, and a
        /// motorised joint to its parent.
        /// </summary>
        void Make(int index, Transform bone, int parent, Vector3 joint, Vector3 end, PhysicsMaterial material)
        {
            var go = new GameObject(bone.name);
            go.transform.SetParent(_root.transform, false);
            go.transform.SetPositionAndRotation(joint, bone.rotation);

            var shape = new GameObject("Shape");
            shape.transform.SetParent(go.transform, false);
            float length = Vector3.Distance(joint, end);
            float radius = Radii[index];
            if (length > radius * 2f)
            {
                // A capsule's long axis is its own Y; the child object is turned to lay it along the bone.
                shape.transform.SetPositionAndRotation((joint + end) * 0.5f, Quaternion.FromToRotation(Vector3.up, end - joint));
                var capsule = shape.AddComponent<CapsuleCollider>();
                capsule.radius = radius;
                capsule.height = length + radius * 2f;
                capsule.material = material;
            }
            else
            {
                shape.transform.position = (joint + end) * 0.5f;
                var ball = shape.AddComponent<SphereCollider>();
                ball.radius = radius;
                ball.material = material;
            }

            var body = go.AddComponent<Rigidbody>();
            body.mass = Masses[index];
            // Gravity is applied by hand each step, scaled by the profile.
            body.useGravity = false;
            body.linearDamping = LinearDamping;
            body.angularDamping = AngularDamping;
            body.maxAngularVelocity = MaxAngularSpeed;
            body.maxDepenetrationVelocity = MaxDepenetrationSpeed;
            body.solverIterations = SolverPasses;
            body.solverVelocityIterations = SolverVelocityPasses;
            _totalMass += Masses[index];

            ref Part part = ref _parts[index];
            part.Bone = bone;
            part.Body = body;
            part.Parent = parent;
            part.TargetPosition = part.WasPosition = joint;
            part.TargetRotation = part.WasRotation = bone.rotation;
            if (parent < 0)
                return;

            Rigidbody parentBody = _parts[parent].Body;
            part.StartLocal = Quaternion.Inverse(parentBody.rotation) * bone.rotation;
            var link = go.AddComponent<ConfigurableJoint>();
            link.connectedBody = parentBody;
            link.anchor = Vector3.zero;
            // WHY these two axes: with the joint's frame lined up with the body's own, the motor's
            // target is simply the wanted rotation relative to the parent, inverted (see Aim).
            link.axis = Vector3.right;
            link.secondaryAxis = Vector3.up;
            link.xMotion = link.yMotion = link.zMotion = ConfigurableJointMotion.Locked;
            link.angularXMotion = link.angularYMotion = link.angularZMotion = ConfigurableJointMotion.Free;
            link.rotationDriveMode = RotationDriveMode.Slerp;
            link.enableCollision = false;
            part.Joint = link;
            part.Frame = Quaternion.identity;
        }

        void LateUpdate() => Tick(Time.deltaTime);

        /// <summary>
        /// Advances the ragdoll by one frame and shows it.
        /// </summary>
        /// <param name="dt">Frame time, seconds.</param>
        /// <remarks>Split out from <c>LateUpdate</c> so the ragdoll can be played back at a fixed step outside Play Mode and looked at frame by frame.</remarks>
        void Tick(float dt)
        {
            if (!_built || dt <= 0f)
                return;

            ReadPose(dt, out Quaternion leftFootPose, out Quaternion rightFootPose);
            // The bodies are built in the T-pose; the first frame puts them on the real pose so the
            // character does not spawn with its arms out and let them fall.
            if (!_posed || (_parts[Hips].Body.position - _parts[Hips].TargetPosition).sqrMagnitude > SnapDistance * SnapDistance)
            {
                PutOnPose();
                _posed = true;
            }
            Aim(_animator.Limp);

            if (Time.time >= _nextLevelCopy)
            {
                _nextLevelCopy = Time.time + RagdollWorld.RefreshTime;
                RagdollWorld.CopyLevelAround(Centre, _solid);
            }

            RagdollWorld.Step(dt);
            Show(leftFootPose, rightFootPose);
        }

        /// <summary>Reads the pose the animator has just written as this frame's targets.</summary>
        void ReadPose(float dt, out Quaternion leftFootPose, out Quaternion rightFootPose)
        {
            for (int i = 0; i < PartCount; i++)
            {
                ref Part part = ref _parts[i];
                part.TargetPosition = part.Bone.position;
                part.TargetRotation = part.Bone.rotation;
            }
            // The chest part is the lowest spine bone. The spine bones above it are not parts:
            // they keep the bend the pose gave them on top of wherever the chest part ends up.
            // WHY not one rigid spine at the top bone's angle, as this first did: a
            // run's shoulders turn some 60 degrees against the hips, spread up the spine; as one
            // rigid block the whole torso swung that far from the waist.
            // The head part hinges at the neck, not at the head bone.
            _parts[Head].TargetPosition = (_neck != null ? _neck : _parts[Head].Bone).position;
            leftFootPose = _leftFoot.rotation;
            rightFootPose = _rightFoot.rotation;

            Vector3 hipsTarget = _parts[Hips].TargetPosition;
            _hipsTargetSpeed = (hipsTarget - _hipsTargetWas) / dt;
            _hipsTargetWas = hipsTarget;

            // How hard the player itself sped up or slowed down this frame. A share of it is
            // applied to every part alike, so that share of a start or stop moves the body as
            // one piece and only the rest has to be dragged through the hips.
            // WHY the visual's movement and not the hips target's: the target also carries the
            // clip's own bob and sway, which the joints are meant to chase, not be handed.
            Vector3 playerNow = transform.position;
            Vector3 playerSpeed = (playerNow - _playerWas) / dt;
            _carryPush = Vector3.ClampMagnitude((playerSpeed - _playerSpeed) / dt, MaxCarryPush) * _profile.StartStopCarry;
            _playerSpeed = playerSpeed;
            _playerWas = playerNow;
        }

        /// <summary>Puts every body on the pose, at rest. Used at spawn and after a teleport.</summary>
        void PutOnPose()
        {
            for (int i = 0; i < PartCount; i++)
            {
                ref Part part = ref _parts[i];
                part.Body.position = part.WasPosition = part.TargetPosition;
                part.Body.rotation = part.WasRotation = part.TargetRotation;
                part.Body.linearVelocity = Vector3.zero;
                part.Body.angularVelocity = Vector3.zero;
            }
            _hipsTargetSpeed = Vector3.zero;
            _playerWas = transform.position;
            _playerSpeed = Vector3.zero;
            _carryPush = Vector3.zero;
        }

        /// <summary>
        /// Points every joint's motor at the pose and sets how hard it pulls.
        /// </summary>
        /// <param name="limp">0 = full strength, 1 = as limp as the profile allows (knocked back or staggering).</param>
        void Aim(float limp)
        {
            float strength = Brace() * Mathf.Lerp(1f, _profile.LimpShare, limp);
            for (int i = 1; i < PartCount; i++)
            {
                ref Part part = ref _parts[i];
                Quaternion wanted = Quaternion.Inverse(_parts[part.Parent].TargetRotation) * part.TargetRotation;
                part.Joint.targetRotation = Quaternion.Inverse(part.Frame) * Quaternion.Inverse(wanted) * part.StartLocal * part.Frame;

                float hz = (i == Chest ? _profile.SpineStrength
                    : i == Head ? _profile.HeadStrength
                    : i <= RightLowerArm ? _profile.ArmStrength
                    : _profile.LegStrength) * strength;
                float omega = 2f * Mathf.PI * hz;
                // WHY "use acceleration": the motor then pulls as hard as it takes to swing whatever
                // is hanging off the joint at the wanted speed, so one strength number means the
                // same thing for a heavy chest and a light forearm.
                part.Joint.slerpDrive = new JointDrive
                {
                    positionSpring = omega * omega,
                    positionDamper = 2f * _profile.JointDamping * omega,
                    maximumForce = float.MaxValue,
                    useAcceleration = true
                };
            }
        }

        /// <summary>
        /// How much harder than normal the joints pull right now: 1 on the floor, rising to the
        /// profile's air brace in a jump.
        /// </summary>
        /// <remarks>
        /// WHY: a jump launches the hips upward at about 8 m/s in a single tick. At floor strength
        /// the heavy top half is left behind, the body pitches into a dive and the arms are flung
        /// straight overhead. Bracing in the air is also what a person does.
        /// </remarks>
        float Brace() => Mathf.Lerp(1f, _profile.AirBrace, _animator.Airborne);

        /// <summary>
        /// Called by <see cref="RagdollWorld"/> before each physics step: gravity and the carry
        /// on every part, and the spring that drags the hips after the player and keeps them upright.
        /// </summary>
        /// <param name="dt">The step, seconds.</param>
        public void BeforeStep(float dt)
        {
            Vector3 fall = Physics.gravity * _profile.RagdollGravity + _carryPush;
            for (int i = 0; i < PartCount; i++)
            {
                ref Part part = ref _parts[i];
                part.WasPosition = part.Body.position;
                part.WasRotation = part.Body.rotation;
                part.Body.AddForce(fall, ForceMode.Acceleration);
            }

            ref Part hips = ref _parts[Hips];
            float limp = _animator.Limp;
            float follow = 2f * Mathf.PI * _profile.HipsFollow;
            // Scaled by the whole body's mass, because the whole body hangs from the hips.
            Vector3 pull = follow * follow * (hips.TargetPosition - hips.Body.position)
                - 2f * _profile.HipsDamping * follow * (hips.Body.linearVelocity - _hipsTargetSpeed);
            hips.Body.AddForce(pull * _totalMass, ForceMode.Force);

            float upright = 2f * Mathf.PI * _profile.UprightStrength * Brace() * Mathf.Lerp(1f, _profile.LimpShare, limp);
            Quaternion off = hips.TargetRotation * Quaternion.Inverse(hips.Body.rotation);
            off.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180f)
                degrees -= 360f;
            if (!float.IsNaN(axis.x) && !float.IsInfinity(axis.x))
            {
                Vector3 turn = upright * upright * (degrees * Mathf.Deg2Rad) * axis
                    - 2f * _profile.HipsDamping * upright * hips.Body.angularVelocity;
                hips.Body.AddTorque(turn, ForceMode.Acceleration);
            }
        }

        /// <summary>Overwrites the visible skeleton with where the bodies are, blended between the last two physics steps.</summary>
        void Show(Quaternion leftFootPose, Quaternion rightFootPose)
        {
            float blend = RagdollWorld.Blend;
            ref Part hips = ref _parts[Hips];
            hips.Bone.SetPositionAndRotation(
                Vector3.Lerp(hips.WasPosition, hips.Body.position, blend),
                Quaternion.Slerp(hips.WasRotation, hips.Body.rotation, blend));

            // Parents before children: every later bone's position comes from the one before.
            for (int i = 1; i < PartCount; i++)
            {
                ref Part part = ref _parts[i];
                // The neck rides rigidly on the chest part; the head part hinges on it.
                if (i == Head && _neck != null)
                    _neck.localRotation = _neckRest;
                part.Bone.rotation = Quaternion.Slerp(part.WasRotation, part.Body.rotation, blend);
            }

            // The feet keep the tip the pose gave them; they are not parts of the ragdoll.
            _leftFoot.rotation = leftFootPose;
            _rightFoot.rotation = rightFootPose;
        }
    }
}
