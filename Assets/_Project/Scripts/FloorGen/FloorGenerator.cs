using System;
using System.Collections.Generic;

namespace CloseTheDeal.FloorGen
{
    /// <summary>Integer-only random stream, so every machine draws the same numbers.</summary>
    public struct FloorRandom
    {
        uint _state;

        public FloorRandom(uint seed, uint floor, uint attempt)
        {
            uint h = seed * 0x9E3779B1u ^ (floor + 1u) * 0x85EBCA6Bu ^ (attempt + 1u) * 0xC2B2AE35u;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            _state = h == 0 ? 0x1234567u : h;
        }

        public uint Next()
        {
            uint x = _state;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>A whole number from min up to but not including max.</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(Next() % (uint)(max - min));

        public bool Percent(int chance) => (int)(Next() % 100u) < chance;

        public void Shuffle<T>(List<T> list)
        {
            for (int n = list.Count - 1; n > 0; n--)
            {
                int k = Range(0, n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }
    }

    /// <summary>Seed and floor number in, layout out. Pure: touches no scene object. Design: docs/systems/FLOOR_GEN.md.</summary>
    public static class FloorGenerator
    {
        public const int MaxAttempts = 24;
        const int Unassigned = -2;
        const float LiftCarInset = 2.075f, LiftLeafInset = 0.225f, LiftFrontStep = 0.75f;
        const float Soffit = 4.3f, FloorToFloor = 4.5f;

        struct Rect
        {
            public int X0, Y0, X1, Y1;
            public Rect(int x0, int y0, int x1, int y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
            public int W => X1 - X0 + 1;
            public int H => Y1 - Y0 + 1;
            public bool Empty => W <= 0 || H <= 0;
        }

        struct LiftOption
        {
            public int Cell, DoorSide;
            public bool Inside;
        }

        /// <summary>The floor every machine builds for this seed and floor number (1 = the ground floor).</summary>
        public static FloorLayout Generate(uint seed, int floor, FloorGenProfile profile)
        {
            FloorLayout best = null;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                FloorLayout layout = Build(seed, floor, attempt, profile);
                if (layout.Valid)
                    return layout;
                if (best == null || layout.Problems.Count < best.Problems.Count)
                    best = layout;
            }

            return best;
        }

        static FloorLayout Build(uint seed, int floor, int attempt, FloorGenProfile profile)
        {
            var rng = new FloorRandom(seed, (uint)floor, (uint)attempt);
            bool top = floor >= profile.FloorCount;
            var layout = new FloorLayout { Seed = seed, Floor = floor, Attempt = attempt, Cols = profile.Cols, Rows = profile.Rows };
            int cols = layout.Cols, rows = layout.Rows;
            layout.RoomOf = new int[cols * rows];
            Array.Fill(layout.RoomOf, Unassigned);
            layout.NorthEdges = new FloorEdge[cols * (rows + 1)];
            layout.WestEdges = new FloorEdge[(cols + 1) * rows];
            var rects = new List<Rect>();
            var inBand = new List<bool>();

            int x0 = rng.Range(1, 3), x1 = cols - 1 - rng.Range(1, 3);
            int y0 = top ? 2 : rng.Range(1, 3), y1 = rows - 1 - rng.Range(1, 3);
            if (y1 - y0 < 2) y1 = rows - 2;
            if (x1 - x0 < 2) { x0 = 1; x1 = cols - 2; }
            layout.RingX0 = x0; layout.RingY0 = y0; layout.RingX1 = x1; layout.RingY1 = y1;

            FloorRoom ring = AddRoom(layout, rects, inBand, RoomKind.Ring, new Rect(x0, y0, x1, y1), false, fill: false);
            for (int i = x0; i <= x1; i++)
                for (int j = y0; j <= y1; j++)
                    if (i == x0 || i == x1 || j == y0 || j == y1)
                        Claim(layout, ring, i, j);

            FloorRoom court = null;
            var blocks = new List<Rect>();
            CutCore(layout, rects, inBand, ref court, ref rng, new Rect(x0 + 1, y0 + 1, x1 - 1, y1 - 1), blocks);
            foreach (Rect block in blocks)
                SplitRooms(layout, rects, inBand, ref rng, block, profile.MaxCoreRoomCells);

            int officeStart = top ? rng.Range(x0 + 1, Math.Max(x0 + 2, x1 - 1)) : -1;
            BandAlong(layout, rects, inBand, ref rng, profile, 0, y0 - 1, x0, x1, officeStart);
            BandAlong(layout, rects, inBand, ref rng, profile, y1 + 1, rows - 1, x0, x1, -1);
            BandAcross(layout, rects, inBand, ref rng, profile, 0, x0 - 1, y0, y1);
            BandAcross(layout, rects, inBand, ref rng, profile, x1 + 1, cols - 1, y0, y1);

            if (top && layout.OfficeRoom < 0)
                layout.Problems.Add("No room for the manager's office.");

            PlaceLifts(layout, rects, ref rng, profile, top, floor);
            NameRooms(layout, inBand, ref rng, profile);
            SetEdges(layout, ref rng, profile);
            AddDoors(layout, ref rng);
            EmitStructure(layout);

            layout.Nav = new FloorNav(layout);
            if (!layout.Nav.AllConnected())
                layout.Problems.Add("The bare floor is not one connected area.");
            FloorFitOut.Place(layout, profile, ref rng, KeepClear(layout));
            FloorFitOut.Settle(layout, profile, ref rng);
            AddMarks(layout, ref rng);
            var allLoot = new List<FloorSpot>();
            PlaceSpots(layout, ref rng, profile, allLoot);
            PlaceStarts(layout, ref rng, profile, floor, allLoot);
            Check(layout, profile, floor, top);
            layout.Valid = layout.Problems.Count == 0;
            return layout;
        }

        static FloorRoom AddRoom(FloorLayout layout, List<Rect> rects, List<bool> inBand, RoomKind kind, Rect rect, bool band, bool fill = true)
        {
            var room = new FloorRoom { Index = layout.Rooms.Count, Kind = kind };
            layout.Rooms.Add(room);
            rects.Add(rect);
            inBand.Add(band);
            if (fill)
                for (int j = rect.Y0; j <= rect.Y1; j++)
                    for (int i = rect.X0; i <= rect.X1; i++)
                        Claim(layout, room, i, j);
            return room;
        }

        static void Claim(FloorLayout layout, FloorRoom room, int i, int j)
        {
            int cell = layout.CellIndex(i, j);
            layout.RoomOf[cell] = room.Index;
            room.Cells.Add(cell);
        }

        static void CutCore(FloorLayout layout, List<Rect> rects, List<bool> inBand, ref FloorRoom court, ref FloorRandom rng, Rect rect, List<Rect> blocks)
        {
            if (rect.Empty)
                return;

            bool thick = Math.Min(rect.W, rect.H) > 2;
            bool wide = !thick && Math.Max(rect.W, rect.H) >= 5 && rng.Percent(50);
            if (!thick && !wide)
            {
                blocks.Add(rect);
                return;
            }

            court ??= AddRoom(layout, rects, inBand, RoomKind.Court, rect, false, fill: false);
            bool cutRows = thick ? rect.H <= rect.W : rect.H > rect.W;
            if (cutRows)
            {
                int margin = thick ? 1 : 2;
                int at = rng.Range(rect.Y0 + margin, rect.Y1 - margin + 1);
                for (int i = rect.X0; i <= rect.X1; i++)
                    Claim(layout, court, i, at);
                CutCore(layout, rects, inBand, ref court, ref rng, new Rect(rect.X0, rect.Y0, rect.X1, at - 1), blocks);
                CutCore(layout, rects, inBand, ref court, ref rng, new Rect(rect.X0, at + 1, rect.X1, rect.Y1), blocks);
            }
            else
            {
                int margin = thick ? 1 : 2;
                int at = rng.Range(rect.X0 + margin, rect.X1 - margin + 1);
                for (int j = rect.Y0; j <= rect.Y1; j++)
                    Claim(layout, court, at, j);
                CutCore(layout, rects, inBand, ref court, ref rng, new Rect(rect.X0, rect.Y0, at - 1, rect.Y1), blocks);
                CutCore(layout, rects, inBand, ref court, ref rng, new Rect(at + 1, rect.Y0, rect.X1, rect.Y1), blocks);
            }
        }

        static void SplitRooms(FloorLayout layout, List<Rect> rects, List<bool> inBand, ref FloorRandom rng, Rect rect, int maxCells)
        {
            int area = rect.W * rect.H;
            if (area <= maxCells && (area <= 2 || rng.Percent(60)))
            {
                AddRoom(layout, rects, inBand, RoomKind.OpenOffice, rect, false);
                return;
            }

            if (rect.W >= rect.H)
            {
                int at = rng.Range(rect.X0 + 1, rect.X1 + 1);
                SplitRooms(layout, rects, inBand, ref rng, new Rect(rect.X0, rect.Y0, at - 1, rect.Y1), maxCells);
                SplitRooms(layout, rects, inBand, ref rng, new Rect(at, rect.Y0, rect.X1, rect.Y1), maxCells);
            }
            else
            {
                int at = rng.Range(rect.Y0 + 1, rect.Y1 + 1);
                SplitRooms(layout, rects, inBand, ref rng, new Rect(rect.X0, rect.Y0, rect.X1, at - 1), maxCells);
                SplitRooms(layout, rects, inBand, ref rng, new Rect(rect.X0, at, rect.X1, rect.Y1), maxCells);
            }
        }

        // North and south bands run the full width; their end rooms must reach a ring column to have a way out.
        static void BandAlong(FloorLayout layout, List<Rect> rects, List<bool> inBand, ref FloorRandom rng, FloorGenProfile profile,
                              int rowFrom, int rowTo, int ringX0, int ringX1, int officeStart)
        {
            if (rowTo < rowFrom)
                return;

            var cuts = new List<int>();
            int cur = Math.Min(ringX0 + 1 + rng.Range(0, 2), ringX1);
            if (officeStart >= 0)
                cur = Math.Min(cur, officeStart);
            cuts.Add(cur);
            while (cur < ringX1)
            {
                int next = Math.Min(ringX1, cur + rng.Range(1, profile.MaxBandRoomLength + 1));
                if (officeStart >= 0)
                {
                    if (cur < officeStart) next = Math.Min(next, officeStart);
                    else if (cur == officeStart) next = officeStart + 2;
                }
                if (next <= cur)
                    break;
                cur = next;
                cuts.Add(cur);
            }

            int from = 0;
            for (int k = 0; k <= cuts.Count; k++)
            {
                int to = k < cuts.Count ? cuts[k] : layout.Cols;
                if (to > from)
                {
                    bool office = from == officeStart && to == officeStart + 2 && rowTo - rowFrom == 1;
                    FloorRoom room = AddRoom(layout, rects, inBand, office ? RoomKind.ManagersOffice : RoomKind.WindowHall,
                                             new Rect(from, rowFrom, to - 1, rowTo), true);
                    if (office)
                    {
                        layout.HasOffice = true;
                        layout.OfficeRoom = room.Index;
                        layout.OfficeX0 = from * FloorLayout.Cell;
                        layout.OfficeY0 = rowFrom * FloorLayout.Cell;
                        layout.OfficeX1 = to * FloorLayout.Cell;
                        layout.OfficeY1 = (rowTo + 1) * FloorLayout.Cell;
                    }
                }
                from = to;
            }
        }

        static void BandAcross(FloorLayout layout, List<Rect> rects, List<bool> inBand, ref FloorRandom rng, FloorGenProfile profile,
                               int colFrom, int colTo, int ringY0, int ringY1)
        {
            if (colTo < colFrom)
                return;

            int cur = ringY0;
            while (cur <= ringY1)
            {
                int next = Math.Min(ringY1 + 1, cur + rng.Range(1, profile.MaxBandRoomLength + 1));
                AddRoom(layout, rects, inBand, RoomKind.WindowHall, new Rect(colFrom, cur, colTo, next - 1), true);
                cur = next;
            }
        }

        static void PlaceLifts(FloorLayout layout, List<Rect> rects, ref FloorRandom rng, FloorGenProfile profile, bool top, int floor)
        {
            int goalCell = -1;
            if (!top)
            {
                List<LiftOption> options = LiftOptions(layout, rects, -1, 0);
                bool inside = rng.Percent(profile.LiftInsideRoomPercent);
                List<LiftOption> wanted = options.FindAll(o => o.Inside == inside);
                if (wanted.Count == 0)
                    wanted = options;
                if (wanted.Count == 0)
                {
                    layout.Problems.Add("Nowhere to put the exit lift.");
                    return;
                }

                LiftOption exit = wanted[rng.Range(0, wanted.Count)];
                layout.ExitLift = Carve(layout, exit);
                layout.HasExitLift = true;
                goalCell = exit.Cell;
            }
            else if (layout.OfficeRoom >= 0)
            {
                goalCell = layout.Rooms[layout.OfficeRoom].Cells[0];
            }

            if (floor < 2 || goalCell < 0)
                return;

            int far = (layout.Cols + layout.Rows) / 3;
            List<LiftOption> arrivals = LiftOptions(layout, rects, goalCell, far);
            if (arrivals.Count == 0)
                arrivals = LiftOptions(layout, rects, goalCell, 2);
            if (arrivals.Count == 0)
            {
                layout.Problems.Add("Nowhere to put the arrival lift.");
                return;
            }

            layout.ArrivalLift = Carve(layout, arrivals[rng.Range(0, arrivals.Count)]);
            layout.HasArrivalLift = true;
        }

        static List<LiftOption> LiftOptions(FloorLayout layout, List<Rect> rects, int awayFromCell, int minCells)
        {
            var options = new List<LiftOption>();
            foreach (FloorRoom room in layout.Rooms)
            {
                if (room.IsCorridor || room.Kind == RoomKind.ManagersOffice || room.Cells.Count < 2)
                    continue;

                Rect rect = rects[room.Index];
                if (room.Cells.Count != rect.W * rect.H)
                    continue; // Already holds a lift.

                for (int corner = 0; corner < 4; corner++)
                {
                    int i = (corner & 1) == 0 ? rect.X0 : rect.X1, j = (corner & 2) == 0 ? rect.Y0 : rect.Y1;
                    if ((corner & 1) == 1 && rect.W == 1 || (corner & 2) == 2 && rect.H == 1)
                        continue;

                    int cell = layout.CellIndex(i, j);
                    if (awayFromCell >= 0)
                    {
                        int ai = awayFromCell % layout.Cols, aj = awayFromCell / layout.Cols;
                        if (Math.Abs(ai - i) + Math.Abs(aj - j) < minCells || layout.RoomOf[awayFromCell] == room.Index)
                            continue;
                    }

                    for (int side = 0; side < 4; side++)
                    {
                        int ni = i + Side.Dx[side], nj = j + Side.Dy[side];
                        int other = layout.RoomAt(ni, nj);
                        if (!layout.InPlate(ni, nj) || other < 0)
                            continue;
                        bool inside = other == room.Index;
                        if (inside || layout.Rooms[other].IsCorridor)
                            options.Add(new LiftOption { Cell = cell, DoorSide = side, Inside = inside });
                    }
                }
            }

            return options;
        }

        static FloorLift Carve(FloorLayout layout, LiftOption option)
        {
            int i = option.Cell % layout.Cols, j = option.Cell / layout.Cols;
            layout.Rooms[layout.RoomOf[option.Cell]].Cells.Remove(option.Cell);
            layout.RoomOf[option.Cell] = FloorLayout.NoRoom;

            float cx = (i + 0.5f) * FloorLayout.Cell, cy = (j + 0.5f) * FloorLayout.Cell;
            float ox = Side.Dx[option.DoorSide], oy = Side.Dy[option.DoorSide];
            float doorX = cx + ox * FloorLayout.Cell * 0.5f, doorY = cy + oy * FloorLayout.Cell * 0.5f;
            return new FloorLift
            {
                CellX = i, CellY = j, DoorSide = option.DoorSide,
                CarX = doorX - ox * LiftCarInset, CarY = doorY - oy * LiftCarInset, CarYaw = option.DoorSide * 90f,
                DoorX = doorX - ox * LiftLeafInset, DoorY = doorY - oy * LiftLeafInset,
                FrontX = doorX + ox * LiftFrontStep, FrontY = doorY + oy * LiftFrontStep
            };
        }

        static void NameRooms(FloorLayout layout, List<bool> inBand, ref FloorRandom rng, FloorGenProfile profile)
        {
            var used = new Dictionary<string, int>();
            foreach (FloorRoom room in layout.Rooms)
            {
                if (room.Kind == RoomKind.Ring) { room.Name = "Ring"; continue; }
                if (room.Kind == RoomKind.Court) { room.Name = "Court"; continue; }
                if (room.Kind == RoomKind.ManagersOffice)
                {
                    room.Name = "Manager's Office";
                    room.Front = EdgeKind.Solid;
                    continue;
                }

                bool band = inBand[room.Index];
                int cells = room.Cells.Count;
                room.Open = rng.Percent(band ? profile.OpenBandRoomPercent : profile.OpenCoreRoomPercent);
                if (room.Open)
                {
                    room.Kind = band && cells >= 2 ? RoomKind.WindowHall : RoomKind.Lounge;
                }
                else
                {
                    int roll = rng.Range(0, 100);
                    room.Kind = cells > 3
                        ? roll < 70 ? RoomKind.OpenOffice : RoomKind.Meeting
                        : cells >= 2
                        ? roll < 50 ? RoomKind.OpenOffice : roll < 75 ? RoomKind.Meeting : roll < 87 ? RoomKind.Kitchen : RoomKind.Archive
                        : roll < 30 ? RoomKind.Meeting : roll < 55 ? RoomKind.Kitchen : roll < 80 ? RoomKind.Print : RoomKind.Archive;
                    int finish = rng.Range(0, 100);
                    room.Front = room.Kind switch
                    {
                        RoomKind.OpenOffice => finish < 55 ? EdgeKind.Glass : EdgeKind.Frost,
                        RoomKind.Meeting => finish < 40 ? EdgeKind.Fluted : finish < 75 ? EdgeKind.Louvre : EdgeKind.Frost,
                        RoomKind.Kitchen => finish < 50 ? EdgeKind.Louvre : EdgeKind.Solid,
                        _ => EdgeKind.Solid
                    };
                }

                string name = Compass(layout, room) + " " + Label(room.Kind);
                used.TryGetValue(name, out int count);
                used[name] = count + 1;
                room.Name = count == 0 ? name : name + " " + (count + 1);
            }
        }

        static string Label(RoomKind kind) => kind switch
        {
            RoomKind.WindowHall => "Window Hall",
            RoomKind.Lounge => "Lounge",
            RoomKind.OpenOffice => "Office",
            RoomKind.Meeting => "Meeting Room",
            RoomKind.Kitchen => "Kitchen",
            RoomKind.Print => "Print Room",
            RoomKind.Archive => "Archive",
            _ => kind.ToString()
        };

        static string Compass(FloorLayout layout, FloorRoom room)
        {
            int sumX = 0, sumY = 0;
            foreach (int cell in room.Cells)
            {
                sumX += 2 * (cell % layout.Cols) + 1;
                sumY += 2 * (cell / layout.Cols) + 1;
            }

            // Thirds of the plate, in whole numbers: centre sums are compared against cols and rows scaled by the cell count.
            int n = room.Cells.Count;
            string ns = 3 * sumY < 2 * layout.Rows * n ? "North" : 3 * sumY > 4 * layout.Rows * n ? "South" : "";
            string ew = 3 * sumX < 2 * layout.Cols * n ? "West" : 3 * sumX > 4 * layout.Cols * n ? "East" : "";
            if (ns.Length > 0 && ew.Length > 0) return ns + "-" + ew;
            if (ns.Length > 0) return ns;
            return ew.Length > 0 ? ew : "Central";
        }

        static void SetEdges(FloorLayout layout, ref FloorRandom rng, FloorGenProfile profile)
        {
            for (int j = 0; j <= layout.Rows; j++)
                for (int i = 0; i < layout.Cols; i++)
                    layout.NorthEdges[i + j * layout.Cols] = EdgeBetween(layout, ref rng, profile, i, j - 1, i, j, Side.South);
            for (int j = 0; j < layout.Rows; j++)
                for (int i = 0; i <= layout.Cols; i++)
                    layout.WestEdges[i + j * (layout.Cols + 1)] = EdgeBetween(layout, ref rng, profile, i - 1, j, i, j, Side.East);
        }

        // (li, lj) is the lower cell (north or west), (hi, hj) the higher; sideOfLow is the lower cell's side on this edge.
        static FloorEdge EdgeBetween(FloorLayout layout, ref FloorRandom rng, FloorGenProfile profile, int li, int lj, int hi, int hj, int sideOfLow)
        {
            bool lowIn = layout.InPlate(li, lj), highIn = layout.InPlate(hi, hj);
            int low = lowIn ? layout.RoomOf[layout.CellIndex(li, lj)] : FloorLayout.NoRoom;
            int high = highIn ? layout.RoomOf[layout.CellIndex(hi, hj)] : FloorLayout.NoRoom;

            if (lowIn && low < 0)
                return LiftEdge(layout, li, lj, sideOfLow, highIn && high >= 0);
            if (highIn && high < 0)
                return LiftEdge(layout, hi, hj, Side.Opposite(sideOfLow), lowIn && low >= 0);

            if (!lowIn || !highIn)
            {
                FloorRoom room = layout.Rooms[lowIn ? low : high];
                if (room.Kind == RoomKind.ManagersOffice)
                    return new FloorEdge { Kind = EdgeKind.FacadeSolid };
                if (rng.Percent(profile.OpenWindowPercent))
                    return new FloorEdge { Kind = EdgeKind.FacadeWindow };
                return new FloorEdge { Kind = rng.Percent(70) ? EdgeKind.FacadeCurtain : EdgeKind.FacadeSolid };
            }

            if (low == high)
                return new FloorEdge { Kind = EdgeKind.Open };

            FloorRoom a = layout.Rooms[low], b = layout.Rooms[high];
            if (a.IsPublic && b.IsPublic)
                return new FloorEdge { Kind = EdgeKind.Open };

            bool portal = a.Kind == RoomKind.ManagersOffice || b.Kind == RoomKind.ManagersOffice;
            if (!a.IsPublic && !b.IsPublic)
                return new FloorEdge { Kind = EdgeKind.Solid, ThickTowardHigh = a.Kind != RoomKind.ManagersOffice, Portal = portal };
            EdgeKind front = a.IsPublic ? b.Front : a.Front;
            return new FloorEdge { Kind = front, ThickTowardHigh = a.IsPublic, Portal = portal, Accent = !portal && front == EdgeKind.Solid };
        }

        static FloorEdge LiftEdge(FloorLayout layout, int i, int j, int side, bool neighbourWalkable)
        {
            bool door = neighbourWalkable && IsLiftDoor(layout, i, j, side);
            return new FloorEdge { Kind = door ? EdgeKind.LiftDoor : EdgeKind.Shaft };
        }

        static bool IsLiftDoor(FloorLayout layout, int i, int j, int side)
        {
            return layout.HasExitLift && layout.ExitLift.CellX == i && layout.ExitLift.CellY == j && layout.ExitLift.DoorSide == side
                || layout.HasArrivalLift && layout.ArrivalLift.CellX == i && layout.ArrivalLift.CellY == j && layout.ArrivalLift.DoorSide == side;
        }

        static void AddDoors(FloorLayout layout, ref FloorRandom rng)
        {
            var walls = new List<(int i, int j, int side, int other)>();
            var linked = new HashSet<int>();
            foreach (FloorRoom room in layout.Rooms)
            {
                if (room.IsCorridor)
                    continue;

                for (int guard = 0; guard < 4; guard++)
                {
                    walls.Clear();
                    linked.Clear();
                    int exits = 0;
                    bool toPublic = false;
                    foreach (int cell in room.Cells)
                    {
                        int i = cell % layout.Cols, j = cell / layout.Cols;
                        for (int side = 0; side < 4; side++)
                        {
                            int other = layout.RoomAt(i + Side.Dx[side], j + Side.Dy[side]);
                            if (other < 0 || other == room.Index)
                                continue;

                            FloorEdge edge = layout.EdgeOf(i, j, side);
                            if (edge.Passable)
                            {
                                exits++;
                                linked.Add(other);
                                toPublic |= layout.Rooms[other].IsPublic;
                            }
                            else
                            {
                                walls.Add((i, j, side, other));
                            }
                        }
                    }

                    if ((exits >= 2 && toPublic) || walls.Count == 0)
                        break;

                    bool wantPublic = !toPublic || rng.Percent(50);
                    List<(int i, int j, int side, int other)> pick = walls.FindAll(w => layout.Rooms[w.other].IsPublic == wantPublic && !linked.Contains(w.other));
                    if (pick.Count == 0) pick = walls.FindAll(w => !linked.Contains(w.other));
                    if (pick.Count == 0) pick = walls;
                    if (!toPublic)
                    {
                        List<(int i, int j, int side, int other)> open = walls.FindAll(w => layout.Rooms[w.other].IsPublic);
                        if (open.Count > 0) pick = open;
                    }

                    (int di, int dj, int dside, _) = pick[rng.Range(0, pick.Count)];
                    FloorEdge wall = layout.EdgeOf(di, dj, dside);
                    wall.DoorAt = wall.Kind == EdgeKind.Solid ? (byte)2 : rng.Percent(50) ? (byte)1 : (byte)3;
                    layout.SetEdge(di, dj, dside, wall);
                }
            }

            foreach (FloorRoom room in layout.Rooms)
            {
                room.Exits = 0;
                foreach (int cell in room.Cells)
                {
                    int i = cell % layout.Cols, j = cell / layout.Cols;
                    for (int side = 0; side < 4; side++)
                    {
                        int other = layout.RoomAt(i + Side.Dx[side], j + Side.Dy[side]);
                        if (other >= 0 && other != room.Index && layout.EdgeOf(i, j, side).Passable)
                            room.Exits++;
                    }
                }
            }
        }

        static void EmitStructure(FloorLayout layout)
        {
            const float C = FloorLayout.Cell;
            float width = layout.Width, depth = layout.Depth;
            for (int j = 0; j < layout.Rows; j++)
                for (int i = 0; i < layout.Cols; i++)
                {
                    int room = layout.RoomOf[layout.CellIndex(i, j)];
                    FloorRoom r = room >= 0 ? layout.Rooms[room] : null;
                    // Routes and open halls keep a bare soffit whose light line follows the way you walk.
                    if (r != null && !r.IsPublic)
                        Add(layout, KitPiece.Slab_4x4, i * C, j * C, 0f, FloorToFloor);
                    else if (r != null && LinesRunSouth(layout, r, i, j))
                        Add(layout, KitPiece.Slab_4x4_LightLine, (i + 1) * C, j * C, 90f, FloorToFloor);
                    else
                        Add(layout, KitPiece.Slab_4x4_LightLine, i * C, j * C, 0f, FloorToFloor);
                    if (r == null)
                        continue;

                    Add(layout, KitPiece.Slab_4x4, i * C, j * C, 0f);
                    if (r.IsPublic)
                        continue;
                    bool carpeted = r.Kind == RoomKind.OpenOffice || r.Kind == RoomKind.Meeting || r.Kind == RoomKind.ManagersOffice;
                    Add(layout, carpeted ? KitPiece.Ceiling_Panel_4x4 : KitPiece.Ceiling_Grid_4x4, i * C, j * C, 0f, Soffit);
                    layout.Lights.Add(new FloorSpot { X = (i + 0.5f) * C, Y = (j + 0.5f) * C, Room = room });
                    if (carpeted)
                        Add(layout, KitPiece.Carpet_Overlay_4x4, i * C, j * C, 0f);
                }

            for (int i = 0; i < layout.Cols; i++)
            {
                Facade(layout, layout.EdgeOf(i, 0, Side.North).Kind, i * C, 0f, 0f);
                Facade(layout, layout.EdgeOf(i, layout.Rows - 1, Side.South).Kind, (i + 1) * C, depth, 180f);
                if (i > 0)
                {
                    if (Windowed(layout, i - 1, 0, i, 0, Side.North)) Add(layout, KitPiece.Column_Facade, i * C, 0f, 0f);
                    if (Windowed(layout, i - 1, layout.Rows - 1, i, layout.Rows - 1, Side.South)) Add(layout, KitPiece.Column_Facade, i * C, depth, 180f);
                }
            }

            for (int j = 0; j < layout.Rows; j++)
            {
                Facade(layout, layout.EdgeOf(0, j, Side.West).Kind, 0f, (j + 1) * C, 270f);
                Facade(layout, layout.EdgeOf(layout.Cols - 1, j, Side.East).Kind, width, j * C, 90f);
                if (j > 0)
                {
                    if (Windowed(layout, 0, j - 1, 0, j, Side.West)) Add(layout, KitPiece.Column_Facade, 0f, j * C, 270f);
                    if (Windowed(layout, layout.Cols - 1, j - 1, layout.Cols - 1, j, Side.East)) Add(layout, KitPiece.Column_Facade, width, j * C, 90f);
                }
            }

            Add(layout, KitPiece.Column_CornerL, 0f, 0f, 0f);
            Add(layout, KitPiece.Column_CornerR, width, 0f, 0f);
            Add(layout, KitPiece.Column_CornerR, 0f, depth, 180f);
            Add(layout, KitPiece.Column_CornerL, width, depth, 180f);

            for (int j = 1; j < layout.Rows; j++)
                for (int i = 0; i < layout.Cols; i++)
                {
                    FloorEdge edge = layout.NorthEdges[i + j * layout.Cols];
                    if (edge.ThickTowardHigh) Wall(layout, edge, i * C, j * C, 0f, 1f, 0f, false);
                    else Wall(layout, edge, (i + 1) * C, j * C, 180f, -1f, 0f, true);
                }

            for (int j = 0; j < layout.Rows; j++)
                for (int i = 1; i < layout.Cols; i++)
                {
                    FloorEdge edge = layout.WestEdges[i + j * (layout.Cols + 1)];
                    if (edge.ThickTowardHigh) Wall(layout, edge, i * C, (j + 1) * C, 270f, 0f, -1f, true);
                    else Wall(layout, edge, i * C, j * C, 90f, 0f, 1f, false);
                }

            if (layout.HasExitLift) Shaft(layout, layout.ExitLift);
            if (layout.HasArrivalLift) Shaft(layout, layout.ArrivalLift);
        }

        // A corridor bay follows the corridor. An open hall takes one direction for the whole room: along its windows, or its longer side.
        static bool LinesRunSouth(FloorLayout layout, FloorRoom room, int i, int j)
        {
            if (room.IsCorridor)
                return Corridor(layout, i, j - 1) + Corridor(layout, i, j + 1) > Corridor(layout, i - 1, j) + Corridor(layout, i + 1, j);

            int i0 = layout.Cols, i1 = -1, j0 = layout.Rows, j1 = -1;
            foreach (int cell in room.Cells)
            {
                int ci = cell % layout.Cols, cj = cell / layout.Cols;
                i0 = Math.Min(i0, ci); i1 = Math.Max(i1, ci);
                j0 = Math.Min(j0, cj); j1 = Math.Max(j1, cj);
            }

            if (j1 < layout.RingY0 || j0 > layout.RingY1) return false;
            if (i1 < layout.RingX0 || i0 > layout.RingX1) return true;
            return j1 - j0 > i1 - i0;
        }

        static int Corridor(FloorLayout layout, int i, int j)
        {
            int room = layout.RoomAt(i, j);
            return room >= 0 && layout.Rooms[room].IsCorridor ? 1 : 0;
        }

        static bool Windowed(FloorLayout layout, int ai, int aj, int bi, int bj, int side)
        {
            return layout.EdgeOf(ai, aj, side).Kind == EdgeKind.FacadeWindow || layout.EdgeOf(bi, bj, side).Kind == EdgeKind.FacadeWindow;
        }

        static void Facade(FloorLayout layout, EdgeKind kind, float x, float y, float yaw)
        {
            switch (kind)
            {
                case EdgeKind.FacadeWindow: Add(layout, KitPiece.Window_4m, x, y, yaw); break;
                case EdgeKind.FacadeCurtain: Add(layout, KitPiece.Wall_Curtain_4m, x, y, yaw); break;
                case EdgeKind.FacadeSolid: Add(layout, KitPiece.Wall_Solid_4m, x, y, yaw); break;
            }
        }

        // (x, y) is where the run starts, (rx, ry) its direction; reversed means the run goes from the edge's far end back to its start.
        static void Wall(FloorLayout layout, FloorEdge edge, float x, float y, float yaw, float rx, float ry, bool reversed)
        {
            if (edge.Kind == EdgeKind.Open || edge.Kind >= EdgeKind.FacadeWindow)
                return;

            if (edge.Kind == EdgeKind.Solid)
            {
                KitPiece piece = edge.DoorAt != 0
                    ? (edge.Portal ? KitPiece.Wall_Door_4m_PortalHead : KitPiece.Wall_Door_4m)
                    : (edge.Portal || edge.Accent ? KitPiece.Wall_Solid_4m_Cobalt : KitPiece.Wall_Solid_4m);
                Add(layout, piece, x, y, yaw);
                return;
            }

            (KitPiece full, KitPiece half) = edge.Kind switch
            {
                EdgeKind.Glass => (KitPiece.Partition_Glass_4m, KitPiece.Partition_Glass_2m),
                EdgeKind.Frost => (KitPiece.Partition_Frost_4m, KitPiece.Partition_Frost_2m),
                EdgeKind.Fluted => (KitPiece.Partition_Fluted_4m, KitPiece.Partition_Fluted_2m),
                _ => (KitPiece.Partition_Louvre_4m, KitPiece.Partition_Louvre_2m)
            };
            if (edge.DoorAt == 0)
            {
                Add(layout, full, x, y, yaw);
                return;
            }

            bool doorFirst = (edge.DoorAt == 1) != reversed;
            float doorAt = doorFirst ? 0f : 2f, plainAt = doorFirst ? 2f : 0f;
            Add(layout, KitPiece.Partition_Glass_Door_2m, x + rx * doorAt, y + ry * doorAt, yaw);
            Add(layout, half, x + rx * plainAt, y + ry * plainAt, yaw);
        }

        static void Shaft(FloorLayout layout, FloorLift lift)
        {
            const float C = FloorLayout.Cell;
            float x0 = lift.CellX * C, y0 = lift.CellY * C, x1 = x0 + C, y1 = y0 + C;
            for (int side = 0; side < 4; side++)
            {
                KitPiece piece = side == lift.DoorSide ? KitPiece.Lift_Door_Frame_4m_1F : KitPiece.Lift_Shaft_Wall_4m_Solid;
                switch (side)
                {
                    case Side.North: Add(layout, piece, x0, y0, 0f); break;
                    case Side.East: Add(layout, piece, x1, y0, 90f); break;
                    case Side.South: Add(layout, piece, x1, y1, 180f); break;
                    default: Add(layout, piece, x0, y1, 270f); break;
                }
            }
        }

        static void Add(FloorLayout layout, KitPiece piece, float x, float y, float yaw, float height = 0f)
        {
            layout.Pieces.Add(new KitPlacement { Piece = piece, X = x, Y = y, Height = height, Yaw = yaw, Still = true });
        }

        // The metre in front of every door, both sides, stays free of furniture.
        static bool[] KeepClear(FloorLayout layout)
        {
            FloorNav nav = layout.Nav;
            var clear = new bool[nav.Count];
            const int P = FloorNav.PerCell;
            for (int j = 0; j < layout.Rows; j++)
                for (int i = 0; i < layout.Cols; i++)
                    for (int side = 0; side < 4; side++)
                    {
                        FloorEdge edge = layout.EdgeOf(i, j, side);
                        int door = edge.Kind == EdgeKind.LiftDoor ? 2 : edge.DoorAt;
                        if (door == 0)
                            continue;

                        for (int along = 2 * door - 2; along <= 2 * door + 1; along++)
                            for (int deep = 0; deep < 3; deep++)
                            {
                                int a = side == Side.North || side == Side.South ? i * P + along : side == Side.West ? i * P + deep : i * P + P - 1 - deep;
                                int b = side == Side.West || side == Side.East ? j * P + along : side == Side.North ? j * P + deep : j * P + P - 1 - deep;
                                clear[a + b * nav.W] = true;
                            }
                    }

            return clear;
        }

        const float WallThick = 0.2f, MarkGap = 0.008f;
        const int LightboxPercent = 40, PosterPercent = 40, PosterCount = 12;
        const float LightboxInset = 0.2f;
        const float PosterWidth = 0.9f, PosterTall = 1.2f, PosterHeight = 1.7f;
        const float NumberSize = 2.6f, NumberHeight = 2.2f;
        const float NameWidth = 2.4f, NameTall = 0.22f, NameHeight = 2.5f;
        const float SloganTall = 0.62f, SloganHeight = 3.95f;
        const int SloganPicks = 1000;

        // Posters on bare outside walls, the floor number on each coloured room, and a name over every door off a walkway.
        static void AddMarks(FloorLayout layout, ref FloorRandom rng)
        {
            const float C = FloorLayout.Cell;
            var numbered = new HashSet<int>();
            string number = layout.Floor.ToString("00");
            for (int j = 0; j < layout.Rows; j++)
                for (int i = 0; i < layout.Cols; i++)
                {
                    int room = layout.RoomOf[layout.CellIndex(i, j)];
                    if (room < 0)
                        continue;

                    FloorRoom here = layout.Rooms[room];
                    for (int side = 0; side < 4; side++)
                    {
                        FloorEdge edge = layout.EdgeOf(i, j, side);
                        int dx = Side.Dx[side], dy = Side.Dy[side];
                        float mx = (i + 0.5f + dx * 0.5f) * C, my = (j + 0.5f + dy * 0.5f) * C;
                        if (edge.Kind == EdgeKind.FacadeSolid)
                        {
                            if (here.Kind == RoomKind.ManagersOffice)
                                continue;
                            int roll = rng.Range(0, 100);
                            if (roll < LightboxPercent && WallIsBare(layout.Nav, i, j, side, 0, FloorNav.PerCell - 1))
                                AddLightbox(layout, i, j, side);
                            else if (roll < LightboxPercent + PosterPercent && WallIsBare(layout.Nav, i, j, side, 2, 5))
                                layout.Marks.Add(new WallMark
                                {
                                    Kind = MarkKind.Poster, X = mx - dx * (WallThick + MarkGap), Y = my - dy * (WallThick + MarkGap),
                                    Height = PosterHeight, Width = PosterWidth, Tall = PosterTall, NormalX = -dx, NormalY = -dy,
                                    Index = rng.Range(0, PosterCount)
                                });
                            continue;
                        }

                        int other = layout.RoomAt(i + dx, j + dy);
                        if (!here.IsPublic || other < 0 || layout.Rooms[other].IsPublic)
                            continue;

                        if (edge.DoorAt != 0)
                        {
                            bool alongX = dx == 0;
                            layout.Marks.Add(new WallMark
                            {
                                Kind = MarkKind.RoomName, X = (alongX ? i * C + edge.DoorAt : mx) - dx * MarkGap, Y = (alongX ? my : j * C + edge.DoorAt) - dy * MarkGap,
                                Height = NameHeight, Width = NameWidth, Tall = NameTall, NormalX = -dx, NormalY = -dy,
                                Index = edge.Kind == EdgeKind.Solid ? 0 : 1, Text = layout.Rooms[other].Name
                            });
                        }
                        else if (edge.Accent)
                        {
                            layout.Marks.Add(new WallMark
                            {
                                Kind = MarkKind.Slogan, X = mx - dx * MarkGap, Y = my - dy * MarkGap,
                                Height = SloganHeight, Width = C, Tall = SloganTall, NormalX = -dx, NormalY = -dy, Index = rng.Range(0, SloganPicks)
                            });
                            if (numbered.Add(other))
                                layout.Marks.Add(new WallMark
                                {
                                    Kind = MarkKind.FloorNumber, X = mx - dx * MarkGap, Y = my - dy * MarkGap,
                                    Height = NumberHeight, Width = NumberSize, Tall = NumberSize, NormalX = -dx, NormalY = -dy, Text = number
                                });
                        }
                    }
                }
        }

        // The lightbox hangs from one end and faces along its own +Z, so each side of the plate has its own corner and turn.
        static void AddLightbox(FloorLayout layout, int i, int j, int side)
        {
            const float C = FloorLayout.Cell;
            float x0 = i * C + LightboxInset, x1 = (i + 1) * C - LightboxInset, y0 = j * C + LightboxInset, y1 = (j + 1) * C - LightboxInset;
            (float x, float y, float yaw) = side switch
            {
                Side.North => (x1, y0, 180f),
                Side.South => (x0, y1, 0f),
                Side.West => (x0, y0, 90f),
                _ => (x1, y1, 270f)
            };
            layout.Pieces.Add(new KitPlacement { Piece = KitPiece.Lightbox_Wall_Wave, X = x, Y = y, Yaw = yaw, Still = true });
        }

        // True when nothing stands against this side of the bay between two half-metre steps along it.
        static bool WallIsBare(FloorNav nav, int i, int j, int side, int from, int to)
        {
            const int P = FloorNav.PerCell;
            for (int along = from; along <= to; along++)
                for (int deep = 0; deep < 2; deep++)
                {
                    int a = side == Side.North || side == Side.South ? i * P + along : side == Side.West ? i * P + deep : i * P + P - 1 - deep;
                    int b = side == Side.West || side == Side.East ? j * P + along : side == Side.North ? j * P + deep : j * P + P - 1 - deep;
                    if (nav.Blocked[a + b * nav.W])
                        return false;
                }
            return true;
        }

        static readonly int[] SpawnNodes = { 4, 4, 2, 2, 5, 5, 2, 5, 5, 2 };
        static readonly int[] LootNodes = { 1, 1, 6, 6, 6, 1, 1, 6 };

        static void PlaceSpots(FloorLayout layout, ref FloorRandom rng, FloorGenProfile profile, List<FloorSpot> allLoot)
        {
            FloorNav nav = layout.Nav;
            const int P = FloorNav.PerCell;
            var loot = new List<FloorSpot>();
            for (int j = 0; j < layout.Rows; j++)
                for (int i = 0; i < layout.Cols; i++)
                {
                    int room = layout.RoomOf[layout.CellIndex(i, j)];
                    if (room < 0)
                        continue;

                    for (int k = 0; k < SpawnNodes.Length; k += 2)
                    {
                        int n = i * P + SpawnNodes[k] + (j * P + SpawnNodes[k + 1]) * nav.W;
                        if (nav.Blocked[n])
                            continue;
                        layout.SpawnSpots.Add(new FloorSpot { X = nav.X(n), Y = nav.Y(n), Room = room });
                        break;
                    }

                    if (layout.Rooms[room].IsCorridor)
                        continue;
                    for (int k = 0; k < LootNodes.Length; k += 2)
                    {
                        int n = i * P + LootNodes[k] + (j * P + LootNodes[k + 1]) * nav.W;
                        if (!nav.Blocked[n])
                            loot.Add(new FloorSpot { X = nav.X(n), Y = nav.Y(n), Room = room });
                    }
                }

            rng.Shuffle(loot);
            allLoot.AddRange(loot);
            float spacing = profile.LootSpacing * profile.LootSpacing;
            foreach (FloorSpot spot in loot)
            {
                if (layout.LootSpots.Count >= profile.MaxLootSpots)
                    break;
                bool crowded = false;
                foreach (FloorSpot kept in layout.LootSpots)
                    crowded |= (kept.X - spot.X) * (kept.X - spot.X) + (kept.Y - spot.Y) * (kept.Y - spot.Y) < spacing;
                if (!crowded)
                    layout.LootSpots.Add(spot);
            }
        }

        static readonly int[] GroupOffsets = { 0, 0, 2, 0, 0, 2, -2, 0, 0, -2, 2, 2, -2, -2, 2, -2, -2, 2 };

        static void PlaceStarts(FloorLayout layout, ref FloorRandom rng, FloorGenProfile profile, int floor, List<FloorSpot> allLoot)
        {
            FloorNav nav = layout.Nav;
            int goal = GoalNode(layout);
            if (goal < 0)
                return;

            var toGoal = new int[nav.Count];
            var fromA = new int[nav.Count];
            var fromB = new int[nav.Count];
            nav.Distances(goal, toGoal);
            int tolerance = (int)(profile.FairStartTolerance / FloorNav.Node);
            int minGoal = (int)(profile.MinStartToGoal / FloorNav.Node), minApart = (int)(profile.MinStartApart / FloorNav.Node);

            var spots = new List<FloorSpot>();
            foreach (FloorSpot spot in layout.SpawnSpots)
                if (spot.Room != layout.OfficeRoom && toGoal[nav.NodeAt(spot.X, spot.Y)] >= minGoal && !NearExitLift(layout, spot))
                    spots.Add(spot);
            rng.Shuffle(spots);

            int nodeA = -1, nodeB = -1;
            if (layout.HasArrivalLift)
            {
                nodeA = nav.NodeAt(layout.ArrivalLift.FrontX, layout.ArrivalLift.FrontY);
                nav.Distances(nodeA, fromA);
                nodeB = PickPartner(nav, spots, toGoal, fromA, toGoal[nodeA], tolerance, minApart);
                LiftGroup(layout, layout.ArrivalLift);
            }
            else
            {
                for (int k = 0; k < spots.Count && k < 16 && nodeB < 0; k++)
                {
                    nodeA = nav.NodeAt(spots[k].X, spots[k].Y);
                    nav.Distances(nodeA, fromA);
                    nodeB = PickPartner(nav, spots, toGoal, fromA, toGoal[nodeA], tolerance, minApart);
                }
                if (nodeB >= 0)
                    Group(layout, nav, nodeA, layout.StartA);
            }

            if (nodeA < 0 || nodeB < 0)
            {
                layout.Problems.Add("No pair of starts is fair and far enough apart.");
                return;
            }

            Group(layout, nav, nodeB, layout.StartB);
            nav.Distances(nodeB, fromB);
            layout.StartAToGoal = FloorNav.Metres(toGoal[nodeA]);
            layout.StartBToGoal = FloorNav.Metres(toGoal[nodeB]);
            layout.StartsApart = FloorNav.Metres(fromA[nodeB]);

            if (floor != 1)
                return;

            int minCaseGoal = (int)(profile.MinCaseToLift / FloorNav.Node), minCaseStart = (int)(profile.MinCaseToStart / FloorNav.Node);
            foreach (FloorSpot spot in allLoot)
            {
                int n = nav.NodeAt(spot.X, spot.Y);
                if (toGoal[n] < minCaseGoal || fromA[n] < minCaseStart || fromB[n] < minCaseStart || Math.Abs(fromA[n] - fromB[n]) > tolerance)
                    continue;

                layout.HasBriefcase = true;
                layout.Briefcase = spot;
                layout.CaseToGoal = FloorNav.Metres(toGoal[n]);
                layout.CaseToStartA = FloorNav.Metres(fromA[n]);
                layout.CaseToStartB = FloorNav.Metres(fromB[n]);
                return;
            }

            layout.Problems.Add("No briefcase spot is far from the lift and fair to both starts.");
        }

        // A walk-in start within two bays of the exit lift would give the lift away through one wall.
        static bool NearExitLift(FloorLayout layout, FloorSpot spot)
        {
            if (!layout.HasExitLift)
                return false;
            int i = (int)(spot.X / FloorLayout.Cell), j = (int)(spot.Y / FloorLayout.Cell);
            return Math.Max(Math.Abs(i - layout.ExitLift.CellX), Math.Abs(j - layout.ExitLift.CellY)) < 3;
        }

        static int GoalNode(FloorLayout layout)
        {
            if (layout.HasExitLift)
                return layout.Nav.NodeAt(layout.ExitLift.FrontX, layout.ExitLift.FrontY);
            if (layout.OfficeRoom < 0)
                return -1;
            foreach (FloorSpot spot in layout.SpawnSpots)
                if (spot.Room == layout.OfficeRoom)
                    return layout.Nav.NodeAt(spot.X, spot.Y);
            return -1;
        }

        static int PickPartner(FloorNav nav, List<FloorSpot> spots, int[] toGoal, int[] fromA, int goalFromA, int tolerance, int minApart)
        {
            foreach (FloorSpot spot in spots)
            {
                int n = nav.NodeAt(spot.X, spot.Y);
                if (fromA[n] >= minApart && Math.Abs(toGoal[n] - goalFromA) <= tolerance)
                    return n;
            }

            return -1;
        }

        static void Group(FloorLayout layout, FloorNav nav, int node, List<FloorSpot> into)
        {
            int a = node % nav.W, b = node / nav.W;
            int room = layout.RoomAt(a / FloorNav.PerCell, b / FloorNav.PerCell);
            for (int k = 0; k < GroupOffsets.Length && into.Count < 4; k += 2)
            {
                int na = a + GroupOffsets[k], nb = b + GroupOffsets[k + 1];
                if (na < 0 || nb < 0 || na >= nav.W || nb >= nav.H)
                    continue;
                int n = na + nb * nav.W;
                if (nav.Blocked[n] || layout.RoomAt(na / FloorNav.PerCell, nb / FloorNav.PerCell) != room)
                    continue;
                into.Add(new FloorSpot { X = nav.X(n), Y = nav.Y(n), Room = room });
            }
        }

        static void LiftGroup(FloorLayout layout, FloorLift lift)
        {
            float yaw = lift.CarYaw;
            for (int k = 0; k < 4; k++)
                layout.StartA.Add(new FloorSpot
                {
                    X = lift.CarX + ((k & 1) == 0 ? -0.7f : 0.7f), Y = lift.CarY + ((k & 2) == 0 ? -0.7f : 0.7f),
                    Yaw = yaw, Room = FloorLayout.NoRoom
                });
        }

        static void Check(FloorLayout layout, FloorGenProfile profile, int floor, bool top)
        {
            FloorNav nav = layout.Nav;
            if (!nav.AllConnected())
                layout.Problems.Add("The furnished floor is not one connected area.");

            foreach (FloorRoom room in layout.Rooms)
                if (!room.IsCorridor && room.Cells.Count > 0 && room.Exits < 2)
                    layout.Problems.Add($"{room.Name} has {room.Exits} exit(s); every room needs two.");

            if (!RingHolds(layout))
                layout.Problems.Add("The ring is not a closed loop.");
            if (layout.SpawnSpots.Count < profile.MinSpawnSpots)
                layout.Problems.Add($"Only {layout.SpawnSpots.Count} spawn spots; the floor needs {profile.MinSpawnSpots}.");
            if (top != layout.HasOffice)
                layout.Problems.Add(top ? "The top floor has no manager's office." : "A lower floor has a manager's office.");
            if (!top && !layout.HasExitLift)
                layout.Problems.Add("No exit lift.");
            if (floor >= 2 && !layout.HasArrivalLift)
                layout.Problems.Add("No arrival lift.");
            if (layout.StartA.Count < 4 || layout.StartB.Count < 4)
                layout.Problems.Add("A start has fewer than four places to stand.");
            if (Math.Abs(layout.StartAToGoal - layout.StartBToGoal) > profile.FairStartTolerance)
                layout.Problems.Add("The starts are not the same distance from the goal.");
            if (layout.StartA.Count > 0 && Math.Min(layout.StartAToGoal, layout.StartBToGoal) < profile.MinStartToGoal)
                layout.Problems.Add("A start is too close to the goal.");
        }

        // A loop survives losing any one cell: walk the ring's own nodes with one cell shut and every other cell must still be reached.
        static bool RingHolds(FloorLayout layout)
        {
            FloorNav nav = layout.Nav;
            List<int> cells = layout.Rooms[0].Cells;
            var steps = new int[nav.Count];
            const int P = FloorNav.PerCell;
            foreach (int shut in new[] { cells[0], cells[cells.Count / 2] })
            {
                int from = -1;
                foreach (int cell in cells)
                {
                    if (cell == shut)
                        continue;
                    from = FirstOpenNode(layout, nav, cell);
                    if (from >= 0)
                        break;
                }
                if (from < 0)
                    return false;

                nav.Distances(from, steps, n =>
                {
                    int cell = n % nav.W / P + n / nav.W / P * layout.Cols;
                    return cell != shut && layout.RoomOf[cell] == 0;
                });
                foreach (int cell in cells)
                {
                    if (cell == shut)
                        continue;
                    int node = FirstOpenNode(layout, nav, cell);
                    if (node < 0 || !ReachedInCell(layout, nav, steps, cell))
                        return false;
                }
            }

            return true;
        }

        static int FirstOpenNode(FloorLayout layout, FloorNav nav, int cell)
        {
            const int P = FloorNav.PerCell;
            int i = cell % layout.Cols, j = cell / layout.Cols;
            for (int b = 0; b < P; b++)
                for (int a = 0; a < P; a++)
                {
                    int n = i * P + a + (j * P + b) * nav.W;
                    if (!nav.Blocked[n])
                        return n;
                }
            return -1;
        }

        static bool ReachedInCell(FloorLayout layout, FloorNav nav, int[] steps, int cell)
        {
            const int P = FloorNav.PerCell;
            int i = cell % layout.Cols, j = cell / layout.Cols;
            for (int b = 0; b < P; b++)
                for (int a = 0; a < P; a++)
                    if (steps[i * P + a + (j * P + b) * nav.W] != FloorNav.Unreached)
                        return true;
            return false;
        }
    }
}
