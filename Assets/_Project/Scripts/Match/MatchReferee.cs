using FishNet.Managing.Timing;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>Host-only per-tick arbiter evaluating falls, checkpoints, briefcase tracking, gates, roofs, and clock expiry.</summary>
    public sealed class MatchReferee
    {
        readonly MatchDirector _match;

        const float GateReach = 0.8f;
        const float GateBelow = 1.5f;
        const float GateAbove = 1.2f;

        uint _countdownTicks;
        uint _matchTicks;
        int _fallTicks;
        int _strayTicks;
        byte _roofWinner;

        MatchProfile Profile => _match.Profile;
        MatchArena Arena => _match.Arena;

        public MatchReferee(MatchDirector match)
        {
            _match = match;
        }

        /// <summary>Recomputes tick durations after tower build.</summary>
        public void ResetRound()
        {
            TimeManager time = _match.TimeManager;
            _countdownTicks = time.TimeToTicks(Profile.CountdownSeconds, TickRounding.RoundUp);
            _matchTicks = time.TimeToTicks(Profile.MatchSeconds, TickRounding.RoundUp);
            _fallTicks = (int)time.TimeToTicks(Profile.FallSeconds, TickRounding.RoundUp);
            _strayTicks = (int)time.TimeToTicks(Profile.StraySeconds, TickRounding.RoundUp);
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
            StepBriefcase(tick);

            // Handle falls in every phase so nobody waits out a lobby or result in the street.
            StepMembers(running);
            if (running)
                Decide(tick);
        }

        void StepMembers(bool running)
        {
            _roofWinner = MatchDirector.NoFirm;
            for (int i = FirmMember.All.Count - 1; i >= 0; i--)
            {
                FirmMember member = FirmMember.All[i];
                Vector3 position = member.Position;
                Arena.Classify(position, Profile.OutsideMargin, out bool sunk, out byte place, out byte paddedPlace, out int floor, out bool onSurface);
                if (FellOut(member, sunk, paddedPlace) || !running)
                    continue;

                StepCheckpoint(member, place, floor, onSurface, position);

                bool carrying = _match.IsCarrying(member) && StepCarrier(member, position, place, floor, onSurface);
                StepGate(member, position, place, carrying);
                bool onRoof = place < MatchArena.Neutral && floor == Arena.RoofFloor && onSurface;
                StepRoof(member, onRoof, carrying);
            }
        }

        bool FellOut(FirmMember member, bool sunk, byte paddedPlace)
        {
            if (!sunk && paddedPlace != MatchArena.Outside)
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

        void StepCheckpoint(FirmMember member, byte place, int floor, bool onSurface, Vector3 position)
        {
            int firm = member.Firm;
            if (place == firm && onSurface && member.Grounded && Arena.IsCheckpoint(floor) && floor > _match.CheckpointFloor(firm) && floor <= _match.OpenFloor)
                _match.ServerSaveCheckpoint(firm, floor, position);
        }

        bool StepCarrier(FirmMember member, Vector3 position, byte place, int floor, bool onSurface)
        {
            if (member.Knocked)
            {
                _match.ServerDrop(member, DropCause.Knocked);
                return false;
            }

            Briefcase.Current.LastCarrierPosition = position;
            if (place == MatchArena.Outside)
                return true;

            if (onSurface && floor > _match.OpenFloor)
                _match.ServerOpenFloor(floor, member.Firm);
            _match.ServerTrackBriefcase(place, floor);
            return true;
        }

        void StepGate(FirmMember member, Vector3 position, byte place, bool carrying)
        {
            bool atGate = _match.HasGates && !carrying && place < MatchArena.Neutral && NearClosedGate(position, place);
            if (atGate && !member.WasAtGate)
            {
                // First refusal explains the rule; later approaches use the short line.
                member.Notify(member.GateExplained ? MatchNotice.GateRefused : MatchNotice.FirstGateRefusal);
                member.GateExplained = true;
            }
            member.WasAtGate = atGate;
        }

        bool NearClosedGate(Vector3 position, int building)
        {
            int closed = _match.OpenFloor + 1;
            if (closed >= Arena.FloorCount)
                return false;

            Vector3 local = Arena.ToFloorSpace(position, building, closed) - _match.StairCentre;
            float reach = MatchGates.Radius + GateReach;
            return local.y > -GateBelow && local.y < GateAbove && local.x * local.x + local.z * local.z < reach * reach;
        }

        void StepRoof(FirmMember member, bool onRoof, bool carrying)
        {
            if (onRoof && carrying)
                _roofWinner = (byte)member.Firm;
            else if (onRoof && !member.WasOnRoof)
                member.Notify(MatchNotice.RoofNeedsBriefcase);

            member.WasOnRoof = onRoof;
        }

        void StepBriefcase(uint tick)
        {
            Briefcase briefcase = Briefcase.Current;
            if (briefcase == null)
                return;

            switch (_match.CaseStatus.State)
            {
                case BriefcaseState.Carried:
                    if (_match.Carrier() == null)
                        _match.ServerDropOrphan();
                    break;
                case BriefcaseState.Returning:
                    if (tick >= briefcase.ReturnAtTick)
                        _match.ServerFinishReturn();
                    break;
                default:
                    StepLoose(briefcase, tick);
                    break;
            }
        }

        void StepLoose(Briefcase briefcase, uint tick)
        {
            Vector3 position = briefcase.Position;
            Arena.Classify(position, Profile.OutsideMargin, out bool sunk, out byte place, out byte paddedPlace, out int floor, out bool onSurface);
            if (place == MatchArena.Outside && !sunk)
                place = paddedPlace; // Sills count as inside that building.

            briefcase.StrayTicks = place == MatchArena.Outside ? briefcase.StrayTicks + 1 : 0;
            if (sunk || briefcase.StrayTicks >= _strayTicks)
            {
                _match.ServerBeginReturn(tick);
                return;
            }

            if (place == MatchArena.Outside)
                return;

            // Recover loose cases resting on a locked floor above OpenFloor.
            if (floor > _match.OpenFloor && onSurface && briefcase.Resting)
            {
                _match.ServerBeginReturn(tick);
                return;
            }

            _match.ServerTrackBriefcase(place, floor);
        }

        void Decide(uint tick)
        {
            if (_roofWinner != MatchDirector.NoFirm)
            {
                _match.ServerEnd(_roofWinner, MatchEndReason.BriefcaseOnRoof, tick);
                return;
            }

            if (_match.ElapsedTicks(tick) < _matchTicks)
                return;

            byte winner = MatchRules.ClockWinner(_match.CaseStatus, out MatchEndReason reason);
            _match.ServerEnd(winner, reason, tick);
        }
    }
}
