using UnityEngine;
using DungeonGuardians.Core;

namespace DungeonGuardians.Presentation
{
    // Renders gentle, animated contextual micro-hints on early levels (1-3):
    // 1. Pickaxe indicators with swinging motion and halo over diggable brick blocks.
    // 2. Beacon guide with pulse and downward indicator over the open exit door when all gold is collected.
    public sealed class ContextualHintOverlay : MonoBehaviour
    {
        private const float BobSpeed = 4.5f;
        private const float BobAmount = 0.08f;
        private const float ScalePulseSpeed = 3.2f;
        private const float SwingAngle = 18f;

        private GameObject digHintLeft;
        private GameObject digHintRight;
        private SpriteRenderer digRendererLeft;
        private SpriteRenderer digRendererRight;
        private SpriteRenderer digHaloLeft;
        private SpriteRenderer digHaloRight;

        private GameObject exitArrow;
        private SpriteRenderer exitArrowRenderer;
        private SpriteRenderer exitGlowRenderer;

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
            Sprite digLeftSprite = Resources.Load<Sprite>("UI/pickaxe_left") ?? GetFallbackPickaxeSprite(true);
            Sprite digRightSprite = Resources.Load<Sprite>("UI/pickaxe_right") ?? GetFallbackPickaxeSprite(false);
            Sprite haloSprite = ExitGlow.GetHaloSprite();

            // 1. Dig Left Indicator
            digHintLeft = new GameObject("DigHint_Left");
            digHintLeft.transform.SetParent(transform, false);

            var haloLeftObj = new GameObject("Halo");
            haloLeftObj.transform.SetParent(digHintLeft.transform, false);
            haloLeftObj.transform.localScale = Vector3.one * 1.5f;
            digHaloLeft = haloLeftObj.AddComponent<SpriteRenderer>();
            digHaloLeft.sprite = haloSprite;
            digHaloLeft.color = new Color(0.25f, 0.85f, 1f, 0.45f);
            digHaloLeft.sortingOrder = 19;

            var iconLeftObj = new GameObject("Icon");
            iconLeftObj.transform.SetParent(digHintLeft.transform, false);
            digRendererLeft = iconLeftObj.AddComponent<SpriteRenderer>();
            digRendererLeft.sprite = digLeftSprite;
            digRendererLeft.color = new Color(1f, 1f, 1f, 0.65f);
            digRendererLeft.sortingOrder = 20;

            digHintLeft.transform.localScale = Vector3.one * 0.65f;
            digHintLeft.SetActive(false);

            // 2. Dig Right Indicator
            digHintRight = new GameObject("DigHint_Right");
            digHintRight.transform.SetParent(transform, false);

            var haloRightObj = new GameObject("Halo");
            haloRightObj.transform.SetParent(digHintRight.transform, false);
            haloRightObj.transform.localScale = Vector3.one * 1.5f;
            digHaloRight = haloRightObj.AddComponent<SpriteRenderer>();
            digHaloRight.sprite = haloSprite;
            digHaloRight.color = new Color(0.25f, 0.85f, 1f, 0.45f);
            digHaloRight.sortingOrder = 19;

            var iconRightObj = new GameObject("Icon");
            iconRightObj.transform.SetParent(digHintRight.transform, false);
            digRendererRight = iconRightObj.AddComponent<SpriteRenderer>();
            digRendererRight.sprite = digRightSprite;
            digRendererRight.color = new Color(1f, 1f, 1f, 0.65f);
            digRendererRight.sortingOrder = 20;

            digHintRight.transform.localScale = Vector3.one * 0.65f;
            digHintRight.SetActive(false);

            // 3. Exit Beacon Arrow
            exitArrow = new GameObject("Exit_Arrow");
            exitArrow.transform.SetParent(transform, false);

            var exitGlowObj = new GameObject("Glow");
            exitGlowObj.transform.SetParent(exitArrow.transform, false);
            exitGlowObj.transform.localScale = Vector3.one * 1.8f;
            exitGlowRenderer = exitGlowObj.AddComponent<SpriteRenderer>();
            exitGlowRenderer.sprite = haloSprite;
            exitGlowRenderer.color = new Color(1f, 0.85f, 0.3f, 0.35f);
            exitGlowRenderer.sortingOrder = 19;

            var arrowIconObj = new GameObject("Indicator");
            arrowIconObj.transform.SetParent(exitArrow.transform, false);
            exitArrowRenderer = arrowIconObj.AddComponent<SpriteRenderer>();
            exitArrowRenderer.sprite = haloSprite;
            exitArrowRenderer.color = new Color(1f, 0.92f, 0.45f, 0.65f);
            exitArrowRenderer.sortingOrder = 20;
            arrowIconObj.transform.localScale = new Vector3(0.6f, 0.9f, 1f);

            exitArrow.transform.localScale = Vector3.one * 0.85f;
            exitArrow.SetActive(false);
        }

        private static Sprite fallbackPickaxeLeft;
        private static Sprite fallbackPickaxeRight;

        private static Sprite GetFallbackPickaxeSprite(bool left)
        {
            if (left && fallbackPickaxeLeft != null) return fallbackPickaxeLeft;
            if (!left && fallbackPickaxeRight != null) return fallbackPickaxeRight;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = left ? "PickaxeLeft" : "PickaxeRight", filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }

            // Draw clean diagonal handle and curved pickaxe head
            float dir = left ? -1f : 1f;
            Vector2 headCenter = new Vector2(size * (left ? 0.35f : 0.65f), size * 0.65f);
            Vector2 handleEnd = new Vector2(size * (left ? 0.75f : 0.25f), size * 0.25f);

            // Handle
            for (float t = 0f; t <= 1f; t += 0.02f)
            {
                Vector2 p = Vector2.Lerp(headCenter, handleEnd, t);
                DrawThickDot(tex, p, 2.5f, new Color(0.85f, 0.65f, 0.35f, 0.95f));
            }

            // Head arc
            for (float a = -0.9f; a <= 0.9f; a += 0.04f)
            {
                float px = headCenter.x + Mathf.Cos(a + (left ? 2.3f : 0.8f)) * 20f * dir;
                float py = headCenter.y + Mathf.Sin(a + (left ? 2.3f : 0.8f)) * 14f;
                DrawThickDot(tex, new Vector2(px, py), 3f, new Color(0.95f, 0.95f, 1f, 0.98f));
            }

            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            if (left) fallbackPickaxeLeft = sprite;
            else fallbackPickaxeRight = sprite;
            return sprite;
        }

        private static void DrawThickDot(Texture2D tex, Vector2 pos, float radius, Color color)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt(pos.x - radius), 0, tex.width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(pos.x + radius), 0, tex.width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(pos.y - radius), 0, tex.height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(pos.y + radius), 0, tex.height - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), pos);
                    if (dist <= radius)
                    {
                        float alpha = color.a * Mathf.Clamp01(1f - (dist / radius) * 0.4f);
                        Color existing = tex.GetPixel(x, y);
                        tex.SetPixel(x, y, Color.Lerp(existing, new Color(color.r, color.g, color.b, 1f), alpha));
                    }
                }
            }
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
            float swing = Mathf.Sin(Time.time * BobSpeed) * SwingAngle;

            // Positioned clearly in front of the 3D block geometry (z = -0.6f) with soft translucency
            if (canDigLeft)
            {
                GridPoint brickPos = new GridPoint(pos.x - 1, pos.y - 1);
                digHintLeft.transform.position = new Vector3(brickPos.x, brickPos.y + 0.45f + bob, -0.6f);
                digHintLeft.transform.localScale = Vector3.one * (0.65f + 0.08f * pulse);
                digHintLeft.transform.rotation = Quaternion.Euler(0f, 0f, swing);
                digRendererLeft.color = new Color(1f, 1f, 1f, 0.45f + 0.2f * pulse);
                digHaloLeft.color = new Color(0.3f, 0.9f, 1f, 0.2f + 0.15f * pulse);
                if (!digHintLeft.activeSelf) digHintLeft.SetActive(true);
            }
            else if (digHintLeft.activeSelf)
            {
                digHintLeft.SetActive(false);
            }

            if (canDigRight)
            {
                GridPoint brickPos = new GridPoint(pos.x + 1, pos.y - 1);
                digHintRight.transform.position = new Vector3(brickPos.x, brickPos.y + 0.45f + bob, -0.6f);
                digHintRight.transform.localScale = Vector3.one * (0.65f + 0.08f * pulse);
                digHintRight.transform.rotation = Quaternion.Euler(0f, 0f, -swing);
                digRendererRight.color = new Color(1f, 1f, 1f, 0.45f + 0.2f * pulse);
                digHaloRight.color = new Color(0.3f, 0.9f, 1f, 0.2f + 0.15f * pulse);
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
            float bob = Mathf.Sin(Time.time * BobSpeed * 0.85f) * 0.12f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * ScalePulseSpeed);

            exitArrow.transform.position = new Vector3(exit.x, exit.y + 0.95f + bob, -0.6f);
            exitArrow.transform.localScale = Vector3.one * (0.75f + 0.2f * pulse);
            exitGlowRenderer.color = new Color(1f, 0.85f, 0.3f, 0.25f + 0.15f * pulse);
            exitArrowRenderer.color = new Color(1f, 0.92f, 0.45f, 0.5f + 0.2f * pulse);
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
