using System;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Integration
{
    /// <summary>
    /// Designer-owned list of what lies loose on which floor at the start of every match, and
    /// the rules for where on the floor it may land. Each line is placed in both towers at the
    /// same spot, so neither firm finds more. Read at runtime, never written (AGENTS.md §4).
    /// Design: docs/systems/INTEGRATION.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Integration/Floor Loot Table", fileName = "FloorLootTable")]
    public sealed class FloorLootTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("The thing to put down: a networked item or piece of furniture from Prefabs/Items or Prefabs/Props.")]
            public NetworkObject Prefab;

            [Tooltip("Which floor, counted up from the reception (0). 1 is the first floor above it. Floors above the roof's are skipped.")]
            [Min(1)] public int Floor;
        }

        [Tooltip("Everything placed at the start of each match, once per tower. Every line is a prop and counts toward the floor's prop cap of 16 (PROPS.md).")]
        [SerializeField] Entry[] _entries = new Entry[0];

        [Header("Where on the floor")]
        [Tooltip("How far from the side walls and the back of the open floor a thing may land, in metres. Bigger keeps loot in the middle of the room.")]
        [Min(0.5f)] [SerializeField] float _wallMargin = 1.5f;

        [Tooltip("How far back from the window sill a thing may land, in metres. Below 1.5 m loot can be knocked out of the window by a stray shove.")]
        [Min(0.5f)] [SerializeField] float _windowMargin = 2.5f;

        [Tooltip("How far from the middle of the stairwell a thing may land, in metres. The stair cage is about 2.75 m across; less than 3.5 m can block the top step.")]
        [Min(0f)] [SerializeField] float _stairClearance = 4f;

        [Tooltip("How much clear room a thing needs around it, in metres from its centre. Raise it if loot lands touching desks.")]
        [Min(0.2f)] [SerializeField] float _clearance = 0.5f;

        [Tooltip("How far apart two things on the same floor must be, in metres.")]
        [Min(0f)] [SerializeField] float _spacing = 2f;

        [Tooltip("How many random spots are tried per thing before it is left out for this match (the log says so). More finds room on crowded floors at a small cost once per match.")]
        [Range(4, 200)] [SerializeField] int _tries = 60;

        public int Count => _entries.Length;
        public Entry this[int index] => _entries[index];
        public float WallMargin => _wallMargin;
        public float WindowMargin => _windowMargin;
        public float StairClearance => _stairClearance;
        public float Clearance => _clearance;
        public float Spacing => _spacing;
        public int Tries => _tries;
    }
}
