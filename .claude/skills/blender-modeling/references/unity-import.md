# Unity import

## What the export does

FBX with `apply_scale_options='FBX_SCALE_ALL'`, `axis_forward='-Z'`, `axis_up='Y'` and `bake_space_transform=True`: the unit and axis change go into the mesh data, so each object imports with rotation 0 and scale 1. Modifiers are applied; no animation is written.

Checked by importing the example crate into Unity 6000.4.8f1 in batch mode: every object at rotation (0, 0, 0) and scale 1, sizes matching the build to the millimetre, materials named as in Blender, an LOD Group with three levels, and the lowest point at the prefab's pivot.

| Blender | Unity |
|---|---|
| +Z (up) | +Y |
| -Y (the model's front) | +Z (forward) |
| +X | -X (so the model's own right, -X in Blender, is +X in Unity) |
| world origin | the prefab's pivot (an object's own origin only sets that child's pivot) |

## Import settings

Model tab: Scale Factor 1, Convert Units on, Bake Axis Conversion off (the export already did it), Import Normals: Import. Read/Write only if a script reads the mesh.

## LODs

`lods()` names the meshes `<name>_LOD0`, `<name>_LOD1`.. under one parent. Unity's importer turns that into an LOD Group on the parent (on the prefab root when it is the only top-level object). Set the transition heights on the LOD Group.

## Colliders

`collider()` writes `<name>_Collider` beside the part. In the prefab (or a model postprocessor), add a MeshCollider with Convex on, using that mesh, and remove its MeshRenderer. The box or hull is at most 255 triangles, Unity's convex limit.

## Materials

An unbaked FBX holds material names and base colours only. Materials tab: either extract them, or remap each name to an existing URP material (Search and Remap, or a per-name mapping). Name materials in the model script after the project's materials so the remap is one step.

### A baked model

After `mdl.mjs bake`, every mesh has one material, `<name>_Baked`, and the export folder holds its textures beside the FBX. Copy them into the project with the FBX, then:

- **Textures.** `<name>_BaseColor.png` and `<name>_Emission.png`: Texture Type Default, sRGB on. `<name>_MetallicSmoothness.png`: sRGB off (it is data), Alpha Source Input Texture Alpha, Alpha Is Transparency off.
- **Material.** Extract the material (Materials tab, Extract Materials) or make a URP Lit material named `<name>_Baked` and remap to it. Set:
  - Base Map: `<name>_BaseColor`, colour white.
  - Metallic Map: `<name>_MetallicSmoothness`, Source **Metallic Alpha** (R is metallic, A is smoothness), Smoothness slider 1 so the map is used as baked.
  - Emission (only when `<name>_Emission.png` exists): tick Emission, Emission Map `<name>_Emission`, colour white with HDR intensity set to the bake's `emission_strength` (in `<name>.build.json` under `textures`; Stormseye's is 2). The map holds the glow colour; the intensity makes it bloom.
- The mesh has one UV map, `BakeUV`, which does not overlap, so it also works for lightmaps: Generate Lightmap UVs can stay off.

## UV maps and lightmaps

Unity takes UV maps by their order in the FBX: the first is UV0 (textures), the second UV1 (baked lightmaps). After `mdl.mjs unwrap`:

- Without `--lightmap`, the mesh has one map, `UVMap`. For baked lighting, tick **Generate Lightmap UVs** in the Model tab (defaults: Hard Angle 88, Angle Error 8%, Area Error 15%, Pack Margin 4). An atlas does not overlap, so it can serve as lightmap UVs as it is, but Unity's generated ones use the lightmap's space better.
- With `--lightmap`, the second map, `Lightmap`, is lightmap UVs already: leave Generate Lightmap UVs **off**, or Unity overwrites it. `check` errors when `Lightmap` is not the second map.
- Tiling and trim UVs overlap and run past 1, so they never work as lightmap UVs: use `--lightmap` or Generate Lightmap UVs.

## Traps

- A mesh parented to a mesh imports turned 90 degrees with this export. `modelkit` refuses it and `check` flags it; put both under a `group()`.
- The world origin is the pivot. A model built floating above z = 0 floats in the prefab; `check` prints the bounds from the origin and notes when the base is not on z = 0.
- `use_mesh_modifiers` is on, so any modifier left on an object is applied in the FBX: what `check` measured is what Unity gets.
