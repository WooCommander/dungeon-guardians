using System;
using System.Collections.Generic;

namespace DungeonGuardians.Core
{
    public sealed class RuntimeLevelState
    {
        public readonly LevelDefinition Definition;
        public readonly TileType[,] Tiles;
        public readonly HashSet<GridPoint> RemainingGold;
        public readonly List<GuardianState> Guardians;
        public readonly List<HoleState> Holes;
        public GridPoint PlayerPosition;
        public bool ExitOpen;
        public bool Won;
        public bool Lost;

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
