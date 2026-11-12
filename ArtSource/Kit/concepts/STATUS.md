# Concept status

**Window bay and columns** — Ready for Alex's review. Covers Window_4m, Window_2m, Column_Facade and the shared Column_CornerL / Column_CornerR profile.

[Sheet](Window_Bay/sheet.png) · [Front](Window_Bay/front.png) · [Side](Window_Bay/side.png) · [Three-quarter](Window_Bay/three_quarter.png) · [Section](Window_Bay/section.png) · [Notes](Window_Bay/notes.md) · [Render source](Window_Bay/render.ps1)

Choose A (Silver Grey) or B (Deep Navy). Verify handed corner placement and shared material assignments against the source kit. The corner is shown in isolation, and the street-side slab is drawn as context.

Hand-back stops after priority 1 for review.

## Window bay — Round 2 · 2026-11-11

**Ready for Alex's concept review:** three shaded design directions based on the actual kit script and FEEDBACK_01.md.

[All-direction comparison](Window_Bay_R2/comparison.png) · [Guide, dimensions and reproduction](Window_Bay_R2/README.md)

| Direction | Idea | Room/street A/B | Shaded turnaround | 2 m / corners | Notes |
| --- | --- | --- | --- | --- | --- |
| Dark Bands | Twin recessed pier scores and continuous navy street sill belts. | [Presentation](Window_Bay_R2/01_Dark_Bands/presentation.png) | [Sheet](Window_Bay_R2/01_Dark_Bands/sheet.png) | [Sheet](Window_Bay_R2/01_Dark_Bands/infill_corner_sheet.png) | [Notes](Window_Bay_R2/01_Dark_Bands/notes.md) |
| Lit Piers | Rounded white piers with recessed white light ribbons and silver sill caps. | [Presentation](Window_Bay_R2/02_Lit_Piers/presentation.png) | [Sheet](Window_Bay_R2/02_Lit_Piers/sheet.png) | [Sheet](Window_Bay_R2/02_Lit_Piers/infill_corner_sheet.png) | [Notes](Window_Bay_R2/02_Lit_Piers/notes.md) |
| Cobalt Corners | Large diagonal cobalt graphics on corner columns only. | [Presentation](Window_Bay_R2/03_Cobalt_Corners/presentation.png) | [Sheet](Window_Bay_R2/03_Cobalt_Corners/sheet.png) | [Sheet](Window_Bay_R2/03_Cobalt_Corners/infill_corner_sheet.png) | [Notes](Window_Bay_R2/03_Cobalt_Corners/notes.md) |

Each direction includes three-bay room heroes at 1.70 m eye height, a three-bay/two-floor street view from 12 m across the gap, navy scale figures behind glass, and silver/navy rail comparisons.
Turnarounds include orthographic front, side and mid-bay section plus shaded room three-quarter; an extra street three-quarter is retained separately.
Render source: [Blender script](Window_Bay_R2/render_concepts.py), [page compositor](Window_Bay_R2/compose_pages.ps1).

**Proposals:** Trim_Navy in all directions, Light_Strip for Lit Piers, Accent_Cobalt for Cobalt Corners; Alex approves direction, finish and new slots.
These are disposable render studies, with render-offset skins and illustrative bevels requiring proper snap lands and flush finish assignment in production.
Two-pane visibility is illustrated with a concept shader; production Unity visibility and gameplay are unverified.
Round 1's statement that source kit files were unavailable does not apply to this round.
Only Window_Bay_R2 and this appended status section were written; hand-back stops at the window bay.

## Window bay — Round 3 · 2026-11-11

**Two shortlisted concepts for Alex's review**, following FEEDBACK_02.md: [comparison board](Window_Bay_R3/comparison.png), [literal 300 px strip](Window_Bay_R3/thumbnail_strip.png), and [review guide / dimensions / reproduction / validation](Window_Bay_R3/README.md).

| Direction | Architectural move | Review files |
| --- | --- | --- |
| **White Trays — recommended** | 120 mm-deep sill pocket and 160 mm pier setback make broad white floor shelves over dark openings. | [Presentation](Window_Bay_R3/01_White_Trays/presentation.png), [dimensioned turnaround](Window_Bay_R3/01_White_Trays/sheet.png), [silver/navy rails](Window_Bay_R3/01_White_Trays/rail_finishes.png), [2 m / corners](Window_Bay_R3/01_White_Trays/infill_corner_sheet.png), [notes](Window_Bay_R3/01_White_Trays/notes.md) |
| **Blue Spandrel** | A full 1 m opaque backpainted-glass band crosses the street face of each sill and column base. | [Presentation](Window_Bay_R3/02_Blue_Spandrel/presentation.png), [dimensioned turnaround](Window_Bay_R3/02_Blue_Spandrel/sheet.png), [silver/navy rails](Window_Bay_R3/02_Blue_Spandrel/rail_finishes.png), [2 m / corners](Window_Bay_R3/02_Blue_Spandrel/infill_corner_sheet.png), [notes](Window_Bay_R3/02_Blue_Spandrel/notes.md) |

**Pick: White Trays with B / Deep Navy rails** for the strongest 2001 white-shelf silhouette while keeping Paper White dominant.
The third [Split Blades study](Window_Bay_R3/03_Split_Blades/notes.md) was culled because it still resembled White Trays in the 300 px rival thumbnail; its raw renders remain as process evidence.

Both shortlisted directions include the 12 m same-height rival view of three bays over three floors, two dark suits on the middle floor and one upstairs through target and observer panes, the 1.70 m room view with carpet and ceiling grid, a 2.67 m close profile, and shaded orthographic dimensions.
Raking street sun, cobalt sky, pale horizon haze and white/blue block-city context replace round 2's flat staging.
The final renders and thumbnail strip were visually inspected; suits remain near-black through both concept panes.

**Material proposals:** `Trim_Navy`, `Glass_Spandrel` for direction 02, and `Light_Strip` for ceiling staging only; no kit material contract was changed.
The concepts retain source dimensions and flat snap lands, use no proud additions, and keep continuous opaque sill backing; original colliders remain a future production constraint.
Source: [Blender geometry and renderer](Window_Bay_R3/render_concepts.py), [page compositor](Window_Bay_R3/compose_pages.ps1).
No Unity or gameplay validation is claimed, and no automated tests were run.
Writes are confined to Window_Bay_R3 and this new section; no state-changing git commands were run.
Hand-back stops at the window bay for Alex's review.
