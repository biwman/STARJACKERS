using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Photon.Pun;
using UnityEngine;

public sealed class SpaceJunkSpawner : MonoBehaviourPun
{
    const string GameStartedKey = "gameStarted";
    const string ObstacleLayoutKey = "obstacleLayout";
    const string ExtractionLayoutKey = "extractionLayout";
    const string NebulaLayoutKey = "nebulaLayout";
    const string RepairBayLayoutKey = "repairBayLayout";
    const string EmptyLayoutSentinel = "__empty__";
    const float MapMargin = 2.4f;
    const float MinDistanceFromExtraction = 3.2f;
    const float MinDistanceFromObstacle = 2.6f;
    const float MinDistanceFromNebula = 2.6f;
    const float MinDistanceFromRepairBay = 4.8f;
    const float MinDistanceBetweenJunk = 2.6f;

    static readonly string[] SpaceJunkItemIds =
    {
        InventoryItemCatalog.SpaceJunkTrashId,
        InventoryItemCatalog.SpaceJunkStandardId,
        InventoryItemCatalog.SpaceJunkAsteroidId
    };

    static SpaceJunkSpawner instance;

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject existing = GameObject.Find("SpaceJunkSpawner");
        if (existing != null && existing.TryGetComponent(out SpaceJunkSpawner existingSpawner))
        {
            instance = existingSpawner;
            return;
        }

        GameObject spawner = new GameObject("SpaceJunkSpawner");
        instance = spawner.AddComponent<SpaceJunkSpawner>();
    }

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        StartCoroutine(SpawnWhenRoundStarts());
    }

    IEnumerator SpawnWhenRoundStarts()
    {
        while (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InRoom)
            yield return null;

        while (!IsRoundStarted())
            yield return null;

        while (!HasLayout(ExtractionLayoutKey) ||
               !HasLayout(ObstacleLayoutKey) ||
               !HasLayout(NebulaLayoutKey) ||
               !HasLayout(RepairBayLayoutKey))
        {
            yield return null;
        }

        if (!PhotonNetwork.IsMasterClient)
            yield break;

        SpawnSpaceJunk();
    }

    void SpawnSpaceJunk()
    {
        int seed = ResolveSeed();
        Random.State previousState = Random.state;
        Random.InitState(seed);

        int targetCount = RollTargetCount(RoomSettings.GetSpaceJunkDensity());
        if (targetCount <= 0)
        {
            Random.state = previousState;
            return;
        }

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        List<Vector2> obstaclePositions = ParsePositionLayout(GetRoomLayout(ObstacleLayoutKey), 0, 1);
        List<Vector2> extractionPositions = ParsePositionLayout(GetRoomLayout(ExtractionLayoutKey), 0, 1);
        List<Vector2> nebulaPositions = ParsePositionLayout(GetRoomLayout(NebulaLayoutKey), 0, 1);
        List<Vector2> repairBayPositions = ParsePositionLayout(GetRoomLayout(RepairBayLayoutKey), 1, 2);
        List<Vector2> spawnedPositions = new List<Vector2>();

        int attempts = 0;
        while (spawnedPositions.Count < targetCount && attempts < 500)
        {
            attempts++;
            Vector2 position = new Vector2(
                Random.Range(-mapSize.x * 0.5f + MapMargin, mapSize.x * 0.5f - MapMargin),
                Random.Range(-mapSize.y * 0.5f + MapMargin, mapSize.y * 0.5f - MapMargin));

            if (!IsFarEnough(position, spawnedPositions, MinDistanceBetweenJunk))
                continue;

            if (!IsFarEnough(position, extractionPositions, MinDistanceFromExtraction))
                continue;

            if (!IsFarEnough(position, obstaclePositions, GetMinDistanceFromObstacle()))
                continue;

            if (!IsFarEnough(position, nebulaPositions, MinDistanceFromNebula))
                continue;

            if (!IsFarEnough(position, repairBayPositions, MinDistanceFromRepairBay))
                continue;

            Collider2D hit = Physics2D.OverlapCircle(position, 1f);
            if (hit != null)
                continue;

            string itemId = SpaceJunkItemIds[Random.Range(0, SpaceJunkItemIds.Length)];
            PhotonNetwork.Instantiate("TreasureNetwork", new Vector3(position.x, position.y, 0f), Quaternion.identity, 0, new object[] { itemId });
            spawnedPositions.Add(position);
        }

        Random.state = previousState;
        Debug.Log("Spawned space junk: " + spawnedPositions.Count);
    }

    int RollTargetCount(string density)
    {
        switch (RoomSettings.NormalizeSpaceJunkDensity(density))
        {
            case RoomSettings.SpaceJunkDensityNone:
                return 0;
            case RoomSettings.SpaceJunkDensityMedium:
                return Random.Range(2, 4);
            case RoomSettings.SpaceJunkDensityHigh:
                return Random.Range(3, 6);
            default:
                return Random.Range(1, 3);
        }
    }

    float GetMinDistanceFromObstacle()
    {
        return MinDistanceFromObstacle * Mathf.Clamp(RoomSettings.GetObstacleSizeMultiplier(), 0.75f, 5f);
    }

    bool IsFarEnough(Vector2 candidate, List<Vector2> positions, float minDistance)
    {
        if (positions == null || positions.Count == 0)
            return true;

        for (int i = 0; i < positions.Count; i++)
        {
            if (Vector2.Distance(candidate, positions[i]) < minDistance)
                return false;
        }

        return true;
    }

    List<Vector2> ParsePositionLayout(string layout, int xIndex, int yIndex)
    {
        List<Vector2> positions = new List<Vector2>();
        if (string.IsNullOrWhiteSpace(layout) || string.Equals(layout, EmptyLayoutSentinel, System.StringComparison.Ordinal))
            return positions;

        string[] entries = layout.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string[] parts = entries[i].Split(',');
            if (parts.Length <= Mathf.Max(xIndex, yIndex))
                continue;

            if (float.TryParse(parts[xIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[yIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                positions.Add(new Vector2(x, y));
            }
        }

        return positions;
    }

    bool HasLayout(string key)
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value) &&
               value is string layout &&
               !string.IsNullOrWhiteSpace(layout);
    }

    string GetRoomLayout(string key)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value) &&
            value is string layout)
        {
            return layout;
        }

        return string.Empty;
    }

    bool IsRoundStarted()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(GameStartedKey, out object value) &&
               value is bool started &&
               started;
    }

    int ResolveSeed()
    {
        int seed = 73;
        if (PhotonNetwork.CurrentRoom != null)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("mapSeed", out object mapSeed))
                seed = CombineSeed(seed, ConvertToInt(mapSeed, seed));

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object startTime))
                seed = CombineSeed(seed, Mathf.RoundToInt((float)(ConvertToDouble(startTime, 0d) * 1000d)));
        }

        seed = CombineSeed(seed, RoomSettings.GetMapBackgroundIndex());
        seed = CombineSeed(seed, RoomSettings.GetMapSizeMode().GetHashCode());
        seed = CombineSeed(seed, RoomSettings.GetSpaceJunkDensity().GetHashCode());
        return seed;
    }

    int CombineSeed(int seed, int value)
    {
        unchecked
        {
            return (seed * 397) ^ value;
        }
    }

    int ConvertToInt(object value, int fallback)
    {
        return value switch
        {
            int intValue => intValue,
            float floatValue => Mathf.RoundToInt(floatValue),
            double doubleValue => Mathf.RoundToInt((float)doubleValue),
            _ => fallback
        };
    }

    double ConvertToDouble(object value, double fallback)
    {
        return value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            _ => fallback
        };
    }
}
