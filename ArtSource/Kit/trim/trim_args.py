# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: put the props (and later the kit) on one Painter trim sheet
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Turns trim_layout.json into the skill's `unwrap --trim Material=v0:v1,...` argument for the materials a
#              build actually uses (unwrap refuses names no mesh uses), so band positions are written down once.
# -----------------------------------------------------------------------------
"""python trim_args.py <piece>.build.json  ->  prints Material=v0:v1,... (inset by the layout's inset_px)."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
layout = json.load(open(os.path.join(HERE, 'trim_layout.json'), encoding='utf-8'))
used = {m for ob in json.load(open(sys.argv[1], encoding='utf-8'))['objects'] for m in ob.get('materials', [])}
size, inset = layout['size_px'], layout['inset_px']
top, out = size, []
for band in layout['bands']:
    v1, v0 = top, top - band['px']
    top = v0
    for mat in band['maps']:
        if mat in used:
            # WHY the inset: strips stop short of the band edge so mip levels don't bleed the next band's colour in.
            out.append(f"{mat}={(v0 + inset) / size:.6f}:{(v1 - inset) / size:.6f}")
print(','.join(out))
