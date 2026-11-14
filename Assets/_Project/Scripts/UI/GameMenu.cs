using System;
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
        string _lastPlayers;
        string _lastMode;

        /// <summary>Raised when the menu opens or closes, a game starts or ends, or the lobby changes.</summary>
        public event Action StateChanged;

        public bool IsOpen => _open;

        public bool InGame => _inGame;

        public SteamLobby Lobby => _lobby;

        /// <summary>Not in a game and not hosting or joining: the title screen's state.</summary>
        public bool AtTitle => _open && !_inGame && !_lobby.IsActive;

        /// <summary>
        /// The kit's title screen calls this while it is in the scene: the card then stays hidden at the title
        /// and the kit draws it instead. Everything else (in-game card, cursor rule, HUD) stays here.
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
            if (!_inGame)
                return;

            if (MenuButtonPressed())
            {
                if (_open)
                    Close();
                else
                    SetOpen(true);
            }
            else if (!_open && Cursor.lockState != CursorLockMode.Locked)
                SetOpen(true); // The mouse was freed elsewhere (editor Esc, alt-tab): show the menu to match.
        }

        /// <summary>A player spawning closes the menu; losing it (leaving, a drop) opens it.</summary>
        void TrackGame()
        {
            bool inGame = PlayerMotor.Local != null;
            if (inGame == _inGame)
                return;

            _inGame = inGame;
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
            if (_inGame)
                Close();
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
            if (_inGame && !_open)
                SetOpen(true);
        }

        void SetOpen(bool open)
        {
            _open = open;
            ShowCard();
            _hudRoot.SetActive(!open && _inGame);
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;

            if (open)
                SelectFirst();
            StateChanged?.Invoke();
        }

        void ShowCard()
        {
            bool kitHasTitle = _titleHandedOff && !_inGame && !_lobby.IsActive;
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
