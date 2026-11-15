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

**Status:** Current (seen in play as host on one machine; restyled to the art bible on 2026-11-15, not yet answered by Alex's art sessions) · **Last updated:** 2026-11-15

When a bullet or a melee blow lands on a player or the test dummy, flat cartoon blood splats onto the walls, desks and props behind them and one splat lands on the floor under them.
Each splat is one sharp graphic shape, and there are four kinds so a row of hits is not one stamp repeated: a **splash** (a small core with pointed spikes fanned the way the hit was travelling, about four in ten), a **slash** (two or three long thin strokes), a **spray** (a cone of dots thinning out along the throw) and a **blot** (a fat lumpy patch, which hangs two or three drips when it lands on a wall), about two in ten each.
Splats are drawn on a grid of small squares, 1.4 cm across in the world whatever the splat's size, so up close their edges step like the pixelled clouds in the sky; from across a room the grid is too fine to see.
It is a deep, cool crimson where the main light reaches the surface and a colder, darker plum where the surface is in shade, with a hard edge between the two and no outline: the same "base colour, one hard cut shadow" rule as the characters ([art bible](../../ArtSource/Kit/concepts/Office/refs/art_bible.txt) §03, §05, §17, §20).
Splats shrink away after about ten seconds, because the bible wants effects to be crisp and brief and the building to return to clean.
The first version (bright red blobs with a dark rim and many droplets, lasting 25 seconds) was rejected by Harvey as out of place.
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
- Drops in the air: [BloodDropBurst.cs](../../Assets/_Project/Scripts/Effects/BloodDropBurst.cs) and [BloodDrop.shader](../../Assets/_Project/Art/Blood/BloodDrop.shader) (`Close the Deal/Blood Drop`), with `Resources/BloodDrop.mat` written by the same Build Blood Assets tool.
- Feed it listens to: [HitFeed.cs](../../Assets/_Project/Scripts/Player/HitFeed.cs); see [HIT_REACTIONS.md](HIT_REACTIONS.md).

## Tuning

Edit `Assets/_Project/Art/Blood/Resources/BloodProfile.asset`; each value has a tooltip.

| Value | What it does |
|---|---|
| Blood colour, Cut colour | The colour in the main light, and the colder, darker one in shade. |
| Base size, Reference impulse, Max size, Size variation | Splat width in metres. Size grows with the square root of the hit's strength, up to the maximum. |
| Depth | Thickness of the projection box, metres. Thicker wraps further around props but can reach through thin geometry. |
| Bullet / Melee splats, Extra splats per 100 impulse, Max splats per hit | How many splats a hit throws behind the victim. |
| Bullet / Melee cone, Cone bonus per 100 impulse | Half-angle of the spray, degrees. |
| Reach | How far behind the victim blood can land, metres. |
| Bullet / Melee stretch | 0 fans the spikes all the way round; 1 throws them forward in a narrow fan. |
| Floor size, Floor reach | The splat under the victim; 0 turns it off. |
| Lifetime, Shrink time | Seconds at full size, then seconds shrinking away. |
| Pop time, Pop overshoot | The quick swell when a splat appears. |
| Max live splats | The cap for the whole scene; the oldest is reused when full. |
| Drops from wound, Drops from body | How many drops fly off the spot that was hit and off the rest of the body, for a pistol-strength hit. Harder hits throw more. |
| Melee drops boost | How many times more drops a melee hit throws. |
| Max drops per hit | The most one hit may throw. |
| Drop speed, Drop spread | How fast they leave the wound (m/s) and how wide they fan (degrees; melee fans twice as wide). |
| Back spray | The share thrown back toward the attacker instead of onward. |
| Drop size, Drop life | Metres across, and seconds before a drop has shrunk away. |
| Drop gravity | How hard they fall, as a multiple of normal gravity. Read when the game starts. |

## Drops in the air

When a body is hit by a shot or a blow, blood also flies off it for about half a second: most from the spot that was hit, fanned along the way the hit was travelling with a share sprayed back at the attacker, and a few shaken off the rest of the body between knee and shoulder.
Each drop is one flat square of the blood colour, stretched along its flight, so it reads as a chunky pixel streak beside the pixel-stepped splats; it takes the colder cut tone while it flies through shade.
The drops fall, shrink and vanish. They do not collide with anything and leave no mark themselves: the splats on the surfaces are placed separately, by rays, at the moment of the hit.
Local and cosmetic like the splats: nothing is synced, and one shared particle system draws every drop in the scene.
Seen in play as host on 2026-11-15, on the host's own body, for a shot and a melee hit.

The shader's own Inspector values (**Pixel size** in metres, 0 for smooth edges; an optional thin rim, off by default; how sideways a surface may be and still get blood) are on `BloodSplat.mat`.
How often each kind of splat appears is fixed in the shader (`Shape` in `BloodSplat.shader`), not on an asset.

## Known limitations

- **Glass takes no blood.** Windows and glass walls are see-through materials that the splat cannot find, so a hit against a window paints only its frame and whatever is behind the glass within the splat's box. Seen in play on 2026-11-15.

- **Characters are skipped, held items are not.** The character shader ([CharacterToon.shader](../../Assets/_Project/Art/Toon/CharacterToon.shader)) marks stencil bit 8 on every pixel a character covers and the splat shader does not draw there, so a body walking through a splat stays clean. Anything else that moves through a splat's box still wears it while inside: a held gun, a carried prop, the eye cards if they ever poke outside the head. Not yet seen in play.
- **Splats do not move with what they landed on.** A splat on a prop stays in the air when the prop is thrown, and paints whatever then passes through it.
- **Surface facing is read from the depth buffer**, so it is noisy for a pixel or two at the edge of a depth jump (the edge of a desk against the far wall).
- **Not compatible with the SRP Batcher.** The per-splat values need GPU instancing, so the material shows as not batcher-compatible; the splats batch as one instanced draw instead.
- **Needs the camera depth texture** (on in `PC_RPAsset`). Without it nothing draws.
- **A build needs the assets.** Run `Build Blood Assets` first, so the material is in a Resources folder and the shader is included.
- **Not checked:** compile, the look, Play Mode, a client's view, cost with many splats, and how it behaves under the first-person viewmodel camera.
