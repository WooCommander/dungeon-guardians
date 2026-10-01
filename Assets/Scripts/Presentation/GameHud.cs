using DungeonGuardians.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    public sealed class GameHud : MonoBehaviour
    {
        private Canvas canvas;
        private Text levelText;
        private Text goldText;
        private Text exitText;
        private Text messageText;
        private PlayerInputBridge input;

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
            exitText.text = open ? "ВЫХОД" : "ЗАКРЫТО";
            exitText.color = open ? new Color(0.42f, 1f, 0.82f) : new Color(1f, 0.74f, 0.28f);
        }

        public void SetPaused(bool paused)
        {
            if (paused)
            {
                ShowMessage("Пауза");
            }
            else if (messageText.text == "Пауза")
            {
                ShowMessage(string.Empty);
            }
        }

        public void ShowMessage(string value)
        {
            messageText.text = value;
            messageText.enabled = !string.IsNullOrWhiteSpace(value);
        }

        private void Build()
        {
            EnsureEventSystem();

            var canvasObject = new GameObject("HUD");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            levelText = AddText("Level", new Vector2(24f, -24f), TextAnchor.UpperLeft);
            goldText = AddText("Gold", new Vector2(0f, -24f), TextAnchor.UpperCenter);
            exitText = AddText("Exit", new Vector2(-24f, -24f), TextAnchor.UpperRight);
            messageText = AddText("Message", new Vector2(0f, 0f), TextAnchor.MiddleCenter);
            messageText.fontSize = 42;
            messageText.enabled = false;

            AddHoldButton("Left", new Vector2(84f, 84f), "<", value => input.SetLeft(value));
            AddHoldButton("Right", new Vector2(220f, 84f), ">", value => input.SetRight(value));
            AddHoldButton("Up", new Vector2(152f, 152f), "^", value => input.SetUp(value));
            AddHoldButton("Down", new Vector2(152f, 20f), "v", value => input.SetDown(value));
            AddTapButton("DigLeft", new Vector2(-260f, 84f), "⛏ L", () => input.DigLeft());
            AddTapButton("DigRight", new Vector2(-96f, 84f), "R ⛏", () => input.DigRight());
            AddTapButton("Pause", new Vector2(-52f, -52f), "Ⅱ", () => input.TogglePause(), TextAnchor.UpperRight);
        }

        private Text AddText(string name, Vector2 anchoredPosition, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(canvas.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;

            RectTransform rect = text.GetComponent<RectTransform>();
            rect.anchorMin = AnchorFor(alignment);
            rect.anchorMax = AnchorFor(alignment);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(480f, 80f);
            return text;
        }

        private void AddHoldButton(string name, Vector2 anchoredPosition, string label, System.Action<bool> callback)
        {
            Button button = AddButton(name, anchoredPosition, label, TextAnchor.LowerLeft);
            HoldButton holdButton = button.gameObject.AddComponent<HoldButton>();
            holdButton.OnChanged = callback;
        }

        private void AddTapButton(string name, Vector2 anchoredPosition, string label, UnityEngine.Events.UnityAction callback, TextAnchor anchor = TextAnchor.LowerRight)
        {
            Button button = AddButton(name, anchoredPosition, label, anchor);
            button.onClick.AddListener(callback);
        }

        private Button AddButton(string name, Vector2 anchoredPosition, string label, TextAnchor anchor)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(canvas.transform, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.1f, 0.13f, 0.82f);
            var button = buttonObject.AddComponent<Button>();

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = AnchorFor(anchor);
            rect.anchorMax = AnchorFor(anchor);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(116f, 116f);

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);
            var text = labelObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;

            RectTransform labelRect = text.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return button;
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
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }
}
