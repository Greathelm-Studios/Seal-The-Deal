using System;
using System.Collections.Generic;
using CloseTheDeal.Lobby;
using CloseTheDeal.MatchMode;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace CloseTheDeal.Records
{
    /// <summary>
    /// Every player's numbers for the match in progress, the kill feed, and each player's final
    /// record. The host counts (MatchRecords.Host.cs); every machine, the host's own included,
    /// keeps the host's copy, which arrives as small broadcasts on change. Nobody counts their
    /// own numbers, so a client can't claim a stat. A plain scene object, not a networked one.
    /// Design, ownership and the UI contract: docs/systems/RECORDS.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class MatchRecords : MonoBehaviour
    {
        /// <summary>The one on this machine, or null.</summary>
        public static MatchRecords Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        [Header("Credit")]
        [Tooltip("How long after a rival's hit a fall out of the building still counts as their kill (the window shot), in seconds. Longer = more falls credited; 0 = falls are never anyone's kill.")]
        [Range(0f, 20f)] [SerializeField] float _windowKillSeconds = 6f;

        [Header("Climb")]
        [Tooltip("How often the host checks which floor each player is on for their best climb, in seconds. Lower notices a quick dash up and back sooner; 0.5 is plenty for floors that take a minute to climb.")]
        [Range(0.1f, 2f)] [SerializeField] float _floorCheckSeconds = 0.5f;

        // ---- Events (every machine) --------------------------------------------------------------

        /// <summary>A player's line changed or appeared. Read it from the argument.</summary>
        public event Action<MatchLine> LineChanged;

        /// <summary>A player left the match; their line is gone. The argument is their client id.</summary>
        public event Action<int> LineRemoved;

        /// <summary>A new match started counting, or this machine left the game: every line is cleared.</summary>
        public event Action Cleared;

        /// <summary>A kill-feed line, decided by the host.</summary>
        public event Action<KillEntry> Kill;

        /// <summary>This machine's player finished a match. Raised once per match, from the host's numbers only.</summary>
        public event Action<MatchFinal> FinalRecord;

        // ---- State (every machine) ---------------------------------------------------------------

        readonly List<MatchLine> _lines = new();
        uint _key;
        uint _lastFinalKey;
        bool _hasFinal;
        NetworkManager _net;
        MatchDirector _match;

        /// <summary>Every player's line for the match in progress, in the order they appeared.</summary>
        public IReadOnlyList<MatchLine> Lines => _lines;

        /// <summary>The last final record this machine received this session, if any. For a results screen opened late.</summary>
        public bool TryGetLastFinal(out MatchFinal final)
        {
            final = _lastFinal;
            return _hasFinal;
        }

        MatchFinal _lastFinal;

        public bool TryGetLine(int clientId, out MatchLine line)
        {
            int index = IndexOf(clientId);
            line = index >= 0 ? _lines[index] : default;
            return index >= 0;
        }

        /// <summary>This machine's own line, if the match has one for it.</summary>
        public bool TryGetLocalLine(out MatchLine line)
        {
            line = default;
            return _net != null && _net.IsClientStarted && _net.ClientManager.Connection != null
                && TryGetLine(_net.ClientManager.Connection.ClientId, out line);
        }

        /// <summary>A player's name as the reception board has it, or a firm-neutral stand-in when the board doesn't know them.</summary>
        public static string NameOf(int clientId)
        {
            LobbyRoster roster = LobbyRoster.Instance;
            int index = roster != null ? roster.IndexOf(clientId) : -1;
            if (index >= 0 && !string.IsNullOrEmpty(roster[index].Name))
                return roster[index].Name;
            return clientId >= 0 ? "Associate " + (clientId + 1) : "The building";
        }

        // ---- Lifecycle ---------------------------------------------------------------------------

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Records] A second MatchRecords was found and switched off. Keep one per scene.");
                enabled = false;
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            Unhook();
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (_net == null)
                HookNetwork();
            if (_match == null && MatchDirector.Instance != null)
                HookMatch(MatchDirector.Instance);
            if (IsHostCounting)
                HostSampleFloors();
            if (_net != null && _match != null && !IsHostCounting)
                enabled = false;
        }

        void HookNetwork()
        {
            _net = InstanceFinder.NetworkManager;
            if (_net == null)
                return;

            _net.ClientManager.RegisterBroadcast<MatchResetBroadcast>(OnReset);
            _net.ClientManager.RegisterBroadcast<MatchLineBroadcast>(OnLine);
            _net.ClientManager.RegisterBroadcast<KillBroadcast>(OnKill);
            _net.ClientManager.RegisterBroadcast<MatchFinalBroadcast>(OnFinal);
            _net.ClientManager.OnClientConnectionState += OnClientConnection;
            HostHookNetwork(_net);
        }

        void HookMatch(MatchDirector match)
        {
            _match = match;
            HostHookMatch(match);
        }

        void Unhook()
        {
            if (_net != null)
            {
                _net.ClientManager.UnregisterBroadcast<MatchResetBroadcast>(OnReset);
                _net.ClientManager.UnregisterBroadcast<MatchLineBroadcast>(OnLine);
                _net.ClientManager.UnregisterBroadcast<KillBroadcast>(OnKill);
                _net.ClientManager.UnregisterBroadcast<MatchFinalBroadcast>(OnFinal);
                _net.ClientManager.OnClientConnectionState -= OnClientConnection;
                HostUnhookNetwork(_net);
            }

            if (_match != null)
                HostUnhookMatch(_match);
            _net = null;
            _match = null;
        }

        // ---- Arriving from the host --------------------------------------------------------------

        void OnClientConnection(ClientConnectionStateArgs args)
        {
            // Leaving (or the host going away) clears what this machine knew. No record is written:
            // only a final record from the host ever reaches the profile.
            if (args.ConnectionState != LocalConnectionState.Stopped)
                return;

            _hasFinal = false;
            Clear(0);
        }

        void OnReset(MatchResetBroadcast message, Channel channel) => Clear(message.MatchKey);

        void Clear(uint key)
        {
            _key = key;
            _lines.Clear();
            Cleared?.Invoke();
        }

        void OnLine(MatchLineBroadcast message, Channel channel)
        {
            if (message.MatchKey != _key)
                return;

            int index = IndexOf(message.Line.ClientId);
            if (message.Gone)
            {
                if (index < 0)
                    return;
                _lines.RemoveAt(index);
                LineRemoved?.Invoke(message.Line.ClientId);
                return;
            }

            if (index >= 0)
                _lines[index] = message.Line;
            else
                _lines.Add(message.Line);
            LineChanged?.Invoke(message.Line);
        }

        void OnKill(KillBroadcast message, Channel channel) => Kill?.Invoke(message.Entry);

        void OnFinal(MatchFinalBroadcast message, Channel channel)
        {
            if (_hasFinal && message.Final.MatchKey == _lastFinalKey)
                return;

            _hasFinal = true;
            _lastFinalKey = message.Final.MatchKey;
            _lastFinal = message.Final;
            FinalRecord?.Invoke(message.Final);
        }

        int IndexOf(int clientId)
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                if (_lines[i].ClientId == clientId)
                    return i;
            }

            return -1;
        }
    }
}
