# UI and UX revamp: shared brief for every lane

**Written:** 2026-11-13 by Adam's orchestrator. Read this whole file, then your lane's message.

## What the owner asked for

Adam's words: "i would like a revamp on the UI and the UX ... get all the lanes on a revamp ... i would like to see a lot of good UI, professional looking UI, with the themes i have set last night."

And, from Alex, on feel: "a good reference for ui in terms of sound, animation and feel, look at the wii and the ds ui feel and animation, not necessarily their style, more just how they animate and feel."

The game is **Seal the Deal** (never "Close the Deal" in anything a player reads; the `CloseTheDeal.*` namespace and `Close the Deal >` menu root stay until a dedicated rename).

## Decisions (orchestrator defaults; Adam was asked and can overrule)

1. **The revamp is built for real in Unity**, in the `buffer` line of work, because sound, animation and feel cannot be judged in a still. VectorCraft mockups are only the look reference for a screen before it is built.
2. **One direction: pass A "Survey" layout with pass B "Aero" glass.** Sky and paper white are the two fields; sky sits inside frames (the window frame, strips, panes); panels are light, see-through, cool glass or white plates with Deep Navy type; thin wide type with one large thin title; hairlines, grids, corner brackets, chevron rows, barcodes; black as a thin accent; Amber `#FF9E1B` only for the rival firm and threat. No dark panels. The guide is `ArtSource/References/ui/` (nine images + README); the mockups are in `ArtSource/UI/review/` (start with `00_compare_passes.png`, the pass-A menu, settings and HUD boards, and pass B's kit and hero).
3. **Feel comes from the Wii and DS system menus, not their look**: everything answers the player. Every hover, focus, press, back, error and page change has motion and a sound; tiles grow and settle; presses squash; screens zoom or slide into each other instead of cutting; the cursor is friendly and magnetic; idle screens are alive. The written rules are in `ArtSource/UI/research/feel_wii_ds.md` (motion table, sound table, focus behaviour) as soon as the UI lane lands it; build to that table so every lane's UI moves and sounds the same. Take feel only: no Nintendo assets, sounds, layouts or marks.
4. **Voice:** corporate and legal, played straight. Buttons are short verbs. Refusals say why.

## Who owns what

| Lane | Owns in the revamp |
|---|---|
| 5 UI | The Unity UI foundation: tokens, components, motion and sound hooks, focus and cursor; main menu, settings, crosshair editor, in-game menu, results. Everyone else builds on this. |
| 7 Aero | The glass: a real frosted, see-through UI glass material for URP, with a no-blur fallback. |
| 8 Sky | The title screen's live sky backdrop in the window frame, and screen-to-screen transitions. |
| 9 Seven | The UI sound set (original, synthesised) and the UI audio bus. |
| 1 Movement | Movement UX: crosshair behaviour with speed, speed and stamina readouts if any, input feel, gamepad feel, controls help. |
| 2 Shooting | Combat HUD: hit marker, kill confirm, ammo and weapon slots, damage direction, low-ammo and reload states. |
| 3 Pickups | Interact prompts, hands slot, throw arc or charge, hands-full refusal, item names. |
| 4 Game mode | Briefcase tracker, open-floor ladder, gate refusal, match clock, countdown, floor-opened banner, win and cap results data. |
| 6 Lobby | Reception UX: firm sign-in, ready board, start, fitting room and outfit browser. |

## Rules for everyone

- **Until the foundation is in `buffer`** (the orchestrator will tell you), do the parts that don't need it: merge `buffer` into your branch; write your feature's **UI contract** (the exact synced state and events a HUD reads, with types, who owns them, when they fire; never polled per frame); fix the UX faults listed for your lane; expose missing events. Do not build one-off UI in your own style.
- **After it lands:** build your feature's UI from its components only. No new colours, fonts or panel styles; if you need a component that doesn't exist, ask the UI lane on AgentHub.
- Work in your own worktree and branch, never in the main checkout (Adam's and Alex's editors are open there) and never in `.claude/worktrees/buffer`. Commit testable slices (code-review first, explicit paths, no AI attribution lines: AGENTS.md §9), never push, never merge into `buffer` or `main`; end a turn with `READY TO MERGE:` and hand-test steps.
- A hook blocks commits that stage graphics settings (`Assets/Settings/*`, "hero graphics are frozen") unless the subject names the graphics change. Don't stage those by accident.
- Host authority is unchanged (AGENTS.md §3). UI reads synced state and sends requests; it never decides an outcome.
- Alex's sessions (branch `Alex_Env`, agents `kr0meee-…` on AgentHub) are building a first-person viewmodel, an arsenal, an elevator level and "pick up any loose object". Lane 3's `ItemCarrier` is the one pickup system; Lane 2's `WeaponHolder` events drive any gun view. Coordinate on AgentHub (channel `Greathelm-Studios/Seal-The-Deal`; you are in bypass-permissions mode now, so hub calls work): set `hub_status`, claim your folders, post rarely.
- Subagents: `sonnet` for mechanical work, `opus` for design and netcode decisions. Each subagent brief restates the done bar and ends with its own Verify list.
- Reports to Adam: one sentence per line, bad news first, bold only what matters, "ready for review" with known issues, never "done".

## Done bar

**Finished, not mocked:** the working UI in the real project, wired to real game state, with controls that work; no placeholder copy, stubs, TODOs or fake data; if something can't be real yet, leave it out and say what's missing.

**Product, not demo:** every state designed (first run, loading, empty, failure, offline, paused, quit and resume); every input answers with sound and motion; screens transition rather than cut; nothing reads as a default engine look; settings stick between launches; legible at 1280×720 and on ultrawide; a stranger can start, understand it and recover from a mistake; mouse, keyboard and gamepad all work with visible focus.

## Verify (every lane, plus its own list)

1. Compile check: 0 `error CS` on your worktree.
2. A capture (frames or clip) of your UI running in a scene, looked at for clipping, popping and overlap.
3. Every interactive element shown in default, hover, focus, pressed and disabled, with its motion and sound event firing.
4. One failure or edge case per screen (a refusal, an empty list, a lost connection, menu opened mid-action).
5. At 1280×720 and 21:9 nothing overlaps or leaves the screen.
6. Host and one client where the UI shows synced state; say plainly what was seen on one instance only.
