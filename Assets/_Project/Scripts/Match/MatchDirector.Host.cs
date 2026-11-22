using System.Collections.Generic;
using CloseTheDeal.Tower;
using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    public sealed partial class MatchDirector
    {
        const float PlayerHeight = 1.8f;
        const float PlayerRadius = 0.35f;
        const float SpawnRing = 1f;
        const int SpawnRingPoints = 6;
        const float DropReach = 0.7f;
        const float ReturnHeight = 0.3f;

        static readonly Vector3 ReceptionDeskFallback = new(8.4f, 1.15f, -9.1f);

        MatchReferee _referee;
        readonly Vector3[] _checkpointSpot = new Vector3[FirmCount];
        bool _gatherOnBuild;
        bool _countdownOnBuild;

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (!_live)
                return; // No profile; OnStartNetwork already logged and disabled.

            _referee = Ruleset == null ? new MatchReferee(this) : null;
            _gatherOnBuild = false;
            _countdownOnBuild = false;
            if (Ruleset == null)
                TeamSpawner.SpawnOverride = ChooseJoinSpawn;
            _snapshot.Value = new MatchSnapshot { Phase = MatchPhase.Waiting, Winner = NoFirm };

            // Towers may already be built before OnStartServer; otherwise OnTowerBuilt calls this.
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

        /// <summary>True when hosting and waiting in reception.</summary>
        public bool CanHostStart => IsServerStarted && HostReady && Phase == MatchPhase.Waiting;

        /// <summary>True when hosting and a match result is in.</summary>
        public bool CanHostRematch => IsServerStarted && HostReady && Phase == MatchPhase.Ended;

        bool HostReady => Ruleset != null ? Ruleset.Ready : _arena.Ready;

        /// <summary>Host only. Starts the first match with current joiner firms.</summary>
        public void HostStart()
        {
            if (CanHostStart)
                ServerNewMatch();
        }

        /// <summary>Host only. Rebuilds towers and starts a fresh rematch after a result.</summary>
        public void HostRematch()
        {
            if (CanHostRematch)
                ServerNewMatch();
        }

        /// <summary>Host only. Applies lobby firm assignments and starts a match run.</summary>
        public bool HostStartRun(IReadOnlyDictionary<int, int> firmByClientId)
        {
            if (!(CanHostStart || CanHostRematch) || firmByClientId == null)
                return false;

            // Move all seats at once so swapping firms never shares a spawn point.
            TeamSpawner spawner = TeamSpawner.Instance;
            if (spawner != null)
                spawner.AssignTeams(firmByClientId);

            foreach (FirmMember member in FirmMember.All)
            {
                if (!member.Owner.IsValid || !firmByClientId.TryGetValue(member.Owner.ClientId, out int firm) || firm < 0 || firm >= FirmCount)
                    continue;

                int slot = spawner != null ? spawner.SlotOf(member.Owner.ClientId) : member.Slot;
                member.ServerSetFirm(firm, Mathf.Max(0, slot));
            }

            ServerNewMatch();
            return true;
        }

        void ServerNewMatch()
        {
            if (Ruleset != null)
            {
                Ruleset.ServerNewMatch();
                return;
            }

            _gatherOnBuild = true;
            _countdownOnBuild = true;
            _tower.Rebuild();
        }

        void ServerPrepareRound()
        {
            _referee.ResetRound();
            for (int firm = 0; firm < FirmCount; firm++)
            {
                _checkpointSpot[firm] = Vector3.zero;
                SetCheckpoint(firm, 0);
            }

            _openFloor.Value = new FloorOpening { Floor = 0, ByFirm = NoFirm };
            foreach (FirmMember member in FirmMember.All)
                member.GateExplained = false;
            ApplyGates();

            Vector3 spawn = SpawnPosition();
            Briefcase briefcase = EnsureBriefcase(spawn, _arena.Facing(0));
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

        Briefcase EnsureBriefcase(Vector3 spawn, Quaternion facing)
        {
            if (Briefcase.Current != null)
                return Briefcase.Current;

            if (_profile.BriefcasePrefab == null)
            {
                Debug.LogError("[Match] The match profile has no briefcase prefab; there is nothing to carry.");
                return null;
            }

            NetworkObject spawned = NetworkManager.GetPooledInstantiated(_profile.BriefcasePrefab, spawn, facing, true);
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

        /// <summary>Host only, for a ruleset. Starts the countdown before the briefcase unlocks.</summary>
        public void ServerBeginCountdown()
        {
            _snapshot.Value = new MatchSnapshot { Phase = MatchPhase.Countdown, PhaseTick = TimeManager.Tick, Winner = NoFirm };
        }

        /// <summary>Host only, for a ruleset. Back to waiting for the host to start.</summary>
        public void ServerBeginWaiting()
        {
            _snapshot.Value = new MatchSnapshot { Phase = MatchPhase.Waiting, Winner = NoFirm };
        }

        /// <summary>Host only, for a ruleset. Puts a fresh, never-carried briefcase down at this spot, spawning it if needed.</summary>
        public void ServerPlaceBriefcase(Vector3 position, Quaternion rotation, int floor)
        {
            Briefcase briefcase = EnsureBriefcase(position, rotation);
            if (briefcase == null)
                return;

            briefcase.ServerRelease(position, rotation, Vector3.zero);
            _briefcase.Value = new BriefcaseStatus
            {
                State = BriefcaseState.Dropped,
                CarrierId = -1,
                CarrierFirm = NoFirm,
                LastCarrierFirm = NoFirm,
                Floor = (byte)floor,
                Position = position
            };
        }

        /// <summary>Host only, for a ruleset. Puts the loose briefcase somewhere else, keeping who carried it last.</summary>
        public void ServerMoveBriefcase(Vector3 position)
        {
            ServerPutDown(position, Quaternion.identity, Vector3.zero);
        }

        /// <summary>Host only, for a ruleset. Puts the briefcase in this player's hands whatever its state.</summary>
        public void ServerHandBriefcase(FirmMember member)
        {
            if (!IsServerStarted || Briefcase.Current == null || member == null)
                return;

            BriefcaseStatus status = _briefcase.Value;
            // Already parked while carried; parking again would set a velocity on a kinematic body.
            if (status.State != BriefcaseState.Carried)
                Briefcase.Current.ServerPark();
            status.State = BriefcaseState.Carried;
            status.CarrierId = member.ObjectId;
            status.CarrierFirm = (byte)member.Firm;
            status.LastCarrierFirm = (byte)member.Firm;
            status.ReturnTick = 0;
            _briefcase.Value = status;
        }

        /// <summary>Host only, for a ruleset. Records which floor the briefcase is on, for the HUD and the result.</summary>
        public void ServerSetBriefcaseFloor(int floor)
        {
            BriefcaseStatus status = _briefcase.Value;
            if (status.Floor == floor)
                return;

            status.Floor = (byte)floor;
            _briefcase.Value = status;
        }

        public void ServerBeginRunning(uint tick)
        {
            _snapshot.Value = new MatchSnapshot { Phase = MatchPhase.Running, PhaseTick = tick, Winner = NoFirm };
        }

        public void ServerEnd(byte winner, MatchEndReason reason, uint tick)
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

        internal void ServerTryPickUp(FirmMember member)
        {
            if (!IsServerStarted || Phase != MatchPhase.Running || member.Knocked || !CanGrab(member))
                return;
            if (HandsFull != null && HandsFull(member))
                return;

            Briefcase.Current.ServerPark();
            BriefcaseStatus status = _briefcase.Value;
            status.State = BriefcaseState.Carried;
            status.CarrierId = member.ObjectId;
            status.CarrierFirm = (byte)member.Firm;
            status.LastCarrierFirm = (byte)member.Firm;
            _briefcase.Value = status;
        }

        public void ServerDrop(FirmMember member, DropCause cause)
        {
            // Briefcase.Current may already be destroyed during host shutdown.
            if (!IsServerStarted || !IsCarrying(member) || Briefcase.Current == null)
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
                // Spawn above head on knock/disconnect so the case never starts inside the player collider.
                position = member.Position + Vector3.up * (member.BodyHeight + 0.2f);
                velocity = member.Velocity * 0.6f + Vector3.up * _profile.DropTossLift;
            }

            ServerPutDown(position, Quaternion.LookRotation(forward, Vector3.up), velocity);
            if (cause == DropCause.Knocked)
                member.Notify(MatchNotice.BriefcaseKnockedLoose);
        }

        Vector3 HandOffPoint(FirmMember member, Vector3 forward)
        {
            Vector3 chest = member.Chest;
            float reach = DropReach;
            if (Physics.SphereCast(chest, 0.2f, forward, out RaycastHit wall, reach, _solidMask, QueryTriggerInteraction.Ignore))
                reach = Mathf.Max(0f, wall.distance - 0.05f);
            return chest + forward * reach;
        }

        internal void ServerMemberLeft(FirmMember member)
        {
            ServerDrop(member, DropCause.LeftTheGame);
        }

        public void ServerDropOrphan()
        {
            ServerPutDown(Briefcase.Current.LastCarrierPosition + Vector3.up, Quaternion.identity, Vector3.zero);
        }

        void ServerPutDown(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (Briefcase.Current == null)
                return;

            Briefcase.Current.ServerRelease(position, rotation, velocity);
            BriefcaseStatus status = _briefcase.Value;
            status.State = BriefcaseState.Dropped;
            status.CarrierId = -1;
            status.CarrierFirm = NoFirm;
            status.Position = position;
            status.ReturnTick = 0;
            _briefcase.Value = status;
        }

        internal void ServerBeginReturn(uint tick)
        {
            Briefcase briefcase = Briefcase.Current;
            briefcase.ServerPark();
            briefcase.ReturnAtTick = tick + TimeManager.TimeToTicks(_profile.ReturnDelaySeconds, TickRounding.RoundUp);

            // Set destination floor/place immediately so clock never credits the out-of-bounds floor.
            BriefcaseStatus status = _briefcase.Value;
            status.State = BriefcaseState.Returning;
            status.CarrierId = -1;
            status.CarrierFirm = NoFirm;
            status.Floor = status.ReturnFloor;
            status.Place = _arena.PlaceOf(ReturnPosition(status.ReturnFloor), 0f);
            status.ReturnTick = briefcase.ReturnAtTick;
            _briefcase.Value = status;
        }

        internal void ServerFinishReturn()
        {
            Vector3 spot = ReturnPosition(_briefcase.Value.ReturnFloor) + Vector3.up * ReturnHeight;
            ServerPutDown(spot, _arena.Facing(0), Vector3.zero);
        }

        internal void ServerOpenFloor(int floor, int byFirm)
        {
            if (floor <= _openFloor.Value.Floor)
                return;

            _openFloor.Value = new FloorOpening { Floor = (byte)floor, ByFirm = (byte)byFirm };
            ApplyGates();
        }

        internal void ServerTrackBriefcase(byte place, int floor)
        {
            BriefcaseStatus status = _briefcase.Value;
            byte returnFloor = status.ReturnFloor;
            if (_arena.IsCheckpoint(floor) && floor > returnFloor && floor <= _openFloor.Value.Floor)
                returnFloor = (byte)floor;

            if (status.Place == place && status.Floor == floor && status.ReturnFloor == returnFloor)
                return;

            status.Place = place;
            status.Floor = (byte)floor;
            status.ReturnFloor = returnFloor;
            _briefcase.Value = status;
        }

        internal void ServerSaveCheckpoint(int firm, int floor, Vector3 spot)
        {
            _checkpointSpot[firm] = spot;
            SetCheckpoint(firm, floor);
        }

        Vector3 SpawnPosition()
        {
            return _crossings != null ? _crossings.Spawn : _arena.FloorPoint(0, 0, ReceptionDeskFallback);
        }

        Vector3 ReturnPosition(int floor)
        {
            if (floor > 0 && _crossings != null && _crossings.TryGetBridgeCentre(floor, out Vector3 centre))
                return centre;
            return SpawnPosition();
        }

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
