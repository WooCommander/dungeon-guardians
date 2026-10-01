using System;
using System.Collections.Generic;

namespace DungeonGuardians.Core
{
    public sealed class DungeonSimulation
    {
        private readonly BalanceConfig balance;
        private int digLockTicks;

        public RuntimeLevelState State { get; private set; }
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

            if (digLockTicks > 0)
            {
                digLockTicks--;
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

            if (input.Left)
            {
                direction = GridPoint.Left;
            }
            else if (input.Right)
            {
                direction = GridPoint.Right;
            }
            else if (input.Up && current == TileType.Ladder)
            {
                direction = GridPoint.Up;
            }
            else if (input.Down && (current == TileType.Ladder || current == TileType.Bar))
            {
                direction = GridPoint.Down;
            }
            else if (!HasSupport(State.PlayerPosition))
            {
                direction = GridPoint.Down;
            }

            GridPoint next = State.PlayerPosition + direction;
            if (CanOccupy(next))
            {
                State.PlayerPosition = next;
            }
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

            State.Tiles[target.x, target.y] = TileType.Air;
            State.Holes.Add(new HoleState(target, TileType.Brick, balance.HoleTicks));
            digLockTicks = balance.DigTicks;
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
            foreach (GuardianState guardian in State.Guardians)
            {
                if (guardian.RespawnTicks > 0)
                {
                    guardian.RespawnTicks--;
                    if (guardian.RespawnTicks == 0)
                    {
                        guardian.Position = FindRespawnPoint();
                    }

                    continue;
                }

                if (GetTile(guardian.Position) == TileType.Air && !HasSupport(guardian.Position))
                {
                    TryMoveGuardian(guardian, GridPoint.Down);
                    continue;
                }

                int horizontal = Math.Sign(State.PlayerPosition.x - guardian.Position.x);
                if (horizontal != 0 && TryMoveGuardian(guardian, new GridPoint(horizontal, 0)))
                {
                    continue;
                }

                int vertical = Math.Sign(State.PlayerPosition.y - guardian.Position.y);
                if (vertical != 0 && GetTile(guardian.Position) == TileType.Ladder)
                {
                    TryMoveGuardian(guardian, new GridPoint(0, vertical));
                }
            }
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

        private bool HasSupport(GridPoint point)
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
