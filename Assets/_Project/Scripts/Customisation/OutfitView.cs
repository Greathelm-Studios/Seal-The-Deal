using UnityEngine;
using UnityEngine.Rendering;

namespace CloseTheDeal.Customisation
{
    /// <summary>
    /// Draws an outfit on a character's body: each slot's chosen colours go onto that slot's
    /// material slots through one reused property block, so the shared toon materials are
    /// never touched (AGENTS.md §4 and §6). A change can answer with a short "fitting" bob.
    /// Purely visual and local; who wears what is decided by PlayerOutfit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OutfitView : MonoBehaviour
    {
        [Tooltip("The outfit choices this character can wear.")]
        [SerializeField] OutfitCatalog _catalog;

        [Tooltip("The character's body mesh, whose material list the outfit slots point into.")]
        [SerializeField] Renderer _body;

        [Tooltip("What bobs when the outfit changes. Leave empty for no motion. Must be a child made for this, never the body's own placed transform.")]
        [SerializeField] Transform _motionPivot;

        [Header("Placed figures only")]
        [Tooltip("For a figure nobody controls, such as the secretary: the choice per category it wears from the start, counted from 0 in each category's list. Leave empty on players; their outfit comes from their save.")]
        [SerializeField] int[] _wearsFromStart = new int[0];

        [Header("Change response")]
        [Tooltip("How long the bob after a change lasts, in seconds. 0.18 reads as a snap without slowing browsing; above 0.3 it lags behind fast flicking.")]
        [Range(0.05f, 0.5f)] [SerializeField] float _bobSeconds = 0.18f;

        [Tooltip("How much taller the character stretches at the top of the bob, as a fraction of their height. 0.03 is a small tailor's tug; 0.1 looks cartoonish.")]
        [Range(0f, 0.15f)] [SerializeField] float _bobStretch = 0.03f;

        static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");
        static readonly int ShadowColourId = Shader.PropertyToID("_ShadowColor");
        static readonly int ContourColourId = Shader.PropertyToID("_OutlineColor");

        MaterialPropertyBlock _block;
        int[][] _materialIndices;
        OutfitLoadout _shown;
        bool _hasShown;
        float _bobTime = -1f;
        Vector3 _pivotRest = Vector3.one;
        bool _forcedVisible;
        ShadowCastingMode _savedShadows;
        bool _savedRenderingOff;

        public OutfitCatalog Catalog => _catalog;
        public OutfitLoadout Shown => _shown;
        public Renderer Body => _body;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_motionPivot != null)
                _pivotRest = _motionPivot.localScale;
        }

        void Start()
        {
            if (_wearsFromStart.Length == 0 || _catalog == null)
                return;

            OutfitLoadout preset = _catalog.Default;
            for (int i = 0; i < _wearsFromStart.Length && i < _catalog.SlotCount; i++)
            {
                if (_catalog.Slot(i) != null && _catalog.Slot(i).IsValidOption(_wearsFromStart[i]))
                    preset = preset.With(i, _wearsFromStart[i]);
            }
            Show(preset, false);
        }

        void OnDisable()
        {
            _bobTime = -1f;
            if (_motionPivot != null)
                _motionPivot.localScale = _pivotRest;
        }

        /// <summary>Draws the outfit. Slots or options the catalog doesn't have are skipped, never guessed.</summary>
        public void Show(OutfitLoadout loadout, bool withMotion)
        {
            if (_catalog == null || _body == null)
                return;
            bool alreadyShown = _hasShown && loadout == _shown;
            if (alreadyShown && !withMotion)
                return;

            for (int s = 0; s < _catalog.SlotCount; s++)
                PaintSlot(s, loadout.Get(s));

            _shown = loadout;
            _hasShown = true;
            if (withMotion && _motionPivot != null)
                _bobTime = 0f;
        }

        OutfitSlot[] _slotOrder;

        /// <summary>Looks up, once, which of the body's material positions each category's material names are at.</summary>
        void ResolveMaterials()
        {
            Material[] materials = _body.sharedMaterials;
            _slotOrder = new OutfitSlot[_catalog.SlotCount];
            _materialIndices = new int[_catalog.SlotCount][];
            for (int s = 0; s < _slotOrder.Length; s++)
            {
                OutfitSlot slot = _catalog.Slot(s);
                _slotOrder[s] = slot;
                if (slot == null)
                    continue;

                var found = new System.Collections.Generic.List<int>(slot.MaterialNameCount);
                for (int n = 0; n < slot.MaterialNameCount; n++)
                {
                    for (int i = 0; i < materials.Length; i++)
                    {
                        // Unity names a runtime copy "Name (Instance)"; match the start so both work.
                        if (materials[i] != null && materials[i].name.StartsWith(slot.MaterialName(n)))
                            found.Add(i);
                    }
                }

                _materialIndices[s] = found.ToArray();
            }
        }

        void PaintSlot(int slotIndex, int optionIndex)
        {
            OutfitSlot slot = _catalog.Slot(slotIndex);
            if (slot != null && slot.IsValidOption(optionIndex))
                Paint(slot, slot.Option(optionIndex));
        }

        /// <summary>Puts one choice's colours on every body material its category covers.</summary>
        void Paint(OutfitSlot slot, OutfitOption option)
        {
            if (_materialIndices == null)
                ResolveMaterials();

            int slotIndex = System.Array.IndexOf(_slotOrder, slot);
            int[] indices = slotIndex >= 0 ? _materialIndices[slotIndex] : null;
            if (indices == null)
                return;

            for (int m = 0; m < indices.Length; m++)
            {
                int material = indices[m];
                _block.Clear();
                _body.GetPropertyBlock(_block, material);
                _block.SetColor(BaseColourId, option.BaseColour);
                _block.SetColor(ShadowColourId, option.ShadowColour);
                _block.SetColor(ContourColourId, option.ContourColour);
                _body.SetPropertyBlock(_block, material);
            }
        }

        /// <summary>
        /// The owner's own body is hidden in first person (PlayerMotor draws only its shadow).
        /// The fitting room needs to see it: this shows it and later puts back exactly what it found.
        /// </summary>
        public void ForceVisible(bool visible)
        {
            if (_body == null || visible == _forcedVisible)
                return;

            if (visible)
            {
                _savedShadows = _body.shadowCastingMode;
                _savedRenderingOff = _body.forceRenderingOff;
                _body.shadowCastingMode = ShadowCastingMode.On;
                _body.forceRenderingOff = false;
            }
            else
            {
                _body.shadowCastingMode = _savedShadows;
                _body.forceRenderingOff = _savedRenderingOff;
            }

            _forcedVisible = visible;
        }

        void Update()
        {
            if (_bobTime < 0f)
                return;

            _bobTime += Time.deltaTime;
            float t = Mathf.Clamp01(_bobTime / _bobSeconds);
            // One up-and-settle: a sine hump, eased out so it lands softly.
            float hump = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.5f);
            float stretch = 1f + _bobStretch * hump;
            float squeeze = 1f - _bobStretch * 0.5f * hump;
            _motionPivot.localScale = Vector3.Scale(_pivotRest, new Vector3(squeeze, stretch, squeeze));

            if (t >= 1f)
            {
                _bobTime = -1f;
                _motionPivot.localScale = _pivotRest;
            }
        }
    }
}
