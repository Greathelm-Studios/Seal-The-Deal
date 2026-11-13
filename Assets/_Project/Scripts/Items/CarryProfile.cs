using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>
    /// Designer-owned tuning for picking things up, carrying, throwing and what a thrown
    /// object does when it lands. Runtime only reads it (AGENTS.md §4).
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Items/Carry Profile", fileName = "CarryProfile")]
    public sealed class CarryProfile : ScriptableObject
    {
        [Header("Reaching")]
        [Tooltip("How far away a player can pick something up, in metres from the eye. 2.5 reaches across a desk; higher grabs things from further off.")]
        [Min(0.5f)] public float Reach = 2.5f;

        [Tooltip("How forgiving the aim is when looking at an item, in metres. 0 needs the crosshair exactly on it; 0.15 picks up small things without fuss.")]
        [Min(0f)] public float LookRadius = 0.15f;

        [Tooltip("Extra reach the host allows a joining player, in metres, because their view runs slightly ahead of the host's. Too low refuses fair grabs on a slow connection; too high lets them grab from further than Reach.")]
        [Min(0f)] public float ReachTolerance = 0.75f;

        [Header("Weight")]
        [Tooltip("Heaviest thing a player can lift, in kilograms. A chair is 8, a filing cabinet 30, a desk 40. Anything heavier shows \"Too heavy\".")]
        [Min(1f)] public float MaxCarryWeight = 35f;

        [Tooltip("Anything this light or lighter does not slow the carrier at all, in kilograms.")]
        [Min(0f)] public float FreeCarryWeight = 3f;

        [Tooltip("Walking and sprinting speed while carrying the heaviest liftable thing, as a fraction of full speed. Weights in between slow proportionally. 0.45 is a visible trudge; 1 never slows.")]
        [Range(0.1f, 1f)] public float SpeedAtMaxWeight = 0.45f;

        [Tooltip("Jump height while carrying the heaviest liftable thing, as a fraction of the full jump. 0.5 still clears a chair but not a desk.")]
        [Range(0.1f, 1f)] public float JumpAtMaxWeight = 0.5f;

        [Header("Letting go")]
        [Tooltip("How much of the carrier's own speed a dropped or thrown thing keeps, as a fraction. 1 = a running throw goes further.")]
        [Range(0f, 1f)] public float InheritVelocity = 1f;

        [Tooltip("How long a thing just let go cannot touch the player who let go of it, in seconds, so a throw never trips over its own thrower. Too short and big furniture snags on the way out.")]
        [Min(0f)] public float ThrowerIgnoreSeconds = 0.35f;

        [Tooltip("Tick to make a player drop what they carry when a blast or a heavy hit knocks them about.")]
        public bool DropWhenKnocked = true;

        [Tooltip("How fast a carried thing is flung from the hands when its carrier is knocked about, in metres per second, on top of the carrier's own speed.")]
        [Min(0f)] public float KnockedDropSpeed = 2.5f;

        [Header("Thrown objects hitting things")]
        [Tooltip("Slowest impact that counts as a hit, in metres per second. Below this a thrown thing just bumps. Raise it if gentle drops hurt people.")]
        [Min(0f)] public float MinImpactSpeed = 4f;

        [Tooltip("Hit points taken per kilogram for each metre per second of impact above the minimum. At 0.35 a chair (8 kg) landing at 12 m/s takes about 22.")]
        [Min(0f)] public float DamagePerKgMps = 0.35f;

        [Tooltip("How fast a struck player is shoved, in metres per second, per kilogram for each metre per second of impact. At 0.08 a chair at 12 m/s shoves at about 7.7.")]
        [Min(0f)] public float ShovePerKgMps = 0.08f;

        [Tooltip("Fastest a thrown object can shove a player, in metres per second, however heavy and fast it was.")]
        [Min(0f)] public float MaxShoveSpeed = 12f;

        [Tooltip("How long a player struck by a thrown object cannot steer, in seconds.")]
        [Min(0f)] public float ShoveControlLossSeconds = 0.5f;

        [Tooltip("How long after a throw the object still counts as thrown, in seconds. After this, or once it has hit someone, it is just furniture again and bumping it hurts nobody.")]
        [Min(0.1f)] public float LiveThrowSeconds = 3f;

        [Header("Leaving the building")]
        [Tooltip("How far below where it started a loose thing may fall before it is taken away and put back where it started, in metres. Must be more than one storey or things dropped down a stairwell vanish.")]
        [Min(1f)] public float FallReturnDistance = 6f;

        [Tooltip("How long a thing that fell out of the building stays gone before it is back where it started, in seconds.")]
        [Min(0f)] public float FallReturnSeconds = 2f;
    }
}
