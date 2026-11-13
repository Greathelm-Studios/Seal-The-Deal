<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-12
  Prompt    : Alex: "no carpet, i want a shiny floor, i want to try get rid of the 'unity look'"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : The owning doc for the polished office floor and its reflection probes (AGENTS.md §7):
              what the player sees, where the files are, how to re-bake, and what is unchecked.
-->
# Polished floor and reflections

**Status:** Current in KitOffice (not in the greybox floor templates; not judged in play) · **Last updated:** 2026-11-12

The office floor is a pale blue-grey polished resin instead of carpet.
It mirrors the room softly: windows, frames, the cobalt wall and the red door show in it, and sun patches land on it as bright blown-out shapes.
Players and anything else that moves are not reflected.

## Files

- Floor material: [Floor_Carpet.mat](../../Assets/_Project/Art/Kit/Materials/Floor_Carpet.mat), colour `#B8C2CE`, smoothness 0.97, no texture. The name stays `Floor_Carpet` because the exported kit models are mapped to it by name. [KitImport.cs](../../Assets/_Project/Editor/Art/KitImport.cs) builds it with these values.
- Probes: `Close the Deal > Art > Bake Floor Reflections` ([FloorReflections.cs](../../Assets/_Project/Editor/Art/FloorReflections.cs)) adds one box-projected reflection probe per `Floor N` under the `Kit Office` root, bakes each to `Assets/_Project/Art/Look/Probes/<scene>_Floor<N>.exr`, and keeps them in a `Reflection Probes` child of the `Kit Office` root, so they move with the building. Moving the building is fine; rotating it is not, because probe boxes stay aligned to the world. It also flags every renderer under `Kit Office` as Reflection Probe Static, because a baked probe only photographs flagged objects; without the flag each probe captured an empty sky. Players and other moving things are not flagged. It asks first and does not save the scene.

## Bounce light

Real-time lighting in URP has no bounce, so a bright sun patch left the ceiling above it unlit.
`Close the Deal > Art > Add Bounce Light` ([BounceLight.cs](../../Assets/_Project/Editor/Art/BounceLight.cs)) works out where sunlight through each window bay lands on each floor, from the scene sun's direction, and puts one soft, shadowless, upward spotlight there (strength 4, 21 lights in KitOffice). The lights live in a `Bounce Lights` child of the `Kit Office` root, so they move with it; re-run the tool if the sun or the building's rotation changes.
The ambient light in `Apply Office Look` is at 60% so the bounce can read: the ceiling is bright over the sun patches and falls to grey-blue deeper in the room. Both values were Alex's pick from a sweep on 2026-11-12.

Run order after any lighting change: `Apply Office Look`, then `Add Bounce Light`, then `Bake Floor Reflections`.

## When to re-bake

A probe is a photograph of the room as it was lit when baked. Re-run Bake Floor Reflections after:

- `Apply Office Look` (sun, grade or sky changes),
- `Build Kit Office Scene`, which rebuilds the floors without probes or the static flag,
- moving or recolouring anything large in the room.

It refuses to run in Play Mode, because Play Mode changes to a scene are thrown away.

## Known limitations

- **Moving things are not reflected**, players included. URP has no screen-space reflections; planar reflections would fix it at the cost of a second render of the scene every frame.
- **Reflections are box-projected from one point per floor**, so they are approximately placed, not exact, near the room's edges.
- **The greybox floor templates still use their own floor**; this is only in KitOffice.
- **The art bible asks for grey carpet** (section 10, "the mundane layer"). The polished floor is Alex's decision of 2026-11-12.
- **Checked:** a render with the floor turned into a perfect mirror shows the room (light strips, stair, walls) in the probes, and before/after renders are in `ArtSource/Look/review/10_polished_floor_before_after.png`.
- **Not checked:** Play Mode, frame cost, and how the reflections read in motion.
