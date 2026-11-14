using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>
    /// For a loose prop with no Visual child: a draw-only copy of its meshes, built once the
    /// first time it is carried, that moves with the carrier's view while the solid part stays
    /// where the host puts it. No colliders, no physics, nothing synced. Skinned meshes are
    /// not copied: a prop that needs one should get a Visual child instead.
    /// </summary>
    static class HandsCopy
    {
        public static Transform Build(Transform root, Renderer[] renderers)
        {
            var copy = new GameObject("In hands").transform;
            copy.SetParent(root, false);

            Vector3 rootScale = root.lossyScale;
            foreach (Renderer renderer in renderers)
            {
                if (renderer is not MeshRenderer meshRenderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                var part = new GameObject(renderer.name) { layer = renderer.gameObject.layer };
                part.transform.SetParent(copy, false);
                part.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                Vector3 scale = renderer.transform.lossyScale;
                part.transform.localScale = new Vector3(scale.x / rootScale.x, scale.y / rootScale.y, scale.z / rootScale.z);

                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var drawn = part.AddComponent<MeshRenderer>();
                drawn.sharedMaterials = meshRenderer.sharedMaterials;
                drawn.shadowCastingMode = meshRenderer.shadowCastingMode;
            }

            copy.gameObject.SetActive(false);
            return copy;
        }
    }
}
