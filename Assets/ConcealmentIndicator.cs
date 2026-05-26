using UnityEngine;

[DisallowMultipleComponent]
public sealed class ConcealmentIndicator : MonoBehaviour
{
    const int SegmentCount = 72;
    const float BaseRingWidth = 0.035f;
    const float OuterRingWidth = 0.08f;
    const float RadiusPadding = 1.22f;

    static Material sharedLineMaterial;

    LineRenderer innerRing;
    LineRenderer outerRing;
    NebulaFieldKind currentKind = NebulaFieldKind.Normal;
    bool isVisible;

    void Awake()
    {
        EnsureRenderers();
    }

    void Update()
    {
        if (!isVisible)
            return;

        float pulse = 0.5f + Mathf.Sin(Time.time * 3.2f) * 0.5f;
        Color baseColor = GetColor(currentKind);
        Color innerColor = baseColor;
        innerColor.a = Mathf.Lerp(0.24f, 0.42f, pulse);
        Color outerColor = baseColor;
        outerColor.a = Mathf.Lerp(0.06f, 0.16f, pulse);

        if (innerRing != null)
        {
            innerRing.startColor = innerColor;
            innerRing.endColor = innerColor;
        }

        if (outerRing != null)
        {
            outerRing.startColor = outerColor;
            outerRing.endColor = outerColor;
        }
    }

    public void SetConcealed(bool visible, NebulaFieldKind kind, SpriteRenderer referenceRenderer)
    {
        EnsureRenderers();

        isVisible = visible;
        currentKind = kind;

        if (innerRing != null)
            innerRing.enabled = visible;
        if (outerRing != null)
            outerRing.enabled = visible;

        if (!visible)
            return;

        ApplySorting(referenceRenderer);
        RebuildRing(referenceRenderer);
        Update();
    }

    void EnsureRenderers()
    {
        if (innerRing == null)
            innerRing = CreateRing("ConcealmentIndicatorInner", BaseRingWidth);
        if (outerRing == null)
            outerRing = CreateRing("ConcealmentIndicatorOuter", OuterRingWidth);
    }

    LineRenderer CreateRing(string objectName, float width)
    {
        GameObject ringObject = new GameObject(objectName);
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = Vector3.zero;
        ringObject.transform.localRotation = Quaternion.identity;
        ringObject.transform.localScale = Vector3.one;

        LineRenderer line = ringObject.AddComponent<LineRenderer>();
        line.enabled = false;
        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = SegmentCount;
        line.widthMultiplier = width;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetLineMaterial();
        return line;
    }

    void ApplySorting(SpriteRenderer referenceRenderer)
    {
        if (referenceRenderer == null)
            return;

        if (innerRing != null)
        {
            innerRing.sortingLayerID = referenceRenderer.sortingLayerID;
            innerRing.sortingOrder = referenceRenderer.sortingOrder + 3;
        }

        if (outerRing != null)
        {
            outerRing.sortingLayerID = referenceRenderer.sortingLayerID;
            outerRing.sortingOrder = referenceRenderer.sortingOrder + 2;
        }
    }

    void RebuildRing(SpriteRenderer referenceRenderer)
    {
        float radius = ResolveLocalRadius(referenceRenderer);
        FillRing(innerRing, radius);
        FillRing(outerRing, radius * 1.06f);
    }

    float ResolveLocalRadius(SpriteRenderer referenceRenderer)
    {
        float worldRadius = 0.8f;
        if (referenceRenderer != null)
        {
            Bounds bounds = referenceRenderer.bounds;
            worldRadius = Mathf.Max(bounds.extents.x, bounds.extents.y) * RadiusPadding;
        }

        Vector3 scale = transform.lossyScale;
        float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        if (largestScale <= 0.0001f)
            largestScale = 1f;

        return Mathf.Max(0.25f, worldRadius / largestScale);
    }

    void FillRing(LineRenderer line, float radius)
    {
        if (line == null)
            return;

        for (int i = 0; i < SegmentCount; i++)
        {
            float t = (Mathf.PI * 2f * i) / SegmentCount;
            line.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f));
        }
    }

    static Color GetColor(NebulaFieldKind kind)
    {
        switch (kind)
        {
            case NebulaFieldKind.Fire:
                return new Color(1f, 0.58f, 0.2f, 1f);
            case NebulaFieldKind.Cloud:
                return new Color(0.92f, 0.96f, 1f, 1f);
            default:
                return new Color(0.38f, 0.88f, 1f, 1f);
        }
    }

    static Material GetLineMaterial()
    {
        if (sharedLineMaterial != null)
            return sharedLineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        sharedLineMaterial = shader != null ? new Material(shader) : null;
        return sharedLineMaterial;
    }
}
