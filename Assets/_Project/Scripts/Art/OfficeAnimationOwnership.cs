// AI-GENERATED SOURCE FILE
// Generator : Codex (GPT-6)
// Generated : 2026-11-15
// Prompt    : REQUEST_ANIM.md; idempotent AddTo without touching designer-owned objects.
// Reviewed : NOT YET REVIEWED BY A HUMAN
// Summary   : Serialized editor-build receipt; no lifecycle callbacks or runtime work.
using UnityEngine;

namespace CloseTheDeal.Art
{
    /// <summary>Identifies the generated group and its monitor-parented overlays for safe editor rebuilds.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class OfficeAnimationOwnership : MonoBehaviour
    {
        [SerializeField, HideInInspector, Tooltip("Monitor screen overlays created by this group; used only when rebuilding in the editor.")]
        Transform[] screenOverlays;
        [SerializeField, HideInInspector, Tooltip("Fingerprint of the last generated hierarchy; an edited hierarchy is left alone.")]
        string fingerprint;

        /// <summary>The monitor-parented objects created with this group.</summary>
        public Transform[] ScreenOverlays => screenOverlays;

        /// <summary>The generated hierarchy fingerprint used by the editor builder.</summary>
        public string Fingerprint => fingerprint;

        /// <summary>Records the objects and hierarchy fingerprint immediately after a build.</summary>
        /// <param name="overlays">New screen overlays parented to the monitor instances.</param>
        /// <param name="generatedFingerprint">Fingerprint of the new group and overlays.</param>
        public void Record(Transform[] overlays, string generatedFingerprint)
        {
            screenOverlays = overlays;
            fingerprint = generatedFingerprint;
        }
    }
}
