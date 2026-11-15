# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: "making a trimsheet in substance ... you can use substance painter"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Painter step 3: the trim sheet's layer stack, painted by the baked ID map.
#              Run from ArtSource/Kit/trim/painter with: python sp.py tp_layers.py
# -----------------------------------------------------------------------------
# Painter step 3: the trim sheet's layer stack, painted by the baked ID map. Clean and graphic (art bible §11: shape,
# colour and light, not grime): one fill per surface, brushed roughness on silver, cyan emission on LEDs, a soft light
# edge on rounded edges and a faint cool cavity so keys, slots and holes sit in. Legacy-colour project: fills take
# 8x8 swatch bitmaps (make_swatches.py).
import os
import substance_painter.textureset as ts
import substance_painter.layerstack as ls
import substance_painter.resource as res
import substance_painter.colormanagement as cm
D = r"C:/Users/alexf/Documents/GitHub/Seal-The-Deal/ArtSource/Kit/trim/painter/swatches"
CT = ls.ChannelType
t = ts.all_texture_sets()[0]
st = t.get_stack()
if not st.has_channel(CT.EmissionColor):
    st.add_channel(CT.EmissionColor, ts.ChannelFormat.sRGB8)
for n in ls.get_root_layer_nodes(st):
    ls.delete_node(n)
_cache = {}
def sw(name):
    if name not in _cache:
        _cache[name] = res.import_project_resource(os.path.join(D, name + ".png"), res.Usage.TEXTURE).identifier()
    return _cache[name]
def gen(name):
    return [r for r in res.search("u:generator") if r.identifier().name.startswith(name)][0].identifier()
log = {}
ROUGH, METAL = CT.SpecularRoughness, CT.BaseMetalness

def fill(label, col, rough, metal=0, emit=None, rough_map=None):
    f = ls.insert_fill(ls.InsertPosition.from_textureset_stack(st))
    f.set_name(label)
    f.set_source(CT.BaseColor, sw("sw_" + col))
    f.set_source(ROUGH, sw(rough_map) if rough_map else sw(f"g{rough:03d}"))
    f.set_source(METAL, sw(f"g{metal:03d}"))
    f.set_source(CT.EmissionColor, sw("sw_" + (emit or "black")))
    return f

def id_mask(f, rgb255):
    f.add_mask(ls.MaskBackground.Black)
    e = ls.insert_color_selection_effect(ls.InsertPosition.inside_node(f, ls.NodeStack.Mask))
    p = e.get_parameters(); p.colors = [cm.Color(*[c / 255 for c in rgb255], cm.GenericColorSpace.Raw)]; p.tolerance = 0.12
    e.set_parameters(p)

def gen_mask(f, name, params=None):
    f.add_mask(ls.MaskBackground.Black)
    g = ls.insert_generator_effect(ls.InsertPosition.inside_node(f, ls.NodeStack.Mask), gen(name))
    src = g.get_source()
    if params:
        src.set_parameters(params)
    log[f.get_name()] = sorted(src.get_parameters().keys())[:12]
    return g

# Baked ID colours as Painter reads them (build/meshmaps/ID_Pearl_id.png): 0.5 came through as 55/255 (linear).
fill("Pearl", "pearl", 15)
id_mask(fill("Silver_Brushed", "silver", 35, metal=85, rough_map="brushed_rough"), (55, 55, 55))
id_mask(fill("Chrome", "chrome", 8, metal=100), (0, 0, 0))
id_mask(fill("Aqua", "aqua", 24), (0, 255, 255))
id_mask(fill("Mist", "mist", 50), (0, 0, 255))
id_mask(fill("Navy", "navy", 55), (0, 255, 0))
id_mask(fill("Screen_Smoke", "screen", 6), (255, 0, 255))
id_mask(fill("Cobalt", "cobalt", 6), (55, 0, 255))
id_mask(fill("LED_Cyan", "led", 30, emit="led"), (255, 255, 0))
id_mask(fill("Seal", "seal", 65), (255, 0, 0))
id_mask(fill("Leaf", "leaf", 76), (55, 255, 0))
# Print band: fixed artwork (make_print.py) as the base colour, masked by its ID (1, 0.5, 0 bakes as 255, 55, 0).
prn = fill("Print", "pearl", 30)
# WHY a versioned file name: Painter reuses an already-imported resource of the same name.
prn.set_source(CT.BaseColor, sw("print_atlas_v2"))
id_mask(prn, (255, 55, 0))
# Soft light edge on every rounded edge (the gloss catch the concepts paint): edge-wear generator with no grunge.
edge = fill("Edge_Light", "pearl_hi", 5)
gen_mask(edge, "mg_metal_edge_wear", {"Wear_Level": 0.18, "Wear_Contrast": 0.55, "Grunge_Amount": 0.0})
for ch in (CT.BaseColor, ROUGH):
    edge.set_opacity(0.45, ch)
for ch in (METAL, CT.EmissionColor):
    edge.set_opacity(0.0, ch)
# Faint cool cavity so keys, slots and holes sit in (not dirt: a blue-grey, no grunge).
cav = fill("Cavity_Cool", "cavity", 60)
gen_mask(cav, "mg_dirt", {"dirt_level": 0.35, "grunge_amount": 0.0})
cav.set_opacity(0.35, CT.BaseColor)
for ch in (ROUGH, METAL, CT.EmissionColor):
    cav.set_opacity(0.0, ch)
log["stack"] = [n.get_name() for n in ls.get_root_layer_nodes(st)]
result = log
