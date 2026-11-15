# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: "making a trimsheet in substance ... you can use substance painter"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Painter: reports which mesh maps the bake has written, so the runner knows when to go on.
#              Run from ArtSource/Kit/trim/painter with: python sp.py tp_bakecheck.py
# -----------------------------------------------------------------------------
# Painter: which mesh maps exist on the texture set yet.
import substance_painter.textureset as ts
B = __import__("substance_painter.baking", fromlist=["x"]).MeshMapUsage
t = ts.all_texture_sets()[0]
result = {u.name: (t.get_mesh_map_resource(u) is not None) for u in (B.AO, B.Curvature, B.Normal, B.ID, B.Height)}
