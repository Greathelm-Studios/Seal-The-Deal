# L2 evidence and limitations

**Status:** Target concept validation · **Last updated:** 2026-11-15.

**Validation:** `node ArtSource/Kit/concepts/Layout/L2/draw_L2.mjs` generated SVGs, display lists and metrics; `& ArtSource/Kit/concepts/Layout/L2/render_L2.ps1` rendered three 2680 × 1880 PNG sheets and the 4020 × 2040 comparison at a common 20 px/m.
Manually inspected plan, flow, stack and comparison; reviewed door offsets, case routes and overhead/arrival boundaries against the metre coordinates.
L1's survey samples positions every 0.5 m and directions every 2° at 1.6 m eye height, with open doors, transparent ordinary glass and conservatively opaque stair projections; these are distinct sampled axes, not a proven global maximum.
The under-flight A-entry sightline crosses the ramp beneath at least 2.55 m of underside clearance geometrically; actual collision and movement remain for an implementation review.
The four required L1 input hashes are unchanged in [source_provenance.json](source_provenance.json); the renderer reads L1 PNGs without rewriting them.
No automated tests, Unity, Play Mode or multiplayer checks ran; no such results are claimed.
`node --check ArtSource/Kit/concepts/Layout/L2/draw_L2.mjs` exited 0 with no syntax errors.
The diagnosis's L1 comparison README link is dead in this checkout; supplied JSON geometry/metrics provided the comparison instead.
Git status initially hit an LFS permission error; a read-only status limited to Layout succeeded with LFS filters disabled for that command.


Generation prompt and built-in tool provenance: [view_prompt.md](view_prompt.md).
