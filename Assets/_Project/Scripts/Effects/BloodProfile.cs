// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Sonnet 5.5 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "create a blood splatter shader that fits in within the games artstyle
//              that paints around objects and ontop of them when hit by these attacks."
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The designer's tuning panel for blood splats: colours, sizes, how many and how
//              wide they spray, how long they last. Its own asset so the look can be changed
//              without touching the spawner or the shader.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CloseTheDeal.Effects
{
    /// <summary>
    /// Designer-owned tuning for blood splats. Runtime only reads it (AGENTS.md §4). If the asset
    /// is missing the spawner uses a copy with these defaults, so blood works before anyone makes one.
    /// </summary>
    /// <remarks>
    /// Defaults are first guesses from the art direction (slightly oversized, flat cartoon blood),
    /// not from play. Written by <c>Seal the Deal &gt; Art &gt; Build Blood Assets</c> to
    /// <c>Assets/_Project/Art/Blood/Resources/BloodProfile.asset</c>.
    /// </remarks>
    [CreateAssetMenu(menuName = "Seal the Deal/Blood Profile", fileName = "BloodProfile")]
    public sealed class BloodProfile : ScriptableObject
    {
        [Header("Look")]
        [Tooltip("The main blood colour: flat, saturated red. The splat has no gradient, so this is exactly what is seen on the lit side.")]
        public Color BloodColor = new Color(0.66f, 0.07f, 0.15f, 1f);

        [Tooltip("The darker tone cut into the side of each splat that faces the floor. One hard-edged step, not a gradient. Pick it by hand, deeper and slightly bluer than the main colour.")]
        public Color CutColor = new Color(0.33f, 0.05f, 0.17f, 1f);

        [Header("Size")]
        [Tooltip("Width of the splat, in metres, for a pistol-strength hit (about 20 impulse). Raise it for bigger, sillier blood.")]
        [Min(0.1f)] public float BaseSize = 0.8f;

        [Tooltip("The impulse (the hit's strength, in newton-seconds) that gives exactly the base size. A pistol shot is about 20. Size grows with the square root of the strength, so a hit ten times harder is about three times wider.")]
        [Min(1f)] public float ReferenceImpulse = 20f;

        [Tooltip("The widest any splat can get, in metres, however hard the hit. Lower = big hits stop looking enormous.")]
        [Min(0.2f)] public float MaxSize = 2.0f;

        [Tooltip("How much each splat's size varies from splat to splat, as a fraction. 0 = all the same size; 0.3 = up to 30% bigger or smaller.")]
        [Range(0f, 0.8f)] public float SizeVariation = 0.25f;

        [Tooltip("How thick the projection box is, in metres. The splat paints every surface inside it, so thicker = it wraps further around corners and over props in front of the wall, but may also reach through thin geometry.")]
        [Min(0.1f)] public float Depth = 0.7f;

        [Header("Spray: how many splats a hit makes")]
        [Tooltip("How many splats a bullet throws onto the surfaces behind the victim. Each one flies along a slightly different line inside the spray cone.")]
        [Min(0)] public int BulletSplats = 2;

        [Tooltip("How many splats a melee hit throws.")]
        [Min(0)] public int MeleeSplats = 4;

        [Tooltip("Extra splats for every 100 impulse of hit strength. 1 = a 300 impulse hit adds 3 splats.")]
        [Min(0f)] public float ExtraSplatsPer100Impulse = 1f;

        [Tooltip("The most splats one hit can throw behind the victim, however hard. Also keeps the cost of one hit predictable.")]
        [Min(1)] public int MaxSplatsPerHit = 8;

        [Tooltip("Half-angle of the spray cone for a bullet, in degrees. 0 = every splat on the line of fire; larger = blood sprayed in a wider fan.")]
        [Range(0f, 80f)] public float BulletCone = 10f;

        [Tooltip("Half-angle of the spray cone for a melee hit, in degrees. Wider than a bullet's because a swing throws blood sideways.")]
        [Range(0f, 80f)] public float MeleeCone = 35f;

        [Tooltip("Extra half-angle of the cone for every 100 impulse, in degrees. Harder hits spray wider.")]
        [Min(0f)] public float ConeBonusPer100Impulse = 5f;

        [Tooltip("How far behind the victim, in metres, the spray can reach a wall or prop. Nothing further away gets blood.")]
        [Min(0.5f)] public float Reach = 3.5f;

        [Tooltip("How much each splat is stretched along the direction the blood was thrown, from 0 (round) to 1 (long streaks). A bullet is cleaner, a melee swing smears.")]
        [Range(0f, 1f)] public float BulletStretch = 0.85f;

        [Tooltip("Stretch for a melee hit; see the bullet one.")]
        [Range(0f, 1f)] public float MeleeStretch = 0.6f;

        [Header("Floor splat")]
        [Tooltip("Size of the one splat dropped on the floor under the victim, as a fraction of the normal splat size. 0 = no floor splat.")]
        [Range(0f, 2f)] public float FloorSize = 0.8f;

        [Tooltip("How far below the victim, in metres, to look for a floor. If there is none that close (a fall, a lift shaft) no floor splat is made.")]
        [Min(0.5f)] public float FloorReach = 3f;

        [Header("Life")]
        [Tooltip("How long a splat stays at full size, in seconds, before it starts to shrink away. Lower = a cleaner level; higher = a messier fight.")]
        [Min(1f)] public float Lifetime = 10f;

        [Tooltip("How long the shrinking away takes, in seconds, at the end of its life.")]
        [Min(0.1f)] public float ShrinkTime = 1.5f;

        [Tooltip("How long the pop-in takes, in seconds: the splat grows from nothing to a little too big and settles. 0 = appears at once.")]
        [Min(0f)] public float PopTime = 0.14f;

        [Tooltip("How far the pop-in overshoots, as a fraction of the final size. 0.25 = it swells to about 125% before settling. 0 = no overshoot.")]
        [Range(0f, 1f)] public float PopOvershoot = 0.25f;

        [Tooltip("The most splats alive at once, in the whole scene. When full, the oldest is reused for the next one. Lower = cheaper, and old blood disappears sooner.")]
        [Min(4)] public int MaxLiveSplats = 48;

        [Header("Drops in the air")]
        [Tooltip("How many drops fly off the spot that was hit, for a pistol-strength hit. Harder hits throw more. 0 = none.")]
        [Min(0)] public int DropsFromWound = 14;

        [Tooltip("How many drops are shaken off the rest of the body, for a pistol-strength hit. 0 = only the spot that was hit bleeds.")]
        [Min(0)] public int DropsFromBody = 6;

        [Tooltip("How many times more drops a melee hit throws than a shot of the same strength. 1 = the same.")]
        [Range(0.5f, 4f)] public float MeleeDropsBoost = 1.5f;

        [Tooltip("The most drops one hit may throw, however hard it was.")]
        [Min(1)] public int MaxDropsPerHit = 60;

        [Tooltip("How fast the drops leave the spot that was hit, in metres per second. Higher = they fly further before falling.")]
        [Min(0.1f)] public float DropSpeed = 5f;

        [Tooltip("How wide the drops fan out around the way the hit was travelling, in degrees. A melee hit fans twice as wide.")]
        [Range(0f, 85f)] public float DropSpread = 30f;

        [Tooltip("The share of the drops that spray back toward the attacker instead of onward. 0 = all fly onward, 0.5 = half each way.")]
        [Range(0f, 1f)] public float BackSpray = 0.35f;

        [Tooltip("How big a drop is, in metres across. Each one varies a little around this.")]
        [Min(0.005f)] public float DropSize = 0.05f;

        [Tooltip("How long a drop lasts, in seconds, shrinking as it goes.")]
        [Min(0.05f)] public float DropLife = 0.55f;

        [Tooltip("How hard the drops fall, as a multiple of normal gravity. Higher = a tighter, heavier arc. Read when the game starts.")]
        [Min(0f)] public float DropGravity = 1.5f;
    }
}
