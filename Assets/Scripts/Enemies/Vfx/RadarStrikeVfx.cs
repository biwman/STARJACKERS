using UnityEngine;

public sealed class RadarStrikeVfx : MonoBehaviour
{
    enum VfxMode
    {
        Marker,
        Impact
    }

    const float MarkerZ = -0.34f;
    const float ImpactZ = -0.35f;
    const int MarkerSortingOrder = 7100;
    const int ImpactSortingOrder = 7150;

    static Material lineMaterial;

    VfxMode mode;
    Vector2 worldPosition;
    float duration;
    float radius;
    float startedAt;
    LineRenderer outerGlow;
    LineRenderer outerCore;
    LineRenderer innerCore;
    LineRenderer crossA;
    LineRenderer crossB;
    LineRenderer impactFlash;
    LineRenderer impactRing;
    LineRenderer impactRingOuter;
    LineRenderer impactBeam;
    LineRenderer impactCoreGlow;
    LineRenderer[] impactSparks;

    public static void SpawnMarker(Vector2 position, float warningDuration, float radius)
    {
        GameObject effect = new GameObject("RadarStrikeMarkerVfx");
        RadarStrikeVfx vfx = effect.AddComponent<RadarStrikeVfx>();
        vfx.InitializeMarker(position, warningDuration, radius);
    }

    public static void SpawnImpact(Vector2 position, float radius)
    {
        GameObject effect = new GameObject("RadarStrikeImpactVfx");
        RadarStrikeVfx vfx = effect.AddComponent<RadarStrikeVfx>();
        vfx.InitializeImpact(position, radius);
    }

    void InitializeMarker(Vector2 position, float warningDuration, float configuredRadius)
    {
        mode = VfxMode.Marker;
        worldPosition = position;
        duration = Mathf.Max(0.2f, warningDuration);
        radius = Mathf.Max(0.45f, configuredRadius);
        startedAt = Time.time;
        EnsureMarkerLines();
        transform.position = new Vector3(position.x, position.y, MarkerZ);
    }

    void InitializeImpact(Vector2 position, float configuredRadius)
    {
        mode = VfxMode.Impact;
        worldPosition = position;
        duration = 0.92f;
        radius = Mathf.Max(0.45f, configuredRadius);
        startedAt = Time.time;
        EnsureImpactLines();
        transform.position = new Vector3(position.x, position.y, ImpactZ);
    }

    void Update()
    {
        float elapsed = Time.time - startedAt;
        float progress = duration > 0.001f ? Mathf.Clamp01(elapsed / duration) : 1f;

        if (mode == VfxMode.Marker)
        {
            UpdateMarker(progress);
            if (progress >= 1f)
                Destroy(gameObject);
            return;
        }

        UpdateImpact(progress);
        if (progress >= 1f)
            Destroy(gameObject);
    }

    void EnsureMarkerLines()
    {
        outerGlow = CreateLine("OuterGlow", MarkerSortingOrder, 0.22f);
        outerCore = CreateLine("OuterCore", MarkerSortingOrder + 1, 0.08f);
        innerCore = CreateLine("InnerCore", MarkerSortingOrder + 2, 0.05f);
        crossA = CreateLine("CrossA", MarkerSortingOrder + 3, 0.07f);
        crossB = CreateLine("CrossB", MarkerSortingOrder + 3, 0.07f);

        outerGlow.loop = true;
        outerCore.loop = true;
        innerCore.loop = true;
    }

    void EnsureImpactLines()
    {
        impactBeam = CreateLine("ImpactBeam", ImpactSortingOrder, 0.24f);
        impactCoreGlow = CreateLine("ImpactCoreGlow", ImpactSortingOrder + 1, 0.34f);
        impactFlash = CreateLine("ImpactFlash", ImpactSortingOrder + 2, 0.16f);
        impactRing = CreateLine("ImpactRing", ImpactSortingOrder + 3, 0.12f);
        impactRingOuter = CreateLine("ImpactRingOuter", ImpactSortingOrder + 2, 0.22f);
        impactSparks = new LineRenderer[7];
        for (int i = 0; i < impactSparks.Length; i++)
            impactSparks[i] = CreateLine("ImpactSpark" + i, ImpactSortingOrder + 4 + i, 0.06f + i * 0.004f);

        impactCoreGlow.loop = true;
        impactRing.loop = true;
        impactRingOuter.loop = true;
    }

    void UpdateMarker(float progress)
    {
        float pulse = Mathf.Sin(Time.time * 9.4f) * 0.5f + 0.5f;
        float urgency = Mathf.SmoothStep(0f, 1f, progress);
        Color warning = Color.Lerp(new Color(1f, 0.68f, 0.2f, 0.95f), new Color(1f, 0.12f, 0.08f, 1f), urgency);
        Color glow = new Color(warning.r, warning.g * 0.9f, warning.b * 0.9f, Mathf.Lerp(0.24f, 0.52f, pulse));
        float outerRadius = radius * Mathf.Lerp(0.86f, 1.08f, pulse);
        float innerRadius = radius * 0.58f;
        float crossExtent = radius * 0.7f;

        UpdateRing(outerGlow, outerRadius, glow, MarkerSortingOrder, 36, 0.06f);
        UpdateRing(outerCore, radius, new Color(1f, 0.98f, 0.9f, 0.96f), MarkerSortingOrder + 1, 32, 0.03f);
        UpdateRing(innerCore, innerRadius, new Color(warning.r, warning.g, warning.b, 0.92f), MarkerSortingOrder + 2, 28, 0.04f);

        Vector3 center = new Vector3(worldPosition.x, worldPosition.y, MarkerZ);
        UpdateSimpleLine(crossA, center + new Vector3(-crossExtent, 0f, 0f), center + new Vector3(crossExtent, 0f, 0f), warning, MarkerSortingOrder + 3);
        UpdateSimpleLine(crossB, center + new Vector3(0f, -crossExtent, 0f), center + new Vector3(0f, crossExtent, 0f), warning, MarkerSortingOrder + 3);
    }

    void UpdateImpact(float progress)
    {
        float inverse = 1f - progress;
        float blast = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 1.25f));
        Color hot = Color.Lerp(new Color(1f, 0.98f, 0.9f, 1f), new Color(1f, 0.36f, 0.08f, 0f), blast);
        Color glow = Color.Lerp(new Color(1f, 0.86f, 0.52f, 0.92f), new Color(0.46f, 0.16f, 0.06f, 0f), progress);
        Color ring = Color.Lerp(new Color(1f, 0.62f, 0.18f, 0.96f), new Color(0.82f, 0.1f, 0.04f, 0f), progress);
        Vector3 center = new Vector3(worldPosition.x, worldPosition.y, ImpactZ);

        float beamLength = Mathf.Lerp(radius * 3.1f, radius * 0.45f, blast);
        UpdateSimpleLine(
            impactBeam,
            center + new Vector3(radius * 0.08f, beamLength, 0f),
            center,
            new Color(1f, 0.88f, 0.58f, inverse * 0.86f),
            ImpactSortingOrder);

        float coreRadius = Mathf.Lerp(radius * 0.32f, radius * 0.92f, blast);
        float flashRadius = Mathf.Lerp(radius * 0.22f, radius * 1.14f, Mathf.SmoothStep(0f, 1f, progress));
        UpdateRing(impactCoreGlow, coreRadius, glow, ImpactSortingOrder + 1, 24, 0.16f);
        UpdateRing(impactFlash, flashRadius, hot, ImpactSortingOrder + 2, 22, 0.12f);
        UpdateRing(impactRing, Mathf.Lerp(radius * 0.18f, radius * 1.0f, progress), ring, ImpactSortingOrder + 3, 28, 0.18f);
        UpdateRing(impactRingOuter, Mathf.Lerp(radius * 0.4f, radius * 1.45f, progress), new Color(ring.r, ring.g, ring.b, inverse * 0.26f), ImpactSortingOrder + 2, 30, 0.24f);
        UpdateImpactSparks(progress, center);
    }

    void UpdateImpactSparks(float progress, Vector3 center)
    {
        if (impactSparks == null)
            return;

        float inverse = 1f - progress;
        for (int i = 0; i < impactSparks.Length; i++)
        {
            LineRenderer spark = impactSparks[i];
            if (spark == null)
                continue;

            float angle = ((360f / impactSparks.Length) * i + 18f * Mathf.Sin(i * 3.17f)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.72f + 0.28f).normalized;
            float startDistance = Mathf.Lerp(radius * 0.08f, radius * 0.42f, progress);
            float length = Mathf.Lerp(radius * (0.9f + i * 0.08f), radius * 0.18f, progress);
            Vector3 start = center + (Vector3)(dir * startDistance);
            Vector3 end = start + (Vector3)(dir * length);
            Color sparkColor = Color.Lerp(new Color(1f, 0.92f, 0.68f, inverse * 0.95f), new Color(0.82f, 0.2f, 0.05f, 0f), progress);
            UpdateSimpleLine(spark, start, end, sparkColor, ImpactSortingOrder + 4 + i);
        }
    }

    LineRenderer CreateLine(string lineName, int sortingOrder, float width)
    {
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader)
            {
                name = "RadarStrikeVfxMaterial",
                color = Color.white
            };
            lineMaterial.renderQueue = 5000;
        }

        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.material = lineMaterial;
        line.useWorldSpace = true;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 10;
        line.numCornerVertices = 8;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = sortingOrder;
        line.startWidth = width;
        line.endWidth = width;
        line.positionCount = 0;
        return line;
    }

    void UpdateRing(LineRenderer line, float ringRadius, Color color, int sortingOrder, int segments, float wobble = 0f)
    {
        if (line == null)
            return;

        line.enabled = color.a > 0.001f;
        line.sortingOrder = sortingOrder;
        line.startColor = color;
        line.endColor = color;
        line.positionCount = segments;

        Vector3 center = new Vector3(worldPosition.x, worldPosition.y, mode == VfxMode.Marker ? MarkerZ : ImpactZ);
        for (int i = 0; i < segments; i++)
        {
            float t = (float)i / segments * Mathf.PI * 2f;
            float localRadius = ringRadius;
            if (wobble > 0.0001f)
                localRadius *= 1f + Mathf.Sin(t * 3f + startedAt * 1.7f + Time.time * 9.5f + i * 0.19f) * wobble;

            line.SetPosition(i, center + new Vector3(Mathf.Cos(t) * localRadius, Mathf.Sin(t) * localRadius, 0f));
        }
    }

    void UpdateSimpleLine(LineRenderer line, Vector3 start, Vector3 end, Color color, int sortingOrder)
    {
        if (line == null)
            return;

        line.enabled = color.a > 0.001f;
        line.sortingOrder = sortingOrder;
        line.startColor = color;
        line.endColor = color;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }
}
