using System.Reflection;
using UnityEngine;

public enum MobilePerformanceProfile
{
    Low,
    Balanced,
    High
}

public static class MobilePerformanceSettings
{
    public const int AndroidLowTargetFrameRate = 40;
    public const int AndroidBalancedTargetFrameRate = 60;
    public const int AndroidHighTargetFrameRate = 60;

    public const int AndroidTargetFrameRate = AndroidBalancedTargetFrameRate;
    public const float LowRenderScale = 0.75f;
    public const float BalancedRenderScale = 0.82f;
    public const float HighRenderScale = 0.9f;
    public const float ReducedVfxFrameInterval = 0.05f;

    const string ProfileOverridePrefsKey = "mobile_performance_profile";

    public static MobilePerformanceProfile CurrentProfile { get; private set; } = MobilePerformanceProfile.Balanced;
    public static int CurrentTargetFrameRate { get; private set; } = AndroidBalancedTargetFrameRate;
    public static float CurrentRenderScale { get; private set; } = BalancedRenderScale;

    public static bool UseReducedVfx
    {
        get
        {
            if (!Application.isMobilePlatform)
                return false;

            return CurrentProfile == MobilePerformanceProfile.Low ||
                CurrentProfile == MobilePerformanceProfile.Balanced;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        ApplyProfile(ResolveProfile());
#endif
    }

    public static void ApplyProfile(MobilePerformanceProfile profile)
    {
        ProfileSettings settings = GetSettings(profile);

        CurrentProfile = profile;
        CurrentTargetFrameRate = settings.TargetFrameRate;
        CurrentRenderScale = settings.RenderScale;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = settings.TargetFrameRate;
        ApplyRenderScale(settings.RenderScale);
        LogProfile(settings);
    }

    static MobilePerformanceProfile ResolveProfile()
    {
        MobilePerformanceProfile overriddenProfile;
        if (TryGetOverrideProfile(out overriddenProfile))
            return overriddenProfile;

        return DetectProfile();
    }

    static bool TryGetOverrideProfile(out MobilePerformanceProfile profile)
    {
        profile = MobilePerformanceProfile.Balanced;

        string value = PlayerPrefs.GetString(ProfileOverridePrefsKey, string.Empty);
        if (string.IsNullOrEmpty(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "low":
                profile = MobilePerformanceProfile.Low;
                return true;
            case "balanced":
                profile = MobilePerformanceProfile.Balanced;
                return true;
            case "high":
                profile = MobilePerformanceProfile.High;
                return true;
            default:
                LogInvalidOverride(value);
                return false;
        }
    }

    static MobilePerformanceProfile DetectProfile()
    {
        int systemMemoryMb = SystemInfo.systemMemorySize;
        int processorCount = SystemInfo.processorCount;
        int graphicsMemoryMb = SystemInfo.graphicsMemorySize;

        bool hasMemorySignal = systemMemoryMb > 0;
        bool hasProcessorSignal = processorCount > 0;
        bool hasGraphicsMemorySignal = graphicsMemoryMb > 0;

        if (!hasMemorySignal && !hasProcessorSignal && !hasGraphicsMemorySignal)
            return MobilePerformanceProfile.Balanced;

        if ((hasMemorySignal && systemMemoryMb < 3500) ||
            (hasProcessorSignal && processorCount <= 4) ||
            (hasGraphicsMemorySignal && graphicsMemoryMb < 1024))
        {
            return MobilePerformanceProfile.Low;
        }

        if ((hasMemorySignal && systemMemoryMb < 5500) ||
            (hasProcessorSignal && processorCount <= 6) ||
            (hasGraphicsMemorySignal && graphicsMemoryMb < 2048))
        {
            return MobilePerformanceProfile.Balanced;
        }

        return MobilePerformanceProfile.High;
    }

    static ProfileSettings GetSettings(MobilePerformanceProfile profile)
    {
        switch (profile)
        {
            case MobilePerformanceProfile.Low:
                return new ProfileSettings(AndroidLowTargetFrameRate, LowRenderScale);
            case MobilePerformanceProfile.High:
                return new ProfileSettings(AndroidHighTargetFrameRate, HighRenderScale);
            default:
                return new ProfileSettings(AndroidBalancedTargetFrameRate, BalancedRenderScale);
        }
    }

    static void ApplyRenderScale(float scale)
    {
        object pipelineAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (pipelineAsset == null)
            pipelineAsset = QualitySettings.renderPipeline;
        if (pipelineAsset == null)
            return;

        PropertyInfo renderScaleProperty = pipelineAsset.GetType().GetProperty("renderScale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (renderScaleProperty == null || !renderScaleProperty.CanWrite)
            return;

        renderScaleProperty.SetValue(pipelineAsset, Mathf.Clamp(scale, 0.55f, 1f), null);
    }

    static void LogProfile(ProfileSettings settings)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log(
            $"Mobile performance profile: {CurrentProfile}, target FPS: {settings.TargetFrameRate}, " +
            $"render scale: {settings.RenderScale:0.##}, RAM: {SystemInfo.systemMemorySize} MB, " +
            $"CPU cores: {SystemInfo.processorCount}, GPU memory: {SystemInfo.graphicsMemorySize} MB");
#endif
    }

    static void LogInvalidOverride(string value)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.LogWarning($"Ignoring unknown mobile performance profile override '{value}'. Use low, balanced, or high.");
#endif
    }

    struct ProfileSettings
    {
        public readonly int TargetFrameRate;
        public readonly float RenderScale;

        public ProfileSettings(int targetFrameRate, float renderScale)
        {
            TargetFrameRate = targetFrameRate;
            RenderScale = renderScale;
        }
    }
}

public static class Physics2DNonAllocQuery
{
    const int DefaultOverlapCapacity = 96;
    const int MaxOverlapCapacity = 2048;

    static Collider2D[] overlapResults;
    static int previousOverlapCount;

    public static int OverlapCircle(Vector2 point, float radius, out Collider2D[] results)
    {
        if (overlapResults == null)
            overlapResults = new Collider2D[DefaultOverlapCapacity];

        int count = Query(point, radius);
        while (count == overlapResults.Length && overlapResults.Length < MaxOverlapCapacity)
        {
            int newSize = Mathf.Min(overlapResults.Length * 2, MaxOverlapCapacity);
            System.Array.Resize(ref overlapResults, newSize);
            count = Query(point, radius);
        }

        if (previousOverlapCount > count)
            System.Array.Clear(overlapResults, count, previousOverlapCount - count);

        previousOverlapCount = count;
        results = overlapResults;
        return count;
    }

    static int Query(Vector2 point, float radius)
    {
#pragma warning disable CS0618
        return Physics2D.OverlapCircleNonAlloc(point, radius, overlapResults);
#pragma warning restore CS0618
    }
}
