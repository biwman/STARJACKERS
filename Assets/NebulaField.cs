using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum NebulaFieldKind
{
    Normal,
    Fire
}

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class NebulaField : MonoBehaviour
{
    const int NebulaVariantCount = 9;
    const int FireNebulaVariantCount = 4;
    const float TargetVisualSize = 3.36f;
    const float ColliderRadiusFactor = 0.4f;
    const float PlayerDeepHideFactor = 1.25f;
    const float PlayerDamageFactor = 1f;
    const float MinSizeMultiplier = 1f;
    const float MaxSizeMultiplier = 4f;
    const int NormalDamagePerTick = 1;
    const int FireDamagePerTick = 3;
    const float NormalSpeedMultiplier = 1f;
    const float FireSpeedMultiplier = 0.5f;

    static readonly Dictionary<int, NebulaField> FieldsByKey = new Dictionary<int, NebulaField>();
    static Sprite[] cachedNebulaSprites;
    static Sprite[] cachedFireNebulaSprites;

    SpriteRenderer spriteRenderer;
    CircleCollider2D triggerCollider;
    Sprite activeNebulaSprite;
    int nebulaKey;
    float nebulaScaleMultiplier = 1f;
    NebulaFieldKind fieldKind = NebulaFieldKind.Normal;

    public NebulaFieldKind FieldKind => fieldKind;
    public int DamagePerTick => fieldKind == NebulaFieldKind.Fire ? FireDamagePerTick : NormalDamagePerTick;
    public float SpeedMultiplier => fieldKind == NebulaFieldKind.Fire ? FireSpeedMultiplier : NormalSpeedMultiplier;

    void Awake()
    {
        nebulaKey = GetHashCode();
        FieldsByKey[nebulaKey] = this;
        spriteRenderer = GetComponent<SpriteRenderer>();
        triggerCollider = GetComponent<CircleCollider2D>();
        nebulaScaleMultiplier = GetDeterministicScaleMultiplier((Vector2)transform.position);
        transform.rotation = Quaternion.Euler(0f, 0f, GetDeterministicRotation((Vector2)transform.position));
        ConfigureVisual();
        ConfigureCollider();
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
    }

    void ConfigureVisual()
    {
        if (spriteRenderer == null)
            return;

        Sprite[] sprites = GetSpritesForKind(fieldKind);

        activeNebulaSprite = ResolveVariantSprite((Vector2)transform.position, sprites);
        if (activeNebulaSprite == null)
            return;

        spriteRenderer.sprite = activeNebulaSprite;
        spriteRenderer.color = fieldKind == NebulaFieldKind.Fire
            ? new Color(1f, 0.82f, 0.56f, 0.9f)
            : new Color(0.9f, 0.97f, 1f, 0.82f);
        ApplySortingLayer();

        float maxDimension = Mathf.Max(activeNebulaSprite.bounds.size.x, activeNebulaSprite.bounds.size.y);
        if (maxDimension > 0f)
        {
            float roomSizeMultiplier = fieldKind == NebulaFieldKind.Fire
                ? RoomSettings.GetFireNebulaSizeMultiplier()
                : RoomSettings.GetNebulaSizeMultiplier();
            float scale = (TargetVisualSize * nebulaScaleMultiplier * roomSizeMultiplier) / maxDimension;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    void ApplySortingLayer()
    {
        SpriteRenderer reference = FindReferenceRenderer("Obstacle");
        if (reference == null)
            reference = FindReferenceRenderer("Ground");
        if (reference == null)
            reference = FindReferenceRenderer("Player");

        if (reference != null)
        {
            spriteRenderer.sortingLayerID = reference.sortingLayerID;
            spriteRenderer.sortingOrder = reference.sortingOrder + 1;
        }
        else
        {
            spriteRenderer.sortingOrder = 10;
        }
    }

    SpriteRenderer FindReferenceRenderer(string objectName)
    {
        GameObject referenceObject = GameObject.Find(objectName);
        if (referenceObject == null)
            return null;

        return referenceObject.GetComponent<SpriteRenderer>();
    }

    void ConfigureCollider()
    {
        if (triggerCollider == null || spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        triggerCollider.isTrigger = true;
        float radius = Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y) * ColliderRadiusFactor;
        Vector3 lossyScale = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
        if (maxScale <= 0.0001f)
            maxScale = 1f;

        triggerCollider.radius = radius / maxScale;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HideInNebulaTarget target = other.GetComponentInParent<HideInNebulaTarget>();
        if (target != null)
        {
            target.UpdateNebulaState(nebulaKey, ShouldHideTarget(target), ShouldDamageTarget(target), DamagePerTick, GetSpeedMultiplierForTarget(target), fieldKind == NebulaFieldKind.Fire);
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        HideInNebulaTarget target = other.GetComponentInParent<HideInNebulaTarget>();
        if (target != null)
        {
            target.UpdateNebulaState(nebulaKey, ShouldHideTarget(target), ShouldDamageTarget(target), DamagePerTick, GetSpeedMultiplierForTarget(target), fieldKind == NebulaFieldKind.Fire);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        HideInNebulaTarget target = other.GetComponentInParent<HideInNebulaTarget>();
        if (target != null)
        {
            target.RemoveNebula(nebulaKey);
        }
    }

    bool ShouldHideTarget(HideInNebulaTarget target)
    {
        if (target == null)
            return false;

        Bounds targetBounds = GetTargetBounds(target);
        float nebulaRadius = GetWorldNebulaRadius();
        float targetRadius = Mathf.Max(targetBounds.extents.x, targetBounds.extents.y);
        float allowedDistance = nebulaRadius - (targetRadius * PlayerDeepHideFactor);

        if (allowedDistance <= 0f)
            return false;

        float distance = Vector2.Distance(transform.position, targetBounds.center);
        return distance <= allowedDistance;
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
        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(false);
        bool hasBounds = false;
        Bounds combined = new Bounds(target.transform.position, Vector3.zero);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;

            if (!hasBounds)
            {
                combined = collider.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(collider.bounds);
            }
        }

        if (hasBounds)
            return combined;

        SpriteRenderer rootRenderer = target.GetComponent<SpriteRenderer>();
        if (rootRenderer != null && rootRenderer.enabled)
            return rootRenderer.bounds;

        SpriteRenderer[] targetRenderers = target.GetComponentsInChildren<SpriteRenderer>(false);
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            SpriteRenderer renderer = targetRenderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                combined = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? combined : new Bounds(target.transform.position, Vector3.one);
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
        int variantCount = kind == NebulaFieldKind.Fire ? FireNebulaVariantCount : NebulaVariantCount;
        string prefix = kind == NebulaFieldKind.Fire ? "fire_nebula_variant_" : "nebula_variant_";
        Sprite[] sprites = new Sprite[variantCount];
        for (int i = 0; i < sprites.Length; i++)
        {
            string suffix = (i + 1).ToString("00");
            sprites[i] = LoadSingleSprite(
                prefix + suffix,
                "Assets/Resources/" + prefix + suffix + ".png",
                null);
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                return sprites;
        }

        if (kind == NebulaFieldKind.Fire)
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

    static Sprite LoadSingleSprite(string resourcesPath, string editorPreferredPath, string editorFallbackPath)
    {
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
