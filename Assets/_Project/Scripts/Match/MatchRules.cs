namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// The rule that picks a winner when the clock runs out, as a plain function with no Unity
    /// objects, so the same state always gives the same answer. Firms are 0 and 1.
    /// </summary>
    public static class MatchRules
    {
        public const byte NoFirm = byte.MaxValue;

        /// <summary>
        /// The firm holding the briefcase wins; if it lies loose, the firm that carried it last;
        /// if nobody ever carried it, nobody.
        /// </summary>
        public static byte ClockWinner(BriefcaseStatus briefcase, out MatchEndReason reason)
        {
            if (briefcase.State == BriefcaseState.Carried && briefcase.CarrierFirm != NoFirm)
            {
                reason = MatchEndReason.ClockHolding;
                return briefcase.CarrierFirm;
            }

            if (briefcase.LastCarrierFirm != NoFirm)
            {
                reason = MatchEndReason.ClockLastCarried;
                return briefcase.LastCarrierFirm;
            }

            reason = MatchEndReason.ClockNeverCarried;
            return NoFirm;
        }
    }
}
