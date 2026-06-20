using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public static class RuntimeSpriteUtility
{
    static readonly Dictionary<string, Sprite> SpriteCacheByKey = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    static Sprite arrowSprite;

    public static Sprite LoadSprite(string resourcePath, string editorAssetPath)
    {
        string cacheKey = BuildCacheKey(resourcePath, editorAssetPath);
        if (SpriteCacheByKey.TryGetValue(cacheKey, out Sprite cachedSprite))
            return cachedSprite;

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            sprite = GetLargestSprite(sprites);
        }

        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(100f, Mathf.Max(texture.width, texture.height)));
        }

#if UNITY_EDITOR
        if (sprite == null && !string.IsNullOrWhiteSpace(editorAssetPath))
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(editorAssetPath);
#endif

        SpriteCacheByKey[cacheKey] = sprite;
        PrewarmSpriteTexture(sprite);
        return sprite;
    }

    public static void PrewarmSprites(params string[] resourcePaths)
    {
        if (resourcePaths == null)
            return;

        for (int i = 0; i < resourcePaths.Length; i++)
            LoadSprite(resourcePaths[i], string.Empty);
    }

    public static void FitRenderer(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Vector2 size = renderer.sprite.bounds.size;
        float largest = Mathf.Max(size.x, size.y);
        if (largest <= 0.001f)
            return;

        float scale = Mathf.Max(0.01f, targetSize / largest);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    public static void FitRendererWorldSize(SpriteRenderer renderer, float targetWorldSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Transform parent = renderer.transform.parent;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        float parentMaxScale = Mathf.Max(Mathf.Abs(parentScale.x), Mathf.Abs(parentScale.y));
        if (parentMaxScale <= 0.0001f)
            parentMaxScale = 1f;

        FitRenderer(renderer, targetWorldSize / parentMaxScale);
    }

    public static Sprite CreateArrowSprite()
    {
        if (arrowSprite != null)
            return arrowSprite;

        Texture2D texture = new Texture2D(64, 64, TextureFormat.ARGB32, false);
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        for (int y = 6; y < 58; y++)
        {
            float t = y / 63f;
            float halfWidth = y < 34 ? Mathf.Lerp(6f, 24f, t / 0.54f) : Mathf.Lerp(16f, 7f, (t - 0.54f) / 0.38f);
            int minX = Mathf.RoundToInt(32f - halfWidth);
            int maxX = Mathf.RoundToInt(32f + halfWidth);
            for (int x = minX; x <= maxX; x++)
            {
                if (x >= 0 && x < texture.width)
                    texture.SetPixel(x, y, Color.white);
            }
        }

        texture.Apply();
        texture.name = "RuntimeGuidanceArrow";
        arrowSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 64f);
        return arrowSprite;
    }

    static string BuildCacheKey(string resourcePath, string editorAssetPath)
    {
        return (resourcePath ?? string.Empty) + "|" + (editorAssetPath ?? string.Empty);
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
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
}
