using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // A burning torch flame: two soft teardrop sprites (orange outside, yellow core) that sway and flicker,
    // plus an optional point light flickering in step. Purely decorative; it never affects gameplay.
    public sealed class TorchFlame : MonoBehaviour
    {
        private const float FlickerSpeed = 6f;
        private const float SwaySpeed = 2.3f;

        private static Sprite flameSprite;

        private Transform outer;
        private Transform core;
        private Light flameLight;
        private float baseIntensity;
        private float seed;

        public static TorchFlame Create(Transform parent, Vector3 localPosition, float size, bool withLight)
        {
            var root = new GameObject("Flame");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            var flame = root.AddComponent<TorchFlame>();
            flame.seed = Random.Range(0f, 100f);
            flame.outer = flame.AddLayer("Outer", new Color(1f, 0.45f, 0.08f, 0.85f), size, 0f);
            flame.core = flame.AddLayer("Core", new Color(1f, 0.88f, 0.45f, 0.95f), size * 0.55f, -0.01f);

            if (withLight)
            {
                var lightObject = new GameObject("Light");
                lightObject.transform.SetParent(root.transform, false);
                // Slightly in front of the wall so the light reaches the gameplay plane.
                lightObject.transform.localPosition = new Vector3(0f, 0.1f, -0.6f);
                flame.flameLight = lightObject.AddComponent<Light>();
                flame.flameLight.type = LightType.Point;
                flame.flameLight.color = new Color(1f, 0.62f, 0.3f);
                flame.flameLight.range = 3.5f;
                flame.flameLight.intensity = flame.baseIntensity = 1.4f;
            }

            return flame;
        }

        private void Update()
        {
            float time = Time.time;
            float flicker = Mathf.PerlinNoise(seed, time * FlickerSpeed);
            float sway = (Mathf.PerlinNoise(seed + 37f, time * SwaySpeed) - 0.5f) * 2f;

            outer.localScale = new Vector3(1f - 0.12f * flicker, 0.85f + 0.35f * flicker, 1f);
            outer.localRotation = Quaternion.Euler(0f, 0f, sway * 8f);
            core.localScale = new Vector3(1f, 0.8f + 0.4f * Mathf.PerlinNoise(seed + 11f, time * FlickerSpeed * 1.3f), 1f);
            core.localRotation = Quaternion.Euler(0f, 0f, sway * 5f);

            if (flameLight != null)
            {
                flameLight.intensity = baseIntensity * (0.75f + 0.5f * flicker);
            }
        }

        private Transform AddLayer(string name, Color color, float size, float depth)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(transform, false);
            layer.transform.localPosition = new Vector3(0f, 0f, depth);

            // The sprite pivot is at the flame's base, so stretching grows the tongue upwards.
            var sprite = new GameObject("Sprite");
            sprite.transform.SetParent(layer.transform, false);
            sprite.transform.localScale = Vector3.one * size;
            var renderer = sprite.AddComponent<SpriteRenderer>();
            renderer.sprite = GetFlameSprite();
            renderer.color = color;
            return layer.transform;
        }

        // A 32x64 teardrop with soft edges: round at the bottom, tapering to a point at the top.
        private static Sprite GetFlameSprite()
        {
            if (flameSprite != null)
            {
                return flameSprite;
            }

            const int width = 32;
            const int height = 64;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                name = "Flame"
            };

            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                // Widest a quarter of the way up, narrowing to nothing at the tip.
                float halfWidth = v < 0.25f ? Mathf.Sqrt(v / 0.25f) : Mathf.Pow(1f - (v - 0.25f) / 0.75f, 1.4f);
                for (int x = 0; x < width; x++)
                {
                    float u = Mathf.Abs(x / (width - 1f) * 2f - 1f);
                    float edge = halfWidth <= 0f ? 0f : Mathf.Clamp01(1f - u / halfWidth);
                    float alpha = Mathf.SmoothStep(0f, 1f, edge) * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, v * 6f));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            flameSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), height);
            return flameSprite;
        }
    }
}
