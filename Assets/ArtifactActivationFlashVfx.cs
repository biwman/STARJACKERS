using System.Collections;
using UnityEngine;

public sealed class ArtifactActivationFlashVfx : MonoBehaviour
{
    const float Lifetime = 1.05f;
    const int CircleSegments = 72;

    static Material lineMaterial;
    static Sprite coreSprite;

    LineRenderer outerRing;
    LineRenderer innerRing;
    LineRenderer verticalRay;
    SpriteRenderer coreGlow;
    float radius = 1.8f;
    float elapsed;

    public static void Spawn(Vector3 position, float worldRadius)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject effect = new GameObject("ArtifactActivationFlashVfx");
        effect.transform.position = new Vector3(position.x, position.y, position.z - 0.08f);
        ArtifactActivationFlashVfx vfx = effect.AddComponent<ArtifactActivationFlashVfx>();
        vfx.radius = Mathf.Clamp(worldRadius, 0.8f, 3.6f);
    }

    void Awake()
    {
        outerRing = CreateRing("OuterRing", 0.08f, GameVisualTheme.TreasureSortingOrder + 14);
        innerRing = CreateRing("InnerRing", 0.045f, GameVisualTheme.TreasureSortingOrder + 15);
        verticalRay = CreateLine("CoreRay", 0.07f, GameVisualTheme.TreasureSortingOrder + 16);

        GameObject coreObject = new GameObject("CoreGlow");
        coreObject.transform.SetParent(transform, false);
        coreGlow = coreObject.AddComponent<SpriteRenderer>();
        coreGlow.sprite = GetCoreSprite();
        coreGlow.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        coreGlow.sortingOrder = GameVisualTheme.TreasureSortingOrder + 17;
        coreGlow.color = new Color(0.22f, 0.72f, 1f, 0.85f);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Lifetime);
        float fade = Mathf.Clamp01(1f - t);
        float pulse = Mathf.Sin(t * Mathf.PI);
        float outerRadius = Mathf.Lerp(radius * 0.22f, radius * 1.35f, EaseOutCubic(t));
        float innerRadius = Mathf.Lerp(radius * 0.08f, radius * 0.74f, EaseOutCubic(Mathf.Clamp01(t * 1.2f)));

        UpdateRing(outerRing, outerRadius, 0.78f * fade);
        UpdateRing(innerRing, innerRadius, 0.95f * fade);
        UpdateRay(verticalRay, radius * Mathf.Lerp(0.5f, 1.4f, pulse), 0.62f * fade);

        if (coreGlow != null)
        {
            float coreScale = Mathf.Lerp(radius * 0.26f, radius * 0.06f, t);
            coreGlow.transform.localScale = new Vector3(coreScale, coreScale, 1f);
            coreGlow.color = new Color(0.16f, 0.82f, 1f, 0.75f * fade);
        }

        if (elapsed >= Lifetime)
            Destroy(gameObject);
    }

    LineRenderer CreateRing(string objectName, float width, int sortingOrder)
    {
        LineRenderer ring = CreateLine(objectName, width, sortingOrder);
        ring.loop = true;
        ring.positionCount = CircleSegments;
        return ring;
    }

    LineRenderer CreateLine(string objectName, float width, int sortingOrder)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(transform, false);
        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.alignment = LineAlignment.View;
        line.positionCount = 2;
        line.widthMultiplier = width;
        line.numCapVertices = 10;
        line.numCornerVertices = 8;
        line.material = GetLineMaterial();
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = sortingOrder;
        return line;
    }

    void UpdateRing(LineRenderer ring, float currentRadius, float alpha)
    {
        if (ring == null)
            return;

        ring.colorGradient = BuildGradient(alpha);
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = (i / (float)CircleSegments) * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * currentRadius, Mathf.Sin(angle) * currentRadius, -0.05f));
        }
    }

    void UpdateRay(LineRenderer ray, float currentRadius, float alpha)
    {
        if (ray == null)
            return;

        ray.colorGradient = BuildGradient(alpha);
        ray.SetPosition(0, new Vector3(0f, -currentRadius, -0.06f));
        ray.SetPosition(1, new Vector3(0f, currentRadius, -0.06f));
    }

    Gradient BuildGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.1f, 0.58f, 1f), 0f),
                new GradientColorKey(new Color(0.62f, 0.95f, 1f), 0.5f),
                new GradientColorKey(new Color(0.08f, 0.35f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(alpha, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    static float EaseOutCubic(float t)
    {
        float inverted = 1f - Mathf.Clamp01(t);
        return 1f - inverted * inverted * inverted;
    }

    static Material GetLineMaterial()
    {
        if (lineMaterial != null)
            return lineMaterial;

        lineMaterial = new Material(Shader.Find("Sprites/Default"))
        {
            name = "ArtifactActivationFlashVfxMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
        return lineMaterial;
    }

    static Sprite GetCoreSprite()
    {
        if (coreSprite != null)
            return coreSprite;

        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
        {
            name = "ArtifactActivationCoreTexture",
            hideFlags = HideFlags.HideAndDontSave
        };

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float dx = (x + 0.5f - texture.width * 0.5f) / (texture.width * 0.5f);
                float dy = (y + 0.5f - texture.height * 0.5f) / (texture.height * 0.5f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        coreSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 64f);
        coreSprite.name = "ArtifactActivationCoreSprite";
        coreSprite.hideFlags = HideFlags.HideAndDontSave;
        return coreSprite;
    }
}
