# LOBBY

**Status:** Current (lane branch `claude/lane-lobby-085700`, not merged) · **Last updated:** 2026-11-12

## What the player sees

Hosting or joining puts every player on the ground floor of the towers: one reception both firms share, white walls and carpet, a glass street front looking across the gap at the rival tower's base, two lifts, a cobalt building directory, a wall-high "G", a water cooler and a copier.
They walk about with the normal movement and see each other.
The **ready board** (top right) lists both firms, who is in each, who is ready and who is in the fitting room, and one line saying what the start is waiting for.
Looking at something usable shows a **prompt** above the bottom of the screen:

- **Firm sign-in stands** (cobalt and cyan signs either side of the desk): F signs you in with that firm.
- **The reception desk**: F readies you up, or takes it back. When everyone is ready and the rule below is met, the host's prompt becomes **Hold F · Start the run**, with a fill bar; a tap still takes the host's ready back.
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
| **Hold to start** | 1 s hold at the desk, host only | `LobbyRules` Start Hold Seconds |
| **Leaving** | A player who leaves is removed from the board; nobody is moved to rebalance; the board then says the firms are uneven if they are | Code |
| **Firm names** | "Cobalt" and "Cyan" (the colours proposed in the UI style research, T1). The real names are undecided | `LobbyRules` |

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

- **TeamSpawner ignores the firm.** It still puts players on teams by connection id (`ClientId % 2`). The game-mode lane must spawn runs from the `RunStartRequested` list instead.
- **Only one lobby room exists.** The GDD gives each firm its own reception; the owner asked for one shared lobby, which is what this builds.
- Interact is **F** (pad: north button), matching the shared asset; E stays lean-right.
- Font is LiberationSans, TextMeshPro's default; the style research's Archivo / IBM Plex Mono aren't in the project.
- The secretary is the same STDMC model as the players, recoloured; she has no glasses, no seated pose and no animation until a rigged character arrives.
- The lobby stays joinable after a run starts (as NETCODE.md already notes).
