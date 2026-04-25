using UnityEngine;

public sealed class ShieldHitVfx : MonoBehaviour
{
    const float Lifetime = 0.46f;
    const float StartScale = 0.25f;
    const float EndScale = 0.95f;
    const float EffectZ = -0.35f;

    static Material lineMaterial;

    LineRenderer[] lines;
    Vector3[] starts;
    Vector3[] ends;
    float[] baseLineWidths;
    Color[] baseColors;
    Vector3 center;
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

        starts = new[]
        {
            new Vector3(-0.62f, 0f, 0f),
            new Vector3(0f, -0.62f, 0f),
            new Vector3(-0.42f, -0.42f, 0f),
            new Vector3(-0.42f, 0.42f, 0f)
        };
        ends = new[]
        {
            new Vector3(0.62f, 0f, 0f),
            new Vector3(0f, 0.62f, 0f),
            new Vector3(0.42f, 0.42f, 0f),
            new Vector3(0.42f, -0.42f, 0f)
        };
        baseColors = new[]
        {
            new Color(0.82f, 1f, 1f, 1f),
            new Color(0.82f, 1f, 1f, 1f),
            new Color(0.12f, 0.78f, 1f, 1f),
            new Color(0.12f, 0.78f, 1f, 1f)
        };

        int sortingLayerId = referenceRenderer != null ? referenceRenderer.sortingLayerID : 0;
        int sortingOrder = referenceRenderer != null ? referenceRenderer.sortingOrder + 140 : 1800;
        lines = new[]
        {
            CreateLine("ShieldFlashHorizontal", 0.16f, baseColors[0], sortingLayerId, sortingOrder),
            CreateLine("ShieldFlashVertical", 0.16f, baseColors[1], sortingLayerId, sortingOrder),
            CreateLine("ShieldFlashSlashA", 0.1f, baseColors[2], sortingLayerId, sortingOrder + 1),
            CreateLine("ShieldFlashSlashB", 0.1f, baseColors[3], sortingLayerId, sortingOrder + 1)
        };
        baseLineWidths = new float[lines.Length];
        for (int i = 0; i < lines.Length; i++)
            baseLineWidths[i] = lines[i] != null ? lines[i].widthMultiplier : 0.1f;

        UpdateLineGeometry(StartScale, 1f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Lifetime);
        float eased = 1f - Mathf.Pow(1f - t, 2f);

        float scale = Mathf.Lerp(StartScale, EndScale, eased);
        float alpha = Mathf.Lerp(1f, 0f, t);
        float widthScale = Mathf.Lerp(1f, 0.35f, t);
        UpdateLineGeometry(scale, alpha, widthScale);

        if (age >= Lifetime)
            Destroy(gameObject);
    }

    LineRenderer CreateLine(string objectName, float width, Color color, int sortingLayerId, int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = width;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 8;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetLineMaterial();
        line.startColor = color;
        line.endColor = color;
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
    }

    void UpdateLineGeometry(float scale, float alpha, float widthScale = 1f)
    {
        if (lines == null)
            return;

        for (int i = 0; i < lines.Length; i++)
            UpdateLineGeometry(i, scale, alpha, widthScale);
    }

    void UpdateLineGeometry(int index, float scale, float alpha, float widthScale)
    {
        LineRenderer line = lines[index];
        if (line == null)
            return;

        line.SetPosition(0, center + starts[index] * scale);
        line.SetPosition(1, center + ends[index] * scale);

        Color startColor = baseColors[index];
        Color endColor = baseColors[index];
        startColor.a = alpha;
        endColor.a = alpha;
        line.startColor = startColor;
        line.endColor = endColor;
        float width = baseLineWidths[index] * widthScale;
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
            name = "ShieldHitVfxLineMaterial",
            color = Color.white
        };
        lineMaterial.renderQueue = 3200;
        return lineMaterial;
    }
}
