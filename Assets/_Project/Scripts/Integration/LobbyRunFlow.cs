using System.Collections.Generic;
using CloseTheDeal.Combat;
using CloseTheDeal.Items;
using CloseTheDeal.Lobby;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Tower;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Integration
{
    /// <summary>
    /// Host only. Joins the lobby to the briefcase match:
    /// - the desk's start (everyone ready) starts the match with the firms chosen at the stands;
    /// - anyone arriving before a match or after a result appears in the lobby, anyone arriving
    ///   mid-match appears where the match puts them;
    /// - a while after a result, everyone is walked back to the lobby and the desk reopens;
    /// - every match starts level: starting guns, full health.
    /// The match itself moves everyone into their firm's reception when it starts.
    /// Owner: host. Synced: nothing of its own (positions ride the bodies, the lobby and match
    /// sync their own state). Design: docs/systems/INTEGRATION.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyRunFlow : MonoBehaviour
    {
        [Tooltip("Where players stand in the lobby: on arrival, and when they are brought back after a result. Used in turn by firm and place in the firm.")]
        [SerializeField] Transform[] _lobbySpawns = new Transform[0];

        [Tooltip("How long the result stays up before everyone is brought back to the lobby, in seconds. Long enough to read the result and celebrate on the roof; 0 brings them back at once.")]
        [Min(0f)] [SerializeField] float _backToLobbySeconds = 8f;

        const int PlacesPerFirm = 4;

        MatchDirector _match;
        float _returnAt = -1f;

        /// <summary>Host only. How many times a lobby start reached the match, and how many of those the match took.</summary>
        public int StartsRequested { get; private set; }
        public int StartsTaken { get; private set; }

        void OnEnable()
        {
            LobbyRoster.RunStartRequested += OnRunStartRequested;
            TeamSpawner.OnPlayerSpawning += OnPlayerSpawning;
        }

        void OnDisable()
        {
            // Both are static: a missed unsubscribe would stack a handler per Play session.
            LobbyRoster.RunStartRequested -= OnRunStartRequested;
            TeamSpawner.OnPlayerSpawning -= OnPlayerSpawning;
        }

        void Start()
        {
            _match = MatchDirector.Instance;
            if (_match == null)
            {
                Debug.LogError("[Integration] No Match in the scene: the lobby desk cannot start anything.");
                return;
            }

            _match.OnPhaseChanged += OnPhaseChanged;
        }

        void OnDestroy()
        {
            if (_match != null)
                _match.OnPhaseChanged -= OnPhaseChanged;
        }

        bool IsHost => _match != null && _match.IsServerStarted;

        // ---- Lobby start → match -----------------------------------------------------------------

        void OnRunStartRequested(IReadOnlyList<RunEntrant> entrants)
        {
            if (!IsHost)
                return;

            StartsRequested++;
            // Lobby firm 0 / 1 is the match's building A / B: both index the firm by the building it climbs.
            if (_match.HostStartRun(LobbyRoster.ToFirmMap(entrants)))
            {
                StartsTaken++;
                Debug.Log($"[Integration] The lobby started the match with {entrants.Count} players.");
                return;
            }

            // A match is already counting down or running (the host's menu got there first):
            // reopen the desk rather than leave the lobby saying a run is starting.
            Debug.Log("[Integration] The lobby asked for a start while a match was already on; the desk is open again.");
            if (LobbyRoster.Instance != null)
                LobbyRoster.Instance.ServerReturnToLobby();
        }

        // ---- Phases --------------------------------------------------------------------------------

        void OnPhaseChanged(MatchPhase previous, MatchPhase next)
        {
            if (!IsHost)
                return;

            _returnAt = next == MatchPhase.Ended ? Time.time + _backToLobbySeconds : -1f;
            if (next == MatchPhase.Countdown)
                FreshStart();
        }

        /// <summary>
        /// Every match starts level, however it was started (desk or menu): the scene's starting
        /// guns with full magazines, full health, and anyone dead when the match moved them comes
        /// back where they were moved to, not where they died last match.
        /// </summary>
        static void FreshStart()
        {
            StartingLoadout loadout = StartingLoadout.Instance;
            IReadOnlyList<FirmMember> all = FirmMember.All;
            for (int i = 0; i < all.Count; i++)
            {
                ItemCarrier carrier = PlayerParts.CarrierOf(all[i]);
                if (carrier != null)
                    carrier.ServerForceDrop();
                if (all[i].TryGetComponent(out WeaponHolder guns))
                    StartingGuns(guns, loadout);
                if (!all[i].TryGetComponent(out Health health))
                    continue;
                if (health.IsDead)
                    health.OverrideNextRespawn(all[i].Position);
                else
                    health.Restore();
            }
        }

        static void StartingGuns(WeaponHolder guns, StartingLoadout loadout)
        {
            for (int slot = 0; slot < WeaponHolder.SlotCount; slot++)
            {
                WeaponDefinition start = loadout != null && slot < loadout.Weapons.Length ? loadout.Weapons[slot] : null;
                if (start == null)
                    guns.Unequip(slot);
                else if (!guns.Equip(start, slot))
                    Debug.LogWarning($"[Integration] Starting gun '{start.name}' is not in the weapon catalog; slot {slot + 1} kept what it had.");
            }
        }

        void Update()
        {
            if (_returnAt < 0f || Time.time < _returnAt)
                return;

            _returnAt = -1f;
            if (IsHost && _match.Phase == MatchPhase.Ended)
                ServerBackToLobby();
        }

        /// <summary>Host only. Everyone back in the lobby with empty hands, the desk open and every ready cleared.</summary>
        public void ServerBackToLobby()
        {
            if (!IsHost || _lobbySpawns.Length == 0)
                return;

            IReadOnlyList<FirmMember> all = FirmMember.All;
            for (int i = all.Count - 1; i >= 0; i--)
                SendToLobby(all[i]);

            if (LobbyRoster.Instance != null)
                LobbyRoster.Instance.ServerReturnToLobby();
            Debug.Log("[Integration] Everyone is back in the lobby; the desk is open.");
        }

        void SendToLobby(FirmMember member)
        {
            // Nothing comes along: the briefcase stays where the match ended, an item where it was carried.
            _match.ServerDrop(member, DropCause.Manual);
            ItemCarrier carrier = PlayerParts.CarrierOf(member);
            if (carrier != null)
                carrier.ServerForceDrop();

            Vector3 spot = LobbySpot(member.Firm, member.Slot).position;
            member.ServerTeleport(spot);
            if (member.TryGetComponent(out Health health) && health.IsDead)
                health.OverrideNextRespawn(spot);
        }

        Transform LobbySpot(int firm, int slot)
        {
            return _lobbySpawns[(firm * PlacesPerFirm + slot) % _lobbySpawns.Length];
        }

        // ---- Arrivals ------------------------------------------------------------------------------

        /// <summary>Before a match and after a result, a joiner appears in the lobby; mid-match the match's own spawn stands.</summary>
        void OnPlayerSpawning(NetworkObject player, NetworkConnection connection, int firm, int slot)
        {
            if (_lobbySpawns.Length == 0)
                return;

            MatchPhase phase = _match != null ? _match.Phase : MatchPhase.Waiting;
            if (phase == MatchPhase.Countdown || phase == MatchPhase.Running)
                return;

            Transform spot = LobbySpot(firm, slot);
            player.transform.SetPositionAndRotation(spot.position, spot.rotation);
            if (player.TryGetComponent(out Rigidbody body))
            {
                body.position = spot.position;
                body.rotation = spot.rotation;
            }
        }
    }
}
