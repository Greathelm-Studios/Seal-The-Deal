using UnityEngine;

namespace CloseTheDeal.Glass
{
    /// <summary>
    /// Designer-owned tuning for breakable glass. Runtime only reads it (AGENTS.md §4).
    /// Design and ownership: docs/systems/GLASS.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Glass Profile", fileName = "GlassProfile")]
    public sealed class GlassProfile : ScriptableObject
    {
        [Header("What counts as glass")]
        [Tooltip("Anything drawn with one of these materials, with its collider on the same object, is breakable glass without needing a Glass Pane added by hand. This is how the greybox stair cages and glass rooms break.")]
        public Material[] GlassMaterials = new Material[0];

        [Header("Breaking by impact")]
        [Tooltip("How fast something must hit a pane head-on to go through it, in metres per second. Players sprint at 9, so 10 means a run never breaks a window but a body thrown by a blast or a killing shot does.")]
        [Min(0.1f)] public float BreakSpeed = 10f;

        [Tooltip("The lightest thing that can break a pane by hitting it, in kilograms. Keeps a flying stapler from doing it; a chair or a player always can.")]
        [Min(0f)] public float MinBreakMass = 3f;

        [Tooltip("How much of its speed a body keeps after going through a pane, as a fraction. 1 = the glass might as well not be there; 0.5 = it nearly stops them.")]
        [Range(0f, 1f)] public float SpeedKept = 0.85f;

        [Tooltip("How long a player who went through a pane can't steer, in seconds.")]
        [Min(0f)] public float ThroughControlLossSeconds = 0.2f;

        [Header("Shards (looks only)")]
        [Tooltip("The burst of shards played where a pane breaks. Each player's game makes its own; they are never sent over the network. Leave empty and a pane just vanishes.")]
        public ParticleSystem ShardEffect;

        [Tooltip("Shards per square metre of pane.")]
        [Range(1f, 60f)] public float ShardsPerSquareMetre = 14f;

        [Tooltip("The most shards one pane can throw, however big it is. Keeps a wall of glass from costing a frame.")]
        [Range(4, 300)] public int MaxShards = 80;

        [Tooltip("How fast the shards fly away from what broke the pane, in metres per second.")]
        [Min(0f)] public float ShardSpeed = 3.5f;
    }
}
