using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CloseTheDeal.Player
{
    /// <summary>Poses the character skeleton each frame from measured movement and motor state (blended clips or procedural fallback) for <see cref="PlayerRagdoll"/>.</summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        // Runs after default-order scripts (lean pivot) and before PlayerRagdoll (110).
        const int ExecutionOrder = 100;
        const float MaxLegExtension = 0.999f;
        const float MaxSaneSpeed = 60f;
        const float DirectionMinSpeed = 0.1f;
        const float MaxSpringStep = 1f / 30f;
        const float HipsLeanShare = 0.25f, SpineShare = 0.3f, ChestShare = 0.6f;
        const float HeadSpringRatio = 0.6f;
        const float HipsRockShare = 0.5f, HeadRockShare = 0.4f;
        const float MaxTurnTwist = 25f, MaxBank = 14f, MaxLean = 60f, MaxCarry = 0.2f, MaxStopCrouch = 0.16f;
        const float ArmSwingSetBack = 10f;
        const float StartLead = 0.7f;
        const float LookPitchShare = 0.4f;
        const float LeftArmNoiseRow = 5.3f, RightArmNoiseRow = 9.1f;
        const float HeadCounterNod = 0.6f;
        const float SlowestCadence = 0.65f;
        const float RunTrackWidth = 0.85f;
        const float KickTiming = 0.65f;
        const float RollStart = 0.5f;
        const float TrailingTuck = 0.35f, TrailingSplit = 0.9f, RisingTuck = 0.4f, AirToePoint = 40f;
        const float RisingArmRaise = 0.7f;
        const float RisingSpeed = 3f, FallingSpeed = -3f;
        const float LandDamping = 0.75f;
        const float MaxRebound = 0.03f;
        const float KnockedTrail = 1.5f, KnockedArmRaise = 0.7f;
        const float MinClipWeight = 0.001f;
        const int IdleInput = 0, RunInput = 1, SprintInput = RunInput + AnimationProfile.RunDirections,
            AirInput = SprintInput + 1, LandInput = AirInput + 1, FidgetInput = LandInput + 1,
            CrouchIdleInput = FidgetInput + 1, CrouchRunInput = CrouchIdleInput + 1,
            SlideInput = CrouchRunInput + AnimationProfile.RunDirections, InputCount = SlideInput + 1;
        const float FidgetBlendTime = 0.2f;
        const float FidgetStillness = StandingStride;
        const int ForwardDirection = 0, ForwardRightDirection = 1, SideDirection = 2, ForwardLeftDirection = 7;
        const float RunDirectionStep = 360f / AnimationProfile.RunDirections;
        const float StandingStride = 0.02f;
        const float RunDirectionBlendTime = 0.12f;
        const float RunDirectionSlack = 10f;
        const float StrideHoldTime = 0.1f;
        const float LookChestShare = 0.35f;
        const float BodyViewHeadTurnLimit = 15f;
        const float StandingToLook = 0.1f;
        const float BreathSpineShare = 0.5f, BreathShoulderShare = 1f;
        const float LandClipRate = 1.25f;
        const float LandFadeStart = 0.45f;
        const float RunningLandShare = 0.6f;
        const float MaxStrafeAimTwist = 80f;
        const float StrafeAimBlendTime = 0.1f;
        const float AirVelocityRef = 6f;
        const float AirArmForwardTuck = 12f;
        const float AirElbowTuck = 26f;
        const float AirFallArmLift = 10f;
        const float AirRiseSpinePitch = 7f;
        const float AirFallSpinePitch = -4f;
        const float AirRiseThighLift = 14f;
        const float AirFallThighReach = 6f;
        const float AirRiseKneeBend = 20f;
        const float AirFallKneeExtend = 10f;
        const float AirTrailLegShare = 0.65f;

        [Header("References")]
        [Tooltip("The clips, and the tuning for the ragdoll and the procedural pose. Read at runtime, never written.")]
        [SerializeField] AnimationProfile _profile;

        [Tooltip("The player's motor: says whether the player is on the floor, knocked back or climbing, and how fast walking and sprinting are. Leave empty for a body nobody is playing (a test dummy): it then always counts as standing on the floor, never knocked back, and never strides.")]
        [SerializeField] PlayerMotor _motor;

        bool _hasMotor;

        [Tooltip("The character model's Animator. Its humanoid avatar is used once, at spawn, to find the skeleton's bones; the clips are then played onto it. It needs no controller.")]
        [SerializeField] Animator _avatar;

        struct Bone
        {
            public Transform T;
            public Quaternion Rest;
        }

        struct Spring
        {
            public Vector3 Value, Rate;

            public void Step(Vector3 target, float hz, float damping, float dt)
            {
                float omega = 2f * Mathf.PI * hz;
                Rate += (omega * omega * (target - Value) - 2f * damping * omega * Rate) * dt;
                Value += Rate * dt;
            }
        }

        Transform _model;
        Bone _hips, _spine, _chest, _upperChest, _neck, _head;

        /// <summary>Cached head bone (available after Start; the Animator avatar is cleared on first frame).</summary>
        public Transform HeadBone => _head.T;
        Bone _leftShoulder, _rightShoulder;
        Bone _leftUpperArm, _leftLowerArm, _rightUpperArm, _rightLowerArm;
        Bone _leftUpperLeg, _leftLowerLeg, _leftFoot, _rightUpperLeg, _rightLowerLeg, _rightFoot;

        Vector3 _hipsRest;
        Vector3 _leftFootRest, _rightFootRest;

        Vector3 _lastPosition;
        float _lastYaw;
        float _groundSpeed, _lastGroundSpeed;
        Vector3 _velocity;
        Vector3 _local;
        float _yawRate;
        Spring _torso, _headSpring;
        Spring _turn;
        Spring _land;
        Vector3 _travel = Vector3.forward;
        float _phase;
        float _moveWeight;
        float _moveWeightRate;
        float _airWeight;
        float _airWeightRate;
        float _knocked;
        float _knockedRate;
        float _stagger;
        float _fallSpeed;
        bool _wasOnFloor = true;
        float _time;

        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        readonly AnimationClipPlayable[] _clips = new AnimationClipPlayable[InputCount];
        readonly float[] _clipLengths = new float[InputCount];
        readonly float[] _clipWeights = new float[InputCount];
        bool _usesClips;
        float _landTime, _landStrength;
        AnimationClipPlayable[] _fidgets;
        int _fidget = -1;
        float _fidgetTime, _fidgetWeight, _fidgetWait;
        readonly float[] _runShares = { 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
        bool _usesCrouch;
        float _crouchWeight, _crouchWeightRate, _slideWeight, _slideWeightRate;
        float _bodyBehind;
        float _lookYaw;
        bool _bodyTurning;
        bool _lookStarted;
        int _runDirection;
        float _heldStride, _strideDip;
        float _strafeAimWeight;
        Transform[] _skeleton;
        Vector3[] _skeletonRestPositions;
        Quaternion[] _skeletonRestRotations;

        /// <summary>0..1 limpness read by <see cref="PlayerRagdoll"/> when knocked back or staggered.</summary>
        public float Limp => Mathf.Max(_knocked, _stagger);

        /// <summary>0..1 airborne blend read by <see cref="PlayerRagdoll"/> to brace joints in flight.</summary>
        public float Airborne => _airWeight;

        // Start (not Awake) guarantees Animator has initialized its humanoid avatar.
        void Start()
        {
            if (_profile == null || _avatar == null || _avatar.avatar == null || !_avatar.avatar.isHuman)
            {
                Debug.LogError("[PlayerAnimator] Needs a profile and a humanoid Animator; the character will stay in its rest pose.", this);
                enabled = false;
                return;
            }
            _hasMotor = _motor != null;

            _model = _avatar.transform;
            _hips = Find(HumanBodyBones.Hips);
            _spine = Find(HumanBodyBones.Spine);
            _chest = Find(HumanBodyBones.Chest);
            _upperChest = Find(HumanBodyBones.UpperChest);
            _leftShoulder = Find(HumanBodyBones.LeftShoulder);
            _rightShoulder = Find(HumanBodyBones.RightShoulder);
            _neck = Find(HumanBodyBones.Neck);
            _head = Find(HumanBodyBones.Head);
            _leftUpperArm = Find(HumanBodyBones.LeftUpperArm);
            _leftLowerArm = Find(HumanBodyBones.LeftLowerArm);
            _rightUpperArm = Find(HumanBodyBones.RightUpperArm);
            _rightLowerArm = Find(HumanBodyBones.RightLowerArm);
            _leftUpperLeg = Find(HumanBodyBones.LeftUpperLeg);
            _leftLowerLeg = Find(HumanBodyBones.LeftLowerLeg);
            _leftFoot = Find(HumanBodyBones.LeftFoot);
            _rightUpperLeg = Find(HumanBodyBones.RightUpperLeg);
            _rightLowerLeg = Find(HumanBodyBones.RightLowerLeg);
            _rightFoot = Find(HumanBodyBones.RightFoot);

            _hipsRest = _model.InverseTransformPoint(_hips.T.position);
            _leftFootRest = _model.InverseTransformPoint(_leftFoot.T.position);
            _rightFootRest = _model.InverseTransformPoint(_rightFoot.T.position);
            _lastPosition = transform.position;
            _lastYaw = _model.eulerAngles.y;

            // Built on the first frame so PlayerRagdoll.Start can resolve its bones before we clear the avatar.
            _usesClips = _profile.HasClips;
            _usesCrouch = _usesClips && _hasMotor && _profile.HasCrouchClips;
            _ready = true;
        }

        void OnDestroy()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }

        void BuildClipGraph()
        {
            var bones = new List<Transform>((int)HumanBodyBones.LastBone);
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform bone = _avatar.GetBoneTransform((HumanBodyBones)i);
                if (bone != null && !bones.Contains(bone))
                    bones.Add(bone);
            }
            _skeleton = bones.ToArray();
            _skeletonRestPositions = new Vector3[_skeleton.Length];
            _skeletonRestRotations = new Quaternion[_skeleton.Length];
            for (int i = 0; i < _skeleton.Length; i++)
                _skeleton[i].GetLocalPositionAndRotation(out _skeletonRestPositions[i], out _skeletonRestRotations[i]);

            // Clear humanoid avatar so generic clips write exact bone transforms without root-offset distortion.
            _avatar.avatar = null;
            for (int i = 0; i < InputCount; i++)
                _clipWeights[i] = -1f;
            _avatar.applyRootMotion = false;
            // AlwaysAnimate ensures the owner's shadow-only body keeps updating bones for the ragdoll.
            _avatar.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _graph = PlayableGraph.Create(nameof(PlayerAnimator));
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _mixer = AnimationMixerPlayable.Create(_graph, InputCount);
            AddClip(IdleInput, _profile.IdleClip);
            for (int i = 0; i < AnimationProfile.RunDirections; i++)
                AddClip(RunInput + i, _profile.RunClips[i]);
            AddClip(SprintInput, _profile.SprintClip);
            AddClip(AirInput, _profile.AirClip);
            AddClip(LandInput, _profile.LandClip);
            if (_usesCrouch)
            {
                AddClip(CrouchIdleInput, _profile.CrouchIdleClip);
                for (int i = 0; i < AnimationProfile.RunDirections; i++)
                    AddClip(CrouchRunInput + i, _profile.CrouchClips[i]);
                AddClip(SlideInput, _profile.SlideClip);
            }

            AnimationClip[] fidgets = _profile.FidgetClips;
            _fidgets = new AnimationClipPlayable[fidgets != null ? fidgets.Length : 0];
            for (int i = 0; i < _fidgets.Length; i++)
            {
                if (fidgets[i] == null)
                    continue;
                _fidgets[i] = AnimationClipPlayable.Create(_graph, fidgets[i]);
                _fidgets[i].SetSpeed(0d);
            }
            _fidgetWait = Random.Range(_profile.FidgetMinWait, _profile.FidgetMaxWait);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Pose", _avatar);
            output.SetSourcePlayable(_mixer);
        }

        void AddClip(int input, AnimationClip clip)
        {
            AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetSpeed(0d);
            _graph.Connect(playable, 0, _mixer, input);
            _clips[input] = playable;
            _clipLengths[input] = clip.length;
        }

        Bone Find(HumanBodyBones id)
        {
            Transform bone = _avatar.GetBoneTransform(id);
            return new Bone
            {
                T = bone,
                Rest = bone != null ? Quaternion.Inverse(_model.rotation) * bone.rotation : Quaternion.identity
            };
        }

        // [NonSerialized] detects Play-Mode script reloads where Start() does not re-run.
        [System.NonSerialized] bool _ready;

        void LateUpdate()
        {
            if (_ready)
            {
                Step(Time.deltaTime);
                return;
            }

            Debug.LogError("[PlayerAnimator] Lost its bones because the code was reloaded during Play. The character holds its last pose; stop and start Play to get it back.", this);
            enabled = false;
        }

        void Step(float dt)
        {
            if (dt <= 0f)
                return;

            // Before anything reads a clip's length.
            if (_usesClips && !_graph.IsValid())
                BuildClipGraph();

            MeasureMovement(dt, out float speed, out float sprint);
            bool onFloor = !_hasMotor || (_motor.Grounded && _motor.State != MoveState.Mantling);
            UpdateWeights(dt, speed, sprint, onFloor);
            if (_usesClips)
                PoseFromClips(sprint, dt);
            else
                Pose(sprint);
        }

        void MeasureMovement(float dt, out float speed, out float sprint)
        {
            Vector3 position = transform.position;
            Vector3 measured = (position - _lastPosition) / dt;
            _lastPosition = position;
            float smoothing = 1f - Mathf.Exp(-dt / _profile.VelocitySmoothing);
            if (measured.sqrMagnitude < MaxSaneSpeed * MaxSaneSpeed)
            {
                _lastGroundSpeed = _groundSpeed;
                _groundSpeed = new Vector2(measured.x, measured.z).magnitude;
                _velocity = Vector3.Lerp(_velocity, measured, smoothing);
            }

            if (!_usesClips)
            {
                float yaw = _model.eulerAngles.y;
                _yawRate = Mathf.Lerp(_yawRate, Mathf.DeltaAngle(_lastYaw, yaw) / dt, smoothing);
                _lastYaw = yaw;
            }

            _local = _model.InverseTransformDirection(_velocity);
            Vector3 planar = new Vector3(_local.x, 0f, _local.z);
            speed = planar.magnitude;
            if (speed > DirectionMinSpeed)
                _travel = planar / speed;

            if (!_usesClips)
            {
                float step = Mathf.Min(dt, MaxSpringStep);
                _torso.Step(planar, _profile.WobbleSpeed, _profile.WobbleDamping, step);
                _headSpring.Step(planar, _profile.WobbleSpeed * HeadSpringRatio, _profile.WobbleDamping, step);
                _turn.Step(new Vector3(_yawRate, 0f, 0f), _profile.WobbleSpeed, _profile.WobbleDamping, step);
                _land.Step(Vector3.zero, _profile.LandBounce, LandDamping, step);
            }

            if (!_hasMotor)
            {
                sprint = 0f;
                return;
            }
            MovementProfile movement = _motor.Profile;
            sprint = Mathf.InverseLerp(movement.WalkSpeed, movement.SprintSpeed, speed);
        }

        void UpdateWeights(float dt, float speed, float sprint, bool onFloor)
        {
            float ease = _profile.PoseBlendTime;
            if (_usesCrouch)
            {
                PlayerAnimState anim = _motor.AnimState;
                _crouchWeight = Mathf.SmoothDamp(_crouchWeight, anim.Crouch, ref _crouchWeightRate, ease, Mathf.Infinity, dt);
                _slideWeight = Mathf.SmoothDamp(_slideWeight, anim.Sliding ? 1f : 0f, ref _slideWeightRate,
                    anim.Sliding ? _profile.SlideBlendIn : _profile.SlideBlendOut, Mathf.Infinity, dt);
            }

            float walkSpeed = _hasMotor ? Mathf.Lerp(_motor.Profile.WalkSpeed, _motor.Profile.CrouchSpeed, _crouchWeight) : 0f;
            float stride = walkSpeed > 0f ? Mathf.Clamp01(speed / walkSpeed) : 0f;
            if (stride >= _heldStride || !_usesClips)
            {
                _heldStride = stride;
                _strideDip = 0f;
            }
            else
            {
                _strideDip += dt;
                if (_strideDip > StrideHoldTime)
                    _heldStride = stride;
            }
            float moveEase = _heldStride < _moveWeight ? _profile.StopBlendTime : ease;
            _moveWeight = Mathf.SmoothDamp(_moveWeight, _heldStride, ref _moveWeightRate, moveEase, Mathf.Infinity, dt);
            _airWeight = Mathf.SmoothDamp(_airWeight, onFloor ? 0f : 1f, ref _airWeightRate, ease, Mathf.Infinity, dt);
            _knocked = Mathf.SmoothDamp(_knocked, _hasMotor && _motor.State == MoveState.Knocked ? 1f : 0f, ref _knockedRate, ease, Mathf.Infinity, dt);
            _stagger = Mathf.MoveTowards(_stagger, 0f, dt / _profile.StaggerRecover);

            if (_usesClips)
            {
                float ground = Mathf.Lerp(
                    _profile.RunClipSpeed * _clipLengths[RunInput],
                    _profile.SprintClipSpeed * _clipLengths[SprintInput],
                    sprint * Mathf.Clamp01(_travel.z));
                float forward = _runShares[ForwardDirection] + _runShares[ForwardRightDirection] + _runShares[ForwardLeftDirection];
                ground = Mathf.Lerp(_profile.SideClipSpeed * _clipLengths[RunInput + SideDirection], ground, forward);
                if (_usesCrouch)
                    ground = Mathf.Lerp(ground, _profile.CrouchClipSpeed * _clipLengths[CrouchRunInput], _crouchWeight);
                float matched = Mathf.Max(speed, SlowestCadence * walkSpeed * _moveWeight);
                _phase = _moveWeight < StandingStride && stride < StandingStride
                    ? 0f
                    : Mathf.Repeat(_phase + matched / ground * dt, 1f);
                _landTime += dt;
            }
            else
            {
                float cadence = Mathf.Lerp(_profile.RunCadence, _profile.SprintCadence, sprint) * 0.5f;
                _phase = Mathf.Repeat(_phase + cadence * Mathf.Lerp(SlowestCadence, 1f, _moveWeight) * dt, 1f);
            }
            _time += dt;

            if (!onFloor)
            {
                _fallSpeed = Mathf.Max(_fallSpeed, -_velocity.y);
            }
            else if (!_wasOnFloor)
            {
                if (!_usesClips)
                {
                    float dip = Mathf.Min(_fallSpeed * _profile.LandGive, _profile.LandDip);
                    _land.Rate.x += dip * 2f * Mathf.PI * _profile.LandBounce;
                }
                _landStrength = Mathf.Clamp01(_fallSpeed / _profile.LandFullSpeed);
                _landTime = 0f;
                _fallSpeed = 0f;
            }
            else
            {
                float lost = _lastGroundSpeed - _groundSpeed;
                if (lost > _profile.ImpactSpeed)
                {
                    _stagger = Mathf.Clamp01(_stagger + lost / _profile.ImpactFullSpeed);
                    if (!_usesClips)
                    {
                        Vector3 thrown = _travel * (lost * _profile.ImpactKick);
                        _torso.Rate += thrown;
                        _headSpring.Rate += thrown;
                    }
                }
            }
            _wasOnFloor = onFloor;
        }

        static Quaternion LeanToward(Vector3 direction, float degrees)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, new Vector3(direction.x, 0f, direction.z));
            return axis.sqrMagnitude > 0f ? Quaternion.AngleAxis(degrees, axis.normalized) : Quaternion.identity;
        }

        void PoseFromClips(float sprint, float dt)
        {
            float floor = 1f - _airWeight;
            float landAt = Mathf.Min(_landTime * LandClipRate, _clipLengths[LandInput]);
            float land = floor * _landStrength
                * (1f - Mathf.SmoothStep(LandFadeStart, 1f, landAt / _clipLengths[LandInput]))
                * Mathf.Lerp(1f, RunningLandShare, _moveWeight);
            float slide = (floor - land) * _slideWeight;
            float ground = floor - land - slide;
            float moving = ground * _moveWeight;
            float crouched = _crouchWeight;
            float idle = ground - moving;
            float standingIdle = idle * (1f - crouched);
            float crouchMoving = moving * crouched;
            moving -= crouchMoving;
            float sprinting = moving * sprint * Mathf.Clamp01(_travel.z);
            float running = moving - sprinting;

            float heading = Mathf.Atan2(_travel.x, _travel.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(_runDirection * RunDirectionStep, heading)) > RunDirectionStep * 0.5f + RunDirectionSlack)
                _runDirection = Mathf.RoundToInt(Mathf.Repeat(heading, 360f) / RunDirectionStep) % AnimationProfile.RunDirections;

            float fidget = Fidget(dt, standingIdle);
            SetClipWeight(IdleInput, standingIdle * (1f - fidget));
            SetClipWeight(FidgetInput, standingIdle * fidget);
            float ease = 1f - Mathf.Exp(-dt / RunDirectionBlendTime);
            for (int i = 0; i < AnimationProfile.RunDirections; i++)
            {
                float wanted = i == _runDirection ? 1f : 0f;
                _runShares[i] += (wanted - _runShares[i]) * ease;
                SetClipWeight(RunInput + i, running * _runShares[i]);
                if (_clipWeights[RunInput + i] > 0f)
                    _clips[RunInput + i].SetTime(_phase * _clipLengths[RunInput + i]);
                if (_usesCrouch)
                {
                    SetClipWeight(CrouchRunInput + i, crouchMoving * _runShares[i]);
                    if (_clipWeights[CrouchRunInput + i] > 0f)
                        _clips[CrouchRunInput + i].SetTime(_phase * _clipLengths[CrouchRunInput + i]);
                }
            }
            if (_usesCrouch)
            {
                SetClipWeight(CrouchIdleInput, idle - standingIdle);
                if (_clipWeights[CrouchIdleInput] > 0f)
                    _clips[CrouchIdleInput].SetTime(Mathf.Repeat(_time, _clipLengths[CrouchIdleInput]));
                SetClipWeight(SlideInput, slide);
                if (_clipWeights[SlideInput] > 0f)
                    _clips[SlideInput].SetTime(Mathf.Repeat(_time, _clipLengths[SlideInput]));
            }
            SetClipWeight(SprintInput, sprinting);
            SetClipWeight(AirInput, _airWeight);
            SetClipWeight(LandInput, land);

            if (_clipWeights[IdleInput] > 0f)
                _clips[IdleInput].SetTime(Mathf.Repeat(_time, _clipLengths[IdleInput]));
            if (_clipWeights[SprintInput] > 0f)
                _clips[SprintInput].SetTime(_phase * _clipLengths[SprintInput]);
            if (_clipWeights[AirInput] > 0f)
                _clips[AirInput].SetTime(Mathf.Repeat(_time, _clipLengths[AirInput]));
            if (_clipWeights[LandInput] > 0f)
                _clips[LandInput].SetTime(landAt);

            // Reset skeleton to T-pose rest so un-animated bones do not retain last frame's ragdoll output.
            for (int i = 0; i < _skeleton.Length; i++)
                _skeleton[i].SetLocalPositionAndRotation(_skeletonRestPositions[i], _skeletonRestRotations[i]);
            _graph.Evaluate();
            AlignUpperBodyToAim(dt, floor - slide);
            ShapeAirPose();
            TurnHeadFirst(dt);
            Breathe(idle);
        }

        void SetClipWeight(int input, float weight)
        {
            if (weight < MinClipWeight)
                weight = 0f;
            if (_clipWeights[input] == weight)
                return;
            _clipWeights[input] = weight;
            _mixer.SetInputWeight(input, weight);
        }

        float Fidget(float dt, float standing)
        {
            bool still = _moveWeight < FidgetStillness && _airWeight < FidgetStillness && standing > 1f - FidgetStillness && !_bodyTurning;
            if (_fidget < 0)
            {
                if (!still || _fidgets.Length == 0)
                    return 0f;
                _fidgetWait -= dt;
                if (_fidgetWait > 0f)
                    return 0f;
                int pick = Random.Range(0, _fidgets.Length);
                if (!_fidgets[pick].IsValid())
                    return 0f;
                _fidget = pick;
                _fidgetTime = 0f;
                _fidgetWeight = 0f;
                _graph.Disconnect(_mixer, FidgetInput);
                _graph.Connect(_fidgets[pick], 0, _mixer, FidgetInput);
            }

            float length = _profile.FidgetClips[_fidget].length;
            _fidgetTime += dt;
            _fidgetWeight = Mathf.MoveTowards(_fidgetWeight, still && _fidgetTime < length ? 1f : 0f, dt / FidgetBlendTime);
            _fidgets[_fidget].SetTime(Mathf.Min(_fidgetTime, length));
            if (_fidgetWeight <= 0f && (!still || _fidgetTime >= length))
            {
                _graph.Disconnect(_mixer, FidgetInput);
                _fidget = -1;
                _fidgetWait = Random.Range(_profile.FidgetMinWait, _profile.FidgetMaxWait);
                return 0f;
            }
            return _fidgetWeight;
        }

        void AlignUpperBodyToAim(float dt, float groundedNonSlide)
        {
            float strafeFactor = _travel.z < 0f ? 1f : Mathf.Abs(_travel.x);
            float targetWeight = strafeFactor * Mathf.Clamp01(groundedNonSlide) * _moveWeight * _profile.StrafeAimAlign;
            _strafeAimWeight += (targetWeight - _strafeAimWeight) * (1f - Mathf.Exp(-dt / StrafeAimBlendTime));
            if (_strafeAimWeight < MinClipWeight)
                return;

            Bone chestRef = _upperChest.T != null ? _upperChest : (_chest.T != null ? _chest : _spine);
            if (chestRef.T == null)
                return;

            Quaternion poseInModel = Quaternion.Inverse(_model.rotation) * chestRef.T.rotation * Quaternion.Inverse(chestRef.Rest);
            Vector3 chestForward = poseInModel * Vector3.forward;
            if (chestForward.x * chestForward.x + chestForward.z * chestForward.z < 0.01f)
                return;

            float chestYaw = Mathf.Atan2(chestForward.x, chestForward.z) * Mathf.Rad2Deg;
            float twist = Mathf.Clamp(-chestYaw * _strafeAimWeight, -MaxStrafeAimTwist, MaxStrafeAimTwist);
            if (Mathf.Abs(twist) < 0.05f)
                return;

            Vector3 up = _model.up;
            bool hasSpine = _spine.T != null;
            bool hasChest = _chest.T != null;
            bool hasUpper = _upperChest.T != null;
            float wSpine = hasSpine ? (hasChest || hasUpper ? 0.35f : 1f) : 0f;
            float wChest = hasChest ? (hasUpper ? 0.35f : (hasSpine ? 0.65f : 1f)) : 0f;
            float wUpper = hasUpper ? (1f - wSpine - wChest) : 0f;

            if (hasSpine) _spine.T.rotation = Quaternion.AngleAxis(twist * wSpine, up) * _spine.T.rotation;
            if (hasChest) _chest.T.rotation = Quaternion.AngleAxis(twist * wChest, up) * _chest.T.rotation;
            if (hasUpper) _upperChest.T.rotation = Quaternion.AngleAxis(twist * wUpper, up) * _upperChest.T.rotation;

            if (_neck.T != null)
            {
                _neck.T.rotation = Quaternion.AngleAxis(-twist * 0.4f, up) * _neck.T.rotation;
                if (_head.T != null)
                    _head.T.rotation = Quaternion.AngleAxis(-twist * 0.6f, up) * _head.T.rotation;
            }
            else if (_head.T != null)
            {
                _head.T.rotation = Quaternion.AngleAxis(-twist, up) * _head.T.rotation;
            }
        }

        void ShapeAirPose()
        {
            float w = _airWeight * (1f - _knocked);
            if (w < MinClipWeight)
                return;

            float rise = Mathf.Clamp(_local.y / AirVelocityRef, -1f, 1f);
            float motion = _profile.AirPoseMotion;
            Vector3 right = _model.right, forward = _model.forward;

            if (motion > 0f)
            {
                float spinePitch = (rise >= 0f ? AirRiseSpinePitch * rise : AirFallSpinePitch * -rise) * motion * w;
                if (Mathf.Abs(spinePitch) > 0.05f)
                {
                    bool hasSpine = _spine.T != null;
                    bool hasChest = _chest.T != null;
                    if (hasSpine) _spine.T.rotation = Quaternion.AngleAxis(spinePitch * (hasChest ? 0.5f : 1f), right) * _spine.T.rotation;
                    if (hasChest) _chest.T.rotation = Quaternion.AngleAxis(spinePitch * (hasSpine ? 0.5f : 1f), right) * _chest.T.rotation;
                    if (_head.T != null) _head.T.rotation = Quaternion.AngleAxis(-spinePitch * 0.7f, right) * _head.T.rotation;
                }
            }

            float armDown = Mathf.Max(0f, _profile.AirArmTuck - Mathf.Max(0f, -rise) * AirFallArmLift * motion) * w;
            float armFwd = AirArmForwardTuck * w;
            float elbowBend = AirElbowTuck * w;

            if (_leftUpperArm.T != null)
            {
                _leftUpperArm.T.rotation = Quaternion.AngleAxis(-armFwd, right) * Quaternion.AngleAxis(armDown, forward) * _leftUpperArm.T.rotation;
                if (_leftLowerArm.T != null)
                {
                    Vector3 leftElbowAxis = _leftLowerArm.T.rotation * (Quaternion.Inverse(_leftLowerArm.Rest) * Vector3.up);
                    _leftLowerArm.T.rotation = Quaternion.AngleAxis(elbowBend, leftElbowAxis) * _leftLowerArm.T.rotation;
                }
            }
            if (_rightUpperArm.T != null)
            {
                _rightUpperArm.T.rotation = Quaternion.AngleAxis(-armFwd, right) * Quaternion.AngleAxis(-armDown, forward) * _rightUpperArm.T.rotation;
                if (_rightLowerArm.T != null)
                {
                    Vector3 rightElbowAxis = _rightLowerArm.T.rotation * (Quaternion.Inverse(_rightLowerArm.Rest) * Vector3.up);
                    _rightLowerArm.T.rotation = Quaternion.AngleAxis(-elbowBend, rightElbowAxis) * _rightLowerArm.T.rotation;
                }
            }

            if (motion <= 0f)
                return;

            float thighPitch = (rise >= 0f ? -AirRiseThighLift * rise : AirFallThighReach * -rise) * motion * w;
            float kneePitch = (rise >= 0f ? AirRiseKneeBend * rise : -AirFallKneeExtend * -rise) * motion * w;
            if (_leftUpperLeg.T != null)
                _leftUpperLeg.T.rotation = Quaternion.AngleAxis(thighPitch, right) * _leftUpperLeg.T.rotation;
            if (_leftLowerLeg.T != null)
                _leftLowerLeg.T.rotation = Quaternion.AngleAxis(kneePitch, right) * _leftLowerLeg.T.rotation;
            if (_rightUpperLeg.T != null)
                _rightUpperLeg.T.rotation = Quaternion.AngleAxis(thighPitch * AirTrailLegShare, right) * _rightUpperLeg.T.rotation;
            if (_rightLowerLeg.T != null)
                _rightLowerLeg.T.rotation = Quaternion.AngleAxis(kneePitch * AirTrailLegShare, right) * _rightLowerLeg.T.rotation;
        }

        void TurnHeadFirst(float dt)
        {
            float look = _model.eulerAngles.y;
            if (!_lookStarted)
            {
                _lookStarted = true;
                _lookYaw = look;
            }
            // The body stays put in the world, so a turn of the look leaves it that much behind.
            _bodyBehind = Mathf.DeltaAngle(look, _lookYaw + _bodyBehind);
            _lookYaw = look;

            bool standing = _moveWeight < StandingToLook && _airWeight < StandingToLook;
            float limit = _hasMotor && PlayerMotor.Local == _motor
                ? Mathf.Min(_profile.HeadTurnLimit, BodyViewHeadTurnLimit)
                : _profile.HeadTurnLimit;
            if (!standing || Mathf.Abs(_bodyBehind) > limit)
                _bodyTurning = true;
            if (_bodyTurning)
            {
                _bodyBehind = Mathf.MoveTowards(_bodyBehind, 0f, _profile.BodyTurnSpeed * dt);
                _bodyTurning = _bodyBehind != 0f;
            }
            if (_bodyBehind == 0f)
                return;

            Vector3 up = _model.up, feet = _model.position;
            Quaternion back = Quaternion.AngleAxis(_bodyBehind, up);
            // The whole skeleton hangs from the hips, so this turns all of it about the feet.
            _hips.T.SetPositionAndRotation(feet + back * (_hips.T.position - feet), back * _hips.T.rotation);
            float chest = _upperChest.T != null ? LookChestShare : 0f;
            if (_upperChest.T != null)
                _upperChest.T.rotation = Quaternion.AngleAxis(-_bodyBehind * chest, up) * _upperChest.T.rotation;
            _head.T.rotation = Quaternion.AngleAxis(-_bodyBehind * (1f - chest), up) * _head.T.rotation;
        }

        void Breathe(float standing)
        {
            float breath = _profile.BreathAmount * standing * Mathf.Sin(_time * _profile.BreathRate * 2f * Mathf.PI);
            if (breath == 0f)
                return;

            Vector3 across = _model.right, ahead = _model.forward;
            Quaternion spine = Quaternion.AngleAxis(-breath * BreathSpineShare, across);
            if (_chest.T != null) _chest.T.rotation = spine * _chest.T.rotation;
            if (_upperChest.T != null) _upperChest.T.rotation = spine * _upperChest.T.rotation;

            float lift = breath * BreathShoulderShare;
            if (_leftShoulder.T != null)
            {
                _leftShoulder.T.rotation = Quaternion.AngleAxis(-lift, ahead) * _leftShoulder.T.rotation;
                _leftUpperArm.T.rotation = Quaternion.AngleAxis(lift, ahead) * _leftUpperArm.T.rotation;
            }
            if (_rightShoulder.T != null)
            {
                _rightShoulder.T.rotation = Quaternion.AngleAxis(lift, ahead) * _rightShoulder.T.rotation;
                _rightUpperArm.T.rotation = Quaternion.AngleAxis(-lift, ahead) * _rightUpperArm.T.rotation;
            }

            _head.T.rotation = Quaternion.AngleAxis(breath, across) * _head.T.rotation;
        }

        void Pose(float sprint)
        {
            Quaternion modelRotation = _model.rotation;
            float floor = 1f - _airWeight;
            float stride = _moveWeight * floor;
            float idle = (1f - _moveWeight) * floor;
            float cycle = _phase * 2f * Mathf.PI;
            float groundTime = _profile.GroundTime;

            float trail = Mathf.Clamp((_turn.Value.x - _yawRate) * _profile.TurnLag, -MaxTurnTwist, MaxTurnTwist);
            float bank = -Mathf.Clamp(_turn.Value.x * _profile.TurnBank, -MaxBank, MaxBank) * stride;
            Vector3 carry = _torso.Value - new Vector3(_local.x, 0f, _local.z);
            float landed = Mathf.Clamp(_land.Value.x, -MaxRebound, _profile.LandDip);
            float overrun = Vector3.Dot(carry, _travel);
            Vector3 planarNow = new Vector3(_local.x, 0f, _local.z);
            Vector3 leaning = overrun < 0f ? Vector3.Lerp(_torso.Value, planarNow, StartLead) : _torso.Value;
            Vector3 headLeaning = overrun < 0f ? Vector3.Lerp(_headSpring.Value, planarNow, StartLead) : _headSpring.Value;

            float bounce = _profile.Bounce * stride * Mathf.Cos(2f * (cycle - Mathf.PI * groundTime));
            float stopping = Mathf.Clamp(overrun * _profile.StopCrouch, 0f, MaxStopCrouch);
            float drop = _profile.HipDrop * idle + _profile.RunCrouch * stride * stride + bounce
                + (stopping + landed + _profile.StaggerDip * _stagger) * floor;
            float sway = Mathf.Sin(_time * _profile.IdleSwayRate * 2f * Mathf.PI) * idle;
            Vector3 hipsOffset = Vector3.down * drop
                + _travel * (Mathf.Clamp(overrun * _profile.BodyCarry, 0f, MaxCarry) * floor)
                + Vector3.right * (_profile.IdleSway * sway);

            float leanScale = Mathf.Lerp(1f, -KnockedTrail, _knocked);
            float lean = Mathf.Clamp(_profile.LeanPerSpeed * leaning.magnitude * leanScale, -MaxLean, MaxLean);
            float rock = _profile.Waddle * stride * Mathf.Sin(cycle) + bank;
            float hipTwist = _profile.HipTwist * stride * Mathf.Cos(cycle);
            float shoulderTwist = _profile.ShoulderTwist * stride * Mathf.Cos(cycle);
            Quaternion hips = LeanToward(leaning, lean * HipsLeanShare)
                * Quaternion.AngleAxis(rock * HipsRockShare, Vector3.forward)
                * Quaternion.AngleAxis(hipTwist, Vector3.up);
            _hips.T.position = _model.TransformPoint(_hipsRest + hipsOffset);
            Set(_hips, modelRotation, hips);

            float breath = Mathf.Sin(_time * _profile.BreathRate * 2f * Mathf.PI) * idle;
            float nod = _profile.StepNod * stride * Mathf.Cos(2f * (cycle - Mathf.PI * groundTime));
            float fold = Mathf.Max(0f, landed) * _profile.LandFold + _profile.BreathAmount * breath + nod;
            Quaternion torso = LeanToward(leaning, lean)
                * Quaternion.AngleAxis(fold, Vector3.right)
                * Quaternion.AngleAxis(rock, Vector3.forward)
                * Quaternion.AngleAxis(trail - shoulderTwist, Vector3.up);
            Set(_spine, modelRotation, Quaternion.Slerp(hips, torso, SpineShare));
            Set(_chest, modelRotation, Quaternion.Slerp(hips, torso, ChestShare));
            Set(_upperChest, modelRotation, torso);

            float wander = _time * _profile.IdleLookRate;
            float lookYaw = _profile.IdleLook * idle * (Mathf.PerlinNoise(wander, 0f) * 2f - 1f);
            float lookPitch = _profile.IdleLook * LookPitchShare * idle * (Mathf.PerlinNoise(0f, wander) * 2f - 1f);
            float headLean = Mathf.Clamp(_profile.LeanPerSpeed * _profile.HeadLean * headLeaning.magnitude * leanScale, -MaxLean, MaxLean);
            Quaternion head = LeanToward(headLeaning, headLean)
                * Quaternion.AngleAxis((fold - nod) * _profile.HeadLean + lookPitch - nod * HeadCounterNod, Vector3.right)
                * Quaternion.AngleAxis(rock * HeadRockShare, Vector3.forward)
                * Quaternion.AngleAxis(trail + lookYaw, Vector3.up);
            Set(_neck, modelRotation, Quaternion.Slerp(torso, head, 0.5f));
            Set(_head, modelRotation, head);

            float raised = Mathf.Max(_airWeight, _knocked * KnockedArmRaise);
            float down = Mathf.Lerp(_profile.IdleArmDown, _profile.RunArmDown, _moveWeight) - _profile.BreathAmount * breath;
            float falling = Mathf.InverseLerp(RisingSpeed, FallingSpeed, _local.y);
            down = Mathf.Lerp(down, _profile.RunArmDown - _profile.AirArmRaise * Mathf.Lerp(RisingArmRaise, 1f, falling), raised);
            float bend = Mathf.Lerp(_profile.IdleElbowBend, _profile.ElbowBend, _moveWeight);
            bend = Mathf.Lerp(bend, _profile.AirElbowBend, raised);
            float swing = Mathf.Lerp(_profile.ArmSwing, _profile.SprintArmSwing, sprint) * stride * Mathf.Cos(cycle);
            float offset = ArmSwingSetBack * stride - _profile.IdleArmForward * idle;
            float drift = _time * _profile.IdleLookRate;
            float leftDrift = _profile.IdleArmDrift * idle * (Mathf.PerlinNoise(drift, LeftArmNoiseRow) * 2f - 1f);
            float rightDrift = _profile.IdleArmDrift * idle * (Mathf.PerlinNoise(drift, RightArmNoiseRow) * 2f - 1f);
            float pump = _profile.ElbowPump * stride * Mathf.Cos(cycle);
            float reachForward = -_profile.AirArmForward * raised;
            PoseArm(_leftUpperArm, _leftLowerArm, modelRotation, torso, new Vector3(swing + offset + leftDrift + reachForward, down, bend - pump), 1f);
            PoseArm(_rightUpperArm, _rightLowerArm, modelRotation, torso, new Vector3(-swing + offset + rightDrift + reachForward, down, bend + pump), -1f);

            float reach = Mathf.Lerp(_profile.StepReach, _profile.SprintStepReach, sprint) * stride;
            float kick = Mathf.Lerp(_profile.HeelKick, _profile.SprintHeelKick, sprint) * stride;
            float tuck = _profile.AirTuck * Mathf.Lerp(RisingTuck, 1f, falling);
            Vector3 leftAir = new Vector3(0f, tuck, _profile.AirLegSplit);
            Vector3 rightAir = new Vector3(0f, tuck * TrailingTuck, -_profile.AirLegSplit * TrailingSplit);
            Vector3 left = FootTarget(_leftFootRest, _phase, reach, kick, stride, leftAir, out float leftPitch);
            Vector3 right = FootTarget(_rightFootRest, Mathf.Repeat(_phase + 0.5f, 1f), reach, kick, stride, rightAir, out float rightPitch);
            Vector3 knee = modelRotation * Vector3.forward;
            SolveLeg(_leftUpperLeg, _leftLowerLeg, _leftFoot, modelRotation, hips, Quaternion.AngleAxis(leftPitch, Vector3.right), _model.TransformPoint(left), knee);
            SolveLeg(_rightUpperLeg, _rightLowerLeg, _rightFoot, modelRotation, hips, Quaternion.AngleAxis(rightPitch, Vector3.right), _model.TransformPoint(right), knee);
        }

        static void Set(in Bone bone, Quaternion modelRotation, Quaternion offset)
        {
            if (bone.T != null)
                bone.T.rotation = modelRotation * offset * bone.Rest;
        }

        static void PoseArm(in Bone upper, in Bone lower, Quaternion modelRotation, Quaternion torso, Vector3 pose, float side)
        {
            Quaternion hang = Quaternion.AngleAxis(pose.y * side, Vector3.forward);
            Set(upper, modelRotation, torso * Quaternion.AngleAxis(pose.x, Vector3.right) * hang);
            Set(lower, modelRotation, torso * Quaternion.AngleAxis(pose.x - pose.z, Vector3.right) * hang);
        }

        Vector3 FootTarget(Vector3 rest, float phase, float reach, float kick, float stride, Vector3 air, out float pitch)
        {
            float groundTime = _profile.GroundTime;
            float along, up;
            if (phase < groundTime)
            {
                float t = phase / groundTime;
                along = Mathf.Lerp(reach, -reach, t);
                up = 0f;
                pitch = _profile.FootRoll * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RollStart, 1f, t));
            }
            else
            {
                float t = (phase - groundTime) / (1f - groundTime);
                float arc = Mathf.Sin(Mathf.PI * Mathf.Pow(t, KickTiming));
                along = Mathf.Lerp(-reach, reach, Mathf.SmoothStep(0f, 1f, t));
                up = kick * arc;
                pitch = Mathf.Lerp(_profile.FootRoll, 0f, t) + (_profile.KickFootPitch - _profile.FootRoll) * arc * (1f - t);
            }

            Vector3 stepping = rest + _travel * along + Vector3.up * up;
            stepping.x = Mathf.Lerp(stepping.x, rest.x * RunTrackWidth + _travel.x * along, stride);
            pitch = Mathf.Lerp(pitch * stride, AirToePoint, _airWeight);
            return Vector3.Lerp(stepping, rest + air, _airWeight);
        }

        static void SolveLeg(in Bone upper, in Bone lower, in Bone foot, Quaternion modelRotation, Quaternion hipsOffset, Quaternion footTip, Vector3 target, Vector3 kneeForward)
        {
            // Reset to rest before solving so thigh twist never accumulates across frames.
            Set(upper, modelRotation, hipsOffset);
            Set(lower, modelRotation, hipsOffset);

            Vector3 hip = upper.T.position;
            float thigh = Vector3.Distance(hip, lower.T.position);
            float shin = Vector3.Distance(lower.T.position, foot.T.position);

            Vector3 toTarget = target - hip;
            float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Epsilon, (thigh + shin) * MaxLegExtension);
            Vector3 direction = toTarget.normalized;

            float along = (thigh * thigh - shin * shin + distance * distance) / (2f * distance);
            float off = Mathf.Sqrt(Mathf.Max(0f, thigh * thigh - along * along));
            Vector3 bend = Vector3.ProjectOnPlane(kneeForward, direction).normalized;
            Vector3 kneePosition = hip + direction * along + bend * off;

            upper.T.rotation = Quaternion.FromToRotation(lower.T.position - hip, kneePosition - hip) * upper.T.rotation;
            lower.T.rotation = Quaternion.FromToRotation(foot.T.position - lower.T.position, hip + direction * distance - lower.T.position) * lower.T.rotation;
            Set(foot, modelRotation, footTip);
        }
    }
}
