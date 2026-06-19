using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PirateBaseLaunchVfx : MonoBehaviour
{
    const float HatchOpenDuration = 2f;
    const float HatchLaunchHoldDuration = 3f;
    const float HatchCloseDuration = 2f;
    const float HatchTargetSizeFactor = 0.37f;
    const int SortingOrderOffset = 8;

    static Sprite[] hatchFrames;

    EnemyBot baseBot;
    SpriteRenderer hatchRenderer;
    int sortingLayerId;
    int sortingOrder;

    public static void Prewarm()
    {
        Sprite[] frames = GetHatchFrames();
        for (int i = 0; i < frames.Length; i++)
            PrewarmSpriteTexture(frames[i]);
    }

    public static void Play(EnemyBot source, EnemyBotKind launchedFighterKind)
    {
        if (source == null)
            return;

        GameObject effect = new GameObject("PirateBaseLaunchVfx");
        effect.transform.SetParent(source.transform, false);
        effect.transform.localPosition = new Vector3(0f, 0f, -0.04f);
        effect.transform.localRotation = Quaternion.identity;
        PirateBaseLaunchVfx vfx = effect.AddComponent<PirateBaseLaunchVfx>();
        vfx.Initialize(source, launchedFighterKind);
    }

    void Initialize(EnemyBot source, EnemyBotKind launchedFighterKind)
    {
        baseBot = source;

        SpriteRenderer baseRenderer = source.GetComponent<SpriteRenderer>();
        if (baseRenderer != null)
        {
            sortingLayerId = baseRenderer.sortingLayerID;
            sortingOrder = baseRenderer.sortingOrder + SortingOrderOffset;
        }
        else
        {
            sortingLayerId = SortingLayer.NameToID(GameVisualTheme.WorldSortingLayerName);
            sortingOrder = GameVisualTheme.EnemySortingOrder + SortingOrderOffset;
        }

        hatchRenderer = CreateRenderer("PirateBaseHatch", sortingOrder);
        StartCoroutine(PlayRoutine());
    }

    SpriteRenderer CreateRenderer(string childName, int order)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = Vector3.zero;
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = order;
        renderer.color = Color.white;
        return renderer;
    }

    IEnumerator PlayRoutine()
    {
        Sprite[] frames = GetHatchFrames();
        if (frames.Length > 0)
        {
            float openFrameDuration = HatchOpenDuration / frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                ApplyHatchFrame(frames[i]);
                yield return new WaitForSeconds(openFrameDuration);
            }
        }
        else
        {
            yield return new WaitForSeconds(HatchOpenDuration);
        }

        yield return new WaitForSeconds(HatchLaunchHoldDuration);

        if (frames.Length > 0)
        {
            float closeFrameDuration = HatchCloseDuration / frames.Length;
            for (int i = frames.Length - 1; i >= 0; i--)
            {
                ApplyHatchFrame(frames[i]);
                yield return new WaitForSeconds(closeFrameDuration);
            }
        }
        else
        {
            yield return new WaitForSeconds(HatchCloseDuration);
        }

        Destroy(gameObject);
    }

    void ApplyHatchFrame(Sprite frame)
    {
        if (hatchRenderer == null || frame == null || baseBot == null)
            return;

        hatchRenderer.enabled = true;
        hatchRenderer.sprite = frame;
        FitRendererToWorldSize(hatchRenderer, Mathf.Max(1f, baseBot.VisualTargetSize * HatchTargetSizeFactor));
    }

    void FitRendererToWorldSize(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds spriteBounds = renderer.sprite.bounds;
        float largestDimension = Mathf.Max(spriteBounds.size.x, spriteBounds.size.y);
        if (largestDimension <= 0.0001f)
            return;

        float worldScale = targetSize / largestDimension;
        Vector3 parentScale = renderer.transform.parent != null ? renderer.transform.parent.lossyScale : Vector3.one;
        float safeX = Mathf.Abs(parentScale.x) > 0.0001f ? Mathf.Abs(parentScale.x) : 1f;
        float safeY = Mathf.Abs(parentScale.y) > 0.0001f ? Mathf.Abs(parentScale.y) : 1f;
        renderer.transform.localScale = new Vector3(worldScale / safeX, worldScale / safeY, 1f);
    }

    static Sprite[] GetHatchFrames()
    {
        if (hatchFrames != null)
            return hatchFrames;

        List<Sprite> frames = new List<Sprite>(9);
        for (int i = 1; i <= 9; i++)
        {
            Texture2D texture = Resources.Load<Texture2D>($"PirateBaseOpening/pirate_base_opening_{i:00}");
            if (texture == null)
                continue;

            float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
            frames.Add(Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit));
        }

        hatchFrames = frames.ToArray();
        return hatchFrames;
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }
}
