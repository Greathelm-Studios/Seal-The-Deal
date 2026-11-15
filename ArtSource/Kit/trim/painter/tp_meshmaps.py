# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: "making a trimsheet in substance ... you can use substance painter"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Painter: exports the baked mesh maps, to check the bake and its ID colours.
#              Run from ArtSource/Kit/trim/painter with: python sp.py tp_meshmaps.py
# -----------------------------------------------------------------------------
# Painter: export the baked mesh maps (to check the ID colours and the bake before painting).
import os
import substance_painter.export as ex
OUT = r"C:/Users/alexf/Documents/GitHub/Seal-The-Deal/ArtSource/Kit/trim/build/meshmaps"
os.makedirs(OUT, exist_ok=True)
preset = next(p for p in ex.list_resource_export_presets() if p.resource_id.name == "Mesh Maps")
cfg = {"exportShaderParams": False, "exportPath": OUT, "defaultExportPreset": preset.resource_id.url(),
       "exportList": [{"rootPath": "ID_Pearl"}],
       "exportParameters": [{"parameters": {"fileFormat": "png", "bitDepth": "8", "sizeLog2": 11, "paddingAlgorithm": "infinite"}}]}
r = ex.export_project_textures(cfg)
result = {"status": str(r.status), "files": {k: [os.path.basename(f) for f in v] for k, v in r.textures.items()}}
