using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    // A line along curves given in picture pixels (MenuStyle.PictureSize), stretched over the picture it lies on:
    // solid, or dashed. Its edges fade softly across the width, so a wide pale copy under a narrow bright one glows.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MapPath : MaskableGraphic
    {
        private readonly List<Vector2[]> lines = new List<Vector2[]>();
        private float width = 6f;
        private float dash;
        private float gap;
        private Texture2D profile;

        public override Texture mainTexture => profile != null ? profile : base.mainTexture;

        public static MapPath Create(Transform picture, string name, Color color, float width, float softness, float dash = 0f, float gap = 0f)
        {
            var path = new GameObject(name, typeof(RectTransform)).AddComponent<MapPath>();
            path.transform.SetParent(picture, false);
            MenuStyle.Stretch(path.rectTransform);
            path.color = color;
            path.width = width;
            path.dash = dash;
            path.gap = gap;
            path.raycastTarget = false;
            path.profile = MakeProfile(softness);
            return path;
        }

        public void SetLines(IEnumerable<Vector2[]> curves)
        {
            lines.Clear();
            lines.AddRange(curves);
            SetVerticesDirty();
        }

        // Alpha across the line's width: 0 at both edges, 1 in the middle, the fade taking `softness` of each half.
        private static Texture2D MakeProfile(float softness)
        {
            const int Size = 32;
            var texture = new Texture2D(1, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int i = 0; i < Size; i++)
            {
                float edge = 1f - Mathf.Abs((i + 0.5f) / Size * 2f - 1f);
                float alpha = softness <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / softness));
                texture.SetPixel(0, i, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            return texture;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            float scale = rect.width / MenuStyle.PictureSize.x;
            foreach (Vector2[] line in lines)
            {
                var points = new List<Vector2>(line.Length);
                foreach (Vector2 p in line)
                {
                    points.Add(new Vector2(rect.xMin + p.x * scale, rect.yMax - p.y * scale));
                }

                if (dash <= 0f)
                {
                    AddStrip(vh, points, width * scale);
                    continue;
                }

                // Walk along the line, cutting it into dashes.
                float on = dash * scale, off = gap * scale;
                float phase = 0f;
                bool drawing = true;
                var piece = new List<Vector2> { points[0] };
                for (int i = 1; i < points.Count; i++)
                {
                    Vector2 a = points[i - 1], b = points[i];
                    float length = Vector2.Distance(a, b);
                    float travelled = 0f;
                    while (travelled < length)
                    {
                        float left = (drawing ? on : off) - phase;
                        float step = Mathf.Min(left, length - travelled);
                        travelled += step;
                        phase += step;
                        Vector2 here = Vector2.Lerp(a, b, travelled / length);
                        if (drawing)
                        {
                            piece.Add(here);
                        }

                        if (phase >= (drawing ? on : off) - 0.001f)
                        {
                            if (drawing)
                            {
                                AddStrip(vh, piece, width * scale);
                            }

                            drawing = !drawing;
                            phase = 0f;
                            piece = new List<Vector2> { here };
                        }
                    }
                }

                if (drawing && piece.Count > 1)
                {
                    AddStrip(vh, piece, width * scale);
                }
            }
        }

        // A strip of quads along the points, its sides offset along the averaged normals.
        private void AddStrip(VertexHelper vh, List<Vector2> points, float stripWidth)
        {
            if (points.Count < 2)
            {
                return;
            }

            float half = stripWidth * 0.5f;
            int start = vh.currentVertCount;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 before = points[Mathf.Max(i - 1, 0)], after = points[Mathf.Min(i + 1, points.Count - 1)];
                Vector2 direction = (after - before).normalized;
                var normal = new Vector2(-direction.y, direction.x);
                vh.AddVert(points[i] + normal * half, color, new Vector2(0f, 1f));
                vh.AddVert(points[i] - normal * half, color, new Vector2(0f, 0f));
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                int v = start + i * 2;
                vh.AddTriangle(v, v + 1, v + 3);
                vh.AddTriangle(v, v + 3, v + 2);
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (profile != null)
            {
                Destroy(profile);
            }
        }
    }
}
