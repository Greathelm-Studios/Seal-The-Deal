using System;

namespace CloseTheDeal.FloorGen
{
    /// <summary>Half-metre walk map of one floor, every storey: walls close links between nodes, furniture closes nodes, stairs join storeys. Design: docs/systems/FLOOR_GEN.md.</summary>
    public sealed class FloorNav
    {
        public const float Node = 0.5f;
        public const int PerCell = 8;
        public const int Unreached = -1;
        const float StairClear = 0.2f;

        public readonly int W, H, Levels;
        public readonly bool[] Blocked;
        /// <summary>Nodes on a flight or its landing: walkable, but nothing may stand or spawn there.</summary>
        public readonly bool[] Reserved;
        readonly int _perLevel;
        readonly bool[] _east;
        readonly bool[] _south;
        readonly int[] _link;
        readonly int[] _queue;
        readonly int[] _scratch;

        public int Count => _perLevel * Levels;

        public FloorNav(FloorLayout layout)
        {
            W = layout.Cols * PerCell;
            H = layout.Rows * PerCell;
            Levels = layout.Levels;
            _perLevel = W * H;
            Blocked = new bool[Count];
            Reserved = new bool[Count];
            _east = new bool[Count];
            _south = new bool[Count];
            _link = new int[Count];
            _queue = new int[Count];
            _scratch = new int[Count];
            Array.Fill(_link, -1);

            for (int level = 0; level < Levels; level++)
                for (int b = 0; b < H; b++)
                    for (int a = 0; a < W; a++)
                    {
                        int n = a + b * W + level * _perLevel;
                        if (a < W - 1)
                            _east[n] = a % PerCell != PerCell - 1 || Crossable(layout, a / PerCell, b / PerCell, Side.East, b % PerCell, level);
                        if (b < H - 1)
                            _south[n] = b % PerCell != PerCell - 1 || Crossable(layout, a / PerCell, b / PerCell, Side.South, a % PerCell, level);
                    }

            if (Levels > 1)
                foreach (FloorStair stair in layout.Stairs)
                    AddStair(stair);
            if (layout.HasExitLift) CloseShaft(layout.ExitLift);
            if (layout.HasArrivalLift) CloseShaft(layout.ArrivalLift);
        }

        // A lift car stands on one storey; on the others its bay is solid shaft.
        void CloseShaft(FloorLift lift)
        {
            for (int level = 0; level < Levels; level++)
            {
                if (level == lift.Level)
                    continue;
                for (int b = 0; b < PerCell; b++)
                    for (int a = 0; a < PerCell; a++)
                        Blocked[Index(lift.CellX * PerCell + a, lift.CellY * PerCell + b, level)] = true;
            }
        }

        static bool Crossable(FloorLayout layout, int i, int j, int side, int along, int level)
        {
            FloorEdge edge = layout.EdgeOf(i, j, side);
            if (edge.Kind == EdgeKind.Open)
                return true;

            int door = edge.DoorAt;
            if (edge.Kind == EdgeKind.LiftDoor)
                door = layout.LiftDoorOn(i, j, side, level) || layout.LiftDoorOn(i + Side.Dx[side], j + Side.Dy[side], Side.Opposite(side), level) ? 2 : 0;
            return door != 0 && (along == 2 * door - 1 || along == 2 * door);
        }

        // The flight's own nodes stand for being on it: open only along the climb, joined at the top to the storey above.
        void AddStair(FloorStair stair)
        {
            int dx = Side.Dx[stair.Dir], dy = Side.Dy[stair.Dir];
            ReserveBeside(stair, dx != 0);
            int a0 = (int)Math.Round(stair.X0 / Node), a1 = (int)Math.Round(stair.X1 / Node) - 1;
            int b0 = (int)Math.Round(stair.Y0 / Node), b1 = (int)Math.Round(stair.Y1 / Node) - 1;
            float length = dx != 0 ? stair.X1 - stair.X0 : stair.Y1 - stair.Y0;
            for (int b = b0; b <= b1; b++)
                for (int a = a0; a <= a1; a++)
                {
                    int n = a + b * W;
                    Blocked[n + _perLevel] = true;
                    float x = (a + 0.5f) * Node, y = (b + 0.5f) * Node;
                    float climbed = dx > 0 ? x - stair.X0 : dx < 0 ? stair.X1 - x : dy > 0 ? y - stair.Y0 : stair.Y1 - y;
                    if (climbed < FloorLayout.StairLead + StairClear)
                        continue;

                    Reserved[n] = true;
                    if (a == a0 && a > 0) _east[n - 1] = false;
                    if (a == a1 && a < W - 1) _east[n] = false;
                    if (b == b0 && b > 0) _south[n - W] = false;
                    if (b == b1 && b < H - 1) _south[n] = false;
                    if (climbed < length - Node)
                        continue;

                    int top = a + dx + (b + dy) * W + _perLevel;
                    _link[n] = top;
                    _link[top] = n;
                    Reserved[top] = true;
                    Reserved[top + dx + dy * W] = true;
                }
        }

        // The row of nodes along the walkway next to a flight is within a body's width of its glass: walkable, but nobody spawns there.
        void ReserveBeside(FloorStair stair, bool alongX)
        {
            int a0 = (int)Math.Round(stair.WalkX0 / Node), a1 = (int)Math.Round(stair.WalkX1 / Node) - 1;
            int b0 = (int)Math.Round(stair.WalkY0 / Node), b1 = (int)Math.Round(stair.WalkY1 / Node) - 1;
            if (alongX)
            {
                if (stair.WalkY0 >= stair.Y1 - 0.01f) b1 = b0;
                else b0 = b1;
            }
            else if (stair.WalkX0 >= stair.X1 - 0.01f) a1 = a0;
            else a0 = a1;

            for (int level = 0; level < Levels; level++)
                for (int b = b0; b <= b1; b++)
                    for (int a = a0; a <= a1; a++)
                        Reserved[Index(a, b, level)] = true;
        }

        public int NodeAt(float x, float y, int level)
        {
            int a = Math.Clamp((int)Math.Floor(x / Node), 0, W - 1);
            int b = Math.Clamp((int)Math.Floor(y / Node), 0, H - 1);
            return a + b * W + Math.Clamp(level, 0, Levels - 1) * _perLevel;
        }

        public int Index(int a, int b, int level) => a + b * W + level * _perLevel;

        public float X(int node) => (node % W + 0.5f) * Node;
        public float Y(int node) => (node / W % H + 0.5f) * Node;
        public int LevelOf(int node) => node / _perLevel;
        public static float Metres(int steps) => steps * Node;

        /// <summary>Blocks every open node on a storey whose centre lies inside the rectangle grown by <paramref name="grow"/>, listing the ones it changed.</summary>
        public void BlockRect(float x0, float y0, float x1, float y1, float grow, int level, System.Collections.Generic.List<int> changed)
        {
            int a0 = Math.Max(0, (int)Math.Ceiling((x0 - grow) / Node - 0.5f));
            int a1 = Math.Min(W - 1, (int)Math.Floor((x1 + grow) / Node - 0.5f));
            int b0 = Math.Max(0, (int)Math.Ceiling((y0 - grow) / Node - 0.5f));
            int b1 = Math.Min(H - 1, (int)Math.Floor((y1 + grow) / Node - 0.5f));
            for (int b = b0; b <= b1; b++)
                for (int a = a0; a <= a1; a++)
                {
                    int n = a + b * W + level * _perLevel;
                    if (Blocked[n])
                        continue;
                    Blocked[n] = true;
                    changed.Add(n);
                }
        }

        public void Unblock(System.Collections.Generic.List<int> nodes)
        {
            foreach (int n in nodes)
                Blocked[n] = false;
        }

        public bool AnyInRect(bool[] mask, float x0, float y0, float x1, float y1, float grow, int level)
        {
            int a0 = Math.Max(0, (int)Math.Ceiling((x0 - grow) / Node - 0.5f));
            int a1 = Math.Min(W - 1, (int)Math.Floor((x1 + grow) / Node - 0.5f));
            int b0 = Math.Max(0, (int)Math.Ceiling((y0 - grow) / Node - 0.5f));
            int b1 = Math.Min(H - 1, (int)Math.Floor((y1 + grow) / Node - 0.5f));
            for (int b = b0; b <= b1; b++)
                for (int a = a0; a <= a1; a++)
                    if (mask[a + b * W + level * _perLevel])
                        return true;
            return false;
        }

        /// <summary>Walking steps from a node to every other node; Unreached where there is no way. Returns how many nodes were reached.</summary>
        public int Distances(int from, int[] steps, Func<int, bool> allowed = null)
        {
            Array.Fill(steps, Unreached);
            if (Blocked[from] || (allowed != null && !allowed(from)))
                return 0;

            int head = 0, tail = 0;
            _queue[tail++] = from;
            steps[from] = 0;
            while (head < tail)
            {
                int n = _queue[head++];
                int a = n % W, b = n / W % H, next = steps[n] + 1;
                if (a < W - 1 && _east[n]) Visit(n + 1, next, steps, allowed, ref tail);
                if (a > 0 && _east[n - 1]) Visit(n - 1, next, steps, allowed, ref tail);
                if (b < H - 1 && _south[n]) Visit(n + W, next, steps, allowed, ref tail);
                if (b > 0 && _south[n - W]) Visit(n - W, next, steps, allowed, ref tail);
                if (_link[n] >= 0) Visit(_link[n], next, steps, allowed, ref tail);
            }

            return tail;
        }

        void Visit(int n, int step, int[] steps, Func<int, bool> allowed, ref int tail)
        {
            if (steps[n] != Unreached || Blocked[n] || (allowed != null && !allowed(n)))
                return;

            steps[n] = step;
            _queue[tail++] = n;
        }

        public int FreeCount()
        {
            int free = 0;
            for (int n = 0; n < Blocked.Length; n++)
                if (!Blocked[n])
                    free++;
            return free;
        }

        /// <summary>
        /// True when every open node on one storey can be walked to from every other without leaving it.
        /// Stairs are never blocked, so a floor that was whole stays whole while each storey passes this after a change to it.
        /// </summary>
        public bool LevelConnected(int level)
        {
            int low = level * _perLevel, high = low + _perLevel, free = 0, start = -1;
            for (int n = low; n < high; n++)
            {
                _scratch[n] = Unreached;
                if (Blocked[n])
                    continue;
                free++;
                if (start < 0)
                    start = n;
            }
            if (start < 0)
                return false;

            int head = 0, tail = 0;
            _queue[tail++] = start;
            _scratch[start] = 0;
            while (head < tail)
            {
                int n = _queue[head++];
                int a = n % W, b = n / W % H;
                if (a < W - 1 && _east[n]) Visit(n + 1, 0, _scratch, null, ref tail);
                if (a > 0 && _east[n - 1]) Visit(n - 1, 0, _scratch, null, ref tail);
                if (b < H - 1 && _south[n]) Visit(n + W, 0, _scratch, null, ref tail);
                if (b > 0 && _south[n - W]) Visit(n - W, 0, _scratch, null, ref tail);
            }

            return tail == free;
        }

        /// <summary>True when every open node can be walked to from every other.</summary>
        public bool AllConnected()
        {
            for (int n = 0; n < Blocked.Length; n++)
                if (!Blocked[n])
                    return Distances(n, _scratch) == FreeCount();
            return false;
        }
    }
}
