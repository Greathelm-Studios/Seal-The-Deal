using UnityEngine;

namespace CloseTheDeal.Nameplates
{
    /// <summary>
    /// Where a point in the world lands on a screen-space canvas, for anything pinned over the
    /// game (names over heads, an off-screen edge marker). Allocation-free; safe per frame.
    /// </summary>
    public static class WorldToCanvas
    {
        /// <summary>
        /// The canvas position (anchored at the bottom-left corner, in canvas units) of a world point
        /// seen by this camera. Returns false when the point is behind the camera; then
        /// <paramref name="at"/> is the mirrored screen point, which an edge marker can clamp to a border.
        /// </summary>
        public static bool TryProject(Camera camera, Canvas canvas, Vector3 world, out Vector2 at)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            bool inFront = screen.z > 0f;
            if (!inFront)
            {
                screen.x = camera.pixelWidth - screen.x;
                screen.y = camera.pixelHeight - screen.y;
            }

            at = new Vector2(screen.x / scale, screen.y / scale);
            return inFront;
        }
    }
}
