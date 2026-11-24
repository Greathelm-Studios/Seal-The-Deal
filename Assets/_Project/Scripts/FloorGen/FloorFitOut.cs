using System.Collections.Generic;

namespace CloseTheDeal.FloorGen
{
    /// <summary>Furniture patterns per room kind, in one 4 m cell's own metres (U east, V south, back wall on the north side). Design: docs/systems/FLOOR_GEN.md.</summary>
    static class FloorFitOut
    {
        const float PlayerRadius = 0.4f;
        const float DeskTop = 0.74f;
        const float PodHalf = 0.35f, ChairHalf = 0.25f;
        const float WallThick = 0.2f, LightboxHalf = 1.8f;

        enum Need : byte { None, Wall, OpaqueWall }

        readonly struct Item
        {
            public readonly KitPiece Piece;
            public readonly float U, V, Yaw, HalfW, HalfD;
            public readonly Need Need;

            public Item(KitPiece piece, float u, float v, float yaw, float halfW, float halfD, Need need = Need.None)
            {
                Piece = piece; U = u; V = v; Yaw = yaw; HalfW = halfW; HalfD = halfD; Need = need;
            }
        }

        readonly struct Loose
        {
            public readonly PropKind Kind;
            public readonly float U, V, Height, Yaw;

            public Loose(PropKind kind, float u, float v, float yaw, float height = 0f)
            {
                Kind = kind; U = u; V = v; Yaw = yaw; Height = height;
            }
        }

        readonly struct Fixed
        {
            public readonly KitPiece Piece;
            public readonly float U, V, Yaw, Height;

            public Fixed(KitPiece piece, float u, float v, float yaw, float height = 0f)
            {
                Piece = piece; U = u; V = v; Yaw = yaw; Height = height;
            }
        }

        sealed class Pattern
        {
            public Item[] Items = new Item[0];
            public Loose[] Props = new Loose[0];
            public Fixed[] Dressing = new Fixed[0];
        }

        static readonly Pattern Bench = new()
        {
            Items = new[] { new Item(KitPiece.Workstation_Bench, 2f, 2f, 0f, 1.2f, 0.8f) },
            Props = new[]
            {
                new Loose(PropKind.Chair, 1.4f, 3.1f, 0f), new Loose(PropKind.Chair, 2.6f, 0.9f, 180f),
                new Loose(PropKind.Chair, 2.6f, 3.1f, 0f), new Loose(PropKind.Chair, 1.4f, 0.9f, 180f),
                new Loose(PropKind.Monitor, 1.4f, 2.3f, 0f, DeskTop), new Loose(PropKind.Monitor, 2.6f, 1.7f, 180f, DeskTop),
                new Loose(PropKind.Monitor, 2.6f, 2.3f, 0f, DeskTop), new Loose(PropKind.Monitor, 1.4f, 1.7f, 180f, DeskTop)
            },
            Dressing = new[]
            {
                new Fixed(KitPiece.Keyboard, 1.4f, 1.43f, 0f, DeskTop), new Fixed(KitPiece.Keyboard, 2.6f, 1.43f, 0f, DeskTop),
                new Fixed(KitPiece.Keyboard, 1.4f, 2.57f, 180f, DeskTop), new Fixed(KitPiece.Keyboard, 2.6f, 2.57f, 180f, DeskTop),
                new Fixed(KitPiece.Phone_Desk, 2.98f, 1.58f, 180f, DeskTop), new Fixed(KitPiece.Phone_Desk, 1.02f, 2.42f, 0f, DeskTop),
                new Fixed(KitPiece.Doc_Tray, 1.02f, 1.55f, 180f, DeskTop), new Fixed(KitPiece.Doc_Tray, 2.98f, 2.45f, 0f, DeskTop),
                new Fixed(KitPiece.PC_Tower, 0.68f, 1.45f, 0f), new Fixed(KitPiece.PC_Tower, 3.32f, 2.55f, 180f)
            }
        };

        static readonly Pattern MeetingTable = new()
        {
            Items = new[] { new Item(KitPiece.Desk_Sculpt, 2f, 2f, 0f, 0.8f, 0.4f) },
            Props = new[] { new Loose(PropKind.Chair, 2f, 1.1f, 180f), new Loose(PropKind.Chair, 2f, 2.9f, 0f) }
        };

        static readonly Pattern KitchenCounter = new()
        {
            Items = new[]
            {
                new Item(KitPiece.Counter_Capsule_2m, 1.5f, 0.8f, 0f, 1f, 0.5f, Need.Wall),
                new Item(KitPiece.Vending_Machine, 3.3f, 0.6f, 180f, 0.45f, 0.34f, Need.OpaqueWall)
            }
        };

        static readonly Pattern PrintCorner = new()
        {
            Items = new[]
            {
                new Item(KitPiece.Copier, 0.8f, 0.6f, 180f, 0.31f, 0.33f, Need.Wall),
                new Item(KitPiece.Storage_Low, 2.7f, 0.5f, 180f, 0.8f, 0.23f, Need.Wall)
            },
            Props = new[] { new Loose(PropKind.Cabinet, 3.3f, 3.3f, 0f) }
        };

        static readonly Pattern ArchiveRow = new()
        {
            Items = new[]
            {
                new Item(KitPiece.Storage_Low, 1f, 0.5f, 180f, 0.8f, 0.23f, Need.Wall),
                new Item(KitPiece.Storage_Low, 3f, 0.5f, 180f, 0.8f, 0.23f, Need.Wall),
                new Item(KitPiece.Cover_Block_2m, 2f, 2.7f, 0f, 1f, 0.3f)
            },
            Props = new[] { new Loose(PropKind.Cabinet, 0.7f, 3.3f, 0f) }
        };

        static readonly Pattern LoungeCounter = new()
        {
            Items = new[]
            {
                new Item(KitPiece.Counter_Capsule_2m, 2f, 2f, 0f, 1f, 0.5f),
                new Item(KitPiece.Planter_Box, 0.8f, 0.6f, 0f, 0.52f, 0.32f, Need.Wall)
            },
            Props = new[] { new Loose(PropKind.Chair, 2f, 3f, 0f), new Loose(PropKind.Chair, 2f, 1f, 180f) }
        };

        static readonly Pattern PodTable = new()
        {
            Items = new[]
            {
                new Item(KitPiece.Counter_Capsule_2m, 2f, 2f, 0f, 1f, 0.5f),
                new Item(KitPiece.Chair_Pod, 2f, 0.9f, 180f, PodHalf, PodHalf),
                new Item(KitPiece.Chair_Pod, 2f, 3.1f, 0f, PodHalf, PodHalf)
            }
        };
        static readonly Pattern CounterAcross = new() { Items = new[] { new Item(KitPiece.Counter_Capsule_2m, 2f, 2f, 90f, 1f, 0.5f) } };
        static readonly Pattern CounterAlong = new() { Items = new[] { new Item(KitPiece.Counter_Capsule_2m, 2f, 2f, 0f, 1f, 0.5f) } };
        static readonly Pattern PlanterByWall = new() { Items = new[] { new Item(KitPiece.Planter_Box, 2f, 0.6f, 0f, 0.52f, 0.32f, Need.Wall) } };
        static readonly Pattern WaterByWall = new() { Items = new[] { new Item(KitPiece.Water_Server, 1f, 0.45f, 180f, 0.18f, 0.18f, Need.OpaqueWall) } };

        static readonly Pattern[] HallPatterns = { Bench, PodTable, LoungeCounter, PlanterByWall };
        static readonly Pattern[] CorridorPatterns = { PlanterByWall, WaterByWall };

        /// <summary>The manager's office: fixed, in metres from the office's north-west corner, never turned.</summary>
        static readonly Item[] OfficeItems =
        {
            new Item(KitPiece.Desk_Reception, 4f, 2.4f, 180f, 2f, 0.45f),
            new Item(KitPiece.Storage_Low, 0.5f, 4f, 90f, 0.8f, 0.23f),
            new Item(KitPiece.Storage_Low, 7.5f, 4f, 270f, 0.8f, 0.23f),
            new Item(KitPiece.Planter_Box, 0.9f, 0.7f, 0f, 0.52f, 0.32f),
            new Item(KitPiece.Planter_Box, 7.1f, 0.7f, 0f, 0.52f, 0.32f),
            new Item(KitPiece.Chair_Pod, 3.1f, 3.7f, 0f, PodHalf, PodHalf),
            new Item(KitPiece.Chair_Pod, 4.9f, 3.7f, 0f, PodHalf, PodHalf)
        };
        static readonly Loose OfficeChair = new(PropKind.Chair, 4f, 1.4f, 180f);

        public static void Place(FloorLayout layout, FloorGenProfile profile, ref FloorRandom rng, bool[] keepClear)
        {
            FloorNav nav = layout.Nav;
            var changed = new List<int>();
            foreach (FloorRoom room in layout.Rooms)
            {
                if (room.Kind == RoomKind.ManagersOffice)
                {
                    foreach (Item item in OfficeItems)
                        TryPlace(layout, nav, keepClear, changed, item, layout.OfficeX0, layout.OfficeY0, 0);
                    layout.PropSpots.Add(new PropSpot { Kind = OfficeChair.Kind, X = layout.OfficeX0 + OfficeChair.U, Y = layout.OfficeY0 + OfficeChair.V, Yaw = OfficeChair.Yaw });
                    float middle = (layout.OfficeX0 + layout.OfficeX1) * 0.5f;
                    layout.Pieces.Add(new KitPlacement { Piece = KitPiece.Lightbox_Wall_Wave, X = middle + LightboxHalf, Y = layout.OfficeY0 + WallThick, Yaw = 180f, Still = true });
                    continue;
                }

                foreach (int cell in room.Cells)
                {
                    Pattern pattern = PatternFor(room.Kind, profile, ref rng);
                    if (pattern == null)
                        continue;

                    int i = cell % layout.Cols, j = cell / layout.Cols;
                    int turn = BestTurn(layout, pattern, i, j, rng.Range(0, 4));
                    bool placedAny = pattern.Items.Length == 0;
                    foreach (Item item in pattern.Items)
                    {
                        if (item.Need != Need.None && !Backed(layout, i, j, turn, item.Need))
                            continue;
                        placedAny |= TryPlace(layout, nav, keepClear, changed, item, i * FloorLayout.Cell, j * FloorLayout.Cell, turn);
                    }

                    if (!placedAny)
                        continue;
                    foreach (Fixed fix in pattern.Dressing)
                    {
                        Turn(fix.U, fix.V, turn, out float u, out float v);
                        layout.Pieces.Add(new KitPlacement
                        {
                            Piece = fix.Piece, X = i * FloorLayout.Cell + u, Y = j * FloorLayout.Cell + v,
                            Height = fix.Height, Yaw = (fix.Yaw + 90f * turn) % 360f, Still = true
                        });
                    }
                    foreach (Loose loose in pattern.Props)
                    {
                        Turn(loose.U, loose.V, turn, out float u, out float v);
                        layout.PropSpots.Add(new PropSpot
                        {
                            Kind = loose.Kind, X = i * FloorLayout.Cell + u, Y = j * FloorLayout.Cell + v,
                            Height = loose.Height, Yaw = loose.Yaw + 90f * turn
                        });
                    }
                }
            }
        }

        /// <summary>Keeps the loose props within the cap. Chairs and monitors over it stand as fixed kit pieces, so desks never look bare.</summary>
        public static void Settle(FloorLayout layout, FloorGenProfile profile, ref FloorRandom rng)
        {
            FloorNav nav = layout.Nav;
            var changed = new List<int>();
            var chairs = new List<PropSpot>();
            rng.Shuffle(layout.PropSpots);
            for (int k = layout.PropSpots.Count - 1; k >= profile.MaxProps; k--)
            {
                PropSpot spot = layout.PropSpots[k];
                layout.PropSpots.RemoveAt(k);
                if (spot.Kind == PropKind.Monitor)
                    layout.Pieces.Add(new KitPlacement { Piece = KitPiece.Monitor_Flat, X = spot.X, Y = spot.Y, Height = spot.Height, Yaw = (spot.Yaw + 180f) % 360f, Still = true });
                else if (spot.Kind == PropKind.Chair)
                    chairs.Add(spot);
            }

            // A fixed chair cannot be shoved aside, so nobody may spawn inside it. One check covers them all on most floors.
            foreach (PropSpot chair in chairs)
                BlockChair(nav, chair, changed);
            bool together = nav.AllConnected();
            if (!together)
                nav.Unblock(changed);
            foreach (PropSpot chair in chairs)
            {
                if (!together)
                {
                    changed.Clear();
                    BlockChair(nav, chair, changed);
                    if (!nav.AllConnected())
                    {
                        nav.Unblock(changed);
                        continue;
                    }
                }
                layout.Pieces.Add(new KitPlacement { Piece = KitPiece.Chair_Task, X = chair.X, Y = chair.Y, Yaw = chair.Yaw, Still = true });
            }
        }

        static void BlockChair(FloorNav nav, PropSpot chair, List<int> changed)
        {
            nav.BlockRect(chair.X - ChairHalf, chair.Y - ChairHalf, chair.X + ChairHalf, chair.Y + ChairHalf, PlayerRadius, changed);
        }

        static Pattern PatternFor(RoomKind kind, FloorGenProfile profile, ref FloorRandom rng)
        {
            switch (kind)
            {
                case RoomKind.Ring:
                    return rng.Percent(profile.CorridorDressingPercent) ? CorridorPatterns[rng.Range(0, CorridorPatterns.Length)] : null;
                case RoomKind.Court:
                    return rng.Percent(35) ? (rng.Percent(50) ? CounterAcross : CounterAlong) : null;
                case RoomKind.WindowHall:
                    return rng.Percent(75) ? HallPatterns[rng.Range(0, HallPatterns.Length)] : null;
                case RoomKind.Lounge: return rng.Percent(80) ? LoungeCounter : PlanterByWall;
                case RoomKind.OpenOffice: return Bench;
                case RoomKind.Meeting: return MeetingTable;
                case RoomKind.Kitchen: return rng.Percent(70) ? KitchenCounter : LoungeCounter;
                case RoomKind.Print: return PrintCorner;
                case RoomKind.Archive: return ArchiveRow;
                default: return null;
            }
        }

        static int BestTurn(FloorLayout layout, Pattern pattern, int i, int j, int first)
        {
            int best = first, bestScore = -1;
            for (int k = 0; k < 4; k++)
            {
                int turn = (first + k) & 3, score = 0;
                foreach (Item item in pattern.Items)
                    if (item.Need == Need.None || Backed(layout, i, j, turn, item.Need))
                        score++;
                if (score > bestScore)
                {
                    best = turn;
                    bestScore = score;
                }
            }

            return best;
        }

        static bool Backed(FloorLayout layout, int i, int j, int turn, Need need)
        {
            FloorEdge edge = layout.EdgeOf(i, j, (Side.North + turn) & 3);
            if (edge.Passable || edge.Kind == EdgeKind.Open || edge.Kind == EdgeKind.FacadeWindow)
                return false;
            return need == Need.Wall || edge.Kind == EdgeKind.Solid || edge.Kind == EdgeKind.FacadeSolid || edge.Kind == EdgeKind.Shaft;
        }

        static void Turn(float u, float v, int turn, out float tu, out float tv)
        {
            tu = u;
            tv = v;
            for (int k = 0; k < turn; k++)
            {
                float next = FloorLayout.Cell - tv;
                tv = tu;
                tu = next;
            }
        }

        static bool TryPlace(FloorLayout layout, FloorNav nav, bool[] keepClear, List<int> changed, Item item, float originX, float originY, int turn)
        {
            Turn(item.U, item.V, turn, out float u, out float v);
            float yaw = (item.Yaw + 90f * turn) % 360f;
            bool sideways = ((int)System.Math.Round(yaw / 90f) & 1) == 1;
            float hw = sideways ? item.HalfD : item.HalfW, hd = sideways ? item.HalfW : item.HalfD;
            float x = originX + u, y = originY + v;

            if (nav.AnyInRect(keepClear, x - hw, y - hd, x + hw, y + hd, PlayerRadius))
                return false;

            changed.Clear();
            nav.BlockRect(x - hw, y - hd, x + hw, y + hd, PlayerRadius, changed);
            if (!nav.AllConnected())
            {
                nav.Unblock(changed);
                return false;
            }

            layout.Pieces.Add(new KitPlacement { Piece = item.Piece, X = x, Y = y, Yaw = yaw, Still = true });
            return true;
        }
    }
}
