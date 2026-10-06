using System;
using System.Collections.Generic;

namespace DungeonGuardians.Core
{
    public sealed class DungeonSimulation
    {
        private readonly BalanceConfig balance;
        // Accumulates speed per tick; the player steps one cell each time it reaches 1. Starts full so the first step is immediate.
        private float playerMoveBudget = 1f;

        // Reused buffers for the guardians' route search.
        private readonly Queue<int> searchQueue = new Queue<int>();
        private readonly GridPoint[] searchMoves = new GridPoint[4];
        private int[] searchPrevious;

        public RuntimeLevelState State { get; private set; }
        public BalanceConfig Balance => balance;
        public event Action StateChanged;

        public DungeonSimulation(LevelDefinition level, BalanceConfig balance)
        {
            this.balance = balance;
            State = new RuntimeLevelState(level);
            CollectGold();
        }

        public void Tick(InputSnapshot input)
        {
            if (State.Won || State.Lost)
            {
                return;
            }

            if (State.PlayerDigTicks > 0)
            {
                State.PlayerDigTicks--;
            }
            else if (input.DigLeft)
            {
                TryDig(-1);
            }
            else if (input.DigRight)
            {
                TryDig(1);
            }
            else
            {
                MovePlayer(input);
            }

            UpdateHoles();
            UpdateGuardians();
            CollectGold();
            CheckExit();
            CheckDefeat();
            StateChanged?.Invoke();
        }

        private void MovePlayer(InputSnapshot input)
        {
            GridPoint direction = new GridPoint(0, 0);
            TileType current = GetTile(State.PlayerPosition);
            bool onLadder = current == TileType.Ladder;
            bool wantsUp = input.Up && onLadder;
            bool wantsDown = input.Down && (onLadder || current == TileType.Bar);
            bool wantsHorizontal = input.Left || input.Right;
            GridPoint horizontal = input.Left ? GridPoint.Left : GridPoint.Right;

            // Without support the player falls regardless of input.
            if (!HasSupport(State.PlayerPosition))
            {
                direction = GridPoint.Down;
            }
            // On a ladder vertical input wins when the move is possible; elsewhere horizontal wins.
            else if (onLadder && wantsUp && CanOccupy(State.PlayerPosition + GridPoint.Up))
            {
                direction = GridPoint.Up;
            }
            else if (onLadder && wantsDown && CanOccupy(State.PlayerPosition + GridPoint.Down))
            {
                direction = GridPoint.Down;
            }
            else if (wantsHorizontal)
            {
                direction = horizontal;
            }
            else if (wantsUp)
            {
                direction = GridPoint.Up;
            }
            else if (wantsDown)
            {
                direction = GridPoint.Down;
            }

            float step = balance.PlayerSpeed / balance.TickRate;
            GridPoint next = State.PlayerPosition + direction;
            bool hasDirection = direction.x != 0 || direction.y != 0;
            if (!hasDirection || !CanOccupy(next))
            {
                playerMoveBudget = Math.Min(playerMoveBudget + step, 1f);
                return;
            }

            playerMoveBudget += step;
            if (playerMoveBudget < 1f)
            {
                return;
            }

            playerMoveBudget -= 1f;
            State.PlayerPosition = next;
        }

        private void TryDig(int horizontalOffset)
        {
            GridPoint feet = State.PlayerPosition + GridPoint.Down;
            if (!IsSolidSupport(feet))
            {
                return;
            }

            GridPoint target = State.PlayerPosition + new GridPoint(horizontalOffset, -1);
            if (!InBounds(target) || GetTile(target) != TileType.Brick || IsActorAt(target))
            {
                return;
            }

            // The cell above the target must be open: no solid block, ladder, bar or exit.
            TileType aboveTarget = GetTile(target + GridPoint.Up);
            if (aboveTarget != TileType.Air && aboveTarget != TileType.Altar)
            {
                return;
            }

            State.Tiles[target.x, target.y] = TileType.Air;
            State.Holes.Add(new HoleState(target, TileType.Brick, balance.HoleTicks));
            State.PlayerDigTicks = balance.DigTicks;
            State.PlayerDigDirection = horizontalOffset;
        }

        private void UpdateHoles()
        {
            for (int i = State.Holes.Count - 1; i >= 0; i--)
            {
                HoleState hole = State.Holes[i];
                hole.RemainingTicks--;
                if (hole.RemainingTicks == balance.GuardianClimbOutTicks)
                {
                    ClimbOut(hole);
                }

                if (hole.RemainingTicks > 0)
                {
                    continue;
                }

                if (State.PlayerPosition.Equals(hole.Position))
                {
                    State.Lost = true;
                }

                for (int g = 0; g < State.Guardians.Count; g++)
                {
                    GuardianState guardian = State.Guardians[g];
                    if (guardian.Position.Equals(hole.Position))
                    {
                        guardian.Trapped = false;
                        guardian.RespawnTicks = balance.GuardianRespawnTicks;
                    }
                }

                State.Tiles[hole.Position.x, hole.Position.y] = hole.RestoresTo;
                State.Holes.RemoveAt(i);
            }
        }

        // Just before the hole closes, a trapped guardian climbs onto the cell above one of its edges: towards the
        // explorer first, then the other way. With both sides blocked it stays and is buried when the hole closes.
        private void ClimbOut(HoleState hole)
        {
            foreach (GuardianState guardian in State.Guardians)
            {
                if (guardian.RespawnTicks > 0 || !guardian.Trapped || !guardian.Position.Equals(hole.Position))
                {
                    continue;
                }

                int towardsPlayer = State.PlayerPosition.x < hole.Position.x ? -1 : 1;
                foreach (int side in new[] { towardsPlayer, -towardsPlayer })
                {
                    GridPoint edge = hole.Position + new GridPoint(side, 1);
                    if (CanOccupy(edge))
                    {
                        guardian.Position = edge;
                        guardian.Trapped = false;
                        guardian.MoveBudget = 0f;
                        break;
                    }
                }
            }
        }

        private void UpdateGuardians()
        {
            float step = balance.GuardianSpeed / balance.TickRate;
            foreach (GuardianState guardian in State.Guardians)
            {
                if (guardian.RespawnTicks > 0)
                {
                    guardian.RespawnTicks--;
                    if (guardian.RespawnTicks == 0)
                    {
                        guardian.Position = FindRespawnPoint();
                        guardian.MoveBudget = 0f;
                    }

                    continue;
                }

                // A trapped guardian stays in its hole until the hole closes over it.
                if (guardian.Trapped)
                {
                    continue;
                }

                guardian.MoveBudget += step;
                if (guardian.MoveBudget < 1f)
                {
                    continue;
                }

                guardian.MoveBudget = StepGuardian(guardian) ? guardian.MoveBudget - 1f : 1f;
            }
        }

        private bool StepGuardian(GuardianState guardian)
        {
            // Gravity comes first: with nothing underneath, the guardian falls wherever its route leads.
            // This is how it drops into a freshly dug hole in its path.
            if (!HasSupport(guardian.Position))
            {
                return TryMoveGuardian(guardian, GridPoint.Down);
            }

            // The route is re-planned before every step, a couple of times a second rather than every frame,
            // so it follows the player as soon as the player changes tier.
            if (!NextGuardianStep(guardian.Position, out GridPoint next))
            {
                return false;
            }

            return TryMoveGuardian(guardian, new GridPoint(next.x - guardian.Position.x, next.y - guardian.Position.y));
        }

        // Breadth-first search over every move a guardian can make (TZ section 7): walking, climbing, hanging on bars,
        // dropping off them and one-way falls. Gives the first step of a shortest route to the player; when the player
        // cannot be reached, the route leads to the reachable cell closest to the player, where the guardian waits.
        // Neighbours are always tried in the same order, so equal routes are chosen the same way every time.
        // Dug holes are planned as the blocks they were: a guardian does not see the trap and walks straight into it.
        private bool NextGuardianStep(GridPoint from, out GridPoint next)
        {
            int width = State.Definition.width;
            int height = State.Definition.height;
            if (searchPrevious == null || searchPrevious.Length != width * height)
            {
                searchPrevious = new int[width * height];
            }

            for (int i = 0; i < searchPrevious.Length; i++)
            {
                searchPrevious[i] = -1;
            }

            GridPoint target = State.PlayerPosition;
            int start = from.y * width + from.x;
            int goal = target.y * width + target.x;
            int best = start;
            int bestDistance = Distance(from, target);
            searchPrevious[start] = start;
            searchQueue.Clear();
            searchQueue.Enqueue(start);

            while (searchQueue.Count > 0)
            {
                int current = searchQueue.Dequeue();
                var point = new GridPoint(current % width, current / width);
                if (current == goal)
                {
                    best = current;
                    break;
                }

                int distance = Distance(point, target);
                if (distance < bestDistance)
                {
                    best = current;
                    bestDistance = distance;
                }

                int moves = GuardianMoves(point);
                for (int m = 0; m < moves; m++)
                {
                    int index = searchMoves[m].y * width + searchMoves[m].x;
                    if (searchPrevious[index] == -1)
                    {
                        searchPrevious[index] = current;
                        searchQueue.Enqueue(index);
                    }
                }
            }

            next = from;
            if (best == start)
            {
                return false;
            }

            int step = best;
            while (searchPrevious[step] != start)
            {
                step = searchPrevious[step];
            }

            next = new GridPoint(step % width, step / width);
            return true;
        }

        // The cells a guardian can move to from point in one step, written to searchMoves in a fixed order.
        private int GuardianMoves(GridPoint point)
        {
            int count = 0;
            if (!PlannedSupport(point))
            {
                // Falling cannot be steered.
                AddMove(point + GridPoint.Down, ref count);
                return count;
            }

            TileType tile = PlannedTile(point);
            AddMove(point + GridPoint.Left, ref count);
            AddMove(point + GridPoint.Right, ref count);
            if (tile == TileType.Ladder)
            {
                AddMove(point + GridPoint.Up, ref count);
            }

            if (tile == TileType.Ladder || tile == TileType.Bar)
            {
                AddMove(point + GridPoint.Down, ref count);
            }

            return count;
        }

        private void AddMove(GridPoint point, ref int count)
        {
            if (InBounds(point) && IsPassable(PlannedTile(point)))
            {
                searchMoves[count++] = point;
            }
        }

        // The map as the guardians plan on it: an open hole still counts as the block it was dug from.
        private TileType PlannedTile(GridPoint point)
        {
            foreach (HoleState hole in State.Holes)
            {
                if (hole.Position.Equals(point))
                {
                    return hole.RestoresTo;
                }
            }

            return GetTile(point);
        }

        private bool PlannedSupport(GridPoint point)
        {
            TileType current = PlannedTile(point);
            if (current == TileType.Ladder || current == TileType.Bar)
            {
                return true;
            }

            GridPoint below = point + GridPoint.Down;
            TileType ground = PlannedTile(below);
            return !InBounds(below) || IsSolid(ground) || IsTrappedGuardianAt(below);
        }

        private static bool IsSolid(TileType tile)
        {
            return tile == TileType.Solid || tile == TileType.Brick || tile == TileType.ExitClosed || tile == TileType.ExitOpen;
        }

        private static bool IsPassable(TileType tile)
        {
            return tile == TileType.Air || tile == TileType.Ladder || tile == TileType.Bar || tile == TileType.ExitOpen || tile == TileType.Altar;
        }

        private static int Distance(GridPoint a, GridPoint b)
        {
            return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
        }

        private bool TryMoveGuardian(GuardianState guardian, GridPoint direction)
        {
            GridPoint next = guardian.Position + direction;
            if (!CanOccupy(next))
            {
                return false;
            }

            guardian.Position = next;
            foreach (HoleState hole in State.Holes)
            {
                if (hole.Position.Equals(next))
                {
                    guardian.Trapped = true;
                    break;
                }
            }

            return true;
        }

        private void CollectGold()
        {
            if (State.RemainingGold.Remove(State.PlayerPosition) && State.RemainingGold.Count == 0)
            {
                State.ExitOpen = true;
                State.Tiles[State.Definition.exit.x, State.Definition.exit.y] = TileType.ExitOpen;
            }
        }

        private void CheckExit()
        {
            State.Won = State.ExitOpen && State.PlayerPosition.Equals(State.Definition.exit);
        }

        private void CheckDefeat()
        {
            foreach (GuardianState guardian in State.Guardians)
            {
                // A guardian caught in a hole is harmless for now (TZ section 7).
                if (guardian.RespawnTicks <= 0 && !guardian.Trapped && guardian.Position.Equals(State.PlayerPosition))
                {
                    State.Lost = true;
                    return;
                }
            }
        }

        public bool HasSupport(GridPoint point)
        {
            TileType current = GetTile(point);
            if (current == TileType.Ladder || current == TileType.Bar)
            {
                return true;
            }

            // The head of a trapped guardian bridges its hole: the explorer and other guardians walk across it.
            GridPoint below = point + GridPoint.Down;
            return IsSolidSupport(below) || IsTrappedGuardianAt(below);
        }

        private bool IsTrappedGuardianAt(GridPoint point)
        {
            foreach (GuardianState guardian in State.Guardians)
            {
                if (guardian.RespawnTicks <= 0 && guardian.Trapped && guardian.Position.Equals(point))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSolidSupport(GridPoint point)
        {
            if (!InBounds(point))
            {
                return true;
            }

            TileType tile = GetTile(point);
            return tile == TileType.Solid || tile == TileType.Brick || tile == TileType.ExitClosed || tile == TileType.ExitOpen;
        }

        private bool CanOccupy(GridPoint point)
        {
            if (!InBounds(point))
            {
                return false;
            }

            TileType tile = GetTile(point);
            return tile == TileType.Air || tile == TileType.Ladder || tile == TileType.Bar || tile == TileType.ExitOpen || tile == TileType.Altar;
        }

        private bool IsActorAt(GridPoint point)
        {
            if (State.PlayerPosition.Equals(point))
            {
                return true;
            }

            foreach (GuardianState guardian in State.Guardians)
            {
                if (guardian.RespawnTicks <= 0 && guardian.Position.Equals(point))
                {
                    return true;
                }
            }

            return false;
        }

        private GridPoint FindRespawnPoint()
        {
            IReadOnlyList<GridPoint> altars = State.Definition.altars;
            if (altars != null)
            {
                foreach (GridPoint altar in altars)
                {
                    if (!IsActorAt(altar))
                    {
                        return altar;
                    }
                }
            }

            return State.Definition.guardians != null && State.Definition.guardians.Length > 0
                ? State.Definition.guardians[0]
                : State.PlayerPosition;
        }

        private bool InBounds(GridPoint point)
        {
            return point.x >= 0 && point.x < State.Definition.width && point.y >= 0 && point.y < State.Definition.height;
        }

        private TileType GetTile(GridPoint point)
        {
            if (!InBounds(point))
            {
                return TileType.Solid;
            }

            return State.Tiles[point.x, point.y];
        }
    }
}
