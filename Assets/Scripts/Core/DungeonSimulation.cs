using System;
using System.Collections.Generic;

namespace DungeonGuardians.Core
{
    public sealed class DungeonSimulation
    {
        private readonly BalanceConfig balance;
        // Accumulates speed per tick; the player steps one cell each time it reaches 1. Starts full so the first step is immediate.
        private float playerMoveBudget = 1f;

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
            if (GetTile(guardian.Position) == TileType.Air && !HasSupport(guardian.Position))
            {
                return TryMoveGuardian(guardian, GridPoint.Down);
            }

            int horizontal = Math.Sign(State.PlayerPosition.x - guardian.Position.x);
            if (horizontal != 0 && TryMoveGuardian(guardian, new GridPoint(horizontal, 0)))
            {
                return true;
            }

            int vertical = Math.Sign(State.PlayerPosition.y - guardian.Position.y);
            return vertical != 0 && GetTile(guardian.Position) == TileType.Ladder && TryMoveGuardian(guardian, new GridPoint(0, vertical));
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
                if (guardian.RespawnTicks <= 0 && guardian.Position.Equals(State.PlayerPosition))
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

            return IsSolidSupport(point + GridPoint.Down);
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
