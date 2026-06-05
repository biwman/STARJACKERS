using UnityEngine;

public sealed class HpHitSparksVfx : MonoBehaviour
{
    const float Lifetime = 0.58f;
    const float EffectZ = -0.35f;
    const int SparkCount = 11;
    const int GlowTextureSize = 96;

    static Material lineMaterial;
    static Sprite glowSprite;

    LineRenderer[] sparks;
    Vector3[] directions;
    float[] lengths;
    float[] speeds;
    float[] delays;
    float[] widths;
    Transform glowTransform;
    SpriteRenderer glowRenderer;
    Vector3 impactPosition;
    Vector3 normal;
    float baseScale;
    float age;

    readonly Color hotWhite = new Color(1f, 0.92f, 0.68f, 1f);
    readonly Color ember = new Color(1f, 0.46f, 0.12f, 1f);
    readonly Color dimRed = new Color(0.62f, 0.12f, 0.04f, 1f);

    public static void Prewarm()
    {
        GetLineMaterial();
        PrewarmSpriteTexture(GetGlowSprite());
    }

    public static void Spawn(Vector3 position, Vector3 targetCenter, SpriteRenderer referenceRenderer = null)
    {
        GameObject effect = new GameObject("HpHitSparksVfx");
        HpHitSparksVfx vfx = effect.AddComponent<HpHitSparksVfx>();
        vfx.Initialize(position, targetCenter, referenceRenderer);
    }

    void Initialize(Vector3 position, Vector3 targetCenter, SpriteRenderer referenceRenderer)
    {
        impactPosition = new Vector3(position.x, position.y, EffectZ);
        transform.position = impactPosition;

        Vector3 outward = impactPosition - new Vector3(targetCenter.x, targetCenter.y, EffectZ);
        if (outward.sqrMagnitude < 0.0001f)
            outward = Vector3.up;
        normal = outward.normalized;

        baseScale = ResolveTargetScale(referenceRenderer);
        int sortingLayerId = ResolveEffectSortingLayerId();
        int sortingOrder = referenceRenderer != null ? referenceRenderer.sortingOrder + 360 : 6600;

        CreateGlow(sortingLayerId, sortingOrder);
        CreateSparks(sortingLayerId, sortingOrder + 1);
        UpdateVisuals(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Lifetime);
        UpdateVisuals(t);

        if (age >= Lifetime)
            Destroy(gameObject);
    }

    void CreateGlow(int sortingLayerId, int sortingOrder)
    {
        GameObject glowObject = new GameObject("HpHitWarmMetalFlash");
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = Vector3.zero;
        glowObject.transform.localRotation = Quaternion.identity;
        glowTransform = glowObject.transform;

        glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = GetGlowSprite();
        glowRenderer.color = Color.clear;
        glowRenderer.sortingLayerID = sortingLayerId;
        glowRenderer.sortingOrder = sortingOrder;
    }

    void CreateSparks(int sortingLayerId, int sortingOrder)
    {
        sparks = new LineRenderer[SparkCount];
        directions = new Vector3[SparkCount];
        lengths = new float[SparkCount];
        speeds = new float[SparkCount];
        delays = new float[SparkCount];
        widths = new float[SparkCount];

        Vector3 tangent = new Vector3(-normal.y, normal.x, 0f);
        for (int i = 0; i < SparkCount; i++)
        {
            float spread = Mathf.Lerp(-0.78f, 0.78f, Hash01(i, 1));
            Vector3 direction = (normal * Mathf.Lerp(0.72f, 1.22f, Hash01(i, 2)) + tangent * spread).normalized;
            directions[i] = direction;
            lengths[i] = baseScale * Mathf.Lerp(0.18f, 0.54f, Hash01(i, 3));
            speeds[i] = baseScale * Mathf.Lerp(0.75f, 1.8f, Hash01(i, 4));
            delays[i] = Mathf.Lerp(0f, 0.08f, Hash01(i, 5));
            widths[i] = Mathf.Lerp(0.025f, 0.06f, Hash01(i, 6));
            sparks[i] = CreateSparkLine("HpMetalSpark" + i, widths[i], sortingLayerId, sortingOrder + i);
        }
    }

    void UpdateVisuals(float t)
    {
        UpdateGlow(t);
        UpdateSparks(t);
    }

    void UpdateGlow(float t)
    {
        if (glowTransform == null || glowRenderer == null)
            return;

        float flashT = Mathf.Clamp01(t / 0.42f);
        float scale = baseScale * Mathf.Lerp(0.22f, 0.48f, 1f - Mathf.Pow(1f - flashT, 2f));
        glowTransform.localScale = new Vector3(scale, scale, 1f);
        glowRenderer.color = new Color(1f, 0.62f, 0.26f, Mathf.Pow(1f - flashT, 2.4f) * 0.52f);
    }

    void UpdateSparks(float t)
    {
        if (sparks == null)
            return;

        for (int i = 0; i < sparks.Length; i++)
        {
            LineRenderer spark = sparks[i];
            if (spark == null)
                continue;

            float localT = Mathf.Clamp01((age - delays[i]) / Mathf.Max(0.01f, Lifetime - delays[i]));
            float eased = 1f - Mathf.Pow(1f - localT, 2.4f);
            Vector3 travel = directions[i] * speeds[i] * eased;
            Vector3 head = impactPosition + travel;
            Vector3 tail = head - directions[i] * lengths[i] * Mathf.Lerp(0.55f, 0.16f, localT);

            spark.SetPosition(0, tail);
            spark.SetPosition(1, head);

            float alpha = Mathf.Pow(1f - localT, 1.85f);
            Color start = Color.Lerp(hotWhite, ember, localT * 0.7f);
            Color end = Color.Lerp(ember, dimRed, localT);
            start.a = alpha;
            end.a = alpha * 0.12f;
            spark.startColor = start;
            spark.endColor = end;

            float width = widths[i] * Mathf.Lerp(1f, 0.28f, localT);
            spark.widthMultiplier = width;
            spark.startWidth = width;
            spark.endWidth = width * 0.16f;
        }
    }

    LineRenderer CreateSparkLine(string objectName, float width, int sortingLayerId, int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = width;
        line.startWidth = width;
        line.endWidth = width * 0.16f;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetLineMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
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
            name = "HpHitSparksVfxLineMaterial",
            color = Color.white
        };
        lineMaterial.renderQueue = 5000;
        return lineMaterial;
    }

    static int ResolveEffectSortingLayerId()
    {
        string[] preferredLayers = { "Bullets", "Player", "Walls", "Ground" };
        SortingLayer[] layers = SortingLayer.layers;
        for (int preferredIndex = 0; preferredIndex < preferredLayers.Length; preferredIndex++)
        {
            string preferredName = preferredLayers[preferredIndex];
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == preferredName)
                    return layers[i].id;
            }
        }

        return 0;
    }

    static float ResolveTargetScale(SpriteRenderer referenceRenderer)
    {
        if (referenceRenderer == null)
            return 1f;

        Bounds bounds = referenceRenderer.bounds;
        float largest = Mathf.Max(bounds.size.x, bounds.size.y);
        return Mathf.Clamp(largest, 0.8f, 4.5f);
    }

    static float Hash01(int index, int salt)
    {
        return Mathf.Repeat(Mathf.Sin((index * 127.1f) + (salt * 311.7f)) * 43758.5453f, 1f);
    }

    static Sprite GetGlowSprite()
    {
        if (glowSprite != null)
            return glowSprite;

        Texture2D texture = new Texture2D(GlowTextureSize, GlowTextureSize, TextureFormat.RGBA32, false)
        {
            name = "HpHitSparksGlowSprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[GlowTextureSize * GlowTextureSize];
        float centerPixel = (GlowTextureSize - 1) * 0.5f;
        for (int y = 0; y < GlowTextureSize; y++)
        {
            for (int x = 0; x < GlowTextureSize; x++)
            {
                float dx = (x - centerPixel) / centerPixel;
                float dy = (y - centerPixel) / centerPixel;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = Mathf.Pow(alpha, 2.4f);
                pixels[(y * GlowTextureSize) + x] = new Color(1f, 0.74f, 0.32f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        glowSprite = Sprite.Create(texture, new Rect(0f, 0f, GlowTextureSize, GlowTextureSize), new Vector2(0.5f, 0.5f), GlowTextureSize);
        glowSprite.name = "HpHitSparksGlowSprite";
        return glowSprite;
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }
}
