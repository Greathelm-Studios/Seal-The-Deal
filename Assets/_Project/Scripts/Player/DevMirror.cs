// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-12
//  Prompt    : Alex: "add like a mirror in the level so i can see myself move"
//  Modified  : Claude Fable 5.2, 2026-11-13 — a true reflection that follows the viewer's eye,
//              after Alex enlarged the mirror and the fixed view came out "stretched and pixelated"
//              2026-11-13 — draws only the part of the mirror that is on screen, at screen
//              resolution (Alex: "really pixelated when i get close up to it")
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : A test mirror for judging the character's animation in first person. Its own
//              component because the game hides your own body from you (PlayerMotor draws it
//              shadow-only), so an ordinary reflective surface would show an empty room; this
//              one shows your body to its own camera only, and leaves the motor untouched.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Rendering;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// A development mirror: a true reflection of the room, drawn by a second camera, with the
    /// local player's own body made visible for that camera alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Local only; nothing is networked. Costs one extra camera render of the scene per frame
    /// while enabled, so it is a test aid, not set dressing: remove it from shipping floors.
    /// </para>
    /// <para>
    /// <b>How it reflects:</b> what you see in a mirror is the room as seen from a point as far
    /// behind the glass as your eye is in front of it, looking out through the glass as if it
    /// were a window, and swapped left to right. Each frame the mirror camera is given exactly
    /// that view: the main camera's position reflected through the surface, and a lopsided
    /// frustum whose edges pass through the edges of the glass and whose near plane is the
    /// surface itself. The picture therefore fits the quad at any size, shape or angle, and
    /// shifts as you move, like a real mirror.
    /// </para>
    /// <para>
    /// <b>Sharpness:</b> the picture is about as many pixels as the screen, and each frame it is
    /// spent only on the patch of glass the main camera can see. WHY: spread over the whole of
    /// a 21 m wall mirror, any affordable picture is a few hundred pixels a metre, and standing
    /// close you look at a hand's width of it blown up to fill the screen. Drawing only the
    /// visible patch keeps one mirror pixel near one screen pixel at any distance and any
    /// mirror size.
    /// </para>
    /// <para>
    /// <b>Alternatives that lost:</b> the first version was a fixed 70-degree view from the
    /// mirror's centre, stretched over the quad. It only looked right at the size it was built
    /// at; enlarged, it magnified and smeared. The usual planar reflection (mirror the view
    /// matrix, draw with reversed culling, sample the result in screen space) needs a custom
    /// shader for the surface; this gets the same picture with the stock URP Unlit material.
    /// </para>
    /// <para>
    /// Put it on a Unity quad whose visible face looks at the room, scaled to any size. The
    /// mirror camera can be any child; only its far clip distance is used. The view and
    /// projection are set on the camera directly, so nobody's transform is moved. The quad's
    /// material needs a <c>_BaseMap</c> texture (URP Unlit).
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Renderer))]
    public sealed class DevMirror : MonoBehaviour
    {
        /// <summary>Depth buffer bits for the mirror's picture. WHY 24: the lowest setting that also gives the camera a stencil buffer, which URP expects.</summary>
        const int DepthBits = 24;

        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int BaseMapTransform = Shader.PropertyToID("_BaseMap_ST");

        /// <summary>
        /// Texture scale and offset that flip the picture left to right. WHY: the camera looks
        /// out of the mirror toward the viewer, so its right is the viewer's left; unflipped, you
        /// would raise your right hand and see the figure raise the hand on the other side, like
        /// a webcam, and nothing would line up with where it meets the glass.
        /// </summary>
        static readonly Vector4 FlipLeftRight = new Vector4(-1f, 1f, 1f, 0f);

        /// <summary>
        /// Closest the eye is treated as being to the glass, metres. WHY: the mirror camera's
        /// near plane is the glass, at the eye's distance from it; at zero the frustum has no
        /// depth and the projection is undefined. 5 cm is nearer than the player's capsule lets
        /// the camera get to a wall.
        /// </summary>
        const float MinEyeDistance = 0.05f;

        /// <summary>
        /// How much wider and taller than the visible patch of glass the picture is drawn, as a
        /// share of the patch on each side. WHY: the patch is worked out from the camera's four
        /// corner rays; a little spare stops the picture's edge showing at the screen's edge
        /// if the view turns between this script running and the frame being drawn.
        /// </summary>
        const float PatchMargin = 0.03f;

        /// <summary>Smallest patch of glass drawn, metres a side. WHY: a patch squeezed to nothing (the mirror seen edge on, or just off screen) would make a frustum with no width.</summary>
        const float MinPatch = 0.05f;

        /// <summary>Smallest and largest picture, pixels a side. WHY 4096: beyond any screen this is tested on; it bounds the cost if the scale is turned up on a large display.</summary>
        const int MinPictureSide = 64, MaxPictureSide = 4096;

        /// <summary>A Unity quad is one unit wide and tall before scaling, so half its size is half its scale.</summary>
        const float QuadHalfSize = 0.5f;

        [Header("Mirror")]
        [Tooltip("The camera that draws the reflection. It draws only into the mirror, never to the screen. Where it sits and which way it points do not matter; only its far clip distance is used.")]
        [SerializeField] Camera _camera;

        [Tooltip("Sharpness of the reflection, as a multiple of the screen's own resolution. 1 = as sharp as the rest of the picture at any distance from the mirror. Lower is softer and cheaper; above 1 smooths jagged edges at a cost. The mirror's size makes no difference.")]
        [Range(0.25f, 2f)]
        [SerializeField] float _pictureScale = 1f;

        Renderer _surface;
        /// <summary>The camera the player looks through; the reflection is worked out from where it is.</summary>
        Camera _eye;
        RenderTexture _picture;
        MaterialPropertyBlock _block;

        PlayerMotor _shownPlayer;
        Renderer[] _body;
        ShadowCastingMode[] _hiddenShadows;
        bool[] _hiddenOff;

        void OnEnable()
        {
            if (_camera == null)
            {
                Debug.LogError("[DevMirror] No mirror camera assigned; the mirror stays blank.", this);
                enabled = false;
                return;
            }

            _surface = GetComponent<Renderer>();
            // The size of the screen when the mirror switched on; a window resized afterwards
            // keeps this picture until the mirror is switched off and on again.
            int width = Mathf.Clamp(Mathf.RoundToInt(Screen.width * _pictureScale), MinPictureSide, MaxPictureSide);
            int height = Mathf.Clamp(Mathf.RoundToInt(Screen.height * _pictureScale), MinPictureSide, MaxPictureSide);
            // WHY clamp: the picture covers only a patch of the glass, and the stock material
            // would otherwise tile it across the rest.
            _picture = new RenderTexture(width, height, DepthBits) { name = "DevMirror", wrapMode = TextureWrapMode.Clamp };
            _camera.targetTexture = _picture;

            _block ??= new MaterialPropertyBlock();
            _block.Clear();
            _surface.GetPropertyBlock(_block);
            _block.SetTexture(BaseMap, _picture);
            _block.SetVector(BaseMapTransform, FlipLeftRight);
            _surface.SetPropertyBlock(_block);

            RenderPipelineManager.beginCameraRendering += ShowOwnBody;
            RenderPipelineManager.endCameraRendering += HideOwnBody;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= ShowOwnBody;
            RenderPipelineManager.endCameraRendering -= HideOwnBody;
            if (_camera != null)
            {
                _camera.targetTexture = null;
                _camera.ResetWorldToCameraMatrix();
                _camera.ResetProjectionMatrix();
            }
            if (_picture != null)
            {
                _picture.Release();
                Destroy(_picture);
                _picture = null;
            }
        }

        /// <summary>
        /// Points the mirror camera at what the viewer's eye would see reflected, for this frame.
        /// </summary>
        /// <remarks>
        /// In <c>LateUpdate</c> so the player's camera has already moved for the frame. With no
        /// main camera (a menu, a loading screen) the mirror keeps its last picture.
        /// </remarks>
        void LateUpdate()
        {
            if (_eye == null)
                _eye = Camera.main;
            if (_eye == null)
                return;

            // The quad shows its face to whoever stands on its -Z side.
            Vector3 centre = transform.position;
            Vector3 intoRoom = -transform.forward;
            Vector3 up = transform.up;
            Vector3 size = transform.lossyScale;
            float halfWidth = Mathf.Abs(size.x) * QuadHalfSize, halfHeight = Mathf.Abs(size.y) * QuadHalfSize;

            // The eye, moved to the same distance behind the glass as it is in front of it.
            Vector3 eye = _eye.transform.position;
            float inFront = Vector3.Dot(eye - centre, intoRoom);
            float distance = Mathf.Max(inFront, MinEyeDistance);
            Vector3 behind = eye - intoRoom * (inFront + distance);

            // A camera there, looking squarely out through the glass. Its right is the quad's
            // left, because it faces the other way.
            Quaternion facing = Quaternion.LookRotation(intoRoom, up);
            Vector3 right = facing * Vector3.right;
            Vector3 toCentre = centre - behind;
            float across = Vector3.Dot(toCentre, right), along = Vector3.Dot(toCentre, up);

            // WHY the scale: Unity's cameras look down their own -Z.
            _camera.worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(behind, facing, Vector3.one).inverse;

            // The patch of glass to draw, in metres from the quad's centre along its own right
            // and up: what the main camera can see of it, or all of it if that cannot be told.
            VisiblePatch(centre, intoRoom, transform.right, up, halfWidth, halfHeight,
                out float xMin, out float xMax, out float yMin, out float yMax);

            // The frustum's four sides pass through the patch's edges, and its near plane is the
            // glass, so nothing behind the mirror is drawn and the picture fits the patch exactly.
            // The mirror camera's right is the quad's left, so the patch's right edge is its left.
            _camera.projectionMatrix = Matrix4x4.Frustum(
                across - xMax, across - xMin, along + yMin, along + yMax,
                distance, Mathf.Max(_camera.farClipPlane, distance + 1f));

            // Lay the picture over just that patch of the quad, flipped left to right. A quad's
            // texture runs 0 to 1 across its full width and height.
            float width = halfWidth * 2f, height = halfHeight * 2f, patchWidth = xMax - xMin, patchHeight = yMax - yMin;
            _block.SetVector(BaseMapTransform, new Vector4(
                FlipLeftRight.x * width / patchWidth, height / patchHeight,
                (xMax + halfWidth) / patchWidth, (-halfHeight - yMin) / patchHeight));
            _surface.SetPropertyBlock(_block);
        }

        /// <summary>
        /// Finds the part of the mirror the main camera can see this frame.
        /// </summary>
        /// <param name="centre">The quad's centre, world space.</param>
        /// <param name="intoRoom">The way the glass faces, unit length.</param>
        /// <param name="quadRight">The quad's own right, unit length.</param>
        /// <param name="up">The quad's own up, unit length.</param>
        /// <param name="halfWidth">Half the quad's width, metres.</param>
        /// <param name="halfHeight">Half the quad's height, metres.</param>
        /// <param name="xMin">Left edge of the patch, metres from the centre along <paramref name="quadRight"/>.</param>
        /// <param name="xMax">Right edge of the patch, the same way.</param>
        /// <param name="yMin">Bottom edge of the patch, metres from the centre along <paramref name="up"/>.</param>
        /// <param name="yMax">Top edge of the patch, the same way.</param>
        /// <remarks>
        /// The rays through the four corners of the screen are followed to the plane of the
        /// glass; everything visible of that plane lies between where they land. If any of them
        /// never reaches it (the camera looks along or away from the mirror, so the visible part
        /// runs off to the horizon), the whole quad is used.
        /// </remarks>
        void VisiblePatch(Vector3 centre, Vector3 intoRoom, Vector3 quadRight, Vector3 up, float halfWidth, float halfHeight,
            out float xMin, out float xMax, out float yMin, out float yMax)
        {
            xMin = float.MaxValue; xMax = float.MinValue; yMin = float.MaxValue; yMax = float.MinValue;
            bool all = true;
            for (int corner = 0; corner < 4; corner++)
            {
                // Viewport corners: (0,0), (1,0), (0,1), (1,1).
                Ray ray = _eye.ViewportPointToRay(new Vector3(corner & 1, corner >> 1, 0f));
                // Negative while the ray is heading for the glass.
                float toward = Vector3.Dot(ray.direction, intoRoom);
                float height = Vector3.Dot(ray.origin - centre, intoRoom);
                if (toward >= -Mathf.Epsilon || height <= 0f)
                {
                    all = false;
                    break;
                }
                Vector3 landed = ray.origin + ray.direction * (height / -toward) - centre;
                float x = Vector3.Dot(landed, quadRight), y = Vector3.Dot(landed, up);
                xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x);
                yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y);
            }

            if (all)
            {
                float spareX = (xMax - xMin) * PatchMargin, spareY = (yMax - yMin) * PatchMargin;
                xMin = Mathf.Max(xMin - spareX, -halfWidth); xMax = Mathf.Min(xMax + spareX, halfWidth);
                yMin = Mathf.Max(yMin - spareY, -halfHeight); yMax = Mathf.Min(yMax + spareY, halfHeight);
            }
            // Nothing of the glass on screen, or no answer: draw all of it.
            if (!all || xMax - xMin < MinPatch || yMax - yMin < MinPatch)
            {
                xMin = -halfWidth; xMax = halfWidth; yMin = -halfHeight; yMax = halfHeight;
            }
        }

        /// <summary>Just before the mirror camera draws: makes the local player's body fully visible, remembering how the motor had it.</summary>
        void ShowOwnBody(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering != _camera)
                return;

            PlayerMotor local = PlayerMotor.Local;
            if (local != _shownPlayer)
            {
                // Resolved once per player, not per frame (AGENTS.md §6).
                _shownPlayer = local;
                _body = local != null ? local.GetComponentsInChildren<Renderer>(true) : null;
                _hiddenShadows = _body != null ? new ShadowCastingMode[_body.Length] : null;
                _hiddenOff = _body != null ? new bool[_body.Length] : null;
            }

            if (_body == null)
                return;

            for (int i = 0; i < _body.Length; i++)
            {
                if (_body[i] == null)
                    continue;
                _hiddenShadows[i] = _body[i].shadowCastingMode;
                _hiddenOff[i] = _body[i].forceRenderingOff;
                _body[i].shadowCastingMode = ShadowCastingMode.On;
                _body[i].forceRenderingOff = false;
            }
        }

        /// <summary>Just after the mirror camera draws: puts the body back exactly as the motor had it, so the main view still hides it.</summary>
        void HideOwnBody(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering != _camera || _body == null)
                return;

            for (int i = 0; i < _body.Length; i++)
            {
                if (_body[i] == null)
                    continue;
                _body[i].shadowCastingMode = _hiddenShadows[i];
                _body[i].forceRenderingOff = _hiddenOff[i];
            }
        }
    }
}
