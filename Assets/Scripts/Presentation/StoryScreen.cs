using System;
using DungeonGuardians.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    // The legend of the city under the mountain, told before the first game: one paragraph per page, typed out
    // letter by letter over the darkened cavern, which slowly drifts closer. A tap (or Space / Enter) first finishes
    // the paragraph, then turns the page; "Пропустить" (or Esc) skips the rest. Shown once; "Начать заново" in the
    // settings brings it back.
    public sealed class StoryScreen : MonoBehaviour
    {
        private const string SeenKey = "story_seen";
        private const float LettersPerSecond = 45f;
        private const float DriftScale = 0.08f;
        private const float DriftSeconds = 40f;
        private static readonly Color BackgroundTint = new Color(0.42f, 0.45f, 0.48f);
        private static readonly Color TextColor = new Color(1f, 0.9f, 0.72f);

        private string[] Pages => Localization.GetStoryParagraphs();

        private Image background;
        private Text body;
        private Text pageDots;
        private Text hint;
        private Button go;
        private int page;
        private float typedAt;
        private float openedAt;
        private Action finished;

        public static bool Seen
        {
            get => PlayerPrefs.GetInt(SeenKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(SeenKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static StoryScreen Create(Transform parent)
        {
            var root = new GameObject("Story", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            MenuStyle.Stretch((RectTransform)root.transform);
            var story = root.AddComponent<StoryScreen>();
            story.Build();
            root.SetActive(false);
            return story;
        }

        public void Open(Action whenDone)
        {
            finished = whenDone;
            page = 0;
            openedAt = Time.unscaledTime;
            gameObject.SetActive(true);
            ShowPage();
        }

        private void Build()
        {
            background = MenuStyle.CreatePicture(transform, "Backgrounds/cavern", AspectRatioFitter.AspectMode.EnvelopeParent);
            background.color = background.sprite != null ? BackgroundTint : Color.black;

            // A dark band behind the text keeps it readable over the bright parts of the painting.
            var band = new GameObject("Band").AddComponent<Image>();
            band.transform.SetParent(transform, false);
            band.color = new Color(0f, 0f, 0f, 0.55f);
            band.raycastTarget = false;
            band.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            band.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            band.rectTransform.sizeDelta = new Vector2(0f, 420f);

            // The whole screen is the "next" button.
            var tapArea = new GameObject("Tap").AddComponent<Image>();
            tapArea.transform.SetParent(transform, false);
            tapArea.color = Color.clear;
            MenuStyle.Stretch(tapArea.rectTransform);
            tapArea.gameObject.AddComponent<Button>().onClick.AddListener(Advance);

            body = MenuStyle.AddLabel(transform, string.Empty, 40);
            body.color = TextColor;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.alignment = TextAnchor.MiddleCenter;
            body.lineSpacing = 1.15f;
            body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            body.rectTransform.sizeDelta = new Vector2(1180f, 380f);
            body.rectTransform.anchoredPosition = Vector2.zero;

            pageDots = MenuStyle.AddLabel(transform, string.Empty, 30);
            pageDots.supportRichText = true;
            pageDots.rectTransform.anchorMin = pageDots.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            pageDots.rectTransform.sizeDelta = new Vector2(400f, 50f);
            pageDots.rectTransform.anchoredPosition = new Vector2(0f, -250f);

            hint = MenuStyle.AddLabel(transform, Localization.T("story_hint"), 24);
            hint.color = new Color(1f, 0.9f, 0.72f, 0.6f);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            hint.rectTransform.sizeDelta = new Vector2(600f, 40f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, 70f);

            Button skip = MenuStyle.CreatePlateButton(transform, Localization.T("story_skip"), new Vector2(260f, 64f), Finish);
            var skipRect = (RectTransform)skip.transform;
            skipRect.anchorMin = skipRect.anchorMax = new Vector2(1f, 1f);
            skipRect.anchoredPosition = new Vector2(-160f, -60f);

            go = MenuStyle.CreatePlateButton(transform, Localization.T("story_go"), new Vector2(340f, 80f), Finish);
            var goRect = (RectTransform)go.transform;
            goRect.anchorMin = goRect.anchorMax = new Vector2(0.5f, 0f);
            goRect.anchoredPosition = new Vector2(0f, 110f);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            // The cavern drifts slowly closer while the story is told.
            background.rectTransform.localScale = Vector3.one * (1f + DriftScale * Mathf.Clamp01((now - openedAt) / DriftSeconds));

            string text = Pages[page];
            int shown = Mathf.Min(text.Length, Mathf.FloorToInt((now - typedAt) * LettersPerSecond));
            // The rest of the paragraph is laid out already but invisible, so lines do not jump while typing.
            body.text = shown >= text.Length ? text : text.Substring(0, shown) + "<color=#00000000>" + text.Substring(shown) + "</color>";

            bool last = page == Pages.Length - 1;
            bool done = shown >= text.Length;
            go.gameObject.SetActive(last && done);
            hint.enabled = !(last && done);
            hint.color = new Color(hint.color.r, hint.color.g, hint.color.b, done ? 0.35f + 0.3f * Mathf.Sin(now * 3f) : 0f);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    Finish();
                }
                else if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                {
                    Advance();
                }
            }
        }

        // A tap finishes the paragraph being typed, or else turns to the next page.
        private void Advance()
        {
            if ((Time.unscaledTime - typedAt) * LettersPerSecond < Pages[page].Length)
            {
                typedAt = -1000f;
                return;
            }

            if (page < Pages.Length - 1)
            {
                page++;
                ShowPage();
            }
            else
            {
                Finish();
            }
        }

        private void ShowPage()
        {
            typedAt = Time.unscaledTime;
            var dots = new System.Text.StringBuilder();
            for (int i = 0; i < Pages.Length; i++)
            {
                dots.Append(i == page ? "<color=#FFC23A>●</color>" : "<color=#6B5A48>●</color>");
                if (i < Pages.Length - 1)
                {
                    dots.Append("  ");
                }
            }

            pageDots.text = dots.ToString();
        }

        private void Finish()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            Seen = true;
            gameObject.SetActive(false);
            Action done = finished;
            finished = null;
            done?.Invoke();
        }
    }
}
