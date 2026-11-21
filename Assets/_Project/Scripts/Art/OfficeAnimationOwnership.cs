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
        public void Record(Transform[] overlays, string generatedFingerprint)
        {
            screenOverlays = overlays;
            fingerprint = generatedFingerprint;
        }
    }
}
