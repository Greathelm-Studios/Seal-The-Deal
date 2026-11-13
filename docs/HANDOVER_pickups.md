# Handover: pickups, furniture and silly weapons lane

**Status:** Target · **Last updated:** 2026-11-13 · **From:** Adam / Claude Code (pickups lane session)

## Goal

Look at a chair, press F, carry it, throw it at someone. Carry a cubicle panel as cover. Arm and throw a Grenade Case that beeps and clears the room. Pull the pin on a fire extinguisher for a smoke wall that then rockets around the floor. Put down a wet floor sign that sends runners flying. The host decides all of it.

## Where it stands

Ready for review, not approved. Built on branch `claude/lane-pickups-bf6f0c` (cut from `lanes-base`):

- Runtime: `Assets/_Project/Scripts/Items/` (new), `Scripts/Core/IDamageable.cs` (exact shared text, guid `7c1d0e5a…`), `Scripts/Props/Prop.cs` (gains `SetHeldStill`, `Body`, and `Throw` is ignored while held).
- Tuning: `Assets/_Project/Profiles/Items/` (item definitions, carry rules, the three gadget profiles, the Grenade Case blast).
- Prefabs: `Assets/_Project/Prefabs/Items/` (cubicle panel, pistol, AR-15, Grenade Case, fire extinguisher, wet floor sign, practice dummy, materials).
- Generated changes outside the lane folders, in their own commit so the merger can drop them and re-run the menu: `Pickup` on the five greybox props, `ItemCarrier` on `Player.prefab`, FishNet's `DefaultPrefabObjects.asset`.
- Editor: `Assets/_Project/Editor/Lanes/PickupsLaneSetup.cs` (setup menus and the scripted check run). Scene: `Assets/_Project/Scenes/Lanes/Lane_Pickups.unity`.
- Docs: new [docs/systems/ITEMS.md](systems/ITEMS.md); [docs/systems/PROPS.md](systems/PROPS.md) updated.

The three office weapons proposed were fire extinguisher, wet floor sign and desk fan; the first two are built. The fan was dropped: a wind cone is the weakest read at distance.

## What was checked

- `node ~/.claude/skills/compile-check/compile-check.mjs`: PASS, 0 errors, 0 warnings.
- `lizard -l csharp Assets/_Project/Scripts/Items Assets/_Project/Scripts/Props`: highest CC 10 (`ItemCarrier.Update`, `Pickup.OnCollisionEnter`); nothing at 11+. Two functions at 11–12 were split.
- Scripted play run, **host only** (one copy of the game hosting with its own player): **Close the Deal > Lanes > Run Pickups Check**, or headless `-executeMethod CloseTheDeal.Editor.Lanes.PickupsLaneRun.RunFromShell -pickupsRole solo -pickupsOut <folder>`. Results are filled in under "Run results" below.
- The script calls the same owner-side methods the buttons call (`LocalTake`, `LocalThrow`, `LocalDrop`, `LocalUse`), so every request goes through the host. **The F, left mouse, G and right mouse bindings themselves were not pressed by anything**; that needs Adam's hands. Sprinting onto the wet floor is faked by setting the body's speed.

### Run results

Latest runs (2026-11-13, after merging main and moving every item onto Switchboard's `NetBody`):

- **Solo (host only): 33 passed, 0 failed.** The earlier wet-floor re-pickup failure was the script, not the game: the slip knocks the sign about 1 m, which clears the trap as designed, and the script was standing at its old spot. It now walks to where the sign is. The allocation check passed this run; it has been flaky before.
- Not checked in batchmode: the look prompt (the mouse cannot be captured) and walking speed under load by keys (profile numbers checked instead: chair 5.48 of 6.00 m/s).
- **Host + client, two copies on one Mac over the local connection: host 4/4, client 8/8.** Contested grab gave the chair to exactly one player; the client saw the host's case arm, beep 13 times and explode, and its own player was thrown; a client leaving while carrying left the chair in the level once; a thrown chair came to rest at the same point on both through Switchboard (-1.80, 12.25, -5.78). No `[Switchboard]` warnings in either log.
- In this run the client won the contested grab, so "the loser is told why" was only seen from the host's side.
- **Lane scene streaming:** `Lane_Pickups.unity` now holds a real `TowerBuilder` with an empty-floor profile (`Profiles/Items/LaneTowerFrame.asset`, `Prefabs/Items/LaneEmptyFloor.prefab`), because Switchboard only streams once a tower is built and only inside its box. Log: "Built two towers of 15 floors"; nothing is drawn.
- Prop count stayed at 15 of the cap of 16 throughout.

## Lane-local inputs (for the movement lane to move into the shared actions asset)

Defined in `ItemCarrier.CreateInput`:

| Action | Keyboard / mouse | Pad |
|---|---|---|
| Interact | F (E is lean right) | West button |
| Throw | Left mouse | Right trigger |
| Drop | G | D-pad down |
| Use Item | Right mouse | Left trigger |

While the hands are full, `ItemCarrier` disables the shared `Player/Attack` action for the owner, so throwing never also fires.

## Merge and menu re-run steps

1. Merge the lane commit. `Scripts/Core.meta` will conflict add/add with the shooting lane (different folder guid, same content otherwise); keep either. `IDamageable.cs` and its `.meta` are identical in both lanes.
2. Drop the generated-prefabs commit if the prefabs conflict, never hand-merge their YAML. Let FishNet regenerate `DefaultPrefabObjects.asset`.
3. Open Unity on the merged checkout and run **Close the Deal > Lanes > Set Up Pickups (prefabs only)**. It adds `Pickup` to the greybox props and `ItemCarrier` to `Player.prefab` if they are missing and changes nothing else. Re-run it after any **Greybox > Rebuild Everything**.
4. To rebuild the test scene: **Close the Deal > Lanes > Set Up Pickups** (asks before rebuilding an existing `Lane_Pickups.unity`).
5. One-line hookups other lanes own:
   - Shooting lane: subscribe to `ItemCarrier.ServerItemTaken`, grant guns where `pickup.Definition.Kind == ItemKind.Gun`, then `pickup.ServerUseUp()` (snippet in ITEMS.md).
   - Shooting lane: call `ItemCarrier.ServerForceDrop()` on the host when a player dies.
   - Movement lane: give `PlayerMotor` a public profile or speed-multiplier override and point `MotorSpeedOverride` at it (see risks).

## How Adam tests it by hand

Open `Assets/_Project/Scenes/Lanes/Lane_Pickups.unity`, press Play, then **Host game**.

1. Walk to the back desks. Look at a chair: **"F · Pick up Chair"** under the crosshair. F: it is in front of you and you walk a bit slower.
2. Left mouse at the dummy on the left: it shoves, and the dummy's green bar shortens.
3. Pick up a cubicle panel and walk: noticeably slower, and it stays solid in front of you.
4. Middle desk: pick up a red Grenade Case. Right mouse: it beeps, faster and faster. Throw it at the chairs: it explodes and they scatter. Pick up the second one, arm it and keep holding it: it goes off in your hands and throws you.
5. Right wall: fire extinguisher. Right mouse: smoke pours out. G to drop it: it skitters round the floor spinning, then runs empty.
6. Left wall: wet floor sign. Right mouse puts it down with a blue puddle. Walk through: fine. Sprint through: you go flying.
7. Throw a chair out of the window: two seconds later it is back where it started.
8. Look at a desk: "Desk · Too heavy to lift".

## Open questions and risks

- **One drop key (G) for both the briefcase and an item.** Rule proposed for integration: a player holds either the objective briefcase or one item, never both; picking up the briefcase drops the item first (host side), so G always means the one thing in the hands.
- Review findings left open: the carry slowdown is applied when each machine learns of the pickup rather than on a tick, so heavy pickups cause a small correction; a late joiner can show a held item as loose if the item spawns before its carrier.

- **Owner words vs GDD:** the GDD's v1 weapon table lists "throwable explosive" but no furniture carrying, gun pickups or office gadgets. Built to the owner's 2026-11-12 words; GDD not edited (only Adam changes design).
- **Carry slowdown uses reflection** on `PlayerMotor._profile` (labelled workaround, ITEMS.md known limitations). If the movement lane renames that field, carrying stops slowing players and logs one error.
- **Gun pickups and death:** gun definitions are set to never return (`ReturnsAfterUsedSeconds = 0`), so the snippet's `ServerUseUp()` despawns the gun pickup for good once taken. If the weapon holder does not drop a fresh pickup when a player dies, guns are lost on death, which breaks "never lost". The shooting lane picks one at integration: drop a pickup on death, or give the gun definitions a return time.
- **Setup reaches FishNet's scene id call by name** (`NetworkObject.CreateSceneId` is internal). Without it networked items placed in a freshly built scene never spawn. If a FishNet update renames it, the tool logs an error and the fix is FishNet's own Reserialize NetworkObjects menu.
- Running the setup menu also re-applies the office look, which rewrites `Assets/_Project/Art/Look/OfficeLook_Volume.asset` and its `.meta`. Those files were left uncommitted in this worktree; they are not lane changes.
- A carried panel is an immovable wall on the host and pushes other players aside. Fun or exploit needs a playtest.

## The one thing that still makes pickups feel like a demo

Carried items float in front of the camera with no hands or arms, and the beep, hiss and boom are generated tones, so picking something up has no physical weight on screen.

## Next steps

Adam plays the hand-test list above; the next agent fixes what he reports, then the integrator does the merge steps.
