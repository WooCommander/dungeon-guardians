using UnityEngine;
using DungeonGuardians.Core;

namespace DungeonGuardians.Presentation
{
    // Renders gentle, animated contextual micro-hints on early levels (1-3):
    // 1. Pickaxe indicators over diggable brick blocks when the player stands next to them.
    // 2. Beacon guide towards the exit door when all gold is collected.
    public sealed class ContextualHintOverlay : MonoBehaviour
    {
        private const float BobSpeed = 4.5f;
        private const float BobAmount = 0.08f;
        private const float ScalePulseSpeed = 3.0f;

        private GameObject digHintLeft;
        private GameObject digHintRight;
        private SpriteRenderer digRendererLeft;
        private SpriteRenderer digRendererRight;
        private GameObject exitArrow;
        private SpriteRenderer exitArrowRenderer;

        private LevelDefinition currentLevel;
        private int currentLevelIndex;
        private bool hasDugOnThisLevel;

        public static ContextualHintOverlay Create(Transform parent)
        {
            var root = new GameObject("Contextual Hint Overlay");
            root.transform.SetParent(parent, false);
            var overlay = root.AddComponent<ContextualHintOverlay>();
            overlay.BuildVisuals();
            return overlay;
        }

        private void BuildVisuals()
        {
            Sprite digLeftSprite = Resources.Load<Sprite>("UI/dig_left");
            Sprite digRightSprite = Resources.Load<Sprite>("UI/dig_right");

            // 1. Dig Left Indicator
            digHintLeft = new GameObject("DigHint_Left");
            digHintLeft.transform.SetParent(transform, false);
            digRendererLeft = digHintLeft.AddComponent<SpriteRenderer>();
            digRendererLeft.sprite = digLeftSprite;
            digRendererLeft.color = new Color(1f, 1f, 1f, 0.85f);
            digHintLeft.transform.localScale = Vector3.one * 0.55f;
            digHintLeft.SetActive(false);

            // 2. Dig Right Indicator
            digHintRight = new GameObject("DigHint_Right");
            digHintRight.transform.SetParent(transform, false);
            digRendererRight = digHintRight.AddComponent<SpriteRenderer>();
            digRendererRight.sprite = digRightSprite;
            digRendererRight.color = new Color(1f, 1f, 1f, 0.85f);
            digHintRight.transform.localScale = Vector3.one * 0.55f;
            digHintRight.SetActive(false);

            // 3. Exit Beacon Arrow
            exitArrow = new GameObject("Exit_Arrow");
            exitArrow.transform.SetParent(transform, false);
            exitArrowRenderer = exitArrow.AddComponent<SpriteRenderer>();
            exitArrowRenderer.sprite = ExitGlow.GetHaloSprite();
            exitArrowRenderer.color = new Color(1f, 0.88f, 0.35f, 0.75f);
            exitArrow.transform.localScale = Vector3.one * 0.8f;
            exitArrow.SetActive(false);
        }

        public void BeginLevel(int levelIndex, LevelDefinition level)
        {
            currentLevelIndex = levelIndex;
            currentLevel = level;
            hasDugOnThisLevel = false;

            if (digHintLeft != null) digHintLeft.SetActive(false);
            if (digHintRight != null) digHintRight.SetActive(false);
            if (exitArrow != null) exitArrow.SetActive(false);
        }

        public void UpdateHints(DungeonSimulation simulation)
        {
            if (simulation == null || simulation.State == null || currentLevel == null)
            {
                HideAll();
                return;
            }

            // Only show tutorial hints on the first 3 levels
            bool isIntroLevel = currentLevelIndex < 3;

            // Track if player has performed any dig
            if (simulation.State.PlayerDigTicks > 0)
            {
                hasDugOnThisLevel = true;
            }

            if (isIntroLevel && !hasDugOnThisLevel && !simulation.State.Won && !simulation.State.Lost)
            {
                UpdateDigHints(simulation);
            }
            else
            {
                if (digHintLeft != null && digHintLeft.activeSelf) digHintLeft.SetActive(false);
                if (digHintRight != null && digHintRight.activeSelf) digHintRight.SetActive(false);
            }

            // Exit door beacon when all gold is collected
            if (isIntroLevel && simulation.State.RemainingGold.Count == 0 && !simulation.State.Won && !simulation.State.Lost)
            {
                UpdateExitBeacon(simulation);
            }
            else
            {
                if (exitArrow != null && exitArrow.activeSelf) exitArrow.SetActive(false);
            }
        }

        private void UpdateDigHints(DungeonSimulation simulation)
        {
            GridPoint pos = simulation.State.PlayerPosition;
            bool canDigLeft = CheckCanDig(simulation, pos, -1);
            bool canDigRight = CheckCanDig(simulation, pos, 1);

            float bob = Mathf.Sin(Time.time * BobSpeed) * BobAmount;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * ScalePulseSpeed);

            if (canDigLeft)
            {
                GridPoint brickPos = new GridPoint(pos.x - 1, pos.y - 1);
                digHintLeft.transform.position = new Vector3(brickPos.x, brickPos.y + 0.35f + bob, -0.05f);
                digHintLeft.transform.localScale = Vector3.one * (0.5f + 0.08f * pulse);
                digRendererLeft.color = new Color(1f, 1f, 0.75f, 0.75f + 0.25f * pulse);
                if (!digHintLeft.activeSelf) digHintLeft.SetActive(true);
            }
            else if (digHintLeft.activeSelf)
            {
                digHintLeft.SetActive(false);
            }

            if (canDigRight)
            {
                GridPoint brickPos = new GridPoint(pos.x + 1, pos.y - 1);
                digHintRight.transform.position = new Vector3(brickPos.x, brickPos.y + 0.35f + bob, -0.05f);
                digHintRight.transform.localScale = Vector3.one * (0.5f + 0.08f * pulse);
                digRendererRight.color = new Color(1f, 1f, 0.75f, 0.75f + 0.25f * pulse);
                if (!digHintRight.activeSelf) digHintRight.SetActive(true);
            }
            else if (digHintRight.activeSelf)
            {
                digHintRight.SetActive(false);
            }
        }

        private static bool CheckCanDig(DungeonSimulation simulation, GridPoint playerPos, int horizontalOffset)
        {
            RuntimeLevelState state = simulation.State;
            if (!simulation.HasSupport(playerPos))
            {
                return false;
            }

            GridPoint target = playerPos + new GridPoint(horizontalOffset, -1);
            if (target.x < 0 || target.x >= state.Definition.width || target.y < 0 || target.y >= state.Definition.height)
            {
                return false;
            }

            if (state.Tiles[target.x, target.y] != TileType.Brick)
            {
                return false;
            }

            // Cell above the target brick must be open air or altar
            GridPoint above = target + GridPoint.Up;
            if (above.y >= 0 && above.y < state.Definition.height)
            {
                TileType aboveTile = state.Tiles[above.x, above.y];
                if (aboveTile != TileType.Air && aboveTile != TileType.Altar)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateExitBeacon(DungeonSimulation simulation)
        {
            GridPoint exit = simulation.State.Definition.exit;
            float bob = Mathf.Sin(Time.time * BobSpeed * 0.8f) * 0.12f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * ScalePulseSpeed);

            exitArrow.transform.position = new Vector3(exit.x, exit.y + 0.85f + bob, -0.08f);
            exitArrow.transform.localScale = Vector3.one * (0.7f + 0.2f * pulse);
            exitArrowRenderer.color = new Color(1f, 0.88f, 0.35f, 0.6f + 0.35f * pulse);
            if (!exitArrow.activeSelf) exitArrow.SetActive(true);
        }

        public void HideAll()
        {
            if (digHintLeft != null) digHintLeft.SetActive(false);
            if (digHintRight != null) digHintRight.SetActive(false);
            if (exitArrow != null) exitArrow.SetActive(false);
        }
    }
}
