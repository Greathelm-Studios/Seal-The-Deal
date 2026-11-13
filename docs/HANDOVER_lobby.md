# Handover: lobby and outfit customisation

**Status:** Target (lane branch `claude/lane-lobby-085700`, not merged) · **Last updated:** 2026-11-13 · **From:** Claude (lobby lane)

Systems docs: [LOBBY.md](systems/LOBBY.md), [CUSTOMISATION.md](systems/CUSTOMISATION.md). Research: [bean_battles_lobby.md](research/bean_battles_lobby.md).

## Goal

Both firms spawn into one shared ground-floor reception, ready up at the desk, pick a firm at a stand, change their outfit at a tailor's mirror, and the host starts the run from the desk.

## Ready for review

- **The reception**, built by `Close the Deal > Lanes > Set Up Lobby` into `Assets/_Project/Scenes/Lanes/Lane_Lobby.unity` from the kit: glass street front, rival tower base, two lifts, cobalt directory with "Seal the Deal", reception desk with a secretary figure, two firm stands (cobalt, cyan), a glass tailor's corner with mirror and suit rail, water cooler, copier, benches.
- **Ready board, prompts and secretary subtitles** (Paper and Hairline uGUI on an Overlay canvas, TextMeshPro, LiberationSans). Chosen uGUI because the project already builds its menu in uGUI and the fitting room needs `Selectable`-free keyboard focus drawn by hand.
- **Firm sign-in with auto-balance**, host-validated; ready board shows firm, ready and "In the fitting room".
- **Fitting room**: camera swing to a three-quarter view, immediate browsing with a small bob, Keep / Put back, locked choices shown with how to unlock, keyboard and pad.
- **Outfit save** (`outfit.json`, versioned, by asset name) and **outfit sync** (8 bytes per player, host validates ids).

## Known issues

- **Alex's rigged STDMC is now in the branch (merged from `buffer`).** The outfit attaches to the player's own `Graphics/Lean/Body` model (split into several meshes; materials are matched by name across all of them) and no second figure is added. Seen in the fitting room: the suit and shirt recolour on the rigged, animated model. The secretary figure is the same model without an animator, so she holds a T-pose.
- **Only three outfit categories** (Suit, Shirt, Belt). Hair and shoes share one material and jacket and trousers share one, so those categories can't ship.
- **No mouse clicks on the fitting form**: the cursor stays captured because a free cursor opens `GameMenu`.
- **`TeamSpawner` ignores firms** (team = client id % 2). The match flow must place players from `RunStartRequested`.
- **The Esc menu still reads CLOSE THE DEAL** (`GreyboxUiSetup`): belongs to the rename pass.
- **Player.prefab is changed by the setup menu and not committed** (it would conflict with Alex's player build): re-run the menu after merge.
- Capture shows the UI washed out because the test render puts the Overlay canvas through post-processing; a real Overlay canvas is not.

## Verified, and how

All runs were headless Editor Play runs of `Lane_Lobby.unity` in local test mode (Steam not running), by scripted drivers kept out of the repo. Key presses injected through the Input System did **not** register in a headless Editor, so those actions were called on the same handlers directly (marked "direct"). Walking was done by moving the host body, not by key input. Nothing was checked in a real window, with a gamepad, or over Steam.

| # | Check | Result | How |
|---|---|---|---|
| 1 | Compile | **0 errors** | `compile-check` skill, 0 `error CS` |
| 2 | Scene built by menu; clip of lobby, ready, firm sign-in, fitting room with camera swing | Built and captured; frame pass done | Headless run, 1280x720 frames at 15 fps, 37 s clip and sheets in the shared notes folder. Ready, sign-in and entering the fitting room were direct calls; browsing and keep/put-back too. Frame pass: no clipping or popping seen; the character glides in a T-pose (static mesh); the camera swing is 0.45 s in, 0.35 s out, so only 4-6 frames at 15 fps. The first pass showed mirrored signs (fixed) |
| 3a | Outfit saved and read back on relaunch | **Works** | Tour kept an outfit; a fresh Editor process loaded `Suit_Nearblack, Shirt_Paleblue, Belt_Nearblack` from the file |
| 3b | Missing, corrupt, newer-version, unknown-name, locked saves | **Default outfit, no error on screen** | Probe loaded each: missing and corrupt (kept as `.corrupt`) and version 99 gave the default; locked and unknown names gave the default; 5 runs unlocked the white suit |
| 4a | Host + one client each see the other's outfit and ready state | **Seen** | Second Editor process from a cloned project; host saw the client's outfit and ready, the client saw the host's |
| 4b | A late joiner sees an outfit set before they joined | **Seen** | Client joined 35 s after the host changed suit and read it at once |
| 4c | Invalid option id refused | **Refused by the host**, simulated | The host's judging method was called with `0xFF00` (a client RPC takes the same path); a real malicious client was not run |
| 5a | Player leaving mid-fitting | **Not run** | Code path: the fitting room aborts when the local body is gone and the roster drops the member |
| 5b | Host starts while someone is customising | **Blocked by rule**, not run | A player in the fitting room is not ready, so the start is refused ("NotAllReady") |
| 5c | Menu opening mid-customisation | **Not run** | The fitting room reads input only while the cursor is captured and waits under the menu |
| 5d | A firm empty at start | **Seen** | `NeedBothFirms` with one player; board says "Each firm needs at least one person" |
| 5e | Steam offline / local test mode | **Seen** | Every run was local test mode. The Steam path is untouched (`SteamLobby`, `GameMenu` unchanged) and not run |
| 6 | Lizard | 2,117 NLOC, avg CC 3.3, **nothing above CC 10** (two functions at 10) | `lizard -l csharp Scripts/Lobby Scripts/Customisation`; four CC 11+ functions refactored |
| 7 | Run start | **Seen** | `RunStartRequested` listed both players with firms 0 and 1; both machines counted one run started |

## Lane-local inputs (for the movement lane to consolidate)

Defined in `Scripts/Lobby/LobbyInput.cs`:

| Action | Keyboard | Pad |
|---|---|---|
| Interact | F | North |
| Confirm (keep outfit) | Enter, Space | South |
| Cancel (put back) | Backspace | East |
| Next / previous option | D / A, arrows | D-pad right / left |
| Next / previous category | S / W, arrows | D-pad down / up |
| Rotate character | Q / R, mouse X | Right stick X |

Fitting-room keys only matter while the player's own input is disabled.

## Hooks for other lanes

- `LobbyRoster.RunStartRequested(IReadOnlyList<RunEntrant>)` (host): client id, firm 0/1, name. `LobbyRoster.ToFirmMap(list)` gives `Dictionary<int clientId, int firm>`, the shape of `MatchDirector.HostStartRun`. Firm 0 = Building A, 1 = Building B.
- `LobbyRoster.Instance.ServerReturnToLobby()` when a run ends.
- `Secretary.LineSpoken(moment, text, seconds)` for voice clips.

## Merge and menu re-run steps

1. Merge the lane branch. Expect add/add on `Assets/_Project/Editor/Lanes.meta` and `Assets/_Project/Scenes/Lanes.meta` (keep either).
2. `Assets/DefaultPrefabObjects.asset` is rewritten by FishNet on first open; do not hand-merge.
3. Run `Close the Deal > Lanes > Set Up Lobby` once. It adds `PlayerOutfit` and the outfit figure (or `OutfitView` on an existing `Graphics/Lean/Body` model) to `Player.prefab`, never rebuilds Graphics, rebuilds `Lane_Lobby.unity`.
4. Run `Close the Deal > Lanes > Set Up Match` (game-mode lane) after it, then subscribe `HostStartRun` to `RunStartRequested` (not done: no match code here).

## What waits on Alex's art

| Slot | He needs to deliver |
|---|---|
| Trousers | Trousers as their own material (today one with the jacket), named e.g. `STDMC_trousers_Toon`; update `Slot_*.asset` names |
| Shoes | Shoes as their own material (today one with hair) |
| Hair | Hair as its own material, plus separate meshes if hair styles should swap (child per style, pivot at the head, same scale as the body) |
| Eyewear, accessory (tie clip, ID card, earpiece) | Separate meshes parented to a head or chest bone, pivots at the attachment point, toon material slot, naming `Eyewear_<name>`, `Accessory_<name>`. Needs a small `OutfitView` addition (show one child per choice) |
| Tie | A tie material of its own (today part of the shirt) |

## Assumptions for the owner to confirm

- Start rule: everyone ready, nobody in the fitting room, at least one per firm, firms within one player.
- Newcomers join the smaller firm; switching firm or entering the fitting room takes your ready back.
- Hold the desk key 1 s to start (host).
- Firm names and colours are not held by the lobby: `LobbyRules` points at the game mode's two `FirmDefinition` assets (`Profiles/Match`), index 0 = Building A. Today they read "Ashcombe & Pryce" and "Halloran Voss" (the game mode's proposals; the owner decides).
- Secretary lines are proposals (`SecretaryLines`).
- Unlocks: runs started on this PC (Silver suit 1, cobalt 3, white 5; navy shirt 2; cobalt belt 1). Locks are client-honoured only.
- One shared lobby (owner's words) instead of one reception per firm (GDD).
- GDD lists cosmetics after v1 and AGENTS.md §2 still says release 29 Oct: both look out of date next to the owner's request and the date.

## The one thing that still makes it feel like a demo

The people: the secretary holds a T-pose behind the desk, nobody has a voice, and the type is the default LiberationSans in an empty white room. An animated secretary and real voices would change it more than any extra feature.
