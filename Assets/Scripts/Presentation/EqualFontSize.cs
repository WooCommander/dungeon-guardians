using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    // One font size for a group of captions: the largest at which every one of them fits its own box, so a short
    // word is not written bigger than a long one beside it. Worked out again whenever a box changes size.
    public sealed class EqualFontSize : MonoBehaviour
    {
        private readonly List<Text> texts = new List<Text>();
        private readonly List<Vector2> sizes = new List<Vector2>();
        private int maxSize = 60;

        public static void Apply(GameObject host, int maxSize, params Text[] captions)
        {
            EqualFontSize group = host.GetComponent<EqualFontSize>();
            if (group == null)
            {
                group = host.AddComponent<EqualFontSize>();
            }

            group.maxSize = maxSize;
            foreach (Text text in captions)
            {
                group.texts.Add(text);
                group.sizes.Add(Vector2.zero);
            }
        }

        private void LateUpdate()
        {
            bool changed = false;
            for (int i = 0; i < texts.Count; i++)
            {
                Vector2 size = texts[i].rectTransform.rect.size;
                if (size != sizes[i])
                {
                    sizes[i] = size;
                    changed = true;
                }
            }

            if (changed)
            {
                Fit();
            }
        }

        private void Fit()
        {
            int size = maxSize;
            var generator = new TextGenerator();
            foreach (Text text in texts)
            {
                Vector2 box = text.rectTransform.rect.size;
                if (box.x <= 0f || box.y <= 0f)
                {
                    return;
                }

                TextGenerationSettings settings = text.GetGenerationSettings(box);
                settings.resizeTextForBestFit = true;
                settings.resizeTextMinSize = 1;
                settings.resizeTextMaxSize = maxSize;
                // Measured as best fit measures: within the box, shrinking until it fits.
                settings.horizontalOverflow = HorizontalWrapMode.Wrap;
                settings.generateOutOfBounds = false;
                generator.Populate(text.text, settings);
                size = Mathf.Min(size, Mathf.FloorToInt(generator.fontSizeUsedForBestFit / Mathf.Max(text.pixelsPerUnit, 0.0001f)));
            }

            foreach (Text text in texts)
            {
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.fontSize = Mathf.Max(size, 1);
            }
        }
    }
}
