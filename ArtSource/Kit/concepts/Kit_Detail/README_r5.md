# Office labyrinth kit detail · Round 5

**Status:** Target concepts for Alex's review · **Last updated:** 2026-11-12

[Contact sheet](contact_sheet_r5.png) · [Governing brief](FEEDBACK_05.md) · [Style notes](FEEDBACK_04.md) · [Exact prompts](prompts_r5.json)

Five families, ten directions, **20 interior views and five top-down sketches**, made with the built-in **image_gen.imagegen** tool in **six image tries**.

Each family sheet puts **A left / B right, hero above / play below**, with a one-line caption per direction and a shared route sketch underneath.
All cameras were requested at **1,700 mm above their local walking surface**.
Atrium A's play camera is on the mezzanine; the other play cameras are on the lower floor.
These are generated concept paintings, not engine captures or measured drawings; the proposed dimensions below govern a build.

## Direction and source review

[FEEDBACK_05.md](FEEDBACK_05.md) governs layout and supersession; FEEDBACK_04 supplies the retained glossy, glass-heavy, abstract style.
The game is one interior office labyrinth with short combat loops, readable corners, branching routes and quick vertical links.
STRAFTAT informs pacing/map flow only through the brief; no layout or visual asset was copied.

The round-4 contact sheet and complete README were reviewed before generation, along with the art-bible text, **all four bible references, all three office plates, the floor-plan sketch and suit reference**.
Bible references 01–03 inform negative space, sky inserts and graphic restraint; 04 informs large white/cobalt masses.
The office plates inform materials, glass and scale; their carpet, clutter, open tower floor plates and cross-street composition are superseded.
The suit reference informs angular silhouettes, long limbs and restrained cel shadows.

**A** develops round 4's capsules, sky lenses, curved glass and high frosted strips into glossy game space.
**B** reduces each family to abstract planes or masses with enough office identity, cover and navigation to play.
There are no street insets, rival towers, repeated floor 32, repeated red doors or repeated helixes.

**Suggested build mix: corridors A / junctions B / rooms A / atrium B / glass B.**
This is a recommendation for review, not approval or implementation of a new kit.

## Shared build boundaries

Dimensions below are **millimetres** unless explicitly marked metres.
Sketch distances are **proposed layout targets**, not observed gameplay measurements.
Each sketch describes topology and approximate placement; A/B variants share the route diagram rather than matching every painted camera exactly.

- Keep the **4,000 / 2,000 grid**, front-left floor-level wall/slab pivots, thickness toward local −Z, flat snap faces and 90-degree assembly turns.
- Keep existing walls **200 thick**, soffit **4,300**, storey **4,500**, slab **200**.
- Keep ordinary doors **1,200 × 2,400** and the **1,180 × 2,380 × 40** leaf with its hinge pivot.
- Open **2,000 or 4,000 corridor mouths** omit wall modules; they are not widened doors.
- Keep flat partitions **4,000 / 2,000 × 100 deep × 2,500 high**, with **1,800 open air above**.
- Keep spiral core / tread / handrail-path / cage radii **800 / 2,600 / 2,630 / 2,750** and centre pivot.
- Keep window bounds and **600 sill** at selected outer routes; windows no longer determine the whole floor plan.

**All proposed applied detail is flush or recessed: new proud detail is 0 mm**, below the 15 mm maximum.
Flush means the original exposed plane; recessed means inward from it.
A cover block's stated size is its whole proposed envelope, not a 15 mm wall detail.
Preserve flat snap/contact lands and the remaining skins listed below; do not combine opposing cuts through a thin piece.

Large rounded corners require a **proposed 2,000 × 2,000 return module**, carved inside its grid-cell bounds, with flat end lands and the same floor-level pivot convention.
A curved-glass return also needs a new 2 × 2 m cell with flat end connections; do not bend it out of an existing 100-deep flat panel's envelope.
Existing 4 m/2 m flat pieces retain their bounds.
Clear route widths must be maintained after placing new returns, cover or guards.

**Height exception proposed only for family 04:** omit the middle slab over the atrium void, retain the upper deck at **4,500**, and retain its normal soffit at **8,800 above the lower floor**.
Adjacent rooms and occupied areas beneath the mezzanine still have **4,300 clear**.
There is no half-storey underneath a normal 4.3 m ceiling, and no new ordinary door size is proposed.

## Material proposals

These are later build targets, not approved material assets or shader work.

| Finish | Proposal |
| --- | --- |
| Gloss white lacquer | Paper White #F3F4F4; smoothness about **0.90**, non-metallic, clean broad highlights. |
| Gloss cobalt | Corporate Cobalt #244FB9; smoothness about **0.94**; full-height fields/returns, with pale backing behind enemies. |
| Polished pale resin | Pale blue-grey/white; smoothness **0.92–0.97**; flush cobalt inlays; softly blurred architecture/figure reflections as visual targets. |
| Inset silver | Smoothness about **0.72**, quiet joints/treads; no opaque bars across players. |
| Clear glass | Near-clear tint, approximately **0.05 base alpha**; one broad and one thin translucent diagonal streak; pale grazing edge; **no mirror image**. |
| Frosted glass | Separate diffuse header **2,100–2,500** or base below **450**; proposed low band stops at **350**. |
| Light/sky | Recessed white coves, cyan only a **10-wide** inlay, restrained bloom; flat sky prints above combat height. |

Glass follows the supplied [glass specification](../../../../docs/systems/GLASS.md).
Keep the **450–2,100** region unobstructed and figures readable through **two panes**.
A frosted header identifies a boundary; it is not full-body concealment or hard cover.
An intact pane shows enemies but blocks the walking route; a **cleared break opening** has no pane or continuous threshold rail.
The paintings do not determine first-shot penetration, shots-to-break or debris lifetime; those belong to the shooting lane.

The current Unity material still named **Floor_Carpet** already has no base texture, colour **#B8C2CE** and smoothness **0.97**.
That historical name is an import mapping, not evidence that the live material is carpet.
Current baked floor probes omit moving players, so painted figure reflections remain an **explicit visual proposal**.
Reuse the polished floor as a starting point; this pass adds no reflection system.

## 01 · Corridors

[Sheet](01_Corridors/sheet_r5.png) · [Top-down](01_Corridors/layout_r5.png) · **1 image try**

### A — Capsule loop

**Idea:** a capsule-lit corner and tall cobalt niche make the next turn recognisable.

- **Kit:** existing Slab_*, Wall_Solid_4m/2m, cobalt variants, Partition_Glass_4m/2m and selected Window_4m/2m; proposed RoundedReturn_2x2, curved-glass return, capsule cove and low counter.
- **Form:** niche **600 × 3,400**, end radius **300**, **recessed 60** in a 200 wall, leaving **140 skin**; silver **recessed 2**; sky print **recessed 20** above **2,600**; cove **recessed 80** in the slab, leaving **120 skin**; floor colour **flush 0**; cyan **10 wide**, **recessed 2**, filled flush.
- **Materials:** gloss white/cobalt, polished resin, clear glass with high frost, inset silver and printed sky.
- **Play:** **16 × 12 m section**, **4 m main route / 2 m open bypass**, reconnecting around the island; **4–8 m target exposed views**; **1.1 m counter** outside circulation; full-height returns stop the next sightline.
- **Navigate/read:** **L / LEGAL capsule**, cobalt floor field and thin guide turning toward the hub; white backing preserves the glass-side enemy silhouette.

### B — Offset planes

**Idea:** staggered planes make the turn itself the landmark.

- **Kit:** the same flat slabs/walls/partitions; proposed low block counter and flush L graphic; no curved-return mesh.
- **Form:** stagger returns **2,000** on the grid; L/colour **flush 0**; silver **recessed 2**; ceiling slot **recessed 40**, leaving **160 slab skin**; cyan as A; no fins or raised letters.
- **Materials:** glossy planar white/cobalt, resin, near-clear glass and separate high frost.
- **Play:** shared loop and **2 m branch**, **4–8 m views**, **1.1 m counter** and full-height returns; offsets prevent a single uninterrupted tunnel.
- **Navigate/read:** storey-height flush **L**, asymmetrical returns and an open branch; keep the enemy's slot against white.

**Build A:** its capsule corner carries the era and the route without requiring small text.
B supplies the cheaper flat-wall connector.

## 02 · Junctions and doors

[Sheet](02_Junctions_Doors/sheet_r5.png) · [Top-down](02_Junctions_Doors/layout_r5.png) · **1 image try**

### A — Sky-lens T

**Idea:** a high sky lens and unlike corner treatments distinguish the branches.

- **Kit:** existing Wall_Door_4m, Door_Leaf, cobalt walls, partitions and slabs; proposed sky-lens finish, optional rounded return and capsule counter.
- **Form:** sky insert **3,600 × 800**, above **2,800**, radius **400**, **recessed 40**, leaving **160 wall skin**; door reveal **recessed 15**; silver **recessed 2**; small corner finish **recessed at most 80**, retaining **120 skin**; graphics **flush 0**.
- **Materials:** white/cobalt gloss, sky print, polished resin, clear corner/high frost and silver door.
- **Play:** **4 m T approach**, left/right branches, secondary **1.2 m door**; **4–8 m views**; **1.1 m counter** and full-height solid return.
- **Navigate/read:** **R / REVIEW**, sky lens, cobalt left return and glass right corner; the door stays subordinate to the route.

### B — Split X

**Idea:** offset exits reveal short peeks instead of one four-way shooting tunnel.

- **Kit:** existing 2 m/4 m walls, doors, partitions and slabs; proposed flush R graphic and low block; no special X mesh.
- **Form:** offset opposite exits **2,000**; cove **recessed 40**, silver **recessed 2**, letter and colour **flush 0**; ordinary door stays **1,200 × 2,400**.
- **Materials:** gloss white/cobalt, pale resin, near-clear glass, high frost and quiet silver.
- **Play:** **4 m approach / 2–4 m open branches**, reconnecting around wall islands; **4–8 m views**, **1.1 m cover**, full-height return cover.
- **Navigate/read:** large flush **R**, one cobalt terminus, a clear corner and a ceiling line toward the next hub; white backs the opponent.

**Build B:** it controls simultaneous angles with existing flat pieces.
The sketch's dashed T cap is A's alternate termination; B opens that arm and staggers the returns.

## 03 · Rooms and cover

[Sheet](03_Rooms_Cover/sheet_r5.png) · [Top-down](03_Rooms_Cover/layout_r5.png) · **1 image try**

### A — Capsule meeting room

**Idea:** one solid capsule desk anchors a compact room loop.

- **Kit:** existing wall/door/slab/flat partition modules; proposed Counter_Capsule_2m, high frosted capsule lintel and optional curved-glass corner; one simple chair outside circulation.
- **Form:** counter **2,000 × 1,000 × 1,100 high**, end radius **500**, solid to the floor; top joint **recessed 2**, filled flush; high lintel at **2,100–2,500**, cut **recessed 30** inside 100 depth, leaving **70 skin**; sky print **recessed 20** above **2,600**; codes **flush 0**.
- **Materials:** glossy white counter, cobalt rear plane, pale resin, clear glass/high frost and silver.
- **Play:** **8 × 8 m room**, **2 m outer loop**, two **1.2 m doors** on adjacent sides; **4–8 m views**; **1.1 m crouch-cover target**, a **2 m-long full-height return**, and at least **2 m clear circulation** around the desk.
- **Navigate/read:** **C / CLAIMS** high lintel and capsule silhouette; glass reveals the enemy beside the solid return, without office clutter.

### B — Negative-space office

**Idea:** reduce the room to one low monolith, one hard-cover return and a C.

- **Kit:** same flat room shell; proposed Counter_Monolith_4m and flush C graphic.
- **Form:** counter **4,000 × 1,000 × 1,100 high**; optional radius-**400**, maximum **250-deep inward scoop** on the non-combat end only; preserve the solid cover-facing body; top joint **recessed 2**, filled flush; code/colour **flush 0**; frost above **2,100**.
- **Materials:** gloss white/cobalt, polished resin and clear glass/high frost; no equipment or chairs.
- **Play:** same **8 × 8 m room / 2 m outer loop / adjacent exits**, **4–8 m views**; the longer **1.1 m block** splits circulation while leaving both ends open; standing cover remains full-height.
- **Navigate/read:** large flush **C**, asymmetrical cover and visible secondary door; white backs the glass-side figure.

**Build A:** its shorter counter preserves circulation and a recognisable meeting-room cue.
B's long counter is an alternative footprint, not an extra block beside A.

## 04 · Atrium and vertical

[Sheet](04_Atrium_Vertical/sheet_r5.png) · [Top-down](04_Atrium_Vertical/layout_r5.png) · **2 image tries**

### A — Capsule spiral

**Idea:** the retained spiral is an arrival landmark behind transparent balconies.

- **Kit:** existing Stair_Flight, Stair_Steps, Stair_Column, Stair_CagePanel and stairwell slab where its hole fits; proposed capsule core finish, mezzanine-edge slabs, frameless guards and counters.
- **Form:** core niche **1,000 × 3,000**, radius **500**, **recessed 120 radially** from radius **800** to **680**; silver **recessed 2**; cove **recessed 80**, leaving **120 slab skin**; H/landing field **flush 0**; remove opaque handrail tubes from the visual variant, retaining walking geometry and original radii/path.
- **Materials:** white/cobalt gloss core, silver treads, near-clear curved cage/guards and polished resin; no projecting halo or opaque guard bars.
- **Play:** **16 × 16 m double-height hub**, upper deck **4.5 m**, **8–12 m principal views** broken by piers; two lower flank routes reconnect; **1.1 m counters**; the spiral radial annulus is **1.8 m before edge clearances**, not a claimed 2 m-wide lane.
- **Navigate/read:** **H / HEARINGS capsule**, visible treads, cobalt arrival field and upper return doors; clear guards preserve the enemy silhouette.

### B — Folded forum

**Idea:** broad stair, terraces, short ramp and drop give the hub quick reconnects.

- **Kit:** existing plain/cobalt walls and slabs; proposed Stair_Straight_2m in a **4 × 8 m bay**, mezzanine-edge slabs, frameless guards, Ramp_2x4, low shelf and block cover.
- **Form:** proposed stair clear width **2,000**, rise **4,500**; **24 risers × 187.5** and **300 goings** are a modelling starting point, with remaining run reserved for connections/landings; resolve final flight fit in module drawings.
- **Form continued:** ramp **2,000 wide × 4,000 horizontal run**, rise **1,000**, slope **1:4**, to a shelf with one open drop edge; graphics **flush 0**, slot **recessed 40**, guard fixings recessed within new slab-edge bounds and below **350 above the local deck**.
- **Materials:** glossy white/cobalt planes, resin, almost-clear stair/balcony guards and inset silver; no opaque horizontal/diagonal handrail tubes.
- **Play:** **16 × 16 m hub**, upper deck **4.5 m**, **8–12 m views**, full-height piers and **1.1 m cover**; **2 m stair** reaches the upper loop; ramp and **1 m drop** make a separate lower reconnect; upper routes rejoin the labyrinth.
- **Navigate/read:** high cobalt open-square/**H** graphic, visible stair silhouette and thin ramp guide.
- **Boundary:** slope locomotion, drop collision, flight fit and any fall-damage rule are proposals, not validated or decided by the artwork.

**Build B:** the wide direct stair is the stronger main fast link.
A remains the existing-kit alternative or secondary authored landmark.
The sketch overlays A's circle and B's stair bay as alternatives, not a requirement to build both.

## 05 · Glass walls

[Sheet](05_Glass_Walls/sheet_r5.png) · [Top-down](05_Glass_Walls/layout_r5.png) · **1 image try**

### A — Skyglass suite

**Idea:** high frosted capsules name the room above almost-clear combat panes.

- **Kit:** existing flat partitions, solid/cobalt walls, slabs and doors; proposed high frosted inserts, optional curved return and break-ready **2 m pane module**.
- **Form:** high capsule **3,000 × 300**, wholly within **2,100–2,500**, face **recessed 30** in 100-deep header; silver **recessed 2**; low frost stops at **350**; pane stays at its original plane; marks **flush 0**; cleared gap has no raised threshold or base rail.
- **Materials:** near-clear two-streak glass, separate high/low frost, gloss white/cobalt, resin and silver.
- **Play:** **12 × 12 m section**, **2 m bypass**, **4–8 m views**, **1.1 m counter** and full-height returns; two panes reveal threats; depicted **2 m cleared break opening** joins the bypass.
- **Navigate/read:** **A / ARCHIVE** lintel and cobalt terminus; faint sheen/edge means intact pane, absence of pane/foot rail means opened route; fragments indicate state, not a debris implementation.

### B — Transparency cuts

**Idea:** solid, visible-but-closed and visible-open cells form a simple combat alphabet.

- **Kit:** existing 2 m/4 m walls, partitions and slabs; proposed high frosted strip, low block and break-ready pane variant; no complicated special wall mesh.
- **Form:** alternate **2,000-wide cells**; solid **4,300 high**, partition **2,500 high**, frost **2,100–2,500 / 0–350**; silver **recessed 2**, slot **recessed 40**, A/colour **flush 0**; no body-height frost or crossbar.
- **Materials:** the same clear/frosted/solid distinction as A, using planar white/cobalt and resin without a capsule lens.
- **Play:** **12 × 12 m section**, **2 m bypass**, **4–8 m views**, **1.1 m block** and full-height returns; a cleared pane opens a flank that rejoins an already viable loop.
- **Navigate/read:** giant flush terminal **A**, staggered piers and distinct closed/open cells; white enemy backing and a two-pane visibility target.

**Build B:** alternating simple bays explain the route states most clearly.
Use A's lintel selectively for departmental identity.

## Existing kit items to reconsider

These are handoff flags, not changes made to the kit.

| Existing item/assumption | Labyrinth handoff |
| --- | --- |
| Rival façade, street trays, three-floor exterior composition | Selected outer window routes replace the mandatory street read; exterior detailing no longer leads this kit. |
| Carpet/tiled-ceiling source language | Unity's floor is already polished, but the kit README/source texture workflow still describe carpet and ceiling tiles; retain polished mapping and propose flat coved ceilings. |
| One portal/floor index per tower floor | Zones need distinct landmarks; L/R/C/H/A are proposed department identities, not a new global numbering rule. |
| Frame-wide partition collider | The source builder places collision over the whole partition on its frame; the shooting lane must coordinate pane, frame/threshold and collision removal for a walkable break opening. |
| Fixed stair cage/silver handrail | The colliding cage and opaque rail need review for transparent combat guards; this pass does not decide destructibility or collision policy. |
| Full-height locked red stair leaf | It belongs to earlier progression gating and can obstruct fast loops; this pass neither assumes it on every combat stair nor removes its game rule. |
| Spiral as sole vertical link | The 1.8 m radial annulus is less direct than the proposed main stair; wide stairs, mezzanine edges, ramp and drop remain proposed modules. |
| 600-high window sill | Keep at outer windows; it is below the requested 1.0–1.2 m crouch-cover range and cannot be the sole interior cover piece. |
| No dedicated clean low-cover family | 1.1 m capsule/block counters need explicit meshes/colliders; open-legged desks do not provide the painted cover. |
| Baked floor reflections | Moving figures are omitted currently; painted figure reflections are a target, not an implemented effect/performance claim. |

The kit README's tower layout/projectile assumptions are older than FEEDBACK_05.
**FEEDBACK_05 is authoritative for these concepts**; projectile interaction stays with the shooting lane.
No gameplay, model, prefab, collider, material, shader or netcode implementation was changed.
No system setup changes were made, and the write-only-here instruction excludes updates to boards/system docs elsewhere.

## Image tries and review

| Family | Initial calls | Revisions | Total tries |
| --- | ---: | ---: | ---: |
| Corridors | 1 | 0 | 1 |
| Junctions/doors | 1 | 0 | 1 |
| Rooms/cover | 1 | 0 | 1 |
| Atrium/vertical | 1 | 1 | 2 |
| Glass walls | 1 | 0 | 1 |
| **Total** | **5** | **1** | **6** |

Each initial call generated one four-panel image.
The sixth call removed the atrium's opaque handrails crossing figures, cleared its glass and clarified the inward cove.
The discarded [atrium draft](04_Atrium_Vertical/try_01_r5.png) is retained; selected originals are views_r5.png in each family folder.
[Prompts](prompts_r5.json) record every attempt and reference role.
[Layout data](layouts_r5.json) and [composition script](compose_r5.ps1) reproduce the captions, sketches and sheets.
Deterministic sheet/sketch composition does not add ImageGen tries or substitute for the generated artwork.

All generated families and assembled sheets were visually reviewed for A/B differences, hero/play positions, close cover, partially revealed enemies, landmarks, polished floors, restrained sky and interior routes.
The atrium revision was inspected for removal of the offending opaque rail tubes.
Layout review cleared two openings where route arrows initially crossed intact glass.

**Painting limits:** cyan lines, highlights and glass streaks are exaggerated; a few cove edges read broader than their intended recess.
Build the **10 mm line**, inward coves and near-clear two-streak glass specified here, not the painted pixel width.
The glass-family frost is subtle; the stated **2,100–2,500 / 0–350** inserts govern a build.
Camera height, exact dimensions, cover feel, curve radii, stair fit and two-pane visibility cannot be certified from these perspective stills.
Partly concealed figures illustrate the visibility target, not a measured gameplay result.

## Artifact validation

Executed: **& 'ArtSource/Kit/concepts/Kit_Detail/compose_r5.ps1'**.
Result: five family sheets, five layout PNGs and one contact sheet composed successfully.

PNG dimensions are read with **System.Drawing.Image.FromFile**.
**SHA-256** hashes are compared against the pre-generation snapshot of **48 existing non-log, non-r5 files**.
Exact commands, preservation results and image metadata are recorded in [validation_r5.json](validation_r5.json).

- Family sheets: **2560 × 2480**.
- Contact sheet: **3200 × 2960**.
- Generated source images: **1536 × 1024**.
- Layout sketches: **1100 × 460**.

No automated tests, Unity compile, Play Mode, multiplayer, movement-feel or performance validation was run for this artwork task.
No state-changing git command was used.
All task-authored deliverables stay in this folder tree; rounds 1–4 and the supplied briefs remain unchanged.

**Next:** Alex reviews the A / B / A / B / B mix before kit modelling.

