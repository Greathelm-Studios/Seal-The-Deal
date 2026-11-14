# UI sound audit: what each interaction plays

**Read from the code, 2026-11-14, buffer as merged into claude/lane-ui-sound. Not run in Unity** (Adam asked for no headless checks, #544), and **nobody has listened**.
Rule that decides most of it: the sound player keeps 12 ms between any two sounds, so when one action asks for two sounds in the same frame, the second is dropped.

| Where | Action | Plays now | Verdict |
|---|---|---|---|
| Any control | Mouse over it | HoverMove (pitch climbs in a run) | OK |
| Any control | Keys / pad move focus to it | Focus | OK (a mouse click never plays Focus) |
| Any control | Arrow at the end of a list | nothing | Missing: no "edge bump" event yet |
| Button | Press down | Select | OK |
| Button | Release / Enter / pad A | Confirm (or the button's own sound) | OK |
| Greyed-out control | Press | Error, and the reason in the status line | OK |
| Toggle | Click | Select on press, then ToggleOn / ToggleOff on release | OK |
| Slider | Drag or arrow | slider tick, pitch follows the value | OK |
| Slider | Click on the track | was Select (masked the tick); **now the pitched tick** | Fixed |
| Stepper (e.g. graphics quality) | Left / right | was Select, the same as a press; **now the tick, pitched by the option** | Fixed |
| Settings | Open from the title | was Confirm only (the screen's Transition in the same frame was dropped); **now Confirm and the Transition together** (screen sounds have their own layer) | Fixed |
| Settings | Change tab (Q / E or click) | TabChange | OK |
| Settings | Back button or Esc | Back, once | OK |
| Controls tab | Press a key cell | was Confirm (masked KeyBindListen: **the listen sound was never heard**); **now KeyBindListen** | Fixed |
| Controls tab | Press a free key | KeyBindSet | OK |
| Controls tab | Esc while listening | Back | OK |
| Controls tab | Key kept for movement or slots | Error | OK |
| Controls tab | Key already used (clash) | Error, and the dialog's PanelOpen with it (screen layer) | OK |
| Clash dialog | Assign | was Confirm (masked KeyBindSet); **now KeyBindSet** | Fixed |
| Any dialog | Stay or Esc | Back, once | OK |
| Title | Esc | PanelOpen ("Quit to desktop?") | OK |
| Title | Title appears | **TitleAppear** (once per arrival, not when a dialog closes) | Added |
| Game start | Frame-zoom transition | **TransitionFrameZoom** (the game-start sting); cloud wipe, pane slide and fade each have their own | Added |
| Kit | Dropdown open / close | never fires | Not a fault: the kit has no dropdown control |

Title and transition sounds are hooked in `UiSoundPlayer.cs` and lane 8's `Scripts/UI/Title/UiTransition.cs`. Fixes are in `Scripts/UI/Kit/ControlsPage.cs`, `UiConfirmDialog.cs` (`Configure` gains an optional last `confirmSound`), `UiStepper.cs` and `UiSlider.cs`, agreed with the UI lane on AgentHub (#553, #554).
