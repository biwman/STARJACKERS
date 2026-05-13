using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireNebulaShipSparksVfx : MonoBehaviour
{
    const int SparkCount = 24;
    const float EffectZOffset = -0.42f;
    const float MinSparkAlpha = 0.18f;
    const float MaxSparkAlpha = 0.92f;

    static Material lineMaterial;

    readonly Color hotCore = new Color(1f, 0.95f, 0.62f, 1f);
    readonly Color orange = new Color(1f, 0.45f, 0.08f, 1f);
    readonly Color ember = new Color(0.95f, 0.12f, 0.02f, 1f);

    LineRenderer[] sparks;
    float[] angleOffsets;
    float[] radiusJitter;
    float[] speedOffsets;
    float[] pulseOffsets;
    SpriteRenderer spriteRenderer;
    Rigidbody2D rb;
    HideInNebulaTarget nebulaTarget;
    PlayerHealth health;
    PlayerMovement movement;
    PhotonView view;
    bool sparksVisible;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        nebulaTarget = GetComponent<HideInNebulaTarget>();
        health = GetComponent<PlayerHealth>();
        movement = GetComponent<PlayerMovement>();
        view = GetComponent<PhotonView>();
        CreateSparks();
        SetSparksVisible(false);
    }

    void Update()
    {
        if (!ShouldShowSparks())
        {
            SetSparksVisible(false);
            return;
        }

        SetSparksVisible(true);
        UpdateSparks();
    }

    bool ShouldShowSparks()
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return false;

        if (nebulaTarget == null || !nebulaTarget.IsInsideFireNebula)
            return false;

        if (health == null || health.IsWreck || health.IsBotControlled || health.IsAstronautControlled)
            return false;

        if (GetComponent<EnemyBot>() != null || GetComponent<AstronautSurvivor>() != null)
            return false;

        if (spriteRenderer == null || !spriteRenderer.enabled)
            return false;

        if (view != null && view.InstantiationData != null && EnemyBot.IsBotInstantiationData(view.InstantiationData))
            return false;

        return true;
    }

    void CreateSparks()
    {
        sparks = new LineRenderer[SparkCount];
        angleOffsets = new float[SparkCount];
        radiusJitter = new float[SparkCount];
        speedOffsets = new float[SparkCount];
        pulseOffsets = new float[SparkCount];

        int sortingLayerId = spriteRenderer != null
            ? spriteRenderer.sortingLayerID
            : SortingLayer.NameToID(GameVisualTheme.WorldSortingLayerName);
        int sortingOrder = spriteRenderer != null
            ? spriteRenderer.sortingOrder + 8
            : GameVisualTheme.PlayerSortingOrder + 8;

        for (int i = 0; i < SparkCount; i++)
        {
            angleOffsets[i] = Hash01(i, 1) * Mathf.PI * 2f;
            radiusJitter[i] = Mathf.Lerp(0.74f, 1.28f, Hash01(i, 2));
            speedOffsets[i] = Mathf.Lerp(0.82f, 1.55f, Hash01(i, 3));
            pulseOffsets[i] = Hash01(i, 4) * Mathf.PI * 2f;
            sparks[i] = CreateSparkLine("FireNebulaShipSpark_" + i, sortingLayerId, sortingOrder + i);
        }
    }

    LineRenderer CreateSparkLine(string objectName, int sortingLayerId, int sortingOrder)
    {
        GameObject sparkObject = new GameObject(objectName);
        sparkObject.transform.SetParent(transform, false);

        LineRenderer line = sparkObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 3;
        line.widthMultiplier = 0.035f;
        line.startWidth = 0.035f;
        line.endWidth = 0.006f;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetLineMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
    }

    void SetSparksVisible(bool visible)
    {
        if (sparksVisible == visible && sparks != null)
            return;

        sparksVisible = visible;
        if (sparks == null)
            return;

        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i] != null)
                sparks[i].enabled = visible;
        }
    }

    void UpdateSparks()
    {
        if (sparks == null)
            return;

        RefreshSorting();

        Vector3 center = ResolveVisualCenter();
        Vector2 velocity = rb != null ? rb.linearVelocity : Vector2.zero;
        float speed = velocity.magnitude;
        float speedReference = movement != null ? Mathf.Max(0.1f, movement.CurrentSpeedReference * 0.5f) : 2.5f;
        float speedT = Mathf.Clamp01(speed / speedReference);
        float intensity = Mathf.Lerp(0.24f, 1f, speedT);
        int activeCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(7f, SparkCount, speedT)), 3, SparkCount);

        float visualRadius = ResolveVisualRadius();
        Vector2 velocityDir = velocity.sqrMagnitude > 0.001f ? velocity.normalized : (Vector2)transform.up;
        Vector3 backDrift = new Vector3(-velocityDir.x, -velocityDir.y, 0f);

        for (int i = 0; i < sparks.Length; i++)
        {
            LineRenderer spark = sparks[i];
            if (spark == null)
                continue;

            bool active = i < activeCount;
            spark.enabled = active;
            if (!active)
                continue;

            float phase = Time.time * Mathf.Lerp(2.2f, 7.5f, intensity) * speedOffsets[i] + angleOffsets[i];
            float pulse = Mathf.Sin(Time.time * Mathf.Lerp(5.2f, 12f, intensity) + pulseOffsets[i]) * 0.5f + 0.5f;
            Vector3 radial = new Vector3(Mathf.Cos(phase), Mathf.Sin(phase), 0f);
            Vector3 tangent = new Vector3(-radial.y, radial.x, 0f);
            float radius = visualRadius * Mathf.Lerp(0.7f, 1.28f, pulse) * radiusJitter[i];
            Vector3 head = center + radial * radius;
            head.z += EffectZOffset;

            float length = visualRadius * Mathf.Lerp(0.18f, 0.84f, intensity) * Mathf.Lerp(0.72f, 1.38f, pulse);
            Vector3 tail = head - (radial * 0.34f + tangent * Mathf.Sin(phase * 1.7f) * 0.22f + backDrift * Mathf.Lerp(0.2f, 1.15f, speedT)).normalized * length;
            Vector3 mid = Vector3.Lerp(tail, head, 0.58f) + tangent * (0.045f + visualRadius * 0.045f) * Mathf.Sin(phase * 2.1f);

            spark.SetPosition(0, tail);
            spark.SetPosition(1, mid);
            spark.SetPosition(2, head);

            float alpha = Mathf.Lerp(MinSparkAlpha, MaxSparkAlpha, intensity) * Mathf.Lerp(0.48f, 1f, pulse);
            Color start = Color.Lerp(ember, orange, pulse);
            Color end = Color.Lerp(orange, hotCore, intensity);
            start.a = alpha * 0.08f;
            end.a = alpha;
            spark.startColor = start;
            spark.endColor = end;

            float width = Mathf.Lerp(0.014f, 0.052f, intensity) * Mathf.Lerp(0.76f, 1.28f, pulse);
            spark.widthMultiplier = width;
            spark.startWidth = width * 0.18f;
            spark.endWidth = width;
        }
    }

    void RefreshSorting()
    {
        if (spriteRenderer == null || sparks == null)
            return;

        int layerId = spriteRenderer.sortingLayerID;
        int order = spriteRenderer.sortingOrder + 8;
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i] == null)
                continue;

            sparks[i].sortingLayerID = layerId;
            sparks[i].sortingOrder = order + i;
        }
    }

    Vector3 ResolveVisualCenter()
    {
        if (spriteRenderer != null)
        {
            Bounds bounds = spriteRenderer.bounds;
            return new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
        }

        return transform.position;
    }

    float ResolveVisualRadius()
    {
        if (spriteRenderer != null)
        {
            Bounds bounds = spriteRenderer.bounds;
            return Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.y), 0.34f, 1.2f);
        }

        return 0.55f;
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
            name = "FireNebulaShipSparksVfxMaterial",
            color = Color.white
        };
        lineMaterial.renderQueue = 5000;
        return lineMaterial;
    }

    static float Hash01(int index, int salt)
    {
        return Mathf.Repeat(Mathf.Sin((index * 139.37f) + (salt * 271.91f)) * 43758.5453f, 1f);
    }
}
