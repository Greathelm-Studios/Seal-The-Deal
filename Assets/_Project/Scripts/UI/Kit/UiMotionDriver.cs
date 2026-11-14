using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>Ticks UiMotion once per frame on unscaled time. Created by UiMotion itself; nothing to place.</summary>
    [AddComponentMenu("")]
    public sealed class UiMotionDriver : MonoBehaviour
    {
        void Update()
        {
            if (!UiMotion.Paused)
                UiMotion.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        }
    }
}
