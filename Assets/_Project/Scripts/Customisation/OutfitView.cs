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

        [Tooltip("Every renderer that draws the character's body: one for a single-mesh model, several where the model is split into parts. Materials are matched by name across all of them.")]
        [SerializeField] Renderer[] _bodies = new Renderer[0];

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
        Target[][] _targets;
        ShadowCastingMode[] _savedShadows;
        bool[] _savedRenderingOff;

        /// <summary>One material slot on one body renderer.</summary>
        readonly struct Target
        {
            public readonly int Renderer;
            public readonly int Material;
            public Target(int renderer, int material) { Renderer = renderer; Material = material; }
        }
        OutfitLoadout _shown;
        bool _hasShown;
        float _bobTime = -1f;
        Vector3 _pivotRest = Vector3.one;
        bool _forcedVisible;

        public OutfitCatalog Catalog => _catalog;
        public OutfitLoadout Shown => _shown;
        public Renderer[] Bodies => _bodies;

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
            if (_catalog == null || _bodies.Length == 0)
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

        /// <summary>Looks up, once, where each category's material names are on the body's renderers.</summary>
        void ResolveMaterials()
        {
            _slotOrder = new OutfitSlot[_catalog.SlotCount];
            _targets = new Target[_catalog.SlotCount][];
            for (int s = 0; s < _slotOrder.Length; s++)
            {
                OutfitSlot slot = _catalog.Slot(s);
                _slotOrder[s] = slot;
                if (slot == null)
                    continue;

                var found = new System.Collections.Generic.List<Target>();
                for (int n = 0; n < slot.MaterialNameCount; n++)
                    FindMaterial(slot.MaterialName(n), found);
                _targets[s] = found.ToArray();
            }
        }

        void FindMaterial(string materialName, System.Collections.Generic.List<Target> found)
        {
            for (int r = 0; r < _bodies.Length; r++)
            {
                if (_bodies[r] == null)
                    continue;

                Material[] materials = _bodies[r].sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    // Unity names a runtime copy "Name (Instance)"; match the start so both work.
                    if (materials[i] != null && materials[i].name.StartsWith(materialName, System.StringComparison.Ordinal))
                        found.Add(new Target(r, i));
                }
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
            if (_targets == null)
                ResolveMaterials();

            int slotIndex = System.Array.IndexOf(_slotOrder, slot);
            Target[] targets = slotIndex >= 0 ? _targets[slotIndex] : null;
            if (targets == null)
                return;

            for (int t = 0; t < targets.Length; t++)
            {
                Renderer body = _bodies[targets[t].Renderer];
                if (body == null)
                    continue;
                _block.Clear();
                body.GetPropertyBlock(_block, targets[t].Material);
                _block.SetColor(BaseColourId, option.BaseColour);
                _block.SetColor(ShadowColourId, option.ShadowColour);
                _block.SetColor(ContourColourId, option.ContourColour);
                body.SetPropertyBlock(_block, targets[t].Material);
            }
        }

        /// <summary>
        /// The owner's own body is hidden in first person (PlayerMotor draws only its shadow).
        /// The fitting room needs to see it: this shows it and later puts back exactly what it found.
        /// </summary>
        public void ForceVisible(bool visible)
        {
            if (_bodies.Length == 0 || visible == _forcedVisible)
                return;

            if (visible)
            {
                _savedShadows = new ShadowCastingMode[_bodies.Length];
                _savedRenderingOff = new bool[_bodies.Length];
            }

            for (int i = 0; i < _bodies.Length; i++)
            {
                if (_bodies[i] == null)
                    continue;

                if (visible)
                {
                    _savedShadows[i] = _bodies[i].shadowCastingMode;
                    _savedRenderingOff[i] = _bodies[i].forceRenderingOff;
                    _bodies[i].shadowCastingMode = ShadowCastingMode.On;
                    _bodies[i].forceRenderingOff = false;
                }
                else
                {
                    _bodies[i].shadowCastingMode = _savedShadows[i];
                    _bodies[i].forceRenderingOff = _savedRenderingOff[i];
                }
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
