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

            MenuStyle.AddPictureButton(picture, "settings_back", new Rect(298f, 105f, 97f, 80f), "<", Close);
            MenuStyle.AddPictureButton(picture, "settings_done", new Rect(835f, 763f, 435f, 112f), "ГОТОВО", Close);
            MenuStyle.AddPictureButton(picture, "settings_reset", new Rect(405f, 772f, 390f, 96f), "НАЧАТЬ ЗАНОВО", StartOver);
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
        }

        // "Start over": progress is wiped, every setting goes back to its default and the story is shown again.
        private void StartOver()
        {
            GameSettings.Music = GameSettings.DefaultMusic;
            GameSettings.Sound = GameSettings.DefaultSound;
            GameSettings.Vibration = true;
            GameSettings.ButtonSize = GameSettings.DefaultButtonSize;
            GameSettings.ButtonOpacity = GameSettings.DefaultButtonOpacity;
            GameSettings.Save();
            // A fresh start tells the legend again.
            StoryScreen.Seen = false;
            Refresh();
            ResetProgress?.Invoke();
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

        private static string Percent(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
