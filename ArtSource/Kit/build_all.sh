#!/usr/bin/env bash
# Usage: ArtSource/Kit/build_all.sh <path to blender-modeling skill>
set -euo pipefail
SKILL="${1:?pass the blender-modeling skill folder}"
HERE="$(cd "$(dirname "$0")" && pwd)"
MDL="node $SKILL/scripts/mdl.mjs"
PIECES="Slab_4x4 Slab_2x4 Slab_4x2 Slab_2x2 Slab_Stairwell Wall_Solid_4m Wall_Solid_2m Window_4m Window_2m Column_Facade
Column_CornerL Column_CornerR Partition_Glass_4m Partition_Glass_2m Stair_Flight Stair_Column Stair_CagePanel Wall_Solid_4m_Cobalt
Wall_Solid_2m_Cobalt Wall_Solid_4m_PortalHead Wall_Door_4m_PortalHead Stair_Steps Stair_LockedDoor Ceiling_LightStrip
Wall_Return_2x2 Wall_Return_2x2_Cobalt Wall_Niche_4m Counter_Capsule_2m Cover_Block_2m Partition_Frost_4m Partition_Frost_2m
Stair_Straight_2m Ramp_2x4 Guard_Glass_4m Guard_Glass_2m Slab_4x4_LightLine Sign_Zone_L Sign_Zone_R Sign_Zone_C
Sign_Zone_H Sign_Zone_A Guard_Rail_4m Guard_Rail_2m Partition_Header_4m Partition_Header_2m Partition_Header_Curve_2x2
Carpet_Overlay_4x4 Ceiling_Grid_4x4 Logo_Aonagi Sign_LED_Reception Water_Wall Lift_Door_Frame_4m_1F Lift_Door_Frame_4m_RF
Lift_Door_Panel Lift_Shaft_Wall_4m Lift_Car Pool_Raised_8x4 Wall_Curtain_4m Ceiling_Beams_4x4 Ribbon_Floor_16m Ribbon_Ceiling_16m
Lightbox_Wall_Wave Lightbox_Wall_Logo Ceiling_Panel_4x4 Partition_Glass_Door_2m Stair_Straight_Half_2m
Partition_Fluted_4m Partition_Fluted_2m Partition_Louvre_4m Partition_Louvre_2m Pendant_Sphere"

for p in $PIECES; do
  $MDL build "$HERE/kit_pieces.py" --out "$HERE/build/$p.blend" --set PIECE="$p" > "$HERE/build/$p.build.log"
  # --watertight is off: panes, rails and slabs with holes are open by design at their joins.
  if $MDL check --blend "$HERE/build/$p.blend" > "$HERE/build/$p.check.log"; then echo "ok    $p"; else echo "FAIL  $p (build/$p.check.log)"; fi
  $MDL export --blend "$HERE/build/$p.blend" --out "$HERE/export/$p" > /dev/null
  $MDL verify --manifest "$HERE/export/$p/manifest.json" > "$HERE/build/$p.verify.log" || echo "VERIFY FAIL $p"
done

# Aero office props and doors on the trim sheet (props_aero.py, trim/): build, trim unwrap, merge, check, export, verify.
AERO="Monitor_Flat PC_Tower Keyboard Workstation_Bench Chair_Task Chair_Pod Phone_Desk Doc_Tray Desk_Sculpt Desk_Reception
Storage_Low Copier Water_Server Umbrella_Stand Vending_Machine Slipper_Cabinet Planter_Box Light_Pendant_Linear
Column_Graphic Column_Graphic_Tall Door_Leaf Wall_Door_4m Wall_Door_2m"
bash "$HERE/trim/build_props.sh" "$SKILL" $AERO
