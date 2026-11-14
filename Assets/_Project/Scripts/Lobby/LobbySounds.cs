using CloseTheDeal.Customisation;
using CloseTheDeal.UI;
using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// Gives the reception's moments their UI sounds from the shared sound set (UiSound): looking
    /// at a desk or stand, using it, readying, signing in, a refusal, the short-handed confirm,
    /// the start, and stepping into and out of the fitting room. The kit's own controls already
    /// sound for themselves; this covers what is not a kit control. Local only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbySounds : MonoBehaviour
    {
        LobbyRoster _roster;
        LobbyInteractor _interactor;

        void OnEnable()
        {
            ReceptionDesk.OnConfirmArmed += OnConfirmArmed;
            FittingRoom.OnEntered += OnFittingEntered;
            FittingRoom.OnExited += OnFittingExited;
        }

        void OnDisable()
        {
            ReceptionDesk.OnConfirmArmed -= OnConfirmArmed;
            FittingRoom.OnEntered -= OnFittingEntered;
            FittingRoom.OnExited -= OnFittingExited;
            Unhook();
        }

        void Update()
        {
            if (_interactor == null && LobbyInteractor.Instance != null)
            {
                _interactor = LobbyInteractor.Instance;
                _interactor.OnTargetChanged += OnTargetChanged;
                _interactor.OnUsed += OnUsed;
            }

            LobbyRoster roster = LobbyRoster.Instance;
            if (roster == _roster)
                return;
            UnhookRoster();
            _roster = roster;
            if (_roster == null)
                return;
            _roster.OnReadyChanged += OnReadyChanged;
            _roster.OnFirmChosen += OnFirmChosen;
            _roster.OnFirmRefused += OnRefused;
            _roster.OnStartBlocked += OnStartBlocked;
            _roster.OnRunStarting += OnRunStarting;
        }

        void Unhook()
        {
            UnhookRoster();
            if (_interactor != null)
            {
                _interactor.OnTargetChanged -= OnTargetChanged;
                _interactor.OnUsed -= OnUsed;
                _interactor = null;
            }
        }

        void UnhookRoster()
        {
            if (_roster == null)
                return;
            _roster.OnReadyChanged -= OnReadyChanged;
            _roster.OnFirmChosen -= OnFirmChosen;
            _roster.OnFirmRefused -= OnRefused;
            _roster.OnStartBlocked -= OnStartBlocked;
            _roster.OnRunStarting -= OnRunStarting;
            _roster = null;
        }

        void OnTargetChanged(LobbyInteractable target)
        {
            if (target != null)
                UiSound.Play(UiSoundEvent.Focus);
        }

        void OnUsed(LobbyInteractable target) => UiSound.Play(UiSoundEvent.Select);

        /// <summary>Only this player's own ready sounds; others' show on the board.</summary>
        void OnReadyChanged(LobbyMember member, bool ready)
        {
            if (_roster.TryGetLocal(out LobbyMember me) && me.ClientId == member.ClientId)
                UiSound.Play(ready ? UiSoundEvent.LobbyReady : UiSoundEvent.LobbyUnready);
        }

        void OnFirmChosen(int firm) => UiSound.Play(UiSoundEvent.Confirm);
        void OnRefused(int firm) => UiSound.Play(UiSoundEvent.Error);
        void OnStartBlocked(StartBlock block) => UiSound.Play(UiSoundEvent.Error);
        void OnConfirmArmed(StartBlock block) => UiSound.Play(UiSoundEvent.Notification);
        void OnRunStarting() => UiSound.Play(UiSoundEvent.Confirm);
        void OnFittingEntered() => UiSound.Play(UiSoundEvent.Transition);
        void OnFittingExited(bool kept) => UiSound.Play(kept ? UiSoundEvent.Confirm : UiSoundEvent.Back);
    }
}
