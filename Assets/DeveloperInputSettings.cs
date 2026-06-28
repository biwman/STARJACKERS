using UnityEngine;

public static class DeveloperInputSettings
{
    const string PcTouchJoystickTestModeKey = "starjackers.dev.pc_touch_joystick_test_mode";

    public static bool PcTouchJoystickTestModeEnabled
    {
        get => PlayerPrefs.GetInt(PcTouchJoystickTestModeKey, 0) != 0;
        set
        {
            PlayerPrefs.SetInt(PcTouchJoystickTestModeKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
