using CloseTheDeal.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>Main title screen and menu driving SteamLobby actions and live sky backdrop.</summary>
    [AddComponentMenu("Close the Deal/UI/Title Screen")]
    public sealed class TitleScreen : UiScreen, IBackdropScreen
    {
        [Header("Backdrop")]
        [Tooltip("Where the title lane's live sky goes. Anything placed here draws behind the window frame's mullions.")]
        [SerializeField] RectTransform _backdropSlot;
        [Tooltip("The static sky drawn until a live backdrop is in the slot.")]
        [SerializeField] GameObject _staticSky;

        [Header("Menu")]
        [SerializeField] UiButton _host;
        [SerializeField] UiButton _joinLocal;
        [SerializeField] UiButton _settings;
        [SerializeField] UiButton _quit;

        [Header("Text")]
        [Tooltip("Steam or local mode, under the title.")]
        [SerializeField] TMP_Text _mode;
        [Tooltip("What is happening now.")]
        [SerializeField] UiStatusLine _status;
        [Tooltip("How to join a friend (Steam only).")]
        [SerializeField] TMP_Text _joinHint;
        [Tooltip("Heading of the focused button's description.")]
        [SerializeField] TMP_Text _focusHeading;
        [Tooltip("The focused button's description.")]
        [SerializeField] TMP_Text _focusBody;
        [Tooltip("Version line under the title, and the footer's version.")]
        [SerializeField] TMP_Text[] _versionLines = new TMP_Text[0];
        [Tooltip("The build line in the footer: version and build id.")]
        [SerializeField] TMP_Text _build;

        [Tooltip("The menu column's group: hidden while a dialog takes its place (the quit confirm sits where the menu was).")]
        [SerializeField] CanvasGroup _menuGroup;
        [Tooltip("The middle column's groups (status, the focused button's explanation): hidden while a wide panel (hosting, joining, a problem) takes the middle and right columns.")]
        [SerializeField] CanvasGroup[] _centreGroups = new CanvasGroup[0];

        [Header("Screens it opens")]
        [SerializeField] UiScreen _settingsScreen;
        [SerializeField] UiConfirmDialog _quitDialog;

        const string Starting = "Starting...";
        const string HostRefusal = "Host game opens once the network is ready.";
        const string QuitTitle = "Quit to desktop?";
        const string QuitBody = "Your settings are saved.";

        SteamLobby _lobby;
        UiTween _menuShown;
        UiTween _centreShown;
        bool _wide;
        string _lastStatus = Starting;
        bool _refusalShown;

        public RectTransform BackdropSlot => _backdropSlot;

        /// <summary>Switches between the live sky backdrop and static fallback (kept when scene has no skybox).</summary>
        public void SetLiveBackdrop(bool live)
        {
            if (_staticSky != null)
                _staticSky.SetActive(!(live && RenderSettings.skybox != null));
        }

        internal void Bind(GameMenu menu)
        {
            _lobby = menu != null ? menu.Lobby : null;
        }

        protected override void Awake()
        {
            _menuShown.Snap(1f);
            _centreShown.Snap(1f);
            base.Awake();
            string version = "v" + Application.version + " · Early Access";
            foreach (TMP_Text line in _versionLines)
            {
                if (line != null)
                    line.text = version;
            }

            if (_build != null)
                _build.text = BuildLine();
        }

        void OnEnable()
        {
            if (_lobby != null)
            {
                _lobby.OnStatus += OnStatus;
                _lobby.OnChanged += Refresh;
                // Status changes while the title was off (in a game, leaving it) were missed: take the lobby's current one.
                if (!string.IsNullOrEmpty(_lobby.StatusText))
                    _lastStatus = _lobby.StatusText;
                _refusalShown = false;
            }

            _host.Clicked += OnHost;
            _joinLocal.Clicked += OnJoinLocal;
            _settings.Clicked += OnSettings;
            _quit.Clicked += OnQuit;
            UiControlEvents.Focused += OnFocused;
            UiControlEvents.Refused += OnRefused;
            Refresh();
        }

        protected override void OnDisable()
        {
            if (_lobby != null)
            {
                _lobby.OnStatus -= OnStatus;
                _lobby.OnChanged -= Refresh;
            }

            _host.Clicked -= OnHost;
            _joinLocal.Clicked -= OnJoinLocal;
            _settings.Clicked -= OnSettings;
            _quit.Clicked -= OnQuit;
            UiControlEvents.Focused -= OnFocused;
            UiControlEvents.Refused -= OnRefused;
            base.OnDisable();
        }

        protected override void OnShowing()
        {
            _firstFocus = _lobby != null && _lobby.Ready ? _host : _settings;
            Refresh();
        }

        protected override void OnCovered(bool covered)
        {
            Fade(ref _menuShown, !covered);
            if (!covered && _wide)
                MakeRoom(false);
        }

        /// <summary>Fades middle-column status and focus text out when a wide panel (session/problem) opens.</summary>
        public void MakeRoom(bool wide)
        {
            _wide = wide;
            Fade(ref _centreShown, !wide);
        }

        void Fade(ref UiTween tween, bool show)
        {
            if (_tokens == null)
                return;
            float to = show ? 1f : 0f;
            if (Mathf.Approximately(tween.To, to) && (tween.Running || Mathf.Approximately(tween.Value, to)))
                return;
            tween.Start(tween.Value, to, show ? _tokens.PanelIn : _tokens.PanelOut, show ? _tokens.EaseOut : _tokens.EaseIn);
            WakeMotion();
        }

        protected override bool TickExtra(float dt)
        {
            _menuShown.Step(dt);
            _centreShown.Step(dt);
            return _menuShown.Running || _centreShown.Running;
        }

        protected override void AfterApply()
        {
            Dim(_menuGroup, _menuShown.Value);
            foreach (CanvasGroup group in _centreGroups)
                Dim(group, _centreShown.Value);
        }

        static void Dim(CanvasGroup group, float shown)
        {
            if (group == null)
                return;
            group.alpha *= shown;
            group.blocksRaycasts = shown > 0.5f;
        }

        public override void OnCancel() => OnQuit();

        void OnStatus(string text)
        {
            _lastStatus = text;
            _refusalShown = false;
            Refresh();
        }

        void Refresh()
        {
            if (_lobby == null)
                return;

            bool ready = _lobby.Ready;
            bool local = _lobby.LocalMode;
            _host.interactable = ready;
            _host.RefusalReason = HostRefusal;
            _host.Description = local
                ? "Host a game on this PC. Start a second copy and choose Join on this PC."
                : "Open a lobby on Steam. Friends join from your name in Steam, up to " + _lobby.MaxPlayers + " players.";
            _joinLocal.gameObject.SetActive(local);
            _joinLocal.interactable = ready;
            _joinLocal.RefusalReason = "Join on this PC opens once the network is ready.";
            if (_joinHint != null)
            {
                _joinHint.gameObject.SetActive(!local || !SteamService.IsReady);
                _joinHint.text = local
                    ? "Steam is not running, so friends can't join. Start Steam, then restart Seal the Deal to play online."
                    : "To join a friend, accept their invite or choose Join Game on their name in Steam.";
            }

            if (_mode != null)
                _mode.text = ModeLine(ready, local);
            if (_status != null && !_refusalShown)
                _status.Set(_lastStatus, ready ? UiStatusKind.Info : UiStatusKind.Busy);
            LinkNavigation(local);
        }

        static string BuildLine()
        {
            string id = Application.isEditor ? "EDITOR" : Application.buildGUID;
            if (!string.IsNullOrEmpty(id) && id.Length > 8)
                id = id.Substring(0, 8).ToUpperInvariant();
            return "BUILD " + Application.version + (string.IsNullOrEmpty(id) ? string.Empty : " · " + id);
        }

        static string ModeLine(bool ready, bool local)
        {
            if (!ready)
                return Starting;
            if (local)
                return SteamService.IsReady ? "Local test mode" : "Local test mode. Steam is not running.";
            return "Signed in to Steam as " + SteamService.LocalName;
        }

        void LinkNavigation(bool local)
        {
            Selectable[] order = local
                ? new Selectable[] { _host, _joinLocal, _settings, _quit }
                : new Selectable[] { _host, _settings, _quit };
            Selectable previous = null;
            for (int i = 0; i < order.Length; i++)
            {
                Selectable current = order[i];
                if (!current.IsInteractable())
                    continue;
                var nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = previous };
                current.navigation = nav;
                if (previous != null)
                {
                    Navigation prevNav = previous.navigation;
                    prevNav.selectOnDown = current;
                    previous.navigation = prevNav;
                }

                previous = current;
            }
        }

        void OnFocused(Selectable control, string description)
        {
            if (control == null || !control.transform.IsChildOf(transform))
                return;
            if (_refusalShown)
            {
                _refusalShown = false; // Moving on clears a refusal; the lobby's own status comes back.
                Refresh();
            }

            if (_focusHeading != null && control is UiButton button && button.Fx.Label != null)
                _focusHeading.text = "§ " + button.Fx.Label.text.ToUpperInvariant();
            if (_focusBody != null)
                _focusBody.text = description;
        }

        void OnRefused(Selectable control, string reason)
        {
            if (control == null || !control.transform.IsChildOf(transform) || string.IsNullOrEmpty(reason) || _status == null)
                return;
            _refusalShown = true;
            _status.Set(reason, UiStatusKind.Refusal);
        }

        void OnHost() => _lobby.Host();

        void OnJoinLocal() => _lobby.JoinLocal();

        void OnSettings()
        {
            if (Stack != null && _settingsScreen != null)
                Stack.Push(_settingsScreen);
        }

        void OnQuit()
        {
            if (Stack == null || _quitDialog == null || Stack.Top != this)
                return;
            _quitDialog.Configure(QuitTitle, QuitBody, "Quit to desktop", "Stay", QuitToDesktop);
            Stack.Push(_quitDialog);
        }

        /// <summary>Exits the application (or Play Mode in the Editor).</summary>
        public static void QuitToDesktop()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}
