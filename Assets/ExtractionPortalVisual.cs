using UnityEngine;

public class ExtractionPortalVisual : MonoBehaviour
{
    enum PortalVisualState
    {
        Inactive,
        Transitioning,
        Active
    }

    const string AlarmSpriteResourcePath = "portal_alarm_light";
    const int AlarmCount = 3;
    const int CurrentCount = 7;
    const int CurrentSegments = 6;
    const float AlarmSizeFactor = 0.15f;
    const float AlarmRadialOffset = 0.82f;
    const float CurrentInnerRadiusFactor = 0.48f;

    static Material sharedLineMaterial;

    readonly SpriteRenderer[] alarmRenderers = new SpriteRenderer[AlarmCount];
    readonly SpriteRenderer[] alarmGlowRenderers = new SpriteRenderer[AlarmCount];
    readonly LineRenderer[] currentLines = new LineRenderer[CurrentCount];
    readonly Vector3[] alarmDirections =
    {
        Vector3.up,
        Vector3.left,
        Vector3.right
    };
    readonly float[] alarmRotations = { 0f, 90f, -90f };

    SpriteRenderer portalRenderer;
    Sprite alarmSprite;
    Sprite lastPortalSprite;
    Vector3 lastTransformScale;
    int lastSortingLayerId;
    int lastSortingOrder;
    PortalVisualState state = PortalVisualState.Inactive;
    float stateStartedAt;
    bool visible = true;
    Vector2 portalCenterLocal;
    float portalInnerRadius = 1f;

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
        RefreshAlarmLayout();
        RefreshCurrentLayout();
        RefreshSorting();
        CacheLayoutState();
    }

    public void SetInactive()
    {
        SetState(PortalVisualState.Inactive);
        SetCurrentsEnabled(false);
        UpdateAlarmVisuals(true);
    }

    public void SetTransitioning()
    {
        SetState(PortalVisualState.Transitioning);
        SetCurrentsEnabled(true);
        UpdateAlarmVisuals(true);
    }

    public void SetActive()
    {
        SetState(PortalVisualState.Active);
        SetCurrentsEnabled(true);
        UpdateAlarmVisuals(true);
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
        SetCurrentsEnabled(state != PortalVisualState.Inactive);
        UpdateAlarmVisuals(true);
    }

    public Vector2 GetEvacuationTargetWorldPosition()
    {
        if (NeedsLayoutRefresh())
            RefreshLayout();

        return transform.TransformPoint(new Vector3(portalCenterLocal.x, portalCenterLocal.y, -0.02f));
    }

    void SetState(PortalVisualState nextState)
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

        UpdateAlarmVisuals(false);
        UpdateCurrentVisuals();
    }

    void EnsureObjects()
    {
        if (alarmSprite == null)
            alarmSprite = LoadSprite(AlarmSpriteResourcePath);

        for (int i = 0; i < AlarmCount; i++)
        {
            if (alarmRenderers[i] == null)
                alarmRenderers[i] = CreateSpriteRenderer("PortalAlarm" + i, alarmSprite, i);

            if (alarmGlowRenderers[i] == null)
                alarmGlowRenderers[i] = CreateSpriteRenderer("PortalAlarmGlow" + i, alarmSprite, i + AlarmCount);
        }

        for (int i = 0; i < CurrentCount; i++)
        {
            if (currentLines[i] == null)
                currentLines[i] = CreateCurrentLine("PortalCurrent" + i);
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

    LineRenderer CreateCurrentLine(string objectName)
    {
        Transform existing = transform.Find(objectName);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(objectName);
        obj.transform.SetParent(transform, false);

        LineRenderer line = obj.GetComponent<LineRenderer>();
        if (line == null)
            line = obj.AddComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = CurrentSegments;
        line.widthMultiplier = 0.045f;
        line.numCapVertices = 3;
        line.numCornerVertices = 3;
        line.alignment = LineAlignment.View;
        line.material = GetLineMaterial();
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = GameVisualTheme.ExtractionZoneSortingOrder + 4;
        line.enabled = false;
        return line;
    }

    void RefreshPortalMetrics()
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
        if (portalRenderer != null && portalRenderer.sprite != null)
            bounds = portalRenderer.sprite.bounds;

        portalCenterLocal = bounds.center;
        portalInnerRadius = Mathf.Max(0.2f, Mathf.Min(bounds.extents.x, bounds.extents.y) * CurrentInnerRadiusFactor);
    }

    void RefreshAlarmLayout()
    {
        Bounds portalBounds = portalRenderer != null && portalRenderer.sprite != null
            ? portalRenderer.sprite.bounds
            : new Bounds(Vector3.zero, Vector3.one * 4f);
        float portalSize = Mathf.Min(portalBounds.size.x, portalBounds.size.y);
        float alarmSpriteSize = 1f;
        if (alarmSprite != null)
            alarmSpriteSize = Mathf.Max(0.01f, Mathf.Max(alarmSprite.bounds.size.x, alarmSprite.bounds.size.y));

        float alarmScale = Mathf.Max(0.01f, portalSize * AlarmSizeFactor / alarmSpriteSize);
        Vector3 center = portalBounds.center;
        Vector3 extents = portalBounds.extents;

        for (int i = 0; i < AlarmCount; i++)
        {
            Vector3 direction = alarmDirections[i];
            Vector3 localPosition = center + new Vector3(
                direction.x * extents.x * AlarmRadialOffset,
                direction.y * extents.y * AlarmRadialOffset,
                -0.01f);

            ApplyAlarmTransform(alarmRenderers[i], localPosition, alarmScale, alarmRotations[i]);
            ApplyAlarmTransform(alarmGlowRenderers[i], localPosition, alarmScale * 1.18f, alarmRotations[i]);
        }
    }

    void ApplyAlarmTransform(SpriteRenderer renderer, Vector3 localPosition, float scale, float rotation)
    {
        if (renderer == null)
            return;

        renderer.transform.localPosition = localPosition;
        renderer.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    void RefreshCurrentLayout()
    {
        for (int i = 0; i < currentLines.Length; i++)
        {
            LineRenderer line = currentLines[i];
            if (line == null)
                continue;

            line.widthMultiplier = Mathf.Max(0.025f, portalInnerRadius * 0.018f);
            line.positionCount = CurrentSegments;
        }
    }

    void RefreshSorting()
    {
        int sortingLayerId = portalRenderer != null ? portalRenderer.sortingLayerID : SortingLayer.NameToID(GameVisualTheme.WorldSortingLayerName);
        int sortingOrder = portalRenderer != null ? portalRenderer.sortingOrder : GameVisualTheme.ExtractionZoneSortingOrder;

        for (int i = 0; i < AlarmCount; i++)
        {
            SetRendererSorting(alarmRenderers[i], sortingLayerId, sortingOrder + 2);
            SetRendererSorting(alarmGlowRenderers[i], sortingLayerId, sortingOrder + 1);
        }

        for (int i = 0; i < currentLines.Length; i++)
        {
            if (currentLines[i] == null)
                continue;

            currentLines[i].sortingLayerID = sortingLayerId;
            currentLines[i].sortingOrder = sortingOrder + 3 + i;
        }
    }

    void SetRendererSorting(SpriteRenderer renderer, int sortingLayerId, int sortingOrder)
    {
        if (renderer == null)
            return;

        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
    }

    void UpdateAlarmVisuals(bool force)
    {
        if (!visible)
        {
            HideVisuals();
            return;
        }

        Color litColor = ResolveAlarmLitColor();
        Color dimColor = ResolveAlarmDimColor();
        Color glowColor = ResolveAlarmGlowColor();

        for (int i = 0; i < AlarmCount; i++)
        {
            float blink = ResolveAlarmBlink(i);
            float flash = Mathf.SmoothStep(0f, 1f, blink);
            Color lampColor = Color.Lerp(dimColor, litColor, flash);

            if (alarmRenderers[i] != null)
            {
                alarmRenderers[i].color = lampColor;
                alarmRenderers[i].enabled = visible && alarmSprite != null;
            }

            if (alarmGlowRenderers[i] == null)
                continue;

            bool glowEnabled = visible && state != PortalVisualState.Inactive && alarmSprite != null;
            alarmGlowRenderers[i].enabled = glowEnabled;
            if (!glowEnabled && !force)
                continue;

            float glowAlpha = Mathf.Lerp(0.02f, glowColor.a, flash);
            alarmGlowRenderers[i].color = new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha);

            Vector3 scale = alarmRenderers[i] != null ? alarmRenderers[i].transform.localScale : Vector3.one;
            float glowScale = Mathf.Lerp(1.05f, state == PortalVisualState.Active ? 1.34f : 1.28f, flash);
            alarmGlowRenderers[i].transform.localScale = new Vector3(scale.x * glowScale, scale.y * glowScale, 1f);
        }
    }

    Color ResolveAlarmLitColor()
    {
        switch (state)
        {
            case PortalVisualState.Transitioning:
                return new Color(1f, 0.9f, 0.14f, 1f);
            case PortalVisualState.Active:
                return new Color(0.32f, 1f, 0.26f, 1f);
            default:
            return new Color(0.42f, 0.42f, 0.42f, 0.82f);
        }
    }

    Color ResolveAlarmDimColor()
    {
        switch (state)
        {
            case PortalVisualState.Transitioning:
                return new Color(0.34f, 0.28f, 0.05f, 0.86f);
            case PortalVisualState.Active:
                return new Color(0.04f, 0.3f, 0.07f, 0.86f);
            default:
                return new Color(0.26f, 0.26f, 0.26f, 0.76f);
        }
    }

    Color ResolveAlarmGlowColor()
    {
        switch (state)
        {
            case PortalVisualState.Transitioning:
                return new Color(1f, 0.76f, 0.02f, 0.9f);
            case PortalVisualState.Active:
                return new Color(0.08f, 1f, 0.18f, 0.95f);
            default:
                return Color.clear;
        }
    }

    float ResolveAlarmBlink(int alarmIndex)
    {
        if (state == PortalVisualState.Inactive)
            return 0f;

        if (state == PortalVisualState.Transitioning)
        {
            float interval = ResolveEvacBuzzerInterval();
            float phase = Mathf.Repeat((Time.time - stateStartedAt + alarmIndex * 0.04f) / interval, 1f);
            return SoftBeaconPulse(phase, 0.16f, 0.74f, 0.12f);
        }

        if (state == PortalVisualState.Active)
        {
            float interval = Mathf.Max(0.7f, ResolveEvacBuzzerInterval() * 1.35f);
            float phase = Mathf.Repeat((Time.time - stateStartedAt + alarmIndex * 0.05f) / interval, 1f);
            return SoftBeaconPulse(phase, 0.22f, 0.82f, 0.18f);
        }

        return 0f;
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

    void UpdateCurrentVisuals()
    {
        if (!visible)
            return;

        if (state == PortalVisualState.Inactive)
            return;

        bool transitioning = state == PortalVisualState.Transitioning;
        Color color = transitioning
            ? new Color(1f, 0.84f, 0.18f, 0.82f)
            : new Color(0.48f, 1f, 0.52f, 0.9f);

        for (int i = 0; i < currentLines.Length; i++)
        {
            LineRenderer line = currentLines[i];
            if (line == null)
                continue;

            bool visible = !transitioning || Mathf.Repeat(Time.time * 3.7f + i * 0.21f, 1f) < 0.48f;
            line.enabled = visible;
            if (!visible)
                continue;

            float alphaPulse = transitioning
                ? Mathf.PingPong(Time.time * 6.5f + i * 0.31f, 1f)
                : 0.72f + Mathf.Sin(Time.time * 3.1f + i) * 0.18f;
            Color animatedColor = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * alphaPulse));
            line.startColor = animatedColor;
            line.endColor = new Color(color.r, color.g, color.b, animatedColor.a * 0.55f);

            UpdateCurrentLine(line, i, transitioning);
        }
    }

    void UpdateCurrentLine(LineRenderer line, int index, bool dashed)
    {
        float radius = portalInnerRadius * Mathf.Lerp(0.42f, 0.92f, Hash01(index, 13));
        float angularVelocity = dashed ? 115f : 46f;
        float startAngle = Time.time * angularVelocity + index * 57f + Hash01(index, 29) * 90f;
        float span = dashed
            ? Mathf.Lerp(20f, 54f, Hash01(index, 47))
            : Mathf.Lerp(90f, 175f, Hash01(index, 47));

        for (int i = 0; i < CurrentSegments; i++)
        {
            float t = CurrentSegments <= 1 ? 0f : i / (float)(CurrentSegments - 1);
            float angle = startAngle + Mathf.Lerp(-span * 0.5f, span * 0.5f, t);
            float wobble = Mathf.Sin(Time.time * (dashed ? 12f : 5.5f) + index * 1.9f + i * 0.8f) * portalInnerRadius * 0.055f;
            Vector2 point = portalCenterLocal + AngleToVector(angle) * (radius + wobble);
            line.SetPosition(i, new Vector3(point.x, point.y, -0.02f));
        }
    }

    void SetCurrentsEnabled(bool enabled)
    {
        for (int i = 0; i < currentLines.Length; i++)
        {
            if (currentLines[i] != null)
                currentLines[i].enabled = visible && enabled;
        }
    }

    void HideVisuals()
    {
        for (int i = 0; i < AlarmCount; i++)
        {
            if (alarmRenderers[i] != null)
                alarmRenderers[i].enabled = false;

            if (alarmGlowRenderers[i] != null)
                alarmGlowRenderers[i].enabled = false;
        }

        for (int i = 0; i < currentLines.Length; i++)
        {
            if (currentLines[i] != null)
                currentLines[i].enabled = false;
        }
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

    static Sprite LoadSprite(string resourcesPath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcesPath);
        if (sprites != null && sprites.Length > 0)
            return sprites[0];

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/" + resourcesPath + ".png");
#else
        return null;
#endif
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
