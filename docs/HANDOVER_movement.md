# Handover: movement lane

**Status:** Ready for review (not approved) · **Last updated:** 2026-11-13 · **From:** movement lane session, branch `claude/lane-movement-864d86`

## Goal

Fast, high-mobility arena movement (the owner's STRAFTAT target replaces the GDD's "chaotic, not precise"; the GDD still says the old thing): momentum, air strafing, held hops, slide, vault and climb that keep speed, on the predicted FishNet body, with the inputs the other lanes need and a read-only animation state for the rig. Owning doc: [docs/systems/PLAYER_MOVEMENT.md](systems/PLAYER_MOVEMENT.md).

## Where it stands

Ready for review (never "done" until the owner has played it):
- Momentum model (floor grip and acceleration, air strafing capped by `Air Strafe Speed`, hold-to-hop, `Max Speed`), crouch, slide with boost and slope pull, climb and vault (`LedgeProbe`) with a "too high / no room" refusal, stair and ramp snapping, look limits ±89° and separate mouse and stick speeds (stick 240°/s across, 150°/s up and down, measured), pad Start opens the menu, Enter no longer fires.
- Input: all shared actions on `PlayerInputReader` behind the captured gate; binding table is in the system doc. Interact is on **F** (E is lean), as the orchestrator confirmed.
- Read-only `PlayerMotor.AnimState` and events (`Jumped`, `Landed`, `MantleStarted`, `MantleRefused`, `CrouchChanged`, `SprintChanged`, `SlideChanged`, `KnockedDown`). `Grounded`, `State`, `Profile.WalkSpeed/SprintSpeed` are kept as they were for Alex's animator (`Grounded` still means feet on the floor; the new `AnimState.Grounded` means the move state and is never true while rising).
- A real bug fixed on the way: all `PlayerInputReader`s shared one actions asset, so a remote body spawning switched off the local player's input; the host lost all control the moment a client joined. Each reader now copies the asset.
- Scene `Assets/_Project/Scenes/Lanes/Lane_Movement.unity` and menu `Close the Deal > Lanes > Set Up Movement`; scripted play session `Scripts/Player/Dev/PlayerMovementSession.cs` (editor and development builds only).

## What was checked

All from scripted sessions that play through made-up keyboard, mouse and pad devices; evidence is in [docs/handover/movement/](handover/movement/) (frames, logs, the session CSV, and one speed chart, which is a chart drawn from the logged speeds, not a capture of a HUD: no HUD readout exists).
1. **Compile:** `compile-check` 0 errors on the final tree.
2. **Solo session, Lane_Movement, 52 checks, 0 failures** (`solo_session.log`): walk 6 m/s in 0.11 s; sprint 9; stop from sprint 0.35 s and 1.1 m; tap jump 0.66 m, hold 1.60 m; sprint jump 6.8 m held forward, 1.7 m pulling back; three chained hops hold exactly 9.00 m/s; air strafing 9 to 15.3 m/s through three hops; crouch (eye 1.6 to 0.9 m, fits a 1.3 m gap, stays down under a low roof); slide 9 to 11.0 m/s boost, 17.8 m, jump out keeps speed; slide down the 20° ramp 9.3 to 10.4 m/s; desk climb 0.29 s; 0.6 m sill vaulted in 0.48 s at 8.5 m/s out; 1.6 m ledge 0.41 s; a climb keeps 8.1 of 9.0 m/s; 2.4 m wall refused (`TooHigh`) and jumped instead; spiral stairs up 1.85 s (6.1 m/s slowest) and down 1.46 s (10.5 m/s peak), never airborne.
3. **Look:** straight up and straight down both reach ±89.0°; stick 240°/s across at full tilt (was about 7), 50°/s at half tilt, 150°/s up and down.
4. **Host + client at about 100 ms (ping 97 to 117 ms), loopback only:** client snap 0 corrections over 1 cm in most route sections, worst 2.0 cm on the stairs, control blast 199.5 cm (so the measure works); top speed seen 13.3 m/s. Details and the retracted early figures are in the system doc.
5. **Edge cases:** too-high ledge refused cleanly; menu opened mid-jump leaves the player idle with Space still held, and nothing fires late; a blast mid-climb takes over at once and the player recovers and walks; the test blast and knockback still work through `Knockback.cs` (unchanged); leaving mid-sprint removes the player and a second spawn is idle with a camera.
6. **Lizard:** no function in `Scripts/Player` or `Editor/Lanes` is above CC 10 after the last change (the highest are `PlayerMovementSession.Host` and `PlayerMotor.RaiseChanges`, both 10); `PlayerMovementSession.Start` was 12 and was split. Run `lizard -l csharp Assets/_Project/Scripts/Player` to recheck.

## Known issues (not fixed)

- **Other players' crouch, lean and slide do not reliably reach a client** (client saw the host crouch to 0.13, lean to 0.20, never slide; jumps, landings, sprint and climbs did arrive). Cause not found. Any rig or the lean tilt on remote bodies depends on it. First thing to chase.
- Three steps of the scripted client route fail (air strafing from a walked-to start, the desk climb, the stair slide-and-jump): the client cannot teleport, so its starting positions differ from the solo run and it hit things. Script positioning, not netcode as far as seen. The host+client run as a whole is therefore "FAIL" in its log; it is the snap numbers above that are reported, not a pass.
- **Sprint speed drops to 6 m/s on the spiral stairs going up** (tight turn, ground acceleration 70). Not retuned.
- **A slide down the spiral stairs leaves the treads** (drifted 4.3 m from the column; treads end at 2.6 m). Left as a level rule in the doc.
- One frame in the host's view of the client stepped 8.5 times its speed; not explained.
- Bandwidth at 8 players is about 3.1 Mbit/s up from the host before packing (reconcile now about 115 B): too heavy for many home lines.
- The committed floor templates (`Floor_Lobby.prefab`: 20 × 10 m, 1 m sill, straight ramps) do not match TOWER.md or the template tool (28 × 14 m, 0.6 m sill, spiral); the doc and tool look right, the prefabs look stale. The lane scene builds its own slice to TOWER.md's numbers.

## Things that differ from your brief (decide)

- **Feel:** STRAFTAT target applied; the GDD still says "chaotic, not precise".
- **Hold to hop** (`Hold To Hop` on the profile, default on) instead of timed hops: holding Space re-hops with no speed loss.
- **Values I changed in `DefaultMovement.asset`** (designer-owned, so confirm): Ground Acceleration 45 to 70; Ground Braking removed, replaced by Ground Friction 6 and Stop Speed 3; Air Acceleration 20 to 30; Mantle Speed Keep 0.9; plus new values (Max Speed 24, Slide Min Speed 7, Slide Boost 2.5 m/s, cooldown 1.5 s, Slide Friction 0.45, Slope Gain 2, End Speed 3.5, Crouch 3 m/s and 1.1 m, Air Strafe Speed 1.5, Step Down Snap 0.35, look speeds, landing dip, speed FOV).
- **Release risk (for WORKBOARD):** at sprint 9 m/s, 11 to 13 m/s sliding and up to 15+ air strafing, a 28 m floor is crossed in 2 to 3 s and a 4.5 m flight climbed in 1.9 s; the GDD's 40 s per floor and its 30 s lobby-to-roof sprint estimate no longer hold without the locked-door gate.
- **Edits outside the lane folders:** `GameMenu.cs` (pad Start), `GreyboxUiSetup.cs` (controls label), because the brief asked for the menu button and the controls list. The label's change only applies after `Rebuild Game UI`.

## Lane-local inputs for consolidation

None: the shared asset already holds every action the other lanes asked for (Attack, AltFire, Interact F, Throw G, Reload R, slots 1 to 4, pad equivalents). Other lanes' temporary local actions can be dropped and replaced with `PlayerInputReader.Consume(PlayerPress.X)` and `ConsumeWeaponSlot()`.

## Merge and re-run steps

1. Merge `claude/lane-movement-864d86`. Expect conflicts in `docs/systems/PLAYER_MOVEMENT.md` (Alex also edited it; mine is a full rewrite, keep both sets of facts) and possibly `Player.prefab` (this lane does **not** edit it: new serialized fields default in code, and `MovementLaneSetup` only adds the slice and course to its own scene and calls the shared play-rig setup; it never touches the player's Graphics hierarchy).
2. Let Unity import; `Assets/_Project/Profiles/DefaultMovement.asset` and `InputSystem_Actions.inputactions` are edited in place (GUIDs unchanged).
3. Run `Close the Deal > Lanes > Set Up Movement` to (re)build the lane scene; `Rebuild Game UI` for the new controls label.
4. Check: open `Lane_Movement.unity`, Play, Host game. WASD, Shift, Space (hold to hop), Ctrl (slide at a run), Q/E, mouse.

## One thing that still makes movement feel like a demo

Other players' bodies do not crouch, lean or slide (no animation, and the values do not yet reach clients), so the speed you feel is not matched by anything you see on anyone else.

## Next steps

Find why remote crouch, lean and slide stay near zero on clients; then fix the client route's starting positions and rerun the pair; then tune ground acceleration for the stairs.
