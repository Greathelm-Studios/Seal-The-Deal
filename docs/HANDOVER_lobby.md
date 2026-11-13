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

- **Static mesh.** STDMC has no rig: T-pose, slides when moving. Waits on Alex's rig; the outfit attaches to `Graphics/Lean/Body` when it is there (path written, not run).
- **Only three outfit categories** (Suit, Shirt, Belt). Hair and shoes share one material and jacket and trousers share one, so those categories can't ship.
- **No mouse clicks on the fitting form**: the cursor stays captured because a free cursor opens `GameMenu`.
- **`TeamSpawner` ignores firms** (team = client id % 2). The match flow must place players from `RunStartRequested`.
- **The Esc menu still reads CLOSE THE DEAL** (`GreyboxUiSetup`): belongs to the rename pass.
- **Player.prefab is changed by the setup menu and not committed** (it would conflict with Alex's player build): re-run the menu after merge.
- Capture shows the UI washed out because the test render puts the Overlay canvas through post-processing; a real Overlay canvas is not.

## Verified, and how

See the table at the end of this file (filled from the runs).

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
- Firm names are Cobalt and Cyan (proposals, one data asset: `LobbyRules`).
- Secretary lines are proposals (`SecretaryLines`).
- Unlocks: runs started on this PC (Silver suit 1, cobalt 3, white 5; navy shirt 2; cobalt belt 1). Locks are client-honoured only.
- One shared lobby (owner's words) instead of one reception per firm (GDD).
- GDD lists cosmetics after v1 and AGENTS.md §2 still says release 29 Oct: both look out of date next to the owner's request and the date.

## The one thing that still makes it feel like a demo

The people: a T-posed static model with default LiberationSans and no voice, standing in an empty white room. A rigged, animated secretary and players with real voices would change it more than any extra feature.
