namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// The rules that pick a winner, as plain functions with no Unity objects, so the same
    /// inputs always give the same answer. Firms are 0 and 1; NoFirm means nobody.
    /// </summary>
    public static class MatchRules
    {
        public const byte NoFirm = byte.MaxValue;

        /// <summary>Tick value meaning "this briefcase has never been on that floor".</summary>
        public const uint Never = uint.MaxValue;

        /// <summary>
        /// The clock ran out. The briefcase on the higher floor wins; on the same floor, the
        /// one that got there first; if even that is level, nobody does.
        /// </summary>
        public static byte ClockWinner(int floorA, int floorB, uint reachedA, uint reachedB, out MatchEndReason reason)
        {
            if (floorA != floorB)
            {
                reason = MatchEndReason.ClockHighestBriefcase;
                return floorA > floorB ? (byte)0 : (byte)1;
            }

            if (reachedA != reachedB)
            {
                reason = MatchEndReason.ClockFirstToFloor;
                return reachedA < reachedB ? (byte)0 : (byte)1;
            }

            reason = MatchEndReason.ClockDeadHeat;
            return NoFirm;
        }

        /// <summary>
        /// One or both firms carried their briefcase onto their roof in the same tick. Alone,
        /// that firm wins. Together, the one whose briefcase reached the floor below the roof
        /// first; if that is level too, the firm in building A, so there is always one winner.
        /// </summary>
        public static byte RoofWinner(bool firmA, bool firmB, uint reachedBelowRoofA, uint reachedBelowRoofB)
        {
            if (firmA != firmB)
                return firmA ? (byte)0 : (byte)1;
            if (!firmA)
                return NoFirm;

            return reachedBelowRoofB < reachedBelowRoofA ? (byte)1 : (byte)0;
        }
    }
}
