using System.Collections.Generic;

namespace CloseTheDeal.FloorGen
{
    /// <summary>Furniture patterns per room kind, in one 4 m cell's own metres (U east, V south, back wall on the north side). Design: docs/systems/FLOOR_GEN.md.</summary>
    static class FloorFitOut
    {
        const float PlayerRadius = 0.4f;
        const float DeskTop = 0.74f;

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

        sealed class Pattern
        {
            public Item[] Items = new Item[0];
            public Loose[] Props = new Loose[0];
        }

        static readonly Pattern Bench = new()
        {
            Items = new[] { new Item(KitPiece.Workstation_Bench, 2f, 2f, 0f, 1.2f, 0.8f) },
            Props = new[]
            {
                new Loose(PropKind.Chair, 1.4f, 3.1f, 0f), new Loose(PropKind.Chair, 2.6f, 0.9f, 180f),
                new Loose(PropKind.Monitor, 1.4f, 2.3f, 0f, DeskTop), new Loose(PropKind.Monitor, 2.6f, 1.7f, 180f, DeskTop)
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

        static readonly Pattern CoverAcross = new() { Items = new[] { new Item(KitPiece.Cover_Block_2m, 2f, 2f, 90f, 1f, 0.3f) } };
        static readonly Pattern CoverAlong = new() { Items = new[] { new Item(KitPiece.Cover_Block_2m, 2f, 2f, 0f, 1f, 0.3f) } };
        static readonly Pattern PlanterByWall = new() { Items = new[] { new Item(KitPiece.Planter_Box, 2f, 0.6f, 0f, 0.52f, 0.32f, Need.Wall) } };
        static readonly Pattern WaterByWall = new() { Items = new[] { new Item(KitPiece.Water_Server, 1f, 0.45f, 180f, 0.18f, 0.18f, Need.OpaqueWall) } };

        static readonly Pattern[] HallPatterns = { Bench, CoverAcross, LoungeCounter, PlanterByWall };
        static readonly Pattern[] CorridorPatterns = { PlanterByWall, WaterByWall };

        /// <summary>The manager's office: fixed, in metres from the office's north-west corner, never turned.</summary>
        static readonly Item[] OfficeItems =
        {
            new Item(KitPiece.Desk_Reception, 4f, 2.4f, 180f, 2f, 0.45f),
            new Item(KitPiece.Storage_Low, 0.5f, 4f, 90f, 0.8f, 0.23f),
            new Item(KitPiece.Storage_Low, 7.5f, 4f, 270f, 0.8f, 0.23f),
            new Item(KitPiece.Planter_Box, 0.9f, 0.7f, 0f, 0.52f, 0.32f),
            new Item(KitPiece.Planter_Box, 7.1f, 0.7f, 0f, 0.52f, 0.32f)
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

        static Pattern PatternFor(RoomKind kind, FloorGenProfile profile, ref FloorRandom rng)
        {
            switch (kind)
            {
                case RoomKind.Ring:
                    return rng.Percent(profile.CorridorDressingPercent) ? CorridorPatterns[rng.Range(0, CorridorPatterns.Length)] : null;
                case RoomKind.Court:
                    return rng.Percent(35) ? (rng.Percent(50) ? CoverAcross : CoverAlong) : null;
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
