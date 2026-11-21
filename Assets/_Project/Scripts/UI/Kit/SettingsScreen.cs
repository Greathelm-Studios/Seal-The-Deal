using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>Tabbed Settings screen (Controls, Video, Audio, Accessibility) with immediate apply and flush on close.</summary>
    [AddComponentMenu("Close the Deal/UI/Settings Screen")]
    public sealed class SettingsScreen : UiScreen
    {
        public override CloseTheDeal.UI.Title.UiTransitionKind? Through => CloseTheDeal.UI.Title.UiTransitionKind.PaneSlide;

        [Header("Tabs")]
        [SerializeField] UiTabPages _tabs;
        [Tooltip("The breadcrumb's last part names the open tab.")]
        [SerializeField] TMP_Text _breadcrumb;
        [Tooltip("The section header above the rows names the open tab.")]
        [SerializeField] TMP_Text _section;

        [Header("Footer")]
        [SerializeField] UiButton _reset;
        [SerializeField] UiButton _back;
        [Tooltip("Says what was saved, or why a press was refused.")]
        [SerializeField] UiStatusLine _status;
        [Tooltip("The focused row's one-line explanation.")]
        [SerializeField] TMP_Text _help;

        [Header("Changes summary (under the rows)")]
        [Tooltip("The block under the rows listing what differs from how the game shipped. Hidden on tabs whose rows fill the page.")]
        [SerializeField] CanvasGroup _changes;
        [Tooltip("The block's heading, e.g. § CHANGED ON THIS TAB · 2.")]
        [SerializeField] TMP_Text _changesHeading;
        [Tooltip("One line per changed setting, or the line saying nothing has changed.")]
        [SerializeField] TMP_Text _changesBody;

        readonly System.Collections.Generic.List<string> _changeLines = new(8);
        string _shownChanges;

        const string KeptLine = "Changes are saved on this PC as you make them.";

        protected override void Awake()
        {
            base.Awake();
            _back.ConfirmSound = UiSoundEvent.Back;
            _reset.RefusalReason = "Everything on this tab is already at its default.";
        }

        void OnEnable()
        {
            _reset.Clicked += OnReset;
            _back.Clicked += OnBack;
            _tabs.Changed += OnTab;
            UiControlEvents.Focused += OnFocused;
            UiControlEvents.Refused += OnRefused;
        }

        protected override void OnDisable()
        {
            _reset.Clicked -= OnReset;
            _back.Clicked -= OnBack;
            _tabs.Changed -= OnTab;
            UiControlEvents.Focused -= OnFocused;
            UiControlEvents.Refused -= OnRefused;
            if (_subscribed != null)
                _subscribed.Saved -= OnSaved;
            _subscribed = null; // Subscribed again when the screen next shows.
            base.OnDisable();
        }

        void Update()
        {
            if (Stack != null && Stack.Top == this && !UiInput.Capturing)
                _tabs.ReadShortcuts();
        }

        protected override void OnShowing()
        {
            if (_subscribed != _tabs.CurrentPage)
                OnTab(_tabs.Current);
            UiSettingsPage page = _tabs.CurrentPage;
            if (page != null)
                page.ShowSaved();
            Link();
            _firstFocus = _tabs.CurrentTab;
            _status.Set(KeptLine, UiStatusKind.Info);
        }

        protected override void OnHiding()
        {
            UiSettingsPage page = _tabs.CurrentPage;
            if (page != null)
                page.Hidden();
            UiSettings.Flush();
            PlayerPrefs.Save(); // Video, audio and controls preferences too.
        }

        UiSettingsPage _subscribed;

        void OnTab(int index)
        {
            if (_subscribed != null)
                _subscribed.Saved -= OnSaved;
            _subscribed = _tabs.CurrentPage;
            _subscribed.Saved += OnSaved;
            string title = _subscribed.Title;
            if (_breadcrumb != null)
                _breadcrumb.text = "SEAL THE DEAL · SETTINGS · " + title.ToUpperInvariant();
            if (_section != null)
                _section.text = "§ " + (index + 1) + "  " + title.ToUpperInvariant();
            Link();
        }

        void OnSaved(string line)
        {
            _status.Set(line, UiStatusKind.Info);
            Link();
        }

        void Link()
        {
            UiSettingsPage page = _tabs.CurrentPage;
            if (page == null)
                return;
            _reset.interactable = !page.AtDefaults;
            Selectable footer = _reset.interactable ? _reset : (Selectable)_back;
            UiButton tab = _tabs.CurrentTab;
            page.LinkRows(tab, footer);
            Selectable first = page.FirstRow != null ? page.FirstRow : footer;
            Selectable last = page.LastRow != null ? page.LastRow : tab;

            for (int i = 0; i < _tabs.Count; i++)
            {
                UiButton t = _tabs.TabAt(i);
                Navigation nav = t.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnDown = first;
                t.navigation = nav;
            }

            _reset.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = last, selectOnRight = _back };
            _back.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = last, selectOnLeft = _reset.interactable ? _reset : null };
            ShowChanges();
        }

        void ShowChanges()
        {
            UiSettingsPage page = _tabs.CurrentPage;
            if (_changes == null || page == null)
                return;
            bool room = page.ShowsChanges;
            _changes.alpha = room ? 1f : 0f;
            if (!room)
                return;
            _changeLines.Clear();
            page.DescribeChanges(_changeLines);
            string body = _changeLines.Count == 0
                ? "Nothing on this tab differs from how the game shipped."
                : string.Join("\n", _changeLines);
            if (body == _shownChanges)
                return;
            _shownChanges = body;
            if (_changesHeading != null)
                _changesHeading.text = "\u00a7 CHANGED ON THIS TAB \u00b7 " + _changeLines.Count;
            if (_changesBody != null)
                _changesBody.text = body;
        }

        void OnReset()
        {
            UiSettingsPage page = _tabs.CurrentPage;
            if (page == null)
                return;
            page.ResetToDefaults();
            Link();
            _status.Set("Defaults restored on this tab and saved on this PC.", UiStatusKind.Info);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_back.gameObject);
        }

        void OnBack() => Stack?.Pop(false);

        void OnFocused(Selectable control, string description)
        {
            if (_help != null && control != null && control.transform.IsChildOf(transform))
                _help.text = description;
        }

        void OnRefused(Selectable control, string reason)
        {
            if (control != null && control.transform.IsChildOf(transform) && !string.IsNullOrEmpty(reason))
                _status.Set(reason, UiStatusKind.Refusal);
        }
    }
}
