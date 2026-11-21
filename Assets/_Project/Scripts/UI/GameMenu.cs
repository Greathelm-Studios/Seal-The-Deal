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
    /// <summary>Owns menu open state, HUD visibility, cursor lock/holds, and kit handoff.</summary>
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
        bool _gameMenuHandedOff;
        bool _closeOnRelease;
        bool _lockOnRelease;
        int _closedFrame = -1;
        string _lastPlayers;
        string _lastMode;

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

        /// <summary>True while the menu is open over an active game (other cursor-holding screens pause under it).</summary>
        public static bool OpenInGame { get; private set; }

        /// <summary>Reference-counted per-owner cursor hold for external in-game screens; dropped on game exit or owner destroy.</summary>
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

        /// <summary>True when not in a game and not hosting or joining.</summary>
        public bool AtTitle => _open && !_inGame && !_lobby.IsActive;

        public bool OutOfGame => !_inGame;

        /// <summary>Hands title (and optionally in-game menu) rendering to the UI kit while keeping cursor/HUD ownership here.</summary>
        public void HandTitleToKit(bool handed, bool inGameToo = false)
        {
            _titleHandedOff = handed;
            _gameMenuHandedOff = handed && inGameToo;
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

            // With the kit drawing the menu, Esc / B / Start close it there (one owner per press); here they only open it.
            bool kitCloses = _gameMenuHandedOff && _open && KitMenuUp;
            if (MenuButtonPressed() && !kitCloses && Time.frameCount != _closedFrame) // not the press that just closed it from outside
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

        void FinishPendingClose()
        {
            if (!_closeOnRelease || UiInput.SubmitHeld)
                return;
            _closeOnRelease = false;
            if (_open && _inGame)
                SetOpen(false);
        }

        void FinishPendingLock()
        {
            if (_lockOnRelease && !UiInput.SubmitHeld)
                ApplyCursor();
        }

        static bool MenuButtonPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || PadMenuPressed();
        }

        /// <summary>The pad's menu button (Start / Menu / Options) went down this frame: it toggles the whole menu.</summary>
        internal static bool PadMenuPressed()
        {
            Gamepad pad = Gamepad.current;
            return pad != null && pad.startButton.wasPressedThisFrame;
        }

        // Only true while kit pane is active; while zooming in, GameMenu keeps Esc/Start so input never sticks.
        internal bool KitMenuUp { get; set; }

        public void Resume()
        {
            if (!_inGame)
                return;
            Close();
            if (!_open)
                _closedFrame = Time.frameCount; // an Esc that closed it from outside this frame must not reopen it here
        }

        // Defer close while Submit is held so pad A (also Jump) does not fire/jump on the frame cursor locks.
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
            bool kitDraws = _inGame ? _gameMenuHandedOff : _titleHandedOff;
            bool show = _open && !kitDraws;
            if (_menuRoot.activeSelf != show)
                _menuRoot.SetActive(show);
        }

        void SelectFirst()
        {
            if (EventSystem.current == null || !_menuRoot.activeInHierarchy)
                return;

            UnityEngine.UI.Button first = _inGame ? _resumeButton : _hostButton;
            if (_lobby.IsActive && !_inGame)
                first = _leaveButton;
            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

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
