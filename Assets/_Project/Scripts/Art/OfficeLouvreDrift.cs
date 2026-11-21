using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CloseTheDeal.Art
{
    /// <summary>Locally sways a generated sun cookie, without rotating or translating the sun.</summary>
    [DisallowMultipleComponent]
    public sealed class OfficeLouvreDrift : MonoBehaviour
    {
        const float Tau = Mathf.PI * 2f;

        [Header("Sun")]
        [SerializeField, Tooltip("Sun whose louvre pattern moves. Its position and direction stay as placed.")]
        UniversalAdditionalLightData sunData;
        [SerializeField, Tooltip("Louvre texture this effect owns. Changing the sun to another texture stops this effect.")]
        Texture cookie;

        [Header("Gentle blind movement")]
        [SerializeField, Tooltip("Maximum shadow travel each side of rest, metres. Higher = wider sway. Zero keeps the shadows still.")]
        float swayMetres = 0.12f;
        [SerializeField, Tooltip("Seconds for one complete sway. Higher = slower shadows. Must be greater than zero; invalid values stop the effect.")]
        float swaySeconds = 30f;

        Light cachedSun;
        Vector2 authoredOffsetMetres;
        Vector2 runtimeOffsetMetres;
        bool ready;
        bool wroteOffset;

        /// <summary>Assigns a newly created effect; its resting offset is read from the sun when Play starts.</summary>
        public void Configure(UniversalAdditionalLightData target, Texture generatedCookie)
        {
            sunData = target;
            cookie = generatedCookie;
        }

        void OnEnable()
        {
            // Never mutate scene lighting outside Play Mode (including with domain reload disabled).
            ready = false;
            wroteOffset = false;
            if (!Application.isPlaying || sunData == null || cookie == null)
                return;
            if (swaySeconds <= 0f || swayMetres < 0f)
            {
                Debug.LogWarning("[Office animation] Louvre sway needs non-negative travel and a period above zero; left the sun alone.", this);
                return;
            }
            cachedSun = sunData.GetComponent<Light>();
            authoredOffsetMetres = sunData.lightCookieOffset;
            runtimeOffsetMetres = authoredOffsetMetres;
            ready = cachedSun != null && cachedSun.type == LightType.Directional;
        }

        void LateUpdate()
        {
            if (!ready || sunData == null || cachedSun == null || cachedSun.cookie != cookie ||
                swaySeconds <= 0f || swayMetres < 0f)
                return;
            // URP cookie offset is in projection metres, not normalized UVs.
            float phase = (float)(Time.timeAsDouble % swaySeconds) * Tau / swaySeconds;
            Vector2 next = authoredOffsetMetres + Vector2.up * (Mathf.Sin(phase) * swayMetres);
            if (runtimeOffsetMetres == next)
                return;
            sunData.lightCookieOffset = next;
            runtimeOffsetMetres = next;
            wroteOffset = true;
        }

        void OnDisable()
        {
            // Restore only if no other tool overwrote our last runtime offset.
            if (wroteOffset && sunData != null && cachedSun != null && cachedSun.cookie == cookie &&
                sunData.lightCookieOffset == runtimeOffsetMetres)
                sunData.lightCookieOffset = authoredOffsetMetres;
            ready = false;
            wroteOffset = false;
        }
    }
}
