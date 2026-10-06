using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // The static background behind a level. A painted picture (Resources/Backgrounds/cavern) is used when present;
    // otherwise a plain stand-in: cold teal haze, silhouettes of giant columns, a seated statue and scaffolding,
    // and dark rock below the level. Flat unlit sprites far behind the gameplay plane; nothing here moves.
    public sealed class CavernBackdrop : MonoBehaviour
    {
        // Depths behind the block plane (z = 0) and draw order for the transparent layers, far to near.
        private const float GradientDepth = 9f;
        private const float FarDepth = 8f;
        private const float ScaffoldDepth = 6f;
                private const int GradientOrder = -100;
        private const int FarOrder = -90;
        private const int ScaffoldOrder = -80;
                private const int GroundOrder = -70;

        // How much larger than the view the painting is drawn when it drifts behind a level larger than the screen.
        private const float ParallaxMargin = 0.12f;
        // The painting's teal already matches the concept screen's background; it is kept at full strength so the
        // warm blocks and gold stand out against cool depths, as on the concept.
        private static readonly Color PaintingTint = Color.white;
        private static readonly Color FarColor = new Color(0.07f, 0.21f, 0.24f);
        private static readonly Color StatueColor = new Color(0.09f, 0.25f, 0.28f);
        private static readonly Color ScaffoldColor = new Color(0.035f, 0.11f, 0.13f);
        private static readonly Color GroundColor = new Color(0.04f, 0.045f, 0.05f);

        // Painted backgrounds live in Assets/Resources/Backgrounds; each level names its own (LevelDefinition.background),
        // falling back to the cavern.
        private const string DefaultPainting = "cavern";

        private SpriteRenderer painting;

        // True when a painted background is shown; it already has its own side walls and floor rubble.
        public bool HasPainting => painting != null;

        private static Sprite square;
        private static Sprite gradient;

        public static CavernBackdrop Build(Transform parent, int width, int height, int seed, string paintingName = null)
        {
            var root = new GameObject("Cavern Backdrop");
            root.transform.SetParent(parent, false);
            var backdrop = root.AddComponent<CavernBackdrop>();
            var random = new System.Random(seed);

            // Generous margins: the camera may show more than the level on wide screens and below it for the controls.
            float left = -10f, right = width + 9f, bottom = -10f, top = height + 4f;

            backdrop.Quad("Haze", GetGradient(), new Vector2((left + right) / 2f, (bottom + top) / 2f),
                new Vector2(right - left, top - bottom), Color.white, GradientDepth, GradientOrder);

            // Painted background art, when present, replaces the procedural silhouettes. It covers the view, keeping
            // its aspect ratio; the haze fills any margin on very wide screens.
            var art = Resources.Load<Sprite>("Backgrounds/" + (string.IsNullOrEmpty(paintingName) ? DefaultPainting : paintingName));
            if (art == null)
            {
                art = Resources.Load<Sprite>("Backgrounds/" + DefaultPainting);
            }

            if (art != null)
            {
                // Sized to the screen by FitToView once the camera is placed.
                backdrop.painting = backdrop.Quad("Painting", art, Vector2.zero, art.bounds.size, PaintingTint, FarDepth, FarOrder);
                return backdrop;
            }

            // Giant columns fading into the haze.
            for (float x = left + 2f + (float)random.NextDouble() * 3f; x < right; x += 6f + (float)random.NextDouble() * 3f)
            {
                float columnWidth = 1.4f + (float)random.NextDouble() * 0.8f;
                backdrop.Rect("Column", new Vector2(x, (bottom + top) / 2f), new Vector2(columnWidth, top - bottom), FarColor, FarDepth, FarOrder);
                float capital = height * (0.75f + (float)random.NextDouble() * 0.2f);
                backdrop.Rect("Capital", new Vector2(x, capital), new Vector2(columnWidth + 0.8f, 0.5f), FarColor, FarDepth, FarOrder);
                backdrop.Rect("Base", new Vector2(x, height * 0.1f), new Vector2(columnWidth + 0.8f, 0.5f), FarColor, FarDepth, FarOrder);
            }

            backdrop.Statue(new Vector2(width * 0.3f, height * 0.45f), height / 13f);

            // Scaffolding bridges at two heights, each with a few candles.
            backdrop.Scaffold(new Vector2(width * 0.35f, height * 0.62f), width * 0.35f, random);
            backdrop.Scaffold(new Vector2(width * 0.62f, height * 0.28f), width * 0.4f, random);


            // Dark rock under the level.
            backdrop.Rect("Ground", new Vector2((left + right) / 2f, (bottom - 0.5f) / 2f), new Vector2(right - left, -0.5f - bottom),
                GroundColor, 0.6f, GroundOrder);
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

        private void Statue(Vector2 center, float scale)
        {
            // A seated pharaoh-like figure: headdress, head, shoulders and body, all one fogged tone.
            Rect("Statue Body", center + new Vector2(0f, -2.5f) * scale, new Vector2(4.4f, 6f) * scale, StatueColor, FarDepth - 0.2f, FarOrder + 1);
            Rect("Statue Shoulders", center + new Vector2(0f, 0.8f) * scale, new Vector2(5.6f, 1.6f) * scale, StatueColor, FarDepth - 0.2f, FarOrder + 1);
            Rect("Statue Headdress", center + new Vector2(0f, 2.6f) * scale, new Vector2(3.4f, 3.2f) * scale, StatueColor, FarDepth - 0.2f, FarOrder + 1);
            Rect("Statue Head", center + new Vector2(0f, 3.2f) * scale, new Vector2(1.9f, 2.4f) * scale, StatueColor * 1.15f, FarDepth - 0.25f, FarOrder + 2);
        }

        private void Scaffold(Vector2 center, float length, System.Random random)
        {
            float x0 = center.x - length / 2f;
            float x1 = center.x + length / 2f;
            Rect("Walkway", center, new Vector2(length, 0.2f), ScaffoldColor, ScaffoldDepth, ScaffoldOrder);
            Rect("Railing", center + new Vector2(0f, 0.7f), new Vector2(length, 0.08f), ScaffoldColor, ScaffoldDepth, ScaffoldOrder);

            for (float x = x0; x <= x1 + 0.01f; x += 1.6f)
            {
                // Posts reach down out of sight; braces cross between neighbouring posts.
                Rect("Post", new Vector2(x, center.y - 4f), new Vector2(0.16f, 8f), ScaffoldColor, ScaffoldDepth, ScaffoldOrder);
                Rect("Rail Post", new Vector2(x, center.y + 0.35f), new Vector2(0.08f, 0.7f), ScaffoldColor, ScaffoldDepth, ScaffoldOrder);
                if (x + 1.6f <= x1 + 0.01f)
                {
                    Rect("Brace", new Vector2(x + 0.8f, center.y - 1f), new Vector2(0.07f, 2.2f), ScaffoldColor, ScaffoldDepth, ScaffoldOrder, 36f);
                    Rect("Brace", new Vector2(x + 0.8f, center.y - 1f), new Vector2(0.07f, 2.2f), ScaffoldColor, ScaffoldDepth, ScaffoldOrder, -36f);
                }

            }
        }

        private SpriteRenderer Rect(string name, Vector2 center, Vector2 size, Color color, float depth, int order, float angle = 0f)
        {
            return Quad(name, GetSquare(), center, size, color, depth, order, angle);
        }

        private SpriteRenderer Quad(string name, Sprite sprite, Vector2 center, Vector2 size, Color color, float depth, int order, float angle = 0f)
        {
            var item = new GameObject(name);
            item.transform.SetParent(transform, false);
            item.transform.localPosition = new Vector3(center.x, center.y, depth);
            item.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            item.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite GetSquare()
        {
            if (square == null)
            {
                square = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            }

            return square;
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
