using System;
using System.Collections.Generic;

namespace DungeonGuardians.Core
{
    public sealed class DungeonSimulation
    {
        private readonly BalanceConfig balance;
        // Accumulates speed per tick; the player steps one cell each time it reaches 1. Starts full so the first step is immediate.
        private float playerMoveBudget = 1f;
        private int playerFallCells;

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
            if (level.dark && level.lightRepelsGuardians && level.torches != null)
            {
                foreach (GridPoint torch in level.torches)
                {
                    for (int dx = -TorchLightCells; dx <= TorchLightCells; dx++)
                    {
                        for (int dy = -TorchLightCells; dy <= TorchLightCells; dy++)
                        {
                            litCells.Add(new GridPoint(torch.x + dx, torch.y + dy));
                        }
                    }
                }
            }

            CollectGold();
        }

        // Cells around each torch (this many in every direction) that guardians will not step into when the level's
        // light repels them. Falling into the light cannot be helped.
        private const int TorchLightCells = 1;
        private readonly HashSet<GridPoint> litCells = new HashSet<GridPoint>();

        // A cell a guardian refuses to step into because it is lit.
        public bool IsSafeLight(GridPoint point)
        {
            return litCells.Contains(point);
        }

        // A life lost but not the level: the explorer starts again from the start cell, the guardians from theirs.
        // Collected gold, the opened exit and dug holes stay as they are.
        public void RevivePlayer()
        {
            State.Lost = false;
            State.LossCause = LossCause.None;
            State.PlayerPosition = State.Definition.playerStart;
            State.PlayerDigTicks = 0;
            playerMoveBudget = 1f;
            GridPoint[] starts = State.Definition.guardians ?? Array.Empty<GridPoint>();
            for (int i = 0; i < State.Guardians.Count && i < starts.Length; i++)
            {
                GuardianState guardian = State.Guardians[i];
                guardian.Position = starts[i];
                guardian.Trapped = false;
                guardian.RespawnTicks = 0;
                guardian.MoveBudget = 0f;
                guardian.ResetBehaviour();
            }

            StateChanged?.Invoke();
        }

        public void Tick(InputSnapshot input)
        {
            if (State.Won || State.Lost)
            {
                return;
            }

            State.Ticks++;
            GridPoint before = State.PlayerPosition;
            bool wasFalling = !HasSupport(before);

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

            // A fall of two cells or more lands with a thud the listeners hear.
            if (wasFalling && !State.PlayerPosition.Equals(before))
            {
                playerFallCells++;
            }

            if (HasSupport(State.PlayerPosition))
            {
                if (playerFallCells >= 2)
                {
                    MakeNoise(State.PlayerPosition);
                }

                playerFallCells = 0;
            }

            UpdateHoles();
            UpdateGuardians();
            UpdateSeals();
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
            // Walking past a ladder never climbs it: while left or right is held and that way is open, the player
            // walks on, even if a diagonal on the d-pad also reports up or down. Climbing takes up or down alone, or
            // a sideways push against a wall.
            else if (wantsHorizontal && CanOccupy(State.PlayerPosition + horizontal))
            {
                direction = horizontal;
            }
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
            MakeNoise(target);
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
                    ClimbOut(hole, false);
                }

                if (hole.RemainingTicks == HeavyClimbOutTicks)
                {
                    ClimbOut(hole, true);
                }

                if (hole.RemainingTicks > 0)
                {
                    continue;
                }

                if (State.PlayerPosition.Equals(hole.Position))
                {
                    State.Lost = true;
                    State.LossCause = LossCause.Buried;
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
        private void ClimbOut(HoleState hole, bool heavy)
        {
            foreach (GuardianState guardian in State.Guardians)
            {
                if ((guardian.Kind == GuardianKind.Heavy) != heavy)
                {
                    continue;
                }

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
                    // Never appear on top of the explorer: while the explorer is within RespawnClearance cells of
                    // the altar, the guardian waits (TZ section 7); the altar keeps glowing as a warning meanwhile.
                    if (guardian.RespawnTicks == 1 && IsNearPlayer(FindRespawnPoint(), RespawnClearance))
                    {
                        continue;
                    }

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

                // An infected guardian stands still between lunges (unless it is falling).
                if (guardian.Kind == GuardianKind.Infected && guardian.RestTicks > 0 && HasSupport(guardian.Position))
                {
                    guardian.RestTicks--;
                    if (guardian.RestTicks == 0)
                    {
                        guardian.LungeCells = InfectedLungeCells;
                        guardian.MoveBudget = 1f;
                    }

                    continue;
                }

                guardian.MoveBudget += step * SpeedFactor(guardian);
                if (guardian.MoveBudget < 1f)
                {
                    continue;
                }

                bool moved = StepGuardian(guardian);
                guardian.MoveBudget = moved ? guardian.MoveBudget - 1f : 1f;
                if (guardian.Kind == GuardianKind.Infected && HasSupport(guardian.Position) && (!moved || --guardian.LungeCells <= 0))
                {
                    guardian.RestTicks = InfectedRestTicks;
                    guardian.MoveBudget = 0f;
                }

                BreakFragileFloor(guardian);
            }
        }

        // Guardian kinds (TZ section 7 adds the plain chaser; the others come from the level design).
        private const float HeavySpeed = 0.7f;
        private const float WardenPatrolSpeed = 0.55f;
        private const float InfectedLungeSpeed = 2.6f;
        private const int InfectedLungeCells = 4;
        // Standing still between lunges (1.2 s); the last InfectedWarningTicks of it its cracks flare.
        private const int InfectedRestTicks = 36;
        public const int InfectedWarningTicks = 14;
        // A heavy guardian climbs out of a hole this many ticks before it closes (0.2 s instead of 0.5 s).
        private const int HeavyClimbOutTicks = 6;
        // A warden sees the explorer this far along its own row, or this close in any direction; it gives up
        // the chase beyond WardenLoseRange.
        private const int WardenSightRow = 7;
        private const int WardenSightNear = 3;
        private const int WardenLoseRange = 9;
        // A listener sees only this close, and hears noises within this many cells.
        private const int ListenerSight = 2;
        private const int ListenerHearing = 16;

        private float SpeedFactor(GuardianState guardian)
        {
            switch (guardian.Kind)
            {
                case GuardianKind.Heavy:
                    return HeavySpeed;
                case GuardianKind.Warden:
                    return guardian.Alerted ? 1f : WardenPatrolSpeed;
                case GuardianKind.Infected:
                    return InfectedLungeSpeed;
                default:
                    return 1f;
            }
        }

        // An infected guardian about to lunge: its cracks flare (for the renderer).
        public static bool IsAboutToLunge(GuardianState guardian)
        {
            return guardian.Kind == GuardianKind.Infected && guardian.RestTicks > 0 && guardian.RestTicks <= InfectedWarningTicks;
        }

        // Where the guardian is heading this step, or null to stand still.
        private GridPoint? GoalFor(GuardianState guardian)
        {
            GridPoint player = State.PlayerPosition;
            switch (guardian.Kind)
            {
                case GuardianKind.Warden:
                {
                    int dx = Math.Abs(guardian.Position.x - player.x);
                    int dy = Math.Abs(guardian.Position.y - player.y);
                    if (guardian.Alerted && Math.Max(dx, dy) > WardenLoseRange)
                    {
                        guardian.Alerted = false;
                    }

                    if (!guardian.Alerted && ((dy == 0 && dx <= WardenSightRow) || Math.Max(dx, dy) <= WardenSightNear))
                    {
                        guardian.Alerted = true;
                    }

                    if (guardian.Alerted)
                    {
                        return player;
                    }

                    // Back on its beat: walk to the end it is heading for, then turn.
                    if (guardian.Position.y != guardian.Post.y || guardian.Position.x < guardian.PatrolLeft || guardian.Position.x > guardian.PatrolRight)
                    {
                        return guardian.Post;
                    }

                    int end = guardian.PatrolDirection > 0 ? guardian.PatrolRight : guardian.PatrolLeft;
                    if (guardian.Position.x == end)
                    {
                        guardian.PatrolDirection = -guardian.PatrolDirection;
                        end = guardian.PatrolDirection > 0 ? guardian.PatrolRight : guardian.PatrolLeft;
                    }

                    return end == guardian.Position.x ? (GridPoint?)null : new GridPoint(end, guardian.Post.y);
                }

                case GuardianKind.Listener:
                {
                    if (Math.Max(Math.Abs(guardian.Position.x - player.x), Math.Abs(guardian.Position.y - player.y)) <= ListenerSight)
                    {
                        guardian.NoiseGoal = null;
                        return player;
                    }

                    if (State.NoiseTick > guardian.HeardTick)
                    {
                        guardian.HeardTick = State.NoiseTick;
                        if (Distance(guardian.Position, State.NoisePoint) <= ListenerHearing)
                        {
                            guardian.NoiseGoal = State.NoisePoint;
                        }
                    }

                    if (guardian.NoiseGoal.HasValue && guardian.Position.Equals(guardian.NoiseGoal.Value))
                    {
                        guardian.NoiseGoal = null;
                    }

                    return guardian.NoiseGoal;
                }

                default:
                    return player;
            }
        }

        private void MakeNoise(GridPoint point)
        {
            State.NoisePoint = point;
            State.NoiseTick = State.Ticks;
        }

        // Planks give way under a heavy guardian: it falls through, and the floor is gone for good.
        private void BreakFragileFloor(GuardianState guardian)
        {
            if (guardian.Kind != GuardianKind.Heavy)
            {
                return;
            }

            GridPoint below = guardian.Position + GridPoint.Down;
            if (InBounds(below) && GetTile(below) == TileType.FragileFloor)
            {
                State.Tiles[below.x, below.y] = TileType.Air;
            }
        }

        private bool StepGuardian(GuardianState guardian)
        {
            // Gravity comes first: with nothing underneath, the guardian falls wherever its route leads.
            // This is how it drops into a freshly dug hole in its path.
            if (!HasSupport(guardian.Position))
            {
                return TryMoveGuardian(guardian, GridPoint.Down, true);
            }

            // The route is re-planned before every step, a couple of times a second rather than every frame,
            // so it follows the player as soon as the player changes tier.
            GridPoint? goal = GoalFor(guardian);
            if (!goal.HasValue || !NextGuardianStep(guardian.Position, goal.Value, out GridPoint next))
            {
                // A listener that cannot get any closer to a noise forgets it.
                guardian.NoiseGoal = null;
                return false;
            }

            return TryMoveGuardian(guardian, new GridPoint(next.x - guardian.Position.x, next.y - guardian.Position.y));
        }

        // Breadth-first search over every move a guardian can make (TZ section 7): walking, climbing, hanging on bars,
        // dropping off them and one-way falls. Gives the first step of a shortest route to the player; when the player
        // cannot be reached, the route leads to the reachable cell closest to the player, where the guardian waits.
        // Neighbours are always tried in the same order, so equal routes are chosen the same way every time.
        // Dug holes are planned as the blocks they were: a guardian does not see the trap and walks straight into it.
        private bool NextGuardianStep(GridPoint from, GridPoint target, out GridPoint next)
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
                AddMove(point + GridPoint.Down, ref count, false);
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

        private void AddMove(GridPoint point, ref int count, bool steered = true)
        {
            if (InBounds(point) && IsPassable(PlannedTile(point)) && !(steered && litCells.Contains(point)))
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
            return tile == TileType.Solid || tile == TileType.Brick || tile == TileType.FragileFloor;
        }

        private static bool IsPassable(TileType tile)
        {
            return tile == TileType.Air || tile == TileType.Ladder || tile == TileType.Bar || tile == TileType.ExitClosed || tile == TileType.ExitOpen || tile == TileType.Altar
                || tile == TileType.PressurePlate || tile == TileType.GateOpen;
        }

        private static int Distance(GridPoint a, GridPoint b)
        {
            return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
        }

        private bool TryMoveGuardian(GuardianState guardian, GridPoint direction, bool falling = false)
        {
            GridPoint next = guardian.Position + direction;
            if (!CanOccupy(next) || (!falling && litCells.Contains(next)))
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
            bool taken = State.RemainingGold.Remove(State.PlayerPosition);
            if (taken)
            {
                MakeNoise(State.PlayerPosition);
            }

            if (taken && State.RemainingGold.Count == 0)
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
                    State.LossCause = LossCause.Guardian;
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
            return tile == TileType.Solid || tile == TileType.Brick || tile == TileType.FragileFloor;
        }

        // The exit is a doorway in the wall, not a block: closed, it is walked past like an empty cell (only the open
        // door ends the level, see CheckExit).
        private bool CanOccupy(GridPoint point)
        {
            if (!InBounds(point))
            {
                return false;
            }

            TileType tile = GetTile(point);
            return tile == TileType.Air || tile == TileType.Ladder || tile == TileType.Bar || tile == TileType.ExitClosed || tile == TileType.ExitOpen || tile == TileType.Altar
                || tile == TileType.PressurePlate || tile == TileType.GateOpen;
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

        // Ticks the gates of a "hold" level stay open after the plate is released (3 s at 30 Hz): time to run through.
        private const int GateHoldTicks = 90;

        // Seal trial: plates pressed by a guardian (or by the explorer, if the level allows) open the gates.
        private void UpdateSeals()
        {
            LevelDefinition level = State.Definition;
            bool pressed = false;
            foreach (GuardianState guardian in State.Guardians)
            {
                pressed |= guardian.RespawnTicks <= 0 && GetTile(guardian.Position) == TileType.PressurePlate;
            }

            pressed |= level.playerPressesPlates && GetTile(State.PlayerPosition) == TileType.PressurePlate;
            State.PlatePressed = pressed;

            bool open;
            if (level.GatesLatch)
            {
                open = State.GatesOpen || pressed;
            }
            else
            {
                State.GateOpenTicks = pressed ? GateHoldTicks : Math.Max(0, State.GateOpenTicks - 1);
                open = State.GateOpenTicks > 0;
            }

            if (open == State.GatesOpen)
            {
                return;
            }

            for (int x = 0; x < level.width; x++)
            {
                for (int y = 0; y < level.height; y++)
                {
                    var cell = new GridPoint(x, y);
                    TileType tile = State.Tiles[x, y];
                    if (open && tile == TileType.GateClosed)
                    {
                        State.Tiles[x, y] = TileType.GateOpen;
                    }
                    else if (!open && tile == TileType.GateOpen && !IsActorAt(cell))
                    {
                        // A gate never comes down on someone standing in it; it closes once they have passed.
                        State.Tiles[x, y] = TileType.GateClosed;
                    }
                }
            }

            State.GatesOpen = open || AnyGateOpen();
        }

        private bool AnyGateOpen()
        {
            foreach (TileType tile in State.Tiles)
            {
                if (tile == TileType.GateOpen)
                {
                    return true;
                }
            }

            return false;
        }

        // Where a dead guardian will appear next: the first altar nobody stands on.
        public GridPoint FindRespawnPoint()
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

        // Within this many cells (horizontally and vertically) the explorer holds back a guardian's return.
        private const int RespawnClearance = 2;

        private bool IsNearPlayer(GridPoint point, int cells)
        {
            return Math.Abs(point.x - State.PlayerPosition.x) < cells && Math.Abs(point.y - State.PlayerPosition.y) < cells;
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
