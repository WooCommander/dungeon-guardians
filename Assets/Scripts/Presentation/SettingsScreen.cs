using System;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGuardians.Presentation
{
    // The settings screen as painted on map-images/settings.png. The picture (tools/cut_settings.py) has its slider
    // knobs, fills and percentages removed; live sliders, the vibration switch and pressable buttons are laid over
    // their painted places. Changes are saved at once and reported through Changed.
    public sealed class SettingsScreen : MonoBehaviour
    {
        // Picture pixels: slider tracks, the knob's travel, the percentage boxes and the switch.
        private const float TrackLeft = 737f;
        private const float TrackRight = 1172f;
        private const float KnobHalf = 27f;
        private const float FillInset = 4f;
        private const float FillTop = 10f;
        private const float FillBottom = 11f;
        private const float KnobTravelLeft = 750f;
        private const float KnobTravelRight = 1160f;
        private const float ValueLeft = 1207f;
        private const float ValueRight = 1323f;
        private const float ValueHalf = 19f;
        private const float MusicRow = 241f;
        private const float SoundRow = 331f;
        private const float ButtonSizeRow = 518f;
        private const float ButtonOpacityRow = 611f;
        private const float CameraStyleRow = 688f;
        private static readonly Rect ToggleBox = new Rect(1205f, 393f, 122f, 60f);
        private static readonly Color ValueColor = new Color(1f, 0.9f, 0.72f);

        private Transform picture;
        private Slider music;
        private Slider sound;
        private Slider buttonSize;
        private Slider buttonOpacity;
        private Image vibration;
        private Sprite vibrationOn;
        private Sprite vibrationOff;
        private Button cameraStyleButton;
        private Text cameraStyleText;
        private GameObject confirmModal;
        private Text toastText;

        public event Action Changed;
        public event Action ResetProgress;

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
            Refresh();
            if (confirmModal != null)
            {
                confirmModal.SetActive(false);
            }
            if (toastText != null)
            {
                toastText.gameObject.SetActive(false);
            }
            gameObject.SetActive(true);
        }

        public void Close()
        {
            GameSettings.Save();
            gameObject.SetActive(false);
        }

        private void Build()
        {
            // A blurred copy fills the screen around the picture, which is shown whole so no row is ever cut off.
            Image backing = MenuStyle.CreatePicture(transform, "Backgrounds/settings_blur", AspectRatioFitter.AspectMode.EnvelopeParent);
            backing.raycastTarget = true;
            picture = MenuStyle.CreatePicture(transform, "Backgrounds/settings", AspectRatioFitter.AspectMode.FitInParent).transform;

            music = AddSlider(MusicRow, value => GameSettings.Music = value, value => Percent(value));
            sound = AddSlider(SoundRow, value => GameSettings.Sound = value, value => Percent(value));
            buttonSize = AddSlider(ButtonSizeRow, value => GameSettings.ButtonSize = value, _ => Percent(GameSettings.ButtonScale));
            buttonOpacity = AddSlider(ButtonOpacityRow, value => GameSettings.ButtonOpacity = value, _ => Percent(GameSettings.ButtonAlpha));
            AddVibrationSwitch();
            AddCameraStyleSwitch(CameraStyleRow);

            MenuStyle.AddPictureButton(picture, "settings_back", new Rect(298f, 105f, 97f, 80f), "<", Close);
            MenuStyle.AddPictureButton(picture, "settings_done", new Rect(835f, 763f, 435f, 112f), "ГОТОВО", Close);
            MenuStyle.AddPictureButton(picture, "settings_reset", new Rect(405f, 772f, 390f, 96f), "НАЧАТЬ ЗАНОВО", ShowConfirmModal);

            BuildToastNotification();
            BuildConfirmModal();
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
            UpdateCameraStyleText();
        }

        private void ShowConfirmModal()
        {
            if (confirmModal != null)
            {
                confirmModal.SetActive(true);
            }
        }

        private void HideConfirmModal()
        {
            if (confirmModal != null)
            {
                confirmModal.SetActive(false);
            }
        }

        // "Start over": progress is wiped, every setting goes back to its default and the story is shown again.
        private void ExecuteStartOver()
        {
            HideConfirmModal();
            GameSettings.Music = GameSettings.DefaultMusic;
            GameSettings.Sound = GameSettings.DefaultSound;
            GameSettings.Vibration = true;
            GameSettings.ButtonSize = GameSettings.DefaultButtonSize;
            GameSettings.ButtonOpacity = GameSettings.DefaultButtonOpacity;
            GameSettings.CameraStyle = 0;
            GameSettings.Save();
            // A fresh start tells the legend again.
            StoryScreen.Seen = false;
            Refresh();
            ResetProgress?.Invoke();
            ShowToast("✓ ПРОГРЕСС И НАСТРОЙКИ СБРОШЕНЫ");
        }

        private void BuildToastNotification()
        {
            var toastObj = new GameObject("ToastNotification");
            toastObj.transform.SetParent(picture, false);
            var bg = toastObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.4f, 0.2f, 0.92f);
            MenuStyle.PlaceOnPicture(bg.rectTransform, new Rect(520f, 690f, 630f, 50f));

            var outline = toastObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 1f, 0.6f, 0.6f);
            outline.effectDistance = new Vector2(2f, 2f);

            toastText = MenuStyle.AddLabel(toastObj.transform, "✓ ПРОГРЕСС СБРОШЕН", 20);
            toastText.fontStyle = FontStyle.Bold;
            toastText.color = Color.white;
            toastText.alignment = TextAnchor.MiddleCenter;
            MenuStyle.Stretch(toastText.rectTransform);

            toastObj.SetActive(false);
        }

        private void ShowToast(string message)
        {
            if (toastText != null)
            {
                toastText.text = message;
                toastText.transform.parent.gameObject.SetActive(true);
                CancelInvoke(nameof(HideToast));
                Invoke(nameof(HideToast), 2.5f);
            }
        }

        private void HideToast()
        {
            if (toastText != null && toastText.transform.parent != null)
            {
                toastText.transform.parent.gameObject.SetActive(false);
            }
        }

        private void BuildConfirmModal()
        {
            confirmModal = new GameObject("ConfirmModal", typeof(RectTransform));
            confirmModal.transform.SetParent(transform, false);
            MenuStyle.Stretch((RectTransform)confirmModal.transform);

            // Scrim overlay
            var scrim = confirmModal.AddComponent<Image>();
            scrim.color = new Color(0.04f, 0.05f, 0.07f, 0.85f);
            scrim.raycastTarget = true;

            // Centered dialog box
            var box = new GameObject("DialogBox").AddComponent<Image>();
            box.transform.SetParent(confirmModal.transform, false);
            box.color = new Color(0.11f, 0.13f, 0.17f, 0.98f);
            var boxRect = box.rectTransform;
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(760f, 380f);
            boxRect.anchoredPosition = Vector2.zero;

            var border = box.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(1f, 0.84f, 0.42f, 0.75f);
            border.effectDistance = new Vector2(3f, 3f);

            // Title
            var title = MenuStyle.AddLabel(box.transform, "⚠️ СБРОСИТЬ ВЕСЬ ПРОГРЕСС?", 28);
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(1f, 0.85f, 0.45f);
            title.alignment = TextAnchor.UpperCenter;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(-40f, 50f);
            titleRect.anchoredPosition = new Vector2(0f, -30f);

            // Description
            var desc = MenuStyle.AddLabel(
                box.transform,
                "Вы действительно хотите начать игру заново?\n\nВсе пройденные уровни, рекорды времени и заработанные звёзды будут удалены безвозвратно.",
                22
            );
            desc.color = new Color(0.88f, 0.88f, 0.92f);
            desc.alignment = TextAnchor.MiddleCenter;
            var descRect = desc.rectTransform;
            descRect.anchorMin = new Vector2(0f, 0.35f);
            descRect.anchorMax = new Vector2(1f, 0.82f);
            descRect.offsetMin = new Vector2(35f, 0f);
            descRect.offsetMax = new Vector2(-35f, 0f);

            // Cancel Button
            var cancelBtn = MenuStyle.CreatePlateButton(box.transform, "ОТМЕНА", new Vector2(240f, 65f), HideConfirmModal);
            var cancelRect = (RectTransform)cancelBtn.transform;
            cancelRect.anchorMin = cancelRect.anchorMax = new Vector2(0.3f, 0.18f);
            cancelRect.anchoredPosition = Vector2.zero;

            // Confirm Button (Red/Gold styled)
            var confirmBtn = MenuStyle.CreatePlateButton(box.transform, "ДА, СБРОСИТЬ", new Vector2(280f, 65f), ExecuteStartOver);
            var confirmRect = (RectTransform)confirmBtn.transform;
            confirmRect.anchorMin = confirmRect.anchorMax = new Vector2(0.7f, 0.18f);
            confirmRect.anchoredPosition = Vector2.zero;
            var confirmImg = confirmBtn.GetComponent<Image>();
            if (confirmImg != null)
            {
                confirmImg.color = new Color(0.55f, 0.18f, 0.14f, 0.95f);
            }

            confirmModal.SetActive(false);
        }

        private Slider AddSlider(float row, Action<float> store, Func<float, string> format)
        {
            float width = TrackRight - TrackLeft;
            float height = 2f * KnobHalf;
            var root = new GameObject("Slider").AddComponent<Image>();
            root.transform.SetParent(picture, false);
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
            valueText.transform.SetParent(picture, false);
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
            vibrationOn = Resources.Load<Sprite>("UI/settings_toggle_on");
            vibrationOff = Resources.Load<Sprite>("UI/settings_toggle_off");
            vibration = new GameObject("Vibration").AddComponent<Image>();
            vibration.transform.SetParent(picture, false);
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

        private void AddCameraStyleSwitch(float row)
        {
            var label = new GameObject("CameraStyleLabel").AddComponent<Text>();
            label.transform.SetParent(picture, false);
            label.font = MenuStyle.Font;
            label.fontStyle = FontStyle.Bold;
            label.fontSize = 28;
            label.alignment = TextAnchor.MiddleRight;
            label.color = new Color(1f, 0.85f, 0.65f);
            label.text = "МАСШТАБ КАМЕРЫ";
            label.raycastTarget = false;
            MenuStyle.PlaceOnPicture(label.rectTransform, new Rect(260f, row - 22f, 450f, 44f));

            var buttonImage = new GameObject("CameraStyleButton").AddComponent<Image>();
            buttonImage.transform.SetParent(picture, false);
            buttonImage.color = new Color(0.18f, 0.14f, 0.11f, 0.95f);
            MenuStyle.PlaceOnPicture(buttonImage.rectTransform, new Rect(TrackLeft, row - 22f, TrackRight - TrackLeft + (ValueRight - ValueLeft) + 40f, 46f));

            cameraStyleText = new GameObject("Text").AddComponent<Text>();
            cameraStyleText.transform.SetParent(buttonImage.transform, false);
            cameraStyleText.font = MenuStyle.Font;
            cameraStyleText.fontStyle = FontStyle.Bold;
            cameraStyleText.fontSize = 20;
            cameraStyleText.alignment = TextAnchor.MiddleCenter;
            cameraStyleText.color = ValueColor;
            cameraStyleText.raycastTarget = false;
            MenuStyle.Stretch(cameraStyleText.rectTransform);

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
                    ? "КРУПНЫЙ ПЛАН (7 РЯДОВ) — ТЕЛЕФОН"
                    : "ОБЗОРНЫЙ ПЛАН (11 РЯДОВ) — ПЛАНШЕТ";
            }
        }

        private static string Percent(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
