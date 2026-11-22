namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// A game mode's rules. MatchDirector keeps the briefcase, phases, pick-up and drop; the mode supplies the rest.
    /// It registers itself in MatchDirector.Ruleset before the network starts. Design: docs/systems/FLOOR_RACE.md.
    /// </summary>
    public interface IMatchRuleset
    {
        /// <summary>Host only. True when the host may start or restart a match.</summary>
        bool Ready { get; }

        /// <summary>The floor in play, counted the way the mode counts it; match records keep each player's best.</summary>
        int CurrentFloor { get; }

        /// <summary>Host only. The host pressed start or rematch.</summary>
        void ServerNewMatch();

        /// <summary>Host only, once per tick: the mode's referee.</summary>
        void ServerStep(uint tick);

        /// <summary>Where the briefcase is, in words for the HUD.</summary>
        string PlaceLabel(BriefcaseStatus status);

        /// <summary>One line on how the match ended, for the results UI.</summary>
        string ReasonLine(MatchSnapshot result, int localFirm);

        /// <summary>True while the briefcase's beacon and screen marker must not show, such as before anyone has found it.</summary>
        bool BriefcaseHidden(BriefcaseStatus status);
    }
}
