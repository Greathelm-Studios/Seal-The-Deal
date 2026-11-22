---
name: blender-modeling
description: "Model static 3D game assets (props, kit pieces, weapons, set dressing) in headless Blender for Unity: a parametric Python model script built from bmesh building blocks with every dimension named, numeric geometry checks, a six-view contact sheet, colliders and LODs, and FBX export that imports into Unity at scale 1 with no rotation. Use when asked to model, build or make a 3D object, prop or mesh for a game, or to move geometry that Unity editor scripts assemble from primitives into Blender. Not for characters, rigging or animation (use character-animation), sculpting, organic shapes, or 3D printing."
---

# Blender modeling for Unity

Everything runs through `scripts/mdl.mjs` (Node 18+), which finds Blender and runs the scripts headless. Paths below are relative to this skill folder. Every command is non-interactive and exits non-zero on failure.

If you were not told this skill folder's path, it is the first of these that contains `scripts/mdl.mjs`: `$BLENDER_MODELING_SKILL`, `~/.saber/skills/blender-modeling`, `~/.agents/skills/blender-modeling`, `~/.claude/skills/blender-modeling`.

A model is a Python script, kept in the game's repository (for example `ArtSource/Models/<name>.py`), that builds the asset with [modelkit](references/modelkit.md). The script is the source; the .blend and FBX are outputs you can always rebuild.

## Why this way

Models written as Blender Python fail in two ways that a render does not show: the script targets an API that has since changed, and the mesh has defects (flipped faces, wrong size, open edges) that look fine in a picture. So the version is pinned, the script uses only modelkit, bmesh and `bpy.data` (no `bpy.ops`, which needs a UI context and fails headless), and the result is measured before it is looked at.

## Workflow

1. **Spec as a table.** Before writing code, list every dimension, clearance, thickness and count the asset needs as a named value in metres, with where it comes from (the user, a reference, an existing Unity builder). Show the table to the user when any value is a guess.
2. **Write the model script.** Rules:
   - Target Blender 4.5 LTS or 5.2 LTS. Use only `modelkit`, `bmesh` and `bpy.data`; no `bpy.ops`.
   - Every value from the table goes in `P = params(...)` at the top, never inline. One unit is one metre; write millimetres as `6 * MM`.
   - Sizes are full extents (`box(0.8, 0.5, 0.45)`). Z is up, the front faces -Y, and the world origin is the asset's pivot in Unity: stand it on z = 0, centred on X and Y, unless it mounts somewhere else (a wall light, a door hinge).
   - `boolean()` uses the EXACT solver; leave it unless every input is a clean closed mesh.
   - Hold multi-part assets together with `group()`; never parent a mesh to a mesh (`modelkit` refuses it, because Unity would import the child turned 90 degrees).
   - Name materials for the Unity materials they will be mapped to. For real surface detail, use `texture()` with a free CC0 texture set from `node scripts/mdl.mjs polyhaven search <words>` and `polyhaven get <slug> --out <dir>` (see [Poly Haven](references/polyhaven.md)); bake it for Unity.
   - Finish with `expect(part, size=...)` for every part whose size the spec fixes. Add `collider()` and `lods()` when the asset needs them.
   See [modelkit](references/modelkit.md) for the functions and [examples/supply_crate.py](examples/supply_crate.py) for a complete model.
3. **Build.** `node scripts/mdl.mjs build <model.py> --out <dir>/<name>.blend [--set KEY=VALUE ...]` prints each object's size, triangles and materials. A failing script prints the lines of its traceback that matter.
4. **Check.** `node scripts/mdl.mjs check --blend <dir>/<name>.blend [--max-tris N]`. Fix every error before going on: flipped or inside-out faces, zero-area faces, loose geometry, missing UVs, negative scale, a size off its `expect()`, a mesh parented to a mesh, a collider that is open or over Unity's 255-triangle convex limit. Read the warnings: open edges are normal on planes and signs (`--watertight` makes them errors for closed props); faces that cut through each other are normal where pieces were joined without a boolean union, wrong inside one solid; an LOD that grows off its LOD0 size will pop. A sliver warning (over 5% of triangles long and thin) is normal on bevel strips; where it comes from boolean cuts, those faces shade and decimate badly. The last lines give the bounds from the origin; `(not standing on z = 0)` means the pivot is not at the base.
5. **Preview and judge.** `node scripts/mdl.mjs preview --blend <dir>/<name>.blend --out <dir>/preview [--colliders]` writes `<name>_sheet.png`: front (-Y), back, left (from -X), right, top and three-quarter, orthographic, stamped with the size. Read the PNG yourself. The check has passed, so do not judge topology; judge only what geometry cannot tell you: proportion against the spec table, features where the spec puts them, parts that read as floating or fused, anything present the spec did not ask for, and material colours on the right faces. `--colliders` draws colliders as a green cage.
   **With a reference image** (concept art, a photo, a blueprint), add `--reference [view=]<image>` for each straight-on view you have (front, back, left, right or top, matching the sheet's views; front if no view is given). It scores how well the model's outline matches the reference's, after scaling the reference to the model's long side, and writes `<name>_<view>_reference.png`: the fitted reference beside the difference, grey where they agree, orange where the model has extra, cyan where it is missing something. The reference needs transparency or a plain background that contrasts with the whole subject; crop away labels, dimension lines and other views first (small specks are ignored, a caption box is not). Targets: outline match at least 0.90 for hard-surface props (0.85 for soft or rounded ones), proportion within 3%, centre of mass within 2%. Below them, patch the parameters the orange and cyan areas point to. A perspective photo or a three-quarter drawing cannot pass; use it only by eye, and say so in the report.
6. **Patch numbers, not structure.** For each discrepancy, change values in `params(...)` (or rebuild with `--set`) and go back to step 3. Change the script's structure only when a feature is missing or wrong in kind, and say so.
7. **Unwrap.** A build has box-mapped UVs, which overlap: fine for tiling materials, wrong for a texture painted or baked for this model. `node scripts/mdl.mjs unwrap --blend <dir>/<name>.blend --out <dir>/uv/<name>.blend [--mode atlas|tiling|trim] [--density PX_PER_M] [--size 1024] [--lightmap] [--trim Material=v0:v1,...]` cuts UVs the way an artist would and writes a new .blend. The settings are the user's choice; take them from the request or from Anim Review's preferences when a round names them:
   - mode: `atlas` for unique UVs, `tiling` for tiling materials, `trim` for a trim sheet
   - texel density in px/m: about 512 third person, 1024 first person
   - texture size
   - `--lightmap`, a second UV map for Unity's baked lighting
   Then run check (it now checks the UVs) and `preview --uv` on the unwrapped .blend and read both. See [UV unwrapping](references/uv-unwrapping.md).
8. **Bake textures (optional).** For a finished look instead of flat named materials, `node scripts/mdl.mjs bake --blend <dir>/<name>.blend --out <dir>/baked/<name>.blend [--size 2048]` writes a new .blend (the build stays as it was) with one material, `<name>_Baked`, and stylized, hand-painted-looking textures beside it: lighter worn edges, darker cavities, a soft top-down gradient and gentle colour variation over each source material, plus metallic/smoothness and, when something glows, emission. Run check and preview on the baked .blend, then export it instead of the build. See [baking](references/baking.md).
9. **Export and verify.** `node scripts/mdl.mjs export --blend <dir>/<name>.blend --out <dir>/export [--split]` writes `<name>.fbx` (or one file per top-level object with `--split`) and a manifest; `node scripts/mdl.mjs verify --manifest <dir>/export/manifest.json` reads the FBX back and checks every object's size, triangles and materials. A baked model's textures are copied beside the FBX, and verify checks the FBX references them.
10. **Into Unity.** Copy the FBX (and a baked model's textures) into the project's model folder. Import settings, the LOD and collider set-up and the baked material are in [Unity import](references/unity-import.md). If the Unity Editor is connected (Unity MCP or `unity-cli`), confirm in the project that the imported objects have rotation 0, scale 1 and the expected bounds before using the prefab.
11. **Report** the model script path, the spec table with the final values, the check result (errors fixed, warnings left and why), what you saw on the sheet, each reference score, the export and verify results, for an unwrapped model the UV settings and the check's UV line, and for a baked model the texture paths and bake time.

## Replacing a Unity editor-script model

Many Unity builders assemble an object from `GameObject.CreatePrimitive` pieces. To move one to Blender: read the builder, take its sizes, positions and colours as the spec table (Unity's Y is Blender's Z; a Unity primitive cube is 1 m, a Unity cylinder is 2 m tall with radius 0.5), write the model script, then compare the sheet with a screenshot of the Unity version. Replace the primitives with the imported prefab in a separate change, and keep the builder until the prefab is in use.

## New computer

1. Copy or clone this skill folder. Nothing in it is machine-specific.
2. Install Blender 4.5 LTS or 5.2 LTS (Windows: `winget install --id BlenderFoundation.Blender`). If it is somewhere unusual, set `BLENDER_PATH`; `node scripts/mdl.mjs blender` shows which one will be used.
3. Install Node 18 or newer if missing.

Tested on Blender 5.2.2, with the exported FBX imported in Unity 6000.4.8f1. Blender 4.5 is expected to work (the scripts avoid 5.x-only calls) but has not been run.
