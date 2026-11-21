# Painter: which mesh maps exist on the texture set yet.
import substance_painter.textureset as ts
B = __import__("substance_painter.baking", fromlist=["x"]).MeshMapUsage
t = ts.all_texture_sets()[0]
result = {u.name: (t.get_mesh_map_resource(u) is not None) for u in (B.AO, B.Curvature, B.Normal, B.ID, B.Height)}
