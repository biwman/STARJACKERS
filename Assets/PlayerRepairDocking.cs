using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerRepairDocking : MonoBehaviourPun
{
    const float HoldToLandSeconds = 1f;
    const float LandingDuration = 2f;
    const float LaunchDuration = 2f;
    const float DockedScaleMultiplier = 0.7f;
    const float RepairPerSecond = 10f;
    const float RepairTickSeconds = 0.2f;
    const float FactoryExchangeStartDelay = 0.2f;
    const float FactoryContainerReleaseIntervalSeconds = 1f;
    const float ScienceExchangeStartDelay = 0.25f;
    const float ScienceScrapReleaseIntervalSeconds = 1f;

    enum DockState
    {
        None,
        Holding,
        Landing,
        Repairing,
        Launching
    }

    enum DockMode
    {
        None,
        RepairBay,
        SpaceFactory,
        ScienceStation
    }

    DockState state;
    DockMode dockMode;
    Coroutine routine;
    Coroutine factoryExchangeRoutine;
    Coroutine scienceExchangeRoutine;
    PlayerMovement movement;
    PlayerShooting shooting;
    Rigidbody2D body;
    Vector3 originalScale;
    Vector3 dockedScale;
    RigidbodyType2D originalBodyType;
    RigidbodyConstraints2D originalConstraints;
    bool originalSimulated = true;
    bool dockingPhysicsLocked;
    float repairAccumulator;
    string activeBayId;
    string activeFactoryId;
    string activeScienceStationId;

    public bool IsBusy => state != DockState.None;
    public bool IsDamageImmune => state == DockState.Landing || state == DockState.Repairing || state == DockState.Launching;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        shooting = GetComponent<PlayerShooting>();
        body = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;
        dockedScale = originalScale * DockedScaleMultiplier;
    }

    void OnDisable()
    {
        if (routine != null)
            StopCoroutine(routine);
        if (factoryExchangeRoutine != null)
            StopCoroutine(factoryExchangeRoutine);
        factoryExchangeRoutine = null;
        if (scienceExchangeRoutine != null)
            StopCoroutine(scienceExchangeRoutine);
        scienceExchangeRoutine = null;
        ReleaseActiveDockOccupancy();
        RestoreDockingPhysicsLock();
        RestoreControls();
        state = DockState.None;
        dockMode = DockMode.None;
        activeBayId = null;
        activeFactoryId = null;
        activeScienceStationId = null;
    }

    void Update()
    {
        if (state == DockState.Repairing)
            MaintainDockedPosition();

        if (!PhotonNetwork.IsMasterClient || state != DockState.Repairing || dockMode != DockMode.RepairBay)
            return;

        repairAccumulator += Time.deltaTime;
        while (repairAccumulator >= RepairTickSeconds)
        {
            repairAccumulator -= RepairTickSeconds;
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health == null || health.IsWreck || health.IsEvacuationAnimating)
            {
                photonView.RPC(nameof(ForceEndRepairDocking), RpcTarget.All);
                return;
            }

            health.RepairVitalsAuthority(Mathf.RoundToInt(RepairPerSecond * RepairTickSeconds));
            if (health.HasFullVitals)
            {
                photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
                return;
            }
        }
    }

    public bool TryStartUse(RepairBay bay, SpaceFactory factory = null, ScienceStation scienceStation = null)
    {
        if (!photonView.IsMine)
            return false;

        if (state == DockState.Repairing)
        {
            if (dockMode == DockMode.RepairBay)
                photonView.RPC(nameof(RequestRepairLaunch), RpcTarget.MasterClient);
            return true;
        }

        if (state != DockState.None)
            return true;

        if (bay != null)
        {
            photonView.RPC(nameof(RequestRepairDock), RpcTarget.MasterClient, bay.StableId);
            return true;
        }

        if (factory != null)
        {
            photonView.RPC(nameof(RequestFactoryDock), RpcTarget.MasterClient, factory.StableId);
            return true;
        }

        if (scienceStation != null)
        {
            photonView.RPC(nameof(RequestScienceStationDock), RpcTarget.MasterClient, scienceStation.StableId);
            return true;
        }

        return false;
    }

    public void StopUseHold()
    {
        if (state != DockState.Holding)
            return;

        if (routine != null)
            StopCoroutine(routine);
        routine = null;
        state = DockState.None;
    }

    IEnumerator HoldToLandRoutine(RepairBay bay)
    {
        state = DockState.Holding;
        float elapsed = 0f;
        while (elapsed < HoldToLandSeconds)
        {
            if (bay == null || Vector2.Distance(transform.position, bay.LandingPoint) > RepairBay.InteractionRadius + 0.45f)
            {
                state = DockState.None;
                routine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        state = DockState.None;
        routine = null;
        photonView.RPC(nameof(RequestRepairDock), RpcTarget.MasterClient, bay.StableId);
    }

    IEnumerator HoldToLandFactoryRoutine(SpaceFactory factory)
    {
        state = DockState.Holding;
        float elapsed = 0f;
        while (elapsed < HoldToLandSeconds)
        {
            if (factory == null || Vector2.Distance(transform.position, factory.LandingPoint) > SpaceFactory.InteractionRadius + 0.45f)
            {
                state = DockState.None;
                routine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        state = DockState.None;
        routine = null;
        photonView.RPC(nameof(RequestFactoryDock), RpcTarget.MasterClient, factory.StableId);
    }

    IEnumerator HoldToLandScienceStationRoutine(ScienceStation scienceStation)
    {
        state = DockState.Holding;
        float elapsed = 0f;
        while (elapsed < HoldToLandSeconds)
        {
            if (scienceStation == null || Vector2.Distance(transform.position, scienceStation.LandingPoint) > ScienceStation.InteractionRadius + 0.45f)
            {
                state = DockState.None;
                routine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        state = DockState.None;
        routine = null;
        photonView.RPC(nameof(RequestScienceStationDock), RpcTarget.MasterClient, scienceStation.StableId);
    }

    [PunRPC]
    void RequestRepairDock(string bayId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || info.Sender == null || info.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        PlayerHealth health = GetComponent<PlayerHealth>();
        RepairBay bay = RepairBay.Find(bayId);
        if (health == null || bay == null || health.IsWreck || health.IsEvacuationAnimating || health.IsAstronautControlled)
            return;

        if (Vector2.Distance(transform.position, bay.LandingPoint) > RepairBay.InteractionRadius + 0.85f)
            return;

        if (!TryReserveRepairBayOccupancy(bay.StableId, photonView.Owner.ActorNumber))
            return;

        Vector3 landingPoint = bay.LandingPoint;
        activeBayId = bay.StableId;
        activeFactoryId = null;
        activeScienceStationId = null;
        dockMode = DockMode.RepairBay;
        photonView.RPC(nameof(BeginRepairLanding), RpcTarget.All, bayId, landingPoint.x, landingPoint.y);
    }

    [PunRPC]
    void RequestFactoryDock(string factoryId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || info.Sender == null || info.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        PlayerHealth health = GetComponent<PlayerHealth>();
        SpaceFactory factory = SpaceFactory.Find(factoryId);
        if (health == null || factory == null || health.IsWreck || health.IsEvacuationAnimating || health.IsAstronautControlled)
            return;

        if (Vector2.Distance(transform.position, factory.LandingPoint) > SpaceFactory.InteractionRadius + 0.85f)
            return;

        if (!TryReserveSpaceFactoryOccupancy(factory.StableId, photonView.Owner.ActorNumber))
            return;

        Vector3 landingPoint = factory.LandingPoint;
        activeFactoryId = factory.StableId;
        activeBayId = null;
        activeScienceStationId = null;
        dockMode = DockMode.SpaceFactory;
        photonView.RPC(nameof(BeginFactoryLanding), RpcTarget.All, factoryId, landingPoint.x, landingPoint.y);
    }

    [PunRPC]
    void RequestScienceStationDock(string stationId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || info.Sender == null || info.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        PlayerHealth health = GetComponent<PlayerHealth>();
        ScienceStation station = ScienceStation.Find(stationId);
        if (health == null || station == null || health.IsWreck || health.IsEvacuationAnimating || health.IsAstronautControlled)
            return;

        if (Vector2.Distance(transform.position, station.LandingPoint) > ScienceStation.InteractionRadius + 0.85f)
            return;

        if (!TryReserveScienceStationOccupancy(station.StableId, photonView.Owner.ActorNumber))
            return;

        Vector3 landingPoint = station.LandingPoint;
        activeScienceStationId = station.StableId;
        activeBayId = null;
        activeFactoryId = null;
        dockMode = DockMode.ScienceStation;
        photonView.RPC(nameof(BeginScienceStationLanding), RpcTarget.All, stationId, landingPoint.x, landingPoint.y);
    }

    [PunRPC]
    void RequestRepairLaunch(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || info.Sender == null || info.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        if (state == DockState.Repairing || state == DockState.Landing)
            photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
    }

    [PunRPC]
    void BeginRepairLanding(string bayId, float landingX, float landingY)
    {
        StopActiveRoutine();
        activeBayId = bayId;
        activeFactoryId = null;
        activeScienceStationId = null;
        dockMode = DockMode.RepairBay;
        routine = StartCoroutine(LandingRoutine(new Vector3(landingX, landingY, transform.position.z)));
    }

    [PunRPC]
    void BeginFactoryLanding(string factoryId, float landingX, float landingY)
    {
        StopActiveRoutine();
        activeFactoryId = factoryId;
        activeBayId = null;
        activeScienceStationId = null;
        dockMode = DockMode.SpaceFactory;
        routine = StartCoroutine(LandingRoutine(new Vector3(landingX, landingY, transform.position.z)));
    }

    [PunRPC]
    void BeginScienceStationLanding(string stationId, float landingX, float landingY)
    {
        StopActiveRoutine();
        activeScienceStationId = stationId;
        activeBayId = null;
        activeFactoryId = null;
        dockMode = DockMode.ScienceStation;
        routine = StartCoroutine(LandingRoutine(new Vector3(landingX, landingY, transform.position.z)));
    }

    IEnumerator LandingRoutine(Vector3 landingPoint)
    {
        state = DockState.Landing;
        LockControls();
        AudioManager.Instance.PlayRepairBayLandingAt(landingPoint);

        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < LandingDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(Mathf.Clamp01(elapsed / LandingDuration));
            transform.position = Vector3.Lerp(startPosition, landingPoint, t);
            transform.localScale = Vector3.Lerp(startScale, dockedScale, t);
            ZeroVelocity();
            yield return null;
        }

        transform.position = ResolveActiveLandingPoint(landingPoint);
        transform.localScale = dockedScale;
        ZeroVelocity();

        PlayerHealth health = GetComponent<PlayerHealth>();
        if (dockMode == DockMode.RepairBay && PhotonNetwork.IsMasterClient && health != null && health.HasFullVitals)
        {
            photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
            yield break;
        }

        state = DockState.Repairing;
        repairAccumulator = 0f;
        routine = null;

        if (dockMode == DockMode.SpaceFactory && photonView.IsMine)
            factoryExchangeRoutine = StartCoroutine(FactoryExchangeRoutine(activeFactoryId));
        else if (dockMode == DockMode.ScienceStation && photonView.IsMine)
            scienceExchangeRoutine = StartCoroutine(ScienceStationExchangeRoutine(activeScienceStationId));
    }

    [PunRPC]
    void BeginRepairLaunch()
    {
        StopActiveRoutine();
        StopFactoryExchangeRoutine();
        StopScienceExchangeRoutine();
        routine = StartCoroutine(LaunchRoutine());
    }

    IEnumerator LaunchRoutine()
    {
        state = DockState.Launching;
        LockControls();
        AudioManager.Instance.PlayRepairBayStartingAt(transform.position);

        Vector3 startScale = transform.localScale;
        Vector3 startPosition = transform.position;
        Vector3 launchOffset = transform.up.sqrMagnitude > 0.001f ? transform.up.normalized * 0.75f : Vector3.up * 0.75f;
        Vector3 endPosition = startPosition + launchOffset;
        float elapsed = 0f;
        while (elapsed < LaunchDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(Mathf.Clamp01(elapsed / LaunchDuration));
            transform.position = Vector3.Lerp(startPosition, endPosition, t);
            transform.localScale = Vector3.Lerp(startScale, originalScale, t);
            ZeroVelocity();
            yield return null;
        }

        transform.localScale = originalScale;
        RestoreDockingPhysicsLock();
        ReleaseActiveDockOccupancy();
        state = DockState.None;
        activeBayId = null;
        activeFactoryId = null;
        activeScienceStationId = null;
        dockMode = DockMode.None;
        routine = null;
        RestoreControls();
    }

    [PunRPC]
    void ForceEndRepairDocking()
    {
        StopActiveRoutine();
        StopFactoryExchangeRoutine();
        StopScienceExchangeRoutine();
        ReleaseActiveDockOccupancy();
        state = DockState.None;
        activeBayId = null;
        activeFactoryId = null;
        activeScienceStationId = null;
        dockMode = DockMode.None;
        transform.localScale = originalScale;
        RestoreDockingPhysicsLock();
        RestoreControls();
    }

    void LockControls()
    {
        ApplyDockingPhysicsLock();

        if (!photonView.IsMine)
            return;

        if (movement != null)
            movement.enabled = false;
        if (shooting != null)
            shooting.enabled = false;
    }

    void RestoreControls()
    {
        if (!photonView.IsMine)
            return;

        PlayerHealth health = GetComponent<PlayerHealth>();
        bool canRestore = health == null || (!health.IsWreck && !health.IsEvacuationAnimating);
        if (!canRestore)
            return;

        if (movement != null)
            movement.enabled = true;
        if (shooting != null)
            shooting.enabled = true;
    }

    void ApplyDockingPhysicsLock()
    {
        if (body == null)
            return;

        if (!dockingPhysicsLocked)
        {
            originalBodyType = body.bodyType;
            originalConstraints = body.constraints;
            originalSimulated = body.simulated;
            dockingPhysicsLocked = true;
        }

        body.simulated = true;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        ZeroVelocity();
    }

    void RestoreDockingPhysicsLock()
    {
        if (body == null || !dockingPhysicsLocked)
            return;

        body.bodyType = originalBodyType;
        body.constraints = originalConstraints;
        body.simulated = originalSimulated;
        ZeroVelocity();
        dockingPhysicsLocked = false;
    }

    void MaintainDockedPosition()
    {
        if (string.IsNullOrWhiteSpace(activeBayId) &&
            string.IsNullOrWhiteSpace(activeFactoryId) &&
            string.IsNullOrWhiteSpace(activeScienceStationId))
        {
            return;
        }

        transform.position = ResolveActiveLandingPoint(transform.position);
        transform.localScale = dockedScale;
        ZeroVelocity();
    }

    Vector3 ResolveActiveLandingPoint(Vector3 fallback)
    {
        RepairBay bay = RepairBay.Find(activeBayId);
        if (bay == null)
        {
            SpaceFactory factory = SpaceFactory.Find(activeFactoryId);
            if (factory == null)
            {
                ScienceStation scienceStation = ScienceStation.Find(activeScienceStationId);
                if (scienceStation == null)
                    return fallback;

                Vector3 scienceLandingPoint = scienceStation.LandingPoint;
                return new Vector3(scienceLandingPoint.x, scienceLandingPoint.y, fallback.z);
            }

            Vector3 factoryLandingPoint = factory.LandingPoint;
            return new Vector3(factoryLandingPoint.x, factoryLandingPoint.y, fallback.z);
        }

        Vector3 landingPoint = bay.LandingPoint;
        return new Vector3(landingPoint.x, landingPoint.y, fallback.z);
    }

    void StopActiveRoutine()
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = null;
    }

    void StopFactoryExchangeRoutine()
    {
        if (factoryExchangeRoutine != null)
            StopCoroutine(factoryExchangeRoutine);
        factoryExchangeRoutine = null;
    }

    void StopScienceExchangeRoutine()
    {
        if (scienceExchangeRoutine != null)
            StopCoroutine(scienceExchangeRoutine);
        scienceExchangeRoutine = null;
    }

    void ReleaseActiveDockOccupancy()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        int actorNumber = photonView != null && photonView.Owner != null ? photonView.Owner.ActorNumber : -1;
        if (!string.IsNullOrWhiteSpace(activeBayId))
            ReleaseRepairBayOccupancy(activeBayId, actorNumber);

        if (!string.IsNullOrWhiteSpace(activeFactoryId))
            ReleaseSpaceFactoryOccupancy(activeFactoryId, actorNumber);

        if (!string.IsNullOrWhiteSpace(activeScienceStationId))
            ReleaseScienceStationOccupancy(activeScienceStationId, actorNumber);
    }

    IEnumerator FactoryExchangeRoutine(string factoryId)
    {
        yield return new WaitForSeconds(FactoryExchangeStartDelay);

        if (!PlayerProfileService.HasInstance)
        {
            factoryExchangeRoutine = null;
            yield break;
        }

        if (string.IsNullOrWhiteSpace(factoryId) || SpaceFactory.IsCompleted(factoryId))
        {
            RequestFactoryLaunchFromOwner();
            factoryExchangeRoutine = null;
            yield break;
        }

        int openSlots = Mathf.Clamp(
            SpaceFactory.RequiredContainerCount - SpaceFactory.GetFilledCount(factoryId),
            0,
            SpaceFactory.RequiredContainerCount);
        if (openSlots <= 0 || !SpaceFactory.CanAcceptContainer(factoryId))
        {
            RequestFactoryLaunchFromOwner();
            factoryExchangeRoutine = null;
            yield break;
        }

        System.Threading.Tasks.Task<string[]> removeTask = PlayerProfileService.Instance.RemoveShipContainersDeferredSaveAsync(openSlots);
        while (!removeTask.IsCompleted)
        {
            if (state != DockState.Repairing || dockMode != DockMode.SpaceFactory)
            {
                factoryExchangeRoutine = null;
                yield break;
            }

            yield return null;
        }

        if (removeTask.IsFaulted)
        {
            Debug.LogError("Space Factory container removal failed: " + removeTask.Exception);
            RequestFactoryLaunchFromOwner();
            factoryExchangeRoutine = null;
            yield break;
        }

        string[] removedContainerIds = removeTask.Result;
        if (removedContainerIds == null || removedContainerIds.Length == 0)
        {
            RequestFactoryLaunchFromOwner();
            factoryExchangeRoutine = null;
            yield break;
        }

        for (int i = 0; i < removedContainerIds.Length; i++)
        {
            if (state != DockState.Repairing || dockMode != DockMode.SpaceFactory)
            {
                RestoreRemainingFactoryContainers(removedContainerIds, i);
                factoryExchangeRoutine = null;
                yield break;
            }

            if (SpaceFactory.IsCompleted(factoryId) || !SpaceFactory.CanAcceptContainer(factoryId))
            {
                RestoreRemainingFactoryContainers(removedContainerIds, i);
                break;
            }

            string removedContainerId = removedContainerIds[i];
            if (string.IsNullOrWhiteSpace(removedContainerId))
                continue;

            photonView.RPC(nameof(RequestFactoryContainerDeposit), RpcTarget.MasterClient, factoryId, removedContainerId);
            float elapsed = 0f;
            while (elapsed < FactoryContainerReleaseIntervalSeconds)
            {
                if (state != DockState.Repairing || dockMode != DockMode.SpaceFactory)
                {
                    RestoreRemainingFactoryContainers(removedContainerIds, i + 1);
                    factoryExchangeRoutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (state == DockState.Repairing && dockMode == DockMode.SpaceFactory)
            RequestFactoryLaunchFromOwner();

        factoryExchangeRoutine = null;
    }

    void RestoreRemainingFactoryContainers(string[] removedContainerIds, int startIndex)
    {
        string[] remaining = BuildRejectedFactoryContainers(removedContainerIds, startIndex);
        if (remaining.Length > 0)
            RestoreRejectedFactoryContainersRpc(remaining);
    }

    void RequestFactoryLaunchFromOwner()
    {
        if (!photonView.IsMine)
            return;

        photonView.RPC(nameof(RequestFactoryLaunch), RpcTarget.MasterClient);
    }

    IEnumerator ScienceStationExchangeRoutine(string stationId)
    {
        yield return new WaitForSeconds(ScienceExchangeStartDelay);

        if (!PlayerProfileService.HasInstance || string.IsNullOrWhiteSpace(stationId))
        {
            RequestScienceStationLaunchFromOwner();
            scienceExchangeRoutine = null;
            yield break;
        }

        int processedScrapCount = 0;
        while (state == DockState.Repairing && dockMode == DockMode.ScienceStation)
        {
            System.Threading.Tasks.Task<string> removeTask = PlayerProfileService.Instance.RemoveFirstShipItemDeferredSaveAsync(InventoryItemCatalog.BlueprintScrapId);
            while (!removeTask.IsCompleted)
            {
                if (state != DockState.Repairing || dockMode != DockMode.ScienceStation)
                {
                    scienceExchangeRoutine = null;
                    yield break;
                }

                yield return null;
            }

            if (removeTask.IsFaulted)
            {
                Debug.LogError("Science Station blueprint scrap removal failed: " + removeTask.Exception);
                RequestScienceStationLaunchFromOwner();
                scienceExchangeRoutine = null;
                yield break;
            }

            if (string.IsNullOrWhiteSpace(removeTask.Result))
                break;

            processedScrapCount++;

            float elapsed = 0f;
            while (elapsed < ScienceScrapReleaseIntervalSeconds)
            {
                if (state != DockState.Repairing || dockMode != DockMode.ScienceStation)
                {
                    scienceExchangeRoutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (state == DockState.Repairing && dockMode == DockMode.ScienceStation)
        {
            if (processedScrapCount > 0)
                photonView.RPC(nameof(RequestScienceStationReward), RpcTarget.MasterClient, stationId, processedScrapCount);
            else
                RequestScienceStationLaunchFromOwner();
        }

        scienceExchangeRoutine = null;
    }

    void RequestScienceStationLaunchFromOwner()
    {
        if (!photonView.IsMine)
            return;

        photonView.RPC(nameof(RequestScienceStationLaunch), RpcTarget.MasterClient);
    }

    [PunRPC]
    void RequestFactoryContainerDeposit(string factoryId, string itemId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || info.Sender == null || info.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        if (!IsFactoryOccupiedByActor(factoryId, photonView.Owner.ActorNumber))
        {
            photonView.RPC(nameof(RestoreRejectedFactoryContainerRpc), photonView.Owner, itemId);
            photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
            return;
        }

        bool accepted = SpaceFactory.TryDepositContainerAuthority(factoryId, itemId, out bool completedNow);
        if (!accepted)
        {
            photonView.RPC(nameof(RestoreRejectedFactoryContainerRpc), photonView.Owner, itemId);
            photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
            return;
        }

        if (completedNow)
        {
            photonView.RPC(nameof(ReceiveFactoryRewardRpc), photonView.Owner, InventoryItemCatalog.CashSuitcaseId);
            photonView.RPC(nameof(ShowFactoryRichMessageRpc), RpcTarget.All);
            photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
        }
    }

    [PunRPC]
    void RequestFactoryContainerBatchDeposit(string factoryId, string[] itemIds, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || info.Sender == null || info.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        if (!IsFactoryOccupiedByActor(factoryId, photonView.Owner.ActorNumber))
        {
            photonView.RPC(nameof(RestoreRejectedFactoryContainersRpc), photonView.Owner, new object[] { itemIds });
            photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
            return;
        }

        bool accepted = SpaceFactory.TryDepositContainersAuthority(factoryId, itemIds, out int acceptedCount, out bool completedNow);
        if (!accepted)
        {
            photonView.RPC(nameof(RestoreRejectedFactoryContainersRpc), photonView.Owner, new object[] { itemIds });
            photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
            return;
        }

        if (itemIds != null && acceptedCount < itemIds.Length)
            photonView.RPC(nameof(RestoreRejectedFactoryContainersRpc), photonView.Owner, new object[] { BuildRejectedFactoryContainers(itemIds, acceptedCount) });

        if (completedNow)
        {
            photonView.RPC(nameof(ReceiveFactoryRewardRpc), photonView.Owner, InventoryItemCatalog.CashSuitcaseId);
            photonView.RPC(nameof(ShowFactoryRichMessageRpc), RpcTarget.All);
        }
    }

    static string[] BuildRejectedFactoryContainers(string[] itemIds, int acceptedCount)
    {
        if (itemIds == null || acceptedCount >= itemIds.Length)
            return new string[0];

        int startIndex = Mathf.Clamp(acceptedCount, 0, itemIds.Length);
        string[] rejected = new string[itemIds.Length - startIndex];
        for (int i = startIndex; i < itemIds.Length; i++)
            rejected[i - startIndex] = itemIds[i];

        return rejected;
    }

    [PunRPC]
    void RequestFactoryLaunch(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || info.Sender == null || info.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        if ((state == DockState.Repairing || state == DockState.Landing) && dockMode == DockMode.SpaceFactory)
            photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
    }

    [PunRPC]
    void RequestScienceStationReward(string stationId, int scrapCount, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || info.Sender == null || info.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        if (!IsScienceStationOccupiedByActor(stationId, photonView.Owner.ActorNumber))
        {
            photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
            return;
        }

        int safeScrapCount = Mathf.Clamp(scrapCount, 1, PlayerInventoryData.ShipSlotCount);
        string blueprintItemId = BlueprintCatalog.RollScienceStationBlueprint(safeScrapCount);
        if (string.IsNullOrWhiteSpace(blueprintItemId))
        {
            photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
            return;
        }

        photonView.RPC(nameof(ReceiveScienceStationRewardRpc), photonView.Owner, blueprintItemId, safeScrapCount);
        photonView.RPC(nameof(ShowScienceStationRewardRpc), photonView.Owner, blueprintItemId, safeScrapCount);
        photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
    }

    [PunRPC]
    void RequestScienceStationLaunch(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || info.Sender == null || info.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        if ((state == DockState.Repairing || state == DockState.Landing) && dockMode == DockMode.ScienceStation)
            photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
    }

    [PunRPC]
    async void ReceiveFactoryRewardRpc(string itemId)
    {
        if (!photonView.IsMine || string.IsNullOrWhiteSpace(itemId))
            return;

        try
        {
            await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(itemId);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to add Space Factory reward: " + ex);
        }
    }

    [PunRPC]
    async void ReceiveScienceStationRewardRpc(string itemId, int scrapCount)
    {
        if (!photonView.IsMine || string.IsNullOrWhiteSpace(itemId))
            return;

        try
        {
            await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(itemId);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to add Science Station blueprint reward: " + ex);
        }
    }

    [PunRPC]
    async void RestoreRejectedFactoryContainerRpc(string itemId)
    {
        if (!photonView.IsMine || string.IsNullOrWhiteSpace(itemId))
            return;

        try
        {
            await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(itemId);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to restore rejected Space Factory container: " + ex);
        }
    }

    [PunRPC]
    async void RestoreRejectedFactoryContainersRpc(string[] itemIds)
    {
        if (!photonView.IsMine || itemIds == null || itemIds.Length == 0)
            return;

        try
        {
            await PlayerProfileService.Instance.AddItemsToShipDeferredSaveAsync(itemIds);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to restore rejected Space Factory containers: " + ex);
        }
    }

    [PunRPC]
    void ShowFactoryRichMessageRpc()
    {
        RoundAnnouncementUI.Show("SOMEONE BECAME RICH!");
    }

    [PunRPC]
    void ShowScienceStationRewardRpc(string itemId, int scrapCount)
    {
        if (!photonView.IsMine || string.IsNullOrWhiteSpace(itemId))
            return;

        RoundAnnouncementUI.Show("BLUEPRINT DECODED: " + InventoryItemCatalog.GetDisplayName(itemId));
    }

    bool TryReserveRepairBayOccupancy(string bayId, int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || string.IsNullOrWhiteSpace(bayId) || actorNumber <= 0)
            return false;

        Dictionary<string, int> occupancy = DeserializeRepairBayOccupancy(GetRepairBayOccupancyStateRaw());
        RemoveStaleOccupants(occupancy);

        if (IsActorOccupyingAnotherBay(occupancy, bayId, actorNumber))
        {
            PublishRepairBayOccupancy(occupancy);
            return false;
        }

        if (occupancy.TryGetValue(bayId, out int occupiedByActor) &&
            occupiedByActor > 0 &&
            occupiedByActor != actorNumber &&
            IsActorInRoom(occupiedByActor))
        {
            PublishRepairBayOccupancy(occupancy);
            return false;
        }

        occupancy[bayId] = actorNumber;
        PublishRepairBayOccupancy(occupancy);
        return true;
    }

    void ReleaseRepairBayOccupancy(string bayId, int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || string.IsNullOrWhiteSpace(bayId))
            return;

        Dictionary<string, int> occupancy = DeserializeRepairBayOccupancy(GetRepairBayOccupancyStateRaw());
        RemoveStaleOccupants(occupancy);

        if (occupancy.TryGetValue(bayId, out int occupiedByActor) &&
            (actorNumber <= 0 || occupiedByActor == actorNumber || !IsActorInRoom(occupiedByActor)))
        {
            occupancy.Remove(bayId);
            PublishRepairBayOccupancy(occupancy);
        }
        else
        {
            PublishRepairBayOccupancy(occupancy);
        }
    }

    bool TryReserveSpaceFactoryOccupancy(string factoryId, int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || string.IsNullOrWhiteSpace(factoryId) || actorNumber <= 0)
            return false;

        Dictionary<string, int> occupancy = DeserializeRepairBayOccupancy(GetSpaceFactoryOccupancyStateRaw());
        RemoveStaleOccupants(occupancy);

        if (IsActorOccupyingAnotherBay(occupancy, factoryId, actorNumber))
        {
            PublishSpaceFactoryOccupancy(occupancy);
            return false;
        }

        if (occupancy.TryGetValue(factoryId, out int occupiedByActor) &&
            occupiedByActor > 0 &&
            occupiedByActor != actorNumber &&
            IsActorInRoom(occupiedByActor))
        {
            PublishSpaceFactoryOccupancy(occupancy);
            return false;
        }

        occupancy[factoryId] = actorNumber;
        PublishSpaceFactoryOccupancy(occupancy);
        return true;
    }

    void ReleaseSpaceFactoryOccupancy(string factoryId, int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || string.IsNullOrWhiteSpace(factoryId))
            return;

        Dictionary<string, int> occupancy = DeserializeRepairBayOccupancy(GetSpaceFactoryOccupancyStateRaw());
        RemoveStaleOccupants(occupancy);

        if (occupancy.TryGetValue(factoryId, out int occupiedByActor) &&
            (actorNumber <= 0 || occupiedByActor == actorNumber || !IsActorInRoom(occupiedByActor)))
        {
            occupancy.Remove(factoryId);
            PublishSpaceFactoryOccupancy(occupancy);
        }
        else
        {
            PublishSpaceFactoryOccupancy(occupancy);
        }
    }

    bool IsFactoryOccupiedByActor(string factoryId, int actorNumber)
    {
        if (string.IsNullOrWhiteSpace(factoryId) || actorNumber <= 0)
            return false;

        Dictionary<string, int> occupancy = DeserializeRepairBayOccupancy(GetSpaceFactoryOccupancyStateRaw());
        RemoveStaleOccupants(occupancy);
        return occupancy.TryGetValue(factoryId, out int occupiedByActor) && occupiedByActor == actorNumber;
    }

    bool TryReserveScienceStationOccupancy(string stationId, int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || string.IsNullOrWhiteSpace(stationId) || actorNumber <= 0)
            return false;

        Dictionary<string, int> occupancy = DeserializeRepairBayOccupancy(GetScienceStationOccupancyStateRaw());
        RemoveStaleOccupants(occupancy);

        if (IsActorOccupyingAnotherBay(occupancy, stationId, actorNumber))
        {
            PublishScienceStationOccupancy(occupancy);
            return false;
        }

        if (occupancy.TryGetValue(stationId, out int occupiedByActor) &&
            occupiedByActor > 0 &&
            occupiedByActor != actorNumber &&
            IsActorInRoom(occupiedByActor))
        {
            PublishScienceStationOccupancy(occupancy);
            return false;
        }

        occupancy[stationId] = actorNumber;
        PublishScienceStationOccupancy(occupancy);
        return true;
    }

    void ReleaseScienceStationOccupancy(string stationId, int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || string.IsNullOrWhiteSpace(stationId))
            return;

        Dictionary<string, int> occupancy = DeserializeRepairBayOccupancy(GetScienceStationOccupancyStateRaw());
        RemoveStaleOccupants(occupancy);

        if (occupancy.TryGetValue(stationId, out int occupiedByActor) &&
            (actorNumber <= 0 || occupiedByActor == actorNumber || !IsActorInRoom(occupiedByActor)))
        {
            occupancy.Remove(stationId);
            PublishScienceStationOccupancy(occupancy);
        }
        else
        {
            PublishScienceStationOccupancy(occupancy);
        }
    }

    bool IsScienceStationOccupiedByActor(string stationId, int actorNumber)
    {
        if (string.IsNullOrWhiteSpace(stationId) || actorNumber <= 0)
            return false;

        Dictionary<string, int> occupancy = DeserializeRepairBayOccupancy(GetScienceStationOccupancyStateRaw());
        RemoveStaleOccupants(occupancy);
        return occupancy.TryGetValue(stationId, out int occupiedByActor) && occupiedByActor == actorNumber;
    }

    static string GetRepairBayOccupancyStateRaw()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.RepairBayOccupancyStateKey, out object value) &&
            value is string raw)
        {
            return raw;
        }

        return string.Empty;
    }

    static string GetSpaceFactoryOccupancyStateRaw()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.SpaceFactoryOccupancyStateKey, out object value) &&
            value is string raw)
        {
            return raw;
        }

        return string.Empty;
    }

    static string GetScienceStationOccupancyStateRaw()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.ScienceStationOccupancyStateKey, out object value) &&
            value is string raw)
        {
            return raw;
        }

        return string.Empty;
    }

    static Dictionary<string, int> DeserializeRepairBayOccupancy(string raw)
    {
        Dictionary<string, int> occupancy = new Dictionary<string, int>(System.StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw))
            return occupancy;

        string[] entries = raw.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            int separatorIndex = entry.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex >= entry.Length - 1)
                continue;

            string bayId = entry.Substring(0, separatorIndex);
            string actorRaw = entry.Substring(separatorIndex + 1);
            if (string.IsNullOrWhiteSpace(bayId) || !int.TryParse(actorRaw, out int actorNumber) || actorNumber <= 0)
                continue;

            occupancy[bayId] = actorNumber;
        }

        return occupancy;
    }

    static void PublishRepairBayOccupancy(Dictionary<string, int> occupancy)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.RepairBayOccupancyStateKey] = SerializeRepairBayOccupancy(occupancy)
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    static void PublishSpaceFactoryOccupancy(Dictionary<string, int> occupancy)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.SpaceFactoryOccupancyStateKey] = SerializeRepairBayOccupancy(occupancy)
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    static void PublishScienceStationOccupancy(Dictionary<string, int> occupancy)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.ScienceStationOccupancyStateKey] = SerializeRepairBayOccupancy(occupancy)
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    static string SerializeRepairBayOccupancy(Dictionary<string, int> occupancy)
    {
        if (occupancy == null || occupancy.Count == 0)
            return string.Empty;

        List<string> bayIds = new List<string>(occupancy.Keys);
        bayIds.Sort(System.StringComparer.Ordinal);

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < bayIds.Count; i++)
        {
            string bayId = bayIds[i];
            if (string.IsNullOrWhiteSpace(bayId) || !occupancy.TryGetValue(bayId, out int actorNumber) || actorNumber <= 0)
                continue;

            if (builder.Length > 0)
                builder.Append(';');

            builder.Append(bayId);
            builder.Append('=');
            builder.Append(actorNumber);
        }

        return builder.ToString();
    }

    static void RemoveStaleOccupants(Dictionary<string, int> occupancy)
    {
        if (occupancy == null || occupancy.Count == 0)
            return;

        List<string> staleBayIds = null;
        foreach (KeyValuePair<string, int> pair in occupancy)
        {
            if (IsActorInRoom(pair.Value))
                continue;

            if (staleBayIds == null)
                staleBayIds = new List<string>();

            staleBayIds.Add(pair.Key);
        }

        if (staleBayIds == null)
            return;

        for (int i = 0; i < staleBayIds.Count; i++)
            occupancy.Remove(staleBayIds[i]);
    }

    static bool IsActorOccupyingAnotherBay(Dictionary<string, int> occupancy, string requestedBayId, int actorNumber)
    {
        if (occupancy == null || actorNumber <= 0)
            return false;

        foreach (KeyValuePair<string, int> pair in occupancy)
        {
            if (pair.Value != actorNumber)
                continue;

            if (!string.Equals(pair.Key, requestedBayId, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static bool IsActorInRoom(int actorNumber)
    {
        if (actorNumber <= 0)
            return false;

        Photon.Realtime.Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].ActorNumber == actorNumber)
                return true;
        }

        return false;
    }

    void ZeroVelocity()
    {
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    float EaseInOut(float t)
    {
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }
}
