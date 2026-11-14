using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>What a status line is saying, shown by the shape of its mark (never colour alone).</summary>
    public enum UiStatusKind
    {
        /// <summary>A filled cobalt square: things are fine.</summary>
        Info = 0,
        /// <summary>A hollow square: waiting on something.</summary>
        Busy = 1,
        /// <summary>An amber square with a navy keyline: refused or failed, and the text says why.</summary>
        Refusal = 2
    }

    /// <summary>One line of status text with its square mark. The text changes only when told to.</summary>
    [AddComponentMenu("Close the Deal/UI/Status Line")]
    public sealed class UiStatusLine : MonoBehaviour
    {
        [Tooltip("The kit's tokens.")]
        [SerializeField] UiTokens _tokens;
        [Tooltip("The square mark before the text.")]
        [SerializeField] Image _mark;
        [Tooltip("The hollow keyline drawn around the mark when busy or refused.")]
        [SerializeField] Image _markOutline;
        [Tooltip("The status text.")]
        [SerializeField] TMP_Text _text;

        string _shown;
        UiStatusKind _kind = (UiStatusKind)(-1);

        public string Text => _shown;

        public void Set(string text, UiStatusKind kind)
        {
            if (text == _shown && kind == _kind)
                return;
            _shown = text;
            _kind = kind;
            if (_text != null)
                _text.text = text;
            if (_tokens == null)
                return;
            if (_mark != null)
            {
                _mark.enabled = kind != UiStatusKind.Busy;
                _mark.color = kind == UiStatusKind.Refusal ? _tokens.Amber : _tokens.Cobalt;
            }

            if (_markOutline != null)
            {
                _markOutline.enabled = kind != UiStatusKind.Info;
                _markOutline.color = kind == UiStatusKind.Busy ? _tokens.Cobalt : _tokens.Navy;
            }
        }
    }
}
