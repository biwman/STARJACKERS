using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class TreasureCollector : MonoBehaviourPun
{
    static TreasureCollector collectButtonBindingOwner;

    const float TreasureScanInterval = 0.08f;
    const float MobileTreasureScanInterval = 0.12f;
    const float UseActionRefreshInterval = 0.12f;
    const float MobileUseActionRefreshInterval = 0.18f;
    const float LoadoutCacheRefreshInterval = 0.25f;
    const float BeamWidth = 0.18f;
    const float BeamJitterAmplitude = 0.13f;
    const float BeamJitterFrequency = 22f;
    const float BeamZOffset = -0.35f;
    const int BeamPointCount = 13;
    const float ArtifactExamineSeconds = 5f;
    const float ArtifactExamineKeepAliveDistance = 0.9f;
    const float ArtifactBeamWidth = 0.16f;
    static readonly Vector2 CollectButtonRoundPosition = new Vector2(230f, 490f);
    static readonly Vector2 CollectButtonDesktopPosition = new Vector2(226f, 96f);
    const float ValuableCargoAnnouncementDuration = 4f;

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
    Vector2 collectButtonDefaultAnchorMin;
    Vector2 collectButtonDefaultAnchorMax;
    Vector2 collectButtonDefaultPivot;
    bool collectButtonDefaultLayoutCaptured;
    bool collectButtonLayoutApplied;
    bool collectButtonUsingDesktopLayout;
    UseButtonVisualController useButtonVisual;
    ExtractionZone pendingActivatedExtraction;
    float pendingActivatedExtractionUntil;
    int pendingTreasureReservationViewId;
    int activeTreasureReservationViewId;
    float nextUseActionRefreshTime;
    PlayerHealth cachedPlayerHealth;
    PlayerRepairDocking cachedPlayerRepairDocking;
    SpriteRenderer cachedSpriteRenderer;
    int cachedLoadoutActorNumber = int.MinValue;
    int cachedLoadoutInventoryRevision = -1;
    float nextLoadoutRefreshTime;
    int cachedLoadoutShipSkinIndex;
    int cachedLoadoutShipCapacity;
    string[] cachedLoadoutShipSlots;
    string[] cachedLoadoutEquipmentSlots;
    bool cachedHasAlienTransmitter;
    bool cachedHasSalvageMagnetArray;
    Gradient collectionBeamGradient;
    Gradient artifactBeamGradient;
    readonly GradientColorKey[] collectionBeamColorKeys =
    {
        new GradientColorKey(new Color(0.96f, 1f, 0.86f), 0f),
        new GradientColorKey(new Color(0.28f, 1f, 0.66f), 0.38f),
        new GradientColorKey(new Color(0.1f, 0.74f, 1f), 1f)
    };
    readonly GradientAlphaKey[] collectionBeamAlphaKeys =
    {
        new GradientAlphaKey(0.95f, 0f),
        new GradientAlphaKey(0.72f, 0.55f),
        new GradientAlphaKey(0.18f, 1f)
    };
    readonly GradientColorKey[] artifactBeamColorKeys =
    {
        new GradientColorKey(new Color(0.08f, 0.46f, 1f), 0f),
        new GradientColorKey(new Color(0.48f, 0.92f, 1f), 0.46f),
        new GradientColorKey(new Color(0.12f, 0.18f, 1f), 1f)
    };
    readonly GradientAlphaKey[] artifactBeamAlphaKeys =
    {
        new GradientAlphaKey(0.86f, 0f),
        new GradientAlphaKey(1f, 0.5f),
        new GradientAlphaKey(0.24f, 1f)
    };

    public bool IsCollectingAny => isCollecting;

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

        if (movement == null)
            movement = GetComponent<PlayerMovement>();

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

        if (photonView.IsMine)
            EnsureCollectButtonPositioned();

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

            UpdateUseButtonAvailabilityThrottled();
            UpdateCollectionBeam();
            UpdateArtifactExamineBeam();
            return;
        }

        if (photonView.IsMine && Time.unscaledTime >= nextTreasureScanTime)
        {
            nextTreasureScanTime = Time.unscaledTime + GetTreasureScanInterval();
            if (!HasLockedCollectibleTarget())
                RefreshClosestCollectible();
        }

        if (photonView.IsMine)
            EnsureActiveCollectibleTargetStillValid();

        UpdateUseButtonAvailabilityThrottled();
        UpdateHaulChargeUseProgress();
        UpdateCollectionBeam();
        UpdateArtifactExamineBeam();
    }

    float GetTreasureScanInterval()
    {
        if (!Application.isMobilePlatform)
            return TreasureScanInterval;

        return MobilePerformanceSettings.CurrentProfile == MobilePerformanceProfile.High
            ? TreasureScanInterval
            : MobileTreasureScanInterval;
    }

    float GetUseActionRefreshInterval()
    {
        if (!Application.isMobilePlatform)
            return UseActionRefreshInterval;

        return MobilePerformanceSettings.CurrentProfile == MobilePerformanceProfile.High
            ? UseActionRefreshInterval
            : MobileUseActionRefreshInterval;
    }

    void UpdateUseButtonAvailabilityThrottled()
    {
        if (Time.unscaledTime < nextUseActionRefreshTime)
            return;

        UpdateUseButtonAvailability();
    }

    PlayerHealth GetCachedPlayerHealth()
    {
        if (cachedPlayerHealth == null)
            cachedPlayerHealth = GetComponent<PlayerHealth>();

        return cachedPlayerHealth;
    }

    PlayerRepairDocking GetCachedPlayerRepairDocking()
    {
        if (cachedPlayerRepairDocking == null)
            cachedPlayerRepairDocking = GetComponent<PlayerRepairDocking>();

        return cachedPlayerRepairDocking;
    }

    SpriteRenderer GetCachedSpriteRenderer()
    {
        if (cachedSpriteRenderer == null)
            cachedSpriteRenderer = GetComponent<SpriteRenderer>();

        return cachedSpriteRenderer;
    }

    void RefreshLoadoutCacheIfNeeded(Photon.Realtime.Player player)
    {
        int actorNumber = player != null ? player.ActorNumber : -1;
        int inventoryRevision = PlayerProfileService.HasInstance ? PlayerProfileService.Instance.InventoryRevision : -1;
        float now = Time.unscaledTime;

        if (cachedLoadoutShipSlots != null &&
            cachedLoadoutActorNumber == actorNumber &&
            cachedLoadoutInventoryRevision == inventoryRevision &&
            now < nextLoadoutRefreshTime)
        {
            return;
        }

        cachedLoadoutActorNumber = actorNumber;
        cachedLoadoutInventoryRevision = inventoryRevision;
        nextLoadoutRefreshTime = now + LoadoutCacheRefreshInterval;
        cachedLoadoutShipSkinIndex = RoomSettings.GetPlayerShipSkin(player, 0);
        cachedLoadoutEquipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(player);
        cachedLoadoutShipSlots = PlayerProfileService.GetPlayerShipInventorySlots(player);
        cachedLoadoutShipCapacity = PlayerProfileService.GetPlayerShipInventoryCapacity(player);
        cachedHasSalvageMagnetArray = InventoryItemCatalog.HasEquippedItem(
            cachedLoadoutEquipmentSlots,
            cachedLoadoutShipSkinIndex,
            InventoryItemCatalog.SalvageMagnetArrayId);
        cachedHasAlienTransmitter = false;

        int slotCount = cachedLoadoutShipSlots != null ? cachedLoadoutShipSlots.Length : 0;
        int limit = Mathf.Clamp(cachedLoadoutShipCapacity, 0, slotCount);
        for (int i = 0; i < limit; i++)
        {
            if (string.Equals(cachedLoadoutShipSlots[i], InventoryItemCatalog.AlienTransmitterId, System.StringComparison.Ordinal))
            {
                cachedHasAlienTransmitter = true;
                break;
            }
        }
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

}
