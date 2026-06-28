using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerMovement))]
public class BoosterBarUI : MonoBehaviourPun
{
    const string LegacyBoosterBarName = "Booster_Bar";
    const string FuelRingRootName = "MovementJoystickBoosterFuelRing";
    const string FuelTrackName = "MovementJoystickBoosterFuelTrack";
    const string FuelFillName = "MovementJoystickBoosterFuelFill";
    const string HandleVisualName = "MovementJoystickHandleVisual";
    const string MovementJoystickName = "JoystickBG";
    const float FuelRingThickness = 0.78f;
    const float TrackRingThickness = 0.72f;
    const float HandleVisualDiameter = 116f;
    const float DesktopFuelRingDiameter = 112f;
    static readonly Vector2 DesktopFuelRingAnchor = new Vector2(0f, 0f);
    static readonly Vector2 DesktopFuelRingPosition = new Vector2(82f, 96f);

    static readonly Color HighBoosterColor = new Color(1f, 0.9f, 0.18f, 0.98f);
    static readonly Color MidBoosterColor = new Color(1f, 0.68f, 0.18f, 0.98f);
    static readonly Color LowBoosterColor = new Color(0.95f, 0.26f, 0.18f, 0.98f);
    static readonly Color EmptyBoosterColor = new Color(0.9f, 0.18f, 0.18f, 0.98f);
    static readonly Color TrackColor = new Color(0.17f, 0.11f, 0.035f, 0.44f);
    static readonly Color HandleVisualColor = new Color32(82, 176, 112, 255);
    static Sprite cachedHandleVisualSprite;

    PlayerMovement movement;
    GameObject ringRoot;
    RectTransform ringRect;
    RectTransform joystickRect;
    RectTransform handleRect;
    RectTransform handleVisualRect;
    Image handleVisualImage;
    Transform canvasTransform;
    JoystickBoosterFuelRingGraphic trackGraphic;
    JoystickBoosterFuelRingGraphic fillGraphic;
    bool isVisible = true;

    void Start()
    {
        movement = GetComponent<PlayerMovement>();

        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        CreateBoosterRing();
        RefreshRing();
    }

    void Update()
    {
        if (fillGraphic == null || trackGraphic == null)
        {
            CreateBoosterRing();
            RefreshRing();
        }

        UpdateVisibility();
        RefreshRing();
    }

    void OnDestroy()
    {
        if (ringRoot != null)
            Destroy(ringRoot);
    }

    void CreateBoosterRing()
    {
        DestroyLegacyBoosterBar();

        GameObject joystickObject = GameObject.Find(MovementJoystickName);
        if (joystickObject == null)
            return;

        joystickRect = joystickObject.GetComponent<RectTransform>();
        if (joystickRect == null)
            return;

        handleRect = ResolveHandleRect(joystickObject);
        EnsureHandleVisual(joystickObject.transform);

        Transform existingRoot = ringRoot != null ? ringRoot.transform : joystickObject.transform.Find(FuelRingRootName);
        if (existingRoot == null)
        {
            Transform canvas = ResolveCanvasTransform();
            existingRoot = canvas != null ? canvas.Find(FuelRingRootName) : null;
        }

        ringRoot = existingRoot != null
            ? existingRoot.gameObject
            : new GameObject(FuelRingRootName, typeof(RectTransform));

        if (ringRoot.transform.parent != joystickObject.transform)
            ringRoot.transform.SetParent(joystickObject.transform, false);

        ringRect = ringRoot.GetComponent<RectTransform>();
        if (ringRect == null)
            return;

        trackGraphic = GetOrCreateRingGraphic(ringRoot.transform, FuelTrackName);
        fillGraphic = GetOrCreateRingGraphic(ringRoot.transform, FuelFillName);
        if (trackGraphic != null)
            trackGraphic.transform.SetAsFirstSibling();
        if (fillGraphic != null)
            fillGraphic.transform.SetAsLastSibling();

        RestoreRingRootCanvasGroup();
        SyncRingTransform();
        isVisible = IsGameplayHudVisible();
        ringRoot.SetActive(isVisible);
    }

    void RefreshRing()
    {
        if (movement == null || fillGraphic == null || trackGraphic == null)
            return;

        SyncRingTransform();

        float normalized = Mathf.Clamp01(movement.BoosterNormalized);
        trackGraphic.SetFill(1f, TrackColor, true, TrackRingThickness, false, false);
        fillGraphic.SetFill(normalized, GetBoosterColor(normalized), true, FuelRingThickness, true, true);
    }

    void UpdateVisibility()
    {
        if (ringRoot == null)
            return;

        bool shouldBeVisible = IsGameplayHudVisible();
        if (isVisible == shouldBeVisible)
            return;

        isVisible = shouldBeVisible;
        ringRoot.SetActive(shouldBeVisible);
        if (handleVisualRect != null)
            SetHandleVisualVisible(shouldBeVisible && !StarjackersInputModeManager.DesktopHudLayoutActive);
    }

    bool IsGameplayHudVisible()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started)
        {
            return GameplayHudVisibility.IsGameplayHudVisible(started);
        }

        return false;
    }

    void SyncRingTransform()
    {
        if (ringRect == null)
            return;

        if (joystickRect == null)
        {
            GameObject joystickObject = GameObject.Find(MovementJoystickName);
            joystickRect = joystickObject != null ? joystickObject.GetComponent<RectTransform>() : null;
        }

        if (joystickRect == null)
            return;

        RestoreRingRootCanvasGroup();

        if (StarjackersInputModeManager.DesktopHudLayoutActive && TryApplyDesktopRingTransform())
        {
            SetHandleVisualVisible(false);
            return;
        }

        if (ringRect.parent != joystickRect)
            ringRect.SetParent(joystickRect, false);

        EnsureHandleVisual(joystickRect);

        ringRect.anchorMin = new Vector2(0.5f, 0.5f);
        ringRect.anchorMax = new Vector2(0.5f, 0.5f);
        ringRect.pivot = new Vector2(0.5f, 0.5f);
        ringRect.anchoredPosition = Vector2.zero;
        ringRect.localRotation = Quaternion.identity;
        ringRect.localScale = Vector3.one;
        ringRect.sizeDelta = Vector2.one * GetFuelRingDiameter();

        SyncHandleVisual();
        PlaceFuelRingAboveHandle();
    }

    void PlaceFuelRingAboveHandle()
    {
        if (ringRoot == null || joystickRect == null)
            return;

        ringRoot.transform.SetAsLastSibling();
    }

    void EnsureHandleVisual(Transform joystickRoot)
    {
        if (joystickRoot == null)
            return;

        Transform existing = joystickRoot.Find(HandleVisualName);
        GameObject visualObject = existing != null
            ? existing.gameObject
            : new GameObject(HandleVisualName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        if (visualObject.transform.parent != joystickRoot)
            visualObject.transform.SetParent(joystickRoot, false);

        handleVisualRect = visualObject.GetComponent<RectTransform>();
        handleVisualImage = visualObject.GetComponent<Image>();
        if (handleVisualImage == null)
            handleVisualImage = visualObject.AddComponent<Image>();

        ApplyHandleVisualStyle();
    }

    void SyncHandleVisual()
    {
        if (joystickRect == null)
            return;

        if (handleVisualRect == null || handleVisualImage == null)
            EnsureHandleVisual(joystickRect);

        if (handleVisualRect == null || handleVisualImage == null)
            return;

        if (handleRect == null)
            handleRect = ResolveHandleRect(joystickRect.gameObject);

        handleVisualRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleVisualRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleVisualRect.pivot = new Vector2(0.5f, 0.5f);
        handleVisualRect.anchoredPosition = handleRect != null ? handleRect.anchoredPosition : Vector2.zero;
        handleVisualRect.sizeDelta = Vector2.one * HandleVisualDiameter;
        handleVisualRect.localRotation = Quaternion.identity;
        handleVisualRect.localScale = Vector3.one;

        handleVisualImage.enabled = true;
        handleVisualImage.color = HandleVisualColor;
        handleVisualImage.canvasRenderer.SetAlpha(1f);
        SetHandleVisualVisible(isVisible && !StarjackersInputModeManager.DesktopHudLayoutActive);
        ApplyHandleVisualStyle();
    }

    bool TryApplyDesktopRingTransform()
    {
        Transform canvas = ResolveCanvasTransform();
        if (canvas == null)
            return false;

        if (ringRect.parent != canvas)
            ringRect.SetParent(canvas, false);

        ringRect.anchorMin = DesktopFuelRingAnchor;
        ringRect.anchorMax = DesktopFuelRingAnchor;
        ringRect.pivot = new Vector2(0.5f, 0.5f);
        ringRect.anchoredPosition = DesktopFuelRingPosition;
        ringRect.localRotation = Quaternion.identity;
        ringRect.localScale = Vector3.one;
        ringRect.sizeDelta = Vector2.one * DesktopFuelRingDiameter;

        if (ringRoot != null)
            ringRoot.transform.SetAsLastSibling();

        return true;
    }

    Transform ResolveCanvasTransform()
    {
        if (canvasTransform != null)
            return canvasTransform;

        GameObject canvas = GameObject.Find("Canvas");
        canvasTransform = canvas != null ? canvas.transform : null;
        return canvasTransform;
    }

    void RestoreRingRootCanvasGroup()
    {
        if (ringRoot == null)
            return;

        CanvasGroup group = ringRoot.GetComponent<CanvasGroup>();
        if (group == null)
            return;

        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    void SetHandleVisualVisible(bool visible)
    {
        if (handleVisualRect != null)
            handleVisualRect.gameObject.SetActive(visible);
    }

    void ApplyHandleVisualStyle()
    {
        if (handleVisualImage == null)
            return;

        handleVisualImage.sprite = GetHandleVisualSprite();
        handleVisualImage.type = Image.Type.Simple;
        handleVisualImage.preserveAspect = false;
        handleVisualImage.raycastTarget = false;
        handleVisualImage.maskable = false;
        handleVisualImage.material = null;
        handleVisualImage.color = HandleVisualColor;
        handleVisualImage.canvasRenderer.SetAlpha(1f);
        handleVisualImage.canvasRenderer.SetColor(HandleVisualColor);

        Outline outline = handleVisualImage.GetComponent<Outline>();
        if (outline == null)
            outline = handleVisualImage.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.72f);
        outline.effectDistance = new Vector2(3f, 3f);
        outline.useGraphicAlpha = false;

    }

    float GetFuelRingDiameter()
    {
        if (handleRect == null)
        {
            GameObject joystickObject = joystickRect != null ? joystickRect.gameObject : GameObject.Find(MovementJoystickName);
            handleRect = joystickObject != null ? ResolveHandleRect(joystickObject) : null;
        }

        float handleDiameter = 128f;
        if (handleRect != null)
        {
            Vector2 handleSize = handleRect.sizeDelta;
            if (handleSize.x <= 0.1f || handleSize.y <= 0.1f)
                handleSize = handleRect.rect.size;

            handleDiameter = Mathf.Max(handleDiameter, Mathf.Max(handleSize.x, handleSize.y));
        }

        float innerRadiusRatio = JoystickBoosterFuelRingGraphic.GetInnerRadiusRatio(FuelRingThickness);
        return handleDiameter / Mathf.Max(0.1f, innerRadiusRatio);
    }

    RectTransform ResolveHandleRect(GameObject joystickObject)
    {
        if (joystickObject == null)
            return null;

        Joystick joystick = joystickObject.GetComponent<Joystick>();
        if (joystick != null && joystick.handle != null)
            return joystick.handle;

        Transform handle = joystickObject.transform.Find("JoystickHandle");
        if (handle == null)
            handle = joystickObject.transform.Find("Handle");

        return handle != null ? handle.GetComponent<RectTransform>() : null;
    }

    JoystickBoosterFuelRingGraphic GetOrCreateRingGraphic(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        GameObject ringObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(JoystickBoosterFuelRingGraphic));

        if (ringObject.transform.parent != parent)
            ringObject.transform.SetParent(parent, false);

        RectTransform rect = ringObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        JoystickBoosterFuelRingGraphic graphic = ringObject.GetComponent<JoystickBoosterFuelRingGraphic>();
        if (graphic == null)
            graphic = ringObject.AddComponent<JoystickBoosterFuelRingGraphic>();

        graphic.raycastTarget = false;
        graphic.maskable = false;
        return graphic;
    }

    void DestroyLegacyBoosterBar()
    {
        GameObject existingBar = GameObject.Find(LegacyBoosterBarName);
        if (existingBar != null)
            Destroy(existingBar);
    }

    static Sprite GetHandleVisualSprite()
    {
        if (cachedHandleVisualSprite != null && cachedHandleVisualSprite.name == "RuntimeMovementJoystickHandleSpriteShaded")
            return cachedHandleVisualSprite;

        const int size = 512;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "RuntimeMovementJoystickHandleTextureShaded";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float normalizedDistance = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                if (normalizedDistance > 1f)
                {
                    pixels[(y * size) + x] = Color.clear;
                    continue;
                }

                float nx = dx / radius;
                float ny = dy / radius;
                float edgeShade = Mathf.SmoothStep(0.56f, 1f, normalizedDistance);
                float topLeftHighlight = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx, ny), new Vector2(-0.32f, 0.34f)) / 0.64f);
                float lowerShadow = Mathf.Clamp01((ny + 0.2f) * -0.72f + normalizedDistance * 0.22f);
                float shade = 0.92f + topLeftHighlight * 0.22f - edgeShade * 0.28f - lowerShadow * 0.14f;
                shade = Mathf.Clamp(shade, 0.58f, 1.08f);
                pixels[(y * size) + x] = new Color(shade, shade, shade, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        cachedHandleVisualSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        cachedHandleVisualSprite.name = "RuntimeMovementJoystickHandleSpriteShaded";
        cachedHandleVisualSprite.hideFlags = HideFlags.HideAndDontSave;
        return cachedHandleVisualSprite;
    }

    static Color GetBoosterColor(float normalized)
    {
        if (normalized > 0.5f)
        {
            float t = Mathf.InverseLerp(0.5f, 1f, normalized);
            return Color.Lerp(MidBoosterColor, HighBoosterColor, t);
        }

        if (normalized > 0.2f)
        {
            float t = Mathf.InverseLerp(0.2f, 0.5f, normalized);
            return Color.Lerp(LowBoosterColor, MidBoosterColor, t);
        }

        return EmptyBoosterColor;
    }
}
