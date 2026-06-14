using System;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

public sealed class ViperRecoveryPlotController : MonoBehaviour
{
    public const string ViperWreckMarker = "viper_recovery_wreck";

    const string RollStartTimeKey = "viperPlot.runtime.rollStartTime";
    const string RollDoneKey = "viperPlot.runtime.rollDone";
    const string PlotStartTimeKey = "viperPlot.runtime.startTime";
    const string HaulerViewIdKey = "viperPlot.runtime.haulerViewId";
    const string WreckViewIdKey = "viperPlot.runtime.wreckViewId";
    const string HaulerDestroyedKey = "viperPlot.runtime.haulerDestroyed";
    const string WreckExtractedKey = "viperPlot.runtime.wreckExtracted";
    const string SpawnDirXKey = "viperPlot.runtime.spawnDirX";
    const string SpawnDirYKey = "viperPlot.runtime.spawnDirY";
    const string PersistentHintOwnerKey = "viper_recovery_plot";
    const string DetachedHintMessage = "Push the Viper wreck to the extraction zone and then escape.";

    const float ScanInterval = 0.35f;
    public const float HaulerSpeed = 1.35f;
    public const float TowedWreckDistance = 2.85f;
    const float WreckExtractionGraceDistance = 1.25f;
    const float HaulerMapEdgeMargin = 3.5f;

    static ViperRecoveryPlotController instance;

    double handledStartTime = double.MinValue;
    double localStartAnnouncementTime = double.MinValue;
    double localDetachedAnnouncementTime = double.MinValue;
    float nextScanTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("ViperRecoveryPlotController");
        instance = root.AddComponent<ViperRecoveryPlotController>();
        DontDestroyOnLoad(root);
    }

    public static void MarkHaulerDestroyed(int haulerViewId, int wreckViewId)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return;

        PhotonHashtable props = new PhotonHashtable
        {
            [HaulerDestroyedKey] = true
        };

        if (wreckViewId > 0)
            props[WreckViewIdKey] = wreckViewId;

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        TractorBeamVfx.StopBeam(haulerViewId);
    }

    public static void UpdateHaulerTravelDirection(Vector2 direction)
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            [SpawnDirXKey] = direction.x,
            [SpawnDirYKey] = direction.y
        });
    }

    public static bool TryEvacuateWreckWithZone(ExtractionZone zone)
    {
        if (zone == null || instance == null || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return false;

        return instance.TryEvacuateWreckInternal(zone);
    }

    public static bool IsViperWreckInstantiationData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               string.Equals(marker, ViperWreckMarker, StringComparison.Ordinal);
    }

    public static Quaternion GetHaulerTravelRotation(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.right;

        direction.Normalize();
        return Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f);
    }

    public static bool TryEnsureViperWreckRuntime(GameObject target)
    {
        if (target == null)
            return false;

        PhotonView view = target.GetComponent<PhotonView>();
        if (!IsViperWreckInstantiationData(view != null ? view.InstantiationData : null))
            return false;

        ViperWreckTowTarget wreck = target.GetComponent<ViperWreckTowTarget>();
        if (wreck == null)
            wreck = target.AddComponent<ViperWreckTowTarget>();

        wreck.InitializeFromPhotonData();
        ActorIdentity.Ensure(target);
        ClearLocalTagIfViperWreck(target);
        RuntimeSceneQueryCache.InvalidateAll();
        return true;
    }

    public static void ClearLocalTagIfViperWreck(GameObject target)
    {
        if (target == null ||
            PhotonNetwork.LocalPlayer == null ||
            !ReferenceEquals(PhotonNetwork.LocalPlayer.TagObject, target))
        {
            return;
        }

        PhotonNetwork.LocalPlayer.TagObject = null;
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
            RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
            handledStartTime = currentStartTime;
            localStartAnnouncementTime = double.MinValue;
            localDetachedAnnouncementTime = double.MinValue;
        }

        if (!ShipUnlockPlotCoordinator.IsActivePlot(ShipUnlockPlotType.Viper))
        {
            RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
            TickMasterLifecycle(currentStartTime);

        if (TryReadPlotState(currentStartTime, out PlotState state))
        {
            EnsureRuntimeObjects(state);
            ShowStartAnnouncement(currentStartTime);
            if (state.HaulerDestroyed && !state.WreckExtracted)
                ShowDetachedAnnouncement(currentStartTime);

            UpdateDetachedHint(state);
        }
        else
        {
            RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
        }
    }

    void TickMasterLifecycle(double currentStartTime)
    {
        if (TryReadPlotState(currentStartTime, out _))
            return;

        if (WasRollHandledForRound(currentStartTime))
            return;

        if (!AnyPlayerEligibleForMission())
        {
            MarkRollHandled(currentStartTime);
            return;
        }

        SpawnMissionObjects(currentStartTime);
    }

    void SpawnMissionObjects(double currentStartTime)
    {
        Vector2 direction = ResolveHaulerDirection();
        Vector2 spawn = ResolveHaulerSpawn(direction);
        Vector2 wreckSpawn = spawn - direction * TowedWreckDistance;
        Quaternion wreckRotation = Quaternion.LookRotation(Vector3.forward, direction);
        Quaternion haulerRotation = GetHaulerTravelRotation(direction);

        GameObject wreckObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            wreckSpawn,
            wreckRotation,
            0,
            new object[] { ViperWreckMarker });
        if (wreckObject == null)
        {
            MarkRollHandled(currentStartTime);
            return;
        }

        ViperWreckTowTarget wreck = EnsureWreckTarget(wreckObject);
        PhotonView wreckView = wreckObject.GetComponent<PhotonView>();
        int wreckViewId = wreckView != null ? wreckView.ViewID : 0;

        GameObject haulerObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawn,
            haulerRotation,
            0,
            new object[] { PlayerDeployableRuntime.ViperContainerHaulerMarker, 0, wreckViewId, direction.x, direction.y });
        if (haulerObject == null)
        {
            MarkRollHandled(currentStartTime);
            return;
        }

        PlayerDeployableRuntime.EnsureAttached(haulerObject);
        PhotonView haulerView = haulerObject.GetComponent<PhotonView>();
        int haulerViewId = haulerView != null ? haulerView.ViewID : 0;
        if (wreck != null)
            wreck.InitializeFromPhotonData();

        PhotonHashtable props = new PhotonHashtable
        {
            [RollStartTimeKey] = currentStartTime,
            [RollDoneKey] = true,
            [PlotStartTimeKey] = currentStartTime,
            [HaulerViewIdKey] = haulerViewId,
            [WreckViewIdKey] = wreckViewId,
            [HaulerDestroyedKey] = false,
            [WreckExtractedKey] = false,
            [SpawnDirXKey] = direction.x,
            [SpawnDirYKey] = direction.y
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        GameVisualTheme.RequestRuntimeRefresh();
    }

    bool TryEvacuateWreckInternal(ExtractionZone zone)
    {
        if (!PhotonNetwork.IsMasterClient || !IsRoundStarted(out double currentStartTime))
            return false;

        if (!TryReadPlotState(currentStartTime, out PlotState state) ||
            !state.HaulerDestroyed ||
            state.WreckExtracted ||
            state.WreckViewId <= 0)
        {
            return false;
        }

        PhotonView wreckView = PhotonView.Find(state.WreckViewId);
        ViperWreckTowTarget wreck = wreckView != null ? wreckView.GetComponent<ViperWreckTowTarget>() : null;
        if (wreck == null || !wreck.CanBeExtracted)
            return false;

        if (zone.GetInteractionDistanceToPoint(wreck.transform.position) > WreckExtractionGraceDistance)
            return false;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable { [WreckExtractedKey] = true });
        Vector2 target = zone.GetEvacuationTargetWorldPosition();
        wreck.photonView.RPC(nameof(ViperWreckTowTarget.BeginWreckEvacuationRpc), RpcTarget.All, target.x, target.y);
        return true;
    }

    void EnsureRuntimeObjects(PlotState state)
    {
        if (state.HaulerViewId > 0)
        {
            PhotonView haulerView = PhotonView.Find(state.HaulerViewId);
            if (haulerView != null)
                PlayerDeployableRuntime.EnsureAttached(haulerView.gameObject);
        }

        if (state.WreckViewId > 0)
        {
            PhotonView wreckView = PhotonView.Find(state.WreckViewId);
            if (wreckView != null)
                EnsureWreckTarget(wreckView.gameObject);
        }
    }

    static ViperWreckTowTarget EnsureWreckTarget(GameObject target)
    {
        if (target == null)
            return null;

        ViperWreckTowTarget wreck = target.GetComponent<ViperWreckTowTarget>();
        if (wreck == null)
            wreck = target.AddComponent<ViperWreckTowTarget>();

        wreck.InitializeFromPhotonData();
        return wreck;
    }

    void ShowStartAnnouncement(double currentStartTime)
    {
        if (localStartAnnouncementTime == currentStartTime)
            return;

        localStartAnnouncementTime = currentStartTime;
        RoundAnnouncementUI.Show("Viper class starship is being hauled to scrapyard.", 3.8f);
    }

    void ShowDetachedAnnouncement(double currentStartTime)
    {
        if (localDetachedAnnouncementTime == currentStartTime)
            return;

        localDetachedAnnouncementTime = currentStartTime;
        RoundAnnouncementUI.Show(DetachedHintMessage, 3.2f);
    }

    void UpdateDetachedHint(PlotState state)
    {
        if (state.HaulerDestroyed && !state.WreckExtracted)
            RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, DetachedHintMessage);
        else
            RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
    }

    bool AnyPlayerEligibleForMission()
    {
        return ShipUnlockPlotCoordinator.TryGetRoundStarterPlayer(out Photon.Realtime.Player starter) &&
               PlayerProfileService.PlayerNeedsViperRecovery(starter);
    }

    bool WasRollHandledForRound(double currentStartTime)
    {
        PhotonHashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (!props.TryGetValue(RollStartTimeKey, out object startValue) ||
            !TryConvertToDouble(startValue, out double storedStart) ||
            Mathf.Abs((float)(storedStart - currentStartTime)) > 0.001f)
        {
            return false;
        }

        return props.TryGetValue(RollDoneKey, out object doneValue) && doneValue is bool done && done;
    }

    void MarkRollHandled(double currentStartTime)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            [RollStartTimeKey] = currentStartTime,
            [RollDoneKey] = true
        });
    }

    bool TryReadPlotState(double currentStartTime, out PlotState state)
    {
        state = default;
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        PhotonHashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (!props.TryGetValue(PlotStartTimeKey, out object startValue) ||
            !TryConvertToDouble(startValue, out double storedStartTime) ||
            Mathf.Abs((float)(storedStartTime - currentStartTime)) > 0.001f)
        {
            return false;
        }

        int haulerViewId = props.TryGetValue(HaulerViewIdKey, out object haulerValue) ? ConvertToInt(haulerValue, 0) : 0;
        int wreckViewId = props.TryGetValue(WreckViewIdKey, out object wreckValue) ? ConvertToInt(wreckValue, 0) : 0;
        bool haulerDestroyed = props.TryGetValue(HaulerDestroyedKey, out object destroyedValue) && destroyedValue is bool destroyed && destroyed;
        bool wreckExtracted = props.TryGetValue(WreckExtractedKey, out object extractedValue) && extractedValue is bool extracted && extracted;
        float dirX = props.TryGetValue(SpawnDirXKey, out object dirXValue) ? ConvertToFloat(dirXValue, 0f) : 0f;
        float dirY = props.TryGetValue(SpawnDirYKey, out object dirYValue) ? ConvertToFloat(dirYValue, 1f) : 1f;
        Vector2 direction = new Vector2(dirX, dirY);
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.up;

        state = new PlotState(haulerViewId, wreckViewId, haulerDestroyed, wreckExtracted, direction.normalized);
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

    Vector2 ResolveHaulerDirection()
    {
        Vector2 direction = new Vector2(UnityEngine.Random.Range(-0.45f, 0.45f), UnityEngine.Random.Range(-1f, 1f));
        if (direction.sqrMagnitude < 0.2f)
            direction = Vector2.right;

        direction.Normalize();
        if (Mathf.Abs(direction.x) < 0.35f)
            direction.x = Mathf.Sign(direction.x == 0f ? 1f : direction.x) * 0.55f;

        return direction.normalized;
    }

    Vector2 ResolveHaulerSpawn(Vector2 direction)
    {
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = Mathf.Max(8f, mapSize.x * 0.5f - 4f);
        float halfY = Mathf.Max(8f, mapSize.y * 0.5f - 4f);
        Vector2 edgeNormal = -direction;
        Vector2 spawn = new Vector2(
            edgeNormal.x >= 0f ? halfX : -halfX,
            UnityEngine.Random.Range(-halfY * 0.65f, halfY * 0.65f));

        if (Mathf.Abs(edgeNormal.y) > Mathf.Abs(edgeNormal.x))
        {
            spawn = new Vector2(
                UnityEngine.Random.Range(-halfX * 0.65f, halfX * 0.65f),
                edgeNormal.y >= 0f ? halfY : -halfY);
        }

        return spawn;
    }

    void ResetLocalState()
    {
        handledStartTime = double.MinValue;
        localStartAnnouncementTime = double.MinValue;
        localDetachedAnnouncementTime = double.MinValue;
        RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
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

    static int ConvertToInt(object value, int fallback)
    {
        if (value is int intValue)
            return intValue;

        if (value is float floatValue)
            return Mathf.RoundToInt(floatValue);

        if (value is double doubleValue)
            return Mathf.RoundToInt((float)doubleValue);

        return fallback;
    }

    static float ConvertToFloat(object value, float fallback)
    {
        if (value is float floatValue)
            return floatValue;

        if (value is int intValue)
            return intValue;

        if (value is double doubleValue)
            return (float)doubleValue;

        return fallback;
    }

    readonly struct PlotState
    {
        public readonly int HaulerViewId;
        public readonly int WreckViewId;
        public readonly bool HaulerDestroyed;
        public readonly bool WreckExtracted;
        public readonly Vector2 Direction;

        public PlotState(int haulerViewId, int wreckViewId, bool haulerDestroyed, bool wreckExtracted, Vector2 direction)
        {
            HaulerViewId = haulerViewId;
            WreckViewId = wreckViewId;
            HaulerDestroyed = haulerDestroyed;
            WreckExtracted = wreckExtracted;
            Direction = direction;
        }
    }
}

public sealed class ViperContainerHaulerDeployable : PlayerDeployableBase
{
    const float DirectionSyncInterval = 0.18f;
    const float SpeedBoostDuration = 5f;
    const float SpeedBoostMultiplier = 2f;
    const float MineDropCooldown = 15f;
    const float AutoCannonChance = 0.15f;
    const float MineRearOffset = 1.05f;
    const float MineSideOffset = 0.48f;
    const float AutoCannonRearOffset = 1.08f;

    int wreckViewId;
    Vector2 travelDirection = Vector2.right;
    bool beamStarted;
    float nextDirectionSyncTime;
    float nextMineDropTime;
    float speedBoostUntilTime;

    protected override int MaxHp => 120;
    protected override int MaxShield => 70;
    protected override float VisualTargetSize => 3.35f;
    protected override float CollisionRadius => 0.95f;
    protected override string SpriteResourcePath => "Enemies/ContainerShip/container_ship";
    protected override string EditorSpritePath => "Assets/Resources/Enemies/ContainerShip/container_ship.png";

    public void InitializeFromPhotonData()
    {
        if (initialized)
            return;

        InitializeCommon();
        object[] data = photonView != null ? photonView.InstantiationData : null;
        wreckViewId = data != null && data.Length > 2 ? ConvertToInt(data[2]) : 0;
        float dirX = data != null && data.Length > 3 ? ConvertToFloat(data[3]) : 1f;
        float dirY = data != null && data.Length > 4 ? ConvertToFloat(data[4]) : 0f;
        travelDirection = new Vector2(dirX, dirY);
        if (travelDirection.sqrMagnitude < 0.001f)
            travelDirection = Vector2.right;
        travelDirection.Normalize();
    }

    void Awake()
    {
        if (PlayerDeployableRuntime.IsViperContainerHaulerData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
    }

    void Start()
    {
        if (PlayerDeployableRuntime.IsViperContainerHaulerData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
    }

    void Update()
    {
        if (!initialized || destroyed)
            return;

        SyncTravelDirectionFromRoom();
        float currentSpeed = GetCurrentSpeed();
        if (PhotonNetwork.IsMasterClient)
            KeepInsideMapBounds(currentSpeed);

        if (travelDirection.sqrMagnitude > 0.001f)
            transform.rotation = ViperRecoveryPlotController.GetHaulerTravelRotation(travelDirection);

        if (PhotonNetwork.IsMasterClient)
            transform.position += (Vector3)(travelDirection * currentSpeed * Time.deltaTime);

        if (wreckViewId > 0 && !beamStarted && PhotonView.Find(wreckViewId) != null)
        {
            TractorBeamVfx.StartBeam(photonView.ViewID, wreckViewId, Vector2.right);
            beamStarted = true;
        }
    }

    float GetCurrentSpeed()
    {
        float multiplier = Time.time < speedBoostUntilTime ? SpeedBoostMultiplier : 1f;
        return ViperRecoveryPlotController.HaulerSpeed * multiplier;
    }

    void SyncTravelDirectionFromRoom()
    {
        if (Time.unscaledTime < nextDirectionSyncTime || PhotonNetwork.CurrentRoom == null)
            return;

        nextDirectionSyncTime = Time.unscaledTime + DirectionSyncInterval;
        ExitGames.Client.Photon.Hashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        float dirX = props.TryGetValue("viperPlot.runtime.spawnDirX", out object dirXValue) ? ConvertToFloat(dirXValue) : travelDirection.x;
        float dirY = props.TryGetValue("viperPlot.runtime.spawnDirY", out object dirYValue) ? ConvertToFloat(dirYValue) : travelDirection.y;
        Vector2 synced = new Vector2(dirX, dirY);
        if (synced.sqrMagnitude < 0.001f)
            return;

        travelDirection = synced.normalized;
    }

    void KeepInsideMapBounds(float currentSpeed)
    {
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = Mathf.Max(6f, mapSize.x * 0.5f - 3.5f);
        float halfY = Mathf.Max(6f, mapSize.y * 0.5f - 3.5f);
        Vector2 position = transform.position;
        Vector2 nextPosition = position + travelDirection * currentSpeed * Time.deltaTime * 2f;
        bool outsideOrLeaving =
            Mathf.Abs(nextPosition.x) > halfX ||
            Mathf.Abs(nextPosition.y) > halfY ||
            Mathf.Abs(position.x) > halfX ||
            Mathf.Abs(position.y) > halfY;

        if (!outsideOrLeaving)
            return;

        Vector2 centerDirection = (-position).normalized;
        if (centerDirection.sqrMagnitude < 0.001f)
            centerDirection = Vector2.right;

        travelDirection = Vector2.Lerp(travelDirection, centerDirection, 0.92f).normalized;
        ViperRecoveryPlotController.UpdateHaulerTravelDirection(travelDirection);
    }

    protected override void OnDestroyedByDamage()
    {
        ViperRecoveryPlotController.MarkHaulerDestroyed(photonView != null ? photonView.ViewID : 0, wreckViewId);
    }

    protected override void OnDamageTakenByPlayer(int attackerViewId)
    {
        if (!PhotonNetwork.IsMasterClient || destroyed)
            return;

        speedBoostUntilTime = Mathf.Max(speedBoostUntilTime, Time.time + SpeedBoostDuration);
        if (Time.time < nextMineDropTime)
            return;

        nextMineDropTime = Time.time + Mathf.Max(0.05f, MineDropCooldown * RoomSettings.GetEnemyAttackCooldownMultiplier());
        if (UnityEngine.Random.value < AutoCannonChance)
            SpawnDefensiveAutoCannon();
        else
            SpawnDefensiveMines();
    }

    void SpawnDefensiveMines()
    {
        if (!PhotonNetwork.InRoom || photonView == null)
            return;

        EnemyBotDefinition mineDefinition = EnemyBotCatalog.GetDefinition(EnemyBotKind.SpaceMine);
        if (mineDefinition == null)
            return;

        Vector2 forward = ResolveForwardDirection();
        Vector2 behind = -forward;
        Vector2 side = new Vector2(-forward.y, forward.x);

        for (int i = 0; i < 2; i++)
        {
            float sideSign = i == 0 ? -1f : 1f;
            Vector2 spawnOffset = behind * MineRearOffset + side * (MineSideOffset * sideSign);
            Vector2 driftDirection = (behind * 0.86f + side * (0.18f * sideSign)).normalized;
            Vector3 spawnPosition = transform.position + (Vector3)spawnOffset;
            GameObject mineObject = PhotonNetwork.Instantiate(
                "Player",
                spawnPosition,
                Quaternion.identity,
                0,
                new object[] { mineDefinition.InstantiationMarker, EnemyBot.ContainerShipMineMarker, photonView.ViewID, driftDirection.x, driftDirection.y });

            if (mineObject == null)
                continue;

            EnemyBot mine = mineObject.GetComponent<EnemyBot>();
            if (mine == null)
                mine = mineObject.AddComponent<EnemyBot>();

            mine.InitializeFromPhotonData();
            Rigidbody2D mineBody = mineObject.GetComponent<Rigidbody2D>();
            if (mineBody != null)
                mineBody.linearVelocity = driftDirection * Mathf.Max(0.25f, mine.EffectiveMoveSpeed);
        }
    }

    void SpawnDefensiveAutoCannon()
    {
        if (!PhotonNetwork.InRoom || photonView == null)
            return;

        Vector2 forward = ResolveForwardDirection();
        Vector2 behind = -forward;
        Vector2 side = new Vector2(-forward.y, forward.x);
        float sideOffset = UnityEngine.Random.Range(-0.22f, 0.22f);
        Vector3 spawnPosition = transform.position + (Vector3)(behind * AutoCannonRearOffset + side * sideOffset);
        float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - 90f;
        GameObject cannonObject = PhotonNetwork.Instantiate(
            "Player",
            spawnPosition,
            Quaternion.Euler(0f, 0f, angle),
            0,
            new object[] { PlayerDeployableRuntime.ContainerShipAutoCannonMarker, photonView.ViewID });

        if (cannonObject != null)
            PlayerDeployableRuntime.EnsureAttached(cannonObject);
    }

    Vector2 ResolveForwardDirection()
    {
        return travelDirection.sqrMagnitude > 0.001f ? travelDirection.normalized : Vector2.right;
    }

    protected override void OnDestroy()
    {
        TractorBeamVfx.StopBeam(photonView != null ? photonView.ViewID : 0);
        base.OnDestroy();
    }

    [PunRPC]
    public new void PlayDeployableHitRpc(bool shieldHit, float x, float y)
    {
        PlayDeployableHitFeedback(shieldHit, x, y);
    }

    [PunRPC]
    public new void PlayDeployableDestroyedRpc()
    {
        PlayDeployableDestroyedFeedback();
    }
}

public sealed class ViperWreckTowTarget : MonoBehaviourPun
{
    const float VisualTargetSize = GameVisualTheme.PlayerTargetSize;
    const float CollisionRadius = 0.46f;
    const float EvacuationDuration = 2.8f;

    static Sprite cachedViperWreckSprite;

    bool initialized;
    bool detached;
    bool evacuating;
    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    MovingSpaceObject movingObject;

    public bool CanBeExtracted => initialized && detached && !evacuating;

    public void InitializeFromPhotonData()
    {
        if (initialized)
            return;

        initialized = true;
        gameObject.name = "ViperRecoveryWreck";
        ConfigureVisuals();
        ConfigurePhysics(false);
        DisablePlayerSpecificSystems();
        ActorIdentity.Ensure(gameObject);
        ViperRecoveryPlotController.ClearLocalTagIfViperWreck(gameObject);
        RuntimeSceneQueryCache.InvalidateAll();
    }

    void Awake()
    {
        if (IsViperWreckData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
    }

    void Start()
    {
        if (IsViperWreckData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
    }

    void Update()
    {
        if (!initialized || evacuating)
            return;

        RefreshVisualIfOverwritten();

        bool shouldDetach = IsHaulerDestroyed();
        if (shouldDetach && !detached)
            Detach();

        if (!detached)
            FollowHauler();
    }

    [PunRPC]
    public void BeginWreckEvacuationRpc(float targetX, float targetY)
    {
        if (evacuating)
            return;

        StartCoroutine(EvacuationRoutine(new Vector2(targetX, targetY)));
    }

    void FollowHauler()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!TryGetMissionState(out int haulerViewId, out Vector2 direction, out bool haulerDestroyed) || haulerDestroyed)
            return;

        PhotonView haulerView = PhotonView.Find(haulerViewId);
        if (haulerView == null)
            return;

        Vector2 targetPosition = (Vector2)haulerView.transform.position - direction.normalized * ViperRecoveryPlotController.TowedWreckDistance;
        transform.position = targetPosition;
        transform.rotation = Quaternion.LookRotation(Vector3.forward, direction);
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    void Detach()
    {
        detached = true;
        ConfigurePhysics(true);
        if (movingObject == null)
            movingObject = gameObject.GetComponent<MovingSpaceObject>();
        if (movingObject == null)
            movingObject = gameObject.AddComponent<MovingSpaceObject>();

        int stableNumber = photonView != null && photonView.ViewID > 0 ? photonView.ViewID : (gameObject.name.GetHashCode() & int.MaxValue);
        movingObject.Configure("viper_recovery_wreck_" + stableNumber, MovingSpaceObject.SpaceObjectType.Treasure);
        movingObject.ForceBroadcastSnapshot();
    }

    bool IsHaulerDestroyed()
    {
        return TryGetMissionState(out _, out _, out bool haulerDestroyed) && haulerDestroyed;
    }

    bool TryGetMissionState(out int haulerViewId, out Vector2 direction, out bool haulerDestroyed)
    {
        haulerViewId = 0;
        direction = Vector2.right;
        haulerDestroyed = false;
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return false;

        ExitGames.Client.Photon.Hashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        haulerViewId = props.TryGetValue("viperPlot.runtime.haulerViewId", out object haulerValue) ? ConvertToInt(haulerValue, 0) : 0;
        float dirX = props.TryGetValue("viperPlot.runtime.spawnDirX", out object dirXValue) ? ConvertToFloat(dirXValue, 1f) : 1f;
        float dirY = props.TryGetValue("viperPlot.runtime.spawnDirY", out object dirYValue) ? ConvertToFloat(dirYValue, 0f) : 0f;
        direction = new Vector2(dirX, dirY);
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.right;
        direction.Normalize();
        haulerDestroyed = props.TryGetValue("viperPlot.runtime.haulerDestroyed", out object destroyedValue) && destroyedValue is bool destroyed && destroyed;
        return haulerViewId > 0;
    }

    void ConfigureVisuals()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        Sprite sprite = LoadViperWreckSprite();
        if (sprite != null)
        {
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = Color.white;
            RuntimeSpriteUtility.FitRenderer(spriteRenderer, VisualTargetSize);
        }

        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = GameVisualTheme.PlayerSortingOrder - 2;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i] != spriteRenderer)
                renderers[i].enabled = false;
        }
    }

    void RefreshVisualIfOverwritten()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        Sprite sprite = LoadViperWreckSprite();
        if (spriteRenderer == null || sprite == null || spriteRenderer.sprite == sprite)
            return;

        ConfigureVisuals();
    }

    static Sprite LoadViperWreckSprite()
    {
        if (cachedViperWreckSprite != null)
            return cachedViperWreckSprite;

        cachedViperWreckSprite = RuntimeSpriteUtility.LoadSprite(
            ShipCatalog.GetWreckResourcePathForSkin(ShipCatalog.ViperStandardSkinIndex),
            ShipCatalog.GetWreckEditorResourcePathForSkin(ShipCatalog.ViperStandardSkinIndex));
        return cachedViperWreckSprite;
    }

    void ConfigurePhysics(bool dynamicBody)
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody2D>();

        body.gravityScale = 0f;
        body.bodyType = dynamicBody ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
        body.simulated = true;
        body.mass = 5f;
        body.linearDamping = dynamicBody ? 0.55f : 0f;
        body.angularDamping = dynamicBody ? 0.8f : 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null)
            circle = gameObject.AddComponent<CircleCollider2D>();
        circle.isTrigger = false;
        float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), 0.001f);
        circle.radius = CollisionRadius / scale;
        circle.offset = Vector2.zero;

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
            box.enabled = false;
    }

    System.Collections.IEnumerator EvacuationRoutine(Vector2 target)
    {
        evacuating = true;
        if (movingObject != null)
            movingObject.enabled = false;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = new Vector3(target.x, target.y, startPosition.z);
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < EvacuationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / EvacuationDuration);
            float eased = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(startPosition, targetPosition, eased);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
            yield return null;
        }

        gameObject.SetActive(false);
    }

    void DisablePlayerSpecificSystems()
    {
        DisableComponent<PlayerMovement>();
        DisableComponent<PlayerShooting>();
        DisableComponent<TreasureCollector>();
        DisableComponent<PlayerRepairDocking>();
        DisableComponent<PlayerHealth>();
        DisableComponent<HealthBarUI>();
        DisableComponent<ShieldBarUI>();
        DisableComponent<PlayerNicknameUI>();
        DisableComponent<BoosterBarUI>();
        DisableComponent<ShipInventoryHudUI>();
        DisableComponent<StartingShipEntryVfx>();
        DisableComponent<SpawnInvulnerabilityVfx>();
        DisableComponent<AstronautSurvivor>();

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            Destroy(thruster);
    }

    void DisableComponent<T>() where T : Behaviour
    {
        T component = GetComponent<T>();
        if (component != null && component != this)
            component.enabled = false;
    }

    static bool IsViperWreckData(object[] data)
    {
        return ViperRecoveryPlotController.IsViperWreckInstantiationData(data);
    }

    static int ConvertToInt(object value, int fallback)
    {
        if (value is int intValue)
            return intValue;
        if (value is float floatValue)
            return Mathf.RoundToInt(floatValue);
        if (value is double doubleValue)
            return Mathf.RoundToInt((float)doubleValue);
        return fallback;
    }

    static float ConvertToFloat(object value, float fallback)
    {
        if (value is float floatValue)
            return floatValue;
        if (value is int intValue)
            return intValue;
        if (value is double doubleValue)
            return (float)doubleValue;
        return fallback;
    }
}
