using System;
using TMPro;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// A yes / no question over the current screen, on a frosted pane behind a paper veil. The safe choice (Stay)
    /// has focus when it opens and is what Esc / the pad's B does; the other choice runs the caller's action.
    /// One prefab serves every confirm: Configure sets its words and action before Push.
    /// </summary>
    [AddComponentMenu("Close the Deal/UI/Confirm Dialog")]
    public sealed class UiConfirmDialog : UiScreen
    {
        [Tooltip("The question, e.g. Quit to desktop?")]
        [SerializeField] TMP_Text _title;
        [Tooltip("One line on what happens.")]
        [SerializeField] TMP_Text _body;
        [Tooltip("The button that does it.")]
        [SerializeField] UiButton _confirm;
        [Tooltip("The safe button: closes the dialog and changes nothing. Has focus when the dialog opens.")]
        [SerializeField] UiButton _stay;
        [Tooltip("The key hint beside the buttons, e.g. 'Stay' after the Esc key cap.")]
        [SerializeField] TMP_Text _cancelHint;

        Action _onConfirm;

        public void Configure(string title, string body, string confirmLabel, string stayLabel, Action onConfirm)
        {
            if (_title != null) _title.text = title;
            if (_body != null) _body.text = body;
            if (_confirm != null && _confirm.Fx.Label != null) _confirm.Fx.Label.text = confirmLabel;
            if (_stay != null && _stay.Fx.Label != null) _stay.Fx.Label.text = stayLabel;
            if (_cancelHint != null) _cancelHint.text = stayLabel;
            _onConfirm = onConfirm;
        }

        protected override void Awake()
        {
            base.Awake();
            _firstFocus = _stay;
            if (_stay != null)
                _stay.ConfirmSound = UiSoundEvent.Back;
        }

        void OnEnable()
        {
            if (_confirm != null) _confirm.Clicked += OnConfirm;
            if (_stay != null) _stay.Clicked += OnStay;
        }

        protected override void OnDisable()
        {
            if (_confirm != null) _confirm.Clicked -= OnConfirm;
            if (_stay != null) _stay.Clicked -= OnStay;
            base.OnDisable();
        }

        /// <summary>Esc / B is Stay.</summary>
        public override void OnCancel()
        {
            UiSound.Play(UiSoundEvent.Back);
            Stack?.Pop(false);
        }

        void OnStay() => Stack?.Pop(false);

        void OnConfirm() => _onConfirm?.Invoke();
    }
}
