using System;
using System.Collections.Generic;
using CloseTheDeal.Net;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>Where a player stands in reception.</summary>
    public enum MemberState : byte
    {
        /// <summary>Walking about; not ready.</summary>
        Browsing,
        /// <summary>Readied up at the desk.</summary>
        Ready,
        /// <summary>In the fitting room. Can't be ready while there.</summary>
        Fitting
    }

    /// <summary>Why the host can't start yet.</summary>
    public enum StartBlock : byte
    {
        None,
        RunInProgress,
        NeedBothFirms,
        Unbalanced,
        NotAllReady
    }

    /// <summary>Whether the host can start the run now.</summary>
    public enum StartState : byte
    {
        /// <summary>Not yet: see the reason.</summary>
        Unavailable,
        /// <summary>Yes: one hold at the desk.</summary>
        Ready,
        /// <summary>Everyone is ready but a firm is empty or the firms are uneven: allowed, with a second hold to confirm.</summary>
        NeedsConfirm
    }

    /// <summary>What the local player should do next, for the guidance line.</summary>
    public enum LobbyStep : byte
    {
        /// <summary>Not in the roster yet.</summary>
        Arriving,
        /// <summary>Ready up at the desk.</summary>
        ReadyUp,
        /// <summary>Ready; others are not.</summary>
        WaitForOthers,
        /// <summary>The host can start.</summary>
        StartRun,
        /// <summary>The host can start short-handed after a confirm.</summary>
        StartShortHanded,
        /// <summary>Everyone is ready; the host has not started.</summary>
        WaitForHost,
        /// <summary>In the fitting room.</summary>
        Fitting,
        /// <summary>The run has started.</summary>
        RunStarting
    }

    /// <summary>One player in reception, as everyone sees it.</summary>
    public struct LobbyMember
    {
        public int ClientId;
        public string Name;
        public byte Firm;
        public MemberState State;
    }

    /// <summary>One player handed to the run.</summary>
    public readonly struct RunEntrant
    {
        public readonly int ClientId;
        public readonly int Firm;
        public readonly string Name;

        public RunEntrant(int clientId, int firm, string name)
        {
            ClientId = clientId;
            Firm = firm;
            Name = name;
        }
    }

    /// <summary>Host-authoritative reception roster, firm balance, ready state, and run-start gate. Design: docs/systems/LOBBY.md.</summary>
    [DisallowMultipleComponent]
    public sealed class LobbyRoster : NetworkBehaviour
    {
        public static LobbyRoster Instance { get; private set; }

        /// <summary>Host only. The host started the run; the list is every player and their firm. The match flow listens here.</summary>
        public static event Action<IReadOnlyList<RunEntrant>> RunStartRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            RunStartRequested = null;
            LocalRemovedByHost = null;
        }

        /// <summary>Converts entrants to a clientId-to-firm map for MatchDirector.HostStartRun (allocates once per run).</summary>
        public static Dictionary<int, int> ToFirmMap(IReadOnlyList<RunEntrant> entrants)
        {
            var map = new Dictionary<int, int>(entrants.Count);
            for (int i = 0; i < entrants.Count; i++)
                map[entrants[i].ClientId] = entrants[i].Firm;
            return map;
        }

        /// <summary>Every machine: the list changed in any way. UI re-reads it.</summary>
        public event Action OnChanged;

        /// <summary>Every machine: someone new arrived after we did.</summary>
        public event Action<LobbyMember> OnArrived;

        /// <summary>Every machine: someone's ready state changed (true = now ready).</summary>
        public event Action<LobbyMember, bool> OnReadyChanged;

        /// <summary>This player only: their sign-in went through (firm index).</summary>
        public event Action<int> OnFirmChosen;

        /// <summary>This player only: a firm switch was refused to keep the firms fair.</summary>
        public event Action<int> OnFirmRefused;

        /// <summary>The host only: a start was refused, and why.</summary>
        public event Action<StartBlock> OnStartBlocked;

        /// <summary>The host only: everyone is ready but a firm is empty or uneven, so the start needs a second, confirming hold (the reason).</summary>
        public event Action<StartBlock> OnStartNeedsConfirm;

        /// <summary>Every machine: the host removed a player (their name), raised just before they leave.</summary>
        public event Action<string> OnKicked;

        /// <summary>Every machine: the run is starting.</summary>
        public event Action OnRunStarting;

        [Tooltip("The firms, the fairness rule and the start rule.")]
        [SerializeField] LobbyRules _rules;

        const int MaxNameLength = 24;

        readonly SyncList<LobbyMember> _members = new();
        readonly SyncVar<bool> _runInProgress = new();

        bool _listening;
        float _listenAfter;
        const float InitialSyncSeconds = 1f;
        bool _nameSent;

        public LobbyRules Rules => _rules;
        public int Count => _members.Count;
        public LobbyMember this[int i] => _members[i];
        public bool RunInProgress => _runInProgress.Value;

        void Awake()
        {
            _members.OnChange += OnMembersChange;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            Instance = this;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (Instance == this)
                Instance = null;
        }

        void OnDestroy()
        {
            _members.OnChange -= OnMembersChange;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoaded;
            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;

            // Host client can load start scenes before LobbyRoster.OnStartServer runs.
            foreach (NetworkConnection conn in ServerManager.Clients.Values)
            {
                if (conn.LoadedStartScenes(true))
                    ServerAdd(conn);
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (NetworkManager != null)
                NetworkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoaded;
            if (ServerManager != null)
                ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Wait InitialSyncSeconds before treating SyncList additions as new arrivals.
            _listening = false;
            _listenAfter = Time.unscaledTime + InitialSyncSeconds;
            _nameSent = false;
            SendNameOnce();
            OnChanged?.Invoke();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _listening = false;
        }

        void SendNameOnce()
        {
            if (_nameSent || !SteamService.IsReady || !TryGetLocal(out _))
                return;

            _nameSent = true;
            CmdSetName(SteamService.LocalName);
        }

        public int IndexOf(int clientId)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].ClientId == clientId)
                    return i;
            }

            return -1;
        }

        /// <summary>This machine's own entry, if it has one yet.</summary>
        public bool TryGetLocal(out LobbyMember member)
        {
            int index = IsClientInitialized && ClientManager.Connection != null ? IndexOf(ClientManager.Connection.ClientId) : -1;
            member = index >= 0 ? _members[index] : default;
            return index >= 0;
        }

        public bool IsLocalHost => IsServerInitialized && IsClientInitialized;

        public int FirmSize(int firm)
        {
            int count = 0;
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Firm == firm)
                    count++;
            }

            return count;
        }

        public int ReadyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _members.Count; i++)
                {
                    if (_members[i].State == MemberState.Ready)
                        count++;
                }

                return count;
            }
        }

        /// <summary>Evaluates whether the host can start directly, needs a short-handed confirm, or is blocked.</summary>
        public StartState Evaluate(out StartBlock reason)
        {
            reason = StartBlock.None;
            if (_runInProgress.Value)
            {
                reason = StartBlock.RunInProgress;
                return StartState.Unavailable;
            }

            if (_members.Count == 0 || ReadyCount < _members.Count)
            {
                reason = StartBlock.NotAllReady;
                return StartState.Unavailable;
            }

            reason = FirmProblem();
            if (reason == StartBlock.None)
                return StartState.Ready;
            return _rules.AllowShortHandedStart ? StartState.NeedsConfirm : StartState.Unavailable;
        }

        /// <summary>None, NeedBothFirms or Unbalanced, whatever the ready states are.</summary>
        public StartBlock FirmProblem()
        {
            int a = FirmSize(0), b = FirmSize(1);
            if (_rules.RequireBothFirms && (a == 0 || b == 0))
                return StartBlock.NeedBothFirms;
            if (Mathf.Abs(a - b) > _rules.MaxImbalance)
                return StartBlock.Unbalanced;
            return StartBlock.None;
        }

        /// <summary>Compatibility: None only when a single hold starts the run.</summary>
        public StartBlock WhyNotStart()
        {
            StartState state = Evaluate(out StartBlock reason);
            return state == StartState.Ready ? StartBlock.None : reason;
        }

        /// <summary>The smaller firm (the one a new sign-in would help), or 0 on a tie.</summary>
        public int SmallerFirm() => FirmSize(1) < FirmSize(0) ? 1 : 0;

        /// <summary>Local player's next guidance step; call on roster changes.</summary>
        public LobbyStep StepFor(LobbyMember me, bool isHost)
        {
            if (_runInProgress.Value)
                return LobbyStep.RunStarting;
            if (me.State == MemberState.Fitting)
                return LobbyStep.Fitting;
            if (me.State != MemberState.Ready)
                return LobbyStep.ReadyUp;

            StartState state = Evaluate(out _);
            if (state == StartState.Unavailable)
                return LobbyStep.WaitForOthers;
            if (!isHost)
                return LobbyStep.WaitForHost;
            return state == StartState.Ready ? LobbyStep.StartRun : LobbyStep.StartShortHanded;
        }

        public void RequestFirm(int firm) => CmdChooseFirm((byte)firm);
        public void RequestReady(bool ready) => CmdSetReady(ready);
        public void RequestFitting(bool fitting) => CmdSetFitting(fitting);
        public void RequestStart(bool confirmed = false) => CmdStart(confirmed);

        /// <summary>The host only: remove this player from the lobby. The host checks it again; anyone else asking changes nothing.</summary>
        public void RequestKick(int clientId) => CmdKick(clientId);

        [ServerRpc(RequireOwnership = false)]
        void CmdKick(int clientId, NetworkConnection conn = null)
        {
            // Host only, never the host itself, and only someone actually in the lobby.
            if (!conn.IsHost || clientId == conn.ClientId)
                return;
            int index = IndexOf(clientId);
            if (index < 0 || !ServerManager.Clients.TryGetValue(clientId, out NetworkConnection target))
                return;

            string name = _members[index].Name;
            Debug.Log($"[Lobby] The host removed {name} (client {clientId}).");
            RpcKicked(name);
            TargetYouWereRemoved(target);
            // Remove from board immediately; transport disconnect lags behind.
            _members.RemoveAt(index);
            // Brief grace so TargetYouWereRemoved arrives before disconnecting.
            StartCoroutine(DisconnectSoon(target));
        }

        System.Collections.IEnumerator DisconnectSoon(NetworkConnection target)
        {
            yield return new WaitForSecondsRealtime(KickGraceSeconds);
            if (target != null && target.IsActive)
                target.Disconnect(false);
        }

        const float KickGraceSeconds = 0.3f;

        /// <summary>This machine only, raised just before the host disconnects it: the host removed this player.</summary>
        public static event Action LocalRemovedByHost;

        [TargetRpc]
        void TargetYouWereRemoved(NetworkConnection target) => LocalRemovedByHost?.Invoke();

        [ObserversRpc]
        void RpcKicked(string name) => OnKicked?.Invoke(name);

        [ServerRpc(RequireOwnership = false)]
        void CmdSetName(string name, NetworkConnection conn = null)
        {
            int index = IndexOf(conn.ClientId);
            if (index < 0)
                return;

            string clean = CleanName(name);
            if (clean.Length == 0)
                return;

            LobbyMember member = _members[index];
            member.Name = clean;
            _members[index] = member;
        }

        [ServerRpc(RequireOwnership = false)]
        void CmdChooseFirm(byte firm, NetworkConnection conn = null)
        {
            int index = IndexOf(conn.ClientId);
            if (index < 0 || firm >= _rules.FirmCount || _runInProgress.Value)
                return;

            LobbyMember member = _members[index];
            if (member.Firm == firm)
            {
                TargetFirmChosen(conn, firm);
                return;
            }

            // Fairness: after the move, the new firm may lead by at most the allowed margin.
            int target = FirmSize(firm) + 1;
            int other = FirmSize(member.Firm) - 1;
            if (target - other > _rules.MaxImbalance)
            {
                TargetFirmRefused(conn, firm);
                return;
            }

            member.Firm = firm;
            if (member.State == MemberState.Ready)
                member.State = MemberState.Browsing; // A change of firm takes the ready back: the others readied for the old teams.
            _members[index] = member;
            TargetFirmChosen(conn, firm);
        }

        [ServerRpc(RequireOwnership = false)]
        void CmdSetReady(bool ready, NetworkConnection conn = null)
        {
            int index = IndexOf(conn.ClientId);
            if (index < 0 || _runInProgress.Value)
                return;

            LobbyMember member = _members[index];
            if (member.State == MemberState.Fitting)
                return;

            MemberState next = ready ? MemberState.Ready : MemberState.Browsing;
            if (member.State == next)
                return;

            member.State = next;
            _members[index] = member;
        }

        [ServerRpc(RequireOwnership = false)]
        void CmdSetFitting(bool fitting, NetworkConnection conn = null)
        {
            int index = IndexOf(conn.ClientId);
            if (index < 0)
                return;

            LobbyMember member = _members[index];
            if (fitting && _runInProgress.Value)
                return; // Ignore late Fitting=true requests once the run has started.
            MemberState next = fitting ? MemberState.Fitting : MemberState.Browsing;
            if (member.State == next)
                return;

            member.State = next;
            _members[index] = member;
        }

        [ServerRpc(RequireOwnership = false)]
        void CmdStart(bool confirmed, NetworkConnection conn = null)
        {
            if (!conn.IsHost)
                return;

            StartState state = Evaluate(out StartBlock reason);
            if (state == StartState.Unavailable)
            {
                TargetStartBlocked(conn, reason);
                return;
            }

            if (state == StartState.NeedsConfirm && !confirmed)
            {
                TargetStartNeedsConfirm(conn, reason);
                return;
            }

            _runInProgress.Value = true;
            // A fresh list per run: listeners may keep it, and a run starts once in minutes.
            var entrants = new List<RunEntrant>(_members.Count);
            for (int i = 0; i < _members.Count; i++)
                entrants.Add(new RunEntrant(_members[i].ClientId, _members[i].Firm, _members[i].Name));

            Debug.Log($"[Lobby] Run start requested with {entrants.Count} players{(state == StartState.NeedsConfirm ? " (short-handed, confirmed)" : string.Empty)}.");
            RpcRunStarting();
            RunStartRequested?.Invoke(entrants);
        }

        /// <summary>Host only. Marks the lobby run as active so reception actions stay locked.</summary>
        public void ServerBeginRun()
        {
            if (!IsServerStarted)
                return;

            _runInProgress.Value = true;
        }

        /// <summary>Host only. Clears ready/fitting states and reopens the desk after a run.</summary>
        public void ServerReturnToLobby()
        {
            if (!IsServerInitialized)
                return;

            _runInProgress.Value = false;
            for (int i = 0; i < _members.Count; i++)
            {
                LobbyMember member = _members[i];
                if (member.State == MemberState.Browsing)
                    continue;
                member.State = MemberState.Browsing;
                _members[i] = member;
            }
        }

        [TargetRpc]
        void TargetFirmChosen(NetworkConnection target, byte firm) => OnFirmChosen?.Invoke(firm);

        [TargetRpc]
        void TargetFirmRefused(NetworkConnection target, byte firm) => OnFirmRefused?.Invoke(firm);

        [TargetRpc]
        void TargetStartNeedsConfirm(NetworkConnection target, StartBlock block) => OnStartNeedsConfirm?.Invoke(block);

        [TargetRpc]
        void TargetStartBlocked(NetworkConnection target, StartBlock block) => OnStartBlocked?.Invoke(block);

        [ObserversRpc]
        void RpcRunStarting() => OnRunStarting?.Invoke();

        void OnClientLoaded(NetworkConnection conn, bool asServer)
        {
            if (asServer)
                ServerAdd(conn);
        }

        void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped)
                return;

            int index = IndexOf(conn.ClientId);
            if (index >= 0)
                _members.RemoveAt(index);
        }

        void ServerAdd(NetworkConnection conn)
        {
            if (IndexOf(conn.ClientId) >= 0)
                return;

            // Fair auto-balance: a newcomer joins the smaller firm; on a tie, the first firm.
            byte firm = (byte)(FirmSize(1) < FirmSize(0) ? 1 : 0);
            _members.Add(new LobbyMember
            {
                ClientId = conn.ClientId,
                Name = DefaultName(),
                Firm = firm,
                State = MemberState.Browsing
            });
        }

        string DefaultName()
        {
            int number = 1;
            for (int tries = 0; tries < 64; tries++, number++)
            {
                string candidate = "Associate " + number;
                bool taken = false;
                for (int i = 0; i < _members.Count && !taken; i++)
                    taken = _members[i].Name == candidate;
                if (!taken)
                    return candidate;
            }

            return "Associate";
        }

        static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            // Strip rich-text tags from remote player names.
            var chars = new System.Text.StringBuilder(Mathf.Min(name.Length, MaxNameLength));
            for (int i = 0; i < name.Length && chars.Length < MaxNameLength; i++)
            {
                char c = name[i];
                if (c != '<' && c != '>' && !char.IsControl(c))
                    chars.Append(c);
            }

            return chars.ToString().Trim();
        }

        void OnMembersChange(SyncListOperation op, int index, LobbyMember previous, LobbyMember next, bool asServer)
        {
            // Host fires OnChange twice; UI and secretary react once on the client side.
            if (asServer)
                return;

            if (!_listening && _listenAfter > 0f && Time.unscaledTime >= _listenAfter)
                _listening = true;
            if (op == SyncListOperation.Add)
                SendNameOnce();
            if (_listening)
                ReportMoment(op, previous, next);
            OnChanged?.Invoke();
        }

        void ReportMoment(SyncListOperation op, LobbyMember previous, LobbyMember next)
        {
            if (op == SyncListOperation.Add)
            {
                OnArrived?.Invoke(next);
                return;
            }

            if (op != SyncListOperation.Set || next.ClientId != previous.ClientId)
                return;

            bool wasReady = previous.State == MemberState.Ready;
            bool isReady = next.State == MemberState.Ready;
            if (wasReady != isReady)
                OnReadyChanged?.Invoke(next, isReady);
        }
    }
}
