using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Props
{
    /// <summary>
    /// A loose piece of furniture. The host simulates it; everyone else holds it kinematic
    /// and follows the host's positions through NetworkTransform. While a player carries it
    /// (Items/Pickup) or it has been taken out of play, the host holds it still as well.
    /// Design: docs/systems/PROPS.md.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Prop : NetworkBehaviour
    {
        [Tooltip("How much of a blast's throw this prop resists, as a fraction. 0 = flies like a chair; 0.7 = a heavy desk that mostly shudders.")]
        [Range(0f, 0.95f)] [SerializeField] float _throwResistance;

        Rigidbody _rigidbody;
        bool _heldStill;

        public float ThrowResistance => _throwResistance;

        /// <summary>The prop's physics body. Only the host may push it.</summary>
        public Rigidbody Body => _rigidbody;

        /// <summary>True while something other than physics is in charge of it: carried in a player's hands, or out of play.</summary>
        public bool HeldStill => _heldStill;

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            ApplyBodyMode();
        }

        /// <summary>
        /// Takes the prop away from physics (true) or hands it back (false). Safe to call on
        /// any machine: only the host ever simulates it, so elsewhere it stays kinematic.
        /// </summary>
        public void SetHeldStill(bool heldStill)
        {
            _heldStill = heldStill;
            ApplyBodyMode();
        }

        /// <summary>Host only. Adds a velocity change so the throw reads the same whatever the mass. Ignored while carried or out of play.</summary>
        public void Throw(Vector3 velocityChange)
        {
            if (!IsServerStarted || _heldStill)
                return;

            _rigidbody.AddForce(velocityChange * (1f - _throwResistance), ForceMode.VelocityChange);
        }

        /// <summary>Only the host runs prop physics; clients are moved by the synced transform.</summary>
        void ApplyBodyMode()
        {
            _rigidbody.isKinematic = !IsServerStarted || _heldStill;
        }
    }
}
