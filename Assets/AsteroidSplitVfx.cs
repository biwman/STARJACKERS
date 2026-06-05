using UnityEngine;

public sealed class AsteroidSplitVfx : MonoBehaviour
{
    const float MinLifetime = 0.9f;
    const float MaxLifetime = 1.32f;
    const float EffectZ = -0.32f;
    const int DustCount = 8;
    const int ShardCount = 14;
    const int TextureSize = 96;

    sealed class DustLayer
    {
        public Transform Transform;
        public SpriteRenderer Renderer;
        public Vector2 Direction;
        public float Distance;
        public float StartScale;
        public float EndScale;
        public float RotationSpeed;
        public float Delay;
    }

    sealed class Shard
    {
        public LineRenderer Line;
        public Vector3 Direction;
        public float Speed;
        public float Length;
        public float Delay;
        public Color Color;
    }

    static Sprite dustSprite;
    static Material lineMaterial;

    readonly DustLayer[] dustLayers = new DustLayer[DustCount];
    readonly Shard[] shards = new Shard[ShardCount];

    Vector3 center;
    float radius;
    float lifetime;
    float age;

    readonly Color warmDust = new Color(0.48f, 0.39f, 0.29f, 1f);
    readonly Color greyDust = new Color(0.36f, 0.35f, 0.32f, 1f);
    readonly Color shardBrown = new Color(0.58f, 0.43f, 0.3f, 1f);
    readonly Color shardGrey = new Color(0.52f, 0.5f, 0.46f, 1f);

    public static void Prewarm()
    {
        PrewarmSpriteTexture(GetDustSprite());
        GetLineMaterial();
    }

    public static void Spawn(Vector3 position, float sourceRadius, SpriteRenderer referenceRenderer = null)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject effect = new GameObject("AsteroidSplitVfx");
        AsteroidSplitVfx vfx = effect.AddComponent<AsteroidSplitVfx>();
        vfx.Initialize(position, sourceRadius, referenceRenderer);
    }

    void Initialize(Vector3 position, float sourceRadius, SpriteRenderer referenceRenderer)
    {
        center = new Vector3(position.x, position.y, EffectZ);
        transform.position = center;
        radius = Mathf.Clamp(sourceRadius, 0.28f, 5.2f);
        lifetime = Mathf.Lerp(MinLifetime, MaxLifetime, Mathf.InverseLerp(0.45f, 3.4f, radius));

        int sortingLayerId = referenceRenderer != null ? referenceRenderer.sortingLayerID : ResolveForegroundSortingLayerId();
        int sortingOrder = referenceRenderer != null ? referenceRenderer.sortingOrder + 260 : 6200;

        CreateDustLayers(sortingLayerId, sortingOrder);
        CreateShards(sortingLayerId, sortingOrder + 18);
        UpdateVisuals(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        UpdateVisuals(t);

        if (age >= lifetime)
            Destroy(gameObject);
    }

    void CreateDustLayers(int sortingLayerId, int sortingOrder)
    {
        Sprite sprite = GetDustSprite();
        for (int i = 0; i < dustLayers.Length; i++)
        {
            float angle = (Mathf.PI * 2f * i) / dustLayers.Length + Hash01(i, 1) * 0.82f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject dustObject = new GameObject("AsteroidSplitDust" + i);
            dustObject.transform.SetParent(transform, false);
            dustObject.transform.localPosition = Vector3.zero;
            dustObject.transform.localRotation = Quaternion.Euler(0f, 0f, Hash01(i, 2) * 360f);

            SpriteRenderer renderer = dustObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.clear;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder + i;

            dustLayers[i] = new DustLayer
            {
                Transform = dustObject.transform,
                Renderer = renderer,
                Direction = direction,
                Distance = Mathf.Lerp(0.16f, 0.62f, Hash01(i, 3)),
                StartScale = Mathf.Lerp(0.18f, 0.36f, Hash01(i, 4)),
                EndScale = Mathf.Lerp(0.52f, 0.94f, Hash01(i, 5)),
                RotationSpeed = Mathf.Lerp(-52f, 52f, Hash01(i, 6)),
                Delay = Mathf.Lerp(0f, 0.11f, Hash01(i, 7))
            };
        }
    }

    void CreateShards(int sortingLayerId, int sortingOrder)
    {
        for (int i = 0; i < shards.Length; i++)
        {
            float angle = (Mathf.PI * 2f * i) / shards.Length + Hash01(i, 8) * 0.55f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            GameObject shardObject = new GameObject("AsteroidSplitShard" + i);
            shardObject.transform.SetParent(transform, false);

            LineRenderer line = shardObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.material = GetLineMaterial();
            line.sortingLayerID = sortingLayerId;
            line.sortingOrder = sortingOrder + i;

            shards[i] = new Shard
            {
                Line = line,
                Direction = direction,
                Speed = radius * Mathf.Lerp(1.7f, 3.9f, Hash01(i, 9)),
                Length = radius * Mathf.Lerp(0.08f, 0.2f, Hash01(i, 10)),
                Delay = Mathf.Lerp(0f, 0.07f, Hash01(i, 11)),
                Color = Color.Lerp(shardBrown, shardGrey, Hash01(i, 12))
            };
        }
    }

    void UpdateVisuals(float t)
    {
        UpdateDust(t);
        UpdateShards(t);
    }

    void UpdateDust(float t)
    {
        for (int i = 0; i < dustLayers.Length; i++)
        {
            DustLayer layer = dustLayers[i];
            if (layer == null || layer.Transform == null || layer.Renderer == null)
                continue;

            float localT = Mathf.Clamp01((t - layer.Delay) / 0.82f);
            float eased = 1f - Mathf.Pow(1f - localT, 2.2f);
            float scale = radius * Mathf.Lerp(layer.StartScale, layer.EndScale, eased);
            Vector2 offset = layer.Direction * radius * layer.Distance * eased;

            layer.Transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            layer.Transform.localScale = new Vector3(scale, scale, 1f);
            layer.Transform.localRotation = Quaternion.Euler(0f, 0f, layer.RotationSpeed * age);

            Color tint = Color.Lerp(warmDust, greyDust, Mathf.Clamp01(localT * 1.3f + Hash01(i, 13) * 0.22f));
            tint.a = Mathf.Sin(localT * Mathf.PI) * Mathf.Lerp(0.26f, 0.48f, Hash01(i, 14));
            layer.Renderer.color = tint;
        }
    }

    void UpdateShards(float t)
    {
        for (int i = 0; i < shards.Length; i++)
        {
            Shard shard = shards[i];
            if (shard == null || shard.Line == null)
                continue;

            float localT = Mathf.Clamp01((t - shard.Delay) / 0.72f);
            float eased = 1f - Mathf.Pow(1f - localT, 2.4f);
            Vector3 head = center + shard.Direction * shard.Speed * eased;
            Vector3 tail = head - shard.Direction * shard.Length * (1f - localT * 0.35f);

            shard.Line.SetPosition(0, tail);
            shard.Line.SetPosition(1, head);

            Color start = shard.Color;
            Color end = greyDust;
            start.a = Mathf.Pow(1f - localT, 1.4f) * 0.88f;
            end.a = Mathf.Pow(1f - localT, 1.8f) * 0.08f;
            shard.Line.startColor = start;
            shard.Line.endColor = end;

            float width = Mathf.Lerp(radius * 0.035f, radius * 0.012f, localT);
            shard.Line.widthMultiplier = width;
            shard.Line.startWidth = width;
            shard.Line.endWidth = width * 0.32f;
        }
    }

    static Sprite GetDustSprite()
    {
        if (dustSprite != null)
            return dustSprite;

        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "AsteroidSplitDustTexture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 centerPoint = new Vector2((TextureSize - 1) * 0.5f, (TextureSize - 1) * 0.5f);
        float radius = TextureSize * 0.43f;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                Vector2 p = new Vector2(x, y);
                float distance = Vector2.Distance(p, centerPoint) / radius;
                float uneven = Mathf.PerlinNoise(x * 0.085f + 11.3f, y * 0.085f + 37.1f);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = Mathf.Pow(alpha, 1.55f) * Mathf.Lerp(0.45f, 1f, uneven);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        dustSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        dustSprite.name = "AsteroidSplitDustSprite";
        return dustSprite;
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
            name = "AsteroidSplitVfxLineMaterial",
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

    static float Hash01(int index, int salt)
    {
        return Mathf.Repeat(Mathf.Sin((index * 127.1f) + (salt * 311.7f)) * 43758.5453f, 1f);
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }
}
