using System;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// One tab of the settings screen: its rows read the saved values when it opens, write them on change, and can put
    /// them back to defaults. The settings screen owns the tab bar, the footer and the right-hand detail column.
    /// </summary>
    public abstract class UiSettingsPage : MonoBehaviour
    {
        [Tooltip("The kit's tokens.")]
        [SerializeField] protected UiTokens _tokens;
        [Tooltip("The tab's name, as the tab bar and the breadcrumb show it.")]
        [SerializeField] string _title;
        [Tooltip("The rows top to bottom, in focus order. Rows that are switched off are skipped.")]
        [SerializeField] protected Selectable[] _rows = Array.Empty<Selectable>();

        /// <summary>Raised after this page saved a change, with a line for the status bar.</summary>
        public event Action<string> Saved;

        public string Title => _title;

        /// <summary>The first row that is showing and usable: where focus lands when the tab opens.</summary>
        public Selectable FirstRow
        {
            get
            {
                foreach (Selectable row in _rows)
                {
                    if (row != null && row.gameObject.activeInHierarchy && row.IsInteractable())
                        return row;
                }

                return null;
            }
        }

        /// <summary>The last showing row, for the footer's up link.</summary>
        public Selectable LastRow
        {
            get
            {
                for (int i = _rows.Length - 1; i >= 0; i--)
                {
                    if (_rows[i] != null && _rows[i].gameObject.activeInHierarchy && _rows[i].IsInteractable())
                        return _rows[i];
                }

                return null;
            }
        }

        /// <summary>Reads every saved value into the rows, without sounds or events.</summary>
        public abstract void ShowSaved();

        /// <summary>True when every value on this tab is at its default (Reset is greyed out then).</summary>
        public abstract bool AtDefaults { get; }

        /// <summary>Puts this tab's values back to how the game shipped.</summary>
        public abstract void ResetToDefaults();

        /// <summary>Called when the page stops showing (tab change or the screen closing).</summary>
        public virtual void Hidden() { }

        /// <summary>Called every frame while the page shows. Only pages with something live (a meter, a countdown) use it.</summary>
        public virtual void Tick(float dt) { }

        protected void RaiseSaved(string line = "Saved on this PC.") => Saved?.Invoke(line);

        /// <summary>Links the showing rows top to bottom; above is the open tab, below is the footer.</summary>
        public void LinkRows(Selectable above, Selectable below)
        {
            Selectable previous = above;
            Selectable last = null;
            foreach (Selectable row in _rows)
            {
                if (row == null || !row.gameObject.activeInHierarchy)
                    continue;
                Navigation nav = row.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = previous;
                nav.selectOnDown = below;
                row.navigation = nav;
                if (last != null)
                {
                    Navigation prev = last.navigation;
                    prev.selectOnDown = row;
                    last.navigation = prev;
                }

                previous = row;
                last = row;
            }
        }
    }
}
