# Window bay and columns

Window_4m and Window_2m use the same sill, rail and pane section. The front sheet compares the two rail finishes at 4 m and includes the 2 m module in A. The room and street views show A. Column_CornerL and Column_CornerR share the isolated corner profile. Their mirrored placement against a return wall still needs checking in the kit.

| Detail | Dimensions and placement | Existing slot |
| --- | --- | --- |
| Solid sill | 4.0 or 2.0 m along X, 0.20 m deep, y = 0 to 1.0 m. Continuous solid cover. | Wall_PaperWhite |
| Sill and head rails | 0.05 m high × 0.10 m deep, centred through the wall. Lower rail at y = 1.00–1.05 m, upper at y = 4.25–4.30 m. Both stop 0.20 m short of each bay endpoint. | Frame_Aluminium |
| Single pane | 3.60 or 1.60 m wide × 3.20 m high × 0.02 m thick, centred between the wall faces, from y = 1.05 to 4.25 m. No intermediate mullion. | Glass_Clear |
| Façade column | 0.40 × 0.24 × 4.30 m (X, Z, Y), centred on the bay endpoint. Each exposed face is 0.02 m ahead of the adjacent wall face. | Wall_PaperWhite, verify against kit |
| Corner column | 0.42 × 0.24 × 4.30 m (X, Z, Y). The isolated view shows the shared profile. Use the kit's handed alignment at the wall return. | Wall_PaperWhite, verify against kit |
| Slab edge | 0.20 m high. Street context at y = −0.20–0 and y = 4.30–4.50 m. The visible edge aligns with the bay face in the study. | Wall_PaperWhite |

- **A** uses Silver Grey #C9CED3. **B** uses Deep Navy #111B38 on the same rail geometry. If Frame_Aluminium is shared across pieces, B needs a finish override or an approved material variant.
- Glass has a pale cool fill with the behind-glass figure slightly lightened to approximate a single pane. Two-pane readability and the no-collider requirement remain engine checks.
- The specified 20 mm column offset is the column's base geometry relative to the wall. Added relief on the columns is zero. The 15 mm allowance applies to added detail.
- The blue cross indicates the bay's room-side left floor pivot. The side elevation and mid-run section show its projection. Each view uses a 1.80 m navy figure. The three-quarter views are illustrative parallel projections. Front, side and section share 94 px/m.
- Blockout files were absent from this workspace and the handoff ZIP. The diagrams use the supplied brief. Check the exact handed corner placement, slab alignment and material assignments against the source kit before modelling.

Regenerate the PNGs with PowerShell by running render.ps1 from this folder.
