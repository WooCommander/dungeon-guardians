using System;

namespace DungeonGuardians.Core
{
    public static class LevelParser
    {
        public static TileType[,] ParseTiles(LevelDefinition definition)
        {
            Validate(definition);

            var tiles = new TileType[definition.width, definition.height];
            for (int rowIndex = 0; rowIndex < definition.rows.Length; rowIndex++)
            {
                string row = definition.rows[rowIndex];
                int y = definition.height - 1 - rowIndex;
                for (int x = 0; x < definition.width; x++)
                {
                    tiles[x, y] = ParseTile(row[x]);
                }
            }

            return tiles;
        }

        public static void Validate(LevelDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.width <= 0 || definition.height <= 0)
            {
                throw new InvalidOperationException($"Level {definition.id} has invalid size.");
            }

            if (definition.rows == null || definition.rows.Length != definition.height)
            {
                throw new InvalidOperationException($"Level {definition.id} row count does not match height.");
            }

            for (int i = 0; i < definition.rows.Length; i++)
            {
                if (definition.rows[i] == null || definition.rows[i].Length != definition.width)
                {
                    throw new InvalidOperationException($"Level {definition.id} row {i} does not match width.");
                }
            }
        }

        private static TileType ParseTile(char value)
        {
            switch (value)
            {
                case '#':
                    return TileType.Solid;
                case 'B':
                    return TileType.Brick;
                case 'H':
                    return TileType.Ladder;
                case '-':
                    return TileType.Bar;
                case 'E':
                    return TileType.ExitClosed;
                case 'A':
                    return TileType.Altar;
                case '_':
                    return TileType.PressurePlate;
                case '|':
                    return TileType.GateClosed;
                case '.':
                case ' ':
                    return TileType.Air;
                default:
                    throw new InvalidOperationException($"Unknown tile symbol '{value}'.");
            }
        }
    }
}
