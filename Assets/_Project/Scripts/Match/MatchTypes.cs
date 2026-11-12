using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    public enum MatchPhase : byte
    {
        /// <summary>Everyone is in reception; the host has not started yet.</summary>
        Waiting = 0,
        Countdown = 1,
        Running = 2,
        /// <summary>A result is in. The host can start a rematch.</summary>
        Ended = 3
    }

    public enum MatchEndReason : byte
    {
        None = 0,
        /// <summary>A player carried their firm's briefcase onto their roof.</summary>
        BriefcaseOnRoof = 1,
        /// <summary>The clock ran out and one briefcase was on a higher floor.</summary>
        ClockHighestBriefcase = 2,
        /// <summary>The clock ran out with both briefcases on the same floor; the winner's got there first.</summary>
        ClockFirstToFloor = 3,
        /// <summary>The clock ran out and nothing separates the firms. No winner.</summary>
        ClockDeadHeat = 4
    }

    public enum BriefcaseState : byte
    {
        /// <summary>Lying loose: on the reception desk, where it was dropped, or where it was put back.</summary>
        Dropped = 0,
        Carried = 1,
        /// <summary>It left its building and is on its way back to the firm's last checkpoint.</summary>
        Returning = 2
    }

    /// <summary>Why a carrier let go. Decides how the briefcase leaves their hand.</summary>
    public enum DropCause : byte
    {
        Manual = 0,
        Knocked = 1,
        FellOut = 2,
        LeftTheGame = 3
    }

    /// <summary>One-off lines for the local player only. The HUD turns them into words.</summary>
    public enum MatchNotice : byte
    {
        RoofNeedsBriefcase = 0,
        RivalBriefcase = 1,
        FellOut = 2,
        BriefcaseKnockedLoose = 3
    }

    /// <summary>What the interact or drop key would do right now, for the local player's prompt.</summary>
    public enum MatchPrompt : byte
    {
        None = 0,
        PickUpBriefcase = 1,
        DropBriefcase = 2
    }

    /// <summary>The match as a whole. Synced host to everyone, only when it changes.</summary>
    public struct MatchSnapshot
    {
        public MatchPhase Phase;
        /// <summary>Host tick at which this phase began. Everyone works the clock out from it locally.</summary>
        public uint PhaseTick;
        /// <summary>Tower index of the winning firm, or MatchDirector.NoFirm.</summary>
        public byte Winner;
        public MatchEndReason Reason;
        /// <summary>Seconds the match had run when it ended.</summary>
        public ushort EndSeconds;
        /// <summary>Floor the deciding briefcase was on when the match ended.</summary>
        public byte EndFloor;
    }

    /// <summary>One firm's briefcase and progress. Synced host to everyone, only when it changes.</summary>
    public struct FirmStatus
    {
        public BriefcaseState State;
        /// <summary>Network object id of the player carrying it. Only meaningful while Carried.</summary>
        public int CarrierId;
        /// <summary>Floor the briefcase is on, counted from reception = 0, in its own tower.</summary>
        public byte BriefcaseFloor;
        /// <summary>The firm's last saved checkpoint floor; 0 means reception.</summary>
        public byte CheckpointFloor;
        /// <summary>Where it was last let go or put back, in world space.</summary>
        public Vector3 Position;
    }
}
