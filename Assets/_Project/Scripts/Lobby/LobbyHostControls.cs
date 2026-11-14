using CloseTheDeal.Player;
using CloseTheDeal.UI;
using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// Opens the host's player list when the host presses Tab (pad: View) in reception, and hands
    /// the controls to it while it is up: the body stands still and the desk, stands and mirror
    /// ignore the player. Only the host, only outside a run, never while the fitting room or the
    /// game menu is up. Local only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyHostControls : MonoBehaviour
    {
        [Tooltip("The host's player list.")]
        [SerializeField] LobbyHostPanel _panel;

        PlayerInputReader _input;

        void Awake()
        {
            LobbyInput.Enable();
            if (_panel == null)
            {
                Debug.LogError($"[Lobby] {name}: no host player list in this scene, so Tab does nothing. Run Close the Deal > Lanes > Set Up Lobby (or Set Up Integration) to rebuild it.", this);
                enabled = false;
                return;
            }
            _panel.Closed += GiveBack;
        }

        void Update()
        {
            if (_panel.IsOpen)
            {
                if (PlayerMotor.Local == null)
                    _panel.Close(); // Left the game.
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
            return roster != null && roster.IsLocalHost && !roster.RunInProgress
                && PlayerMotor.Local != null && LobbyInput.Captured
                && (interactor == null || !interactor.Blocked);
        }

        void Open()
        {
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
            GameMenu.ReleaseCursor(this);
            if (_input != null)
                _input.enabled = true;
            _input = null;
            if (LobbyInteractor.Instance != null)
                LobbyInteractor.Instance.Unblock();
        }
    }
}
