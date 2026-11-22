using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Designer-owned movement tuning. Runtime only reads it (AGENTS.md §4).
    /// </summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Movement Profile", fileName = "MovementProfile")]
    public sealed class MovementProfile : ScriptableObject
    {
        [Header("Ground")]
        [Tooltip("Walking speed in metres per second. Higher = faster across a floor. 6.75 is a quick walk (it was 6 until the owner asked for a touch more speed).")]
        public float WalkSpeed = 6.75f;

        [Tooltip("Speed in metres per second while Sprint is held.")]
        public float SprintSpeed = 10.1f;

        [Tooltip("How quickly the player gets up to speed and changes direction on the floor, in metres per second per second. Higher = snappier starts and sharper turns; at 70 a standing start reaches walking speed in 0.1 s. Must stay above Ground Friction times Sprint Speed (60.6 at the defaults) or the grip wins and sprint never reaches its speed.")]
        public float GroundAcceleration = 80f;

        [Tooltip("How hard the floor grips a running player: the fraction of their speed taken away each second. It is what stops you when you let go, and what bleeds off speed above your running speed if you stay on the floor instead of hopping. At 6 a sprint stops in 0.3 s and 1 m (measured in the movement lane scene); lower values slide further.")]
        public float GroundFriction = 6f;

        [Tooltip("How fast a player running on the floor can swing their speed round to a new direction without losing any of it, in degrees per second. Holding 9 m/s round the spiral stairs' walking line needs about 300; 720 makes every turn on foot lossless. 0 = turns only by grip and acceleration, which bleeds speed in tight curves (sprint dropped to 6 m/s up the spiral). Reversing (more than 100° away) still brakes.")]
        public float GroundTurnRate = 720f;

        [Tooltip("Below this speed, in metres per second, the floor grips as if you were moving this fast, so the last bit of a stop is crisp instead of a long creep. Higher = stops deader.")]
        public float StopSpeed = 3f;

        [Tooltip("Fastest a player can travel across the floor or through the air by their own movement, in metres per second. A ceiling on what hops, slides and air strafing can build up to, so the netcode and the floor sizes have a known worst case. Blasts are not capped by this.")]
        public float MaxSpeed = 24f;

        [Tooltip("How quickly a knocked-down player skids to a stop once on the floor, in metres per second per second. Lower = longer comedy slide.")]
        public float KnockedBraking = 15f;

        [Tooltip("How fast the body turns to face where the camera looks, in degrees per second. 720 feels immediate; 360 shows the turn; below 180 feels sluggish.")]
        public float TurnSpeed = 720f;

        [Header("Stairs and slopes")]
        [Tooltip("Tallest vertical step or stair riser a walking player steps straight up onto without jumping, in metres. 0.35 clears standard stair treads and floor lips smoothly; 0 turns step-up off.")]
        [Min(0f)] public float StepUpHeight = 0.35f;

        [Tooltip("How far the floor may drop away under a walking player before they count as falling, in metres. Keeps feet on the stairs when running down them or over the top of a ramp. At 0.35 a sprint down the spiral stairs (peaking at 10.8 m/s) never left the treads in the lane test. Higher = sticks to bigger drops, but small ledges stop counting as a fall.")]
        public float StepDownSnap = 0.35f;

        [Header("Crouch")]
        [Tooltip("Speed in metres per second while crouched. Sprint does nothing while crouched.")]
        public float CrouchSpeed = 3.3f;

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

        [Tooltip("How strongly a downhill pulls a slide along, as a multiple of real gravity's pull down the slope. 0 = slopes do nothing. At 2, a slide down the lane scene's 20° ramp (8.8 m long) went from 9.3 to 10.4 m/s; 2.5 builds speed visibly even from a faster sprint. Higher = steeper speed gain downhill.")]
        public float SlideSlopeGain = 2.5f;

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

        [Tooltip("Pull-down while rising from a jump, as a multiple of normal gravity. 1 = floaty real-world arcs; 2.4 = crisp arena jumps. Blasts use Knocked Gravity Multiplier instead. The way down from a jump uses Fall Gravity Multiplier instead.")]
        public float AirGravityMultiplier = 2.4f;

        [Tooltip("Fastest a player can fall, in metres per second. Caps long drops and big blasts. 30 is a hard but readable fall; 0 = no cap.")]
        public float MaxFallSpeed = 30f;

        [Tooltip("Pull-down on a player thrown by a blast, as a multiple of normal gravity, for the whole flight. Separate from the jump's so a floatier jump doesn't send blasts further. 2 is what blasts were tuned with.")]
        public float KnockedGravityMultiplier = 2f;

        [Header("Jump feel")]
        [Tooltip("Pull-down near the top of a jump, as a multiple of normal gravity, while moving up or down slower than Apex Hang Speed. Lower than Air Gravity Multiplier gives a brief micro-hang at the peak; 1.4 with 0.9 m/s hangs for ~0.13 s.")]
        public float ApexGravityMultiplier = 1.4f;

        [Tooltip("How slow the rise or fall must be, in metres per second, for the apex hang to apply. 0.9 with 1.4 apex gravity hangs for about 0.13 seconds.")]
        public float ApexHangSpeed = 0.9f;

        [Tooltip("Pull-down on the way down from a jump, as a multiple of normal gravity. Replaces the air gravity once the jump peaks. Equal to air gravity = symmetric arc; higher (3.4) = quick, punchy landings. Doesn't change blasts.")]
        public float FallGravityMultiplier = 3.4f;

        [Tooltip("Fraction of upward speed kept when Space is let go early, so a tap is a short hop and a hold is a full jump. 1 = every jump is full height; 0.5 = a tap reaches about a quarter of the height.")]
        [Range(0f, 1f)] public float JumpReleaseKeep = 0.5f;

        [Tooltip("How long after walking off an edge a jump still works, in seconds. 0.1 forgives a late press without feeling like a double jump.")]
        public float CoyoteTime = 0.12f;

        [Tooltip("How early before landing a jump press is remembered and fired on touchdown, in seconds. 0.12 makes chained hops easy; 0 = must press on the ground.")]
        public float JumpBufferTime = 0.15f;

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

        [Header("Body")]
        [Tooltip("Height of the eye above the feet when standing, in metres. The first-person camera sits here and shots leave from here. 1.6 on the 1.8 m body; the crouch lowers it by the difference between standing and crouched height.")]
        public float EyeHeight = 1.6f;

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
        [Tooltip("How quickly the view's bounce settles, in springs per second. Every take-off, landing, slide and climb kicks the view and this spring brings it back. Higher = tighter and smaller dip; lower = deeper, slower bounce.")]
        public float CameraSpringFrequency = 4.5f;

        [Tooltip("How much the view's bounce overshoots before it settles, 0 to 1. 1 = no overshoot (dead); 0.55 dips cleanly and recovers with a slight rebound; below 0.3 wobbles.")]
        [Range(0.1f, 1f)] public float CameraSpringDamping = 0.55f;

        [Tooltip("Kick on take-off, in metres per second downward: the view dips a touch and springs up into the jump. 0 = none.")]
        public float JumpKick = 0.65f;

        [Tooltip("Kick on landing for each metre per second of falling speed, downward. 0.18 dips about 3.4 cm off a full jump (10.3 m/s) and up to 4.4 cm off a hard drop. 0 = none.")]
        public float LandKickPerSpeed = 0.18f;

        [Tooltip("The most a landing can kick the view, in metres per second, however hard the fall.")]
        public float LandKickMax = 2.4f;

        [Tooltip("Kick when a slide starts, in metres per second downward: the view drops into the slide.")]
        public float SlideKick = 0.9f;

        [Tooltip("Kick when a climb or vault starts, in metres per second downward, before the climb lifts the view: reads as gathering for the pull-up.")]
        public float MantleKick = 0.3f;

        [Tooltip("Kick when sprinting starts, in metres per second backward: the view lags a touch as the body surges.")]
        public float SprintStartKick = 0.06f;

        [Tooltip("Kick when a fast run stops dead, in metres per second forward: the view carries on a little and settles back.")]
        public float HardStopKick = 0.10f;

        [Tooltip("Slowest a run can be, in metres per second, for stopping from it to count as a hard stop.")]
        public float HardStopSpeed = 7f;

        [Tooltip("How much wider the view gets while sliding, in degrees of field of view, on top of the speed widening. 0 = none.")]
        public float SlideFovKick = 4f;

        [Tooltip("How much wider the view gets at speed, in degrees of field of view: nothing at walking speed, all of it at sprint speed and above, in the air too. 0 = none. 6 sells the speed; above 12 looks like a zoom.")]
        public float SpeedFovKick = 6f;

        [Tooltip("How fast the speed widening comes in and goes out, in degrees per second.")]
        public float SpeedFovRate = 30f;

        [Tooltip("How far the view rolls while sliding, in degrees, leaning away from the slide's sideways drift and slightly into the slide when it runs straight. Rolling never moves the crosshair, so aim is untouched. 0 = no roll; 2 reads as weight; above 5 is seasick.")]
        public float SlideRoll = 2f;

        [Tooltip("How fast the slide roll comes in and goes out, in degrees per second.")]
        public float SlideRollRate = 20f;

        [Tooltip("How far the view tilts into a sideways strafe at walk speed, in degrees. Rolling never moves the crosshair, so aim is untouched. 0 = no roll; 0.8 gives a grounded feel; above 3 feels seasick.")]
        [Range(0f, 5f)] public float StrafeRoll = 0.8f;

        [Tooltip("How fast the strafe roll comes in and goes out, in degrees per second.")]
        [Min(0.1f)] public float StrafeRollRate = 10f;

        [Header("Crosshair")]
        [Tooltip("How far the crosshair's arms open for each metre per second above walking speed, in pixels at 100% crosshair size on a 1080p screen. 1.2 opens it about 4 px at a sprint and 11 px at 15 m/s of air strafing. 0 = speed never opens it. With a gun in hand, the gun's own spread is used instead whenever that is wider.")]
        public float CrosshairOpenPerSpeed = 1.2f;

        [Tooltip("Extra opening while in the air (jumping, hopping, falling, thrown), in pixels at 100% size. 6 reads clearly without hiding the centre.")]
        public float CrosshairOpenInAir = 6f;

        [Tooltip("The most movement alone can open the crosshair, in pixels at 100% size.")]
        public float CrosshairOpenMax = 16f;

        [Tooltip("How fast the crosshair opens and closes toward its target, in pixels per second. 80 settles in about a fifth of a second; lower is lazier.")]
        public float CrosshairOpenRate = 80f;

        [Tooltip("Landing faster than this, in metres per second downward, gives the crosshair a small outward pulse. A full jump lands at 9.4; 0 = every landing pulses.")]
        public float CrosshairLandingPulseSpeed = 8f;

        [Tooltip("How much the crosshair pulses outward on take-off, as a fraction of its arm length. 0 = none.")]
        [Range(0f, 1f)] public float CrosshairJumpPulse = 0.2f;

        [Tooltip("How much the crosshair pulses outward when a slide starts, as a fraction of its arm length. 0 = none.")]
        [Range(0f, 1f)] public float CrosshairSlidePulse = 0.35f;

        [Tooltip("Extra opening while sliding, in pixels at 100% size.")]
        public float CrosshairOpenSliding = 4f;

        [Header("Sound")]
        [Tooltip("The movement sounds (footsteps, jump, landing, slide, climbs, wind). Empty = silent movement. Filled by Seal the Deal > Lanes > Set Up Movement Sounds when empty.")]
        public MovementSoundSet Sounds;

        [Header("Gamepad rumble")]
        [Tooltip("Rumble strength on landing for each metre per second of falling speed, 0 to 1 of the motors' full strength. Only on a gamepad. 0 = none.")]
        public float RumbleLandPerSpeed = 0.05f;

        [Tooltip("The most a landing can rumble, 0 to 1.")]
        [Range(0f, 1f)] public float RumbleLandMax = 0.7f;

        [Tooltip("Rumble strength when a slide starts, 0 to 1. 0 = none.")]
        [Range(0f, 1f)] public float RumbleSlide = 0.35f;

        [Tooltip("How long one rumble lasts, in seconds.")]
        public float RumbleSeconds = 0.14f;

        [Header("Gamepad")]
        [Tooltip("Smallest push of the move stick that counts, as a fraction of full tilt. 0.1 ignores stick drift; raise it if a pad creeps.")]
        [Range(0f, 0.5f)] public float MoveStickDeadZone = 0.1f;

        [Tooltip("Smallest push of the look stick that turns the view, as a fraction of full tilt. 0.15 stops a worn stick drifting the aim; raise it if the view creeps on its own.")]
        [Range(0f, 0.5f)] public float LookStickDeadZone = 0.15f;

        [Tooltip("Extra turning speed added when the look stick is held right at the edge, in degrees per second. Lets a pad player turn round quickly without making fine aim twitchy. 0 = off; 160 takes a full-tilt turn from 240 to 400.")]
        public float StickTurnBoost = 160f;

        [Tooltip("How long the look stick must sit at the edge before the extra turn starts, in seconds. Shorter = quicker to kick in, but flicks that overshoot start turning faster too.")]
        public float StickTurnBoostDelay = 0.15f;

        [Tooltip("How long the extra turn takes to build up to full once it starts, in seconds.")]
        public float StickTurnBoostRamp = 0.25f;
    }
}
