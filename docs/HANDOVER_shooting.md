# Handover: shooting and breakable glass lane

**Status:** Target · **Last updated:** 2026-11-12 · **From:** Adam's Claude session (shooting lane, branch `claude/lane-shooting-e0f93d`)

## Goal

Two guns that feel tight and fair at high movement speed, a host that decides every hit, players who die and come back on the same floor after 5 s, and window glass that shatters when shot, blasted or crashed through and stays broken for the match on every machine, late joiners included.

## Where it stands

Built (all under this lane's folders; design and ownership in [WEAPONS.md](systems/WEAPONS.md) and [GLASS.md](systems/GLASS.md)):

- `WeaponDefinition` per gun (Pistol, AR-15), a `WeaponCatalog`, and `WeaponHolder` on the player with `Equip(definition)` / `Equip(definition, slot)` / `Unequip()` / `Unequip(slot)` for the pickups lane.
- Host-side hit decision with favour-the-shooter rewind (capped 0.25 s), owner-side instant feedback, learnable recoil patterns, accuracy multipliers for moving and airborne.
- `Health` on players and dummies (`IDamageable`), death, 5 s respawn on the floor, `Damaged` / `Died` / `Respawned` events, synced health and last attacker.
- Breakable glass: `GlassPane`, the host-owned `GlassRegistry` with deterministic pane numbers, local pooled shards.
- `Knockback` extended: optional blast damage, attacker id, glass breaking.
- `DoorDamage`: bullets wear down the locked door.
- Test scene `Assets/_Project/Scenes/Lanes/Lane_Shooting.unity`, built by **Close the Deal > Lanes > Set Up Shooting**.

The commits and what was seen running are listed in the report that ends the session; this file is updated with each slice.

## What was checked

- `node ~/.claude/skills/compile-check/compile-check.mjs`: PASS, 0 errors (runtime and editor scripts).
- Headless Unity 6000.4.8f1 on this worktree: `-executeMethod CloseTheDeal.Editor.Lanes.ShootingLaneSetup.SetUpFromCommandLine` (0 `error CS`, every scene NetworkObject has an id), then the self-check as host (`SelfCheckFromCommandLine -laneRole host -laneWithClient -local`) with a second editor on an APFS clone of the worktree joining as client (`-laneRole client -laneLatency 50`, measured round trip 70–87 ms). Logs and pictures stay in the session scratchpad, not the repo.
- **Passed on the host:** both guns on spawn; 9 panes numbered; a pistol shot breaks both panes on its path and the host confirms the hit (34 damage); three hits kill a dummy and a kill confirmation arrives; a broken pane ignores further shots; AR-15 fires 15–16 shots in 1.5 s; empty magazine dry-fires; a reload cut off by a switch gives no ammo; a full reload fills once; dead players can't fire, reload or switch and the reload drops; respawn after 5.0 s on the same floor with full magazines; the AR-15 recoil pattern is identical over two magazines (0.000° apart); the fire button does nothing while the menu has the mouse (with a check that the click reached the Input System); a blast breaks glass in its radius; a thrown 8 kg chair breaks the glass-room wall and carries on through; 0 B allocated per shot over 30 shots (main thread, editor).
- **Passed from the client:** joining after breaks shows the same 5 panes broken with no shards; the client's own shot shows at once (tracer, ammo 11) before the host answers; the host confirms the client's hit and the damage shows on the client; the panes the client's shot crossed break on the client; 0 B per shot; the host kills the client's player and the body goes out through the window, breaks it and falls to the street; the client hears its own death and respawn and comes back whole on its floor; after leaving, its panes are whole; a client that rejoins sees every pane the match broke (8) and the dummies exactly as the host has them.
- **Hits on fast targets (feel pass run):** pistol shots at dummies running across the far roof at 9, 18 and 24 m/s (sprint, twice sprint, the movement lane's top speed): **10/10 at every speed from the host, and 10/10 at every speed from a client on a 70–80 ms round trip**, all confirmed by the host. Fixed by sending the host tick the shooter's screen showed and testing one tick either side (measured: a client's picture sat 1–2 ticks off the tick it reported).
- **Feel pass, seen in the captures:** the pistol shot shows a small varied flash whose light warms the window frame for a frame, a tracer to the true end point, both panes bursting into shards, sparks at the hit and the dummy flashing and rocking away; the AR-15 burst shows the gun kicked and the view climbing. Every check above still passes after the pass (host 25/25, client 13/13).
- **Not checked:** the real mouse button with the menu closed (the cursor doesn't lock headless, so shots in the run go through `WeaponHolder.PullTrigger`); a build; Steam; more than one client; anything by hand.

## Assumptions made while Adam was away (2026-11-13 night)

- Head hits exist (top 0.3 m of the body) and are reported to the shooter as precision hits, but **do the same damage as the body** (`Head Multiplier` 1 on both guns) until Adam decides whether head shots should matter.
- No kill hit-stop: slowing time would slow the shared networked world. No aim assist: a mouse game for now.
- The feel numbers (snap, shake, FOV punch, gun kick) are first-pass values in the shape NorseFPS uses, not tuned by hand.

## Open questions and risks

- **Feel target, for Adam to confirm** (the orchestrator's reading of "the movement needs to feel like STRAFTAT and high mobility good gun play"): first shot on the crosshair; recoil a fixed, learnable pattern rather than random bloom; running and jumping barely cost accuracy; fast time-to-kill (pistol 3 hits, AR-15 5 hits); instant feedback with a host-confirmed hit and a separate kill confirm; quick draw (0.2 / 0.3 s) and reload (1.1 / 1.7 s). Numbers are starting points on the gun assets.
- **GDD mismatch:** [GDD.md](GDD.md) still says combat "should feel chaotic rather than precise" and calls the rifle the reliable gun. Built to Adam's newer words; the GDD needs his edit (not changed here).
- **Owner decisions newer than the GDD** this lane follows: teams are rival firms; window glass breaks (the GDD and TOWER.md say there is no glass in the bays).
- **Workaround:** PlayerMotor's left-click test blast shares the fire button, so Set Up Shooting points the motor at `Profiles/Weapons/NoTestBlast.asset` (a blast that does nothing) and clears its two blast masks. The real fix is in the movement lane: take the test blast out of `PlayerMotor`. Then delete that asset.
- **Muzzle light cost unmeasured:** every shot lights a real-time point light for 0.03–0.04 s; eight players on full auto may hit URP's per-object light limit. Profile in a 4v4 build; if it costs, keep the light on the shooter's own gun only.
- **Release risk carried:** favour-the-shooter means a target can be hit up to 0.25 s after reaching cover on a laggy shooter's screen. That is the price of fair hits on fast targets.

## Lane-local inputs (for the shared actions asset)

Defined in code in `WeaponHolder.WeaponInput`:

| Action | Binding |
|---|---|
| Fire | `<Mouse>/leftButton` |
| Reload | `<Keyboard>/r` |
| Weapon Slot 1 | `<Keyboard>/1` |
| Weapon Slot 2 | `<Keyboard>/2` |

Fire shares left mouse with the shared `Player/Attack` action (the test blast) and with the pickups lane's Throw.

## Merge steps

1. Merge the lane branch. Expect an add/add conflict on `Assets/_Project/Scripts/Core.meta` with the pickups lane (two folder guids); keep either. `IDamageable.cs` and its `.meta` are identical in both lanes.
2. Open Unity and let it import.
3. Run **Close the Deal > Lanes > Set Up Shooting**. It writes any missing lane assets (never over edited ones), adds `Health`, `WeaponHolder` and `WeaponView` to `Player.prefab`, and asks before rebuilding the test scene. Re-run it after any other lane's tool rewrites `Player.prefab`, and once `LockedDoor.prefab` exists (it adds `DoorDamage` then).
4. In any other scene that should have guns and breaking glass (Greybox, KitOffice), run **Close the Deal > Lanes > Add Shooting To Open Scene**, review, save.
5. Pickups lane hooks (not compiled against here): on the host, subscribe to `ItemCarrier.ServerItemTaken(carrier, pickup)`; for `ItemKind.Gun` call `WeaponHolder.Equip(gun definition)` and `pickup.ServerUseUp()`. On `Health.Died` (host) call `ItemCarrier.ServerForceDrop()`. The pickups lane's `Item_Pistol` / `Item_AR15` need a reference to this lane's `Pistol.asset` / `AR15.asset`.
6. Game-mode lane: `Health.OverrideNextRespawn(position)` for window exits to the checkpoint; `Health.AnyDied` carries the attacker.

## Next steps

Play `Lane_Shooting.unity` by hand with a second copy joined, and tune the gun assets by feel.
