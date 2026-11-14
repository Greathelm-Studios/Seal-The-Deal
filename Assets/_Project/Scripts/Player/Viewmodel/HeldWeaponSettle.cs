// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-14
//  Prompt    : Harvey: "have the actual playermodel follow the first person view model"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Modified  : Claude Fable 5.2, 2026-11-14 — the second pass now only brings the arm part
//              of the way back (Harvey: the wrist "just floats around static while the rest
//              of the arm flops").
//  Summary   : Runs PlayerHeldWeapon's arm hold a second time each frame, after the ragdoll
//              has shown its bodies, so the body's gun is steadied toward the aim without
//              being pinned to it.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// The after-the-ragdoll half of <see cref="PlayerHeldWeapon"/>. Added by it at spawn;
    /// never put on a prefab by hand.
    /// </summary>
    /// <remarks>
    /// WHY a second component and not a second call: Unity runs each component once per
    /// frame at its own place in the order, and the hold has to happen on both sides of
    /// <see cref="PlayerRagdoll"/> (before, so the ragdoll chases it; after, so the gun is
    /// steadied). Local to each client and synced by nothing (AGENTS.md §3).
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(SettleOrder)]
    public sealed class HeldWeaponSettle : MonoBehaviour
    {
        /// <summary>After PlayerRagdoll (110), which overwrites the arm with where its bodies are.</summary>
        const int SettleOrder = 115;

        PlayerHeldWeapon _held;

        /// <summary>Says whose arm to settle. Called once by <see cref="PlayerHeldWeapon"/> when it adds this.</summary>
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
