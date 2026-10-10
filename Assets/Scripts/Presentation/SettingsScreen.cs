using System;
using DungeonGuardians.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    // The settings screen organized into 3 tabs: Sound, Controls, Display & Language.
    // Built on clean modular sprites and auto-adapting layouts.
    public sealed class SettingsScreen : MonoBehaviour
    {
        // Dimensions matching reference resolution 1672x941
        private const float IconLeft = 200f;
        private const float IconSize = 56f;
        private const float LabelLeft = 275f;
        private const float LabelHalf = 22f;

        private const float TrackLeft = 750f;
        private const float TrackRight = 1180f;
        private const float TrackHalf = 16f;
        private const float KnobHalf = 26f;
        private const float KnobTravelLeft = TrackLeft + 14f;
        private const float KnobTravelRight = TrackRight - 14f;
        private const float FillInset = 4f;
        private const float FillTop = 8f;
        private const float FillBottom = 9f;

        private const float ValueLeft = 1210f;
        private const float ValueRight = 1330f;
        private const float ValueHalf = 20f;

        private static readonly Rect TabSoundBox = new Rect(206f, 195f, 400f, 76f);
        private static readonly Rect TabControlsBox = new Rect(636f, 195f, 400f, 76f);
        private static readonly Rect TabGraphicsBox = new Rect(1066f, 195f, 400f, 76f);

        private static readonly Rect ToggleBox = new Rect(1190f, 320f, 136f, 60f);
        private static readonly Rect ActionButtonBox = new Rect(800f, 0f, 526f, 64f);

        private static readonly Color ValueColor = new Color(1f, 0.9f, 0.72f);
        private static readonly Color LabelGold = new Color(1f, 0.88f, 0.58f);

        private Transform picture;
        private Transform currentPage;
        private GameObject soundPage;
        private GameObject controlsPage;
        private GameObject graphicsPage;

        private Image soundTabBg;
        private Text soundTabText;
        private Image controlsTabBg;
        private Text controlsTabText;
        private Image graphicsTabBg;
        private Text graphicsTabText;

        private Slider music;
        private Slider sound;
        private Slider buttonSize;
        private Slider buttonOpacity;

        private Image vibration;
        private Sprite vibrationOn;
        private Sprite vibrationOff;

        private Button cameraStyleButton;
        private Text cameraStyleText;
        private Text headerText;

        private Text musicLabelText;
        private Text soundLabelText;
        private Text vibrationLabelText;
        private Text buttonSizeLabelText;
        private Text buttonOpacityLabelText;
        private Text cameraLabelText;
        private Text langLabelText;
        private Text tiltText;
        private Text tiltLabelText;
        private Text cancelText;
        private Text doneText;

        private Image langAutoBg;
        private Text langAutoText;
        private Image langRuBg;
        private Text langRuText;
        private Image langEnBg;
        private Text langEnText;

        private string currentHeaderKey = "settings_header";
        private (float music, float sound, bool vibration, float buttonSize, float buttonOpacity, int cameraStyle, bool tiltControl, float tiltCalibration, float tiltVerticalCalibration, AppLanguage language) opened;

        public event Action Changed;
        public event Action Closed;

        public static SettingsScreen Create(Transform parent)
        {
            var root = new GameObject("Settings", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            MenuStyle.Stretch((RectTransform)root.transform);
            var screen = root.AddComponent<SettingsScreen>();
            screen.Build();
            return screen;
        }

        public void Open()
        {
            opened = (GameSettings.Music, GameSettings.Sound, GameSettings.Vibration, GameSettings.ButtonSize,
                GameSettings.ButtonOpacity, GameSettings.CameraStyle, GameSettings.TiltControl, GameSettings.TiltCalibration,
                GameSettings.TiltVerticalCalibration, Localization.SelectedPreference);
            SetPage(soundPage, "settings_sound_section");
            Refresh();
            gameObject.SetActive(true);
        }

        public void Close()
        {
            bool wasActive = gameObject.activeSelf;
            GameSettings.Save();
            gameObject.SetActive(false);
            if (wasActive)
            {
                Closed?.Invoke();
            }
        }

        private void Cancel()
        {
            GameSettings.Music = opened.music;
            GameSettings.Sound = opened.sound;
            GameSettings.Vibration = opened.vibration;
            GameSettings.ButtonSize = opened.buttonSize;
            GameSettings.ButtonOpacity = opened.buttonOpacity;
            GameSettings.CameraStyle = opened.cameraStyle;
            GameSettings.TiltControl = opened.tiltControl;
            GameSettings.TiltCalibration = opened.tiltCalibration;
            GameSettings.TiltVerticalCalibration = opened.tiltVerticalCalibration;
            Localization.SetLanguage(opened.language);
            Refresh();
            Changed?.Invoke();
            Close();
        }

        private void Build()
        {
            Image backing = MenuStyle.CreatePicture(transform, "Backgrounds/settings_blur", AspectRatioFitter.AspectMode.EnvelopeParent);
            backing.raycastTarget = true;
            picture = MenuStyle.CreatePicture(transform, "Backgrounds/settings", AspectRatioFitter.AspectMode.FitInParent).transform;

            // Top Header: НАСТРОЙКИ / SETTINGS
            headerText = AddHeaderTitle();

            // 3 Pages
            soundPage = CreatePage("SoundPage");
            controlsPage = CreatePage("ControlsPage");
            graphicsPage = CreatePage("GraphicsPage");

            AddTabs();

            // 1) Sound Tab
            currentPage = soundPage.transform;
            AddIcon(410f, "UI/icon_sound");
            musicLabelText = AddRowLabel(410f, "settings_music");
            music = AddSlider(410f, value => GameSettings.Music = value, _ => Percent(GameSettings.Music));

            AddIcon(530f, "UI/icon_sound");
            soundLabelText = AddRowLabel(530f, "settings_sound");
            sound = AddSlider(530f, value => GameSettings.Sound = value, _ => Percent(GameSettings.Sound));

            // 2) Controls Tab
            currentPage = controlsPage.transform;
            AddIcon(350f, "UI/icon_vibration");
            vibrationLabelText = AddRowLabel(350f, "settings_vibration");
            AddVibrationSwitch(350f);

            AddIcon(440f, "UI/icon_dpad");
            tiltLabelText = AddRowLabel(440f, "settings_tilt");
            AddTiltSwitch(440f);

            AddIcon(530f, "UI/icon_controls");
            buttonSizeLabelText = AddRowLabel(530f, "settings_size");
            buttonSize = AddSlider(530f, value => GameSettings.ButtonSize = value, _ => Percent(GameSettings.ButtonScale));

            AddIcon(620f, "UI/icon_controls");
            buttonOpacityLabelText = AddRowLabel(620f, "settings_opacity");
            buttonOpacity = AddSlider(620f, value => GameSettings.ButtonOpacity = value, _ => Percent(GameSettings.ButtonAlpha));

            // 3) Display & Language Tab
            currentPage = graphicsPage.transform;
            langLabelText = AddRowLabel(410f, "settings_lang_label");
            AddLanguageSelector(410f);
            AddCameraStyleSwitch(530f);

            // Bottom Buttons
            cancelText = MenuStyle.AddQuietButton(picture, "settings_cancel", new Rect(380f, 763f, 435f, 112f), Localization.T("settings_cancel"), Cancel);
            doneText = MenuStyle.AddCaptionedButton(picture, "settings_done_blank", new Rect(855f, 763f, 435f, 112f), Localization.T("settings_done"), MenuStyle.GoldCaption, Close);
            EqualFontSize.Apply(picture.gameObject, 60, cancelText, doneText);

            SetPage(soundPage, "settings_sound_section");
        }

        private void AddTabs()
        {
            (soundTabBg, soundTabText) = CreateTab("TabSound", TabSoundBox, "settings_sound_section", () => SetPage(soundPage, "settings_sound_section"));
            (controlsTabBg, controlsTabText) = CreateTab("TabControls", TabControlsBox, "settings_controls", () => SetPage(controlsPage, "settings_controls"));
            (graphicsTabBg, graphicsTabText) = CreateTab("TabGraphics", TabGraphicsBox, "settings_graphics", () => SetPage(graphicsPage, "settings_graphics"));
        }

        private (Image bg, Text text) CreateTab(string name, Rect box, string key, Action onClick)
        {
            Button button = MenuStyle.CreatePlateButtonOnPicture(picture, string.Empty, box, onClick);
            button.name = name;
            Image bg = button.GetComponent<Image>();
            Text text = button.GetComponentInChildren<Text>();
            text.text = Localization.T(key);
            text.fontSize = 32;
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 36;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.05f, 0.01f, 0.98f);
            outline.effectDistance = new Vector2(2f, -2f);
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.04f, 0.02f, 0.01f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return (bg, text);
        }

        private void Refresh()
        {
            music.SetValueWithoutNotify(GameSettings.Music);
            sound.SetValueWithoutNotify(GameSettings.Sound);
            buttonSize.SetValueWithoutNotify(GameSettings.ButtonSize);
            buttonOpacity.SetValueWithoutNotify(GameSettings.ButtonOpacity);
            foreach (Slider slider in new[] { music, sound, buttonSize, buttonOpacity })
            {
                slider.onValueChanged.Invoke(slider.value);
            }

            vibration.sprite = GameSettings.Vibration ? vibrationOn : vibrationOff;
            RefreshLanguageSelector();
            UpdateCameraStyleText();
        }

        private void AddIcon(float rowY, string spritePath)
        {
            var iconImg = new GameObject("Icon").AddComponent<Image>();
            iconImg.transform.SetParent(currentPage ?? picture, false);
            iconImg.sprite = Resources.Load<Sprite>(spritePath);
            iconImg.color = new Color(1f, 0.88f, 0.58f, 0.95f);
            iconImg.raycastTarget = false;
            MenuStyle.PlaceOnPicture(iconImg.rectTransform, new Rect(IconLeft, rowY - IconSize / 2f, IconSize, IconSize));
        }

        private Slider AddSlider(float row, Action<float> store, Func<float, string> format)
        {
            Transform parent = currentPage ?? picture;
            float width = TrackRight - TrackLeft;
            float height = 2f * KnobHalf;

            // Track background
            var trackBg = new GameObject("TrackBg").AddComponent<Image>();
            trackBg.transform.SetParent(parent, false);
            trackBg.sprite = Resources.Load<Sprite>("UI/settings_track");
            trackBg.type = Image.Type.Sliced;
            trackBg.raycastTarget = false;
            MenuStyle.PlaceOnPicture(trackBg.rectTransform, new Rect(TrackLeft, row - TrackHalf, width, TrackHalf * 2f));

            var root = new GameObject("Slider").AddComponent<Image>();
            root.transform.SetParent(parent, false);
            root.color = Color.clear;
            MenuStyle.PlaceOnPicture(root.rectTransform, new Rect(TrackLeft, row - KnobHalf, width, height));

            // Fill
            RectTransform fillArea = AddArea(root.transform, "Fill Area",
                new Vector2(FillInset / width, (KnobHalf - FillBottom) / height),
                new Vector2(1f - FillInset / width, (KnobHalf + FillTop) / height));
            var fill = new GameObject("Fill").AddComponent<Image>();
            fill.transform.SetParent(fillArea, false);
            fill.raycastTarget = false;
            fill.sprite = Resources.Load<Sprite>("UI/settings_fill");
            fill.color = Color.white;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            MenuStyle.Stretch(fill.rectTransform);

            // Knob
            RectTransform knobArea = AddArea(root.transform, "Knob Area",
                new Vector2((KnobTravelLeft - TrackLeft) / width, 0f),
                new Vector2((KnobTravelRight - TrackLeft) / width, 1f));
            var knob = new GameObject("Knob").AddComponent<Image>();
            knob.transform.SetParent(knobArea, false);
            knob.sprite = Resources.Load<Sprite>("UI/settings_knob");
            knob.color = Color.white;
            knob.rectTransform.sizeDelta = Vector2.zero;
            var square = knob.gameObject.AddComponent<AspectRatioFitter>();
            square.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            square.aspectRatio = 1f;

            // Value text
            var valueText = new GameObject("Value").AddComponent<Text>();
            valueText.transform.SetParent(parent, false);
            valueText.font = MenuStyle.Font;
            valueText.fontStyle = FontStyle.Bold;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.color = ValueColor;
            valueText.raycastTarget = false;
            valueText.resizeTextForBestFit = true;
            valueText.resizeTextMinSize = 8;
            valueText.resizeTextMaxSize = 60;
            MenuStyle.PlaceOnPicture(valueText.rectTransform, new Rect(ValueLeft, row - ValueHalf, ValueRight - ValueLeft, 2f * ValueHalf));

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = knob.rectTransform;
            slider.targetGraphic = knob;
            slider.direction = Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.onValueChanged.AddListener(value =>
            {
                store(value);
                valueText.text = format(value);
                Changed?.Invoke();
            });
            return slider;
        }

        private GameObject CreatePage(string name)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(picture, false);
            MenuStyle.Stretch((RectTransform)page.transform);
            return page;
        }

        private static RectTransform AddArea(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var area = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            area.SetParent(parent, false);
            area.anchorMin = anchorMin;
            area.anchorMax = anchorMax;
            area.offsetMin = area.offsetMax = Vector2.zero;
            return area;
        }

        private void AddVibrationSwitch(float rowY)
        {
            Transform parent = currentPage ?? picture;
            vibrationOn = Resources.Load<Sprite>("UI/settings_toggle_on");
            vibrationOff = Resources.Load<Sprite>("UI/settings_toggle_off");
            vibration = new GameObject("Vibration").AddComponent<Image>();
            vibration.transform.SetParent(parent, false);
            MenuStyle.PlaceOnPicture(vibration.rectTransform, new Rect(ToggleBox.x, rowY - ToggleBox.height / 2f, ToggleBox.width, ToggleBox.height));

            MenuStyle.MakeButton(vibration, () =>
            {
                GameSettings.Vibration = !GameSettings.Vibration;
                vibration.sprite = GameSettings.Vibration ? vibrationOn : vibrationOff;
                GameSettings.Vibrate();
                Changed?.Invoke();
            });
        }

        private void AddLanguageSelector(float rowY)
        {
            float btnW = 160f;
            float btnH = 64f;
            float spacing = 20f;
            float startX = 800f;

            (langAutoBg, langAutoText) = CreateLangButton("Lang_Auto", new Rect(startX, rowY - btnH / 2f, btnW, btnH), AppLanguage.Auto);
            (langRuBg, langRuText) = CreateLangButton("Lang_RU", new Rect(startX + btnW + spacing, rowY - btnH / 2f, btnW, btnH), AppLanguage.Russian);
            (langEnBg, langEnText) = CreateLangButton("Lang_EN", new Rect(startX + (btnW + spacing) * 2f, rowY - btnH / 2f, btnW, btnH), AppLanguage.English);
        }

        private (Image bg, Text text) CreateLangButton(string name, Rect rect, AppLanguage lang)
        {
            Transform parent = currentPage ?? picture;
            var buttonImage = new GameObject(name).AddComponent<Image>();
            buttonImage.transform.SetParent(parent, false);
            buttonImage.sprite = Resources.Load<Sprite>("UI/tab_inactive");
            buttonImage.type = Image.Type.Sliced;
            MenuStyle.PlaceOnPicture(buttonImage.rectTransform, rect);

            var text = new GameObject("Text").AddComponent<Text>();
            text.transform.SetParent(buttonImage.transform, false);
            MenuStyle.Stretch(text.rectTransform);
            text.font = MenuStyle.Font;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 32;

            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.06f, 0.02f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            MenuStyle.MakeButton(buttonImage, () =>
            {
                Localization.SetLanguage(lang);
                Refresh();
                Changed?.Invoke();
            });

            return (buttonImage, text);
        }

        private Text AddHeaderTitle()
        {
            var title = new GameObject("HeaderTitle").AddComponent<Text>();
            title.transform.SetParent(picture, false);
            title.font = MenuStyle.Font;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(1f, 0.94f, 0.74f);
            title.text = Localization.T("settings_header");
            title.raycastTarget = false;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 24;
            title.resizeTextMaxSize = 62;

            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.14f, 0.07f, 0.02f, 0.95f);
            outline.effectDistance = new Vector2(3f, -3f);

            MenuStyle.PlaceOnPicture(title.rectTransform, new Rect(530f, 65f, 612f, 92f));
            return title;
        }

        private Text AddRowLabel(float rowY, string key)
        {
            Transform parent = currentPage ?? picture;
            var label = new GameObject("Label_" + key).AddComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = MenuStyle.Font;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = LabelGold;
            label.text = Localization.T(key);
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = 36;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.06f, 0.02f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            MenuStyle.PlaceOnPicture(label.rectTransform, new Rect(LabelLeft, rowY - LabelHalf, TrackLeft - 20f - LabelLeft, 2f * LabelHalf));
            return label;
        }

        private void RefreshLanguageSelector()
        {
            AppLanguage selected = Localization.SelectedPreference;
            Sprite activeSprite = Resources.Load<Sprite>("UI/tab_active");
            Sprite inactiveSprite = Resources.Load<Sprite>("UI/tab_inactive");

            SetLangButtonState(langAutoBg, langAutoText, selected == AppLanguage.Auto, Localization.T("settings_lang_auto"), activeSprite, inactiveSprite);
            SetLangButtonState(langRuBg, langRuText, selected == AppLanguage.Russian, Localization.T("settings_lang_ru"), activeSprite, inactiveSprite);
            SetLangButtonState(langEnBg, langEnText, selected == AppLanguage.English, Localization.T("settings_lang_en"), activeSprite, inactiveSprite);

            if (headerText != null) headerText.text = Localization.T(currentHeaderKey);
            if (soundTabText != null) soundTabText.text = Localization.T("settings_sound_section");
            if (controlsTabText != null) controlsTabText.text = Localization.T("settings_controls");
            if (graphicsTabText != null) graphicsTabText.text = Localization.T("settings_graphics");
            if (musicLabelText != null) musicLabelText.text = Localization.T("settings_music");
            if (soundLabelText != null) soundLabelText.text = Localization.T("settings_sound");
            if (vibrationLabelText != null) vibrationLabelText.text = Localization.T("settings_vibration");
            if (buttonSizeLabelText != null) buttonSizeLabelText.text = Localization.T("settings_size");
            if (buttonOpacityLabelText != null) buttonOpacityLabelText.text = Localization.T("settings_opacity");
            if (cameraLabelText != null) cameraLabelText.text = Localization.T("settings_camera");
            if (langLabelText != null) langLabelText.text = Localization.T("settings_lang_label");
            if (tiltLabelText != null) tiltLabelText.text = Localization.T("settings_tilt");
            if (tiltText != null) tiltText.text = Localization.T(GameSettings.TiltControl ? "settings_tilt_on" : "settings_tilt_off");
            if (cancelText != null) cancelText.text = Localization.T("settings_cancel");
            if (doneText != null) doneText.text = Localization.T("settings_done");
        }

        private void AddTiltSwitch(float rowY)
        {
            Rect box = new Rect(ActionButtonBox.x, rowY - ActionButtonBox.height / 2f, ActionButtonBox.width, ActionButtonBox.height);
            Button button = MenuStyle.CreatePlateButtonOnPicture(currentPage ?? picture, string.Empty, box, ToggleTiltControl);
            tiltText = button.GetComponentInChildren<Text>();
            tiltText.fontSize = 24;
            tiltText.resizeTextForBestFit = true;
            tiltText.resizeTextMinSize = 10;
            tiltText.resizeTextMaxSize = 28;
            tiltText.text = Localization.T(GameSettings.TiltControl ? "settings_tilt_on" : "settings_tilt_off");
        }

        private void ToggleTiltControl()
        {
            GameSettings.TiltControl = !GameSettings.TiltControl;
            if (GameSettings.TiltControl)
            {
                Accelerometer accelerometer = Accelerometer.current;
                if (accelerometer != null)
                {
                    if (!accelerometer.enabled)
                    {
                        InputSystem.EnableDevice(accelerometer);
                    }

                    Vector3 acceleration = accelerometer.acceleration.ReadValue();
                    GameSettings.TiltCalibration = acceleration.x;
                    GameSettings.TiltVerticalCalibration = acceleration.y;
                }
            }

            if (tiltText != null)
            {
                tiltText.text = Localization.T(GameSettings.TiltControl ? "settings_tilt_on" : "settings_tilt_off");
            }

            Changed?.Invoke();
        }

        private void SetPage(GameObject page, string headerKey)
        {
            currentHeaderKey = headerKey;
            if (soundPage != null) soundPage.SetActive(page == soundPage);
            if (controlsPage != null) controlsPage.SetActive(page == controlsPage);
            if (graphicsPage != null) graphicsPage.SetActive(page == graphicsPage);
            if (headerText != null) headerText.text = Localization.T("settings_header");
            UpdateTabs(page);
        }

        private void UpdateTabs(GameObject activePage)
        {
            Sprite activeSprite = Resources.Load<Sprite>("UI/tab_active");
            Sprite inactiveSprite = Resources.Load<Sprite>("UI/tab_inactive");
            SetTabState(soundTabBg, soundTabText, activePage == soundPage, activeSprite, inactiveSprite);
            SetTabState(controlsTabBg, controlsTabText, activePage == controlsPage, activeSprite, inactiveSprite);
            SetTabState(graphicsTabBg, graphicsTabText, activePage == graphicsPage, activeSprite, inactiveSprite);
        }

        private static void SetTabState(Image bg, Text text, bool isSelected, Sprite activeSprite, Sprite inactiveSprite)
        {
            if (bg == null || text == null) return;
            bg.sprite = isSelected ? activeSprite : inactiveSprite;
            bg.type = Image.Type.Sliced;
            bg.color = isSelected ? Color.white : new Color(0.85f, 0.85f, 0.85f, 0.95f);
            text.color = isSelected ? new Color(1f, 0.97f, 0.84f) : new Color(0.74f, 0.70f, 0.62f, 0.85f);
        }

        private static void SetLangButtonState(Image bg, Text text, bool isSelected, string caption, Sprite activeSprite, Sprite inactiveSprite)
        {
            if (bg == null || text == null) return;
            text.text = caption;
            bg.sprite = isSelected ? activeSprite : inactiveSprite;
            bg.type = Image.Type.Sliced;
            bg.color = isSelected ? Color.white : new Color(0.85f, 0.85f, 0.85f, 0.9f);
            text.color = isSelected ? new Color(1f, 0.97f, 0.84f) : new Color(0.74f, 0.70f, 0.62f, 0.85f);
        }

        private void AddCameraStyleSwitch(float row)
        {
            Transform parent = currentPage ?? picture;
            Rect box = new Rect(ActionButtonBox.x, row - ActionButtonBox.height / 2f, ActionButtonBox.width, ActionButtonBox.height);
            cameraLabelText = new GameObject("CameraStyleLabel").AddComponent<Text>();
            cameraLabelText.transform.SetParent(parent, false);
            cameraLabelText.font = MenuStyle.Font;
            cameraLabelText.fontStyle = FontStyle.Bold;
            cameraLabelText.alignment = TextAnchor.MiddleLeft;
            cameraLabelText.color = LabelGold;
            cameraLabelText.text = Localization.T("settings_camera");
            cameraLabelText.raycastTarget = false;
            cameraLabelText.resizeTextForBestFit = true;
            cameraLabelText.resizeTextMinSize = 8;
            cameraLabelText.resizeTextMaxSize = 60;
            var outline = cameraLabelText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.06f, 0.02f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            MenuStyle.PlaceOnPicture(cameraLabelText.rectTransform, new Rect(LabelLeft, row - LabelHalf, box.xMin - 30f - LabelLeft, 2f * LabelHalf));

            Button button = MenuStyle.CreatePlateButtonOnPicture(parent, string.Empty, box, () =>
            {
                GameSettings.CameraStyle = GameSettings.CameraStyle == 0 ? 1 : 0;
                UpdateCameraStyleText();
                Changed?.Invoke();
            });
            cameraStyleButton = button;
            cameraStyleText = button.GetComponentInChildren<Text>();
            cameraStyleText.fontSize = 24;
            cameraStyleText.resizeTextForBestFit = true;
            cameraStyleText.resizeTextMinSize = 10;
            cameraStyleText.resizeTextMaxSize = 28;
            cameraStyleText.color = MenuStyle.GoldCaption;

            UpdateCameraStyleText();
        }

        private void UpdateCameraStyleText()
        {
            if (cameraStyleText != null)
            {
                cameraStyleText.text = GameSettings.CameraStyle == 0
                    ? Localization.T("settings_phone")
                    : Localization.T("settings_tablet");
            }
        }

        private static string Percent(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
