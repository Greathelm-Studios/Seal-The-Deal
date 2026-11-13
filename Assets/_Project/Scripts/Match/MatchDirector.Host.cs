using System.Collections.Generic;
using CloseTheDeal.Tower;
using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// The host's half of the match: starting and ending it, and every briefcase and
    /// checkpoint outcome. Nothing here runs on a client; clients only see the synced result.
    /// </summary>
    public sealed partial class MatchDirector
    {
        /// <summary>The player body the spawn checks make room for, in metres (PLAYER_MOVEMENT.md: a 1.8 m capsule, 0.35 m radius).</summary>
        const float PlayerHeight = 1.8f;
        const float PlayerRadius = 0.35f;

        /// <summary>How far from a checkpoint spot the next player is placed when the spot is taken, in metres.</summary>
        const float SpawnRing = 1f;
        const int SpawnRingPoints = 6;

        /// <summary>How far ahead of the carrier a dropped briefcase leaves the hand, in metres: just clear of their body.</summary>
        const float DropReach = 0.7f;

        /// <summary>How far above its return spot a returning briefcase appears, in metres, so it settles rather than clips.</summary>
        const float ReturnHeight = 0.3f;

        /// <summary>Where the briefcase sits in Building A's reception when the scene has no plaza: on the reception desk, metres from the window edge.</summary>
        static readonly Vector3 ReceptionDeskFallback = new(8.4f, 1.15f, -9.1f);

        MatchReferee _referee;
        readonly Vector3[] _checkpointSpot = new Vector3[FirmCount];
        bool _gatherOnBuild;
        bool _countdownOnBuild;

        // ---- Lifecycle -----------------------------------------------------------------------

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (!_live)
                return; // No profile; OnStartNetwork has said so.

            _referee = new MatchReferee(this);
            _gatherOnBuild = false;
            _countdownOnBuild = false;
            TeamSpawner.SpawnOverride = ChooseJoinSpawn;
            _snapshot.Value = new MatchSnapshot { Phase = MatchPhase.Waiting, Winner = NoFirm };

            // The tower may have built before this object started; if not, OnTowerBuilt does this.
            if (_tower != null && _tower.IsBuilt)
                ServerPrepareRound();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (TeamSpawner.SpawnOverride == ChooseJoinSpawn)
                TeamSpawner.SpawnOverride = null;
            _referee = null;
        }

        // ---- Starting and ending -------------------------------------------------------------

        /// <summary>True when this machine is the host and the match is waiting in reception.</summary>
        public bool CanHostStart => IsServerStarted && _arena.Ready && Phase == MatchPhase.Waiting;

        /// <summary>True when this machine is the host and a result is in.</summary>
        public bool CanHostRematch => IsServerStarted && _arena.Ready && Phase == MatchPhase.Ended;

        /// <summary>Host only. Starts the first match with the firms as the host assigned them on join.</summary>
        public void HostStart()
        {
            if (CanHostStart)
                ServerNewMatch();
        }

        /// <summary>Host only. After a result: fresh towers, everyone back to reception, countdown.</summary>
        public void HostRematch()
        {
            if (CanHostRematch)
                ServerNewMatch();
        }

        /// <summary>
        /// Host only. The entry point for a lobby that has already decided the firms: puts each
        /// listed player in the given firm (client id → firm 0 or 1), then starts a run as Start
        /// does. Players not listed keep the firm they have. Works while waiting or after a
        /// result; returns false and changes nothing otherwise.
        /// </summary>
        public bool HostStartRun(IReadOnlyDictionary<int, int> firmByClientId)
        {
            if (!(CanHostStart || CanHostRematch) || firmByClientId == null)
                return false;

            TeamSpawner spawner = TeamSpawner.Instance;
            foreach (FirmMember member in FirmMember.All)
            {
                if (!member.Owner.IsValid || !firmByClientId.TryGetValue(member.Owner.ClientId, out int firm) || firm < 0 || firm >= FirmCount)
                    continue;

                int slot = spawner != null ? spawner.AssignTeam(member.Owner.ClientId, firm) : member.Slot;
                member.ServerSetFirm(firm, slot);
            }

            ServerNewMatch();
            return true;
        }

        /// <summary>Rebuilding the towers puts the locked doors and furniture back and changes the layout; the rest follows in ServerPrepareRound once they stand.</summary>
        void ServerNewMatch()
        {
            _gatherOnBuild = true;
            _countdownOnBuild = true;
            _tower.Rebuild();
        }

        /// <summary>The towers stand (first build or a rebuild): the briefcase on its spawn, progress wiped, and the round begun if one was asked for.</summary>
        void ServerPrepareRound()
        {
            _referee.ResetRound();
            for (int firm = 0; firm < FirmCount; firm++)
            {
                _checkpointSpot[firm] = Vector3.zero;
                SetCheckpoint(firm, 0);
            }

            _openFloor.Value = 0;
            ApplyGates();

            Vector3 spawn = SpawnPosition();
            Briefcase briefcase = EnsureBriefcase(spawn);
            if (briefcase != null)
            {
                briefcase.ServerRelease(spawn, _arena.Facing(0), Vector3.zero);
                _briefcase.Value = new BriefcaseStatus
                {
                    State = BriefcaseState.Dropped,
                    CarrierId = -1,
                    CarrierFirm = NoFirm,
                    LastCarrierFirm = NoFirm,
                    Place = _arena.PlaceOf(spawn, 0f),
                    Position = spawn
                };
            }

            if (_gatherOnBuild)
                ServerGatherInReception();
            if (_countdownOnBuild)
                _snapshot.Value = new MatchSnapshot { Phase = MatchPhase.Countdown, PhaseTick = TimeManager.Tick, Winner = NoFirm };

            _gatherOnBuild = false;
            _countdownOnBuild = false;
        }

        /// <summary>One briefcase for as long as the host runs: spawned once, then only ever moved.</summary>
        Briefcase EnsureBriefcase(Vector3 spawn)
        {
            if (Briefcase.Current != null)
                return Briefcase.Current;

            if (_profile.BriefcasePrefab == null)
            {
                Debug.LogError("[Match] The match profile has no briefcase prefab; there is nothing to carry.");
                return null;
            }

            NetworkObject spawned = NetworkManager.GetPooledInstantiated(_profile.BriefcasePrefab, spawn, _arena.Facing(0), true);
            if (!spawned.TryGetComponent(out Briefcase briefcase))
            {
                Debug.LogError("[Match] The briefcase prefab has no Briefcase component.");
                Destroy(spawned.gameObject);
                return null;
            }

            ServerManager.Spawn(spawned);
            return briefcase;
        }

        void ServerGatherInReception()
        {
            foreach (FirmMember member in FirmMember.All)
                ServerRespawn(member);
        }

        internal void ServerBeginRunning(uint tick)
        {
            _snapshot.Value = new MatchSnapshot { Phase = MatchPhase.Running, PhaseTick = tick, Winner = NoFirm };
        }

        internal void ServerEnd(byte winner, MatchEndReason reason, uint tick)
        {
            float seconds = (float)(ElapsedTicks(tick) * TimeManager.TickDelta);
            _snapshot.Value = new MatchSnapshot
            {
                Phase = MatchPhase.Ended,
                PhaseTick = tick,
                Winner = winner,
                Reason = reason,
                EndSeconds = (ushort)Mathf.Clamp(Mathf.FloorToInt(seconds), 0, ushort.MaxValue),
                EndFloor = _briefcase.Value.Floor
            };
            Debug.Log($"[Match] Ended: winner {(winner == NoFirm ? "none" : FirmName(winner))}, {reason}, after {seconds:0} s.");
        }

        // ---- Briefcase outcomes --------------------------------------------------------------

        /// <summary>A player pressed interact. If the loose briefcase is in their reach, it is theirs. Requests are handled one at a time, so of two in the same tick the first to arrive wins.</summary>
        internal void ServerTryPickUp(FirmMember member)
        {
            if (!IsServerStarted || Phase != MatchPhase.Running || member.Knocked || !CanGrab(member))
                return;

            Briefcase.Current.ServerPark();
            BriefcaseStatus status = _briefcase.Value;
            status.State = BriefcaseState.Carried;
            status.CarrierId = member.ObjectId;
            status.CarrierFirm = (byte)member.Firm;
            status.LastCarrierFirm = (byte)member.Firm;
            _briefcase.Value = status;
        }

        /// <summary>The carrier lets go, whatever the reason. Does nothing if they carry nothing.</summary>
        internal void ServerDrop(FirmMember member, DropCause cause)
        {
            if (!IsServerStarted || !IsCarrying(member))
                return;

            Vector3 forward = member.Forward;
            Vector3 position;
            Vector3 velocity;
            if (cause == DropCause.Manual)
            {
                position = HandOffPoint(member, forward);
                velocity = member.Velocity + forward * _profile.DropTossSpeed + Vector3.up * _profile.DropTossLift;
            }
            else
            {
                // Knocked loose: it leaves from above the head, so it never starts inside the body.
                position = member.Position + Vector3.up * (member.BodyHeight + 0.2f);
                velocity = member.Velocity * 0.6f + Vector3.up * _profile.DropTossLift;
            }

            ServerPutDown(position, Quaternion.LookRotation(forward, Vector3.up), velocity);
            if (cause == DropCause.Knocked)
                member.Notify(MatchNotice.BriefcaseKnockedLoose);
        }

        /// <summary>Just ahead of the carrier's chest, pulled back if a wall is closer than that.</summary>
        Vector3 HandOffPoint(FirmMember member, Vector3 forward)
        {
            Vector3 chest = member.Chest;
            float reach = DropReach;
            if (Physics.SphereCast(chest, 0.2f, forward, out RaycastHit wall, reach, _solidMask, QueryTriggerInteraction.Ignore))
                reach = Mathf.Max(0f, wall.distance - 0.05f);
            return chest + forward * reach;
        }

        /// <summary>A player's body is leaving the game; if they carry the briefcase it stays, where they stood.</summary>
        internal void ServerMemberLeft(FirmMember member)
        {
            ServerDrop(member, DropCause.LeftTheGame);
        }

        /// <summary>The carrier vanished without a word: the briefcase goes down where they were last seen.</summary>
        internal void ServerDropOrphan()
        {
            ServerPutDown(Briefcase.Current.LastCarrierPosition + Vector3.up, Quaternion.identity, Vector3.zero);
        }

        void ServerPutDown(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            Briefcase.Current.ServerRelease(position, rotation, velocity);
            BriefcaseStatus status = _briefcase.Value;
            status.State = BriefcaseState.Dropped;
            status.CarrierId = -1;
            status.CarrierFirm = NoFirm;
            status.Position = position;
            _briefcase.Value = status;
        }

        /// <summary>The briefcase left the buildings: it goes away, and comes back after the delay.</summary>
        internal void ServerBeginReturn(uint tick)
        {
            Briefcase briefcase = Briefcase.Current;
            briefcase.ServerPark();
            briefcase.ReturnAtTick = tick + TimeManager.TimeToTicks(_profile.ReturnDelaySeconds, TickRounding.RoundUp);

            // Where it will be is known now, so the clock never credits a floor it has left.
            BriefcaseStatus status = _briefcase.Value;
            status.State = BriefcaseState.Returning;
            status.CarrierId = -1;
            status.CarrierFirm = NoFirm;
            status.Floor = status.ReturnFloor;
            status.Place = _arena.PlaceOf(ReturnPosition(status.ReturnFloor), 0f);
            _briefcase.Value = status;
        }

        internal void ServerFinishReturn()
        {
            Vector3 spot = ReturnPosition(_briefcase.Value.ReturnFloor) + Vector3.up * ReturnHeight;
            ServerPutDown(spot, _arena.Facing(0), Vector3.zero);
        }

        /// <summary>Host only. The carrier stood on a floor above the open floor: it opens, in both towers, for the rest of the match.</summary>
        internal void ServerOpenFloor(int floor)
        {
            if (floor <= _openFloor.Value)
                return;

            _openFloor.Value = (byte)floor;
            ApplyGates();
        }

        /// <summary>Host only. Keeps where-it-is up to date, writing only when something changed. The return floor only ever rises, and never above the open floor.</summary>
        internal void ServerTrackBriefcase(byte place, int floor)
        {
            BriefcaseStatus status = _briefcase.Value;
            byte returnFloor = status.ReturnFloor;
            if (_arena.IsCheckpoint(floor) && floor > returnFloor && floor <= _openFloor.Value)
                returnFloor = (byte)floor;

            if (status.Place == place && status.Floor == floor && status.ReturnFloor == returnFloor)
                return;

            status.Place = place;
            status.Floor = (byte)floor;
            status.ReturnFloor = returnFloor;
            _briefcase.Value = status;
        }

        /// <summary>A player stood on a checkpoint floor of their own tower: their firm comes back to that spot after a fall.</summary>
        internal void ServerSaveCheckpoint(int firm, int floor, Vector3 spot)
        {
            _checkpointSpot[firm] = spot;
            SetCheckpoint(firm, floor);
        }

        // ---- Places --------------------------------------------------------------------------

        Vector3 SpawnPosition()
        {
            return _crossings != null ? _crossings.Spawn : _arena.FloorPoint(0, 0, ReceptionDeskFallback);
        }

        /// <summary>The skybridge of that checkpoint floor, or the spawn when there is none.</summary>
        Vector3 ReturnPosition(int floor)
        {
            if (floor > 0 && _crossings != null && _crossings.TryGetBridgeCentre(floor, out Vector3 centre))
                return centre;
            return SpawnPosition();
        }

        /// <summary>Puts a player back at their firm's last checkpoint, or in reception if there is none yet.</summary>
        internal void ServerRespawn(FirmMember member)
        {
            int firm = member.Firm;
            if (CheckpointFloor(firm) > 0)
            {
                member.ServerTeleport(FreeSpotNear(_checkpointSpot[firm], member.Slot));
                return;
            }

            if (_tower.TryGetSpawn(firm, member.Slot, out Vector3 position, out _))
                member.ServerTeleport(position);
        }

        /// <summary>A player joining a running match starts at their firm's checkpoint; before one is saved, in reception as usual.</summary>
        bool ChooseJoinSpawn(int team, int slot, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (!_arena.Ready || Phase != MatchPhase.Running || CheckpointFloor(team) == 0)
                return false;

            position = FreeSpotNear(_checkpointSpot[team], slot);
            rotation = _arena.Facing(team);
            return true;
        }

        /// <summary>The spot itself, or the first free place on a ring around it, starting from a different one per slot so two players never land on each other.</summary>
        Vector3 FreeSpotNear(Vector3 spot, int slot)
        {
            for (int i = 0; i <= SpawnRingPoints; i++)
            {
                int index = (slot + i) % (SpawnRingPoints + 1);
                Vector3 candidate = spot;
                if (index > 0)
                    candidate += Quaternion.Euler(0f, 360f / SpawnRingPoints * (index - 1), 0f) * Vector3.forward * SpawnRing;
                if (Standable(candidate))
                    return candidate;
            }

            return spot;
        }

        /// <summary>Floor underfoot and room for a whole body, with nobody and nothing already there.</summary>
        bool Standable(Vector3 feet)
        {
            if (!Physics.Raycast(feet + Vector3.up * 0.5f, Vector3.down, 0.8f, _solidMask, QueryTriggerInteraction.Ignore))
                return false;

            Vector3 low = feet + Vector3.up * (PlayerRadius + 0.1f);
            Vector3 high = feet + Vector3.up * (PlayerHeight - PlayerRadius);
            return !Physics.CheckCapsule(low, high, PlayerRadius, ~0, QueryTriggerInteraction.Ignore);
        }

        void SetCheckpoint(int firm, int floor)
        {
            if (firm == 0)
                _checkpointA.Value = (byte)floor;
            else
                _checkpointB.Value = (byte)floor;
        }
    }
}
