using UnityEngine;
using UnityEngine.UI;

public sealed class JoystickBoosterRingGraphic : MaskableGraphic
{
    const int SegmentCount = 96;
    const float DefaultArcDegrees = 72f;
    const float EdgeFadePortion = 0.18f;

    [SerializeField] float centerAngleDegrees;
    [SerializeField] float arcDegrees = DefaultArcDegrees;
    [SerializeField] float thicknessScale = 1f;
    [SerializeField] float centerHighlight = 1f;
    [SerializeField] bool arcVisible;

    public void SetArc(float centerAngle, float visibleArcDegrees = DefaultArcDegrees, bool visible = true, float thickness = 1f, float highlight = 1f)
    {
        float normalizedCenter = Mathf.Repeat(centerAngle, 360f);
        float clampedArc = Mathf.Clamp(visibleArcDegrees, 1f, 360f);
        float clampedThickness = Mathf.Clamp01(thickness);
        float clampedHighlight = Mathf.Clamp01(highlight);
        if (arcVisible == visible &&
            Mathf.Abs(Mathf.DeltaAngle(centerAngleDegrees, normalizedCenter)) < 0.05f &&
            Mathf.Abs(arcDegrees - clampedArc) < 0.05f &&
            Mathf.Abs(thicknessScale - clampedThickness) < 0.005f &&
            Mathf.Abs(centerHighlight - clampedHighlight) < 0.005f)
        {
            return;
        }

        centerAngleDegrees = normalizedCenter;
        arcDegrees = clampedArc;
        thicknessScale = clampedThickness;
        centerHighlight = clampedHighlight;
        arcVisible = visible;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!arcVisible || color.a <= 0.001f)
            return;

        Rect rect = rectTransform.rect;
        float outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
        if (outerRadius <= 0.01f)
            return;

        float normalRadius = outerRadius / PlayerMovement.AdvancedBoosterOuterInputLimit;
        float maxBandWidth = outerRadius - normalRadius;
        float liveBandWidth = maxBandWidth * Mathf.Lerp(0.42f, 1f, thicknessScale);
        float innerRadius = outerRadius - liveBandWidth;
        Vector2 center = rect.center;
        int segmentCount = Mathf.Max(6, Mathf.CeilToInt(SegmentCount * (arcDegrees / 360f)));
        float startAngle = centerAngleDegrees - arcDegrees * 0.5f;
        float step = arcDegrees / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            float t0 = i / (float)segmentCount;
            float t1 = (i + 1f) / segmentCount;
            float angle0 = (startAngle + step * i) * Mathf.Deg2Rad;
            float angle1 = (startAngle + step * (i + 1)) * Mathf.Deg2Rad;
            Vector2 direction0 = new Vector2(Mathf.Cos(angle0), Mathf.Sin(angle0));
            Vector2 direction1 = new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1));
            Color32 innerColor0 = ColorWithFade(t0, 0f);
            Color32 outerColor0 = ColorWithFade(t0, 1f);
            Color32 innerColor1 = ColorWithFade(t1, 0f);
            Color32 outerColor1 = ColorWithFade(t1, 1f);

            int startIndex = vh.currentVertCount;
            vh.AddVert(center + direction0 * innerRadius, innerColor0, Vector2.zero);
            vh.AddVert(center + direction0 * outerRadius, outerColor0, Vector2.zero);
            vh.AddVert(center + direction1 * outerRadius, outerColor1, Vector2.zero);
            vh.AddVert(center + direction1 * innerRadius, innerColor1, Vector2.zero);
            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }
    }

    Color32 ColorWithFade(float normalizedArcPosition, float radialPosition)
    {
        float edgeDistance = Mathf.Min(normalizedArcPosition, 1f - normalizedArcPosition);
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edgeDistance / EdgeFadePortion));
        float centerDistance = Mathf.Abs(normalizedArcPosition - 0.5f) * 2f;
        float centerGlow = 1f - Mathf.SmoothStep(0.12f, 1f, centerDistance);
        float radialGlow = Mathf.Lerp(0.58f, 1f, Mathf.Clamp01(radialPosition));
        float alphaBoost = Mathf.Lerp(0.82f, 1.2f, centerGlow * centerHighlight);
        Color faded = color;
        Color warmCore = new Color(1f, 0.46f, 0.16f, faded.a);
        faded = Color.Lerp(faded, warmCore, centerGlow * centerHighlight * 0.24f);
        faded.a *= fade * radialGlow * alphaBoost;
        return faded;
    }
}
