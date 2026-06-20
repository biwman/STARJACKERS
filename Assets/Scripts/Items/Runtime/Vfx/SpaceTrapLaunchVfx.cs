using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class SpaceTrapLaunchVfx : MonoBehaviour
{
    const float TravelDuration = 0.28f;

    Vector3 start;
    Vector3 end;
    float startedAt;
    SpriteRenderer spriteRenderer;

    public static void Spawn(Vector3 from, Vector3 to)
    {
        GameObject effect = new GameObject("SpaceTrapLaunchVfx");
        SpaceTrapLaunchVfx vfx = effect.AddComponent<SpaceTrapLaunchVfx>();
        vfx.Initialize(from, to);
    }

    void Initialize(Vector3 from, Vector3 to)
    {
        start = new Vector3(from.x, from.y, -0.36f);
        end = new Vector3(to.x, to.y, -0.36f);
        startedAt = Time.time;
        transform.position = start;
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = RuntimeSpriteUtility.LoadSprite("space_trap_top_down_resource", "Assets/space_trap_top_down.png");
        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = 86;
        spriteRenderer.color = Color.white;
        RuntimeSpriteUtility.FitRenderer(spriteRenderer, 0.42f);
    }

    void Update()
    {
        float t = Mathf.Clamp01((Time.time - startedAt) / TravelDuration);
        Vector3 arc = Vector3.Lerp(start, end, t);
        Vector2 flatDirection = end - start;
        Vector2 perpendicular = flatDirection.sqrMagnitude > 0.001f ? new Vector2(-flatDirection.y, flatDirection.x).normalized : Vector2.up;
        float wobble = Mathf.Sin(t * Mathf.PI) * 0.18f;
        transform.position = arc + (Vector3)(perpendicular * wobble);

        Vector2 direction = (Vector2)(end - start);
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = Mathf.Lerp(1f, 0.15f, t);
            spriteRenderer.color = color;
        }

        if (t >= 1f)
            Destroy(gameObject);
    }
}
