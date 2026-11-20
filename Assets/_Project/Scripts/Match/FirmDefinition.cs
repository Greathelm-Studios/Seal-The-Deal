using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// One of the two rival law firms. Designer-owned; runtime only reads it (AGENTS.md §4).
    /// On a player's own screen their firm is drawn in the match profile's "own" colour and the
    /// rival in its "rival" colour, so the colour here is only for neutral displays.
    /// </summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Firm", fileName = "Firm")]
    public sealed class FirmDefinition : ScriptableObject
    {
        [Tooltip("The firm's name as players read it on the HUD and the results.")]
        public string DisplayName = "Firm";

        [Tooltip("The firm's own colour, used where neither side is 'yours': results shown to both firms, spectating. In play each player sees their own firm in the match profile's own-firm colour instead.")]
        public Color MarkerColour = Color.white;

        [Tooltip("Which building this firm climbs: 0 = Building A, 1 = Building B. The two firms must use different buildings.")]
        [Range(0, 1)] public int Tower;
    }
}
