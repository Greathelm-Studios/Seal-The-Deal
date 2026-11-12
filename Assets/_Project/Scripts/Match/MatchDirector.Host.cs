using CloseTheDeal.Tower;
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

        /// <summary>How far above a checkpoint spot a returning briefcase appears, in metres, so it settles rather than clips.</summary>
        const float ReturnHeight = 0.3f;

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
            SetSnapshot(new MatchSnapshot { Phase = MatchPhase.Waiting, Winner = NoFirm });

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

        /// <summary>Host only. Starts the first match: fresh towers, everyone to reception, countdown.</summary>
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

        /// <summary>Rebuilding the towers puts the locked doors and furniture back and changes the layout; the rest follows in ServerPrepareRound once they stand.</summary>
        void ServerNewMatch()
        {
            _gatherOnBuild = true;
            _countdownOnBuild = true;
            _tower.Rebuild();
        }

        /// <summary>The towers stand (first build or a rebuild): briefcases on their desks, progress wiped, and the round begun if one was asked for.</summary>
        void ServerPrepareRound()
        {
            _referee.ResetRound();
            for (int firm = 0; firm < FirmCount; firm++)
            {
                Vector3 home = HomePosition(firm);
                _checkpointSpot[firm] = home;
                Briefcase briefcase = EnsureBriefcase(firm, home);
                if (briefcase == null)
                    continue;

                briefcase.ServerRelease(home, _arena.Facing(firm), Vector3.zero);
                SetFirm(firm, new FirmStatus { State = BriefcaseState.Dropped, CarrierId = -1, Position = home });
            }

            if (_gatherOnBuild)
                ServerGatherInReception();
            if (_countdownOnBuild)
                SetSnapshot(new MatchSnapshot { Phase = MatchPhase.Countdown, PhaseTick = TimeManager.Tick, Winner = NoFirm });

            _gatherOnBuild = false;
            _countdownOnBuild = false;
        }

        /// <summary>One briefcase per firm for as long as the host runs: spawned once, then only ever moved.</summary>
        Briefcase EnsureBriefcase(int firm, Vector3 home)
        {
            Briefcase existing = Briefcase.Of(firm);
            if (existing != null)
                return existing;

            if (_profile.BriefcasePrefab == null)
            {
                Debug.LogError("[Match] The match profile has no briefcase prefab; the firms have nothing to carry.");
                return null;
            }

            NetworkObject spawned = NetworkManager.GetPooledInstantiated(_profile.BriefcasePrefab, home, _arena.Facing(firm), true);
            if (!spawned.TryGetComponent(out Briefcase briefcase))
            {
                Debug.LogError("[Match] The briefcase prefab has no Briefcase component.");
                Destroy(spawned.gameObject);
                return null;
            }

            briefcase.AssignFirm(firm);
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
            _referee.MarkStart(tick);
            SetSnapshot(new MatchSnapshot { Phase = MatchPhase.Running, PhaseTick = tick, Winner = NoFirm });
        }

        internal void ServerEnd(byte winner, MatchEndReason reason, uint tick)
        {
            float seconds = (float)(ElapsedTicks(tick) * TimeManager.TickDelta);
            int decidingFirm = winner == NoFirm ? 0 : winner;
            SetSnapshot(new MatchSnapshot
            {
                Phase = MatchPhase.Ended,
                PhaseTick = tick,
                Winner = winner,
                Reason = reason,
                EndSeconds = (ushort)Mathf.Clamp(Mathf.FloorToInt(seconds), 0, ushort.MaxValue),
                EndFloor = Firm(decidingFirm).BriefcaseFloor
            });
            Debug.Log($"[Match] Ended: winner {(winner == NoFirm ? "none" : FirmName(winner))}, {reason}, after {seconds:0} s.");
        }

        // ---- Briefcase outcomes --------------------------------------------------------------

        /// <summary>A player pressed interact. Their own firm's briefcase in reach is picked up; the rival's gets them told why not.</summary>
        internal void ServerTryPickUp(FirmMember member)
        {
            if (!IsServerStarted || Phase != MatchPhase.Running || member.Knocked || IsCarrying(member))
                return;

            int firm = member.Firm;
            if (CanGrab(member, firm))
            {
                Briefcase.Of(firm).ServerPark();
                FirmStatus status = Firm(firm);
                status.State = BriefcaseState.Carried;
                status.CarrierId = member.ObjectId;
                SetFirm(firm, status);
                return;
            }

            if (CanGrab(member, 1 - firm))
                member.Notify(MatchNotice.RivalBriefcase);
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

            ServerPutDown(member.Firm, position, Quaternion.LookRotation(forward, Vector3.up), velocity);
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
        internal void ServerDropOrphan(int firm)
        {
            Briefcase briefcase = Briefcase.Of(firm);
            ServerPutDown(firm, briefcase.LastCarrierPosition + Vector3.up, Quaternion.identity, Vector3.zero);
        }

        void ServerPutDown(int firm, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            Briefcase.Of(firm).ServerRelease(position, rotation, velocity);
            FirmStatus status = Firm(firm);
            status.State = BriefcaseState.Dropped;
            status.CarrierId = -1;
            status.Position = position;
            SetFirm(firm, status);
        }

        /// <summary>The briefcase left its building: it goes away, and comes back at the checkpoint after the delay.</summary>
        internal void ServerBeginReturn(int firm, uint tick)
        {
            Briefcase briefcase = Briefcase.Of(firm);
            briefcase.ServerPark();
            briefcase.ReturnAtTick = tick + TimeManager.TimeToTicks(_profile.ReturnDelaySeconds, FishNet.Managing.Timing.TickRounding.RoundUp);

            // Its progress goes back with it straight away, so the clock never credits a floor it has left.
            FirmStatus status = Firm(firm);
            status.State = BriefcaseState.Returning;
            status.CarrierId = -1;
            status.BriefcaseFloor = status.CheckpointFloor;
            SetFirm(firm, status);
        }

        internal void ServerFinishReturn(int firm)
        {
            ServerPutDown(firm, _checkpointSpot[firm] + Vector3.up * ReturnHeight, _arena.Facing(firm), Vector3.zero);
        }

        internal void ServerSetBriefcaseFloor(int firm, int floor)
        {
            FirmStatus status = Firm(firm);
            if (status.BriefcaseFloor == floor)
                return;

            status.BriefcaseFloor = (byte)floor;
            SetFirm(firm, status);
        }

        /// <summary>The carrier stood on a checkpoint floor: that floor, and the spot they stood on, is where the firm comes back to.</summary>
        internal void ServerSaveCheckpoint(int firm, int floor, Vector3 spot)
        {
            _checkpointSpot[firm] = spot;
            FirmStatus status = Firm(firm);
            status.CheckpointFloor = (byte)floor;
            SetFirm(firm, status);
        }

        // ---- Placing players -----------------------------------------------------------------

        /// <summary>Puts a player back at their firm's last checkpoint, or in reception if there is none yet.</summary>
        internal void ServerRespawn(FirmMember member)
        {
            int firm = member.Firm;
            if (Firm(firm).CheckpointFloor > 0)
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
            if (!_arena.Ready || Phase != MatchPhase.Running || Firm(team).CheckpointFloor == 0)
                return false;

            position = FreeSpotNear(_checkpointSpot[team], slot);
            rotation = _arena.Facing(team);
            return true;
        }

        Vector3 HomePosition(int firm)
        {
            return _arena.FloorPoint(firm, 0, _profile.HomeInReception);
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

        // ---- Synced state writes -------------------------------------------------------------

        void SetSnapshot(MatchSnapshot snapshot) => _snapshot.Value = snapshot;

        void SetFirm(int firm, FirmStatus status)
        {
            if (firm == 0)
                _firmA.Value = status;
            else
                _firmB.Value = status;
        }
    }
}
