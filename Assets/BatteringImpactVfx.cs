using UnityEngine;
using UnityEngine.Rendering;

public sealed class BatteringImpactVfx : MonoBehaviour
{
    const float Lifetime = 0.72f;
    const float EffectZ = -0.31f;
    const int SparkCount = 18;
    const int RingSegments = 52;
    const int GlowTextureSize = 128;

    static Material sharedMaterial;
    static Sprite glowSprite;

    LineRenderer pressureRing;
    LineRenderer[] sparks;
    Vector3[] directions;
    float[] speeds;
    float[] lengths;
    float[] widths;
    float[] delays;
    Transform glowTransform;
    SpriteRenderer glowRenderer;
    Vector3 impactPosition;
    Vector3 impactNormal;
    float baseScale;
    float age;

    readonly Color hotCore = new Color(1f, 0.94f, 0.68f, 1f);
    readonly Color metalWhite = new Color(0.72f, 0.94f, 1f, 1f);
    readonly Color amber = new Color(1f, 0.46f, 0.08f, 1f);
    readonly Color ember = new Color(0.92f, 0.16f, 0.04f, 1f);

    public static void Prewarm()
    {
        GetMaterial();
        PrewarmSpriteTexture(GetGlowSprite());
    }

    public static void Spawn(Vector3 position, Vector2 normal, SpriteRenderer referenceRenderer = null)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject effect = new GameObject("BatteringImpactVfx");
        BatteringImpactVfx vfx = effect.AddComponent<BatteringImpactVfx>();
        vfx.Initialize(position, normal, referenceRenderer);
    }

    void Initialize(Vector3 position, Vector2 normal, SpriteRenderer referenceRenderer)
    {
        impactPosition = new Vector3(position.x, position.y, EffectZ);
        transform.position = impactPosition;

        Vector2 resolvedNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.up;
        impactNormal = new Vector3(resolvedNormal.x, resolvedNormal.y, 0f);
        baseScale = ResolveScale(referenceRenderer);

        int sortingLayerId = referenceRenderer != null ? referenceRenderer.sortingLayerID : ResolveForegroundSortingLayerId();
        int sortingOrder = referenceRenderer != null ? referenceRenderer.sortingOrder + 420 : 7000;

        CreateGlow(sortingLayerId, sortingOrder);
        CreatePressureRing(sortingLayerId, sortingOrder + 1);
        CreateSparks(sortingLayerId, sortingOrder + 2);
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
        GameObject glowObject = new GameObject("BatteringImpactFlash");
        glowObject.transform.SetParent(transform, false);
        glowTransform = glowObject.transform;

        glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = GetGlowSprite();
        glowRenderer.material = GetMaterial();
        glowRenderer.sortingLayerID = sortingLayerId;
        glowRenderer.sortingOrder = sortingOrder;
        glowRenderer.color = Color.clear;
    }

    void CreatePressureRing(int sortingLayerId, int sortingOrder)
    {
        GameObject ringObject = new GameObject("BatteringImpactPressureRing");
        ringObject.transform.SetParent(transform, false);

        pressureRing = ringObject.AddComponent<LineRenderer>();
        pressureRing.useWorldSpace = false;
        pressureRing.loop = true;
        pressureRing.positionCount = RingSegments;
        pressureRing.widthMultiplier = 0.08f;
        pressureRing.numCapVertices = 6;
        pressureRing.numCornerVertices = 6;
        pressureRing.alignment = LineAlignment.View;
        pressureRing.material = GetMaterial();
        pressureRing.sortingLayerID = sortingLayerId;
        pressureRing.sortingOrder = sortingOrder;

        for (int i = 0; i < RingSegments; i++)
        {
            float a = (i / (float)RingSegments) * Mathf.PI * 2f;
            pressureRing.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.72f, 0f) * 0.12f);
        }
    }

    void CreateSparks(int sortingLayerId, int sortingOrder)
    {
        sparks = new LineRenderer[SparkCount];
        directions = new Vector3[SparkCount];
        speeds = new float[SparkCount];
        lengths = new float[SparkCount];
        widths = new float[SparkCount];
        delays = new float[SparkCount];

        Vector3 tangent = new Vector3(-impactNormal.y, impactNormal.x, 0f);
        for (int i = 0; i < SparkCount; i++)
        {
            float spread = Mathf.Lerp(-1.05f, 1.05f, Hash01(i, 1));
            float forward = Mathf.Lerp(0.62f, 1.34f, Hash01(i, 2));
            directions[i] = (impactNormal * forward + tangent * spread).normalized;
            speeds[i] = baseScale * Mathf.Lerp(2.2f, 5.8f, Hash01(i, 3));
            lengths[i] = baseScale * Mathf.Lerp(0.18f, 0.62f, Hash01(i, 4));
            widths[i] = Mathf.Lerp(0.028f, 0.075f, Hash01(i, 5));
            delays[i] = Mathf.Lerp(0f, 0.075f, Hash01(i, 6));
            sparks[i] = CreateSparkLine("BatteringImpactSpark" + i, widths[i], sortingLayerId, sortingOrder + i);
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
        line.endWidth = width * 0.18f;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
    }

    void UpdateVisuals(float t)
    {
        UpdateGlow(t);
        UpdatePressureRing(t);
        UpdateSparks();
    }

    void UpdateGlow(float t)
    {
        if (glowTransform == null || glowRenderer == null)
            return;

        float flashT = Mathf.Clamp01(t / 0.46f);
        float scaleX = baseScale * Mathf.Lerp(0.34f, 0.86f, 1f - Mathf.Pow(1f - flashT, 2.2f));
        float scaleY = scaleX * Mathf.Lerp(0.62f, 0.92f, flashT);
        glowTransform.localScale = new Vector3(scaleX, scaleY, 1f);
        glowTransform.localRotation = Quaternion.FromToRotation(Vector3.right, impactNormal);
        glowRenderer.color = Color.Lerp(hotCore, amber, flashT * 0.5f) * new Color(1f, 1f, 1f, Mathf.Pow(1f - flashT, 2.1f) * 0.72f);
    }

    void UpdatePressureRing(float t)
    {
        if (pressureRing == null)
            return;

        float ringT = Mathf.Clamp01(t / 0.55f);
        float scale = baseScale * Mathf.Lerp(0.18f, 0.92f, 1f - Mathf.Pow(1f - ringT, 2.6f));
        pressureRing.transform.localScale = new Vector3(scale, scale, 1f);
        pressureRing.transform.localRotation = Quaternion.FromToRotation(Vector3.right, impactNormal);

        Color color = Color.Lerp(metalWhite, amber, ringT * 0.5f);
        color.a = Mathf.Pow(1f - ringT, 2.4f) * 0.74f;
        pressureRing.startColor = color;
        pressureRing.endColor = color;
        pressureRing.widthMultiplier = Mathf.Lerp(0.095f, 0.018f, ringT);
    }

    void UpdateSparks()
    {
        if (sparks == null)
            return;

        for (int i = 0; i < sparks.Length; i++)
        {
            LineRenderer spark = sparks[i];
            if (spark == null)
                continue;

            float localT = Mathf.Clamp01((age - delays[i]) / Mathf.Max(0.01f, Lifetime - delays[i]));
            float eased = 1f - Mathf.Pow(1f - localT, 2.55f);
            Vector3 head = impactPosition + directions[i] * speeds[i] * eased;
            Vector3 tail = head - directions[i] * lengths[i] * Mathf.Lerp(0.72f, 0.12f, localT);
            spark.SetPosition(0, tail);
            spark.SetPosition(1, head);

            float alpha = Mathf.Pow(1f - localT, 1.85f);
            Color start = Color.Lerp(hotCore, amber, localT * 0.65f);
            Color end = Color.Lerp(amber, ember, localT);
            start.a = alpha;
            end.a = alpha * 0.16f;
            spark.startColor = start;
            spark.endColor = end;

            float width = widths[i] * Mathf.Lerp(1f, 0.24f, localT);
            spark.widthMultiplier = width;
            spark.startWidth = width;
            spark.endWidth = width * 0.18f;
        }
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
            name = "BatteringImpactVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 5000;
        return sharedMaterial;
    }

    static Sprite GetGlowSprite()
    {
        if (glowSprite != null)
            return glowSprite;

        Texture2D texture = new Texture2D(GlowTextureSize, GlowTextureSize, TextureFormat.RGBA32, false)
        {
            name = "BatteringImpactGlowSprite",
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
                alpha = Mathf.Pow(alpha, 2.1f);
                pixels[(y * GlowTextureSize) + x] = new Color(1f, 0.72f, 0.26f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        glowSprite = Sprite.Create(texture, new Rect(0f, 0f, GlowTextureSize, GlowTextureSize), new Vector2(0.5f, 0.5f), GlowTextureSize);
        glowSprite.name = "BatteringImpactGlowSprite";
        return glowSprite;
    }

    static float ResolveScale(SpriteRenderer referenceRenderer)
    {
        if (referenceRenderer == null)
            return 1f;

        Bounds bounds = referenceRenderer.bounds;
        return Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.y), 0.75f, 3.2f);
    }

    static int ResolveForegroundSortingLayerId()
    {
        string[] preferredLayers = { "Player", "Bullets", "Walls", "Ground" };
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

    static float Hash01(int index, int salt)
    {
        return Mathf.Repeat(Mathf.Sin((index * 91.73f) + (salt * 417.19f)) * 52342.127f, 1f);
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }
}
