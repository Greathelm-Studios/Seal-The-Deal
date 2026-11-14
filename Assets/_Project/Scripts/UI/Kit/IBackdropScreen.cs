using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// A kit screen with a window onto the live sky (lane 8's TitleSky and its view presets). The sky lane puts its
    /// view under BackdropSlot and calls SetLiveBackdrop(true), which hides the screen's flat fallback sky.
    /// </summary>
    public interface IBackdropScreen
    {
        /// <summary>Where the live sky goes; it draws behind the window frame's bars.</summary>
        RectTransform BackdropSlot { get; }

        /// <summary>True once a live view is in the slot. A scene without a sky material keeps the flat fallback.</summary>
        void SetLiveBackdrop(bool live);
    }
}
