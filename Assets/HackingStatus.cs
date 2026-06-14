using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HackingStatus : MonoBehaviour
{
    const float DirectionIntervalMin = 0.28f;
    const float DirectionIntervalMax = 0.82f;
    const float ShotIntervalMin = 0.26f;
    const float ShotIntervalMax = 0.74f;
    const int RingPointCount = 48;

    static Material sharedLineMaterial;

    PhotonView view;
    SpriteRenderer[] spriteRenderers;
    Color[] originalColors;
    LineRenderer outerRing;
    LineRenderer innerRing;
    float activeUntil;
    float nextDirectionAt;
    float nextShotAt;
    float forcedSpeedMultiplier = 1f;
    int sourceViewId;
    Vector2 forcedMoveDirection = Vector2.up;

    public bool IsActive => Time.time < activeUntil;

    public static HackingStatus Attach(GameObject target, float duration, int sourcePhotonViewId)
    {
        if (target == null || duration <= 0f)
            return null;

        HackingStatus status = target.GetComponent<HackingStatus>();
        if (status == null)
            status = target.AddComponent<HackingStatus>();

        status.Activate(duration, sourcePhotonViewId);
        return status;
    }

    public static bool IsActiveOn(GameObject target)
    {
        HackingStatus status = target != null ? target.GetComponent<HackingStatus>() : null;
        return status != null && status.IsActive;
    }

    public static bool TryGetForcedMotion(GameObject target, out Vector2 direction, out float speedMultiplier)
    {
        direction = Vector2.zero;
        speedMultiplier = 1f;

        HackingStatus status = target != null ? target.GetComponent<HackingStatus>() : null;
        if (status == null || !status.IsActive || !status.CanDriveLocalControl())
            return false;

        status.RefreshForcedMotion();
        direction = status.forcedMoveDirection.sqrMagnitude > 0.001f
            ? status.forcedMoveDirection.normalized
            : Vector2.up;
        speedMultiplier = Mathf.Clamp(status.forcedSpeedMultiplier, 0.35f, 1.15f);
        return true;
    }

    public static bool TryConsumeRandomShot(GameObject target, out Vector2 direction)
    {
        direction = Vector2.zero;

        HackingStatus status = target != null ? target.GetComponent<HackingStatus>() : null;
        if (status == null || !status.IsActive || !status.CanDriveLocalControl())
            return false;

        if (Time.time < status.nextShotAt)
            return false;

        direction = Random.insideUnitCircle;
        if (direction.sqrMagnitude <= 0.001f)
            direction = status.forcedMoveDirection.sqrMagnitude > 0.001f ? status.forcedMoveDirection : Vector2.up;

        direction.Normalize();
        status.nextShotAt = Time.time + Random.Range(ShotIntervalMin, ShotIntervalMax);
        return true;
    }

    void Awake()
    {
        view = GetComponent<PhotonView>();
    }

    void Update()
    {
        if (!IsActive)
        {
            Destroy(this);
            return;
        }

        RefreshForcedMotion();
        UpdateVisuals();
    }

    void OnDestroy()
    {
        RestoreRendererColors();
        DestroyLine(outerRing);
        DestroyLine(innerRing);
    }

    void Activate(float duration, int sourcePhotonViewId)
    {
        view = view != null ? view : GetComponent<PhotonView>();
        sourceViewId = sourcePhotonViewId;
        activeUntil = Mathf.Max(activeUntil, Time.time + Mathf.Max(0.05f, duration));
        nextDirectionAt = 0f;
        nextShotAt = Time.time + Random.Range(ShotIntervalMin * 0.55f, ShotIntervalMax);
        CacheRenderers();
        EnsureRings();
        RefreshForcedMotion(true);
    }

    bool CanDriveLocalControl()
    {
        if (GetComponent<EnemyBot>() != null)
            return false;

        return view == null || view.IsMine;
    }

    void RefreshForcedMotion(bool force = false)
    {
        if (!force && Time.time < nextDirectionAt && forcedMoveDirection.sqrMagnitude > 0.001f)
            return;

        forcedMoveDirection = Random.insideUnitCircle;
        if (forcedMoveDirection.sqrMagnitude <= 0.001f)
            forcedMoveDirection = Vector2.up;

        forcedMoveDirection.Normalize();
        forcedSpeedMultiplier = Random.Range(0.46f, 1.08f);
        nextDirectionAt = Time.time + Random.Range(DirectionIntervalMin, DirectionIntervalMax);
    }

    void CacheRenderers()
    {
        if (spriteRenderers != null && spriteRenderers.Length > 0)
            return;

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
            originalColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
    }

    void RestoreRendererColors()
    {
        if (spriteRenderers == null || originalColors == null)
            return;

        int count = Mathf.Min(spriteRenderers.Length, originalColors.Length);
        for (int i = 0; i < count; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];
        }
    }

    void EnsureRings()
    {
        if (outerRing != null && innerRing != null)
            return;

        SpriteRenderer reference = null;
        if (spriteRenderers != null)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    reference = spriteRenderers[i];
                    break;
                }
            }
        }

        int sortingLayerId = reference != null ? reference.sortingLayerID : 0;
        int sortingOrder = reference != null ? reference.sortingOrder + 420 : 2600;
        outerRing = CreateRing("HackingStatusOuterRing", sortingLayerId, sortingOrder, 0.045f);
        innerRing = CreateRing("HackingStatusInnerRing", sortingLayerId, sortingOrder + 1, 0.028f);
    }

    void UpdateVisuals()
    {
        CacheRenderers();
        float pulse = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(Time.time * 21f + sourceViewId * 0.07f));
        float glitch = Mathf.Repeat(Time.time * 19f + sourceViewId * 0.013f, 1f) < 0.22f ? 1f : 0f;
        Color cyan = new Color(0.1f, 0.95f, 1f, 1f);
        Color magenta = new Color(1f, 0.12f, 0.72f, 1f);

        if (spriteRenderers != null && originalColors != null)
        {
            int count = Mathf.Min(spriteRenderers.Length, originalColors.Length);
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer renderer = spriteRenderers[i];
                if (renderer == null)
                    continue;

                Color original = originalColors[i];
                Color target = glitch > 0f ? magenta : Color.Lerp(cyan, magenta, pulse * 0.35f);
                Color color = Color.Lerp(original, target, Mathf.Lerp(0.18f, 0.42f, pulse) + glitch * 0.22f);
                color.a = original.a;
                renderer.color = color;
            }
        }

        float radius = ResolveRadius();
        UpdateRing(outerRing, radius * Mathf.Lerp(1.05f, 1.18f, pulse), new Color(0.08f, 0.95f, 1f, Mathf.Lerp(0.22f, 0.62f, pulse)));
        UpdateRing(innerRing, radius * Mathf.Lerp(0.66f, 0.78f, 1f - pulse), new Color(1f, 0.12f, 0.78f, Mathf.Lerp(0.16f, 0.48f, 1f - pulse)));
    }

    float ResolveRadius()
    {
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
            return Mathf.Clamp(Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y) * 1.18f, 0.55f, 3.3f);

        SpriteRenderer renderer = spriteRenderers != null && spriteRenderers.Length > 0 ? spriteRenderers[0] : null;
        if (renderer != null)
            return Mathf.Clamp(Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y) * 1.18f, 0.55f, 3.3f);

        return 0.86f;
    }

    LineRenderer CreateRing(string objectName, int sortingLayerId, int sortingOrder, float width)
    {
        GameObject ringObject = new GameObject(objectName);
        ringObject.transform.SetParent(transform, false);
        ringObject.layer = gameObject.layer;

        LineRenderer line = ringObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = RingPointCount;
        line.widthMultiplier = width;
        line.numCapVertices = 6;
        line.numCornerVertices = 6;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetLineMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        return line;
    }

    void UpdateRing(LineRenderer line, float radius, Color color)
    {
        if (line == null)
            return;

        line.enabled = true;
        float phase = Time.time * 2.7f + sourceViewId * 0.01f;
        Vector3 center = transform.position;
        center.z -= 0.09f;
        for (int i = 0; i < RingPointCount; i++)
        {
            float t = i / (float)RingPointCount;
            float angle = (t * Mathf.PI * 2f) + phase;
            float notch = Mathf.Repeat(t * 10f + Time.time * 1.7f, 1f) < 0.18f ? 0.82f : 1f;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (radius * notch);
            line.SetPosition(i, point);
        }

        line.startColor = color;
        line.endColor = color;
    }

    static void DestroyLine(LineRenderer line)
    {
        if (line != null)
            Destroy(line.gameObject);
    }

    static Material GetLineMaterial()
    {
        if (sharedLineMaterial != null)
            return sharedLineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        sharedLineMaterial = new Material(shader)
        {
            name = "HackingStatusLineMaterial",
            color = Color.white
        };
        sharedLineMaterial.renderQueue = 3400;
        return sharedLineMaterial;
    }
}
