using CloseTheDeal.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// The wait between pressing Host or Join and standing in reception, drawn in the title's middle and right columns
    /// (the window and its sky stay): what is happening in the lobby's own words, a progress track that is honest
    /// (while connecting it fills over the 15 s the join waits before giving up; elsewhere only the chevrons step,
    /// because nothing measurable is happening), and Cancel (Esc / B), which leaves. Opens and closes with the game
    /// menu's state (UiRoot); text changes only on lobby events and once a second while connecting.
    /// </summary>
    [AddComponentMenu("Close the Deal/UI/Session Panel")]
    public sealed class SessionPanel : UiScreen
    {
        /// <summary>Opens and closes under lane 8's CloudWipe (docs/systems/UI_KIT.md "Screens").</summary>
        public override CloseTheDeal.UI.Title.UiTransitionKind? Through => CloseTheDeal.UI.Title.UiTransitionKind.CloudWipe;

        [Header("Parts")]
        [Tooltip("The section caption, e.g. § 1  JOIN GAME.")]
        [SerializeField] TMP_Text _caption;
        [Tooltip("The short state word at the caption's right, e.g. connecting.")]
        [SerializeField] TMP_Text _state;
        [Tooltip("What the lobby says it is doing.")]
        [SerializeField] UiStatusLine _status;
        [Tooltip("The progress track's fill (its width follows the connect clock).")]
        [SerializeField] RectTransform _fill;
        [Tooltip("The three chevrons that step while waiting: a progress sign, not decoration.")]
        [SerializeField] Graphic[] _chevrons = new Graphic[0];
        [Tooltip("The line under the track: how long it waits, how many seats are filled.")]
        [SerializeField] TMP_Text _detail;
        [Tooltip("Leaves the lobby or stops the join.")]
        [SerializeField] UiButton _cancel;

        [Header("Timing")]
        [Tooltip("Seconds each chevron stays lit as they step (the feel table's 0.4 s).")]
        [Min(0.1f)] [SerializeField] float _chevronStep = 0.4f;

        SteamLobby _lobby;
        TitleScreen _title;
        float _trackWidth = -1f;
        float _clock;
        int _shownSecond = -1;
        int _lit = -1;
        string _lastStatus = string.Empty;
        bool _showing;

        /// <summary>Given by the UI root: the lobby this panel reports on and the title it sits on.</summary>
        internal void Bind(SteamLobby lobby, TitleScreen title)
        {
            _lobby = lobby;
            _title = title;
        }

        protected override void Awake()
        {
            base.Awake();
            _firstFocus = _cancel;
            if (_cancel != null)
                _cancel.ConfirmSound = UiSoundEvent.Back;
            if (_fill != null)
                _trackWidth = ((RectTransform)_fill.parent).rect.width;
        }

        void OnEnable()
        {
            if (_cancel != null)
                _cancel.Clicked += OnCancelPressed;
            if (_lobby != null)
            {
                _lobby.OnStatus += OnStatus;
                _lobby.OnChanged += Refresh;
            }
        }

        protected override void OnDisable()
        {
            if (_cancel != null)
                _cancel.Clicked -= OnCancelPressed;
            if (_lobby != null)
            {
                _lobby.OnStatus -= OnStatus;
                _lobby.OnChanged -= Refresh;
            }

            base.OnDisable();
        }

        protected override void OnShowing()
        {
            _showing = true;
            Cancelled = false;
            if (_title != null)
                _title.MakeRoom(true);
            _clock = 0f;
            _shownSecond = -1;
            if (_lobby != null)
                _lastStatus = _lobby.StatusText;
            Refresh();
            WakeMotion();
        }

        protected override void OnHiding()
        {
            _showing = false;
            if (_title != null)
                _title.MakeRoom(false);
        }

        /// <summary>Esc / B cancels, with the Back sound.</summary>
        public override void OnCancel()
        {
            UiSound.Play(UiSoundEvent.Back);
            Leave();
        }

        void OnCancelPressed() => Leave();

        /// <summary>True once the player cancelled: the panel's close is silent (Back already answered).</summary>
        internal bool Cancelled { get; private set; }

        void Leave()
        {
            Cancelled = true;
            if (_lobby != null && _lobby.IsActive)
                _lobby.Leave(); // the root closes this panel when the lobby says it stopped
        }

        void OnStatus(string text)
        {
            _lastStatus = text;
            Refresh();
        }

        /// <summary>Re-reads the lobby: on its events, never per frame.</summary>
        public void Refresh()
        {
            if (_lobby == null)
                return;
            LobbyStage stage = _lobby.Stage;
            bool hosting = stage == LobbyStage.CreatingLobby || stage == LobbyStage.Hosting;
            SetText(_caption, hosting ? "§ 1  HOST GAME" : "§ 1  JOIN GAME");
            SetText(_state, StateWord(stage));
            if (_status != null && !string.IsNullOrEmpty(_lastStatus))
                _status.Set(_lastStatus, stage == LobbyStage.Connected || stage == LobbyStage.Hosting ? UiStatusKind.Info : UiStatusKind.Busy);
            if (_cancel != null && _cancel.Fx.Label != null)
                _cancel.Fx.Label.text = hosting ? "Close lobby" : "Cancel";
            if (_cancel != null)
                _cancel.Description = hosting ? "Stop hosting and go back to the title." : "Stop joining and go back to the title.";
            _shownSecond = -1;
            ShowDetail(stage);
        }

        static string StateWord(LobbyStage stage)
        {
            switch (stage)
            {
                case LobbyStage.CreatingLobby: return "opening";
                case LobbyStage.Hosting: return "hosting";
                case LobbyStage.JoiningSteam: return "joining";
                case LobbyStage.Connecting: return "connecting";
                case LobbyStage.Connected: return "loading";
                default: return string.Empty;
            }
        }

        void ShowDetail(LobbyStage stage)
        {
            if (_detail == null)
                return;
            switch (stage)
            {
                case LobbyStage.Connecting:
                    int second = Mathf.FloorToInt(_lobby.ConnectElapsed);
                    if (second == _shownSecond)
                        return;
                    _shownSecond = second;
                    _detail.text = "Gives up after " + Mathf.RoundToInt(_lobby.ConnectTimeout) + " s · " + second + " s so far";
                    return;
                case LobbyStage.Connected:
                case LobbyStage.Hosting:
                    _detail.text = "Opening reception · " + _lobby.PlayerCount + " of " + _lobby.MaxPlayers + " seats filled";
                    return;
                case LobbyStage.CreatingLobby:
                    _detail.text = "Steam usually answers in a few seconds";
                    return;
                default:
                    _detail.text = "Waiting for Steam";
                    return;
            }
        }

        protected override bool TickExtra(float dt)
        {
            if (_lobby == null || !gameObject.activeInHierarchy)
                return false;
            _clock += dt;
            LobbyStage stage = _lobby.Stage;
            if (_fill != null && _trackWidth > 0f)
            {
                float t = stage == LobbyStage.Connecting ? Mathf.Clamp01(_lobby.ConnectElapsed / _lobby.ConnectTimeout) : 0f;
                Vector2 size = _fill.sizeDelta;
                size.x = Mathf.Round(t * _trackWidth); // linear on purpose: progress is the one place for it
                _fill.sizeDelta = size;
            }

            if (stage == LobbyStage.Connecting)
                ShowDetail(stage);
            StepChevrons();
            return _showing && _lobby.IsActive; // hidden (in a game), it sleeps so the screen can switch off
        }

        /// <summary>One chevron lit at a time, stepping every 0.4 s; all half-lit with Reduce motion (no stepping).</summary>
        void StepChevrons()
        {
            if (_chevrons.Length == 0 || _tokens == null)
                return;
            int lit = UiMotion.Reduced ? -2 : (int)(_clock / _chevronStep) % _chevrons.Length;
            if (lit == _lit)
                return;
            _lit = lit;
            for (int i = 0; i < _chevrons.Length; i++)
            {
                if (_chevrons[i] != null)
                    _chevrons[i].color = lit == -2 ? _tokens.Ink3 : i == lit ? _tokens.Cobalt : _tokens.Hair;
            }
        }

        static void SetText(TMP_Text text, string value)
        {
            if (text != null && text.text != value)
                text.text = value;
        }
    }
}
