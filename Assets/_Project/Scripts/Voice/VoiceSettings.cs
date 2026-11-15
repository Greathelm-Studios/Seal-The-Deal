// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "Add VOIP/Proximity chat to the game."
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : This player's own voice chat choices (how the microphone opens, and whether
//              they hear themselves for a mic test), remembered between launches.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CloseTheDeal.Voice
{
    /// <summary>How this player's microphone opens.</summary>
    public enum MicMode
    {
        /// <summary>The microphone is open all the time; the voice key mutes and unmutes it.</summary>
        Open = 0,

        /// <summary>The microphone is open only while the voice key is held.</summary>
        PushToTalk = 1,

        /// <summary>Nothing is recorded or sent. Other players are still heard.</summary>
        Off = 2,
    }

    /// <summary>
    /// This player's voice chat choices. Local to this machine and never synced: the host
    /// only ever sees the voice a player chose to send.
    /// </summary>
    public static class VoiceSettings
    {
        const string ModeKey = "ctd.voice.mode";

        static bool _loaded;
        static MicMode _mode;

        /// <summary>Raised when <see cref="Mode"/> or <see cref="Muted"/> changes, for a HUD or settings row.</summary>
        public static event System.Action Changed;

        /// <summary>How the microphone opens. Saved with the other preferences.</summary>
        public static MicMode Mode
        {
            get
            {
                Load();
                return _mode;
            }
            set
            {
                Load();
                if (_mode == value)
                    return;
                _mode = value;
                PlayerPrefs.SetInt(ModeKey, (int)value);
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// True while the player has muted an open microphone with the voice key. Not saved:
        /// every launch starts unmuted, so nobody is silent without knowing why.
        /// </summary>
        public static bool Muted { get; private set; }

        /// <summary>
        /// Plays this player's own voice back to them, flat, after it has been through the same
        /// compression other players hear. A mic test; not saved.
        /// </summary>
        public static bool HearSelf { get; set; }

        /// <summary>Mutes or unmutes an open microphone.</summary>
        public static void SetMuted(bool muted)
        {
            if (Muted == muted)
                return;
            Muted = muted;
            Changed?.Invoke();
        }

        static void Load()
        {
            if (_loaded)
                return;
            _loaded = true;
            int saved = PlayerPrefs.GetInt(ModeKey, (int)MicMode.Open);
            _mode = saved >= 0 && saved <= (int)MicMode.Off ? (MicMode)saved : MicMode.Open;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _loaded = false;
            Muted = false;
            HearSelf = false;
            Changed = null;
        }
    }
}
