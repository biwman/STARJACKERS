using UnityEngine;

public sealed class HunterLanceBeamVfx : MonoBehaviour
{
    const float EffectZOffset = -0.092f;
    const int SortingOrder = 2385;
    const float ShotGlowWidth = 0.96f;
    const float ShotCoreWidth = 0.27f;
    const float AimGlowWidth = 0.48f;
    const float AimCoreWidth = 0.11f;

    static Material sharedMaterial;

    LineRenderer glowLine;
    LineRenderer coreLine;
    Vector3 origin;
    Vector3 direction = Vector3.up;
    float range = 12f;
    float duration = 0.6f;
    float startedAt;
    bool isShot;

    public static void Prewarm()
    {
        GetMaterial();
    }

    public static void SpawnAim(Vector2 start, Vector2 aimDirection, float beamRange, float warningDuration)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject effect = new GameObject("HunterLanceAimVfx");
        HunterLanceBeamVfx vfx = effect.AddComponent<HunterLanceBeamVfx>();
        vfx.Initialize(start, aimDirection, beamRange, Mathf.Max(0.1f, warningDuration), false);
    }

    public static void SpawnShot(Vector2 start, Vector2 aimDirection, float beamRange)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject effect = new GameObject("HunterLanceShotVfx");
        HunterLanceBeamVfx vfx = effect.AddComponent<HunterLanceBeamVfx>();
        vfx.Initialize(start, aimDirection, beamRange, 0.22f, true);
    }

    void Initialize(Vector2 start, Vector2 aimDirection, float beamRange, float effectDuration, bool shot)
    {
        origin = new Vector3(start.x, start.y, EffectZOffset);
        direction = aimDirection.sqrMagnitude > 0.001f
            ? new Vector3(aimDirection.x, aimDirection.y, 0f).normalized
            : Vector3.up;
        range = Mathf.Max(0.5f, beamRange);
        duration = Mathf.Max(0.05f, effectDuration);
        isShot = shot;
        startedAt = Time.time;

        transform.position = origin;
        glowLine = CreateLine("HunterLanceGlow", shot ? ShotGlowWidth : AimGlowWidth, SortingOrder);
        coreLine = CreateLine("HunterLanceCore", shot ? ShotCoreWidth : AimCoreWidth, SortingOrder + 1);
        UpdateLines(0f);
    }

    void Update()
    {
        float t = Mathf.Clamp01((Time.time - startedAt) / duration);
        UpdateLines(t);

        if (t >= 1f)
            Destroy(gameObject);
    }

    void UpdateLines(float t)
    {
        Vector3 start = origin;
        Vector3 end = origin + direction * range;
        float pulse = Mathf.Sin(Time.time * (isShot ? 52f : 18f)) * 0.5f + 0.5f;
        float alpha = isShot
            ? Mathf.Lerp(1f, 0f, t)
            : Mathf.Lerp(0.28f, 0.76f, Mathf.PingPong(t * 2.4f, 1f)) * Mathf.Lerp(1f, 0.15f, Mathf.Max(0f, t - 0.72f) / 0.28f);

        ApplyLine(glowLine, start, end, isShot ? BuildShotGlow(alpha, pulse) : BuildAimGlow(alpha, pulse), isShot ? ShotGlowWidth : Mathf.Lerp(0.32f, 0.56f, pulse));
        ApplyLine(coreLine, start, end, isShot ? BuildShotCore(alpha, pulse) : BuildAimCore(alpha, pulse), isShot ? ShotCoreWidth : Mathf.Lerp(0.07f, 0.14f, pulse));
    }

    void ApplyLine(LineRenderer line, Vector3 start, Vector3 end, Gradient gradient, float width)
    {
        if (line == null)
            return;

        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.colorGradient = gradient;
        line.widthMultiplier = width;
    }

    LineRenderer CreateLine(string lineName, float width, int order)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = width;
        line.numCapVertices = 12;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetMaterial();
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = order;
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.15f),
            new Keyframe(0.12f, 1f),
            new Keyframe(0.88f, 1f),
            new Keyframe(1f, 0.15f));
        return line;
    }

    static Gradient BuildAimCore(float alpha, float pulse)
    {
        return BuildGradient(
            new Color(0.68f, 0.95f, 1f),
            new Color(0.08f, 0.72f, 1f),
            new Color(0.04f, 0.28f, 0.9f),
            alpha * Mathf.Lerp(0.74f, 1f, pulse));
    }

    static Gradient BuildAimGlow(float alpha, float pulse)
    {
        return BuildGradient(
            new Color(0.08f, 0.58f, 1f),
            new Color(0.12f, 0.24f, 1f),
            new Color(0.52f, 0.08f, 1f),
            alpha * Mathf.Lerp(0.35f, 0.58f, pulse));
    }

    static Gradient BuildShotCore(float alpha, float pulse)
    {
        return BuildGradient(
            Color.white,
            new Color(0.48f, 0.96f, 1f),
            new Color(0.08f, 0.6f, 1f),
            alpha * Mathf.Lerp(0.82f, 1f, pulse));
    }

    static Gradient BuildShotGlow(float alpha, float pulse)
    {
        return BuildGradient(
            new Color(0.62f, 0.96f, 1f),
            new Color(0.02f, 0.6f, 1f),
            new Color(0.08f, 0.12f, 0.92f),
            alpha * Mathf.Lerp(0.38f, 0.72f, pulse));
    }

    static Gradient BuildGradient(Color start, Color middle, Color end, float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(middle, 0.46f),
                new GradientColorKey(end, 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha * 0.12f, 0f),
                new GradientAlphaKey(alpha, 0.18f),
                new GradientAlphaKey(alpha * 0.86f, 0.82f),
                new GradientAlphaKey(alpha * 0.08f, 1f)
            });
        return gradient;
    }

    static Material GetMaterial()
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        sharedMaterial = new Material(shader)
        {
            name = "HunterLanceBeamVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3370;
        return sharedMaterial;
    }
}
