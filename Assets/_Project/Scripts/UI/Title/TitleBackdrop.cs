using UnityEngine;

namespace CloseTheDeal.UI.Title
{
    /// <summary>
    /// The title screen's live set: a white window frame seen from inside and slightly below, dividing the
    /// game's own sky into panes. This component only adds the idle life: the camera pivot breathes a few
    /// centimetres, the sun slowly shifts, and the sky plays planes more often than in the game.
    /// It moves the pivot and the sun it is given, never the authored scene objects, and puts the skybox
    /// back when it is disabled so nothing leaks into a gameplay scene. Design: docs/systems/UI_TITLE.md.
    /// </summary>
    public sealed class TitleBackdrop : MonoBehaviour
    {
        [Header("Camera breathing")]
        [Tooltip("The empty pivot the menu camera sits under. It drifts; the camera itself is never touched.")]
        [SerializeField] Transform _rig;
        [Tooltip("How far the camera drifts, in metres. 0.03 = three centimetres. Higher = a more restless frame.")]
        [SerializeField, Range(0f, 0.15f)] float _driftMetres = 0.035f;
        [Tooltip("Seconds for one slow breath in and out. Higher = calmer.")]
        [SerializeField, Range(3f, 30f)] float _breathSeconds = 9f;
        [Tooltip("How far the camera turns while it breathes, in degrees. Higher = more sway.")]
        [SerializeField, Range(0f, 1.5f)] float _swayDegrees = 0.3f;

        [Header("Light")]
        [Tooltip("The sun. It swings slowly so the sun slabs on the floor creep across it.")]
        [SerializeField] Light _sun;
        [Tooltip("How far the sun swings either side of its set angle, in degrees. Higher = the shadows travel further.")]
        [SerializeField, Range(0f, 10f)] float _sunSwingDegrees = 2.5f;
        [Tooltip("Seconds for the sun to swing out and back. Higher = slower shift.")]
        [SerializeField, Range(10f, 180f)] float _sunSeconds = 60f;

        [Header("Sky")]
        [Tooltip("Seconds between planes crossing the sky on this screen (the game uses 40). 0 = leave the sky as it is.")]
        [SerializeField, Range(0f, 120f)] float _planeEverySeconds = 18f;

        [Header("Reduced motion")]
        [Tooltip("When on, the camera and sun hold still if the player turned reduced motion on in settings.")]
        [SerializeField] bool _respectReducedMotion = true;

        Vector3 _baseRigPos;
        Quaternion _baseRigRot, _baseSunRot;

        bool _hasBase;

        void Awake() => CacheBase();

        // WHY lazily as well: Awake does not run on a component in an edit-mode scene, and the capture tool
        // poses it there; without the cache the pivot would be driven from the origin.
        void CacheBase()
        {
            if (_hasBase) return;
            if (_rig != null) { _baseRigPos = _rig.localPosition; _baseRigRot = _rig.localRotation; }
            if (_sun != null) _baseSunRot = _sun.transform.localRotation;
            _hasBase = true;
        }

        void OnEnable() => TitleSkyOverride.Acquire(_planeEverySeconds);

        void OnDisable()
        {
            TitleSkyOverride.Release();
            Pose(0f);
        }

        void Update() => Pose(Application.isPlaying ? Time.unscaledTime : 0f);

        /// <summary>Sets the idle pose for a moment in time (seconds). Capture tools call it to step the idle by hand.</summary>
        public void Pose(float seconds)
        {
            CacheBase();
            bool still = _respectReducedMotion && UiTransition.ReducedMotion;
            float t = still ? 0f : seconds;
            if (_rig != null)
            {
                float a = t * Mathf.PI * 2f / _breathSeconds;
                Vector3 offset = new Vector3(Mathf.Sin(a * 0.5f) * 0.6f, Mathf.Sin(a) * 0.5f, Mathf.Sin(a * 0.25f) * 0.4f) * _driftMetres;
                Quaternion sway = Quaternion.Euler(Mathf.Sin(a * 0.7f) * _swayDegrees, Mathf.Sin(a * 0.35f) * _swayDegrees, 0f);
                _rig.localPosition = _baseRigPos + offset;
                _rig.localRotation = _baseRigRot * sway;
            }
            if (_sun != null)
            {
                float swing = still ? 0f : Mathf.Sin(t * Mathf.PI * 2f / _sunSeconds) * _sunSwingDegrees;
                _sun.transform.localRotation = _baseSunRot * Quaternion.Euler(0f, swing, 0f);
            }
        }
    }

    /// <summary>
    /// Where the title screen leaves room for the UI lane's lockup and menu. All rectangles are fractions of
    /// the screen, measured from the top left, inside a centred 16:9 frame so they hold on 21:9.
    /// </summary>
    public static class TitleSafeAreas
    {
        /// <summary>Calm white lower band: the title lockup and the menu go here and nowhere above it.</summary>
        public static readonly Rect LowerBand = new Rect(0.06f, 0.64f, 0.88f, 0.30f);
        /// <summary>The window and sky: keep it free of UI.</summary>
        public static readonly Rect SkyWindow = new Rect(0.14f, 0.04f, 0.72f, 0.56f);

        /// <summary>The 16:9 frame's share of the screen width: 1 at 16:9, 0.76 at 21:9.</summary>
        public static float FrameWidthShare(float aspect) => Mathf.Min(1f, (16f / 9f) / aspect);

        /// <summary>A safe area as a fraction of the real screen, for any aspect ratio.</summary>
        public static Rect OnScreen(Rect area, float aspect)
        {
            float share = FrameWidthShare(aspect);
            float x0 = 0.5f - share * 0.5f;
            return new Rect(x0 + area.x * share, area.y, area.width * share, area.height);
        }
    }
}
