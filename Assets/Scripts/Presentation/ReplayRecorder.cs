using System;
using System.IO;
using DungeonGuardians.Core;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // Records the keys of a level being played (LevelReplay) and, in the editor, keeps the best winning run of each
    // level in LevelReplays/<id>.txt beside Assets: the runs tools/LevelCheck plays back to show every level still
    // wins. A run replaces the kept one when it loses fewer lives, or as many in fewer ticks, or when the kept one
    // no longer wins.
    public sealed class ReplayRecorder
    {
        private LevelReplay replay = new LevelReplay();

        public static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "LevelReplays"));

        public void Begin(LevelDefinition level)
        {
            replay = new LevelReplay { LevelId = level.id };
        }

        // Before each tick of the simulation, while the explorer is in play.
        public void Tick(InputSnapshot input)
        {
            replay.Add(input);
        }

        public void Revive()
        {
            replay.AddRevive();
        }

        private const string PrefsPrefix = "dg_replay_";

        public static LevelReplay LoadBestReplay(string levelId, LevelDefinition level, BalanceConfig balance)
        {
            if (string.IsNullOrEmpty(levelId))
            {
                return null;
            }

            string serialized = null;
#if UNITY_EDITOR
            try
            {
                string path = Path.Combine(Folder, levelId + ".txt");
                if (File.Exists(path))
                {
                    serialized = File.ReadAllText(path);
                }
            }
            catch
            {
            }
#endif
            if (string.IsNullOrEmpty(serialized))
            {
                serialized = PlayerPrefs.GetString(PrefsPrefix + levelId, null);
            }

            if (string.IsNullOrEmpty(serialized))
            {
                return null;
            }

            try
            {
                LevelReplay parsed = LevelReplay.Parse(serialized);
                if (level != null && balance != null)
                {
                    LevelReplay.RunResult result = parsed.Run(level, balance);
                    if (!result.Won)
                    {
                        return null;
                    }
                }
                return parsed;
            }
            catch
            {
                return null;
            }
        }

        public void SaveWin(LevelDefinition level, BalanceConfig balance)
        {
            if (level == null || balance == null)
            {
                return;
            }

            try
            {
                LevelReplay.RunResult check = replay.Run(level, balance);
                if (!check.Won)
                {
                    Debug.LogWarning($"Replay of {level.id} does not win when played back ({check.Problem}); not kept.");
                    return;
                }

                LevelReplay existing = LoadBestReplay(level.id, level, balance);
                if (existing != null)
                {
                    LevelReplay.RunResult existingRun = existing.Run(level, balance);
                    bool better = !existingRun.Won
                        || check.LivesLost < existingRun.LivesLost
                        || (check.LivesLost == existingRun.LivesLost && check.Ticks < existingRun.Ticks);
                    if (!better)
                    {
                        return;
                    }
                }

                string serialized = replay.Serialize($"record {DateTime.Now:yyyy-MM-dd HH:mm}");
                PlayerPrefs.SetString(PrefsPrefix + level.id, serialized);
                PlayerPrefs.Save();

#if UNITY_EDITOR
                try
                {
                    Directory.CreateDirectory(Folder);
                    string path = Path.Combine(Folder, level.id + ".txt");
                    File.WriteAllText(path, serialized);
                    Debug.Log($"Replay of {level.id} kept: {check.Ticks} ticks, {check.LivesLost} lives lost ({path}).");
                }
                catch
                {
                }
#endif
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Replay of {level.id} not kept: {exception.Message}");
            }
        }
    }
}
