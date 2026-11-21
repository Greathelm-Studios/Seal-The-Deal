# Character shader

**Status:** Current (on the STDMC character, which every player wears) · **Last updated:** 2026-11-21

Characters are drawn as flat graphic shapes inside a softly lit building: each material is one base colour and one hard-edged, colder shadow colour, with no gradients.
Pale parts (shirt, skin) carry a thin navy contour so they don't dissolve into white walls; suits and shoes carry none.
The building, glass and props stay on standard URP Lit materials (or [AeroPlastic.shader](../../Assets/_Project/Art/Plastic/AeroPlastic.shader) on trim-sheet props).

This implements art bible §04 ("soft world light + hard character value breaks") and §05 ("base -> cut shadow").
The bible text is at [art_bible.txt](../../ArtSource/Kit/concepts/Office/refs/art_bible.txt).

## Architecture and rationale

- **Hand-written HLSL over Shader Graph:** Shader Graph lacks built-in nodes for URP's Forward+ clustered additional-light loop and a second front-culled contour pass (`SRPDefaultUnlit`), both of which would otherwise require custom function nodes and a renderer feature. Keeping all passes in one HLSL file also ensures every pass shares the exact same `UnityPerMaterial` constant buffer layout for SRP Batcher compatibility.
- **Flat ambient and clamped scene light:** The shader samples spherical harmonics at a zero normal (`SampleSH(half3(0, 0, 0))`) so ambient light tints evenly without painting soft directional gradients across the body. Incoming scene light is clamped to `1.0` so high-intensity sunlight (used to bloom white walls) never washes out artist-picked character swatches.
- **Indoors shadow bypass:** `_ReceiveShadows` defaults to `0` so characters keep their crisp directional value cut even when standing indoors under floor slabs.
- **Selective back-face contour shell:** Instead of a full-screen post-process edge filter (which would outline every prop and require a renderer feature and layer mask), the `SRPDefaultUnlit` pass extrudes back faces in screen space (`positionCS.xy` scaled by `positionCS.w`) so line width stays consistent at 2 m and 12 m, clamped by `_OutlineMaxSize` in world metres so distant hands and hair are not buried in ink. Materials with contour width `0` disable the pass to save a draw call.
- **Tangent-space baked outline normals:** Hard-edged meshes split normals along sharp edges, breaking the extruded shell into shards. [CharacterOutlineBake.cs](../../Assets/_Project/Editor/Art/CharacterOutlineBake.cs) welds coincident vertices within `0.00001 m` and stores smoothed normals in `UV3` (`TEXCOORD3`) in each vertex's tangent space so smoothed directions deform properly when skinned bones bend. Baked meshes are updated in place to preserve asset GUIDs.
- **Starter material palette and GUID stability:** [CharacterToonMaterials.cs](../../Assets/_Project/Editor/Art/CharacterToonMaterials.cs) generates fingerprinted starter materials (`GeneratedAssetGuard`), updates existing materials in place so prefab references never break, enables GPU instancing for up to 8 players plus ragdolls, and chooses cold shadow hues that read cleanly (e.g. pale cobalt-grey `#B4C2E0` on white shirts so shadows do not look dirty, and muted `#B8979A` on skin after a stronger mauve read as bruising).

## Files

- Shader: [CharacterToon.shader](../../Assets/_Project/Art/Toon/CharacterToon.shader), listed in the material shader menu as `Close the Deal/Character Toon`.
- Starter materials: [Assets/_Project/Art/Toon/Materials/](../../Assets/_Project/Art/Toon/Materials/): `Char_Suit_Navy`, `Char_Shirt_White`, `Char_Skin`, `Char_Shoe_Leather`.
- Tool: `Close the Deal > Art > Build Character Toon Materials` ([CharacterToonMaterials.cs](../../Assets/_Project/Editor/Art/CharacterToonMaterials.cs)) writes any starter material that is missing or untouched. It never overwrites one a person has edited, and touches no scene or prefab.
- First real character: [Assets/_Project/Art/Characters/STDMC/](../../Assets/_Project/Art/Characters/STDMC/), with seven toon materials made from the model's own colours, the baked outline meshes and the prefab `STDMC_Toon`. `STDMC_Toon` holds `STDMC_Rigged.fbx`: the model on a Mixamo skeleton (52 bones), imported as a Unity humanoid at 1.8 m with the toon materials mapped in its import settings, standing with its feet at the prefab's origin. `STDMC.fbx` is the earlier static export with no skeleton; nothing uses it any more.
- Player: the greybox tool ([GreyboxSceneSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxSceneSetup.cs)) nests `STDMC_Toon` in [Player.prefab](../../Assets/_Project/Prefabs/Player.prefab) as the body whenever it builds that prefab, so a change to `STDMC_Toon` reaches every player.
- Face: [Character eyes](CHARACTER_EYES.md) owns the head-mounted eye cards, their alpha-clipped shader ([CharacterEyeCard.shader](../../Assets/_Project/Art/Characters/Eyes/CharacterEyeCard.shader), which uses `Offset 0, -1` to stay clear of the face mesh at distance while occluding behind hair), and [CharacterEyeSetup.cs](../../Assets/_Project/Editor/Art/CharacterEyeSetup.cs).
- Review renders: [ArtSource/Toon/review/](../../ArtSource/Toon/review/) includes the capsule stand-in and the rigged STDMC front, back and 12 m views.

## Stencil mark

The base and contour passes set stencil bit 8 on every pixel a character covers (added 2026-11-15).
The blood splat shader reads it to leave characters unpainted ([BLOOD.md](BLOOD.md)).
Bit 8 is one of the four bits URP leaves to projects; any other effect that wants "is this pixel a character" can test the same bit.

## Near clip for the body view

Every pass except the shadow pass skips fragments inside the world-space sphere `_BodyViewHeadSphere` (`xyz` = head centre, `w` = radius squared, set per-camera by `PlayerViewmodel` for the owner's own camera and cleared to zero for mirrors, shadows, and every other camera; the eye-card shader does the same).
It is 0 for every camera except the one a player looks out of their own body with, in the body view ([VIEWMODEL.md](VIEWMODEL.md)), so it changes nothing anywhere else.

## Tuning a material

| Inspector value | What it does |
|---|---|
| Base colour | The lit side. |
| Shadow colour | The shadow side. Pick it by hand, colder rather than just darker. |
| Shadow size | Where the cut falls. 0.5 is the natural light/dark boundary. |
| Shadow edge softness | Anti-aliasing only. Keep it tiny or the cut becomes a gradient. |
| Cast shadows cut in too | 0 by default: the cut follows the sun's direction even indoors, where the slab above would otherwise put the whole body in shadow. |
| Highlight size | 0 = off. For leather and shoes: one hard streak. |
| How much room light changes the colours | 0 = colours always exactly as picked. Higher lets a dark service room darken the character. |
| Contour width | Screen pixels, up close. 0 = off. |
| Contour is never thicker than | A cap in metres. The line thins as the character gets smaller on screen, so from across the street it does not bury hair and hands. |
| Contour colour | Pick the opposite value to the surface: a pale line on dark parts (suit, hair), a dark line on pale parts (skin, shirt). A dark line on a dark suit cannot be seen. |

A material built with contour width 0 has its contour pass switched off to save a draw call.
To give it a contour later, delete the material and re-run the tool with a width set in the tool, or re-enable the `SRPDefaultUnlit` pass in the material's debug Inspector.

## Known limitations

- **The contour needs smooth normals, and hard-edged meshes need a bake.** On a hard edge the line splits open. Select the model and run `Close the Deal > Art > Bake Character Outline Normals` ([CharacterOutlineBake.cs](../../Assets/_Project/Editor/Art/CharacterOutlineBake.cs)): it writes a copy of each mesh into an `Outline` folder beside the model, with smoothed normals in an extra channel that only the contour uses. Point the character at the baked mesh, and re-run after re-exporting the model.
- **Skin colours are placeholders.** The bible gives no skin hexes; `Char_Skin` is one neutral tone so the shader can be judged.
- **No screen-space shadow support.** The PC renderer doesn't use that feature; the shader says where to add it if that changes.
- **Not checked for this shader:** animation, Play Mode, a client's view, frame cost with 8 players, and the Mobile renderer.
