using System;
using DungeonGuardians.Input;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    public sealed class GameHud : MonoBehaviour
    {
        // The touch controls float over the level, which fills the whole screen; the following camera keeps the
        // explorer above them (LevelRenderer). Their sprites come from the concept screen (image.png, 1672 x 941,
        // tools/cut_ui.py) and are placed at the concept's pixel positions, converted to the 900-unit-high canvas.
        private const float ConceptToCanvas = 900f / 941f;
        private static readonly Color HighlightColor = new Color(1f, 0.85f, 0.55f, 0.3f);
        private static readonly Color PressedButtonColor = new Color(0.78f, 0.78f, 0.78f, 1f);

        private static readonly Color PanelColor = new Color(0.08f, 0.1f, 0.13f, 0.82f);
        private static readonly Color PressedColor = new Color(0.25f, 0.62f, 0.58f, 0.92f);
        private static readonly Color DPadBackColor = new Color(0.08f, 0.1f, 0.13f, 0.35f);

        // Unity 6 removed the built-in Arial.ttf; LegacyRuntime.ttf is its replacement.
        private static Font uiFont;
        private static Font UiFont => uiFont != null ? uiFont : uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private Canvas canvas;
        // The d-pad, the dig buttons and the strip behind them, shown or hidden together (GameSettings).
        private GameObject controlsRoot;
        // The d-pad and the dig buttons with their captions, scaled towards their screen corners and faded by the
        // button size and opacity settings.
        private RectTransform leftGroup;
        private RectTransform rightGroup;
        private GameObject pausePanel;
        private GameObject defeatPanel;
        private Text defeatTitle;
        private GameObject victoryPanel;
        private Text victoryTitle;
        private Text victorySubtitle;
        private Text levelText;
        private Text goldText;
        private Text livesText;
        // The level's name, shown large when the level starts and fading away.
        private Text titleBanner;
        private Text keyboardHint;
        // The keyboard hint stays a little longer than the title.
        private const float HintHold = 5f;
        private float titleShownAt = -100f;
        private const float TitleHold = 2.2f;
        private const float TitleFade = 0.8f;
        private Text exitText;
        private Text messageText;
        private Image upArrow;
        private Image downArrow;
        private Image leftArrow;
        private Image rightArrow;
        private Image digLeftButton;
        private Image digRightButton;
        private bool conceptControls;
        private bool conceptTopBar;
        private static Sprite softCircle;
        private PlayerInputBridge input;
        private string messageLabel = string.Empty;

        public event Action MenuRequested;
        public event Action MapRequested;

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
            levelText.text = conceptTopBar ? $"УРОВЕНЬ <color=#FFC23A>{index:00}</color>" : $"{index:00}/{count:00}  {title}";
            titleBanner.text = $"<size=30>УРОВЕНЬ {index}</size>\n{title}";
            titleShownAt = Time.unscaledTime;
        }

        // Hearts: the lives left glow red, the lost ones are dark.
        public void SetLives(int left, int total)
        {
            var hearts = new System.Text.StringBuilder();
            for (int i = 0; i < total; i++)
            {
                hearts.Append(i < left ? "<color=#FF5A3C>♥</color>" : "<color=#4A3A32>♥</color>");
                if (i < total - 1)
                {
                    hearts.Append(' ');
                }
            }

            livesText.text = hearts.ToString();
        }

        public void SetGold(int collected, int total)
        {
            goldText.text = conceptTopBar ? $"<color=#FFE7B0>{collected}</color> / {total}" : $"{collected} / {total}";
        }

        public void SetExit(bool open)
        {
            exitText.text = open ? "EXIT" : "LOCKED";
            exitText.color = open ? new Color(0.42f, 1f, 0.82f) : new Color(1f, 0.74f, 0.28f);
        }

        public void SetVisible(bool visible)
        {
            if (canvas != null)
            {
                canvas.gameObject.SetActive(visible);
            }
        }

        public void RefreshControls()
        {
            if (controlsRoot == null)
            {
                return;
            }

            controlsRoot.SetActive(GameSettings.TouchControlsVisible);
            foreach (RectTransform group in new[] { leftGroup, rightGroup })
            {
                group.localScale = Vector3.one * GameSettings.ButtonScale;
                group.GetComponent<CanvasGroup>().alpha = GameSettings.ButtonAlpha;
            }
        }

        public void SetPaused(bool paused)
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(paused);
                return;
            }

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

            float shown = Time.unscaledTime - titleShownAt;
            float alpha = 1f - Mathf.Clamp01((shown - TitleHold) / TitleFade);
            titleBanner.enabled = alpha > 0f;
            titleBanner.color = new Color(titleBanner.color.r, titleBanner.color.g, titleBanner.color.b, alpha);
            float hintAlpha = GameSettings.TouchControlsVisible ? 0f : 1f - Mathf.Clamp01((shown - HintHold) / TitleFade);
            keyboardHint.enabled = hintAlpha > 0f;
            keyboardHint.color = new Color(keyboardHint.color.r, keyboardHint.color.g, keyboardHint.color.b, hintAlpha * 0.85f);

            var held = input.HeldDirections;
            if (conceptControls)
            {
                // A warm glow over the pressed petal; a pressed dig button darkens and sinks slightly.
                Glow(upArrow, held.Up);
                Glow(downArrow, held.Down);
                Glow(leftArrow, held.Left);
                Glow(rightArrow, held.Right);
                Press(digLeftButton, input.DigLeftHeld);
                Press(digRightButton, input.DigRightHeld);
                return;
            }

            Tint(upArrow, held.Up);
            Tint(downArrow, held.Down);
            Tint(leftArrow, held.Left);
            Tint(rightArrow, held.Right);
            Tint(digLeftButton, input.DigLeftHeld);
            Tint(digRightButton, input.DigRightHeld);
        }

        private void Build()
        {

            // Landscape phones vary mostly in width, so the canvas scales by height to keep the controls the same
            // physical size.
            canvas = MenuStyle.CreateCanvas("HUD", transform, 0);

            levelText = AddText("Level", new Vector2(24f, -24f), TextAnchor.UpperLeft);
            livesText = AddText("Lives", new Vector2(24f, -70f), TextAnchor.UpperLeft);
            livesText.supportRichText = true;
            goldText = AddText("Gold", new Vector2(0f, -24f), TextAnchor.UpperCenter);
            exitText = AddText("Exit", new Vector2(-24f, -24f), TextAnchor.UpperRight);
            titleBanner = MenuStyle.AddLabel(canvas.transform, string.Empty, 64);
            titleBanner.color = new Color(1f, 0.82f, 0.45f);
            titleBanner.lineSpacing = 0.9f;
            titleBanner.rectTransform.anchorMin = titleBanner.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            titleBanner.rectTransform.sizeDelta = new Vector2(1200f, 160f);
            titleBanner.rectTransform.anchoredPosition = new Vector2(0f, -190f);
            messageText = AddText("Message", new Vector2(0f, 0f), TextAnchor.MiddleCenter);
            messageText.fontSize = 42;
            messageText.enabled = false;
            conceptTopBar = BuildConceptTopBar();

            // Touch controls are read by PlayerInputBridge per pointer, not through uGUI events.
            controlsRoot = new GameObject("Touch Controls", typeof(RectTransform));
            controlsRoot.transform.SetParent(canvas.transform, false);
            MenuStyle.Stretch((RectTransform)controlsRoot.transform);
            leftGroup = AddControlGroup("Left Controls", new Vector2(0f, 0f));
            rightGroup = AddControlGroup("Right Controls", new Vector2(1f, 0f));
            conceptControls = BuildConceptControls();
            if (!conceptControls)
            {
                BuildPlainControls();
            }

            RefreshControls();

            if (!conceptTopBar)
            {
                Image pause = AddPanel("Pause", canvas.transform, TextAnchor.UpperRight, new Vector2(-60f, -110f), new Vector2(96f, 96f), "II");
                pause.raycastTarget = true;
                pause.gameObject.AddComponent<Button>().onClick.AddListener(() => input.TogglePause());
            }

            BuildPausePanel();
            BuildDefeatPanel();
            BuildVictoryPanel();
            BuildKeyboardHint();
            // Everything above keeps clear of a phone's camera cutout and rounded corners.
            SafeArea.Wrap(canvas.transform);
        }

        // On a PC, where there are no touch controls, the keys are shown at the bottom when a level starts.
        private void BuildKeyboardHint()
        {
            keyboardHint = MenuStyle.AddLabel(canvas.transform,
                "← → ↑ ↓ или WASD — движение     Q / E — копать влево / вправо     R — заново     Esc — пауза", 26);
            keyboardHint.color = new Color(1f, 0.92f, 0.75f);
            keyboardHint.rectTransform.anchorMin = keyboardHint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            keyboardHint.rectTransform.sizeDelta = new Vector2(1500f, 50f);
            keyboardHint.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            keyboardHint.enabled = false;
        }

        private Text victoryStars;
        private Text victoryStats;

        private void BuildVictoryPanel()
        {
            var overlay = new GameObject("Victory Panel").AddComponent<Image>();
            overlay.transform.SetParent(canvas.transform, false);
            overlay.color = new Color(0.01f, 0.02f, 0.03f, 0.65f);
            MenuStyle.Stretch(overlay.rectTransform);

            victoryTitle = MenuStyle.AddLabel(overlay.transform, string.Empty, 54);
            victoryTitle.color = new Color(1f, 0.85f, 0.42f);
            victoryTitle.fontStyle = FontStyle.Bold;
            victoryTitle.rectTransform.anchorMin = victoryTitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            victoryTitle.rectTransform.sizeDelta = new Vector2(1300f, 80f);
            victoryTitle.rectTransform.anchoredPosition = new Vector2(0f, 220f);

            victoryStars = MenuStyle.AddLabel(overlay.transform, "★★★", 48);
            victoryStars.color = new Color(1f, 0.84f, 0.28f);
            victoryStars.alignment = TextAnchor.MiddleCenter;
            victoryStars.rectTransform.anchorMin = victoryStars.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            victoryStars.rectTransform.sizeDelta = new Vector2(400f, 60f);
            victoryStars.rectTransform.anchoredPosition = new Vector2(0f, 155f);

            victoryStats = MenuStyle.AddLabel(overlay.transform, string.Empty, 26);
            victoryStats.color = new Color(0.95f, 0.92f, 0.82f);
            victoryStats.alignment = TextAnchor.MiddleCenter;
            victoryStats.rectTransform.anchorMin = victoryStats.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            victoryStats.rectTransform.sizeDelta = new Vector2(800f, 50f);
            victoryStats.rectTransform.anchoredPosition = new Vector2(0f, 105f);

            victorySubtitle = MenuStyle.AddLabel(overlay.transform, string.Empty, 24);
            victorySubtitle.color = new Color(0.85f, 0.85f, 0.85f);
            victorySubtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            victorySubtitle.rectTransform.anchorMin = victorySubtitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            victorySubtitle.rectTransform.sizeDelta = new Vector2(1100f, 60f);
            victorySubtitle.rectTransform.anchoredPosition = new Vector2(0f, 55f);

            // The one way on: the level map shows the progress and the next level is chosen there.
            Button map = MenuStyle.CreatePlateButton(overlay.transform, "КАРТА УРОВНЕЙ", new Vector2(460f, 84f), () => MapRequested?.Invoke());
            var mapRect = (RectTransform)map.transform;
            mapRect.anchorMin = mapRect.anchorMax = new Vector2(0.5f, 0.5f);
            mapRect.anchoredPosition = new Vector2(0f, -40f);

            victoryPanel = overlay.gameObject;
            victoryPanel.SetActive(false);
        }

        // After the last life is lost (DefeatSequence); the title names what happened.
        public void ShowDefeat(string title)
        {
            if (defeatPanel != null)
            {
                defeatTitle.text = title;
                defeatPanel.SetActive(true);
            }
        }

        // The level is won. Without a next level the "next" button is hidden and the subtitle closes the story.
        public void ShowVictory(string title, string subtitle, int stars, float timeSeconds, bool isNewBest)
        {
            if (victoryPanel == null)
            {
                return;
            }

            victoryTitle.text = title;
            victorySubtitle.text = subtitle;

            string starsStr;
            switch (stars)
            {
                case 3: starsStr = "★ ★ ★"; break;
                case 2: starsStr = "★ ★ ☆"; break;
                default: starsStr = "★ ☆ ☆"; break;
            }
            victoryStars.text = starsStr;

            int mins = (int)(timeSeconds / 60f);
            int secs = (int)(timeSeconds % 60f);
            string timeStr = $"{mins:00}:{secs:00}";
            victoryStats.text = isNewBest
                ? $"Время: <color=#FFE7B0>{timeStr}</color>  <color=#5AFFDF>★ НОВЫЙ РЕКОРД! ★</color>"
                : $"Время: <color=#FFE7B0>{timeStr}</color>";

            victoryPanel.SetActive(true);
        }

        public void HideVictory()
        {
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(false);
            }
        }

        public void HideDefeat()
        {
            if (defeatPanel != null)
            {
                defeatPanel.SetActive(false);
            }
        }

        // The level stays visible behind it, the stone explorer in the middle of it.
        private void BuildDefeatPanel()
        {
            var overlay = new GameObject("Defeat Panel").AddComponent<Image>();
            overlay.transform.SetParent(canvas.transform, false);
            overlay.color = new Color(0.01f, 0.02f, 0.03f, 0.55f);
            MenuStyle.Stretch(overlay.rectTransform);

            Text title = MenuStyle.AddLabel(overlay.transform, string.Empty, 56);
            defeatTitle = title;
            title.color = new Color(1f, 0.8f, 0.4f);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            title.rectTransform.sizeDelta = new Vector2(1200f, 100f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 110f);

            Button retry = MenuStyle.CreatePlateButton(overlay.transform, "ПОПРОБОВАТЬ СНОВА", new Vector2(460f, 84f), () => input.Restart());
            var retryRect = (RectTransform)retry.transform;
            retryRect.anchorMin = retryRect.anchorMax = new Vector2(0.5f, 0.5f);
            retryRect.anchoredPosition = new Vector2(0f, -20f);
            AddPauseButton(overlay.transform, "В МЕНЮ", -125f, () => MenuRequested?.Invoke());

            defeatPanel = overlay.gameObject;
            defeatPanel.SetActive(false);
        }

        // Over the frozen level: continue, start the level again or go back to the start screen.
        private void BuildPausePanel()
        {
            var overlay = new GameObject("Pause Panel").AddComponent<Image>();
            overlay.transform.SetParent(canvas.transform, false);
            overlay.color = new Color(0.01f, 0.02f, 0.03f, 0.7f);
            MenuStyle.Stretch(overlay.rectTransform);

            Text title = MenuStyle.AddLabel(overlay.transform, "ПАУЗА", 54);
            title.color = new Color(1f, 0.8f, 0.4f);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            title.rectTransform.sizeDelta = new Vector2(600f, 90f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 190f);

            AddPauseButton(overlay.transform, "ПРОДОЛЖИТЬ", 70f, () => input.TogglePause());
            AddPauseButton(overlay.transform, "ЗАНОВО", -30f, () => input.Restart());
            AddPauseButton(overlay.transform, "В МЕНЮ", -130f, () => MenuRequested?.Invoke());

            pausePanel = overlay.gameObject;
            pausePanel.SetActive(false);
        }

        private static void AddPauseButton(Transform parent, string label, float y, Action onClick)
        {
            Button button = MenuStyle.CreatePlateButton(parent, label, new Vector2(360f, 76f), onClick);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        // The concept's top bar: "УРОВЕНЬ 03" on a dark plate at the top left, the gold counter with a bar icon in the
        // middle and the round pause button at the top right. Positions are concept pixel centres from the top corners.
        private bool BuildConceptTopBar()
        {
            Sprite levelPlate = Resources.Load<Sprite>("UI/plate_level");
            Sprite goldPlate = Resources.Load<Sprite>("UI/plate_gold");
            Sprite goldIcon = Resources.Load<Sprite>("UI/gold_icon");
            Sprite pauseSprite = Resources.Load<Sprite>("UI/pause");
            if (levelPlate == null || goldPlate == null || goldIcon == null || pauseSprite == null)
            {
                return false;
            }

            Image level = AddSprite("Level Plate", levelPlate, TextAnchor.UpperLeft, new Vector2(137.5f, -37.5f));
            PlaceLabel(levelText, level.transform, Vector2.zero, 27, TextAnchor.MiddleCenter);
            Image lives = AddSprite("Lives Plate", levelPlate, TextAnchor.UpperLeft, new Vector2(137.5f + 245f, -37.5f));
            PlaceLabel(livesText, lives.transform, Vector2.zero, 30, TextAnchor.MiddleCenter);

            Image gold = AddSprite("Gold Plate", goldPlate, TextAnchor.UpperCenter, new Vector2(0.5f, -39f));
            Image icon = AddSprite("Gold Icon", goldIcon, TextAnchor.UpperCenter, new Vector2(-51f, -40f));
            icon.transform.SetParent(gold.transform, true);
            PlaceLabel(goldText, gold.transform, new Vector2(35f, 0f), 29, TextAnchor.MiddleCenter);

            // The exit state is shown by the door itself on the concept.
            exitText.enabled = false;

            Image pause = AddSprite("Pause", pauseSprite, TextAnchor.UpperRight, new Vector2(-(1672f - 1619f), -48f));
            pause.raycastTarget = true;
            pause.gameObject.AddComponent<Button>().onClick.AddListener(() => input.TogglePause());
            return true;
        }

        // Moves an existing label onto a plate, centred at a concept-pixel offset.
        private static void PlaceLabel(Text label, Transform plate, Vector2 conceptOffset, int fontSize, TextAnchor alignment)
        {
            label.transform.SetParent(plate, false);
            label.supportRichText = true;
            label.fontSize = fontSize;
            label.alignment = alignment;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = conceptOffset * ConceptToCanvas;
            rect.sizeDelta = new Vector2(220f, 50f);
        }

        // A full-screen layer whose pivot is a bottom corner, so scaling it grows the controls out of that corner.
        private RectTransform AddControlGroup(string name, Vector2 corner)
        {
            var group = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            group.transform.SetParent(controlsRoot.transform, false);
            var rect = (RectTransform)group.transform;
            MenuStyle.Stretch(rect);
            rect.pivot = corner;
            group.GetComponent<CanvasGroup>().blocksRaycasts = false;
            return rect;
        }

        // The concept's touch controls, over the level: the round d-pad on the left, the two pickaxe buttons with
        // captions on the right.
        private bool BuildConceptControls()
        {
            Sprite dpadSprite = Resources.Load<Sprite>("UI/dpad");
            Sprite digLeftSprite = Resources.Load<Sprite>("UI/dig_left");
            Sprite digRightSprite = Resources.Load<Sprite>("UI/dig_right");
            if (dpadSprite == null || digLeftSprite == null || digRightSprite == null)
            {
                return false;
            }

            // Positions are the concept's pixel centres measured from the bottom-left or bottom-right corner.
            Image dpad = AddSprite("DPad", dpadSprite, TextAnchor.LowerLeft, new Vector2(180f, 941f - 806f), leftGroup);
            upArrow = AddGlow("Up", dpad.transform, new Vector2(0f, 69f));
            downArrow = AddGlow("Down", dpad.transform, new Vector2(0f, -69f));
            leftArrow = AddGlow("Left", dpad.transform, new Vector2(-73f, 0f));
            rightArrow = AddGlow("Right", dpad.transform, new Vector2(75f, 0f));

            digLeftButton = AddSprite("DigLeft", digLeftSprite, TextAnchor.LowerRight, new Vector2(-(1672f - 1332f), 941f - 792f), rightGroup);
            digRightButton = AddSprite("DigRight", digRightSprite, TextAnchor.LowerRight, new Vector2(-(1672f - 1525f), 941f - 792f), rightGroup);
            AddCaption("UI/label_dig_left", new Vector2(-(1672f - 1333f), 941f - 887f));
            AddCaption("UI/label_dig_right", new Vector2(-(1672f - 1527f), 941f - 887f));

            input.BindTouchAreas(dpad.rectTransform, digLeftButton.rectTransform, digRightButton.rectTransform);
            return true;
        }

        // Concept pixel position (centre) and native size become canvas units.
        private Image AddSprite(string name, Sprite sprite, TextAnchor anchor, Vector2 conceptPosition, Transform parent = null)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent != null ? parent : canvas.transform, false);
            image.sprite = sprite;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = AnchorFor(anchor);
            rect.anchoredPosition = conceptPosition * ConceptToCanvas;
            rect.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height) * ConceptToCanvas;
            return image;
        }

        private void AddCaption(string path, Vector2 conceptPosition)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                AddSprite(sprite.name, sprite, TextAnchor.LowerRight, conceptPosition, rightGroup);
            }
        }

        private static Image AddGlow(string name, Transform dpad, Vector2 conceptOffset)
        {
            var glow = new GameObject(name).AddComponent<Image>();
            glow.transform.SetParent(dpad, false);
            glow.sprite = GetSoftCircle();
            glow.color = Color.clear;
            glow.raycastTarget = false;
            glow.rectTransform.anchoredPosition = conceptOffset * ConceptToCanvas;
            glow.rectTransform.sizeDelta = new Vector2(96f, 96f) * ConceptToCanvas;
            return glow;
        }

        private static void Glow(Image glow, bool pressed)
        {
            glow.color = pressed ? HighlightColor : Color.clear;
        }

        private static void Press(Image button, bool pressed)
        {
            button.color = pressed ? PressedButtonColor : Color.white;
            button.rectTransform.localScale = Vector3.one * (pressed ? 0.94f : 1f);
        }

        // A round glow that fades towards the edge, generated once.
        private static Sprite GetSoftCircle()
        {
            if (softCircle != null)
            {
                return softCircle;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Soft Circle" };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float fade = Mathf.Clamp01(1f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, fade * fade));
                }
            }

            texture.Apply();
            softCircle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return softCircle;
        }

        // Plain stand-in controls, used when the concept sprites are missing.
        private void BuildPlainControls()
        {
            Image dpad = AddPanel("DPad", leftGroup, TextAnchor.LowerLeft, new Vector2(150f, 121f), new Vector2(232f, 232f), string.Empty);
            dpad.color = DPadBackColor;
            upArrow = AddPanel("Up", dpad.transform, TextAnchor.MiddleCenter, new Vector2(0f, 78f), new Vector2(76f, 76f), "^");
            downArrow = AddPanel("Down", dpad.transform, TextAnchor.MiddleCenter, new Vector2(0f, -78f), new Vector2(76f, 76f), "v");
            leftArrow = AddPanel("Left", dpad.transform, TextAnchor.MiddleCenter, new Vector2(-78f, 0f), new Vector2(76f, 76f), "<");
            rightArrow = AddPanel("Right", dpad.transform, TextAnchor.MiddleCenter, new Vector2(78f, 0f), new Vector2(76f, 76f), ">");

            digLeftButton = AddPanel("DigLeft", rightGroup, TextAnchor.LowerRight, new Vector2(-280f, 112f), new Vector2(150f, 150f), "DIG L");
            digRightButton = AddPanel("DigRight", rightGroup, TextAnchor.LowerRight, new Vector2(-105f, 112f), new Vector2(150f, 150f), "DIG R");

            input.BindTouchAreas(dpad.rectTransform, digLeftButton.rectTransform, digRightButton.rectTransform);
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
    }
}
