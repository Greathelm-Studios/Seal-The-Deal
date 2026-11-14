using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.Customisation
{
    /// <summary>
    /// The fitting-room panel: a form-like list of outfit categories on the left of the screen,
    /// the focused one marked with a cobalt bar, and the current choice with its lock state.
    /// Slides in and out; text is rebuilt only when the player changes something, never per
    /// frame. Built by Close the Deal > Lanes > Set Up Lobby; restyle it there.
    /// </summary>
    public sealed class FittingRoomScreen : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] CanvasGroup _group;
        [SerializeField] RectTransform _panel;
        [SerializeField] TMP_Text[] _slotNames = new TMP_Text[0];
        [SerializeField] TMP_Text[] _slotValues = new TMP_Text[0];
        [SerializeField] Image[] _slotFocus = new Image[0];
        [SerializeField] Image[] _slotRows = new Image[0];
        [SerializeField] TMP_Text _optionName;
        [SerializeField] TMP_Text _optionCount;
        [SerializeField] TMP_Text _lockLine;
        [SerializeField] TMP_Text _notice;
        [SerializeField] TMP_Text _hint;

        [Header("Look")]
        [Tooltip("The focused row's fill.")]
        [SerializeField] Color _focusedRow = new(0.141f, 0.310f, 0.725f, 0.08f);

        [Tooltip("Text colour of a locked choice.")]
        [SerializeField] Color _lockedText = new(0.357f, 0.396f, 0.463f, 1f);

        [Tooltip("Normal text colour.")]
        [SerializeField] Color _text = new(0.067f, 0.106f, 0.220f, 1f);

        [Header("Motion")]
        [Tooltip("Seconds for the panel to slide in.")]
        [Range(0.05f, 0.6f)] [SerializeField] float _openSeconds = 0.22f;

        [Tooltip("Seconds for the panel to slide away.")]
        [Range(0.05f, 0.6f)] [SerializeField] float _closeSeconds = 0.3f;

        [Tooltip("How far the panel travels as it slides, in pixels at 1920 × 1080.")]
        [Range(0f, 200f)] [SerializeField] float _slideDistance = 40f;

        [Tooltip("How far the choice name kicks sideways when it changes, in pixels.")]
        [Range(0f, 60f)] [SerializeField] float _nudgeDistance = 18f;

        [Tooltip("Seconds the kick takes to settle.")]
        [Range(0.05f, 0.4f)] [SerializeField] float _nudgeSeconds = 0.14f;

        const string KeysHint = "Enter   Keep outfit      Backspace   Put back      A / D   Change      W / S   Category      Mouse, Q / R   Turn";
        const string PadHint = "A   Keep outfit      B   Put back      D-pad left / right   Change      D-pad up / down   Category      Right stick   Turn";
        const string LockedNotice = "One of these choices is still locked. Put it back, or pick another.";
        const float NoticeSeconds = 3f;

        Vector2 _panelRest;
        Vector2 _optionRest;
        float _t = -1f;
        bool _opening;
        float _nudgeT = -1f;
        float _nudgeSign;
        float _noticeUntil = -1f;

        void Awake()
        {
            _panelRest = _panel.anchoredPosition;
            _optionRest = _optionName.rectTransform.anchoredPosition;
            _group.alpha = 0f;
            if (!_opening)
                gameObject.SetActive(false);
        }

        public void Open()
        {
            // Set before activating: a screen that starts inactive runs Awake inside SetActive.
            _opening = true;
            _t = 0f;
            gameObject.SetActive(true);
            _notice.text = string.Empty;
        }

        /// <summary>Slides the form away. <paramref name="notice"/> (such as "Outfit kept.") shows while it goes, so keep and put back are unmistakable.</summary>
        public void Close(string notice = null)
        {
            if (!string.IsNullOrEmpty(notice))
                _notice.text = notice;
            _opening = false;
            _t = 0f;
        }

        public void NudgeOption(int step)
        {
            _nudgeSign = Mathf.Sign(step);
            _nudgeT = 0f;
        }

        public void ShowLockedNotice()
        {
            _notice.text = LockedNotice;
            _noticeUntil = Time.unscaledTime + NoticeSeconds;
            NudgeOption(1);
        }

        public void Show(OutfitCatalog catalog, OutfitLoadout loadout, int focused, int runsStarted, bool gamepad)
        {
            int count = catalog.SlotCount;
            for (int i = 0; i < _slotNames.Length; i++)
            {
                bool used = i < count;
                _slotRows[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                OutfitSlot slot = catalog.Slot(i);
                OutfitOption option = slot.Option(loadout.Get(i));
                bool locked = !option.IsUnlocked(runsStarted);
                _slotNames[i].text = slot.DisplayName;
                _slotValues[i].text = locked ? option.DisplayName + "  (locked)" : option.DisplayName;
                _slotValues[i].color = locked ? _lockedText : _text;
                _slotFocus[i].enabled = i == focused;
                _slotRows[i].color = i == focused ? _focusedRow : Color.clear;
            }

            OutfitSlot current = catalog.Slot(focused);
            int index = loadout.Get(focused);
            OutfitOption shown = current.Option(index);
            _optionName.text = shown.DisplayName;
            _optionCount.text = current.DisplayName + "   " + (index + 1) + " of " + current.OptionCount;
            _lockLine.text = LockLine(shown, runsStarted);
            _hint.text = gamepad ? PadHint : KeysHint;
        }

        static string LockLine(OutfitOption option, int runsStarted)
        {
            if (option.IsUnlocked(runsStarted))
                return option.RunsToUnlock > 0 ? "Unlocked" : string.Empty;

            int need = option.RunsToUnlock;
            string runs = need == 1 ? "1 run" : need + " runs";
            return "Locked. Opens after you have started " + runs + ". You have started " + runsStarted + ".";
        }

        void Update()
        {
            AnimatePanel();
            AnimateNudge();
            if (_noticeUntil > 0f && Time.unscaledTime > _noticeUntil)
            {
                _noticeUntil = -1f;
                _notice.text = string.Empty;
            }
        }

        void AnimatePanel()
        {
            if (_t < 0f)
                return;

            _t += Time.unscaledDeltaTime / (_opening ? _openSeconds : _closeSeconds);
            float t = Mathf.Clamp01(_t);
            // Ease out on the way in (arrives softly), ease in on the way out (leaves briskly).
            float shown = _opening ? 1f - (1f - t) * (1f - t) : 1f - t * t;
            _group.alpha = shown;
            _panel.anchoredPosition = _panelRest + Vector2.left * (_slideDistance * (1f - shown));

            if (t < 1f)
                return;
            _t = -1f;
            if (!_opening)
                gameObject.SetActive(false);
        }

        void AnimateNudge()
        {
            if (_nudgeT < 0f)
                return;

            _nudgeT += Time.unscaledDeltaTime / _nudgeSeconds;
            float t = Mathf.Clamp01(_nudgeT);
            float offset = (1f - t) * (1f - t) * _nudgeDistance * _nudgeSign;
            _optionName.rectTransform.anchoredPosition = _optionRest + Vector2.right * offset;
            if (t >= 1f)
                _nudgeT = -1f;
        }
    }
}
