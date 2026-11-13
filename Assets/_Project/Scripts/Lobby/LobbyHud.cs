using System.Text;
using CloseTheDeal.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// The reception overlay: the ready board (who is in which firm and who is ready), the
    /// prompt for whatever the player is looking at (with a fill while the host holds to
    /// start) and the secretary's subtitles. The board text is rebuilt only when the roster
    /// changes; the prompt only when its line changes. Built by Close the Deal > Lanes > Set Up
    /// Lobby; restyle it there. Hidden while the fitting room or the game menu is up.
    /// </summary>
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
        [SerializeField] TMP_Text _promptText;
        [SerializeField] Image _holdFill;

        [Header("Subtitle")]
        [SerializeField] CanvasGroup _subtitle;
        [SerializeField] TMP_Text _subtitleText;

        [Header("Look")]
        [Tooltip("Colour of the 'Ready' mark on the board.")]
        [SerializeField] Color _readyColour = new(0.141f, 0.310f, 0.725f, 1f);

        [Tooltip("Colour of secondary board text.")]
        [SerializeField] Color _mutedColour = new(0.290f, 0.329f, 0.408f, 1f);

        [Header("Motion")]
        [Tooltip("Seconds for the board, prompt and subtitle to fade in or out.")]
        [Range(0.05f, 0.5f)] [SerializeField] float _fadeSeconds = 0.15f;

        LobbyRoster _roster;
        LobbyInteractor _interactor;
        readonly StringBuilder _builder = new(256);
        string _shownPrompt;
        float _shownFill = -2f;
        float _subtitleUntil = -1f;
        bool _boardDirty = true;
        string _readyHex;
        string _mutedHex;

        void Awake()
        {
            _readyHex = ColorUtility.ToHtmlStringRGB(_readyColour);
            _mutedHex = ColorUtility.ToHtmlStringRGB(_mutedColour);
            _board.alpha = 0f;
            _prompt.alpha = 0f;
            _subtitle.alpha = 0f;
        }

        void OnEnable() => Secretary.LineSpoken += OnLine;
        void OnDisable() => Secretary.LineSpoken -= OnLine;

        void Update()
        {
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

        // ---- Board ---------------------------------------------------------------------------

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

            _status.text = StatusLine(rules);
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

        string StatusLine(LobbyRules rules)
        {
            switch (_roster.WhyNotStart())
            {
                case StartBlock.RunInProgress:
                    return "The run has started.";
                case StartBlock.NeedBothFirms:
                    return "Each firm needs at least one person. Sign in at a firm's stand.";
                case StartBlock.Unbalanced:
                    int smaller = _roster.FirmSize(0) < _roster.FirmSize(1) ? 0 : 1;
                    return "The firms are uneven. Someone should sign in with " + rules.FirmName(smaller) + ".";
                case StartBlock.NotAllReady:
                    return _roster.ReadyCount + " of " + _roster.Count + " ready. Ready up at the reception desk.";
                default:
                    return _roster.IsLocalHost
                        ? "Everyone is ready. Hold F at the desk to start the run."
                        : "Everyone is ready. Waiting for the host to start the run.";
            }
        }

        // ---- Prompt --------------------------------------------------------------------------

        void UpdatePrompt(bool inReception)
        {
            LobbyInteractable target = inReception && _interactor != null ? _interactor.Target : null;
            string line = target != null ? target.Prompt(LobbyInput.UsingGamepad) : null;
            Fade(_prompt, line != null);

            if (line != null && !ReferenceEquals(line, _shownPrompt))
            {
                _shownPrompt = line;
                _promptText.text = line;
            }

            float fill = target != null ? target.HoldProgress : -1f;
            if (!Mathf.Approximately(fill, _shownFill))
            {
                _shownFill = fill;
                _holdFill.enabled = fill >= 0f;
                _holdFill.fillAmount = Mathf.Max(0f, fill);
            }
        }

        // ---- Subtitles -----------------------------------------------------------------------

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
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / _fadeSeconds);
        }
    }
}
