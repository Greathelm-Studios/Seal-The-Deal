using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>A kit screen that hosts a live sky backdrop behind its window frame.</summary>
    public interface IBackdropScreen
    {
        /// <summary>Slot behind the window bars where the live sky view attaches.</summary>
        RectTransform BackdropSlot { get; }

        /// <summary>Switches between the live sky view and the static fallback.</summary>
        void SetLiveBackdrop(bool live);
    }
}
