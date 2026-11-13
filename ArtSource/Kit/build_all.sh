#!/usr/bin/env bash
# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-11
#  Prompt    : "Start the snap kit"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Builds, checks and exports every kit piece in one go, so the whole kit is always
#              rebuilt from the same script values. Outputs go to build/ and export/ beside it.
#  Modified  : Claude Opus 5.5, 2026-11-12 — adds the office-direction pieces (cobalt and portal walls,
#              stair steps, locked door, strip light).
#  Modified  : Claude Opus 5.5, 2026-11-12 — adds the round-5 labyrinth pieces.
# -----------------------------------------------------------------------------
# Usage: ArtSource/Kit/build_all.sh <path to blender-modeling skill>
set -euo pipefail
SKILL="${1:?pass the blender-modeling skill folder}"
HERE="$(cd "$(dirname "$0")" && pwd)"
MDL="node $SKILL/scripts/mdl.mjs"
PIECES="Slab_4x4 Slab_2x4 Slab_4x2 Slab_2x2 Slab_Stairwell Wall_Solid_4m Wall_Solid_2m Wall_Door_4m Door_Leaf
Window_4m Window_2m Column_Facade Column_CornerL Column_CornerR Partition_Glass_4m Partition_Glass_2m Stair_Flight Stair_Column Stair_CagePanel
Wall_Solid_4m_Cobalt Wall_Solid_2m_Cobalt Wall_Solid_4m_PortalHead Wall_Door_4m_PortalHead Stair_Steps Stair_LockedDoor Ceiling_LightStrip
Wall_Return_2x2 Wall_Return_2x2_Cobalt Wall_Niche_4m Counter_Capsule_2m Cover_Block_2m Partition_Frost_4m Partition_Frost_2m
Stair_Straight_2m Ramp_2x4 Guard_Glass_4m Guard_Glass_2m Slab_4x4_LightLine"

for p in $PIECES; do
  $MDL build "$HERE/kit_pieces.py" --out "$HERE/build/$p.blend" --set PIECE="$p" > "$HERE/build/$p.build.log"
  # --watertight is off: panes, rails and slabs with holes are open by design at their joins.
  if $MDL check --blend "$HERE/build/$p.blend" > "$HERE/build/$p.check.log"; then echo "ok    $p"; else echo "FAIL  $p (build/$p.check.log)"; fi
  $MDL export --blend "$HERE/build/$p.blend" --out "$HERE/export/$p" > /dev/null
  $MDL verify --manifest "$HERE/export/$p/manifest.json" > "$HERE/build/$p.verify.log" || echo "VERIFY FAIL $p"
done
