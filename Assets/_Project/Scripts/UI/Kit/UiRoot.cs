using CloseTheDeal.Net;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// The UI kit's canvas in a scene. Shows the title screen whenever the game menu is at the title (not hosting,
    /// joining or playing) and closes its screens when the game takes over; the game menu keeps owning the rule
    /// menu open = mouse free = player idle. Also reads the device the player is on each frame (before the
    /// EventSystem runs) and passes Esc / the pad's B to the top screen. Built by Close the Deal > Lanes > Set Up UI.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [AddComponentMenu("Close the Deal/UI/UI Root")]
    public sealed class UiRoot : MonoBehaviour
    {
        [Tooltip("The kit's tokens.")]
        [SerializeField] UiTokens _tokens;
        [Tooltip("The game menu this kit takes the title state from.")]
        [SerializeField] GameMenu _menu;
        [Tooltip("The kit's screen stack.")]
        [SerializeField] UiScreenStack _stack;
        [Tooltip("The first screen: the title and main menu.")]
        [SerializeField] TitleScreen _title;
        [Tooltip("Hosting, joining and loading: the wait, with Cancel. Opens over the title while the lobby is busy.")]
        [SerializeField] SessionPanel _session;
        [Tooltip("What went wrong and how to fix it. Opens over the title when the lobby reports a problem.")]
        [SerializeField] ProblemPanel _problem;

        bool _repeatApplied;
        bool _started;
        bool _brokenDismissed;
        bool _enteringGame;

        void Awake()
        {
            // The kit canvas is a prefab: when it is regenerated its link to the scene's game menu can drop, so find it once.
            if (_menu == null)
                _menu = FindAnyObjectByType<GameMenu>();
            if (_title != null)
                _title.Bind(_menu);
            SteamLobby lobby = _menu != null ? _menu.Lobby : null;
            if (_session != null)
                _session.Bind(lobby, _title);
            if (_problem != null)
            {
                _problem.Bind(lobby, _title);
                _problem.Dismissed += () => _brokenDismissed = true;
            }
        }

        public UiScreenStack Stack => _stack;

        public TitleScreen Title => _title;

        void OnEnable()
        {
            if (_menu == null)
            {
                Debug.LogError("[UI] The UI root has no game menu, so it can't tell when to show the title. Run Close the Deal > Lanes > Set Up UI.", this);
                return;
            }

            _menu.StateChanged += Sync;
            _stack.Changed += Sync;
            _menu.HandTitleToKit(true);
            if (_started)
                Sync();
        }

        /// <summary>
        /// The first screen opens here, not in OnEnable: this runs early (order -200), before the EventSystem and the
        /// focus marks are enabled, and the title needs both to take focus and draw its marks on top.
        /// </summary>
        void Start()
        {
            _started = true;
            if (_menu != null)
                Sync();
        }

        void OnDisable()
        {
            if (_menu == null)
                return;
            _menu.StateChanged -= Sync;
            _stack.Changed -= Sync;
            _menu.HandTitleToKit(false);
        }

        void Update()
        {
            UiInput.Poll();
            if (!_repeatApplied && UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.currentInputModule != null)
            {
                UiInput.ApplyRepeat(_tokens);
                _repeatApplied = true;
            }

            // A click on empty paper drops focus; the next key or pad press puts it back on the top screen.
            // During the zoom into a game input is the game's; while a change waits for its cover, Esc does nothing.
            bool ours = (_menu == null || (_menu.OutOfGame && !_enteringGame)) && !_stack.Busy;
            if (ours && _stack.Count > 0 && UiInput.UsingKeysOrPad && !UiInput.Capturing
                && UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == null)
                _stack.Top.Refocus();

            if (ours && _stack.Count > 0 && UiInput.CancelPressed && !UiInput.Capturing)
                _stack.Cancel();
        }

        public SessionPanel Session => _session;

        public ProblemPanel Problem => _problem;

        /// <summary>
        /// Keeps the screens in step with the game menu and the lobby. Out of a game: the title, with the session
        /// panel over it while the lobby is busy and the problem panel when it reports one (a problem persists in
        /// the lobby, so this lands on Title then Problem whatever order the events come in). Into a game: the
        /// window closes over the view (lane 8's frame zoom) and the screens clear under it. Runs on the menu's and
        /// the stack's events, never per frame.
        /// </summary>
        void Sync()
        {
            if (!_menu.OutOfGame)
            {
                EnterGame();
                return;
            }

            bool fromGame = _enteringGame;
            _enteringGame = false;
            if (_stack.Busy)
                return; // a change is waiting for its transition's cover; it calls back here when it lands
            // Back from a game: the title (and any problem panel over it) comes in under one cloud wipe.
            if (fromGame && _stack.Count == 0 && _started
                && _stack.RunCovered(CloseTheDeal.UI.Title.UiTransitionKind.CloudWipe, () => { if (isActiveAndEnabled) Sync(); }))
                return;

            if (_stack.Count == 0)
                _stack.Push(_title);
            UiScreen want = Wanted();
            UiScreen top = _stack.Top;
            if (top == want)
                return;
            bool ours = top == _session || top == _problem;
            if (want == null)
            {
                if (ours)
                    _stack.Pop(!(top == _session && _session.Cancelled)); // a cancel already played Back
                return;
            }

            if (ours)
                _stack.Replace(want);
            else if (top == _title)
                _stack.Push(want);
            // Over Settings or the quit question the panel waits: Sync runs again when that closes.
        }

        UiScreen Wanted()
        {
            SteamLobby lobby = _menu.Lobby;
            if (lobby == null)
                return null;
            if (lobby.IsActive)
                return _session;
            if (_problem != null && _problem.Closing)
                return null;
            bool problem = lobby.Problem != LobbyProblem.None && !(lobby.Problem == LobbyProblem.NetworkBroken && _brokenDismissed);
            return problem ? _problem : null;
        }

        /// <summary>A player of ours is in the world: close the window over the view, then clear the screens under it.</summary>
        void EnterGame()
        {
            if (_stack.Count == 0 || _enteringGame)
                return;
            _enteringGame = true;
            if (UiMotion.Reduced || !isActiveAndEnabled)
            {
                _stack.Clear();
                return;
            }

            CloseTheDeal.UI.Title.UiTransition.Play(CloseTheDeal.UI.Title.UiTransitionKind.FrameZoom, () =>
            {
                if (!_menu.OutOfGame)
                    _stack.Clear();
            });
        }
    }
}
