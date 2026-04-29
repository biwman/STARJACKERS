using System.Collections.Generic;
using UnityEngine;

public sealed class RepairBay : MonoBehaviour
{
    public const float StationMaxWorldSize = 7.2f;
    public const float InteractionRadius = 1.75f;
    const float LandingOffsetXFactor = -0.30f;
    const float LandingOffsetYFactor = 0.02f;
    const float DriftAmplitude = 0.42f;
    const float DriftSpeed = 0.08f;

    static readonly Dictionary<string, RepairBay> BaysById = new Dictionary<string, RepairBay>();

    string stableId;
    Vector2 anchorPosition;
    float driftPhase;
    SpriteRenderer spriteRenderer;
    CircleCollider2D interactionTrigger;

    public string StableId => stableId;
    public Vector3 LandingPoint => ResolveLandingPoint();

    public static RepairBay Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        BaysById.TryGetValue(id, out RepairBay bay);
        return bay;
    }

    public static RepairBay FindClosestUsable(Vector2 position)
    {
        RepairBay best = null;
        float bestDistance = float.MaxValue;
        foreach (RepairBay bay in BaysById.Values)
        {
            if (bay == null)
                continue;

            float distance = Vector2.Distance(position, bay.LandingPoint);
            if (distance <= InteractionRadius && distance < bestDistance)
            {
                best = bay;
                bestDistance = distance;
            }
        }

        return best;
    }

    public void Configure(string id, Vector2 position, float phase)
    {
        if (!string.IsNullOrWhiteSpace(stableId))
            BaysById.Remove(stableId);

        stableId = id;
        anchorPosition = position;
        driftPhase = phase;
        BaysById[stableId] = this;
        EnsureVisuals();
        UpdatePosition();
    }

    void Awake()
    {
        EnsureVisuals();
    }

    void OnDestroy()
    {
        if (!string.IsNullOrWhiteSpace(stableId))
            BaysById.Remove(stableId);
    }

    void Update()
    {
        UpdatePosition();
    }

    void EnsureVisuals()
    {
        SpriteRenderer renderer;
        if (!TryGetComponent(out renderer) || renderer == null)
            renderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer = renderer;

        if (renderer.sprite == null)
            renderer.sprite = Resources.Load<Sprite>("stacja_naprawcza_resource");

        renderer.color = Color.white;
        renderer.sortingLayerName = "Ground";
        renderer.sortingOrder = 2;

        if (renderer.sprite != null)
        {
            float maxDimension = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
            float scale = maxDimension > 0.001f ? StationMaxWorldSize / maxDimension : 1f;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        CircleCollider2D trigger;
        if (!TryGetComponent(out trigger) || trigger == null)
            trigger = gameObject.AddComponent<CircleCollider2D>();

        interactionTrigger = trigger;
        interactionTrigger.isTrigger = true;
        float worldScale = Mathf.Max(0.001f, Mathf.Abs(transform.localScale.x));
        interactionTrigger.radius = InteractionRadius / worldScale;
    }

    void UpdatePosition()
    {
        float time = Photon.Pun.PhotonNetwork.InRoom ? (float)Photon.Pun.PhotonNetwork.Time : Time.time;
        Vector2 drift = new Vector2(
            Mathf.Sin((time * DriftSpeed) + driftPhase),
            Mathf.Cos((time * DriftSpeed * 0.73f) + driftPhase * 1.37f)) * DriftAmplitude;

        transform.position = new Vector3(anchorPosition.x + drift.x, anchorPosition.y + drift.y, 0.45f);
        transform.rotation = Quaternion.identity;
    }

    Vector3 ResolveLandingPoint()
    {
        if (spriteRenderer == null)
            return transform.position;

        Bounds bounds = spriteRenderer.bounds;
        return transform.position + new Vector3(bounds.size.x * LandingOffsetXFactor, bounds.size.y * LandingOffsetYFactor, -0.05f);
    }
}
