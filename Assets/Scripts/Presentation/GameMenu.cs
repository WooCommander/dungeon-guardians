using System;
using DungeonGuardians.Core;
using DungeonGuardians.Persistence;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    // The start screen: the painted title picture (map-images/menu.png) with its two buttons made pressable.
    // "Settings" opens SettingsScreen over it. Pieces come from tools/cut_menu.py.
    public sealed class GameMenu : MonoBehaviour
    {
        // The painted guardians' glowing eyes (picture pixels: centre and glow size), one statue per group so both
        // eyes of a statue flicker together: the big statue on the right and the one further back.
        private static readonly (Vector2 centre, float size)[][] Eyes =
        {
            new[] { (new Vector2(1355f, 277f), 70f), (new Vector2(1442f, 241f), 70f), (new Vector2(1379f, 196f), 52f) },
            new[] { (new Vector2(1117f, 500f), 40f), (new Vector2(1153f, 491f), 40f), (new Vector2(1129f, 467f), 30f) },
        };
        private static readonly Color EyeColor = new Color(0.45f, 1f, 1f, 1f);

        private Canvas canvas;
        private SettingsScreen settings;
        private StoryScreen story;
        private LevelMapScreen levelMap;
        private PlayerProgress progress;
        private LevelCatalog catalog;
        private readonly System.Collections.Generic.List<(Image glow, int statue)> eyeGlows =
            new System.Collections.Generic.List<(Image glow, int statue)>();

        public event Action<int> PlayLevel;
        public event Action SettingsChanged;
        public event Action ResetProgress;

        public void Initialize(PlayerProgress progress, LevelCatalog catalog)
        {
            this.progress = progress;
            this.catalog = catalog;
            canvas = MenuStyle.CreateCanvas("Menu", transform, 10);

            // The picture covers the screen keeping its proportions; the buttons are its children, so they stay on
            // their painted places whatever the screen shape.
            Image picture = MenuStyle.CreatePicture(canvas.transform, "Backgrounds/menu", AspectRatioFitter.AspectMode.EnvelopeParent);
            AddEyeGlows(picture.transform);
            
            // 1. ИГРАТЬ (Main gold banner button). Its caption is written in code like the others below it.
            var captions = new System.Collections.Generic.List<Text>
            {
                MenuStyle.AddCaptionedButton(picture.transform, "menu_play_blank", new Rect(619f, 467f, 434f, 124f), Localization.T("menu_play"), MenuStyle.GoldCaption, StartGame),
            };

            // 2. НАСТРОЙКИ and 3. ВЫХОД (desktop only): the size and shape of ИГРАТЬ, unlit. Each covers the small painted
            // plate under it.
            captions.Add(MenuStyle.AddQuietButton(picture.transform, "menu_button", new Rect(619f, 615f, 434f, 124f), Localization.T("menu_settings"), OpenSettings));
            if (!Application.isMobilePlatform)
            {
                captions.Add(MenuStyle.AddQuietButton(picture.transform, "menu_button", new Rect(619f, 763f, 434f, 124f), Localization.T("menu_exit"), Application.Quit));
            }

            // One size and face for all three captions: the largest at which "НАСТРОЙКИ" still fits.
            EqualFontSize.Apply(picture.gameObject, 60, captions.ToArray());

            settings = SettingsScreen.Create(canvas.transform);
            story = StoryScreen.Create(canvas.transform);
            levelMap = LevelMapScreen.Create(canvas.transform);

            levelMap.LevelSelected += index =>
            {
                levelMap.Close();
                PlayLevel?.Invoke(index);
            };
            levelMap.BackRequested += () =>
            {
                levelMap.Close();
                Show();
            };

            settings.Changed += () => SettingsChanged?.Invoke();
            levelMap.ResetRequested += () => ResetProgress?.Invoke();
            Show();
        }

        public void OpenMap()
        {
            canvas.gameObject.SetActive(true);
            settings.Close();
            story.gameObject.SetActive(false);
            levelMap.Open(progress, catalog);
        }

        public void Show()
        {
            canvas.gameObject.SetActive(true);
            settings.Close();
            levelMap.Close();
        }

        public void Hide()
        {
            canvas.gameObject.SetActive(false);
        }

        private void AddEyeGlows(Transform picture)
        {
            for (int statue = 0; statue < Eyes.Length; statue++)
            {
                foreach ((Vector2 centre, float size) in Eyes[statue])
                {
                    var glow = new GameObject("Eye Glow").AddComponent<Image>();
                    glow.transform.SetParent(picture, false);
                    glow.sprite = ExitGlow.GetHaloSprite();
                    glow.raycastTarget = false;
                    MenuStyle.PlaceOnPicture(glow.rectTransform, new Rect(centre.x - size / 2f, centre.y - size / 2f, size, size));
                    eyeGlows.Add((glow, statue));
                }
            }
        }

        // Each statue's eyes smoulder unevenly, now and then flaring or almost dying away.
        private void Update()
        {
            if (!canvas.gameObject.activeInHierarchy)
            {
                return;
            }

            HandleBack();
            float time = Time.unscaledTime;
            foreach ((Image glow, int statue) in eyeGlows)
            {
                float slow = Mathf.PerlinNoise(statue * 13.7f, time * 1.3f);
                float fast = Mathf.PerlinNoise(statue * 5.1f + 40f, time * 9f);
                float level = Mathf.Clamp01(0.15f + 1.1f * slow * (0.7f + 0.3f * fast));
                glow.color = new Color(EyeColor.r, EyeColor.g, EyeColor.b, level * 0.85f);
                glow.rectTransform.localScale = Vector3.one * (0.85f + 0.3f * level);
            }
        }

        // Android's back button (Esc on a keyboard): closes the settings or the level map's dialog, and on the start
        // screen leaves the game.
        private void HandleBack()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || story.gameObject.activeSelf)
            {
                return;
            }

            if (levelMap.gameObject.activeSelf)
            {
                if (levelMap.HideDialog())
                {
                    return;
                }

                levelMap.Close();
                Show();
                return;
            }

            if (settings.gameObject.activeSelf)
            {
                settings.Close();
            }
            else if (Application.platform == RuntimePlatform.Android)
            {
                Application.Quit();
            }
        }

        // The first game opens with the legend of the city; after that "Play" opens the Seekers Path level map.
        private void StartGame()
        {
            if (StoryScreen.Seen)
            {
                OpenMap();
                return;
            }

            story.Open(OpenMap);
        }

        private void OpenSettings()
        {
            settings.Open();
        }
    }

    // Shared look of the menu, the settings screen and the pause panel: painted pictures with pressable pieces laid over
    // them, the stone plate cut from the start screen, warm gold captions.
    public static class MenuStyle
    {
        // Size of the painted screens (map-images/menu.png, settings.png); pieces are placed by their pixel boxes.
        public static readonly Vector2 PictureSize = new Vector2(1672f, 941f);

        // The plate's ends with the diamonds stay as they are; only the middle stretches (tools/cut_menu.py).
        private const float PlateEnd = 60f;
        private static readonly Color CaptionColor = new Color(1f, 0.87f, 0.58f);
        private static readonly Color PlainPlateColor = new Color(0.22f, 0.16f, 0.11f, 0.95f);

        private static Font font;
        private static Sprite plate;
        private static bool plateLoaded;

        // Unity 6 removed the built-in Arial.ttf; LegacyRuntime.ttf is its replacement.
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            // Buttons only receive clicks and taps through an EventSystem; the start screen is the first canvas built.
            EnsureEventSystem();
            var canvasObject = new GameObject(name);
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        // A full-screen painted picture keeping its proportions: covering the screen (EnvelopeParent) or shown whole
        // (FitInParent). Anything placed on it with PlaceOnPicture follows it.
        public static Image CreatePicture(Transform parent, string resource, AspectRatioFitter.AspectMode mode)
        {
            var picture = new GameObject(resource).AddComponent<Image>();
            picture.transform.SetParent(parent, false);
            picture.sprite = Resources.Load<Sprite>(resource);
            picture.color = picture.sprite != null ? Color.white : new Color(0.05f, 0.1f, 0.12f);
            picture.raycastTarget = false;
            var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = mode;
            fitter.aspectRatio = PictureSize.x / PictureSize.y;
            return picture;
        }

        // Box in picture pixels, measured from the top-left corner as in an image editor.
        public static void PlaceOnPicture(RectTransform rect, Rect box)
        {
            rect.anchorMin = new Vector2(box.xMin / PictureSize.x, 1f - box.yMax / PictureSize.y);
            rect.anchorMax = new Vector2(box.xMax / PictureSize.x, 1f - box.yMin / PictureSize.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // A button over its painted place: unpressed it looks exactly like the picture, pressed it darkens.
        public static Button AddPictureButton(Transform picture, string sprite, Rect box, string fallbackLabel, Action onClick)
        {
            Sprite art = Resources.Load<Sprite>("UI/" + sprite);
            var image = new GameObject(sprite).AddComponent<Image>();
            image.transform.SetParent(picture, false);
            PlaceOnPicture(image.rectTransform, box);
            if (art != null)
            {
                image.sprite = art;
                // A plate with fixed ends (a border in its .meta) stretches in the middle only.
                if (art.border != Vector4.zero)
                {
                    image.type = Image.Type.Sliced;
                }
            }
            else
            {
                // Without the cut-outs the picture is missing too: draw a plain button with a caption.
                image.color = PlainPlateColor;
                AddLabel(image.transform, fallbackLabel, 34);
            }

            return MakeButton(image, onClick);
        }

        public static readonly Color QuietCaption = new Color(1f, 0.88f, 0.63f);
        public static readonly Color GoldCaption = new Color(0.2f, 0.1f, 0.03f);

        // A picture button whose caption is written in code (the plate itself is blank: tools/unlit_buttons.cs) in
        // the serif of the painted captions, between the diamonds at its ends. Returns the caption.
        public static Text AddCaptionedButton(Transform picture, string sprite, Rect box, string caption, Color color, Action onClick)
        {
            Button button = AddPictureButton(picture, sprite, box, string.Empty, onClick);
            Text label = AddLabel(button.transform, caption, 60);
            Font serif = Resources.Load<Font>("Fonts/PTSerif-Bold");
            if (serif != null)
            {
                label.font = serif;
                label.fontStyle = FontStyle.Normal;
            }
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 8;
            label.resizeTextMaxSize = 60;
            label.rectTransform.anchorMin = new Vector2(0.19f, 0.25f);
            label.rectTransform.anchorMax = new Vector2(0.81f, 0.75f);
            // Dark letters on gold get a faint light rim; light letters on bronze keep the dark one.
            if (color.grayscale < 0.5f)
            {
                label.GetComponent<Outline>().effectColor = new Color(1f, 0.93f, 0.7f, 0.35f);
            }
            return label;
        }

        // The unlit twin of a golden button, with a light caption.
        public static Text AddQuietButton(Transform picture, string sprite, Rect box, string caption, Action onClick)
        {
            return AddCaptionedButton(picture, sprite, box, caption, QuietCaption, onClick);
        }

        public static Button CreatePlateButton(Transform parent, string label, Vector2 size, Action onClick)
        {
            var image = new GameObject(string.IsNullOrEmpty(label) ? "Button" : label).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.rectTransform.sizeDelta = size;
            Sprite sprite = GetPlate();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
            else
            {
                image.color = PlainPlateColor;
            }

            AddLabel(image.transform, label, 30);
            return MakeButton(image, onClick);
        }

        public static Button CreatePlateButtonOnPicture(Transform picture, string label, Rect box, Action onClick)
        {
            var image = new GameObject(string.IsNullOrEmpty(label) ? "PlateButton" : label).AddComponent<Image>();
            image.transform.SetParent(picture, false);
            PlaceOnPicture(image.rectTransform, box);
            Sprite sprite = GetPlate();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
            else
            {
                image.color = PlainPlateColor;
            }

            AddLabel(image.transform, label, 28);
            return MakeButton(image, onClick);
        }

        public static Button MakeButton(Image image, Action onClick)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            return button;
        }

        public static Text AddLabel(Transform parent, string value, int fontSize)
        {
            var text = new GameObject("Label").AddComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = Font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = CaptionColor;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.06f, 0.02f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            Stretch(text.rectTransform);
            return text;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Sprite GetPlate()
        {
            if (!plateLoaded)
            {
                plateLoaded = true;
                Sprite source = Resources.Load<Sprite>("UI/menu_plate");
                if (source != null)
                {
                    plate = Sprite.Create(source.texture, source.rect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit, 0,
                        SpriteMeshType.FullRect, new Vector4(PlateEnd, 0f, PlateEnd, 0f));
                }
            }

            return plate;
        }
    }
}
