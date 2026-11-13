# CUSTOMISATION

**Status:** Current (lane branch `claude/lane-lobby-085700`, not merged) · **Last updated:** 2026-11-12

## What the player sees

In reception, a glass-walled tailor's corner holds a full-length mirror, a plinth and a rail of suits.
Looking at the mirror and pressing **F** swings the camera out of first person (0.45 s, eased) into a three-quarter view of your own character, and a white form slides in on the left: **Form F-1 · Wardrobe amendment**, with one row per outfit category.

- **A / D** (or d-pad left/right) changes the focused category's choice; the character changes at once and gives a small tailor's bob, and the choice name kicks sideways.
- **W / S** (or d-pad up/down) moves between categories; the focused row has a cobalt bar.
- **Mouse, Q / R or the right stick** walks the view round the character.
- **Enter** (pad A) keeps the outfit; **Backspace** (pad B) puts back what you were wearing. Either swings the camera back into first person (0.35 s).
- Esc still opens the game menu; the fitting room waits underneath until it closes.

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
- Prefab: `Assets/_Project/Prefabs/Lobby/OutfitCharacter.prefab` (STDMC fitted to 1.8 m, feet at 0, facing +Z, under a `Fit` pivot the bob scales). **Set Up Lobby** puts it on `Player.prefab` under `Graphics/Lean` at y −0.9 and turns off the greybox capsule and nose renderers.

## The rigged character (now in the branch)

- `OutfitCharacter.prefab` (the secretary, and the greybox player) is rebuilt from `STDMC_Toon` each time **Set Up Lobby** runs, measured from all its renderers together, so a changed model needs only a re-run.
- Each category points at **materials by name** (`STDMC_lambert4_Toon` and so on), found on the body's renderers at run time (the rigged model is split into several meshes; `OutfitView` lists them all). A rigged model that keeps those material names needs nothing; a new naming needs `Slot_*.asset` updated (render each material highlighted, as was done for STDMC, to see which is which).
- If `Player.prefab` already has a real character at `Graphics/Lean/Body` (as Alex's player build does), **Set Up Lobby** puts `OutfitView` on that object and adds no second figure and hides nothing. **Seen** on the merged player build (recolouring suit and shirt on the animated model in the fitting room).
- Parts that are separate meshes (hair, eyewear) become show/hide categories: that needs one small addition to `OutfitView` (enable one child per choice), not done here because no such parts exist yet.
- What the owner's categories need from the art: see [HANDOVER_lobby.md](../HANDOVER_lobby.md).

## Known limitations

- **The secretary figure has no animator** (T-pose). Players use Alex's animated rig.
- **Suit and trousers can't differ**, and **hair and shoes are one material**, so trousers, shoes and hair categories aren't shipped.
- Mouse clicking on the form doesn't work: the cursor stays captured, because a free cursor means the game menu is open (`GameMenu`). Keys, pad and mouse movement do.
- Skin tone is not a category: the bible has no skin palette yet (CHARACTER_SHADER.md).
