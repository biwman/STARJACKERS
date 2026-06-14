using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public sealed class HackingBeamVfx : MonoBehaviour
{
    const int DashCount = 12;
    const float EffectZOffset = -0.1f;
    const int SortingOrderOffset = 430;

    static readonly Dictionary<int, HackingBeamVfx> ActiveBySourceViewId = new Dictionary<int, HackingBeamVfx>();
    static Material sharedMaterial;

    readonly LineRenderer[] glowSegments = new LineRenderer[DashCount];
    readonly LineRenderer[] coreSegments = new LineRenderer[DashCount];
    Transform source;
    Transform target;
    int sourceViewId;
    int targetViewId;
    int sortingLayerId;
    int sortingOrder = 2700;
    float startedAt;
    float autoStopAt;

    public static void Prewarm()
    {
        GetMaterial();
    }

    public static void StartBeam(int sourcePhotonViewId, int targetPhotonViewId, float expectedDuration)
    {
        StopBeam(sourcePhotonViewId);

        PhotonView sourceView = PhotonView.Find(sourcePhotonViewId);
        PhotonView targetView = PhotonView.Find(targetPhotonViewId);
        if (sourceView == null || targetView == null)
            return;

        GameObject effect = new GameObject("HackingBeamVfx_" + sourcePhotonViewId);
        HackingBeamVfx vfx = effect.AddComponent<HackingBeamVfx>();
        vfx.Initialize(sourceView.transform, targetView.transform, sourcePhotonViewId, targetPhotonViewId, expectedDuration);
        ActiveBySourceViewId[sourcePhotonViewId] = vfx;
    }

    public static void StopBeam(int sourcePhotonViewId)
    {
        if (!ActiveBySourceViewId.TryGetValue(sourcePhotonViewId, out HackingBeamVfx vfx))
            return;

        ActiveBySourceViewId.Remove(sourcePhotonViewId);
        if (vfx != null)
            Destroy(vfx.gameObject);
    }

    public static void ResetForSessionTransition()
    {
        List<HackingBeamVfx> active = new List<HackingBeamVfx>(ActiveBySourceViewId.Values);
        ActiveBySourceViewId.Clear();
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i] != null)
                Destroy(active[i].gameObject);
        }
    }

    void Initialize(Transform sourceTransform, Transform targetTransform, int resolvedSourceViewId, int resolvedTargetViewId, float expectedDuration)
    {
        source = sourceTransform;
        target = targetTransform;
        sourceViewId = resolvedSourceViewId;
        targetViewId = resolvedTargetViewId;
        startedAt = Time.time;
        autoStopAt = Time.time + Mathf.Max(0.3f, expectedDuration) + 0.5f;

        SpriteRenderer sourceRenderer = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
        if (sourceRenderer != null)
        {
            sortingLayerId = sourceRenderer.sortingLayerID;
            sortingOrder = sourceRenderer.sortingOrder + SortingOrderOffset;
        }

        if (source != null)
            gameObject.layer = source.gameObject.layer;

        for (int i = 0; i < DashCount; i++)
        {
            glowSegments[i] = CreateSegment("HackingBeamGlow_" + i, 0.22f, sortingOrder);
            coreSegments[i] = CreateSegment("HackingBeamCore_" + i, 0.07f, sortingOrder + 1);
        }
    }

    void Update()
    {
        if (source == null || target == null || Time.time >= autoStopAt)
        {
            StopBeam(sourceViewId);
            return;
        }

        UpdateBeam();
    }

    void OnDestroy()
    {
        if (ActiveBySourceViewId.TryGetValue(sourceViewId, out HackingBeamVfx active) && active == this)
            ActiveBySourceViewId.Remove(sourceViewId);
    }

    void UpdateBeam()
    {
        Vector3 start = GetSourcePoint();
        Vector3 end = GetTargetPoint(start);
        Vector3 delta = end - start;
        float distance = Mathf.Max(0.1f, delta.magnitude);
        Vector3 zOffset = new Vector3(0f, 0f, EffectZOffset);
        float age = Time.time - startedAt;
        float scroll = Mathf.Repeat(age * 0.86f, 1f);
        float pulse = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(Time.time * 24f + targetViewId * 0.11f));

        for (int i = 0; i < DashCount; i++)
        {
            float dashPhase = Mathf.Repeat((i / (float)DashCount) + scroll, 1f);
            float dashLength = Mathf.Lerp(0.045f, 0.105f, Mathf.PingPong(age * 3.8f + i * 0.13f, 1f));
            float t0 = dashPhase;
            float t1 = Mathf.Min(1f, t0 + dashLength);
            bool enabled = t0 < 0.96f && Mathf.Repeat(Time.time * 17f + i * 0.41f, 1f) > 0.13f;

            Vector3 a = Vector3.Lerp(start, end, t0) + zOffset;
            Vector3 b = Vector3.Lerp(start, end, t1) + zOffset;
            float distanceFade = Mathf.Clamp01(distance / 7.5f);
            float alpha = enabled ? Mathf.Lerp(0.45f, 0.95f, pulse) * Mathf.Lerp(1f, 0.72f, distanceFade) : 0f;
            Color core = i % 3 == 0
                ? new Color(1f, 0.12f, 0.72f, alpha)
                : new Color(0.12f, 0.92f, 1f, alpha);
            Color glow = new Color(core.r, core.g, core.b, alpha * 0.38f);

            UpdateSegment(glowSegments[i], a, b, glow, enabled, Mathf.Lerp(0.16f, 0.28f, pulse));
            UpdateSegment(coreSegments[i], a, b, core, enabled, Mathf.Lerp(0.045f, 0.09f, pulse));
        }
    }

    Vector3 GetSourcePoint()
    {
        if (source == null)
            return Vector3.zero;

        float forwardOffset = 0.52f;
        SpriteRenderer renderer = source.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
            forwardOffset = Mathf.Max(0.38f, Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y) * 0.72f);

        return source.position + source.up * forwardOffset;
    }

    Vector3 GetTargetPoint(Vector3 sourcePoint)
    {
        if (target == null)
            return sourcePoint;

        Collider2D collider = target.GetComponent<Collider2D>();
        if (collider != null)
            return Vector3.Lerp(target.position, collider.ClosestPoint(sourcePoint), 0.86f);

        return target.position;
    }

    LineRenderer CreateSegment(string objectName, float width, int order)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        if (source != null)
            lineObject.layer = source.gameObject.layer;

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = order;
        return line;
    }

    static void UpdateSegment(LineRenderer line, Vector3 start, Vector3 end, Color color, bool enabled, float width)
    {
        if (line == null)
            return;

        line.enabled = enabled;
        if (!enabled)
            return;

        line.widthMultiplier = width;
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, color.a * 0.72f);
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    static Material GetMaterial()
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        sharedMaterial = new Material(shader)
        {
            name = "HackingBeamVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3450;
        return sharedMaterial;
    }
}
