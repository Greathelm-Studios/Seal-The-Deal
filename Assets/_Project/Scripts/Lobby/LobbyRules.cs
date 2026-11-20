using CloseTheDeal.MatchMode;
using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// The reception's rules: which firm definitions the two firms are, how lopsided they may get, and when the host can
    /// start. Designer content (AGENTS.md §4): read at runtime, never written.
    /// Every rule here is a proposal waiting on the owner; see docs/systems/LOBBY.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Lobby/Lobby Rules", fileName = "LobbyRules")]
    public sealed class LobbyRules : ScriptableObject
    {
        [Header("Firms")]
        [Tooltip("The two firms, as the same firm definitions the match reads, so a firm's name and colour live in one place. Order does not matter: each firm is matched to its building by its own Tower setting (0 = Building A, 1 = Building B). Left empty, the board reads 'Firm A' and 'Firm B' in grey.")]
        [SerializeField] FirmDefinition[] _firms = new FirmDefinition[0];

        [Header("Balance")]
        [Tooltip("The most one firm may outnumber the other, in players. 1 keeps it fair (2 v 1 is fine, 3 v 1 is not). New arrivals join the smaller firm; switching that would break this is refused.")]
        [Range(0, 7)] [SerializeField] int _maxImbalance = 1;

        [Tooltip("Tick to need at least one player in each firm before the run can start. Untick only for solo testing: a one-firm run has nobody across the gap.")]
        [SerializeField] bool _requireBothFirms = true;

        [Header("Starting short-handed (proposal for the owner)")]
        [Tooltip("Tick to let the host start once everyone is ready even when a firm is empty or the firms are uneven, after a second hold at the desk to confirm. Untick and the run waits until both firms have players within the limit above. Ticked is what lets one person start a test run alone.")]
        [SerializeField] bool _allowShortHandedStart = true;

        [Tooltip("How long the host has to confirm a short-handed start with a second hold, in seconds, before the confirm is dropped and the first hold has to be repeated.")]
        [Range(2f, 15f)] [SerializeField] float _confirmSeconds = 6f;

        [Header("Start")]
        [Tooltip("How long the host holds the button at the desk to start the run, in seconds. Holding stops an accidental start; 1 s feels deliberate, above 2 s feels sluggish.")]
        [Range(0.2f, 3f)] [SerializeField] float _startHoldSeconds = 1f;

        [Header("Reach")]
        [Tooltip("How close a player must be to a desk, stand or mirror to use it, in metres from their eyes.")]
        [Range(0.5f, 5f)] [SerializeField] float _interactRange = 2.6f;

        [Tooltip("How directly the player must look at it: 0 = anywhere in front, 1 = dead centre. 0.8 is a comfortable glance.")]
        [Range(0f, 0.99f)] [SerializeField] float _interactAim = 0.8f;

        public int FirmCount => 2;

        /// <summary>The firm that climbs this building (0 = A, 1 = B), or null when none is set.</summary>
        public FirmDefinition Firm(int building)
        {
            for (int i = 0; i < _firms.Length; i++)
            {
                if (_firms[i] != null && _firms[i].Tower == building)
                    return _firms[i];
            }

            return null;
        }

        public string FirmName(int firm)
        {
            FirmDefinition definition = Firm(firm);
            if (definition != null && !string.IsNullOrEmpty(definition.DisplayName))
                return definition.DisplayName;
            return firm == 0 ? "Firm A" : "Firm B";
        }

        public Color FirmColour(int firm)
        {
            FirmDefinition definition = Firm(firm);
            return definition != null ? definition.MarkerColour : new Color(0.5f, 0.5f, 0.55f, 1f);
        }
        public int MaxImbalance => _maxImbalance;
        public bool RequireBothFirms => _requireBothFirms;
        public bool AllowShortHandedStart => _allowShortHandedStart;
        public float ConfirmSeconds => _confirmSeconds;
        public float StartHoldSeconds => _startHoldSeconds;
        public float InteractRange => _interactRange;
        public float InteractAim => _interactAim;
    }
}
