# Office floor layout: brief

**From:** Alex, drafted by Claude · **Date:** 2026-11-15 · **For:** Codex layout options (round L1)

## The problem

Alex: the KitOffice scene is **"too confusing and things feel randomly placed with no actual believability"**. It must still serve our game design, movement and objectives, but feel believable and be easier to understand.

The current scene (`Assets/_Project/Editor/Art/KitOfficeScene.cs`, captures in `ArtSource/Kit/review/unity_office/`) is a 32 × 24 m, two-storey labyrinth assembled from the round-5 kit families: capsule loop, split X, capsule room, atrium, glass cuts. It was composed to show off kit pieces, not planned as a building, so nothing explains why a room is where it is.

## What the layout has to serve (Alex, 2026-11-15: "a mix of all these")

- **One office building, several stacked floors.** Two firms fight over **one shared briefcase** and carry it upward. Higher floors open as the case gets there, and reaching the roof with it wins. This is Adam's game-mode lane; details may still change.
- **Each floor also plays on its own as a fast close-quarters arena.** Fights happen on the floor, not only on the way up.
- **2v2 to 4v4** (up to 8 players), tuned for 2v2 first.
- **Movement:** first person, walk 6 m/s, sprint 9 m/s, 1.6 m jump, mantle ledges 0.4–1.6 m, Q/E lean, slides and fast arena feel (Adam's movement lane). Blasts throw players and props. STRAFTAT is the pace reference.
- **Combat:** guns, rockets, throwables, and any loose prop under 35 kg can be picked up and thrown (16 loose props per floor cap). Glass is breakable and never stops a projectile.
- **Look (unchanged):** the approved glossy 2001-corporate-future Aero direction (DECISION_r5, AEROSET and PANLINE notes): white glossy floor, glass, cobalt, lit signage.

## What the research says

Sources are listed at the end. These are the rules the options are judged on.

### 1. Believability: plan it like a real office first

Real office floors follow one well-known logic, and players read it without being taught:

- **A central core** holds the lifts, the enclosed escape stairs, toilets, risers and plant. It is solid, the same on every floor, and the floor wraps around it. A central core is the most common arrangement because it leaves the daylit perimeter for work.
- **Depth from window to core is 6–13.5 m** (BCO guide; a US federal guide gives about 12 m as the daylight optimum). Deeper than that and the middle goes dark.
- **Lift lobby to reception:** you arrive from the lifts into a lobby, then reception, then the floor. Travel from reception to the lifts is kept short.
- **Primary circulation is 15–22 % of the floor:** a clear main route, often a loop round the core.
- **Open-plan desks sit on the window side.** Enclosed rooms (meeting rooms, offices, quiet rooms, print and copy, server and store) sit on the inner side against the core, or in corners. Kitchen and break-out sit at a node people pass.
- **Multi-floor tenants add a feature stair:** an open interconnecting stair in a cut-out (a small atrium) that links their floors. It's the social heart and a landmark.
- **The shell repeats on every floor, while the fit-out varies.** Same core, same columns, same windows; each floor's rooms differ by department. This is exactly how our floor templates should vary, and why they'll still feel believable.

> Level-design caveat: real office plans "usually don't play well because they lack flow". Aim for **authenticity, not reality**: keep the recognisable logic (core, ring route, desks at the windows, rooms on the inside, a feature stair), then bend it for play.

### 2. Readability: give the player a mental map (Lynch, via Brian Upton's GDC talk)

- **Paths:** one main loop around the core that you can always find again.
- **Edges:** the window wall (always one side, always bright) and the core (always solid, always in the middle) mean you always know which way is "out" and which is "in".
- **Districts:** each quarter of the floor is one department with one identity: a zone letter and name, one accent treatment and its own fit-out. That's the existing L / R / C / H / A system, used as districts, not stickers.
- **Nodes:** the lift lobby and reception, the feature-stair landing and the kitchen. Every route passes through or near them.
- **Landmarks:** the feature stair, the reception lightbox wall and one big graphic per district, visible from far off. Avoid the "road to nowhere": every path ends at a node.
- Mirror's Edge's lesson: an off-white world with a few rationed accent colours where colour means something (red there meant "route"). Ours: cobalt and zone identity mean "where you are", and the locked-door red means "objective" or "blocked".

### 3. Multiplayer flow

- **Loops, not corridors.** A hallway gives one line of sight; loops let players move round each other. Good maps layer big loops with small loops inside them (Counter-Strike). The ring round the core is the big loop. Rooms with two doors and cut-throughs make the small loops.
- **Three lanes** between the two firms' sides (the Level Design Book's three-lane typology): for example the window-side open plan, the core-side ring corridor, and an interior route through rooms.
- **No dead ends.** Every room has two ways out (Epic's guidance: dead ends become death traps).
- **Sightlines:** mostly short (5–15 m, our close-quarters pace), with a few long ones (25 m+) along the window edge and across the atrium as deliberate risk. Glass walls give information without cover.
- **The middle decides fights:** a thesis on tactical FPS maps found the team winning the mid area usually wins. Make the briefcase's route cross a readable, contested mid (the feature stair and atrium) with flanks round it.
- **Symmetry for fairness:** rotational (point) symmetry between the two firms' spawn sides keeps it fair without looking mirrored and artificial.
- **Carrying an objective** (CTF practice): several routes in and out of the objective area, with one or two high-risk areas in the middle the carrier must cross. The carrier needs a fast exposed route and a slower covered one.

### 4. Verticality between floors

- Players need **several ways up and down**. Stairs alone become chokepoints.
- Our real-office answer is three ways up, each with its own risk:
  1. the **feature stair** in the atrium: open, fast, exposed, the main contested route
  2. the **escape stairs** in the core: enclosed, slower and safer, the flank. Fire code wants two.
  3. the **lift**: rare, loud, and it gives you away (GDD comebacks)
- Atriums: use one, with at least two floors overlooking it. Upper-floor shooters see down into it, so give the lower floor cover from above.
- Stair positions must **line up on every floor** (the shell is fixed).

## Fixed constraints

- Kit grid: 4 m bays and 2 m infills. 4.5 m floor to floor, 4.3 m to soffit, 0.2 m slab. Doors 1.2 × 2.4 m. Pivots and snap convention in `ArtSource/Kit/README.md`.
- Stairs need ramp collision. Ledges from 0.4 to 1.6 m can be mantled. Cover heights are about 1.1 m crouch and full height. 
- Window sill 0.6 m. Curtain-wall glass is allowed (AEROSET note).
- Up to 16 loose props per floor.
- Floor plate size is open: propose one between 32 × 24 m and 48 × 36 m and justify it for 2v2–4v4 at 6–9 m/s. The current 32 × 24 m is the starting point.

## What Codex delivers (round L1)

Write only inside `ArtSource/Kit/concepts/Layout/`. No state-changing git commands; no Unity or kit changes.

1. **`CURRENT_DIAGNOSIS.md`:** read `KitOfficeScene.cs` and the captures, and say concretely why the current scene reads as random and confusing, against the rules above.
2. **Three layout options** (`L1_A`, `L1_B`, `L1_C`), each a genuinely different answer: for example a central core with a ring; a core on one side with a big atrium; split cores with a central atrium. For each option:
   - **`plan.png`:** a clean top-down plan **drawn with code to exact scale** (SVG to PNG, 4 m grid visible, a metre scale bar), not image-generated. Show the core, stairs, lift, atrium void, walls (solid, glass, frosted), doors, rooms with names, the open plan, cover blocks and windows. Label districts with their zone letter and colour.
   - **`flow.png`:** the same plan with the main loop, three lanes, nodes, landmarks, both firms' spawn sides, the briefcase start and its routes up (feature stair, escape stairs, lift), and the five longest sightlines with lengths in metres.
   - **`stack.png`:** a simple section or exploded view showing how 3 floors plus the roof stack: the same core and stairs, different fit-out per floor.
   - **`view.png`:** one eye-level concept image (image generation) of the option's main node, in the approved look (glossy white floor, glass, cobalt, lit signage, less realistic), so Alex can feel the space.
   - **`README.md`:** the logic in a few lines: why each room is where a real office would put it, how it plays (loops, lanes, cover, mid, carrier routes), stats (floor size, circulation %, longest sightline, rooms with two exits, time to cross at 9 m/s), and which kit pieces it needs, new ones as proposals.
3. **`contact_sheet_L1.png`** with all three side by side, and **`README_L1.md`** comparing them, with your recommendation.

Draw plans with code so they are exact and readable. Use image generation only for the `view.png` mood images. Keep everything else in the art bible and DECISION_r5.

## Sources

- BCO Specification for Offices quick guide (floor depths, circulation): https://gt-website.files.svdcdn.com/production/publications/1906_BCO-Spec-Concertina.pdf
- UK Government Property Agency technical annex (core placement, heights): https://assets.publishing.service.gov.uk/media/6297a7eee90e070397a00fdd/GPA_Architectural_Technical_Annex_V1.0_Document_uploaded_via_email._A_Accepted_as_Stage_Complete_6.pdf
- Floor plate efficiency and central cores (vendor article, secondary): https://www.homestyler.com/article/maximizing-office-tower-floor-plate-efficiency?lang=ko_KR
- "Authenticity, not reality" and real-place maps lacking flow: https://filmstories.co.uk/features/the-essential-techniques-for-great-multiplayer-level-design/
- Lynch's five elements in level design (Brian Upton, GDC): https://gamedeveloper.com/game-platforms/post-gdc-i-rainbow-six-i-s-upton-talks-landscaping-game-worlds and https://gamedeveloper.com/design/realism-and-legibility-in-open-world-level-design
- Cognitive maps: https://www.skeletoncodemachine.com/p/maps-as-data
- Loops and FPS layout: https://critpoints.net/2018/02/18/good-fps-map-design/
- Layout typologies (three lane, hub and spoke, figure 8): The Level Design Book, layout/typology (read via a mirror)
- Dead ends, sightlines and landmarks: https://dev.epicgames.com/documentation/en-us/fortnite-creative/level-design-best-practices-in-fortnite-creative
- Mid-area control in tactical FPS (SMU thesis): https://scholar.smu.edu/guildhall_leveldesign_etds/41
- CTF routes and carrier risk: https://unrealarchive.org/wikis/unreal-wiki/Legacy:Mapping_For_CTF.html and https://unrealarchive.org/wikis/the-liandri-archives/Capture_the_Flag.html
- Vertical routes and atriums: https://unrealarchive.org/wikis/unreal-wiki/Legacy:Z-Axis.html and https://www.github.com/redeclipse/base/discussions/1165
- Mirror's Edge colour and wayfinding: https://www.worldofleveldesign.com/categories/game_environments_design/mirrors-edge-color.php
- Office floor tour (R6 Siege Skyscraper: a C-shaped floor round a central lift hall, three staircases): https://automaton-media.com/articles/impressionjp/20161121-34213/
- STRAFTAT (pace reference): https://www.pcgamer.com/games/fps/best-fps-2024-straftat
