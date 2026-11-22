# CUSTOMISATION

**Status:** Current · **Last updated:** 2026-11-21

## What the player sees

In reception, a glass-walled tailor's corner holds a full-length mirror, a plinth, and a rail of suits. Looking at the mirror and pressing **F** swings the camera out of first person (0.45 s) into a three-quarter view of your character while **Form F-1: Wardrobe amendment** slides in on the left.

- **Left / Right** (arrows or D-pad) changes the focused category's choice immediately with a subtle stretch bob on the character.
- **Up / Down** moves between categories and **Keep outfit** / **Put back** buttons.
- **Q / R, right stick, or right-mouse drag** orbits the camera around the character; focusing a different category eases the camera back to that category's authored angle, distance, and height.
- **Enter** (gamepad A) keeps the outfit; **Backspace** (gamepad B) reverts to what you wore before. Either swings back into first person (0.35 s).
- **Esc** opens the game menu over the fitting room; the fitting room waits underneath until closed.

Other players see you standing at the mirror and only see your new outfit once kept. The ready board displays you as "In the fitting room" and clears ready state so the host cannot start mid-change.

### Shipped categories and unlocks

| Category | Choices | Locked |
|---|---|---|
| Suit (jacket + trousers) | Charcoal, Deep navy, Near black, Silver grey, Corporate cobalt, Paper white | Silver grey (1 run), Corporate cobalt (3 runs), Paper white (5 runs) |
| Shirt | White, Pale blue, Silver grey, Deep navy | Deep navy (2 runs) |
| Belt | Brown leather, Near black, Deep navy, Corporate cobalt | Corporate cobalt (1 run) |

Each choice sets `_BaseColor`, `_ShadowColor`, and `_OutlineColor` on the cel shader ([CHARACTER_SHADER.md](CHARACTER_SHADER.md)). Every suit's line is near-black (#05070F) since 2026-11-21 (Alex: the pale line on the dark suits read as white line art round the arm in first person).

## Ownership and netcode boundary (`AGENTS.md §3`)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Worn outfit | Host (`PlayerOutfit._worn` `SyncVar<ulong>`) | 8 bytes (`OutfitLoadout`, 1 byte per slot) | On spawn and on Keep |
| Outfit request | Client sends 8 bytes (`CmdWear`); host validates catalog IDs | 8 bytes + RPC header | On spawn and on Keep |
| Refusal | Host sends `TargetRefused` on invalid ID | One RPC | Only on invalid request |
| Browsing preview, camera orbit, form | Local to owner | Nothing | Never |
| Saved outfit and `runsStarted` count | Local PC (`outfit.json`) | Nothing | Never |

Meshes and materials never cross the network; every machine reads its own `OutfitCatalog`. Host validation checks catalog ID validity only (`IsValid`), not unlock counts, because `runsStarted` is local cosmetic state.

## Architecture and design rationale

- **Material-name binding (`OutfitView`):** Matches `_materialNames` by prefix (`StartsWith`, handling Unity's `" (Instance)"` suffix) across all `_bodies` renderers and applies colours through one reused `MaterialPropertyBlock`, preserving shared toon materials (`AGENTS.md §4`, `§6`). `ForceVisible` temporarily restores shadow casting and `forceRenderingOff` on the local player's first-person hidden body during fitting.
- **Camera and control handoff (`FittingRoom`):** Calls `LobbyInteractor.Block`, disables `PlayerInputReader` and `PlayerCamera`, and holds the cursor via `GameMenu.HoldCursor(this)`. Run starts increment `runsStarted` on `MatchDirector` `Countdown` entry (when `MatchDirector` is present, so refused desk starts never increment while host menu starts do) and abort any open fitting session cleanly.
- **Async persistence (`OutfitSave`):** Stores choices by `OutfitSlot` and `OutfitOption` asset name in `Application.persistentDataPath/outfit.json` (or `-outfitSave <path>`). Writes enqueue onto `BackgroundFileWriter` ([PROFILE.md](PROFILE.md)), and `Read()` flushes `BackgroundFileWriter` before reading. Unreadable saves move aside as `.corrupt` and fall back to catalog defaults.

## UI contract

- **Read:** `PlayerOutfit.Local` (`Worn`, `RunsStarted`, `Catalog`), `FittingRoom.Instance.IsOpen`, `FittingRoomScreen.Show(catalog, loadout, focusedSlot, runsStarted, gamepad)`.
- **Events:** `FittingRoom` (`OnEntered`, `OnExited(kept)`, `OnSlotFocused`, `OnOptionShown`, `OnLockedRefused`); `PlayerOutfit` (`OnLocalSpawned`, `OnWornChanged`, `OnRefused`, `OnRunsStartedChanged`).
- **Actions:** `PlayerOutfit.Preview`, `Commit`, `Revert` (driven by `FittingRoom`).

## Files and character rig integration

- Scripts under [Assets/_Project/Scripts/Customisation/](../../Assets/_Project/Scripts/Customisation/): `PlayerOutfit.cs`, `OutfitView.cs`, `OutfitLoadout.cs`, `OutfitSave.cs`, `OutfitCatalog.cs`, `OutfitSlot.cs`, `OutfitOption.cs`, `FittingRoom.cs`, `FittingRoomScreen.cs`, `FittingRoomMirror.cs`.
- Assets under `Assets/_Project/Profiles/Customisation/`: `OutfitCatalog.asset`, `Slot_*.asset`, `Options/*.asset`.
- Character setup: `Player.prefab` uses `OutfitView` on `Graphics/Lean/Body` (or `OutfitCharacter.prefab` on greybox players without a rigged model).

## Known limitations

- The reception secretary figure has no gameplay animator (`SecretaryIdle` plays idle breathing only).
- Suit and trousers share one material slot, and hair/shoes are single-material parts, so separate trouser, shoe, and hair categories are not shipped.
- Skin tone is not yet a category (no skin palette in art bible yet).
