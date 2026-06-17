using System;
using UnityEngine;

public sealed class AdvancedSpaceBackground : MonoBehaviour
{
    const string RootName = "AdvancedSpaceBackground";
    const string NoobHavenBackdropResourcePrefix = "Visuals/Backgrounds/";
    const string NoobHavenBackdropResourceSuffix = "_resource";
    const string BackgroundObjectResourcePrefix = "Visuals/Backgrounds/";
    const string BackgroundObjectResourceSuffix = "_resource";
    const string ToxicAreaCloudResourcePath = "Visuals/Backgrounds/background_object13_addon_resource";
    const int BackdropSortingOrder = GameVisualTheme.BackgroundSortingOrder - 20;
    const int HazeSortingOrder = GameVisualTheme.BackgroundSortingOrder - 18;
    const int FarSortingOrder = GameVisualTheme.BackgroundSortingOrder - 17;
    const int MidSortingOrder = GameVisualTheme.BackgroundSortingOrder - 16;
    const int DustSortingOrder = GameVisualTheme.BackgroundSortingOrder - 15;
    const int BackgroundObjectSortingOrder = GameVisualTheme.BackgroundSortingOrder - 10;
    const float MapPadding = 58f;
    const float NoobHavenBackdropViewportOverscan = 1.18f;
    const float NoobHavenBackdropParallax = 0.026f;
    const float BackgroundObjectParallax = 0.06f;
    const float ToxicPlanetParallax = 0.052f;
    const float ToxicCloudParallax = 0.092f;
    const float GravityWellCoreParallax = 1f;
    const float GravityWellObjectPulseSpeed = 2.2f;
    const float GravityWellObjectPulseAmount = 0.045f;
    const float GravityWellEdgeSparkRotationSpeed = 18f;

    enum AdvancedBackgroundStyle
    {
        None,
        NoobHaven,
        GravityWell,
        ToxicArea
    }

    enum LayerStyle
    {
        Haze,
        Star,
        Dust
    }

    sealed class LayerState
    {
        public GameObject Root;
        public Transform Transform;
        public Mesh Mesh;
        public MeshRenderer Renderer;
        public float Parallax;
        public Vector2 Drift;
        public float TwinkleSpeed;
        public float TwinkleAmount;
        public float Phase;
        public float RotationSpeed;
        public bool ClampParallax;
        public Vector2 MaxParallaxOffset;
        public SpriteRenderer SpriteRenderer;
        public Vector2 SpriteSize;
        public bool ScaleToCameraView;
        public float ViewportOverscan;
        public Vector2 BaseOffset;
        public bool KeepInsideCameraView;
        public Vector3 BaseLocalScale;
        public bool PulseSpriteScale;
        public Transform EdgeEffectTransform;
        public Mesh EdgeEffectMesh;
        public MeshRenderer EdgeEffectRenderer;
        public float EdgeEffectRotationSpeed;
    }

    static AdvancedSpaceBackground instance;
    static Material sharedMaterial;
    static Texture2D glowTexture;
    static Sprite noobHavenBackdropSprite;
    static string noobHavenBackdropSpriteId;
    static Sprite backgroundObjectSprite;
    static string backgroundObjectSpriteId;
    static Sprite toxicAreaCloudSprite;

    readonly LayerState[] layers = new LayerState[10];
    MaterialPropertyBlock propertyBlock;
    Camera cachedCamera;

    int builtSeed = int.MinValue;
    AdvancedBackgroundStyle builtStyle = AdvancedBackgroundStyle.None;
    string builtBackgroundObjectId = string.Empty;
    Vector2 builtMapSize = Vector2.zero;
    float nextSettingsCheck;
    float nextGlowUpdate;

    public static void RefreshForCurrentSettings()
    {
        if (!Application.isPlaying)
            return;

        EnsureInstance();
        if (instance != null)
            instance.RefreshInternal();
    }

    public static bool ShouldShowForCurrentSettings()
    {
        return ResolveStyleForCurrentSettings() != AdvancedBackgroundStyle.None;
    }

    public static Color GetGroundTintForCurrentSettings()
    {
        switch (ResolveStyleForCurrentSettings())
        {
            case AdvancedBackgroundStyle.NoobHaven:
                return new Color(1f, 1f, 1f, 0f);
            case AdvancedBackgroundStyle.GravityWell:
                return new Color(1f, 1f, 1f, 0f);
            case AdvancedBackgroundStyle.ToxicArea:
                return new Color(1f, 1f, 1f, 0f);
            default:
                return Color.white;
        }
    }

    static AdvancedBackgroundStyle ResolveStyleForCurrentSettings()
    {
        string mapId = RoomSettings.GetSelectedLobbyMapId();
        if (string.Equals(mapId, LobbyMapCatalog.GravityWellMapId, StringComparison.Ordinal))
            return AdvancedBackgroundStyle.GravityWell;

        if (string.Equals(mapId, LobbyMapCatalog.ToxicAreaMapId, StringComparison.Ordinal))
            return AdvancedBackgroundStyle.ToxicArea;

        return AdvancedBackgroundStyle.NoobHaven;
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = GameObject.Find(RootName);
        if (root == null)
            root = new GameObject(RootName);

        instance = root.GetComponent<AdvancedSpaceBackground>();
        if (instance == null)
            instance = root.AddComponent<AdvancedSpaceBackground>();

        DontDestroyOnLoad(root);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (Time.unscaledTime >= nextSettingsCheck)
        {
            nextSettingsCheck = Time.unscaledTime + 0.45f;
            RefreshInternal();
        }
    }

    void LateUpdate()
    {
        if (!gameObject.activeSelf || layers[0] == null)
            return;

        UpdateLayerTransforms();
        if (Time.unscaledTime >= nextGlowUpdate)
        {
            nextGlowUpdate = Time.unscaledTime + 0.08f;
            UpdateLayerGlow();
        }
    }

    void RefreshInternal()
    {
        AdvancedBackgroundStyle style = ResolveStyleForCurrentSettings();
        if (style == AdvancedBackgroundStyle.None)
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        string backgroundObjectId = RoomSettings.GetBackgroundObjectId();
        int seed = BuildSeed(
            RoomSettings.GetSelectedLobbyMapId(),
            RoomSettings.GetMapBackgroundIndex(),
            RoomSettings.GetMapSizeMode(),
            RoomSettings.GetParallaxBackgroundId());
        if (seed != builtSeed ||
            style != builtStyle ||
            !string.Equals(backgroundObjectId, builtBackgroundObjectId, StringComparison.Ordinal) ||
            Vector2.Distance(mapSize, builtMapSize) > 0.01f ||
            layers[0] == null)
        {
            BuildLayers(style, seed, mapSize);
        }

        UpdateLayerTransforms();
        UpdateLayerGlow();
    }

    bool ShouldShowAdvancedBackground()
    {
        return ResolveStyleForCurrentSettings() != AdvancedBackgroundStyle.None;
    }

    void BuildLayers(AdvancedBackgroundStyle style, int seed, Vector2 mapSize)
    {
        switch (style)
        {
            case AdvancedBackgroundStyle.GravityWell:
                BuildGravityWellLayers(seed, mapSize);
                break;
            case AdvancedBackgroundStyle.ToxicArea:
                BuildToxicAreaLayers(seed, mapSize);
                break;
            case AdvancedBackgroundStyle.NoobHaven:
                BuildNoobHavenLayers(seed, mapSize);
                break;
        }
    }

    void BuildNoobHavenLayers(int seed, Vector2 mapSize)
    {
        ClearLayers();

        System.Random random = new System.Random(seed);
        Vector2 extent = new Vector2(mapSize.x + MapPadding, mapSize.y + MapPadding);

        int layerIndex = 0;
        LayerState backdropLayer = CreateNoobHavenBackdropLayer(mapSize);
        if (backdropLayer != null)
            layers[layerIndex++] = backdropLayer;

        layers[layerIndex++] = CreateLayer("NoobHavenFarStars", 560, extent, 0.035f, 0.13f, 1f, 1f, 0.075f, Vector2.zero, 0.18f, 0.045f, FarSortingOrder, LayerStyle.Star, random);
        layers[layerIndex++] = CreateLayer("NoobHavenSoftHaze", 30, extent, 3.2f, 8.6f, 2.65f, 0.54f, 0.11f, Vector2.zero, 0.13f, 0.025f, HazeSortingOrder, LayerStyle.Haze, random);
        layers[layerIndex++] = CreateLayer("NoobHavenMidStars", 300, extent, 0.07f, 0.24f, 1f, 1f, 0.18f, Vector2.zero, 0.45f, 0.075f, MidSortingOrder, LayerStyle.Star, random);
        layers[layerIndex++] = CreateLayer("NoobHavenNearDust", 144, extent, 0.055f, 0.18f, 2.45f, 0.48f, 0.32f, Vector2.zero, 0.34f, 0.055f, DustSortingOrder, LayerStyle.Dust, random);
        LayerState backgroundObjectLayer = CreateBackgroundObjectLayer(mapSize, random);
        if (backgroundObjectLayer != null)
            layers[layerIndex++] = backgroundObjectLayer;

        builtSeed = seed;
        builtStyle = AdvancedBackgroundStyle.NoobHaven;
        builtBackgroundObjectId = RoomSettings.GetBackgroundObjectId();
        builtMapSize = mapSize;
    }

    LayerState CreateNoobHavenBackdropLayer(Vector2 mapSize)
    {
        string backgroundId = RoomSettings.GetParallaxBackgroundId();
        Sprite sprite = LoadNoobHavenBackdropSprite(backgroundId);
        if (sprite == null)
            return null;

        GameObject layerObject = new GameObject("NoobHavenKosmosBackdrop_" + backgroundId, typeof(SpriteRenderer));
        layerObject.transform.SetParent(transform, false);

        SpriteRenderer renderer = layerObject.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = BackdropSortingOrder;

        Vector2 spriteSize = sprite.bounds.size;
        if (spriteSize.x <= 0.001f || spriteSize.y <= 0.001f)
            spriteSize = new Vector2(16f, 9f);

        return new LayerState
        {
            Root = layerObject,
            Transform = layerObject.transform,
            SpriteRenderer = renderer,
            SpriteSize = spriteSize,
            ScaleToCameraView = true,
            ViewportOverscan = NoobHavenBackdropViewportOverscan,
            Parallax = NoobHavenBackdropParallax,
            Drift = Vector2.zero,
            TwinkleSpeed = 0f,
            TwinkleAmount = 0f,
            Phase = StableLayerPhase("NoobHavenKosmosBackdrop"),
            ClampParallax = true,
            MaxParallaxOffset = new Vector2(
                Mathf.Clamp(mapSize.x * 0.032f, 4f, 12f),
                Mathf.Clamp(mapSize.y * 0.032f, 4f, 12f))
        };
    }

    void BuildGravityWellLayers(int seed, Vector2 mapSize)
    {
        ClearLayers();

        System.Random random = new System.Random(seed);
        Vector2 extent = new Vector2(mapSize.x + MapPadding, mapSize.y + MapPadding);
        float mapRadius = Mathf.Min(mapSize.x, mapSize.y) * 0.5f;
        float outerRadius = Mathf.Max(15f, mapRadius * 0.96f);
        float middleRadius = Mathf.Max(10f, mapRadius * 0.68f);

        LayerState backdropLayer = CreateNoobHavenBackdropLayer(mapSize);
        if (backdropLayer != null)
            layers[0] = backdropLayer;

        layers[1] = CreateLayer("GravityFarStars", 520, extent, 0.035f, 0.18f, 1f, 1f, 0.055f, new Vector2(0.0012f, -0.0007f), 0.28f, 0.09f, FarSortingOrder, LayerStyle.Star, random);
        layers[2] = CreateMeshLayer(
            "GravityLensDust",
            BuildOrbitParticleMesh(
                "GravityLensDustMesh",
                270,
                middleRadius * 0.48f,
                outerRadius * 1.08f,
                0.55f,
                2.4f,
                0.04f,
                0.18f,
                0.54f,
                new Color(0.52f, 0.98f, 1f, 0.28f),
                new Color(0.78f, 0.36f, 1f, 0.18f),
                new Color(1f, 0.78f, 0.28f, 0.32f),
                random),
            1f,
            Vector2.zero,
            GravityWellCoreParallax,
            0.065f,
            FarSortingOrder + 1,
            0.28f);
        layers[3] = CreateMeshLayer(
            "GravityFractureGlow",
            BuildOrbitParticleMesh(
                "GravityFractureGlowMesh",
                140,
                middleRadius * 0.56f,
                outerRadius * 1.14f,
                0.42f,
                2.1f,
                0.035f,
                0.13f,
                0.5f,
                new Color(0.8f, 0.35f, 1f, 0.28f),
                new Color(0.25f, 0.55f, 1f, 0.18f),
                new Color(1f, 0.44f, 0.12f, 0.42f),
                random),
            1f,
            Vector2.zero,
            GravityWellCoreParallax,
            0.055f,
            MidSortingOrder,
            0.42f);
        layers[4] = CreateMeshLayer(
            "AccretionDiskGold",
            BuildOrbitParticleMesh(
                "AccretionDiskGoldMesh",
                210,
                2.8f,
                middleRadius * 1.08f,
                1.1f,
                5.6f,
                0.12f,
                0.46f,
                0.42f,
                new Color(1f, 0.99f, 0.78f, 0.96f),
                new Color(1f, 0.36f, 0.08f, 0.48f),
                new Color(0.98f, 1f, 1f, 0.82f),
                random),
            1f,
            Vector2.zero,
            GravityWellCoreParallax,
            0.075f,
            MidSortingOrder + 1,
            -0.58f);
        layers[5] = CreateMeshLayer(
            "AccretionDiskIon",
            BuildOrbitParticleMesh(
                "AccretionDiskIonMesh",
                160,
                4.2f,
                outerRadius,
                0.75f,
                3.4f,
                0.06f,
                0.24f,
                0.58f,
                new Color(0.36f, 0.95f, 1f, 0.58f),
                new Color(0.68f, 0.2f, 1f, 0.28f),
                new Color(1f, 0.9f, 0.44f, 0.48f),
                random),
            1f,
            Vector2.zero,
            GravityWellCoreParallax,
            0.075f,
            MidSortingOrder + 2,
            0.36f);
        layers[6] = CreateMeshLayer(
            "WhiteHotInnerDisk",
            BuildOrbitParticleMesh(
                "WhiteHotInnerDiskMesh",
                98,
                2.4f,
                Mathf.Max(7.5f, middleRadius * 0.46f),
                0.75f,
                3.8f,
                0.1f,
                0.32f,
                0.38f,
                new Color(1f, 1f, 0.86f, 0.96f),
                new Color(1f, 0.62f, 0.12f, 0.68f),
                new Color(0.8f, 1f, 1f, 0.88f),
                random),
            1f,
            Vector2.zero,
            GravityWellCoreParallax,
            0.09f,
            DustSortingOrder,
            -0.86f);
        layers[7] = CreateMeshLayer(
            "NearEmberStreaks",
            BuildOrbitParticleMesh(
                "NearEmberStreaksMesh",
                130,
                5.2f,
                outerRadius * 1.2f,
                0.35f,
                2.2f,
                0.04f,
                0.16f,
                0.5f,
                new Color(1f, 0.52f, 0.08f, 0.46f),
                new Color(0.48f, 0.12f, 1f, 0.24f),
                new Color(1f, 0.9f, 0.36f, 0.62f),
                random),
            1f,
            Vector2.zero,
            GravityWellCoreParallax,
            0.1f,
            DustSortingOrder + 1,
            0.7f);
        layers[8] = CreateMeshLayer(
            "EventHorizon",
            BuildGravityCoreMesh("EventHorizonMesh", random),
            1f,
            Vector2.zero,
            GravityWellCoreParallax,
            0.055f,
            DustSortingOrder + 2,
            0.12f);
        layers[9] = CreateBackgroundObjectLayer(mapSize, random);

        builtSeed = seed;
        builtStyle = AdvancedBackgroundStyle.GravityWell;
        builtBackgroundObjectId = RoomSettings.GetBackgroundObjectId();
        builtMapSize = mapSize;
    }

    void BuildToxicAreaLayers(int seed, Vector2 mapSize)
    {
        ClearLayers();

        System.Random random = new System.Random(seed);
        Vector2 extent = new Vector2(mapSize.x + MapPadding, mapSize.y + MapPadding);

        int layerIndex = 0;
        LayerState backdropLayer = CreateNoobHavenBackdropLayer(mapSize);
        if (backdropLayer != null)
            layers[layerIndex++] = backdropLayer;

        layers[layerIndex++] = CreateLayer("ToxicFarStars", 500, extent, 0.035f, 0.16f, 1f, 1f, 0.05f, new Vector2(0.0009f, -0.0004f), 0.26f, 0.08f, FarSortingOrder, LayerStyle.Star, random);
        layers[layerIndex++] = CreateLayer("ToxicGreenHaze", 38, extent, 3.6f, 9.2f, 2.8f, 0.58f, 0.12f, new Vector2(0.0014f, 0.0007f), 0.2f, 0.04f, HazeSortingOrder, LayerStyle.Haze, random);
        layers[layerIndex++] = CreateLayer("ToxicMidStars", 260, extent, 0.065f, 0.22f, 1f, 1f, 0.2f, Vector2.zero, 0.45f, 0.08f, MidSortingOrder, LayerStyle.Star, random);
        layers[layerIndex++] = CreateLayer("ToxicIonDust", 132, extent, 0.06f, 0.2f, 2.3f, 0.5f, 0.34f, new Vector2(-0.001f, 0.001f), 0.36f, 0.065f, DustSortingOrder, LayerStyle.Dust, random);

        string objectId = RoomSettings.GetBackgroundObjectId();
        if (!string.Equals(objectId, RoomSettings.BackgroundObjectOff, StringComparison.Ordinal))
        {
            float targetSize = Mathf.Clamp(Mathf.Min(mapSize.x, mapSize.y) * 0.34f, 11f, 17f);
            Vector2 baseOffset = new Vector2(
                Mathf.Clamp(mapSize.x * 0.13f, 3.2f, 6.2f),
                Mathf.Clamp(mapSize.y * 0.04f, 1.2f, 2.4f));

            LayerState planetLayer = CreateToxicAreaSpriteLayer(
                "ToxicAreaBackgroundPlanet_" + objectId,
                LoadBackgroundObjectSprite(objectId),
                BackgroundObjectSortingOrder,
                ToxicPlanetParallax,
                Vector2.zero,
                baseOffset,
                targetSize,
                Color.white,
                true,
                0f);
            if (planetLayer != null)
                layers[layerIndex++] = planetLayer;

            LayerState cloudLayer = CreateToxicAreaSpriteLayer(
                "ToxicAreaPlanetClouds",
                LoadToxicAreaCloudSprite(),
                BackgroundObjectSortingOrder + 1,
                ToxicCloudParallax,
                new Vector2(0.0016f, 0.0008f),
                baseOffset + new Vector2(targetSize * 0.02f, targetSize * 0.04f),
                targetSize * 0.72f,
                new Color(0.88f, 1f, 0.45f, 0.72f),
                true,
                -5f);
            if (cloudLayer != null)
                layers[layerIndex++] = cloudLayer;
        }

        builtSeed = seed;
        builtStyle = AdvancedBackgroundStyle.ToxicArea;
        builtBackgroundObjectId = RoomSettings.GetBackgroundObjectId();
        builtMapSize = mapSize;
    }

    LayerState CreateToxicAreaSpriteLayer(
        string layerName,
        Sprite sprite,
        int sortingOrder,
        float parallax,
        Vector2 drift,
        Vector2 baseOffset,
        float targetSize,
        Color color,
        bool keepInsideCameraView,
        float rotationZ)
    {
        if (sprite == null)
            return null;

        GameObject layerObject = new GameObject(layerName, typeof(SpriteRenderer));
        layerObject.transform.SetParent(transform, false);

        SpriteRenderer renderer = layerObject.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = sortingOrder;

        Vector2 spriteSize = sprite.bounds.size;
        float maxDimension = Mathf.Max(spriteSize.x, spriteSize.y);
        if (maxDimension <= 0.001f)
            maxDimension = 1f;

        float scale = targetSize / maxDimension;
        Vector3 baseLocalScale = new Vector3(scale, scale, 1f);
        layerObject.transform.localScale = baseLocalScale;
        layerObject.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);

        return new LayerState
        {
            Root = layerObject,
            Transform = layerObject.transform,
            SpriteRenderer = renderer,
            SpriteSize = spriteSize,
            Parallax = parallax,
            Drift = drift,
            TwinkleSpeed = 0f,
            TwinkleAmount = 0f,
            Phase = StableLayerPhase(layerName),
            BaseOffset = baseOffset,
            KeepInsideCameraView = keepInsideCameraView,
            BaseLocalScale = baseLocalScale
        };
    }

    LayerState CreateBackgroundObjectLayer(Vector2 mapSize, System.Random random)
    {
        string objectId = RoomSettings.GetBackgroundObjectId();
        if (string.Equals(objectId, RoomSettings.BackgroundObjectOff, StringComparison.Ordinal))
            return null;

        Sprite sprite = LoadBackgroundObjectSprite(objectId);
        if (sprite == null)
            return null;

        GameObject layerObject = new GameObject("AdvancedBackgroundObject_" + objectId, typeof(SpriteRenderer));
        layerObject.transform.SetParent(transform, false);

        SpriteRenderer renderer = layerObject.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = BackgroundObjectSortingOrder;

        Vector2 spriteSize = sprite.bounds.size;
        float maxDimension = Mathf.Max(spriteSize.x, spriteSize.y);
        if (maxDimension <= 0.001f)
            maxDimension = 1f;

        bool isGravityWellCenterObject = IsGravityWellBackgroundObject13(objectId);
        float targetSize = Mathf.Clamp(Mathf.Min(mapSize.x, mapSize.y) * 0.2f, 8f, 20f);
        if (IsHiddenDimensionBackgroundObject14(objectId))
            targetSize *= 0.5f;

        float scale = targetSize / maxDimension;
        Vector3 baseLocalScale = new Vector3(scale, scale, 1f);
        layerObject.transform.localScale = baseLocalScale;
        layerObject.transform.rotation = isGravityWellCenterObject
            ? Quaternion.identity
            : Quaternion.Euler(0f, 0f, NextFloat(random, -7f, 7f));

        Transform edgeEffectTransform = null;
        Mesh edgeEffectMesh = null;
        MeshRenderer edgeEffectRenderer = null;
        if (isGravityWellCenterObject)
            CreateGravityWellObjectEdgeEffect(layerObject.transform, spriteSize, random, out edgeEffectTransform, out edgeEffectMesh, out edgeEffectRenderer);

        return new LayerState
        {
            Root = layerObject,
            Transform = layerObject.transform,
            SpriteRenderer = renderer,
            SpriteSize = spriteSize,
            Parallax = isGravityWellCenterObject ? GravityWellCoreParallax : BackgroundObjectParallax,
            Drift = Vector2.zero,
            TwinkleSpeed = 0f,
            TwinkleAmount = 0f,
            Phase = StableLayerPhase(objectId),
            BaseOffset = ResolveBackgroundObjectOffset(objectId, mapSize, targetSize, random),
            KeepInsideCameraView = !isGravityWellCenterObject,
            BaseLocalScale = baseLocalScale,
            PulseSpriteScale = isGravityWellCenterObject,
            EdgeEffectTransform = edgeEffectTransform,
            EdgeEffectMesh = edgeEffectMesh,
            EdgeEffectRenderer = edgeEffectRenderer,
            EdgeEffectRotationSpeed = GravityWellEdgeSparkRotationSpeed
        };
    }

    Vector2 ResolveBackgroundObjectOffset(string objectId, Vector2 mapSize, float targetSize, System.Random random)
    {
        if (IsGravityWellBackgroundObject13(objectId))
            return Vector2.zero;

        return PickBackgroundObjectOffset(mapSize, targetSize, random);
    }

    static bool IsGravityWellBackgroundObject13(string objectId)
    {
        return string.Equals(RoomSettings.GetSelectedLobbyMapId(), LobbyMapCatalog.GravityWellMapId, StringComparison.Ordinal) &&
               string.Equals(RoomSettings.NormalizeBackgroundObjectId(objectId), RoomSettings.BackgroundObject13, StringComparison.Ordinal);
    }

    static bool IsHiddenDimensionBackgroundObject14(string objectId)
    {
        return string.Equals(RoomSettings.GetSelectedLobbyMapId(), LobbyMapCatalog.HiddenDimensionMapId, StringComparison.Ordinal) &&
               string.Equals(RoomSettings.NormalizeBackgroundObjectId(objectId), RoomSettings.BackgroundObject14, StringComparison.Ordinal);
    }

    void CreateGravityWellObjectEdgeEffect(
        Transform parent,
        Vector2 spriteSize,
        System.Random random,
        out Transform effectTransform,
        out Mesh effectMesh,
        out MeshRenderer effectRenderer)
    {
        effectTransform = null;
        effectMesh = null;
        effectRenderer = null;

        if (parent == null || spriteSize.x <= 0.001f || spriteSize.y <= 0.001f)
            return;

        GameObject effectObject = new GameObject("GravityWellBackgroundObjectEdgeSparks", typeof(MeshFilter), typeof(MeshRenderer));
        effectObject.transform.SetParent(parent, false);
        effectObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);

        effectMesh = BuildGravityWellObjectEdgeSparkMesh(spriteSize, random);
        MeshFilter filter = effectObject.GetComponent<MeshFilter>();
        filter.sharedMesh = effectMesh;

        effectRenderer = effectObject.GetComponent<MeshRenderer>();
        effectRenderer.sharedMaterial = GetSharedMaterial();
        effectRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        effectRenderer.sortingOrder = BackgroundObjectSortingOrder + 1;
        effectRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        effectRenderer.receiveShadows = false;
        effectTransform = effectObject.transform;
    }

    Mesh BuildGravityWellObjectEdgeSparkMesh(Vector2 spriteSize, System.Random random)
    {
        const int streakCount = 42;
        Vector3[] vertices = new Vector3[streakCount * 4];
        Vector2[] uvs = new Vector2[streakCount * 4];
        Color[] colors = new Color[streakCount * 4];
        int[] triangles = new int[streakCount * 6];

        float radiusX = spriteSize.x * 0.48f;
        float radiusY = spriteSize.y * 0.42f;
        float maxSize = Mathf.Max(spriteSize.x, spriteSize.y);

        for (int i = 0; i < streakCount; i++)
        {
            float angle = ((i / (float)streakCount) * Mathf.PI * 2f) + NextFloat(random, -0.08f, 0.08f);
            Vector2 center = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
            Vector2 tangent = new Vector2(-Mathf.Sin(angle) * radiusX, Mathf.Cos(angle) * radiusY).normalized;
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector2.right;

            Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
            float length = NextFloat(random, maxSize * 0.035f, maxSize * 0.115f);
            float thickness = NextFloat(random, maxSize * 0.006f, maxSize * 0.018f);
            Vector2 halfTangent = tangent * (length * 0.5f);
            Vector2 halfNormal = normal * (thickness * 0.5f);

            int vertex = i * 4;
            int tri = i * 6;
            vertices[vertex] = center - halfTangent - halfNormal;
            vertices[vertex + 1] = center - halfTangent + halfNormal;
            vertices[vertex + 2] = center + halfTangent + halfNormal;
            vertices[vertex + 3] = center + halfTangent - halfNormal;

            uvs[vertex] = new Vector2(0f, 0f);
            uvs[vertex + 1] = new Vector2(0f, 1f);
            uvs[vertex + 2] = new Vector2(1f, 1f);
            uvs[vertex + 3] = new Vector2(1f, 0f);

            float alpha = NextFloat(random, 0.32f, 0.9f);
            Color color = Color.Lerp(new Color(1f, 0.34f, 0.02f, alpha), new Color(1f, 0.82f, 0.16f, alpha), NextFloat(random, 0f, 1f));
            colors[vertex] = color;
            colors[vertex + 1] = color;
            colors[vertex + 2] = color;
            colors[vertex + 3] = color;

            triangles[tri] = vertex;
            triangles[tri + 1] = vertex + 1;
            triangles[tri + 2] = vertex + 2;
            triangles[tri + 3] = vertex;
            triangles[tri + 4] = vertex + 2;
            triangles[tri + 5] = vertex + 3;
        }

        Mesh mesh = new Mesh { name = "GravityWellBackgroundObjectEdgeSparkMesh" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(spriteSize.x * 1.3f, spriteSize.y * 1.3f, 4f));
        mesh.MarkDynamic();
        return mesh;
    }

    Vector2 PickBackgroundObjectOffset(Vector2 mapSize, float targetSize, System.Random random)
    {
        Camera camera = ResolveCamera();
        if (camera != null && camera.orthographic)
        {
            float visibleHeight = camera.orthographicSize * 2f;
            float visibleWidth = visibleHeight * Mathf.Max(0.01f, camera.aspect);
            float safetyRadius = targetSize * 0.58f;
            float halfX = Mathf.Max(0f, visibleWidth * 0.5f - safetyRadius - 0.7f);
            float halfY = Mathf.Max(0f, visibleHeight * 0.5f - safetyRadius - 0.7f);
            return new Vector2(NextFloat(random, -halfX, halfX), NextFloat(random, -halfY, halfY));
        }

        float fallbackX = Mathf.Max(0f, Mathf.Min(8f, mapSize.x * 0.14f));
        float fallbackY = Mathf.Max(0f, Mathf.Min(4.5f, mapSize.y * 0.14f));
        return new Vector2(NextFloat(random, -fallbackX, fallbackX), NextFloat(random, -fallbackY, fallbackY));
    }

    LayerState CreateLayer(
        string layerName,
        int count,
        Vector2 extent,
        float minSize,
        float maxSize,
        float widthScale,
        float heightScale,
        float parallax,
        Vector2 drift,
        float twinkleSpeed,
        float twinkleAmount,
        int sortingOrder,
        LayerStyle style,
        System.Random random,
        float rotationSpeed = 0f)
    {
        Mesh mesh = BuildParticleMesh(layerName + "Mesh", count, extent, minSize, maxSize, widthScale, heightScale, style, random);
        return CreateMeshLayer(layerName, mesh, parallax, drift, twinkleSpeed, twinkleAmount, sortingOrder, rotationSpeed);
    }

    LayerState CreateMeshLayer(
        string layerName,
        Mesh mesh,
        float parallax,
        Vector2 drift,
        float twinkleSpeed,
        float twinkleAmount,
        int sortingOrder,
        float rotationSpeed = 0f)
    {
        GameObject layerObject = new GameObject(layerName, typeof(MeshFilter), typeof(MeshRenderer));
        layerObject.transform.SetParent(transform, false);

        MeshFilter filter = layerObject.GetComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = layerObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetSharedMaterial();
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return new LayerState
        {
            Root = layerObject,
            Transform = layerObject.transform,
            Mesh = mesh,
            Renderer = renderer,
            Parallax = parallax,
            Drift = drift,
            TwinkleSpeed = twinkleSpeed,
            TwinkleAmount = twinkleAmount,
            Phase = StableLayerPhase(layerName),
            RotationSpeed = rotationSpeed
        };
    }

    Mesh BuildParticleMesh(
        string meshName,
        int count,
        Vector2 extent,
        float minSize,
        float maxSize,
        float widthScale,
        float heightScale,
        LayerStyle style,
        System.Random random)
    {
        Vector3[] vertices = new Vector3[count * 4];
        Vector2[] uvs = new Vector2[count * 4];
        Color[] colors = new Color[count * 4];
        int[] triangles = new int[count * 6];

        for (int i = 0; i < count; i++)
        {
            float x = NextFloat(random, -extent.x * 0.5f, extent.x * 0.5f);
            float y = NextFloat(random, -extent.y * 0.5f, extent.y * 0.5f);
            float size = NextFloat(random, minSize, maxSize);
            float halfW = size * widthScale * 0.5f;
            float halfH = size * heightScale * 0.5f;
            float angle = style == LayerStyle.Star ? 0f : NextFloat(random, -38f, 38f) * Mathf.Deg2Rad;
            Vector3 right = new Vector3(Mathf.Cos(angle) * halfW, Mathf.Sin(angle) * halfW, 0f);
            Vector3 up = new Vector3(-Mathf.Sin(angle) * halfH, Mathf.Cos(angle) * halfH, 0f);
            Vector3 center = new Vector3(x, y, 0f);
            int vertex = i * 4;
            int tri = i * 6;

            vertices[vertex] = center - right - up;
            vertices[vertex + 1] = center - right + up;
            vertices[vertex + 2] = center + right + up;
            vertices[vertex + 3] = center + right - up;

            uvs[vertex] = new Vector2(0f, 0f);
            uvs[vertex + 1] = new Vector2(0f, 1f);
            uvs[vertex + 2] = new Vector2(1f, 1f);
            uvs[vertex + 3] = new Vector2(1f, 0f);

            Color color = PickParticleColor(style, random);
            colors[vertex] = color;
            colors[vertex + 1] = color;
            colors[vertex + 2] = color;
            colors[vertex + 3] = color;

            triangles[tri] = vertex;
            triangles[tri + 1] = vertex + 1;
            triangles[tri + 2] = vertex + 2;
            triangles[tri + 3] = vertex;
            triangles[tri + 4] = vertex + 2;
            triangles[tri + 5] = vertex + 3;
        }

        Mesh mesh = new Mesh { name = meshName };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(extent.x * 1.8f, extent.y * 1.8f, 4f));
        mesh.MarkDynamic();
        return mesh;
    }

    Mesh BuildOrbitParticleMesh(
        string meshName,
        int count,
        float minRadius,
        float maxRadius,
        float minLength,
        float maxLength,
        float minThickness,
        float maxThickness,
        float ellipseY,
        Color innerColor,
        Color outerColor,
        Color sparkColor,
        System.Random random)
    {
        Vector3[] vertices = new Vector3[count * 4];
        Vector2[] uvs = new Vector2[count * 4];
        Color[] colors = new Color[count * 4];
        int[] triangles = new int[count * 6];
        ellipseY = Mathf.Clamp(ellipseY, 0.25f, 1f);

        for (int i = 0; i < count; i++)
        {
            float radius = NextFloat(random, minRadius, maxRadius);
            float angle = NextFloat(random, 0f, Mathf.PI * 2f);
            float radiusT = Mathf.InverseLerp(minRadius, maxRadius, radius);
            float length = NextFloat(random, minLength, maxLength) * Mathf.Lerp(1.15f, 0.72f, radiusT);
            float thickness = NextFloat(random, minThickness, maxThickness);
            Vector2 center = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * ellipseY);
            Vector2 tangent = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle) * ellipseY).normalized;
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            float radialJitter = NextFloat(random, -0.16f, 0.16f) * radius;
            center += normal * radialJitter;

            Vector2 halfTangent = tangent * (length * 0.5f);
            Vector2 halfNormal = normal * (thickness * 0.5f);
            int vertex = i * 4;
            int tri = i * 6;

            vertices[vertex] = center - halfTangent - halfNormal;
            vertices[vertex + 1] = center - halfTangent + halfNormal;
            vertices[vertex + 2] = center + halfTangent + halfNormal;
            vertices[vertex + 3] = center + halfTangent - halfNormal;

            uvs[vertex] = new Vector2(0f, 0f);
            uvs[vertex + 1] = new Vector2(0f, 1f);
            uvs[vertex + 2] = new Vector2(1f, 1f);
            uvs[vertex + 3] = new Vector2(1f, 0f);

            Color color = NextFloat(random, 0f, 1f) < 0.075f
                ? sparkColor
                : Color.Lerp(innerColor, outerColor, radiusT);
            color.a *= NextFloat(random, 0.55f, 1f);
            colors[vertex] = color;
            colors[vertex + 1] = color;
            colors[vertex + 2] = color;
            colors[vertex + 3] = color;

            triangles[tri] = vertex;
            triangles[tri + 1] = vertex + 1;
            triangles[tri + 2] = vertex + 2;
            triangles[tri + 3] = vertex;
            triangles[tri + 4] = vertex + 2;
            triangles[tri + 5] = vertex + 3;
        }

        Mesh mesh = new Mesh { name = meshName };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        float boundsSize = maxRadius * 2.8f;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(boundsSize, boundsSize, 4f));
        mesh.MarkDynamic();
        return mesh;
    }

    Mesh BuildGravityCoreMesh(string meshName, System.Random random)
    {
        const int count = 8;
        Vector3[] vertices = new Vector3[count * 4];
        Vector2[] uvs = new Vector2[count * 4];
        Color[] colors = new Color[count * 4];
        int[] triangles = new int[count * 6];

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            float size = Mathf.Lerp(4.2f, 15.8f, t * t);
            float width = size * Mathf.Lerp(1f, 1.68f, t);
            float height = size * Mathf.Lerp(1f, 0.52f, t);
            float angle = i == 0 ? 0f : NextFloat(random, -22f, 22f) * Mathf.Deg2Rad;
            Vector3 right = new Vector3(Mathf.Cos(angle) * width * 0.5f, Mathf.Sin(angle) * width * 0.5f, 0f);
            Vector3 up = new Vector3(-Mathf.Sin(angle) * height * 0.5f, Mathf.Cos(angle) * height * 0.5f, 0f);
            Vector3 center = Vector3.zero;
            int vertex = i * 4;
            int tri = i * 6;

            vertices[vertex] = center - right - up;
            vertices[vertex + 1] = center - right + up;
            vertices[vertex + 2] = center + right + up;
            vertices[vertex + 3] = center + right - up;

            uvs[vertex] = new Vector2(0f, 0f);
            uvs[vertex + 1] = new Vector2(0f, 1f);
            uvs[vertex + 2] = new Vector2(1f, 1f);
            uvs[vertex + 3] = new Vector2(1f, 0f);

            Color color = t < 0.48f
                ? Color.Lerp(new Color(0f, 0f, 0.008f, 0.99f), new Color(0.035f, 0.01f, 0.075f, 0.82f), t / 0.48f)
                : Color.Lerp(new Color(0.44f, 0.12f, 0.72f, 0.38f), new Color(1f, 0.58f, 0.08f, 0.2f), (t - 0.48f) / 0.52f);
            colors[vertex] = color;
            colors[vertex + 1] = color;
            colors[vertex + 2] = color;
            colors[vertex + 3] = color;

            triangles[tri] = vertex;
            triangles[tri + 1] = vertex + 1;
            triangles[tri + 2] = vertex + 2;
            triangles[tri + 3] = vertex;
            triangles[tri + 4] = vertex + 2;
            triangles[tri + 5] = vertex + 3;
        }

        Mesh mesh = new Mesh { name = meshName };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(38f, 38f, 4f));
        mesh.MarkDynamic();
        return mesh;
    }

    Color PickParticleColor(LayerStyle style, System.Random random)
    {
        if (style == LayerStyle.Haze)
        {
            Color blue = new Color(0.28f, 0.58f, 1f, 1f);
            Color violet = new Color(0.66f, 0.42f, 1f, 1f);
            Color cyan = new Color(0.36f, 0.9f, 1f, 1f);
            float hazeRoll = NextFloat(random, 0f, 1f);
            Color hazeColor = hazeRoll < 0.5f ? blue : hazeRoll < 0.82f ? violet : cyan;
            hazeColor.a = NextFloat(random, 0.055f, 0.16f);
            return hazeColor;
        }

        if (style == LayerStyle.Dust)
        {
            Color dustColor = new Color(0.7f, 0.86f, 1f, 1f);
            dustColor.a = NextFloat(random, 0.12f, 0.28f);
            return dustColor;
        }

        float starRoll = NextFloat(random, 0f, 1f);
        Color warm = new Color(1f, 0.92f, 0.78f, 1f);
        Color cold = new Color(0.68f, 0.84f, 1f, 1f);
        Color neutral = new Color(0.92f, 0.98f, 1f, 1f);
        Color starColor = starRoll < 0.2f ? warm : starRoll < 0.62f ? cold : neutral;
        starColor.a = NextFloat(random, 0.36f, 1f);
        return starColor;
    }

    void UpdateLayerTransforms()
    {
        Camera camera = ResolveCamera();
        if (camera == null)
            return;

        Vector3 cameraPosition = camera.transform.position;
        float time = Time.time;

        for (int i = 0; i < layers.Length; i++)
        {
            LayerState layer = layers[i];
            if (layer?.Transform == null)
                continue;

            UpdateCameraCoverScale(layer, camera);

            Vector2 driftOffset = layer.Drift * time;
            Vector2 parallaxOffset = new Vector2(cameraPosition.x * layer.Parallax, cameraPosition.y * layer.Parallax);
            if (layer.ClampParallax)
            {
                parallaxOffset.x = Mathf.Clamp(parallaxOffset.x, -layer.MaxParallaxOffset.x, layer.MaxParallaxOffset.x);
                parallaxOffset.y = Mathf.Clamp(parallaxOffset.y, -layer.MaxParallaxOffset.y, layer.MaxParallaxOffset.y);
            }

            Vector2 relativeOffset = layer.BaseOffset - parallaxOffset + driftOffset;
            if (layer.KeepInsideCameraView)
                relativeOffset = ClampOffsetInsideCameraView(layer, camera, relativeOffset);

            layer.Transform.position = new Vector3(
                cameraPosition.x + relativeOffset.x,
                cameraPosition.y + relativeOffset.y,
                0f);

            if (Mathf.Abs(layer.RotationSpeed) > 0.0001f)
                layer.Transform.rotation = Quaternion.Euler(0f, 0f, time * layer.RotationSpeed);

            if (layer.PulseSpriteScale)
            {
                float pulse = 1f + Mathf.Sin(time * GravityWellObjectPulseSpeed + layer.Phase) * GravityWellObjectPulseAmount;
                layer.Transform.localScale = layer.BaseLocalScale * Mathf.Max(0.92f, pulse);
                if (layer.SpriteRenderer != null)
                {
                    float glow = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(time * GravityWellObjectPulseSpeed + layer.Phase));
                    layer.SpriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.78f, 0.42f, 1f), glow * 0.16f);
                }
            }

            if (layer.EdgeEffectTransform != null && Mathf.Abs(layer.EdgeEffectRotationSpeed) > 0.0001f)
                layer.EdgeEffectTransform.localRotation = Quaternion.Euler(0f, 0f, time * layer.EdgeEffectRotationSpeed);
        }
    }

    Camera ResolveCamera()
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            return cachedCamera;

        cachedCamera = Camera.main;
        return cachedCamera;
    }

    static Vector2 ClampOffsetInsideCameraView(LayerState layer, Camera camera, Vector2 relativeOffset)
    {
        if (camera == null || !camera.orthographic || layer?.Transform == null)
            return relativeOffset;

        float visibleHeight = camera.orthographicSize * 2f;
        float visibleWidth = visibleHeight * Mathf.Max(0.01f, camera.aspect);
        Vector3 scale = layer.Transform.localScale;
        float worldWidth = Mathf.Abs(layer.SpriteSize.x * scale.x);
        float worldHeight = Mathf.Abs(layer.SpriteSize.y * scale.y);
        float safetyRadius = Mathf.Max(worldWidth, worldHeight) * 0.5f * 0.82f;
        float margin = Mathf.Max(0.65f, safetyRadius * 0.16f);
        float halfX = Mathf.Max(0f, visibleWidth * 0.5f - safetyRadius + margin);
        float halfY = Mathf.Max(0f, visibleHeight * 0.5f - safetyRadius + margin);
        return new Vector2(
            SmoothClamp(relativeOffset.x, halfX),
            SmoothClamp(relativeOffset.y, halfY));
    }

    static float SmoothClamp(float value, float halfRange)
    {
        if (halfRange <= 0.001f)
            return 0f;

        float normalized = Mathf.Clamp(value / halfRange, -2f, 2f);
        return (float)System.Math.Tanh(normalized) * halfRange;
    }

    static void UpdateCameraCoverScale(LayerState layer, Camera camera)
    {
        if (!layer.ScaleToCameraView || camera == null || !camera.orthographic)
            return;

        Vector2 spriteSize = layer.SpriteSize;
        if (spriteSize.x <= 0.001f || spriteSize.y <= 0.001f)
            return;

        float visibleHeight = camera.orthographicSize * 2f;
        float visibleWidth = visibleHeight * Mathf.Max(0.01f, camera.aspect);
        float overscan = Mathf.Max(1f, layer.ViewportOverscan);
        Vector2 targetSize = new Vector2(
            visibleWidth * overscan + layer.MaxParallaxOffset.x * 2f,
            visibleHeight * overscan + layer.MaxParallaxOffset.y * 2f);
        float coverScale = Mathf.Max(targetSize.x / spriteSize.x, targetSize.y / spriteSize.y);
        layer.Transform.localScale = new Vector3(coverScale, coverScale, 1f);
    }

    void UpdateLayerGlow()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < layers.Length; i++)
        {
            LayerState layer = layers[i];
            if (layer == null)
                continue;

            if (layer.Renderer != null)
            {
                float pulse = 1.12f + Mathf.Sin(Time.time * layer.TwinkleSpeed + layer.Phase) * layer.TwinkleAmount;
                layer.Renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_Color", new Color(pulse, pulse, pulse, 1f));
                layer.Renderer.SetPropertyBlock(propertyBlock);
            }

            if (layer.EdgeEffectRenderer != null)
            {
                float edgePulse = 1.18f + Mathf.Sin(Time.time * 5.3f + layer.Phase) * 0.26f;
                layer.EdgeEffectRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_Color", new Color(edgePulse, edgePulse * 0.82f, edgePulse * 0.42f, 1f));
                layer.EdgeEffectRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }

    void ClearLayers()
    {
        for (int i = 0; i < layers.Length; i++)
        {
            LayerState layer = layers[i];
            if (layer == null)
                continue;

            if (layer.Mesh != null)
                Destroy(layer.Mesh);

            if (layer.EdgeEffectMesh != null)
                Destroy(layer.EdgeEffectMesh);

            if (layer.Root != null)
                Destroy(layer.Root);

            layers[i] = null;
        }

        builtStyle = AdvancedBackgroundStyle.None;
        builtBackgroundObjectId = string.Empty;
    }

    static Material GetSharedMaterial()
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");

        if (shader == null)
            return null;

        sharedMaterial = new Material(shader)
        {
            name = "AdvancedSpaceBackgroundMaterial",
            mainTexture = GetGlowTexture(),
            hideFlags = HideFlags.HideAndDontSave
        };
        sharedMaterial.renderQueue = 2990;
        return sharedMaterial;
    }

    static Sprite LoadNoobHavenBackdropSprite(string backgroundId)
    {
        backgroundId = RoomSettings.NormalizeParallaxBackgroundId(backgroundId);
        if (noobHavenBackdropSprite != null && string.Equals(noobHavenBackdropSpriteId, backgroundId, StringComparison.Ordinal))
            return noobHavenBackdropSprite;

        noobHavenBackdropSprite = null;
        noobHavenBackdropSpriteId = backgroundId;

        string resourcePath = GetNoobHavenBackdropResourcePath(backgroundId);
        noobHavenBackdropSprite = Resources.Load<Sprite>(resourcePath);
        if (noobHavenBackdropSprite != null)
            return noobHavenBackdropSprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture != null)
            return noobHavenBackdropSprite = CreateSpriteFromTexture(texture, "NoobHavenKosmosBackdropSprite");

#if UNITY_EDITOR
        noobHavenBackdropSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(GetNoobHavenBackdropResourceAssetPath(backgroundId));
        if (noobHavenBackdropSprite != null)
            return noobHavenBackdropSprite;

        noobHavenBackdropSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/" + backgroundId + ".png");
        if (noobHavenBackdropSprite != null)
            return noobHavenBackdropSprite;

        texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(GetNoobHavenBackdropResourceAssetPath(backgroundId));
        if (texture == null)
            texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/" + backgroundId + ".png");
        if (texture != null)
            return noobHavenBackdropSprite = CreateSpriteFromTexture(texture, "NoobHavenKosmosBackdropSprite");
#endif

        if (!string.Equals(backgroundId, RoomSettings.DefaultParallaxBackground, StringComparison.Ordinal))
        {
            noobHavenBackdropSpriteId = null;
            return LoadNoobHavenBackdropSprite(RoomSettings.DefaultParallaxBackground);
        }

        return null;
    }

    static Sprite LoadBackgroundObjectSprite(string objectId)
    {
        objectId = RoomSettings.NormalizeBackgroundObjectId(objectId);
        if (string.Equals(objectId, RoomSettings.BackgroundObjectOff, StringComparison.Ordinal))
            return null;

        if (backgroundObjectSprite != null && string.Equals(backgroundObjectSpriteId, objectId, StringComparison.Ordinal))
            return backgroundObjectSprite;

        backgroundObjectSprite = null;
        backgroundObjectSpriteId = objectId;

        string resourcePath = GetBackgroundObjectResourcePath(objectId);
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture != null)
            return backgroundObjectSprite = CreateSpriteFromTexture(texture, "AdvancedBackgroundObjectSprite");

        backgroundObjectSprite = Resources.Load<Sprite>(resourcePath);
        if (backgroundObjectSprite != null)
            return backgroundObjectSprite;

        string directResourcePath = GetBackgroundObjectDirectResourcePath(objectId);
        texture = Resources.Load<Texture2D>(directResourcePath);
        if (texture != null)
            return backgroundObjectSprite = CreateSpriteFromTexture(texture, "AdvancedBackgroundObjectSprite");

        backgroundObjectSprite = Resources.Load<Sprite>(directResourcePath);
        if (backgroundObjectSprite != null)
            return backgroundObjectSprite;

#if UNITY_EDITOR
        texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(GetBackgroundObjectResourceAssetPath(objectId));
        if (texture != null)
            return backgroundObjectSprite = CreateSpriteFromTexture(texture, "AdvancedBackgroundObjectSprite");

        texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(GetBackgroundObjectDirectResourceAssetPath(objectId));
        if (texture != null)
            return backgroundObjectSprite = CreateSpriteFromTexture(texture, "AdvancedBackgroundObjectSprite");

        texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/" + objectId + ".png");
        if (texture != null)
            return backgroundObjectSprite = CreateSpriteFromTexture(texture, "AdvancedBackgroundObjectSprite");

        backgroundObjectSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(GetBackgroundObjectResourceAssetPath(objectId));
        if (backgroundObjectSprite != null)
            return backgroundObjectSprite;

        backgroundObjectSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(GetBackgroundObjectDirectResourceAssetPath(objectId));
        if (backgroundObjectSprite != null)
            return backgroundObjectSprite;

        backgroundObjectSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/" + objectId + ".png");
        if (backgroundObjectSprite != null)
            return backgroundObjectSprite;
#endif

        return null;
    }

    static Sprite LoadToxicAreaCloudSprite()
    {
        if (toxicAreaCloudSprite != null)
            return toxicAreaCloudSprite;

        Texture2D texture = Resources.Load<Texture2D>(ToxicAreaCloudResourcePath);
        if (texture != null)
            return toxicAreaCloudSprite = CreateSpriteFromTexture(texture, "ToxicAreaCloudSprite");

        toxicAreaCloudSprite = Resources.Load<Sprite>(ToxicAreaCloudResourcePath);
        if (toxicAreaCloudSprite != null)
            return toxicAreaCloudSprite;

#if UNITY_EDITOR
        texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/" + ToxicAreaCloudResourcePath + ".png");
        if (texture != null)
            return toxicAreaCloudSprite = CreateSpriteFromTexture(texture, "ToxicAreaCloudSprite");

        texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/background_object 13 addon.png");
        if (texture != null)
            return toxicAreaCloudSprite = CreateSpriteFromTexture(texture, "ToxicAreaCloudSprite");

        toxicAreaCloudSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/" + ToxicAreaCloudResourcePath + ".png");
        if (toxicAreaCloudSprite != null)
            return toxicAreaCloudSprite;
#endif

        return null;
    }

    static string GetBackgroundObjectResourcePath(string objectId)
    {
        return BackgroundObjectResourcePrefix +
               RoomSettings.NormalizeBackgroundObjectId(objectId) +
               BackgroundObjectResourceSuffix;
    }

    static string GetBackgroundObjectDirectResourcePath(string objectId)
    {
        return BackgroundObjectResourcePrefix +
               RoomSettings.NormalizeBackgroundObjectId(objectId);
    }

#if UNITY_EDITOR
    static string GetBackgroundObjectResourceAssetPath(string objectId)
    {
        return "Assets/Resources/" + GetBackgroundObjectResourcePath(objectId) + ".png";
    }

    static string GetBackgroundObjectDirectResourceAssetPath(string objectId)
    {
        return "Assets/Resources/" + GetBackgroundObjectDirectResourcePath(objectId) + ".png";
    }
#endif

    static string GetNoobHavenBackdropResourcePath(string backgroundId)
    {
        return NoobHavenBackdropResourcePrefix +
               RoomSettings.NormalizeParallaxBackgroundId(backgroundId) +
               NoobHavenBackdropResourceSuffix;
    }

#if UNITY_EDITOR
    static string GetNoobHavenBackdropResourceAssetPath(string backgroundId)
    {
        return "Assets/Resources/" + GetNoobHavenBackdropResourcePath(backgroundId) + ".png";
    }
#endif

    static Sprite CreateSpriteFromTexture(Texture2D texture, string spriteName)
    {
        if (texture == null)
            return null;

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0u,
            SpriteMeshType.FullRect);
        sprite.name = spriteName;
        return sprite;
    }

    static Texture2D GetGlowTexture()
    {
        if (glowTexture != null)
            return glowTexture;

        const int size = 64;
        glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "AdvancedSpaceBackgroundSoftGlow",
            hideFlags = HideFlags.HideAndDontSave
        };
        glowTexture.wrapMode = TextureWrapMode.Clamp;
        glowTexture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float core = Mathf.Clamp01(1f - distance * 2.15f);
                float halo = Mathf.Clamp01(1f - distance);
                float alpha = Mathf.Clamp01(core * core + halo * halo * halo * 0.42f);
                glowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        glowTexture.Apply(false, true);
        return glowTexture;
    }

    static int BuildSeed(string mapId, int backgroundIndex, string mapSize, string parallaxBackgroundId)
    {
        unchecked
        {
            int seed = 216613626;
            seed = (seed ^ StableHash(mapId)) * 16777619;
            seed = (seed ^ StableHash(mapSize)) * 16777619;
            seed = (seed ^ backgroundIndex) * 16777619;
            seed = (seed ^ StableHash(parallaxBackgroundId)) * 16777619;
            return seed;
        }
    }

    static int StableHash(string value)
    {
        unchecked
        {
            int hash = 216613626;
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                    hash = (hash ^ value[i]) * 16777619;
            }

            return hash;
        }
    }

    static float NextFloat(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    static float StableLayerPhase(string value)
    {
        return Mathf.Abs(Mathf.Sin(StableHash(value) * 0.00031f)) * Mathf.PI * 2f;
    }
}
