namespace CloseTheDeal.UI
{
    /// <summary>
    /// What the UI sound hub (UiSound, Scripts/UI/Kit) needs from whatever makes the noise. The sound lane's
    /// UiSoundPlayer implements it through UiSoundPlayerAdapter and registers itself before the first scene loads.
    /// </summary>
    public interface IUiSoundPlayer
    {
        /// <summary>Play e, pitched up by semitones. False if it was dropped (gap, voices, unfocused) or has no clip.</summary>
        bool Play(UiSoundEvent e, float semitones);

        /// <summary>A hover: each one in a run climbs the pentatonic scale.</summary>
        void PlayHover();

        /// <summary>A slider tick whose pitch follows the value, 0 to 1.</summary>
        void PlaySlider(float value01);
    }
}
