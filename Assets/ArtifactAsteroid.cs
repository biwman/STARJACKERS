using System.Collections.Generic;
using UnityEngine;

public sealed class ArtifactAsteroid : MonoBehaviour
{
    public const float ExamineRange = 2.55f;
    const float TargetWorldSize = 4.15f;
    const int VariantCount = 6;
    const int ColliderPointCount = 20;
    const float ColliderShapeShrink = 0.86f;
    const float ColliderMinHalfExtent = 0.22f;

    static readonly Dictionary<string, ArtifactAsteroid> ArtifactsById = new Dictionary<string, ArtifactAsteroid>();
    static readonly Sprite[] InactiveSprites = new Sprite[VariantCount];
    static readonly Sprite[] ActiveSprites = new Sprite[VariantCount];

    string stableId;
    int variantIndex;
    bool active;
    float sizeFactor = 1f;
    SpriteRenderer spriteRenderer;
    PolygonCollider2D bodyCollider;
    MovingSpaceObject movingObject;

    public string StableId => stableId;
    public bool IsActive => active;
    public Vector3 BeamTarget => bodyCollider != null ? bodyCollider.bounds.center : transform.position;

    public static ArtifactAsteroid Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        ArtifactsById.TryGetValue(id, out ArtifactAsteroid artifact);
        return artifact;
    }

    public static List<ArtifactAsteroid> GetAllRuntimeArtifacts()
    {
        return new List<ArtifactAsteroid>(ArtifactsById.Values);
    }

    public static int TotalCount => ArtifactsById.Count;

    public static int ActiveCount
    {
        get
        {
            int count = 0;
            foreach (ArtifactAsteroid artifact in ArtifactsById.Values)
            {
                if (artifact != null && artifact.IsActive)
                    count++;
            }

            return count;
        }
    }

    public static void ClearAllRuntimeArtifacts()
    {
        List<ArtifactAsteroid> artifacts = new List<ArtifactAsteroid>(ArtifactsById.Values);
        ArtifactsById.Clear();

        for (int i = 0; i < artifacts.Count; i++)
        {
            ArtifactAsteroid artifact = artifacts[i];
            if (artifact != null && artifact.gameObject != null && artifact.gameObject.scene.IsValid())
                Destroy(artifact.gameObject);
        }
    }

    public static ArtifactAsteroid FindClosestInactiveUsable(Vector2 position)
    {
        ArtifactAsteroid best = null;
        float bestDistance = float.MaxValue;
        foreach (ArtifactAsteroid artifact in ArtifactsById.Values)
        {
            if (artifact == null || artifact.IsActive)
                continue;

            float distance = artifact.GetInteractionDistanceToPoint(position);
            if (distance <= ExamineRange && distance < bestDistance)
            {
                best = artifact;
                bestDistance = distance;
            }
        }

        return best;
    }

    public void Configure(string id, Vector2 position, int configuredVariantIndex, float rotationDegrees, float configuredSizeFactor, bool isActive)
    {
        EnsureComponents();

        if (!string.IsNullOrWhiteSpace(stableId))
            ArtifactsById.Remove(stableId);

        stableId = id;
        variantIndex = Mathf.Clamp(configuredVariantIndex, 0, VariantCount - 1);
        sizeFactor = Mathf.Clamp(configuredSizeFactor, 0.82f, 1.18f);
        transform.position = new Vector3(position.x, position.y, 0.04f);
        transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        active = isActive;

        if (!string.IsNullOrWhiteSpace(stableId))
            ArtifactsById[stableId] = this;

        ApplyVisualState(false);
        ConfigureMotion();
    }

    public void SetActiveState(bool value, bool playActivationFx)
    {
        if (active == value)
            return;

        active = value;
        ApplyVisualState(playActivationFx && active);
    }

    public float GetInteractionDistanceToPoint(Vector2 point)
    {
        EnsureComponents();
        if (bodyCollider != null && bodyCollider.enabled)
            return Vector2.Distance(bodyCollider.ClosestPoint(point), point);

        return Vector2.Distance(transform.position, point);
    }

    void Awake()
    {
        EnsureComponents();
    }

    void OnDestroy()
    {
        if (!string.IsNullOrWhiteSpace(stableId))
            ArtifactsById.Remove(stableId);
    }

    void EnsureComponents()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = GameVisualTheme.ObstacleSortingOrder + 1;

        CircleCollider2D staleCircleCollider = GetComponent<CircleCollider2D>();
        if (staleCircleCollider != null)
        {
            staleCircleCollider.enabled = false;
            if (Application.isPlaying)
                Destroy(staleCircleCollider);
            else
                DestroyImmediate(staleCircleCollider);
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<PolygonCollider2D>();
            if (bodyCollider == null)
                bodyCollider = gameObject.AddComponent<PolygonCollider2D>();
        }

        bodyCollider.isTrigger = false;
        bodyCollider.autoTiling = false;

        if (movingObject == null)
            movingObject = GetComponent<MovingSpaceObject>();
        if (movingObject == null)
            movingObject = gameObject.AddComponent<MovingSpaceObject>();
    }

    void ApplyVisualState(bool playActivationFx)
    {
        EnsureComponents();
        spriteRenderer.sprite = LoadSprite(variantIndex, active);
        spriteRenderer.color = Color.white;
        FitToTargetWorldSize();
        ConfigureColliderShape();

        if (playActivationFx)
        {
            ArtifactActivationFlashVfx.Spawn(transform.position, GetApproximateWorldRadius());
            AudioManager.Instance.PlayAlienArtifactActivatedAt(transform.position);
        }
    }

    void FitToTargetWorldSize()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        float maxDimension = Mathf.Max(spriteRenderer.sprite.bounds.size.x, spriteRenderer.sprite.bounds.size.y);
        if (maxDimension <= 0.001f)
            return;

        float targetSize = TargetWorldSize * sizeFactor;
        float scale = targetSize / maxDimension;
        transform.localScale = new Vector3(scale, scale, 1f);

        if (bodyCollider != null)
        {
            bodyCollider.offset = Vector2.zero;
        }
    }

    float GetApproximateWorldRadius()
    {
        if (bodyCollider != null)
        {
            Bounds bounds = bodyCollider.bounds;
            return Mathf.Max(0.8f, bounds.extents.x, bounds.extents.y);
        }

        return TargetWorldSize * sizeFactor * 0.5f;
    }

    void ConfigureColliderShape()
    {
        if (bodyCollider == null)
            return;

        Sprite referenceSprite = LoadSprite(variantIndex, false);
        if (referenceSprite == null)
            referenceSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        if (referenceSprite == null)
            return;

        Bounds spriteBounds = GetSpriteOpaqueLocalBounds(referenceSprite);
        float halfWidth = Mathf.Max(ColliderMinHalfExtent, spriteBounds.extents.x * ColliderShapeShrink);
        float halfHeight = Mathf.Max(ColliderMinHalfExtent, spriteBounds.extents.y * ColliderShapeShrink);
        Vector2 center = spriteBounds.center;
        Vector2[] points = new Vector2[ColliderPointCount];

        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)points.Length * Mathf.PI * 2f;
            float rockiness = 0.96f + 0.04f * Mathf.Sin(angle * 3f + variantIndex * 1.37f);
            points[i] = center + new Vector2(Mathf.Cos(angle) * halfWidth * rockiness, Mathf.Sin(angle) * halfHeight * rockiness);
        }

        bodyCollider.pathCount = 1;
        bodyCollider.SetPath(0, points);

        if (movingObject != null)
            movingObject.NotifyColliderShapeChanged();
    }

    void ConfigureMotion()
    {
        EnsureComponents();
        if (movingObject == null || string.IsNullOrWhiteSpace(stableId))
            return;

        movingObject.Configure(stableId, MovingSpaceObject.SpaceObjectType.Obstacle);
    }

    static Bounds GetSpriteOpaqueLocalBounds(Sprite sprite)
    {
        if (sprite == null)
            return new Bounds(Vector3.zero, Vector3.one);

        Vector2[] vertices = sprite.vertices;
        if (vertices != null && vertices.Length > 0)
        {
            Vector2 min = vertices[0];
            Vector2 max = vertices[0];
            for (int i = 1; i < vertices.Length; i++)
            {
                Vector2 vertex = vertices[i];
                min = Vector2.Min(min, vertex);
                max = Vector2.Max(max, vertex);
            }

            Vector2 size = max - min;
            if (size.x > 0.001f && size.y > 0.001f)
                return new Bounds((min + max) * 0.5f, size);
        }

        return sprite.bounds;
    }

    static Sprite LoadSprite(int index, bool activeState)
    {
        Sprite[] cache = activeState ? ActiveSprites : InactiveSprites;
        int clampedIndex = Mathf.Clamp(index, 0, VariantCount - 1);
        Sprite cached = cache[clampedIndex];
        if (cached != null)
            return cached;

        string state = activeState ? "active" : "inactive";
        string variant = (clampedIndex + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        string path = "Visuals/Obstacles/ArtifactAsteroids/artifact_asteroid_" + variant + "_" + state;
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(path);
            sprite = GetLargestSprite(sprites);
        }

        cache[clampedIndex] = sprite;
        return sprite;
    }

    static Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            float area = sprite.rect.width * sprite.rect.height;
            if (best == null || area > bestArea)
            {
                best = sprite;
                bestArea = area;
            }
        }

        return best;
    }
}
