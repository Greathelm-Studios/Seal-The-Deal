using CloseTheDeal.Lobby;
using UnityEngine;

namespace CloseTheDeal.Customisation
{
    /// <summary>The tailor's mirror in reception: press interact to step into the fitting room.</summary>
    public sealed class FittingRoomMirror : LobbyInteractable
    {
        const string KeyPrompt = "F   Change your outfit";
        const string PadPrompt = "Y   Change your outfit";

        public override string Prompt(bool gamepad)
        {
            LobbyRoster roster = LobbyRoster.Instance;
            if (FittingRoom.Instance == null || PlayerOutfit.Local == null || (roster != null && roster.RunInProgress))
                return null;
            return gamepad ? PadPrompt : KeyPrompt;
        }

        public override void OnPress()
        {
            if (FittingRoom.Instance != null)
                FittingRoom.Instance.Enter(this);
        }
    }
}
