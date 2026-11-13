using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Designer-owned movement tuning. Runtime only reads it (AGENTS.md §4).
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Movement Profile", fileName = "MovementProfile")]
    public sealed class MovementProfile : ScriptableObject
    {
        [Header("Ground")]
        [Tooltip("Walking speed in metres per second. Higher = faster across a floor. 6 is a brisk walk.")]
        public float WalkSpeed = 6f;

        [Tooltip("Speed in metres per second while Sprint is held.")]
        public float SprintSpeed = 9f;

        [Tooltip("How quickly the player gets up to speed and changes direction on the floor, in metres per second per second. Higher = snappier starts and sharper turns; at 70 a standing start reaches walking speed in 0.1 s. Must stay above Ground Friction times Sprint Speed (54 at the defaults) or the grip wins and sprint never reaches its speed.")]
        public float GroundAcceleration = 70f;

        [Tooltip("How hard the floor grips a running player: the fraction of their speed taken away each second. It is what stops you when you let go, and what bleeds off speed above your running speed if you stay on the floor instead of hopping. At 6 a sprint stops in 0.3 s and 1 m (measured in the movement lane scene); lower values slide further.")]
        public float GroundFriction = 6f;

        [Tooltip("Below this speed, in metres per second, the floor grips as if you were moving this fast, so the last bit of a stop is crisp instead of a long creep. Higher = stops deader.")]
        public float StopSpeed = 3f;

        [Tooltip("Fastest a player can travel across the floor or through the air by their own movement, in metres per second. A ceiling on what hops, slides and air strafing can build up to, so the netcode and the floor sizes have a known worst case. Blasts are not capped by this.")]
        public float MaxSpeed = 24f;

        [Tooltip("How quickly a knocked-down player skids to a stop once on the floor, in metres per second per second. Lower = longer comedy slide.")]
        public float KnockedBraking = 15f;

        [Tooltip("How fast the body turns to face where the camera looks, in degrees per second. 720 feels immediate; 360 shows the turn; below 180 feels sluggish.")]
        public float TurnSpeed = 720f;

        [Header("Stairs and slopes")]
        [Tooltip("How far the floor may drop away under a walking player before they count as falling, in metres. Keeps feet on the stairs when running down them or over the top of a ramp. At 0.35 a sprint down the spiral stairs (peaking at 10.8 m/s) never left the treads in the lane test. Higher = sticks to bigger drops, but small ledges stop counting as a fall.")]
        public float StepDownSnap = 0.35f;

        [Header("Crouch")]
        [Tooltip("Speed in metres per second while crouched. Sprint does nothing while crouched.")]
        public float CrouchSpeed = 3f;

        [Tooltip("How tall the body is when fully crouched, in metres. Standing is the height of the player prefab's capsule (1.8). Lower = fits under more, and a smaller target. Can't go below twice the capsule radius (0.7).")]
        public float CrouchHeight = 1.1f;

        [Tooltip("How fast the player ducks and stands, in full crouches per second. 8 = an eighth of a second to get all the way down.")]
        public float CrouchRate = 8f;

        [Header("Slide")]
        [Tooltip("Slowest a player can be moving, in metres per second, for Crouch to start a slide instead of a crouch. Above walking speed, so it takes a sprint, a hop or a fall to slide.")]
        public float SlideMinSpeed = 7f;

        [Tooltip("Speed added at the start of a slide, in metres per second. The reward for sliding out of a sprint or a landing. 0 = a slide only keeps what you had.")]
        public float SlideBoost = 2.5f;

        [Tooltip("How long before a new slide can give its boost again, in seconds. Stops crouch-spamming for free speed; sliding itself is never locked out.")]
        public float SlideBoostCooldown = 1.5f;

        [Tooltip("How fast a slide loses speed on the flat: the fraction of speed taken away each second. Far below Ground Friction, so a slide carries. At 0.45 a slide out of a sprint went 17.8 m in 2.7 s in the lane test; lower = longer slides.")]
        public float SlideFriction = 0.45f;

        [Tooltip("How strongly a downhill pulls a slide along, as a multiple of real gravity's pull down the slope. 0 = slopes do nothing. At 2, a slide down the lane scene's 20° ramp (8.8 m long) went from 9.3 to 10.4 m/s; higher = steeper speed gain downhill.")]
        public float SlideSlopeGain = 2f;

        [Tooltip("A slide ends when it slows to this speed, in metres per second, and becomes a crouch. Keep at or above Crouch Speed.")]
        public float SlideEndSpeed = 3.5f;

        [Header("Air")]
        [Tooltip("How quickly a direction held in the air changes the player's velocity, in metres per second per second. It turns a jump, brakes it when you pull back, and is how fast air strafing adds speed. Also steers a slide. 0 = no air control.")]
        public float AirAcceleration = 30f;

        [Tooltip("The most speed a held direction can add along itself in the air, in metres per second. Small on purpose: pushing forward in the air barely speeds you up, but holding a strafe key while turning the view the same way keeps adding, which is how air strafing curves a jump and builds speed: at 1.5, three strafed hops took a player from 9 to 15 m/s in the lane test. Higher = easier speed gain and floatier control; 0 = no air control at all.")]
        public float AirStrafeSpeed = 1.5f;

        [Tooltip("Tick so that holding Jump hops again the instant you land, keeping all your speed (no timing needed). Untick and each hop needs a fresh press inside the Jump Buffer Time before landing, which is harder and loses speed when mistimed.")]
        public bool HoldToHop = true;

        [Tooltip("Jump height in metres on flat ground.")]
        public float JumpHeight = 1.3f;

        [Tooltip("Pull-down while rising from a jump and all through a blast flight, as a multiple of normal gravity. 1 = floaty real-world arcs; 2 = snappy game jumps. Also shapes how far a blast throws you. The way down from a jump uses Fall Gravity Multiplier instead.")]
        public float AirGravityMultiplier = 2f;

        [Tooltip("Fastest a player can fall, in metres per second. Caps long drops and big blasts. 30 is a hard but readable fall; 0 = no cap.")]
        public float MaxFallSpeed = 30f;

        [Header("Jump feel")]
        [Tooltip("Pull-down on the way down from a jump, as a multiple of normal gravity. Replaces the air gravity once the jump peaks. Equal to air gravity = symmetric arc; higher = quick, punchy landings. Doesn't change blasts.")]
        public float FallGravityMultiplier = 3f;

        [Tooltip("Fraction of upward speed kept when Space is let go early, so a tap is a short hop and a hold is a full jump. 1 = every jump is full height; 0.5 = a tap reaches about a quarter of the height.")]
        [Range(0f, 1f)] public float JumpReleaseKeep = 0.5f;

        [Tooltip("How long after walking off an edge a jump still works, in seconds. 0.1 forgives a late press without feeling like a double jump.")]
        public float CoyoteTime = 0.1f;

        [Tooltip("How early before landing a jump press is remembered and fired on touchdown, in seconds. 0.12 makes chained hops easy; 0 = must press on the ground.")]
        public float JumpBufferTime = 0.12f;

        [Header("Lean")]
        [Tooltip("How far the head slides sideways at full lean, in metres. 0.45 clears a door frame from a body tucked behind it. Shots leave from the leaned eye.")]
        public float LeanDistance = 0.45f;

        [Tooltip("How far the view tilts at full lean, in degrees. 12 reads clearly; above 20 gets disorienting.")]
        public float LeanAngle = 12f;

        [Tooltip("How fast the lean goes in and out, in full leans per second. 6 = about a sixth of a second to full lean.")]
        public float LeanSpeed = 6f;

        [Header("Climbing")]
        [Tooltip("Highest ledge the player can climb onto, in metres above their feet. Keep below the player's height (1.8) or they climb things they can't reach. Measured from the feet, so a full jump adds its height on top.")]
        public float MaxLedgeHeight = 1.6f;

        [Tooltip("Lowest edge that counts as a climb, in metres above the feet. Anything lower must be a ramp to be walked over.")]
        public float MinLedgeHeight = 0.4f;

        [Tooltip("How far in front of the body a ledge can be and still be grabbed, in metres.")]
        public float LedgeReach = 0.7f;

        [Tooltip("How long the tallest climb takes from grab to standing on top, in seconds. Shorter climbs finish sooner.")]
        public float MantleDuration = 0.35f;

        [Tooltip("Fraction of the speed the player ran in with that they keep coming out of a climb or vault. 0 = stops dead on top; 1 = loses nothing. A vault over something thin never comes out slower than walking speed, so it always clears.")]
        [Range(0f, 1f)] public float MantleSpeedKeep = 0.9f;

        [Header("Ground check")]
        [Tooltip("How far below the feet still counts as touching the floor, in metres. Keep about 0.1: higher and the player 'lands' before reaching it.")]
        public float GroundProbe = 0.1f;

        [Header("Look")]
        [Tooltip("Mouse look speed in degrees per mouse count. 0.12 is a typical shooter feel; higher turns faster.")]
        public float MouseSensitivity = 0.12f;

        [Tooltip("How fast the look stick turns the view left and right at full tilt, in degrees per second. 240 is a full turn in a second and a half (measured 240). Before this setting the stick turned at about 7.")]
        public float StickYawSpeed = 240f;

        [Tooltip("How fast the look stick tilts the view up and down at full tilt, in degrees per second. Slower than the turn so a sideways flick doesn't throw the aim up or down.")]
        public float StickPitchSpeed = 150f;

        [Tooltip("How much of the look stick's travel is kept for fine aim. 1 = speed rises evenly with tilt; higher = small pushes aim more finely while a full push still turns at full speed. At 1.6, half tilt turned at 50 degrees per second in the lane test (the look dead zone takes a little more off).")]
        [Range(1f, 3f)] public float StickLookCurve = 1.6f;

        [Tooltip("How far the view can look up, in degrees above level. Keep at or below 89: at 90 the view flips.")]
        [Range(0f, 89f)] public float LookUpLimit = 89f;

        [Tooltip("How far the view can look down, in degrees below level. Keep at or below 89: at 90 the view flips.")]
        [Range(0f, 89f)] public float LookDownLimit = 89f;

        [Header("View feel")]
        [Tooltip("How far the view dips on landing, in metres per metre-per-second of falling speed. 0 = no dip. At 0.012 a full jump (landing at 9.4 m/s) dips the view 0.11 m.")]
        public float LandingDipPerSpeed = 0.012f;

        [Tooltip("The most the view can dip on landing, in metres, however hard the fall.")]
        public float LandingDipMax = 0.25f;

        [Tooltip("How long the view takes to come back up after a landing dip, in seconds.")]
        public float LandingDipRecover = 0.25f;

        [Tooltip("How much wider the view gets at speed, in degrees of field of view: nothing at walking speed, all of it at sprint speed and above, in the air too. 0 = none. 6 sells the speed; above 12 looks like a zoom.")]
        public float SpeedFovKick = 6f;

        [Tooltip("How fast the speed widening comes in and goes out, in degrees per second.")]
        public float SpeedFovRate = 30f;
    }
}
