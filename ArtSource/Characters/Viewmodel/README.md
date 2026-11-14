<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Fable 5.2 (Claude Code)
  Generated : 2026-11-13
  Prompt    : Alex: "set up an fps viewmodel with the characters model we are using"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : How the first-person arm clips were made, so they can be remade or added to.
-->
# First-person arm clips

`Assets/_Project/Art/Characters/STDMC/Animations/STDMC_ViewmodelMoves.fbx` holds `Pistol_Idle_Loop` (the only one in use), `Pistol_Shoot`, `Pistol_Reload` and `Pistol_Aim_Neutral` from the Quaternius Universal Animation Library 1 (CC0), retargeted onto the character's own skeleton with the `character-animation` skill. The system is described in [docs/systems/VIEWMODEL.md](../../../docs/systems/VIEWMODEL.md).

To remake it (paths relative to the repo root; `<skill>` is the character-animation skill folder, `<work>` any scratch folder):

```bash
node <skill>/scripts/ca.mjs retarget --character Assets/_Project/Art/Characters/STDMC/STDMC_Rigged.fbx --clips Pistol_Idle_Loop,Pistol_Shoot,Pistol_Reload,Pistol_Aim_Neutral --out <work>/work.blend
```

```bash
blender -b <work>/work.blend --python ArtSource/Characters/Viewmodel/unit_scale.py -- <work>/work_unit.blend <skill>/scripts
```

```bash
node <skill>/scripts/ca.mjs export --blend <work>/work_unit.blend --engine unity --no-mesh --out <work>/export
```

Copy `<work>/export/work_unit.fbx` over `STDMC_ViewmodelMoves.fbx`, then run `Close the Deal > Greybox > Rebuild Viewmodel` in Unity. The tool sets the import settings the first time (Generic, no avatar, Preserve Hierarchy, the rig's scale, Loop Time on `_Loop` clips).

The middle step is not optional: without [unit_scale.py](unit_scale.py) every bone imports 100 times out (see "The clips" in [CHARACTER_ANIMATION.md](../../../docs/systems/CHARACTER_ANIMATION.md)).

To check a clip from the eye before exporting: `node <skill>/scripts/ca.mjs preview --file <work>/work.blend --out <work>/preview --first-person`.
