using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloseTheDeal.Profile
{
    /// <summary>A career number the profile keeps, for unlocks and the stats page to ask for by name.</summary>
    public enum ProfileStat
    {
        MatchesPlayed,
        Wins,
        Losses,
        Draws,
        Kills,
        Deaths,
        Falls,
        WindowKills,
        BriefcasePickups,
        Steals,
        FloorsOpened,
        Throws,
        CarrySeconds,
        SecondsPlayed,
        BestFloor,
        MostKillsInMatch,
        WinStreak,
        BestWinStreak,
        ThrowHits
    }

    /// <summary>How one finished match went for this player, newest first in the profile's history.</summary>
    [Serializable]
    public sealed class MatchSummary
    {
        [SerializeField] string playedUtc;
        [SerializeField] int outcome;
        [SerializeField] int firm;
        [SerializeField] int reason;
        [SerializeField] int players;
        [SerializeField] int seconds;
        [SerializeField] int kills;
        [SerializeField] int deaths;
        [SerializeField] int falls;
        [SerializeField] int steals;
        [SerializeField] int floorsOpened;
        [SerializeField] int carrySeconds;
        [SerializeField] int bestFloor;

        /// <summary>For the JSON reader only.</summary>
        MatchSummary() { }

        public MatchSummary(DateTime playedUtc, MatchOutcome outcome, int firm, int reason, int players, int seconds,
            int kills, int deaths, int falls, int steals, int floorsOpened, int carrySeconds, int bestFloor)
        {
            this.playedUtc = playedUtc.ToString("o");
            this.outcome = (int)outcome;
            this.firm = firm;
            this.reason = reason;
            this.players = players;
            this.seconds = seconds;
            this.kills = kills;
            this.deaths = deaths;
            this.falls = falls;
            this.steals = steals;
            this.floorsOpened = floorsOpened;
            this.carrySeconds = carrySeconds;
            this.bestFloor = bestFloor;
        }

        public DateTime PlayedUtc => DateTime.TryParse(playedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime at) ? at : DateTime.MinValue;
        public MatchOutcome Outcome => (MatchOutcome)outcome;
        /// <summary>0 = Building A, 1 = Building B.</summary>
        public int Firm => firm;
        /// <summary>CloseTheDeal.MatchMode.MatchEndReason as an int.</summary>
        public int Reason => reason;
        public int Players => players;
        public int Seconds => seconds;
        public int Kills => kills;
        public int Deaths => deaths;
        public int Falls => falls;
        public int Steals => steals;
        public int FloorsOpened => floorsOpened;
        public int CarrySeconds => carrySeconds;
        public int BestFloor => bestFloor;
    }

    public enum MatchOutcome
    {
        Lost,
        Won,
        /// <summary>The clock ran out and nobody ever picked up the briefcase.</summary>
        NoWinner
    }

    /// <summary>Serializable career profile (totals, last 20 matches, seen hints) managed by ProfileStore. Design: docs/systems/PROFILE.md.</summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        /// <summary>Raise when the file layout changes, and teach ProfileStore.Upgrade the old one.</summary>
        public const int CurrentVersion = 1;

        /// <summary>How many recent matches the profile keeps.</summary>
        public const int HistoryLength = 20;

        // 0 until set: a file without a version reads as not a profile.
        [SerializeField] int version;
        [SerializeField] string createdUtc;
        [SerializeField] string lastPlayedUtc;
        [SerializeField] int[] stats = new int[StatCount];
        [SerializeField] List<MatchSummary> recent = new();
        [SerializeField] List<string> seenHints = new();

        static readonly int StatCount = Enum.GetValues(typeof(ProfileStat)).Length;

        public int Version => version;
        public DateTime CreatedUtc => Parse(createdUtc);
        /// <summary>When the last match was recorded, or DateTime.MinValue if never.</summary>
        public DateTime LastPlayedUtc => Parse(lastPlayedUtc);
        /// <summary>Recent matches, newest first, at most HistoryLength.</summary>
        public IReadOnlyList<MatchSummary> Recent => recent;
        public bool IsNew => Get(ProfileStat.MatchesPlayed) == 0;

        public int Get(ProfileStat stat)
        {
            int index = (int)stat;
            return stats != null && index >= 0 && index < stats.Length ? stats[index] : 0;
        }

        public bool HasSeen(string hintId) => seenHints != null && seenHints.Contains(hintId);

        internal static PlayerProfile Fresh(DateTime nowUtc) => new() { version = CurrentVersion, createdUtc = nowUtc.ToString("o") };

        internal void Set(ProfileStat stat, int value) => stats[(int)stat] = Mathf.Max(0, value);

        internal void Add(ProfileStat stat, int amount) => Set(stat, SafeAdd(Get(stat), amount));

        internal void Best(ProfileStat stat, int value)
        {
            if (value > Get(stat))
                Set(stat, value);
        }

        internal void Push(MatchSummary summary, DateTime nowUtc)
        {
            lastPlayedUtc = nowUtc.ToString("o");
            recent.Insert(0, summary);
            if (recent.Count > HistoryLength)
                recent.RemoveRange(HistoryLength, recent.Count - HistoryLength);
        }

        internal bool MarkSeen(string hintId)
        {
            if (string.IsNullOrEmpty(hintId) || HasSeen(hintId))
                return false;
            seenHints.Add(hintId);
            return true;
        }

        /// <summary>Makes a file read from disk whole: missing lists, a short stats array from an older layout.</summary>
        internal void Repair(DateTime nowUtc)
        {
            if (stats == null || stats.Length != StatCount)
                Array.Resize(ref stats, StatCount);
            for (int i = 0; i < stats.Length; i++)
                stats[i] = Mathf.Max(0, stats[i]);
            recent ??= new List<MatchSummary>();
            recent.RemoveAll(m => m == null);
            seenHints ??= new List<string>();
            if (string.IsNullOrEmpty(createdUtc))
                createdUtc = nowUtc.ToString("o");
            version = CurrentVersion;
        }

        static int SafeAdd(int a, int b)
        {
            long sum = (long)a + b;
            return sum > int.MaxValue ? int.MaxValue : (int)Math.Max(0, sum);
        }

        static DateTime Parse(string value) =>
            DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime at) ? at : DateTime.MinValue;
    }
}
