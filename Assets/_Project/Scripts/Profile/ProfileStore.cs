using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using CloseTheDeal.Records;
using UnityEngine;

namespace CloseTheDeal.Profile
{
    /// <summary>Persistent JSON career-profile store backed by BackgroundFileWriter. Design: docs/systems/PROFILE.md.</summary>
    public static class ProfileStore
    {
        const string FileName = "profile.json";
        const string PathArg = "-profileSave";

        /// <summary>Editor only: a scripted check started from a menu stores its own path here, so it never touches the real profile.</summary>
        public const string EditorPathKey = "ProfileStore.EditorPath";

        /// <summary>Why the profile in memory is what it is. For logs and a settings page line, not for a player-facing error.</summary>
        public enum LoadState
        {
            NotLoaded,
            /// <summary>No file yet: a first run.</summary>
            Fresh,
            Loaded,
            /// <summary>The file couldn't be read; it was kept as .corrupt and a fresh profile started.</summary>
            RecoveredFromCorrupt,
            /// <summary>The file is from a newer build; it is left alone and nothing is saved this session.</summary>
            NewerFileKept
        }

        /// <summary>The profile changed (a match was recorded, a hint marked seen, or it was loaded).</summary>
        public static event Action<PlayerProfile> Changed;

        /// <summary>A finished match was added: the match, then the whole profile.</summary>
        public static event Action<MatchSummary, PlayerProfile> MatchRecorded;

        static PlayerProfile _current;
        static string _pathOverride;

        public static LoadState State { get; private set; } = LoadState.NotLoaded;

        /// <summary>This player's profile, loaded on first use.</summary>
        public static PlayerProfile Current
        {
            get
            {
                if (_current == null)
                    Load();
                return _current;
            }
        }

        /// <summary>The career number for an unlock or stats page.</summary>
        public static int Get(ProfileStat stat) => Current.Get(stat);

        public static string FilePath
        {
            get
            {
                _pathOverride ??= ReadPathArg() ?? string.Empty;
                return _pathOverride.Length > 0 ? _pathOverride : Path.Combine(Application.persistentDataPath, FileName);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _current = null;
            _pathOverride = null;
            State = LoadState.NotLoaded;
            Changed = null;
            MatchRecorded = null;
        }

        /// <summary>Reads the file again (or starts fresh) and raises Changed.</summary>
        public static void Load()
        {
            DateTime now = DateTime.UtcNow;
            _current = Read(now, out LoadState state);
            State = state;
            Debug.Log($"[Profile] {State}: {FilePath} ({_current.Get(ProfileStat.MatchesPlayed)} matches).");
            Changed?.Invoke(_current);
        }

        /// <summary>Records one finished match from the host's MatchFinal and enqueues a save.</summary>
        public static MatchSummary RecordMatch(MatchFinal final, byte noFirm)
        {
            PlayerProfile profile = Current;
            MatchLine line = final.Line;
            MatchOutcome outcome = final.Winner == noFirm ? MatchOutcome.NoWinner : final.Winner == line.Firm ? MatchOutcome.Won : MatchOutcome.Lost;
            DateTime now = DateTime.UtcNow;
            var summary = new MatchSummary(now, outcome, line.Firm, final.Reason, final.Players, line.SecondsPlayed,
                line.Kills, line.Deaths, line.Falls, line.Steals, line.FloorsOpened, line.CarrySeconds, line.BestFloor);

            AddTotals(profile, line, outcome);
            profile.Push(summary, now);
            Save();
            MatchRecorded?.Invoke(summary, profile);
            Changed?.Invoke(profile);
            return summary;
        }

        /// <summary>Adds cash the host awarded to this player's profile and saves. Returns the new total.</summary>
        public static int AddCash(int amount)
        {
            if (amount <= 0)
                return Current.Get(ProfileStat.Cash);

            Current.Add(ProfileStat.Cash, amount);
            Save();
            Changed?.Invoke(_current);
            return _current.Get(ProfileStat.Cash);
        }

        /// <summary>Remembers that a first-run hint was shown, and saves. Returns false if it already was.</summary>
        public static bool MarkHintSeen(string hintId)
        {
            if (!Current.MarkSeen(hintId))
                return false;
            Save();
            Changed?.Invoke(_current);
            return true;
        }

        static void AddTotals(PlayerProfile profile, MatchLine line, MatchOutcome outcome)
        {
            profile.Add(ProfileStat.MatchesPlayed, 1);
            profile.Add(outcome == MatchOutcome.Won ? ProfileStat.Wins : outcome == MatchOutcome.Lost ? ProfileStat.Losses : ProfileStat.Draws, 1);
            profile.Set(ProfileStat.WinStreak, outcome == MatchOutcome.Won ? profile.Get(ProfileStat.WinStreak) + 1 : 0);
            profile.Best(ProfileStat.BestWinStreak, profile.Get(ProfileStat.WinStreak));
            profile.Add(ProfileStat.Kills, line.Kills);
            profile.Add(ProfileStat.Deaths, line.Deaths);
            profile.Add(ProfileStat.Falls, line.Falls);
            profile.Add(ProfileStat.WindowKills, line.WindowKills);
            profile.Add(ProfileStat.BriefcasePickups, line.Pickups);
            profile.Add(ProfileStat.Steals, line.Steals);
            profile.Add(ProfileStat.FloorsOpened, line.FloorsOpened);
            profile.Add(ProfileStat.Throws, line.Throws);
            profile.Add(ProfileStat.ThrowHits, line.ThrowHits);
            profile.Add(ProfileStat.CarrySeconds, line.CarrySeconds);
            profile.Add(ProfileStat.SecondsPlayed, line.SecondsPlayed);
            profile.Best(ProfileStat.BestFloor, line.BestFloor);
            profile.Best(ProfileStat.MostKillsInMatch, line.Kills);
        }

        static PlayerProfile Read(DateTime now, out LoadState state)
        {
            BackgroundFileWriter.Flush();
            string path = FilePath;
            if (!File.Exists(path))
            {
                state = LoadState.Fresh;
                return PlayerProfile.Fresh(now);
            }

            try
            {
                return Parse(File.ReadAllText(path), path, now, out state);
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Profile] Couldn't read {path} ({e.Message}); starting a fresh profile. The file was kept as .corrupt.");
                KeepCorrupt(path);
                state = LoadState.RecoveredFromCorrupt;
                return PlayerProfile.Fresh(now);
            }
        }

        static PlayerProfile Parse(string json, string path, DateTime now, out LoadState state)
        {
            var data = JsonUtility.FromJson<PlayerProfile>(json);
            if (data == null || data.Version < 1)
                throw new ArgumentException("not a profile");

            if (data.Version > PlayerProfile.CurrentVersion)
            {
                Debug.LogWarning($"[Profile] {path} is from a newer build (version {data.Version}); it is left as it is and this session's matches won't be saved.");
                state = LoadState.NewerFileKept;
                return PlayerProfile.Fresh(now);
            }

            data.Repair(now);
            state = LoadState.Loaded;
            return data;
        }

        static void Save()
        {
            if (State == LoadState.NewerFileKept)
                return;

            string path = FilePath;
            string json = JsonUtility.ToJson(_current, true);
            BackgroundFileWriter.Enqueue(path, json, "[Profile]");
        }

        static void KeepCorrupt(string path)
        {
            try
            {
                File.Copy(path, path + ".corrupt", true);
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Profile] Couldn't set the unreadable profile aside ({e.Message}).");
            }
        }

        static string ReadPathArg()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == PathArg)
                    return args[i + 1];
            }

#if UNITY_EDITOR
            string editorPath = UnityEditor.SessionState.GetString(EditorPathKey, string.Empty);
            if (editorPath.Length > 0)
                return editorPath;
#endif
            return null;
        }
    }

    /// <summary>Serialized background worker flushing atomic temp+replace JSON writes off the main thread. Design: docs/systems/PROFILE.md.</summary>
    internal static class BackgroundFileWriter
    {
        readonly struct WriteRequest
        {
            public readonly string Path;
            public readonly string Contents;
            public readonly string ErrorContext;

            public WriteRequest(string path, string contents, string errorContext)
            {
                Path = path;
                Contents = contents;
                ErrorContext = errorContext;
            }
        }

        static readonly ConcurrentQueue<WriteRequest> Queue = new();
        static readonly object WorkerLock = new();
        static Task _worker = Task.CompletedTask;
        static bool _running;
        static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Flush();
            if (!_hooked)
            {
                Application.quitting += Flush;
                _hooked = true;
            }
        }

        public static void Enqueue(string path, string contents, string errorContext)
        {
            if (!_hooked)
            {
                Application.quitting += Flush;
                _hooked = true;
            }

            lock (WorkerLock)
            {
                Queue.Enqueue(new WriteRequest(path, contents, errorContext));
                if (!_running)
                {
                    _running = true;
                    _worker = Task.Run(Drain);
                }
            }
        }

        public static void Flush()
        {
            while (true)
            {
                Task worker;
                lock (WorkerLock)
                {
                    if (!_running && Queue.IsEmpty)
                        return;
                    worker = _worker;
                }

                try
                {
                    worker.Wait();
                }
                catch (AggregateException)
                {
                }
            }
        }

        static void Drain()
        {
            while (true)
            {
                WriteRequest req;
                lock (WorkerLock)
                {
                    if (!Queue.TryDequeue(out req))
                    {
                        _running = false;
                        return;
                    }
                }

                WriteAtomic(req);
            }
        }

        static void WriteAtomic(WriteRequest req)
        {
            string temp = req.Path + ".tmp";
            try
            {
                string dir = Path.GetDirectoryName(req.Path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(temp, req.Contents);
                if (File.Exists(req.Path))
                    File.Replace(temp, req.Path, null);
                else
                    File.Move(temp, req.Path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"{req.ErrorContext} Couldn't save to {req.Path} ({e.Message}).");
            }
        }
    }
}
