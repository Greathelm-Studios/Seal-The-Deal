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

        [Header("Fitting-room view")]
        [Tooltip("How far round the character the view turns to show this category, in degrees from straight in front (positive = towards the character's left). The view eases there when the player moves to this category, so the part being changed faces the camera.")]
        [Range(-180f, 180f)] [SerializeField] float _viewAngle = 30f;

        [Tooltip("How far from the character the camera stands for this category, in metres. Closer for small parts (a belt), further for the whole suit. Below 1.2 the head leaves the frame.")]
        [Range(0.8f, 4f)] [SerializeField] float _viewDistance = 2.3f;

        [Tooltip("The height the camera looks at for this category, in metres above the feet: about 1.3 for a shirt, 0.95 for the whole body, 0.9 for a belt.")]
        [Range(0.3f, 1.8f)] [SerializeField] float _viewHeight = 0.95f;

        public float ViewAngle => _viewAngle;
        public float ViewDistance => _viewDistance;
        public float ViewHeight => _viewHeight;

        public string DisplayName => _displayName;
        public int MaterialNameCount => _materialNames.Length;
        public string MaterialName(int i) => _materialNames[i];
        public int OptionCount => _options.Length;
        public OutfitOption Option(int i) => _options[i];
        public int DefaultOption => Mathf.Clamp(_defaultOption, 0, Mathf.Max(0, _options.Length - 1));

        public bool IsValidOption(int index) => index >= 0 && index < _options.Length && _options[index] != null;

#if UNITY_EDITOR
        /// <summary>Editor tools only: fills a freshly created slot. Never called at runtime (AGENTS.md §4).</summary>
        public void EditorInit(string displayName, string[] materialNames, OutfitOption[] options, int defaultOption, float viewAngle = 30f, float viewDistance = 2.3f, float viewHeight = 0.95f)
        {
            _displayName = displayName;
            _materialNames = materialNames;
            _options = options;
            _defaultOption = defaultOption;
            _viewAngle = viewAngle;
            _viewDistance = viewDistance;
            _viewHeight = viewHeight;
        }
#endif
    }
}
