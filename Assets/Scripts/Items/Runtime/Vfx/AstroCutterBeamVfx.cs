using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class AstroCutterBeamVfx : MonoBehaviour
{
    sealed class BeamEntry
    {
        public AstroCutterBeamVfx Beam;
    }

    static readonly Dictionary<int, BeamEntry> ActiveBeams = new Dictionary<int, BeamEntry>();

    Transform source;
    int sourceViewId;
    Vector2 direction;
    float range;
    bool fullWidth;
    float endTime;
    LineRenderer coreLine;
    LineRenderer glowLine;
    LineRenderer[] burnSparkLines;
    AudioSource loopAudio;
    readonly RaycastHit2D[] clippedRangeHits = new RaycastHit2D[96];

    public static void StartBeam(int sourceViewId, Vector2 beamDirection, float beamRange, float duration, bool useFullWidth)
    {
        StopBeam(sourceViewId);

        PhotonView sourceView = PhotonView.Find(sourceViewId);
        if (sourceView == null)
            return;

        GameObject beamObject = new GameObject("AstroCutterBeam_" + sourceViewId);
        AstroCutterBeamVfx beam = beamObject.AddComponent<AstroCutterBeamVfx>();
        beam.Initialize(sourceView.transform, sourceViewId, beamDirection, beamRange, duration, useFullWidth);
        ActiveBeams[sourceViewId] = new BeamEntry { Beam = beam };
    }

    public static void ResetForSessionTransition()
    {
        List<BeamEntry> active = new List<BeamEntry>(ActiveBeams.Values);
        ActiveBeams.Clear();
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i]?.Beam != null)
                Destroy(active[i].Beam.gameObject);
        }
    }

    public static void StopBeam(int sourceViewId)
    {
        if (!ActiveBeams.TryGetValue(sourceViewId, out BeamEntry entry))
            return;

        if (entry?.Beam != null)
            Destroy(entry.Beam.gameObject);

        ActiveBeams.Remove(sourceViewId);
    }

    void Initialize(Transform sourceTransform, int sourcePhotonViewId, Vector2 beamDirection, float beamRange, float duration, bool useFullWidth)
    {
        source = sourceTransform;
        sourceViewId = sourcePhotonViewId;
        direction = beamDirection.sqrMagnitude > 0.001f ? beamDirection.normalized : Vector2.up;
        range = Mathf.Max(0.2f, beamRange);
        fullWidth = useFullWidth;
        endTime = Time.time + Mathf.Max(0.1f, duration);
        float widthScale = fullWidth ? 1f : 0.5f;
        coreLine = CreateLine("Core", 0.09f * widthScale, 74);
        glowLine = CreateLine("Glow", 0.28f * widthScale, 73);
        CreateBurnSparks();
        ConfigureAudio();
        UpdateLine();
    }

    void Update()
    {
        if (source == null || Time.time >= endTime)
        {
            Destroy(gameObject);
            return;
        }

        UpdateLine();
    }

    void OnDestroy()
    {
        if (loopAudio != null)
            loopAudio.Stop();

        foreach (KeyValuePair<int, BeamEntry> pair in new List<KeyValuePair<int, BeamEntry>>(ActiveBeams))
        {
            if (pair.Value?.Beam == this)
                ActiveBeams.Remove(pair.Key);
        }
    }

    LineRenderer CreateLine(string objectName, float width, int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.positionCount = 14;
        line.widthMultiplier = width;
        line.numCapVertices = 10;
        line.numCornerVertices = 8;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.textureMode = LineTextureMode.Stretch;
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = sortingOrder;
        return line;
    }

    void UpdateLine()
    {
        Vector2 start = source.position + source.up * 0.58f;
        Vector2 safeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : (Vector2)source.up;
        float visibleRange = ResolveObstacleClippedRange(start, safeDirection, range);
        Vector2 perpendicular = new Vector2(-safeDirection.y, safeDirection.x);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
        float widthScale = fullWidth ? 1f : 0.5f;
        float jitterScale = fullWidth ? 1f : 0.5f;
        Gradient coreGradient = BuildGradient(new Color(0.95f, 0.72f, 1f, 0.95f), new Color(1f, 0.82f, 0.18f, 0.9f), 0.95f);
        Gradient glowGradient = BuildGradient(new Color(0.5f, 0.12f, 1f, 0.34f), new Color(1f, 0.74f, 0.05f, 0.28f), 0.55f + pulse * 0.25f);
        if (coreLine != null)
        {
            coreLine.colorGradient = coreGradient;
            coreLine.widthMultiplier = Mathf.Lerp(0.075f, 0.13f, pulse) * widthScale;
            SetLinePositions(coreLine, start, safeDirection, perpendicular, visibleRange, 0.045f * jitterScale);
        }

        if (glowLine != null)
        {
            glowLine.colorGradient = glowGradient;
            glowLine.widthMultiplier = Mathf.Lerp(0.22f, 0.34f, pulse) * widthScale;
            SetLinePositions(glowLine, start, safeDirection, perpendicular, visibleRange, 0.09f * jitterScale);
        }

        bool blocked = visibleRange < range - 0.08f;
        UpdateBurnSparks(blocked, start + safeDirection * visibleRange, safeDirection, perpendicular, pulse, jitterScale);

        if (loopAudio != null)
            loopAudio.transform.position = start;
    }

    float ResolveObstacleClippedRange(Vector2 start, Vector2 safeDirection, float maxRange)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };
        int hitCount = Physics2D.CircleCast(start, fullWidth ? 0.28f : 0.14f, safeDirection, filter, clippedRangeHits, Mathf.Max(0.2f, maxRange));
        return AstroCutterBeamBlocker.ResolveClippedRange(clippedRangeHits, hitCount, source, sourceViewId, maxRange);
    }

    void SetLinePositions(LineRenderer line, Vector2 start, Vector2 safeDirection, Vector2 perpendicular, float visibleRange, float jitterScale)
    {
        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (float)(line.positionCount - 1);
            float wave = Mathf.Sin(Time.time * 17f + t * Mathf.PI * 7f) * jitterScale;
            Vector2 point = start + safeDirection * (visibleRange * t) + perpendicular * wave;
            line.SetPosition(i, new Vector3(point.x, point.y, -0.42f));
        }
    }

    void CreateBurnSparks()
    {
        burnSparkLines = new LineRenderer[7];
        for (int i = 0; i < burnSparkLines.Length; i++)
        {
            GameObject sparkObject = new GameObject("BurnSpark_" + i);
            sparkObject.transform.SetParent(transform, false);
            LineRenderer spark = sparkObject.AddComponent<LineRenderer>();
            spark.useWorldSpace = true;
            spark.alignment = LineAlignment.View;
            spark.positionCount = 2;
            spark.widthMultiplier = 0.025f;
            spark.numCapVertices = 3;
            spark.numCornerVertices = 1;
            spark.material = new Material(Shader.Find("Sprites/Default"));
            spark.textureMode = LineTextureMode.Stretch;
            spark.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            spark.sortingOrder = 77 + i;
            spark.enabled = false;
            burnSparkLines[i] = spark;
        }
    }

    void UpdateBurnSparks(bool active, Vector2 endpoint, Vector2 safeDirection, Vector2 perpendicular, float pulse, float jitterScale)
    {
        if (burnSparkLines == null)
            return;

        float widthScale = fullWidth ? 1.2f : 0.85f;
        float time = Time.time;
        for (int i = 0; i < burnSparkLines.Length; i++)
        {
            LineRenderer spark = burnSparkLines[i];
            if (spark == null)
                continue;

            spark.enabled = active;
            if (!active)
                continue;

            float phase = time * (22f + i * 1.7f) + i * 1.37f;
            float flicker = 0.5f + 0.5f * Mathf.Sin(phase);
            float sideSign = i % 2 == 0 ? 1f : -1f;
            float sideAmount = sideSign * Mathf.Lerp(0.03f, 0.18f, (i % 4) / 3f) * jitterScale;
            Vector2 sparkDirection = (-safeDirection * Mathf.Lerp(0.32f, 0.78f, (i % 3) / 2f) + perpendicular * sideSign * Mathf.Lerp(0.25f, 0.75f, flicker)).normalized;
            Vector2 start = endpoint - safeDirection * 0.025f + perpendicular * sideAmount * 0.35f;
            Vector2 end = start + sparkDirection * Mathf.Lerp(0.08f, 0.32f, flicker) * widthScale;
            spark.SetPosition(0, new Vector3(start.x, start.y, -0.46f));
            spark.SetPosition(1, new Vector3(end.x, end.y, -0.46f));
            spark.widthMultiplier = Mathf.Lerp(0.012f, 0.045f, flicker) * widthScale;
            spark.startColor = i % 3 == 0
                ? new Color(1f, 0.96f, 0.48f, 0.94f)
                : new Color(1f, 0.52f, 0.08f, 0.78f);
            spark.endColor = i % 3 == 1
                ? new Color(0.62f, 0.12f, 1f, 0.02f)
                : new Color(1f, 0.12f, 0.03f, 0.02f);
        }
    }

    Gradient BuildGradient(Color a, Color b, float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(a, 0f),
                new GradientColorKey(b, 0.52f),
                new GradientColorKey(a, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(alpha, 0.08f),
                new GradientAlphaKey(alpha, 0.88f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    void ConfigureAudio()
    {
        AudioClip clip = AudioManager.Instance != null ? AudioManager.Instance.AstroCutterClip : null;
        if (clip == null)
            return;

        loopAudio = gameObject.AddComponent<AudioSource>();
        AudioManager.Instance.ConfigureSpatialSource(loopAudio, 0.62f);
        loopAudio.clip = clip;
        loopAudio.loop = true;
        loopAudio.Play();
    }
}
