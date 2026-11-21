using CloseTheDeal.UI;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>Plays match UI audio events on each player's machine via UiSound. Design: docs/systems/MATCH.md.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MatchDirector))]
    public sealed class MatchSounds : MonoBehaviour
    {
        [Tooltip("Shortest gap between two refusal sounds, seconds. Walking into a gate and being told it is shut arrive close together; this keeps them one sound.")]
        [Min(0f)] [SerializeField] float _refusalGap = 0.6f;

        MatchDirector _match;
        float _lastRefusal = -10f;

        void Awake()
        {
            _match = GetComponent<MatchDirector>();
        }

        void OnEnable()
        {
            _match.OnCountdownChanged += OnCountdown;
            _match.OnPhaseChanged += OnPhase;
            _match.OnFloorOpened += OnFloorOpened;
            _match.OnBriefcasePickedUp += OnPickedUp;
            _match.OnBriefcaseDropped += OnDropped;
            _match.OnCapWarning += OnWarning;
            _match.OnCheckpointSaved += OnCheckpoint;
            _match.OnMatchEnded += OnEnded;
            FirmMember.OnNotice += OnNotice;
            MatchGates.OnGateTouched += OnGateTouched;
        }

        void OnDisable()
        {
            _match.OnCountdownChanged -= OnCountdown;
            _match.OnPhaseChanged -= OnPhase;
            _match.OnFloorOpened -= OnFloorOpened;
            _match.OnBriefcasePickedUp -= OnPickedUp;
            _match.OnBriefcaseDropped -= OnDropped;
            _match.OnCapWarning -= OnWarning;
            _match.OnCheckpointSaved -= OnCheckpoint;
            _match.OnMatchEnded -= OnEnded;
            FirmMember.OnNotice -= OnNotice;
            MatchGates.OnGateTouched -= OnGateTouched;
        }

        void OnCountdown(int secondsLeft)
        {
            if (secondsLeft > 0)
                UiSound.Play(UiSoundEvent.CountdownTick);
        }

        void OnPhase(MatchPhase previous, MatchPhase next)
        {
            if (next == MatchPhase.Running)
                UiSound.Play(UiSoundEvent.CountdownGo);
        }

        void OnFloorOpened(int floor, int byFirm) => UiSound.Play(UiSoundEvent.FloorOpened);
        void OnPickedUp(int firm) => UiSound.Play(UiSoundEvent.BriefcaseTaken);
        void OnDropped(int firm) => UiSound.Play(UiSoundEvent.BriefcaseDropped);
        void OnWarning(int secondsLeft) => UiSound.Play(UiSoundEvent.Notification);

        void OnCheckpoint(int firm, int floor)
        {
            FirmMember local = FirmMember.Local;
            if (local != null && local.Firm == firm)
                UiSound.Play(UiSoundEvent.Notification);
        }

        void OnEnded(MatchSnapshot result)
        {
            FirmMember local = FirmMember.Local;
            bool won = local != null && result.Winner == local.Firm;
            UiSound.Play(won ? UiSoundEvent.MatchWon : UiSoundEvent.MatchLost);
        }

        void OnNotice(MatchNotice notice)
        {
            switch (notice)
            {
                case MatchNotice.GateRefused:
                case MatchNotice.FirstGateRefusal:
                case MatchNotice.RoofNeedsBriefcase:
                    Refuse();
                    break;
                case MatchNotice.FellOut:
                    UiSound.Play(UiSoundEvent.Notification);
                    break;
            }
        }

        void OnGateTouched(int floor) => Refuse();

        void Refuse()
        {
            if (Time.unscaledTime - _lastRefusal < _refusalGap)
                return;

            _lastRefusal = Time.unscaledTime;
            UiSound.Play(UiSoundEvent.Error);
        }
    }
}
