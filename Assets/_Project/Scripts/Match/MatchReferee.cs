using FishNet.Managing.Timing;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// Host only. Applies the rules once per tick, after physics: who fell out, who dropped
    /// the briefcase, which floor each briefcase is on, checkpoints, kicks, the roof and the
    /// clock. It reads positions and calls the director's host methods; it sends nothing itself.
    /// Players are checked in one fixed order within the tick, so two things happening in the
    /// same tick always resolve the same way.
    /// </summary>
    public sealed class MatchReferee
    {
        readonly MatchDirector _match;
        readonly bool[] _onRoofWithBriefcase = new bool[MatchDirector.FirmCount];
        uint[][] _firstReached = new uint[MatchDirector.FirmCount][];

        uint _countdownTicks;
        uint _matchTicks;
        int _fallTicks;
        int _strayTicks;
        uint _kickCooldownTicks;

        MatchProfile Profile => _match.Profile;
        MatchArena Arena => _match.Arena;

        public MatchReferee(MatchDirector match)
        {
            _match = match;
        }

        /// <summary>New towers, new round: forget which floors each briefcase has reached and re-read the tuning.</summary>
        public void ResetRound()
        {
            TimeManager time = _match.TimeManager;
            _countdownTicks = time.TimeToTicks(Profile.CountdownSeconds, TickRounding.RoundUp);
            _matchTicks = time.TimeToTicks(Profile.MatchSeconds, TickRounding.RoundUp);
            _fallTicks = (int)time.TimeToTicks(Profile.FallSeconds, TickRounding.RoundUp);
            _strayTicks = (int)time.TimeToTicks(Profile.StraySeconds, TickRounding.RoundUp);
            _kickCooldownTicks = time.TimeToTicks(Profile.KickCooldownSeconds, TickRounding.RoundUp);

            for (int firm = 0; firm < _firstReached.Length; firm++)
            {
                _firstReached[firm] = new uint[Arena.FloorCount];
                for (int floor = 0; floor < _firstReached[firm].Length; floor++)
                    _firstReached[firm][floor] = MatchRules.Never;
            }
        }

        /// <summary>The match clock started: both briefcases are in reception as of now.</summary>
        public void MarkStart(uint tick)
        {
            for (int firm = 0; firm < _firstReached.Length; firm++)
                MarkReached(firm, 0, tick);
        }

        public void Step(uint tick)
        {
            if (!Arena.Ready)
                return;

            MatchPhase phase = _match.Phase;
            if (phase == MatchPhase.Countdown && _match.ElapsedTicks(tick) >= _countdownTicks)
            {
                _match.ServerBeginRunning(tick);
                phase = MatchPhase.Running;
            }

            bool running = phase == MatchPhase.Running;
            for (int firm = 0; firm < MatchDirector.FirmCount; firm++)
                StepBriefcase(firm, tick, running);

            // Falls are handled in every phase, so nobody waits out a lobby or a result in the street.
            StepMembers(tick, running);
            if (running)
                Decide(tick);
        }

        // ---- Players -------------------------------------------------------------------------

        void StepMembers(uint tick, bool running)
        {
            _onRoofWithBriefcase[0] = false;
            _onRoofWithBriefcase[1] = false;

            // Backwards, so a respawn or a drop that changes nothing in the list is still safe if one ever does.
            for (int i = FirmMember.All.Count - 1; i >= 0; i--)
            {
                FirmMember member = FirmMember.All[i];
                Vector3 position = member.Position;
                if (FellOut(member, position) || !running)
                    continue;

                int firm = member.Firm;
                bool inOwnTower = Arena.Inside(position, firm, 0f);
                int floor = Arena.FloorOf(position, firm);
                bool carrying = _match.IsCarrying(member);
                if (carrying)
                    carrying = StepCarrier(member, position, inOwnTower, floor, tick);

                StepRoof(member, inOwnTower && floor == Arena.RoofFloor && Arena.OnFloorSurface(position, firm, floor), carrying);
            }
        }

        /// <summary>Outside both buildings for long enough, or under the street: back to the firm's checkpoint, without the briefcase.</summary>
        bool FellOut(FirmMember member, Vector3 position)
        {
            bool sunk = Arena.Sunk(position);
            if (!sunk && Arena.InsideAny(position, Profile.OutsideMargin))
            {
                member.OutsideTicks = 0;
                return false;
            }

            member.OutsideTicks++;
            if (!sunk && member.OutsideTicks < _fallTicks)
                return false;

            member.OutsideTicks = 0;
            _match.ServerDrop(member, DropCause.FellOut);
            _match.ServerRespawn(member);
            member.Notify(MatchNotice.FellOut);
            member.AnnounceFellOut();
            return true;
        }

        /// <summary>Returns false when the carrier lost the briefcase this tick.</summary>
        bool StepCarrier(FirmMember member, Vector3 position, bool inOwnTower, int floor, uint tick)
        {
            int firm = member.Firm;
            if (member.Knocked)
            {
                _match.ServerDrop(member, DropCause.Knocked);
                return false;
            }

            Briefcase.Of(firm).LastCarrierPosition = position;
            if (!inOwnTower)
                return true;

            ReachFloor(firm, floor, tick);
            bool newCheckpoint = Arena.IsCheckpoint(floor) && floor > _match.Firm(firm).CheckpointFloor;
            if (newCheckpoint && member.Grounded && Arena.OnFloorSurface(position, firm, floor))
                _match.ServerSaveCheckpoint(firm, floor, position);
            return true;
        }

        /// <summary>On your own roof with the briefcase claims the win; without it, you are told why nothing happened, once per visit.</summary>
        void StepRoof(FirmMember member, bool onRoof, bool carrying)
        {
            if (onRoof && carrying)
                _onRoofWithBriefcase[member.Firm] = true;
            else if (onRoof && !member.WasOnRoof)
                member.Notify(MatchNotice.RoofNeedsBriefcase);

            member.WasOnRoof = onRoof;
        }

        // ---- Briefcases ----------------------------------------------------------------------

        void StepBriefcase(int firm, uint tick, bool running)
        {
            Briefcase briefcase = Briefcase.Of(firm);
            if (briefcase == null)
                return;

            switch (_match.Firm(firm).State)
            {
                case BriefcaseState.Carried:
                    if (_match.Carrier(firm) == null)
                        _match.ServerDropOrphan(firm);
                    break;
                case BriefcaseState.Returning:
                    if (tick >= briefcase.ReturnAtTick)
                        _match.ServerFinishReturn(firm);
                    break;
                default:
                    StepLoose(firm, briefcase, tick, running);
                    break;
            }
        }

        /// <summary>A loose briefcase: sent back if it has left its own building, otherwise its floor is kept up to date and rivals can kick it.</summary>
        void StepLoose(int firm, Briefcase briefcase, uint tick, bool running)
        {
            Vector3 position = briefcase.Position;
            bool sunk = Arena.Sunk(position);
            bool home = !sunk && Arena.Inside(position, firm, Profile.OutsideMargin);
            briefcase.StrayTicks = home ? 0 : briefcase.StrayTicks + 1;

            if (sunk || briefcase.StrayTicks >= _strayTicks)
            {
                _match.ServerBeginReturn(firm, tick);
                return;
            }

            if (!home)
                return;

            ReachFloor(firm, Arena.FloorOf(position, firm), tick);
            if (running && tick >= briefcase.KickReadyTick)
                TryKick(firm, briefcase, tick);
        }

        /// <summary>The first rival close enough boots it away from themselves.</summary>
        void TryKick(int firm, Briefcase briefcase, uint tick)
        {
            Vector3 centre = briefcase.Centre;
            float radius = Profile.KickRadius;

            for (int i = 0; i < FirmMember.All.Count; i++)
            {
                FirmMember rival = FirmMember.All[i];
                if (rival.Firm == firm)
                    continue;

                Vector3 feet = rival.Position;
                Vector3 away = centre - feet;
                bool level = away.y > -0.3f && away.y < rival.BodyHeight;
                away.y = 0f;
                if (!level || away.sqrMagnitude > radius * radius || !_match.InReach(rival, centre))
                    continue;

                Vector3 direction = away.sqrMagnitude > 0.0001f ? away.normalized : rival.Forward;
                briefcase.ServerKick(direction * Profile.KickSpeed + Vector3.up * Profile.KickLift);
                briefcase.KickReadyTick = tick + _kickCooldownTicks;
                return;
            }
        }

        // ---- Floors and the result -----------------------------------------------------------

        void ReachFloor(int firm, int floor, uint tick)
        {
            _match.ServerSetBriefcaseFloor(firm, floor);
            MarkReached(firm, floor, tick);
        }

        /// <summary>Notes the first tick a briefcase was on this floor, and on any lower floor it skipped.</summary>
        void MarkReached(int firm, int floor, uint tick)
        {
            uint[] reached = _firstReached[firm];
            for (int i = Mathf.Min(floor, reached.Length - 1); i >= 0 && reached[i] == MatchRules.Never; i--)
                reached[i] = tick;
        }

        uint FirstReached(int firm, int floor)
        {
            uint[] reached = _firstReached[firm];
            return floor >= 0 && floor < reached.Length ? reached[floor] : MatchRules.Never;
        }

        /// <summary>The roof ends the match at once; otherwise the clock does when it runs out.</summary>
        void Decide(uint tick)
        {
            int belowRoof = Arena.RoofFloor - 1;
            byte winner = MatchRules.RoofWinner(_onRoofWithBriefcase[0], _onRoofWithBriefcase[1], FirstReached(0, belowRoof), FirstReached(1, belowRoof));
            if (winner != MatchRules.NoFirm)
            {
                _match.ServerEnd(winner, MatchEndReason.BriefcaseOnRoof, tick);
                return;
            }

            if (_match.ElapsedTicks(tick) < _matchTicks)
                return;

            int floorA = _match.Firm(0).BriefcaseFloor;
            int floorB = _match.Firm(1).BriefcaseFloor;
            winner = MatchRules.ClockWinner(floorA, floorB, FirstReached(0, floorA), FirstReached(1, floorB), out MatchEndReason reason);
            _match.ServerEnd(winner, reason, tick);
        }
    }
}
