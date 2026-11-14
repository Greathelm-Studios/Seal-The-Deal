using System;
using System.Reflection;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// Plugs UiSoundPlayer into the UI sound hub: before the first scene loads it sets UiSound.Player to an instance of
    /// this class. The hub class lives in the UI kit (another lane's branch), so it is found by name; if the kit is not
    /// in the build yet, nothing is registered and UiSoundPlayer can still be called directly.
    /// </summary>
    public sealed class UiSoundPlayerAdapter : IUiSoundPlayer
    {
        public bool Play(UiSoundEvent e, float semitones) => UiSoundPlayer.Play(e, semitones);
        public void PlayHover() => UiSoundPlayer.PlayHover();
        public void PlaySlider(float value01) => UiSoundPlayer.PlaySlider(value01);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            Type hub = typeof(UiSoundPlayerAdapter).Assembly.GetType("CloseTheDeal.UI.UiSound");
            PropertyInfo slot = hub?.GetProperty("Player", BindingFlags.Public | BindingFlags.Static);
            if (slot == null || !slot.CanWrite || !slot.PropertyType.IsAssignableFrom(typeof(UiSoundPlayerAdapter))) return;
            slot.SetValue(null, new UiSoundPlayerAdapter());
        }
    }
}
