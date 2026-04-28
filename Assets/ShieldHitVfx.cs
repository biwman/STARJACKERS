using UnityEngine;

public sealed class ShieldHitVfx : MonoBehaviour
{
    const float Lifetime = 0.58f;
    const float EffectZ = -0.35f;

    static Material lineMaterial;

    LineRenderer outerGlow;
    LineRenderer innerGlow;
    LineRenderer ripple;
    Vector3 center;
    float baseRadius = 0.65f;
    float age;

    public static void Spawn(Vector3 position, SpriteRenderer referenceRenderer = null)
    {
        GameObject effect = new GameObject("ShieldHitVfx");
        ShieldHitVfx vfx = effect.AddComponent<ShieldHitVfx>();
        vfx.Initialize(position, referenceRenderer);
    }

    void Initialize(Vector3 position, SpriteRenderer referenceRenderer)
    {
        center = new Vector3(position.x, position.y, EffectZ);
        transform.position = center;

        if (referenceRenderer != null)
        {
            baseRadius = Mathf.Clamp(Mathf.Max(referenceRenderer.bounds.extents.x, referenceRenderer.bounds.extents.y) * 0.74f, 0.42f, 1.2f);
        }

        int sortingLayerId = referenceRenderer != null ? referenceRenderer.sortingLayerID : 0;
        int sortingOrder = referenceRenderer != null ? referenceRenderer.sortingOrder + 140 : 1800;
        outerGlow = CreateRing("ShieldHitOuterGlow", 64, 0.18f, sortingLayerId, sortingOrder);
        innerGlow = CreateRing("ShieldHitInnerGlow", 64, 0.07f, sortingLayerId, sortingOrder + 1);
        ripple = CreateRing("ShieldHitRipple", 48, 0.035f, sortingLayerId, sortingOrder + 2);
        UpdateRings(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Lifetime);
        UpdateRings(t);

        if (age >= Lifetime)
            Destroy(gameObject);
    }

    LineRenderer CreateRing(string objectName, int segments, float width, int sortingLayerId, int sortingOrder)
    {
        GameObject ringObject = new GameObject(objectName);
        ringObject.transform.SetParent(transform, false);

        LineRenderer line = ringObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.widthMultiplier = width;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 12;
        line.numCornerVertices = 8;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetLineMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
    }

    void UpdateRings(float t)
    {
        float flash = 1f - Mathf.SmoothStep(0.02f, 1f, t);
        float bloom = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);

        UpdateRing(
            outerGlow,
            Mathf.Lerp(baseRadius * 0.42f, baseRadius * 1.18f, Mathf.SmoothStep(0f, 1f, t)),
            new Color(0.18f, 0.68f, 1f, 0.46f * flash + 0.16f * bloom),
            Mathf.Lerp(0.2f, 0.035f, t),
            0.72f);

        UpdateRing(
            innerGlow,
            Mathf.Lerp(baseRadius * 0.26f, baseRadius * 0.78f, Mathf.SmoothStep(0f, 1f, t)),
            new Color(0.76f, 0.98f, 1f, 0.72f * flash),
            Mathf.Lerp(0.09f, 0.02f, t),
            0.56f);

        UpdateRing(
            ripple,
            Mathf.Lerp(baseRadius * 0.15f, baseRadius * 1.42f, t),
            new Color(0.38f, 0.86f, 1f, 0.34f * (1f - t)),
            Mathf.Lerp(0.05f, 0.01f, t),
            0.7f);
    }

    void UpdateRing(LineRenderer line, float radius, Color color, float width, float verticalScale)
    {
        if (line == null)
            return;

        for (int i = 0; i < line.positionCount; i++)
        {
            float a = (i / (float)line.positionCount) * Mathf.PI * 2f;
            float irregularity = 1f + Mathf.Sin(a * 3.1f + age * 9f) * 0.025f + Mathf.Sin(a * 5.7f) * 0.018f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius * irregularity, Mathf.Sin(a) * radius * verticalScale * irregularity, 0f));
        }

        line.startColor = color;
        line.endColor = color;
        line.widthMultiplier = width;
        line.startWidth = width;
        line.endWidth = width;
    }

    static Material GetLineMaterial()
    {
        if (lineMaterial != null)
            return lineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        lineMaterial = new Material(shader)
        {
            name = "ShieldHitVfxGlowMaterial",
            color = Color.white
        };
        lineMaterial.renderQueue = 3200;
        return lineMaterial;
    }
}
