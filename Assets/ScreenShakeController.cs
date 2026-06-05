using UnityEngine;

public readonly struct ScreenShakeProfile
{
    public readonly float Amplitude;
    public readonly float Duration;
    public readonly float Frequency;
    public readonly float FalloffStartDistance;
    public readonly float MaxDistance;

    public ScreenShakeProfile(float amplitude, float duration, float frequency, float falloffStartDistance, float maxDistance)
    {
        Amplitude = amplitude;
        Duration = duration;
        Frequency = frequency;
        FalloffStartDistance = falloffStartDistance;
        MaxDistance = maxDistance;
    }

    public bool IsValid => Amplitude > 0f && Duration > 0f;
}

public static class ScreenShakeProfiles
{
    // Tune per-event shake strength and timing here.
    public static readonly ScreenShakeProfile SpaceBombExplosion = new ScreenShakeProfile(
        amplitude: 0.34f,
        duration: 0.62f,
        frequency: 34f,
        falloffStartDistance: 7.5f,
        maxDistance: 19f);

    public static readonly ScreenShakeProfile AsteroidShowerImpact = new ScreenShakeProfile(
        amplitude: 0.46f,
        duration: 0.54f,
        frequency: 28f,
        falloffStartDistance: 8.5f,
        maxDistance: 23f);

    public static readonly ScreenShakeProfile EnemyBoom = new ScreenShakeProfile(
        amplitude: 0.08f,
        duration: 0.2f,
        frequency: 32f,
        falloffStartDistance: 3.5f,
        maxDistance: 9f);
}

public static class ScreenShakeController
{
    const int MaxActiveShakes = 8;
    const float CombinedAmplitudeMultiplier = 1.45f;

    struct ActiveShake
    {
        public ScreenShakeProfile Profile;
        public Vector2 WorldPosition;
        public float StartTime;
        public float Seed;
    }

    static readonly ActiveShake[] activeShakes = new ActiveShake[MaxActiveShakes];
    static int activeShakeCount;
    static int requestCounter;

    public static void Request(ScreenShakeProfile profile, Vector3 worldPosition)
    {
        if (!profile.IsValid || !RoomSettings.AreVisualEffectsEnabled())
            return;

        ActiveShake shake = new ActiveShake
        {
            Profile = profile,
            WorldPosition = worldPosition,
            StartTime = Time.unscaledTime,
            Seed = (++requestCounter * 37.17f) + worldPosition.x * 3.11f + worldPosition.y * 5.23f
        };

        if (activeShakeCount < activeShakes.Length)
        {
            activeShakes[activeShakeCount] = shake;
            activeShakeCount++;
            return;
        }

        activeShakes[FindWeakestShakeIndex()] = shake;
    }

    public static Vector3 GetOffset(Vector3 cameraBasePosition)
    {
        if (activeShakeCount == 0)
            return Vector3.zero;

        float now = Time.unscaledTime;
        Vector2 combined = Vector2.zero;
        float combinedLimit = 0f;

        for (int i = activeShakeCount - 1; i >= 0; i--)
        {
            ActiveShake shake = activeShakes[i];
            float elapsed = now - shake.StartTime;
            if (elapsed >= shake.Profile.Duration)
            {
                RemoveShakeAt(i);
                continue;
            }

            float timeT = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, shake.Profile.Duration));
            float envelope = Mathf.Pow(1f - timeT, 1.65f);
            float distanceMultiplier = GetDistanceMultiplier(shake.Profile, cameraBasePosition, shake.WorldPosition);
            float strength = shake.Profile.Amplitude * envelope * distanceMultiplier;
            if (strength <= 0.0001f)
                continue;

            float sampleTime = now * Mathf.Max(1f, shake.Profile.Frequency);
            float x = (Mathf.PerlinNoise(shake.Seed, sampleTime) * 2f) - 1f;
            float y = (Mathf.PerlinNoise(shake.Seed + 19.31f, sampleTime + 7.73f) * 2f) - 1f;
            Vector2 direction = new Vector2(x, y);
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            combined += direction * strength;
            combinedLimit = Mathf.Max(combinedLimit, shake.Profile.Amplitude * distanceMultiplier);
        }

        if (combinedLimit > 0f)
        {
            float maxMagnitude = combinedLimit * CombinedAmplitudeMultiplier;
            if (combined.sqrMagnitude > maxMagnitude * maxMagnitude)
                combined = combined.normalized * maxMagnitude;
        }

        return new Vector3(combined.x, combined.y, 0f);
    }

    static float GetDistanceMultiplier(ScreenShakeProfile profile, Vector3 cameraBasePosition, Vector2 shakeWorldPosition)
    {
        float maxDistance = Mathf.Max(0f, profile.MaxDistance);
        if (maxDistance <= 0.001f)
            return 1f;

        float distance = Vector2.Distance(cameraBasePosition, shakeWorldPosition);
        if (distance >= maxDistance)
            return 0f;

        float falloffStart = Mathf.Clamp(profile.FalloffStartDistance, 0f, maxDistance);
        if (distance <= falloffStart)
            return 1f;

        float t = Mathf.InverseLerp(falloffStart, maxDistance, distance);
        return 1f - Mathf.SmoothStep(0f, 1f, t);
    }

    static int FindWeakestShakeIndex()
    {
        float now = Time.unscaledTime;
        int weakestIndex = 0;
        float weakestStrength = float.MaxValue;
        for (int i = 0; i < activeShakes.Length; i++)
        {
            ActiveShake shake = activeShakes[i];
            float remaining = Mathf.Clamp01(1f - ((now - shake.StartTime) / Mathf.Max(0.001f, shake.Profile.Duration)));
            float strength = shake.Profile.Amplitude * remaining;
            if (strength < weakestStrength)
            {
                weakestStrength = strength;
                weakestIndex = i;
            }
        }

        return weakestIndex;
    }

    static void RemoveShakeAt(int index)
    {
        activeShakeCount--;
        if (index < activeShakeCount)
            activeShakes[index] = activeShakes[activeShakeCount];
    }
}

public struct DynamicCameraZoomProfile
{
    public float Multiplier;
    public float Duration;
    public float ZoomInSpeed;
    public float ZoomOutSpeed;
    public float FalloffStartDistance;
    public float MaxDistance;

    public DynamicCameraZoomProfile(float multiplier, float duration, float zoomInSpeed, float zoomOutSpeed, float falloffStartDistance, float maxDistance)
    {
        Multiplier = multiplier;
        Duration = duration;
        ZoomInSpeed = zoomInSpeed;
        ZoomOutSpeed = zoomOutSpeed;
        FalloffStartDistance = falloffStartDistance;
        MaxDistance = maxDistance;
    }

    public bool IsValid => Multiplier > 1f && Duration > 0f;

    public DynamicCameraZoomProfile WithMultiplier(float multiplier)
    {
        DynamicCameraZoomProfile copy = this;
        copy.Multiplier = Mathf.Max(1f, multiplier);
        return copy;
    }
}

public readonly struct DynamicCameraZoomState
{
    public readonly float Multiplier;
    public readonly float ZoomInSpeed;
    public readonly float ZoomOutSpeed;

    public DynamicCameraZoomState(float multiplier, float zoomInSpeed, float zoomOutSpeed)
    {
        Multiplier = multiplier;
        ZoomInSpeed = zoomInSpeed;
        ZoomOutSpeed = zoomOutSpeed;
    }
}

public static class DynamicCameraZoomProfiles
{
    public static readonly DynamicCameraZoomProfile SpaceBombFlight = new DynamicCameraZoomProfile(
        multiplier: 1.3f,
        duration: 7.9f,
        zoomInSpeed: 4.75f,
        zoomOutSpeed: 1.9f,
        falloffStartDistance: 8.5f,
        maxDistance: 22f);

    public static readonly DynamicCameraZoomProfile AsteroidShowerWarning = new DynamicCameraZoomProfile(
        multiplier: 1.16f,
        duration: 3.5f,
        zoomInSpeed: 3.25f,
        zoomOutSpeed: 1.7f,
        falloffStartDistance: 8f,
        maxDistance: 24f);

    public static readonly DynamicCameraZoomProfile HunterLanceLock = new DynamicCameraZoomProfile(
        multiplier: 1.14f,
        duration: 1.2f,
        zoomInSpeed: 3.5f,
        zoomOutSpeed: 1.8f,
        falloffStartDistance: 7f,
        maxDistance: 22f);

    public static readonly DynamicCameraZoomProfile CosmicWormDanger = new DynamicCameraZoomProfile(
        multiplier: 1.19f,
        duration: 2f,
        zoomInSpeed: 3.75f,
        zoomOutSpeed: 1.75f,
        falloffStartDistance: 10f,
        maxDistance: 30f);

    public static readonly DynamicCameraZoomProfile PirateBaseLaunch = new DynamicCameraZoomProfile(
        multiplier: 1.14f,
        duration: 3.4f,
        zoomInSpeed: 2.8f,
        zoomOutSpeed: 1.6f,
        falloffStartDistance: 11f,
        maxDistance: 32f);

    public static readonly DynamicCameraZoomProfile SuperBooster = new DynamicCameraZoomProfile(
        multiplier: 1.08f,
        duration: 2f,
        zoomInSpeed: 4f,
        zoomOutSpeed: 2.1f,
        falloffStartDistance: 0f,
        maxDistance: 0f);

    public static readonly DynamicCameraZoomProfile EndDisasterMeteor = new DynamicCameraZoomProfile(
        multiplier: 1.6f,
        duration: 0.35f,
        zoomInSpeed: 2.4f,
        zoomOutSpeed: 2.8f,
        falloffStartDistance: 0f,
        maxDistance: 0f);
}

public static class DynamicCameraZoomController
{
    const int MaxActiveZooms = 16;
    const float DefaultZoomInSpeed = 6f;
    const float DefaultZoomOutSpeed = 3.2f;

    struct ActiveZoom
    {
        public int Token;
        public DynamicCameraZoomProfile Profile;
        public Vector2 WorldPosition;
        public float EndTime;
        public bool Global;
    }

    static readonly ActiveZoom[] activeZooms = new ActiveZoom[MaxActiveZooms];
    static int activeZoomCount;
    static int requestCounter;

    public static int Request(DynamicCameraZoomProfile profile, Vector3 worldPosition)
    {
        return Request(profile, worldPosition, profile.Duration);
    }

    public static int Request(DynamicCameraZoomProfile profile, Vector3 worldPosition, float duration)
    {
        return RequestInternal(profile, worldPosition, duration, false);
    }

    public static int RequestGlobal(DynamicCameraZoomProfile profile)
    {
        return RequestGlobal(profile, profile.Duration);
    }

    public static int RequestGlobal(DynamicCameraZoomProfile profile, float duration)
    {
        return RequestInternal(profile, Vector3.zero, duration, true);
    }

    public static int Refresh(int token, DynamicCameraZoomProfile profile, Vector3 worldPosition, float duration)
    {
        return RefreshInternal(token, profile, worldPosition, duration, false);
    }

    public static int RefreshGlobal(int token, DynamicCameraZoomProfile profile, float duration)
    {
        return RefreshInternal(token, profile, Vector3.zero, duration, true);
    }

    public static void UpdateWorldPosition(int token, Vector3 worldPosition)
    {
        if (token <= 0)
            return;

        int index = FindTokenIndex(token);
        if (index < 0)
            return;

        ActiveZoom zoom = activeZooms[index];
        zoom.WorldPosition = worldPosition;
        activeZooms[index] = zoom;
    }

    public static void Cancel(int token)
    {
        if (token <= 0)
            return;

        int index = FindTokenIndex(token);
        if (index >= 0)
            RemoveZoomAt(index);
    }

    public static DynamicCameraZoomState GetState(Vector3 cameraBasePosition)
    {
        PruneExpired();

        if (!RoomSettings.AreVisualEffectsEnabled() || !RoomSettings.IsDynamicCameraZoomEnabled() || activeZoomCount == 0)
            return new DynamicCameraZoomState(1f, DefaultZoomInSpeed, DefaultZoomOutSpeed);

        float bestMultiplier = 1f;
        float bestZoomInSpeed = DefaultZoomInSpeed;
        float bestZoomOutSpeed = DefaultZoomOutSpeed;

        for (int i = 0; i < activeZoomCount; i++)
        {
            ActiveZoom zoom = activeZooms[i];
            float distanceMultiplier = zoom.Global ? 1f : GetDistanceMultiplier(zoom.Profile, cameraBasePosition, zoom.WorldPosition);
            if (distanceMultiplier <= 0f)
                continue;

            float multiplier = 1f + (Mathf.Max(1f, zoom.Profile.Multiplier) - 1f) * distanceMultiplier;
            if (multiplier <= bestMultiplier)
                continue;

            bestMultiplier = multiplier;
            bestZoomInSpeed = Mathf.Max(0.1f, zoom.Profile.ZoomInSpeed);
            bestZoomOutSpeed = Mathf.Max(0.1f, zoom.Profile.ZoomOutSpeed);
        }

        return new DynamicCameraZoomState(bestMultiplier, bestZoomInSpeed, bestZoomOutSpeed);
    }

    static int RequestInternal(DynamicCameraZoomProfile profile, Vector3 worldPosition, float duration, bool global)
    {
        if (!ShouldAccept(profile, duration))
            return 0;

        PruneExpired();

        ActiveZoom zoom = new ActiveZoom
        {
            Token = ++requestCounter,
            Profile = profile,
            WorldPosition = worldPosition,
            EndTime = Time.unscaledTime + Mathf.Max(0.01f, duration),
            Global = global
        };

        if (activeZoomCount < activeZooms.Length)
        {
            activeZooms[activeZoomCount] = zoom;
            activeZoomCount++;
            return zoom.Token;
        }

        activeZooms[FindWeakestZoomIndex()] = zoom;
        return zoom.Token;
    }

    static int RefreshInternal(int token, DynamicCameraZoomProfile profile, Vector3 worldPosition, float duration, bool global)
    {
        if (!ShouldAccept(profile, duration))
        {
            Cancel(token);
            return 0;
        }

        int index = token > 0 ? FindTokenIndex(token) : -1;
        if (index < 0)
            return RequestInternal(profile, worldPosition, duration, global);

        ActiveZoom zoom = activeZooms[index];
        zoom.Profile = profile;
        zoom.WorldPosition = worldPosition;
        zoom.EndTime = Time.unscaledTime + Mathf.Max(0.01f, duration);
        zoom.Global = global;
        activeZooms[index] = zoom;
        return token;
    }

    static bool ShouldAccept(DynamicCameraZoomProfile profile, float duration)
    {
        return profile.IsValid &&
               duration > 0f &&
               RoomSettings.AreVisualEffectsEnabled() &&
               RoomSettings.IsDynamicCameraZoomEnabled();
    }

    static float GetDistanceMultiplier(DynamicCameraZoomProfile profile, Vector3 cameraBasePosition, Vector2 zoomWorldPosition)
    {
        float maxDistance = Mathf.Max(0f, profile.MaxDistance);
        if (maxDistance <= 0.001f)
            return 1f;

        Vector2 cameraPosition = cameraBasePosition;
        float distance = Vector2.Distance(cameraPosition, zoomWorldPosition);
        if (distance >= maxDistance)
            return 0f;

        float falloffStart = Mathf.Clamp(profile.FalloffStartDistance, 0f, maxDistance);
        if (distance <= falloffStart)
            return 1f;

        float t = Mathf.InverseLerp(falloffStart, maxDistance, distance);
        return 1f - Mathf.SmoothStep(0f, 1f, t);
    }

    static void PruneExpired()
    {
        float now = Time.unscaledTime;
        for (int i = activeZoomCount - 1; i >= 0; i--)
        {
            if (now >= activeZooms[i].EndTime)
                RemoveZoomAt(i);
        }
    }

    static int FindTokenIndex(int token)
    {
        for (int i = 0; i < activeZoomCount; i++)
        {
            if (activeZooms[i].Token == token)
                return i;
        }

        return -1;
    }

    static int FindWeakestZoomIndex()
    {
        int weakestIndex = 0;
        float weakestMultiplier = float.MaxValue;
        for (int i = 0; i < activeZooms.Length; i++)
        {
            float multiplier = Mathf.Max(1f, activeZooms[i].Profile.Multiplier);
            if (multiplier < weakestMultiplier)
            {
                weakestMultiplier = multiplier;
                weakestIndex = i;
            }
        }

        return weakestIndex;
    }

    static void RemoveZoomAt(int index)
    {
        activeZoomCount--;
        if (index < activeZoomCount)
            activeZooms[index] = activeZooms[activeZoomCount];
    }
}
