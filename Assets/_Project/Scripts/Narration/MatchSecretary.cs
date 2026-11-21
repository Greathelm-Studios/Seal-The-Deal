using CloseTheDeal.MatchMode;
using CloseTheDeal.Records;
using UnityEngine;

namespace CloseTheDeal.Narration
{
    /// <summary>Emits local secretary subtitles for key match moments read from synced match state and records. Design: docs/systems/NARRATION.md.</summary>
    [DisallowMultipleComponent]
    public sealed class MatchSecretary : MonoBehaviour
    {
        [Tooltip("What she says during a match.")]
        [SerializeField] MatchSecretaryLines _lines;

        MatchDirector _match;
        MatchRecords _records;
        readonly int[] _lastPick = new int[System.Enum.GetValues(typeof(MatchMoment)).Length];
        float _quietUntil;
        bool _picked;
        bool _killed;

        void Awake()
        {
            for (int i = 0; i < _lastPick.Length; i++)
                _lastPick[i] = -1;
            if (_lines == null)
                Debug.LogWarning("[Secretary] MatchSecretary has no lines asset; she stays quiet during matches.", this);
        }

        void Update()
        {
            if (_match == null && MatchDirector.Instance != null)
                HookMatch(MatchDirector.Instance);
            if (_records == null && MatchRecords.Instance != null)
            {
                _records = MatchRecords.Instance;
                _records.Kill += OnKill;
            }
            if (_match != null && _records != null)
                enabled = false;
        }

        void OnDestroy()
        {
            if (_records != null)
                _records.Kill -= OnKill;
            if (_match == null)
                return;
            _match.OnPhaseChanged -= OnPhase;
            _match.OnMatchEnded -= OnEnded;
            _match.OnBriefcasePickedUp -= OnPickedUp;
            _match.OnBriefcaseStolen -= OnStolen;
            _match.OnFloorOpened -= OnFloorOpened;
            _match.OnCheckpointSaved -= OnCheckpoint;
            _match.OnCapWarning -= OnCapWarning;
            _match.OnBriefcaseReturned -= OnReturned;
        }

        void HookMatch(MatchDirector match)
        {
            _match = match;
            _match.OnPhaseChanged += OnPhase;
            _match.OnMatchEnded += OnEnded;
            _match.OnBriefcasePickedUp += OnPickedUp;
            _match.OnBriefcaseStolen += OnStolen;
            _match.OnFloorOpened += OnFloorOpened;
            _match.OnCheckpointSaved += OnCheckpoint;
            _match.OnCapWarning += OnCapWarning;
            _match.OnBriefcaseReturned += OnReturned;
        }

        static int LocalFirm => FirmMember.Local != null ? FirmMember.Local.Firm : MatchDirector.NoFirm;

        void OnPhase(MatchPhase previous, MatchPhase next)
        {
            if (next == MatchPhase.Countdown)
            {
                _picked = false;
                _killed = false;
            }
            else if (next == MatchPhase.Running)
            {
                Say(MatchMoment.MatchStart, Fill.None);
            }
        }

        void OnEnded(MatchSnapshot result)
        {
            int winner = result.Winner;
            MatchMoment moment = winner == MatchDirector.NoFirm ? MatchMoment.NoWinner : winner == LocalFirm ? MatchMoment.Won : MatchMoment.Lost;
            Say(moment, new Fill { Firm = winner, Rival = Other(winner) }, ignoreQuiet: true);
        }

        void OnPickedUp(int firm)
        {
            if (_picked)
                return;
            _picked = true;
            Say(MatchMoment.FirstPickup, new Fill { Firm = firm, Name = CarrierName() });
        }

        void OnStolen(int byFirm, int fromFirm) =>
            Say(MatchMoment.Steal, new Fill { Firm = byFirm, Rival = fromFirm, Name = CarrierName() });

        void OnFloorOpened(int floor, int byFirm) =>
            Say(MatchMoment.FloorOpened, new Fill { Firm = byFirm, Floor = floor, Name = CarrierName() });

        void OnCheckpoint(int firm, int floor)
        {
            if (firm == LocalFirm)
                Say(MatchMoment.CheckpointMoved, new Fill { Firm = firm, Floor = floor });
        }

        void OnCapWarning(int secondsLeft) =>
            Say(MatchMoment.CapWarning, new Fill { Time = MatchHud.FormatClock(secondsLeft) }, ignoreQuiet: true);

        void OnReturned()
        {
            BriefcaseStatus status = _match.CaseStatus;
            Say(MatchMoment.BriefcaseReturned, new Fill { Place = _match.PlaceLabel(status.Place, status.Floor) });
        }

        void OnKill(KillEntry entry)
        {
            if (entry.Kind == KillKind.KnockedOut)
                Say(MatchMoment.WindowKill, new Fill { Killer = MatchRecords.NameOf(entry.KillerClientId), Victim = MatchRecords.NameOf(entry.VictimClientId) });
            else if (entry.Kind == KillKind.Fell && IsLocal(entry.VictimClientId))
                Say(MatchMoment.FellOut, new Fill { Name = MatchRecords.NameOf(entry.VictimClientId) });
            else if (entry.Kind == KillKind.Killed && !_killed)
                FirstKill(entry);
        }

        void FirstKill(KillEntry entry)
        {
            _killed = true;
            Say(MatchMoment.FirstKill, new Fill { Killer = MatchRecords.NameOf(entry.KillerClientId), Victim = MatchRecords.NameOf(entry.VictimClientId) });
        }

        struct Fill
        {
            public static Fill None => default;
            public int? Firm, Rival, Floor;
            public string Name, Killer, Victim, Place, Time;
        }

        void Say(MatchMoment moment, Fill fill, bool ignoreQuiet = false)
        {
            MatchSecretaryLines.Moment entry = _lines != null ? _lines.For(moment) : null;
            if (entry == null || entry.Lines == null || entry.Lines.Length == 0 || _match == null)
                return;
            if (!ignoreQuiet && Time.unscaledTime < _quietUntil && !OutranksCurrent(entry.Priority))
                return;

            string text = Format(entry.Lines[Pick(moment, entry.Lines.Length)], fill);
            float seconds = Subtitles.SecondsFor(text, _lines.SecondsPerWord, _lines.MinimumSeconds);
            if (Subtitles.Show(new SubtitleLine(_lines.Speaker, text, seconds, entry.Priority, moment.ToString())))
                _quietUntil = Time.unscaledTime + seconds + _lines.QuietSeconds;
        }

        static bool OutranksCurrent(int priority) => !Subtitles.TryGetCurrent(out SubtitleLine current) || priority > current.Priority;

        int Pick(MatchMoment moment, int count)
        {
            int last = _lastPick[(int)moment];
            int pick;
            if (count == 1 || last < 0 || last >= count)
                pick = Random.Range(0, count);
            else
            {
                // Any line but the last one said.
                pick = Random.Range(0, count - 1);
                if (pick >= last)
                    pick++;
            }
            _lastPick[(int)moment] = pick;
            return pick;
        }

        string Format(string line, Fill fill)
        {
            return line
                .Replace("{firm}", FirmName(fill.Firm))
                .Replace("{rival}", FirmName(fill.Rival))
                .Replace("{floor}", fill.Floor.HasValue ? _match.Arena.FloorLabel(fill.Floor.Value) : string.Empty)
                .Replace("{name}", fill.Name ?? string.Empty)
                .Replace("{killer}", fill.Killer ?? string.Empty)
                .Replace("{victim}", fill.Victim ?? string.Empty)
                .Replace("{place}", fill.Place ?? string.Empty)
                .Replace("{time}", fill.Time ?? string.Empty);
        }

        string FirmName(int? firm) => firm.HasValue && firm.Value >= 0 && firm.Value != MatchDirector.NoFirm ? _match.FirmName(firm.Value) : string.Empty;

        static int Other(int firm) => firm == 0 ? 1 : firm == 1 ? 0 : MatchDirector.NoFirm;

        string CarrierName()
        {
            FirmMember carrier = _match.Carrier();
            return carrier != null && carrier.Owner != null ? MatchRecords.NameOf(carrier.Owner.ClientId) : string.Empty;
        }

        static bool IsLocal(int clientId) => FirmMember.Local != null && FirmMember.Local.Owner != null && FirmMember.Local.Owner.ClientId == clientId;
    }
}
