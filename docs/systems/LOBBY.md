# LOBBY

**Status:** Current (lane branch `claude/lane-lobby-085700`, not merged) · **Last updated:** 2026-11-12

## What the player sees

Hosting or joining puts every player on the ground floor of the towers: one reception both firms share, white walls and carpet, a glass street front looking across the gap at the rival tower's base, two lifts, a cobalt building directory, a wall-high "G", a water cooler and a copier.
They walk about with the normal movement and see each other.
The **ready board** (top right) lists both firms, who is in each, who is ready and who is in the fitting room, and one line saying what the start is waiting for.
Looking at something usable shows a **prompt** above the bottom of the screen:

- **Firm sign-in stands** (a sign in each firm's own colour either side of the desk): F signs you in with that firm.
- **The reception desk**: F readies you up, or takes it back. When everyone is ready and the rule below is met, the host's prompt becomes **Hold to start the run** beside the current device’s F, Y or Triangle keycap, with a fill bar; a tap still takes the host's ready back.
- **The tailor's mirror** in the glass corner: F opens the fitting room ([CUSTOMISATION.md](CUSTOMISATION.md)).

The **secretary** sits behind the desk. When someone arrives, readies, signs in or is refused, and when the run starts, she says one deadpan line as a subtitle (navy bar at the bottom) and turns her head toward the camera.
There is no voice audio; each line raises an event so a voice can be attached later.

Steam host, join and invite are unchanged: the game menu (`GameMenu`, `SteamLobby`) still does them, and Esc still opens it over everything.

## Rules (all proposals for the owner)

| Rule | What it does | Where to change it |
|---|---|---|
| **Start** | The host can start when every player is ready, nobody is in the fitting room, each firm has at least one player, and the firms differ by at most 1 | `LobbyRules`: Require Both Firms, Max Imbalance |
| **Auto-balance** | A newcomer joins the smaller firm; on a tie, the first firm | Code (`LobbyRoster.ServerAdd`) |
| **Switching firm** | Allowed unless the new firm would then lead by more than Max Imbalance; a refusal gets a secretary line | `LobbyRules` Max Imbalance |
| **Switching takes the ready back** | Others readied up for the old teams | Code |
| **Fitting room takes the ready back** | So the host can never start with someone still choosing an outfit | Code |
| **Short-handed start** | When everyone is ready but a firm is empty or the firms are uneven, the host can still start: the first hold arms a confirm (the prompt says why), a second hold within 6 s starts the run. A lone player can therefore start a test run from the desk. **Proposal for the owner**: untick Allow Short Handed Start in Lobby Rules and the run waits for both firms | `LobbyRules`: Allow Short Handed Start, Confirm Seconds |
| **Hold to start** | 1 s hold at the desk, host only | `LobbyRules` Start Hold Seconds |
| **Leaving** | A player who leaves is removed from the board; nobody is moved to rebalance; the board then says the firms are uneven if they are | Code |
| **Firm names and colours** | Read from the two `FirmDefinition` assets in `Profiles/Match` (today "Ashcombe & Pryce" and "Halloran Voss", the game mode's proposals; the names are the owner's decision). Lobby Rules > Firms points at them, matching each to its building by its own Tower setting (0 = Building A = firm 0). Nothing in the lobby holds a name or colour of its own | `Profiles/Match/Firm_*.asset` |

## Who owns what

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Roster (client id, name ≤ 24 chars, firm, state) | **Host** (`LobbyRoster`, a scene NetworkObject) | One entry per player, a SyncList: about 8 + name bytes per change | On change only |
| Run in progress | Host | One bool | On change |
| Requests (sign in, ready, fitting, start, name) | Each client asks; the host decides | One small RPC each | On button press |
| Replies (firm chosen or refused, start blocked) | Host → that player | One RPC | On request |
| Run starting | Host → everyone | One RPC | Once per run |
| Prompts, board text, subtitles, secretary head | Each machine, locally | Nothing | — |

Late joiners get the whole roster with the object's spawn.
A client never decides anything: a request the rules refuse changes nothing.

## UI contract

What a screen reads, and when it changes. UI reads this state and sends requests; it never decides an outcome (AGENTS.md §3). Nothing here is polled per frame: rebuild text on the event.

### State (read)

| Read | Type | Owner | Meaning |
|---|---|---|---|
| `LobbyRoster.Instance` | `LobbyRoster` | Host object, readable everywhere; null until the lobby is networked | The roster |
| `Count`, `this[i]` | `LobbyMember { int ClientId; string Name; byte Firm; MemberState State }` | Host, synced list | One entry per player. `Firm` is 0 = Building A, 1 = Building B. `State`: `Browsing`, `Ready`, `Fitting` |
| `TryGetLocal(out LobbyMember)` | bool | Local | This player's own entry |
| `FirmSize(0 / 1)`, `ReadyCount` | int | Derived | Firm counts and how many are ready |
| `Rules.FirmName(f)`, `Rules.FirmColour(f)` | string, Color | `FirmDefinition` assets | Name and colour of a firm; the only source |
| `RunInProgress` | bool | Host, synced | The run has started |
| `Evaluate(out StartBlock reason)` | `StartState { Unavailable, Ready, NeedsConfirm }` | Derived | Whether the host can start. `Ready`: one hold. `NeedsConfirm`: everyone is ready but `reason` is `NeedBothFirms` or `Unbalanced`; a second hold confirms |
| `reason` | `StartBlock { None, RunInProgress, NotAllReady, NeedBothFirms, Unbalanced }` | Derived | Why the start is unavailable (or needs a confirm) |
| `FirmProblem()`, `SmallerFirm()` | `StartBlock`, int | Derived | The firm-balance problem regardless of ready states, and the firm a sign-in would help |
| `StepFor(me, isHost)` | `LobbyStep { Arriving, ReadyUp, WaitForOthers, StartRun, StartShortHanded, WaitForHost, Fitting, RunStarting }` | Derived | The local player's next step: the guidance line |
| `IsLocalHost` | bool | Local | This machine hosts |
| `LobbyInteractor.Target`, `LobbyInteractable.Prompt(gamepad)`, `.HoldProgress` | object, string (cached, null = none), float 0..1 or -1 | Local | What the player is looking at, its prompt line, and the fill while the host holds to start |

### Events

| Event | Payload | Fires on | When |
|---|---|---|---|
| `LobbyRoster.OnChanged` | none | Every machine | Any change to the list or the run flag: rebuild the board and guidance |
| `OnArrived` | `LobbyMember` | Every machine | Someone new joined after this machine did |
| `OnReadyChanged` | `LobbyMember, bool ready` | Every machine | A player readied or took it back |
| `OnFirmChosen` / `OnFirmRefused` | int firm | That player | A sign-in went through / was refused to keep the firms fair |
| `OnStartBlocked` | `StartBlock` | The host | The host tried to start and could not |
| `OnStartNeedsConfirm` | `StartBlock` | The host | The host asked to start short-handed without confirming (the desk confirms with a second hold) |
| `ReceptionDesk.OnConfirmArmed` | `StartBlock` | The host machine | The first hold armed a short-handed confirm (6 s window) |
| `OnRunStarting` | none | Every machine | The host started the run |
| `OnKicked` | string name | Every machine | The host removed a player, just before they leave |
| `Secretary.LineSpoken` | `SecretaryMoment, string, float seconds` | Every machine | The secretary speaks a line (arrival, ready, not ready, firm chosen, firm refused, start blocked, short-handed, start) |
| `LobbyInteractor.OnTargetChanged` | `LobbyInteractable` | Local | The player looks at, or away from, a desk, stand or mirror |
| `LobbyInteractor.OnUsed` | `LobbyInteractable` | Local | The interact button went down on a target |

### Requests (send)

`RequestReady(bool)`, `RequestFirm(int)`, `RequestFitting(bool)`, `RequestStart(bool confirmed)`, `RequestKick(int clientId)` (host only): each is one small RPC the host judges; a refusal comes back as the events above.

### Guidance wording (on the ready board)

| `LobbyStep` | Line |
|---|---|
| `ReadyUp` | "You are with {firm}. Ready up at the reception desk. To change firm, use a sign-in stand." |
| `WaitForOthers` | "Ready. Waiting for {names}." |
| `StartRun` (host) | "Everyone is ready. Hold {interact} at the desk to start the run." |
| `StartShortHanded` (host) | "Everyone is ready. {reason}. Hold {interact} at the desk twice to start anyway." |
| `WaitForHost` | "Everyone is ready. Waiting for the host to start the run." |
| `Fitting` | "You are in the fitting room. Keep the outfit or put it back to continue." |

### Suggested sound and motion hooks

Ready / take back: `OnReadyChanged` (UI sound `LobbyReady` / `LobbyUnready`). Sign-in: `OnFirmChosen` (`Confirm`), `OnFirmRefused` (`Error`). Refused start: `OnStartBlocked` (`Error`). Confirm armed: `OnConfirmArmed` (`Notification`). Start: `OnRunStarting` (`CountdownGo`). Target change: `OnTargetChanged` (`Focus`). Use: `OnUsed` (`Select`).


## Host controls

The host presses **Tab** (pad: **View**) in reception to open **Players**, a kit screen listing everyone else with their firm. Choosing a player asks **Remove {name}?** (kit confirm dialog, **Keep them** focused first); **Remove** asks the roster, which checks again on the host that the asker is the host, that the player is not the host and is in the lobby, then disconnects them. Everyone hears the removal (`OnKicked`) and the board drops them at once. The removed player is told first (`LocalRemovedByHost`, a kit plate over their title: "The host removed you from the lobby. You can join again from their Steam profile or an invite."), and the host disconnects them 0.3 s later so that message arrives.

- **Proposal / known gaps:** a removed player can join again (a ban needs a lasting identity such as the Steam id, which is the netcode side's); no lock-the-lobby or move-between-firms yet.
- The list closes on Tab, Backspace / B, Close, the game menu opening, or the host leaving.

## Look and sound

The board, prompt and subtitles are UI kit **Plates** with the kit's type roles and colours (`UiTokens`), fading on the kit's panel timings. The fitting room and the host's player list are kit **screens** on the lobby canvas's own `UiScreenStack`, with the kit's slide-and-reveal, focus frame (shown after a key or pad press), and the kit controls' own sounds. Reception moments that are not kit controls sound through `LobbySounds`: target change `Focus`, use `Select`, own ready / take back `LobbyReady` / `LobbyUnready`, sign-in `Confirm`, refusals `Error`, short-handed confirm `Notification`, start `Confirm`, fitting room in `Transition`, out `Confirm` (kept) or `Back` (put back).

## Hooks for other lanes

- **`LobbyRoster.RunStartRequested`** (static, host only): `IReadOnlyList<RunEntrant>` with each player's client id, firm (0/1) and name. The match flow listens here; this lane builds no match logic.
- `LobbyRoster.ServerReturnToLobby()` (host): call when a run ends; clears everyone's ready and reopens the desk.
- `LobbyRoster.Instance` events for UI and sound: `OnChanged`, `OnArrived`, `OnReadyChanged`, `OnFirmChosen`, `OnFirmRefused`, `OnStartBlocked`, `OnRunStarting`.
- `Secretary.LineSpoken(moment, text, seconds)` for voice clips; `LobbyInteractor.OnTargetChanged` / `OnUsed` for interaction sounds.

## Files

- Scripts: [Assets/_Project/Scripts/Lobby/](../../Assets/_Project/Scripts/Lobby/) (`LobbyRoster`, `LobbyInteractor`, `LobbyInteractable`, `ReceptionDesk`, `FirmSignIn`, `Secretary`, `LobbyHud`, `LobbyInput`, `LobbyRules`, `SecretaryLines`).
- Tuning: `Assets/_Project/Profiles/Customisation/LobbyRules.asset`, `SecretaryLines.asset` (kept beside the outfit data because the lane owns that folder only; move to `Profiles/Lobby/` on merge if preferred).
- Scene: `Assets/_Project/Scenes/Lanes/Lane_Lobby.unity`, built by **Close the Deal > Lanes > Set Up Lobby** ([LobbyLaneSetup.cs](../../Assets/_Project/Editor/Lanes/LobbyLaneSetup.cs)). Restyle the board and prompt in that tool; the scene is rebuilt from it.

## Known limitations

- **TeamSpawner ignores the firm** while players wait. When the desk starts a run in `Lane_Integration.unity`, the match moves everyone into the firm they signed in with (`HostStartRun`, [INTEGRATION.md](INTEGRATION.md)).
- **Only one lobby room exists.** The GDD gives each firm its own reception; the owner asked for one shared lobby, which is what this builds.
- Interact is **F** (pad: north button), matching the shared asset; E stays lean-right.
- Font is LiberationSans, TextMeshPro's default; the style research's Archivo / IBM Plex Mono aren't in the project.
- The secretary is the same rigged STDMC model as the players, recoloured, with no animator: she holds a T-pose, has no glasses and no seated pose.
- The lobby stays joinable after a run starts (as NETCODE.md already notes).
