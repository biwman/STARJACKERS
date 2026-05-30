using UnityEngine;

public class ExtractionCarrierVisual : MonoBehaviour
{
    enum CarrierVisualState
    {
        Inactive,
        Transitioning,
        Active
    }

    const int LightCount = 40;
    const float LightSizeFactor = 0.009f;
    const float GlowSizeMultiplier = 2.6f;
    const float HangarTargetNormalizedX = 0.76f;
    const float HangarTargetNormalizedY = 0.36f;

    static readonly Vector2[] LightAnchors =
    {
        // Hangar frame: around the black inlet, never inside it.
        new Vector2(0.60f, 0.51f),
        new Vector2(0.65f, 0.54f),
        new Vector2(0.71f, 0.55f),
        new Vector2(0.77f, 0.54f),
        new Vector2(0.83f, 0.52f),
        new Vector2(0.88f, 0.48f),
        new Vector2(0.91f, 0.43f),
        new Vector2(0.92f, 0.37f),
        new Vector2(0.91f, 0.31f),
        new Vector2(0.89f, 0.25f),
        new Vector2(0.84f, 0.21f),
        new Vector2(0.78f, 0.19f),
        new Vector2(0.72f, 0.18f),
        new Vector2(0.66f, 0.19f),
        new Vector2(0.61f, 0.23f),
        new Vector2(0.58f, 0.29f),
        new Vector2(0.57f, 0.36f),
        new Vector2(0.58f, 0.43f),
        new Vector2(0.60f, 0.49f),

        // Left hull side.
        new Vector2(0.08f, 0.47f),
        new Vector2(0.14f, 0.44f),
        new Vector2(0.20f, 0.41f),
        new Vector2(0.26f, 0.38f),
        new Vector2(0.32f, 0.35f),
        new Vector2(0.38f, 0.32f),
        new Vector2(0.44f, 0.29f),
        new Vector2(0.50f, 0.26f),
        new Vector2(0.56f, 0.23f),
        new Vector2(0.12f, 0.55f),
        new Vector2(0.21f, 0.53f),
        new Vector2(0.30f, 0.51f),
        new Vector2(0.39f, 0.49f),
        new Vector2(0.48f, 0.46f),

        // Upper-right bridge and command tower details.
        new Vector2(0.49f, 0.66f),
        new Vector2(0.54f, 0.72f),
        new Vector2(0.59f, 0.76f),
        new Vector2(0.64f, 0.74f),
        new Vector2(0.68f, 0.68f),
        new Vector2(0.63f, 0.62f),
        new Vector2(0.70f, 0.61f)
    };

    static Sprite sharedLightSprite;

    readonly SpriteRenderer[] lightRenderers = new SpriteRenderer[LightCount];
    readonly SpriteRenderer[] glowRenderers = new SpriteRenderer[LightCount];

    SpriteRenderer carrierRenderer;
    Sprite lastCarrierSprite;
    Vector3 lastTransformScale;
    int lastSortingLayerId;
    int lastSortingOrder;
    CarrierVisualState state = CarrierVisualState.Inactive;
    float stateStartedAt;
    bool visible = true;
    Bounds carrierBounds = new Bounds(Vector3.zero, Vector3.one * 4f);

    public void Initialize(SpriteRenderer renderer)
    {
        carrierRenderer = renderer;
        EnsureObjects();
        RefreshLayout();
        SetInactive();
    }

    public void RefreshLayout()
    {
        if (carrierRenderer == null)
            carrierRenderer = GetComponent<SpriteRenderer>();

        EnsureObjects();
        RefreshCarrierMetrics();
        RefreshLightLayout();
        RefreshSorting();
        CacheLayoutState();
    }

    public void SetVisible(bool isVisible)
    {
        visible = isVisible;
        enabled = isVisible;
        EnsureObjects();
        ApplyVisibility();
        if (visible)
            UpdateLightVisuals(true);
    }

    public void SetInactive()
    {
        SetState(CarrierVisualState.Inactive);
        UpdateLightVisuals(true);
    }

    public void SetTransitioning()
    {
        SetState(CarrierVisualState.Transitioning);
        UpdateLightVisuals(true);
    }

    public void SetActive()
    {
        SetState(CarrierVisualState.Active);
        UpdateLightVisuals(true);
    }

    public Vector2 GetEvacuationTargetWorldPosition()
    {
        if (NeedsLayoutRefresh())
            RefreshLayout();

        Vector3 localTarget = new Vector3(
            carrierBounds.min.x + carrierBounds.size.x * HangarTargetNormalizedX,
            carrierBounds.min.y + carrierBounds.size.y * HangarTargetNormalizedY,
            -0.02f);
        return transform.TransformPoint(localTarget);
    }

    void SetState(CarrierVisualState nextState)
    {
        if (state == nextState)
            return;

        state = nextState;
        stateStartedAt = Time.time;
    }

    void LateUpdate()
    {
        if (!visible)
            return;

        if (NeedsLayoutRefresh())
            RefreshLayout();

        UpdateLightVisuals(false);
    }

    void EnsureObjects()
    {
        if (sharedLightSprite == null)
            sharedLightSprite = CreateLightSprite();

        for (int i = 0; i < LightCount; i++)
        {
            if (lightRenderers[i] == null)
                lightRenderers[i] = CreateSpriteRenderer("CarrierLight" + i, sharedLightSprite, i + LightCount);

            if (glowRenderers[i] == null)
                glowRenderers[i] = CreateSpriteRenderer("CarrierLightGlow" + i, sharedLightSprite, i);
        }
    }

    SpriteRenderer CreateSpriteRenderer(string objectName, Sprite sprite, int index)
    {
        Transform existing = transform.Find(objectName);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(objectName);
        obj.transform.SetParent(transform, false);

        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = obj.AddComponent<SpriteRenderer>();

        renderer.sprite = sprite;
        renderer.enabled = visible && sprite != null;
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = GameVisualTheme.ExtractionZoneSortingOrder + 1 + index;
        return renderer;
    }

    void RefreshCarrierMetrics()
    {
        carrierBounds = new Bounds(Vector3.zero, Vector3.one * 4f);
        if (carrierRenderer != null && carrierRenderer.sprite != null)
            carrierBounds = carrierRenderer.sprite.bounds;
    }

    void RefreshLightLayout()
    {
        float carrierSize = Mathf.Max(carrierBounds.size.x, carrierBounds.size.y);
        float spriteSize = sharedLightSprite != null
            ? Mathf.Max(0.01f, Mathf.Max(sharedLightSprite.bounds.size.x, sharedLightSprite.bounds.size.y))
            : 1f;
        float baseScale = Mathf.Max(0.01f, carrierSize * LightSizeFactor / spriteSize);

        for (int i = 0; i < LightCount; i++)
        {
            Vector2 anchor = LightAnchors[i % LightAnchors.Length];
            Vector3 localPosition = new Vector3(
                carrierBounds.min.x + carrierBounds.size.x * anchor.x,
                carrierBounds.min.y + carrierBounds.size.y * anchor.y,
                -0.02f);
            float scaleVariance = Mathf.Lerp(0.82f, 1.18f, Hash01(i, 37));
            ApplyLightTransform(lightRenderers[i], localPosition, baseScale * scaleVariance);
            ApplyLightTransform(glowRenderers[i], localPosition, baseScale * GlowSizeMultiplier * scaleVariance);
        }
    }

    void ApplyLightTransform(SpriteRenderer renderer, Vector3 localPosition, float scale)
    {
        if (renderer == null)
            return;

        renderer.transform.localPosition = localPosition;
        renderer.transform.localRotation = Quaternion.identity;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    void RefreshSorting()
    {
        int sortingLayerId = carrierRenderer != null ? carrierRenderer.sortingLayerID : SortingLayer.NameToID(GameVisualTheme.WorldSortingLayerName);
        int sortingOrder = carrierRenderer != null ? carrierRenderer.sortingOrder : GameVisualTheme.ExtractionZoneSortingOrder;

        for (int i = 0; i < LightCount; i++)
        {
            SetRendererSorting(glowRenderers[i], sortingLayerId, sortingOrder + 1);
            SetRendererSorting(lightRenderers[i], sortingLayerId, sortingOrder + 2);
        }
    }

    void SetRendererSorting(SpriteRenderer renderer, int sortingLayerId, int sortingOrder)
    {
        if (renderer == null)
            return;

        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
    }

    void ApplyVisibility()
    {
        for (int i = 0; i < LightCount; i++)
        {
            if (lightRenderers[i] != null)
                lightRenderers[i].enabled = visible && sharedLightSprite != null;

            if (glowRenderers[i] != null)
                glowRenderers[i].enabled = false;
        }
    }

    void UpdateLightVisuals(bool force)
    {
        if (!visible)
        {
            ApplyVisibility();
            return;
        }

        Color litColor = ResolveLitColor();
        Color dimColor = ResolveDimColor();
        Color glowColor = ResolveGlowColor();

        for (int i = 0; i < LightCount; i++)
        {
            float blink = ResolveLightBlink(i);
            float intensity = Mathf.SmoothStep(0f, 1f, blink);
            Color lightColor = Color.Lerp(dimColor, litColor, intensity);

            if (lightRenderers[i] != null)
            {
                lightRenderers[i].enabled = sharedLightSprite != null;
                lightRenderers[i].color = lightColor;
            }

            if (glowRenderers[i] == null)
                continue;

            bool glowEnabled = state != CarrierVisualState.Inactive && sharedLightSprite != null;
            glowRenderers[i].enabled = glowEnabled;
            if (!glowEnabled && !force)
                continue;

            float glowAlpha = Mathf.Lerp(0.02f, glowColor.a, intensity);
            glowRenderers[i].color = new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha);
        }
    }

    Color ResolveLitColor()
    {
        switch (state)
        {
            case CarrierVisualState.Transitioning:
                return new Color(1f, 0.88f, 0.16f, 1f);
            case CarrierVisualState.Active:
                return new Color(0.26f, 1f, 0.3f, 1f);
            default:
                return new Color(0.22f, 0.23f, 0.22f, 0.28f);
        }
    }

    Color ResolveDimColor()
    {
        switch (state)
        {
            case CarrierVisualState.Transitioning:
                return new Color(0.28f, 0.23f, 0.04f, 0.56f);
            case CarrierVisualState.Active:
                return new Color(0.05f, 0.3f, 0.08f, 0.7f);
            default:
                return new Color(0.04f, 0.045f, 0.05f, 0.24f);
        }
    }

    Color ResolveGlowColor()
    {
        switch (state)
        {
            case CarrierVisualState.Transitioning:
                return new Color(1f, 0.72f, 0.03f, 0.58f);
            case CarrierVisualState.Active:
                return new Color(0.08f, 1f, 0.18f, 0.5f);
            default:
                return Color.clear;
        }
    }

    float ResolveLightBlink(int lightIndex)
    {
        if (state == CarrierVisualState.Inactive)
            return 0f;

        if (state == CarrierVisualState.Transitioning)
        {
            float interval = ResolveEvacBuzzerInterval();
            float phaseOffset = Hash01(lightIndex, 11) * 0.26f + (lightIndex % 4) * 0.03f;
            float phase = Mathf.Repeat((Time.time - stateStartedAt + phaseOffset) / interval, 1f);
            return SoftBeaconPulse(phase, 0.18f, 0.76f, 0.08f);
        }

        float wave = Mathf.Sin((Time.time - stateStartedAt) * 1.6f + lightIndex * 0.41f);
        return Mathf.Clamp01(0.86f + wave * 0.08f);
    }

    bool NeedsLayoutRefresh()
    {
        if (carrierRenderer == null)
            return true;

        return lastCarrierSprite != carrierRenderer.sprite ||
               lastTransformScale != transform.localScale ||
               lastSortingLayerId != carrierRenderer.sortingLayerID ||
               lastSortingOrder != carrierRenderer.sortingOrder;
    }

    void CacheLayoutState()
    {
        if (carrierRenderer == null)
            return;

        lastCarrierSprite = carrierRenderer.sprite;
        lastTransformScale = transform.localScale;
        lastSortingLayerId = carrierRenderer.sortingLayerID;
        lastSortingOrder = carrierRenderer.sortingOrder;
    }

    static float SoftBeaconPulse(float phase, float attackEnd, float releaseEnd, float floor)
    {
        if (phase < attackEnd)
            return Mathf.Lerp(floor, 1f, Mathf.SmoothStep(0f, 1f, phase / attackEnd));

        if (phase < releaseEnd)
            return Mathf.Lerp(1f, floor, Mathf.SmoothStep(0f, 1f, (phase - attackEnd) / (releaseEnd - attackEnd)));

        return floor;
    }

    static float ResolveEvacBuzzerInterval()
    {
        AudioManager audio = AudioManager.Instance;
        return audio != null ? Mathf.Max(0.45f, audio.EvacBuzzerPulseInterval) : 0.5f;
    }

    static float Hash01(int index, int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)(index + 1)) * 16777619u;
            hash = (hash ^ (uint)(salt + 31)) * 16777619u;
            hash ^= hash >> 13;
            hash *= 1274126177u;
            return (hash & 0x00ffffff) / 16777215f;
        }
    }

    static Sprite CreateLightSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
