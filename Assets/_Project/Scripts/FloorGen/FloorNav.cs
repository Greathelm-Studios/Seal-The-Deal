using System;

namespace CloseTheDeal.FloorGen
{
    /// <summary>Half-metre walk map of one floor: walls close links between nodes, furniture closes nodes. Design: docs/systems/FLOOR_GEN.md.</summary>
    public sealed class FloorNav
    {
        public const float Node = 0.5f;
        public const int PerCell = 8;
        public const int Unreached = -1;

        public readonly int W, H;
        public readonly bool[] Blocked;
        readonly bool[] _east;
        readonly bool[] _south;
        readonly int[] _queue;
        readonly int[] _scratch;

        public int Count => W * H;

        public FloorNav(FloorLayout layout)
        {
            W = layout.Cols * PerCell;
            H = layout.Rows * PerCell;
            Blocked = new bool[W * H];
            _east = new bool[W * H];
            _south = new bool[W * H];
            _queue = new int[W * H];
            _scratch = new int[W * H];

            for (int b = 0; b < H; b++)
                for (int a = 0; a < W; a++)
                {
                    int n = a + b * W;
                    if (a < W - 1)
                        _east[n] = a % PerCell != PerCell - 1 || Crossable(layout.EdgeOf(a / PerCell, b / PerCell, Side.East), b % PerCell);
                    if (b < H - 1)
                        _south[n] = b % PerCell != PerCell - 1 || Crossable(layout.EdgeOf(a / PerCell, b / PerCell, Side.South), a % PerCell);
                }
        }

        static bool Crossable(FloorEdge edge, int along)
        {
            if (edge.Kind == EdgeKind.Open)
                return true;
            int door = edge.Kind == EdgeKind.LiftDoor ? 2 : edge.DoorAt;
            return door != 0 && (along == 2 * door - 1 || along == 2 * door);
        }

        public int NodeAt(float x, float y)
        {
            int a = Math.Clamp((int)Math.Floor(x / Node), 0, W - 1);
            int b = Math.Clamp((int)Math.Floor(y / Node), 0, H - 1);
            return a + b * W;
        }

        public float X(int node) => (node % W + 0.5f) * Node;
        public float Y(int node) => (node / W + 0.5f) * Node;
        public static float Metres(int steps) => steps * Node;

        /// <summary>Blocks every open node whose centre lies inside the rectangle grown by <paramref name="grow"/>, listing the ones it changed.</summary>
        public void BlockRect(float x0, float y0, float x1, float y1, float grow, System.Collections.Generic.List<int> changed)
        {
            int a0 = Math.Max(0, (int)Math.Ceiling((x0 - grow) / Node - 0.5f));
            int a1 = Math.Min(W - 1, (int)Math.Floor((x1 + grow) / Node - 0.5f));
            int b0 = Math.Max(0, (int)Math.Ceiling((y0 - grow) / Node - 0.5f));
            int b1 = Math.Min(H - 1, (int)Math.Floor((y1 + grow) / Node - 0.5f));
            for (int b = b0; b <= b1; b++)
                for (int a = a0; a <= a1; a++)
                {
                    int n = a + b * W;
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

        public bool AnyInRect(bool[] mask, float x0, float y0, float x1, float y1, float grow)
        {
            int a0 = Math.Max(0, (int)Math.Ceiling((x0 - grow) / Node - 0.5f));
            int a1 = Math.Min(W - 1, (int)Math.Floor((x1 + grow) / Node - 0.5f));
            int b0 = Math.Max(0, (int)Math.Ceiling((y0 - grow) / Node - 0.5f));
            int b1 = Math.Min(H - 1, (int)Math.Floor((y1 + grow) / Node - 0.5f));
            for (int b = b0; b <= b1; b++)
                for (int a = a0; a <= a1; a++)
                    if (mask[a + b * W])
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
                int a = n % W, next = steps[n] + 1;
                if (a < W - 1 && _east[n]) Visit(n + 1, next, steps, allowed, ref tail);
                if (a > 0 && _east[n - 1]) Visit(n - 1, next, steps, allowed, ref tail);
                if (n + W < Count && _south[n]) Visit(n + W, next, steps, allowed, ref tail);
                if (n - W >= 0 && _south[n - W]) Visit(n - W, next, steps, allowed, ref tail);
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
