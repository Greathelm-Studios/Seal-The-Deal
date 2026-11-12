# Greybox review scene

**Status:** Current · **Last updated:** 2026-11-11

A Blender review scene of the game's current floor, used to judge environment art at player eye height and across the gap before the modular kit is cut.
It copies the floor the greybox tool builds in Unity ([TOWER.md](../../docs/systems/TOWER.md)): 28 × 18 m floors, 4.5 m floor to floor, a 12 m gap, and central spiral stairs with a cage and locked door.
It shows two facing towers, three floors each, with dark 1.8 m player-sized stand-ins (pivot at the feet).
It is not a Unity asset and replaces no floor template.

The `.py` files are the source. The `.blend` and `.build.json` are outputs; rebuild them rather than editing them.

## Build and render

Uses the `blender-modeling` skill's tool (`mdl.mjs`) and Blender 4.5+ (built and run with 5.3.0).

```bash
node <skill>/scripts/mdl.mjs build ArtSource/Greybox/floor_pair_greybox.py --out ArtSource/Greybox/floor_pair_greybox.blend
node <skill>/scripts/mdl.mjs check --blend ArtSource/Greybox/floor_pair_greybox.blend
blender -b ArtSource/Greybox/floor_pair_greybox.blend -P ArtSource/Greybox/review_views.py -- <out folder> --measure
```

`review_views.py` renders 8 views: inside tower A on the route to the stairs, from A's meeting room, A looking across, B looking at A's stair entrance from the same floor, from a bay off to the side, from one floor above and one floor below, and an overview.
Run it with no options for the recommended look. Its options (`--glass-alpha`, `--glass-reflect`, `--interior`, `--sun`, `--palette`, `--no-facade-glass`) reproduce the comparisons below.

`--measure` scores how well the stand-ins on A's test floor read from tower B. Each B view is rendered again without them, and the score is the average change in brightness where a suit shows (0 = invisible, higher = stands out more).
It measures contrast only, not whether a person would spot the suit.

## Façade glass

The repo's floors have open window bays with no glass. This scene adds a visual-only pane per bay (`FACADE_GLASS`) to test how glazing affects the view across the gap. A rival looks through two panes: their own window and yours.

Scores from the same floor (`B2_sees_A_stairs`), one floor above (`B3`) and one floor below (`B1`):

| Variant | Glass opacity | Ceiling light (W/m²) | Sun | Walls | Same floor | Above | Below |
|---|---|---|---|---|---|---|---|
| First pass | 0.18 | 0 | across the street | greybox grey | 0.030 | 0.044 | — |
| Fainter glass only | 0.08 | 0 | across | grey | 0.052 | 0.082 | — |
| Light only | 0.18 | 6 | along | grey | 0.061 | 0.062 | — |
| Both | 0.08 | 6 | along | grey | 0.099 | 0.075 | — |
| Both, no sky reflection | 0.08 | 6 | along | grey | 0.099 | 0.075 | — |
| Open bays | none | 6 | along | grey | 0.208 | 0.109 | — |
| **Recommended** | **0.05** | **10** | **along** | **bible white** | **0.241** | **0.135** | **0.031** |
| Recommended, open bays | none | 10 | along | bible white | 0.353 | 0.197 | — |

What changed the read, largest effect first:

1. **Pale walls behind the suits.** Swapping the greybox's mid-grey walls for the art bible's Paper White (#F3F4F4) did most of the work. The bible asks for this anyway: players should read as dark figures against bright interiors.
2. **A bright interior.** With the room darker than the street, even faint glass covers it with a pale film. Ceiling light at about the level of the daylight outside fixes that.
3. **Very faint glass.** Two panes at 0.18 opacity cut contrast to about a third of the open bays. At 0.05 the glass costs about a third of the contrast.
4. **Sun along the street.** A sun across the street always leaves one tower's front in its own shadow.

The glass's sky reflection made no measurable difference in this render setup. Unity's reflections may behave differently and need checking there.

## Still open

- **From the floor below, the rival can't see your stair entrance** under any material setting: your slab and sill are in the way. Only the geometry or the design can change that.
- **From the same floor, the 1 m sill hides each suit below the chest** at the stairs. The sill is also cover, so this is a design trade-off, not an art fix.
- **The slab undersides render dark** because the review light only points down. White ceilings, as in the references, need ambient or bounce light in Unity.
- **The floor is untested as dark carpet.** The references use grey-blue carpet, which will put dark suits against a dark floor in views from above. Test it before choosing the carpet's value.
- **Nothing here has been checked in Unity.** Opacity, light levels and the sun direction are Blender starting points. URP glass, light units and tonemapping all need retuning against the same views in Unity.
