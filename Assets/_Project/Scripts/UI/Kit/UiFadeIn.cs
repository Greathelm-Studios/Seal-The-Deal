using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>Fades an older (pre-kit) panel in when it appears, so it never cuts in. Uses the kit's panel timing.</summary>
    [AddComponentMenu("Seal the Deal/UI/Fade In")]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiFadeIn : MonoBehaviour, IUiAnimated
    {
        [Tooltip("The kit's tokens (panel timing).")]
        [SerializeField] UiTokens _tokens;

        CanvasGroup _group;
        UiTween _alpha;

        void OnEnable()
        {
            if (_group == null)
                _group = GetComponent<CanvasGroup>();
            if (_tokens == null)
            {
                _group.alpha = 1f;
                return;
            }

            _alpha.Start(0f, 1f, UiMotion.Reduced ? _tokens.ReducedFade : _tokens.PanelIn, _tokens.EaseOut);
            _group.alpha = 0f;
            UiMotion.Wake(this);
        }

        void OnDisable()
        {
            UiMotion.Sleep(this);
            if (_group != null)
                _group.alpha = 1f;
        }

        public bool Tick(float dt)
        {
            _group.alpha = _alpha.Step(dt);
            return _alpha.Running;
        }
    }
}
