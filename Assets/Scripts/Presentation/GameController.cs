using System.Collections.Generic;
using DungeonGuardians.Core;
using DungeonGuardians.Input;
using DungeonGuardians.Persistence;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    public sealed class GameController : MonoBehaviour
    {
        private PlayerInputBridge input;
        private LevelRenderer levelRenderer;
        private GameHud hud;
        private BalanceConfig balance;
        private ProgressStore progressStore;
        private PlayerProgress progress;
        private LevelCatalog catalog;
        private DungeonSimulation simulation;
        private int levelIndex;
        private float accumulator;
        private bool paused;

        public void Initialize(PlayerInputBridge input, LevelRenderer levelRenderer, GameHud hud, BalanceConfig balance, ProgressStore progressStore)
        {
            this.input = input;
            this.levelRenderer = levelRenderer;
            this.hud = hud;
            this.balance = balance;
            this.progressStore = progressStore;
            progress = progressStore.Load();
            catalog = LevelCatalog.LoadFromResources();

            if (catalog.Levels.Count == 0)
            {
                Debug.LogError("No levels found in Resources/Levels.");
                return;
            }

            levelIndex = FindLevelIndex(progress.lastLevelId);
            LoadLevel(levelIndex);
        }

        private void Update()
        {
            if (simulation == null)
            {
                return;
            }

            InputSnapshot snapshot = input.Read();
            if (snapshot.Pause)
            {
                paused = !paused;
                hud.SetPaused(paused);
            }

            if (snapshot.Restart)
            {
                LoadLevel(levelIndex);
                return;
            }

            if (paused)
            {
                return;
            }

            accumulator += Time.deltaTime;
            float tickLength = 1f / balance.TickRate;
            while (accumulator >= tickLength)
            {
                accumulator -= tickLength;
                simulation.Tick(snapshot);

                if (simulation.State.Won)
                {
                    CompleteLevel();
                    break;
                }

                if (simulation.State.Lost)
                {
                    hud.ShowMessage("Поражение");
                    break;
                }

                snapshot = InputSnapshot.Empty;
            }
        }

        private void LoadLevel(int index)
        {
            levelIndex = Mathf.Clamp(index, 0, catalog.Levels.Count - 1);
            paused = false;
            accumulator = 0f;
            simulation = new DungeonSimulation(catalog.Levels[levelIndex], balance);
            simulation.StateChanged += Render;
            hud.Bind(input);
            hud.SetLevel(catalog.Levels[levelIndex].title, levelIndex + 1, catalog.Levels.Count);
            hud.SetPaused(false);
            hud.ShowMessage(string.Empty);
            Render();
        }

        private void Render()
        {
            levelRenderer.Render(simulation.State);
            hud.SetGold(simulation.State.Definition.gold.Length - simulation.State.RemainingGold.Count, simulation.State.Definition.gold.Length);
            hud.SetExit(simulation.State.ExitOpen);
        }

        private void CompleteLevel()
        {
            string completedId = catalog.Levels[levelIndex].id;
            AddUnique(progress.completedLevelIds, completedId);

            int nextIndex = Mathf.Min(levelIndex + 1, catalog.Levels.Count - 1);
            AddUnique(progress.unlockedLevelIds, catalog.Levels[nextIndex].id);
            progress.lastLevelId = catalog.Levels[nextIndex].id;
            progressStore.Save(progress);
            hud.ShowMessage(levelIndex + 1 >= catalog.Levels.Count ? "Все уровни пройдены" : "Выход открыт");

            if (levelIndex + 1 < catalog.Levels.Count)
            {
                LoadLevel(nextIndex);
            }
        }

        private int FindLevelIndex(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                return 0;
            }

            for (int i = 0; i < catalog.Levels.Count; i++)
            {
                if (catalog.Levels[i].id == levelId)
                {
                    return i;
                }
            }

            return 0;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }
    }
}
