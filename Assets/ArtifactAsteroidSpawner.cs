using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

public sealed class ArtifactAsteroidSpawner : MonoBehaviourPunCallbacks
{
    public const string LayoutKey = "artifactAsteroidLayout";

    const string EmptyLayoutSentinel = "__empty__";
    const string MapSeedKey = "mapSeed";
    const string ObstacleLayoutKey = "obstacleLayout";
    const string ExtractionLayoutKey = "extractionLayout";
    const string RepairBayLayoutKey = "repairBayLayout";
    const string SpaceFactoryLayoutKey = SpaceFactorySpawner.LayoutKey;
    const string ScienceStationLayoutKey = ScienceStationSpawner.LayoutKey;
    const string NebulaLayoutKey = "nebulaLayout";
    const string FireNebulaLayoutKey = NebulaSpawner.FireNebulaLayoutKey;
    const string ToxicNebulaLayoutKey = NebulaSpawner.ToxicNebulaLayoutKey;
    const float MapMargin = 4.2f;
    const float MinDistanceBetweenArtifacts = 8.8f;
    const float MinDistanceFromExtractionZones = 8.5f;
    const float MinDistanceFromRepairBays = 7.2f;
    const float MinDistanceFromFactories = 7.4f;
    const float MinDistanceFromScienceStations = 7.4f;
    const float MinDistanceFromObstacles = 5.8f;
    const float MinDistanceFromNebulas = 4.6f;
    const float StrictOverlapRadius = 3.2f;
    const float RelaxedOverlapRadius = 1.25f;

    static ArtifactAsteroidSpawner instance;

    readonly HashSet<string> appliedActiveIds = new HashSet<string>(System.StringComparer.Ordinal);
    Coroutine spawnRoutine;
    Coroutine announcementRoutine;
    bool layoutApplied;
    bool stateInitialized;

    public static void EnsureExists()
    {
        if (instance != null)
        {
            instance.EnsureSpawnRoutineRunning();
            return;
        }

        GameObject existing = GameObject.Find("ArtifactAsteroidSpawner");
        if (existing != null && existing.TryGetComponent(out ArtifactAsteroidSpawner existingSpawner))
        {
            instance = existingSpawner;
            instance.EnsureSpawnRoutineRunning();
            return;
        }

        GameObject spawner = new GameObject("ArtifactAsteroidSpawner");
        instance = spawner.AddComponent<ArtifactAsteroidSpawner>();
    }

    public static void ResetForSessionTransition()
    {
        if (instance == null)
            return;

        instance.ResetLocalRuntimeState();
    }

    public static bool TryActivateAuthority(string artifactId)
    {
        return instance != null && instance.TryActivateAuthorityInternal(artifactId);
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

    public override void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged)
    {
        if (propertiesThatChanged == null || PhotonNetwork.CurrentRoom == null)
            return;

        if (!layoutApplied && propertiesThatChanged.ContainsKey(LayoutKey))
        {
            string layout = GetRoomLayout(LayoutKey);
            if (!string.IsNullOrWhiteSpace(layout))
                ApplyLayout(layout);
        }

        if (layoutApplied && propertiesThatChanged.ContainsKey(RoomSettings.ArtifactAsteroidsStateKey))
            ApplyStateFromRoom(true);
    }

    void EnsureSpawnRoutineRunning()
    {
        if (spawnRoutine != null)
            return;

        if (layoutApplied && IsRoundStarted())
            return;

        spawnRoutine = StartCoroutine(InitializeWhenRoundStarts());
    }

    void ResetLocalRuntimeState()
    {
        StopAllCoroutines();
        spawnRoutine = null;
        announcementRoutine = null;
        layoutApplied = false;
        stateInitialized = false;
        appliedActiveIds.Clear();
        ArtifactAsteroid.ClearAllRuntimeArtifacts();
        EnsureSpawnRoutineRunning();
    }

    IEnumerator InitializeWhenRoundStarts()
    {
        while (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InRoom)
            yield return null;

        while (!IsRoundStarted())
            yield return null;

        while (!HasLayout(ExtractionLayoutKey) ||
               !HasLayout(RepairBayLayoutKey) ||
               !HasLayout(SpaceFactoryLayoutKey) ||
               !HasLayout(ScienceStationLayoutKey) ||
               !HasLayout(ObstacleLayoutKey) ||
               !HasLayout(NebulaLayoutKey) ||
               !HasLayout(FireNebulaLayoutKey) ||
               !HasLayout(ToxicNebulaLayoutKey))
        {
            yield return null;
        }

        if (layoutApplied)
        {
            spawnRoutine = null;
            yield break;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            string layout = BuildLayout(
                ResolveSeed(),
                ParsePositionLayout(GetRoomLayout(ExtractionLayoutKey), 0, 1),
                ParsePositionLayout(GetRoomLayout(RepairBayLayoutKey), 1, 2),
                ParsePositionLayout(GetRoomLayout(SpaceFactoryLayoutKey), 1, 2),
                ParsePositionLayout(GetRoomLayout(ScienceStationLayoutKey), 1, 2),
                ParsePositionLayout(GetRoomLayout(ObstacleLayoutKey), 0, 1),
                BuildNebulaPositions());

            PhotonHashtable props = new PhotonHashtable
            {
                [LayoutKey] = layout,
                [RoomSettings.ArtifactAsteroidsStateKey] = string.Empty
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

        spawnRoutine = null;
    }

    List<Vector2> BuildNebulaPositions()
    {
        List<Vector2> positions = ParsePositionLayout(GetRoomLayout(NebulaLayoutKey), 0, 1);
        positions.AddRange(ParsePositionLayout(GetRoomLayout(FireNebulaLayoutKey), 0, 1));
        positions.AddRange(ParsePositionLayout(GetRoomLayout(ToxicNebulaLayoutKey), 0, 1));
        return positions;
    }

    string BuildLayout(int seed, List<Vector2> extractionPositions, List<Vector2> repairBayPositions, List<Vector2> factoryPositions, List<Vector2> scienceStationPositions, List<Vector2> obstaclePositions, List<Vector2> nebulaPositions)
    {
        int count = Mathf.Max(0, RoomSettings.GetArtifactAsteroidsCount());
        if (count <= 0)
            return EmptyLayoutSentinel;

        Vector2 mapSize = RoomSettings.GetGameplayMapDimensions();
        Random.State previousState = Random.state;
        Random.InitState(seed);

        List<Vector2> positions = new List<Vector2>();
        FillPositions(
            positions,
            count,
            mapSize,
            extractionPositions,
            repairBayPositions,
            factoryPositions,
            scienceStationPositions,
            obstaclePositions,
            nebulaPositions,
            MinDistanceBetweenArtifacts,
            StrictOverlapRadius,
            1200);

        if (positions.Count < count)
        {
            FillPositions(
                positions,
                count,
                mapSize,
                extractionPositions,
                repairBayPositions,
                factoryPositions,
                scienceStationPositions,
                obstaclePositions,
                nebulaPositions,
                MinDistanceBetweenArtifacts * 0.7f,
                RelaxedOverlapRadius,
                1800);
        }

        Random.state = previousState;
        if (positions.Count == 0)
            return EmptyLayoutSentinel;

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < positions.Count; i++)
        {
            if (i > 0)
                builder.Append(';');

            int variant = Mathf.Abs(CombineSeed(seed, i * 31 + 17)) % 6;
            float rotation = Mathf.Repeat((seed * 0.073f) + i * 67.3f + Random.Range(-18f, 18f), 360f);
            float size = Random.Range(0.92f, 1.1f);
            builder.Append("artifact_asteroid_");
            builder.Append(i.ToString("00", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(positions[i].x.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(positions[i].y.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(variant.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(rotation.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(size.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    void FillPositions(List<Vector2> positions, int count, Vector2 mapSize, List<Vector2> extractionPositions, List<Vector2> repairBayPositions, List<Vector2> factoryPositions, List<Vector2> scienceStationPositions, List<Vector2> obstaclePositions, List<Vector2> nebulaPositions, float minDistanceBetweenArtifacts, float overlapRadius, int attempts)
    {
        int tries = 0;
        while (positions.Count < count && tries < attempts)
        {
            tries++;
            float x = Random.Range(-mapSize.x * 0.5f + MapMargin, mapSize.x * 0.5f - MapMargin);
            float y = Random.Range(-mapSize.y * 0.5f + MapMargin, mapSize.y * 0.5f - MapMargin);
            Vector2 candidate = new Vector2(x, y);

            if (!IsFarEnough(candidate, positions, minDistanceBetweenArtifacts))
                continue;

            if (!IsFarEnough(candidate, extractionPositions, MinDistanceFromExtractionZones))
                continue;

            if (!IsFarEnough(candidate, repairBayPositions, MinDistanceFromRepairBays))
                continue;

            if (!IsFarEnough(candidate, factoryPositions, MinDistanceFromFactories))
                continue;

            if (!IsFarEnough(candidate, scienceStationPositions, MinDistanceFromScienceStations))
                continue;

            if (!IsFarEnough(candidate, obstaclePositions, MinDistanceFromObstacles))
                continue;

            if (!IsFarEnough(candidate, nebulaPositions, MinDistanceFromNebulas))
                continue;

            if (Physics2D.OverlapCircle(candidate, overlapRadius) != null)
                continue;

            positions.Add(candidate);
        }
    }

    void ApplyLayout(string layout)
    {
        if (layoutApplied || string.IsNullOrWhiteSpace(layout))
            return;

        ArtifactAsteroid.ClearAllRuntimeArtifacts();
        layoutApplied = true;

        if (!string.Equals(layout, EmptyLayoutSentinel, System.StringComparison.Ordinal))
        {
            string[] entries = layout.Split(';');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                string[] parts = entry.Split(',');
                if (parts.Length < 6)
                    continue;

                string id = parts[0];
                if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                    !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                    !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int variant) ||
                    !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotation) ||
                    !float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float size))
                {
                    continue;
                }

                GameObject artifactObject = new GameObject(id);
                ArtifactAsteroid artifact = artifactObject.AddComponent<ArtifactAsteroid>();
                artifact.Configure(id, new Vector2(x, y), variant, rotation, size, false);
            }
        }

        ApplyStateFromRoom(false);
    }

    void ApplyStateFromRoom(bool playNewActivations)
    {
        HashSet<string> activeIds = DeserializeState(GetRoomStateRaw());
        List<ArtifactAsteroid> artifacts = ArtifactAsteroid.GetAllRuntimeArtifacts();
        bool hasNewActivation = false;

        for (int i = 0; i < artifacts.Count; i++)
        {
            ArtifactAsteroid artifact = artifacts[i];
            if (artifact == null)
                continue;

            bool shouldBeActive = activeIds.Contains(artifact.StableId);
            bool newlyActive = playNewActivations && stateInitialized && shouldBeActive && !appliedActiveIds.Contains(artifact.StableId);
            artifact.SetActiveState(shouldBeActive, newlyActive);
            hasNewActivation |= newlyActive;
        }

        appliedActiveIds.Clear();
        foreach (string id in activeIds)
            appliedActiveIds.Add(id);

        stateInitialized = true;

        if (hasNewActivation)
            ShowActivationAnnouncement();
    }

    void ShowActivationAnnouncement()
    {
        int total = ArtifactAsteroid.TotalCount;
        if (total <= 0)
            return;

        int activeCount = ArtifactAsteroid.ActiveCount;
        if (announcementRoutine != null)
            StopCoroutine(announcementRoutine);

        announcementRoutine = StartCoroutine(ShowActivationAnnouncementRoutine(activeCount, total));
    }

    IEnumerator ShowActivationAnnouncementRoutine(int activeCount, int total)
    {
        RoundAnnouncementUI.Show("Artifact activated " + activeCount + "/" + total, 2.1f);
        if (activeCount >= total)
        {
            yield return new WaitForSeconds(1.65f);
            RoundAnnouncementUI.Show("All artifacts activated", 3f);
        }

        announcementRoutine = null;
    }

    bool TryActivateAuthorityInternal(string artifactId)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || string.IsNullOrWhiteSpace(artifactId))
            return false;

        ArtifactAsteroid artifact = ArtifactAsteroid.Find(artifactId);
        if (artifact == null || artifact.IsActive)
            return false;

        HashSet<string> activeIds = DeserializeState(GetRoomStateRaw());
        if (!activeIds.Add(artifactId))
            return false;

        PhotonHashtable props = new PhotonHashtable
        {
            [RoomSettings.ArtifactAsteroidsStateKey] = SerializeState(activeIds)
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        return true;
    }

    int ResolveSeed()
    {
        int seed = 17291;
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MapSeedKey, out object value) &&
            value is int mapSeed)
        {
            seed = mapSeed;
        }

        seed = CombineSeed(seed, RoomSettings.GetSelectedLobbyMapId().GetHashCode());
        seed = CombineSeed(seed, RoomSettings.GetArtifactAsteroidsDensity().GetHashCode());
        return seed;
    }

    static int CombineSeed(int seed, int value)
    {
        unchecked
        {
            return (seed * 397) ^ value;
        }
    }

    static bool IsFarEnough(Vector2 candidate, List<Vector2> positions, float minDistance)
    {
        for (int i = 0; i < positions.Count; i++)
        {
            if (Vector2.Distance(candidate, positions[i]) < minDistance)
                return false;
        }

        return true;
    }

    static string GetRoomLayout(string key)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value) &&
            value is string layout)
        {
            return layout;
        }

        return string.Empty;
    }

    static string GetRoomStateRaw()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.ArtifactAsteroidsStateKey, out object value) &&
            value is string raw)
        {
            return raw;
        }

        return string.Empty;
    }

    static bool HasLayout(string key)
    {
        return !string.IsNullOrWhiteSpace(GetRoomLayout(key));
    }

    static bool IsRoundStarted()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
               value is bool started &&
               started;
    }

    static List<Vector2> ParsePositionLayout(string layout, int xIndex, int yIndex)
    {
        List<Vector2> positions = new List<Vector2>();
        if (string.IsNullOrWhiteSpace(layout) || string.Equals(layout, EmptyLayoutSentinel, System.StringComparison.Ordinal))
            return positions;

        string[] entries = layout.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            string[] parts = entry.Split(',');
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

    static HashSet<string> DeserializeState(string raw)
    {
        HashSet<string> activeIds = new HashSet<string>(System.StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw))
            return activeIds;

        string[] entries = raw.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string id = entries[i];
            if (!string.IsNullOrWhiteSpace(id))
                activeIds.Add(id);
        }

        return activeIds;
    }

    static string SerializeState(HashSet<string> activeIds)
    {
        if (activeIds == null || activeIds.Count == 0)
            return string.Empty;

        List<string> sorted = new List<string>(activeIds);
        sorted.Sort(System.StringComparer.Ordinal);
        return string.Join(";", sorted);
    }
}
