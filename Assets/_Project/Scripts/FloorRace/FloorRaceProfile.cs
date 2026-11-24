using System;
using CloseTheDeal.FloorGen;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.FloorRace
{
    /// <summary>Designer-owned tuning for the floor race. Read at runtime, never written. Design: docs/systems/FLOOR_RACE.md.</summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Floor Race Profile", fileName = "FloorRace")]
    public sealed class FloorRaceProfile : ScriptableObject
    {
        [Serializable]
        public struct LootEntry
        {
            [Tooltip("A networked weapon or gadget pickup.")]
            public NetworkObject Prefab;

            [Tooltip("How often this turns up against the other entries. 2 turns up twice as often as 1; 0 never.")]
            [Min(0)] public int Weight;
        }

        [Header("Floors")]
        [Tooltip("How generated floors are laid out: plate size, room mix, fair-start distances and how many storeys the building has.")]
        public FloorGenProfile Floors;

        [Header("The lift")]
        [Tooltip("Seconds the lift doors take to close or open. Shorter makes boarding snappier; under 0.4 players get shut out mid-step.")]
        [Range(0.2f, 3f)] public float DoorSeconds = 0.8f;

        [Tooltip("Seconds between the doors closing and everyone arriving on the next floor's loading screen. Longer lets the closing doors read.")]
        [Range(0f, 3f)] public float DepartHoldSeconds = 0.7f;

        [Tooltip("Size of the space inside the lift car that counts as aboard, in metres: width, height, depth. Keep it inside the car walls (3.7 x 2.6 x 3.55) so someone outside the doors does not count.")]
        public Vector3 CabinSize = new(3.2f, 2.6f, 3.2f);

        [Header("Moving up a floor")]
        [Tooltip("Seconds the lift takes between floors. The firm aboard rides it standing in the closed car and steps out where they stood; the other firm sees the floor-change screen for as long. The next floor is built during the ride. Longer gives the ride weight and makes every floor change slower.")]
        [Range(0.5f, 8f)] public float RideSeconds = 3f;

        [Tooltip("Seconds the next floor exists before players are moved onto it, so every machine has finished building it. Raise it if clients ever arrive on an unbuilt floor.")]
        [Range(0.2f, 3f)] public float BuildLeadSeconds = 0.5f;

        [Tooltip("Seconds the firm without the briefcase gets on the new floor before the lift doors open on the firm that has it. Longer favours the firm that lost the last floor.")]
        [Range(0f, 30f)] public float HeadStartSeconds = 12f;

        [Tooltip("Seconds after arriving before the floor below is removed. It only needs to outlast a slow client's arrival.")]
        [Range(0.2f, 5f)] public float TeardownSeconds = 1f;

        [Header("Death and respawn")]
        [Tooltip("Seconds between a firm's last player going down and the whole firm coming back. 0 is instant; a short wait lets the last death be seen.")]
        [Range(0f, 10f)] public float WipeRespawnSeconds = 2f;

        [Tooltip("Seconds a player may be outside the floor's walls before it counts as falling to their death. Longer shows more of the fall.")]
        [Range(0f, 5f)] public float FallSeconds = 1.2f;

        [Tooltip("How far outside the walls still counts as inside, in metres. Higher forgives leaning out of a window.")]
        [Range(0f, 3f)] public float OutsideMargin = 0.75f;

        [Tooltip("How far below the floor a player must drop to be killed at once, in metres.")]
        [Range(1f, 20f)] public float FallDepth = 3f;

        [Header("Smart respawn")]
        [Tooltip("A respawn spot is rejected if a living rival is within this many metres. Higher keeps a returning firm further from the fight.")]
        [Range(0f, 30f)] public float MinRivalDistance = 8f;

        [Tooltip("A respawn spot is rejected if a living rival can see it from within this many metres.")]
        [Range(0f, 60f)] public float SightRange = 40f;

        [Tooltip("A respawn spot is never closer to the briefcase than this many metres of walking. Lower lets a wiped firm come back on top of it.")]
        [Range(0f, 30f)] public float MinCaseDistance = 6f;

        [Header("Loot and props")]
        [Tooltip("How many weapons and gadgets lie on each floor. The floor offers up to 24 spots; the seed picks which are used.")]
        [Range(0, 24)] public int LootPerFloor = 8;

        [Tooltip("What can turn up as loot.")]
        public LootEntry[] Loot = new LootEntry[0];

        [Tooltip("The loose chair placed at desks and tables.")]
        public NetworkObject ChairProp;

        [Tooltip("The loose monitor placed on desks.")]
        public NetworkObject MonitorProp;

        [Tooltip("The loose cabinet placed in print rooms and archives.")]
        public NetworkObject CabinetProp;

        [Tooltip("How far above the floor loot and props are dropped in, in metres, so they settle rather than start inside the slab.")]
        [Range(0.05f, 1f)] public float DropHeight = 0.3f;

        [Header("Cash")]
        [Tooltip("Cash every player of a firm earns each time the firm takes the briefcase up a floor.")]
        [Min(0)] public int CashPerFloor = 100;

        [Tooltip("Extra cash every player of the winning firm earns for delivering the briefcase to the manager's office, on top of Cash Per Floor.")]
        [Min(0)] public int WinBonusCash;
    }
}
