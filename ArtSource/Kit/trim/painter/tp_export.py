# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: "making a trimsheet in substance ... you can use substance painter"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Painter step 4: exports the sheet's URP maps for KitImport.cs.
#              Run from ArtSource/Kit/trim/painter with: python sp.py tp_export.py
# -----------------------------------------------------------------------------
# Painter step 4: export the sheet for URP (Metallic Standard: BaseMap, MetallicSmoothness, Normal, Emission).
import os
import substance_painter.export as ex
OUT = r"C:/Users/alexf/Documents/GitHub/Seal-The-Deal/ArtSource/Kit/trim/build/export"
os.makedirs(OUT, exist_ok=True)
preset = next(p for p in ex.list_resource_export_presets() if p.resource_id.name == "Unity Universal Render Pipeline (Metallic Standard)")
cfg = {"exportShaderParams": False, "exportPath": OUT, "defaultExportPreset": preset.resource_id.url(),
       "exportList": [{"rootPath": "ID_Pearl"}],
       "exportParameters": [{"parameters": {"fileFormat": "png", "bitDepth": "8", "sizeLog2": 11, "paddingAlgorithm": "infinite", "dithering": True}}]}
r = ex.export_project_textures(cfg)
result = {"status": str(r.status), "files": {k: [os.path.basename(f) for f in v] for k, v in r.textures.items()}}
