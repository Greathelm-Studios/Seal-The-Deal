using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>Local-only MaterialPropertyBlock highlight for the liftable item under the crosshair.</summary>
    static class LookHighlight
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly MaterialPropertyBlock Block = new();

        /// <summary>How far the item's own colour is lifted toward white while looked at.</summary>
        const float Lift = 0.28f;

        public static void Set(Renderer[] renderers, bool on)
        {
            foreach (Renderer renderer in renderers)
            {
                // Cleared even while hidden (a carried prop hides its own meshes), so nothing stays
                // brightened once it is shown again; only turned on where it can be seen.
                if (renderer == null || (on && !renderer.enabled))
                    continue;

                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(BaseColorId))
                    continue;

                if (!on)
                {
                    if (IsOurs(renderer))
                        renderer.SetPropertyBlock(null);
                    continue;
                }

                if (renderer.HasPropertyBlock())
                    continue;

                Color own = material.GetColor(BaseColorId);
                Block.Clear();
                Block.SetColor(BaseColorId, Color.Lerp(own, Color.white, Lift));
                renderer.SetPropertyBlock(Block);
            }
        }

        static bool IsOurs(Renderer renderer)
        {
            if (!renderer.HasPropertyBlock())
                return false;

            Block.Clear();
            renderer.GetPropertyBlock(Block);
            Color drawn = Block.GetColor(BaseColorId);
            Color own = renderer.sharedMaterial.GetColor(BaseColorId);
            Color expected = Color.Lerp(own, Color.white, Lift);
            return Mathf.Abs(drawn.r - expected.r) < 0.002f
                && Mathf.Abs(drawn.g - expected.g) < 0.002f
                && Mathf.Abs(drawn.b - expected.b) < 0.002f;
        }
    }
}
