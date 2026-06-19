using System.Collections.Generic;
using UnityEngine;

public sealed class SpaceAnimalDeathVfx : MonoBehaviour
{
    const float FramesPerSecond = 18f;
    const float FallbackDuration = 0.45f;
    const float EffectZOffset = -0.06f;

    static readonly Dictionary<string, Sprite[]> FrameCacheByResourcePath = new Dictionary<string, Sprite[]>(System.StringComparer.Ordinal);

    SpriteRenderer spriteRenderer;
    Sprite[] frames = System.Array.Empty<Sprite>();
    float targetSize = 2.5f;
    float frameCursor;
    float startedAt;

    public static void Prewarm()
    {
        LoadFrames(ResolveResourcePath(EnemyBotKind.SpaceManta));
        LoadFrames(ResolveResourcePath(EnemyBotKind.GravitySquid));
    }

    public static void Play(EnemyBotKind kind, Vector3 position, float rotationZ, float visualTargetSize)
    {
        GameObject effect = new GameObject("SpaceAnimalDeathVfx_" + kind);
        effect.transform.position = new Vector3(position.x, position.y, position.z + EffectZOffset);
        effect.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
        SpaceAnimalDeathVfx vfx = effect.AddComponent<SpaceAnimalDeathVfx>();
        vfx.Initialize(kind, visualTargetSize);
    }

    void Initialize(EnemyBotKind kind, float visualTargetSize)
    {
        targetSize = Mathf.Max(0.6f, visualTargetSize);
        startedAt = Time.time;
        frames = LoadFrames(ResolveResourcePath(kind));

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = GameVisualTheme.EnemySortingOrder + 4;
        spriteRenderer.color = Color.white;

        if (frames.Length > 0)
        {
            spriteRenderer.sprite = frames[0];
            FitRendererToTargetSize(spriteRenderer, targetSize * 1.12f);
        }

        AudioManager.Instance.PlayAnimalDeathAt(transform.position);
    }

    void Update()
    {
        if (frames == null || frames.Length == 0)
        {
            if (Time.time - startedAt >= FallbackDuration)
                Destroy(gameObject);
            return;
        }

        frameCursor += Time.deltaTime * FramesPerSecond;
        int frameIndex = Mathf.FloorToInt(frameCursor);
        if (frameIndex >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }

        spriteRenderer.sprite = frames[frameIndex];
        FitRendererToTargetSize(spriteRenderer, targetSize * Mathf.Lerp(1.12f, 1.34f, frameIndex / Mathf.Max(1f, frames.Length - 1f)));
    }

    static string ResolveResourcePath(EnemyBotKind kind)
    {
        return kind == EnemyBotKind.GravitySquid
            ? "Enemies/GravitySquid/Death"
            : "Enemies/SpaceManta/Death";
    }

    static Sprite[] LoadFrames(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return System.Array.Empty<Sprite>();

        if (FrameCacheByResourcePath.TryGetValue(resourcePath, out Sprite[] cachedFrames))
            return cachedFrames;

        List<Sprite> result = new List<Sprite>();
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites != null)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                    result.Add(sprites[i]);
            }
        }

        if (result.Count == 0)
        {
            Texture2D[] textures = Resources.LoadAll<Texture2D>(resourcePath);
            if (textures != null)
            {
                for (int i = 0; i < textures.Length; i++)
                {
                    Sprite sprite = CreateSpriteFromTexture(textures[i]);
                    if (sprite != null)
                        result.Add(sprite);
                }
            }
        }

        result.Sort(CompareSpritesForAnimation);
        Sprite[] frames = result.ToArray();
        for (int i = 0; i < frames.Length; i++)
            PrewarmSpriteTexture(frames[i]);

        FrameCacheByResourcePath[resourcePath] = frames;
        return frames;
    }

    static Sprite CreateSpriteFromTexture(Texture2D texture)
    {
        if (texture == null)
            return null;

        float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);
        sprite.name = texture.name;
        return sprite;
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }

    static int CompareSpritesForAnimation(Sprite a, Sprite b)
    {
        int indexA = ExtractTrailingNumber(a != null ? a.name : string.Empty);
        int indexB = ExtractTrailingNumber(b != null ? b.name : string.Empty);
        if (indexA >= 0 && indexB >= 0 && indexA != indexB)
            return indexA.CompareTo(indexB);

        return string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty);
    }

    static int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
            return -1;

        int endIndex = value.Length - 1;
        while (endIndex >= 0 && !char.IsDigit(value[endIndex]))
            endIndex--;

        if (endIndex < 0)
            return -1;

        int startIndex = endIndex;
        while (startIndex >= 0 && char.IsDigit(value[startIndex]))
            startIndex--;

        string numberPart = value.Substring(startIndex + 1, endIndex - startIndex);
        return int.TryParse(numberPart, out int number) ? number : -1;
    }

    static void FitRendererToTargetSize(SpriteRenderer renderer, float size)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds spriteBounds = renderer.sprite.bounds;
        float largest = Mathf.Max(spriteBounds.size.x, spriteBounds.size.y);
        if (largest <= 0.0001f)
            return;

        float scale = size / largest;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
