using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

public static class MapInstanceService
{
    public const string MainInstanceId = "main";
    public const string HiddenDimensionInstanceId = "hidden_dimension";
    public const string PhotonInstantiationMarker = "map_instance";

    public const string MapTravelLockedKey = "mapTravel.locked";
    public const string MapTravelRiftStateKey = "mapTravel.rift.state";
    public const string MapTravelMessageKey = "mapTravel.message";
    public const string HiddenDimensionActiveKey = "mapInstance.hiddenDimension.active";
    public const string HiddenDimensionCenterXKey = "mapInstance.hiddenDimension.centerX";
    public const string HiddenDimensionCenterYKey = "mapInstance.hiddenDimension.centerY";
    public const string HiddenDimensionObstacleLayoutKey = "mapInstance.hiddenDimension.obstacleLayout";
    public const string HiddenDimensionExtractionLayoutKey = "mapInstance.hiddenDimension.extractionLayout";
    public const string HiddenDimensionNebulaLayoutKey = "mapInstance.hiddenDimension.nebulaLayout";
    public const string HiddenDimensionFireNebulaLayoutKey = "mapInstance.hiddenDimension.fireNebulaLayout";
    public const string HiddenDimensionToxicNebulaLayoutKey = "mapInstance.hiddenDimension.toxicNebulaLayout";
    public const string HiddenDimensionCloudLayoutKey = "mapInstance.hiddenDimension.cloudLayout";
    public const string HiddenDimensionCloudDirectionKey = "mapInstance.hiddenDimension.cloudDirection";
    public const string HiddenDimensionTreasureLayoutKey = "mapInstance.hiddenDimension.treasureLayout";
    public const string HiddenDimensionAlienSecretsLayoutKey = "mapInstance.hiddenDimension.alienSecretsLayout";
    public const string HiddenDimensionNetworkObjectsSpawnedKey = "mapInstance.hiddenDimension.networkObjectsSpawned";

    const float HiddenDimensionDefaultCenterX = 420f;
    const float HiddenDimensionDefaultCenterY = 0f;
    const float InstanceBoundsTolerance = 12f;

    public readonly struct BoundsInfo
    {
        public readonly string InstanceId;
        public readonly string MapId;
        public readonly Vector2 Center;
        public readonly Vector2 Size;
        public readonly Vector2 InnerSize;
        public readonly string ExtractionType;

        public BoundsInfo(string instanceId, string mapId, Vector2 center, Vector2 size, string extractionType)
            : this(instanceId, mapId, center, size, size, extractionType)
        {
        }

        public BoundsInfo(string instanceId, string mapId, Vector2 center, Vector2 size, Vector2 innerSize, string extractionType)
        {
            InstanceId = string.IsNullOrWhiteSpace(instanceId) ? MainInstanceId : instanceId;
            MapId = string.IsNullOrWhiteSpace(mapId) ? RoomSettings.DefaultLobbyMapId : mapId;
            Center = center;
            Size = size;
            InnerSize = innerSize.x > 0f && innerSize.y > 0f ? innerSize : size;
            ExtractionType = string.IsNullOrWhiteSpace(extractionType) ? RoomSettings.DefaultExtractionType : extractionType;
        }
    }

    public static bool IsHiddenDimensionActive()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(HiddenDimensionActiveKey, out object value) &&
               value is bool active &&
               active;
    }

    public static bool TryGetBoundsForInstance(string instanceId, out BoundsInfo bounds)
    {
        if (string.Equals(instanceId, HiddenDimensionInstanceId, System.StringComparison.Ordinal))
        {
            if (IsHiddenDimensionActive())
            {
                LobbyMapDefinition hiddenMap = LobbyMapCatalog.Get(LobbyMapCatalog.HiddenDimensionMapId);
                bool toxicBordersEnabled = hiddenMap == null || hiddenMap.ToxicBordersEnabled;
                bounds = new BoundsInfo(
                    HiddenDimensionInstanceId,
                    LobbyMapCatalog.HiddenDimensionMapId,
                    GetHiddenDimensionCenter(),
                    GetMapDimensions(hiddenMap, toxicBordersEnabled),
                    GetMapDimensions(hiddenMap, false),
                    RoomSettings.ExtractionTypeAncientPortal);
                return true;
            }

            bounds = default;
            return false;
        }

        bounds = new BoundsInfo(
            MainInstanceId,
            RoomSettings.GetSelectedLobbyMapId(),
            Vector2.zero,
            RoomSettings.GetMapDimensions(),
            RoomSettings.GetGameplayMapDimensions(),
            RoomSettings.GetExtractionType());
        return true;
    }

    public static bool TryGetBoundsForWorldPosition(Vector2 position, out BoundsInfo bounds)
    {
        if (TryGetBoundsForInstance(HiddenDimensionInstanceId, out BoundsInfo hiddenBounds) &&
            IsInsideBounds(position, hiddenBounds.Center, hiddenBounds.Size, InstanceBoundsTolerance))
        {
            bounds = hiddenBounds;
            return true;
        }

        return TryGetBoundsForInstance(MainInstanceId, out bounds);
    }

    public static string GetInstanceIdForWorldPosition(Vector2 position)
    {
        return TryGetBoundsForWorldPosition(position, out BoundsInfo bounds)
            ? bounds.InstanceId
            : MainInstanceId;
    }

    public static string GetInstanceIdForTransform(Transform transform)
    {
        if (transform == null)
            return MainInstanceId;

        MapInstanceMember member = transform.GetComponentInParent<MapInstanceMember>();
        if (member != null && !string.IsNullOrWhiteSpace(member.InstanceId))
            return member.InstanceId;

        return GetInstanceIdForWorldPosition(transform.position);
    }

    public static string GetMapIdForTransform(Transform transform)
    {
        if (transform == null)
            return RoomSettings.GetSelectedLobbyMapId();

        return TryGetBoundsForInstance(GetInstanceIdForTransform(transform), out BoundsInfo bounds)
            ? bounds.MapId
            : RoomSettings.GetSelectedLobbyMapId();
    }

    public static string GetMapIdForWorldPosition(Vector2 position)
    {
        return TryGetBoundsForWorldPosition(position, out BoundsInfo bounds)
            ? bounds.MapId
            : RoomSettings.GetSelectedLobbyMapId();
    }

    public static bool IsSameInstance(Vector2 left, Vector2 right)
    {
        return string.Equals(
            GetInstanceIdForWorldPosition(left),
            GetInstanceIdForWorldPosition(right),
            System.StringComparison.Ordinal);
    }

    public static Vector2 ClampToInstanceBounds(Vector2 position, float margin = 0f)
    {
        if (!TryGetBoundsForWorldPosition(position, out BoundsInfo bounds))
            return RoomSettings.ClampToGameplayMapBounds(position, margin);

        float safeMargin = Mathf.Max(0f, margin);
        float halfX = Mathf.Max(0.5f, bounds.Size.x * 0.5f - safeMargin);
        float halfY = Mathf.Max(0.5f, bounds.Size.y * 0.5f - safeMargin);
        return new Vector2(
            Mathf.Clamp(position.x, bounds.Center.x - halfX, bounds.Center.x + halfX),
            Mathf.Clamp(position.y, bounds.Center.y - halfY, bounds.Center.y + halfY));
    }

    public static Vector2 ClampToInstanceInnerBounds(Vector2 position, float margin = 0f)
    {
        if (!TryGetBoundsForWorldPosition(position, out BoundsInfo bounds))
            return RoomSettings.ClampToGameplayMapBounds(position, margin);

        float safeMargin = Mathf.Max(0f, margin);
        float halfX = Mathf.Max(0.5f, bounds.InnerSize.x * 0.5f - safeMargin);
        float halfY = Mathf.Max(0.5f, bounds.InnerSize.y * 0.5f - safeMargin);
        return new Vector2(
            Mathf.Clamp(position.x, bounds.Center.x - halfX, bounds.Center.x + halfX),
            Mathf.Clamp(position.y, bounds.Center.y - halfY, bounds.Center.y + halfY));
    }

    public static Vector2 GetMapDimensions(LobbyMapDefinition map, bool toxicBorders)
    {
        Vector2 size;
        switch (map != null ? map.MapSize : RoomSettings.DefaultMapSize)
        {
            case "small":
                size = new Vector2(24f, 24f);
                break;
            case "large":
                size = new Vector2(38.4f, 38.4f);
                break;
            case "very_large":
                size = new Vector2(48f, 48f);
                break;
            case "super_large":
                size = new Vector2(60f, 60f);
                break;
            default:
                size = new Vector2(30f, 30f);
                break;
        }

        size *= RoomSettings.MapDimensionsScale;
        return toxicBorders ? size * RoomSettings.ToxicBordersMapScale : size;
    }

    public static float GetMapAreaMultiplier(Vector2 mapSize)
    {
        const float baseArea = 25f * 25f;
        float area = Mathf.Max(1f, mapSize.x * mapSize.y);
        return Mathf.Max(0.5f, area / baseArea);
    }

    public static float GetObstacleSizeMultiplierForPosition(Vector2 position)
    {
        if (TryGetBoundsForWorldPosition(position, out BoundsInfo bounds) &&
            string.Equals(bounds.InstanceId, HiddenDimensionInstanceId, System.StringComparison.Ordinal))
        {
            LobbyMapDefinition hiddenMap = LobbyMapCatalog.Get(LobbyMapCatalog.HiddenDimensionMapId);
            return hiddenMap != null ? Mathf.Max(0.1f, hiddenMap.ObstacleSizePercent / 100f) : RoomSettings.GetObstacleSizeMultiplier();
        }

        return RoomSettings.GetObstacleSizeMultiplier();
    }

    public static int GetObstacleMaxSplitCountForPosition(Vector2 position)
    {
        int sizePercent = Mathf.RoundToInt(GetObstacleSizeMultiplierForPosition(position) * 100f);
        if (sizePercent >= 400)
            return 6;

        if (sizePercent >= 200)
            return 5;

        return 4;
    }

    public static bool TryGetExtractionTypeForObject(GameObject target, out string extractionType)
    {
        extractionType = RoomSettings.GetExtractionType();
        if (target == null)
            return false;

        MapInstanceMember member = target.GetComponent<MapInstanceMember>();
        if (member != null && !string.IsNullOrWhiteSpace(member.ExtractionTypeOverride))
        {
            extractionType = RoomSettings.NormalizeExtractionType(member.ExtractionTypeOverride);
            return true;
        }

        if (TryGetBoundsForWorldPosition(target.transform.position, out BoundsInfo bounds))
        {
            extractionType = RoomSettings.NormalizeExtractionType(bounds.ExtractionType);
            return true;
        }

        return false;
    }

    public static void ConfigureMember(GameObject target, string instanceId, string extractionTypeOverride = "")
    {
        if (target == null)
            return;

        MapInstanceMember member = target.GetComponent<MapInstanceMember>();
        if (member == null)
            member = target.AddComponent<MapInstanceMember>();

        member.Configure(instanceId, extractionTypeOverride);
    }

    public static bool TryReadPhotonInstantiationData(object[] data, out string instanceId, out string extractionType)
    {
        instanceId = string.Empty;
        extractionType = string.Empty;
        if (data == null || data.Length < 2 || data[0] is not string marker ||
            !string.Equals(marker, PhotonInstantiationMarker, System.StringComparison.Ordinal))
        {
            return false;
        }

        instanceId = data[1] as string ?? string.Empty;
        if (data.Length > 2)
            extractionType = data[2] as string ?? string.Empty;

        return !string.IsNullOrWhiteSpace(instanceId);
    }

    public static Hashtable BuildClearRoundProperties()
    {
        Hashtable props = new Hashtable();
        AppendClearRoundProperties(props);
        return props;
    }

    public static void AppendClearRoundProperties(Hashtable props)
    {
        if (props == null)
            return;

        props[MapTravelLockedKey] = false;
        props[MapTravelRiftStateKey] = string.Empty;
        props[MapTravelMessageKey] = string.Empty;
        props[HiddenDimensionActiveKey] = false;
        props[HiddenDimensionCenterXKey] = HiddenDimensionDefaultCenterX;
        props[HiddenDimensionCenterYKey] = HiddenDimensionDefaultCenterY;
        props[HiddenDimensionObstacleLayoutKey] = string.Empty;
        props[HiddenDimensionExtractionLayoutKey] = string.Empty;
        props[HiddenDimensionNebulaLayoutKey] = string.Empty;
        props[HiddenDimensionFireNebulaLayoutKey] = string.Empty;
        props[HiddenDimensionToxicNebulaLayoutKey] = string.Empty;
        props[HiddenDimensionCloudLayoutKey] = string.Empty;
        props[HiddenDimensionCloudDirectionKey] = string.Empty;
        props[HiddenDimensionTreasureLayoutKey] = string.Empty;
        props[HiddenDimensionAlienSecretsLayoutKey] = string.Empty;
        props[HiddenDimensionNetworkObjectsSpawnedKey] = false;
    }

    static Vector2 GetHiddenDimensionCenter()
    {
        float x = ReadRoomFloat(HiddenDimensionCenterXKey, HiddenDimensionDefaultCenterX);
        float y = ReadRoomFloat(HiddenDimensionCenterYKey, HiddenDimensionDefaultCenterY);
        return new Vector2(x, y);
    }

    static float ReadRoomFloat(string key, float fallback)
    {
        if (PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value))
        {
            return fallback;
        }

        return value switch
        {
            float asFloat => asFloat,
            double asDouble => (float)asDouble,
            int asInt => asInt,
            _ => fallback
        };
    }

    static bool IsInsideBounds(Vector2 position, Vector2 center, Vector2 size, float tolerance)
    {
        float halfX = size.x * 0.5f + Mathf.Max(0f, tolerance);
        float halfY = size.y * 0.5f + Mathf.Max(0f, tolerance);
        return position.x >= center.x - halfX &&
               position.x <= center.x + halfX &&
               position.y >= center.y - halfY &&
               position.y <= center.y + halfY;
    }
}

public sealed class MapInstanceMember : MonoBehaviour
{
    [SerializeField] string instanceId = MapInstanceService.MainInstanceId;
    [SerializeField] string extractionTypeOverride = string.Empty;

    public string InstanceId => string.IsNullOrWhiteSpace(instanceId) ? MapInstanceService.MainInstanceId : instanceId;
    public string ExtractionTypeOverride => extractionTypeOverride ?? string.Empty;

    public void Configure(string configuredInstanceId, string configuredExtractionTypeOverride = "")
    {
        instanceId = string.IsNullOrWhiteSpace(configuredInstanceId)
            ? MapInstanceService.MainInstanceId
            : configuredInstanceId;
        extractionTypeOverride = configuredExtractionTypeOverride ?? string.Empty;
    }
}
