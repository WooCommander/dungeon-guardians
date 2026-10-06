using System.Collections.Generic;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // The veil of a dark hall (Resources/Shaders/Darkness): it covers the camera's view in front of the level and is
    // cleared around the helmet lamp and the torches, which flicker with their flames. What must still be noticed in
    // the dark is drawn in front of it: faint glints where gold lies and the guardians' glowing eyes.
    public sealed class DarknessOverlay : MonoBehaviour
    {
        private const int MaxLights = 24;
        // In front of every actor and statue, behind the HUD (which is an overlay canvas anyway).
        private const float Depth = -3f;
        private const float GlintDepth = -3.2f;
        private const float LampRadius = 3.4f;
        private const float TorchRadius = 2.4f;
        private static readonly Color GlintColor = new Color(1f, 0.85f, 0.4f, 0.5f);
        private static readonly Color EyeColor = new Color(0.4f, 1f, 1f, 0.9f);

        private static Sprite square;
        // Glints and eyes must be drawn after the veil (queue Transparent+100), so they get their own sprite material.
        private static Material aboveVeil;

        private Material material;
        private Transform veil;
        private readonly Vector4[] lights = new Vector4[MaxLights];
        private readonly List<SpriteRenderer> eyes = new List<SpriteRenderer>();

        public static DarknessOverlay Create(Transform parent)
        {
            var shader = Resources.Load<Shader>("Shaders/Darkness");
            if (shader == null)
            {
                Debug.LogWarning("Resources/Shaders/Darkness not found; the dark hall is shown lit.");
                return null;
            }

            var root = new GameObject("Darkness");
            root.transform.SetParent(parent, false);
            var overlay = root.AddComponent<DarknessOverlay>();
            overlay.material = new Material(shader) { name = "Darkness" };

            var veilObject = new GameObject("Veil");
            veilObject.transform.SetParent(root.transform, false);
            var renderer = veilObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquare();
            renderer.sharedMaterial = overlay.material;
            overlay.veil = veilObject.transform;
            return overlay;
        }

        // Called every frame with the camera, the lamp position (or null when there is no explorer) and the torch
        // flames; guardianHeads are the points between each visible guardian's eyes, with the way it faces
        // (-1 left, 1 right, 0 towards the camera; null entries are hidden).
        public void UpdateView(Camera camera, Vector3? lamp, IReadOnlyList<TorchFlame> torches, IReadOnlyList<(Vector3 head, int facing)?> guardianHeads)
        {
            float height = camera.orthographicSize * 2f;
            float width = height * camera.aspect;
            Vector3 centre = camera.transform.position;
            veil.position = new Vector3(centre.x, centre.y, Depth);
            veil.localScale = new Vector3(width + 1f, height + 1f, 1f);

            int count = 0;
            if (lamp.HasValue)
            {
                lights[count++] = new Vector4(lamp.Value.x, lamp.Value.y, LampRadius, 1f);
            }

            foreach (TorchFlame torch in torches)
            {
                if (count >= MaxLights)
                {
                    break;
                }

                if (torch == null)
                {
                    continue;
                }

                Vector3 position = torch.transform.position;
                // Only torches near the view matter.
                if (Mathf.Abs(position.x - centre.x) > width * 0.5f + TorchRadius || Mathf.Abs(position.y - centre.y) > height * 0.5f + TorchRadius)
                {
                    continue;
                }

                float flicker = 0.9f + 0.1f * Mathf.PerlinNoise(position.x * 3.1f, Time.time * 5f);
                lights[count++] = new Vector4(position.x, position.y, TorchRadius * flicker, 1f);
            }

            material.SetVectorArray("_Lights", lights);
            material.SetInt("_LightCount", count);
            UpdateEyes(guardianHeads);
        }

        // A faint twinkle in front of the veil, so gold can be found in the dark; it is a child of the gold bar.
        public static void AddGlint(GameObject gold)
        {
            var glint = new GameObject("Dark Glint");
            glint.transform.SetParent(gold.transform, false);
            Vector3 scale = gold.transform.lossyScale;
            glint.transform.localScale = new Vector3(0.35f / scale.x, 0.35f / scale.y, 1f);
            var renderer = glint.AddComponent<SpriteRenderer>();
            renderer.sprite = ExitGlow.GetHaloSprite();
            renderer.color = GlintColor;
            renderer.sharedMaterial = AboveVeil();
            glint.AddComponent<Twinkle>();
            // In front of the veil whatever the bar's own depth.
            glint.transform.position = new Vector3(glint.transform.position.x, glint.transform.position.y, GlintDepth);
        }

        private void UpdateEyes(IReadOnlyList<(Vector3 head, int facing)?> heads)
        {
            while (eyes.Count < heads.Count * 2)
            {
                var eye = new GameObject("Eye").AddComponent<SpriteRenderer>();
                eye.transform.SetParent(transform, false);
                eye.sprite = ExitGlow.GetHaloSprite();
                eye.color = EyeColor;
                eye.sharedMaterial = AboveVeil();
                eye.transform.localScale = Vector3.one * 0.13f;
                eyes.Add(eye);
            }

            for (int i = 0; i < eyes.Count; i++)
            {
                int guardian = i / 2;
                bool visible = guardian < heads.Count && heads[guardian].HasValue;
                eyes[i].enabled = visible;
                if (!visible)
                {
                    continue;
                }

                (Vector3 head, int facing) = heads[guardian].Value;
                // Facing the camera the eyes sit side by side; in profile they crowd towards the face.
                float spread = facing == 0 ? 0.09f : 0.05f;
                float forward = facing * 0.1f;
                float side = i % 2 == 0 ? -1f : 1f;
                eyes[i].transform.position = new Vector3(head.x + forward + side * spread, head.y, GlintDepth);
            }
        }

        private static Material AboveVeil()
        {
            if (aboveVeil == null)
            {
                aboveVeil = new Material(Shader.Find("Sprites/Default")) { name = "Above Darkness", renderQueue = 3200 };
            }

            return aboveVeil;
        }

        private static Sprite GetSquare()
        {
            if (square == null)
            {
                square = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            }

            return square;
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        // Gold glints come and go, each at its own pace.
        private sealed class Twinkle : MonoBehaviour
        {
            private SpriteRenderer glint;
            private float seed;

            private void Awake()
            {
                glint = GetComponent<SpriteRenderer>();
                seed = Random.value * 50f;
            }

            private void Update()
            {
                float level = Mathf.Pow(Mathf.PerlinNoise(seed, Time.time * 1.7f), 2f);
                glint.color = new Color(GlintColor.r, GlintColor.g, GlintColor.b, GlintColor.a * (0.25f + level));
            }
        }
    }
}
