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
        /// <summary>Values 1 to 4 are the two-tower mode's endings. They are kept because saved match history stores this number.</summary>
        BriefcaseOnRoof = 1,
        ClockHolding = 2,
        ClockLastCarried = 3,
        ClockNeverCarried = 4,
        /// <summary>Floor race: a player carried the briefcase into the manager's office on the top floor.</summary>
        ManagersOffice = 5
    }

    public enum BriefcaseState : byte
    {
        /// <summary>Lying loose: where it spawned, where it was dropped, or where it was put back.</summary>
        Dropped = 0,
        Carried = 1
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
        FellOut = 0,
        BriefcaseKnockedLoose = 1
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
        /// <summary>Index of the winning firm, or MatchDirector.NoFirm.</summary>
        public byte Winner;
        public MatchEndReason Reason;
        /// <summary>Seconds the match had run when it ended.</summary>
        public ushort EndSeconds;
        /// <summary>Floor the briefcase was on when the match ended.</summary>
        public byte EndFloor;
    }

    /// <summary>The one briefcase. Synced host to everyone, only when it changes.</summary>
    public struct BriefcaseStatus
    {
        public BriefcaseState State;
        /// <summary>Network object id of the player carrying it. Only meaningful while Carried.</summary>
        public int CarrierId;
        /// <summary>Firm of the player carrying it, or NoFirm.</summary>
        public byte CarrierFirm;
        /// <summary>Firm of the last player to carry it, or NoFirm if nobody has yet.</summary>
        public byte LastCarrierFirm;
        /// <summary>Floor it is on, as the mode counts floors.</summary>
        public byte Floor;
        /// <summary>Where it was last let go or put back, in world space.</summary>
        public Vector3 Position;
    }
}
