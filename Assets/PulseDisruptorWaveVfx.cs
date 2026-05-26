using UnityEngine;

public sealed class PulseDisruptorWaveVfx : MonoBehaviour
{
    const float EffectZ = -0.36f;
    const int RingSegments = 72;
    const int SpokeCount = 18;

    static Material sharedMaterial;

    LineRenderer[] rings;
    LineRenderer[] spokes;
    float maxRadius;
    float duration;
    float elapsed;
    int sortingLayerId;
    int sortingOrder;

    public static void Spawn(Vector3 position, float radius, float duration)
    {
        GameObject root = new GameObject("PulseDisruptorWaveVfx");
        root.transform.position = new Vector3(position.x, position.y, EffectZ);
        PulseDisruptorWaveVfx vfx = root.AddComponent<PulseDisruptorWaveVfx>();
        vfx.Configure(radius, duration);
    }

    void Configure(float radius, float configuredDuration)
    {
        maxRadius = Mathf.Max(0.5f, radius);
        duration = Mathf.Clamp(configuredDuration, 0.2f, 2.5f);
        sortingLayerId = ResolveForegroundSortingLayerId();
        sortingOrder = 6900;

        rings = new LineRenderer[3];
        for (int i = 0; i < rings.Length; i++)
        {
            rings[i] = CreateLine("PulseWaveRing_" + i);
            rings[i].loop = true;
            rings[i].positionCount = RingSegments;
        }

        spokes = new LineRenderer[SpokeCount];
        for (int i = 0; i < spokes.Length; i++)
        {
            spokes[i] = CreateLine("PulseWaveSpoke_" + i);
            spokes[i].positionCount = 2;
        }
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
        float eased = 1f - Mathf.Pow(1f - t, 2.2f);
        float radius = Mathf.Lerp(0.15f, maxRadius, eased);
        float alpha = 1f - t;

        UpdateRing(rings[0], radius, 0.075f, new Color(0.32f, 0.96f, 1f, 0.78f * alpha), 0.08f);
        UpdateRing(rings[1], radius * 0.72f, 0.04f, new Color(0.76f, 0.98f, 1f, 0.46f * alpha), 0.05f);
        UpdateRing(rings[2], radius * 0.38f, 0.028f, new Color(0.18f, 0.58f, 1f, 0.32f * alpha), 0.035f);
        UpdateSpokes(radius, alpha, t);

        if (elapsed >= duration)
            Destroy(gameObject);
    }

    void UpdateRing(LineRenderer ring, float radius, float width, Color color, float wobble)
    {
        if (ring == null)
            return;

        ring.widthMultiplier = width;
        ring.startColor = color;
        ring.endColor = new Color(color.r, color.g, color.b, 0f);
        ring.sortingLayerID = sortingLayerId;
        ring.sortingOrder = sortingOrder;

        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = (i / (float)ring.positionCount) * Mathf.PI * 2f;
            float localRadius = radius * (1f + Mathf.Sin(angle * 5f + elapsed * 14f) * wobble);
            ring.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle) * localRadius, Mathf.Sin(angle) * localRadius, 0f));
        }
    }

    void UpdateSpokes(float radius, float alpha, float t)
    {
        for (int i = 0; i < spokes.Length; i++)
        {
            LineRenderer spoke = spokes[i];
            if (spoke == null)
                continue;

            float angle = ((i / (float)spokes.Length) * Mathf.PI * 2f) + Mathf.Sin(elapsed * 3f + i) * 0.08f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            float inner = Mathf.Max(0.1f, radius * Mathf.Lerp(0.1f, 0.72f, t));
            float outer = radius * Mathf.Lerp(0.5f, 1f, t);
            spoke.SetPosition(0, transform.position + direction * inner);
            spoke.SetPosition(1, transform.position + direction * outer);
            spoke.widthMultiplier = Mathf.Lerp(0.018f, 0.006f, t);
            spoke.startColor = new Color(0.6f, 0.96f, 1f, 0.35f * alpha);
            spoke.endColor = new Color(0.1f, 0.42f, 1f, 0f);
            spoke.sortingLayerID = sortingLayerId;
            spoke.sortingOrder = sortingOrder - 1;
        }
    }

    LineRenderer CreateLine(string objectName)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 3;
        line.numCornerVertices = 2;
        line.material = GetSharedMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
    }

    static Material GetSharedMaterial()
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        sharedMaterial = new Material(shader);
        sharedMaterial.renderQueue = 5000;
        return sharedMaterial;
    }

    static int ResolveForegroundSortingLayerId()
    {
        string[] preferredLayers = { "Bullets", "Player", "Walls", "Ground" };
        SortingLayer[] layers = SortingLayer.layers;
        for (int preferredIndex = 0; preferredIndex < preferredLayers.Length; preferredIndex++)
        {
            string preferredName = preferredLayers[preferredIndex];
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == preferredName)
                    return layers[i].id;
            }
        }

        return 0;
    }
}
