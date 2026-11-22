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
        /// <summary>A player carried the briefcase onto a roof.</summary>
        BriefcaseOnRoof = 1,
        /// <summary>The clock ran out while a player of the winning firm held the briefcase.</summary>
        ClockHolding = 2,
        /// <summary>The clock ran out with the briefcase lying loose; the firm that carried it last wins.</summary>
        ClockLastCarried = 3,
        /// <summary>The clock ran out and nobody ever picked the briefcase up. No winner.</summary>
        ClockNeverCarried = 4,
        /// <summary>Floor race: a player carried the briefcase into the manager's office on the top floor.</summary>
        ManagersOffice = 5
    }

    public enum BriefcaseState : byte
    {
        /// <summary>Lying loose: where it spawned, where it was dropped, or where it was put back.</summary>
        Dropped = 0,
        Carried = 1,
        /// <summary>It left the buildings and is on its way back.</summary>
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
        FellOut = 1,
        BriefcaseKnockedLoose = 2,
        /// <summary>A player without the briefcase reached the gate to a floor nobody has carried it onto yet.</summary>
        GateRefused = 3,
        /// <summary>The first time this match a player reaches a shut gate: the longer line that explains the rule.</summary>
        FirstGateRefusal = 4
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
        /// <summary>Firm of the last player to carry it, or NoFirm if nobody has yet. Decides the clock when it lies loose.</summary>
        public byte LastCarrierFirm;
        /// <summary>Where it is: building 0, building 1, or MatchArena.Neutral (the plaza or a skybridge).</summary>
        public byte Place;
        /// <summary>Floor it is on, counted from reception and the plaza = 0.</summary>
        public byte Floor;
        /// <summary>The highest checkpoint floor it has been on; where it comes back to if it leaves the buildings. 0 = its spawn.</summary>
        public byte ReturnFloor;
        /// <summary>Where it was last let go or put back, in world space.</summary>
        public Vector3 Position;
        /// <summary>While Returning: the host tick it reappears at, so a HUD can count it down. 0 otherwise.</summary>
        public uint ReturnTick;
    }

    /// <summary>The highest floor anyone may climb to, and which firm's carrier opened it. Synced as one value, only on change.</summary>
    public struct FloorOpening
    {
        /// <summary>The open floor: 0 = the ground floor.</summary>
        public byte Floor;
        /// <summary>The firm whose carrier opened it, or NoFirm at the start of a match.</summary>
        public byte ByFirm;
    }
}
