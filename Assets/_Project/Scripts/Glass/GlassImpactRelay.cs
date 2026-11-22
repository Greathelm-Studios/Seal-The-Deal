using UnityEngine;

namespace CloseTheDeal.Glass
{
    /// <summary>Passes hits on a collider to the pane that owns it, when the two sit on different objects. Added by GlassRegistry.</summary>
    [DisallowMultipleComponent]
    public sealed class GlassImpactRelay : MonoBehaviour
    {
        internal GlassPane Pane;

        void OnCollisionEnter(Collision collision)
        {
            if (Pane != null)
                Pane.Impact(collision);
        }
    }
}
