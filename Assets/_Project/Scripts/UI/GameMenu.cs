using System;
using System.Collections.Generic;
using CloseTheDeal.Net;
using CloseTheDeal.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// The playtest menu and HUD. Owns three things together so they can never disagree:
    /// whether the menu is open, whether the HUD shows, and whether the mouse is captured.
    /// Menu open = mouse free and the player stands still; menu closed = mouse captured, HUD up.
    /// The menu opens by itself whenever there is no local player (before joining, after
    /// leaving, on disconnect); in game, Esc or the pad's Start button toggles it. Greybox, not the final lobby.
    /// Text is rebuilt only on lobby events, never per frame. Design: docs/systems/MENU_AND_HUD.md.
    /// The UI kit's title screen takes over the "not in a game" state when present (HandTitleToKit); this
    /// card then shows only while hosting, joining or in a game. Closing waits for the confirm button to
    /// be let go, because the pad's A is both UI Submit and Jump (docs/systems/UI_KIT.md).
    /// </summary>
    public sealed class GameMenu : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] SteamLobby _lobby;
        [SerializeField] GameObject _menuRoot;
        [SerializeField] GameObject _hudRoot;

        [Header("Header")]
        [SerializeField] TMP_Text _modeText;
        [SerializeField] TMP_Text _statusText;

        [Header("Not in a game")]
        [SerializeField] GameObject _startSection;
        [SerializeField] UnityEngine.UI.Button _hostButton;
        [SerializeField] GameObject _joinHint;
        [SerializeField] UnityEngine.UI.Button _joinLocalButton;

        [Header("In a game")]
        [SerializeField] GameObject _gameSection;
        [SerializeField] TMP_Text _playersText;
        [SerializeField] UnityEngine.UI.Button _inviteButton;
        [SerializeField] GameObject _inviteHint;
        [SerializeField] UnityEngine.UI.Button _resumeButton;
        [SerializeField] UnityEngine.UI.Button _leaveButton;

        bool _open = true;
        bool _inGame;
        bool _titleHandedOff;
        bool _closeOnRelease;
        bool _lockOnRelease;
        int _closedFrame = -1;
        string _lastPlayers;
        string _lastMode;

        // ---- Cursor holds (another lane's screen that needs the mouse) ------------------------------

        static readonly HashSet<object> Holds = new();
        static readonly Predicate<object> Gone = o => o is UnityEngine.Object u && u == null;

        /// <summary>Raised when a hold is added or released (not for holds dropped because their owner was destroyed).</summary>
        static event Action HoldsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Holds.Clear();
            HoldsChanged = null;
            OpenInGame = false;
        }

        /// <summary>
        /// True while this menu is open over a game. A screen that holds the cursor reads this to stand still while the
        /// menu is over it (Esc opens the menu on top of a held screen).
        /// </summary>
        public static bool OpenInGame { get; private set; }

        /// <summary>
        /// Frees the mouse for a screen of the owner's own (a fitting room, a host's player list) without opening this
        /// menu. Reference-counted per owner: while any hold exists in a game the cursor is free and visible, the
        /// player stands still (every input reader reads the cursor lock) and the HUD stays up. Esc still opens this
        /// menu over it. Holding twice with the same owner counts once. Leaving the game drops every hold, and an
        /// owner that is destroyed stops counting by itself; release it when its screen closes.
        /// </summary>
        public static void HoldCursor(object owner)
        {
            if (owner != null && Holds.Add(owner))
                HoldsChanged?.Invoke();
        }

        /// <summary>Gives the mouse back to the game once no hold is left (after the confirm button is let go).</summary>
        public static void ReleaseCursor(object owner)
        {
            if (owner != null && Holds.Remove(owner))
                HoldsChanged?.Invoke();
        }

        /// <summary>True while some screen holds the cursor free.</summary>
        public static bool CursorHeld
        {
            get
            {
                Holds.RemoveWhere(Gone);
                return Holds.Count > 0;
            }
        }

        static void DropAllHolds()
        {
            if (Holds.Count == 0)
                return;
            Holds.Clear(); // no event: the caller applies the cursor once, with the menu's new state
        }

        /// <summary>Raised when the menu opens or closes, a game starts or ends, or the lobby changes.</summary>
        public event Action StateChanged;

        public bool IsOpen => _open;

        /// <summary>What the menu last asked of the mouse: true = free and visible, false = captured.</summary>
        public bool CursorFree { get; private set; } = true;

        public bool InGame => _inGame;

        public SteamLobby Lobby => _lobby;

        /// <summary>Not in a game and not hosting or joining: the title screen's state.</summary>
        public bool AtTitle => _open && !_inGame && !_lobby.IsActive;

        /// <summary>
        /// No player of ours in the world: the title, hosting, joining, loading and any failure. The kit draws all of
        /// it while it is in the scene; this card shows only in a game.
        /// </summary>
        public bool OutOfGame => !_inGame;

        /// <summary>
        /// The kit calls this while it is in the scene: the card then stays hidden whenever we are out of a game
        /// (title, hosting, joining, loading, failures) and the kit draws those instead. The in-game card, the cursor
        /// rule and the HUD stay here.
        /// </summary>
        public void HandTitleToKit(bool handed)
        {
            _titleHandedOff = handed;
            ShowCard();
            if (_open)
                SelectFirst();
        }

        void OnEnable()
        {
            _lobby.OnStatus += SetStatus;
            _lobby.OnChanged += Refresh;
            HoldsChanged += ApplyCursor;
            ApplyCursor(); // holds may have changed while this was switched off

            _hostButton.onClick.AddListener(_lobby.Host);
            _joinLocalButton.onClick.AddListener(_lobby.JoinLocal);
            _inviteButton.onClick.AddListener(_lobby.Invite);
            _resumeButton.onClick.AddListener(Resume);
            _leaveButton.onClick.AddListener(_lobby.Leave);
        }

        void OnDisable()
        {
            _lobby.OnStatus -= SetStatus;
            _lobby.OnChanged -= Refresh;
            HoldsChanged -= ApplyCursor;

            _hostButton.onClick.RemoveListener(_lobby.Host);
            _joinLocalButton.onClick.RemoveListener(_lobby.JoinLocal);
            _inviteButton.onClick.RemoveListener(_lobby.Invite);
            _resumeButton.onClick.RemoveListener(Resume);
            _leaveButton.onClick.RemoveListener(_lobby.Leave);
        }

        void Start()
        {
            SetOpen(true);
            Refresh();
        }

        void Update()
        {
            TrackGame();
            FinishPendingClose();
            FinishPendingLock();
            if (!_inGame)
                return;

            if (MenuButtonPressed() && Time.frameCount != _closedFrame) // not the press that just closed it from outside
            {
                if (_open)
                    Close();
                else
                    SetOpen(true);
            }
            else if (!_open && !_lockOnRelease && Cursor.lockState != CursorLockMode.Locked && !CursorHeld)
            {
                if (CursorFree)
                    ApplyCursor(); // The holder was destroyed without releasing: the mouse goes back to the game.
                else
                    SetOpen(true); // The mouse was freed elsewhere (editor Esc, alt-tab): show the menu to match.
            }
        }

        /// <summary>A player spawning closes the menu; losing it (leaving, a drop) opens it.</summary>
        void TrackGame()
        {
            bool inGame = PlayerMotor.Local != null;
            if (inGame == _inGame)
                return;

            _inGame = inGame;
            OpenInGame = _open && inGame;
            DropAllHolds(); // a hold belongs to one game: none survives into the next, or out to the title
            if (inGame)
                Close();
            else
                SetOpen(true);
            Refresh();
        }

        /// <summary>A close asked for while the confirm button was down happens once it is let go.</summary>
        void FinishPendingClose()
        {
            if (!_closeOnRelease || UiInput.SubmitHeld)
                return;
            _closeOnRelease = false;
            if (_open && _inGame)
                SetOpen(false);
        }

        /// <summary>A cursor lock asked for while the confirm button was down happens once it is let go.</summary>
        void FinishPendingLock()
        {
            if (_lockOnRelease && !UiInput.SubmitHeld)
                ApplyCursor();
        }

        static bool MenuButtonPressed()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                || (pad != null && pad.startButton.wasPressedThisFrame);
        }

        // ---- Open / close --------------------------------------------------------------------

        public void Resume()
        {
            if (!_inGame)
                return;
            Close();
            if (!_open)
                _closedFrame = Time.frameCount; // an Esc that closed it from outside this frame must not reopen it here
        }

        /// <summary>
        /// Closes the menu, but not while a confirm button is still held: the pad's A is UI Submit and
        /// also Jump, so handing the mouse back on the press frame would fire or jump. It closes on release.
        /// </summary>
        void Close()
        {
            if (!_open)
                return;
            if (UiInput.SubmitHeld)
            {
                _closeOnRelease = true;
                return;
            }

            SetOpen(false);
        }

        /// <summary>Opens the menu from outside (the match ending, say). The rule holds: mouse free, player idle.</summary>
        public void Open()
        {
            _closeOnRelease = false; // A close still waiting for the confirm button is overruled (a match ending).
            if (_inGame && !_open)
                SetOpen(true);
        }

        void SetOpen(bool open)
        {
            _open = open;
            OpenInGame = open && _inGame;
            ShowCard();
            _hudRoot.SetActive(!open && _inGame);
            ApplyCursor();

            if (open)
                SelectFirst();
            StateChanged?.Invoke();
        }

        /// <summary>
        /// The mouse is free while the menu is open, out of a game, or held by another screen (HoldCursor); otherwise
        /// it is captured, but never while the confirm button is still down (the pad's A is also Jump).
        /// </summary>
        void ApplyCursor()
        {
            bool free = _open || !_inGame || CursorHeld;
            if (!free && UiInput.SubmitHeld)
            {
                _lockOnRelease = true;
                return;
            }

            _lockOnRelease = false;
            CursorFree = free;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        void ShowCard()
        {
            bool kitHasTitle = _titleHandedOff && !_inGame;
            bool show = _open && !kitHasTitle;
            if (_menuRoot.activeSelf != show)
                _menuRoot.SetActive(show);
        }

        /// <summary>Puts keyboard and pad focus on the most likely next action.</summary>
        void SelectFirst()
        {
            if (EventSystem.current == null || !_menuRoot.activeInHierarchy)
                return;

            UnityEngine.UI.Button first = _inGame ? _resumeButton : _hostButton;
            if (_lobby.IsActive && !_inGame)
                first = _leaveButton;
            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        // ---- State ---------------------------------------------------------------------------

        void SetStatus(string text)
        {
            _statusText.text = text;
            Refresh();
        }

        void Refresh()
        {
            bool active = _lobby.IsActive;
            bool local = _lobby.LocalMode;

            _startSection.SetActive(!active);
            _gameSection.SetActive(active);

            _hostButton.interactable = _lobby.Ready;
            _joinHint.SetActive(!local);
            _joinLocalButton.gameObject.SetActive(local);
            _joinLocalButton.interactable = _lobby.Ready;

            _inviteButton.gameObject.SetActive(_lobby.CanInviteViaOverlay);
            _inviteHint.SetActive(active && _lobby.InLobby);
            _resumeButton.gameObject.SetActive(_inGame);

            SetIfChanged(_playersText, ref _lastPlayers, active ? $"Players  {_lobby.PlayerCount} / {_lobby.MaxPlayers}" : string.Empty);
            SetIfChanged(_modeText, ref _lastMode, ModeLine(local));

            bool cardWasHidden = !_menuRoot.activeSelf;
            ShowCard();
            if (cardWasHidden && _menuRoot.activeSelf)
                SelectFirst();
            StateChanged?.Invoke();
        }

        string ModeLine(bool local)
        {
            if (!_lobby.Ready)
                return "Starting...";
            if (local)
                return SteamService.IsReady ? "Local test mode (forced)" : "Local test mode · Steam isn't running";
            return "Steam · signed in as " + SteamService.LocalName;
        }

        static void SetIfChanged(TMP_Text target, ref string last, string value)
        {
            if (value == last)
                return;
            last = value;
            target.text = value;
        }
    }
}
