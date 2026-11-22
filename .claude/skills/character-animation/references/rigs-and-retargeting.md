# Rigs and retargeting

## Canonical bones

The library rig uses Unreal mannequin names. Every rig is mapped onto these:

`root, pelvis, spine_01, spine_02, spine_03, neck_01, Head`, and per side (`_l`, `_r`): `clavicle, upperarm, lowerarm, hand, thigh, calf, foot, ball`, plus fingers `thumb|index|middle|ring|pinky_01..03`.

Required: `pelvis, Head, upperarm, lowerarm, hand, thigh, calf, foot` on both sides. Everything else is optional; unmapped bones follow their parent at rest (fingers stay open, twist bones stay neutral).

## How mapping is detected

1. Names. Prefixes are stripped (`mixamorig:`, `mixamorig1:`, any `namespace:`, `Bip01 `, `DEF-`, `CC_Base_`, `RL_`). Side comes from `Left`/`Right`, `.L`/`_l`/`l_` and similar. The rest is matched against synonyms, for example `UpLeg|UpperLeg|thigh`, `Leg|LowerLeg|calf|shin`, `ForeArm|lowerarm`, `Shoulder|clavicle`, `ToeBase|toe|ball`, `Mid|middle`. Bones ending in `end`, `leaf`, `top`, `pole`, `ik`, `twist`, and metacarpals, are ignored.
2. Geometry fixes names that lie: a bone named right that sits on the left is treated as left.
3. Structure: the pelvis is the shared parent of the two thighs, the root is the pelvis's parentless parent, and the spine is the chain from the pelvis to the clavicles' parent (first, middle and last of that chain become `spine_01..03`). When the thighs' parent does not also carry the spine (Character Creator's `CC_Base_Pelvis` under `CC_Base_Hip`), the pelvis moves up to the bone that carries both.

Tested on: the library's own rig, a Quaternius knight on a different generic rig (`Body`, `Hips`, `Abdomen`, `Torso`, `UpperLeg.L`, feet parented to the root bone, mislabelled thumbs), a Mixamo-named skeleton (`mixamorig:Hips` and so on), and the library mannequin renamed to Character Creator names with CC's Hip/Pelvis split, and to UE5 names with metacarpals. Not tested on real Character Creator or UE5 Manny exports, Rigify control rigs or VRoid/VRM; inspect first and expect to need a map file.

## Custom map file

When `inspect` reports missing bones, write JSON mapping canonical names to your bone names, and pass it with `--map` to `inspect` and `retarget`. Entries override detection; an empty string removes a mapping. One side is enough: `hand_l: Wrist.L` also maps `hand_r` to `Wrist.R` when that bone exists and the file does not name `hand_r` itself (`inspect` notes what it mirrored).

```json
{ "pelvis": "Hip_Ctrl", "hand_l": "Wrist.L", "hand_r": "Wrist.R" }
```

`--source-map` does the same for the source when you use `--source`.

## Method (what retarget.py does)

World-space rotation transfer, baked every frame:

- Each mapped bone takes the source bone's rotation change from its rest pose, in world space.
- Rest poses are aligned first: the source is turned to face the character's facing, and a bone whose aim differs by more than 20 degrees between the two rests (an A-pose arm against a T-pose arm) is rotated to match. Smaller differences are treated as the rig's own style and kept, otherwise a shin that leans back at rest gets straightened and pushes the heel through the floor. The pelvis is aligned by a whole frame (thighs to neck, left to right), feet keep their own rest orientation, hands and fingers inherit from the forearm.
- Hip motion is scaled by leg length (thigh height ratio). Each frame the body is then lifted or lowered so the lowest ankle or toe joint is as far above its rest height as the source's (scaled), which keeps planted feet planted on characters with different leg proportions.
- Bone positions follow the semantic chain, so rigs whose feet are not children of the shins (IK-style rigs) still work.

## Troubleshooting

| What you see | Likely cause | Fix |
|---|---|---|
| Inspect exit 2, bones missing | Unusual names | Map file with `--map` |
| Limbs twisted or flipped | Wrong bone mapped (check the `inspect` list), or a rig with control bones driving deform bones | Map file; export the deform skeleton only from the source tool |
| Arms stuck out sideways in every clip | Rest pose detection; the character is in an A-pose but the arm bones were not mapped | Check `upperarm`/`lowerarm` mapping |
| Feet through the floor on jumps | In-place jump clip | `--root-motion` |
| Character a few percent into or above the floor | Mesh heel or toe rotating around the ankle; joint contact is matched, mesh is not | Acceptable under about 2% of height; otherwise use foot IK in the engine |
| Faces backwards when walking | Character's rest facing differs from the engine convention | `inspect` warns when the facing is not -Y; rotate the character in its source file |
| Hands stay open | Source or character has no finger bones | Expected |
