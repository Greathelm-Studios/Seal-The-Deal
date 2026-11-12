using UnityEngine;

namespace CloseTheDeal.Core
{
    /// <summary>Anything the host can hurt. Only the host calls this.</summary>
    public interface IDamageable
    {
        /// <summary>Host only. amount is hit points; point and direction are world space; attackerClientId is -1 for the world.</summary>
        void ApplyDamage(float amount, Vector3 point, Vector3 direction, int attackerClientId);
    }
}
