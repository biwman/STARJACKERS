using Photon.Pun;
using UnityEngine;

public sealed class ToxicBorderController : MonoBehaviour
{
    const int DamagePerTick = 8;
    const float DamageTickInterval = 0.65f;
    const float LayoutEpsilon = 0.02f;
    const float VisualPulseSpeed = 1.2f;
    const float SpriteRatioEpsilon = 0.002f;
    const float ExposureOutlineRefreshInterval = 0.08f;
    const int TextureSize = 512;
    const int VisualLayerCount = 3;
    const int LayerHaze = 0;
    const int LayerClouds = 1;
    const int LayerWisps = 2;

    static readonly string[] MistLayerNames =
    {
        "ToxicBorder_Haze",
        "ToxicBorder_Clouds",
        "ToxicBorder_Wisps"
    };

    static readonly float[] LayerAlphaMin = { 0.34f, 0.58f, 0.32f };
    static readonly float[] LayerAlphaMax = { 0.58f, 0.9f, 0.68f };
    static readonly float[] LayerPulseSpeed = { 0.68f, 1.08f, 1.62f };
    static readonly float[] LayerDriftMagnitude = { 0.045f, 0.12f, 0.22f };
    static readonly float[] LayerScalePulse = { 0.004f, 0.007f, 0.011f };
    static readonly Color[] LayerDimColors =
    {
        new Color(0.12f, 0.5f, 0.18f, 1f),
        new Color(0.28f, 0.88f, 0.12f, 1f),
        new Color(0.52f, 1f, 0.2f, 1f)
    };
    static readonly Color[] LayerBrightColors =
    {
        new Color(0.34f, 0.95f, 0.18f, 1f),
        new Color(0.82f, 1f, 0.18f, 1f),
        new Color(1f, 1f, 0.42f, 1f)
    };

    static ToxicBorderController instance;
    static readonly Sprite[] toxicBorderSprites = new Sprite[VisualLayerCount];
    static readonly Vector2[] toxicBorderSpriteRatios = new Vector2[VisualLayerCount];

    readonly SpriteRenderer[] mistRenderers = new SpriteRenderer[VisualLayerCount];
    readonly Transform[] mistTransforms = new Transform[VisualLayerCount];

    Vector2 builtInnerSize = Vector2.zero;
    Vector2 builtOuterSize = Vector2.zero;
    float nextDamageTick;
    float nextExposureOutlineRefresh;
    float phase;
    bool visualsActive;
    bool exposureOutlinesActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject existing = GameObject.Find("ToxicBorderController");
        if (existing != null && existing.TryGetComponent(out ToxicBorderController controller))
        {
            instance = controller;
            return;
        }

        GameObject root = new GameObject("ToxicBorderController");
        instance = root.AddComponent<ToxicBorderController>();
        DontDestroyOnLoad(root);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        phase = Mathf.Abs(Mathf.Sin(GetHashCode() * 0.173f)) * Mathf.PI * 2f;
        nextDamageTick = Time.time + Mathf.Repeat(phase, DamageTickInterval);
    }

    void Update()
    {
        bool active = ShouldRun();
        bool showVisuals = active && RoomSettings.AreVisualEffectsEnabled();
        SetVisualsActive(showVisuals);

        if (!active)
        {
            nextDamageTick = Time.time + DamageTickInterval;
            DisableExposureOutlinesIfNeeded();
            return;
        }

        UpdateExposureOutlines();

        if (showVisuals)
        {
            RefreshVisualLayoutIfNeeded();
            UpdateVisualPulse();
        }

        if (Time.time < nextDamageTick)
            return;

        nextDamageTick = Time.time + DamageTickInterval;
        if (CanApplyAuthorityDamage())
            ApplyDamageTick();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    bool ShouldRun()
    {
        if (!RoomSettings.AreToxicBordersEnabled())
            return false;

        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return false;

        if (RoomSettings.GetSessionState() != RoomSettings.SessionStateInPlay)
            return false;

        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
               value is bool started &&
               started;
    }

    bool CanApplyAuthorityDamage()
    {
        return !PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient;
    }

    void ApplyDamageTick()
    {
        Vector2 innerSize = RoomSettings.GetBaseMapDimensions();
        Vector2 outerSize = RoomSettings.GetMapDimensions();
        if (!HasToxicBorder(innerSize, outerSize))
            return;

        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth health = players[i];
            if (!CanDamageTarget(health))
                continue;

            Bounds bounds = ResolveTargetBounds(health);
            if (!IntersectsToxicBorder(bounds, innerSize, outerSize))
                continue;

            DamageTarget(health);
        }
    }

    bool CanDamageTarget(PlayerHealth health)
    {
        if (health == null ||
            !health.isActiveAndEnabled ||
            health.IsWreck ||
            health.IsEvacuationAnimating)
        {
            return false;
        }

        if (health.GetComponent<PlayerDeployableBase>() != null ||
            health.GetComponent<LureBeaconDecoy>() != null ||
            health.GetComponent<ViperWreckTowTarget>() != null)
        {
            return false;
        }

        return true;
    }

    Bounds ResolveTargetBounds(PlayerHealth health)
    {
        HideInNebulaTarget nebulaTarget = health.GetComponent<HideInNebulaTarget>();
        if (nebulaTarget != null)
            return nebulaTarget.GetNebulaBounds();

        Collider2D collider = health.GetComponentInChildren<Collider2D>();
        if (collider != null)
            return collider.bounds;

        SpriteRenderer renderer = health.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
            return renderer.bounds;

        return new Bounds(health.transform.position, Vector3.one);
    }

    static bool IntersectsToxicBorder(Bounds bounds, Vector2 innerSize, Vector2 outerSize)
    {
        float innerHalfX = innerSize.x * 0.5f;
        float innerHalfY = innerSize.y * 0.5f;
        float outerHalfX = outerSize.x * 0.5f;
        float outerHalfY = outerSize.y * 0.5f;

        bool overlapsOuter =
            bounds.max.x >= -outerHalfX &&
            bounds.min.x <= outerHalfX &&
            bounds.max.y >= -outerHalfY &&
            bounds.min.y <= outerHalfY;
        if (!overlapsOuter)
            return false;

        return bounds.min.x < -innerHalfX ||
               bounds.max.x > innerHalfX ||
               bounds.min.y < -innerHalfY ||
               bounds.max.y > innerHalfY;
    }

    void DamageTarget(PlayerHealth health)
    {
        PhotonView view = health.photonView;
        if (view != null && view.ViewID != 0)
        {
            view.RPC(
                nameof(PlayerHealth.TakeEnvironmentalDamage),
                RpcTarget.MasterClient,
                DamagePerTick);
            return;
        }

        health.TakeEnvironmentalDamage(DamagePerTick);
    }

    void UpdateExposureOutlines()
    {
        if (Time.time < nextExposureOutlineRefresh)
            return;

        nextExposureOutlineRefresh = Time.time + ExposureOutlineRefreshInterval;

        Vector2 innerSize = RoomSettings.GetBaseMapDimensions();
        Vector2 outerSize = RoomSettings.GetMapDimensions();
        if (!HasToxicBorder(innerSize, outerSize))
        {
            DisableExposureOutlinesIfNeeded();
            return;
        }

        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        bool localPlayerExposed = false;
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth health = players[i];
            if (!CanDamageTarget(health))
                continue;

            Bounds bounds = ResolveTargetBounds(health);
            if (!IntersectsToxicBorder(bounds, innerSize, outerSize))
                continue;

            ToxicBorderExposureOutline outline = health.GetComponent<ToxicBorderExposureOutline>();
            if (outline == null)
                outline = health.gameObject.AddComponent<ToxicBorderExposureOutline>();

            outline.ShowForFrame();
            exposureOutlinesActive = true;

            if (IsLocalPlayerTarget(health))
                localPlayerExposed = true;
        }

        if (localPlayerExposed)
            ToxicBorderDeathZoneWarningUI.ShowForFrame();
        else
            ToxicBorderDeathZoneWarningUI.HideImmediate();
    }

    void DisableExposureOutlinesIfNeeded()
    {
        ToxicBorderDeathZoneWarningUI.HideImmediate();

        if (!exposureOutlinesActive)
            return;

        ToxicBorderExposureOutline[] outlines = FindObjectsByType<ToxicBorderExposureOutline>(FindObjectsInactive.Include);
        for (int i = 0; i < outlines.Length; i++)
        {
            if (outlines[i] != null)
                outlines[i].HideNow();
        }

        exposureOutlinesActive = false;
    }

    static bool IsLocalPlayerTarget(PlayerHealth health)
    {
        if (health == null ||
            health.IsBotControlled ||
            health.IsNeutralRiderControlled ||
            health.IsEnemyAstronautControlled)
        {
            return false;
        }

        PhotonView view = health.photonView;
        if (view != null && view.ViewID != 0)
            return view.IsMine;

        return PhotonNetwork.LocalPlayer != null &&
               ReferenceEquals(PhotonNetwork.LocalPlayer.TagObject, health.gameObject);
    }

    void SetVisualsActive(bool active)
    {
        if (active)
            EnsureVisuals();

        if (visualsActive == active)
            return;

        visualsActive = active;
        for (int i = 0; i < mistRenderers.Length; i++)
        {
            if (mistRenderers[i] != null)
                mistRenderers[i].enabled = active;
        }
    }

    void EnsureVisuals()
    {
        DestroyLegacyMistLayer();

        for (int i = 0; i < VisualLayerCount; i++)
        {
            if (mistRenderers[i] == null)
                CreateMistLayer(i);
        }
    }

    void CreateMistLayer(int layerIndex)
    {
        GameObject mistObject = new GameObject(MistLayerNames[layerIndex], typeof(SpriteRenderer));
        mistObject.transform.SetParent(transform, false);
        mistTransforms[layerIndex] = mistObject.transform;

        SpriteRenderer renderer = mistObject.GetComponent<SpriteRenderer>();
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = GameVisualTheme.PlayerSortingOrder + 3 + layerIndex;
        renderer.enabled = visualsActive;
        mistRenderers[layerIndex] = renderer;
    }

    void RefreshVisualLayoutIfNeeded()
    {
        Vector2 innerSize = RoomSettings.GetBaseMapDimensions();
        Vector2 outerSize = RoomSettings.GetMapDimensions();
        if (!HasToxicBorder(innerSize, outerSize))
        {
            SetVisualsActive(false);
            return;
        }

        if (Vector2.Distance(innerSize, builtInnerSize) <= LayoutEpsilon &&
            Vector2.Distance(outerSize, builtOuterSize) <= LayoutEpsilon)
        {
            return;
        }

        EnsureVisuals();
        builtInnerSize = innerSize;
        builtOuterSize = outerSize;

        for (int i = 0; i < VisualLayerCount; i++)
        {
            mistRenderers[i].sprite = GetToxicBorderSprite(innerSize, outerSize, i);
            mistTransforms[i].localPosition = Vector3.zero;
            mistTransforms[i].localRotation = Quaternion.identity;
            mistTransforms[i].localScale = new Vector3(
                Mathf.Max(LayoutEpsilon, outerSize.x),
                Mathf.Max(LayoutEpsilon, outerSize.y),
                1f);
        }
    }

    void UpdateVisualPulse()
    {
        if (builtOuterSize.x <= 0f || builtOuterSize.y <= 0f)
            return;

        float borderThickness = Mathf.Max(0.5f, Mathf.Min(
            (builtOuterSize.x - builtInnerSize.x) * 0.5f,
            (builtOuterSize.y - builtInnerSize.y) * 0.5f));
        for (int i = 0; i < VisualLayerCount; i++)
        {
            SpriteRenderer renderer = mistRenderers[i];
            Transform layerTransform = mistTransforms[i];
            if (renderer == null || layerTransform == null)
                continue;

            float pulse = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(Time.time * VisualPulseSpeed * LayerPulseSpeed[i] + phase + i * 1.31f));
            Color color = Color.Lerp(LayerDimColors[i], LayerBrightColors[i], pulse);
            color.a = Mathf.Lerp(LayerAlphaMin[i], LayerAlphaMax[i], pulse);
            renderer.color = color;

            float drift = borderThickness * LayerDriftMagnitude[i];
            float driftX = Mathf.Sin(Time.time * (0.16f + i * 0.07f) + phase + i * 2.17f) * drift;
            float driftY = Mathf.Cos(Time.time * (0.13f + i * 0.05f) + phase * 0.73f + i * 1.43f) * drift;
            float scalePulse = 1f + Mathf.Sin(Time.time * (0.22f + i * 0.09f) + phase + i) * LayerScalePulse[i];
            layerTransform.localPosition = new Vector3(driftX, driftY, 0f);
            layerTransform.localScale = new Vector3(
                Mathf.Max(LayoutEpsilon, builtOuterSize.x * scalePulse),
                Mathf.Max(LayoutEpsilon, builtOuterSize.y * scalePulse),
                1f);
        }
    }

    void DestroyLegacyMistLayer()
    {
        Transform legacy = transform.Find("ToxicBorder_Mist");
        if (legacy != null)
            Destroy(legacy.gameObject);
    }

    static bool HasToxicBorder(Vector2 innerSize, Vector2 outerSize)
    {
        return outerSize.x > innerSize.x + LayoutEpsilon &&
               outerSize.y > innerSize.y + LayoutEpsilon;
    }

    static Sprite GetToxicBorderSprite(Vector2 innerSize, Vector2 outerSize, int layerIndex)
    {
        float ratioX = outerSize.x > 0f ? Mathf.Clamp(innerSize.x / outerSize.x, 0.05f, 0.98f) : 0.84f;
        float ratioY = outerSize.y > 0f ? Mathf.Clamp(innerSize.y / outerSize.y, 0.05f, 0.98f) : 0.84f;
        Vector2 ratio = new Vector2(ratioX, ratioY);
        if (toxicBorderSprites[layerIndex] != null && Vector2.Distance(ratio, toxicBorderSpriteRatios[layerIndex]) <= SpriteRatioEpsilon)
            return toxicBorderSprites[layerIndex];

        toxicBorderSpriteRatios[layerIndex] = ratio;
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "ToxicBorderMistTexture_" + MistLayerNames[layerIndex],
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float seed = 19.37f + layerIndex * 41.11f;
        float maxBorderThickness = Mathf.Max(0.001f, Mathf.Max(1f - ratioX, 1f - ratioY));
        float innerBleed = GetLayerInnerBleed(layerIndex, maxBorderThickness);
        float outerFeather = GetLayerOuterFeather(layerIndex, maxBorderThickness);
        float warpAmplitude = GetLayerWarpAmplitude(layerIndex, maxBorderThickness);

        for (int y = 0; y < TextureSize; y++)
        {
            float ny = ((y + 0.5f) / TextureSize) * 2f - 1f;
            for (int x = 0; x < TextureSize; x++)
            {
                float nx = ((x + 0.5f) / TextureSize) * 2f - 1f;
                Color color = ResolveMistPixel(nx, ny, ratioX, ratioY, innerBleed, outerFeather, maxBorderThickness, warpAmplitude, seed, layerIndex);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            TextureSize);
        sprite.name = "ToxicBorderMistSprite_" + MistLayerNames[layerIndex];
        toxicBorderSprites[layerIndex] = sprite;
        return sprite;
    }

    static Color ResolveMistPixel(float nx, float ny, float ratioX, float ratioY, float innerBleed, float outerFeather, float maxBorderThickness, float warpAmplitude, float seed, int layerIndex)
    {
        float ax = Mathf.Abs(nx);
        float ay = Mathf.Abs(ny);
        float largeWarp = (FractalNoise(nx * 1.75f + seed, ny * 1.75f - seed * 0.61f, seed) - 0.5f) * 2f;
        float midWarp = (FractalNoise(nx * 4.4f - seed * 0.27f, ny * 3.7f + seed * 0.19f, seed + 17.3f) - 0.5f) * 2f;
        float edgeWarp = largeWarp * warpAmplitude + midWarp * warpAmplitude * 0.44f;
        float rawOver = Mathf.Max(ax - ratioX, ay - ratioY);
        float warpedOver = rawOver + edgeWarp;

        if (warpedOver <= -innerBleed)
            return Color.clear;

        float outerDistance = 1f - Mathf.Max(ax, ay);
        float outerWarp = (FractalNoise(nx * 2.5f + seed * 0.33f, ny * 2.5f - seed * 0.44f, seed + 29.7f) - 0.5f) * outerFeather * 0.55f;
        float innerFade = SmoothStep01(-innerBleed, innerBleed * 0.75f, warpedOver);
        float outerFade = SmoothStep01(0f, outerFeather, outerDistance + outerWarp);
        float depth = Mathf.Clamp01((warpedOver + innerBleed) / (maxBorderThickness + innerBleed));
        float depthBias = Mathf.Lerp(0.58f, 1f, SmoothStep01(0.08f, 0.78f, depth));

        float cloudA = FractalNoise(nx * 3.2f + seed * 0.31f, ny * 3.2f - seed * 0.21f, seed + 3.1f);
        float cloudB = FractalNoise(nx * 7.4f - seed * 0.13f, ny * 5.8f + seed * 0.29f, seed + 9.7f);
        float cloudC = FractalNoise(nx * 15.5f + seed * 0.07f, ny * 13.2f - seed * 0.11f, seed + 22.4f);
        float cloud = Mathf.Clamp01(cloudA * 0.54f + cloudB * 0.32f + cloudC * 0.14f);
        float alphaMask = ResolveLayerAlphaMask(nx, ny, cloud, cloudA, cloudB, seed, layerIndex);
        float alpha = innerFade * outerFade * depthBias * alphaMask * GetLayerAlphaScale(layerIndex);

        if (alpha < 0.012f)
            return Color.clear;

        float acid = Mathf.Clamp01(cloud * 0.68f + cloudB * 0.22f + depth * 0.1f);
        Color dark = GetLayerDarkColor(layerIndex);
        Color bright = GetLayerBrightColor(layerIndex);
        Color color = Color.Lerp(dark, bright, acid);
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    static float ResolveLayerAlphaMask(float nx, float ny, float cloud, float cloudA, float cloudB, float seed, int layerIndex)
    {
        switch (layerIndex)
        {
            case LayerHaze:
            {
                float softCloud = SmoothStep01(0.08f, 0.86f, cloud);
                return Mathf.Lerp(0.18f, 0.72f, softCloud);
            }
            case LayerClouds:
            {
                float clump = SmoothStep01(0.25f, 0.82f, cloud);
                float holes = SmoothStep01(0.12f, 0.72f, cloudA);
                return Mathf.Clamp01(clump * Mathf.Lerp(0.34f, 1.05f, holes));
            }
            default:
            {
                float ribbonNoise = FractalNoise(nx * 5.1f + seed, ny * 9.7f - seed, seed + 51.9f);
                float ribbon = 1f - Mathf.Abs(Mathf.Sin((nx * 2.4f + ny * 6.8f + ribbonNoise * 1.75f + seed * 0.035f) * Mathf.PI));
                float thinLines = SmoothStep01(0.62f, 0.96f, ribbon);
                float broken = SmoothStep01(0.46f, 0.9f, cloudB);
                return Mathf.Clamp01(thinLines * broken * Mathf.Lerp(0.45f, 1f, cloud));
            }
        }
    }

    static float GetLayerInnerBleed(int layerIndex, float maxBorderThickness)
    {
        switch (layerIndex)
        {
            case LayerHaze:
                return Mathf.Clamp(maxBorderThickness * 0.44f, 0.045f, 0.105f);
            case LayerClouds:
                return Mathf.Clamp(maxBorderThickness * 0.31f, 0.03f, 0.074f);
            default:
                return Mathf.Clamp(maxBorderThickness * 0.22f, 0.018f, 0.052f);
        }
    }

    static float GetLayerOuterFeather(int layerIndex, float maxBorderThickness)
    {
        switch (layerIndex)
        {
            case LayerHaze:
                return Mathf.Clamp(maxBorderThickness * 0.52f, 0.05f, 0.13f);
            case LayerClouds:
                return Mathf.Clamp(maxBorderThickness * 0.34f, 0.035f, 0.095f);
            default:
                return Mathf.Clamp(maxBorderThickness * 0.25f, 0.026f, 0.07f);
        }
    }

    static float GetLayerWarpAmplitude(int layerIndex, float maxBorderThickness)
    {
        switch (layerIndex)
        {
            case LayerHaze:
                return Mathf.Clamp(maxBorderThickness * 0.18f, 0.018f, 0.043f);
            case LayerClouds:
                return Mathf.Clamp(maxBorderThickness * 0.32f, 0.025f, 0.068f);
            default:
                return Mathf.Clamp(maxBorderThickness * 0.42f, 0.03f, 0.082f);
        }
    }

    static float GetLayerAlphaScale(int layerIndex)
    {
        switch (layerIndex)
        {
            case LayerHaze:
                return 0.44f;
            case LayerClouds:
                return 0.82f;
            default:
                return 0.66f;
        }
    }

    static Color GetLayerDarkColor(int layerIndex)
    {
        switch (layerIndex)
        {
            case LayerHaze:
                return new Color(0.03f, 0.22f, 0.1f, 1f);
            case LayerClouds:
                return new Color(0.08f, 0.46f, 0.09f, 1f);
            default:
                return new Color(0.2f, 0.72f, 0.12f, 1f);
        }
    }

    static Color GetLayerBrightColor(int layerIndex)
    {
        switch (layerIndex)
        {
            case LayerHaze:
                return new Color(0.34f, 0.84f, 0.18f, 1f);
            case LayerClouds:
                return new Color(0.78f, 1f, 0.16f, 1f);
            default:
                return new Color(1f, 1f, 0.38f, 1f);
        }
    }

    static float FractalNoise(float x, float y, float seed)
    {
        float a = Mathf.PerlinNoise(x + seed, y - seed * 0.37f);
        float b = Mathf.PerlinNoise(x * 2.17f - seed * 0.19f, y * 2.03f + seed * 0.23f);
        float c = Mathf.PerlinNoise(x * 4.11f + seed * 0.07f, y * 3.73f - seed * 0.11f);
        return Mathf.Clamp01(a * 0.58f + b * 0.29f + c * 0.13f);
    }

    static float SmoothStep01(float edge0, float edge1, float value)
    {
        if (Mathf.Abs(edge1 - edge0) <= 0.0001f)
            return value >= edge1 ? 1f : 0f;

        float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
