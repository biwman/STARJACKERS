using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class BioTrapCaptureVfx : MonoBehaviour
{
    const int StrandCount = 7;
    readonly LineRenderer[] strands = new LineRenderer[StrandCount];
    Vector2 source;
    Vector2 target;
    float startedAt;
    float duration;

    public static void Spawn(Vector3 sourcePosition, Vector3 targetPosition)
    {
        GameObject effectObject = new GameObject("BioTrapCaptureVfx");
        BioTrapCaptureVfx effect = effectObject.AddComponent<BioTrapCaptureVfx>();
        effect.Initialize(sourcePosition, targetPosition);
    }

    void Initialize(Vector3 sourcePosition, Vector3 targetPosition)
    {
        source = sourcePosition;
        target = targetPosition;
        startedAt = Time.time;
        duration = 0.68f;
        for (int i = 0; i < strands.Length; i++)
            strands[i] = CreateStrand(i);

        UpdateStrands();
    }

    void Update()
    {
        if (Time.time >= startedAt + duration)
        {
            Destroy(gameObject);
            return;
        }

        UpdateStrands();
    }

    LineRenderer CreateStrand(int index)
    {
        GameObject strandObject = new GameObject("BioTrapStrand_" + index.ToString("00"));
        strandObject.transform.SetParent(transform, false);
        LineRenderer strand = strandObject.AddComponent<LineRenderer>();
        strand.useWorldSpace = true;
        strand.positionCount = 5;
        strand.widthMultiplier = 0.025f;
        strand.numCapVertices = 4;
        strand.numCornerVertices = 3;
        strand.material = new Material(Shader.Find("Sprites/Default"));
        strand.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        strand.sortingOrder = 78 + index;
        return strand;
    }

    void UpdateStrands()
    {
        float age = Mathf.Clamp01((Time.time - startedAt) / Mathf.Max(0.001f, duration));
        Vector2 direction = target - source;
        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.up;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized;
        float netRadius = Mathf.Lerp(0.78f, 0.12f, age);
        Color color = Color.Lerp(new Color(0.38f, 1f, 0.72f, 0.88f), new Color(1f, 0.86f, 0.38f, 0.1f), age);
        for (int i = 0; i < strands.Length; i++)
        {
            LineRenderer strand = strands[i];
            if (strand == null)
                continue;

            float side = i - (strands.Length - 1) * 0.5f;
            Vector2 strandTarget = target + normal * (side * netRadius * 0.32f);
            for (int p = 0; p < strand.positionCount; p++)
            {
                float t = p / (float)(strand.positionCount - 1);
                float curve = Mathf.Sin(t * Mathf.PI) * netRadius * (0.25f + Mathf.Abs(side) * 0.04f);
                Vector2 point = Vector2.Lerp(source, strandTarget, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t + age * 0.24f))) + normal * curve * Mathf.Sign(side == 0f ? 1f : side);
                strand.SetPosition(p, new Vector3(point.x, point.y, -0.45f));
            }

            strand.startColor = color;
            strand.endColor = new Color(color.r, color.g, color.b, color.a * 0.18f);
        }
    }
}
