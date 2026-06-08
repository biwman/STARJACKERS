using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum NebulaFieldKind
{
    Normal,
    Fire,
    Toxic,
    Cloud
}

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class NebulaField : MonoBehaviour
{
    const int NebulaVariantCount = 9;
    const int FireNebulaVariantCount = 4;
    const int ToxicNebulaVariantCount = 1;
    const float TargetVisualSize = 3.36f;
    const float CloudDriftSpeed = 0.22f;
    const float CloudWrapPadding = 7f;
    const float ColliderRadiusFactor = 0.4f;
    const float CloudColliderRadiusFactor = 0.52f;
    const float CloudHideSampleExtentFactor = 0.45f;
    const int CloudHideRequiredSamples = 3;
    const float TriggerStayRefreshInterval = 0.08f;
    const float PlayerDeepHideFactor = 1.25f;
    const float PlayerDamageFactor = 1f;
    const float MinSizeMultiplier = 1f;
    const float MaxSizeMultiplier = 4f;
    const int NormalDamagePerTick = 1;
    const int FireDamagePerTick = 3;
    const int ToxicDamagePerTick = 1;
    const float NormalSpeedMultiplier = 1f;
    const float FireSpeedMultiplier = 0.5f;
    static readonly string[] CloudSpriteResourcePaths =
    {
        "Visuals/Clouds/cloud_topdown_cumulus_broad",
        "Visuals/Clouds/cloud_topdown_front_band",
        "Visuals/Clouds/cloud_topdown_scattered_cluster",
        "Visuals/Clouds/cloud_topdown_wispy_sheet"
    };

    static readonly Dictionary<int, NebulaField> FieldsByKey = new Dictionary<int, NebulaField>();
    static Sprite[] cachedNebulaSprites;
    static Sprite[] cachedFireNebulaSprites;
    static Sprite[] cachedToxicNebulaSprites;
    static Sprite[] cachedCloudSprites;

    SpriteRenderer spriteRenderer;
    AdvancedNebulaVisual advancedVisual;
    CircleCollider2D triggerCollider;
    Rigidbody2D cloudRigidbody;
    Sprite activeNebulaSprite;
    Vector2[] activeSpriteVertices;
    ushort[] activeSpriteTriangles;
    Bounds activeSpriteBounds;
    int nebulaKey;
    float nebulaScaleMultiplier = 1f;
    NebulaFieldKind fieldKind = NebulaFieldKind.Normal;
    Vector2 cloudInitialPosition;
    Vector2 cloudDriftDirection = Vector2.right;
    Vector2 cloudBoundsMin;
    Vector2 cloudBoundsSpan;
    double cloudRoundStartTime;
    int cloudIndex;
    int cloudWrapGeneration;
    bool cloudDriftConfigured;
    bool cloudUsesNetworkClock;
    readonly Dictionary<HideInNebulaTarget, float> nextTriggerStayRefreshByTarget = new Dictionary<HideInNebulaTarget, float>();

    public NebulaFieldKind FieldKind => fieldKind;
    public int DamagePerTick => fieldKind == NebulaFieldKind.Fire ? FireDamagePerTick : fieldKind == NebulaFieldKind.Toxic ? ToxicDamagePerTick : fieldKind == NebulaFieldKind.Cloud ? 0 : NormalDamagePerTick;
    public float SpeedMultiplier => fieldKind == NebulaFieldKind.Fire ? FireSpeedMultiplier : NormalSpeedMultiplier;

    void Awake()
    {
        nebulaKey = GetHashCode();
        FieldsByKey[nebulaKey] = this;
        spriteRenderer = GetComponent<SpriteRenderer>();
        triggerCollider = GetComponent<CircleCollider2D>();
        cloudRigidbody = GetComponent<Rigidbody2D>();
        nebulaScaleMultiplier = GetDeterministicScaleMultiplier((Vector2)transform.position);
        transform.rotation = Quaternion.Euler(0f, 0f, GetDeterministicRotation((Vector2)transform.position));
        ConfigureVisual();
        ConfigureCollider();
        RefreshAdvancedVisualState();
    }

    void Update()
    {
        if (fieldKind == NebulaFieldKind.Normal)
            RefreshAdvancedVisualState();
    }

    void FixedUpdate()
    {
        if (fieldKind == NebulaFieldKind.Cloud && cloudDriftConfigured)
            UpdateCloudDrift(false);
    }

    void OnEnable()
    {
        FieldsByKey[nebulaKey] = this;
    }

    void OnDisable()
    {
        if (FieldsByKey.TryGetValue(nebulaKey, out NebulaField field) && field == this)
            FieldsByKey.Remove(nebulaKey);

        HideInNebulaTarget.RemoveNebulaFromAll(nebulaKey);
    }

    public static bool TryGetField(int nebulaId, out NebulaField field)
    {
        if (FieldsByKey.TryGetValue(nebulaId, out field) && field != null && field.isActiveAndEnabled)
            return true;

        field = null;
        return false;
    }

    public void ConfigureKind(NebulaFieldKind kind)
    {
        if (fieldKind == kind && activeNebulaSprite != null)
            return;

        fieldKind = kind;
        ConfigureVisual();
        ConfigureCollider();
        RefreshAdvancedVisualState();
    }

    public void ConfigureCloudDrift(Vector2 direction, int index)
    {
        cloudDriftDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        cloudInitialPosition = transform.position;
        cloudIndex = Mathf.Max(0, index);
        cloudWrapGeneration = 0;
        cloudDriftConfigured = true;
        ConfigureCloudBounds();
        CaptureCloudTiming();
        ConfigureVisual();
        ConfigureCollider();
        RefreshAdvancedVisualState();
        UpdateCloudDrift(true);
    }

    void ConfigureVisual()
    {
        if (spriteRenderer == null)
            return;

        Sprite[] sprites = GetSpritesForKind(fieldKind);
        Vector2 visualSeedPosition = GetVisualSeedPosition();

        activeNebulaSprite = ResolveVariantSprite(visualSeedPosition, sprites);
        if (activeNebulaSprite == null)
            return;

        spriteRenderer.sprite = activeNebulaSprite;
        activeSpriteVertices = activeNebulaSprite.vertices;
        activeSpriteTriangles = activeNebulaSprite.triangles;
        activeSpriteBounds = activeNebulaSprite.bounds;
        spriteRenderer.color = fieldKind == NebulaFieldKind.Fire
            ? new Color(1f, 0.82f, 0.56f, 0.9f)
            : fieldKind == NebulaFieldKind.Toxic
                ? new Color(0.88f, 1f, 0.42f, 0.9f)
            : fieldKind == NebulaFieldKind.Cloud
                ? new Color(1f, 1f, 1f, 0.9f)
                : new Color(0.9f, 0.97f, 1f, 0.82f);
        ApplySortingLayer();

        float maxDimension = Mathf.Max(activeNebulaSprite.bounds.size.x, activeNebulaSprite.bounds.size.y);
        if (maxDimension > 0f)
        {
            float roomSizeMultiplier = fieldKind == NebulaFieldKind.Fire
                ? RoomSettings.GetFireNebulaSizeMultiplier()
                : fieldKind == NebulaFieldKind.Toxic
                    ? RoomSettings.GetToxicNebulaSizeMultiplier()
                : fieldKind == NebulaFieldKind.Cloud
                    ? RoomSettings.GetCloudsSizeMultiplier()
                : RoomSettings.GetNebulaSizeMultiplier();
            float sizeMultiplier = fieldKind == NebulaFieldKind.Cloud
                ? GetDeterministicScaleMultiplier(visualSeedPosition)
                : nebulaScaleMultiplier;
            float scale = (TargetVisualSize * sizeMultiplier * roomSizeMultiplier) / maxDimension;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        if (fieldKind == NebulaFieldKind.Cloud)
            transform.rotation = Quaternion.Euler(0f, 0f, GetDeterministicRotation(visualSeedPosition));
    }

    void RefreshAdvancedVisualState()
    {
        if (spriteRenderer == null)
            return;

        bool useAdvancedVisual = fieldKind == NebulaFieldKind.Normal && RoomSettings.IsAdvancedNebulaEnabled();
        if (fieldKind == NebulaFieldKind.Normal)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.color = useAdvancedVisual
                ? new Color(0.52f, 0.9f, 1f, 0.32f)
                : new Color(0.9f, 0.97f, 1f, 0.82f);
        }

        if (!useAdvancedVisual)
        {
            if (advancedVisual != null)
                advancedVisual.SetVisible(false);
            return;
        }

        if (advancedVisual == null)
            advancedVisual = gameObject.GetComponent<AdvancedNebulaVisual>() ?? gameObject.AddComponent<AdvancedNebulaVisual>();

        float localHideRadius = triggerCollider != null ? triggerCollider.radius : 0.5f;
        advancedVisual.Configure(nebulaKey, spriteRenderer.sortingOrder, localHideRadius);
    }

    void ApplySortingLayer()
    {
        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = Mathf.Min(GameVisualTheme.ExtractionZoneSortingOrder, GameVisualTheme.RepairBaySortingOrder) - 1;
    }

    void ConfigureCollider()
    {
        if (triggerCollider == null || spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        triggerCollider.isTrigger = true;
        float radiusFactor = fieldKind == NebulaFieldKind.Cloud ? CloudColliderRadiusFactor : ColliderRadiusFactor;
        float radius = Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y) * radiusFactor;
        Vector3 lossyScale = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
        if (maxScale <= 0.0001f)
            maxScale = 1f;

        triggerCollider.radius = radius / maxScale;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HideInNebulaTarget target = other.GetComponentInParent<HideInNebulaTarget>();
        RefreshTargetState(target);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        HideInNebulaTarget target = other.GetComponentInParent<HideInNebulaTarget>();
        if (target == null || !ShouldRefreshTriggerStay(target))
            return;

        RefreshTargetState(target);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        HideInNebulaTarget target = other.GetComponentInParent<HideInNebulaTarget>();
        if (target != null)
        {
            nextTriggerStayRefreshByTarget.Remove(target);
            target.RemoveNebula(nebulaKey);
        }
    }

    bool ShouldRefreshTriggerStay(HideInNebulaTarget target)
    {
        float now = Time.time;
        if (nextTriggerStayRefreshByTarget.TryGetValue(target, out float nextRefresh) && now < nextRefresh)
            return false;

        nextTriggerStayRefreshByTarget[target] = now + TriggerStayRefreshInterval;
        return true;
    }

    void RefreshTargetState(HideInNebulaTarget target)
    {
        if (target == null)
            return;

        target.UpdateNebulaState(nebulaKey, ShouldHideTarget(target), ShouldDamageTarget(target), DamagePerTick, GetSpeedMultiplierForTarget(target), fieldKind);
    }

    bool ShouldHideTarget(HideInNebulaTarget target)
    {
        if (target == null)
            return false;

        if (fieldKind == NebulaFieldKind.Cloud)
            return ShouldHideTargetInCloud(target);

        Bounds targetBounds = GetTargetBounds(target);
        float nebulaRadius = GetWorldNebulaRadius();
        float targetRadius = Mathf.Max(targetBounds.extents.x, targetBounds.extents.y);
        float allowedDistance = nebulaRadius - (targetRadius * PlayerDeepHideFactor);

        if (allowedDistance <= 0f)
            return false;

        float distance = Vector2.Distance(transform.position, targetBounds.center);
        return distance <= allowedDistance;
    }

    bool ShouldHideTargetInCloud(HideInNebulaTarget target)
    {
        Bounds targetBounds = GetTargetBounds(target);
        Vector2 center = targetBounds.center;

        if (!IsWorldPointInsideActiveSpriteShape(center))
            return false;

        float sampleX = Mathf.Max(0.02f, targetBounds.extents.x * CloudHideSampleExtentFactor);
        float sampleY = Mathf.Max(0.02f, targetBounds.extents.y * CloudHideSampleExtentFactor);
        int insideSamples = 1;

        if (IsWorldPointInsideActiveSpriteShape(center + new Vector2(sampleX, 0f)))
            insideSamples++;
        if (IsWorldPointInsideActiveSpriteShape(center - new Vector2(sampleX, 0f)))
            insideSamples++;
        if (IsWorldPointInsideActiveSpriteShape(center + new Vector2(0f, sampleY)))
            insideSamples++;
        if (IsWorldPointInsideActiveSpriteShape(center - new Vector2(0f, sampleY)))
            insideSamples++;

        return insideSamples >= CloudHideRequiredSamples;
    }

    public bool ContainsTarget(HideInNebulaTarget target)
    {
        if (target == null)
            return false;

        Bounds targetBounds = GetTargetBounds(target);
        float nebulaRadius = GetWorldNebulaRadius();
        float targetRadius = Mathf.Max(targetBounds.extents.x, targetBounds.extents.y);
        float distance = Vector2.Distance(transform.position, targetBounds.center);
        return distance <= nebulaRadius + Mathf.Max(0.02f, targetRadius * 0.04f);
    }

    public bool ShouldHide(HideInNebulaTarget target)
    {
        return ShouldHideTarget(target);
    }

    public bool ShouldDamage(HideInNebulaTarget target)
    {
        return ShouldDamageTarget(target);
    }

    public float GetSpeedMultiplierForTarget(HideInNebulaTarget target)
    {
        if (fieldKind != NebulaFieldKind.Fire)
            return NormalSpeedMultiplier;

        return ShouldDamageTarget(target) ? FireSpeedMultiplier : NormalSpeedMultiplier;
    }

    bool ShouldDamageTarget(HideInNebulaTarget target)
    {
        if (fieldKind == NebulaFieldKind.Cloud)
            return false;

        PlayerHealth health = target.GetComponent<PlayerHealth>();
        if (health == null)
            return false;

        Bounds targetBounds = GetTargetBounds(target);
        float nebulaRadius = GetWorldNebulaRadius();
        float targetRadius = Mathf.Max(targetBounds.extents.x, targetBounds.extents.y);
        float allowedDistance = nebulaRadius - (targetRadius * PlayerDamageFactor);

        if (allowedDistance <= 0f)
            return false;

        float distance = Vector2.Distance(transform.position, targetBounds.center);
        return distance <= allowedDistance;
    }

    Bounds GetTargetBounds(HideInNebulaTarget target)
    {
        return target != null ? target.GetNebulaBounds() : new Bounds(transform.position, Vector3.one);
    }

    float GetWorldNebulaRadius()
    {
        if (triggerCollider == null)
            return 0f;

        Vector3 lossyScale = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
        if (maxScale <= 0.0001f)
            maxScale = 1f;

        return triggerCollider.radius * maxScale;
    }

    bool IsWorldPointInsideActiveSpriteShape(Vector2 worldPoint)
    {
        if (activeNebulaSprite == null)
            return false;

        Vector2 localPoint = transform.InverseTransformPoint(worldPoint);
        if (!activeSpriteBounds.Contains(localPoint))
            return false;

        if (activeSpriteVertices == null ||
            activeSpriteTriangles == null ||
            activeSpriteVertices.Length < 3 ||
            activeSpriteTriangles.Length < 3)
        {
            return true;
        }

        for (int i = 0; i + 2 < activeSpriteTriangles.Length; i += 3)
        {
            Vector2 a = activeSpriteVertices[activeSpriteTriangles[i]];
            Vector2 b = activeSpriteVertices[activeSpriteTriangles[i + 1]];
            Vector2 c = activeSpriteVertices[activeSpriteTriangles[i + 2]];
            if (IsPointInTriangle(localPoint, a, b, c))
                return true;
        }

        return false;
    }

    static bool IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(point, a, b);
        float d2 = Cross(point, b, c);
        float d3 = Cross(point, c, a);
        bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNegative && hasPositive);
    }

    static float Cross(Vector2 point, Vector2 a, Vector2 b)
    {
        return (point.x - b.x) * (a.y - b.y) - (a.x - b.x) * (point.y - b.y);
    }

    void UpdateCloudDrift(bool forceRefresh)
    {
        float elapsed = (float)GetRoundElapsedSeconds();
        Vector2 rawPosition = cloudInitialPosition + cloudDriftDirection * (CloudDriftSpeed * elapsed);
        int wrapX;
        int wrapY;
        float wrappedX = WrapCoordinate(rawPosition.x, cloudBoundsMin.x, cloudBoundsSpan.x, out wrapX);
        float wrappedY = WrapCoordinate(rawPosition.y, cloudBoundsMin.y, cloudBoundsSpan.y, out wrapY);
        int wrapGeneration = (wrapX * 73856093) ^ (wrapY * 19349663);
        bool changedGeneration = wrapGeneration != cloudWrapGeneration;

        Vector2 wrappedPosition = new Vector2(wrappedX, wrappedY);
        if (cloudRigidbody != null)
        {
            if (forceRefresh || changedGeneration)
                cloudRigidbody.position = wrappedPosition;
            else
                cloudRigidbody.MovePosition(wrappedPosition);
        }
        else
        {
            transform.position = new Vector3(wrappedX, wrappedY, transform.position.z);
        }

        if (forceRefresh || changedGeneration)
        {
            cloudWrapGeneration = wrapGeneration;
            ConfigureVisual();
            ConfigureCollider();
            HideInNebulaTarget.RemoveNebulaFromAll(nebulaKey);
        }
    }

    void ConfigureCloudBounds()
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float minX = -mapSize.x * 0.5f - CloudWrapPadding;
        float maxX = mapSize.x * 0.5f + CloudWrapPadding;
        float minY = -mapSize.y * 0.5f - CloudWrapPadding;
        float maxY = mapSize.y * 0.5f + CloudWrapPadding;

        cloudBoundsMin = new Vector2(minX, minY);
        cloudBoundsSpan = new Vector2(
            Mathf.Max(1f, maxX - minX),
            Mathf.Max(1f, maxY - minY));
    }

    void CaptureCloudTiming()
    {
        cloudUsesNetworkClock = false;
        cloudRoundStartTime = 0d;

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object value) &&
            value is double startTime)
        {
            cloudUsesNetworkClock = true;
            cloudRoundStartTime = startTime;
        }
    }

    static float WrapCoordinate(float value, float min, float span, out int wrapCount)
    {
        float offset = value - min;
        wrapCount = Mathf.FloorToInt(offset / span);
        return min + Mathf.Repeat(offset, span);
    }

    double GetRoundElapsedSeconds()
    {
        if (cloudUsesNetworkClock)
        {
            double elapsed = PhotonNetwork.Time - cloudRoundStartTime;
            return elapsed > 0d ? elapsed : 0d;
        }

        return Time.timeSinceLevelLoad;
    }

    Vector2 GetVisualSeedPosition()
    {
        if (fieldKind != NebulaFieldKind.Cloud)
            return transform.position;

        return cloudInitialPosition + new Vector2(
            (cloudIndex + 1) * 7.91f + cloudWrapGeneration * 13.37f,
            (cloudIndex + 1) * 3.17f - cloudWrapGeneration * 5.29f);
    }

    static float GetDeterministicScaleMultiplier(Vector2 position)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Mathf.RoundToInt(position.x * 100f);
            hash = hash * 31 + Mathf.RoundToInt(position.y * 100f);
            float normalized = Mathf.Abs(Mathf.Sin(hash * 0.0001234f));
            return Mathf.Lerp(MinSizeMultiplier, MaxSizeMultiplier, normalized);
        }
    }

    static float GetDeterministicRotation(Vector2 position)
    {
        unchecked
        {
            int hash = 23;
            hash = hash * 37 + Mathf.RoundToInt(position.x * 100f);
            hash = hash * 37 + Mathf.RoundToInt(position.y * 100f);
            float normalized = Mathf.Abs(Mathf.Sin(hash * 0.0001731f));
            return Mathf.Lerp(0f, 360f, normalized);
        }
    }

    static Sprite[] GetSpritesForKind(NebulaFieldKind kind)
    {
        if (kind == NebulaFieldKind.Fire)
        {
            if (cachedFireNebulaSprites == null || cachedFireNebulaSprites.Length == 0)
                cachedFireNebulaSprites = LoadSprites(NebulaFieldKind.Fire);

            return cachedFireNebulaSprites;
        }

        if (kind == NebulaFieldKind.Cloud)
        {
            if (cachedCloudSprites == null || cachedCloudSprites.Length == 0)
                cachedCloudSprites = LoadSprites(NebulaFieldKind.Cloud);

            return cachedCloudSprites;
        }

        if (kind == NebulaFieldKind.Toxic)
        {
            if (cachedToxicNebulaSprites == null || cachedToxicNebulaSprites.Length == 0)
                cachedToxicNebulaSprites = LoadSprites(NebulaFieldKind.Toxic);

            return cachedToxicNebulaSprites;
        }

        if (cachedNebulaSprites == null || cachedNebulaSprites.Length == 0)
            cachedNebulaSprites = LoadSprites(NebulaFieldKind.Normal);

        return cachedNebulaSprites;
    }

    static int GetDeterministicVariantIndex(Vector2 position, int variantCount)
    {
        if (variantCount <= 0)
            return 0;

        unchecked
        {
            int hash = 31;
            hash = hash * 41 + Mathf.RoundToInt(position.x * 100f);
            hash = hash * 41 + Mathf.RoundToInt(position.y * 100f);
            return Mathf.Abs(hash) % variantCount;
        }
    }

    static Sprite ResolveVariantSprite(Vector2 position, Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        int startIndex = GetDeterministicVariantIndex(position, sprites.Length);
        for (int offset = 0; offset < sprites.Length; offset++)
        {
            int index = (startIndex + offset) % sprites.Length;
            if (sprites[index] != null)
                return sprites[index];
        }

        return null;
    }

    static Sprite[] LoadSprites(NebulaFieldKind kind)
    {
        if (kind == NebulaFieldKind.Cloud)
        {
            Sprite[] cloudSprites = new Sprite[CloudSpriteResourcePaths.Length];
            for (int i = 0; i < cloudSprites.Length; i++)
            {
                string resourcePath = CloudSpriteResourcePaths[i];
                cloudSprites[i] = LoadSingleSprite(
                    resourcePath,
                    "Assets/Resources/" + resourcePath + ".png",
                    null);
            }

            return cloudSprites;
        }

        int variantCount = kind == NebulaFieldKind.Fire
            ? FireNebulaVariantCount
            : kind == NebulaFieldKind.Toxic
                ? ToxicNebulaVariantCount
                : NebulaVariantCount;
        string prefix = kind == NebulaFieldKind.Fire
            ? "fire_nebula_variant_"
            : kind == NebulaFieldKind.Toxic
                ? "toxic_nebula_variant_"
                : "nebula_variant_";
        Sprite[] sprites = new Sprite[variantCount];
        for (int i = 0; i < sprites.Length; i++)
        {
            string suffix = (i + 1).ToString("00");
            sprites[i] = LoadSingleSprite(
                prefix + suffix,
                "Assets/Resources/" + prefix + suffix + ".png",
                null,
                kind == NebulaFieldKind.Toxic);
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                return sprites;
        }

        if (kind == NebulaFieldKind.Fire || kind == NebulaFieldKind.Toxic)
            return sprites;

        Sprite fallback = LoadLegacySprite();
        if (fallback != null)
            sprites[0] = fallback;

        return sprites;
    }

    static Sprite LoadLegacySprite()
    {
        Sprite sprite = LoadSingleSprite("koszmiczna_anomalia_resource", "Assets/Resources/koszmiczna_anomalia_resource.png", "Assets/koszmiczna_anomalia.png");
        if (sprite != null)
            return sprite;

        return LoadSingleSprite("nebula_frayed_resource", "Assets/Resources/nebula_frayed_resource.png", "Assets/nebula_frayed.png");
    }

    static Sprite LoadSingleSprite(string resourcesPath, string editorPreferredPath, string editorFallbackPath, bool preferFullTexture = false)
    {
        if (preferFullTexture)
        {
            Sprite fullTextureSprite = LoadFullTextureSprite(resourcesPath, editorPreferredPath);
            if (fullTextureSprite != null)
                return fullTextureSprite;
        }

        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcesPath);
        sprite = GetLargestSprite(sprites);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
        if (texture != null)
            return CreateSpriteFromTexture(texture);

#if UNITY_EDITOR
        sprite = LoadEditorSprite(editorPreferredPath);
        if (sprite != null)
            return sprite;

        if (!string.IsNullOrWhiteSpace(editorFallbackPath))
            return LoadEditorSprite(editorFallbackPath);
#endif

        return null;
    }

    static Sprite LoadFullTextureSprite(string resourcesPath, string editorPreferredPath)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
        if (texture != null)
            return CreateSpriteFromTexture(texture);

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(editorPreferredPath))
        {
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(editorPreferredPath);
            if (texture != null)
                return CreateSpriteFromTexture(texture);
        }
#endif

        return null;
    }

    static Sprite CreateSpriteFromTexture(Texture2D texture)
    {
        if (texture == null)
            return null;

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float pixelsPerUnit = Mathf.Max(texture.width, texture.height);
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);
    }

    static Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (candidate == null)
                continue;

            float area = candidate.rect.width * candidate.rect.height;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }

#if UNITY_EDITOR
    static Sprite LoadEditorSprite(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
            return sprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite loadedSprite)
                return loadedSprite;
        }

        return null;
    }
#endif
}
