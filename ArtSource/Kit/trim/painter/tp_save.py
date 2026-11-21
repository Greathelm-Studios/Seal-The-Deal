# Painter: save the project beside the sheet's source so it can be opened and repainted by hand.
import substance_painter.project as pr
pr.save_as(r"C:/Users/alexf/Documents/GitHub/Seal-The-Deal/ArtSource/Kit/trim/painter/Trim_Aero.spp", pr.ProjectSaveMode.Full)
result = pr.file_path()
