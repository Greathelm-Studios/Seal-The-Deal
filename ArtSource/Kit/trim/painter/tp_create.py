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
