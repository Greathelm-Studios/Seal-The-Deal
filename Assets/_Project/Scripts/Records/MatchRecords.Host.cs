using System.Collections.Generic;
using CloseTheDeal.Combat;
using CloseTheDeal.Items;
using CloseTheDeal.MatchMode;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace CloseTheDeal.Records
{
    public sealed partial class MatchRecords
    {
        struct LastHit
        {
            public int Attacker;
            public float Time;
        }

        readonly Dictionary<int, MatchLine> _tally = new();
        readonly Dictionary<int, float> _joinedAt = new();
        readonly Dictionary<int, LastHit> _lastHit = new();
        readonly HashSet<Pickup> _watchedThrows = new();

        bool _running;
        bool _serverHooked;
        uint _hostKey;
        int _carrierClient = -1;
        float _carryStart;
        float _carryCarried;
        float _nextFloorCheck;

        bool IsHost => _net != null && _net.IsServerStarted;
        bool IsHostCounting => IsHost && _running;

        void HostHookNetwork(NetworkManager net)
        {
            net.ServerManager.OnServerConnectionState += HostServerConnectionChanged;
            if (net.IsServerStarted)
                HostHookServerEvents();
        }

        void HostUnhookNetwork(NetworkManager net)
        {
            net.ServerManager.OnServerConnectionState -= HostServerConnectionChanged;
            HostUnhookServerEvents();
        }

        void HostServerConnectionChanged(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                HostHookServerEvents();
            else if (args.ConnectionState == LocalConnectionState.Stopped)
                HostUnhookServerEvents();
        }

        void HostHookServerEvents()
        {
            if (_serverHooked || _net == null)
                return;
            _serverHooked = true;
            _net.ServerManager.OnRemoteConnectionState += HostConnectionChanged;
            _net.ServerManager.OnAuthenticationResult += HostAuthenticated;
            Health.AnyDied += HostDied;
            Health.AnyDamaged += HostDamaged;
            FirmMember.OnFellOut += HostFellOut;
            FirmMember.OnRosterChanged += HostRosterChanged;
            ItemCarrier.ServerItemTaken += HostItemTaken;
            Pickup.ServerThrowLanded += HostThrowLanded;
        }

        void HostUnhookServerEvents()
        {
            if (!_serverHooked)
                return;
            _serverHooked = false;
            if (_net != null)
            {
                _net.ServerManager.OnRemoteConnectionState -= HostConnectionChanged;
                _net.ServerManager.OnAuthenticationResult -= HostAuthenticated;
            }
            Health.AnyDied -= HostDied;
            Health.AnyDamaged -= HostDamaged;
            FirmMember.OnFellOut -= HostFellOut;
            FirmMember.OnRosterChanged -= HostRosterChanged;
            ItemCarrier.ServerItemTaken -= HostItemTaken;
            Pickup.ServerThrowLanded -= HostThrowLanded;
            UnwatchThrows();
        }

        // Unhook consumed pickups (e.g. guns taken into slots) that never fire Released.
        void UnwatchThrows()
        {
            foreach (Pickup pickup in _watchedThrows)
            {
                if (pickup != null)
                    pickup.Released -= HostReleased;
            }

            _watchedThrows.Clear();
        }

        void HostHookMatch(MatchDirector match)
        {
            match.OnPhaseChanged += HostPhaseChanged;
            match.OnBriefcaseChanged += HostBriefcaseChanged;
            match.OnClosed += HostClosed;
        }

        void HostUnhookMatch(MatchDirector match)
        {
            match.OnPhaseChanged -= HostPhaseChanged;
            match.OnBriefcaseChanged -= HostBriefcaseChanged;
            match.OnClosed -= HostClosed;
        }

        void HostPhaseChanged(MatchPhase previous, MatchPhase next)
        {
            if (!IsHost)
                return;

            if (next == MatchPhase.Countdown)
                HostReset();
            else if (next == MatchPhase.Running)
                HostBeginRunning();
            else if (next == MatchPhase.Ended && _running)
                HostFinish();
            else
            {
                _running = false;
                if (_net != null && _match != null)
                    enabled = false;
            }
        }

        void HostClosed()
        {
            // The match went away on the host (it stopped hosting): nothing is finished, nothing is sent.
            _running = false;
            _hostKey = 0;
            _carrierClient = -1;
            _tally.Clear();
            _joinedAt.Clear();
            _lastHit.Clear();
            _carried.Clear();
            UnwatchThrows();
            if (_net != null && _match != null)
                enabled = false;
        }

        void HostReset()
        {
            _running = false;
            _tally.Clear();
            _joinedAt.Clear();
            _lastHit.Clear();
            _carrierClient = -1;
            _carryCarried = 0f;
            _carried.Clear();
            UnwatchThrows();
            // The countdown's start tick names the match; the host's own copy hears it like everyone else's.
            _hostKey = _match.Snapshot.PhaseTick;
            _net.ServerManager.Broadcast(new MatchResetBroadcast { MatchKey = _hostKey });
        }

        void HostBeginRunning()
        {
            _running = true;
            enabled = true;
            _nextFloorCheck = 0f;
            HostAddMissingPlayers();
        }

        void HostFinish()
        {
            HostEndCarry(Time.time);
            _running = false;
            if (_net != null && _match != null)
                enabled = false;
            MatchSnapshot snapshot = _match.Snapshot;
            float end = Time.time;
            IReadOnlyList<FirmMember> members = FirmMember.All;
            byte players = (byte)Mathf.Min(_tally.Count, byte.MaxValue);
            for (int i = 0; i < members.Count; i++)
            {
                NetworkConnection owner = members[i].Owner;
                // Only players with a line took part; anyone else gets no record of a match they didn't play.
                if (owner == null || !owner.IsValid || !_tally.ContainsKey(owner.ClientId))
                    continue;

                MatchLine line = HostLine(members[i]);
                float joined = _joinedAt.TryGetValue(line.ClientId, out float at) ? at : end;
                line.SecondsPlayed = Seconds(end - joined);
                HostPut(line);
                var final = new MatchFinal
                {
                    MatchKey = _hostKey,
                    Winner = snapshot.Winner,
                    Reason = (byte)snapshot.Reason,
                    EndSeconds = snapshot.EndSeconds,
                    Players = players,
                    Line = line
                };
                _net.ServerManager.Broadcast(owner, new MatchFinalBroadcast { Final = final });
            }
        }

        void HostRosterChanged()
        {
            if (IsHostCounting)
                HostAddMissingPlayers();
        }

        void HostAddMissingPlayers()
        {
            IReadOnlyList<FirmMember> members = FirmMember.All;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Owner != null && members[i].Owner.IsValid && !_tally.ContainsKey(members[i].Owner.ClientId))
                    HostPut(HostLine(members[i]));
            }
        }

        void HostConnectionChanged(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped)
                return;

            // A leaver's numbers go with them: nothing is written for a match they didn't finish. Ids are
            // reused by the transport, so nothing of theirs may be left for the next player with this id.
            int id = connection.ClientId;
            if (_carrierClient == id)
                HostEndCarry(Time.time);
            _joinedAt.Remove(id);
            _carried.Remove(id);
            ForgetHits(id);
            if (!_tally.TryGetValue(id, out MatchLine line))
                return;
            _tally.Remove(id);
            _net.ServerManager.Broadcast(new MatchLineBroadcast { MatchKey = _hostKey, Gone = true, Line = line });
        }

        readonly List<int> _forget = new();

        void ForgetHits(int clientId)
        {
            _lastHit.Remove(clientId);
            _forget.Clear();
            foreach (KeyValuePair<int, LastHit> hit in _lastHit)
            {
                if (hit.Value.Attacker == clientId)
                    _forget.Add(hit.Key);
            }

            for (int i = 0; i < _forget.Count; i++)
                _lastHit.Remove(_forget[i]);
        }

        void HostAuthenticated(NetworkConnection connection, bool passed)
        {
            if (!passed || _hostKey == 0)
                return;

            // A late joiner gets the match's lines so far; their own appears when their player does.
            _net.ServerManager.Broadcast(connection, new MatchResetBroadcast { MatchKey = _hostKey });
            foreach (MatchLine line in _tally.Values)
                _net.ServerManager.Broadcast(connection, new MatchLineBroadcast { MatchKey = _hostKey, Line = line });
        }

        MatchLine HostLine(FirmMember member)
        {
            int id = member.Owner.ClientId;
            if (_tally.TryGetValue(id, out MatchLine line))
            {
                // A late joiner's line can be made before the host seats them in a firm; follow the firm they play for.
                line.Firm = (byte)member.Firm;
                return line;
            }

            if (!_joinedAt.ContainsKey(id))
                _joinedAt[id] = Time.time;
            return new MatchLine { ClientId = id, Firm = (byte)member.Firm };
        }

        void HostPut(MatchLine line)
        {
            _tally[line.ClientId] = line;
            _net.ServerManager.Broadcast(new MatchLineBroadcast { MatchKey = _hostKey, Line = line });
        }

        static FirmMember MemberOf(int clientId)
        {
            IReadOnlyList<FirmMember> members = FirmMember.All;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Owner != null && members[i].Owner.ClientId == clientId)
                    return members[i];
            }

            return null;
        }

        static ushort Seconds(float seconds) => (ushort)Mathf.Clamp(Mathf.FloorToInt(seconds), 0, ushort.MaxValue);
        static ushort Add(ushort value) => value == ushort.MaxValue ? value : (ushort)(value + 1);

        void HostDamaged(Health health, DamageInfo info)
        {
            if (!IsHostCounting || info.AttackerClientId < 0 || !health.TryGetComponent(out FirmMember victim) || victim.Owner == null)
                return;
            // Only a rival's hit can earn a window kill, so a teammate's graze never wipes it.
            if (Rival(victim, info.AttackerClientId) == null)
                return;

            _lastHit[victim.Owner.ClientId] = new LastHit { Attacker = info.AttackerClientId, Time = Time.time };
        }

        void HostDied(Health health, DamageInfo info)
        {
            if (!IsHostCounting || !health.TryGetComponent(out FirmMember victim) || victim.Owner == null || !victim.Owner.IsValid)
                return;

            MatchLine line = HostLine(victim);
            line.Deaths = Add(line.Deaths);
            HostPut(line);
            // The killing hit is credited here; it must not also earn a window kill if the body is thrown outside.
            _lastHit.Remove(line.ClientId);
            FirmMember killer = Rival(victim, info.AttackerClientId);
            if (killer != null)
            {
                MatchLine credit = HostLine(killer);
                credit.Kills = Add(credit.Kills);
                HostPut(credit);
            }

            HostFeed(line.ClientId, killer, killer != null ? KillKind.Killed : KillKind.Died);
        }

        void HostFellOut(FirmMember victim)
        {
            // A body that is already dead was counted as a death; being carried outside by the blast isn't a second loss.
            if (!IsHostCounting || victim.Owner == null || !victim.Owner.IsValid || IsDead(victim))
                return;

            MatchLine line = HostLine(victim);
            line.Falls = Add(line.Falls);
            HostPut(line);
            FirmMember killer = null;
            if (_lastHit.TryGetValue(line.ClientId, out LastHit hit) && Time.time - hit.Time <= _windowKillSeconds)
                killer = Rival(victim, hit.Attacker);
            if (killer != null)
            {
                MatchLine credit = HostLine(killer);
                credit.Kills = Add(credit.Kills);
                credit.WindowKills = Add(credit.WindowKills);
                HostPut(credit);
            }

            _lastHit.Remove(line.ClientId);
            HostFeed(line.ClientId, killer, killer != null ? KillKind.KnockedOut : KillKind.Fell);
        }

        static bool IsDead(FirmMember member) => member.TryGetComponent(out Health health) && health.IsDead;

        static FirmMember Rival(FirmMember victim, int attackerClientId)
        {
            if (attackerClientId < 0)
                return null;
            FirmMember attacker = MemberOf(attackerClientId);
            return attacker != null && attacker != victim && attacker.Firm != victim.Firm ? attacker : null;
        }

        void HostFeed(int victim, FirmMember killer, KillKind kind)
        {
            var entry = new KillEntry { VictimClientId = victim, KillerClientId = killer != null ? killer.Owner.ClientId : -1, Kind = kind };
            _net.ServerManager.Broadcast(new KillBroadcast { Entry = entry });
        }

        void HostBriefcaseChanged(BriefcaseStatus previous, BriefcaseStatus next)
        {
            if (!IsHostCounting)
                return;

            bool was = previous.State == BriefcaseState.Carried;
            bool now = next.State == BriefcaseState.Carried;
            bool sameHands = was && now && previous.CarrierId == next.CarrierId;
            if (was && !sameHands)
                HostEndCarry(Time.time);
            if (now && !sameHands)
                HostStartCarry(previous, next);
        }

        void HostStartCarry(BriefcaseStatus previous, BriefcaseStatus next)
        {
            FirmMember carrier = FirmMember.Find(next.CarrierId);
            if (carrier == null || carrier.Owner == null || !carrier.Owner.IsValid)
                return;

            MatchLine line = HostLine(carrier);
            line.Pickups = Add(line.Pickups);
            byte from = previous.State == BriefcaseState.Carried ? previous.CarrierFirm : previous.LastCarrierFirm;
            if (from != MatchDirector.NoFirm && from != next.CarrierFirm)
                line.Steals = Add(line.Steals);
            HostPut(line);
            _carrierClient = line.ClientId;
            _carryStart = Time.time;
            _carryCarried = CarriedSoFar(line.ClientId);
        }

        void HostEndCarry(float now)
        {
            if (_carrierClient < 0)
                return;

            int id = _carrierClient;
            _carrierClient = -1;
            if (!_tally.TryGetValue(id, out MatchLine line))
                return;

            _carried[id] = _carryCarried + (now - _carryStart);
            line.CarrySeconds = Seconds(_carried[id]);
            HostPut(line);
        }

        readonly Dictionary<int, float> _carried = new();

        float CarriedSoFar(int clientId) => _carried.TryGetValue(clientId, out float seconds) ? seconds : 0f;

        void HostItemTaken(ItemCarrier carrier, Pickup pickup)
        {
            if (!IsHostCounting)
                return;
            if (pickup != null && _watchedThrows.Add(pickup))
                pickup.Released += HostReleased;
        }

        void HostReleased(Pickup pickup, ReleaseKind kind)
        {
            pickup.Released -= HostReleased;
            _watchedThrows.Remove(pickup);
            if (kind != ReleaseKind.Thrown || !IsHostCounting)
                return;

            FirmMember thrower = MemberOf(pickup.LastHolderClientId);
            if (thrower == null)
                return;

            MatchLine line = HostLine(thrower);
            line.Throws = Add(line.Throws);
            HostPut(line);
        }

        void HostThrowLanded(Pickup item, int throwerClientId, GameObject hit, float damage)
        {
            if (!IsHostCounting || hit == null)
                return;
            FirmMember thrower = MemberOf(throwerClientId);
            if (thrower == null)
                return;
            FirmMember struck = hit.GetComponentInParent<FirmMember>();
            if (struck == null || struck.Owner == null || Rival(struck, throwerClientId) == null)
                return;

            MatchLine line = HostLine(thrower);
            line.ThrowHits = Add(line.ThrowHits);
            HostPut(line);
        }

        void HostSampleFloors()
        {
            if (Time.time < _nextFloorCheck || _match == null)
                return;

            _nextFloorCheck = Time.time + _floorCheckSeconds;
            IReadOnlyList<FirmMember> members = FirmMember.All;
            for (int i = 0; i < members.Count; i++)
            {
                FirmMember member = members[i];
                if (member.Owner == null || !_tally.TryGetValue(member.Owner.ClientId, out MatchLine line))
                    continue;

                // A late joiner's line is made before the host seats them in a firm; correct it once that lands.
                int floor = MatchDirector.Ruleset != null ? MatchDirector.Ruleset.CurrentFloor : 0;
                bool firmMoved = line.Firm != member.Firm;
                if (floor <= line.BestFloor && !firmMoved)
                    continue;

                line.Firm = (byte)member.Firm;
                line.BestFloor = (byte)Mathf.Clamp(Mathf.Max(floor, line.BestFloor), 0, byte.MaxValue);
                HostPut(line);
            }
        }
    }
}
