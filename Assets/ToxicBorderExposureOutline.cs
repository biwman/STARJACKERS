using System.Collections.Generic;
using UnityEngine;

public sealed class ToxicBorderExposureOutline : MonoBehaviour
{
    const float ExposureGraceSeconds = 0.18f;
    const float SourceRefreshInterval = 0.35f;
    const float PulseSpeed = 3.05f;
    const float BaseEdgeAlpha = 0.16f;
    const float PulseEdgeAlpha = 0.12f;
    const float BaseHaloAlpha = 0.11f;
    const float PulseHaloAlpha = 0.08f;
    const float EdgeScale = 1.04f;
    const float HaloScale = 1.13f;
    const float MinLocalOffset = 0.018f;
    const float MaxLocalOffset = 0.075f;
    const float LocalOffsetRatio = 0.042f;
    const float AuraPadding = 1.42f;
    const float MinAuraSize = 0.95f;
    const float AuraDrift = 0.045f;
    const int MaxOutlinedRenderers = 10;
    const int EdgeSortingOffset = 11;
    const int HaloSortingOffset = -1;
    const int AuraSortingOffset = 14;
    const int AuraLayerCount = 3;
    const int AuraTextureSize = 192;

    static readonly Vector2[] EdgeDirections =
    {
        new Vector2(0f, 1f),
        new Vector2(0f, -1f),
        new Vector2(-1f, 0f),
        new Vector2(1f, 0f),
        new Vector2(0.7f, 0.7f),
        new Vector2(-0.7f, 0.7f),
        new Vector2(0.7f, -0.7f),
        new Vector2(-0.7f, -0.7f)
    };

    static readonly float[] AuraLayerScales = { 1.02f, 1.19f, 1.38f };
    static readonly float[] AuraLayerAlpha = { 0.42f, 0.28f, 0.19f };
    static readonly float[] AuraLayerPulse = { 0.16f, 0.12f, 0.09f };
    static readonly float[] AuraLayerRotationSpeed = { 5.2f, -3.1f, 2.15f };
    static readonly Color[] AuraLayerColors =
    {
        new Color(0.24f, 0.94f, 0.1f, 1f),
        new Color(0.54f, 1f, 0.14f, 1f),
        new Color(0.86f, 1f, 0.26f, 1f)
    };
    static readonly Sprite[] AuraSprites = new Sprite[AuraLayerCount];

    readonly List<OutlineBinding> bindings = new List<OutlineBinding>();
    readonly SpriteRenderer[] auraRenderers = new SpriteRenderer[AuraLayerCount];

    float lastExposureTime = -1000f;
    float nextSourceRefreshTime;
    float phase;
    bool visible;

    void Awake()
    {
        phase = Mathf.Abs(Mathf.Sin(GetHashCode() * 0.271f)) * Mathf.PI * 2f;
        enabled = false;
    }

    void OnDisable()
    {
        SetLayerVisibility(false);
    }

    void OnDestroy()
    {
        ClearBindings();
        DestroyAuraRenderers();
    }

    void LateUpdate()
    {
        if (Time.time - lastExposureTime > ExposureGraceSeconds)
        {
            HideNow();
            return;
        }

        if (Time.time >= nextSourceRefreshTime)
            RefreshSourceBindingsIfNeeded();

        RefreshLayers();
    }

    public void ShowForFrame()
    {
        lastExposureTime = Time.time;
        enabled = true;

        if (bindings.Count == 0)
            RebuildBindings();
        else if (Time.time >= nextSourceRefreshTime)
            RefreshSourceBindingsIfNeeded();

        visible = true;
        RefreshLayers();
    }

    public void HideNow()
    {
        visible = false;
        SetLayerVisibility(false);
        enabled = false;
    }

    void RebuildBindings()
    {
        ClearBindings();
        nextSourceRefreshTime = Time.time + SourceRefreshInterval;

        SpriteRenderer[] sources = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sources.Length && bindings.Count < MaxOutlinedRenderers; i++)
        {
            SpriteRenderer source = sources[i];
            if (!CanUseSource(source))
                continue;

            bindings.Add(CreateBinding(source));
        }
    }

    void RefreshSourceBindingsIfNeeded()
    {
        nextSourceRefreshTime = Time.time + SourceRefreshInterval;
        if (NeedsRebuild())
            RebuildBindings();
    }

    bool CanUseSource(SpriteRenderer source)
    {
        if (source == null ||
            source.sprite == null ||
            source.GetComponent<ToxicBorderExposureOutlineLayer>() != null ||
            source.GetComponentInParent<Canvas>() != null)
        {
            return false;
        }

        return true;
    }

    bool NeedsRebuild()
    {
        SpriteRenderer[] sources = GetComponentsInChildren<SpriteRenderer>(true);
        int usableCount = 0;
        for (int i = 0; i < sources.Length && usableCount < MaxOutlinedRenderers; i++)
        {
            SpriteRenderer source = sources[i];
            if (!CanUseSource(source))
                continue;

            usableCount++;
            if (!HasBindingFor(source))
                return true;
        }

        return usableCount != bindings.Count;
    }

    bool HasBindingFor(SpriteRenderer source)
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            if (bindings[i].Source == source)
                return true;
        }

        return false;
    }

    OutlineBinding CreateBinding(SpriteRenderer source)
    {
        OutlineBinding binding = new OutlineBinding
        {
            Source = source,
            Halo = CreateLayer(source, "Halo"),
            Edges = new SpriteRenderer[EdgeDirections.Length]
        };

        for (int i = 0; i < binding.Edges.Length; i++)
            binding.Edges[i] = CreateLayer(source, "Edge_" + i);

        return binding;
    }

    SpriteRenderer CreateLayer(SpriteRenderer source, string layerName)
    {
        GameObject layerObject = new GameObject("ToxicBorderExposureOutline_" + layerName);
        layerObject.transform.SetParent(source.transform, false);
        layerObject.AddComponent<ToxicBorderExposureOutlineLayer>();

        SpriteRenderer layer = layerObject.AddComponent<SpriteRenderer>();
        layer.enabled = false;
        CopyStaticRendererState(source, layer, 0);
        return layer;
    }

    void RefreshLayers()
    {
        float pulse = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(Time.time * PulseSpeed + phase));
        Color edgeColor = new Color(0.42f, 1f, 0.12f, BaseEdgeAlpha + PulseEdgeAlpha * pulse);
        Color haloColor = new Color(0.18f, 0.92f, 0.16f, BaseHaloAlpha + PulseHaloAlpha * pulse);
        float scalePulse = 1f + pulse * 0.035f;
        bool anySourceVisible = false;
        Bounds combinedBounds = default;

        for (int i = bindings.Count - 1; i >= 0; i--)
        {
            OutlineBinding binding = bindings[i];
            if (binding.Source == null || binding.Source.sprite == null)
            {
                DestroyBinding(binding);
                bindings.RemoveAt(i);
                continue;
            }

            bool sourceVisible = visible &&
                                 binding.Source.enabled &&
                                 binding.Source.gameObject.activeInHierarchy;
            if (sourceVisible)
            {
                if (!anySourceVisible)
                {
                    combinedBounds = binding.Source.bounds;
                    anySourceVisible = true;
                }
                else
                {
                    combinedBounds.Encapsulate(binding.Source.bounds);
                }
            }

            float localOffset = ResolveLocalOffset(binding.Source);

            RefreshLayer(
                binding.Source,
                binding.Halo,
                Vector2.zero,
                HaloScale + pulse * 0.035f,
                haloColor,
                sourceVisible,
                HaloSortingOffset);

            for (int edgeIndex = 0; edgeIndex < binding.Edges.Length; edgeIndex++)
            {
                Vector2 offset = EdgeDirections[edgeIndex] * localOffset;
                RefreshLayer(
                    binding.Source,
                    binding.Edges[edgeIndex],
                    offset,
                    EdgeScale * scalePulse,
                    edgeColor,
                    sourceVisible,
                    EdgeSortingOffset);
            }
        }

        RefreshExposureAura(anySourceVisible, combinedBounds, pulse);
    }

    static float ResolveLocalOffset(SpriteRenderer source)
    {
        if (source == null || source.sprite == null)
            return MinLocalOffset;

        Vector3 size = source.sprite.bounds.size;
        float maxSize = Mathf.Max(size.x, size.y);
        return Mathf.Clamp(maxSize * LocalOffsetRatio, MinLocalOffset, MaxLocalOffset);
    }

    void RefreshLayer(
        SpriteRenderer source,
        SpriteRenderer layer,
        Vector2 localOffset,
        float localScale,
        Color color,
        bool sourceVisible,
        int sortingOffset)
    {
        if (layer == null)
            return;

        if (!sourceVisible)
        {
            layer.enabled = false;
            return;
        }

        CopyStaticRendererState(source, layer, sortingOffset);

        Color finalColor = color;
        finalColor.a *= source.color.a;
        layer.color = finalColor;
        layer.transform.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);
        layer.transform.localRotation = Quaternion.identity;
        layer.transform.localScale = Vector3.one * localScale;
        layer.enabled = true;
    }

    static void CopyStaticRendererState(SpriteRenderer source, SpriteRenderer layer, int sortingOffset)
    {
        layer.sprite = source.sprite;
        layer.flipX = source.flipX;
        layer.flipY = source.flipY;
        layer.drawMode = source.drawMode;
        layer.size = source.size;
        layer.tileMode = source.tileMode;
        layer.maskInteraction = source.maskInteraction;
        layer.sortingLayerID = source.sortingLayerID;
        layer.sortingOrder = source.sortingOrder + sortingOffset;
        layer.sharedMaterial = source.sharedMaterial;
    }

    void RefreshExposureAura(bool sourceVisible, Bounds bounds, float pulse)
    {
        if (!sourceVisible)
        {
            SetAuraVisibility(false);
            return;
        }

        EnsureAuraRenderers();

        SpriteRenderer reference = GetPrimarySourceRenderer();
        Vector3 center = bounds.center;
        float baseWidth = Mathf.Max(MinAuraSize, bounds.size.x * AuraPadding);
        float baseHeight = Mathf.Max(MinAuraSize, bounds.size.y * AuraPadding);

        for (int i = 0; i < auraRenderers.Length; i++)
        {
            SpriteRenderer aura = auraRenderers[i];
            if (aura == null)
                continue;

            if (reference != null)
            {
                aura.sortingLayerID = reference.sortingLayerID;
                aura.sortingOrder = Mathf.Max(
                    reference.sortingOrder + AuraSortingOffset + i,
                    GameVisualTheme.PlayerSortingOrder + AuraSortingOffset + i);
            }
            else
            {
                aura.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
                aura.sortingOrder = GameVisualTheme.PlayerSortingOrder + AuraSortingOffset + i;
            }

            float layerPulse = Mathf.Sin(Time.time * (PulseSpeed * 0.56f + i * 0.27f) + phase + i * 1.71f) * 0.5f + 0.5f;
            float driftX = Mathf.Sin(Time.time * (0.73f + i * 0.19f) + phase + i) * AuraDrift * (i + 1);
            float driftY = Mathf.Cos(Time.time * (0.61f + i * 0.17f) + phase * 0.41f + i) * AuraDrift * (i + 1);
            float scale = AuraLayerScales[i] * (1f + layerPulse * 0.045f);
            float alpha = AuraLayerAlpha[i] + AuraLayerPulse[i] * pulse;

            Color color = AuraLayerColors[i];
            color.a = alpha;
            aura.color = color;
            aura.transform.position = new Vector3(center.x + driftX, center.y + driftY, transform.position.z);
            aura.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * AuraLayerRotationSpeed[i] + phase * Mathf.Rad2Deg);
            ApplyWorldScale(aura.transform, baseWidth * scale, baseHeight * scale);
            aura.enabled = true;
        }
    }

    void EnsureAuraRenderers()
    {
        for (int i = 0; i < auraRenderers.Length; i++)
        {
            if (auraRenderers[i] != null)
                continue;

            GameObject auraObject = new GameObject("ToxicBorderExposureAura_" + i);
            auraObject.transform.SetParent(transform, false);
            auraObject.AddComponent<ToxicBorderExposureOutlineLayer>();

            SpriteRenderer aura = auraObject.AddComponent<SpriteRenderer>();
            aura.sprite = GetAuraSprite(i);
            aura.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            aura.sortingOrder = GameVisualTheme.PlayerSortingOrder + AuraSortingOffset + i;
            aura.enabled = false;
            auraRenderers[i] = aura;
        }
    }

    void SetAuraVisibility(bool active)
    {
        for (int i = 0; i < auraRenderers.Length; i++)
        {
            if (auraRenderers[i] != null)
                auraRenderers[i].enabled = active;
        }
    }

    void DestroyAuraRenderers()
    {
        for (int i = 0; i < auraRenderers.Length; i++)
        {
            if (auraRenderers[i] != null)
                Destroy(auraRenderers[i].gameObject);

            auraRenderers[i] = null;
        }
    }

    static void ApplyWorldScale(Transform target, float worldWidth, float worldHeight)
    {
        Vector3 parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
        float scaleX = Mathf.Abs(parentScale.x) > 0.0001f ? worldWidth / Mathf.Abs(parentScale.x) : worldWidth;
        float scaleY = Mathf.Abs(parentScale.y) > 0.0001f ? worldHeight / Mathf.Abs(parentScale.y) : worldHeight;
        target.localScale = new Vector3(scaleX, scaleY, 1f);
    }

    static Sprite GetAuraSprite(int layerIndex)
    {
        layerIndex = Mathf.Clamp(layerIndex, 0, AuraLayerCount - 1);
        if (AuraSprites[layerIndex] != null)
            return AuraSprites[layerIndex];

        Texture2D texture = new Texture2D(AuraTextureSize, AuraTextureSize, TextureFormat.RGBA32, false)
        {
            name = "ToxicBorderExposureAuraTexture_" + layerIndex,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float seed = 31.7f + layerIndex * 18.29f;
        for (int y = 0; y < AuraTextureSize; y++)
        {
            float ny = ((y + 0.5f) / AuraTextureSize) * 2f - 1f;
            for (int x = 0; x < AuraTextureSize; x++)
            {
                float nx = ((x + 0.5f) / AuraTextureSize) * 2f - 1f;
                texture.SetPixel(x, y, ResolveAuraPixel(nx, ny, seed, layerIndex));
            }
        }

        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, AuraTextureSize, AuraTextureSize),
            new Vector2(0.5f, 0.5f),
            AuraTextureSize);
        sprite.name = "ToxicBorderExposureAuraSprite_" + layerIndex;
        AuraSprites[layerIndex] = sprite;
        return sprite;
    }

    static Color ResolveAuraPixel(float nx, float ny, float seed, int layerIndex)
    {
        float radius = Mathf.Sqrt(nx * nx + ny * ny);
        if (radius > 1f)
            return Color.clear;

        float angle = Mathf.Atan2(ny, nx);
        float broadNoise = AuraNoise(nx * 2.1f + seed, ny * 2.1f - seed * 0.37f, seed);
        float detailNoise = AuraNoise(nx * 7.2f - seed * 0.19f, ny * 6.6f + seed * 0.23f, seed + 11.3f);
        float ribbon = 1f - Mathf.Abs(Mathf.Sin(angle * (3.4f + layerIndex * 1.3f) + broadNoise * 3.1f + seed));
        float wisps = SmoothStep01(0.42f, 0.92f, ribbon) * SmoothStep01(0.38f, 0.88f, detailNoise);
        float warpedRadius = radius + (broadNoise - 0.5f) * 0.13f + (detailNoise - 0.5f) * 0.045f;
        float inner = SmoothStep01(0.28f + layerIndex * 0.035f, 0.58f + layerIndex * 0.025f, warpedRadius);
        float outer = 1f - SmoothStep01(0.73f, 1f, warpedRadius);
        float cloudyRing = inner * outer * Mathf.Lerp(0.45f, 1.08f, broadNoise);
        float centerHaze = (1f - SmoothStep01(0.16f, 0.68f, radius)) * Mathf.Lerp(0.05f, 0.13f, broadNoise);
        float alpha = Mathf.Clamp01(cloudyRing + centerHaze + wisps * 0.22f);
        alpha *= Mathf.Lerp(0.68f, 1f, 1f - layerIndex * 0.18f);
        return new Color(1f, 1f, 1f, alpha);
    }

    static float AuraNoise(float x, float y, float seed)
    {
        float a = Mathf.PerlinNoise(x + seed, y - seed * 0.31f);
        float b = Mathf.PerlinNoise(x * 2.17f - seed * 0.17f, y * 1.93f + seed * 0.23f);
        float c = Mathf.PerlinNoise(x * 4.03f + seed * 0.07f, y * 3.81f - seed * 0.11f);
        return Mathf.Clamp01(a * 0.6f + b * 0.28f + c * 0.12f);
    }

    static float SmoothStep01(float edge0, float edge1, float value)
    {
        if (Mathf.Abs(edge1 - edge0) <= 0.0001f)
            return value >= edge1 ? 1f : 0f;

        float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    SpriteRenderer GetPrimarySourceRenderer()
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            SpriteRenderer source = bindings[i].Source;
            if (source != null)
                return source;
        }

        return null;
    }

    void SetLayerVisibility(bool active)
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            OutlineBinding binding = bindings[i];
            if (binding.Halo != null)
                binding.Halo.enabled = active;

            if (binding.Edges == null)
                continue;

            for (int edgeIndex = 0; edgeIndex < binding.Edges.Length; edgeIndex++)
            {
                if (binding.Edges[edgeIndex] != null)
                    binding.Edges[edgeIndex].enabled = active;
            }
        }

        SetAuraVisibility(active);
    }

    void ClearBindings()
    {
        for (int i = 0; i < bindings.Count; i++)
            DestroyBinding(bindings[i]);

        bindings.Clear();
    }

    static void DestroyBinding(OutlineBinding binding)
    {
        if (binding == null)
            return;

        if (binding.Halo != null)
            Destroy(binding.Halo.gameObject);

        if (binding.Edges == null)
            return;

        for (int i = 0; i < binding.Edges.Length; i++)
        {
            if (binding.Edges[i] != null)
                Destroy(binding.Edges[i].gameObject);
        }
    }

    sealed class OutlineBinding
    {
        public SpriteRenderer Source;
        public SpriteRenderer Halo;
        public SpriteRenderer[] Edges;
    }
}

sealed class ToxicBorderExposureOutlineLayer : MonoBehaviour
{
}
