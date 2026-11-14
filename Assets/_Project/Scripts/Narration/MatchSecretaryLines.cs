using System;
using UnityEngine;

namespace CloseTheDeal.Narration
{
    /// <summary>When the secretary speaks during a match. A voice can key its clips off these.</summary>
    public enum MatchMoment
    {
        MatchStart,
        FirstPickup,
        FirstKill,
        Steal,
        FloorOpened,
        CheckpointMoved,
        WindowKill,
        FellOut,
        BriefcaseReturned,
        CapWarning,
        Won,
        Lost,
        NoWinner
    }

    /// <summary>
    /// What the secretary says during a match, a few lines per moment; one is picked at random,
    /// never the same twice in a row. Designer content: read at runtime, never written. Every
    /// shipped line is a proposal for the owner. Design: docs/systems/NARRATION.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Narration/Match Secretary Lines", fileName = "MatchSecretaryLines")]
    public sealed class MatchSecretaryLines : ScriptableObject
    {
        [Serializable]
        public sealed class Moment
        {
            [Tooltip("When these lines are said.")]
            public MatchMoment When;

            [Tooltip("How much this moment matters. A line only interrupts one with the same or a lower number. Results 100, the clock 80, window kills 60, steals 50, floors 40, the rest lower.")]
            [Range(0, 100)] public int Priority = 20;

            [Tooltip("The lines. Words in braces are filled in: {name} the player it is about, {firm} their firm, {rival} the other firm, {killer} and {victim} for a window kill, {floor} the floor's name, {place} where the briefcase came back, {time} the time left.")]
            [TextArea(1, 3)] public string[] Lines = new string[0];
        }

        [Tooltip("Who the subtitles say is speaking.")]
        [SerializeField] string _speaker = "Secretary";

        [Tooltip("How long a line stays up, in seconds per word. 0.35 suits a relaxed reader.")]
        [Range(0.15f, 1f)] [SerializeField] float _secondsPerWord = 0.35f;

        [Tooltip("The shortest time a line stays up, in seconds, however few words it has.")]
        [Range(1f, 6f)] [SerializeField] float _minimumSeconds = 2.5f;

        [Tooltip("The least time between two of her lines, in seconds, so she comments rather than chatters. A higher-priority line still cuts in.")]
        [Range(0f, 20f)] [SerializeField] float _quietSeconds = 4f;

        [Tooltip("What she says, one entry per moment. A moment with no entry or no lines stays silent.")]
        [SerializeField] Moment[] _moments = new Moment[0];

        public string Speaker => _speaker;
        public float SecondsPerWord => _secondsPerWord;
        public float MinimumSeconds => _minimumSeconds;
        public float QuietSeconds => _quietSeconds;

        /// <summary>The entry for a moment, or null if it has none.</summary>
        public Moment For(MatchMoment when)
        {
            for (int i = 0; i < _moments.Length; i++)
            {
                if (_moments[i] != null && _moments[i].When == when)
                    return _moments[i];
            }

            return null;
        }

#if UNITY_EDITOR
        /// <summary>Editor tools only: fills a freshly created asset. Never called at runtime (AGENTS.md §4).</summary>
        public void EditorInit(Moment[] moments) => _moments = moments;
#endif
    }
}
