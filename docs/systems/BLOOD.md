<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Sonnet 5.5 (Claude Code)
  Generated : 2026-11-15
  Prompt    : Harvey: "create a blood splatter shader that fits in within the games artstyle
              that paints around objects and ontop of them when hit by these attacks."
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : The owning doc for blood splats (AGENTS.md §7): what the player sees, who owns
              what, where the files are, how to tune it, and what has not been checked.
-->
# Blood

**Status:** Current (written but not yet compiled or judged in play) · **Last updated:** 2026-11-15

When a bullet or a melee blow lands on a player or the test dummy, flat cartoon blood splats onto the walls, desks and props behind them and one splat lands on the floor under them.
Each splat is a blobby main shape with satellite drops and a few streaks thrown the way the hit was travelling, one red and one darker cut tone on the side facing the floor, with a thin dark rim.
It is meant to be funny, so it is slightly oversized.
Splats pop in with a small overshoot, stay for a while, then shrink away.
Thrown props and explosions do not make blood.

The look follows the art bible's characters: flat colour, one hard-edged shadow step, no gradients, no gloss ([CHARACTER_SHADER.md](CHARACTER_SHADER.md)).

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced? |
|---|---|---|
| Whether a hit happened, and how hard | The hit code (host decides for players) | Not by this system |
| The splats | Each machine, locally | **No.** Cosmetic only. |

Every machine hears the hit through `HitFeed.Struck` and draws its own splats, so the same hit looks a little different on two machines (each picks its own random shape).
A splat has no collider and nothing reads it back: it cannot affect gameplay.

## How it works

- Each splat is an ordinary cube drawn by the shader `Close the Deal/Blood Splat`. The shader reads the camera depth texture, works out where each pixel's surface is inside the cube, and paints the splat there. So it covers whatever is inside the box: floor, wall, a desk in front of the wall, a prop. The URP Decal feature is not enabled in this project and is not needed.
- It skips surfaces that face away from the cube (the facing is worked out from the depth buffer), so blood does not smear through a thin wall onto the far side, or streak along surfaces that are edge-on.
- `BloodSplatSystem` makes itself when the first scene loads (a hidden object that survives scene loads), listens to `HitFeed.Struck`, and for each Bullet or Melee hit casts rays from the hit point along the hit direction and a few more inside a cone. Each ray finds the nearest surface that is not the victim and not on the Player, Viewmodel or UI layers, and a splat box is placed there looking into the surface. One more ray goes straight down for the floor splat.
- Splats are pooled, drawn as one instanced batch, and capped; at the cap the oldest is reused.

## Files

- Shader: [BloodSplat.shader](../../Assets/_Project/Art/Blood/BloodSplat.shader), `Close the Deal/Blood Splat`.
- Runtime: [BloodSplatSystem.cs](../../Assets/_Project/Scripts/Effects/BloodSplatSystem.cs) and [BloodProfile.cs](../../Assets/_Project/Scripts/Effects/BloodProfile.cs), namespace `CloseTheDeal.Effects`.
- Tool: `Close the Deal > Art > Build Blood Assets` ([BloodAssets.cs](../../Assets/_Project/Editor/Art/BloodAssets.cs)) writes `Assets/_Project/Art/Blood/Resources/BloodSplat.mat` and `BloodProfile.asset` if missing. It never overwrites an edited material, and never rebuilds the profile once it exists. Run it once before making a build, because the build needs the material in a Resources folder. In the Editor the blood works without it, using the profile defaults.
- Test: `Close the Deal > Art > Spawn Test Blood (Play Mode)` ([BloodTestSplats.cs](../../Assets/_Project/Editor/Art/BloodTestSplats.cs)) fires a pistol-strength, a melee and a very hard hit at the surface the camera faces.
- Feed it listens to: [HitFeed.cs](../../Assets/_Project/Scripts/Player/HitFeed.cs); see [HIT_REACTIONS.md](HIT_REACTIONS.md).

## Tuning

Edit `Assets/_Project/Art/Blood/Resources/BloodProfile.asset`; each value has a tooltip.

| Value | What it does |
|---|---|
| Blood colour, Cut colour | The red and the one darker step. |
| Base size, Reference impulse, Max size, Size variation | Splat width in metres. Size grows with the square root of the hit's strength, up to the maximum. |
| Depth | Thickness of the projection box, metres. Thicker wraps further around props but can reach through thin geometry. |
| Bullet / Melee splats, Extra splats per 100 impulse, Max splats per hit | How many splats a hit throws behind the victim. |
| Bullet / Melee cone, Cone bonus per 100 impulse | Half-angle of the spray, degrees. |
| Reach | How far behind the victim blood can land, metres. |
| Bullet / Melee stretch | How long the streaks are, 0 to 1. |
| Floor size, Floor reach | The splat under the victim; 0 turns it off. |
| Lifetime, Shrink time | Seconds at full size, then seconds shrinking away. |
| Pop time, Pop overshoot | The quick swell when a splat appears. |
| Max live splats | The cap for the whole scene; the oldest is reused when full. |

The shader's own Inspector values (rim width and strength, how far the cut tone reaches in, how sideways a surface may be and still get blood) are on `BloodSplat.mat`.

## Known limitations

- **Characters are skipped, held items are not.** The character shader ([CharacterToon.shader](../../Assets/_Project/Art/Toon/CharacterToon.shader)) marks stencil bit 8 on every pixel a character covers and the splat shader does not draw there, so a body walking through a splat stays clean. Anything else that moves through a splat's box still wears it while inside: a held gun, a carried prop, the eye cards if they ever poke outside the head. Not yet seen in play.
- **Splats do not move with what they landed on.** A splat on a prop stays in the air when the prop is thrown, and paints whatever then passes through it.
- **Surface facing is read from the depth buffer**, so it is noisy for a pixel or two at the edge of a depth jump (the edge of a desk against the far wall).
- **Not compatible with the SRP Batcher.** The per-splat values need GPU instancing, so the material shows as not batcher-compatible; the splats batch as one instanced draw instead.
- **Needs the camera depth texture** (on in `PC_RPAsset`). Without it nothing draws.
- **A build needs the assets.** Run `Build Blood Assets` first, so the material is in a Resources folder and the shader is included.
- **Not checked:** compile, the look, Play Mode, a client's view, cost with many splats, and how it behaves under the first-person viewmodel camera.
