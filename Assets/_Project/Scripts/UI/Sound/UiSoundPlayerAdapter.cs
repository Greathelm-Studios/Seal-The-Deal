using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>Registers UiSoundPlayer as the active UiSound.Player before the first scene loads.</summary>
    public sealed class UiSoundPlayerAdapter : IUiSoundPlayer
    {
        public bool Play(UiSoundEvent e, float semitones) => UiSoundPlayer.Play(e, semitones);
        public void PlayHover() => UiSoundPlayer.PlayHover();
        public void PlaySlider(float value01) => UiSoundPlayer.PlaySlider(value01);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => UiSound.Player = new UiSoundPlayerAdapter();
    }
}
