using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>Designer-owned tuning for the wet floor sign. Runtime only reads it (AGENTS.md §4).</summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Items/Wet Floor Profile", fileName = "WetFloor")]
    public sealed class WetFloorProfile : ScriptableObject
    {
        [Header("Putting it down")]
        [Tooltip("How far in front of the player the sign is put down, in metres.")]
        [Min(0.5f)] public float PlaceDistance = 1.2f;

        [Tooltip("How far the sign can be nudged from where it was put down before the floor stops being slippery, in metres. A blast or a kick clears the trap.")]
        [Min(0.05f)] public float NudgeTolerance = 0.4f;

        [Header("The wet patch")]
        [Tooltip("How far the slippery floor reaches around the sign, in metres. The puddle is drawn this size.")]
        [Min(0.5f)] public float Radius = 2.2f;

        [Tooltip("Anyone crossing the wet floor faster than this slips, in metres per second. Set it between walking and sprinting speed so walking is safe and running is not. It catches both firms, the one who placed it included.")]
        [Min(0f)] public float SlipAboveSpeed = 7f;

        [Tooltip("How fast a slipping player slides on, in metres per second, in the direction they were running.")]
        [Min(0f)] public float SlipSpeed = 9f;

        [Tooltip("How fast a slipping player's feet go out from under them, in metres per second upward. Higher = a bigger pratfall.")]
        [Min(0f)] public float SlipLift = 3f;

        [Tooltip("How long a slipping player cannot steer, in seconds.")]
        [Min(0f)] public float ControlLossSeconds = 0.9f;

        [Tooltip("How long before the same player can slip again, in seconds, so nobody is juggled on the spot.")]
        [Min(0f)] public float RepeatSeconds = 2f;
    }
}
