using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyDeathBoomVfx : MonoBehaviour
{
    const float Lifetime = 1.18f;
    const float EffectZ = -0.34f;
    const int TextureSize = 160;
    const int RingSegments = 64;
    const int FireLayerCount = 4;
    const int SmokeLayerCount = 5;
    const int SparkCount = 12;
    const int DebrisCount = 6;
    const int PoolWarmCount = 5;

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

    struct Streak
    {
        public LineRenderer Line;
        public Vector3 Direction;
        public float Speed;
        public float Length;
        public float Width;
        public float LateralDrift;
        public Color StartColor;
        public Color EndColor;
    }

    static Material sharedMaterial;
    static Sprite coreSprite;
    static Sprite fireSprite;
    static Sprite smokeSprite;
    static readonly Stack<EnemyDeathBoomVfx> Pool = new Stack<EnemyDeathBoomVfx>(PoolWarmCount);
    static Vector3[] ringUnitPoints;

    readonly SpriteLayer[] fireLayers = new SpriteLayer[FireLayerCount];
    readonly SpriteLayer[] smokeLayers = new SpriteLayer[SmokeLayerCount];
    readonly Streak[] sparks = new Streak[SparkCount];
    readonly Streak[] debris = new Streak[DebrisCount];

    SpriteRenderer coreRenderer;
    LineRenderer pressureRing;
    LineRenderer heatRing;
    Vector3 center;
    float radius;
    float age;

    readonly Color hotWhite = new Color(1f, 0.94f, 0.62f, 1f);
    readonly Color ember = new Color(1f, 0.45f, 0.08f, 1f);
    readonly Color deepFlame = new Color(0.68f, 0.12f, 0.025f, 1f);
    readonly Color soot = new Color(0.22f, 0.21f, 0.2f, 1f);

    public static void Prewarm()
    {
        GetMaterial();
        PrewarmSpriteTexture(GetCoreSprite());
        PrewarmSpriteTexture(GetFireSprite());
        PrewarmSpriteTexture(GetSmokeSprite());
        WarmPool(PoolWarmCount);
    }

    public static void Spawn(Vector3 position, SpriteRenderer referenceRenderer, float visualTargetSize)
    {
        if (!RoomSettings.AreVisualEffectsEnabled() || !RoomSettings.AreBoomVfxEnabled())
            return;

        EnemyDeathBoomVfx vfx = GetFromPool();
        vfx.Initialize(position, referenceRenderer, visualTargetSize);
    }

    static void WarmPool(int count)
    {
        for (int i = Pool.Count; i < count; i++)
        {
            EnemyDeathBoomVfx vfx = CreateInstance();
            vfx.Initialize(Vector3.zero, null, GameVisualTheme.PlayerTargetSize, false);
            vfx.ReturnToPool();
        }
    }

    static EnemyDeathBoomVfx GetFromPool()
    {
        while (Pool.Count > 0)
        {
            EnemyDeathBoomVfx pooled = Pool.Pop();
            if (pooled != null)
            {
                pooled.gameObject.SetActive(true);
                return pooled;
            }
        }

        EnemyDeathBoomVfx created = CreateInstance();
        created.gameObject.SetActive(true);
        return created;
    }

    static EnemyDeathBoomVfx CreateInstance()
    {
        GameObject effect = new GameObject("EnemyDeathBoomVfx");
        effect.SetActive(false);
        return effect.AddComponent<EnemyDeathBoomVfx>();
    }

    void Initialize(Vector3 position, SpriteRenderer referenceRenderer, float visualTargetSize, bool playScreenShake = true)
    {
        center = new Vector3(position.x, position.y, EffectZ);
        transform.position = center;
        age = 0f;

        float rendererRadius = referenceRenderer != null
            ? Mathf.Max(referenceRenderer.bounds.extents.x, referenceRenderer.bounds.extents.y)
            : 0f;
        float targetRadius = Mathf.Max(0.2f, visualTargetSize * 0.54f);
        radius = Mathf.Clamp(Mathf.Max(rendererRadius, targetRadius), 0.48f, 2.65f);

        int sortingLayerId = referenceRenderer != null
            ? referenceRenderer.sortingLayerID
            : SortingLayer.NameToID(GameVisualTheme.WorldSortingLayerName);
        int sortingOrder = referenceRenderer != null
            ? referenceRenderer.sortingOrder + 48
            : GameVisualTheme.EnemySortingOrder + 48;

        CreateOrResetCore(sortingLayerId, sortingOrder + 8);
        CreateOrResetFireLayers(sortingLayerId, sortingOrder + 3);
        CreateOrResetSmokeLayers(sortingLayerId, sortingOrder + 1);
        pressureRing = CreateOrResetLine(pressureRing, "BoomPressureRing", 0.12f, sortingLayerId, sortingOrder + 12, true, RingSegments);
        heatRing = CreateOrResetLine(heatRing, "BoomHeatRing", 0.08f, sortingLayerId, sortingOrder + 13, true, RingSegments);
        CreateOrResetStreaks(sparks, "BoomSpark", sortingLayerId, sortingOrder + 16, true);
        CreateOrResetStreaks(debris, "BoomDebris", sortingLayerId, sortingOrder + 10, false);
        if (playScreenShake)
            ScreenShakeController.Request(ScreenShakeProfiles.EnemyBoom, center);
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

    void CreateOrResetCore(int sortingLayerId, int sortingOrder)
    {
        if (coreRenderer == null)
        {
            GameObject coreObject = new GameObject("BoomHotCore");
            coreObject.transform.SetParent(transform, false);
            coreRenderer = coreObject.AddComponent<SpriteRenderer>();
        }

        coreRenderer.gameObject.SetActive(true);
        coreRenderer.sprite = GetCoreSprite();
        coreRenderer.material = GetMaterial();
        coreRenderer.sortingLayerID = sortingLayerId;
        coreRenderer.sortingOrder = sortingOrder;
        coreRenderer.color = Color.clear;
        coreRenderer.transform.localPosition = Vector3.zero;
        coreRenderer.transform.localScale = Vector3.one;
    }

    void CreateOrResetFireLayers(int sortingLayerId, int sortingOrder)
    {
        Sprite sprite = GetFireSprite();
        for (int i = 0; i < fireLayers.Length; i++)
        {
            float angle = ((Mathf.PI * 2f) / fireLayers.Length) * i + Hash01(i, 2) * 0.72f;
            SpriteLayer layer = fireLayers[i] ?? CreateSpriteLayer("BoomFireBloom" + i, sprite, sortingLayerId, sortingOrder + i);
            ResetSpriteLayer(layer, sprite, sortingLayerId, sortingOrder + i);
            layer.Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            layer.Distance = Mathf.Lerp(0.02f, 0.24f, Hash01(i, 3));
            layer.StartScale = Mathf.Lerp(0.64f, 0.88f, Hash01(i, 4));
            layer.EndScale = Mathf.Lerp(1.36f, 1.92f, Hash01(i, 5));
            layer.RotationSpeed = Mathf.Lerp(-54f, 54f, Hash01(i, 6));
            layer.Delay = Mathf.Lerp(0f, 0.09f, Hash01(i, 7));
            layer.Alpha = Mathf.Lerp(0.58f, 0.88f, Hash01(i, 8));
            fireLayers[i] = layer;
        }
    }

    void CreateOrResetSmokeLayers(int sortingLayerId, int sortingOrder)
    {
        Sprite sprite = GetSmokeSprite();
        for (int i = 0; i < smokeLayers.Length; i++)
        {
            float angle = ((Mathf.PI * 2f) / smokeLayers.Length) * i + Hash01(i, 11) * 0.9f;
            SpriteLayer layer = smokeLayers[i] ?? CreateSpriteLayer("BoomSmokePuff" + i, sprite, sortingLayerId, sortingOrder + i);
            ResetSpriteLayer(layer, sprite, sortingLayerId, sortingOrder + i);
            layer.Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            layer.Distance = Mathf.Lerp(0.18f, 0.72f, Hash01(i, 12));
            layer.StartScale = Mathf.Lerp(0.36f, 0.62f, Hash01(i, 13));
            layer.EndScale = Mathf.Lerp(1.18f, 2.15f, Hash01(i, 14));
            layer.RotationSpeed = Mathf.Lerp(-22f, 22f, Hash01(i, 15));
            layer.Delay = Mathf.Lerp(0.05f, 0.22f, Hash01(i, 16));
            layer.Alpha = Mathf.Lerp(0.2f, 0.42f, Hash01(i, 17));
            smokeLayers[i] = layer;
        }
    }

    SpriteLayer CreateSpriteLayer(string objectName, Sprite sprite, int sortingLayerId, int sortingOrder)
    {
        GameObject layerObject = new GameObject(objectName);
        layerObject.transform.SetParent(transform, false);
        layerObject.transform.localRotation = Quaternion.Euler(0f, 0f, Hash01(sortingOrder, 19) * 360f);

        SpriteRenderer renderer = layerObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.material = GetMaterial();
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.clear;

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
        layer.Transform.localRotation = Quaternion.Euler(0f, 0f, Hash01(sortingOrder, 19) * 360f);
        layer.Renderer.sprite = sprite;
        layer.Renderer.material = GetMaterial();
        layer.Renderer.sortingLayerID = sortingLayerId;
        layer.Renderer.sortingOrder = sortingOrder;
        layer.Renderer.color = Color.clear;
    }

    void CreateOrResetStreaks(Streak[] streaks, string objectPrefix, int sortingLayerId, int sortingOrder, bool spark)
    {
        for (int i = 0; i < streaks.Length; i++)
        {
            float angle = ((Mathf.PI * 2f) / streaks.Length) * i + Hash01(i, spark ? 23 : 31) * 0.42f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            LineRenderer line = CreateOrResetLine(streaks[i].Line, objectPrefix + i, spark ? 0.04f : 0.058f, sortingLayerId, sortingOrder + i, false, 2);
            streaks[i] = new Streak
            {
                Line = line,
                Direction = direction,
                Speed = radius * Mathf.Lerp(spark ? 2.8f : 1.35f, spark ? 5.5f : 2.55f, Hash01(i, spark ? 24 : 32)),
                Length = radius * Mathf.Lerp(spark ? 0.28f : 0.18f, spark ? 0.62f : 0.38f, Hash01(i, spark ? 25 : 33)),
                Width = Mathf.Lerp(spark ? 0.028f : 0.045f, spark ? 0.065f : 0.092f, Hash01(i, spark ? 26 : 34)),
                LateralDrift = Mathf.Lerp(-0.32f, 0.32f, Hash01(i, spark ? 27 : 35)) * radius,
                StartColor = spark ? hotWhite : new Color(0.72f, 0.48f, 0.3f, 1f),
                EndColor = spark ? ember : new Color(0.16f, 0.14f, 0.12f, 1f)
            };
        }
    }

    LineRenderer CreateOrResetLine(LineRenderer line, string objectName, float width, int sortingLayerId, int sortingOrder, bool loop, int positionCount)
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
        line.numCapVertices = 4;
        line.numCornerVertices = 3;
        line.material = GetMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
    }

    void UpdateVisuals(float t)
    {
        UpdateCore(t);
        UpdateFire(t);
        UpdateSmoke(t);
        UpdateRings(t);
        UpdateStreaks(sparks, t, true);
        UpdateStreaks(debris, t, false);
    }

    void UpdateCore(float t)
    {
        if (coreRenderer == null)
            return;

        float coreT = Mathf.Clamp01(t / 0.32f);
        float scale = radius * Mathf.Lerp(0.34f, 1.58f, 1f - Mathf.Pow(1f - coreT, 3f));
        coreRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        coreRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, age * 38f);
        Color color = Color.Lerp(Color.white, hotWhite, coreT);
        color.a = Mathf.Pow(1f - coreT, 2.1f) * 0.88f;
        coreRenderer.color = color;
    }

    void UpdateFire(float t)
    {
        for (int i = 0; i < fireLayers.Length; i++)
        {
            SpriteLayer layer = fireLayers[i];
            if (layer == null || layer.Transform == null || layer.Renderer == null)
                continue;

            float localT = Mathf.Clamp01((t - layer.Delay) / 0.5f);
            float eased = 1f - Mathf.Pow(1f - localT, 3f);
            float scale = radius * Mathf.Lerp(layer.StartScale, layer.EndScale, eased);
            Vector2 offset = layer.Direction * radius * layer.Distance * eased;
            layer.Transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            layer.Transform.localScale = new Vector3(scale, scale, 1f);
            layer.Transform.localRotation = Quaternion.Euler(0f, 0f, layer.RotationSpeed * age);

            Color tint = Color.Lerp(hotWhite, deepFlame, Mathf.Clamp01(localT * 1.25f));
            tint.a = Mathf.Pow(1f - localT, 1.55f) * layer.Alpha;
            layer.Renderer.color = tint;
        }
    }

    void UpdateSmoke(float t)
    {
        for (int i = 0; i < smokeLayers.Length; i++)
        {
            SpriteLayer layer = smokeLayers[i];
            if (layer == null || layer.Transform == null || layer.Renderer == null)
                continue;

            float localT = Mathf.Clamp01((t - layer.Delay) / 0.92f);
            float eased = 1f - Mathf.Pow(1f - localT, 2f);
            float scale = radius * Mathf.Lerp(layer.StartScale, layer.EndScale, eased);
            Vector2 offset = layer.Direction * radius * layer.Distance * eased;
            layer.Transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            layer.Transform.localScale = new Vector3(scale, scale, 1f);
            layer.Transform.localRotation = Quaternion.Euler(0f, 0f, layer.RotationSpeed * age);

            Color tint = Color.Lerp(new Color(0.56f, 0.44f, 0.31f, 1f), soot, Mathf.Clamp01(localT * 1.2f));
            tint.a = Mathf.Sin(localT * Mathf.PI) * layer.Alpha * Mathf.Pow(1f - Mathf.Max(0f, localT - 0.5f), 1.4f);
            layer.Renderer.color = tint;
        }
    }

    void UpdateRings(float t)
    {
        float pressureT = Mathf.Clamp01(t / 0.56f);
        float pressureRadius = Mathf.Lerp(radius * 0.18f, radius * 1.7f, 1f - Mathf.Pow(1f - pressureT, 3f));
        UpdateCircle(pressureRing, pressureRadius, Mathf.Pow(1f - pressureT, 1.65f) * 0.64f, Mathf.Lerp(0.14f, 0.018f, pressureT), ember);

        float heatT = Mathf.Clamp01(t / 0.38f);
        float heatRadius = Mathf.Lerp(radius * 0.08f, radius * 1.08f, heatT);
        UpdateCircle(heatRing, heatRadius, Mathf.Pow(1f - heatT, 2f) * 0.42f, Mathf.Lerp(0.09f, 0.014f, heatT), hotWhite);
    }

    void UpdateCircle(LineRenderer ring, float ringRadius, float alpha, float width, Color color)
    {
        if (ring == null)
            return;

        Vector3[] unitPoints = GetRingUnitPoints();
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = ((Mathf.PI * 2f) / RingSegments) * i;
            float wobble = Mathf.Sin(angle * 7f + age * 5.2f) * radius * 0.018f;
            Vector3 point = unitPoints[i];
            ring.SetPosition(i, center + new Vector3(point.x * (ringRadius + wobble), point.y * (ringRadius + wobble), 0f));
        }

        color.a = alpha;
        ring.startColor = color;
        ring.endColor = color;
        ring.widthMultiplier = width;
        ring.startWidth = width;
        ring.endWidth = width;
    }

    void UpdateStreaks(Streak[] streaks, float t, bool spark)
    {
        if (streaks == null)
            return;

        float streakT = Mathf.Clamp01(t / (spark ? 0.58f : 0.82f));
        float alpha = Mathf.Pow(1f - streakT, spark ? 1.35f : 1.9f);
        for (int i = 0; i < streaks.Length; i++)
        {
            LineRenderer line = streaks[i].Line;
            if (line == null)
                continue;

            Vector3 side = new Vector3(-streaks[i].Direction.y, streaks[i].Direction.x, 0f);
            Vector3 drift = side * streaks[i].LateralDrift * streakT * (1f - streakT);
            float distance = streaks[i].Speed * streakT * (1f - 0.24f * streakT);
            Vector3 head = center + streaks[i].Direction * distance + drift;
            Vector3 tail = head - streaks[i].Direction * streaks[i].Length * Mathf.Lerp(1f, 0.25f, streakT);

            line.SetPosition(0, tail);
            line.SetPosition(1, head);

            Color start = Color.Lerp(streaks[i].StartColor, streaks[i].EndColor, streakT);
            Color end = streaks[i].EndColor;
            start.a = alpha * (spark ? 0.9f : 0.5f);
            end.a = alpha * (spark ? 0.05f : 0.18f);
            line.startColor = start;
            line.endColor = end;
            line.widthMultiplier = Mathf.Lerp(streaks[i].Width, streaks[i].Width * 0.24f, streakT);
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
            name = "EnemyDeathBoomVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 5000;
        return sharedMaterial;
    }

    static Sprite GetCoreSprite()
    {
        if (coreSprite != null)
            return coreSprite;

        Texture2D texture = CreateRadialTexture("EnemyDeathBoomCoreSprite", TextureKind.Core);
        coreSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        coreSprite.name = "EnemyDeathBoomCoreSprite";
        return coreSprite;
    }

    static Sprite GetFireSprite()
    {
        if (fireSprite != null)
            return fireSprite;

        Texture2D texture = CreateRadialTexture("EnemyDeathBoomFireSprite", TextureKind.Fire);
        fireSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        fireSprite.name = "EnemyDeathBoomFireSprite";
        return fireSprite;
    }

    static Sprite GetSmokeSprite()
    {
        if (smokeSprite != null)
            return smokeSprite;

        Texture2D texture = CreateRadialTexture("EnemyDeathBoomSmokeSprite", TextureKind.Smoke);
        smokeSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        smokeSprite.name = "EnemyDeathBoomSmokeSprite";
        return smokeSprite;
    }

    static Texture2D CreateRadialTexture(string textureName, TextureKind kind)
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
                float noise = Mathf.Sin(angle * 5.3f + distance * 10.7f) * 0.08f
                    + Mathf.Sin(angle * 12.1f - distance * 14.6f) * 0.045f
                    + Mathf.Sin(dx * 18.4f + dy * 7.9f) * 0.03f;
                float shapedDistance = distance + (kind == TextureKind.Core ? noise * 0.25f : noise);
                Color pixel = Color.clear;

                if (shapedDistance <= 1f)
                {
                    switch (kind)
                    {
                        case TextureKind.Core:
                        {
                            float alpha = Mathf.Clamp01((1f - shapedDistance) / 0.82f);
                            alpha = Mathf.Pow(alpha, 1.65f);
                            pixel = new Color(1f, 1f, 1f, alpha);
                            break;
                        }
                        case TextureKind.Fire:
                        {
                            float core = Mathf.Clamp01(1f - shapedDistance / 0.24f);
                            float body = Mathf.Clamp01(1f - Mathf.Abs(shapedDistance - 0.44f) / 0.42f);
                            float edge = Mathf.Clamp01((1f - shapedDistance) / 0.3f);
                            float alpha = Mathf.Max(core, body * 0.88f) * edge;
                            pixel = shapedDistance < 0.3f
                                ? Color.Lerp(new Color(1f, 0.96f, 0.62f, alpha), new Color(1f, 0.48f, 0.08f, alpha), shapedDistance / 0.3f)
                                : Color.Lerp(new Color(1f, 0.48f, 0.08f, alpha), new Color(0.5f, 0.08f, 0.02f, alpha), Mathf.Clamp01((shapedDistance - 0.3f) / 0.7f));
                            pixel.a = alpha;
                            break;
                        }
                        case TextureKind.Smoke:
                        {
                            float body = Mathf.Clamp01(1f - Mathf.Abs(shapedDistance - 0.42f) / 0.52f);
                            float edge = Mathf.Clamp01((1f - shapedDistance) / 0.34f);
                            float centerFade = Mathf.Clamp01(shapedDistance / 0.16f);
                            float alpha = body * edge * centerFade * 0.74f;
                            pixel = new Color(1f, 1f, 1f, alpha);
                            break;
                        }
                    }
                }

                pixels[(y * TextureSize) + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }

    static float Hash01(int index, int salt)
    {
        return Mathf.Repeat(Mathf.Sin((index * 127.1f) + (salt * 311.7f)) * 43758.5453f, 1f);
    }

    static Vector3[] GetRingUnitPoints()
    {
        if (ringUnitPoints != null)
            return ringUnitPoints;

        ringUnitPoints = new Vector3[RingSegments];
        for (int i = 0; i < ringUnitPoints.Length; i++)
        {
            float angle = ((Mathf.PI * 2f) / ringUnitPoints.Length) * i;
            ringUnitPoints[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }

        return ringUnitPoints;
    }

    enum TextureKind
    {
        Core,
        Fire,
        Smoke
    }
}
