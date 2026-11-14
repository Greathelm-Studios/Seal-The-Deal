// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-12
//  Prompt    : Alex: "set up a humanoid procedural animation system" (built-in, no clips), then
//              three rounds of feel notes ending "too stiff, transitions are harsh ... the video
//              i sent is kinda what im looking for, but that more airy top heavy loose feel"
//  Modified  : Claude Fable 5.2, 2026-11-12 — clip fields and the start-stop carry, after
//              Alex chose to drive the ragdoll from real animation clips
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The tuning panel for PlayerAnimator. Its own asset, like MovementProfile, so the
//              look of the run can be tuned without touching Player.prefab (which the greybox
//              tool regenerates) and so runtime never writes a designer's values (AGENTS.md §4).
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Designer-owned tuning for the character animation: the clips it plays, the ragdoll that
    /// chases them, and the older procedural pose used when a clip slot is empty. Runtime only
    /// reads it (AGENTS.md §4).
    /// </summary>
    /// <remarks>
    /// Angles are degrees, distances metres, times seconds. All distances assume the 1.8 m
    /// character; a differently sized character needs its own profile. The defaults were set by
    /// eye against frame strips of the reference video ("Full Procedural Animations System") and
    /// of this animator played back in the Editor; none has been tuned in a real play session.
    /// </remarks>
    [CreateAssetMenu(menuName = "Close the Deal/Animation Profile", fileName = "AnimationProfile")]
    public sealed class AnimationProfile : ScriptableObject
    {
        /// <summary>How many directions <see cref="RunClips"/> holds: one every 45 degrees.</summary>
        public const int RunDirections = 8;

        /// <summary>True when every clip slot is filled, so the animator can play clips instead of its procedural pose.</summary>
        public bool HasClips
        {
            get
            {
                if (IdleClip == null || SprintClip == null || AirClip == null || LandClip == null
                    || RunClips == null || RunClips.Length != RunDirections)
                    return false;
                for (int i = 0; i < RunDirections; i++)
                {
                    if (RunClips[i] == null)
                        return false;
                }
                return true;
            }
        }

        [Header("Clips (the poses the ragdoll chases; with any slot empty the built-in procedural pose is used instead)")]
        [Tooltip("Standing still. Loops.")]
        public AnimationClip IdleClip;

        [Tooltip("The run in eight directions with the body facing forward, clockwise seen from above: forward, forward-right, right, back-right, back, back-left, left, forward-left. All must loop and start on the same foot; they may be different lengths.")]
        public AnimationClip[] RunClips = new AnimationClip[RunDirections];

        [Tooltip("The flat-out forward sprint. Loops, and starts on the same foot as the run.")]
        public AnimationClip SprintClip;

        [Tooltip("In the air: jumping or falling. Loops.")]
        public AnimationClip AirClip;

        [Tooltip("Touching down: starts in the air pose, sinks into a crouch and stands up. Plays once.")]
        public AnimationClip LandClip;

        [Tooltip("The speed across the floor the run clip's stride was made for, in metres per second. The legs turn over faster or slower than the clip to keep the feet matched to the real speed. Lower = quicker legs; raise it if the feet look like they are skating forward.")]
        [Range(2f, 12f)] public float RunClipSpeed = 5.36f;

        [Tooltip("The same for the sprint clip, in metres per second.")]
        [Range(2f, 14f)] public float SprintClipSpeed = 8.25f;

        [Tooltip("How fast the legs step when moving sideways or backwards: the speed across the floor, in metres per second, at which those clips play at the rate they were made. At 6 a strafe at walking speed steps about one and a half times a second, calmly, and the feet slide. Lower = quicker, busier steps that slide less; 3.7 was the old rate.")]
        [Range(1f, 12f)] public float SideClipSpeed = 6f;

        [Tooltip("How far you can look round, in degrees either side, with only the head and shoulders turning. Past this the body turns to face where you are looking. Lower = the body follows almost at once; higher = he looks further over his shoulder first. Only while standing: moving or in the air, the body always faces the look.")]
        [Range(0f, 90f)] public float HeadTurnLimit = 50f;

        [Tooltip("How fast the body turns to face the look once it follows, in degrees per second. Lower = a slow shuffle round; higher = a snap.")]
        [Range(60f, 1080f)] public float BodyTurnSpeed = 300f;

        [Tooltip("Falling speed at touchdown, in metres per second, that plays the whole landing crouch. Slower landings sink in proportion. A 1.6 m jump lands at about 9.7, so at 20 an ordinary jump sinks about halfway. Lower = deeper landings.")]
        [Range(3f, 40f)] public float LandFullSpeed = 20f;

        [Header("Idle fidgets (little things he does while standing still)")]
        [Tooltip("The fidgets: scratching, yawning, looking about. One is picked at random each time. Each must start and end on the idle pose and plays once. Empty = he only stands and breathes.")]
        public AnimationClip[] FidgetClips = new AnimationClip[0];

        [Tooltip("Shortest time he stands still between fidgets, in seconds. Lower = restless.")]
        [Range(0.5f, 60f)] public float FidgetMinWait = 5f;

        [Tooltip("Longest time he stands still between fidgets, in seconds. The wait is picked at random between the shortest and this each time.")]
        [Range(0.5f, 120f)] public float FidgetMaxWait = 12f;

        [Header("Looseness (procedural pose only; unused while clips are assigned)")]
        [Tooltip("How fast the upper body wobbles after a start, stop or turn, in wobbles per second. Lower = big slow airy sways; higher = a quick shiver. The head always wobbles slower than the chest.")]
        [Range(0.5f, 6f)] public float WobbleSpeed = 2.2f;

        [Tooltip("How quickly a wobble dies away. 0 = wobbles for ever, 1 = no overshoot at all (stiff). About 0.55 overshoots a little, once, and settles.")]
        [Range(0.05f, 1f)] public float WobbleDamping = 0.55f;

        [Tooltip("Lean into the direction of travel, in degrees for every metre per second of speed. At 2.9 a 6 m/s run leans about 17 degrees, and more for a moment each time it overshoots.")]
        [Range(0f, 5f)] public float LeanPerSpeed = 2.9f;

        [Tooltip("How much of the chest's lean the head takes, on its own slower wobble. 0 = head always level, 1 = as far as the chest. Low values keep the eyes up while the body pitches under them.")]
        [Range(0f, 1.5f)] public float HeadLean = 0.4f;

        [Tooltip("How far the hips slide on over the feet after a stop, in metres for every metre per second the top of the body is still carrying. 0 = hips nailed above the feet.")]
        [Range(0f, 0.06f)] public float BodyCarry = 0.012f;

        [Tooltip("How far the body sinks into its knees while it is still carrying speed after a stop, in metres for every metre per second. This is the skid into a crouch.")]
        [Range(0f, 0.05f)] public float StopCrouch = 0.012f;

        [Tooltip("How far the shoulders and head trail behind a turn, in seconds: degrees of twist for every degree per second of turning. 0 = turns as one block.")]
        [Range(0f, 0.15f)] public float TurnLag = 0.015f;

        [Tooltip("How far the body banks into a turn while running, in degrees for every degree per second of turning. At 0.02 a 360-a-second turn banks about 7 degrees.")]
        [Range(0f, 0.1f)] public float TurnBank = 0.02f;

        [Header("Standing (procedural pose only)")]
        [Tooltip("How far the arms hang down from straight out sideways while standing, in degrees. 90 = flat against the body; about 80 lets them hang close beside the hips.")]
        [Range(0f, 90f)] public float IdleArmDown = 80f;

        [Tooltip("Bend at the elbow while standing, in degrees. 0 = dead straight arms.")]
        [Range(0f, 90f)] public float IdleElbowBend = 16f;

        [Tooltip("How far in front of straight down the arms hang while standing, in degrees, so the hands rest in front of the thighs rather than at the trouser seams.")]
        [Range(0f, 30f)] public float IdleArmForward = 7f;

        [Tooltip("How far each arm drifts forward and back on its own while standing, in degrees each way. 0 = both arms hang dead still.")]
        [Range(0f, 15f)] public float IdleArmDrift = 4f;

        [Tooltip("How far the hips sit below their straight-legged height while standing, in metres, so the knees are soft. Higher = a deeper, readier stance.")]
        [Range(0f, 0.2f)] public float HipDrop = 0.04f;

        [Tooltip("How far the chest tips back and the shoulders lift with each breath while standing, in degrees. 0 = no breathing beyond what the idle clip has. Used with the clips as well as with the procedural pose.")]
        [Range(0f, 10f)] public float BreathAmount = 8f;

        [Tooltip("Breaths per second while standing. 0.27 is about 16 a minute, a body at rest. Used with the clips as well as with the procedural pose.")]
        [Range(0.05f, 1f)] public float BreathRate = 0.27f;

        [Tooltip("How far the hips drift from side to side while standing, in metres each way. The feet stay put.")]
        [Range(0f, 0.08f)] public float IdleSway = 0.018f;

        [Tooltip("Sideways drifts per second while standing.")]
        [Range(0.02f, 1f)] public float IdleSwayRate = 0.13f;

        [Tooltip("How far the head wanders while standing, as if looking around, in degrees each way. 0 = stares straight ahead.")]
        [Range(0f, 45f)] public float IdleLook = 9f;

        [Tooltip("How quickly the head finds something new to look at while standing. Higher = fidgety.")]
        [Range(0.02f, 2f)] public float IdleLookRate = 0.2f;

        [Header("Running: legs (procedural pose only)")]
        [Tooltip("Footfalls per second at walking speed. Higher = quicker, busier legs; lower = long floating strides.")]
        [Range(1f, 5f)] public float RunCadence = 2.7f;

        [Tooltip("Footfalls per second at full sprint.")]
        [Range(1f, 5f)] public float SprintCadence = 3.2f;

        [Tooltip("Share of each stride a foot spends on the floor. Below 0.5 there is a moment with both feet in the air, which is what makes it a run and gives it bounce. 0.5 or more is a walk.")]
        [Range(0.25f, 0.6f)] public float GroundTime = 0.38f;

        [Tooltip("How far a foot lands in front of the hip, and pushes off behind it, at walking speed, in metres.")]
        [Range(0.05f, 0.6f)] public float StepReach = 0.36f;

        [Tooltip("The same at full sprint, in metres. Above about 0.5 the leg runs out of length.")]
        [Range(0.05f, 0.6f)] public float SprintStepReach = 0.44f;

        [Tooltip("How high the heel kicks up behind the body after pushing off, at walking speed, in metres. This folds the knee and is most of what stops the legs looking like stilts.")]
        [Range(0f, 0.7f)] public float HeelKick = 0.36f;

        [Tooltip("The same at full sprint, in metres.")]
        [Range(0f, 0.7f)] public float SprintHeelKick = 0.5f;

        [Tooltip("How far the hips sit below their straight-legged height while running, in metres. Higher = a deeper, springier run with more bend in the knees.")]
        [Range(0f, 0.25f)] public float RunCrouch = 0.13f;

        [Tooltip("How far the hips rise and fall with each stride, in metres each way. Lowest as a foot takes the weight, highest with both feet off the floor.")]
        [Range(0f, 0.15f)] public float Bounce = 0.06f;

        [Tooltip("How far the heel lifts as the foot pushes off the floor, in degrees.")]
        [Range(0f, 70f)] public float FootRoll = 35f;

        [Tooltip("How far the toes point down and back at the top of the heel kick, in degrees.")]
        [Range(0f, 110f)] public float KickFootPitch = 70f;

        [Header("Running: upper body (procedural pose only)")]
        [Tooltip("How far the hips turn with each stride, the hip of the leading leg going forward, in degrees each way.")]
        [Range(0f, 30f)] public float HipTwist = 6f;

        [Tooltip("How far the shoulders turn against the hips with each stride, in degrees each way.")]
        [Range(0f, 30f)] public float ShoulderTwist = 7f;

        [Tooltip("How far the chest rocks from side to side over each foot, in degrees. The hips rock half as far. 0 = glides level.")]
        [Range(0f, 20f)] public float Waddle = 2f;

        [Tooltip("How far the arms hang down from straight out sideways while running, in degrees. About 80 tucks the elbows in beside the ribs.")]
        [Range(0f, 90f)] public float RunArmDown = 80f;

        [Tooltip("How far each arm swings forward and back at walking speed, in degrees each way.")]
        [Range(0f, 90f)] public float ArmSwing = 36f;

        [Tooltip("The same at full sprint, in degrees.")]
        [Range(0f, 90f)] public float SprintArmSwing = 48f;

        [Tooltip("Bend at the elbow while running, in degrees. About 90 is a runner's pumping arm.")]
        [Range(0f, 130f)] public float ElbowBend = 72f;

        [Tooltip("How far the elbow closes as its arm swings forward and opens as it swings back, in degrees each way. 0 = the forearm stays stuck out in front.")]
        [Range(0f, 50f)] public float ElbowPump = 14f;

        [Tooltip("How far the chest nods down as each foot takes the weight, in degrees. The head tips back against most of it. This is the top-heavy bob.")]
        [Range(0f, 15f)] public float StepNod = 3f;

        [Header("Ragdoll (a body of rigidbodies and joints chasing the pose)")]
        [Tooltip("How strongly the spine's motor pulls the chest to its pose, in swings per second. Lower = the heavy top half sways, lags and droops more. This is the main wobble dial.")]
        [Range(0.5f, 40f)] public float SpineStrength = 18f;

        [Tooltip("How strongly the neck's motor holds the head up, in swings per second. Lower = a heavy head that bobs and lolls.")]
        [Range(0.5f, 40f)] public float HeadStrength = 14f;

        [Tooltip("How strongly the shoulder and elbow motors pull the arms to their pose, in swings per second. Lower = arms that trail, flail and swing wide.")]
        [Range(0.5f, 40f)] public float ArmStrength = 20f;

        [Tooltip("How strongly the hip and knee motors pull the legs through their stride, in swings per second. Keep this well above the others or the legs cannot keep up with the run.")]
        [Range(1f, 40f)] public float LegStrength = 26f;

        [Tooltip("How quickly every joint's swinging dies away. 1 = no overshoot (stiff), 0.4 = swings past and settles, below 0.2 = keeps flapping.")]
        [Range(0.05f, 1.5f)] public float JointDamping = 0.85f;

        [Tooltip("How tightly the hips are pulled along after the player, in swings per second. Lower = the whole body trails behind and sloshes; below about 3 it visibly lags the camera.")]
        [Range(1.5f, 12f)] public float HipsFollow = 8f;

        [Tooltip("How quickly the hips' own bouncing behind the player dies away. 1 = none.")]
        [Range(0.2f, 1.5f)] public float HipsDamping = 0.75f;

        [Tooltip("How much of the player's speeding up and slowing down the whole body simply goes along with, instead of being yanked there by its hips. 0 = full yank: the top bends back and the legs trail on every start, and it folds forward on every stop. 1 = no reaction at all, the body starts and stops as one piece. This is the start-and-stop floppiness dial.")]
        [Range(0f, 1f)] public float StartStopCarry = 0.65f;

        [Tooltip("How strongly the hips are held upright and facing the right way, in swings per second. Lower = the pelvis rocks and twists more.")]
        [Range(1f, 40f)] public float UprightStrength = 20f;

        [Tooltip("How heavy the body is, as a multiple of real gravity. Lower = floaty limbs that drift down; 1 = arms and head drop like real ones.")]
        [Range(0f, 2f)] public float RagdollGravity = 0.6f;

        [Tooltip("How much harder every joint pulls while in the air, as a multiple of its strength on the floor. 1 = a jump sprawls and tumbles; 2 = it holds its shape.")]
        [Range(1f, 4f)] public float AirBrace = 2f;

        [Tooltip("How much of their strength the joints keep while the body is limp (knocked back, or just after running into something). 0.1 = a rag doll; 1 = no reaction.")]
        [Range(0.02f, 1f)] public float LimpShare = 0.12f;

        [Header("Jumping and landing (procedural pose only)")]
        [Tooltip("How far the arms are thrown up from their running position while in the air, in degrees. 66 puts them out to the sides just below shoulder height; they lift a little further as the body falls.")]
        [Range(0f, 170f)] public float AirArmRaise = 66f;

        [Tooltip("How far forward of the body the raised arms reach in the air, in degrees.")]
        [Range(0f, 60f)] public float AirArmForward = 28f;

        [Tooltip("Bend at the elbow in the air, in degrees.")]
        [Range(0f, 120f)] public float AirElbowBend = 48f;

        [Tooltip("How far the feet pull up toward the body once the jump tops out and starts to fall, in metres. On the way up the legs hang straighter.")]
        [Range(0f, 0.5f)] public float AirTuck = 0.16f;

        [Tooltip("How far the leading foot hangs in front of the trailing one in the air, in metres.")]
        [Range(0f, 0.5f)] public float AirLegSplit = 0.26f;

        [Tooltip("How far the hips sink on landing, in metres for every metre per second of falling speed. A 1.6 m jump lands at about 9.7.")]
        [Range(0f, 0.06f)] public float LandGive = 0.018f;

        [Tooltip("Deepest the hips can sink from a landing, in metres, however hard it is.")]
        [Range(0f, 0.5f)] public float LandDip = 0.24f;

        [Tooltip("How fast the body springs back up out of a landing, in bounces per second. Lower = a long, soft, floaty recovery.")]
        [Range(0.5f, 6f)] public float LandBounce = 2.6f;

        [Tooltip("How far the chest folds forward over the legs at the bottom of a landing, in degrees for every metre the hips have sunk.")]
        [Range(0f, 200f)] public float LandFold = 35f;

        [Header("Getting hit")]
        [Tooltip("Speed across the floor lost in a single frame, in metres per second, that counts as running into something. Ordinary braking loses about 1; a wall at walking speed takes all 6.")]
        [Min(0.5f)] public float ImpactSpeed = 4f;

        [Tooltip("Speed lost at once, in metres per second, that staggers the body fully.")]
        [Min(1f)] public float ImpactFullSpeed = 9f;

        [Tooltip("How much harder than normal the chest and head are thrown on by an impact. 0 = only their usual wobble.")]
        [Range(0f, 10f)] public float ImpactKick = 2f;

        [Tooltip("How far the knees buckle on a full stagger, in metres.")]
        [Range(0f, 0.35f)] public float StaggerDip = 0.08f;

        [Tooltip("How long the body takes to gather itself after a full stagger, in seconds.")]
        [Range(0.1f, 3f)] public float StaggerRecover = 0.5f;

        [Header("Smoothing")]
        [Tooltip("How long the body takes to ease between standing, running and airborne poses, in seconds. It eases in and out rather than moving at a fixed rate. Lower = snappier.")]
        [Range(0.02f, 0.5f)] public float PoseBlendTime = 0.12f;

        [Tooltip("How much the measured speed is smoothed, in seconds. Higher hides network corrections but makes the feet late to start and stop.")]
        [Range(0.01f, 0.3f)] public float VelocitySmoothing = 0.05f;
    }
}
