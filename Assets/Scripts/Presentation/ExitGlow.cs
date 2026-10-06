using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // The open exit glows once all gold is collected: a warm halo behind the door and a golden light, both pulsing
    // gently so the way out catches the eye. Purely decorative.
    public sealed class ExitGlow : MonoBehaviour
    {
        private const float PulseSpeed = 2.4f;
        private static readonly Color HaloColor = new Color(1f, 0.78f, 0.32f, 0.75f);

        private static Sprite haloSprite;

        private SpriteRenderer halo;
        private Light glowLight;
        private Vector3 haloScale;

        public static ExitGlow Create(Transform parent, Vector3 localCenter, float size)
        {
            var root = new GameObject("Exit Glow");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localCenter;
            var glow = root.AddComponent<ExitGlow>();

            var haloObject = new GameObject("Halo");
            haloObject.transform.SetParent(root.transform, false);
            glow.halo = haloObject.AddComponent<SpriteRenderer>();
            glow.halo.sprite = GetHaloSprite();
            glow.halo.color = HaloColor;
            glow.haloScale = Vector3.one * size;
            haloObject.transform.localScale = glow.haloScale;

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(root.transform, false);
            // In front of the wall so the light reaches the door, the blocks around it and the explorer.
            lightObject.transform.localPosition = new Vector3(0f, 0f, -1.2f);
            glow.glowLight = lightObject.AddComponent<Light>();
            glow.glowLight.type = LightType.Point;
            glow.glowLight.color = new Color(1f, 0.75f, 0.35f);
            glow.glowLight.range = 4f;
            return glow;
        }

        private void Update()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed);
            halo.color = new Color(HaloColor.r, HaloColor.g, HaloColor.b, HaloColor.a * (0.7f + 0.3f * pulse));
            halo.transform.localScale = haloScale * (0.95f + 0.1f * pulse);
            glowLight.intensity = 1.6f + 0.8f * pulse;
        }

        // A soft round glow, brightest in the middle, generated once; the gold bars use it too.
        public static Sprite GetHaloSprite()
        {
            if (haloSprite != null)
            {
                return haloSprite;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Exit Halo" };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float fade = Mathf.Clamp01(1f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, fade * fade));
                }
            }

            texture.Apply();
            haloSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return haloSprite;
        }
    }
}
