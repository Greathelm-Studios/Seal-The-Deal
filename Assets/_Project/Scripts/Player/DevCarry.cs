// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-14
//  Prompt    : Alex: "i wanna be able to drop weapons and pickthem up. G to throw and F to
//              pick up ... I wanna be able to pick up the blocks too", then "Stand-in now"
//              when told Adam's pickup lane owns the real system
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : A throwaway pick-up-and-throw for testing hits on the dummy before Adam's
//              pickup system (lane 3's ItemCarrier) reaches this branch. Its own file, made
//              by itself at Play start, so it touches no prefab, scene or other lane's code
//              and is removed by deleting this file and ThrownHitTracker.cs.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.InputSystem;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// <b>Stand-in, to be deleted when Adam's pickup system arrives.</b> F picks up the loose
    /// object under the crosshair (or puts down the one held); G throws the one held. A thrown
    /// object that strikes the test dummy hits it with its own weight times its speed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ownership (AGENTS.md §3):</b> works only on the host, which owns every loose object;
    /// the host moves the object and props reach clients through their synced transforms as
    /// usual. On a client F and G do nothing. Nothing new is sent over the network.
    /// </para>
    /// <para>
    /// Made at Play start in the Editor and development builds only; a release build never
    /// has it. Reads F and G straight off the keyboard (no input actions, so no rebinding) and
    /// only while the cursor is captured, so menus are unaffected.
    /// </para>
    /// <para>
    /// Allocates only on a pick-up (the object's collider list) and a throw (one tracker
    /// component); nothing per frame.
    /// </para>
    /// <para>
    /// Alternative considered: building on the movement lane's Interact action or extending
    /// a lane's carry code. Both put stand-in code into files other people own, for a
    /// feature that is being built properly elsewhere.
    /// </para>
    /// </remarks>
    public sealed class DevCarry : MonoBehaviour
    {
        /// <summary>How far ahead an object can be picked up, metres. WHY 3: arm's reach plus a step, so a prop on a desk in front of you counts and one across the room does not.</summary>
        const float Reach = 3f;

        /// <summary>The heaviest object that can be picked up, kilograms. WHY 60: covers every office prop and block in the kit while leaving out anything the size of a person.</summary>
        const float MaxCarryMass = 60f;

        /// <summary>Where a held object hangs, metres in front of the eye. WHY 1.4: far enough that a chair does not fill the screen, near enough to read as held.</summary>
        const float HoldDistance = 1.4f;

        /// <summary>How quickly a held object is pulled to where it should hang, per second; 1/this is roughly how long it takes to catch up. WHY 15: catches up in about a fifteenth of a second, so it follows a quick turn without snapping.</summary>
        const float HoldPull = 15f;

        /// <summary>The fastest a held object is moved to follow the view, m/s. WHY 25: faster than a sprinting turn sweeps the hold point, slow enough that one caught on a wall does not jitter.</summary>
        const float MaxHoldSpeed = 25f;

        /// <summary>A held object more than this far from where it should hang, metres, has been caught on something and is let go.</summary>
        const float BreakDistance = 2.5f;

        /// <summary>
        /// How hard a throw is, newton-seconds: a light object leaves faster than a heavy one.
        /// WHY 200: a 0.5 kg tablet and an 8 kg chair both leave at the speed cap, a 40 kg
        /// desk at 5 m/s, so a chair or desk hitting the dummy lands at or above its
        /// knock-down strength (150) and a tablet only flinches it.
        /// </summary>
        const float ThrowImpulse = 200f;

        /// <summary>The fastest anything is thrown, m/s. WHY 20: a hard human throw is about 25; a little under keeps small things from vanishing.</summary>
        const float MaxThrowSpeed = 20f;

        /// <summary>How much of the throw goes up, as a fraction of forward. WHY 0.1: a slight arc so a throw at the floor ahead still carries.</summary>
        const float ThrowLift = 0.1f;

        /// <summary>The layers the pick-up ray ignores: players (the ray starts inside your own capsule) and the first-person arms. Layer numbers from the greybox tool (Player 6) and the viewmodel tool (Viewmodel 8).</summary>
        const int IgnoredLayers = (1 << 6) | (1 << 8);

        Rigidbody _held;
        Collider[] _heldShapes;
        bool _heldUsedGravity;
        Collider _playerShape;

        /// <summary>Makes the one stand-in at Play start, in the Editor and development builds only.</summary>
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

        /// <summary>Picks up the loose object under the crosshair, if there is one light enough.</summary>
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

        /// <summary>Pulls the held object toward where it should hang, by setting its speed, so it still bumps into walls instead of passing through them.</summary>
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

        /// <summary>Lets go of the held object at a given speed.</summary>
        /// <param name="velocity">Its speed on leaving, m/s, world space.</param>
        /// <param name="thrown">True for a throw: it then hits the dummy if it strikes one.</param>
        void Let(Vector3 velocity, bool thrown)
        {
            Rigidbody body = _held;
            _held = null;
            if (body == null)
                return;
            body.useGravity = _heldUsedGravity;
            body.linearVelocity = velocity;
            // The tracker gives the player's capsule back its collisions once the object is
            // clear, and reports a throw's hit on the dummy.
            ThrownHitTracker.Arm(body, _playerShape, _heldShapes, thrown);
            _heldShapes = null;
        }

        /// <summary>Stops or restores collisions between the held object and the player's own capsule, so carrying it does not shove the player.</summary>
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
