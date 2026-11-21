using UnityEngine;
using UnityEngine.Rendering;

namespace CloseTheDeal.Player
{
    /// <summary>Development mirror rendering a true reflected view onto a URP Unlit quad, showing the local player's body to its own camera.</summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class DevMirror : MonoBehaviour
    {
        // 24-bit depth provides the stencil buffer URP expects.
        const int DepthBits = 24;

        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int BaseMapTransform = Shader.PropertyToID("_BaseMap_ST");
        static readonly Vector4 FlipLeftRight = new Vector4(-1f, 1f, 1f, 0f);
        const float MinEyeDistance = 0.05f;
        const float PatchMargin = 0.03f;
        const float MinPatch = 0.05f;
        const int MinPictureSide = 64, MaxPictureSide = 4096;
        const float QuadHalfSize = 0.5f;

        [Header("Mirror")]
        [Tooltip("The camera that draws the reflection. It draws only into the mirror, never to the screen. Where it sits and which way it points do not matter; only its far clip distance is used.")]
        [SerializeField] Camera _camera;

        [Tooltip("Sharpness of the reflection, as a multiple of the screen's own resolution. 1 = as sharp as the rest of the picture at any distance from the mirror. Lower is softer and cheaper; above 1 smooths jagged edges at a cost. The mirror's size makes no difference.")]
        [Range(0.25f, 2f)]
        [SerializeField] float _pictureScale = 1f;

        Renderer _surface;
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
            int width = Mathf.Clamp(Mathf.RoundToInt(Screen.width * _pictureScale), MinPictureSide, MaxPictureSide);
            int height = Mathf.Clamp(Mathf.RoundToInt(Screen.height * _pictureScale), MinPictureSide, MaxPictureSide);
            // Clamp wrap prevents the stock Unlit shader from tiling the partial patch across the quad.
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

        void LateUpdate()
        {
            if (_eye == null)
                _eye = Camera.main;
            if (_eye == null)
                return;

            Vector3 centre = transform.position;
            Vector3 intoRoom = -transform.forward;
            Vector3 up = transform.up;
            Vector3 size = transform.lossyScale;
            float halfWidth = Mathf.Abs(size.x) * QuadHalfSize, halfHeight = Mathf.Abs(size.y) * QuadHalfSize;

            Vector3 eye = _eye.transform.position;
            float inFront = Vector3.Dot(eye - centre, intoRoom);
            float distance = Mathf.Max(inFront, MinEyeDistance);
            Vector3 behind = eye - intoRoom * (inFront + distance);

            Quaternion facing = Quaternion.LookRotation(intoRoom, up);
            Vector3 right = facing * Vector3.right;
            Vector3 toCentre = centre - behind;
            float across = Vector3.Dot(toCentre, right), along = Vector3.Dot(toCentre, up);

            // Unity cameras look down -Z.
            _camera.worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(behind, facing, Vector3.one).inverse;

            VisiblePatch(centre, intoRoom, transform.right, up, halfWidth, halfHeight,
                out float xMin, out float xMax, out float yMin, out float yMax);

            _camera.projectionMatrix = Matrix4x4.Frustum(
                across - xMax, across - xMin, along + yMin, along + yMax,
                distance, Mathf.Max(_camera.farClipPlane, distance + 1f));

            float width = halfWidth * 2f, height = halfHeight * 2f, patchWidth = xMax - xMin, patchHeight = yMax - yMin;
            _block.SetVector(BaseMapTransform, new Vector4(
                FlipLeftRight.x * width / patchWidth, height / patchHeight,
                (xMax + halfWidth) / patchWidth, (-halfHeight - yMin) / patchHeight));
            _surface.SetPropertyBlock(_block);
        }

        void VisiblePatch(Vector3 centre, Vector3 intoRoom, Vector3 quadRight, Vector3 up, float halfWidth, float halfHeight,
            out float xMin, out float xMax, out float yMin, out float yMax)
        {
            xMin = float.MaxValue; xMax = float.MinValue; yMin = float.MaxValue; yMax = float.MinValue;
            bool all = true;
            for (int corner = 0; corner < 4; corner++)
            {
                Ray ray = _eye.ViewportPointToRay(new Vector3(corner & 1, corner >> 1, 0f));
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
            if (!all || xMax - xMin < MinPatch || yMax - yMin < MinPatch)
            {
                xMin = -halfWidth; xMax = halfWidth; yMin = -halfHeight; yMax = halfHeight;
            }
        }

        void ShowOwnBody(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering != _camera)
                return;

            PlayerMotor local = PlayerMotor.Local;
            if (local != _shownPlayer)
            {
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
