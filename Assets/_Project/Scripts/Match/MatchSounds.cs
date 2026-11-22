using CloseTheDeal.UI;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>Plays match UI audio events on each player's machine via UiSound. Design: docs/systems/MATCH.md.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MatchDirector))]
    public sealed class MatchSounds : MonoBehaviour
    {
        MatchDirector _match;

        void Awake()
        {
            _match = GetComponent<MatchDirector>();
        }

        void OnEnable()
        {
            _match.OnCountdownChanged += OnCountdown;
            _match.OnPhaseChanged += OnPhase;
            _match.OnBriefcasePickedUp += OnPickedUp;
            _match.OnBriefcaseDropped += OnDropped;
            _match.OnMatchEnded += OnEnded;
            FirmMember.OnNotice += OnNotice;
        }

        void OnDisable()
        {
            _match.OnCountdownChanged -= OnCountdown;
            _match.OnPhaseChanged -= OnPhase;
            _match.OnBriefcasePickedUp -= OnPickedUp;
            _match.OnBriefcaseDropped -= OnDropped;
            _match.OnMatchEnded -= OnEnded;
            FirmMember.OnNotice -= OnNotice;
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

        void OnPickedUp(int firm) => UiSound.Play(UiSoundEvent.BriefcaseTaken);
        void OnDropped(int firm) => UiSound.Play(UiSoundEvent.BriefcaseDropped);

        void OnEnded(MatchSnapshot result)
        {
            FirmMember local = FirmMember.Local;
            bool won = local != null && result.Winner == local.Firm;
            UiSound.Play(won ? UiSoundEvent.MatchWon : UiSoundEvent.MatchLost);
        }

        void OnNotice(MatchNotice notice)
        {
            if (notice == MatchNotice.FellOut)
                UiSound.Play(UiSoundEvent.Notification);
        }
    }
}
