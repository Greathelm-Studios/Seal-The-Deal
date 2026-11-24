using System;
using System.Collections.Generic;

namespace CloseTheDeal.FloorGen
{
    /// <summary>Plans a floor's whole ceiling in one pass from its finished rooms and walls. Draws no random numbers. Design: docs/systems/FLOOR_GEN.md.</summary>
    public static class FloorCeiling
    {
        public const float Soffit = 4.3f;
        public const float SpineWidth = 0.3f, LaneWidth = 0.2f, StripWidth = 0.2f;
        const float C = FloorLayout.Cell;
        const float EndInset = 0.5f;
        static readonly float[] LaneAt = { 2f };
        static readonly float[] StripAt = { 1.25f, 2.75f };

        public static float WidthOf(CeilingLineKind kind) => kind == CeilingLineKind.Spine ? SpineWidth : kind == CeilingLineKind.Lane ? LaneWidth : StripWidth;

        internal static void Plan(FloorLayout layout)
        {
            layout.CeilingOf = new CeilingKind[layout.Cols * layout.Rows];
            foreach (FloorRoom room in layout.Rooms)
                if (!room.IsCorridor && room.Cells.Count > 0)
                    room.CeilingAlongY = RunsSouth(layout, room);

            for (int j = 0; j < layout.Rows; j++)
                for (int i = 0; i < layout.Cols; i++)
                {
                    int index = layout.RoomOf[layout.CellIndex(i, j)];
                    if (index < 0)
                        continue;

                    FloorRoom room = layout.Rooms[index];
                    CeilingKind kind = room.IsCorridor ? CeilingKind.Route
                        : room.Open ? CeilingKind.Lanes
                        : room.Kind == RoomKind.Meeting || room.Kind == RoomKind.ManagersOffice ? CeilingKind.Panels
                        : CeilingKind.Tiles;
                    layout.CeilingOf[layout.CellIndex(i, j)] = kind;
                    if (kind != CeilingKind.Tiles && kind != CeilingKind.Panels)
                        continue;

                    layout.Pieces.Add(new KitPlacement
                    {
                        Piece = kind == CeilingKind.Tiles ? KitPiece.Ceiling_Grid_4x4 : KitPiece.Ceiling_Panel_4x4,
                        X = i * C, Y = j * C, Height = Soffit, Still = true
                    });
                    layout.Lights.Add(new FloorSpot { X = (i + 0.5f) * C, Y = (j + 0.5f) * C, Room = index });
                }

            Spine(layout);
            Rows(layout, CeilingKind.Lanes, CeilingLineKind.Lane, LaneAt);
            Rows(layout, CeilingKind.Tiles, CeilingLineKind.Strip, StripAt);
        }

        // A closed room's rows run its longer side. Otherwise lines follow the corridor the room fronts, then its window band, then its longer side.
        static bool RunsSouth(FloorLayout layout, FloorRoom room)
        {
            int east = 0, south = 0;
            int i0 = layout.Cols, i1 = -1, j0 = layout.Rows, j1 = -1;
            foreach (int cell in room.Cells)
            {
                int i = cell % layout.Cols, j = cell / layout.Cols;
                i0 = Math.Min(i0, i); i1 = Math.Max(i1, i);
                j0 = Math.Min(j0, j); j1 = Math.Max(j1, j);
                for (int side = 0; side < 4; side++)
                {
                    if (!IsRoute(layout, i + Side.Dx[side], j + Side.Dy[side]))
                        continue;
                    if (side == Side.North || side == Side.South) east++;
                    else south++;
                }
            }

            if (!room.Open && i1 - i0 != j1 - j0) return j1 - j0 > i1 - i0;
            if (east != south) return south > east;
            if (j1 < layout.RingY0 || j0 > layout.RingY1) return false;
            if (i1 < layout.RingX0 || i0 > layout.RingX1) return true;
            if (i1 - i0 != j1 - j0) return j1 - j0 > i1 - i0;
            return layout.Rows > layout.Cols;
        }

        static bool IsRoute(FloorLayout layout, int i, int j)
        {
            int room = layout.RoomAt(i, j);
            return room >= 0 && layout.Rooms[room].IsCorridor;
        }

        // East-west runs own every junction, so a north-south run stops at the edge of the run it meets and nothing overlaps.
        static void Spine(FloorLayout layout)
        {
            const float Half = SpineWidth * 0.5f;
            for (int j = 0; j < layout.Rows; j++)
                for (int i = 0; i < layout.Cols; i++)
                {
                    if (!IsRoute(layout, i, j) || IsRoute(layout, i - 1, j))
                        continue;
                    int end = i;
                    while (IsRoute(layout, end + 1, j)) end++;
                    if (end > i)
                        layout.CeilingLines.Add(new CeilingLine
                        {
                            Kind = CeilingLineKind.Spine, X0 = (i + 0.5f) * C - Half, X1 = (end + 0.5f) * C + Half, Y0 = (j + 0.5f) * C, Y1 = (j + 0.5f) * C
                        });
                }

            for (int i = 0; i < layout.Cols; i++)
                for (int j = 0; j < layout.Rows; j++)
                {
                    if (!IsRoute(layout, i, j) || IsRoute(layout, i, j - 1))
                        continue;
                    int end = j;
                    while (IsRoute(layout, i, end + 1)) end++;
                    if (end > j)
                        layout.CeilingLines.Add(new CeilingLine
                        {
                            Kind = CeilingLineKind.Spine, X0 = (i + 0.5f) * C, X1 = (i + 0.5f) * C,
                            Y0 = (j + 0.5f) * C + (Crossed(layout, i, j) ? Half : -Half), Y1 = (end + 0.5f) * C - (Crossed(layout, i, end) ? Half : -Half)
                        });
                }
        }

        static bool Crossed(FloorLayout layout, int i, int j) => IsRoute(layout, i - 1, j) || IsRoute(layout, i + 1, j);

        // One line per offset across the bay, carried through every neighbouring bay it can run into without meeting a wall.
        static void Rows(FloorLayout layout, CeilingKind kind, CeilingLineKind line, float[] offsets)
        {
            for (int j = 0; j < layout.Rows; j++)
                for (int i = 0; i < layout.Cols; i++)
                {
                    int cell = layout.CellIndex(i, j);
                    if (layout.CeilingOf[cell] != kind)
                        continue;

                    bool south = layout.Rooms[layout.RoomOf[cell]].CeilingAlongY;
                    int di = south ? 0 : 1, dj = south ? 1 : 0;
                    if (Continues(layout, kind, south, i - di, j - dj))
                        continue;

                    int ei = i, ej = j;
                    while (Continues(layout, kind, south, ei, ej))
                    {
                        ei += di;
                        ej += dj;
                    }

                    foreach (float offset in offsets)
                        layout.CeilingLines.Add(south
                            ? new CeilingLine { Kind = line, X0 = i * C + offset, X1 = i * C + offset, Y0 = j * C + EndInset, Y1 = (ej + 1) * C - EndInset }
                            : new CeilingLine { Kind = line, X0 = i * C + EndInset, X1 = (ei + 1) * C - EndInset, Y0 = j * C + offset, Y1 = j * C + offset });
                }
        }

        // True when a line in bay (i, j) carries on into the next bay east or south.
        static bool Continues(FloorLayout layout, CeilingKind kind, bool south, int i, int j)
        {
            int ni = south ? i : i + 1, nj = south ? j + 1 : j;
            if (!layout.InPlate(i, j) || !layout.InPlate(ni, nj))
                return false;
            int a = layout.CellIndex(i, j), b = layout.CellIndex(ni, nj);
            return layout.CeilingOf[a] == kind && layout.CeilingOf[b] == kind
                && layout.Rooms[layout.RoomOf[a]].CeilingAlongY == south && layout.Rooms[layout.RoomOf[b]].CeilingAlongY == south
                && layout.EdgeOf(i, j, south ? Side.South : Side.East).Kind == EdgeKind.Open;
        }

        /// <summary>Adds a line to <paramref name="faults"/> for anything wrong with the plan: a lit line through a wall or over the wrong ceiling, a corridor with no spine, a bay with no lane or light.</summary>
        public static void Check(FloorLayout layout, List<string> faults)
        {
            int links = 0, laneBays = 0, roomBays = 0;
            for (int j = 0; j < layout.Rows; j++)
                for (int i = 0; i < layout.Cols; i++)
                {
                    CeilingKind kind = layout.CeilingOf[layout.CellIndex(i, j)];
                    if (kind == CeilingKind.Lanes) laneBays++;
                    if (kind == CeilingKind.Tiles || kind == CeilingKind.Panels) roomBays++;
                    if (kind != CeilingKind.Route)
                        continue;
                    if (IsRoute(layout, i + 1, j)) links++;
                    if (IsRoute(layout, i, j + 1)) links++;
                }

            int spanned = 0, laned = 0;
            foreach (CeilingLine line in layout.CeilingLines)
            {
                CeilingKind want = line.Kind == CeilingLineKind.Spine ? CeilingKind.Route : line.Kind == CeilingLineKind.Lane ? CeilingKind.Lanes : CeilingKind.Tiles;
                bool east = line.AlongX;
                int i = (int)(line.X0 / C), j = (int)(line.Y0 / C);
                int last = east ? (int)((line.X1 - 0.01f) / C) : (int)((line.Y1 - 0.01f) / C);
                int bays = last - (east ? i : j) + 1;
                if (line.Kind == CeilingLineKind.Spine) spanned += bays - 1;
                else if (line.Kind == CeilingLineKind.Lane) laned += bays;

                for (int k = 0; k < bays; k++)
                {
                    int ci = east ? i + k : i, cj = east ? j : j + k;
                    if (!layout.InPlate(ci, cj) || layout.CeilingOf[layout.CellIndex(ci, cj)] != want)
                        faults.Add($"A ceiling {line.Kind} runs over a bay that is not {want}.");
                    else if (k < bays - 1 && layout.EdgeOf(ci, cj, east ? Side.East : Side.South).Kind != EdgeKind.Open)
                        faults.Add($"A ceiling {line.Kind} runs through a wall.");
                }
            }

            if (spanned != links)
                faults.Add($"The spine covers {spanned} of {links} corridor links.");
            if (laned != laneBays)
                faults.Add($"Lanes cover {laned} of {laneBays} open hall bays.");
            if (layout.Lights.Count != roomBays)
                faults.Add($"{layout.Lights.Count} room lights for {roomBays} closed room bays.");
        }
    }
}
