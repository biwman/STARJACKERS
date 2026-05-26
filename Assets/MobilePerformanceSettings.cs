using UnityEngine;

public static class MobilePerformanceSettings
{
    public const int AndroidTargetFrameRate = 60;
    public const float BalancedRenderScale = 0.85f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = AndroidTargetFrameRate;
#endif
    }
}
