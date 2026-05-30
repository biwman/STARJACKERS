using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EndDisasterMeteorVfx : MonoBehaviour
{
    const float InitialMaxWorldSize = 0.28f;
    const float FinalMapFillMultiplier = 1.16f;
    const float EffectZ = 0.62f;
    const int SortingOrderAboveBackgroundFx = 6;
    const string MapSeedKey = "mapSeed";
    const string MeteorAlarmResourcePath = "Audio/meteor_alarm";

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
    GameObject warningMessageObject;
    CanvasGroup warningCanvasGroup;
    TextMeshProUGUI warningMessageText;

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
        return true;
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
        EnsureWarningMessage();
        if (warningMessageObject == null)
            return;

        warningMessageObject.SetActive(true);
        warningMessageObject.transform.SetAsLastSibling();

        if (warningCanvasGroup != null)
        {
            float pulse = Mathf.PingPong(Time.unscaledTime * 2.7f, 1f);
            warningCanvasGroup.alpha = Mathf.Lerp(0.78f, 1f, Mathf.Max(progress, pulse));
        }

        if (warningMessageText != null)
        {
            warningMessageText.color = Color.Lerp(
                new Color(1f, 0.72f, 0.34f, 1f),
                new Color(1f, 0.25f, 0.14f, 1f),
                Mathf.Clamp01(progress * 1.18f));
        }
    }

    void EnsureWarningMessage()
    {
        if (warningMessageObject != null && warningMessageObject.scene.IsValid())
            return;

        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject == null)
            return;

        warningMessageObject = new GameObject("EndDisasterWarningMessage", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        warningMessageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = warningMessageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -86f);
        rect.sizeDelta = new Vector2(360f, 54f);

        Image background = warningMessageObject.GetComponent<Image>();
        background.color = new Color(0.12f, 0.03f, 0.02f, 0.74f);
        background.raycastTarget = false;

        warningCanvasGroup = warningMessageObject.GetComponent<CanvasGroup>();
        warningCanvasGroup.interactable = false;
        warningCanvasGroup.blocksRaycasts = false;

        GameObject textObject = new GameObject("EndDisasterWarningText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
        textObject.transform.SetParent(warningMessageObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 4f);
        textRect.offsetMax = new Vector2(-16f, -4f);

        warningMessageText = textObject.GetComponent<TextMeshProUGUI>();
        warningMessageText.text = "Disaster incoming!";
        warningMessageText.fontSize = 24f;
        warningMessageText.fontStyle = FontStyles.Bold;
        warningMessageText.characterSpacing = 1.2f;
        warningMessageText.alignment = TextAlignmentOptions.Left;
        warningMessageText.textWrappingMode = TextWrappingModes.NoWrap;
        warningMessageText.raycastTarget = false;

        Shadow shadow = textObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(2.5f, -2.5f);

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference != null)
        {
            warningMessageText.font = reference.font;
            warningMessageText.fontSharedMaterial = reference.fontSharedMaterial;
        }

        warningMessageObject.SetActive(false);
    }

    void HideWarningMessage()
    {
        if (warningMessageObject != null)
            warningMessageObject.SetActive(false);
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
