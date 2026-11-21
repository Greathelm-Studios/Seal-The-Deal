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
