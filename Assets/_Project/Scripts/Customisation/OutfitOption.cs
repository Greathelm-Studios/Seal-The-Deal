using UnityEngine;

namespace CloseTheDeal.Customisation
{
    /// <summary>Designer-owned colour and unlock requirement for one outfit choice. Design: docs/systems/CUSTOMISATION.md.</summary>
    [CreateAssetMenu(menuName = "Close the Deal/Customisation/Outfit Option", fileName = "Option_")]
    public sealed class OutfitOption : ScriptableObject
    {
        [Tooltip("What the player reads in the fitting room. Keep it short: two or three words, straight-faced.")]
        [SerializeField] string _displayName = "Navy";

        [Header("Colours (art bible §03 and §05)")]
        [Tooltip("The lit side of this part.")]
        [SerializeField] Color _baseColour = new(0.067f, 0.106f, 0.220f, 1f);

        [Tooltip("The shadow side. Pick it colder, not just darker, or the part looks dirty.")]
        [SerializeField] Color _shadowColour = new(0.035f, 0.050f, 0.120f, 1f);

        [Tooltip("The thin line round the part, on parts that have one. Use the opposite value to the surface: a pale line on a dark part, a dark line on a pale part.")]
        [SerializeField] Color _contourColour = new(0.067f, 0.106f, 0.220f, 1f);

        [Header("Unlock")]
        [Tooltip("How many runs the player must have started (on this PC) before they can wear this. 0 = always available. Higher = a longer wait; 5 is about an evening's play.")]
        [Min(0)] [SerializeField] int _runsToUnlock;

        public string DisplayName => _displayName;
        public Color BaseColour => _baseColour;
        public Color ShadowColour => _shadowColour;
        public Color ContourColour => _contourColour;
        public int RunsToUnlock => _runsToUnlock;

        public bool IsUnlocked(int runsStarted) => runsStarted >= _runsToUnlock;

#if UNITY_EDITOR
        /// <summary>Editor tools only: fills a freshly created option. Never called at runtime (AGENTS.md §4).</summary>
        public void EditorInit(string displayName, Color baseColour, Color shadowColour, Color contourColour, int runsToUnlock)
        {
            _displayName = displayName;
            _baseColour = baseColour;
            _shadowColour = shadowColour;
            _contourColour = contourColour;
            _runsToUnlock = runsToUnlock;
        }
#endif
    }
}
