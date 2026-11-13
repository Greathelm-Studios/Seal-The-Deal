// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "set up an fps viewmodel with the characters model we are using"
//  Modified  : Claude Fable 5.2, 2026-11-13 — framing and posing taken from the NorseFPS
//              project at Alex's request ("steal the viewmodel framing and posing from the
//              norsefps project"): the gun is placed in the view and the arm reaches to it.
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
        public Vector3 GunPosition = new(0.082f, -0.055f, 0.32f);

        [Tooltip("How the gun is turned at rest, in degrees: pitch (negative lifts the muzzle), yaw (negative turns the muzzle in toward the middle of the screen), roll.")]
        public Vector3 GunTurn = new(-4f, -9f, 0f);

        [Tooltip("Where the shoulder of the gun arm sits relative to your eye, in metres: right, up, forward. Lower and further back makes the forearm come up from the bottom corner; higher shows more of the upper arm.")]
        public Vector3 ShoulderPosition = new(0.17f, -0.20f, -0.08f);

        [Header("Look sway")]
        [Tooltip("How far behind the view the gun trails when you turn, in seconds of turning. 0.03 means that at a turn of 100 degrees a second the gun trails 3 degrees. 0 locks the gun to the view.")]
        [Min(0f)] public float LookLagSeconds = 0.03f;

        [Tooltip("The most the gun can trail behind the view, in degrees, however fast you turn. 4 is the NorseFPS value.")]
        [Range(0f, 20f)] public float LookLagLimit = 4f;

        [Tooltip("How long the gun takes to catch up with the view, in seconds. Lower is snappier, higher is heavier and floatier.")]
        [Min(0.01f)] public float LookLagSettleSeconds = 0.09f;

        [Header("Walk bob")]
        [Tooltip("How far the gun rises and falls with each step at full run speed, in metres. 0 turns the bob off.")]
        [Min(0f)] public float BobHeight = 0.014f;

        [Tooltip("How far the gun swings side to side with each stride at full run speed, in metres.")]
        [Min(0f)] public float BobSide = 0.02f;

        [Tooltip("Steps per metre travelled. 0.45 is a step about every 2.2 m.")]
        [Min(0f)] public float BobStepsPerMetre = 0.45f;

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
