using System;
using CloseTheDeal.Net;
using TMPro;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// What went wrong with hosting, joining or a game, in the title's middle and right columns: an amber plate with
    /// the problem in plain words and how to fix it, the button that fixes it (Host again, Try again, Open Steam
    /// friends, Quit to desktop) and Back (Esc / B), which forgets the problem and returns to the title. One panel for
    /// every SteamLobby problem; the words follow ui_inventory §5.3. It refuses a fix that can't run now (Steam's
    /// overlay missing, the network not ready) with its reason, never a dead button.
    /// </summary>
    [AddComponentMenu("Close the Deal/UI/Problem Panel")]
    public sealed class ProblemPanel : UiScreen
    {
        /// <summary>Opens and closes under lane 8's CloudWipe (docs/systems/UI_KIT.md "Screens").</summary>
        public override CloseTheDeal.UI.Title.UiTransitionKind? Through => CloseTheDeal.UI.Title.UiTransitionKind.CloudWipe;

        [Header("Parts")]
        [Tooltip("The section caption, e.g. § 1  JOIN GAME.")]
        [SerializeField] TMP_Text _caption;
        [Tooltip("The problem in one line, on the amber plate.")]
        [SerializeField] TMP_Text _headline;
        [Tooltip("How to put it right, under the headline.")]
        [SerializeField] TMP_Text _body;
        [Tooltip("The button that fixes it (hidden when nothing in the game can).")]
        [SerializeField] UiButton _fix;
        [Tooltip("Forgets the problem and goes back to the title.")]
        [SerializeField] UiButton _back;

        enum Fix { None, HostAgain, TryAgain, OpenFriends, Quit }

        SteamLobby _lobby;
        TitleScreen _title;
        Fix _fixAction;
        LobbyProblem _shown;

        /// <summary>The problem the panel is showing.</summary>
        public LobbyProblem Shown => _shown;

        /// <summary>Raised when the player dismisses a problem the lobby keeps (a broken network), so the root doesn't reopen it.</summary>
        internal event Action Dismissed;

        /// <summary>A problem arrives with the Error sound, not the usual panel sound.</summary>
        public override UiSoundEvent OpenSound => UiSoundEvent.Error;

        internal void Bind(SteamLobby lobby, TitleScreen title)
        {
            _lobby = lobby;
            _title = title;
        }

        protected override void Awake()
        {
            base.Awake();
            if (_back != null)
                _back.ConfirmSound = UiSoundEvent.Back;
        }

        void OnEnable()
        {
            if (_fix != null) _fix.Clicked += OnFix;
            if (_back != null) _back.Clicked += OnBack;
        }

        protected override void OnDisable()
        {
            if (_fix != null) _fix.Clicked -= OnFix;
            if (_back != null) _back.Clicked -= OnBack;
            base.OnDisable();
        }

        protected override void OnShowing()
        {
            if (_title != null)
                _title.MakeRoom(true);
            Show(_lobby != null ? _lobby.Problem : LobbyProblem.None);
        }

        protected override void OnHiding()
        {
            if (_title != null)
                _title.MakeRoom(false);
        }

        /// <summary>Esc / B: Back.</summary>
        public override void OnCancel()
        {
            UiSound.Play(UiSoundEvent.Back);
            OnBack();
        }

        /// <summary>Fills the panel for a problem. Public so the capture tool and the gallery can show every one.</summary>
        public void Show(LobbyProblem problem)
        {
            _shown = problem;
            string host = _lobby != null && !string.IsNullOrEmpty(_lobby.ProblemHost) ? _lobby.ProblemHost : "the host";
            string detail = _lobby != null ? _lobby.ProblemDetail : string.Empty;
            int max = _lobby != null ? _lobby.MaxPlayers : 8;
            Words words = WordsFor(problem, host, detail, max, _lobby != null && _lobby.LocalMode);
            words.Headline = Sentence(words.Headline);
            if (_caption != null) _caption.text = words.Caption;
            if (_headline != null) _headline.text = words.Headline;
            if (_body != null) _body.text = words.Body;
            _fixAction = words.Fix;
            if (_fix != null)
            {
                _fix.gameObject.SetActive(words.Fix != Fix.None);
                if (_fix.Fx.Label != null)
                    _fix.Fx.Label.text = words.FixLabel;
                _fix.Description = words.FixAbout;
                RefreshFixUse();
            }

            if (_back != null)
                _back.Description = "Back to the title. Nothing else changes.";
            _firstFocus = _fix != null && _fix.gameObject.activeSelf && _fix.interactable ? _fix : _back;
            LinkButtons();
            Refocus();
        }

        /// <summary>A fix that can't run right now stays visible, greyed out, and says why when pressed.</summary>
        void RefreshFixUse()
        {
            bool usable = true;
            string why = string.Empty;
            if (_lobby != null)
            {
                switch (_fixAction)
                {
                    case Fix.OpenFriends:
                        usable = _lobby.CanOpenFriends;
                        why = "Steam's overlay isn't available here. Open your friends list in Steam itself.";
                        break;
                    case Fix.TryAgain:
                        usable = _lobby.CanRetryJoin;
                        why = _lobby.LocalMode
                            ? "Return to the title and choose Join on this PC."
                            : "There's nothing to try again. Join a friend from Steam.";
                        break;
                    case Fix.HostAgain:
                        usable = _lobby.Ready && !_lobby.IsActive;
                        why = "Host game opens once the network is ready.";
                        break;
                }
            }

            _fix.interactable = usable;
            _fix.RefusalReason = why;
        }

        void LinkButtons()
        {
            bool fixShown = _fix != null && _fix.gameObject.activeSelf && _fix.interactable;
            if (_back != null)
                _back.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = fixShown ? _fix : null };
            if (fixShown)
                _fix.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnRight = _back };
        }

        void OnFix()
        {
            Fix fix = _fixAction;
            if (_lobby == null)
                return;
            switch (fix)
            {
                case Fix.HostAgain:
                    Close();
                    _lobby.Host();
                    break;
                case Fix.TryAgain:
                    Close();
                    _lobby.RetryJoin();
                    break;
                case Fix.OpenFriends:
                    _lobby.OpenFriends(); // the overlay opens over the game; the panel stays for when they come back
                    break;
                case Fix.Quit:
                    TitleScreen.QuitToDesktop();
                    break;
            }
        }

        void OnBack() => Close();

        /// <summary>True while the panel closes itself, so the root doesn't put it straight back.</summary>
        internal bool Closing { get; private set; }

        /// <summary>Closes (with the sound the button already played) and forgets the problem.</summary>
        void Close()
        {
            Closing = true;
            if (Stack != null && Stack.Top == this)
                Stack.Pop(false);
            if (_lobby != null && _lobby.Problem == LobbyProblem.NetworkBroken)
                Dismissed?.Invoke();
            else
                _lobby?.ClearProblem();
            Closing = false;
        }

        // ---- Words (ui_inventory §5.3: plain, and each names the fix) --------------------------------------

        struct Words
        {
            public string Caption, Headline, Body, FixLabel, FixAbout;
            public Fix Fix;
        }

        const string JoinCaption = "§ 1  JOIN GAME";
        const string HostCaption = "§ 1  HOST GAME";
        const string GameCaption = "§ 1  GAME";

        static Words WordsFor(LobbyProblem problem, string host, string detail, int max, bool local)
        {
            switch (problem)
            {
                case LobbyProblem.Unreachable when local:
                case LobbyProblem.ConnectionLost when local:
                    return W(JoinCaption, "Unable to reach the game on this PC.",
                        "Choose Host a game in a second copy of Seal the Deal on this PC, then try again here.",
                        Fix.TryAgain, "Try again", "Reconnect to the game on this PC.");
                case LobbyProblem.HostClosed when local:
                case LobbyProblem.HostDropped when local:
                    return W(GameCaption, "The host on this PC disconnected.",
                        "Host here, or go back and join once the other copy is hosting again.",
                        Fix.HostAgain, "Host a game", "Start a game on this PC.");
                case LobbyProblem.NetworkBroken:
                    return W(HostCaption, "Unable to start the network.", "Restart Seal the Deal. If it keeps happening, reinstall it from Steam.",
                        Fix.Quit, "Quit to desktop", "Close the game so it can start again cleanly.");
                case LobbyProblem.LobbyCreateFailed:
                    return W(HostCaption, "Steam could not open a lobby" + Code(detail) + ".", "Check that Steam is online, then host again.",
                        Fix.HostAgain, "Host again", "Ask Steam for a lobby again.");
                case LobbyProblem.GameClosed:
                    return W(JoinCaption, "Unable to join. That game has closed.", "Ask your friend to host again, or host your own game.",
                        Fix.HostAgain, "Host a game", "Open your own lobby; friends join from your name in Steam.");
                case LobbyProblem.FriendsOnly:
                    return W(JoinCaption, "Unable to join. The lobby is friends-only.", "Add " + host + " as a Steam friend first, then join again from their name.",
                        Fix.OpenFriends, "Open Steam friends", "Opens your Steam friends list over the game.");
                case LobbyProblem.Full:
                    return W(JoinCaption, "Unable to join. The game is full (" + max + " players).", "A seat opens when a player leaves. Try again in a moment, or host your own game.",
                        Fix.TryAgain, "Try again", "Ask for a seat in the same game again.");
                case LobbyProblem.Refused:
                    return W(JoinCaption, "Unable to join. Steam refused the request" + Code(detail) + ".", "This is usually brief. Try again.",
                        Fix.TryAgain, "Try again", "Ask to join the same game again.");
                case LobbyProblem.VersionMismatch:
                    return W(JoinCaption, "Unable to join. Game versions differ.",
                        "Update Seal the Deal through Steam and try again. Your version: " + Application.version
                        + ". Host version: " + (string.IsNullOrEmpty(detail) ? "unknown" : detail) + ".",
                        Fix.Quit, "Quit to desktop", "Close the game so Steam can update it.");
                case LobbyProblem.Unreachable:
                    return W(JoinCaption, "Unable to reach " + host + "'s game.", "They may have left, or the connection failed. Try joining again.",
                        Fix.TryAgain, "Try again", "Join the same game again.");
                case LobbyProblem.HostClosed:
                    return W(GameCaption, host + " closed the game. The run has ended.", "Host your own game, or join a friend from Steam.",
                        Fix.HostAgain, "Host a game", "Open your own lobby.");
                case LobbyProblem.HostDropped:
                    return W(GameCaption, "Lost " + host + "'s game. Their connection dropped.", "When they're back, join again from their name in Steam, or host your own game.",
                        Fix.HostAgain, "Host a game", "Open your own lobby.");
                case LobbyProblem.ConnectionLost:
                    return W(GameCaption, "Lost the connection to " + host + "'s game.", "Check your internet connection, then join again.",
                        Fix.TryAgain, "Try again", "Join the same game again.");
                default:
                    return W(GameCaption, string.Empty, string.Empty, Fix.None, string.Empty, string.Empty);
            }
        }

        /// <summary>A line that starts with the host's name ("the host closed...") starts with a capital.</summary>
        static string Sentence(string line) =>
            string.IsNullOrEmpty(line) || char.IsUpper(line[0]) ? line : char.ToUpperInvariant(line[0]) + line.Substring(1);

        static string Code(string detail) => string.IsNullOrEmpty(detail) ? string.Empty : " (" + detail + ")";

        static Words W(string caption, string headline, string body, Fix fix, string fixLabel, string fixAbout) =>
            new() { Caption = caption, Headline = headline, Body = body, Fix = fix, FixLabel = fixLabel, FixAbout = fixAbout };
    }
}
