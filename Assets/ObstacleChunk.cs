using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ObstacleChunk : MonoBehaviour
{
    public readonly struct RuntimeState
    {
        public readonly string StableId;
        public readonly Vector2 Position;
        public readonly Vector2 Velocity;
        public readonly float Rotation;
        public readonly float AngularVelocity;
        public readonly float SizeFactor;
        public readonly int MaxHealth;
        public readonly int CurrentHealth;
        public readonly int SplitCount;
        public readonly int SpriteVariantIndex;

        public RuntimeState(
            string stableId,
            Vector2 position,
            Vector2 velocity,
            float rotation,
            float angularVelocity,
            float sizeFactor,
            int maxHealth,
            int currentHealth,
            int splitCount,
            int spriteVariantIndex)
        {
            StableId = stableId;
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            AngularVelocity = angularVelocity;
            SizeFactor = sizeFactor;
            MaxHealth = maxHealth;
            CurrentHealth = currentHealth;
            SplitCount = splitCount;
            SpriteVariantIndex = spriteVariantIndex;
        }
    }

    public const float DefaultTargetWorldSize = 3f;
    public const float MinimumSizeFactor = 0.01f;

    static readonly Dictionary<string, ObstacleChunk> ChunksByStableId = new Dictionary<string, ObstacleChunk>();

    string stableId;
    float sizeFactor = 1f;
    int maxHealth = RoomSettings.DefaultObstacleHp;
    int currentHealth = RoomSettings.DefaultObstacleHp;
    int splitCount;
    int spriteVariantIndex = -1;
    MovingSpaceObject movingObject;
    Rigidbody2D body;
    SpriteRenderer spriteRenderer;

    public string StableId => stableId;
    public float SizeFactor => sizeFactor;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public int SplitCount => splitCount;
    public int SpriteVariantIndex => spriteVariantIndex;
    public bool CanSplit => maxHealth > 1 && splitCount < MapInstanceService.GetObstacleMaxSplitCountForPosition(transform.position) && sizeFactor > (MinimumSizeFactor * 2f) + 0.0001f;

    public static ObstacleChunk Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        ChunksByStableId.TryGetValue(id, out ObstacleChunk chunk);
        return chunk;
    }

    void Awake()
    {
        CacheComponents();
    }

    void OnDestroy()
    {
        if (!string.IsNullOrWhiteSpace(stableId))
            ChunksByStableId.Remove(stableId);
    }

    public void Configure(string id, float configuredSizeFactor, int configuredMaxHealth, int configuredCurrentHealth, int configuredSplitCount, int configuredSpriteVariantIndex)
    {
        CacheComponents();

        if (!string.IsNullOrWhiteSpace(stableId))
            ChunksByStableId.Remove(stableId);

        stableId = id;
        sizeFactor = Mathf.Max(MinimumSizeFactor, configuredSizeFactor);
        maxHealth = Mathf.Max(1, configuredMaxHealth);
        currentHealth = Mathf.Clamp(configuredCurrentHealth, 0, maxHealth);
        splitCount = Mathf.Max(0, configuredSplitCount);
        spriteVariantIndex = configuredSpriteVariantIndex;

        if (!string.IsNullOrWhiteSpace(stableId))
            ChunksByStableId[stableId] = this;

        ApplyImmediateScale();
    }

    public void SetHealth(int configuredMaxHealth, int configuredCurrentHealth)
    {
        maxHealth = Mathf.Max(1, configuredMaxHealth);
        currentHealth = Mathf.Clamp(configuredCurrentHealth, 0, maxHealth);
    }

    public bool ApplyDamageAuthority(int damage)
    {
        if (!RoomSettings.AreObstaclesDestructible())
            return false;

        if (damage <= 0)
            return false;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        return currentHealth <= 0;
    }

    public RuntimeState CaptureRuntimeState()
    {
        CacheComponents();

        Vector2 position = body != null ? body.position : (Vector2)transform.position;
        Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
        float rotation = body != null ? body.rotation : transform.eulerAngles.z;
        float angularVelocity = body != null ? body.angularVelocity : 0f;

        return new RuntimeState(
            stableId,
            position,
            velocity,
            rotation,
            angularVelocity,
            sizeFactor,
            maxHealth,
            currentHealth,
            splitCount,
            spriteVariantIndex);
    }

    public void ApplyRuntimeState(RuntimeState state, bool isAuthority)
    {
        Configure(state.StableId, state.SizeFactor, state.MaxHealth, state.CurrentHealth, state.SplitCount, state.SpriteVariantIndex);

        if (movingObject != null)
            movingObject.SetMotionState(state.Position, state.Velocity, state.Rotation, state.AngularVelocity, isAuthority);
        else if (body != null)
        {
            body.position = state.Position;
            body.rotation = state.Rotation;
            body.linearVelocity = state.Velocity;
            body.angularVelocity = state.AngularVelocity;
        }
        else
        {
            transform.position = state.Position;
            transform.rotation = Quaternion.Euler(0f, 0f, state.Rotation);
        }
    }

    public float GetApproximateRadius()
    {
        CacheComponents();

        Collider2D[] colliders = GetComponents<Collider2D>();
        float maxExtent = 0.5f;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D current = colliders[i];
            if (current == null || !current.enabled)
                continue;

            Bounds bounds = current.bounds;
            maxExtent = Mathf.Max(maxExtent, bounds.extents.x, bounds.extents.y);
        }

        return maxExtent;
    }

    public void ApplyImmediateScale()
    {
        CacheComponents();
        FitRendererToTargetSize(spriteRenderer, GetTargetWorldSize());
    }

    public float GetTargetWorldSize()
    {
        return DefaultTargetWorldSize * MapInstanceService.GetObstacleSizeMultiplierForPosition(transform.position) * sizeFactor;
    }

    public static float ComputeStableSizeFactor(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return 1f;

        uint hash = ComputeStableHash(key);
        float sampleX = Mathf.Abs(hash * 0.00013f) + 17.3f;
        float sampleY = Mathf.Abs(hash * 0.00029f) + 29.7f;
        float noise = Mathf.PerlinNoise(sampleX, sampleY);
        return Mathf.Lerp(0.5f, 1.5f, noise);
    }

    public static int ComputeStableSpriteVariantIndex(string key, int spriteCount)
    {
        if (spriteCount <= 0)
            return -1;

        if (string.IsNullOrWhiteSpace(key))
            return 0;

        uint hash = ComputeStableHash(key);
        return (int)(hash % (uint)spriteCount);
    }

    static uint ComputeStableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                hash ^= (byte)(c & 0xFF);
                hash *= 16777619u;
                hash ^= (byte)(c >> 8);
                hash *= 16777619u;
            }

            return hash;
        }
    }

    static void FitRendererToTargetSize(SpriteRenderer renderer, float targetMaxWorldSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        float maxDimension = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        if (maxDimension <= 0f)
            return;

        float scale = targetMaxWorldSize / maxDimension;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    void CacheComponents()
    {
        if (movingObject == null)
            movingObject = GetComponent<MovingSpaceObject>();

        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }
}
