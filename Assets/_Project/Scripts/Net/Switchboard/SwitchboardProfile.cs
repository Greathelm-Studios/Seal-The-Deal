using UnityEngine;

namespace CloseTheDeal.Net
{
    /// <summary>
    /// Designer-owned tuning for the prop stream. Runtime only reads it (AGENTS.md §4).
    /// Design: docs/systems/SWITCHBOARD.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Switchboard Profile", fileName = "SwitchboardProfile")]
    public sealed class SwitchboardProfile : ScriptableObject
    {
        [Header("Budget")]
        [Tooltip("Maximum upload for moving furniture to each player, in bytes per second. Higher allows more updates; lower saves upload. Minimum 2000 bytes per second.")]
        [Min(2000)]
        public int BytesPerSecondPerPlayer = 12000;

        [Tooltip("How many furniture update packets go to each player per second. Higher smooths motion but leaves fewer bytes per packet. Best as a divisor of the 60-tick rate; range 5 to 60.")]
        [Range(5, 60)]
        public int SendsPerSecond = 30;

        [Header("Floor bands")]
        [Tooltip("Floors above and below a player in the same tower that receive frequent furniture updates. Higher reaches farther; lower saves upload. Minimum 0 floors.")]
        [Min(0)]
        public int NearFloorsSameTower = 1;

        [Tooltip("Floors above and below a player in the rival tower that receive frequent furniture updates. Higher reaches farther across the street; lower saves upload. Minimum 0 floors.")]
        [Min(0)]
        public int NearFloorsRivalTower = 2;

        [Tooltip("How much priority distant furniture gains each send, as a fraction of nearby furniture. Higher updates it more often; lower favours nearby furniture. Range 0.01 to 1.")]
        [Range(0.01f, 1f)]
        public float FarWeight = 0.1f;

        [Header("Resting and healing")]
        [Tooltip("How many times a stopped piece of furniture sends its final position. Higher resists lost packets but uses more upload. Range 1 to 10 sends.")]
        [Range(1, 10)]
        public int RestRepeats = 3;

        [Tooltip("How many still pieces of furniture per second receive a position refresh. Higher corrects more quickly but uses more upload. Minimum 0 bodies per second.")]
        [Min(0)]
        public int HealBodiesPerSecond = 8;

        [Header("Smoothing")]
        [Tooltip("Shortest delay for playing furniture motion on clients, in milliseconds. Higher hides uneven arrivals but feels later; lower responds sooner. Minimum 0 milliseconds.")]
        [Min(0f)]
        public float SmoothingDelayFloorMs = 50f;

        [Tooltip("Longest delay for playing furniture motion on clients, in milliseconds. Higher hides more uneven arrivals but feels later; lower responds sooner. Minimum 0 milliseconds.")]
        [Min(0f)]
        public float SmoothingDelayCeilingMs = 250f;
    }
}
