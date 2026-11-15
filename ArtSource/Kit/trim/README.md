<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-15
  Prompt    : Alex: "making a trimsheet in substance or something that can be used on most modular kits and props"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : How the AONAGI Aero trim sheet is made in Substance Painter and how the props are mapped onto it.
-->
# AONAGI Aero trim sheet

**Status:** Current for the office props and doors · **Last updated:** 2026-11-15

One 2048 Painter-made sheet, at 1024 px/m (it repeats every 2 m along U), that carries the props' colours, gloss, metal and small relief.
Every prop surface on the sheet is merged into a single material, `Trim_Aero`, so a prop draws with one opaque material, plus `Plastic_Aqua_Frost` (the props' frosted aqua acrylic, 82 % opaque), `Glass_Clear` or `Light_Strip` where it has frost, glass or a glowing diffuser.

## Layout ([trim_layout.json](trim_layout.json))

From the top: flat **swatch bands** for whole surfaces (Pearl, Silver brushed, Chrome, Aqua, Mist, Navy, Screen smoke, Cobalt, LED cyan, Seal red, Leaf), then **detail bands**: Keys (key caps on a mist well), Vents (rounded slots), Grille (round perforation), Buttons (cyan-ringed round buttons over two keypad rows), Ribbed (brushed silver ribs) and Panel (pearl panels with shut lines).
The last 512 px are the **Print band**: fixed artwork drawn by `painter/make_print.py` (the AONAGI ring, an asset tag, the copier's 複写 / COPY display, the NAGI / MIZU / AO can wraps) at the rectangles in `trim_layout.json` "print". A face whose material is `Print_<name>` is fitted to its rectangle by `apply_trim.py`: flat plates by their own plane, cans wrapped round their axis.
A model picks a band by the material its faces carry: `Wall_PaperWhite` maps to Pearl, `Trim_Keys` to Keys, and so on (the `maps` lists).

## Make the sheet

1. `node <skill>/scripts/mdl.mjs build trim/trim_board.py --out <dir>/TrimBoard.blend`, then `export --split`, and copy the two FBX files into `trim/build/`. The board is a 2 x 2 m low plane plus an ID-coloured high-poly with the relief.
2. `python painter/make_print.py`, then open Substance 3D Painter with the `envart_remote` plugin (Documents/Adobe/Adobe Substance 3D Painter/python/startup). From `trim/painter/`: `python make_swatches.py`, then `python sp.py tp_create.py`, `tp_bake.py` (poll `tp_bakecheck.py` and `tp_sets.py` until not busy), `tp_layers.py`, `tp_export.py`, `tp_save.py`.
3. The URP maps land in `trim/build/export/`; `KitImport.cs` copies them into `Assets/_Project/Art/Kit/Textures/Trim_Aero_*.png`.

`painter/Trim_Aero.spp` is the saved project, there to repaint by hand. Keep the band layout, or update `trim_layout.json` to match. It is 34 MB, and `*.spp` is not in the repo's Git LFS rules yet.

## Put a prop on it

`trim/build_props.sh <skill> <piece>...` (also called at the end of `build_all.sh`): build from `props_aero.py`, trim-mode unwrap with the bands from `trim_args.py`, merge with `apply_trim.py`, check, export to `export/<piece>/`, verify.
`preview_trim.py` and `render_textured.py` make a textured EEVEE review render; the skill's contact sheet shows flat material colours only.

## Unity material

`KitImport.cs` builds `Trim_Aero` on the **Close the Deal/Aero Plastic** shader (Assets/_Project/Art/Plastic): URP Lit's lighting with a lacquer clearcoat, a soft top-to-underside gradient and a fresnel rim. Lacquer, rim and gradient are sliders on the material and survive a re-import. `Plastic_Aqua_Frost` stays transparent URP Lit.

## Known limits

- Swatch faces are squeezed to fit their band's height. That's harmless on flat colour, but brushed silver streaks get denser on big faces.
- Strips stop 6 px inside each band, so mipmaps barely bleed between bands. Far-away props may still pick up a hint of the next band.
- The walls, floors and partitions aren't on the sheet yet.
