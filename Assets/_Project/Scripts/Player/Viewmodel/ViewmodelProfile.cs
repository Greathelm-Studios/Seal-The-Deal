// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "set up an fps viewmodel with the characters model we are using"
//  Modified  : Claude Fable 5.2, 2026-11-13 — framing and posing taken from the NorseFPS
//              project at Alex's request ("steal the viewmodel framing and posing from the
//              norsefps project"): the gun is placed in the view and the arm reaches to it.
//  Modified  : Claude Fable 5.2, 2026-11-14 — Harvey: the view is "super unrefined", make
//              it "in line with the movement and more clean". Added movement weight, sprint
//              carry, climbing, idle breathing, and roll and shift on the look sway.
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The designer's tuning panel for the first-person arms: where the gun sits on
//              screen, how it lags behind the look, bobs with the stride, kicks on a shot and
//              dips for a reload. Its own asset so the feel can be tuned without touching
//              the player prefab.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Designer-owned tuning for the first-person arms and the weapon they hold.
    /// Runtime only reads it (AGENTS.md §4).
    /// </summary>
    /// <remarks>
    /// The framing numbers start from NorseFPS's hand cannon (holder 0.18, -0.12, 0.70 m at a
    /// 62 degree lens, 4 degrees muzzle-up, 9 degrees turned in), pulled in to 0.32 m because
    /// this character's arm is 0.43 m long and has to reach the grip. The gun keeps the same
    /// direction from the eye, so it sits at the same place on screen.
    /// WHY one profile for every weapon: slice 1 has one pistol. When weapons carry their own
    /// kick and reload, those parts move to the weapon's data asset; the framing and sway stay.
    /// </remarks>
    [CreateAssetMenu(menuName = "Close the Deal/Viewmodel Profile", fileName = "ViewmodelProfile")]
    public sealed class ViewmodelProfile : ScriptableObject
    {
        [Header("Lens")]
        [Tooltip("How wide the arms are drawn, in degrees of vertical view. This is separate from the world's view: lower makes the arm and gun look bigger and closer, higher makes them small and stretched. 62 is the NorseFPS value.")]
        [Range(30f, 90f)] public float FieldOfView = 62f;

        [Tooltip("Nearest distance at which the arms are still drawn, in metres. Anything closer to the eye is cut away. Raise it only if the sleeve fills the screen; 1 cm keeps the whole forearm.")]
        [Min(0.005f)] public float NearDistance = 0.01f;

        [Tooltip("Furthest distance at which the arms are drawn, in metres. The arm never reaches past about 1 m, so 5 is plenty.")]
        [Min(1f)] public float FarDistance = 5f;

        [Header("Framing")]
        [Tooltip("Where the gun's grip sits relative to your eye, in metres: right, up, forward. This is the main framing dial. Keep it within about 0.4 m of the shoulder below or the arm cannot reach and locks straight.")]
        public Vector3 GunPosition = new(0.125f, -0.135f, 0.38f);

        [Tooltip("How the gun is turned at rest, in degrees: pitch (negative lifts the muzzle), yaw (negative turns the muzzle in toward the middle of the screen), roll (positive tips the top of the gun inward, for a loose, canted hold).")]
        public Vector3 GunTurn = new(-1f, -3f, 4f);

        [Tooltip("Where the shoulder of the gun arm sits relative to your eye, in metres: right, up, forward. Level with the gun and well behind it brings the forearm in straight behind the gun; lower makes the forearm come up from underneath and bends the wrist.")]
        public Vector3 ShoulderPosition = new(0.165f, -0.165f, -0.10f);

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

        [Header("Movement weight")]
        [Tooltip("How far the gun is left behind by your own movement, in seconds of travel. 0.004 means that at 6 m/s the gun sits 2.4 cm behind where you are heading: back when you run forward, left when you strafe right, down when you jump, up when you fall. 0 locks the gun to the view.")]
        [Min(0f)] public float MoveLagSeconds = 0.004f;

        [Tooltip("The fastest movement the gun reacts to, in metres per second. Past this the gun drifts and tilts no further, so a long fall does not throw it off screen. 10 is a little over sprint speed.")]
        [Min(0.1f)] public float MoveLagSpeedLimit = 10f;

        [Tooltip("How quickly the gun catches up with a change in movement, in swings per second. Higher is tighter and lighter, lower is heavier and looser.")]
        [Range(0.5f, 10f)] public float MoveSpringSpeed = 3.2f;

        [Tooltip("How much the gun overshoots when you stop, start or land. 1 settles with no bounce; lower bounces more. This is what makes a landing dip the gun.")]
        [Range(0.2f, 1f)] public float MoveSpringDamping = 0.55f;

        [Tooltip("How hard a landing pushes the gun down, as a multiple of the fall's own speed. 0 leaves only the bounce the damping gives; 1.5 dips the gun about 3 cm after a full-height jump. The harder the fall, the deeper the dip.")]
        [Range(0f, 4f)] public float LandBounce = 1.5f;

        [Tooltip("How far the gun rolls when you strafe, in degrees per metre per second of sideways speed. 0.4 is about 2.4 degrees at walking speed. The top of the gun is left behind, tipping the opposite way to your movement.")]
        [Range(0f, 2f)] public float StrafeRoll = 0.4f;

        [Tooltip("How far the muzzle tips when you rise or fall, in degrees per metre per second. The muzzle dips as you go up and lifts as you come down.")]
        [Range(0f, 2f)] public float AirTilt = 0.5f;

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

        [Header("Idle breathing")]
        [Tooltip("How far the gun rises and falls while you stand still, in metres. Fades out as you move. 0 holds it dead still.")]
        [Min(0f)] public float BreathHeight = 0.0015f;

        [Tooltip("How long one breath takes, in seconds.")]
        [Min(0.5f)] public float BreathSeconds = 4f;

        [Header("Walk bob")]
        [Tooltip("How far the gun rises and falls with each step at full run speed, in metres. 0 turns the bob off.")]
        [Min(0f)] public float BobHeight = 0.014f;

        [Tooltip("How far the gun swings side to side with each stride at full run speed, in metres.")]
        [Min(0f)] public float BobSide = 0.02f;

        [Tooltip("Steps per metre travelled. 0.45 is a step about every 2.2 m.")]
        [Min(0f)] public float BobStepsPerMetre = 0.45f;

        [Tooltip("How far the gun rocks from side to side with each stride at full run speed, in degrees.")]
        [Range(0f, 10f)] public float BobRoll = 1.2f;

        [Tooltip("How quickly the bob fades in when you set off and out when you stop or leave the ground, in seconds.")]
        [Min(0.01f)] public float BobFadeSeconds = 0.15f;

        [Header("Fire kick")]
        [Tooltip("How far the muzzle jumps up on a shot, in degrees. This moves the gun and arm only; it does not move your aim.")]
        [Range(0f, 45f)] public float KickPitch = 14f;

        [Tooltip("How far the gun punches back toward you on a shot, in metres.")]
        [Min(0f)] public float KickBack = 0.05f;

        [Tooltip("How far the gun twists on a shot, in degrees. A little roll stops the kick looking like it runs on a rail.")]
        [Range(0f, 20f)] public float KickRoll = 3f;

        [Tooltip("How long the gun takes to settle after a shot, in seconds. Shorter reads as a light, snappy gun.")]
        [Min(0.01f)] public float KickRecoverSeconds = 0.2f;

        [Header("Reload")]
        [Tooltip("How long the reload move lasts, in seconds. Only the look for now; there is no ammunition yet.")]
        [Min(0.1f)] public float ReloadSeconds = 1.2f;

        [Tooltip("How far the gun drops at the deepest point of the reload, in metres.")]
        [Min(0f)] public float ReloadDrop = 0.10f;

        [Tooltip("How far the muzzle tips up at the deepest point of the reload, in degrees.")]
        [Range(0f, 80f)] public float ReloadTilt = 40f;

        [Tooltip("How far the gun rolls onto its side at the deepest point of the reload, in degrees.")]
        [Range(0f, 90f)] public float ReloadRoll = 25f;
    }
}
