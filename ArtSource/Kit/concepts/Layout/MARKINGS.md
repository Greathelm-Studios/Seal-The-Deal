<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-15
  Prompt    : Alex on the L2 KitOffice build: "looks good but bland and boring, the markings don't fully go round
              the office floor and ceiling, I don't want them randomly placed, I want them to follow the structure
              with purpose and intent, and have different markings that match our art style that add purpose and
              intent narratively and gameplay-wise"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : A markings system for the L2 floor: every floor, wall and ceiling mark has a job (where am I,
              where do I go, what is this, whose is this), follows the building's structure, and runs unbroken.
              It replaces decoration placed piece by piece.
-->
# L2 markings system

**From:** Alex, drafted by Claude · **Date:** 2026-11-15 · **For:** the KitOffice L2 build · Read with [DECISION_L2.md](DECISION_L2.md) and the L2 plans.

## Alex's feedback on the L2 build

**"Looks good but bland and boring."** The markings don't go all the way round the floor and ceiling, and they look randomly placed. He wants them to **follow the structure with purpose and intent**, with **different kinds of marking** in our art style that add purpose **narratively and for gameplay**.

What the build does now (KitOfficeScene.cs): the cobalt floor and ceiling ribbons cover one 16 m stretch of the west ring (`RibbonCells`, x 4–8). Signs and lightboxes are placed per room. So the marks start and stop in mid-air, and nothing tells you why they're there.

## Rules for every mark

1. **Every mark has one job.** Each one answers a question: *where am I*, *where does this go*, *what is this*, *whose is this* or *what can I do here*. If it answers none, cut it.
2. **Marks follow the structure.** They run along the 4 m grid, the ring, wall bases, ceiling edges and core faces. Each one **starts and ends at something solid**: a column, a wall end, a door, a core corner or a stair. Nothing floats in the middle of a floor or ceiling.
3. **Continuous where the thing it marks is continuous.** The ring is one loop, so its mark is one unbroken loop on the floor *and* the ceiling, all the way round. A district runs to its doorways, so its colour runs to them too.
4. **Floor and ceiling agree.** Every floor route has a ceiling partner directly above it (a light line, a cove or a soffit band), so the route reads whether you look down or ahead.
5. **One family, several voices.** The look is unchanged (glossy white, cobalt, navy, silver, cyan only as a thin light line, the locked-door red for "blocked"). No cyan floor light trails (DECISION_r5) and no bubbles. Graphics are textures or decals, never modelled.

## The six kinds of marking

### 1. The ring: *where does this go*
- **Floor:** one continuous inlay band in **glossy navy with a thin silver edge**, about 300 mm wide, flush, running the whole ring (L2 flow's pale-blue loop), round every corner and through every node. It is not lit; it's a material inlay like PANLINE's floor bands.
- **Ceiling:** directly above, one continuous **light cove or slot** following the same path, the main "lit route" you see ahead of you.
- At each node (N1–N6 on the flow plan) the band **widens into a landing pad**: a rounded rectangle with the node's name in microtype ("LIFT LOBBY", "A ENTRY", "COURT").

### 2. Districts: *where am I*
- Each wing (L, R, C, A, plus H for the court) owns **one accent treatment that wraps floor, wall and ceiling together**: a broad glossy cobalt or district-tint band at wall base and soffit, the giant zone letter on its most visible wall, and its name and Japanese line as one sign family.
- **Threshold strips:** where a route crosses into a district (a doorway, a ring corner), a 600 mm **threshold band** in that district's treatment runs wall to wall across the floor, like an airport gate line, with the letter set in it. You always know the moment you cross into a district.
- The accent stops exactly at the district's doorways and wall ends.

### 3. Firm territory: *whose is this* (narrative and gameplay)
- **Story:** two rival law firms share one floor plate: a lease dispute made architecture. Each firm's half carries its brand on the **tenancy line**, a crisp brand-coloured inlay across the court that marks the legal boundary between the two leases. On it, in tiny deadpan type: "DEMISE LINE · TENANCY A | TENANCY B · NO CROSSING WITHOUT APPOINTMENT".
- Each firm's entry corner has its **firm wordmark lightbox** (the reception wall) and its brand colour in the threshold strip. Players know from the floor which half they're on and which way home is.
- For gameplay this is the "middle": the tenancy line runs through the court past the briefcase start, so "crossing the line" is the push.
- Firm colours come from `FirmDefinition` (MATCH.md: neutral marker colours per firm; the HUD's cyan and amber are per player and stay off the floor).

### 4. Objective and vertical: *what can I do here*
- **The deal plinth:** the briefcase start in the court gets concentric glossy inlay rings and a fine radial tick scale ("CLOSING TABLE"), the floor's bullseye, visible from the bridge above.
- **Gate lines at every stair arrival** (MATCH floor gates): a 300 mm **locked-door red** band with hairline chevrons across the open side of each stair cage, with "FLOOR 3 · OPENS WITH THE CASE" microtype. Red means blocked everywhere in the game, so the band matches the frosted gate panel.
- **Vertical supergraphics:** each enclosed stair (E1, E2) and the feature stair carry the **floor number** at room height on the core wall at every landing, and on the stair door. The feature stair's stringer gets one cobalt band that **continues up through every floor** on the same face: the building's spine.
- **Lift:** the lift lobby floor gets a navy pad with the floor number; the lift doors carry the number in big type.

### 5. Real office safety marks: *believable, and they help read the space*
These are what make a real office feel real, and each one also helps play:
- **Evacuation plan decals** ("YOU ARE HERE") at each stair door and in the lift lobby, showing **the actual L2 plan** with the zones lettered: a minimap in the world.
- **Glazing manifestation:** frosted dot or line bands on glass walls at about 0.9–1.0 m and 1.5 m (a real code requirement). On **breakable glass** use one pattern (fine frosted dots); on fixed glass use the frame and no dots. Players learn which glass breaks.
- **Edge marks:** a thin navy and silver edge strip along the atrium void edge, bridge edges and stair nosings (real slip-and-edge contrast), so drops and jump-offs read at speed.
- **Door and room plates:** every door has a room plate (number, name and Japanese line) at the handle side. Every room's name doubles as its callout.

### 6. Ceiling structure: *the ceiling explains the floor*
- Ceiling light lines **run unbroken edge to edge along the grid**, wall to wall, never stopping mid-ceiling.
- Floating panels sit **only over rooms and work zones**, aligned to the 4 m grid and centred on them, so a panel means "a room is here". The ring keeps its cove (kind 1), and the court is open to the bridge and its glazed roof.
- Each district's soffit band from kind 2 closes its ceiling edge.

## Where this leaves the current ribbons

Keep the **sweeping cobalt ribbon** as a *landmark*, not a route: one ribbon per firm entry, sweeping from the entry corner's reception wall down to that firm's threshold strip. So it means "this firm's front door", and it starts and ends at structure. Remove the floating 16 m ring segment, which kind 1 replaces.

## Build notes (for the scene session)

- Build marks from the L2 data (`L2/layouts.json`: ring path, nodes, districts, doors, stair arrivals, case start), so they follow the plan exactly and update if the plan changes. Don't place them by hand piece by piece.
- Floor bands and inlays: flush decals or a floor-layer mesh with the glossy floor material (keep floor reflections and SSAO working). Ceiling coves: the existing light-strip family.
- Keep the loose-prop and performance budgets; marks are static.
- Show Alex captures of: the ring corner at a node, a district threshold, the court with the tenancy line and plinth, a stair arrival with its gate line and floor number, and a "you are here" plan.
