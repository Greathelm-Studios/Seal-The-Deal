using CloseTheDeal.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// The match's section of the game menu, built with the UI kit: where the match stands,
    /// who sealed the deal and why, and the host's one button (start, then rematch). Everyone
    /// else reads the next step in the status line. When a match ends it opens the menu through
    /// GameMenu, so the menu's rule still holds: menu open, mouse free, player idle. A full
    /// results screen is lane 5's; this is the result until that lands. Text changes only on
    /// match events. Design: docs/systems/MATCH.md.
    /// </summary>
    public sealed class MatchMenuPanel : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] MatchDirector _match;
        [SerializeField] GameMenu _menu;
        [SerializeField] UiTokens _tokens;
        [SerializeField] GameObject _section;
        [SerializeField] TMP_Text _headline;
        [SerializeField] UiStatusLine _reason;
        [SerializeField] UiButton _action;
        [SerializeField] TMP_Text _actionLabel;

        [Header("Words")]
        [Tooltip("The host's button before the first match. In a scene where the lobby desk starts the match, say that this skips the desk.")]
        [SerializeField] string _startLabel = "Start the match";
        [Tooltip("The host's button after a result.")]
        [SerializeField] string _rematchLabel = "Rematch";
        [Tooltip("What everyone else reads while waiting for the first match.")]
        [SerializeField] string _waitingForStart = "The host starts the match.";
        [Tooltip("What everyone else reads after a result.")]
        [SerializeField] string _waitingForRematch = "The host can start a rematch.";

        // An empty slot means the scene's menu section predates this script: say so once and stand down.
        // Switching off from Awake runs OnDisable at once, before anything was subscribed.
        void Awake() => _wired = MatchWiring.Complete(this);

        bool _wired;

        void OnEnable()
        {
            // Switched back on by something else while still unwired: stay off.
            if (!_wired)
            {
                enabled = false;
                return;
            }

            _match.OnStarted += OnStarted;
            _match.OnClosed += Refresh;
            _match.OnPhaseChanged += OnPhaseChanged;
            _match.OnCountdownChanged += OnSecondPassed;
            _match.OnClockChanged += OnSecondPassed;
            _match.OnArenaChanged += Refresh;
            FirmMember.OnRosterChanged += Refresh;
            FirmMember.OnLocalChanged += Refresh;
            _action.Clicked += OnAction;
            Refresh();
        }

        // Disconnect callbacks can outlive the separate menu canvas during application shutdown.
        void OnApplicationQuit() => enabled = false;

        void OnDisable()
        {
            if (!_wired)
                return;

            _match.OnStarted -= OnStarted;
            _match.OnClosed -= Refresh;
            _match.OnPhaseChanged -= OnPhaseChanged;
            _match.OnCountdownChanged -= OnSecondPassed;
            _match.OnClockChanged -= OnSecondPassed;
            _match.OnArenaChanged -= Refresh;
            FirmMember.OnRosterChanged -= Refresh;
            FirmMember.OnLocalChanged -= Refresh;
            _action.Clicked -= OnAction;
        }

        /// <summary>Joining while a result is up: the result is where the match stands, so the menu shows it (quietly: it isn't news).</summary>
        void OnStarted()
        {
            Refresh();
            if (_match.Phase == MatchPhase.Ended)
                _menu.Open();
        }

        void OnAction()
        {
            if (_match.CanHostStart)
                _match.HostStart();
            else if (_match.CanHostRematch)
                _match.HostRematch();
            else
                return;

            // The round is starting: hand the player back their mouse and their body.
            _menu.Resume();
        }

        void OnPhaseChanged(MatchPhase previous, MatchPhase next)
        {
            Refresh();
            if (next != MatchPhase.Ended)
                return;

            _menu.Open();
            if (_action.isActiveAndEnabled && _action.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_action.gameObject);
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
            int own = FirmMember.Local.Firm;

            _headline.text = Headline(phase);
            _reason.Set(Detail(phase, own), phase == MatchPhase.Ended ? UiStatusKind.Info : UiStatusKind.Busy);

            _action.gameObject.SetActive(host && action);
            _actionLabel.text = phase == MatchPhase.Ended ? _rematchLabel : _startLabel;
            _action.interactable = host && (_match.CanHostStart || _match.CanHostRematch);
            _action.RefusalReason = "The towers are still being built.";
        }

        string Headline(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Waiting:
                    return "WAITING IN RECEPTION";
                case MatchPhase.Countdown:
                    return "STARTING";
                case MatchPhase.Running:
                    return "QUARTER OPEN  ·  " + MatchHud.FormatClock(_match.SecondsLeft) + " LEFT";
                default:
                    MatchSnapshot result = _match.Snapshot;
                    return result.Winner == MatchDirector.NoFirm ? "NO DEAL" : _match.FirmName(result.Winner).ToUpperInvariant() + " SEALED THE DEAL";
            }
        }

        string Detail(MatchPhase phase, int own)
        {
            if (phase == MatchPhase.Ended)
            {
                string result = _match.ReasonLine(_match.Snapshot, own);
                return _match.IsServerStarted ? result : result + "  " + _waitingForRematch;
            }

            string firms = $"You are with {_match.FirmName(own)}.  {FirmMember.CountIn(own)} v {FirmMember.CountIn(1 - own)}";
            if (phase == MatchPhase.Waiting && !_match.IsServerStarted)
                return firms + "  " + _waitingForStart;
            return firms;
        }
    }
}
