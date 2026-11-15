#!/usr/bin/env bash
# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: Aero office props on one Painter trim sheet
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Builds each props_aero.py piece onto the trim sheet: build, trim-mode unwrap at the sheet's density,
#              merge the sheet's materials into Trim_Aero, check, export and verify into the kit's build/ and export/,
#              where KitImport reads them. One script so every prop goes through the same steps.
# -----------------------------------------------------------------------------
# Usage: ArtSource/Kit/trim/build_props.sh <blender-modeling skill folder> <piece>...
set -euo pipefail
SKILL="${1:?pass the blender-modeling skill folder}"; shift
KIT="$(cd "$(dirname "$0")/.." && pwd)"
MDL="node $SKILL/scripts/mdl.mjs"
BLENDER="$($MDL blender | tail -1)"
for p in "$@"; do
  b="$KIT/build/$p"
  $MDL build "$KIT/props_aero.py" --out "$b.blend" --set PIECE="$p" > "$b.build.log"
  args="$(python "$KIT/trim/trim_args.py" "$b.build.json")"
  # WHY 1024 px/m at 2048: the sheet's own density (trim_layout.json), so the props' detail matches the board's.
  $MDL unwrap --blend "$b.blend" --out "$b.uv.blend" --mode trim --density 1024 --size 2048 --trim "$args" > "$b.unwrap.log"
  # WHY build/trim/<piece>.blend: the export names the FBX after the .blend, and KitImport reads export/<piece>/<piece>.fbx.
  t="$KIT/build/trim/$p.blend"
  mkdir -p "$KIT/build/trim"
  "$BLENDER" -b "$b.uv.blend" --factory-startup -P "$KIT/trim/apply_trim.py" -- "$t" 2>&1 | grep TRIM_MERGED > "$b.trim.log" || { echo "TRIM FAIL $p"; continue; }
  if $MDL check --blend "$t" > "$b.check.log"; then echo "ok    $p"; else echo "FAIL  $p (build/$p.check.log)"; fi
  rm -f "$KIT/export/$p/$p.trim.fbx"
  $MDL export --blend "$t" --out "$KIT/export/$p" > /dev/null
  $MDL verify --manifest "$KIT/export/$p/manifest.json" > "$b.verify.log" || echo "VERIFY FAIL $p"
done
