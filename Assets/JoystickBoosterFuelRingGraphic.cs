using UnityEngine;
using UnityEngine.UI;

public sealed class JoystickBoosterFuelRingGraphic : MaskableGraphic
{
    const int SegmentCount = 128;
    const float MinVisibleArcDegrees = 8f;
    const float EdgeFadePortion = 0.12f;
    const float MinBandScale = 0.38f;
    const float MaxBandScale = 0.78f;

    [SerializeField] float fillAmount = 1f;
    [SerializeField] float thicknessScale = 1f;
    [SerializeField] bool ringVisible = true;
    [SerializeField] bool showMinimumAtZero = true;
    [SerializeField] bool fadeArcEdges = true;

    public void SetFill(float fill, Color ringColor, bool visible = true, float thickness = 1f, bool minimumAtZero = true, bool fadeEdges = true)
    {
        float clampedFill = Mathf.Clamp01(fill);
        float clampedThickness = Mathf.Clamp01(thickness);
        bool changed =
            Mathf.Abs(fillAmount - clampedFill) > 0.001f ||
            Mathf.Abs(thicknessScale - clampedThickness) > 0.001f ||
            ringVisible != visible ||
            showMinimumAtZero != minimumAtZero ||
            fadeArcEdges != fadeEdges ||
            !ColorsClose(color, ringColor);

        fillAmount = clampedFill;
        thicknessScale = clampedThickness;
        ringVisible = visible;
        showMinimumAtZero = minimumAtZero;
        fadeArcEdges = fadeEdges;
        color = ringColor;

        if (changed)
            SetVerticesDirty();
    }

    public static float GetInnerRadiusRatio(float thickness)
    {
        float rawInputBandRatio = 1f - (1f / PlayerMovement.AdvancedBoosterOuterInputLimit);
        float bandScale = Mathf.Lerp(MinBandScale, MaxBandScale, Mathf.Clamp01(thickness));
        return 1f - rawInputBandRatio * bandScale;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!ringVisible || color.a <= 0.001f)
            return;

        Rect rect = rectTransform.rect;
        float outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
        if (outerRadius <= 0.01f)
            return;

        float arcDegrees = 360f * fillAmount;
        if (showMinimumAtZero)
            arcDegrees = Mathf.Max(MinVisibleArcDegrees, arcDegrees);

        arcDegrees = Mathf.Clamp(arcDegrees, 0f, 360f);
        if (arcDegrees <= 0.001f)
            return;

        bool fullCircle = arcDegrees >= 359.9f;
        float innerRadius = Mathf.Max(0f, outerRadius * GetInnerRadiusRatio(thicknessScale));
        Vector2 center = rect.center;
        int segmentCount = fullCircle
            ? SegmentCount
            : Mathf.Max(4, Mathf.CeilToInt(SegmentCount * (arcDegrees / 360f)));
        float step = arcDegrees / segmentCount;
        float startAngleDegrees = 90f;

        for (int i = 0; i < segmentCount; i++)
        {
            float t0 = i / (float)segmentCount;
            float t1 = (i + 1f) / segmentCount;
            float angle0 = (startAngleDegrees - step * i) * Mathf.Deg2Rad;
            float angle1 = (startAngleDegrees - step * (i + 1)) * Mathf.Deg2Rad;
            Vector2 direction0 = new Vector2(Mathf.Cos(angle0), Mathf.Sin(angle0));
            Vector2 direction1 = new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1));
            Color32 innerColor0 = ColorWithEffects(t0, 0f, fullCircle);
            Color32 outerColor0 = ColorWithEffects(t0, 1f, fullCircle);
            Color32 innerColor1 = ColorWithEffects(t1, 0f, fullCircle);
            Color32 outerColor1 = ColorWithEffects(t1, 1f, fullCircle);

            int startIndex = vh.currentVertCount;
            vh.AddVert(center + direction0 * innerRadius, innerColor0, Vector2.zero);
            vh.AddVert(center + direction0 * outerRadius, outerColor0, Vector2.zero);
            vh.AddVert(center + direction1 * outerRadius, outerColor1, Vector2.zero);
            vh.AddVert(center + direction1 * innerRadius, innerColor1, Vector2.zero);
            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }
    }

    Color32 ColorWithEffects(float normalizedArcPosition, float radialPosition, bool fullCircle)
    {
        float edgeFade = 1f;
        if (fadeArcEdges && !fullCircle)
        {
            float edgeDistance = Mathf.Min(normalizedArcPosition, 1f - normalizedArcPosition);
            edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edgeDistance / EdgeFadePortion));
        }

        float centerDistance = Mathf.Abs(normalizedArcPosition - 0.5f) * 2f;
        float centerGlow = fullCircle ? 0f : 1f - Mathf.SmoothStep(0.12f, 1f, centerDistance);
        float radialGlow = Mathf.Lerp(0.72f, 1.08f, Mathf.Clamp01(radialPosition));
        Color result = color;
        result = Color.Lerp(result, Color.white, centerGlow * 0.1f);
        result.a *= edgeFade * radialGlow * Mathf.Lerp(0.94f, 1.1f, centerGlow);
        return result;
    }

    static bool ColorsClose(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.001f &&
               Mathf.Abs(a.g - b.g) < 0.001f &&
               Mathf.Abs(a.b - b.b) < 0.001f &&
               Mathf.Abs(a.a - b.a) < 0.001f;
    }
}
