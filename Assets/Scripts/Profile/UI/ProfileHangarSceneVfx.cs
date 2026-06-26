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
    const int LandingLightCount = 10;
    const float SceneFloatZoomAmplitude = 0.065f;
    const float SceneFloatRightDriftMax = 36f;

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
        public float Phase;
    }

    readonly List<SparkItem> sparks = new List<SparkItem>();
    readonly List<LandingLight> landingLights = new List<LandingLight>();

    RectTransform rootRect;
    RectTransform spaceWindowRect;
    RectTransform spaceBackgroundRect;
    RectTransform mapObjectRect;
    RectTransform hangarRect;
    RectTransform shipShadowRect;
    RectTransform welderRect;
    RectTransform shipImageRect;
    Image shipImageComponent;
    Image spaceBackgroundImage;
    Image mapObjectImage;
    Image hangarImage;
    Image shipShadowImage;
    Image welderImage;

    Sprite homeHangarSprite;
    Sprite inventoryHangarSprite;
    Sprite welderSprite;

    Vector2 shipBasePosition;
    Vector2 shipBasePanelPosition;
    Vector3 shipBaseScale = Vector3.one;
    Quaternion shipBaseRotation = Quaternion.identity;
    Vector2 shipShadowBasePosition;
    Vector2 shipShadowBasePanelPosition;
    Vector3 shipShadowBaseScale = Vector3.one;
    bool hasShipShadowBase;
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
    Vector2 mapObjectBasePosition;
    bool hasShipBase;
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
        if (shipShadowRect != null && shipPreviewRoot != null && shipShadowRect.parent != shipPreviewRoot)
            shipShadowRect.SetParent(shipPreviewRoot, false);
        if (shipPreviewRoot != null)
            SetSparkParent(shipPreviewRoot);
        SetDetachedShipEffectsActive(true);

        if (!hasMode || currentMode != mode)
        {
            currentMode = mode;
            hasMode = true;
        }

        EnsureSceneProfileSelected();
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

        if (hasShipShadowBase && shipShadowRect != null)
        {
            shipShadowRect.anchoredPosition = shipShadowBasePosition;
            shipShadowRect.localScale = shipShadowBaseScale;
            shipShadowRect.localRotation = Quaternion.identity;
        }

        hasShipBase = false;
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
        RefreshWelderAttachmentIfShipChanged();
        UpdateSceneFloat(time);
        UpdateLandingLights(time);
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

        welderImage = CreateImage(transform, "WelderRig", welderSprite, Color.white);
        welderRect = welderImage.rectTransform;
        welderImage.preserveAspect = true;

        BuildSparks(transform);
        built = true;
    }

    void LoadSprites()
    {
        homeHangarSprite = LoadResourceSprite("UI/ProfileHangarVfx/hangar_duzy_runtime");
        inventoryHangarSprite = LoadResourceSprite("UI/ProfileHangarVfx/hangar_maly_runtime");
        welderSprite = LoadResourceSprite("UI/ProfileHangarVfx/spawacz_runtime");
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

    void ApplyModeLayout()
    {
        bool inventory = currentMode == DisplayMode.Inventory;
        float windowWidth = inventory ? 0.47f : 0.285f;
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

        if (hangarImage != null)
            hangarImage.sprite = inventory ? inventoryHangarSprite : homeHangarSprite;

        ApplyShipShadowLayout();
        ApplyWelderLayout(inventory);
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

    void RefreshWelderAttachmentIfShipChanged()
    {
        if (welderRect == null || !hasWelderBase)
            return;

        Sprite currentSprite = shipImageComponent != null ? shipImageComponent.sprite : null;
        if (hasWelderContact && welderContactShipSprite == currentSprite)
            return;

        ApplyShipShadowLayout();
        RepositionWelderForCurrentShip();
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
            welderRect.SetSiblingIndex(Mathf.Min(shipIndex + 1, welderRect.parent.childCount - 1));
            return;
        }

        welderRect.SetAsLastSibling();
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

    void BuildLandingLights(Transform parent)
    {
        for (int i = 0; i < LandingLightCount; i++)
        {
            Image image = CreateImage(parent, "LandingPadLight_" + i, softDotSprite, new Color(1f, 0.82f, 0.45f, 0.62f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            landingLights.Add(new LandingLight
            {
                Rect = rect,
                Image = image,
                BaseColor = i % 3 == 0 ? new Color(1f, 0.56f, 0.36f, 0.72f) : new Color(0.86f, 0.96f, 1f, 0.68f),
                Phase = Seed01(i, 83) * Mathf.PI * 2f
            });
        }
    }

    void ApplyLandingLightPositions(bool inventory)
    {
        Vector2[] positions = inventory
            ? new[]
            {
                new Vector2(0.59f, 0.89f), new Vector2(0.74f, 0.905f), new Vector2(0.90f, 0.89f),
                new Vector2(0.59f, 0.11f), new Vector2(0.74f, 0.095f), new Vector2(0.90f, 0.11f),
                new Vector2(0.565f, 0.5f), new Vector2(0.925f, 0.5f),
                new Vector2(0.62f, 0.66f), new Vector2(0.88f, 0.34f)
            }
            : new[]
            {
                new Vector2(0.36f, 0.89f), new Vector2(0.50f, 0.905f), new Vector2(0.64f, 0.89f),
                new Vector2(0.36f, 0.11f), new Vector2(0.50f, 0.095f), new Vector2(0.64f, 0.11f),
                new Vector2(0.335f, 0.5f), new Vector2(0.665f, 0.5f),
                new Vector2(0.39f, 0.66f), new Vector2(0.61f, 0.34f)
            };

        for (int i = 0; i < landingLights.Count; i++)
        {
            LandingLight light = landingLights[i];
            if (light == null)
                continue;
            light.NormalizedPosition = positions[i % positions.Length];
        }
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

    void UpdateSpaceWindow(float time)
    {
        if (spaceWindowRect == null)
            return;

        if (spaceBackgroundRect != null)
        {
            ApplySpaceBackgroundLayout();
            spaceBackgroundRect.localScale = Vector3.one * (1.045f + Mathf.Sin(time * 0.12f) * 0.006f);
        }

        if (mapObjectRect != null)
        {
            mapObjectRect.anchoredPosition = mapObjectBasePosition;
            mapObjectRect.localScale = Vector3.one * (1f + Mathf.Sin(time * 0.16f + 0.8f) * 0.008f);
        }
    }

    void ApplySpaceBackgroundLayout()
    {
        if (spaceBackgroundRect == null)
            return;

        Vector2 rootSize = GetRectSize(rootRect, new Vector2(1920f, 1080f));
        Vector2 windowSize = GetRectSize(spaceWindowRect, new Vector2(rootSize.x * (currentMode == DisplayMode.Inventory ? 0.47f : 0.285f), rootSize.y));
        Sprite sprite = spaceBackgroundImage != null ? spaceBackgroundImage.sprite : null;
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
        spaceBackgroundRect.anchoredPosition = new Vector2((rootSize.x - windowSize.x) * 0.5f, 0f);
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
            float flicker = 0.52f + Mathf.PerlinNoise(i * 7.13f, time * 1.6f + light.Phase) * 0.48f;
            light.Rect.sizeDelta = Vector2.one * Mathf.Lerp(22f, 42f, flicker);
            Color color = light.BaseColor;
            color.a *= Mathf.Lerp(0.36f, 1f, flicker);
            light.Image.color = color;
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

        if (welderRect != null)
            welderRect.gameObject.SetActive(active);

        for (int i = 0; i < sparks.Count; i++)
        {
            SparkItem spark = sparks[i];
            if (spark != null && spark.Rect != null)
                spark.Rect.gameObject.SetActive(active);
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

        if (welderRect != null)
            Destroy(welderRect.gameObject);

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

    static Vector2 GetRectSize(RectTransform rect, Vector2 fallback)
    {
        if (rect == null)
            return fallback;

        Rect bounds = rect.rect;
        if (bounds.width > 1f && bounds.height > 1f)
            return bounds.size;

        return fallback;
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
