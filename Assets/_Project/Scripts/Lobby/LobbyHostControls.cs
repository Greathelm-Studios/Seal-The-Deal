using CloseTheDeal.Player;
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
            _panel.Closed += GiveBack;
        }

        void Update()
        {
            if (_panel.IsOpen)
            {
                if (PlayerMotor.Local == null || !LobbyInput.Captured)
                    _panel.Close(); // Left the game, or the game menu opened over it.
                else if (LobbyInput.HostPanel.WasPressedThisFrame() || LobbyInput.Cancel.WasPressedThisFrame())
                    _panel.Close();
                return;
            }

            if (LobbyInput.HostPanel.WasPressedThisFrame() && CanOpen())
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
            _panel.Open(LobbyRoster.Instance);
        }

        void GiveBack()
        {
            if (_input != null)
                _input.enabled = true;
            _input = null;
            if (LobbyInteractor.Instance != null)
                LobbyInteractor.Instance.Unblock();
        }
    }
}
