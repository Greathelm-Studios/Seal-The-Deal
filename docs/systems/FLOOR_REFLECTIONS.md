# Polished floor and reflections

**Status:** Current in KitOffice (not in the greybox floor templates; not judged in play) · **Last updated:** 2026-11-21

The office floor is a pale polished tiled resin instead of carpet.
It mirrors the room softly: windows, frames, the cobalt wall and the red door show in it, and sun patches land on it as bright blown-out shapes.
Players and anything else that moves are not reflected.

## Architecture and rationale

- **Custom box-projected probes over baked/planar alternatives:** URP has no screen-space reflections, and planar reflections would cost a second render of the scene every frame across up to 8 players. Baked `Custom` reflection probes (`.exr` cubemaps assigned directly) are free at runtime, require no full scene lighting bake, and reflect static building geometry accurately via box projection.
- **Two-tier probe layout:** [FloorReflections.cs](../../Assets/_Project/Editor/Art/FloorReflections.cs) places one 512 px whole-floor probe per `Floor N` as the floor fallback, and flags all renderers under `Kit Office` as `ReflectionProbeStatic` (without that flag, probes capture empty sky; moving players and props stay unflagged). [ZoneReflections.cs](../../Assets/_Project/Editor/Art/ZoneReflections.cs) then places an 8 m structural-bay grid of 256 px probes at eye height (1.5 m) with a 1 m blend distance so glossy props and floors take the nearest bay's reflections without needing a per-room list as floor plans change.
- **Probe box margin and parenting:** Each probe box extends `0.4 m` past the floor, walls, and ceiling (`BoxMargin`) because probe weight fades to zero at box edges; exact-fit boxes left the floor mirroring the sky. Both probe groups and bounce lights are parented under `Kit Office` so moving the building moves them with it (rotating the building still breaks reflections because Unity probe boxes stay world-aligned).
- **Floor brightness:** A pure white floor reflecting a white room shows almost no reflection; `Floor_Carpet` uses a slightly muted pale tone (`#DCE8EC` base / tiled `Floor_Carpet.png`) and smoothness `0.96` so reflections read clearly.
- **Hiding animated and marking layers during bake:** [KitOfficeScene.cs](../../Assets/_Project/Editor/Art/KitOfficeScene.cs) temporarily disables `Markings`, `Animated`, and monitor `CRT` overlays before baking probes so additive shafts/dust do not freeze into the floor and cyan markings/CRTs do not tint every shadowed floor probe cyan.
- **Fake bounce lights over Adaptive Probe Volumes:** Real-time lighting in URP has no bounce, leaving ceilings dark over bright sun patches. [BounceLight.cs](../../Assets/_Project/Editor/Art/BounceLight.cs) computes where directional sunlight enters each 4 m facade bay and places shadowless upward spotlights (intensity `2.5`, range `7 m`, outer `160` deg, inner `40` deg) instead of Adaptive Probe Volumes, which would require mixed lighting and re-baking for every procedurally stacked floor.

## Files

- Floor material: [Floor_Carpet.mat](../../Assets/_Project/Art/Kit/Materials/Floor_Carpet.mat), smoothness `0.96`, textured with `Floor_Carpet.png` (thin-joint pale tiles, base colour `#DCE8EC` before texture multiplication). The name stays `Floor_Carpet` because the exported kit models are mapped to it by name. [KitImport.cs](../../Assets/_Project/Editor/Art/KitImport.cs) builds it with these values.
- Floor probes: `Seal the Deal > Art > Bake Floor Reflections` ([FloorReflections.cs](../../Assets/_Project/Editor/Art/FloorReflections.cs)) adds one box-projected reflection probe per `Floor N` under `Kit Office`, bakes each to `Assets/_Project/Art/Look/Probes/<scene>_Floor<N>.exr`, flags building renderers as `ReflectionProbeStatic`, and stores probes in `Kit Office/Reflection Probes`. It asks first and does not save the scene.
- Zone probes: `Seal the Deal > Art > Bake Zone Reflections` ([ZoneReflections.cs](../../Assets/_Project/Editor/Art/ZoneReflections.cs)) adds an 8 m grid (5 x 4 per 40 x 32 m storey) of 256 px probes under `Kit Office/Zone Reflection Probes`, baking `<scene>_Zone<F>_<I>_<J>.exr`. Run after `Bake Floor Reflections`.

## Bounce light

`Seal the Deal > Art > Add Bounce Light` ([BounceLight.cs](../../Assets/_Project/Editor/Art/BounceLight.cs)) projects the sun's direction in `Kit Office` local space through each of the 10 x 4 m facade bays on every floor and places one soft, shadowless, upward spotlight (`2.5` intensity, cooled pale blue `(0.86, 0.91, 1)`) at each sun patch `0.15 m` above the floor.
The ambient level in `Apply Office Look` is `0.5` (`AmbientLevel`) so bounce patches lift ceilings locally without flattening the room.

Run order after any lighting change: `Apply Office Look`, then `Add Bounce Light`, then `Bake Floor Reflections`, then `Bake Zone Reflections`.

## When to re-bake

A probe is a photograph of the room as it was lit when baked. Re-run `Bake Floor Reflections` and `Bake Zone Reflections` after:

- `Apply Office Look` (sun, grade or sky changes),
- `Build Kit Office Scene` (already runs the full bake pipeline automatically),
- moving or recolouring anything large in the room.

All three tools refuse to run in Play Mode because Play Mode scene changes are discarded.

## Known limitations

- **Moving things are not reflected**, players included. URP has no screen-space reflections; planar reflections would fix it at the cost of a second render of the scene every frame.
- **Reflections are box-projected from discrete points**, so they are approximately placed, not exact, near room edges and corners.
- **Building rotation is unsupported for probes:** Moving `Kit Office` works, but rotating it misaligns world-aligned probe boxes.
- **The greybox floor templates still use their own floor**; this is only in KitOffice.
- **The art bible asks for grey carpet** (section 10, "the mundane layer"). The polished floor is Alex's decision of 2026-11-12.
- **Checked:** a render with the floor turned into a perfect mirror shows the room (light strips, stair, walls) in the probes, and before/after renders are in `ArtSource/Look/review/10_polished_floor_before_after.png`.
- **Not checked:** Play Mode, frame cost, and how the reflections read in motion.
