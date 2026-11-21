using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Player
{
    /// <summary>Client-local active ragdoll (11 rigidbodies in <see cref="RagdollWorld"/>) driven toward <see cref="PlayerAnimator"/>'s pose.</summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class PlayerRagdoll : MonoBehaviour
    {
        // Runs after PlayerAnimator (100) so this frame's pose targets are already written.
        const int ExecutionOrder = 110;

        const int Hips = 0, Chest = 1, Head = 2,
            LeftUpperArm = 3, LeftLowerArm = 4, RightUpperArm = 5, RightLowerArm = 6,
            LeftUpperLeg = 7, LeftLowerLeg = 8, RightUpperLeg = 9, RightLowerLeg = 10, PartCount = 11;

        static readonly float[] Masses = { 10f, 10f, 5f, 2f, 1.5f, 2f, 1.5f, 5f, 3.5f, 5f, 3.5f };
        static readonly float[] Radii = { 0.14f, 0.13f, 0.13f, 0.05f, 0.045f, 0.05f, 0.045f, 0.075f, 0.06f, 0.075f, 0.06f };
        const float HeadHeight = 0.13f, HandLength = 0.09f;
        const float SnapDistance = 2.5f;
        const float MaxCarryPush = 150f;
        const float HipsSteadiness = 25f;

        const float LinearDamping = 0.2f, AngularDamping = 1.5f, MaxAngularSpeed = 25f, MaxDepenetrationSpeed = 3f;
        const int SolverPasses = 14, SolverVelocityPasses = 4;

        const float KneeStraight = 5f, KneeBent = 150f, ElbowStraight = 5f, ElbowBent = 155f, HingeSlack = 20f;
        const float SpineBend = 50f, NeckBend = 55f, HipSwing = 120f, HipTwist = 45f, HipSpread = 55f;
        const float NeckTurn = 120f;

        [Header("References")]
        [Tooltip("Tuning for how strongly the ragdoll is pulled to its pose. Read at runtime, never written.")]
        [SerializeField] AnimationProfile _profile;

        [Tooltip("The animator that writes the pose this ragdoll chases, and says when the body should go limp.")]
        [SerializeField] PlayerAnimator _animator;

        [Tooltip("The character model's Animator. Only used once, at spawn, to find the skeleton's bones through its humanoid avatar.")]
        [SerializeField] Animator _avatar;

        [Tooltip("What the ragdoll bumps into: everything solid in the level, but not players.")]
        [SerializeField] LayerMask _solid = ~0;

        [Tooltip("Tuning for how the body reacts to hits. Optional: left empty, hits do nothing to this body. Read at runtime, never written.")]
        [SerializeField] HitReactionProfile _hitProfile;

        const float MinBlastDistance = 0.01f;
        const float RemoteLodDistance = 25f;
        const float RemoteLodResumeDistance = 22f;
        const float RemoteLodFloorDelta = 9f;
        const float RemoteLodResumeFloorDelta = 8f;

        struct Part
        {
            public Transform Bone;
            public Rigidbody Body;
            public Collider Shape;
            public float Weak;
            public ConfigurableJoint Joint;
            public int Parent;
            public Quaternion StartLocal;
            public Quaternion Frame;
            public Vector3 TargetPosition;
            public Quaternion TargetRotation;
            public Vector3 WasPosition;
            public Quaternion WasRotation;
            public float LastDriveHz;
            public float LastDriveDamping;
        }

        static int s_staggerCounter;
        static PhysicsMaterial s_slippery;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_slippery = null;
        }

        static PhysicsMaterial GetSlipperyMaterial()
        {
            if (s_slippery == null)
            {
                s_slippery = new PhysicsMaterial("Ragdoll")
                {
                    dynamicFriction = 0.2f,
                    staticFriction = 0.2f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }
            return s_slippery;
        }

        readonly Part[] _parts = new Part[PartCount];
        GameObject _root;
        PlayerMotor _motor;
        Transform _neck, _leftFoot, _rightFoot;
        float _totalMass;
        Vector3 _hipsTargetWas;
        Vector3 _hipsTargetSpeed;
        Vector3 _playerWas, _playerSpeed;
        Vector3 _carryPush;
        float _nextLevelCopy;
        bool _built;
        bool _posed;
        bool _lodPaused;
        bool _flinching;
        float _recover;
        PhysicsMaterial _grippy;
        bool _down;
        float _downTime;
        float _hold = 1f;

        /// <summary>World-space centre used by <see cref="RagdollWorld"/> to keep nearby level proxies active.</summary>
        public Vector3 Centre => _hold < 1f ? _parts[Hips].Body.position : _parts[Hips].TargetPosition;

        /// <summary>True once built and while rigidbodies remain valid (false after a Play-Mode script reload).</summary>
        public bool Running => enabled && _built && _parts[Hips].Body != null;

        /// <summary>True when a hit profile is assigned.</summary>
        public bool TakesHits => _hitProfile != null;

        /// <summary>True when hits above <see cref="HitReactionProfile.KnockDownImpulse"/> can floor this body.</summary>
        public bool KnocksDown => _hitProfile != null && _hitProfile.KnockDownImpulse > 0f;

        /// <summary>True while fallen and awaiting recovery (owners should hold the target pose at the body).</summary>
        public bool Down => _down;

        /// <summary>World-space ragdoll hip position.</summary>
        public Vector3 HipsPosition => _built ? _parts[Hips].Body.position : transform.position;

        void Start()
        {
            if (_profile == null || _animator == null || _avatar == null || _avatar.avatar == null || !_avatar.avatar.isHuman)
            {
                Debug.LogError("[PlayerRagdoll] Needs a profile, the player's animator and a humanoid Animator; the character will show the plain pose.", this);
                enabled = false;
                return;
            }

            _motor = GetComponentInParent<PlayerMotor>();
            Build();
        }

        void OnEnable()
        {
            if (!_built || _root == null)
                return;
            if (ShouldPauseLod())
            {
                _lodPaused = true;
                return;
            }
            _lodPaused = false;
            _root.SetActive(true);
            RagdollWorld.Add(this, _root);
            _posed = false;
            _nextLevelCopy = 0f;
        }

        void OnDisable()
        {
            if (!_built)
                return;
            DeactivateRagdoll();
            _lodPaused = false;
        }

        void OnDestroy()
        {
            if (!_built)
                return;
            RagdollWorld.Remove(this);
            if (_root != null)
                Destroy(_root);
            if (_grippy != null)
                Destroy(_grippy);
        }

        void DeactivateRagdoll()
        {
            if (_root != null)
            {
                _root.SetActive(false);
                if (gameObject.scene.IsValid() && gameObject.scene.isLoaded && _root.scene != gameObject.scene)
                    SceneManager.MoveGameObjectToScene(_root, gameObject.scene);
            }
            RagdollWorld.Remove(this);
        }

        bool ShouldPauseLod()
        {
            if (_motor == null || _motor == PlayerMotor.Local)
                return false;

            Vector3 viewerPos;
            if (PlayerCamera.Instance != null)
                viewerPos = PlayerCamera.Instance.transform.position;
            else if (PlayerMotor.Local != null)
                viewerPos = PlayerMotor.Local.transform.position;
            else
                return false;

            Vector3 delta = transform.position - viewerPos;
            float maxDist = _lodPaused ? RemoteLodResumeDistance : RemoteLodDistance;
            float maxVertical = _lodPaused ? RemoteLodResumeFloorDelta : RemoteLodFloorDelta;
            return delta.sqrMagnitude > maxDist * maxDist || Mathf.Abs(delta.y) > maxVertical;
        }

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


            _root = new GameObject("Ragdoll");
            PhysicsMaterial slippery = GetSlipperyMaterial();

            Vector3 up = _avatar.transform.up;
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

            Vector3 right = _avatar.transform.right, forward = _avatar.transform.forward;
            Hinge(LeftLowerLeg, right, up, KneeStraight, KneeBent, false);
            Hinge(RightLowerLeg, right, up, KneeStraight, KneeBent, false);
            Hinge(LeftLowerArm, up, forward, ElbowStraight, ElbowBent, false);
            Hinge(RightLowerArm, up, forward, ElbowStraight, ElbowBent, true);
            Ball(Chest, SpineBend, SpineBend, SpineBend);
            Ball(Head, NeckBend, NeckTurn, NeckBend);
            Ball(LeftUpperLeg, HipSwing, HipTwist, HipSpread);
            Ball(RightUpperLeg, HipSwing, HipTwist, HipSpread);

            // Disable self-collision so arms do not snag on the torso while running.
            Collider[] colliders = _root.GetComponentsInChildren<Collider>();
            for (int a = 0; a < colliders.Length; a++)
            {
                for (int b = a + 1; b < colliders.Length; b++)
                    Physics.IgnoreCollision(colliders[a], colliders[b]);
            }

            _parts[Hips].Body.inertiaTensor *= HipsSteadiness;
            _hipsTargetWas = _parts[Hips].Body.position;
            _playerWas = transform.position;
            _built = true;
            if (ShouldPauseLod())
            {
                _lodPaused = true;
                _root.SetActive(false);
            }
            else
            {
                RagdollWorld.Add(this, _root);
            }
        }

        void Hinge(int index, Vector3 axis, Vector3 across, float straight, float bent, bool bendsBackward)
        {
            ref Part part = ref _parts[index];
            ConfigurableJoint link = part.Joint;
            Quaternion toLocal = Quaternion.Inverse(part.Body.rotation);
            link.axis = toLocal * axis;
            link.secondaryAxis = toLocal * across;
            part.Frame = FrameOf(link);
            link.angularXMotion = link.angularYMotion = link.angularZMotion = ConfigurableJointMotion.Limited;
            // ConfigurableJoint measures X angles opposite to a body-axis rotation, so bend/straight swap signs.
            link.lowAngularXLimit = new SoftJointLimit { limit = bendsBackward ? -straight : -bent };
            link.highAngularXLimit = new SoftJointLimit { limit = bendsBackward ? bent : straight };
            link.angularYLimit = link.angularZLimit = new SoftJointLimit { limit = HingeSlack };
        }

        void Ball(int index, float x, float y, float z)
        {
            ConfigurableJoint link = _parts[index].Joint;
            link.angularXMotion = link.angularYMotion = link.angularZMotion = ConfigurableJointMotion.Limited;
            link.lowAngularXLimit = new SoftJointLimit { limit = -x };
            link.highAngularXLimit = new SoftJointLimit { limit = x };
            link.angularYLimit = new SoftJointLimit { limit = y };
            link.angularZLimit = new SoftJointLimit { limit = z };
        }

        static Quaternion FrameOf(ConfigurableJoint link)
        {
            Vector3 x = link.axis.normalized;
            Vector3 z = Vector3.Cross(x, link.secondaryAxis).normalized;
            Vector3 y = Vector3.Cross(z, x).normalized;
            return Quaternion.LookRotation(z, y);
        }

        static Vector3 Past(Vector3 from, Vector3 to) => to + (to - from).normalized * HandLength;

        void Make(int index, Transform bone, int parent, Vector3 joint, Vector3 end, PhysicsMaterial material)
        {
            var go = new GameObject(bone.name);
            go.transform.SetParent(_root.transform, false);
            go.transform.SetPositionAndRotation(joint, bone.rotation);

            var shape = new GameObject("Shape");
            shape.transform.SetParent(go.transform, false);
            float length = Vector3.Distance(joint, end);
            float radius = Radii[index];
            Collider made;
            if (length > radius * 2f)
            {
                // A capsule's long axis is its own Y; the child object is turned to lay it along the bone.
                shape.transform.SetPositionAndRotation((joint + end) * 0.5f, Quaternion.FromToRotation(Vector3.up, end - joint));
                var capsule = shape.AddComponent<CapsuleCollider>();
                capsule.radius = radius;
                capsule.height = length + radius * 2f;
                capsule.material = material;
                made = capsule;
            }
            else
            {
                shape.transform.position = (joint + end) * 0.5f;
                var ball = shape.AddComponent<SphereCollider>();
                ball.radius = radius;
                ball.material = material;
                made = ball;
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
            part.Shape = made;
            part.Parent = parent;
            part.TargetPosition = part.WasPosition = joint;
            part.TargetRotation = part.WasRotation = bone.rotation;
            part.LastDriveHz = -1f;
            part.LastDriveDamping = -1f;
            if (parent < 0)
                return;

            Rigidbody parentBody = _parts[parent].Body;
            part.StartLocal = Quaternion.Inverse(parentBody.rotation) * bone.rotation;
            var link = go.AddComponent<ConfigurableJoint>();
            link.connectedBody = parentBody;
            link.anchor = Vector3.zero;
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

        void Tick(float dt)
        {
            if (!_built || dt <= 0f)
                return;
            if (!Running)
            {
                // Play-Mode script reload drops rigidbodies without re-running Start().
                Debug.LogError("[PlayerRagdoll] Lost its body because the code was reloaded during Play; this character stays in its plain pose until Play is restarted. "
                    + "To stop it happening, set Edit > Preferences > General > Script Changes While Playing to Recompile After Finished Playing.", this);
                RagdollWorld.Remove(this);
                enabled = false;
                return;
            }

            if (ShouldPauseLod())
            {
                if (!_lodPaused)
                {
                    _lodPaused = true;
                    if (_down)
                        EndKnockDownState();
                    _hold = 1f;
                    _flinching = false;
                    DeactivateRagdoll();
                }
                return;
            }

            if (_lodPaused)
            {
                _lodPaused = false;
                if (_root != null)
                {
                    _root.SetActive(true);
                    RagdollWorld.Add(this, _root);
                }
                _posed = false;
                _nextLevelCopy = 0f;
            }

            ReadPose(dt, out Quaternion leftFootPose, out Quaternion rightFootPose);
            if (!_posed || (_hold >= 1f && (_parts[Hips].Body.position - _parts[Hips].TargetPosition).sqrMagnitude > SnapDistance * SnapDistance))
            {
                PutOnPose();
                _posed = true;
            }
            if (_flinching)
                RecoverFromHits(dt);
            if (_hold < 1f)
                UpdateKnockDown(dt);
            Aim(_animator.Limp);

            // Refresh proxies every frame while knocked down so thrown limbs do not outrun level copies.
            if (_hold < 1f || Time.time >= _nextLevelCopy)
            {
                _nextLevelCopy = _nextLevelCopy <= 0f
                    ? Time.time + RagdollWorld.RefreshTime + (s_staggerCounter++ & 7) * 0.025f
                    : Time.time + RagdollWorld.RefreshTime;
                RagdollWorld.CopyLevelAround(Centre, _solid);
            }

            RagdollWorld.Step(dt);
            Show(leftFootPose, rightFootPose);
        }

        void ReadPose(float dt, out Quaternion leftFootPose, out Quaternion rightFootPose)
        {
            for (int i = 0; i < PartCount; i++)
            {
                ref Part part = ref _parts[i];
                part.TargetPosition = part.Bone.position;
                part.TargetRotation = part.Bone.rotation;
            }
            // Neck and upper spine stay out of the rigidbody chain so they preserve clip bend above the chest part.
            _parts[Head].TargetPosition = (_neck != null ? _neck : _parts[Head].Bone).position;
            leftFootPose = _leftFoot.rotation;
            rightFootPose = _rightFoot.rotation;

            Vector3 hipsTarget = _parts[Hips].TargetPosition;
            _hipsTargetSpeed = (hipsTarget - _hipsTargetWas) / dt;
            _hipsTargetWas = hipsTarget;

            Vector3 playerNow = transform.position;
            Vector3 playerSpeed = (playerNow - _playerWas) / dt;
            _carryPush = Vector3.ClampMagnitude((playerSpeed - _playerSpeed) / dt, MaxCarryPush) * _profile.StartStopCarry;
            _playerSpeed = playerSpeed;
            _playerWas = playerNow;
        }

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
            _hipsTargetWas = _parts[Hips].TargetPosition;
            _hipsTargetSpeed = Vector3.zero;
            _playerWas = transform.position;
            _playerSpeed = Vector3.zero;
            _carryPush = Vector3.zero;
        }

        void Aim(float limp)
        {
            float strength = Brace() * Mathf.Lerp(1f, _profile.LimpShare, limp);
            if (_hold < 1f)
                strength *= Mathf.Lerp(_hitProfile.DownLimpShare, 1f, Mathf.SmoothStep(0f, 1f, _hold));
            float damping = _profile.JointDamping;
            for (int i = 1; i < PartCount; i++)
            {
                ref Part part = ref _parts[i];
                Quaternion wanted = Quaternion.Inverse(_parts[part.Parent].TargetRotation) * part.TargetRotation;
                part.Joint.targetRotation = Quaternion.Inverse(part.Frame) * Quaternion.Inverse(wanted) * part.StartLocal * part.Frame;

                float hz = (i == Chest ? _profile.SpineStrength
                    : i == Head ? _profile.HeadStrength
                    : i <= RightLowerArm ? _profile.ArmStrength
                    : _profile.LegStrength) * strength;
                if (part.Weak > 0f)
                    hz *= Mathf.Lerp(1f, _hitProfile.FlinchLimpShare, part.Weak);

                if (Mathf.Abs(part.LastDriveHz - hz) < 1e-4f && Mathf.Abs(part.LastDriveDamping - damping) < 1e-4f)
                    continue;

                part.LastDriveHz = hz;
                part.LastDriveDamping = damping;
                float omega = 2f * Mathf.PI * hz;
                part.Joint.slerpDrive = new JointDrive
                {
                    positionSpring = omega * omega,
                    positionDamper = 2f * damping * omega,
                    maximumForce = float.MaxValue,
                    useAcceleration = true
                };
            }
        }

        /// <summary>Applies a visual-only hit impulse and joint slackening to the ragdoll in <see cref="RagdollWorld"/>.</summary>
        public void Hit(in HitInfo hit)
        {
            if (!Running || _lodPaused || !_posed || _hitProfile == null || hit.Impulse <= 0f)
                return;

            if (!_down && _hitProfile.KnockDownImpulse > 0f && hit.Impulse >= _hitProfile.KnockDownImpulse
                && (hit.Type != HitType.Bullet || _hitProfile.BulletsKnockDown))
                KnockDown();

            float impulse = hit.Impulse * _hitProfile.ImpulseShare;
            float weak = Mathf.Clamp01(hit.Impulse / _hitProfile.FlinchFullImpulse);
            Vector3 along = hit.Direction.normalized;
            // Read before any kick, which marks the body as flinching. A shot landing during a
            // melee hit's recovery does not cut that recovery short.
            float recover = hit.Type == HitType.Melee ? _hitProfile.MeleeRecover : _hitProfile.FlinchRecover;
            _recover = _flinching ? Mathf.Max(_recover, recover) : recover;

            if (hit.Radius > 0f)
            {
                // A blast: the impulse is shared out by weight and by how near each part is, so
                // the near side of the body is thrown harder and the body is turned as well as
                // pushed.
                float shares = 0f;
                for (int i = 0; i < PartCount; i++)
                    shares += BlastShare(i, hit);
                if (shares <= 0f)
                    return;
                for (int i = 0; i < PartCount; i++)
                {
                    float share = BlastShare(i, hit);
                    if (share <= 0f)
                        continue;
                    Vector3 centre = _parts[i].Body.worldCenterOfMass;
                    Vector3 away = centre - hit.Point;
                    float distance = away.magnitude;
                    Kick(i, (distance > MinBlastDistance ? away / distance : along) * (impulse * share / shares), centre, weak);
                }
                return;
            }

            if (hit.Type == HitType.Melee && _hitProfile.MeleeBodyShare > 0f)
            {
                // A swing shoves as much as it strikes: part of its push is shared over the
                // whole upper body by weight, so chest, head and arms are thrown together and the
                // body folds at the waist, where a shot only jerks the part it struck.
                float shove = impulse * _hitProfile.MeleeBodyShare;
                impulse -= shove;
                float upperMass = 0f;
                for (int i = Chest; i <= RightLowerArm; i++)
                    upperMass += _parts[i].Body.mass;
                for (int i = Chest; i <= RightLowerArm; i++)
                    Kick(i, along * (shove * _parts[i].Body.mass / upperMass), _parts[i].Body.worldCenterOfMass, weak);
            }

            int struck = NearestPart(hit.Point);
            Kick(struck, along * impulse, hit.Point, weak);
            for (int i = 0; i < PartCount; i++)
            {
                if (i != struck && (_parts[i].Parent == struck || _parts[struck].Parent == i))
                    Kick(i, along * (impulse * _hitProfile.NeighbourShare), _parts[i].Body.worldCenterOfMass, weak * _hitProfile.NeighbourShare);
            }
        }

        float BlastShare(int index, in HitInfo hit)
        {
            Rigidbody body = _parts[index].Body;
            float distance = Vector3.Distance(body.worldCenterOfMass, hit.Point);
            return body.mass * (1f - Mathf.Clamp01(distance / hit.Radius));
        }

        int NearestPart(Vector3 point)
        {
            int nearest = Hips;
            float best = float.MaxValue;
            for (int i = 0; i < PartCount; i++)
            {
                float distance = (_parts[i].Shape.ClosestPoint(point) - point).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }
            return nearest;
        }

        void Kick(int index, Vector3 impulse, Vector3 at, float weak)
        {
            ref Part part = ref _parts[index];
            part.Body.AddForceAtPosition(Vector3.ClampMagnitude(impulse, part.Body.mass * _hitProfile.MaxKickSpeed), at, ForceMode.Impulse);
            part.Weak = Mathf.Max(part.Weak, weak);
            _flinching = true;
        }

        void KnockDown()
        {
            _down = true;
            _downTime = 0f;
            _hold = 0f;
            if (_grippy == null)
            {
                _grippy = new PhysicsMaterial("Ragdoll (down)")
                {
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Maximum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }
            _grippy.dynamicFriction = _grippy.staticFriction = _hitProfile.DownFriction;
            for (int i = 0; i < PartCount; i++)
            {
                // Continuous collision while down prevents fast limbs tunnelling through 0.2 m floor slabs.
                _parts[i].Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _parts[i].Shape.sharedMaterial = _grippy;
            }
        }

        void EndKnockDownState()
        {
            _down = false;
            PhysicsMaterial slippery = GetSlipperyMaterial();
            for (int i = 0; i < PartCount; i++)
            {
                _parts[i].Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                _parts[i].Shape.sharedMaterial = slippery;
            }
        }

        void UpdateKnockDown(float dt)
        {
            if (_down)
            {
                _downTime += dt;
                float settle = _hitProfile.SettleSpeed;
                bool atRest = _parts[Hips].Body.linearVelocity.sqrMagnitude < settle * settle;
                if (_downTime < _hitProfile.DownMaxTime && (_downTime < _hitProfile.DownMinTime || !atRest))
                    return;
                EndKnockDownState();
            }
            _hold = Mathf.MoveTowards(_hold, 1f, dt / Mathf.Max(_hitProfile.GetUpTime, HitReactionProfile.MinFlinchRecover));
        }

        void RecoverFromHits(float dt)
        {
            float fade = dt / Mathf.Max(_recover, HitReactionProfile.MinFlinchRecover);
            _flinching = false;
            for (int i = 0; i < PartCount; i++)
            {
                ref Part part = ref _parts[i];
                part.Weak = Mathf.MoveTowards(part.Weak, 0f, fade);
                _flinching |= part.Weak > 0f;
            }
        }

        float Brace() => Mathf.Lerp(1f, _profile.AirBrace, _animator.Airborne);

        /// <summary>Called by <see cref="RagdollWorld"/> before each 120 Hz physics step to apply gravity, carry, and hip spring forces.</summary>
        public void BeforeStep(float dt)
        {
            Vector3 fall = Physics.gravity * _profile.RagdollGravity + (_hold < 1f ? Vector3.zero : _carryPush);
            float held = _hold < 1f ? Mathf.SmoothStep(0f, 1f, _hold) : 1f;
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
            Vector3 pull = follow * follow * (hips.TargetPosition - hips.Body.position)
                - 2f * _profile.HipsDamping * follow * (hips.Body.linearVelocity - _hipsTargetSpeed);
            hips.Body.AddForce(pull * (_totalMass * held), ForceMode.Force);

            float upright = 2f * Mathf.PI * _profile.UprightStrength * Brace() * Mathf.Lerp(1f, _profile.LimpShare, limp);
            Quaternion off = hips.TargetRotation * Quaternion.Inverse(hips.Body.rotation);
            off.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180f)
                degrees -= 360f;
            if (!float.IsNaN(axis.x) && !float.IsInfinity(axis.x))
            {
                Vector3 turn = upright * upright * (degrees * Mathf.Deg2Rad) * axis
                    - 2f * _profile.HipsDamping * upright * hips.Body.angularVelocity;
                hips.Body.AddTorque(turn * held, ForceMode.Acceleration);
            }
        }

        void Show(Quaternion leftFootPose, Quaternion rightFootPose)
        {
            float blend = RagdollWorld.Blend;
            ref Part hips = ref _parts[Hips];
            hips.Bone.SetPositionAndRotation(
                Vector3.Lerp(hips.WasPosition, hips.Body.position, blend),
                Quaternion.Slerp(hips.WasRotation, hips.Body.rotation, blend));

            // Parents before children; skip neck/feet so pose neck angle and foot pitch stay intact.
            for (int i = 1; i < PartCount; i++)
            {
                ref Part part = ref _parts[i];
                part.Bone.rotation = Quaternion.Slerp(part.WasRotation, part.Body.rotation, blend);
            }

            _leftFoot.rotation = leftFootPose;
            _rightFoot.rotation = rightFootPose;
        }
    }
}
