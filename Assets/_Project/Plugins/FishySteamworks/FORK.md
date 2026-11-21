# FishySteamworks fork

This is FirstGearGames' FishySteamworks **4.1.1** (the release `.unitypackage`, last upstream commit Aug 2024), moved here from `Assets/FishNet/Plugins/` on 2026-10-03 because upstream is unmaintained and it needed a fix. Every change from upstream is listed below and marked `// Fork patch: ... (see FORK.md)` at the code site, so a future upstream release can be diffed against this folder.

Upstream: https://github.com/FirstGearGames/FishySteamworks

## Patches

1. **`Core/CommonSocket.cs`, `Send`**: when an outgoing packet exactly fills its buffer, the original resized a copy of the array, wrote the channel byte into the copy, then built the segment from the original. A full buffer either threw `ArgumentException` or went out with a stale channel byte. FishNet's latency simulator hands the transport exactly-full buffers, so it hit on every packet. Upstream issue: https://github.com/FirstGearGames/FishySteamworks/issues/19. Now copies `segment` + trailing `channelId` into a reusable per-socket `_outboundBuffer` (grown on demand) with a single persistently pinned `GCHandle` (freed on socket stop/finalization), avoiding per-packet `GCHandle.Alloc`, `Array.Resize`, and caller-buffer mutation.

2. **`Core/CommonSocket.cs`, `GetMessage`** (and its two callers in `ServerSocket.IterateIncoming` and `ClientSocket.IterateIncoming`): the original copied a received Steam message of any size into the MTU-sized receive buffer and read the channel from its last byte. An empty message threw `IndexOutOfRangeException`; one larger than the buffer (Steam accepts reliable messages up to 512 KB) threw `ArgumentException`. Either was thrown inside the receive loop, every frame, so a single modified peer stopped the host reading every connection after its own and leaked the unreleased Steam messages. `GetMessage` now reads `m_pData` (`Marshal.ReadIntPtr(ptr)`) and `m_cbSize` (`Marshal.ReadInt32(ptr, IntPtr.Size)`) directly instead of `Marshal.PtrToStructure<SteamNetworkingMessage_t>(ptr)`, drops and releases a message outside 1..buffer length, logs a warning (rate-limited to once per 5 seconds with a suppressed count so a hostile peer cannot flood the log), and returns false; the callers skip it. FishNet keeps honest packets at least two bytes under the MTU, so nothing honest is dropped.

3. **`Core/BidirectionalDictionary.cs`, `Add`, `Clear`, `FirstTypes`/`SecondTypes`**: both `Add(T1, T2)` and `Add(T2, T1)` remove any existing reverse-map entry for `value` before inserting, keeping the forward and reverse maps 1:1. Added `Clear()` and changed `FirstTypes`/`SecondTypes` to return concrete `Dictionary<,>.KeyCollection` instead of `IEnumerable<>` to prevent enumerator boxing.

4. **`Core/ServerSocket.cs`, `StopConnection()`**: closes all active `HSteamNetConnection` handles in `_steamConnections.First.Keys` via `CloseConnection(..., 0, string.Empty, false)`, clears `_steamConnections`, `_steamIds`, `_cachedConnectionIds`, and `_clientHostIncoming` (`ClearQueue`), and frees the pinned outbound buffer.

5. **`Core/ServerSocket.cs`, `IterateIncoming()` & `ReceivedFromClientHost()`**: calls `ByteArrayPool.Store(packet.Data)` after dispatching host-local packets and on early return when `!_clientHostStarted`, stopping pooled buffer leaks.

6. **`Core/ServerSocket.cs`, `IterateOutgoing()`**: iterates `_steamConnections.First.Keys` directly to eliminate 60 Hz enumerator boxing.

7. **`FishySteamworks.cs`, `GetConnectionState(false)`**: returns `_clientHost.GetLocalConnectionState()` whenever it is not `LocalConnectionState.Stopped`, so host-client state is reported accurately.

8. **`Core/CommonSocket.cs`, `Core/ServerSocket.cs`, `Core/ClientSocket.cs` (`EnsureReceiveBuffers`)**: skipped the 1 MB `InboundBuffer` and `MessagePointers` allocation on `ClientHostSocket` (and inactive sockets) by lazily allocating receive buffers inside `ServerSocket.StartConnection` and `ClientSocket.StartConnection`.

9. **`Core/ClientSocket.cs`**: removed the unused foreground `_timeoutThread` / `CheckTimeout` that called `Thread.Abort()` (`SteamLobby` runs its own 15 s timeout on the main thread).
