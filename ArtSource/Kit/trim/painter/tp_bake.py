# Painter step 2: bake the high-poly trim board (relief + ID by material colour) onto the low plane.
import os
import substance_painter.textureset as ts
import substance_painter.baking as bk
HIGH = r"C:/Users/alexf/Documents/GitHub/Seal-The-Deal/ArtSource/Kit/trim/build/TrimBoard_TrimBoard_High.fbx"
assert os.path.exists(HIGH)
B = bk.MeshMapUsage
t = ts.all_texture_sets()[0]
p = bk.BakingParameters.from_texture_set(t)
c, idp, ao = p.common(), p.baker(B.ID), p.baker(B.AO)
# WHY these distances: the board is 2 m square (diagonal 2.83 m) and its relief is under 6 mm, so relative 0.0025
# reaches ~7 mm either side of the plane; AO 0.004 is ~11 mm of contact shadow, enough to sit keys and slots in.
p.set({c["OutputSize"]: (11, 11), c["LowAsHigh"]: False, c["HipolyMesh"]: "file:///" + HIGH,
       c["RelativeScale"]: True, c["MaxHeight"]: 0.0025, c["MaxDepth"]: 0.0025, c["DilationWidth"]: 8,
       idp["ColorSource"]: idp["ColorSource"].enum_values()["Material color"],
       ao["MaxDistance"]: 0.004})
p.set_enabled_bakers([B.AO, B.Curvature, B.Thickness, B.WorldSpaceNormal, B.Position, B.Normal, B.ID, B.Height])
p.set_textureset_enabled(True)
bk.bake_selected_textures_async()
result = "bake started"
