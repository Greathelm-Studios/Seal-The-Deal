using System;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using Steamworks;
using UnityEngine;

namespace CloseTheDeal.Net
{
    /// <summary>Why hosting, joining or a game ended badly. Kept until the player acts on it (LobbyProblem.None = nothing wrong).</summary>
    public enum LobbyProblem
    {
        None = 0,
        /// <summary>The scene's network setup is broken: nothing can be hosted or joined.</summary>
        NetworkBroken,
        /// <summary>Steam would not create a lobby (detail: Steam's result code).</summary>
        LobbyCreateFailed,
        /// <summary>The lobby no longer exists.</summary>
        GameClosed,
        /// <summary>The lobby is friends-only and the host isn't a friend.</summary>
        FriendsOnly,
        /// <summary>Every seat is taken.</summary>
        Full,
        /// <summary>Steam refused entry for another reason (detail: Steam's response).</summary>
        Refused,
        /// <summary>The host runs another version (detail: theirs).</summary>
        VersionMismatch,
        /// <summary>The host's game could not be reached, or never answered within 15 s.</summary>
        Unreachable,
        /// <summary>The host left or closed the game.</summary>
        HostClosed,
        /// <summary>The host dropped out of the lobby.</summary>
        HostDropped,
        /// <summary>Our link to the host was lost mid-game.</summary>
        ConnectionLost
    }

    /// <summary>Where hosting or joining has got to, for the UI's waiting screen.</summary>
    public enum LobbyStage
    {
        /// <summary>Not hosting or joining.</summary>
        Idle = 0,
        /// <summary>Asking Steam for a lobby.</summary>
        CreatingLobby,
        /// <summary>Hosting (server up).</summary>
        Hosting,
        /// <summary>Entering a friend's Steam lobby.</summary>
        JoiningSteam,
        /// <summary>Connecting to the host's game (the 15 s clock runs).</summary>
        Connecting,
        /// <summary>Connected to the host; waiting for the game to put us in.</summary>
        Connected
    }

    /// <summary>Manages Steam lobbies, rich presence, and FishNet Multipass transport selection.</summary>
    [DisallowMultipleComponent]
    public sealed class SteamLobby : MonoBehaviour
    {
        [Tooltip("How many players one lobby holds, host included. 8 = two teams of up to 4.")]
        [SerializeField] int _maxPlayers = 8;

        [Tooltip("Tick to ignore Steam and connect directly on this PC, for testing an editor against a build. Off for anything a friend joins.")]
        [SerializeField] bool _forceLocalTestMode;

        /// <summary>Human-readable state for the menu. Raised only when something changes.</summary>
        public event Action<string> OnStatus;

        /// <summary>Raised whenever the lobby's membership or our connection changes, so UI can re-read state.</summary>
        public event Action OnChanged;

        public CSteamID LobbyId { get; private set; } = CSteamID.Nil;

        /// <summary>True when connecting directly on this PC instead of through Steam.</summary>
        public bool LocalMode { get; private set; }

        /// <summary>False until Start has decided the mode, or if the network setup is broken.</summary>
        public bool Ready { get; private set; }

        public bool InLobby => LobbyId.IsValid();

        /// <summary>True when hosting, joining, or joined via either transport.</summary>
        public bool IsActive => _creatingLobby || _joiningSteam || _joined || InLobby
            || (_net != null && (_net.ServerManager.Started || _net.ClientManager.Started));

        public bool IsHosting => _net != null && _net.ServerManager.Started;

        public bool IsLobbyOwner => InLobby && SteamMatchmaking.GetLobbyOwner(LobbyId) == SteamService.LocalId;

        public int MaxPlayers => _maxPlayers;

        /// <summary>Players in the Steam lobby or connected to this session.</summary>
        public int PlayerCount
        {
            get
            {
                if (InLobby)
                    return SteamMatchmaking.GetNumLobbyMembers(LobbyId);
                if (IsHosting)
                    return _net.ServerManager.Clients.Count;
                return _net != null && _net.ClientManager.Started ? _net.ClientManager.Clients.Count : 0;
            }
        }

        /// <summary>What went wrong last, kept until the player acts on it (ClearProblem) or starts something new.</summary>
        public LobbyProblem Problem { get; private set; }

        /// <summary>The words that go with the problem (Steam's code, the host's version); may be empty.</summary>
        public string ProblemDetail { get; private set; } = string.Empty;

        /// <summary>The host's name as it was when the problem happened ("the host" when unknown).</summary>
        public string ProblemHost { get; private set; } = "the host";

        /// <summary>Where hosting or joining has got to.</summary>
        public LobbyStage Stage
        {
            get
            {
                if (_creatingLobby)
                    return LobbyStage.CreatingLobby;
                if (IsHosting)
                    return LobbyStage.Hosting;
                if (_joined)
                    return _connected ? LobbyStage.Connected : LobbyStage.Connecting;
                return _joiningSteam ? LobbyStage.JoiningSteam : LobbyStage.Idle;
            }
        }

        /// <summary>While connecting: seconds since it started and how long it waits before giving up (honest progress).</summary>
        public float ConnectElapsed => _connectDeadline < 0f ? 0f : ConnectTimeoutSeconds - (_connectDeadline - Time.unscaledTime);

        public float ConnectTimeout => ConnectTimeoutSeconds;

        /// <summary>The host's name for the waiting screen ("the host" when unknown, the PC's game in local mode).</summary>
        public string HostDisplayName => HostName();

        /// <summary>True when Try again can repeat the last join (a lobby we tried, or the game on this PC).</summary>
        public bool CanRetryJoin => Ready && !IsActive && (LocalMode ? _lastJoinLocal : _lastLobby.IsValid());

        /// <summary>True when Steam's overlay can open the friends list (not in the Editor or a build started outside Steam).</summary>
        public bool CanOpenFriends => !LocalMode && SteamService.IsReady && SteamUtils.IsOverlayEnabled();

        /// <summary>True when Steam's overlay invite dialog is available.</summary>
        public bool CanInviteViaOverlay => !LocalMode && InLobby && SteamService.IsReady && SteamUtils.IsOverlayEnabled();

        const string HostKey = "host";
        const string VersionKey = "ver";
        const string ConnectLobbyArg = "+connect_lobby";
        const string LocalArg = "-local";
        const string LocalAddress = "localhost";

        const string ConnectKey = "connect";
        const string GroupKey = "steam_player_group";
        const string GroupSizeKey = "steam_player_group_size";
        const string PresenceStatusKey = "status";

        const float ConnectTimeoutSeconds = 15f;

        NetworkManager _net;
        Multipass _multipass;
        int _transportIndex = -1;
        CSteamID _hostId = CSteamID.Nil;
        float _connectDeadline = -1f;
        bool _joined;
        bool _connected;
        bool _leaving;

        bool _creatingLobby;
        CSteamID _abandonedLobby = CSteamID.Nil;
        CSteamID _lastLobby = CSteamID.Nil;
        bool _lastJoinLocal;
        bool _joiningSteam;
        bool _hostClosedLink;
        Callback<LobbyCreated_t> _lobbyCreated;
        Callback<LobbyEnter_t> _lobbyEntered;
        Callback<GameLobbyJoinRequested_t> _joinRequested;
        Callback<GameRichPresenceJoinRequested_t> _presenceJoinRequested;
        Callback<LobbyChatUpdate_t> _lobbyChanged;
        Callback<SteamNetConnectionStatusChangedCallback_t> _linkChanged;

        void Start()
        {
            _net = InstanceFinder.NetworkManager;
            _multipass = _net != null ? _net.TransportManager.Transport as Multipass : null;
            if (_multipass == null)
            {
                Debug.LogError("[Lobby] The NetworkManager needs a Multipass transport holding FishySteamworks and Tugboat. Run Seal the Deal > Greybox > Set Up Scene.");
                SetProblem(LobbyProblem.NetworkBroken, "Multipass");
                Status(BrokenNetworkLine("Network setup is out of date. Run Seal the Deal > Greybox > Set Up Scene."));
                return;
            }

            _net.ClientManager.OnClientConnectionState += OnClientConnectionState;
            _net.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;

            LocalMode = _forceLocalTestMode || HasArg(LocalArg) || !SteamService.IsReady;
            if (!UseTransport(LocalMode))
                return;

            Ready = true;
            if (LocalMode)
            {
                Status("Ready. Host a game, or join one running on this PC.");
                return;
            }

            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            _presenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnPresenceJoinRequested);
            _lobbyChanged = Callback<LobbyChatUpdate_t>.Create(OnLobbyChanged);
            _linkChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnLinkChanged);

            Status("Ready. Host a game, or join a friend from Steam.");
            JoinFromCommandLine();
        }

        void Update()
        {
            if (_connectDeadline < 0f || Time.unscaledTime < _connectDeadline)
                return;

            _connectDeadline = -1f;
            Fail(LobbyProblem.Unreachable, string.Empty, UnreachableReason(HostName()));
        }

        void OnApplicationQuit()
        {
            if (!SteamService.IsReady)
                return;

            if (InLobby)
            {
                SteamMatchmaking.LeaveLobby(LobbyId);
                LobbyId = CSteamID.Nil;
            }
            SteamFriends.ClearRichPresence();
        }

        void OnDestroy()
        {
            if (SteamService.IsReady && InLobby)
            {
                SteamMatchmaking.LeaveLobby(LobbyId);
                SteamFriends.ClearRichPresence();
                LobbyId = CSteamID.Nil;
            }

            if (_net != null)
            {
                _net.ClientManager.OnClientConnectionState -= OnClientConnectionState;
                _net.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            }

            _lobbyCreated?.Dispose();
            _lobbyEntered?.Dispose();
            _joinRequested?.Dispose();
            _presenceJoinRequested?.Dispose();
            _lobbyChanged?.Dispose();
            _linkChanged?.Dispose();
        }

        public void Host()
        {
            if (!Ready || IsActive)
                return;

            ClearProblem();
            if (LocalMode)
            {
                StartHosting();
                Status(HostingStatus());
                return;
            }

            Status("Opening a lobby on Steam...");
            _creatingLobby = true;
            OnChanged?.Invoke();
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, _maxPlayers);
        }

        /// <summary>Local test mode: join the host running on this PC.</summary>
        public void JoinLocal()
        {
            if (!Ready || !LocalMode || IsActive)
                return;

            ClearProblem();
            _lastJoinLocal = true;
            StartClient(LocalAddress);
            Status("Joining the game on this PC...");
        }

        public void Invite()
        {
            if (CanInviteViaOverlay)
                SteamFriends.ActivateGameOverlayInviteDialog(LobbyId);
        }

        public void Leave() => Drop("You left the game.");

        /// <summary>Repeats the last join: the same Steam lobby, or the game on this PC. Refused (false) when there is none.</summary>
        public bool RetryJoin()
        {
            if (!CanRetryJoin)
                return false;
            if (LocalMode)
                JoinLocal();
            else
                JoinLobby(_lastLobby);
            return true;
        }

        /// <summary>The player has read the problem (Back): it is forgotten, and the title's status says the game is ready.</summary>
        public void ClearProblem()
        {
            if (Problem == LobbyProblem.None || Problem == LobbyProblem.NetworkBroken)
                return;
            Problem = LobbyProblem.None;
            ProblemDetail = string.Empty;
            OnChanged?.Invoke();
        }

        /// <summary>Opens Steam's friends list in the overlay (to add the host as a friend, or join one).</summary>
        public void OpenFriends()
        {
            if (CanOpenFriends)
                SteamFriends.ActivateGameOverlay("friends");
        }

        void SetProblem(LobbyProblem problem, string detail)
        {
            Problem = problem;
            ProblemDetail = detail ?? string.Empty;
            ProblemHost = HostName();
        }

        void Fail(LobbyProblem problem, string detail, string status)
        {
            SetProblem(problem, detail);
            Drop(status);
        }

        void Drop(string status)
        {
            if (_net == null)
                return;

            _leaving = true;
            _creatingLobby = false;
            _connectDeadline = -1f;
            _joined = false;
            _connected = false;
            if (_joiningSteam && _lastLobby.IsValid() && !InLobby)
                _abandonedLobby = _lastLobby; // Steam may still answer the join: that answer is ignored (and left)
            _joiningSteam = false;

            // Started is false while still connecting, and that attempt must stop too.
            if (_net.ClientManager.Started || (_multipass != null && _multipass.GetConnectionState(false) != LocalConnectionState.Stopped))
                _net.ClientManager.StopConnection();
            if (_net.ServerManager.Started)
                _multipass.StopServerConnection(true, _transportIndex);

            if (InLobby)
                SteamMatchmaking.LeaveLobby(LobbyId);
            if (SteamService.IsReady)
                SteamFriends.ClearRichPresence();

            LobbyId = CSteamID.Nil;
            _hostId = CSteamID.Nil;
            _leaving = false;
            Status(status);
            OnChanged?.Invoke();
        }

        bool UseTransport(bool local)
        {
            Transport transport = local
                ? _multipass.GetTransport<FishNet.Transporting.Tugboat.Tugboat>()
                : _multipass.GetTransport<global::FishySteamworks.FishySteamworks>();

            if (transport == null)
            {
                string missing = local ? "Tugboat" : "FishySteamworks";
                Debug.LogError($"[Lobby] Multipass has no {missing}. Run Seal the Deal > Greybox > Set Up Scene.");
                SetProblem(LobbyProblem.NetworkBroken, missing);
                Status(BrokenNetworkLine($"Network setup is missing {missing}. Run Seal the Deal > Greybox > Set Up Scene."));
                return false;
            }

            _transportIndex = transport.Index;
            _multipass.SetClientTransport(_transportIndex);
            return true;
        }

        void StartHosting()
        {
            _multipass.StartConnection(true, _transportIndex);
            // FishySteamworks excludes the host client from its cap; Tugboat includes it.
            _multipass.SetMaximumClients(LocalMode ? _maxPlayers : _maxPlayers - 1, _transportIndex);
            _net.ClientManager.StartConnection();
            OnChanged?.Invoke();
        }

        void StartClient(string address)
        {
            _joined = true;
            _connected = false;
            _hostClosedLink = false;
            _connectDeadline = Time.unscaledTime + ConnectTimeoutSeconds;
            _multipass.SetClientAddress(address, _transportIndex);
            _net.ClientManager.StartConnection();
            OnChanged?.Invoke();
        }

        void OnLobbyCreated(LobbyCreated_t cb)
        {
            // False when the player left while Steam was still creating it: leave that lobby.
            bool wanted = _creatingLobby;
            _creatingLobby = false;
            if (cb.m_eResult != EResult.k_EResultOK)
            {
                if (wanted)
                {
                    SetProblem(LobbyProblem.LobbyCreateFailed, cb.m_eResult.ToString());
                    Status("Steam could not open a lobby. Check that Steam is online, then host again.");
                }
                OnChanged?.Invoke();
                return;
            }

            if (!wanted)
            {
                _abandonedLobby = new CSteamID(cb.m_ulSteamIDLobby);
                SteamMatchmaking.LeaveLobby(_abandonedLobby);
                // Steam may have delivered this lobby's enter callback first.
                if (LobbyId == _abandonedLobby)
                {
                    LobbyId = CSteamID.Nil;
                    _hostId = CSteamID.Nil;
                    SteamFriends.ClearRichPresence();
                    OnChanged?.Invoke();
                }
                return;
            }

            LobbyId = new CSteamID(cb.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(LobbyId, HostKey, SteamService.LocalId.ToString());
            SteamMatchmaking.SetLobbyData(LobbyId, VersionKey, Application.version);

            StartHosting();
            Status(HostingStatus());
        }

        void OnLobbyEntered(LobbyEnter_t cb)
        {
            if (cb.m_ulSteamIDLobby == _abandonedLobby.m_SteamID)
            {
                if ((EChatRoomEnterResponse)cb.m_EChatRoomEnterResponse == EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                    SteamMatchmaking.LeaveLobby(_abandonedLobby);
                return;
            }

            var response = (EChatRoomEnterResponse)cb.m_EChatRoomEnterResponse;
            if (response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                _joiningSteam = false;
                _hostId = CSteamID.Nil;
                SetProblem(ProblemFor(response), response.ToString());
                Status(JoinFailureReason(response));
                OnChanged?.Invoke();
                return;
            }

            LobbyId = new CSteamID(cb.m_ulSteamIDLobby);
            _hostId = ReadHostId();

            // Steam raises this for the host as well; the host is already connected to itself.
            if (IsLobbyOwner)
            {
                PublishPresence();
                OnChanged?.Invoke();
                return;
            }

            string theirs = SteamMatchmaking.GetLobbyData(LobbyId, VersionKey);
            if (theirs != Application.version)
            {
                string host = HostName();
                SteamMatchmaking.LeaveLobby(LobbyId);
                LobbyId = CSteamID.Nil;
                SetProblem(LobbyProblem.VersionMismatch, theirs ?? string.Empty);
                ProblemHost = host;
                _hostId = CSteamID.Nil;
                _joiningSteam = false;
                Status(VersionMismatchReason(host, theirs));
                OnChanged?.Invoke();
                return;
            }

            PublishPresence();
            _joiningSteam = false;
            StartClient(_hostId.m_SteamID.ToString());
            Status($"Connecting to {HostName()}'s game...");
        }

        void OnJoinRequested(GameLobbyJoinRequested_t cb)
        {
            JoinLobby(cb.m_steamIDLobby);
        }

        void OnPresenceJoinRequested(GameRichPresenceJoinRequested_t cb)
        {
            if (TryParseConnectLobby(cb.m_rgchConnect.Split(' '), out CSteamID lobby))
                JoinLobby(lobby);
        }

        void JoinLobby(CSteamID lobby)
        {
            if (IsActive)
                Leave();

            ClearProblem();
            if (_abandonedLobby == lobby)
                _abandonedLobby = CSteamID.Nil; // joining it again on purpose
            _lastLobby = lobby;
            _lastJoinLocal = false;
            _joiningSteam = true;
            Status("Joining from Steam...");
            OnChanged?.Invoke();
            SteamMatchmaking.JoinLobby(lobby);
        }

        void OnLobbyChanged(LobbyChatUpdate_t cb)
        {
            if (cb.m_ulSteamIDLobby != LobbyId.m_SteamID)
                return;

            uint change = cb.m_rgfChatMemberStateChange;
            bool hostGone = !IsHosting
                && cb.m_ulSteamIDUserChanged == _hostId.m_SteamID
                && (change & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) == 0;
            if (hostGone)
            {
                bool closed = (change & (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft) != 0;
                string host = HostName();
                if (closed)
                    Fail(LobbyProblem.HostClosed, string.Empty, $"{host} closed the game. The run has ended.");
                else
                    Fail(LobbyProblem.HostDropped, string.Empty, $"Lost {host}'s game. Their connection dropped.");
                return;
            }

            PublishPresence();
            OnChanged?.Invoke();
        }

        void OnLinkChanged(SteamNetConnectionStatusChangedCallback_t cb)
        {
            if (IsHosting)
                return;
            if (!_hostId.IsValid() || cb.m_info.m_identityRemote.GetSteamID() != _hostId)
                return;

            ESteamNetworkingConnectionState state = cb.m_info.m_eState;
            if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer)
                _hostClosedLink = true;
            else if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
                _hostClosedLink = false;
        }

        CSteamID ReadHostId()
        {
            string hostId = SteamMatchmaking.GetLobbyData(LobbyId, HostKey);
            if (ulong.TryParse(hostId, out ulong id))
                return new CSteamID(id);
            return SteamMatchmaking.GetLobbyOwner(LobbyId);
        }

        void PublishPresence()
        {
            if (!InLobby || LocalMode)
                return;

            string lobby = LobbyId.m_SteamID.ToString();
            SteamFriends.SetRichPresence(ConnectKey, ConnectLobbyArg + " " + lobby);
            SteamFriends.SetRichPresence(GroupKey, lobby);
            SteamFriends.SetRichPresence(GroupSizeKey, PlayerCount.ToString());
            SteamFriends.SetRichPresence(PresenceStatusKey, $"In {HostName()}'s lobby ({PlayerCount}/{_maxPlayers})");
        }

        static string JoinFailureReason(EChatRoomEnterResponse response)
        {
            switch (response)
            {
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist:
                    return "Unable to join. That game has closed.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed:
                    return "Unable to join. The lobby is friends-only: add the host as a Steam friend first.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull:
                    return "Unable to join. The game is full.";
                default:
                    return $"Unable to join. Steam refused the request ({response}). Try again.";
            }
        }

        static LobbyProblem ProblemFor(EChatRoomEnterResponse response)
        {
            switch (response)
            {
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist: return LobbyProblem.GameClosed;
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed: return LobbyProblem.FriendsOnly;
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull: return LobbyProblem.Full;
                default: return LobbyProblem.Refused;
            }
        }

        static string BrokenNetworkLine(string forTeam) =>
            Application.isEditor || Debug.isDebugBuild ? forTeam : "Unable to start the network. Restart the game.";

        static string VersionMismatchReason(string host, string theirs)
        {
            string theirVersion = string.IsNullOrEmpty(theirs) ? "an older version" : "version " + theirs;
            return $"Unable to join. {host} is on {theirVersion}, you are on version {Application.version}. Whoever is behind needs to update.";
        }

        static string UnreachableReason(string host)
        {
            return $"Unable to reach {host}'s game. They may have left. Try joining again.";
        }

        void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    _connected = true;
                    _connectDeadline = -1f;
                    Status(ConnectedStatus());
                    break;
                case LocalConnectionState.Stopped:
                    // Our own Leave stops the client too; only an uninvited stop is news.
                    if (!_leaving && _joined && !_net.ServerManager.Started)
                    {
                        string host = HostName();
                        Fail(StoppedProblem(), string.Empty, StoppedReason(host));
                        return;
                    }
                    break;
            }

            OnChanged?.Invoke();
        }

        void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            OnChanged?.Invoke();
        }

        string StoppedReason(string host)
        {
            if (!_connected)
                return UnreachableReason(host);
            if (_hostClosedLink)
                return $"{host} closed the game. The run has ended.";
            return $"Lost the connection to {host}'s game.";
        }

        LobbyProblem StoppedProblem()
        {
            if (!_connected)
                return LobbyProblem.Unreachable;
            return _hostClosedLink ? LobbyProblem.HostClosed : LobbyProblem.ConnectionLost;
        }

        string ConnectedStatus()
        {
            if (_net.ServerManager.Started)
                return HostingStatus();

            return LocalMode ? "Connected to the game on this PC." : $"Connected to {HostName()}'s game.";
        }

        string HostingStatus()
        {
            return LocalMode
                ? "Hosting on this PC. Start a second copy and choose Join on this PC."
                : "Hosting. Invite friends, or they can choose Join Game on your name in Steam.";
        }

        void JoinFromCommandLine()
        {
            if (TryParseConnectLobby(Environment.GetCommandLineArgs(), out CSteamID lobby))
                JoinLobby(lobby);
        }

        static bool TryParseConnectLobby(string[] args, out CSteamID lobby)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != ConnectLobbyArg || !ulong.TryParse(args[i + 1], out ulong id))
                    continue;

                lobby = new CSteamID(id);
                return true;
            }

            lobby = CSteamID.Nil;
            return false;
        }

        static bool HasArg(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        string HostName()
        {
            if (!_hostId.IsValid())
                return "the host";

            return SteamFriends.GetFriendPersonaName(_hostId);
        }

        /// <summary>The last status line, for UI that opens after it was said.</summary>
        public string StatusText { get; private set; } = string.Empty;

        void Status(string text)
        {
            StatusText = text;
            Debug.Log("[Lobby] " + text);
            OnStatus?.Invoke(text);
        }
    }
}
