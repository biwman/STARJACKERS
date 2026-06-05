using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class PilotBarrierVfx : MonoBehaviour
{
    const int PointCount = 128;
    const float ZOffset = -0.08f;
    const int SortingOrderOffset = 170;

    static Material sharedMaterial;

    SpriteRenderer shipRenderer;
    LineRenderer outerRing;
    LineRenderer shimmerRing;
    GameObject rootObject;
    float endTime;
    float phase;

    public static void Attach(GameObject target, float duration)
    {
        if (target == null || duration <= 0f || !RoomSettings.AreVisualEffectsEnabled())
            return;

        PilotBarrierVfx existing = target.GetComponent<PilotBarrierVfx>();
        if (existing != null)
        {
            existing.endTime = Mathf.Max(existing.endTime, Time.time + duration);
            return;
        }

        PilotBarrierVfx vfx = target.AddComponent<PilotBarrierVfx>();
        vfx.Initialize(duration);
    }

    void Initialize(float duration)
    {
        shipRenderer = GetComponentInChildren<SpriteRenderer>();
        endTime = Time.time + duration;

        rootObject = new GameObject("PilotBarrierVfxRoot");
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = new Vector3(0f, 0f, ZOffset);
        rootObject.transform.localRotation = Quaternion.identity;
        rootObject.layer = gameObject.layer;

        int sortingLayerId = shipRenderer != null ? shipRenderer.sortingLayerID : 0;
        int sortingOrder = shipRenderer != null ? shipRenderer.sortingOrder + SortingOrderOffset : 1400;
        outerRing = CreateRing("PilotBarrierOuterRing", sortingLayerId, sortingOrder, 0.075f);
        shimmerRing = CreateRing("PilotBarrierShimmerRing", sortingLayerId, sortingOrder + 1, 0.035f);
    }

    void Update()
    {
        if (Time.time >= endTime || !RoomSettings.AreVisualEffectsEnabled())
        {
            DestroySelf();
            return;
        }

        phase += Time.deltaTime;
        Vector2 radius = ResolveRadius();
        SetRingPoints(outerRing, radius, 0f, 7f, true);
        SetRingPoints(shimmerRing, radius * 0.88f, Mathf.PI / PointCount, 11f, false);

        float pulse = 0.5f + Mathf.Sin(phase * 5.8f) * 0.5f;
        SetRingColor(outerRing, Color.Lerp(new Color(0.3f, 1f, 0.86f, 0.32f), new Color(1f, 0.82f, 0.36f, 0.72f), pulse));
        SetRingColor(shimmerRing, Color.Lerp(new Color(0.96f, 0.42f, 1f, 0.22f), new Color(0.56f, 1f, 0.94f, 0.58f), 1f - pulse));
    }

    LineRenderer CreateRing(string objectName, int sortingLayerId, int sortingOrder, float width)
    {
        GameObject ringObject = new GameObject(objectName);
        ringObject.transform.SetParent(rootObject.transform, false);
        ringObject.layer = gameObject.layer;

        LineRenderer line = ringObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = PointCount + 1;
        line.widthMultiplier = width;
        line.numCapVertices = 6;
        line.numCornerVertices = 6;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.material = GetMaterial("PilotBarrierVfxMaterial", 3400);
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
    }

    void SetRingPoints(LineRenderer line, Vector2 radius, float offset, float rippleFrequency, bool dashed)
    {
        if (line == null)
            return;

        for (int i = 0; i <= PointCount; i++)
        {
            float t = i / (float)PointCount;
            float angle = (t * Mathf.PI * 2f) + offset;
            float dash = dashed ? Mathf.Clamp01(Mathf.Sin((angle * 9f) + phase * 7f) * 2.4f + 0.38f) : 1f;
            float ripple = 1f + Mathf.Sin((angle * rippleFrequency) + phase * 4.5f) * 0.025f * dash;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius.x * ripple, Mathf.Sin(angle) * radius.y * ripple, 0f));
        }
    }

    Vector2 ResolveRadius()
    {
        if (shipRenderer != null)
        {
            Vector3 scale = transform.lossyScale;
            float scaleX = Mathf.Max(0.001f, Mathf.Abs(scale.x));
            float scaleY = Mathf.Max(0.001f, Mathf.Abs(scale.y));
            Bounds bounds = shipRenderer.bounds;
            return new Vector2(
                Mathf.Max(0.72f, bounds.extents.x / scaleX + 0.18f),
                Mathf.Max(0.72f, bounds.extents.y / scaleY + 0.18f));
        }

        return new Vector2(0.82f, 0.82f);
    }

    void SetRingColor(LineRenderer line, Color color)
    {
        if (line == null)
            return;

        line.startColor = color;
        line.endColor = color;
    }

    void OnDestroy()
    {
        if (rootObject != null)
            Destroy(rootObject);
    }

    void DestroySelf()
    {
        if (rootObject != null)
        {
            Destroy(rootObject);
            rootObject = null;
        }

        Destroy(this);
    }

    static Material GetMaterial(string materialName, int renderQueue)
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        sharedMaterial = new Material(shader)
        {
            name = materialName,
            color = Color.white
        };
        sharedMaterial.renderQueue = renderQueue;
        return sharedMaterial;
    }
}

public sealed class RoburMarkAuraVfx : MonoBehaviour
{
    const int PointCount = 96;
    static Material sharedMaterial;

    SpriteRenderer targetRenderer;
    LineRenderer auraRing;
    float endTime;
    float phase;

    public static void Attach(int targetViewId, float duration)
    {
        if (targetViewId <= 0 || duration <= 0f || !RoomSettings.AreVisualEffectsEnabled())
            return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null)
            return;

        RoburMarkAuraVfx existing = targetView.GetComponent<RoburMarkAuraVfx>();
        if (existing != null)
        {
            existing.endTime = Mathf.Max(existing.endTime, Time.time + duration);
            return;
        }

        RoburMarkAuraVfx vfx = targetView.gameObject.AddComponent<RoburMarkAuraVfx>();
        vfx.Initialize(duration);
    }

    void Initialize(float duration)
    {
        targetRenderer = GetComponentInChildren<SpriteRenderer>();
        endTime = Time.time + duration;
        int sortingLayerId = targetRenderer != null ? targetRenderer.sortingLayerID : 0;
        int sortingOrder = targetRenderer != null ? targetRenderer.sortingOrder + 190 : 1600;
        auraRing = CreateRing(sortingLayerId, sortingOrder);
    }

    void Update()
    {
        if (Time.time >= endTime || !RoomSettings.AreVisualEffectsEnabled())
        {
            Destroy(this);
            return;
        }

        phase += Time.deltaTime;
        Vector2 radius = ResolveRadius();
        for (int i = 0; i < auraRing.positionCount; i++)
        {
            float angle = (i / (float)auraRing.positionCount) * Mathf.PI * 2f;
            float ripple = 1f + Mathf.Sin(angle * 5f + phase * 8f) * 0.04f;
            auraRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius.x * ripple, Mathf.Sin(angle) * radius.y * ripple, -0.18f));
        }

        float pulse = 0.5f + Mathf.Sin(phase * 9f) * 0.5f;
        Color color = Color.Lerp(new Color(1f, 0.06f, 0.04f, 0.34f), new Color(1f, 0.28f, 0.18f, 0.86f), pulse);
        auraRing.startColor = color;
        auraRing.endColor = color;
        auraRing.widthMultiplier = Mathf.Lerp(0.05f, 0.12f, pulse);
    }

    LineRenderer CreateRing(int sortingLayerId, int sortingOrder)
    {
        GameObject ringObject = new GameObject("RoburMarkAuraRing");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = Vector3.zero;
        ringObject.layer = gameObject.layer;

        LineRenderer line = ringObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = PointCount;
        line.widthMultiplier = 0.08f;
        line.numCapVertices = 8;
        line.numCornerVertices = 8;
        line.alignment = LineAlignment.View;
        line.material = GetMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
    }

    Vector2 ResolveRadius()
    {
        if (targetRenderer != null)
        {
            Vector3 scale = transform.lossyScale;
            float scaleX = Mathf.Max(0.001f, Mathf.Abs(scale.x));
            float scaleY = Mathf.Max(0.001f, Mathf.Abs(scale.y));
            Bounds bounds = targetRenderer.bounds;
            return new Vector2(
                Mathf.Max(0.62f, bounds.extents.x / scaleX + 0.22f),
                Mathf.Max(0.62f, bounds.extents.y / scaleY + 0.22f));
        }

        return new Vector2(0.78f, 0.78f);
    }

    void OnDestroy()
    {
        Transform ring = transform.Find("RoburMarkAuraRing");
        if (ring != null)
            Destroy(ring.gameObject);
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
            name = "RoburMarkAuraVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3400;
        return sharedMaterial;
    }
}

public sealed class ConfusionWaveVfx : MonoBehaviour
{
    const int PointCount = 128;
    static Material sharedMaterial;

    LineRenderer ring;
    Vector3 center;
    float radius;
    float duration;
    float age;

    public static void Spawn(Vector3 position, float targetRadius, float waveDuration)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject obj = new GameObject("CharlieConfusionWaveVfx");
        ConfusionWaveVfx vfx = obj.AddComponent<ConfusionWaveVfx>();
        vfx.Initialize(position, targetRadius, Mathf.Clamp(waveDuration * 0.38f, 0.55f, 1.5f));
    }

    void Initialize(Vector3 position, float targetRadius, float waveDuration)
    {
        center = new Vector3(position.x, position.y, -0.42f);
        radius = Mathf.Max(1f, targetRadius);
        duration = Mathf.Max(0.1f, waveDuration);
        transform.position = center;
        ring = CreateRing();
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        float currentRadius = Mathf.Lerp(0.25f, radius, Mathf.SmoothStep(0f, 1f, t));
        float alpha = Mathf.Sin(t * Mathf.PI) * 0.82f;

        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = (i / (float)ring.positionCount) * Mathf.PI * 2f;
            float ripple = 1f + Mathf.Sin(angle * 8f + age * 12f) * 0.015f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * currentRadius * ripple, Mathf.Sin(angle) * currentRadius * ripple, 0f));
        }

        Color color = new Color(0.22f, 1f, 0.34f, alpha);
        ring.startColor = color;
        ring.endColor = color;
        ring.widthMultiplier = Mathf.Lerp(0.12f, 0.34f, 1f - t);

        if (age >= duration)
            Destroy(gameObject);
    }

    LineRenderer CreateRing()
    {
        LineRenderer line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = PointCount;
        line.widthMultiplier = 0.18f;
        line.numCapVertices = 12;
        line.numCornerVertices = 8;
        line.alignment = LineAlignment.View;
        line.material = GetMaterial("CharlieConfusionWaveVfxMaterial", 3500);
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = GameVisualTheme.PlayerSortingOrder + 220;
        return line;
    }

    static Material GetMaterial(string name, int renderQueue)
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        sharedMaterial = new Material(shader)
        {
            name = name,
            color = Color.white
        };
        sharedMaterial.renderQueue = renderQueue;
        return sharedMaterial;
    }
}

public sealed class AtlasSuppressorWaveVfx : MonoBehaviour
{
    const int PointCount = 128;
    static Material sharedMaterial;

    LineRenderer outerRing;
    LineRenderer innerRing;
    float radius;
    float duration;
    float age;

    public static void Spawn(Vector3 position, float targetRadius)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject obj = new GameObject("AtlasSuppressorWaveVfx");
        AtlasSuppressorWaveVfx vfx = obj.AddComponent<AtlasSuppressorWaveVfx>();
        vfx.Initialize(position, targetRadius);
    }

    void Initialize(Vector3 position, float targetRadius)
    {
        radius = Mathf.Max(1f, targetRadius);
        duration = 0.9f;
        transform.position = new Vector3(position.x, position.y, -0.43f);
        outerRing = CreateRing("AtlasSuppressorOuterRing", 0.22f, GameVisualTheme.PlayerSortingOrder + 224);
        innerRing = CreateRing("AtlasSuppressorInnerRing", 0.08f, GameVisualTheme.PlayerSortingOrder + 225);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        float currentRadius = Mathf.Lerp(0.3f, radius, eased);
        float innerRadius = Mathf.Lerp(0.1f, radius * 0.62f, eased);
        float alpha = Mathf.Sin(t * Mathf.PI) * 0.86f;

        UpdateRing(outerRing, currentRadius, alpha, t, 0.02f);
        UpdateRing(innerRing, innerRadius, alpha * 0.7f, t, 0.035f);

        if (age >= duration)
            Destroy(gameObject);
    }

    void UpdateRing(LineRenderer ring, float currentRadius, float alpha, float t, float rippleScale)
    {
        if (ring == null)
            return;

        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = (i / (float)ring.positionCount) * Mathf.PI * 2f;
            float ripple = 1f + Mathf.Sin(angle * 10f + age * 16f) * rippleScale;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * currentRadius * ripple, Mathf.Sin(angle) * currentRadius * ripple, 0f));
        }

        Color hot = new Color(1f, 0.72f, 0.28f, alpha);
        Color pale = new Color(1f, 0.97f, 0.82f, alpha * 0.72f);
        Color color = Color.Lerp(hot, pale, Mathf.Sin(age * 18f) * 0.5f + 0.5f);
        ring.startColor = color;
        ring.endColor = color;
        ring.widthMultiplier = Mathf.Lerp(0.18f, 0.03f, t);
    }

    LineRenderer CreateRing(string objectName, float width, int sortingOrder)
    {
        GameObject ringObject = new GameObject(objectName);
        ringObject.transform.SetParent(transform, false);
        LineRenderer line = ringObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = PointCount;
        line.widthMultiplier = width;
        line.numCapVertices = 12;
        line.numCornerVertices = 8;
        line.alignment = LineAlignment.View;
        line.material = GetMaterial();
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = sortingOrder;
        return line;
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
            name = "AtlasSuppressorWaveVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3500;
        return sharedMaterial;
    }
}

public sealed class NovaScavengerBurstVfx : MonoBehaviour
{
    const float DefaultLifetime = 2.5f;
    const int BeamPoints = 13;
    const float BeamWidth = 0.18f;
    const float BeamJitterAmplitude = 0.13f;
    const float BeamJitterFrequency = 22f;
    static Material sharedMaterial;

    readonly List<LineRenderer> beams = new List<LineRenderer>();
    readonly List<Transform> targets = new List<Transform>();
    readonly List<Vector3> fallbackTargets = new List<Vector3>();
    Transform source;
    Vector3 fallbackSource;
    float age;
    float lifetime = DefaultLifetime;

    public static void Spawn(int sourceViewId, string targetViewIds, float duration)
    {
        if (sourceViewId <= 0 || string.IsNullOrWhiteSpace(targetViewIds) || !RoomSettings.AreVisualEffectsEnabled())
            return;

        PhotonView sourceView = PhotonView.Find(sourceViewId);
        if (sourceView == null)
            return;

        List<Transform> targetTransforms = new List<Transform>();
        string[] parts = targetViewIds.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out int viewId))
                continue;

            PhotonView targetView = PhotonView.Find(viewId);
            if (targetView != null)
                targetTransforms.Add(targetView.transform);
        }

        if (targetTransforms.Count == 0)
            return;

        GameObject obj = new GameObject("NovaScavengerBurstVfx");
        NovaScavengerBurstVfx vfx = obj.AddComponent<NovaScavengerBurstVfx>();
        vfx.Initialize(sourceView.transform, targetTransforms, duration);
    }

    void Initialize(Transform sourceTransform, List<Transform> targetTransforms, float duration)
    {
        source = sourceTransform;
        fallbackSource = source != null ? source.position : Vector3.zero;
        lifetime = Mathf.Max(0.1f, duration);
        transform.position = Vector3.zero;

        for (int i = 0; i < targetTransforms.Count; i++)
        {
            Transform target = targetTransforms[i];
            if (target == null)
                continue;

            targets.Add(target);
            fallbackTargets.Add(target.position);
            beams.Add(CreateBeam(i));
        }
    }

    void Update()
    {
        age += Time.deltaTime;
        float fadeIn = Mathf.Clamp01(age / 0.15f);
        float fadeOut = Mathf.Clamp01((lifetime - age) / 0.25f);
        float visibility = Mathf.Min(fadeIn, fadeOut);
        float pulse = Mathf.Sin(Time.time * 14f) * 0.5f + 0.5f;
        float alpha = Mathf.Lerp(0.72f, 1f, pulse) * visibility;
        Vector3 start = source != null ? source.position : fallbackSource;

        for (int i = 0; i < beams.Count; i++)
        {
            LineRenderer beam = beams[i];
            if (beam == null)
                continue;

            Vector3 end = targets[i] != null ? targets[i].position : fallbackTargets[i];
            Vector3 delta = end - start;
            Vector3 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : (source != null ? source.up : Vector3.up);
            Vector3 side = Vector3.Cross(direction, Vector3.forward).normalized;
            for (int p = 0; p < BeamPoints; p++)
            {
                float u = p / (float)(BeamPoints - 1);
                Vector3 point = Vector3.Lerp(start, end, u);
                float taper = Mathf.Sin(u * Mathf.PI);
                float waveA = Mathf.Sin((u * Mathf.PI * 5f) + Time.time * BeamJitterFrequency + i * 0.43f);
                float waveB = Mathf.Sin((u * Mathf.PI * 11f) - Time.time * 13f + i * 0.31f) * 0.45f;
                point += side * ((waveA + waveB) * BeamJitterAmplitude * taper * visibility);
                point.z = -0.36f;
                beam.SetPosition(p, point);
            }

            beam.colorGradient = BuildCollectionBeamGradient(alpha);
            beam.widthMultiplier = Mathf.Lerp(BeamWidth * 0.72f, BeamWidth * 1.3f, pulse) * visibility;
        }

        if (age >= lifetime)
            Destroy(gameObject);
    }

    LineRenderer CreateBeam(int index)
    {
        GameObject beamObject = new GameObject("NovaScavengerBeam_" + index);
        beamObject.transform.SetParent(transform, false);
        LineRenderer line = beamObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = BeamPoints;
        line.widthMultiplier = BeamWidth;
        line.startWidth = BeamWidth;
        line.endWidth = BeamWidth * 0.55f;
        line.widthCurve = BuildCollectionBeamWidthCurve();
        line.numCapVertices = 12;
        line.numCornerVertices = 10;
        line.alignment = LineAlignment.View;
        line.material = GetMaterial();
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = GameVisualTheme.PlayerSortingOrder + 240;
        return line;
    }

    static Gradient BuildCollectionBeamGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.96f, 1f, 0.86f), 0f),
                new GradientColorKey(new Color(0.28f, 1f, 0.66f), 0.38f),
                new GradientColorKey(new Color(0.1f, 0.74f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f * alpha, 0f),
                new GradientAlphaKey(0.72f * alpha, 0.55f),
                new GradientAlphaKey(0.18f * alpha, 1f)
            });
        return gradient;
    }

    static AnimationCurve BuildCollectionBeamWidthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.62f),
            new Keyframe(0.18f, 1.2f),
            new Keyframe(0.58f, 0.82f),
            new Keyframe(1f, 0.22f));
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
            name = "NovaScavengerBurstVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3500;
        return sharedMaterial;
    }
}

public sealed class ElectromagneticConeWaveVfx : MonoBehaviour
{
    const int ArcPoints = 44;
    const int ArcCount = 4;
    const float Duration = 0.82f;
    static Material sharedMaterial;

    readonly List<LineRenderer> arcs = new List<LineRenderer>();
    LineRenderer leftEdge;
    LineRenderer rightEdge;
    Vector2 forward = Vector2.up;
    float range;
    float halfAngle;
    float age;

    public static void Spawn(Vector3 origin, Vector2 direction, float targetRange, float coneAngleDegrees)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject obj = new GameObject("CovaxElectromagneticConeWaveVfx");
        ElectromagneticConeWaveVfx vfx = obj.AddComponent<ElectromagneticConeWaveVfx>();
        vfx.Initialize(origin, direction, targetRange, coneAngleDegrees);
    }

    void Initialize(Vector3 origin, Vector2 direction, float targetRange, float coneAngleDegrees)
    {
        transform.position = new Vector3(origin.x, origin.y, -0.43f);
        forward = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        range = Mathf.Max(1f, targetRange);
        halfAngle = Mathf.Clamp(coneAngleDegrees, 8f, 160f) * 0.5f;

        for (int i = 0; i < ArcCount; i++)
            arcs.Add(CreateLine("CovaxEmpArc_" + i, 0.14f));

        leftEdge = CreateLine("CovaxEmpLeftEdge", 0.085f);
        rightEdge = CreateLine("CovaxEmpRightEdge", 0.085f);
        leftEdge.positionCount = 2;
        rightEdge.positionCount = 2;
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Duration);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        float alpha = Mathf.Sin(t * Mathf.PI) * 0.86f;
        float currentRange = Mathf.Lerp(0.35f, range, eased);
        Color color = new Color(0.42f, 0.9f, 1f, alpha);

        for (int i = 0; i < arcs.Count; i++)
        {
            LineRenderer arc = arcs[i];
            if (arc == null)
                continue;

            float arcRadius = currentRange * ((i + 1f) / ArcCount);
            arc.positionCount = ArcPoints;
            arc.widthMultiplier = Mathf.Lerp(0.24f, 0.055f, t) * Mathf.Lerp(1.15f, 0.65f, i / (float)ArcCount);
            arc.startColor = color;
            arc.endColor = color;

            for (int p = 0; p < ArcPoints; p++)
            {
                float u = p / (float)(ArcPoints - 1);
                float angle = Mathf.Lerp(-halfAngle, halfAngle, u);
                float ripple = 1f + Mathf.Sin((u * Mathf.PI * 8f) + age * 20f + i) * 0.018f;
                Vector2 dir = Rotate(forward, angle);
                arc.SetPosition(p, new Vector3(dir.x * arcRadius * ripple, dir.y * arcRadius * ripple, 0f));
            }
        }

        UpdateEdge(leftEdge, -halfAngle, currentRange, color, t);
        UpdateEdge(rightEdge, halfAngle, currentRange, color, t);

        if (age >= Duration)
            Destroy(gameObject);
    }

    void UpdateEdge(LineRenderer line, float angle, float currentRange, Color color, float t)
    {
        if (line == null)
            return;

        Vector2 dir = Rotate(forward, angle);
        line.SetPosition(0, Vector3.zero);
        line.SetPosition(1, new Vector3(dir.x * currentRange, dir.y * currentRange, 0f));
        line.widthMultiplier = Mathf.Lerp(0.16f, 0.035f, t);
        line.startColor = new Color(color.r, color.g, color.b, color.a * 0.55f);
        line.endColor = color;
    }

    LineRenderer CreateLine(string objectName, float width)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = ArcPoints;
        line.widthMultiplier = width;
        line.numCapVertices = 10;
        line.numCornerVertices = 8;
        line.alignment = LineAlignment.View;
        line.material = GetMaterial();
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = GameVisualTheme.PlayerSortingOrder + 260;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    static Vector2 Rotate(Vector2 value, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(rad);
        float cos = Mathf.Cos(rad);
        return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
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
            name = "CovaxElectromagneticConeWaveVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3500;
        return sharedMaterial;
    }
}

public sealed class ElectromagneticShockVfx : MonoBehaviour
{
    const int RingPoints = 56;
    const int BoltCount = 6;
    static Material sharedMaterial;

    SpriteRenderer targetRenderer;
    GameObject rootObject;
    LineRenderer ring;
    LineRenderer[] bolts;
    float endTime;
    float phase;

    public static void Attach(GameObject target, float duration)
    {
        if (target == null || duration <= 0f || !RoomSettings.AreVisualEffectsEnabled())
            return;

        ElectromagneticShockVfx existing = target.GetComponent<ElectromagneticShockVfx>();
        if (existing != null)
        {
            existing.endTime = Mathf.Max(existing.endTime, Time.time + duration);
            return;
        }

        ElectromagneticShockVfx vfx = target.AddComponent<ElectromagneticShockVfx>();
        vfx.Initialize(duration);
    }

    void Initialize(float duration)
    {
        targetRenderer = GetComponentInChildren<SpriteRenderer>();
        endTime = Time.time + duration;

        rootObject = new GameObject("ElectromagneticShockVfxRoot");
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = new Vector3(0f, 0f, -0.16f);
        rootObject.layer = gameObject.layer;

        int sortingLayerId = targetRenderer != null ? targetRenderer.sortingLayerID : 0;
        int sortingOrder = targetRenderer != null ? targetRenderer.sortingOrder + 210 : GameVisualTheme.PlayerSortingOrder + 210;
        ring = CreateLine("ElectromagneticShockRing", sortingLayerId, sortingOrder, RingPoints, true, 0.055f);
        bolts = new LineRenderer[BoltCount];
        for (int i = 0; i < bolts.Length; i++)
            bolts[i] = CreateLine("ElectromagneticShockBolt_" + i, sortingLayerId, sortingOrder + 1, 3, false, 0.045f);
    }

    void Update()
    {
        if (Time.time >= endTime || !RoomSettings.AreVisualEffectsEnabled())
        {
            DestroySelf();
            return;
        }

        phase += Time.deltaTime;
        Vector2 radius = ResolveRadius();
        float pulse = 0.5f + Mathf.Sin(phase * 18f) * 0.5f;
        Color color = Color.Lerp(new Color(0.22f, 0.75f, 1f, 0.35f), new Color(0.9f, 1f, 1f, 0.92f), pulse);

        if (ring != null)
        {
            for (int i = 0; i < RingPoints; i++)
            {
                float angle = (i / (float)RingPoints) * Mathf.PI * 2f;
                float ripple = 1f + Mathf.Sin(angle * 9f + phase * 14f) * 0.05f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius.x * ripple, Mathf.Sin(angle) * radius.y * ripple, 0f));
            }

            ring.startColor = color;
            ring.endColor = color;
            ring.widthMultiplier = Mathf.Lerp(0.035f, 0.095f, pulse);
        }

        for (int i = 0; i < bolts.Length; i++)
        {
            LineRenderer bolt = bolts[i];
            if (bolt == null)
                continue;

            float angle = ((i / (float)bolts.Length) * Mathf.PI * 2f) + phase * (0.8f + i * 0.09f);
            float jitter = Mathf.Sin(phase * 27f + i * 1.7f) * 0.34f;
            Vector3 start = new Vector3(Mathf.Cos(angle) * radius.x * 0.72f, Mathf.Sin(angle) * radius.y * 0.72f, 0f);
            Vector3 end = new Vector3(Mathf.Cos(angle + jitter) * radius.x * 1.18f, Mathf.Sin(angle + jitter) * radius.y * 1.18f, 0f);
            Vector3 mid = (start + end) * 0.5f + new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f) * Mathf.Sin(phase * 21f + i) * 0.16f;
            bolt.SetPosition(0, start);
            bolt.SetPosition(1, mid);
            bolt.SetPosition(2, end);
            bolt.startColor = color;
            bolt.endColor = new Color(color.r, color.g, color.b, color.a * 0.12f);
            bolt.widthMultiplier = Mathf.Lerp(0.025f, 0.075f, pulse);
        }
    }

    LineRenderer CreateLine(string objectName, int sortingLayerId, int sortingOrder, int pointCount, bool loop, float width)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(rootObject.transform, false);
        lineObject.layer = gameObject.layer;
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = pointCount;
        line.widthMultiplier = width;
        line.numCapVertices = 8;
        line.numCornerVertices = 8;
        line.alignment = LineAlignment.View;
        line.material = GetMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    Vector2 ResolveRadius()
    {
        if (targetRenderer != null)
        {
            Vector3 scale = transform.lossyScale;
            float scaleX = Mathf.Max(0.001f, Mathf.Abs(scale.x));
            float scaleY = Mathf.Max(0.001f, Mathf.Abs(scale.y));
            Bounds bounds = targetRenderer.bounds;
            return new Vector2(
                Mathf.Max(0.64f, bounds.extents.x / scaleX + 0.18f),
                Mathf.Max(0.64f, bounds.extents.y / scaleY + 0.18f));
        }

        return new Vector2(0.78f, 0.78f);
    }

    void OnDestroy()
    {
        if (rootObject != null)
            Destroy(rootObject);
    }

    void DestroySelf()
    {
        if (rootObject != null)
        {
            Destroy(rootObject);
            rootObject = null;
        }

        Destroy(this);
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
            name = "ElectromagneticShockVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3500;
        return sharedMaterial;
    }
}

public sealed class AshSuperchargeVfx : MonoBehaviour
{
    const int RingPoints = 96;
    const int SparkCount = 8;
    const float ZOffset = -0.07f;
    const int SortingOrderOffset = 185;

    static Material sharedMaterial;

    SpriteRenderer targetRenderer;
    GameObject rootObject;
    LineRenderer chargeRing;
    LineRenderer pulseRing;
    LineRenderer[] sparks;
    float endTime;
    float phase;

    public static void Attach(GameObject target, float duration)
    {
        if (target == null || duration <= 0f || !RoomSettings.AreVisualEffectsEnabled())
            return;

        AshSuperchargeVfx existing = target.GetComponent<AshSuperchargeVfx>();
        if (existing != null)
        {
            existing.endTime = Mathf.Max(existing.endTime, Time.time + duration);
            return;
        }

        AshSuperchargeVfx vfx = target.AddComponent<AshSuperchargeVfx>();
        vfx.Initialize(duration);
    }

    void Initialize(float duration)
    {
        targetRenderer = GetComponentInChildren<SpriteRenderer>();
        endTime = Time.time + duration;
        rootObject = new GameObject("AshSuperchargeVfxRoot");
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = new Vector3(0f, 0f, ZOffset);
        rootObject.transform.localRotation = Quaternion.identity;
        rootObject.layer = gameObject.layer;

        int sortingLayerId = targetRenderer != null ? targetRenderer.sortingLayerID : 0;
        int sortingOrder = targetRenderer != null ? targetRenderer.sortingOrder + SortingOrderOffset : 1500;
        chargeRing = CreateLine("AshSuperchargeRing", sortingLayerId, sortingOrder, RingPoints + 1, true, 0.065f);
        pulseRing = CreateLine("AshSuperchargePulse", sortingLayerId, sortingOrder + 1, RingPoints + 1, true, 0.035f);
        sparks = new LineRenderer[SparkCount];
        for (int i = 0; i < sparks.Length; i++)
            sparks[i] = CreateLine("AshSuperchargeSpark_" + i, sortingLayerId, sortingOrder + 2, 3, false, 0.05f);
    }

    void Update()
    {
        if (Time.time >= endTime || !RoomSettings.AreVisualEffectsEnabled())
        {
            DestroySelf();
            return;
        }

        phase += Time.deltaTime;
        Vector2 radius = ResolveRadius();
        float pulse = 0.5f + Mathf.Sin(phase * 9f) * 0.5f;
        Color cyan = new Color(0.3f, 1f, 0.95f, Mathf.Lerp(0.4f, 0.82f, pulse));
        Color amber = new Color(1f, 0.56f, 0.18f, Mathf.Lerp(0.18f, 0.58f, 1f - pulse));
        SetRing(chargeRing, radius * Mathf.Lerp(1.03f, 1.12f, pulse), phase * 1.7f, cyan);
        SetRing(pulseRing, radius * Mathf.Lerp(0.82f, 0.98f, 1f - pulse), -phase * 2.2f, amber);
        SetSparks(radius, cyan, amber);
    }

    void SetRing(LineRenderer line, Vector2 radius, float offset, Color color)
    {
        if (line == null)
            return;

        for (int i = 0; i <= RingPoints; i++)
        {
            float t = i / (float)RingPoints;
            float angle = (t * Mathf.PI * 2f) + offset;
            float ripple = 1f + Mathf.Sin((angle * 8f) + phase * 10f) * 0.018f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius.x * ripple, Mathf.Sin(angle) * radius.y * ripple, 0f));
        }

        line.startColor = color;
        line.endColor = color;
    }

    void SetSparks(Vector2 radius, Color cyan, Color amber)
    {
        if (sparks == null)
            return;

        for (int i = 0; i < sparks.Length; i++)
        {
            LineRenderer spark = sparks[i];
            if (spark == null)
                continue;

            float angle = ((i / (float)sparks.Length) * Mathf.PI * 2f) + phase * (2.6f + i * 0.07f);
            float lead = Mathf.Sin(phase * 17f + i * 1.4f) * 0.2f;
            Vector3 start = new Vector3(Mathf.Cos(angle) * radius.x * 0.36f, Mathf.Sin(angle) * radius.y * 0.36f, 0f);
            Vector3 end = new Vector3(Mathf.Cos(angle + lead) * radius.x * 1.28f, Mathf.Sin(angle + lead) * radius.y * 1.28f, 0f);
            Vector3 mid = (start + end) * 0.5f + new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f) * 0.16f;
            spark.SetPosition(0, start);
            spark.SetPosition(1, mid);
            spark.SetPosition(2, end);

            Color color = Color.Lerp(cyan, amber, (i % 2) * 0.65f);
            spark.startColor = color;
            spark.endColor = new Color(color.r, color.g, color.b, 0.05f);
            spark.widthMultiplier = Mathf.Lerp(0.025f, 0.07f, Mathf.Abs(Mathf.Sin(phase * 12f + i)));
        }
    }

    LineRenderer CreateLine(string objectName, int sortingLayerId, int sortingOrder, int pointCount, bool loop, float width)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(rootObject.transform, false);
        lineObject.layer = gameObject.layer;
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = pointCount;
        line.widthMultiplier = width;
        line.numCapVertices = 8;
        line.numCornerVertices = 8;
        line.alignment = LineAlignment.View;
        line.material = GetMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    Vector2 ResolveRadius()
    {
        if (targetRenderer != null)
        {
            Vector3 scale = transform.lossyScale;
            float scaleX = Mathf.Max(0.001f, Mathf.Abs(scale.x));
            float scaleY = Mathf.Max(0.001f, Mathf.Abs(scale.y));
            Bounds bounds = targetRenderer.bounds;
            return new Vector2(
                Mathf.Max(0.68f, bounds.extents.x / scaleX + 0.22f),
                Mathf.Max(0.68f, bounds.extents.y / scaleY + 0.22f));
        }

        return new Vector2(0.84f, 0.84f);
    }

    void OnDestroy()
    {
        if (rootObject != null)
            Destroy(rootObject);
    }

    void DestroySelf()
    {
        if (rootObject != null)
        {
            Destroy(rootObject);
            rootObject = null;
        }

        Destroy(this);
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
            name = "AshSuperchargeVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3500;
        return sharedMaterial;
    }
}
