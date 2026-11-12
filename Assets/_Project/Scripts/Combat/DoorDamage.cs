using CloseTheDeal.Core;
using CloseTheDeal.Tower;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Lets guns wear down a locked door. The door itself only counts blasts, so this adds
    /// up gun damage and hands the door one blast's worth each time enough has landed.
    /// Host only, like the door's own count.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BreakableDoor))]
    public sealed class DoorDamage : MonoBehaviour, IDamageable
    {
        [Tooltip("How much gun damage counts as one blast on this door. The door breaks after its own number of blasts (3 by default), so 100 here means 300 damage: twelve pistol shots at close range.")]
        [Min(1f)] [SerializeField] float _damagePerBlast = 100f;

        BreakableDoor _door;
        float _banked;

        void Awake()
        {
            _door = GetComponent<BreakableDoor>();
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
