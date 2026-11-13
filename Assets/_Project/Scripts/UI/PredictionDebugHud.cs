using CloseTheDeal.Net;
using CloseTheDeal.Player;
using TMPro;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// Greybox readout for the local player's body: state, speed, how far the host's last
    /// correction moved it, and ping. Exists so the first network tests can tell "feels
    /// wrong" from "is wrong". Rebuilds its text at most ten times a second, and only when
    /// a value changed.
    /// </summary>
    public sealed class PredictionDebugHud : MonoBehaviour
    {
        struct Sample
        {
            public bool Present;
            public MoveState State;
            public int SpeedTenths;
            public int CorrectionCm;
            public int Corrections;
            public int PingMs;
            public bool SwitchboardActive;
            public bool SwitchboardIsHost;
            public int PropBytesPerSecond;
            public int PropBodiesPerSecond;
            public int MovingBodies;
            public int SmoothingDelayMs;

            public bool SameAs(in Sample other)
            {
                return Present == other.Present
                    && State == other.State
                    && SpeedTenths == other.SpeedTenths
                    && CorrectionCm == other.CorrectionCm
                    && Corrections == other.Corrections
                    && PingMs == other.PingMs
                    && SwitchboardActive == other.SwitchboardActive
                    && SwitchboardIsHost == other.SwitchboardIsHost
                    && PropBytesPerSecond == other.PropBytesPerSecond
                    && PropBodiesPerSecond == other.PropBodiesPerSecond
                    && MovingBodies == other.MovingBodies
                    && SmoothingDelayMs == other.SmoothingDelayMs;
            }
        }

        [SerializeField] TMP_Text _text;

        [Tooltip("How often the readout refreshes, in seconds. It only rebuilds the text when a value changed.")]
        [SerializeField] float _interval = 0.1f;

        float _nextTime;
        Sample _shown;

        void Update()
        {
            if (Time.unscaledTime < _nextTime)
                return;
            _nextTime = Time.unscaledTime + _interval;

            Sample now = Read(PlayerMotor.Local);
            if (now.SameAs(_shown))
                return;

            _shown = now;
            _text.text = Render(now);
        }

        static Sample Read(PlayerMotor motor)
        {
            Sample sample = default;
            sample.SwitchboardActive = SwitchboardStats.Active;
            if (sample.SwitchboardActive)
            {
                sample.SwitchboardIsHost = SwitchboardStats.IsHost;
                sample.PropBytesPerSecond = SwitchboardStats.BytesPerSecond;
                sample.PropBodiesPerSecond = SwitchboardStats.BodiesPerSecond;
                if (sample.SwitchboardIsHost)
                    sample.MovingBodies = SwitchboardStats.MovingBodies;
                else
                    sample.SmoothingDelayMs = SwitchboardStats.DelayMs;
            }

            if (motor != null)
            {
                sample.Present = true;
                sample.State = motor.State;
                sample.SpeedTenths = Mathf.RoundToInt(motor.PlanarSpeed * 10f);
                sample.CorrectionCm = Mathf.RoundToInt(motor.LastCorrectionMetres * 100f);
                sample.Corrections = motor.CorrectionCount;
                sample.PingMs = (int)motor.TimeManager.RoundTripTime;
            }

            return sample;
        }

        static string Render(in Sample s)
        {
            string player = s.Present
                ? $"{s.State}  ·  {s.SpeedTenths / 10f:0.0} m/s  ·  ping {s.PingMs} ms\n"
                    + $"Host corrections {s.Corrections}  ·  last {s.CorrectionCm / 100f:0.00} m"
                : string.Empty;

            if (!s.SwitchboardActive)
                return player;

            string props = s.SwitchboardIsHost
                ? $"Props out {s.PropBytesPerSecond / 1000f:0.0} kB/s  ·  {s.PropBodiesPerSecond} updates/s  ·  {s.MovingBodies} moving"
                : $"Props in {s.PropBytesPerSecond / 1000f:0.0} kB/s  ·  {s.PropBodiesPerSecond} updates/s  ·  smoothing {s.SmoothingDelayMs} ms";
            return s.Present ? player + "\n" + props : props;
        }
    }
}
