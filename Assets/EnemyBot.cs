using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum EnemyBotKind
{
    Drone,
    Corsair,
    SpaceMine,
    SpaceTruck,
    Mothership,
    NeutralFighter,
    RadarShip,
    RescueShip,
    PirateFighter,
    PirateFighterElite,
    PirateFighterAce,
    SpaceManta,
    PirateBase,
    GravitySquid,
    HunterLance,
    ContainerShip,
    CosmicWorm
}

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
        AudioClip clip = AudioManager.Instance.DrillingClip;
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

public sealed class GravitySquidTetherVfx : MonoBehaviour
{
    const int BeamPointCount = 24;
    const float EffectZOffset = -0.085f;
    const int SortingOrderOffset = 282;

    static readonly Dictionary<int, GravitySquidTetherVfx> ActiveBySourceViewId = new Dictionary<int, GravitySquidTetherVfx>();
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

        GameObject effect = new GameObject("GravitySquidTetherVfx_" + sourcePhotonViewId);
        GravitySquidTetherVfx vfx = effect.AddComponent<GravitySquidTetherVfx>();
        vfx.Initialize(sourceView.transform, targetView.transform, sourcePhotonViewId);
        ActiveBySourceViewId[sourcePhotonViewId] = vfx;
    }

    public static void StopBeam(int sourcePhotonViewId)
    {
        if (!ActiveBySourceViewId.TryGetValue(sourcePhotonViewId, out GravitySquidTetherVfx vfx))
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

        glowLine = CreateLine("GravitySquidTetherGlow", 0.42f, sortingOrder);
        coreLine = CreateLine("GravitySquidTetherCore", 0.13f, sortingOrder + 1);
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
        if (ActiveBySourceViewId.TryGetValue(sourceViewId, out GravitySquidTetherVfx active) && active == this)
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
        float pulse = Mathf.Sin(Time.time * 8.2f) * 0.5f + 0.5f;
        float wave = Mathf.Lerp(0.04f, 0.16f, pulse) * Mathf.Clamp01(distance / 10f);

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
            float ripple = Mathf.Sin((t * Mathf.PI * 6f) - Time.time * 11f) * wave * taper;
            float pull = Mathf.Sin((t * Mathf.PI * 15f) + Time.time * 6.5f) * wave * 0.2f * taper;
            point += perpendicular * (ripple + pull);
            point.z = source.position.z + EffectZOffset;
            line.SetPosition(i, point);
        }

        float alpha = core ? Mathf.Lerp(0.84f, 1f, pulse) : Mathf.Lerp(0.32f, 0.66f, pulse);
        line.colorGradient = core ? BuildCoreGradient(alpha) : BuildGlowGradient(alpha);
        line.widthMultiplier = core
            ? Mathf.Lerp(0.1f, 0.17f, pulse)
            : Mathf.Lerp(0.27f, 0.48f, pulse);
    }

    Vector3 GetSourcePoint()
    {
        SpriteRenderer renderer = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
        if (renderer == null)
            return source != null ? source.position : Vector3.zero;

        Vector3 center = renderer.bounds.center;
        Vector3 targetPosition = target != null ? target.position : center;
        Vector3 towardTarget = targetPosition - center;
        if (towardTarget.sqrMagnitude <= 0.0001f)
            towardTarget = source.up;

        return center + towardTarget.normalized * Mathf.Max(0.08f, renderer.bounds.extents.y * 0.18f);
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
        line.numCornerVertices = 12;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = order;
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.18f),
            new Keyframe(0.18f, 0.94f),
            new Keyframe(0.72f, 0.82f),
            new Keyframe(1f, 0.24f));
        return line;
    }

    void CreateAudioSource()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = AudioManager.Instance.GravitySquidTetherClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 0.7f;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 3.5f;
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
                new GradientColorKey(new Color(0.96f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.16f, 0.95f, 1f), 0.48f),
                new GradientColorKey(new Color(0.58f, 0.22f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha, 0f),
                new GradientAlphaKey(alpha * 0.92f, 0.55f),
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
                new GradientColorKey(new Color(0.08f, 0.92f, 1f), 0f),
                new GradientColorKey(new Color(0.32f, 0.34f, 1f), 0.5f),
                new GradientColorKey(new Color(0.48f, 0.08f, 0.86f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha * 0.78f, 0f),
                new GradientAlphaKey(alpha * 0.58f, 0.52f),
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
            name = "GravitySquidTetherVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3360;
        return sharedMaterial;
    }
}

public sealed class HunterLanceBeamVfx : MonoBehaviour
{
    const float EffectZOffset = -0.092f;
    const int SortingOrder = 2385;
    const float ShotGlowWidth = 0.96f;
    const float ShotCoreWidth = 0.27f;
    const float AimGlowWidth = 0.48f;
    const float AimCoreWidth = 0.11f;

    static Material sharedMaterial;

    LineRenderer glowLine;
    LineRenderer coreLine;
    Vector3 origin;
    Vector3 direction = Vector3.up;
    float range = 12f;
    float duration = 0.6f;
    float startedAt;
    bool isShot;

    public static void Prewarm()
    {
        GetMaterial();
    }

    public static void SpawnAim(Vector2 start, Vector2 aimDirection, float beamRange, float warningDuration)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject effect = new GameObject("HunterLanceAimVfx");
        HunterLanceBeamVfx vfx = effect.AddComponent<HunterLanceBeamVfx>();
        vfx.Initialize(start, aimDirection, beamRange, Mathf.Max(0.1f, warningDuration), false);
    }

    public static void SpawnShot(Vector2 start, Vector2 aimDirection, float beamRange)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject effect = new GameObject("HunterLanceShotVfx");
        HunterLanceBeamVfx vfx = effect.AddComponent<HunterLanceBeamVfx>();
        vfx.Initialize(start, aimDirection, beamRange, 0.22f, true);
    }

    void Initialize(Vector2 start, Vector2 aimDirection, float beamRange, float effectDuration, bool shot)
    {
        origin = new Vector3(start.x, start.y, EffectZOffset);
        direction = aimDirection.sqrMagnitude > 0.001f
            ? new Vector3(aimDirection.x, aimDirection.y, 0f).normalized
            : Vector3.up;
        range = Mathf.Max(0.5f, beamRange);
        duration = Mathf.Max(0.05f, effectDuration);
        isShot = shot;
        startedAt = Time.time;

        transform.position = origin;
        glowLine = CreateLine("HunterLanceGlow", shot ? ShotGlowWidth : AimGlowWidth, SortingOrder);
        coreLine = CreateLine("HunterLanceCore", shot ? ShotCoreWidth : AimCoreWidth, SortingOrder + 1);
        UpdateLines(0f);
    }

    void Update()
    {
        float t = Mathf.Clamp01((Time.time - startedAt) / duration);
        UpdateLines(t);

        if (t >= 1f)
            Destroy(gameObject);
    }

    void UpdateLines(float t)
    {
        Vector3 start = origin;
        Vector3 end = origin + direction * range;
        float pulse = Mathf.Sin(Time.time * (isShot ? 52f : 18f)) * 0.5f + 0.5f;
        float alpha = isShot
            ? Mathf.Lerp(1f, 0f, t)
            : Mathf.Lerp(0.28f, 0.76f, Mathf.PingPong(t * 2.4f, 1f)) * Mathf.Lerp(1f, 0.15f, Mathf.Max(0f, t - 0.72f) / 0.28f);

        ApplyLine(glowLine, start, end, isShot ? BuildShotGlow(alpha, pulse) : BuildAimGlow(alpha, pulse), isShot ? ShotGlowWidth : Mathf.Lerp(0.32f, 0.56f, pulse));
        ApplyLine(coreLine, start, end, isShot ? BuildShotCore(alpha, pulse) : BuildAimCore(alpha, pulse), isShot ? ShotCoreWidth : Mathf.Lerp(0.07f, 0.14f, pulse));
    }

    void ApplyLine(LineRenderer line, Vector3 start, Vector3 end, Gradient gradient, float width)
    {
        if (line == null)
            return;

        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.colorGradient = gradient;
        line.widthMultiplier = width;
    }

    LineRenderer CreateLine(string lineName, float width, int order)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = width;
        line.numCapVertices = 12;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetMaterial();
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = order;
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.15f),
            new Keyframe(0.12f, 1f),
            new Keyframe(0.88f, 1f),
            new Keyframe(1f, 0.15f));
        return line;
    }

    static Gradient BuildAimCore(float alpha, float pulse)
    {
        return BuildGradient(
            new Color(0.68f, 0.95f, 1f),
            new Color(0.08f, 0.72f, 1f),
            new Color(0.04f, 0.28f, 0.9f),
            alpha * Mathf.Lerp(0.74f, 1f, pulse));
    }

    static Gradient BuildAimGlow(float alpha, float pulse)
    {
        return BuildGradient(
            new Color(0.08f, 0.58f, 1f),
            new Color(0.12f, 0.24f, 1f),
            new Color(0.52f, 0.08f, 1f),
            alpha * Mathf.Lerp(0.35f, 0.58f, pulse));
    }

    static Gradient BuildShotCore(float alpha, float pulse)
    {
        return BuildGradient(
            Color.white,
            new Color(0.48f, 0.96f, 1f),
            new Color(0.08f, 0.6f, 1f),
            alpha * Mathf.Lerp(0.82f, 1f, pulse));
    }

    static Gradient BuildShotGlow(float alpha, float pulse)
    {
        return BuildGradient(
            new Color(0.62f, 0.96f, 1f),
            new Color(0.02f, 0.6f, 1f),
            new Color(0.08f, 0.12f, 0.92f),
            alpha * Mathf.Lerp(0.38f, 0.72f, pulse));
    }

    static Gradient BuildGradient(Color start, Color middle, Color end, float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(middle, 0.46f),
                new GradientColorKey(end, 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha * 0.12f, 0f),
                new GradientAlphaKey(alpha, 0.18f),
                new GradientAlphaKey(alpha * 0.86f, 0.82f),
                new GradientAlphaKey(alpha * 0.08f, 1f)
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
            name = "HunterLanceBeamVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3370;
        return sharedMaterial;
    }
}

public sealed class SpaceAnimalDeathVfx : MonoBehaviour
{
    const float FramesPerSecond = 18f;
    const float FallbackDuration = 0.45f;
    const float EffectZOffset = -0.06f;

    static readonly Dictionary<string, Sprite[]> FrameCacheByResourcePath = new Dictionary<string, Sprite[]>(System.StringComparer.Ordinal);

    SpriteRenderer spriteRenderer;
    Sprite[] frames = System.Array.Empty<Sprite>();
    float targetSize = 2.5f;
    float frameCursor;
    float startedAt;

    public static void Prewarm()
    {
        LoadFrames(ResolveResourcePath(EnemyBotKind.SpaceManta));
        LoadFrames(ResolveResourcePath(EnemyBotKind.GravitySquid));
    }

    public static void Play(EnemyBotKind kind, Vector3 position, float rotationZ, float visualTargetSize)
    {
        GameObject effect = new GameObject("SpaceAnimalDeathVfx_" + kind);
        effect.transform.position = new Vector3(position.x, position.y, position.z + EffectZOffset);
        effect.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
        SpaceAnimalDeathVfx vfx = effect.AddComponent<SpaceAnimalDeathVfx>();
        vfx.Initialize(kind, visualTargetSize);
    }

    void Initialize(EnemyBotKind kind, float visualTargetSize)
    {
        targetSize = Mathf.Max(0.6f, visualTargetSize);
        startedAt = Time.time;
        frames = LoadFrames(ResolveResourcePath(kind));

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = GameVisualTheme.EnemySortingOrder + 4;
        spriteRenderer.color = Color.white;

        if (frames.Length > 0)
        {
            spriteRenderer.sprite = frames[0];
            FitRendererToTargetSize(spriteRenderer, targetSize * 1.12f);
        }

        AudioManager.Instance.PlayExplosionAt(transform.position);
    }

    void Update()
    {
        if (frames == null || frames.Length == 0)
        {
            if (Time.time - startedAt >= FallbackDuration)
                Destroy(gameObject);
            return;
        }

        frameCursor += Time.deltaTime * FramesPerSecond;
        int frameIndex = Mathf.FloorToInt(frameCursor);
        if (frameIndex >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }

        spriteRenderer.sprite = frames[frameIndex];
        FitRendererToTargetSize(spriteRenderer, targetSize * Mathf.Lerp(1.12f, 1.34f, frameIndex / Mathf.Max(1f, frames.Length - 1f)));
    }

    static string ResolveResourcePath(EnemyBotKind kind)
    {
        return kind == EnemyBotKind.GravitySquid
            ? "Enemies/GravitySquid/Death"
            : "Enemies/SpaceManta/Death";
    }

    static Sprite[] LoadFrames(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return System.Array.Empty<Sprite>();

        if (FrameCacheByResourcePath.TryGetValue(resourcePath, out Sprite[] cachedFrames))
            return cachedFrames;

        List<Sprite> result = new List<Sprite>();
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites != null)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                    result.Add(sprites[i]);
            }
        }

        if (result.Count == 0)
        {
            Texture2D[] textures = Resources.LoadAll<Texture2D>(resourcePath);
            if (textures != null)
            {
                for (int i = 0; i < textures.Length; i++)
                {
                    Sprite sprite = CreateSpriteFromTexture(textures[i]);
                    if (sprite != null)
                        result.Add(sprite);
                }
            }
        }

        result.Sort(CompareSpritesForAnimation);
        Sprite[] frames = result.ToArray();
        for (int i = 0; i < frames.Length; i++)
            PrewarmSpriteTexture(frames[i]);

        FrameCacheByResourcePath[resourcePath] = frames;
        return frames;
    }

    static Sprite CreateSpriteFromTexture(Texture2D texture)
    {
        if (texture == null)
            return null;

        float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);
        sprite.name = texture.name;
        return sprite;
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }

    static int CompareSpritesForAnimation(Sprite a, Sprite b)
    {
        int indexA = ExtractTrailingNumber(a != null ? a.name : string.Empty);
        int indexB = ExtractTrailingNumber(b != null ? b.name : string.Empty);
        if (indexA >= 0 && indexB >= 0 && indexA != indexB)
            return indexA.CompareTo(indexB);

        return string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty);
    }

    static int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
            return -1;

        int endIndex = value.Length - 1;
        while (endIndex >= 0 && !char.IsDigit(value[endIndex]))
            endIndex--;

        if (endIndex < 0)
            return -1;

        int startIndex = endIndex;
        while (startIndex >= 0 && char.IsDigit(value[startIndex]))
            startIndex--;

        string numberPart = value.Substring(startIndex + 1, endIndex - startIndex);
        return int.TryParse(numberPart, out int number) ? number : -1;
    }

    static void FitRendererToTargetSize(SpriteRenderer renderer, float size)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds spriteBounds = renderer.sprite.bounds;
        float largest = Mathf.Max(spriteBounds.size.x, spriteBounds.size.y);
        if (largest <= 0.0001f)
            return;

        float scale = size / largest;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }
}

public sealed class PirateBaseLaunchVfx : MonoBehaviour
{
    const float HatchOpenDuration = 2f;
    const float HatchLaunchHoldDuration = 3f;
    const float HatchCloseDuration = 2f;
    const float HatchTargetSizeFactor = 0.37f;
    const int SortingOrderOffset = 8;

    static Sprite[] hatchFrames;

    EnemyBot baseBot;
    SpriteRenderer hatchRenderer;
    int sortingLayerId;
    int sortingOrder;

    public static void Prewarm()
    {
        Sprite[] frames = GetHatchFrames();
        for (int i = 0; i < frames.Length; i++)
            PrewarmSpriteTexture(frames[i]);
    }

    public static void Play(EnemyBot source, EnemyBotKind launchedFighterKind)
    {
        if (source == null)
            return;

        GameObject effect = new GameObject("PirateBaseLaunchVfx");
        effect.transform.SetParent(source.transform, false);
        effect.transform.localPosition = new Vector3(0f, 0f, -0.04f);
        effect.transform.localRotation = Quaternion.identity;
        PirateBaseLaunchVfx vfx = effect.AddComponent<PirateBaseLaunchVfx>();
        vfx.Initialize(source, launchedFighterKind);
    }

    void Initialize(EnemyBot source, EnemyBotKind launchedFighterKind)
    {
        baseBot = source;

        SpriteRenderer baseRenderer = source.GetComponent<SpriteRenderer>();
        if (baseRenderer != null)
        {
            sortingLayerId = baseRenderer.sortingLayerID;
            sortingOrder = baseRenderer.sortingOrder + SortingOrderOffset;
        }
        else
        {
            sortingLayerId = SortingLayer.NameToID(GameVisualTheme.WorldSortingLayerName);
            sortingOrder = GameVisualTheme.EnemySortingOrder + SortingOrderOffset;
        }

        hatchRenderer = CreateRenderer("PirateBaseHatch", sortingOrder);
        StartCoroutine(PlayRoutine());
    }

    SpriteRenderer CreateRenderer(string childName, int order)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = Vector3.zero;
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = order;
        renderer.color = Color.white;
        return renderer;
    }

    IEnumerator PlayRoutine()
    {
        Sprite[] frames = GetHatchFrames();
        if (frames.Length > 0)
        {
            float openFrameDuration = HatchOpenDuration / frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                ApplyHatchFrame(frames[i]);
                yield return new WaitForSeconds(openFrameDuration);
            }
        }
        else
        {
            yield return new WaitForSeconds(HatchOpenDuration);
        }

        yield return new WaitForSeconds(HatchLaunchHoldDuration);

        if (frames.Length > 0)
        {
            float closeFrameDuration = HatchCloseDuration / frames.Length;
            for (int i = frames.Length - 1; i >= 0; i--)
            {
                ApplyHatchFrame(frames[i]);
                yield return new WaitForSeconds(closeFrameDuration);
            }
        }
        else
        {
            yield return new WaitForSeconds(HatchCloseDuration);
        }

        Destroy(gameObject);
    }

    void ApplyHatchFrame(Sprite frame)
    {
        if (hatchRenderer == null || frame == null || baseBot == null)
            return;

        hatchRenderer.enabled = true;
        hatchRenderer.sprite = frame;
        FitRendererToWorldSize(hatchRenderer, Mathf.Max(1f, baseBot.VisualTargetSize * HatchTargetSizeFactor));
    }

    void FitRendererToWorldSize(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds spriteBounds = renderer.sprite.bounds;
        float largestDimension = Mathf.Max(spriteBounds.size.x, spriteBounds.size.y);
        if (largestDimension <= 0.0001f)
            return;

        float worldScale = targetSize / largestDimension;
        Vector3 parentScale = renderer.transform.parent != null ? renderer.transform.parent.lossyScale : Vector3.one;
        float safeX = Mathf.Abs(parentScale.x) > 0.0001f ? Mathf.Abs(parentScale.x) : 1f;
        float safeY = Mathf.Abs(parentScale.y) > 0.0001f ? Mathf.Abs(parentScale.y) : 1f;
        renderer.transform.localScale = new Vector3(worldScale / safeX, worldScale / safeY, 1f);
    }

    static Sprite[] GetHatchFrames()
    {
        if (hatchFrames != null)
            return hatchFrames;

        List<Sprite> frames = new List<Sprite>(9);
        for (int i = 1; i <= 9; i++)
        {
            Texture2D texture = Resources.Load<Texture2D>($"PirateBaseOpening/pirate_base_opening_{i:00}");
            if (texture == null)
                continue;

            float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
            frames.Add(Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit));
        }

        hatchFrames = frames.ToArray();
        return hatchFrames;
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }
}

public static class EnemyTargetingUtility
{
    const float BeaconPriorityRangeMultiplier = 1.9f;

    public static Transform FindClosestTarget(Vector2 origin, PlayerHealth observerHealth, float maxDistance, bool requireNebulaVisibility, bool includeEnemyAstronauts = false)
    {
        Transform bestBeaconTarget = null;
        float bestBeaconDistance = float.MaxValue;

        float beaconRange = maxDistance * BeaconPriorityRangeMultiplier;
        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (!IsValidBeaconTarget(beacon, origin, beaconRange))
                continue;

            float distance = Vector2.Distance(origin, beacon.transform.position);
            if (distance >= bestBeaconDistance)
                continue;

            bestBeaconDistance = distance;
            bestBeaconTarget = beacon.transform;
        }

        if (bestBeaconTarget != null)
            return bestBeaconTarget;

        Transform bestDeployableTarget = null;
        float bestDeployableDistance = float.MaxValue;
        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (!IsValidDeployableTarget(deployable, origin, maxDistance))
                continue;

            float distance = Vector2.Distance(origin, deployable.transform.position);
            if (distance >= bestDeployableDistance)
                continue;

            bestDeployableDistance = distance;
            bestDeployableTarget = deployable.transform;
        }

        if (bestDeployableTarget != null)
            return bestDeployableTarget;

        Transform bestTarget = null;
        float bestDistance = float.MaxValue;

        PlayerHealth[] players = Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (!IsValidPlayerTarget(candidate, observerHealth, origin, maxDistance, requireNebulaVisibility, includeEnemyAstronauts))
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestTarget = candidate.transform;
        }

        return bestTarget;
    }

    public static bool IsTargetValid(Transform target, PlayerHealth observerHealth, Vector2 origin, float maxDistance, bool requireNebulaVisibility, bool includeEnemyAstronauts = false)
    {
        if (target == null)
            return false;

        PlayerHealth player = target.GetComponent<PlayerHealth>();
        if (player != null)
        {
            if (IsAnyBeaconAvailable(origin, maxDistance * BeaconPriorityRangeMultiplier))
                return false;

            if (IsAnyDeployableAvailable(origin, maxDistance))
                return false;

            return IsValidPlayerTarget(player, observerHealth, origin, maxDistance, requireNebulaVisibility, includeEnemyAstronauts);
        }

        PlayerDeployableBase deployable = target.GetComponent<PlayerDeployableBase>();
        if (deployable != null)
        {
            if (IsAnyBeaconAvailable(origin, maxDistance * BeaconPriorityRangeMultiplier))
                return false;

            return IsValidDeployableTarget(deployable, origin, maxDistance);
        }

        LureBeaconDecoy beacon = target.GetComponent<LureBeaconDecoy>();
        return IsValidBeaconTarget(beacon, origin, maxDistance * BeaconPriorityRangeMultiplier);
    }

    public static bool IsAnyTargetInRange(Vector2 origin, PlayerHealth observerHealth, float radius)
    {
        return FindClosestTarget(origin, observerHealth, radius, false) != null;
    }

    static bool IsValidPlayerTarget(PlayerHealth candidate, PlayerHealth observerHealth, Vector2 origin, float maxDistance, bool requireNebulaVisibility, bool includeEnemyAstronauts)
    {
        if (candidate == null || candidate == observerHealth || candidate.IsWreck || candidate.IsBotControlled || candidate.IsEvacuationAnimating)
            return false;

        if (includeEnemyAstronauts)
        {
            if (!ActorIdentity.CanBeTargetedByMonstersActor(candidate))
                return false;
        }
        else if (!ActorIdentity.CanBeTargetedByEnemyShipsActor(candidate))
        {
            return false;
        }

        if (candidate.GetComponent<LureBeaconDecoy>() != null)
            return false;

        if (Vector2.Distance(origin, candidate.transform.position) > maxDistance)
            return false;

        if (requireNebulaVisibility)
        {
            HideInNebulaTarget candidateNebulaState = candidate.GetComponent<HideInNebulaTarget>();
            HideInNebulaTarget observerNebulaState = observerHealth != null ? observerHealth.GetComponent<HideInNebulaTarget>() : null;
            if (candidateNebulaState != null && candidateNebulaState.IsHiddenFromObserver(observerNebulaState))
                return false;
        }

        return true;
    }

    static bool IsValidBeaconTarget(LureBeaconDecoy beacon, Vector2 origin, float maxDistance)
    {
        if (beacon == null || !beacon.CanBeTargeted)
            return false;

        return Vector2.Distance(origin, beacon.transform.position) <= maxDistance;
    }

    static bool IsValidDeployableTarget(PlayerDeployableBase deployable, Vector2 origin, float maxDistance)
    {
        if (deployable == null || !deployable.CanBeTargetedByEnemyBots)
            return false;

        return Vector2.Distance(origin, deployable.transform.position) <= maxDistance;
    }

    static bool IsAnyBeaconAvailable(Vector2 origin, float maxDistance)
    {
        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (IsValidBeaconTarget(beacon, origin, maxDistance))
                return true;
        }

        return false;
    }

    static bool IsAnyDeployableAvailable(Vector2 origin, float maxDistance)
    {
        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (IsValidDeployableTarget(deployable, origin, maxDistance))
                return true;
        }

        return false;
    }
}

public enum EnemyMovementModel
{
    GuardAndChase,
    OrbitMap,
    Drift,
    RouteExtractionZones,
    Mothership,
    NeutralFighter,
    RadarShip,
    RescueShip,
    PirateFighter,
    PirateBase,
    SpaceManta,
    GravitySquid,
    HunterLance,
    ContainerShip,
    CosmicWorm
}

public enum EnemySpawnPattern
{
    SafePerimeter,
    WideCorners
}

public enum EnemyTrailVisualStyle
{
    None,
    OrangeSmall,
    RedLarge,
    GreenTwin,
    BlueTwin,
    OrangeRedTwin,
    PurpleLarge
}

[System.Serializable]
public class EnemyExplosionProfile
{
    Sprite[] cachedFrames;

    public int Damage;
    public WeaponDamageType DamageType;
    public WeaponDeliveryMethod DeliveryMethod;
    public WeaponDeliveryFlags DeliveryFlags;
    public float TriggerRadius;
    public float VisualTargetSize;
    public float VisualDuration;
    public int VisualStartFrame;
    public int VisualColumns;
    public int VisualRows;
    public string VisualResourcePath;
    public string EditorAssetPath;
    public string SoundId;

    public Sprite[] GetVisualFrames()
    {
        if (cachedFrames != null && cachedFrames.Length > 0)
            return cachedFrames;

        if (!string.IsNullOrWhiteSpace(VisualResourcePath))
        {
            cachedFrames = Resources.LoadAll<Sprite>(VisualResourcePath);
            if (cachedFrames != null && cachedFrames.Length > 0)
            {
                SortSpritesForAnimation(cachedFrames);
                return cachedFrames;
            }

            Sprite single = Resources.Load<Sprite>(VisualResourcePath);
            if (single != null)
            {
                cachedFrames = new[] { single };
                return cachedFrames;
            }
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(EditorAssetPath))
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(EditorAssetPath);
            System.Collections.Generic.List<Sprite> sprites = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                    sprites.Add(sprite);
            }

            if (sprites.Count > 0)
            {
                sprites.Sort(CompareSpritesForAnimation);
                cachedFrames = sprites.ToArray();
                return cachedFrames;
            }

            Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>(EditorAssetPath);
            if (single != null)
            {
                cachedFrames = new[] { single };
                return cachedFrames;
            }
        }
#endif

        return System.Array.Empty<Sprite>();
    }

    static void SortSpritesForAnimation(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length <= 1)
            return;

        System.Array.Sort(sprites, CompareSpritesForAnimation);
    }

    static int CompareSpritesForAnimation(Sprite a, Sprite b)
    {
        string nameA = a != null ? a.name : string.Empty;
        string nameB = b != null ? b.name : string.Empty;

        int indexA = ExtractTrailingNumber(nameA);
        int indexB = ExtractTrailingNumber(nameB);
        bool hasIndexA = indexA >= 0;
        bool hasIndexB = indexB >= 0;

        if (hasIndexA && hasIndexB && indexA != indexB)
            return indexA.CompareTo(indexB);

        if (hasIndexA != hasIndexB)
            return hasIndexA ? -1 : 1;

        return string.CompareOrdinal(nameA, nameB);
    }

    static int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
            return -1;

        int endIndex = value.Length - 1;
        if (!char.IsDigit(value[endIndex]))
            return -1;

        int startIndex = endIndex;
        while (startIndex >= 0 && char.IsDigit(value[startIndex]))
            startIndex--;

        string numberPart = value.Substring(startIndex + 1, endIndex - startIndex);
        return int.TryParse(numberPart, out int number) ? number : -1;
    }
}

[System.Serializable]
public class EnemyTrailProfile
{
    public Vector2 RootOffsetFactors;
    public float RootRotationZ;
    public Vector2[] TrailOffsetFactors;
    public float MinTrailTime;
    public float MaxTrailTime;
    public float MinTrailWidth;
    public float MaxTrailWidth;
    public float EmissionThreshold;
    public EnemyTrailVisualStyle VisualStyle;
}

[System.Serializable]
public class EnemyMovementProfile
{
    public EnemyMovementModel Model;
    public EnemySpawnPattern SpawnPattern;
    public float MoveSpeed;
    public float TurnResponsiveness;
    public float DetectionRadius;
    public float DisengageRadius;
    public float OrbitDistance;
    public float PreferredDistance;
    public float ShootDistance;
    public float RepathInterval;
    public float TargetRefreshInterval;
    public float IdleDriftTurnSpeed;
    public float OrbitRadiusFactor;
    public float OrbitAngularSpeed;
}

[System.Serializable]
public class EnemyWeaponProfile
{
    public int AmmoCount;
    public float ReloadDuration;
    public float FireRate;
    public int Damage;
    public WeaponDamageType DamageType;
    public WeaponDeliveryMethod DeliveryMethod;
    public WeaponDeliveryFlags DeliveryFlags;
    public float BulletScaleMultiplier;
    public Color BulletColor;
    public float BulletSpeed;
    public float MuzzleOffsetDistance;
    public bool InfiniteAmmo;
    public bool RotateTowardAim;
    public float Range;
    public string ShotSoundId;
    public int MuzzleStreamCount;
}

[System.Serializable]
public class EnemyWreckProfile
{
    Sprite cachedSprite;

    public float Mass;
    public float LinearDamping;
    public float AngularDamping;
    public float DriftSpeed;
    public float AngularVelocityRange;
    public string RewardItemId;
    public bool DestroyWhenEmpty;
    public Color BaseColor;
    public string VisualResourcePath;
    public string EditorAssetPath;

    public Sprite GetVisualSprite()
    {
        if (cachedSprite != null)
            return cachedSprite;

        if (!string.IsNullOrWhiteSpace(VisualResourcePath))
        {
            cachedSprite = Resources.Load<Sprite>(VisualResourcePath);
            if (cachedSprite != null)
                return cachedSprite;

            Sprite[] sprites = Resources.LoadAll<Sprite>(VisualResourcePath);
            cachedSprite = GetLargestSprite(sprites);
            if (cachedSprite != null)
                return cachedSprite;

            Texture2D texture = Resources.Load<Texture2D>(VisualResourcePath);
            if (texture != null)
            {
                float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
                cachedSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
                return cachedSprite;
            }
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(EditorAssetPath))
        {
            cachedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EditorAssetPath);
            if (cachedSprite != null)
                return cachedSprite;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(EditorAssetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    cachedSprite = sprite;
                    return cachedSprite;
                }
            }
        }
#endif

        return null;
    }

    static Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (candidate == null)
                continue;

            float area = candidate.rect.width * candidate.rect.height;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }
}

[System.Serializable]
public class EnemyBotDefinition
{
    Sprite cachedSprite;

    public EnemyBotKind Kind;
    public string Id;
    public string DisplayName;
    public string InstantiationMarker;
    public string VisualResourcePath;
    public string AnimationResourcePath;
    public string EditorAssetPath;
    public float AnimationFramesPerSecond;
    public float TargetSize;
    public float PhysicsMass;
    public float LinearDamping;
    public float AngularDamping;
    public int DefaultHp;
    public int DefaultShield;
    public int MaxHp;
    public int MaxShield;
    public float DefaultSpeedMultiplier = 1f;
    public bool DefaultEnabled;
    public bool ShowInEnemySettings = true;
    public int DefaultCount;
    public int DefaultSpawnSecond;
    public EnemyMovementProfile Movement;
    public EnemyWeaponProfile Weapon;
    public EnemyWreckProfile Wreck;
    public EnemyTrailProfile Trails;
    public EnemyExplosionProfile Explosion;

    public string EnabledRoomKey => $"enemy.{Id}.enabled";
    public string CountRoomKey => $"enemy.{Id}.count";
    public string HpRoomKey => $"enemy.{Id}.hp";
    public string ShieldRoomKey => $"enemy.{Id}.shield";
    public string DamageRoomKey => $"enemy.{Id}.damage";
    public string SpeedRoomKey => $"enemy.{Id}.speed";
    public string SpawnSecondRoomKey => $"enemy.{Id}.spawnSecond";
    public string RespawnEnabledRoomKey => $"enemy.{Id}.respawnEnabled";
    public string RespawnIntervalRoomKey => $"enemy.{Id}.respawnInterval";
    public int DefaultDamage => Explosion != null ? Explosion.Damage : Weapon != null ? Weapon.Damage : 0;

    public Sprite GetVisualSprite()
    {
        if (cachedSprite != null)
            return cachedSprite;

        if (!string.IsNullOrWhiteSpace(VisualResourcePath))
        {
            cachedSprite = Resources.Load<Sprite>(VisualResourcePath);
            if (cachedSprite != null)
                return cachedSprite;

            Sprite[] sprites = Resources.LoadAll<Sprite>(VisualResourcePath);
            cachedSprite = GetLargestSprite(sprites);
            if (cachedSprite != null)
                return cachedSprite;

            Texture2D texture = Resources.Load<Texture2D>(VisualResourcePath);
            if (texture != null)
            {
                float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
                cachedSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
                return cachedSprite;
            }
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(EditorAssetPath))
        {
            cachedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EditorAssetPath);
            if (cachedSprite != null)
                return cachedSprite;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(EditorAssetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    cachedSprite = sprite;
                    return cachedSprite;
                }
            }
        }
#endif

        return null;
    }

    static Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (candidate == null)
                continue;

            float area = candidate.rect.width * candidate.rect.height;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }
}

public static class EnemyBotCatalog
{
    static readonly EnemyBotDefinition[] Definitions =
    {
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.Drone,
            Id = "drone",
            DisplayName = "Drone",
            InstantiationMarker = "enemy_bot",
            VisualResourcePath = "droid1_resource",
            EditorAssetPath = "Assets/droid1.png",
            TargetSize = 1.04f,
            PhysicsMass = 2.8f,
            LinearDamping = 0.08f,
            AngularDamping = 0.22f,
            DefaultHp = 50,
            DefaultShield = 20,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = true,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.GuardAndChase,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.1f,
                TurnResponsiveness = 300f,
                DetectionRadius = 10f,
                DisengageRadius = 20f,
                OrbitDistance = 5.5f,
                PreferredDistance = 7.5f,
                ShootDistance = 12f,
                RepathInterval = 0.35f,
                TargetRefreshInterval = 0.45f,
                IdleDriftTurnSpeed = 18f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 10,
                ReloadDuration = 6f,
                FireRate = 0.15f,
                Damage = 10,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.BurstProjectile,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 10f,
                MuzzleOffsetDistance = 0.5f,
                InfiniteAmmo = false,
                RotateTowardAim = true,
                Range = 12f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 4.6f,
                LinearDamping = 0.56f,
                AngularDamping = 0.72f,
                DriftSpeed = 0.12f,
                AngularVelocityRange = 4f,
                RewardItemId = InventoryItemCatalog.DroidScrapId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.2f, 0.23f, 0.26f, 0.94f)
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, 0.44f),
                RootRotationZ = 0f,
                TrailOffsetFactors = new[] { new Vector2(0f, 0.02f) },
                MinTrailTime = 0.22f,
                MaxTrailTime = 0.82f,
                MinTrailWidth = 0.03f,
                MaxTrailWidth = 0.16f,
                EmissionThreshold = 0.04f,
                VisualStyle = EnemyTrailVisualStyle.OrangeSmall
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.Corsair,
            Id = "corsair",
            DisplayName = "Corsair",
            InstantiationMarker = "enemy_bot_corsair",
            VisualResourcePath = "statek_duzy_resource",
            EditorAssetPath = "Assets/statek_duzy.png",
            TargetSize = 5.2f,
            PhysicsMass = 24f,
            LinearDamping = 0.16f,
            AngularDamping = 0.38f,
            DefaultHp = 200,
            DefaultShield = 20,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = true,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.OrbitMap,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 1.9f,
                TurnResponsiveness = 150f,
                DetectionRadius = 7f,
                OrbitRadiusFactor = 0.43f,
                OrbitAngularSpeed = 0.32f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 9999,
                ReloadDuration = 0f,
                FireRate = 1f,
                Damage = 20,
                DamageType = WeaponDamageType.Plasma,
                DeliveryMethod = WeaponDeliveryMethod.DirectProjectile,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 2f,
                BulletColor = new Color(0.15f, 1f, 0.28f, 1f),
                BulletSpeed = 9f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 7f,
                ShotSoundId = "corsair"
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 22f,
                LinearDamping = 0.84f,
                AngularDamping = 1.05f,
                DriftSpeed = 0.07f,
                AngularVelocityRange = 1.5f,
                RewardItemId = InventoryItemCatalog.CorsairSalvageId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.17f, 0.15f, 0.16f, 0.96f),
                VisualResourcePath = "wrak_corsair_resource",
                EditorAssetPath = "Assets/wrak_corsair.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.62f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.76f, 0.08f),
                    new Vector2(0f, 0.2f),
                    new Vector2(0.76f, 0.08f)
                },
                MinTrailTime = 0.65f,
                MaxTrailTime = 1.55f,
                MinTrailWidth = 0.12f,
                MaxTrailWidth = 0.34f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.RedLarge
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.SpaceMine,
            Id = "space_mine",
            DisplayName = "Space Mine",
            InstantiationMarker = "enemy_bot_space_mine",
            VisualResourcePath = "space_mine_resource",
            EditorAssetPath = "Assets/space mine.png",
            TargetSize = 1.08f,
            PhysicsMass = 3.8f,
            LinearDamping = 0.18f,
            AngularDamping = 0.42f,
            DefaultHp = 20,
            DefaultShield = 20,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = true,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.Drift,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 0.18f,
                TurnResponsiveness = 20f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                DamageType = WeaponDamageType.None,
                DeliveryMethod = WeaponDeliveryMethod.None,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 3.6f,
                LinearDamping = 0.78f,
                AngularDamping = 0.96f,
                DriftSpeed = 0.09f,
                AngularVelocityRange = 2.4f,
                RewardItemId = InventoryItemCatalog.SpaceMineWreckId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.19f, 0.21f, 0.24f, 0.96f),
                VisualResourcePath = "wrak_miny_resource",
                EditorAssetPath = "Assets/wrak_miny.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = Vector2.zero,
                RootRotationZ = 0f,
                TrailOffsetFactors = System.Array.Empty<Vector2>(),
                MinTrailTime = 0f,
                MaxTrailTime = 0f,
                MinTrailWidth = 0f,
                MaxTrailWidth = 0f,
                EmissionThreshold = 1f,
                VisualStyle = EnemyTrailVisualStyle.None
            },
            Explosion = new EnemyExplosionProfile
            {
                Damage = 50,
                DamageType = WeaponDamageType.Explosive,
                DeliveryMethod = WeaponDeliveryMethod.Mine,
                DeliveryFlags = WeaponDeliveryFlags.AreaDamage,
                TriggerRadius = 2.08f,
                VisualTargetSize = 4.1f,
                VisualDuration = 1.25f,
                VisualStartFrame = 2,
                VisualColumns = 4,
                VisualRows = 6,
                VisualResourcePath = "",
                EditorAssetPath = "",
                SoundId = "space_mine_boom"
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.SpaceTruck,
            Id = "space_truck",
            DisplayName = "Space Truck",
            InstantiationMarker = "enemy_bot_space_truck",
            VisualResourcePath = "space_truck_resource",
            EditorAssetPath = "Assets/space_truck.png",
            TargetSize = 4.2f,
            PhysicsMass = 18f,
            LinearDamping = 0.1f,
            AngularDamping = 0.32f,
            DefaultHp = 100,
            DefaultShield = 50,
            DefaultSpeedMultiplier = 1.5f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.RouteExtractionZones,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 1.9f,
                TurnResponsiveness = 170f,
                TargetRefreshInterval = 0.45f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                DamageType = WeaponDamageType.None,
                DeliveryMethod = WeaponDeliveryMethod.None,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 18f,
                LinearDamping = 0.78f,
                AngularDamping = 0.96f,
                DriftSpeed = 0.08f,
                AngularVelocityRange = 1.8f,
                RewardItemId = InventoryItemCatalog.SpaceTruckWreckId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.18f, 0.22f, 0.2f, 0.98f),
                VisualResourcePath = "space_truck_wrak_resource",
                EditorAssetPath = "Assets/space_truck_wrak.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.48f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.36f, 0.02f),
                    new Vector2(0.36f, 0.02f)
                },
                MinTrailTime = 0.5f,
                MaxTrailTime = 1.25f,
                MinTrailWidth = 0.08f,
                MaxTrailWidth = 0.24f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.GreenTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.ContainerShip,
            Id = "container_ship",
            DisplayName = "Container Hauler",
            InstantiationMarker = "enemy_bot_container_ship",
            VisualResourcePath = "Enemies/ContainerShip/container_ship",
            EditorAssetPath = "Assets/Resources/Enemies/ContainerShip/container_ship.png",
            TargetSize = 3.35f,
            PhysicsMass = 15f,
            LinearDamping = 0.11f,
            AngularDamping = 0.3f,
            DefaultHp = 120,
            DefaultShield = 70,
            MaxHp = 280,
            MaxShield = 220,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.ContainerShip,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 1.32f,
                TurnResponsiveness = 180f,
                OrbitRadiusFactor = 0.38f,
                OrbitAngularSpeed = 0.24f,
                TargetRefreshInterval = 0.35f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                DamageType = WeaponDamageType.None,
                DeliveryMethod = WeaponDeliveryMethod.None,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 15f,
                LinearDamping = 0.82f,
                AngularDamping = 1f,
                DriftSpeed = 0.08f,
                AngularVelocityRange = 1.6f,
                RewardItemId = InventoryItemCatalog.ContainerShipWreckId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.34f, 0.35f, 0.36f, 0.98f),
                VisualResourcePath = "Enemies/ContainerShip/container_ship_wreck",
                EditorAssetPath = "Assets/Resources/Enemies/ContainerShip/container_ship_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0.46f, 0f),
                RootRotationZ = 90f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(0.1f, -0.34f),
                    new Vector2(0.1f, 0.34f)
                },
                MinTrailTime = 0.38f,
                MaxTrailTime = 1.1f,
                MinTrailWidth = 0.05f,
                MaxTrailWidth = 0.19f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.BlueTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.NeutralFighter,
            Id = "neutral_fighter",
            DisplayName = "Neutral Fighter",
            InstantiationMarker = "enemy_bot_neutral_fighter",
            VisualResourcePath = "neutral_fighter_resource",
            EditorAssetPath = "Assets/neutral_fighter.png",
            TargetSize = 0.94f,
            PhysicsMass = 5.4f,
            LinearDamping = 0.08f,
            AngularDamping = 0.2f,
            DefaultHp = 20,
            DefaultShield = 20,
            DefaultSpeedMultiplier = 1.5f,
            DefaultEnabled = false,
            DefaultCount = 2,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.NeutralFighter,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.1f,
                TurnResponsiveness = 320f,
                DetectionRadius = 6f,
                DisengageRadius = 8.5f,
                OrbitDistance = 3.1f,
                PreferredDistance = 4.4f,
                ShootDistance = 7.8f,
                RepathInterval = 0.24f,
                TargetRefreshInterval = 0.28f,
                IdleDriftTurnSpeed = 28f,
                OrbitAngularSpeed = 1.25f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 6,
                ReloadDuration = 4f,
                FireRate = 0.5f,
                Damage = 10,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.BurstProjectile,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 0.58f,
                BulletColor = new Color(1f, 0.08f, 0.04f, 1f),
                BulletSpeed = 11.5f,
                MuzzleOffsetDistance = 0.62f,
                InfiniteAmmo = false,
                RotateTowardAim = true,
                Range = 8f,
                ShotSoundId = "shoot_small"
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 3.8f,
                LinearDamping = 0.68f,
                AngularDamping = 0.85f,
                DriftSpeed = 0.1f,
                AngularVelocityRange = 3.2f,
                RewardItemId = InventoryItemCatalog.NeutralFighterSalvageId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.42f, 0.44f, 0.46f, 0.98f),
                VisualResourcePath = "neutral_fighter_wreck_resource",
                EditorAssetPath = "Assets/neutral_fighter_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.44f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[] { new Vector2(0f, 0.04f) },
                MinTrailTime = 0.18f,
                MaxTrailTime = 0.7f,
                MinTrailWidth = 0.025f,
                MaxTrailWidth = 0.14f,
                EmissionThreshold = 0.03f,
                VisualStyle = EnemyTrailVisualStyle.OrangeSmall
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.RadarShip,
            Id = "radar_ship",
            DisplayName = "Radar Ship",
            InstantiationMarker = "enemy_bot_radar_ship",
            VisualResourcePath = "radar_ship_resource",
            EditorAssetPath = "Assets/radar_ship.png",
            TargetSize = 3.2f,
            PhysicsMass = 16f,
            LinearDamping = 0.11f,
            AngularDamping = 0.28f,
            DefaultHp = 90,
            DefaultShield = 110,
            DefaultSpeedMultiplier = 1.1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.RadarShip,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 0.88f,
                TurnResponsiveness = 150f,
                DetectionRadius = 8.5f,
                DisengageRadius = 12f,
                OrbitDistance = 5.6f,
                PreferredDistance = 7.8f,
                ShootDistance = 8.5f,
                RepathInterval = 0.26f,
                TargetRefreshInterval = 0.24f,
                IdleDriftTurnSpeed = 15f,
                OrbitAngularSpeed = 0.42f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 9999,
                ReloadDuration = 0f,
                FireRate = 3f,
                Damage = 38,
                DamageType = WeaponDamageType.Explosive,
                DeliveryMethod = WeaponDeliveryMethod.RemoteStrike,
                DeliveryFlags = WeaponDeliveryFlags.AreaDamage | WeaponDeliveryFlags.Delayed,
                BulletScaleMultiplier = 1.8f,
                BulletColor = new Color(1f, 0.55f, 0.18f, 1f),
                BulletSpeed = 18f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 8.5f,
                ShotSoundId = "radar_ship"
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 18f,
                LinearDamping = 0.82f,
                AngularDamping = 1.02f,
                DriftSpeed = 0.08f,
                AngularVelocityRange = 1.3f,
                RewardItemId = InventoryItemCatalog.RadarShipSalvageId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.46f, 0.48f, 0.54f, 0.98f),
                VisualResourcePath = "radar_ship_wreck_resource",
                EditorAssetPath = "Assets/radar_ship_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.46f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[] { new Vector2(0f, 0.02f) },
                MinTrailTime = 0.34f,
                MaxTrailTime = 1.12f,
                MinTrailWidth = 0.05f,
                MaxTrailWidth = 0.2f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.OrangeSmall
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.HunterLance,
            Id = "hunter_lance",
            DisplayName = "Hunter Lance",
            InstantiationMarker = "enemy_bot_hunter_lance",
            VisualResourcePath = "Enemies/HunterLance/hunter_lance_resource",
            EditorAssetPath = "Assets/Resources/Enemies/HunterLance/hunter_lance_resource.png",
            TargetSize = 2.2f,
            PhysicsMass = 12.5f,
            LinearDamping = 0.1f,
            AngularDamping = 0.26f,
            DefaultHp = 85,
            DefaultShield = 115,
            MaxHp = 250,
            MaxShield = 250,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.HunterLance,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.28f,
                TurnResponsiveness = 295f,
                DetectionRadius = 14.5f,
                DisengageRadius = 20f,
                OrbitDistance = 6.8f,
                PreferredDistance = 9.2f,
                ShootDistance = 13.8f,
                RepathInterval = 0.18f,
                TargetRefreshInterval = 0.22f,
                IdleDriftTurnSpeed = 19f,
                OrbitAngularSpeed = 0.58f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 4.6f,
                Damage = 36,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.Beam,
                DeliveryFlags = WeaponDeliveryFlags.ShieldFocused,
                BulletScaleMultiplier = 0f,
                BulletColor = new Color(0.36f, 0.9f, 1f, 1f),
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 13.8f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 12f,
                LinearDamping = 0.78f,
                AngularDamping = 0.98f,
                DriftSpeed = 0.08f,
                AngularVelocityRange = 1.8f,
                RewardItemId = InventoryItemCatalog.HunterLanceCoreId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.18f, 0.2f, 0.24f, 0.98f),
                VisualResourcePath = "Enemies/HunterLance/hunter_lance_wreck_resource",
                EditorAssetPath = "Assets/Resources/Enemies/HunterLance/hunter_lance_wreck_resource.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.56f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.24f, 0.04f),
                    new Vector2(0.24f, 0.04f)
                },
                MinTrailTime = 0.34f,
                MaxTrailTime = 1.08f,
                MinTrailWidth = 0.045f,
                MaxTrailWidth = 0.18f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.BlueTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.PirateFighter,
            Id = "pirate_fighter",
            DisplayName = "Pirate Fighter",
            InstantiationMarker = "enemy_bot_pirate_fighter",
            VisualResourcePath = "pirate_fighter_1_resource",
            EditorAssetPath = "Assets/pirate_fighter_1.png",
            TargetSize = 1.32f,
            PhysicsMass = 5.2f,
            LinearDamping = 0.07f,
            AngularDamping = 0.18f,
            DefaultHp = 50,
            DefaultShield = 50,
            DefaultSpeedMultiplier = 1.5f,
            DefaultEnabled = false,
            DefaultCount = 2,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.PirateFighter,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.65f,
                TurnResponsiveness = 430f,
                DetectionRadius = 6f,
                DisengageRadius = 13f,
                OrbitDistance = 2.7f,
                PreferredDistance = 4.6f,
                ShootDistance = 8.6f,
                RepathInterval = 0.16f,
                TargetRefreshInterval = 0.18f,
                IdleDriftTurnSpeed = 34f,
                OrbitAngularSpeed = 1.55f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 8,
                ReloadDuration = 4f,
                FireRate = 0.12f,
                Damage = 10,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.BurstProjectile,
                DeliveryFlags = WeaponDeliveryFlags.MultiStream,
                BulletScaleMultiplier = 0.42f,
                BulletColor = new Color(0.08f, 0.62f, 1f, 1f),
                BulletSpeed = 18f,
                MuzzleOffsetDistance = 0.44f,
                InfiniteAmmo = false,
                RotateTowardAim = true,
                Range = 8.6f,
                ShotSoundId = "pirate_fighter",
                MuzzleStreamCount = 2
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 4.8f,
                LinearDamping = 0.66f,
                AngularDamping = 0.84f,
                DriftSpeed = 0.11f,
                AngularVelocityRange = 3.6f,
                RewardItemId = InventoryItemCatalog.PirateFighterSalvageId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.42f, 0.34f, 0.26f, 0.98f),
                VisualResourcePath = "pirate_fighter_wreck_resource",
                EditorAssetPath = "Assets/pirate_fighter_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.42f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.38f, 0.04f),
                    new Vector2(0.38f, 0.04f)
                },
                MinTrailTime = 0.22f,
                MaxTrailTime = 0.86f,
                MinTrailWidth = 0.032f,
                MaxTrailWidth = 0.15f,
                EmissionThreshold = 0.025f,
                VisualStyle = EnemyTrailVisualStyle.OrangeRedTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.PirateFighterElite,
            Id = "pirate_fighter_elite",
            DisplayName = "Pirate Fighter Elite",
            InstantiationMarker = "enemy_bot_pirate_fighter_elite",
            VisualResourcePath = "pirate_fighter_elite_resource",
            EditorAssetPath = "Assets/pirate_fighter_elite.png",
            TargetSize = 1.32f,
            PhysicsMass = 5.2f,
            LinearDamping = 0.07f,
            AngularDamping = 0.18f,
            DefaultHp = 66,
            DefaultShield = 66,
            DefaultSpeedMultiplier = 1.5f,
            DefaultEnabled = false,
            DefaultCount = 2,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.PirateFighter,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.65f,
                TurnResponsiveness = 430f,
                DetectionRadius = 6f,
                DisengageRadius = 13f,
                OrbitDistance = 2.7f,
                PreferredDistance = 4.6f,
                ShootDistance = 8.6f,
                RepathInterval = 0.16f,
                TargetRefreshInterval = 0.18f,
                IdleDriftTurnSpeed = 34f,
                OrbitAngularSpeed = 1.55f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 10,
                ReloadDuration = 4f,
                FireRate = 0.12f,
                Damage = 10,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.BurstProjectile,
                DeliveryFlags = WeaponDeliveryFlags.MultiStream,
                BulletScaleMultiplier = 0.42f,
                BulletColor = new Color(1f, 0.08f, 0.03f, 1f),
                BulletSpeed = 18f,
                MuzzleOffsetDistance = 0.44f,
                InfiniteAmmo = false,
                RotateTowardAim = true,
                Range = 8.6f,
                ShotSoundId = "pirate_fighter",
                MuzzleStreamCount = 2
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 4.8f,
                LinearDamping = 0.66f,
                AngularDamping = 0.84f,
                DriftSpeed = 0.11f,
                AngularVelocityRange = 3.6f,
                RewardItemId = InventoryItemCatalog.PirateFighterSalvageId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.42f, 0.34f, 0.26f, 0.98f),
                VisualResourcePath = "pirate_fighter_wreck_resource",
                EditorAssetPath = "Assets/pirate_fighter_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.42f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.38f, 0.04f),
                    new Vector2(0.38f, 0.04f)
                },
                MinTrailTime = 0.22f,
                MaxTrailTime = 0.86f,
                MinTrailWidth = 0.032f,
                MaxTrailWidth = 0.15f,
                EmissionThreshold = 0.025f,
                VisualStyle = EnemyTrailVisualStyle.OrangeRedTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.PirateFighterAce,
            Id = "pirate_fighter_ace",
            DisplayName = "Pirate Fighter Ace",
            InstantiationMarker = "enemy_bot_pirate_fighter_ace",
            VisualResourcePath = "pirate_fighter_ace_resource",
            EditorAssetPath = "Assets/pirate_fighter_ace.png",
            TargetSize = 1.32f,
            PhysicsMass = 5.2f,
            LinearDamping = 0.07f,
            AngularDamping = 0.18f,
            DefaultHp = 66,
            DefaultShield = 66,
            DefaultSpeedMultiplier = 1.5f,
            DefaultEnabled = false,
            DefaultCount = 2,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.PirateFighter,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.65f,
                TurnResponsiveness = 430f,
                DetectionRadius = 6f,
                DisengageRadius = 13f,
                OrbitDistance = 2.7f,
                PreferredDistance = 4.6f,
                ShootDistance = 8.6f,
                RepathInterval = 0.16f,
                TargetRefreshInterval = 0.18f,
                IdleDriftTurnSpeed = 34f,
                OrbitAngularSpeed = 1.55f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 10,
                ReloadDuration = 4f,
                FireRate = 0.12f,
                Damage = 10,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.BurstProjectile,
                DeliveryFlags = WeaponDeliveryFlags.MultiStream,
                BulletScaleMultiplier = 0.42f,
                BulletColor = new Color(1f, 0.08f, 0.03f, 1f),
                BulletSpeed = 18f,
                MuzzleOffsetDistance = 0.44f,
                InfiniteAmmo = false,
                RotateTowardAim = true,
                Range = 8.6f,
                ShotSoundId = "pirate_fighter",
                MuzzleStreamCount = 3
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 4.8f,
                LinearDamping = 0.66f,
                AngularDamping = 0.84f,
                DriftSpeed = 0.11f,
                AngularVelocityRange = 3.6f,
                RewardItemId = InventoryItemCatalog.PirateFighterSalvageId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.42f, 0.34f, 0.26f, 0.98f),
                VisualResourcePath = "pirate_fighter_wreck_resource",
                EditorAssetPath = "Assets/pirate_fighter_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.42f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.38f, 0.04f),
                    new Vector2(0.38f, 0.04f)
                },
                MinTrailTime = 0.22f,
                MaxTrailTime = 0.86f,
                MinTrailWidth = 0.032f,
                MaxTrailWidth = 0.15f,
                EmissionThreshold = 0.025f,
                VisualStyle = EnemyTrailVisualStyle.OrangeRedTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.SpaceManta,
            Id = "space_manta",
            DisplayName = "Space Manta",
            InstantiationMarker = "enemy_bot_space_manta",
            VisualResourcePath = "Enemies/SpaceManta/space_manta_flap_00",
            AnimationResourcePath = "Enemies/SpaceManta",
            EditorAssetPath = "Assets/Resources/Enemies/SpaceManta/space_manta_flap_00.png",
            AnimationFramesPerSecond = 7f,
            TargetSize = 2.42f,
            PhysicsMass = 9.2f,
            LinearDamping = 0.09f,
            AngularDamping = 0.2f,
            DefaultHp = 100,
            DefaultShield = 0,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.SpaceManta,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.32f,
                TurnResponsiveness = 330f,
                DetectionRadius = 13.5f,
                DisengageRadius = 20f,
                OrbitDistance = 3.3f,
                PreferredDistance = 5.8f,
                ShootDistance = 8.8f,
                RepathInterval = 0.22f,
                TargetRefreshInterval = 0.24f,
                IdleDriftTurnSpeed = 22f,
                OrbitAngularSpeed = 0.86f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 3.4f,
                Damage = 40,
                DamageType = WeaponDamageType.Kinetic,
                DeliveryMethod = WeaponDeliveryMethod.ContactDash,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 0f,
                BulletColor = new Color(0.3f, 0.88f, 1f, 1f),
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 8.8f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 7.5f,
                LinearDamping = 0.74f,
                AngularDamping = 0.94f,
                DriftSpeed = 0.08f,
                AngularVelocityRange = 2.4f,
                RewardItemId = InventoryItemCatalog.SpaceAnimalRemainsId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.26f, 0.28f, 0.36f, 0.96f),
                VisualResourcePath = "Enemies/SpaceManta/space_manta_wreck_resource",
                EditorAssetPath = "Assets/Resources/Enemies/SpaceManta/space_manta_wreck_resource.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.34f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.34f, 0.04f),
                    new Vector2(0.34f, 0.04f)
                },
                MinTrailTime = 0.3f,
                MaxTrailTime = 0.96f,
                MinTrailWidth = 0.04f,
                MaxTrailWidth = 0.18f,
                EmissionThreshold = 0.035f,
                VisualStyle = EnemyTrailVisualStyle.BlueTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.GravitySquid,
            Id = "gravity_squid",
            DisplayName = "Gravity Squid",
            InstantiationMarker = "enemy_bot_gravity_squid",
            VisualResourcePath = "Enemies/GravitySquid/gravity_squid_flap_00",
            AnimationResourcePath = "Enemies/GravitySquid",
            EditorAssetPath = "Assets/Resources/Enemies/GravitySquid/gravity_squid_flap_00.png",
            AnimationFramesPerSecond = 6.3f,
            TargetSize = 2.68f,
            PhysicsMass = 10.5f,
            LinearDamping = 0.12f,
            AngularDamping = 0.24f,
            DefaultHp = 80,
            DefaultShield = 70,
            MaxHp = 200,
            MaxShield = 200,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.GravitySquid,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.15f,
                TurnResponsiveness = 260f,
                DetectionRadius = 13.2f,
                DisengageRadius = 21f,
                OrbitDistance = 5.1f,
                PreferredDistance = 8.2f,
                ShootDistance = 10.6f,
                RepathInterval = 0.22f,
                TargetRefreshInterval = 0.24f,
                IdleDriftTurnSpeed = 18f,
                OrbitAngularSpeed = 0.74f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 7f,
                Damage = 8,
                DamageType = WeaponDamageType.Gravitic,
                DeliveryMethod = WeaponDeliveryMethod.Tether,
                DeliveryFlags = WeaponDeliveryFlags.Continuous | WeaponDeliveryFlags.ShieldFocused,
                BulletScaleMultiplier = 0f,
                BulletColor = new Color(0.12f, 0.96f, 1f, 1f),
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 10.6f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 8.6f,
                LinearDamping = 0.82f,
                AngularDamping = 0.96f,
                DriftSpeed = 0.06f,
                AngularVelocityRange = 2.1f,
                RewardItemId = InventoryItemCatalog.SpaceAnimalRemainsId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.16f, 0.12f, 0.28f, 0.96f),
                VisualResourcePath = "Enemies/GravitySquid/gravity_squid_wreck_resource",
                EditorAssetPath = "Assets/Resources/Enemies/GravitySquid/gravity_squid_wreck_resource.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.28f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.22f, -0.02f),
                    new Vector2(0.22f, -0.02f)
                },
                MinTrailTime = 0.26f,
                MaxTrailTime = 0.78f,
                MinTrailWidth = 0.035f,
                MaxTrailWidth = 0.14f,
                EmissionThreshold = 0.03f,
                VisualStyle = EnemyTrailVisualStyle.PurpleLarge
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.PirateBase,
            Id = "pirate_base",
            DisplayName = "Pirate Base",
            InstantiationMarker = "enemy_bot_pirate_base",
            VisualResourcePath = "pirate_base_resource",
            EditorAssetPath = "Assets/pirate_base.png",
            TargetSize = 7.4f,
            PhysicsMass = 110f,
            LinearDamping = 0.12f,
            AngularDamping = 1.2f,
            DefaultHp = 500,
            DefaultShield = 1000,
            MaxHp = 500,
            MaxShield = 1000,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.PirateBase,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 0.42f,
                TurnResponsiveness = 0f,
                DetectionRadius = 0f,
                DisengageRadius = 0f,
                PreferredDistance = 0f,
                RepathInterval = 0.42f,
                TargetRefreshInterval = 0.55f,
                IdleDriftTurnSpeed = 0f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                DamageType = WeaponDamageType.None,
                DeliveryMethod = WeaponDeliveryMethod.Spawner,
                DeliveryFlags = WeaponDeliveryFlags.Autonomous,
                BulletScaleMultiplier = 0f,
                BulletColor = Color.white,
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 120f,
                LinearDamping = 0.95f,
                AngularDamping = 1.3f,
                DriftSpeed = 0.04f,
                AngularVelocityRange = 0.55f,
                RewardItemId = InventoryItemCatalog.PirateBaseCoreId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.36f, 0.33f, 0.38f, 0.98f),
                VisualResourcePath = "pirate_base_wreck_resource",
                EditorAssetPath = "Assets/pirate_base_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.48f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.38f, 0.04f),
                    new Vector2(0f, 0.02f),
                    new Vector2(0.38f, 0.04f)
                },
                MinTrailTime = 0.72f,
                MaxTrailTime = 1.85f,
                MinTrailWidth = 0.08f,
                MaxTrailWidth = 0.34f,
                EmissionThreshold = 0.01f,
                VisualStyle = EnemyTrailVisualStyle.PurpleLarge
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.RescueShip,
            Id = "rescue_ship",
            DisplayName = "Rescue Ship",
            InstantiationMarker = "enemy_bot_rescue_ship",
            VisualResourcePath = "rescue_ship_resource",
            EditorAssetPath = "Assets/rescue_ship.png",
            TargetSize = 2.18f,
            PhysicsMass = 20f,
            LinearDamping = 0.1f,
            AngularDamping = 0.3f,
            DefaultHp = 85,
            DefaultShield = 95,
            DefaultSpeedMultiplier = 1.9f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.RescueShip,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 0.96f,
                TurnResponsiveness = 155f,
                DetectionRadius = 18f,
                DisengageRadius = 22f,
                OrbitDistance = 2.6f,
                PreferredDistance = 3.1f,
                ShootDistance = 0f,
                RepathInterval = 0.18f,
                TargetRefreshInterval = 0.2f,
                IdleDriftTurnSpeed = 14f,
                OrbitAngularSpeed = 0.25f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                DamageType = WeaponDamageType.None,
                DeliveryMethod = WeaponDeliveryMethod.None,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 0f,
                BulletColor = new Color(0.54f, 0.9f, 1f, 1f),
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 22f,
                LinearDamping = 0.84f,
                AngularDamping = 1.08f,
                DriftSpeed = 0.07f,
                AngularVelocityRange = 1.15f,
                RewardItemId = InventoryItemCatalog.RescueShipSalvageId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.48f, 0.54f, 0.6f, 0.98f),
                VisualResourcePath = "rescue_ship_wreck_resource",
                EditorAssetPath = "Assets/rescue_ship_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.44f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.34f, 0.06f),
                    new Vector2(0.34f, 0.06f)
                },
                MinTrailTime = 0.42f,
                MaxTrailTime = 1.18f,
                MinTrailWidth = 0.05f,
                MaxTrailWidth = 0.18f,
                EmissionThreshold = 0.015f,
                VisualStyle = EnemyTrailVisualStyle.BlueTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.Mothership,
            Id = "mothership",
            DisplayName = "Mothership",
            InstantiationMarker = "enemy_bot_mothership",
            VisualResourcePath = "mother_ship_resource",
            EditorAssetPath = "Assets/mother_ship.png",
            TargetSize = 7.28f,
            PhysicsMass = 95f,
            LinearDamping = 0.08f,
            AngularDamping = 0.42f,
            DefaultHp = 200,
            DefaultShield = 200,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.Mothership,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 0.82f,
                TurnResponsiveness = 28f,
                DetectionRadius = 13.5f,
                DisengageRadius = 22f,
                PreferredDistance = 6.8f,
                RepathInterval = 0.45f,
                TargetRefreshInterval = 0.35f,
                OrbitRadiusFactor = 0.38f,
                OrbitAngularSpeed = 0.18f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 10,
                ReloadDuration = 3f,
                FireRate = 0.28f,
                Damage = 10,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.BurstProjectile,
                DeliveryFlags = WeaponDeliveryFlags.MultiStream,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 10f,
                MuzzleOffsetDistance = 0.38f,
                InfiniteAmmo = false,
                RotateTowardAim = false,
                Range = 18f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 120f,
                LinearDamping = 0.94f,
                AngularDamping = 1.4f,
                DriftSpeed = 0.045f,
                AngularVelocityRange = 0.7f,
                RewardItemId = InventoryItemCatalog.MothershipCoreId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.52f, 0.55f, 0.58f, 0.98f),
                VisualResourcePath = "mother_ship_wrak_resource",
                EditorAssetPath = "Assets/mother_ship_wrak.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(-0.6f, 0f),
                RootRotationZ = 0f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(0f, -0.54f),
                    new Vector2(0f, -0.27f),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0.27f),
                    new Vector2(0f, 0.54f)
                },
                MinTrailTime = 3.1f,
                MaxTrailTime = 6.2f,
                MinTrailWidth = 0.56f,
                MaxTrailWidth = 1.35f,
                EmissionThreshold = 0f,
                VisualStyle = EnemyTrailVisualStyle.RedLarge
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.CosmicWorm,
            Id = "cosmic_worm",
            DisplayName = "Cosmic Worm",
            InstantiationMarker = "enemy_bot_cosmic_worm",
            VisualResourcePath = "Enemies/CosmicWorm/cosmic_worm_resource",
            EditorAssetPath = "Assets/Resources/Enemies/CosmicWorm/cosmic_worm_resource.png",
            TargetSize = 11.8f,
            PhysicsMass = 140f,
            LinearDamping = 0.12f,
            AngularDamping = 0.52f,
            DefaultHp = 1600,
            DefaultShield = 260,
            MaxHp = 3200,
            MaxShield = 900,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            ShowInEnemySettings = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 25,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.CosmicWorm,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 0.72f,
                TurnResponsiveness = 44f,
                DetectionRadius = 24f,
                DisengageRadius = 36f,
                OrbitDistance = 8.5f,
                PreferredDistance = 11.5f,
                ShootDistance = 18f,
                RepathInterval = 0.38f,
                TargetRefreshInterval = 0.32f,
                OrbitRadiusFactor = 0.44f,
                OrbitAngularSpeed = 0.2f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 9999,
                ReloadDuration = 0f,
                FireRate = 0.18f,
                Damage = 18,
                DamageType = WeaponDamageType.Plasma,
                DeliveryMethod = WeaponDeliveryMethod.SpreadProjectile,
                DeliveryFlags = WeaponDeliveryFlags.MultiStream,
                BulletScaleMultiplier = 1.75f,
                BulletColor = new Color(0.55f, 0.18f, 1f, 1f),
                BulletSpeed = 8.6f,
                MuzzleOffsetDistance = 0.54f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 18f,
                ShotSoundId = "cosmic_worm",
                MuzzleStreamCount = 5
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 155f,
                LinearDamping = 1.08f,
                AngularDamping = 1.42f,
                DriftSpeed = 0.035f,
                AngularVelocityRange = 0.45f,
                RewardItemId = InventoryItemCatalog.VoidMawCoreId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.32f, 0.28f, 0.38f, 0.98f),
                VisualResourcePath = "Enemies/CosmicWorm/cosmic_worm_resource",
                EditorAssetPath = "Assets/Resources/Enemies/CosmicWorm/cosmic_worm_resource.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(-0.48f, 0f),
                RootRotationZ = 0f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.58f, -0.16f),
                    new Vector2(-0.52f, 0.16f),
                    new Vector2(-0.28f, 0f)
                },
                MinTrailTime = 1.8f,
                MaxTrailTime = 3.8f,
                MinTrailWidth = 0.26f,
                MaxTrailWidth = 0.72f,
                EmissionThreshold = 0f,
                VisualStyle = EnemyTrailVisualStyle.PurpleLarge
            }
        }
    };

    static readonly System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition> DefinitionsByKind = BuildDefinitionsByKind();
    static readonly System.Collections.Generic.Dictionary<string, EnemyBotDefinition> DefinitionsByMarker = BuildDefinitionsByMarker();

    public static System.Collections.Generic.IReadOnlyList<EnemyBotDefinition> AllDefinitions => Definitions;

    public static void PrewarmRoundAssets()
    {
        for (int i = 0; i < Definitions.Length; i++)
            PrewarmDefinition(Definitions[i]);

        EnemyContainerShipBehavior.PrewarmCargoSprites();
        EnemyMothershipBehavior.PrewarmTurretAssets();

        RescueShipBeamVfx.Prewarm();
        PirateBaseCollectionBeamVfx.Prewarm();
        GravitySquidTetherVfx.Prewarm();
        HunterLanceBeamVfx.Prewarm();
        SpaceAnimalDeathVfx.Prewarm();
        PirateBaseLaunchVfx.Prewarm();
    }

    public static EnemyBotDefinition GetDefinition(EnemyBotKind kind)
    {
        DefinitionsByKind.TryGetValue(kind, out EnemyBotDefinition definition);
        return definition;
    }

    public static EnemyBotDefinition GetDefinition(string marker)
    {
        if (string.IsNullOrWhiteSpace(marker))
            return null;

        DefinitionsByMarker.TryGetValue(marker, out EnemyBotDefinition definition);
        return definition;
    }

    static System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition> BuildDefinitionsByKind()
    {
        System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition> result = new System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition>();
        for (int i = 0; i < Definitions.Length; i++)
            result[Definitions[i].Kind] = Definitions[i];

        return result;
    }

    static System.Collections.Generic.Dictionary<string, EnemyBotDefinition> BuildDefinitionsByMarker()
    {
        System.Collections.Generic.Dictionary<string, EnemyBotDefinition> result = new System.Collections.Generic.Dictionary<string, EnemyBotDefinition>(System.StringComparer.Ordinal);
        for (int i = 0; i < Definitions.Length; i++)
            result[Definitions[i].InstantiationMarker] = Definitions[i];

        return result;
    }

    static void PrewarmDefinition(EnemyBotDefinition definition)
    {
        if (definition == null)
            return;

        PrewarmSpriteTexture(definition.GetVisualSprite());
        if (!string.IsNullOrWhiteSpace(definition.AnimationResourcePath))
            PrewarmSprites(EnemySpriteFrameAnimator.PrewarmFrames(definition.AnimationResourcePath));

        if (definition.Wreck != null)
            PrewarmSpriteTexture(definition.Wreck.GetVisualSprite());

        if (definition.Explosion != null)
            PrewarmSprites(definition.Explosion.GetVisualFrames());
    }

    static void PrewarmSprites(Sprite[] sprites)
    {
        if (sprites == null)
            return;

        for (int i = 0; i < sprites.Length; i++)
            PrewarmSpriteTexture(sprites[i]);
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }
}

public sealed class EnemySpriteFrameAnimator : MonoBehaviour
{
    static readonly System.Collections.Generic.Dictionary<string, Sprite[]> FrameCacheByResourcePath = new System.Collections.Generic.Dictionary<string, Sprite[]>(System.StringComparer.Ordinal);

    SpriteRenderer targetRenderer;
    Sprite[] frames = System.Array.Empty<Sprite>();
    string loadedResourcePath;
    float framesPerSecond = 7f;
    float speedMultiplier = 1f;
    float frameCursor;

    public void Configure(SpriteRenderer renderer, string resourcePath, float fps)
    {
        targetRenderer = renderer;
        framesPerSecond = Mathf.Max(0.1f, fps);

        if (!string.Equals(loadedResourcePath, resourcePath, System.StringComparison.Ordinal))
        {
            loadedResourcePath = resourcePath;
            frames = LoadFrames(resourcePath);
            frameCursor = frames.Length > 0 ? Random.Range(0f, frames.Length) : 0f;
        }
    }

    public void SetSpeedMultiplier(float value)
    {
        speedMultiplier = Mathf.Clamp(value, 0.15f, 3.5f);
    }

    public static Sprite[] PrewarmFrames(string resourcePath)
    {
        return LoadFrames(resourcePath);
    }

    void LateUpdate()
    {
        if (targetRenderer == null || frames == null || frames.Length == 0)
            return;

        frameCursor += Time.deltaTime * framesPerSecond * speedMultiplier;
        int frameIndex = Mathf.FloorToInt(frameCursor) % frames.Length;
        if (frameIndex < 0)
            frameIndex += frames.Length;

        targetRenderer.sprite = frames[frameIndex];
    }

    static Sprite[] LoadFrames(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return System.Array.Empty<Sprite>();

        if (FrameCacheByResourcePath.TryGetValue(resourcePath, out Sprite[] cachedFrames))
            return cachedFrames;

        Sprite[] allSprites = Resources.LoadAll<Sprite>(resourcePath);
        System.Collections.Generic.List<Sprite> candidates = new System.Collections.Generic.List<Sprite>();
        System.Collections.Generic.List<Sprite> flapCandidates = new System.Collections.Generic.List<Sprite>();
        if (allSprites != null)
        {
            for (int i = 0; i < allSprites.Length; i++)
                AddAnimationCandidate(allSprites[i], candidates, flapCandidates);
        }

        if (candidates.Count == 0)
        {
            Texture2D[] textures = Resources.LoadAll<Texture2D>(resourcePath);
            if (textures != null)
            {
                for (int i = 0; i < textures.Length; i++)
                {
                    Sprite sprite = CreateSpriteFromTexture(textures[i]);
                    AddAnimationCandidate(sprite, candidates, flapCandidates);
                }
            }
        }

        System.Collections.Generic.List<Sprite> selected = flapCandidates.Count > 0 ? flapCandidates : candidates;
        selected.Sort(CompareSpritesForAnimation);
        Sprite[] frames = selected.ToArray();
        for (int i = 0; i < frames.Length; i++)
            PrewarmSpriteTexture(frames[i]);

        FrameCacheByResourcePath[resourcePath] = frames;
        return frames;
    }

    static void AddAnimationCandidate(Sprite sprite, System.Collections.Generic.List<Sprite> candidates, System.Collections.Generic.List<Sprite> flapCandidates)
    {
        if (sprite == null)
            return;

        string name = sprite.name ?? string.Empty;
        if (name.IndexOf("wreck", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        candidates.Add(sprite);
        if (name.IndexOf("flap", System.StringComparison.OrdinalIgnoreCase) >= 0)
            flapCandidates.Add(sprite);
    }

    static Sprite CreateSpriteFromTexture(Texture2D texture)
    {
        if (texture == null)
            return null;

        float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);
        sprite.name = texture.name;
        return sprite;
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }

    static int CompareSpritesForAnimation(Sprite a, Sprite b)
    {
        int indexA = ExtractTrailingNumber(a != null ? a.name : string.Empty);
        int indexB = ExtractTrailingNumber(b != null ? b.name : string.Empty);
        if (indexA >= 0 && indexB >= 0 && indexA != indexB)
            return indexA.CompareTo(indexB);

        return string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty);
    }

    static int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
            return -1;

        int endIndex = value.Length - 1;
        while (endIndex >= 0 && !char.IsDigit(value[endIndex]))
            endIndex--;

        if (endIndex < 0)
            return -1;

        int startIndex = endIndex;
        while (startIndex >= 0 && char.IsDigit(value[startIndex]))
            startIndex--;

        string numberPart = value.Substring(startIndex + 1, endIndex - startIndex);
        return int.TryParse(numberPart, out int number) ? number : -1;
    }
}

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBot : MonoBehaviourPun
{
    const string PlayerPlacedMineMarker = "player_gadget_mine";
    public const string ContainerShipMineMarker = "container_ship_mine";
    const string SummonedDroneMarker = "space_truck_summoned_drone";
    public const string PirateBaseLaunchedFighterMarker = "pirate_base_launched_fighter";
    const float PirateBaseLaunchAnimationDuration = 3f;
    const float PirateBaseLaunchStartScale = 0.3f;
    const float PirateBaseLaunchExitDistance = 1.15f;

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyBotBehaviorBase behavior;
    SpriteRenderer cachedRenderer;
    EnemyBotKind kind;
    bool hasInitialized;
    bool hasAppliedStats;
    bool hasDetonated;
    bool spawnTeleportVfxPlayed;
    bool isPlayerPlacedMine;
    bool isOwnedMine;
    bool isSummonedDrone;
    bool isPirateBaseLaunchedFighter;
    bool spaceTruckFirstHitHandled;
    bool spaceTruckHalfHpHandled;
    bool pirateBaseLaunchProtected;
    bool pirateBaseLaunchBodyStateStored;
    bool pirateBaseLaunchColliderStateStored;
    float forcedSpeedMultiplier;
    float forcedSpeedMultiplierUntil;
    float visualScaleMultiplier = 1f;
    float pirateBaseLaunchStartedAt;
    RigidbodyType2D pirateBaseLaunchPreviousBodyType;
    bool pirateBaseLaunchPreviousSimulated;
    Collider2D[] pirateBaseLaunchColliders;
    bool[] pirateBaseLaunchColliderStates;
    Vector3 pirateBaseLaunchStartPosition;
    Vector3 pirateBaseLaunchEndPosition;
    int mineOwnerViewId;
    int containerShipCargoVariantIndex;
    int pirateBaseLaunchTargetViewId;
    int pirateBaseLaunchSourceViewId;
    int cosmicWormSwallowZoomToken;
    float confusedUntil;
    float nextConfusedDirectionAt;
    float nextConfusedShotAt;
    Vector2 confusedMoveDirection = Vector2.up;

    public EnemyBotKind Kind => kind;
    public EnemyBotDefinition Definition => EnemyBotCatalog.GetDefinition(kind);
    public bool IsCorsair => kind == EnemyBotKind.Corsair;
    public bool IsSpaceMine => kind == EnemyBotKind.SpaceMine;
    public bool IsSpaceTruck => kind == EnemyBotKind.SpaceTruck;
    public bool IsRadarShip => kind == EnemyBotKind.RadarShip;
    public bool IsRescueShip => kind == EnemyBotKind.RescueShip;
    public bool IsPirateFighter => IsPirateFighterKind(kind);
    public bool IsMothership => kind == EnemyBotKind.Mothership;
    public bool IsPirateBase => kind == EnemyBotKind.PirateBase;
    public bool IsCosmicWorm => kind == EnemyBotKind.CosmicWorm;
    public bool IsPlayerPlacedMine => isPlayerPlacedMine;
    public bool IsOwnedMine => isOwnedMine;
    public bool IsSummonedDrone => isSummonedDrone;
    public bool IsPirateBaseLaunchedFighter => isPirateBaseLaunchedFighter;
    public bool IsPirateBaseLaunchProtected => pirateBaseLaunchProtected;
    public int MineOwnerViewId => mineOwnerViewId;
    public int ContainerShipCargoVariantIndex => Mathf.Clamp(containerShipCargoVariantIndex, 0, InventoryItemCatalog.BlueprintScrapContainerVariantCount - 1);
    public int PirateBaseLaunchTargetViewId => pirateBaseLaunchTargetViewId;
    public int PirateBaseLaunchSourceViewId => pirateBaseLaunchSourceViewId;
    public bool IsConfused => Time.time < confusedUntil;
    public float VisualTargetSize => Definition != null ? Definition.TargetSize : 1.04f;
    public float EffectiveMoveSpeed => Definition != null && Definition.Movement != null
        ? Definition.Movement.MoveSpeed * EffectiveSpeedMultiplier * NebulaSpeedMultiplier * ElectromagneticShockStatus.GetSpeedMultiplier(gameObject) * AtlasSuppressionStatus.GetSpeedMultiplier(gameObject)
        : 1f;
    public float EffectiveSpeedMultiplier => forcedSpeedMultiplier > 0f && (forcedSpeedMultiplierUntil <= 0f || Time.time < forcedSpeedMultiplierUntil)
        ? forcedSpeedMultiplier
        : RoomSettings.GetEnemySpeedMultiplier(kind);
    float NebulaSpeedMultiplier
    {
        get
        {
            HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>();
            return nebulaTarget != null ? nebulaTarget.CurrentNebulaSpeedMultiplier : 1f;
        }
    }

    public static bool IsPlayerControlledDamageSource(int attackerViewID)
    {
        if (attackerViewID <= 0)
            return false;

        PhotonView attackerView = PhotonView.Find(attackerViewID);
        if (attackerView == null)
            return false;

        PlayerHealth attackerHealth = attackerView.GetComponent<PlayerHealth>();
        if (attackerHealth != null &&
            !attackerHealth.IsBotControlled &&
            !attackerHealth.IsNeutralRiderControlled &&
            !attackerHealth.IsAstronautControlled &&
            attackerHealth.GetComponent<PlayerDeployableBase>() == null &&
            attackerHealth.GetComponent<LureBeaconDecoy>() == null)
        {
            return true;
        }

        PlayerDeployableBase deployable = attackerView.GetComponent<PlayerDeployableBase>();
        if (deployable != null)
            return deployable.OwnerShipViewId != attackerViewID &&
                   IsPlayerControlledDamageSource(deployable.OwnerShipViewId);

        EnemyBot attackerBot = attackerView.GetComponent<EnemyBot>();
        return attackerBot != null && attackerBot.Kind == EnemyBotKind.SpaceMine && attackerBot.IsPlayerPlacedMine;
    }

    public static bool IsPirateFighterKind(EnemyBotKind candidate)
    {
        return candidate == EnemyBotKind.PirateFighter ||
               candidate == EnemyBotKind.PirateFighterElite ||
               candidate == EnemyBotKind.PirateFighterAce;
    }

    public static bool IsSpaceAnimalKind(EnemyBotKind candidate)
    {
        return candidate == EnemyBotKind.SpaceManta ||
               candidate == EnemyBotKind.GravitySquid;
    }

    public void ActivateTemporarySpeedMultiplier(float multiplier, float duration)
    {
        forcedSpeedMultiplier = Mathf.Max(0f, multiplier);
        forcedSpeedMultiplierUntil = duration > 0f ? Time.time + duration : 0f;
    }

    public static bool IsBotObject(GameObject target)
    {
        return target != null && target.GetComponent<EnemyBot>() != null;
    }

    public static bool IsBotView(PhotonView targetView)
    {
        return targetView != null && targetView.GetComponent<EnemyBot>() != null;
    }

    public bool CanReceivePilotHostileEffect()
    {
        if (!hasInitialized)
            InitializeFromPhotonData();

        return health != null &&
               !health.IsWreck &&
               kind != EnemyBotKind.SpaceMine &&
               kind != EnemyBotKind.RescueShip &&
               !isOwnedMine &&
               !isSummonedDrone;
    }

    public static bool IsBotInstantiationData(object[] data)
    {
        return GetDefinitionFromInstantiationData(data) != null;
    }

    public static EnemyBotKind GetKindFromInstantiationData(object[] data)
    {
        EnemyBotDefinition definition = GetDefinitionFromInstantiationData(data);
        return definition != null ? definition.Kind : EnemyBotKind.Drone;
    }

    static EnemyBotDefinition GetDefinitionFromInstantiationData(object[] data)
    {
        if (data == null ||
            data.Length == 0 ||
            !(data[0] is string marker))
        {
            return null;
        }

        return EnemyBotCatalog.GetDefinition(marker);
    }

    public void InitializeFromPhotonData()
    {
        if (hasInitialized)
            return;

        view = GetComponent<PhotonView>();
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
        kind = GetKindFromInstantiationData(view != null ? view.InstantiationData : null);
        ResolveSpecialMineOwner(view != null ? view.InstantiationData : null);
        ResolveContainerShipCargoVariant(view != null ? view.InstantiationData : null);
        ResolveSummonedDrone(view != null ? view.InstantiationData : null);
        ResolvePirateBaseLaunchedFighter(view != null ? view.InstantiationData : null);

        DisablePlayerOnlySystems();
        EnsureBehavior();
        ApplyBotVisuals();
        EnsureAnimatedVisual();
        PlaySpawnTeleportVfx();
        ConfigurePhysics();
        ConfigureColliderToVisual();
        ApplyMineOwnerCollisionIgnore();
        if (GetComponent<EnemyBotHealthBarUI>() == null)
        {
            gameObject.AddComponent<EnemyBotHealthBarUI>();
        }
        if (!hasAppliedStats)
        {
            ApplyBotStats();
            hasAppliedStats = true;
        }
        if (isPirateBaseLaunchedFighter)
            BeginPirateBaseLaunchAnimation();

        hasInitialized = true;
    }

    void PlaySpawnTeleportVfx()
    {
        if (spawnTeleportVfxPlayed || !IsGameStarted() || isOwnedMine || isPirateBaseLaunchedFighter || kind == EnemyBotKind.RescueShip)
            return;

        spawnTeleportVfxPlayed = true;
        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        float radius = VisualTargetSize * 0.62f;
        if (cachedRenderer != null)
            radius = Mathf.Max(radius, Mathf.Max(cachedRenderer.bounds.size.x, cachedRenderer.bounds.size.y) * 0.58f);

        EnemySpawnTeleportVfx.Spawn(transform.position, cachedRenderer, radius);
    }

    void Awake()
    {
        InitializeFromPhotonData();
    }

    void Start()
    {
        InitializeFromPhotonData();
    }

    void Update()
    {
        EnsureStableVisuals();
        UpdatePirateBaseLaunchAnimation();
        if (isOwnedMine)
            ApplyMineOwnerCollisionIgnore();

        if (!view.IsMine || !IsGameStarted() || health == null || health.IsWreck)
            return;

        if (behavior == null)
            EnsureBehavior();
    }

    void FixedUpdate()
    {
        if (!view.IsMine || !IsGameStarted() || health == null || health.IsWreck)
            return;

        if (!hasInitialized)
            InitializeFromPhotonData();

        if (behavior == null)
            EnsureBehavior();

        if (pirateBaseLaunchProtected)
        {
            UpdatePirateBaseLaunchAnimation();
            return;
        }

        if (IsConfused)
        {
            ApplyConfusedBehavior();
            return;
        }

        behavior?.TickBehavior();
    }

    [PunRPC]
    public void ApplyConfusionRpc(float duration)
    {
        if (!CanReceivePilotHostileEffect() || duration <= 0f)
            return;

        confusedUntil = Mathf.Max(confusedUntil, Time.time + duration);
        nextConfusedDirectionAt = 0f;
        nextConfusedShotAt = 0f;
    }

    void ApplyConfusedBehavior()
    {
        if (rb == null)
            return;

        if (Time.time >= nextConfusedDirectionAt || confusedMoveDirection.sqrMagnitude <= 0.001f)
        {
            confusedMoveDirection = Random.insideUnitCircle;
            if (confusedMoveDirection.sqrMagnitude <= 0.001f)
                confusedMoveDirection = Vector2.up;

            confusedMoveDirection.Normalize();
            nextConfusedDirectionAt = Time.time + Random.Range(0.35f, 0.95f);
        }

        float speed = EffectiveMoveSpeed * Random.Range(0.45f, 1.1f);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, confusedMoveDirection * speed, 0.22f);
        float targetAngle = Mathf.Atan2(confusedMoveDirection.y, confusedMoveDirection.x) * Mathf.Rad2Deg - 90f;
        rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, targetAngle, 240f * Time.fixedDeltaTime));

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null && Time.time >= nextConfusedShotAt)
        {
            Vector2 shotDirection = Random.insideUnitCircle;
            if (shotDirection.sqrMagnitude <= 0.001f)
                shotDirection = confusedMoveDirection;

            shooting.TryFireBot(shotDirection.normalized);
            nextConfusedShotAt = Time.time + Random.Range(0.22f, 0.75f);
        }
    }

    void ConfigurePhysics()
    {
        if (rb == null || Definition == null)
            return;

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.mass = Definition.PhysicsMass;
        rb.linearDamping = Definition.LinearDamping;
        rb.angularDamping = Definition.AngularDamping;
    }

    void ConfigureColliderToVisual()
    {
        if (kind != EnemyBotKind.Mothership && kind != EnemyBotKind.PirateBase && kind != EnemyBotKind.SpaceManta && kind != EnemyBotKind.GravitySquid && kind != EnemyBotKind.HunterLance && kind != EnemyBotKind.ContainerShip && kind != EnemyBotKind.CosmicWorm)
            return;

        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        if (cachedRenderer == null)
            return;

        Bounds bounds = cachedRenderer.bounds;
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            Vector2 boxScale = kind == EnemyBotKind.SpaceManta
                ? new Vector2(bounds.size.x * 0.68f, bounds.size.y * 0.46f)
                : kind == EnemyBotKind.GravitySquid
                    ? new Vector2(bounds.size.x * 0.56f, bounds.size.y * 0.68f)
                : kind == EnemyBotKind.HunterLance
                    ? new Vector2(bounds.size.x * 0.48f, bounds.size.y * 0.84f)
                : kind == EnemyBotKind.PirateBase
                    ? new Vector2(bounds.size.x * 0.78f, bounds.size.y * 0.7f)
                : kind == EnemyBotKind.ContainerShip
                    ? new Vector2(bounds.size.x * 0.78f, bounds.size.y * 0.58f)
                : kind == EnemyBotKind.CosmicWorm
                    ? new Vector2(bounds.size.x * 0.74f, bounds.size.y * 0.5f)
                    : new Vector2(bounds.size.x * 0.82f, bounds.size.y * 0.72f);
            SetWorldBoxSize(boxCollider, boxScale);
        }

        CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
        if (circleCollider != null)
            circleCollider.enabled = false;
    }

    void SetWorldBoxSize(BoxCollider2D collider2D, Vector2 worldSize)
    {
        Vector3 scale = collider2D.transform.lossyScale;
        float safeX = Mathf.Abs(scale.x) > 0.0001f ? Mathf.Abs(scale.x) : 1f;
        float safeY = Mathf.Abs(scale.y) > 0.0001f ? Mathf.Abs(scale.y) : 1f;
        collider2D.size = new Vector2(worldSize.x / safeX, worldSize.y / safeY);
        collider2D.offset = Vector2.zero;
    }

    void ApplyBotStats()
    {
        if (health == null || Definition == null)
            return;

        health.ConfigureBaseStats(RoomSettings.GetEnemyHp(kind), RoomSettings.GetEnemyShield(kind));
    }

    void EnsureBehavior()
    {
        behavior = GetComponent<EnemyBotBehaviorBase>();
        if (behavior == null || !IsMatchingBehavior(behavior))
        {
            if (behavior != null)
                Destroy(behavior);

            if (Definition != null && Definition.Movement != null)
            {
                switch (Definition.Movement.Model)
                {
                    case EnemyMovementModel.OrbitMap:
                        behavior = gameObject.AddComponent<EnemyCorsairBehavior>();
                        break;
                    case EnemyMovementModel.Drift:
                        behavior = gameObject.AddComponent<EnemyMineBehavior>();
                        break;
                    case EnemyMovementModel.RouteExtractionZones:
                        behavior = gameObject.AddComponent<EnemySpaceTruckBehavior>();
                        break;
                    case EnemyMovementModel.RadarShip:
                        behavior = gameObject.AddComponent<EnemyRadarShipBehavior>();
                        break;
                    case EnemyMovementModel.RescueShip:
                        behavior = gameObject.AddComponent<EnemyRescueShipBehavior>();
                        break;
                    case EnemyMovementModel.PirateFighter:
                        behavior = gameObject.AddComponent<EnemyPirateFighterBehavior>();
                        break;
                    case EnemyMovementModel.PirateBase:
                        behavior = gameObject.AddComponent<EnemyPirateBaseBehavior>();
                        break;
                    case EnemyMovementModel.SpaceManta:
                        behavior = gameObject.AddComponent<EnemySpaceMantaBehavior>();
                        break;
                    case EnemyMovementModel.GravitySquid:
                        behavior = gameObject.AddComponent<EnemyGravitySquidBehavior>();
                        break;
                    case EnemyMovementModel.HunterLance:
                        behavior = gameObject.AddComponent<EnemyHunterLanceBehavior>();
                        break;
                    case EnemyMovementModel.ContainerShip:
                        behavior = gameObject.AddComponent<EnemyContainerShipBehavior>();
                        break;
                    case EnemyMovementModel.CosmicWorm:
                        behavior = gameObject.AddComponent<EnemyCosmicWormBehavior>();
                        break;
                    case EnemyMovementModel.Mothership:
                        behavior = gameObject.AddComponent<EnemyMothershipBehavior>();
                        break;
                    case EnemyMovementModel.NeutralFighter:
                        behavior = gameObject.AddComponent<EnemyNeutralFighterBehavior>();
                        break;
                    default:
                        behavior = gameObject.AddComponent<EnemyDroneBehavior>();
                        break;
                }
            }
            else
            {
                behavior = gameObject.AddComponent<EnemyDroneBehavior>();
            }
        }

        behavior.Initialize(this);
    }

    bool IsMatchingBehavior(EnemyBotBehaviorBase existingBehavior)
    {
        if (existingBehavior == null || Definition == null || Definition.Movement == null)
            return false;

        return Definition.Movement.Model switch
        {
            EnemyMovementModel.OrbitMap => existingBehavior is EnemyCorsairBehavior,
            EnemyMovementModel.Drift => existingBehavior is EnemyMineBehavior,
            EnemyMovementModel.RouteExtractionZones => existingBehavior is EnemySpaceTruckBehavior,
            EnemyMovementModel.RadarShip => existingBehavior is EnemyRadarShipBehavior,
            EnemyMovementModel.RescueShip => existingBehavior is EnemyRescueShipBehavior,
            EnemyMovementModel.PirateFighter => existingBehavior is EnemyPirateFighterBehavior,
            EnemyMovementModel.PirateBase => existingBehavior is EnemyPirateBaseBehavior,
            EnemyMovementModel.SpaceManta => existingBehavior is EnemySpaceMantaBehavior,
            EnemyMovementModel.GravitySquid => existingBehavior is EnemyGravitySquidBehavior,
            EnemyMovementModel.HunterLance => existingBehavior is EnemyHunterLanceBehavior,
            EnemyMovementModel.ContainerShip => existingBehavior is EnemyContainerShipBehavior,
            EnemyMovementModel.CosmicWorm => existingBehavior is EnemyCosmicWormBehavior,
            EnemyMovementModel.Mothership => existingBehavior is EnemyMothershipBehavior,
            EnemyMovementModel.NeutralFighter => existingBehavior is EnemyNeutralFighterBehavior,
            _ => existingBehavior is EnemyDroneBehavior
        };
    }

    void DisablePlayerOnlySystems()
    {
        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.enabled = false;

        ShipInventoryHudUI cargoHud = GetComponent<ShipInventoryHudUI>();
        if (cargoHud != null)
            cargoHud.enabled = false;

        BoosterBarUI boosterUi = GetComponent<BoosterBarUI>();
        if (boosterUi != null)
            boosterUi.enabled = false;
    }

    void ApplyBotVisuals()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        cachedRenderer = renderer;
        Sprite sprite = GetVisualSprite();
        if (sprite == null)
            return;

        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = GameVisualTheme.EnemySortingOrder;
        FitRendererToTargetSize(renderer, VisualTargetSize);
    }

    void EnsureAnimatedVisual()
    {
        if (Definition == null || string.IsNullOrWhiteSpace(Definition.AnimationResourcePath))
            return;

        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        if (cachedRenderer == null)
            return;

        EnemySpriteFrameAnimator animator = GetComponent<EnemySpriteFrameAnimator>();
        if (animator == null)
            animator = gameObject.AddComponent<EnemySpriteFrameAnimator>();

        animator.Configure(
            cachedRenderer,
            Definition.AnimationResourcePath,
            Definition.AnimationFramesPerSecond > 0f ? Definition.AnimationFramesPerSecond : 7f);
    }

    bool UsesAnimatedVisual()
    {
        return Definition != null && !string.IsNullOrWhiteSpace(Definition.AnimationResourcePath);
    }

    void EnsureStableVisuals()
    {
        if (health != null && health.IsWreck)
            return;

        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        if (cachedRenderer == null)
            return;

        if (!UsesAnimatedVisual())
        {
            Sprite desiredSprite = GetVisualSprite();
            if (desiredSprite != null && cachedRenderer.sprite != desiredSprite)
                cachedRenderer.sprite = desiredSprite;
        }
        else if (cachedRenderer.sprite == null)
        {
            cachedRenderer.sprite = GetVisualSprite();
        }

        cachedRenderer.color = Color.white;
        cachedRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        cachedRenderer.sortingOrder = pirateBaseLaunchProtected ? ResolvePirateBaseLaunchSortingOrder() : GameVisualTheme.EnemySortingOrder;
        FitRendererToTargetSize(cachedRenderer, VisualTargetSize);
    }

    public Sprite GetVisualSprite()
    {
        return Definition != null ? Definition.GetVisualSprite() : null;
    }

    public bool HasCustomDeathExplosion()
    {
        return Definition != null && Definition.Explosion != null;
    }

    public void RequestDetonation()
    {
        if (hasDetonated || !CanRequestDetonation() || Definition == null || Definition.Explosion == null)
            return;

        EnemyExplosionProfile explosion = Definition.Explosion;
        hasDetonated = true;
        Vector3 detonationPosition = GetVisualCenterWorldPosition();
        SpaceObjectMotionSync.BroadcastSpaceMineDetonation(detonationPosition, explosion.TriggerRadius);
        DetonateNearbyTargets(explosion);
        if (PhotonNetwork.CurrentRoom != null && photonView != null)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    bool CanRequestDetonation()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return true;

        if (PhotonNetwork.IsMasterClient)
            return true;

        return isOwnedMine && view != null && view.IsMine;
    }

    void DetonateNearbyTargets(EnemyExplosionProfile explosion)
    {
        if (explosion == null)
            return;

        WeaponHitContext hitContext = new WeaponHitContext(explosion.DamageType, explosion.DeliveryMethod, explosion.DeliveryFlags, string.Empty);
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            EnemyBot candidateBot = candidate.GetComponent<EnemyBot>();
            if (candidateBot != null && candidateBot.Kind == EnemyBotKind.SpaceMine)
                continue;

            if (ShouldIgnoreMineTriggerFor(candidate))
                continue;

            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            if (distance > explosion.TriggerRadius)
                continue;

            PhotonView targetView = candidate.GetComponent<PhotonView>();
            if (targetView != null)
                targetView.RPC(
                    nameof(PlayerHealth.TakeDamageWithContext),
                    RpcTarget.MasterClient,
                    RoomSettings.GetEnemyDamage(kind),
                    photonView.ViewID,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted || beacon.photonView == null)
                continue;

            float distance = Vector2.Distance(transform.position, beacon.transform.position);
            if (distance > explosion.TriggerRadius)
                continue;

            beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageAt), RpcTarget.MasterClient, RoomSettings.GetEnemyDamage(kind), photonView.ViewID, beacon.transform.position.x, beacon.transform.position.y);
        }

        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (deployable == null || !deployable.CanBeTargeted || deployable.photonView == null)
                continue;

            float distance = Vector2.Distance(transform.position, deployable.transform.position);
            if (distance > explosion.TriggerRadius)
                continue;

            int damage = RoomSettings.GetEnemyDamage(kind);
            deployable.photonView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                damage,
                damage,
                photonView.ViewID,
                deployable.transform.position.x,
                deployable.transform.position.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }
    }

    [PunRPC]
    void PlayMineDetonationEffects(float x, float y, float z)
    {
        SpawnSpaceMineDetonationEffects(new Vector3(x, y, z));
    }

    bool IsGameStarted()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started)
        {
            return started;
        }

        return false;
    }

    void FitRendererToTargetSize(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds spriteBounds = renderer.sprite.bounds;
        float largestDimension = Mathf.Max(spriteBounds.size.x, spriteBounds.size.y);
        if (largestDimension <= 0.0001f)
            return;

        float scale = (targetSize * Mathf.Max(0.05f, visualScaleMultiplier)) / largestDimension;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    Vector3 GetVisualCenterWorldPosition()
    {
        if (cachedRenderer != null)
            return cachedRenderer.bounds.center;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
            return renderer.bounds.center;

        return transform.position;
    }

    void ResolveSpecialMineOwner(object[] instantiationData)
    {
        isPlayerPlacedMine = false;
        isOwnedMine = false;
        mineOwnerViewId = 0;

        if (kind != EnemyBotKind.SpaceMine || instantiationData == null || instantiationData.Length < 3)
            return;

        if (!(instantiationData[1] is string marker))
            return;

        bool playerMine = string.Equals(marker, PlayerPlacedMineMarker, System.StringComparison.Ordinal);
        bool containerShipMine = string.Equals(marker, ContainerShipMineMarker, System.StringComparison.Ordinal);
        if (!playerMine && !containerShipMine)
            return;

        if (instantiationData[2] is int ownerViewId && ownerViewId > 0)
        {
            isPlayerPlacedMine = playerMine;
            isOwnedMine = true;
            mineOwnerViewId = ownerViewId;
        }
    }

    void ResolveContainerShipCargoVariant(object[] instantiationData)
    {
        containerShipCargoVariantIndex = 0;
        if (kind != EnemyBotKind.ContainerShip)
            return;

        int variantCount = Mathf.Max(1, InventoryItemCatalog.BlueprintScrapContainerVariantCount);
        if (instantiationData != null && instantiationData.Length >= 2 && instantiationData[1] is int variantIndex)
        {
            containerShipCargoVariantIndex = Mathf.Clamp(variantIndex, 0, variantCount - 1);
            return;
        }

        int seed = view != null && view.ViewID > 0 ? view.ViewID : Mathf.RoundToInt(transform.position.sqrMagnitude * 1000f);
        containerShipCargoVariantIndex = Mathf.Abs(seed) % variantCount;
    }

    void ResolveSummonedDrone(object[] instantiationData)
    {
        isSummonedDrone = false;

        if (kind != EnemyBotKind.Drone || instantiationData == null || instantiationData.Length < 2)
            return;

        isSummonedDrone = instantiationData[1] is string marker &&
                          string.Equals(marker, SummonedDroneMarker, System.StringComparison.Ordinal);
    }

    void ResolvePirateBaseLaunchedFighter(object[] instantiationData)
    {
        isPirateBaseLaunchedFighter = false;
        pirateBaseLaunchTargetViewId = 0;
        pirateBaseLaunchSourceViewId = 0;

        if (!IsPirateFighterKind(kind) || instantiationData == null || instantiationData.Length < 2)
            return;

        if (!(instantiationData[1] is string marker) ||
            !string.Equals(marker, PirateBaseLaunchedFighterMarker, System.StringComparison.Ordinal))
        {
            return;
        }

        isPirateBaseLaunchedFighter = true;
        if (instantiationData.Length >= 3 && instantiationData[2] is int targetViewId)
            pirateBaseLaunchTargetViewId = Mathf.Max(0, targetViewId);
        if (instantiationData.Length >= 4 && instantiationData[3] is int sourceViewId)
            pirateBaseLaunchSourceViewId = Mathf.Max(0, sourceViewId);
    }

    void BeginPirateBaseLaunchAnimation()
    {
        if (pirateBaseLaunchProtected)
            return;

        pirateBaseLaunchProtected = true;
        visualScaleMultiplier = PirateBaseLaunchStartScale;
        pirateBaseLaunchStartedAt = Time.time;
        pirateBaseLaunchStartPosition = transform.position;
        Vector3 exitDirection = transform.up.sqrMagnitude > 0.001f ? transform.up.normalized : Vector3.up;
        pirateBaseLaunchEndPosition = pirateBaseLaunchStartPosition + exitDirection * PirateBaseLaunchExitDistance;
        StoreAndLockPirateBaseLaunchBody();
        StoreAndDisablePirateBaseLaunchColliders();
        ApplyPirateBaseLaunchRenderOrder();
    }

    void StoreAndLockPirateBaseLaunchBody()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        if (!pirateBaseLaunchBodyStateStored)
        {
            pirateBaseLaunchBodyStateStored = true;
            pirateBaseLaunchPreviousBodyType = rb.bodyType;
            pirateBaseLaunchPreviousSimulated = rb.simulated;
        }

        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    void StoreAndDisablePirateBaseLaunchColliders()
    {
        if (!pirateBaseLaunchColliderStateStored)
        {
            pirateBaseLaunchColliderStateStored = true;
            pirateBaseLaunchColliders = GetComponentsInChildren<Collider2D>(true);
            pirateBaseLaunchColliderStates = new bool[pirateBaseLaunchColliders.Length];
            for (int i = 0; i < pirateBaseLaunchColliders.Length; i++)
            {
                Collider2D launchCollider = pirateBaseLaunchColliders[i];
                pirateBaseLaunchColliderStates[i] = launchCollider != null && launchCollider.enabled;
            }
        }

        if (pirateBaseLaunchColliders == null)
            return;

        for (int i = 0; i < pirateBaseLaunchColliders.Length; i++)
        {
            Collider2D launchCollider = pirateBaseLaunchColliders[i];
            if (launchCollider != null)
                launchCollider.enabled = false;
        }
    }

    void UpdatePirateBaseLaunchAnimation()
    {
        if (!pirateBaseLaunchProtected)
            return;

        float elapsed = Time.time - pirateBaseLaunchStartedAt;
        float t = Mathf.Clamp01(elapsed / PirateBaseLaunchAnimationDuration);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        visualScaleMultiplier = Mathf.Lerp(PirateBaseLaunchStartScale, 1f, eased);
        if (cachedRenderer != null)
        {
            ApplyPirateBaseLaunchRenderOrder();
            FitRendererToTargetSize(cachedRenderer, VisualTargetSize);
        }

        Vector3 nextPosition = Vector3.Lerp(pirateBaseLaunchStartPosition, pirateBaseLaunchEndPosition, eased);

        StoreAndLockPirateBaseLaunchBody();
        StoreAndDisablePirateBaseLaunchColliders();
        if (rb != null && rb.bodyType == RigidbodyType2D.Kinematic)
            rb.MovePosition(nextPosition);
        else
            transform.position = nextPosition;

        if (t < 1f)
            return;

        FinishPirateBaseLaunchAnimation();
    }

    void FinishPirateBaseLaunchAnimation()
    {
        RestorePirateBaseLaunchColliders();
        pirateBaseLaunchProtected = false;
        visualScaleMultiplier = 1f;

        if (rb != null)
        {
            rb.simulated = pirateBaseLaunchBodyStateStored ? pirateBaseLaunchPreviousSimulated : true;
            rb.bodyType = pirateBaseLaunchBodyStateStored ? pirateBaseLaunchPreviousBodyType : RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        ForceCombatTarget(pirateBaseLaunchTargetViewId);
    }

    void ApplyPirateBaseLaunchRenderOrder()
    {
        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        if (cachedRenderer == null)
            return;

        cachedRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        cachedRenderer.sortingOrder = ResolvePirateBaseLaunchSortingOrder();
    }

    int ResolvePirateBaseLaunchSortingOrder()
    {
        int baseOrder = GameVisualTheme.EnemySortingOrder;
        if (pirateBaseLaunchSourceViewId <= 0)
            return baseOrder + 12;

        PhotonView sourceView = PhotonView.Find(pirateBaseLaunchSourceViewId);
        SpriteRenderer sourceRenderer = sourceView != null ? sourceView.GetComponent<SpriteRenderer>() : null;
        if (sourceRenderer != null)
            baseOrder = sourceRenderer.sortingOrder;

        return baseOrder + 12;
    }

    void RestorePirateBaseLaunchColliders()
    {
        if (!pirateBaseLaunchColliderStateStored || pirateBaseLaunchColliders == null || pirateBaseLaunchColliderStates == null)
            return;

        int count = Mathf.Min(pirateBaseLaunchColliders.Length, pirateBaseLaunchColliderStates.Length);
        for (int i = 0; i < count; i++)
        {
            Collider2D launchCollider = pirateBaseLaunchColliders[i];
            if (launchCollider != null)
                launchCollider.enabled = pirateBaseLaunchColliderStates[i];
        }
    }

    void ApplyMineOwnerCollisionIgnore()
    {
        if (!isOwnedMine || mineOwnerViewId <= 0)
            return;

        PhotonView ownerView = PhotonView.Find(mineOwnerViewId);
        if (ownerView == null)
            return;

        Collider2D[] mineColliders = GetComponentsInChildren<Collider2D>(true);
        Collider2D[] ownerColliders = ownerView.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < mineColliders.Length; i++)
        {
            Collider2D mineCollider = mineColliders[i];
            if (mineCollider == null)
                continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider2D ownerCollider = ownerColliders[j];
                if (ownerCollider == null)
                    continue;

                Physics2D.IgnoreCollision(mineCollider, ownerCollider, true);
            }
        }
    }

    public bool ShouldIgnoreMineTriggerFor(PlayerHealth candidate)
    {
        if (!isOwnedMine || candidate == null || mineOwnerViewId <= 0)
            return false;

        PhotonView candidateView = candidate.GetComponent<PhotonView>();
        return candidateView != null && candidateView.ViewID == mineOwnerViewId;
    }

    public void ConvertMothershipTurretsToWreckVisuals()
    {
        if (kind != EnemyBotKind.Mothership)
            return;

        EnemyMothershipBehavior mothershipBehavior = behavior as EnemyMothershipBehavior;
        if (mothershipBehavior == null)
            mothershipBehavior = GetComponent<EnemyMothershipBehavior>();

        mothershipBehavior?.ConvertTurretsToWreckVisuals();
    }

    public void HideContainerShipCargoVisual()
    {
        if (kind != EnemyBotKind.ContainerShip)
            return;

        EnemyContainerShipBehavior containerShipBehavior = behavior as EnemyContainerShipBehavior;
        if (containerShipBehavior == null)
            containerShipBehavior = GetComponent<EnemyContainerShipBehavior>();

        containerShipBehavior?.HideCargoVisual();
    }

    public void NotifyDamageTaken(int previousHp, int currentHp, int shieldDamage, int hpDamage, int attackerViewID)
    {
        if (!PhotonNetwork.IsMasterClient || health == null || health.IsWreck)
            return;

        bool wasHit = shieldDamage > 0 || hpDamage > 0;
        if (!wasHit)
            return;

        if (kind != EnemyBotKind.SpaceMine && kind != EnemyBotKind.RescueShip && kind != EnemyBotKind.PirateBase)
            EnemyBotManager.NotifyRescueShipSummonTrigger(this);

        if (kind == EnemyBotKind.Mothership && behavior is EnemyMothershipBehavior mothershipBehavior)
            mothershipBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.NeutralFighter && behavior is EnemyNeutralFighterBehavior neutralFighterBehavior)
            neutralFighterBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.RadarShip && behavior is EnemyRadarShipBehavior radarShipBehavior)
            radarShipBehavior.NotifyDamageSource(attackerViewID);

        if (IsPirateFighter && behavior is EnemyPirateFighterBehavior pirateFighterBehavior)
            pirateFighterBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.PirateBase && behavior is EnemyPirateBaseBehavior pirateBaseBehavior)
            pirateBaseBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.SpaceManta && behavior is EnemySpaceMantaBehavior spaceMantaBehavior)
            spaceMantaBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.GravitySquid && behavior is EnemyGravitySquidBehavior gravitySquidBehavior)
            gravitySquidBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.HunterLance && behavior is EnemyHunterLanceBehavior hunterLanceBehavior)
            hunterLanceBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.ContainerShip && behavior is EnemyContainerShipBehavior containerShipBehavior)
            containerShipBehavior.NotifyDamageTaken(attackerViewID);

        if (kind == EnemyBotKind.CosmicWorm && behavior is EnemyCosmicWormBehavior cosmicWormBehavior)
            cosmicWormBehavior.NotifyDamageSource(attackerViewID);

        if (kind != EnemyBotKind.SpaceTruck)
            return;

        if (!spaceTruckFirstHitHandled)
        {
            spaceTruckFirstHitHandled = true;
            forcedSpeedMultiplier = 2f;
            TriggerSpaceTruckAlarmAndDrone();
        }

        int halfHp = Mathf.CeilToInt(health.maxHP * 0.5f);
        if (!spaceTruckHalfHpHandled && previousHp > halfHp && currentHp <= halfHp)
        {
            spaceTruckHalfHpHandled = true;
            TriggerSpaceTruckAlarmAndDrone();
        }
    }

    void TriggerSpaceTruckAlarmAndDrone()
    {
        Vector3 position = GetVisualCenterWorldPosition();
        photonView.RPC(nameof(PlaySpaceTruckAlert), RpcTarget.All, position.x, position.y, position.z);
        SpawnSummonedDroneNear(position);
    }

    void SpawnSummonedDroneNear(Vector3 sourcePosition)
    {
        EnemyBotDefinition droneDefinition = EnemyBotCatalog.GetDefinition(EnemyBotKind.Drone);
        if (droneDefinition == null)
            return;

        Vector2 offset = new Vector2(
            Mathf.Sin(Time.time * 3.7f + photonView.ViewID) > 0f ? 1.8f : -1.8f,
            1.25f);
        Vector3 spawnPosition = sourcePosition + (Vector3)offset;
        GameObject droneObject = PhotonNetwork.Instantiate("Player", spawnPosition, Quaternion.identity, 0, new object[] { droneDefinition.InstantiationMarker, SummonedDroneMarker });
        if (droneObject == null)
            return;

        EnemyBot drone = droneObject.GetComponent<EnemyBot>();
        if (drone == null)
            drone = droneObject.AddComponent<EnemyBot>();

        drone.InitializeFromPhotonData();
    }

    [PunRPC]
    void PlaySpaceTruckAlert(float x, float y, float z)
    {
        AudioManager.Instance.PlaySpaceTruckAlertAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void SpawnRadarStrikeMarkerRpc(float x, float y, float warningDuration, float radius)
    {
        RadarStrikeVfx.SpawnMarker(new Vector2(x, y), warningDuration, radius);
    }

    [PunRPC]
    public void PlayRadarShipShootRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayRadarShipShootAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void PlayRadarShipIncomingRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayRadarShipIncomingAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void SpawnRadarStrikeImpactRpc(float x, float y, float radius)
    {
        RadarStrikeVfx.SpawnImpact(new Vector2(x, y), radius);
    }

    [PunRPC]
    public void PlayRescueShipIncomingRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayRescueShipIncomingAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void PlaySpaceMantaWarningRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlaySpaceMantaWarningAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void PlayGravitySquidWarningRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayGravitySquidWarningAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void PlayHunterLanceLockRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayHunterLanceLockAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void PlayHunterLanceFireRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayHunterLanceFireAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void SpawnHunterLanceAimRpc(float originX, float originY, float directionX, float directionY, float range, float duration)
    {
        HunterLanceBeamVfx.SpawnAim(new Vector2(originX, originY), new Vector2(directionX, directionY), range, duration);
        DynamicCameraZoomController.Request(DynamicCameraZoomProfiles.HunterLanceLock, new Vector3(originX, originY, 0f), duration);
    }

    [PunRPC]
    public void SpawnHunterLanceShotRpc(float originX, float originY, float directionX, float directionY, float range)
    {
        HunterLanceBeamVfx.SpawnShot(new Vector2(originX, originY), new Vector2(directionX, directionY), range);
    }

    [PunRPC]
    public void PlayCosmicWormPhaseRpc(int phase, float x, float y, float z, float radius)
    {
        CosmicWormPhaseBurstVfx.Spawn(new Vector3(x, y, z), phase, radius);
        CosmicWormVisualController.AttachOrUpdate(this, phase, false);
    }

    [PunRPC]
    public void SpawnCosmicWormSpitVfxRpc(float x, float y, float directionX, float directionY, int phase)
    {
        CosmicWormSpitVfx.Spawn(new Vector2(x, y), new Vector2(directionX, directionY), phase);
    }

    [PunRPC]
    public void SpawnCosmicWormDashWarningRpc(float originX, float originY, float directionX, float directionY, float range, float duration)
    {
        CosmicWormDashWarningVfx.Spawn(new Vector2(originX, originY), new Vector2(directionX, directionY), range, duration);
        Vector2 direction = new Vector2(directionX, directionY);
        if (direction.sqrMagnitude > 0.001f)
            direction.Normalize();
        Vector2 midpoint = new Vector2(originX, originY) + direction * (Mathf.Max(0f, range) * 0.5f);
        DynamicCameraZoomController.Request(DynamicCameraZoomProfiles.CosmicWormDanger, new Vector3(midpoint.x, midpoint.y, 0f), duration);
    }

    [PunRPC]
    public void SpawnCosmicWormDashTrailRpc(float x, float y, float directionX, float directionY, float radius)
    {
        CosmicWormDashTrailVfx.Spawn(new Vector2(x, y), new Vector2(directionX, directionY), radius);
    }

    [PunRPC]
    public void StartCosmicWormSwallowRpc(float x, float y, float directionX, float directionY, float radius, float duration, int sourceViewId)
    {
        CosmicWormSwallowVfx.StartEffect(sourceViewId, new Vector2(x, y), new Vector2(directionX, directionY), radius, duration);
        cosmicWormSwallowZoomToken = DynamicCameraZoomController.Refresh(
            cosmicWormSwallowZoomToken,
            DynamicCameraZoomProfiles.CosmicWormDanger.WithMultiplier(1.2f),
            new Vector3(x, y, 0f),
            duration);
    }

    [PunRPC]
    public void StopCosmicWormSwallowRpc(int sourceViewId)
    {
        CosmicWormSwallowVfx.StopEffect(sourceViewId);
        DynamicCameraZoomController.Cancel(cosmicWormSwallowZoomToken);
        cosmicWormSwallowZoomToken = 0;
    }

    [PunRPC]
    public void StartGravitySquidTetherRpc(int targetViewId)
    {
        GravitySquidTetherVfx.StartBeam(photonView != null ? photonView.ViewID : 0, targetViewId);
    }

    [PunRPC]
    public void StopGravitySquidTetherRpc()
    {
        GravitySquidTetherVfx.StopBeam(photonView != null ? photonView.ViewID : 0);
    }

    [PunRPC]
    public void StartRescueShipBeamRpc(int targetViewId)
    {
        RescueShipBeamVfx.StartBeam(photonView != null ? photonView.ViewID : 0, targetViewId);
    }

    [PunRPC]
    public void StopRescueShipBeamRpc()
    {
        RescueShipBeamVfx.StopBeam(photonView != null ? photonView.ViewID : 0);
    }

    [PunRPC]
    public void StartPirateBaseCollectionBeamRpc(int targetViewId)
    {
        if (kind != EnemyBotKind.PirateBase)
            return;

        PirateBaseCollectionBeamVfx.StartBeam(photonView != null ? photonView.ViewID : 0, targetViewId);
    }

    [PunRPC]
    public void StopPirateBaseCollectionBeamRpc()
    {
        PirateBaseCollectionBeamVfx.StopBeam(photonView != null ? photonView.ViewID : 0);
    }

    [PunRPC]
    public void PlayPirateBaseLaunchVfxRpc(int fighterKindValue)
    {
        if (kind != EnemyBotKind.PirateBase)
            return;

        PirateBaseLaunchVfx.Play(this, (EnemyBotKind)fighterKindValue);
        DynamicCameraZoomController.Request(DynamicCameraZoomProfiles.PirateBaseLaunch, transform.position);
    }

    public void ForceCombatTarget(int targetViewId)
    {
        if (IsPirateFighter && behavior is EnemyPirateFighterBehavior pirateFighterBehavior)
            pirateFighterBehavior.NotifyForcedTarget(targetViewId);
    }

    public void DropPirateBaseCargoOnDeath()
    {
        if (kind != EnemyBotKind.PirateBase)
            return;

        EnemyPirateBaseBehavior pirateBaseBehavior = behavior as EnemyPirateBaseBehavior;
        if (pirateBaseBehavior == null)
            pirateBaseBehavior = GetComponent<EnemyPirateBaseBehavior>();

        pirateBaseBehavior?.DropCollectedCargoOnDeath();
    }

    public void DropContainerShipCargoOnDeath()
    {
        if (kind != EnemyBotKind.ContainerShip || !PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom)
            return;

        int variantIndex = ContainerShipCargoVariantIndex;
        string itemId = InventoryItemCatalog.GetBlueprintScrapContainerItemId(variantIndex);
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        Vector2 driftDirection = rb != null && rb.linearVelocity.sqrMagnitude > 0.04f
            ? rb.linearVelocity.normalized
            : (Vector2)transform.up;
        if (driftDirection.sqrMagnitude <= 0.001f)
            driftDirection = Vector2.up;

        Vector2 side = new Vector2(-driftDirection.y, driftDirection.x);
        float seed = (photonView != null ? photonView.ViewID : variantIndex + 1) * 0.173f;
        Vector2 drift = (driftDirection * 0.62f + side * Mathf.Lerp(-0.22f, 0.22f, Mathf.PerlinNoise(seed, seed + 9.3f))).normalized * 0.58f;
        Vector3 dropPosition = GetVisualCenterWorldPosition() - (Vector3)(driftDirection * 0.25f);
        GameObject cargo = PhotonNetwork.Instantiate("TreasureNetwork", dropPosition, Quaternion.identity, 0, new object[] { itemId });
        if (cargo != null)
        {
            Rigidbody2D cargoBody = cargo.GetComponent<Rigidbody2D>();
            if (cargoBody != null)
            {
                cargoBody.linearVelocity = drift;
                cargoBody.angularVelocity = Mathf.Lerp(-28f, 28f, Mathf.PerlinNoise(seed + 1.7f, seed + 4.1f));
            }
        }

        GameVisualTheme.RequestRuntimeRefresh(true);
    }

    public static Vector3 ResolveSpaceMineDetonationPosition(int sourceViewId, Vector3 fallbackWorldPosition)
    {
        if (sourceViewId > 0)
        {
            PhotonView sourceView = PhotonView.Find(sourceViewId);
            if (sourceView != null)
            {
                EnemyBot bot = sourceView.GetComponent<EnemyBot>();
                if (bot != null)
                    return bot.GetVisualCenterWorldPosition();

                return sourceView.transform.position;
            }
        }

        return fallbackWorldPosition;
    }

    public static void SpawnSpaceMineDetonationEffects(Vector3 worldPosition)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(EnemyBotKind.SpaceMine);
        EnemyExplosionProfile explosion = definition != null ? definition.Explosion : null;
        SpawnSpaceMineDetonationEffects(worldPosition, explosion != null ? explosion.TriggerRadius : 0f);
    }

    public static void SpawnSpaceMineDetonationEffects(Vector3 worldPosition, float radius)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(EnemyBotKind.SpaceMine);
        EnemyExplosionProfile explosion = definition != null ? definition.Explosion : null;
        if (explosion == null)
            return;

        float effectRadius = radius > 0.1f ? radius : explosion.TriggerRadius;
        if (effectRadius >= 4.5f)
            SpaceBombExplosionVfx.Spawn(worldPosition, effectRadius);
        else
            SpaceMineExplosionVfx.Spawn(worldPosition, effectRadius);

        if (explosion.SoundId == "space_mine_boom")
            AudioManager.Instance.PlaySpaceMineBoomAt(worldPosition);
        else
            AudioManager.Instance.PlayExplosionAt(worldPosition);
    }
}

public abstract class EnemyBotBehaviorBase : MonoBehaviour
{
    protected EnemyBot bot;

    public virtual void Initialize(EnemyBot owner)
    {
        bot = owner;
    }

    protected static float ScaleEnemyAttackWindup(float duration)
    {
        return Mathf.Max(0.01f, duration * RoomSettings.GetEnemyAttackWindupMultiplier());
    }

    protected static float ScaleEnemyAttackCooldown(float cooldown)
    {
        return Mathf.Max(0.05f, cooldown * RoomSettings.GetEnemyAttackCooldownMultiplier());
    }

    public abstract void TickBehavior();
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyDroneBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    float nextTargetRefreshTime;
    float nextRepathTime;
    Vector2 currentMoveDirection = Vector2.up;
    Transform currentTarget;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                Mathf.Max(weapon.AmmoCount, 1),
                weapon.ReloadDuration,
                RoomSettings.GetEnemyDamage(owner.Kind),
                weapon.BulletScaleMultiplier,
                weapon.BulletColor,
                weapon.MuzzleOffsetDistance,
                weapon.InfiniteAmmo,
                weapon.BulletSpeed,
                weapon.ShotSoundId,
                weapon.Range,
                string.Empty,
                10f,
                weapon.DamageType,
                weapon.DeliveryMethod,
                weapon.DeliveryFlags);
        }
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        if (Time.time >= nextTargetRefreshTime)
        {
            nextTargetRefreshTime = Time.time + movement.TargetRefreshInterval;
            currentTarget = ResolveTarget();
        }

        if (currentTarget == null)
        {
            ApplyIdleDrift();
            return;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + movement.RepathInterval;
            currentMoveDirection = CalculateMoveDirection(currentTarget.position);
        }

        Vector2 desiredVelocity = currentMoveDirection * bot.EffectiveMoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.14f);

        Vector2 aimDirection = (Vector2)currentTarget.position - rb.position;
        if (weapon != null && weapon.RotateTowardAim && aimDirection.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg + 90f;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }

        TryShootAtTarget(aimDirection);
    }

    void ApplyIdleDrift()
    {
        if (rb.linearVelocity.sqrMagnitude < 0.05f)
        {
            Vector2 fallback = currentMoveDirection.sqrMagnitude > 0.001f ? currentMoveDirection : Vector2.up;
            rb.linearVelocity = fallback.normalized * (bot.EffectiveMoveSpeed * 0.36f);
        }

        float spin = Mathf.Sin(Time.time * 0.45f + view.ViewID * 0.23f) * movement.IdleDriftTurnSpeed;
        rb.MoveRotation(rb.rotation + spin * Time.fixedDeltaTime);
    }

    Transform ResolveTarget()
    {
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, transform.position, movement.DisengageRadius, true))
            return currentTarget;

        return FindClosestVisibleHumanTarget(movement.DetectionRadius);
    }

    Transform FindClosestVisibleHumanTarget(float maxDistance)
    {
        return EnemyTargetingUtility.FindClosestTarget(transform.position, health, maxDistance, true);
    }

    bool IsValidVisibleTarget(PlayerHealth candidate, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(candidate != null ? candidate.transform : null, health, transform.position, maxDistance, true);
    }

    Vector2 CalculateMoveDirection(Vector2 targetPosition)
    {
        Vector2 toTarget = targetPosition - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return currentMoveDirection;

        Vector2 towardTarget = toTarget / distance;
        Vector2 orbitDirection = new Vector2(-towardTarget.y, towardTarget.x);
        if (Mathf.Sin(Time.time * 0.6f + view.ViewID * 0.27f) < 0f)
            orbitDirection *= -1f;

        Vector2 result;
        if (distance > movement.PreferredDistance)
            result = towardTarget * 0.84f + orbitDirection * 0.28f;
        else if (distance < movement.OrbitDistance)
            result = -towardTarget * 0.72f + orbitDirection * 0.52f;
        else
            result = orbitDirection * 0.85f + towardTarget * 0.18f;

        return result.normalized;
    }

    void TryShootAtTarget(Vector2 aimDirection)
    {
        if (shooting == null || weapon == null || aimDirection.sqrMagnitude <= 0.001f)
            return;

        if (aimDirection.magnitude > weapon.Range)
            return;

        if (weapon.RotateTowardAim)
        {
            Vector2 normalizedAim = aimDirection.normalized;
            float facingDot = Vector2.Dot(-transform.up, normalizedAim);
            if (facingDot < 0.9f)
                return;
        }

        shooting.TryFireBot(aimDirection.normalized);
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyCorsairBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    Vector2 orbitCenter;
    float orbitRadius;
    float orbitAngle;
    float orbitDirection;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        orbitCenter = Vector2.zero;
        orbitRadius = Mathf.Max(6f, Mathf.Min(mapSize.x, mapSize.y) * movement.OrbitRadiusFactor);
        int orbitSeed = view != null ? view.ViewID : 0;
        orbitAngle = Mathf.Abs(orbitSeed * 0.137f) % (Mathf.PI * 2f);
        orbitDirection = (orbitSeed % 2 == 0) ? 1f : -1f;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                9999,
                weapon.ReloadDuration,
                RoomSettings.GetEnemyDamage(owner.Kind),
                weapon.BulletScaleMultiplier,
                weapon.BulletColor,
                weapon.MuzzleOffsetDistance,
                weapon.InfiniteAmmo,
                weapon.BulletSpeed,
                weapon.ShotSoundId,
                weapon.Range,
                string.Empty,
                10f,
                weapon.DamageType,
                weapon.DeliveryMethod,
                weapon.DeliveryFlags);
        }
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        orbitAngle += orbitDirection * movement.OrbitAngularSpeed * Time.fixedDeltaTime;
        Vector2 fromCenter = rb.position - orbitCenter;
        if (fromCenter.sqrMagnitude < 0.01f)
            fromCenter = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle));

        Vector2 radialDirection = fromCenter.normalized;
        Vector2 tangentDirection = orbitDirection > 0f
            ? new Vector2(-radialDirection.y, radialDirection.x)
            : new Vector2(radialDirection.y, -radialDirection.x);

        float radialError = orbitRadius - fromCenter.magnitude;
        Vector2 desiredVelocity = tangentDirection * bot.EffectiveMoveSpeed + radialDirection * (radialError * 1.35f);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.16f);

        if (desiredVelocity.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(desiredVelocity.y, desiredVelocity.x) * Mathf.Rad2Deg + 270f;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }

        TryShootNearestTarget();
    }

    void TryShootNearestTarget()
    {
        if (shooting == null || weapon == null)
            return;

        Transform bestTarget = EnemyTargetingUtility.FindClosestTarget(transform.position, health, weapon.Range, true);

        if (bestTarget == null)
            return;

        Vector2 shootDirection = bestTarget.position - transform.position;
        if (shootDirection.sqrMagnitude <= 0.01f)
            return;

        shooting.TryFireBot(shootDirection.normalized);
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyNeutralFighterBehavior : EnemyBotBehaviorBase
{
    enum FighterMode
    {
        Patrol,
        Combat,
        Flee
    }

    const float FleeDuration = 5f;
    const float AvoidanceScanRadius = 1.75f;
    const float AvoidanceWeight = 0.62f;
    const float MapEdgeSoftTurnMargin = 5.4f;
    const float MapEdgeHardTurnMargin = 1.15f;
    const float MapEdgeLookAheadSeconds = 1.2f;
    const float MapEdgeMinimumLookAhead = 1.1f;
    const float MapEdgeMaximumLookAhead = 3.4f;
    const float MapEdgeTurnTangentWeight = 0.62f;
    const float FireIntervalJitter = 0.1f;
    const float StuckVelocityThreshold = 0.16f;
    const float StuckDuration = 0.42f;
    const float AvoidanceSuppressionDuration = 0.85f;
    const float StuckEscapeDuration = 0.7f;
    const float MinimumMoveSpeedFraction = 0.28f;

    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    FighterMode mode = FighterMode.Patrol;
    Transform currentTarget;
    Vector2 patrolDirection = Vector2.up;
    Vector2 fleeDirection = Vector2.right;
    float nextTargetRefreshTime;
    float nextRepathTime;
    float nextPatrolTurnTime;
    float fleeUntil;
    float orbitDirection = 1f;
    float lowSpeedSince;
    float avoidanceSuppressedUntil;
    float stuckEscapeUntil;
    Vector2 stuckEscapeDirection = Vector2.up;
    float edgeAvoidanceStrength;
    Vector2 edgeInwardNormal;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.211f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        orbitDirection = seed % 2 == 0 ? 1f : -1f;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                weapon.AmmoCount,
                weapon.ReloadDuration,
                RoomSettings.GetEnemyDamage(owner.Kind),
                weapon.BulletScaleMultiplier,
                weapon.BulletColor,
                weapon.MuzzleOffsetDistance,
                weapon.InfiniteAmmo,
                weapon.BulletSpeed,
                weapon.ShotSoundId,
                weapon.Range,
                string.Empty,
                10f,
                weapon.DamageType,
                weapon.DeliveryMethod,
                weapon.DeliveryFlags);
        }
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        RefreshTargetIfNeeded();
        UpdateMode();

        edgeAvoidanceStrength = 0f;
        edgeInwardNormal = Vector2.zero;

        Vector2 desiredDirection = ResolveDesiredDirection();
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        desiredDirection = ApplyAvoidance(desiredDirection);
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        desiredDirection = ResolveStuckEscapeDirection(desiredDirection);
        if (desiredDirection.sqrMagnitude <= 0.001f)
            desiredDirection = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection.normalized;

        float speed = ResolveCurrentSpeed();
        Vector2 desiredVelocity = desiredDirection.normalized * speed;
        float velocityBlend = mode == FighterMode.Combat ? 0.2f : 0.13f;
        if (edgeAvoidanceStrength > 0.001f)
            velocityBlend = Mathf.Max(velocityBlend, Mathf.Lerp(0.22f, 0.54f, edgeAvoidanceStrength));

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, velocityBlend);
        ApplyMinimumMoveSpeed(desiredDirection, speed);

        Vector2 aimDirection = ResolveAimDirection(desiredDirection);
        if (edgeAvoidanceStrength > 0.58f &&
            edgeInwardNormal.sqrMagnitude > 0.001f &&
            aimDirection.sqrMagnitude > 0.001f &&
            Vector2.Dot(aimDirection.normalized, edgeInwardNormal.normalized) < -0.12f)
        {
            aimDirection = desiredDirection;
        }

        RotateNoseToward(aimDirection);

        if (mode == FighterMode.Combat)
            TryShootAtTarget();
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView != null)
            currentTarget = attackerView.transform;

        Vector2 threatPosition = attackerView != null ? (Vector2)attackerView.transform.position : rb != null ? rb.position - Vector2.right : (Vector2)transform.position - Vector2.right;
        Vector2 away = rb != null ? rb.position - threatPosition : (Vector2)transform.position - threatPosition;
        if (away.sqrMagnitude < 0.001f)
            away = rb != null && rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection;

        fleeDirection = away.normalized;
        fleeUntil = Time.time + FleeDuration;
        mode = FighterMode.Flee;
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.12f, movement.TargetRefreshInterval);
        currentTarget = ResolveTarget();
    }

    void UpdateMode()
    {
        if (mode == FighterMode.Flee)
        {
            if (Time.time < fleeUntil)
                return;

            if (currentTarget == null || !IsTargetWithin(currentTarget, movement.DetectionRadius))
                mode = FighterMode.Patrol;
            else
                mode = FighterMode.Combat;
            return;
        }

        if (currentTarget != null && IsTargetWithin(currentTarget, movement.DetectionRadius))
        {
            mode = FighterMode.Combat;
            return;
        }

        if (mode == FighterMode.Combat && (currentTarget == null || !IsTargetWithin(currentTarget, movement.DisengageRadius)))
            mode = FighterMode.Patrol;
    }

    Vector2 ResolveDesiredDirection()
    {
        switch (mode)
        {
            case FighterMode.Combat:
                return ResolveCombatDirection();
            case FighterMode.Flee:
                return fleeDirection.sqrMagnitude > 0.001f ? fleeDirection.normalized : -transform.up;
            default:
                return ResolvePatrolDirection();
        }
    }

    Vector2 ResolvePatrolDirection()
    {
        if (Time.time >= nextPatrolTurnTime)
        {
            nextPatrolTurnTime = Time.time + Random.Range(1.2f, 2.4f);
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.55f, 0.55f);
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        Vector2 boundarySteer = ResolveMapBoundarySteering(patrolDirection, out float boundaryStrength, out _);
        if (boundaryStrength > 0.001f)
        {
            float steerBlend = Mathf.Clamp01(0.28f + boundaryStrength * 0.62f);
            patrolDirection = (patrolDirection.normalized * (1f - steerBlend) + boundarySteer * steerBlend).normalized;
        }

        return patrolDirection.normalized;
    }

    Vector2 ResolveCombatDirection()
    {
        if (currentTarget == null)
            return ResolvePatrolDirection();

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return ResolvePatrolDirection();

        Vector2 toward = toTarget / distance;
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-toward.y, toward.x)
            : new Vector2(toward.y, -toward.x);

        float slowOrbitWave = Mathf.Sin(Time.time * 1.8f + view.ViewID * 0.17f) * 0.18f;
        if (distance > movement.PreferredDistance + 0.6f)
            return (toward * 0.86f + tangent * (0.26f + slowOrbitWave)).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.72f + tangent * 0.68f).normalized;

        return (tangent * 0.88f + toward * 0.16f).normalized;
    }

    Vector2 ApplyAvoidance(Vector2 desiredDirection)
    {
        if (Time.time < avoidanceSuppressedUntil)
            return ResolveStuckEscapeDirection(desiredDirection);

        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f
            ? desiredDirection.normalized
            : rb.linearVelocity.sqrMagnitude > 0.001f
                ? rb.linearVelocity.normalized
                : patrolDirection.normalized;
        Vector2 avoidance = Vector2.zero;
        int closeAvoidedObjects = 0;
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(rb.position, AvoidanceScanRadius, out Collider2D[] hits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            if (!IsAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(rb.position);
            Vector2 toObstacle = closest - rb.position;
            if (toObstacle.sqrMagnitude > 0.0001f && Vector2.Dot(toObstacle.normalized, desired) < -0.2f)
                continue;

            Vector2 away = rb.position - closest;
            float distance = Mathf.Max(0.12f, away.magnitude);
            if (away.sqrMagnitude <= 0.0001f)
                away = rb.position - (Vector2)hit.transform.position;

            if (away.sqrMagnitude > 0.0001f)
            {
                closeAvoidedObjects++;
                avoidance += away.normalized * Mathf.Clamp01((AvoidanceScanRadius - distance) / AvoidanceScanRadius);
            }
        }

        if (avoidance.sqrMagnitude <= 0.001f)
            return desiredDirection;

        UpdateStuckSuppression(closeAvoidedObjects, avoidance, desired);
        if (Time.time < avoidanceSuppressedUntil)
            return ResolveStuckEscapeDirection(desired);

        Vector2 blended = (desired + avoidance.normalized * AvoidanceWeight).normalized;
        if (Vector2.Dot(blended, desired) < 0.2f)
            blended = (desired * 0.82f + avoidance.normalized * 0.18f).normalized;

        return blended;
    }

    void UpdateStuckSuppression(int closeAvoidedObjects, Vector2 avoidance, Vector2 desired)
    {
        if (closeAvoidedObjects < 1 || rb.linearVelocity.magnitude > StuckVelocityThreshold)
        {
            lowSpeedSince = 0f;
            return;
        }

        if (lowSpeedSince <= 0f)
        {
            lowSpeedSince = Time.time;
            return;
        }

        if (Time.time - lowSpeedSince >= StuckDuration)
        {
            avoidanceSuppressedUntil = Time.time + AvoidanceSuppressionDuration;
            stuckEscapeUntil = Time.time + StuckEscapeDuration;
            stuckEscapeDirection = ResolveEscapeDirection(avoidance, desired);
            lowSpeedSince = 0f;
        }
    }

    Vector2 ResolveEscapeDirection(Vector2 avoidance, Vector2 desired)
    {
        Vector2 escape = avoidance.sqrMagnitude > 0.001f ? avoidance.normalized : Vector2.zero;
        if (escape.sqrMagnitude <= 0.001f)
            escape = desired.sqrMagnitude > 0.001f ? new Vector2(-desired.y, desired.x).normalized : patrolDirection.normalized;

        if (desired.sqrMagnitude > 0.001f && Vector2.Dot(escape, desired.normalized) < -0.45f)
        {
            Vector2 tangent = new Vector2(-desired.y, desired.x).normalized;
            escape = (escape * 0.55f + tangent * 0.45f).normalized;
        }

        return escape.sqrMagnitude > 0.001f ? escape.normalized : Vector2.up;
    }

    Vector2 ResolveStuckEscapeDirection(Vector2 desiredDirection)
    {
        Vector2 desired = NormalizeMoveDirection(desiredDirection);
        if (Time.time >= stuckEscapeUntil || stuckEscapeDirection.sqrMagnitude <= 0.001f)
            return desired;

        return (desired * 0.32f + stuckEscapeDirection.normalized * 0.68f).normalized;
    }

    bool IsAvoidedObject(Collider2D hit)
    {
        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        return hit.GetComponentInParent<DroppedCargoCrate>() != null;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = NormalizeMoveDirection(desiredDirection);
        Vector2 edgeSteer = ResolveMapBoundarySteering(desired, out float strength, out Vector2 inwardNormal);
        if (strength <= 0.001f)
            return desiredDirection;

        if (strength > edgeAvoidanceStrength)
        {
            edgeAvoidanceStrength = strength;
            edgeInwardNormal = inwardNormal;
        }

        float inwardDot = inwardNormal.sqrMagnitude > 0.001f ? Vector2.Dot(desired, inwardNormal.normalized) : 0f;
        float blend = Mathf.Clamp01(0.28f + strength * 0.72f);
        if (inwardDot < -0.05f)
            blend = Mathf.Clamp01(blend + 0.22f);

        Vector2 result = (desired * (1f - blend) + edgeSteer * blend).normalized;
        if (strength > 0.72f && inwardNormal.sqrMagnitude > 0.001f && Vector2.Dot(result, inwardNormal.normalized) < 0.18f)
            result = (result * 0.42f + inwardNormal.normalized * 0.92f).normalized;

        return result;
    }

    Vector2 NormalizeMoveDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
            return direction.normalized;

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.001f)
            return rb.linearVelocity.normalized;

        return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;
    }

    Vector2 ResolveMapBoundarySteering(Vector2 desiredDirection, out float strength, out Vector2 inwardNormal)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float bodyRadius = Mathf.Clamp(bot != null ? bot.VisualTargetSize * 0.58f : 0.55f, 0.35f, 1.2f);
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - bodyRadius);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - bodyRadius);
        Vector2 desired = NormalizeMoveDirection(desiredDirection);
        float lookAheadDistance = Mathf.Clamp(
            ResolveCurrentSpeed() * MapEdgeLookAheadSeconds,
            MapEdgeMinimumLookAhead,
            MapEdgeMaximumLookAhead);
        Vector2 predictedPosition = rb.position + desired * lookAheadDistance;
        Vector2 push = Vector2.zero;
        float maxStrength = 0f;

        float rightDistance = halfX - predictedPosition.x;
        if (rightDistance < MapEdgeSoftTurnMargin)
        {
            float axisStrength = CalculateMapEdgeStrength(rightDistance);
            push.x -= axisStrength;
            maxStrength = Mathf.Max(maxStrength, axisStrength);
        }

        float leftDistance = predictedPosition.x + halfX;
        if (leftDistance < MapEdgeSoftTurnMargin)
        {
            float axisStrength = CalculateMapEdgeStrength(leftDistance);
            push.x += axisStrength;
            maxStrength = Mathf.Max(maxStrength, axisStrength);
        }

        float topDistance = halfY - predictedPosition.y;
        if (topDistance < MapEdgeSoftTurnMargin)
        {
            float axisStrength = CalculateMapEdgeStrength(topDistance);
            push.y -= axisStrength;
            maxStrength = Mathf.Max(maxStrength, axisStrength);
        }

        float bottomDistance = predictedPosition.y + halfY;
        if (bottomDistance < MapEdgeSoftTurnMargin)
        {
            float axisStrength = CalculateMapEdgeStrength(bottomDistance);
            push.y += axisStrength;
            maxStrength = Mathf.Max(maxStrength, axisStrength);
        }

        if (push.sqrMagnitude <= 0.001f)
        {
            strength = 0f;
            inwardNormal = Vector2.zero;
            return Vector2.zero;
        }

        inwardNormal = push.normalized;
        strength = Mathf.Clamp01(maxStrength);

        bool affectsX = Mathf.Abs(push.x) > 0.001f;
        bool affectsY = Mathf.Abs(push.y) > 0.001f;
        Vector2 tangent = new Vector2(-inwardNormal.y, inwardNormal.x);
        if (Vector2.Dot(tangent, desired) < 0f)
            tangent = -tangent;

        float outwardAmount = Mathf.Clamp01(-Vector2.Dot(desired, inwardNormal));
        float tangentWeight = affectsX != affectsY
            ? Mathf.Lerp(MapEdgeTurnTangentWeight, 0.2f, outwardAmount)
            : Mathf.Lerp(0.28f, 0.12f, outwardAmount);

        Vector2 steering = inwardNormal + tangent * tangentWeight;
        return steering.sqrMagnitude > 0.001f ? steering.normalized : inwardNormal;
    }

    float CalculateMapEdgeStrength(float distanceToEdge)
    {
        float softStrength = Mathf.Clamp01((MapEdgeSoftTurnMargin - distanceToEdge) / MapEdgeSoftTurnMargin);
        if (distanceToEdge < MapEdgeHardTurnMargin)
        {
            float hardStrength = Mathf.InverseLerp(MapEdgeHardTurnMargin, -MapEdgeHardTurnMargin, distanceToEdge);
            softStrength = Mathf.Max(softStrength, 0.72f + hardStrength * 0.28f);
        }

        return Mathf.Clamp01(softStrength);
    }

    float ResolveCurrentSpeed()
    {
        float baseSpeed = bot != null ? bot.EffectiveMoveSpeed : 1f;
        return mode == FighterMode.Patrol ? baseSpeed * 0.5f : baseSpeed;
    }

    void ApplyMinimumMoveSpeed(Vector2 desiredDirection, float speed)
    {
        if (desiredDirection.sqrMagnitude <= 0.001f || speed <= 0f)
            return;

        float minimumSpeed = Mathf.Max(0.18f, speed * MinimumMoveSpeedFraction);
        if (rb.linearVelocity.magnitude >= minimumSpeed)
            return;

        rb.linearVelocity = desiredDirection.normalized * minimumSpeed;
    }

    Vector2 ResolveAimDirection(Vector2 moveDirection)
    {
        if (Time.time < stuckEscapeUntil && moveDirection.sqrMagnitude > 0.001f)
            return moveDirection.normalized;

        if (mode == FighterMode.Combat && currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            if (toTarget.sqrMagnitude > 0.001f)
                return toTarget.normalized;
        }

        return moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : transform.up;
    }

    void RotateNoseToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    void TryShootAtTarget()
    {
        if (shooting == null || weapon == null || currentTarget == null)
            return;

        Vector2 aim = (Vector2)currentTarget.position - rb.position;
        float distance = aim.magnitude;
        if (distance <= 0.001f || distance > weapon.Range)
            return;

        Vector2 normalizedAim = aim / distance;
        if (Vector2.Dot(transform.up, normalizedAim) < 0.92f)
            return;

        Vector3 muzzle = transform.position + transform.up * Mathf.Max(0.1f, weapon.MuzzleOffsetDistance);
        float cooldownJitter = Random.Range(-FireIntervalJitter, FireIntervalJitter);
        shooting.TryFireBotFromWorld(normalizedAim, muzzle, cooldownJitter);
    }

    Transform ResolveTarget()
    {
        float allowedRange = mode == FighterMode.Combat ? movement.DisengageRadius : movement.DetectionRadius;
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, transform.position, allowedRange, true))
            return currentTarget;

        return FindClosestVisibleHumanTarget(movement.DetectionRadius);
    }

    Transform FindClosestVisibleHumanTarget(float maxDistance)
    {
        return EnemyTargetingUtility.FindClosestTarget(transform.position, health, maxDistance, true);
    }

    bool IsValidVisibleTarget(PlayerHealth candidate, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(candidate != null ? candidate.transform : null, health, transform.position, maxDistance, true);
    }

    bool IsTargetWithin(Transform target, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(target, health, transform.position, maxDistance, true);
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyPirateFighterBehavior : EnemyBotBehaviorBase
{
    enum PirateMode
    {
        Patrol,
        AttackRun,
        BreakAway,
        CriticalFlee
    }

    const float CriticalHpFraction = 0.3f;
    const float AvoidanceScanRadius = 1.9f;
    const float AvoidanceWeight = 0.48f;
    const float MapEdgeMargin = 2.4f;
    const float MapEdgeSteerWeight = 0.82f;
    const float PatrolTurnIntervalMin = 0.95f;
    const float PatrolTurnIntervalMax = 1.85f;
    const float SquadronRefreshInterval = 0.34f;
    const float SquadronJoinDistance = 4.2f;
    const float SquadronComfortDistance = 2.35f;
    const float SquadronSeparationDistance = 1.18f;
    const float AttackRearOffset = 3.2f;
    const float AttackPointTolerance = 2.35f;
    const float RearArcDot = 0.25f;
    const float AttackBurstWindow = 0.88f;
    const float BreakAwayDuration = 1.35f;
    const float ForcedCombatDuration = 8f;
    const float FireIntervalJitter = 0.035f;
    const float BeaconPriorityRangeMultiplier = 1.9f;
    const float StuckVelocityThreshold = 0.16f;
    const float StuckDuration = 0.46f;
    const float AvoidanceSuppressionDuration = 0.85f;
    const float StuckEscapeDuration = 0.72f;
    const float MinimumMoveSpeedFraction = 0.3f;

    readonly Transform[] squadmates = new Transform[2];

    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    PirateMode mode = PirateMode.Patrol;
    Transform currentTarget;
    Vector2 patrolDirection = Vector2.up;
    Vector2 fallbackFleeDirection = Vector2.right;
    float nextTargetRefreshTime;
    float nextPatrolTurnTime;
    float nextSquadronRefreshTime;
    float breakAwayUntil;
    float forcedCombatUntil;
    int forcedCombatTargetViewId;
    float lowSpeedSince;
    float avoidanceSuppressedUntil;
    float stuckEscapeUntil;
    Vector2 stuckEscapeDirection = Vector2.up;
    float attackRunStartedAt;
    float lastSuccessfulShotTime = -10f;
    float orbitDirection = 1f;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.227f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        fallbackFleeDirection = patrolDirection;
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
        attackRunStartedAt = Time.time;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                weapon.AmmoCount,
                weapon.ReloadDuration,
                RoomSettings.GetEnemyDamage(owner.Kind),
                weapon.BulletScaleMultiplier,
                weapon.BulletColor,
                weapon.MuzzleOffsetDistance,
                weapon.InfiniteAmmo,
                weapon.BulletSpeed,
                weapon.ShotSoundId,
                weapon.Range,
                ResolveProjectileEffectId(owner.Kind),
                2.4f,
                weapon.DamageType,
                weapon.DeliveryMethod,
                weapon.DeliveryFlags);
        }

        if (owner.PirateBaseLaunchTargetViewId > 0)
            NotifyForcedTarget(owner.PirateBaseLaunchTargetViewId);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        RefreshTargetIfNeeded();
        RefreshSquadronIfNeeded();
        UpdateMode();

        Vector2 desiredDirection = ResolveDesiredDirection();
        desiredDirection = ApplySquadronSteering(desiredDirection);
        desiredDirection = ApplyAvoidance(desiredDirection);
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        desiredDirection = ResolveStuckEscapeDirection(desiredDirection);
        if (desiredDirection.sqrMagnitude <= 0.001f)
            desiredDirection = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection;

        float speed = ResolveCurrentSpeed();
        float velocityBlend = mode == PirateMode.AttackRun ? 0.22f : mode == PirateMode.CriticalFlee ? 0.28f : 0.16f;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredDirection.normalized * speed, velocityBlend);
        ApplyMinimumMoveSpeed(desiredDirection, speed);

        Vector2 aimDirection = ResolveAimDirection(desiredDirection);
        RotateNoseToward(aimDirection);

        if (mode == PirateMode.AttackRun)
            TryShootAtTarget();
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView != null)
            currentTarget = attackerView.transform;

        Vector2 threatPosition = attackerView != null ? (Vector2)attackerView.transform.position : rb != null ? rb.position - Vector2.right : (Vector2)transform.position - Vector2.right;
        Vector2 away = rb != null ? rb.position - threatPosition : (Vector2)transform.position - threatPosition;
        if (away.sqrMagnitude < 0.001f)
            away = rb != null && rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection;

        fallbackFleeDirection = away.normalized;
        forcedCombatUntil = Time.time + ForcedCombatDuration;
        forcedCombatTargetViewId = attackerViewID;
        if (!IsCriticalHealth())
            EnterAttackRun();
    }

    public void NotifyForcedTarget(int targetViewID)
    {
        PhotonView targetView = targetViewID > 0 ? PhotonView.Find(targetViewID) : null;
        if (targetView == null)
            return;

        Transform forcedTarget = targetView.transform;
        if (IsProtectedCharlieTarget(forcedTarget))
        {
            if (currentTarget == forcedTarget)
                currentTarget = null;

            return;
        }

        NotifyDamageSource(targetViewID);
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.12f, movement.TargetRefreshInterval);
        currentTarget = ResolveTarget();
    }

    Transform ResolveTarget()
    {
        float validRange = mode == PirateMode.Patrol && Time.time >= forcedCombatUntil
            ? movement.DetectionRadius
            : movement.DisengageRadius;

        if (IsValidPirateCaseCarrierTarget(currentTarget))
            return currentTarget;

        if (!IsProtectedCharlieTarget(currentTarget) &&
            EnemyTargetingUtility.IsTargetValid(currentTarget, health, rb.position, validRange, true))
            return currentTarget;

        return FindClosestUnprotectedTarget(movement.DetectionRadius);
    }

    Transform FindClosestUnprotectedTarget(float maxDistance)
    {
        Transform bestBeaconTarget = null;
        float bestBeaconDistance = float.MaxValue;
        float beaconRange = maxDistance * BeaconPriorityRangeMultiplier;

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted)
                continue;

            float distance = Vector2.Distance(rb.position, beacon.transform.position);
            if (distance > beaconRange || distance >= bestBeaconDistance)
                continue;

            bestBeaconDistance = distance;
            bestBeaconTarget = beacon.transform;
        }

        if (bestBeaconTarget != null)
            return bestBeaconTarget;

        PlayerHealth pirateCaseCarrier = ValuableCargoCarrierUtility.FindBestPirateCaseCarrier(rb.position, float.PositiveInfinity, health);
        if (pirateCaseCarrier != null)
            return pirateCaseCarrier.transform;

        Transform bestTarget = null;
        float bestDistance = float.MaxValue;
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (!IsValidUnprotectedPlayerTarget(candidate, maxDistance))
                continue;

            float distance = Vector2.Distance(rb.position, candidate.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestTarget = candidate.transform;
        }

        return bestTarget;
    }

    bool IsValidPirateCaseCarrierTarget(Transform target)
    {
        PlayerHealth targetHealth = target != null ? target.GetComponent<PlayerHealth>() : null;
        return ValuableCargoCarrierUtility.IsPirateCaseCarrier(targetHealth);
    }

    bool IsValidUnprotectedPlayerTarget(PlayerHealth candidate, float maxDistance)
    {
        if (candidate == null || candidate == health || candidate.IsWreck || candidate.IsBotControlled || candidate.IsAstronautControlled || candidate.IsEvacuationAnimating)
            return false;

        if (candidate.GetComponent<LureBeaconDecoy>() != null)
            return false;

        if (IsProtectedCharlieTarget(candidate.transform))
            return false;

        if (Vector2.Distance(rb.position, candidate.transform.position) > maxDistance)
            return false;

        HideInNebulaTarget candidateNebulaState = candidate.GetComponent<HideInNebulaTarget>();
        HideInNebulaTarget observerNebulaState = health != null ? health.GetComponent<HideInNebulaTarget>() : null;
        return candidateNebulaState == null || !candidateNebulaState.IsHiddenFromObserver(observerNebulaState);
    }

    bool IsProtectedCharlieTarget(Transform target)
    {
        PlayerHealth targetHealth = target != null ? target.GetComponent<PlayerHealth>() : null;
        if (targetHealth == null || targetHealth.photonView == null)
            return false;

        if (ValuableCargoCarrierUtility.IsPirateCaseCarrier(targetHealth))
            return false;

        if (!PilotCatalog.IsSelectedPilot(targetHealth.photonView.Owner, PilotCatalog.CharlieSmartId))
            return false;

        return Time.time >= forcedCombatUntil || targetHealth.photonView.ViewID != forcedCombatTargetViewId;
    }

    void RefreshSquadronIfNeeded()
    {
        if (Time.time < nextSquadronRefreshTime)
            return;

        nextSquadronRefreshTime = Time.time + SquadronRefreshInterval;
        squadmates[0] = null;
        squadmates[1] = null;
        float bestA = float.MaxValue;
        float bestB = float.MaxValue;

        EnemyBot[] bots = FindObjectsByType<EnemyBot>(FindObjectsInactive.Exclude);
        for (int i = 0; i < bots.Length; i++)
        {
            EnemyBot candidate = bots[i];
            if (candidate == null || candidate == bot || !EnemyBot.IsPirateFighterKind(candidate.Kind))
                continue;

            PlayerHealth candidateHealth = candidate.GetComponent<PlayerHealth>();
            if (candidateHealth != null && candidateHealth.IsWreck)
                continue;

            float distance = Vector2.Distance(rb.position, candidate.transform.position);
            if (distance < bestA)
            {
                bestB = bestA;
                squadmates[1] = squadmates[0];
                bestA = distance;
                squadmates[0] = candidate.transform;
            }
            else if (distance < bestB)
            {
                bestB = distance;
                squadmates[1] = candidate.transform;
            }
        }
    }

    void UpdateMode()
    {
        if (IsCriticalHealth())
        {
            mode = PirateMode.CriticalFlee;
            return;
        }

        if (mode == PirateMode.BreakAway)
        {
            if (Time.time < breakAwayUntil)
                return;

            EnterAttackRun();
            return;
        }

        if (IsProtectedCharlieTarget(currentTarget))
            currentTarget = null;

        if (currentTarget != null &&
            (IsValidPirateCaseCarrierTarget(currentTarget) ||
             EnemyTargetingUtility.IsTargetValid(currentTarget, health, rb.position, movement.DisengageRadius, true)))
        {
            if (mode != PirateMode.AttackRun)
                EnterAttackRun();
            return;
        }

        if (Time.time < forcedCombatUntil && currentTarget != null)
        {
            if (mode != PirateMode.AttackRun)
                EnterAttackRun();
            return;
        }

        mode = PirateMode.Patrol;
    }

    void EnterAttackRun()
    {
        mode = PirateMode.AttackRun;
        attackRunStartedAt = Time.time;
    }

    Vector2 ResolveDesiredDirection()
    {
        switch (mode)
        {
            case PirateMode.AttackRun:
                return ResolveAttackRunDirection();
            case PirateMode.BreakAway:
                return ResolveBreakAwayDirection();
            case PirateMode.CriticalFlee:
                return ResolveCriticalFleeDirection();
            default:
                return ResolvePatrolDirection();
        }
    }

    Vector2 ResolvePatrolDirection()
    {
        if (Time.time >= nextPatrolTurnTime)
        {
            nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.5f, 0.5f);
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        return patrolDirection;
    }

    Vector2 ResolveAttackRunDirection()
    {
        if (currentTarget == null)
            return ResolvePatrolDirection();

        Vector2 attackPoint = ResolveRearAttackPoint(currentTarget);
        Vector2 toAttackPoint = attackPoint - rb.position;
        float distanceToAttackPoint = toAttackPoint.magnitude;
        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        Vector2 targetForward = ResolveTargetForward(currentTarget);
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-targetForward.y, targetForward.x)
            : new Vector2(targetForward.y, -targetForward.x);

        if (distanceToAttackPoint > AttackPointTolerance)
            return (toAttackPoint.normalized * 0.92f + tangent * 0.16f).normalized;

        if (toTarget.magnitude < movement.OrbitDistance)
            return (-toTarget.normalized * 0.78f + tangent * 0.42f).normalized;

        return (-targetForward * 0.42f + tangent * 0.58f).normalized;
    }

    Vector2 ResolveBreakAwayDirection()
    {
        if (currentTarget == null)
            return rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection;

        Vector2 away = rb.position - (Vector2)currentTarget.position;
        if (away.sqrMagnitude <= 0.001f)
            away = fallbackFleeDirection;

        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-away.y, away.x)
            : new Vector2(away.y, -away.x);

        return (away.normalized * 0.82f + tangent.normalized * 0.38f).normalized;
    }

    Vector2 ResolveCriticalFleeDirection()
    {
        Transform threat = currentTarget != null
            ? currentTarget
            : FindClosestUnprotectedTarget(movement.DisengageRadius);

        Vector2 away = fallbackFleeDirection;
        if (threat != null)
            away = rb.position - (Vector2)threat.position;

        if (away.sqrMagnitude <= 0.001f)
            away = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection;

        Vector2 centerPull = (-rb.position).sqrMagnitude > 0.001f ? (-rb.position).normalized : Vector2.zero;
        fallbackFleeDirection = (away.normalized * 0.84f + centerPull * 0.28f).normalized;
        return fallbackFleeDirection;
    }

    Vector2 ApplySquadronSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : patrolDirection;
        Vector2 cohesion = Vector2.zero;
        Vector2 separation = Vector2.zero;
        Vector2 alignment = Vector2.zero;
        int count = 0;

        for (int i = 0; i < squadmates.Length; i++)
        {
            Transform mate = squadmates[i];
            if (mate == null)
                continue;

            Vector2 toMate = (Vector2)mate.position - rb.position;
            float distance = toMate.magnitude;
            if (distance <= 0.001f)
                continue;

            count++;
            if (distance > SquadronComfortDistance && distance < SquadronJoinDistance)
                cohesion += toMate.normalized * Mathf.InverseLerp(SquadronComfortDistance, SquadronJoinDistance, distance);

            if (distance < SquadronSeparationDistance)
                separation -= toMate.normalized * Mathf.InverseLerp(SquadronSeparationDistance, 0.12f, distance);

            Rigidbody2D mateBody = mate.GetComponent<Rigidbody2D>();
            if (mateBody != null && mateBody.linearVelocity.sqrMagnitude > 0.001f)
                alignment += mateBody.linearVelocity.normalized;
        }

        if (count <= 0)
            return desired;

        Vector2 steering = desired;
        if (cohesion.sqrMagnitude > 0.001f)
            steering += cohesion.normalized * (mode == PirateMode.Patrol ? 0.42f : 0.2f);
        if (separation.sqrMagnitude > 0.001f)
            steering += separation.normalized * 0.72f;
        if (alignment.sqrMagnitude > 0.001f && mode == PirateMode.Patrol)
            steering += alignment.normalized * 0.16f;

        return steering.sqrMagnitude > 0.001f ? steering.normalized : desired;
    }

    Vector2 ApplyAvoidance(Vector2 desiredDirection)
    {
        if (Time.time < avoidanceSuppressedUntil)
            return ResolveStuckEscapeDirection(desiredDirection);

        Vector2 desired = NormalizeMoveDirection(desiredDirection);
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(rb.position, AvoidanceScanRadius, out Collider2D[] hits);
        Vector2 avoidance = Vector2.zero;
        int closeAvoidedObjects = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            if (!IsAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(rb.position);
            Vector2 away = rb.position - closest;
            if (away.sqrMagnitude <= 0.0001f)
                away = rb.position - (Vector2)hit.transform.position;

            float distance = Mathf.Max(0.12f, away.magnitude);
            if (distance > AvoidanceScanRadius)
                continue;

            if (away.sqrMagnitude > 0.0001f)
            {
                closeAvoidedObjects++;
                avoidance += away.normalized * Mathf.Clamp01((AvoidanceScanRadius - distance) / AvoidanceScanRadius);
            }
        }

        if (avoidance.sqrMagnitude <= 0.001f)
            return desiredDirection;

        UpdateStuckSuppression(closeAvoidedObjects, avoidance, desired);
        if (Time.time < avoidanceSuppressedUntil)
            return ResolveStuckEscapeDirection(desired);

        Vector2 result = (desired + avoidance.normalized * AvoidanceWeight).normalized;
        return Vector2.Dot(result, desired) < 0.12f
            ? (desired * 0.8f + avoidance.normalized * 0.2f).normalized
            : result;
    }

    void UpdateStuckSuppression(int closeAvoidedObjects, Vector2 avoidance, Vector2 desired)
    {
        if (closeAvoidedObjects < 1 || rb.linearVelocity.magnitude > StuckVelocityThreshold)
        {
            lowSpeedSince = 0f;
            return;
        }

        if (lowSpeedSince <= 0f)
        {
            lowSpeedSince = Time.time;
            return;
        }

        if (Time.time - lowSpeedSince >= StuckDuration)
        {
            avoidanceSuppressedUntil = Time.time + AvoidanceSuppressionDuration;
            stuckEscapeUntil = Time.time + StuckEscapeDuration;
            stuckEscapeDirection = ResolveEscapeDirection(avoidance, desired);
            lowSpeedSince = 0f;
        }
    }

    Vector2 ResolveEscapeDirection(Vector2 avoidance, Vector2 desired)
    {
        Vector2 escape = avoidance.sqrMagnitude > 0.001f ? avoidance.normalized : Vector2.zero;
        if (escape.sqrMagnitude <= 0.001f)
            escape = desired.sqrMagnitude > 0.001f ? new Vector2(-desired.y, desired.x).normalized : patrolDirection.normalized;

        if (desired.sqrMagnitude > 0.001f && Vector2.Dot(escape, desired.normalized) < -0.45f)
        {
            Vector2 tangent = new Vector2(-desired.y, desired.x).normalized;
            escape = (escape * 0.55f + tangent * 0.45f).normalized;
        }

        return escape.sqrMagnitude > 0.001f ? escape.normalized : Vector2.up;
    }

    Vector2 ResolveStuckEscapeDirection(Vector2 desiredDirection)
    {
        Vector2 desired = NormalizeMoveDirection(desiredDirection);
        if (Time.time >= stuckEscapeUntil || stuckEscapeDirection.sqrMagnitude <= 0.001f)
            return desired;

        return (desired * 0.3f + stuckEscapeDirection.normalized * 0.7f).normalized;
    }

    Vector2 NormalizeMoveDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
            return direction.normalized;

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.001f)
            return rb.linearVelocity.normalized;

        return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;
    }

    bool IsAvoidedObject(Collider2D hit)
    {
        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        if (hit.GetComponentInParent<DroppedCargoCrate>() != null)
            return true;

        EnemyBot otherBot = hit.GetComponentInParent<EnemyBot>();
        return otherBot != null && otherBot != bot;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : patrolDirection;
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.2f, bot.EffectiveMoveSpeed * 1.35f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + 1.5f, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - 1.5f, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + 1.5f, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - 1.5f, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        Vector2 tangent = new Vector2(-inward.y, inward.x);
        if (Vector2.Dot(tangent, desired) < 0f)
            tangent = -tangent;

        Vector2 edgeTurn = (inward.normalized * 0.88f + tangent.normalized * 0.28f).normalized;
        return (desired * (1f - MapEdgeSteerWeight) + edgeTurn * MapEdgeSteerWeight).normalized;
    }

    float ResolveCurrentSpeed()
    {
        float baseSpeed = bot != null ? bot.EffectiveMoveSpeed : 1f;
        switch (mode)
        {
            case PirateMode.Patrol:
                return baseSpeed * 0.72f;
            case PirateMode.BreakAway:
                return baseSpeed * 1.16f;
            case PirateMode.CriticalFlee:
                return baseSpeed * 1.32f;
            default:
                return baseSpeed;
        }
    }

    void ApplyMinimumMoveSpeed(Vector2 desiredDirection, float speed)
    {
        if (desiredDirection.sqrMagnitude <= 0.001f || speed <= 0f)
            return;

        float minimumSpeed = Mathf.Max(0.18f, speed * MinimumMoveSpeedFraction);
        if (rb.linearVelocity.magnitude >= minimumSpeed)
            return;

        rb.linearVelocity = desiredDirection.normalized * minimumSpeed;
    }

    Vector2 ResolveAimDirection(Vector2 moveDirection)
    {
        if (Time.time < stuckEscapeUntil && moveDirection.sqrMagnitude > 0.001f)
            return moveDirection.normalized;

        if ((mode == PirateMode.AttackRun || mode == PirateMode.BreakAway) && currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            if (toTarget.sqrMagnitude > 0.001f)
                return toTarget.normalized;
        }

        return moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : transform.up;
    }

    void TryShootAtTarget()
    {
        if (shooting == null || weapon == null || currentTarget == null)
            return;

        if (IsProtectedCharlieTarget(currentTarget))
            return;

        Vector2 aim = (Vector2)currentTarget.position - rb.position;
        float distance = aim.magnitude;
        if (distance <= 0.001f || distance > weapon.Range)
            return;

        Vector2 normalizedAim = aim / distance;
        if (Vector2.Dot(transform.up, normalizedAim) < 0.9f)
            return;

        bool inRearArc = IsBehindTarget(currentTarget);
        bool closeToAttackPoint = Vector2.Distance(rb.position, ResolveRearAttackPoint(currentTarget)) <= AttackPointTolerance + 0.35f;
        if (!inRearArc && !closeToAttackPoint)
            return;

        Vector3[] muzzles = ResolveWingMuzzles();
        if (WouldHitOwnPirateBase(muzzles, currentTarget.position))
            return;

        bool fired = shooting.TryFireBotVolleyFromWorld(normalizedAim, muzzles, Random.Range(-FireIntervalJitter, FireIntervalJitter));
        if (fired)
        {
            lastSuccessfulShotTime = Time.time;
            return;
        }

        if (Time.time - attackRunStartedAt > AttackBurstWindow && Time.time - lastSuccessfulShotTime > 0.24f)
            EnterBreakAway();
    }

    bool WouldHitOwnPirateBase(Vector3[] muzzles, Vector3 targetPosition)
    {
        int sourceViewId = bot != null ? bot.PirateBaseLaunchSourceViewId : 0;
        if (sourceViewId <= 0 || muzzles == null || muzzles.Length == 0)
            return false;

        PhotonView sourceView = PhotonView.Find(sourceViewId);
        EnemyBot sourceBase = sourceView != null ? sourceView.GetComponent<EnemyBot>() : null;
        PlayerHealth sourceHealth = sourceBase != null ? sourceBase.GetComponent<PlayerHealth>() : null;
        if (sourceBase == null || sourceBase.Kind != EnemyBotKind.PirateBase || (sourceHealth != null && sourceHealth.IsWreck))
            return false;

        for (int i = 0; i < muzzles.Length; i++)
        {
            Vector2 origin = muzzles[i];
            Vector2 toTarget = (Vector2)targetPosition - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f)
                continue;

            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, toTarget / distance, distance);
            for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
            {
                Collider2D hitCollider = hits[hitIndex].collider;
                EnemyBot hitBot = hitCollider != null ? hitCollider.GetComponentInParent<EnemyBot>() : null;
                if (hitBot == null || hitBot.Kind != EnemyBotKind.PirateBase || hitBot.photonView == null)
                    continue;

                if (hitBot.photonView.ViewID == sourceViewId)
                    return true;
            }
        }

        return false;
    }

    Vector3[] ResolveWingMuzzles()
    {
        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        float lateral = 0.46f;
        float forward = Mathf.Max(0.16f, weapon != null ? weapon.MuzzleOffsetDistance : 0.34f);
        if (renderer != null)
        {
            lateral = Mathf.Max(0.28f, renderer.bounds.extents.x * 0.72f);
            forward = Mathf.Max(forward, renderer.bounds.extents.y * 0.08f);
        }

        int streamCount = Mathf.Clamp(weapon != null && weapon.MuzzleStreamCount > 0 ? weapon.MuzzleStreamCount : 2, 1, 4);
        Vector3 center = transform.position + transform.up * forward;
        if (streamCount == 1)
            return new[] { center };

        Vector3[] muzzles = new Vector3[streamCount];
        for (int i = 0; i < streamCount; i++)
        {
            float side = Mathf.Lerp(-1f, 1f, i / (float)(streamCount - 1));
            muzzles[i] = center + transform.right * (side * lateral);
        }

        return muzzles;
    }

    static string ResolveProjectileEffectId(EnemyBotKind kind)
    {
        return kind == EnemyBotKind.PirateFighterElite || kind == EnemyBotKind.PirateFighterAce
            ? "pirate_fighter_red"
            : "pirate_fighter";
    }

    void EnterBreakAway()
    {
        mode = PirateMode.BreakAway;
        breakAwayUntil = Time.time + BreakAwayDuration;
        if (currentTarget != null)
        {
            Vector2 away = rb.position - (Vector2)currentTarget.position;
            if (away.sqrMagnitude > 0.001f)
                fallbackFleeDirection = away.normalized;
        }
    }

    bool IsBehindTarget(Transform target)
    {
        if (target == null)
            return false;

        Vector2 fromTargetToFighter = rb.position - (Vector2)target.position;
        if (fromTargetToFighter.sqrMagnitude <= 0.001f)
            return false;

        Vector2 targetForward = ResolveTargetForward(target);
        return Vector2.Dot(fromTargetToFighter.normalized, -targetForward) >= RearArcDot;
    }

    Vector2 ResolveRearAttackPoint(Transform target)
    {
        Vector2 targetForward = ResolveTargetForward(target);
        Vector2 targetVelocity = ResolveTargetVelocity(target);
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-targetForward.y, targetForward.x)
            : new Vector2(targetForward.y, -targetForward.x);
        float wave = Mathf.Sin(Time.time * 1.7f + (view != null ? view.ViewID : 1) * 0.19f) * 0.55f;
        return (Vector2)target.position - targetForward * AttackRearOffset + targetVelocity * 0.28f + tangent * wave;
    }

    Vector2 ResolveTargetForward(Transform target)
    {
        if (target == null)
            return Vector2.up;

        Rigidbody2D targetBody = target.GetComponent<Rigidbody2D>();
        if (targetBody != null && targetBody.linearVelocity.sqrMagnitude > 0.2f)
            return targetBody.linearVelocity.normalized;

        Vector2 forward = target.up;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector2.up;
    }

    Vector2 ResolveTargetVelocity(Transform target)
    {
        Rigidbody2D targetBody = target != null ? target.GetComponent<Rigidbody2D>() : null;
        return targetBody != null ? targetBody.linearVelocity : Vector2.zero;
    }

    bool IsCriticalHealth()
    {
        if (health == null || health.maxHP <= 0)
            return false;

        return health.CurrentHP <= Mathf.CeilToInt(health.maxHP * CriticalHpFraction);
    }

    void RotateNoseToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemySpaceMantaBehavior : EnemyBotBehaviorBase
{
    enum MantaMode
    {
        Patrol,
        Stalk,
        ChargeWindup,
        Dash,
        Recovery
    }

    const float ChargeWindupDuration = 1.025f;
    const float DashDuration = 0.58f;
    const float RecoveryDuration = 0.68f;
    const float DashSpeedMultiplier = 5.9f;
    const float DashHitRadiusFactor = 0.42f;
    const float AvoidanceScanRadius = 1.9f;
    const float AvoidanceWeight = 0.46f;
    const float MapEdgeMargin = 2.4f;
    const float PatrolTurnIntervalMin = 1.2f;
    const float PatrolTurnIntervalMax = 2.4f;

    readonly HashSet<int> damagedThisDash = new HashSet<int>();

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    EnemySpriteFrameAnimator frameAnimator;
    Transform currentTarget;
    Vector2 patrolDirection = Vector2.up;
    Vector2 chargeDirection = Vector2.up;
    float nextTargetRefreshTime;
    float nextRepathTime;
    float nextPatrolTurnTime;
    float nextChargeTime;
    float modeStartedAt;
    float orbitDirection = 1f;
    MantaMode mode = MantaMode.Patrol;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;
        frameAnimator = owner.GetComponent<EnemySpriteFrameAnimator>();

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.193f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        nextChargeTime = Time.time + Random.Range(0.65f, 1.45f);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        if (frameAnimator == null)
            frameAnimator = GetComponent<EnemySpriteFrameAnimator>();

        RefreshTargetIfNeeded();

        switch (mode)
        {
            case MantaMode.ChargeWindup:
                TickChargeWindup();
                break;
            case MantaMode.Dash:
                TickDash();
                break;
            case MantaMode.Recovery:
                TickRecovery();
                break;
            default:
                TickPatrolOrStalk();
                break;
        }
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView == null)
            return;

        PlayerHealth attackerHealth = attackerView.GetComponent<PlayerHealth>();
        if (attackerHealth != null && attackerHealth.IsAstronautControlled && !attackerHealth.IsEnemyAstronautControlled)
            return;

        currentTarget = attackerView.transform;
        nextTargetRefreshTime = Time.time + 0.35f;
        nextChargeTime = Mathf.Min(nextChargeTime, Time.time + 0.25f);
        if (mode == MantaMode.Patrol)
            mode = MantaMode.Stalk;
    }

    void TickPatrolOrStalk()
    {
        SetAnimationSpeed(1f);

        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            float distance = toTarget.magnitude;
            if (distance > 0.001f && distance <= ResolveChargeRange() && Time.time >= nextChargeTime)
            {
                BeginChargeWindup(toTarget / distance);
                return;
            }

            mode = MantaMode.Stalk;
        }
        else
        {
            mode = MantaMode.Patrol;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + Mathf.Max(0.12f, movement.RepathInterval);
            patrolDirection = currentTarget != null ? ResolveStalkDirection() : ResolvePatrolDirection();
        }

        Vector2 desired = ApplyAvoidance(ApplyMapEdgeSteering(patrolDirection));
        if (desired.sqrMagnitude <= 0.001f)
            desired = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : Vector2.up;

        float speedMultiplier = currentTarget != null ? 1f : 0.54f;
        Vector2 desiredVelocity = desired.normalized * (bot.EffectiveMoveSpeed * speedMultiplier);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, currentTarget != null ? 0.17f : 0.1f);

        Vector2 lookDirection = currentTarget != null
            ? (Vector2)currentTarget.position - rb.position
            : rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : desired;
        RotateNoseToward(lookDirection);
    }

    void BeginChargeWindup(Vector2 direction)
    {
        chargeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.up;
        mode = MantaMode.ChargeWindup;
        modeStartedAt = Time.time;
        rb.linearVelocity *= 0.28f;
        SetAnimationSpeed(1.75f);

        if (bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.PlaySpaceMantaWarningRpc), RpcTarget.All, rb.position.x, rb.position.y, transform.position.z);
    }

    void TickChargeWindup()
    {
        SetAnimationSpeed(1.8f);
        RefreshChargeDirection();
        RotateNoseToward(chargeDirection);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, -chargeDirection * (bot.EffectiveMoveSpeed * 0.18f), 0.2f);

        if (Time.time - modeStartedAt >= ScaleEnemyAttackWindup(ChargeWindupDuration))
            BeginDash();
    }

    void BeginDash()
    {
        mode = MantaMode.Dash;
        modeStartedAt = Time.time;
        damagedThisDash.Clear();
        SetAnimationSpeed(2.55f);
        rb.linearVelocity = chargeDirection * ResolveDashSpeed();
    }

    void TickDash()
    {
        SetAnimationSpeed(2.55f);
        RotateNoseToward(chargeDirection);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, chargeDirection * ResolveDashSpeed(), 0.36f);

        if (TryApplyDashDamage() || Time.time - modeStartedAt >= DashDuration)
            BeginRecovery();
    }

    void BeginRecovery()
    {
        mode = MantaMode.Recovery;
        modeStartedAt = Time.time;
        nextChargeTime = Time.time + ResolveChargeCooldown();
        SetAnimationSpeed(0.72f);
        rb.linearVelocity *= 0.42f;
    }

    void TickRecovery()
    {
        SetAnimationSpeed(0.72f);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, 0.08f);
        RotateNoseToward(rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : chargeDirection);

        if (Time.time - modeStartedAt >= RecoveryDuration)
            mode = currentTarget != null ? MantaMode.Stalk : MantaMode.Patrol;
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.12f, movement.TargetRefreshInterval);
        currentTarget = ResolveTarget();
    }

    Transform ResolveTarget()
    {
        float allowedRange = currentTarget != null ? movement.DisengageRadius : movement.DetectionRadius;
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, rb.position, allowedRange, true, true))
            return currentTarget;

        return EnemyTargetingUtility.FindClosestTarget(rb.position, health, movement.DetectionRadius, true, true);
    }

    Vector2 ResolvePatrolDirection()
    {
        if (Time.time >= nextPatrolTurnTime)
        {
            nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.58f, 0.58f);
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;
    }

    Vector2 ResolveStalkDirection()
    {
        if (currentTarget == null)
            return ResolvePatrolDirection();

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return ResolvePatrolDirection();

        Vector2 toward = toTarget / distance;
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-toward.y, toward.x)
            : new Vector2(toward.y, -toward.x);
        float wave = Mathf.Sin(Time.time * 1.35f + (view != null ? view.ViewID : 1) * 0.17f) * 0.16f;

        if (distance > movement.PreferredDistance)
            return (toward * 0.82f + tangent * (0.26f + wave)).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.5f + tangent * 0.78f).normalized;

        return (tangent * 0.88f + toward * 0.18f).normalized;
    }

    void RefreshChargeDirection()
    {
        if (currentTarget == null)
            return;

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        if (toTarget.sqrMagnitude <= 0.001f)
            return;

        chargeDirection = Vector2.Lerp(chargeDirection, toTarget.normalized, 0.18f).normalized;
    }

    bool TryApplyDashDamage()
    {
        float hitRadius = Mathf.Max(0.55f, bot.VisualTargetSize * DashHitRadiusFactor);
        Vector2 hitCenter = rb.position + chargeDirection * Mathf.Max(0.18f, hitRadius * 0.35f);
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(hitCenter, hitRadius, out Collider2D[] hits);
        bool appliedHit = false;
        int attackerViewId = bot.photonView != null ? bot.photonView.ViewID : 0;
        int damage = RoomSettings.GetEnemyDamage(bot.Kind);
        WeaponHitContext hitContext = weapon != null
            ? new WeaponHitContext(weapon.DamageType, weapon.DeliveryMethod, weapon.DeliveryFlags, string.Empty)
            : WeaponHitContext.None;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            PlayerHealth candidate = hit.GetComponentInParent<PlayerHealth>();
            PhotonView targetView = candidate != null ? candidate.GetComponent<PhotonView>() : null;
            if (candidate == null || targetView == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating)
                continue;

            if (!ActorIdentity.CanBeTargetedByMonstersActor(candidate) || candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            if (!damagedThisDash.Add(targetView.ViewID))
                continue;

            targetView.RPC(
                nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                RpcTarget.MasterClient,
                damage,
                damage,
                attackerViewId,
                hitCenter.x,
                hitCenter.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
            appliedHit = true;
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted || beacon.photonView == null)
                continue;

            if (Vector2.Distance(hitCenter, beacon.transform.position) > hitRadius)
                continue;

            if (!damagedThisDash.Add(beacon.photonView.ViewID))
                continue;

            beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageProfileAt), RpcTarget.MasterClient, damage, damage, attackerViewId, hitCenter.x, hitCenter.y);
            appliedHit = true;
        }

        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (deployable == null || !deployable.CanBeTargeted || deployable.photonView == null)
                continue;

            if (Vector2.Distance(hitCenter, deployable.transform.position) > hitRadius)
                continue;

            if (!damagedThisDash.Add(deployable.photonView.ViewID))
                continue;

            deployable.photonView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                damage,
                damage,
                attackerViewId,
                hitCenter.x,
                hitCenter.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
            appliedHit = true;
        }

        return appliedHit;
    }

    Vector2 ApplyAvoidance(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(rb.position, AvoidanceScanRadius, out Collider2D[] hits);
        Vector2 avoidance = Vector2.zero;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            if (!IsAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(rb.position);
            Vector2 away = rb.position - closest;
            if (away.sqrMagnitude <= 0.0001f)
                away = rb.position - (Vector2)hit.transform.position;

            float distance = Mathf.Max(0.12f, away.magnitude);
            if (distance > AvoidanceScanRadius)
                continue;

            avoidance += away.normalized * Mathf.Clamp01((AvoidanceScanRadius - distance) / AvoidanceScanRadius);
        }

        if (avoidance.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desired + avoidance.normalized * AvoidanceWeight).normalized;
    }

    bool IsAvoidedObject(Collider2D hit)
    {
        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        return hit.GetComponentInParent<DroppedCargoCrate>() != null;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.2f, bot.EffectiveMoveSpeed * 1.8f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + MapEdgeMargin, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - MapEdgeMargin, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + MapEdgeMargin, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - MapEdgeMargin, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desired * 0.38f + inward.normalized * 0.78f).normalized;
    }

    float ResolveChargeRange()
    {
        return weapon != null && weapon.Range > 0f ? weapon.Range : Mathf.Max(6f, movement.ShootDistance);
    }

    float ResolveChargeCooldown()
    {
        return ScaleEnemyAttackCooldown(weapon != null && weapon.FireRate > 0f ? weapon.FireRate : 3.4f);
    }

    float ResolveDashSpeed()
    {
        return Mathf.Max(bot.EffectiveMoveSpeed * DashSpeedMultiplier, bot.EffectiveMoveSpeed + 5.2f);
    }

    void SetAnimationSpeed(float multiplier)
    {
        if (frameAnimator == null)
            frameAnimator = GetComponent<EnemySpriteFrameAnimator>();

        if (frameAnimator != null)
            frameAnimator.SetSpeedMultiplier(multiplier);
    }

    void RotateNoseToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyGravitySquidBehavior : EnemyBotBehaviorBase
{
    enum SquidMode
    {
        Patrol,
        Stalk,
        Windup,
        Channel,
        Recovery
    }

    const float WindupDuration = 1.05f;
    const float ChannelDuration = 2.45f;
    const float RecoveryDuration = 0.95f;
    const float BreakDistance = 13.2f;
    const float DamageTickInterval = 0.5f;
    const float PullTickInterval = 0.16f;
    const float PullEffectDuration = 0.28f;
    const float PullAcceleration = 12.2f;
    const float PullMaxSpeed = 8.2f;
    const float ChannelMoveSpeedMultiplier = 0.52f;
    const float AvoidanceScanRadius = 2.05f;
    const float AvoidanceWeight = 0.42f;
    const float MapEdgeMargin = 2.6f;
    const float PatrolTurnIntervalMin = 1.2f;
    const float PatrolTurnIntervalMax = 2.3f;

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    EnemySpriteFrameAnimator frameAnimator;
    Transform currentTarget;
    Vector2 patrolDirection = Vector2.up;
    int activeTargetViewId;
    float nextTargetRefreshTime;
    float nextRepathTime;
    float nextPatrolTurnTime;
    float nextAttackTime;
    float nextDamageTickTime;
    float nextPullTickTime;
    float modeStartedAt;
    float orbitDirection = 1f;
    SquidMode mode = SquidMode.Patrol;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;
        frameAnimator = owner.GetComponent<EnemySpriteFrameAnimator>();

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.217f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        nextAttackTime = Time.time + Random.Range(1.1f, 2.2f);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        if (frameAnimator == null)
            frameAnimator = GetComponent<EnemySpriteFrameAnimator>();

        switch (mode)
        {
            case SquidMode.Windup:
                TickWindup();
                break;
            case SquidMode.Channel:
                TickChannel();
                break;
            case SquidMode.Recovery:
                TickRecovery();
                break;
            default:
                RefreshTargetIfNeeded();
                TickPatrolOrStalk();
                break;
        }
    }

    void OnDisable()
    {
        StopActiveTetherVfx();
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView == null)
            return;

        PlayerHealth attackerHealth = attackerView.GetComponent<PlayerHealth>();
        if (attackerHealth != null && attackerHealth.IsAstronautControlled && !attackerHealth.IsEnemyAstronautControlled)
            return;

        currentTarget = attackerView.transform;
        nextTargetRefreshTime = Time.time + 0.35f;
        nextAttackTime = Mathf.Min(nextAttackTime, Time.time + 0.35f);
        if (mode == SquidMode.Patrol)
            mode = SquidMode.Stalk;
    }

    void TickPatrolOrStalk()
    {
        SetAnimationSpeed(currentTarget != null ? 1.08f : 0.82f);

        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            float distance = toTarget.magnitude;
            if (distance > 0.001f && distance <= ResolveTetherRange() && Time.time >= nextAttackTime)
            {
                BeginWindup(currentTarget);
                return;
            }

            mode = SquidMode.Stalk;
        }
        else
        {
            mode = SquidMode.Patrol;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + Mathf.Max(0.12f, movement.RepathInterval);
            patrolDirection = currentTarget != null ? ResolveStalkDirection() : ResolvePatrolDirection();
        }

        Vector2 desired = ApplyAvoidance(ApplyMapEdgeSteering(patrolDirection));
        if (desired.sqrMagnitude <= 0.001f)
            desired = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : Vector2.up;

        float speedMultiplier = currentTarget != null ? 1f : 0.5f;
        Vector2 desiredVelocity = desired.normalized * (bot.EffectiveMoveSpeed * speedMultiplier);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, currentTarget != null ? 0.16f : 0.09f);

        Vector2 lookDirection = currentTarget != null
            ? (Vector2)currentTarget.position - rb.position
            : rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : desired;
        RotateToward(lookDirection);
    }

    void BeginWindup(Transform target)
    {
        PhotonView targetView = target != null ? target.GetComponentInParent<PhotonView>() : null;
        if (targetView == null)
            return;

        activeTargetViewId = targetView.ViewID;
        mode = SquidMode.Windup;
        modeStartedAt = Time.time;
        rb.linearVelocity *= 0.28f;
        SetAnimationSpeed(1.55f);

        if (bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.PlayGravitySquidWarningRpc), RpcTarget.All, rb.position.x, rb.position.y, transform.position.z);
    }

    void TickWindup()
    {
        SetAnimationSpeed(1.65f);
        if (!IsActiveTargetStillValid(ResolveTetherRange() + 1.5f))
        {
            BeginRecovery();
            return;
        }

        Transform target = ResolveActiveTargetTransform();
        Vector2 toTarget = target != null ? (Vector2)target.position - rb.position : patrolDirection;
        RotateToward(toTarget);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, -toTarget.normalized * (bot.EffectiveMoveSpeed * 0.12f), 0.18f);

        if (Time.time - modeStartedAt >= ScaleEnemyAttackWindup(WindupDuration))
            BeginChannel();
    }

    void BeginChannel()
    {
        mode = SquidMode.Channel;
        modeStartedAt = Time.time;
        nextDamageTickTime = 0f;
        nextPullTickTime = 0f;
        SetAnimationSpeed(2.05f);

        if (bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.StartGravitySquidTetherRpc), RpcTarget.All, activeTargetViewId);
    }

    void TickChannel()
    {
        SetAnimationSpeed(2.05f);
        if (!IsActiveTargetStillValid(BreakDistance))
        {
            BeginRecovery();
            return;
        }

        Transform target = ResolveActiveTargetTransform();
        Vector2 toTarget = target != null ? (Vector2)target.position - rb.position : patrolDirection;
        RotateToward(toTarget);

        Vector2 channelDirection = ResolveChannelMoveDirection(target);
        rb.linearVelocity = Vector2.Lerp(
            rb.linearVelocity,
            channelDirection * (bot.EffectiveMoveSpeed * ChannelMoveSpeedMultiplier),
            0.13f);

        if (Time.time >= nextDamageTickTime)
        {
            nextDamageTickTime = Time.time + DamageTickInterval;
            ApplyTetherDamageTick();
        }

        if (Time.time >= nextPullTickTime)
        {
            nextPullTickTime = Time.time + PullTickInterval;
            ApplyTetherPullTick();
        }

        if (Time.time - modeStartedAt >= ChannelDuration)
            BeginRecovery();
    }

    void BeginRecovery()
    {
        StopActiveTetherVfx();
        mode = SquidMode.Recovery;
        modeStartedAt = Time.time;
        nextAttackTime = Time.time + ResolveTetherCooldown();
        SetAnimationSpeed(0.72f);
        rb.linearVelocity *= 0.55f;
        activeTargetViewId = 0;
    }

    void TickRecovery()
    {
        SetAnimationSpeed(0.72f);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, 0.065f);
        RotateToward(rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : patrolDirection);

        if (Time.time - modeStartedAt >= RecoveryDuration)
        {
            RefreshTargetIfNeeded();
            mode = currentTarget != null ? SquidMode.Stalk : SquidMode.Patrol;
        }
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.12f, movement.TargetRefreshInterval);
        currentTarget = ResolveTarget();
    }

    Transform ResolveTarget()
    {
        float allowedRange = currentTarget != null ? movement.DisengageRadius : movement.DetectionRadius;
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, rb.position, allowedRange, true, true))
            return currentTarget;

        return EnemyTargetingUtility.FindClosestTarget(rb.position, health, movement.DetectionRadius, true, true);
    }

    Vector2 ResolvePatrolDirection()
    {
        if (Time.time >= nextPatrolTurnTime)
        {
            nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.48f, 0.48f);
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;
    }

    Vector2 ResolveStalkDirection()
    {
        if (currentTarget == null)
            return ResolvePatrolDirection();

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return ResolvePatrolDirection();

        Vector2 toward = toTarget / distance;
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-toward.y, toward.x)
            : new Vector2(toward.y, -toward.x);
        float wave = Mathf.Sin(Time.time * 1.15f + (view != null ? view.ViewID : 1) * 0.17f) * 0.14f;

        if (distance > movement.PreferredDistance)
            return (toward * 0.74f + tangent * (0.34f + wave)).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.62f + tangent * 0.72f).normalized;

        return (tangent * 0.9f + toward * 0.1f).normalized;
    }

    Vector2 ResolveChannelMoveDirection(Transform target)
    {
        if (target == null)
            return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;

        Vector2 toTarget = (Vector2)target.position - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return Vector2.zero;

        Vector2 toward = toTarget / distance;
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-toward.y, toward.x)
            : new Vector2(toward.y, -toward.x);

        if (distance > movement.PreferredDistance + 1.2f)
            return (toward * 0.36f + tangent * 0.24f).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.54f + tangent * 0.32f).normalized;

        return (tangent * 0.42f + toward * 0.05f).normalized;
    }

    bool IsActiveTargetStillValid(float maxDistance)
    {
        PhotonView targetView = activeTargetViewId > 0 ? PhotonView.Find(activeTargetViewId) : null;
        if (targetView == null)
            return false;

        float distance = Vector2.Distance(rb.position, targetView.transform.position);
        if (distance > maxDistance)
            return false;

        LureBeaconDecoy beacon = targetView.GetComponent<LureBeaconDecoy>();
        if (beacon != null)
            return beacon.CanBeTargeted;

        PlayerDeployableBase deployable = targetView.GetComponent<PlayerDeployableBase>();
        if (deployable != null)
            return deployable.CanBeTargeted;

        PlayerHealth player = targetView.GetComponent<PlayerHealth>();
        return player != null && player != health && !player.IsWreck && !player.IsBotControlled && ActorIdentity.CanBeTargetedByMonstersActor(player) && !player.IsEvacuationAnimating;
    }

    Transform ResolveActiveTargetTransform()
    {
        PhotonView targetView = activeTargetViewId > 0 ? PhotonView.Find(activeTargetViewId) : null;
        return targetView != null ? targetView.transform : null;
    }

    void ApplyTetherDamageTick()
    {
        PhotonView targetView = activeTargetViewId > 0 ? PhotonView.Find(activeTargetViewId) : null;
        if (targetView == null)
            return;

        Vector2 impact = ResolveTargetImpactPoint(targetView.transform);
        int attackerViewId = bot.photonView != null ? bot.photonView.ViewID : 0;
        int baseDamagePerSecond = Mathf.Max(0, RoomSettings.GetEnemyDamage(bot.Kind));
        int shieldDamage = Mathf.Max(1, Mathf.CeilToInt(baseDamagePerSecond * DamageTickInterval));
        int hpDamage = Mathf.Max(1, Mathf.CeilToInt(baseDamagePerSecond * 0.5f * DamageTickInterval));
        WeaponHitContext hitContext = weapon != null
            ? new WeaponHitContext(weapon.DamageType, weapon.DeliveryMethod, weapon.DeliveryFlags, string.Empty)
            : WeaponHitContext.None;

        LureBeaconDecoy beacon = targetView.GetComponent<LureBeaconDecoy>();
        if (beacon != null)
        {
            if (beacon.CanBeTargeted)
                beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageProfileAt), RpcTarget.MasterClient, shieldDamage, hpDamage, attackerViewId, impact.x, impact.y);
            return;
        }

        PlayerDeployableBase deployable = targetView.GetComponent<PlayerDeployableBase>();
        if (deployable != null)
        {
            if (deployable.CanBeTargeted)
                deployable.photonView.RPC(
                    nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                    RpcTarget.MasterClient,
                    shieldDamage,
                    hpDamage,
                    attackerViewId,
                    impact.x,
                    impact.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
            return;
        }

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        if (targetHealth != null && targetHealth != health && !targetHealth.IsWreck && !targetHealth.IsBotControlled && ActorIdentity.CanBeTargetedByMonstersActor(targetHealth) && !targetHealth.IsEvacuationAnimating)
            targetView.RPC(
                nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                RpcTarget.MasterClient,
                shieldDamage,
                hpDamage,
                attackerViewId,
                impact.x,
                impact.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
    }

    void ApplyTetherPullTick()
    {
        PhotonView targetView = activeTargetViewId > 0 ? PhotonView.Find(activeTargetViewId) : null;
        if (targetView == null || targetView.Owner == null)
            return;

        if (targetView.GetComponent<LureBeaconDecoy>() != null || targetView.GetComponent<PlayerDeployableBase>() != null)
            return;

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        if (targetHealth == null || targetHealth == health || targetHealth.IsWreck || targetHealth.IsBotControlled || !ActorIdentity.CanBeTargetedByMonstersActor(targetHealth) || targetHealth.IsEvacuationAnimating)
            return;

        int sourceViewId = bot.photonView != null ? bot.photonView.ViewID : 0;
        targetView.RPC(
            nameof(PlayerHealth.ApplyGravityTetherPullRpc),
            targetView.Owner,
            sourceViewId,
            rb.position.x,
            rb.position.y,
            PullAcceleration,
            PullMaxSpeed,
            PullEffectDuration);
    }

    Vector2 ResolveTargetImpactPoint(Transform target)
    {
        if (target == null)
            return rb.position;

        Collider2D collider = target.GetComponent<Collider2D>();
        if (collider != null)
            return collider.ClosestPoint(rb.position);

        return target.position;
    }

    Vector2 ApplyAvoidance(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(rb.position, AvoidanceScanRadius, out Collider2D[] hits);
        Vector2 avoidance = Vector2.zero;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            if (!IsAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(rb.position);
            Vector2 away = rb.position - closest;
            if (away.sqrMagnitude <= 0.0001f)
                away = rb.position - (Vector2)hit.transform.position;

            float distance = Mathf.Max(0.12f, away.magnitude);
            if (distance > AvoidanceScanRadius)
                continue;

            avoidance += away.normalized * Mathf.Clamp01((AvoidanceScanRadius - distance) / AvoidanceScanRadius);
        }

        if (avoidance.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desired + avoidance.normalized * AvoidanceWeight).normalized;
    }

    bool IsAvoidedObject(Collider2D hit)
    {
        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        return hit.GetComponentInParent<DroppedCargoCrate>() != null;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.2f, bot.EffectiveMoveSpeed * 1.9f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + MapEdgeMargin, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - MapEdgeMargin, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + MapEdgeMargin, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - MapEdgeMargin, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desired * 0.36f + inward.normalized * 0.72f).normalized;
    }

    float ResolveTetherRange()
    {
        return weapon != null && weapon.Range > 0f ? weapon.Range : Mathf.Max(8f, movement.ShootDistance);
    }

    float ResolveTetherCooldown()
    {
        return ScaleEnemyAttackCooldown(weapon != null && weapon.FireRate > 0f ? weapon.FireRate : 7f);
    }

    void StopActiveTetherVfx()
    {
        if (view == null || !view.IsMine || activeTargetViewId <= 0 || bot == null || bot.photonView == null)
            return;

        bot.photonView.RPC(nameof(EnemyBot.StopGravitySquidTetherRpc), RpcTarget.All);
    }

    void SetAnimationSpeed(float multiplier)
    {
        if (frameAnimator == null)
            frameAnimator = GetComponent<EnemySpriteFrameAnimator>();

        if (frameAnimator != null)
            frameAnimator.SetSpeedMultiplier(multiplier);
    }

    void RotateToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyHunterLanceBehavior : EnemyBotBehaviorBase
{
    enum LanceMode
    {
        Patrol,
        Stalk,
        LockOn,
        Recovery
    }

    const float LockOnDuration = 1.125f;
    const float RecoveryDuration = 0.82f;
    const float BeamRadius = 0.46f;
    const float HpDamageMultiplier = 0.48f;
    const float LockReverseSpeedMultiplier = 0.18f;
    const float AvoidanceScanRadius = 2.1f;
    const float AvoidanceWeight = 0.44f;
    const float MapEdgeMargin = 2.6f;
    const float PatrolTurnIntervalMin = 1.05f;
    const float PatrolTurnIntervalMax = 2.05f;

    readonly HashSet<int> damagedThisShot = new HashSet<int>();

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    Transform currentTarget;
    Vector2 patrolDirection = Vector2.up;
    Vector2 lockedDirection = Vector2.up;
    int lockedTargetViewId;
    float nextTargetRefreshTime;
    float nextRepathTime;
    float nextPatrolTurnTime;
    float nextShotTime;
    float modeStartedAt;
    float orbitDirection = 1f;
    LanceMode mode = LanceMode.Patrol;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.239f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        nextShotTime = Time.time + Random.Range(1.0f, 1.8f);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        switch (mode)
        {
            case LanceMode.LockOn:
                TickLockOn();
                break;
            case LanceMode.Recovery:
                TickRecovery();
                break;
            default:
                RefreshTargetIfNeeded();
                TickPatrolOrStalk();
                break;
        }
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView == null)
            return;

        currentTarget = attackerView.transform;
        nextTargetRefreshTime = Time.time + 0.35f;
        nextShotTime = Mathf.Min(nextShotTime, Time.time + 0.35f);
        if (mode == LanceMode.Patrol)
            mode = LanceMode.Stalk;
    }

    void TickPatrolOrStalk()
    {
        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            float distance = toTarget.magnitude;
            if (distance > 0.001f && distance <= ResolveBeamRange() && Time.time >= nextShotTime)
            {
                BeginLockOn(currentTarget);
                return;
            }

            mode = LanceMode.Stalk;
        }
        else
        {
            mode = LanceMode.Patrol;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + Mathf.Max(0.12f, movement.RepathInterval);
            patrolDirection = currentTarget != null ? ResolveStalkDirection() : ResolvePatrolDirection();
        }

        Vector2 desired = ApplyAvoidance(ApplyMapEdgeSteering(patrolDirection));
        if (desired.sqrMagnitude <= 0.001f)
            desired = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : Vector2.up;

        float speedMultiplier = currentTarget != null ? 1f : 0.58f;
        Vector2 desiredVelocity = desired.normalized * (bot.EffectiveMoveSpeed * speedMultiplier);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, currentTarget != null ? 0.17f : 0.1f);

        Vector2 lookDirection = currentTarget != null
            ? (Vector2)currentTarget.position - rb.position
            : rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : desired;
        RotateNoseToward(lookDirection);
    }

    void BeginLockOn(Transform target)
    {
        PhotonView targetView = target != null ? target.GetComponentInParent<PhotonView>() : null;
        if (targetView == null)
            return;

        lockedTargetViewId = targetView.ViewID;
        lockedDirection = ResolvePredictedAimDirection(target);
        if (lockedDirection.sqrMagnitude <= 0.001f)
            lockedDirection = transform.up;

        mode = LanceMode.LockOn;
        modeStartedAt = Time.time;
        rb.linearVelocity *= 0.24f;
        RotateNoseToward(lockedDirection);

        Vector2 origin = ResolveMuzzlePosition(lockedDirection);
        float range = ResolveBlockedBeamRange(origin, lockedDirection, ResolveBeamRange());
        if (bot.photonView != null)
        {
            bot.photonView.RPC(nameof(EnemyBot.PlayHunterLanceLockRpc), RpcTarget.All, rb.position.x, rb.position.y, transform.position.z);
            bot.photonView.RPC(nameof(EnemyBot.SpawnHunterLanceAimRpc), RpcTarget.All, origin.x, origin.y, lockedDirection.x, lockedDirection.y, range, ResolveLockOnDuration());
        }
    }

    void TickLockOn()
    {
        Transform target = ResolveLockedTargetTransform();
        if (!IsLockedTargetStillValid(ResolveBeamRange() + 2f))
        {
            BeginRecovery(0.45f);
            return;
        }

        RotateNoseToward(lockedDirection);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, -lockedDirection * (bot.EffectiveMoveSpeed * LockReverseSpeedMultiplier), 0.18f);

        float lockOnDuration = ResolveLockOnDuration();
        if (target != null && Time.time - modeStartedAt < lockOnDuration * 0.45f)
            lockedDirection = Vector2.Lerp(lockedDirection, ResolvePredictedAimDirection(target), 0.025f).normalized;

        if (Time.time - modeStartedAt >= lockOnDuration)
            FireLance();
    }

    void FireLance()
    {
        Vector2 direction = lockedDirection.sqrMagnitude > 0.001f ? lockedDirection.normalized : Vector2.up;
        Vector2 origin = ResolveMuzzlePosition(direction);
        float range = ResolveBlockedBeamRange(origin, direction, ResolveBeamRange());

        if (bot.photonView != null)
        {
            bot.photonView.RPC(nameof(EnemyBot.PlayHunterLanceFireRpc), RpcTarget.All, origin.x, origin.y, transform.position.z);
            bot.photonView.RPC(nameof(EnemyBot.SpawnHunterLanceShotRpc), RpcTarget.All, origin.x, origin.y, direction.x, direction.y, range);
        }

        ApplyLanceDamage(origin, direction, range);
        BeginRecovery(RecoveryDuration);
    }

    void BeginRecovery(float duration)
    {
        mode = LanceMode.Recovery;
        modeStartedAt = Time.time;
        nextShotTime = Time.time + ResolveShotCooldown();
        rb.linearVelocity *= 0.55f;
        lockedTargetViewId = 0;
        recoveryDuration = Mathf.Max(0.1f, duration);
    }

    float recoveryDuration = RecoveryDuration;

    void TickRecovery()
    {
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, 0.065f);
        RotateNoseToward(rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : lockedDirection);

        if (Time.time - modeStartedAt >= recoveryDuration)
        {
            RefreshTargetIfNeeded();
            mode = currentTarget != null ? LanceMode.Stalk : LanceMode.Patrol;
        }
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.12f, movement.TargetRefreshInterval);
        currentTarget = ResolveTarget();
    }

    Transform ResolveTarget()
    {
        float allowedRange = currentTarget != null ? movement.DisengageRadius : movement.DetectionRadius;
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, rb.position, allowedRange, true))
            return currentTarget;

        return EnemyTargetingUtility.FindClosestTarget(rb.position, health, movement.DetectionRadius, true);
    }

    Vector2 ResolvePatrolDirection()
    {
        if (Time.time >= nextPatrolTurnTime)
        {
            nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.46f, 0.46f);
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;
    }

    Vector2 ResolveStalkDirection()
    {
        if (currentTarget == null)
            return ResolvePatrolDirection();

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return ResolvePatrolDirection();

        Vector2 toward = toTarget / distance;
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-toward.y, toward.x)
            : new Vector2(toward.y, -toward.x);
        float wave = Mathf.Sin(Time.time * 1.05f + (view != null ? view.ViewID : 1) * 0.13f) * 0.12f;

        if (distance > movement.PreferredDistance)
            return (toward * 0.76f + tangent * (0.28f + wave)).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.66f + tangent * 0.58f).normalized;

        return (tangent * 0.86f + toward * 0.08f).normalized;
    }

    Vector2 ResolvePredictedAimDirection(Transform target)
    {
        Vector2 targetPosition = target != null ? (Vector2)target.position : rb.position + patrolDirection;
        Rigidbody2D targetBody = target != null ? target.GetComponent<Rigidbody2D>() : null;
        float distance = Vector2.Distance(rb.position, targetPosition);
        if (targetBody != null)
        {
            float leadTime = Mathf.Clamp(distance / 16f, 0.12f, 0.46f);
            targetPosition += targetBody.linearVelocity * leadTime;
        }

        Vector2 direction = targetPosition - rb.position;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : patrolDirection;
    }

    Transform ResolveLockedTargetTransform()
    {
        PhotonView targetView = lockedTargetViewId > 0 ? PhotonView.Find(lockedTargetViewId) : null;
        return targetView != null ? targetView.transform : null;
    }

    bool IsLockedTargetStillValid(float maxDistance)
    {
        PhotonView targetView = lockedTargetViewId > 0 ? PhotonView.Find(lockedTargetViewId) : null;
        if (targetView == null)
            return false;

        if (Vector2.Distance(rb.position, targetView.transform.position) > maxDistance)
            return false;

        LureBeaconDecoy beacon = targetView.GetComponent<LureBeaconDecoy>();
        if (beacon != null)
            return beacon.CanBeTargeted;

        PlayerDeployableBase deployable = targetView.GetComponent<PlayerDeployableBase>();
        if (deployable != null)
            return deployable.CanBeTargeted;

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        return targetHealth != null && targetHealth != health && !targetHealth.IsWreck && !targetHealth.IsBotControlled && !targetHealth.IsAstronautControlled && !targetHealth.IsEvacuationAnimating;
    }

    Vector2 ResolveMuzzlePosition(Vector2 direction)
    {
        Vector2 forward = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        return rb.position + forward * Mathf.Max(0.35f, bot.VisualTargetSize * 0.43f);
    }

    float ResolveBlockedBeamRange(Vector2 origin, Vector2 direction, float maxRange)
    {
        Vector2 normalized = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, BeamRadius * 0.92f, normalized, maxRange);
        float closestBlockDistance = maxRange;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            Collider2D hitCollider = hit.collider;
            if (hitCollider == null || hitCollider.attachedRigidbody == rb)
                continue;

            float hitDistance = hit.distance;
            if (hitDistance <= 0.04f || hitDistance >= closestBlockDistance)
                continue;

            if (!IsBeamBlockingObject(hitCollider))
                continue;

            closestBlockDistance = Mathf.Max(0.08f, hitDistance - 0.06f);
        }

        return closestBlockDistance;
    }

    void ApplyLanceDamage(Vector2 origin, Vector2 direction, float range)
    {
        damagedThisShot.Clear();
        int attackerViewId = bot.photonView != null ? bot.photonView.ViewID : 0;
        int baseDamage = Mathf.Max(0, RoomSettings.GetEnemyDamage(bot.Kind));
        int shieldDamage = Mathf.Max(1, baseDamage);
        int hpDamage = Mathf.Max(1, Mathf.CeilToInt(baseDamage * HpDamageMultiplier));
        WeaponHitContext hitContext = weapon != null
            ? new WeaponHitContext(weapon.DamageType, weapon.DeliveryMethod, weapon.DeliveryFlags, string.Empty)
            : WeaponHitContext.None;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, BeamRadius, direction.normalized, range);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.attachedRigidbody == rb)
                continue;

            PlayerHealth candidate = hitCollider.GetComponentInParent<PlayerHealth>();
            PhotonView targetView = candidate != null ? candidate.GetComponent<PhotonView>() : null;
            if (candidate == null || targetView == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating)
                continue;

            if (candidate.IsBotControlled || candidate.IsAstronautControlled || candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            if (!damagedThisShot.Add(targetView.ViewID))
                continue;

            Vector2 impact = hits[i].point.sqrMagnitude > 0.001f ? hits[i].point : candidate.transform.position;
            targetView.RPC(
                nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                RpcTarget.MasterClient,
                shieldDamage,
                hpDamage,
                attackerViewId,
                impact.x,
                impact.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted || beacon.photonView == null)
                continue;

            if (!IsPointInsideBeam(beacon.transform.position, origin, direction, range, BeamRadius))
                continue;

            if (!damagedThisShot.Add(beacon.photonView.ViewID))
                continue;

            Vector2 impact = ResolveClosestPointOnBeam(beacon.transform.position, origin, direction, range);
            beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageProfileAt), RpcTarget.MasterClient, shieldDamage, hpDamage, attackerViewId, impact.x, impact.y);
        }

        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (deployable == null || !deployable.CanBeTargeted || deployable.photonView == null)
                continue;

            if (!IsPointInsideBeam(deployable.transform.position, origin, direction, range, BeamRadius))
                continue;

            if (!damagedThisShot.Add(deployable.photonView.ViewID))
                continue;

            Vector2 impact = ResolveClosestPointOnBeam(deployable.transform.position, origin, direction, range);
            deployable.photonView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                shieldDamage,
                hpDamage,
                attackerViewId,
                impact.x,
                impact.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }
    }

    bool IsPointInsideBeam(Vector2 point, Vector2 origin, Vector2 direction, float range, float radius)
    {
        Vector2 closest = ResolveClosestPointOnBeam(point, origin, direction, range);
        return Vector2.Distance(point, closest) <= radius;
    }

    Vector2 ResolveClosestPointOnBeam(Vector2 point, Vector2 origin, Vector2 direction, float range)
    {
        Vector2 normalized = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        float projection = Vector2.Dot(point - origin, normalized);
        projection = Mathf.Clamp(projection, 0f, Mathf.Max(0.01f, range));
        return origin + normalized * projection;
    }

    Vector2 ApplyAvoidance(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(rb.position, AvoidanceScanRadius, out Collider2D[] hits);
        Vector2 avoidance = Vector2.zero;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            if (!IsAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(rb.position);
            Vector2 away = rb.position - closest;
            if (away.sqrMagnitude <= 0.0001f)
                away = rb.position - (Vector2)hit.transform.position;

            float distance = Mathf.Max(0.12f, away.magnitude);
            if (distance > AvoidanceScanRadius)
                continue;

            avoidance += away.normalized * Mathf.Clamp01((AvoidanceScanRadius - distance) / AvoidanceScanRadius);
        }

        if (avoidance.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desired + avoidance.normalized * AvoidanceWeight).normalized;
    }

    bool IsAvoidedObject(Collider2D hit)
    {
        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        return hit.GetComponentInParent<DroppedCargoCrate>() != null;
    }

    bool IsBeamBlockingObject(Collider2D hit)
    {
        return IsAvoidedObject(hit);
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.2f, bot.EffectiveMoveSpeed * 1.85f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + MapEdgeMargin, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - MapEdgeMargin, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + MapEdgeMargin, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - MapEdgeMargin, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desired * 0.34f + inward.normalized * 0.76f).normalized;
    }

    float ResolveBeamRange()
    {
        return weapon != null && weapon.Range > 0f ? weapon.Range : Mathf.Max(10f, movement.ShootDistance);
    }

    float ResolveLockOnDuration()
    {
        return ScaleEnemyAttackWindup(LockOnDuration);
    }

    float ResolveShotCooldown()
    {
        return ScaleEnemyAttackCooldown(weapon != null && weapon.FireRate > 0f ? weapon.FireRate : 4.6f);
    }

    void RotateNoseToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyMothershipBehavior : EnemyBotBehaviorBase
{
    sealed class TurretRuntime
    {
        public Transform Root;
        public int Ammo;
        public float NextFireTime;
        public float ReloadFinishTime;
        public bool Reloading;
    }

    const float TurretFullTurnDuration = 4f;
    const float DamageSourceChaseDuration = 5f;
    const float ShieldRegenPerSecond = 2f;
    const float TurretPivotOffsetFromCenterFactor = 1f / 6f;
    const float TurretTargetSizeFactor = 0.285f;
    const float TurretMinimumTargetSize = 1.02f;

    static Sprite cachedTurretSprite;
    static Sprite cachedTurretWreckSprite;

    public static void PrewarmTurretAssets()
    {
        PrewarmSpriteTexture(LoadTurretSprite());
        PrewarmSpriteTexture(LoadTurretWreckSprite());
    }

    readonly TurretRuntime[] turrets = new TurretRuntime[6];
    readonly Vector2[] turretOffsetFactors =
    {
        new Vector2(0.28f, -0.01f),
        new Vector2(0.1f, 0.14f),
        new Vector2(0.11f, -0.13f),
        new Vector2(-0.16f, 0.23f),
        new Vector2(-0.25f, -0.01f),
        new Vector2(-0.16f, -0.23f)
    };

    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    SpriteRenderer mothershipRenderer;
    Vector2 orbitCenter;
    float orbitRadius;
    float orbitAngle;
    float orbitDirection = 1f;
    float nextTargetRefreshTime;
    float forcedDirectionUntil;
    float shieldRegenAccumulator;
    Transform currentTarget;
    Vector2 forcedMoveDirection;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        mothershipRenderer = owner.GetComponent<SpriteRenderer>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        orbitCenter = Vector2.zero;
        orbitRadius = Mathf.Max(7f, Mathf.Min(mapSize.x, mapSize.y) * (movement != null ? movement.OrbitRadiusFactor : 0.38f));
        int seed = view != null ? view.ViewID : 1;
        orbitAngle = Mathf.Abs(seed * 0.119f) % (Mathf.PI * 2f);
        orbitDirection = seed % 2 == 0 ? 1f : -1f;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                weapon.AmmoCount,
                weapon.ReloadDuration,
                RoomSettings.GetEnemyDamage(owner.Kind),
                weapon.BulletScaleMultiplier,
                weapon.BulletColor,
                weapon.MuzzleOffsetDistance,
                weapon.InfiniteAmmo,
                weapon.BulletSpeed,
                weapon.ShotSoundId,
                weapon.Range,
                string.Empty,
                10f,
                weapon.DamageType,
                weapon.DeliveryMethod,
                weapon.DeliveryFlags);
        }

        EnsureTurrets();
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        EnsureTurrets();
        RegenerateShield();

        if (Time.time >= nextTargetRefreshTime)
        {
            nextTargetRefreshTime = Time.time + Mathf.Max(0.15f, movement.TargetRefreshInterval);
            currentTarget = ResolveTarget();
        }

        Vector2 desiredDirection = ResolveMoveDirection();
        Vector2 desiredVelocity = desiredDirection * bot.EffectiveMoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.1f);
        RotateHullToward(desiredVelocity);
        TickTurrets();
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        Vector2 sourcePosition = rb != null ? rb.position - Vector2.right : (Vector2)transform.position - Vector2.right;
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView != null)
            sourcePosition = attackerView.transform.position;

        Vector2 fromShipToSource = sourcePosition - (Vector2)transform.position;
        if (fromShipToSource.sqrMagnitude < 0.001f)
            fromShipToSource = rb != null && rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : Vector2.right;

        forcedMoveDirection = fromShipToSource.normalized;
        forcedDirectionUntil = Time.time + DamageSourceChaseDuration;
    }

    public void ConvertTurretsToWreckVisuals()
    {
        Sprite wreckSprite = LoadTurretWreckSprite();
        if (wreckSprite == null)
            return;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child.name != "TurretVisual")
                continue;

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.sprite = wreckSprite;
        }
    }

    Vector2 ResolveMoveDirection()
    {
        if (Time.time < forcedDirectionUntil && forcedMoveDirection.sqrMagnitude > 0.001f)
            return forcedMoveDirection.normalized;

        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            if (toTarget.sqrMagnitude > 0.001f)
                return toTarget.normalized;
        }

        orbitAngle += orbitDirection * movement.OrbitAngularSpeed * Time.fixedDeltaTime;
        Vector2 fromCenter = rb.position - orbitCenter;
        if (fromCenter.sqrMagnitude < 0.01f)
            fromCenter = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle));

        Vector2 radial = fromCenter.normalized;
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-radial.y, radial.x)
            : new Vector2(radial.y, -radial.x);
        float radialError = orbitRadius - fromCenter.magnitude;
        Vector2 orbitVelocity = tangent + radial * Mathf.Clamp(radialError * 0.12f, -0.5f, 0.5f);
        return orbitVelocity.sqrMagnitude > 0.001f ? orbitVelocity.normalized : Vector2.right;
    }

    void RotateHullToward(Vector2 desiredVelocity)
    {
        if (desiredVelocity.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(desiredVelocity.y, desiredVelocity.x) * Mathf.Rad2Deg;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    Transform ResolveTarget()
    {
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, transform.position, movement.DisengageRadius, true))
            return currentTarget;

        return FindClosestVisibleHumanTarget(movement.DetectionRadius);
    }

    Transform FindClosestVisibleHumanTarget(float maxDistance)
    {
        return EnemyTargetingUtility.FindClosestTarget(transform.position, health, maxDistance, true);
    }

    bool IsValidVisibleTarget(PlayerHealth candidate, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(candidate != null ? candidate.transform : null, health, transform.position, maxDistance, true);
    }

    void RegenerateShield()
    {
        if (health == null || health.HasBrokenShield || health.CurrentShield >= health.MaxShield)
            return;

        shieldRegenAccumulator += ShieldRegenPerSecond * Time.fixedDeltaTime;
        if (shieldRegenAccumulator < 1f)
            return;

        float amount = Mathf.Floor(shieldRegenAccumulator);
        shieldRegenAccumulator -= amount;
        health.TryRestoreShieldAuthority(amount, true);
    }

    void EnsureTurrets()
    {
        Sprite turretSprite = LoadTurretSprite();
        Vector2 shipLocalSize = GetMothershipLocalSize();

        for (int i = 0; i < turrets.Length; i++)
        {
            if (turrets[i] == null)
                turrets[i] = new TurretRuntime { Ammo = weapon != null ? Mathf.Max(1, weapon.AmmoCount) : 10 };

            if (turrets[i].Root == null)
            {
                GameObject turretObject = new GameObject("MothershipTurret_" + i);
                turretObject.transform.SetParent(transform, false);
                turrets[i].Root = turretObject.transform;
            }

            EnsureTurretVisual(turrets[i].Root, turretSprite);
            turrets[i].Root.localScale = Vector3.one;
            turrets[i].Root.localPosition = new Vector3(
                turretOffsetFactors[i].x * shipLocalSize.x,
                turretOffsetFactors[i].y * shipLocalSize.y,
                0f);
            FitTurretVisual(turrets[i].Root);
        }
    }

    Vector2 GetMothershipLocalSize()
    {
        if (mothershipRenderer != null && mothershipRenderer.sprite != null)
            return mothershipRenderer.sprite.bounds.size;

        return new Vector2(7f, 3f);
    }

    void EnsureTurretVisual(Transform turretRoot, Sprite turretSprite)
    {
        if (turretRoot == null)
            return;

        SpriteRenderer rootRenderer = turretRoot.GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;

        Transform visual = turretRoot.Find("TurretVisual");
        if (visual == null)
        {
            GameObject visualObject = new GameObject("TurretVisual");
            visualObject.transform.SetParent(turretRoot, false);
            visual = visualObject.transform;
        }

        SpriteRenderer visualRenderer = visual.GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
            visualRenderer = visual.gameObject.AddComponent<SpriteRenderer>();

        visualRenderer.sprite = turretSprite;
        visualRenderer.color = Color.white;
        if (mothershipRenderer != null)
        {
            visualRenderer.sortingLayerID = mothershipRenderer.sortingLayerID;
            visualRenderer.sortingOrder = mothershipRenderer.sortingOrder + 1;
        }
    }

    void FitTurretVisual(Transform turretRoot)
    {
        Transform visual = turretRoot != null ? turretRoot.Find("TurretVisual") : null;
        SpriteRenderer renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (renderer == null || renderer.sprite == null)
            return;

        float largest = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        if (largest <= 0.001f)
            return;

        float targetSize = Mathf.Max(TurretMinimumTargetSize, GetMothershipLocalSize().x * TurretTargetSizeFactor);
        float scale = targetSize / largest;
        visual.localScale = new Vector3(scale, scale, 1f);
        float pivotOffset = renderer.sprite.bounds.size.x * scale * TurretPivotOffsetFromCenterFactor;
        visual.localPosition = new Vector3(-pivotOffset, 0f, 0f);
        visual.localRotation = Quaternion.identity;
    }

    void TickTurrets()
    {
        if (weapon == null || shooting == null)
            return;

        float turnSpeed = 360f / TurretFullTurnDuration;
        for (int i = 0; i < turrets.Length; i++)
        {
            TurretRuntime turret = turrets[i];
            if (turret == null || turret.Root == null)
                continue;

            UpdateTurretReload(turret);
            Transform target = FindClosestVisibleHumanTargetFrom(turret.Root.position, weapon.Range);
            if (target == null)
                continue;

            Vector2 toTarget = target.position - turret.Root.position;
            if (toTarget.sqrMagnitude <= 0.001f)
                continue;

            float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 180f;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
            turret.Root.rotation = Quaternion.RotateTowards(turret.Root.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);

            if (Time.time < turret.NextFireTime || turret.Reloading || turret.Ammo <= 0)
                continue;

            Vector2 muzzleDirection = -turret.Root.right;
            Vector3 muzzlePosition = turret.Root.position + (Vector3)(muzzleDirection * GetTurretMuzzleDistance(turret.Root));
            if (shooting.FireBotProjectileFromWorld(muzzleDirection, muzzlePosition))
            {
                turret.Ammo--;
                turret.NextFireTime = Time.time + Mathf.Max(0.05f, weapon.FireRate * AtlasSuppressionStatus.GetFireIntervalMultiplier(gameObject) * RoomSettings.GetEnemyAttackCooldownMultiplier());
                if (turret.Ammo <= 0)
                {
                    turret.Reloading = true;
                    turret.ReloadFinishTime = Time.time + Mathf.Max(0f, weapon.ReloadDuration * AtlasSuppressionStatus.GetReloadMultiplier(gameObject));
                }
            }
        }
    }

    float GetTurretMuzzleDistance(Transform turretRoot)
    {
        Transform visual = turretRoot != null ? turretRoot.Find("TurretVisual") : null;
        SpriteRenderer renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (renderer == null || renderer.sprite == null)
            return Mathf.Max(0.18f, weapon != null ? weapon.MuzzleOffsetDistance : 0.18f);

        float visualWidth = renderer.sprite.bounds.size.x * Mathf.Abs(visual.lossyScale.x);
        return (visualWidth * (0.5f + TurretPivotOffsetFromCenterFactor)) + Mathf.Max(0.08f, weapon != null ? weapon.MuzzleOffsetDistance : 0.18f);
    }

    void UpdateTurretReload(TurretRuntime turret)
    {
        if (!turret.Reloading || weapon == null || Time.time < turret.ReloadFinishTime)
            return;

        turret.Reloading = false;
        turret.Ammo = Mathf.Max(1, weapon.AmmoCount);
    }

    Transform FindClosestVisibleHumanTargetFrom(Vector3 origin, float maxDistance)
    {
        return EnemyTargetingUtility.FindClosestTarget(origin, health, maxDistance, true);
    }

    static Sprite LoadTurretSprite()
    {
        if (cachedTurretSprite != null)
            return cachedTurretSprite;

        cachedTurretSprite = Resources.Load<Sprite>("wieza_mother_ship_resource");
        if (cachedTurretSprite != null)
            return cachedTurretSprite;

#if UNITY_EDITOR
        cachedTurretSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/wieza_mother_ship.png");
        if (cachedTurretSprite != null)
            return cachedTurretSprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/wieza_mother_ship.png");
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                cachedTurretSprite = sprite;
                return cachedTurretSprite;
            }
        }
#endif

        return null;
    }

    static Sprite LoadTurretWreckSprite()
    {
        if (cachedTurretWreckSprite != null)
            return cachedTurretWreckSprite;

        cachedTurretWreckSprite = Resources.Load<Sprite>("wieza_mother_ship_wrak_resource");
        if (cachedTurretWreckSprite != null)
            return cachedTurretWreckSprite;

#if UNITY_EDITOR
        cachedTurretWreckSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/wieza_mother_ship_wrak.png");
        if (cachedTurretWreckSprite != null)
            return cachedTurretWreckSprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/wieza_mother_ship_wrak.png");
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                cachedTurretWreckSprite = sprite;
                return cachedTurretWreckSprite;
            }
        }
#endif

        return null;
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyMineBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyExplosionProfile explosion;
    Vector2 driftDirection;
    float nextRetargetTime;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        explosion = owner.Definition != null ? owner.Definition.Explosion : null;

        TryResolveInstancedDriftDirection();

        if (driftDirection.sqrMagnitude <= 0.001f)
        {
            int seed = view != null ? view.ViewID : Random.Range(1, 9999);
            float angle = Mathf.Abs(seed * 0.173f) % (Mathf.PI * 2f);
            driftDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (driftDirection.sqrMagnitude <= 0.001f)
                driftDirection = Vector2.up;
        }
    }

    void TryResolveInstancedDriftDirection()
    {
        object[] data = view != null ? view.InstantiationData : null;
        if (data == null || data.Length < 5)
            return;

        if (!(data[1] is string marker) ||
            !string.Equals(marker, EnemyBot.ContainerShipMineMarker, System.StringComparison.Ordinal))
        {
            return;
        }

        Vector2 direction = new Vector2(ConvertToFloat(data[3]), ConvertToFloat(data[4]));
        if (direction.sqrMagnitude > 0.001f)
            driftDirection = direction.normalized;
    }

    static float ConvertToFloat(object value)
    {
        if (value is float floatValue)
            return floatValue;

        if (value is double doubleValue)
            return (float)doubleValue;

        if (value is int intValue)
            return intValue;

        return 0f;
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null || explosion == null)
            return;

        if (health != null && health.IsWreck)
            return;

        Vector2 desiredVelocity = driftDirection.normalized * bot.EffectiveMoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.08f);
        rb.angularVelocity = Mathf.Lerp(rb.angularVelocity, 8f, 0.04f);

        if (Time.time >= nextRetargetTime)
        {
            nextRetargetTime = Time.time + 0.12f;
            if (IsAnyTargetInRange(explosion.TriggerRadius))
                bot.RequestDetonation();
        }
    }

    bool IsAnyTargetInRange(float radius)
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            if (bot != null && bot.ShouldIgnoreMineTriggerFor(candidate))
                continue;

            EnemyBot candidateBot = candidate.GetComponent<EnemyBot>();
            if (candidateBot != null && candidateBot.Kind == EnemyBotKind.SpaceMine)
                continue;

            if (candidateBot != null && candidateBot.Kind == EnemyBotKind.ContainerShip && (bot == null || !bot.IsPlayerPlacedMine))
                continue;

            if (Vector2.Distance(transform.position, candidate.transform.position) <= radius)
                return true;
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted)
                continue;

            if (Vector2.Distance(transform.position, beacon.transform.position) <= radius)
                return true;
        }

        return false;
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyContainerShipBehavior : EnemyBotBehaviorBase
{
    const float SpeedBoostDuration = 5f;
    const float MineDropCooldown = 15f;
    const float AutoCannonChance = 0.15f;
    const float MineRearOffset = 1.05f;
    const float MineSideOffset = 0.48f;
    const float AutoCannonRearOffset = 1.08f;
    const float CargoVisualTargetSize = 1.08f;
    const float MapEdgeMargin = 3.1f;
    const string CargoVisualName = "ContainerShipCargoVisual";
    const string CargoTopResourcePath = "Enemies/ContainerShip/container_set2_top";

    static Sprite[] cachedCargoTopSprites;

    public static void PrewarmCargoSprites()
    {
        if (cachedCargoTopSprites == null || cachedCargoTopSprites.Length == 0)
            cachedCargoTopSprites = Resources.LoadAll<Sprite>(CargoTopResourcePath);

        if (cachedCargoTopSprites == null)
            return;

        for (int i = 0; i < cachedCargoTopSprites.Length; i++)
            PrewarmSpriteTexture(cachedCargoTopSprites[i]);
    }

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    Vector2 orbitCenter;
    float orbitRadius;
    float orbitAngle;
    float orbitDirection = 1f;
    float nextMineDropTime;
    Vector2 lastMoveDirection = Vector2.left;
    SpriteRenderer cargoRenderer;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        orbitCenter = Vector2.zero;
        orbitRadius = Mathf.Max(5.6f, Mathf.Min(mapSize.x, mapSize.y) * (movement != null ? movement.OrbitRadiusFactor : 0.38f));
        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        orbitAngle = Mathf.Abs(seed * 0.137f) % (Mathf.PI * 2f);
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        EnsureCargoVisual();
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        EnsureCargoVisual();

        orbitAngle += orbitDirection * movement.OrbitAngularSpeed * Time.fixedDeltaTime;
        Vector2 fromCenter = rb.position - orbitCenter;
        if (fromCenter.sqrMagnitude < 0.01f)
            fromCenter = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle));

        Vector2 radialDirection = fromCenter.normalized;
        Vector2 tangentDirection = orbitDirection > 0f
            ? new Vector2(-radialDirection.y, radialDirection.x)
            : new Vector2(radialDirection.y, -radialDirection.x);

        float radialError = orbitRadius - fromCenter.magnitude;
        Vector2 desiredVelocity = tangentDirection * bot.EffectiveMoveSpeed + radialDirection * (radialError * 1.12f);
        desiredVelocity = ApplyMapEdgeSteering(desiredVelocity);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.14f);

        if (desiredVelocity.sqrMagnitude > 0.001f)
        {
            lastMoveDirection = desiredVelocity.normalized;
            float targetAngle = Mathf.Atan2(lastMoveDirection.y, lastMoveDirection.x) * Mathf.Rad2Deg + 180f;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }
    }

    public void NotifyDamageTaken(int attackerViewID)
    {
        if (!PhotonNetwork.IsMasterClient || bot == null || health == null || health.IsWreck)
            return;

        if (!EnemyBot.IsPlayerControlledDamageSource(attackerViewID))
            return;

        bot.ActivateTemporarySpeedMultiplier(2f, SpeedBoostDuration);
        if (Time.time < nextMineDropTime)
            return;

        nextMineDropTime = Time.time + ScaleEnemyAttackCooldown(MineDropCooldown);
        if (Random.value < AutoCannonChance)
            SpawnDefensiveAutoCannon();
        else
            SpawnDefensiveMines();
    }

    void SpawnDefensiveMines()
    {
        if (!PhotonNetwork.InRoom || view == null)
            return;

        EnemyBotDefinition mineDefinition = EnemyBotCatalog.GetDefinition(EnemyBotKind.SpaceMine);
        if (mineDefinition == null)
            return;

        Vector2 forward = ResolveForwardDirection();
        Vector2 behind = -forward;
        Vector2 side = new Vector2(-forward.y, forward.x);

        for (int i = 0; i < 2; i++)
        {
            float sideSign = i == 0 ? -1f : 1f;
            Vector2 spawnOffset = behind * MineRearOffset + side * (MineSideOffset * sideSign);
            Vector2 driftDirection = (behind * 0.86f + side * (0.18f * sideSign)).normalized;
            Vector3 spawnPosition = transform.position + (Vector3)spawnOffset;
            GameObject mineObject = PhotonNetwork.Instantiate(
                "Player",
                spawnPosition,
                Quaternion.identity,
                0,
                new object[] { mineDefinition.InstantiationMarker, EnemyBot.ContainerShipMineMarker, view.ViewID, driftDirection.x, driftDirection.y });

            if (mineObject == null)
                continue;

            EnemyBot mine = mineObject.GetComponent<EnemyBot>();
            if (mine == null)
                mine = mineObject.AddComponent<EnemyBot>();

            mine.InitializeFromPhotonData();
            Rigidbody2D mineBody = mineObject.GetComponent<Rigidbody2D>();
            if (mineBody != null)
                mineBody.linearVelocity = driftDirection * Mathf.Max(0.25f, mine.EffectiveMoveSpeed);
        }
    }

    void SpawnDefensiveAutoCannon()
    {
        if (!PhotonNetwork.InRoom || view == null)
            return;

        Vector2 forward = ResolveForwardDirection();
        Vector2 behind = -forward;
        Vector2 side = new Vector2(-forward.y, forward.x);
        float sideOffset = Random.Range(-0.22f, 0.22f);
        Vector3 spawnPosition = transform.position + (Vector3)(behind * AutoCannonRearOffset + side * sideOffset);
        float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - 90f;
        GameObject cannonObject = PhotonNetwork.Instantiate(
            "Player",
            spawnPosition,
            Quaternion.Euler(0f, 0f, angle),
            0,
            new object[] { PlayerDeployableRuntime.ContainerShipAutoCannonMarker, view.ViewID });

        if (cannonObject != null)
            PlayerDeployableRuntime.EnsureAttached(cannonObject);
    }

    Vector2 ResolveForwardDirection()
    {
        if (lastMoveDirection.sqrMagnitude > 0.001f)
            return lastMoveDirection.normalized;

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.001f)
            return rb.linearVelocity.normalized;

        return Vector2.left;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredVelocity)
    {
        if (rb == null || desiredVelocity.sqrMagnitude <= 0.001f)
            return desiredVelocity;

        Vector2 desiredDirection = desiredVelocity.normalized;
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desiredDirection * Mathf.Max(1.6f, bot.EffectiveMoveSpeed * 2.2f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= 1f;
        else if (predicted.x < -halfX)
            inward.x += 1f;

        if (predicted.y > halfY)
            inward.y -= 1f;
        else if (predicted.y < -halfY)
            inward.y += 1f;

        if (inward.sqrMagnitude <= 0.001f)
            return desiredVelocity;

        Vector2 steered = (desiredDirection * 0.42f + inward.normalized * 0.58f).normalized;
        return steered * desiredVelocity.magnitude;
    }

    void EnsureCargoVisual()
    {
        if (bot == null)
            return;

        Sprite cargoSprite = GetCargoSprite(bot.ContainerShipCargoVariantIndex);
        if (cargoSprite == null)
            return;

        if (cargoRenderer == null)
        {
            Transform existing = transform.Find(CargoVisualName);
            GameObject cargoObject = existing != null ? existing.gameObject : new GameObject(CargoVisualName);
            cargoObject.transform.SetParent(transform, false);
            cargoObject.transform.localPosition = Vector3.zero;
            cargoObject.transform.localRotation = Quaternion.identity;
            cargoRenderer = cargoObject.GetComponent<SpriteRenderer>();
            if (cargoRenderer == null)
                cargoRenderer = cargoObject.AddComponent<SpriteRenderer>();
        }

        cargoRenderer.sprite = cargoSprite;
        cargoRenderer.color = Color.white;
        cargoRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        SpriteRenderer hullRenderer = bot.GetComponent<SpriteRenderer>();
        cargoRenderer.sortingOrder = hullRenderer != null ? hullRenderer.sortingOrder + 1 : GameVisualTheme.EnemySortingOrder + 1;
        FitCargoSpriteToTargetSize(cargoRenderer, CargoVisualTargetSize);
    }

    public void HideCargoVisual()
    {
        Transform cargoTransform = cargoRenderer != null ? cargoRenderer.transform : transform.Find(CargoVisualName);
        if (cargoTransform == null)
            return;

        SpriteRenderer[] renderers = cargoTransform.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }

        cargoTransform.gameObject.SetActive(false);
        Destroy(cargoTransform.gameObject);
        cargoRenderer = null;
    }

    static Sprite GetCargoSprite(int variantIndex)
    {
        if (cachedCargoTopSprites == null || cachedCargoTopSprites.Length == 0)
            cachedCargoTopSprites = Resources.LoadAll<Sprite>(CargoTopResourcePath);

        if (cachedCargoTopSprites == null || cachedCargoTopSprites.Length == 0)
            return null;

        string expectedName = "container_set2_top_" + Mathf.Clamp(variantIndex, 0, InventoryItemCatalog.BlueprintScrapContainerVariantCount - 1);
        for (int i = 0; i < cachedCargoTopSprites.Length; i++)
        {
            Sprite sprite = cachedCargoTopSprites[i];
            if (sprite != null && string.Equals(sprite.name, expectedName, System.StringComparison.Ordinal))
                return sprite;
        }

        int clampedIndex = Mathf.Clamp(variantIndex, 0, cachedCargoTopSprites.Length - 1);
        return cachedCargoTopSprites[clampedIndex];
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }

    static void FitCargoSpriteToTargetSize(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds spriteBounds = renderer.sprite.bounds;
        float largestDimension = Mathf.Max(spriteBounds.size.x, spriteBounds.size.y);
        if (largestDimension <= 0.0001f)
            return;

        Vector3 parentScale = renderer.transform.parent != null ? renderer.transform.parent.lossyScale : Vector3.one;
        float inheritedScale = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(parentScale.x), Mathf.Abs(parentScale.y)));
        float scale = targetSize / (largestDimension * inheritedScale);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemySpaceTruckBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    ExtractionZone[] extractionZones = System.Array.Empty<ExtractionZone>();
    int targetZoneIndex;
    float nextZoneRefreshTime;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        RefreshExtractionZones(true);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        if (Time.time >= nextZoneRefreshTime || extractionZones == null || extractionZones.Length == 0)
            RefreshExtractionZones(false);

        Vector2 desiredDirection = ResolveDesiredDirection();
        Vector2 desiredVelocity = desiredDirection * bot.EffectiveMoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.13f);

        if (desiredVelocity.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(desiredVelocity.y, desiredVelocity.x) * Mathf.Rad2Deg + 270f;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }
    }

    void RefreshExtractionZones(bool chooseNearest)
    {
        nextZoneRefreshTime = Time.time + Mathf.Max(0.3f, movement != null ? movement.TargetRefreshInterval : 0.45f);
        extractionZones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        if (extractionZones == null || extractionZones.Length == 0)
            return;

        if (chooseNearest)
        {
            targetZoneIndex = FindNearestZoneIndex();
            AdvanceTargetZone();
        }
        else
        {
            targetZoneIndex = Mathf.Clamp(targetZoneIndex, 0, extractionZones.Length - 1);
        }
    }

    Vector2 ResolveDesiredDirection()
    {
        if (extractionZones == null || extractionZones.Length == 0)
            return rb.linearVelocity.sqrMagnitude > 0.01f ? rb.linearVelocity.normalized : Vector2.up;

        ExtractionZone targetZone = extractionZones[Mathf.Clamp(targetZoneIndex, 0, extractionZones.Length - 1)];
        if (targetZone == null)
        {
            AdvanceTargetZone();
            return Vector2.up;
        }

        Vector2 toTarget = (Vector2)targetZone.transform.position - rb.position;
        if (toTarget.magnitude <= 1.4f)
        {
            AdvanceTargetZone();
            targetZone = extractionZones[Mathf.Clamp(targetZoneIndex, 0, extractionZones.Length - 1)];
            if (targetZone != null)
                toTarget = (Vector2)targetZone.transform.position - rb.position;
        }

        return toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector2.up;
    }

    int FindNearestZoneIndex()
    {
        int bestIndex = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < extractionZones.Length; i++)
        {
            ExtractionZone zone = extractionZones[i];
            if (zone == null)
                continue;

            float distance = Vector2.Distance(rb.position, zone.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    void AdvanceTargetZone()
    {
        if (extractionZones == null || extractionZones.Length == 0)
            return;

        targetZoneIndex = (targetZoneIndex + 1) % extractionZones.Length;
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyRescueShipBehavior : EnemyBotBehaviorBase
{
    const float HealRange = 5.9f;
    const float BeamBreakRange = 7.7f;
    const float HealPerSecond = 10f;
    const float HealStandOffDistance = 4.3f;
    const float HealAnchorTolerance = 0.92f;
    const float MinimumHealLockDuration = 1f;
    const float PatrolTurnIntervalMin = 1.35f;
    const float PatrolTurnIntervalMax = 2.6f;
    const float PatrolSpeedMultiplier = 0.78f;
    const float EntrySpeedMultiplier = 2.45f;
    const float MapEdgeMargin = 2.6f;
    const float MapEdgeSteerWeight = 0.82f;
    const float RecoveryEdgeThreshold = 2.1f;
    const float AvoidanceScanRadius = 2.1f;
    const float AvoidanceWeight = 0.4f;

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    Collider2D bodyCollider;
    readonly System.Collections.Generic.List<Collider2D> wallColliders = new System.Collections.Generic.List<Collider2D>(4);
    PlayerHealth currentHealTarget;
    Vector2 patrolDirection = Vector2.up;
    float nextTargetRefreshTime;
    float nextPatrolTurnTime;
    float healAccumulator;
    float healLockEndTime;
    int activeBeamTargetViewId = -1;
    bool wallCollisionsIgnoredWhileEntering;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        bodyCollider = owner.GetComponent<Collider2D>();
        RefreshWallColliders();

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.193f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
    }

    void OnDisable()
    {
        SetWallCollisionIgnored(false);
        StopBeam();
    }

    void OnDestroy()
    {
        SetWallCollisionIgnored(false);
        StopBeam();
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
        {
            StopBeam();
            return;
        }

        RefreshTargetIfNeeded();

        bool enteringMap = !IsInsidePlayableBounds(rb.position);
        SetWallCollisionIgnored(enteringMap);
        bool hadActiveHealingState = currentHealTarget != null || activeBeamTargetViewId > 0 || healLockEndTime > Time.time;
        bool canHealCurrentTarget = IsHealableTarget(currentHealTarget, bot);
        Vector2 desiredDirection;
        bool withinHealRange = false;

        if (canHealCurrentTarget)
        {
            desiredDirection = ResolveHealDirection(currentHealTarget, out withinHealRange);
        }
        else
        {
            currentHealTarget = null;
            if (hadActiveHealingState)
                EnterPatrolMode(enteringMap);
            desiredDirection = enteringMap ? (-rb.position).normalized : ResolvePatrolDirection();
        }

        desiredDirection = ApplyAvoidance(desiredDirection);
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        if (!canHealCurrentTarget && IsNearMapEdge(rb.position, RecoveryEdgeThreshold))
            desiredDirection = Vector2.Lerp(desiredDirection.normalized, (-rb.position).normalized, 0.72f).normalized;
        if (desiredDirection.sqrMagnitude <= 0.001f)
            desiredDirection = patrolDirection.sqrMagnitude > 0.001f ? patrolDirection : Vector2.up;

        bool sustainHealLock = canHealCurrentTarget
            && activeBeamTargetViewId > 0
            && Time.time < healLockEndTime
            && Vector2.Distance(rb.position, GetTargetPoint(currentHealTarget)) <= BeamBreakRange;
        bool healingThisFrame = canHealCurrentTarget && (withinHealRange || sustainHealLock);

        float speedMultiplier = enteringMap ? EntrySpeedMultiplier : canHealCurrentTarget ? 1f : PatrolSpeedMultiplier;
        if (healingThisFrame)
            speedMultiplier = 0.16f;

        Vector2 desiredVelocity = desiredDirection.normalized * (bot.EffectiveMoveSpeed * speedMultiplier);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, healingThisFrame ? 0.24f : 0.14f);

        Vector2 aimDirection = canHealCurrentTarget
            ? (GetTargetPoint(currentHealTarget) - rb.position)
            : rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : desiredDirection;
        RotateHullToward(aimDirection);

        if (healingThisFrame)
        {
            PhotonView targetView = currentHealTarget != null ? currentHealTarget.GetComponent<PhotonView>() : null;
            int targetViewId = targetView != null ? targetView.ViewID : -1;
            bool startedNewBeam = targetViewId > 0 && activeBeamTargetViewId != targetViewId;
            StartBeam(currentHealTarget);
            if (startedNewBeam)
                healLockEndTime = Time.time + MinimumHealLockDuration;
            ApplyHealing(currentHealTarget);
        }
        else
        {
            healAccumulator = 0f;
            healLockEndTime = 0f;
            StopBeam();
        }
    }

    public static bool TryFindNearestDamagedAlly(Vector2 origin, EnemyBot selfBot, out PlayerHealth result)
    {
        result = null;
        float bestDistance = float.MaxValue;
        PlayerHealth[] candidates = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerHealth candidate = candidates[i];
            if (!IsHealableTarget(candidate, selfBot))
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            result = candidate;
        }

        return result != null;
    }

    static bool IsHealableTarget(PlayerHealth candidate, EnemyBot selfBot)
    {
        if (candidate == null || candidate.IsWreck || candidate.IsEvacuationAnimating || !candidate.IsBotControlled)
            return false;

        EnemyBot candidateBot = candidate.GetComponent<EnemyBot>();
        if (candidateBot == null || candidateBot == selfBot)
            return false;

        if (candidateBot.Kind == EnemyBotKind.SpaceMine || candidateBot.Kind == EnemyBotKind.RescueShip)
            return false;

        return candidate.CurrentHP < candidate.maxHP || candidate.CurrentShield < candidate.MaxShield;
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < healLockEndTime && IsHealableTarget(currentHealTarget, bot))
            return;

        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.14f, movement.TargetRefreshInterval);
        TryFindNearestDamagedAlly(rb.position, bot, out currentHealTarget);
    }

    Vector2 ResolveHealDirection(PlayerHealth target, out bool withinRange)
    {
        Vector2 targetPoint = GetTargetPoint(target);
        Vector2 anchorPoint = GetHealAnchorPoint(targetPoint);
        Vector2 toAnchor = anchorPoint - rb.position;
        float anchorDistance = toAnchor.magnitude;
        float targetDistance = Vector2.Distance(rb.position, targetPoint);
        withinRange = targetDistance <= HealRange && anchorDistance <= HealAnchorTolerance;
        if (anchorDistance <= 0.001f)
            return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection : Vector2.up;

        if (targetDistance <= BeamBreakRange)
            return toAnchor / anchorDistance;

        return toAnchor / anchorDistance;
    }

    Vector2 ResolvePatrolDirection()
    {
        if (IsNearMapEdge(rb.position, RecoveryEdgeThreshold))
            return (-rb.position).normalized;

        if (Time.time >= nextPatrolTurnTime)
        {
            nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.5f, 0.5f);
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        return patrolDirection;
    }

    void EnterPatrolMode(bool enteringMap)
    {
        healAccumulator = 0f;
        healLockEndTime = 0f;
        StopBeam();

        Vector2 fallbackDirection;
        if (enteringMap || IsNearMapEdge(rb.position, RecoveryEdgeThreshold))
        {
            fallbackDirection = (-rb.position).sqrMagnitude > 0.001f ? (-rb.position).normalized : Vector2.up;
        }
        else if (rb.linearVelocity.sqrMagnitude > 0.001f)
        {
            fallbackDirection = rb.linearVelocity.normalized;
        }
        else
        {
            fallbackDirection = patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;
        }

        patrolDirection = fallbackDirection;
        nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
        rb.linearVelocity = patrolDirection * (bot.EffectiveMoveSpeed * Mathf.Max(PatrolSpeedMultiplier, 0.92f));
    }

    Vector2 ApplyAvoidance(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(rb.position, AvoidanceScanRadius, out Collider2D[] hits);
        Vector2 avoidance = Vector2.zero;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            if (!IsAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(rb.position);
            Vector2 away = rb.position - closest;
            if (away.sqrMagnitude <= 0.0001f)
                away = rb.position - (Vector2)hit.transform.position;

            float distance = Mathf.Max(0.1f, away.magnitude);
            if (distance > AvoidanceScanRadius)
                continue;

            avoidance += away.normalized * Mathf.Clamp01((AvoidanceScanRadius - distance) / AvoidanceScanRadius);
        }

        if (avoidance.sqrMagnitude <= 0.001f)
            return desiredDirection;

        Vector2 result = (desired + avoidance.normalized * AvoidanceWeight).normalized;
        return Vector2.Dot(result, desired) < 0.1f
            ? (desired * 0.76f + avoidance.normalized * 0.24f).normalized
            : result;
    }

    bool IsAvoidedObject(Collider2D hit)
    {
        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        if (hit.GetComponentInParent<DroppedCargoCrate>() != null)
            return true;

        EnemyBot otherBot = hit.GetComponentInParent<EnemyBot>();
        return otherBot != null && otherBot != bot;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.2f, bot.EffectiveMoveSpeed * 1.8f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + 1.8f, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - 1.8f, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + 1.8f, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - 1.8f, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desired * (1f - MapEdgeSteerWeight) + inward.normalized * MapEdgeSteerWeight).normalized;
    }

    bool IsInsidePlayableBounds(Vector2 position)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        return Mathf.Abs(position.x) <= halfX && Mathf.Abs(position.y) <= halfY;
    }

    bool IsNearMapEdge(Vector2 position, float threshold)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        return halfX - Mathf.Abs(position.x) <= threshold || halfY - Mathf.Abs(position.y) <= threshold;
    }

    Vector2 GetHealAnchorPoint(Vector2 targetPoint)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        float leftClearance = targetPoint.x + halfX;
        float rightClearance = halfX - targetPoint.x;
        float bottomClearance = targetPoint.y + halfY;
        float topClearance = halfY - targetPoint.y;

        Vector2 inward = (-targetPoint).sqrMagnitude > 0.001f ? (-targetPoint).normalized : Vector2.up;
        float smallestClearance = Mathf.Min(Mathf.Min(leftClearance, rightClearance), Mathf.Min(bottomClearance, topClearance));
        if (smallestClearance == leftClearance)
            inward = Vector2.right;
        else if (smallestClearance == rightClearance)
            inward = Vector2.left;
        else if (smallestClearance == bottomClearance)
            inward = Vector2.up;
        else if (smallestClearance == topClearance)
            inward = Vector2.down;

        return targetPoint + inward.normalized * HealStandOffDistance;
    }

    void RefreshWallColliders()
    {
        wallColliders.Clear();
        string[] wallNames = { "WallTop", "WallBottom", "WallLeft", "WallRight" };
        for (int i = 0; i < wallNames.Length; i++)
        {
            GameObject wall = GameObject.Find(wallNames[i]);
            if (wall == null)
                continue;

            Collider2D wallCollider = wall.GetComponent<Collider2D>();
            if (wallCollider != null)
                wallColliders.Add(wallCollider);
        }
    }

    void SetWallCollisionIgnored(bool ignored)
    {
        if (bodyCollider == null)
            return;

        if (wallColliders.Count == 0 || wallColliders.TrueForAll(collider => collider == null))
            RefreshWallColliders();

        if (wallCollisionsIgnoredWhileEntering == ignored)
            return;

        wallCollisionsIgnoredWhileEntering = ignored;
        for (int i = 0; i < wallColliders.Count; i++)
        {
            Collider2D wallCollider = wallColliders[i];
            if (wallCollider == null)
                continue;

            Physics2D.IgnoreCollision(bodyCollider, wallCollider, ignored);
        }
    }

    Vector2 GetTargetPoint(PlayerHealth target)
    {
        if (target == null)
            return rb.position;

        Collider2D collider = target.GetComponentInChildren<Collider2D>();
        if (collider != null)
            return collider.ClosestPoint(rb.position);

        return target.transform.position;
    }

    void RotateHullToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    void StartBeam(PlayerHealth target)
    {
        PhotonView targetView = target != null ? target.GetComponent<PhotonView>() : null;
        if (targetView == null || bot == null || bot.photonView == null)
            return;

        if (activeBeamTargetViewId == targetView.ViewID)
            return;

        StopBeam();
        activeBeamTargetViewId = targetView.ViewID;
        bot.photonView.RPC(nameof(EnemyBot.StartRescueShipBeamRpc), RpcTarget.All, activeBeamTargetViewId);
    }

    void StopBeam()
    {
        if (activeBeamTargetViewId <= 0 || bot == null || bot.photonView == null)
        {
            activeBeamTargetViewId = -1;
            return;
        }

        bot.photonView.RPC(nameof(EnemyBot.StopRescueShipBeamRpc), RpcTarget.All);
        activeBeamTargetViewId = -1;
    }

    void ApplyHealing(PlayerHealth target)
    {
        if (target == null)
            return;

        healAccumulator += HealPerSecond * Time.fixedDeltaTime;
        int wholePoints = Mathf.FloorToInt(healAccumulator);
        if (wholePoints <= 0)
            return;

        healAccumulator -= wholePoints;
        target.RepairVitalsAuthority(wholePoints);
        if (target.HasFullVitals)
        {
            if (Time.time >= healLockEndTime)
            {
                currentHealTarget = null;
                nextTargetRefreshTime = 0f;
                healLockEndTime = 0f;
                StopBeam();
            }
        }
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyPirateBaseBehavior : EnemyBotBehaviorBase
{
    const float ArrivalDistance = 2.25f;
    const float CollectionDuration = 10f;
    const float CollectionBreakDistance = 4.6f;
    const float CargoDropRadius = 1.35f;
    const float MapEdgeMargin = 3f;
    const float LaunchSpawnDelay = 2f;
    const float LaunchStageDuration = 7f;
    static readonly List<Collider2D> CollectibleColliderScratch = new List<Collider2D>(8);
    static readonly List<SpriteRenderer> CollectibleRendererScratch = new List<SpriteRenderer>(4);

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    Transform currentTarget;
    int currentTargetViewId;
    float nextTargetRefreshTime;
    float initialRotation;
    bool launchSequenceRunning;
    bool defenseLaunchTriggered;
    int latestThreatTargetViewId;
    bool collectionRunning;
    float collectionStartedAt;
    int collectingTargetViewId;
    int collectingLootIndex = -1;
    string collectingItemId;
    readonly List<string> collectedCargoItemIds = new List<string>();

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        initialRotation = rb != null ? rb.rotation : owner.transform.eulerAngles.z;
    }

    void OnDisable()
    {
        CancelCollection(true);
    }

    void OnDestroy()
    {
        CancelCollection(true);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
        {
            CancelCollection(false);
            return;
        }

        if (collectionRunning)
        {
            TickCollection();
            return;
        }

        if (launchSequenceRunning)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.MoveRotation(initialRotation);
            return;
        }

        RefreshTargetIfNeeded();

        Vector2 desiredVelocity = Vector2.zero;
        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            float targetDistance = GetDistanceToCollectible(currentTarget);
            if (targetDistance <= ArrivalDistance)
            {
                BeginCollection();
                return;
            }

            if (targetDistance > 0.001f)
            {
                Vector2 desiredDirection = ApplyMapEdgeSteering(toTarget.normalized);
                desiredVelocity = desiredDirection * bot.EffectiveMoveSpeed;
            }
        }

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, desiredVelocity.sqrMagnitude > 0.001f ? 0.09f : 0.045f);
        rb.angularVelocity = 0f;
        rb.MoveRotation(initialRotation);
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        TryTriggerLaunchSequence(ResolveValidPlayerViewId(attackerViewID));
    }

    public static void NotifyCollectibleCollected(int collectibleViewId, int collectorViewId)
    {
        if (!PhotonNetwork.IsMasterClient || collectibleViewId <= 0 || collectorViewId <= 0)
            return;

        EnemyPirateBaseBehavior[] bases = FindObjectsByType<EnemyPirateBaseBehavior>(FindObjectsInactive.Exclude);
        for (int i = 0; i < bases.Length; i++)
        {
            EnemyPirateBaseBehavior pirateBase = bases[i];
            if (pirateBase == null)
                continue;

            pirateBase.NotifyTargetCollected(collectibleViewId, collectorViewId);
        }
    }

    void NotifyTargetCollected(int collectibleViewId, int collectorViewId)
    {
        if (health != null && health.IsWreck)
            return;

        if (collectibleViewId != currentTargetViewId)
            return;

        if (collectionRunning)
            CancelCollection(false);

        TryTriggerLaunchSequence(ResolveValidPlayerViewId(collectorViewId));
        currentTarget = null;
        currentTargetViewId = 0;
        nextTargetRefreshTime = 0f;
    }

    void RefreshTargetIfNeeded()
    {
        bool currentValid = IsCurrentTargetValid();
        if (currentValid && Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.18f, movement.TargetRefreshInterval);
        Transform nextTarget = ResolveMostValuableCollectible(out int nextTargetViewId);
        if (nextTarget != null)
        {
            currentTarget = nextTarget;
            currentTargetViewId = nextTargetViewId;
        }
        else if (!currentValid)
        {
            currentTarget = null;
            currentTargetViewId = 0;
        }
    }

    bool IsCurrentTargetValid()
    {
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy || currentTargetViewId <= 0)
            return false;

        PhotonView targetView = PhotonView.Find(currentTargetViewId);
        if (targetView == null)
            return false;

        Treasure treasure = targetView.GetComponent<Treasure>();
        if (treasure != null)
            return !treasure.isBeingCollected;

        DroppedCargoCrate crate = targetView.GetComponent<DroppedCargoCrate>();
        if (crate != null)
            return crate.HasLoot && !crate.isBeingCollected;

        ShipWreck wreck = targetView.GetComponent<ShipWreck>();
        return wreck != null && wreck.HasLoot && !wreck.isBeingCollected;
    }

    Transform ResolveMostValuableCollectible(out int targetViewId)
    {
        Transform bestTarget = null;
        targetViewId = 0;
        int bestValue = int.MinValue;
        float bestDistance = float.MaxValue;

        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || treasure.isBeingCollected)
                continue;

            PhotonView targetView = treasure.GetComponent<PhotonView>();
            ConsiderCollectible(treasure.transform, targetView, treasure.itemId, ref bestTarget, ref targetViewId, ref bestValue, ref bestDistance);
        }

        DroppedCargoCrate[] crates = FindObjectsByType<DroppedCargoCrate>(FindObjectsInactive.Exclude);
        for (int i = 0; i < crates.Length; i++)
        {
            DroppedCargoCrate crate = crates[i];
            if (crate == null || !crate.HasLoot || crate.isBeingCollected)
                continue;

            PhotonView targetView = crate.GetComponent<PhotonView>();
            ConsiderCollectible(crate.transform, targetView, crate.StoredItemId, ref bestTarget, ref targetViewId, ref bestValue, ref bestDistance);
        }

        ShipWreck[] wrecks = FindObjectsByType<ShipWreck>(FindObjectsInactive.Exclude);
        for (int i = 0; i < wrecks.Length; i++)
        {
            ShipWreck wreck = wrecks[i];
            if (wreck == null || !wreck.HasLoot || wreck.isBeingCollected)
                continue;

            PhotonView targetView = wreck.GetComponent<PhotonView>();
            string itemId = wreck.GetLootItemAt(wreck.GetFirstLootIndex());
            ConsiderCollectible(wreck.transform, targetView, itemId, ref bestTarget, ref targetViewId, ref bestValue, ref bestDistance);
        }

        return bestTarget;
    }

    void ConsiderCollectible(Transform candidate, PhotonView targetView, string itemId, ref Transform bestTarget, ref int bestViewId, ref int bestValue, ref float bestDistance)
    {
        if (candidate == null || targetView == null)
            return;

        int value = Mathf.Max(0, InventoryItemCatalog.GetSellValueAstrons(itemId));
        float distance = GetDistanceToCollectible(candidate);
        if (value < bestValue)
            return;

        if (value == bestValue && distance >= bestDistance)
            return;

        bestValue = value;
        bestDistance = distance;
        bestTarget = candidate;
        bestViewId = targetView.ViewID;
    }

    void BeginCollection()
    {
        if (currentTargetViewId <= 0 || collectionRunning)
            return;

        PhotonView targetView = PhotonView.Find(currentTargetViewId);
        if (targetView == null || IsCollectibleReserved(targetView))
        {
            ClearCurrentTarget();
            return;
        }

        if (!TryResolveCollectibleLoot(targetView, out string itemId, out int lootIndex))
        {
            ClearCurrentTarget();
            return;
        }

        collectionRunning = true;
        collectionStartedAt = Time.time;
        collectingTargetViewId = currentTargetViewId;
        collectingItemId = itemId;
        collectingLootIndex = lootIndex;
        MarkCollectibleCollectionState(collectingTargetViewId, true);

        if (bot != null && bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.StartPirateBaseCollectionBeamRpc), RpcTarget.All, collectingTargetViewId);

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.MoveRotation(initialRotation);
    }

    void TickCollection()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.MoveRotation(initialRotation);

        if (!IsCollectingTargetStillValid())
        {
            CancelCollection(true);
            ClearCurrentTarget();
            return;
        }

        if (Time.time - collectionStartedAt >= CollectionDuration)
            CompleteCollection();
    }

    void CompleteCollection()
    {
        if (!IsCollectingTargetStillValid())
        {
            CancelCollection(true);
            ClearCurrentTarget();
            return;
        }

        if (!string.IsNullOrWhiteSpace(collectingItemId))
            collectedCargoItemIds.Add(collectingItemId);

        RemoveCollectedTarget(collectingTargetViewId, collectingLootIndex, collectingItemId);
        CancelCollection(false);
        ClearCurrentTarget();
    }

    void CancelCollection(bool releaseTarget)
    {
        if (!collectionRunning && collectingTargetViewId <= 0)
            return;

        int targetViewId = collectingTargetViewId;
        collectionRunning = false;
        collectionStartedAt = 0f;
        collectingTargetViewId = 0;
        collectingLootIndex = -1;
        collectingItemId = null;

        if (releaseTarget && targetViewId > 0)
            MarkCollectibleCollectionState(targetViewId, false);

        if (view != null && view.IsMine && bot != null && bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.StopPirateBaseCollectionBeamRpc), RpcTarget.All);
    }

    public void DropCollectedCargoOnDeath()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        CancelCollection(true);
        if (collectedCargoItemIds.Count == 0)
            return;

        Vector3 center = transform.position;
        int seed = view != null ? view.ViewID : Mathf.RoundToInt(center.sqrMagnitude * 1000f);
        for (int i = 0; i < collectedCargoItemIds.Count; i++)
        {
            string itemId = collectedCargoItemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            float angle = ((i / Mathf.Max(1f, collectedCargoItemIds.Count)) * Mathf.PI * 2f) + seed * 0.173f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (direction.sqrMagnitude <= 0.001f)
                direction = Vector2.up;

            float radius = Mathf.Lerp(0.45f, CargoDropRadius, Hash01(seed, i + 17));
            Vector3 dropPosition = center + (Vector3)(direction.normalized * radius);
            Vector2 tangent = new Vector2(-direction.y, direction.x) * Mathf.Lerp(-0.28f, 0.28f, Hash01(seed, i + 41));
            Vector2 drift = (direction.normalized * 0.78f + tangent).normalized * Mathf.Lerp(0.48f, 0.92f, Hash01(seed, i + 73));
            DroppedCargoManager.DropItemAtPosition(itemId, dropPosition, drift);
        }

        collectedCargoItemIds.Clear();
        GameVisualTheme.RequestRuntimeRefresh();
    }

    bool IsCollectingTargetStillValid()
    {
        if (collectingTargetViewId <= 0 || string.IsNullOrWhiteSpace(collectingItemId))
            return false;

        PhotonView targetView = PhotonView.Find(collectingTargetViewId);
        if (targetView == null)
            return false;

        if (!TryResolveCollectibleLoot(targetView, out string currentItemId, out int currentLootIndex))
            return false;

        if (!string.Equals(currentItemId, collectingItemId, System.StringComparison.Ordinal) || currentLootIndex != collectingLootIndex)
            return false;

        float distance = GetDistanceToCollectible(targetView.transform);
        return distance <= CollectionBreakDistance;
    }

    float GetDistanceToCollectible(Transform target)
    {
        if (target == null)
            return float.MaxValue;

        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        float bestDistance = float.MaxValue;

        CollectibleColliderScratch.Clear();
        target.GetComponentsInChildren(false, CollectibleColliderScratch);
        for (int i = 0; i < CollectibleColliderScratch.Count; i++)
        {
            Collider2D collider = CollectibleColliderScratch[i];
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;

            float distance = Vector2.Distance(origin, collider.ClosestPoint(origin));
            if (distance < bestDistance)
                bestDistance = distance;
        }

        if (bestDistance < float.MaxValue)
            return bestDistance;

        CollectibleRendererScratch.Clear();
        target.GetComponentsInChildren(false, CollectibleRendererScratch);
        for (int i = 0; i < CollectibleRendererScratch.Count; i++)
        {
            SpriteRenderer renderer = CollectibleRendererScratch[i];
            if (renderer == null || !renderer.enabled || renderer.sprite == null || !renderer.gameObject.activeInHierarchy)
                continue;

            Vector3 closest = renderer.bounds.ClosestPoint(origin);
            float distance = Vector2.Distance(origin, closest);
            if (distance < bestDistance)
                bestDistance = distance;
        }

        return bestDistance < float.MaxValue
            ? bestDistance
            : Vector2.Distance(origin, target.position);
    }

    bool TryResolveCollectibleLoot(PhotonView targetView, out string itemId, out int lootIndex)
    {
        itemId = null;
        lootIndex = -1;
        if (targetView == null)
            return false;

        Treasure treasure = targetView.GetComponent<Treasure>();
        if (treasure != null)
        {
            itemId = treasure.itemId;
            lootIndex = 0;
            return !string.IsNullOrWhiteSpace(itemId);
        }

        DroppedCargoCrate crate = targetView.GetComponent<DroppedCargoCrate>();
        if (crate != null && crate.HasLoot)
        {
            itemId = crate.StoredItemId;
            lootIndex = 0;
            return !string.IsNullOrWhiteSpace(itemId);
        }

        ShipWreck wreck = targetView.GetComponent<ShipWreck>();
        if (wreck != null && wreck.HasLoot)
        {
            lootIndex = wreck.GetFirstLootIndex();
            itemId = wreck.GetLootItemAt(lootIndex);
            return lootIndex >= 0 && !string.IsNullOrWhiteSpace(itemId);
        }

        return false;
    }

    bool IsCollectibleReserved(PhotonView targetView)
    {
        if (targetView == null)
            return false;

        Treasure treasure = targetView.GetComponent<Treasure>();
        if (treasure != null)
            return treasure.isBeingCollected;

        DroppedCargoCrate crate = targetView.GetComponent<DroppedCargoCrate>();
        if (crate != null)
            return crate.isBeingCollected;

        ShipWreck wreck = targetView.GetComponent<ShipWreck>();
        return wreck != null && wreck.isBeingCollected;
    }

    void MarkCollectibleCollectionState(int targetViewId, bool value)
    {
        if (!PhotonNetwork.IsMasterClient || targetViewId <= 0)
            return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null)
            return;

        Treasure treasure = targetView.GetComponent<Treasure>();
        if (treasure != null)
        {
            targetView.RPC(nameof(Treasure.SetBeingCollectedRpc), RpcTarget.All, value);
            return;
        }

        DroppedCargoCrate crate = targetView.GetComponent<DroppedCargoCrate>();
        if (crate != null)
        {
            targetView.RPC(nameof(DroppedCargoCrate.SetBeingCollectedRpc), RpcTarget.All, value);
            return;
        }

        ShipWreck wreck = targetView.GetComponent<ShipWreck>();
        if (wreck != null)
            targetView.RPC(nameof(ShipWreck.SetBeingCollectedRpc), RpcTarget.All, value);
    }

    void RemoveCollectedTarget(int targetViewId, int lootIndex, string itemId)
    {
        if (!PhotonNetwork.IsMasterClient || targetViewId <= 0)
            return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null)
            return;

        int collectorViewId = view != null ? view.ViewID : 0;
        Treasure treasure = targetView.GetComponent<Treasure>();
        if (treasure != null)
        {
            if (!string.Equals(treasure.itemId, itemId, System.StringComparison.Ordinal))
                return;

            SpaceTrapTarget.DetonateIfArmed(targetViewId, collectorViewId);
            PhotonNetwork.Destroy(targetView.gameObject);
            return;
        }

        DroppedCargoCrate crate = targetView.GetComponent<DroppedCargoCrate>();
        if (crate != null)
        {
            if (!crate.HasLoot || !string.Equals(crate.StoredItemId, itemId, System.StringComparison.Ordinal))
                return;

            SpaceTrapTarget.DetonateIfArmed(targetViewId, collectorViewId);
            targetView.RPC(nameof(DroppedCargoCrate.ClearStoredItemRpc), RpcTarget.All);
            PhotonNetwork.Destroy(targetView.gameObject);
            return;
        }

        ShipWreck wreck = targetView.GetComponent<ShipWreck>();
        if (wreck == null || !wreck.HasLoot)
            return;

        string currentItemId = wreck.GetLootItemAt(lootIndex);
        if (!string.Equals(currentItemId, itemId, System.StringComparison.Ordinal))
            return;

        SpaceTrapTarget.DetonateIfArmed(targetViewId, collectorViewId);
        targetView.RPC(nameof(ShipWreck.RemoveLootAtIndexRpc), RpcTarget.All, lootIndex);
    }

    void ClearCurrentTarget()
    {
        currentTarget = null;
        currentTargetViewId = 0;
        nextTargetRefreshTime = 0f;
    }

    static float Hash01(int baseSeed, int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)baseSeed) * 16777619u;
            hash = (hash ^ (uint)salt) * 16777619u;
            hash ^= hash >> 13;
            hash *= 1274126177u;
            return (hash & 0xFFFFFF) / 16777215f;
        }
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.1f, bot.EffectiveMoveSpeed * 2f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= 1f;
        else if (predicted.x < -halfX)
            inward.x += 1f;

        if (predicted.y > halfY)
            inward.y -= 1f;
        else if (predicted.y < -halfY)
            inward.y += 1f;

        if (inward.sqrMagnitude <= 0.001f)
            return desired;

        return (desired * 0.42f + inward.normalized * 0.58f).normalized;
    }

    void TryTriggerLaunchSequence(int targetViewId)
    {
        targetViewId = ResolveValidPlayerViewId(targetViewId);
        if (targetViewId <= 0)
            targetViewId = FindNearestPlayerViewId(transform.position);

        if (targetViewId <= 0)
            return;

        if (collectionRunning)
            CancelCollection(true);

        latestThreatTargetViewId = targetViewId;
        if (launchSequenceRunning || defenseLaunchTriggered)
            return;

        defenseLaunchTriggered = true;
        StartCoroutine(LaunchDefenseSequence());
    }

    IEnumerator LaunchDefenseSequence()
    {
        launchSequenceRunning = true;
        EnemyBotKind[] launchOrder = { EnemyBotKind.PirateFighterElite, EnemyBotKind.PirateFighterAce };
        for (int i = 0; i < launchOrder.Length; i++)
        {
            if (health != null && health.IsWreck)
                break;

            EnemyBotKind fighterKind = launchOrder[i];
            int targetViewId = ResolveValidPlayerViewId(latestThreatTargetViewId);
            if (targetViewId <= 0)
                targetViewId = FindNearestPlayerViewId(transform.position);

            if (bot != null && bot.photonView != null)
                bot.photonView.RPC(nameof(EnemyBot.PlayPirateBaseLaunchVfxRpc), RpcTarget.All, (int)fighterKind);

            yield return new WaitForSeconds(LaunchSpawnDelay);
            SpawnLaunchedFighter(fighterKind, targetViewId);
            yield return new WaitForSeconds(Mathf.Max(0.05f, LaunchStageDuration - LaunchSpawnDelay));
        }

        launchSequenceRunning = false;
    }

    void SpawnLaunchedFighter(EnemyBotKind fighterKind, int targetViewId)
    {
        if (health != null && health.IsWreck)
            return;

        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(fighterKind);
        if (definition == null)
            return;

        Vector3 spawnPosition = transform.position;
        GameObject fighterObject = PhotonNetwork.Instantiate(
            "Player",
            spawnPosition,
            transform.rotation,
            0,
            new object[] { definition.InstantiationMarker, EnemyBot.PirateBaseLaunchedFighterMarker, targetViewId, view != null ? view.ViewID : 0 });

        if (fighterObject == null)
            return;

        EnemyBot fighter = fighterObject.GetComponent<EnemyBot>();
        if (fighter == null)
            fighter = fighterObject.AddComponent<EnemyBot>();

        fighter.InitializeFromPhotonData();
        fighter.ForceCombatTarget(targetViewId);
        GameVisualTheme.RequestRuntimeRefresh();
    }

    int ResolveValidPlayerViewId(int viewId)
    {
        if (viewId <= 0)
            return 0;

        PhotonView targetView = PhotonView.Find(viewId);
        if (targetView == null)
            return 0;

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        if (targetHealth == null || targetHealth.IsWreck || targetHealth.IsBotControlled || targetHealth.IsAstronautControlled || targetHealth.IsEvacuationAnimating)
            return 0;

        return targetView.ViewID;
    }

    static int FindNearestPlayerViewId(Vector2 origin)
    {
        int pirateCaseCarrierViewId = ValuableCargoCarrierUtility.FindBestPirateCaseCarrierViewId(origin, float.PositiveInfinity);
        if (pirateCaseCarrierViewId > 0)
            return pirateCaseCarrierViewId;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        int bestViewId = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate == null || candidate.IsWreck || candidate.IsBotControlled || candidate.IsAstronautControlled || candidate.IsEvacuationAnimating)
                continue;

            PhotonView candidateView = candidate.GetComponent<PhotonView>();
            if (candidateView == null)
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestViewId = candidateView.ViewID;
        }

        return bestViewId;
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyRadarShipBehavior : EnemyBotBehaviorBase
{
    const float StrikeWarningDuration = 2f;
    const float StrikeRadius = 1.35f;
    const float AvoidanceScanRadius = 2.2f;
    const float AvoidanceWeight = 0.34f;
    const float PatrolRefreshInterval = 1.15f;
    const float PatrolArrivalDistance = 1.9f;
    const float PatrolFallbackTurnIntervalMin = 1.4f;
    const float PatrolFallbackTurnIntervalMax = 2.5f;
    const float MapEdgeMargin = 2f;
    const float MapEdgeSteerWeight = 0.78f;

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    Transform currentTarget;
    Transform patrolCollectibleTarget;
    Vector2 fallbackPatrolDirection = Vector2.up;
    float nextTargetRefreshTime;
    float nextPatrolRefreshTime;
    float nextFallbackTurnTime;
    float nextStrikeTime;
    float orbitDirection = 1f;
    bool strikePending;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.171f) % (Mathf.PI * 2f);
        fallbackPatrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        nextStrikeTime = Time.time + Random.Range(0.5f, 1.2f);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        RefreshTargetIfNeeded();
        RefreshPatrolTargetIfNeeded();

        Vector2 desiredDirection = currentTarget != null
            ? ResolveCombatDirection()
            : ResolvePatrolDirection();

        desiredDirection = ApplyCollectibleAvoidance(desiredDirection);
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        if (desiredDirection.sqrMagnitude <= 0.001f)
            desiredDirection = fallbackPatrolDirection.sqrMagnitude > 0.001f ? fallbackPatrolDirection : Vector2.up;

        float speedMultiplier = currentTarget != null ? 1f : 0.82f;
        Vector2 desiredVelocity = desiredDirection.normalized * (bot.EffectiveMoveSpeed * speedMultiplier);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, currentTarget != null ? 0.16f : 0.11f);

        Vector2 aimDirection = currentTarget != null
            ? (Vector2)currentTarget.position - rb.position
            : rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : desiredDirection;
        RotateHullToward(aimDirection);

        if (currentTarget != null)
            TryCallRadarStrike();
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView != null)
        {
            currentTarget = attackerView.transform;
            nextTargetRefreshTime = Time.time + 0.4f;
            nextStrikeTime = Mathf.Min(nextStrikeTime, Time.time + 0.25f);
        }
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.18f, movement.TargetRefreshInterval);
        currentTarget = ResolvePlayerTarget();
    }

    void RefreshPatrolTargetIfNeeded()
    {
        if (currentTarget != null)
            return;

        if (patrolCollectibleTarget != null && patrolCollectibleTarget.gameObject.activeInHierarchy)
            return;

        if (Time.time < nextPatrolRefreshTime)
            return;

        nextPatrolRefreshTime = Time.time + PatrolRefreshInterval;
        patrolCollectibleTarget = ResolveBestCollectibleTarget();
    }

    Transform ResolvePlayerTarget()
    {
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, rb.position, movement.DisengageRadius, false))
            return currentTarget;

        return EnemyTargetingUtility.FindClosestTarget(rb.position, health, movement.DetectionRadius, false);
    }

    bool IsValidPlayerTarget(PlayerHealth candidate, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(candidate != null ? candidate.transform : null, health, rb.position, maxDistance, false);
    }

    Transform ResolveBestCollectibleTarget()
    {
        Transform bestTarget = null;
        float bestScore = float.MinValue;

        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || treasure.isBeingCollected)
                continue;

            float score = ScoreCollectible(treasure.transform.position, treasure.itemId, 1f);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = treasure.transform;
            }
        }

        ShipWreck[] wrecks = FindObjectsByType<ShipWreck>(FindObjectsInactive.Exclude);
        for (int i = 0; i < wrecks.Length; i++)
        {
            ShipWreck wreck = wrecks[i];
            if (wreck == null || !wreck.HasLoot || wreck.isBeingCollected)
                continue;

            string itemId = wreck.GetLootItemAt(wreck.GetFirstLootIndex());
            float score = ScoreCollectible(wreck.transform.position, itemId, 1.18f);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = wreck.transform;
            }
        }

        DroppedCargoCrate[] crates = FindObjectsByType<DroppedCargoCrate>(FindObjectsInactive.Exclude);
        for (int i = 0; i < crates.Length; i++)
        {
            DroppedCargoCrate crate = crates[i];
            if (crate == null || !crate.HasLoot || crate.isBeingCollected)
                continue;

            float score = ScoreCollectible(crate.transform.position, crate.StoredItemId, 1.08f);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = crate.transform;
            }
        }

        return bestTarget;
    }

    float ScoreCollectible(Vector2 position, string itemId, float categoryMultiplier)
    {
        int sellValue = Mathf.Max(1, InventoryItemCatalog.GetSellValueAstrons(itemId));
        float distance = Vector2.Distance(rb.position, position);
        float rarityWeight = (int)InventoryItemCatalog.GetRarity(itemId) * 110f;
        return sellValue * categoryMultiplier + rarityWeight - distance * 38f;
    }

    Vector2 ResolvePatrolDirection()
    {
        if (patrolCollectibleTarget != null && patrolCollectibleTarget.gameObject.activeInHierarchy)
        {
            Vector2 toTarget = (Vector2)patrolCollectibleTarget.position - rb.position;
            if (toTarget.magnitude <= PatrolArrivalDistance)
            {
                patrolCollectibleTarget = null;
                nextPatrolRefreshTime = 0f;
            }
            else
            {
                return toTarget.normalized;
            }
        }

        if (Time.time >= nextFallbackTurnTime)
        {
            nextFallbackTurnTime = Time.time + Random.Range(PatrolFallbackTurnIntervalMin, PatrolFallbackTurnIntervalMax);
            float angle = Mathf.Atan2(fallbackPatrolDirection.y, fallbackPatrolDirection.x) + Random.Range(-0.52f, 0.52f);
            fallbackPatrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        return fallbackPatrolDirection;
    }

    Vector2 ResolveCombatDirection()
    {
        if (currentTarget == null)
            return ResolvePatrolDirection();

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return ResolvePatrolDirection();

        Vector2 toward = toTarget / distance;
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-toward.y, toward.x)
            : new Vector2(toward.y, -toward.x);

        float wobble = Mathf.Sin(Time.time * 1.35f + (view != null ? view.ViewID : 1) * 0.13f) * 0.14f;
        if (distance > movement.PreferredDistance + 0.5f)
            return (toward * 0.78f + tangent * (0.32f + wobble)).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.62f + tangent * 0.78f).normalized;

        return (tangent * 0.92f + toward * 0.12f).normalized;
    }

    Vector2 ApplyCollectibleAvoidance(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(rb.position, AvoidanceScanRadius, out Collider2D[] hits);
        Vector2 avoidance = Vector2.zero;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            if (!IsAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(rb.position);
            Vector2 away = rb.position - closest;
            if (away.sqrMagnitude <= 0.0001f)
                away = rb.position - (Vector2)hit.transform.position;

            float distance = Mathf.Max(0.12f, away.magnitude);
            if (distance > AvoidanceScanRadius)
                continue;

            avoidance += away.normalized * Mathf.Clamp01((AvoidanceScanRadius - distance) / AvoidanceScanRadius);
        }

        if (avoidance.sqrMagnitude <= 0.001f)
            return desiredDirection;

        Vector2 result = (desired + avoidance.normalized * AvoidanceWeight).normalized;
        return Vector2.Dot(result, desired) < 0.1f
            ? (desired * 0.78f + avoidance.normalized * 0.22f).normalized
            : result;
    }

    bool IsAvoidedObject(Collider2D hit)
    {
        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        return hit.GetComponentInParent<DroppedCargoCrate>() != null;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.4f, bot.EffectiveMoveSpeed * 1.8f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + 1.4f, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - 1.4f, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + 1.4f, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - 1.4f, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desired * (1f - MapEdgeSteerWeight) + inward.normalized * MapEdgeSteerWeight).normalized;
    }

    void RotateHullToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    void TryCallRadarStrike()
    {
        if (weapon == null || currentTarget == null || strikePending || Time.time < nextStrikeTime)
            return;

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        if (toTarget.magnitude > weapon.Range)
            return;

        Vector2 strikePoint = currentTarget.position;
        strikePending = true;
        nextStrikeTime = Time.time + ScaleEnemyAttackCooldown(Mathf.Max(0.2f, weapon.FireRate));
        float warningDuration = ResolveStrikeWarningDuration();

        if (bot.photonView != null)
        {
            bot.photonView.RPC(nameof(EnemyBot.PlayRadarShipIncomingRpc), RpcTarget.All, strikePoint.x, strikePoint.y, 0f);
            bot.photonView.RPC(nameof(EnemyBot.SpawnRadarStrikeMarkerRpc), RpcTarget.All, strikePoint.x, strikePoint.y, warningDuration, StrikeRadius);
        }

        StartCoroutine(ExecuteStrikeAfterDelay(strikePoint, warningDuration));
    }

    IEnumerator ExecuteStrikeAfterDelay(Vector2 strikePoint, float warningDuration)
    {
        yield return new WaitForSeconds(warningDuration);

        if (bot == null || bot.photonView == null)
        {
            strikePending = false;
            yield break;
        }

        bot.photonView.RPC(nameof(EnemyBot.PlayRadarShipShootRpc), RpcTarget.All, strikePoint.x, strikePoint.y, 0f);
        ApplyStrikeDamage(strikePoint);
        bot.photonView.RPC(nameof(EnemyBot.SpawnRadarStrikeImpactRpc), RpcTarget.All, strikePoint.x, strikePoint.y, StrikeRadius);
        strikePending = false;
    }

    float ResolveStrikeWarningDuration()
    {
        return ScaleEnemyAttackWindup(StrikeWarningDuration);
    }

    void ApplyStrikeDamage(Vector2 strikePoint)
    {
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(strikePoint, StrikeRadius, out Collider2D[] hits);
        HashSet<int> processedViewIds = new HashSet<int>();
        int attackerViewId = bot.photonView != null ? bot.photonView.ViewID : 0;
        int baseDamage = RoomSettings.GetEnemyDamage(bot.Kind);
        WeaponHitContext hitContext = weapon != null
            ? new WeaponHitContext(weapon.DamageType, weapon.DeliveryMethod, weapon.DeliveryFlags, string.Empty)
            : WeaponHitContext.None;

        for (int i = 0; i < hitCount; i++)
        {
            PlayerHealth candidate = hits[i] != null ? hits[i].GetComponentInParent<PlayerHealth>() : null;
            PhotonView targetView = candidate != null ? candidate.GetComponent<PhotonView>() : null;
            if (candidate == null || targetView == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating)
                continue;

            if (candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            if (!processedViewIds.Add(targetView.ViewID))
                continue;

            float distance = Vector2.Distance(strikePoint, candidate.transform.position);
            float falloff = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(distance / StrikeRadius));
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * falloff));
            targetView.RPC(
                nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                RpcTarget.MasterClient,
                damage,
                damage,
                attackerViewId,
                strikePoint.x,
                strikePoint.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted || beacon.photonView == null)
                continue;

            if (!processedViewIds.Add(beacon.photonView.ViewID))
                continue;

            float distance = Vector2.Distance(strikePoint, beacon.transform.position);
            if (distance > StrikeRadius)
                continue;

            float falloff = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(distance / StrikeRadius));
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * falloff));
            beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageProfileAt), RpcTarget.MasterClient, damage, damage, attackerViewId, strikePoint.x, strikePoint.y);
        }

        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (deployable == null || !deployable.CanBeTargeted || deployable.photonView == null)
                continue;

            if (!processedViewIds.Add(deployable.photonView.ViewID))
                continue;

            float distance = Vector2.Distance(strikePoint, deployable.transform.position);
            if (distance > StrikeRadius)
                continue;

            float falloff = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(distance / StrikeRadius));
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * falloff));
            deployable.photonView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                damage,
                damage,
                attackerViewId,
                strikePoint.x,
                strikePoint.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }
    }
}

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
