using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class LootHookSnatchVfx : MonoBehaviour
{
    Transform source;
    Transform target;
    float endsAt;
    LineRenderer glowLine;
    LineRenderer coreLine;

    public static void Spawn(Transform sourceTransform, Transform targetTransform)
    {
        if (sourceTransform == null || targetTransform == null)
            return;

        GameObject effectObject = new GameObject("LootHookSnatchVfx");
        LootHookSnatchVfx effect = effectObject.AddComponent<LootHookSnatchVfx>();
        effect.Initialize(sourceTransform, targetTransform);
    }

    void Initialize(Transform sourceTransform, Transform targetTransform)
    {
        source = sourceTransform;
        target = targetTransform;
        endsAt = Time.time + 0.58f;
        glowLine = CreateLine("Glow", 0.19f, 76, new Color(1f, 0.68f, 0.12f, 0.34f));
        coreLine = CreateLine("Core", 0.045f, 77, new Color(1f, 0.96f, 0.58f, 0.92f));
        UpdateLines();
    }

    void Update()
    {
        if (source == null || target == null || Time.time >= endsAt)
        {
            Destroy(gameObject);
            return;
        }

        UpdateLines();
    }

    LineRenderer CreateLine(string objectName, float width, int sortingOrder, Color color)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 10;
        line.widthMultiplier = width;
        line.numCapVertices = 8;
        line.numCornerVertices = 4;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0f);
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = sortingOrder;
        return line;
    }

    void UpdateLines()
    {
        Vector2 start = source.position;
        Vector2 end = target.position;
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.up;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized;
        float tLife = Mathf.Clamp01((endsAt - Time.time) / 0.58f);
        SetHookLine(glowLine, start, end, normal, 0.18f * tLife);
        SetHookLine(coreLine, start, end, normal, 0.08f * tLife);
    }

    void SetHookLine(LineRenderer line, Vector2 start, Vector2 end, Vector2 normal, float amplitude)
    {
        if (line == null)
            return;

        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (float)(line.positionCount - 1);
            float wave = Mathf.Sin(t * Mathf.PI * 3f + Time.time * 26f) * amplitude;
            Vector2 point = Vector2.Lerp(start, end, t) + normal * wave;
            line.SetPosition(i, new Vector3(point.x, point.y, -0.44f));
        }
    }
}
