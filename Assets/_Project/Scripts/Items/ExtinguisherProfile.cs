using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>Designer-owned tuning for the fire extinguisher. Runtime only reads it (AGENTS.md §4).</summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Items/Extinguisher Profile", fileName = "Extinguisher")]
    public sealed class ExtinguisherProfile : ScriptableObject
    {
        [Tooltip("How long it pours smoke once the pin is pulled, in seconds. It cannot be stopped. Longer fills a bigger part of the floor.")]
        [Min(0.5f)] public float SmokeSeconds = 6f;

        [Tooltip("How hard the escaping gas pushes the loose extinguisher along, in metres per second gained each second. 0 = it just sits there smoking; 14 sends it skittering across the floor.")]
        [Min(0f)] public float Thrust = 14f;

        [Tooltip("How hard the gas spins it, in turns per second gained each second. Higher = tighter, wilder circles.")]
        [Min(0f)] public float Spin = 1.5f;

        [Tooltip("Fastest the gas will drive it, in metres per second. It still shoves anyone it runs into.")]
        [Min(0f)] public float MaxSpeed = 9f;

        [Tooltip("How long the empty extinguisher stays around as something to throw before it is taken away, in seconds. A fresh one is then back where it started after the wait set on its item definition.")]
        [Min(0f)] public float EmptyLingerSeconds = 8f;
    }
}
