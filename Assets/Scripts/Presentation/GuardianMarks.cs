using DungeonGuardians.Core;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // How each kind of guardian is told apart (they share one golem model): a tint, a size, and for the infected
    // a red glow from its cracks that flares just before it lunges.
    public static class GuardianMarks
    {
        public static Color Tint(GuardianKind kind)
        {
            switch (kind)
            {
                case GuardianKind.Warden:
                    return new Color(0.78f, 0.88f, 1f);
                case GuardianKind.Listener:
                    return new Color(0.78f, 1f, 0.8f);
                case GuardianKind.Heavy:
                    return new Color(0.62f, 0.56f, 0.52f);
                case GuardianKind.Infected:
                    return new Color(1f, 0.72f, 0.68f);
                default:
                    return Color.white;
            }
        }

        public static float HeightScale(GuardianKind kind)
        {
            return kind == GuardianKind.Heavy ? 1.3f : 1f;
        }

        // The view must keep up with the fastest move of its kind.
        public static float SpeedScale(GuardianKind kind)
        {
            return kind == GuardianKind.Infected ? 2.8f : 1f;
        }
    }

    // The infected guardian's cracks: a dull red glow that pulses slowly, flaring bright before a lunge.
    public sealed class InfectedGlow : MonoBehaviour
    {
        private static readonly Color GlowColor = new Color(1f, 0.15f, 0.08f, 0.55f);

        private SpriteRenderer glow;
        private Light glowLight;
        private bool flaring;

        public static InfectedGlow Attach(CharacterView view)
        {
            var root = new GameObject("Infected Glow");
            root.transform.SetParent(view.transform, false);
            root.transform.localPosition = new Vector3(0f, view.Height * 0.55f, -0.25f);
            var marks = root.AddComponent<InfectedGlow>();

            var glowObject = new GameObject("Glow");
            glowObject.transform.SetParent(root.transform, false);
            marks.glow = glowObject.AddComponent<SpriteRenderer>();
            marks.glow.sprite = ExitGlow.GetHaloSprite();

            marks.glowLight = root.AddComponent<Light>();
            marks.glowLight.type = LightType.Point;
            marks.glowLight.color = new Color(1f, 0.2f, 0.1f);
            marks.glowLight.range = 2f;
            return marks;
        }

        public void SetFlaring(bool value)
        {
            flaring = value;
        }

        private void LateUpdate()
        {
            // Face the camera whatever way the guardian turns.
            transform.rotation = Quaternion.identity;
            float pulse = flaring
                ? 0.75f + 0.25f * Mathf.Sin(Time.time * 40f)
                : 0.25f + 0.15f * Mathf.Sin(Time.time * 2.5f);
            glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, GlowColor.a * pulse * (flaring ? 1.6f : 1f));
            glow.transform.localScale = Vector3.one * (flaring ? 1.5f : 1f) / Mathf.Max(0.001f, transform.lossyScale.x);
            glowLight.intensity = flaring ? 3f * pulse : 0.6f * pulse;
        }
    }

    // A ring spreading from where the explorer made a noise, in levels with listeners: the player sees what the
    // listeners hear.
    public sealed class NoiseRipple : MonoBehaviour
    {
        private const float Duration = 0.9f;
        private const float MaxSize = 3.2f;
        private static readonly Color RingColor = new Color(0.75f, 1f, 0.8f, 0.6f);
        private static Sprite ring;

        private SpriteRenderer sprite;
        private float age;

        public static void Spawn(Transform parent, Vector3 centre)
        {
            var root = new GameObject("Noise");
            root.transform.SetParent(parent, false);
            root.transform.position = centre;
            var ripple = root.AddComponent<NoiseRipple>();
            ripple.sprite = root.AddComponent<SpriteRenderer>();
            ripple.sprite.sprite = GetRing();
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = age / Duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            transform.localScale = Vector3.one * Mathf.Lerp(0.4f, MaxSize, t);
            sprite.color = new Color(RingColor.r, RingColor.g, RingColor.b, RingColor.a * (1f - t));
        }

        private static Sprite GetRing()
        {
            if (ring != null)
            {
                return ring;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Noise Ring" };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float band = 1f - Mathf.Abs(distance - 0.85f) / 0.1f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(band)));
                }
            }

            texture.Apply();
            ring = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return ring;
        }
    }
}
