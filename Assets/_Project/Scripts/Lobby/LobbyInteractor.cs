using System;
using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>Local ray-aim selector feeding interact press/hold/release to reception interactables. Design: docs/systems/LOBBY.md.</summary>
    [DisallowMultipleComponent]
    public sealed class LobbyInteractor : MonoBehaviour
    {
        public static LobbyInteractor Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        /// <summary>The thing in reach changed (null = nothing). UI and sound listen.</summary>
        public event Action<LobbyInteractable> OnTargetChanged;

        /// <summary>The interact button went down on a target. For a click sound or a button flash.</summary>
        public event Action<LobbyInteractable> OnUsed;

        [Tooltip("Reach and aim rules.")]
        [SerializeField] LobbyRules _rules;

        LobbyInteractable _target;
        bool _holding;
        float _held;
        int _blockers;

        public LobbyInteractable Target => _target;

        /// <summary>True while something has taken over the controls.</summary>
        public bool Blocked => _blockers > 0;

        void Awake()
        {
            Instance = this;
            LobbyInput.Enable();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Something (the fitting room) takes over the controls until Unblock. Counted, so two takers can't clash.</summary>
        public void Block()
        {
            _blockers++;
            SetTarget(null);
        }

        public void Unblock() => _blockers = Mathf.Max(0, _blockers - 1);

        void Update()
        {
            SetTarget(CanLook() ? FindTarget() : null);
            if (_target == null)
                return;

            if (LobbyInput.Interact.WasPressedThisFrame())
            {
                _holding = true;
                _held = 0f;
                _target.OnPress();
                OnUsed?.Invoke(_target);
            }

            if (_target == null)
                return; // The press may have handed the controls elsewhere.

            if (_holding && LobbyInput.Interact.IsPressed())
            {
                _held += Time.deltaTime;
                _target.OnHold(_held);
            }
            else if (_holding)
            {
                _holding = false;
                _target.OnRelease(_held);
            }
        }

        bool CanLook()
        {
            return _blockers == 0
                && PlayerMotor.Local != null
                && PlayerCamera.Instance != null
                && LobbyInput.Captured;
        }

        LobbyInteractable FindTarget()
        {
            Transform eye = PlayerCamera.Instance.transform;
            Vector3 origin = eye.position;
            Vector3 forward = eye.forward;
            float range = _rules.InteractRange;

            LobbyInteractable best = null;
            float bestScore = _rules.InteractAim;
            var all = LobbyInteractable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Vector3 to = all[i].FocusPoint - origin;
                float distance = to.magnitude;
                if (distance > range || distance < 0.001f)
                    continue;

                float aim = Vector3.Dot(forward, to / distance);
                if (aim > bestScore)
                {
                    bestScore = aim;
                    best = all[i];
                }
            }

            return best;
        }

        void SetTarget(LobbyInteractable next)
        {
            if (next == _target)
                return;

            if (_target != null && _holding)
                _target.OnLost();

            _holding = false;
            _held = 0f;
            _target = next;
            OnTargetChanged?.Invoke(next);
        }
    }
}
