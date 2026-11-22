# Glass

**Status:** Current (visual sheen not judged in play; breaking verified in shooting lane self-check, and on the host in KitOffice) · **Last updated:** 2026-11-22

## What the player sees

Glass is almost clear, with a pair of hard diagonal light streaks, one wide and one thin, drawn across each pane.
The streaks slide over the pane as the viewer walks past, the way a reflection would, and the glass goes pale and more solid when seen nearly edge-on.
Nothing is actually reflected: the art bible rules out mirror glass because players have to read each other through two panes.

**Glass breaks.** A bullet passes straight through a pane and shatters it on the way; a blast shatters every pane in its radius; a chair, a desk, or a thrown player that hits a pane fast enough goes through it and keeps most of its speed.
A broken pane stays broken for the rest of the match and blocks nothing: people, bullets, and furniture all pass. A rematch (new towers) starts with every pane whole, on every machine.
The building shell (slabs, sills, pillars, walls) never breaks.

## Breaking: who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Which panes are broken | **Host.** Only the host decides a pane breaks | A set of pane numbers on the scene's `GlassRegistry` (`SyncHashSet<uint>`): ~5 B per break; full set to late joiners | Only when a pane breaks |
| The panes themselves | Every machine, locally (scene objects, or built with towers from the seed) | Nothing | Local only |
| Shards | Every machine makes its own | Nothing, never networked | Local only |

**Deterministic pane IDs (`GlassRegistry`):** Scene panes (outside `NetworkObject` roots) are numbered sequentially in scene order. Tower panes are indexed after each machine builds from the seed using a 32-bit packed ID: bit 31 = `AgainstNormalBit` (shard flight direction), bit 30 = `TowerBit`, bits 24 to 29 = building index, bits 12 to 23 = floor index (max 64 floors), bits 0 to 11 = pane index on that floor (max 4096 panes/floor).

- **Late joiners:** `OnStartClient` sets `_liveFromFrame = Time.frameCount + 1` and applies existing `_broken` entries with `live: false` so holes appear immediately without spawning bursts of old shards.
- **Rematches (`OnTowerCleared`):** When towers tear down for a rematch, the host clears `_broken` (resetting scene glass too) and every client drops tower IDs locally at once so re-built towers do not re-break stale IDs while the clear is in flight.
- **Client prediction trade-off:** On a client, a pane disappears only when the host's sync arrives, so for a few tens of milliseconds its own body can still bump a pane the host has already broken.

## Architecture and design rationale

### What counts as glass & runtime collider fitting

- Any object with a `GlassPane` component (using its assigned renderers/colliders, or scanning children on `Awake`).
- Any active renderer using a material listed in `GlassProfile.GlassMaterials` (today only `Glass_Clear`). `GlassRegistry.AddMaterialPanes` attaches `GlassPane` via `InitKnown` (skipping redundant `Awake` child scans) and picks the pane's collider in this order:
  1. **A collider on the glass object itself** (greybox templates).
  2. **The kit piece's collider.** Kit pieces keep colliders on separate `*_Collider` objects beside the drawn meshes, so `TakePieceColliders` looks up to three levels above the glass for a collider with no renderer that overlaps the glass's sheet. One that covers 90 % of the pane is taken alone (a partition's or curtain wall's whole-wall box, a guard's glass box, a stair guard's hull). Failing that, every collider lying at least half inside the pane is taken (the two panels either side of a glass door; the doorway stays open and the head rail stays solid). Anything else drawn in the same sheet (a frost band, the lift shaft's ribbon) breaks with the pane.
  3. **A fitted box**, if `SolidifyGlassWithoutCollider` is enabled, from drawn `renderer.bounds` for panes thinner than `MaxPaneThickness` (0.15 m), when the piece has no collider for its glass (the kit window, whose only collider is its sill). The thickness check skips curved meshes whose world-aligned box would fill the space they wrap, and reading `renderer.bounds` avoids mesh-collider issues after tower static merging.
- A collider taken from another object gets a `GlassImpactRelay`, which passes physics hits on to the pane so a thrown body still breaks it.
- **A scene needs a `GlassRegistry`** (a "Glass" object with a `NetworkObject`) or none of its glass can break. The shooting and integration lanes and KitOffice have one; the KitOffice builder places it.
- Glass under a spawned `NetworkObject` (such as a prop) is not supported because its ID would not be deterministic across machines.

In KitOffice this makes 155 panes: glass, frost and header partitions, glass doors' side panels, curtain walls, windows, guards, stair guards and the lift shaft's glass wall. Fluted and louvre partitions use other materials and stay solid.

### Shards and sheen shader

- **`GlassShards`:** Pools local `ParticleSystem` bursts (`MaxActiveBursts = 32`, `MaxPooledPerPrefab = 24`), emitting `ceil(width * height * ShardsPerSquareMetre)` particles capped at `MaxShards` without allocating during sustained fire.
- **`GlassSheen.shader` (`"Seal the Deal/Glass Sheen"`):** Unlit, transparent, casts no shadows so sunlight enters rooms untouched. Uses drawn diagonal streaks rather than reflection probes or planar reflections so sky/room reflections never obscure players across the street. Uses `Cull Back` because kit panes are thin closed boxes (drawing both faces would double streak opacity). Derives a horizontal `along` vector from `normalWS` with a deterministic sign flip so both sides of a pane show matching streaks, falling back to world X for horizontal skylights/floors (`abs(normalWS.y) >= 0.9`).

## Tuning

### Breaking ([DefaultGlass.asset](../../Assets/_Project/Profiles/Glass/DefaultGlass.asset))

| Inspector value | What it does |
|---|---|
| Glass Materials | Materials treated as breakable glass automatically. |
| Solidify Glass Without Collider | Adds fitted box colliders to flat material-matched panes that lack colliders. |
| Break Speed | Head-on speed required to smash through a pane (m/s; default 10, above 9 m/s sprint). |
| Min Break Mass | Lightest rigidbody mass that can break a pane by impact (kg). |
| Speed Kept | Fraction of speed kept after smashing through. |
| Through Control Loss Seconds | Steering lock duration after smashing through. |
| Shard Effect, Shards Per Square Metre, Max Shards, Shard Speed | Local shard particle burst tuning. |

### Visuals ([Glass_Clear.mat](../../Assets/_Project/Art/Kit/Materials/Glass_Clear.mat))

| Inspector value | What it does |
|---|---|
| Glass tint (`_BaseColor`) | Tint and base opacity (keep alpha near 0.05 for cross-street readability). |
| Streak colour (`_SheenColor`) | Streak colour and opacity. |
| Streak tilt (`_SheenAngle`) | Degrees from vertical. |
| Distance between streak pairs (`_SheenSpacing`) | Metres between pairs along the glass. |
| Width of the wide / thin streak (`_SheenWide`, `_SheenThin`) | Metres; set thin to 0 for a single streak. |
| How far the streaks slide (`_SheenSlide`) | Parallax shift as the viewer moves; 0 = painted on. |
| Edge colour & power (`_EdgeColor`, `_EdgePower`) | Grazing-angle pale edge. |

## For other systems

- Glass implements `CloseTheDeal.Core.IDamageable`: any damage on the host breaks the pane.
- `GlassRegistry.BreakInRadius(point, radius)` on the host breaks all panes inside a blast.
- `GlassPane.AnyBroken(pane, direction)` fires on every machine on live breaks (not late-join sync).

## Files

- Shader: [GlassSheen.shader](../../Assets/_Project/Art/Glass/GlassSheen.shader)
- Material: [Glass_Clear.mat](../../Assets/_Project/Art/Kit/Materials/Glass_Clear.mat), assigned on import by [KitImport.cs](../../Assets/_Project/Editor/Art/KitImport.cs)
- Scripts: [Assets/_Project/Scripts/Glass/](../../Assets/_Project/Scripts/Glass/) (`GlassPane.cs`, `GlassRegistry.cs`, `GlassImpactRelay.cs`, `GlassProfile.cs`, `GlassShards.cs`)
- Prefab: [Assets/_Project/Prefabs/Glass/](../../Assets/_Project/Prefabs/Glass/)

## Known limitations

- Only panes placed in the scene are indexed. Indexing for buildings made at runtime went with the two-tower mode on 2026-11-22, so glass on the floor race's generated floors does not break ([FLOOR_GEN.md](FLOOR_GEN.md)); the pane-numbering and rematch notes above that mention towers describe that removed half.
- **Kit glass colliders are chosen or added at runtime.** Fitted axis-aligned boxes are exact for quarter-turn panes and slightly oversized for angled panes. Curved panes thicker than 0.15 m get neither a piece collider nor a fitted box (`Partition_Header_Curve_2x2` does not break). The lasting fix is authoring glass colliders during kit import.
- **A broken kit pane takes its frame's collision with it.** Most kit pieces have one box for frame and glass together, so once the pane breaks the frame is still drawn but blocks nothing: bullets and bodies pass through a curtain wall's mullions and a header partition's white band above 2.5 m. Guards with their own glass box (`Guard_Glass_*`) keep a solid shoe. The lasting fix is the same: a separate glass collider per piece at kit import.
- **Not yet seen from a client in KitOffice.** The sync is the same set of pane numbers the lanes use; only the host was checked there.
- **Whole-pane destruction only.** No partial holes or cracked states.
- **Shards are flat squares that fall through floors.**
- **Body break distance:** A body thrown from closer than ~1.3 m to a window hits the 0.6 m sill before reaching the glass (see [WEAPONS.md](WEAPONS.md)).
- **Non-box collider bounds:** Panes using non-box or child colliders measure shard size from world-aligned bounds, which can skew if rotated.
- **Sheen visibility:** Streaks can briefly cross behind a player seen across the street, and `Cull Back` assumes panes are thin closed boxes rather than single-sided sheets.
- **Visual sheen unjudged in Play Mode:** Motion parallax and curved stair-cage panels have only been inspected in still renders.
