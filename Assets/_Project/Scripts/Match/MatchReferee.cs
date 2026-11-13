using FishNet.Managing.Timing;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// Host only. Applies the rules once per tick, after physics: who fell out, who dropped
    /// the briefcase, where the briefcase is, checkpoints, the roof and the clock. It reads
    /// positions and calls the director's host methods; it sends nothing itself. Players are
    /// checked in one fixed order within the tick, so the result never depends on timing.
    /// </summary>
    public sealed class MatchReferee
    {
        readonly MatchDirector _match;

        /// <summary>How far from the gate a player is told about it, in metres: beyond the panels, below the floor (the top of the flight), and above it.</summary>
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

        /// <summary>New towers, new round: re-read the tuning.</summary>
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

            // Falls are handled in every phase, so nobody waits out a lobby or a result in the street.
            StepMembers(running);
            if (running)
                Decide(tick);
        }

        // ---- Players -------------------------------------------------------------------------

        void StepMembers(bool running)
        {
            _roofWinner = MatchDirector.NoFirm;
            for (int i = FirmMember.All.Count - 1; i >= 0; i--)
            {
                FirmMember member = FirmMember.All[i];
                Vector3 position = member.Position;
                if (FellOut(member, position) || !running)
                    continue;

                byte place = Arena.PlaceOf(position, 0f);
                int floor = Arena.FloorOf(position);
                bool onSurface = Arena.OnFloorSurface(position, floor);
                StepCheckpoint(member, place, floor, onSurface, position);

                bool carrying = _match.IsCarrying(member) && StepCarrier(member, position, place, floor, onSurface);
                StepGate(member, position, place, carrying);
                bool onRoof = place < MatchArena.Neutral && floor == Arena.RoofFloor && onSurface;
                StepRoof(member, onRoof, carrying);
            }
        }

        /// <summary>Outside the buildings, plaza and skybridges for long enough, or under the street: back to the firm's checkpoint, without the briefcase.</summary>
        bool FellOut(FirmMember member, Vector3 position)
        {
            bool sunk = Arena.Sunk(position);
            if (!sunk && Arena.PlaceOf(position, Profile.OutsideMargin) != MatchArena.Outside)
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

        /// <summary>Standing on a higher checkpoint floor of your own tower moves your firm's respawn point there. Never above the open floor, so nobody respawns where they could not have walked.</summary>
        void StepCheckpoint(FirmMember member, byte place, int floor, bool onSurface, Vector3 position)
        {
            int firm = member.Firm;
            if (place == firm && onSurface && member.Grounded && Arena.IsCheckpoint(floor) && floor > _match.CheckpointFloor(firm) && floor <= _match.OpenFloor)
                _match.ServerSaveCheckpoint(firm, floor, position);
        }

        /// <summary>Returns false when the carrier lost the briefcase this tick. A carrier standing on a floor above the open floor opens it for everyone.</summary>
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
                _match.ServerOpenFloor(floor);
            _match.ServerTrackBriefcase(place, floor);
            return true;
        }

        /// <summary>
        /// Someone without the briefcase at the top of the stairs to a closed floor is told why
        /// they can't go on, once per approach. The gate itself is a wall on every machine; this
        /// only gives it words.
        /// </summary>
        void StepGate(FirmMember member, Vector3 position, byte place, bool carrying)
        {
            bool atGate = _match.HasGates && !carrying && place < MatchArena.Neutral && NearClosedGate(position, place);
            if (atGate && !member.WasAtGate)
                member.Notify(MatchNotice.GateRefused);
            member.WasAtGate = atGate;
        }

        /// <summary>Within reach of the stairwell, close under or on the first closed floor.</summary>
        bool NearClosedGate(Vector3 position, int building)
        {
            int closed = _match.OpenFloor + 1;
            if (closed >= Arena.FloorCount)
                return false;

            Vector3 local = Arena.ToFloorSpace(position, building, closed) - _match.StairCentre;
            float reach = MatchGates.Radius + GateReach;
            return local.y > -GateBelow && local.y < GateAbove && local.x * local.x + local.z * local.z < reach * reach;
        }

        /// <summary>On a roof with the briefcase claims the win; without it, you are told why nothing happened, once per visit.</summary>
        void StepRoof(FirmMember member, bool onRoof, bool carrying)
        {
            if (onRoof && carrying)
                _roofWinner = (byte)member.Firm;
            else if (onRoof && !member.WasOnRoof)
                member.Notify(MatchNotice.RoofNeedsBriefcase);

            member.WasOnRoof = onRoof;
        }

        // ---- The briefcase -------------------------------------------------------------------

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

        /// <summary>A loose briefcase: sent back if it has left the buildings, plaza and skybridges for long enough; otherwise where it is is kept up to date.</summary>
        void StepLoose(Briefcase briefcase, uint tick)
        {
            Vector3 position = briefcase.Position;
            bool sunk = Arena.Sunk(position);
            byte place = Arena.PlaceOf(position, 0f);
            if (place == MatchArena.Outside && !sunk)
                place = Arena.PlaceOf(position, Profile.OutsideMargin); // Lying on a sill still counts as that building.

            briefcase.StrayTicks = place == MatchArena.Outside ? briefcase.StrayTicks + 1 : 0;
            if (sunk || briefcase.StrayTicks >= _strayTicks)
            {
                _match.ServerBeginReturn(tick);
                return;
            }

            if (place != MatchArena.Outside)
                _match.ServerTrackBriefcase(place, Arena.FloorOf(position));
        }

        // ---- The result ----------------------------------------------------------------------

        /// <summary>The roof ends the match at once; otherwise the clock does when it runs out.</summary>
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
