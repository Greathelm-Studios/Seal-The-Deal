using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// The reception's rules: the two firms, how lopsided they may get, and when the host can
    /// start. Designer content (AGENTS.md §4): read at runtime, never written.
    /// Every rule here is a proposal waiting on the owner; see docs/systems/LOBBY.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Lobby/Lobby Rules", fileName = "LobbyRules")]
    public sealed class LobbyRules : ScriptableObject
    {
        [Header("Firms")]
        [Tooltip("What players read for the first firm, on the sign-in stand and the ready board. The firm names are not decided yet.")]
        [SerializeField] string _firmAName = "Cobalt";

        [Tooltip("What players read for the second firm.")]
        [SerializeField] string _firmBName = "Cyan";

        [Tooltip("The first firm's colour on the board and its sign-in stand.")]
        [SerializeField] Color _firmAColour = new(0.141f, 0.310f, 0.725f, 1f);

        [Tooltip("The second firm's colour.")]
        [SerializeField] Color _firmBColour = new(0.125f, 0.831f, 0.902f, 1f);

        [Header("Balance")]
        [Tooltip("The most one firm may outnumber the other, in players. 1 keeps it fair (2 v 1 is fine, 3 v 1 is not). New arrivals join the smaller firm; switching that would break this is refused.")]
        [Range(0, 7)] [SerializeField] int _maxImbalance = 1;

        [Tooltip("Tick to need at least one player in each firm before the run can start. Untick only for solo testing: a one-firm run has nobody across the gap.")]
        [SerializeField] bool _requireBothFirms = true;

        [Header("Start")]
        [Tooltip("How long the host holds the button at the desk to start the run, in seconds. Holding stops an accidental start; 1 s feels deliberate, above 2 s feels sluggish.")]
        [Range(0.2f, 3f)] [SerializeField] float _startHoldSeconds = 1f;

        [Header("Reach")]
        [Tooltip("How close a player must be to a desk, stand or mirror to use it, in metres from their eyes.")]
        [Range(0.5f, 5f)] [SerializeField] float _interactRange = 2.6f;

        [Tooltip("How directly the player must look at it: 0 = anywhere in front, 1 = dead centre. 0.8 is a comfortable glance.")]
        [Range(0f, 0.99f)] [SerializeField] float _interactAim = 0.8f;

        public int FirmCount => 2;
        public string FirmName(int firm) => firm == 0 ? _firmAName : _firmBName;
        public Color FirmColour(int firm) => firm == 0 ? _firmAColour : _firmBColour;
        public int MaxImbalance => _maxImbalance;
        public bool RequireBothFirms => _requireBothFirms;
        public float StartHoldSeconds => _startHoldSeconds;
        public float InteractRange => _interactRange;
        public float InteractAim => _interactAim;
    }
}
