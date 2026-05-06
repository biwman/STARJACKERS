using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

public sealed class RepairBaySpawner : MonoBehaviourPunCallbacks
{
    const string LayoutKey = "repairBayLayout";
    const string ExtractionLayoutKey = "extractionLayout";
    const string EmptyLayoutSentinel = "__empty__";
    const float Margin = 4.2f;
    const float MinDistanceBetweenBays = 9f;
    const float MinDistanceFromExtractionZones = 7f;
    const float ExtractionLayoutWaitTimeout = 2f;

    static RepairBaySpawner instance;

    bool layoutApplied;

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject existing = GameObject.Find("RepairBaySpawner");
        if (existing != null && existing.TryGetComponent(out RepairBaySpawner existingSpawner))
        {
            instance = existingSpawner;
            return;
        }

        GameObject spawner = new GameObject("RepairBaySpawner");
        instance = spawner.AddComponent<RepairBaySpawner>();
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
            yield return StartCoroutine(WaitForExtractionLayout());

            int seed = ResolveSeed();
            string layout = BuildLayout(seed, GetExtractionPositions());
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
                if (PhotonNetwork.CurrentRoom != null &&
                    PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(LayoutKey, out object value) &&
                    value is string layout &&
                    !string.IsNullOrWhiteSpace(layout))
                {
                    ApplyLayout(layout);
                }

                if (!layoutApplied)
                    yield return null;
            }
        }
    }

    IEnumerator WaitForExtractionLayout()
    {
        float deadline = Time.realtimeSinceStartup + ExtractionLayoutWaitTimeout;
        while (PhotonNetwork.CurrentRoom != null &&
               string.IsNullOrWhiteSpace(GetRoomLayout(ExtractionLayoutKey)) &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
    }

    string BuildLayout(int seed, List<Vector2> extractionPositions)
    {
        int count = RoomSettings.GetRepairBayCount();
        if (count <= 0)
            return EmptyLayoutSentinel;

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        Random.State previousState = Random.state;
        Random.InitState(seed);

        List<Vector2> positions = new List<Vector2>();
        int attempts = 0;
        while (positions.Count < count && attempts < 600)
        {
            attempts++;
            Vector2 pos = new Vector2(
                Random.Range(-mapSize.x * 0.5f + Margin, mapSize.x * 0.5f - Margin),
                Random.Range(-mapSize.y * 0.5f + Margin, mapSize.y * 0.5f - Margin));

            bool tooClose = IsTooCloseToAny(pos, positions, MinDistanceBetweenBays) ||
                            IsTooCloseToAny(pos, extractionPositions, MinDistanceFromExtractionZones);

            if (!tooClose)
                positions.Add(pos);
        }

        Random.state = previousState;

        if (positions.Count == 0)
            return EmptyLayoutSentinel;

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < positions.Count; i++)
        {
            if (i > 0)
                builder.Append(';');

            float phase = Mathf.Repeat(Mathf.Sin((seed * 17.17f) + (i * 91.3f)) * 1000f, Mathf.PI * 2f);
            builder.Append("repair_bay_");
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

    void ApplyLayout(string layout)
    {
        if (layoutApplied || string.IsNullOrWhiteSpace(layout))
            return;

        layoutApplied = true;
        RepairBay.ClearAllRuntimeBays();
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

            GameObject bayObject = new GameObject("RepairBay_" + id, typeof(SpriteRenderer), typeof(CircleCollider2D));
            RepairBay bay = bayObject.AddComponent<RepairBay>();
            bay.Configure(id, new Vector2(x, y), phase);
        }
    }

    int ResolveSeed()
    {
        int seed = 41;
        if (PhotonNetwork.CurrentRoom != null)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("mapSeed", out object mapSeed))
                seed = CombineSeed(seed, ConvertToInt(mapSeed, seed));

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object startTime))
                seed = CombineSeed(seed, Mathf.RoundToInt((float)(ConvertToDouble(startTime, 0d) * 1000d)));
        }

        seed = CombineSeed(seed, RoomSettings.GetRepairBayCount());
        seed = CombineSeed(seed, RoomSettings.GetMapBackgroundIndex());
        return seed;
    }

    List<Vector2> GetExtractionPositions()
    {
        return ParseExtractionLayout(GetRoomLayout(ExtractionLayoutKey));
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

    List<Vector2> ParseExtractionLayout(string layout)
    {
        List<Vector2> positions = new List<Vector2>();
        if (string.IsNullOrWhiteSpace(layout))
            return positions;

        string[] entries = layout.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string[] parts = entries[i].Split(',');
            if (parts.Length != 2)
                continue;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                continue;
            }

            positions.Add(new Vector2(x, y));
        }

        return positions;
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
