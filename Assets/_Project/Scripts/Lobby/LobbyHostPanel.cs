using System;
using CloseTheDeal.UI;
using TMPro;
using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// The host's player list in reception, built from the UI kit: one row per other player with
    /// their firm, and Remove behind a confirm dialog. The host opens it with Tab (pad: View) via
    /// LobbyHostControls. Removing asks the roster, which checks again that the asker is the
    /// host. Built by Close the Deal > Lanes > Set Up Lobby.
    /// </summary>
    public sealed class LobbyHostPanel : UiScreen
    {
        [Header("Wiring")]
        [SerializeField] UiButton[] _rows = new UiButton[0];
        [SerializeField] TMP_Text[] _rowLabels = new TMP_Text[0];
        [SerializeField] TMP_Text _empty;
        [SerializeField] UiButton _close;
        [SerializeField] UiConfirmDialog _confirm;
        [SerializeField] UiScreenStack _stack;

        /// <summary>The panel closed (by Close, Back or Tab).</summary>
        public event Action Closed;

        readonly int[] _rowClient = new int[8];
        LobbyRoster _roster;
        bool _open;

        public bool IsOpen => _open;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < _rows.Length; i++)
            {
                int row = i;
                _rows[i].Clicked += () => AskRemove(row);
            }

            _close.Clicked += Close;
            gameObject.SetActive(false);
        }

        public void Open(LobbyRoster roster)
        {
            _roster = roster;
            _roster.OnChanged += Fill;
            _open = true;
            Fill();
            _stack.Push(this);
        }

        public void Close()
        {
            if (!_open)
                return;
            _open = false;
            if (_roster != null)
                _roster.OnChanged -= Fill;
            while (_stack.Count > 0)
                _stack.Pop();
            Closed?.Invoke();
        }

        public override void OnCancel() => Close();

        /// <summary>One row per player other than the host, in roster order. Rebuilt on roster changes only.</summary>
        void Fill()
        {
            if (_roster == null || !_roster.TryGetLocal(out LobbyMember me))
                return;

            int row = 0;
            for (int i = 0; i < _roster.Count && row < _rows.Length; i++)
            {
                LobbyMember member = _roster[i];
                if (member.ClientId == me.ClientId)
                    continue;
                _rowClient[row] = member.ClientId;
                _rowLabels[row].text = member.Name + "   ·   " + _roster.Rules.FirmName(member.Firm) + "   ·   Remove";
                _rows[row].gameObject.SetActive(true);
                row++;
            }

            for (int i = row; i < _rows.Length; i++)
                _rows[i].gameObject.SetActive(false);
            _empty.gameObject.SetActive(row == 0);
            LinkNavigation(row);
            if (row == 0 && _open)
                Refocus();
        }

        /// <summary>Up and down run through the rows in use and wrap via Close, so focus never lands on a hidden row.</summary>
        void LinkNavigation(int used)
        {
            for (int i = 0; i < used; i++)
            {
                var nav = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit };
                nav.selectOnUp = i == 0 ? (UnityEngine.UI.Selectable)_close : _rows[i - 1];
                nav.selectOnDown = i == used - 1 ? (UnityEngine.UI.Selectable)_close : _rows[i + 1];
                _rows[i].navigation = nav;
            }

            var closeNav = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit };
            closeNav.selectOnUp = used > 0 ? _rows[used - 1] : null;
            closeNav.selectOnDown = used > 0 ? _rows[0] : null;
            _close.navigation = closeNav;
            _firstFocus = used > 0 ? _rows[0] : _close;
        }

        void AskRemove(int row)
        {
            int clientId = _rowClient[row];
            int index = _roster.IndexOf(clientId);
            if (index < 0)
                return;

            string name = _roster[index].Name;
            _confirm.Configure("Remove " + name + "?",
                name + " leaves the lobby at once. They can join again from your Steam profile or an invite.",
                "Remove", "Keep them", () =>
                {
                    _roster.RequestKick(clientId);
                    _stack.Pop(false); // The dialog only reports the confirm; it is ours to close.
                });
            _stack.Push(_confirm);
        }
    }
}
