using System;
using UnityEngine;

namespace CloseTheDeal.Onboarding
{
    /// <summary>The moment a first-run hint is for. Each fires the first time it happens to this player.</summary>
    public enum HintTrigger
    {
        /// <summary>Standing in reception, before any match.</summary>
        ArrivedInReception,
        /// <summary>A match starts running.</summary>
        MatchRunning,
        /// <summary>This player picks up the briefcase.</summary>
        CarryingBriefcase,
        /// <summary>The rival firm picks up the briefcase.</summary>
        RivalCarrying,
        /// <summary>This player is killed.</summary>
        Died,
        /// <summary>This player falls out of the building.</summary>
        FellOut,
        /// <summary>A match ends and was recorded.</summary>
        MatchRecorded
    }

    /// <summary>Designer-owned list of first-run onboarding hints and pacing gap. Design: docs/systems/ONBOARDING.md.</summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Onboarding/Hints", fileName = "OnboardingHints")]
    public sealed class OnboardingHints : ScriptableObject
    {
        [Serializable]
        public sealed class Hint
        {
            [Tooltip("A short name the profile remembers this hint by. Change it and everyone sees the hint again once.")]
            public string Id = string.Empty;

            [Tooltip("When it shows: the first time this happens to the player.")]
            public HintTrigger When;

            [Tooltip("What it says. {interact} and {drop} become the keys for picking up and dropping.")]
            [TextArea(1, 3)] public string Text = string.Empty;

            [Tooltip("How long it stays on screen, in seconds.")]
            [Range(2f, 15f)] public float Seconds = 6f;
        }

        [Tooltip("The hints, in no particular order. Leave a moment out and nothing shows for it.")]
        [SerializeField] Hint[] _hints = new Hint[0];

        [Tooltip("Seconds between two hints that arrive together, so the second doesn't cover the first.")]
        [Range(0f, 5f)] [SerializeField] float _gapSeconds = 1f;

        public float GapSeconds => _gapSeconds;

        public Hint For(HintTrigger when)
        {
            for (int i = 0; i < _hints.Length; i++)
            {
                if (_hints[i] != null && _hints[i].When == when)
                    return _hints[i];
            }

            return null;
        }

#if UNITY_EDITOR
        /// <summary>Editor tools only: fills a freshly created asset. Never called at runtime (AGENTS.md §4).</summary>
        public void EditorInit(Hint[] hints) => _hints = hints;
#endif
    }
}
