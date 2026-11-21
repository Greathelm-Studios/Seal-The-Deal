# TOWER

**Status:** Current · **Last updated:** 2026-11-21

## What the player sees

Two office towers stand facing each other across the street. Each team starts in its own lobby at the bottom and climbs floor by floor up an open spiral staircase near the middle of every floor. Rivals can see and shoot across the street through the open window bays. On every floor above the lobby, a locked red door stands across the stairs just past the arrival point; three blasts break it open. Every few floors is a checkpoint, near the top is the boardroom, and above it is the roof.

The host picks a seed on start, and every machine builds identical towers locally from that seed (both towers share the same seed by default so neither team gets an easier layout).

## Who owns what (`AGENTS.md §3`)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Seed | Host picks on start and on `Rebuild()` | One `uint` `SyncVar` | Once per build |
| Buildings | Built locally on every machine from the seed (static geometry, not networked) | Nothing | Never |
| Team and spawn | Host (`TeamSpawner`: balances to fewer players, A on tie; leavers free seats) | Firm index on player spawn (`FirmMember`) | On join |
| Locked doors | Host spawns at markers via `PropSpawner` and counts blast hits | Hits taken (`byte` `SyncVar`); hides at limit | Only when hit |

Nothing about the buildings is sent over the network beyond the seed. Locked doors are the exception: they are networked objects spawned like furniture. A client's predicted body collides with a door until the host's broken state arrives, so stepping through right as it breaks can correct by a few centimetres.

## Architecture and design rationale

- **Runtime static batching and mesh cleanup (`TowerBuilder`):** Floors are instantiated at runtime from `FloorTemplate` prefabs, so Unity's build-time static batching never sees them. After instantiating all floors for a building, `TowerBuilder` runs `StaticBatchingUtility.Combine` on the `Transform[]` parts listed in `FloorTemplate.NeverMoves`. Because `Combine` creates runtime meshes not backed by assets, `NoteOwnMeshes()` snapshots the original `sharedMesh` references right before merging and `RecordMergedMeshes()` records only the newly generated meshes so `DestroyMergedMeshes()` frees them on `Clear()` and `OnDestroy()`, preventing rematch mesh leaks without touching asset meshes.
- **Immediate detach on rebuild:** `Clear()` deactivates and unparents old building roots (`SetActive(false)` and `SetParent(null)`) before calling `Destroy`, because `Destroy` defers until end of frame and listeners running during `OnBuilt` (such as the glass registry) must never walk stale building children.
- **Deterministic layout (`TowerLayout`):** Turns `seed` + `TowerProfile` into a bottom-to-top `FloorSpec` list using `Unity.Mathematics.Random`, never repeating the same ordinary office template twice in a row when multiple exist.
- **Two-pass lobby team assignment (`TeamSpawner`):** `AssignTeams` removes all listed players from their old seats before seating them in their new teams so simultaneous team swaps never collide on a spawn slot. A 5th+ player on a team (possible when a lobby forces teams) wraps round-robin across the 4 lobby spawn slots. `ResetStatics` intentionally preserves `OnPlayerSpawning` because `FirmMember` hooks it at `SubsystemRegistration`.
- **Designer-authored vs runtime-derived (`AGENTS.md §4`):** Authored values are `TowerProfile` fields, `FloorTemplate` prefabs, and lobby spawn markers. Derived values are the chosen seed, floor stack, building transforms, and runtime batched meshes.

## The floor stack and template convention

From bottom to top: **Lobby**, then ordinary **Office** floors with a **Checkpoint** inserted after every `CheckpointEvery` floors (including straight before the boardroom when evenly divisible), then **Boardroom**, then **Roof**. Defaults: 9 ordinary floors, checkpoint every 3 (15 floors total).

Every floor is a prefab with `FloorTemplate` sharing the standard shell:

- **Pivot:** centre of the front (window) edge at floor level. Local `+Z` faces the rival tower; Building B is rotated 180 degrees.
- **Footprint:** 28 m wide, 14 m main floor + 4 m service spine deep, 4.5 m floor to floor (`TowerProfile` holds the dimensions).
- **Window side (`+Z`):** 0.6 m sill with pillars every 4 m and breakable `GlassPane` bays (`Windows` group, kept outside `NeverMoves`). The middle bay of Lobby and Checkpoint floors opens to the plaza and skybridges ([MATCH.md](MATCH.md)); Roof has a 1.2 m parapet.
- **Service spine (back 4 m):** WC, STORE, and UTIL at 30 / 40 / 30% width, each with a 1.2 m door.
- **Stairs:** open clockwise spiral around a 0.8 m column (`Assets/_Project/Floors/SpiralFlight.asset` ramp collider), 1.2 m clearance from the spine, starting and arriving on the window side.
- **Locked door and cage:** on every floor above Lobby, `Prefabs/Props/LockedDoor.prefab` spans the treads 30 degrees past arrival inside a glass stairwell cage open from 300 to 360 degrees.
- **Loose furniture:** placed as `PropMarker`s (`PropSpawner`, [PROPS.md](PROPS.md)), never static boxes. Leave anything animated, breakable, or lit out of `FloorTemplate.NeverMoves`.

## Files and tuning

Under [Assets/_Project/Scripts/Tower/](../../Assets/_Project/Scripts/Tower/):

- [TowerProfile.cs](../../Assets/_Project/Scripts/Tower/TowerProfile.cs): tuning asset at `Assets/_Project/Profiles/DefaultTower.asset`.
- [FloorTemplate.cs](../../Assets/_Project/Scripts/Tower/FloorTemplate.cs): `Kind`, `SpawnPoints`, `NeverMoves`.
- [BreakableDoor.cs](../../Assets/_Project/Scripts/Tower/BreakableDoor.cs): `HitsToBreak` (default 3) on `LockedDoor.prefab`.
- [TowerLayout.cs](../../Assets/_Project/Scripts/Tower/TowerLayout.cs): pure seed-to-layout builder.
- [TowerBuilder.cs](../../Assets/_Project/Scripts/Tower/TowerBuilder.cs): scene builder, static batching, `Rebuild()`, `TryGetSpawn`.
- [TeamSpawner.cs](../../Assets/_Project/Scripts/Tower/TeamSpawner.cs): `NetworkManager` player spawner with `SpawnOverride` and `OnPlayerSpawning`.

Template generation: **Close the Deal > Greybox > Rebuild Floor Templates** (or **Rebuild Floor Templates Only**) in [GreyboxTowerSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxTowerSetup.cs). Edited templates and filled `TowerProfile` slots are never overwritten unless selected and marked with **Let the Tool Rebuild Selected**.

## Known limitations

- Greybox visuals only: doors show no progressive damage before vanishing on break.
- The tower stands 80 m along `+X` from the flat test area (`Spawn In Tower` toggle on `TeamSpawner`).
- Destructible panels and keycards are not implemented in towers yet (loot placement is in [INTEGRATION.md](INTEGRATION.md); the prototype lift is in [ELEVATOR.md](ELEVATOR.md)).
- Without `MatchDirector`, falling out of a window lands in the street; with `MatchDirector` ([MATCH.md](MATCH.md)), players return to their firm's last checkpoint.
- The stairwell glass cage blocks blast rays beyond 3 m inside it, and rival teams can break your door from across the street (only blasts break doors; no gun damage to doors yet).
- Spine 30 / 40 / 30 ratios and stairwell radius are constants in the template builder rather than `TowerProfile` fields, with 3 ordinary office templates currently authored.
