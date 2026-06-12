using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

public sealed class BisonIndustrialPlotController : MonoBehaviour
{
    const float ScanInterval = 0.35f;
    const float PlotMatchTimeTolerance = 0.001f;
    const float ZoneVisualWorldSize = 5.8f;
    const float PartsSpawnOffsetX = 1.55f;
    const float PartsSpawnOffsetY = -0.12f;
    const float PartsExtractionGraceDistance = 1.15f;
    const string PersistentHintOwnerKey = "bison-industrial-haul";
    const string StartAnnouncement = "Industrial Zone needs to evacuate its industrial parts. They offer cargo ship as a reward.";
    const string HaulHint = "Escape with industrial parts through Extraction Zone";

    const string PlotStartTimeKey = "bisonPlot.runtime.startTime";
    const string ZoneXKey = "bisonPlot.runtime.zoneX";
    const string ZoneYKey = "bisonPlot.runtime.zoneY";
    const string PartsViewIdKey = "bisonPlot.runtime.partsViewId";
    const string PartsVariantKey = "bisonPlot.runtime.partsVariant";
    const string PartsDestroyedKey = "bisonPlot.runtime.partsDestroyed";
    const string PartsExtractedKey = "bisonPlot.runtime.partsExtracted";
    const string FirstHaulDoneKey = "bisonPlot.runtime.firstHaulDone";
    const string AmbushSpawnedKey = "bisonPlot.runtime.ambushSpawned";
    const string PartsHaulerViewIdKey = "bisonPlot.runtime.partsHaulerViewId";
    const string GuardMarker = "bison_plot_extraction_guard";

    static BisonIndustrialPlotController instance;
    static int cachedPartsViewId;
    static IndustrialPartsHaulable cachedParts;

    double handledStartTime = double.MinValue;
    double localStartAnnouncementTime = double.MinValue;
    float nextScanTime;
    GameObject zoneVisualObject;
    double visualStartTime = double.MinValue;

    struct PlotState
    {
        public double StartTime;
        public Vector2 ZonePosition;
        public int PartsViewId;
        public int PartsVariantIndex;
        public bool PartsDestroyed;
        public bool PartsExtracted;
        public bool FirstHaulDone;
        public bool AmbushSpawned;
        public int PartsHaulerViewId;
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

        GameObject root = new GameObject("BisonIndustrialPlotController");
        instance = root.AddComponent<BisonIndustrialPlotController>();
        DontDestroyOnLoad(root);
    }

    public static bool IsIndustrialPartsInstantiationData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               string.Equals(marker, PlayerDeployableRuntime.BisonIndustrialPartsMarker, StringComparison.Ordinal);
    }

    public static bool CanStartHaul(PlayerHealth player)
    {
        return TryGetUsablePartsForPlayer(player, out IndustrialPartsHaulable parts) &&
               parts.CanLocalPlayerStartHaul(player);
    }

    public static bool TryStartHaul(PlayerHealth player)
    {
        if (!TryGetUsablePartsForPlayer(player, out IndustrialPartsHaulable parts))
            return false;

        return parts.TryBeginLocalHaul(player);
    }

    public static bool CanDropHaul(PlayerHealth player)
    {
        return player != null &&
               player.photonView != null &&
               TryGetCurrentParts(out IndustrialPartsHaulable parts) &&
               parts.IsHauledBy(player.photonView.ViewID);
    }

    public static bool TryDropHaul(PlayerHealth player)
    {
        if (!CanDropHaul(player))
            return false;

        if (!TryGetCurrentParts(out IndustrialPartsHaulable parts))
            return false;

        parts.RequestDrop(player.photonView.ViewID);
        return true;
    }

    public static float GetHaulSpeedMultiplier(int playerViewId)
    {
        if (playerViewId <= 0 || !TryGetCurrentParts(out IndustrialPartsHaulable parts))
            return 1f;

        return parts.IsHauledBy(playerViewId) ? 0.3f : 1f;
    }

    public static bool TryGetHaulChargeProgress(PlayerHealth player, out float progress)
    {
        progress = 0f;
        if (player == null || player.photonView == null || !TryGetCurrentParts(out IndustrialPartsHaulable parts))
            return false;

        return parts.TryGetLocalHaulChargeProgress(player.photonView.ViewID, out progress);
    }

    public static bool IsHaulChargeInProgress(PlayerHealth player)
    {
        return TryGetHaulChargeProgress(player, out _);
    }

    public static void NotifyPlayerDamaged(PlayerHealth player, WeaponHitContext hitContext)
    {
        if (player == null || player.photonView == null)
            return;

        if (string.Equals(hitContext.DamageSource, "nebula", StringComparison.Ordinal))
            return;

        if (!TryGetCurrentParts(out IndustrialPartsHaulable parts))
            return;

        int playerViewId = player.photonView.ViewID;
        if (parts.IsHauledBy(playerViewId) || parts.IsChargingBy(playerViewId))
            parts.RequestDrop(playerViewId);
    }

    public static bool TryEvacuateIndustrialPartsWithPlayer(ExtractionZone zone, PlayerHealth player)
    {
        if (zone == null || player == null || instance == null || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return false;

        return instance.TryEvacuateIndustrialPartsInternal(zone, player);
    }

    public static void MarkPartsDestroyed(int partsViewId)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            [PartsDestroyedKey] = true,
            [PartsHaulerViewIdKey] = 0
        });
        RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
    }

    public static void MarkPartsHauled(int playerViewId)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        PhotonHashtable props = new PhotonHashtable
        {
            [PartsHaulerViewIdKey] = Mathf.Max(0, playerViewId)
        };

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(FirstHaulDoneKey, out object firstValue) ||
            !(firstValue is bool firstHaulDone) ||
            !firstHaulDone)
        {
            props[FirstHaulDoneKey] = true;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    public static void MarkPartsDropped(int playerViewId)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        PhotonHashtable props = new PhotonHashtable { [PartsHaulerViewIdKey] = 0 };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    static bool TryGetUsablePartsForPlayer(PlayerHealth player, out IndustrialPartsHaulable parts)
    {
        parts = null;
        if (player == null || player.photonView == null || player.IsAstronautControlled || player.IsWreck || player.IsEvacuationAnimating)
            return false;

        if (!ShipUnlockPlotCoordinator.IsActivePlot(ShipUnlockPlotType.Bison))
            return false;

        if (!TryGetCurrentParts(out parts))
            return false;

        return parts != null && !parts.IsMissionResolved;
    }

    static bool TryGetCurrentParts(out IndustrialPartsHaulable parts)
    {
        parts = null;
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PartsViewIdKey, out object value) ||
            !TryConvertToInt(value, out int partsViewId) ||
            partsViewId <= 0)
        {
            return false;
        }

        if (cachedPartsViewId == partsViewId && cachedParts != null)
        {
            parts = cachedParts;
            return true;
        }

        PhotonView partsView = PhotonView.Find(partsViewId);
        if (partsView == null)
            return false;

        parts = partsView.GetComponent<IndustrialPartsHaulable>();
        if (parts == null)
            parts = partsView.gameObject.AddComponent<IndustrialPartsHaulable>();

        parts.InitializeFromPhotonData();
        cachedPartsViewId = partsViewId;
        cachedParts = parts;
        return parts != null;
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

    void OnDestroy()
    {
        ClearLocalVisuals();
        RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            ResetLocalState();
            return;
        }

        float now = Time.unscaledTime;
        if (now < nextScanTime)
            return;

        nextScanTime = now + ScanInterval;
        TickLifecycle();
    }

    void TickLifecycle()
    {
        if (!IsRoundStarted(out double currentStartTime))
        {
            ResetLocalState();
            return;
        }

        if (currentStartTime != handledStartTime)
        {
            handledStartTime = currentStartTime;
            localStartAnnouncementTime = double.MinValue;
            RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
            ClearLocalVisuals();
        }

        if (!ShipUnlockPlotCoordinator.IsActivePlot(ShipUnlockPlotType.Bison))
        {
            RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
            ClearLocalVisuals();
            return;
        }

        if (PhotonNetwork.IsMasterClient)
            TickMasterLifecycle(currentStartTime);

        if (TryReadPlotState(currentStartTime, out PlotState state))
        {
            EnsureRuntimeObjects(state);
            ShowStartAnnouncement(currentStartTime);
            UpdateLocalHint(state);

            if (PhotonNetwork.IsMasterClient)
                TickMasterAmbush(state);
        }
        else
        {
            RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
            ClearLocalVisuals();
        }
    }

    void TickMasterLifecycle(double currentStartTime)
    {
        if (TryReadPlotState(currentStartTime, out _))
            return;

        if (!AnyPlayerEligibleForMission())
            return;

        SpawnMissionObjects(currentStartTime);
    }

    void SpawnMissionObjects(double currentStartTime)
    {
        Vector2 zonePosition = ResolveZoneSpawnPosition();
        int variantIndex = UnityEngine.Random.Range(0, IndustrialPartsHaulable.AssetVariantCount);
        Vector2 partsPosition = zonePosition + new Vector2(PartsSpawnOffsetX, PartsSpawnOffsetY);

        GameObject partsObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            partsPosition,
            Quaternion.identity,
            0,
            new object[] { PlayerDeployableRuntime.BisonIndustrialPartsMarker, 0, variantIndex });

        if (partsObject == null)
            return;

        PlayerDeployableRuntime.EnsureAttached(partsObject);
        PhotonView partsView = partsObject.GetComponent<PhotonView>();
        int partsViewId = partsView != null ? partsView.ViewID : 0;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            [PlotStartTimeKey] = currentStartTime,
            [ZoneXKey] = zonePosition.x,
            [ZoneYKey] = zonePosition.y,
            [PartsViewIdKey] = partsViewId,
            [PartsVariantKey] = variantIndex,
            [PartsDestroyedKey] = false,
            [PartsExtractedKey] = false,
            [FirstHaulDoneKey] = false,
            [AmbushSpawnedKey] = false,
            [PartsHaulerViewIdKey] = 0
        });
        GameVisualTheme.RequestRuntimeRefresh();
    }

    bool TryEvacuateIndustrialPartsInternal(ExtractionZone zone, PlayerHealth player)
    {
        if (!PhotonNetwork.IsMasterClient || !IsRoundStarted(out double currentStartTime))
            return false;

        if (!TryReadPlotState(currentStartTime, out PlotState state) ||
            state.PartsDestroyed ||
            state.PartsExtracted ||
            state.PartsViewId <= 0)
        {
            return false;
        }

        PhotonView playerView = player.photonView;
        PhotonView partsView = PhotonView.Find(state.PartsViewId);
        IndustrialPartsHaulable parts = partsView != null ? partsView.GetComponent<IndustrialPartsHaulable>() : null;
        if (playerView == null || parts == null || parts.IsMissionResolved)
            return false;

        bool hauledByPlayer = parts.IsHauledBy(playerView.ViewID);
        bool partsInsideZone = zone.GetInteractionDistanceToPoint(parts.transform.position) <= PartsExtractionGraceDistance;
        if (!hauledByPlayer && !partsInsideZone)
            return false;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            [PartsExtractedKey] = true,
            [PartsHaulerViewIdKey] = 0
        });

        Vector2 target = zone.GetEvacuationTargetWorldPosition();
        parts.photonView.RPC(nameof(IndustrialPartsHaulable.BeginIndustrialPartsEvacuationRpc), RpcTarget.All, target.x, target.y);
        RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
        return true;
    }

    void TickMasterAmbush(PlotState state)
    {
        if (!state.FirstHaulDone || state.AmbushSpawned || state.PartsDestroyed || state.PartsExtracted)
            return;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable { [AmbushSpawnedKey] = true });
        SpawnExtractionGuards(state.PartsHaulerViewId);
    }

    void SpawnExtractionGuards(int haulerViewId)
    {
        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        for (int i = 0; i < zones.Length; i++)
        {
            ExtractionZone zone = zones[i];
            if (zone == null)
                continue;

            Vector2 position = ResolveGuardSpawnPosition(zone, haulerViewId);
            SpawnGuardEnemy(position, ResolveRandomGuardKind(), haulerViewId);
        }
    }

    EnemyBotKind ResolveRandomGuardKind()
    {
        return UnityEngine.Random.Range(0, 2) == 0 ? EnemyBotKind.PirateFighter : EnemyBotKind.Drone;
    }

    Vector2 ResolveGuardSpawnPosition(ExtractionZone zone, int haulerViewId)
    {
        Vector2 zonePosition = zone != null ? (Vector2)zone.transform.position : Vector2.zero;
        Vector2 awayFromHauler = UnityEngine.Random.insideUnitCircle;
        PhotonView haulerView = haulerViewId > 0 ? PhotonView.Find(haulerViewId) : null;
        if (haulerView != null)
            awayFromHauler = zonePosition - (Vector2)haulerView.transform.position;

        if (awayFromHauler.sqrMagnitude < 0.001f)
            awayFromHauler = UnityEngine.Random.insideUnitCircle;
        if (awayFromHauler.sqrMagnitude < 0.001f)
            awayFromHauler = Vector2.up;

        awayFromHauler.Normalize();
        Vector2 side = new Vector2(-awayFromHauler.y, awayFromHauler.x);
        Vector2 offset = awayFromHauler * UnityEngine.Random.Range(4.8f, 7.2f) + side * UnityEngine.Random.Range(-2.25f, 2.25f);
        return ClampToMapBounds(zonePosition + offset);
    }

    void SpawnGuardEnemy(Vector2 position, EnemyBotKind kind, int targetViewId)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        GameObject enemyObject = PhotonNetwork.Instantiate(
            "Player",
            ClampToMapBounds(position),
            Quaternion.identity,
            0,
            new object[] { definition.InstantiationMarker, GuardMarker });

        if (enemyObject == null)
            return;

        EnemyBot bot = enemyObject.GetComponent<EnemyBot>();
        if (bot == null)
            bot = enemyObject.AddComponent<EnemyBot>();

        bot.InitializeFromPhotonData();
        if (targetViewId > 0)
            bot.ForceCombatTarget(targetViewId);

        GameVisualTheme.RequestRuntimeRefresh();
    }

    void EnsureRuntimeObjects(PlotState state)
    {
        EnsureZoneVisual(state);
        if (state.PartsViewId > 0)
        {
            PhotonView partsView = PhotonView.Find(state.PartsViewId);
            if (partsView != null)
                PlayerDeployableRuntime.EnsureAttached(partsView.gameObject);
        }
    }

    void EnsureZoneVisual(PlotState state)
    {
        if (zoneVisualObject != null && visualStartTime == state.StartTime)
            return;

        ClearLocalVisuals();
        visualStartTime = state.StartTime;
        zoneVisualObject = new GameObject("BisonIndustrialZoneVisual");
        zoneVisualObject.transform.position = new Vector3(state.ZonePosition.x, state.ZonePosition.y, -0.06f);
        SpriteRenderer renderer = zoneVisualObject.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeSpriteUtility.LoadSprite("Visuals/Bases/industrial_zone", "Assets/Resources/Visuals/Bases/industrial_zone.png");
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = 18;
        renderer.color = Color.white;
        RuntimeSpriteUtility.FitRenderer(renderer, ZoneVisualWorldSize);
    }

    void ShowStartAnnouncement(double currentStartTime)
    {
        if (localStartAnnouncementTime == currentStartTime)
            return;

        localStartAnnouncementTime = currentStartTime;
        RoundAnnouncementUI.Show(StartAnnouncement, 4.5f);
    }

    void UpdateLocalHint(PlotState state)
    {
        if (state.PartsDestroyed || state.PartsExtracted)
        {
            RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
            return;
        }

        PlayerHealth localPlayer = GetLocalRoundPlayer();
        if (localPlayer != null && CanDropHaul(localPlayer))
            RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, HaulHint);
        else
            RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
    }

    Vector2 ResolveZoneSpawnPosition()
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(8f, mapSize.x * 0.5f - 7f);
        float halfY = Mathf.Max(8f, mapSize.y * 0.5f - 7f);
        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);

        for (int attempt = 0; attempt < 48; attempt++)
        {
            Vector2 candidate = new Vector2(
                UnityEngine.Random.Range(-halfX, halfX),
                UnityEngine.Random.Range(-halfY, halfY));

            if (IsFarEnoughFromExtractionZones(candidate, zones, 10f))
                return candidate;
        }

        return new Vector2(-halfX * 0.35f, halfY * 0.15f);
    }

    bool IsFarEnoughFromExtractionZones(Vector2 candidate, ExtractionZone[] zones, float minDistance)
    {
        if (zones == null)
            return true;

        for (int i = 0; i < zones.Length; i++)
        {
            ExtractionZone zone = zones[i];
            if (zone != null && Vector2.Distance(candidate, zone.transform.position) < minDistance)
                return false;
        }

        return true;
    }

    Vector2 ClampToMapBounds(Vector2 position)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(4f, mapSize.x * 0.5f - 3f);
        float halfY = Mathf.Max(4f, mapSize.y * 0.5f - 3f);
        return new Vector2(Mathf.Clamp(position.x, -halfX, halfX), Mathf.Clamp(position.y, -halfY, halfY));
    }

    bool AnyPlayerEligibleForMission()
    {
        Photon.Realtime.Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            if (PlayerProfileService.PlayerNeedsBisonIndustrialParts(players[i]))
                return true;
        }

        return false;
    }

    bool TryReadPlotState(double currentStartTime, out PlotState state)
    {
        state = default;
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        PhotonHashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (!props.TryGetValue(PlotStartTimeKey, out object startValue) ||
            !TryConvertToDouble(startValue, out double startTime) ||
            Mathf.Abs((float)(startTime - currentStartTime)) > PlotMatchTimeTolerance)
        {
            return false;
        }

        float zoneX = props.TryGetValue(ZoneXKey, out object zoneXValue) ? ConvertToFloat(zoneXValue) : 0f;
        float zoneY = props.TryGetValue(ZoneYKey, out object zoneYValue) ? ConvertToFloat(zoneYValue) : 0f;
        state = new PlotState
        {
            StartTime = startTime,
            ZonePosition = new Vector2(zoneX, zoneY),
            PartsViewId = props.TryGetValue(PartsViewIdKey, out object partsValue) && TryConvertToInt(partsValue, out int partsViewId) ? partsViewId : 0,
            PartsVariantIndex = props.TryGetValue(PartsVariantKey, out object variantValue) && TryConvertToInt(variantValue, out int variant) ? variant : 0,
            PartsDestroyed = props.TryGetValue(PartsDestroyedKey, out object destroyedValue) && destroyedValue is bool destroyed && destroyed,
            PartsExtracted = props.TryGetValue(PartsExtractedKey, out object extractedValue) && extractedValue is bool extracted && extracted,
            FirstHaulDone = props.TryGetValue(FirstHaulDoneKey, out object haulValue) && haulValue is bool firstHaul && firstHaul,
            AmbushSpawned = props.TryGetValue(AmbushSpawnedKey, out object ambushValue) && ambushValue is bool ambush && ambush,
            PartsHaulerViewId = props.TryGetValue(PartsHaulerViewIdKey, out object haulerValue) && TryConvertToInt(haulerValue, out int haulerViewId) ? haulerViewId : 0
        };

        return true;
    }

    static PlayerHealth GetLocalRoundPlayer()
    {
        if (PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.TagObject is GameObject tagged)
        {
            PlayerHealth taggedHealth = tagged.GetComponent<PlayerHealth>();
            if (GameTimer.IsActiveRoundPlayer(taggedHealth))
                return taggedHealth;
        }

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player != null && player.photonView != null && player.photonView.IsMine && GameTimer.IsActiveRoundPlayer(player))
                return player;
        }

        return null;
    }

    void ResetLocalState()
    {
        handledStartTime = double.MinValue;
        localStartAnnouncementTime = double.MinValue;
        cachedPartsViewId = 0;
        cachedParts = null;
        ClearLocalVisuals();
        RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
    }

    void ClearLocalVisuals()
    {
        if (zoneVisualObject != null)
            Destroy(zoneVisualObject);

        zoneVisualObject = null;
        visualStartTime = double.MinValue;
    }

    static bool IsRoundStarted(out double currentStartTime)
    {
        currentStartTime = 0d;
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object startedValue) ||
            !(startedValue is bool started) ||
            !started)
        {
            return false;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object startValue))
            TryConvertToDouble(startValue, out currentStartTime);

        return true;
    }

    static bool TryConvertToDouble(object value, out double result)
    {
        try
        {
            switch (value)
            {
                case double d:
                    result = d;
                    return true;
                case float f:
                    result = f;
                    return true;
                case int i:
                    result = i;
                    return true;
                case long l:
                    result = l;
                    return true;
                default:
                    result = 0d;
                    return false;
            }
        }
        catch (Exception)
        {
            result = 0d;
            return false;
        }
    }

    static bool TryConvertToInt(object value, out int result)
    {
        try
        {
            switch (value)
            {
                case int i:
                    result = i;
                    return true;
                case float f:
                    result = Mathf.RoundToInt(f);
                    return true;
                case double d:
                    result = Mathf.RoundToInt((float)d);
                    return true;
                default:
                    result = 0;
                    return false;
            }
        }
        catch (Exception)
        {
            result = 0;
            return false;
        }
    }

    static float ConvertToFloat(object value)
    {
        if (value is float f)
            return f;

        if (value is double d)
            return (float)d;

        if (value is int i)
            return i;

        return 0f;
    }
}

public sealed class IndustrialPartsHaulable : PlayerDeployableBase
{
    public const int AssetVariantCount = 6;
    const float HaulStartRange = 1.65f;
    const float HaulKeepAliveRange = 2.45f;
    const float HaulChargeSeconds = 3f;
    const float FollowDistance = 1.55f;
    const float FollowStrength = 8.5f;
    const float TowDirectionTurnDegreesPerSecond = 90f;
    const float EvacuationDuration = 3.2f;
    const float ExplosionRadius = 3.15f;
    const int ExplosionDamage = 70;

    static readonly string[] ResourcePaths =
    {
        "Visuals/IndustrialParts/industrial_parts_01",
        "Visuals/IndustrialParts/industrial_parts_02",
        "Visuals/IndustrialParts/industrial_parts_03",
        "Visuals/IndustrialParts/industrial_parts_04",
        "Visuals/IndustrialParts/industrial_parts_05",
        "Visuals/IndustrialParts/industrial_parts_06"
    };

    static readonly string[] EditorPaths =
    {
        "Assets/Resources/Visuals/IndustrialParts/industrial_parts_01.png",
        "Assets/Resources/Visuals/IndustrialParts/industrial_parts_02.png",
        "Assets/Resources/Visuals/IndustrialParts/industrial_parts_03.png",
        "Assets/Resources/Visuals/IndustrialParts/industrial_parts_04.png",
        "Assets/Resources/Visuals/IndustrialParts/industrial_parts_05.png",
        "Assets/Resources/Visuals/IndustrialParts/industrial_parts_06.png"
    };

    int variantIndex;
    int haulerViewId;
    int chargingHaulerViewId;
    int ignoredCollisionHaulerViewId;
    int localChargingPlayerViewId;
    float localHaulChargeStartedAt = -1f;
    Vector2 towOffsetDirection = Vector2.down;
    bool towOffsetDirectionInitialized;
    bool evacuating;
    Coroutine localHaulRoutine;
    readonly List<IgnoredCollisionPair> ignoredHaulerCollisions = new List<IgnoredCollisionPair>();

    struct IgnoredCollisionPair
    {
        public Collider2D PartsCollider;
        public Collider2D HaulerCollider;
    }

    protected override int MaxHp => 100;
    protected override int MaxShield => 0;
    protected override float VisualTargetSize => 1.45f;
    protected override float CollisionRadius => 0.68f;
    protected override string SpriteResourcePath => ResourcePaths[Mathf.Clamp(variantIndex, 0, ResourcePaths.Length - 1)];
    protected override string EditorSpritePath => EditorPaths[Mathf.Clamp(variantIndex, 0, EditorPaths.Length - 1)];
    public bool IsMissionResolved => destroyed || evacuating;

    public void InitializeFromPhotonData()
    {
        if (initialized)
            return;

        object[] data = photonView != null ? photonView.InstantiationData : null;
        variantIndex = data != null && data.Length > 2 ? Mathf.Clamp(ConvertToInt(data[2]), 0, AssetVariantCount - 1) : 0;
        gameObject.name = "BisonIndustrialParts";
        InitializeCommon();
        ActorIdentity.Ensure(gameObject);
        RuntimeSceneQueryCache.InvalidateAll();
    }

    void Awake()
    {
        if (BisonIndustrialPlotController.IsIndustrialPartsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
    }

    void Start()
    {
        if (BisonIndustrialPlotController.IsIndustrialPartsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
    }

    void FixedUpdate()
    {
        if (!initialized || destroyed || evacuating || haulerViewId <= 0 || !PhotonNetwork.IsMasterClient)
            return;

        PhotonView haulerView = PhotonView.Find(haulerViewId);
        if (haulerView == null)
        {
            DropHaulAuthority(haulerViewId);
            return;
        }

        Vector2 desiredOffset = ResolveDesiredTowDirection(haulerView);
        if (!towOffsetDirectionInitialized)
        {
            towOffsetDirection = ResolveInitialTowDirection(haulerView, desiredOffset);
            towOffsetDirectionInitialized = true;
        }
        else
        {
            towOffsetDirection = RotateTowardDirection(
                towOffsetDirection,
                desiredOffset,
                TowDirectionTurnDegreesPerSecond * Time.fixedDeltaTime);
        }

        Vector2 target = (Vector2)haulerView.transform.position + towOffsetDirection.normalized * FollowDistance;
        Vector2 current = body != null ? body.position : (Vector2)transform.position;
        Vector2 next = Vector2.Lerp(current, target, Mathf.Clamp01(FollowStrength * Time.fixedDeltaTime));
        if (body != null)
        {
            body.linearVelocity = (next - current) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            body.MovePosition(next);
        }
        else
        {
            transform.position = next;
        }
    }

    Vector2 ResolveDesiredTowDirection(PhotonView haulerView)
    {
        Vector2 desired = haulerView != null ? -(Vector2)haulerView.transform.up : Vector2.down;
        return desired.sqrMagnitude > 0.001f ? desired.normalized : Vector2.down;
    }

    Vector2 ResolveInitialTowDirection(PhotonView haulerView, Vector2 fallbackDirection)
    {
        if (haulerView != null)
        {
            Vector2 fromHaulerToParts = (Vector2)transform.position - (Vector2)haulerView.transform.position;
            if (fromHaulerToParts.sqrMagnitude > 0.001f)
                return fromHaulerToParts.normalized;
        }

        return fallbackDirection.sqrMagnitude > 0.001f ? fallbackDirection.normalized : Vector2.down;
    }

    static Vector2 RotateTowardDirection(Vector2 current, Vector2 target, float maxDegreesDelta)
    {
        if (target.sqrMagnitude <= 0.001f)
            return current.sqrMagnitude > 0.001f ? current.normalized : Vector2.down;

        if (current.sqrMagnitude <= 0.001f)
            return target.normalized;

        float currentAngle = Mathf.Atan2(current.y, current.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
        float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, Mathf.Max(0f, maxDegreesDelta));
        float radians = nextAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    public bool CanLocalPlayerStartHaul(PlayerHealth player)
    {
        if (player == null || player.photonView == null || IsMissionResolved || haulerViewId > 0 || chargingHaulerViewId > 0)
            return false;

        if (!GameTimer.IsActiveRoundPlayer(player) || player.IsAstronautControlled)
            return false;

        return Vector2.Distance(player.transform.position, transform.position) <= HaulStartRange;
    }

    public bool TryBeginLocalHaul(PlayerHealth player)
    {
        if (!CanLocalPlayerStartHaul(player))
            return false;

        if (localHaulRoutine != null)
            return true;

        localHaulRoutine = StartCoroutine(LocalHaulRoutine(player));
        return true;
    }

    public bool IsHauledBy(int playerViewId)
    {
        return playerViewId > 0 && haulerViewId == playerViewId && !IsMissionResolved;
    }

    public bool IsChargingBy(int playerViewId)
    {
        return playerViewId > 0 && chargingHaulerViewId == playerViewId && !IsMissionResolved;
    }

    public bool TryGetLocalHaulChargeProgress(int playerViewId, out float progress)
    {
        progress = 0f;
        if (playerViewId <= 0 ||
            localChargingPlayerViewId != playerViewId ||
            localHaulRoutine == null ||
            IsMissionResolved)
        {
            return false;
        }

        if (localHaulChargeStartedAt <= 0f || !IsChargingBy(playerViewId))
            return true;

        progress = Mathf.Clamp01((Time.time - localHaulChargeStartedAt) / HaulChargeSeconds);
        return true;
    }

    public void RequestDrop(int playerViewId)
    {
        if (photonView == null || playerViewId <= 0)
            return;

        if (PhotonNetwork.IsMasterClient)
        {
            DropHaulAuthority(playerViewId);
            return;
        }

        photonView.RPC(nameof(RequestDropHaulRpc), RpcTarget.MasterClient, playerViewId);
    }

    IEnumerator LocalHaulRoutine(PlayerHealth player)
    {
        int playerViewId = player != null && player.photonView != null ? player.photonView.ViewID : 0;
        localChargingPlayerViewId = playerViewId;
        localHaulChargeStartedAt = -1f;
        photonView.RPC(nameof(RequestStartHaulChargeRpc), RpcTarget.MasterClient, playerViewId);
        float approvalDeadline = Time.time + 1.5f;
        while (!IsChargingBy(playerViewId))
        {
            if (!CanContinueLocalCharge(player, playerViewId) ||
                Time.time >= approvalDeadline ||
                haulerViewId > 0 ||
                (chargingHaulerViewId > 0 && chargingHaulerViewId != playerViewId))
            {
                ClearLocalHaulChargeProgress(playerViewId);
                yield break;
            }

            yield return null;
        }

        float startTime = Time.time;
        localHaulChargeStartedAt = startTime;

        while (Time.time - startTime < HaulChargeSeconds)
        {
            if (!IsChargingBy(playerViewId) || !CanContinueLocalCharge(player, playerViewId))
            {
                RequestDrop(playerViewId);
                ClearLocalHaulChargeProgress(playerViewId);
                yield break;
            }

            yield return null;
        }

        if (IsChargingBy(playerViewId) && CanContinueLocalCharge(player, playerViewId))
        {
            photonView.RPC(nameof(RequestAttachHaulRpc), RpcTarget.MasterClient, playerViewId);
        }
        else
        {
            RequestDrop(playerViewId);
        }

        ClearLocalHaulChargeProgress(playerViewId);
    }

    void ClearLocalHaulChargeProgress(int playerViewId)
    {
        if (playerViewId <= 0 || localChargingPlayerViewId == playerViewId)
        {
            localChargingPlayerViewId = 0;
            localHaulChargeStartedAt = -1f;
        }

        localHaulRoutine = null;
    }

    bool CanContinueLocalCharge(PlayerHealth player, int playerViewId)
    {
        if (player == null || player.photonView == null || player.photonView.ViewID != playerViewId || IsMissionResolved)
            return false;

        if (!GameTimer.IsActiveRoundPlayer(player) || player.IsAstronautControlled || player.IsWreck || player.IsEvacuationAnimating)
            return false;

        return Vector2.Distance(player.transform.position, transform.position) <= HaulKeepAliveRange;
    }

    bool IsAuthoritativeRpc(PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null || messageInfo.Sender == null)
            return true;

        return messageInfo.Sender.ActorNumber == PhotonNetwork.MasterClient.ActorNumber;
    }

    bool IsRequestFromPlayerOwner(int playerViewId, PhotonMessageInfo messageInfo)
    {
        PhotonView playerView = PhotonView.Find(playerViewId);
        return playerView != null &&
               playerView.Owner != null &&
               messageInfo.Sender != null &&
               playerView.Owner.ActorNumber == messageInfo.Sender.ActorNumber;
    }

    bool CanMasterUseHauler(int playerViewId, float maxDistance)
    {
        if (!PhotonNetwork.IsMasterClient || playerViewId <= 0)
            return false;

        PhotonView playerView = PhotonView.Find(playerViewId);
        PlayerHealth player = playerView != null ? playerView.GetComponent<PlayerHealth>() : null;
        if (player == null || player.IsAstronautControlled || player.IsWreck || player.IsEvacuationAnimating || player.CurrentHP <= 0)
            return false;

        if (!GameTimer.IsActiveRoundPlayer(player))
            return false;

        return Vector2.Distance(player.transform.position, transform.position) <= maxDistance;
    }

    [PunRPC]
    void RequestStartHaulChargeRpc(int playerViewId, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient ||
            IsMissionResolved ||
            playerViewId <= 0 ||
            haulerViewId > 0 ||
            chargingHaulerViewId > 0 ||
            !IsRequestFromPlayerOwner(playerViewId, messageInfo) ||
            !CanMasterUseHauler(playerViewId, HaulStartRange + 0.25f))
        {
            return;
        }

        photonView.RPC(nameof(StartHaulChargeRpc), RpcTarget.All, playerViewId);
    }

    [PunRPC]
    void RequestAttachHaulRpc(int playerViewId, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient ||
            IsMissionResolved ||
            playerViewId <= 0 ||
            !IsRequestFromPlayerOwner(playerViewId, messageInfo))
        {
            return;
        }

        if ((haulerViewId > 0 && haulerViewId != playerViewId) ||
            chargingHaulerViewId != playerViewId)
        {
            return;
        }

        if (!CanMasterUseHauler(playerViewId, HaulKeepAliveRange))
        {
            DropHaulAuthority(playerViewId);
            return;
        }

        photonView.RPC(nameof(AttachHaulRpc), RpcTarget.All, playerViewId);
        BisonIndustrialPlotController.MarkPartsHauled(playerViewId);
    }

    [PunRPC]
    void RequestDropHaulRpc(int playerViewId, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient ||
            playerViewId <= 0 ||
            !IsRequestFromPlayerOwner(playerViewId, messageInfo))
        {
            return;
        }

        DropHaulAuthority(playerViewId);
    }

    void DropHaulAuthority(int playerViewId)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null)
            return;

        if (playerViewId > 0 && haulerViewId != playerViewId && chargingHaulerViewId != playerViewId)
            return;

        photonView.RPC(nameof(DropHaulRpc), RpcTarget.All, playerViewId);
        BisonIndustrialPlotController.MarkPartsDropped(playerViewId);
    }

    [PunRPC]
    public void StartHaulChargeRpc(int playerViewId, PhotonMessageInfo messageInfo)
    {
        if (!IsAuthoritativeRpc(messageInfo))
            return;

        if (IsMissionResolved || playerViewId <= 0)
            return;

        if (haulerViewId > 0 || (chargingHaulerViewId > 0 && chargingHaulerViewId != playerViewId))
            return;

        chargingHaulerViewId = playerViewId;
        IgnoreHaulerCollisions(playerViewId);
        TractorBeamVfx.StartBeam(playerViewId, photonView != null ? photonView.ViewID : 0, Vector2.up, 0f, 0f);
    }

    [PunRPC]
    public void AttachHaulRpc(int playerViewId, PhotonMessageInfo messageInfo)
    {
        if (!IsAuthoritativeRpc(messageInfo))
            return;

        if (IsMissionResolved || playerViewId <= 0)
            return;

        if (haulerViewId > 0 && haulerViewId != playerViewId)
            return;

        if (chargingHaulerViewId > 0 && chargingHaulerViewId != playerViewId)
            return;

        chargingHaulerViewId = 0;
        haulerViewId = playerViewId;
        IgnoreHaulerCollisions(playerViewId);
        TractorBeamVfx.StartBeam(playerViewId, photonView != null ? photonView.ViewID : 0, Vector2.up, 0f, 0f);
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = 80f;
            body.linearDamping = 5.5f;
            body.angularDamping = 8f;
        }

        PhotonView haulerView = PhotonView.Find(playerViewId);
        Vector2 desiredOffset = ResolveDesiredTowDirection(haulerView);
        towOffsetDirection = ResolveInitialTowDirection(haulerView, desiredOffset);
        towOffsetDirectionInitialized = true;
    }

    [PunRPC]
    public void DropHaulRpc(int playerViewId, PhotonMessageInfo messageInfo)
    {
        if (!IsAuthoritativeRpc(messageInfo))
            return;

        int sourceViewId = playerViewId > 0 ? playerViewId : Mathf.Max(haulerViewId, chargingHaulerViewId);
        if (sourceViewId > 0)
            TractorBeamVfx.StopBeam(sourceViewId);

        if (haulerViewId == playerViewId || playerViewId <= 0)
            haulerViewId = 0;

        if (chargingHaulerViewId == playerViewId || playerViewId <= 0)
            chargingHaulerViewId = 0;

        if (localChargingPlayerViewId == playerViewId || playerViewId <= 0)
            ClearLocalHaulChargeProgress(playerViewId);

        if (haulerViewId <= 0)
            towOffsetDirectionInitialized = false;

        if (ignoredCollisionHaulerViewId == playerViewId || playerViewId <= 0)
            RestoreIgnoredHaulerCollisions();

        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = 95f;
            body.linearDamping = 7.5f;
            body.angularDamping = 10f;
        }
    }

    [PunRPC]
    public void BeginIndustrialPartsEvacuationRpc(float targetX, float targetY)
    {
        if (evacuating)
            return;

        if (haulerViewId > 0)
            TractorBeamVfx.StopBeam(haulerViewId);
        if (chargingHaulerViewId > 0)
            TractorBeamVfx.StopBeam(chargingHaulerViewId);

        haulerViewId = 0;
        chargingHaulerViewId = 0;
        ClearLocalHaulChargeProgress(0);
        towOffsetDirectionInitialized = false;
        RestoreIgnoredHaulerCollisions();
        StartCoroutine(EvacuationRoutine(new Vector2(targetX, targetY)));
    }

    IEnumerator EvacuationRoutine(Vector2 target)
    {
        evacuating = true;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        Vector3 start = transform.position;
        Vector3 end = new Vector3(target.x, target.y, start.z);
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < EvacuationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / EvacuationDuration);
            float eased = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(start, end, eased);
            transform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.02f, eased);
            yield return null;
        }

        if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else if (!PhotonNetwork.InRoom)
            Destroy(gameObject);
    }

    protected override void ConfigurePhysics()
    {
        base.ConfigurePhysics();
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = 95f;
            body.linearDamping = 7.5f;
            body.angularDamping = 10f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }

    protected override void OnDestroyedByDamage()
    {
        BisonIndustrialPlotController.MarkPartsDestroyed(photonView != null ? photonView.ViewID : 0);
        ApplyExplosionDamage();
    }

    void ApplyExplosionDamage()
    {
        Vector2 center = transform.position;
        if (!PhotonNetwork.IsMasterClient)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, ExplosionRadius);
        HashSet<int> processed = new HashSet<int>();
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            PhotonView targetView = hit.GetComponentInParent<PhotonView>();
            if (targetView == null || targetView.ViewID == (photonView != null ? photonView.ViewID : 0) || !processed.Add(targetView.ViewID))
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable != this)
            {
                targetView.RPC(
                    nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                    RpcTarget.MasterClient,
                    ExplosionDamage,
                    ExplosionDamage,
                    photonView != null ? photonView.ViewID : 0,
                    center.x,
                    center.y,
                    (int)WeaponDamageType.Explosive,
                    (int)WeaponDeliveryMethod.AreaPulse,
                    (int)WeaponDeliveryFlags.AreaDamage,
                    PlayerHealth.DamageSourceExplosive);
                continue;
            }

            PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                targetView.RPC(
                    nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                    RpcTarget.MasterClient,
                    ExplosionDamage,
                    ExplosionDamage,
                    photonView != null ? photonView.ViewID : 0,
                    center.x,
                    center.y,
                    (int)WeaponDamageType.Explosive,
                    (int)WeaponDeliveryMethod.AreaPulse,
                    (int)WeaponDeliveryFlags.AreaDamage,
                    PlayerHealth.DamageSourceExplosive);
            }
        }
    }

    protected override void OnDisable()
    {
        if (haulerViewId > 0)
            TractorBeamVfx.StopBeam(haulerViewId);
        if (chargingHaulerViewId > 0)
            TractorBeamVfx.StopBeam(chargingHaulerViewId);
        RestoreIgnoredHaulerCollisions();
        base.OnDisable();
    }

    void IgnoreHaulerCollisions(int playerViewId)
    {
        if (playerViewId <= 0 || ignoredCollisionHaulerViewId == playerViewId)
            return;

        RestoreIgnoredHaulerCollisions();

        PhotonView haulerView = PhotonView.Find(playerViewId);
        if (haulerView == null)
            return;

        Collider2D[] partsColliders = GetComponentsInChildren<Collider2D>(true);
        Collider2D[] haulerColliders = haulerView.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < partsColliders.Length; i++)
        {
            Collider2D partsCollider = partsColliders[i];
            if (partsCollider == null)
                continue;

            for (int j = 0; j < haulerColliders.Length; j++)
            {
                Collider2D haulerCollider = haulerColliders[j];
                if (haulerCollider == null)
                    continue;

                Physics2D.IgnoreCollision(partsCollider, haulerCollider, true);
                ignoredHaulerCollisions.Add(new IgnoredCollisionPair
                {
                    PartsCollider = partsCollider,
                    HaulerCollider = haulerCollider
                });
            }
        }

        ignoredCollisionHaulerViewId = playerViewId;
    }

    void RestoreIgnoredHaulerCollisions()
    {
        for (int i = 0; i < ignoredHaulerCollisions.Count; i++)
        {
            IgnoredCollisionPair pair = ignoredHaulerCollisions[i];
            if (pair.PartsCollider != null && pair.HaulerCollider != null)
                Physics2D.IgnoreCollision(pair.PartsCollider, pair.HaulerCollider, false);
        }

        ignoredHaulerCollisions.Clear();
        ignoredCollisionHaulerViewId = 0;
    }

    [PunRPC]
    public new void PlayDeployableHitRpc(bool shieldHit, float x, float y)
    {
        PlayDeployableHitFeedback(shieldHit, x, y);
    }

    [PunRPC]
    public new void PlayDeployableDestroyedRpc()
    {
        EnemyBot.SpawnSpaceMineDetonationEffects(transform.position, ExplosionRadius);
    }
}
