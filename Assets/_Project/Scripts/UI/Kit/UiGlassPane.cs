using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// A light pane: the glass lane's frosted URP material when one is assigned, otherwise the no-blur fallback
    /// (paper at the frosted or plate alpha, with the hairline edge and soft shadow the builder adds).
    /// Assign the glass material here, or on the pane prefab, to switch every pane to real glass.
    /// </summary>
    [AddComponentMenu("Close the Deal/UI/Glass Pane")]
    public sealed class UiGlassPane : MonoBehaviour
    {
        [Tooltip("The kit's tokens.")]
        [SerializeField] UiTokens _tokens;
        [Tooltip("The pane's surface.")]
        [SerializeField] Image _pane;
        [Tooltip("The real frosted glass material from the glass lane. Empty = the no-blur fallback.")]
        [SerializeField] Material _glassMaterial;
        [Tooltip("On: a frosted menu pane (a little see-through). Off: a white plate (the HUD's surface).")]
        [SerializeField] bool _frosted = true;

        public Material GlassMaterial
        {
            get => _glassMaterial;
            set { _glassMaterial = value; Apply(); }
        }

        void OnEnable() => Apply();

        public void Apply()
        {
            if (_pane == null || _tokens == null)
                return;
            if (_glassMaterial != null)
            {
                _pane.material = _glassMaterial;
                _pane.color = Color.white;
                return;
            }

            _pane.material = null;
            _pane.color = UiTokens.WithAlpha(_tokens.Paper, _frosted ? _tokens.FrostedAlpha : _tokens.PlateAlpha);
        }
    }
}
