using System.IO;
using DungeonGuardians.Core;
using DungeonGuardians.Presentation;
using UnityEditor;
using UnityEngine;

namespace DungeonGuardians.Editor
{
    // "Dungeon Guardians > Проверить уровни": every level can be won, and its recorded replay still wins
    // (Core/LevelValidation.cs; the same check tools/LevelCheck runs in CI). From the command line it exits with 1 when
    // a level fails:
    //   Unity.exe -batchmode -quit -projectPath . -executeMethod DungeonGuardians.Editor.LevelCheckMenu.RunFromCommandLine
    public static class LevelCheckMenu
    {
        [MenuItem("Dungeon Guardians/Проверить уровни")]
        public static void Run()
        {
            LevelValidation.Report report = Check();
            string summary = report.Text.ToString();
            if (report.Passed)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }

            EditorUtility.DisplayDialog("Проверка уровней",
                report.Passed
                    ? $"Уровней: {report.Levels}. Не пройти: 0. Только ценой жизней: {report.Warned}. Без записи прохождения: {report.WithoutReplay}.\n\nПодробности в консоли."
                    : $"Не пройти уровней: {report.Failed} из {report.Levels}.\n\nПодробности в консоли.",
                "OK");
        }

        public static void RunFromCommandLine()
        {
            LevelValidation.Report report = Check();
            Debug.Log(report.Text.ToString());
            EditorApplication.Exit(report.Passed ? 0 : 1);
        }

        private static LevelValidation.Report Check()
        {
            LevelCatalog catalog = LevelCatalog.LoadFromResources();
            return LevelValidation.CheckAll(catalog.Levels, id =>
            {
                string path = Path.Combine(ReplayRecorder.Folder, id + ".txt");
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }, new BalanceConfig(), false);
        }
    }
}
