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
        private SettingsScreen inGameSettings;
        private MusicPlayer music;
        private FootstepPlayer footsteps;
        private SoundEffectPlayer sfx;
        private GhostRunner ghost;
        private DefeatSequence defeat;
        private BalanceConfig balance;
        private ProgressStore progressStore;
        private PlayerProgress progress;
        private LevelCatalog catalog;
        private DungeonSimulation simulation;
        // The keys of the attempt in play; in the editor a win is kept as the level's replay (tools/LevelCheck).
        private readonly ReplayRecorder recorder = new ReplayRecorder();
        private int levelIndex;
        private float accumulator;
        private bool paused;
        private bool settingsOpen;
        private bool pauseBeforeSettings;
        private bool pendingDigLeft;
        private bool pendingDigRight;
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
            sfx = gameObject.AddComponent<SoundEffectPlayer>();
            defeat = gameObject.AddComponent<DefeatSequence>();
            ghost = gameObject.AddComponent<GhostRunner>();
            this.balance = balance;
            ghost.Initialize(levelRenderer.transform, balance);
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
            hud.SettingsRequested += OpenInGameSettings;
            hud.NextLevelRequested += PlayNextLevel;
            hud.RestartLevelRequested += RestartCurrentLevel;
            menu.PlayLevel += StartLevel;
            menu.SettingsChanged += hud.RefreshControls;
            menu.ResetProgress += ResetProgress;
            menu.Initialize(progress, catalog);
            Canvas settingsCanvas = MenuStyle.CreateCanvas("In-Game Settings", transform, 30);
            inGameSettings = SettingsScreen.Create(settingsCanvas.transform);
            inGameSettings.Changed += hud.RefreshControls;
            inGameSettings.Closed += CloseInGameSettings;
            inGameSettings.Close();
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
            CloseInGameSettings();
            if (sfx != null)
            {
                sfx.BindSimulation(null);
            }
            if (ghost != null)
            {
                ghost.SetVisible(false);
            }
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
            recorder.Revive();
            simulation.RevivePlayer();
        }

        private void ShowMenu()
        {
            CloseInGameSettings();
            // The level stops; it stays built behind the opaque start screen until the next one replaces it.
            if (sfx != null)
            {
                sfx.BindSimulation(null);
            }
            if (ghost != null)
            {
                ghost.SetVisible(false);
            }
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

            if (settingsOpen)
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

            float zoomDelta = input.ReadPinchZoomDelta();
            if (Mathf.Abs(zoomDelta) > 0.0001f)
            {
                levelRenderer.ApplyZoomDelta(zoomDelta);
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
                if (!simulation.State.Won && !simulation.State.Lost)
                {
                    recorder.Tick(tickInput);
                }

                simulation.Tick(tickInput);
                if (ghost != null)
                {
                    ghost.Tick();
                }

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
                            hud.ShowMessage(livesLeft == 1 ? Localization.T("hud_last_life") : Localization.T("hud_lives_left", livesLeft));
                            defeat.Play(levelRenderer.Player, levelRenderer.PlayerLamp, buried, () => NextLife(buried));
                        }
                        else
                        {
                            string title = buried ? Localization.T("hud_defeat_buried") : Localization.T("hud_defeat_caught");
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

        private void OpenInGameSettings()
        {
            if (simulation == null || inGameSettings == null || settingsOpen)
            {
                return;
            }

            pauseBeforeSettings = paused;
            settingsOpen = true;
            SetPaused(true);
            inGameSettings.Open();
        }

        private void CloseInGameSettings()
        {
            if (!settingsOpen)
            {
                return;
            }

            settingsOpen = false;
            if (inGameSettings != null && inGameSettings.gameObject.activeSelf)
            {
                inGameSettings.Close();
                return;
            }

            SetPaused(pauseBeforeSettings);
            hud.RefreshControls();
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
            recorder.Begin(catalog.Levels[levelIndex]);
            simulation.StateChanged += Render;
            footsteps.BeginLevel();
            if (sfx != null)
            {
                sfx.BindSimulation(simulation);
            }
            if (ghost != null)
            {
                ghost.BeginLevel(catalog.Levels[levelIndex]);
            }
            hud.Bind(input);
            hud.RefreshControls();
            hud.SetLevel(Localization.GetLevelTitle(catalog.Levels[levelIndex].id, catalog.Levels[levelIndex].title), levelIndex + 1, catalog.Levels.Count);
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
            LevelDefinition level = catalog.Levels[levelIndex];
            string completedId = level.id;
            recorder.SaveWin(level, balance);
            lastCompletionTime = Mathf.Max(0.1f, Time.time - levelStartTime);

            float targetTime = level.GetTargetTime();
            bool flawless = livesLeft >= Lives;
            bool beatSpeed = lastCompletionTime <= targetTime;

            // 1 star: complete level, +1 star: no lives lost (3/3), +1 star: beat speed par time
            lastEarnedStars = 1 + (flawless ? 1 : 0) + (beatSpeed ? 1 : 0);

            lastIsNewBest = progress.RecordCompletion(levelIndex, completedId, lastCompletionTime, lastEarnedStars, out bool isNewBestTime);
            progress.lastSelectedLevelIndex = Mathf.Min(levelIndex + 1, catalog.Levels.Count - 1);
            progressStore.Save(progress);

            StopVictoryDelay();
            victoryDelay = StartCoroutine(ShowVictoryLater());
        }

        private void PlayNextLevel()
        {
            int next = levelIndex + 1;
            if (next < catalog.Levels.Count)
            {
                hud.HideVictory();
                LoadLevel(next);
            }
            else
            {
                ShowLevelMap();
            }
        }

        private void RestartCurrentLevel()
        {
            hud.HideVictory();
            LoadLevel(levelIndex);
        }

        // The explorer steps through the door; a moment later the victory panel. After the last level it closes the
        // story instead of offering the next one.
        private IEnumerator ShowVictoryLater()
        {
            yield return new WaitForSeconds(VictoryDelay);
            victoryDelay = null;
            LevelDefinition level = catalog.Levels[levelIndex];
            bool hasNext = levelIndex + 1 < catalog.Levels.Count;
            float targetTime = level.GetTargetTime();
            string localizedTitle = Localization.GetLevelTitle(level.id, level.title);

            if (hasNext)
            {
                hud.ShowVictory(Localization.T("hud_victory_title"), Localization.T("hud_victory_subtitle", localizedTitle), lastEarnedStars, lastCompletionTime, targetTime, livesLeft, Lives, lastIsNewBest, true);
            }
            else
            {
                hud.ShowVictory(Localization.T("hud_all_won_title"),
                    Localization.T("hud_all_won_subtitle"), lastEarnedStars, lastCompletionTime, targetTime, livesLeft, Lives, lastIsNewBest, false);
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
