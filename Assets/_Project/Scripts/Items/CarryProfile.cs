using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>
    /// Designer-owned tuning for picking things up, carrying, throwing and what a thrown
    /// object does when it lands. Runtime only reads it (AGENTS.md §4).
    /// </summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Items/Carry Profile", fileName = "CarryProfile")]
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

        [Tooltip("How long a player must wait before taking back the thing they just let go of, in seconds. Stops a thrown chair being snatched back and thrown again every few ticks at someone pinned in a corner. Anyone else may catch it at once.")]
        [Min(0f)] public float RetakeSeconds = 0.6f;

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
        [Tooltip("Outside both towers (out of a window, into the street): how far below where it started a loose thing may fall before it is taken away and put back, in metres. Less than one storey, so a thing thrown out of a first-floor window still comes back; things on a skybridge at their own level stay.")]
        [Min(0.5f)] public float OutsideDropMetres = 2.5f;

        [Tooltip("Anywhere, inside or out: how far below the ground floor a loose thing may fall before it is taken away and put back, in metres.")]
        [Min(0f)] public float BelowGroundMetres = 1f;

        [Tooltip("Only where no tower has been built: how far below where it started a loose thing may fall before it is taken away and put back, in metres. Must be more than one storey or things dropped down a stairwell vanish.")]
        [Min(1f)] public float FallReturnDistance = 6f;

        [Tooltip("How long a thing that fell out of the building stays gone before it is back where it started, in seconds.")]
        [Min(0f)] public float FallReturnSeconds = 2f;

        [Header("Feel in the hands (drawn only, never synced)")]
        [Tooltip("How long a light thing takes to fly from where it lay into the hands, in seconds. Eases out. Under 0.3 s keeps the take feeling instant.")]
        [Range(0f, 0.3f)] public float TakeSecondsLight = 0.12f;

        [Tooltip("How long a thing at the weight limit takes to come into the hands, in seconds.")]
        [Range(0f, 0.3f)] public float TakeSecondsHeavy = 0.24f;

        [Tooltip("How quickly a light carried thing catches up when the view turns, per second. Higher is stiffer; 28 barely trails.")]
        [Min(1f)] public float SwayFollowLight = 28f;

        [Tooltip("How quickly a thing at the weight limit catches up when the view turns, per second. Lower swings further behind, so heavy things feel heavy.")]
        [Min(1f)] public float SwayFollowHeavy = 9f;

        [Tooltip("The furthest a carried thing may trail behind a fast turn, in degrees, so it never leaves the view.")]
        [Range(0f, 30f)] public float MaxSwayDegrees = 10f;

        [Header("Sound")]
        [Tooltip("Every item's sounds and the mixer bus they play through, unless an item definition names its own set.")]
        public ItemSoundSet Sounds;

        [Header("Any loose prop (no item definition of its own)")]
        [Tooltip("How fast the lightest loose props (1 kg or less) leave the hands when thrown, in metres per second. Heavier ones are slower, down to the speed below at the weight limit.")]
        [Min(0f)] public float LooseThrowSpeedLight = 15f;

        [Tooltip("How fast a loose prop at the weight limit leaves the hands when thrown, in metres per second.")]
        [Min(0f)] public float LooseThrowSpeedHeavy = 5f;

        [Tooltip("How fast the lightest loose props tumble when thrown, in full turns per second.")]
        [Min(0f)] public float LooseTumbleLight = 2.2f;

        [Tooltip("How fast a loose prop at the weight limit tumbles when thrown, in full turns per second.")]
        [Min(0f)] public float LooseTumbleHeavy = 0.3f;

        [Tooltip("How far in front of the eye a loose prop's near face is held, in metres. Its own depth is added, so a big thing is held further out.")]
        [Min(0.1f)] public float LooseHoldAhead = 0.55f;

        [Tooltip("How far below the eye a loose prop's base is held, in metres, before its height is counted. With the setting below, a 1 m chair's base sits 1 m below the eye.")]
        [Min(0f)] public float LooseHoldBelowEye = 0.3f;

        [Tooltip("How much further down the base is held for each metre of the prop's height, so tall things are carried low and do not block the view. 0.7 keeps the top of most things just under the crosshair.")]
        [Range(0f, 1f)] public float LooseHoldDropPerMetre = 0.7f;

        /// <summary>
        /// The carry rules in play, for loose props that were not given a profile of their own.
        /// Set by the first player's ItemCarrier; null until one exists.
        /// </summary>
        public static CarryProfile Shared { get; internal set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Shared = null;
        }
    }
}
