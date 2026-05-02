using System.Collections.Generic;
using UnityEngine;

public sealed class AimMarkerVfx : MonoBehaviour
{
    const int SegmentCount = 14;
    const float SegmentFill = 0.6f;
    const float CoreLineWidth = 0.052f;
    const float GlowLineWidth = 0.16f;
    const float ArcCoreLineWidth = 0.072f;
    const float ArcGlowLineWidth = 0.22f;
    const float ArcTargetRingWidth = 0.06f;
    const float ArcTargetCrossWidth = 0.05f;
    const float EffectZOffset = -0.09f;
    const int SortingOrderOffset = 520;
    static readonly Color MarkerCoreColor = new Color(0.78f, 0.96f, 1f, 0.9f);
    static readonly Color MarkerGlowColor = new Color(0.16f, 0.66f, 1f, 0.34f);

    sealed class Segment
    {
        public LineRenderer Glow;
        public LineRenderer Core;
    }

    readonly List<Segment> segments = new List<Segment>();
    Material lineMaterial;
    Transform ownerTransform;
    SpriteRenderer referenceRenderer;
    LineRenderer targetRingGlow;
    LineRenderer targetRingCore;
    LineRenderer targetCrossA;
    LineRenderer targetCrossB;
    int sortingLayerId;
    int sortingOrder = 3000;

    public static AimMarkerVfx EnsureFor(GameObject owner)
    {
        if (owner == null)
            return null;

        AimMarkerVfx marker = owner.GetComponent<AimMarkerVfx>();
        if (marker == null)
            marker = owner.AddComponent<AimMarkerVfx>();

        marker.Configure(owner.transform);
        return marker;
    }

    void Awake()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        lineMaterial = new Material(shader)
        {
            name = "AimMarkerVfxMaterial",
            color = Color.white
        };
        lineMaterial.renderQueue = 3300;
        EnsureSegments();
        Hide();
    }

    void OnDestroy()
    {
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] != null)
            {
                if (segments[i].Glow != null)
                    Destroy(segments[i].Glow.gameObject);
                if (segments[i].Core != null)
                    Destroy(segments[i].Core.gameObject);
            }
        }

        if (targetRingGlow != null)
            Destroy(targetRingGlow.gameObject);
        if (targetRingCore != null)
            Destroy(targetRingCore.gameObject);
        if (targetCrossA != null)
            Destroy(targetCrossA.gameObject);
        if (targetCrossB != null)
            Destroy(targetCrossB.gameObject);

        if (lineMaterial != null)
            Destroy(lineMaterial);
    }

    void Configure(Transform owner)
    {
        ownerTransform = owner;
        referenceRenderer = ownerTransform != null ? ownerTransform.GetComponentInChildren<SpriteRenderer>() : null;
        RefreshSorting();
    }

    public void ShowLine(Vector3 origin, Vector2 direction, float range)
    {
        ShowLine(origin, direction, range, MarkerCoreColor);
    }

    public void ShowLine(Vector3 origin, Vector2 direction, float range, Color markerColor)
    {
        if (direction.sqrMagnitude < 0.001f || range <= 0f)
        {
            Hide();
            return;
        }

        EnsureSegments();
        EnsureArcTargetMarker();
        RefreshSorting();
        Vector3 dir = new Vector3(direction.normalized.x, direction.normalized.y, 0f);
        float usableRange = Mathf.Max(0.5f, range);
        float step = usableRange / SegmentCount;
        float segmentLength = step * SegmentFill;
        Vector3 startOffset = dir * ResolveStartOffset();
        origin.z += EffectZOffset;

        for (int i = 0; i < segments.Count; i++)
        {
            Segment segment = segments[i];
            if (segment == null)
                continue;

            Vector3 start = origin + startOffset + dir * (i * step);
            Vector3 end = start + dir * segmentLength;
            float pulse = Mathf.Sin(Time.time * 10f + i * 0.52f) * 0.5f + 0.5f;
            Color resolvedCore = Color.Lerp(markerColor, Color.white, 0.32f);
            Color resolvedGlow = Color.Lerp(markerColor, MarkerGlowColor, 0.22f);
            Color coreStart = new Color(resolvedCore.r, resolvedCore.g, resolvedCore.b, Mathf.Max(0.2f, markerColor.a) * 0.9f);
            Color coreEnd = new Color(resolvedCore.r, resolvedCore.g, resolvedCore.b, Mathf.Lerp(0.28f, 0.58f, pulse) * Mathf.Max(0.2f, markerColor.a));
            Color glowStart = new Color(resolvedGlow.r, resolvedGlow.g, resolvedGlow.b, Mathf.Lerp(0.22f, 0.42f, pulse) * Mathf.Max(0.2f, markerColor.a));
            Color glowEnd = new Color(resolvedGlow.r, resolvedGlow.g, resolvedGlow.b, Mathf.Lerp(0.08f, 0.18f, pulse) * Mathf.Max(0.2f, markerColor.a));

            UpdateLine(segment.Glow, start, end, glowStart, glowEnd, GlowLineWidth, sortingOrder);
            UpdateLine(segment.Core, start, end, coreStart, coreEnd, CoreLineWidth, sortingOrder + 1);
        }
    }

    public void ShowArc(Vector3 origin, Vector3 target, Color markerColor, float arcHeight)
    {
        Vector2 delta = target - origin;
        if (delta.sqrMagnitude <= 0.001f)
        {
            Hide();
            return;
        }

        EnsureSegments();
        RefreshSorting();

        Vector3 startOffset = ((Vector3)delta.normalized) * ResolveStartOffset();
        Vector3 startPoint = origin + startOffset;
        Vector3 endPoint = target;
        Vector2 tangent = (endPoint - startPoint);
        Vector2 normal = tangent.sqrMagnitude > 0.001f ? new Vector2(-tangent.y, tangent.x).normalized : Vector2.up;
        origin.z += EffectZOffset;

        for (int i = 0; i < segments.Count; i++)
        {
            Segment segment = segments[i];
            if (segment == null)
                continue;

            float t0 = i / (float)segments.Count;
            float t1 = (i + SegmentFill) / segments.Count;
            Vector3 segmentStart = EvaluateArcPoint(startPoint, endPoint, normal, arcHeight, t0);
            Vector3 segmentEnd = EvaluateArcPoint(startPoint, endPoint, normal, arcHeight, Mathf.Clamp01(t1));
            segmentStart.z = origin.z;
            segmentEnd.z = origin.z;

            float pulse = Mathf.Sin(Time.time * 8.5f + i * 0.46f) * 0.5f + 0.5f;
            Color resolvedCore = Color.Lerp(markerColor, Color.white, 0.28f);
            Color resolvedGlow = Color.Lerp(markerColor, MarkerGlowColor, 0.18f);
            Color coreStart = new Color(resolvedCore.r, resolvedCore.g, resolvedCore.b, Mathf.Max(0.2f, markerColor.a) * 0.92f);
            Color coreEnd = new Color(resolvedCore.r, resolvedCore.g, resolvedCore.b, Mathf.Lerp(0.24f, 0.52f, pulse) * Mathf.Max(0.2f, markerColor.a));
            Color glowStart = new Color(resolvedGlow.r, resolvedGlow.g, resolvedGlow.b, Mathf.Lerp(0.2f, 0.38f, pulse) * Mathf.Max(0.2f, markerColor.a));
            Color glowEnd = new Color(resolvedGlow.r, resolvedGlow.g, resolvedGlow.b, Mathf.Lerp(0.08f, 0.16f, pulse) * Mathf.Max(0.2f, markerColor.a));

            UpdateLine(segment.Glow, segmentStart, segmentEnd, glowStart, glowEnd, ArcGlowLineWidth, sortingOrder);
            UpdateLine(segment.Core, segmentStart, segmentEnd, coreStart, coreEnd, ArcCoreLineWidth, sortingOrder + 1);
        }

        UpdateArcTargetMarker(endPoint, markerColor);
    }

    public void Hide()
    {
        EnsureSegments();
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] == null)
                continue;

            if (segments[i].Glow != null)
                segments[i].Glow.enabled = false;
            if (segments[i].Core != null)
                segments[i].Core.enabled = false;
        }

        if (targetRingGlow != null)
            targetRingGlow.enabled = false;
        if (targetRingCore != null)
            targetRingCore.enabled = false;
        if (targetCrossA != null)
            targetCrossA.enabled = false;
        if (targetCrossB != null)
            targetCrossB.enabled = false;
    }

    void EnsureSegments()
    {
        while (segments.Count < SegmentCount)
        {
            int index = segments.Count;
            Segment segment = new Segment
            {
                Glow = CreateLine("AimMarkerGlow_" + index, GlowLineWidth, sortingOrder),
                Core = CreateLine("AimMarkerCore_" + index, CoreLineWidth, sortingOrder + 1)
            };
            segments.Add(segment);
        }
    }

    void EnsureArcTargetMarker()
    {
        if (targetRingGlow == null)
            targetRingGlow = CreateLine("AimMarkerTargetGlow", ArcTargetRingWidth * 1.9f, sortingOrder);
        if (targetRingCore == null)
            targetRingCore = CreateLine("AimMarkerTargetCore", ArcTargetRingWidth, sortingOrder + 1);
        if (targetCrossA == null)
            targetCrossA = CreateLine("AimMarkerTargetCrossA", ArcTargetCrossWidth, sortingOrder + 2);
        if (targetCrossB == null)
            targetCrossB = CreateLine("AimMarkerTargetCrossB", ArcTargetCrossWidth, sortingOrder + 2);

        targetRingGlow.loop = true;
        targetRingGlow.positionCount = 24;
        targetRingCore.loop = true;
        targetRingCore.positionCount = 24;
    }

    LineRenderer CreateLine(string objectName, float width, int order)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        if (ownerTransform != null)
            lineObject.layer = ownerTransform.gameObject.layer;

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = width;
        line.startWidth = width;
        line.endWidth = width * 0.72f;
        line.numCapVertices = 8;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = lineMaterial;
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = order;
        line.enabled = false;
        return line;
    }

    void UpdateLine(LineRenderer line, Vector3 start, Vector3 end, Color startColor, Color endColor, float width, int order)
    {
        if (line == null)
            return;

        line.enabled = true;
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = order;
        line.widthMultiplier = width;
        line.startColor = startColor;
        line.endColor = endColor;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    void RefreshSorting()
    {
        if (referenceRenderer == null && ownerTransform != null)
            referenceRenderer = ownerTransform.GetComponentInChildren<SpriteRenderer>();

        if (referenceRenderer != null)
        {
            sortingLayerId = referenceRenderer.sortingLayerID;
            sortingOrder = referenceRenderer.sortingOrder + SortingOrderOffset;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            Segment segment = segments[i];
            if (segment == null)
                continue;

            if (segment.Glow != null)
            {
                segment.Glow.sortingLayerID = sortingLayerId;
                segment.Glow.sortingOrder = sortingOrder;
            }

            if (segment.Core != null)
            {
                segment.Core.sortingLayerID = sortingLayerId;
                segment.Core.sortingOrder = sortingOrder + 1;
            }
        }

        RefreshAuxLine(targetRingGlow, sortingOrder);
        RefreshAuxLine(targetRingCore, sortingOrder + 1);
        RefreshAuxLine(targetCrossA, sortingOrder + 2);
        RefreshAuxLine(targetCrossB, sortingOrder + 2);
    }

    float ResolveStartOffset()
    {
        if (referenceRenderer != null)
            return Mathf.Max(0.42f, Mathf.Max(referenceRenderer.bounds.extents.x, referenceRenderer.bounds.extents.y) * 0.72f);

        return 0.62f;
    }

    static Vector3 EvaluateArcPoint(Vector3 startPoint, Vector3 endPoint, Vector2 normal, float arcHeight, float t)
    {
        Vector3 linear = Vector3.Lerp(startPoint, endPoint, Mathf.Clamp01(t));
        float arc = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * Mathf.Max(0.2f, arcHeight);
        return linear + (Vector3)(normal * arc);
    }

    void UpdateArcTargetMarker(Vector3 endPoint, Color markerColor)
    {
        EnsureArcTargetMarker();

        float pulse = Mathf.Sin(Time.time * 9.5f) * 0.5f + 0.5f;
        float ringRadius = 0.34f + pulse * 0.08f;
        Color glowColor = new Color(markerColor.r, markerColor.g, markerColor.b, 0.34f);
        Color coreColor = new Color(Mathf.Lerp(markerColor.r, 1f, 0.25f), Mathf.Lerp(markerColor.g, 1f, 0.25f), Mathf.Lerp(markerColor.b, 1f, 0.25f), 0.92f);

        UpdateRing(targetRingGlow, endPoint, ringRadius * 1.14f, glowColor, ArcTargetRingWidth * 1.9f, sortingOrder);
        UpdateRing(targetRingCore, endPoint, ringRadius, coreColor, ArcTargetRingWidth, sortingOrder + 1);

        Vector3 diagA = new Vector3(0.22f, 0.22f, 0f);
        Vector3 diagB = new Vector3(-0.22f, 0.22f, 0f);
        UpdateLine(targetCrossA, endPoint - diagA, endPoint + diagA, coreColor, new Color(coreColor.r, coreColor.g, coreColor.b, 0.58f), ArcTargetCrossWidth, sortingOrder + 2);
        UpdateLine(targetCrossB, endPoint - diagB, endPoint + diagB, coreColor, new Color(coreColor.r, coreColor.g, coreColor.b, 0.58f), ArcTargetCrossWidth, sortingOrder + 2);
    }

    void UpdateRing(LineRenderer line, Vector3 center, float radius, Color color, float width, int order)
    {
        if (line == null)
            return;

        line.enabled = true;
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = order;
        line.widthMultiplier = width;
        line.startColor = color;
        line.endColor = color;
        line.loop = true;
        line.positionCount = Mathf.Max(16, line.positionCount);

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = (i / (float)line.positionCount) * Mathf.PI * 2f;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, EffectZOffset));
        }
    }

    void RefreshAuxLine(LineRenderer line, int order)
    {
        if (line == null)
            return;

        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = order;
    }
}
