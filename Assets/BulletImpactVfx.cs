using UnityEngine;

public sealed class BulletImpactVfx : MonoBehaviour
{
    const float RailDuration = 0.22f;
    const float IonDuration = 0.36f;
    const float GatlingDuration = 0.18f;
    const float ArtilleryDuration = 0.56f;
    const float RocketDuration = 0.46f;
    const float DefaultDuration = 0.26f;
    const float EffectZ = -0.35f;

    static Material sharedLineMaterial;

    LineRenderer[] lines;
    float[] baseWidths;
    Color[] startColors;
    Color[] endColors;
    float duration;
    float elapsed;
    float pulseRadius;
    bool expandPulse;
    int sortingLayerId;
    int sortingOrder;

    public static void Spawn(string effectId, Vector3 position, Vector2 direction, float scale)
    {
        GameObject root = new GameObject("BulletImpactVfx_" + (string.IsNullOrWhiteSpace(effectId) ? "default" : effectId));
        root.transform.position = new Vector3(position.x, position.y, EffectZ);
        BulletImpactVfx vfx = root.AddComponent<BulletImpactVfx>();
        vfx.Configure(effectId, direction, Mathf.Max(0.45f, scale));
    }

    void Configure(string effectId, Vector2 direction, float scale)
    {
        sortingLayerId = ResolveForegroundSortingLayerId();
        sortingOrder = 6800;
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        if (string.Equals(effectId, "rail", System.StringComparison.OrdinalIgnoreCase))
        {
            ConfigureRail(direction, scale);
            return;
        }

        if (string.Equals(effectId, "ion", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(effectId, "pulse_disruptor", System.StringComparison.OrdinalIgnoreCase))
        {
            ConfigureIon(direction, scale);
            return;
        }

        if (string.Equals(effectId, "gatling", System.StringComparison.OrdinalIgnoreCase))
        {
            ConfigureGatling(direction, scale);
            return;
        }

        if (string.Equals(effectId, "artillery", System.StringComparison.OrdinalIgnoreCase))
        {
            ConfigureArtillery(direction, scale);
            return;
        }

        if (string.Equals(effectId, "rocket", System.StringComparison.OrdinalIgnoreCase))
        {
            ConfigureRocket(direction, scale);
            return;
        }

        ConfigureDefault(direction, scale);
    }

    void ConfigureRail(Vector2 direction, float scale)
    {
        duration = RailDuration;
        lines = new LineRenderer[7];
        baseWidths = new float[lines.Length];
        startColors = new Color[lines.Length];
        endColors = new Color[lines.Length];

        AddLine(0, Vector2.zero, -direction * (0.58f * scale), 0.07f * scale, new Color(1f, 0.9f, 0.45f, 0.95f), new Color(1f, 0.18f, 0.02f, 0f));
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        for (int i = 1; i < lines.Length; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            float spread = Mathf.Lerp(0.22f, 0.74f, (i - 1) / 5f);
            Vector2 spark = (-direction * Random.Range(0.28f, 0.62f) + tangent * side * spread).normalized;
            AddLine(i, Vector2.zero, spark * Random.Range(0.28f, 0.62f) * scale, Random.Range(0.018f, 0.04f) * scale, new Color(1f, 0.58f, 0.12f, 0.88f), new Color(1f, 0.08f, 0.02f, 0f));
        }
    }

    void ConfigureIon(Vector2 direction, float scale)
    {
        duration = IonDuration;
        expandPulse = true;
        pulseRadius = 0.22f * scale;
        lines = new LineRenderer[7];
        baseWidths = new float[lines.Length];
        startColors = new Color[lines.Length];
        endColors = new Color[lines.Length];

        AddCircle(0, pulseRadius, 0.035f * scale, new Color(0.35f, 0.9f, 1f, 0.8f));
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        for (int i = 1; i < lines.Length; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            Vector2 spark = (-direction * Random.Range(0.15f, 0.42f) + tangent * side * Random.Range(0.24f, 0.76f)).normalized;
            AddLine(i, -spark * 0.04f * scale, spark * Random.Range(0.22f, 0.5f) * scale, Random.Range(0.014f, 0.028f) * scale, new Color(0.4f, 0.95f, 1f, 0.86f), new Color(0.1f, 0.38f, 1f, 0f));
        }
    }

    void ConfigureGatling(Vector2 direction, float scale)
    {
        duration = GatlingDuration;
        lines = new LineRenderer[3];
        baseWidths = new float[lines.Length];
        startColors = new Color[lines.Length];
        endColors = new Color[lines.Length];

        Vector2 tangent = new Vector2(-direction.y, direction.x);
        AddLine(0, -direction * 0.02f * scale, -direction * 0.24f * scale, 0.022f * scale, new Color(1f, 0.96f, 0.58f, 0.8f), new Color(1f, 0.32f, 0.04f, 0f));
        for (int i = 1; i < lines.Length; i++)
        {
            float side = i == 1 ? 1f : -1f;
            Vector2 spark = (-direction * Random.Range(0.16f, 0.34f) + tangent * side * Random.Range(0.22f, 0.52f)).normalized;
            AddLine(i, Vector2.zero, spark * Random.Range(0.12f, 0.26f) * scale, Random.Range(0.008f, 0.018f) * scale, new Color(1f, 0.74f, 0.26f, 0.62f), new Color(0.8f, 0.12f, 0.02f, 0f));
        }
    }

    void ConfigureDefault(Vector2 direction, float scale)
    {
        duration = DefaultDuration;
        lines = new LineRenderer[4];
        baseWidths = new float[lines.Length];
        startColors = new Color[lines.Length];
        endColors = new Color[lines.Length];

        for (int i = 0; i < lines.Length; i++)
        {
            Vector2 spark = Quaternion.Euler(0f, 0f, -35f + i * 23f) * -direction;
            AddLine(i, Vector2.zero, spark.normalized * Random.Range(0.16f, 0.36f) * scale, Random.Range(0.012f, 0.026f) * scale, new Color(0.9f, 0.95f, 1f, 0.64f), new Color(0.35f, 0.7f, 1f, 0f));
        }
    }

    void ConfigureArtillery(Vector2 direction, float scale)
    {
        duration = ArtilleryDuration;
        expandPulse = true;
        pulseRadius = 0.36f * scale;
        lines = new LineRenderer[12];
        baseWidths = new float[lines.Length];
        startColors = new Color[lines.Length];
        endColors = new Color[lines.Length];

        AddCircle(0, pulseRadius, 0.055f * scale, new Color(1f, 0.76f, 0.22f, 0.72f));
        AddCircle(1, pulseRadius * 0.42f, 0.17f * scale, new Color(1f, 0.94f, 0.64f, 0.9f));
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        for (int i = 2; i < lines.Length; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            Vector2 spark = (-direction * UnityEngine.Random.Range(-0.08f, 0.46f) + tangent * side * UnityEngine.Random.Range(0.14f, 1.18f)).normalized;
            float sparkLength = UnityEngine.Random.Range(0.28f, 0.86f) * scale;
            float sparkWidth = UnityEngine.Random.Range(0.018f, 0.045f) * scale;
            AddLine(
                i,
                -spark * UnityEngine.Random.Range(0.02f, 0.06f) * scale,
                spark * sparkLength,
                sparkWidth,
                i % 3 == 0 ? new Color(1f, 0.95f, 0.62f, 0.9f) : new Color(1f, 0.68f, 0.18f, 0.84f),
                new Color(0.82f, 0.14f, 0.03f, 0f));
        }
    }

    void ConfigureRocket(Vector2 direction, float scale)
    {
        duration = RocketDuration;
        expandPulse = true;
        pulseRadius = 0.3f * scale;
        lines = new LineRenderer[10];
        baseWidths = new float[lines.Length];
        startColors = new Color[lines.Length];
        endColors = new Color[lines.Length];

        AddCircle(0, pulseRadius, 0.052f * scale, new Color(1f, 0.82f, 0.26f, 0.76f));
        AddCircle(1, pulseRadius * 0.48f, 0.13f * scale, new Color(1f, 0.96f, 0.66f, 0.88f));
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        for (int i = 2; i < lines.Length; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            Vector2 spark = (-direction * UnityEngine.Random.Range(-0.04f, 0.5f) + tangent * side * UnityEngine.Random.Range(0.16f, 0.96f)).normalized;
            AddLine(
                i,
                -spark * UnityEngine.Random.Range(0.02f, 0.05f) * scale,
                spark * UnityEngine.Random.Range(0.24f, 0.72f) * scale,
                UnityEngine.Random.Range(0.016f, 0.04f) * scale,
                i % 3 == 0 ? new Color(1f, 0.96f, 0.62f, 0.9f) : new Color(1f, 0.48f, 0.12f, 0.82f),
                new Color(0.78f, 0.1f, 0.02f, 0f));
        }
    }

    void AddLine(int index, Vector2 localStart, Vector2 localEnd, float width, Color start, Color end)
    {
        LineRenderer line = CreateLine("ImpactLine_" + index);
        line.positionCount = 2;
        line.SetPosition(0, transform.position + (Vector3)localStart);
        line.SetPosition(1, transform.position + (Vector3)localEnd);
        StoreLine(index, line, width, start, end);
    }

    void AddCircle(int index, float radius, float width, Color color)
    {
        LineRenderer line = CreateLine("ImpactCircle_" + index);
        line.loop = true;
        line.positionCount = 28;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = (i / (float)line.positionCount) * Mathf.PI * 2f;
            line.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        StoreLine(index, line, width, color, new Color(color.r, color.g, color.b, 0f));
    }

    LineRenderer CreateLine(string objectName)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.material = GetSharedLineMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
    }

    void StoreLine(int index, LineRenderer line, float width, Color start, Color end)
    {
        lines[index] = line;
        baseWidths[index] = width;
        startColors[index] = start;
        endColors[index] = end;
        line.widthMultiplier = width;
        line.startColor = start;
        line.endColor = end;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
        float alpha = 1f - t;
        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer line = lines[i];
            if (line == null)
                continue;

            Color start = startColors[i];
            Color end = endColors[i];
            start.a *= alpha;
            end.a *= alpha;
            line.startColor = start;
            line.endColor = end;
            line.widthMultiplier = baseWidths[i] * Mathf.Lerp(1.15f, 0.2f, t);
            line.sortingLayerID = sortingLayerId;
            line.sortingOrder = sortingOrder + i;

            if (expandPulse)
            {
                if (i == 0)
                    UpdateCircle(line, pulseRadius * Mathf.Lerp(1f, 2.35f, t), 0.18f);
                else if (i == 1)
                    UpdateCircle(line, pulseRadius * 0.42f * Mathf.Lerp(1f, 1.55f, t), 0.12f);
            }
        }

        if (elapsed >= duration)
            Destroy(gameObject);
    }

    void UpdateCircle(LineRenderer line, float radius, float wobble = 0f)
    {
        if (line == null)
            return;

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = (i / (float)line.positionCount) * Mathf.PI * 2f;
            float localRadius = radius;
            if (wobble > 0.0001f)
                localRadius *= 1f + Mathf.Sin(angle * 3f + elapsed * 12f + i * 0.41f) * wobble;

            line.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle) * localRadius, Mathf.Sin(angle) * localRadius, 0f));
        }
    }

    static Material GetSharedLineMaterial()
    {
        if (sharedLineMaterial != null)
            return sharedLineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        sharedLineMaterial = new Material(shader);
        sharedLineMaterial.renderQueue = 5000;
        return sharedLineMaterial;
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
