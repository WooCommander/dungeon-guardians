using System;
using DungeonGuardians.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    // The settings screen as painted on map-images/settings.png. The picture (tools/cut_settings.py) has its slider
    // knobs, fills and percentages removed; live sliders, the vibration switch and pressable buttons are laid over
    // their painted places. Changes are saved at once and reported through Changed.
    public sealed class SettingsScreen : MonoBehaviour
    {
        // Picture reference dimensions (1672x941) matching tools/cut_settings.py
        private const float MusicRow = 241f;
        private const float SoundRow = 331f;
        private const float ButtonOpacityRow = 611f;

        private const float TrackLeft = 737f;
        private const float TrackRight = 1172f;
        private const float TrackHalf = 15f;
        private const float KnobHalf = 27f;
        private const float KnobTravelLeft = TrackLeft + 12f;
        private const float KnobTravelRight = TrackRight - 12f;
        private const float FillInset = 4f;
        private const float FillTop = 9f;
        private const float FillBottom = 10f;

        private const float ValueLeft = 1207f;
        private const float ValueRight = 1323f;
        private const float ValueHalf = 19f;

        // Row labels: where dynamic text starts right after painted row icons.
        private const float LabelLeft = 430f;
        private const float LabelHalf = 22f;
        private const float CameraChevronLeft = 1282f;
        private static readonly Rect CameraBox = new Rect(948f, 673f, 386f, 64f);
        private static readonly Rect TiltBox = new Rect(948f, 393f, 386f, 64f);
        private static readonly Rect TabSoundBox = new Rect(140f, 200f, 450f, 74f);
        private static readonly Rect TabControlsBox = new Rect(610f, 200f, 450f, 74f);
        private static readonly Rect TabGraphicsBox = new Rect(1080f, 200f, 450f, 74f);
        private static readonly Rect SectionBackdropBox = new Rect(120f, 300f, 1430f, 415f);

        private static readonly Rect ToggleBox = new Rect(1205f, 393f, 122f, 60f);
        private static readonly Color ValueColor = new Color(1f, 0.9f, 0.72f);
        private static readonly Color LabelGold = new Color(1f, 0.88f, 0.58f);

        private Transform picture;
        private Transform currentPage;
        private GameObject generalPage;
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
            // What "ОТМЕНА" goes back to.
            opened = (GameSettings.Music, GameSettings.Sound, GameSettings.Vibration, GameSettings.ButtonSize,
                GameSettings.ButtonOpacity, GameSettings.CameraStyle, GameSettings.TiltControl, GameSettings.TiltCalibration,
                GameSettings.TiltVerticalCalibration, Localization.SelectedPreference);
            SetPage(generalPage, "settings_header");
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

        // Puts every setting back as it was when the screen opened, and closes it.
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
            // A blurred copy fills the screen around the picture, which is shown whole so no row is ever cut off.
            Image backing = MenuStyle.CreatePicture(transform, "Backgrounds/settings_blur", AspectRatioFitter.AspectMode.EnvelopeParent);
            backing.raycastTarget = true;
            picture = MenuStyle.CreatePicture(transform, "Backgrounds/settings", AspectRatioFitter.AspectMode.FitInParent).transform;

            // Top Header: НАСТРОЙКИ / SETTINGS
            headerText = AddHeaderTitle();
            soundPage = CreatePage("SoundPage");
            controlsPage = CreatePage("ControlsPage");
            graphicsPage = CreatePage("GraphicsPage");
            AddTabs();
            currentPage = soundPage.transform;
            AddSectionBackdrop();

            // Interactive controls over their exact painted slots on the stone frame:
            // Row 1: Музыка / Music
            musicLabelText = AddRowLabel(370f, "settings_music");
            music = AddSlider(370f, value => GameSettings.Music = value, _ => Percent(GameSettings.Music));

            // Row 2: Звуки / Sound FX
            soundLabelText = AddRowLabel(505f, "settings_sound");
            sound = AddSlider(505f, value => GameSettings.Sound = value, _ => Percent(GameSettings.Sound));

            // Row 3: Вибрация / Vibration + Селектор языка
            currentPage = graphicsPage.transform;
            AddSectionBackdrop();
            AddLanguageSelector();
            AddCameraStyleSwitch(505f);

            // Row 4: Размер кнопок / Button Size
            currentPage = controlsPage.transform;
            AddSectionBackdrop();
            vibrationLabelText = AddRowLabel(370f, "settings_vibration");
            AddVibrationSwitch();

            // Row 5: Прозрачность кнопок / Button Opacity
            

            // Row 6: Масштаб камеры / Camera Zoom
            buttonSizeLabelText = AddRowLabel(505f, "settings_size");
            buttonSize = AddSlider(505f, value => GameSettings.ButtonSize = value, _ => Percent(GameSettings.ButtonScale));
            buttonOpacityLabelText = AddRowLabel(640f, "settings_opacity");
            buttonOpacity = AddSlider(640f, value => GameSettings.ButtonOpacity = value, _ => Percent(GameSettings.ButtonAlpha));
            AddTiltSwitch();

            // The bottom row, either side of the centre: "ОТМЕНА" / "ГОТОВО"
            cancelText = MenuStyle.AddQuietButton(picture, "settings_cancel", new Rect(380f, 763f, 435f, 112f), Localization.T("settings_cancel"), Cancel);
            doneText = MenuStyle.AddCaptionedButton(picture, "settings_done_blank", new Rect(855f, 763f, 435f, 112f), Localization.T("settings_done"), MenuStyle.GoldCaption, Close);
            EqualFontSize.Apply(picture.gameObject, 60, cancelText, doneText);
            SetPage(soundPage, "settings_sound_section");
        }

        private void AddSectionBackdrop()
        {
            var backdrop = new GameObject("SectionBackdrop").AddComponent<Image>();
            backdrop.transform.SetParent(currentPage, false);
            backdrop.color = new Color(0.02f, 0.018f, 0.014f, 0.86f);
            MenuStyle.PlaceOnPicture(backdrop.rectTransform, SectionBackdropBox);
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
            text.fontSize = 34;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 42;
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

        private Slider AddSlider(float row, Action<float> store, Func<float, string> format)
        {
            Transform parent = currentPage ?? picture;
            float width = TrackRight - TrackLeft;
            float height = 2f * KnobHalf;
            var root = new GameObject("Slider").AddComponent<Image>();
            root.transform.SetParent(parent, false);
            // Invisible, but catches taps anywhere along the track.
            root.color = Color.clear;
            MenuStyle.PlaceOnPicture(root.rectTransform, new Rect(TrackLeft, row - KnobHalf, width, height));

            // The slider drives the anchors of the fill and the knob within their own areas.
            RectTransform fillArea = AddArea(root.transform, "Fill Area",
                new Vector2(FillInset / width, (KnobHalf - FillBottom) / height),
                new Vector2(1f - FillInset / width, (KnobHalf + FillTop) / height));
            var fill = new GameObject("Fill").AddComponent<Image>();
            fill.transform.SetParent(fillArea, false);
            fill.raycastTarget = false;
            fill.sprite = Resources.Load<Sprite>("UI/settings_fill");
            fill.color = fill.sprite != null ? Color.white : new Color(1f, 0.7f, 0.2f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            MenuStyle.Stretch(fill.rectTransform);

            RectTransform knobArea = AddArea(root.transform, "Knob Area",
                new Vector2((KnobTravelLeft - TrackLeft) / width, 0f),
                new Vector2((KnobTravelRight - TrackLeft) / width, 1f));
            var knob = new GameObject("Knob").AddComponent<Image>();
            knob.transform.SetParent(knobArea, false);
            knob.sprite = Resources.Load<Sprite>("UI/settings_knob");
            knob.color = knob.sprite != null ? Color.white : new Color(0.3f, 0.9f, 1f);
            // The knob is as tall as the slider and square.
            knob.rectTransform.sizeDelta = Vector2.zero;
            var square = knob.gameObject.AddComponent<AspectRatioFitter>();
            square.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            square.aspectRatio = 1f;

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
            // Arrow keys would move a slider that still has focus after a drag.
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

        private void AddVibrationSwitch()
        {
            Transform parent = currentPage ?? picture;
            vibrationOn = Resources.Load<Sprite>("UI/settings_toggle_on");
            vibrationOff = Resources.Load<Sprite>("UI/settings_toggle_off");
            vibration = new GameObject("Vibration").AddComponent<Image>();
            vibration.transform.SetParent(parent, false);
            MenuStyle.PlaceOnPicture(vibration.rectTransform, ToggleBox);
            if (vibrationOn == null || vibrationOff == null)
            {
                vibration.color = new Color(0.2f, 0.7f, 0.7f);
            }

            MenuStyle.MakeButton(vibration, () =>
            {
                GameSettings.Vibration = !GameSettings.Vibration;
                vibration.sprite = GameSettings.Vibration ? vibrationOn : vibrationOff;
                // A buzz when it is switched on, so the player feels what it does.
                GameSettings.Vibrate();
                Changed?.Invoke();
            });
        }

        private void AddLanguageSelector()
        {
            (langAutoBg, langAutoText) = CreateLangButton("Lang_Auto", new Rect(737f, 397f, 137f, 52f), AppLanguage.Auto);
            (langRuBg, langRuText) = CreateLangButton("Lang_RU", new Rect(886f, 397f, 137f, 52f), AppLanguage.Russian);
            (langEnBg, langEnText) = CreateLangButton("Lang_EN", new Rect(1035f, 397f, 137f, 52f), AppLanguage.English);
        }

        private (Image bg, Text text) CreateLangButton(string name, Rect rect, AppLanguage lang)
        {
            Transform parent = currentPage ?? picture;
            var buttonImage = new GameObject(name).AddComponent<Image>();
            buttonImage.transform.SetParent(parent, false);
            buttonImage.sprite = Resources.Load<Sprite>("UI/settings_cancel");
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
            text.resizeTextMaxSize = 34;

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

            // Centered plaque area between the two crystals
            MenuStyle.PlaceOnPicture(title.rectTransform, new Rect(570f, 65f, 532f, 92f));
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
            Sprite activeSprite = Resources.Load<Sprite>("UI/settings_done_blank");
            Sprite inactiveSprite = Resources.Load<Sprite>("UI/settings_cancel");

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
            if (tiltLabelText != null) tiltLabelText.text = Localization.T("settings_tilt");
            if (tiltText != null) tiltText.text = Localization.T(GameSettings.TiltControl ? "settings_tilt_on" : "settings_tilt_off");
            if (cancelText != null) cancelText.text = Localization.T("settings_cancel");
            if (doneText != null) doneText.text = Localization.T("settings_done");
        }

        private void AddTiltSwitch()
        {
            tiltLabelText = AddRowLabel(423f, "settings_tilt");
            Button button = MenuStyle.CreatePlateButtonOnPicture(currentPage ?? picture, string.Empty, TiltBox, ToggleTiltControl);
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
            if (headerText != null) headerText.text = Localization.T(headerKey);
            UpdateTabs(page);
        }

        private void UpdateTabs(GameObject activePage)
        {
            Sprite activeSprite = Resources.Load<Sprite>("UI/settings_done_blank");
            Sprite inactiveSprite = Resources.Load<Sprite>("UI/settings_cancel");
            SetTabState(soundTabBg, soundTabText, activePage == soundPage, activeSprite, inactiveSprite);
            SetTabState(controlsTabBg, controlsTabText, activePage == controlsPage, activeSprite, inactiveSprite);
            SetTabState(graphicsTabBg, graphicsTabText, activePage == graphicsPage, activeSprite, inactiveSprite);
        }

        private static void SetTabState(Image bg, Text text, bool isSelected, Sprite activeSprite, Sprite inactiveSprite)
        {
            if (bg == null || text == null) return;
            bg.sprite = isSelected ? activeSprite : inactiveSprite;
            bg.color = isSelected ? Color.white : new Color(0.72f, 0.72f, 0.72f, 0.88f);
            text.color = isSelected ? MenuStyle.GoldCaption : MenuStyle.QuietCaption;
        }

        private static void SetLangButtonState(Image bg, Text text, bool isSelected, string caption, Sprite activeSprite, Sprite inactiveSprite)
        {
            if (bg == null || text == null) return;
            text.text = caption;
            if (isSelected)
            {
                bg.sprite = activeSprite;
                bg.color = Color.white;
                text.color = MenuStyle.GoldCaption;
            }
            else
            {
                bg.sprite = inactiveSprite;
                bg.color = new Color(0.75f, 0.75f, 0.75f, 0.85f);
                text.color = new Color(0.75f, 0.72f, 0.65f, 0.75f);
            }
        }

        // The painted row 6, which tools/settings_camera_row.ps1 gives a magnifier and clears of "Язык" and "Русский":
        // the label goes where "Язык" was, the current choice into the painted dropdown frame.
        private void AddCameraStyleSwitch(float row)
        {
            Transform parent = currentPage ?? picture;
            Rect box = new Rect(CameraBox.x, row - 32f, CameraBox.width, CameraBox.height);
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

            // The whole painted frame is the button; a tap switches between the two framings.
            var buttonImage = new GameObject("CameraStyleButton").AddComponent<Image>();
            buttonImage.transform.SetParent(parent, false);
            buttonImage.color = Color.clear;
            MenuStyle.PlaceOnPicture(buttonImage.rectTransform, box);

            cameraStyleText = new GameObject("CameraStyleValue").AddComponent<Text>();
            cameraStyleText.transform.SetParent(parent, false);
            cameraStyleText.font = MenuStyle.Font;
            cameraStyleText.fontStyle = FontStyle.Bold;
            cameraStyleText.alignment = TextAnchor.MiddleCenter;
            cameraStyleText.color = ValueColor;
            cameraStyleText.raycastTarget = false;
            cameraStyleText.resizeTextForBestFit = true;
            cameraStyleText.resizeTextMinSize = 8;
            cameraStyleText.resizeTextMaxSize = 60;
            // Between the frame's left edge and its painted chevron.
            MenuStyle.PlaceOnPicture(cameraStyleText.rectTransform,
                new Rect(box.xMin + 22f, row - ValueHalf, CameraChevronLeft - box.xMin - 34f, 2f * ValueHalf));

            cameraStyleButton = MenuStyle.MakeButton(buttonImage, () =>
            {
                GameSettings.CameraStyle = GameSettings.CameraStyle == 0 ? 1 : 0;
                UpdateCameraStyleText();
                Changed?.Invoke();
            });

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
