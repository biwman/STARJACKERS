using UnityEngine;

public static class MobilePerformanceSettings
{
    public const int AndroidTargetFrameRate = 45;
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
