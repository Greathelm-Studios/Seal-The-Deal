# Painter status: is a project open, is it busy, which texture sets and channels it has.
import substance_painter.project as pr
import substance_painter.textureset as ts
r = {"open": pr.is_open(), "busy": pr.is_busy() if pr.is_open() else None}
if pr.is_open():
    r["sets"] = {t.name: [str(c) for c in t.get_stack().all_channels()] for t in ts.all_texture_sets()}
result = r
