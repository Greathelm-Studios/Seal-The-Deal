using System;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>Central UI sound dispatch hub forwarding calls to the registered IUiSoundPlayer.</summary>
    public static class UiSound
    {
        /// <summary>What actually plays sounds. Set once by the sound lane before the first scene loads.</summary>
        public static IUiSoundPlayer Player { get; set; }

        /// <summary>Raised for every request, sounded or not, with the event asked for. For logs and checks.</summary>
        public static event Action<UiSoundEvent> Played;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Player = null;
            Played = null;
        }

        /// <summary>Asks for a UI sound. Silent until the sound lane's player is registered.</summary>
        public static void Play(UiSoundEvent e)
        {
            if (e == UiSoundEvent.None)
                return;
            Played?.Invoke(e);
            Player?.Play(e, 0f);
        }

        /// <summary>A pointer moved onto a control. Each hover in a quick run climbs the scale (the player decides how).</summary>
        public static void PlayHover()
        {
            Played?.Invoke(UiSoundEvent.HoverMove);
            Player?.PlayHover();
        }

        /// <summary>A slider moved a step; the tick's pitch follows the value, 0 to 1.</summary>
        public static void PlaySlider(float value01)
        {
            Played?.Invoke(UiSoundEvent.SliderTick);
            Player?.PlaySlider(value01);
        }
    }
}
