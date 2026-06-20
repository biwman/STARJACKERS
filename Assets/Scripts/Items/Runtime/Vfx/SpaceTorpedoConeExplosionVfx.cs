using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class SpaceTorpedoConeExplosionVfx : MonoBehaviour
{
    const float Lifetime = 0.68f;
    const float EffectZ = -0.36f;
    const int RayCount = 11;
    const int ArcSegments = 18;

    static Material sharedMaterial;

    readonly LineRenderer[] rays = new LineRenderer[RayCount];
    LineRenderer frontArc;
    LineRenderer coreJet;
    Vector3 center;
    Vector2 direction = Vector2.up;
    float range;
    float halfAngle;
    float age;

    public static void Spawn(Vector3 position, Vector2 forward, float coneRange, float coneHalfAngleDegrees)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject effectObject = new GameObject("SpaceTorpedoConeExplosionVfx");
        SpaceTorpedoConeExplosionVfx effect = effectObject.AddComponent<SpaceTorpedoConeExplosionVfx>();
        effect.Initialize(position, forward, coneRange, coneHalfAngleDegrees);
    }

    void Initialize(Vector3 position, Vector2 forward, float coneRange, float coneHalfAngleDegrees)
    {
        center = new Vector3(position.x, position.y, EffectZ);
        transform.position = center;
        direction = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector2.up;
        range = Mathf.Max(0.5f, coneRange);
        halfAngle = Mathf.Clamp(coneHalfAngleDegrees, 8f, 85f);
        age = 0f;

        for (int i = 0; i < rays.Length; i++)
            rays[i] = CreateLine("TorpedoConeRay_" + i.ToString("00"), 0.055f, false, 2, 6900 + i);

        frontArc = CreateLine("TorpedoConeFrontArc", 0.075f, false, ArcSegments, 6920);
        coreJet = CreateLine("TorpedoConeCoreJet", 0.18f, false, 2, 6925);
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
        float expansion = 1f - Mathf.Pow(1f - t, 2.2f);
        float fade = Mathf.Pow(1f - t, 1.35f);
        Vector3 origin = center + (Vector3)(direction * Mathf.Lerp(0.04f, 0.18f, expansion));

        for (int i = 0; i < rays.Length; i++)
        {
            LineRenderer ray = rays[i];
            if (ray == null)
                continue;

            float normalized = rays.Length <= 1 ? 0.5f : i / (float)(rays.Length - 1);
            float angle = Mathf.Lerp(-halfAngle, halfAngle, normalized) + Mathf.Sin(i * 7.13f) * 2.5f;
            Vector2 rayDirection = Rotate(direction, angle);
            float length = range * Mathf.Lerp(0.72f, 1.08f, Hash01(i, 3)) * expansion;
            Vector3 end = center + (Vector3)(rayDirection * length);
            ray.SetPosition(0, origin);
            ray.SetPosition(1, end);

            Color start = new Color(1f, 0.88f, 0.42f, 0.88f * fade);
            Color finish = new Color(1f, 0.18f, 0.02f, 0f);
            ray.startColor = start;
            ray.endColor = finish;
            ray.widthMultiplier = Mathf.Lerp(0.08f, 0.012f, t) * Mathf.Lerp(0.78f, 1.22f, Hash01(i, 9));
        }

        UpdateFrontArc(expansion, fade);
        UpdateCoreJet(expansion, fade);
    }

    void UpdateFrontArc(float expansion, float fade)
    {
        if (frontArc == null)
            return;

        float currentRange = range * expansion;
        for (int i = 0; i < frontArc.positionCount; i++)
        {
            float normalized = frontArc.positionCount <= 1 ? 0.5f : i / (float)(frontArc.positionCount - 1);
            float angle = Mathf.Lerp(-halfAngle, halfAngle, normalized);
            Vector2 arcDirection = Rotate(direction, angle);
            Vector3 point = center + (Vector3)(arcDirection * currentRange);
            frontArc.SetPosition(i, point);
        }

        Color arcColor = new Color(0.52f, 0.96f, 1f, 0.55f * fade);
        frontArc.startColor = arcColor;
        frontArc.endColor = new Color(1f, 0.35f, 0.08f, 0.2f * fade);
        frontArc.widthMultiplier = Mathf.Lerp(0.12f, 0.018f, 1f - fade);
    }

    void UpdateCoreJet(float expansion, float fade)
    {
        if (coreJet == null)
            return;

        Vector3 start = center - (Vector3)(direction * 0.08f);
        Vector3 end = center + (Vector3)(direction * range * Mathf.Lerp(0.42f, 0.92f, expansion));
        coreJet.SetPosition(0, start);
        coreJet.SetPosition(1, end);
        coreJet.startColor = new Color(1f, 0.96f, 0.72f, 0.92f * fade);
        coreJet.endColor = new Color(0.4f, 0.86f, 1f, 0f);
        coreJet.widthMultiplier = Mathf.Lerp(0.22f, 0.035f, 1f - fade);
    }

    LineRenderer CreateLine(string objectName, float width, bool loop, int positionCount, int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = Mathf.Max(2, positionCount);
        line.widthMultiplier = width;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 5;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetMaterial();
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
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
            name = "SpaceTorpedoConeExplosionVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 5000;
        return sharedMaterial;
    }

    static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }

    static float Hash01(int index, int salt)
    {
        return Mathf.Repeat(Mathf.Sin(index * 127.1f + salt * 311.7f) * 43758.5453f, 1f);
    }
}
