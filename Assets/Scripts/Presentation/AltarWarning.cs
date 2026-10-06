using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // Shown at an altar shortly before a guardian returns there (TZ section 7): a cold cyan glow pulsing faster and
    // brighter as the moment comes, and lingering while the explorer stands too close and holds the guardian back.
    public sealed class AltarWarning : MonoBehaviour
    {
        private const float Size = 1.6f;
        private static readonly Color GlowColor = new Color(0.35f, 1f, 1f, 0.75f);

        private SpriteRenderer glow;
        private Light glowLight;

        public static AltarWarning Create(Transform parent)
        {
            var root = new GameObject("Altar Warning");
            root.transform.SetParent(parent, false);
            var warning = root.AddComponent<AltarWarning>();

            var glowObject = new GameObject("Glow");
            glowObject.transform.SetParent(root.transform, false);
            glowObject.transform.localScale = Vector3.one * Size;
            warning.glow = glowObject.AddComponent<SpriteRenderer>();
            warning.glow.sprite = ExitGlow.GetHaloSprite();

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0f, -0.8f);
            warning.glowLight = lightObject.AddComponent<Light>();
            warning.glowLight.type = LightType.Point;
            warning.glowLight.color = new Color(0.4f, 1f, 1f);
            warning.glowLight.range = 2.5f;

            root.SetActive(false);
            return warning;
        }

        // centre: the middle of the altar cell, in the level's local space.
        public void Show(Vector3 centre)
        {
            transform.localPosition = centre;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
            glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, GlowColor.a * (0.45f + 0.55f * pulse));
            glow.transform.localScale = Vector3.one * Size * (0.85f + 0.25f * pulse);
            glowLight.intensity = 1f + 1.5f * pulse;
        }
    }
}
