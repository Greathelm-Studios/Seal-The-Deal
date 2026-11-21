#!/usr/bin/env bash
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
