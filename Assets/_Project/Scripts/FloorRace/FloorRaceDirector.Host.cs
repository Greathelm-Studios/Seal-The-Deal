using System;
using System.Collections.Generic;
using CloseTheDeal.Combat;
using CloseTheDeal.FloorGen;
using CloseTheDeal.Integration;
using CloseTheDeal.Items;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Tower;
using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.FloorRace
{
    public sealed partial class FloorRaceDirector
    {
        const float PlayerHeight = 1.8f, PlayerRadius = 0.35f;
        const float SpawnRing = 1f;
        const int SpawnRingPoints = 6;
        const float SpawnLift = 0.1f;
        const float BoardPollSeconds = 0.1f;
        const float DepartLeadSeconds = 0.25f;
        const float CaseReturnHeight = 0.3f;
        const float OfficeInset = 0.4f;
        const float EyeHeight = 1.6f;
        const float FallKill = 100000f;

        FloorRaceLoot _loot;
        readonly FloorRaceSpawns _spawns = new();
        readonly List<Vector3> _rivalEyes = new();
        readonly uint[] _wipeTick = new uint[MatchDirector.FirmCount];
        bool _freshMatch;
        int _liftCarrierId = -1;
        int _startAFirm;
        int _pollTicks;
        Vector3 _caseHome;
        bool _hasCaseHome;

        MatchDirector Match => MatchDirector.Instance;

        public bool Ready => _floor != null && (Stage == RaceStage.Idle || Stage == RaceStage.Playing);

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (!_networked)
                return;

            _loot = new FloorRaceLoot(NetworkManager, _profile);
            _freshMatch = false;
            _liftCarrierId = -1;
            _startAFirm = 0;
            Array.Clear(_wipeTick, 0, _wipeTick.Length);
            TeamSpawner.SpawnOverride = ChooseJoinSpawn;
            GunPickups.SpawnLoose = _loot.SpawnLoose;
            SetState(new FloorRaceState { Seed = PickSeed(), Floor = 1, Stage = RaceStage.Idle, StageTick = TimeManager.Tick, LiftFirm = MatchDirector.NoFirm });
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (TeamSpawner.SpawnOverride == ChooseJoinSpawn)
                TeamSpawner.SpawnOverride = null;
            if (_loot != null && GunPickups.SpawnLoose == _loot.SpawnLoose)
                GunPickups.SpawnLoose = null;
            _loot = null;
        }

        void SetState(FloorRaceState state)
        {
            _state.Value = state;
            ShowFloor(state);
        }

        void SetStage(RaceStage stage, uint tick)
        {
            FloorRaceState state = _state.Value;
            state.Stage = stage;
            state.StageTick = tick;
            SetState(state);
        }

        public void ServerNewMatch()
        {
            if (!IsServerStarted || !Ready)
                return;

            ForceDropItems();
            _loot.DespawnAll();
            uint seed = PickSeed();
            if (seed == _state.Value.Seed)
                seed++;
            _freshMatch = true;
            _liftCarrierId = -1;
            _startAFirm = 0;
            _hasCaseHome = false;
            Array.Clear(_wipeTick, 0, _wipeTick.Length);
            Match.ServerBeginWaiting();
            SetState(new FloorRaceState { Seed = seed, Floor = 1, Stage = RaceStage.Building, StageTick = TimeManager.Tick, LiftFirm = MatchDirector.NoFirm });
        }

        public void ServerStep(uint tick)
        {
            if (_floor == null || Match == null)
                return;

            FloorRaceState state = _state.Value;
            float since = StageSeconds;
            switch (state.Stage)
            {
                case RaceStage.Building:
                    if (since >= _profile.BuildLeadSeconds)
                        Arrive(tick);
                    break;
                case RaceStage.HeadStart:
                    if (since >= _profile.HeadStartSeconds)
                        SetStage(RaceStage.Playing, tick);
                    break;
                case RaceStage.Departing:
                    if (since >= _profile.DoorSeconds + _profile.DepartHoldSeconds)
                        LeaveFloor(tick);
                    break;
            }

            MatchPhase phase = Match.Phase;
            if (phase == MatchPhase.Countdown && Match.ElapsedTicks(tick) >= TimeManager.TimeToTicks(Match.Profile.CountdownSeconds, TickRounding.RoundUp))
            {
                Match.ServerBeginRunning(tick);
                phase = MatchPhase.Running;
            }

            StepMembers();
            StepBriefcase();
            StepWipes(tick);

            if (_state.Value.Stage != RaceStage.Playing || phase != MatchPhase.Running)
                return;
            if (IsTopFloor)
            {
                StepOffice(tick);
                return;
            }

            if (++_pollTicks < TimeManager.TimeToTicks(BoardPollSeconds, TickRounding.RoundUp))
                return;
            _pollTicks = 0;
            StepBoarding(tick);
        }

        void StepMembers()
        {
            BuiltFloor floor = StandingFloor;
            FloorLayout layout = floor.Layout;
            float margin = _profile.OutsideMargin;
            int fallTicks = (int)TimeManager.TimeToTicks(_profile.FallSeconds, TickRounding.RoundUp);
            for (int i = FirmMember.All.Count - 1; i >= 0; i--)
            {
                FirmMember member = FirmMember.All[i];
                Health health = HealthOf(member);
                bool dead = health != null && health.IsDead;
                bool carrying = Match.IsCarrying(member);
                if (carrying && (dead || member.Knocked))
                {
                    Match.ServerDrop(member, DropCause.Knocked);
                    carrying = false;
                }

                if (dead)
                {
                    member.OutsideTicks = 0;
                    continue;
                }

                Vector3 world = member.Position;
                Vector3 plan = floor.Plan(world);
                bool outside = plan.x < -margin || plan.y < -margin || plan.x > layout.Width + margin || plan.y > layout.Depth + margin;
                bool below = plan.z < -_profile.FallDepth || float.IsNaN(plan.x);
                if (!outside && !below)
                {
                    member.OutsideTicks = 0;
                    if (carrying)
                    {
                        Briefcase.Current.LastCarrierPosition = world;
                        _caseHome = world;
                        _hasCaseHome = true;
                        Match.ServerSetBriefcaseFloor(layout.Floor);
                    }
                    continue;
                }

                if (!below && ++member.OutsideTicks < fallTicks)
                    continue;

                member.OutsideTicks = 0;
                if (carrying)
                    Match.ServerDrop(member, DropCause.FellOut);
                if (health != null)
                    health.ApplyDamage(FallKill, world, Vector3.down, -1);
                member.Notify(MatchNotice.FellOut);
                member.AnnounceFellOut();
            }
        }

        void StepBriefcase()
        {
            Briefcase briefcase = Briefcase.Current;
            if (briefcase == null)
                return;

            BriefcaseStatus status = Match.CaseStatus;
            if (status.State == BriefcaseState.Carried)
            {
                if (Match.Carrier() == null)
                    Match.ServerDropOrphan();
                return;
            }

            BuiltFloor floor = StandingFloor;
            FloorLayout layout = floor.Layout;
            Vector3 world = briefcase.Position;
            Vector3 plan = floor.Plan(world);
            bool inside = plan.x >= 0f && plan.y >= 0f && plan.x <= layout.Width && plan.y <= layout.Depth && plan.z > -1f && plan.z < _builder.FloorPitch - 1f;
            if (inside)
            {
                briefcase.StrayTicks = 0;
                _caseHome = world;
                _hasCaseHome = true;
                return;
            }

            if (++briefcase.StrayTicks < (int)TimeManager.TimeToTicks(Match.Profile.StraySeconds, TickRounding.RoundUp))
                return;

            // It went out of a window or through the floor: back to the spawn spot nearest where it was last inside.
            briefcase.StrayTicks = 0;
            Match.ServerMoveBriefcase(NearestSpot(floor, _hasCaseHome ? _caseHome : world) + Vector3.up * CaseReturnHeight);
        }

        static Vector3 NearestSpot(BuiltFloor floor, Vector3 world)
        {
            Vector3 plan = floor.Plan(world);
            FloorLayout layout = floor.Layout;
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < layout.SpawnSpots.Count; i++)
            {
                FloorSpot spot = layout.SpawnSpots[i];
                float distance = (spot.X - plan.x) * (spot.X - plan.x) + (spot.Y - plan.y) * (spot.Y - plan.y);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return floor.World(layout.SpawnSpots[best].X, layout.SpawnSpots[best].Y);
        }

        // A firm comes back only when all of it is down, together, near the briefcase.
        void StepWipes(uint tick)
        {
            RaceStage stage = _state.Value.Stage;
            if (stage == RaceStage.Departing || stage == RaceStage.Building)
            {
                Array.Clear(_wipeTick, 0, _wipeTick.Length);
                return;
            }

            for (int firm = 0; firm < MatchDirector.FirmCount; firm++)
            {
                if (!FirmIsDown(firm))
                {
                    _wipeTick[firm] = 0;
                    continue;
                }

                if (_wipeTick[firm] == 0)
                    _wipeTick[firm] = tick + Math.Max(1u, TimeManager.TimeToTicks(_profile.WipeRespawnSeconds, TickRounding.RoundUp));
                if (tick < _wipeTick[firm])
                    continue;

                _wipeTick[firm] = 0;
                RespawnFirm(firm);
            }
        }

        void RespawnFirm(int firm)
        {
            _rivalEyes.Clear();
            for (int i = 0; i < FirmMember.All.Count; i++)
            {
                FirmMember member = FirmMember.All[i];
                if (member.Firm != firm && !IsDown(member))
                    _rivalEyes.Add(member.Position + Vector3.up * EyeHeight);
            }

            BuiltFloor floor = _floor;
            Vector3 briefcase = Briefcase.Current != null && Match.CaseStatus.State != BriefcaseState.Carried
                ? Briefcase.Current.Position
                : Match.Carrier() != null ? Match.Carrier().Position : floor.World(floor.Layout.Width * 0.5f, floor.Layout.Depth * 0.5f);
            Vector3 spot = _spawns.Pick(floor, briefcase, _rivalEyes, _profile, Match.SolidMask);
            for (int i = 0; i < FirmMember.All.Count; i++)
            {
                FirmMember member = FirmMember.All[i];
                if (member.Firm == firm)
                    PutAt(member, FreeSpotNear(spot, member.Slot));
            }

            Debug.Log($"[Floor Race] {Match.FirmName(firm)} was wiped and came back at {spot}.");
        }

        void StepBoarding(uint tick)
        {
            FirmMember carrier = Match.Carrier();
            if (carrier == null || IsDown(carrier) || !InExitLift(carrier.Position))
                return;

            CountAboard(carrier.Firm, out int aboard, out int living);
            if (aboard < living)
                return;

            _liftCarrierId = carrier.ObjectId;
            FloorRaceState state = _state.Value;
            state.Stage = RaceStage.Departing;
            state.StageTick = tick + TimeManager.TimeToTicks(DepartLeadSeconds, TickRounding.RoundUp);
            state.LiftFirm = (byte)carrier.Firm;
            SetState(state);
            Debug.Log($"[Floor Race] {Match.FirmName(carrier.Firm)} boarded the lift on floor {state.Floor}.");
        }

        void StepOffice(uint tick)
        {
            FirmMember carrier = Match.Carrier();
            if (carrier == null || IsDown(carrier))
                return;

            FloorLayout layout = _floor.Layout;
            Vector3 plan = _floor.Plan(carrier.Position);
            bool inOffice = layout.HasOffice && plan.x > layout.OfficeX0 + OfficeInset && plan.x < layout.OfficeX1 - OfficeInset
                && plan.y > layout.OfficeY0 + OfficeInset && plan.y < layout.OfficeY1 - OfficeInset && plan.z > -0.3f && plan.z < 3f;
            if (!inOffice)
                return;

            Pay(carrier.Firm, _profile.CashPerFloor + _profile.WinBonusCash, layout.Floor);
            Match.ServerEnd((byte)carrier.Firm, MatchEndReason.ManagersOffice, tick);
        }

        // Doors are shut: pay the firm, clear the floor's loot, and show every machine the next floor.
        void LeaveFloor(uint tick)
        {
            FloorRaceState state = _state.Value;
            Pay(state.LiftFirm, _profile.CashPerFloor, state.Floor);
            ForceDropItems();
            _loot.DespawnAll();
            _hasCaseHome = false;
            state.Floor++;
            state.Stage = RaceStage.Building;
            state.StageTick = tick;
            SetState(state);
        }

        // Every machine has had the build lead to finish the new floor: move everyone onto it.
        void Arrive(uint tick)
        {
            FloorLayout layout = _floor.Layout;
            FloorRaceState state = _state.Value;
            _startAFirm = state.LiftFirm == MatchDirector.NoFirm ? 0 : state.LiftFirm;
            StartingLoadout loadout = StartingLoadout.Instance;
            for (int i = 0; i < FirmMember.All.Count; i++)
            {
                FirmMember member = FirmMember.All[i];
                PutAt(member, StartOf(member.Firm, member.Slot));
                if (!_freshMatch)
                    continue;

                Health health = HealthOf(member);
                if (health != null)
                    health.Restore();
                if (member.TryGetComponent(out WeaponHolder guns))
                    StartingGuns(guns, loadout);
            }

            _loot.SpawnFloor(_floor);
            if (_freshMatch)
            {
                _freshMatch = false;
                if (layout.HasBriefcase)
                {
                    Vector3 spot = _floor.World(layout.Briefcase.X, layout.Briefcase.Y, CaseReturnHeight);
                    Match.ServerPlaceBriefcase(spot, Quaternion.identity, layout.Floor);
                    _caseHome = spot;
                    _hasCaseHome = true;
                }
                else
                {
                    Debug.LogError("[Floor Race] Floor 1 has no briefcase spot; the match cannot be played.");
                }

                Match.ServerBeginCountdown();
                SetStage(RaceStage.Playing, tick);
                return;
            }

            FirmMember carrier = FirmMember.Find(_liftCarrierId);
            if (carrier == null || carrier.Firm != state.LiftFirm)
                carrier = FirstOf(state.LiftFirm);
            if (carrier != null)
                Match.ServerHandBriefcase(carrier);
            Match.ServerSetBriefcaseFloor(layout.Floor);
            SetStage(RaceStage.HeadStart, tick);
        }

        static FirmMember FirstOf(int firm)
        {
            for (int i = 0; i < FirmMember.All.Count; i++)
                if (FirmMember.All[i].Firm == firm)
                    return FirmMember.All[i];
            return null;
        }

        Vector3 StartOf(int firm, int slot)
        {
            FloorLayout layout = _floor.Layout;
            List<FloorSpot> spots = firm == _startAFirm ? layout.StartA : layout.StartB;
            if (spots.Count == 0)
                spots = layout.SpawnSpots;
            FloorSpot spot = spots[Mathf.Abs(slot) % spots.Count];
            return _floor.World(spot.X, spot.Y, SpawnLift);
        }

        // Alive or dead, the player ends up standing here.
        void PutAt(FirmMember member, Vector3 position)
        {
            Health health = HealthOf(member);
            if (health == null || !health.ServerRespawnAt(position))
                member.ServerTeleport(position);
            member.OutsideTicks = 0;
        }

        bool ChooseJoinSpawn(int team, int slot, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (_floor == null)
                return false;

            position = StartOf(team, slot);
            return true;
        }

        void Pay(int firm, int amount, int floor)
        {
            if (amount <= 0 || firm == MatchDirector.NoFirm)
                return;

            for (int i = 0; i < FirmMember.All.Count; i++)
            {
                FirmMember member = FirmMember.All[i];
                if (member.Firm == firm && member.Owner.IsValid)
                    TargetCash(member.Owner, amount, (byte)floor);
            }
        }

        // Loot is despawned with its floor; anything in a player's hands must be let go first.
        static void ForceDropItems()
        {
            for (int i = 0; i < FirmMember.All.Count; i++)
            {
                ItemCarrier carrier = PlayerParts.CarrierOf(FirmMember.All[i]);
                if (carrier != null)
                    carrier.ServerForceDrop();
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
                    Debug.LogWarning($"[Floor Race] Starting gun '{start.name}' is not in the weapon catalog; slot {slot + 1} kept what it had.");
            }
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
                    return candidate + Vector3.up * SpawnLift;
            }

            return spot + Vector3.up * SpawnLift;
        }

        bool Standable(Vector3 feet)
        {
            int solid = Match.SolidMask;
            if (!Physics.Raycast(feet + Vector3.up * 0.5f, Vector3.down, 0.8f, solid, QueryTriggerInteraction.Ignore))
                return false;

            Vector3 low = feet + Vector3.up * (PlayerRadius + 0.1f);
            Vector3 high = feet + Vector3.up * (PlayerHeight - PlayerRadius);
            return !Physics.CheckCapsule(low, high, PlayerRadius, solid, QueryTriggerInteraction.Ignore);
        }

        static uint PickSeed()
        {
            uint seed = unchecked((uint)Environment.TickCount) ^ (uint)UnityEngine.Random.Range(1, int.MaxValue);
            return seed == 0 ? 1u : seed;
        }
    }
}
