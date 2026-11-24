using UnityEngine;

namespace CloseTheDeal.FloorGen
{
    /// <summary>Designer-owned tuning for generated floors. Read at runtime, never written. Design: docs/systems/FLOOR_GEN.md.</summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Floor Gen Profile", fileName = "FloorGen")]
    public sealed class FloorGenProfile : ScriptableObject
    {
        [Header("Plate")]
        [Tooltip("How many 4 m bays wide a floor is, east to west. 10 = 40 metres, the KitOffice plate. Wider floors take longer to search and cost more to draw.")]
        [Range(8, 12)] public int Cols = 10;

        [Tooltip("How many 4 m bays deep a floor is, north to south. 8 = 32 metres, the KitOffice plate.")]
        [Range(6, 10)] public int Rows = 8;

        [Header("Layout types")]
        [Tooltip("How often a floor is a core office: the corridor loop round a block of rooms, with rooms and halls along the windows. This is the share out of the four numbers added together; 0 = never. If all four are 0 every floor is this one.")]
        [Range(0, 100)] public int CoreOfficeWeight = 40;

        [Tooltip("How often a floor is open plan: wide window halls of desks with few walls, and a small block of rooms in the middle. Long sightlines, easy to cross, hard to hide in.")]
        [Range(0, 100)] public int OpenPlanWeight = 20;

        [Tooltip("How often a floor is cellular: small closed offices and meeting rooms nearly everywhere. Short sightlines and many doors to check.")]
        [Range(0, 100)] public int CellularWeight = 20;

        [Tooltip("How often a floor is a gallery: the corridor loop runs along the glass and the whole middle is blocks of rooms cut by cross corridors. The most turns and junctions, so the easiest to get lost in.")]
        [Range(0, 100)] public int GalleryWeight = 20;

        [Header("Rooms")]
        [Tooltip("The most 4 m bays one room in the middle of the floor may cover. Higher = fewer, bigger rooms and longer sightlines.")]
        [Range(1, 6)] public int MaxCoreRoomCells = 4;

        [Tooltip("The longest a room along the windows may run, in 4 m bays. Higher = longer window halls.")]
        [Range(1, 4)] public int MaxBandRoomLength = 3;

        [Tooltip("Out of 100 rooms along the windows, how many are open plan with no wall to the corridor. Higher = a more open floor with longer sightlines.")]
        [Range(0, 100)] public int OpenBandRoomPercent = 35;

        [Tooltip("Out of 100 rooms in the middle of the floor, how many are open plan. Higher = a more open floor.")]
        [Range(0, 100)] public int OpenCoreRoomPercent = 15;

        [Tooltip("Out of 100 window bays, how many are open windows a player can be blown out of. The rest are sealed glass or solid wall. Higher = more deaths by falling.")]
        [Range(0, 100)] public int OpenWindowPercent = 30;

        [Tooltip("Out of 100 chances, how often the exit lift's doors face into a room instead of onto a corridor. Higher = harder to find.")]
        [Range(0, 100)] public int LiftInsideRoomPercent = 70;

        [Tooltip("Out of 100 corridor bays, how many get a planter, water cooler or column against a wall. Higher = more clutter and cover in the corridors.")]
        [Range(0, 60)] public int CorridorDressingPercent = 14;

        [Header("Fair starts")]
        [Tooltip("How far each firm's start must be from the exit lift (or the manager's office), in metres of walking. Higher = a longer first run.")]
        [Min(4f)] public float MinStartToGoal = 16f;

        [Tooltip("The most the two firms' walking distances to the exit lift may differ, in metres. Lower = fairer, but the generator has fewer floors to choose from.")]
        [Min(1f)] public float FairStartTolerance = 6f;

        [Tooltip("How far apart the two firms start, in metres of walking. Higher = longer before the first fight.")]
        [Min(4f)] public float MinStartApart = 20f;

        [Tooltip("Floor 1 only: how far the hidden briefcase must be from the exit lift, in metres of walking. Higher = a longer carry once it is found.")]
        [Min(4f)] public float MinCaseToLift = 20f;

        [Tooltip("Floor 1 only: how far the hidden briefcase must be from either firm's start, in metres of walking. Higher = a longer search.")]
        [Min(2f)] public float MinCaseToStart = 12f;

        [Header("Spots")]
        [Tooltip("The fewest respawn spots a floor must offer. A floor with fewer is thrown away and generated again.")]
        [Range(4, 60)] public int MinSpawnSpots = 24;

        [Tooltip("The most places a floor offers for weapons and gadgets. The floor race picks how many are used.")]
        [Range(1, 40)] public int MaxLootSpots = 24;

        [Tooltip("How far apart loot spots are kept, in metres. Higher spreads loot across the floor.")]
        [Min(1f)] public float LootSpacing = 6f;

        [Tooltip("The most loose props (chairs, monitors, cabinets) a floor offers. 16 is the limit the prop netcode is budgeted for; do not raise it without profiling.")]
        [Range(0, 16)] public int MaxProps = 16;

        [Header("Building")]
        [Tooltip("How many storeys the building has. The last one holds the manager's office.")]
        [Range(2, 20)] public int FloorCount = 10;
    }
}
