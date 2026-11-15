# Handover: UI and UX, art direction

**Status:** Target · **Last updated:** 2026-11-15 · **From:** Adam's orchestrator session (Claude), handing over to a fresh session

Read this first, then `AGENTS.md`. Adam is closing the old lane sessions because their context is full. This file is the single place to start from. It points the next session at the art direction and the references Adam chose; it does not repeat the code docs, it links them.

## Goal

A professional, finished-looking UI for **Seal the Deal** that a stranger would take for a shipped game: built for real in Unity, in the look Adam chose, and with the feel of the Wii and DS menus (every input answers with motion and sound). Adam's words: "a lot of good UI, professional looking UI, with the themes I have set", and on feel: "look at the Wii and the DS UI feel and animation, not necessarily their style, more just how they animate and feel."

**Today's focus, from Adam: the UI only.** No new gameplay or mechanics work.

## The art direction (what to build to)

Read these, in this order, before drawing or building anything:

1. **The nine UI references Adam called "the guide we need for UI"**: [ArtSource/References/ui/](../ArtSource/References/ui/) and its [README](../ArtSource/References/ui/README.md) (what each shows and what to take). They overrule everything below where they disagree.
2. **The art bible**: [ArtSource/Kit/concepts/Office/refs/art_bible.txt](../ArtSource/Kit/concepts/Office/refs/art_bible.txt), sections 03 colour, 13 typography and graphics, 14 UI/HUD, 19 composition, 20 do/don't. Its four plates `bible_ref_01..04.png` sit beside it.
3. **Alex's sky, light and grade references** (the in-game look the UI sits on): [ArtSource/References/](../ArtSource/References/) and its README. Alex liked `sky_and_clouds/03_clouds_traced_by_squares_over_grid` and `04_thresholded_flat_cloud_cel` best.
4. **Adam's style keywords**: "2000s corporate futurism, aero, gen X soft club, glassmorphism".
5. **The shared revamp brief** the lanes built to: [ArtSource/UI/REVAMP_BRIEF.md](../ArtSource/UI/REVAMP_BRIEF.md).
6. **Research behind it**: [ArtSource/UI/research/](../ArtSource/UI/research/): `style_research.md` (tokens, glass recipe, motifs), `ux_research.md` (sizes, crosshair, contrast), `ui_inventory.md` (every screen, label and binding), `feel_wii_ds.md` (motion and sound tables), `fonts_by_pass.md` (typefaces per direction).

**The direction, as decided so far** (Adam has approved none of it formally; these are the working calls):

- **Layout of pass A "Survey" with the glass of pass B "Aero".** Sky and paper white are the two fields; sky sits inside frames; panels are light see-through glass or white plates with Deep Navy type; thin, wide type with one large thin title; hairlines, grids, corner brackets, chevrons, barcodes; black only as a thin accent. **No dark panels.**
- **The title is a white window frame over the game's own live sky** (Lane 8's backdrop), not painted clouds. Adam on the frame: "the borders on the main screen are too thick, but they do work, they do look nice." The bars were thinned from about 54 px to 24 px at 1080p; he has not seen the result in play.
- **Palette**: the bible's seven swatches (#F3F4F4, #C9CED3, #111B38, #244FB9, #2879EF, #20D4E6, #101116) plus **Amber #FF9E1B** for the rival firm and for threat. You are cyan ◆, the rival is amber ▲. Amber as a solid block reads too heavy (the old rival chip, the current "Unable to reach the host's game" box): use it as an edge or mark, not a fill.
- **Feel**: build to the motion and sound tables in `feel_wii_ds.md`. Hover grows and settles, press squashes, focus brackets snap, screens slide or zoom rather than cut, idle screens are alive. User-started motion under 300 ms. A reduce-motion path for every animation.
- **Name and voice**: the game is **Seal the Deal** (never "Close the Deal" in anything a player reads; the `CloseTheDeal` namespace and `Close the Deal >` menus stay until a dedicated rename). Tagline "Terms and Conditions Apply." Copy is corporate and legal, played straight. Interact is **F**, drop/throw **G**, lean **Q/E**.

**Earlier explorations, kept for reference only**: four VectorCraft kit passes, A Survey, B Aero, C Sky, D Seven (Windows 7 glass). Side by side in [review/keyframes/00_compare_passes.png](../ArtSource/UI/review/keyframes/00_compare_passes.png); heroes `00a`–`00d` beside it. The generator that drew them is [ArtSource/UI/tools/](../ArtSource/UI/tools/) (`ctd_ui.py` and `make_*.py`).

## Where it stands

All code is on the **local branch `buffer`** (worktree `.claude/worktrees/buffer`), about 230 commits ahead of `origin/main`. **It has not been pushed.** It holds every lane plus `origin/harvey-netcode` (which includes `origin/main`). Last compile check: 0 errors. **Nobody has played any of it by hand.**

**Built in Unity, on the UI kit** (code docs: [UI_KIT.md](systems/UI_KIT.md), [UI_GLASS.md](systems/UI_GLASS.md), [UI_TITLE.md](systems/UI_TITLE.md), [UI_SOUND.md](systems/UI_SOUND.md), [MENU_AND_HUD.md](systems/MENU_AND_HUD.md)):

- **UI foundation** (`Assets/_Project/Scripts/UI/Kit/`, assets in `Assets/_Project/UI/`): tokens, buttons, settings rows, focus frame, motion driver, sound hub `UiSound`, screen stack.
- **Title and main menu** on the live sky, device-aware key hints (keys, Xbox, PlayStation), hosting, joining, problem screens, quit confirm.
- **Settings**: Video, Audio, Controls (rebinding with a clash dialog), Accessibility (crosshair editor, reduce motion). Values save between launches.
- **In-game menu on glass that doesn't pause**: committed last ("Give the game an in-game menu on glass that doesn't pause…"), **never captured**.
- **Screen transitions** (pane slide, cloud wipe, frame zoom, fade), called from the screen stack.
- **Frosted UI glass** for URP with a no-blur fallback, on the HUD plates.
- **Feature HUDs on the kit**: match (clock, briefcase tracker, open-floor ladder, gate refusal, floor opened, result), combat (hit marker, kill confirm, ammo, reload, health, death card), items (pick-up prompt, hands slot, refusals, look highlight), movement (crosshair that opens with speed, controls card), name plates over heads, reception and fitting room.

**What to look at**: [ArtSource/UI/review/keyframes/](../ArtSource/UI/review/keyframes/) (committed). The full set of about 90 captures and clips is on disk in the main checkout at `ArtSource/UI/review/` with a gallery page `UI_NOW.html` (not in git).

## Known faults, worst first

1. **Typeface**: everything still uses Unity's default (Liberation Sans). This does more harm to the look than any other single thing. Target faces are in `fonts_by_pass.md`; **downloading them needs Adam's yes.**
2. **The in-game HUD looks unfinished next to the menus**: the old dark "Esc Menu" and network-readout chips still show in combat captures (Lane 5's in-game menu commit claims to replace them; unverified), and the gun is a grey box.
3. **Amber error box** on the problem screens is a heavy solid fill.
4. **Sound**: the 58 synthesised clips and a test batch on sampled instruments (FluidSynth + GeneralUser GS, kept in `ArtSource/Audio/synth_test/`, main checkout only) were both rejected by Adam as too artificial. Kenney's CC0 interface and impact packs were also rejected. Every sound slot can now take a recording (drop it in `ArtSource/Audio/recorded/…`, add a row to `ArtSource/Audio/LICENCES.md`); see `ArtSource/Audio/SOUND_SHOPPING_LIST.md`. **The GDD says no asset packs ship**, so library sounds need Adam to change that line. Text-to-sound models (MOSS-SoundEffect, Stable Audio Open) were found but not tried.
5. **Two held-gun systems**: Lane 2's `WeaponView` and Harvey's `PlayerViewmodel`/`PlayerHeldWeapon`. In `buffer`, `PlayerViewmodel` is not on `Player.prefab` until "Close the Deal > Greybox > Rebuild Viewmodel" is run (now add-only). With both on, a body can draw two guns. Not UI, but it shows in every first-person capture.
6. **Two pick-up systems run together**: Lane 3's `ItemCarrier` and Alex's stand-in `DevCarry` both use F and G in the editor. **Adam's decision: keep Alex's; build around it.** Lane 3's is also kept.

## Rules that bit the last sessions

- **No headless verification runs.** Adam: "no need to run headless checks… I want to see the game." Compile-check, commit, and give him exact hand-test steps. Unity runs only to apply a set-up menu or to make a capture he asked to see.
- `Player.prefab`: every tool only **adds** what is missing; never rebuild it. `Lane_Integration.unity`: one session at a time commits it; announce on AgentHub first.
- Saved settings (PlayerPrefs) are shared by every checkout on this Mac: restore anything a run changes.
- A hook blocks commits that stage `Assets/Settings/*` unless the subject names the graphics change.
- No AI co-author lines in commit messages (`AGENTS.md` §9).
- AgentHub channel `Greathelm-Studios/Seal-The-Deal`. Alex's agents are `kr0meee-…`, Harvey's `sixnationn-…`.

## Open questions for Adam

- Push `buffer`, and merge it into `main`?
- Download the target fonts?
- Sound: which source (recordings, a text-to-sound model, or a person designing them)? Change the GDD's "no packs ship" line?
- Firm names (currently proposals: "Ashcombe & Pryce" and others in the firm assets).
- Floor gates, locked doors, or both; headshots; friendly fire (all gameplay, parked).
- Harvey asked for a reply on Discord.

## Next steps

1. Open the `buffer` worktree in Unity, run "Close the Deal > Lanes > Set Up Integration", open `Assets/_Project/Scenes/Lanes/Lane_Integration.unity`, press Play, and capture the title, settings, the in-game menu (Esc) and the HUD as they are now. Compare against the keyframes.
2. Fix the look in this order: typeface (once Adam allows it), the in-game HUD chips and plates onto the kit and glass, the amber error box as an edge, then motion and sound polish against `feel_wii_ds.md`.
3. Show Adam a capture or short clip per change and wait for his call before the next visible change.
