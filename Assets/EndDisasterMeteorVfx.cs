using Photon.Pun;
using UnityEngine;

public sealed class EndDisasterMeteorVfx : MonoBehaviour
{
    const float InitialMaxWorldSize = 0.28f;
    const float FinalMapFillMultiplier = 1.16f;
    const float EffectZ = 0.62f;
    const int SortingOrderAboveBackgroundFx = 6;
    const string MapSeedKey = "mapSeed";
    const string MeteorAlarmResourcePath = "Audio/meteor_alarm";
    const float DynamicZoomStartCountdownSeconds = 30f;
    const float DynamicZoomMultiplierPerElapsedSecond = 0.02f;

    static readonly string[] ObstacleSpriteResourcePaths =
    {
        "asteroida_1_clean_resource",
        "asteroida_2_clean_resource",
        "asteroida_3_clean_resource",
        "asteroida_podluzna_1_clean_resource",
        "asteroida_podluzna_2_clean_resource",
        "asteroida_nadkruszona_resource",
        "asteroida_ziemniak_resource",
        "Visuals/Obstacles/asteroid_obstacle_resource"
    };

    static EndDisasterMeteorVfx instance;

    SpriteRenderer spriteRenderer;
    GameTimer timer;
    int activeSeed = int.MinValue;
    float targetFinalMaxWorldSize;
    float baseSpriteMaxWorldSize = 1f;
    float rotationSpeed;
    bool meteorVisible;
    AudioClip meteorAlarmClip;
    AudioSource meteorAlarmSource;
    int dynamicZoomToken;

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject existing = GameObject.Find("EndDisasterMeteorVfx");
        if (existing != null && existing.TryGetComponent(out EndDisasterMeteorVfx existingVfx))
        {
            instance = existingVfx;
            return;
        }

        GameObject effect = new GameObject("EndDisasterMeteorVfx");
        instance = effect.AddComponent<EndDisasterMeteorVfx>();
    }

    void Awake()
    {
        instance = this;
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.enabled = false;
        spriteRenderer.color = Color.white;
        ConfigureSorting();
        transform.position = new Vector3(0f, 0f, EffectZ);
    }

    void OnDestroy()
    {
        CancelDynamicZoom();
        StopMeteorAlarm();
        HideWarningMessage();

        if (instance == this)
            instance = null;
    }

    void Update()
    {
        bool extractionCinematic = GameplayHudVisibility.IsExtractionCinematicSuppressed;

        if (!IsRoundStarted() || !RoomSettings.IsEndDisasterMeteorEnabled())
        {
            if (TryHoldMeteorForExtractionCinematic(extractionCinematic))
                return;

            HideMeteor();
            return;
        }

        if (timer == null)
            timer = FindAnyObjectByType<GameTimer>();

        if (timer == null)
        {
            if (TryHoldMeteorForExtractionCinematic(extractionCinematic))
                return;

            HideMeteor();
            return;
        }

        float warningSeconds = Mathf.Max(1f, RoomSettings.GetEndDisasterWarningSeconds());
        float remaining = timer.GetCurrentRemainingTime();
        if (remaining > warningSeconds)
        {
            if (TryHoldMeteorForExtractionCinematic(extractionCinematic))
                return;

            HideMeteor();
            return;
        }

        EnsureMeteorInitialized();
        float progress = Mathf.Clamp01((warningSeconds - remaining) / warningSeconds);
        float eased = EaseInCubic(progress);
        float maxWorldSize = Mathf.Lerp(InitialMaxWorldSize, targetFinalMaxWorldSize, eased);
        float scale = baseSpriteMaxWorldSize > 0.001f ? maxWorldSize / baseSpriteMaxWorldSize : 1f;

        transform.position = new Vector3(0f, 0f, EffectZ);
        transform.localScale = new Vector3(scale, scale, 1f);
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime, Space.Self);

        Color color = Color.white;
        color.a = Mathf.Lerp(0.78f, 0.96f, progress);
        spriteRenderer.color = color;
        spriteRenderer.enabled = true;

        if (extractionCinematic)
        {
            StopMeteorAlarm();
            HideWarningMessage();
        }
        else
        {
            UpdateMeteorAlarm(progress);
            UpdateWarningMessage(progress);
            UpdateDynamicZoom(remaining);
        }

        meteorVisible = true;
    }

    void EnsureMeteorInitialized()
    {
        int seed = ResolveSeed();
        if (meteorVisible && activeSeed == seed && spriteRenderer.sprite != null)
            return;

        activeSeed = seed;
        Sprite sprite = LoadMeteorSprite(seed);
        spriteRenderer.sprite = sprite;
        ConfigureSorting();

        baseSpriteMaxWorldSize = sprite != null
            ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)
            : 1f;

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        targetFinalMaxWorldSize = Mathf.Max(mapSize.x, mapSize.y) * FinalMapFillMultiplier;
        rotationSpeed = (Hash01(seed, 37) < 0.5f ? -1f : 1f) * Mathf.Lerp(1.5f, 4.2f, Hash01(seed, 91));
        transform.rotation = Quaternion.Euler(0f, 0f, Hash01(seed, 13) * 360f);
    }

    void HideMeteor()
    {
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        StopMeteorAlarm();
        HideWarningMessage();
        CancelDynamicZoom();
        meteorVisible = false;
    }

    bool TryHoldMeteorForExtractionCinematic(bool extractionCinematic)
    {
        if (!extractionCinematic || !meteorVisible || spriteRenderer == null || spriteRenderer.sprite == null)
            return false;

        transform.position = new Vector3(0f, 0f, EffectZ);
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime, Space.Self);
        spriteRenderer.enabled = true;
        StopMeteorAlarm();
        HideWarningMessage();
        CancelDynamicZoom();
        return true;
    }

    void UpdateDynamicZoom(float remainingSeconds)
    {
        float elapsedCountdownSeconds = Mathf.Clamp(
            DynamicZoomStartCountdownSeconds - Mathf.Max(0f, remainingSeconds),
            0f,
            DynamicZoomStartCountdownSeconds);
        float multiplier = 1f + elapsedCountdownSeconds * DynamicZoomMultiplierPerElapsedSecond;
        DynamicCameraZoomProfile profile = DynamicCameraZoomProfiles.EndDisasterMeteor.WithMultiplier(multiplier);
        dynamicZoomToken = DynamicCameraZoomController.RefreshGlobal(dynamicZoomToken, profile, 0.35f);
    }

    void CancelDynamicZoom()
    {
        DynamicCameraZoomController.Cancel(dynamicZoomToken);
        dynamicZoomToken = 0;
    }

    void UpdateMeteorAlarm(float progress)
    {
        EnsureMeteorAlarmSource();
        if (meteorAlarmSource == null || meteorAlarmSource.clip == null)
            return;

        meteorAlarmSource.volume = GetMeteorAlarmVolume(progress);
        if (!meteorAlarmSource.isPlaying)
            meteorAlarmSource.Play();
    }

    void EnsureMeteorAlarmSource()
    {
        if (meteorAlarmClip == null)
            meteorAlarmClip = Resources.Load<AudioClip>(MeteorAlarmResourcePath);

        if (meteorAlarmClip == null)
            return;

        if (meteorAlarmSource == null)
            meteorAlarmSource = gameObject.AddComponent<AudioSource>();

        meteorAlarmSource.clip = meteorAlarmClip;
        meteorAlarmSource.loop = true;
        meteorAlarmSource.playOnAwake = false;
        meteorAlarmSource.spatialBlend = 0f;
        meteorAlarmSource.volume = 0f;
        meteorAlarmSource.dopplerLevel = 0f;
    }

    void StopMeteorAlarm()
    {
        if (meteorAlarmSource == null)
            return;

        if (meteorAlarmSource.isPlaying)
            meteorAlarmSource.Stop();

        meteorAlarmSource.volume = 0f;
    }

    float GetMeteorAlarmVolume(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (progress < 0.34f)
            return Mathf.Lerp(0.2f, 0.38f, progress / 0.34f);

        if (progress < 0.78f)
            return Mathf.Lerp(0.38f, 0.72f, (progress - 0.34f) / 0.44f);

        return Mathf.Lerp(0.72f, 1f, (progress - 0.78f) / 0.22f);
    }

    void UpdateWarningMessage(float progress)
    {
        string message = progress >= 0.72f ? "DISASTER IMPACT IMMINENT" : "DISASTER INCOMING";
        RoundMessageLayer.ShowWarning(message, RoundMessagePriority.Warning, 0.34f);
    }

    void HideWarningMessage()
    {
        RoundMessageLayer.ClearWarning(RoundMessagePriority.Warning);
    }

    void ConfigureSorting()
    {
        if (spriteRenderer == null)
            return;

        GameObject ground = GameObject.Find("Ground");
        SpriteRenderer groundRenderer = ground != null ? ground.GetComponent<SpriteRenderer>() : null;
        if (groundRenderer != null)
        {
            spriteRenderer.sortingLayerID = groundRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = groundRenderer.sortingOrder + SortingOrderAboveBackgroundFx;
            return;
        }

        spriteRenderer.sortingLayerName = "Ground";
        spriteRenderer.sortingOrder = SortingOrderAboveBackgroundFx;
    }

    Sprite LoadMeteorSprite(int seed)
    {
        int startIndex = Mathf.Abs(seed) % ObstacleSpriteResourcePaths.Length;
        for (int offset = 0; offset < ObstacleSpriteResourcePaths.Length; offset++)
        {
            int index = (startIndex + offset) % ObstacleSpriteResourcePaths.Length;
            Sprite sprite = Resources.Load<Sprite>(ObstacleSpriteResourcePaths[index]);
            if (sprite != null)
                return sprite;

            Sprite[] sprites = Resources.LoadAll<Sprite>(ObstacleSpriteResourcePaths[index]);
            sprite = GetLargestSprite(sprites);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            float area = sprite.rect.width * sprite.rect.height;
            if (area > bestArea)
            {
                best = sprite;
                bestArea = area;
            }
        }

        return best;
    }

    int ResolveSeed()
    {
        int seed = 17;
        if (PhotonNetwork.CurrentRoom != null)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MapSeedKey, out object mapSeedValue))
                seed = CombineSeed(seed, ConvertToInt(mapSeedValue, seed));

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object startTimeValue))
                seed = CombineSeed(seed, Mathf.RoundToInt((float)(ConvertToDouble(startTimeValue, 0d) * 1000d)));

            seed = CombineSeed(seed, RoomSettings.GetMapBackgroundIndex());
            seed = CombineSeed(seed, Mathf.RoundToInt(RoomSettings.GetRoundDuration()));
            seed = CombineSeed(seed, RoomSettings.GetEndDisasterWarningSeconds());
        }

        return seed;
    }

    int CombineSeed(int seed, int value)
    {
        unchecked
        {
            return (seed * 397) ^ value;
        }
    }

    int ConvertToInt(object value, int fallback)
    {
        return value switch
        {
            int intValue => intValue,
            float floatValue => Mathf.RoundToInt(floatValue),
            double doubleValue => Mathf.RoundToInt((float)doubleValue),
            _ => fallback
        };
    }

    double ConvertToDouble(object value, double fallback)
    {
        return value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            _ => fallback
        };
    }

    bool IsRoundStarted()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
               value is bool started &&
               started;
    }

    float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    float Hash01(int seed, int salt)
    {
        return Mathf.Repeat(Mathf.Sin((seed * 12.9898f) + (salt * 78.233f)) * 43758.5453f, 1f);
    }
}
