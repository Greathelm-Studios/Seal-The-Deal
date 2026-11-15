# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: "making a trimsheet in substance ... you can use substance painter"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Painter step 1: creates the trim project on the board's low plane.
#              Run from ArtSource/Kit/trim/painter with: python sp.py tp_create.py
# -----------------------------------------------------------------------------
# Painter step 1 (run through sp.py): a new project on the trim board low plane, 2048, OpenGL normals. WHY the HDRP template: it is the stock template with plain BaseColor/Metallic/Roughness channels the URP export preset reads (there is no URP template).
import substance_painter.project as pr
T = r"C:/Program Files/Adobe/Adobe Substance 3D Painter/resources/starter_assets/templates/Unity HD Render Pipeline (Metallic Standard).spt"
MESH = r"C:/Users/alexf/Documents/GitHub/Seal-The-Deal/ArtSource/Kit/trim/build/TrimBoard_TrimBoard_Low.fbx"
if pr.is_open():
    pr.close()
s = pr.Settings(default_texture_resolution=2048, normal_map_format=pr.NormalMapFormat.OpenGL,
                tangent_space_mode=pr.TangentSpace.PerFragment, import_cameras=False)
pr.create(mesh_file_path=MESH, template_file_path=T, settings=s)
result = "created"
