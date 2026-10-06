using System;

namespace DungeonGuardians.Core
{
    // How one guardian behaves; matched to LevelDefinition.guardians by index (missing entries are plain chasers).
    [Serializable]
    public sealed class GuardianSpec
    {
        // "chaser", "warden", "listener", "heavy" or "infected".
        public string kind = "chaser";
        // A warden's beat: the columns it walks between on the row it starts on.
        public int patrolLeft = -1;
        public int patrolRight = -1;
    }

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
        public GuardianSpec[] guardianKinds = Array.Empty<GuardianSpec>();
        public GridPoint[] altars = Array.Empty<GridPoint>();
        // Torch stands (decoration); the level builder puts an indestructible block under each one.
        public GridPoint[] torches = Array.Empty<GridPoint>();
        // "fit": the whole level on screen (puzzle halls). "follow": the camera follows the explorer at the size of
        // a 13-row hall, for long galleries and deep shafts.
        public string view = "fit";
        // The painted background, a picture in Resources/Backgrounds (cavern, mine, city, flooded, seal, heart).
        public string background = "cavern";

        public bool FollowCamera => view == "follow";

        // A dark hall: only the helmet lamp and the torches light the way.
        public bool dark;
        // In a dark hall, guardians shun torchlight: the cells around a torch are safe islands.
        public bool lightRepelsGuardians;

        // Seal trial: every pressure plate ('_') opens every gate ('|'). "latch": the gates stay open once opened;
        // "hold": they stay open while a plate is pressed and a few seconds after.
        public string gateMode = "latch";
        // Whether the explorer can press the plates, or only a guardian can.
        public bool playerPressesPlates;

        public bool GatesLatch => gateMode != "hold";
    }
}
