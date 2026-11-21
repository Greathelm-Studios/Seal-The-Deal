using CloseTheDeal.Lobby;
using CloseTheDeal.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>In-game menu glass pane: navigation, session summary, match slot, and read-only reception seats.</summary>
    [AddComponentMenu("Close the Deal/UI/In-Game Menu")]
    public sealed class InGameMenuScreen : UiScreen
    {
        [Header("Menu")]
        [SerializeField] UiButton _back;
        [SerializeField] UiButton _invite;
        [SerializeField] UiButton _settings;
        [SerializeField] UiButton _leave;

        [Header("Text")]
        [Tooltip("Heading of the focused button's explanation.")]
        [SerializeField] TMP_Text _focusHeading;
        [Tooltip("The focused button's explanation.")]
        [SerializeField] TMP_Text _focusBody;
        [Tooltip("Who is hosting and how many players are in; a refused press says why here.")]
        [SerializeField] UiStatusLine _session;

        [Header("Match")]
        [Tooltip("Where the match lane's section sits (moved here by Set Up UI). Its button joins this menu's navigation.")]
        [SerializeField] RectTransform _matchSlot;

        [Header("Reception")]
        [Tooltip("The seats block: shown while the players are in reception and the run has not started.")]
        [SerializeField] GameObject _reception;
        [Tooltip("The seats block's contents, moved to sit just under the match section (whatever its height).")]
        [SerializeField] RectTransform _receptionBlock;
        [Tooltip("Gap between the match section and the seats, in pixels at 1920 x 1080.")]
        [Min(0f)] [SerializeField] float _receptionGap = 24f;
        [Tooltip("How many are ready, beside the block's caption.")]
        [SerializeField] TMP_Text _readyCount;
        [Tooltip("Each firm's name, first firm then second.")]
        [SerializeField] TMP_Text[] _firmNames = new TMP_Text[0];
        [Tooltip("Each firm's colour mark beside its name.")]
        [SerializeField] Graphic[] _firmMarks = new Graphic[0];
        [Tooltip("Seat names: the first firm's seats top to bottom, then the second firm's.")]
        [SerializeField] TMP_Text[] _seatNames = new TMP_Text[0];
        [Tooltip("Seat states (Ready, Not ready, Fitting room), in the same order as the names.")]
        [SerializeField] TMP_Text[] _seatStates = new TMP_Text[0];
        [Tooltip("Seat marks: a filled cyan square when ready, in the same order.")]
        [SerializeField] Graphic[] _seatMarks = new Graphic[0];
        [Tooltip("Seat keylines: an empty square while not ready, in the same order.")]
        [SerializeField] Graphic[] _seatRings = new Graphic[0];

        [Header("Screens it opens")]
        [SerializeField] UiScreen _settingsScreen;
        [SerializeField] UiConfirmDialog _confirm;

        const string InviteRefusal = "Steam's overlay is off, so the invite list can't open here. Friends can still join from your name in Steam.";

        GameMenu _menu;
        SteamLobby _lobby;
        LobbyRoster _roster;
        Transform _matchChild;
        Selectable _matchAction;
        bool _matchShown;
        float _matchHeight;
        bool _matchActionUp;
        bool _refusalShown;
        bool _dirty = true;
        string _sessionText = string.Empty;
        UiButton[] _menuButtons;

        internal void Bind(GameMenu menu)
        {
            _menu = menu;
            _lobby = menu != null ? menu.Lobby : null;
        }

        protected override void Awake()
        {
            base.Awake();
            _firstFocus = _back;
            _menuButtons = new[] { _back, _invite, _settings, _leave };
            if (_back != null)
                _back.ConfirmSound = UiSoundEvent.Back;
        }

        void OnEnable()
        {
            if (_lobby != null)
                _lobby.OnChanged += MarkDirty;
            _back.Clicked += OnBack;
            _invite.Clicked += OnInvite;
            _settings.Clicked += OnSettings;
            _leave.Clicked += OnLeave;
            UiControlEvents.Focused += OnFocused;
            UiControlEvents.Refused += OnRefused;
        }

        protected override void OnDisable()
        {
            if (_lobby != null)
                _lobby.OnChanged -= MarkDirty;
            _back.Clicked -= OnBack;
            _invite.Clicked -= OnInvite;
            _settings.Clicked -= OnSettings;
            _leave.Clicked -= OnLeave;
            UiControlEvents.Focused -= OnFocused;
            UiControlEvents.Refused -= OnRefused;
            WatchRoster(null);
            base.OnDisable();
        }

        protected override void OnShowing()
        {
            WatchRoster(LobbyRoster.Instance); // a scene object that spawns with the game: re-read each time the menu opens
            _refusalShown = false;
            Refresh();
        }

        void LateUpdate()
        {
            if (LobbyRoster.Instance != _roster)
            {
                WatchRoster(LobbyRoster.Instance); // spawned after the menu opened
                MarkDirty();
            }

            if (_dirty)
            {
                Refresh();
                return;
            }

            EnsureMatchAction();
            if (_matchChild == null)
                return;

            bool shown = _matchChild.gameObject.activeSelf;
            float height = shown ? ((RectTransform)_matchChild).rect.height : 0f;
            bool actionUp = _matchAction != null && _matchAction.isActiveAndEnabled;
            if (shown == _matchShown && Mathf.Approximately(height, _matchHeight) && actionUp == _matchActionUp)
                return;
            _matchShown = shown;
            _matchHeight = height;
            _matchActionUp = actionUp;
            PlaceReception();
            LinkNavigation();
        }

        void MarkDirty() => _dirty = true;

        /// <summary>Esc / B: resume gameplay.</summary>
        public override void OnCancel()
        {
            UiSound.Play(UiSoundEvent.Back);
            OnBack();
        }

        void WatchRoster(LobbyRoster roster)
        {
            if (roster == _roster)
                return;
            if (_roster != null)
                _roster.OnChanged -= MarkDirty;
            _roster = roster;
            if (_roster != null)
                _roster.OnChanged += MarkDirty;
        }

        void Refresh()
        {
            _dirty = false;
            if (_lobby == null || _tokens == null)
                return;
            WatchRoster(LobbyRoster.Instance);

            bool local = _lobby.LocalMode;
            bool host = _lobby.IsHosting;
            _invite.gameObject.SetActive(!local); // Steam only: in local test mode there is nobody to invite
            _invite.interactable = _lobby.CanInviteViaOverlay;
            _invite.RefusalReason = InviteRefusal;
            _leave.Description = host
                ? "End this game for everyone and go back to the title."
                : "Go back to the title. Your team carries on without you.";

            int count = _lobby.PlayerCount;
            string who = host ? "You are hosting" : "Hosted by " + _lobby.HostDisplayName;
            string players = count == 1 ? "1 player" : count + " players";
            _sessionText = who + ".  " + players + " of " + _lobby.MaxPlayers + (local ? ", on this PC." : ".");
            if (!_refusalShown && _session != null)
                _session.Set(_sessionText, UiStatusKind.Info);

            RefreshReception();
            LinkNavigation();
        }

        void RefreshReception()
        {
            bool show = _roster != null && !_roster.RunInProgress && _roster.Rules != null;
            if (_reception != null && _reception.activeSelf != show)
                _reception.SetActive(show);
            if (!show)
                return;

            PlaceReception();
            int ready = RefreshFirm(0) + RefreshFirm(1);
            if (_readyCount != null)
                _readyCount.text = ready + " of " + _roster.Count + " ready";
        }

        void PlaceReception()
        {
            if (_receptionBlock == null || _matchSlot == null)
                return;
            const float builtFor = 196f;
            float match = 0f;
            for (int i = 0; i < _matchSlot.childCount; i++)
            {
                if (_matchSlot.GetChild(i).gameObject.activeSelf)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_matchSlot);
                    match = LayoutUtility.GetPreferredHeight(_matchSlot);
                    break;
                }
            }

            float shift = match > 0f ? match - builtFor : -(builtFor + _receptionGap);
            _receptionBlock.anchoredPosition = new Vector2(0f, -shift);
        }

        int RefreshFirm(int firm)
        {
            if (firm < _firmNames.Length && _firmNames[firm] != null)
                _firmNames[firm].text = _roster.Rules.FirmName(firm) + "   " + _roster.FirmSize(firm);
            if (firm < _firmMarks.Length && _firmMarks[firm] != null)
                _firmMarks[firm].color = _roster.Rules.FirmColour(firm);

            int perFirm = _seatNames.Length / 2;
            int seat = 0, ready = 0;
            for (int i = 0; i < _roster.Count && seat < perFirm; i++)
            {
                LobbyMember member = _roster[i];
                if (member.Firm != firm)
                    continue;
                if (member.State == MemberState.Ready)
                    ready++;
                SetSeat(firm * perFirm + seat, member);
                seat++;
            }

            for (; seat < perFirm; seat++)
                SetEmpty(firm * perFirm + seat);
            return ready;
        }

        void SetSeat(int index, LobbyMember member)
        {
            bool me = _roster.TryGetLocal(out LobbyMember local) && local.ClientId == member.ClientId;
            _seatNames[index].text = me ? member.Name + " (you)" : member.Name;
            _seatNames[index].color = _tokens.Navy;
            bool ready = member.State == MemberState.Ready;
            _seatStates[index].text = ready ? "Ready" : member.State == MemberState.Fitting ? "Fitting room" : "Not ready";
            _seatStates[index].color = ready ? _tokens.Navy : _tokens.Ink2;
            _seatMarks[index].enabled = ready;
            _seatRings[index].enabled = !ready;
        }

        void SetEmpty(int index)
        {
            _seatNames[index].text = "Seat available";
            _seatNames[index].color = _tokens.Ink3;
            _seatStates[index].text = string.Empty;
            _seatMarks[index].enabled = false;
            _seatRings[index].enabled = false;
        }

        void LinkNavigation()
        {
            Selectable right = MatchSelectable();
            Selectable previous = null;
            for (int i = 0; i < _menuButtons.Length; i++)
            {
                UiButton current = _menuButtons[i];
                if (current == null || !current.gameObject.activeSelf)
                    continue;
                var nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = previous, selectOnRight = right };
                current.navigation = nav;
                if (previous != null)
                {
                    Navigation prevNav = previous.navigation;
                    prevNav.selectOnDown = current;
                    previous.navigation = prevNav;
                }

                previous = current;
            }
        }

        void EnsureMatchAction()
        {
            if (_matchSlot == null || _matchSlot.childCount == 0)
            {
                _matchChild = null;
                _matchAction = null;
                return;
            }

            Transform child = _matchSlot.GetChild(0);
            if (child != _matchChild)
            {
                _matchChild = child;
                _matchAction = child.GetComponentInChildren<Selectable>(true); // once per section, not per frame
            }
        }

        Selectable MatchSelectable()
        {
            EnsureMatchAction();
            return _matchAction != null && _matchAction.isActiveAndEnabled ? _matchAction : null;
        }

        void OnFocused(Selectable control, string description)
        {
            if (control == null || !control.transform.IsChildOf(transform))
                return;
            if (_refusalShown)
            {
                _refusalShown = false; // moving on clears a refusal; the session line comes back
                _session?.Set(_sessionText, UiStatusKind.Info);
            }

            if (_focusHeading != null && control is UiButton button && button.Fx.Label != null)
                _focusHeading.text = "§ " + button.Fx.Label.text.ToUpperInvariant();
            if (_focusBody != null)
                _focusBody.text = description;
        }

        void OnRefused(Selectable control, string reason)
        {
            if (control == null || !control.transform.IsChildOf(transform) || string.IsNullOrEmpty(reason) || _session == null)
                return;
            _refusalShown = true;
            _session.Set(reason, UiStatusKind.Refusal);
        }

        void OnBack()
        {
            if (_menu != null)
                _menu.Resume();
        }

        void OnInvite() => _lobby?.Invite();

        void OnSettings()
        {
            if (Stack != null && _settingsScreen != null)
                Stack.Push(_settingsScreen);
        }

        void OnLeave()
        {
            if (Stack == null || _confirm == null || Stack.Top != this || _lobby == null)
                return;

            int count = _lobby.PlayerCount;
            if (!_lobby.IsHosting)
                _confirm.Configure("Leave " + _lobby.HostDisplayName + "'s game?", "Your team carries on without you.", "Leave game", "Stay", Leave);
            else if (count > 1)
                _confirm.Configure("End the game for everyone?", "You are the host. Leaving sends all " + count + " players back to the title.",
                    "End game for everyone", "Stay", Leave);
            else
                _confirm.Configure("Close your game?", "You are the only player here. Leaving closes the game.", "Leave game", "Stay", Leave);
            Stack.Push(_confirm);
        }

        void Leave() => _lobby?.Leave();
    }
}
