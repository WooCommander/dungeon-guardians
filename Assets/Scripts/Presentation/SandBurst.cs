using System.Collections.Generic;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // A trickle of sand grains falling from a stone figure: tiny sprites that drop, drift a little and fade out,
    // then the whole burst removes itself. Purely decorative.
    public sealed class SandBurst : MonoBehaviour
    {
        private const int GrainCount = 40;
        private const float Gravity = -4.5f;
        private const float MinSize = 0.022f;
        private const float MaxSize = 0.042f;
        private const float MinLife = 0.6f;
        private const float MaxLife = 1.3f;
        // Released over this long, so it trickles rather than bursts.
        private const float Spread = 0.5f;
        private static readonly Color SandColor = new Color(0.86f, 0.72f, 0.48f);

        private static Sprite grainSprite;

        private sealed class Grain
        {
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float Delay;
            public float Age;
            public float Life;
        }

        private readonly List<Grain> grains = new List<Grain>();
        private float floor;

        // feet: the figure's bottom centre; grains come from all over its body and stop at its feet.
        public static SandBurst Spawn(Transform parent, Vector3 feet, float height)
        {
            var root = new GameObject("Sand");
            root.transform.SetParent(parent, false);
            var burst = root.AddComponent<SandBurst>();
            burst.floor = feet.y;

            for (int i = 0; i < GrainCount; i++)
            {
                var grainObject = new GameObject("Grain");
                grainObject.transform.SetParent(root.transform, false);
                // In front of the figure, from its body (narrower towards the head).
                float up = Random.Range(0.1f, 0.95f);
                float across = Random.Range(-0.2f, 0.2f) * height * (1.1f - 0.5f * up);
                grainObject.transform.position = feet + new Vector3(across, up * height, -0.3f);
                grainObject.transform.localScale = Vector3.one * Random.Range(MinSize, MaxSize);
                var renderer = grainObject.AddComponent<SpriteRenderer>();
                renderer.sprite = GetGrainSprite();
                renderer.color = SandColor * Random.Range(0.85f, 1.1f);
                renderer.enabled = false;
                burst.grains.Add(new Grain
                {
                    Renderer = renderer,
                    Velocity = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.1f, 0.35f), 0f),
                    Delay = Random.Range(0f, Spread),
                    Life = Random.Range(MinLife, MaxLife)
                });
            }

            return burst;
        }

        private void Update()
        {
            bool alive = false;
            float delta = Time.deltaTime;
            foreach (Grain grain in grains)
            {
                if (grain.Delay > 0f)
                {
                    grain.Delay -= delta;
                    alive = true;
                    continue;
                }

                grain.Age += delta;
                if (grain.Age >= grain.Life)
                {
                    grain.Renderer.enabled = false;
                    continue;
                }

                alive = true;
                grain.Renderer.enabled = true;
                grain.Velocity.y += Gravity * delta;
                Transform grainTransform = grain.Renderer.transform;
                Vector3 position = grainTransform.position + grain.Velocity * delta;
                if (position.y < floor)
                {
                    // Settles on the floor and fades there.
                    position.y = floor;
                    grain.Velocity = Vector3.zero;
                }

                grainTransform.position = position;
                Color color = grain.Renderer.color;
                color.a = 1f - Mathf.Pow(grain.Age / grain.Life, 2f);
                grain.Renderer.color = color;
            }

            if (!alive)
            {
                Destroy(gameObject);
            }
        }

        // A small soft dot.
        private static Sprite GetGrainSprite()
        {
            if (grainSprite != null)
            {
                return grainSprite;
            }

            const int size = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Sand Grain" };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - distance) * 2.5f)));
                }
            }

            texture.Apply();
            grainSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return grainSprite;
        }
    }
}
