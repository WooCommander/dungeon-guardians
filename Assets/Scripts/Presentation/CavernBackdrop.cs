using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // The static background behind a level: the level's painting (Resources/Backgrounds) over a cold teal haze that
    // fills any margin around it. Flat unlit sprites far behind the gameplay plane; nothing here moves.
    public sealed class CavernBackdrop : MonoBehaviour
    {
        // Depths behind the block plane (z = 0) and draw order for the transparent layers, far to near.
        private const float GradientDepth = 9f;
        private const float FarDepth = 8f;
        private const int GradientOrder = -100;
        private const int FarOrder = -90;

        // How much larger than the view the painting is drawn when it drifts behind a level larger than the screen.
        private const float ParallaxMargin = 0.12f;
        // The painting's teal already matches the concept screen's background; it is kept at full strength so the
        // warm blocks and gold stand out against cool depths, as on the concept.
        private static readonly Color PaintingTint = Color.white;

        // Painted backgrounds live in Assets/Resources/Backgrounds; each level names its own (LevelDefinition.background),
        // falling back to the cavern.
        private const string DefaultPainting = "cavern";

        private SpriteRenderer painting;

        // True when a painted background is shown; it already has its own side walls and floor rubble.
        public bool HasPainting => painting != null;

        private static Sprite gradient;

        public static CavernBackdrop Build(Transform parent, int width, int height, string paintingName = null)
        {
            var root = new GameObject("Cavern Backdrop");
            root.transform.SetParent(parent, false);
            var backdrop = root.AddComponent<CavernBackdrop>();

            // Generous margins: the camera may show more than the level on wide screens and below it for the controls.
            float left = -10f, right = width + 9f, bottom = -10f, top = height + 4f;
            backdrop.Quad("Haze", GetGradient(), new Vector2((left + right) / 2f, (bottom + top) / 2f),
                new Vector2(right - left, top - bottom), Color.white, GradientDepth, GradientOrder);

            var art = Resources.Load<Sprite>("Backgrounds/" + (string.IsNullOrEmpty(paintingName) ? DefaultPainting : paintingName));
            if (art == null)
            {
                art = Resources.Load<Sprite>("Backgrounds/" + DefaultPainting);
            }

            if (art != null)
            {
                // Sized to the screen by FitToView once the camera is placed.
                backdrop.painting = backdrop.Quad("Painting", art, Vector2.zero, art.bounds.size, PaintingTint, FarDepth, FarOrder);
            }

            return backdrop;
        }

        // The painting covers the camera's view. parallax (-1..1 on each axis) shows where the camera is within a
        // level larger than the screen: the painting is then drawn a little larger and shifted the other way, so the
        // distant cavern seems to drift slowly behind the level.
        public void FitToView(Camera camera, Vector2 parallax = default)
        {
            if (painting == null)
            {
                return;
            }

            float viewHeight = camera.orthographicSize * 2f;
            float viewWidth = viewHeight * camera.aspect;
            Vector2 art = painting.sprite.bounds.size;
            float margin = parallax == Vector2.zero ? 0f : ParallaxMargin;
            float scale = Mathf.Max(viewWidth / art.x, viewHeight / art.y) * (1f + margin);
            painting.transform.localScale = new Vector3(scale, scale, 1f);
            Vector3 position = transform.InverseTransformPoint(camera.transform.position);
            Vector2 slack = (art * scale - new Vector2(viewWidth, viewHeight)) * 0.5f;
            painting.transform.localPosition = new Vector3(position.x - parallax.x * slack.x, position.y - parallax.y * slack.y, FarDepth);
        }

        private SpriteRenderer Quad(string name, Sprite sprite, Vector2 center, Vector2 size, Color color, float depth, int order)
        {
            var item = new GameObject(name);
            item.transform.SetParent(transform, false);
            item.transform.localPosition = new Vector3(center.x, center.y, depth);
            item.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        // Vertical cavern haze: dark at the top and bottom, cold teal light in the middle.
        private static Sprite GetGradient()
        {
            if (gradient != null)
            {
                return gradient;
            }

            const int height = 128;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Cavern Haze" };
            Color[] keys =
            {
                new Color(0.02f, 0.05f, 0.06f),
                new Color(0.05f, 0.15f, 0.17f),
                new Color(0.11f, 0.31f, 0.34f),
                new Color(0.06f, 0.17f, 0.2f),
                new Color(0.02f, 0.05f, 0.06f),
            };
            float[] stops = { 0f, 0.3f, 0.58f, 0.85f, 1f };
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                int k = 0;
                while (k < stops.Length - 2 && v > stops[k + 1])
                {
                    k++;
                }

                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(stops[k], stops[k + 1], v));
                texture.SetPixel(0, y, Color.Lerp(keys[k], keys[k + 1], t));
            }

            texture.Apply();
            gradient = Sprite.Create(texture, new Rect(0f, 0f, 1f, height), new Vector2(0.5f, 0.5f), height);
            return gradient;
        }
    }
}
