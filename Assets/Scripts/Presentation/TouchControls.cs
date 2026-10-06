using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // Whether the on-screen d-pad and dig buttons are shown. By default only on phones and tablets: on a PC the game
    // is played with the keyboard, and the strip would only take room from the level. The settings screen can force
    // them on or off; the choice is kept in PlayerPrefs.
    public static class TouchControls
    {
        public enum Mode
        {
            Auto,
            On,
            Off
        }

        private const string PrefsKey = "touch_controls";

        public static Mode Current
        {
            get => (Mode)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, (int)Mode.Auto), 0, 2);
            set
            {
                PlayerPrefs.SetInt(PrefsKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        // The Device Simulator reports a mobile platform too, so the controls show there in the editor.
        public static bool Visible => Current == Mode.On || (Current == Mode.Auto && Application.isMobilePlatform);

        public static string Label(Mode mode)
        {
            switch (mode)
            {
                case Mode.On:
                    return "ВКЛ";
                case Mode.Off:
                    return "ВЫКЛ";
                default:
                    return "АВТО";
            }
        }
    }
}
