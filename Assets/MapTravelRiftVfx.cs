using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public sealed class MapTravelRiftVfx : MonoBehaviour
{
    const int RingSegments = 160;
    const float CollapseHoldSeconds = 0.95f;
    const int DiskTextureSize = 128;

    static readonly Dictionary<string, MapTravelRiftVfx> ActiveByToken = new Dictionary<string, MapTravelRiftVfx>(System.StringComparer.Ordinal);
    static Material additiveMaterial;
    static Sprite diskSprite;

    string token;
    double startTime;
    float duration;
    float radius;
    bool collapsing;
    float collapseStartedAt;
    SpriteRenderer diskRenderer;
    SpriteRenderer innerLensRenderer;
    LineRenderer outerRing;
    LineRenderer innerRing;
    LineRenderer fractureRing;
    ParticleSystem motes;

    public static void Show(string riftToken, Vector2 origin, float worldRadius, double riftStartTime, float riftDuration)
    {
        if (string.IsNullOrWhiteSpace(riftToken))
            return;

        if (ActiveByToken.TryGetValue(riftToken, out MapTravelRiftVfx existing) && existing != null)
        {
            existing.transform.position = new Vector3(origin.x, origin.y, -0.15f);
            existing.Configure(riftToken, worldRadius, riftStartTime, riftDuration);
            return;
        }

        GameObject root = new GameObject("MapTravelRiftVfx_" + riftToken);
        root.transform.position = new Vector3(origin.x, origin.y, -0.15f);
        MapTravelRiftVfx vfx = root.AddComponent<MapTravelRiftVfx>();
        vfx.Configure(riftToken, worldRadius, riftStartTime, riftDuration);
        ActiveByToken[riftToken] = vfx;
    }

    public static void Collapse(string riftToken)
    {
        if (string.IsNullOrWhiteSpace(riftToken))
            return;

        if (ActiveByToken.TryGetValue(riftToken, out MapTravelRiftVfx vfx) && vfx != null)
            vfx.BeginCollapse();
    }

    public static void ClearAll()
    {
        List<MapTravelRiftVfx> active = new List<MapTravelRiftVfx>(ActiveByToken.Values);
        ActiveByToken.Clear();
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i] != null)
                Destroy(active[i].gameObject);
        }
    }

    void Configure(string riftToken, float worldRadius, double riftStartTime, float riftDuration)
    {
        token = riftToken;
        radius = Mathf.Max(1f, worldRadius);
        startTime = riftStartTime;
        duration = Mathf.Max(0.1f, riftDuration);
        collapsing = false;

        EnsureVisuals();
        UpdateVisualState();
    }

    void OnDestroy()
    {
        if (!string.IsNullOrWhiteSpace(token) &&
            ActiveByToken.TryGetValue(token, out MapTravelRiftVfx active) &&
            active == this)
        {
            ActiveByToken.Remove(token);
        }
    }

    void Update()
    {
        UpdateVisualState();
    }

    void BeginCollapse()
    {
        if (collapsing)
            return;

        collapsing = true;
        collapseStartedAt = Time.time;
        if (motes != null)
        {
            ParticleSystem.EmissionModule emission = motes.emission;
            emission.rateOverTime = 0f;
            motes.Emit(80);
        }
    }

    void UpdateVisualState()
    {
        if (diskRenderer == null)
            return;

        double now = PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;
        float progress = Mathf.Clamp01((float)((now - startTime) / duration));
        if (progress >= 1f && !collapsing)
            BeginCollapse();

        float pulse = 0.5f + 0.5f * Mathf.Sin((Time.time * 5.7f) + progress * 8.2f);
        float slowPulse = 0.5f + 0.5f * Mathf.Sin((Time.time * 1.8f) + 1.1f);
        float collapseProgress = collapsing ? Mathf.Clamp01((Time.time - collapseStartedAt) / CollapseHoldSeconds) : 0f;
        float collapseScale = collapsing ? Mathf.Lerp(1f, 0.08f, Smooth(collapseProgress)) : 1f;
        float intensity = Mathf.Lerp(0.45f, 1f, progress) * Mathf.Lerp(1f, 2.2f, collapseProgress);
        float visibleRadius = radius * Mathf.Lerp(0.82f, 1.06f, pulse * 0.18f + progress * 0.82f) * collapseScale;

        transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 8.5f);

        ApplyDisk(diskRenderer, visibleRadius * 2f, new Color(0.06f, 0.95f, 0.98f, 0.16f * intensity * (1f - collapseProgress * 0.5f)));
        ApplyDisk(innerLensRenderer, visibleRadius * Mathf.Lerp(0.88f, 1.18f, slowPulse), new Color(0.6f, 0.18f, 1f, 0.105f * intensity * (1f - collapseProgress * 0.42f)));

        DrawRing(outerRing, visibleRadius, 0.09f + pulse * 0.075f, new Color(0.5f, 1f, 0.96f, Mathf.Lerp(0.55f, 0.95f, pulse) * intensity));
        DrawRing(innerRing, visibleRadius * Mathf.Lerp(0.48f, 0.62f, slowPulse), 0.045f, new Color(0.92f, 0.42f, 1f, 0.58f * intensity));
        DrawFractureRing(fractureRing, visibleRadius * Mathf.Lerp(0.76f, 0.92f, pulse), 0.038f, new Color(0.72f, 0.92f, 1f, 0.72f * intensity), progress);

        if (motes != null)
        {
            ParticleSystem.ShapeModule shape = motes.shape;
            shape.radius = Mathf.Max(0.2f, visibleRadius * 0.92f);
            ParticleSystem.MainModule main = motes.main;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.25f, 1f, 0.92f, 0.82f),
                new Color(0.92f, 0.42f, 1f, 0.6f));
        }

        if (collapsing && collapseProgress >= 1f)
            Destroy(gameObject);
    }

    void EnsureVisuals()
    {
        if (diskRenderer != null)
            return;

        diskRenderer = CreateDiskRenderer("RiftEventHorizon", -4);
        innerLensRenderer = CreateDiskRenderer("RiftLens", -3);
        outerRing = CreateRing("RiftOuterRing", 0);
        innerRing = CreateRing("RiftInnerRing", 1);
        fractureRing = CreateRing("RiftFractureRing", 2);
        motes = CreateParticles();
    }

    SpriteRenderer CreateDiskRenderer(string objectName, int sortingOrder)
    {
        GameObject child = new GameObject(objectName, typeof(SpriteRenderer));
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        renderer.sprite = GetDiskSprite();
        renderer.material = GetAdditiveMaterial();
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = 1800 + sortingOrder;
        return renderer;
    }

    LineRenderer CreateRing(string objectName, int sortingOrder)
    {
        GameObject child = new GameObject(objectName, typeof(LineRenderer));
        child.transform.SetParent(transform, false);
        LineRenderer renderer = child.GetComponent<LineRenderer>();
        renderer.useWorldSpace = false;
        renderer.loop = true;
        renderer.positionCount = RingSegments;
        renderer.material = GetAdditiveMaterial();
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = 1810 + sortingOrder;
        renderer.numCapVertices = 6;
        renderer.numCornerVertices = 6;
        return renderer;
    }

    ParticleSystem CreateParticles()
    {
        GameObject child = new GameObject("RiftForeignMotes", typeof(ParticleSystem));
        child.transform.SetParent(transform, false);
        ParticleSystem particles = child.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.42f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 260;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 90f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0.16f;
        shape.arc = 360f;

        ParticleSystem.ColorOverLifetimeModule colorLifetime = particles.colorOverLifetime;
        colorLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.22f, 1f, 0.94f), 0f),
                new GradientColorKey(new Color(0.92f, 0.44f, 1f), 0.62f),
                new GradientColorKey(new Color(0.18f, 0.32f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.88f, 0.16f),
                new GradientAlphaKey(0.46f, 0.68f),
                new GradientAlphaKey(0f, 1f)
            });
        colorLifetime.color = gradient;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetAdditiveMaterial();
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = 1820;
        particles.Play();
        return particles;
    }

    static void ApplyDisk(SpriteRenderer renderer, float diameter, Color color)
    {
        if (renderer == null)
            return;

        renderer.color = color;
        renderer.transform.localScale = new Vector3(diameter, diameter, 1f);
    }

    static void DrawRing(LineRenderer renderer, float ringRadius, float width, Color color)
    {
        if (renderer == null)
            return;

        renderer.widthMultiplier = width;
        renderer.startColor = color;
        renderer.endColor = color;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = (i / (float)RingSegments) * Mathf.PI * 2f;
            renderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * ringRadius, Mathf.Sin(angle) * ringRadius, 0f));
        }
    }

    static void DrawFractureRing(LineRenderer renderer, float ringRadius, float width, Color color, float progress)
    {
        if (renderer == null)
            return;

        renderer.widthMultiplier = width;
        renderer.startColor = color;
        renderer.endColor = color;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = (i / (float)RingSegments) * Mathf.PI * 2f;
            float noise = Mathf.PerlinNoise(i * 0.071f + progress * 2.4f, Time.time * 0.26f);
            float jag = Mathf.Lerp(0.93f, 1.08f, noise);
            renderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * ringRadius * jag, Mathf.Sin(angle) * ringRadius * jag, 0f));
        }
    }

    static Material GetAdditiveMaterial()
    {
        if (additiveMaterial != null)
            return additiveMaterial;

        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        additiveMaterial = new Material(shader)
        {
            name = "MapTravelRiftAdditiveMaterial"
        };
        return additiveMaterial;
    }

    static Sprite GetDiskSprite()
    {
        if (diskSprite != null)
            return diskSprite;

        Texture2D texture = new Texture2D(DiskTextureSize, DiskTextureSize, TextureFormat.RGBA32, false)
        {
            name = "MapTravelRiftDiskTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((DiskTextureSize - 1) * 0.5f, (DiskTextureSize - 1) * 0.5f);
        float maxRadius = DiskTextureSize * 0.5f;
        for (int y = 0; y < DiskTextureSize; y++)
        {
            for (int x = 0; x < DiskTextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                float alpha = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance));
                alpha *= Mathf.SmoothStep(1f, 0.08f, Mathf.Clamp01(distance));
                float ring = Mathf.Exp(-Mathf.Pow((distance - 0.68f) * 9.5f, 2f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha * 0.72f + ring * 0.28f)));
            }
        }

        texture.Apply(false, true);
        diskSprite = Sprite.Create(texture, new Rect(0f, 0f, DiskTextureSize, DiskTextureSize), new Vector2(0.5f, 0.5f), DiskTextureSize);
        diskSprite.name = "MapTravelRiftDiskSprite";
        return diskSprite;
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
