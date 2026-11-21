using UnityEngine;
using UnityEngine.InputSystem;

namespace CloseTheDeal.Player
{
    /// <summary>Temporary stand-in pickup/throw (F/G, host-only, Editor/Dev builds) for testing dummy hits until ItemCarrier lands.</summary>
    public sealed class DevCarry : MonoBehaviour
    {
        const float Reach = 3f;
        const float MaxCarryMass = 60f;
        const float HoldDistance = 1.4f;
        const float HoldPull = 15f;
        const float MaxHoldSpeed = 25f;
        const float BreakDistance = 2.5f;
        const float ThrowImpulse = 200f;
        const float MaxThrowSpeed = 20f;
        const float ThrowLift = 0.1f;
        // Ignores Player (6) and Viewmodel (8) layers.
        const int IgnoredLayers = (1 << 6) | (1 << 8);

        Rigidbody _held;
        Collider[] _heldShapes;
        bool _heldUsedGravity;
        Collider _playerShape;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var go = new GameObject(nameof(DevCarry));
            DontDestroyOnLoad(go);
            go.AddComponent<DevCarry>();
#endif
        }

        void Update()
        {
            PlayerMotor player = PlayerMotor.Local;
            PlayerCamera view = PlayerCamera.Instance;
            if (player == null || view == null || !player.IsServerStarted)
            {
                if (_held != null)
                    Let(Vector3.zero, false);
                return;
            }

            Keyboard keys = Keyboard.current;
            if (keys == null || Cursor.lockState != CursorLockMode.Locked)
                return;

            Transform eye = view.transform;
            if (keys.fKey.wasPressedThisFrame)
            {
                if (_held != null)
                    Let(_held.linearVelocity, false);
                else
                    TryPickUp(eye, player);
            }
            else if (keys.gKey.wasPressedThisFrame && _held != null)
            {
                float speed = Mathf.Min(MaxThrowSpeed, ThrowImpulse / _held.mass);
                Let((eye.forward + Vector3.up * ThrowLift).normalized * speed, true);
            }

            if (_held != null)
                Hold(eye);
        }

        void TryPickUp(Transform eye, PlayerMotor player)
        {
            if (!Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, Reach, ~IgnoredLayers, QueryTriggerInteraction.Ignore))
                return;
            Rigidbody body = hit.rigidbody;
            if (body == null || body.isKinematic || body.mass > MaxCarryMass || body.GetComponentInParent<PlayerMotor>() != null)
                return;

            _held = body;
            _heldUsedGravity = body.useGravity;
            body.useGravity = false;
            _heldShapes = body.GetComponentsInChildren<Collider>();
            _playerShape = player.GetComponent<Collider>();
            SetTouchesPlayer(false);
        }

        void Hold(Transform eye)
        {
            Vector3 target = eye.position + eye.forward * HoldDistance;
            Vector3 off = target - _held.position;
            if (off.sqrMagnitude > BreakDistance * BreakDistance)
            {
                Let(Vector3.zero, false);
                return;
            }
            _held.linearVelocity = Vector3.ClampMagnitude(off * HoldPull, MaxHoldSpeed);
            _held.angularVelocity = Vector3.zero;
        }

        void Let(Vector3 velocity, bool thrown)
        {
            Rigidbody body = _held;
            _held = null;
            if (body == null)
                return;
            body.useGravity = _heldUsedGravity;
            body.linearVelocity = velocity;
            ThrownHitTracker.Arm(body, _playerShape, _heldShapes, thrown);
            _heldShapes = null;
        }

        void SetTouchesPlayer(bool touches)
        {
            if (_playerShape == null || _heldShapes == null)
                return;
            for (int i = 0; i < _heldShapes.Length; i++)
            {
                if (_heldShapes[i] != null)
                    Physics.IgnoreCollision(_playerShape, _heldShapes[i], !touches);
            }
        }
    }
}
