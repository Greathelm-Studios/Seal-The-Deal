using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// Designer-owned tuning for the briefcase game mode. Runtime only reads it (AGENTS.md §4).
    /// Design and ownership: docs/systems/MATCH.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Match Profile", fileName = "MatchProfile")]
    public sealed class MatchProfile : ScriptableObject
    {
        [Header("Firms")]
        [Tooltip("The two rival firms, one per building.")]
        public FirmDefinition[] Firms = new FirmDefinition[0];

        [Tooltip("Colour of your own firm's briefcase and HUD line, on your own screen.")]
        public Color OwnFirmColour = new(0f, 0.9f, 1f, 1f);

        [Tooltip("Colour of the rival firm's briefcase and HUD line, on your own screen.")]
        public Color RivalFirmColour = new(1f, 0.62f, 0.106f, 1f);

        [Header("Match flow")]
        [Tooltip("Seconds between the host starting the match and the briefcases unlocking. Longer gives late arrivals time to find the desk.")]
        [Min(1f)] public float CountdownSeconds = 5f;

        [Tooltip("Seconds a match can run before the clock decides it: the higher briefcase wins. 900 = the 15-minute cap.")]
        [Min(10f)] public float MatchSeconds = 900f;

        [Header("Briefcase")]
        [Tooltip("The networked briefcase. One is spawned per firm.")]
        public NetworkObject BriefcasePrefab;

        [Tooltip("Where each firm's briefcase starts, in metres from the centre of the reception's window edge: X along the street, Y up, Z negative into the floor. The default sits on the reception desk.")]
        public Vector3 HomeInReception = new(8.4f, 1.15f, -9.1f);

        [Tooltip("How close a player's chest must be to their briefcase to pick it up, in metres. Higher = easier grabs through a scrum; above about 3 it reaches across desks.")]
        [Min(0.5f)] public float PickupRange = 2.2f;

        [Tooltip("How fast a dropped briefcase leaves the hand, forwards, in metres per second. Higher throws it further: 2.5 lands about a metre ahead, 6 clears a desk.")]
        [Min(0f)] public float DropTossSpeed = 2.5f;

        [Tooltip("Upward speed added to a dropped briefcase, in metres per second. Higher gives a loftier hand-off.")]
        [Min(0f)] public float DropTossLift = 1.5f;

        [Tooltip("Seconds a dropped briefcase may lie outside its own building before it is sent back to the firm's last checkpoint.")]
        [Min(0.1f)] public float StraySeconds = 2f;

        [Tooltip("Seconds a briefcase is gone for once it has left the building, before it reappears at the firm's last checkpoint. Longer punishes losing it more.")]
        [Min(0f)] public float ReturnDelaySeconds = 3f;

        [Header("Rival kick")]
        [Tooltip("How close a rival must come to a loose briefcase to kick it, in metres from its centre. Higher makes it easier to boot in passing.")]
        [Min(0.3f)] public float KickRadius = 0.8f;

        [Tooltip("How fast a kicked briefcase leaves the rival's foot, in metres per second. 7 slides it across a floor; 12 can put it through a window.")]
        [Min(0f)] public float KickSpeed = 7f;

        [Tooltip("Upward speed added to a kick, in metres per second. Higher lobs it; 0 skims the floor.")]
        [Min(0f)] public float KickLift = 2.5f;

        [Tooltip("Seconds before the same briefcase can be kicked again. Lower lets a rival dribble it.")]
        [Min(0.1f)] public float KickCooldownSeconds = 0.6f;

        [Header("Leaving the building")]
        [Tooltip("How far outside a building's walls still counts as inside, in metres. Higher forgives leaning out of a window; too high and a player on the street is never sent back.")]
        [Min(0f)] public float OutsideMargin = 0.75f;

        [Tooltip("Seconds a player falls outside the buildings before they are put back at their firm's last checkpoint. Longer shows more of the fall.")]
        [Min(0f)] public float FallSeconds = 1.2f;

        [Header("Carrying")]
        [Tooltip("Seconds the briefcase takes to move into the hand when picked up. Keep it short: 0.15 reads as a grab.")]
        [Min(0f)] public float CarryBlendSeconds = 0.15f;

        [Tooltip("Where the carrier sees their own briefcase, in metres from their eye: X right, Y up, Z forward. The default sits low on the right of the view.")]
        public Vector3 FirstPersonOffset = new(0.42f, -0.5f, 0.95f);

        [Header("HUD")]
        [Tooltip("Seconds a message such as 'The roof only counts with your briefcase' stays on screen.")]
        [Min(1f)] public float NoticeSeconds = 4f;

        /// <summary>The firm that climbs the given building, or null when none is set up for it.</summary>
        public FirmDefinition FirmInTower(int tower)
        {
            foreach (FirmDefinition firm in Firms)
            {
                if (firm != null && firm.Tower == tower)
                    return firm;
            }

            return null;
        }
    }
}
