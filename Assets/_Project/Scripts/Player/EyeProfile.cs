// AI-GENERATED SOURCE FILE
// Generator : Codex (GPT-6)
// Generated : 2026-11-12
// Prompt    : Give the STDMC player character animated 2D eye cards.
// Reviewed  : NOT YET REVIEWED BY A HUMAN
// Summary   : Designer-owned timing and expression thresholds for the eye cards.
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>Read-only runtime tuning for the character's eye cards.</summary>
    [CreateAssetMenu(menuName = "Close the Deal/Eye Profile", fileName = "EyeProfile")]
    public sealed class EyeProfile : ScriptableObject
    {
        // WHY: a complete blink needs time to show both its closing and opening frames.
        private const float ShortestBlink = 0.08f;

        [Header("Blink")]
        [Tooltip("Shortest (X) and longest (Y) wait between blinks, in seconds. Raise either for less frequent blinking. Negative waits count as zero; swapped values are read in ascending order.")]
        [SerializeField] private Vector2 _blinkInterval = new(2f, 5f);

        [Tooltip("Time in seconds to close and reopen the eyes. Higher makes a slower blink. Values below 0.08 seconds use 0.08 so the blink has time to show.")]
        [Min(ShortestBlink)]
        [SerializeField] private float _blinkDuration = 0.16f;

        [Header("Glance")]
        [Tooltip("Furthest the pupils travel from centre, as a fraction of one eye card's width. Higher makes the glance more obvious. Zero keeps them centred.")]
        [Range(0f, 0.25f)]
        [SerializeField] private float _glanceRange = 0.16f;

        [Tooltip("How quickly the pupils travel, in eye-card widths per second. Higher makes them track sooner. Zero keeps them where they are.")]
        [Min(0f)]
        [SerializeField] private float _glanceSpeed = 1.6f;

        [Header("Expressions")]
        [Tooltip("How strong a stagger must be to make the eyes wince, from 0 (the slightest stagger) to 1 (the strongest). Knockback always makes the eyes wince.")]
        [Range(0f, 1f)]
        [SerializeField] private float _winceThreshold = 0.35f;

        [Tooltip("How far into the airborne pose the eyes go wide, from 0 (as soon as the pose starts) to 1 (fully in the pose). Lower makes the eyes open sooner.")]
        [Range(0f, 1f)]
        [SerializeField] private float _wideThreshold = 0.5f;

        [Tooltip("Where between walking and full sprint the eyes narrow, from 0 (just above walking speed) to 1 (full sprint). Lower makes the eyes narrow sooner.")]
        [Range(0f, 1f)]
        [SerializeField] private float _focusThreshold = 0.5f;

        /// <summary>Seconds between blinks, shortest to longest.</summary>
        public Vector2 BlinkInterval => new(Mathf.Max(0f, Mathf.Min(_blinkInterval.x, _blinkInterval.y)),
            Mathf.Max(0f, Mathf.Max(_blinkInterval.x, _blinkInterval.y)));

        /// <summary>Seconds for a complete blink.</summary>
        public float BlinkDuration => Mathf.Max(ShortestBlink, _blinkDuration);

        /// <summary>Maximum pupil displacement in card widths.</summary>
        public float GlanceRange => _glanceRange;

        /// <summary>Pupil speed in card widths per second.</summary>
        public float GlanceSpeed => _glanceSpeed;

        /// <summary>Minimum stagger weight for the wince.</summary>
        public float WinceThreshold => _winceThreshold;

        /// <summary>Minimum airborne pose weight for the wide eyes.</summary>
        public float WideThreshold => _wideThreshold;

        /// <summary>Minimum weight between a walk and full sprint for the focused eyes.</summary>
        public float FocusThreshold => _focusThreshold;
    }
}
