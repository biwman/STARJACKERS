using System.Collections.Generic;
using UnityEngine;

public sealed class SpaceMineExplosionVfx : MonoBehaviour
{
    const float Lifetime = 1.55f;
    const float EffectZ = -0.35f;
    const int TextureSize = 160;
    const int CircleSegments = 64;
    const int FireLayerCount = 4;
    const int SmokeLayerCount = 5;
    const int SparkCount = 12;
    const int LightweightCircleSegments = 40;
    const int LightweightFireLayerCount = 3;
    const int LightweightSmokeLayerCount = 3;
    const int LightweightSparkCount = 6;
    const int PoolWarmCount = 4;

    sealed class SpriteLayer
    {
        public Transform Transform;
        public SpriteRenderer Renderer;
        public Vector2 Direction;
        public float Distance;
        public float StartScale;
        public float EndScale;
        public float RotationSpeed;
        public float Delay;
        public float Alpha;
    }

    static Material lineMaterial;
    static Sprite fireSprite;
    static Sprite smokeSprite;
    static readonly Stack<SpaceMineExplosionVfx> Pool = new Stack<SpaceMineExplosionVfx>(PoolWarmCount);
    static Vector3[] circleUnitPoints;

    readonly SpriteLayer[] fireLayers = new SpriteLayer[FireLayerCount];
    readonly SpriteLayer[] smokeLayers = new SpriteLayer[SmokeLayerCount];
    readonly LineRenderer[] sparks = new LineRenderer[SparkCount];

    LineRenderer shockwave;
    LineRenderer heatRing;
    Vector3 center;
    float blastRadius;
    float age;
    int activeFireLayerCount;
    int activeSmokeLayerCount;
    int activeSparkCount;
    int activeCircleSegments;

    readonly Color hotCore = new Color(1f, 0.88f, 0.48f, 1f);
    readonly Color amber = new Color(1f, 0.43f, 0.08f, 1f);
    readonly Color deepFire = new Color(0.9f, 0.16f, 0.03f, 1f);
    readonly Color smokeColor = new Color(0.28f, 0.24f, 0.2f, 1f);

    public static void Prewarm()
    {
        GetLineMaterial();
        PrewarmSpriteTexture(GetFireSprite());
        PrewarmSpriteTexture(GetSmokeSprite());
        WarmPool(PoolWarmCount);
    }

    public static void Spawn(Vector3 position, float radius, SpriteRenderer referenceRenderer = null, bool lightweight = false)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        SpaceMineExplosionVfx vfx = GetFromPool();
        vfx.Initialize(position, radius, referenceRenderer, lightweight);
    }

    static void WarmPool(int count)
    {
        for (int i = Pool.Count; i < count; i++)
        {
            SpaceMineExplosionVfx vfx = CreateInstance();
            vfx.Initialize(Vector3.zero, 1.15f, null, false);
            vfx.ReturnToPool();
        }
    }

    static SpaceMineExplosionVfx GetFromPool()
    {
        while (Pool.Count > 0)
        {
            SpaceMineExplosionVfx pooled = Pool.Pop();
            if (pooled != null)
            {
                pooled.gameObject.SetActive(true);
                return pooled;
            }
        }

        SpaceMineExplosionVfx created = CreateInstance();
        created.gameObject.SetActive(true);
        return created;
    }

    static SpaceMineExplosionVfx CreateInstance()
    {
        GameObject effect = new GameObject("SpaceMineExplosionVfx");
        effect.SetActive(false);
        return effect.AddComponent<SpaceMineExplosionVfx>();
    }

    void Initialize(Vector3 position, float radius, SpriteRenderer referenceRenderer, bool lightweight)
    {
        center = new Vector3(position.x, position.y, EffectZ);
        transform.position = center;
        blastRadius = Mathf.Clamp(radius, 1.15f, 10f);
        age = 0f;
        activeFireLayerCount = lightweight ? LightweightFireLayerCount : FireLayerCount;
        activeSmokeLayerCount = lightweight ? LightweightSmokeLayerCount : SmokeLayerCount;
        activeSparkCount = lightweight ? LightweightSparkCount : SparkCount;
        activeCircleSegments = lightweight ? LightweightCircleSegments : CircleSegments;

        int sortingLayerId = referenceRenderer != null ? referenceRenderer.sortingLayerID : ResolveForegroundSortingLayerId();
        int sortingOrder = referenceRenderer != null ? referenceRenderer.sortingOrder + 320 : 6500;

        CreateOrResetFireLayers(sortingLayerId, sortingOrder);
        CreateOrResetSmokeLayers(sortingLayerId, sortingOrder + 20);

        shockwave = CreateOrResetLine(shockwave, "MinePressureWave", 0.16f, new Color(1f, 0.72f, 0.38f, 0.7f), sortingLayerId, sortingOrder + 40, true, activeCircleSegments);
        heatRing = CreateOrResetLine(heatRing, "MineHeatDistortionRing", 0.08f, new Color(1f, 0.9f, 0.62f, 0.42f), sortingLayerId, sortingOrder + 41, true, activeCircleSegments);

        for (int i = 0; i < sparks.Length; i++)
        {
            if (i >= activeSparkCount)
            {
                if (sparks[i] != null)
                    sparks[i].gameObject.SetActive(false);
                continue;
            }

            sparks[i] = CreateOrResetLine(sparks[i], "MineEmberSpark" + i, 0.05f, amber, sortingLayerId, sortingOrder + 50, false, 2);
        }

        UpdateVisuals(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Lifetime);
        UpdateVisuals(t);

        if (age >= Lifetime)
            ReturnToPool();
    }

    void UpdateVisuals(float t)
    {
        UpdateFire(t);
        UpdateSmoke(t);
        UpdateShockwave(t);
        UpdateSparks(t);
    }

    void ReturnToPool()
    {
        gameObject.SetActive(false);
        Pool.Push(this);
    }

    void CreateOrResetFireLayers(int sortingLayerId, int sortingOrder)
    {
        Sprite sprite = GetFireSprite();
        for (int i = 0; i < fireLayers.Length; i++)
        {
            if (i >= activeFireLayerCount)
            {
                if (fireLayers[i] != null && fireLayers[i].Renderer != null)
                    fireLayers[i].Renderer.gameObject.SetActive(false);
                continue;
            }

            float angle = (Mathf.PI * 2f * i) / Mathf.Max(1, activeFireLayerCount) + Hash01(i, 1) * 0.45f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            SpriteLayer layer = fireLayers[i] ?? CreateSpriteLayer("MineFireBloom" + i, sprite, sortingLayerId, sortingOrder + i);
            ResetSpriteLayer(layer, sprite, sortingLayerId, sortingOrder + i);
            layer.Direction = direction;
            layer.Distance = Mathf.Lerp(0.04f, 0.22f, Hash01(i, 2));
            layer.StartScale = Mathf.Lerp(0.52f, 0.78f, Hash01(i, 3));
            layer.EndScale = Mathf.Lerp(1.05f, 1.42f, Hash01(i, 4));
            layer.RotationSpeed = Mathf.Lerp(-34f, 34f, Hash01(i, 5));
            layer.Delay = Mathf.Lerp(0f, 0.08f, Hash01(i, 6));
            layer.Alpha = Mathf.Lerp(0.62f, 0.88f, Hash01(i, 7));
            fireLayers[i] = layer;
        }
    }

    void CreateOrResetSmokeLayers(int sortingLayerId, int sortingOrder)
    {
        Sprite sprite = GetSmokeSprite();
        for (int i = 0; i < smokeLayers.Length; i++)
        {
            if (i >= activeSmokeLayerCount)
            {
                if (smokeLayers[i] != null && smokeLayers[i].Renderer != null)
                    smokeLayers[i].Renderer.gameObject.SetActive(false);
                continue;
            }

            float angle = (Mathf.PI * 2f * i) / Mathf.Max(1, activeSmokeLayerCount) + Hash01(i, 8) * 0.7f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            SpriteLayer layer = smokeLayers[i] ?? CreateSpriteLayer("MineSmokePuff" + i, sprite, sortingLayerId, sortingOrder + i);
            ResetSpriteLayer(layer, sprite, sortingLayerId, sortingOrder + i);
            layer.Direction = direction;
            layer.Distance = Mathf.Lerp(0.14f, 0.56f, Hash01(i, 9));
            layer.StartScale = Mathf.Lerp(0.42f, 0.68f, Hash01(i, 10));
            layer.EndScale = Mathf.Lerp(1.05f, 1.72f, Hash01(i, 11));
            layer.RotationSpeed = Mathf.Lerp(-18f, 18f, Hash01(i, 12));
            layer.Delay = Mathf.Lerp(0.06f, 0.22f, Hash01(i, 13));
            layer.Alpha = Mathf.Lerp(0.24f, 0.42f, Hash01(i, 14));
            smokeLayers[i] = layer;
        }
    }

    SpriteLayer CreateSpriteLayer(string objectName, Sprite sprite, int sortingLayerId, int sortingOrder)
    {
        GameObject layerObject = new GameObject(objectName);
        layerObject.transform.SetParent(transform, false);
        layerObject.transform.localPosition = Vector3.zero;
        layerObject.transform.localRotation = Quaternion.Euler(0f, 0f, Hash01(sortingOrder, 15) * 360f);

        SpriteRenderer renderer = layerObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.clear;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;

        return new SpriteLayer
        {
            Transform = layerObject.transform,
            Renderer = renderer
        };
    }

    void ResetSpriteLayer(SpriteLayer layer, Sprite sprite, int sortingLayerId, int sortingOrder)
    {
        if (layer == null || layer.Transform == null || layer.Renderer == null)
            return;

        layer.Renderer.gameObject.SetActive(true);
        layer.Transform.localPosition = Vector3.zero;
        layer.Transform.localScale = Vector3.one;
        layer.Transform.localRotation = Quaternion.Euler(0f, 0f, Hash01(sortingOrder, 15) * 360f);
        layer.Renderer.sprite = sprite;
        layer.Renderer.color = Color.clear;
        layer.Renderer.sortingLayerID = sortingLayerId;
        layer.Renderer.sortingOrder = sortingOrder;
    }

    void UpdateFire(float t)
    {
        float fireT = Mathf.Clamp01(t / 0.58f);
        float alpha = SmoothFade(0f, 0.58f, t);
        float diameter = blastRadius * 2f;

        for (int i = 0; i < activeFireLayerCount; i++)
        {
            SpriteLayer layer = fireLayers[i];
            if (layer == null || layer.Transform == null || layer.Renderer == null)
                continue;

            float localT = Mathf.Clamp01((t - layer.Delay) / 0.58f);
            float eased = 1f - Mathf.Pow(1f - localT, 3f);
            float scale = diameter * Mathf.Lerp(layer.StartScale, layer.EndScale, eased);
            Vector2 offset = layer.Direction * blastRadius * layer.Distance * eased;

            layer.Transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            layer.Transform.localScale = new Vector3(scale, scale, 1f);
            layer.Transform.localRotation = Quaternion.Euler(0f, 0f, layer.RotationSpeed * age);

            Color tint = Color.Lerp(hotCore, deepFire, Mathf.Clamp01(localT * 1.3f));
            tint.a = alpha * layer.Alpha * (1f - localT * 0.3f);
            layer.Renderer.color = tint;
        }
    }

    void UpdateSmoke(float t)
    {
        float smokeT = Mathf.Clamp01((t - 0.05f) / 0.95f);
        float alpha = Mathf.Sin(smokeT * Mathf.PI);
        float diameter = blastRadius * 2f;

        for (int i = 0; i < activeSmokeLayerCount; i++)
        {
            SpriteLayer layer = smokeLayers[i];
            if (layer == null || layer.Transform == null || layer.Renderer == null)
                continue;

            float localT = Mathf.Clamp01((t - layer.Delay) / 1.05f);
            float eased = 1f - Mathf.Pow(1f - localT, 2f);
            float scale = diameter * Mathf.Lerp(layer.StartScale, layer.EndScale, eased);
            Vector2 offset = layer.Direction * blastRadius * layer.Distance * eased;

            layer.Transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            layer.Transform.localScale = new Vector3(scale, scale, 1f);
            layer.Transform.localRotation = Quaternion.Euler(0f, 0f, layer.RotationSpeed * age);

            Color tint = Color.Lerp(new Color(0.45f, 0.36f, 0.28f, 1f), smokeColor, localT);
            tint.a = alpha * layer.Alpha * (1f - localT * 0.25f);
            layer.Renderer.color = tint;
        }
    }

    void UpdateShockwave(float t)
    {
        float shockT = Mathf.Clamp01(t / 0.72f);
        float shockAlpha = Mathf.Pow(1f - shockT, 1.7f) * 0.62f;
        float radius = Mathf.Lerp(blastRadius * 0.18f, blastRadius * 1.15f, 1f - Mathf.Pow(1f - shockT, 3f));
        UpdateCircle(shockwave, radius, shockAlpha, Mathf.Lerp(0.14f, 0.025f, shockT), new Color(1f, 0.72f, 0.38f, 1f));

        float heatT = Mathf.Clamp01(t / 0.48f);
        float heatAlpha = Mathf.Pow(1f - heatT, 2f) * 0.36f;
        float heatRadius = Mathf.Lerp(blastRadius * 0.08f, blastRadius * 0.72f, heatT);
        UpdateCircle(heatRing, heatRadius, heatAlpha, Mathf.Lerp(0.09f, 0.018f, heatT), new Color(1f, 0.93f, 0.72f, 1f));
    }

    void UpdateSparks(float t)
    {
        if (sparks == null)
            return;

        float sparkT = Mathf.Clamp01(t / 0.72f);
        float alpha = Mathf.Pow(1f - sparkT, 1.4f);
        for (int i = 0; i < activeSparkCount; i++)
        {
            LineRenderer spark = sparks[i];
            if (spark == null)
                continue;

            float angle = ((Mathf.PI * 2f) / Mathf.Max(1, activeSparkCount)) * i + Hash01(i, 16) * 0.4f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            float distance = blastRadius * Mathf.Lerp(0.25f, 1.15f + Hash01(i, 17) * 0.35f, sparkT);
            float length = blastRadius * Mathf.Lerp(0.1f, 0.24f, Hash01(i, 18));
            Vector3 head = center + direction * distance;
            Vector3 tail = head - direction * length * (1f - sparkT * 0.35f);

            spark.SetPosition(0, tail);
            spark.SetPosition(1, head);

            Color start = Color.Lerp(hotCore, amber, sparkT);
            Color end = amber;
            start.a = alpha * 0.85f;
            end.a = alpha * 0.08f;
            spark.startColor = start;
            spark.endColor = end;

            float width = Mathf.Lerp(0.075f, 0.018f, sparkT);
            spark.widthMultiplier = width;
            spark.startWidth = width;
            spark.endWidth = width * 0.2f;
        }
    }

    void UpdateCircle(LineRenderer ring, float radius, float alpha, float width, Color color)
    {
        if (ring == null)
            return;

        Vector3[] unitPoints = GetCircleUnitPoints();
        for (int i = 0; i < activeCircleSegments; i++)
        {
            Vector3 point = unitPoints[i];
            ring.SetPosition(i, center + new Vector3(point.x * radius, point.y * radius, 0f));
        }

        color.a = alpha;
        ring.startColor = color;
        ring.endColor = color;
        ring.widthMultiplier = width;
        ring.startWidth = width;
        ring.endWidth = width;
    }

    LineRenderer CreateOrResetLine(LineRenderer line, string objectName, float width, Color color, int sortingLayerId, int sortingOrder, bool loop = false, int positionCount = 2)
    {
        if (line == null)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(transform, false);

            line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.material = GetLineMaterial();
        }

        line.gameObject.SetActive(true);
        line.loop = loop;
        line.positionCount = Mathf.Max(2, positionCount);
        line.widthMultiplier = width;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 3;
        line.material = GetLineMaterial();
        line.startColor = color;
        line.endColor = color;
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
            name = "SpaceMineExplosionVfxLineMaterial",
            color = Color.white
        };
        lineMaterial.renderQueue = 5000;
        return lineMaterial;
    }

    static int ResolveForegroundSortingLayerId()
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

    static float SmoothFade(float start, float end, float t)
    {
        float normalized = Mathf.Clamp01((t - start) / Mathf.Max(0.001f, end - start));
        return Mathf.Pow(1f - normalized, 1.6f);
    }

    static float Hash01(int index, int salt)
    {
        return Mathf.Repeat(Mathf.Sin((index * 127.1f) + (salt * 311.7f)) * 43758.5453f, 1f);
    }

    static Vector3[] GetCircleUnitPoints()
    {
        if (circleUnitPoints != null)
            return circleUnitPoints;

        circleUnitPoints = new Vector3[CircleSegments];
        for (int i = 0; i < circleUnitPoints.Length; i++)
        {
            float angle = (Mathf.PI * 2f * i) / circleUnitPoints.Length;
            circleUnitPoints[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }

        return circleUnitPoints;
    }

    static Sprite GetFireSprite()
    {
        if (fireSprite != null)
            return fireSprite;

        Texture2D texture = CreateRadialTexture("SpaceMineRealisticFireSprite", true);
        fireSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        fireSprite.name = "SpaceMineRealisticFireSprite";
        return fireSprite;
    }

    static Sprite GetSmokeSprite()
    {
        if (smokeSprite != null)
            return smokeSprite;

        Texture2D texture = CreateRadialTexture("SpaceMineRealisticSmokeSprite", false);
        smokeSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        smokeSprite.name = "SpaceMineRealisticSmokeSprite";
        return smokeSprite;
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }

    static Texture2D CreateRadialTexture(string textureName, bool fire)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[TextureSize * TextureSize];
        float centerPixel = (TextureSize - 1) * 0.5f;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float dx = (x - centerPixel) / centerPixel;
                float dy = (y - centerPixel) / centerPixel;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float noise = Mathf.Sin(angle * 5.7f + distance * 9.4f) * 0.09f
                    + Mathf.Sin(angle * 11.3f - distance * 13.1f) * 0.05f
                    + Mathf.Sin((dx * 17.7f) + (dy * 9.2f)) * 0.035f;
                float shapedDistance = distance + noise;
                Color pixel = Color.clear;

                if (shapedDistance <= 1f)
                {
                    if (fire)
                    {
                        float core = Mathf.Clamp01(1f - shapedDistance / 0.28f);
                        float body = Mathf.Clamp01(1f - Mathf.Abs(shapedDistance - 0.48f) / 0.4f);
                        float edge = Mathf.Clamp01((1f - shapedDistance) / 0.28f);
                        float alpha = Mathf.Max(core, body * 0.85f) * edge;
                        Color inner = new Color(1f, 0.92f, 0.58f, alpha);
                        Color mid = new Color(1f, 0.48f, 0.09f, alpha);
                        Color outer = new Color(0.55f, 0.11f, 0.03f, alpha);
                        pixel = shapedDistance < 0.34f
                            ? Color.Lerp(inner, mid, Mathf.Clamp01(shapedDistance / 0.34f))
                            : Color.Lerp(mid, outer, Mathf.Clamp01((shapedDistance - 0.34f) / 0.66f));
                        pixel.a = alpha;
                    }
                    else
                    {
                        float body = Mathf.Clamp01(1f - Mathf.Abs(shapedDistance - 0.42f) / 0.5f);
                        float softEdge = Mathf.Clamp01((1f - shapedDistance) / 0.32f);
                        float centerFade = Mathf.Clamp01(shapedDistance / 0.18f);
                        float alpha = body * softEdge * centerFade;
                        pixel = new Color(1f, 1f, 1f, alpha * 0.72f);
                    }
                }

                pixels[(y * TextureSize) + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }
}

public sealed class SpaceBombExplosionVfx : MonoBehaviour
{
    const float Lifetime = 1.28f;
    const int RingCount = 2;
    const int RayCount = 18;
    const int CircleSegments = 72;
    const float EffectZ = -0.44f;
    const int PoolWarmCount = 3;

    static Material sharedMaterial;
    static readonly Stack<SpaceBombExplosionVfx> Pool = new Stack<SpaceBombExplosionVfx>(PoolWarmCount);
    static Vector3[] circleUnitPoints;

    readonly LineRenderer[] rings = new LineRenderer[RingCount];
    readonly LineRenderer[] rays = new LineRenderer[RayCount];
    Vector3 center;
    float radius;
    float age;

    public static void Prewarm()
    {
        GetMaterial();
        WarmPool(PoolWarmCount);
    }

    public static void Spawn(Vector3 position, float blastRadius)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        ScreenShakeController.Request(ScreenShakeProfiles.SpaceBombExplosion, position);
        SpaceMineExplosionVfx.Spawn(position, blastRadius, null, true);

        SpaceBombExplosionVfx vfx = GetFromPool();
        vfx.Initialize(position, blastRadius);
    }

    static void WarmPool(int count)
    {
        for (int i = Pool.Count; i < count; i++)
        {
            SpaceBombExplosionVfx vfx = CreateInstance();
            vfx.Initialize(Vector3.zero, 3.5f);
            vfx.ReturnToPool();
        }
    }

    static SpaceBombExplosionVfx GetFromPool()
    {
        while (Pool.Count > 0)
        {
            SpaceBombExplosionVfx pooled = Pool.Pop();
            if (pooled != null)
            {
                pooled.gameObject.SetActive(true);
                return pooled;
            }
        }

        SpaceBombExplosionVfx created = CreateInstance();
        created.gameObject.SetActive(true);
        return created;
    }

    static SpaceBombExplosionVfx CreateInstance()
    {
        GameObject effect = new GameObject("SpaceBombExplosionVfx");
        effect.SetActive(false);
        return effect.AddComponent<SpaceBombExplosionVfx>();
    }

    void Initialize(Vector3 position, float blastRadius)
    {
        center = new Vector3(position.x, position.y, EffectZ);
        transform.position = center;
        radius = Mathf.Clamp(blastRadius, 3.5f, 8f);
        age = 0f;

        for (int i = 0; i < rings.Length; i++)
            rings[i] = CreateOrResetLine(rings[i], "SpaceBombShockRing" + i, 0.12f, true, CircleSegments, 6800 + i);

        for (int i = 0; i < rays.Length; i++)
            rays[i] = CreateOrResetLine(rays[i], "SpaceBombBlastRay" + i, 0.065f, false, 2, 6820 + i);

        UpdateVisuals(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Lifetime);
        UpdateVisuals(t);

        if (age >= Lifetime)
            ReturnToPool();
    }

    void ReturnToPool()
    {
        gameObject.SetActive(false);
        Pool.Push(this);
    }

    void UpdateVisuals(float t)
    {
        for (int i = 0; i < rings.Length; i++)
        {
            LineRenderer ring = rings[i];
            if (ring == null)
                continue;

            float delay = i * 0.09f;
            float localT = Mathf.Clamp01((t - delay) / Mathf.Max(0.001f, 1f - delay));
            float eased = 1f - Mathf.Pow(1f - localT, 3f);
            float ringRadius = Mathf.Lerp(radius * (0.18f + i * 0.08f), radius * (1.38f + i * 0.12f), eased);
            float alpha = Mathf.Pow(1f - localT, 1.65f) * Mathf.Lerp(0.72f, 0.38f, i / (float)rings.Length);
            float width = Mathf.Lerp(0.18f, 0.018f, localT) * Mathf.Lerp(1f, 0.72f, i / (float)rings.Length);

            Vector3[] unitPoints = GetCircleUnitPoints();
            for (int point = 0; point < CircleSegments; point++)
            {
                Vector3 unitPoint = unitPoints[point];
                float angle = (Mathf.PI * 2f * point) / CircleSegments;
                float wobble = Mathf.Sin(angle * 9f + i * 1.7f + age * 4f) * radius * 0.012f;
                ring.SetPosition(point, center + new Vector3(unitPoint.x * (ringRadius + wobble), unitPoint.y * (ringRadius + wobble), 0f));
            }

            Color color = Color.Lerp(new Color(1f, 0.9f, 0.42f, 1f), new Color(1f, 0.28f, 0.04f, 1f), localT);
            color.a = alpha;
            ring.startColor = color;
            ring.endColor = color;
            ring.widthMultiplier = width;
            ring.startWidth = width;
            ring.endWidth = width;
        }

        float rayT = Mathf.Clamp01(t / 0.72f);
        float rayAlpha = Mathf.Pow(1f - rayT, 1.4f);
        for (int i = 0; i < rays.Length; i++)
        {
            LineRenderer ray = rays[i];
            if (ray == null)
                continue;

            float angle = ((Mathf.PI * 2f) / rays.Length) * i + Hash01(i, 4) * 0.18f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            float startDistance = radius * Mathf.Lerp(0.08f, 0.32f, Hash01(i, 8)) * rayT;
            float endDistance = radius * Mathf.Lerp(0.82f, 1.42f, Hash01(i, 12)) * (1f - Mathf.Pow(1f - rayT, 2.3f));
            Vector3 start = center + direction * startDistance;
            Vector3 end = center + direction * endDistance;
            ray.SetPosition(0, start);
            ray.SetPosition(1, end);

            Color startColor = new Color(1f, 0.92f, 0.44f, rayAlpha * 0.82f);
            Color endColor = new Color(1f, 0.2f, 0.02f, 0f);
            ray.startColor = startColor;
            ray.endColor = endColor;
            ray.widthMultiplier = Mathf.Lerp(0.11f, 0.018f, rayT) * Mathf.Lerp(0.72f, 1.2f, Hash01(i, 16));
        }
    }

    LineRenderer CreateOrResetLine(LineRenderer line, string objectName, float width, bool loop, int positionCount, int sortingOrder)
    {
        if (line == null)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(transform, false);

            line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.material = GetMaterial();
        }

        line.gameObject.SetActive(true);
        line.loop = loop;
        line.positionCount = Mathf.Max(2, positionCount);
        line.widthMultiplier = width;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 5;
        line.numCornerVertices = 4;
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
            name = "SpaceBombExplosionVfxLineMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 5000;
        return sharedMaterial;
    }

    static float Hash01(int index, int salt)
    {
        return Mathf.Repeat(Mathf.Sin((index * 127.1f) + (salt * 311.7f)) * 43758.5453f, 1f);
    }

    static Vector3[] GetCircleUnitPoints()
    {
        if (circleUnitPoints != null)
            return circleUnitPoints;

        circleUnitPoints = new Vector3[CircleSegments];
        for (int i = 0; i < circleUnitPoints.Length; i++)
        {
            float angle = (Mathf.PI * 2f * i) / circleUnitPoints.Length;
            circleUnitPoints[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }

        return circleUnitPoints;
    }
}
