using CloseTheDeal.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Combat
{
    /// <summary>Pooled local bullet-hole marks. The oldest mark is reused once the cap is reached; nothing is synced.</summary>
    public sealed class BulletHoles : MonoBehaviour
    {
        const int MaxHoles = 128;
        const float MaxDistance = 60f;
        const float SurfaceLift = 0.002f;
        const float SizeJitter = 0.2f;

        static BulletHoles _instance;
        static Mesh _quad;

        readonly MeshRenderer[] _holes = new MeshRenderer[MaxHoles];
        int _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
        }

        void OnEnable()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        void OnSceneUnloaded(Scene scene)
        {
            for (int i = 0; i < _holes.Length; i++)
            {
                if (_holes[i] != null)
                    _holes[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Leaves a mark on a surface. A mark on something that moves (a prop) rides with it.</summary>
        /// <param name="material">The mark's look.</param>
        /// <param name="point">Where the shot hit, in world space.</param>
        /// <param name="normal">The surface's outward direction there.</param>
        /// <param name="size">Width of the mark, in metres.</param>
        /// <param name="attachTo">What it was hit on when that can move, else null.</param>
        public static void Place(Material material, Vector3 point, Vector3 normal, float size, Transform attachTo)
        {
            if (material == null || size <= 0f || normal.sqrMagnitude < 0.0001f)
                return;

            if (PlayerCamera.Instance != null && (point - PlayerCamera.Instance.transform.position).sqrMagnitude > MaxDistance * MaxDistance)
                return;

            if (_instance == null)
            {
                var go = new GameObject("Bullet Holes");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<BulletHoles>();
            }

            _instance.Spawn(material, point, normal.normalized, size, attachTo);
        }

        void Spawn(Material material, Vector3 point, Vector3 normal, float size, Transform attachTo)
        {
            MeshRenderer hole = _holes[_next];
            if (hole == null)
                hole = _holes[_next] = MakeHole();
            _next = (_next + 1) % MaxHoles;

            Transform t = hole.transform;
            t.SetParent(attachTo != null ? attachTo : transform, false);
            float width = size * (1f + Random.Range(-SizeJitter, SizeJitter));
            Quaternion facing = Quaternion.LookRotation(normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            t.SetPositionAndRotation(point + normal * SurfaceLift, facing);
            Vector3 parentScale = t.parent.lossyScale;
            t.localScale = new Vector3(width / NonZero(parentScale.x), width / NonZero(parentScale.y), width / NonZero(parentScale.z));
            hole.sharedMaterial = material;
            hole.gameObject.SetActive(true);
        }

        static float NonZero(float value)
        {
            return Mathf.Abs(value) > 0.0001f ? value : 1f;
        }

        MeshRenderer MakeHole()
        {
            var go = new GameObject("Hole");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        static Mesh Quad()
        {
            if (_quad != null)
                return _quad;

            _quad = new Mesh { name = "BulletHoleQuad" };
            _quad.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) });
            _quad.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            _quad.SetNormals(new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward });
            _quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            _quad.RecalculateBounds();
            return _quad;
        }
    }
}
