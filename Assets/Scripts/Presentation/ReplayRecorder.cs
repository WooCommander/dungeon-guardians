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

        public void SaveWin(LevelDefinition level, BalanceConfig balance)
        {
#if UNITY_EDITOR
            try
            {
                LevelReplay.RunResult check = replay.Run(level, balance);
                if (!check.Won)
                {
                    Debug.LogWarning($"Replay of {level.id} does not win when played back ({check.Problem}); not kept.");
                    return;
                }

                string path = Path.Combine(Folder, level.id + ".txt");
                if (File.Exists(path))
                {
                    LevelReplay kept = LevelReplay.Parse(File.ReadAllText(path));
                    LevelReplay.RunResult keptRun = kept.Run(level, balance);
                    bool better = !keptRun.Won || check.LivesLost < keptRun.LivesLost
                        || (check.LivesLost == keptRun.LivesLost && check.Ticks < keptRun.Ticks);
                    if (!better)
                    {
                        return;
                    }
                }

                Directory.CreateDirectory(Folder);
                File.WriteAllText(path, replay.Serialize($"recorded in the editor {DateTime.Now:yyyy-MM-dd HH:mm}"));
                Debug.Log($"Replay of {level.id} kept: {check.Ticks} ticks, {check.LivesLost} lives lost ({path}).");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Replay of {level.id} not kept: {exception.Message}");
            }
#endif
        }
    }
}
