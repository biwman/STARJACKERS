using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

public enum InvaderPlotUseAction
{
    None,
    Contact,
    Stabilize,
    Sync
}

public sealed class InvaderInvasionPlotController : MonoBehaviour
{
    const float ScanInterval = 0.35f;
    const float PlotMatchTimeTolerance = 0.001f;
    const float UseRange = 2.45f;
    const float ContactSeconds = 5f;
    const float StabilizeSeconds = 5f;
    const float SyncSeconds = 8f;
    const float AssimilationRequiredSeconds = 45f;
    const float AssimilationFieldRadius = 5.4f;
    const string PersistentHintOwnerKey = "invader-invasion-plot";
    const string EchoGuardMarker = "invader_plot_echo_guard";

    const string PlotStartTimeKey = "invaderPlot.runtime.startTime";
    const string StageKey = "invaderPlot.runtime.stage";
    const string TargetActorKey = "invaderPlot.runtime.targetActor";
    const string ObjectiveDoneKey = "invaderPlot.runtime.objectiveDone";
    const string ObjectiveActorKey = "invaderPlot.runtime.objectiveActor";
    const string ObjectiveConsumedKey = "invaderPlot.runtime.objectiveConsumed";
    const string DangerPhaseKey = "invaderPlot.runtime.dangerPhase";
    const string RiftXKey = "invaderPlot.runtime.riftX";
    const string RiftYKey = "invaderPlot.runtime.riftY";
    const string Node1XKey = "invaderPlot.runtime.node1X";
    const string Node1YKey = "invaderPlot.runtime.node1Y";
    const string Node2XKey = "invaderPlot.runtime.node2X";
    const string Node2YKey = "invaderPlot.runtime.node2Y";
    const string Node1DoneKey = "invaderPlot.runtime.node1Done";
    const string Node2DoneKey = "invaderPlot.runtime.node2Done";
    const string FieldXKey = "invaderPlot.runtime.fieldX";
    const string FieldYKey = "invaderPlot.runtime.fieldY";
    const string FieldSecondsKey = "invaderPlot.runtime.fieldSeconds";
    const string ShellXKey = "invaderPlot.runtime.shellX";
    const string ShellYKey = "invaderPlot.runtime.shellY";

    static InvaderInvasionPlotController instance;

    readonly List<GameObject> visualObjects = new List<GameObject>();
    double handledStartTime = double.MinValue;
    double localAnnouncementStartTime = double.MinValue;
    int localAnnouncementStage;
    double visualStartTime = double.MinValue;
    int visualStage;
    float nextScanTime;
    float localAssimilationSeconds;
    float nextAssimilationSyncTime;
    Coroutine localUseRoutine;
    int localUsePlayerViewId;
    InvaderPlotUseAction localUseAction;
    int localUseNodeIndex;
    float localUseProgress;

    struct PlotState
    {
        public double StartTime;
        public int Stage;
        public int TargetActorNumber;
        public bool ObjectiveDone;
        public int ObjectiveActorNumber;
        public bool ObjectiveConsumed;
        public int DangerPhase;
        public Vector2 RiftPosition;
        public Vector2 Node1Position;
        public Vector2 Node2Position;
        public bool Node1Done;
        public bool Node2Done;
        public Vector2 FieldPosition;
        public float FieldSeconds;
        public Vector2 ShellPosition;
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

        GameObject root = new GameObject("InvaderInvasionPlotController");
        instance = root.AddComponent<InvaderInvasionPlotController>();
        DontDestroyOnLoad(root);
    }

    public static bool TryGetUseAction(PlayerHealth player, out InvaderPlotUseAction action)
    {
        action = InvaderPlotUseAction.None;
        if (instance == null)
            return false;

        return instance.TryGetUseActionInternal(player, out action, out _, out _);
    }

    public static bool TryStartUse(PlayerHealth player)
    {
        if (instance == null)
            return false;

        return instance.TryStartUseInternal(player);
    }

    public static bool TryGetUseChargeProgress(PlayerHealth player, out float progress, out InvaderPlotUseAction action)
    {
        progress = 0f;
        action = InvaderPlotUseAction.None;
        if (instance == null || player == null || player.photonView == null)
            return false;

        if (instance.localUseRoutine == null || instance.localUsePlayerViewId != player.photonView.ViewID)
            return false;

        progress = Mathf.Clamp01(instance.localUseProgress);
        action = instance.localUseAction;
        return action != InvaderPlotUseAction.None;
    }

    public static bool IsUseChargeInProgress(PlayerHealth player)
    {
        return TryGetUseChargeProgress(player, out _, out _);
    }

    public static void NotifyUseCanceledByShot(PlayerHealth player)
    {
        if (instance == null || player == null || player.photonView == null)
            return;

        if (instance.localUseRoutine != null &&
            instance.localUsePlayerViewId == player.photonView.ViewID &&
            instance.localUseAction == InvaderPlotUseAction.Stabilize)
        {
            instance.CancelLocalUse(true);
        }
    }

    public static bool TryCompleteStageOnEvacuation(PlayerHealth player, out int completedStage)
    {
        completedStage = 0;
        if (instance == null || player == null || player.photonView == null)
            return false;

        return instance.TryCompleteStageOnEvacuationInternal(player, out completedStage);
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
            ResetRoundState();
            return;
        }

        float now = Time.unscaledTime;
        if (now < nextScanTime)
        {
            TickLocalContinuousStage();
            return;
        }

        nextScanTime = now + ScanInterval;
        TickLifecycle();
        TickLocalContinuousStage();
    }

    void TickLifecycle()
    {
        if (!IsRoundStarted(out double currentStartTime))
        {
            ResetRoundState();
            return;
        }

        if (currentStartTime != handledStartTime)
        {
            handledStartTime = currentStartTime;
            localAnnouncementStartTime = double.MinValue;
            localAnnouncementStage = 0;
            localAssimilationSeconds = 0f;
            CancelLocalUse(false);
            ClearLocalVisuals();
            RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
        }

        if (!ShipUnlockPlotCoordinator.IsActivePlot(ShipUnlockPlotType.Invader))
        {
            ClearLocalVisuals();
            RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
            CancelLocalUse(false);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
            EnsureMissionState(currentStartTime);

        if (!TryReadState(out PlotState state))
            return;

        EnsureLocalVisuals(state);
        ShowStageAnnouncement(state);
        UpdateLocalHint(state);

        if (PhotonNetwork.IsMasterClient)
            TickMasterDanger(state);
    }

    void EnsureMissionState(double currentStartTime)
    {
        if (TryReadState(out PlotState existing) &&
            Mathf.Abs((float)(existing.StartTime - currentStartTime)) <= PlotMatchTimeTolerance &&
            existing.Stage > 0)
        {
            return;
        }

        if (!TryResolveTargetPlayer(out Photon.Realtime.Player targetPlayer, out int nextStage))
            return;

        Vector2 anchor = ResolveObjectiveSpawnPosition();
        Vector2 nodeOffset = UnityEngine.Random.insideUnitCircle.normalized;
        if (nodeOffset.sqrMagnitude < 0.001f)
            nodeOffset = Vector2.right;

        Vector2 nodePerp = new Vector2(-nodeOffset.y, nodeOffset.x);
        Vector2 node1 = ClampToMapBounds(anchor + nodeOffset * 4.2f + nodePerp * 1.6f);
        Vector2 node2 = ClampToMapBounds(anchor - nodeOffset * 4.2f - nodePerp * 1.6f);

        PhotonHashtable props = new PhotonHashtable
        {
            [PlotStartTimeKey] = currentStartTime,
            [StageKey] = nextStage,
            [TargetActorKey] = targetPlayer.ActorNumber,
            [ObjectiveDoneKey] = false,
            [ObjectiveActorKey] = 0,
            [ObjectiveConsumedKey] = false,
            [DangerPhaseKey] = 0,
            [RiftXKey] = anchor.x,
            [RiftYKey] = anchor.y,
            [Node1XKey] = node1.x,
            [Node1YKey] = node1.y,
            [Node2XKey] = node2.x,
            [Node2YKey] = node2.y,
            [Node1DoneKey] = false,
            [Node2DoneKey] = false,
            [FieldXKey] = anchor.x,
            [FieldYKey] = anchor.y,
            [FieldSecondsKey] = 0f,
            [ShellXKey] = anchor.x,
            [ShellYKey] = anchor.y
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    bool TryResolveTargetPlayer(out Photon.Realtime.Player targetPlayer, out int nextStage)
    {
        targetPlayer = null;
        nextStage = 0;

        if (!ShipUnlockPlotCoordinator.TryGetRoundStarterPlayer(out targetPlayer) ||
            !PlayerProfileService.PlayerNeedsInvaderImprints(targetPlayer))
        {
            return false;
        }

        int bestProgress = Mathf.Clamp(
            PlayerProfileService.GetPlayerInvaderImprintsRecovered(targetPlayer),
            0,
            PlayerProfileService.InvaderImprintsRequired);
        nextStage = Mathf.Clamp(bestProgress + 1, 1, PlayerProfileService.InvaderImprintsRequired);
        return true;
    }

    bool TryGetUseActionInternal(PlayerHealth player, out InvaderPlotUseAction action, out int nodeIndex, out float requiredSeconds)
    {
        action = InvaderPlotUseAction.None;
        nodeIndex = 0;
        requiredSeconds = 0f;

        if (!IsPlayerEligibleForLocalInteraction(player))
            return false;

        if (!TryReadState(out PlotState state))
            return false;

        if (!IsTargetPlayer(player, state))
            return false;

        Vector2 playerPosition = player.transform.position;
        if (state.Stage == 1 && !state.ObjectiveDone && IsWithinUseRange(playerPosition, state.RiftPosition))
        {
            action = InvaderPlotUseAction.Contact;
            requiredSeconds = ContactSeconds;
            return true;
        }

        if (state.Stage == 2 && !state.ObjectiveDone)
        {
            if (!state.Node1Done && IsWithinUseRange(playerPosition, state.Node1Position))
            {
                action = InvaderPlotUseAction.Stabilize;
                nodeIndex = 1;
                requiredSeconds = StabilizeSeconds;
                return true;
            }

            if (!state.Node2Done && IsWithinUseRange(playerPosition, state.Node2Position))
            {
                action = InvaderPlotUseAction.Stabilize;
                nodeIndex = 2;
                requiredSeconds = StabilizeSeconds;
                return true;
            }
        }

        if (state.Stage == 4 && !state.ObjectiveDone && IsWithinUseRange(playerPosition, state.ShellPosition))
        {
            action = InvaderPlotUseAction.Sync;
            requiredSeconds = SyncSeconds;
            return true;
        }

        return false;
    }

    bool TryStartUseInternal(PlayerHealth player)
    {
        if (!TryGetUseActionInternal(player, out InvaderPlotUseAction action, out int nodeIndex, out float requiredSeconds))
            return false;

        CancelLocalUse(false);
        localUseRoutine = StartCoroutine(LocalUseRoutine(player, action, nodeIndex, requiredSeconds));
        return true;
    }

    IEnumerator LocalUseRoutine(PlayerHealth player, InvaderPlotUseAction action, int nodeIndex, float requiredSeconds)
    {
        localUsePlayerViewId = player != null && player.photonView != null ? player.photonView.ViewID : 0;
        localUseAction = action;
        localUseNodeIndex = nodeIndex;
        localUseProgress = 0f;

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, requiredSeconds);
        while (elapsed < duration)
        {
            if (!TryGetUseActionInternal(player, out InvaderPlotUseAction currentAction, out int currentNodeIndex, out _ ) ||
                currentAction != action ||
                currentNodeIndex != nodeIndex)
            {
                CancelLocalUse(true);
                yield break;
            }

            elapsed += Time.deltaTime;
            localUseProgress = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        CompleteLocalUseObjective(player, action, nodeIndex);
        CancelLocalUse(false);
    }

    void CancelLocalUse(bool showInterrupted)
    {
        if (localUseRoutine != null)
            StopCoroutine(localUseRoutine);

        localUseRoutine = null;
        localUsePlayerViewId = 0;
        localUseAction = InvaderPlotUseAction.None;
        localUseNodeIndex = 0;
        localUseProgress = 0f;

        if (showInterrupted)
            RoundAnnouncementUI.Show("Alien resonance interrupted.", 2.2f);
    }

    void CompleteLocalUseObjective(PlayerHealth player, InvaderPlotUseAction action, int nodeIndex)
    {
        if (player == null || player.photonView == null || PhotonNetwork.CurrentRoom == null)
            return;

        if (!TryReadState(out PlotState state) || !IsTargetPlayer(player, state))
            return;

        int actorNumber = player.photonView.Owner != null ? player.photonView.Owner.ActorNumber : 0;
        PhotonHashtable props = new PhotonHashtable();

        if (state.Stage == 1 && action == InvaderPlotUseAction.Contact)
        {
            props[ObjectiveDoneKey] = true;
            props[ObjectiveActorKey] = actorNumber;
            RoundAnnouncementUI.Show("Alien imprint stabilized. Escape to preserve it.", 3.2f);
        }
        else if (state.Stage == 2 && action == InvaderPlotUseAction.Stabilize)
        {
            bool node1Done = state.Node1Done || nodeIndex == 1;
            bool node2Done = state.Node2Done || nodeIndex == 2;
            props[Node1DoneKey] = node1Done;
            props[Node2DoneKey] = node2Done;
            if (node1Done && node2Done)
            {
                props[ObjectiveDoneKey] = true;
                props[ObjectiveActorKey] = actorNumber;
                RoundAnnouncementUI.Show("Resonance stabilized. Escape to preserve it.", 3.2f);
            }
            else
            {
                RoundAnnouncementUI.Show("Resonance node stabilized.", 2.4f);
            }
        }
        else if (state.Stage == 4 && action == InvaderPlotUseAction.Sync)
        {
            props[ObjectiveDoneKey] = true;
            props[ObjectiveActorKey] = actorNumber;
            RoundAnnouncementUI.Show("Invader shell synchronized. Escape to preserve it.", 3.2f);
        }

        if (props.Count > 0)
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    void TickLocalContinuousStage()
    {
        if (!ShipUnlockPlotCoordinator.IsActivePlot(ShipUnlockPlotType.Invader))
            return;

        if (!TryReadState(out PlotState state) || state.Stage != 3 || state.ObjectiveDone)
            return;

        PlayerHealth localPlayer = GetLocalRoundPlayer();
        if (!IsPlayerEligibleForLocalInteraction(localPlayer) || !IsTargetPlayer(localPlayer, state))
            return;

        localAssimilationSeconds = Mathf.Max(localAssimilationSeconds, state.FieldSeconds);
        float distance = Vector2.Distance(localPlayer.transform.position, state.FieldPosition);
        if (distance > AssimilationFieldRadius)
            return;

        localAssimilationSeconds = Mathf.Clamp(
            localAssimilationSeconds + Time.deltaTime,
            0f,
            AssimilationRequiredSeconds);

        if (Time.time >= nextAssimilationSyncTime)
        {
            nextAssimilationSyncTime = Time.time + 0.55f;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                [FieldSecondsKey] = localAssimilationSeconds
            });
        }

        if (localAssimilationSeconds >= AssimilationRequiredSeconds)
        {
            int actorNumber = localPlayer.photonView.Owner != null ? localPlayer.photonView.Owner.ActorNumber : 0;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                [FieldSecondsKey] = AssimilationRequiredSeconds,
                [ObjectiveDoneKey] = true,
                [ObjectiveActorKey] = actorNumber
            });
            RoundAnnouncementUI.Show("Assimilation imprint stabilized. Escape to preserve it.", 3.2f);
        }
    }

    bool TryCompleteStageOnEvacuationInternal(PlayerHealth player, out int completedStage)
    {
        completedStage = 0;
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || !TryReadState(out PlotState state))
            return false;

        if (!state.ObjectiveDone || state.ObjectiveConsumed)
            return false;

        int actorNumber = player.photonView.Owner != null ? player.photonView.Owner.ActorNumber : 0;
        if (actorNumber <= 0 || actorNumber != state.ObjectiveActorNumber || actorNumber != state.TargetActorNumber)
            return false;

        completedStage = Mathf.Clamp(state.Stage, 1, PlayerProfileService.InvaderImprintsRequired);
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            [ObjectiveConsumedKey] = true
        });
        return true;
    }

    void TickMasterDanger(PlotState state)
    {
        if (state.DangerPhase >= state.Stage)
            return;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable { [DangerPhaseKey] = state.Stage });
        SpawnStageEchoGuards(state);
    }

    void SpawnStageEchoGuards(PlotState state)
    {
        int targetViewId = ResolveTargetPlayerViewId(state.TargetActorNumber);
        Vector2 center = GetStageCenter(state);
        int count = state.Stage == 1 ? 1 : state.Stage == 2 ? 2 : state.Stage == 3 ? 2 : 3;

        for (int i = 0; i < count; i++)
        {
            EnemyBotKind kind = ResolveEchoGuardKind(state.Stage, i);
            Vector2 spawn = ResolveEchoGuardSpawnPosition(center, state.TargetActorNumber, i);
            SpawnEchoGuard(spawn, kind, targetViewId);
        }
    }

    EnemyBotKind ResolveEchoGuardKind(int stage, int ordinal)
    {
        if (stage >= 4 && ordinal == 1)
            return EnemyBotKind.GravitySquid;

        if (stage >= 3 && ordinal == 0)
            return EnemyBotKind.SpaceManta;

        return ordinal % 2 == 0 ? EnemyBotKind.Drone : EnemyBotKind.SpaceManta;
    }

    Vector2 ResolveEchoGuardSpawnPosition(Vector2 center, int targetActorNumber, int ordinal)
    {
        Vector2 away = UnityEngine.Random.insideUnitCircle;
        PlayerHealth target = FindPlayerByActorNumber(targetActorNumber);
        if (target != null)
            away = center - (Vector2)target.transform.position;

        if (away.sqrMagnitude < 0.001f)
            away = Quaternion.Euler(0f, 0f, ordinal * 137f) * Vector2.right;

        away.Normalize();
        Vector2 side = new Vector2(-away.y, away.x);
        Vector2 offset = away * UnityEngine.Random.Range(5.4f, 8.2f) + side * UnityEngine.Random.Range(-3.2f, 3.2f);
        return ClampToMapBounds(center + offset);
    }

    void SpawnEchoGuard(Vector2 position, EnemyBotKind kind, int targetViewId)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        GameObject enemyObject = PhotonNetwork.Instantiate(
            "Player",
            ClampToMapBounds(position),
            Quaternion.identity,
            0,
            new object[] { definition.InstantiationMarker, EchoGuardMarker });
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

    void EnsureLocalVisuals(PlotState state)
    {
        if (visualStartTime == state.StartTime && visualStage == state.Stage)
            return;

        ClearLocalVisuals();
        visualStartTime = state.StartTime;
        visualStage = state.Stage;

        switch (state.Stage)
        {
            case 1:
                AddSpriteVisual("AlienSignalRift", "Visuals/Invader/alien_signal_rift", "Assets/Resources/Visuals/Invader/alien_signal_rift.png", state.RiftPosition, 3.8f, new Color(0.76f, 1f, 0.9f, 0.95f), 22, true);
                AddEchoVisuals(state.RiftPosition, 2);
                break;
            case 2:
                AddSpriteVisual("ResonanceNodeA", "Visuals/Invader/resonance_node", "Assets/Resources/Visuals/Invader/resonance_node.png", state.Node1Position, 1.9f, state.Node1Done ? new Color(0.55f, 1f, 0.74f, 0.62f) : Color.white, 22, !state.Node1Done);
                AddSpriteVisual("ResonanceNodeB", "Visuals/Invader/resonance_node", "Assets/Resources/Visuals/Invader/resonance_node.png", state.Node2Position, 1.9f, state.Node2Done ? new Color(0.55f, 1f, 0.74f, 0.62f) : Color.white, 22, !state.Node2Done);
                AddEchoVisuals((state.Node1Position + state.Node2Position) * 0.5f, 3);
                break;
            case 3:
                AddSpriteVisual("AssimilationField", "Visuals/Invader/assimilation_field", "Assets/Resources/Visuals/Invader/assimilation_field.png", state.FieldPosition, AssimilationFieldRadius * 2f, new Color(0.68f, 1f, 0.84f, 0.46f), 18, true);
                AddEchoVisuals(state.FieldPosition, 4);
                break;
            case 4:
                AddSpriteVisual("InvaderShell", "Visuals/Invader/invader_shell", "Assets/Resources/Visuals/Invader/invader_shell.png", state.ShellPosition, 2.6f, Color.white, 22, true);
                AddEchoVisuals(state.ShellPosition, 5);
                break;
        }
    }

    void AddEchoVisuals(Vector2 center, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / Mathf.Max(1, count)) * i + UnityEngine.Random.Range(-18f, 18f);
            Vector2 offset = Quaternion.Euler(0f, 0f, angle) * Vector2.right * UnityEngine.Random.Range(1.8f, 3.4f);
            AddSpriteVisual("AlienEchoVisual", "Visuals/Invader/alien_echo", "Assets/Resources/Visuals/Invader/alien_echo.png", center + offset, 1.1f, new Color(0.74f, 1f, 0.92f, 0.52f), 21, true);
        }
    }

    void AddSpriteVisual(string name, string resourcePath, string editorPath, Vector2 position, float worldSize, Color color, int sortingOrder, bool pulse)
    {
        GameObject visual = new GameObject(name);
        visual.transform.position = new Vector3(position.x, position.y, -0.08f);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeSpriteUtility.LoadSprite(resourcePath, editorPath);
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.color = color;
        RuntimeSpriteUtility.FitRenderer(renderer, worldSize);

        if (pulse)
            visual.AddComponent<InvaderObjectivePulse>();

        visualObjects.Add(visual);
    }

    void ShowStageAnnouncement(PlotState state)
    {
        if (localAnnouncementStartTime == state.StartTime && localAnnouncementStage == state.Stage)
            return;

        localAnnouncementStartTime = state.StartTime;
        localAnnouncementStage = state.Stage;
        RoundAnnouncementUI.Show(GetStageAnnouncement(state.Stage), 4.5f);
    }

    string GetStageAnnouncement(int stage)
    {
        switch (stage)
        {
            case 1: return "Unknown alien signal detected in local space.";
            case 2: return "Resonance nodes are phasing into this sector.";
            case 3: return "Assimilation field is active. Survive the exposure.";
            case 4: return "Dormant Invader shell detected. Synchronize before escape.";
            default: return string.Empty;
        }
    }

    void UpdateLocalHint(PlotState state)
    {
        PlayerHealth localPlayer = GetLocalRoundPlayer();
        if (localPlayer == null || !IsTargetPlayer(localPlayer, state) || state.ObjectiveConsumed)
        {
            RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
            return;
        }

        if (state.ObjectiveDone)
        {
            RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, "Escape through Extraction Zone to secure alien imprint");
            return;
        }

        switch (state.Stage)
        {
            case 1:
                RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, "CONTACT the alien rift, then escape through Extraction Zone");
                break;
            case 2:
                RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, "STABILIZE both resonance nodes without shooting, then escape");
                break;
            case 3:
                int seconds = Mathf.FloorToInt(Mathf.Max(localAssimilationSeconds, state.FieldSeconds));
                RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, "Stay inside Assimilation Field: " + seconds + "/" + Mathf.RoundToInt(AssimilationRequiredSeconds) + "s");
                break;
            case 4:
                RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, "SYNC with the Invader shell, then escape through Extraction Zone");
                break;
            default:
                RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
                break;
        }
    }

    Vector2 GetStageCenter(PlotState state)
    {
        switch (state.Stage)
        {
            case 1: return state.RiftPosition;
            case 2: return (state.Node1Position + state.Node2Position) * 0.5f;
            case 3: return state.FieldPosition;
            case 4: return state.ShellPosition;
            default: return Vector2.zero;
        }
    }

    Vector2 ResolveObjectiveSpawnPosition()
    {
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = Mathf.Max(8f, mapSize.x * 0.5f - 8f);
        float halfY = Mathf.Max(8f, mapSize.y * 0.5f - 8f);
        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);

        for (int attempt = 0; attempt < 64; attempt++)
        {
            Vector2 candidate = new Vector2(
                UnityEngine.Random.Range(-halfX, halfX),
                UnityEngine.Random.Range(-halfY, halfY));

            if (IsFarEnoughFromExtractionZones(candidate, zones, 12f))
                return candidate;
        }

        return new Vector2(halfX * 0.22f, -halfY * 0.18f);
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

    bool TryReadState(out PlotState state)
    {
        state = default;
        if (!IsRoundStarted(out double currentStartTime) || PhotonNetwork.CurrentRoom == null)
            return false;

        PhotonHashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (!props.TryGetValue(PlotStartTimeKey, out object startValue) ||
            !TryConvertToDouble(startValue, out double storedStartTime) ||
            Mathf.Abs((float)(storedStartTime - currentStartTime)) > PlotMatchTimeTolerance)
        {
            return false;
        }

        state = new PlotState
        {
            StartTime = storedStartTime,
            Stage = props.TryGetValue(StageKey, out object stageValue) && TryConvertToInt(stageValue, out int stage) ? Mathf.Clamp(stage, 1, PlayerProfileService.InvaderImprintsRequired) : 0,
            TargetActorNumber = props.TryGetValue(TargetActorKey, out object actorValue) && TryConvertToInt(actorValue, out int actorNumber) ? actorNumber : 0,
            ObjectiveDone = props.TryGetValue(ObjectiveDoneKey, out object doneValue) && doneValue is bool done && done,
            ObjectiveActorNumber = props.TryGetValue(ObjectiveActorKey, out object objectiveActorValue) && TryConvertToInt(objectiveActorValue, out int objectiveActor) ? objectiveActor : 0,
            ObjectiveConsumed = props.TryGetValue(ObjectiveConsumedKey, out object consumedValue) && consumedValue is bool consumed && consumed,
            DangerPhase = props.TryGetValue(DangerPhaseKey, out object dangerValue) && TryConvertToInt(dangerValue, out int danger) ? danger : 0,
            RiftPosition = new Vector2(ReadFloat(props, RiftXKey), ReadFloat(props, RiftYKey)),
            Node1Position = new Vector2(ReadFloat(props, Node1XKey), ReadFloat(props, Node1YKey)),
            Node2Position = new Vector2(ReadFloat(props, Node2XKey), ReadFloat(props, Node2YKey)),
            Node1Done = props.TryGetValue(Node1DoneKey, out object node1Value) && node1Value is bool node1Done && node1Done,
            Node2Done = props.TryGetValue(Node2DoneKey, out object node2Value) && node2Value is bool node2Done && node2Done,
            FieldPosition = new Vector2(ReadFloat(props, FieldXKey), ReadFloat(props, FieldYKey)),
            FieldSeconds = props.TryGetValue(FieldSecondsKey, out object fieldValue) && TryConvertToFloat(fieldValue, out float fieldSeconds) ? Mathf.Clamp(fieldSeconds, 0f, AssimilationRequiredSeconds) : 0f,
            ShellPosition = new Vector2(ReadFloat(props, ShellXKey), ReadFloat(props, ShellYKey))
        };

        return state.Stage > 0 && state.TargetActorNumber > 0;
    }

    bool IsPlayerEligibleForLocalInteraction(PlayerHealth player)
    {
        return player != null &&
               player.photonView != null &&
               player.photonView.IsMine &&
               ShipUnlockPlotCoordinator.IsRoundStarter(player.photonView.Owner) &&
               !player.IsAstronautControlled &&
               !player.IsWreck &&
               !player.IsEvacuationAnimating &&
               ShipUnlockPlotCoordinator.IsActivePlot(ShipUnlockPlotType.Invader);
    }

    bool IsTargetPlayer(PlayerHealth player, PlotState state)
    {
        if (player == null || player.photonView == null || player.photonView.Owner == null)
            return false;

        return player.photonView.Owner.ActorNumber == state.TargetActorNumber;
    }

    bool IsWithinUseRange(Vector2 playerPosition, Vector2 objectivePosition)
    {
        return Vector2.Distance(playerPosition, objectivePosition) <= UseRange;
    }

    PlayerHealth GetLocalRoundPlayer()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player != null && player.photonView != null && player.photonView.IsMine && GameTimer.IsActiveRoundPlayer(player))
                return player;
        }

        return null;
    }

    PlayerHealth FindPlayerByActorNumber(int actorNumber)
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player != null &&
                player.photonView != null &&
                player.photonView.Owner != null &&
                player.photonView.Owner.ActorNumber == actorNumber)
            {
                return player;
            }
        }

        return null;
    }

    int ResolveTargetPlayerViewId(int actorNumber)
    {
        PlayerHealth player = FindPlayerByActorNumber(actorNumber);
        return player != null && player.photonView != null ? player.photonView.ViewID : 0;
    }

    Vector2 ClampToMapBounds(Vector2 position)
    {
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = Mathf.Max(4f, mapSize.x * 0.5f - 3f);
        float halfY = Mathf.Max(4f, mapSize.y * 0.5f - 3f);
        return new Vector2(Mathf.Clamp(position.x, -halfX, halfX), Mathf.Clamp(position.y, -halfY, halfY));
    }

    void ResetRoundState()
    {
        handledStartTime = double.MinValue;
        localAnnouncementStartTime = double.MinValue;
        localAnnouncementStage = 0;
        visualStartTime = double.MinValue;
        visualStage = 0;
        localAssimilationSeconds = 0f;
        CancelLocalUse(false);
        ClearLocalVisuals();
        RoundAnnouncementUI.ClearPersistentHint(PersistentHintOwnerKey);
    }

    void ClearLocalVisuals()
    {
        for (int i = 0; i < visualObjects.Count; i++)
        {
            if (visualObjects[i] != null)
                Destroy(visualObjects[i]);
        }

        visualObjects.Clear();
        visualStartTime = double.MinValue;
        visualStage = 0;
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

    static float ReadFloat(PhotonHashtable props, string key)
    {
        return props != null &&
               props.TryGetValue(key, out object value) &&
               TryConvertToFloat(value, out float result)
            ? result
            : 0f;
    }

    static bool TryConvertToFloat(object value, out float result)
    {
        result = 0f;
        if (value == null)
            return false;

        try
        {
            switch (value)
            {
                case float f:
                    result = f;
                    return true;
                case double d:
                    result = (float)d;
                    return true;
                case int i:
                    result = i;
                    return true;
                case long l:
                    result = l;
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            result = 0f;
            return false;
        }
    }

    static bool TryConvertToInt(object value, out int result)
    {
        result = 0;
        if (value == null)
            return false;

        try
        {
            switch (value)
            {
                case int i:
                    result = i;
                    return true;
                case long l:
                    result = (int)l;
                    return true;
                case float f:
                    result = Mathf.RoundToInt(f);
                    return true;
                case double d:
                    result = Mathf.RoundToInt((float)d);
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            result = 0;
            return false;
        }
    }

    static bool TryConvertToDouble(object value, out double result)
    {
        result = 0d;
        if (value == null)
            return false;

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
                    return false;
            }
        }
        catch
        {
            result = 0d;
            return false;
        }
    }
}

public sealed class InvaderObjectivePulse : MonoBehaviour
{
    SpriteRenderer spriteRenderer;
    Vector3 baseScale;
    Color baseColor;
    float phase;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        baseColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        phase = UnityEngine.Random.Range(0f, 6.28f);
    }

    void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * 2.4f + phase) * 0.065f;
        transform.localScale = baseScale * pulse;

        if (spriteRenderer != null)
        {
            float alpha = Mathf.Clamp01(baseColor.a * (0.82f + Mathf.Sin(Time.time * 3.1f + phase) * 0.18f));
            spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
    }
}
