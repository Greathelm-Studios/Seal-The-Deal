# Engine presets

All three expect the character's front to face -Y in Blender (the library's convention); `inspect` warns otherwise. Frame rate is whatever retarget baked (30 fps by default, `--fps` to change). Exports bake every frame with no keyframe reduction, all bones, no leaf bones, mesh modifiers not applied.

| Engine | Format | Scale | Axes | Notes |
|---|---|---|---|---|
| Unreal 5 | FBX | Blender metres, FBX unit scale written so Unreal reads centimetres (`apply_unit_scale`, `FBX_SCALE_NONE`) | forward -Z, up Y (Unreal converts) | Armature object renamed `Armature` so Unreal does not add an extra root bone; smoothing exported per face |
| Unity | FBX | `FBX_SCALE_ALL`, so the model imports at scale 1 instead of 100 | forward -Z, up Y | |
| Godot 4 | GLB | metres | Y up (glTF) | One animation per action, sampled every frame |

Bundled (default) puts the mesh, skeleton and every clip in one file. `--split` writes `<name>_<clip>.<ext>` per clip, each with the mesh, so a single clip can be re-imported on its own.

`--no-mesh` writes the skeleton and clips without the mesh: for clips added later to a character already in the engine (Unreal: pick its Skeleton on import; Unity: Avatar Definition "Copy From Other Avatar"). `--mesh-only` writes the character in its rest pose with no animation. `verify` checks the mesh is there or absent as asked.

## Bone names

`--bone-names` renames the mapped bones (see [rigs and retargeting](rigs-and-retargeting.md)) in the exported files only; the working file keeps its names. Bones outside the map (twist, face, props) keep theirs. Only the names change: the hierarchy, rest pose and proportions stay the character's own, so this is for tools that match bones by name, not a conversion to another skeleton.

| Scheme | Names | Use |
|---|---|---|
| `ue` | `pelvis`, `spine_01..03`, `neck_01`, `head`, `upperarm_l`... | Unreal IK Retargeter and mannequin-named tooling. The library rig already uses these apart from `Head`, so this matters for characters on other rigs |
| `mixamo` | `mixamorig:Hips`, `mixamorig:LeftArm`... (no root bone) | Tools and engine importers with Mixamo presets |
| `cc` | `CC_Base_Hip`, `CC_Base_L_Upperarm`... | Reallusion Character Creator / iClone characterisation. Untested in Reallusion's tools |

Export fails if the character already has a bone with one of the new names that is not the mapped bone (a rig that already uses that naming): export it without `--bone-names`. `verify` follows the renames when it compares bone positions.

## In place or root motion

Chosen at retarget time, not export time. In place (default) suits Unity with "Bake Into Pose", Godot with code-driven movement and Unreal without root motion. `--root-motion` puts the travel on the root bone (or the pelvis if the character has no root bone, which Unreal cannot use for root motion; retarget warns). The preview prints how far the hips travel and in which direction.

## Importing (not tested inside the engines)

The exports were checked by reopening them in Blender, not in the engines; treat these steps as the expected path.

- **Unreal 5**: drag the FBX into the Content Browser. Import as Skeletal Mesh with animations. For later split files, untick mesh import and pick the existing Skeleton. For root-motion clips, tick Enable Root Motion on each Anim Sequence. The library rig uses the Unreal mannequin's bone names, so the IK Retargeter can map it to Manny with few changes.
- **Unity**: drop the FBX into Assets. Rig tab: Humanoid (or Generic), Avatar from this model. Animation tab: tick Loop Time on `*_Loop` clips; for in-place clips tick Bake Into Pose for root rotation and position Y and XZ.
- **Godot 4**: drop the GLB into the project. Animations appear on the AnimationPlayer of the imported scene. Set Loop Mode for `*_Loop` clips in Advanced Import Settings.
