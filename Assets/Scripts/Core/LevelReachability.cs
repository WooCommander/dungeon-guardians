using System;
using System.Collections.Generic;

namespace DungeonGuardians.Core
{
    // A quick check that a level can be won at all, guardians aside: can the explorer, moving exactly as
    // DungeonSimulation moves it, take every piece of gold in some order and then reach the exit?
    //
    // The moves: walking, climbing ladders, hanging on bars and dropping off them, falling (which cannot be undone:
    // a fall is a one-way street), and digging down through a brick floor (dig beside you, step over, drop in, fall
    // on through if there is nothing below). Gold is taken by passing through its cell.
    //
    // The check is generous where it cannot know better, so it never fails a level that can be won: gates count as
    // open whenever something can open them, planks may also give way, and a hole never closes on the explorer. It
    // catches what no player could do (gold walled in, a one-way drop away from the exit, a gate nothing opens);
    // whether the guardians leave room to do it is for the recorded replays (LevelReplay) to show.
    public static class LevelReachability
    {
        public sealed class Result
        {
            public bool Passable;
            // What is wrong, for the report; empty when passable.
            public readonly List<string> Problems = new List<string>();
            // Things taken on trust (gates opened by guardians), for the report.
            public readonly List<string> Notes = new List<string>();
            // A route that wins: runs from the start through the gold, the last one ending at the exit. Every run but
            // the last ends in a dead end and costs a life.
            public readonly List<List<GridPoint>> Runs = new List<List<GridPoint>>();
            // Lives the route costs: 0 for a level that can be won cleanly.
            public int LivesNeeded;
        }

        public static Result Check(LevelDefinition level)
        {
            var result = new Result();
            TileType[,] tiles;
            try
            {
                tiles = LevelParser.ParseTiles(level);
            }
            catch (Exception exception)
            {
                result.Problems.Add(exception.Message);
                return result;
            }

            int width = level.width, height = level.height;
            bool inBounds(GridPoint p) => p.x >= 0 && p.x < width && p.y >= 0 && p.y < height;

            // Where things stand.
            CheckCell(level.playerStart, "the start", tiles, inBounds, result);
            CheckCell(level.exit, "the exit", tiles, inBounds, result);
            if (inBounds(level.exit) && tiles[level.exit.x, level.exit.y] != TileType.ExitClosed)
            {
                result.Problems.Add($"the exit {level.exit} is not on an exit tile ('E')");
            }

            GridPoint[] gold = level.gold ?? Array.Empty<GridPoint>();
            if (gold.Length == 0)
            {
                result.Problems.Add("there is no gold: the exit never opens");
            }

            foreach (GridPoint piece in gold)
            {
                CheckCell(piece, $"gold {piece}", tiles, inBounds, result);
            }

            if (result.Problems.Count > 0)
            {
                return result;
            }

            // Gates: open as soon as anything can press a plate.
            bool hasGates = false, hasPlates = false;
            foreach (TileType tile in tiles)
            {
                hasGates |= tile == TileType.GateClosed;
                hasPlates |= tile == TileType.PressurePlate;
            }

            bool hasGuardians = level.guardians != null && level.guardians.Length > 0;
            bool gatesOpen = hasPlates && (level.playerPressesPlates || hasGuardians);
            if (hasGates && !gatesOpen)
            {
                result.Notes.Add(hasPlates
                    ? "the gates stay shut: only a guardian presses the plates, and there is none"
                    : "the gates stay shut: there is no pressure plate");
            }
            else if (hasGates && !level.playerPressesPlates)
            {
                result.Notes.Add("the gates are taken as open: a guardian has to be lured onto a plate");
            }

            var map = new Map(tiles, width, height, gatesOpen);

            // Which of the places that matter can be reached from which: the start, each piece of gold, the exit.
            var places = new List<GridPoint> { level.playerStart };
            places.AddRange(gold);
            places.Add(level.exit);
            var reach = new List<HashSet<GridPoint>>();
            foreach (GridPoint place in places)
            {
                reach.Add(map.ReachableFrom(place));
            }

            HashSet<GridPoint> fromStart = reach[0];
            foreach (GridPoint piece in gold)
            {
                if (!fromStart.Contains(piece))
                {
                    result.Problems.Add($"gold {piece} cannot be reached from the start {level.playerStart}");
                }
            }

            if (!fromStart.Contains(level.exit))
            {
                result.Problems.Add($"the exit {level.exit} cannot be reached from the start {level.playerStart}");
            }

            if (result.Problems.Count > 0)
            {
                return result;
            }

            // Falls only go one way, so the order matters, and some gold may leave the explorer with no way on: then
            // only a lost life (caught, or buried in a closing hole) brings it back to the start, the gold taken
            // staying taken. So the route is a few runs, each from the start through some gold, the last one ending
            // at the exit; every run but the last costs a life. The fewest runs is the smallest cover of the gold and
            // the exit by chains of "can reach", found by matching each place to the one that follows it.
            int count = gold.Length + 1;
            GridPoint PlaceOf(int i) => i < gold.Length ? gold[i] : level.exit;
            HashSet<GridPoint> ReachOf(int i) => reach[i + 1];

            bool Follows(int a, int b)
            {
                // Nothing comes after the exit.
                if (a == b || a == count - 1 || !ReachOf(a).Contains(PlaceOf(b)))
                {
                    return false;
                }

                // Two places that reach each other follow in one order only, so runs never loop.
                bool back = b != count - 1 && ReachOf(b).Contains(PlaceOf(a));
                return !back || a < b;
            }

            var previous = new int[count];
            for (int i = 0; i < count; i++)
            {
                previous[i] = -1;
            }

            bool Match(int a, bool[] tried)
            {
                for (int b = 0; b < count; b++)
                {
                    if (tried[b] || !Follows(a, b))
                    {
                        continue;
                    }

                    tried[b] = true;
                    if (previous[b] < 0 || Match(previous[b], tried))
                    {
                        previous[b] = a;
                        return true;
                    }
                }

                return false;
            }

            for (int a = 0; a < count; a++)
            {
                Match(a, new bool[count]);
            }

            var following = new int[count];
            for (int i = 0; i < count; i++)
            {
                following[i] = -1;
            }

            for (int b = 0; b < count; b++)
            {
                if (previous[b] >= 0)
                {
                    following[previous[b]] = b;
                }
            }

            // The runs, the one that ends at the exit last.
            var runs = new List<List<GridPoint>>();
            List<GridPoint> last = null;
            for (int head = 0; head < count; head++)
            {
                if (previous[head] >= 0)
                {
                    continue;
                }

                var run = new List<GridPoint>();
                bool toExit = false;
                for (int i = head; i >= 0; i = following[i])
                {
                    run.Add(PlaceOf(i));
                    toExit |= i == count - 1;
                }

                if (toExit)
                {
                    last = run;
                }
                else
                {
                    runs.Add(run);
                    result.Notes.Add($"after gold {run[run.Count - 1]} there is no way on: the explorer must lose a life to start again");
                }
            }

            runs.Add(last);
            result.Runs.AddRange(runs);
            result.LivesNeeded = runs.Count - 1;
            if (result.LivesNeeded >= LevelReplay.Lives)
            {
                result.Problems.Add($"it takes {result.LivesNeeded + 1} runs from the start, {result.LivesNeeded} lost lives, and there are only {LevelReplay.Lives - 1} to spare");
                return result;
            }

            result.Passable = true;
            return result;
        }

        private static void CheckCell(GridPoint cell, string what, TileType[,] tiles, Func<GridPoint, bool> inBounds, Result result)
        {
            if (!inBounds(cell))
            {
                result.Problems.Add($"{what} {cell} is outside the level");
                return;
            }

            TileType tile = tiles[cell.x, cell.y];
            if (tile == TileType.Solid || tile == TileType.Brick || tile == TileType.FragileFloor)
            {
                result.Problems.Add($"{what} {cell} is inside a block ({tile})");
            }
        }

        // The explorer's moves on the level's map, as in DungeonSimulation.MovePlayer and TryDig.
        private sealed class Map
        {
            private readonly TileType[,] tiles;
            private readonly int width;
            private readonly int height;
            private readonly bool gatesOpen;

            public Map(TileType[,] tiles, int width, int height, bool gatesOpen)
            {
                this.tiles = tiles;
                this.width = width;
                this.height = height;
                this.gatesOpen = gatesOpen;
            }

            public HashSet<GridPoint> ReachableFrom(GridPoint start)
            {
                var seen = new HashSet<GridPoint> { start };
                var queue = new Queue<GridPoint>();
                queue.Enqueue(start);
                var next = new List<GridPoint>();
                while (queue.Count > 0)
                {
                    GridPoint point = queue.Dequeue();
                    next.Clear();
                    Moves(point, next);
                    foreach (GridPoint step in next)
                    {
                        if (seen.Add(step))
                        {
                            queue.Enqueue(step);
                        }
                    }
                }

                return seen;
            }

            private void Moves(GridPoint point, List<GridPoint> moves)
            {
                // Standing in a brick cell means it was dug, in a plank cell that the planks broke: it is open now.
                TileType here = Tile(point);
                if (here == TileType.Brick || here == TileType.FragileFloor)
                {
                    here = TileType.Air;
                }

                bool fragileBelow = Tile(point + GridPoint.Down) == TileType.FragileFloor;
                if (!HasSupport(point, here))
                {
                    if (CanOccupy(point + GridPoint.Down))
                    {
                        moves.Add(point + GridPoint.Down);
                    }

                    return;
                }

                // Planks may be broken by a heavy guardian: the explorer may then fall through.
                if (fragileBelow)
                {
                    moves.Add(point + GridPoint.Down);
                }

                foreach (GridPoint side in new[] { GridPoint.Left, GridPoint.Right })
                {
                    if (CanOccupy(point + side))
                    {
                        moves.Add(point + side);
                    }

                    // Digging: the brick diagonally below on that side, with open space above it; the explorer stands
                    // on solid ground, steps over the hole and drops in.
                    GridPoint target = point + new GridPoint(side.x, -1);
                    TileType above = Tile(target + GridPoint.Up);
                    if (IsSolidSupport(point + GridPoint.Down) && Tile(target) == TileType.Brick
                        && (above == TileType.Air || above == TileType.Altar))
                    {
                        moves.Add(target);
                    }
                }

                if (here == TileType.Ladder && CanOccupy(point + GridPoint.Up))
                {
                    moves.Add(point + GridPoint.Up);
                }

                if ((here == TileType.Ladder || here == TileType.Bar) && CanOccupy(point + GridPoint.Down))
                {
                    moves.Add(point + GridPoint.Down);
                }
            }

            private bool HasSupport(GridPoint point, TileType here)
            {
                return here == TileType.Ladder || here == TileType.Bar || IsSolidSupport(point + GridPoint.Down);
            }

            private bool IsSolidSupport(GridPoint point)
            {
                if (!InBounds(point))
                {
                    return true;
                }

                TileType tile = Tile(point);
                return tile == TileType.Solid || tile == TileType.Brick || tile == TileType.FragileFloor;
            }

            private bool CanOccupy(GridPoint point)
            {
                if (!InBounds(point))
                {
                    return false;
                }

                TileType tile = Tile(point);
                return tile == TileType.Air || tile == TileType.Ladder || tile == TileType.Bar || tile == TileType.ExitClosed
                    || tile == TileType.ExitOpen || tile == TileType.Altar || tile == TileType.PressurePlate
                    || tile == TileType.GateOpen || (gatesOpen && tile == TileType.GateClosed);
            }

            private bool InBounds(GridPoint point)
            {
                return point.x >= 0 && point.x < width && point.y >= 0 && point.y < height;
            }

            private TileType Tile(GridPoint point)
            {
                return InBounds(point) ? tiles[point.x, point.y] : TileType.Solid;
            }
        }
    }
}
