namespace CloseTheDeal.UI
{
    /// <summary>Playback backend registered on UiSound.Player by the UI sound system.</summary>
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
