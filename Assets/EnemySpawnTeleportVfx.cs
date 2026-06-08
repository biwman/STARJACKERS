using UnityEngine;

public sealed class EnemySpawnTeleportVfx : MonoBehaviour
{
    const float Lifetime = 0.72f;
    const float AdvancedLifetime = 1.18f;
    const float EffectZ = -0.34f;
    const int CircleSegments = 72;
    const int AdvancedCircleSegments = 96;
    const int AdvancedSparkCount = 18;
    const string PortalSpritePath = "VFX/EnemySpawn/advanced_spawn_portal";
    const string ShockwaveSpritePath = "VFX/EnemySpawn/advanced_spawn_shockwave";
    const string FlashSpritePath = "VFX/EnemySpawn/advanced_spawn_flash";

    static Material lineMaterial;
    static Material spriteMaterial;
    static Sprite portalSprite;
    static Sprite shockwaveSprite;
    static Sprite flashSprite;

    LineRenderer outerRing;
    LineRenderer innerRing;
    LineRenderer[] beams;
    LineRenderer[] sparkLines;
    SpriteRenderer portalRenderer;
    SpriteRenderer portalEchoRenderer;
    SpriteRenderer shockwaveRenderer;
    SpriteRenderer flashRenderer;
    SpriteRenderer silhouetteRenderer;
    readonly Color outerColor = new Color(0.92f, 1f, 1f, 1f);
    readonly Color innerColor = new Color(0.12f, 0.84f, 1f, 1f);
    readonly Color beamColor = new Color(0.5f, 0.96f, 1f, 1f);
    Vector3 center;
    float baseRadius;
    float age;
    bool advanced;
    float[] sparkAngles;
    float[] sparkDistances;
    float[] sparkLengths;
    float[] sparkSpeeds;
    float[] sparkDelays;
    Vector3 silhouetteBaseScale = Vector3.one;

    public static void Prewarm()
    {
        GetLineMaterial();
        GetSpriteMaterial();
        LoadAdvancedSprites();
    }

    public static void Spawn(Vector3 position, SpriteRenderer referenceRenderer = null, float radius = 1f)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject effect = new GameObject("EnemySpawnTeleportVfx");
        EnemySpawnTeleportVfx vfx = effect.AddComponent<EnemySpawnTeleportVfx>();
        vfx.Initialize(position, referenceRenderer, radius);
    }

    void Initialize(Vector3 position, SpriteRenderer referenceRenderer, float radius)
    {
        center = new Vector3(position.x, position.y, EffectZ);
        transform.position = center;
        baseRadius = Mathf.Clamp(radius, 0.65f, 5.5f);
        advanced = RoomSettings.IsAdvancedSpawnVfxEnabled();

        int sortingLayerId = referenceRenderer != null ? referenceRenderer.sortingLayerID : 0;
        int sortingOrder = referenceRenderer != null ? referenceRenderer.sortingOrder + 135 : 1800;

        if (advanced)
            InitializeAdvanced(referenceRenderer, sortingLayerId, sortingOrder);
        else
            InitializeClassic(sortingLayerId, sortingOrder);

        UpdateVisuals(0f);
    }

    void InitializeClassic(int sortingLayerId, int sortingOrder)
    {
        outerRing = CreateLine("TeleportOuterRing", 0.12f, outerColor, sortingLayerId, sortingOrder);
        outerRing.loop = true;
        outerRing.positionCount = CircleSegments;

        innerRing = CreateLine("TeleportInnerRing", 0.08f, innerColor, sortingLayerId, sortingOrder + 1);
        innerRing.loop = true;
        innerRing.positionCount = CircleSegments;

        beams = new[]
        {
            CreateLine("TeleportBeamN", 0.1f, beamColor, sortingLayerId, sortingOrder + 2),
            CreateLine("TeleportBeamS", 0.1f, beamColor, sortingLayerId, sortingOrder + 2),
            CreateLine("TeleportBeamE", 0.1f, beamColor, sortingLayerId, sortingOrder + 2),
            CreateLine("TeleportBeamW", 0.1f, beamColor, sortingLayerId, sortingOrder + 2)
        };
    }

    void InitializeAdvanced(SpriteRenderer referenceRenderer, int sortingLayerId, int sortingOrder)
    {
        LoadAdvancedSprites();

        shockwaveRenderer = CreateSpriteLayer("TeleportShockwaveSprite", shockwaveSprite, sortingLayerId, sortingOrder - 1, new Color(0.62f, 0.96f, 1f, 0f));
        portalEchoRenderer = CreateSpriteLayer("TeleportPortalEchoSprite", portalSprite, sortingLayerId, sortingOrder, new Color(0.25f, 0.75f, 1f, 0f));
        portalRenderer = CreateSpriteLayer("TeleportPortalVortexSprite", portalSprite, sortingLayerId, sortingOrder + 1, new Color(0.78f, 1f, 1f, 0f));
        flashRenderer = CreateSpriteLayer("TeleportArrivalFlashSprite", flashSprite, sortingLayerId, sortingOrder + 6, new Color(0.9f, 1f, 1f, 0f));
        silhouetteRenderer = CreateSilhouetteLayer(referenceRenderer, sortingLayerId, sortingOrder + 4);

        outerRing = CreateLine("TeleportAdvancedOuterRing", 0.11f, outerColor, sortingLayerId, sortingOrder + 7);
        outerRing.loop = true;
        outerRing.positionCount = AdvancedCircleSegments;

        innerRing = CreateLine("TeleportAdvancedInnerRing", 0.07f, innerColor, sortingLayerId, sortingOrder + 8);
        innerRing.loop = true;
        innerRing.positionCount = AdvancedCircleSegments;

        beams = new LineRenderer[8];
        for (int i = 0; i < beams.Length; i++)
            beams[i] = CreateLine("TeleportAdvancedBeam" + i, 0.11f, beamColor, sortingLayerId, sortingOrder + 9);

        sparkLines = new LineRenderer[AdvancedSparkCount];
        sparkAngles = new float[AdvancedSparkCount];
        sparkDistances = new float[AdvancedSparkCount];
        sparkLengths = new float[AdvancedSparkCount];
        sparkSpeeds = new float[AdvancedSparkCount];
        sparkDelays = new float[AdvancedSparkCount];

        int seed = Mathf.RoundToInt((center.x * 37.11f + center.y * 91.73f + baseRadius * 13.7f) * 1000f);
        System.Random random = new System.Random(seed);
        for (int i = 0; i < sparkLines.Length; i++)
        {
            sparkLines[i] = CreateLine("TeleportAdvancedSpark" + i, 0.045f, beamColor, sortingLayerId, sortingOrder + 10);
            sparkAngles[i] = (float)(random.NextDouble() * Mathf.PI * 2f);
            sparkDistances[i] = Mathf.Lerp(0.42f, 1.24f, (float)random.NextDouble());
            sparkLengths[i] = Mathf.Lerp(0.18f, 0.52f, (float)random.NextDouble());
            sparkSpeeds[i] = Mathf.Lerp(-1.6f, 1.6f, (float)random.NextDouble());
            sparkDelays[i] = Mathf.Lerp(0f, 0.32f, (float)random.NextDouble());
        }
    }

    void Update()
    {
        age += Time.deltaTime;
        float lifetime = advanced ? AdvancedLifetime : Lifetime;
        float t = Mathf.Clamp01(age / lifetime);
        UpdateVisuals(t);

        if (age >= lifetime)
            Destroy(gameObject);
    }

    void UpdateVisuals(float t)
    {
        if (advanced)
            UpdateAdvancedVisuals(t);
        else
            UpdateClassicVisuals(t);
    }

    void UpdateClassicVisuals(float t)
    {
        float expand = EaseOutCubic(t);
        float pulse = Mathf.Sin(t * Mathf.PI);
        float outerRadius = Mathf.Lerp(baseRadius * 0.38f, baseRadius * 1.28f, expand);
        float innerRadius = Mathf.Lerp(baseRadius * 0.14f, baseRadius * 0.72f, expand);
        float alpha = Mathf.Lerp(1f, 0f, t);

        UpdateCircle(outerRing, outerRadius, alpha, Mathf.Lerp(0.16f, 0.03f, t));
        UpdateCircle(innerRing, innerRadius, alpha * 0.82f, Mathf.Lerp(0.1f, 0.02f, t));
        UpdateBeams(outerRadius, alpha, pulse);
    }

    void UpdateAdvancedVisuals(float t)
    {
        float ease = EaseOutCubic(t);
        float portalIn = SmoothRange(0f, 0.18f, t);
        float portalOut = 1f - SmoothRange(0.62f, 1f, t);
        float portalAlpha = portalIn * portalOut;
        float flashT = Mathf.Clamp01(t / 0.34f);
        float shockT = Mathf.Clamp01((t - 0.06f) / 0.72f);
        float pulse = Mathf.Sin(t * Mathf.PI);

        UpdateSpriteLayer(
            portalRenderer,
            baseRadius * Mathf.Lerp(1.18f, 2.72f, ease),
            age * -128f,
            new Color(0.74f, 1f, 1f, 0.82f * portalAlpha));

        UpdateSpriteLayer(
            portalEchoRenderer,
            baseRadius * Mathf.Lerp(1.76f, 3.38f, ease),
            age * 82f,
            new Color(0.25f, 0.86f, 1f, 0.28f * portalAlpha));

        UpdateSpriteLayer(
            shockwaveRenderer,
            baseRadius * Mathf.Lerp(0.82f, 4.15f, EaseOutCubic(shockT)),
            age * 18f,
            new Color(0.68f, 0.96f, 1f, 0.78f * Mathf.Pow(1f - shockT, 1.45f)));

        UpdateSpriteLayer(
            flashRenderer,
            baseRadius * Mathf.Lerp(0.82f, 3.04f, EaseOutCubic(flashT)),
            age * -34f,
            new Color(0.92f, 1f, 1f, 0.92f * Mathf.Pow(1f - flashT, 2.2f)));

        if (silhouetteRenderer != null)
        {
            float silhouetteAlpha = 0.56f * (1f - SmoothRange(0.08f, 0.58f, t));
            silhouetteRenderer.transform.localScale = silhouetteBaseScale * Mathf.Lerp(1.34f, 0.98f, EaseOutCubic(Mathf.Clamp01(t / 0.52f)));
            silhouetteRenderer.color = new Color(0.45f, 0.95f, 1f, silhouetteAlpha);
        }

        float ringAlpha = portalAlpha * (0.72f + 0.22f * pulse);
        UpdateAdvancedCircle(outerRing, baseRadius * Mathf.Lerp(0.58f, 1.86f, ease), ringAlpha, Mathf.Lerp(0.16f, 0.035f, t), outerColor, 0.58f);
        UpdateAdvancedCircle(innerRing, baseRadius * Mathf.Lerp(0.22f, 1.05f, ease), ringAlpha * 0.82f, Mathf.Lerp(0.1f, 0.024f, t), innerColor, -0.92f);
        UpdateAdvancedBeams(baseRadius * Mathf.Lerp(0.5f, 1.82f, ease), ringAlpha, pulse);
        UpdateAdvancedSparks(t);
    }

    void UpdateCircle(LineRenderer ring, float radius, float alpha, float width)
    {
        if (ring == null)
            return;

        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / CircleSegments;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            ring.SetPosition(i, point);
        }

        Color start = ring == outerRing ? outerColor : innerColor;
        start.a = alpha;
        ring.startColor = start;
        ring.endColor = start;
        ring.widthMultiplier = width;
        ring.startWidth = width;
        ring.endWidth = width;
    }

    void UpdateAdvancedCircle(LineRenderer ring, float radius, float alpha, float width, Color color, float phase)
    {
        if (ring == null)
            return;

        int count = ring.positionCount;
        for (int i = 0; i < count; i++)
        {
            float angle = (Mathf.PI * 2f * i) / count;
            float wobble = Mathf.Sin(angle * 7f + age * 8f + phase) * baseRadius * 0.026f * alpha;
            float spark = Mathf.Sin(angle * 19f - age * 13f - phase) * baseRadius * 0.012f * alpha;
            float resolvedRadius = Mathf.Max(0.01f, radius + wobble + spark);
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * resolvedRadius, Mathf.Sin(angle) * resolvedRadius, 0f);
            ring.SetPosition(i, point);
        }

        color.a = alpha;
        ring.startColor = color;
        ring.endColor = color;
        ring.widthMultiplier = width;
        ring.startWidth = width;
        ring.endWidth = width;
    }

    void UpdateBeams(float radius, float alpha, float pulse)
    {
        if (beams == null)
            return;

        Vector3[] directions =
        {
            Vector3.up,
            Vector3.down,
            Vector3.right,
            Vector3.left
        };

        for (int i = 0; i < beams.Length && i < directions.Length; i++)
        {
            LineRenderer beam = beams[i];
            if (beam == null)
                continue;

            Vector3 direction = directions[i];
            float startDistance = radius * Mathf.Lerp(0.14f, 0.38f, pulse);
            float endDistance = radius * Mathf.Lerp(0.95f, 1.18f, pulse);
            beam.SetPosition(0, center + direction * startDistance);
            beam.SetPosition(1, center + direction * endDistance);

            Color color = beamColor;
            color.a = alpha * 0.86f;
            beam.startColor = color;
            color.a = alpha * 0.15f;
            beam.endColor = color;
            float width = Mathf.Lerp(0.13f, 0.03f, age / Lifetime);
            beam.widthMultiplier = width;
            beam.startWidth = width;
            beam.endWidth = width * 0.5f;
        }
    }

    void UpdateAdvancedBeams(float radius, float alpha, float pulse)
    {
        if (beams == null)
            return;

        for (int i = 0; i < beams.Length; i++)
        {
            LineRenderer beam = beams[i];
            if (beam == null)
                continue;

            float angle = (Mathf.PI * 2f * i) / beams.Length + age * 0.36f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            float startDistance = radius * Mathf.Lerp(0.04f, 0.22f, pulse);
            float endDistance = radius * Mathf.Lerp(0.86f, 1.62f + (i % 2) * 0.18f, pulse);
            beam.SetPosition(0, center + direction * startDistance);
            beam.SetPosition(1, center + direction * endDistance);

            Color start = beamColor;
            start.a = alpha * Mathf.Lerp(0.72f, 0.95f, pulse);
            Color end = start;
            end.a = alpha * 0.03f;
            beam.startColor = start;
            beam.endColor = end;
            float width = Mathf.Lerp(0.14f, 0.018f, Mathf.Clamp01(age / AdvancedLifetime));
            beam.widthMultiplier = width;
            beam.startWidth = width;
            beam.endWidth = width * 0.18f;
        }
    }

    void UpdateAdvancedSparks(float t)
    {
        if (sparkLines == null)
            return;

        for (int i = 0; i < sparkLines.Length; i++)
        {
            LineRenderer spark = sparkLines[i];
            if (spark == null)
                continue;

            float localT = Mathf.Clamp01((t - sparkDelays[i]) / Mathf.Max(0.01f, 1f - sparkDelays[i]));
            if (t < sparkDelays[i])
            {
                SetLineAlpha(spark, 0f);
                continue;
            }

            float angle = sparkAngles[i] + sparkSpeeds[i] * age;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            float startDistance = baseRadius * (sparkDistances[i] + localT * 1.12f);
            float length = baseRadius * sparkLengths[i] * Mathf.Lerp(1f, 0.42f, localT);
            spark.SetPosition(0, center + direction * startDistance);
            spark.SetPosition(1, center + direction * (startDistance + length));

            float alpha = Mathf.Sin(localT * Mathf.PI) * Mathf.Pow(1f - t, 0.72f);
            Color start = new Color(0.78f, 1f, 1f, alpha * 0.95f);
            Color end = new Color(0.3f, 0.9f, 1f, alpha * 0.04f);
            spark.startColor = start;
            spark.endColor = end;
            float width = Mathf.Lerp(0.07f, 0.012f, localT);
            spark.widthMultiplier = width;
            spark.startWidth = width;
            spark.endWidth = width * 0.28f;
        }
    }

    void SetLineAlpha(LineRenderer line, float alpha)
    {
        Color start = line.startColor;
        Color end = line.endColor;
        start.a = alpha;
        end.a = alpha;
        line.startColor = start;
        line.endColor = end;
    }

    void UpdateSpriteLayer(SpriteRenderer renderer, float diameter, float rotationDegrees, Color color)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        float spriteWidth = Mathf.Max(0.01f, Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y));
        float scale = Mathf.Max(0.01f, diameter / spriteWidth);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
        renderer.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        renderer.color = color;
    }

    SpriteRenderer CreateSpriteLayer(string objectName, Sprite sprite, int sortingLayerId, int sortingOrder, Color color)
    {
        if (sprite == null)
            return null;

        GameObject spriteObject = new GameObject(objectName);
        spriteObject.transform.SetParent(transform, false);
        spriteObject.transform.localPosition = Vector3.zero;
        spriteObject.transform.localScale = Vector3.zero;

        SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = GetSpriteMaterial();
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        renderer.color = color;
        return renderer;
    }

    SpriteRenderer CreateSilhouetteLayer(SpriteRenderer referenceRenderer, int sortingLayerId, int sortingOrder)
    {
        if (referenceRenderer == null || referenceRenderer.sprite == null)
            return null;

        GameObject spriteObject = new GameObject("TeleportArrivalSilhouette");
        spriteObject.transform.SetParent(transform, false);
        spriteObject.transform.localPosition = Vector3.zero;
        spriteObject.transform.localRotation = referenceRenderer.transform.rotation;
        silhouetteBaseScale = referenceRenderer.transform.lossyScale;
        spriteObject.transform.localScale = silhouetteBaseScale;

        SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
        renderer.sprite = referenceRenderer.sprite;
        renderer.flipX = referenceRenderer.flipX;
        renderer.flipY = referenceRenderer.flipY;
        renderer.sharedMaterial = GetSpriteMaterial();
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        renderer.color = new Color(0.45f, 0.95f, 1f, 0f);
        return renderer;
    }

    LineRenderer CreateLine(string objectName, float width, Color color, int sortingLayerId, int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = width;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 10;
        line.numCornerVertices = 6;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetLineMaterial();
        line.startColor = color;
        line.endColor = color;
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
    }

    static void LoadAdvancedSprites()
    {
        if (portalSprite == null)
            portalSprite = LoadSprite(PortalSpritePath);
        if (shockwaveSprite == null)
            shockwaveSprite = LoadSprite(ShockwaveSpritePath);
        if (flashSprite == null)
            flashSprite = LoadSprite(FlashSpritePath);
    }

    static Sprite LoadSprite(string path)
    {
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(path);
        if (sprites != null && sprites.Length > 0)
            return sprites[0];

        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
            return null;

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static Material GetLineMaterial()
    {
        if (lineMaterial != null)
            return lineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        lineMaterial = new Material(shader)
        {
            name = "EnemySpawnTeleportVfxLineMaterial",
            color = Color.white
        };
        lineMaterial.renderQueue = 3200;
        return lineMaterial;
    }

    static Material GetSpriteMaterial()
    {
        if (spriteMaterial != null)
            return spriteMaterial;

        Shader shader = Shader.Find("Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        spriteMaterial = new Material(shader)
        {
            name = "EnemySpawnTeleportVfxSpriteMaterial",
            color = Color.white
        };
        spriteMaterial.renderQueue = 3200;
        return spriteMaterial;
    }

    static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    static float SmoothRange(float start, float end, float value)
    {
        if (Mathf.Abs(end - start) < 0.0001f)
            return value >= end ? 1f : 0f;

        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((value - start) / (end - start)));
    }
}
