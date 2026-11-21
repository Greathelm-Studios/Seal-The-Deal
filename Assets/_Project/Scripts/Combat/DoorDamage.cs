using System.Collections.Generic;
using CloseTheDeal.Core;
using CloseTheDeal.Tower;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>Host-only IDamageable adapter that converts accumulated bullet damage into BreakableDoor blasts.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BreakableDoor))]
    public sealed class DoorDamage : MonoBehaviour, IDamageable
    {
        [Tooltip("How much gun damage counts as one blast on this door. The door breaks after its own number of blasts (3 by default), so 100 here means 300 damage: twelve pistol shots at close range.")]
        [Min(1f)] [SerializeField] float _damagePerBlast = 100f;

        static readonly Dictionary<Collider, DoorDamage> ByCollider = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ByCollider.Clear();
        }

        BreakableDoor _door;
        Collider[] _colliders;
        float _banked;

        public bool IsBroken => _door != null && _door.IsBroken;

        public static bool TryFind(Collider collider, out DoorDamage door)
        {
            return ByCollider.TryGetValue(collider, out door);
        }

        void Awake()
        {
            _door = GetComponent<BreakableDoor>();
            _colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider c = _colliders[i];
                if (c != null)
                    ByCollider[c] = this;
            }
        }

        void OnDestroy()
        {
            if (_colliders == null)
                return;

            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider c = _colliders[i];
                if (c != null && ByCollider.TryGetValue(c, out DoorDamage owner) && owner == this)
                    ByCollider.Remove(c);
            }
        }

        void OnEnable()
        {
            // A pooled door comes back whole, so its half-landed damage goes too.
            _banked = 0f;
        }

        public void ApplyDamage(float amount, Vector3 point, Vector3 direction, int attackerClientId)
        {
            if (!_door.IsServerStarted || _door.IsBroken)
                return;

            _banked += amount;
            while (_banked >= _damagePerBlast && !_door.IsBroken)
            {
                _banked -= _damagePerBlast;
                _door.TakeBlast();
            }
        }
    }
}
