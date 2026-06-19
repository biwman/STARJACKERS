using UnityEngine;

public sealed class EnemySpriteFrameAnimator : MonoBehaviour
{
    static readonly System.Collections.Generic.Dictionary<string, Sprite[]> FrameCacheByResourcePath = new System.Collections.Generic.Dictionary<string, Sprite[]>(System.StringComparer.Ordinal);

    SpriteRenderer targetRenderer;
    Sprite[] frames = System.Array.Empty<Sprite>();
    string loadedResourcePath;
    float framesPerSecond = 7f;
    float speedMultiplier = 1f;
    float frameCursor;

    public void Configure(SpriteRenderer renderer, string resourcePath, float fps)
    {
        targetRenderer = renderer;
        framesPerSecond = Mathf.Max(0.1f, fps);

        if (!string.Equals(loadedResourcePath, resourcePath, System.StringComparison.Ordinal))
        {
            loadedResourcePath = resourcePath;
            frames = LoadFrames(resourcePath);
            frameCursor = frames.Length > 0 ? Random.Range(0f, frames.Length) : 0f;
        }
    }

    public void SetSpeedMultiplier(float value)
    {
        speedMultiplier = Mathf.Clamp(value, 0.15f, 3.5f);
    }

    public static Sprite[] PrewarmFrames(string resourcePath)
    {
        return LoadFrames(resourcePath);
    }

    void LateUpdate()
    {
        if (targetRenderer == null || frames == null || frames.Length == 0)
            return;

        frameCursor += Time.deltaTime * framesPerSecond * speedMultiplier;
        int frameIndex = Mathf.FloorToInt(frameCursor) % frames.Length;
        if (frameIndex < 0)
            frameIndex += frames.Length;

        targetRenderer.sprite = frames[frameIndex];
    }

    static Sprite[] LoadFrames(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return System.Array.Empty<Sprite>();

        if (FrameCacheByResourcePath.TryGetValue(resourcePath, out Sprite[] cachedFrames))
            return cachedFrames;

        Sprite[] allSprites = Resources.LoadAll<Sprite>(resourcePath);
        System.Collections.Generic.List<Sprite> candidates = new System.Collections.Generic.List<Sprite>();
        System.Collections.Generic.List<Sprite> flapCandidates = new System.Collections.Generic.List<Sprite>();
        if (allSprites != null)
        {
            for (int i = 0; i < allSprites.Length; i++)
                AddAnimationCandidate(allSprites[i], candidates, flapCandidates);
        }

        if (candidates.Count == 0)
        {
            Texture2D[] textures = Resources.LoadAll<Texture2D>(resourcePath);
            if (textures != null)
            {
                for (int i = 0; i < textures.Length; i++)
                {
                    Sprite sprite = CreateSpriteFromTexture(textures[i]);
                    AddAnimationCandidate(sprite, candidates, flapCandidates);
                }
            }
        }

        System.Collections.Generic.List<Sprite> selected = flapCandidates.Count > 0 ? flapCandidates : candidates;
        selected.Sort(CompareSpritesForAnimation);
        Sprite[] frames = selected.ToArray();
        for (int i = 0; i < frames.Length; i++)
            PrewarmSpriteTexture(frames[i]);

        FrameCacheByResourcePath[resourcePath] = frames;
        return frames;
    }

    static void AddAnimationCandidate(Sprite sprite, System.Collections.Generic.List<Sprite> candidates, System.Collections.Generic.List<Sprite> flapCandidates)
    {
        if (sprite == null)
            return;

        string name = sprite.name ?? string.Empty;
        if (name.IndexOf("wreck", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        candidates.Add(sprite);
        if (name.IndexOf("flap", System.StringComparison.OrdinalIgnoreCase) >= 0)
            flapCandidates.Add(sprite);
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
}
