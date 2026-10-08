using System.Collections.Generic;

namespace DungeonGuardians.Core
{
    // A walk through a level in the real simulation with its guardians taken out: every tick the explorer takes the
    // shortest way to the next piece of gold (in the order LevelReachability found), then to the exit, on the map as it
    // is now (dug holes included), pressing the key for the first step. A win shows that the route LevelReachability
    // found works under DungeonSimulation's own rules, so the two cannot drift apart unnoticed.
    public static class LevelWalkthrough
    {
        // Gives up after this long (5 minutes of play).
        private const int MaxTicks = 30 * 60 * 5;

        // route: the places to visit in order, the exit last. Returns the winning keys, or null with the reason.
        public static LevelReplay Play(LevelDefinition level, BalanceConfig balance, IList<GridPoint> route, out string problem)
        {
            problem = string.Empty;
            var targets = new List<GridPoint>(route);
            var simulation = new DungeonSimulation(WithoutGuardians(level), balance);
            RuntimeLevelState state = simulation.State;
            var replay = new LevelReplay { LevelId = level.id };
            while (state.Ticks < MaxTicks)
            {
                // The next place still to visit: gold not yet taken, then the exit.
                while (targets.Count > 1 && !state.RemainingGold.Contains(targets[0]))
                {
                    targets.RemoveAt(0);
                }

                InputSnapshot input = Step(simulation, targets[0]);
                replay.Add(input);
                simulation.Tick(input);
                if (state.Won)
                {
                    return replay;
                }

                if (state.Lost)
                {
                    problem = $"tick {state.Ticks}: " + (state.LossCause == LossCause.Buried ? "buried" : "caught by a guardian") + $" at {state.PlayerPosition}";
                    return null;
                }
            }

            problem = $"no win after {MaxTicks} ticks";
            return null;
        }

        // The level as it is, with nobody but the explorer in it.
        public static LevelDefinition WithoutGuardians(LevelDefinition level)
        {
            return new LevelDefinition
            {
                id = level.id, version = level.version, title = level.title, width = level.width, height = level.height,
                rows = level.rows, playerStart = level.playerStart, exit = level.exit, gold = level.gold,
                guardians = new GridPoint[0], guardianKinds = new GuardianSpec[0], altars = level.altars, torches = level.torches,
                view = level.view, background = level.background, dark = level.dark, lightRepelsGuardians = level.lightRepelsGuardians,
                gateMode = level.gateMode, playerPressesPlates = level.playerPressesPlates,
            };
        }

        // The key for the first step of a shortest way from the explorer to the target.
        private static InputSnapshot Step(DungeonSimulation simulation, GridPoint target)
        {
            RuntimeLevelState state = simulation.State;
            GridPoint start = state.PlayerPosition;
            if (start.Equals(target))
            {
                return InputSnapshot.Empty;
            }

            var first = new Dictionary<GridPoint, InputSnapshot> { [start] = InputSnapshot.Empty };
            var queue = new Queue<GridPoint>();
            queue.Enqueue(start);
            var moves = new List<(GridPoint, InputSnapshot)>();
            while (queue.Count > 0)
            {
                GridPoint point = queue.Dequeue();
                moves.Clear();
                Moves(simulation, point, moves);
                foreach ((GridPoint next, InputSnapshot key) in moves)
                {
                    if (first.ContainsKey(next))
                    {
                        continue;
                    }

                    first[next] = point.Equals(start) ? key : first[point];
                    if (next.Equals(target))
                    {
                        return first[next];
                    }

                    queue.Enqueue(next);
                }
            }

            // No way there now (a hole still closing, a gate shut): wait.
            return InputSnapshot.Empty;
        }

        // The explorer's moves from point, with the key that makes each, as in DungeonSimulation.MovePlayer and TryDig.
        private static void Moves(DungeonSimulation simulation, GridPoint point, List<(GridPoint, InputSnapshot)> moves)
        {
            RuntimeLevelState state = simulation.State;
            TileType here = Tile(state, point);
            if (!simulation.HasSupport(point))
            {
                if (CanOccupy(state, point + GridPoint.Down))
                {
                    moves.Add((point + GridPoint.Down, InputSnapshot.Empty));
                }

                return;
            }

            foreach (int side in new[] { -1, 1 })
            {
                GridPoint beside = point + new GridPoint(side, 0);
                if (CanOccupy(state, beside))
                {
                    moves.Add((beside, new InputSnapshot { Left = side < 0, Right = side > 0 }));
                }

                GridPoint target = point + new GridPoint(side, -1);
                TileType above = Tile(state, target + GridPoint.Up);
                if (IsSolid(Tile(state, point + GridPoint.Down)) && Tile(state, target) == TileType.Brick
                    && (above == TileType.Air || above == TileType.Altar))
                {
                    moves.Add((target, new InputSnapshot { DigLeft = side < 0, DigRight = side > 0 }));
                }
            }

            if (here == TileType.Ladder && CanOccupy(state, point + GridPoint.Up))
            {
                moves.Add((point + GridPoint.Up, new InputSnapshot { Up = true }));
            }

            if ((here == TileType.Ladder || here == TileType.Bar) && CanOccupy(state, point + GridPoint.Down))
            {
                moves.Add((point + GridPoint.Down, new InputSnapshot { Down = true }));
            }
        }

        private static bool IsSolid(TileType tile)
        {
            return tile == TileType.Solid || tile == TileType.Brick || tile == TileType.FragileFloor;
        }

        private static bool CanOccupy(RuntimeLevelState state, GridPoint point)
        {
            TileType tile = Tile(state, point);
            return point.x >= 0 && point.y >= 0 && point.x < state.Definition.width && point.y < state.Definition.height
                && (tile == TileType.Air || tile == TileType.Ladder || tile == TileType.Bar || tile == TileType.ExitClosed
                    || tile == TileType.ExitOpen || tile == TileType.Altar || tile == TileType.PressurePlate || tile == TileType.GateOpen);
        }

        private static TileType Tile(RuntimeLevelState state, GridPoint point)
        {
            bool inside = point.x >= 0 && point.y >= 0 && point.x < state.Definition.width && point.y < state.Definition.height;
            return inside ? state.Tiles[point.x, point.y] : TileType.Solid;
        }
    }
}
