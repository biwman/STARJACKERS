using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

public sealed class SpaceFactorySpawner : MonoBehaviourPunCallbacks
{
    public const string LayoutKey = "spaceFactoryLayout";

    const string ObstacleLayoutKey = "obstacleLayout";
    const string ExtractionLayoutKey = "extractionLayout";
    const string RepairBayLayoutKey = "repairBayLayout";
    const string NebulaLayoutKey = "nebulaLayout";
    const string FireNebulaLayoutKey = NebulaSpawner.FireNebulaLayoutKey;
    const string EmptyLayoutSentinel = "__empty__";
    const float Margin = 5.3f;
    const float MinDistanceBetweenFactories = 11f;
    const float MinDistanceFromExtractionZones = 8.2f;
    const float MinDistanceFromRepairBays = 10f;
    const float MinDistanceFromObstacles = 7f;
    const float MinDistanceFromNebulas = 5.8f;
    const float StrictOverlapRadius = 3.4f;
    const float RelaxedOverlapRadius = 1.25f;

    static SpaceFactorySpawner instance;

    bool layoutApplied;

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject existing = GameObject.Find("SpaceFactorySpawner");
        if (existing != null && existing.TryGetComponent(out SpaceFactorySpawner existingSpawner))
        {
            instance = existingSpawner;
            return;
        }

        GameObject spawner = new GameObject("SpaceFactorySpawner");
        instance = spawner.AddComponent<SpaceFactorySpawner>();
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
        StartCoroutine(InitializeWhenRoundStarts());
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (layoutApplied ||
            propertiesThatChanged == null ||
            !propertiesThatChanged.ContainsKey(LayoutKey) ||
            PhotonNetwork.CurrentRoom == null)
        {
            return;
        }

        string layout = GetRoomLayout(LayoutKey);
        if (!string.IsNullOrWhiteSpace(layout))
            ApplyLayout(layout);
    }

    void ResetLocalRuntimeState()
    {
        StopAllCoroutines();
        layoutApplied = false;
        SpaceFactory.ClearAllRuntimeFactories();
        StartCoroutine(InitializeWhenRoundStarts());
    }

    IEnumerator InitializeWhenRoundStarts()
    {
        while (!PhotonNetwork.InRoom)
            yield return null;

        while (!IsRoundStarted())
            yield return null;

        if (layoutApplied)
            yield break;

        if (PhotonNetwork.IsMasterClient)
        {
            while (!HasLayout(ExtractionLayoutKey) ||
                   !HasLayout(RepairBayLayoutKey) ||
                   !HasLayout(ObstacleLayoutKey) ||
                   !HasLayout(NebulaLayoutKey) ||
                   !HasLayout(FireNebulaLayoutKey))
            {
                yield return null;
            }

            int seed = ResolveSeed();
            List<Vector2> nebulaPositions = ParsePositionLayout(GetRoomLayout(NebulaLayoutKey), 0, 1);
            nebulaPositions.AddRange(ParsePositionLayout(GetRoomLayout(FireNebulaLayoutKey), 0, 1));
            string layout = BuildLayout(
                seed,
                ParsePositionLayout(GetRoomLayout(ExtractionLayoutKey), 0, 1),
                ParsePositionLayout(GetRoomLayout(RepairBayLayoutKey), 1, 2),
                ParsePositionLayout(GetRoomLayout(ObstacleLayoutKey), 0, 1),
                nebulaPositions);

            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
            {
                [LayoutKey] = layout
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            ApplyLayout(layout);
        }
        else
        {
            while (!layoutApplied)
            {
                string layout = GetRoomLayout(LayoutKey);
                if (!string.IsNullOrWhiteSpace(layout))
                    ApplyLayout(layout);

                if (!layoutApplied)
                    yield return null;
            }
        }
    }

    string BuildLayout(int seed, List<Vector2> extractionPositions, List<Vector2> repairBayPositions, List<Vector2> obstaclePositions, List<Vector2> nebulaPositions)
    {
        int count = RoomSettings.GetSpaceFactoryCount();
        if (count <= 0)
            return EmptyLayoutSentinel;

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        Random.State previousState = Random.state;
        Random.InitState(seed);

        List<Vector2> positions = new List<Vector2>();
        FillPositions(
            positions,
            count,
            mapSize,
            extractionPositions,
            repairBayPositions,
            obstaclePositions,
            nebulaPositions,
            MinDistanceFromExtractionZones,
            MinDistanceFromRepairBays,
            MinDistanceFromObstacles,
            MinDistanceFromNebulas,
            StrictOverlapRadius,
            900);

        if (positions.Count < count)
        {
            FillPositions(
                positions,
                count,
                mapSize,
                extractionPositions,
                repairBayPositions,
                obstaclePositions,
                nebulaPositions,
                MinDistanceFromExtractionZones * 0.68f,
                MinDistanceFromRepairBays * 0.62f,
                MinDistanceFromObstacles * 0.58f,
                MinDistanceFromNebulas * 0.55f,
                RelaxedOverlapRadius,
                1400);
        }

        Random.state = previousState;
        if (positions.Count == 0)
            return EmptyLayoutSentinel;

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < positions.Count; i++)
        {
            if (i > 0)
                builder.Append(';');

            float phase = Mathf.Repeat(Mathf.Sin((seed * 19.37f) + (i * 67.9f)) * 1000f, Mathf.PI * 2f);
            builder.Append("space_factory_");
            builder.Append(i);
            builder.Append(',');
            builder.Append(positions[i].x.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(positions[i].y.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(phase.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    void FillPositions(
        List<Vector2> positions,
        int count,
        Vector2 mapSize,
        List<Vector2> extractionPositions,
        List<Vector2> repairBayPositions,
        List<Vector2> obstaclePositions,
        List<Vector2> nebulaPositions,
        float extractionDistance,
        float repairBayDistance,
        float obstacleDistance,
        float nebulaDistance,
        float overlapRadius,
        int maxAttempts)
    {
        int attempts = 0;
        float scaledObstacleDistance = obstacleDistance * Mathf.Clamp(RoomSettings.GetObstacleSizeMultiplier(), 0.75f, 5f);
        while (positions.Count < count && attempts < maxAttempts)
        {
            attempts++;
            Vector2 pos = new Vector2(
                Random.Range(-mapSize.x * 0.5f + Margin, mapSize.x * 0.5f - Margin),
                Random.Range(-mapSize.y * 0.5f + Margin, mapSize.y * 0.5f - Margin));

            if (IsTooCloseToAny(pos, positions, MinDistanceBetweenFactories))
                continue;

            if (IsTooCloseToAny(pos, extractionPositions, extractionDistance))
                continue;

            if (IsTooCloseToAny(pos, repairBayPositions, repairBayDistance))
                continue;

            if (IsTooCloseToAny(pos, obstaclePositions, scaledObstacleDistance))
                continue;

            if (IsTooCloseToAny(pos, nebulaPositions, nebulaDistance))
                continue;

            if (HasBlockingOverlap(pos, overlapRadius))
                continue;

            positions.Add(pos);
        }
    }

    bool HasBlockingOverlap(Vector2 position, float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            if (hit.GetComponentInParent<NebulaField>() != null)
                continue;

            return true;
        }

        return false;
    }

    void ApplyLayout(string layout)
    {
        if (layoutApplied || string.IsNullOrWhiteSpace(layout))
            return;

        layoutApplied = true;
        SpaceFactory.ClearAllRuntimeFactories();
        if (string.Equals(layout, EmptyLayoutSentinel, System.StringComparison.Ordinal))
            return;

        string[] entries = layout.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string[] parts = entries[i].Split(',');
            if (parts.Length != 4)
                continue;

            string id = parts[0];
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float phase))
            {
                continue;
            }

            GameObject factoryObject = new GameObject("SpaceFactory_" + id, typeof(SpriteRenderer), typeof(CircleCollider2D));
            SpaceFactory factory = factoryObject.AddComponent<SpaceFactory>();
            factory.Configure(id, new Vector2(x, y), phase);
        }
    }

    bool IsTooCloseToAny(Vector2 candidate, List<Vector2> positions, float minDistance)
    {
        if (positions == null || positions.Count == 0)
            return false;

        for (int i = 0; i < positions.Count; i++)
        {
            if (Vector2.Distance(candidate, positions[i]) < minDistance)
                return true;
        }

        return false;
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

    int ResolveSeed()
    {
        int seed = 113;
        if (PhotonNetwork.CurrentRoom != null)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("mapSeed", out object mapSeed))
                seed = CombineSeed(seed, ConvertToInt(mapSeed, seed));

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object startTime))
                seed = CombineSeed(seed, Mathf.RoundToInt((float)(ConvertToDouble(startTime, 0d) * 1000d)));
        }

        seed = CombineSeed(seed, RoomSettings.GetSpaceFactoryCount());
        seed = CombineSeed(seed, RoomSettings.GetMapBackgroundIndex());
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

    bool IsRoundStarted()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
               value is bool started &&
               started;
    }
}
