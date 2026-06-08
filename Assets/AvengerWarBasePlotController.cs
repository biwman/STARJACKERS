using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

public sealed class AvengerWarBasePlotController : MonoBehaviour
{
    const string PlotStartTimeKey = "avengerPlot.runtime.startTime";
    const string BaseXKey = "avengerPlot.runtime.baseX";
    const string BaseYKey = "avengerPlot.runtime.baseY";
    const string TurretsSpawnedKey = "avengerPlot.runtime.turretsSpawned";
    const string TurretViewIdsKey = "avengerPlot.runtime.turretViewIds";
    const string DefenceInactiveKey = "avengerPlot.runtime.defenceInactive";
    const string AvengerLaunchedKey = "avengerPlot.runtime.avengerLaunched";
    const string LaunchPlayerViewIdKey = "avengerPlot.runtime.launchPlayerViewId";
    const string AmbushSpawnedKey = "avengerPlot.runtime.ambushSpawned";
    const string AmbushDroneMarker = "avenger_plot_guard_drone";

    const float ScanInterval = 0.35f;
    const float BaseWorldSize = 8.7f;
    const float BaseClearanceRadius = 5.2f;
    const float AmbushSpawnStepDelay = 0.18f;
    const float PlotMatchTimeTolerance = 0.001f;
    const float PlotMatchPositionTolerance = 0.08f;

    static readonly Vector2[] TurretOffsets =
    {
        new Vector2(-0.22f, 0.22f),
        new Vector2(0.22f, 0.22f),
        new Vector2(-0.22f, -0.22f),
        new Vector2(0.22f, -0.22f)
    };

    static AvengerWarBasePlotController instance;

    double handledStartTime = double.MinValue;
    double localOperationalAnnouncementStartTime = double.MinValue;
    double localInactiveAnnouncementStartTime = double.MinValue;
    double masterPlotInitializationStartedTime = double.MinValue;
    double masterTurretsSpawnStartedTime = double.MinValue;
    double masterAmbushSpawnStartedTime = double.MinValue;
    float nextScanTime;
    AvengerWarBase localBase;
    Coroutine ambushSpawnRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("AvengerWarBasePlotController");
        instance = root.AddComponent<AvengerWarBasePlotController>();
        DontDestroyOnLoad(root);
    }

    public static Vector2 GetTurretWorldOffset(int slotIndex)
    {
        int safeIndex = Mathf.Abs(slotIndex) % TurretOffsets.Length;
        return TurretOffsets[safeIndex] * BaseWorldSize;
    }

    public static void MarkAvengerLaunched(int playerViewId)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || playerViewId <= 0)
            return;

        PhotonHashtable props = new PhotonHashtable
        {
            [AvengerLaunchedKey] = true,
            [LaunchPlayerViewIdKey] = playerViewId
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
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
            handledStartTime = double.MinValue;
            ResetMasterRoundGuards();
            DestroyLocalBase();
            return;
        }

        if (!IsPlotSupportedAndEnabled())
        {
            ResetMasterRoundGuards();
            DestroyLocalBase();
            return;
        }

        if (currentStartTime != handledStartTime)
        {
            handledStartTime = currentStartTime;
            ResetMasterRoundGuards();
            DestroyLocalBase();
        }

        if (PhotonNetwork.IsMasterClient)
            TickMasterLifecycle(currentStartTime);

        if (TryReadPlotState(currentStartTime, out PlotState state))
        {
            EnsureLocalBase(state);
            ShowLocalOperationalAnnouncement(currentStartTime);
            if (state.DefenceInactive)
                ShowDefenceInactiveAnnouncement(currentStartTime);
        }
        else
        {
            DestroyLocalBase();
        }
    }

    void TickMasterLifecycle(double currentStartTime)
    {
        if (!TryReadPlotState(currentStartTime, out PlotState state))
        {
            if (Mathf.Abs((float)(masterPlotInitializationStartedTime - currentStartTime)) <= PlotMatchTimeTolerance)
                return;

            if (!AnyPlayerHasAvengerStartingCodes())
                return;

            masterPlotInitializationStartedTime = currentStartTime;
            Vector2 basePosition = ResolveBasePosition();
            PhotonHashtable props = new PhotonHashtable
            {
                [PlotStartTimeKey] = currentStartTime,
                [BaseXKey] = basePosition.x,
                [BaseYKey] = basePosition.y,
                [TurretsSpawnedKey] = false,
                [TurretViewIdsKey] = string.Empty,
                [DefenceInactiveKey] = false,
                [AvengerLaunchedKey] = false,
                [LaunchPlayerViewIdKey] = 0,
                [AmbushSpawnedKey] = false
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            return;
        }

        CleanupOrphanWarBaseTurrets(state, currentStartTime);

        if (!state.TurretsSpawned)
        {
            if (Mathf.Abs((float)(masterTurretsSpawnStartedTime - currentStartTime)) <= PlotMatchTimeTolerance)
                return;

            masterTurretsSpawnStartedTime = currentStartTime;
            int[] turretViewIds = SpawnWarBaseTurrets(state.BasePosition, currentStartTime);
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                [TurretsSpawnedKey] = true,
                [TurretViewIdsKey] = SerializeViewIds(turretViewIds)
            });
            state.TurretsSpawned = true;
            state.TurretViewIds = turretViewIds;
        }

        if (state.TurretsSpawned && !state.DefenceInactive && !HasActiveWarBaseTurrets(state, currentStartTime))
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable { [DefenceInactiveKey] = true });
            state.DefenceInactive = true;
        }

        if (state.AvengerLaunched && !state.AmbushSpawned)
        {
            if (!IsLaunchPlayerConfirmedAvenger(state))
                return;

            if (Mathf.Abs((float)(masterAmbushSpawnStartedTime - currentStartTime)) <= PlotMatchTimeTolerance)
                return;

            masterAmbushSpawnStartedTime = currentStartTime;
            if (ambushSpawnRoutine != null)
                StopCoroutine(ambushSpawnRoutine);

            ambushSpawnRoutine = StartCoroutine(SpawnAvengerAmbushRoutine(state, currentStartTime));
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable { [AmbushSpawnedKey] = true });
        }
    }

    int[] SpawnWarBaseTurrets(Vector2 basePosition, double currentStartTime)
    {
        List<int> viewIds = new List<int>(TurretOffsets.Length);
        for (int i = 0; i < TurretOffsets.Length; i++)
        {
            Vector2 spawn = basePosition + GetTurretWorldOffset(i);
            GameObject turret = PhotonNetwork.InstantiateRoomObject(
                "Player",
                spawn,
                Quaternion.identity,
                0,
                new object[] { PlayerDeployableRuntime.WarBaseRocketAutoTurretMarker, 0, i, currentStartTime, basePosition.x, basePosition.y });

            if (turret != null)
            {
                PlayerDeployableRuntime.EnsureAttached(turret);

                PhotonView turretView = turret.GetComponent<PhotonView>();
                if (turretView != null && turretView.ViewID > 0)
                    viewIds.Add(turretView.ViewID);
            }
        }

        return viewIds.ToArray();
    }

    IEnumerator SpawnAvengerAmbushRoutine(PlotState state, double currentStartTime)
    {
        Vector2 focus = ResolveLaunchFocus(state);
        Vector2 escapePoint = ResolveEscapePoint(focus);
        Vector2 escapeDirection = escapePoint - focus;
        if (escapeDirection.sqrMagnitude < 0.001f)
            escapeDirection = Vector2.up;
        escapeDirection.Normalize();

        Vector2 nearDrone = ClampToMapBounds(focus + Rotate(escapeDirection, 72f) * 10.8f);
        Vector2 nearRider = ClampToMapBounds(focus + Rotate(escapeDirection, -78f) * 12.6f);
        Vector2 escapeDrone = ClampToMapBounds(escapePoint + Rotate(escapeDirection, 125f) * 4.8f);
        Vector2 randomDrone = ResolveRandomAmbushPoint(focus, escapePoint, 13.5f, 5.5f);
        Vector2 randomRider = ResolveRandomAmbushPoint(focus, escapePoint, 15.5f, 6.5f);

        if (!CanContinueAmbushSpawn(state, currentStartTime))
        {
            ambushSpawnRoutine = null;
            yield break;
        }

        SpawnGuardDrone(nearDrone);
        yield return new WaitForSeconds(AmbushSpawnStepDelay);

        if (!CanContinueAmbushSpawn(state, currentStartTime))
        {
            ambushSpawnRoutine = null;
            yield break;
        }

        SpawnAggressiveRider(nearRider, ShipCatalog.ViperStandardSkinIndex, "MIL_AI_1", 700, state.LaunchPlayerViewId);
        yield return new WaitForSeconds(AmbushSpawnStepDelay);

        if (!CanContinueAmbushSpawn(state, currentStartTime))
        {
            ambushSpawnRoutine = null;
            yield break;
        }

        SpawnGuardDrone(escapeDrone);
        yield return new WaitForSeconds(AmbushSpawnStepDelay);

        if (!CanContinueAmbushSpawn(state, currentStartTime))
        {
            ambushSpawnRoutine = null;
            yield break;
        }

        SpawnGuardDrone(randomDrone);
        yield return new WaitForSeconds(AmbushSpawnStepDelay);

        if (!CanContinueAmbushSpawn(state, currentStartTime))
        {
            ambushSpawnRoutine = null;
            yield break;
        }

        SpawnAggressiveRider(randomRider, ShipCatalog.InvaderCamoSkinIndex, "MIL_AI_2", 701, state.LaunchPlayerViewId);
        GameVisualTheme.RequestRuntimeRefresh();
        ambushSpawnRoutine = null;
    }

    bool CanContinueAmbushSpawn(PlotState state, double currentStartTime)
    {
        return PhotonNetwork.IsMasterClient &&
               IsRoundStarted(out double activeStartTime) &&
               Mathf.Abs((float)(activeStartTime - currentStartTime)) <= PlotMatchTimeTolerance &&
               IsLaunchPlayerConfirmedAvenger(state);
    }

    bool IsLaunchPlayerConfirmedAvenger(PlotState state)
    {
        if (state.LaunchPlayerViewId <= 0)
            return false;

        PhotonView playerView = PhotonView.Find(state.LaunchPlayerViewId);
        if (playerView == null || playerView.Owner == null)
            return false;

        PlayerHealth player = playerView.GetComponent<PlayerHealth>();
        if (player == null || player.IsBotControlled || player.IsNeutralRiderControlled || player.IsAstronautControlled)
            return false;

        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(playerView.Owner, ShipCatalog.ExplorerBasicSkinIndex);
        return ShipCatalog.GetShipTypeFromSkinIndex(shipSkinIndex) == ShipType.Avenger;
    }

    void SpawnGuardDrone(Vector2 position)
    {
        EnemyBotDefinition droneDefinition = EnemyBotCatalog.GetDefinition(EnemyBotKind.Drone);
        if (droneDefinition == null)
            return;

        GameObject droneObject = PhotonNetwork.Instantiate(
            "Player",
            position,
            Quaternion.identity,
            0,
            new object[] { droneDefinition.InstantiationMarker, AmbushDroneMarker });

        if (droneObject == null)
            return;

        EnemyBot bot = droneObject.GetComponent<EnemyBot>();
        if (bot == null)
            bot = droneObject.AddComponent<EnemyBot>();

        bot.InitializeFromPhotonData();
    }

    void SpawnAggressiveRider(Vector2 position, int skinIndex, string name, int ordinal, int targetViewId)
    {
        object[] data = NeutralRiderController.BuildAggressiveInstantiationData(skinIndex, name, ordinal, targetViewId);
        GameObject riderObject = PhotonNetwork.Instantiate("Player", position, Quaternion.identity, 0, data);
        if (riderObject == null)
            return;

        NeutralRiderController rider = riderObject.GetComponent<NeutralRiderController>();
        if (rider == null)
            rider = riderObject.AddComponent<NeutralRiderController>();

        rider.InitializeFromPhotonData();
        ActorIdentity.Ensure(riderObject);
    }

    Vector2 ResolveLaunchFocus(PlotState state)
    {
        if (state.LaunchPlayerViewId > 0)
        {
            PhotonView launchedView = PhotonView.Find(state.LaunchPlayerViewId);
            if (launchedView != null)
                return launchedView.transform.position;
        }

        return state.BasePosition + AvengerWarBase.AvengerPadOffset * BaseWorldSize;
    }

    Vector2 ResolveEscapePoint(Vector2 focus)
    {
        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        if (zones == null || zones.Length == 0)
            return ClampToMapBounds(new Vector2(0f, RoomSettings.GetMapDimensions().y * 0.42f));

        ExtractionZone best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < zones.Length; i++)
        {
            ExtractionZone zone = zones[i];
            if (zone == null)
                continue;

            float distance = Vector2.Distance(focus, zone.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = zone;
        }

        return best != null ? (Vector2)best.transform.position : focus;
    }

    Vector2 ResolveRandomAmbushPoint(Vector2 focus, Vector2 escapePoint, float minFocusDistance, float minEscapeDistance)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(6f, mapSize.x * 0.5f - 2.2f);
        float halfY = Mathf.Max(6f, mapSize.y * 0.5f - 2.2f);

        for (int i = 0; i < 24; i++)
        {
            Vector2 candidate = new Vector2(
                UnityEngine.Random.Range(-halfX, halfX),
                UnityEngine.Random.Range(-halfY, halfY));
            if (Vector2.Distance(candidate, focus) < minFocusDistance)
                continue;
            if (Vector2.Distance(candidate, escapePoint) < minEscapeDistance)
                continue;
            if (!IsAmbushSpawnAreaClear(candidate))
                continue;

            return candidate;
        }

        Vector2 direction = UnityEngine.Random.insideUnitCircle;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.right;
        return ClampToMapBounds(focus + direction.normalized * Mathf.Max(minFocusDistance, 14f));
    }

    Vector2 ClampToMapBounds(Vector2 position)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(6f, mapSize.x * 0.5f - 1.6f);
        float halfY = Mathf.Max(6f, mapSize.y * 0.5f - 1.6f);
        return new Vector2(
            Mathf.Clamp(position.x, -halfX, halfX),
            Mathf.Clamp(position.y, -halfY, halfY));
    }

    bool IsAmbushSpawnAreaClear(Vector2 candidate)
    {
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(candidate, 1.15f, out Collider2D[] hits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (hit.GetComponentInParent<PlayerHealth>() != null ||
                hit.GetComponentInParent<ObstacleChunk>() != null ||
                hit.GetComponentInParent<MovingSpaceObject>() != null ||
                hit.GetComponentInParent<RepairBay>() != null ||
                hit.GetComponentInParent<SpaceFactory>() != null ||
                hit.GetComponentInParent<ScienceStation>() != null)
            {
                return false;
            }
        }

        return true;
    }

    static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }

    bool HasActiveWarBaseTurrets(PlotState state, double currentStartTime)
    {
        if (state.TurretViewIds != null && state.TurretViewIds.Length >= TurretOffsets.Length)
        {
            for (int i = 0; i < state.TurretViewIds.Length; i++)
            {
                if (IsTrackedWarBaseTurretActive(state.TurretViewIds[i]))
                    return true;
            }

            return false;
        }

        IReadOnlyCollection<PlayerDeployableBase> deployables = PlayerDeployableBase.GetActiveDeployables();
        foreach (PlayerDeployableBase deployable in deployables)
        {
            if (deployable is RocketAutoTurretDeployable turret &&
                turret.IsWarBaseDefenseTurret &&
                deployable.CanBeTargeted &&
                IsWarBaseTurretForCurrentPlot(turret, state, currentStartTime))
            {
                return true;
            }
        }

        return false;
    }

    bool IsTrackedWarBaseTurretActive(int viewId)
    {
        if (viewId <= 0)
            return false;

        PhotonView turretView = PhotonView.Find(viewId);
        if (turretView == null)
            return false;

        if (!PlayerDeployableRuntime.IsWarBaseRocketAutoTurretData(turretView.InstantiationData))
            return false;

        PlayerDeployableBase deployable = turretView.GetComponent<PlayerDeployableBase>();
        return deployable == null || deployable.CanBeTargeted;
    }

    void CleanupOrphanWarBaseTurrets(PlotState state, double currentStartTime)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        List<GameObject> orphans = null;
        IReadOnlyCollection<PlayerDeployableBase> deployables = PlayerDeployableBase.GetActiveDeployables();
        foreach (PlayerDeployableBase deployable in deployables)
        {
            if (deployable is not RocketAutoTurretDeployable turret || !turret.IsWarBaseDefenseTurret)
                continue;

            if (IsWarBaseTurretForCurrentPlot(turret, state, currentStartTime))
                continue;

            orphans ??= new List<GameObject>();
            orphans.Add(turret.gameObject);
        }

        if (orphans == null)
            return;

        for (int i = 0; i < orphans.Count; i++)
        {
            if (orphans[i] == null)
                continue;

            if (PhotonNetwork.InRoom)
                PhotonNetwork.Destroy(orphans[i]);
            else
                Destroy(orphans[i]);
        }
    }

    bool IsWarBaseTurretForCurrentPlot(RocketAutoTurretDeployable turret, PlotState state, double currentStartTime)
    {
        if (turret == null || turret.photonView == null)
            return false;

        object[] data = turret.photonView.InstantiationData;
        if (!PlayerDeployableRuntime.IsWarBaseRocketAutoTurretData(data))
            return false;

        if (state.HasTrackedTurretViewId(turret.photonView.ViewID))
            return true;

        if (data != null && data.Length >= 6 && TryConvertToDouble(data[3], out double turretStartTime))
        {
            float baseX = ConvertToFloat(data[4], float.NaN);
            float baseY = ConvertToFloat(data[5], float.NaN);
            if (!float.IsNaN(baseX) && !float.IsNaN(baseY))
            {
                return Mathf.Abs((float)(turretStartTime - currentStartTime)) <= PlotMatchTimeTolerance &&
                       Vector2.Distance(new Vector2(baseX, baseY), state.BasePosition) <= PlotMatchPositionTolerance;
            }
        }

        return IsNearCurrentWarBaseTurretSlot(turret.transform.position, state.BasePosition);
    }

    bool IsNearCurrentWarBaseTurretSlot(Vector2 position, Vector2 basePosition)
    {
        for (int i = 0; i < TurretOffsets.Length; i++)
        {
            Vector2 slotPosition = basePosition + GetTurretWorldOffset(i);
            if (Vector2.Distance(position, slotPosition) <= 0.95f)
                return true;
        }

        return false;
    }

    void EnsureLocalBase(PlotState state)
    {
        if (localBase == null)
        {
            GameObject root = new GameObject("AvengerWarBase");
            localBase = root.AddComponent<AvengerWarBase>();
        }

        localBase.Configure(state.BasePosition, BaseWorldSize, state.DefenceInactive, state.AvengerLaunched);
    }

    void ShowLocalOperationalAnnouncement(double currentStartTime)
    {
        if (localOperationalAnnouncementStartTime == currentStartTime)
            return;

        if (!PlayerProfileService.PlayerHasAvengerStartingCodes(PhotonNetwork.LocalPlayer))
            return;

        localOperationalAnnouncementStartTime = currentStartTime;
        RoundAnnouncementUI.Show("Military installation is operational. Beware of its defences!", 3.4f);
    }

    void ShowDefenceInactiveAnnouncement(double currentStartTime)
    {
        if (localInactiveAnnouncementStartTime == currentStartTime)
            return;

        localInactiveAnnouncementStartTime = currentStartTime;
        RoundAnnouncementUI.Show("Military base defence inactive", 2.8f);
    }

    bool TryReadPlotState(double currentStartTime, out PlotState state)
    {
        state = default;
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        ExitGames.Client.Photon.Hashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (!props.TryGetValue(PlotStartTimeKey, out object startValue) ||
            !TryConvertToDouble(startValue, out double storedStartTime) ||
            Mathf.Abs((float)(storedStartTime - currentStartTime)) > 0.001f)
        {
            return false;
        }

        float x = props.TryGetValue(BaseXKey, out object xValue) ? ConvertToFloat(xValue, 0f) : 0f;
        float y = props.TryGetValue(BaseYKey, out object yValue) ? ConvertToFloat(yValue, 0f) : 0f;
        bool turretsSpawned = props.TryGetValue(TurretsSpawnedKey, out object turretsValue) && turretsValue is bool turrets && turrets;
        bool defenceInactive = props.TryGetValue(DefenceInactiveKey, out object inactiveValue) && inactiveValue is bool inactive && inactive;
        bool avengerLaunched = props.TryGetValue(AvengerLaunchedKey, out object launchedValue) && launchedValue is bool launched && launched;
        bool ambushSpawned = props.TryGetValue(AmbushSpawnedKey, out object ambushValue) && ambushValue is bool ambush && ambush;
        int launchPlayerViewId = props.TryGetValue(LaunchPlayerViewIdKey, out object viewIdValue) ? ConvertToInt(viewIdValue, 0) : 0;
        int[] turretViewIds = props.TryGetValue(TurretViewIdsKey, out object turretViewIdsValue)
            ? DeserializeViewIds(turretViewIdsValue)
            : Array.Empty<int>();

        state = new PlotState(new Vector2(x, y), turretsSpawned, defenceInactive, avengerLaunched, ambushSpawned, launchPlayerViewId, turretViewIds);
        return true;
    }

    bool IsRoundStarted(out double currentStartTime)
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

    bool IsPlotSupportedAndEnabled()
    {
        string mapId = RoomSettings.GetSelectedLobbyMapId();
        return ShipUnlockPlotCoordinator.IsActivePlot(ShipUnlockPlotType.Avenger) &&
               RoomSettings.IsAvengerPlotEnabled() &&
               LobbyMapCatalog.IsAvengerPlotEnabledByDefault(mapId);
    }

    bool AnyPlayerHasAvengerStartingCodes()
    {
        Photon.Realtime.Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            if (PlayerProfileService.PlayerHasAvengerStartingCodes(players[i]))
                return true;
        }

        return false;
    }

    Vector2 ResolveBasePosition()
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(7f, mapSize.x * 0.5f - BaseClearanceRadius);
        float halfY = Mathf.Max(7f, mapSize.y * 0.5f - BaseClearanceRadius);
        Vector2 focus = ResolveCodeCarrierPosition();
        Vector2[] candidates =
        {
            new Vector2(focus.x + halfX * 0.35f, focus.y + halfY * 0.28f),
            new Vector2(0f, halfY * 0.45f),
            new Vector2(halfX * 0.42f, 0f),
            new Vector2(-halfX * 0.42f, 0f),
            new Vector2(0f, -halfY * 0.42f)
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            Vector2 clamped = new Vector2(
                Mathf.Clamp(candidates[i].x, -halfX, halfX),
                Mathf.Clamp(candidates[i].y, -halfY, halfY));
            if (IsBasePositionClear(clamped))
                return clamped;
        }

        return Vector2.zero;
    }

    Vector2 ResolveCodeCarrierPosition()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player == null || player.photonView == null || player.IsBotControlled || player.IsNeutralRiderControlled || player.IsAstronautControlled)
                continue;

            if (PlayerProfileService.PlayerHasAvengerStartingCodes(player.photonView.Owner))
                return player.transform.position;
        }

        return Vector2.zero;
    }

    bool IsBasePositionClear(Vector2 candidate)
    {
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(candidate, BaseClearanceRadius, out Collider2D[] hits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (hit.GetComponentInParent<PlayerHealth>() != null ||
                hit.GetComponentInParent<ObstacleChunk>() != null ||
                hit.GetComponentInParent<MovingSpaceObject>() != null ||
                hit.GetComponentInParent<RepairBay>() != null ||
                hit.GetComponentInParent<SpaceFactory>() != null ||
                hit.GetComponentInParent<ScienceStation>() != null)
            {
                return false;
            }
        }

        return true;
    }

    void ResetLocalState()
    {
        handledStartTime = double.MinValue;
        localOperationalAnnouncementStartTime = double.MinValue;
        localInactiveAnnouncementStartTime = double.MinValue;
        ResetMasterRoundGuards();
        DestroyLocalBase();
    }

    void ResetMasterRoundGuards()
    {
        masterPlotInitializationStartedTime = double.MinValue;
        masterTurretsSpawnStartedTime = double.MinValue;
        masterAmbushSpawnStartedTime = double.MinValue;

        if (ambushSpawnRoutine != null)
        {
            StopCoroutine(ambushSpawnRoutine);
            ambushSpawnRoutine = null;
        }
    }

    void DestroyLocalBase()
    {
        if (localBase == null)
            return;

        Destroy(localBase.gameObject);
        localBase = null;
    }

    static bool TryConvertToDouble(object value, out double result)
    {
        if (value is double doubleValue)
        {
            result = doubleValue;
            return true;
        }

        if (value is float floatValue)
        {
            result = floatValue;
            return true;
        }

        if (value is int intValue)
        {
            result = intValue;
            return true;
        }

        result = 0d;
        return false;
    }

    static float ConvertToFloat(object value, float fallback)
    {
        if (value is float floatValue)
            return floatValue;
        if (value is double doubleValue)
            return (float)doubleValue;
        if (value is int intValue)
            return intValue;

        return fallback;
    }

    static int ConvertToInt(object value, int fallback)
    {
        if (value is int intValue)
            return intValue;
        if (value is short shortValue)
            return shortValue;
        if (value is byte byteValue)
            return byteValue;
        if (value is float floatValue)
            return Mathf.RoundToInt(floatValue);
        if (value is double doubleValue)
            return Mathf.RoundToInt((float)doubleValue);

        return fallback;
    }

    static string SerializeViewIds(int[] viewIds)
    {
        if (viewIds == null || viewIds.Length == 0)
            return string.Empty;

        return string.Join(",", viewIds);
    }

    static int[] DeserializeViewIds(object value)
    {
        if (value is int[] intArray)
            return intArray;

        if (value is string serialized && !string.IsNullOrWhiteSpace(serialized))
        {
            string[] parts = serialized.Split(',');
            List<int> ids = new List<int>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], out int id) && id > 0)
                    ids.Add(id);
            }

            return ids.ToArray();
        }

        return Array.Empty<int>();
    }

    struct PlotState
    {
        public Vector2 BasePosition;
        public bool TurretsSpawned;
        public bool DefenceInactive;
        public bool AvengerLaunched;
        public bool AmbushSpawned;
        public int LaunchPlayerViewId;
        public int[] TurretViewIds;

        public PlotState(Vector2 basePosition, bool turretsSpawned, bool defenceInactive, bool avengerLaunched, bool ambushSpawned, int launchPlayerViewId, int[] turretViewIds)
        {
            BasePosition = basePosition;
            TurretsSpawned = turretsSpawned;
            DefenceInactive = defenceInactive;
            AvengerLaunched = avengerLaunched;
            AmbushSpawned = ambushSpawned;
            LaunchPlayerViewId = launchPlayerViewId;
            TurretViewIds = turretViewIds ?? Array.Empty<int>();
        }

        public bool HasTrackedTurretViewId(int viewId)
        {
            if (viewId <= 0 || TurretViewIds == null)
                return false;

            for (int i = 0; i < TurretViewIds.Length; i++)
            {
                if (TurretViewIds[i] == viewId)
                    return true;
            }

            return false;
        }
    }
}

public sealed class AvengerWarBase : MonoBehaviour
{
    public static readonly Vector2 PlayerPadOffset = new Vector2(-0.340f, 0.011f);
    public static readonly Vector2 AvengerPadOffset = new Vector2(0.338f, 0.009f);

    const float InteractionRadius = 1.35f;
    const float BoardingSeconds = 5f;
    const float LandingMoveSeconds = 1.1f;

    static readonly List<AvengerWarBase> ActiveBases = new List<AvengerWarBase>();

    SpriteRenderer baseRenderer;
    SpriteRenderer avengerRenderer;
    CircleCollider2D interactionCollider;
    bool defenceInactive;
    bool avengerLaunched;
    float worldSize;
    Coroutine boardingRoutine;

    public Vector2 PlayerLandingPoint => (Vector2)transform.position + PlayerPadOffset * worldSize;
    public Vector2 AvengerLaunchPoint => (Vector2)transform.position + AvengerPadOffset * worldSize;

    public static AvengerWarBase FindClosestUsable(Vector2 position)
    {
        AvengerWarBase best = null;
        float bestDistance = float.MaxValue;
        for (int i = ActiveBases.Count - 1; i >= 0; i--)
        {
            AvengerWarBase candidate = ActiveBases[i];
            if (candidate == null)
            {
                ActiveBases.RemoveAt(i);
                continue;
            }

            if (!candidate.CanLocalPlayerUse())
                continue;

            float distance = Vector2.Distance(position, candidate.PlayerLandingPoint);
            if (distance > InteractionRadius || distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = candidate;
        }

        return best;
    }

    void OnEnable()
    {
        if (!ActiveBases.Contains(this))
            ActiveBases.Add(this);
    }

    void OnDisable()
    {
        ActiveBases.Remove(this);
    }

    void OnDestroy()
    {
        ActiveBases.Remove(this);
    }

    public void Configure(Vector2 center, float baseWorldSize, bool defenceIsInactive, bool launched)
    {
        worldSize = Mathf.Max(1f, baseWorldSize);
        defenceInactive = defenceIsInactive;
        avengerLaunched = launched;
        transform.position = new Vector3(center.x, center.y, 0f);
        EnsureVisuals();
        RefreshVisuals();
    }

    public bool TryStartUse(PlayerHealth player)
    {
        if (player == null || player.photonView == null || !player.photonView.IsMine || boardingRoutine != null)
            return false;

        if (!CanLocalPlayerUse())
            return false;

        if (Vector2.Distance(player.transform.position, PlayerLandingPoint) > InteractionRadius + 0.45f)
            return false;

        boardingRoutine = StartCoroutine(BoardingRoutine(player));
        return true;
    }

    bool CanLocalPlayerUse()
    {
        return defenceInactive &&
               !avengerLaunched &&
               PlayerProfileService.HasInstance &&
               PlayerProfileService.Instance.HasAvengerStartingCodesInShip();
    }

    IEnumerator BoardingRoutine(PlayerHealth player)
    {
        RoundAnnouncementUI.Show("Checking starting codes", 2.3f);

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        PlayerShooting shooting = player.GetComponent<PlayerShooting>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        RigidbodyType2D originalBodyType = RigidbodyType2D.Dynamic;
        RigidbodyConstraints2D originalConstraints = RigidbodyConstraints2D.None;
        bool originalSimulated = true;
        bool hadBody = body != null;
        if (hadBody)
        {
            originalBodyType = body.bodyType;
            originalConstraints = body.constraints;
            originalSimulated = body.simulated;
            body.simulated = true;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }
        if (shooting != null)
            shooting.enabled = false;

        Vector3 startPosition = player.transform.position;
        Vector3 landingPoint = new Vector3(PlayerLandingPoint.x, PlayerLandingPoint.y, startPosition.z);
        float elapsed = 0f;
        while (elapsed < BoardingSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / LandingMoveSeconds);
            t = t * t * (3f - 2f * t);
            player.transform.position = Vector3.Lerp(startPosition, landingPoint, t);
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            yield return null;
        }

        int originalSkin = RoomSettings.GetPlayerShipSkin(
            PhotonNetwork.LocalPlayer,
            PlayerProfileService.Instance.CurrentProfile != null ? PlayerProfileService.Instance.CurrentProfile.ShipSkinIndex : ShipCatalog.ExplorerBasicSkinIndex);
        Task<bool> beginAttemptTask = PlayerProfileService.Instance.BeginAvengerTheftAttemptAsync(originalSkin);
        while (!beginAttemptTask.IsCompleted)
            yield return null;

        bool accepted = !beginAttemptTask.IsFaulted && !beginAttemptTask.IsCanceled && beginAttemptTask.Result;
        if (!accepted)
        {
            if (beginAttemptTask.Exception != null)
                Debug.LogError("Avenger starting codes check failed: " + beginAttemptTask.Exception);

            RestorePlayerControl(movement, shooting, body, hadBody, originalBodyType, originalConstraints, originalSimulated);
            boardingRoutine = null;
            yield break;
        }

        int avengerSkin = ShipCatalog.AvengerDarkGreenSkinIndex;
        PlayerProfileService.Instance.SetActiveRoundShipSkin(avengerSkin);
        RoundAnnouncementUI.Show("Starting sequence initiated", 2.5f);
        Vector2 launch = AvengerLaunchPoint;
        player.photonView.RPC(nameof(PlayerHealth.BeginAvengerShipOverrideRpc), RpcTarget.All, avengerSkin, launch.x, launch.y, 0f);
        AvengerWarBasePlotController.MarkAvengerLaunched(player.photonView.ViewID);
        avengerLaunched = true;
        RefreshVisuals();

        RestorePlayerControl(movement, shooting, body, hadBody, originalBodyType, originalConstraints, originalSimulated);
        boardingRoutine = null;
    }

    void RestorePlayerControl(
        PlayerMovement movement,
        PlayerShooting shooting,
        Rigidbody2D body,
        bool hadBody,
        RigidbodyType2D originalBodyType,
        RigidbodyConstraints2D originalConstraints,
        bool originalSimulated)
    {
        if (hadBody && body != null)
        {
            body.bodyType = originalBodyType;
            body.constraints = originalConstraints;
            body.simulated = originalSimulated;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        if (movement != null)
            movement.enabled = true;
        if (shooting != null)
            shooting.enabled = true;
    }

    void EnsureVisuals()
    {
        if (baseRenderer == null)
        {
            GameObject visual = new GameObject("WarBaseVisual");
            visual.transform.SetParent(transform, false);
            baseRenderer = visual.AddComponent<SpriteRenderer>();
            baseRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            baseRenderer.sortingOrder = GameVisualTheme.RepairBaySortingOrder;
        }

        if (avengerRenderer == null)
        {
            GameObject visual = new GameObject("ParkedAvengerVisual");
            visual.transform.SetParent(transform, false);
            avengerRenderer = visual.AddComponent<SpriteRenderer>();
            avengerRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            avengerRenderer.sortingOrder = GameVisualTheme.PlayerSortingOrder - 1;
        }

        if (interactionCollider == null)
        {
            interactionCollider = gameObject.GetComponent<CircleCollider2D>();
            if (interactionCollider == null)
                interactionCollider = gameObject.AddComponent<CircleCollider2D>();

            interactionCollider.isTrigger = true;
        }
    }

    void RefreshVisuals()
    {
        if (baseRenderer != null)
        {
            baseRenderer.sprite = RuntimeSpriteUtility.LoadSprite("Visuals/Bases/war_base", "Assets/Resources/Visuals/Bases/war_base.png");
            baseRenderer.color = defenceInactive ? Color.white : new Color(1f, 0.94f, 0.82f, 1f);
            RuntimeSpriteUtility.FitRenderer(baseRenderer, worldSize);
        }

        if (avengerRenderer != null)
        {
            avengerRenderer.sprite = RuntimeSpriteUtility.LoadSprite(
                ShipCatalog.GetShipSkinResourcePath(ShipCatalog.AvengerDarkGreenSkinIndex),
                ShipCatalog.GetShipSkinEditorResourcePath(ShipCatalog.AvengerDarkGreenSkinIndex));
            avengerRenderer.transform.localPosition = new Vector3(AvengerPadOffset.x * worldSize, AvengerPadOffset.y * worldSize, -0.04f);
            avengerRenderer.transform.localRotation = Quaternion.identity;
            avengerRenderer.color = Color.white;
            RuntimeSpriteUtility.FitRenderer(avengerRenderer, GameVisualTheme.PlayerTargetSize * 1.1f);
            avengerRenderer.gameObject.SetActive(!avengerLaunched);
        }

        if (interactionCollider != null)
        {
            interactionCollider.radius = InteractionRadius;
            interactionCollider.offset = PlayerPadOffset * worldSize;
        }
    }
}
