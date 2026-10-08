using System;
using System.Collections.Generic;

namespace DungeonGuardians.Persistence
{
    [Serializable]
    public sealed class LevelRecord
    {
        public int levelIndex;
        public string levelId = string.Empty;
        public bool completed;
        public int stars;
        public float bestTimeSeconds;
        public int attempts;
    }

    [Serializable]
    public sealed class PlayerProgress
    {
        public int highestUnlockedIndex = 0;
        public int lastSelectedLevelIndex = 0;
        public List<LevelRecord> records = new List<LevelRecord>();

        public void ResetAll()
        {
            highestUnlockedIndex = 0;
            lastSelectedLevelIndex = 0;
            records.Clear();
        }

        public LevelRecord GetOrCreateRecord(int levelIndex, string levelId)
        {
            foreach (LevelRecord record in records)
            {
                if (record.levelIndex == levelIndex)
                {
                    if (string.IsNullOrEmpty(record.levelId))
                    {
                        record.levelId = levelId;
                    }
                    return record;
                }
            }

            var newRecord = new LevelRecord
            {
                levelIndex = levelIndex,
                levelId = levelId ?? string.Empty
            };
            records.Add(newRecord);
            return newRecord;
        }

        public bool IsUnlocked(int levelIndex)
        {
            return levelIndex <= highestUnlockedIndex;
        }

        public bool RecordCompletion(int levelIndex, string levelId, float timeSeconds, int starsAwarded, out bool isNewBestTime)
        {
            isNewBestTime = false;
            LevelRecord record = GetOrCreateRecord(levelIndex, levelId);
            record.completed = true;

            if (starsAwarded > record.stars)
            {
                record.stars = starsAwarded;
            }

            if (record.bestTimeSeconds <= 0.001f || timeSeconds < record.bestTimeSeconds)
            {
                record.bestTimeSeconds = timeSeconds;
                isNewBestTime = true;
            }

            if (levelIndex + 1 > highestUnlockedIndex)
            {
                highestUnlockedIndex = levelIndex + 1;
            }

            return isNewBestTime;
        }

        public int GetCompletedCount()
        {
            int count = 0;
            foreach (LevelRecord record in records)
            {
                if (record.completed)
                {
                    count++;
                }
            }
            return count;
        }

        public int GetTotalStars()
        {
            int total = 0;
            foreach (LevelRecord record in records)
            {
                total += record.stars;
            }
            return total;
        }
    }
}
