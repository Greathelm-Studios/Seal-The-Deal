using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// Plugs UiSoundPlayer into the UI sound hub (UiSound, Scripts/UI/Kit): registered before the first scene loads.
    /// </summary>
    public sealed class UiSoundPlayerAdapter : IUiSoundPlayer
    {
        public bool Play(UiSoundEvent e, float semitones) => UiSoundPlayer.Play(e, semitones);
        public void PlayHover() => UiSoundPlayer.PlayHover();
        public void PlaySlider(float value01) => UiSoundPlayer.PlaySlider(value01);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => UiSound.Player = new UiSoundPlayerAdapter();
    }
}
