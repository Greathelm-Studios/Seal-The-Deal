<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-15
  Prompt    : Alex on L1: "take all the positives of each and try to negate the weakness as much as possible"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : Round-L2 brief: one hybrid office floor that combines L1_A, L1_B and L1_C's strengths and designs
              out their weaknesses, checked against the game mode now on main (docs/systems/MATCH.md).
-->
# Office floor layout, round L2: one hybrid

**From:** Alex, drafted by Claude · **Date:** 2026-11-15 · Read with [BRIEF.md](BRIEF.md) (still applies) and your L1 output.

## Alex's direction

> "Take all the positives of each and try to negate the weaknesses as much as possible."

## What to keep and what to fix

| Option | Keep | Fix |
|---|---|---|
| **L1_A, central core and ring** | The most believable: a solid core everyone understands, and **one continuous ring you can always find again** | The core **hides the middle**: low visibility and no readable contested mid |
| **L1_B, edge core and daylight court** | **The clearest read:** arrive from the lift lobby and the court with the feature stair is straight ahead. A strong landmark and mid, compact 40 × 28 m, each firm enters from its own corner, daylight wings | **A wall of sealed dead space** (risers, WC, plant), and **both enclosed stairs on the far wall**, far from both firms and predictable |
| **L1_C, split cores and forum** | **Two cores, so each side has a nearby enclosed stair:** balanced flanks, roomier districts, a shared forum, room for 4v4 | **Big and slow:** 48 m across, long rotations between fights |

## Starting hypothesis (beat it if you can)

A **twin-core court**:

- B's **daylight court** in the middle, with the **feature stair** as the mid and the landmark, and the **lift lobby** on one edge of the court for B's clear arrival.
- C's **two slim cores**, one either side of the court, each holding **one enclosed stair** plus WC and riser. So each firm has a near flank stair, and the two stairs are rotationally symmetric.
- A's **continuous ring** wraps the court and both cores, and is the route you always find again.
- **No sealed dead space** facing play: core faces carry real, enterable rooms with two doors (print and copy, server, archive store, kitchen), or glazing into the court. Only small risers stay sealed.
- The **court stays visible** from the ring through glass and openings, so the middle is never hidden, but give it hard cover so it isn't a shooting gallery from the upper floor.
- **Plate size between B and C:** about 40 × 32 m. Justify the time to cross and the rotation times at 9 m/s against L1_C.

## Layout reference: CS Agency (Alex, 2026-11-15)

Alex: "a good reference might be the map Agency from CS:GO in terms of layout". Agency (cs_agency, by Patrick Murphy and Rick Underhill) is a hostage map set in "the highly polished confines of a modern corporate skyscraper": the Underhill & Murphy advertising agency's penthouse. Valve's own description is "the precision of classic bomb defusal layouts ... in a modern skyscraper". Overview checked: https://static.wikia.nocookie.net/cswikia/images/e/ea/Agency_cs2_overview.png (CS2 radar), plus the Counter-Strike wiki page.

What to take from its layout. This is structure only; it's Valve and community content, so copy no geometry.

- **Open versus dense contrast.** One side is a big open, double-height entrance hall (CT side: lobby, lifts, a feature stair and a glass bridge over it). The other side is a **dense cluster of small rooms** (T side: offices, storage, conference room, back room, kitchen). The fight is about pushing from the open into the dense. In our symmetric mode, read it as an open court in the middle with denser office wings at each firm's side.
- **Two or three parallel routes** from the open hall into the dense wing, roughly a left corridor, a central connector and a right route through rooms. A classic bomb-defusal three-lane read inside an office.
- **Every space is named by a real office function** players use as callouts: Main Hall, Side Hall (security with metal detectors), Conference, Storage, Back Room, Window Hallway, Kitchen. Believability and callouts come from the same names.
- **Landmarks inside the space:** revolving billboards in the Main Hall, the metal detectors in the Side Hall, the glass bridge in the centre, a broken lift by the entry.
- **Two levels joined in the open hall** (stair plus a glass bridge), with most of the dense fighting on one level.
- **Objective rooms sit deep in the dense wing,** with more than one way in.

Apply these to the hybrid: the court is our Main Hall (open, double height, feature stair, a glass bridge across at the upper level), the firm wings are the dense, room-rich halves, three named routes link each wing to the court, and every room has a real name that doubles as a callout.

## The game mode now on main (`docs/systems/MATCH.md`: read it)

The implemented mode is two firms, **one shared briefcase**, **floor gates**: a frosted panel closes the open side of the stair cage at every stair arrival above the open floor, and the carrier opens it. A 30 m beacon on the briefcase, checkpoint floors that move a firm's respawn, and a roof win. It is still built as **two towers with a plaza and skybridges**, while Alex wants **one large office**. Design the floor so it works for both:

- every stair arrival (feature stair and enclosed stairs) has a **clear gate line**: one open side to close
- the briefcase start at the court, visible from both firm entries
- spawn and reception at opposite corners with rotational symmetry
- note in the README where a skybridge would attach if the two-tower version stays

Don't decide the one-building vs two-tower question; flag it for Adam.

## Deliver (keep it tight: Alex is waiting)

Write only in `ArtSource/Kit/concepts/Layout/`. Reuse your L1 drawing code. Keep L1 files untouched.

1. **`L2/plan.png`, `L2/flow.png`, `L2/stack.png`:** the same standards as L1: code-drawn, exact scale, lanes, nodes, landmarks, sightlines with lengths, firm spawns, briefcase start and routes up, gate lines marked.
2. **`L2/view.png`:** one eye-level mood image of the court from a firm's entry (image generation).
3. **`L2/README.md`:** short. A table of each L1 strength kept and each L1 weakness fixed (and how), stats (size, circulation %, rotation times, longest sightlines, rooms with two exits, loose props), MATCH.md fit, open questions for Adam.
4. **`contact_sheet_L2.png`:** L2 plan and flow beside L1_A, L1_B and L1_C's plans for comparison.

Do the plans first, the view last. Don't redo the L1 READMEs.
