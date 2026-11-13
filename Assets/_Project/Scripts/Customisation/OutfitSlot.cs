using UnityEngine;

namespace CloseTheDeal.Customisation
{
    /// <summary>
    /// One clear graphic category of the outfit (art bible §07): suit, shirt, shoes and so on.
    /// Each slot recolours one or more material slots of the character's body mesh.
    /// Designer content: read at runtime, never written.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Customisation/Outfit Slot", fileName = "Slot_")]
    public sealed class OutfitSlot : ScriptableObject
    {
        [Tooltip("The category name the player reads, such as Suit or Shoes.")]
        [SerializeField] string _displayName = "Suit";

        [Tooltip("The names of the character's materials this category recolours, exactly as they appear on the body's renderer (for example STDMC_lambert4_Toon). Names, not positions, so a re-exported or rigged model with the same materials in a different order still works. A name the body doesn't have is skipped.")]
        [SerializeField] string[] _materialNames = new string[0];

        [Tooltip("The choices, in the order the player flicks through them. At most 255.")]
        [SerializeField] OutfitOption[] _options = new OutfitOption[0];

        [Tooltip("The choice a player starts with when they have no save, counted from 0 in the list above. It must be unlocked from the first run.")]
        [Min(0)] [SerializeField] int _defaultOption;

        public string DisplayName => _displayName;
        public int MaterialNameCount => _materialNames.Length;
        public string MaterialName(int i) => _materialNames[i];
        public int OptionCount => _options.Length;
        public OutfitOption Option(int i) => _options[i];
        public int DefaultOption => Mathf.Clamp(_defaultOption, 0, Mathf.Max(0, _options.Length - 1));

        public bool IsValidOption(int index) => index >= 0 && index < _options.Length && _options[index] != null;

#if UNITY_EDITOR
        /// <summary>Editor tools only: fills a freshly created slot. Never called at runtime (AGENTS.md §4).</summary>
        public void EditorInit(string displayName, string[] materialNames, OutfitOption[] options, int defaultOption)
        {
            _displayName = displayName;
            _materialNames = materialNames;
            _options = options;
            _defaultOption = defaultOption;
        }
#endif
    }
}
