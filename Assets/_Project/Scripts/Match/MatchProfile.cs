using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// Designer-owned tuning for the briefcase and the two firms. Runtime only reads it (AGENTS.md §4).
    /// Design and ownership: docs/systems/MATCH.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Match Profile", fileName = "MatchProfile")]
    public sealed class MatchProfile : ScriptableObject
    {
        [Header("Firms")]
        [Tooltip("The two rival firms.")]
        public FirmDefinition[] Firms = new FirmDefinition[0];

        [Tooltip("Colour of your own firm on your own screen: its HUD line, and the briefcase while one of you carries it.")]
        public Color OwnFirmColour = new(0f, 0.9f, 1f, 1f);

        [Tooltip("Colour of the rival firm on your own screen: its HUD line, and the briefcase while one of them carries it.")]
        public Color RivalFirmColour = new(1f, 0.62f, 0.106f, 1f);

        [Tooltip("Colour of the briefcase while nobody carries it. Keep it clear of red and yellow, which belong to the Grenade Case.")]
        public Color LooseColour = new(0.42f, 0.3f, 0.22f, 1f);

        [Header("Match flow")]
        [Tooltip("Seconds between the host starting the match and the briefcase unlocking. Longer gives everyone time to look round before the search starts.")]
        [Min(1f)] public float CountdownSeconds = 5f;

        [Header("Briefcase")]
        [Tooltip("The networked briefcase. One is spawned for the match.")]
        public NetworkObject BriefcasePrefab;

        [Tooltip("How close a player's chest must be to the briefcase to pick it up, in metres. Higher = easier grabs through a scrum; above about 3 it reaches across desks.")]
        [Min(0.5f)] public float PickupRange = 2.2f;

        [Tooltip("How fast a dropped briefcase leaves the hand, forwards, in metres per second. Higher throws it further: 2.5 lands about a metre ahead, 6 clears a desk.")]
        [Min(0f)] public float DropTossSpeed = 2.5f;

        [Tooltip("Upward speed added to a dropped briefcase, in metres per second. Higher gives a loftier hand-off.")]
        [Min(0f)] public float DropTossLift = 1.5f;

        [Tooltip("Seconds a dropped briefcase may lie outside the building before it is put back inside.")]
        [Min(0.1f)] public float StraySeconds = 2f;

        [Header("Carrying")]
        [Tooltip("Seconds the briefcase takes to move into the hand when picked up. Keep it short: 0.15 reads as a grab.")]
        [Min(0f)] public float CarryBlendSeconds = 0.15f;

        [Tooltip("Where the carrier sees the briefcase, in metres from their eye: X right, Y up, Z forward. The default tucks it into the lower LEFT corner, mostly below the view, so the way ahead stays clear and the gun (lower right) keeps its side.")]
        public Vector3 FirstPersonOffset = new(-0.34f, -0.5f, 0.8f);

        [Tooltip("How big the carrier sees their own briefcase, as a fraction of its real size. Smaller blocks less of the view; 1 is true size, which fills a corner of the screen.")]
        [Range(0.2f, 1f)] public float FirstPersonScale = 0.55f;

        [Header("Beacon")]
        [Tooltip("The see-through-walls marker above the briefcase. Leave empty for no beacon.")]
        public Material BeaconMaterial;

        [Tooltip("How far away the beacon can be seen through walls, in metres. 30 covers most of a floor; past it, the beacon fades out and only the case itself shows.")]
        [Min(1f)] public float BeaconRange = 30f;

        [Tooltip("How tall the beacon's column of light is above the case, in metres. Taller reads from further below; too tall pokes through the floor above everywhere.")]
        [Min(0.5f)] public float BeaconHeight = 3.5f;

        [Tooltip("Colour of the beacon while nobody carries the briefcase. Keep it clear of red, yellow and the firm colours.")]
        public Color BeaconLooseColour = new(0.95f, 0.97f, 1f, 1f);

        [Header("HUD")]
        [Tooltip("Seconds a message such as 'You dropped the briefcase' stays on screen.")]
        [Min(1f)] public float NoticeSeconds = 4f;

        /// <summary>The firm with this index (0 or 1), or null when none is set up for it.</summary>
        public FirmDefinition FirmByIndex(int index)
        {
            foreach (FirmDefinition firm in Firms)
            {
                if (firm != null && firm.Tower == index)
                    return firm;
            }

            return null;
        }
    }
}
