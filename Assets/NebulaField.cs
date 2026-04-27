using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class NebulaField : MonoBehaviour
{
    const float TargetVisualSize = 3.36f;
    const float ColliderRadiusFactor = 0.4f;
    const float PlayerDeepHideFactor = 1.25f;
    const float PlayerDamageFactor = 1f;
    const float MinSizeMultiplier = 1f;
    const float MaxSizeMultiplier = 4f;

    static readonly Dictionary<int, NebulaField> FieldsByKey = new Dictionary<int, NebulaField>();
    static Sprite cachedNebulaSprite;

    SpriteRenderer spriteRenderer;
    CircleCollider2D triggerCollider;
    int nebulaKey;
    float nebulaScaleMultiplier = 1f;

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

    void ConfigureVisual()
    {
        if (spriteRenderer == null)
            return;

        if (cachedNebulaSprite == null)
        {
            cachedNebulaSprite = LoadSprite();
        }

        if (cachedNebulaSprite == null)
            return;

        spriteRenderer.sprite = cachedNebulaSprite;
        spriteRenderer.color = new Color(0.72f, 0.9f, 1f, 0.72f);
        ApplySortingLayer();

        float maxDimension = Mathf.Max(cachedNebulaSprite.bounds.size.x, cachedNebulaSprite.bounds.size.y);
        if (maxDimension > 0f)
        {
            float scale = (TargetVisualSize * nebulaScaleMultiplier) / maxDimension;
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
            target.UpdateNebulaState(nebulaKey, ShouldHideTarget(target), ShouldDamageTarget(target));
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        HideInNebulaTarget target = other.GetComponentInParent<HideInNebulaTarget>();
        if (target != null)
        {
            target.UpdateNebulaState(nebulaKey, ShouldHideTarget(target), ShouldDamageTarget(target));
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

    static Sprite LoadSprite()
    {
        Sprite sprite = Resources.Load<Sprite>("koszmiczna_anomalia_resource");
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>("koszmiczna_anomalia_resource");
        sprite = GetLargestSprite(sprites);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>("koszmiczna_anomalia_resource");
        if (texture != null)
            return CreateSpriteFromTexture(texture);

        sprite = Resources.Load<Sprite>("nebula_frayed_resource");
        if (sprite != null)
            return sprite;

        sprites = Resources.LoadAll<Sprite>("nebula_frayed_resource");
        sprite = GetLargestSprite(sprites);
        if (sprite != null)
            return sprite;

        texture = Resources.Load<Texture2D>("nebula_frayed_resource");
        if (texture != null)
            return CreateSpriteFromTexture(texture);

#if UNITY_EDITOR
        sprite = LoadEditorSprite("Assets/Resources/koszmiczna_anomalia_resource.png");
        if (sprite != null)
            return sprite;

        sprite = LoadEditorSprite("Assets/koszmiczna_anomalia.png");
        if (sprite != null)
            return sprite;

        sprite = LoadEditorSprite("Assets/Resources/nebula_frayed_resource.png");
        if (sprite != null)
            return sprite;

        sprite = LoadEditorSprite("Assets/nebula_frayed.png");
        if (sprite != null)
            return sprite;
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
