using DungeonGuardians.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    public sealed class GameHud : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.08f, 0.1f, 0.13f, 0.82f);
        private static readonly Color PressedColor = new Color(0.25f, 0.62f, 0.58f, 0.92f);
        private static readonly Color DPadBackColor = new Color(0.08f, 0.1f, 0.13f, 0.35f);

        // Unity 6 removed the built-in Arial.ttf; LegacyRuntime.ttf is its replacement.
        private static Font uiFont;
        private static Font UiFont => uiFont != null ? uiFont : uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private Canvas canvas;
        private Text levelText;
        private Text goldText;
        private Text exitText;
        private Text messageText;
        private Image upArrow;
        private Image downArrow;
        private Image leftArrow;
        private Image rightArrow;
        private Image digLeftButton;
        private Image digRightButton;
        private PlayerInputBridge input;
        private string messageLabel = string.Empty;

        public void Bind(PlayerInputBridge bridge)
        {
            input = bridge;
            if (canvas == null)
            {
                Build();
            }
        }

        public void SetLevel(string title, int index, int count)
        {
            levelText.text = $"{index:00}/{count:00}  {title}";
        }

        public void SetGold(int collected, int total)
        {
            goldText.text = $"{collected} / {total}";
        }

        public void SetExit(bool open)
        {
            exitText.text = open ? "EXIT" : "LOCKED";
            exitText.color = open ? new Color(0.42f, 1f, 0.82f) : new Color(1f, 0.74f, 0.28f);
        }

        public void SetPaused(bool paused)
        {
            if (paused)
            {
                ShowMessage("PAUSE");
            }
            else if (messageLabel == "PAUSE")
            {
                ShowMessage(string.Empty);
            }
        }

        public void ShowMessage(string value)
        {
            messageLabel = value;
            messageText.text = value;
            messageText.enabled = !string.IsNullOrWhiteSpace(value);
        }

        private void LateUpdate()
        {
            if (input == null || canvas == null)
            {
                return;
            }

            var held = input.HeldDirections;
            Tint(upArrow, held.Up);
            Tint(downArrow, held.Down);
            Tint(leftArrow, held.Left);
            Tint(rightArrow, held.Right);
            Tint(digLeftButton, input.DigLeftHeld);
            Tint(digRightButton, input.DigRightHeld);
        }

        private void Build()
        {
            EnsureEventSystem();

            var canvasObject = new GameObject("HUD");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Landscape phones vary mostly in width, so scale by height to keep the controls the same physical size.
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();

            levelText = AddText("Level", new Vector2(24f, -24f), TextAnchor.UpperLeft);
            goldText = AddText("Gold", new Vector2(0f, -24f), TextAnchor.UpperCenter);
            exitText = AddText("Exit", new Vector2(-24f, -24f), TextAnchor.UpperRight);
            messageText = AddText("Message", new Vector2(0f, 0f), TextAnchor.MiddleCenter);
            messageText.fontSize = 42;
            messageText.enabled = false;

            // Touch controls are read by PlayerInputBridge per pointer, not through uGUI events.
            Image dpad = AddPanel("DPad", canvas.transform, TextAnchor.LowerLeft, new Vector2(210f, 210f), new Vector2(340f, 340f), string.Empty);
            dpad.color = DPadBackColor;
            upArrow = AddPanel("Up", dpad.transform, TextAnchor.MiddleCenter, new Vector2(0f, 112f), new Vector2(112f, 112f), "^");
            downArrow = AddPanel("Down", dpad.transform, TextAnchor.MiddleCenter, new Vector2(0f, -112f), new Vector2(112f, 112f), "v");
            leftArrow = AddPanel("Left", dpad.transform, TextAnchor.MiddleCenter, new Vector2(-112f, 0f), new Vector2(112f, 112f), "<");
            rightArrow = AddPanel("Right", dpad.transform, TextAnchor.MiddleCenter, new Vector2(112f, 0f), new Vector2(112f, 112f), ">");

            digLeftButton = AddPanel("DigLeft", canvas.transform, TextAnchor.LowerRight, new Vector2(-330f, 150f), new Vector2(170f, 170f), "DIG L");
            digRightButton = AddPanel("DigRight", canvas.transform, TextAnchor.LowerRight, new Vector2(-130f, 150f), new Vector2(170f, 170f), "DIG R");

            input.BindTouchAreas(dpad.rectTransform, digLeftButton.rectTransform, digRightButton.rectTransform);

            Image pause = AddPanel("Pause", canvas.transform, TextAnchor.UpperRight, new Vector2(-60f, -110f), new Vector2(96f, 96f), "II");
            pause.raycastTarget = true;
            pause.gameObject.AddComponent<Button>().onClick.AddListener(() => input.TogglePause());
        }

        private Text AddText(string name, Vector2 anchoredPosition, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(canvas.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = UiFont;
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;

            RectTransform rect = text.GetComponent<RectTransform>();
            rect.anchorMin = AnchorFor(alignment);
            rect.anchorMax = AnchorFor(alignment);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(480f, 80f);
            return text;
        }

        private static Image AddPanel(string name, Transform parent, TextAnchor anchor, Vector2 anchoredPosition, Vector2 size, string label)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.AddComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = false;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = AnchorFor(anchor);
            rect.anchorMax = AnchorFor(anchor);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            if (string.IsNullOrEmpty(label))
            {
                return image;
            }

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(panelObject.transform, false);
            var text = labelObject.AddComponent<Text>();
            text.font = UiFont;
            text.fontSize = 34;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            text.raycastTarget = false;

            RectTransform labelRect = text.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return image;
        }

        private static void Tint(Image image, bool pressed)
        {
            image.color = pressed ? PressedColor : PanelColor;
        }

        private static Vector2 AnchorFor(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    return new Vector2(0f, 1f);
                case TextAnchor.UpperCenter:
                    return new Vector2(0.5f, 1f);
                case TextAnchor.UpperRight:
                    return new Vector2(1f, 1f);
                case TextAnchor.MiddleCenter:
                    return new Vector2(0.5f, 0.5f);
                case TextAnchor.LowerLeft:
                    return new Vector2(0f, 0f);
                case TextAnchor.LowerRight:
                    return new Vector2(1f, 0f);
                default:
                    return new Vector2(0.5f, 0.5f);
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }
    }
}
