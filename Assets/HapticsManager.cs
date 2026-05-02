using UnityEngine;

public static class HapticsManager
{
    const float DefaultMinInterval = 0.06f;
    const float HeavyMinInterval = 0.16f;

    static float nextAllowedTime;

    public static void PlayShieldHit()
    {
        PlayOneShot(28, 115, DefaultMinInterval);
    }

    public static void PlayHpHit()
    {
        PlayOneShot(45, 190, DefaultMinInterval);
    }

    public static void PlayDeath()
    {
        PlayOneShot(95, 255, HeavyMinInterval);
    }

    public static void PlayBatteringImpact()
    {
        PlayOneShot(70, 240, HeavyMinInterval);
    }

    static void PlayOneShot(long durationMs, int amplitude, float minInterval)
    {
        if (!RoomSettings.AreHapticsEnabled())
            return;

        if (Time.unscaledTime < nextAllowedTime)
            return;

        nextAllowedTime = Time.unscaledTime + Mathf.Max(0.01f, minInterval);

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            if (currentActivity == null)
                return;

            using AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                return;

            using AndroidJavaClass versionClass = new AndroidJavaClass("android.os.Build$VERSION");
            int sdkInt = versionClass.GetStatic<int>("SDK_INT");
            long safeDuration = durationMs < 1L ? 1L : durationMs;
            int safeAmplitude = Mathf.Clamp(amplitude, 1, 255);
            if (sdkInt >= 26)
            {
                using AndroidJavaClass vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                using AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                    "createOneShot",
                    safeDuration,
                    safeAmplitude);
                vibrator.Call("vibrate", effect);
            }
            else
            {
                vibrator.Call("vibrate", safeDuration);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Haptics failed: " + ex.Message);
        }
#endif
    }
}
