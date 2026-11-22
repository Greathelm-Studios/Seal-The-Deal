# modelkit

`from modelkit import *` in a model script. Shapes are bmesh objects you transform and combine, then turn into Blender objects with `part()`. Lengths are metres; `MM = 0.001`.

## Params

| Function | Does |
|---|---|
| `P = params(NAME=default, ...)` | Every dimension as a named value; read as `P.NAME`. `build --set NAME=VALUE` overrides one (same type as the default); an unknown name fails the build. The values used go in the build report. |

## Shapes (return a bmesh)

| Function | Makes |
|---|---|
| `box(x, y, z, base='center')` | A box of those full sizes, centred on the origin, or standing on z = 0 with `base='bottom'` |
| `cylinder(radius, depth, segments=32, axis='Z', base='center')` | A capped cylinder along X, Y or Z |
| `cone(r_bottom, r_top, depth, segments=32, axis='Z', base='center')` | A capped cone or frustum |
| `sphere(radius, segments=32, rings=16)` | A UV sphere |

## Changing and combining (change the bmesh in place unless noted)

| Function | Does |
|---|---|
| `move(bm, x, y, z)` | Translate |
| `rotate(bm, degrees, axis='Z', center=(0, 0, 0))` | Rotate about an axis through `center` |
| `scale(bm, x, y, z, center=(0, 0, 0))` | Scale; a negative product also flips the faces so they stay outward |
| `bevel(bm, width, segments=2, angle=30)` | Rounds every edge whose faces meet at more than `angle` degrees |
| `mirror(bm, axis='X')` | Adds the mirror image across the plane through 0, welding the seam |
| `join(*bms)` | New bmesh holding all of them (overlaps are kept, not merged) |
| `array(bm, count, x, y, z)` | New bmesh: `count` copies, each offset by (x, y, z) |
| `boolean(bm, cutter, op='DIFFERENCE', solver='EXACT')` | New bmesh: `DIFFERENCE`, `UNION` or `INTERSECT`. EXACT with self-intersection on, so cutters made with `join()` work. Fails if a DIFFERENCE or UNION leaves no faces |
| `paint(bm, slot)` | Sets every face to material slot `slot` (0-based), before joining pieces in different materials |

## Objects

| Function | Does |
|---|---|
| `material(name, color, metallic=0, roughness=0.5)` | A named material (reused by name). `color` is sRGB 0-1, as a colour picker shows it (hex `#523019` is `(0.32, 0.19, 0.10)`); modelkit converts it to Blender's linear colour. FBX carries the name and base colour |
| `texture(name, maps, projection='box', scale=1.0)` | A material from a Poly Haven texture set downloaded with `mdl.mjs polyhaven get` (`maps` is its `<slug>.json`): base colour, roughness, normal and metallic, one repeat per the set's real-world size times `scale`. `'box'` maps in object space, so it looks the same whatever the UVs are (bakes replace them); `'uv'` follows tiling UVs. FBX carries the name and average colour; bake it for Unity. See [Poly Haven](polyhaven.md) |
| `part(name, bm, materials=None, parent=None, pivot='origin', smooth=30, uv_scale=1.0)` | The object. `materials` is one material or a list in slot order. `pivot` sets the object's own origin (`'origin'`, `'bottom'`, `'center'` or a point) without moving it; it matters for children of a `group()`, such as a door's hinge. `smooth`: faces meeting under that many degrees shade smooth, `None` for flat. UVs are box-mapped at `uv_scale` metres per UV unit so tiling textures match across parts; `uv_scale=None` skips UVs (check will fail it) |
| `group(name, *children, location=(0, 0, 0))` | An empty holding parts: one GameObject with children in Unity. The only allowed parent for a mesh |
| `collider(part, kind='hull')` | `<part>_Collider` beside the part: convex hull (simplified to fit Unity's 255-triangle convex limit) or `'box'` |
| `lods(part, ratios=(0.5, 0.25))` | Renames the part `<name>_LOD0`, adds decimated `<name>_LOD1`.. and puts them (and its collider) under an empty `<name>`: an LOD Group in Unity. Returns the empty |
| `expect(part, size=(x, y, z), tol=0.5 * MM)` | Declares the part's world size; `check` fails when the built part is off by more than `tol` |

## Patterns

- A slot or hole: build the cutter from pieces with `join()` (a stadium is a box and two cylinders), then `boolean(body, cutter)`.
- A trim ring: `boolean(box(outer), box(inner))`, then `array()` it.
- Pieces in two materials in one mesh: `paint()` each piece, `join()` them, pass `part()` both materials in slot order.
- A left-right symmetric part: model one half on +X, then `mirror(bm, 'X')`.
- Parts that move in the game (lid, door, wheel): separate `part()`s under one `group()`, each with `pivot` at its hinge or axle.
- Several variants: one script, values from `params`, built with different `--set` values into different files.
