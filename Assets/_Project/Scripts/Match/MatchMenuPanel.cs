using CloseTheDeal.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// The match's section of the game menu: where the match stands, who won and why, and the
    /// host's one button (start, then rematch). When a match ends it opens the menu through
    /// GameMenu, so the menu's own rule still holds: menu open, mouse free, player idle.
    /// Text changes only on match events. Design: docs/systems/MATCH.md.
    /// </summary>
    public sealed class MatchMenuPanel : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] MatchDirector _match;
        [SerializeField] GameMenu _menu;
        [SerializeField] GameObject _section;
        [SerializeField] TMP_Text _headline;
        [SerializeField] TMP_Text _detail;
        [SerializeField] UnityEngine.UI.Button _actionButton;
        [SerializeField] TMP_Text _actionLabel;
        [SerializeField] TMP_Text _waitingText;

        [Header("Words")]
        [Tooltip("The host's button before the first match. In a scene where the lobby desk starts the match, say that this skips the desk.")]
        [SerializeField] string _startLabel = "Start the match";
        [Tooltip("The host's button after a result.")]
        [SerializeField] string _rematchLabel = "Rematch";
        [Tooltip("What everyone else reads while waiting for the first match.")]
        [SerializeField] string _waitingForStart = "The host starts the match.";
        [Tooltip("What everyone else reads after a result.")]
        [SerializeField] string _waitingForRematch = "The host can start a rematch.";

        void OnEnable()
        {
            _match.OnStarted += Refresh;
            _match.OnClosed += Refresh;
            _match.OnPhaseChanged += OnPhaseChanged;
            _match.OnCountdownChanged += OnSecondPassed;
            _match.OnClockChanged += OnSecondPassed;
            _match.OnArenaChanged += Refresh;
            FirmMember.OnRosterChanged += Refresh;
            FirmMember.OnLocalChanged += Refresh;
            _actionButton.onClick.AddListener(OnAction);
            Refresh();
        }

        void OnDisable()
        {
            _match.OnStarted -= Refresh;
            _match.OnClosed -= Refresh;
            _match.OnPhaseChanged -= OnPhaseChanged;
            _match.OnCountdownChanged -= OnSecondPassed;
            _match.OnClockChanged -= OnSecondPassed;
            _match.OnArenaChanged -= Refresh;
            FirmMember.OnRosterChanged -= Refresh;
            FirmMember.OnLocalChanged -= Refresh;
            _actionButton.onClick.RemoveListener(OnAction);
        }

        void OnAction()
        {
            if (_match.CanHostStart)
                _match.HostStart();
            else if (_match.CanHostRematch)
                _match.HostRematch();

            // The round is starting: hand the player back their mouse and their body.
            _menu.Resume();
        }

        void OnPhaseChanged(MatchPhase previous, MatchPhase next)
        {
            Refresh();
            if (next != MatchPhase.Ended)
                return;

            _menu.Open();
            if (_match.CanHostRematch && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_actionButton.gameObject);
        }

        /// <summary>The clock only matters here while the menu is actually up.</summary>
        void OnSecondPassed(int secondsLeft)
        {
            if (_section.activeInHierarchy)
                Refresh();
        }

        void Refresh()
        {
            bool live = _match.IsLive && FirmMember.Local != null;
            _section.SetActive(live);
            if (!live)
                return;

            MatchPhase phase = _match.Phase;
            bool host = _match.IsServerStarted;
            bool action = phase == MatchPhase.Waiting || phase == MatchPhase.Ended;

            _headline.text = Headline(phase);
            _detail.text = Detail(phase);
            _actionButton.gameObject.SetActive(action && host);
            _actionButton.interactable = _match.CanHostStart || _match.CanHostRematch;
            _actionLabel.text = phase == MatchPhase.Ended ? _rematchLabel : _startLabel;
            _waitingText.gameObject.SetActive(action && !host);
            _waitingText.text = phase == MatchPhase.Ended ? _waitingForRematch : _waitingForStart;
        }

        string Headline(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Waiting:
                    return "Waiting in reception";
                case MatchPhase.Countdown:
                    return "Starting";
                case MatchPhase.Running:
                    return "Match running  ·  " + MatchHud.FormatClock(_match.SecondsLeft) + " left";
                default:
                    MatchSnapshot result = _match.Snapshot;
                    return result.Winner == MatchDirector.NoFirm ? "No deal." : _match.FirmName(result.Winner) + " sealed the deal.";
            }
        }

        string Detail(MatchPhase phase)
        {
            int own = FirmMember.Local.Firm;
            if (phase != MatchPhase.Ended)
                return $"You are with <b>{_match.FirmName(own)}</b>.  {FirmMember.CountIn(own)} v {FirmMember.CountIn(1 - own)}";

            MatchSnapshot result = _match.Snapshot;
            string side = result.Winner == MatchDirector.NoFirm ? string.Empty : result.Winner == own ? "  That is your firm." : "  That is the rival firm.";
            return Reason(result) + side;
        }

        string Reason(MatchSnapshot result)
        {
            string clock = MatchHud.FormatClock(result.EndSeconds);
            string floor = _match.Arena.FloorLabel(result.EndFloor);
            switch (result.Reason)
            {
                case MatchEndReason.BriefcaseOnRoof:
                    return $"Briefcase delivered to the roof at {clock}.";
                case MatchEndReason.ClockHolding:
                    return $"The quarter closed at {clock} with the briefcase in their hands, on {floor}.";
                case MatchEndReason.ClockLastCarried:
                    return $"The quarter closed at {clock}. The briefcase lay loose on {floor}; they carried it last.";
                default:
                    return $"The quarter closed at {clock} and nobody ever picked the briefcase up.";
            }
        }
    }
}
