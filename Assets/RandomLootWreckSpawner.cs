using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Photon.Pun;
using UnityEngine;

public sealed class RandomLootWreckSpawner : MonoBehaviourPun
{
    const string GameStartedKey = "gameStarted";
    const string ObstacleLayoutKey = "obstacleLayout";
    const string ExtractionLayoutKey = "extractionLayout";
    const string NebulaLayoutKey = "nebulaLayout";
    const string FireNebulaLayoutKey = NebulaSpawner.FireNebulaLayoutKey;
    const string ToxicNebulaLayoutKey = NebulaSpawner.ToxicNebulaLayoutKey;
    const string RepairBayLayoutKey = "repairBayLayout";
    const string SpaceFactoryLayoutKey = SpaceFactorySpawner.LayoutKey;
    const string EmptyLayoutSentinel = "__empty__";
    const float MapMargin = 2.8f;
    const float MinDistanceFromExtraction = 3.2f;
    const float MinDistanceFromObstacle = 2.7f;
    const float MinDistanceFromNebula = 2.8f;
    const float MinDistanceFromRepairBay = 4.2f;
    const float MinDistanceFromSpaceFactory = 5.8f;
    const float MinDistanceBetweenWrecks = 2.8f;

    static RandomLootWreckSpawner instance;

    Coroutine spawnRoutine;
    string lastSpawnRoundToken = string.Empty;

    public static void EnsureExists()
    {
        if (instance != null)
        {
            instance.EnsureSpawnRoutineRunning();
            return;
        }

        GameObject existing = GameObject.Find("RandomLootWreckSpawner");
        if (existing != null && existing.TryGetComponent(out RandomLootWreckSpawner existingSpawner))
        {
            instance = existingSpawner;
            instance.EnsureSpawnRoutineRunning();
            return;
        }

        GameObject spawner = new GameObject("RandomLootWreckSpawner");
        instance = spawner.AddComponent<RandomLootWreckSpawner>();
    }

    public static void ResetForSessionTransition()
    {
        if (instance == null)
            return;

        instance.ResetLocalRuntimeState();
    }

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Start()
    {
        EnsureSpawnRoutineRunning();
    }

    void EnsureSpawnRoutineRunning()
    {
        if (spawnRoutine != null)
            return;

        if (IsRoundStarted() && HasSpawnedForCurrentRound())
            return;

        spawnRoutine = StartCoroutine(SpawnWhenRoundStarts());
    }

    void ResetLocalRuntimeState()
    {
        StopAllCoroutines();
        spawnRoutine = null;
        lastSpawnRoundToken = string.Empty;
        EnsureSpawnRoutineRunning();
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
               !HasLayout(FireNebulaLayoutKey) ||
               !HasLayout(ToxicNebulaLayoutKey) ||
               !HasLayout(RepairBayLayoutKey) ||
               !HasLayout(SpaceFactoryLayoutKey))
        {
            yield return null;
        }

        if (HasSpawnedForCurrentRound())
        {
            spawnRoutine = null;
            yield break;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            spawnRoutine = null;
            yield break;
        }

        lastSpawnRoundToken = BuildCurrentRoundToken();
        SpawnRandomLootWrecks();
        spawnRoutine = null;
    }

    void SpawnRandomLootWrecks()
    {
        int desiredCount = RoomSettings.GetRandomLootWreckCount();
        if (desiredCount <= 0)
            return;

        List<Vector2> spawnedPositions = GetExistingRandomLootWreckPositions();
        int remainingCount = Mathf.Max(0, desiredCount - spawnedPositions.Count);
        if (remainingCount <= 0)
            return;

        int seed = ResolveSeed();
        Random.State previousState = Random.state;
        Random.InitState(seed);

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        List<Vector2> obstaclePositions = ParsePositionLayout(GetRoomLayout(ObstacleLayoutKey), 0, 1);
        List<Vector2> extractionPositions = ParsePositionLayout(GetRoomLayout(ExtractionLayoutKey), 0, 1);
        List<Vector2> nebulaPositions = ParsePositionLayout(GetRoomLayout(NebulaLayoutKey), 0, 1);
        nebulaPositions.AddRange(ParsePositionLayout(GetRoomLayout(FireNebulaLayoutKey), 0, 1));
        nebulaPositions.AddRange(ParsePositionLayout(GetRoomLayout(ToxicNebulaLayoutKey), 0, 1));
        List<Vector2> repairBayPositions = ParsePositionLayout(GetRoomLayout(RepairBayLayoutKey), 1, 2);
        List<Vector2> spaceFactoryPositions = ParsePositionLayout(GetRoomLayout(SpaceFactoryLayoutKey), 1, 2);

        int attempts = 0;
        int spawnedThisPass = 0;
        while (spawnedThisPass < remainingCount && attempts < 750)
        {
            attempts++;
            Vector2 position = new Vector2(
                Random.Range(-mapSize.x * 0.5f + MapMargin, mapSize.x * 0.5f - MapMargin),
                Random.Range(-mapSize.y * 0.5f + MapMargin, mapSize.y * 0.5f - MapMargin));

            if (!IsFarEnough(position, spawnedPositions, MinDistanceBetweenWrecks))
                continue;

            if (!IsFarEnough(position, extractionPositions, MinDistanceFromExtraction))
                continue;

            if (!IsFarEnough(position, obstaclePositions, GetMinDistanceFromObstacle()))
                continue;

            if (!IsFarEnough(position, nebulaPositions, MinDistanceFromNebula))
                continue;

            if (!IsFarEnough(position, repairBayPositions, MinDistanceFromRepairBay))
                continue;

            if (!IsFarEnough(position, spaceFactoryPositions, MinDistanceFromSpaceFactory))
                continue;

            Collider2D hit = Physics2D.OverlapCircle(position, 0.95f);
            if (hit != null)
                continue;

            string itemId = InventoryItemCatalog.GetRandomLootWreckItemId(Random.Range(0, InventoryItemCatalog.RandomLootWreckVariantCount));
            PhotonNetwork.Instantiate("TreasureNetwork", new Vector3(position.x, position.y, 0f), Quaternion.identity, 0, new object[] { itemId });
            spawnedPositions.Add(position);
            spawnedThisPass++;
        }

        Random.state = previousState;
        if (spawnedThisPass > 0)
            GameVisualTheme.RequestRuntimeRefresh();
    }

    List<Vector2> GetExistingRandomLootWreckPositions()
    {
        List<Vector2> positions = new List<Vector2>();
        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || !InventoryItemCatalog.IsRandomLootWreckItem(treasure.itemId))
                continue;

            positions.Add(treasure.transform.position);
        }

        return positions;
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

    bool HasSpawnedForCurrentRound()
    {
        return string.Equals(lastSpawnRoundToken, BuildCurrentRoundToken(), System.StringComparison.Ordinal);
    }

    string BuildCurrentRoundToken()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return string.Empty;

        string startValue = "nostart";
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object value) && value != null)
            startValue = value.ToString();

        return PhotonNetwork.CurrentRoom.Name + "_" + startValue;
    }

    int ResolveSeed()
    {
        int seed = 151;
        if (PhotonNetwork.CurrentRoom != null)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("mapSeed", out object mapSeed))
                seed = CombineSeed(seed, ConvertToInt(mapSeed, seed));

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object startTime))
                seed = CombineSeed(seed, Mathf.RoundToInt((float)(ConvertToDouble(startTime, 0d) * 1000d)));
        }

        seed = CombineSeed(seed, RoomSettings.GetMapBackgroundIndex());
        seed = CombineSeed(seed, RoomSettings.GetMapSizeMode().GetHashCode());
        seed = CombineSeed(seed, RoomSettings.GetRandomLootWreckCount());
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
