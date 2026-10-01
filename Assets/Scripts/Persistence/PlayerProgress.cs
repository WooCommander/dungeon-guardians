using System;
using System.Collections.Generic;

namespace DungeonGuardians.Persistence
{
    [Serializable]
    public sealed class PlayerProgress
    {
        public string lastLevelId;
        public List<string> unlockedLevelIds = new List<string>();
        public List<string> completedLevelIds = new List<string>();
    }
}
