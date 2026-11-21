# LOBBY

**Status:** Current · **Last updated:** 2026-11-21

## What the player sees

Hosting or joining puts every player in shared **reception** with white walls, carpet, a glass front looking down the street at the towers, two lifts, a firm directory, water cooler, and copier.

The **ready board** (top right) lists both firms, their members, who is ready or in the fitting room, and one guidance line saying what start is waiting for. Looking at usable reception objects shows an interact prompt:

- **Firm sign-in stands** (one per firm flanking the desk): press **F** to sign in with that firm.
- **Reception desk:** press **F** to ready up or unready. When everyone is ready, the host's prompt becomes **Hold F to Start the run** with a fill bar (a quick tap still unreadies the host).
- **Tailor's mirror** in the glass corner: press **F** to open the fitting room ([CUSTOMISATION.md](CUSTOMISATION.md)).

The **secretary** behind the desk turns toward the player and speaks deadpan lines as subtitles ([NARRATION.md](NARRATION.md)) on arrivals, sign-ins, ready changes, refusals, and run start, raising `Secretary.LineSpoken` for future voice clips.

## Rules (proposals for the owner)

| Rule | Behaviour | Where configured |
|---|---|---|
| Start | Host starts when all are ready, nobody is fitting, both firms have a player, and firm imbalance is within limit | `LobbyRules`: `RequireBothFirms`, `MaxImbalance` |
| Auto-balance | New arrivals join the smaller firm (first firm on tie) | `LobbyRoster.ServerAdd` |
| Switching firm | Refused if target firm would lead by more than `MaxImbalance`; switching clears ready state | `LobbyRules`: `MaxImbalance` |
| Fitting room | Entering clears ready state so the host cannot start while someone is changing | `LobbyRoster.CmdSetFitting` |
| Short-handed start | When all are ready but firm balance/empty check fails and `AllowShortHandedStart` is enabled, first hold arms a 6 s confirm and second hold starts | `LobbyRules`: `AllowShortHandedStart`, `ConfirmSeconds` |
| Hold to start | Host holds interact for 1 s at the desk | `LobbyRules`: `StartHoldSeconds` |
| Leaving | Leavers drop off the board without auto-rebalancing remaining players | `LobbyRoster.OnRemoteConnectionState` |
| Firm names and colours | Read from `FirmDefinition` assets in `Profiles/Match/`, matched by `Tower` index (`0` = Building A, `1` = Building B) | `Profiles/Match/Firm_*.asset` |

## Ownership and netcode boundary (`AGENTS.md §3`)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Roster (`ClientId`, `Name` <= 24 chars, `Firm`, `State`) | Host (`LobbyRoster`) | `SyncList<LobbyMember>` (~8 + name bytes) | On change only |
| Run in progress | Host | `bool` `SyncVar` | On change |
| Requests (sign in, ready, fitting, start, kick, Steam name) | Client sends `ServerRpc`; host validates | Small RPC per action | On press |
| Replies (firm chosen/refused, start blocked/confirm, kicked) | Host sends `TargetRpc` / `ObserversRpc` | Small RPC | On request |
| Prompts, board text, subtitles, secretary head/idle | Computed locally on each machine | Nothing | Never |

Late joiners receive the full `SyncList` on spawn; clients defer arrival greetings for `InitialSyncSeconds` (1 s) so initial list replay does not trigger greeting lines.

## Architecture and design rationale

- **Roster-driven flow (`LobbyRoster`, `ReceptionDesk`):** `ReceptionDesk` uses preallocated string lookup tables indexed by `Mode` so zero strings allocate per frame; short-handed confirm prompts are built only when armed. On kick (`CmdKick`), the host verifies `conn.IsHost`, removes the target from `_members` immediately (avoiding transport disconnect lag), sends `TargetYouWereRemoved` (`LobbyRemovedNotice` displays the explanation over title UI), and disconnects after `0.3 s` (`KickGraceSeconds`) so the message arrives.
- **Counted interaction blocker (`LobbyInteractor`):** `Block()` / `Unblock()` uses a reference counter (`_blockers`) so overlapping owners (fitting room, host panel, `LobbyRoom` boundary) never prematurely unblock each other.
- **Secretary idle playback (`SecretaryIdle`):** Strips `Animator.avatar` so bone clips play without humanoid retargeting via a manual `PlayableGraph`, offset per world position and paused whenever `RunInProgress` is true or all renderers are off-screen.
- **Host player list (`LobbyHostPanel`, `LobbyHostControls`):** Host toggles **Tab** (gamepad: **D-pad Up hold 0.5 s**, per `LobbyInput`) in reception to open the player list, holding the cursor via `GameMenu.HoldCursor` and verifying `ClientId` + `Name` before executing a kick in case a slot reused mid-dialog.
- **UI & sound (`LobbyHud`, `LobbySounds`):** Uses UI kit plates, `UiTokens`, `UiScreenStack`, and `UiSound`. `LobbyHud` validates kit wiring in `Awake` and disables cleanly on older scenes.

## UI contract

- **Read on `LobbyRoster.Instance`:** `Count`, `this[i]`, `TryGetLocal`, `FirmSize`, `ReadyCount`, `Rules`, `RunInProgress`, `Evaluate(out StartBlock)`, `FirmProblem()`, `SmallerFirm()`, `StepFor`, `IsLocalHost`.
- **Events:** `LobbyRoster.OnChanged`, `OnArrived`, `OnReadyChanged`, `OnFirmChosen`, `OnFirmRefused`, `OnStartBlocked`, `OnStartNeedsConfirm`, `OnRunStarting`, `OnKicked`, `LocalRemovedByHost`, static `RunStartRequested` (host); `ReceptionDesk.OnConfirmArmed`; `Secretary.LineSpoken`; `LobbyInteractor.OnTargetChanged`, `OnUsed`.
- **Requests:** `RequestReady`, `RequestFirm`, `RequestFitting`, `RequestStart`, `RequestKick`.

## Files and setup

- Scripts under [Assets/_Project/Scripts/Lobby/](../../Assets/_Project/Scripts/Lobby/): `LobbyRoster.cs`, `LobbyInteractor.cs`, `LobbyInteractable.cs`, `ReceptionDesk.cs`, `FirmSignIn.cs`, `Secretary.cs`, `SecretaryIdle.cs`, `SecretaryLines.cs`, `LobbyHud.cs`, `LobbyInput.cs`, `LobbyRules.cs`, `LobbySounds.cs`, `LobbyHostControls.cs`, `LobbyHostPanel.cs`, `LobbyRemovedNotice.cs`.
- Tuning: `Assets/_Project/Profiles/Customisation/LobbyRules.asset`, `SecretaryLines.asset`.
- Scene setup: **Close the Deal > Lanes > Set Up Lobby** ([LobbyLaneSetup.cs](../../Assets/_Project/Editor/Lanes/LobbyLaneSetup.cs)) builds `Assets/_Project/Scenes/Lanes/Lane_Lobby.unity`.

## Known limitations

- Kicked players can rejoin immediately (banning requires persistent Steam ID tracking).
- `TeamSpawner` ignores lobby firms while waiting in reception; `LobbyRunFlow.HostStartRun` applies lobby firm choices when the match starts ([INTEGRATION.md](INTEGRATION.md)).
- One shared reception room is used (owner preference over per-firm ground floors).
- Interact is **F** (gamepad north button), matching shared actions where **E** is lean-right; host panel gamepad binding is **D-pad Up hold (0.5 s)**.
- Fonts use stand-in Liberation Sans; the secretary reuses the player STDMC model with idle breathing only (no seated desk animation or glasses).
- Lobby remains joinable after a match starts (matching netcode behaviour).
