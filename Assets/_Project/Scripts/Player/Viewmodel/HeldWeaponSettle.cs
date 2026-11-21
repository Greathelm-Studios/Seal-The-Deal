using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>Post-ragdoll half of <see cref="PlayerHeldWeapon"/>; added at spawn so the hold brackets <see cref="PlayerRagdoll"/>.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(SettleOrder)]
    public sealed class HeldWeaponSettle : MonoBehaviour
    {
        // Runs after PlayerRagdoll (110) writes body transforms back to the skeleton.
        const int SettleOrder = 115;

        PlayerHeldWeapon _held;

        internal void Follow(PlayerHeldWeapon held)
        {
            _held = held;
        }

        void LateUpdate()
        {
            if (_held != null && _held.isActiveAndEnabled)
                _held.Hold(true);
        }
    }
}
