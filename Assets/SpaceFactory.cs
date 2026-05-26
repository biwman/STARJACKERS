using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

public sealed class SpaceFactory : MonoBehaviourPunCallbacks
{
    public const float FactoryMaxWorldSize = 9.6f;
    public const float InteractionRadius = 1.9f;
    public const int RequiredContainerCount = 6;

    const float LandingOffsetXFactor = -0.175f;
    const float LandingOffsetYFactor = 0f;
    const float DriftAmplitude = 0.18f;
    const float DriftSpeed = 0.028f;
    const float ContainerIconWorldSizeFactor = 0.075f;

    static readonly Vector2[] ContainerSlotOffsetFactors =
    {
        new Vector2(0.175f, -0.055f),
        new Vector2(0.308f, -0.055f),
        new Vector2(0.175f, -0.158f),
        new Vector2(0.308f, -0.158f),
        new Vector2(0.175f, -0.262f),
        new Vector2(0.308f, -0.262f)
    };

    static readonly Dictionary<string, SpaceFactory> FactoriesById = new Dictionary<string, SpaceFactory>();
    static bool containerIconsPrewarmed;

    string stableId;
    Vector2 anchorPosition;
    float driftPhase;
    SpriteRenderer spriteRenderer;
    CircleCollider2D interactionTrigger;
    SpriteRenderer[] containerSlotRenderers;

    public string StableId => stableId;
    public Vector3 LandingPoint => ResolveLandingPoint();

    public static SpaceFactory Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        FactoriesById.TryGetValue(id, out SpaceFactory factory);
        return factory;
    }

    public static void ClearAllRuntimeFactories()
    {
        List<SpaceFactory> factories = new List<SpaceFactory>(FactoriesById.Values);
        FactoriesById.Clear();

        for (int i = 0; i < factories.Count; i++)
        {
            SpaceFactory factory = factories[i];
            if (factory != null && factory.gameObject != null && factory.gameObject.scene.IsValid())
                Destroy(factory.gameObject);
        }
    }

    public static SpaceFactory FindClosestUsable(Vector2 position)
    {
        SpaceFactory best = null;
        float bestDistance = float.MaxValue;
        foreach (SpaceFactory factory in FactoriesById.Values)
        {
            if (factory == null)
                continue;

            int localActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
            if (factory.IsOccupiedByOther(localActorNumber))
                continue;

            float distance = Vector2.Distance(position, factory.LandingPoint);
            if (distance <= InteractionRadius && distance < bestDistance)
            {
                best = factory;
                bestDistance = distance;
            }
        }

        return best;
    }

    public static bool IsCompleted(string factoryId)
    {
        Dictionary<string, FactoryProgress> state = DeserializeFactoryState(GetFactoryStateRaw());
        return state.TryGetValue(factoryId, out FactoryProgress progress) && progress.Completed;
    }

    public static int GetFilledCount(string factoryId)
    {
        Dictionary<string, FactoryProgress> state = DeserializeFactoryState(GetFactoryStateRaw());
        return state.TryGetValue(factoryId, out FactoryProgress progress) ? Mathf.Min(progress.ContainerIds.Count, RequiredContainerCount) : 0;
    }

    public static bool CanAcceptContainer(string factoryId)
    {
        Dictionary<string, FactoryProgress> state = DeserializeFactoryState(GetFactoryStateRaw());
        if (!state.TryGetValue(factoryId, out FactoryProgress progress))
            return true;

        return !progress.Completed && progress.ContainerIds.Count < RequiredContainerCount;
    }

    public static bool TryDepositContainerAuthority(string factoryId, string itemId, out bool completedNow)
    {
        completedNow = false;
        bool accepted = TryDepositContainersAuthority(factoryId, new[] { itemId }, out int acceptedCount, out completedNow);
        return accepted && acceptedCount > 0;
    }

    public static bool TryDepositContainersAuthority(string factoryId, string[] itemIds, out int acceptedCount, out bool completedNow)
    {
        acceptedCount = 0;
        completedNow = false;
        if (!PhotonNetwork.IsMasterClient ||
            PhotonNetwork.CurrentRoom == null ||
            string.IsNullOrWhiteSpace(factoryId) ||
            itemIds == null ||
            itemIds.Length == 0)
        {
            return false;
        }

        Dictionary<string, FactoryProgress> state = DeserializeFactoryState(GetFactoryStateRaw());
        if (!state.TryGetValue(factoryId, out FactoryProgress progress))
        {
            progress = new FactoryProgress();
            state[factoryId] = progress;
        }

        if (progress.Completed || progress.ContainerIds.Count >= RequiredContainerCount)
            return false;

        for (int i = 0; i < itemIds.Length && progress.ContainerIds.Count < RequiredContainerCount; i++)
        {
            string itemId = itemIds[i];
            if (!InventoryItemCatalog.IsContainerItem(itemId))
                continue;

            progress.ContainerIds.Add(itemId);
            acceptedCount++;
        }

        if (acceptedCount <= 0)
            return false;

        if (progress.ContainerIds.Count >= RequiredContainerCount)
        {
            progress.Completed = true;
            completedNow = true;
        }

        PublishFactoryState(state);
        RefreshFactoryContainerVisuals(factoryId);
        return true;
    }

    public bool IsOccupiedByOther(int actorNumber)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return false;

        Dictionary<string, int> occupancy = DeserializeOccupancy(GetFactoryOccupancyStateRaw());
        if (!occupancy.TryGetValue(stableId, out int occupiedByActor))
            return false;

        return occupiedByActor > 0 && occupiedByActor != actorNumber;
    }

    public void Configure(string id, Vector2 position, float phase)
    {
        if (!string.IsNullOrWhiteSpace(stableId))
            FactoriesById.Remove(stableId);

        stableId = id;
        anchorPosition = position;
        driftPhase = phase;
        FactoriesById[stableId] = this;
        EnsureVisuals();
        UpdatePosition();
        RefreshContainerVisuals();
    }

    void Awake()
    {
        EnsureVisuals();
    }

    void OnDestroy()
    {
        if (!string.IsNullOrWhiteSpace(stableId))
            FactoriesById.Remove(stableId);
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(RoomSettings.SpaceFactoryStateKey))
            RefreshContainerVisuals();
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
            spriteRenderer.sprite = Resources.Load<Sprite>("space_factory");

        spriteRenderer.color = Color.white;
        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = GameVisualTheme.RepairBaySortingOrder;

        if (spriteRenderer.sprite != null)
        {
            float maxDimension = Mathf.Max(spriteRenderer.sprite.bounds.size.x, spriteRenderer.sprite.bounds.size.y);
            float scale = maxDimension > 0.001f ? FactoryMaxWorldSize / maxDimension : 1f;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        if (!TryGetComponent(out interactionTrigger) || interactionTrigger == null)
            interactionTrigger = gameObject.AddComponent<CircleCollider2D>();

        interactionTrigger.isTrigger = true;
        float worldScale = Mathf.Max(0.001f, Mathf.Abs(transform.localScale.x));
        interactionTrigger.radius = InteractionRadius / worldScale;
        interactionTrigger.offset = GetLandingLocalOffset();

        PrewarmContainerIcons();
        EnsureContainerSlotRenderers();
    }

    void EnsureContainerSlotRenderers()
    {
        if (containerSlotRenderers != null && containerSlotRenderers.Length == RequiredContainerCount)
            return;

        containerSlotRenderers = new SpriteRenderer[RequiredContainerCount];
        for (int i = 0; i < RequiredContainerCount; i++)
        {
            Transform existing = transform.Find("SpaceFactoryContainerSlot_" + i);
            GameObject slotObject = existing != null ? existing.gameObject : new GameObject("SpaceFactoryContainerSlot_" + i);
            slotObject.transform.SetParent(transform, false);
            slotObject.transform.localPosition = GetContainerSlotLocalPosition(i);
            slotObject.transform.localRotation = Quaternion.identity;

            SpriteRenderer renderer = slotObject.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = slotObject.AddComponent<SpriteRenderer>();

            renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            renderer.sortingOrder = GameVisualTheme.RepairBaySortingOrder + 1;
            renderer.color = Color.white;
            renderer.enabled = false;
            containerSlotRenderers[i] = renderer;
        }
    }

    void UpdatePosition()
    {
        float time = PhotonNetwork.InRoom ? (float)PhotonNetwork.Time : Time.time;
        Vector2 drift = new Vector2(
            Mathf.Sin((time * DriftSpeed) + driftPhase),
            Mathf.Cos((time * DriftSpeed * 0.61f) + driftPhase * 1.23f)) * DriftAmplitude;

        transform.position = new Vector3(anchorPosition.x + drift.x, anchorPosition.y + drift.y, 0.42f);
        transform.rotation = Quaternion.identity;
    }

    void RefreshContainerVisuals()
    {
        EnsureContainerSlotRenderers();
        Dictionary<string, FactoryProgress> state = DeserializeFactoryState(GetFactoryStateRaw());
        state.TryGetValue(stableId, out FactoryProgress progress);

        for (int i = 0; i < containerSlotRenderers.Length; i++)
        {
            SpriteRenderer renderer = containerSlotRenderers[i];
            if (renderer == null)
                continue;

            string itemId = progress != null && i < progress.ContainerIds.Count ? progress.ContainerIds[i] : null;
            Sprite sprite = !string.IsNullOrWhiteSpace(itemId) ? InventoryItemCatalog.GetIcon(itemId) : null;
            renderer.sprite = sprite;
            renderer.enabled = sprite != null;
            renderer.transform.localPosition = GetContainerSlotLocalPosition(i);

            if (sprite != null)
                FitContainerIcon(renderer);
        }
    }

    static void RefreshFactoryContainerVisuals(string factoryId)
    {
        SpaceFactory factory = Find(factoryId);
        if (factory != null)
            factory.RefreshContainerVisuals();
    }

    static void PrewarmContainerIcons()
    {
        if (containerIconsPrewarmed)
            return;

        containerIconsPrewarmed = true;
        for (int i = 0; i < InventoryItemCatalog.ContainerVariantCount; i++)
            InventoryItemCatalog.GetIcon(InventoryItemCatalog.GetContainerItemId(i));
    }

    void FitContainerIcon(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null || spriteRenderer == null)
            return;

        float targetWorldSize = Mathf.Max(0.2f, spriteRenderer.bounds.size.x * ContainerIconWorldSizeFactor);
        float parentScale = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x));
        float maxDimension = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        float localScale = maxDimension > 0.001f ? targetWorldSize / (maxDimension * parentScale) : 1f;
        renderer.transform.localScale = new Vector3(localScale, localScale, 1f);
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

    Vector3 GetContainerSlotLocalPosition(int index)
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return Vector3.zero;

        int safeIndex = Mathf.Clamp(index, 0, ContainerSlotOffsetFactors.Length - 1);
        Vector2 size = spriteRenderer.sprite.bounds.size;
        Vector2 factor = ContainerSlotOffsetFactors[safeIndex];
        return new Vector3(size.x * factor.x, size.y * factor.y, -0.04f);
    }

    static string GetFactoryStateRaw()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.SpaceFactoryStateKey, out object value) &&
            value is string raw)
        {
            return raw;
        }

        return string.Empty;
    }

    static string GetFactoryOccupancyStateRaw()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.SpaceFactoryOccupancyStateKey, out object value) &&
            value is string raw)
        {
            return raw;
        }

        return string.Empty;
    }

    static Dictionary<string, FactoryProgress> DeserializeFactoryState(string raw)
    {
        Dictionary<string, FactoryProgress> state = new Dictionary<string, FactoryProgress>(System.StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw))
            return state;

        string[] entries = raw.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            int separatorIndex = entry.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex >= entry.Length - 1)
                continue;

            string factoryId = entry.Substring(0, separatorIndex);
            string[] parts = entry.Substring(separatorIndex + 1).Split('|');
            if (string.IsNullOrWhiteSpace(factoryId) || parts.Length == 0)
                continue;

            FactoryProgress progress = new FactoryProgress
            {
                Completed = parts[0] == "1"
            };

            for (int partIndex = 1; partIndex < parts.Length && progress.ContainerIds.Count < RequiredContainerCount; partIndex++)
            {
                string itemId = parts[partIndex];
                if (InventoryItemCatalog.IsContainerItem(itemId))
                    progress.ContainerIds.Add(itemId);
            }

            if (progress.ContainerIds.Count >= RequiredContainerCount)
                progress.Completed = true;

            state[factoryId] = progress;
        }

        return state;
    }

    static void PublishFactoryState(Dictionary<string, FactoryProgress> state)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable
        {
            [RoomSettings.SpaceFactoryStateKey] = SerializeFactoryState(state)
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    static string SerializeFactoryState(Dictionary<string, FactoryProgress> state)
    {
        if (state == null || state.Count == 0)
            return string.Empty;

        List<string> factoryIds = new List<string>(state.Keys);
        factoryIds.Sort(System.StringComparer.Ordinal);

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < factoryIds.Count; i++)
        {
            string factoryId = factoryIds[i];
            if (string.IsNullOrWhiteSpace(factoryId) || !state.TryGetValue(factoryId, out FactoryProgress progress) || progress == null)
                continue;

            if (builder.Length > 0)
                builder.Append(';');

            builder.Append(factoryId);
            builder.Append('=');
            builder.Append(progress.Completed ? '1' : '0');
            for (int itemIndex = 0; itemIndex < progress.ContainerIds.Count && itemIndex < RequiredContainerCount; itemIndex++)
            {
                builder.Append('|');
                builder.Append(progress.ContainerIds[itemIndex]);
            }
        }

        return builder.ToString();
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

    sealed class FactoryProgress
    {
        public bool Completed;
        public readonly List<string> ContainerIds = new List<string>();
    }
}
