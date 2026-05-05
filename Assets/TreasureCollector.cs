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
    static readonly System.Collections.Generic.Dictionary<int, int> ReservedWreckLoot = new System.Collections.Generic.Dictionary<int, int>();
    static readonly System.Collections.Generic.Dictionary<int, int> ReservedDroppedCargoLoot = new System.Collections.Generic.Dictionary<int, int>();

    const float TreasureScanInterval = 0.08f;
    const float BeamWidth = 0.18f;
    const float BeamJitterAmplitude = 0.13f;
    const float BeamJitterFrequency = 22f;
    const float BeamZOffset = -0.35f;
    const int BeamPointCount = 13;
    const float CollectFacingTurnSpeed = 720f;
    const float HudButtonVerticalNudge = 12f;
    const float ExtractionUseSearchRadius = 2.1f;
    const float ExtractionUseKeepAliveDistance = 1.15f;

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
    Coroutine extractionUseRoutine;
    Coroutine collectibleUseRoutine;
    bool collectButtonHooked;
    RectTransform nudgedCollectButtonRect;

    public static void ResetRoundReservations()
    {
        ReservedWreckLoot.Clear();
        ReservedDroppedCargoLoot.Clear();
    }

    void Start()
    {
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
            if (currentTreasure != null || currentWreck != null || currentDroppedCargo != null)
            {
                ClearCurrentHighlight();
                currentTreasure = null;
                currentWreck = null;
                currentDroppedCargo = null;
            }

            UpdateCollectionBeam();
            return;
        }

        if (photonView.IsMine && Time.unscaledTime >= nextTreasureScanTime)
        {
            nextTreasureScanTime = Time.unscaledTime + TreasureScanInterval;
            if (!HasLockedCollectibleTarget())
                RefreshClosestCollectible();
        }

        UpdateCollectionBeam();
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

        EventTrigger trigger = collectButton.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = collectButton.gameObject.AddComponent<EventTrigger>();

        trigger.triggers = new List<EventTrigger.Entry>();

        EventTrigger.Entry down = new EventTrigger.Entry();
        down.eventID = EventTriggerType.PointerDown;
        down.callback.AddListener(_ => StartHolding());
        trigger.triggers.Add(down);

        EventTrigger.Entry up = new EventTrigger.Entry();
        up.eventID = EventTriggerType.PointerUp;
        up.callback.AddListener(_ => StopHolding());
        trigger.triggers.Add(up);

        collectButtonHooked = true;
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

        PlayerRepairDocking repairDocking = GetComponent<PlayerRepairDocking>();
        RepairBay repairBay = RepairBay.FindClosestUsable(transform.position);
        if (repairDocking != null && (repairDocking.IsBusy || repairBay != null))
        {
            if (repairDocking.TryStartUse(repairBay))
                return;
        }

        currentExtraction = ResolveNearbyExtractionZone();
        if (currentExtraction != null)
        {
            if (!isCollecting)
            {
                isCollecting = true;
                LockControls();
                extractionUseRoutine = StartCoroutine(HoldExtractionRoutine(currentExtraction));
            }

            return;
        }

        if (!IsAstronautMode())
        {
            if (!HasLockedCollectibleTarget())
                RefreshClosestCollectible();

            if (!HasUsableCollectibleTarget())
                ForceResolveCollectibleAtUsePress();

            if (HasUsableCollectibleTarget())
            {
                if (!PlayerProfileService.Instance.HasFreeShipInventorySlot())
                {
                    return;
                }

                if (currentTreasure != null && !isCollecting)
                {
                    isCollecting = true;
                    StartCollectibleFeedback(currentTreasure.GetComponent<PhotonView>());
                    collectibleUseRoutine = StartCoroutine(CollectTreasureRoutine(currentTreasure));
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

        if (repairDocking != null && repairDocking.IsBusy)
            return;

        ReleaseCurrentCollectibleReservation();
        isCollecting = false;
        StopCollectibleFeedback();
        if (extractionUseRoutine != null)
        {
            StopCoroutine(extractionUseRoutine);
            extractionUseRoutine = null;
        }

        if (collectibleUseRoutine != null)
        {
            StopCoroutine(collectibleUseRoutine);
            collectibleUseRoutine = null;
        }

        if (movement != null) movement.enabled = true;
        if (shooting != null) shooting.enabled = true;
    }

    IEnumerator HoldExtractionRoutine(ExtractionZone extractionZone)
    {
        float timer = 0f;

        while (timer < collectTime)
        {
            if (!isCollecting || extractionZone == null || !IsExtractionStillUsable(extractionZone))
            {
                AbortCollection();
                yield break;
            }

            currentExtraction = extractionZone;
            timer += Time.deltaTime;
            yield return null;
        }

        PhotonView ezView = extractionZone.GetComponent<PhotonView>();
        if (ezView != null)
            photonView.RPC(nameof(RequestUseExtraction), RpcTarget.MasterClient, ezView.ViewID);

        FinishCollection();
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

        Collider2D zoneCollider = extractionZone.GetComponent<Collider2D>();
        if (zoneCollider != null)
        {
            Vector2 closestPoint = zoneCollider.ClosestPoint(transform.position);
            return Vector2.Distance(transform.position, closestPoint);
        }

        return Vector2.Distance(transform.position, extractionZone.transform.position);
    }

    IEnumerator CollectTreasureRoutine(Treasure treasureToCollect)
    {
        if (treasureToCollect == null || treasureToCollect.isBeingCollected)
        {
            AbortCollection(treasureToCollect);
            yield break;
        }

        treasureToCollect.isBeingCollected = true;
        LockControls();

        float timer = 0f;

        while (timer < collectTime)
        {
            if (!isCollecting || treasureToCollect == null || !IsTreasureInCollectRange(treasureToCollect))
            {
                AbortCollection(treasureToCollect);
                yield break;
            }

            currentTreasure = treasureToCollect;
            FaceCollectibleTarget();
            timer += Time.deltaTime;
            yield return null;
        }

        string collectedItemId = treasureToCollect.itemId;
        PhotonView treasureView = treasureToCollect.GetComponent<PhotonView>();
        int treasureViewId = treasureView != null ? treasureView.ViewID : 0;

        int collectXp = RoundXpTracker.RecordTreasureCollected(photonView.Owner, collectedItemId);
        AddScore(collectXp);
        StoreCollectedItem(collectedItemId);

        treasureToCollect.isBeingCollected = false;
        FinishCollection();

        if (treasureViewId > 0)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonView viewToDestroy = PhotonView.Find(treasureViewId);
                if (viewToDestroy != null)
                    PhotonNetwork.Destroy(viewToDestroy.gameObject);
            }
            else
            {
                photonView.RPC(nameof(RequestDestroyTreasure), RpcTarget.MasterClient, treasureViewId);
            }
        }
    }

    IEnumerator LootWreckRoutine(ShipWreck wreckToLoot)
    {
        if (wreckToLoot == null || !wreckToLoot.HasLoot || wreckToLoot.isBeingCollected)
        {
            AbortCollection();
            yield break;
        }

        wreckToLoot.isBeingCollected = true;
        LockControls();

        float timer = 0f;

        while (timer < collectTime)
        {
            if (!isCollecting || wreckToLoot == null || !wreckToLoot.HasLoot || !IsWreckInCollectRange(wreckToLoot))
            {
                wreckToLoot.isBeingCollected = false;
                AbortCollection();
                yield break;
            }

            currentWreck = wreckToLoot;
            FaceCollectibleTarget();
            timer += Time.deltaTime;
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
        LockControls();

        float timer = 0f;

        while (timer < collectTime)
        {
            if (!isCollecting || crateToLoot == null || !crateToLoot.HasLoot || !IsDroppedCargoInCollectRange(crateToLoot))
            {
                crateToLoot.isBeingCollected = false;
                AbortCollection();
                yield break;
            }

            currentDroppedCargo = crateToLoot;
            FaceCollectibleTarget();
            timer += Time.deltaTime;
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

    void LockControls()
    {
        if (movement != null) movement.enabled = false;
        if (shooting != null) shooting.enabled = false;
    }

    void ReleaseCurrentCollectibleReservation()
    {
        if (currentTreasure != null)
            currentTreasure.isBeingCollected = false;
        if (currentWreck != null)
            currentWreck.isBeingCollected = false;
        if (currentDroppedCargo != null)
            currentDroppedCargo.isBeingCollected = false;
    }

    void FaceCollectibleTarget()
    {
        if (!photonView.IsMine)
            return;

        Vector2 target = GetCurrentCollectibleCenter(transform.position);
        Vector2 toTarget = target - (Vector2)transform.position;
        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, CollectFacingTurnSpeed * Time.deltaTime);
    }

    public void ForceCancelCollectionForDeath()
    {
        isCollecting = false;

        if (currentTreasure != null)
            currentTreasure.isBeingCollected = false;
        if (currentWreck != null)
            currentWreck.isBeingCollected = false;
        if (currentDroppedCargo != null)
            currentDroppedCargo.isBeingCollected = false;

        if (extractionUseRoutine != null)
        {
            StopCoroutine(extractionUseRoutine);
            extractionUseRoutine = null;
        }

        if (collectibleUseRoutine != null)
        {
            StopCoroutine(collectibleUseRoutine);
            collectibleUseRoutine = null;
        }

        StopAllCoroutines();
        StopLocalDrillingLoop();
        SetBeamEnabled(false);
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

        FinishCollection();
    }

    void FinishCollection()
    {
        isCollecting = false;
        collectibleUseRoutine = null;
        StopCollectibleFeedback();
        currentTreasure = null;
        currentWreck = null;
        currentDroppedCargo = null;

        if (!CanRestoreControlsAfterCollection())
        {
            if (movement != null) movement.enabled = false;
            if (shooting != null) shooting.enabled = false;
            return;
        }

        if (movement != null) movement.enabled = true;
        if (shooting != null) shooting.enabled = true;
    }

    bool CanRestoreControlsAfterCollection()
    {
        PlayerHealth health = GetComponent<PlayerHealth>();
        return health == null || (!health.IsWreck && !health.IsEvacuationAnimating);
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
            if (distance > Treasure.CollectRange)
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
            float wreckCollectRange = wreck.SourceShipSkinIndex < 0 ? Treasure.CollectRange + 0.45f : Treasure.CollectRange;
            if (distance > wreckCollectRange)
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

    bool HasUsableCollectibleTarget()
    {
        return (currentTreasure != null && IsTreasureInCollectRange(currentTreasure)) ||
               (currentWreck != null && currentWreck.HasLoot && IsWreckInCollectRange(currentWreck)) ||
               (currentDroppedCargo != null && currentDroppedCargo.HasLoot && IsDroppedCargoInCollectRange(currentDroppedCargo));
    }

    void ForceResolveCollectibleAtUsePress()
    {
        Vector2 tipPosition = GetShipTipPosition();
        Collider2D[] hits = Physics2D.OverlapCircleAll(tipPosition, Treasure.CollectRange + 0.55f);
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
                if (distance <= Treasure.CollectRange && distance < bestDistance)
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
                float wreckCollectRange = wreck.SourceShipSkinIndex < 0 ? Treasure.CollectRange + 0.45f : Treasure.CollectRange;
                float distance = GetDistanceFromTipToCollider(wreck.GetComponent<Collider2D>(), wreck.transform.position, tipPosition);
                if (distance <= wreckCollectRange && distance < bestDistance)
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

    bool IsTreasureInCollectRange(Treasure treasure)
    {
        if (treasure == null)
            return false;

        return GetDistanceFromTipToCollider(treasure.GetComponent<Collider2D>(), treasure.transform.position, GetShipTipPosition()) <= Treasure.CollectRange;
    }

    bool IsWreckInCollectRange(ShipWreck wreck)
    {
        if (wreck == null || !wreck.HasLoot)
            return false;

        float wreckCollectRange = wreck.SourceShipSkinIndex < 0 ? Treasure.CollectRange + 0.45f : Treasure.CollectRange;
        return GetDistanceFromTipToCollider(wreck.GetComponent<Collider2D>(), wreck.transform.position, GetShipTipPosition()) <= wreckCollectRange;
    }

    bool IsDroppedCargoInCollectRange(DroppedCargoCrate crate)
    {
        if (crate == null || !crate.HasLoot)
            return false;

        return GetDistanceFromTipToCollider(crate.GetComponent<Collider2D>(), crate.transform.position, GetShipTipPosition()) <= Treasure.CollectRange;
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
            shouldShow = currentTreasure != null
                ? IsTreasureInCollectRange(currentTreasure)
                : currentWreck != null
                    ? IsWreckInCollectRange(currentWreck)
                    : IsDroppedCargoInCollectRange(currentDroppedCargo);
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

    Vector2 GetCurrentCollectibleCenter(Vector2 fallback)
    {
        if (currentTreasure != null)
            return currentTreasure.transform.position;
        if (currentWreck != null)
            return currentWreck.transform.position;
        if (currentDroppedCargo != null)
            return currentDroppedCargo.transform.position;

        return fallback;
    }

    void SetBeamEnabled(bool enabled)
    {
        beamActive = enabled;
        if (collectionBeam != null)
            collectionBeam.enabled = enabled;
    }

    void OnDestroy()
    {
        ClearCurrentHighlight();
        StopLocalDrillingLoop();

        if (pickupToastObject != null)
            Destroy(pickupToastObject);
    }

    [PunRPC]
    void RequestDestroyTreasure(int viewID)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        PhotonView pv = PhotonView.Find(viewID);
        if (pv != null)
        {
            PhotonNetwork.Destroy(pv.gameObject);
        }
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
        if (wreck == null || !wreck.HasLoot)
            return;

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner))
            return;

        if (ReservedWreckLoot.TryGetValue(viewID, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return;

        int lootIndex = wreck.GetFirstLootIndex();
        string itemId = wreck.GetLootItemAt(lootIndex);
        if (lootIndex < 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        ReservedWreckLoot[viewID] = photonView.OwnerActorNr;
        photonView.RPC(nameof(ReceivePendingWreckLootRpc), photonView.Owner, viewID, lootIndex, itemId);
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
        if (crate == null || !crate.HasLoot)
            return;

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner))
            return;

        if (ReservedDroppedCargoLoot.TryGetValue(viewID, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return;

        string itemId = crate.StoredItemId;
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        ReservedDroppedCargoLoot[viewID] = photonView.OwnerActorNr;
        photonView.RPC(nameof(ReceivePendingDroppedCargoLootRpc), photonView.Owner, viewID, itemId);
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
    void RequestUseExtraction(int viewID)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        PhotonView pv = PhotonView.Find(viewID);
        if (pv != null)
        {
            ExtractionZone ez = pv.GetComponent<ExtractionZone>();
            if (ez != null)
            {
                ez.TryUse(photonView);
            }
        }
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
            bool stored = await PlayerProfileService.Instance.AddItemToShipAsync(itemId);
            if (stored)
            {
                AddScore(RoundXpTracker.RecordWreckLooted(photonView.Owner, false));
                ShowPickupToast(itemId);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to receive wreck loot item: " + ex);
        }
    }

    [PunRPC]
    async void ReceivePendingWreckLootRpc(int wreckViewId, int lootIndex, string itemId)
    {
        bool playerWreck = false;
        PhotonView wreckView = PhotonView.Find(wreckViewId);
        if (wreckView != null)
        {
            ShipWreck wreck = wreckView.GetComponent<ShipWreck>();
            playerWreck = wreck != null && wreck.SourceShipSkinIndex >= 0;
        }

        bool stored = false;
        try
        {
            stored = await PlayerProfileService.Instance.AddItemToShipAsync(itemId);
            if (stored)
            {
                AddScore(RoundXpTracker.RecordWreckLooted(photonView.Owner, playerWreck));
                ShowPickupToast(itemId);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to store reserved wreck loot item: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ResolveReservedWreckLoot), RpcTarget.MasterClient, wreckViewId, lootIndex, itemId, stored);
    }

    [PunRPC]
    async void ReceivePendingDroppedCargoLootRpc(int crateViewId, string itemId)
    {
        bool stored = false;
        try
        {
            stored = await PlayerProfileService.Instance.AddItemToShipAsync(itemId);
            if (stored)
            {
                AddScore(RoundXpTracker.RecordDroppedCargoLooted(photonView.Owner));
                ShowPickupToast(itemId);
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
    void ResolveReservedWreckLoot(int wreckViewId, int lootIndex, string itemId, bool stored)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!ReservedWreckLoot.TryGetValue(wreckViewId, out int reservedActor) || reservedActor != photonView.OwnerActorNr)
            return;

        ReservedWreckLoot.Remove(wreckViewId);
        if (!stored)
            return;

        PhotonView wreckView = PhotonView.Find(wreckViewId);
        if (wreckView == null)
            return;

        ShipWreck wreck = wreckView.GetComponent<ShipWreck>();
        if (wreck == null || !wreck.HasLoot)
            return;

        string currentItemId = wreck.GetLootItemAt(lootIndex);
        if (!string.Equals(currentItemId, itemId, System.StringComparison.Ordinal))
            return;

        wreckView.RPC(nameof(ShipWreck.RemoveLootAtIndexRpc), RpcTarget.All, lootIndex);
    }

    [PunRPC]
    void ResolveReservedDroppedCargoLoot(int crateViewId, string itemId, bool stored)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!ReservedDroppedCargoLoot.TryGetValue(crateViewId, out int reservedActor) || reservedActor != photonView.OwnerActorNr)
            return;

        ReservedDroppedCargoLoot.Remove(crateViewId);
        if (!stored)
            return;

        PhotonView crateView = PhotonView.Find(crateViewId);
        if (crateView == null)
            return;

        DroppedCargoCrate crate = crateView.GetComponent<DroppedCargoCrate>();
        if (crate == null || !crate.HasLoot || !string.Equals(crate.StoredItemId, itemId, System.StringComparison.Ordinal))
            return;

        crateView.RPC(nameof(DroppedCargoCrate.ClearStoredItemRpc), RpcTarget.All);
        PhotonNetwork.Destroy(crateView.gameObject);
    }

    async void StoreCollectedItem(string itemId)
    {
        try
        {
            bool stored = await PlayerProfileService.Instance.AddItemToShipAsync(itemId);
            if (stored)
            {
                ShowPickupToast(itemId);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to store collected item: " + ex);
        }
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
