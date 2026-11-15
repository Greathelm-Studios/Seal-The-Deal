# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: "making a trimsheet in substance ... you can use substance painter"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Painter: saves the project beside its scripts so it can be repainted by hand.
#              Run from ArtSource/Kit/trim/painter with: python sp.py tp_save.py
# -----------------------------------------------------------------------------
# Painter: save the project beside the sheet's source so it can be opened and repainted by hand.
import substance_painter.project as pr
pr.save_as(r"C:/Users/alexf/Documents/GitHub/Seal-The-Deal/ArtSource/Kit/trim/painter/Trim_Aero.spp", pr.ProjectSaveMode.Full)
result = pr.file_path()
