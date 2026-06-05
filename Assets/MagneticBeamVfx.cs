using System.Collections.Generic;
using UnityEngine;

public sealed class MagneticBeamVfx : MonoBehaviour
{
    const float Radius = 8f;
    const float Duration = 3f;
    const float EffectZOffset = -0.08f;
    const int MaxBeams = 18;
    const int BeamSortingOrderOffset = 260;

    static Material sharedMaterial;

    readonly List<LineRenderer> beams = new List<LineRenderer>();
    Transform source;
    SpriteRenderer sourceRenderer;
    int sortingLayerId;
    int sortingOrder = 2400;
    float age;

    public static void Prewarm()
    {
        GetMaterial();
    }

    public static void Spawn(Transform sourceTransform)
    {
        if (sourceTransform == null)
            return;

        GameObject effect = new GameObject("MagneticBeamVfx");
        MagneticBeamVfx vfx = effect.AddComponent<MagneticBeamVfx>();
        vfx.Initialize(sourceTransform);
    }

    void Initialize(Transform sourceTransform)
    {
        source = sourceTransform;
        sourceRenderer = sourceTransform.GetComponentInChildren<SpriteRenderer>();
        if (sourceRenderer != null)
        {
            sortingLayerId = sourceRenderer.sortingLayerID;
            sortingOrder = sourceRenderer.sortingOrder + BeamSortingOrderOffset;
        }

        for (int i = 0; i < MaxBeams; i++)
        {
            LineRenderer line = CreateBeam(i);
            line.enabled = false;
            beams.Add(line);
        }
    }

    void Update()
    {
        if (source == null)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        float normalizedAge = Mathf.Clamp01(age / Duration);
        UpdateBeams(normalizedAge);

        if (age >= Duration)
            Destroy(gameObject);
    }

    void UpdateBeams(float normalizedAge)
    {
        MovingSpaceObject[] objects = FindObjectsByType<MovingSpaceObject>(FindObjectsInactive.Exclude);
        Vector3 sourcePosition = source.position;
        int beamIndex = 0;
        for (int i = 0; i < objects.Length && beamIndex < beams.Count; i++)
        {
            MovingSpaceObject target = objects[i];
            if (target == null)
                continue;

            Vector3 targetPosition = target.transform.position;
            float distance = Vector2.Distance(sourcePosition, targetPosition);
            if (distance > Radius)
                continue;

            LineRenderer beam = beams[beamIndex++];
            UpdateBeam(beam, sourcePosition, targetPosition, distance, normalizedAge, beamIndex);
        }

        for (int i = beamIndex; i < beams.Count; i++)
            beams[i].enabled = false;
    }

    void UpdateBeam(LineRenderer beam, Vector3 start, Vector3 end, float distance, float normalizedAge, int beamIndex)
    {
        if (beam == null)
            return;

        beam.enabled = true;
        Vector3 direction = end - start;
        Vector3 perpendicular = direction.sqrMagnitude > 0.001f
            ? Vector3.Cross(direction.normalized, Vector3.forward)
            : Vector3.right;

        float pulse = Mathf.Sin((Time.time * 18f) + (beamIndex * 1.7f)) * 0.5f + 0.5f;
        float wave = Mathf.Lerp(0.06f, 0.2f, pulse) * Mathf.Clamp01(distance / Radius);
        float alpha = Mathf.Sin(normalizedAge * Mathf.PI);
        float renderZ = start.z + EffectZOffset;
        for (int point = 0; point < beam.positionCount; point++)
        {
            float t = point / (float)(beam.positionCount - 1);
            Vector3 pointPosition = Vector3.Lerp(start, end, t);
            float ripple = Mathf.Sin((t * Mathf.PI * 6f) + Time.time * 16f + beamIndex) * wave;
            pointPosition += perpendicular * ripple;
            pointPosition.z = renderZ;
            beam.SetPosition(point, pointPosition);
        }

        Color core = new Color(0.76f, 0.98f, 1f, 0.95f * alpha);
        Color edge = new Color(0.08f, 0.54f, 1f, 0.72f * alpha);
        beam.startColor = core;
        beam.endColor = edge;
        beam.widthMultiplier = Mathf.Lerp(0.12f, 0.28f, pulse);
        beam.sortingLayerID = sortingLayerId;
        beam.sortingOrder = sortingOrder;
    }

    LineRenderer CreateBeam(int index)
    {
        GameObject beamObject = new GameObject("MagneticBeamLine" + index);
        beamObject.transform.SetParent(transform, false);
        if (source != null)
            beamObject.layer = source.gameObject.layer;

        LineRenderer line = beamObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 13;
        line.widthMultiplier = 0.18f;
        line.numCapVertices = 14;
        line.numCornerVertices = 12;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
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
            name = "MagneticBeamVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3300;
        return sharedMaterial;
    }
}
