// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "a test dummy ... to test animations of being hit by certain powers of
//              weapons", with the reactions tuned on "a profile asset of your own"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The designer's tuning panel for how a body reacts to hits. Its own asset, apart
//              from AnimationProfile, because hit reactions are tuned against weapons and the
//              animation profile is tuned against movement, by different people at different
//              times.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Designer-owned tuning for hit reactions on the active ragdoll. Runtime only reads it
    /// (AGENTS.md §4).
    /// </summary>
    /// <remarks>
    /// Every default here is a first guess picked from frame strips of the test dummy, not from
    /// play. Which hits should only flinch and which should knock a body down is a design
    /// question that is still open; this asset holds only the flinch.
    /// </remarks>
    [CreateAssetMenu(menuName = "Close the Deal/Hit Reaction Profile", fileName = "HitReactionProfile")]
    public sealed class HitReactionProfile : ScriptableObject
    {
        /// <summary>The shortest recovery the flinch allows, seconds; see <see cref="FlinchRecover"/>'s tooltip. WHY 0.05: six ragdoll physics steps, the least in which a joint can visibly give and come back.</summary>
        public const float MinFlinchRecover = 0.05f;

        [Header("Flinch: the push")]
        [Tooltip("How much of a hit's push the body takes, as a fraction. 1 = all of it. Lower = every hit moves the body less; higher = everything hits like a truck.")]
        [Range(0f, 2f)] public float ImpulseShare = 1f;

        [Tooltip("How much of the push the parts joined to the struck one also take, as a fraction of it. 0 = only the struck part jerks; 1 = the whole area moves as a lump.")]
        [Range(0f, 1f)] public float NeighbourShare = 0.4f;

        [Tooltip("The fastest one hit may throw a single body part, in metres per second. Stops a heavy hit on a light forearm firing it across the room. Lower = tamer; higher = wilder limbs.")]
        [Min(0f)] public float MaxKickSpeed = 8f;

        [Header("Flinch: the slack joints")]
        [Tooltip("A hit this hard, in newton-seconds, makes the struck joint go fully slack; weaker hits slacken it in proportion. A pistol shot is about 20. Lower = small hits make the body crumple more.")]
        [Min(1f)] public float FlinchFullImpulse = 25f;

        [Tooltip("How strong a fully slack joint still is, as a fraction of normal. 0 = it hangs completely loose; 1 = hits never weaken joints.")]
        [Range(0f, 1f)] public float FlinchLimpShare = 0.15f;

        [Tooltip("How long a fully slack joint takes to get its strength back, in seconds. Lower = snappy twitches; higher = the body stays rubbery after a hit. Never shorter than 0.05.")]
        [Min(MinFlinchRecover)] public float FlinchRecover = 0.35f;

        // WHY "placeholder": which hits floor a body, for how long, and how it gets up are
        // design rules the GDD has not settled (it says only "on explosions and heavy hits,
        // then recover quickly"). These values exist so the rules can be tried on the dummy.
        [Header("Knock-down (placeholder rules, not yet decided)")]
        [Tooltip("A hit at least this hard, in newton-seconds, knocks the body off its feet. 0 = nothing ever does. At 150 a thrown desk (about 320) and any blast (235 to 590) floor it, and a thrown chair (about 80) does not.")]
        [Min(0f)] public float KnockDownImpulse = 150f;

        [Tooltip("Whether a shot can knock the body down when it is hard enough. Off = bullets only ever flinch, however strong.")]
        public bool BulletsKnockDown;

        [Tooltip("How strong the joints still are while the body is down, as a fraction of normal. 0 = a completely limp rag; 0.2 = it holds some shape as it falls.")]
        [Range(0f, 1f)] public float DownLimpShare = 0.05f;

        [Tooltip("How much the floor grips a knocked-down body. 0 = it skates like ice; 1 = it stops within a couple of metres. Lower = longer, sillier slides.")]
        [Range(0f, 1f)] public float DownFriction = 0.8f;

        [Tooltip("The shortest the body stays down, in seconds, even if it has stopped moving.")]
        [Min(0f)] public float DownMinTime = 0.6f;

        [Tooltip("The longest the body stays down, in seconds, even if it is still sliding or tumbling. Lower = a quicker, less punishing knock-down.")]
        [Min(0f)] public float DownMaxTime = 1.5f;

        [Tooltip("The body counts as having come to rest when its hips move slower than this, in metres per second. It gets up once at rest (after the shortest time above).")]
        [Min(0f)] public float SettleSpeed = 0.5f;

        [Tooltip("How long getting back up takes, in seconds: the body is drawn from where it lies up into its standing pose. Lower = it pops up; higher = a slow, heavy rise. Never shorter than 0.05.")]
        [Min(MinFlinchRecover)] public float GetUpTime = 0.6f;
    }
}
