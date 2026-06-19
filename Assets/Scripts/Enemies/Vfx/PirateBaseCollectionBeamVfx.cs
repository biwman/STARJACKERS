using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public sealed class PirateBaseCollectionBeamVfx : MonoBehaviour
{
    const int BeamPointCount = 18;
    const float EffectZOffset = -0.078f;
    const int SortingOrderOffset = 284;

    static readonly Dictionary<int, PirateBaseCollectionBeamVfx> ActiveBySourceViewId = new Dictionary<int, PirateBaseCollectionBeamVfx>();
    static Material sharedMaterial;

    Transform source;
    Transform target;
    LineRenderer coreLine;
    LineRenderer glowLine;
    AudioSource audioSource;
    int sourceViewId;
    int sortingLayerId;
    int sortingOrder = 2400;

    public static void Prewarm()
    {
        GetMaterial();
    }

    public static void StartBeam(int sourcePhotonViewId, int targetPhotonViewId)
    {
        StopBeam(sourcePhotonViewId);

        PhotonView sourceView = PhotonView.Find(sourcePhotonViewId);
        PhotonView targetView = PhotonView.Find(targetPhotonViewId);
        if (sourceView == null || targetView == null)
            return;

        GameObject effect = new GameObject("PirateBaseCollectionBeamVfx_" + sourcePhotonViewId);
        PirateBaseCollectionBeamVfx vfx = effect.AddComponent<PirateBaseCollectionBeamVfx>();
        vfx.Initialize(sourceView.transform, targetView.transform, sourcePhotonViewId);
        ActiveBySourceViewId[sourcePhotonViewId] = vfx;
    }

    public static void StopBeam(int sourcePhotonViewId)
    {
        if (!ActiveBySourceViewId.TryGetValue(sourcePhotonViewId, out PirateBaseCollectionBeamVfx vfx))
            return;

        ActiveBySourceViewId.Remove(sourcePhotonViewId);
        if (vfx != null)
            Destroy(vfx.gameObject);
    }

    void Initialize(Transform sourceTransform, Transform targetTransform, int resolvedSourceViewId)
    {
        source = sourceTransform;
        target = targetTransform;
        sourceViewId = resolvedSourceViewId;

        SpriteRenderer sourceRenderer = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
        if (sourceRenderer != null)
        {
            sortingLayerId = sourceRenderer.sortingLayerID;
            sortingOrder = sourceRenderer.sortingOrder + SortingOrderOffset;
        }

        if (source != null)
            gameObject.layer = source.gameObject.layer;

        glowLine = CreateLine("PirateBaseCollectGlow", 0.34f, sortingOrder);
        coreLine = CreateLine("PirateBaseCollectCore", 0.12f, sortingOrder + 1);
        CreateAudioSource();
    }

    void Update()
    {
        if (source == null || target == null)
        {
            StopBeam(sourceViewId);
            return;
        }

        transform.position = source.position;
        UpdateBeam();
        UpdateAudio();
    }

    void OnDestroy()
    {
        if (ActiveBySourceViewId.TryGetValue(sourceViewId, out PirateBaseCollectionBeamVfx active) && active == this)
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
        float pulse = Mathf.Sin(Time.time * 14f) * 0.5f + 0.5f;
        float wave = Mathf.Lerp(0.045f, 0.14f, pulse) * Mathf.Clamp01(distance / 5f);

        UpdateLine(glowLine, start, end, perpendicular, wave, pulse, false);
        UpdateLine(coreLine, start, end, perpendicular, wave * 0.34f, pulse, true);
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
            float ripple = Mathf.Sin((t * Mathf.PI * 5f) + Time.time * 13f) * wave * taper;
            float shimmer = Mathf.Sin((t * Mathf.PI * 11f) - Time.time * 8f) * wave * 0.32f * taper;
            point += perpendicular * (ripple + shimmer);
            point.z = source.position.z + EffectZOffset;
            line.SetPosition(i, point);
        }

        float alpha = core ? Mathf.Lerp(0.82f, 1f, pulse) : Mathf.Lerp(0.38f, 0.68f, pulse);
        line.colorGradient = core ? BuildCoreGradient(alpha) : BuildGlowGradient(alpha);
        line.widthMultiplier = core
            ? Mathf.Lerp(0.09f, 0.15f, pulse)
            : Mathf.Lerp(0.23f, 0.38f, pulse);
    }

    Vector3 GetSourcePoint()
    {
        SpriteRenderer renderer = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
        if (renderer == null)
            return source != null ? source.position : Vector3.zero;

        Vector3 center = renderer.bounds.center;
        Vector3 towardTarget = target != null ? target.position - center : source.up;
        if (towardTarget.sqrMagnitude <= 0.0001f)
            towardTarget = source.up;

        return center + towardTarget.normalized * Mathf.Max(0.15f, Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y) * 0.18f);
    }

    Vector3 GetTargetPoint(Vector3 sourcePoint)
    {
        Collider2D collider = target != null ? target.GetComponent<Collider2D>() : null;
        if (collider != null)
            return collider.ClosestPoint(sourcePoint);

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
        line.numCapVertices = 12;
        line.numCornerVertices = 10;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = order;
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.58f),
            new Keyframe(0.18f, 1.18f),
            new Keyframe(0.62f, 0.8f),
            new Keyframe(1f, 0.24f));
        return line;
    }

    void CreateAudioSource()
    {
        AudioClip clip = AudioManager.Instance.PirateBaseDrillClip;
        if (clip == null)
            return;

        audioSource = gameObject.AddComponent<AudioSource>();
        AudioManager.Instance.ConfigureSpatialSource(audioSource, 0.5f);
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.Play();
    }

    void UpdateAudio()
    {
        if (audioSource == null || source == null)
            return;

        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    static Gradient BuildCoreGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.96f, 1f, 0.86f), 0f),
                new GradientColorKey(new Color(0.28f, 1f, 0.66f), 0.44f),
                new GradientColorKey(new Color(0.1f, 0.74f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f * alpha, 0f),
                new GradientAlphaKey(0.82f * alpha, 0.55f),
                new GradientAlphaKey(0.2f * alpha, 1f)
            });
        return gradient;
    }

    static Gradient BuildGlowGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.3f, 1f, 0.76f), 0f),
                new GradientColorKey(new Color(0.08f, 0.6f, 1f), 0.56f),
                new GradientColorKey(new Color(0.02f, 0.18f, 0.48f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.42f * alpha, 0f),
                new GradientAlphaKey(0.52f * alpha, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
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
            name = "PirateBaseCollectionBeamVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3355;
        return sharedMaterial;
    }
}
