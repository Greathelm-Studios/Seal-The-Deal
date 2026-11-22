# ITEMS

**Status:** Current · **Last updated:** 2026-11-22

## What the player sees

Look at a loose item or throwable prop within reach and a prompt names it (**F - Pick up Chair**). Press **F** to hold it in your single hands slot. While holding something: **LMB** throws, **G** drops, and **RMB** uses gadgets. Full hands cannot fire weapons.

- **Furniture** (chair, monitor, filing cabinet, cubicle panel): heavy items slow movement and shorten jumps. Thrown items shove and damage anyone hit based on speed and mass. Too-heavy props (desks, copiers) show **Too heavy**.
- **Cubicle panel:** stays solid in your hands as moving cover (blocks shots and other players without colliding with the carrier).
- **Grenade Case:** RMB arms a 3.5 s fuse with accelerating beeps and flashing light, then blasts players and furniture wherever it is (even in hand). Visually distinct from the objective briefcase (red hazard styling, keypad, warning light).
- **Fire Extinguisher:** RMB pulls the pin for 6 s of unstoppable smoke. Carried, it trails smoke behind you; dropped, escaping gas skitters it across the floor.
- **Wet Floor Sign:** RMB places it ahead on flat ground, spreading a puddle that trips sprinting players from either firm (walking is safe). Picking it up or knocking it aside clears the trap.
- **Pistol and AR-15:** real pickups; in `Lane_Integration.unity` and `KitOffice.unity` taking one moves it into weapon slots (refill if you carry that gun, else an empty slot, else it swaps with the gun in hand, which drops in front of you; [INTEGRATION.md](INTEGRATION.md)), while scenes without `GunPickups` carry them like loose items. In `KitOffice.unity` two pistols and two AR-15s lie near the player spawn and a new one appears at its spot 20 s after it is taken. This works in the Editor too.

Knocked-loose items stay where they drop. Anything falling out of the building returns to its starting spot after 2 seconds. Used-up gadgets respawn at their home spot after `ReturnsAfterUsedSeconds`.

## Ownership and netcode boundary (`AGENTS.md §3`)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Who holds an item | Host validates reach, weight, empty hands, and state | Holder `NetworkObject` `SyncVar` (`Pickup`) | On change |
| Out of play / back in play | Host | `_away` `bool` `SyncVar` (`Pickup`) | On change |
| Loose item motion | Host (`Prop`, [PROPS.md](PROPS.md)) | Switchboard `NetBody` ([SWITCHBOARD.md](SWITCHBOARD.md)) | While moving, inside Switchboard budget |
| Carried item solid collider | Host moves shields/gadgets each tick; plain items stay parked while hidden | Switchboard `NetBody` (shields/gadgets only) | While moving |
| Carried item visuals | Local on every machine (`_visual` or `HandsCopy` follows view or smoothed body) | Nothing | Never |
| Throw, drop, use | Owner sends `ServerRpc`; host executes | Holder change + velocity | On press |
| Refusals | Host sends `TargetRpc` (`DenyReason`) | One enum byte | On refusal |
| Impact shove and damage | Host collision only | Health/body state + `ObserversImpact` (0.1 s cooldown) | On hit |
| Grenade Case fuse & blast | Host | `_detonateTick` `SyncVar` + `ObserversExploded` | On arm / detonation |
| Extinguisher smoke & skitter | Host | `_ventUntilTick` and `_empty` `SyncVar`s (smoke is local particles) | On pull / empty |
| Wet floor slips | Host | `_down` `SyncVar` + `ObserversSlipped` | On change / slip |
| Carry slowdown | Every machine derives from `Held` weight | Nothing | Never |
| Loose guns at fixed spots (`LooseGunSpawns`, scenes with no tower) | Host spawns the mapped pickup prefab at each spot and again after the respawn delay | Nothing of its own; only the spawned pickups replicate as normal `Pickup` objects | Spawn at start, then once per taken gun after `_respawnSeconds` (default 20 s, 0 = never) |

Not yet verified over Steam or under real network latency across multiple computers.

**Gun pickups in KitOffice and the Editor: checked as host by script, not by hand (2026-11-22).** In KitOffice the four guns spawned at their spots, the real carrier was live in the Editor (`ItemCarrier.Local` set, fire held with full hands), and a scripted take of an AR-15 from 1.8 m used the pickup up and left the gun in its slot. Not checked: pressing F by hand, the on-screen prompt (the test ran with the game menu open, which hides the HUD), the respawn after 20 seconds, a swap that drops the old gun, and anything from a client. The pickups show the guns' own models ([HeldWeaponPrefabs.cs](../../Assets/_Project/Editor/Art/HeldWeaponPrefabs.cs) swaps the stand-in cubes under `Visual`); they stand upright on their colliders rather than lying on their side.

## Loose guns in a scene with no tower

`LooseGunSpawns` (`Scripts/Integration/`) is a host-only component holding a list of spots (a child `Transform` plus which gun from `GunPickupMap`). On server start it spawns the mapped pickup prefab at each spot through FishNet, the same way `FloorLoot` does, and spawns a new one `_respawnSeconds` after a gun is taken (0 = never). Move a spot by moving its child object. When a swapped-out gun drops and the scene has no `FloorLoot`, `GunPickups` drops it through `LooseGunSpawns.ServerSpawnLoose` instead; a swapped-out gun is not put back. `Lane_Integration.unity` is unchanged: it has `FloorLoot` and no `LooseGunSpawns`.

Add it to KitOffice with **Seal the Deal > Art > Add Gun Pickups To KitOffice** (with `KitOffice.unity` open). It adds only what is missing (`GunPickups`, `SharedHands`, `LooseGunSpawns` with four default spots, the item HUD), never moves or rebuilds what exists, and leaves the scene unsaved.

## Architecture and design rationale

- **Centralized host tick driver (`Pickup`):** Instead of subscribing every spawned pickup to FishNet's `TimeManager.OnTick`, `Pickup` maintains static server lists (`_allServer`, `_carried`, `_awayList`, `_liveList`) with O(1) swap-and-pop index tracking (`_allServerIndex`, etc.). One static `DriverTick()` processes return timers, updates carried solid colliders (`FollowHolder`), retires sleeping/expired live throws, and sweeps `_allServer` at a budgeted ~4 checks/s per item (`_sweepBudget`) for out-of-bounds recovery.
- **Carry visual vs solid collider split:** Plain carried items disable colliders and leave their rigidbody parked where picked up (so Switchboard treats them as sleeping and sends zero transform updates). Clients interpolate visuals in `LateUpdate` (order 100, after `PlayerCamera`), and on release wait up to `AwaitPoseSeconds` (0.4 s) for the host's released pose before blending onto the physical body. Shields (`CoverWhileCarried`) and gadgets (`ItemBehaviour != null`) move their solid rigidbody each host tick via `MovePosition`/`MoveRotation` on the `Player` layer while ignoring the carrier's capsule.
- **Teleport sync with Switchboard:** `Pickup.ServerTeleport` wakes the rigidbody and calls `NetBody.ForceSync()` because Switchboard ignores sleeping bodies.
- **Grab occlusion (`ItemCarrier`):** Reach checks use `ItemLayers.LookMask` (`~Player`) so walls, floors, and locked doors (which share the `Prop` layer) block grabs while permitting adjacent loose props.
- **Any loose prop (`LooseItemDefinition`, `HandsCopy`):** Props with `Prop` and `Pickup` but no `ItemDefinition` derive and cache a runtime `ItemDefinition` keyed by `(CarryProfile, ReadableName, mass, localBounds)` from `Rigidbody.mass` and bounds. Props without a `_visual` child lazily build a visual-only `HandsCopy` (mesh renderers only; skinned meshes require an authored `Visual` child). `NotThrowable` excludes props from `Set Up Pickups` and runtime offers.
- **Carry slowdown (`MotorSpeedOverride`):** Clones `MovementProfile` into a slowed runtime copy without mutating the designer asset.
- **Audio pooling (`ItemAudio`):** Uses a 24-voice 3D `AudioSource` pool routed through `ItemSoundSet.Output` (`GameMixer.mixer` SFX bus) with a 40 m hearing cull, replacing `AudioSource.PlayClipAtPoint` so mixer volume applies and zero objects allocate per sound.

## Rules and tuning

Authored assets under `Assets/_Project/Profiles/Items/`:

- `Item_*.asset` (`ItemDefinition`): `DisplayName`, `Kind`, `WeightKg` (keep equal to `Rigidbody.mass`), `HeldOffset`/`HeldEuler`/`HoldClearance`, `CoverWhileCarried`, hand seats, `ThrowSpeed`, `ThrowTumble`, `Sounds`, `UseVerb`, `ReturnsAfterUsedSeconds`.
- `DefaultCarry.asset` (`CarryProfile`): reach (2.5 m + 0.75 m lag allowance), `MaxCarryWeight` (35 kg), `FreeCarryWeight` (3 kg), slowdown fractions (45% speed, 50% jump at max weight), `InheritVelocity`, `RetakeSeconds` (0.6 s), throw impact formula (`(speed - 4) * kg * 0.35` damage, `speed * kg * 0.08` shove capped at 12), and out-of-building thresholds (`BelowGroundMetres`, `OutsideDropMetres`).
- `GrenadeCase.asset`, `GrenadeCaseBlast.asset`, `Extinguisher.asset`, `WetFloor.asset`: per-gadget tuning.

## UI and audio contract

- **HUD events (`CloseTheDeal.Items`):** `ItemCarrier.LocalChanged`, `ItemCarrier.UsingPadChanged`, `ItemCarrier.KeyName`, `PromptChanged`, `HeldChanged`, `Denied`, `Load`; `Pickup.Taken`, `Released`, `Impact`, `Returned`, `ServerThrowLanded`; gadget events on `GrenadeCase`, `FireExtinguisher`, `WetFloorSign`.
- **Refusal wording (`ItemHud.RefusalText`):** `Gone` ("Someone else got it"), `HandsFull` ("Hands full · G to drop" / D-pad down), `TooHeavy` ("Too heavy to lift"), `TooFar` ("Too far away"), `CannotUseHere` ("No room to put it down here").
- **Look highlight (`LookHighlight`):** 28% `_BaseColor` lift via a shared `MaterialPropertyBlock` on renderers without an existing block.
- **HUD (`ItemHud`):** built from UI kit `Plate`, `KeyHint`, `UiTokens`, `UiMotion`, and `UiSound`. Added via **Seal the Deal > Lanes > Add Item HUD To Open Scene**.
- **World sounds (`ItemSoundSet`):** `PickUp`, `Drop`, `Throw`, `ImpactLight`/`ImpactHeavy` (6 kg threshold), `Arm`, `Beep`, `Explosion`, `VentLoop`, `VentEnd`, `WetFloorDown`, `Slip` under `Assets/_Project/Audio/Items/`.

## Files and setup

Under [Assets/_Project/Scripts/Items/](../../Assets/_Project/Scripts/Items/): `ItemCarrier.cs`, `Pickup.cs`, `ItemDefinition.cs`, `CarryProfile.cs`, `LooseItemDefinition.cs`, `HandsCopy.cs`, `ItemBehaviour.cs`, `GrenadeCase.cs`, `GrenadeCaseProfile.cs`, `FireExtinguisher.cs`, `ExtinguisherProfile.cs`, `WetFloorSign.cs`, `WetFloorProfile.cs`, `ItemBlast.cs`, `MotorSpeedOverride.cs`, `ItemLayers.cs`, `ItemSoundSet.cs`, `ItemAudio.cs`, `LookHighlight.cs`, `DamageDummy.cs`, `ItemHud.cs`. Shared interface: [IDamageable.cs](../../Assets/_Project/Scripts/Core/IDamageable.cs).

Setup: **Seal the Deal > Lanes > Set Up Pickups** ([PickupsLaneSetup.cs](../../Assets/_Project/Editor/Lanes/PickupsLaneSetup.cs)) and **Seal the Deal > Items > Make Selected Prefabs Throwable**.

## Known limitations

- **Workaround in `MotorSpeedOverride`:** Mutates `PlayerMotor`'s private `_profile` field via reflection until `PlayerMotor` exposes a public profile-override method.
- **`DevCarry` stand-in:** `ItemCarrier` now runs in the Editor and dev builds. `DevCarry` (host-only rigidbody grab on F/G) stands down whenever the local player has an `ItemCarrier`, and still serves a player without one. Its thrown-prop hits on the hit dummy (`ThrownHitTracker`) therefore no longer happen where `ItemCarrier` is present; real throws report through `Pickup.ServerThrowLanded`.
- **Remote item pitch:** Remote players' held items render level at chest height because pitch is not synced.
- **Moving cover push:** Carried cubicle panels act as unpushable moving walls on the host.
- **Local smoke puffs:** Extinguisher particle shapes differ slightly across machines (timing and world bounds match).
- **Single throw strength:** No throw charge or arc preview.
- **Stand-in sounds and no first-person carry arms:** Sounds are generated clips; carried items float without hand/arm meshes yet.
