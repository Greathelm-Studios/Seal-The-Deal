namespace CloseTheDeal.UI
{
    /// <summary>
    /// Every event the UI can ask a sound for. The names match the table in docs/systems/UI_SOUND.md and the
    /// clips Assets/_Project/Audio/UI/UI_&lt;Name&gt;.wav. Call UiSound.Play(UiSoundEvent.X); never load a clip yourself.
    /// </summary>
    public enum UiSoundEvent
    {
        None = 0,
        HoverMove, Focus, Select, Confirm, Back, Error,
        TabChange, ToggleOn, ToggleOff, SliderTick, DropdownOpen, DropdownClose,
        KeyBindListen, KeyBindSet, PanelOpen, PanelClose, Transition, Notification,
        LobbyReady, LobbyUnready, CountdownTick, CountdownGo, MatchWon, MatchLost,
        FloorOpened, BriefcaseTaken, BriefcaseDropped, HandsFull, HitMarker, HitMarkerHead, KillConfirm,
        // Appended 2026-11-14 (never insert above: buttons and the sound set store the number).
        TitleAppear, TransitionCloudWipe, TransitionPaneSlide, TransitionFrameZoom, TransitionFade,
        // Appended 2026-11-15: someone joins or leaves the reception, and a gameplay hint (kept apart from Notification,
        // which is for warnings and prompts).
        PlayerJoined, PlayerLeft, Tip,
    }

    /// <summary>The mixer buses the settings screen controls. Each has an exposed volume parameter on the mixer.</summary>
    public enum AudioBus { Master, Music, Sfx, Voice, Ui }
}
