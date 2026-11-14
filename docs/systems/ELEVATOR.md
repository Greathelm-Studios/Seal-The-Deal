<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Fable 5.2 (Claude Code)
  Generated : 2026-11-13
  Prompt    : Alex: "make the dummy and arsenal section be on the ceiling. to access it, you should [take] an elevator"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : The owning doc for the lift and the rooftop range it serves, so the netcode ownership is written
              down before the code is trusted (AGENTS.md §3).
-->
# ELEVATOR

**Status:** Target · **Last updated:** 2026-11-13 · **Verified against:** the working tree on `Alex_Env`: both scripts compile; the roof was built once in the open scene and rendered; the lift is not in a scene and has not been ridden

This is dev-scene work in `KitOffice.unity` at Alex's request. It is not on the v1 list in [GDD.md](../GDD.md).

## What happens in the game

Beside reception, north-east of the atrium, a glass-backed capsule lift waits with its doors open.
Walk in and stand for two seconds: the doors close, the car rides up past the upper floor behind glass and opens onto the open rooftop.
The rooftop is the test range: the hit dummy, the arsenal and loose props to throw.
Stand in the car again to ride down. Wait in front of closed doors and the car comes to you.
There is no button to press.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| When a ride starts, and from which stop | Host | One `LiftRide` value (stop, start tick) | On change: once per ride, a few bytes |
| Car height, door positions | Nobody: every machine computes them from the ride's start tick | Nothing | Never |
| Riders and callers | Host checks who stands in the car or at the other stop | Nothing | Host only, two overlap checks per tick while the lift is idle |
| Props in the car | Host, as all props ([PROPS.md](PROPS.md)) | The normal prop stream | Unchanged |

A client never starts a ride and never moves the car on its own authority.
The player body is client-predicted, so on a reconcile the client puts the car back where it was for each replayed tick; that is why the car is a function of the tick and not a streamed transform.
The rejected alternative is a closed-door ride where the host teleports the riders: simpler, but it cannot carry loose props and hides the ride. It is the fallback if riding jitters from a client.

## Code

- [Elevator.cs](../../Assets/_Project/Scripts/Lift/Elevator.cs): the whole system. Tuning is on the component: travel (m), top speed (m/s), acceleration (m/s²), door time (s), dwell time (s), the cabin box and the call radius.
- [KitOfficeRoof.cs](../../Assets/_Project/Editor/Art/KitOfficeRoof.cs): the roof's builder, menu `Close the Deal > Art > Add Kit Office Roof Range`. It rebuilds one "Roof range" group and, when the kit's "Lift" group is in the scene, adds the `Elevator`, its `NetworkObject`, the car's kinematic body and the call spots.
- Planned, not yet done: [KitOfficeScene.cs](../../Assets/_Project/Editor/Art/KitOfficeScene.cs) calls `KitOfficeRoof.AddTo` at the end of its build, and places the kit pieces `Lift_Car`, `Lift_Door_Panel` and `Lift_Door_Frame_4m_1F` / `_RF` under a "Lift" group. Until that call exists, a rebuild of KitOffice loses the roof and the menu item puts it back.

## The rooftop range

Open to the sky: nothing is overhead except the lift's head-house. Roof-local metres, x east, z south, the roof slab's top at y 9.

| What | Where | Why there |
|---|---|---|
| Glass guard rails | The whole edge | Nobody walks off |
| Hit dummy, facing west | (27, −19) | 4 m of clear floor all round, as the hit-reaction session asked |
| Full-height glass screens | East edge south of z −12, south edge east of x 20 | A blasted body must not clear a 1.1 m rail; walls within 6 m of the dummy |
| Second DevMirror, 8 m | Inside the east screen, behind the dummy | Reactions are judged in a mirror |
| Arsenal bay, 8 × 6 m, with a rack wall | Corner (7, −16), wall along z −22 | Its east edge is the firing line, 12 m from the dummy. Empty until weapon pickups exist; the weapons session parents racks to `ArsenalBay` |
| Loose props: 3 monitors, 3 chairs, 2 cabinets | x 19 to 21, z −14.5 to −21.5 | Things to throw at the dummy. 8 of the 16-per-floor cap ([PROPS.md](PROPS.md)). Placed as scene objects because KitOffice has no tower for `PropSpawner` |
| Raised pool, 8 × 4 m | Corner (6, −6) | Alex: "what could be funny is if added a pool". Skipped until the kit piece `Pool_Raised_8x4` exists; knee-deep, no swimming |

## Known limitations

- Two stops only. The upper floor has no stop: its side of the shaft faces the atrium void.
- A ragdolling body's limbs can dip through the moving car floor: the ragdoll's physics copy takes fixed colliders only ([CHARACTER_ANIMATION.md](CHARACTER_ANIMATION.md)).
- The car moves once per tick with no in-between smoothing, so it may look steppier than the rider's own smoothed body.
- Doors close whether or not something is in the doorway, and the car comes down whether or not something is under it.
- Not yet seen from a client, so by AGENTS.md §3 it is not done.
