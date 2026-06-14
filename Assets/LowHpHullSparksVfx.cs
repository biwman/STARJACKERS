using UnityEngine;

[DisallowMultipleComponent]
public sealed class LowHpHullSparksVfx : MonoBehaviour
{
    const int ArcCount = 7;
    const float HpThresholdRatio = 0.5f;
    const float EffectZOffset = -0.36f;
    const float SettingsCheckInterval = 0.35f;

    static Material lineMaterial;

    readonly Color coldBlue = new Color(0.32f, 0.72f, 1f, 1f);
    readonly Color hotCore = new Color(0.88f, 0.96f, 1f, 1f);
    readonly Color warmCopper = new Color(1f, 0.58f, 0.22f, 1f);

    LineRenderer[] arcs;
    Vector2[] localStart;
    Vector2[] localEnd;
    float[] arcStartedAt;
    float[] arcDuration;
    float[] arcBend;
    float[] arcSeed;
    float[] arcWidth;
    SpriteRenderer spriteRenderer;
    PlayerHealth health;
    bool settingsAllowEffect;
    float nextSettingsCheck;
    float nextBurstTime;
    bool anyArcEnabled;
    bool effectRunning;

    public static void Prewarm()
    {
        GetLineMaterial();
    }

    public static void AttachIfNeeded(GameObject target)
    {
        if (target == null || target.GetComponent<LowHpHullSparksVfx>() != null)
            return;

        target.AddComponent<LowHpHullSparksVfx>();
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        health = GetComponent<PlayerHealth>();
        CreateArcPool();
        DisableAllArcs();
        ScheduleNextBurst(true);
    }

    void OnDisable()
    {
        DisableAllArcs();
        effectRunning = false;
    }

    void Update()
    {
        if (!ShouldRun())
        {
            if (anyArcEnabled)
                DisableAllArcs();
            effectRunning = false;
            return;
        }

        if (!effectRunning)
        {
            effectRunning = true;
            ScheduleNextBurst(true);
        }

        float now = Time.time;
        UpdateActiveArcs(now);

        if (now >= nextBurstTime)
        {
            StartBurst(now);
            ScheduleNextBurst(false);
        }
    }

    bool ShouldRun()
    {
        if (health == null || spriteRenderer == null || !spriteRenderer.enabled)
            return false;

        if (health.IsWreck || health.IsEvacuationAnimating || health.IsBotControlled ||
            health.IsNeutralRiderControlled || health.IsAstronautControlled)
        {
            return false;
        }

        if (health.CurrentHP <= 0 || health.maxHP <= 0)
            return false;

        if (health.CurrentHP >= health.maxHP * HpThresholdRatio)
            return false;

        if (Time.unscaledTime >= nextSettingsCheck)
        {
            settingsAllowEffect = RoomSettings.AreVisualEffectsEnabled() && RoomSettings.AreLowHpHullSparksEnabled();
            nextSettingsCheck = Time.unscaledTime + SettingsCheckInterval;
        }

        return settingsAllowEffect;
    }

    void CreateArcPool()
    {
        arcs = new LineRenderer[ArcCount];
        localStart = new Vector2[ArcCount];
        localEnd = new Vector2[ArcCount];
        arcStartedAt = new float[ArcCount];
        arcDuration = new float[ArcCount];
        arcBend = new float[ArcCount];
        arcSeed = new float[ArcCount];
        arcWidth = new float[ArcCount];

        int sortingLayerId = spriteRenderer != null
            ? spriteRenderer.sortingLayerID
            : SortingLayer.NameToID(GameVisualTheme.WorldSortingLayerName);
        int sortingOrder = spriteRenderer != null
            ? spriteRenderer.sortingOrder + 14
            : GameVisualTheme.PlayerSortingOrder + 14;

        for (int i = 0; i < ArcCount; i++)
        {
            arcs[i] = CreateArcLine("LowHpHullSpark_" + i, sortingLayerId, sortingOrder + i);
        }
    }

    LineRenderer CreateArcLine(string objectName, int sortingLayerId, int sortingOrder)
    {
        GameObject arcObject = new GameObject(objectName);
        arcObject.transform.SetParent(transform, false);

        LineRenderer line = arcObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 4;
        line.widthMultiplier = 0.018f;
        line.startWidth = 0.018f;
        line.endWidth = 0.006f;
        line.numCapVertices = 3;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetLineMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        line.enabled = false;
        return line;
    }

    void StartBurst(float now)
    {
        RefreshSorting();

        float hpRatio = health != null && health.maxHP > 0
            ? Mathf.Clamp01(health.CurrentHP / (float)health.maxHP)
            : 1f;
        float damageIntensity = Mathf.InverseLerp(HpThresholdRatio, 0.08f, hpRatio);
        int burstCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, 3f, damageIntensity)) + Random.Range(0, 2), 1, 4);

        for (int i = 0; i < burstCount; i++)
        {
            int arcIndex = FindFreeArcIndex(now);
            if (arcIndex < 0)
                return;

            ConfigureArc(arcIndex, now, damageIntensity);
        }
    }

    int FindFreeArcIndex(float now)
    {
        if (arcs == null)
            return -1;

        for (int i = 0; i < arcs.Length; i++)
        {
            if (arcs[i] == null || !arcs[i].enabled || now - arcStartedAt[i] >= arcDuration[i])
                return i;
        }

        return -1;
    }

    void ConfigureArc(int index, float now, float damageIntensity)
    {
        Bounds localBounds = ResolveLocalSpriteBounds();
        Vector2 extents = new Vector2(
            Mathf.Max(0.05f, localBounds.extents.x),
            Mathf.Max(0.05f, localBounds.extents.y));

        float baseAngle = Random.Range(0f, Mathf.PI * 2f);
        float span = Random.Range(0.18f, Mathf.Lerp(0.32f, 0.52f, damageIntensity));
        float radius = Random.Range(0.38f, 0.86f);
        float radiusJitter = Random.Range(-0.08f, 0.08f);

        localStart[index] = PointOnHull(localBounds.center, extents, baseAngle - span, Mathf.Clamp01(radius + radiusJitter));
        localEnd[index] = PointOnHull(localBounds.center, extents, baseAngle + span, Mathf.Clamp01(radius - radiusJitter));
        arcStartedAt[index] = now;
        arcDuration[index] = Random.Range(0.085f, Mathf.Lerp(0.14f, 0.21f, damageIntensity));
        arcBend[index] = Random.Range(-0.08f, 0.08f) * Mathf.Max(extents.x, extents.y);
        arcSeed[index] = Random.Range(0f, 1000f);
        arcWidth[index] = Mathf.Lerp(0.011f, 0.026f, damageIntensity) * Random.Range(0.84f, 1.18f);

        if (arcs[index] != null)
            arcs[index].enabled = true;
        anyArcEnabled = true;
        UpdateArcVisual(index, now);
    }

    void UpdateActiveArcs(float now)
    {
        if (arcs == null || !anyArcEnabled)
            return;

        bool stillEnabled = false;
        for (int i = 0; i < arcs.Length; i++)
        {
            LineRenderer arc = arcs[i];
            if (arc == null || !arc.enabled)
                continue;

            if (now - arcStartedAt[i] >= arcDuration[i])
            {
                arc.enabled = false;
                continue;
            }

            UpdateArcVisual(i, now);
            stillEnabled = true;
        }

        anyArcEnabled = stillEnabled;
    }

    void UpdateArcVisual(int index, float now)
    {
        LineRenderer arc = arcs[index];
        if (arc == null)
            return;

        float t = Mathf.Clamp01((now - arcStartedAt[index]) / Mathf.Max(0.01f, arcDuration[index]));
        Vector2 start = localStart[index];
        Vector2 end = localEnd[index];
        Vector2 center = Vector2.Lerp(start, end, 0.5f);
        Vector2 direction = end - start;
        Vector2 normal = direction.sqrMagnitude > 0.0001f
            ? new Vector2(-direction.y, direction.x).normalized
            : Vector2.up;

        float flicker = 0.72f + (Mathf.Sin((now * 86f) + arcSeed[index]) * 0.28f);
        float bendPulse = Mathf.Sin((now * 54f) + arcSeed[index]) * 0.018f;
        Vector2 midA = Vector2.Lerp(start, center, 0.58f) + normal * (arcBend[index] + bendPulse);
        Vector2 midB = Vector2.Lerp(center, end, 0.42f) - normal * (arcBend[index] * 0.58f - bendPulse);

        arc.SetPosition(0, ToEffectWorld(start));
        arc.SetPosition(1, ToEffectWorld(midA));
        arc.SetPosition(2, ToEffectWorld(midB));
        arc.SetPosition(3, ToEffectWorld(end));

        float alpha = Mathf.Pow(1f - t, 1.65f) * flicker;
        Color startColor = Color.Lerp(coldBlue, warmCopper, t * 0.55f);
        Color endColor = Color.Lerp(hotCore, coldBlue, t * 0.35f);
        startColor.a = alpha * 0.42f;
        endColor.a = alpha;
        arc.startColor = startColor;
        arc.endColor = endColor;

        float width = arcWidth[index] * Mathf.Lerp(1f, 0.28f, t) * flicker;
        arc.widthMultiplier = width;
        arc.startWidth = width * 0.62f;
        arc.endWidth = width * 0.28f;
    }

    void DisableAllArcs()
    {
        anyArcEnabled = false;
        if (arcs == null)
            return;

        for (int i = 0; i < arcs.Length; i++)
        {
            if (arcs[i] != null)
                arcs[i].enabled = false;
        }
    }

    void ScheduleNextBurst(bool firstDelay)
    {
        float hpRatio = health != null && health.maxHP > 0
            ? Mathf.Clamp01(health.CurrentHP / (float)health.maxHP)
            : 1f;
        float damageIntensity = Mathf.InverseLerp(HpThresholdRatio, 0.08f, hpRatio);
        float minDelay = firstDelay ? 0.35f : Mathf.Lerp(1.18f, 0.48f, damageIntensity);
        float maxDelay = firstDelay ? 1.45f : Mathf.Lerp(2.65f, 1.18f, damageIntensity);
        nextBurstTime = Time.time + Random.Range(minDelay, maxDelay);
    }

    void RefreshSorting()
    {
        if (spriteRenderer == null || arcs == null)
            return;

        int layerId = spriteRenderer.sortingLayerID;
        int order = spriteRenderer.sortingOrder + 14;
        for (int i = 0; i < arcs.Length; i++)
        {
            if (arcs[i] == null)
                continue;

            arcs[i].sortingLayerID = layerId;
            arcs[i].sortingOrder = order + i;
        }
    }

    Bounds ResolveLocalSpriteBounds()
    {
        if (spriteRenderer != null && spriteRenderer.sprite != null)
            return spriteRenderer.sprite.bounds;

        return new Bounds(Vector3.zero, Vector3.one);
    }

    Vector3 ToEffectWorld(Vector2 localPoint)
    {
        Vector3 world = transform.TransformPoint(new Vector3(localPoint.x, localPoint.y, 0f));
        world.z = transform.position.z + EffectZOffset;
        return world;
    }

    static Vector2 PointOnHull(Vector3 center, Vector2 extents, float angle, float radius)
    {
        radius = Mathf.Clamp(radius, 0.18f, 0.92f);
        return new Vector2(
            center.x + Mathf.Cos(angle) * extents.x * radius,
            center.y + Mathf.Sin(angle) * extents.y * radius);
    }

    static Material GetLineMaterial()
    {
        if (lineMaterial != null)
            return lineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        lineMaterial = new Material(shader)
        {
            name = "LowHpHullSparksVfxMaterial",
            color = Color.white
        };
        lineMaterial.renderQueue = 5000;
        return lineMaterial;
    }
}
