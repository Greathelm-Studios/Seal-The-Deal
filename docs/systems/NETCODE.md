# Netcode

**Status:** Current (tested in Editor/local mode; two-PC Steam session not yet observed) · **Last updated:** 2026-11-21

## What the player sees

One player presses **Host game**. A Steam lobby is created and the game starts a server on their PC.
A friend joins in one of three ways, none of them a code: the host presses **Invite with the Steam overlay** and picks them, they pick **Join Game** on the host's name in their Steam friends list or profile, or they accept an invite in Steam chat. If their game is closed, Steam launches it straight into the lobby. Their game enters the lobby, reads the host's Steam ID from it, and connects.
Up to **8** players fit in one lobby (the lobby's Max Players; it also sets the transport's cap when hosting starts). Players walk, jump, climb ledges, shoot, and throw each other through windows (see [PLAYER_MOVEMENT.md](PLAYER_MOVEMENT.md)).
**Esc** brings the menu back; **Leave game** disconnects and leaves the lobby. The menu itself is in [MENU_AND_HUD.md](MENU_AND_HUD.md).

The lobby is **friends-only**: players must be Steam friends, or Steam refuses the join and the menu says why. A friend on a different game version is turned away with a message saying who needs to update.
The overlay invite button only appears when Steam's overlay is active, which it is not in the Unity editor or in a build started outside Steam; the friends-list route works everywhere.
Every failure reports what happened in the menu's status line: a host that cannot be reached (the join gives up after 15 s), a host who closed the game, or a connection that dropped. Each sends the player back to the menu with the lobby cleaned up.

Stack: **FishNet 4.7.3** (the pinned tag; its own version strings still say 4.7.2) with the forked **FishySteamworks 4.1.1** transport over **Steamworks.NET 2025.164.1**. Steam connects two players directly when both allow it and relays through its own network otherwise; either way no ports are opened. Relay access is warmed up at launch so the first Host or Join does not wait for it.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Steam lobby | Steam; the host is its owner | Lobby data: `host` = host's SteamID64, `ver` = game version | On change |
| Rich presence | Each player, about themselves | `connect` (what Join Game does), `steam_player_group`, `steam_player_group_size`, `status` | On lobby events |
| Server | Host's PC | None (host authority) | 60 ticks/s; FishNet also steps physics per tick |
| Player body | **Host.** Spawned by the host, owned by the connecting client, which predicts its own moves | Inputs up (~30 B), host state down (~80 B) | Every tick |
| Knockback | Host only | Inside the body state | On hit |
| Props (furniture) | **Host** simulates; clients hold them kinematic | Position and rotation, 13 bytes per prop, through [Switchboard](SWITCHBOARD.md) | While moving: up to 30/s nearby, ~3/s far, capped at 12 KB/s per player |
| Towers | Host picks a seed; everyone builds locally; see [TOWER.md](TOWER.md) | One number | Once, and per rematch |
| Match, firms, the briefcase | **Host**; see [MATCH.md](MATCH.md) | Phase, briefcase status and checkpoint SyncVars; loose briefcase transform | On change; briefcase 30/s only while moving |

## What is FishNet's and what is ours

FishNet is the plumbing: connections, spawning, ownership, synced values, remote calls, and the predicted player body. We keep it (see [DECISIONS.md](../DECISIONS.md)) because prediction, join/spawn lifecycle, and reliability are the expensive parts of netcode and every gameplay system already sits on them.

Where Seal the Deal has specific bandwidth or authority requirements, we write our own layer on top behind our own interfaces:

- **Props:** [Switchboard](SWITCHBOARD.md), a budgeted unreliable pose stream per client that favours visible floors.
- **Voice:** [Voice](VOICE.md), custom proximity VOIP over FishNet broadcasts using Opus and RNNoise.
- **Target, not built:** choosing the best-connected lobby member as host before a run starts.

## Transports and lifecycle

Both transports sit inside a **Multipass** on the NetworkManager: FishySteamworks first, Tugboat (direct connection) second, with server actions per transport rather than global. FishNet only wires up the transport it has when it starts, so `SteamLobby` picks Steam or local inside Multipass per session (`UseTransport`) before starting rather than swapping transports afterwards, and the Steam server is never started when Steam is not running. **Set Up Scene** builds this; a scene from before it shows "Network setup is out of date" in the menu.

Key lifecycle and hardening rules in `SteamService`, `SteamLobby`, and our `FishySteamworks` fork:

- **Static reset for no-domain-reload Play Mode:** `SteamService` resets `IsReady` and `_instance` via `SubsystemRegistration`. `SteamService.Initialise` warms up `SteamNetworkingUtils.InitRelayNetworkAccess()` on startup so the first Host or Join does not stall waiting for relay measurement.
- **Host capacity accounting:** FishySteamworks keeps the host's own local client off its remote client cap, so `SteamLobby` passes `_maxPlayers - 1` to FishySteamworks and `_maxPlayers` to Tugboat.
- **Connecting cancellation and cleanup:** `FishNet.ClientManager.Started` stays false while a client is still connecting, so `SteamLobby.Drop()` also checks `_multipass.GetConnectionState(false) != LocalConnectionState.Stopped` to abort an in-flight join, tracks cancelled lobby creates/joins (`_abandonedLobby`) so late Steam callbacks leave the lobby immediately, and leaves any active lobby plus clears rich presence in `OnDestroy()` and `OnApplicationQuit()`.
- **Peer disconnect filtering:** `SteamLobby.OnLinkChanged` checks `cb.m_info.m_identityRemote.GetSteamID() == _hostId` so Steam connection events from unrelated peers cannot misclassify a host disconnect.

### FishySteamworks fork patches

Upstream FishySteamworks 4.1.1 is unmaintained, so we maintain a fork in [Assets/_Project/Plugins/FishySteamworks/](../../Assets/_Project/Plugins/FishySteamworks/) (full details in [FORK.md](../../Assets/_Project/Plugins/FishySteamworks/FORK.md)):

1. **`CommonSocket.Send`:** Copies `segment` and trailing `channelId` into a reusable persistently pinned `_outboundBuffer` (grown on demand, freed on stop/finalization). Fixes the upstream full-buffer channel byte bug (triggered by FishNet's latency simulator) and eliminates per-packet `GCHandle.Alloc`, `Array.Resize`, and caller-buffer mutation.
2. **`CommonSocket.GetMessage`:** Reads `m_pData` (`Marshal.ReadIntPtr`) and `m_cbSize` (`Marshal.ReadInt32`) directly without `Marshal.PtrToStructure`, drops and releases empty or over-MTU messages with a 5 s rate-limited warning so a malformed packet cannot throw in the receive loop and stall all other connections.
3. **`BidirectionalDictionary`:** Enforces 1:1 mappings in `Add`, adds `Clear()`, and returns concrete `Dictionary<,>.KeyCollection` on `FirstTypes`/`SecondTypes` to avoid 60 Hz enumerator boxing.
4. **`ServerSocket.StopConnection`:** Closes all active `HSteamNetConnection` handles, clears connection/Steam ID maps and `_clientHostIncoming`, and frees the pinned outbound buffer.
5. **`ServerSocket.IterateIncoming` & `ReceivedFromClientHost`:** Returns host-local `ByteArrayPool` buffers after dispatch and on early return when `!_clientHostStarted`.
6. **`ServerSocket.IterateOutgoing`:** Iterates `_steamConnections.First.Keys` directly to eliminate 60 Hz enumerator boxing.
7. **`FishySteamworks.GetConnectionState(false)`:** Reports `_clientHost` state whenever acting as host.
8. **Lazy receive buffers (`CommonSocket.EnsureReceiveBuffers`):** Skips the 1 MB `InboundBuffer` and `MessagePointers` allocation on `ClientHostSocket` and inactive sockets, allocating them only in `ServerSocket`/`ClientSocket.StartConnection`.
9. **`ClientSocket`:** Removed the unused foreground `_timeoutThread` / `CheckTimeout` that called `Thread.Abort()` (`SteamLobby` runs its 15 s timeout on the main thread).

## Scripts

All under [Assets/_Project/Scripts/](../../Assets/_Project/Scripts/):

- `Net/SteamService.cs`: Starts Steam on launch, pumps callbacks every frame, warms up relay network access, and shuts down on quit. Missing Steam is a warning, not an error: the lobby falls back to local test mode.
- `Net/SteamLobby.cs`: Host, join local, invite, leave. Creates or joins the Steam lobby, starts FishNet's server and/or client on the chosen transport, publishes rich presence, handles `+connect_lobby` launch arguments, checks game version on entry, enforces the 15 s connect timeout, and maps Steam failures to plain messages.
- `UI/GameMenu.cs`: The menu and HUD; see [MENU_AND_HUD.md](MENU_AND_HUD.md).
- `Player/`, `Combat/`: The predicted body and knockback; owned by [PLAYER_MOVEMENT.md](PLAYER_MOVEMENT.md) and [WEAPONS.md](WEAPONS.md).

Scene wiring is built by **Seal the Deal > Greybox > Set Up Scene** ([GreyboxSceneSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxSceneSetup.cs)). It only adds what is missing and never rebuilds anything already in the scene.

## Known limitations

- **If the host leaves, the run ends.** No host migration; this is by design (see the GDD).
- **No rejoin.** A dropped client has to join again from the friends list or a fresh invite.
- Steam runs under Valve's test app id **480** until we own an app id. Steam shows the game as "Spacewar", and Join Game from the friends list launches whatever is installed as app 480, so test the launch-from-Steam path only once we have our own id.
- Lobbies are friends-only; there is no public list or matchmaking (out of scope for v1).
- The lobby stays joinable during a run if nothing explicitly closes it in match flow; check [MATCH.md](MATCH.md) for current match-phase gates.
- Two-PC playtesting over Steam has not yet been recorded against this fork.

## Testing it

### Over Steam, two PCs

- Steam running and signed in on both PCs, and the two accounts are **Steam friends**.
- Both on the same commit, opening the active playtest scene in Unity 6000.4.8f1 (or a build of it).
- Host: Play, **Host game**. Friend: Play, then right-click the host in the Steam friends list and pick **Join Game** (or accept the host's invite). Both games must already be open while using app id 480.
- `steam_appid.txt` (containing `480`) must sit next to the executable or in the project root. Steamworks.NET writes it into the project root the first time the editor opens; it is git-ignored and must never ship.

### On one PC, without Steam: local test mode

Two copies of the game on the same PC connect directly instead of through Steam. The lobby uses this when Steam is not running, when the game is launched with `-local`, or when **Force Local Test Mode** is ticked on `SteamLobby`.

1. Build the game (File > Build Profiles > Windows) from the same commit as the editor.
2. In the editor press Play, then **Host game**.
3. Launch the build with `-local` (or with Steam closed) and press **Join game on this PC**.

Everything after joining (movement, blasts, corrections) uses the real network path over loopback, so it shows logic bugs but not lag.

### What to watch

On the debug readout, the Switchboard line shows prop upload in kB/s summed over all clients (it should never pass 12 times the number of clients), updates per second, and moving props on host (or download, updates per second, and smoothing delay in ms on a client).
The bottom-left readout shows the local body's state, speed, ping, and host correction rate/distance. Corrections of a few centimetres are normal; repeated corrections of half a metre or more while walking on flat ground mean prediction and host disagree.
