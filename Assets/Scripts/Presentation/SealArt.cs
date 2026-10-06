using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // Sprites drawn in code, generated once: the seal trial's sandstone pressure plate with a golden rune and its iron
    // portcullis with a golden seal, and the worn planks of a fragile floor.
    public static class SealArt
    {
        private static Sprite plate;
        private static Sprite gate;
        private static Sprite planks;

        // 64 x 24: three worn boards on two cross beams, with cracks: a floor that will not hold much.
        public static Sprite Planks()
        {
            if (planks != null)
            {
                return planks;
            }

            const int width = 64;
            const int height = 24;
            var texture = NewTexture(width, height, "Fragile Planks");
            var wood = new Color(0.52f, 0.34f, 0.18f);
            var dark = new Color(0.22f, 0.13f, 0.07f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool seam = x % 21 == 0 || y == 0 || y == height - 1;
                    bool beam = (x >= 6 && x <= 10) || (x >= 53 && x <= 57);
                    float grain = 0.85f + 0.15f * Mathf.Sin(y * 1.7f + Mathf.Sin(x * 0.3f) * 2f);
                    Color color = seam ? dark : wood * grain * (beam ? 0.8f : 1f);
                    // A few dark cracks.
                    if (Mathf.Abs(y - 12 - 6f * Mathf.Sin(x * 0.25f)) < 0.6f && x > 24 && x < 44)
                    {
                        color = dark;
                    }

                    color.a = 1f;
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            planks = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), width);
            return planks;
        }

        // 64 x 12: a flat slab, darker at its edges, a golden rune line across its face.
        public static Sprite Plate()
        {
            if (plate != null)
            {
                return plate;
            }

            const int width = 64;
            const int height = 12;
            var texture = NewTexture(width, height, "Pressure Plate");
            var stone = new Color(0.55f, 0.43f, 0.3f);
            var rune = new Color(1f, 0.82f, 0.35f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool edge = x < 2 || x >= width - 2 || y < 2 || y >= height - 2;
                    Color color = edge ? stone * 0.6f : stone * (0.9f + 0.1f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f));
                    // The rune: a band of diamonds along the slab.
                    float diamond = Mathf.Abs((x % 12) - 6f) + Mathf.Abs(y - height / 2f);
                    if (!edge && diamond < 3.2f && diamond > 1.8f)
                    {
                        color = rune;
                    }

                    color.a = 1f;
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            plate = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), width);
            return plate;
        }

        // 64 x 80: iron bars with cross braces and a golden seal ring in the middle; transparent between the bars.
        public static Sprite Gate()
        {
            if (gate != null)
            {
                return gate;
            }

            const int width = 64;
            const int height = 80;
            var texture = NewTexture(width, height, "Gate");
            var iron = new Color(0.22f, 0.2f, 0.19f);
            var ironLight = new Color(0.38f, 0.34f, 0.3f);
            var gold = new Color(1f, 0.78f, 0.3f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color = Color.clear;
                    int barX = x % 13;
                    bool bar = barX >= 4 && barX <= 8;
                    bool brace = y % 26 >= 2 && y % 26 <= 6;
                    if (bar || brace)
                    {
                        // Rounded bars: lighter along their middle.
                        float shade = bar ? 1f - Mathf.Abs(barX - 6f) / 3f : 0.6f;
                        color = Color.Lerp(iron, ironLight, shade);
                    }

                    // Spikes at the bottom of each bar.
                    if (y < 6 && bar && Mathf.Abs(barX - 6f) > y * 0.5f)
                    {
                        color = Color.clear;
                    }

                    float ring = Vector2.Distance(new Vector2(x, y), new Vector2(width / 2f, height / 2f));
                    if (ring < 11f && ring > 6f)
                    {
                        color = gold * (0.85f + 0.15f * Mathf.Sin(x * 0.8f));
                        color.a = 1f;
                    }

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            gate = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), width);
            return gate;
        }

        private static Texture2D NewTexture(int width, int height, string name)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = name
            };
        }
    }
}
