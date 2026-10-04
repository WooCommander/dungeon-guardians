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
        // Dig taps are one-shot: keep them until a simulation tick consumes them, since not every frame has a tick.
        private bool pendingDigLeft;
        private bool pendingDigRight;

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
                SetPaused(!paused);
            }

            int hotkeyLevel = input.ReadLevelHotkey();
            if (hotkeyLevel >= 0 && hotkeyLevel < catalog.Levels.Count)
            {
                LoadLevel(hotkeyLevel);
                return;
            }

            if (snapshot.Restart)
            {
                LoadLevel(levelIndex);
                return;
            }

            if (paused)
            {
                pendingDigLeft = false;
                pendingDigRight = false;
                return;
            }

            pendingDigLeft |= snapshot.DigLeft;
            pendingDigRight |= snapshot.DigRight;

            accumulator += Time.deltaTime;
            float tickLength = 1f / balance.TickRate;
            while (accumulator >= tickLength)
            {
                accumulator -= tickLength;
                InputSnapshot tickInput = snapshot;
                tickInput.DigLeft = pendingDigLeft;
                tickInput.DigRight = pendingDigRight;
                pendingDigLeft = false;
                pendingDigRight = false;
                simulation.Tick(tickInput);

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
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && simulation != null)
            {
                SetPaused(true);
            }
        }

        private void SetPaused(bool value)
        {
            paused = value;
            pendingDigLeft = false;
            pendingDigRight = false;
            hud.SetPaused(paused);
        }

        private void LoadLevel(int index)
        {
            levelIndex = Mathf.Clamp(index, 0, catalog.Levels.Count - 1);
            paused = false;
            pendingDigLeft = false;
            pendingDigRight = false;
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
            levelRenderer.Render(simulation);
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
