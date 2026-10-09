using System.Collections.Generic;
using DungeonGuardians.Core;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // Plays back the player's personal best record run as a semi-transparent cyan ghost silhouette during gameplay.
    // Gives visual feedback for speedrunning and race-against-best-time.
    public sealed class GhostRunner : MonoBehaviour
    {
        private const float GhostHeight = 1.1f;
        private const float GhostWalkPlayback = 1.6f;
        private static readonly Color GhostTint = new Color(0.25f, 0.85f, 1.0f, 0.45f);

        private CharacterView ghostView;
        private DungeonSimulation ghostSimulation;
        private LevelDefinition currentLevel;
        private BalanceConfig currentBalance;
        private LevelReplay currentReplay;
        private readonly List<InputSnapshot> tickInputs = new List<InputSnapshot>();
        private readonly HashSet<int> reviveTicks = new HashSet<int>();
        private int currentTick;
        private bool hasGhost;
        private bool won;

        public bool HasGhost => hasGhost;

        public void Initialize(Transform parent, BalanceConfig balance)
        {
            currentBalance = balance;
            if (ghostView == null)
            {
                ghostView = CharacterView.Create("explorer", parent, GhostHeight, balance.PlayerSpeed, GhostTint, GhostWalkPlayback);
                ghostView.gameObject.name = "Ghost Explorer";
                ghostView.SetTint(GhostTint);
                ghostView.SetVisible(false);
            }
        }

        public void BeginLevel(LevelDefinition level)
        {
            currentLevel = level;
            currentTick = 0;
            won = false;
            tickInputs.Clear();
            reviveTicks.Clear();

            currentReplay = ReplayRecorder.LoadBestReplay(level.id, level, currentBalance);
            if (currentReplay == null || currentReplay.Steps.Count == 0)
            {
                hasGhost = false;
                ghostSimulation = null;
                if (ghostView != null)
                {
                    ghostView.SetVisible(false);
                }
                return;
            }

            // Unpack steps to discrete ticks
            int tickIndex = 0;
            foreach (LevelReplay.Step step in currentReplay.Steps)
            {
                if (step.Revive)
                {
                    reviveTicks.Add(tickIndex);
                }
                else
                {
                    for (int t = 0; t < step.Ticks; t++)
                    {
                        tickInputs.Add(step.Input);
                        tickIndex++;
                    }
                }
            }

            if (tickInputs.Count == 0)
            {
                hasGhost = false;
                if (ghostView != null)
                {
                    ghostView.SetVisible(false);
                }
                return;
            }

            hasGhost = true;
            ghostSimulation = new DungeonSimulation(level, currentBalance);
            if (ghostView != null)
            {
                ghostView.SetVisible(true);
                ghostView.SetTint(GhostTint);
                ghostView.SnapNextMove();
                ghostView.SetTarget(ToWorld(ghostSimulation.State.PlayerPosition));
            }
        }

        public void Tick()
        {
            if (!hasGhost || ghostSimulation == null || won || ghostView == null)
            {
                return;
            }

            if (reviveTicks.Contains(currentTick))
            {
                ghostSimulation.RevivePlayer();
                ghostView.SnapNextMove();
            }

            if (currentTick < tickInputs.Count)
            {
                InputSnapshot input = tickInputs[currentTick];
                ghostSimulation.Tick(input);

                if (ghostSimulation.State.Won)
                {
                    won = true;
                    ghostView.SetTarget(ToWorld(ghostSimulation.State.Definition.exit));
                    ghostView.SetVisible(false);
                    return;
                }

                Vector3 ghostWorld = ToWorld(ghostSimulation.State.PlayerPosition);
                ghostView.SetTarget(ghostWorld);

                if (ghostSimulation.State.PlayerDigTicks > 0)
                {
                    ghostView.SetPose(CharacterPose.Dig, ghostSimulation.State.PlayerDigDirection);
                }
                else
                {
                    ghostView.SetPose(MovementPose(ghostSimulation, ghostSimulation.State.PlayerPosition));
                }

                currentTick++;
            }
            else
            {
                // Replay finished
                ghostView.SetVisible(false);
            }
        }

        public void SetVisible(bool visible)
        {
            if (ghostView != null && (!visible || hasGhost))
            {
                ghostView.SetVisible(visible && hasGhost && !won);
            }
        }

        private static Vector3 ToWorld(GridPoint point)
        {
            return new Vector3(point.x, point.y, 0.02f);
        }

        private static CharacterPose MovementPose(DungeonSimulation simulation, GridPoint position)
        {
            switch (simulation.State.Tiles[position.x, position.y])
            {
                case TileType.Ladder:
                    return CharacterPose.Ladder;
                case TileType.Bar:
                    return CharacterPose.Bar;
            }

            return simulation.HasSupport(position) ? CharacterPose.Ground : CharacterPose.Fall;
        }
    }
}
