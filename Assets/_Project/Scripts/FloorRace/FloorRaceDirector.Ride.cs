using CloseTheDeal.Audio;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.FloorRace
{
    // What the lift ride feels like to a player aboard. Local to each machine: nothing here is synced or read by the host's rules.
    public sealed partial class FloorRaceDirector
    {
        const float ShakeHz = 17f, DepartJolt = 0.45f, ArriveJolt = 0.6f;

        bool _aboard, _rideOn;
        Transform _shaftLights;

        void Update()
        {
            if (_floor == null)
                return;

            FloorRaceState state = _state.Value;
            if (state.Stage == RaceStage.Departing)
            {
                FirmMember local = FirmMember.Local;
                _aboard = local != null && local.Firm == state.LiftFirm && InExitLift(local.Position);
            }
            else if (state.Stage == RaceStage.Playing || state.Stage == RaceStage.Idle)
            {
                _aboard = false;
            }

            bool ride = _aboard && state.Stage == RaceStage.Building && _oldFloor != null;
            if (ride != _rideOn)
            {
                _rideOn = ride;
                if (ride) BeginRide();
                else EndRide(state.Stage == RaceStage.HeadStart);
            }

            if (!ride)
                return;

            float along = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(StageSeconds / RideLength(state)));
            if (_shaftLights != null)
                _shaftLights.localPosition = new Vector3(0f, -along * _builder.FloorPitch, 0f);
            PlayerCamera view = PlayerCamera.Instance;
            if (view != null)
                view.Nudge(Vector3.up * ((Mathf.PerlinNoise(Time.time * ShakeHz, 0.37f) - 0.5f) * _builder.Kit.RideShake * Time.deltaTime));
        }

        void BeginRide()
        {
            FloorKit kit = _builder.Kit;
            SoundSystem.PlayFlat(kit.LiftRide, null, kit.RideVolume);
            _shaftLights = _builder.AddShaftLights(_oldFloor, _oldFloor.Layout.ExitLift);
            PlayerCamera view = PlayerCamera.Instance;
            if (view != null)
                view.Nudge(Vector3.down * DepartJolt);
        }

        // The lights belong to the floor that was left and go when it is torn down.
        void EndRide(bool arrived)
        {
            _shaftLights = null;
            if (!arrived)
                return;

            FloorKit kit = _builder.Kit;
            SoundSystem.PlayFlat(kit.LiftArrive, null, kit.RideVolume);
            PlayerCamera view = PlayerCamera.Instance;
            if (view != null)
                view.Nudge(Vector3.up * ArriveJolt);
        }
    }
}
