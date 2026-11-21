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

    /// <summary>Local voice chat preferences persisted in PlayerPrefs (never synced).</summary>
    public static class VoiceSettings
    {
        const string ModeKey = "ctd.voice.mode";
        const string DeviceKey = "ctd.voice.device";
        const string NoiseKey = "ctd.voice.noise";

        public const MicMode DefaultMode = MicMode.Open;
        public const bool DefaultNoiseSuppression = true;

        static bool _loaded;
        static MicMode _mode;
        static string _device;
        static bool _noise;
        static bool _hearSelf;

        /// <summary>Raised when any value here changes, for the HUD, the settings rows and the microphone itself.</summary>
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

        /// <summary>Selected microphone name, or empty for the system default.</summary>
        public static string Device
        {
            get
            {
                Load();
                return _device;
            }
            set
            {
                Load();
                value ??= string.Empty;
                if (_device == value)
                    return;
                _device = value;
                PlayerPrefs.SetString(DeviceKey, value);
                Changed?.Invoke();
            }
        }

        /// <summary>True to clean keyboard, fan and room noise out of the microphone before it is sent.</summary>
        public static bool NoiseSuppression
        {
            get
            {
                Load();
                return _noise;
            }
            set
            {
                Load();
                if (_noise == value)
                    return;
                _noise = value;
                PlayerPrefs.SetInt(NoiseKey, value ? 1 : 0);
                Changed?.Invoke();
            }
        }

        /// <summary>Transient mute state for open microphone mode (resets each launch).</summary>
        public static bool Muted { get; private set; }

        /// <summary>Transient local loopback mic test toggle (not saved).</summary>
        public static bool HearSelf
        {
            get => _hearSelf;
            set
            {
                if (_hearSelf == value)
                    return;
                _hearSelf = value;
                Changed?.Invoke();
            }
        }

        /// <summary>True when every saved value is how the game shipped.</summary>
        public static bool AtDefaults => Mode == DefaultMode && Device.Length == 0 && NoiseSuppression == DefaultNoiseSuppression;

        /// <summary>Puts every saved value back to how the game shipped.</summary>
        public static void ResetToDefaults()
        {
            Mode = DefaultMode;
            Device = string.Empty;
            NoiseSuppression = DefaultNoiseSuppression;
        }

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
            int saved = PlayerPrefs.GetInt(ModeKey, (int)DefaultMode);
            _mode = saved >= 0 && saved <= (int)MicMode.Off ? (MicMode)saved : DefaultMode;
            _device = PlayerPrefs.GetString(DeviceKey, string.Empty);
            _noise = PlayerPrefs.GetInt(NoiseKey, DefaultNoiseSuppression ? 1 : 0) != 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _loaded = false;
            Muted = false;
            _hearSelf = false;
            Changed = null;
        }
    }
}
