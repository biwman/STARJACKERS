using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Photon.Pun;
using UnityEngine;

public class NebulaSpawner : MonoBehaviour
{
    public const string FireNebulaLayoutKey = "fireNebulaLayout";

    const string EmptyLayoutSentinel = "__empty__";
    const string GameStartedKey = "gameStarted";
    const string ExtractionLayoutKey = "extractionLayout";
    const string ObstacleLayoutKey = "obstacleLayout";
    const string NebulaLayoutKey = "nebulaLayout";
    const float Margin = 3f;
    const float MinNebulaDistance = 4.5f;
    const float MinDistanceFromExtraction = 4f;
    const float MinDistanceFromObstacle = 3.8f;
    const float MinDistanceFromPlayers = 3.6f;

    public int nebulaCount = 5;

    bool layoutApplied;
    float nextRetryTime;

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

        while (!HasExtractionLayout() || !HasObstacleLayout())
            yield return null;

        while (!layoutApplied)
        {
            TryApplyOrBuildLayout();
            if (!layoutApplied)
                yield return null;
        }
    }

    void Update()
    {
        if (layoutApplied || Time.unscaledTime < nextRetryTime)
            return;

        nextRetryTime = Time.unscaledTime + 0.35f;
        TryApplyOrBuildLayout();
    }

    void TryApplyOrBuildLayout()
    {
        if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InRoom || !IsRoundStarted())
            return;

        if (!HasExtractionLayout() || !HasObstacleLayout())
            return;

        string normalLayout = GetRoomLayout(NebulaLayoutKey);
        string fireLayout = GetRoomLayout(FireNebulaLayoutKey);

        if (PhotonNetwork.IsMasterClient && (string.IsNullOrWhiteSpace(normalLayout) || string.IsNullOrWhiteSpace(fireLayout)))
        {
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();

            if (string.IsNullOrWhiteSpace(normalLayout))
            {
                normalLayout = BuildNebulaLayout(RoomSettings.NebulaDensityKey, null);
                props[NebulaLayoutKey] = normalLayout;
            }

            List<Vector2> normalPositions = ParseLayout(normalLayout);
            if (string.IsNullOrWhiteSpace(fireLayout))
            {
                fireLayout = BuildNebulaLayout(RoomSettings.FireNebulaDensityKey, normalPositions);
                props[FireNebulaLayoutKey] = fireLayout;
            }

            if (props.Count > 0)
                PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        if (!string.IsNullOrWhiteSpace(normalLayout) && !string.IsNullOrWhiteSpace(fireLayout))
            ApplyLayouts(normalLayout, fireLayout);
    }

    void ApplyLayouts(string normalLayout, string fireLayout)
    {
        if (layoutApplied)
            return;

        ApplyLayerLayout(normalLayout, NebulaFieldKind.Normal);
        ApplyLayerLayout(fireLayout, NebulaFieldKind.Fire);
        layoutApplied = true;
    }

    void ApplyLayerLayout(string layout, NebulaFieldKind kind)
    {
        if (string.IsNullOrWhiteSpace(layout) || layout == EmptyLayoutSentinel)
            return;

        List<Vector2> positions = ParseLayout(layout);
        for (int i = 0; i < positions.Count; i++)
        {
            GameObject nebula = new GameObject(kind == NebulaFieldKind.Fire ? "FireNebula" : "Nebula");
            nebula.transform.position = new Vector3(positions[i].x, positions[i].y, 0f);
            nebula.AddComponent<SpriteRenderer>();
            nebula.AddComponent<CircleCollider2D>();
            NebulaField field = nebula.AddComponent<NebulaField>();
            field.ConfigureKind(kind);
        }
    }

    string BuildNebulaLayout(string densityKey, List<Vector2> reservedPositions)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        List<Vector2> positions = new List<Vector2>();
        List<Vector2> extractionPositions = ParseLayout(GetRoomLayout(ExtractionLayoutKey));
        List<Vector2> obstaclePositions = ParseLayout(GetRoomLayout(ObstacleLayoutKey));
        List<Vector2> playerPositions = GetCurrentPlayerPositions();
        int targetCount = Mathf.Max(0, Mathf.RoundToInt(nebulaCount * GetDensityMultiplier(densityKey) * RoomSettings.GetMapAreaMultiplier()));
        int attempts = 0;

        while (positions.Count < targetCount && attempts < 400)
        {
            attempts++;

            float x = Random.Range(-mapSize.x / 2f + Margin, mapSize.x / 2f - Margin);
            float y = Random.Range(-mapSize.y / 2f + Margin, mapSize.y / 2f - Margin);
            Vector2 candidate = new Vector2(x, y);

            if (!IsFarEnough(candidate, positions, MinNebulaDistance))
                continue;

            if (reservedPositions != null && !IsFarEnough(candidate, reservedPositions, MinNebulaDistance))
                continue;

            if (!IsFarEnough(candidate, extractionPositions, MinDistanceFromExtraction))
                continue;

            if (!IsFarEnough(candidate, obstaclePositions, GetMinDistanceFromObstacle()))
                continue;

            if (!IsFarEnough(candidate, playerPositions, MinDistanceFromPlayers))
                continue;

            positions.Add(candidate);
        }

        if (positions.Count == 0)
            return EmptyLayoutSentinel;

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < positions.Count; i++)
        {
            if (i > 0)
                builder.Append(';');

            builder.Append(positions[i].x.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(positions[i].y.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
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

    List<Vector2> ParseLayout(string layout)
    {
        List<Vector2> positions = new List<Vector2>();
        if (string.IsNullOrWhiteSpace(layout) || layout == EmptyLayoutSentinel)
            return positions;

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

    List<Vector2> GetCurrentPlayerPositions()
    {
        List<Vector2> positions = new List<Vector2>();
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                positions.Add(players[i].transform.position);
            }
        }

        return positions;
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

    float GetDensityMultiplier(string densityKey)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(densityKey, out object value) &&
            value is string density)
        {
            string normalized = densityKey == RoomSettings.FireNebulaDensityKey
                ? RoomSettings.NormalizeFireNebulaDensity(density)
                : density.Trim().ToLowerInvariant();

            switch (normalized)
            {
                case "none": return 0f;
                case "low": return 0.4f;
                case "high": return 1.2f;
                default: return 0.675f;
            }
        }

        return densityKey == RoomSettings.FireNebulaDensityKey ? 0f : 0.5f;
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
        return !string.IsNullOrWhiteSpace(GetRoomLayout(ExtractionLayoutKey));
    }

    bool HasObstacleLayout()
    {
        return !string.IsNullOrWhiteSpace(GetRoomLayout(ObstacleLayoutKey));
    }
}
