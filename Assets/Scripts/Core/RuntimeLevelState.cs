using System;
using System.Collections.Generic;

namespace DungeonGuardians.Core
{
    public enum LossCause
    {
        None,
        // Touched by an active guardian.
        Guardian,
        // A hole closed while the explorer was inside it.
        Buried
    }

    public sealed class RuntimeLevelState
    {
        public readonly LevelDefinition Definition;
        public readonly TileType[,] Tiles;
        public readonly HashSet<GridPoint> RemainingGold;
        public readonly List<GuardianState> Guardians;
        public readonly List<HoleState> Holes;
        public GridPoint PlayerPosition;
        // Ticks left in the current dig; movement is locked while it is above zero.
        public int PlayerDigTicks;
        // -1 for left, 1 for right.
        public int PlayerDigDirection;
        public bool ExitOpen;
        public bool Won;
        public bool Lost;
        public LossCause LossCause;

        public RuntimeLevelState(LevelDefinition definition)
        {
            Definition = definition;
            Tiles = LevelParser.ParseTiles(definition);
            RemainingGold = new HashSet<GridPoint>(definition.gold ?? Array.Empty<GridPoint>());
            Guardians = new List<GuardianState>();
            Holes = new List<HoleState>();
            PlayerPosition = definition.playerStart;

            foreach (GridPoint point in definition.guardians ?? Array.Empty<GridPoint>())
            {
                Guardians.Add(new GuardianState(point));
            }
        }
    }

    public sealed class GuardianState
    {
        public GridPoint Position;
        public int RespawnTicks;
        public bool Trapped;
        // Accumulates speed per tick; the guardian steps one cell each time it reaches 1.
        public float MoveBudget;

        public GuardianState(GridPoint position)
        {
            Position = position;
        }
    }

    public sealed class HoleState
    {
        public GridPoint Position;
        public TileType RestoresTo;
        public int RemainingTicks;

        public HoleState(GridPoint position, TileType restoresTo, int remainingTicks)
        {
            Position = position;
            RestoresTo = restoresTo;
            RemainingTicks = remainingTicks;
        }
    }
}
