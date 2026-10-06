using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    // The start screen: the painted title picture (map-images/menu.png) with its two buttons made pressable.
    // "Settings" opens SettingsScreen over it. Pieces come from tools/cut_menu.py.
    public sealed class GameMenu : MonoBehaviour
    {
        private Canvas canvas;
        private SettingsScreen settings;

        public event Action Play;
        public event Action SettingsChanged;
        public event Action ResetProgress;

        public void Initialize()
        {
            canvas = MenuStyle.CreateCanvas("Menu", transform, 10);

            // The picture covers the screen keeping its proportions; the buttons are its children, so they stay on
            // their painted places whatever the screen shape.
            Image picture = MenuStyle.CreatePicture(canvas.transform, "Backgrounds/menu", AspectRatioFitter.AspectMode.EnvelopeParent);
            MenuStyle.AddPictureButton(picture.transform, "menu_play", new Rect(619f, 467f, 434f, 124f), "ИГРАТЬ", () => Play?.Invoke());
            MenuStyle.AddPictureButton(picture.transform, "menu_settings", new Rect(659f, 617f, 351f, 79f), "НАСТРОЙКИ", OpenSettings);

            settings = SettingsScreen.Create(canvas.transform);
            settings.Changed += () => SettingsChanged?.Invoke();
            settings.ResetProgress += () => ResetProgress?.Invoke();
            Show();
        }

        public void Show()
        {
            canvas.gameObject.SetActive(true);
            settings.Close();
        }

        public void Hide()
        {
            canvas.gameObject.SetActive(false);
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
            }
            else
            {
                // Without the cut-outs the picture is missing too: draw a plain button with a caption.
                image.color = PlainPlateColor;
                AddLabel(image.transform, fallbackLabel, 34);
            }

            return MakeButton(image, onClick);
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
