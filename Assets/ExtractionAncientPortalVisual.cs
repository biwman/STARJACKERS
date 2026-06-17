using UnityEngine;

public class ExtractionAncientPortalVisual : MonoBehaviour
{
    enum AncientPortalVisualState
    {
        Inactive,
        Transitioning,
        Active
    }

    const int LightCount = 4;
    const int RingCount = 4;
    const int RingSegments = 80;
    const int RiftLineCount = 7;
    const int RiftLineSegments = 8;
    const float LightSizeFactor = 0.065f;
    const float GlowSizeMultiplier = 2.45f;
    const float InnerRadiusFactor = 0.43f;

    static readonly Vector2[] LightAnchors =
    {
        new Vector2(0.5f, 0.78f),
        new Vector2(0.78f, 0.5f),
        new Vector2(0.5f, 0.22f),
        new Vector2(0.22f, 0.5f)
    };

    static Sprite sharedLightSprite;
    static Sprite sharedGlowSprite;
    static Material sharedLineMaterial;

    readonly SpriteRenderer[] lightRenderers = new SpriteRenderer[LightCount];
    readonly SpriteRenderer[] glowRenderers = new SpriteRenderer[LightCount];
    readonly LineRenderer[] ringLines = new LineRenderer[RingCount];
    readonly LineRenderer[] riftLines = new LineRenderer[RiftLineCount];

    SpriteRenderer portalRenderer;
    SpriteRenderer centerGlowRenderer;
    Sprite lastPortalSprite;
    Vector3 lastTransformScale;
    int lastSortingLayerId;
    int lastSortingOrder;
    AncientPortalVisualState state = AncientPortalVisualState.Inactive;
    float stateStartedAt;
    bool visible = true;
    Bounds portalBounds = new Bounds(Vector3.zero, Vector3.one * 4f);
    Vector2 portalCenterLocal;
    float portalInnerRadius = 1f;
    Vector3 centerGlowBaseScale = Vector3.one;

    public void Initialize(SpriteRenderer renderer)
    {
        portalRenderer = renderer;
        EnsureObjects();
        RefreshLayout();
        SetInactive();
    }

    public void RefreshLayout()
    {
        if (portalRenderer == null)
            portalRenderer = GetComponent<SpriteRenderer>();

        EnsureObjects();
        RefreshPortalMetrics();
        RefreshLightLayout();
        RefreshCenterLayout();
        RefreshSorting();
        CacheLayoutState();
    }

    public void SetVisible(bool isVisible)
    {
        visible = isVisible;
        enabled = isVisible;
        EnsureObjects();

        if (!visible)
        {
            HideVisuals();
            return;
        }

        RefreshLayout();
        SetPortalEffectsEnabled(state != AncientPortalVisualState.Inactive);
        UpdateLightVisuals(true);
    }

    public void SetInactive()
    {
        SetState(AncientPortalVisualState.Inactive);
        SetPortalEffectsEnabled(false);
        UpdateLightVisuals(true);
    }

    public void SetTransitioning()
    {
        SetState(AncientPortalVisualState.Transitioning);
        SetPortalEffectsEnabled(true);
        UpdateLightVisuals(true);
    }

    public void SetActive()
    {
        SetState(AncientPortalVisualState.Active);
        SetPortalEffectsEnabled(true);
        UpdateLightVisuals(true);
    }

    public Vector2 GetEvacuationTargetWorldPosition()
    {
        if (NeedsLayoutRefresh())
            RefreshLayout();

        return transform.TransformPoint(new Vector3(portalCenterLocal.x, portalCenterLocal.y, -0.02f));
    }

    void SetState(AncientPortalVisualState nextState)
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
        UpdatePortalEffects();
    }

    void EnsureObjects()
    {
        if (sharedLightSprite == null)
            sharedLightSprite = CreateRadialSprite(32, 0.48f);
        if (sharedGlowSprite == null)
            sharedGlowSprite = CreateRadialSprite(96, 0.5f);

        for (int i = 0; i < LightCount; i++)
        {
            if (lightRenderers[i] == null)
                lightRenderers[i] = CreateSpriteRenderer("AncientPortalLight" + i, sharedLightSprite);

            if (glowRenderers[i] == null)
                glowRenderers[i] = CreateSpriteRenderer("AncientPortalLightGlow" + i, sharedGlowSprite);
        }

        if (centerGlowRenderer == null)
            centerGlowRenderer = CreateSpriteRenderer("AncientPortalCenterGlow", sharedGlowSprite);

        for (int i = 0; i < RingCount; i++)
        {
            if (ringLines[i] == null)
                ringLines[i] = CreateLineRenderer("AncientPortalRing" + i, RingSegments + 1);
        }

        for (int i = 0; i < RiftLineCount; i++)
        {
            if (riftLines[i] == null)
                riftLines[i] = CreateLineRenderer("AncientPortalRift" + i, RiftLineSegments);
        }
    }

    SpriteRenderer CreateSpriteRenderer(string objectName, Sprite sprite)
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
        renderer.sortingOrder = GameVisualTheme.ExtractionZoneSortingOrder + 2;
        return renderer;
    }

    LineRenderer CreateLineRenderer(string objectName, int pointCount)
    {
        Transform existing = transform.Find(objectName);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(objectName);
        obj.transform.SetParent(transform, false);

        LineRenderer line = obj.GetComponent<LineRenderer>();
        if (line == null)
            line = obj.AddComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = pointCount;
        line.widthMultiplier = 0.035f;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;
        line.material = GetLineMaterial();
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = GameVisualTheme.ExtractionZoneSortingOrder - 1;
        line.enabled = false;
        return line;
    }

    void RefreshPortalMetrics()
    {
        portalBounds = new Bounds(Vector3.zero, Vector3.one * 4f);
        if (portalRenderer != null && portalRenderer.sprite != null)
            portalBounds = portalRenderer.sprite.bounds;

        portalCenterLocal = portalBounds.center;
        portalInnerRadius = Mathf.Max(0.25f, Mathf.Min(portalBounds.extents.x, portalBounds.extents.y) * InnerRadiusFactor);
    }

    void RefreshLightLayout()
    {
        float portalSize = Mathf.Max(portalBounds.size.x, portalBounds.size.y);
        float lightSpriteSize = sharedLightSprite != null
            ? Mathf.Max(0.01f, Mathf.Max(sharedLightSprite.bounds.size.x, sharedLightSprite.bounds.size.y))
            : 1f;
        float lightScale = Mathf.Max(0.01f, portalSize * LightSizeFactor / lightSpriteSize);

        for (int i = 0; i < LightCount; i++)
        {
            Vector2 anchor = LightAnchors[i % LightAnchors.Length];
            Vector3 localPosition = new Vector3(
                portalBounds.min.x + portalBounds.size.x * anchor.x,
                portalBounds.min.y + portalBounds.size.y * anchor.y,
                -0.03f);

            ApplySpriteTransform(lightRenderers[i], localPosition, lightScale);
            ApplySpriteTransform(glowRenderers[i], localPosition, lightScale * GlowSizeMultiplier);
        }
    }

    void RefreshCenterLayout()
    {
        if (centerGlowRenderer != null)
        {
            float glowSpriteSize = sharedGlowSprite != null
                ? Mathf.Max(0.01f, Mathf.Max(sharedGlowSprite.bounds.size.x, sharedGlowSprite.bounds.size.y))
                : 1f;
            float glowScale = Mathf.Max(0.01f, portalInnerRadius * 2.15f / glowSpriteSize);
            ApplySpriteTransform(centerGlowRenderer, new Vector3(portalCenterLocal.x, portalCenterLocal.y, 0.02f), glowScale);
            centerGlowBaseScale = centerGlowRenderer.transform.localScale;
        }

        float lineWidth = Mathf.Max(0.018f, portalInnerRadius * 0.016f);
        for (int i = 0; i < RingCount; i++)
        {
            if (ringLines[i] != null)
            {
                ringLines[i].widthMultiplier = lineWidth * Mathf.Lerp(0.78f, 1.2f, Hash01(i, 41));
                ringLines[i].positionCount = RingSegments + 1;
            }
        }

        for (int i = 0; i < RiftLineCount; i++)
        {
            if (riftLines[i] != null)
            {
                riftLines[i].widthMultiplier = lineWidth * Mathf.Lerp(0.62f, 1.0f, Hash01(i, 73));
                riftLines[i].positionCount = RiftLineSegments;
            }
        }
    }

    void ApplySpriteTransform(SpriteRenderer renderer, Vector3 localPosition, float scale)
    {
        if (renderer == null)
            return;

        renderer.transform.localPosition = localPosition;
        renderer.transform.localRotation = Quaternion.identity;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    void RefreshSorting()
    {
        int sortingLayerId = portalRenderer != null ? portalRenderer.sortingLayerID : SortingLayer.NameToID(GameVisualTheme.WorldSortingLayerName);
        int sortingOrder = portalRenderer != null ? portalRenderer.sortingOrder : GameVisualTheme.ExtractionZoneSortingOrder;

        if (centerGlowRenderer != null)
        {
            centerGlowRenderer.sortingLayerID = sortingLayerId;
            centerGlowRenderer.sortingOrder = sortingOrder - 1;
        }

        for (int i = 0; i < LightCount; i++)
        {
            SetRendererSorting(glowRenderers[i], sortingLayerId, sortingOrder + 1);
            SetRendererSorting(lightRenderers[i], sortingLayerId, sortingOrder + 2);
        }

        for (int i = 0; i < RingCount; i++)
        {
            if (ringLines[i] == null)
                continue;

            ringLines[i].sortingLayerID = sortingLayerId;
            ringLines[i].sortingOrder = sortingOrder - 1;
        }

        for (int i = 0; i < RiftLineCount; i++)
        {
            if (riftLines[i] == null)
                continue;

            riftLines[i].sortingLayerID = sortingLayerId;
            riftLines[i].sortingOrder = sortingOrder - 1;
        }
    }

    void SetRendererSorting(SpriteRenderer renderer, int sortingLayerId, int sortingOrder)
    {
        if (renderer == null)
            return;

        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
    }

    void UpdateLightVisuals(bool force)
    {
        if (!visible)
        {
            HideVisuals();
            return;
        }

        Color litColor = ResolveLightLitColor();
        Color dimColor = ResolveLightDimColor();
        Color glowColor = ResolveLightGlowColor();

        for (int i = 0; i < LightCount; i++)
        {
            float blink = ResolveLightBlink(i);
            float intensity = Mathf.SmoothStep(0f, 1f, blink);
            Color lightColor = Color.Lerp(dimColor, litColor, intensity);

            if (lightRenderers[i] != null)
            {
                lightRenderers[i].enabled = visible && sharedLightSprite != null;
                lightRenderers[i].color = lightColor;
            }

            if (glowRenderers[i] == null)
                continue;

            bool glowEnabled = visible && state != AncientPortalVisualState.Inactive && sharedGlowSprite != null;
            glowRenderers[i].enabled = glowEnabled;
            if (!glowEnabled && !force)
                continue;

            float glowAlpha = Mathf.Lerp(0.03f, glowColor.a, intensity);
            glowRenderers[i].color = new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha);
        }
    }

    Color ResolveLightLitColor()
    {
        switch (state)
        {
            case AncientPortalVisualState.Transitioning:
                return new Color(1f, 0.86f, 0.08f, 1f);
            case AncientPortalVisualState.Active:
                return new Color(0.18f, 1f, 0.28f, 1f);
            default:
                return new Color(0.25f, 0.28f, 0.3f, 0.55f);
        }
    }

    Color ResolveLightDimColor()
    {
        switch (state)
        {
            case AncientPortalVisualState.Transitioning:
                return new Color(0.32f, 0.25f, 0.02f, 0.72f);
            case AncientPortalVisualState.Active:
                return new Color(0.03f, 0.28f, 0.08f, 0.82f);
            default:
                return new Color(0.04f, 0.055f, 0.07f, 0.34f);
        }
    }

    Color ResolveLightGlowColor()
    {
        switch (state)
        {
            case AncientPortalVisualState.Transitioning:
                return new Color(1f, 0.68f, 0.02f, 0.86f);
            case AncientPortalVisualState.Active:
                return new Color(0.04f, 1f, 0.2f, 0.78f);
            default:
                return Color.clear;
        }
    }

    float ResolveLightBlink(int lightIndex)
    {
        if (state == AncientPortalVisualState.Inactive)
            return 0f;

        if (state == AncientPortalVisualState.Transitioning)
        {
            float interval = ResolveEvacBuzzerInterval();
            float phase = Mathf.Repeat((Time.time - stateStartedAt) / interval - lightIndex * 0.18f, 1f);
            return SoftBeaconPulse(phase, 0.16f, 0.68f, 0.08f);
        }

        float wave = Mathf.Sin((Time.time - stateStartedAt) * 1.45f + lightIndex * 0.55f);
        return Mathf.Clamp01(0.9f + wave * 0.08f);
    }

    void UpdatePortalEffects()
    {
        bool effectsEnabled = visible && state != AncientPortalVisualState.Inactive;
        SetPortalEffectsEnabled(effectsEnabled);
        if (!effectsEnabled)
            return;

        bool transitioning = state == AncientPortalVisualState.Transitioning;
        Color baseColor = transitioning
            ? new Color(1f, 0.72f, 0.12f, 0.72f)
            : new Color(0.24f, 1f, 0.82f, 0.78f);
        Color accentColor = transitioning
            ? new Color(0.44f, 0.88f, 1f, 0.36f)
            : new Color(0.78f, 0.24f, 1f, 0.5f);

        UpdateCenterGlow(baseColor, accentColor, transitioning);
        UpdateRings(baseColor, accentColor, transitioning);
        UpdateRifts(baseColor, accentColor, transitioning);
    }

    void UpdateCenterGlow(Color baseColor, Color accentColor, bool transitioning)
    {
        if (centerGlowRenderer == null)
            return;

        float pulse = transitioning
            ? Mathf.PingPong((Time.time - stateStartedAt) * 5.2f, 1f)
            : 0.58f + Mathf.Sin((Time.time - stateStartedAt) * 2.2f) * 0.16f;
        Color glow = Color.Lerp(accentColor, baseColor, Mathf.Clamp01(pulse));
        glow.a *= transitioning ? 0.42f : 0.62f;
        centerGlowRenderer.color = glow;

        float scalePulse = transitioning
            ? Mathf.Lerp(0.9f, 1.12f, pulse)
            : 1f + Mathf.Sin((Time.time - stateStartedAt) * 1.6f) * 0.06f;
        centerGlowRenderer.transform.localScale = new Vector3(
            centerGlowBaseScale.x * scalePulse,
            centerGlowBaseScale.y * scalePulse,
            1f);
    }

    void UpdateRings(Color baseColor, Color accentColor, bool transitioning)
    {
        for (int i = 0; i < RingCount; i++)
        {
            LineRenderer line = ringLines[i];
            if (line == null)
                continue;

            bool isVisible = !transitioning || Mathf.Repeat(Time.time * 4.1f + i * 0.23f, 1f) < 0.58f;
            line.enabled = isVisible;
            if (!isVisible)
                continue;

            float blend = RingCount <= 1 ? 0f : i / (float)(RingCount - 1);
            Color color = Color.Lerp(baseColor, accentColor, blend);
            float alphaPulse = transitioning
                ? Mathf.PingPong(Time.time * 5.4f + i * 0.29f, 1f)
                : 0.72f + Mathf.Sin(Time.time * 2.4f + i * 0.7f) * 0.16f;
            color.a *= Mathf.Clamp01(alphaPulse);
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.62f);

            UpdateRingLine(line, i, transitioning);
        }
    }

    void UpdateRingLine(LineRenderer line, int index, bool transitioning)
    {
        float radius = portalInnerRadius * Mathf.Lerp(0.42f, 0.92f, Hash01(index, 17));
        float angleOffset = (Time.time - stateStartedAt) * (transitioning ? 78f : 28f) * (index % 2 == 0 ? 1f : -1f);
        float wobbleStrength = portalInnerRadius * (transitioning ? 0.038f : 0.065f);

        for (int i = 0; i <= RingSegments; i++)
        {
            float t = i / (float)RingSegments;
            float angle = t * 360f + angleOffset + index * 31f;
            float wobble = Mathf.Sin(Time.time * (transitioning ? 10f : 3.8f) + index * 2.1f + t * Mathf.PI * 6f) * wobbleStrength;
            Vector2 point = portalCenterLocal + AngleToVector(angle) * (radius + wobble);
            line.SetPosition(i, new Vector3(point.x, point.y, 0.02f));
        }
    }

    void UpdateRifts(Color baseColor, Color accentColor, bool transitioning)
    {
        for (int i = 0; i < RiftLineCount; i++)
        {
            LineRenderer line = riftLines[i];
            if (line == null)
                continue;

            bool isVisible = !transitioning || Mathf.Repeat(Time.time * 3.6f + i * 0.17f, 1f) < 0.5f;
            line.enabled = isVisible;
            if (!isVisible)
                continue;

            Color color = Color.Lerp(accentColor, baseColor, Hash01(i, 29));
            float alpha = transitioning
                ? Mathf.PingPong(Time.time * 6.2f + i * 0.37f, 1f)
                : 0.52f + Mathf.Sin(Time.time * 3.1f + i * 0.49f) * 0.18f;
            color.a *= Mathf.Clamp01(alpha);
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.36f);

            UpdateRiftLine(line, i, transitioning);
        }
    }

    void UpdateRiftLine(LineRenderer line, int index, bool transitioning)
    {
        float time = Time.time - stateStartedAt;
        float startAngle = index * (360f / RiftLineCount) + time * (transitioning ? 95f : 34f);
        float length = portalInnerRadius * Mathf.Lerp(0.58f, 1.02f, Hash01(index, 53));
        float spiral = transitioning ? 48f : 115f;

        for (int i = 0; i < RiftLineSegments; i++)
        {
            float t = RiftLineSegments <= 1 ? 0f : i / (float)(RiftLineSegments - 1);
            float radius = Mathf.Lerp(portalInnerRadius * 0.08f, length, t);
            float angle = startAngle + spiral * t + Mathf.Sin(time * 2.8f + index + t * 5f) * 18f;
            Vector2 point = portalCenterLocal + AngleToVector(angle) * radius;
            line.SetPosition(i, new Vector3(point.x, point.y, 0.02f));
        }
    }

    void SetPortalEffectsEnabled(bool enabled)
    {
        if (centerGlowRenderer != null)
            centerGlowRenderer.enabled = visible && enabled && sharedGlowSprite != null;

        for (int i = 0; i < RingCount; i++)
        {
            if (ringLines[i] != null)
                ringLines[i].enabled = visible && enabled;
        }

        for (int i = 0; i < RiftLineCount; i++)
        {
            if (riftLines[i] != null)
                riftLines[i].enabled = visible && enabled;
        }
    }

    void HideVisuals()
    {
        for (int i = 0; i < LightCount; i++)
        {
            if (lightRenderers[i] != null)
                lightRenderers[i].enabled = false;

            if (glowRenderers[i] != null)
                glowRenderers[i].enabled = false;
        }

        SetPortalEffectsEnabled(false);
    }

    bool NeedsLayoutRefresh()
    {
        if (portalRenderer == null)
            return true;

        return lastPortalSprite != portalRenderer.sprite ||
               lastTransformScale != transform.localScale ||
               lastSortingLayerId != portalRenderer.sortingLayerID ||
               lastSortingOrder != portalRenderer.sortingOrder;
    }

    void CacheLayoutState()
    {
        if (portalRenderer == null)
            return;

        lastPortalSprite = portalRenderer.sprite;
        lastTransformScale = transform.localScale;
        lastSortingLayerId = portalRenderer.sortingLayerID;
        lastSortingOrder = portalRenderer.sortingOrder;
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

    static Vector2 AngleToVector(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
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

    static Sprite CreateRadialSprite(int size, float radiusFactor)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * Mathf.Clamp(radiusFactor, 0.1f, 0.5f);
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

    static Material GetLineMaterial()
    {
        if (sharedLineMaterial != null)
            return sharedLineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        sharedLineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedLineMaterial;
    }
}
