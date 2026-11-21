# CUSTOMISATION

**Status:** Current (lane branch `claude/lane-lobby-085700`, not merged) · **Last updated:** 2026-11-12

## What the player sees

In reception, a glass-walled tailor's corner holds a full-length mirror, a plinth and a rail of suits.
Looking at the mirror and pressing **F** swings the camera out of first person (0.45 s, eased) into a three-quarter view of your own character, and a white form slides in on the left: **Form F-1 · Wardrobe amendment**, with one row per outfit category.

- **Left / Right** (arrows or D-pad; the kit form's own navigation) changes the focused category's choice; the character changes at once and gives a small tailor's bob, and the choice name kicks sideways.
- **Up / Down** moves between categories and down to **Keep outfit** and **Put back** (kit buttons); the focused row has the kit's highlight, cobalt rule and focus frame.
- **Mouse, Q / R or the right stick** walks the view round the character.
- **Enter** (pad A) keeps the outfit; **Backspace** (pad B) puts back what you were wearing. Either swings the camera back into first person (0.35 s).
- The first-person arms and gameplay HUD stay hidden while the camera shows the outfit, including the swing back to first person. Esc opens the game menu over the fitting room, which resumes when the menu closes.

Other players see you standing at the mirror the whole time, and your outfit changes for them only when you keep it.
The ready board shows you as "In the fitting room", which takes back your ready, so the host can't start the run while anyone is still choosing.

### Categories and choices shipped today

Only parts that exist as their own material on the STDMC character can be a category (art bible §07: swap clear graphic categories, keep the silhouette).

| Category | Choices (bible palette) | Locked |
|---|---|---|
| Suit (jacket and trousers together) | Charcoal (the model's own), Deep navy, Near black, Silver grey, Corporate cobalt, Paper white | Silver grey after 1 run, Corporate cobalt after 3, Paper white after 5 |
| Shirt | White (the model's own), Pale blue, Silver grey, Deep navy | Deep navy after 2 runs |
| Belt | Brown leather (the model's own), Near black, Deep navy, Corporate cobalt | Corporate cobalt after 1 run |

Every choice sets the part's lit colour, a colder shadow colour (bible §05) and a contour of the opposite value (CHARACTER_SHADER.md).

### Locks and the second session

A locked choice can still be browsed, so you see what you're working toward; the form says "Locked. Opens after you have started 3 runs. You have started 1." Keeping an outfit with a locked choice in it is refused with a notice.
The count is **runs started on this PC**: it goes up by one on every player's machine when the host starts a run from the desk. That is the only progress data that exists today; when the match flow records finished runs or wins, the rule can move to those.

## Saving

`Application.persistentDataPath/outfit.json` (or the path after `-outfitSave` on the command line, which tests use).
It holds a version number, the runs-started count and each category's choice **by asset name**, so reordering or adding choices doesn't scramble a save.
A missing file starts the default outfit; an unreadable one is set aside as `outfit.json.corrupt` and the default is used; an unknown version or a choice that no longer exists falls back to that category's default.
Nothing is shown on screen in any of those cases; a warning goes to the log.
Writes go to a temporary file first and then replace the save, so a crash mid-write can't corrupt it.

## Who owns what

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Worn outfit | **Host** (`PlayerOutfit` on Player.prefab, a SyncVar) | **8 bytes per player** (one byte per category, up to 8 categories) | Once on spawn, then only when someone keeps an outfit |
| Outfit request | The owner sends the 8 bytes; the host checks every id against the catalog and keeps or refuses it | 8 bytes + RPC header | On spawn and on Keep |
| Refusal | Host → that player | One RPC | Only on a bad request |
| Browsing preview, camera, form | The owner's machine only | Nothing | — |
| Saved outfit and runs count | The player's own PC | Nothing | — |

Meshes and materials never cross the network; every machine draws the ids with its own copy of the catalog.
Late joiners get everyone's outfit with each player's spawn.
**The host does not check locks**: the runs count lives on the player's own PC, so locks are honoured by the client only. Outfits are cosmetic, so a player who edits their save gains only a colour.

## Data

- `Assets/_Project/Profiles/Customisation/OutfitCatalog.asset`: the categories in order (`OutfitCatalog`).
- `Slot_*.asset`: one per category: its name, the **names** of the body materials it recolours (not positions, so a rigged or re-exported model with the same materials works), its choices (`OutfitSlot`).
- `Options/*.asset`: one per choice: name, colours, runs to unlock (`OutfitOption`). The save stores these asset names, so renaming one resets that choice in old saves to the default.
- The set-up tool writes these only when they are missing; edit them freely.

## Files

- Scripts: [Assets/_Project/Scripts/Customisation/](../../Assets/_Project/Scripts/Customisation/) (`PlayerOutfit`, `OutfitView`, `OutfitLoadout`, `OutfitSave`, `OutfitCatalog`, `OutfitSlot`, `OutfitOption`, `FittingRoom`, `FittingRoomScreen`, `FittingRoomMirror`).
- Prefab: `Assets/_Project/Prefabs/Lobby/OutfitCharacter.prefab` (STDMC fitted to 1.8 m, feet at 0, facing +Z, under a `Fit` pivot the bob scales). On `Player.prefab` the outfit lives on the real character at `Graphics/Lean/Body` (an `OutfitView` there, `PlayerOutfit` > View pointing at it); **Set Up Lobby** only stands this figure under `Graphics/Lean` at y −0.9, hiding the capsule and nose, on a greybox player that has no model yet.

## The rigged character (now in the branch)

- `OutfitCharacter.prefab` (the secretary, and the greybox player) is rebuilt from `STDMC_Toon` each time **Set Up Lobby** runs, measured from all its renderers together, so a changed model needs only a re-run.
- Each category points at **materials by name** (`STDMC_lambert4_Toon` and so on), found on the body's renderers at run time (the rigged model is split into several meshes; `OutfitView` lists them all). A rigged model that keeps those material names needs nothing; a new naming needs `Slot_*.asset` updated (render each material highlighted, as was done for STDMC, to see which is which).
- If `Player.prefab` already has a real character at `Graphics/Lean/Body` (as Alex's player build does), **Set Up Lobby** puts `OutfitView` on that object and adds no second figure and hides nothing. **Seen** on the merged player build (recolouring suit and shirt on the animated model in the fitting room).
- Parts that are separate meshes (hair, eyewear) become show/hide categories: that needs one small addition to `OutfitView` (enable one child per choice), not done here because no such parts exist yet.
- What the owner's categories need from the art: see [HANDOVER_lobby.md](../HANDOVER_lobby.md).

## UI contract

State and events a screen reads. The outfit itself is decided by the host (`PlayerOutfit`); everything below is local to the player browsing.

### State (read)

| Read | Type | Meaning |
|---|---|---|
| `PlayerOutfit.Local` | `PlayerOutfit`, null until the local player has spawned | The player's outfit |
| `Worn` | `OutfitLoadout` (8 bytes, one option index per category) | What the host holds the player wearing |
| `RunsStarted` | int | Runs started on this PC: decides what is unlocked |
| `Catalog.SlotCount`, `Catalog.Slot(i)` | `OutfitSlot` | The categories, in listing order |
| `slot.DisplayName`, `slot.OptionCount`, `slot.Option(i)` | string, int, `OutfitOption` | A category and its choices |
| `option.DisplayName`, `BaseColour`, `ShadowColour`, `ContourColour` | string, Color | A choice and the colours it paints |
| `option.IsUnlocked(runsStarted)`, `option.RunsToUnlock` | bool, int | Locked state and the reason: "Locked. Opens after you have started {n} runs. You have started {runs}." |
| `FittingRoom.Instance.IsOpen` | bool | The fitting room is in use |
| `FittingRoomScreen.Show(catalog, loadout, focusedSlot, runsStarted, gamepad)` | call | Redraws the form from the browsed loadout; called on a change, never per frame |

### Events

| Event | Payload | Fires on | When |
|---|---|---|---|
| `FittingRoom.OnEntered` | none | Local | The player stepped into the fitting room (camera starts swinging out) |
| `FittingRoom.OnExited` | bool kept | Local | Back in first person; `kept` false for a put back or a cut-short session |
| `FittingRoom.OnSlotFocused` | int slot | Local | The player moved to another category |
| `FittingRoom.OnOptionShown` | int slot, int option | Local | A choice was shown on the character (every flick) |
| `FittingRoom.OnLockedRefused` | none | Local | Keep was pressed with a locked choice in the outfit |
| `PlayerOutfit.OnWornChanged` | `OutfitLoadout` | Every machine | A player's worn outfit changed after the host accepted it |
| `PlayerOutfit.OnRefused` | none | The owner | The host refused the outfit (an id the catalog doesn't have) |
| `PlayerOutfit.OnRunsStartedChanged` | int | The owner | A run started: locked choices may have opened |

### Requests (send)

`PlayerOutfit.Preview(loadout)` shows a choice locally; `Commit(loadout)` saves it and asks the host; `Revert()` puts back what is worn. The fitting room calls these; a replacement screen should drive `FittingRoom`, not these directly.

### Built from the UI kit

`FittingRoomScreen` is a kit `UiScreen` with one kit `UiStepper` row per category, kit `UiButton`s for Keep outfit and Put back, a kit `UiStatusLine`, on the lobby canvas's own `UiScreenStack` (so it slides and reveals with the kit's motion, and its controls sound and move like every other kit control). It reports `SlotFocused(int)`, `OptionChosen(int, int)`, `KeepPressed`, `PutBackPressed` to `FittingRoom`, which decides. Enter / Space (pad A) and Backspace (pad B) still keep and put back from anywhere on the form. The mouse turns the character; it does not click the form, because the cursor stays captured (a free cursor means the game menu).

### Feel

On every flick the camera pushes in a little and settles, and the character does a small stretch-and-settle (`OutfitView` change bob). Moving to another category eases the camera to that category's own angle, distance and height (set on each `OutfitSlot`), turning back from wherever the player spun it. Keep and put back both slide the form away with a notice ("Outfit kept." / "Outfit put back."). Sounds and motion hang off the events above; none is hard-coded. Tuning: `FittingRoom` (view smoothing, push), `OutfitView` (bob), `OutfitSlot` (view).

## Known limitations

- **The secretary figure has no animator** (T-pose). Players use Alex's animated rig.
- **Suit and trousers can't differ**, and **hair and shoes are one material**, so trousers, shoes and hair categories aren't shipped.
- Mouse clicking on the form doesn't work: the cursor stays captured, because a free cursor means the game menu is open (`GameMenu`). Keys, pad and mouse movement do.
- Skin tone is not a category: the bible has no skin palette yet (CHARACTER_SHADER.md).
