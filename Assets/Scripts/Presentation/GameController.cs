using System.Collections;
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
        private GameMenu menu;
        private MusicPlayer music;
        private FootstepPlayer footsteps;
        private DefeatSequence defeat;
        private BalanceConfig balance;
        private ProgressStore progressStore;
        private PlayerProgress progress;
        private LevelCatalog catalog;
        private DungeonSimulation simulation;
        private int levelIndex;
        private float accumulator;
        private bool paused;
        private bool lossReported;
        private bool winReported;
        private Coroutine victoryDelay;
        // A short pause on the threshold before the victory panel.
        private const float VictoryDelay = 0.8f;
        // Catches the explorer survives on one attempt at a level; each leaves a stone statue behind.
        private const int Lives = 3;
        private int livesLeft;
        private float levelStartTime;
        private float lastCompletionTime;
        private int lastEarnedStars;
        private bool lastIsNewBest;

        public void Initialize(PlayerInputBridge input, LevelRenderer levelRenderer, GameHud hud, GameMenu menu, MusicPlayer music, BalanceConfig balance, ProgressStore progressStore)
        {
            this.input = input;
            this.levelRenderer = levelRenderer;
            this.hud = hud;
            this.menu = menu;
            this.music = music;
            footsteps = gameObject.AddComponent<FootstepPlayer>();
            defeat = gameObject.AddComponent<DefeatSequence>();
            this.balance = balance;
            this.progressStore = progressStore;
            progress = progressStore.Load();
            catalog = LevelCatalog.LoadFromResources();

            if (catalog.Levels.Count == 0)
            {
                Debug.LogError("No levels found in Resources/Levels.");
                return;
            }

            // Navigation events
            hud.MenuRequested += ShowMenu;
            hud.MapRequested += ShowLevelMap;
            hud.NextRequested += () => LoadLevel(levelIndex + 1);
            menu.PlayLevel += StartLevel;
            menu.SettingsChanged += hud.RefreshControls;
            menu.ResetProgress += ResetProgress;
            menu.Initialize(progress, catalog);
        }

        private void ResetProgress()
        {
            if (progress != null)
            {
                progress.ResetAll();
            }
            else
            {
                progress = new PlayerProgress();
            }
            progressStore.Clear();
            progressStore.Save(progress);
        }

        private void ShowLevelMap()
        {
            simulation = null;
            paused = false;
            defeat.Stop();
            music.SetMood(MusicPlayer.Mood.Menu);
            hud.SetVisible(false);
            menu.OpenMap();
        }

        private void StartLevel(int index)
        {
            menu.Hide();
            hud.SetVisible(true);
            LoadLevel(index);
        }

        // The stone explorer stays where it was caught; a new one sets out from the start.
        private void NextLife(bool buried)
        {
            if (simulation == null)
            {
                return;
            }

            levelRenderer.LeaveStatue(!buried);
            lossReported = false;
            hud.ShowMessage(string.Empty);
            simulation.RevivePlayer();
        }

        private void ShowMenu()
        {
            // The level stops; it stays built behind the opaque start screen until the next one replaces it.
            simulation = null;
            paused = false;
            defeat.Stop();
            StopVictoryDelay();
            hud.HideDefeat();
            hud.HideVictory();
            hud.SetPaused(false);
            hud.SetVisible(false);
            menu.Show();
            music.SetMood(MusicPlayer.Mood.Menu);
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
            int step = input.ReadLevelStep();
            if (step != 0)
            {
                hotkeyLevel = Mathf.Clamp(levelIndex + step, 0, catalog.Levels.Count - 1);
            }

            if (hotkeyLevel >= 0 && hotkeyLevel < catalog.Levels.Count)
            {
                LoadLevel(hotkeyLevel);
                return;
            }

            if (input.ReadToggleMobileView())
            {
#if UNITY_EDITOR
                GameSettings.ForceTouchInEditor = !GameSettings.ForceTouchInEditor;
                hud.RefreshControls();
#endif
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
                    if (!winReported)
                    {
                        winReported = true;
                        CompleteLevel();
                    }

                    break;
                }

                if (simulation.State.Lost)
                {
                    if (!lossReported)
                    {
                        // A lost level keeps ticking without changes; play the defeat only once.
                        lossReported = true;
                        livesLeft--;
                        hud.SetLives(livesLeft, Lives);
                        bool buried = simulation.State.LossCause == LossCause.Buried;
                        if (livesLeft > 0)
                        {
                            hud.ShowMessage(livesLeft == 1 ? "Осталась последняя жизнь" : $"Осталось жизней: {livesLeft}");
                            defeat.Play(levelRenderer.Player, levelRenderer.PlayerLamp, buried, () => NextLife(buried));
                        }
                        else
                        {
                            string title = buried ? "Тебя замуровало в камне" : "Хранитель остановил тебя";
                            defeat.Play(levelRenderer.Player, levelRenderer.PlayerLamp, buried, () => hud.ShowDefeat(title));
                        }
                    }

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
            music.SetMood(paused ? MusicPlayer.Mood.Paused : MusicPlayer.Mood.Game);
        }

        private void LoadLevel(int index)
        {
            levelIndex = Mathf.Clamp(index, 0, catalog.Levels.Count - 1);
            levelStartTime = Time.time;
            paused = false;
            lossReported = false;
            winReported = false;
            StopVictoryDelay();
            livesLeft = Lives;
            defeat.Stop();
            pendingDigLeft = false;
            pendingDigRight = false;
            accumulator = 0f;
            simulation = new DungeonSimulation(catalog.Levels[levelIndex], balance);
            simulation.StateChanged += Render;
            footsteps.BeginLevel();
            hud.Bind(input);
            hud.RefreshControls();
            hud.SetLevel(catalog.Levels[levelIndex].title, levelIndex + 1, catalog.Levels.Count);
            hud.SetPaused(false);
            hud.HideDefeat();
            hud.HideVictory();
            hud.SetLives(livesLeft, Lives);
            music.SetMood(MusicPlayer.Mood.Game);
            hud.ShowMessage(string.Empty);
            Render();
        }

        private void Render()
        {
            levelRenderer.Render(simulation);
            footsteps.Track(simulation);
            hud.SetGold(simulation.State.Definition.gold.Length - simulation.State.RemainingGold.Count, simulation.State.Definition.gold.Length);
            hud.SetExit(simulation.State.ExitOpen);
        }

        private void CompleteLevel()
        {
            string completedId = catalog.Levels[levelIndex].id;
            lastCompletionTime = Mathf.Max(0.1f, Time.time - levelStartTime);
            lastEarnedStars = livesLeft >= 3 ? 3 : (livesLeft == 2 ? 2 : 1);

            lastIsNewBest = progress.RecordCompletion(levelIndex, completedId, lastCompletionTime, lastEarnedStars, out bool isNewBestTime);
            progress.lastSelectedLevelIndex = Mathf.Min(levelIndex + 1, catalog.Levels.Count - 1);
            progressStore.Save(progress);

            StopVictoryDelay();
            victoryDelay = StartCoroutine(ShowVictoryLater());
        }

        // The explorer steps through the door; a moment later the victory panel. After the last level it closes the
        // story instead of offering the next one.
        private IEnumerator ShowVictoryLater()
        {
            yield return new WaitForSeconds(VictoryDelay);
            victoryDelay = null;
            LevelDefinition level = catalog.Levels[levelIndex];
            bool hasNext = levelIndex + 1 < catalog.Levels.Count;
            if (hasNext)
            {
                hud.ShowVictory("Уровень пройден", $"«{level.title}» — всё золото собрано", lastEarnedStars, lastCompletionTime, lastIsNewBest, true);
            }
            else
            {
                hud.ShowVictory("Все залы пройдены",
                    "Золото печатей собрано. Но внизу, за последним сводом, что-то шевельнулось…", lastEarnedStars, lastCompletionTime, lastIsNewBest, false);
            }
        }

        private void StopVictoryDelay()
        {
            if (victoryDelay != null)
            {
                StopCoroutine(victoryDelay);
                victoryDelay = null;
            }
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
