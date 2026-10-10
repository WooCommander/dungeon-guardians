using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // Player settings from the settings screen, kept in PlayerPrefs. Slider settings are stored as the slider position
    // (0..1); the properties below turn them into what the game uses.
    public static class GameSettings
    {
        private const string MusicKey = "settings_music";
        private const string SoundKey = "settings_sound";
        private const string VibrationKey = "settings_vibration";
        private const string ButtonSizeKey = "settings_button_size";
        private const string ButtonOpacityKey = "settings_button_opacity";
        private const string CameraStyleKey = "settings_camera_style";
        private const string TiltControlKey = "settings_tilt_control";
        private const string TiltCalibrationKey = "settings_tilt_calibration";
        private const string TiltVerticalCalibrationKey = "settings_tilt_vertical_calibration";
        private const string TiltSensitivityKey = "settings_tilt_sensitivity";

        // Defaults as on the settings concept; buttons start at their normal size.
        public const float DefaultMusic = 0.6f;
        public const float DefaultSound = 0.8f;
        public const float DefaultButtonSize = 0.5f;
        // The buttons lie over the level, so by default the level shows through them a little.
        public const float DefaultButtonOpacity = 0.7f;
        public const int DefaultCameraRows = 7;
        public const int OverviewCameraRows = 11;

        private const float MinButtonScale = 0.6f;
        private const float MaxButtonScale = 1.4f;
        private const float MinButtonOpacity = 0.2f;
        public const float DefaultTiltSensitivity = 0.22f;

        // 0 = Close (7 rows), 1 = Overview (11 rows)
        public static int CameraStyle
        {
            get => PlayerPrefs.GetInt(CameraStyleKey, 0);
            set => PlayerPrefs.SetInt(CameraStyleKey, Mathf.Clamp(value, 0, 1));
        }

        public static int MobileRows => CameraStyle == 0 ? DefaultCameraRows : OverviewCameraRows;

        public static bool TiltControl
        {
            get => PlayerPrefs.GetInt(TiltControlKey, 0) != 0;
            set => PlayerPrefs.SetInt(TiltControlKey, value ? 1 : 0);
        }

        public static float TiltCalibration
        {
            get => PlayerPrefs.GetFloat(TiltCalibrationKey, 0f);
            set => PlayerPrefs.SetFloat(TiltCalibrationKey, Mathf.Clamp(value, -1f, 1f));
        }

        public static float TiltVerticalCalibration
        {
            get => PlayerPrefs.GetFloat(TiltVerticalCalibrationKey, 0f);
            set => PlayerPrefs.SetFloat(TiltVerticalCalibrationKey, Mathf.Clamp(value, -1f, 1f));
        }

        public static float TiltSensitivity
        {
            get => PlayerPrefs.GetFloat(TiltSensitivityKey, DefaultTiltSensitivity);
            set => PlayerPrefs.SetFloat(TiltSensitivityKey, Mathf.Clamp(value, 0.08f, 0.5f));
        }

        public static float Music
        {
            get => PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
            set => PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
        }

        public static float Sound
        {
            get => PlayerPrefs.GetFloat(SoundKey, DefaultSound);
            set => PlayerPrefs.SetFloat(SoundKey, Mathf.Clamp01(value));
        }

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) != 0;
            set => PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
        }

        public static float ButtonSize
        {
            get => PlayerPrefs.GetFloat(ButtonSizeKey, DefaultButtonSize);
            set => PlayerPrefs.SetFloat(ButtonSizeKey, Mathf.Clamp01(value));
        }

        public static float ButtonOpacity
        {
            get => PlayerPrefs.GetFloat(ButtonOpacityKey, DefaultButtonOpacity);
            set => PlayerPrefs.SetFloat(ButtonOpacityKey, Mathf.Clamp01(value));
        }

        // The middle of the size slider is the normal size.
        public static float ButtonScale => Mathf.Lerp(MinButtonScale, MaxButtonScale, ButtonSize);
        public static float ButtonAlpha => Mathf.Lerp(MinButtonOpacity, 1f, ButtonOpacity);

#if UNITY_EDITOR
        private static bool? forceTouchInEditor;
        public static bool ForceTouchInEditor
        {
            get => forceTouchInEditor ?? true;
            set => forceTouchInEditor = value;
        }

        public static bool TouchControlsVisible => Application.isMobilePlatform || ForceTouchInEditor;
#else
        public static bool TouchControlsVisible => Application.isMobilePlatform;
#endif

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        // A short buzz on phones, when vibration is on.
        public static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Vibration)
            {
                Handheld.Vibrate();
            }
#endif
        }
    }
}
