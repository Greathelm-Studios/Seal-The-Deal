using System.Collections.Generic;

namespace CloseTheDeal.FloorGen
{
    /// <summary>Kit prefabs the generator places. Each name is the prefab's file name in Art/Kit/Prefabs.</summary>
    public enum KitPiece : byte
    {
        Slab_4x4,
        Slab_4x4_LightLine,
        Wall_Solid_4m,
        Wall_Solid_4m_Cobalt,
        Wall_Door_4m,
        Wall_Door_4m_PortalHead,
        Wall_Curtain_4m,
        Window_4m,
        Column_Facade,
        Column_CornerL,
        Column_CornerR,
        Partition_Glass_4m,
        Partition_Glass_2m,
        Partition_Frost_4m,
        Partition_Frost_2m,
        Partition_Fluted_4m,
        Partition_Fluted_2m,
        Partition_Louvre_4m,
        Partition_Louvre_2m,
        Partition_Glass_Door_2m,
        Lift_Door_Frame_4m_1F,
        Lift_Shaft_Wall_4m_Solid,
        Lift_Car,
        Lift_Door_Panel,
        Workstation_Bench,
        Desk_Sculpt,
        Desk_Reception,
        Counter_Capsule_2m,
        Cover_Block_2m,
        Storage_Low,
        Copier,
        Vending_Machine,
        Water_Server,
        Planter_Box,
        Column_Graphic,
        Ceiling_Panel_4x4,
        Carpet_Overlay_4x4,
        Ceiling_Grid_4x4,
        Chair_Task,
        Chair_Pod,
        Monitor_Flat,
        Keyboard,
        Phone_Desk,
        Doc_Tray,
        PC_Tower,
        Lightbox_Wall_Wave
    }

    public enum RoomKind : byte
    {
        Ring,
        Court,
        WindowHall,
        Lounge,
        OpenOffice,
        Meeting,
        Kitchen,
        Print,
        Archive,
        ManagersOffice
    }

    public enum EdgeKind : byte
    {
        Open,
        Solid,
        Glass,
        Frost,
        Fluted,
        Louvre,
        FacadeWindow,
        FacadeCurtain,
        FacadeSolid,
        Shaft,
        LiftDoor
    }

    /// <summary>What a loose prop spot holds. The floor race maps each to a networked prop prefab.</summary>
    public enum PropKind : byte
    {
        Chair,
        Monitor,
        Cabinet
    }

    /// <summary>Cell sides in plan: north is the top of the drawing (Unity +Z).</summary>
    public static class Side
    {
        public const int North = 0, East = 1, South = 2, West = 3;
        public static readonly int[] Dx = { 0, 1, 0, -1 };
        public static readonly int[] Dy = { -1, 0, 1, 0 };
        public static int Opposite(int side) => (side + 2) & 3;
    }

    public struct FloorEdge
    {
        public EdgeKind Kind;
        /// <summary>Door centre in metres from the edge's west or north end (1, 2 or 3); 0 = no door.</summary>
        public byte DoorAt;
        /// <summary>True when the wall's thickness goes south (north edges) or east (west edges).</summary>
        public bool ThickTowardHigh;
        public bool Portal;
        /// <summary>A solid wall in the floor's accent colour.</summary>
        public bool Accent;

        public bool Passable => Kind == EdgeKind.Open || Kind == EdgeKind.LiftDoor || DoorAt != 0;
    }

    /// <summary>One kit prefab to instantiate. Plan metres: X east, Y south (Unity Z = -Y), Height up from the floor.</summary>
    public struct KitPlacement
    {
        public KitPiece Piece;
        public float X, Y, Height, Yaw;
        public bool Still;
    }

    public struct FloorSpot
    {
        public float X, Y, Yaw;
        public int Room;
    }

    public struct PropSpot
    {
        public PropKind Kind;
        public float X, Y, Height, Yaw;
    }

    public enum MarkKind : byte
    {
        /// <summary>A poster; Index picks which one.</summary>
        Poster,
        /// <summary>The floor's number, large, on a coloured wall.</summary>
        FloorNumber,
        /// <summary>A room's name over its door. Index is 1 when the door is in a glass wall, whose dark head rail needs light lettering.</summary>
        RoomName,
        /// <summary>A line of company wisdom in a stripe high on a coloured wall; Index picks which one.</summary>
        Slogan
    }

    /// <summary>Something flat on a wall face. X, Y is its centre in plan metres on the face; (NormalX, NormalY) points out of the wall.</summary>
    public struct WallMark
    {
        public MarkKind Kind;
        public float X, Y, Height, Width, Tall;
        public int NormalX, NormalY;
        public int Index;
        public string Text;
    }

    /// <summary>What hangs over one bay.</summary>
    public enum CeilingKind : byte
    {
        /// <summary>A lift shaft: nothing.</summary>
        None,
        /// <summary>Bare soffit over a corridor, carrying the spine.</summary>
        Route,
        /// <summary>Bare soffit over an open hall, with one lane down the bay.</summary>
        Lanes,
        /// <summary>A flush tiled ceiling with two rows of strips.</summary>
        Tiles,
        /// <summary>A floating panel with a glowing edge.</summary>
        Panels
    }

    public enum CeilingLineKind : byte
    {
        /// <summary>The lit line over the corridors: one closed loop over the ring, joined by every cross corridor.</summary>
        Spine,
        Lane,
        Strip
    }

    /// <summary>One straight lit line on the ceiling, by its centre line in plan metres. Runs east (Y0 = Y1) or south (X0 = X1).</summary>
    public struct CeilingLine
    {
        public CeilingLineKind Kind;
        public float X0, Y0, X1, Y1;

        public bool AlongX => Y0 == Y1;
        public float Length => AlongX ? X1 - X0 : Y1 - Y0;
    }

    public struct FloorLift
    {
        public int CellX, CellY;
        /// <summary>The side of the cell the doors are on.</summary>
        public int DoorSide;
        public float CarX, CarY, CarYaw;
        public float DoorX, DoorY;
        /// <summary>Where a player stands just outside the doors.</summary>
        public float FrontX, FrontY;
    }

    public sealed class FloorRoom
    {
        public int Index;
        public RoomKind Kind;
        public string Name;
        public bool Open;
        public EdgeKind Front;
        public int Exits;
        /// <summary>True when this room's ceiling lines run north to south.</summary>
        public bool CeilingAlongY;
        public readonly List<int> Cells = new();

        public bool IsCorridor => Kind == RoomKind.Ring || Kind == RoomKind.Court;
        public bool IsPublic => IsCorridor || Open;
    }

    /// <summary>One generated floor: rooms, walls, kit placements, starts and authored spots. Built by FloorGenerator, never edited after.</summary>
    public sealed class FloorLayout
    {
        public const float Cell = 4f;
        public const int NoRoom = -1;

        public uint Seed;
        public int Floor;
        public int Attempt;
        public bool Valid;
        public readonly List<string> Problems = new();

        public int Cols, Rows;
        public int RingX0, RingY0, RingX1, RingY1;
        public int[] RoomOf;
        public readonly List<FloorRoom> Rooms = new();
        /// <summary>Edge on the north side of cell (i, j): index i + j * Cols, j from 0 to Rows.</summary>
        public FloorEdge[] NorthEdges;
        /// <summary>Edge on the west side of cell (i, j): index i + j * (Cols + 1), i from 0 to Cols.</summary>
        public FloorEdge[] WestEdges;

        public readonly List<KitPlacement> Pieces = new();
        public bool HasExitLift, HasArrivalLift;
        public FloorLift ExitLift, ArrivalLift;
        public bool HasOffice;
        public int OfficeRoom = NoRoom;
        public float OfficeX0, OfficeY0, OfficeX1, OfficeY1;

        /// <summary>Starts for the firm that holds the briefcase (inside the arrival lift), or firm A on floor 1.</summary>
        public readonly List<FloorSpot> StartA = new();
        public readonly List<FloorSpot> StartB = new();
        public bool HasBriefcase;
        public FloorSpot Briefcase;
        public readonly List<FloorSpot> SpawnSpots = new();
        public readonly List<FloorSpot> LootSpots = new();
        public readonly List<PropSpot> PropSpots = new();
        /// <summary>The ceiling over each cell, planned for the whole floor at once by FloorCeiling.</summary>
        public CeilingKind[] CeilingOf;
        public readonly List<CeilingLine> CeilingLines = new();
        /// <summary>Where the ceiling plan puts a room light, in plan metres.</summary>
        public readonly List<FloorSpot> Lights = new();
        public readonly List<WallMark> Marks = new();

        public FloorNav Nav;
        public float StartAToGoal, StartBToGoal, StartsApart, CaseToGoal, CaseToStartA, CaseToStartB;

        public float Width => Cols * Cell;
        public float Depth => Rows * Cell;
        public int CellIndex(int i, int j) => i + j * Cols;
        public bool InPlate(int i, int j) => i >= 0 && j >= 0 && i < Cols && j < Rows;

        public int RoomAt(int i, int j) => InPlate(i, j) ? RoomOf[CellIndex(i, j)] : NoRoom;

        /// <summary>The room at a plan position, or null outside the plate and inside a lift shaft.</summary>
        public FloorRoom RoomAtPoint(float x, float y)
        {
            int i = (int)System.Math.Floor(x / Cell), j = (int)System.Math.Floor(y / Cell);
            int room = RoomAt(i, j);
            return room >= 0 ? Rooms[room] : null;
        }

        public FloorEdge EdgeOf(int i, int j, int side)
        {
            switch (side)
            {
                case Side.North: return NorthEdges[i + j * Cols];
                case Side.South: return NorthEdges[i + (j + 1) * Cols];
                case Side.West: return WestEdges[i + j * (Cols + 1)];
                default: return WestEdges[i + 1 + j * (Cols + 1)];
            }
        }

        internal void SetEdge(int i, int j, int side, FloorEdge edge)
        {
            switch (side)
            {
                case Side.North: NorthEdges[i + j * Cols] = edge; break;
                case Side.South: NorthEdges[i + (j + 1) * Cols] = edge; break;
                case Side.West: WestEdges[i + j * (Cols + 1)] = edge; break;
                default: WestEdges[i + 1 + j * (Cols + 1)] = edge; break;
            }
        }
    }
}
