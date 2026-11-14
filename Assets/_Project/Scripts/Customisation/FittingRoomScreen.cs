using System;
using CloseTheDeal.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.Customisation
{
    /// <summary>
    /// The fitting-room form, built from the UI kit: one stepper row per outfit category (left and
    /// right change the choice, up and down move between categories), Keep outfit and Put back
    /// buttons, a line saying what the focused choice is and whether it is locked, and a status
    /// line. It is a kit screen, so it slides in and out with the kit's motion and its controls
    /// answer with the kit's sounds. It decides nothing: it tells the fitting room what the
    /// player picked. Built by Close the Deal > Lanes > Set Up Lobby.
    /// </summary>
    public sealed class FittingRoomScreen : UiScreen
    {
        [Header("Wiring")]
        [SerializeField] UiStepper[] _rows = new UiStepper[0];
        [SerializeField] TMP_Text[] _rowNames = new TMP_Text[0];
        [SerializeField] UiButton _keep;
        [SerializeField] UiButton _putBack;
        [SerializeField] TMP_Text _detail;
        [SerializeField] UiStatusLine _status;
        [SerializeField] TMP_Text _hint;
        [SerializeField] UiScreenStack _stack;

        /// <summary>The player moved focus to a category's row.</summary>
        public event Action<int> SlotFocused;

        /// <summary>The player stepped a category to another choice (slot, option).</summary>
        public event Action<int, int> OptionChosen;

        /// <summary>Keep outfit was activated (click, Enter or the pad's A on the button).</summary>
        public event Action KeepPressed;

        /// <summary>Put back was activated, or Back was pressed on the screen.</summary>
        public event Action PutBackPressed;

        const string KeysHint = "Left / Right  change     Up / Down  category     Mouse, Q / R  turn     Enter  keep     Backspace  put back";
        const string PadHint = "D-pad left / right  change     D-pad up / down  category     Right stick  turn     A  keep     B  put back";
        const string LockedReason = "One of these choices is still locked. Put it back, or pick another.";

        string[][] _optionNames;
        int _namesForRuns = -1;
        bool _open;

        public bool IsShowing => _open;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < _rows.Length; i++)
            {
                int slot = i;
                _rows[i].Changed += option => OptionChosen?.Invoke(slot, option);
            }

            _keep.Clicked += () => KeepPressed?.Invoke();
            _putBack.Clicked += () => PutBackPressed?.Invoke();
            gameObject.SetActive(false);
        }

        void OnEnable() => UiControlEvents.Focused += OnFocused;

        protected override void OnDisable()
        {
            base.OnDisable();
            UiControlEvents.Focused -= OnFocused;
        }

        void OnFocused(Selectable control, string description)
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i] == control)
                {
                    SlotFocused?.Invoke(i);
                    return;
                }
            }
        }

        public override void OnCancel() => PutBackPressed?.Invoke();

        public void Open()
        {
            _open = true;
            _status.Set(string.Empty, UiStatusKind.Info);
            _stack.Push(this);
        }

        /// <summary>Slides the form away. <paramref name="notice"/> (such as "Outfit kept.") shows in the status line as it goes, so keep and put back are unmistakable.</summary>
        public void Close(string notice = null)
        {
            if (!_open)
                return;
            _open = false;
            if (!string.IsNullOrEmpty(notice))
                _status.Set(notice, UiStatusKind.Info);
            _stack.Pop(false);
        }

        /// <summary>Keep was asked for with a locked choice in the outfit: the button refuses (amber, a shake, the reason).</summary>
        public void ShowLockedNotice()
        {
            _keep.RefusalReason = LockedReason;
            _keep.Refuse();
            _status.Set(LockedReason, UiStatusKind.Refusal);
        }

        /// <summary>Redraws the rows and the detail line from the browsed outfit. Called on a change, never per frame.</summary>
        public void Show(OutfitCatalog catalog, OutfitLoadout loadout, int focused, int runsStarted, bool gamepad)
        {
            BuildNames(catalog, runsStarted);
            for (int i = 0; i < _rows.Length; i++)
            {
                bool used = i < catalog.SlotCount;
                _rows[i].transform.parent.gameObject.SetActive(used);
                if (!used)
                    continue;
                _rowNames[i].text = catalog.Slot(i).DisplayName;
                _rows[i].SetOptions(_optionNames[i], loadout.Get(i));
            }

            OutfitSlot slot = catalog.Slot(focused);
            OutfitOption shown = slot.Option(loadout.Get(focused));
            _detail.text = slot.DisplayName + ": " + shown.DisplayName + ".  " + LockLine(shown, runsStarted);
            _hint.text = gamepad ? PadHint : KeysHint;
        }

        /// <summary>The option names per category, with "(locked)" on locked ones. Rebuilt only when the runs count changes.</summary>
        void BuildNames(OutfitCatalog catalog, int runsStarted)
        {
            if (_optionNames != null && _optionNames.Length == catalog.SlotCount && _namesForRuns == runsStarted)
                return;

            _namesForRuns = runsStarted;
            _optionNames = new string[catalog.SlotCount][];
            for (int s = 0; s < catalog.SlotCount; s++)
            {
                OutfitSlot slot = catalog.Slot(s);
                _optionNames[s] = new string[slot.OptionCount];
                for (int o = 0; o < slot.OptionCount; o++)
                {
                    OutfitOption option = slot.Option(o);
                    _optionNames[s][o] = option.IsUnlocked(runsStarted) ? option.DisplayName : option.DisplayName + "  (locked)";
                }
            }
        }

        static string LockLine(OutfitOption option, int runsStarted)
        {
            if (option.IsUnlocked(runsStarted))
                return option.RunsToUnlock > 0 ? "Unlocked." : string.Empty;

            int need = option.RunsToUnlock;
            string runs = need == 1 ? "1 run" : need + " runs";
            return "Locked: opens after you have started " + runs + ". You have started " + runsStarted + ".";
        }
    }
}
