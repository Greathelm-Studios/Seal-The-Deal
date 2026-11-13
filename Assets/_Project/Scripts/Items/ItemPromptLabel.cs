using TMPro;
using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>
    /// Shows the local player's item prompts on screen: what can be picked up, what is being
    /// carried and what its buttons do, and why the host refused something. Listens to the
    /// local ItemCarrier's events; text is rebuilt only when one of them fires.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemPromptLabel : MonoBehaviour
    {
        [Tooltip("The line under the crosshair: \"F · Pick up Chair\", or why something was refused.")]
        [SerializeField] TMP_Text _prompt;

        [Tooltip("The lines at the bottom of the screen naming what is carried and its buttons.")]
        [SerializeField] TMP_Text _carried;

        [Tooltip("How long a refusal such as \"Someone else got it\" stays on screen, in seconds.")]
        [Min(0.2f)] [SerializeField] float _refusalSeconds = 1.6f;

        ItemCarrier _carrier;
        string _lookText = "";
        float _refusalLeft;

        void OnEnable()
        {
            ItemCarrier.LocalChanged += Bind;
            Bind(ItemCarrier.Local);
        }

        void OnDisable()
        {
            ItemCarrier.LocalChanged -= Bind;
            Bind(null);
        }

        void Update()
        {
            if (_refusalLeft <= 0f)
                return;

            _refusalLeft -= Time.deltaTime;
            if (_refusalLeft <= 0f)
                SetText(_prompt, _lookText);
        }

        void Bind(ItemCarrier carrier)
        {
            if (_carrier != null)
            {
                _carrier.PromptChanged -= OnPromptChanged;
                _carrier.HeldChanged -= OnHeldChanged;
                _carrier.Denied -= OnDenied;
            }

            _carrier = carrier;
            _lookText = "";
            _refusalLeft = 0f;
            SetText(_prompt, "");
            SetText(_carried, "");

            if (_carrier == null)
                return;

            _carrier.PromptChanged += OnPromptChanged;
            _carrier.HeldChanged += OnHeldChanged;
            _carrier.Denied += OnDenied;
            OnHeldChanged(_carrier.Held);
        }

        void OnPromptChanged(PromptKind kind, ItemDefinition item)
        {
            _lookText = kind switch
            {
                PromptKind.PickUp => "F · Pick up " + item.DisplayName,
                PromptKind.TooHeavy => item.DisplayName + " · Too heavy to lift",
                _ => ""
            };

            if (_refusalLeft <= 0f)
                SetText(_prompt, _lookText);
        }

        void OnHeldChanged(Pickup held)
        {
            if (held == null)
            {
                SetText(_carried, "");
                return;
            }

            ItemDefinition item = held.Definition;
            string buttons = "LMB · Throw     G · Drop";
            if (!string.IsNullOrEmpty(item.UseVerb))
                buttons += "     RMB · " + item.UseVerb;
            SetText(_carried, item.DisplayName + "\n" + buttons);
        }

        void OnDenied(DenyReason reason)
        {
            string text = reason switch
            {
                DenyReason.Gone => "Someone else got it",
                DenyReason.HandsFull => "Hands full · G to drop",
                DenyReason.TooHeavy => "Too heavy to lift",
                DenyReason.TooFar => "Too far away",
                DenyReason.CannotUseHere => "No room to put it down here",
                _ => ""
            };

            if (text.Length == 0)
                return;

            _refusalLeft = _refusalSeconds;
            SetText(_prompt, text);
        }

        static void SetText(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text;
        }
    }
}
