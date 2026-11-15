# L2 detailed design notes

**Status:** Target concept.

Times are drawn path length divided by 9 m/s, not observed travel or shortest-path guarantees; doors, turns, acceleration and combat add time.
Enclosed stair length includes two 3.6 m runs rising 2.25 m each, a 3.6 m return and two 2 m end landings; feature flight rises 4.5 m over 7.2 m.
L1_C times are normalised to include the same 4 m of end landings omitted by its original metric.
The case's fast exposed climb is intentionally slower than C's nearly immediate feature access, leaving a contest before ascent.
The 35 m window/lobby axes remain a risk; short room fights are a design intention, not a measured engagement distribution.
Area uses L1's exact rectangle-union method; circulation is 17.5% of gross, or 20.8% of the stated planning NIA proxy, not a certified office efficiency figure.

**MATCH fit and limits:** read with `git show origin/main:docs/systems/MATCH.md` at `3b9d5efbc1fc19774f91c7e7b4fea414247b29f9`, without switching branches.
Keep one shared case, 30 m beacon, carrier-led global floor opening, per-firm checkpoints and a roof win; every lower-floor point is within 25.62 m horizontally of the central case, but this does not guarantee visibility between floors.
G1 is E1's single south arrival face (6 m), G2 E2's north face (6 m), GF the feature's west arrival (2 m); red traces show upper arrivals, not shut gates on the starting floor.
Continuous body-blocking sides above the ramps, plus roof cages, are proposed to prevent leaving a stair around its gate; ordinary breakable partitions and 1.1 m guards cannot provide this boundary.
Shots still pass through clear cage faces and frosted gates: neither is ballistic cover.
The solid south landing slab gives a 4 × 2 m shelter at 4.3 m clear height; the transparent north bridge gives information and crossing access, not overhead cover.
Upper routes follow the guarded void's edges and north bridge, rather than the lower floor's diagonal across Court.
The three floors register at 0 / 4.5 / 9 m, roof at 13.5 m; core, stair and gate ports repeat while fit-out changes.

If two towers stay, the checkpoint skybridge attaches through the east 2 m port at **(40,14.8)**, or the rotated west port **(0,17.2)** in the facing tower; keep its deck at the existing 0.6 m sill height and preserve the mantle.
The brief's court case start and two firms inside one plate are proposals: the implemented mode still starts the case on the plaza and maps each firm/checkpoint to a separate tower.
This floor is not a drop-in replacement for the existing spiral-cage gate builder or tower arena maths.
Future case, gate opening and checkpoint outcomes remain host-owned, synced on change under the existing match contract; this concept adds no network messages.

**Adam to decide before a build:** one building versus two towers; court versus plaza case start; firm territory/checkpoint and case-return locations in one building; floor gates versus breakable locked doors; whether body-blocking stair cages are exempt from ordinary glass breakage; lift availability remains roadmap.
Spawn positions, enclosed stairs and case distances are rotationally balanced; the single directional feature flight and shared north lift are not fully symmetric and need a play review.

**Kit:** reuse existing slab, wall/door, window, clear/frost partition, capsule counter, guard and `Stair_Straight_2m` families; rotate the straight flight 90°.
Proposals only: a 6 × 8 m enclosed return-stair assembly using two measured half-rise ramps, a body-blocking full-height clear cage with arrival gate, and an 8 × 2.4 m glass bridge deck; existing 1.1 m guards alone are insufficient.
No kit, Unity scene, prefab, runtime behaviour or owning system doc changed.


The opening is 56.0 sq m after two 0.4 x 2m landing pockets are deducted from the 57.6 sq m rectangular reservation.
Stair cage collision, under-flight access, roof containment and all mantle/jump bypasses require implementation review; the diagrams do not validate movement.
