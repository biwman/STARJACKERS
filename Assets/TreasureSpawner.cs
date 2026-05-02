using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

public class TreasureSpawner : MonoBehaviourPun
{
    const string GameStartedKey = "gameStarted";
    const string TreasureDensityKey = "treasureDensity";
    const string ObstacleLayoutKey = "obstacleLayout";
    const string ExtractionLayoutKey = "extractionLayout";
    const string NebulaLayoutKey = "nebulaLayout";
    const float MinDistanceFromExtraction = 3f;
    const float MinDistanceFromObstacle = 2.6f;
    const float MinDistanceFromNebula = 2.8f;
    static readonly float[] VeryLowRichnessWeights = { 70f, 21f, 7f, 1f, 0.8f, 0.2f };
    static readonly float[] LowRichnessWeights = { 60f, 25f, 10f, 3f, 1.5f, 0.5f };
    static readonly float[] MediumRichnessWeights = { 50f, 25f, 13f, 7f, 4f, 1f };
    static readonly float[] HighRichnessWeights = { 40f, 28f, 15f, 10f, 5f, 2f };
    static readonly float[] VeryHighRichnessWeights = { 30f, 29f, 17f, 15f, 6f, 3f };
    static readonly float[] ExtremeRichnessWeights = { 20f, 30f, 20f, 17f, 8f, 5f };

    public int treasureCount = 10;
    public float mapSizeX = 25f;
    public float mapSizeY = 25f;

    void Start()
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        mapSizeX = mapSize.x;
        mapSizeY = mapSize.y;

        Debug.Log("TreasureSpawner Start");
        StartCoroutine(SpawnWhenRoundStarts());
    }

    IEnumerator SpawnWhenRoundStarts()
    {
        while (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InRoom)
            yield return null;

        while (!IsRoundStarted())
            yield return null;

        while (!HasExtractionLayout() || !HasObstacleLayout() || !HasNebulaLayout())
            yield return null;

        if (!PhotonNetwork.IsMasterClient)
            yield break;

        Debug.Log("Master spawning treasures for started round");
        SpawnTreasures();
    }

    void SpawnTreasures()
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        mapSizeX = mapSize.x;
        mapSizeY = mapSize.y;

        List<Vector2> obstaclePositions = ParseLayout(ObstacleLayoutKey);
        List<Vector2> extractionPositions = ParseLayout(ExtractionLayoutKey);
        List<Vector2> nebulaPositions = ParseLayout(NebulaLayoutKey);
        int spawned = 0;
        int attempts = 0;
        int targetCount = Mathf.Max(0, Mathf.RoundToInt(treasureCount * GetDensityMultiplier() * RoomSettings.GetMapAreaMultiplier()));

        while (spawned < targetCount && attempts < 300)
        {
            attempts++;

            float margin = 2f;
            float x = Random.Range(-mapSizeX / 2 + margin, mapSizeX / 2 - margin);
            float y = Random.Range(-mapSizeY / 2 + margin, mapSizeY / 2 - margin);
            Vector2 pos2D = new Vector2(x, y);

            if (!IsFarEnough(pos2D, extractionPositions, MinDistanceFromExtraction))
                continue;

            if (!IsFarEnough(pos2D, obstaclePositions, GetMinDistanceFromObstacle()))
                continue;

            if (!IsFarEnough(pos2D, nebulaPositions, MinDistanceFromNebula))
                continue;

            Collider2D hit = Physics2D.OverlapCircle(pos2D, 1f);
            if (hit == null)
            {
                PhotonNetwork.Instantiate("TreasureNetwork", new Vector3(x, y, 0f), Quaternion.identity, 0, new object[] { RollTreasureItemId() });
                spawned++;
            }
        }

        Debug.Log("Spawned treasures: " + spawned);
    }

    bool IsFarEnough(Vector2 candidate, List<Vector2> positions, float minDistance)
    {
        for (int i = 0; i < positions.Count; i++)
        {
            if (Vector2.Distance(candidate, positions[i]) < minDistance)
                return false;
        }

        return true;
    }

    float GetMinDistanceFromObstacle()
    {
        return MinDistanceFromObstacle * Mathf.Clamp(RoomSettings.GetObstacleSizeMultiplier(), 0.75f, 5f);
    }

    List<Vector2> ParseLayout(string key)
    {
        List<Vector2> positions = new List<Vector2>();
        if (PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value) ||
            value is not string layout ||
            string.IsNullOrWhiteSpace(layout))
        {
            return positions;
        }

        string[] entries = layout.Split(';');
        foreach (string entry in entries)
        {
            string[] parts = entry.Split(',');
            if (parts.Length != 2)
                continue;

            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                positions.Add(new Vector2(x, y));
            }
        }

        return positions;
    }

    float GetDensityMultiplier()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TreasureDensityKey, out object value) &&
            value is string density)
        {
            switch (density)
            {
                case "none": return 0f;
                case "low": return 0.5f;
                case "high": return 2f;
                default: return 1f;
            }
        }

        return 1f;
    }

    string RollTreasureItemId()
    {
        float[] weights = GetResourceRichnessWeights();
        float total = 0f;
        for (int i = 0; i < weights.Length; i++)
            total += Mathf.Max(0f, weights[i]);

        if (total <= 0f)
            return InventoryItemCatalog.AsteroidResourceId;

        float roll = Random.Range(0f, total);
        if (ConsumeWeight(ref roll, weights, 0))
            return InventoryItemCatalog.AsteroidResourceId;

        if (ConsumeWeight(ref roll, weights, 1))
            return InventoryItemCatalog.AsteroidGoldId;

        if (ConsumeWeight(ref roll, weights, 2))
            return InventoryItemCatalog.AsteroidRareId;

        if (ConsumeWeight(ref roll, weights, 3))
            return InventoryItemCatalog.RichAsteroidId;

        if (ConsumeWeight(ref roll, weights, 4))
            return InventoryItemCatalog.AsteroidEpicId;

        return InventoryItemCatalog.AsteroidLegendaryId;
    }

    float[] GetResourceRichnessWeights()
    {
        switch (RoomSettings.GetResourceRichness())
        {
            case RoomSettings.ResourceRichnessVeryLow:
                return VeryLowRichnessWeights;
            case RoomSettings.ResourceRichnessLow:
                return LowRichnessWeights;
            case RoomSettings.ResourceRichnessHigh:
                return HighRichnessWeights;
            case RoomSettings.ResourceRichnessVeryHigh:
                return VeryHighRichnessWeights;
            case RoomSettings.ResourceRichnessExtreme:
                return ExtremeRichnessWeights;
            default:
                return MediumRichnessWeights;
        }
    }

    bool ConsumeWeight(ref float roll, float[] weights, int index)
    {
        if (weights == null || index < 0 || index >= weights.Length)
            return false;

        float weight = Mathf.Max(0f, weights[index]);
        if (roll < weight)
            return true;

        roll -= weight;
        return false;
    }

    bool IsRoundStarted()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(GameStartedKey, out object value) && value is bool started)
        {
            return started;
        }

        return false;
    }

    bool HasExtractionLayout()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ExtractionLayoutKey, out object value) &&
               value is string layout &&
               !string.IsNullOrWhiteSpace(layout);
    }

    bool HasObstacleLayout()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ObstacleLayoutKey, out object value) &&
               value is string layout &&
               !string.IsNullOrWhiteSpace(layout);
    }

    bool HasNebulaLayout()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NebulaLayoutKey, out object value) &&
               value is string layout &&
               !string.IsNullOrWhiteSpace(layout);
    }
}
