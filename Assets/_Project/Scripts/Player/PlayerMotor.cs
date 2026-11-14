using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace CloseTheDeal.Player
{
    public enum MoveState : byte
    {
        Grounded = 0,
        Airborne = 1,
        Mantling = 2,
        Knocked = 3
    }

    /// <summary>
    /// The player's body: walking, crouching, jumping, climbing and being knocked about.
    /// Host-authoritative with client prediction (FishNet): the owner runs the same tick
    /// logic ahead of the host and is corrected by the host's reconcile when they differ.
    /// This file is the tick and the states; climbing is in PlayerMotor.Mantle.cs, sliding in PlayerMotor.Slide.cs, and what
    /// rigs, sound and UI read is in PlayerMotor.Feedback.cs.
    /// Design and ownership: docs/systems/PLAYER_MOVEMENT.md.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(PlayerInputReader))]
    public sealed partial class PlayerMotor : TickNetworkBehaviour
    {
        // ---- Replicated data ---------------------------------------------------------------

        public struct OneShots
        {
            public bool Jump;

            public void Clear()
            {
                Jump = false;
            }
        }

        /// <summary>One tick of input from the owner. Sent owner → host every tick.</summary>
        public struct MoveInput : IReplicateData
        {
            public Vector2 Move;
            public float Yaw;
            public bool Sprint;
            public bool Crouch;
            /// <summary>Jump is held down; letting go while rising cuts the jump short.</summary>
            public bool JumpHeld;
            /// <summary>-1 lean left, +1 lean right, 0 upright.</summary>
            public sbyte Lean;
            public OneShots OneShots;
            uint _tick;

            public void Dispose() => OneShots.Clear();
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        /// <summary>The host's word on where the body is. Sent host → everyone every tick.</summary>
        public struct MoveReconcile : IReconcileData
        {
            public PredictionRigidbody Body;
            public MoveState State;
            public uint StateTicksLeft;
            public MantleData Mantle;
            /// <summary>A throw the host has decided but the body's next tick has not applied yet.</summary>
            public Vector3 PendingKnock;
            public JumpTimers Jump;
            /// <summary>-1 to +1: how far the player is leaning, and which way.</summary>
            public float Lean;
            /// <summary>0 standing to 1 fully crouched.</summary>
            public float Crouch;
            public FeedbackCounters Counters;
            public SlideData Slide;
            uint _tick;

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        /// <summary>The jump forgiveness windows, counted in ticks remaining so a reconcile restores them exactly.</summary>
        public struct JumpTimers
        {
            /// <summary>Ticks left in which a jump still works after walking off an edge.</summary>
            public byte Coyote;
            /// <summary>Ticks left in which an early press still fires on landing.</summary>
            public byte Buffer;
            /// <summary>Still rising from a jump that letting go of Space can cut short.</summary>
            public bool Rising;
        }

        /// <summary>A climb in progress.</summary>
        public struct MantleData
        {
            /// <summary>Where the feet are heading.</summary>
            public Vector3 Target;
            /// <summary>Velocity the body leaves the climb with, metres per second.</summary>
            public Vector3 Exit;
            public byte Phase;
            public MantleKind Kind;
        }

        /// <summary>A slide in progress, and how long until the next one may boost.</summary>
        public struct SlideData
        {
            public bool Active;
            public ushort BoostCooldownTicks;
        }

        /// <summary>
        /// Counts of things that happened, carried in the reconcile so every machine can tell
        /// "a jump happened" from "the same jump was replayed". They wrap; only a change matters.
        /// </summary>
        public struct FeedbackCounters
        {
            public byte Jumps;
            public byte Mantles;
            public byte Refusals;
            public MantleRefusal LastRefusal;
            public byte Landings;
            /// <summary>How fast the last landing was falling, metres per second.</summary>
            public float LastLandSpeed;
            /// <summary>Off the floor since the last landing; a climb in progress doesn't count.</summary>
            public bool InAir;
        }

        struct GroundHit
        {
            public bool Found;
            public Vector3 Normal;
            /// <summary>How far the feet are above the floor, metres.</summary>
            public float Gap;
        }

        // ---- Inspector -----------------------------------------------------------------------

        [Tooltip("Designer tuning for walking, jumping and climbing. Read at runtime, never written.")]
        [SerializeField] MovementProfile _profile;

        [Tooltip("What counts as floor, wall and ledge: everything solid in the level, but not players.")]
        [SerializeField] LayerMask _groundMask = ~0;

        [Tooltip("The smoothed visual the camera follows. FishNet moves this child between ticks so motion never steps.")]
        [SerializeField] Transform _cameraTarget;

        [Tooltip("The part of the visual that tilts and slides when this player leans, pivoting at the hips, so other players see the peek. Built by the greybox tool; leave empty and leaning still works, it just isn't shown on the body.")]
        [SerializeField] Transform _leanPivot;

        [Tooltip("Tick to squash the body down when this player crouches, so a crouch shows on a body with no animation. Untick once the character has a crouch animation of its own.")]
        [SerializeField] bool _squashBodyWhenCrouched = true;

        // ---- Runtime -------------------------------------------------------------------------

        const float GroundSlopeLimit = 50f;
        const float MantleTimeoutMultiplier = 2f;
        const float ArriveDistance = 0.05f;
        const float ProbeInset = 0.05f;
        const float CorrectionNoticeMetres = 0.01f;
        const byte MantleRise = 0;
        const byte MantleStepOnto = 1;
        const float LeanShotRadius = 0.1f;
        /// <summary>Fastest the body is pulled down onto a floor that dropped away under it, metres per second. Two ticks cover the deepest snap.</summary>
        const float MaxSnapSpeed = 12f;
        /// <summary>A gap under the feet smaller than this is left to physics, metres.</summary>
        const float SnapSlack = 0.005f;
        /// <summary>Widest change of direction the floor turn swings through, degrees; anything wider is a reversal and brakes.</summary>
        const float MaxGroundTurnAngle = 100f;


        /// <summary>The body this client controls, for local readouts. Null on a pure host with no player.</summary>
        public static PlayerMotor Local { get; private set; }

        /// <summary>Raised when the local player's body arrives (the new one) or leaves (null). A HUD subscribes here instead of polling <see cref="Local"/>.</summary>
        public static event System.Action<PlayerMotor> LocalChanged;

        /// <summary>
        /// How many ticks past another player's last known input this machine keeps repeating
        /// it (presses left out) while it runs that body ahead. About a third of a second covers
        /// a 100 ms round trip with FishNet's interpolation; beyond it the body is assumed to
        /// have let go of everything, so a player who drops out coasts to a stop.
        /// </summary>
        const uint SpectatorRepeatTicks = 20;

        /// <summary>
        /// Whether the host also fills a client's missing ticks with its last real input (true) or
        /// runs them empty, as FishNet hands them over (false). Other clients always fill. Kept
        /// switchable while the two are measured against each other; see PLAYER_MOVEMENT.md.
        /// </summary>
        internal static bool HostFillsMissingInput = true;

        readonly PredictionRigidbody _body = new();
        Vector3 _tipBeforeReconcile;
        Rigidbody _rigidbody;
        CapsuleCollider _capsule;
        PlayerInputReader _input;
        float _standHeight;

        MoveState _state;
        uint _stateTicksLeft;
        MantleData _mantle;
        Vector3 _pendingKnock;
        JumpTimers _jump;
        float _lean;
        float _crouch;
        FeedbackCounters _counters;
        SlideData _slide;
        uint _currentTick;
        MoveInput _lastRealInput;
        bool _countersSeeded;
        GroundHit _ground;

        public MoveState State => _state;
        public bool Grounded => _ground.Found;
        public float PlanarSpeed => Planar(_rigidbody.linearVelocity).magnitude;
        public MovementProfile Profile => _profile;

        /// <summary>The designer's profile from the prefab, kept while a runtime copy is in use.</summary>
        MovementProfile _authoredProfile;

        /// <summary>
        /// Moves with a runtime copy of the profile instead of the designer's asset (a heavy
        /// carried load's slower numbers), or with the designer's again when given null. Never
        /// pass the designer's asset after changing it: copy it (AGENTS.md §4). The owner and the
        /// host must make the same call on the same tick, or prediction and host disagree.
        /// </summary>
        public void UseProfile(MovementProfile runtimeCopy)
        {
            _profile = runtimeCopy != null ? runtimeCopy : _authoredProfile;
            _input.Profile = _profile;
        }

        /// <summary>-1 to +1: how far this player is leaning, and which way. Moves once per tick.</summary>
        public float Lean => _lean;

        /// <summary>0 standing to 1 fully crouched. Moves once per tick.</summary>
        public float Crouch => _crouch;

        /// <summary>Height of the eye above the feet when standing, in metres.</summary>
        public float StandingEyeHeight => _profile.EyeHeight;

        /// <summary>How far the eye comes down at full crouch, in metres.</summary>
        public float CrouchEyeDrop => _standHeight - CrouchedHeight;

        /// <summary>Height of the eye above the feet right now, in metres. Shots leave from here.</summary>
        public float EyeHeight => StandingEyeHeight - CrouchEyeDrop * _crouch;

        /// <summary>How far the host's last correction moved this body's latest predicted position, in metres, after replaying the inputs the host had not yet seen. Only the owning client measures it; the host never corrects itself.</summary>
        public float LastCorrectionMetres { get; private set; }

        /// <summary>How many corrections above a centimetre have arrived since spawn.</summary>
        public int CorrectionCount { get; private set; }


        float CrouchedHeight => Mathf.Clamp(_profile.CrouchHeight, _capsule.radius * 2f, _standHeight);

        // ---- Lifecycle -----------------------------------------------------------------------

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
            _input = GetComponent<PlayerInputReader>();
            _authoredProfile = _profile;
            _input.Profile = _profile;
            _input.enabled = false;
            _standHeight = _capsule.height;
            _body.Initialize(_rigidbody);
            AwakeFeedback();
        }

        public override void OnStartNetwork()
        {
            // A pooled body comes back with whatever its last life left behind.
            _state = MoveState.Grounded;
            _stateTicksLeft = 0;
            _mantle = default;
            _pendingKnock = Vector3.zero;
            _jump = default;
            _lean = 0f;
            _crouch = 0f;
            _counters = default;
            _slide = default;
            _lastRealInput = default;
            _countersSeeded = false;
            _ground = default;
            LastCorrectionMetres = 0f;
            CorrectionCount = 0;
            ApplyCrouchToCapsule();
            ResetFeedback();
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
                return;

            Local = this;
            LocalChanged?.Invoke(this);
            _input.enabled = true;
            if (!IsServerStarted)
            {
                PredictionManager.OnPreReconcile += NoteTipBeforeReconcile;
                PredictionManager.OnPostReconcile += MeasureCorrection;
            }
            ShowOwnBody(false);
            if (PlayerCamera.Instance != null)
                PlayerCamera.Instance.Follow(_cameraTarget, _input, this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsOwner)
                return;

            if (Local == this)
            {
                Local = null;
                LocalChanged?.Invoke(null);
            }
            PredictionManager.OnPreReconcile -= NoteTipBeforeReconcile;
            PredictionManager.OnPostReconcile -= MeasureCorrection;
            _input.enabled = false;
            ShowOwnBody(true);
            if (PlayerCamera.Instance != null)
                PlayerCamera.Instance.Release(_cameraTarget);
        }

        // ---- Ticks ---------------------------------------------------------------------------

        protected override void TimeManager_OnTick()
        {
            RunMove(BuildInput());
        }

        protected override void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }

        MoveInput BuildInput()
        {
            if (!IsOwner)
                return default;

            PlayerCamera cam = PlayerCamera.Instance;
            float yaw = cam != null ? cam.Yaw : _rigidbody.rotation.eulerAngles.y;
            return new MoveInput
            {
                Move = _input.Move,
                Yaw = yaw,
                Sprint = _input.Sprint,
                Crouch = _input.Crouch,
                JumpHeld = _input.JumpHeld,
                Lean = _input.Lean,
                // Attack and the other buttons belong to the weapon and item systems; movement only takes Jump.
                OneShots = new OneShots { Jump = _input.Consume(PlayerPress.Jump) }
            };
        }

        public override void CreateReconcile()
        {
            RunReconcile(new MoveReconcile
            {
                Body = _body,
                State = _state,
                StateTicksLeft = _stateTicksLeft,
                Mantle = _mantle,
                PendingKnock = _pendingKnock,
                Jump = _jump,
                Lean = _lean,
                Crouch = _crouch,
                Counters = _counters,
                Slide = _slide
            });
        }

        [Replicate]
        void RunMove(MoveInput input, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            input = FillMissingInput(input, state);
            if (!state.ContainsReplayed())
                _currentTick = input.GetTick();

            float dt = (float)TimeManager.TickDelta;
            BeginTick(input);

            switch (_state)
            {
                case MoveState.Grounded: GroundedStep(input, dt); break;
                case MoveState.Airborne: AirborneStep(input, dt); break;
                case MoveState.Mantling: MantleStep(dt); break;
                case MoveState.Knocked: KnockedStep(dt); break;
            }

            // After any velocity write: setting velocity clears queued forces, and this must survive.
            float yaw = Mathf.MoveTowardsAngle(_rigidbody.rotation.eulerAngles.y, input.Yaw, _profile.TurnSpeed * dt);
            _body.MoveRotation(Quaternion.Euler(0f, yaw, 0f));

            LeanStep(input, dt);
            CrouchStep(input, dt);
            EndTick();
        }

        /// <summary>Finds the floor and starts this tick's timers.</summary>
        void BeginTick(MoveInput input)
        {
            // A walking body keeps hold of a floor that drops away a little, so stairs and ramp tops don't launch it.
            float reach = _state == MoveState.Grounded ? Mathf.Max(_profile.GroundProbe, _profile.StepDownSnap) : _profile.GroundProbe;
            _ground = ProbeGround(reach);

            // Remember the press for a moment, so one made just before landing still jumps.
            if (input.OneShots.Jump)
                _jump.Buffer = (byte)(TicksFor(_profile.JumpBufferTime) + 1);

            CountLanding();
        }

        /// <summary>
        /// Counted on the tick, before a hop can launch again, so every landing of a chain of
        /// hops is reported once, on every machine, however briefly the feet touched.
        /// </summary>
        void CountLanding()
        {
            if (_state == MoveState.Mantling)
                return;

            float vertical = _rigidbody.linearVelocity.y;
            if (!_ground.Found)
            {
                _counters.InAir = true;
                return;
            }

            if (!_counters.InAir || vertical > 0f)
                return;

            _counters.InAir = false;
            _counters.Landings++;
            _counters.LastLandSpeed = -vertical;
        }

        void EndTick()
        {
            if (_jump.Buffer > 0)
                _jump.Buffer--;
            if (_slide.BoostCooldownTicks > 0)
                _slide.BoostCooldownTicks--;

            ApplyPendingKnock();
            _body.Simulate();
        }

        [Reconcile]
        void RunReconcile(MoveReconcile data, Channel channel = Channel.Unreliable)
        {
            _body.Reconcile(data.Body);
            _state = data.State;
            _stateTicksLeft = data.StateTicksLeft;
            _mantle = data.Mantle;
            _pendingKnock = data.PendingKnock;
            _jump = data.Jump;
            _lean = data.Lean;
            _crouch = data.Crouch;
            _counters = data.Counters;
            _slide = data.Slide;

            // A player who joins late must not hear every jump and landing the others made before.
            if (!IsOwner && !_countersSeeded)
            {
                _seenCounters = data.Counters;
                _countersSeeded = true;
            }
            ApplyCrouchToCapsule();
        }

        /// <summary>The body's latest predicted position, just before the host's correction is applied.</summary>
        void NoteTipBeforeReconcile(uint clientTick, uint serverTick)
        {
            _tipBeforeReconcile = _rigidbody.position;
        }

        /// <summary>
        /// How far the host's correction moved the body's latest predicted position, once the
        /// unconfirmed inputs have been replayed on top of it: the snap the player would see.
        /// FishNet runs the replay between its pre- and post-reconcile events.
        /// </summary>
        void MeasureCorrection(uint clientTick, uint serverTick)
        {
            float metres = Vector3.Distance(_tipBeforeReconcile, _rigidbody.position);
            if (metres < CorrectionNoticeMetres)
                return;

            LastCorrectionMetres = metres;
            CorrectionCount++;
        }

        /// <summary>
        /// Ticks of a body this machine doesn't control that carry no real input: on other
        /// clients, the few ticks FishNet runs past the last input that has arrived; on the host,
        /// a client's tick whose input hasn't arrived yet. FishNet hands those over as empty input
        /// (still flagged Ticked, but not Created). Run as empty, a held crouch, lean, slide or
        /// sprint lets go and the body faces north until the next real input or host state puts
        /// it back, which is what other players used to see. Instead the last real input is
        /// repeated, presses left out, for up to <see cref="SpectatorRepeatTicks"/>; after that
        /// the body lets go of everything but keeps its facing.
        /// </summary>
        MoveInput FillMissingInput(MoveInput input, ReplicateState state)
        {
            if (IsOwner || (IsServerStarted && !HostFillsMissingInput))
                return input;

            if (state.ContainsCreated())
            {
                _lastRealInput = input;
                return input;
            }

            MoveInput guess = _lastRealInput;
            // Signed: a replay can reach a tick older than the newest real input already stored.
            long sinceReal = (long)input.GetTick() - _lastRealInput.GetTick();
            if (sinceReal > SpectatorRepeatTicks)
                guess = new MoveInput { Yaw = _lastRealInput.Yaw };
            guess.OneShots.Clear();
            guess.SetTick(input.GetTick());
            return guess;
        }

        // ---- States --------------------------------------------------------------------------

        void GroundedStep(MoveInput input, float dt)
        {
            if (!_ground.Found)
            {
                Enter(MoveState.Airborne);
                AirborneStep(input, dt);
                return;
            }

            _jump.Coyote = TicksFor(_profile.CoyoteTime);
            Vector3 planar = Planar(_rigidbody.linearVelocity);
            float speedBefore = planar.magnitude;

            // Before the floor gets to grip: a hop on the landing tick keeps every bit of speed.
            bool pressed = _jump.Buffer > 0;
            if (pressed || (_profile.HoldToHop && input.JumpHeld))
            {
                // Space at something climbable climbs it; at anything else, it jumps.
                if (!TryStartMantle(input, pressed))
                    Jump(planar);
                return;
            }

            if (!SlideStep(input, ref planar, dt))
                planar = Run(input, planar, dt);
            _body.Velocity(AlongGround(CapOwnSpeed(planar, speedBefore), dt));
            // Hold still on a slope: nothing else stops a frictionless body creeping down the stairs.
            _body.AddForce(-Physics.gravity, ForceMode.Acceleration);
        }

        /// <summary>The floor grips, then the held direction pushes: stop when nothing is held, turn sharply when something is.</summary>
        Vector3 Run(MoveInput input, Vector3 planar, float dt)
        {
            float speed = planar.magnitude;
            if (speed > 0f)
            {
                float drop = Mathf.Max(speed, _profile.StopSpeed) * _profile.GroundFriction * dt;
                planar *= Mathf.Max(speed - drop, 0f) / speed;
            }

            Vector3 wish = WishDirection(input);
            planar = TurnAlongGround(planar, wish, dt);
            return Accelerate(planar, wish, WishSpeed(input), _profile.GroundAcceleration, dt);
        }

        /// <summary>
        /// Holds what the player's own movement can build to the profile's top speed. A body
        /// already going faster (thrown by a blast) keeps that speed and only loses it to grip.
        /// </summary>
        Vector3 CapOwnSpeed(Vector3 planar, float speedBefore)
        {
            return Vector3.ClampMagnitude(planar, Mathf.Max(speedBefore, _profile.MaxSpeed));
        }

        /// <summary>
        /// Swings the floor speed toward the held direction without losing any of it, at the
        /// profile's turn rate, so a sprint holds its speed round the spiral stairs and other
        /// tight curves. A reversal (more than 100° away) is left to acceleration, which brakes.
        /// </summary>
        Vector3 TurnAlongGround(Vector3 planar, Vector3 wish, float dt)
        {
            if (_profile.GroundTurnRate <= 0f || wish.sqrMagnitude < 0.01f || planar.sqrMagnitude < 0.01f)
                return planar;

            if (Vector3.Angle(planar, wish) > MaxGroundTurnAngle)
                return planar;

            return Vector3.RotateTowards(planar, wish.normalized * planar.magnitude, _profile.GroundTurnRate * Mathf.Deg2Rad * dt, 0f);
        }

        /// <summary>
        /// Adds speed along the held direction, but only up to the wanted speed measured along
        /// that direction. Speed across it is left alone, which is the whole of momentum
        /// movement: turning while pushing sideways keeps adding, pushing forward does not.
        /// </summary>
        static Vector3 Accelerate(Vector3 planar, Vector3 wish, float wishSpeed, float acceleration, float dt)
        {
            float push = wish.magnitude;
            if (push < 0.01f)
                return planar;

            Vector3 direction = wish / push;
            float room = wishSpeed * Mathf.Min(push, 1f) - Vector3.Dot(planar, direction);
            return room > 0f ? planar + direction * Mathf.Min(acceleration * dt, room) : planar;
        }

        /// <summary>
        /// The floor speed the player asked for, tilted to run along the slope under the feet
        /// rather than into or off it, and pulled down onto a floor that has dropped away.
        /// </summary>
        Vector3 AlongGround(Vector3 planar, float dt)
        {
            Vector3 normal = _ground.Normal;
            float climb = -(normal.x * planar.x + normal.z * planar.z) / normal.y;
            float snap = _ground.Gap > SnapSlack ? Mathf.Min(_ground.Gap / dt, MaxSnapSpeed) : 0f;
            return new Vector3(planar.x, climb - snap, planar.z);
        }

        /// <summary>Launches at full jump speed, keeping the planar speed the player already has.</summary>
        void Jump(Vector3 planar)
        {
            Enter(MoveState.Airborne);
            _jump = new JumpTimers { Rising = true };
            _counters.Jumps++;
            _body.Velocity(new Vector3(planar.x, JumpSpeed(), planar.z));
        }

        void AirborneStep(MoveInput input, float dt)
        {
            Vector3 velocity = _rigidbody.linearVelocity;
            if (_ground.Found && velocity.y <= 0f)
            {
                Enter(MoveState.Grounded);
                GroundedStep(input, dt);
                return;
            }

            // Coyote time: just walked off an edge, so a jump still counts as from the floor.
            if (_jump.Buffer > 0 && _jump.Coyote > 0)
            {
                Jump(Planar(velocity));
                return;
            }

            if (_jump.Coyote > 0)
                _jump.Coyote--;

            if (TryStartMantle(input, false))
                return;

            Vector3 before = Planar(velocity);
            Vector3 planar = CapOwnSpeed(Accelerate(before, WishDirection(input), _profile.AirStrafeSpeed, _profile.AirAcceleration, dt), before.magnitude);

            float vertical = CutJumpOnRelease(velocity.y, input.JumpHeld);
            _body.Velocity(new Vector3(planar.x, ClampFall(vertical), planar.z));
            AddExtraGravity(vertical > 0f ? _profile.AirGravityMultiplier : _profile.FallGravityMultiplier);
        }

        /// <summary>Letting go of jump while still rising keeps only part of the upward speed: a tap hops, a hold leaps. Once per jump.</summary>
        float CutJumpOnRelease(float vertical, bool jumpHeld)
        {
            if (!_jump.Rising)
                return vertical;

            if (vertical <= 0f)
            {
                _jump.Rising = false;
                return vertical;
            }

            if (jumpHeld)
                return vertical;

            _jump.Rising = false;
            return vertical * _profile.JumpReleaseKeep;
        }

        void KnockedStep(float dt)
        {
            Vector3 velocity = _rigidbody.linearVelocity;
            if (_ground.Found)
            {
                Vector3 planar = Vector3.MoveTowards(Planar(velocity), Vector3.zero, _profile.KnockedBraking * dt);
                _body.Velocity(new Vector3(planar.x, velocity.y, planar.z));
            }
            else
            {
                _body.Velocity(new Vector3(velocity.x, ClampFall(velocity.y), velocity.z));
                AddExtraGravity(_profile.AirGravityMultiplier);
            }

            if (_stateTicksLeft > 0)
                _stateTicksLeft--;
            if (_stateTicksLeft == 0)
                Enter(_ground.Found ? MoveState.Grounded : MoveState.Airborne);
        }

        /// <summary>
        /// Throws the player and takes their control away for a while. Host only: the host
        /// is the sole judge of hits, and the result reaches everyone through the reconcile.
        /// The throw is held until this body's own tick applies it: a blast usually lands
        /// during another player's tick, and any force queued then would be cleared by this
        /// body's next velocity write before physics ever saw it. A climb in progress is dropped.
        /// </summary>
        public void ApplyKnockback(Vector3 velocityChange, float controlLossSeconds)
        {
            if (!IsServerStarted)
                return;

            _pendingKnock += velocityChange;
            Enter(MoveState.Knocked);
            _stateTicksLeft = System.Math.Max(1u, TimeManager.TimeToTicks(controlLossSeconds, TickRounding.RoundUp));
        }

        /// <summary>Last thing before the physics step, so no velocity write this tick can clear it.</summary>
        void ApplyPendingKnock()
        {
            if (_pendingKnock == Vector3.zero)
                return;

            _body.AddForce(_pendingKnock, ForceMode.VelocityChange);
            _pendingKnock = Vector3.zero;
        }

        // ---- Lean and crouch -----------------------------------------------------------------

        /// <summary>Leans toward the held side at the profile pace; only on the floor, so a jump or a blast straightens you up.</summary>
        void LeanStep(MoveInput input, float dt)
        {
            float target = _state == MoveState.Grounded ? Mathf.Clamp(input.Lean, -1, 1) : 0f;
            _lean = Mathf.MoveTowards(_lean, target, _profile.LeanSpeed * dt);
        }

        /// <summary>
        /// Ducks while crouch is held, on the floor or in the air, and stands again when it is
        /// let go and there is headroom. A climb or a blast stands the player up.
        /// </summary>
        void CrouchStep(MoveInput input, float dt)
        {
            bool inControl = _state == MoveState.Grounded || _state == MoveState.Airborne;
            float target = input.Crouch && inControl ? 1f : 0f;
            if (target < _crouch && !HeadroomToStand())
                return;

            if (target == _crouch)
                return;

            _crouch = Mathf.MoveTowards(_crouch, target, _profile.CrouchRate * dt);
            ApplyCrouchToCapsule();
        }

        bool HeadroomToStand()
        {
            float radius = _capsule.radius;
            Vector3 feet = _rigidbody.position;
            Vector3 low = feet + Vector3.up * (radius + ProbeInset);
            Vector3 high = feet + Vector3.up * (_standHeight - radius);
            return !Physics.CheckCapsule(low, high, radius - ProbeInset, _groundMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>The collider shrinks from the top, feet staying put. The prefab's own height is kept as the standing height and never written.</summary>
        void ApplyCrouchToCapsule()
        {
            float height = Mathf.Lerp(_standHeight, CrouchedHeight, _crouch);
            if (Mathf.Approximately(_capsule.height, height))
                return;

            _capsule.height = height;
            _capsule.center = new Vector3(0f, height * 0.5f, 0f);
        }

        /// <summary>
        /// Where shots should leave from: the eye, slid sideways by the current lean but never
        /// through a wall. <paramref name="yaw"/> is the facing, in degrees around the vertical.
        /// </summary>
        public Vector3 LeanedEyePosition(float yaw)
        {
            Vector3 head = _rigidbody.position + Vector3.up * EyeHeight;
            float reach = _profile.LeanDistance * Mathf.Abs(_lean);
            if (reach < 0.001f)
                return head;

            Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * Mathf.Sign(_lean);
            if (Physics.SphereCast(head, LeanShotRadius, direction, out RaycastHit wall, reach, _groundMask, QueryTriggerInteraction.Ignore))
                reach = wall.distance;

            return head + direction * reach;
        }

        // ---- Helpers -------------------------------------------------------------------------

        /// <summary>The floor under the feet within the reach, if it is flat enough to stand on.</summary>
        GroundHit ProbeGround(float reach)
        {
            float radius = _capsule.radius - ProbeInset;
            Vector3 origin = _rigidbody.position + Vector3.up * (_capsule.radius + ProbeInset);

            if (!Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, reach + ProbeInset * 3f, _groundMask, QueryTriggerInteraction.Ignore))
                return default;

            if (Vector3.Angle(hit.normal, Vector3.up) > GroundSlopeLimit)
                return default;

            // The probe is a slightly smaller ball started slightly higher than the body's own
            // foot; on a slope it meets the floor later than the foot would, by this much.
            float gap = hit.distance - ProbeInset * (1f + 1f / hit.normal.y);
            if (gap > reach)
                return default;

            return new GroundHit { Found = true, Normal = hit.normal, Gap = gap };
        }

        Vector3 WishDirection(MoveInput input)
        {
            Vector3 planar = Quaternion.Euler(0f, input.Yaw, 0f) * new Vector3(input.Move.x, 0f, input.Move.y);
            return planar.sqrMagnitude > 1f ? planar.normalized : planar;
        }

        float WishSpeed(MoveInput input)
        {
            return _crouch > 0.5f ? _profile.CrouchSpeed : input.Sprint ? _profile.SprintSpeed : _profile.WalkSpeed;
        }

        float JumpSpeed()
        {
            return Mathf.Sqrt(2f * -Physics.gravity.y * _profile.AirGravityMultiplier * _profile.JumpHeight);
        }

        float ClampFall(float verticalSpeed)
        {
            return _profile.MaxFallSpeed > 0f ? Mathf.Max(verticalSpeed, -_profile.MaxFallSpeed) : verticalSpeed;
        }

        /// <summary>Tops Unity's own gravity up to the given multiple of it.</summary>
        void AddExtraGravity(float multiplier)
        {
            if (multiplier > 1f)
                _body.AddForce(Physics.gravity * (multiplier - 1f), ForceMode.Acceleration);
        }

        byte TicksFor(float seconds)
        {
            if (seconds <= 0f)
                return 0;

            return (byte)System.Math.Min(TimeManager.TimeToTicks(seconds, TickRounding.RoundUp), 254u);
        }

        void Enter(MoveState next)
        {
            _state = next;
            _stateTicksLeft = 0;
            _mantle = default;
            _slide.Active = false;
            _jump.Rising = false;
            // Only walking off an edge keeps coyote time; a blast or a climb never hands out a free jump.
            if (next != MoveState.Airborne)
                _jump.Coyote = 0;
        }

        static Vector3 Planar(Vector3 v) => new(v.x, 0f, v.z);
    }
}
