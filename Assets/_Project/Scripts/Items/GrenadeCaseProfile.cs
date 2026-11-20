using CloseTheDeal.Combat;
using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>Designer-owned tuning for the grenade case. Runtime only reads it (AGENTS.md §4).</summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Items/Grenade Case Profile", fileName = "GrenadeCase")]
    public sealed class GrenadeCaseProfile : ScriptableObject
    {
        [Header("Fuse")]
        [Tooltip("How long from arming to the explosion, in seconds. It goes off wherever it is by then, the armer's hands included. Shorter is harder to throw back; longer is easier to dodge.")]
        [Min(0.5f)] public float FuseSeconds = 3.5f;

        [Tooltip("Gap between beeps just after arming, in seconds.")]
        [Min(0.02f)] public float SlowBeepSeconds = 0.6f;

        [Tooltip("Gap between beeps just before it goes off, in seconds. The beeps speed up smoothly from the slow gap to this.")]
        [Min(0.02f)] public float FastBeepSeconds = 0.08f;

        [Header("Explosion")]
        [Tooltip("How the explosion throws players and furniture: its radius and speeds. The same kind of asset rockets use.")]
        public BlastProfile Blast;

        [Tooltip("Hit points taken from anything at the centre of the explosion.")]
        [Min(0f)] public float Damage = 80f;

        [Tooltip("How much of the damage something at the very edge of the blast radius still takes, as a fraction. 1 = full damage everywhere inside.")]
        [Range(0f, 1f)] public float EdgeDamage = 0.25f;

        [Header("Look")]
        [Tooltip("How long the flash of the explosion lasts, in seconds.")]
        [Min(0.05f)] public float FlashSeconds = 0.3f;
    }
}
