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
    const float FalseRiftShieldDamageRatio = 0.72f;
    const float RiftClusterRadius = 7.2f;
    const float RiftMinSeparation = 4.6f;
    const float ResonanceRequiredSeconds = 10.5f;
    const float ResonanceNodeMinDistance = 14f;
    const float ResonanceNodeMaxDistance = 24f;
    const float ResonanceBandHalfWidth = 2.35f;
    const float ResonancePushAcceleration = 1.85f;
    const float ResonanceResetDriftDuration = 2.35f;
    const float ResonanceRequiredSpeed = 1.15f;
    const float AssimilationBoosterDrainSeconds = 8f;
    const float AssimilationAmmoDrainSeconds = 9f;
    const float AssimilationShieldDrainPerSecond = 50f / 13f;
    const float AssimilationHpDrainSeconds = 15f;
    const float AssimilationFullHpDamageRatio = 0.7f;
    const float EscortFormFollowDistance = 2.4f;
    const float EscortFormMaxExtractionDistance = 9.5f;
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
    const string Rift2XKey = "invaderPlot.runtime.rift2X";
    const string Rift2YKey = "invaderPlot.runtime.rift2Y";
    const string Rift3XKey = "invaderPlot.runtime.rift3X";
    const string Rift3YKey = "invaderPlot.runtime.rift3Y";
    const string TrueRiftIndexKey = "invaderPlot.runtime.trueRiftIndex";
    const string Rift1ConsumedKey = "invaderPlot.runtime.rift1Consumed";
    const string Rift2ConsumedKey = "invaderPlot.runtime.rift2Consumed";
    const string Rift3ConsumedKey = "invaderPlot.runtime.rift3Consumed";
    const string Node1XKey = "invaderPlot.runtime.node1X";
    const string Node1YKey = "invaderPlot.runtime.node1Y";
    const string Node2XKey = "invaderPlot.runtime.node2X";
    const string Node2YKey = "invaderPlot.runtime.node2Y";
    const string Node1DoneKey = "invaderPlot.runtime.node1Done";
    const string Node2DoneKey = "invaderPlot.runtime.node2Done";
    const string ResonanceSecondsKey = "invaderPlot.runtime.resonanceSeconds";
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
    string visualStateSignature = string.Empty;
    float nextScanTime;
    float localResonanceSeconds;
    float nextResonanceSyncTime;
    bool localWasInResonanceBand;
    float localLastResonanceProjection = -1f;
    float localAssimilationSeconds;
    float nextAssimilationSyncTime;
    float localShieldDrainAccumulator;
    float localHpDrainAccumulator;
    Vector2 localEscortFormPosition;
    bool localEscortFormInitialized;
    float nextEscortSyncTime;
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
        public Vector2 Rift2Position;
        public Vector2 Rift3Position;
        public int TrueRiftIndex;
        public bool Rift1Consumed;
        public bool Rift2Consumed;
        public bool Rift3Consumed;
        public Vector2 Node1Position;
        public Vector2 Node2Position;
        public bool Node1Done;
        public bool Node2Done;
        public float ResonanceSeconds;
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
            localResonanceSeconds = 0f;
            nextResonanceSyncTime = 0f;
            localWasInResonanceBand = false;
            localLastResonanceProjection = -1f;
            localAssimilationSeconds = 0f;
            nextAssimilationSyncTime = 0f;
            localShieldDrainAccumulator = 0f;
            localHpDrainAccumulator = 0f;
            localEscortFormPosition = Vector2.zero;
            localEscortFormInitialized = false;
            nextEscortSyncTime = 0f;
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
        int trueRiftIndex = UnityEngine.Random.Range(1, 4);
        ResolveRiftPositions(anchor, out Vector2 rift1, out Vector2 rift2, out Vector2 rift3);
        ResolveResonanceNodePositions(anchor, out Vector2 node1, out Vector2 node2);

        PhotonHashtable props = new PhotonHashtable
        {
            [PlotStartTimeKey] = currentStartTime,
            [StageKey] = nextStage,
            [TargetActorKey] = targetPlayer.ActorNumber,
            [ObjectiveDoneKey] = false,
            [ObjectiveActorKey] = 0,
            [ObjectiveConsumedKey] = false,
            [DangerPhaseKey] = 0,
            [RiftXKey] = rift1.x,
            [RiftYKey] = rift1.y,
            [Rift2XKey] = rift2.x,
            [Rift2YKey] = rift2.y,
            [Rift3XKey] = rift3.x,
            [Rift3YKey] = rift3.y,
            [TrueRiftIndexKey] = trueRiftIndex,
            [Rift1ConsumedKey] = false,
            [Rift2ConsumedKey] = false,
            [Rift3ConsumedKey] = false,
            [Node1XKey] = node1.x,
            [Node1YKey] = node1.y,
            [Node2XKey] = node2.x,
            [Node2YKey] = node2.y,
            [Node1DoneKey] = false,
            [Node2DoneKey] = false,
            [ResonanceSecondsKey] = 0f,
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
        if (state.Stage == 1 && !state.ObjectiveDone && TryGetUsableRiftIndex(state, playerPosition, out int riftIndex))
        {
            action = InvaderPlotUseAction.Contact;
            nodeIndex = riftIndex;
            requiredSeconds = ContactSeconds;
            return true;
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
            int safeRiftIndex = Mathf.Clamp(nodeIndex, 1, 3);
            if (safeRiftIndex == state.TrueRiftIndex)
            {
                props[ObjectiveDoneKey] = true;
                props[ObjectiveActorKey] = actorNumber;
                RoundAnnouncementUI.Show("Alien imprint stabilized. Escape to preserve it.", 3.2f);
            }
            else
            {
                props[GetRiftConsumedKey(safeRiftIndex)] = true;
                ApplyFalseRiftBurst(player, GetRiftPosition(state, safeRiftIndex));
                RoundAnnouncementUI.Show("FIND THE TRUE RIFT", 2.8f);
            }
        }
        else if (state.Stage == 4 && action == InvaderPlotUseAction.Sync)
        {
            props[ObjectiveDoneKey] = true;
            props[ObjectiveActorKey] = actorNumber;
            props[ShellXKey] = player.transform.position.x;
            props[ShellYKey] = player.transform.position.y;
            localEscortFormPosition = player.transform.position;
            localEscortFormInitialized = true;
            RoundAnnouncementUI.Show("HELP THIS INVADER FORM TO ESCAPE FROM THIS AREA", 3.6f);
        }

        if (props.Count > 0)
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    void TickLocalContinuousStage()
    {
        if (!ShipUnlockPlotCoordinator.IsActivePlot(ShipUnlockPlotType.Invader))
            return;

        if (!TryReadState(out PlotState state))
            return;

        PlayerHealth localPlayer = GetLocalRoundPlayer();
        if (!IsPlayerEligibleForLocalInteraction(localPlayer) || !IsTargetPlayer(localPlayer, state))
            return;

        if (state.Stage == 2)
        {
            TickLocalResonanceBandStage(localPlayer, state);
            return;
        }

        if (state.Stage == 4)
        {
            TickLocalEscortStage(localPlayer, state);
            return;
        }

        if (state.Stage != 3 || state.ObjectiveDone)
            return;

        localAssimilationSeconds = Mathf.Max(localAssimilationSeconds, state.FieldSeconds);
        float distance = Vector2.Distance(localPlayer.transform.position, state.FieldPosition);
        if (distance > AssimilationFieldRadius)
            return;

        InvaderAssimilationSparksVfx.Attach(localPlayer.gameObject);
        ApplyAssimilationDrain(localPlayer, Time.deltaTime);

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

    void TickLocalResonanceBandStage(PlayerHealth localPlayer, PlotState state)
    {
        if (state.ObjectiveDone)
            return;

        localResonanceSeconds = Mathf.Max(localResonanceSeconds, state.ResonanceSeconds);
        Rigidbody2D body = localPlayer.GetComponent<Rigidbody2D>();
        bool inBand = TryGetResonanceBandInfo(
            localPlayer.transform.position,
            state.Node1Position,
            state.Node2Position,
            out float projection,
            out float signedDistance,
            out Vector2 direction,
            out Vector2 perpendicular);

        if (!inBand)
        {
            ResetLocalResonanceProgressIfNeeded(localPlayer, state, perpendicular, signedDistance);
            return;
        }

        float sideSign = Mathf.Abs(signedDistance) > 0.05f ? Mathf.Sign(signedDistance) : Mathf.Sign(Mathf.Sin(Time.time * 3.7f));
        Vector2 outward = perpendicular * (Mathf.Approximately(sideSign, 0f) ? 1f : sideSign);
        if (body != null && body.simulated)
            body.linearVelocity += outward * ResonancePushAcceleration * Time.deltaTime;

        float speedAlongBand = 0f;
        if (body != null)
            speedAlongBand = Mathf.Abs(Vector2.Dot(body.linearVelocity, direction));

        bool movingAlongBand = speedAlongBand >= ResonanceRequiredSpeed ||
                               (localLastResonanceProjection >= 0f && Mathf.Abs(projection - localLastResonanceProjection) >= 0.012f);
        localLastResonanceProjection = projection;
        localWasInResonanceBand = true;

        if (!movingAlongBand)
            return;

        localResonanceSeconds = Mathf.Clamp(localResonanceSeconds + Time.deltaTime, 0f, ResonanceRequiredSeconds);
        if (Time.time >= nextResonanceSyncTime)
        {
            nextResonanceSyncTime = Time.time + 0.35f;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                [ResonanceSecondsKey] = localResonanceSeconds
            });
        }

        if (localResonanceSeconds >= ResonanceRequiredSeconds)
        {
            int actorNumber = localPlayer.photonView.Owner != null ? localPlayer.photonView.Owner.ActorNumber : 0;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                [ResonanceSecondsKey] = ResonanceRequiredSeconds,
                [Node1DoneKey] = true,
                [Node2DoneKey] = true,
                [ObjectiveDoneKey] = true,
                [ObjectiveActorKey] = actorNumber
            });
            RoundAnnouncementUI.Show("Resonance path stabilized. Escape to preserve it.", 3.2f);
        }
    }

    void ResetLocalResonanceProgressIfNeeded(PlayerHealth localPlayer, PlotState state, Vector2 fallbackPerpendicular, float signedDistance)
    {
        if (localResonanceSeconds <= 0.05f && state.ResonanceSeconds <= 0.05f && !localWasInResonanceBand)
            return;

        Vector2 resetDirection = fallbackPerpendicular.sqrMagnitude > 0.001f
            ? fallbackPerpendicular.normalized * (Mathf.Abs(signedDistance) > 0.05f ? Mathf.Sign(signedDistance) : 1f)
            : UnityEngine.Random.insideUnitCircle.normalized;
        if (resetDirection.sqrMagnitude < 0.001f)
            resetDirection = Vector2.right;

        localResonanceSeconds = 0f;
        localWasInResonanceBand = false;
        localLastResonanceProjection = -1f;
        nextResonanceSyncTime = Time.time + 0.35f;

        PlayerMovement movement = localPlayer.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.ApplyInvaderResonanceDrift(ResonanceResetDriftDuration, resetDirection.normalized * 2.8f);

        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            [ResonanceSecondsKey] = 0f
        });
        RoundAnnouncementUI.Show("Resonance path lost.", 1.8f);
    }

    void ApplyAssimilationDrain(PlayerHealth localPlayer, float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        PlayerMovement movement = localPlayer.GetComponent<PlayerMovement>();
        if (movement != null && movement.DrainInvaderAssimilationBooster(deltaTime / AssimilationBoosterDrainSeconds) > 0.0001f)
            return;

        PlayerShooting shooting = localPlayer.GetComponent<PlayerShooting>();
        if (shooting != null)
        {
            float ammoDrainPerSecond = Mathf.Max(1f, shooting.MaxAmmo / AssimilationAmmoDrainSeconds);
            if (shooting.DrainInvaderAssimilationAmmo(ammoDrainPerSecond * deltaTime) > 0)
                return;
        }

        if (localPlayer.CurrentShield > 0)
        {
            localShieldDrainAccumulator += AssimilationShieldDrainPerSecond * deltaTime;
            int shieldDamage = Mathf.FloorToInt(localShieldDrainAccumulator);
            if (shieldDamage > 0)
            {
                localShieldDrainAccumulator -= shieldDamage;
                RequestInvaderDrain(localPlayer, shieldDamage, 0);
            }

            return;
        }

        int minimumHp = Mathf.Max(1, Mathf.RoundToInt(localPlayer.maxHP * 0.02f));
        if (localPlayer.CurrentHP <= minimumHp)
            return;

        float hpDrainPerSecond = Mathf.Max(1f, localPlayer.maxHP * AssimilationFullHpDamageRatio / AssimilationHpDrainSeconds);
        localHpDrainAccumulator += hpDrainPerSecond * deltaTime;
        int hpDamage = Mathf.FloorToInt(localHpDrainAccumulator);
        if (hpDamage > 0)
        {
            localHpDrainAccumulator -= hpDamage;
            RequestInvaderDrain(localPlayer, 0, hpDamage);
        }
    }

    void RequestInvaderDrain(PlayerHealth player, int shieldDamage, int hpDamage)
    {
        if (player == null || player.photonView == null)
            return;

        int minimumHp = Mathf.Max(1, Mathf.RoundToInt(player.maxHP * 0.02f));
        Vector2 impact = player.transform.position;
        player.photonView.RPC(
            nameof(PlayerHealth.ApplyInvaderEnvironmentalDrainRpc),
            RpcTarget.MasterClient,
            Mathf.Max(0, shieldDamage),
            Mathf.Max(0, hpDamage),
            minimumHp,
            impact.x,
            impact.y);
    }

    void TickLocalEscortStage(PlayerHealth localPlayer, PlotState state)
    {
        if (!state.ObjectiveDone || state.ObjectiveConsumed)
        {
            localEscortFormInitialized = false;
            return;
        }

        Rigidbody2D body = localPlayer.GetComponent<Rigidbody2D>();
        Vector2 playerPosition = localPlayer.transform.position;
        Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
        Vector2 behind = velocity.sqrMagnitude > 0.05f ? -velocity.normalized : -(Vector2)localPlayer.transform.up;
        if (behind.sqrMagnitude < 0.001f)
            behind = Vector2.down;

        Vector2 desired = playerPosition + behind.normalized * EscortFormFollowDistance;
        if (!localEscortFormInitialized)
        {
            localEscortFormPosition = state.ShellPosition.sqrMagnitude > 0.001f ? state.ShellPosition : desired;
            localEscortFormInitialized = true;
        }

        localEscortFormPosition = Vector2.Lerp(localEscortFormPosition, desired, 1f - Mathf.Exp(-Time.deltaTime * 4.2f));
        if (Time.time >= nextEscortSyncTime)
        {
            nextEscortSyncTime = Time.time + 0.28f;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                [ShellXKey] = localEscortFormPosition.x,
                [ShellYKey] = localEscortFormPosition.y
            });
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

        if (!IsStage4EscortCloseEnough(player, state))
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
        string signature = BuildVisualStateSignature(state);
        if (visualStartTime == state.StartTime && visualStage == state.Stage && string.Equals(visualStateSignature, signature, StringComparison.Ordinal))
            return;

        ClearLocalVisuals();
        visualStartTime = state.StartTime;
        visualStage = state.Stage;
        visualStateSignature = signature;

        switch (state.Stage)
        {
            case 1:
                AddRiftVisuals(state);
                break;
            case 2:
                AddResonanceBandVisual(state.Node1Position, state.Node2Position);
                AddSpriteVisual("ResonanceNodeA", "Visuals/Invader/resonance_node", "Assets/Resources/Visuals/Invader/resonance_node.png", state.Node1Position, 1.9f, state.Node1Done ? new Color(0.55f, 1f, 0.74f, 0.62f) : Color.white, 22, !state.Node1Done);
                AddSpriteVisual("ResonanceNodeB", "Visuals/Invader/resonance_node", "Assets/Resources/Visuals/Invader/resonance_node.png", state.Node2Position, 1.9f, state.Node2Done ? new Color(0.55f, 1f, 0.74f, 0.62f) : Color.white, 22, !state.Node2Done);
                AddEchoVisuals((state.Node1Position + state.Node2Position) * 0.5f, 3);
                break;
            case 3:
                AddSpriteVisual("AssimilationField", "Visuals/Invader/assimilation_field", "Assets/Resources/Visuals/Invader/assimilation_field.png", state.FieldPosition, AssimilationFieldRadius * 2f, new Color(0.68f, 1f, 0.84f, 0.46f), 18, true);
                AddAssimilationFieldVisual(state.FieldPosition);
                AddEchoVisuals(state.FieldPosition, 4);
                break;
            case 4:
                if (state.ObjectiveDone)
                {
                    AddInvaderEscortVisual(state);
                }
                else
                {
                    AddSpriteVisual("InvaderShell", "Visuals/Invader/invader_shell", "Assets/Resources/Visuals/Invader/invader_shell.png", state.ShellPosition, 2.6f, Color.white, 22, true);
                    AddEchoVisuals(state.ShellPosition, 5);
                }
                break;
        }
    }

    string BuildVisualStateSignature(PlotState state)
    {
        return state.Stage + ":" +
               state.ObjectiveDone + ":" +
               state.ObjectiveConsumed + ":" +
               state.Rift1Consumed + ":" +
               state.Rift2Consumed + ":" +
               state.Rift3Consumed;
    }

    void AddRiftVisuals(PlotState state)
    {
        for (int i = 1; i <= 3; i++)
        {
            if (IsRiftConsumed(state, i))
                continue;

            Vector2 position = GetRiftPosition(state, i);
            AddSpriteVisual("AlienSignalRift" + i, "Visuals/Invader/alien_signal_rift", "Assets/Resources/Visuals/Invader/alien_signal_rift.png", position, 3.8f, new Color(0.76f, 1f, 0.9f, 0.95f), 22, true);
            AddEchoVisuals(position, 1);
        }
    }

    void AddResonanceBandVisual(Vector2 node1, Vector2 node2)
    {
        GameObject visual = new GameObject("InvaderResonanceBandVisual");
        InvaderResonanceBandVisual band = visual.AddComponent<InvaderResonanceBandVisual>();
        band.Configure(node1, node2, ResonanceBandHalfWidth);
        visualObjects.Add(visual);
    }

    void AddAssimilationFieldVisual(Vector2 center)
    {
        GameObject visual = new GameObject("InvaderAssimilationFieldVisual");
        InvaderAssimilationFieldVisual field = visual.AddComponent<InvaderAssimilationFieldVisual>();
        field.Configure(center, AssimilationFieldRadius);
        visualObjects.Add(visual);
    }

    void AddInvaderEscortVisual(PlotState state)
    {
        GameObject visual = new GameObject("InvaderEscortFormVisual");
        InvaderEscortFormVisual escort = visual.AddComponent<InvaderEscortFormVisual>();
        escort.Configure(state.TargetActorNumber, state.ShellPosition);
        visualObjects.Add(visual);
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
            case 2: return "Resonance corridor is phasing between alien nodes.";
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
            if (state.Stage == 4)
                RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, "HELP THIS INVADER FORM TO ESCAPE FROM THIS AREA");
            else
                RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, "Escape through Extraction Zone to secure alien imprint");
            return;
        }

        switch (state.Stage)
        {
            case 1:
                RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, "CONTACT the true alien rift, then escape");
                break;
            case 2:
                int resonanceSeconds = Mathf.FloorToInt(Mathf.Max(localResonanceSeconds, state.ResonanceSeconds));
                RoundAnnouncementUI.SetPersistentHint(PersistentHintOwnerKey, "Fly inside Resonance Band: " + resonanceSeconds + "/" + Mathf.RoundToInt(ResonanceRequiredSeconds) + "s");
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
            case 1: return GetRiftPosition(state, state.TrueRiftIndex);
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

    void ResolveRiftPositions(Vector2 anchor, out Vector2 rift1, out Vector2 rift2, out Vector2 rift3)
    {
        Vector2 baseDirection = UnityEngine.Random.insideUnitCircle.normalized;
        if (baseDirection.sqrMagnitude < 0.001f)
            baseDirection = Vector2.right;

        rift1 = ClampToMapBounds(anchor + baseDirection * RiftClusterRadius);
        rift2 = ClampToMapBounds(anchor + (Vector2)(Quaternion.Euler(0f, 0f, 122f) * baseDirection) * RiftClusterRadius);
        rift3 = ClampToMapBounds(anchor + (Vector2)(Quaternion.Euler(0f, 0f, 244f) * baseDirection) * RiftClusterRadius);

        if (Vector2.Distance(rift1, rift2) < RiftMinSeparation ||
            Vector2.Distance(rift1, rift3) < RiftMinSeparation ||
            Vector2.Distance(rift2, rift3) < RiftMinSeparation)
        {
            rift1 = ClampToMapBounds(anchor + Vector2.right * RiftClusterRadius);
            rift2 = ClampToMapBounds(anchor + new Vector2(-0.48f, 0.88f) * RiftClusterRadius);
            rift3 = ClampToMapBounds(anchor + new Vector2(-0.48f, -0.88f) * RiftClusterRadius);
        }
    }

    void ResolveResonanceNodePositions(Vector2 anchor, out Vector2 node1, out Vector2 node2)
    {
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = Mathf.Max(7f, mapSize.x * 0.5f - 5f);
        float halfY = Mathf.Max(7f, mapSize.y * 0.5f - 5f);
        float maxDistance = Mathf.Min(ResonanceNodeMaxDistance, Mathf.Max(ResonanceNodeMinDistance, Mathf.Min(halfX, halfY) * 1.35f));
        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);

        for (int attempt = 0; attempt < 72; attempt++)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.right;

            float distance = UnityEngine.Random.Range(ResonanceNodeMinDistance, maxDistance);
            Vector2 center = ClampToMapBounds(anchor + UnityEngine.Random.insideUnitCircle * 3.8f);
            Vector2 candidate1 = ClampToMapBounds(center + direction * distance * 0.5f);
            Vector2 candidate2 = ClampToMapBounds(center - direction * distance * 0.5f);
            float actualDistance = Vector2.Distance(candidate1, candidate2);
            if (actualDistance < ResonanceNodeMinDistance)
                continue;

            if (!IsFarEnoughFromExtractionZones(candidate1, zones, 9f) ||
                !IsFarEnoughFromExtractionZones(candidate2, zones, 9f))
            {
                continue;
            }

            node1 = candidate1;
            node2 = candidate2;
            return;
        }

        Vector2 fallbackDirection = Mathf.Abs(anchor.x) > Mathf.Abs(anchor.y) ? Vector2.up : Vector2.right;
        float fallbackDistance = Mathf.Clamp(Mathf.Min(halfX, halfY) * 1.15f, ResonanceNodeMinDistance, maxDistance);
        node1 = ClampToMapBounds(anchor + fallbackDirection * fallbackDistance * 0.5f);
        node2 = ClampToMapBounds(anchor - fallbackDirection * fallbackDistance * 0.5f);
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
            Rift2Position = new Vector2(ReadFloat(props, Rift2XKey), ReadFloat(props, Rift2YKey)),
            Rift3Position = new Vector2(ReadFloat(props, Rift3XKey), ReadFloat(props, Rift3YKey)),
            TrueRiftIndex = props.TryGetValue(TrueRiftIndexKey, out object trueRiftValue) && TryConvertToInt(trueRiftValue, out int trueRiftIndex) ? Mathf.Clamp(trueRiftIndex, 1, 3) : 1,
            Rift1Consumed = props.TryGetValue(Rift1ConsumedKey, out object rift1ConsumedValue) && rift1ConsumedValue is bool rift1Consumed && rift1Consumed,
            Rift2Consumed = props.TryGetValue(Rift2ConsumedKey, out object rift2ConsumedValue) && rift2ConsumedValue is bool rift2Consumed && rift2Consumed,
            Rift3Consumed = props.TryGetValue(Rift3ConsumedKey, out object rift3ConsumedValue) && rift3ConsumedValue is bool rift3Consumed && rift3Consumed,
            Node1Position = new Vector2(ReadFloat(props, Node1XKey), ReadFloat(props, Node1YKey)),
            Node2Position = new Vector2(ReadFloat(props, Node2XKey), ReadFloat(props, Node2YKey)),
            Node1Done = props.TryGetValue(Node1DoneKey, out object node1Value) && node1Value is bool node1Done && node1Done,
            Node2Done = props.TryGetValue(Node2DoneKey, out object node2Value) && node2Value is bool node2Done && node2Done,
            ResonanceSeconds = props.TryGetValue(ResonanceSecondsKey, out object resonanceValue) && TryConvertToFloat(resonanceValue, out float resonanceSeconds) ? Mathf.Clamp(resonanceSeconds, 0f, ResonanceRequiredSeconds) : 0f,
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

    bool TryGetUsableRiftIndex(PlotState state, Vector2 playerPosition, out int riftIndex)
    {
        for (int i = 1; i <= 3; i++)
        {
            if (IsRiftConsumed(state, i))
                continue;

            if (IsWithinUseRange(playerPosition, GetRiftPosition(state, i)))
            {
                riftIndex = i;
                return true;
            }
        }

        riftIndex = 0;
        return false;
    }

    static bool IsRiftConsumed(PlotState state, int index)
    {
        switch (index)
        {
            case 1: return state.Rift1Consumed;
            case 2: return state.Rift2Consumed;
            case 3: return state.Rift3Consumed;
            default: return true;
        }
    }

    static Vector2 GetRiftPosition(PlotState state, int index)
    {
        switch (index)
        {
            case 2: return state.Rift2Position;
            case 3: return state.Rift3Position;
            default: return state.RiftPosition;
        }
    }

    static string GetRiftConsumedKey(int index)
    {
        switch (index)
        {
            case 2: return Rift2ConsumedKey;
            case 3: return Rift3ConsumedKey;
            default: return Rift1ConsumedKey;
        }
    }

    void ApplyFalseRiftBurst(PlayerHealth player, Vector2 riftPosition)
    {
        if (player == null || player.photonView == null)
            return;

        int shieldDamage = Mathf.Max(28, Mathf.CeilToInt(player.MaxShield * FalseRiftShieldDamageRatio));
        player.photonView.RPC(
            nameof(PlayerHealth.ApplyInvaderEnvironmentalDrainRpc),
            RpcTarget.MasterClient,
            shieldDamage,
            0,
            Mathf.Max(1, Mathf.RoundToInt(player.maxHP * 0.02f)),
            riftPosition.x,
            riftPosition.y);

        ArtifactActivationFlashVfx.Spawn(new Vector3(riftPosition.x, riftPosition.y, 0f), 2.8f);
    }

    bool IsWithinUseRange(Vector2 playerPosition, Vector2 objectivePosition)
    {
        return Vector2.Distance(playerPosition, objectivePosition) <= UseRange;
    }

    bool TryGetResonanceBandInfo(
        Vector2 playerPosition,
        Vector2 node1,
        Vector2 node2,
        out float projection,
        out float signedDistance,
        out Vector2 direction,
        out Vector2 perpendicular)
    {
        Vector2 segment = node2 - node1;
        float length = segment.magnitude;
        if (length <= 0.001f)
        {
            projection = 0f;
            signedDistance = 0f;
            direction = Vector2.right;
            perpendicular = Vector2.up;
            return false;
        }

        direction = segment / length;
        perpendicular = new Vector2(-direction.y, direction.x);
        Vector2 fromStart = playerPosition - node1;
        projection = Vector2.Dot(fromStart, direction) / length;
        signedDistance = Vector2.Dot(fromStart, perpendicular);
        return projection >= 0f && projection <= 1f && Mathf.Abs(signedDistance) <= ResonanceBandHalfWidth;
    }

    bool IsStage4EscortCloseEnough(PlayerHealth player, PlotState state)
    {
        if (state.Stage != 4)
            return true;

        if (!state.ObjectiveDone)
            return false;

        return Vector2.Distance(player.transform.position, state.ShellPosition) <= EscortFormMaxExtractionDistance;
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
        visualStateSignature = string.Empty;
        localResonanceSeconds = 0f;
        nextResonanceSyncTime = 0f;
        localWasInResonanceBand = false;
        localLastResonanceProjection = -1f;
        localAssimilationSeconds = 0f;
        nextAssimilationSyncTime = 0f;
        localShieldDrainAccumulator = 0f;
        localHpDrainAccumulator = 0f;
        localEscortFormPosition = Vector2.zero;
        localEscortFormInitialized = false;
        nextEscortSyncTime = 0f;
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
        visualStateSignature = string.Empty;
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

static class InvaderPlotVfxUtility
{
    static Material lineMaterial;
    static Sprite glowSprite;

    public static Material LineMaterial
    {
        get
        {
            if (lineMaterial != null)
                return lineMaterial;

            lineMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                name = "InvaderPlotVfxMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            lineMaterial.renderQueue = 5000;
            return lineMaterial;
        }
    }

    public static Sprite GlowSprite
    {
        get
        {
            if (glowSprite != null)
                return glowSprite;

            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "InvaderPlotGlowTexture",
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                    float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha * (1f - Mathf.Clamp01(distance * 0.25f));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            glowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            glowSprite.name = "InvaderPlotGlowSprite";
            glowSprite.hideFlags = HideFlags.HideAndDontSave;
            return glowSprite;
        }
    }

    public static LineRenderer CreateLine(Transform parent, string name, float width, int order, bool loop = false)
    {
        GameObject lineObject = new GameObject(name);
        if (parent != null)
            lineObject.transform.SetParent(parent, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.material = LineMaterial;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 12;
        line.numCornerVertices = 8;
        line.widthMultiplier = width;
        line.loop = loop;
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = order;
        return line;
    }

    public static void SetCircle(LineRenderer line, Vector2 center, float radius, Color color, int segments, float wobble)
    {
        if (line == null)
            return;

        int safeSegments = Mathf.Max(16, segments);
        line.enabled = color.a > 0.001f;
        line.startColor = color;
        line.endColor = color;
        line.positionCount = safeSegments;
        for (int i = 0; i < safeSegments; i++)
        {
            float angle = i / (float)safeSegments * Mathf.PI * 2f;
            float localRadius = radius * (1f + Mathf.Sin(angle * 4f + Time.time * 2.1f) * wobble);
            line.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * localRadius, center.y + Mathf.Sin(angle) * localRadius, -0.07f));
        }
    }
}

public sealed class InvaderResonanceBandVisual : MonoBehaviour
{
    LineRenderer bandGlow;
    LineRenderer bandCore;
    LineRenderer edgeA;
    LineRenderer edgeB;
    Vector2 node1;
    Vector2 node2;
    float halfWidth;

    public void Configure(Vector2 start, Vector2 end, float width)
    {
        node1 = start;
        node2 = end;
        halfWidth = Mathf.Max(0.5f, width);
    }

    void Awake()
    {
        bandGlow = InvaderPlotVfxUtility.CreateLine(transform, "ResonanceBandGlow", 2.2f, 17);
        bandCore = InvaderPlotVfxUtility.CreateLine(transform, "ResonanceBandCore", 0.28f, 20);
        edgeA = InvaderPlotVfxUtility.CreateLine(transform, "ResonanceBandEdgeA", 0.08f, 21);
        edgeB = InvaderPlotVfxUtility.CreateLine(transform, "ResonanceBandEdgeB", 0.08f, 21);
    }

    void Update()
    {
        Vector2 segment = node2 - node1;
        if (segment.sqrMagnitude < 0.001f)
            return;

        Vector2 direction = segment.normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float pulse = 0.5f + Mathf.Sin(Time.time * 3.6f) * 0.5f;
        SetLine(bandGlow, node1, node2, new Color(0.38f, 1f, 0.78f, 0.18f + pulse * 0.1f), halfWidth * 2f);
        SetLine(bandCore, node1, node2, new Color(0.78f, 1f, 0.92f, 0.72f), 0.18f + pulse * 0.08f);
        SetLine(edgeA, node1 + perpendicular * halfWidth, node2 + perpendicular * halfWidth, new Color(0.48f, 1f, 0.82f, 0.58f), 0.08f);
        SetLine(edgeB, node1 - perpendicular * halfWidth, node2 - perpendicular * halfWidth, new Color(0.48f, 1f, 0.82f, 0.58f), 0.08f);
    }

    void SetLine(LineRenderer line, Vector2 start, Vector2 end, Color color, float width)
    {
        if (line == null)
            return;

        line.widthMultiplier = width;
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, color.a * 0.62f);
        line.positionCount = 2;
        line.SetPosition(0, new Vector3(start.x, start.y, -0.08f));
        line.SetPosition(1, new Vector3(end.x, end.y, -0.08f));
    }
}

public sealed class InvaderAssimilationFieldVisual : MonoBehaviour
{
    LineRenderer outerRing;
    LineRenderer innerRing;
    LineRenderer pulseRing;
    SpriteRenderer glow;
    Vector2 center;
    float radius;

    public void Configure(Vector2 fieldCenter, float fieldRadius)
    {
        center = fieldCenter;
        radius = Mathf.Max(1f, fieldRadius);
        transform.position = new Vector3(center.x, center.y, -0.09f);
    }

    void Awake()
    {
        outerRing = InvaderPlotVfxUtility.CreateLine(transform, "AssimilationOuterRing", 0.12f, 23, true);
        innerRing = InvaderPlotVfxUtility.CreateLine(transform, "AssimilationInnerRing", 0.07f, 24, true);
        pulseRing = InvaderPlotVfxUtility.CreateLine(transform, "AssimilationPulseRing", 0.16f, 25, true);

        GameObject glowObject = new GameObject("AssimilationFieldGlow");
        glowObject.transform.SetParent(transform, false);
        glow = glowObject.AddComponent<SpriteRenderer>();
        glow.sprite = InvaderPlotVfxUtility.GlowSprite;
        glow.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        glow.sortingOrder = 16;
    }

    void Update()
    {
        float pulse = 0.5f + Mathf.Sin(Time.time * 2.2f) * 0.5f;
        InvaderPlotVfxUtility.SetCircle(outerRing, center, radius * (1f + pulse * 0.04f), new Color(0.42f, 1f, 0.76f, 0.72f), 96, 0.018f);
        InvaderPlotVfxUtility.SetCircle(innerRing, center, radius * 0.62f, new Color(0.72f, 1f, 0.9f, 0.45f), 72, 0.045f);
        InvaderPlotVfxUtility.SetCircle(pulseRing, center, Mathf.Lerp(radius * 0.18f, radius * 0.92f, pulse), new Color(0.2f, 1f, 0.62f, 0.18f + 0.28f * (1f - pulse)), 72, 0.02f);

        if (glow != null)
        {
            float size = radius * (2.25f + pulse * 0.18f);
            glow.transform.localScale = new Vector3(size, size, 1f);
            glow.color = new Color(0.26f, 1f, 0.72f, 0.12f + pulse * 0.07f);
        }
    }
}

public sealed class InvaderAssimilationSparksVfx : MonoBehaviour
{
    const int SparkCount = 8;
    const float KeepAliveSeconds = 0.18f;

    readonly LineRenderer[] sparks = new LineRenderer[SparkCount];
    readonly float[] phases = new float[SparkCount];
    float keepAliveUntil;

    public static void Attach(GameObject target)
    {
        if (!RoomSettings.AreVisualEffectsEnabled() || target == null)
            return;

        InvaderAssimilationSparksVfx vfx = target.GetComponent<InvaderAssimilationSparksVfx>();
        if (vfx == null)
            vfx = target.AddComponent<InvaderAssimilationSparksVfx>();

        vfx.keepAliveUntil = Time.time + KeepAliveSeconds;
    }

    void Awake()
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            sparks[i] = InvaderPlotVfxUtility.CreateLine(transform, "AssimilationSpark" + i, 0.045f, 44);
            phases[i] = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }
    }

    void Update()
    {
        if (Time.time > keepAliveUntil)
        {
            Destroy(this);
            return;
        }

        Vector2 center = transform.position;
        for (int i = 0; i < sparks.Length; i++)
        {
            float angle = phases[i] + Time.time * (1.8f + i * 0.09f);
            float radius = 0.55f + Mathf.Sin(Time.time * 4.1f + phases[i]) * 0.18f + (i % 3) * 0.12f;
            Vector2 start = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector2 tangent = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
            Vector2 end = start + tangent * (0.22f + (i % 2) * 0.14f);
            Color color = new Color(0.55f, 1f, 0.82f, 0.42f + Mathf.Sin(Time.time * 5f + phases[i]) * 0.18f);
            sparks[i].startColor = color;
            sparks[i].endColor = new Color(0.9f, 1f, 0.92f, 0f);
            sparks[i].positionCount = 2;
            sparks[i].SetPosition(0, new Vector3(start.x, start.y, -0.11f));
            sparks[i].SetPosition(1, new Vector3(end.x, end.y, -0.11f));
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i] != null)
                Destroy(sparks[i].gameObject);
        }
    }
}

public sealed class InvaderEscortFormVisual : MonoBehaviour
{
    SpriteRenderer shellRenderer;
    SpriteRenderer glowRenderer;
    LineRenderer tetherLine;
    int targetActorNumber;
    Vector2 formPosition;
    PlayerHealth targetPlayer;
    float nextTargetResolveTime;

    public void Configure(int actorNumber, Vector2 initialPosition)
    {
        targetActorNumber = actorNumber;
        formPosition = initialPosition;
        transform.position = new Vector3(formPosition.x, formPosition.y, -0.1f);
    }

    void Awake()
    {
        GameObject glowObject = new GameObject("InvaderEscortGlow");
        glowObject.transform.SetParent(transform, false);
        glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = InvaderPlotVfxUtility.GlowSprite;
        glowRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        glowRenderer.sortingOrder = 19;

        GameObject shellObject = new GameObject("InvaderEscortShell");
        shellObject.transform.SetParent(transform, false);
        shellRenderer = shellObject.AddComponent<SpriteRenderer>();
        shellRenderer.sprite = RuntimeSpriteUtility.LoadSprite(
            ShipCatalog.GetShipSkinResourcePath(ShipCatalog.InvaderCamoSkinIndex),
            ShipCatalog.GetShipSkinEditorResourcePath(ShipCatalog.InvaderCamoSkinIndex));
        shellRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        shellRenderer.sortingOrder = 26;
        RuntimeSpriteUtility.FitRenderer(shellRenderer, GameVisualTheme.PlayerTargetSize * 0.95f);

        tetherLine = InvaderPlotVfxUtility.CreateLine(transform, "InvaderEscortTether", 0.08f, 18);
    }

    void Update()
    {
        ResolveTarget();
        Vector2 desired = formPosition;
        if (targetPlayer != null)
        {
            Rigidbody2D body = targetPlayer.GetComponent<Rigidbody2D>();
            Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
            Vector2 behind = velocity.sqrMagnitude > 0.05f ? -velocity.normalized : -(Vector2)targetPlayer.transform.up;
            if (behind.sqrMagnitude < 0.001f)
                behind = Vector2.down;

            desired = (Vector2)targetPlayer.transform.position + behind.normalized * 2.4f;
        }

        formPosition = Vector2.Lerp(formPosition, desired, 1f - Mathf.Exp(-Time.deltaTime * 3.6f));
        transform.position = new Vector3(formPosition.x, formPosition.y, -0.1f);
        float pulse = 0.5f + Mathf.Sin(Time.time * 3.2f) * 0.5f;

        if (shellRenderer != null)
        {
            shellRenderer.color = new Color(0.62f, 1f, 0.86f, 0.38f + pulse * 0.18f);
            shellRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.6f) * 8f);
        }

        if (glowRenderer != null)
        {
            float scale = 2.6f + pulse * 0.32f;
            glowRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            glowRenderer.color = new Color(0.3f, 1f, 0.72f, 0.18f + pulse * 0.08f);
        }

        if (tetherLine != null && targetPlayer != null)
        {
            Vector2 target = targetPlayer.transform.position;
            tetherLine.startColor = new Color(0.42f, 1f, 0.76f, 0.36f);
            tetherLine.endColor = new Color(0.42f, 1f, 0.76f, 0f);
            tetherLine.positionCount = 2;
            tetherLine.SetPosition(0, new Vector3(formPosition.x, formPosition.y, -0.12f));
            tetherLine.SetPosition(1, new Vector3(target.x, target.y, -0.12f));
        }
    }

    void ResolveTarget()
    {
        if (targetPlayer != null && targetPlayer.photonView != null && targetPlayer.photonView.Owner != null && targetPlayer.photonView.Owner.ActorNumber == targetActorNumber)
            return;

        if (Time.time < nextTargetResolveTime)
            return;

        nextTargetResolveTime = Time.time + 0.4f;
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate != null &&
                candidate.photonView != null &&
                candidate.photonView.Owner != null &&
                candidate.photonView.Owner.ActorNumber == targetActorNumber)
            {
                targetPlayer = candidate;
                return;
            }
        }
    }
}
