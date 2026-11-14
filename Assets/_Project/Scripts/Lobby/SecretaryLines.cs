using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>When the secretary speaks. Audio can key clips off these.</summary>
    public enum SecretaryMoment : byte
    {
        Arrival,
        Ready,
        NotReady,
        FirmChosen,
        FirmRefused,
        StartBlocked,
        ShortHanded,
        Start
    }

    /// <summary>
    /// What the secretary says, one list per moment; a line is picked at random, never twice in
    /// a row. {name} becomes the player's name, {firm} their firm. Designer content: read at
    /// runtime, never written. The shipped lines are proposals for the owner.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Lobby/Secretary Lines", fileName = "SecretaryLines")]
    public sealed class SecretaryLines : ScriptableObject
    {
        [Tooltip("Someone walks into reception.")]
        [SerializeField] string[] _arrival = new string[0];

        [Tooltip("Someone readies up at the desk.")]
        [SerializeField] string[] _ready = new string[0];

        [Tooltip("Someone takes their ready back.")]
        [SerializeField] string[] _notReady = new string[0];

        [Tooltip("Someone signs in with a firm. Heard by that player only.")]
        [SerializeField] string[] _firmChosen = new string[0];

        [Tooltip("A firm switch is refused because it would make the firms unfair. Heard by that player only.")]
        [SerializeField] string[] _firmRefused = new string[0];

        [Tooltip("The host tries to start before the rule is met. Heard by the host only.")]
        [SerializeField] string[] _startBlocked = new string[0];

        [Tooltip("The host's first hold arms a short-handed start (a firm is empty or the firms are uneven). Heard by the host only.")]
        [SerializeField] string[] _shortHanded = new string[0];

        [Tooltip("The run starts. Heard by everyone.")]
        [SerializeField] string[] _start = new string[0];

        [Tooltip("How long a line stays on screen, in seconds per word, with a 2.5 s floor. 0.35 suits a reader at a relaxed pace.")]
        [Range(0.15f, 1f)] [SerializeField] float _secondsPerWord = 0.35f;

        public float SecondsPerWord => _secondsPerWord;

        public string[] For(SecretaryMoment moment)
        {
            switch (moment)
            {
                case SecretaryMoment.Arrival: return _arrival;
                case SecretaryMoment.Ready: return _ready;
                case SecretaryMoment.NotReady: return _notReady;
                case SecretaryMoment.FirmChosen: return _firmChosen;
                case SecretaryMoment.FirmRefused: return _firmRefused;
                case SecretaryMoment.StartBlocked: return _startBlocked;
                case SecretaryMoment.ShortHanded: return _shortHanded;
                default: return _start;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor tools only: fills a freshly created asset. Never called at runtime (AGENTS.md §4).</summary>
        public void EditorInit(string[] arrival, string[] ready, string[] notReady, string[] firmChosen, string[] firmRefused, string[] startBlocked, string[] shortHanded, string[] start)
        {
            _arrival = arrival;
            _ready = ready;
            _notReady = notReady;
            _firmChosen = firmChosen;
            _firmRefused = firmRefused;
            _startBlocked = startBlocked;
            _shortHanded = shortHanded;
            _start = start;
        }
#endif
    }
}
