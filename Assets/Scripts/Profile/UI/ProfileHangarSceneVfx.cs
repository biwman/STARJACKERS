using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ProfileHangarSceneVfx : MonoBehaviour
{
    public enum DisplayMode
    {
        Home,
        Inventory
    }

    const int SparkCount = 26;
    const float HomeSpaceWindowWidth = 0.285f;
    const float InventorySpaceWindowWidth = 0.47f;
    const float HangarTextureWidth = 1672f;
    const float HangarTextureHeight = 941f;
    const float InventoryLandingLightXScale = 1.14f;
    const float InventoryLandingLightXOffset = 322f;
    const float InventoryLandingLightYScale = 1.02f;
    const float InventoryLandingLightYOffset = 2f;
    const float SceneFloatZoomAmplitude = 0.065f;
    const float SceneFloatRightDriftMax = 36f;
    // Flip this to false to restore the pre-parallax space window behavior.
    const bool SpaceWindowParallaxEnabled = true;
    const float SpaceWindowBackgroundParallaxStrength = 0.28f;
    const float SpaceWindowBackgroundScale = 1.074f;
    const float SpaceWindowPointerInfluence = 0.24f;
    const float SpaceWindowPointerSmoothSpeed = 3.8f;
    const float SpaceWindowObjectRotationFromParallax = 0.032f;
    const float SpaceWindowObjectIdleRotation = 1.4f;
    static readonly bool DistantShipTrafficEnabled = true;
    static readonly bool SpaceWindowStarLayersEnabled = true;
    const int SpaceWindowFarStarCount = 18;
    const int SpaceWindowNearStarCount = 11;
    const float SpaceWindowFarStarParallaxStrength = 0.10f;
    const float SpaceWindowNearStarParallaxStrength = 0.20f;
    const float DistantShipParallaxStrength = 0.16f;
    // Flip this to false to remove the foreground drone scan pass.
    static readonly bool HangarScanDroneEnabled = true;
    const float HangarScanDroneCycleDuration = 68f;
    const float HangarScanDroneFlyInDuration = 5.2f;
    const float HangarScanDroneScanDuration = 4.4f;
    const float HangarScanDroneFlyOutDuration = 5.4f;
    const float HangarScanDroneHomeSize = 96f;
    const float HangarScanDroneInventorySize = 82f;
    const int LadderPlacementAttemptCount = 18;
    const int LadderPlacementVersion = 2;
    const float LadderScale = 0.70f;
    const float LadderWelderClearance = 16f;
    const float LadderShipOverlapMin = 18f;
    const float LadderShipOverlapMax = 30f;

    sealed class SceneProfile
    {
        public readonly string MapId;
        public readonly string ParallaxBackgroundId;
        public readonly string BackgroundObjectId;

        public SceneProfile(string mapId, string parallaxBackgroundId, string backgroundObjectId)
        {
            MapId = mapId;
            ParallaxBackgroundId = parallaxBackgroundId;
            BackgroundObjectId = backgroundObjectId;
        }
    }

    static Sprite pixelSprite;
    static Sprite softDotSprite;
    static SceneProfile[] sceneProfiles;
    static SceneProfile cachedSceneProfile;
    static int cachedSceneIndex = -1;
    static float cachedObjectRotation;

    readonly struct WelderContactSite
    {
        public readonly Vector2 NormalizedPoint;
        public readonly Vector2 OutwardNormal;

        public WelderContactSite(Vector2 normalizedPoint, Vector2 outwardNormal)
        {
            NormalizedPoint = normalizedPoint;
            OutwardNormal = outwardNormal.normalized;
        }
    }

    static readonly WelderContactSite[] WelderContactSites =
    {
        new WelderContactSite(new Vector2(0.50f, 0.88f), Vector2.up),
        new WelderContactSite(new Vector2(0.34f, 0.58f), new Vector2(-0.72f, 0.68f)),
        new WelderContactSite(new Vector2(0.66f, 0.58f), new Vector2(0.72f, 0.68f)),
        new WelderContactSite(new Vector2(0.38f, 0.34f), new Vector2(-0.56f, -0.82f)),
        new WelderContactSite(new Vector2(0.50f, 0.20f), Vector2.down),
        new WelderContactSite(new Vector2(0.62f, 0.34f), new Vector2(0.56f, -0.82f))
    };

    readonly struct LadderPlacement
    {
        public readonly float SideSign;
        public readonly Vector2 NormalizedPoint;
        public readonly float NormalizedY;

        public LadderPlacement(float sideSign, float normalizedY)
            : this(sideSign, new Vector2(sideSign < 0f ? 0f : 1f, normalizedY))
        {
        }

        public LadderPlacement(float sideSign, Vector2 normalizedPoint)
        {
            SideSign = sideSign < 0f ? -1f : 1f;
            NormalizedPoint = new Vector2(Mathf.Clamp01(normalizedPoint.x), Mathf.Clamp01(normalizedPoint.y));
            NormalizedY = NormalizedPoint.y;
        }
    }

    sealed class SparkItem
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 Velocity;
        public float Phase;
        public float Lifetime;
        public float Length;
    }

    sealed class LandingLight
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 NormalizedPosition;
        public Color BaseColor;
        public float BaseSize;
        public LandingLightColor ColorKind;
        public float Phase;
    }

    enum LandingLightColor
    {
        Yellow,
        Red
    }

    enum LandingLightSize
    {
        Tiny,
        Medium
    }

    readonly struct LandingLightConfig
    {
        public readonly Vector2 NormalizedPosition;
        public readonly LandingLightColor Color;
        public readonly LandingLightSize Size;

        public LandingLightConfig(Vector2 normalizedPosition, LandingLightColor color, LandingLightSize size)
        {
            NormalizedPosition = normalizedPosition;
            Color = color;
            Size = size;
        }
    }

    static readonly LandingLightConfig[] LandingLightConfigs =
    {
        new LandingLightConfig(new Vector2(0.36036f, 0.95382f), LandingLightColor.Yellow, LandingLightSize.Medium),
        new LandingLightConfig(new Vector2(0.63487f, 0.95262f), LandingLightColor.Yellow, LandingLightSize.Medium),
        new LandingLightConfig(new Vector2(0.49615f, 0.95319f), LandingLightColor.Yellow, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.25034f, 0.92183f), LandingLightColor.Yellow, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.26346f, 0.72047f), LandingLightColor.Yellow, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.67045f, 0.66525f), LandingLightColor.Yellow, LandingLightSize.Medium),
        new LandingLightConfig(new Vector2(0.26406f, 0.56161f), LandingLightColor.Yellow, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.34061f, 0.54030f), LandingLightColor.Yellow, LandingLightSize.Medium),
        new LandingLightConfig(new Vector2(0.65254f, 0.53606f), LandingLightColor.Yellow, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.65221f, 0.47512f), LandingLightColor.Yellow, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.26465f, 0.44626f), LandingLightColor.Yellow, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.66956f, 0.35226f), LandingLightColor.Yellow, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.25934f, 0.29436f), LandingLightColor.Yellow, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.25117f, 0.07179f), LandingLightColor.Yellow, LandingLightSize.Medium),
        new LandingLightConfig(new Vector2(0.63611f, 0.05951f), LandingLightColor.Yellow, LandingLightSize.Medium),
        new LandingLightConfig(new Vector2(0.35738f, 0.05745f), LandingLightColor.Yellow, LandingLightSize.Medium),
        new LandingLightConfig(new Vector2(0.49551f, 0.05632f), LandingLightColor.Yellow, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.56850f, 0.95692f), LandingLightColor.Red, LandingLightSize.Medium),
        new LandingLightConfig(new Vector2(0.42015f, 0.95747f), LandingLightColor.Red, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.34868f, 0.77258f), LandingLightColor.Red, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.64264f, 0.76939f), LandingLightColor.Red, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.34149f, 0.47922f), LandingLightColor.Red, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.64384f, 0.24761f), LandingLightColor.Red, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.34809f, 0.24601f), LandingLightColor.Red, LandingLightSize.Tiny),
        new LandingLightConfig(new Vector2(0.41894f, 0.05531f), LandingLightColor.Red, LandingLightSize.Medium),
        new LandingLightConfig(new Vector2(0.56854f, 0.05258f), LandingLightColor.Red, LandingLightSize.Medium)
    };

    static readonly Vector2[] InventoryLandingLightPixelOffsets =
    {
        new Vector2(-4f, -3f),
        new Vector2(-11f, -3f),
        new Vector2(-6f, -3f),
        Vector2.zero,
        Vector2.zero,
        new Vector2(-11f, -2f),
        Vector2.zero,
        new Vector2(-3f, -1f),
        new Vector2(-11f, -2f),
        new Vector2(-10f, -2f),
        Vector2.zero,
        new Vector2(-10f, -1f),
        Vector2.zero,
        Vector2.zero,
        new Vector2(-10f, -2f),
        new Vector2(-3f, -2f),
        new Vector2(-5f, -2f),
        new Vector2(-9f, -3f),
        new Vector2(-5f, -3f),
        new Vector2(-4f, -3f),
        new Vector2(-11f, -3f),
        new Vector2(-3f, -2f),
        new Vector2(-10f, -1f),
        new Vector2(-4f, -1f),
        new Vector2(-4f, -2f),
        new Vector2(-4f, -1f)
    };

    sealed class SpaceStar
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 NormalizedPosition;
        public float Size;
        public float Alpha;
        public float Phase;
    }

    readonly List<SparkItem> sparks = new List<SparkItem>();
    readonly List<LandingLight> landingLights = new List<LandingLight>();
    readonly List<SpaceStar> farSpaceStars = new List<SpaceStar>();
    readonly List<SpaceStar> nearSpaceStars = new List<SpaceStar>();

    RectTransform rootRect;
    RectTransform spaceWindowRect;
    RectTransform spaceBackgroundRect;
    RectTransform farSpaceStarsRect;
    RectTransform nearSpaceStarsRect;
    RectTransform distantShipRootRect;
    RectTransform distantShipTrailRect;
    RectTransform distantShipCoreRect;
    RectTransform mapObjectRect;
    RectTransform hangarRect;
    RectTransform shipShadowRect;
    RectTransform ladderRect;
    RectTransform welderRect;
    RectTransform scanDroneGlowRect;
    RectTransform scanDroneBeamRect;
    RectTransform scanDroneBeamCoreRect;
    RectTransform scanDroneSweepRect;
    RectTransform scanDroneRect;
    RectTransform shipImageRect;
    Image shipImageComponent;
    Image spaceBackgroundImage;
    Image distantShipTrailImage;
    Image distantShipCoreImage;
    Image mapObjectImage;
    Image hangarImage;
    Image shipShadowImage;
    Image ladderImage;
    Image welderImage;
    Image scanDroneGlowImage;
    Image scanDroneBeamImage;
    Image scanDroneBeamCoreImage;
    Image scanDroneSweepImage;
    Image scanDroneImage;

    Sprite homeHangarSprite;
    Sprite inventoryHangarSprite;
    Sprite ladderSprite;
    Sprite welderSprite;
    Sprite scanDroneSprite;

    Vector2 shipBasePosition;
    Vector2 shipBasePanelPosition;
    Vector3 shipBaseScale = Vector3.one;
    Quaternion shipBaseRotation = Quaternion.identity;
    Vector2 shipShadowBasePosition;
    Vector2 shipShadowBasePanelPosition;
    Vector3 shipShadowBaseScale = Vector3.one;
    bool hasShipShadowBase;
    Vector2 ladderBasePosition;
    Vector2 ladderBasePanelPosition;
    Vector3 ladderBaseScale = Vector3.one;
    Quaternion ladderBaseRotation = Quaternion.identity;
    Sprite ladderPlacementShipSprite;
    float ladderSideSign = -1f;
    float ladderContactNormalizedX = 0f;
    float ladderContactNormalizedY = 0.5f;
    int ladderPlacementVersion;
    Vector2 ladderDockTipNormalized = new Vector2(0.5f, 0.96f);
    Vector2 welderBasePosition;
    Vector2 welderBasePanelPosition;
    Vector3 welderBaseScale = Vector3.one;
    Quaternion welderBaseRotation = Quaternion.identity;
    Sprite welderContactShipSprite;
    Vector2 welderContactNormalized = new Vector2(0.5f, 0.88f);
    Vector2 welderContactNormal = Vector2.up;
    float welderContactRotationJitter;
    Vector2 welderTorchNormalized = new Vector2(0.842f, 0.97f);
    bool hasWelderTorchPoint;
    Vector2 spaceBackgroundBasePosition;
    Vector2 mapObjectBasePosition;
    Vector2 smoothedSpaceWindowPointerParallax;
    Vector2 distantShipStartPosition;
    Vector2 distantShipEndPosition;
    float distantShipDuration = 64f;
    float distantShipPhase;
    float distantShipTrailBaseAlpha = 0.30f;
    bool hasDistantShipRoute;
    float scanDroneCycleStartTime;
    bool hasScanDroneCycleStartTime;
    bool scanDroneEffectsActive = true;
    Vector2 cachedSpaceBackgroundRootSize;
    Vector2 cachedSpaceBackgroundWindowSize;
    Sprite cachedSpaceBackgroundSprite;
    DisplayMode cachedSpaceBackgroundMode;
    bool hasCachedSpaceBackgroundLayout;
    bool hasShipBase;
    bool hasLadderBase;
    bool hasLadderPlacement;
    bool hasWelderBase;
    bool hasWelderContact;
    DisplayMode currentMode;
    bool hasMode;
    int selectedSceneIndex = -1;
    SceneProfile selectedSceneProfile;
    float selectedObjectRotation;
    bool built;

    public void Configure(DisplayMode mode, RectTransform shipPreviewRoot, RectTransform shipImage)
    {
        EnsureBuilt();
        RestoreAnimatedTargets();

        shipImageRect = shipImage;
        shipImageComponent = shipImageRect != null ? shipImageRect.GetComponent<Image>() : null;
        if (shipImageRect != null)
        {
            shipBasePosition = shipImageRect.anchoredPosition;
            shipBasePanelPosition = GetRectCenterInRoot(shipImageRect);
            shipBaseScale = shipImageRect.localScale;
            shipBaseRotation = shipImageRect.localRotation;
            hasShipBase = true;
        }

        if (welderRect != null && shipPreviewRoot != null && welderRect.parent != shipPreviewRoot)
            welderRect.SetParent(shipPreviewRoot, false);
        if (ladderRect != null && shipPreviewRoot != null && ladderRect.parent != shipPreviewRoot)
            ladderRect.SetParent(shipPreviewRoot, false);
        if (shipShadowRect != null && shipPreviewRoot != null && shipShadowRect.parent != shipPreviewRoot)
            shipShadowRect.SetParent(shipPreviewRoot, false);
        if (shipPreviewRoot != null)
            SetScanDroneParent(shipPreviewRoot);
        if (shipPreviewRoot != null)
            SetSparkParent(shipPreviewRoot);
        SetDetachedShipEffectsActive(true);

        if (!hasMode || currentMode != mode)
        {
            currentMode = mode;
            hasMode = true;
        }

        EnsureSceneProfileSelected();
        EnsureScanDroneCycleStartTime();
        ApplyModeLayout();
        gameObject.SetActive(true);
    }

    public static void RequestNewSceneProfileForNextMenu()
    {
        cachedSceneProfile = null;
        cachedSceneIndex = -1;
        cachedObjectRotation = 0f;
    }

    public void RestoreAnimatedTargets()
    {
        if (hasShipBase && shipImageRect != null)
        {
            shipImageRect.anchoredPosition = shipBasePosition;
            shipImageRect.localScale = shipBaseScale;
            shipImageRect.localRotation = shipBaseRotation;
        }

        if (hasWelderBase && welderRect != null)
        {
            welderRect.anchoredPosition = welderBasePosition;
            welderRect.localScale = welderBaseScale;
            welderRect.localRotation = welderBaseRotation;
        }

        if (hasLadderBase && ladderRect != null)
        {
            ladderRect.anchoredPosition = ladderBasePosition;
            ladderRect.localScale = ladderBaseScale;
            ladderRect.localRotation = ladderBaseRotation;
        }

        if (hasShipShadowBase && shipShadowRect != null)
        {
            shipShadowRect.anchoredPosition = shipShadowBasePosition;
            shipShadowRect.localScale = shipShadowBaseScale;
            shipShadowRect.localRotation = Quaternion.identity;
        }

        hasShipBase = false;
        hasLadderBase = false;
        hasWelderBase = false;
        hasShipShadowBase = false;
    }

    void Awake()
    {
        EnsureBuilt();
    }

    void OnDisable()
    {
        RestoreAnimatedTargets();
        SetDetachedShipEffectsActive(false);
        hasScanDroneCycleStartTime = false;
    }

    void OnDestroy()
    {
        RestoreAnimatedTargets();
        DestroyDetachedShipEffects();
    }

    void Update()
    {
        EnsureBuilt();
        float time = Time.unscaledTime;
        UpdateSpaceWindow(time);
        RefreshShipAttachmentsIfShipChanged();
        UpdateSceneFloat(time);
        UpdateLandingLights(time);
        UpdateScanDrone(time);
        UpdateWelderAndSparks(time);
    }

    void EnsureBuilt()
    {
        if (built)
            return;

        EnsureSprites();
        LoadSprites();
        rootRect = EnsureRectTransform(gameObject);
        StretchToParent(rootRect);

        GameObject spaceWindowObject = new GameObject("SpaceWindow", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        spaceWindowObject.transform.SetParent(transform, false);
        spaceWindowRect = spaceWindowObject.GetComponent<RectTransform>();
        Image spaceWindowImage = spaceWindowObject.GetComponent<Image>();
        spaceWindowImage.color = new Color(0.01f, 0.015f, 0.028f, 1f);
        spaceWindowImage.raycastTarget = false;

        spaceBackgroundImage = CreateImage(spaceWindowObject.transform, "MapSpaceBackground", null, Color.white);
        spaceBackgroundRect = spaceBackgroundImage.rectTransform;
        spaceBackgroundImage.preserveAspect = true;

        farSpaceStarsRect = BuildSpaceStarLayer(spaceWindowObject.transform, "FarSparseStars", farSpaceStars, SpaceWindowFarStarCount, 401);
        nearSpaceStarsRect = BuildSpaceStarLayer(spaceWindowObject.transform, "NearSparseStars", nearSpaceStars, SpaceWindowNearStarCount, 503);

        GameObject distantShipObject = new GameObject("DistantTrafficShip", typeof(RectTransform));
        distantShipObject.transform.SetParent(spaceWindowObject.transform, false);
        distantShipRootRect = distantShipObject.GetComponent<RectTransform>();
        distantShipRootRect.anchorMin = new Vector2(0.5f, 0.5f);
        distantShipRootRect.anchorMax = new Vector2(0.5f, 0.5f);
        distantShipRootRect.pivot = new Vector2(0.5f, 0.5f);

        distantShipTrailImage = CreateImage(distantShipObject.transform, "DistantTrafficShipTrail", softDotSprite, new Color(0.36f, 0.82f, 1f, 0.30f));
        distantShipTrailRect = distantShipTrailImage.rectTransform;
        distantShipTrailRect.anchorMin = new Vector2(0.5f, 0.5f);
        distantShipTrailRect.anchorMax = new Vector2(0.5f, 0.5f);
        distantShipTrailRect.pivot = new Vector2(1f, 0.5f);
        distantShipTrailRect.anchoredPosition = Vector2.zero;

        distantShipCoreImage = CreateImage(distantShipObject.transform, "DistantTrafficShipCore", softDotSprite, new Color(0.92f, 0.98f, 1f, 0.96f));
        distantShipCoreRect = distantShipCoreImage.rectTransform;
        distantShipCoreRect.anchorMin = new Vector2(0.5f, 0.5f);
        distantShipCoreRect.anchorMax = new Vector2(0.5f, 0.5f);
        distantShipCoreRect.pivot = new Vector2(0.5f, 0.5f);
        distantShipCoreRect.anchoredPosition = Vector2.zero;

        mapObjectImage = CreateImage(spaceWindowObject.transform, "MapSpaceObject", null, new Color(1f, 1f, 1f, 0.68f));
        mapObjectRect = mapObjectImage.rectTransform;
        mapObjectImage.preserveAspect = true;

        hangarImage = CreateImage(transform, "HangarOverlay", null, Color.white);
        hangarRect = hangarImage.rectTransform;
        StretchToParent(hangarRect);
        hangarImage.preserveAspect = false;

        BuildLandingLights(hangarRect);

        shipShadowImage = CreateImage(transform, "ShipSoftShadow", null, new Color(0f, 0f, 0f, 0.32f));
        shipShadowRect = shipShadowImage.rectTransform;
        shipShadowImage.preserveAspect = true;

        ladderImage = CreateImage(transform, "DockingLadder", ladderSprite, Color.white);
        ladderRect = ladderImage.rectTransform;
        ladderImage.preserveAspect = true;

        welderImage = CreateImage(transform, "WelderRig", welderSprite, Color.white);
        welderRect = welderImage.rectTransform;
        welderImage.preserveAspect = true;

        BuildScanDroneEffects(transform);
        BuildSparks(transform);
        built = true;
    }

    void LoadSprites()
    {
        homeHangarSprite = LoadResourceSprite("UI/ProfileHangarVfx/hangar_duzy_runtime");
        inventoryHangarSprite = LoadResourceSprite("UI/ProfileHangarVfx/hangar_maly_runtime");
        ladderSprite = LoadResourceSprite("UI/ProfileHangarVfx/drabinka_runtime");
        welderSprite = LoadResourceSprite("UI/ProfileHangarVfx/spawacz_runtime");
        scanDroneSprite = LoadResourceSprite("UI/ProfileHangarVfx/hangar_scan_drone_runtime");
    }

    void EnsureSceneProfileSelected()
    {
        if (cachedSceneProfile == null)
            SelectRandomSceneProfile();

        selectedSceneIndex = cachedSceneIndex;
        selectedSceneProfile = cachedSceneProfile;
        selectedObjectRotation = cachedObjectRotation;
        ApplySelectedSceneProfile();
    }

    static void SelectRandomSceneProfile()
    {
        SceneProfile[] profiles = GetSceneProfiles();
        if (profiles.Length <= 0)
        {
            cachedSceneIndex = -1;
            cachedSceneProfile = null;
            cachedObjectRotation = 0f;
            return;
        }

        cachedSceneIndex = UnityEngine.Random.Range(0, profiles.Length);
        cachedSceneProfile = profiles[cachedSceneIndex];
        cachedObjectRotation = Mathf.Lerp(-18f, 18f, Seed01(cachedSceneIndex, 5));
    }

    void ApplySelectedSceneProfile()
    {
        if (selectedSceneProfile == null)
            return;

        if (spaceBackgroundImage != null)
            spaceBackgroundImage.sprite = LoadParallaxBackgroundSprite(selectedSceneProfile.ParallaxBackgroundId);
        if (mapObjectImage != null)
        {
            mapObjectImage.sprite = LoadBackgroundObjectSprite(selectedSceneProfile.BackgroundObjectId);
            mapObjectImage.enabled = mapObjectImage.sprite != null;
        }
    }

    void EnsureScanDroneCycleStartTime()
    {
        if (hasScanDroneCycleStartTime)
            return;

        int seedIndex = selectedSceneIndex >= 0 ? selectedSceneIndex : 0;
        scanDroneCycleStartTime = Time.unscaledTime + Mathf.Lerp(7f, 18f, Seed01(seedIndex, 813));
        hasScanDroneCycleStartTime = true;
    }

    void ApplyModeLayout()
    {
        bool inventory = currentMode == DisplayMode.Inventory;
        float windowWidth = inventory ? InventorySpaceWindowWidth : HomeSpaceWindowWidth;
        SetRectAnchors(spaceWindowRect, Vector2.zero, new Vector2(windowWidth, 1f), Vector2.zero, Vector2.zero);

        if (spaceBackgroundRect != null)
            ApplySpaceBackgroundLayout();

        if (mapObjectRect != null)
        {
            mapObjectRect.anchorMin = new Vector2(0.5f, 0.5f);
            mapObjectRect.anchorMax = new Vector2(0.5f, 0.5f);
            mapObjectRect.pivot = new Vector2(0.5f, 0.5f);
            mapObjectBasePosition = inventory ? new Vector2(-52f, -28f) : new Vector2(-26f, -18f);
            mapObjectRect.anchoredPosition = mapObjectBasePosition;
            mapObjectRect.sizeDelta = inventory ? new Vector2(420f, 420f) : new Vector2(300f, 300f);
            mapObjectRect.localRotation = Quaternion.Euler(0f, 0f, selectedObjectRotation);
        }

        ConfigureSpaceStarLayers(inventory);
        ConfigureDistantShipLayout(inventory);

        if (hangarImage != null)
            hangarImage.sprite = inventory ? inventoryHangarSprite : homeHangarSprite;

        ApplyShipShadowLayout();
        ApplyWelderLayout(inventory);
        ApplyLadderLayout(inventory);
        ApplyScanDroneLayout(inventory);
        ApplyLandingLightPositions(inventory);
    }

    void ApplyShipShadowLayout()
    {
        if (shipShadowRect == null || shipImageRect == null)
            return;

        Rect shipRect = GetShipVisibleRect();
        Vector2 imageSize = GetRectSize(shipImageRect, shipImageRect.sizeDelta);
        if (imageSize.x <= 1f || imageSize.y <= 1f)
            imageSize = shipRect.size;

        shipShadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shipShadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shipShadowRect.pivot = new Vector2(0.5f, 0.5f);
        shipShadowRect.sizeDelta = imageSize;
        shipShadowBasePosition = shipBasePosition + new Vector2(20f, -24f);
        shipShadowRect.anchoredPosition = shipShadowBasePosition;
        shipShadowBaseScale = shipBaseScale * 1.055f;
        shipShadowRect.localScale = shipShadowBaseScale;
        shipShadowRect.localRotation = Quaternion.identity;
        shipShadowBasePanelPosition = GetRectCenterInRoot(shipShadowRect);
        hasShipShadowBase = true;

        if (shipShadowImage != null)
        {
            shipShadowImage.sprite = shipImageComponent != null ? shipImageComponent.sprite : null;
            shipShadowImage.enabled = shipShadowImage.sprite != null;
            shipShadowImage.color = new Color(0f, 0f, 0f, currentMode == DisplayMode.Inventory ? 0.30f : 0.34f);
            shipShadowImage.raycastTarget = false;
        }

        PlaceShipShadowBelowShipImage();
    }

    void ApplyWelderLayout(bool inventory)
    {
        if (welderRect == null)
            return;

        welderRect.anchorMin = new Vector2(0.5f, 0.5f);
        welderRect.anchorMax = new Vector2(0.5f, 0.5f);
        welderRect.pivot = new Vector2(0.5f, 0.5f);
        welderRect.sizeDelta = inventory ? new Vector2(78f, 139f) : new Vector2(84f, 150f);
        welderRect.localScale = Vector3.one;

        if (welderImage != null)
            welderImage.sprite = welderSprite;

        EnsureWelderTorchPoint();
        RepositionWelderForCurrentShip();
    }

    void ApplyLadderLayout(bool inventory)
    {
        if (ladderRect == null)
            return;

        ladderRect.anchorMin = new Vector2(0.5f, 0.5f);
        ladderRect.anchorMax = new Vector2(0.5f, 0.5f);
        ladderRect.pivot = new Vector2(0.5f, 0.5f);
        ladderRect.localScale = Vector3.one;

        if (ladderImage != null)
        {
            ladderImage.sprite = ladderSprite;
            ladderImage.enabled = ladderSprite != null;
        }

        if (ladderSprite == null)
        {
            ladderRect.gameObject.SetActive(false);
            hasLadderBase = false;
            return;
        }

        ladderRect.gameObject.SetActive(true);
        RepositionLadderForCurrentShip(inventory);
    }

    void ApplyScanDroneLayout(bool inventory)
    {
        if (scanDroneRect == null)
            return;

        ConfigureCenteredOverlayRect(scanDroneGlowRect);
        ConfigureCenteredOverlayRect(scanDroneBeamRect);
        ConfigureCenteredOverlayRect(scanDroneBeamCoreRect);
        ConfigureCenteredOverlayRect(scanDroneSweepRect);
        ConfigureCenteredOverlayRect(scanDroneRect);

        float droneWidth = inventory ? HangarScanDroneInventorySize : HangarScanDroneHomeSize;
        float droneHeight = droneWidth;
        if (scanDroneSprite != null && scanDroneSprite.rect.width > 1f)
            droneHeight = droneWidth * scanDroneSprite.rect.height / scanDroneSprite.rect.width;

        scanDroneRect.sizeDelta = new Vector2(droneWidth, droneHeight);
        scanDroneRect.localScale = Vector3.one;

        if (scanDroneImage != null)
        {
            scanDroneImage.sprite = scanDroneSprite;
            scanDroneImage.enabled = HangarScanDroneEnabled && scanDroneSprite != null;
            scanDroneImage.preserveAspect = true;
        }

        PlaceScanDroneAboveShipImage();
    }

    void RefreshShipAttachmentsIfShipChanged()
    {
        if ((welderRect == null || !hasWelderBase) && (ladderRect == null || !hasLadderBase))
            return;

        Sprite currentSprite = shipImageComponent != null ? shipImageComponent.sprite : null;
        bool welderUpToDate = welderRect == null || !hasWelderBase || (hasWelderContact && welderContactShipSprite == currentSprite);
        bool ladderUpToDate = ladderRect == null || !hasLadderBase || (hasLadderPlacement && ladderPlacementShipSprite == currentSprite);
        if (welderUpToDate && ladderUpToDate)
            return;

        ApplyShipShadowLayout();
        if (!welderUpToDate)
            RepositionWelderForCurrentShip();
        if (!ladderUpToDate)
            RepositionLadderForCurrentShip(currentMode == DisplayMode.Inventory);
    }

    void RepositionWelderForCurrentShip()
    {
        if (welderRect == null)
            return;

        welderRect.localScale = Vector3.one;
        EnsureWelderContactForCurrentShip();
        welderRect.localRotation = ResolveWelderRotation();
        welderRect.anchoredPosition = CalculateWelderBasePosition();
        welderBasePosition = welderRect.anchoredPosition;
        welderBasePanelPosition = GetRectCenterInRoot(welderRect);
        welderBaseScale = welderRect.localScale;
        welderBaseRotation = welderRect.localRotation;
        hasWelderBase = true;
        PlaceWelderAboveShipImage();
    }

    void RepositionLadderForCurrentShip(bool inventory)
    {
        if (ladderRect == null || ladderSprite == null)
            return;

        Vector2 ladderSize = CalculateLadderSize(inventory);
        LadderPlacement placement = PickLadderPlacement(ladderSize);
        ApplyLadderTransform(placement, ladderSize);

        ladderBasePosition = ladderRect.anchoredPosition;
        ladderBasePanelPosition = GetRectCenterInRoot(ladderRect);
        ladderBaseScale = ladderRect.localScale;
        ladderBaseRotation = ladderRect.localRotation;
        ladderPlacementShipSprite = shipImageComponent != null ? shipImageComponent.sprite : null;
        ladderSideSign = placement.SideSign;
        ladderContactNormalizedX = placement.NormalizedPoint.x;
        ladderContactNormalizedY = placement.NormalizedY;
        ladderPlacementVersion = LadderPlacementVersion;
        hasLadderBase = true;
        hasLadderPlacement = true;

        PlaceLadderAboveShipImage();
        PlaceWelderAboveShipImage();
    }

    Vector2 CalculateLadderSize(bool inventory)
    {
        Rect shipRect = GetShipVisibleRect();
        float shipHeight = Mathf.Max(1f, shipRect.height);
        float length = Mathf.Clamp(
            shipHeight * (inventory ? 0.43f : 0.48f),
            inventory ? 150f : 172f,
            inventory ? 218f : 252f);
        float aspect = ladderSprite != null && ladderSprite.rect.height > 1f
            ? ladderSprite.rect.width / ladderSprite.rect.height
            : 0.39f;
        float width = Mathf.Clamp(length * aspect, inventory ? 54f : 60f, inventory ? 92f : 104f);
        return new Vector2(width, length) * LadderScale;
    }

    LadderPlacement PickLadderPlacement(Vector2 ladderSize)
    {
        Sprite currentSprite = shipImageComponent != null ? shipImageComponent.sprite : null;
        Rect welderBounds = GetRectBoundsInRoot(welderRect);
        bool hasWelderBounds = welderRect != null &&
                               welderRect.gameObject.activeInHierarchy &&
                               welderBounds.width > 1f &&
                               welderBounds.height > 1f;

        LadderPlacement bestPlacement = new LadderPlacement(UnityEngine.Random.value < 0.5f ? -1f : 1f, UnityEngine.Random.Range(0.08f, 0.92f));
        float bestOverlap = float.PositiveInfinity;
        bool hasBestPlacement = false;

        if (hasLadderPlacement && ladderPlacementVersion == LadderPlacementVersion && ladderPlacementShipSprite == currentSprite)
        {
            LadderPlacement storedPlacement = new LadderPlacement(ladderSideSign, new Vector2(ladderContactNormalizedX, ladderContactNormalizedY));
            float storedOverlap = EvaluateLadderPlacement(storedPlacement, ladderSize, hasWelderBounds, welderBounds);
            if (storedOverlap <= 0f)
                return storedPlacement;

            bestPlacement = storedPlacement;
            bestOverlap = storedOverlap;
            hasBestPlacement = true;
        }

        float firstSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        for (int i = 0; i < LadderPlacementAttemptCount; i++)
        {
            float sideSign = (i & 1) == 0 ? firstSide : -firstSide;
            LadderPlacement candidate = CreateLadderPlacementCandidate(currentSprite, sideSign, UnityEngine.Random.Range(0.02f, 0.98f));
            float overlap = EvaluateLadderPlacement(candidate, ladderSize, hasWelderBounds, welderBounds);
            if (overlap <= 0f)
                return candidate;

            if (!hasBestPlacement || overlap < bestOverlap)
            {
                bestPlacement = candidate;
                bestOverlap = overlap;
                hasBestPlacement = true;
            }
        }

        for (int sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            float sideSign = sideIndex == 0 ? firstSide : -firstSide;
            for (int yIndex = 0; yIndex < 9; yIndex++)
            {
                LadderPlacement candidate = CreateLadderPlacementCandidate(currentSprite, sideSign, (yIndex + 0.5f) / 9f);
                float overlap = EvaluateLadderPlacement(candidate, ladderSize, hasWelderBounds, welderBounds);
                if (overlap <= 0f)
                    return candidate;

                if (!hasBestPlacement || overlap < bestOverlap)
                {
                    bestPlacement = candidate;
                    bestOverlap = overlap;
                    hasBestPlacement = true;
                }
            }
        }

        return bestPlacement;
    }

    LadderPlacement CreateLadderPlacementCandidate(Sprite sprite, float sideSign, float fallbackNormalizedY)
    {
        return TryPickLadderSideContact(sprite, sideSign, fallbackNormalizedY, out Vector2 normalizedPoint)
            ? new LadderPlacement(sideSign, normalizedPoint)
            : new LadderPlacement(sideSign, sideSign < 0f ? new Vector2(0.28f, fallbackNormalizedY) : new Vector2(0.72f, fallbackNormalizedY));
    }

    bool TryPickLadderSideContact(Sprite sprite, float sideSign, float preferredNormalizedY, out Vector2 normalizedPoint)
    {
        normalizedPoint = Vector2.zero;
        if (sprite == null)
            return false;

        List<Vector2> sidePoints = new List<Vector2>();
        List<Vector2> shape = new List<Vector2>();
        int shapeCount = sprite.GetPhysicsShapeCount();
        for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
        {
            shape.Clear();
            sprite.GetPhysicsShape(shapeIndex, shape);
            AddLadderSideShapeSamples(sprite, shape, sideSign, sidePoints);
        }

        if (sidePoints.Count == 0 && sprite.vertices != null)
        {
            for (int i = 0; i < sprite.vertices.Length; i++)
                AddLadderSideSample(sprite, sprite.vertices[i], sideSign, sidePoints);
        }

        if (sidePoints.Count == 0)
            return false;

        normalizedPoint = SelectLadderSidePoint(sidePoints, sideSign, preferredNormalizedY);
        return true;
    }

    static void AddLadderSideShapeSamples(Sprite sprite, List<Vector2> shape, float sideSign, List<Vector2> sidePoints)
    {
        if (shape == null || shape.Count == 0)
            return;

        for (int i = 0; i < shape.Count; i++)
        {
            Vector2 start = shape[i];
            Vector2 end = shape[(i + 1) % shape.Count];
            AddLadderSideSample(sprite, start, sideSign, sidePoints);
            AddLadderSideSample(sprite, Vector2.Lerp(start, end, 0.25f), sideSign, sidePoints);
            AddLadderSideSample(sprite, Vector2.Lerp(start, end, 0.50f), sideSign, sidePoints);
            AddLadderSideSample(sprite, Vector2.Lerp(start, end, 0.75f), sideSign, sidePoints);
        }
    }

    static void AddLadderSideSample(Sprite sprite, Vector2 point, float sideSign, List<Vector2> sidePoints)
    {
        if (!TryNormalizeSpritePoint(sprite, point, out Vector2 normalized))
            return;

        if (normalized.y < 0.12f || normalized.y > 0.88f)
            return;

        if (sideSign < 0f && normalized.x > 0.52f)
            return;
        if (sideSign > 0f && normalized.x < 0.48f)
            return;

        Vector2 radial = Vector2.Scale(normalized - new Vector2(0.5f, 0.5f), GetSpriteAspectScale(sprite));
        if (radial.sqrMagnitude < 0.035f)
            return;

        sidePoints.Add(normalized);
    }

    static Vector2 SelectLadderSidePoint(List<Vector2> sidePoints, float sideSign, float preferredNormalizedY)
    {
        Vector2 bestPoint = sidePoints[0];
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < sidePoints.Count; i++)
        {
            Vector2 candidate = sidePoints[i];
            float edgeScore = sideSign < 0f ? candidate.x : 1f - candidate.x;
            float yScore = Mathf.Abs(candidate.y - preferredNormalizedY);
            float score = yScore * 1.7f + edgeScore * 0.42f;
            if (score < bestScore)
            {
                bestScore = score;
                bestPoint = candidate;
            }
        }

        return new Vector2(
            Mathf.Clamp(bestPoint.x, 0.04f, 0.96f),
            Mathf.Clamp(bestPoint.y + UnityEngine.Random.Range(-0.018f, 0.018f), 0.12f, 0.88f));
    }

    float EvaluateLadderPlacement(LadderPlacement placement, Vector2 ladderSize, bool hasWelderBounds, Rect welderBounds)
    {
        ApplyLadderTransform(placement, ladderSize);
        if (!hasWelderBounds)
            return 0f;

        Rect ladderBounds = ExpandRect(GetRectBoundsInRoot(ladderRect), LadderWelderClearance);
        return CalculateOverlapArea(ladderBounds, welderBounds);
    }

    void ApplyLadderTransform(LadderPlacement placement, Vector2 ladderSize)
    {
        if (ladderRect == null)
            return;

        ladderRect.anchorMin = new Vector2(0.5f, 0.5f);
        ladderRect.anchorMax = new Vector2(0.5f, 0.5f);
        ladderRect.pivot = new Vector2(0.5f, 0.5f);
        ladderRect.sizeDelta = ladderSize;
        ladderRect.localScale = Vector3.one;
        ladderRect.localRotation = Quaternion.Euler(0f, 0f, placement.SideSign < 0f ? -90f : 90f);
        ladderRect.anchoredPosition = CalculateLadderBasePosition(placement, ladderSize);
    }

    Vector2 CalculateLadderBasePosition(LadderPlacement placement, Vector2 ladderSize)
    {
        Vector2 contact = CalculateLadderShipContactPoint(placement);
        float overlap = Mathf.Clamp(ladderSize.y * 0.16f, LadderShipOverlapMin, LadderShipOverlapMax);
        Vector2 targetTip = contact + new Vector2(-placement.SideSign * overlap, 0f);
        return targetTip - GetLadderTipOffset(Vector3.one);
    }

    Vector2 CalculateLadderShipContactPoint(LadderPlacement placement)
    {
        Rect shipRect = GetShipVisibleRect();
        return new Vector2(
            Mathf.Lerp(shipRect.xMin, shipRect.xMax, placement.NormalizedPoint.x),
            Mathf.Lerp(shipRect.yMin, shipRect.yMax, placement.NormalizedPoint.y));
    }

    Vector2 GetLadderTipOffset(Vector3 scale)
    {
        if (ladderRect == null)
            return Vector2.zero;

        Vector2 tipOffset = new Vector2(
            (ladderDockTipNormalized.x - 0.5f) * ladderRect.sizeDelta.x,
            (ladderDockTipNormalized.y - 0.5f) * ladderRect.sizeDelta.y);
        Vector3 scaledOffset = new Vector3(tipOffset.x * scale.x, tipOffset.y * scale.y, 0f);
        Vector3 rotatedOffset = ladderRect.localRotation * scaledOffset;
        return new Vector2(rotatedOffset.x, rotatedOffset.y);
    }

    void EnsureWelderContactForCurrentShip()
    {
        Sprite currentSprite = shipImageComponent != null ? shipImageComponent.sprite : null;
        if (hasWelderContact && welderContactShipSprite == currentSprite)
            return;

        welderContactShipSprite = currentSprite;
        if (!TryPickSpritePhysicsShapeContact(currentSprite, out welderContactNormalized, out welderContactNormal) &&
            !TryPickSpriteOutlineContact(currentSprite, out welderContactNormalized, out welderContactNormal))
        {
            int siteIndex = UnityEngine.Random.Range(0, WelderContactSites.Length);
            WelderContactSite site = WelderContactSites[siteIndex];
            welderContactNormalized = site.NormalizedPoint + new Vector2(
                UnityEngine.Random.Range(-0.025f, 0.025f),
                UnityEngine.Random.Range(-0.025f, 0.025f));
            welderContactNormalized = new Vector2(
                Mathf.Clamp(welderContactNormalized.x, 0.16f, 0.84f),
                Mathf.Clamp(welderContactNormalized.y, 0.16f, 0.90f));
            welderContactNormal = site.OutwardNormal.sqrMagnitude > 0.001f
                ? site.OutwardNormal
                : (welderContactNormalized - new Vector2(0.5f, 0.5f)).normalized;
        }

        welderContactRotationJitter = 0f;
        hasWelderContact = true;
    }

    void EnsureWelderTorchPoint()
    {
        if (hasWelderTorchPoint)
            return;

        if (!TryFindTopRightSpritePoint(welderSprite, out welderTorchNormalized))
            welderTorchNormalized = new Vector2(0.842f, 0.97f);

        hasWelderTorchPoint = true;
    }

    bool TryPickSpritePhysicsShapeContact(Sprite sprite, out Vector2 normalizedPoint, out Vector2 outwardNormal)
    {
        normalizedPoint = Vector2.zero;
        outwardNormal = Vector2.up;
        if (sprite == null || sprite.GetPhysicsShapeCount() <= 0)
            return false;

        List<Vector2> candidates = new List<Vector2>();
        List<Vector2> shape = new List<Vector2>();
        for (int shapeIndex = 0; shapeIndex < sprite.GetPhysicsShapeCount(); shapeIndex++)
        {
            shape.Clear();
            sprite.GetPhysicsShape(shapeIndex, shape);
            if (shape.Count <= 0)
                continue;

            for (int i = 0; i < shape.Count; i++)
            {
                AddContactCandidate(sprite, shape[i], candidates, 0.02f, 0.98f);

                Vector2 next = shape[(i + 1) % shape.Count];
                AddContactCandidate(sprite, Vector2.Lerp(shape[i], next, 0.33f), candidates, 0.02f, 0.98f);
                AddContactCandidate(sprite, Vector2.Lerp(shape[i], next, 0.66f), candidates, 0.02f, 0.98f);
            }
        }

        if (candidates.Count == 0)
            return false;

        normalizedPoint = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        outwardNormal = CalculateOutwardNormal(sprite, normalizedPoint);
        return true;
    }

    bool TryPickSpriteOutlineContact(Sprite sprite, out Vector2 normalizedPoint, out Vector2 outwardNormal)
    {
        normalizedPoint = Vector2.zero;
        outwardNormal = Vector2.up;
        if (sprite == null || sprite.vertices == null || sprite.vertices.Length == 0)
            return false;

        Rect spriteRect = sprite.rect;
        if (spriteRect.width <= 1f || spriteRect.height <= 1f || sprite.pixelsPerUnit <= 0.001f)
            return false;

        List<Vector2> candidates = new List<Vector2>();
        Vector2 center = new Vector2(0.5f, 0.5f);
        Vector2 aspect = new Vector2(spriteRect.width / Mathf.Max(1f, spriteRect.height), 1f);
        Vector2[] vertices = sprite.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (!TryNormalizeSpritePoint(sprite, vertices[i], out Vector2 normalized))
                continue;

            Vector2 radial = Vector2.Scale(normalized - center, aspect);
            if (radial.sqrMagnitude < 0.11f)
                continue;
            if (normalized.x < 0.06f || normalized.x > 0.94f || normalized.y < 0.06f || normalized.y > 0.94f)
                continue;

            candidates.Add(normalized);
        }

        if (candidates.Count == 0)
            return false;

        normalizedPoint = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        outwardNormal = CalculateOutwardNormal(sprite, normalizedPoint);
        return true;
    }

    static void AddContactCandidate(Sprite sprite, Vector2 point, List<Vector2> candidates, float minEdge, float maxEdge)
    {
        if (!TryNormalizeSpritePoint(sprite, point, out Vector2 normalized))
            return;

        Vector2 radial = Vector2.Scale(normalized - new Vector2(0.5f, 0.5f), GetSpriteAspectScale(sprite));
        if (radial.sqrMagnitude < 0.085f)
            return;
        if (normalized.x < minEdge || normalized.x > maxEdge || normalized.y < minEdge || normalized.y > maxEdge)
            return;

        candidates.Add(normalized);
    }

    static bool TryFindTopRightSpritePoint(Sprite sprite, out Vector2 normalizedPoint)
    {
        normalizedPoint = Vector2.zero;
        if (sprite == null)
            return false;

        bool found = false;
        float bestScore = float.NegativeInfinity;
        List<Vector2> shape = new List<Vector2>();
        int shapeCount = sprite.GetPhysicsShapeCount();
        for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
        {
            shape.Clear();
            sprite.GetPhysicsShape(shapeIndex, shape);
            for (int i = 0; i < shape.Count; i++)
                ConsiderTopRightPoint(sprite, shape[i], ref found, ref bestScore, ref normalizedPoint);
        }

        if (found)
            return !IsLikelyTransparentTextureCorner(normalizedPoint);

        Vector2[] vertices = sprite.vertices;
        if (vertices == null)
            return false;

        for (int i = 0; i < vertices.Length; i++)
            ConsiderTopRightPoint(sprite, vertices[i], ref found, ref bestScore, ref normalizedPoint);

        return found && !IsLikelyTransparentTextureCorner(normalizedPoint);
    }

    static bool IsLikelyTransparentTextureCorner(Vector2 normalizedPoint)
    {
        return normalizedPoint.x > 0.965f && normalizedPoint.y > 0.965f;
    }

    static void ConsiderTopRightPoint(Sprite sprite, Vector2 point, ref bool found, ref float bestScore, ref Vector2 bestPoint)
    {
        if (!TryNormalizeSpritePoint(sprite, point, out Vector2 normalized))
            return;

        float score = normalized.x - (1f - normalized.y);
        if (!found || score > bestScore)
        {
            found = true;
            bestScore = score;
            bestPoint = normalized;
        }
    }

    static bool TryNormalizeSpritePoint(Sprite sprite, Vector2 point, out Vector2 normalized)
    {
        normalized = Vector2.zero;
        if (sprite == null)
            return false;

        Rect spriteRect = sprite.rect;
        if (spriteRect.width <= 1f || spriteRect.height <= 1f || sprite.pixelsPerUnit <= 0.001f)
            return false;

        normalized = new Vector2(
            (point.x * sprite.pixelsPerUnit + sprite.pivot.x) / spriteRect.width,
            (point.y * sprite.pixelsPerUnit + sprite.pivot.y) / spriteRect.height);
        return normalized.x >= -0.05f && normalized.x <= 1.05f && normalized.y >= -0.05f && normalized.y <= 1.05f;
    }

    static Vector2 CalculateOutwardNormal(Sprite sprite, Vector2 normalizedPoint)
    {
        Vector2 normal = Vector2.Scale(normalizedPoint - new Vector2(0.5f, 0.5f), GetSpriteAspectScale(sprite));
        return normal.sqrMagnitude > 0.001f ? normal.normalized : Vector2.up;
    }

    static Vector2 GetSpriteAspectScale(Sprite sprite)
    {
        if (sprite == null || sprite.rect.height <= 1f)
            return Vector2.one;

        return new Vector2(sprite.rect.width / Mathf.Max(1f, sprite.rect.height), 1f);
    }

    Vector2 CalculateWelderBasePosition()
    {
        Vector2 contact = CalculateShipContactPoint();
        return contact - GetWelderTorchOffset(Vector3.one);
    }

    Vector2 CalculateShipContactPoint()
    {
        Rect shipRect = GetShipVisibleRect();
        return new Vector2(
            Mathf.Lerp(shipRect.xMin, shipRect.xMax, welderContactNormalized.x),
            Mathf.Lerp(shipRect.yMin, shipRect.yMax, welderContactNormalized.y));
    }

    Quaternion ResolveWelderRotation()
    {
        if (welderRect == null)
            return Quaternion.identity;

        Vector2 centerFromTorch = -GetWelderTorchOffset(Vector3.one);
        float baseAngle = Mathf.Atan2(centerFromTorch.y, centerFromTorch.x) * Mathf.Rad2Deg;
        Vector2 normal = GetWelderContactNormal();
        float targetAngle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, 0f, targetAngle - baseAngle + welderContactRotationJitter);
    }

    Vector2 GetWelderContactNormal()
    {
        return welderContactNormal.sqrMagnitude > 0.001f ? welderContactNormal.normalized : Vector2.up;
    }

    Rect GetShipVisibleRect()
    {
        Vector2 imageSize = shipImageRect != null
            ? GetRectSize(shipImageRect, shipImageRect.sizeDelta)
            : new Vector2(980f, 756f);
        if (imageSize.x <= 1f || imageSize.y <= 1f)
            imageSize = new Vector2(980f, 756f);

        Sprite sprite = shipImageComponent != null ? shipImageComponent.sprite : null;
        if (sprite != null && sprite.rect.width > 1f && sprite.rect.height > 1f)
        {
            float spriteAspect = sprite.rect.width / sprite.rect.height;
            float imageAspect = imageSize.x / imageSize.y;
            if (imageAspect > spriteAspect)
                imageSize.x = imageSize.y * spriteAspect;
            else
                imageSize.y = imageSize.x / spriteAspect;
        }

        return new Rect(
            shipBasePosition.x - imageSize.x * 0.5f,
            shipBasePosition.y - imageSize.y * 0.5f,
            imageSize.x,
            imageSize.y);
    }

    void PlaceWelderAboveShipImage()
    {
        if (welderRect == null)
            return;

        if (shipImageRect != null && welderRect.parent == shipImageRect.parent)
        {
            int shipIndex = shipImageRect.GetSiblingIndex();
            int targetIndex = shipIndex + 1;
            if (ladderRect != null && ladderRect.parent == welderRect.parent)
                targetIndex = Mathf.Max(targetIndex, ladderRect.GetSiblingIndex() + 1);
            welderRect.SetSiblingIndex(Mathf.Min(targetIndex, welderRect.parent.childCount - 1));
            return;
        }

        welderRect.SetAsLastSibling();
    }

    void PlaceLadderAboveShipImage()
    {
        if (ladderRect == null)
            return;

        if (shipImageRect != null && ladderRect.parent == shipImageRect.parent)
        {
            int shipIndex = shipImageRect.GetSiblingIndex();
            ladderRect.SetSiblingIndex(Mathf.Min(shipIndex + 1, ladderRect.parent.childCount - 1));
            return;
        }

        ladderRect.SetAsLastSibling();
    }

    void PlaceShipShadowBelowShipImage()
    {
        if (shipShadowRect == null)
            return;

        if (shipImageRect != null && shipShadowRect.parent == shipImageRect.parent)
        {
            int shipIndex = shipImageRect.GetSiblingIndex();
            shipShadowRect.SetSiblingIndex(Mathf.Max(0, shipIndex));
            return;
        }

        shipShadowRect.SetAsFirstSibling();
    }

    void PlaceScanDroneAboveShipImage()
    {
        if (scanDroneRect == null)
            return;

        if (scanDroneGlowRect != null)
            scanDroneGlowRect.SetAsLastSibling();
        if (scanDroneBeamRect != null)
            scanDroneBeamRect.SetAsLastSibling();
        if (scanDroneBeamCoreRect != null)
            scanDroneBeamCoreRect.SetAsLastSibling();
        if (scanDroneSweepRect != null)
            scanDroneSweepRect.SetAsLastSibling();
        scanDroneRect.SetAsLastSibling();
    }

    void BuildLandingLights(Transform parent)
    {
        for (int i = 0; i < LandingLightConfigs.Length; i++)
        {
            LandingLightConfig config = LandingLightConfigs[i];
            Image image = CreateImage(parent, "LandingPadLight_" + i, softDotSprite, new Color(1f, 0.82f, 0.45f, 0.62f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            landingLights.Add(new LandingLight
            {
                Rect = rect,
                Image = image,
                NormalizedPosition = config.NormalizedPosition,
                BaseColor = ResolveLandingLightColor(config.Color),
                BaseSize = ResolveLandingLightSize(config.Size),
                ColorKind = config.Color,
                Phase = Seed01(i, 83) * Mathf.PI * 2f
            });
        }
    }

    void ApplyLandingLightPositions(bool inventory)
    {
        for (int i = 0; i < landingLights.Count; i++)
        {
            LandingLight light = landingLights[i];
            if (light == null)
                continue;

            LandingLightConfig config = LandingLightConfigs[i % LandingLightConfigs.Length];
            light.NormalizedPosition = ResolveLandingLightPosition(config.NormalizedPosition, inventory, i);
            light.BaseColor = ResolveLandingLightColor(config.Color);
            light.BaseSize = ResolveLandingLightSize(config.Size);
            light.ColorKind = config.Color;
        }
    }

    static Vector2 ResolveLandingLightPosition(Vector2 homePosition, bool inventory, int index)
    {
        if (!inventory)
            return homePosition;

        float homeX = homePosition.x * HangarTextureWidth;
        float homeY = (1f - homePosition.y) * HangarTextureHeight;
        float inventoryX = InventoryLandingLightXOffset + homeX * InventoryLandingLightXScale;
        float inventoryY = InventoryLandingLightYOffset + homeY * InventoryLandingLightYScale;
        if (index >= 0 && index < InventoryLandingLightPixelOffsets.Length)
        {
            inventoryX += InventoryLandingLightPixelOffsets[index].x;
            inventoryY += InventoryLandingLightPixelOffsets[index].y;
        }
        return new Vector2(
            Mathf.Clamp01(inventoryX / HangarTextureWidth),
            Mathf.Clamp01(1f - inventoryY / HangarTextureHeight));
    }

    static Color ResolveLandingLightColor(LandingLightColor color)
    {
        return color == LandingLightColor.Red
            ? new Color(1f, 0.18f, 0.13f, 0.72f)
            : new Color(1f, 0.84f, 0.26f, 0.70f);
    }

    static float ResolveLandingLightSize(LandingLightSize size)
    {
        return size == LandingLightSize.Medium ? 30f : 14f;
    }

    void BuildScanDroneEffects(Transform parent)
    {
        scanDroneGlowImage = CreateImage(parent, "HangarScanDroneTargetGlow", softDotSprite, new Color(0.22f, 0.84f, 1f, 0f));
        scanDroneGlowRect = scanDroneGlowImage.rectTransform;

        scanDroneBeamImage = CreateImage(parent, "HangarScanDroneBeam", softDotSprite, new Color(0.18f, 0.82f, 1f, 0f));
        scanDroneBeamRect = scanDroneBeamImage.rectTransform;

        scanDroneBeamCoreImage = CreateImage(parent, "HangarScanDroneBeamCore", pixelSprite, new Color(0.66f, 0.96f, 1f, 0f));
        scanDroneBeamCoreRect = scanDroneBeamCoreImage.rectTransform;

        scanDroneSweepImage = CreateImage(parent, "HangarScanDroneSweep", pixelSprite, new Color(0.46f, 0.95f, 1f, 0f));
        scanDroneSweepRect = scanDroneSweepImage.rectTransform;

        scanDroneImage = CreateImage(parent, "HangarScanDrone", scanDroneSprite, Color.white);
        scanDroneRect = scanDroneImage.rectTransform;
        scanDroneImage.preserveAspect = true;

        ConfigureCenteredOverlayRect(scanDroneGlowRect);
        ConfigureCenteredOverlayRect(scanDroneBeamRect);
        ConfigureCenteredOverlayRect(scanDroneBeamCoreRect);
        ConfigureCenteredOverlayRect(scanDroneSweepRect);
        ConfigureCenteredOverlayRect(scanDroneRect);
        SetScanDroneEffectsActive(false);
    }

    void BuildSparks(Transform parent)
    {
        for (int i = 0; i < SparkCount; i++)
        {
            Image image = CreateImage(parent, "WelderSpark_" + i, pixelSprite, new Color(1f, 0.72f, 0.18f, 0.85f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            sparks.Add(new SparkItem
            {
                Rect = rect,
                Image = image,
                Velocity = new Vector2(Mathf.Lerp(42f, 170f, Seed01(i, 101)), Mathf.Lerp(-110f, 112f, Seed01(i, 103))),
                Phase = Seed01(i, 107),
                Lifetime = Mathf.Lerp(0.28f, 0.72f, Seed01(i, 109)),
                Length = Mathf.Lerp(8f, 28f, Seed01(i, 113))
            });
        }
    }

    RectTransform BuildSpaceStarLayer(Transform parent, string layerName, List<SpaceStar> stars, int count, int seedChannel)
    {
        GameObject layerObject = new GameObject(layerName, typeof(RectTransform));
        layerObject.transform.SetParent(parent, false);
        RectTransform layerRect = layerObject.GetComponent<RectTransform>();
        layerRect.anchorMin = new Vector2(0.5f, 0.5f);
        layerRect.anchorMax = new Vector2(0.5f, 0.5f);
        layerRect.pivot = new Vector2(0.5f, 0.5f);
        layerRect.anchoredPosition = Vector2.zero;
        layerRect.sizeDelta = Vector2.zero;

        for (int i = 0; i < count; i++)
        {
            Image image = CreateImage(layerObject.transform, layerName + "_Star_" + i, softDotSprite, new Color(0.82f, 0.94f, 1f, 0.35f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            stars.Add(new SpaceStar
            {
                Rect = rect,
                Image = image,
                Phase = Seed01(i, seedChannel) * 100f
            });
        }

        return layerRect;
    }

    void ConfigureSpaceStarLayers(bool inventory)
    {
        bool active = SpaceWindowStarLayersEnabled;
        if (farSpaceStarsRect != null)
            SetGameObjectActiveIfChanged(farSpaceStarsRect.gameObject, active);
        if (nearSpaceStarsRect != null)
            SetGameObjectActiveIfChanged(nearSpaceStarsRect.gameObject, active);
        if (!active)
            return;

        Vector2 rootSize = GetRectSize(rootRect, new Vector2(1920f, 1080f));
        int seedIndex = selectedSceneIndex >= 0 ? selectedSceneIndex : 0;

        ConfigureSpaceStarLayer(farSpaceStars, rootSize, seedIndex, 601, false);
        ConfigureSpaceStarLayer(nearSpaceStars, rootSize, seedIndex, 709, true);
    }

    void ConfigureSpaceStarLayer(List<SpaceStar> stars, Vector2 fieldSize, int sceneSeed, int seedChannel, bool nearLayer)
    {
        float halfWidth = Mathf.Max(160f, fieldSize.x * 0.5f);
        float halfHeight = Mathf.Max(260f, fieldSize.y * 0.5f);
        float visibleLeftX = -halfWidth;
        float visibleRightX = -halfWidth + fieldSize.x * InventorySpaceWindowWidth;
        Vector2 homeObjectCenter = GetMapObjectFieldPosition(false, fieldSize);
        Vector2 inventoryObjectCenter = GetMapObjectFieldPosition(true, fieldSize);
        float homeSafeHalfX = Mathf.Max(150f, 300f * 0.62f);
        float homeSafeHalfY = Mathf.Max(150f, 300f * 0.62f);
        float inventorySafeHalfX = Mathf.Max(150f, 420f * 0.62f);
        float inventorySafeHalfY = Mathf.Max(150f, 420f * 0.62f);

        for (int i = 0; i < stars.Count; i++)
        {
            SpaceStar star = stars[i];
            if (star == null || star.Rect == null)
                continue;

            Vector2 position = Vector2.zero;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int starSeed = sceneSeed * 83 + i * 17 + attempt * 29 + (nearLayer ? 1200 : 0);
                float x = Mathf.Lerp(visibleLeftX + 24f, visibleRightX - 24f, Seed01(starSeed, seedChannel + 1));
                float y = Mathf.Lerp(-halfHeight + 52f, halfHeight - 52f, Seed01(starSeed, seedChannel + 3));
                position = new Vector2(x, y);
                bool overlapsMapObject =
                    IsInsideSafeArea(position, homeObjectCenter, homeSafeHalfX, homeSafeHalfY) ||
                    IsInsideSafeArea(position, inventoryObjectCenter, inventorySafeHalfX, inventorySafeHalfY);
                if (!overlapsMapObject)
                    break;
            }

            star.NormalizedPosition = new Vector2(
                Mathf.InverseLerp(visibleLeftX, visibleRightX, position.x),
                Mathf.InverseLerp(-halfHeight, halfHeight, position.y));
            star.Size = nearLayer
                ? Mathf.Lerp(2.7f, 5.2f, Seed01(sceneSeed * 97 + i, seedChannel + 5))
                : Mathf.Lerp(1.5f, 3.2f, Seed01(sceneSeed * 97 + i, seedChannel + 5));
            star.Alpha = nearLayer
                ? Mathf.Lerp(0.30f, 0.58f, Seed01(sceneSeed * 97 + i, seedChannel + 7))
                : Mathf.Lerp(0.18f, 0.40f, Seed01(sceneSeed * 97 + i, seedChannel + 7));
            star.Phase = Seed01(sceneSeed * 97 + i, seedChannel + 9) * 100f;

            star.Rect.anchoredPosition = position;
            star.Rect.sizeDelta = Vector2.one * star.Size;
            star.Rect.localScale = Vector3.one;
            star.Rect.localRotation = Quaternion.identity;
        }
    }

    Vector2 GetMapObjectFieldPosition(bool inventory, Vector2 fieldSize)
    {
        float windowWidth = fieldSize.x * (inventory ? InventorySpaceWindowWidth : HomeSpaceWindowWidth);
        Vector2 basePosition = inventory ? new Vector2(-52f, -28f) : new Vector2(-26f, -18f);
        return new Vector2(basePosition.x - (fieldSize.x - windowWidth) * 0.5f, basePosition.y);
    }

    static bool IsInsideSafeArea(Vector2 position, Vector2 center, float halfWidth, float halfHeight)
    {
        return Mathf.Abs(position.x - center.x) < halfWidth &&
               Mathf.Abs(position.y - center.y) < halfHeight;
    }

    void ConfigureDistantShipLayout(bool inventory)
    {
        if (distantShipRootRect == null)
            return;

        SetGameObjectActiveIfChanged(distantShipRootRect.gameObject, DistantShipTrafficEnabled);
        if (!DistantShipTrafficEnabled)
        {
            hasDistantShipRoute = false;
            return;
        }

        Vector2 rootSize = GetRectSize(rootRect, new Vector2(1920f, 1080f));
        float halfWidth = Mathf.Max(160f, rootSize.x * 0.5f);
        float halfHeight = Mathf.Max(260f, rootSize.y * 0.5f);
        int seedIndex = selectedSceneIndex >= 0 ? selectedSceneIndex : 0;

        bool topLane = Seed01(seedIndex, 211) >= 0.42f;
        float laneSign = topLane ? 1f : -1f;
        float laneY = laneSign * Mathf.Lerp(halfHeight * 0.42f, halfHeight * 0.70f, Seed01(seedIndex, 213));

        float objectSafeHalfHeight = Mathf.Max(420f * 0.5f + 78f, 180f);
        float objectSafeCenterY = Mathf.Lerp(-18f, -28f, 0.5f);
        if (Mathf.Abs(laneY - objectSafeCenterY) < objectSafeHalfHeight)
            laneY = objectSafeCenterY + laneSign * objectSafeHalfHeight;
        laneY = Mathf.Clamp(laneY, -halfHeight + 82f, halfHeight - 82f);

        float laneDrift = Mathf.Lerp(-halfHeight * 0.11f, halfHeight * 0.11f, Seed01(seedIndex, 217));
        float endY = Mathf.Clamp(laneY + laneDrift, -halfHeight + 82f, halfHeight - 82f);
        if (Mathf.Abs(endY - objectSafeCenterY) < objectSafeHalfHeight)
            endY = Mathf.Clamp(objectSafeCenterY + laneSign * objectSafeHalfHeight, -halfHeight + 82f, halfHeight - 82f);

        bool leftToRight = Seed01(seedIndex, 219) > 0.5f;
        float overscan = Mathf.Lerp(78f, 132f, Seed01(seedIndex, 221));
        float visibleLeftX = -halfWidth;
        float visibleRightX = -halfWidth + rootSize.x * InventorySpaceWindowWidth;
        float startX = leftToRight ? visibleLeftX - overscan : visibleRightX + overscan;
        float endX = leftToRight ? visibleRightX + overscan : visibleLeftX - overscan;
        distantShipStartPosition = new Vector2(startX, laneY);
        distantShipEndPosition = new Vector2(endX, endY);
        distantShipDuration = Mathf.Lerp(56f, 84f, Seed01(seedIndex, 223));
        distantShipPhase = Seed01(seedIndex, 227);
        distantShipTrailBaseAlpha = Mathf.Lerp(0.22f, 0.34f, Seed01(seedIndex, 229));

        if (distantShipTrailRect != null)
        {
            float trailLength = Mathf.Clamp(rootSize.x * Mathf.Lerp(0.20f, 0.34f, Seed01(seedIndex, 231)), 96f, 220f);
            float trailThickness = Mathf.Lerp(4f, 6.4f, Seed01(seedIndex, 233));
            distantShipTrailRect.sizeDelta = new Vector2(trailLength, trailThickness);
            distantShipTrailRect.localRotation = Quaternion.identity;
            distantShipTrailRect.localScale = Vector3.one;
        }

        if (distantShipCoreRect != null)
        {
            float coreSize = Mathf.Lerp(4.6f, 7.2f, Seed01(seedIndex, 235));
            distantShipCoreRect.sizeDelta = new Vector2(coreSize, coreSize);
            distantShipCoreRect.localRotation = Quaternion.identity;
            distantShipCoreRect.localScale = Vector3.one;
        }

        hasDistantShipRoute = true;
    }

    void UpdateSpaceWindow(float time)
    {
        if (spaceWindowRect == null)
            return;

        Vector2 parallaxOffset = SpaceWindowParallaxEnabled
            ? CalculateSpaceWindowParallaxOffset(time)
            : Vector2.zero;

        if (spaceBackgroundRect != null)
        {
            ApplySpaceBackgroundLayout();
            float backgroundScale = SpaceWindowParallaxEnabled ? SpaceWindowBackgroundScale : 1.045f;
            spaceBackgroundRect.anchoredPosition = spaceBackgroundBasePosition + parallaxOffset * SpaceWindowBackgroundParallaxStrength;
            spaceBackgroundRect.localScale = Vector3.one * (backgroundScale + Mathf.Sin(time * 0.12f) * 0.006f);
        }

        UpdateSpaceStarLayers(time, parallaxOffset);
        UpdateDistantShip(time, parallaxOffset);

        if (mapObjectRect != null)
        {
            mapObjectRect.anchoredPosition = mapObjectBasePosition + parallaxOffset;
            mapObjectRect.localScale = Vector3.one * (1f + Mathf.Sin(time * 0.16f + 0.8f) * 0.008f);
            float idleRotation = SpaceWindowParallaxEnabled
                ? Mathf.Sin(time * 0.11f + 1.7f) * SpaceWindowObjectIdleRotation
                : 0f;
            mapObjectRect.localRotation = Quaternion.Euler(
                0f,
                0f,
                selectedObjectRotation + idleRotation + parallaxOffset.x * SpaceWindowObjectRotationFromParallax);
        }
    }

    void UpdateSpaceStarLayers(float time, Vector2 parallaxOffset)
    {
        if (!SpaceWindowStarLayersEnabled)
            return;

        if (farSpaceStarsRect != null)
            farSpaceStarsRect.anchoredPosition = spaceBackgroundBasePosition + parallaxOffset * SpaceWindowFarStarParallaxStrength;

        if (nearSpaceStarsRect != null)
            nearSpaceStarsRect.anchoredPosition = spaceBackgroundBasePosition + parallaxOffset * SpaceWindowNearStarParallaxStrength;

        UpdateSpaceStarLayer(farSpaceStars, time, false);
        UpdateSpaceStarLayer(nearSpaceStars, time, true);
    }

    void UpdateSpaceStarLayer(List<SpaceStar> stars, float time, bool nearLayer)
    {
        for (int i = 0; i < stars.Count; i++)
        {
            SpaceStar star = stars[i];
            if (star == null || star.Rect == null || star.Image == null)
                continue;

            float twinkle = 0.72f + Mathf.PerlinNoise(star.Phase, time * (nearLayer ? 0.34f : 0.22f)) * 0.28f;
            float pulse = 1f + Mathf.Sin(time * (nearLayer ? 0.37f : 0.21f) + star.Phase) * (nearLayer ? 0.08f : 0.05f);
            star.Rect.sizeDelta = Vector2.one * star.Size * pulse;
            star.Image.color = nearLayer
                ? new Color(0.78f, 0.92f, 1f, star.Alpha * twinkle)
                : new Color(0.68f, 0.82f, 1f, star.Alpha * twinkle);
        }
    }

    void UpdateDistantShip(float time, Vector2 parallaxOffset)
    {
        if (distantShipRootRect == null)
            return;

        if (!DistantShipTrafficEnabled)
            return;

        if (!hasDistantShipRoute)
            ConfigureDistantShipLayout(currentMode == DisplayMode.Inventory);

        float progress = Mathf.Repeat(time / Mathf.Max(1f, distantShipDuration) + distantShipPhase, 1f);
        float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.08f));
        float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - progress) / 0.10f));
        float fade = fadeIn * fadeOut;
        Vector2 position = Vector2.Lerp(distantShipStartPosition, distantShipEndPosition, progress);
        distantShipRootRect.anchoredPosition = spaceBackgroundBasePosition + position + parallaxOffset * DistantShipParallaxStrength;
        distantShipRootRect.sizeDelta = Vector2.zero;
        distantShipRootRect.localScale = Vector3.one;

        Vector2 direction = distantShipEndPosition - distantShipStartPosition;
        if (direction.sqrMagnitude > 0.001f)
            distantShipRootRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        float shimmer = 0.84f + Mathf.PerlinNoise(19.7f, time * 0.85f) * 0.16f;
        if (distantShipTrailImage != null)
        {
            Color trailColor = new Color(0.36f, 0.82f, 1f, distantShipTrailBaseAlpha * fade * shimmer);
            distantShipTrailImage.color = trailColor;
        }

        if (distantShipCoreImage != null)
        {
            Color coreColor = new Color(0.92f, 0.98f, 1f, 0.95f * fade * shimmer);
            distantShipCoreImage.color = coreColor;
        }
    }

    void ApplySpaceBackgroundLayout()
    {
        if (spaceBackgroundRect == null)
            return;

        Vector2 rootSize = GetRectSize(rootRect, new Vector2(1920f, 1080f));
        Vector2 windowSize = GetRectSize(spaceWindowRect, new Vector2(rootSize.x * (currentMode == DisplayMode.Inventory ? InventorySpaceWindowWidth : HomeSpaceWindowWidth), rootSize.y));
        Sprite sprite = spaceBackgroundImage != null ? spaceBackgroundImage.sprite : null;
        if (hasCachedSpaceBackgroundLayout &&
            cachedSpaceBackgroundSprite == sprite &&
            cachedSpaceBackgroundMode == currentMode &&
            Approximately(cachedSpaceBackgroundRootSize, rootSize) &&
            Approximately(cachedSpaceBackgroundWindowSize, windowSize))
        {
            return;
        }

        float spriteAspect = sprite != null && sprite.rect.height > 1f
            ? sprite.rect.width / sprite.rect.height
            : rootSize.x / Mathf.Max(1f, rootSize.y);
        float rootAspect = rootSize.x / Mathf.Max(1f, rootSize.y);
        Vector2 backgroundSize = rootAspect > spriteAspect
            ? new Vector2(rootSize.x, rootSize.x / spriteAspect)
            : new Vector2(rootSize.y * spriteAspect, rootSize.y);

        spaceBackgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        spaceBackgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        spaceBackgroundRect.pivot = new Vector2(0.5f, 0.5f);
        spaceBackgroundRect.sizeDelta = backgroundSize;
        spaceBackgroundBasePosition = new Vector2((rootSize.x - windowSize.x) * 0.5f, 0f);
        spaceBackgroundRect.anchoredPosition = spaceBackgroundBasePosition;
        cachedSpaceBackgroundRootSize = rootSize;
        cachedSpaceBackgroundWindowSize = windowSize;
        cachedSpaceBackgroundSprite = sprite;
        cachedSpaceBackgroundMode = currentMode;
        hasCachedSpaceBackgroundLayout = true;
    }

    Vector2 CalculateSpaceWindowParallaxOffset(float time)
    {
        Vector2 maxOffset = GetSpaceWindowParallaxMaxOffset();
        Vector2 idleOffset = new Vector2(
            Mathf.Sin(time * 0.17f) * 0.70f + Mathf.Sin(time * 0.053f + 1.8f) * 0.30f,
            Mathf.Sin(time * 0.13f + 0.9f) * 0.62f + Mathf.Sin(time * 0.071f + 2.4f) * 0.24f);
        idleOffset = Vector2.Scale(idleOffset, maxOffset);

        Vector2 pointerOffset = Vector2.zero;
        if (TryGetPointerPosition(out Vector2 pointerPosition))
        {
            float screenWidth = Mathf.Max(1f, Screen.width);
            float screenHeight = Mathf.Max(1f, Screen.height);
            Vector2 normalizedPointer = new Vector2(
                Mathf.Clamp(pointerPosition.x / screenWidth * 2f - 1f, -1f, 1f),
                Mathf.Clamp(pointerPosition.y / screenHeight * 2f - 1f, -1f, 1f));
            pointerOffset = -Vector2.Scale(normalizedPointer, maxOffset * SpaceWindowPointerInfluence);
        }

        float pointerBlend = 1f - Mathf.Exp(-SpaceWindowPointerSmoothSpeed * Time.unscaledDeltaTime);
        smoothedSpaceWindowPointerParallax = Vector2.Lerp(
            smoothedSpaceWindowPointerParallax,
            pointerOffset,
            pointerBlend);

        return idleOffset + smoothedSpaceWindowPointerParallax;
    }

    Vector2 GetSpaceWindowParallaxMaxOffset()
    {
        return currentMode == DisplayMode.Inventory
            ? new Vector2(52f, 32f)
            : new Vector2(34f, 22f);
    }

    static bool TryGetPointerPosition(out Vector2 position)
    {
        position = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            position = mouse.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        position = Input.mousePosition;
        return true;
#else
        return false;
#endif
    }

    void UpdateSceneFloat(float time)
    {
        float floatCycle = Mathf.Sin(time * 0.30f) * 0.5f + 0.5f;
        float pulse = 1f + floatCycle * SceneFloatZoomAmplitude;
        Vector2 rootSize = GetRectSize(rootRect, new Vector2(1920f, 1080f));
        float horizontalScaleMargin = Mathf.Max(0f, rootSize.x * (pulse - 1f) * 0.5f - 2f);
        float rightDrift = Mathf.Min(Mathf.SmoothStep(0f, 1f, floatCycle) * SceneFloatRightDriftMax, horizontalScaleMargin);
        Vector2 sceneDrift = new Vector2(rightDrift, 0f);

        if (hangarRect != null)
        {
            hangarRect.localScale = Vector3.one * pulse;
            hangarRect.anchoredPosition = sceneDrift;
        }

        if (hasShipShadowBase && shipShadowRect != null)
        {
            shipShadowRect.localScale = shipShadowBaseScale * pulse;
            shipShadowRect.localRotation = Quaternion.identity;
            SetRectCenterInRoot(shipShadowRect, shipShadowBasePanelPosition * pulse + sceneDrift);
        }

        if (hasShipBase && shipImageRect != null)
        {
            shipImageRect.localScale = shipBaseScale * pulse;
            shipImageRect.localRotation = shipBaseRotation;
            SetRectCenterInRoot(shipImageRect, shipBasePanelPosition * pulse + sceneDrift);
        }

        if (hasLadderBase && ladderRect != null)
        {
            ladderRect.localScale = ladderBaseScale * pulse;
            ladderRect.localRotation = ladderBaseRotation;
            SetRectCenterInRoot(ladderRect, ladderBasePanelPosition * pulse + sceneDrift);
        }

        if (hasWelderBase && welderRect != null)
        {
            welderRect.localScale = welderBaseScale * pulse;
            welderRect.localRotation = welderBaseRotation;
            SetRectCenterInRoot(welderRect, welderBasePanelPosition * pulse + sceneDrift);
        }
    }

    void UpdateLandingLights(float time)
    {
        Vector2 rootSize = GetRectSize(rootRect, new Vector2(1920f, 1080f));
        for (int i = 0; i < landingLights.Count; i++)
        {
            LandingLight light = landingLights[i];
            if (light == null || light.Rect == null || light.Image == null)
                continue;

            light.Rect.anchoredPosition = new Vector2((light.NormalizedPosition.x - 0.5f) * rootSize.x, (light.NormalizedPosition.y - 0.5f) * rootSize.y);
            if (light.ColorKind == LandingLightColor.Red)
            {
                float blinkWave = Mathf.Sin(time * 4.4f + light.Phase) * 0.5f + 0.5f;
                float blink = Mathf.SmoothStep(0f, 1f, blinkWave);
                float warningPulse = Mathf.Lerp(0.20f, 1f, blink);
                light.Rect.sizeDelta = Vector2.one * light.BaseSize * Mathf.Lerp(0.88f, 1.22f, blink);
                Color color = light.BaseColor;
                color.a *= warningPulse;
                light.Image.color = color;
            }
            else
            {
                float shimmerWave = Mathf.Sin(time * 0.95f + light.Phase) * 0.5f + 0.5f;
                float shimmerNoise = Mathf.PerlinNoise(i * 7.13f, time * 0.72f + light.Phase);
                float shimmer = Mathf.Clamp01(shimmerWave * 0.55f + shimmerNoise * 0.45f);
                light.Rect.sizeDelta = Vector2.one * light.BaseSize * Mathf.Lerp(0.94f, 1.12f, shimmer);
                Color color = Color.Lerp(new Color(1f, 0.68f, 0.12f, light.BaseColor.a), new Color(1f, 0.94f, 0.42f, light.BaseColor.a), shimmer);
                color.a *= Mathf.Lerp(0.62f, 1f, shimmer);
                light.Image.color = color;
            }
        }
    }

    void UpdateScanDrone(float time)
    {
        if (!HangarScanDroneEnabled ||
            scanDroneRect == null ||
            scanDroneImage == null ||
            scanDroneSprite == null ||
            shipImageRect == null ||
            !hasShipBase)
        {
            SetScanDroneEffectsActive(false);
            return;
        }

        float visibleDuration = HangarScanDroneFlyInDuration + HangarScanDroneScanDuration + HangarScanDroneFlyOutDuration;
        EnsureScanDroneCycleStartTime();
        float cycleTime = Mathf.Repeat(time - scanDroneCycleStartTime, HangarScanDroneCycleDuration);
        if (cycleTime > visibleDuration)
        {
            SetScanDroneEffectsActive(false);
            return;
        }

        SetScanDroneEffectsActive(true);

        Vector2 rootSize = GetRectSize(rootRect, new Vector2(1920f, 1080f));
        Vector2 shipCenter = GetRectCenterInRoot(shipImageRect);
        Vector2 shipSize = GetRectSize(shipImageRect, shipImageRect.sizeDelta);
        shipSize = new Vector2(
            Mathf.Abs(shipSize.x * shipImageRect.localScale.x),
            Mathf.Abs(shipSize.y * shipImageRect.localScale.y));
        if (shipSize.x < 1f || shipSize.y < 1f)
            shipSize = new Vector2(280f, 420f);

        float halfX = Mathf.Clamp(shipSize.x * 0.28f, 56f, 178f);
        float halfY = Mathf.Clamp(shipSize.y * 0.30f, 64f, 214f);
        Vector2 hoverBase = shipCenter + new Vector2(-halfX * 0.48f, halfY * 1.05f + 38f);
        Vector2 hoverDrift = new Vector2(Mathf.Sin(time * 1.35f) * 9f, Mathf.Sin(time * 2.10f + 0.65f) * 5f);
        Vector2 start = shipCenter + new Vector2(-halfX * 2.65f - 180f, halfY * 1.50f + 96f);
        Vector2 entryBend = shipCenter + new Vector2(-halfX * 1.35f - 64f, halfY * 1.65f + 42f);
        float exitX = rootSize.x * 0.5f + Mathf.Max(scanDroneRect.sizeDelta.x, scanDroneRect.sizeDelta.y) + 140f;
        float exitY = Mathf.Clamp(shipCenter.y + halfY * 1.25f + 86f, -rootSize.y * 0.5f + 80f, rootSize.y * 0.5f - 80f);
        Vector2 exitBend = new Vector2(
            Mathf.Lerp(hoverBase.x, exitX, 0.42f),
            Mathf.Clamp(shipCenter.y + halfY * 1.58f + 58f, -rootSize.y * 0.5f + 80f, rootSize.y * 0.5f - 80f));
        Vector2 exit = new Vector2(exitX, exitY);

        Vector2 dronePosition;
        float hoverRotation = Mathf.Sin(time * 1.24f + 0.4f) * 2.8f;
        float droneRotation = hoverRotation;
        bool scanning = false;
        float scanProgress = 0f;
        if (cycleTime < HangarScanDroneFlyInDuration)
        {
            float t = Smooth01(cycleTime / HangarScanDroneFlyInDuration);
            dronePosition = QuadraticBezier(start, entryBend, hoverBase + hoverDrift, t);
        }
        else if (cycleTime < HangarScanDroneFlyInDuration + HangarScanDroneScanDuration)
        {
            scanProgress = Mathf.Clamp01((cycleTime - HangarScanDroneFlyInDuration) / HangarScanDroneScanDuration);
            dronePosition = hoverBase + hoverDrift;
            scanning = true;
        }
        else
        {
            float flyOutTime = cycleTime - HangarScanDroneFlyInDuration - HangarScanDroneScanDuration;
            float flyOutProgress = Mathf.Clamp01(flyOutTime / HangarScanDroneFlyOutDuration);
            float t = Smooth01(flyOutProgress);
            dronePosition = QuadraticBezier(hoverBase + hoverDrift, exitBend, exit, t);
            Vector2 escapeDirection = QuadraticBezierDerivative(hoverBase + hoverDrift, exitBend, exit, t);
            if (escapeDirection.sqrMagnitude > 0.001f)
            {
                float escapeHeading = Mathf.Atan2(escapeDirection.y, escapeDirection.x) * Mathf.Rad2Deg + 90f;
                float turnIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(flyOutProgress / 0.42f));
                droneRotation = Mathf.LerpAngle(hoverRotation, escapeHeading, turnIn);
            }
        }

        float edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(cycleTime / 0.75f));
        SetRectCenterInRoot(scanDroneRect, dronePosition);
        scanDroneRect.localRotation = Quaternion.Euler(0f, 0f, droneRotation);
        scanDroneRect.localScale = Vector3.one * (1f + Mathf.Sin(time * 2.25f) * 0.012f);
        scanDroneImage.color = new Color(1f, 1f, 1f, edgeFade);

        float scanAlpha = 0f;
        Vector2 scanFocus = shipCenter;
        if (scanning)
        {
            float scanEdge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(scanProgress / 0.16f)) *
                             Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - scanProgress) / 0.16f));
            float sweep = Mathf.PingPong(scanProgress * 2.35f, 1f);
            scanFocus = shipCenter + new Vector2(
                Mathf.Sin(scanProgress * Mathf.PI * 4f) * halfX * 0.24f,
                Mathf.Lerp(halfY * 0.54f, -halfY * 0.52f, sweep));
            scanAlpha = edgeFade * scanEdge;
        }

        UpdateScanDroneScanVisuals(dronePosition, scanFocus, shipSize, scanAlpha, time);
    }

    void UpdateScanDroneScanVisuals(Vector2 dronePosition, Vector2 scanFocus, Vector2 shipSize, float alpha, float time)
    {
        float shimmer = 0.78f + Mathf.PerlinNoise(41.7f, time * 2.35f) * 0.22f;
        float beamAlpha = alpha * shimmer;

        if (scanDroneBeamRect != null && scanDroneBeamImage != null)
        {
            SetConnectorBetweenRootPoints(scanDroneBeamRect, dronePosition, scanFocus, Mathf.Lerp(46f, 70f, shimmer));
            scanDroneBeamImage.color = new Color(0.16f, 0.78f, 1f, 0.15f * beamAlpha);
        }

        if (scanDroneBeamCoreRect != null && scanDroneBeamCoreImage != null)
        {
            SetConnectorBetweenRootPoints(scanDroneBeamCoreRect, dronePosition, scanFocus, 2.4f);
            scanDroneBeamCoreImage.color = new Color(0.72f, 0.98f, 1f, 0.34f * beamAlpha);
        }

        if (scanDroneSweepRect != null && scanDroneSweepImage != null)
        {
            SetRectCenterInRoot(scanDroneSweepRect, scanFocus);
            scanDroneSweepRect.sizeDelta = new Vector2(Mathf.Clamp(shipSize.x * 0.34f, 58f, 170f), 3.2f);
            scanDroneSweepRect.localRotation = Quaternion.identity;
            scanDroneSweepRect.localScale = Vector3.one;
            scanDroneSweepImage.color = new Color(0.52f, 0.96f, 1f, 0.48f * beamAlpha);
        }

        if (scanDroneGlowRect != null && scanDroneGlowImage != null)
        {
            SetRectCenterInRoot(scanDroneGlowRect, scanFocus);
            float glowSize = Mathf.Clamp(Mathf.Max(shipSize.x, shipSize.y) * 0.28f, 82f, 190f);
            scanDroneGlowRect.sizeDelta = Vector2.one * glowSize;
            scanDroneGlowRect.localRotation = Quaternion.identity;
            scanDroneGlowRect.localScale = Vector3.one;
            scanDroneGlowImage.color = new Color(0.16f, 0.86f, 1f, 0.18f * beamAlpha);
        }
    }

    void UpdateWelderAndSparks(float time)
    {
        if (welderRect == null)
            return;

        Transform sparkParent = welderRect.parent != null ? welderRect.parent : transform;
        for (int i = 0; i < sparks.Count; i++)
        {
            SparkItem spark = sparks[i];
            if (spark == null || spark.Rect == null || spark.Image == null)
                continue;

            if (spark.Rect.parent != sparkParent)
                spark.Rect.SetParent(sparkParent, false);

            float cycle = Mathf.Repeat(time * (1.2f + spark.Phase * 0.9f) + spark.Phase, 1f);
            float live = Mathf.Clamp01(cycle / spark.Lifetime);
            float burst = Mathf.SmoothStep(0f, 1f, 1f - live);
            Vector2 origin = GetTorchLocalPosition();
            Vector2 position = origin + spark.Velocity * live * 0.42f;
            position.y -= live * live * 54f;

            spark.Rect.anchorMin = new Vector2(0.5f, 0.5f);
            spark.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            spark.Rect.pivot = new Vector2(0.5f, 0.5f);
            spark.Rect.anchoredPosition = position;
            spark.Rect.sizeDelta = new Vector2(spark.Length * (0.4f + burst), Mathf.Lerp(2f, 5f, burst));
            spark.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(spark.Velocity.y, spark.Velocity.x) * Mathf.Rad2Deg);

            Color color = Color.Lerp(new Color(0.45f, 0.78f, 1f, 1f), new Color(1f, 0.54f, 0.1f, 1f), spark.Phase);
            color.a = burst * (0.55f + Mathf.PerlinNoise(spark.Phase * 17f, time * 12f) * 0.45f);
            spark.Image.color = color;
        }
    }

    Vector2 GetTorchLocalPosition()
    {
        if (welderRect == null)
            return Vector2.zero;

        return welderRect.anchoredPosition + GetWelderTorchOffset(welderRect.localScale);
    }

    Vector2 GetWelderTorchOffset(Vector3 scale)
    {
        if (welderRect == null)
            return Vector2.zero;

        Vector2 torchOffset = new Vector2(
            (welderTorchNormalized.x - 0.5f) * welderRect.sizeDelta.x,
            (welderTorchNormalized.y - 0.5f) * welderRect.sizeDelta.y);
        Vector3 scaledOffset = new Vector3(torchOffset.x * scale.x, torchOffset.y * scale.y, 0f);
        Vector3 rotatedOffset = welderRect.localRotation * scaledOffset;
        return new Vector2(rotatedOffset.x, rotatedOffset.y);
    }

    static SceneProfile[] GetSceneProfiles()
    {
        if (sceneProfiles != null && sceneProfiles.Length > 0)
            return sceneProfiles;

        List<SceneProfile> profiles = new List<SceneProfile>();
        IReadOnlyList<LobbyMapDefinition> maps = LobbyMapCatalog.AllMaps;
        if (maps != null)
        {
            for (int i = 0; i < maps.Count; i++)
            {
                LobbyMapDefinition map = maps[i];
                if (map == null || string.IsNullOrWhiteSpace(map.Id))
                    continue;

                string objectId = RoomSettings.NormalizeBackgroundObjectId(LobbyMapCatalog.GetDefaultBackgroundObjectId(map.Id));
                if (string.Equals(objectId, RoomSettings.BackgroundObjectOff, StringComparison.Ordinal))
                    continue;

                profiles.Add(new SceneProfile(
                    map.Id,
                    LobbyMapCatalog.GetDefaultParallaxBackgroundId(map.Id),
                    objectId));
            }
        }

        if (profiles.Count == 0)
        {
            profiles.Add(new SceneProfile(
                LobbyMapCatalog.NoobHavenMapId,
                RoomSettings.ParallaxBackgroundKosmos6,
                RoomSettings.BackgroundObject2));
        }

        sceneProfiles = profiles.ToArray();
        return sceneProfiles;
    }

    static Sprite LoadParallaxBackgroundSprite(string backgroundId)
    {
        return LoadResourceSprite("Visuals/Backgrounds/" + RoomSettings.NormalizeParallaxBackgroundId(backgroundId) + "_resource");
    }

    static Sprite LoadBackgroundObjectSprite(string objectId)
    {
        objectId = RoomSettings.NormalizeBackgroundObjectId(objectId);
        if (string.Equals(objectId, RoomSettings.BackgroundObjectOff, StringComparison.Ordinal))
            return null;

        return LoadResourceSprite("Visuals/Backgrounds/" + objectId + "_resource") ??
               LoadResourceSprite("Visuals/Backgrounds/" + objectId);
    }

    static Sprite LoadResourceSprite(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(path);
        if (sprites != null && sprites.Length > 0)
        {
            Sprite best = null;
            float bestArea = 0f;
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite candidate = sprites[i];
                if (candidate == null)
                    continue;

                float area = candidate.rect.width * candidate.rect.height;
                if (best == null || area > bestArea)
                {
                    best = candidate;
                    bestArea = area;
                }
            }

            if (best != null)
                return best;
        }

        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
            return null;

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(texture.width, texture.height));
    }

    void SetDetachedShipEffectsActive(bool active)
    {
        if (shipShadowRect != null)
            shipShadowRect.gameObject.SetActive(active);

        if (ladderRect != null)
            ladderRect.gameObject.SetActive(active && ladderSprite != null);

        if (welderRect != null)
            welderRect.gameObject.SetActive(active);

        if (!active || !HangarScanDroneEnabled || scanDroneSprite == null)
            SetScanDroneEffectsActive(false);

        for (int i = 0; i < sparks.Count; i++)
        {
            SparkItem spark = sparks[i];
            if (spark != null && spark.Rect != null)
                spark.Rect.gameObject.SetActive(active);
        }
    }

    void SetScanDroneParent(Transform parent)
    {
        if (parent == null)
            return;

        if (scanDroneGlowRect != null && scanDroneGlowRect.parent != parent)
            scanDroneGlowRect.SetParent(parent, false);
        if (scanDroneBeamRect != null && scanDroneBeamRect.parent != parent)
            scanDroneBeamRect.SetParent(parent, false);
        if (scanDroneBeamCoreRect != null && scanDroneBeamCoreRect.parent != parent)
            scanDroneBeamCoreRect.SetParent(parent, false);
        if (scanDroneSweepRect != null && scanDroneSweepRect.parent != parent)
            scanDroneSweepRect.SetParent(parent, false);
        if (scanDroneRect != null && scanDroneRect.parent != parent)
            scanDroneRect.SetParent(parent, false);

        PlaceScanDroneAboveShipImage();
    }

    void SetScanDroneEffectsActive(bool active)
    {
        if (scanDroneEffectsActive == active)
            return;

        scanDroneEffectsActive = active;
        if (scanDroneGlowRect != null)
            SetGameObjectActiveIfChanged(scanDroneGlowRect.gameObject, active);
        if (scanDroneBeamRect != null)
            SetGameObjectActiveIfChanged(scanDroneBeamRect.gameObject, active);
        if (scanDroneBeamCoreRect != null)
            SetGameObjectActiveIfChanged(scanDroneBeamCoreRect.gameObject, active);
        if (scanDroneSweepRect != null)
            SetGameObjectActiveIfChanged(scanDroneSweepRect.gameObject, active);
        if (scanDroneRect != null)
            SetGameObjectActiveIfChanged(scanDroneRect.gameObject, active);

        if (!active)
        {
            SetImageAlpha(scanDroneGlowImage, 0f);
            SetImageAlpha(scanDroneBeamImage, 0f);
            SetImageAlpha(scanDroneBeamCoreImage, 0f);
            SetImageAlpha(scanDroneSweepImage, 0f);
            SetImageAlpha(scanDroneImage, 0f);
        }
    }

    void SetSparkParent(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = 0; i < sparks.Count; i++)
        {
            SparkItem spark = sparks[i];
            if (spark != null && spark.Rect != null && spark.Rect.parent != parent)
                spark.Rect.SetParent(parent, false);
        }
    }

    void DestroyDetachedShipEffects()
    {
        if (shipShadowRect != null)
            Destroy(shipShadowRect.gameObject);

        if (ladderRect != null)
            Destroy(ladderRect.gameObject);

        if (welderRect != null)
            Destroy(welderRect.gameObject);

        if (scanDroneGlowRect != null)
            Destroy(scanDroneGlowRect.gameObject);
        if (scanDroneBeamRect != null)
            Destroy(scanDroneBeamRect.gameObject);
        if (scanDroneBeamCoreRect != null)
            Destroy(scanDroneBeamCoreRect.gameObject);
        if (scanDroneSweepRect != null)
            Destroy(scanDroneSweepRect.gameObject);
        if (scanDroneRect != null)
            Destroy(scanDroneRect.gameObject);

        for (int i = 0; i < sparks.Count; i++)
        {
            SparkItem spark = sparks[i];
            if (spark != null && spark.Rect != null)
                Destroy(spark.Rect.gameObject);
        }
    }

    static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    static void SetGameObjectActiveIfChanged(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    static RectTransform EnsureRectTransform(GameObject target)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null)
            rect = target.AddComponent<RectTransform>();
        return rect;
    }

    static void StretchToParent(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static void SetRectAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    static void ConfigureCenteredOverlayRect(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }

    static Vector2 GetRectSize(RectTransform rect, Vector2 fallback)
    {
        if (rect == null)
            return fallback;

        Rect bounds = rect.rect;
        if (bounds.width > 1f && bounds.height > 1f)
            return bounds.size;

        return fallback;
    }

    static bool Approximately(Vector2 a, Vector2 b)
    {
        return (a - b).sqrMagnitude <= 0.01f;
    }

    Vector2 GetRectCenterInRoot(RectTransform rect)
    {
        if (rect == null)
            return Vector2.zero;
        if (rootRect == null)
            return rect.anchoredPosition;

        Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
        Vector3 rootLocal = rootRect.InverseTransformPoint(worldCenter);
        return new Vector2(rootLocal.x, rootLocal.y);
    }

    Rect GetRectBoundsInRoot(RectTransform rect)
    {
        if (rect == null)
            return new Rect(0f, 0f, 0f, 0f);

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = rootRect != null ? rootRect.InverseTransformPoint(corners[i]) : corners[i];
            min = Vector2.Min(min, new Vector2(local.x, local.y));
            max = Vector2.Max(max, new Vector2(local.x, local.y));
        }

        if (float.IsNaN(min.x) || float.IsNaN(min.y) || float.IsNaN(max.x) || float.IsNaN(max.y) ||
            float.IsInfinity(min.x) || float.IsInfinity(min.y) || float.IsInfinity(max.x) || float.IsInfinity(max.y))
            return new Rect(0f, 0f, 0f, 0f);

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static Rect ExpandRect(Rect rect, float padding)
    {
        padding = Mathf.Max(0f, padding);
        return new Rect(rect.xMin - padding, rect.yMin - padding, rect.width + padding * 2f, rect.height + padding * 2f);
    }

    static float CalculateOverlapArea(Rect a, Rect b)
    {
        float width = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float height = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return width > 0f && height > 0f ? width * height : 0f;
    }

    void SetRectCenterInRoot(RectTransform rect, Vector2 rootLocalCenter)
    {
        if (rect == null)
            return;
        if (rootRect == null)
        {
            rect.anchoredPosition = rootLocalCenter;
            return;
        }

        Vector3 targetWorldCenter = rootRect.TransformPoint(new Vector3(rootLocalCenter.x, rootLocalCenter.y, 0f));
        Vector3 currentWorldCenter = rect.TransformPoint(rect.rect.center);
        rect.position += targetWorldCenter - currentWorldCenter;
    }

    void SetConnectorBetweenRootPoints(RectTransform rect, Vector2 start, Vector2 end, float thickness)
    {
        if (rect == null)
            return;

        Vector2 delta = end - start;
        float length = Mathf.Max(1f, delta.magnitude);
        rect.sizeDelta = new Vector2(length, Mathf.Max(1f, thickness));
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        rect.localScale = Vector3.one;
        SetRectCenterInRoot(rect, (start + end) * 0.5f);
    }

    static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        t = Mathf.Clamp01(t);
        Vector2 a = Vector2.Lerp(start, control, t);
        Vector2 b = Vector2.Lerp(control, end, t);
        return Vector2.Lerp(a, b, t);
    }

    static Vector2 QuadraticBezierDerivative(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        t = Mathf.Clamp01(t);
        return 2f * ((1f - t) * (control - start) + t * (end - control));
    }

    static void EnsureSprites()
    {
        if (pixelSprite == null)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            pixelSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        if (softDotSprite == null)
        {
            const int textureSize = 32;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
            float radius = textureSize * 0.5f;
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply(false, true);
            softDotSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
            softDotSprite.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    static float Seed01(int index, int channel)
    {
        unchecked
        {
            uint value = (uint)(index + 1);
            value ^= (uint)(channel + 17) * 0x9E3779B9u;
            value *= 0x85EBCA6Bu;
            value ^= value >> 13;
            value *= 0xC2B2AE35u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
