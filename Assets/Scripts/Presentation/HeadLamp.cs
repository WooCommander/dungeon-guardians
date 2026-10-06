using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // The lamp on the explorer's helmet: a small warm glow and a short-range light that follow the head. It is lit
    // all the time; the defeat sequence makes it flicker and finally go out.
    public sealed class HeadLamp : MonoBehaviour
    {
        // On the front of the helmet, as a share of the explorer's height: up from the feet and forward of the body.
        private const float LampHeight = 0.84f;
        private const float LampForward = 0.13f;
        private const float GlowSize = 0.45f;
        private const float LightRange = 1.6f;
        private const float LightIntensity = 1.1f;
        private static readonly Color GlowColor = new Color(1f, 0.92f, 0.7f, 0.75f);

        private SpriteRenderer glow;
        private Light lampLight;
        private float level = 1f;

        // Attached to the head bone while the explorer stands in its fitted rest pose (facing away from the camera,
        // along +Z), so it then follows every turn and nod.
        public static HeadLamp Attach(CharacterView explorer)
        {
            Transform head = explorer.FindBone("Head");
            Transform parent = head != null ? head : explorer.transform;
            Vector3 world = explorer.transform.position + new Vector3(0f, explorer.Height * LampHeight, explorer.Height * LampForward);

            var root = new GameObject("Head Lamp");
            root.transform.position = world;
            root.transform.SetParent(parent, true);
            var lamp = root.AddComponent<HeadLamp>();

            var glowObject = new GameObject("Glow");
            glowObject.transform.SetParent(root.transform, false);
            lamp.glow = glowObject.AddComponent<SpriteRenderer>();
            lamp.glow.sprite = ExitGlow.GetHaloSprite();
            lamp.glow.color = GlowColor;

            lamp.lampLight = root.AddComponent<Light>();
            lamp.lampLight.type = LightType.Point;
            lamp.lampLight.color = new Color(1f, 0.9f, 0.7f);
            lamp.lampLight.range = LightRange;
            lamp.SetLevel(1f);
            return lamp;
        }

        // 1 = fully lit, 0 = out.
        public void SetLevel(float value)
        {
            level = Mathf.Clamp01(value);
            glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, GlowColor.a * level);
            lampLight.intensity = LightIntensity * level;
            lampLight.enabled = level > 0f;
        }

        private void LateUpdate()
        {
            // The glow is a flat sprite: keep it facing the camera and the same size however the head turns.
            Transform glowTransform = glow.transform;
            glowTransform.rotation = Quaternion.identity;
            Vector3 parentScale = transform.lossyScale;
            glowTransform.localScale = new Vector3(GlowSize / parentScale.x, GlowSize / parentScale.y, 1f);
        }
    }
}
