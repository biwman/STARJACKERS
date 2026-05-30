using System.Collections.Generic;
using System.Globalization;
using Photon.Pun;
using UnityEngine;

public sealed class ScienceStation : MonoBehaviour
{
    public const float StationMaxWorldSize = 8.4f;
    public const float InteractionRadius = 1.9f;

    const float LandingOffsetXFactor = 0.28f;
    const float LandingOffsetYFactor = -0.02f;
    const float DriftAmplitude = 0.22f;
    const float DriftSpeed = 0.035f;

    static readonly Dictionary<string, ScienceStation> StationsById = new Dictionary<string, ScienceStation>();

    string stableId;
    Vector2 anchorPosition;
    float driftPhase;
    SpriteRenderer spriteRenderer;
    CircleCollider2D interactionTrigger;

    public string StableId => stableId;
    public Vector3 LandingPoint => ResolveLandingPoint();

    public static ScienceStation Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        StationsById.TryGetValue(id, out ScienceStation station);
        return station;
    }

    public static void ClearAllRuntimeStations()
    {
        List<ScienceStation> stations = new List<ScienceStation>(StationsById.Values);
        StationsById.Clear();

        for (int i = 0; i < stations.Count; i++)
        {
            ScienceStation station = stations[i];
            if (station != null && station.gameObject != null && station.gameObject.scene.IsValid())
                Destroy(station.gameObject);
        }
    }

    public static ScienceStation FindClosestUsable(Vector2 position)
    {
        ScienceStation best = null;
        float bestDistance = float.MaxValue;
        foreach (ScienceStation station in StationsById.Values)
        {
            if (station == null)
                continue;

            int localActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
            if (station.IsOccupiedByOther(localActorNumber))
                continue;

            float distance = Vector2.Distance(position, station.LandingPoint);
            if (distance <= InteractionRadius && distance < bestDistance)
            {
                best = station;
                bestDistance = distance;
            }
        }

        return best;
    }

    public bool IsOccupiedByOther(int actorNumber)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return false;

        Dictionary<string, int> occupancy = DeserializeOccupancy(GetOccupancyStateRaw());
        if (!occupancy.TryGetValue(stableId, out int occupiedByActor))
            return false;

        return occupiedByActor > 0 && occupiedByActor != actorNumber;
    }

    public void Configure(string id, Vector2 position, float phase)
    {
        if (!string.IsNullOrWhiteSpace(stableId))
            StationsById.Remove(stableId);

        stableId = id;
        anchorPosition = position;
        driftPhase = phase;
        StationsById[stableId] = this;
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
            StationsById.Remove(stableId);
    }

    void Update()
    {
        UpdatePosition();
    }

    void EnsureVisuals()
    {
        if (!TryGetComponent(out spriteRenderer) || spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (spriteRenderer.sprite == null)
            spriteRenderer.sprite = LoadScienceStationSprite();

        spriteRenderer.color = Color.white;
        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = GameVisualTheme.RepairBaySortingOrder;

        if (spriteRenderer.sprite != null)
        {
            float maxDimension = Mathf.Max(spriteRenderer.sprite.bounds.size.x, spriteRenderer.sprite.bounds.size.y);
            float scale = maxDimension > 0.001f ? StationMaxWorldSize / maxDimension : 1f;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        if (!TryGetComponent(out interactionTrigger) || interactionTrigger == null)
            interactionTrigger = gameObject.AddComponent<CircleCollider2D>();

        interactionTrigger.isTrigger = true;
        float worldScale = Mathf.Max(0.001f, Mathf.Abs(transform.localScale.x));
        interactionTrigger.radius = InteractionRadius / worldScale;
        interactionTrigger.offset = GetLandingLocalOffset();
    }

    void UpdatePosition()
    {
        float time = PhotonNetwork.InRoom ? (float)PhotonNetwork.Time : Time.time;
        Vector2 drift = new Vector2(
            Mathf.Sin((time * DriftSpeed) + driftPhase),
            Mathf.Cos((time * DriftSpeed * 0.67f) + driftPhase * 1.19f)) * DriftAmplitude;

        transform.position = new Vector3(anchorPosition.x + drift.x, anchorPosition.y + drift.y, 0.43f);
        transform.rotation = Quaternion.identity;
    }

    Vector3 ResolveLandingPoint()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return transform.position;

        Vector2 localOffset = GetLandingLocalOffset();
        Vector3 worldOffset = transform.TransformVector(new Vector3(localOffset.x, localOffset.y, -0.05f));
        return transform.position + worldOffset;
    }

    Vector2 GetLandingLocalOffset()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return Vector2.zero;

        Vector2 size = spriteRenderer.sprite.bounds.size;
        return new Vector2(size.x * LandingOffsetXFactor, size.y * LandingOffsetYFactor);
    }

    static Sprite LoadScienceStationSprite()
    {
        Sprite sprite = Resources.Load<Sprite>("science_station");
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>("science_station");
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (candidate == null)
                continue;

            float area = candidate.rect.width * candidate.rect.height;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }

    static string GetOccupancyStateRaw()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.ScienceStationOccupancyStateKey, out object value) &&
            value is string raw)
        {
            return raw;
        }

        return string.Empty;
    }

    static Dictionary<string, int> DeserializeOccupancy(string raw)
    {
        Dictionary<string, int> occupancy = new Dictionary<string, int>(System.StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw))
            return occupancy;

        string[] entries = raw.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            int separatorIndex = entry.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex >= entry.Length - 1)
                continue;

            string id = entry.Substring(0, separatorIndex);
            if (int.TryParse(entry.Substring(separatorIndex + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int actorNumber) &&
                !string.IsNullOrWhiteSpace(id))
            {
                occupancy[id] = actorNumber;
            }
        }

        return occupancy;
    }
}
