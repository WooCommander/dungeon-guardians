using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    // The start screen: the painted title picture (map-images/menu.png) with its three buttons made pressable,
    // plus the level select and settings panels that open over it. Pieces come from tools/cut_menu.py.
    public sealed class GameMenu : MonoBehaviour
    {
        // Size of the source picture; the buttons are placed by their pixel boxes in it (see tools/cut_menu.py).
        private static readonly Vector2 PictureSize = new Vector2(1672f, 941f);
        private static readonly Color OverlayColor = new Color(0.01f, 0.02f, 0.03f, 0.78f);
        private static readonly Color TitleColor = new Color(1f, 0.8f, 0.4f);

        private Canvas canvas;
        private GameObject levelsPanel;
        private GameObject settingsPanel;
        private Transform levelGrid;
        private Text controlsLabel;

        private int levelCount;
        private Func<int, bool> isUnlocked;
        private Func<int, bool> isCompleted;

        public event Action Play;
        public event Action<int> PlayLevel;
        public event Action SettingsChanged;

        public void Initialize(int levelCount, Func<int, bool> isUnlocked, Func<int, bool> isCompleted)
        {
            this.levelCount = levelCount;
            this.isUnlocked = isUnlocked;
            this.isCompleted = isCompleted;
            Build();
        }

        public void Show()
        {
            canvas.gameObject.SetActive(true);
            levelsPanel.SetActive(false);
            settingsPanel.SetActive(false);
        }

        public void Hide()
        {
            canvas.gameObject.SetActive(false);
        }

        private void Build()
        {
            canvas = MenuStyle.CreateCanvas("Menu", transform, 10);

            // The picture covers the screen keeping its proportions; the buttons are its children, so they stay on
            // their painted places whatever the screen shape.
            var picture = new GameObject("Picture").AddComponent<Image>();
            picture.transform.SetParent(canvas.transform, false);
            picture.sprite = Resources.Load<Sprite>("Backgrounds/menu");
            picture.color = picture.sprite != null ? Color.white : new Color(0.05f, 0.1f, 0.12f);
            picture.raycastTarget = false;
            var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = PictureSize.x / PictureSize.y;

            AddPictureButton(picture.transform, "menu_play", new Rect(619f, 467f, 434f, 124f), "ИГРАТЬ", () => Play?.Invoke());
            AddPictureButton(picture.transform, "menu_levels", new Rect(666f, 618f, 340f, 76f), "ВЫБОР УРОВНЯ", OpenLevels);
            AddPictureButton(picture.transform, "menu_settings", new Rect(675f, 715f, 324f, 69f), "НАСТРОЙКИ", OpenSettings);

            levelsPanel = BuildPanel("ВЫБОР УРОВНЯ", out Transform levelsBody);
            var grid = levelsBody.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300f, 76f);
            grid.spacing = new Vector2(24f, 20f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;
            levelGrid = levelsBody;

            settingsPanel = BuildPanel("НАСТРОЙКИ", out Transform settingsBody);
            var column = settingsBody.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = false;
            column.childControlHeight = false;
            Button controls = MenuStyle.CreatePlateButton(settingsBody, string.Empty, new Vector2(560f, 76f), CycleControls);
            controlsLabel = controls.GetComponentInChildren<Text>();
            UpdateControlsLabel();

            Show();
        }

        // A button over its painted place: unpressed it looks exactly like the picture, pressed it darkens.
        private static void AddPictureButton(Transform picture, string sprite, Rect box, string fallbackLabel, Action onClick)
        {
            Sprite art = Resources.Load<Sprite>("UI/" + sprite);
            var image = new GameObject(sprite).AddComponent<Image>();
            image.transform.SetParent(picture, false);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(box.xMin / PictureSize.x, 1f - box.yMax / PictureSize.y);
            rect.anchorMax = new Vector2(box.xMax / PictureSize.x, 1f - box.yMin / PictureSize.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            if (art != null)
            {
                image.sprite = art;
            }
            else
            {
                // Without the cut-outs the picture is missing too: draw a plain button with a caption.
                image.color = new Color(0.3f, 0.2f, 0.1f);
                MenuStyle.AddLabel(image.transform, fallbackLabel, 34);
            }

            MenuStyle.MakeButton(image, onClick);
        }

        // A dark overlay with a title, a body for the panel's content and a "back" button at the bottom.
        private GameObject BuildPanel(string title, out Transform body)
        {
            var overlay = new GameObject(title).AddComponent<Image>();
            overlay.transform.SetParent(canvas.transform, false);
            overlay.color = OverlayColor;
            MenuStyle.Stretch(overlay.rectTransform);

            Text heading = MenuStyle.AddLabel(overlay.transform, title, 54);
            heading.color = TitleColor;
            RectTransform headingRect = heading.rectTransform;
            headingRect.anchorMin = new Vector2(0f, 1f);
            headingRect.anchorMax = new Vector2(1f, 1f);
            headingRect.sizeDelta = new Vector2(0f, 100f);
            headingRect.anchoredPosition = new Vector2(0f, -110f);

            var content = new GameObject("Body", typeof(RectTransform));
            content.transform.SetParent(overlay.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(1000f, 440f);
            contentRect.anchoredPosition = new Vector2(0f, 10f);
            body = content.transform;

            Button back = MenuStyle.CreatePlateButton(overlay.transform, "НАЗАД", new Vector2(320f, 76f), Show);
            var backRect = (RectTransform)back.transform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0f);
            backRect.anchoredPosition = new Vector2(0f, 110f);

            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        private void OpenLevels()
        {
            // Rebuilt on every opening, so levels unlocked since the last time show up.
            foreach (Transform child in levelGrid)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < levelCount; i++)
            {
                int index = i;
                bool unlocked = isUnlocked(index);
                Button button = MenuStyle.CreatePlateButton(levelGrid, $"УРОВЕНЬ {index + 1:00}", new Vector2(300f, 76f), () => PlayLevel?.Invoke(index));
                button.interactable = unlocked;
                // The built-in font has no check mark: completed levels are told apart by colour.
                Text caption = button.GetComponentInChildren<Text>();
                if (!unlocked)
                {
                    caption.color = new Color(0.55f, 0.5f, 0.45f);
                }
                else if (isCompleted(index))
                {
                    caption.color = new Color(0.55f, 1f, 0.8f);
                }
            }

            levelsPanel.SetActive(true);
        }

        private void OpenSettings()
        {
            UpdateControlsLabel();
            settingsPanel.SetActive(true);
        }

        private void CycleControls()
        {
            TouchControls.Current = (TouchControls.Mode)(((int)TouchControls.Current + 1) % 3);
            UpdateControlsLabel();
            SettingsChanged?.Invoke();
        }

        private void UpdateControlsLabel()
        {
            controlsLabel.text = $"ЭКРАННЫЕ КНОПКИ: <color=#FFC23A>{TouchControls.Label(TouchControls.Current)}</color>";
        }
    }

    // Shared look of the menu and the pause panel: the stone plate cut from the start screen, warm gold captions.
    public static class MenuStyle
    {
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
