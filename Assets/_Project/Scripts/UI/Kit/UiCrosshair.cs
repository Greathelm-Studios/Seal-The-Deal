using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>HUD and settings-preview crosshair driven by UiSettings, spread (OpenPixels), and hit pulses.</summary>
    [AddComponentMenu("Seal the Deal/UI/Crosshair")]
    public sealed class UiCrosshair : MonoBehaviour, IUiAnimated
    {
        [Tooltip("The kit's tokens (crosshair colours and outline).")]
        [SerializeField] UiTokens _tokens;
        [Tooltip("The centre dot's outline, then its core.")]
        [SerializeField] Image _dotOutline;
        [SerializeField] Image _dot;
        [Tooltip("The four arms' outlines: up, down, left, right.")]
        [SerializeField] Image[] _armOutlines = new Image[4];
        [Tooltip("The four arms' cores: up, down, left, right.")]
        [SerializeField] Image[] _arms = new Image[4];

        [Header("Shape at 100% (pixels)")]
        [Tooltip("Width of the centre dot's core.")]
        [SerializeField] float _dotSize = 4f;
        [Tooltip("Length of each arm's core.")]
        [SerializeField] float _armLength = 8f;
        [Tooltip("Thickness of each arm's core.")]
        [SerializeField] float _armThickness = 2f;
        [Tooltip("Empty space between the centre and where each arm starts.")]
        [SerializeField] float _gap = 4f;
        [Tooltip("Width of the outline around every part. Stays 1 px at every size.")]
        [SerializeField] float _outline = 1f;

        static readonly Vector2[] Directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        float _open;
        int _openShown;
        UiTween _pulse;

        /// <summary>Extra spread gap in 100%-scale pixels; only redraws when whole-pixel rounded value changes.</summary>
        public float OpenPixels
        {
            get => _open;
            set
            {
                _open = Mathf.Max(0f, value);
                int whole = Mathf.RoundToInt(_open * UiSettings.CrosshairSize / 100f);
                if (whole == _openShown)
                    return;
                _openShown = whole;
                UpdateArmPositions();
            }
        }

        /// <summary>A short flash outward (a hit, a kill): strength 1 opens the arms by their own length and settles back.</summary>
        public void Pulse(float strength)
        {
            if (_tokens == null || strength <= 0f || UiMotion.Reduced)
                return;
            _pulse.Start(Mathf.Max(_pulse.Value, strength), 0f, _tokens.PanelIn, _tokens.EaseOut);
            UiMotion.Wake(this);
        }

        public bool Tick(float dt)
        {
            _pulse.Step(dt);
            UpdateArmPositions();
            return _pulse.Running;
        }

        void Awake() => UiMotion.EnsureSubCanvas(gameObject);

        void OnEnable()
        {
            UiSettings.Changed += Draw;
            Draw();
        }

        void OnDisable()
        {
            UiSettings.Changed -= Draw;
            UiMotion.Sleep(this);
        }

        /// <summary>Redraws from the saved settings.</summary>
        public void Draw()
        {
            if (_tokens == null)
                return;
            CrosshairStyle style = UiSettings.CrosshairStyle;
            float scale = UiSettings.CrosshairSize / 100f;
            Color core = ColourOf(UiSettings.CrosshairColour);
            Color edge = _tokens.Black;

            bool dot = style != CrosshairStyle.Cross;
            bool arms = style != CrosshairStyle.Dot;
            float dotSize = Whole(_dotSize * scale, 2f);
            Place(_dotOutline, _dot, dot, Vector2.zero, new Vector2(dotSize, dotSize), core, edge);

            float length = Whole(_armLength * scale, 2f);
            float thickness = Whole(_armThickness * scale, 1f);
            for (int i = 0; i < 4; i++)
            {
                Vector2 dir = Directions[i];
                Vector2 size = dir.x == 0f ? new Vector2(thickness, length) : new Vector2(length, thickness);
                StylePart(i < _armOutlines.Length ? _armOutlines[i] : null, i < _arms.Length ? _arms[i] : null, arms, size, core, edge);
            }

            UpdateArmPositions();
        }

        void UpdateArmPositions()
        {
            if (_tokens == null || UiSettings.CrosshairStyle == CrosshairStyle.Dot)
                return;
            float scale = UiSettings.CrosshairSize / 100f;
            float length = Whole(_armLength * scale, 2f);
            float pulse = _pulse.Value * length;
            float start = Whole(_gap * scale, 2f) + Mathf.Round(_open * scale) + Mathf.Round(pulse) + length * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 at = Directions[i] * start;
                if (i < _arms.Length && _arms[i] != null)
                    _arms[i].rectTransform.anchoredPosition = at;
                if (i < _armOutlines.Length && _armOutlines[i] != null)
                    _armOutlines[i].rectTransform.anchoredPosition = at;
            }
        }

        public Color ColourOf(CrosshairColour colour)
        {
            switch (colour)
            {
                case CrosshairColour.Cyan: return _tokens.Cyan;
                case CrosshairColour.ElectricBlue: return _tokens.Electric;
                default: return _tokens.Paper;
            }
        }

        void Place(Image outline, Image core, bool show, Vector2 at, Vector2 size, Color coreColour, Color edgeColour)
        {
            StylePart(outline, core, show, size, coreColour, edgeColour);
            if (core != null)
                core.rectTransform.anchoredPosition = at;
            if (outline != null)
                outline.rectTransform.anchoredPosition = at;
        }

        void StylePart(Image outline, Image core, bool show, Vector2 size, Color coreColour, Color edgeColour)
        {
            if (core != null)
            {
                core.enabled = show;
                core.rectTransform.sizeDelta = size;
                core.color = coreColour;
            }

            if (outline != null)
            {
                outline.enabled = show;
                outline.rectTransform.sizeDelta = size + Vector2.one * (_outline * 2f);
                outline.color = edgeColour;
            }
        }

        static float Whole(float pixels, float minimum) => Mathf.Max(minimum, Mathf.Round(pixels));
    }
}
