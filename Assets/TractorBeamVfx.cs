using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public sealed class TractorBeamVfx : MonoBehaviour
{
    const int BeamPointCount = 15;
    const float EffectZOffset = -0.07f;
    const int SortingOrderOffset = 280;

    static readonly Dictionary<int, TractorBeamVfx> ActiveBySourceViewId = new Dictionary<int, TractorBeamVfx>();
    static Material sharedMaterial;
    static AudioClip tractorBeamClip;

    Transform source;
    Transform target;
    LineRenderer coreLine;
    LineRenderer glowLine;
    AudioSource audioSource;
    int sourceViewId;
    int targetViewId;
    int sortingLayerId;
    int sortingOrder = 2400;
    Vector2 sourceLocalAnchorDirection = Vector2.up;
    float sourceAnchorDistanceFactor = 1f;
    float targetAnchorDistanceFactor = 1f;

    public static void Prewarm()
    {
        GetMaterial();
        GetTractorBeamClip();
    }

    public static void StartBeam(int sourcePhotonViewId, int targetPhotonViewId)
    {
        StartBeam(sourcePhotonViewId, targetPhotonViewId, Vector2.up);
    }

    public static void StartBeam(int sourcePhotonViewId, int targetPhotonViewId, Vector2 sourceLocalDirection)
    {
        StartBeam(sourcePhotonViewId, targetPhotonViewId, sourceLocalDirection, 1f);
    }

    public static void StartBeam(int sourcePhotonViewId, int targetPhotonViewId, Vector2 sourceLocalDirection, float sourceAnchorDistance)
    {
        StartBeam(sourcePhotonViewId, targetPhotonViewId, sourceLocalDirection, sourceAnchorDistance, 1f);
    }

    public static void StartBeam(int sourcePhotonViewId, int targetPhotonViewId, Vector2 sourceLocalDirection, float sourceAnchorDistance, float targetAnchorDistance)
    {
        StopBeam(sourcePhotonViewId);

        PhotonView sourceView = PhotonView.Find(sourcePhotonViewId);
        PhotonView targetView = PhotonView.Find(targetPhotonViewId);
        if (sourceView == null || targetView == null)
            return;

        GameObject effect = new GameObject("TractorBeamVfx_" + sourcePhotonViewId);
        TractorBeamVfx vfx = effect.AddComponent<TractorBeamVfx>();
        vfx.Initialize(sourceView.transform, targetView.transform, sourcePhotonViewId, targetPhotonViewId, sourceLocalDirection, sourceAnchorDistance, targetAnchorDistance);
        ActiveBySourceViewId[sourcePhotonViewId] = vfx;
    }

    public static void ResetForSessionTransition()
    {
        List<TractorBeamVfx> active = new List<TractorBeamVfx>(ActiveBySourceViewId.Values);
        ActiveBySourceViewId.Clear();
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i] != null)
                Destroy(active[i].gameObject);
        }
    }

    public static void StopBeam(int sourcePhotonViewId)
    {
        if (!ActiveBySourceViewId.TryGetValue(sourcePhotonViewId, out TractorBeamVfx vfx))
            return;

        ActiveBySourceViewId.Remove(sourcePhotonViewId);
        if (vfx != null)
            Destroy(vfx.gameObject);
    }

    void Initialize(Transform sourceTransform, Transform targetTransform, int resolvedSourceViewId, int resolvedTargetViewId, Vector2 localAnchorDirection, float anchorDistanceFactor, float targetAnchorFactor)
    {
        source = sourceTransform;
        target = targetTransform;
        sourceViewId = resolvedSourceViewId;
        targetViewId = resolvedTargetViewId;
        sourceLocalAnchorDirection = localAnchorDirection.sqrMagnitude > 0.001f ? localAnchorDirection.normalized : Vector2.up;
        sourceAnchorDistanceFactor = Mathf.Clamp01(anchorDistanceFactor);
        targetAnchorDistanceFactor = Mathf.Clamp01(targetAnchorFactor);

        SpriteRenderer sourceRenderer = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
        if (sourceRenderer != null)
        {
            sortingLayerId = sourceRenderer.sortingLayerID;
            sortingOrder = sourceRenderer.sortingOrder + SortingOrderOffset;
        }

        if (source != null)
            gameObject.layer = source.gameObject.layer;

        glowLine = CreateLine("TractorBeamGlow", 0.38f, sortingOrder);
        coreLine = CreateLine("TractorBeamCore", 0.12f, sortingOrder + 1);
        CreateAudioSource();
    }

    void Update()
    {
        if (source == null || target == null)
        {
            StopBeam(sourceViewId);
            return;
        }

        UpdateBeam();
        UpdateAudio();
    }

    void OnDestroy()
    {
        if (ActiveBySourceViewId.TryGetValue(sourceViewId, out TractorBeamVfx active) && active == this)
            ActiveBySourceViewId.Remove(sourceViewId);

        if (audioSource != null)
            audioSource.Stop();
    }

    void UpdateBeam()
    {
        Vector3 start = GetSourcePoint();
        Vector3 end = GetTargetPoint(start);
        Vector3 delta = end - start;
        Vector3 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : source.up;
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.forward);
        float distance = Mathf.Max(0.1f, delta.magnitude);
        float pulse = Mathf.Sin((Time.time * 16f) + targetViewId * 0.07f) * 0.5f + 0.5f;
        float wave = Mathf.Lerp(0.05f, 0.18f, pulse) * Mathf.Clamp01(distance / 8f);

        UpdateLine(glowLine, start, end, perpendicular, wave, pulse, false);
        UpdateLine(coreLine, start, end, perpendicular, wave * 0.42f, pulse, true);
    }

    void UpdateLine(LineRenderer line, Vector3 start, Vector3 end, Vector3 perpendicular, float wave, float pulse, bool core)
    {
        if (line == null)
            return;

        line.enabled = true;
        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (float)(line.positionCount - 1);
            Vector3 point = Vector3.Lerp(start, end, t);
            float taper = Mathf.Sin(t * Mathf.PI);
            float ripple = Mathf.Sin((t * Mathf.PI * 7f) + Time.time * 18f) * wave * taper;
            float counter = Mathf.Sin((t * Mathf.PI * 13f) - Time.time * 11f) * wave * 0.35f * taper;
            point += perpendicular * (ripple + counter);
            point.z = source.position.z + EffectZOffset;
            line.SetPosition(i, point);
        }

        float alpha = core ? Mathf.Lerp(0.68f, 1f, pulse) : Mathf.Lerp(0.28f, 0.58f, pulse);
        line.colorGradient = core ? BuildCoreGradient(alpha) : BuildGlowGradient(alpha);
        line.widthMultiplier = core
            ? Mathf.Lerp(0.1f, 0.18f, pulse)
            : Mathf.Lerp(0.28f, 0.46f, pulse);
    }

    Vector3 GetSourcePoint()
    {
        if (sourceAnchorDistanceFactor <= 0.001f)
            return source != null ? source.position : Vector3.zero;

        float forwardOffset = 0.55f;
        SpriteRenderer renderer = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
        if (renderer != null)
            forwardOffset = Mathf.Max(0.4f, Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y) * 0.9f);
        forwardOffset *= sourceAnchorDistanceFactor;

        Vector3 anchorDirection = source != null
            ? source.TransformDirection(new Vector3(sourceLocalAnchorDirection.x, sourceLocalAnchorDirection.y, 0f)).normalized
            : Vector3.up;
        if (anchorDirection.sqrMagnitude < 0.001f)
            anchorDirection = source != null ? source.up : Vector3.up;

        return source.position + anchorDirection * forwardOffset;
    }

    Vector3 GetTargetPoint(Vector3 sourcePoint)
    {
        if (targetAnchorDistanceFactor <= 0.001f)
            return target != null ? target.position : sourcePoint;

        Collider2D collider = target != null ? target.GetComponent<Collider2D>() : null;
        if (collider != null)
        {
            Vector3 edgePoint = collider.ClosestPoint(sourcePoint);
            Vector3 centerPoint = target != null ? target.position : sourcePoint;
            return Vector3.Lerp(centerPoint, edgePoint, targetAnchorDistanceFactor);
        }

        return target != null ? target.position : sourcePoint;
    }

    LineRenderer CreateLine(string objectName, float width, int order)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        if (source != null)
            lineObject.layer = source.gameObject.layer;

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = BeamPointCount;
        line.widthMultiplier = width;
        line.numCapVertices = 14;
        line.numCornerVertices = 12;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = order;
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.25f),
            new Keyframe(0.18f, 1f),
            new Keyframe(0.72f, 0.72f),
            new Keyframe(1f, 0.22f));
        return line;
    }

    void CreateAudioSource()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = GetTractorBeamClip();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 0.78f;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 4f;
        audioSource.maxDistance = 24f;

        if (audioSource.clip != null)
            audioSource.Play();
    }

    void UpdateAudio()
    {
        transform.position = source != null ? source.position : transform.position;
        if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
            audioSource.Play();
    }

    static Gradient BuildCoreGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.78f), 0f),
                new GradientColorKey(new Color(1f, 0.86f, 0.2f), 0.48f),
                new GradientColorKey(new Color(1f, 0.56f, 0.04f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha, 0f),
                new GradientAlphaKey(alpha * 0.86f, 0.52f),
                new GradientAlphaKey(alpha * 0.38f, 1f)
            });
        return gradient;
    }

    static Gradient BuildGlowGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.34f), 0f),
                new GradientColorKey(new Color(1f, 0.64f, 0.08f), 0.55f),
                new GradientColorKey(new Color(1f, 0.38f, 0.02f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha * 0.72f, 0f),
                new GradientAlphaKey(alpha * 0.55f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    static AudioClip GetTractorBeamClip()
    {
        if (tractorBeamClip != null)
            return tractorBeamClip;

        tractorBeamClip = Resources.Load<AudioClip>("Audio/tractor_beam_sound");
        return tractorBeamClip;
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
            name = "TractorBeamVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3350;
        return sharedMaterial;
    }
}
