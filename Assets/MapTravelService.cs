using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public sealed class MapTravelService : MonoBehaviourPunCallbacks
{
    const string EmptyLayoutSentinel = "__empty__";
    const float HiddenRiftRadius = 8.8f;
    const float HiddenRiftDurationSeconds = 10f;
    const float HiddenRiftTimeBonusSeconds = 120f;
    const float EndSequenceBlockSeconds = 40f;
    const float HiddenPreparationDelaySeconds = 0.35f;
    const float HiddenRuntimeCheckInterval = 0.5f;
    const int HiddenLocalObjectsPerFrame = 5;
    const int HiddenNetworkSpawnsPerFrame = 3;
    const int BaseObstacleCount = 10;
    const int BaseTreasureCount = 10;
    const int BaseAlienSecretCount = 3;
    const int BaseNebulaCount = 5;
    const float ObstacleMargin = 4.2f;
    const float TreasureMargin = 2.8f;
    const float NebulaMargin = 3.2f;
    const float ExtractionMargin = 8f;
    const float MinObstacleDistance = 8.8f;
    const float MinTreasureDistance = 2.6f;
    const float MinNebulaDistance = 4.5f;
    const string AllPlayersAnnouncementTarget = "0";
    const string TooLateMessage = "TOO LATE TO FIND HIDDEN DIMENSION. TRY AGAIN LATER";
    const string DistortionMessage = "SPACE-TIME DISTORTIONS DETECTED";
    const int HiddenAdvancedBackgroundSortingOffset = 12;

    static readonly float[] ExtremelyLowRichnessWeights = { 85f, 12f, 2.5f, 0.4f, 0.09f, 0.01f };
    static readonly float[] VeryLowRichnessWeights = { 70f, 21f, 7f, 1f, 0.8f, 0.2f };
    static readonly float[] LowRichnessWeights = { 60f, 25f, 10f, 3f, 1.5f, 0.5f };
    static readonly float[] MediumRichnessWeights = { 50f, 25f, 13f, 7f, 4f, 1f };
    static readonly float[] HighRichnessWeights = { 40f, 28f, 15f, 10f, 5f, 2f };
    static readonly float[] VeryHighRichnessWeights = { 30f, 29f, 17f, 15f, 6f, 3f };
    static readonly float[] ExtremeRichnessWeights = { 20f, 30f, 20f, 17f, 8f, 5f };

    static MapTravelService instance;

    string shownMessageToken = string.Empty;
    string activeVfxToken = string.Empty;
    string finalizedRiftToken = string.Empty;
    string builtHiddenSignature = string.Empty;
    string hiddenBuildRoutineSignature = string.Empty;
    GameObject hiddenRoot;
    Coroutine hiddenRoomPropertiesRoutine;
    Coroutine hiddenBuildRoutine;
    Coroutine hiddenNetworkSpawnRoutine;
    float nextHiddenRequiredEnemyCheckTime;
    float nextHiddenRuntimeCheckTime;

    struct RiftState
    {
        public string Token;
        public string TargetInstanceId;
        public Vector2 Origin;
        public float Radius;
        public double StartTime;
        public float Duration;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject root = GameObject.Find("MapTravelService");
        if (root == null)
            root = new GameObject("MapTravelService");

        instance = root.GetComponent<MapTravelService>();
        if (instance == null)
            instance = root.AddComponent<MapTravelService>();

        DontDestroyOnLoad(root);
    }

    public static bool TryOpenHiddenDimensionRift(Vector2 origin, Player activatingPlayer)
    {
        EnsureExists();
        return instance != null && instance.TryOpenHiddenDimensionRiftInternal(origin, activatingPlayer);
    }

    public static void ResetLocalRuntimeState()
    {
        if (instance == null)
            return;

        instance.shownMessageToken = string.Empty;
        instance.activeVfxToken = string.Empty;
        instance.finalizedRiftToken = string.Empty;
        instance.builtHiddenSignature = string.Empty;
        instance.hiddenBuildRoutineSignature = string.Empty;
        instance.nextHiddenRuntimeCheckTime = 0f;
        instance.StopHiddenPreparationRoutines();
        MapTravelRiftVfx.ClearAll();
        ObstacleSpawner.DestroyRuntimeObstaclesInInstance(MapInstanceService.HiddenDimensionInstanceId);
        if (instance.hiddenRoot != null)
            Destroy(instance.hiddenRoot);

        instance.hiddenRoot = null;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom)
            return;

        HandleCurrentMessage();
        HandleCurrentRift();

        if (!MapInstanceService.IsHiddenDimensionActive())
        {
            if (hiddenRoot != null || !string.IsNullOrEmpty(builtHiddenSignature))
                ClearHiddenDimensionLocalRuntime(false);
            return;
        }

        if (Time.unscaledTime >= nextHiddenRuntimeCheckTime)
        {
            nextHiddenRuntimeCheckTime = Time.unscaledTime + HiddenRuntimeCheckInterval;
            TryBuildHiddenDimensionLocalRuntime();
        }

        if (PhotonNetwork.IsMasterClient)
            TrySpawnHiddenDimensionNetworkObjects();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged == null)
            return;

        if (propertiesThatChanged.ContainsKey(MapInstanceService.MapTravelMessageKey))
            HandleCurrentMessage();

        if (propertiesThatChanged.ContainsKey(MapInstanceService.MapTravelRiftStateKey))
            HandleCurrentRift();

        if (propertiesThatChanged.ContainsKey(MapInstanceService.HiddenDimensionActiveKey) ||
            propertiesThatChanged.ContainsKey(MapInstanceService.HiddenDimensionObstacleLayoutKey) ||
            propertiesThatChanged.ContainsKey(MapInstanceService.HiddenDimensionNebulaLayoutKey))
        {
            if (!MapInstanceService.IsHiddenDimensionActive())
            {
                ClearHiddenDimensionLocalRuntime(false);
                return;
            }

            TryBuildHiddenDimensionLocalRuntime();
        }
    }

    bool TryOpenHiddenDimensionRiftInternal(Vector2 origin, Player activatingPlayer)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return false;

        if (TryParseRiftState(GetRoomString(MapInstanceService.MapTravelRiftStateKey), out RiftState existing) &&
            !string.IsNullOrWhiteSpace(existing.Token))
        {
            return false;
        }

        float remaining = GetCurrentRoundRemainingTime();
        if (remaining <= EndSequenceBlockSeconds)
        {
            PublishAnnouncement(activatingPlayer != null ? activatingPlayer.ActorNumber : 0, TooLateMessage);
            return false;
        }

        LobbyMapDefinition hiddenMap = LobbyMapCatalog.Get(LobbyMapCatalog.HiddenDimensionMapId);
        if (hiddenMap == null)
            return false;

        ScheduleHiddenDimensionRoomProperties(hiddenMap);
        AddRoundTimeBonus();
        PhotonNetwork.CurrentRoom.IsOpen = false;

        string token = BuildToken("rift");
        RiftState rift = new RiftState
        {
            Token = token,
            TargetInstanceId = MapInstanceService.HiddenDimensionInstanceId,
            Origin = origin,
            Radius = HiddenRiftRadius,
            StartTime = PhotonNetwork.Time,
            Duration = HiddenRiftDurationSeconds
        };

        Hashtable props = new Hashtable
        {
            [MapInstanceService.MapTravelLockedKey] = true,
            [MapInstanceService.MapTravelRiftStateKey] = SerializeRiftState(rift)
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        PublishAnnouncement(0, DistortionMessage);
        MapTravelRiftVfx.Show(token, origin, HiddenRiftRadius, rift.StartTime, HiddenRiftDurationSeconds);
        return true;
    }

    void HandleCurrentMessage()
    {
        string raw = GetRoomString(MapInstanceService.MapTravelMessageKey);
        if (string.IsNullOrWhiteSpace(raw))
            return;

        string[] parts = raw.Split(new[] { '|' }, 3);
        if (parts.Length < 3)
            return;

        string token = parts[0];
        if (string.Equals(token, shownMessageToken, System.StringComparison.Ordinal))
            return;

        string target = parts[1];
        int localActor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
        if (!string.Equals(target, AllPlayersAnnouncementTarget, System.StringComparison.Ordinal) &&
            (!int.TryParse(target, NumberStyles.Integer, CultureInfo.InvariantCulture, out int targetActor) || targetActor != localActor))
        {
            return;
        }

        shownMessageToken = token;
        RoundAnnouncementUI.Show(parts[2], 3.2f);
    }

    void HandleCurrentRift()
    {
        string raw = GetRoomString(MapInstanceService.MapTravelRiftStateKey);
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (!string.IsNullOrEmpty(activeVfxToken))
                MapTravelRiftVfx.ClearAll();

            activeVfxToken = string.Empty;
            finalizedRiftToken = string.Empty;
            return;
        }

        if (!TryParseRiftState(raw, out RiftState rift) || string.IsNullOrWhiteSpace(rift.Token))
            return;

        double now = PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;
        if (now <= rift.StartTime + rift.Duration + 1.1d &&
            !string.Equals(activeVfxToken, rift.Token, System.StringComparison.Ordinal))
        {
            activeVfxToken = rift.Token;
            MapTravelRiftVfx.Show(rift.Token, rift.Origin, rift.Radius, rift.StartTime, rift.Duration);
        }

        if (!PhotonNetwork.IsMasterClient ||
            string.Equals(finalizedRiftToken, rift.Token, System.StringComparison.Ordinal) ||
            now < rift.StartTime + rift.Duration)
        {
            return;
        }

        finalizedRiftToken = rift.Token;
        FinalizeRift(rift);
    }

    void FinalizeRift(RiftState rift)
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        if (!MapInstanceService.IsHiddenDimensionActive() ||
            string.IsNullOrWhiteSpace(GetRoomString(MapInstanceService.HiddenDimensionObstacleLayoutKey)))
        {
            if (hiddenRoomPropertiesRoutine != null)
            {
                StopCoroutine(hiddenRoomPropertiesRoutine);
                hiddenRoomPropertiesRoutine = null;
            }

            EnsureHiddenDimensionRoomProperties(LobbyMapCatalog.Get(LobbyMapCatalog.HiddenDimensionMapId));
        }

        TryBuildHiddenDimensionLocalRuntime();
        TrySpawnHiddenDimensionNetworkObjects();
        List<PlayerHealth> travelers = FindPlayersInsideRift(rift);
        for (int i = 0; i < travelers.Count; i++)
        {
            PlayerHealth traveler = travelers[i];
            if (traveler == null || traveler.photonView == null)
                continue;

            Vector2 spawn = ResolveHiddenSpawnPosition(i, travelers.Count);
            PlayerMovement movement = traveler.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                traveler.photonView.RPC(
                    nameof(PlayerMovement.TeleportToMapInstanceRpc),
                    RpcTarget.All,
                    spawn.x,
                    spawn.y,
                    MapInstanceService.HiddenDimensionInstanceId);
            }
        }

        MapTravelRiftVfx.Collapse(rift.Token);
    }

    List<PlayerHealth> FindPlayersInsideRift(RiftState rift)
    {
        List<PlayerHealth> result = new List<PlayerHealth>();
        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (!GameTimer.IsActiveRoundPlayer(player))
                continue;

            if (!MapInstanceService.IsSameInstance(player.transform.position, rift.Origin))
                continue;

            if (Vector2.Distance(player.transform.position, rift.Origin) <= rift.Radius)
                result.Add(player);
        }

        result.Sort((left, right) =>
        {
            int leftActor = left != null && left.photonView != null && left.photonView.Owner != null ? left.photonView.Owner.ActorNumber : 0;
            int rightActor = right != null && right.photonView != null && right.photonView.Owner != null ? right.photonView.Owner.ActorNumber : 0;
            return leftActor.CompareTo(rightActor);
        });
        return result;
    }

    Vector2 ResolveHiddenSpawnPosition(int index, int count)
    {
        MapInstanceService.TryGetBoundsForInstance(MapInstanceService.HiddenDimensionInstanceId, out MapInstanceService.BoundsInfo bounds);
        int safeCount = Mathf.Max(1, count);
        float angle = (index / (float)safeCount) * Mathf.PI * 2f;
        float radius = Mathf.Min(5.5f, Mathf.Min(bounds.InnerSize.x, bounds.InnerSize.y) * 0.12f);
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        return MapInstanceService.ClampToInstanceInnerBounds(bounds.Center + offset, 4f);
    }

    void EnsureHiddenDimensionRoomProperties(LobbyMapDefinition hiddenMap)
    {
        if (PhotonNetwork.CurrentRoom == null || hiddenMap == null)
            return;

        if (MapInstanceService.IsHiddenDimensionActive() &&
            !string.IsNullOrWhiteSpace(GetRoomString(MapInstanceService.HiddenDimensionObstacleLayoutKey)))
        {
            return;
        }

        Vector2 innerSize = MapInstanceService.GetMapDimensions(hiddenMap, false);
        Vector2 outerSize = MapInstanceService.GetMapDimensions(hiddenMap, hiddenMap.ToxicBordersEnabled);
        Vector2 center = ResolveHiddenDimensionCenter(outerSize);
        int seed = BuildStableSeed(hiddenMap.Id);
        List<Vector2> extractionPositions = BuildExtractionPositions(hiddenMap, center, innerSize, seed);
        List<Vector2> obstaclePositions = BuildObstaclePositions(hiddenMap, center, innerSize, extractionPositions, seed + 11);
        List<Vector2> nebulaPositions = BuildNebulaPositions(hiddenMap.NebulaDensity, center, innerSize, extractionPositions, obstaclePositions, null, seed + 23);
        List<Vector2> fireNebulaPositions = BuildNebulaPositions(hiddenMap.FireNebulaDensity, center, innerSize, extractionPositions, obstaclePositions, nebulaPositions, seed + 31);
        List<Vector2> reservedNebulaPositions = new List<Vector2>(nebulaPositions);
        reservedNebulaPositions.AddRange(fireNebulaPositions);
        List<Vector2> toxicNebulaPositions = BuildNebulaPositions(hiddenMap.ToxicNebulaDensity, center, innerSize, extractionPositions, obstaclePositions, reservedNebulaPositions, seed + 43);
        Vector2 cloudDirection = BuildCloudDirection(seed + 53);
        List<Vector2> cloudPositions = BuildCloudPositions(hiddenMap.CloudsDensity, center, innerSize, seed + 59);
        string treasureLayout = BuildTreasureLayout(hiddenMap, center, innerSize, extractionPositions, obstaclePositions, nebulaPositions, fireNebulaPositions, toxicNebulaPositions, seed + 71);
        string alienSecretsLayout = BuildAlienSecretsLayout(hiddenMap, center, innerSize, extractionPositions, obstaclePositions, nebulaPositions, fireNebulaPositions, toxicNebulaPositions, ParsePositions(treasureLayout), seed + 79);

        Hashtable props = new Hashtable
        {
            [MapInstanceService.HiddenDimensionActiveKey] = true,
            [MapInstanceService.HiddenDimensionCenterXKey] = center.x,
            [MapInstanceService.HiddenDimensionCenterYKey] = center.y,
            [MapInstanceService.HiddenDimensionExtractionLayoutKey] = SerializePositions(extractionPositions),
            [MapInstanceService.HiddenDimensionObstacleLayoutKey] = SerializePositions(obstaclePositions),
            [MapInstanceService.HiddenDimensionNebulaLayoutKey] = SerializePositions(nebulaPositions),
            [MapInstanceService.HiddenDimensionFireNebulaLayoutKey] = SerializePositions(fireNebulaPositions),
            [MapInstanceService.HiddenDimensionToxicNebulaLayoutKey] = SerializePositions(toxicNebulaPositions),
            [MapInstanceService.HiddenDimensionCloudLayoutKey] = SerializePositions(cloudPositions),
            [MapInstanceService.HiddenDimensionCloudDirectionKey] = SerializeVector2(cloudDirection),
            [MapInstanceService.HiddenDimensionTreasureLayoutKey] = treasureLayout,
            [MapInstanceService.HiddenDimensionAlienSecretsLayoutKey] = alienSecretsLayout,
            [MapInstanceService.HiddenDimensionNetworkObjectsSpawnedKey] = false
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    void ScheduleHiddenDimensionRoomProperties(LobbyMapDefinition hiddenMap)
    {
        if (PhotonNetwork.CurrentRoom == null || hiddenMap == null)
            return;

        if (MapInstanceService.IsHiddenDimensionActive() &&
            !string.IsNullOrWhiteSpace(GetRoomString(MapInstanceService.HiddenDimensionObstacleLayoutKey)))
        {
            return;
        }

        if (hiddenRoomPropertiesRoutine != null)
            return;

        hiddenRoomPropertiesRoutine = StartCoroutine(EnsureHiddenDimensionRoomPropertiesRoutine(hiddenMap));
    }

    IEnumerator EnsureHiddenDimensionRoomPropertiesRoutine(LobbyMapDefinition hiddenMap)
    {
        yield return null;

        if (PhotonNetwork.CurrentRoom == null || hiddenMap == null)
        {
            hiddenRoomPropertiesRoutine = null;
            yield break;
        }

        if (MapInstanceService.IsHiddenDimensionActive() &&
            !string.IsNullOrWhiteSpace(GetRoomString(MapInstanceService.HiddenDimensionObstacleLayoutKey)))
        {
            hiddenRoomPropertiesRoutine = null;
            yield break;
        }

        Vector2 innerSize = MapInstanceService.GetMapDimensions(hiddenMap, false);
        Vector2 outerSize = MapInstanceService.GetMapDimensions(hiddenMap, hiddenMap.ToxicBordersEnabled);
        Vector2 center = ResolveHiddenDimensionCenter(outerSize);
        int seed = BuildStableSeed(hiddenMap.Id);
        yield return null;

        List<Vector2> extractionPositions = BuildExtractionPositions(hiddenMap, center, innerSize, seed);
        yield return null;

        List<Vector2> obstaclePositions = BuildObstaclePositions(hiddenMap, center, innerSize, extractionPositions, seed + 11);
        yield return null;

        List<Vector2> nebulaPositions = BuildNebulaPositions(hiddenMap.NebulaDensity, center, innerSize, extractionPositions, obstaclePositions, null, seed + 23);
        yield return null;

        List<Vector2> fireNebulaPositions = BuildNebulaPositions(hiddenMap.FireNebulaDensity, center, innerSize, extractionPositions, obstaclePositions, nebulaPositions, seed + 31);
        yield return null;

        List<Vector2> reservedNebulaPositions = new List<Vector2>(nebulaPositions);
        reservedNebulaPositions.AddRange(fireNebulaPositions);
        List<Vector2> toxicNebulaPositions = BuildNebulaPositions(hiddenMap.ToxicNebulaDensity, center, innerSize, extractionPositions, obstaclePositions, reservedNebulaPositions, seed + 43);
        yield return null;

        Vector2 cloudDirection = BuildCloudDirection(seed + 53);
        List<Vector2> cloudPositions = BuildCloudPositions(hiddenMap.CloudsDensity, center, innerSize, seed + 59);
        yield return null;

        string treasureLayout = BuildTreasureLayout(hiddenMap, center, innerSize, extractionPositions, obstaclePositions, nebulaPositions, fireNebulaPositions, toxicNebulaPositions, seed + 71);
        yield return null;

        string alienSecretsLayout = BuildAlienSecretsLayout(hiddenMap, center, innerSize, extractionPositions, obstaclePositions, nebulaPositions, fireNebulaPositions, toxicNebulaPositions, ParsePositions(treasureLayout), seed + 79);

        Hashtable props = new Hashtable
        {
            [MapInstanceService.HiddenDimensionActiveKey] = true,
            [MapInstanceService.HiddenDimensionCenterXKey] = center.x,
            [MapInstanceService.HiddenDimensionCenterYKey] = center.y,
            [MapInstanceService.HiddenDimensionExtractionLayoutKey] = SerializePositions(extractionPositions),
            [MapInstanceService.HiddenDimensionObstacleLayoutKey] = SerializePositions(obstaclePositions),
            [MapInstanceService.HiddenDimensionNebulaLayoutKey] = SerializePositions(nebulaPositions),
            [MapInstanceService.HiddenDimensionFireNebulaLayoutKey] = SerializePositions(fireNebulaPositions),
            [MapInstanceService.HiddenDimensionToxicNebulaLayoutKey] = SerializePositions(toxicNebulaPositions),
            [MapInstanceService.HiddenDimensionCloudLayoutKey] = SerializePositions(cloudPositions),
            [MapInstanceService.HiddenDimensionCloudDirectionKey] = SerializeVector2(cloudDirection),
            [MapInstanceService.HiddenDimensionTreasureLayoutKey] = treasureLayout,
            [MapInstanceService.HiddenDimensionAlienSecretsLayoutKey] = alienSecretsLayout,
            [MapInstanceService.HiddenDimensionNetworkObjectsSpawnedKey] = false
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        hiddenRoomPropertiesRoutine = null;
    }

    void TryBuildHiddenDimensionLocalRuntime()
    {
        if (!MapInstanceService.IsHiddenDimensionActive())
            return;

        if (!TryGetHiddenRuntimeSignature(out string signature) ||
            string.Equals(signature, builtHiddenSignature, System.StringComparison.Ordinal))
        {
            return;
        }

        if (hiddenBuildRoutine != null && string.Equals(hiddenBuildRoutineSignature, signature, System.StringComparison.Ordinal))
            return;

        if (hiddenBuildRoutine != null)
            StopCoroutine(hiddenBuildRoutine);

        hiddenBuildRoutineSignature = signature;
        hiddenBuildRoutine = StartCoroutine(BuildHiddenDimensionLocalRuntimeRoutine(signature));
    }

    IEnumerator BuildHiddenDimensionLocalRuntimeRoutine(string signature)
    {
        yield return new WaitForSecondsRealtime(HiddenPreparationDelaySeconds);

        if (!MapInstanceService.IsHiddenDimensionActive() ||
            !TryGetHiddenRuntimeSignature(out string currentSignature) ||
            !string.Equals(currentSignature, signature, System.StringComparison.Ordinal))
        {
            ClearHiddenBuildRoutine(signature);
            yield break;
        }

        if (hiddenRoot != null)
            Destroy(hiddenRoot);

        yield return null;

        hiddenRoot = new GameObject("MapInstance_HiddenDimension");
        DontDestroyOnLoad(hiddenRoot);

        LobbyMapDefinition hiddenMap = LobbyMapCatalog.Get(LobbyMapCatalog.HiddenDimensionMapId);
        if (!MapInstanceService.TryGetBoundsForInstance(MapInstanceService.HiddenDimensionInstanceId, out MapInstanceService.BoundsInfo bounds))
        {
            ClearHiddenBuildRoutine(signature);
            yield break;
        }

        yield return null;

        BuildHiddenBackground(hiddenRoot.transform, hiddenMap, bounds.Center, bounds.InnerSize, bounds.Size);
        yield return null;

        BuildHiddenToxicBorder(hiddenRoot.transform, bounds.Center, bounds.InnerSize, bounds.Size);
        BuildHiddenWalls(hiddenRoot.transform, bounds.Center, bounds.Size);
        yield return null;

        yield return BuildHiddenObstaclesRoutine(GetRoomString(MapInstanceService.HiddenDimensionObstacleLayoutKey), hiddenMap);
        yield return BuildHiddenNebulasRoutine(GetRoomString(MapInstanceService.HiddenDimensionNebulaLayoutKey), NebulaFieldKind.Normal, Vector2.zero);
        yield return BuildHiddenNebulasRoutine(GetRoomString(MapInstanceService.HiddenDimensionFireNebulaLayoutKey), NebulaFieldKind.Fire, Vector2.zero);
        yield return BuildHiddenNebulasRoutine(GetRoomString(MapInstanceService.HiddenDimensionToxicNebulaLayoutKey), NebulaFieldKind.Toxic, Vector2.zero);
        yield return BuildHiddenNebulasRoutine(
            GetRoomString(MapInstanceService.HiddenDimensionCloudLayoutKey),
            NebulaFieldKind.Cloud,
            ParseVector2(GetRoomString(MapInstanceService.HiddenDimensionCloudDirectionKey), Vector2.right));

        builtHiddenSignature = signature;
        ClearHiddenBuildRoutine(signature);
    }

    bool TryGetHiddenRuntimeSignature(out string signature)
    {
        string obstacleLayout = GetRoomString(MapInstanceService.HiddenDimensionObstacleLayoutKey);
        if (string.IsNullOrWhiteSpace(obstacleLayout))
        {
            signature = string.Empty;
            return false;
        }

        signature = obstacleLayout + "#" +
                    GetRoomString(MapInstanceService.HiddenDimensionNebulaLayoutKey) + "#" +
                    GetRoomString(MapInstanceService.HiddenDimensionFireNebulaLayoutKey) + "#" +
                    GetRoomString(MapInstanceService.HiddenDimensionToxicNebulaLayoutKey) + "#" +
                    GetRoomString(MapInstanceService.HiddenDimensionCloudLayoutKey);
        return true;
    }

    void ClearHiddenBuildRoutine(string signature)
    {
        if (!string.Equals(hiddenBuildRoutineSignature, signature, System.StringComparison.Ordinal))
            return;

        hiddenBuildRoutine = null;
        hiddenBuildRoutineSignature = string.Empty;
    }

    void ClearHiddenDimensionLocalRuntime(bool clearRifts = true)
    {
        builtHiddenSignature = string.Empty;
        StopHiddenPreparationRoutines();
        if (clearRifts)
        {
            activeVfxToken = string.Empty;
            finalizedRiftToken = string.Empty;
            MapTravelRiftVfx.ClearAll();
        }

        ObstacleSpawner.DestroyRuntimeObstaclesInInstance(MapInstanceService.HiddenDimensionInstanceId);
        if (hiddenRoot != null)
            Destroy(hiddenRoot);

        hiddenRoot = null;
    }

    void StopHiddenPreparationRoutines()
    {
        if (hiddenBuildRoutine != null)
            StopCoroutine(hiddenBuildRoutine);

        if (hiddenRoomPropertiesRoutine != null)
            StopCoroutine(hiddenRoomPropertiesRoutine);

        if (hiddenNetworkSpawnRoutine != null)
            StopCoroutine(hiddenNetworkSpawnRoutine);

        hiddenRoomPropertiesRoutine = null;
        hiddenBuildRoutine = null;
        hiddenNetworkSpawnRoutine = null;
        hiddenBuildRoutineSignature = string.Empty;
        nextHiddenRequiredEnemyCheckTime = 0f;
        nextHiddenRuntimeCheckTime = 0f;
    }

    void TrySpawnHiddenDimensionNetworkObjects()
    {
        if (!PhotonNetwork.IsMasterClient ||
            PhotonNetwork.CurrentRoom == null ||
            !MapInstanceService.IsHiddenDimensionActive())
        {
            return;
        }

        if (GetRoomBool(MapInstanceService.HiddenDimensionNetworkObjectsSpawnedKey, false))
        {
            if (Time.time >= nextHiddenRequiredEnemyCheckTime)
            {
                nextHiddenRequiredEnemyCheckTime = Time.time + 1.25f;
                EnsureHiddenDimensionRequiredEnemies();
            }
            return;
        }

        if (hiddenNetworkSpawnRoutine != null)
            return;

        hiddenNetworkSpawnRoutine = StartCoroutine(SpawnHiddenDimensionNetworkObjectsRoutine());
    }

    IEnumerator SpawnHiddenDimensionNetworkObjectsRoutine()
    {
        yield return new WaitForSecondsRealtime(HiddenPreparationDelaySeconds);

        if (!PhotonNetwork.IsMasterClient ||
            PhotonNetwork.CurrentRoom == null ||
            !MapInstanceService.IsHiddenDimensionActive() ||
            GetRoomBool(MapInstanceService.HiddenDimensionNetworkObjectsSpawnedKey, false))
        {
            hiddenNetworkSpawnRoutine = null;
            yield break;
        }

        SpawnHiddenExtractionZones();
        yield return null;

        yield return SpawnHiddenTreasuresRoutine();
        yield return SpawnHiddenAlienSecretsRoutine();
        yield return SpawnHiddenEnemiesRoutine();
        EnsureHiddenDimensionRequiredEnemies();

        Hashtable props = new Hashtable
        {
            [MapInstanceService.HiddenDimensionNetworkObjectsSpawnedKey] = true
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        hiddenNetworkSpawnRoutine = null;
    }

    void SpawnHiddenExtractionZones()
    {
        List<Vector2> positions = ParsePositions(GetRoomString(MapInstanceService.HiddenDimensionExtractionLayoutKey));
        for (int i = 0; i < positions.Count; i++)
        {
            PhotonNetwork.Instantiate(
                "ExtractionZone",
                new Vector3(positions[i].x, positions[i].y, 0f),
                Quaternion.identity,
                0,
                new object[]
                {
                    MapInstanceService.PhotonInstantiationMarker,
                    MapInstanceService.HiddenDimensionInstanceId,
                    RoomSettings.ExtractionTypeAncientPortal
                });
        }

        if (positions.Count > 0)
            GameVisualTheme.RequestRuntimeRefresh();
    }

    IEnumerator SpawnHiddenTreasuresRoutine()
    {
        string layout = GetRoomString(MapInstanceService.HiddenDimensionTreasureLayoutKey);
        if (string.IsNullOrWhiteSpace(layout) || string.Equals(layout, EmptyLayoutSentinel, System.StringComparison.Ordinal))
            yield break;

        string[] entries = layout.Split(';');
        int spawnedThisFrame = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            string[] parts = entries[i].Split(',');
            if (parts.Length < 3)
                continue;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                continue;
            }

            string itemId = parts[2];
            PhotonNetwork.Instantiate("TreasureNetwork", new Vector3(x, y, 0f), Quaternion.identity, 0, new object[] { itemId });
            spawnedThisFrame++;
            if (spawnedThisFrame >= HiddenNetworkSpawnsPerFrame)
            {
                spawnedThisFrame = 0;
                yield return null;
            }
        }
    }

    IEnumerator SpawnHiddenAlienSecretsRoutine()
    {
        string layout = GetRoomString(MapInstanceService.HiddenDimensionAlienSecretsLayoutKey);
        if (string.IsNullOrWhiteSpace(layout) || string.Equals(layout, EmptyLayoutSentinel, System.StringComparison.Ordinal))
            yield break;

        string[] entries = layout.Split(';');
        int spawnedThisFrame = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            string[] parts = entries[i].Split(',');
            if (parts.Length < 3)
                continue;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                continue;
            }

            int variantIndex = 0;
            string serializedItemId = parts[2];
            int serializedVariantIndex = InventoryItemCatalog.GetAlienSecretVariantIndex(serializedItemId);
            if (serializedVariantIndex >= 0)
            {
                variantIndex = serializedVariantIndex;
            }

            if (parts.Length > 3 && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedVariant))
                variantIndex = InventoryItemCatalog.NormalizeAlienSecretVariantIndex(parsedVariant);

            string alienSecretItemId = InventoryItemCatalog.GetAlienSecretItemId(variantIndex);
            PhotonNetwork.Instantiate(
                "TreasureNetwork",
                new Vector3(x, y, 0f),
                Quaternion.identity,
                0,
                new object[] { alienSecretItemId, variantIndex });
            spawnedThisFrame++;
            if (spawnedThisFrame >= HiddenNetworkSpawnsPerFrame)
            {
                spawnedThisFrame = 0;
                yield return null;
            }
        }
    }

    IEnumerator SpawnHiddenEnemiesRoutine()
    {
        LobbyMapDefinition hiddenMap = LobbyMapCatalog.Get(LobbyMapCatalog.HiddenDimensionMapId);
        if (hiddenMap == null || hiddenMap.EnemyPresets == null)
            yield break;

        MapInstanceService.TryGetBoundsForInstance(MapInstanceService.HiddenDimensionInstanceId, out MapInstanceService.BoundsInfo bounds);
        int spawnedOrdinal = 0;
        int spawnedThisFrame = 0;
        for (int presetIndex = 0; presetIndex < hiddenMap.EnemyPresets.Count; presetIndex++)
        {
            LobbyEnemyMapPreset preset = hiddenMap.EnemyPresets[presetIndex];
            if (preset == null || !preset.Enabled || preset.Count <= 0)
                continue;

            EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(preset.Kind);
            if (definition == null)
                continue;

            for (int countIndex = 0; countIndex < preset.Count; countIndex++)
            {
                Vector2 spawn = definition.Kind == EnemyBotKind.RiftWarden
                    ? ResolveHiddenRiftWardenSpawn(bounds, spawnedOrdinal++)
                    : ResolveHiddenEnemySpawn(bounds.Center, bounds.InnerSize, spawnedOrdinal++);
                SpawnHiddenEnemy(definition, spawn);
                spawnedThisFrame++;
                if (spawnedThisFrame >= HiddenNetworkSpawnsPerFrame)
                {
                    spawnedThisFrame = 0;
                    yield return null;
                }
            }
        }
    }

    void EnsureHiddenDimensionRequiredEnemies()
    {
        LobbyMapDefinition hiddenMap = LobbyMapCatalog.Get(LobbyMapCatalog.HiddenDimensionMapId);
        LobbyEnemyMapPreset wardenPreset = FindHiddenEnemyPreset(hiddenMap, EnemyBotKind.RiftWarden);
        if (wardenPreset == null || !wardenPreset.Enabled || wardenPreset.Count <= 0)
            return;

        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(EnemyBotKind.RiftWarden);
        if (definition == null)
            return;

        MapInstanceService.TryGetBoundsForInstance(MapInstanceService.HiddenDimensionInstanceId, out MapInstanceService.BoundsInfo bounds);
        int existingCount = CountLiveHiddenEnemies(EnemyBotKind.RiftWarden);
        int missingCount = Mathf.Max(0, wardenPreset.Count - existingCount);
        for (int i = 0; i < missingCount; i++)
        {
            Vector2 spawn = ResolveHiddenRiftWardenSpawn(bounds, existingCount + i);
            SpawnHiddenEnemy(definition, spawn);
            Debug.Log($"[MapTravelService] Spawned missing Rift Warden in Hidden Dimension at {spawn}.");
        }
    }

    static LobbyEnemyMapPreset FindHiddenEnemyPreset(LobbyMapDefinition map, EnemyBotKind kind)
    {
        if (map == null || map.EnemyPresets == null)
            return null;

        for (int i = 0; i < map.EnemyPresets.Count; i++)
        {
            LobbyEnemyMapPreset preset = map.EnemyPresets[i];
            if (preset != null && preset.Kind == kind)
                return preset;
        }

        return null;
    }

    static int CountLiveHiddenEnemies(EnemyBotKind kind)
    {
        if (!MapInstanceService.TryGetBoundsForInstance(MapInstanceService.HiddenDimensionInstanceId, out MapInstanceService.BoundsInfo bounds))
            return 0;

        int count = 0;
        EnemyBot[] bots = FindObjectsByType<EnemyBot>(FindObjectsInactive.Exclude);
        for (int i = 0; i < bots.Length; i++)
        {
            EnemyBot bot = bots[i];
            if (bot == null || !IsHiddenEnemyOfKind(bot, kind))
                continue;

            PlayerHealth health = bot.GetComponent<PlayerHealth>();
            if (health != null && health.IsWreck)
                continue;

            if (MapInstanceService.IsSameInstance(bot.transform.position, bounds.Center))
                count++;
        }

        return count;
    }

    static bool IsHiddenEnemyOfKind(EnemyBot bot, EnemyBotKind kind)
    {
        if (bot == null)
            return false;

        PhotonView view = bot.GetComponent<PhotonView>();
        EnemyBotKind instantiatedKind = EnemyBot.GetKindFromInstantiationData(view != null ? view.InstantiationData : null);
        return instantiatedKind == kind || bot.Kind == kind;
    }

    static GameObject SpawnHiddenEnemy(EnemyBotDefinition definition, Vector2 spawn)
    {
        if (definition == null)
            return null;

        GameObject botObject = PhotonNetwork.Instantiate(
            "Player",
            spawn,
            Quaternion.identity,
            0,
            new object[] { definition.InstantiationMarker });

        BootstrapHiddenEnemy(botObject);
        return botObject;
    }

    static void BootstrapHiddenEnemy(GameObject botObject)
    {
        if (botObject == null)
            return;

        MapInstanceService.ConfigureMember(botObject, MapInstanceService.HiddenDimensionInstanceId);
        EnemyBot bot = botObject.GetComponent<EnemyBot>();
        if (bot == null)
            bot = botObject.AddComponent<EnemyBot>();

        bot.InitializeFromPhotonData();
        ActorIdentity.Ensure(botObject);
    }

    void BuildHiddenBackground(Transform root, LobbyMapDefinition hiddenMap, Vector2 center, Vector2 innerSize, Vector2 outerSize)
    {
        if (root == null)
            return;

        GameObject backgroundRoot = new GameObject("HiddenDimensionAdvancedSpaceBackground");
        backgroundRoot.transform.SetParent(root, false);
        backgroundRoot.transform.position = new Vector3(center.x, center.y, 0f);
        MapInstanceService.ConfigureMember(backgroundRoot, MapInstanceService.HiddenDimensionInstanceId);

        AdvancedSpaceBackground background = backgroundRoot.AddComponent<AdvancedSpaceBackground>();
        background.ConfigureRuntimeMapInstance(
            LobbyMapCatalog.HiddenDimensionMapId,
            outerSize,
            LobbyMapCatalog.GetDefaultParallaxBackgroundId(LobbyMapCatalog.HiddenDimensionMapId),
            LobbyMapCatalog.GetDefaultBackgroundObjectId(LobbyMapCatalog.HiddenDimensionMapId),
            hiddenMap != null ? hiddenMap.MapBackgroundIndex : RoomSettings.DefaultMapBackground,
            hiddenMap != null ? hiddenMap.MapSize : RoomSettings.DefaultMapSize,
            MapInstanceService.HiddenDimensionInstanceId,
            HiddenAdvancedBackgroundSortingOffset);
    }

    void BuildHiddenToxicBorder(Transform root, Vector2 center, Vector2 innerSize, Vector2 outerSize)
    {
        if (innerSize.x <= 0f || innerSize.y <= 0f || outerSize.x <= innerSize.x || outerSize.y <= innerSize.y)
            return;

        GameObject borderObject = new GameObject("HiddenDimensionDeathZone");
        borderObject.transform.SetParent(root, false);
        MapInstanceService.ConfigureMember(borderObject, MapInstanceService.HiddenDimensionInstanceId);
        MapInstanceToxicBorder border = borderObject.AddComponent<MapInstanceToxicBorder>();
        border.Configure(MapInstanceService.HiddenDimensionInstanceId, center, innerSize, outerSize);
    }

    void BuildHiddenWalls(Transform root, Vector2 center, Vector2 size)
    {
        CreateWall(root, "HiddenWallTop", new Vector2(center.x, center.y + size.y * 0.5f), new Vector2(size.x, 1f));
        CreateWall(root, "HiddenWallBottom", new Vector2(center.x, center.y - size.y * 0.5f), new Vector2(size.x, 1f));
        CreateWall(root, "HiddenWallLeft", new Vector2(center.x - size.x * 0.5f, center.y), new Vector2(1f, size.y));
        CreateWall(root, "HiddenWallRight", new Vector2(center.x + size.x * 0.5f, center.y), new Vector2(1f, size.y));
    }

    void CreateWall(Transform root, string wallName, Vector2 position, Vector2 size)
    {
        GameObject wall = new GameObject(wallName, typeof(BoxCollider2D));
        wall.transform.SetParent(root, false);
        wall.transform.position = new Vector3(position.x, position.y, 0f);
        BoxCollider2D collider = wall.GetComponent<BoxCollider2D>();
        collider.size = size;
        collider.sharedMaterial = MovingSpaceObject.GetSharedSoftBoundaryMaterial();
        MapInstanceService.ConfigureMember(wall, MapInstanceService.HiddenDimensionInstanceId);
    }

    IEnumerator BuildHiddenObstaclesRoutine(string layout, LobbyMapDefinition hiddenMap)
    {
        List<Vector2> positions = ParsePositions(layout);
        int hp = hiddenMap != null ? Mathf.Max(1, hiddenMap.ObstacleHp) : RoomSettings.DefaultObstacleHp;
        for (int i = 0; i < positions.Count; i++)
        {
            string stableId = "hidden_dimension_obstacle_" + i;
            ObstacleSpawner.CreateLocalRuntimeObstacle(stableId, positions[i], hp, PhotonNetwork.IsMasterClient);
            if ((i + 1) % HiddenLocalObjectsPerFrame == 0)
                yield return null;
        }
    }

    IEnumerator BuildHiddenNebulasRoutine(string layout, NebulaFieldKind kind, Vector2 cloudDirection)
    {
        if (hiddenRoot == null || string.IsNullOrWhiteSpace(layout) || string.Equals(layout, EmptyLayoutSentinel, System.StringComparison.Ordinal))
            yield break;

        List<Vector2> positions = ParsePositions(layout);
        for (int i = 0; i < positions.Count; i++)
        {
            GameObject nebula = new GameObject(kind == NebulaFieldKind.Fire ? "HiddenFireNebula" : kind == NebulaFieldKind.Toxic ? "HiddenToxicNebula" : kind == NebulaFieldKind.Cloud ? "HiddenCloud" : "HiddenNebula");
            nebula.transform.SetParent(hiddenRoot.transform, false);
            nebula.transform.position = new Vector3(positions[i].x, positions[i].y, 0f);
            nebula.AddComponent<SpriteRenderer>();
            nebula.AddComponent<CircleCollider2D>();
            if (kind == NebulaFieldKind.Cloud)
            {
                Rigidbody2D body = nebula.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            NebulaField field = nebula.AddComponent<NebulaField>();
            field.ConfigureKind(kind);
            if (kind == NebulaFieldKind.Cloud)
                field.ConfigureCloudDrift(cloudDirection, i);

            MapInstanceService.ConfigureMember(nebula, MapInstanceService.HiddenDimensionInstanceId);
            if ((i + 1) % HiddenLocalObjectsPerFrame == 0)
                yield return null;
        }
    }

    Vector2 ResolveHiddenDimensionCenter(Vector2 hiddenSize)
    {
        Vector2 mainSize = RoomSettings.GetMapDimensions();
        float x = mainSize.x * 0.5f + hiddenSize.x * 0.5f + 260f;
        return new Vector2(Mathf.Max(420f, x), 0f);
    }

    List<Vector2> BuildExtractionPositions(LobbyMapDefinition map, Vector2 center, Vector2 size, int seed)
    {
        int count = Mathf.Max(1, map != null ? map.ExtractionZoneCount : 1);
        List<Vector2> positions = new List<Vector2>();
        System.Random random = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            float angle = count == 1 ? -Mathf.PI * 0.5f : (i / (float)count) * Mathf.PI * 2f;
            float radius = Mathf.Min(size.x, size.y) * 0.12f;
            Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            candidate += new Vector2(RandomSigned(random) * 1.2f, RandomSigned(random) * 1.2f);
            positions.Add(ClampToBounds(candidate, center, size, ExtractionMargin));
        }

        return positions;
    }

    List<Vector2> BuildObstaclePositions(LobbyMapDefinition map, Vector2 center, Vector2 size, List<Vector2> extractionPositions, int seed)
    {
        int targetCount = Mathf.Max(0, Mathf.RoundToInt(BaseObstacleCount * GetDensityMultiplier(map != null ? map.ObstacleDensity : RoomSettings.DefaultObstacleDensity) * MapInstanceService.GetMapAreaMultiplier(size)));
        List<Vector2> positions = new List<Vector2>();
        System.Random random = new System.Random(seed);
        int attempts = 0;
        while (positions.Count < targetCount && attempts < targetCount * 90 + 200)
        {
            attempts++;
            Vector2 candidate = RandomPoint(random, center, size, ObstacleMargin);
            if (!IsFarEnough(candidate, positions, MinObstacleDistance))
                continue;

            if (!IsFarEnough(candidate, extractionPositions, MinObstacleDistance * 0.72f))
                continue;

            positions.Add(candidate);
        }

        return positions;
    }

    List<Vector2> BuildNebulaPositions(string density, Vector2 center, Vector2 size, List<Vector2> extractionPositions, List<Vector2> obstaclePositions, List<Vector2> reservedPositions, int seed)
    {
        int targetCount = Mathf.Max(0, Mathf.RoundToInt(BaseNebulaCount * GetDensityMultiplier(density) * MapInstanceService.GetMapAreaMultiplier(size)));
        List<Vector2> positions = new List<Vector2>();
        System.Random random = new System.Random(seed);
        int attempts = 0;
        while (positions.Count < targetCount && attempts < targetCount * 80 + 160)
        {
            attempts++;
            Vector2 candidate = RandomPoint(random, center, size, NebulaMargin);
            if (!IsFarEnough(candidate, positions, MinNebulaDistance))
                continue;

            if (reservedPositions != null && !IsFarEnough(candidate, reservedPositions, MinNebulaDistance))
                continue;

            if (!IsFarEnough(candidate, extractionPositions, 4f))
                continue;

            if (!IsFarEnough(candidate, obstaclePositions, 3.8f))
                continue;

            positions.Add(candidate);
        }

        return positions;
    }

    List<Vector2> BuildCloudPositions(string density, Vector2 center, Vector2 size, int seed)
    {
        int targetCount = Mathf.Max(0, Mathf.RoundToInt(BaseNebulaCount * GetDensityMultiplier(density) * MapInstanceService.GetMapAreaMultiplier(size)));
        List<Vector2> positions = new List<Vector2>();
        System.Random random = new System.Random(seed);
        int attempts = 0;
        while (positions.Count < targetCount && attempts < targetCount * 50 + 100)
        {
            attempts++;
            Vector2 candidate = RandomPoint(random, center, size, NebulaMargin);
            if (IsFarEnough(candidate, positions, 3.2f))
                positions.Add(candidate);
        }

        return positions;
    }

    string BuildTreasureLayout(LobbyMapDefinition map, Vector2 center, Vector2 size, List<Vector2> extractionPositions, List<Vector2> obstaclePositions, List<Vector2> nebulaPositions, List<Vector2> fireNebulaPositions, List<Vector2> toxicNebulaPositions, int seed)
    {
        int targetCount = Mathf.Max(0, Mathf.RoundToInt(BaseTreasureCount * GetDensityMultiplier(map != null ? map.ResourceDensity : RoomSettings.DefaultTreasureDensity) * MapInstanceService.GetMapAreaMultiplier(size)));
        if (targetCount <= 0)
            return EmptyLayoutSentinel;

        List<Vector2> allNebulaPositions = new List<Vector2>(nebulaPositions);
        allNebulaPositions.AddRange(fireNebulaPositions);
        allNebulaPositions.AddRange(toxicNebulaPositions);
        List<string> entries = new List<string>();
        List<Vector2> treasurePositions = new List<Vector2>();
        System.Random random = new System.Random(seed);
        int attempts = 0;
        while (entries.Count < targetCount && attempts < targetCount * 80 + 200)
        {
            attempts++;
            Vector2 candidate = RandomPoint(random, center, size, TreasureMargin);
            if (!IsFarEnough(candidate, treasurePositions, MinTreasureDistance))
                continue;

            if (!IsFarEnough(candidate, extractionPositions, 3f))
                continue;

            if (!IsFarEnough(candidate, obstaclePositions, 2.9f))
                continue;

            if (!IsFarEnough(candidate, allNebulaPositions, 2.8f))
                continue;

            treasurePositions.Add(candidate);
            entries.Add(
                candidate.x.ToString(CultureInfo.InvariantCulture) + "," +
                candidate.y.ToString(CultureInfo.InvariantCulture) + "," +
                RollTreasureItemId(map != null ? map.ResourceRichness : RoomSettings.DefaultResourceRichness, random));
        }

        return entries.Count > 0 ? string.Join(";", entries) : EmptyLayoutSentinel;
    }

    string BuildAlienSecretsLayout(
        LobbyMapDefinition map,
        Vector2 center,
        Vector2 size,
        List<Vector2> extractionPositions,
        List<Vector2> obstaclePositions,
        List<Vector2> nebulaPositions,
        List<Vector2> fireNebulaPositions,
        List<Vector2> toxicNebulaPositions,
        List<Vector2> treasurePositions,
        int seed)
    {
        string density = LobbyMapCatalog.GetDefaultAlienSecretsDensity(map != null ? map.Id : LobbyMapCatalog.HiddenDimensionMapId);
        int targetCount = Mathf.Max(0, Mathf.RoundToInt(BaseAlienSecretCount * GetAlienSecretsDensityMultiplier(density) * MapInstanceService.GetMapAreaMultiplier(size)));
        if (targetCount <= 0)
            return EmptyLayoutSentinel;

        List<Vector2> allNebulaPositions = new List<Vector2>(nebulaPositions);
        allNebulaPositions.AddRange(fireNebulaPositions);
        allNebulaPositions.AddRange(toxicNebulaPositions);
        List<Vector2> reservedTreasurePositions = treasurePositions != null ? new List<Vector2>(treasurePositions) : new List<Vector2>();
        List<string> entries = new List<string>();
        List<Vector2> alienSecretPositions = new List<Vector2>();
        System.Random random = new System.Random(seed);
        int attempts = 0;
        while (entries.Count < targetCount && attempts < targetCount * 90 + 220)
        {
            attempts++;
            Vector2 candidate = RandomPoint(random, center, size, TreasureMargin + 0.4f);
            if (!IsFarEnough(candidate, alienSecretPositions, MinTreasureDistance))
                continue;

            if (!IsFarEnough(candidate, reservedTreasurePositions, MinTreasureDistance))
                continue;

            if (!IsFarEnough(candidate, extractionPositions, 3f))
                continue;

            if (!IsFarEnough(candidate, obstaclePositions, 2.9f))
                continue;

            if (!IsFarEnough(candidate, allNebulaPositions, 2.8f))
                continue;

            int variantIndex = random.Next(0, InventoryItemCatalog.AlienSecretVariantCount);
            string alienSecretItemId = InventoryItemCatalog.GetAlienSecretItemId(variantIndex);
            alienSecretPositions.Add(candidate);
            entries.Add(
                candidate.x.ToString(CultureInfo.InvariantCulture) + "," +
                candidate.y.ToString(CultureInfo.InvariantCulture) + "," +
                alienSecretItemId + "," +
                variantIndex.ToString(CultureInfo.InvariantCulture));
        }

        return entries.Count > 0 ? string.Join(";", entries) : EmptyLayoutSentinel;
    }

    void AddRoundTimeBonus()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        float currentDuration = RoomSettings.GetRoundDuration();
        double roundEndUtcMs = ReadRoomDouble(RoomSettings.RoundEndUtcMsKey, -1d);
        Hashtable props = new Hashtable
        {
            [RoomSettings.RoundDurationKey] = currentDuration + HiddenRiftTimeBonusSeconds
        };

        if (roundEndUtcMs > 0d)
            props[RoomSettings.RoundEndUtcMsKey] = roundEndUtcMs + HiddenRiftTimeBonusSeconds * 1000d;

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    float GetCurrentRoundRemainingTime()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return 0f;

        double startTime = ReadRoomDouble(RoomSettings.StartTimeKey, -1d);
        if (startTime < 0d)
            return RoomSettings.GetRoundDuration();

        double elapsed = PhotonNetwork.Time - startTime;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("loneShipModeStartTime", out object loneValue) &&
            loneValue is double loneStart &&
            loneStart >= 0d)
        {
            double before = loneStart - startTime;
            double after = (PhotonNetwork.Time - loneStart) * RoomSettings.GetLastShipTimerMultiplier();
            elapsed = before + after;
        }

        return Mathf.Max(0f, RoomSettings.GetRoundDuration() - (float)elapsed);
    }

    void PublishAnnouncement(int actorNumber, string message)
    {
        if (PhotonNetwork.CurrentRoom == null || string.IsNullOrWhiteSpace(message))
            return;

        string token = BuildToken("msg");
        string target = actorNumber > 0 ? actorNumber.ToString(CultureInfo.InvariantCulture) : AllPlayersAnnouncementTarget;
        Hashtable props = new Hashtable
        {
            [MapInstanceService.MapTravelMessageKey] = token + "|" + target + "|" + message
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    static string SerializeRiftState(RiftState state)
    {
        return string.Join("|", new[]
        {
            state.Token,
            state.TargetInstanceId,
            state.Origin.x.ToString(CultureInfo.InvariantCulture),
            state.Origin.y.ToString(CultureInfo.InvariantCulture),
            state.Radius.ToString(CultureInfo.InvariantCulture),
            state.StartTime.ToString(CultureInfo.InvariantCulture),
            state.Duration.ToString(CultureInfo.InvariantCulture)
        });
    }

    static bool TryParseRiftState(string raw, out RiftState state)
    {
        state = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string[] parts = raw.Split('|');
        if (parts.Length < 7)
            return false;

        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
            !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float radius) ||
            !double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out double startTime) ||
            !float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float duration))
        {
            return false;
        }

        state = new RiftState
        {
            Token = parts[0],
            TargetInstanceId = parts[1],
            Origin = new Vector2(x, y),
            Radius = radius,
            StartTime = startTime,
            Duration = duration
        };
        return true;
    }

    static string SerializePositions(List<Vector2> positions)
    {
        if (positions == null || positions.Count == 0)
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

    static List<Vector2> ParsePositions(string layout)
    {
        List<Vector2> positions = new List<Vector2>();
        if (string.IsNullOrWhiteSpace(layout) || string.Equals(layout, EmptyLayoutSentinel, System.StringComparison.Ordinal))
            return positions;

        string[] entries = layout.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string[] parts = entries[i].Split(',');
            if (parts.Length < 2)
                continue;

            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                positions.Add(new Vector2(x, y));
            }
        }

        return positions;
    }

    static string SerializeVector2(Vector2 value)
    {
        return value.x.ToString(CultureInfo.InvariantCulture) + "," + value.y.ToString(CultureInfo.InvariantCulture);
    }

    static Vector2 ParseVector2(string raw, Vector2 fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        string[] parts = raw.Split(',');
        if (parts.Length < 2)
            return fallback;

        return float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
               float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
            ? new Vector2(x, y).normalized
            : fallback;
    }

    static Vector2 RandomPoint(System.Random random, Vector2 center, Vector2 size, float margin)
    {
        float x = Mathf.Lerp(center.x - size.x * 0.5f + margin, center.x + size.x * 0.5f - margin, (float)random.NextDouble());
        float y = Mathf.Lerp(center.y - size.y * 0.5f + margin, center.y + size.y * 0.5f - margin, (float)random.NextDouble());
        return new Vector2(x, y);
    }

    static Vector2 ClampToBounds(Vector2 position, Vector2 center, Vector2 size, float margin)
    {
        float halfX = Mathf.Max(1f, size.x * 0.5f - margin);
        float halfY = Mathf.Max(1f, size.y * 0.5f - margin);
        return new Vector2(
            Mathf.Clamp(position.x, center.x - halfX, center.x + halfX),
            Mathf.Clamp(position.y, center.y - halfY, center.y + halfY));
    }

    static bool IsFarEnough(Vector2 candidate, List<Vector2> positions, float minDistance)
    {
        if (positions == null)
            return true;

        for (int i = 0; i < positions.Count; i++)
        {
            if (Vector2.Distance(candidate, positions[i]) < minDistance)
                return false;
        }

        return true;
    }

    static float GetDensityMultiplier(string density)
    {
        switch (RoomSettings.NormalizeSpaceJunkDensity(density))
        {
            case RoomSettings.SpaceJunkDensityNone:
                return 0f;
            case RoomSettings.SpaceJunkDensityLow:
                return 0.5f;
            case RoomSettings.SpaceJunkDensityHigh:
                return 2f;
        }

        switch (RoomSettings.NormalizeTreasureDensity(density))
        {
            case RoomSettings.TreasureDensityNone:
                return 0f;
            case RoomSettings.TreasureDensityVeryLow:
                return 0.25f;
            case RoomSettings.TreasureDensityLow:
                return 0.5f;
            case RoomSettings.TreasureDensityHigh:
                return 2f;
            default:
                return 1f;
        }
    }

    static float GetAlienSecretsDensityMultiplier(string density)
    {
        switch (RoomSettings.NormalizeAlienSecretsDensity(density))
        {
            case RoomSettings.AlienSecretsDensityNone:
                return 0f;
            case RoomSettings.AlienSecretsDensityVeryLow:
                return 0.25f;
            case RoomSettings.AlienSecretsDensityLow:
                return 0.5f;
            case RoomSettings.AlienSecretsDensityHigh:
                return 2f;
            case RoomSettings.AlienSecretsDensityVeryHigh:
                return 3f;
            default:
                return 1f;
        }
    }

    static string RollTreasureItemId(string richness, System.Random random)
    {
        float[] weights = GetResourceRichnessWeights(richness);
        float total = 0f;
        for (int i = 0; i < weights.Length; i++)
            total += Mathf.Max(0f, weights[i]);

        if (total <= 0f)
            return InventoryItemCatalog.AsteroidResourceId;

        float roll = (float)random.NextDouble() * total;
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

    static float[] GetResourceRichnessWeights(string richness)
    {
        switch (RoomSettings.NormalizeResourceRichness(richness))
        {
            case RoomSettings.ResourceRichnessExtremelyLow:
                return ExtremelyLowRichnessWeights;
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

    static bool ConsumeWeight(ref float roll, float[] weights, int index)
    {
        if (weights == null || index < 0 || index >= weights.Length)
            return false;

        float weight = Mathf.Max(0f, weights[index]);
        if (roll < weight)
            return true;

        roll -= weight;
        return false;
    }

    static Vector2 BuildCloudDirection(int seed)
    {
        System.Random random = new System.Random(seed);
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
    }

    static Vector2 ResolveHiddenEnemySpawn(Vector2 center, Vector2 size, int ordinal)
    {
        float angle = Mathf.Abs((ordinal + 3) * 1.917f) % (Mathf.PI * 2f);
        float radius = Mathf.Min(size.x, size.y) * Mathf.Lerp(0.22f, 0.42f, Mathf.Repeat(ordinal * 0.37f, 1f));
        return ClampToBounds(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, center, size, 5f);
    }

    static Vector2 ResolveHiddenRiftWardenSpawn(MapInstanceService.BoundsInfo bounds, int ordinal)
    {
        Vector2 anchor = bounds.Center;
        List<Vector2> secrets = ParsePositions(GetRoomString(MapInstanceService.HiddenDimensionAlienSecretsLayoutKey));
        if (secrets.Count > 0)
        {
            for (int i = 0; i < secrets.Count; i++)
                anchor += secrets[i];

            anchor /= secrets.Count + 1;
        }
        else
        {
            List<Vector2> portals = ParsePositions(GetRoomString(MapInstanceService.HiddenDimensionExtractionLayoutKey));
            if (portals.Count > 0)
                anchor = portals[0];
        }

        float angle = Mathf.Abs((ordinal + 11) * 2.399f) % (Mathf.PI * 2f);
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 3.4f;
        return ClampToBounds(anchor + offset, bounds.Center, bounds.InnerSize, 4.5f);
    }

    static float RandomSigned(System.Random random)
    {
        return ((float)random.NextDouble() * 2f) - 1f;
    }

    static string BuildToken(string prefix)
    {
        return prefix + "_" + PhotonNetwork.Time.ToString("F3", CultureInfo.InvariantCulture) + "_" + Random.Range(1000, 9999);
    }

    static int BuildStableSeed(string value)
    {
        unchecked
        {
            int seed = 17291;
            string roomName = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name ?? string.Empty : string.Empty;
            string raw = roomName + "_" + value;
            for (int i = 0; i < raw.Length; i++)
                seed = (seed * 397) ^ raw[i];

            return seed;
        }
    }

    static string GetRoomString(string key)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value) &&
            value is string raw)
        {
            return raw;
        }

        return string.Empty;
    }

    static bool GetRoomBool(string key, bool fallback)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value))
        {
            if (value is bool boolValue)
                return boolValue;

            if (value is int intValue)
                return intValue != 0;
        }

        return fallback;
    }

    static double ReadRoomDouble(string key, double fallback)
    {
        if (PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value))
        {
            return fallback;
        }

        return value switch
        {
            double asDouble => asDouble,
            float asFloat => asFloat,
            int asInt => asInt,
            _ => fallback
        };
    }

    static Sprite LoadBackgroundSprite(int backgroundIndex)
    {
        string path = "Visuals/Backgrounds/background" + Mathf.Clamp(backgroundIndex, 1, 99) + "_resource";
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
            texture = Resources.Load<Texture2D>("Visuals/Backgrounds/background5_resource");

        if (texture == null)
            return null;

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(100f, Mathf.Max(texture.width, texture.height)));
    }

    static Sprite LoadParallaxBackgroundSprite(string backgroundId)
    {
        backgroundId = RoomSettings.NormalizeParallaxBackgroundId(backgroundId);
        string path = "Visuals/Backgrounds/" + backgroundId + "_resource";
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
            return Resources.Load<Sprite>(path);

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(100f, Mathf.Max(texture.width, texture.height)));
    }

    static void FitSpriteToCover(SpriteRenderer renderer, Vector2 targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Vector2 spriteSize = renderer.sprite.bounds.size;
        if (spriteSize.x <= 0.001f || spriteSize.y <= 0.001f)
            return;

        float scale = Mathf.Max(targetSize.x / spriteSize.x, targetSize.y / spriteSize.y);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    static Sprite LoadBackgroundObjectSprite(string objectId)
    {
        objectId = RoomSettings.NormalizeBackgroundObjectId(objectId);
        if (string.Equals(objectId, RoomSettings.BackgroundObjectOff, System.StringComparison.Ordinal))
            return null;

        string resourcePath = "Visuals/Backgrounds/" + objectId + "_resource";
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            texture = Resources.Load<Texture2D>("Visuals/Backgrounds/" + objectId);

        if (texture != null)
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(100f, Mathf.Max(texture.width, texture.height)));

        return Resources.Load<Sprite>(resourcePath);
    }
}
