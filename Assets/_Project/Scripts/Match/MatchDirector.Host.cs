using System.Collections.Generic;
using CloseTheDeal.Tower;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    public sealed partial class MatchDirector
    {
        const float DropReach = 0.7f;

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (!_live)
                return; // No profile or mode; OnStartNetwork already logged and disabled.

            _snapshot.Value = new MatchSnapshot { Phase = MatchPhase.Waiting, Winner = NoFirm };
        }

        /// <summary>True when hosting and waiting for the first match.</summary>
        public bool CanHostStart => IsServerStarted && _live && Ruleset.Ready && Phase == MatchPhase.Waiting;

        /// <summary>True when hosting and a match result is in.</summary>
        public bool CanHostRematch => IsServerStarted && _live && Ruleset.Ready && Phase == MatchPhase.Ended;

        /// <summary>Host only. Starts the first match with current joiner firms.</summary>
        public void HostStart()
        {
            if (CanHostStart)
                Ruleset.ServerNewMatch();
        }

        /// <summary>Host only. Starts a fresh match after a result.</summary>
        public void HostRematch()
        {
            if (CanHostRematch)
                Ruleset.ServerNewMatch();
        }

        /// <summary>Host only. Applies lobby firm assignments and starts a match.</summary>
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

            Ruleset.ServerNewMatch();
            return true;
        }

        /// <summary>Host only, for the mode. Starts the countdown before the briefcase unlocks.</summary>
        public void ServerBeginCountdown()
        {
            _snapshot.Value = new MatchSnapshot { Phase = MatchPhase.Countdown, PhaseTick = TimeManager.Tick, Winner = NoFirm };
        }

        /// <summary>Host only, for the mode. Back to waiting for the host to start.</summary>
        public void ServerBeginWaiting()
        {
            _snapshot.Value = new MatchSnapshot { Phase = MatchPhase.Waiting, Winner = NoFirm };
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

        /// <summary>Host only, for the mode. Puts a fresh, never-carried briefcase down at this spot, spawning it if needed.</summary>
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

        /// <summary>Host only, for the mode. Puts the loose briefcase somewhere else, keeping who carried it last.</summary>
        public void ServerMoveBriefcase(Vector3 position)
        {
            ServerPutDown(position, Quaternion.identity, Vector3.zero);
        }

        /// <summary>Host only, for the mode. Puts the briefcase in this player's hands whatever its state.</summary>
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
            _briefcase.Value = status;
        }

        /// <summary>Host only, for the mode. Records which floor the briefcase is on, for the HUD and the result.</summary>
        public void ServerSetBriefcaseFloor(int floor)
        {
            BriefcaseStatus status = _briefcase.Value;
            if (status.Floor == floor)
                return;

            status.Floor = (byte)floor;
            _briefcase.Value = status;
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

        /// <summary>Host only. The carrier vanished without dropping it: put it down where they last stood.</summary>
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
            _briefcase.Value = status;
        }
    }
}
