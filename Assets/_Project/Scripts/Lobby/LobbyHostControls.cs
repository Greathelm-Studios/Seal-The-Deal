using CloseTheDeal.MatchMode;
using CloseTheDeal.Player;
using CloseTheDeal.UI;
using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>Handles Tab/d-pad-up toggle and input capture for the host player list. Design: docs/systems/LOBBY.md.</summary>
    [DisallowMultipleComponent]
    public sealed class LobbyHostControls : MonoBehaviour
    {
        [Tooltip("The host's player list.")]
        [SerializeField] LobbyHostPanel _panel;

        PlayerInputReader _input;
        bool _holding;

        void Awake()
        {
            LobbyInput.Enable();
            if (_panel == null)
            {
                Debug.LogError($"[Lobby] {name}: no host player list in this scene, so Tab does nothing. Run Seal the Deal > Lanes > Set Up Lobby (or Set Up Integration) to rebuild it.", this);
                enabled = false;
                return;
            }
            _panel.Closed += GiveBack;
        }

        void OnDisable()
        {
            if (_panel != null && _panel.IsOpen)
                _panel.Close();
            else
                GiveBack();
        }

        void OnDestroy()
        {
            if (_panel != null)
                _panel.Closed -= GiveBack;
        }

        void Update()
        {
            if (_panel.IsOpen)
            {
                LobbyRoster roster = LobbyRoster.Instance;
                MatchDirector match = MatchDirector.Instance;
                if (PlayerMotor.Local == null
                    || (roster != null && roster.RunInProgress)
                    || (match != null && match.Phase != MatchPhase.Waiting))
                    _panel.Close(); // Left the game or a match started (even while Esc menu is open).
                else if (!GameMenu.OpenInGame && (LobbyInput.HostPanel.WasPerformedThisFrame() || LobbyInput.Cancel.WasPressedThisFrame()))
                    _panel.Close(); // While the Esc menu is over it, the list waits.
                return;
            }

            if (LobbyInput.HostPanel.WasPerformedThisFrame() && CanOpen())
                Open();
        }

        bool CanOpen()
        {
            LobbyRoster roster = LobbyRoster.Instance;
            LobbyInteractor interactor = LobbyInteractor.Instance;
            MatchDirector match = MatchDirector.Instance;
            return roster != null && roster.IsLocalHost && !roster.RunInProgress
                && (match == null || match.Phase == MatchPhase.Waiting)
                && PlayerMotor.Local != null && LobbyInput.Captured
                && (interactor == null || !interactor.Blocked);
        }

        void Open()
        {
            _holding = true;
            if (LobbyInteractor.Instance != null)
                LobbyInteractor.Instance.Block();
            _input = PlayerMotor.Local.GetComponent<PlayerInputReader>();
            if (_input != null)
                _input.enabled = false;
            GameMenu.HoldCursor(this); // Mouse clicks the list; Esc still opens the menu over it.
            _panel.Open(LobbyRoster.Instance);
        }

        void GiveBack()
        {
            if (!_holding)
                return;
            _holding = false;
            GameMenu.ReleaseCursor(this);
            if (_input != null)
                _input.enabled = true;
            _input = null;
            if (LobbyInteractor.Instance != null)
                LobbyInteractor.Instance.Unblock();
        }
    }
}
