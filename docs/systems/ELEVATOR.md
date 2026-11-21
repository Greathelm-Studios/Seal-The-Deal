# ELEVATOR

**Status:** Target · **Last updated:** 2026-11-21

Dev-scene work in `KitOffice.unity` at Alex's request, not on the v1 list in [GDD.md](../GDD.md). Compiled and ridden in host playtests (car and rider heights matched, doors parted 0.75 m each way), but not yet walked by a human or verified from a client, so it remains Target (`AGENTS.md §3`).

## What the player sees

Beside reception, north-east of the atrium, a glass-backed capsule lift waits with its doors open. Walk in and stand for two seconds: the doors close, the car rides up past the upper floor behind glass, and the doors open onto the open rooftop range (hit dummy, arsenal display, and loose props). Stand in the car again to ride down, or wait in front of closed doors to call the car. There is no button to press.

## Who owns what (`AGENTS.md §3`)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| When a ride starts, and from which stop | Host | One `LiftRide` value (`FromStop`, `DepartTick`, `Departed`) | On change: once per ride, a few bytes |
| Car height, door positions | Deterministic function of host tick on every machine | Nothing | Never |
| Riders and callers | Host polls cabin box and opposite call point | Nothing | Host only, ~10 Hz while idle (`PollIntervalSeconds = 0.1f`) |
| Props in the car | Host, as all props ([PROPS.md](PROPS.md)) | Normal prop stream | Unchanged |

## Architecture and design rationale

- **Tick-derived motion over streamed transforms:** The player body is client-predicted and re-simulated on every reconcile. A streamed car would sit at the wrong height during replay ticks and correct the rider continuously. Because `CarPosition` and `DoorOpen` are pure functions of the host tick (`DepartTick + 0.25 s` schedule lead so the `SyncVar` reaches clients before doors move), `OnPreReplicateReplay` rewinds the car to `serverTick` and steps it to `serverTick + 1`, matching predicted player movement.
- **Client tick alignment:** `OnPreReconcile(clientTick, serverTick)` records `_localAheadOfHost` so clients translate `TimeManager.LocalTick` into the host tick.
- **Trapezoidal speed profile and early-out:** `Awake` precomputes acceleration, cruise, and deceleration durations from `_speed`, `_acceleration`, and `_travel` (dropping the cruise phase if `2 * accelDistance >= _travel`). `Place` sets `_restPlaced` once a ride finishes so idle ticks do no transform work.
- **Designer authority:** The scene builder wires `_car`, `_doorLeaves`, `_doorSlides`, and `_callPoints`. Authored rest positions are read once in `Awake` and never written back. Door slides are authored in each leaf's local space (so mirrored pairs both open along `+X`) and rotated into parent space at startup.
- **Fallback:** A closed-door teleport ride was rejected because it hides the ride and cannot carry loose props, but remains the fallback if client riding jitters.

## Files and tuning

- [Elevator.cs](../../Assets/_Project/Scripts/Lift/Elevator.cs): `Travel` (m), `Speed` (m/s), `Acceleration` (m/s²), `DoorSeconds` (s), `DwellSeconds` (s), `CabinCentre`, `CabinSize`, and `CallRadius` (m).
- [KitOfficeRoof.cs](../../Assets/_Project/Editor/Art/KitOfficeRoof.cs): roof builder (`Seal the Deal > Art > Add Kit Office Roof Range`), adds the `Elevator`, `NetworkObject`, kinematic car body, and call spots when the `Lift` group exists.
- [KitOfficeScene.cs](../../Assets/_Project/Editor/Art/KitOfficeScene.cs): places the lift kit pieces and invokes `KitOfficeRoof.AddTo` at the end of a rebuild.

## The rooftop range

Open to the sky, roof-local metres (x east, z south, roof slab top at `y = 9`).

| What | Where | Why there |
|---|---|---|
| Glass guard rails | Perimeter | Prevents walking off |
| Hit dummy, facing west | (27, -19) | 4 m clear floor all round; moves up from the upper floor once the lift exists |
| Full-height glass screens | East edge south of z -12, south edge east of x 20 | Catches blasted bodies that would clear a 1.1 m rail |
| Second `DevMirror`, 8 m | Inside the east screen, behind the dummy | Reactions are judged in a mirror |
| Arsenal bay, 8 x 6 m, with rack wall | Corner (7, -16), wall along z -22 | East edge is a 12 m firing line; three counters carry display-only stand-in weapons from `Assets/_Project/Prefabs/Arsenal/` |
| Loose props: 3 monitors, 3 chairs, 2 cabinets | x 19 to 21, z -14.5 to -21.5 | Scene-placed throwables (8 of the 16-per-floor cap in [PROPS.md](PROPS.md)) |
| Raised pool, 8 x 4 m | Corner (6, -6) | `Pool_Raised_8x4`, knee-deep, no swimming |

## Known limitations

- Two stops only (the upper floor faces the atrium void).
- Ragdoll limbs can dip through the moving car floor because ragdoll physics only resolves against fixed colliders.
- Car motion updates once per tick with no sub-tick visual smoothing.
- Doors close regardless of objects in the doorway, and the car descends regardless of objects underneath.
- Not yet tested from a connected client.
