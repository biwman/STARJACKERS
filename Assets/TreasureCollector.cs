using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TreasureCollector : MonoBehaviourPun
{
    static readonly System.Collections.Generic.Dictionary<int, int> ReservedTreasureCollections = new System.Collections.Generic.Dictionary<int, int>();
    static readonly System.Collections.Generic.Dictionary<int, int> ReservedWreckLoot = new System.Collections.Generic.Dictionary<int, int>();
    static readonly System.Collections.Generic.Dictionary<int, int> ReservedDroppedCargoLoot = new System.Collections.Generic.Dictionary<int, int>();
    static readonly System.Collections.Generic.Dictionary<int, int> ReservedRandomLootWrecks = new System.Collections.Generic.Dictionary<int, int>();
    static TreasureCollector collectButtonBindingOwner;

    const float TreasureScanInterval = 0.08f;
    const float BeamWidth = 0.18f;
    const float BeamJitterAmplitude = 0.13f;
    const float BeamJitterFrequency = 22f;
    const float BeamZOffset = -0.35f;
    const int BeamPointCount = 13;
    const float ArtifactExamineSeconds = 5f;
    const float ArtifactExamineKeepAliveDistance = 0.9f;
    const float ArtifactBeamWidth = 0.16f;
    static readonly Vector2 CollectButtonRoundPosition = new Vector2(230f, 490f);
    const float ValuableCargoAnnouncementDuration = 4f;

    enum NovaPendingCollectibleKind
    {
        Treasure,
        RandomLootWreck,
        Wreck,
        DroppedCargo
    }

    sealed class NovaPendingCollectible
    {
        public NovaPendingCollectibleKind Kind;
        public int ViewId;
        public int LootIndex;
        public string ItemId;
        public string BlueprintItemId;
    }

    enum UseActionType
    {
        None,
        Collect,
        Land,
        AvengerBoard,
        Examine,
        Activate,
        Escape,
        Haul,
        Drop,
        InvaderContact,
        InvaderStabilize,
        InvaderSync
    }

    public Button collectButton;
    public TMP_Text scoreText;

    public PlayerMovement movement;
    public PlayerShooting shooting;

    Treasure currentTreasure;
    ShipWreck currentWreck;
    DroppedCargoCrate currentDroppedCargo;
    ArtifactAsteroid currentArtifactAsteroid;
    bool isCollecting;
    bool isExaminingArtifact;
    ExtractionZone currentExtraction;
    public float collectTime = 3f;
    public int totalScore = 0;
    AudioSource drillingAudioSource;
    LineRenderer collectionBeam;
    LineRenderer artifactExamineBeam;
    float nextTreasureScanTime;
    bool beamActive;
    bool artifactBeamActive;
    Coroutine collectibleUseRoutine;
    Coroutine extractionActivationRoutine;
    Coroutine artifactExamineRoutine;
    bool collectButtonHooked;
    bool isActivatingExtraction;
    RectTransform positionedCollectButtonRect;
    UseButtonVisualController useButtonVisual;
    ExtractionZone pendingActivatedExtraction;
    float pendingActivatedExtractionUntil;
    int pendingTreasureReservationViewId;
    int activeTreasureReservationViewId;

    public bool IsCollectingAny => isCollecting;

    public static void ResetRoundReservations()
    {
        ReservedTreasureCollections.Clear();
        ReservedWreckLoot.Clear();
        ReservedDroppedCargoLoot.Clear();
        ReservedRandomLootWrecks.Clear();
    }

    public bool TryNovaScavengerBurstOnMaster(float rangeMultiplier, float collectDelaySeconds, out string targetViewIds)
    {
        targetViewIds = string.Empty;
        if (!PhotonNetwork.IsMasterClient || photonView == null || photonView.Owner == null)
            return false;

        float maxRange = Treasure.CollectRange * Mathf.Max(1f, rangeMultiplier);
        Vector2 tipPosition = GetShipTipPosition();
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(tipPosition, maxRange + 0.85f, out Collider2D[] hits);
        HashSet<int> queuedViewIds = new HashSet<int>();
        List<int> visualTargetIds = new List<int>();
        List<NovaPendingCollectible> pendingCollectibles = new List<NovaPendingCollectible>();

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            Treasure treasure = hit.GetComponent<Treasure>() ?? hit.GetComponentInParent<Treasure>();
            if (treasure != null)
            {
                if (TryQueueNovaTreasureOnMaster(treasure, tipPosition, maxRange, queuedViewIds, pendingCollectibles))
                    visualTargetIds.Add(treasure.photonView.ViewID);

                continue;
            }

            ShipWreck wreck = hit.GetComponent<ShipWreck>() ?? hit.GetComponentInParent<ShipWreck>();
            if (wreck != null)
            {
                PhotonView wreckView = wreck.GetComponent<PhotonView>();
                if (TryQueueNovaWreckOnMaster(wreck, tipPosition, maxRange, queuedViewIds, pendingCollectibles) && wreckView != null)
                    visualTargetIds.Add(wreckView.ViewID);

                continue;
            }

            DroppedCargoCrate crate = hit.GetComponent<DroppedCargoCrate>() ?? hit.GetComponentInParent<DroppedCargoCrate>();
            if (crate != null)
            {
                PhotonView crateView = crate.GetComponent<PhotonView>();
                if (TryQueueNovaDroppedCargoOnMaster(crate, tipPosition, maxRange, queuedViewIds, pendingCollectibles) && crateView != null)
                    visualTargetIds.Add(crateView.ViewID);
            }
        }

        targetViewIds = BuildViewIdCsv(visualTargetIds);
        if (pendingCollectibles.Count > 0)
            StartCoroutine(ResolveNovaScavengerBurstAfterDelay(pendingCollectibles, collectDelaySeconds));

        return visualTargetIds.Count > 0;
    }

    void Start()
    {
        if (ViperRecoveryPlotController.TryEnsureViperWreckRuntime(gameObject))
        {
            enabled = false;
            return;
        }

        if (PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
        {
            PlayerDeployableRuntime.EnsureAttached(gameObject);
            enabled = false;
            return;
        }

        if (LureBeaconDecoy.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
        {
            LureBeaconDecoy.EnsureAttached(gameObject);
            enabled = false;
            return;
        }

        if (NeutralRiderController.IsNeutralRiderInstantiationData(photonView != null ? photonView.InstantiationData : null) ||
            NeutralRiderController.IsNeutralRider(gameObject))
        {
            NeutralRiderController rider = GetComponent<NeutralRiderController>();
            if (rider == null)
                rider = gameObject.AddComponent<NeutralRiderController>();

            rider.InitializeFromPhotonData();
            enabled = false;
            return;
        }

        if (ShouldDisableUseControllerForActor())
        {
            DisableForEnemyAstronaut();
            return;
        }

        if (GetComponent<EnemyBot>() != null)
        {
            enabled = false;
            return;
        }

        SetupDrillingAudio();
        SetupBeam();
        SetupArtifactExamineBeam();

        if (!photonView.IsMine)
            return;

        TryBindHudReferences();

        if (scoreText != null)
        {
            scoreText.text = "XP: 0";
        }

        UpdateUseButtonAvailability();
        SyncScoreProperty();
    }

    void Update()
    {
        if (ShouldDisableUseControllerForActor())
        {
            DisableForEnemyAstronaut();
            return;
        }

        if (photonView.IsMine && (!collectButtonHooked || collectButtonBindingOwner != this || collectButton == null || scoreText == null))
        {
            TryBindHudReferences();
        }

        HandleKeyboardUseShortcut();

        if (photonView.IsMine && IsAstronautMode())
        {
            if (isExaminingArtifact)
                CancelArtifactExamine();

            if (isCollecting)
            {
                CancelActiveCollection();
            }
            else if (currentTreasure != null || currentWreck != null || currentDroppedCargo != null || currentArtifactAsteroid != null)
            {
                ClearCurrentHighlight();
                currentTreasure = null;
                currentWreck = null;
                currentDroppedCargo = null;
                currentArtifactAsteroid = null;
            }

            UpdateUseButtonAvailability();
            UpdateCollectionBeam();
            UpdateArtifactExamineBeam();
            return;
        }

        if (photonView.IsMine && Time.unscaledTime >= nextTreasureScanTime)
        {
            nextTreasureScanTime = Time.unscaledTime + TreasureScanInterval;
            if (!HasLockedCollectibleTarget())
                RefreshClosestCollectible();
        }

        if (photonView.IsMine)
            EnsureActiveCollectibleTargetStillValid();

        UpdateUseButtonAvailability();
        UpdateHaulChargeUseProgress();
        UpdateCollectionBeam();
        UpdateArtifactExamineBeam();
    }

    void EnsureActiveCollectibleTargetStillValid()
    {
        if (isExaminingArtifact && !IsArtifactStillUsable(currentArtifactAsteroid))
        {
            CancelArtifactExamine();
            return;
        }

        if (!isCollecting)
            return;

        if (pendingTreasureReservationViewId > 0)
        {
            if (PhotonView.Find(pendingTreasureReservationViewId) == null)
                CancelActiveCollection();

            return;
        }

        if (currentTreasure != null && activeTreasureReservationViewId > 0)
        {
            PhotonView treasureView = currentTreasure.GetComponent<PhotonView>();
            if (treasureView == null || treasureView.ViewID != activeTreasureReservationViewId)
                CancelActiveCollection();
        }
        else if (currentTreasure == null && currentWreck == null && currentDroppedCargo == null)
        {
            CancelActiveCollection();
        }
    }

    void TryBindHudReferences()
    {
        if (!photonView.IsMine)
            return;

        if (ShouldDisableUseControllerForActor())
        {
            DisableForEnemyAstronaut();
            return;
        }

        if (scoreText == null)
        {
            GameObject scoreObject = GameObject.Find("ScoreText");
            if (scoreObject != null)
                scoreText = scoreObject.GetComponent<TMP_Text>();
        }

        if (collectButton == null)
        {
            GameObject buttonObject = GameObject.Find("CollectButton");
            if (buttonObject != null)
                collectButton = buttonObject.GetComponent<Button>();
        }

        EnsureCollectButtonPositioned();

        if (collectButton == null)
            return;

        if (collectButtonHooked && collectButtonBindingOwner == this)
            return;

        useButtonVisual = collectButton.GetComponent<UseButtonVisualController>();

        EventTrigger trigger = collectButton.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = collectButton.gameObject.AddComponent<EventTrigger>();

        trigger.triggers = new List<EventTrigger.Entry>();

        EventTrigger.Entry down = new EventTrigger.Entry();
        down.eventID = EventTriggerType.PointerDown;
        down.callback.AddListener(_ => StartHolding());
        trigger.triggers.Add(down);

        collectButtonHooked = true;
        collectButtonBindingOwner = this;
        UpdateUseButtonAvailability();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!photonView.IsMine)
            return;

        ExtractionZone ez = other.GetComponent<ExtractionZone>();
        if (ez == null)
            ez = other.GetComponentInParent<ExtractionZone>();
        if (ez != null)
        {
            currentExtraction = ez;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!photonView.IsMine)
            return;

        ExtractionZone ez = other.GetComponent<ExtractionZone>();
        if (ez == null)
            ez = other.GetComponentInParent<ExtractionZone>();
        if (ez != null && currentExtraction == ez)
        {
            currentExtraction = null;
        }
    }

    public void StartHolding()
    {
        if (!photonView.IsMine)
            return;

        if (ShouldDisableUseControllerForActor())
            return;

        if (isActivatingExtraction)
            return;

        if (isExaminingArtifact)
            return;

        PlayerHealth playerHealth = GetComponent<PlayerHealth>();
        if (BisonIndustrialPlotController.IsHaulChargeInProgress(playerHealth))
            return;
        if (InvaderInvasionPlotController.IsUseChargeInProgress(playerHealth))
            return;

        AvengerWarBase avengerBase = AvengerWarBase.FindClosestUsable(transform.position);
        if (avengerBase != null)
        {
            CancelActiveCollection();
            if (avengerBase.TryStartUse(GetComponent<PlayerHealth>()))
                return;
        }

        PlayerRepairDocking repairDocking = GetComponent<PlayerRepairDocking>();
        RepairBay repairBay = RepairBay.FindClosestUsable(transform.position);
        SpaceFactory spaceFactory = SpaceFactory.FindClosestUsable(transform.position);
        ScienceStation scienceStation = ScienceStation.FindClosestUsable(transform.position);
        if (repairDocking != null && (repairDocking.IsBusy || repairBay != null || spaceFactory != null || scienceStation != null))
        {
            CancelActiveCollection();
            if (repairDocking.TryStartUse(repairBay, spaceFactory, scienceStation))
                return;
        }

        ArtifactAsteroid artifact = ResolveUsableArtifactAsteroid();
        if (artifact != null)
        {
            CancelActiveCollection();
            StartArtifactExamine(artifact);
            return;
        }

        if (BisonIndustrialPlotController.TryDropHaul(playerHealth))
        {
            CancelActiveCollection();
            return;
        }

        if (BisonIndustrialPlotController.TryStartHaul(playerHealth))
        {
            CancelActiveCollection();
            SetUseProgress(0f, true);
            UpdateUseButtonAvailability();
            return;
        }

        if (InvaderInvasionPlotController.TryStartUse(playerHealth))
        {
            CancelActiveCollection();
            SetUseProgress(0f, true);
            UpdateUseButtonAvailability();
            return;
        }

        currentExtraction = ResolveNearbyExtractionZone();
        if (currentExtraction != null)
        {
            if (CanUseExtractionNow(currentExtraction))
            {
                CancelActiveCollection();
                UseExtraction(currentExtraction);
                return;
            }

            if (!CanCollectWhileExtractionUnavailable(currentExtraction))
                return;
        }

        TryStartCollectibleUse();
    }

    void HandleKeyboardUseShortcut()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (!photonView.IsMine)
            return;

        if (!IsKeyboardUseShortcutPressedThisFrame())
            return;

        if (IsTypingInInputField())
            return;

        StartHolding();
#endif
    }

    static bool IsKeyboardUseShortcutPressedThisFrame()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E))
            return true;
#endif
#endif

        return false;
    }

    static bool IsTypingInInputField()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected == null)
            return false;

        return selected.GetComponent<TMP_InputField>() != null ||
               selected.GetComponentInParent<TMP_InputField>() != null ||
               selected.GetComponent<UnityEngine.UI.InputField>() != null ||
               selected.GetComponentInParent<UnityEngine.UI.InputField>() != null;
    }

    bool TryStartCollectibleUse()
    {
        if (IsAstronautMode())
            return false;

        if (isCollecting)
            return true;

        if (!HasLockedCollectibleTarget())
            RefreshClosestCollectible();

        if (!HasUsableCollectibleTarget())
            ForceResolveCollectibleAtUsePress();

        if (!HasUsableCollectibleTarget())
            return false;

        if (!CanStoreCurrentCollectible())
            return true;

        if (currentTreasure != null && !isCollecting)
        {
            RequestTreasureCollectionReservation(currentTreasure);
            return true;
        }

        if (currentWreck != null && currentWreck.HasLoot && !currentWreck.isBeingCollected && !isCollecting)
        {
            isCollecting = true;
            StartCollectibleFeedback(currentWreck.GetComponent<PhotonView>());
            collectibleUseRoutine = StartCoroutine(LootWreckRoutine(currentWreck));
            return true;
        }

        if (currentDroppedCargo != null && currentDroppedCargo.HasLoot && !currentDroppedCargo.isBeingCollected && !isCollecting)
        {
            isCollecting = true;
            StartCollectibleFeedback(currentDroppedCargo.GetComponent<PhotonView>());
            collectibleUseRoutine = StartCoroutine(LootDroppedCargoRoutine(currentDroppedCargo));
            return true;
        }

        return false;
    }

    ExtractionZone ResolveNearbyExtractionZone()
    {
        PlayerHealth playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
            return null;

        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        ExtractionZone bestZone = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < zones.Length; i++)
        {
            ExtractionZone extractionZone = zones[i];
            if (extractionZone == null || !extractionZone.CanPlayerRequestEvacuation(playerHealth))
                continue;

            float distance = GetDistanceToExtractionZone(extractionZone);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestZone = extractionZone;
            }
        }

        return bestZone;
    }

    bool IsAstronautMode()
    {
        ActorIdentity identity = ActorIdentity.Ensure(gameObject);
        return identity != null && identity.IsAstronaut;
    }

    public void StopHolding()
    {
        if (!photonView.IsMine)
            return;

        PlayerRepairDocking repairDocking = GetComponent<PlayerRepairDocking>();
        if (repairDocking != null)
            repairDocking.StopUseHold();

        CancelArtifactExamine();
        CancelActiveCollection();
        CancelExtractionActivation();
    }

    public void CancelCollectionForShot()
    {
        if (!photonView.IsMine)
            return;

        if (isCollecting)
            CancelActiveCollection();

        if (isExaminingArtifact)
            CancelArtifactExamine();

        InvaderInvasionPlotController.NotifyUseCanceledByShot(GetComponent<PlayerHealth>());
    }

    void CancelActiveCollection()
    {
        bool hadActiveCollection = isCollecting || collectibleUseRoutine != null;
        bool hadTarget = currentTreasure != null || currentWreck != null || currentDroppedCargo != null;
        if (!hadActiveCollection && !hadTarget)
            return;

        if (hadActiveCollection)
            ReleaseCurrentCollectibleReservation();

        isCollecting = false;

        if (collectibleUseRoutine != null)
        {
            StopCoroutine(collectibleUseRoutine);
            collectibleUseRoutine = null;
        }

        if (hadActiveCollection)
        {
            StopCollectibleFeedback();
            SetUseProgress(0f, false);
        }

        ClearCurrentHighlight();
        currentTreasure = null;
        currentWreck = null;
        currentDroppedCargo = null;
        UpdateUseButtonAvailability();
    }

    void UseExtraction(ExtractionZone extractionZone)
    {
        if (extractionZone == null)
            return;

        if (extractionZone.IsTransitioning || extractionZone.IsEvacuating || IsPendingActivatedExtraction(extractionZone))
            return;

        if (!extractionZone.IsActive)
        {
            StartExtractionActivation(extractionZone);
            return;
        }

        PhotonView ezView = extractionZone.GetComponent<PhotonView>();
        if (ezView != null)
            photonView.RPC(nameof(RequestUseExtraction), RpcTarget.MasterClient, ezView.ViewID);
    }

    bool CanUseExtractionNow(ExtractionZone extractionZone)
    {
        return extractionZone != null &&
               !extractionZone.IsTransitioning &&
               !extractionZone.IsEvacuating &&
               !IsPendingActivatedExtraction(extractionZone);
    }

    bool CanCollectWhileExtractionUnavailable(ExtractionZone extractionZone)
    {
        if (extractionZone == null)
            return false;

        if (extractionZone.IsTransitioning)
            return true;

        if (extractionZone.IsEvacuating)
            return false;

        return IsPendingActivatedExtraction(extractionZone);
    }

    void StartExtractionActivation(ExtractionZone extractionZone)
    {
        if (extractionZone == null || isActivatingExtraction)
            return;

        extractionActivationRoutine = StartCoroutine(ActivateExtractionRoutine(extractionZone));
    }

    IEnumerator ActivateExtractionRoutine(ExtractionZone extractionZone)
    {
        isActivatingExtraction = true;

        float timer = 0f;
        float requiredActivationTime = Mathf.Max(0.1f, (extractionZone != null ? extractionZone.activationTime : collectTime) * GetAtlasExtractionActivationMultiplier());
        SetUseProgress(0f, true);

        while (timer < requiredActivationTime)
        {
            if (extractionZone == null ||
                extractionZone.IsActive ||
                extractionZone.IsTransitioning ||
                extractionZone.IsEvacuating ||
                !IsExtractionStillUsable(extractionZone))
            {
                FinishExtractionActivation(false);
                yield break;
            }

            currentExtraction = extractionZone;
            timer += Time.deltaTime;
            SetUseProgress(timer / requiredActivationTime, true);
            yield return null;
        }

        PhotonView ezView = extractionZone.GetComponent<PhotonView>();
        if (ezView != null)
        {
            pendingActivatedExtraction = extractionZone;
            pendingActivatedExtractionUntil = Time.time + 2f;
            photonView.RPC(nameof(RequestUseExtraction), RpcTarget.MasterClient, ezView.ViewID);
        }

        FinishExtractionActivation(false);
    }

    void CancelExtractionActivation()
    {
        if (!isActivatingExtraction && extractionActivationRoutine == null)
            return;

        if (extractionActivationRoutine != null)
        {
            StopCoroutine(extractionActivationRoutine);
            extractionActivationRoutine = null;
        }

        FinishExtractionActivation(false);
    }

    void FinishExtractionActivation(bool keepFullProgress)
    {
        isActivatingExtraction = false;
        extractionActivationRoutine = null;
        SetUseProgress(keepFullProgress ? 1f : 0f, keepFullProgress);
        UpdateUseButtonAvailability();
    }

    void StartArtifactExamine(ArtifactAsteroid artifact)
    {
        if (artifact == null || isExaminingArtifact)
            return;

        currentArtifactAsteroid = artifact;
        artifactExamineRoutine = StartCoroutine(ExamineArtifactRoutine(artifact));
    }

    IEnumerator ExamineArtifactRoutine(ArtifactAsteroid artifact)
    {
        isExaminingArtifact = true;
        float timer = 0f;
        SetUseProgress(0f, true);
        StartArtifactExamineFeedback(artifact);

        while (timer < ArtifactExamineSeconds)
        {
            if (!IsArtifactStillUsable(artifact))
            {
                FinishArtifactExamine(false);
                yield break;
            }

            currentArtifactAsteroid = artifact;
            timer += Time.deltaTime;
            SetUseProgress(timer / ArtifactExamineSeconds, true);
            yield return null;
        }

        photonView.RPC(nameof(RequestActivateArtifactAsteroid), RpcTarget.MasterClient, artifact.StableId);
        FinishArtifactExamine(false);
    }

    void CancelArtifactExamine()
    {
        if (!isExaminingArtifact && artifactExamineRoutine == null && currentArtifactAsteroid == null)
            return;

        if (artifactExamineRoutine != null)
        {
            StopCoroutine(artifactExamineRoutine);
            artifactExamineRoutine = null;
        }

        FinishArtifactExamine(false);
    }

    void FinishArtifactExamine(bool keepFullProgress)
    {
        bool wasExamining = isExaminingArtifact || artifactBeamActive;
        isExaminingArtifact = false;
        artifactExamineRoutine = null;
        if (wasExamining)
            StopArtifactExamineFeedback();

        SetUseProgress(keepFullProgress ? 1f : 0f, keepFullProgress);
        currentArtifactAsteroid = null;
        UpdateUseButtonAvailability();
    }

    bool IsExtractionStillUsable(ExtractionZone extractionZone)
    {
        if (extractionZone == null)
            return false;

        PlayerHealth playerHealth = GetComponent<PlayerHealth>();
        return playerHealth != null && extractionZone.CanPlayerRequestEvacuation(playerHealth);
    }

    float GetDistanceToExtractionZone(ExtractionZone extractionZone)
    {
        if (extractionZone == null)
            return float.MaxValue;

        return extractionZone.GetInteractionDistanceToPoint(transform.position);
    }

    float GetAtlasExtractionActivationMultiplier()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        if (!PilotCatalog.IsSelectedPilot(owner, PilotCatalog.AtlasId) || !PlayerProfileService.HasInstance)
            return 1f;

        return AtlasPilotRoundTracker.HasCargoRunBonus(PlayerProfileService.Instance.CurrentProfile)
            ? AtlasPilotRoundTracker.ExtractionActivationMultiplier
            : 1f;
    }

    void RequestTreasureCollectionReservation(Treasure treasure)
    {
        if (treasure == null || treasure.isBeingCollected || IsTreasureTemporarilyLocked(treasure))
        {
            AbortCollection(treasure);
            return;
        }

        PhotonView treasureView = treasure.GetComponent<PhotonView>();
        if (treasureView == null || !PhotonNetwork.InRoom)
        {
            BeginTreasureCollection(treasure, 0);
            return;
        }

        isCollecting = true;
        currentTreasure = treasure;
        pendingTreasureReservationViewId = treasureView.ViewID;
        activeTreasureReservationViewId = 0;
        SetUseProgress(0f, true);
        photonView.RPC(nameof(RequestReserveTreasureCollection), RpcTarget.MasterClient, treasureView.ViewID);
    }

    void BeginTreasureCollection(Treasure treasure, int reservedViewId)
    {
        if (!photonView.IsMine)
            return;

        if (treasure == null || treasure.isBeingCollected || IsTreasureTemporarilyLocked(treasure))
        {
            CancelActiveCollection();
            return;
        }

        pendingTreasureReservationViewId = 0;
        activeTreasureReservationViewId = reservedViewId;
        currentTreasure = treasure;
        isCollecting = true;
        StartCollectibleFeedback(treasure.GetComponent<PhotonView>());
        collectibleUseRoutine = StartCoroutine(CollectTreasureRoutine(treasure));
    }

    IEnumerator CollectTreasureRoutine(Treasure treasureToCollect)
    {
        if (treasureToCollect == null || treasureToCollect.isBeingCollected || IsTreasureTemporarilyLocked(treasureToCollect))
        {
            AbortCollection(treasureToCollect);
            yield break;
        }

        treasureToCollect.isBeingCollected = true;

        float timer = 0f;
        float requiredCollectTime = GetTreasureCollectTime(treasureToCollect);
        SetUseProgress(0f, true);

        while (timer < requiredCollectTime)
        {
            if (!isCollecting || treasureToCollect == null || IsTreasureTemporarilyLocked(treasureToCollect) || !IsTreasureInCollectRange(treasureToCollect, true))
            {
                AbortCollection(treasureToCollect);
                yield break;
            }

            currentTreasure = treasureToCollect;
            timer += Time.deltaTime;
            SetUseProgress(timer / requiredCollectTime, true);
            yield return null;
        }

        string collectedItemId = InventoryItemCatalog.ResolveAlienSecretItemId(
            treasureToCollect.itemId,
            treasureToCollect.visualVariantIndex);
        PhotonView treasureView = treasureToCollect.GetComponent<PhotonView>();
        int treasureViewId = treasureView != null ? treasureView.ViewID : 0;

        if (InventoryItemCatalog.IsRandomLootWreckItem(collectedItemId))
        {
            if (treasureViewId > 0)
                photonView.RPC(nameof(RequestRandomLootWreck), RpcTarget.MasterClient, treasureViewId);

            treasureToCollect.isBeingCollected = false;
            ReleaseMasterTreasureReservation(true);
            FinishCollection();
            yield break;
        }

        int collectXp = RoundXpTracker.RecordTreasureCollected(photonView.Owner, collectedItemId);
        AddScore(collectXp);
        StoreCollectedItem(ResolveCovaxAsteroidCargoItem(collectedItemId), collectedItemId);

        treasureToCollect.isBeingCollected = false;
        FinishCollection();
        ReleaseMasterTreasureReservation(false);

        if (treasureViewId > 0)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonView viewToDestroy = PhotonView.Find(treasureViewId);
                if (viewToDestroy != null)
                {
                    ReservedTreasureCollections.Remove(treasureViewId);
                    SpaceTrapTarget.DetonateIfArmed(treasureViewId, photonView != null ? photonView.ViewID : 0);
                    NotifyPirateBasesAboutCollectedTarget(treasureViewId);
                    PhotonNetwork.Destroy(viewToDestroy.gameObject);
                }
            }
            else
            {
                photonView.RPC(nameof(RequestDestroyTreasure), RpcTarget.MasterClient, treasureViewId);
            }
        }
    }

    float GetTreasureCollectTime(Treasure treasure)
    {
        float pilotBonus = PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.RobyId) ? 0.5f : 0f;
        float adjustedCollectTime = collectTime - pilotBonus;
        if (ShouldApplyVectorResourceRoutine(treasure != null ? treasure.itemId : null))
            adjustedCollectTime /= 3f;

        return Mathf.Max(0.1f, adjustedCollectTime);
    }

    float GetCollectTimeForItem(string itemId)
    {
        float adjustedCollectTime = collectTime;
        if (ShouldApplyVectorResourceRoutine(itemId))
            adjustedCollectTime /= 3f;

        return Mathf.Max(0.1f, adjustedCollectTime);
    }

    bool ShouldApplyVectorResourceRoutine(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (!PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.VectorId))
            return false;

        if (InventoryItemCatalog.GetItemType(itemId) != InventoryItemType.Resource)
            return false;

        InventoryItemRarity rarity = InventoryItemCatalog.GetRarity(itemId);
        return rarity == InventoryItemRarity.Common || rarity == InventoryItemRarity.Uncommon;
    }

    IEnumerator LootWreckRoutine(ShipWreck wreckToLoot)
    {
        if (wreckToLoot == null || !wreckToLoot.HasLoot || wreckToLoot.isBeingCollected)
        {
            AbortCollection();
            yield break;
        }

        wreckToLoot.isBeingCollected = true;

        float timer = 0f;
        int lootIndex = wreckToLoot.GetFirstLootIndex();
        float requiredCollectTime = GetCollectTimeForItem(wreckToLoot.GetLootItemAt(lootIndex));
        SetUseProgress(0f, true);

        while (timer < requiredCollectTime)
        {
            if (!isCollecting || wreckToLoot == null || !wreckToLoot.HasLoot || !IsWreckInCollectRange(wreckToLoot, true))
            {
                wreckToLoot.isBeingCollected = false;
                AbortCollection();
                yield break;
            }

            currentWreck = wreckToLoot;
            timer += Time.deltaTime;
            SetUseProgress(timer / requiredCollectTime, true);
            yield return null;
        }

        PhotonView wreckView = wreckToLoot.GetComponent<PhotonView>();
        if (wreckView != null)
        {
            photonView.RPC(nameof(RequestLootWreck), RpcTarget.MasterClient, wreckView.ViewID, GetLocalUnlockedBlueprintIdsForDropRoll());
        }

        wreckToLoot.isBeingCollected = false;
        FinishCollection();
    }

    IEnumerator LootDroppedCargoRoutine(DroppedCargoCrate crateToLoot)
    {
        if (crateToLoot == null || !crateToLoot.HasLoot || crateToLoot.isBeingCollected)
        {
            AbortCollection();
            yield break;
        }

        crateToLoot.isBeingCollected = true;

        float timer = 0f;
        float requiredCollectTime = GetCollectTimeForItem(crateToLoot.StoredItemId);
        SetUseProgress(0f, true);

        while (timer < requiredCollectTime)
        {
            if (!isCollecting || crateToLoot == null || !crateToLoot.HasLoot || !IsDroppedCargoInCollectRange(crateToLoot, true))
            {
                crateToLoot.isBeingCollected = false;
                AbortCollection();
                yield break;
            }

            currentDroppedCargo = crateToLoot;
            timer += Time.deltaTime;
            SetUseProgress(timer / requiredCollectTime, true);
            yield return null;
        }

        PhotonView crateView = crateToLoot.GetComponent<PhotonView>();
        if (crateView != null)
        {
            photonView.RPC(nameof(RequestLootDroppedCargo), RpcTarget.MasterClient, crateView.ViewID);
        }

        crateToLoot.isBeingCollected = false;
        FinishCollection();
    }

    void ReleaseCurrentCollectibleReservation()
    {
        ReleaseMasterTreasureReservation(true);

        if (currentTreasure != null)
            currentTreasure.isBeingCollected = false;
        if (currentWreck != null)
            currentWreck.isBeingCollected = false;
        if (currentDroppedCargo != null)
            currentDroppedCargo.isBeingCollected = false;
    }

    void ReleaseMasterTreasureReservation(bool notifyMaster)
    {
        int viewId = activeTreasureReservationViewId > 0 ? activeTreasureReservationViewId : pendingTreasureReservationViewId;
        activeTreasureReservationViewId = 0;
        pendingTreasureReservationViewId = 0;

        if (!notifyMaster || viewId <= 0 || !PhotonNetwork.InRoom || photonView == null)
            return;

        photonView.RPC(nameof(RequestReleaseTreasureCollection), RpcTarget.MasterClient, viewId);
    }

    public void ForceCancelCollectionForDeath()
    {
        CancelExtractionActivation();
        isCollecting = false;

        if (currentTreasure != null)
            currentTreasure.isBeingCollected = false;
        if (currentWreck != null)
            currentWreck.isBeingCollected = false;
        if (currentDroppedCargo != null)
            currentDroppedCargo.isBeingCollected = false;

        ReleaseMasterTreasureReservation(true);

        if (collectibleUseRoutine != null)
        {
            StopCoroutine(collectibleUseRoutine);
            collectibleUseRoutine = null;
        }

        StopAllCoroutines();
        StopLocalDrillingLoop();
        SetBeamEnabled(false);
        SetUseProgress(0f, false);
        ClearCurrentHighlight();

        currentTreasure = null;
        currentWreck = null;
        currentDroppedCargo = null;

        if (movement != null) movement.enabled = false;
        if (shooting != null) shooting.enabled = false;
    }

    void AbortCollection(Treasure treasure = null)
    {
        if (treasure != null)
            treasure.isBeingCollected = false;

        ReleaseMasterTreasureReservation(true);
        FinishCollection();
    }

    void FinishCollection()
    {
        isCollecting = false;
        collectibleUseRoutine = null;
        StopCollectibleFeedback();
        SetUseProgress(0f, false);
        ClearCurrentHighlight();
        currentTreasure = null;
        currentWreck = null;
        currentDroppedCargo = null;
        UpdateUseButtonAvailability();
    }

    void StartCollectibleFeedback(PhotonView targetView)
    {
        photonView.RPC(nameof(StartDrillingLoopSfx), RpcTarget.All);
        if (targetView != null)
        {
            photonView.RPC(nameof(SetBeamTargetRpc), RpcTarget.All, targetView.ViewID, true);
        }
    }

    void StopCollectibleFeedback()
    {
        photonView.RPC(nameof(StopDrillingLoopSfx), RpcTarget.All);
        photonView.RPC(nameof(ClearBeamTargetRpc), RpcTarget.All);
    }

    void StartArtifactExamineFeedback(ArtifactAsteroid artifact)
    {
        if (artifact == null)
            return;

        photonView.RPC(nameof(SetArtifactExamineBeamTargetRpc), RpcTarget.All, artifact.StableId, true);
    }

    void StopArtifactExamineFeedback()
    {
        photonView.RPC(nameof(ClearArtifactExamineBeamTargetRpc), RpcTarget.All);
    }

    void RefreshClosestCollectible()
    {
        Treasure nextTreasure = null;
        ShipWreck nextWreck = null;
        DroppedCargoCrate nextDroppedCargo = null;
        float bestDistance = float.MaxValue;
        Vector2 tipPosition = GetShipTipPosition();

        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        foreach (Treasure treasure in treasures)
        {
            if (treasure == null || treasure.isBeingCollected)
                continue;

            float distance = GetDistanceFromTipToCollider(treasure.GetComponent<Collider2D>(), treasure.transform.position, tipPosition);
            if (distance > GetTreasureCollectRange(treasure))
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                nextTreasure = treasure;
                nextWreck = null;
                nextDroppedCargo = null;
            }
        }

        ShipWreck[] wrecks = FindObjectsByType<ShipWreck>(FindObjectsInactive.Exclude);
        foreach (ShipWreck wreck in wrecks)
        {
            if (wreck == null || !wreck.HasLoot || wreck.isBeingCollected)
                continue;

            float distance = GetDistanceFromTipToCollider(wreck.GetComponent<Collider2D>(), wreck.transform.position, tipPosition);
            if (distance > GetWreckCollectRange(wreck))
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                nextTreasure = null;
                nextWreck = wreck;
                nextDroppedCargo = null;
            }
        }

        DroppedCargoCrate[] droppedCrates = FindObjectsByType<DroppedCargoCrate>(FindObjectsInactive.Exclude);
        foreach (DroppedCargoCrate crate in droppedCrates)
        {
            if (crate == null || !crate.HasLoot || crate.isBeingCollected)
                continue;

            float distance = GetDistanceFromTipToCollider(crate.GetComponent<Collider2D>(), crate.transform.position, tipPosition);
            if (distance > Treasure.CollectRange)
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                nextTreasure = null;
                nextWreck = null;
                nextDroppedCargo = crate;
            }
        }

        if (currentTreasure == nextTreasure && currentWreck == nextWreck && currentDroppedCargo == nextDroppedCargo)
        {
            if (currentTreasure != null)
                currentTreasure.Highlight();
            else if (currentWreck != null)
                currentWreck.Highlight();
            else if (currentDroppedCargo != null)
                currentDroppedCargo.Highlight();

            return;
        }

        ClearCurrentHighlight();

        currentTreasure = nextTreasure;
        currentWreck = nextWreck;
        currentDroppedCargo = nextDroppedCargo;

        if (currentTreasure != null)
        {
            currentTreasure.Highlight();
        }
        else if (currentWreck != null)
        {
            currentWreck.Highlight();
        }
        else if (currentDroppedCargo != null)
        {
            currentDroppedCargo.Highlight();
        }
    }

    bool HasLockedCollectibleTarget()
    {
        if (!isCollecting)
            return false;

        return currentTreasure != null || currentWreck != null || currentDroppedCargo != null;
    }

    public bool HasUseActionAvailable()
    {
        return ResolveUseActionType() != UseActionType.None;
    }

    UseActionType ResolveUseActionType()
    {
        if (!photonView.IsMine)
            return UseActionType.None;

        if (ShouldDisableUseControllerForActor())
            return UseActionType.None;

        bool astronautMode = IsAstronautMode();
        UseActionType activeProgressAction = ResolveActiveProgressUseActionType(astronautMode);
        if (activeProgressAction != UseActionType.None)
            return activeProgressAction;

        if (!astronautMode)
        {
            AvengerWarBase avengerBase = AvengerWarBase.FindClosestUsable(transform.position);
            if (avengerBase != null)
                return UseActionType.AvengerBoard;

            PlayerRepairDocking repairDocking = GetComponent<PlayerRepairDocking>();
            RepairBay repairBay = RepairBay.FindClosestUsable(transform.position);
            SpaceFactory spaceFactory = SpaceFactory.FindClosestUsable(transform.position);
            ScienceStation scienceStation = ScienceStation.FindClosestUsable(transform.position);
            if (repairDocking != null && (repairDocking.IsBusy || repairBay != null || spaceFactory != null || scienceStation != null))
                return UseActionType.Land;
        }

        ArtifactAsteroid artifact = ResolveUsableArtifactAsteroid();
        if (artifact != null)
            return UseActionType.Examine;

        if (!astronautMode)
        {
            PlayerHealth playerHealth = GetComponent<PlayerHealth>();
            if (BisonIndustrialPlotController.CanDropHaul(playerHealth))
                return UseActionType.Drop;
            if (BisonIndustrialPlotController.CanStartHaul(playerHealth))
                return UseActionType.Haul;
            if (InvaderInvasionPlotController.TryGetUseAction(playerHealth, out InvaderPlotUseAction invaderAction))
                return ConvertInvaderUseAction(invaderAction);
        }

        ExtractionZone extractionZone = ResolveUsableExtractionZone();
        if (extractionZone != null)
        {
            if (CanUseExtractionNow(extractionZone))
                return extractionZone.IsActive ? UseActionType.Escape : UseActionType.Activate;

            if (!CanCollectWhileExtractionUnavailable(extractionZone))
                return UseActionType.None;
        }

        if (astronautMode)
            return UseActionType.None;

        return ResolveCollectibleUseActionType();
    }

    UseActionType ResolveCollectibleUseActionType()
    {
        if (!HasUsableCollectibleTarget())
            return UseActionType.None;

        return CanStoreCurrentCollectible() ? UseActionType.Collect : UseActionType.None;
    }

    UseActionType ResolveActiveProgressUseActionType(bool astronautMode)
    {
        if (isActivatingExtraction)
            return UseActionType.Activate;

        if (isExaminingArtifact)
            return UseActionType.Examine;

        if (isCollecting)
            return UseActionType.Collect;

        if (!astronautMode && BisonIndustrialPlotController.IsHaulChargeInProgress(GetComponent<PlayerHealth>()))
            return UseActionType.Haul;

        if (!astronautMode &&
            InvaderInvasionPlotController.TryGetUseChargeProgress(GetComponent<PlayerHealth>(), out _, out InvaderPlotUseAction invaderAction))
        {
            return ConvertInvaderUseAction(invaderAction);
        }

        return UseActionType.None;
    }

    UseActionType ConvertInvaderUseAction(InvaderPlotUseAction action)
    {
        switch (action)
        {
            case InvaderPlotUseAction.Contact:
                return UseActionType.InvaderContact;
            case InvaderPlotUseAction.Stabilize:
                return UseActionType.InvaderStabilize;
            case InvaderPlotUseAction.Sync:
                return UseActionType.InvaderSync;
            default:
                return UseActionType.None;
        }
    }

    ExtractionZone ResolveUsableExtractionZone()
    {
        if (currentExtraction != null && IsExtractionStillUsable(currentExtraction))
            return currentExtraction;

        return ResolveNearbyExtractionZone();
    }

    ArtifactAsteroid ResolveUsableArtifactAsteroid()
    {
        if (isExaminingArtifact && IsArtifactStillUsable(currentArtifactAsteroid))
            return currentArtifactAsteroid;

        if (!PlayerHasAlienTransmitter(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer))
            return null;

        ArtifactAsteroid artifact = ArtifactAsteroid.FindClosestInactiveUsable(GetShipTipPosition());
        if (artifact == null || artifact.IsActive)
            return null;

        return artifact;
    }

    bool IsArtifactStillUsable(ArtifactAsteroid artifact)
    {
        if (artifact == null || artifact.IsActive)
            return false;

        if (!PlayerHasAlienTransmitter(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer))
            return false;

        return artifact.GetInteractionDistanceToPoint(GetShipTipPosition()) <= ArtifactAsteroid.ExamineRange + ArtifactExamineKeepAliveDistance;
    }

    bool PlayerHasAlienTransmitter(Photon.Realtime.Player player)
    {
        string[] slots = PlayerProfileService.GetPlayerShipInventorySlots(player);
        int capacity = PlayerProfileService.GetPlayerShipInventoryCapacity(player);
        int limit = Mathf.Clamp(capacity, 0, slots != null ? slots.Length : 0);
        for (int i = 0; i < limit; i++)
        {
            if (string.Equals(slots[i], InventoryItemCatalog.AlienTransmitterId, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    bool CanStoreCurrentCollectible()
    {
        if (!PlayerProfileService.HasInstance)
            return false;

        if (currentTreasure != null && InventoryItemCatalog.IsRandomLootWreckItem(currentTreasure.itemId))
        {
            return PlayerProfileService.Instance.HasFreeShipInventorySlot() ||
                   PlayerProfileService.PlayerHasFreeUtilityEquipmentSlot(PhotonNetwork.LocalPlayer) ||
                   PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.AtlasId);
        }

        if (currentTreasure != null)
            return CanStoreTreasure(currentTreasure);

        if (currentDroppedCargo != null)
        {
            string storedItemId = InventoryItemCatalog.IsBlueprintScrapContainerItem(currentDroppedCargo.StoredItemId)
                ? InventoryItemCatalog.BlueprintScrapId
                : currentDroppedCargo.StoredItemId;
            return PlayerProfileService.Instance.HasFreeShipInventorySlot(storedItemId) || CanAtlasAutoReplaceCargo(storedItemId);
        }

        return PlayerProfileService.Instance.HasFreeShipInventorySlot() || CanAtlasAutoReplaceCargo(null);
    }

    bool CanStoreTreasure(Treasure treasure)
    {
        if (treasure == null || !PlayerProfileService.HasInstance)
            return false;

        if (IsTreasureTemporarilyLocked(treasure))
            return false;

        if (InventoryItemCatalog.IsRandomLootWreckItem(treasure.itemId))
        {
            return PlayerProfileService.Instance.HasFreeShipInventorySlot() ||
                   PlayerProfileService.PlayerHasFreeUtilityEquipmentSlot(PhotonNetwork.LocalPlayer) ||
                   PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.AtlasId);
        }

        if (InventoryItemCatalog.IsBlueprintScrapContainerItem(treasure.itemId))
            return PlayerProfileService.Instance.HasFreeShipInventorySlot(InventoryItemCatalog.BlueprintScrapId) ||
                   CanAtlasAutoReplaceCargo(InventoryItemCatalog.BlueprintScrapId);

        return PlayerProfileService.Instance.HasFreeShipInventorySlot(treasure.itemId) || CanAtlasAutoReplaceCargo(treasure.itemId);
    }

    bool HasUsableCollectibleTarget()
    {
        return IsTreasureUsable(currentTreasure) ||
               IsWreckUsable(currentWreck) ||
               IsDroppedCargoUsable(currentDroppedCargo);
    }

    bool IsTreasureUsable(Treasure treasure)
    {
        return treasure != null &&
               !IsTreasureTemporarilyLocked(treasure) &&
               (!treasure.isBeingCollected || (isCollecting && currentTreasure == treasure)) &&
               IsTreasureInCollectRange(treasure);
    }

    bool IsTreasureTemporarilyLocked(Treasure treasure)
    {
        return treasure != null &&
               InventoryItemCatalog.IsAlienSecretItem(treasure.itemId) &&
               RiftWardenLockdownField.IsTreasureLocked(treasure);
    }

    bool IsWreckUsable(ShipWreck wreck)
    {
        return wreck != null &&
               wreck.HasLoot &&
               (!wreck.isBeingCollected || (isCollecting && currentWreck == wreck)) &&
               IsWreckInCollectRange(wreck);
    }

    bool IsDroppedCargoUsable(DroppedCargoCrate crate)
    {
        return crate != null &&
               crate.HasLoot &&
               (!crate.isBeingCollected || (isCollecting && currentDroppedCargo == crate)) &&
               IsDroppedCargoInCollectRange(crate);
    }

    void UpdateUseButtonAvailability()
    {
        UseActionType actionType = ResolveUseActionType();
        SetUseButtonState(actionType != UseActionType.None, actionType);
    }

    void SetUseButtonState(bool available, UseActionType actionType)
    {
        if (collectButton == null)
            return;

        if (useButtonVisual == null)
            useButtonVisual = collectButton.GetComponent<UseButtonVisualController>();

        string label = GetUseButtonLabel(actionType);
        if (useButtonVisual != null)
        {
            useButtonVisual.SetLabel(label);
            useButtonVisual.SetAvailable(available);
            if (!IsUseProgressActive())
                useButtonVisual.SetProgress(0f, false);
        }
        else
        {
            SetUseButtonText(label);
        }
    }

    string GetUseButtonLabel(UseActionType actionType)
    {
        switch (actionType)
        {
            case UseActionType.Collect:
                return "COLLECT";
            case UseActionType.Land:
            case UseActionType.AvengerBoard:
                return "LAND";
            case UseActionType.Examine:
                return "EXAMINE";
            case UseActionType.Activate:
                return "ACTIVATE";
            case UseActionType.Escape:
                return "ESCAPE";
            case UseActionType.Haul:
                return "HAUL";
            case UseActionType.Drop:
                return "DROP";
            case UseActionType.InvaderContact:
                return "CONTACT";
            case UseActionType.InvaderStabilize:
                return "STABILIZE";
            case UseActionType.InvaderSync:
                return "SYNC";
            default:
                return string.Empty;
        }
    }

    void SetUseButtonText(string label)
    {
        TMP_Text text = collectButton != null ? collectButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (text != null && text.text != label)
            text.text = label;
    }

    bool IsUseProgressActive()
    {
        return ResolveActiveProgressUseActionType(IsAstronautMode()) != UseActionType.None;
    }

    void SetUseProgress(float progress, bool visible)
    {
        if (useButtonVisual == null && collectButton != null)
            useButtonVisual = collectButton.GetComponent<UseButtonVisualController>();

        if (useButtonVisual != null)
            useButtonVisual.SetProgress(progress, visible);
    }

    void UpdateHaulChargeUseProgress()
    {
        if (!photonView.IsMine)
            return;

        if (BisonIndustrialPlotController.TryGetHaulChargeProgress(GetComponent<PlayerHealth>(), out float progress))
            SetUseProgress(progress, true);

        if (InvaderInvasionPlotController.TryGetUseChargeProgress(GetComponent<PlayerHealth>(), out float invaderProgress, out _))
            SetUseProgress(invaderProgress, true);
    }

    bool IsPendingActivatedExtraction(ExtractionZone extractionZone)
    {
        if (pendingActivatedExtraction == null)
            return false;

        bool sameZone = pendingActivatedExtraction == extractionZone;
        bool expired = Time.time > pendingActivatedExtractionUntil;
        if (!sameZone || expired || pendingActivatedExtraction.IsActive || pendingActivatedExtraction.IsTransitioning)
        {
            pendingActivatedExtraction = null;
            pendingActivatedExtractionUntil = 0f;
            return false;
        }

        return true;
    }

    void ForceResolveCollectibleAtUsePress()
    {
        Vector2 tipPosition = GetShipTipPosition();
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(tipPosition, GetCollectibleSearchRange(Treasure.CollectRange + 0.55f), out Collider2D[] hits);
        Treasure nextTreasure = null;
        ShipWreck nextWreck = null;
        DroppedCargoCrate nextDroppedCargo = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            Treasure treasure = hit.GetComponent<Treasure>() ?? hit.GetComponentInParent<Treasure>();
            if (treasure != null)
            {
                if (treasure.isBeingCollected)
                    continue;

                float distance = GetDistanceFromTipToCollider(treasure.GetComponent<Collider2D>(), treasure.transform.position, tipPosition);
                if (distance <= GetTreasureCollectRange(treasure) && distance < bestDistance)
                {
                    bestDistance = distance;
                    nextTreasure = treasure;
                    nextWreck = null;
                    nextDroppedCargo = null;
                }

                continue;
            }

            ShipWreck wreck = hit.GetComponent<ShipWreck>() ?? hit.GetComponentInParent<ShipWreck>();
            if (wreck != null && wreck.HasLoot)
            {
                if (wreck.isBeingCollected)
                    continue;

                float distance = GetDistanceFromTipToCollider(wreck.GetComponent<Collider2D>(), wreck.transform.position, tipPosition);
                if (distance <= GetWreckCollectRange(wreck) && distance < bestDistance)
                {
                    bestDistance = distance;
                    nextTreasure = null;
                    nextWreck = wreck;
                    nextDroppedCargo = null;
                }

                continue;
            }

            DroppedCargoCrate crate = hit.GetComponent<DroppedCargoCrate>() ?? hit.GetComponentInParent<DroppedCargoCrate>();
            if (crate != null && crate.HasLoot)
            {
                if (crate.isBeingCollected)
                    continue;

                float distance = GetDistanceFromTipToCollider(crate.GetComponent<Collider2D>(), crate.transform.position, tipPosition);
                if (distance <= Treasure.CollectRange && distance < bestDistance)
                {
                    bestDistance = distance;
                    nextTreasure = null;
                    nextWreck = null;
                    nextDroppedCargo = crate;
                }
            }
        }

        if (nextTreasure == null && nextWreck == null && nextDroppedCargo == null)
            return;

        ClearCurrentHighlight();
        currentTreasure = nextTreasure;
        currentWreck = nextWreck;
        currentDroppedCargo = nextDroppedCargo;

        if (currentTreasure != null)
            currentTreasure.Highlight();
        else if (currentWreck != null)
            currentWreck.Highlight();
        else if (currentDroppedCargo != null)
            currentDroppedCargo.Highlight();
    }

    void ClearCurrentHighlight()
    {
        if (currentTreasure != null)
            currentTreasure.Unhighlight();

        if (currentWreck != null)
            currentWreck.Unhighlight();

        if (currentDroppedCargo != null)
            currentDroppedCargo.Unhighlight();
    }

    bool IsTreasureInCollectRange(Treasure treasure, bool useKeepAliveRange = false)
    {
        if (treasure == null)
            return false;

        return GetDistanceFromTipToCollider(treasure.GetComponent<Collider2D>(), treasure.transform.position, GetShipTipPosition()) <= GetTreasureCollectRange(treasure, useKeepAliveRange);
    }

    bool IsWreckInCollectRange(ShipWreck wreck, bool useKeepAliveRange = false)
    {
        if (wreck == null || !wreck.HasLoot)
            return false;

        return GetDistanceFromTipToCollider(wreck.GetComponent<Collider2D>(), wreck.transform.position, GetShipTipPosition()) <= GetWreckCollectRange(wreck, useKeepAliveRange);
    }

    bool IsDroppedCargoInCollectRange(DroppedCargoCrate crate, bool useKeepAliveRange = false)
    {
        if (crate == null || !crate.HasLoot)
            return false;

        return GetDistanceFromTipToCollider(crate.GetComponent<Collider2D>(), crate.transform.position, GetShipTipPosition()) <= GetCollectRange(Treasure.CollectRange, useKeepAliveRange);
    }

    float GetCollectRange(float baseRange, bool useKeepAliveRange)
    {
        if (!useKeepAliveRange)
            return baseRange;

        return baseRange * (1f + RoomSettings.GetCollectKeepAliveRangeBonusPercent() / 100f);
    }

    float GetTreasureCollectRange(Treasure treasure, bool useKeepAliveRange = false)
    {
        float baseRange = Treasure.CollectRange;
        if (treasure != null && InventoryItemCatalog.IsRandomLootWreckItem(treasure.itemId))
            baseRange = ApplySalvageMagnetRange(baseRange);

        return GetCollectRange(baseRange, useKeepAliveRange);
    }

    float GetWreckCollectRange(ShipWreck wreck, bool useKeepAliveRange = false)
    {
        float baseRange = wreck != null && wreck.SourceShipSkinIndex < 0 ? Treasure.CollectRange + 0.45f : Treasure.CollectRange;
        return GetCollectRange(ApplySalvageMagnetRange(baseRange), useKeepAliveRange);
    }

    float GetCollectibleSearchRange(float baseRange)
    {
        return HasSalvageMagnetArrayEquipped() ? baseRange * 2f : baseRange;
    }

    float ApplySalvageMagnetRange(float baseRange)
    {
        return HasSalvageMagnetArrayEquipped() ? baseRange * 2f : baseRange;
    }

    bool HasSalvageMagnetArrayEquipped()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        return InventoryItemCatalog.HasEquippedItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.SalvageMagnetArrayId);
    }

    float GetDistanceFromTipToCollider(Collider2D collider, Vector2 fallbackPosition, Vector2 tipPosition)
    {
        if (collider != null)
        {
            Vector2 closestPoint = collider.ClosestPoint(tipPosition);
            return Vector2.Distance(tipPosition, closestPoint);
        }

        return Vector2.Distance(tipPosition, fallbackPosition);
    }

    Vector2 GetShipTipPosition()
    {
        float forwardOffset = 0.55f;
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            forwardOffset = Mathf.Max(0.4f, renderer.bounds.extents.y * 0.9f);
        }

        return (Vector2)transform.position + (Vector2)transform.up * forwardOffset;
    }

    void EnsureCollectButtonPositioned()
    {
        if (collectButton == null)
            return;

        RectTransform rect = collectButton.GetComponent<RectTransform>();
        if (rect == null || rect == positionedCollectButtonRect)
            return;

        rect.anchoredPosition = CollectButtonRoundPosition;
        positionedCollectButtonRect = rect;
    }

    void SetupBeam()
    {
        Transform existing = transform.Find("TreasureBeam");
        GameObject beamObject = existing != null ? existing.gameObject : new GameObject("TreasureBeam");
        beamObject.transform.SetParent(transform, false);

        collectionBeam = beamObject.GetComponent<LineRenderer>();
        if (collectionBeam == null)
        {
            collectionBeam = beamObject.AddComponent<LineRenderer>();
        }

        collectionBeam.useWorldSpace = true;
        collectionBeam.alignment = LineAlignment.View;
        collectionBeam.positionCount = BeamPointCount;
        collectionBeam.widthMultiplier = BeamWidth;
        collectionBeam.startWidth = BeamWidth;
        collectionBeam.endWidth = BeamWidth * 0.55f;
        collectionBeam.numCapVertices = 12;
        collectionBeam.numCornerVertices = 10;
        collectionBeam.material = new Material(Shader.Find("Sprites/Default"));
        collectionBeam.colorGradient = BuildCollectionBeamGradient(1f);
        collectionBeam.widthCurve = BuildCollectionBeamWidthCurve();
        collectionBeam.textureMode = LineTextureMode.Stretch;

        SpriteRenderer referenceRenderer = GetComponent<SpriteRenderer>();
        if (referenceRenderer != null)
        {
            collectionBeam.sortingLayerID = referenceRenderer.sortingLayerID;
            collectionBeam.sortingOrder = referenceRenderer.sortingOrder + 36;
        }
        else
        {
            collectionBeam.sortingLayerName = "Default";
            collectionBeam.sortingOrder = 50;
        }

        collectionBeam.enabled = false;
    }

    void SetupArtifactExamineBeam()
    {
        Transform existing = transform.Find("ArtifactExamineBeam");
        GameObject beamObject = existing != null ? existing.gameObject : new GameObject("ArtifactExamineBeam");
        beamObject.transform.SetParent(transform, false);

        artifactExamineBeam = beamObject.GetComponent<LineRenderer>();
        if (artifactExamineBeam == null)
            artifactExamineBeam = beamObject.AddComponent<LineRenderer>();

        artifactExamineBeam.useWorldSpace = true;
        artifactExamineBeam.alignment = LineAlignment.View;
        artifactExamineBeam.positionCount = BeamPointCount;
        artifactExamineBeam.widthMultiplier = ArtifactBeamWidth;
        artifactExamineBeam.startWidth = ArtifactBeamWidth;
        artifactExamineBeam.endWidth = ArtifactBeamWidth * 0.62f;
        artifactExamineBeam.numCapVertices = 12;
        artifactExamineBeam.numCornerVertices = 10;
        artifactExamineBeam.material = new Material(Shader.Find("Sprites/Default"));
        artifactExamineBeam.colorGradient = BuildArtifactBeamGradient(1f);
        artifactExamineBeam.widthCurve = BuildCollectionBeamWidthCurve();
        artifactExamineBeam.textureMode = LineTextureMode.Stretch;

        SpriteRenderer referenceRenderer = GetComponent<SpriteRenderer>();
        if (referenceRenderer != null)
        {
            artifactExamineBeam.sortingLayerID = referenceRenderer.sortingLayerID;
            artifactExamineBeam.sortingOrder = referenceRenderer.sortingOrder + 38;
        }
        else
        {
            artifactExamineBeam.sortingLayerName = "Default";
            artifactExamineBeam.sortingOrder = 52;
        }

        artifactExamineBeam.enabled = false;
    }

    void UpdateCollectionBeam()
    {
        if (collectionBeam == null)
            return;

        bool shouldShow = beamActive && (currentTreasure != null || currentWreck != null || currentDroppedCargo != null);
        if (shouldShow && photonView.IsMine)
        {
            bool useKeepAliveRange = isCollecting;
            shouldShow = currentTreasure != null
                ? IsTreasureInCollectRange(currentTreasure, useKeepAliveRange)
                : currentWreck != null
                    ? IsWreckInCollectRange(currentWreck, useKeepAliveRange)
                    : IsDroppedCargoInCollectRange(currentDroppedCargo, useKeepAliveRange);
        }

        collectionBeam.enabled = shouldShow;

        if (!shouldShow)
            return;

        Vector2 start = GetShipTipPosition();
        Vector2 end = GetCollectibleBeamTarget(start);
        Vector2 delta = end - start;
        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : (Vector2)transform.up;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float pulse = Mathf.Sin(Time.time * 14f) * 0.5f + 0.5f;
        float alpha = Mathf.Lerp(0.72f, 1f, pulse);
        collectionBeam.colorGradient = BuildCollectionBeamGradient(alpha);
        collectionBeam.widthMultiplier = Mathf.Lerp(BeamWidth * 0.72f, BeamWidth * 1.3f, pulse);

        for (int i = 0; i < collectionBeam.positionCount; i++)
        {
            float t = i / (float)(collectionBeam.positionCount - 1);
            Vector2 point = Vector2.Lerp(start, end, t);
            float taper = Mathf.Sin(t * Mathf.PI);
            float waveA = Mathf.Sin((t * Mathf.PI * 5f) + Time.time * BeamJitterFrequency);
            float waveB = Mathf.Sin((t * Mathf.PI * 11f) - Time.time * 13f) * 0.45f;
            float jitter = (waveA + waveB) * BeamJitterAmplitude * taper;
            point += perpendicular * jitter;
            collectionBeam.SetPosition(i, new Vector3(point.x, point.y, BeamZOffset));
        }
    }

    void UpdateArtifactExamineBeam()
    {
        if (artifactExamineBeam == null)
            return;

        bool shouldShow = artifactBeamActive && currentArtifactAsteroid != null;
        if (shouldShow && photonView.IsMine)
            shouldShow = IsArtifactStillUsable(currentArtifactAsteroid);

        artifactExamineBeam.enabled = shouldShow;
        if (!shouldShow)
            return;

        Vector2 start = GetShipTipPosition();
        Vector2 end = currentArtifactAsteroid != null ? (Vector2)currentArtifactAsteroid.BeamTarget : start;
        Vector2 delta = end - start;
        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : (Vector2)transform.up;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float pulse = Mathf.Sin(Time.time * 18f) * 0.5f + 0.5f;
        float alpha = Mathf.Lerp(0.62f, 1f, pulse);
        artifactExamineBeam.colorGradient = BuildArtifactBeamGradient(alpha);
        artifactExamineBeam.widthMultiplier = Mathf.Lerp(ArtifactBeamWidth * 0.65f, ArtifactBeamWidth * 1.45f, pulse);

        for (int i = 0; i < artifactExamineBeam.positionCount; i++)
        {
            float t = i / (float)(artifactExamineBeam.positionCount - 1);
            Vector2 point = Vector2.Lerp(start, end, t);
            float taper = Mathf.Sin(t * Mathf.PI);
            float waveA = Mathf.Sin((t * Mathf.PI * 7f) + Time.time * 20f);
            float waveB = Mathf.Sin((t * Mathf.PI * 13f) - Time.time * 16f) * 0.5f;
            float jitter = (waveA + waveB) * BeamJitterAmplitude * 0.78f * taper;
            point += perpendicular * jitter;
            artifactExamineBeam.SetPosition(i, new Vector3(point.x, point.y, BeamZOffset - 0.03f));
        }
    }

    Gradient BuildCollectionBeamGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.96f, 1f, 0.86f), 0f),
                new GradientColorKey(new Color(0.28f, 1f, 0.66f), 0.38f),
                new GradientColorKey(new Color(0.1f, 0.74f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f * alpha, 0f),
                new GradientAlphaKey(0.72f * alpha, 0.55f),
                new GradientAlphaKey(0.18f * alpha, 1f)
            });
        return gradient;
    }

    Gradient BuildArtifactBeamGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.08f, 0.46f, 1f), 0f),
                new GradientColorKey(new Color(0.48f, 0.92f, 1f), 0.46f),
                new GradientColorKey(new Color(0.12f, 0.18f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.86f * alpha, 0f),
                new GradientAlphaKey(1f * alpha, 0.5f),
                new GradientAlphaKey(0.24f * alpha, 1f)
            });
        return gradient;
    }

    AnimationCurve BuildCollectionBeamWidthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.62f),
            new Keyframe(0.18f, 1.2f),
            new Keyframe(0.58f, 0.82f),
            new Keyframe(1f, 0.22f));
    }

    Vector2 GetCollectibleBeamTarget(Vector2 start)
    {
        Collider2D collider = currentTreasure != null
            ? currentTreasure.GetComponent<Collider2D>()
            : currentWreck != null ? currentWreck.GetComponent<Collider2D>() : currentDroppedCargo != null ? currentDroppedCargo.GetComponent<Collider2D>() : null;

        Vector2 fallbackPosition = currentTreasure != null
            ? currentTreasure.transform.position
            : currentWreck != null ? currentWreck.transform.position : currentDroppedCargo != null ? currentDroppedCargo.transform.position : start;

        if (collider != null)
        {
            return collider.ClosestPoint(start);
        }

        return fallbackPosition;
    }

    void SetBeamEnabled(bool enabled)
    {
        beamActive = enabled;
        if (collectionBeam != null)
            collectionBeam.enabled = enabled;
    }

    void SetArtifactBeamEnabled(bool enabled)
    {
        artifactBeamActive = enabled;
        if (artifactExamineBeam != null)
            artifactExamineBeam.enabled = enabled;
    }

    void OnDestroy()
    {
        if (collectButtonBindingOwner == this)
            collectButtonBindingOwner = null;

        ReleaseMasterTreasureReservation(true);
        SetArtifactBeamEnabled(false);
        ClearCurrentHighlight();
        StopLocalDrillingLoop();
    }

    void OnDisable()
    {
        if (collectButtonBindingOwner == this)
            collectButtonBindingOwner = null;

        SetArtifactBeamEnabled(false);
    }

    public void DisableForEnemyAstronaut()
    {
        if (collectButtonBindingOwner == this)
            collectButtonBindingOwner = null;

        collectButtonHooked = false;
        enabled = false;
    }

    bool ShouldDisableUseControllerForActor()
    {
        ActorIdentity identity = ActorIdentity.Ensure(gameObject);
        if (identity == null)
            return false;

        return !identity.CanUsePlayerUseButton &&
               (identity.IsEnemy || identity.IsWreck || identity.Form == ActorForm.Deployable);
    }

    [PunRPC]
    void RequestReserveTreasureCollection(int viewID, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!IsRequestFromOwner(messageInfo))
            return;

        bool accepted = false;
        PhotonView pv = PhotonView.Find(viewID);
        Treasure treasure = pv != null ? pv.GetComponent<Treasure>() : null;
        if (treasure != null && !treasure.isBeingCollected && !IsTreasureTemporarilyLocked(treasure) && IsTreasureInCollectRange(treasure, true))
        {
            if (!ReservedTreasureCollections.TryGetValue(viewID, out int reservedActor) ||
                reservedActor == photonView.OwnerActorNr)
            {
                ReservedTreasureCollections[viewID] = photonView.OwnerActorNr;
                accepted = true;
            }
        }

        if (photonView != null && photonView.Owner != null)
            photonView.RPC(nameof(ConfirmTreasureCollectionReservationRpc), photonView.Owner, viewID, accepted);
    }

    [PunRPC]
    void ConfirmTreasureCollectionReservationRpc(int viewID, bool accepted)
    {
        if (!photonView.IsMine)
            return;

        if (pendingTreasureReservationViewId != viewID)
            return;

        if (!accepted)
        {
            CancelActiveCollection();
            return;
        }

        PhotonView targetView = PhotonView.Find(viewID);
        Treasure treasure = targetView != null ? targetView.GetComponent<Treasure>() : null;
        if (treasure == null || !CanStoreTreasure(treasure))
        {
            CancelActiveCollection();
            return;
        }

        BeginTreasureCollection(treasure, viewID);
    }

    [PunRPC]
    void RequestReleaseTreasureCollection(int viewID, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || !IsRequestFromOwner(messageInfo))
            return;

        if (ReservedTreasureCollections.TryGetValue(viewID, out int reservedActor) &&
            reservedActor == photonView.OwnerActorNr)
        {
            ReservedTreasureCollections.Remove(viewID);
        }
    }

    [PunRPC]
    void RequestDestroyTreasure(int viewID, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!IsRequestFromOwner(messageInfo))
            return;

        if (ReservedTreasureCollections.TryGetValue(viewID, out int reservedActor) &&
            reservedActor != photonView.OwnerActorNr)
        {
            if (photonView != null && photonView.Owner != null)
                photonView.RPC(nameof(ForceCancelCollectibleUseRpc), photonView.Owner, viewID);

            return;
        }

        PhotonView pv = PhotonView.Find(viewID);
        if (pv == null)
        {
            ReservedTreasureCollections.Remove(viewID);
            return;
        }

        if (pv != null)
        {
            ReservedTreasureCollections.Remove(viewID);
            SpaceTrapTarget.DetonateIfArmed(viewID, photonView != null ? photonView.ViewID : 0);
            NotifyPirateBasesAboutCollectedTarget(viewID);
            PhotonNetwork.Destroy(pv.gameObject);
        }
    }

    [PunRPC]
    void ForceCancelCollectibleUseRpc(int viewID)
    {
        if (!photonView.IsMine)
            return;

        int currentViewId = 0;
        if (currentTreasure != null)
        {
            PhotonView treasureView = currentTreasure.GetComponent<PhotonView>();
            currentViewId = treasureView != null ? treasureView.ViewID : 0;
        }

        if (viewID <= 0 || currentViewId == viewID || pendingTreasureReservationViewId == viewID || activeTreasureReservationViewId == viewID)
            CancelActiveCollection();
    }

    void NotifyPirateBasesAboutCollectedTarget(int collectibleViewId)
    {
        int collectorViewId = photonView != null ? photonView.ViewID : 0;
        EnemyPirateBaseBehavior.NotifyCollectibleCollected(collectibleViewId, collectorViewId);
    }

    bool TryQueueNovaTreasureOnMaster(Treasure treasure, Vector2 tipPosition, float maxRange, HashSet<int> queuedViewIds, List<NovaPendingCollectible> pendingCollectibles)
    {
        if (treasure == null || treasure.photonView == null || treasure.isBeingCollected)
            return false;

        int viewId = treasure.photonView.ViewID;
        if (viewId <= 0 || queuedViewIds.Contains(viewId))
            return false;

        if (GetDistanceFromTipToCollider(treasure.GetComponent<Collider2D>(), treasure.transform.position, tipPosition) > maxRange)
            return false;

        if (InventoryItemCatalog.IsRandomLootWreckItem(treasure.itemId))
            return TryQueueNovaRandomLootWreckOnMaster(treasure, queuedViewIds, pendingCollectibles);

        if (ReservedTreasureCollections.TryGetValue(viewId, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return false;

        string collectibleItemId = InventoryItemCatalog.ResolveAlienSecretItemId(treasure.itemId, treasure.visualVariantIndex);
        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, collectibleItemId))
            return false;

        queuedViewIds.Add(viewId);
        treasure.isBeingCollected = true;
        ReservedTreasureCollections[viewId] = photonView.OwnerActorNr;
        pendingCollectibles.Add(new NovaPendingCollectible
        {
            Kind = NovaPendingCollectibleKind.Treasure,
            ViewId = viewId,
            ItemId = collectibleItemId
        });
        return true;
    }

    bool TryQueueNovaRandomLootWreckOnMaster(Treasure treasure, HashSet<int> queuedViewIds, List<NovaPendingCollectible> pendingCollectibles)
    {
        if (treasure == null || treasure.photonView == null)
            return false;

        int viewId = treasure.photonView.ViewID;
        if (viewId <= 0 || queuedViewIds.Contains(viewId))
            return false;

        if (ReservedRandomLootWrecks.TryGetValue(viewId, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return false;

        string rewardItemId = RollRandomLootWreckReward();
        if (string.IsNullOrWhiteSpace(rewardItemId))
            return false;

        bool canStoreReward = PlayerProfileService.PlayerCanStoreShipItemOrAtlasAutoReplace(photonView.Owner, rewardItemId) ||
                              PlayerProfileService.PlayerHasFreeEquipmentSlotForItem(photonView.Owner, rewardItemId);
        if (!canStoreReward)
            return false;

        queuedViewIds.Add(viewId);
        treasure.isBeingCollected = true;
        ReservedRandomLootWrecks[viewId] = photonView.OwnerActorNr;
        pendingCollectibles.Add(new NovaPendingCollectible
        {
            Kind = NovaPendingCollectibleKind.RandomLootWreck,
            ViewId = viewId,
            ItemId = rewardItemId
        });
        return true;
    }

    bool TryQueueNovaWreckOnMaster(ShipWreck wreck, Vector2 tipPosition, float maxRange, HashSet<int> queuedViewIds, List<NovaPendingCollectible> pendingCollectibles)
    {
        PhotonView wreckView = wreck != null ? wreck.GetComponent<PhotonView>() : null;
        if (wreck == null || wreckView == null || !wreck.HasLoot || wreck.isBeingCollected)
            return false;

        int viewId = wreckView.ViewID;
        if (viewId <= 0 || queuedViewIds.Contains(viewId))
            return false;

        if (GetDistanceFromTipToCollider(wreck.GetComponent<Collider2D>(), wreck.transform.position, tipPosition) > maxRange)
            return false;

        if (ReservedWreckLoot.TryGetValue(viewId, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return false;

        int lootIndex = wreck.GetFirstLootIndex();
        string itemId = wreck.GetLootItemAt(lootIndex);
        if (lootIndex < 0 || string.IsNullOrWhiteSpace(itemId))
            return false;

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, itemId))
            return false;

        string blueprintItemId = string.Empty;
        if (wreck.SourceShipSkinIndex < 0)
            BlueprintCatalog.TryRollWreckBlueprintDrop(itemId, wreck.SourceEnemyKindValue, out blueprintItemId);

        queuedViewIds.Add(viewId);
        wreck.isBeingCollected = true;
        ReservedWreckLoot[viewId] = photonView.OwnerActorNr;
        pendingCollectibles.Add(new NovaPendingCollectible
        {
            Kind = NovaPendingCollectibleKind.Wreck,
            ViewId = viewId,
            LootIndex = lootIndex,
            ItemId = itemId,
            BlueprintItemId = blueprintItemId ?? string.Empty
        });
        return true;
    }

    bool TryQueueNovaDroppedCargoOnMaster(DroppedCargoCrate crate, Vector2 tipPosition, float maxRange, HashSet<int> queuedViewIds, List<NovaPendingCollectible> pendingCollectibles)
    {
        PhotonView crateView = crate != null ? crate.GetComponent<PhotonView>() : null;
        if (crate == null || crateView == null || !crate.HasLoot || crate.isBeingCollected)
            return false;

        int viewId = crateView.ViewID;
        if (viewId <= 0 || queuedViewIds.Contains(viewId))
            return false;

        if (GetDistanceFromTipToCollider(crate.GetComponent<Collider2D>(), crate.transform.position, tipPosition) > maxRange)
            return false;

        if (ReservedDroppedCargoLoot.TryGetValue(viewId, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return false;

        string itemId = crate.StoredItemId;
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, itemId))
            return false;

        queuedViewIds.Add(viewId);
        crate.isBeingCollected = true;
        ReservedDroppedCargoLoot[viewId] = photonView.OwnerActorNr;
        pendingCollectibles.Add(new NovaPendingCollectible
        {
            Kind = NovaPendingCollectibleKind.DroppedCargo,
            ViewId = viewId,
            ItemId = itemId
        });
        return true;
    }

    IEnumerator ResolveNovaScavengerBurstAfterDelay(List<NovaPendingCollectible> pendingCollectibles, float delaySeconds)
    {
        float delay = Mathf.Max(0f, delaySeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!PhotonNetwork.IsMasterClient || photonView == null || pendingCollectibles == null)
            yield break;

        if (photonView.Owner == null)
        {
            ReleaseNovaPendingReservations(pendingCollectibles);
            yield break;
        }

        for (int i = 0; i < pendingCollectibles.Count; i++)
            ResolveNovaPendingCollectible(pendingCollectibles[i]);
    }

    void ResolveNovaPendingCollectible(NovaPendingCollectible pending)
    {
        if (pending == null || pending.ViewId <= 0 || photonView == null)
            return;

        if (photonView.Owner == null)
        {
            ReleaseNovaPendingReservation(pending);
            return;
        }

        switch (pending.Kind)
        {
            case NovaPendingCollectibleKind.Treasure:
                ResolveNovaPendingTreasure(pending);
                break;
            case NovaPendingCollectibleKind.RandomLootWreck:
                ResolveNovaPendingRandomLootWreck(pending);
                break;
            case NovaPendingCollectibleKind.Wreck:
                ResolveNovaPendingWreck(pending);
                break;
            case NovaPendingCollectibleKind.DroppedCargo:
                ResolveNovaPendingDroppedCargo(pending);
                break;
        }
    }

    void ReleaseNovaPendingReservations(List<NovaPendingCollectible> pendingCollectibles)
    {
        if (pendingCollectibles == null)
            return;

        for (int i = 0; i < pendingCollectibles.Count; i++)
            ReleaseNovaPendingReservation(pendingCollectibles[i]);
    }

    void ReleaseNovaPendingReservation(NovaPendingCollectible pending)
    {
        if (pending == null || pending.ViewId <= 0)
            return;

        PhotonView targetView = PhotonView.Find(pending.ViewId);
        switch (pending.Kind)
        {
            case NovaPendingCollectibleKind.Treasure:
                ReleaseNovaTreasureReservation(pending.ViewId, targetView != null ? targetView.GetComponent<Treasure>() : null);
                break;
            case NovaPendingCollectibleKind.RandomLootWreck:
                ReleaseNovaRandomLootWreckReservation(pending.ViewId, targetView != null ? targetView.GetComponent<Treasure>() : null);
                break;
            case NovaPendingCollectibleKind.Wreck:
                ReleaseNovaWreckReservation(pending.ViewId, targetView != null ? targetView.GetComponent<ShipWreck>() : null);
                break;
            case NovaPendingCollectibleKind.DroppedCargo:
                ReleaseNovaDroppedCargoReservation(pending.ViewId, targetView != null ? targetView.GetComponent<DroppedCargoCrate>() : null);
                break;
        }
    }

    void ResolveNovaPendingTreasure(NovaPendingCollectible pending)
    {
        if (!IsReservationOwnedByCurrentPlayer(ReservedTreasureCollections, pending.ViewId))
            return;

        PhotonView targetView = PhotonView.Find(pending.ViewId);
        Treasure treasure = targetView != null ? targetView.GetComponent<Treasure>() : null;
        string resolvedTreasureItemId = treasure != null
            ? InventoryItemCatalog.ResolveAlienSecretItemId(treasure.itemId, treasure.visualVariantIndex)
            : null;
        if (treasure == null || string.IsNullOrWhiteSpace(pending.ItemId) ||
            !string.Equals(resolvedTreasureItemId, pending.ItemId, System.StringComparison.Ordinal))
        {
            ReleaseNovaTreasureReservation(pending.ViewId, treasure);
            return;
        }

        photonView.RPC(nameof(ReceiveNovaTreasureRpc), photonView.Owner, pending.ViewId, pending.ItemId);
    }

    void ResolveNovaPendingRandomLootWreck(NovaPendingCollectible pending)
    {
        if (!IsReservationOwnedByCurrentPlayer(ReservedRandomLootWrecks, pending.ViewId))
            return;

        PhotonView targetView = PhotonView.Find(pending.ViewId);
        Treasure wreck = targetView != null ? targetView.GetComponent<Treasure>() : null;
        if (wreck == null || string.IsNullOrWhiteSpace(pending.ItemId) || !InventoryItemCatalog.IsRandomLootWreckItem(wreck.itemId))
        {
            ReleaseNovaRandomLootWreckReservation(pending.ViewId, wreck);
            return;
        }

        photonView.RPC(nameof(ReceivePendingRandomLootWreckRpc), photonView.Owner, pending.ViewId, pending.ItemId);
    }

    void ResolveNovaPendingWreck(NovaPendingCollectible pending)
    {
        if (!IsReservationOwnedByCurrentPlayer(ReservedWreckLoot, pending.ViewId))
            return;

        PhotonView targetView = PhotonView.Find(pending.ViewId);
        ShipWreck wreck = targetView != null ? targetView.GetComponent<ShipWreck>() : null;
        string currentItemId = wreck != null ? wreck.GetLootItemAt(pending.LootIndex) : string.Empty;
        if (wreck == null || !wreck.HasLoot || string.IsNullOrWhiteSpace(pending.ItemId) ||
            !string.Equals(currentItemId, pending.ItemId, System.StringComparison.Ordinal))
        {
            ReleaseNovaWreckReservation(pending.ViewId, wreck);
            return;
        }

        photonView.RPC(nameof(ReceivePendingWreckLootRpc), photonView.Owner, pending.ViewId, pending.LootIndex, pending.ItemId, pending.BlueprintItemId ?? string.Empty);
    }

    void ResolveNovaPendingDroppedCargo(NovaPendingCollectible pending)
    {
        if (!IsReservationOwnedByCurrentPlayer(ReservedDroppedCargoLoot, pending.ViewId))
            return;

        PhotonView targetView = PhotonView.Find(pending.ViewId);
        DroppedCargoCrate crate = targetView != null ? targetView.GetComponent<DroppedCargoCrate>() : null;
        if (crate == null || !crate.HasLoot || string.IsNullOrWhiteSpace(pending.ItemId) ||
            !string.Equals(crate.StoredItemId, pending.ItemId, System.StringComparison.Ordinal))
        {
            ReleaseNovaDroppedCargoReservation(pending.ViewId, crate);
            return;
        }

        photonView.RPC(nameof(ReceivePendingDroppedCargoLootRpc), photonView.Owner, pending.ViewId, pending.ItemId);
    }

    bool IsReservationOwnedByCurrentPlayer(Dictionary<int, int> reservations, int viewId)
    {
        return photonView != null &&
               reservations != null &&
               reservations.TryGetValue(viewId, out int reservedActor) &&
               reservedActor == photonView.OwnerActorNr;
    }

    void ReleaseNovaTreasureReservation(int viewId, Treasure treasure)
    {
        if (IsReservationOwnedByCurrentPlayer(ReservedTreasureCollections, viewId))
            ReservedTreasureCollections.Remove(viewId);

        if (treasure != null)
            treasure.isBeingCollected = false;
    }

    void ReleaseNovaRandomLootWreckReservation(int viewId, Treasure wreck)
    {
        if (IsReservationOwnedByCurrentPlayer(ReservedRandomLootWrecks, viewId))
            ReservedRandomLootWrecks.Remove(viewId);

        if (wreck != null)
            wreck.isBeingCollected = false;
    }

    void ReleaseNovaWreckReservation(int viewId, ShipWreck wreck)
    {
        if (IsReservationOwnedByCurrentPlayer(ReservedWreckLoot, viewId))
            ReservedWreckLoot.Remove(viewId);

        if (wreck != null)
            wreck.isBeingCollected = false;
    }

    void ReleaseNovaDroppedCargoReservation(int viewId, DroppedCargoCrate crate)
    {
        if (IsReservationOwnedByCurrentPlayer(ReservedDroppedCargoLoot, viewId))
            ReservedDroppedCargoLoot.Remove(viewId);

        if (crate != null)
            crate.isBeingCollected = false;
    }

    [PunRPC]
    void RequestRandomLootWreck(int viewID)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null)
            return;

        PhotonView wreckView = PhotonView.Find(viewID);
        if (wreckView == null)
            return;

        Treasure wreck = wreckView.GetComponent<Treasure>();
        if (wreck == null || !InventoryItemCatalog.IsRandomLootWreckItem(wreck.itemId))
            return;

        if (ReservedRandomLootWrecks.TryGetValue(viewID, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return;

        string rewardItemId = RollRandomLootWreckReward();
        if (string.IsNullOrWhiteSpace(rewardItemId))
            return;

        bool canStoreReward = PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, rewardItemId) ||
                              PlayerProfileService.PlayerHasFreeEquipmentSlotForItem(photonView.Owner, rewardItemId);
        if (!canStoreReward)
            return;

        ReservedRandomLootWrecks[viewID] = photonView.OwnerActorNr;
        photonView.RPC(nameof(ReceivePendingRandomLootWreckRpc), photonView.Owner, viewID, rewardItemId);
    }

    [PunRPC]
    void RequestLootWreck(int viewID, string[] unlockedBlueprintIds)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        PhotonView wreckView = PhotonView.Find(viewID);
        if (wreckView == null)
            return;

        ShipWreck wreck = wreckView.GetComponent<ShipWreck>();
        if (wreck == null || !wreck.HasLoot || (wreck.isBeingCollected && currentWreck != wreck))
            return;

        if (ReservedWreckLoot.TryGetValue(viewID, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return;

        int lootIndex = wreck.GetFirstLootIndex();
        string itemId = wreck.GetLootItemAt(lootIndex);
        if (lootIndex < 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        if (!PlayerProfileService.PlayerCanStoreShipItemOrAtlasAutoReplace(photonView.Owner, itemId))
            return;

        string blueprintItemId = string.Empty;
        if (wreck.SourceShipSkinIndex < 0)
            BlueprintCatalog.TryRollWreckBlueprintDrop(itemId, wreck.SourceEnemyKindValue, unlockedBlueprintIds, out blueprintItemId);

        ReservedWreckLoot[viewID] = photonView.OwnerActorNr;
        photonView.RPC(nameof(ReceivePendingWreckLootRpc), photonView.Owner, viewID, lootIndex, itemId, blueprintItemId ?? string.Empty);
    }

    [PunRPC]
    void RequestLootDroppedCargo(int viewID)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        PhotonView crateView = PhotonView.Find(viewID);
        if (crateView == null)
            return;

        DroppedCargoCrate crate = crateView.GetComponent<DroppedCargoCrate>();
        if (crate == null || !crate.HasLoot || (crate.isBeingCollected && currentDroppedCargo != crate))
            return;

        if (ReservedDroppedCargoLoot.TryGetValue(viewID, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return;

        string itemId = crate.StoredItemId;
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (!PlayerProfileService.PlayerCanStoreShipItemOrAtlasAutoReplace(photonView.Owner, itemId))
            return;

        ReservedDroppedCargoLoot[viewID] = photonView.OwnerActorNr;
        photonView.RPC(nameof(ReceivePendingDroppedCargoLootRpc), photonView.Owner, viewID, itemId);
    }

    string RollRandomLootWreckReward()
    {
        float roll = Random.value;
        InventoryItemCategory category;
        if (roll < 0.7f)
            return RollRandomUtilityEquipmentReward();
        else if (roll < 0.8f)
            category = InventoryItemCategory.Shield;
        else if (roll < 0.9f)
            category = InventoryItemCategory.Weapon;
        else
            category = InventoryItemCategory.Engine;

        string[] itemIds = InventoryItemCatalog.GetEquipmentItemIdsByCategory(category);
        if (itemIds == null || itemIds.Length == 0)
            return string.Empty;

        return itemIds[Random.Range(0, itemIds.Length)];
    }

    string RollRandomUtilityEquipmentReward()
    {
        List<string> itemIds = new List<string>();
        AddEquipmentItemIds(itemIds, InventoryItemCategory.Gadget);
        AddEquipmentItemIds(itemIds, InventoryItemCategory.Support);
        AddEquipmentItemIds(itemIds, InventoryItemCategory.Rescue);
        if (itemIds.Count == 0)
            return string.Empty;

        return itemIds[Random.Range(0, itemIds.Count)];
    }

    void AddEquipmentItemIds(List<string> itemIds, InventoryItemCategory category)
    {
        if (itemIds == null)
            return;

        string[] categoryItemIds = InventoryItemCatalog.GetEquipmentItemIdsByCategory(category);
        if (categoryItemIds == null)
            return;

        itemIds.AddRange(categoryItemIds);
    }

    public void AddScore(int amount)
    {
        if (amount <= 0)
            return;

        SetScoreTotal(totalScore + amount);
    }

    public void SetScoreTotal(int score)
    {
        totalScore = Mathf.Max(0, score);

        if (scoreText != null)
        {
            scoreText.text = "XP: " + totalScore;
        }

        SyncScoreProperty();
        RoundResultsTracker.RecordScore(photonView.Owner, totalScore);
    }

    [PunRPC]
    void RequestUseExtraction(int viewID, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!IsRequestFromOwner(messageInfo))
            return;

        PhotonView pv = PhotonView.Find(viewID);
        bool accepted = false;
        if (pv != null)
        {
            ExtractionZone ez = pv.GetComponent<ExtractionZone>();
            if (ez != null)
            {
                accepted = ez.TryUse(photonView);
            }
        }

        if (!accepted && photonView != null && photonView.Owner != null)
            photonView.RPC(nameof(RejectUseExtractionRpc), photonView.Owner, viewID);
    }

    bool IsRequestFromOwner(PhotonMessageInfo messageInfo)
    {
        return photonView != null &&
               photonView.Owner != null &&
               messageInfo.Sender != null &&
               messageInfo.Sender.ActorNumber == photonView.Owner.ActorNumber;
    }

    [PunRPC]
    void RejectUseExtractionRpc(int viewID)
    {
        if (!photonView.IsMine)
            return;

        if (pendingActivatedExtraction != null)
        {
            PhotonView pendingView = pendingActivatedExtraction.GetComponent<PhotonView>();
            if (pendingView == null || pendingView.ViewID == viewID)
            {
                pendingActivatedExtraction = null;
                pendingActivatedExtractionUntil = 0f;
            }
        }

        CancelExtractionActivation();
        SetUseProgress(0f, false);
        UpdateUseButtonAvailability();
    }

    void SyncScoreProperty()
    {
        if (!photonView.IsMine || !PhotonNetwork.IsConnected)
            return;

        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props[RoomSettings.ScoreKey] = totalScore;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    [PunRPC]
    void StartDrillingLoopSfx()
    {
        if (drillingAudioSource == null)
        {
            SetupDrillingAudio();
        }

        if (drillingAudioSource == null || drillingAudioSource.clip == null)
            return;

        drillingAudioSource.loop = true;
        if (!drillingAudioSource.isPlaying)
            drillingAudioSource.Play();
    }

    [PunRPC]
    void StopDrillingLoopSfx()
    {
        StopLocalDrillingLoop();
    }

    void SetupDrillingAudio()
    {
        AudioClip clip = AudioManager.Instance.DrillingClip;
        if (clip == null)
            return;

        Transform existing = transform.Find("DrillingAudioSource");
        GameObject audioObject = existing != null ? existing.gameObject : new GameObject("DrillingAudioSource");
        audioObject.transform.SetParent(transform, false);

        drillingAudioSource = audioObject.GetComponent<AudioSource>();
        if (drillingAudioSource == null)
        {
            drillingAudioSource = audioObject.AddComponent<AudioSource>();
        }

        drillingAudioSource.clip = clip;
        drillingAudioSource.loop = true;
        drillingAudioSource.playOnAwake = false;
        drillingAudioSource.volume = 0.455f;
        AudioManager.Instance.ConfigureSpatialSource(drillingAudioSource, 0.455f);
        drillingAudioSource.loop = true;
        drillingAudioSource.playOnAwake = false;
    }

    void StopLocalDrillingLoop()
    {
        if (drillingAudioSource != null && drillingAudioSource.isPlaying)
            drillingAudioSource.Stop();
    }

    [PunRPC]
    void RequestActivateArtifactAsteroid(string artifactId, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null)
            return;

        if (messageInfo.Sender != photonView.Owner)
            return;

        if (!PlayerHasAlienTransmitter(photonView.Owner))
            return;

        ArtifactAsteroid artifact = ArtifactAsteroid.Find(artifactId);
        if (artifact == null || artifact.IsActive)
            return;

        if (artifact.GetInteractionDistanceToPoint(GetShipTipPosition()) > ArtifactAsteroid.ExamineRange + ArtifactExamineKeepAliveDistance)
            return;

        ArtifactAsteroidSpawner.TryActivateAuthority(artifactId, photonView.Owner);
    }

    [PunRPC]
    void SetBeamTargetRpc(int targetViewId, bool active)
    {
        PhotonView targetView = PhotonView.Find(targetViewId);
        currentTreasure = targetView != null ? targetView.GetComponent<Treasure>() : null;
        currentWreck = currentTreasure == null && targetView != null ? targetView.GetComponent<ShipWreck>() : null;
        currentDroppedCargo = currentTreasure == null && currentWreck == null && targetView != null ? targetView.GetComponent<DroppedCargoCrate>() : null;
        SetBeamEnabled(active && (currentTreasure != null || currentWreck != null || currentDroppedCargo != null));
    }

    [PunRPC]
    void ClearBeamTargetRpc()
    {
        SetBeamEnabled(false);
        currentTreasure = null;
        currentWreck = null;
        currentDroppedCargo = null;
    }

    [PunRPC]
    void SetArtifactExamineBeamTargetRpc(string artifactId, bool active)
    {
        currentArtifactAsteroid = ArtifactAsteroid.Find(artifactId);
        SetArtifactBeamEnabled(active && currentArtifactAsteroid != null);
    }

    [PunRPC]
    void ClearArtifactExamineBeamTargetRpc()
    {
        SetArtifactBeamEnabled(false);
        if (!isExaminingArtifact)
            currentArtifactAsteroid = null;
    }

    [PunRPC]
    async void ReceiveLootedItemRpc(string itemId)
    {
        try
        {
            bool stored = await StoreItemToShipWithContainerDropsAsync(itemId);
            if (stored)
            {
                AddScore(RoundXpTracker.RecordWreckLooted(photonView.Owner, false));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to receive wreck loot item: " + ex);
        }
    }

    [PunRPC]
    async void ReceiveNovaTreasureRpc(int treasureViewId, string itemId)
    {
        bool stored = false;
        try
        {
            stored = await StoreItemToShipWithContainerDropsAsync(itemId, itemId, true, true);
            if (stored)
                AddScore(RoundXpTracker.RecordTreasureCollected(photonView.Owner, itemId));
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to store Nova burst treasure item: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ResolveReservedNovaTreasure), RpcTarget.MasterClient, treasureViewId, itemId, stored);
    }

    [PunRPC]
    async void ReceivePendingWreckLootRpc(int wreckViewId, int lootIndex, string itemId, string blueprintItemId)
    {
        bool playerWreck = false;
        PhotonView wreckView = PhotonView.Find(wreckViewId);
        if (wreckView != null)
        {
            ShipWreck wreck = wreckView.GetComponent<ShipWreck>();
            playerWreck = wreck != null && wreck.SourceShipSkinIndex >= 0;
        }

        bool itemStored = false;
        bool blueprintStored = false;
        try
        {
            bool hasBonusBlueprint = InventoryItemCatalog.IsBlueprintItem(blueprintItemId);
            if (hasBonusBlueprint)
            {
                blueprintStored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(blueprintItemId);
                if (blueprintStored)
                    ShowPickupToast(blueprintItemId);
            }

            if (hasBonusBlueprint)
            {
                itemStored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(itemId);
                if (itemStored)
                    ShowPickupToast(itemId);
            }
            else
            {
                itemStored = await StoreItemToShipWithContainerDropsAsync(itemId);
            }

            if (itemStored || blueprintStored)
            {
                AddScore(RoundXpTracker.RecordWreckLooted(photonView.Owner, playerWreck));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to store reserved wreck loot item: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ResolveReservedWreckLoot), RpcTarget.MasterClient, wreckViewId, lootIndex, itemId, blueprintItemId ?? string.Empty, itemStored, blueprintStored);
    }

    [PunRPC]
    async void ReceivePendingDroppedCargoLootRpc(int crateViewId, string itemId)
    {
        bool stored = false;
        try
        {
            stored = await StoreItemToShipWithContainerDropsAsync(itemId);
            if (stored)
            {
                AddScore(RoundXpTracker.RecordDroppedCargoLooted(photonView.Owner));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to store reserved dropped cargo item: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ResolveReservedDroppedCargoLoot), RpcTarget.MasterClient, crateViewId, itemId, stored);
    }

    [PunRPC]
    async void ReceivePendingRandomLootWreckRpc(int wreckViewId, string itemId)
    {
        bool stored = false;
        try
        {
            stored = await PlayerProfileService.Instance.AddRandomLootEquipmentDeferredSaveAsync(itemId);
            if (!stored)
                stored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(itemId);

            if (stored)
            {
                AddScore(RoundXpTracker.RecordWreckLooted(photonView.Owner, false));
                ShowPickupToast(itemId);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to store random loot wreck item: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ResolveReservedRandomLootWreck), RpcTarget.MasterClient, wreckViewId, itemId, stored);
    }

    [PunRPC]
    void ResolveReservedWreckLoot(int wreckViewId, int lootIndex, string itemId, string blueprintItemId, bool itemStored, bool blueprintStored)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!ReservedWreckLoot.TryGetValue(wreckViewId, out int reservedActor) || reservedActor != photonView.OwnerActorNr)
            return;

        ReservedWreckLoot.Remove(wreckViewId);
        PhotonView wreckView = PhotonView.Find(wreckViewId);
        ShipWreck wreck = wreckView != null ? wreckView.GetComponent<ShipWreck>() : null;
        if (wreck != null)
            wreck.isBeingCollected = false;

        if (!itemStored && !blueprintStored)
            return;

        if (wreckView == null || wreck == null || !wreck.HasLoot)
            return;

        string currentItemId = wreck.GetLootItemAt(lootIndex);
        if (!string.Equals(currentItemId, itemId, System.StringComparison.Ordinal))
            return;

        SpaceTrapTarget.DetonateIfArmed(wreckViewId, photonView != null ? photonView.ViewID : 0);
        NotifyPirateBasesAboutCollectedTarget(wreckViewId);
        if (blueprintStored && !itemStored)
            DropOverflowWreckLoot(wreck, itemId);

        wreckView.RPC(nameof(ShipWreck.RemoveLootAtIndexRpc), RpcTarget.All, lootIndex);
    }

    [PunRPC]
    void ResolveReservedNovaTreasure(int treasureViewId, string itemId, bool stored)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!ReservedTreasureCollections.TryGetValue(treasureViewId, out int reservedActor) || reservedActor != photonView.OwnerActorNr)
            return;

        ReservedTreasureCollections.Remove(treasureViewId);
        PhotonView treasureView = PhotonView.Find(treasureViewId);
        Treasure treasure = treasureView != null ? treasureView.GetComponent<Treasure>() : null;
        if (treasure != null)
            treasure.isBeingCollected = false;

        string resolvedTreasureItemId = treasure != null
            ? InventoryItemCatalog.ResolveAlienSecretItemId(treasure.itemId, treasure.visualVariantIndex)
            : null;
        if (!stored || treasureView == null || treasure == null || !string.Equals(resolvedTreasureItemId, itemId, System.StringComparison.Ordinal))
            return;

        SpaceTrapTarget.DetonateIfArmed(treasureViewId, photonView != null ? photonView.ViewID : 0);
        NotifyPirateBasesAboutCollectedTarget(treasureViewId);
        PhotonNetwork.Destroy(treasureView.gameObject);
    }

    void DropOverflowWreckLoot(ShipWreck wreck, string itemId)
    {
        if (wreck == null || string.IsNullOrWhiteSpace(itemId))
            return;

        Vector2 driftDirection = Random.insideUnitCircle.normalized;
        if (driftDirection.sqrMagnitude < 0.001f)
            driftDirection = Vector2.up;

        Vector3 dropPosition = wreck.transform.position + (Vector3)(driftDirection * 0.65f);
        Vector2 driftVelocity = driftDirection * Random.Range(0.45f, 0.85f);
        DroppedCargoManager.DropItemAtPosition(itemId, dropPosition, driftVelocity);
    }

    [PunRPC]
    void ResolveReservedDroppedCargoLoot(int crateViewId, string itemId, bool stored)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!ReservedDroppedCargoLoot.TryGetValue(crateViewId, out int reservedActor) || reservedActor != photonView.OwnerActorNr)
            return;

        ReservedDroppedCargoLoot.Remove(crateViewId);
        PhotonView crateView = PhotonView.Find(crateViewId);
        DroppedCargoCrate crate = crateView != null ? crateView.GetComponent<DroppedCargoCrate>() : null;
        if (crate != null)
            crate.isBeingCollected = false;

        if (!stored)
            return;

        if (crateView == null || crate == null || !crate.HasLoot || !string.Equals(crate.StoredItemId, itemId, System.StringComparison.Ordinal))
            return;

        SpaceTrapTarget.DetonateIfArmed(crateViewId, photonView != null ? photonView.ViewID : 0);
        NotifyPirateBasesAboutCollectedTarget(crateViewId);
        crateView.RPC(nameof(DroppedCargoCrate.ClearStoredItemRpc), RpcTarget.All);
        PhotonNetwork.Destroy(crateView.gameObject);
    }

    [PunRPC]
    void ResolveReservedRandomLootWreck(int wreckViewId, string itemId, bool stored)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!ReservedRandomLootWrecks.TryGetValue(wreckViewId, out int reservedActor) || reservedActor != photonView.OwnerActorNr)
            return;

        ReservedRandomLootWrecks.Remove(wreckViewId);
        PhotonView wreckView = PhotonView.Find(wreckViewId);
        Treasure wreck = wreckView != null ? wreckView.GetComponent<Treasure>() : null;
        if (wreck != null)
            wreck.isBeingCollected = false;

        if (!stored)
            return;

        if (wreckView == null || wreck == null || !InventoryItemCatalog.IsRandomLootWreckItem(wreck.itemId))
            return;

        SpaceTrapTarget.DetonateIfArmed(wreckViewId, photonView != null ? photonView.ViewID : 0);
        NotifyPirateBasesAboutCollectedTarget(wreckViewId);
        PhotonNetwork.Destroy(wreckView.gameObject);
    }

    async void StoreCollectedItem(string itemId, string sourceItemId = null)
    {
        try
        {
            await StoreItemToShipWithContainerDropsAsync(itemId, sourceItemId ?? itemId, true, true);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to store collected item: " + ex);
        }
    }

    bool CanAtlasAutoReplaceCargo(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !PlayerProfileService.HasInstance)
            return false;

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        if (!PilotCatalog.IsSelectedPilot(owner, PilotCatalog.AtlasId))
            return false;

        int slotIndex;
        string replacedItemId;
        return AtlasPilotRoundTracker.CanReplaceLeastValuableRoundCargo(
            PlayerProfileService.Instance.CurrentProfile,
            itemId,
            out slotIndex,
            out replacedItemId);
    }

    async System.Threading.Tasks.Task<bool> AddItemToShipWithAtlasAutoDropDeferredSaveAsync(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !PlayerProfileService.HasInstance)
            return false;

        bool stored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(itemId);
        if (stored)
            return true;

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        if (!PilotCatalog.IsSelectedPilot(owner, PilotCatalog.AtlasId))
            return false;

        int slotIndex;
        string replacedItemId;
        if (!AtlasPilotRoundTracker.CanReplaceLeastValuableRoundCargo(
                PlayerProfileService.Instance.CurrentProfile,
                itemId,
                out slotIndex,
                out replacedItemId))
        {
            return false;
        }

        string droppedItemId = await PlayerProfileService.Instance.ReplaceShipItemDeferredSaveAsync(slotIndex, itemId);
        if (string.IsNullOrWhiteSpace(droppedItemId))
            return false;

        DropAtlasAutoDroppedCargo(droppedItemId);
        return true;
    }

    void DropAtlasAutoDroppedCargo(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        Vector2 driftDirection = Random.insideUnitCircle;
        if (driftDirection.sqrMagnitude < 0.001f)
            driftDirection = Vector2.up;

        driftDirection.Normalize();
        Vector3 dropPosition = transform.position + (Vector3)(driftDirection * 0.72f);
        Vector2 driftVelocity = driftDirection * Random.Range(0.55f, 1.05f);
        DroppedCargoManager.DropItemAtPosition(itemId, dropPosition, driftVelocity);
    }

    async System.Threading.Tasks.Task<bool> StoreItemToShipWithContainerDropsAsync(
        string itemId,
        string sourceItemId = null,
        bool recordAsteroidProgress = false,
        bool allowNovaSpaceJunkBonus = false)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !PlayerProfileService.HasInstance)
            return false;

        bool isBlueprintScrapContainer =
            InventoryItemCatalog.IsBlueprintScrapContainerItem(itemId) ||
            InventoryItemCatalog.IsBlueprintScrapContainerItem(sourceItemId);
        if (isBlueprintScrapContainer)
        {
            bool stored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(InventoryItemCatalog.BlueprintScrapId);
            if (stored)
            {
                ShowPickupToast(InventoryItemCatalog.BlueprintScrapId);
                if (BlueprintCatalog.RollBlueprintScrapContainerBonus())
                {
                    bool bonusStored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(InventoryItemCatalog.BlueprintScrapId);
                    if (bonusStored)
                        ShowPickupToast(InventoryItemCatalog.BlueprintScrapId);
                }
            }

            return stored;
        }

        string storedItemId = BlueprintCatalog.ResolveContainerBlueprintDrop(itemId, GetLocalUnlockedBlueprintIdsForDropRoll());
        bool itemStored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(storedItemId);
        if (!itemStored)
            return false;

        ShowPickupToast(storedItemId);
        BroadcastValuableCargoCollected(storedItemId);

        if (recordAsteroidProgress)
            await RecordAsteroidSalvageProgressAsync(sourceItemId ?? itemId);

        if (allowNovaSpaceJunkBonus && IsNovaSpaceJunkBonusActive(storedItemId))
        {
            bool bonusStored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(storedItemId);
            if (bonusStored)
                ShowPickupToast(storedItemId);
        }

        return true;
    }

    string[] GetLocalUnlockedBlueprintIdsForDropRoll()
    {
        return PlayerProfileService.HasInstance && PlayerProfileService.Instance.CurrentProfile != null
            ? PlayerProfileService.Instance.CurrentProfile.UnlockedBlueprintIds
            : new string[0];
    }

    void BroadcastValuableCargoCollected(string itemId)
    {
        if (!InventoryItemCatalog.IsTrackedValuableCargo(itemId) || photonView == null)
            return;

        photonView.RPC(nameof(ShowValuableCargoCollectedRpc), RpcTarget.All, photonView.OwnerActorNr, itemId);
    }

    [PunRPC]
    void ShowValuableCargoCollectedRpc(int actorNumber, string itemId)
    {
        if (!InventoryItemCatalog.IsTrackedValuableCargo(itemId))
            return;

        string playerName = ResolveAnnouncementPlayerName(actorNumber);
        string itemName = InventoryItemCatalog.GetDisplayName(itemId);
        bool pirateCase = string.Equals(itemId, InventoryItemCatalog.PirateCaseId, System.StringComparison.Ordinal);
        string message = pirateCase
            ? playerName + " secured " + itemName + " - pirates hunting!"
            : playerName + " secured " + itemName + ".";

        Color markerColor;
        if (!ValuableCargoCarrierUtility.TryGetTrackedCargoMarkerColor(itemId, out markerColor))
            markerColor = new Color(1f, 0.75f, 0.18f, 0.95f);

        RoundMessageLayer.ShowTopCenter(message, ValuableCargoAnnouncementDuration, markerColor);
    }

    static string ResolveAnnouncementPlayerName(int actorNumber)
    {
        Photon.Realtime.Player player = PhotonNetwork.CurrentRoom != null
            ? PhotonNetwork.CurrentRoom.GetPlayer(actorNumber)
            : null;

        if (player != null && !string.IsNullOrWhiteSpace(player.NickName))
            return player.NickName;

        return "Someone";
    }

    string ResolveCovaxAsteroidCargoItem(string itemId)
    {
        if (!InventoryItemCatalog.IsAsteroidResource(itemId))
            return itemId;

        if (!PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.CovaxId))
            return itemId;

        if (UnityEngine.Random.value >= 0.1f)
            return itemId;

        return InventoryItemCatalog.TryGetNextAsteroidRarityId(itemId, out string upgradedItemId)
            ? upgradedItemId
            : itemId;
    }

    async System.Threading.Tasks.Task RecordAsteroidSalvageProgressAsync(string itemId)
    {
        if (!InventoryItemCatalog.IsAsteroidResource(itemId) || !PlayerProfileService.HasInstance)
            return;

        await PlayerProfileService.Instance.RecordPilotAsteroidSalvageAsync();
    }

    bool IsNovaSpaceJunkBonusActive(string itemId)
    {
        return PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.NovaId) &&
               PilotCatalog.IsSpaceJunkItem(itemId);
    }

    static string BuildViewIdCsv(List<int> viewIds)
    {
        if (viewIds == null || viewIds.Count == 0)
            return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < viewIds.Count; i++)
        {
            if (viewIds[i] <= 0)
                continue;

            if (builder.Length > 0)
                builder.Append(',');

            builder.Append(viewIds[i]);
        }

        return builder.ToString();
    }

    void ShowPickupToast(string itemId)
    {
        if (!photonView.IsMine || string.IsNullOrWhiteSpace(itemId))
            return;

        Sprite icon = InventoryItemCatalog.GetIcon(itemId);
        string label = InventoryItemCatalog.GetDisplayName(itemId);
        if (string.IsNullOrWhiteSpace(label))
            label = InventoryItemCatalog.GetShortLabel(itemId);

        RoundMessageLayer.ShowLeftFeed("LOOT", label, icon, 2f);
    }
}
