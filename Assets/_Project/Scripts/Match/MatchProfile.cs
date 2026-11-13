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

        [Tooltip("Colour of your own firm on your own screen: its HUD line, and the briefcase while one of you carries it.")]
        public Color OwnFirmColour = new(0f, 0.9f, 1f, 1f);

        [Tooltip("Colour of the rival firm on your own screen: its HUD line, and the briefcase while one of them carries it.")]
        public Color RivalFirmColour = new(1f, 0.62f, 0.106f, 1f);

        [Tooltip("Colour of the briefcase while nobody carries it. Keep it clear of red and yellow, which belong to the Grenade Case.")]
        public Color LooseColour = new(0.42f, 0.3f, 0.22f, 1f);

        [Header("Match flow")]
        [Tooltip("Seconds between the host starting the match and the briefcase unlocking. Longer gives late arrivals time to reach the plaza.")]
        [Min(1f)] public float CountdownSeconds = 5f;

        [Tooltip("Seconds a match can run before the clock decides it: the firm holding the briefcase, or that carried it last, wins. 900 = the 15-minute cap.")]
        [Min(10f)] public float MatchSeconds = 900f;

        [Tooltip("Seconds left on the clock when everyone is warned the quarter is about to close. 60 = one minute's warning; 0 = no warning.")]
        [Min(0f)] public float CapWarningSeconds = 60f;

        [Header("Briefcase")]
        [Tooltip("The networked briefcase. One is spawned for the match.")]
        public NetworkObject BriefcasePrefab;

        [Tooltip("Where the briefcase starts, in metres from the centre of the plaza between the towers: X along the street, Y up, Z toward Building B. Keep Z at 0 so neither firm is closer.")]
        public Vector3 SpawnOnPlaza = new(0f, 0.3f, 0f);

        [Tooltip("How close a player's chest must be to the briefcase to pick it up, in metres. Higher = easier grabs through a scrum; above about 3 it reaches across desks.")]
        [Min(0.5f)] public float PickupRange = 2.2f;

        [Tooltip("How fast a dropped briefcase leaves the hand, forwards, in metres per second. Higher throws it further: 2.5 lands about a metre ahead, 6 clears a desk.")]
        [Min(0f)] public float DropTossSpeed = 2.5f;

        [Tooltip("Upward speed added to a dropped briefcase, in metres per second. Higher gives a loftier hand-off.")]
        [Min(0f)] public float DropTossLift = 1.5f;

        [Tooltip("Seconds a dropped briefcase may lie outside the buildings, plaza and skybridges before it is sent back.")]
        [Min(0.1f)] public float StraySeconds = 2f;

        [Tooltip("Seconds a briefcase is gone for once it has left the buildings, before it reappears on the skybridge of the highest checkpoint it reached (or on the plaza). Longer punishes losing it more.")]
        [Min(0f)] public float ReturnDelaySeconds = 3f;

        [Header("Leaving the building")]
        [Tooltip("How far outside a building's walls still counts as inside, in metres. Higher forgives leaning out of a window; too high and a player on the street is never sent back.")]
        [Min(0f)] public float OutsideMargin = 0.75f;

        [Tooltip("Seconds a player falls outside the buildings, plaza and skybridges before they are put back at their firm's last checkpoint. Longer shows more of the fall.")]
        [Min(0f)] public float FallSeconds = 1.2f;

        [Header("Crossings (plaza and skybridges)")]
        [Tooltip("Width of each skybridge between the towers, in metres. Must stay under 3.6, the gap between the window pillars, or the bridge hits them.")]
        [Range(1f, 3.5f)] public float BridgeWidth = 3f;

        [Tooltip("Height of the railings along each skybridge, in metres. 1.1 stops a walk-off but not a blast.")]
        [Min(0f)] public float RailHeight = 1.1f;

        [Header("Carrying")]
        [Tooltip("Seconds the briefcase takes to move into the hand when picked up. Keep it short: 0.15 reads as a grab.")]
        [Min(0f)] public float CarryBlendSeconds = 0.15f;

        [Tooltip("Where the carrier sees the briefcase, in metres from their eye: X right, Y up, Z forward. The default sits low on the right of the view.")]
        public Vector3 FirstPersonOffset = new(0.42f, -0.5f, 0.95f);

        [Header("Beacon")]
        [Tooltip("The see-through-walls marker above the briefcase. Leave empty for no beacon.")]
        public Material BeaconMaterial;

        [Tooltip("How far away the beacon can be seen through floors and walls, in metres. 30 covers about six floors; past it, the beacon fades out and only the case itself shows.")]
        [Min(1f)] public float BeaconRange = 30f;

        [Tooltip("How tall the beacon's column of light is above the case, in metres. Taller reads from further below; too tall pokes through the floor above everywhere.")]
        [Min(0.5f)] public float BeaconHeight = 3.5f;

        [Tooltip("Colour of the beacon while nobody carries the briefcase. Keep it clear of red, yellow and the firm colours.")]
        public Color BeaconLooseColour = new(0.95f, 0.97f, 1f, 1f);

        [Header("HUD")]
        [Tooltip("Seconds a message such as 'The roof only counts with the briefcase' stays on screen.")]
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
