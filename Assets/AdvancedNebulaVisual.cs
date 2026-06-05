using UnityEngine;

[DisallowMultipleComponent]
public class AdvancedNebulaVisual : MonoBehaviour
{
    const string RootName = "AdvancedNebulaVisualRoot";
    const string BlobName = "NebulaBlob";
    const int TextureSize = 224;
    const float VisualRadiusCoverage = 1.08f;
    const float MinimumLocalDiameter = 1.2f;
    const float BaseAlpha = 0.96f;
    const float PulseAmount = 0.05f;
    const int VisualVersion = 3;

    Transform root;
    SpriteRenderer blobRenderer;
    Sprite blobSprite;
    Texture2D blobTexture;
    float phase;
    int configuredSeed = int.MinValue;
    int configuredVisualVersion = int.MinValue;

    public void Configure(int seed, int baseSortingOrder, float localHideRadius)
    {
        EnsureRoot();
        ApplyRootScale(localHideRadius);
        EnsureRenderer(baseSortingOrder);

        if (configuredSeed != seed || configuredVisualVersion != VisualVersion || blobSprite == null)
        {
            configuredSeed = seed;
            configuredVisualVersion = VisualVersion;
            phase = Mathf.Abs(Mathf.Sin(seed * 0.0137f)) * 24f;
            blobSprite = CreateBlobSprite(seed);
            blobRenderer.sprite = blobSprite;
        }

        ApplyMapTint();
        SetVisible(true);
    }

    public void SetVisible(bool visible)
    {
        EnsureRoot();
        if (root != null)
            root.gameObject.SetActive(visible);
    }

    void Update()
    {
        if (blobRenderer == null || root == null || !root.gameObject.activeSelf)
            return;

        Color color = blobRenderer.color;
        color.a = BaseAlpha + Mathf.Sin(Time.time * 0.85f + phase) * PulseAmount;
        blobRenderer.color = color;
    }

    void EnsureRoot()
    {
        if (root != null)
            return;

        Transform existing = transform.Find(RootName);
        if (existing != null)
        {
            root = existing;
        }
        else
        {
            GameObject rootObject = new GameObject(RootName);
            rootObject.transform.SetParent(transform, false);
            root = rootObject.transform;
        }

        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;
        DisableLegacyLayer("BackHaze");
        DisableLegacyLayer("DepthCloud");
        DisableLegacyLayer("CoreCloud");
        DisableLegacyLayer("SensorDistortion");
        DisableLegacyLayer("InnerDust");
    }

    void DisableLegacyLayer(string layerName)
    {
        if (root == null)
            return;

        Transform existing = root.Find(layerName);
        if (existing != null)
            existing.gameObject.SetActive(false);
    }

    void ApplyRootScale(float localHideRadius)
    {
        if (root == null)
            return;

        float localDiameter = Mathf.Max(MinimumLocalDiameter, localHideRadius * 2f * VisualRadiusCoverage);
        root.localScale = new Vector3(localDiameter, localDiameter, 1f);
    }

    void EnsureRenderer(int baseSortingOrder)
    {
        Transform existing = root.Find(BlobName);
        GameObject blobObject;
        if (existing != null)
        {
            blobObject = existing.gameObject;
        }
        else
        {
            blobObject = new GameObject(BlobName, typeof(SpriteRenderer));
            blobObject.transform.SetParent(root, false);
        }

        blobObject.SetActive(true);
        blobObject.transform.localPosition = Vector3.zero;
        blobObject.transform.localRotation = Quaternion.identity;
        blobObject.transform.localScale = Vector3.one;

        blobRenderer = blobObject.GetComponent<SpriteRenderer>();
        blobRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        blobRenderer.sortingOrder = baseSortingOrder - 1;
    }

    void ApplyMapTint()
    {
        Color primary;
        Color secondary;
        ResolveNebulaTint(out primary, out secondary);

        Color color = Color.Lerp(secondary, primary, 0.72f);
        color.a = BaseAlpha;
        if (blobRenderer != null)
            blobRenderer.color = color;
    }

    static void ResolveNebulaTint(out Color primary, out Color secondary)
    {
        switch (RoomSettings.GetSelectedLobbyMapId())
        {
            case LobbyMapCatalog.MinefieldMapId:
                primary = new Color(0.42f, 0.78f, 0.9f, 1f);
                secondary = new Color(0.16f, 0.26f, 0.32f, 1f);
                break;
            case LobbyMapCatalog.PirateBayMapId:
                primary = new Color(0.46f, 0.84f, 0.76f, 1f);
                secondary = new Color(0.18f, 0.3f, 0.26f, 1f);
                break;
            case LobbyMapCatalog.SnowFieldMapId:
                primary = new Color(0.74f, 0.92f, 1f, 1f);
                secondary = new Color(0.28f, 0.44f, 0.58f, 1f);
                break;
            case LobbyMapCatalog.DeepSpaceMapId:
                primary = new Color(0.5f, 0.68f, 1f, 1f);
                secondary = new Color(0.22f, 0.22f, 0.58f, 1f);
                break;
            case LobbyMapCatalog.AncientSpaceMapId:
                primary = new Color(0.68f, 0.6f, 1f, 1f);
                secondary = new Color(0.28f, 0.2f, 0.5f, 1f);
                break;
            case LobbyMapCatalog.TheThreatMapId:
                primary = new Color(0.74f, 0.48f, 0.95f, 1f);
                secondary = new Color(0.24f, 0.12f, 0.34f, 1f);
                break;
            case LobbyMapCatalog.ToxicAreaMapId:
                primary = new Color(0.56f, 1f, 0.26f, 1f);
                secondary = new Color(0.12f, 0.34f, 0.16f, 1f);
                break;
            default:
                primary = new Color(0.48f, 0.9f, 1f, 1f);
                secondary = new Color(0.16f, 0.34f, 0.56f, 1f);
                break;
        }
    }

    Sprite CreateBlobSprite(int seed)
    {
        if (blobTexture != null)
            Destroy(blobTexture);

        blobTexture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "AdvancedNebulaBlobTexture",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2((TextureSize - 1) * 0.5f, (TextureSize - 1) * 0.5f);
        float seedA = seed * 0.017f;
        float seedB = seed * 0.031f;

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                Vector2 normalized = (new Vector2(x, y) - center) / (TextureSize * 0.5f);
                float angle = Mathf.Atan2(normalized.y, normalized.x);
                float lobe = 1f
                    + Mathf.Sin(angle * 3f + seedA) * 0.11f
                    + Mathf.Sin(angle * 5f + seedB) * 0.075f
                    + Mathf.Sin(angle * 7f + seedA * 0.7f) * 0.045f;

                Vector2 stretched = new Vector2(normalized.x * 0.94f, normalized.y * 1.06f);
                float radius = stretched.magnitude / Mathf.Max(0.76f, lobe);
                float rawRadius = normalized.magnitude;
                float roundSafety = 1f - Mathf.SmoothStep(0.86f, 0.98f, rawRadius);
                float outerMask = (1f - Mathf.SmoothStep(0.7f, 0.95f, radius)) * roundSafety;
                float coreMask = (1f - Mathf.SmoothStep(0.42f, 0.72f, radius)) * roundSafety;
                float edgeMask = Mathf.SmoothStep(0.18f, 0.46f, radius) * outerMask;

                float broadNoise = Mathf.PerlinNoise(x * 0.022f + seedA, y * 0.022f + seedB);
                float detailNoise = Mathf.PerlinNoise(x * 0.07f + seedB, y * 0.07f + seedA);
                float smoke = Mathf.SmoothStep(0.18f, 0.92f, broadNoise * 0.68f + detailNoise * 0.32f);
                float sensorFlecks = detailNoise > 0.74f ? (detailNoise - 0.74f) * 1.8f : 0f;

                float alpha = outerMask * 0.18f;
                alpha += coreMask * Mathf.Lerp(0.5f, 0.86f, smoke);
                alpha += edgeMask * sensorFlecks * 0.14f;
                alpha = Mathf.Clamp01(alpha * (0.86f + broadNoise * 0.22f));

                Color pixel = new Color(1f, 1f, 1f, alpha);
                blobTexture.SetPixel(x, y, pixel);
            }
        }

        blobTexture.Apply(false, true);
        Sprite sprite = Sprite.Create(
            blobTexture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            TextureSize,
            0u,
            SpriteMeshType.FullRect);
        sprite.name = "AdvancedNebulaBlob";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
