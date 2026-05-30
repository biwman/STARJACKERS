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

    const float TreasureScanInterval = 0.08f;
    const float BeamWidth = 0.18f;
    const float BeamJitterAmplitude = 0.13f;
    const float BeamJitterFrequency = 22f;
    const float BeamZOffset = -0.35f;
    const int BeamPointCount = 13;
    const float HudButtonVerticalNudge = 12f;
    const float ExtractionUseSearchRadius = 2.1f;
    const float ExtractionUseKeepAliveDistance = 1.15f;

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
        Activate,
        Escape
    }

    public Button collectButton;
    public TMP_Text scoreText;

    public PlayerMovement movement;
    public PlayerShooting shooting;

    Treasure currentTreasure;
    ShipWreck currentWreck;
    DroppedCargoCrate currentDroppedCargo;
    bool isCollecting;
    ExtractionZone currentExtraction;
    public float collectTime = 3f;
    public int totalScore = 0;
    AudioSource drillingAudioSource;
    LineRenderer collectionBeam;
    float nextTreasureScanTime;
    bool beamActive;
    GameObject pickupToastObject;
    Image pickupToastIcon;
    TMP_Text pickupToastLabel;
    Coroutine pickupToastRoutine;
    Coroutine collectibleUseRoutine;
    Coroutine extractionActivationRoutine;
    bool collectButtonHooked;
    bool isActivatingExtraction;
    RectTransform nudgedCollectButtonRect;
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
        Collider2D[] hits = Physics2D.OverlapCircleAll(tipPosition, maxRange + 0.85f);
        HashSet<int> queuedViewIds = new HashSet<int>();
        List<int> visualTargetIds = new List<int>();
        List<NovaPendingCollectible> pendingCollectibles = new List<NovaPendingCollectible>();

        for (int i = 0; i < hits.Length; i++)
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
        if (PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
        {
            PlayerDeployableRuntime.EnsureAttached(gameObject);
            enabled = false;
            return;
        }

        if (GetComponent<EnemyBot>() != null)
        {
            enabled = false;
            return;
        }

        SetupDrillingAudio();
        SetupBeam();

        if (!photonView.IsMine)
            return;

        TryBindHudReferences();

        if (scoreText != null)
        {
            scoreText.text = "XP: 0";
        }

        UpdateUseButtonAvailability();
        SyncScoreProperty();

        SetupPickupToast();
    }

    void Update()
    {
        if (photonView.IsMine && (!collectButtonHooked || collectButton == null || scoreText == null))
        {
            TryBindHudReferences();
        }

        if (photonView.IsMine && IsAstronautMode())
        {
            if (isCollecting)
            {
                CancelActiveCollection();
            }
            else if (currentTreasure != null || currentWreck != null || currentDroppedCargo != null)
            {
                ClearCurrentHighlight();
                currentTreasure = null;
                currentWreck = null;
                currentDroppedCargo = null;
            }

            UpdateUseButtonAvailability();
            UpdateCollectionBeam();
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
        UpdateCollectionBeam();
    }

    void EnsureActiveCollectibleTargetStillValid()
    {
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

        EnsureCollectButtonNudged();

        if (collectButton == null || collectButtonHooked)
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

        if (isActivatingExtraction)
            return;

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

        currentExtraction = ResolveNearbyExtractionZone();
        if (currentExtraction != null)
        {
            CancelActiveCollection();
            UseExtraction(currentExtraction);
            return;
        }

        if (!IsAstronautMode())
        {
            if (isCollecting)
                return;

            if (!HasLockedCollectibleTarget())
                RefreshClosestCollectible();

            if (!HasUsableCollectibleTarget())
                ForceResolveCollectibleAtUsePress();

            if (HasUsableCollectibleTarget())
            {
                if (!CanStoreCurrentCollectible())
                {
                    return;
                }

                if (currentTreasure != null && !isCollecting)
                {
                    RequestTreasureCollectionReservation(currentTreasure);
                    return;
                }

                if (currentWreck != null && currentWreck.HasLoot && !isCollecting)
                {
                    isCollecting = true;
                    StartCollectibleFeedback(currentWreck.GetComponent<PhotonView>());
                    collectibleUseRoutine = StartCoroutine(LootWreckRoutine(currentWreck));
                    return;
                }

                if (currentDroppedCargo != null && currentDroppedCargo.HasLoot && !isCollecting)
                {
                    isCollecting = true;
                    StartCollectibleFeedback(currentDroppedCargo.GetComponent<PhotonView>());
                    collectibleUseRoutine = StartCoroutine(LootDroppedCargoRoutine(currentDroppedCargo));
                    return;
                }
            }
        }
    }

    ExtractionZone ResolveNearbyExtractionZone()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, ExtractionUseSearchRadius);
        ExtractionZone bestZone = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            ExtractionZone extractionZone = hit.GetComponent<ExtractionZone>();
            if (extractionZone == null)
                extractionZone = hit.GetComponentInParent<ExtractionZone>();

            if (extractionZone == null)
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
        return AstronautSurvivor.IsAstronautInstantiationData(photonView.InstantiationData) ||
               GetComponent<AstronautSurvivor>() != null;
    }

    public void StopHolding()
    {
        if (!photonView.IsMine)
            return;

        PlayerRepairDocking repairDocking = GetComponent<PlayerRepairDocking>();
        if (repairDocking != null)
            repairDocking.StopUseHold();

        CancelActiveCollection();
        CancelExtractionActivation();
    }

    public void CancelCollectionForShot()
    {
        if (!photonView.IsMine || !isCollecting)
            return;

        CancelActiveCollection();
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
        float requiredActivationTime = Mathf.Max(0.1f, extractionZone != null ? extractionZone.activationTime : collectTime);
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

    bool IsExtractionStillUsable(ExtractionZone extractionZone)
    {
        if (extractionZone == null)
            return false;

        return GetDistanceToExtractionZone(extractionZone) <= ExtractionUseKeepAliveDistance;
    }

    float GetDistanceToExtractionZone(ExtractionZone extractionZone)
    {
        if (extractionZone == null)
            return float.MaxValue;

        return extractionZone.GetInteractionDistanceToPoint(transform.position);
    }

    void RequestTreasureCollectionReservation(Treasure treasure)
    {
        if (treasure == null || treasure.isBeingCollected)
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

        if (treasure == null || treasure.isBeingCollected)
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
        if (treasureToCollect == null || treasureToCollect.isBeingCollected)
        {
            AbortCollection(treasureToCollect);
            yield break;
        }

        treasureToCollect.isBeingCollected = true;

        float timer = 0f;
        float requiredCollectTime = GetTreasureCollectTime();
        SetUseProgress(0f, true);

        while (timer < requiredCollectTime)
        {
            if (!isCollecting || treasureToCollect == null || !IsTreasureInCollectRange(treasureToCollect, true))
            {
                AbortCollection(treasureToCollect);
                yield break;
            }

            currentTreasure = treasureToCollect;
            timer += Time.deltaTime;
            SetUseProgress(timer / requiredCollectTime, true);
            yield return null;
        }

        string collectedItemId = treasureToCollect.itemId;
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

    float GetTreasureCollectTime()
    {
        float pilotBonus = PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.RobyId) ? 0.5f : 0f;
        return Mathf.Max(0.1f, collectTime - pilotBonus);
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
        float requiredCollectTime = Mathf.Max(0.1f, collectTime);
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
            photonView.RPC(nameof(RequestLootWreck), RpcTarget.MasterClient, wreckView.ViewID);
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
        float requiredCollectTime = Mathf.Max(0.1f, collectTime);
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
            if (treasure == null)
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
            if (wreck == null || !wreck.HasLoot)
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
            if (crate == null || !crate.HasLoot)
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

        bool astronautMode = IsAstronautMode();

        if (!astronautMode)
        {
            PlayerRepairDocking repairDocking = GetComponent<PlayerRepairDocking>();
            RepairBay repairBay = RepairBay.FindClosestUsable(transform.position);
            SpaceFactory spaceFactory = SpaceFactory.FindClosestUsable(transform.position);
            ScienceStation scienceStation = ScienceStation.FindClosestUsable(transform.position);
            if (repairDocking != null && (repairDocking.IsBusy || repairBay != null || spaceFactory != null || scienceStation != null))
                return UseActionType.Land;
        }

        ExtractionZone extractionZone = ResolveUsableExtractionZone();
        if (extractionZone != null)
        {
            if (extractionZone.IsTransitioning || extractionZone.IsEvacuating || IsPendingActivatedExtraction(extractionZone))
                return UseActionType.None;

            return extractionZone.IsActive ? UseActionType.Escape : UseActionType.Activate;
        }

        if (astronautMode)
            return UseActionType.None;

        if (!HasUsableCollectibleTarget())
            return UseActionType.None;

        return CanStoreCurrentCollectible() ? UseActionType.Collect : UseActionType.None;
    }

    ExtractionZone ResolveUsableExtractionZone()
    {
        if (currentExtraction != null && IsExtractionStillUsable(currentExtraction))
            return currentExtraction;

        return ResolveNearbyExtractionZone();
    }

    bool CanStoreCurrentCollectible()
    {
        if (!PlayerProfileService.HasInstance)
            return false;

        if (currentTreasure != null && InventoryItemCatalog.IsRandomLootWreckItem(currentTreasure.itemId))
        {
            return PlayerProfileService.Instance.HasFreeShipInventorySlot() ||
                   PlayerProfileService.PlayerHasFreeGadgetEquipmentSlot(PhotonNetwork.LocalPlayer);
        }

        if (currentTreasure != null)
            return CanStoreTreasure(currentTreasure);

        if (currentDroppedCargo != null)
        {
            string storedItemId = InventoryItemCatalog.IsBlueprintScrapContainerItem(currentDroppedCargo.StoredItemId)
                ? InventoryItemCatalog.BlueprintScrapId
                : currentDroppedCargo.StoredItemId;
            return PlayerProfileService.Instance.HasFreeShipInventorySlot(storedItemId);
        }

        return PlayerProfileService.Instance.HasFreeShipInventorySlot();
    }

    bool CanStoreTreasure(Treasure treasure)
    {
        if (treasure == null || !PlayerProfileService.HasInstance)
            return false;

        if (InventoryItemCatalog.IsRandomLootWreckItem(treasure.itemId))
        {
            return PlayerProfileService.Instance.HasFreeShipInventorySlot() ||
                   PlayerProfileService.PlayerHasFreeGadgetEquipmentSlot(PhotonNetwork.LocalPlayer);
        }

        if (InventoryItemCatalog.IsBlueprintScrapContainerItem(treasure.itemId))
            return PlayerProfileService.Instance.HasFreeShipInventorySlot(InventoryItemCatalog.BlueprintScrapId);

        return PlayerProfileService.Instance.HasFreeShipInventorySlot(treasure.itemId);
    }

    bool HasUsableCollectibleTarget()
    {
        return (currentTreasure != null && IsTreasureInCollectRange(currentTreasure)) ||
               (currentWreck != null && currentWreck.HasLoot && IsWreckInCollectRange(currentWreck)) ||
               (currentDroppedCargo != null && currentDroppedCargo.HasLoot && IsDroppedCargoInCollectRange(currentDroppedCargo));
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
                return "LAND";
            case UseActionType.Activate:
                return "ACTIVATE";
            case UseActionType.Escape:
                return "ESCAPE";
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
        return isCollecting || isActivatingExtraction;
    }

    void SetUseProgress(float progress, bool visible)
    {
        if (useButtonVisual == null && collectButton != null)
            useButtonVisual = collectButton.GetComponent<UseButtonVisualController>();

        if (useButtonVisual != null)
            useButtonVisual.SetProgress(progress, visible);
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
        Collider2D[] hits = Physics2D.OverlapCircleAll(tipPosition, GetCollectibleSearchRange(Treasure.CollectRange + 0.55f));
        Treasure nextTreasure = null;
        ShipWreck nextWreck = null;
        DroppedCargoCrate nextDroppedCargo = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            Treasure treasure = hit.GetComponent<Treasure>() ?? hit.GetComponentInParent<Treasure>();
            if (treasure != null)
            {
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
        if (currentTreasure != null && !currentTreasure.isBeingCollected)
            currentTreasure.Unhighlight();

        if (currentWreck != null && !currentWreck.isBeingCollected)
            currentWreck.Unhighlight();

        if (currentDroppedCargo != null && !currentDroppedCargo.isBeingCollected)
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

    void EnsureCollectButtonNudged()
    {
        if (collectButton == null)
            return;

        RectTransform rect = collectButton.GetComponent<RectTransform>();
        if (rect == null || rect == nudgedCollectButtonRect)
            return;

        rect.anchoredPosition += new Vector2(0f, HudButtonVerticalNudge);
        nudgedCollectButtonRect = rect;
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

    void OnDestroy()
    {
        ReleaseMasterTreasureReservation(true);
        ClearCurrentHighlight();
        StopLocalDrillingLoop();

        if (pickupToastObject != null)
            Destroy(pickupToastObject);
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
        if (treasure != null && !treasure.isBeingCollected && IsTreasureInCollectRange(treasure, true))
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

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, treasure.itemId))
            return false;

        queuedViewIds.Add(viewId);
        treasure.isBeingCollected = true;
        ReservedTreasureCollections[viewId] = photonView.OwnerActorNr;
        pendingCollectibles.Add(new NovaPendingCollectible
        {
            Kind = NovaPendingCollectibleKind.Treasure,
            ViewId = viewId,
            ItemId = treasure.itemId
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

        bool canStoreReward = PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, rewardItemId) ||
                              (InventoryItemCatalog.GetCategory(rewardItemId) == InventoryItemCategory.Gadget &&
                               PlayerProfileService.PlayerHasFreeGadgetEquipmentSlot(photonView.Owner));
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
        if (treasure == null || string.IsNullOrWhiteSpace(pending.ItemId) ||
            !string.Equals(treasure.itemId, pending.ItemId, System.StringComparison.Ordinal))
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
                              (InventoryItemCatalog.GetCategory(rewardItemId) == InventoryItemCategory.Gadget &&
                               PlayerProfileService.PlayerHasFreeGadgetEquipmentSlot(photonView.Owner));
        if (!canStoreReward)
            return;

        ReservedRandomLootWrecks[viewID] = photonView.OwnerActorNr;
        photonView.RPC(nameof(ReceivePendingRandomLootWreckRpc), photonView.Owner, viewID, rewardItemId);
    }

    [PunRPC]
    void RequestLootWreck(int viewID)
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

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, itemId))
            return;

        string blueprintItemId = string.Empty;
        if (wreck.SourceShipSkinIndex < 0)
            BlueprintCatalog.TryRollWreckBlueprintDrop(itemId, wreck.SourceEnemyKindValue, out blueprintItemId);

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

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, itemId))
            return;

        ReservedDroppedCargoLoot[viewID] = photonView.OwnerActorNr;
        photonView.RPC(nameof(ReceivePendingDroppedCargoLootRpc), photonView.Owner, viewID, itemId);
    }

    string RollRandomLootWreckReward()
    {
        float roll = Random.value;
        InventoryItemCategory category;
        if (roll < 0.7f)
            category = InventoryItemCategory.Gadget;
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
                blueprintStored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(blueprintItemId);
                if (blueprintStored)
                    ShowPickupToast(blueprintItemId);
            }

            if (hasBonusBlueprint)
            {
                itemStored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(itemId);
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

        if (!stored || treasureView == null || treasure == null || !string.Equals(treasure.itemId, itemId, System.StringComparison.Ordinal))
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
            bool stored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(InventoryItemCatalog.BlueprintScrapId);
            if (stored)
            {
                ShowPickupToast(InventoryItemCatalog.BlueprintScrapId);
                if (BlueprintCatalog.RollBlueprintScrapContainerBonus())
                {
                    bool bonusStored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(InventoryItemCatalog.BlueprintScrapId);
                    if (bonusStored)
                        ShowPickupToast(InventoryItemCatalog.BlueprintScrapId);
                }
            }

            return stored;
        }

        string storedItemId = BlueprintCatalog.ResolveContainerBlueprintDrop(itemId);
        bool itemStored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(storedItemId);
        if (!itemStored)
            return false;

        ShowPickupToast(storedItemId);

        if (recordAsteroidProgress)
            await RecordAsteroidSalvageProgressAsync(sourceItemId ?? itemId);

        if (allowNovaSpaceJunkBonus && IsNovaSpaceJunkBonusActive(storedItemId))
        {
            bool bonusStored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(storedItemId);
            if (bonusStored)
                ShowPickupToast(storedItemId);
        }

        return true;
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

    void SetupPickupToast()
    {
        if (!photonView.IsMine || pickupToastObject != null)
            return;

        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
            return;

        pickupToastObject = new GameObject("PickupToast", typeof(RectTransform), typeof(Image));
        pickupToastObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = pickupToastObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(22f, -24f);
        rect.sizeDelta = new Vector2(128f, 122f);

        Image bg = pickupToastObject.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.09f, 0.15f, 0.92f);
        bg.raycastTarget = false;

        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(pickupToastObject.transform, false);

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -6f);
        titleRect.sizeDelta = new Vector2(-12f, 20f);

        TextMeshProUGUI titleText = titleObject.GetComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 13f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.color = new Color(0.84f, 0.93f, 1f, 0.95f);
        titleText.text = "LOOT";

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(pickupToastObject.transform, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, -8f);
        iconRect.sizeDelta = new Vector2(72f, 72f);

        pickupToastIcon = iconObject.GetComponent<Image>();
        pickupToastIcon.preserveAspect = true;
        pickupToastIcon.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(pickupToastObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 7f);
        labelRect.sizeDelta = new Vector2(-14f, 24f);

        pickupToastLabel = labelObject.GetComponent<TextMeshProUGUI>();
        pickupToastLabel.alignment = TextAlignmentOptions.Center;
        pickupToastLabel.fontSize = 14f;
        pickupToastLabel.fontStyle = FontStyles.Bold;
        pickupToastLabel.textWrappingMode = TextWrappingModes.NoWrap;
        pickupToastLabel.color = new Color(0.95f, 0.98f, 1f, 0.96f);
        pickupToastLabel.text = string.Empty;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference != null)
        {
            titleText.font = reference.font;
            titleText.fontSharedMaterial = reference.fontSharedMaterial;
            pickupToastLabel.font = reference.font;
            pickupToastLabel.fontSharedMaterial = reference.fontSharedMaterial;
        }

        pickupToastObject.SetActive(false);
    }

    void ShowPickupToast(string itemId)
    {
        if (!photonView.IsMine || string.IsNullOrWhiteSpace(itemId))
            return;

        SetupPickupToast();
        if (pickupToastObject == null)
            return;

        Sprite icon = InventoryItemCatalog.GetIcon(itemId);
        if (pickupToastIcon != null)
        {
            pickupToastIcon.sprite = icon;
            pickupToastIcon.enabled = icon != null;
        }

        if (pickupToastLabel != null)
        {
            string label = InventoryItemCatalog.GetDisplayName(itemId);
            if (string.IsNullOrWhiteSpace(label))
                label = InventoryItemCatalog.GetShortLabel(itemId);

            pickupToastLabel.text = label;
        }

        if (pickupToastRoutine != null)
            StopCoroutine(pickupToastRoutine);

        pickupToastRoutine = StartCoroutine(PickupToastRoutine());
    }

    IEnumerator PickupToastRoutine()
    {
        pickupToastObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        pickupToastObject.SetActive(false);
        pickupToastRoutine = null;
    }
}
