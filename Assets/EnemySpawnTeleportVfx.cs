using UnityEngine;

public sealed class EnemySpawnTeleportVfx : MonoBehaviour
{
    const float Lifetime = 0.72f;
    const float EffectZ = -0.34f;
    const int CircleSegments = 72;

    static Material lineMaterial;

    LineRenderer outerRing;
    LineRenderer innerRing;
    LineRenderer[] beams;
    Color outerColor = new Color(0.92f, 1f, 1f, 1f);
    Color innerColor = new Color(0.12f, 0.84f, 1f, 1f);
    Color beamColor = new Color(0.5f, 0.96f, 1f, 1f);
    Vector3 center;
    float baseRadius;
    float age;

    public static void Spawn(Vector3 position, SpriteRenderer referenceRenderer = null, float radius = 1f)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject effect = new GameObject("EnemySpawnTeleportVfx");
        EnemySpawnTeleportVfx vfx = effect.AddComponent<EnemySpawnTeleportVfx>();
        vfx.Initialize(position, referenceRenderer, radius);
    }

    void Initialize(Vector3 position, SpriteRenderer referenceRenderer, float radius)
    {
        center = new Vector3(position.x, position.y, EffectZ);
        transform.position = center;
        baseRadius = Mathf.Clamp(radius, 0.65f, 5.5f);

        int sortingLayerId = referenceRenderer != null ? referenceRenderer.sortingLayerID : 0;
        int sortingOrder = referenceRenderer != null ? referenceRenderer.sortingOrder + 135 : 1800;

        outerRing = CreateLine("TeleportOuterRing", 0.12f, outerColor, sortingLayerId, sortingOrder);
        outerRing.loop = true;
        outerRing.positionCount = CircleSegments;

        innerRing = CreateLine("TeleportInnerRing", 0.08f, innerColor, sortingLayerId, sortingOrder + 1);
        innerRing.loop = true;
        innerRing.positionCount = CircleSegments;

        beams = new[]
        {
            CreateLine("TeleportBeamN", 0.1f, beamColor, sortingLayerId, sortingOrder + 2),
            CreateLine("TeleportBeamS", 0.1f, beamColor, sortingLayerId, sortingOrder + 2),
            CreateLine("TeleportBeamE", 0.1f, beamColor, sortingLayerId, sortingOrder + 2),
            CreateLine("TeleportBeamW", 0.1f, beamColor, sortingLayerId, sortingOrder + 2)
        };

        UpdateVisuals(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Lifetime);
        UpdateVisuals(t);

        if (age >= Lifetime)
            Destroy(gameObject);
    }

    void UpdateVisuals(float t)
    {
        float expand = 1f - Mathf.Pow(1f - t, 3f);
        float pulse = Mathf.Sin(t * Mathf.PI);
        float outerRadius = Mathf.Lerp(baseRadius * 0.38f, baseRadius * 1.28f, expand);
        float innerRadius = Mathf.Lerp(baseRadius * 0.14f, baseRadius * 0.72f, expand);
        float alpha = Mathf.Lerp(1f, 0f, t);

        UpdateCircle(outerRing, outerRadius, alpha, Mathf.Lerp(0.16f, 0.03f, t));
        UpdateCircle(innerRing, innerRadius, alpha * 0.82f, Mathf.Lerp(0.1f, 0.02f, t));
        UpdateBeams(outerRadius, alpha, pulse);
    }

    void UpdateCircle(LineRenderer ring, float radius, float alpha, float width)
    {
        if (ring == null)
            return;

        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / CircleSegments;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            ring.SetPosition(i, point);
        }

        Color start = ring == outerRing ? outerColor : innerColor;
        start.a = alpha;
        ring.startColor = start;
        ring.endColor = start;
        ring.widthMultiplier = width;
        ring.startWidth = width;
        ring.endWidth = width;
    }

    void UpdateBeams(float radius, float alpha, float pulse)
    {
        if (beams == null)
            return;

        Vector3[] directions =
        {
            Vector3.up,
            Vector3.down,
            Vector3.right,
            Vector3.left
        };

        for (int i = 0; i < beams.Length; i++)
        {
            LineRenderer beam = beams[i];
            if (beam == null)
                continue;

            Vector3 direction = directions[i];
            float startDistance = radius * Mathf.Lerp(0.14f, 0.38f, pulse);
            float endDistance = radius * Mathf.Lerp(0.95f, 1.18f, pulse);
            beam.SetPosition(0, center + direction * startDistance);
            beam.SetPosition(1, center + direction * endDistance);

            Color color = beamColor;
            color.a = alpha * 0.86f;
            beam.startColor = color;
            color.a = alpha * 0.15f;
            beam.endColor = color;
            float width = Mathf.Lerp(0.13f, 0.03f, age / Lifetime);
            beam.widthMultiplier = width;
            beam.startWidth = width;
            beam.endWidth = width * 0.5f;
        }
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
        line.numCapVertices = 10;
        line.numCornerVertices = 6;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetLineMaterial();
        line.startColor = color;
        line.endColor = color;
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
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
            name = "EnemySpawnTeleportVfxLineMaterial",
            color = Color.white
        };
        lineMaterial.renderQueue = 3200;
        return lineMaterial;
    }
}
