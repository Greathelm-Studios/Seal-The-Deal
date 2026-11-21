using System.Text;
using CloseTheDeal.Player;
using CloseTheDeal.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.Lobby
{
    /// <summary>Reception overlay showing the ready board, interactable prompt, and secretary subtitles. Design: docs/systems/LOBBY.md.</summary>
    public sealed class LobbyHud : MonoBehaviour
    {
        [Header("Board")]
        [SerializeField] CanvasGroup _board;
        [SerializeField] TMP_Text[] _firmNames = new TMP_Text[2];
        [SerializeField] TMP_Text[] _firmLists = new TMP_Text[2];
        [SerializeField] Image[] _firmMarks = new Image[2];
        [SerializeField] TMP_Text _status;

        [Header("Prompt")]
        [SerializeField] CanvasGroup _prompt;
        [SerializeField] TMP_Text _promptKey;
        [SerializeField] TMP_Text _promptText;
        [SerializeField] Image _holdFill;

        [Header("Subtitle")]
        [SerializeField] CanvasGroup _subtitle;
        [SerializeField] TMP_Text _subtitleText;

        [Header("Kit")]
        [Tooltip("The UI kit's tokens: fade timings and colours come from here.")]
        [SerializeField] UiTokens _tokens;

        [Header("Look")]
        [Tooltip("Colour of the 'Ready' mark on the board.")]
        [SerializeField] Color _readyColour = new(0.141f, 0.310f, 0.725f, 1f);

        [Tooltip("Colour of secondary board text.")]
        [SerializeField] Color _mutedColour = new(0.290f, 0.329f, 0.408f, 1f);


        LobbyRoster _roster;
        LobbyInteractor _interactor;
        readonly StringBuilder _builder = new(256);
        readonly StringBuilder _names = new(96);
        string _shownPrompt;
        string _interactKey;
        float _shownFill = -2f;
        float _subtitleUntil = -1f;
        bool _boardDirty = true;
        string _readyHex;
        string _mutedHex;

        const string RebuildHint = "Run Seal the Deal > Lanes > Set Up Lobby (or Set Up Integration) to rebuild it.";

        void Awake()
        {
            // A scene built before the UI kit revamp lacks these. One clear error and stand down:
            // never throw per frame, never get in the way of a player spawning.
            if (_tokens == null || _board == null || _prompt == null || _subtitle == null || _promptKey == null
                || _promptText == null || _holdFill == null || _status == null || _subtitleText == null)
            {
                Debug.LogError($"[Lobby] {name}: the reception HUD is from an older build and is missing its UI kit wiring, so the ready board, prompt and subtitles are off. {RebuildHint}", this);
                enabled = false;
                return;
            }

            _readyHex = ColorUtility.ToHtmlStringRGB(_readyColour);
            _mutedHex = ColorUtility.ToHtmlStringRGB(_mutedColour);
            _board.alpha = 0f;
            _prompt.alpha = 0f;
            _subtitle.alpha = 0f;
        }

        void OnEnable()
        {
            Secretary.LineSpoken += OnLine;
            UiGlyphs.Changed += RefreshInput;
            RefreshInput();
        }

        void OnDisable()
        {
            Secretary.LineSpoken -= OnLine;
            UiGlyphs.Changed -= RefreshInput;
        }

        void RefreshInput()
        {
            _interactKey = LobbyInput.Interact == null ? null : UiGlyphs.For(LobbyInput.Interact, UiGlyphs.Current);
            _shownPrompt = null;
            _boardDirty = true;
        }

        void Update()
        {
            if (_interactKey == null && LobbyInput.Interact != null)
                RefreshInput();
            Hook();
            bool inReception = _roster != null && PlayerMotor.Local != null && LobbyInput.Captured
                && (_interactor == null || !_interactor.Blocked);

            if (_boardDirty && _roster != null)
                RebuildBoard();

            Fade(_board, inReception);
            UpdatePrompt(inReception);
            Fade(_subtitle, Time.unscaledTime < _subtitleUntil && PlayerMotor.Local != null);
        }

        void Hook()
        {
            if (_interactor == null)
                _interactor = LobbyInteractor.Instance;

            LobbyRoster roster = LobbyRoster.Instance;
            if (roster == _roster)
                return;
            if (_roster != null)
                _roster.OnChanged -= MarkDirty;
            _roster = roster;
            if (_roster != null)
                _roster.OnChanged += MarkDirty;
            _boardDirty = true;
        }

        void OnDestroy()
        {
            if (_roster != null)
                _roster.OnChanged -= MarkDirty;
        }

        void MarkDirty() => _boardDirty = true;

        void RebuildBoard()
        {
            _boardDirty = false;
            LobbyRules rules = _roster.Rules;
            bool haveMe = _roster.TryGetLocal(out LobbyMember me);

            for (int firm = 0; firm < 2; firm++)
            {
                _firmNames[firm].text = rules.FirmName(firm) + "   " + _roster.FirmSize(firm);
                _firmMarks[firm].color = rules.FirmColour(firm);

                _builder.Clear();
                for (int i = 0; i < _roster.Count; i++)
                {
                    LobbyMember member = _roster[i];
                    if (member.Firm != firm)
                        continue;
                    AppendRow(member, haveMe && member.ClientId == me.ClientId);
                }

                if (_builder.Length == 0)
                    _builder.Append("<color=#").Append(_mutedHex).Append(">Nobody yet</color>");
                _firmLists[firm].text = _builder.ToString();
            }

            _status.text = StatusLine(rules, haveMe, me);
        }

        void AppendRow(LobbyMember member, bool isMe)
        {
            if (_builder.Length > 0)
                _builder.Append('\n');
            if (isMe)
                _builder.Append("<b>");
            _builder.Append(member.Name);
            if (isMe)
                _builder.Append(" (you)</b>");

            switch (member.State)
            {
                case MemberState.Ready:
                    _builder.Append("   <color=#").Append(_readyHex).Append("><b>Ready</b></color>");
                    break;
                case MemberState.Fitting:
                    _builder.Append("   <color=#").Append(_mutedHex).Append(">In the fitting room</color>");
                    break;
            }
        }

        string StatusLine(LobbyRules rules, bool haveMe, LobbyMember me)
        {
            if (!haveMe)
                return "Joining reception.";

            LobbyStep step = _roster.StepFor(me, _roster.IsLocalHost);
            switch (step)
            {
                case LobbyStep.Fitting:
                    return "You are in the fitting room. Keep the outfit or put it back to continue.";
                case LobbyStep.ReadyUp:
                    return "You are with " + rules.FirmName(me.Firm) + ". Ready up at the reception desk. To change firm, use a sign-in stand.";
                case LobbyStep.WaitForOthers:
                    return "Ready. Waiting for " + NotReadyNames() + ".";
                case LobbyStep.StartRun:
                    return "Everyone is ready. Hold " + _interactKey + " at the desk to start the run.";
                case LobbyStep.StartShortHanded:
                    return ShortHandedLine(rules);
                case LobbyStep.WaitForHost:
                    return "Everyone is ready. Waiting for the host to start the run.";
                default:
                    return "The run has started.";
            }
        }

        string ShortHandedLine(LobbyRules rules)
        {
            StartBlock problem = _roster.FirmProblem();
            string why = problem == StartBlock.NeedBothFirms
                ? rules.FirmName(_roster.SmallerFirm()) + " has nobody"
                : "The firms are uneven";
            return "Everyone is ready. " + why + ". Hold " + _interactKey + " at the desk twice to start anyway.";
        }

        string NotReadyNames()
        {
            _names.Clear();
            for (int i = 0; i < _roster.Count; i++)
            {
                if (_roster[i].State != MemberState.Ready)
                {
                    if (_names.Length > 0)
                        _names.Append(", ");
                    _names.Append(_roster[i].Name);
                }
            }

            return _names.Length > 0 ? _names.ToString() : "the others";
        }

        void UpdatePrompt(bool inReception)
        {
            LobbyInteractable target = inReception && _interactor != null ? _interactor.Target : null;
            string line = target != null ? target.Prompt(UiGlyphs.OnPad) : null;
            Fade(_prompt, line != null);

            if (line != null && !ReferenceEquals(line, _shownPrompt))
            {
                _shownPrompt = line;
                int gap = line.IndexOf("   ", System.StringComparison.Ordinal);
                _promptKey.text = gap > 0 ? _interactKey : string.Empty;
                _promptText.text = gap > 0 ? line.Substring(gap + 3) : line;
                _promptKey.transform.parent.gameObject.SetActive(gap > 0);
            }

            float fill = target != null ? target.HoldProgress : -1f;
            if (!Mathf.Approximately(fill, _shownFill))
            {
                _shownFill = fill;
                _holdFill.enabled = fill >= 0f;
                _holdFill.fillAmount = Mathf.Max(0f, fill);
            }
        }

        void OnLine(SecretaryMoment moment, string text, float seconds)
        {
            _subtitleText.text = text;
            _subtitleUntil = Time.unscaledTime + seconds;
        }

        void Fade(CanvasGroup group, bool show)
        {
            float target = show ? 1f : 0f;
            if (Mathf.Approximately(group.alpha, target))
                return;
            // The kit's panel timings: in a little slower than out. Reduce motion makes both the short fade.
            float seconds = UiMotion.Reduced ? _tokens.ReducedFade : show ? _tokens.PanelIn : _tokens.PanelOut;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
        }
    }
}
