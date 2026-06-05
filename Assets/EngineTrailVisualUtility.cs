using UnityEngine;
using UnityEngine.Rendering;

public static class EngineTrailVisualUtility
{
    static Material sharedSpritesMaterial;
    static Sprite sharedNozzleGlowSprite;

    public static Material GetSpritesMaterial()
    {
        if (sharedSpritesMaterial != null)
            return sharedSpritesMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        sharedSpritesMaterial = new Material(shader)
        {
            name = "EngineTrailSpritesMaterial"
        };
        return sharedSpritesMaterial;
    }

    public static void ConfigureTrailBase(TrailRenderer trail)
    {
        if (trail == null)
            return;

        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.numCapVertices = 3;
        trail.numCornerVertices = 2;
        trail.sharedMaterial = GetSpritesMaterial();
        trail.generateLightingData = false;
    }

    public static void ConfigureLineBase(LineRenderer line)
    {
        if (line == null)
            return;

        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.sharedMaterial = GetSpritesMaterial();
        line.generateLightingData = false;
    }

    public static Gradient BuildEngineGradient(Color core, Color hot, Color body, Color tail)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(core, 0f),
                new GradientColorKey(hot, 0.18f),
                new GradientColorKey(body, 0.54f),
                new GradientColorKey(tail, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.94f, 0f),
                new GradientAlphaKey(0.66f, 0.24f),
                new GradientAlphaKey(0.26f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    public static AnimationCurve BuildShipWidthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.12f, 0.82f),
            new Keyframe(0.6f, 0.3f),
            new Keyframe(1f, 0f));
    }

    public static AnimationCurve BuildSoftWidthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.16f, 0.94f),
            new Keyframe(0.62f, 0.46f),
            new Keyframe(1f, 0f));
    }

    public static Sprite GetNozzleGlowSprite()
    {
        if (sharedNozzleGlowSprite != null)
            return sharedNozzleGlowSprite;

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "EngineNozzleGlowTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                float core = Mathf.Clamp01(1f - distance);
                float alpha = Mathf.Pow(core, 2.3f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        sharedNozzleGlowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        sharedNozzleGlowSprite.name = "EngineNozzleGlowSprite";
        return sharedNozzleGlowSprite;
    }
}
