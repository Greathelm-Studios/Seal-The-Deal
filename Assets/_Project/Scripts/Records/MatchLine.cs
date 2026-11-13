using FishNet.Broadcast;

namespace CloseTheDeal.Records
{
    /// <summary>
    /// One player's numbers for the match in progress, as the host counted them. Every machine
    /// holds the host's copy of every line; nobody counts their own. Design: docs/systems/RECORDS.md.
    /// </summary>
    public struct MatchLine
    {
        /// <summary>The player's connection id on the host. Matches LobbyRoster and Health's attacker id.</summary>
        public int ClientId;
        /// <summary>The firm the player climbed for: 0 = Building A, 1 = Building B.</summary>
        public byte Firm;
        /// <summary>Rivals this player killed, window kills included.</summary>
        public ushort Kills;
        /// <summary>Times this player was killed (falls are counted apart).</summary>
        public ushort Deaths;
        /// <summary>Times this player fell out of the buildings.</summary>
        public ushort Falls;
        /// <summary>Rivals who fell out shortly after this player hit them: the window shot.</summary>
        public ushort WindowKills;
        /// <summary>Times this player picked up the briefcase.</summary>
        public ushort Pickups;
        /// <summary>Times this player took the briefcase away from the rival firm.</summary>
        public ushort Steals;
        /// <summary>Floors this player opened by carrying the briefcase onto them.</summary>
        public ushort FloorsOpened;
        /// <summary>Items this player threw.</summary>
        public ushort Throws;
        /// <summary>Whole seconds this player held the briefcase.</summary>
        public ushort CarrySeconds;
        /// <summary>Whole seconds this player was in the match (from the start, or from joining late).</summary>
        public ushort SecondsPlayed;
        /// <summary>Highest floor this player stood on (0 = reception).</summary>
        public byte BestFloor;
    }

    /// <summary>What kind of death a feed entry is.</summary>
    public enum KillKind : byte
    {
        /// <summary>Shot or blown up by a rival.</summary>
        Killed,
        /// <summary>Fell out after a rival's hit: the window shot.</summary>
        KnockedOut,
        /// <summary>Fell out with nobody to blame.</summary>
        Fell,
        /// <summary>Died to the world, themselves or a teammate.</summary>
        Died
    }

    /// <summary>One kill-feed line, decided by the host. Killer is -1 when nobody gets the credit.</summary>
    public struct KillEntry
    {
        public int VictimClientId;
        public int KillerClientId;
        public KillKind Kind;
    }

    /// <summary>How the match ended for one player, sent by the host to that player only.</summary>
    public struct MatchFinal
    {
        /// <summary>Identifies the match within this hosting session, so a record is never applied twice.</summary>
        public uint MatchKey;
        /// <summary>Winning firm, or MatchDirector.NoFirm for no winner.</summary>
        public byte Winner;
        /// <summary>CloseTheDeal.MatchMode.MatchEndReason, as a byte.</summary>
        public byte Reason;
        /// <summary>Match clock at the end, in seconds since the start.</summary>
        public ushort EndSeconds;
        /// <summary>How many players were in the match at the end.</summary>
        public byte Players;
        /// <summary>This player's own numbers, as the host counted them.</summary>
        public MatchLine Line;
    }

    // ---- Wire messages (host → clients) ---------------------------------------------------------

    /// <summary>A line changed, or a player left (Gone).</summary>
    public struct MatchLineBroadcast : IBroadcast
    {
        public uint MatchKey;
        public bool Gone;
        public MatchLine Line;
    }

    /// <summary>A new match is counting: clear every line.</summary>
    public struct MatchResetBroadcast : IBroadcast
    {
        public uint MatchKey;
    }

    /// <summary>A kill-feed line.</summary>
    public struct KillBroadcast : IBroadcast
    {
        public KillEntry Entry;
    }

    /// <summary>The match is over; this is your record.</summary>
    public struct MatchFinalBroadcast : IBroadcast
    {
        public MatchFinal Final;
    }
}
