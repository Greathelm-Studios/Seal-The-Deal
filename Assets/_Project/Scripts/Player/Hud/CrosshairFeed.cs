using CloseTheDeal.Combat;
using CloseTheDeal.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Player.Hud
{
    /// <summary>
    /// Drives the one crosshair (the UI kit's <see cref="UiCrosshair"/>) from the local player:
    /// its arms open with speed and while airborne, or by the gun's own spread when that is wider
    /// (the spread already grows when moving or airborne, so the two are never added); a hard
    /// landing, a hit and a kill pulse it. Shooting draws its own hit marker; this only moves the
    /// crosshair. Tuning: the Crosshair values on MovementProfile.
    /// </summary>
    [AddComponentMenu("Close the Deal/Movement/Crosshair Feed")]
    public sealed class CrosshairFeed : MonoBehaviour
    {
        [Tooltip("The HUD's crosshair.")]
        [SerializeField] UiCrosshair _crosshair;

        /// <summary>Half the height of the 1080p design frame the crosshair's pixel sizes are written for.</summary>
        const float HalfDesignHeight = 540f;
        const float HitPulse = 0.5f;
        const float PrecisionHitPulse = 0.8f;
        const float KillPulse = 1f;
        const float LandingPulse = 0.3f;

        static CrosshairFeed _instance;
        static bool _sceneHasNoCrosshair;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _sceneHasNoCrosshair = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => _sceneHasNoCrosshair = false;

        PlayerMotor _motor;
        WeaponHolder _weapons;
        Camera _camera;
        float _open;

        /// <summary>The gap being fed, in pixels at 100% size. For checks.</summary>
        public float Open => _open;

        /// <summary>
        /// Makes sure the HUD's crosshair has a feed: the scene's own if a set-up menu added one,
        /// otherwise one put on the HUD crosshair now (not the settings screen's preview tiles).
        /// Called when the camera starts following the local player, so every scene with the kit
        /// crosshair gets it without a scene edit. Nothing is added when there is no HUD crosshair.
        /// </summary>
        public static void EnsureInScene()
        {
            if (_instance != null || _sceneHasNoCrosshair)
                return;

            CrosshairFeed existing = FindAnyObjectByType<CrosshairFeed>(FindObjectsInactive.Include);
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            foreach (UiCrosshair crosshair in FindObjectsByType<UiCrosshair>(FindObjectsInactive.Include))
            {
                if (crosshair.GetComponentInParent<SettingsScreen>(true) != null)
                    continue;

                var feed = crosshair.gameObject.AddComponent<CrosshairFeed>();
                feed._crosshair = crosshair;
                _instance = feed;
                if (feed.isActiveAndEnabled)
                    feed.Bind(PlayerMotor.Local);
                return;
            }

            _sceneHasNoCrosshair = true;
        }

        void Awake()
        {
            _instance = this;
            _sceneHasNoCrosshair = false;
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        void OnEnable()
        {
            PlayerMotor.LocalChanged += Bind;
            Bind(PlayerMotor.Local);
        }

        void OnDisable()
        {
            PlayerMotor.LocalChanged -= Bind;
            Bind(null);
        }

        void Bind(PlayerMotor motor)
        {
            if (_motor != null)
            {
                _motor.Landed -= OnLanded;
                _motor.Jumped -= OnJumped;
                _motor.SlideChanged -= OnSlideChanged;
            }
            if (_weapons != null)
            {
                _weapons.HitConfirmed -= OnHit;
                _weapons.KillConfirmed -= OnKill;
            }

            _motor = motor;
            _weapons = motor != null ? motor.GetComponent<WeaponHolder>() : null;
            if (_motor != null)
            {
                _motor.Landed += OnLanded;
                _motor.Jumped += OnJumped;
                _motor.SlideChanged += OnSlideChanged;
            }
            if (_weapons != null)
            {
                _weapons.HitConfirmed += OnHit;
                _weapons.KillConfirmed += OnKill;
            }

            _open = 0f;
            if (_crosshair != null)
                _crosshair.OpenPixels = 0f;
        }

        /// <summary>A continuously moving visual, so it reads the motor once a frame; writes OpenPixels only when it changes.</summary>
        void Update()
        {
            if (_motor == null || _crosshair == null)
                return;

            float target = Mathf.Max(MovementOpen(), SpreadOpen());
            float next = Mathf.MoveTowards(_open, target, _motor.Profile.CrosshairOpenRate * Time.unscaledDeltaTime);
            if (next != _open)
            {
                _open = next;
                _crosshair.OpenPixels = _open;
            }
        }

        float MovementOpen()
        {
            MovementProfile profile = _motor.Profile;
            PlayerAnimState state = _motor.AnimState;
            float open = Mathf.Max(0f, state.PlanarSpeed - profile.WalkSpeed) * profile.CrosshairOpenPerSpeed;
            if (!state.Grounded && !state.Mantling)
                open += profile.CrosshairOpenInAir;
            if (state.Sliding)
                open += profile.CrosshairOpenSliding;
            return Mathf.Min(open, profile.CrosshairOpenMax);
        }

        /// <summary>The gun's stray as a gap on screen: where a shot at the edge of the spread would land, in 1080p pixels.</summary>
        float SpreadOpen()
        {
            if (_weapons == null)
                return 0f;

            float spread = _weapons.CurrentSpreadDegrees;
            if (spread <= 0f)
                return 0f;

            if (_camera == null)
            {
                PlayerCamera pc = PlayerCamera.Instance;
                _camera = pc != null ? pc.GetComponent<Camera>() : Camera.main;
            }
            if (_camera == null)
                return 0f;

            float halfFov = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            return HalfDesignHeight * Mathf.Tan(spread * Mathf.Deg2Rad) / Mathf.Tan(halfFov);
        }

        void OnLanded(float fallSpeed)
        {
            if (_crosshair != null && fallSpeed >= _motor.Profile.CrosshairLandingPulseSpeed)
                _crosshair.Pulse(LandingPulse);
        }

        void OnJumped()
        {
            if (_crosshair != null)
                _crosshair.Pulse(_motor.Profile.CrosshairJumpPulse);
        }

        void OnSlideChanged(bool sliding)
        {
            if (sliding && _crosshair != null)
                _crosshair.Pulse(_motor.Profile.CrosshairSlidePulse);
        }

        void OnHit(Vector3 point, float damage, bool killed, bool precision)
        {
            if (_crosshair != null)
                _crosshair.Pulse(precision ? PrecisionHitPulse : HitPulse);
        }

        void OnKill(Vector3 point)
        {
            if (_crosshair != null)
                _crosshair.Pulse(KillPulse);
        }
    }
}
