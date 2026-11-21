using CloseTheDeal.Net;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>Kit canvas root: syncs screen stack with GameMenu/SteamLobby state and routes UI navigation/cancel.</summary>
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
        [Tooltip("The in-game menu: a glass pane over the running game, open while the game menu is open in a game.")]
        [SerializeField] InGameMenuScreen _gameMenu;

        bool _repeatApplied;
        bool _started;
        bool _brokenDismissed;
        bool _inGameView; // a game of ours is on screen (from the frame the zoom starts until we leave it)
        bool _zooming;    // the window is closing over the view: input is the game's, the screens wait
        bool _returningFromGame;
        int _zoomCount;

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

            if (_gameMenu != null)
                _gameMenu.Bind(_menu);
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
            _menu.HandTitleToKit(true, _gameMenu != null);
            if (_started)
                Sync();
        }

        // Delay first screen push until Start (order -200 runs before EventSystem and UiFocusFrame enable).
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
            bool ours = Ours();
            if (ours && _stack.Count > 0 && _menu != null && _menu.InGame && GameMenu.PadMenuPressed())
            {
                UiSound.Play(UiSoundEvent.Back);
                _menu.Resume(); // Start toggles the whole in-game menu, from any screen in it
                return;
            }

            if (ours && _stack.Count > 0 && UiInput.UsingKeysOrPad && !UiInput.Capturing
                && UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == null)
                _stack.Top.Refocus();

            if (ours && _stack.Count > 0 && UiInput.CancelPressed && !UiInput.Capturing)
                _stack.Cancel();
        }

        bool Ours()
        {
            if (_menu == null)
                return true;
            if (_stack.Busy || _zooming)
                return false; // a change waits for its cover, or the window is zooming into the game
            return _menu.OutOfGame || _menu.IsOpen;
        }

        public SessionPanel Session => _session;

        public InGameMenuScreen GameMenuScreen => _gameMenu;

        public ProblemPanel Problem => _problem;

        void Sync()
        {
            if (!_menu.OutOfGame)
            {
                SyncInGame();
                return;
            }

            _zooming = false;
            _menu.KitMenuUp = false;
            if (_inGameView)
            {
                _inGameView = false; // left the game: its menu screens go, the title comes back
                _returningFromGame = true;
                _stack.Clear();
            }

            if (_stack.Busy)
                return; // a change is waiting for its transition's cover; it calls back here when it lands
            if (_returningFromGame)
            {
                // Back from a game: the title (and any problem panel over it) comes in under one cloud wipe.
                _returningFromGame = false;
                if (_stack.Count == 0 && _started
                    && _stack.RunCovered(CloseTheDeal.UI.Title.UiTransitionKind.CloudWipe, () => { if (isActiveAndEnabled) Sync(); }))
                    return;
            }

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

        void SyncInGame()
        {
            if (!_inGameView)
            {
                _inGameView = true;
                EnterGame();
                return;
            }

            if (_zooming || _gameMenu == null)
                return;
            bool open = _menu.IsOpen;
            if (open && _stack.Count == 0)
            {
                _stack.Push(_gameMenu);
                UiSound.Play(UiSoundEvent.PanelOpen); // the stack plays nothing for its first screen
            }
            else if (!open && _stack.Count > 0)
                _stack.Clear(); // the close's own key or button already answered (Back, Confirm)
            _menu.KitMenuUp = _stack.Count > 0;
        }

        void EnterGame()
        {
            if (_stack.Count == 0)
            {
                SyncInGame();
                return;
            }

            if (!isActiveAndEnabled) // with Reduce motion lane 8 plays its short fade instead (and its sound)
            {
                _stack.Clear();
                SyncInGame();
                return;
            }

            _zooming = true;
            int zoom = ++_zoomCount;
            CloseTheDeal.UI.Title.UiTransition.Play(CloseTheDeal.UI.Title.UiTransitionKind.FrameZoom, () =>
            {
                if (_menu.OutOfGame || zoom != _zoomCount)
                    return; // left the game meanwhile, or a later zoom owns the view
                _zooming = false;
                _stack.Clear();
                SyncInGame(); // the game menu may have opened during the zoom
            });
        }
    }
}
