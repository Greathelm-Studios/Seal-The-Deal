# Handover: UI wide pass (lane 5)

**Status:** Target · **Last updated:** 2026-11-14 · **From:** lane 5 UI agent (Claude), worktree `.claude/worktrees/lane-ui`, branch `claude/lane-ui`

Copy rules: when slices 4–6 land, move this to `docs/archive/` with a one-line outcome (AGENTS.md §7).

## Goal

Every screen a shipped game has, built on the kit (`docs/systems/UI_KIT.md`), with motion and a sound on every input (`ArtSource/UI/research/feel_wii_ds.md` in the main checkout is the contract), wired to real game state, legible at 1280×720 and 21:9, mouse + keys + pad with visible focus.

## Where it stands

Committed on `claude/lane-ui` (buffer merged in before each):

- "Add the kit's edge marker and key hints that follow the player's device": `UiEdgeMarker` (+ `EdgeMarker.prefab`), `UiGlyphs` (device tracking and the one key-name table), `UiKeyHint` on the `KeyHint` prefab; title, settings, confirm and the Controls page's pad column switch Enter / A / Cross. Lane 4 draws its briefcase marker with it.
- "Put the title on the live sky only, give settings a changes summary, and fix the slider bar and resolution revert": lane 8's live sky is the only sky (flat fallback without a sky material), 22 px slider thumbs, "Changed on this tab" under the rows, resolution revert / clamp, focus marks sleep, fixed-key refusal, `UiAudio.DefaultOf`.
- The slice 3 commit (hosting, joining, loading, problems; see its body): `SteamLobby` gained `LobbyProblem` / `LobbyStage`, `Problem` (kept until acted on), `RetryJoin`, `OpenFriends`, `StatusText`, inventory §5.2–5.3 copy, a join in progress counts as `IsActive`, a cancelled Steam join is abandoned; `GameMenu.OutOfGame` hands every out-of-game state to the kit; `SessionPanel` and `ProblemPanel` over the title; `UiRoot` decides them from state and plays lane 8's frame zoom into the game; the edge marker slides round the bottom when a target crosses behind (140 ms).

Not built (slices 4–6, in this order; the brief is the coordinator's "wide pass" message):

4. **In-game menu** as a light glass pane (lane 7's material via `UiGlassPane`; fallback stays) that does not pause; leave confirm (host vs client copy, ui_inventory §5.5); Esc rules; menu open ⇔ mouse free ⇔ player idle; replaces the dark playtest card (`GameMenu`'s `Menu` card). Also owed here:
   - **Promised to lane 6 (n0-m-4d-4b6e, AgentHub #516/#517):** `GameMenu.HoldCursor(object owner)` / `GameMenu.ReleaseCursor(object owner)`, reference-counted per owner: while any hold exists in a game the cursor is free and visible, input stays idle, and the "cursor freed elsewhere" rule does not open the menu; Esc still opens the menu; leaving / despawning drops all holds. Post on AgentHub when committed.
   - Lane 4's #487: the old "Esc · Menu" chip and the network readout move onto the kit (KeyHint / Plate); the readout obeys Show network stats.
   - Lane 4's #499: the card's title still reads "CLOSE THE DEAL / Greybox playtest" (GreyboxSceneSetup.cs); the players line counted only Steam members (fixed in `SteamLobby.PlayerCount` for local clients; the line itself goes with the card).
   - Review lane's suggestion: a Settings entry in the in-game menu.
   - Lobby seats / ready (mockups `01_menu_survey_05/06`): there is nowhere to show them out of a game (players spawn into reception at once), so they belong in the in-game menu while in reception, read-only from `LobbyRoster` (lane 6 owns the ready board and the start; the UI never starts the run).
5. Results + rematch screen (lane 4's `MatchDirector.Snapshot`, `ReasonLine`, `OnMatchEnded`, `CanHostRematch/HostRematch`, `FirmName/ColourFor`; lane 11's `Records.MatchRecords` for the per-player table); scoreboard (Tab); kill/event feed; toast stack; subtitle bar (`Narration.Subtitles.Shown`); first-run quick setup + hint toast (`Onboarding.FirstRunHints.HintShown`); controls help card from real bindings (`UiGlyphs`); credits and licences from the repo's real licence files only; quit-to-desktop flow; version line (`Build.BuildInfo.Display`). Toasts and the scoreboard wear lane 7's glass. All of lane 11's contracts are in buffer now (docs/systems/RECORDS.md, NARRATION.md, ONBOARDING.md, BUILD.md).
6. `Assets/_Project/Scenes/Lanes/Lane_UiGallery.unity` built by `Close the Deal > Lanes > Build UI Gallery` (GeneratedAssetGuard rules): every component in every state, pages, keys/pad navigation, a reduced-motion toggle.

## What was checked

- `node ~/.claude/skills/compile-check/compile-check.mjs`: PASS, 0 errors, after every merge of buffer.
- Captures: `scratchpad run.sh <mode>` = Unity 6000.4.8f1 `-batchmode` (no `-quit`, no `-nographics`) `-executeMethod CloseTheDeal.Editor.UI.UiKitCapture.RunFromShell -uiCaptureOut <dir> -uiCaptureMode wide1|wide2|wide3 -local -lanePort 17785`, after `UiKitSetup.ReplaceFromCommandLine` (rebuilds untouched kit prefabs, replaces KitUI in Lane_Integration; do NOT commit that scene unless you hold it: post on AgentHub first, orchestrator rule #514). All three modes: all checks PASS. Script: `Assets/_Project/Editor/UI/UiKitCaptureWide.cs`.
- wide3 runs a real local host (asserts it is the server) and a real failed join (a silent UDP socket on port+1 so the client genuinely waits), Cancel while connecting, Back on a problem, the Error sound on arrival, and logs clips through the "UI" mixer group.
- Not observed: a second instance (host closed / dropped / connection lost are only stills from `ProblemPanel.Show`), Steam (every Steam-only problem is a still), a gamepad (device switching is forced in the capture), Play Mode by a person, the frame zoom itself (the capture calls lane 8's `UiTransition.Preview` on the UI camera, but no zoom frames appear in `13_session_problem_feel.mp4`; the zoom draws on its own overlay; film it in Play Mode or fix the preview path).
- Captures in the main checkout's `ArtSource/UI/review/`: `12_*` (slices 1–2) and `13_*` (slice 3), each with a capture log, a sound log and an mp4.

## Open questions and risks

- Final typeface missing (Liberation Sans only). TMP's `LiberationSans SDF - Fallback.asset` keeps rewriting itself in the tree: never stage it.
- **Shared PlayerPrefs:** every checkout on Adam's Mac shares `~/Library/Preferences/unity.DefaultCompany.CloseTheDeal.plist`. An interrupted rebind capture (`RebindClash`, `persist-write`) left Jump on G (cleared on 2026-11-14, AgentHub #521/#522). Make any capture that writes InputPrefs / UiSettings restore in a finally path.
- Sun slabs on the title still cross the text (behind it); the orchestrator said to keep them as they are.
- `TitleSky` (lane 8) still renders its camera when a scene has no sky material, under the kit's fallback; the fix is in lane 8's file.
- Lane 7 asked for a glass pane that overlaps the live sky (#498) so the glass has something to show; not done.
- On this PC a failed join gives up at LiteNetLib's ~5 s while the panel says 15 s (the lobby's own backstop).

## Defaults chosen (write them into UI_KIT.md when they change)

- Pads that are neither Xbox nor PlayStation get Xbox labels; stick clicks read L3/R3 on both; nothing bound on a device shows "—".
- Edge marker: over the target in view, 48 px in at the edge out of view; loose square drawn at 72 %; side flip slides 140 ms round the bottom.
- Session panel: only the connect clock gets a filling track; every other wait shows the chevrons only. Cancel reads "Close lobby" while hosting.
- Problems: each keeps the inventory's §5.3 words and gets one fix button (Host again / a game, Try again, Open Steam friends, Quit to desktop) plus Back; a fix that can't run is greyed with its reason.
- Settings: the changes summary is hidden on Controls (its key list fills the page).

## Next steps

1. `git merge buffer` in the worktree, compile-check, read UI_KIT.md, MENU_AND_HUD.md and this file.
2. Build slice 4 starting with `GameMenu.HoldCursor` / `ReleaseCursor` (lane 6 is waiting) and post on AgentHub (channel `Greathelm-Studios/Seal-The-Deal`, agent `n0-m-4d-ced8`).
