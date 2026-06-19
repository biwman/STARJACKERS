using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public sealed class RescueShipBeamVfx : MonoBehaviour
{
    const int BeamPointCount = 18;
    const float EffectZOffset = -0.075f;
    const int SortingOrderOffset = 276;

    static readonly Dictionary<int, RescueShipBeamVfx> ActiveBySourceViewId = new Dictionary<int, RescueShipBeamVfx>();
    static Material sharedMaterial;
    static AudioClip rescueShipBeamClip;

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
        GetRescueShipBeamClip();
    }

    public static void StartBeam(int sourcePhotonViewId, int targetPhotonViewId)
    {
        StopBeam(sourcePhotonViewId);

        PhotonView sourceView = PhotonView.Find(sourcePhotonViewId);
        PhotonView targetView = PhotonView.Find(targetPhotonViewId);
        if (sourceView == null || targetView == null)
            return;

        GameObject effect = new GameObject("RescueShipBeamVfx_" + sourcePhotonViewId);
        RescueShipBeamVfx vfx = effect.AddComponent<RescueShipBeamVfx>();
        vfx.Initialize(sourceView.transform, targetView.transform, sourcePhotonViewId);
        ActiveBySourceViewId[sourcePhotonViewId] = vfx;
    }

    public static void StopBeam(int sourcePhotonViewId)
    {
        if (!ActiveBySourceViewId.TryGetValue(sourcePhotonViewId, out RescueShipBeamVfx vfx))
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

        glowLine = CreateLine("RescueBeamGlow", 0.34f, sortingOrder);
        coreLine = CreateLine("RescueBeamCore", 0.12f, sortingOrder + 1);
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
        if (ActiveBySourceViewId.TryGetValue(sourceViewId, out RescueShipBeamVfx active) && active == this)
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
        float pulse = Mathf.Sin(Time.time * 6.5f) * 0.5f + 0.5f;
        float wave = Mathf.Lerp(0.02f, 0.08f, pulse) * Mathf.Clamp01(distance / 8f);

        UpdateLine(glowLine, start, end, perpendicular, wave, pulse, false);
        UpdateLine(coreLine, start, end, perpendicular, wave * 0.38f, pulse, true);
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
            float ripple = Mathf.Sin((t * Mathf.PI * 5f) + Time.time * 9f) * wave * taper;
            float shimmer = Mathf.Sin((t * Mathf.PI * 12f) - Time.time * 5.5f) * wave * 0.22f * taper;
            point += perpendicular * (ripple + shimmer);
            point.z = source.position.z + EffectZOffset;
            line.SetPosition(i, point);
        }

        float alpha = core ? Mathf.Lerp(0.78f, 1f, pulse) : Mathf.Lerp(0.36f, 0.62f, pulse);
        line.colorGradient = core ? BuildCoreGradient(alpha) : BuildGlowGradient(alpha);
        line.widthMultiplier = core
            ? Mathf.Lerp(0.09f, 0.16f, pulse)
            : Mathf.Lerp(0.24f, 0.38f, pulse);
    }

    Vector3 GetSourcePoint()
    {
        SpriteRenderer renderer = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
        if (renderer == null)
            return source != null ? source.position : Vector3.zero;

        Bounds bounds = renderer.bounds;
        float side = Mathf.Sign((target != null ? target.position.x : source.position.x) - source.position.x);
        if (Mathf.Abs(side) < 0.1f)
            side = 1f;

        return new Vector3(
            source.position.x + bounds.extents.x * 0.58f * side,
            source.position.y - bounds.extents.y * 0.12f,
            source.position.z);
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
        line.numCapVertices = 14;
        line.numCornerVertices = 10;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = order;
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.22f),
            new Keyframe(0.14f, 0.92f),
            new Keyframe(0.78f, 0.74f),
            new Keyframe(1f, 0.2f));
        return line;
    }

    void CreateAudioSource()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = GetRescueShipBeamClip();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 0.72f;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 4f;
        audioSource.maxDistance = 22f;

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
                new GradientColorKey(new Color(0.92f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.48f, 0.92f, 1f), 0.46f),
                new GradientColorKey(new Color(0.08f, 0.62f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha, 0f),
                new GradientAlphaKey(alpha * 0.86f, 0.55f),
                new GradientAlphaKey(alpha * 0.34f, 1f)
            });
        return gradient;
    }

    static Gradient BuildGlowGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.56f, 0.9f, 1f), 0f),
                new GradientColorKey(new Color(0.2f, 0.62f, 1f), 0.5f),
                new GradientColorKey(new Color(0.02f, 0.2f, 0.48f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha * 0.78f, 0f),
                new GradientAlphaKey(alpha * 0.54f, 0.52f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    static AudioClip GetRescueShipBeamClip()
    {
        if (rescueShipBeamClip != null)
            return rescueShipBeamClip;

        rescueShipBeamClip = Resources.Load<AudioClip>("Audio/rescue_ship_beam");
        return rescueShipBeamClip;
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
            name = "RescueShipBeamVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3350;
        return sharedMaterial;
    }
}
