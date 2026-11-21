using CloseTheDeal.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>Settings Audio tab: mixer bus sliders, output meter, and voice chat controls.</summary>
    [AddComponentMenu("Close the Deal/UI/Settings Page - Audio")]
    public sealed class AudioPage : UiSettingsPage
    {
        [Tooltip("Master, Music, Effects, Voice, Interface: same order as the mixer's buses.")]
        [SerializeField] UiSlider[] _buses = new UiSlider[5];
        [Tooltip("Plays one interface sound so the levels can be heard.")]
        [SerializeField] UiButton _test;
        [Tooltip("The meter's bar: its width follows the loudest sample just played.")]
        [SerializeField] RectTransform _meterBar;
        [Tooltip("The meter bar's full width in pixels.")]
        [SerializeField] float _meterWidth = 400f;
        [Tooltip("Voice chat: which microphone to record from. The first choice is the system default.")]
        [SerializeField] UiStepper _microphone;
        [Tooltip("Voice chat: open microphone, push to talk, or off.")]
        [SerializeField] UiStepper _micMode;
        [Tooltip("Voice chat: clean keyboard, fan and room noise out of the microphone.")]
        [SerializeField] UiToggle _noise;
        [Tooltip("Voice chat: play the player's own voice back to them while this tab shows.")]
        [SerializeField] UiToggle _micTest;

        static readonly AudioBus[] Order = { AudioBus.Master, AudioBus.Music, AudioBus.Sfx, AudioBus.Voice, AudioBus.Ui };
        static readonly string[] ModeNames = { "Open microphone", "Push to talk", "Off" };
        const string DefaultMicName = "System default";

        const int MicNameLength = 18;

        bool HasVoice => _microphone != null && _micMode != null && _noise != null && _micTest != null;

        string[] _devices = System.Array.Empty<string>();

        static string ShortName(string device)
        {
            string name = device;
            int open = name.IndexOf('(');
            if (open >= 0 && name.EndsWith(")"))
                name = name.Substring(open + 1, name.Length - open - 2);
            return name.Length > MicNameLength ? name.Substring(0, MicNameLength - 1).TrimEnd() + "\u2026" : name;
        }

        void ShowMicrophones()
        {
            _devices = Microphone.devices;
            var names = new string[_devices.Length + 1];
            names[0] = DefaultMicName;
            for (int i = 0; i < _devices.Length; i++)
                names[i + 1] = ShortName(_devices[i]);
            _microphone.SetOptions(names, System.Array.IndexOf(_devices, VoiceSettings.Device) + 1);
        }

        static string RowName(Component control)
        {
            var label = control.transform.parent != null ? control.transform.parent.Find("Name") : null;
            return label != null && label.TryGetComponent(out TMPro.TMP_Text text) ? text.text : control.name;
        }

        static int DefaultPercent(int i) => Mathf.RoundToInt(UiAudio.DefaultOf(Order[i]) * 100f);

        System.Action<int>[] _handlers;
        UiSoundMeter _meter;
        bool _addedMeter;
        float _shown;

        void OnEnable()
        {
            _handlers ??= new System.Action<int>[_buses.Length];
            for (int i = 0; i < _buses.Length; i++)
            {
                _buses[i].minValue = 0f;
                _buses[i].maxValue = 20f;
                _buses[i].Configure(5f, "%");
                int bus = i;
                _handlers[i] ??= percent => OnVolume(bus, percent);
                _buses[i].Changed += _handlers[i];
            }

            _test.ConfirmSound = UiSoundEvent.None;   // its answer is the test sound; a Confirm in the same frame masks it
            _test.Clicked += OnTest;
            AttachMeter();
            if (HasVoice)
            {
                _micMode.SetOptions(ModeNames, (int)VoiceSettings.Mode);
                _microphone.Changed += OnMicrophone;
                _micMode.Changed += OnMicMode;
                _noise.Changed += OnNoise;
                _micTest.Changed += OnMicTest;
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < _buses.Length; i++)
                _buses[i].Changed -= _handlers[i];
            _test.Clicked -= OnTest;
            DetachMeter();
            if (HasVoice)
            {
                _microphone.Changed -= OnMicrophone;
                _micMode.Changed -= OnMicMode;
                _noise.Changed -= OnNoise;
                _micTest.Changed -= OnMicTest;
            }

            VoiceSettings.HearSelf = false;
        }

        public override void ShowSaved()
        {
            // A page shown again before its fade-out finished never gets OnEnable, but Hidden() detached the meter.
            AttachMeter();
            for (int i = 0; i < _buses.Length; i++)
                _buses[i].SetDisplayWithoutNotify(Mathf.RoundToInt(UiAudio.Get(Order[i]) * 20f) * 5);
            if (!HasVoice)
                return;
            ShowMicrophones();
            _micMode.SetWithoutNotify((int)VoiceSettings.Mode);
            _noise.SetWithoutNotify(VoiceSettings.NoiseSuppression);
            _micTest.SetWithoutNotify(VoiceSettings.HearSelf);
        }

        public override bool ShowsChanges => !HasVoice;

        public override bool AtDefaults
        {
            get
            {
                for (int i = 0; i < Order.Length; i++)
                {
                    if (Mathf.RoundToInt(UiAudio.Get(Order[i]) * 100f) != DefaultPercent(i))
                        return false;
                }

                return VoiceSettings.AtDefaults;
            }
        }

        public override void ResetToDefaults()
        {
            for (int i = 0; i < Order.Length; i++)
                UiAudio.SetVolume(Order[i], UiAudio.DefaultOf(Order[i]));
            VoiceSettings.ResetToDefaults();
            ShowSaved();
        }

        public override void Hidden()
        {
            DetachMeter();
            // The mic test ends with the tab, so nobody walks off still hearing themselves.
            VoiceSettings.HearSelf = false;
            if (HasVoice)
                _micTest.SetWithoutNotify(false);
        }

        public override void DescribeChanges(System.Collections.Generic.List<string> lines)
        {
            for (int i = 0; i < Order.Length; i++)
            {
                int now = Mathf.RoundToInt(UiAudio.Get(Order[i]) * 100f);
                int shipped = DefaultPercent(i);
                if (now != shipped)
                    lines.Add(ChangeLine(RowName(_buses[i]), now + "%", shipped + "%"));
            }
        }

        public override void Tick(float dt)
        {
            if (_meterBar == null)
                return;
            float peak = _meter != null ? UiSoundMeter.Peak : 0f;
            UiSoundMeter.Reset();
            _shown = Mathf.Max(peak, Mathf.MoveTowards(_shown, 0f, dt * 1.5f));
            Vector2 size = _meterBar.sizeDelta;
            size.x = Mathf.Round(Mathf.Clamp01(_shown) * _meterWidth);
            _meterBar.sizeDelta = size;
        }

        void OnVolume(int bus, int percent)
        {
            UiAudio.SetVolume(Order[bus], percent / 100f);
            RaiseSaved();
        }

        void OnTest() => UiSound.Play(UiSoundEvent.Notification);

        void OnMicrophone(int index)
        {
            VoiceSettings.Device = index <= 0 || index > _devices.Length ? string.Empty : _devices[index - 1];
            RaiseSaved();
        }

        void OnMicMode(int index)
        {
            VoiceSettings.Mode = (MicMode)index;
            RaiseSaved();
        }

        void OnNoise(bool on)
        {
            VoiceSettings.NoiseSuppression = on;
            RaiseSaved();
        }

        // Not saved: the test is only for while this tab is open.
        void OnMicTest(bool on) => VoiceSettings.HearSelf = on;

        void AttachMeter()
        {
            if (_meter != null)
                return;
            AudioListener listener = FindAnyObjectByType<AudioListener>();
            if (listener == null)
                return;
            _meter = listener.GetComponent<UiSoundMeter>();
            if (_meter == null)
            {
                _meter = listener.gameObject.AddComponent<UiSoundMeter>();
                _addedMeter = true;
            }

            UiSoundMeter.Reset();
        }

        void DetachMeter()
        {
            if (_addedMeter && _meter != null)
                Destroy(_meter);
            _meter = null;
            _addedMeter = false;
        }
    }
}
