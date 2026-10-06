using System;

namespace DungeonGuardians.Core
{
    [Serializable]
    public sealed class LevelDefinition
    {
        public string id;
        public int version = 1;
        public string title;
        public int width;
        public int height;
        public string[] rows;
        public GridPoint playerStart;
        public GridPoint exit;
        public GridPoint[] gold = Array.Empty<GridPoint>();
        public GridPoint[] guardians = Array.Empty<GridPoint>();
        public GridPoint[] altars = Array.Empty<GridPoint>();
        // Torch stands (decoration); the level builder puts an indestructible block under each one.
        public GridPoint[] torches = Array.Empty<GridPoint>();
    }
}
