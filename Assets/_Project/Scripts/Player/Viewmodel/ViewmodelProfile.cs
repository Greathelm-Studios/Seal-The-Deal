using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>Designer-owned tuning for held-weapon framing, sway, kick, reload, and true-POV body view.</summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Viewmodel Profile", fileName = "ViewmodelProfile")]
    public sealed class ViewmodelProfile : ScriptableObject
    {
        [Header("Framing")]
        [Tooltip("Where the gun's grip sits relative to your eye, in metres: right, up, forward. This is the main framing dial.")]
        public Vector3 GunPosition = new(0.125f, -0.135f, 0.38f);

        [Tooltip("How the gun is turned at rest, in degrees: pitch (negative lifts the muzzle), yaw (negative turns the muzzle in toward the middle of the screen), roll (positive tips the top of the gun inward, for a loose, canted hold).")]
        public Vector3 GunTurn = new(-1f, -3f, 4f);

        [Header("Two-handed hold")]
        [Tooltip("Where the gun's grip sits when it is held in both hands, in metres from your eye: right, up, forward. Nearer the middle and closer in than the one-handed place, because the left arm has to reach it from the other shoulder.")]
        public Vector3 TwoHandedPosition = new(0.055f, -0.10f, 0.33f);

        [Tooltip("How the gun is turned when it is held in both hands, in degrees: pitch, yaw, roll, as for Gun turn.")]
        public Vector3 TwoHandedTurn = new(-1f, -1f, 0f);

        [Tooltip("How long the gun takes to move between the one-handed and two-handed place, in seconds.")]
        [Min(0.01f)] public float TwoHandedBlendSeconds = 0.18f;

        [Header("Body (what other players see)")]
        [Tooltip("How much further out the body holds the gun than your own view does, in metres along the aim. 0 leaves the body's elbow visibly bent; about 0.06 straightens the arm. Too far and the arm locks straight and the hand falls short of the gun when the body sways back.")]
        [Range(0f, 0.15f)] public float BodyGunForward = 0.06f;

        [Tooltip("How much the body's gun arm flips about when you move, as a multiple of what the ragdoll throws it. 0 holds the arm and gun dead still on the aim; 1 is the ragdoll's own lag, swing and bounce; 2 doubles it. The gun's muzzle swings by the same multiple. However high this is, the hand is never thrown more than 4 cm sideways, 7 cm up or down or 6 cm forward or back from the aimed spot, or the muzzle more than 12 degrees off the aim. Standing still the arm always comes back to straight on the aim.")]
        [Range(0f, 4f)] public float BodyArmSwing = 0.5f;

        [Tooltip("How long the body's gun arm takes to come back to straight on the aim after being thrown about, in seconds. Shorter recovers sooner and cuts slow swings short; longer lets a swing play out and leaves the arm off the aim for longer.")]
        [Min(0.05f)] public float BodySwingSettleSeconds = 0.35f;

        [Header("Look sway")]
        [Tooltip("How far behind the view the gun trails when you turn, in seconds of turning. 0.03 means that at a turn of 100 degrees a second the gun trails 3 degrees. 0 locks the gun to the view.")]
        [Min(0f)] public float LookLagSeconds = 0.03f;

        [Tooltip("The most the gun can trail behind the view, in degrees, however fast you turn. 4 is the NorseFPS value.")]
        [Range(0f, 20f)] public float LookLagLimit = 4f;

        [Tooltip("How long the gun takes to catch up with the view, in seconds. Lower is snappier, higher is heavier and floatier.")]
        [Min(0.01f)] public float LookLagSettleSeconds = 0.09f;

        [Tooltip("How far the gun rolls into a turn, in degrees of roll per degree it trails. 0.5 means a gun trailing 4 degrees also tips 2 degrees. 0 keeps it level.")]
        [Range(0f, 2f)] public float LookLagRoll = 0.5f;

        [Tooltip("How far the gun slides across the view as it trails a turn, in metres per degree it trails. Gives the arm some weight rather than twisting the gun on the spot. 0 turns it off.")]
        [Min(0f)] public float LookLagShift = 0.0012f;

        [Tooltip("How much the gun stays upright when you lean, as a fraction of the lean's tilt. 0 tilts the gun fully with the view; 1 keeps it level with the floor.")]
        [Range(0f, 1f)] public float LeanCounterRoll = 0.35f;

        [Header("Sprint carry")]
        [Tooltip("Where the gun moves to at full sprint, in metres from its resting place: right, up, forward. It blends in between walking and sprint speed.")]
        public Vector3 SprintShift = new(0f, -0.012f, -0.015f);

        [Tooltip("How the gun turns at full sprint, in degrees on top of its resting turn: pitch (positive drops the muzzle), yaw (negative turns it inward), roll.")]
        public Vector3 SprintTurn = new(3f, -6f, -4f);

        [Tooltip("How long the gun takes to move into and out of the sprint carry, in seconds.")]
        [Min(0.01f)] public float SprintBlendSeconds = 0.18f;

        [Header("Climbing")]
        [Tooltip("How far the gun drops out of the way while you climb a ledge, in metres.")]
        [Min(0f)] public float ClimbDrop = 0.09f;

        [Tooltip("How far the muzzle tips down while you climb a ledge, in degrees.")]
        [Range(0f, 80f)] public float ClimbTilt = 30f;

        [Tooltip("How long the gun takes to drop for a climb and come back up, in seconds.")]
        [Min(0.01f)] public float ClimbBlendSeconds = 0.12f;

        [Header("Fire kick (only when no gun is wired: a real gun kicks with its own 'gun in the hand' values)")]
        [Tooltip("How far the muzzle jumps up on a shot, in degrees. This moves the gun and arm only; it does not move your aim.")]
        [Range(0f, 45f)] public float KickPitch = 14f;

        [Tooltip("How far the gun punches back toward you on a shot, in metres.")]
        [Min(0f)] public float KickBack = 0.05f;

        [Tooltip("How far the gun twists on a shot, in degrees. A little roll stops the kick looking like it runs on a rail.")]
        [Range(0f, 20f)] public float KickRoll = 3f;

        [Tooltip("How long the gun takes to settle after a shot, in seconds. Shorter reads as a light, snappy gun.")]
        [Min(0.01f)] public float KickRecoverSeconds = 0.2f;

        [Header("Slide (a pistol's top moving back on each shot, seen by everyone, and racked on an empty click and after a reload)")]
        [Tooltip("How far the slide snaps back on a shot, in metres of the gun's own size. 0 = the slide never moves. A real pistol's goes back about a sixth of its length.")]
        [Range(0f, 0.06f)] public float SlideTravel = 0.024f;

        [Tooltip("How long the slide takes to come forward again, in seconds. Keep it shorter than the time between shots or a fast trigger finger sees it stuck back.")]
        [Range(0.02f, 0.3f)] public float SlideReturnSeconds = 0.075f;

        [Tooltip("How long the slide takes to be pulled back, held and let go when it is racked at the end of a reload, in seconds. A click on an empty magazine pulls it back at the same speed and it stays back until the reload lets it go. Longer reads as a slow, deliberate pull; shorter as a flick.")]
        [Range(0.08f, 0.8f)] public float SlideRackSeconds = 0.26f;

        [Header("Reload")]
        [Tooltip("How long the reload move lasts when no gun from the weapon system is held, in seconds. A real gun's reload lasts as long as the gun's own Reload seconds.")]
        [Min(0.1f)] public float ReloadSeconds = 1.2f;

        [Tooltip("Where the gun moves to while it is held up for the reload, in metres from its resting place: right, up, forward. Up and toward the middle brings the grip, the magazine and the left hand into view; too low and the whole reload happens below the bottom of the screen.")]
        public Vector3 ReloadShift = new(-0.03f, 0.06f, -0.01f);

        [Tooltip("How the gun is turned while it is held up for the reload, in degrees on top of its resting turn: pitch (negative lifts the muzzle), yaw (negative turns the muzzle in toward the middle), roll (negative tips the top of the gun outward, which swings the bottom of the grip toward the left hand so you can see the magazine go in).")]
        public Vector3 ReloadTurn = new(-25f, -30f, -65f);

        [Tooltip("How far the gun swings past its reload pose as it snaps up, as a fraction of the move. 0 = it eases in with no overshoot; higher is snappier and more cartoony.")]
        [Range(0f, 0.5f)] public float ReloadOvershoot = 0.12f;

        [Tooltip("How far the magazine slides to come clear of the grip, in metres: the length of the magazine's body. The pistol's is 0.056. Too short and the magazine drops away while its top is still inside the grip; too long and it hangs in the air below the grip before the new one is pushed in.")]
        [Range(0f, 0.15f)] public float ReloadMagazineLength = 0.058f;

        [Tooltip("How far the grip leans, in degrees: the magazine slides along this line, not straight down. The pistol's grip leans 12.3 (measured from the model). Wrong and the magazine cuts through the front or back of the grip as it slides.")]
        [Range(-30f, 30f)] public float ReloadMagazineLean = 12.3f;

        [Tooltip("How far the empty magazine falls before it disappears, in metres. Raise it if you can see the magazine vanish in mid-air.")]
        [Range(0.05f, 1f)] public float ReloadMagazineFall = 0.35f;

        [Tooltip("How hard the gun flicks as the empty magazine is thrown out, in degrees of extra roll.")]
        [Range(0f, 30f)] public float ReloadEjectFlick = 7f;

        [Tooltip("How far the gun is knocked upward as the new magazine is slapped in, in metres. 0 = no bump.")]
        [Range(0f, 0.05f)] public float ReloadSeatBump = 0.014f;

        [Tooltip("How much the left hand comes up to push the new magazine in. 0 = the magazine goes in on its own and the left hand stays where it was; 1 = the hand comes all the way to the bottom of the grip.")]
        [Range(0f, 1f)] public float ReloadHandReach = 1f;

        [Header("Body view (looking out of the real body)")]
        [Tooltip("How much of the head QUICK movement the eye takes in the body view: the bob of each step and the jolt of a hit. 0 = none, a gliding eye (most comfortable); 1 = all of it (most likely to make people feel sick). Slow movement, like leaning into a run or looking at the floor, is always followed in full.")]
        [Range(0f, 1f)] public float BodyViewHeadFollow = 0.08f;

        [Tooltip("How long the eye takes to settle onto where the head has moved to in the body view, in seconds. Higher = steadier and less W/S surge, but the body can get ahead of the eye when you start to sprint; lower = tighter and shakier.")]
        [Range(0.01f, 0.5f)] public float BodyViewSmoothing = 0.22f;

        [Tooltip("How far in front of the head bone the eye sits in the body view, in metres: where the face is. Higher keeps the camera lens clear of your shoulders and upper arm; lower shows more of your arm.")]
        [Range(0f, 0.3f)] public float BodyViewForward = 0.065f;

        [Tooltip("The most quick head movement the eye may take in the body view, in metres, before the share above is applied. A cap so a hard hit cannot throw the view about.")]
        [Range(0f, 0.5f)] public float BodyViewMaxOffset = 0.15f;

        [Tooltip("Where the gun is held in the body view, measured from GunPosition, in metres: X right, Y up, Z further out. Raise Z to extend the arm further out in front of you; raise Y to bring it higher into view.")]
        public Vector3 BodyViewGunShift = new Vector3(0.045f, 0.015f, 0.04f);

        [Tooltip("Radius of the world-space sphere around your real head bone that is not drawn on your own screen, in metres: hides your head, hair and eye cards even when the ragdoll moves fast. Raise if hair pokes into view; lower if your shoulder or sleeve gets cut away. Mirrors, shadows and other players always see the whole body.")]
        [Range(0f, 0.5f)] public float BodyViewHeadClip = 0.20f;

        [Tooltip("How much the ragdoll's movement throws the gun arm around in the body view, as a multiple of the ragdoll's own swing. 0 holds the arm stiff on aim; higher makes it floppier and goofier. Outward and downward swings stay framed while inward and upward swings cushion before crossing the crosshair.")]
        [Range(0f, 3f)] public float BodyViewArmSwing = 0.45f;

        [Tooltip("How long the body-view arm swing is smoothed over, in seconds. Removes physics-step jitter while keeping responsive swings. Lower is snappier; higher is floatier.")]
        [Range(0.005f, 0.2f)] public float BodyViewSwingSmooth = 0.035f;

        [Tooltip("Minimum distance to the right of the crosshair the gun stays in the one-handed body view, in metres. Raise if the gun or forearm drifts into the middle of the screen.")]
        [Range(0f, 0.25f)] public float BodyViewKeepOutRight = 0.155f;

        [Tooltip("Minimum distance below the crosshair the gun stays in the body view, in metres. Raise if the gun lifts into your sightline.")]
        [Range(0f, 0.25f)] public float BodyViewKeepOutDown = 0.085f;

        [Tooltip("Closest the gun may pull back toward your eyes in the body view, in metres. Raise if the hand or sleeve clips into the camera lens.")]
        [Range(0.12f, 0.45f)] public float BodyViewMinForward = 0.36f;
    }
}
