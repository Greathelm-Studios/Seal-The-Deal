using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// The crosshair, drawn from the player's saved settings: Dot, Cross or Cross + Dot, in Paper White, Cyan or
    /// Electric Blue, 50–200% size, always with a 1 px Near Black outline so it reads over white walls, sky and
    /// dark rooms. Redraws only when a setting changes. The HUD's crosshair and the settings screen's live
    /// preview tiles are the same component. Sizes are whole pixels at 1920 x 1080.
    /// </summary>
    [AddComponentMenu("Close the Deal/UI/Crosshair")]
    public sealed class UiCrosshair : MonoBehaviour
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

        void OnEnable()
        {
            UiSettings.Changed += Draw;
            Draw();
        }

        void OnDisable() => UiSettings.Changed -= Draw;

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
            float start = Whole(_gap * scale, 2f) + length * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 dir = Directions[i];
                Vector2 size = dir.x == 0f ? new Vector2(thickness, length) : new Vector2(length, thickness);
                Place(i < _armOutlines.Length ? _armOutlines[i] : null, i < _arms.Length ? _arms[i] : null, arms, dir * start, size, core, edge);
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
            if (core != null)
            {
                core.enabled = show;
                core.rectTransform.anchoredPosition = at;
                core.rectTransform.sizeDelta = size;
                core.color = coreColour;
            }

            if (outline != null)
            {
                outline.enabled = show;
                outline.rectTransform.anchoredPosition = at;
                outline.rectTransform.sizeDelta = size + Vector2.one * (_outline * 2f);
                outline.color = edgeColour;
            }
        }

        static float Whole(float pixels, float minimum) => Mathf.Max(minimum, Mathf.Round(pixels));
    }
}
