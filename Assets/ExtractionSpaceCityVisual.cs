using UnityEngine;

public class ExtractionSpaceCityVisual : MonoBehaviour
{
    enum SpaceCityVisualState
    {
        Inactive,
        Transitioning,
        Active
    }

    const int LightCount = 54;
    const float LightSizeFactor = 0.0075f;
    const float GlowSizeMultiplier = 2.35f;
    const float IntakeTargetNormalizedX = 0.81f;
    const float IntakeTargetNormalizedY = 0.37f;

    static readonly Vector2[] LightAnchors =
    {
        new Vector2(0.68f, 0.56f),
        new Vector2(0.73f, 0.59f),
        new Vector2(0.79f, 0.60f),
        new Vector2(0.85f, 0.58f),
        new Vector2(0.90f, 0.54f),
        new Vector2(0.94f, 0.48f),
        new Vector2(0.96f, 0.41f),
        new Vector2(0.95f, 0.34f),
        new Vector2(0.92f, 0.28f),
        new Vector2(0.87f, 0.22f),
        new Vector2(0.81f, 0.20f),
        new Vector2(0.75f, 0.21f),
        new Vector2(0.70f, 0.25f),
        new Vector2(0.66f, 0.31f),
        new Vector2(0.65f, 0.38f),
        new Vector2(0.66f, 0.45f),
        new Vector2(0.68f, 0.52f),
        new Vector2(0.14f, 0.28f),
        new Vector2(0.20f, 0.27f),
        new Vector2(0.26f, 0.26f),
        new Vector2(0.32f, 0.25f),
        new Vector2(0.38f, 0.24f),
        new Vector2(0.44f, 0.23f),
        new Vector2(0.50f, 0.23f),
        new Vector2(0.56f, 0.22f),
        new Vector2(0.08f, 0.45f),
        new Vector2(0.16f, 0.47f),
        new Vector2(0.24f, 0.49f),
        new Vector2(0.32f, 0.50f),
        new Vector2(0.40f, 0.50f),
        new Vector2(0.48f, 0.49f),
        new Vector2(0.56f, 0.47f),
        new Vector2(0.64f, 0.45f),
        new Vector2(0.16f, 0.52f),
        new Vector2(0.23f, 0.57f),
        new Vector2(0.32f, 0.61f),
        new Vector2(0.42f, 0.67f),
        new Vector2(0.51f, 0.76f),
        new Vector2(0.60f, 0.72f),
        new Vector2(0.67f, 0.63f),
        new Vector2(0.75f, 0.56f),
        new Vector2(0.62f, 0.37f),
        new Vector2(0.60f, 0.31f),
        new Vector2(0.58f, 0.25f),
        new Vector2(0.64f, 0.22f),
        new Vector2(0.71f, 0.18f),
        new Vector2(0.06f, 0.33f),
        new Vector2(0.10f, 0.25f),
        new Vector2(0.18f, 0.21f),
        new Vector2(0.28f, 0.18f),
        new Vector2(0.38f, 0.16f),
        new Vector2(0.48f, 0.17f),
        new Vector2(0.58f, 0.18f),
        new Vector2(0.68f, 0.19f)
    };

    static Sprite sharedLightSprite;

    readonly SpriteRenderer[] lightRenderers = new SpriteRenderer[LightCount];
    readonly SpriteRenderer[] glowRenderers = new SpriteRenderer[LightCount];

    SpriteRenderer cityRenderer;
    Sprite lastCitySprite;
    Vector3 lastTransformScale;
    int lastSortingLayerId;
    int lastSortingOrder;
    SpaceCityVisualState state = SpaceCityVisualState.Inactive;
    float stateStartedAt;
    bool visible = true;
    Bounds cityBounds = new Bounds(Vector3.zero, Vector3.one * 4f);

    public void Initialize(SpriteRenderer renderer)
    {
        cityRenderer = renderer;
        EnsureObjects();
        RefreshLayout();
        SetInactive();
    }

    public void RefreshLayout()
    {
        if (cityRenderer == null)
            cityRenderer = GetComponent<SpriteRenderer>();

        EnsureObjects();
        RefreshCityMetrics();
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
        SetState(SpaceCityVisualState.Inactive);
        UpdateLightVisuals(true);
    }

    public void SetTransitioning()
    {
        SetState(SpaceCityVisualState.Transitioning);
        UpdateLightVisuals(true);
    }

    public void SetActive()
    {
        SetState(SpaceCityVisualState.Active);
        UpdateLightVisuals(true);
    }

    public Vector2 GetEvacuationTargetWorldPosition()
    {
        if (NeedsLayoutRefresh())
            RefreshLayout();

        Vector3 localTarget = new Vector3(
            cityBounds.min.x + cityBounds.size.x * IntakeTargetNormalizedX,
            cityBounds.min.y + cityBounds.size.y * IntakeTargetNormalizedY,
            -0.02f);
        return transform.TransformPoint(localTarget);
    }

    void SetState(SpaceCityVisualState nextState)
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
                lightRenderers[i] = CreateSpriteRenderer("SpaceCityLight" + i, sharedLightSprite, i + LightCount);

            if (glowRenderers[i] == null)
                glowRenderers[i] = CreateSpriteRenderer("SpaceCityLightGlow" + i, sharedLightSprite, i);
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

    void RefreshCityMetrics()
    {
        cityBounds = new Bounds(Vector3.zero, Vector3.one * 4f);
        if (cityRenderer != null && cityRenderer.sprite != null)
            cityBounds = cityRenderer.sprite.bounds;
    }

    void RefreshLightLayout()
    {
        float citySize = Mathf.Max(cityBounds.size.x, cityBounds.size.y);
        float spriteSize = sharedLightSprite != null
            ? Mathf.Max(0.01f, Mathf.Max(sharedLightSprite.bounds.size.x, sharedLightSprite.bounds.size.y))
            : 1f;
        float baseScale = Mathf.Max(0.01f, citySize * LightSizeFactor / spriteSize);

        for (int i = 0; i < LightCount; i++)
        {
            Vector2 anchor = LightAnchors[i % LightAnchors.Length];
            Vector3 localPosition = new Vector3(
                cityBounds.min.x + cityBounds.size.x * anchor.x,
                cityBounds.min.y + cityBounds.size.y * anchor.y,
                -0.02f);
            float scaleVariance = Mathf.Lerp(0.82f, 1.15f, Hash01(i, 37));
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
        int sortingLayerId = cityRenderer != null ? cityRenderer.sortingLayerID : SortingLayer.NameToID(GameVisualTheme.WorldSortingLayerName);
        int sortingOrder = cityRenderer != null ? cityRenderer.sortingOrder : GameVisualTheme.ExtractionZoneSortingOrder;

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

            bool glowEnabled = state != SpaceCityVisualState.Inactive && sharedLightSprite != null;
            glowRenderers[i].enabled = glowEnabled;
            if (!glowEnabled && !force)
                continue;

            float glowAlpha = Mathf.Lerp(0.015f, glowColor.a, intensity);
            glowRenderers[i].color = new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha);
        }
    }

    Color ResolveLitColor()
    {
        switch (state)
        {
            case SpaceCityVisualState.Transitioning:
                return new Color(1f, 0.86f, 0.14f, 1f);
            case SpaceCityVisualState.Active:
                return new Color(0.24f, 1f, 0.32f, 1f);
            default:
                return new Color(0.2f, 0.22f, 0.22f, 0.22f);
        }
    }

    Color ResolveDimColor()
    {
        switch (state)
        {
            case SpaceCityVisualState.Transitioning:
                return new Color(0.25f, 0.21f, 0.04f, 0.52f);
            case SpaceCityVisualState.Active:
                return new Color(0.04f, 0.27f, 0.08f, 0.68f);
            default:
                return new Color(0.035f, 0.04f, 0.045f, 0.2f);
        }
    }

    Color ResolveGlowColor()
    {
        switch (state)
        {
            case SpaceCityVisualState.Transitioning:
                return new Color(1f, 0.72f, 0.03f, 0.5f);
            case SpaceCityVisualState.Active:
                return new Color(0.08f, 1f, 0.2f, 0.46f);
            default:
                return Color.clear;
        }
    }

    float ResolveLightBlink(int lightIndex)
    {
        if (state == SpaceCityVisualState.Inactive)
            return 0f;

        if (state == SpaceCityVisualState.Transitioning)
        {
            float interval = ResolveEvacBuzzerInterval();
            float phaseOffset = Hash01(lightIndex, 13) * 0.22f + (lightIndex % 5) * 0.025f;
            float phase = Mathf.Repeat((Time.time - stateStartedAt + phaseOffset) / interval, 1f);
            return SoftBeaconPulse(phase, 0.18f, 0.78f, 0.07f);
        }

        float wave = Mathf.Sin((Time.time - stateStartedAt) * 1.45f + lightIndex * 0.37f);
        return Mathf.Clamp01(0.84f + wave * 0.09f);
    }

    bool NeedsLayoutRefresh()
    {
        if (cityRenderer == null)
            return true;

        return lastCitySprite != cityRenderer.sprite ||
               lastTransformScale != transform.localScale ||
               lastSortingLayerId != cityRenderer.sortingLayerID ||
               lastSortingOrder != cityRenderer.sortingOrder;
    }

    void CacheLayoutState()
    {
        if (cityRenderer == null)
            return;

        lastCitySprite = cityRenderer.sprite;
        lastTransformScale = transform.localScale;
        lastSortingLayerId = cityRenderer.sortingLayerID;
        lastSortingOrder = cityRenderer.sortingOrder;
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
