# Blood

**Status:** Current (seen in play as host on one machine; restyled to the art bible on 2026-11-15, not yet reviewed by Alex's art sessions or tested on a client) · **Last updated:** 2026-11-21

## What the player sees

When a bullet or a melee blow lands on a player or test dummy, flat cartoon blood splats onto the walls, desks, and props behind them, one splat lands on the floor beneath them, and chunky square drops fly off the wound and body for about half a second.
Each splat picks one of four sharp graphic shapes: a **splash** (spiked core fanned along the hit, ~40%), a **slash** (two or three long strokes, ~20%), a **spray** (cone of thinning dots, ~20%), or a **blot** (lumpy patch with vertical drips when on a wall, ~20%).
Splats snap to a 1.4 cm world-space pixel grid (skipping the grid when smaller than ~1.5 screen pixels to prevent shimmer) and follow the art bible's two-tone character rule: deep cool crimson where the main light reaches, colder darker plum in shade, with a hard edge and no outline.
Splats pop in with a brief overshoot, hold for ~10 s, and shrink away so the building returns to clean. Thrown props and explosions do not make blood.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced? |
|---|---|---|
| Whether a hit happened and how hard | The hit code (host decides for players) | Not by this system |
| The splats and air drops | Each machine, locally | **No.** Cosmetic only. |

Every machine hears the hit via `HitFeed.Struck` and draws its own splats and drops. Neither touches physics or gameplay, and per-machine shapes differ slightly due to local random seeds.

## Architecture and design rationale

### Self-creation and lifecycle

Both `BloodSplatSystem` and `BloodDropBurst` self-create at `RuntimeInitializeLoadType.AfterSceneLoad` (`DontDestroyOnLoad`) whenever a graphics device is present (skipping headless/batch servers). No scene or prefab needs wiring. Both clear active visuals on `SceneManager.sceneUnloaded` and reset statics via `SubsystemRegistration`.

### Surface-wrapping splat projection (`BloodSplatSystem` & `BloodSplat.shader`)

- **Why a cube projector instead of quads or URP Decals:** The URP Decal Renderer Feature is not enabled in this project's shared renderer assets. Instead, each splat is a unit cube (borrowed once from `GameObject.CreatePrimitive(PrimitiveType.Cube)` so every build is guaranteed to include it) placed facing into the hit surface (`SurfaceOffsetFraction = 0.2` of `Depth` in front of the wall so it wraps over desks/props in front while keeping margin behind for depth-buffer precision).
- **Camera-inside-box rendering (`Cull Front`, `ZTest Always`):** Rendering back faces with depth test disabled ensures the splat volume still projects onto surfaces inside the box even when the camera steps inside the cube.
- **Shader culling & GPU helper-lane safety:** `BloodSplat.shader` evaluates `ddx`/`ddy` and `fwidth` before any `discard` so quad helper lanes stay valid. It then early-clips sky depth and outside-box coordinates before running normal reconstruction, angle fade (`_AngleCut`), bounding-circle reject (`maxRadius = 1.28 + aa`), or procedural SDF/shadow math.
- **Character stencil exclusion:** `Stencil { Ref 8 ReadMask 8 Comp NotEqual }` skips pixels marked by `CharacterToon.shader` so living characters walking through a splat box do not wear wall blood.
- **Lit vs. shaded cut tone:** Defines `_SURFACE_TYPE_TRANSPARENT 1` so URP's `GetMainLight` reads the shadow map directly rather than the opaque screen-space shadow texture.
- **Surface raycasts & pooling:** `BloodSplatSystem` excludes `Player`, `Ragdoll`, `Viewmodel`, and `UI` layers so the victim's collider does not eat rays, skips hits beyond `MaxSplatDistance` (60 m) from the camera, manages a FIFO ring buffer (`MaxLiveSplats`) + reusable `Splat` pool, uses an ease-out-back `PopCurve` (`c1 = overshoot * 12`), and writes per-splat `_Color`, `_CutColor`, and `_Params` via a single reused `MaterialPropertyBlock` with GPU instancing (`enableInstancing = true` on `BloodSplat.mat`).

### Air drops (`BloodDropBurst` & `BloodDrop.shader`)

- **Single shared `ParticleSystem`:** Uses one world-space stretched `ParticleSystem` (`StretchPerSpeed = 0.025 s`, zero world collisions) rather than per-hit particle objects or physics queries.
- **Eye-safety and distance culling:** Skips drops spawned within `NearEye` (0.5 m) of the camera so first-person hits do not block the lens, and skips bursts beyond `MaxDropDistance` (60 m).
- **Opaque sorting (`BloodDrop.shader`):** Uses `Opaque` (`Queue = Geometry+10`) so drops depth-sort cleanly against the struck body without transparency sorting overhead, while defining `_SURFACE_TYPE_TRANSPARENT 1` so drops take `_CutColor` when passing through main-light shadow.

## Files

- Shaders: [BloodSplat.shader](../../Assets/_Project/Art/Blood/BloodSplat.shader) (`"Close the Deal/Blood Splat"`), [BloodDrop.shader](../../Assets/_Project/Art/Blood/BloodDrop.shader) (`"Close the Deal/Blood Drop"`).
- Runtime: [BloodSplatSystem.cs](../../Assets/_Project/Scripts/Effects/BloodSplatSystem.cs), [BloodDropBurst.cs](../../Assets/_Project/Scripts/Effects/BloodDropBurst.cs), [BloodProfile.cs](../../Assets/_Project/Scripts/Effects/BloodProfile.cs) (namespace `CloseTheDeal.Effects`).
- Editor asset builder: `Close the Deal > Art > Build Blood Assets` ([BloodAssets.cs](../../Assets/_Project/Editor/Art/BloodAssets.cs)) writes `Assets/_Project/Art/Blood/Resources/BloodSplat.mat`, `BloodDrop.mat`, and `BloodProfile.asset`, protected by `GeneratedAssetGuard` (never overwrites hand-edited materials or an existing `BloodProfile.asset`). Building a standalone player requires running this once so the materials/shaders are included via `Resources`.
- Editor test helper: `Close the Deal > Art > Spawn Test Blood (Play Mode)` ([BloodTestSplats.cs](../../Assets/_Project/Editor/Art/BloodTestSplats.cs)) fires pistol, melee, and heavy test hits at the surface in front of the camera.
- Feed: [HitFeed.cs](../../Assets/_Project/Scripts/Player/HitFeed.cs) (see [HIT_REACTIONS.md](HIT_REACTIONS.md)).

## Tuning

Edit `Assets/_Project/Art/Blood/Resources/BloodProfile.asset` (every field has an Inspector tooltip covering colours, splat size/depth/spray counts/cones/reach/stretch, floor splat, pop/shrink life, `MaxLiveSplats`, and air drop counts/speed/spread/back-spray/gravity). Shader-level look sliders (`_PixelSize`, `_RimColor`, `_RimWidth`, `_AngleCut`) live on `BloodSplat.mat`; shape variant probabilities (`Splash` 40%, `Slash` 20%, `Spray` 20%, `Blot` 20%) are fixed in `BloodSplat.shader`.

## Known limitations

- **Glass takes no blood.** Transparent glass does not write depth, so hits against windows paint only the frame and geometry behind within the box.
- **Characters are excluded, held items are not.** Stencil bit 8 keeps character bodies clean, but held guns, carried props, or eye cards protruding past the body can wear a splat while inside its box.
- **Splats stay in world space.** A splat on a movable prop remains in the air when the prop is thrown.
- **Depth-derivative normal noise.** Reconstructing surface normals from the depth buffer causes 1 to 2 px edge noise along sharp depth discontinuities (such as a desk edge against a far wall).
- **Requires camera depth texture** (enabled in `PC_RPAsset`). Without it, splats draw nothing.
- **Not SRP Batcher compatible.** Per-splat `MaterialPropertyBlock` data uses GPU instancing instead.
- **Unverified in multiplayer/build.** Client views, first-person viewmodel interaction, and high-splat-count frame times have not yet been tested.
