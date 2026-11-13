<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Fable 5.2 (Claude Code)
  Generated : 2026-11-11
  Prompt    : Alex: "do what you recommend, as long as it fits the art direction and that 2000s
              corporate future aesthetic" (recommendation: soft building, graphic people)
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : The owning doc for the character shader (AGENTS.md §7): what it does, where its
              files are, how to tune it, and what has not been checked.
-->
# Character shader

**Status:** Current (on the STDMC character, which every player wears) · **Last updated:** 2026-11-12

Characters are drawn as flat graphic shapes inside a softly lit building: each material is one base colour and one hard-edged, colder shadow colour, with no gradients.
Pale parts (shirt, skin) carry a thin navy contour so they don't dissolve into white walls; suits and shoes carry none.
The building, glass and props stay on standard URP Lit materials.

This implements art bible §04 ("soft world light + hard character value breaks") and §05 ("base → cut shadow").
The bible text is at [art_bible.txt](../../ArtSource/Kit/concepts/Office/refs/art_bible.txt).

## Files

- Shader: [CharacterToon.shader](../../Assets/_Project/Art/Toon/CharacterToon.shader), listed in the material shader menu as `Close the Deal/Character Toon`.
- Starter materials: [Assets/_Project/Art/Toon/Materials/](../../Assets/_Project/Art/Toon/Materials/): `Char_Suit_Navy`, `Char_Shirt_White`, `Char_Skin`, `Char_Shoe_Leather`.
- Tool: `Close the Deal > Art > Build Character Toon Materials` ([CharacterToonMaterials.cs](../../Assets/_Project/Editor/Art/CharacterToonMaterials.cs)) writes any starter material that is missing or untouched. It never overwrites one a person has edited, and touches no scene or prefab.
- First real character: [Assets/_Project/Art/Characters/STDMC/](../../Assets/_Project/Art/Characters/STDMC/), with seven toon materials made from the model's own colours, the baked outline meshes and the prefab `STDMC_Toon`. `STDMC_Toon` holds `STDMC_Rigged.fbx`: the model on a Mixamo skeleton (52 bones), imported as a Unity humanoid at 1.8 m with the toon materials mapped in its import settings, standing with its feet at the prefab's origin. `STDMC.fbx` is the earlier static export with no skeleton; nothing uses it any more.
- Player: the greybox tool ([GreyboxSceneSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxSceneSetup.cs)) nests `STDMC_Toon` in [Player.prefab](../../Assets/_Project/Prefabs/Player.prefab) as the body whenever it builds that prefab, so a change to `STDMC_Toon` reaches every player.
- Face: [Character eyes](CHARACTER_EYES.md) owns the head-mounted eye cards, their alpha-clipped shader and the artist texture layout.
- Review renders: [ArtSource/Toon/review/](../../ArtSource/Toon/review/) includes the capsule stand-in and the rigged STDMC front, back and 12 m views.

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
