// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-12
//  Prompt    : Alex: "set up a humanoid procedural animation system" (built-in, no clips), then
//              three rounds of feel notes ending "too stiff, transitions are harsh, i dont like
//              the idle, the jump is weird, arms still janky ... the video i sent is kinda what
//              im looking for, but that more airy top heavy loose feel"
//  Modified  : Claude Fable 5.2, 2026-11-12 — plays animation clips (idle, an eight-way run,
//              sprint, air, landing) as the pose when the profile has them; Alex chose clips
//              after the hand-written poses still read as "off". The procedural pose is kept
//              as the fallback for a profile with an empty clip slot.
//              2026-11-13 — breathing added on top of the idle clip, and run directions
//              cross-fade instead of switching (Alex: "more prominent breathing", sideways
//              legs "really chaotic and wobbly")
//              2026-11-13 — standing, the head turns first and the body follows past a
//              threshold (Alex: "move the head first, then body, like a threshold of rotation")
//              2026-11-13 — idle fidgets: now and then, standing, he scratches, yawns or
//              looks about (Alex: "more animations in the idle ... to give it more life")
//              2026-11-13 — the motor is optional, so a body with no player behind it (the
//              hit-reaction test dummy) can stand, breathe and fidget with the same component
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Poses the player's humanoid skeleton every frame from how the body is moving,
//              with no animation clips. Its own component, not part of PlayerMotor, because it
//              is purely visual: the motor is predicted and host-judged, and nothing here may
//              feed back into it or be sent over the network (AGENTS.md §3).
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Decides the pose the player's body is trying to hold each frame, from the body's measured
    /// movement and the motor's state: blended animation clips when the profile has them, or a
    /// procedural pose (a bouncy run with a flight phase, an upright idle, an arms-up jump) when
    /// it does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ownership (AGENTS.md §3):</b> local to each client and synced by nothing. Every client
    /// derives the pose from what it already has: where its smoothed copy of the player moved
    /// this frame, and the motor's state, which the reconcile carries. Two clients may show a
    /// limb in slightly different places; nothing reads the pose back, so that cannot desync.
    /// </para>
    /// <para>
    /// <b>Clips:</b> a playable graph blends idle, a run in eight directions (the body faces
    /// forward while the feet travel any way, as a first-person player's do), a forward sprint,
    /// an air pose and a landing, straight onto the model's Animator. It needs no Animator
    /// Controller and no parameters. This component sets every clip's time itself and evaluates
    /// the graph by hand, so the legs turn over at the rate that matches the real speed across
    /// the floor, every run clip is at the same point of its stride, and the pose can be played
    /// back at a fixed step outside Play Mode. WHY not an Animator Controller with a blend tree:
    /// it evaluates on Unity's schedule, before FishNet has moved the smoothed visual for the
    /// frame, and its state lives in an asset a rebuilt prefab would have to be wired to.
    /// </para>
    /// <para>
    /// <b>The procedural pose (fallback):</b> bones are found once through the model's humanoid avatar, so any
    /// humanoid rig works. Each frame the spine, head and arms are turned directly and each leg
    /// is solved from a foot target by a two-bone solver. Rotations are offsets from the rest
    /// pose captured in <c>Start</c>, so the model must spawn in its T-pose.
    /// </para>
    /// <para>
    /// <b>The run</b> is modelled on the reference video "Full Procedural Animations System":
    /// hips low, each foot on the floor for less than half the stride so both are off it for a
    /// moment, the heel kicking up behind after push-off, elbows bent and pumping.
    /// </para>
    /// <para>
    /// <b>What shows on screen:</b> not this pose directly. <see cref="PlayerRagdoll"/> runs
    /// straight after and treats it as a target for a body of rigidbodies and joints, then
    /// shows that body. This component only decides what the body is trying to do.
    /// </para>
    /// <para>
    /// <b>Alternatives that lost:</b> Unity's IK pass (<c>OnAnimatorIK</c>) only runs while an
    /// Animator Controller plays a clip. The Animation Rigging package is a new dependency.
    /// Clips were first turned down in favour of staying clip-free, then chosen after all.
    /// </para>
    /// <para>
    /// Runs in <c>LateUpdate</c> so it poses after FishNet has moved the smoothed visual for the
    /// frame. Allocates nothing per frame. Mantling players get the airborne pose for now.
    /// </para>
    /// <para>
    /// <b>Where it goes:</b> on the player's smoothed visual (the <c>Graphics</c> object), above
    /// the lean pivot. Its own movement is what the feet are matched to, so on the pivot or the
    /// model a lean's sideways slide would be read as running.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        /// <summary>
        /// Runs after default-order scripts, so the motor's lean tilt for this frame is already
        /// on the pivot above the model. WHY 100: any positive value works; 100 leaves room for
        /// other late visuals to be placed before or after it.
        /// </summary>
        const int ExecutionOrder = 100;

        /// <summary>
        /// Legs never straighten past this share of their full length. WHY 0.999: at exactly
        /// full length the knee has no bend direction left and flips from frame to frame.
        /// </summary>
        const float MaxLegExtension = 0.999f;

        /// <summary>
        /// A measured speed above this, metres per second, is a teleport (a spawn or a large
        /// correction), not movement, and is ignored. WHY 60: twice the 30 m/s fall cap, the
        /// fastest the motor moves a body on purpose.
        /// </summary>
        const float MaxSaneSpeed = 60f;

        /// <summary>Below this speed, metres per second, the direction of travel is too noisy to use and the last good one is kept.</summary>
        const float DirectionMinSpeed = 0.1f;

        /// <summary>
        /// Longest step the springs are advanced by at once, seconds. WHY 1/30: stepped further
        /// than that at the profile's highest spring speed a spring gains energy and blows up, so
        /// a long frame is treated as a 30 fps one.
        /// </summary>
        const float MaxSpringStep = 1f / 30f;

        /// <summary>
        /// Share of the torso's lean the hips, the lowest spine bone and the next one up take;
        /// the top one takes all of it. WHY uneven: an even bend reads as a stiff plank tipping
        /// from the hips. Most of the bend high up makes the chest and head whip over a steadier
        /// waist, which is what a top-heavy body does. Picked by eye.
        /// </summary>
        const float HipsLeanShare = 0.25f, SpineShare = 0.3f, ChestShare = 0.6f;

        /// <summary>
        /// How much slower than the torso the head wobbles. WHY below 1: the head hangs furthest
        /// from the hips, so it should arrive last and ring longest; at the same speed the whole
        /// upper body sways as one block. Picked by eye.
        /// </summary>
        const float HeadSpringRatio = 0.6f;

        /// <summary>Share of the chest's side-to-side rock and bank the hips, and the head, take. Picked by eye.</summary>
        const float HipsRockShare = 0.5f, HeadRockShare = 0.4f;

        /// <summary>
        /// Limits on what the springs may add: torso twist from turning, bank and total lean, in
        /// degrees, and how far the hips may slide over the feet or sink while stopping, in
        /// metres. WHY: a network correction or a flick of the mouse would otherwise fold the body
        /// inside out or pull the hips off the legs.
        /// </summary>
        const float MaxTurnTwist = 25f, MaxBank = 14f, MaxLean = 60f, MaxCarry = 0.2f, MaxStopCrouch = 0.16f;

        /// <summary>How far behind straight down the middle of the arm swing sits, degrees. WHY: swinging evenly either side of straight down, the forward hand comes up over the face; a runner's elbows work mostly behind the ribs. Read off the reference's run frames.</summary>
        const float ArmSwingSetBack = 10f;

        /// <summary>How far the lean is pulled from the springy velocity toward the real one while the body is setting off, 0–1. Picked by eye from the start-stop strip.</summary>
        const float StartLead = 0.7f;

        /// <summary>Share of the head's wandering look that goes into nodding rather than turning. Picked by eye.</summary>
        const float LookPitchShare = 0.4f;

        /// <summary>Where along the second axis of Unity's noise each arm's standing drift is read, away from the head's look (row 0) and from each other, so the three do not move together. Any other values would do.</summary>
        const float LeftArmNoiseRow = 5.3f, RightArmNoiseRow = 9.1f;

        /// <summary>How much of the chest's nod at each footfall the head tips back against, so the chin stays up while the chest bobs under it. Picked by eye.</summary>
        const float HeadCounterNod = 0.6f;

        /// <summary>
        /// Slowest the legs turn over, as a share of their full cadence, while the stride is
        /// still small. WHY not 0: legs that slow to a crawl as the body eases off look like a
        /// wind-down toy; quick small steps look like a body catching its balance.
        /// </summary>
        const float SlowestCadence = 0.65f;

        /// <summary>
        /// How narrow the feet's track is while running, as a share of their standing width.
        /// WHY: a runner's feet land nearer the middle of the body than they stand. WHY not much
        /// nearer: at 0.55, tried first, the legs crossed over each other seen from the front.
        /// </summary>
        const float RunTrackWidth = 0.85f;

        /// <summary>
        /// Shapes the heel kick so it peaks early in the swing, while the foot is still behind
        /// the body. WHY 0.65: at 1 the foot would be highest as it passes under the hips, which
        /// is a march, not a kick. Read off the reference's run frames.
        /// </summary>
        const float KickTiming = 0.65f;

        /// <summary>Where in the time a foot is on the floor the heel starts to lift for push-off, 0–1. Read off the reference's run frames.</summary>
        const float RollStart = 0.5f;

        /// <summary>
        /// The air pose's asymmetry: how much of the tuck the trailing leg takes, how far back
        /// it hangs as a share of the leading leg's forward hang, how much of the tuck the legs
        /// keep on the way up, and how far the toes point down, degrees. WHY uneven: two legs in
        /// the same pose read as a mannequin dropped off a shelf. Picked by eye.
        /// </summary>
        const float TrailingTuck = 0.35f, TrailingSplit = 0.9f, RisingTuck = 0.4f, AirToePoint = 40f;

        /// <summary>Share of the full air arm raise the arms reach on the way up; they reach all of it once the body is falling. Picked by eye from the jump strip.</summary>
        const float RisingArmRaise = 0.7f;

        /// <summary>Vertical speeds, metres per second, between which the legs go from hanging (rising) to tucked (falling). Picked by eye.</summary>
        const float RisingSpeed = 3f, FallingSpeed = -3f;

        /// <summary>How quickly a landing's bounce dies away: 0 = bounces for ever, 1 = no rebound. WHY 0.75: it sinks and comes straight back up with almost no rebound; lower values read as wobble.</summary>
        const float LandDamping = 0.75f;

        /// <summary>How far above standing height a landing's rebound may lift the hips, metres. WHY small: the legs are already nearly straight.</summary>
        const float MaxRebound = 0.03f;

        /// <summary>
        /// While knocked back the body leans this many times its usual amount, the other way
        /// (the head trails the flight instead of leading it), and the arms are thrown up this
        /// share of the way to the jump pose. Picked by eye.
        /// </summary>
        const float KnockedTrail = 1.5f, KnockedArmRaise = 0.7f;

        /// <summary>Where each clip plugs into the mixer: idle, then the eight run directions, then sprint, air and landing.</summary>
        const int IdleInput = 0, RunInput = 1, SprintInput = RunInput + AnimationProfile.RunDirections,
            AirInput = SprintInput + 1, LandInput = AirInput + 1, FidgetInput = LandInput + 1, InputCount = FidgetInput + 1;

        /// <summary>
        /// How long a fidget takes to blend in, and to blend out when the player moves off in
        /// the middle of one, seconds. WHY short: every fidget clip starts and ends on the idle
        /// pose, so the blend only has to hide being cut off early.
        /// </summary>
        const float FidgetBlendTime = 0.2f;

        /// <summary>
        /// How still the body has to be for a fidget to start or carry on: no more than this
        /// much stride or air. WHY the same as <see cref="StandingStride"/>: a fidget mixed into
        /// the first step of a run would drag an arm through it.
        /// </summary>
        const float FidgetStillness = StandingStride;

        /// <summary>Where the forward-going clips and one sideways clip sit in the profile's eight run directions (0 = forward, clockwise). The five sideways and backward clips are all one length, so any one of them gives it.</summary>
        const int ForwardDirection = 0, ForwardRightDirection = 1, SideDirection = 2, ForwardLeftDirection = 7;

        /// <summary>Degrees between one run direction and the next.</summary>
        const float RunDirectionStep = 360f / AnimationProfile.RunDirections;

        /// <summary>
        /// Below this much of a stride the body counts as standing, and the stride cycle is put
        /// back to its start. WHY: every set-off then begins with the same first step instead of
        /// wherever the legs happened to be left by the last run. At 2% of a run nothing shows.
        /// </summary>
        const float StandingStride = 0.02f;

        /// <summary>
        /// How long the legs take to change from one run direction's clip to another's, seconds.
        /// WHY: the direction of travel can flip in a frame (left to right on the keys). Switched
        /// at once, the legs jump from one clip's pose to an unrelated one every reversal, which
        /// is most of what read as chaotic sideways. About an eighth of a second is one footfall
        /// at running cadence; longer and the feet visibly keep stepping the old way.
        /// </summary>
        const float RunDirectionBlendTime = 0.12f;

        /// <summary>
        /// How far past the halfway line between two run directions the travel has to swing
        /// before the legs change clip, degrees. WHY the legs snap to one of the eight clips at
        /// all, instead of mixing the two nearest by angle as this first did: the sideways and
        /// backward clips are not in step with each other or with the forward ones, so a mix of
        /// two is a pose neither has, and the measured direction of travel wanders a few degrees
        /// every time the camera turns. That constant re-mixing was the wobble. WHY 10: wider
        /// than that wander, narrower than the 22.5 degrees to the next clip's own middle.
        /// </summary>
        const float RunDirectionSlack = 10f;

        /// <summary>
        /// How long the body keeps its stride after the speed drops, seconds, before it starts
        /// easing toward the stance. WHY: reversing direction takes the speed through zero for
        /// about a fifth of a second (brake at 60 m/s², set off at 45). Without this the pose
        /// went 57% of the way to the standing stance and back on every left-right flip; with it, 12%
        /// (measured in the flip preview), which was the snap. The price is that a real stop
        /// begins to settle this much later.
        /// </summary>
        const float StrideHoldTime = 0.2f;

        /// <summary>
        /// While the body is turned away from where the player is looking, the share of that
        /// angle the upper chest turns back toward the look; the head makes up the rest, so the
        /// face is always on the look. WHY 0.35: a head turned 50 degrees on square shoulders
        /// reads as an owl; a real look round takes the shoulders a third of the way. Picked by eye.
        /// </summary>
        const float LookChestShare = 0.35f;

        /// <summary>
        /// Below this much stride and this much air the body counts as standing, and may lag
        /// the look. Moving or airborne, it always turns to face the look. WHY 0.1: the run
        /// clips are steered by the look direction, so a body left turned would run crab-wise;
        /// a tenth of a stride is before the legs visibly move.
        /// </summary>
        const float StandingToLook = 0.1f;

        /// <summary>
        /// How a breath is shared out. Each of the two upper spine bones takes half the chest's
        /// tip; the shoulders rise by this share of it; the head tips back down by all of it so
        /// the face stays level. Picked by eye.
        /// </summary>
        const float BreathSpineShare = 0.5f, BreathShoulderShare = 1f;

        /// <summary>
        /// How much faster than recorded the landing clip plays. WHY 1.6: the clip takes 1.27 s
        /// to stand back up, which suits a heavy jump; this game's movement is quick, and at 1.6
        /// the crouch bottoms out about 0.17 s after touchdown. Picked from the clip's frames.
        /// </summary>
        const float LandClipRate = 1.6f;

        /// <summary>Where in the landing clip it starts to hand back to standing or running, 0–1. WHY 0.3: just past the bottom of its crouch (frame 8 of 38, sped up), so the body comes up into whatever it is doing next.</summary>
        const float LandFadeStart = 0.3f;

        /// <summary>How much of a landing shows while running at full stride, as a share of the same landing standing still. WHY below 1: the landing clip plants both feet, which slides them along the floor at running speed.</summary>
        const float RunningLandShare = 0.4f;

        [Header("References")]
        [Tooltip("The clips, and the tuning for the ragdoll and the procedural pose. Read at runtime, never written.")]
        [SerializeField] AnimationProfile _profile;

        [Tooltip("The player's motor: says whether the player is on the floor, knocked back or climbing, and how fast walking and sprinting are. Leave empty for a body nobody is playing (a test dummy): it then always counts as standing on the floor, never knocked back, and never strides.")]
        [SerializeField] PlayerMotor _motor;

        /// <summary>True when a motor was assigned at spawn. Checked once: asking Unity whether an object reference is alive costs more than a bool, and this is read every frame.</summary>
        bool _hasMotor;

        [Tooltip("The character model's Animator. Its humanoid avatar is used once, at spawn, to find the skeleton's bones; the clips are then played onto it. It needs no controller.")]
        [SerializeField] Animator _avatar;

        /// <summary>A bone and the rotation it had in the T-pose, relative to the model root.</summary>
        struct Bone
        {
            public Transform T;
            public Quaternion Rest;
        }

        /// <summary>A value that chases a target like a weight on a spring: it lags, overshoots and settles.</summary>
        struct Spring
        {
            public Vector3 Value, Rate;

            /// <summary>Moves the value one step toward the target.</summary>
            /// <param name="target">What to chase.</param>
            /// <param name="hz">Wobbles per second.</param>
            /// <param name="damping">0 = rings for ever, 1 = no overshoot.</param>
            /// <param name="dt">Step, seconds; the caller keeps it at or below <see cref="MaxSpringStep"/>.</param>
            public void Step(Vector3 target, float hz, float damping, float dt)
            {
                float omega = 2f * Mathf.PI * hz;
                Rate += (omega * omega * (target - Value) - 2f * damping * omega * Rate) * dt;
                Value += Rate * dt;
            }
        }

        Transform _model;
        Bone _hips, _spine, _chest, _upperChest, _neck, _head;
        /// <summary>The collar bones, which lift with each breath. Optional in a humanoid rig; null transforms if this one has none.</summary>
        Bone _leftShoulder, _rightShoulder;
        Bone _leftUpperArm, _leftLowerArm, _rightUpperArm, _rightLowerArm;
        Bone _leftUpperLeg, _leftLowerLeg, _leftFoot, _rightUpperLeg, _rightLowerLeg, _rightFoot;

        /// <summary>Hips position in the T-pose, relative to the model root, metres.</summary>
        Vector3 _hipsRest;
        /// <summary>Ankle positions in the T-pose, relative to the model root, metres.</summary>
        Vector3 _leftFootRest, _rightFootRest;

        Vector3 _lastPosition;
        float _lastYaw;
        /// <summary>This frame's and last frame's unsmoothed speed across the floor, metres per second; a sudden drop between them is an impact.</summary>
        float _groundSpeed, _lastGroundSpeed;
        /// <summary>Smoothed world velocity of the visual, metres per second.</summary>
        Vector3 _velocity;
        /// <summary>Velocity relative to the model, metres per second: x right, y up, z forward.</summary>
        Vector3 _local;
        /// <summary>How fast the body is turning, degrees per second, positive to the right.</summary>
        float _yawRate;
        /// <summary>
        /// Springy copies of <see cref="_local"/> for the torso and the head, the head slower.
        /// The body leans by these instead of by the real velocity, which is what makes it sway,
        /// overshoot and settle.
        /// </summary>
        Spring _torso, _headSpring;
        /// <summary>A springy copy of <see cref="_yawRate"/>, in x; the upper body turns by this, so it trails a turn and swings past.</summary>
        Spring _turn;
        /// <summary>How far a landing has sunk the hips, metres, in x. Kicked on touchdown, then it springs back to 0.</summary>
        Spring _land;
        /// <summary>Direction of travel across the floor, relative to the model, unit length.</summary>
        Vector3 _travel = Vector3.forward;
        /// <summary>0–1 through one left stride plus one right stride.</summary>
        float _phase;
        /// <summary>0 = standing, 1 = running at full stride. Eased.</summary>
        float _moveWeight;
        float _moveWeightRate;
        /// <summary>0 = on the floor, 1 = airborne. Eased.</summary>
        float _airWeight;
        float _airWeightRate;
        /// <summary>0–1: how far into the knocked-back, limp body the pose is. Eased.</summary>
        float _knocked;
        float _knockedRate;
        /// <summary>0–1: how hard the body just ran into something. Fades back to 0.</summary>
        float _stagger;
        /// <summary>The fastest the body was falling during the current time in the air, metres per second, for sizing the landing.</summary>
        float _fallSpeed;
        bool _wasOnFloor = true;
        /// <summary>Seconds since spawn, for the breathing, the standing sway and the wandering look, and for the clips that are not tied to the stride.</summary>
        float _time;

        /// <summary>Blends the clips onto the model's Animator. Only valid while <see cref="_usesClips"/>.</summary>
        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        /// <summary>One playable per mixer input, in input order. Their clocks are stopped; this component sets each one's time every frame.</summary>
        readonly AnimationClipPlayable[] _clips = new AnimationClipPlayable[InputCount];
        /// <summary>Each clip's length, seconds, in input order.</summary>
        readonly float[] _clipLengths = new float[InputCount];
        /// <summary>True when the profile had every clip at spawn; false = the procedural pose.</summary>
        bool _usesClips;
        /// <summary>Seconds since the last touchdown, and how much of the landing clip that touchdown earned, 0–1.</summary>
        float _landTime, _landStrength;
        /// <summary>One playable per fidget clip on the profile, made once; only the one being played is plugged into the mixer. Null entries where the profile has an empty slot.</summary>
        AnimationClipPlayable[] _fidgets;
        /// <summary>Which fidget is plugged in, or -1 for none.</summary>
        int _fidget = -1;
        /// <summary>How far into the current fidget, seconds; how much of it shows, 0–1; and how long until the next one may start, seconds.</summary>
        float _fidgetTime, _fidgetWeight, _fidgetWait;
        /// <summary>How much of the run each direction's clip is showing, 0–1, summing to 1. Eased toward the direction of travel; starts as all forward.</summary>
        readonly float[] _runShares = { 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
        /// <summary>How far the body is turned from where the player is looking, degrees, positive to the right of the look. 0 once it has caught up.</summary>
        float _bodyBehind;
        /// <summary>Where the player was looking last frame, degrees round the compass.</summary>
        float _lookYaw;
        /// <summary>True from the moment the look passes the head's limit until the body has turned all the way to it.</summary>
        bool _bodyTurning;
        bool _lookStarted;
        /// <summary>Which of the eight run clips the legs are on, 0 = forward, clockwise.</summary>
        int _runDirection;
        /// <summary>The stride the body is holding on to while the speed dips, 0–1, and for how long it has been dipping, seconds.</summary>
        float _heldStride, _strideDip;
        /// <summary>Every bone of the skeleton, hips first, and where each sat relative to its parent in the T-pose. See <see cref="PoseFromClips"/> for why they are put back each frame.</summary>
        Transform[] _skeleton;
        Vector3[] _skeletonRestPositions;
        Quaternion[] _skeletonRestRotations;

        /// <summary>0–1: how limp the body should be right now. 1 while knocked back, and briefly after running into something. <see cref="PlayerRagdoll"/> weakens its joints by this.</summary>
        public float Limp => Mathf.Max(_knocked, _stagger);

        /// <summary>0–1: how far into the airborne pose the body is. <see cref="PlayerRagdoll"/> braces its joints by this, so a jump holds its shape.</summary>
        public float Airborne => _airWeight;

        /// <summary>
        /// WHY Start and not Awake: the Animator has to have set itself up before it can be asked
        /// for bones, and Unity gives no order between two components' Awake calls.
        /// </summary>
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

            // The graph itself is made on the first frame; see BuildClipGraph for why.
            _usesClips = _profile.HasClips;
        }

        void OnDestroy()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }

        /// <summary>
        /// Makes the graph that blends the profile's clips onto the model's Animator.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The graph's own clock is off (manual update) and every clip's speed is 0: nothing
        /// advances unless <see cref="PoseFromClips"/> says so.
        /// </para>
        /// <para>
        /// WHY the humanoid avatar is taken off the Animator here: the clips are plain bone
        /// animation, already fitted to this exact skeleton in Blender, and are meant to be
        /// written to the bones as they are. With a humanoid avatar on the Animator, Unity passes
        /// them through its humanoid maths, and this model's origin is at its hips, not its feet,
        /// which that maths does not expect: measured, it threw the hips metres off and bent the
        /// arms wrongly. The avatar's only job is finding bones by name at spawn.
        /// </para>
        /// <para>
        /// WHY on the first frame and not in <c>Start</c>: <see cref="PlayerRagdoll"/> also finds
        /// its bones through the avatar in its own <c>Start</c>, which runs after this one's.
        /// </para>
        /// </remarks>
        void BuildClipGraph()
        {
            _avatar.avatar = null;

            _skeleton = _hips.T.GetComponentsInChildren<Transform>(true);
            _skeletonRestPositions = new Vector3[_skeleton.Length];
            _skeletonRestRotations = new Quaternion[_skeleton.Length];
            for (int i = 0; i < _skeleton.Length; i++)
                _skeleton[i].GetLocalPositionAndRotation(out _skeletonRestPositions[i], out _skeletonRestRotations[i]);
            // WHY: the clips are in place and this component never moves the player. Left on,
            // root motion would let the Animator shift the model under the motor.
            _avatar.applyRootMotion = false;
            // WHY: the default (cull update transforms) stops the Animator writing bones while no
            // camera sees the model, and the first-person body is shadow-only. The ragdoll reads
            // the bones as its targets every frame; with nothing written it would read back its
            // own last frame and drift away.
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

        /// <summary>Plugs one clip into the mixer with its clock stopped.</summary>
        void AddClip(int input, AnimationClip clip)
        {
            AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetSpeed(0d);
            _graph.Connect(playable, 0, _mixer, input);
            _clips[input] = playable;
            _clipLengths[input] = clip.length;
        }

        /// <summary>Looks a bone up through the humanoid avatar. Optional bones (chest, upper chest, neck) come back with a null transform.</summary>
        Bone Find(HumanBodyBones id)
        {
            Transform bone = _avatar.GetBoneTransform(id);
            return new Bone
            {
                T = bone,
                Rest = bone != null ? Quaternion.Inverse(_model.rotation) * bone.rotation : Quaternion.identity
            };
        }

        void LateUpdate() => Step(Time.deltaTime);

        /// <summary>
        /// Advances the animation by one frame and poses the skeleton.
        /// </summary>
        /// <param name="dt">Frame time, seconds.</param>
        /// <remarks>Split out from <c>LateUpdate</c> so the animation can be played back at a fixed step outside Play Mode and looked at frame by frame.</remarks>
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

        /// <summary>
        /// Works out how fast and which way the visual moved and turned this frame, and steps
        /// the body's springs.
        /// </summary>
        /// <param name="dt">Frame time, seconds.</param>
        /// <param name="speed">Speed across the floor, metres per second.</param>
        /// <param name="sprint">0 at walking speed or below, 1 at full sprint.</param>
        /// <remarks>
        /// WHY measure the visual instead of reading the rigidbody: on other players' copies the
        /// rigidbody steps once per tick and jumps on corrections, while the visual is what
        /// FishNet smooths and what the feet have to match on screen.
        /// </remarks>
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

            float yaw = _model.eulerAngles.y;
            _yawRate = Mathf.Lerp(_yawRate, Mathf.DeltaAngle(_lastYaw, yaw) / dt, smoothing);
            _lastYaw = yaw;

            _local = _model.InverseTransformDirection(_velocity);
            Vector3 planar = new Vector3(_local.x, 0f, _local.z);
            speed = planar.magnitude;
            if (speed > DirectionMinSpeed)
                _travel = planar / speed;

            float step = Mathf.Min(dt, MaxSpringStep);
            _torso.Step(planar, _profile.WobbleSpeed, _profile.WobbleDamping, step);
            _headSpring.Step(planar, _profile.WobbleSpeed * HeadSpringRatio, _profile.WobbleDamping, step);
            _turn.Step(new Vector3(_yawRate, 0f, 0f), _profile.WobbleSpeed, _profile.WobbleDamping, step);
            _land.Step(Vector3.zero, _profile.LandBounce, LandDamping, step);

            if (!_hasMotor)
            {
                sprint = 0f;
                return;
            }
            MovementProfile movement = _motor.Profile;
            sprint = Mathf.InverseLerp(movement.WalkSpeed, movement.SprintSpeed, speed);
        }

        /// <summary>Advances the stride cycle and the eased pose blends, and reacts to touching down and to running into things.</summary>
        void UpdateWeights(float dt, float speed, float sprint, bool onFloor)
        {
            // WHY SmoothDamp and not a fixed rate: a blend that moves at a constant rate starts
            // and stops dead, and every limb pops at both ends. This eases in and out.
            float ease = _profile.PoseBlendTime;
            // With no motor there is no walking speed to measure a stride against: it stands.
            float walkSpeed = _hasMotor ? _motor.Profile.WalkSpeed : 0f;
            float stride = walkSpeed > 0f ? Mathf.Clamp01(speed / walkSpeed) : 0f;
            // With clips, a dip in speed shorter than the hold time does not shrink the stride.
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
            _moveWeight = Mathf.SmoothDamp(_moveWeight, _heldStride, ref _moveWeightRate, ease, Mathf.Infinity, dt);
            _airWeight = Mathf.SmoothDamp(_airWeight, onFloor ? 0f : 1f, ref _airWeightRate, ease, Mathf.Infinity, dt);
            _knocked = Mathf.SmoothDamp(_knocked, _hasMotor && _motor.State == MoveState.Knocked ? 1f : 0f, ref _knockedRate, ease, Mathf.Infinity, dt);
            _stagger = Mathf.MoveTowards(_stagger, 0f, dt / _profile.StaggerRecover);

            if (_usesClips)
            {
                // The legs turn over at whatever rate makes one cycle of the clip cover the
                // ground the body really covered: the distance the clip's stride was made for,
                // at the real speed. Below a share of walking speed they keep that share's
                // turnover instead of slowing to a crawl. Standing, the cycle waits at its start.
                float ground = Mathf.Lerp(
                    _profile.RunClipSpeed * _clipLengths[RunInput],
                    _profile.SprintClipSpeed * _clipLengths[SprintInput],
                    sprint * Mathf.Clamp01(_travel.z));
                // Sideways and backwards have a step rate of their own. WHY: those clips are
                // steps, not a run. Turned over at the run's rate they played 1.6 times faster
                // than they were made, and in a recording of real play each foot swung 45 cm
                // and lifted 26 cm two and a half times a second, which read as chaotic.
                float forward = _runShares[ForwardDirection] + _runShares[ForwardRightDirection] + _runShares[ForwardLeftDirection];
                ground = Mathf.Lerp(_profile.SideClipSpeed * _clipLengths[RunInput + SideDirection], ground, forward);
                float matched = Mathf.Max(speed, SlowestCadence * walkSpeed);
                _phase = _moveWeight < StandingStride && stride < StandingStride
                    ? 0f
                    : Mathf.Repeat(_phase + matched / ground * dt, 1f);
                _landTime += dt;
            }
            else
            {
                // Two footfalls to a cycle. The legs keep a fair turnover even at a small stride.
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
                // Touchdown: kick the landing spring hard enough to sink the hips by an amount
                // that grows with how fast the body came down.
                float dip = Mathf.Min(_fallSpeed * _profile.LandGive, _profile.LandDip);
                _land.Rate.x += dip * 2f * Mathf.PI * _profile.LandBounce;
                _landStrength = Mathf.Clamp01(_fallSpeed / _profile.LandFullSpeed);
                _landTime = 0f;
                _fallSpeed = 0f;
            }
            else
            {
                // Running into something: speed across the floor lost all at once.
                // WHY only on the floor and only speed lost: a jump or a blast is just as sudden
                // but adds speed, and a landing has its own reaction above. Ordinary braking
                // loses about 1 m/s a frame, well under the profile's threshold.
                float lost = _lastGroundSpeed - _groundSpeed;
                if (lost > _profile.ImpactSpeed)
                {
                    _stagger = Mathf.Clamp01(_stagger + lost / _profile.ImpactFullSpeed);
                    Vector3 thrown = _travel * (lost * _profile.ImpactKick);
                    _torso.Rate += thrown;
                    _headSpring.Rate += thrown;
                }
            }
            _wasOnFloor = onFloor;
        }

        /// <summary>A lean of <paramref name="degrees"/> toward a direction across the floor (x right, z forward), about the axis that tips the body that way.</summary>
        static Quaternion LeanToward(Vector3 direction, float degrees)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, new Vector3(direction.x, 0f, direction.z));
            return axis.sqrMagnitude > 0f ? Quaternion.AngleAxis(degrees, axis.normalized) : Quaternion.identity;
        }

        /// <summary>
        /// Sets how much of each clip shows and where in it this frame is, then has the graph
        /// write the blend onto the skeleton.
        /// </summary>
        /// <param name="sprint">0 at walking speed or below, 1 at full sprint.</param>
        /// <param name="dt">Frame time, seconds.</param>
        void PoseFromClips(float sprint, float dt)
        {
            float floor = 1f - _airWeight;
            // The landing: all of what the touchdown earned until the bottom of the crouch, then
            // handed back as the body stands up.
            float landAt = Mathf.Min(_landTime * LandClipRate, _clipLengths[LandInput]);
            float land = floor * _landStrength
                * (1f - Mathf.SmoothStep(LandFadeStart, 1f, landAt / _clipLengths[LandInput]))
                * Mathf.Lerp(1f, RunningLandShare, _moveWeight);
            float ground = floor - land;
            float moving = ground * _moveWeight;
            // Only a forward run becomes the sprint clip; sprinting sideways or backwards is the
            // run clip for that direction turning over faster.
            float sprinting = moving * sprint * Mathf.Clamp01(_travel.z);
            float running = moving - sprinting;

            // The one run direction nearest the way the feet are travelling. It only changes
            // once the travel has swung clearly past the halfway line to the next.
            float heading = Mathf.Atan2(_travel.x, _travel.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(_runDirection * RunDirectionStep, heading)) > RunDirectionStep * 0.5f + RunDirectionSlack)
                _runDirection = Mathf.RoundToInt(Mathf.Repeat(heading, 360f) / RunDirectionStep) % AnimationProfile.RunDirections;

            float fidget = Fidget(dt, ground - moving);
            _mixer.SetInputWeight(IdleInput, (ground - moving) * (1f - fidget));
            _mixer.SetInputWeight(FidgetInput, (ground - moving) * fidget);
            // Each direction's share eases toward all or nothing, so a change of direction
            // cross-fades straight from one clip to the other. Every stride clip sits at the same
            // point of its cycle, whatever its length.
            float ease = 1f - Mathf.Exp(-dt / RunDirectionBlendTime);
            for (int i = 0; i < AnimationProfile.RunDirections; i++)
            {
                float wanted = i == _runDirection ? 1f : 0f;
                _runShares[i] += (wanted - _runShares[i]) * ease;
                _mixer.SetInputWeight(RunInput + i, running * _runShares[i]);
                _clips[RunInput + i].SetTime(_phase * _clipLengths[RunInput + i]);
            }
            _mixer.SetInputWeight(SprintInput, sprinting);
            _mixer.SetInputWeight(AirInput, _airWeight);
            _mixer.SetInputWeight(LandInput, land);

            _clips[IdleInput].SetTime(Mathf.Repeat(_time, _clipLengths[IdleInput]));
            _clips[SprintInput].SetTime(_phase * _clipLengths[SprintInput]);
            _clips[AirInput].SetTime(Mathf.Repeat(_time, _clipLengths[AirInput]));
            _clips[LandInput].SetTime(landAt);

            // WHY the whole skeleton goes back to its T-pose first: the Animator does not write
            // a bone on a frame when no clip moves it (the lowest spine bone never moves in the
            // run clips), and the ragdoll overwrote every bone last frame. Left alone, such a
            // bone would keep the ragdoll's last answer and be read back as this frame's target.
            for (int i = 0; i < _skeleton.Length; i++)
                _skeleton[i].SetLocalPositionAndRotation(_skeletonRestPositions[i], _skeletonRestRotations[i]);
            _graph.Evaluate();
            TurnHeadFirst(dt);
            Breathe(ground - moving);
        }

        /// <summary>
        /// Runs the idle fidgets: waits a random while, plays one of the profile's fidget clips
        /// in place of the idle, then waits again.
        /// </summary>
        /// <param name="dt">Frame time, seconds.</param>
        /// <param name="standing">How much of the pose is the idle, 0–1.</param>
        /// <returns>How much of the idle's share the fidget takes this frame, 0–1.</returns>
        /// <remarks>
        /// <para>
        /// Which fidget and when are picked with Unity's random numbers on each client, like the
        /// rest of the pose: two screens will see the same player scratch at different moments.
        /// Nothing reads it back, so nothing can desync (AGENTS.md §3).
        /// </para>
        /// <para>
        /// A fidget only starts, and only keeps its place, while the body is standing still on
        /// the floor. Moving off blends it out and drops it; the wait starts again from there.
        /// </para>
        /// </remarks>
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

        /// <summary>
        /// Lets the head lead a look round: standing, the body stays where it was while the head
        /// and shoulders follow the look, and only once the look has gone past the head's limit
        /// does the body turn to face it.
        /// </summary>
        /// <param name="dt">Frame time, seconds.</param>
        /// <remarks>
        /// <para>
        /// The model itself always faces the look (the motor turns it, and that is not this
        /// component's to change), so the lag is put on the skeleton: the hips are turned back by
        /// however far the body is behind, and the upper chest and head are turned forward again
        /// by the same amount between them. Nothing here moves the player or the camera.
        /// </para>
        /// <para>
        /// Once the limit is passed the body turns the whole way, not just back inside the
        /// limit. WHY: stopping at the limit leaves him standing with his head cranked round for
        /// as long as the player keeps looking that way; a person shuffles round to face it.
        /// </para>
        /// </remarks>
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
            if (!standing || Mathf.Abs(_bodyBehind) > _profile.HeadTurnLimit)
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

        /// <summary>
        /// Adds a breath on top of the pose the clips just wrote: the chest tips back and up, the
        /// shoulders lift, and the head stays level, then it all comes back down.
        /// </summary>
        /// <param name="standing">How much of the pose is the idle, 0–1. Breathing fades out with it.</param>
        /// <remarks>
        /// WHY in code and not in the clip: the library idle breathes by a degree or so, which
        /// the ragdoll all but swallows, and how visible a breath should be is a matter of
        /// taste. Here it is two Inspector values (Breath Amount, Breath Rate) that can be tuned
        /// while playing. The alternative, exaggerating the clip in Blender, needs a re-export
        /// for every change.
        /// </remarks>
        void Breathe(float standing)
        {
            // Positive while breathing in.
            float breath = _profile.BreathAmount * standing * Mathf.Sin(_time * _profile.BreathRate * 2f * Mathf.PI);
            if (breath == 0f)
                return;

            Vector3 across = _model.right, ahead = _model.forward;
            // Turning about the model's right tips a bone forward, so breathing in is a negative turn.
            Quaternion spine = Quaternion.AngleAxis(-breath * BreathSpineShare, across);
            if (_chest.T != null) _chest.T.rotation = spine * _chest.T.rotation;
            if (_upperChest.T != null) _upperChest.T.rotation = spine * _upperChest.T.rotation;

            // The collar bones lift, and each arm turns back by as much so it still hangs straight
            // down from the raised shoulder. The model's left is its -X: a negative turn about its
            // forward axis lifts the left side, a positive one the right.
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

            // The head rode back with the chest; tip it forward again so the face stays put.
            _head.T.rotation = Quaternion.AngleAxis(breath, across) * _head.T.rotation;
        }

        /// <summary>Writes this frame's procedural pose onto the skeleton, parents before children. Used when the profile has no clips.</summary>
        /// <param name="sprint">0 at walking speed or below, 1 at full sprint.</param>
        void Pose(float sprint)
        {
            Quaternion modelRotation = _model.rotation;
            float floor = 1f - _airWeight;
            float stride = _moveWeight * floor;
            float idle = (1f - _moveWeight) * floor;
            float cycle = _phase * 2f * Mathf.PI;
            float groundTime = _profile.GroundTime;

            float trail = Mathf.Clamp((_turn.Value.x - _yawRate) * _profile.TurnLag, -MaxTurnTwist, MaxTurnTwist);
            // Banking: a runner tips into a turn. Turning right is positive; tipping right is a negative roll.
            float bank = -Mathf.Clamp(_turn.Value.x * _profile.TurnBank, -MaxBank, MaxBank) * stride;
            // How much speed the top of the body is still carrying after the feet changed theirs.
            Vector3 carry = _torso.Value - new Vector3(_local.x, 0f, _local.z);
            float landed = Mathf.Clamp(_land.Value.x, -MaxRebound, _profile.LandDip);
            // Positive while the top is carrying on past the feet (a stop), negative while it is
            // being left behind (a start).
            float overrun = Vector3.Dot(carry, _travel);
            // What the body leans by. On a stop it is the springy velocity alone, so the top
            // sails on. On a start the spring would leave the chest upright over legs that have
            // already gone, which reads as sitting down, so the lean is pulled most of the way to
            // the real velocity: the body tips into the run as it sets off.
            Vector3 planarNow = new Vector3(_local.x, 0f, _local.z);
            Vector3 leaning = overrun < 0f ? Vector3.Lerp(_torso.Value, planarNow, StartLead) : _torso.Value;
            Vector3 headLeaning = overrun < 0f ? Vector3.Lerp(_headSpring.Value, planarNow, StartLead) : _headSpring.Value;

            // ---- Hips: low while running, rising and falling with each stride, sinking into a
            // stop or a landing, sliding on over the feet, drifting a little while standing.
            // The bounce is lowest halfway through a foot's time on the floor and highest halfway
            // between push-off and the other foot landing, twice a cycle.
            float bounce = _profile.Bounce * stride * Mathf.Cos(2f * (cycle - Mathf.PI * groundTime));
            // Only while the top is carrying on past the feet (a stop), not while it is being left behind (a start).
            float stopping = Mathf.Clamp(overrun * _profile.StopCrouch, 0f, MaxStopCrouch);
            // WHY the crouch goes with the square of the stride: setting off, the legs should
            // reach before the hips sink; sinking in step with them reads as sitting down first.
            float drop = _profile.HipDrop * idle + _profile.RunCrouch * stride * stride + bounce
                + (stopping + landed + _profile.StaggerDip * _stagger) * floor;
            float sway = Mathf.Sin(_time * _profile.IdleSwayRate * 2f * Mathf.PI) * idle;
            Vector3 hipsOffset = Vector3.down * drop
                + _travel * (Mathf.Clamp(overrun * _profile.BodyCarry, 0f, MaxCarry) * floor)
                + Vector3.right * (_profile.IdleSway * sway);

            // Knocked back, the lean reverses and grows: the head trails the flight.
            float leanScale = Mathf.Lerp(1f, -KnockedTrail, _knocked);
            float lean = Mathf.Clamp(_profile.LeanPerSpeed * leaning.magnitude * leanScale, -MaxLean, MaxLean);
            // The rock puts the chest over whichever foot is taking the weight: left for the
            // first part of the cycle. The hip of the leading leg goes forward; the shoulders
            // turn the other way.
            float rock = _profile.Waddle * stride * Mathf.Sin(cycle) + bank;
            float hipTwist = _profile.HipTwist * stride * Mathf.Cos(cycle);
            float shoulderTwist = _profile.ShoulderTwist * stride * Mathf.Cos(cycle);
            Quaternion hips = LeanToward(leaning, lean * HipsLeanShare)
                * Quaternion.AngleAxis(rock * HipsRockShare, Vector3.forward)
                * Quaternion.AngleAxis(hipTwist, Vector3.up);
            _hips.T.position = _model.TransformPoint(_hipsRest + hipsOffset);
            Set(_hips, modelRotation, hips);

            // ---- Spine: leans by the springy velocity (so it overshoots), folds over the legs
            // at the bottom of a landing, trails a turn, and breathes. Most of it high up.
            float breath = Mathf.Sin(_time * _profile.BreathRate * 2f * Mathf.PI) * idle;
            // The chest nods down as each foot takes the weight, twice a cycle, in time with the hips' bounce.
            float nod = _profile.StepNod * stride * Mathf.Cos(2f * (cycle - Mathf.PI * groundTime));
            float fold = Mathf.Max(0f, landed) * _profile.LandFold + _profile.BreathAmount * breath + nod;
            Quaternion torso = LeanToward(leaning, lean)
                * Quaternion.AngleAxis(fold, Vector3.right)
                * Quaternion.AngleAxis(rock, Vector3.forward)
                * Quaternion.AngleAxis(trail - shoulderTwist, Vector3.up);
            Set(_spine, modelRotation, Quaternion.Slerp(hips, torso, SpineShare));
            Set(_chest, modelRotation, Quaternion.Slerp(hips, torso, ChestShare));
            Set(_upperChest, modelRotation, torso);

            // ---- Head: stays mostly upright while the body pitches under it, on its own slower
            // spring, so it arrives after the chest and nods past it. Standing, it looks around.
            float wander = _time * _profile.IdleLookRate;
            float lookYaw = _profile.IdleLook * idle * (Mathf.PerlinNoise(wander, 0f) * 2f - 1f);
            float lookPitch = _profile.IdleLook * LookPitchShare * idle * (Mathf.PerlinNoise(0f, wander) * 2f - 1f);
            float headLean = Mathf.Clamp(_profile.LeanPerSpeed * _profile.HeadLean * headLeaning.magnitude * leanScale, -MaxLean, MaxLean);
            Quaternion head = LeanToward(headLeaning, headLean)
                * Quaternion.AngleAxis((fold - nod) * _profile.HeadLean + lookPitch - nod * HeadCounterNod, Vector3.right)
                * Quaternion.AngleAxis(rock * HeadRockShare, Vector3.forward)
                * Quaternion.AngleAxis(trail + lookYaw, Vector3.up);
            // The neck sits halfway between the chest and the head.
            Set(_neck, modelRotation, Quaternion.Slerp(torso, head, 0.5f));
            Set(_head, modelRotation, head);

            // ---- Arms: relaxed and held a little out while standing; tucked, bent and pumping
            // against the legs while running; thrown up in the air.
            float raised = Mathf.Max(_airWeight, _knocked * KnockedArmRaise);
            float down = Mathf.Lerp(_profile.IdleArmDown, _profile.RunArmDown, _moveWeight) - _profile.BreathAmount * breath;
            // How far through the jump the body is: 0 on the way up, 1 once it is coming down.
            float falling = Mathf.InverseLerp(RisingSpeed, FallingSpeed, _local.y);
            // The arms come up as the body leaves the floor and lift further as it falls, the
            // way arms go out for balance on the way down.
            down = Mathf.Lerp(down, _profile.RunArmDown - _profile.AirArmRaise * Mathf.Lerp(RisingArmRaise, 1f, falling), raised);
            float bend = Mathf.Lerp(_profile.IdleElbowBend, _profile.ElbowBend, _moveWeight);
            bend = Mathf.Lerp(bend, _profile.AirElbowBend, raised);
            float swing = Mathf.Lerp(_profile.ArmSwing, _profile.SprintArmSwing, sprint) * stride * Mathf.Cos(cycle);
            // Both arms sit a little behind straight down while running, and a little in front of
            // it while standing, where each also drifts slowly on its own so they never hang as
            // a matched pair.
            float offset = ArmSwingSetBack * stride - _profile.IdleArmForward * idle;
            float drift = _time * _profile.IdleLookRate;
            float leftDrift = _profile.IdleArmDrift * idle * (Mathf.PerlinNoise(drift, LeftArmNoiseRow) * 2f - 1f);
            float rightDrift = _profile.IdleArmDrift * idle * (Mathf.PerlinNoise(drift, RightArmNoiseRow) * 2f - 1f);
            // The elbow closes as its arm comes forward and opens as it goes back, so the hand
            // travels from beside the chest to behind the hip instead of staying out in front.
            float pump = _profile.ElbowPump * stride * Mathf.Cos(cycle);
            // In the air the arms are thrown up and a little forward, not straight overhead.
            float reachForward = -_profile.AirArmForward * raised;
            PoseArm(_leftUpperArm, _leftLowerArm, modelRotation, torso, new Vector3(swing + offset + leftDrift + reachForward, down, bend - pump), 1f);
            PoseArm(_rightUpperArm, _rightLowerArm, modelRotation, torso, new Vector3(-swing + offset + rightDrift + reachForward, down, bend + pump), -1f);

            // ---- Legs: each foot follows its own half of the stride cycle on the floor, or
            // hangs in the air pose.
            float reach = Mathf.Lerp(_profile.StepReach, _profile.SprintStepReach, sprint) * stride;
            float kick = Mathf.Lerp(_profile.HeelKick, _profile.SprintHeelKick, sprint) * stride;
            // Hanging on the way up, tucked once the jump tops out.
            float tuck = _profile.AirTuck * Mathf.Lerp(RisingTuck, 1f, falling);
            Vector3 leftAir = new Vector3(0f, tuck, _profile.AirLegSplit);
            Vector3 rightAir = new Vector3(0f, tuck * TrailingTuck, -_profile.AirLegSplit * TrailingSplit);
            Vector3 left = FootTarget(_leftFootRest, _phase, reach, kick, stride, leftAir, out float leftPitch);
            Vector3 right = FootTarget(_rightFootRest, Mathf.Repeat(_phase + 0.5f, 1f), reach, kick, stride, rightAir, out float rightPitch);
            Vector3 knee = modelRotation * Vector3.forward;
            SolveLeg(_leftUpperLeg, _leftLowerLeg, _leftFoot, modelRotation, hips, Quaternion.AngleAxis(leftPitch, Vector3.right), _model.TransformPoint(left), knee);
            SolveLeg(_rightUpperLeg, _rightLowerLeg, _rightFoot, modelRotation, hips, Quaternion.AngleAxis(rightPitch, Vector3.right), _model.TransformPoint(right), knee);
        }

        /// <summary>Turns a bone to its T-pose rotation plus an offset, both relative to the model root.</summary>
        static void Set(in Bone bone, Quaternion modelRotation, Quaternion offset)
        {
            if (bone.T != null)
                bone.T.rotation = modelRotation * offset * bone.Rest;
        }

        /// <summary>
        /// Poses one arm.
        /// </summary>
        /// <param name="pose">x = degrees swung back (+) or forward (-), y = degrees dropped from the T-pose (negative is above the shoulder), z = degrees of bend at the elbow, always forward.</param>
        /// <param name="side">+1 for the left arm, -1 for the right: they drop in mirrored directions.</param>
        static void PoseArm(in Bone upper, in Bone lower, Quaternion modelRotation, Quaternion torso, Vector3 pose, float side)
        {
            Quaternion hang = Quaternion.AngleAxis(pose.y * side, Vector3.forward);
            Set(upper, modelRotation, torso * Quaternion.AngleAxis(pose.x, Vector3.right) * hang);
            Set(lower, modelRotation, torso * Quaternion.AngleAxis(pose.x - pose.z, Vector3.right) * hang);
        }

        /// <summary>
        /// Where an ankle should be this frame, relative to the model root, metres, and how the
        /// foot is tipped.
        /// </summary>
        /// <param name="rest">Where the ankle stands when still.</param>
        /// <param name="phase">0–1 through this foot's own stride: on the floor first, then in the air.</param>
        /// <param name="reach">How far in front of the hip the foot lands and behind it the foot pushes off, metres.</param>
        /// <param name="kick">How high the heel kicks up behind, metres.</param>
        /// <param name="stride">0 = standing, 1 = full stride; scales how far the foot tips and how narrow its track is.</param>
        /// <param name="air">Where the foot hangs in the air pose, as an offset from <paramref name="rest"/>.</param>
        /// <param name="pitch">How far the toes point down, degrees.</param>
        Vector3 FootTarget(Vector3 rest, float phase, float reach, float kick, float stride, Vector3 air, out float pitch)
        {
            float groundTime = _profile.GroundTime;
            float along, up;
            if (phase < groundTime)
            {
                // On the floor: the body passes over the foot, so the foot travels back under
                // it, and the heel lifts as it comes up to push-off.
                float t = phase / groundTime;
                along = Mathf.Lerp(reach, -reach, t);
                up = 0f;
                pitch = _profile.FootRoll * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RollStart, 1f, t));
            }
            else
            {
                // In the air: the heel kicks up behind, then the knee drives the foot forward
                // and it comes down ahead of the hip with the toes level.
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

        /// <summary>
        /// Bends one leg so its ankle reaches a target, then tips the foot.
        /// </summary>
        /// <param name="hipsOffset">The hips' offset from rest, which the leg starts from before it is bent.</param>
        /// <param name="footTip">How the foot is tipped, relative to the model root.</param>
        /// <param name="target">Where the ankle should be, world space.</param>
        /// <param name="kneeForward">The way the knee bends, world space.</param>
        /// <remarks>
        /// The leg is put back in its rest pose first and bent from there by the shortest turn.
        /// WHY: bending from last frame's pose lets a little twist build up along the thigh
        /// every frame until the knee points sideways.
        /// </remarks>
        static void SolveLeg(in Bone upper, in Bone lower, in Bone foot, Quaternion modelRotation, Quaternion hipsOffset, Quaternion footTip, Vector3 target, Vector3 kneeForward)
        {
            Set(upper, modelRotation, hipsOffset);
            Set(lower, modelRotation, hipsOffset);

            Vector3 hip = upper.T.position;
            float thigh = Vector3.Distance(hip, lower.T.position);
            float shin = Vector3.Distance(lower.T.position, foot.T.position);

            Vector3 toTarget = target - hip;
            float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Epsilon, (thigh + shin) * MaxLegExtension);
            Vector3 direction = toTarget.normalized;

            // The knee sits on a circle around the hip-to-ankle line; the law of cosines gives
            // how far along that line and how far off it, and kneeForward picks the point.
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
