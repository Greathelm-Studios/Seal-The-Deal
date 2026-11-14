using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// Settings › Audio: one slider per mixer bus (Master, Music, Effects, Voice, Interface) through the sound lane's
    /// UiAudio, a Play test sound button and a live output meter in the right column (the sound lane's UiSoundMeter
    /// on the listener, added only while this tab shows).
    /// </summary>
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

        static readonly AudioBus[] Order = { AudioBus.Master, AudioBus.Music, AudioBus.Sfx, AudioBus.Voice, AudioBus.Ui };

        /// <summary>The row's own name, as the player reads it beside the slider.</summary>
        static string RowName(Component control)
        {
            var label = control.transform.parent != null ? control.transform.parent.Find("Name") : null;
            return label != null && label.TryGetComponent(out TMPro.TMP_Text text) ? text.text : control.name;
        }

        /// <summary>The sound lane's shipped level for a bus, in percent.</summary>
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

            _test.Clicked += OnTest;
            AttachMeter();
        }

        void OnDisable()
        {
            for (int i = 0; i < _buses.Length; i++)
                _buses[i].Changed -= _handlers[i];
            _test.Clicked -= OnTest;
            DetachMeter();
        }

        public override void ShowSaved()
        {
            for (int i = 0; i < _buses.Length; i++)
                _buses[i].SetDisplayWithoutNotify(Mathf.RoundToInt(UiAudio.Get(Order[i]) * 20f) * 5);
        }

        public override bool AtDefaults
        {
            get
            {
                for (int i = 0; i < Order.Length; i++)
                {
                    if (Mathf.RoundToInt(UiAudio.Get(Order[i]) * 100f) != DefaultPercent(i))
                        return false;
                }

                return true;
            }
        }

        public override void ResetToDefaults()
        {
            for (int i = 0; i < Order.Length; i++)
                UiAudio.SetVolume(Order[i], UiAudio.DefaultOf(Order[i]));
            ShowSaved();
        }

        public override void Hidden() => DetachMeter();

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
