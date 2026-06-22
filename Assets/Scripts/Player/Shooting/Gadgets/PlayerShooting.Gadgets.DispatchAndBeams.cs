using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public partial class PlayerShooting
{
    [PunRPC]
    void RequestStartTractorBeam(string itemId, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || !IsGameStarted() || !string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
            return;

        if (AreShipControlsBlocked())
            return;

        if (photonView == null || photonView.Owner == null || messageInfo.Sender == null || messageInfo.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        Photon.Realtime.Player owner = photonView.Owner;
        int maxCharges = ResolveEquippedGadgetMaxCharges(owner, itemId);
        if (maxCharges <= 0)
            return;

        int remainingCharges = GetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, itemId, maxCharges);
        if (remainingCharges <= 0)
            return;

        PhotonView targetView = FindClosestTractorBeamTarget();
        if (targetView == null)
            return;

        StopAuthoritativeTractorBeam(true);
        SetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, itemId, remainingCharges - 1, maxCharges);
        RoundXpTracker.RecordGadgetSuccess(owner, itemId);
        activeTractorBeamTargetViewId = targetView.ViewID;
        activeTractorBeamItemId = itemId;
        photonView.RPC(nameof(StartTractorBeamEffects), RpcTarget.All, photonView.ViewID, targetView.ViewID);
        authoritativeTractorBeamRoutine = StartCoroutine(TractorBeamPullRoutine(targetView.ViewID));
    }

    [PunRPC]
    void RequestStopTractorBeam(string itemId, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || !string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
            return;

        if (photonView == null || photonView.Owner == null || messageInfo.Sender == null || messageInfo.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        StopAuthoritativeTractorBeam(true);
    }

    int ResolveEquippedGadgetMaxCharges(Photon.Realtime.Player owner, string itemId)
    {
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        List<string> orderedItems = new List<string>();
        Dictionary<string, int> gadgetCounts = CollectEquippedGadgetCounts(equipmentSlots, shipSkinIndex, orderedItems);
        int equippedCount = gadgetCounts.TryGetValue(itemId, out int count) ? count : 0;
        return ResolveGadgetMaxCharges(itemId, equippedCount);
    }

    void StopAuthoritativeTractorBeam(bool notifyClients)
    {
        if (authoritativeTractorBeamRoutine != null)
        {
            StopCoroutine(authoritativeTractorBeamRoutine);
            authoritativeTractorBeamRoutine = null;
        }

        if (notifyClients && photonView != null)
            photonView.RPC(nameof(StopTractorBeamEffects), RpcTarget.All, photonView.ViewID);

        activeTractorBeamTargetViewId = 0;
        activeTractorBeamItemId = null;
    }

    bool TryExecuteAuthoritativeGadgetUse(string itemId)
    {
        if (string.Equals(itemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return TryDeployGadgetMine();

        if (string.Equals(itemId, InventoryItemCatalog.SpaceBombId, StringComparison.Ordinal))
        {
            bool deployed = NewItemsRuntime.TryDeploySpaceBomb(this);
            if (deployed)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return deployed;
        }

        if (string.Equals(itemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
            return TryActivateBatteryCharge();

        if (string.Equals(itemId, InventoryItemCatalog.MagneticBeamId, StringComparison.Ordinal))
            return TryActivateMagneticBeam();

        if (string.Equals(itemId, InventoryItemCatalog.StasisBuoyId, StringComparison.Ordinal))
        {
            bool deployed = NewItemsRuntime.TryDeployStasisBuoy(this);
            if (deployed)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return deployed;
        }

        if (string.Equals(itemId, InventoryItemCatalog.TetherHarpoonId, StringComparison.Ordinal))
        {
            bool tethered = NewItemsRuntime.TryStartTetherHarpoon(this);
            if (tethered)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return tethered;
        }

        if (string.Equals(itemId, InventoryItemCatalog.SpaceTorpedoId, StringComparison.Ordinal))
        {
            bool launched = NewItemsRuntime.TryLaunchSpaceTorpedo(this);
            if (launched)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return launched;
        }

        if (string.Equals(itemId, InventoryItemCatalog.BioTrapId, StringComparison.Ordinal))
        {
            bool captured = NewItemsRuntime.TryFireBioTrap(this);
            if (captured)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return captured;
        }

        if (string.Equals(itemId, InventoryItemCatalog.AsteroidBreacherBombId, StringComparison.Ordinal))
        {
            bool breached = NewItemsRuntime.TryDetonateAsteroidBreacherBomb(this);
            if (breached)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return breached;
        }

        if (string.Equals(itemId, InventoryItemCatalog.MetalDriftWallId, StringComparison.Ordinal))
        {
            bool deployed = NewItemsRuntime.TryDeployMetalDriftWall(this);
            if (deployed)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return deployed;
        }

        if (string.Equals(itemId, InventoryItemCatalog.LureBeaconId, StringComparison.Ordinal))
            return TryDeployLureBeacon();

        if (string.Equals(itemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal))
        {
            bool deployed = NewItemsRuntime.TryDeployAutoTurret(this);
            if (deployed)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return deployed;
        }

        if (string.Equals(itemId, InventoryItemCatalog.RocketAutoTurretId, StringComparison.Ordinal))
        {
            bool deployed = NewItemsRuntime.TryDeployRocketAutoTurret(this);
            if (deployed)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return deployed;
        }

        if (string.Equals(itemId, InventoryItemCatalog.GuidanceSystemId, StringComparison.Ordinal))
        {
            photonView.RPC(nameof(ActivateGuidanceSystemRpc), photonView.Owner, GetAdjustedGuidanceSystemDuration());
            RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);
            return true;
        }

        if (string.Equals(itemId, InventoryItemCatalog.ShortScannerId, StringComparison.Ordinal))
        {
            photonView.RPC(nameof(ActivateShortScannerRevealRpc), photonView.Owner, GetAdjustedShortScannerRevealDuration());
            RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);
            return true;
        }

        if (string.Equals(itemId, InventoryItemCatalog.CloakDeviceId, StringComparison.Ordinal))
        {
            HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>();
            if (nebulaTarget != null && nebulaTarget.IsCloaked)
                return false;

            photonView.RPC(nameof(ActivateCloakDeviceRpc), RpcTarget.All, CloakDeviceDuration);
            RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);
            return true;
        }

        if (string.Equals(itemId, InventoryItemCatalog.HackingDeviceId, StringComparison.Ordinal))
            return TryStartHackingDevice(itemId);

        if (string.Equals(itemId, InventoryItemCatalog.SpaceDrillId, StringComparison.Ordinal))
        {
            bool launched = NewItemsRuntime.TryLaunchSpaceDrill(this);
            if (launched)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return launched;
        }

        if (string.Equals(itemId, InventoryItemCatalog.SpaceTrapId, StringComparison.Ordinal))
        {
            bool armed = NewItemsRuntime.TryArmSpaceTrap(this);
            if (armed)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return armed;
        }

        if (string.Equals(itemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
        {
            ShipDamageState damageState = GetComponent<ShipDamageState>();
            if (damageState != null && damageState.IsBoosterDisabled())
                return false;

            photonView.RPC(nameof(ActivateSuperBoosterRpc), photonView.Owner, SuperBoosterDuration);
            photonView.RPC(nameof(PlaySuperBoosterSfxRpc), RpcTarget.All);
            RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);
            return true;
        }

        return false;
    }

    [PunRPC]
    void ActivateGuidanceSystemRpc(float duration)
    {
        GuidanceSystemOverlay.EnsureFor(gameObject)?.ActivateGuidance(duration);
    }

    [PunRPC]
    void ActivateShortScannerRevealRpc(float duration)
    {
        AudioManager.Instance.PlayShortScannerAt(transform.position);
        ShortScannerRevealStatus.EnsureFor(gameObject)?.ActivateReveal(duration);
    }

    [PunRPC]
    void ActivateCloakDeviceRpc(float duration)
    {
        AudioManager.Instance.PlayCloakActivationAt(transform.position);
        HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>() ?? gameObject.AddComponent<HideInNebulaTarget>();
        nebulaTarget.ActivateCloak(duration);
    }

    [PunRPC]
    void CancelCloakDeviceRpc()
    {
        HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>();
        if (nebulaTarget != null)
            nebulaTarget.CancelCloak();
    }

    float GetAdjustedGuidanceSystemDuration()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return GuidanceSystemDuration + (PilotCatalog.IsSelectedPilot(owner, PilotCatalog.VectorId) ? VectorGuidanceSystemDurationBonus : 0f);
    }

    float GetAdjustedShortScannerRevealDuration()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return ShortScannerRevealDuration + (PilotCatalog.IsSelectedPilot(owner, PilotCatalog.VectorId) ? VectorShortScannerRevealDurationBonus : 0f);
    }

    [PunRPC]
    void ActivateSuperBoosterRpc(float duration)
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.ActivateSuperBooster(duration);
    }

    [PunRPC]
    void PlaySuperBoosterSfxRpc()
    {
        AudioManager.Instance.PlaySuperBoosterAt(transform.position);
    }

    [PunRPC]
    void PlaySpaceDrillDeliverySoundRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlaySpaceDrillDeliveryAt(new Vector3(x, y, z));
    }

    [PunRPC]
    void ArmSpaceTrapTargetRpc(int targetViewId, int ownerViewId)
    {
        PhotonView target = PhotonView.Find(targetViewId);
        SpaceTrapTarget trap = target != null ? SpaceTrapTarget.Attach(target.gameObject) : null;
        if (trap != null)
        {
            SpaceTrapLaunchVfx.Spawn(transform.position, target.transform.position);
            trap.Arm(ownerViewId);
        }
    }

    [PunRPC]
    void ClearSpaceTrapTargetRpc(int targetViewId)
    {
        SpaceTrapTarget.ClearLocalMarker(targetViewId);
    }

    bool TryDeployLureBeacon()
    {
        if (!PhotonNetwork.IsMasterClient)
            return false;

        Vector2 deployDirection = -(Vector2)transform.up;
        if (deployDirection.sqrMagnitude < 0.001f)
            deployDirection = Vector2.down;
        else
            deployDirection = deployDirection.normalized;

        Vector3 spawnPosition = ResolveLureBeaconSpawnPosition(deployDirection);
        GameObject beaconObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)),
            0,
            new object[]
            {
                LureBeaconDecoy.InstantiationMarker,
                photonView != null ? photonView.ViewID : 0,
                deployDirection.x,
                deployDirection.y
            });

        if (beaconObject == null)
            return false;

        LureBeaconDecoy.EnsureAttached(beaconObject);
        return true;
    }

    Vector3 ResolveLureBeaconSpawnPosition(Vector2 preferredDirection)
    {
        Vector2 origin = transform.position;
        Vector2 fallbackDirection = preferredDirection.sqrMagnitude > 0.001f ? preferredDirection.normalized : Vector2.down;
        for (int attempt = 0; attempt < 14; attempt++)
        {
            float jitter = attempt == 0 ? 0f : UnityEngine.Random.Range(-26f, 26f);
            Vector2 candidateDirection = Quaternion.Euler(0f, 0f, jitter) * fallbackDirection;
            if (candidateDirection.sqrMagnitude < 0.001f)
                candidateDirection = fallbackDirection;

            Vector2 candidate = origin + candidateDirection.normalized * LureBeaconDeployDistance;
            if (IsLureBeaconSpawnPositionFree(candidate))
                return new Vector3(candidate.x, candidate.y, 0f);
        }

        Vector2 fallback = origin + fallbackDirection * LureBeaconDeployDistance;
        return new Vector3(fallback.x, fallback.y, 0f);
    }

    bool IsLureBeaconSpawnPositionFree(Vector2 candidate)
    {
        int hitCount = Physics2D.OverlapCircle(candidate, LureBeaconSpawnClearanceRadius, CreatePhysicsQueryFilter(), SpawnClearanceHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = SpawnClearanceHits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            if (hit.GetComponentInParent<LureBeaconDecoy>() != null)
                continue;

            return false;
        }

        return true;
    }

    static ContactFilter2D CreatePhysicsQueryFilter()
    {
        return new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };
    }

    bool TryActivateMagneticBeam()
    {
        if (CountMagneticBeamTargets() > 0)
            RoundXpTracker.RecordGadgetSuccess(photonView.Owner, InventoryItemCatalog.MagneticBeamId);

        photonView.RPC(nameof(PlayMagneticBeamEffects), RpcTarget.All);
        StartCoroutine(MagneticBeamPullRoutine());
        return true;
    }

    int CountMagneticBeamTargets()
    {
        int count = 0;
        Vector2 sourcePosition = transform.position;
        MovingSpaceObject[] objects = FindObjectsByType<MovingSpaceObject>(FindObjectsInactive.Exclude);
        for (int i = 0; i < objects.Length; i++)
        {
            MovingSpaceObject movingObject = objects[i];
            if (movingObject == null)
                continue;

            if (Vector2.Distance(sourcePosition, movingObject.transform.position) <= MagneticBeamRadius)
                count++;
        }

        return count;
    }

    IEnumerator MagneticBeamPullRoutine()
    {
        float elapsed = 0f;
        WaitForFixedUpdate wait = new WaitForFixedUpdate();
        while (elapsed < MagneticBeamDuration)
        {
            ApplyMagneticBeamPull(Time.fixedDeltaTime);
            elapsed += Time.fixedDeltaTime;
            yield return wait;
        }
    }

    void ApplyMagneticBeamPull(float deltaTime)
    {
        Vector2 sourcePosition = transform.position;
        MovingSpaceObject[] objects = FindObjectsByType<MovingSpaceObject>(FindObjectsInactive.Exclude);
        for (int i = 0; i < objects.Length; i++)
        {
            MovingSpaceObject movingObject = objects[i];
            if (movingObject == null)
                continue;

            float distance = Vector2.Distance(sourcePosition, movingObject.transform.position);
            if (distance > MagneticBeamRadius)
                continue;

            movingObject.ApplyMagneticPull(sourcePosition, MagneticBeamPullStrength, deltaTime);
        }
    }

    [PunRPC]
    void PlayMagneticBeamEffects()
    {
        AudioManager.Instance.PlayMagneticBeamAt(transform.position);
        MagneticBeamVfx.Spawn(transform);
    }

    PhotonView FindClosestTractorBeamTarget()
    {
        Vector2 sourcePosition = transform.position;
        PhotonView bestView = null;
        float bestDistance = float.MaxValue;
        PhotonView[] views = FindObjectsByType<PhotonView>(FindObjectsInactive.Exclude);
        for (int i = 0; i < views.Length; i++)
        {
            PhotonView candidateView = views[i];
            if (candidateView == null || candidateView == photonView)
                continue;

            if (!IsValidTractorBeamTarget(candidateView))
                continue;

            Collider2D collider = candidateView.GetComponent<Collider2D>();
            Vector2 closest = collider != null ? collider.ClosestPoint(sourcePosition) : (Vector2)candidateView.transform.position;
            float distance = Vector2.Distance(sourcePosition, closest);
            if (distance > TractorBeamRadius || distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestView = candidateView;
        }

        return bestView;
    }

    bool IsValidTractorBeamTarget(PhotonView candidateView)
    {
        if (candidateView == null)
            return false;

        Treasure treasure = candidateView.GetComponent<Treasure>();
        if (treasure != null)
            return !treasure.isBeingCollected;

        ShipWreck wreck = candidateView.GetComponent<ShipWreck>();
        if (wreck != null)
            return wreck.HasLoot && !wreck.isBeingCollected;

        DroppedCargoCrate crate = candidateView.GetComponent<DroppedCargoCrate>();
        if (crate != null)
            return crate.HasLoot && !crate.isBeingCollected;

        return false;
    }

    IEnumerator TractorBeamPullRoutine(int targetViewId)
    {
        float elapsed = 0f;
        WaitForFixedUpdate wait = new WaitForFixedUpdate();
        while (elapsed < TractorBeamMaxDuration)
        {
            PhotonView targetView = PhotonView.Find(targetViewId);
            if (targetView == null || !IsValidTractorBeamTarget(targetView))
                break;

            ApplyTractorBeamPull(targetView, Time.fixedDeltaTime);
            elapsed += Time.fixedDeltaTime;
            yield return wait;
        }

        authoritativeTractorBeamRoutine = null;
        StopAuthoritativeTractorBeam(true);
    }

    void ApplyTractorBeamPull(PhotonView targetView, float deltaTime)
    {
        if (targetView == null || deltaTime <= 0f)
            return;

        Vector2 sourcePosition = transform.position;
        MovingSpaceObject movingObject = targetView.GetComponent<MovingSpaceObject>();
        if (movingObject != null)
        {
            movingObject.ApplyTractorTetherPull(sourcePosition, TractorBeamPullStrength, TractorBeamSlackDistance, deltaTime);
            return;
        }

        Rigidbody2D targetBody = targetView.GetComponent<Rigidbody2D>();
        if (targetBody == null)
            return;

        Vector2 toSource = sourcePosition - targetBody.position;
        float distance = toSource.magnitude;
        if (distance < 0.08f)
        {
            targetBody.linearVelocity *= 0.88f;
            return;
        }

        float stretch = Mathf.Max(0f, distance - TractorBeamSlackDistance);
        float tetherRamp = Mathf.Clamp01(stretch / TractorBeamTetherRampDistance);
        float pullAcceleration = TractorBeamPullStrength * Mathf.Lerp(0.78f, 3.2f, tetherRamp);
        targetBody.linearVelocity += toSource.normalized * pullAcceleration * deltaTime;
        float maxSpeed = Mathf.Lerp(6.4f, 11.5f, tetherRamp);
        if (targetBody.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            targetBody.linearVelocity = targetBody.linearVelocity.normalized * maxSpeed;
    }

    [PunRPC]
    void StartTractorBeamEffects(int sourceViewId, int targetViewId)
    {
        TractorBeamVfx.StartBeam(sourceViewId, targetViewId);
    }

    [PunRPC]
    void StopTractorBeamEffects(int sourceViewId)
    {
        TractorBeamVfx.StopBeam(sourceViewId);
    }
}
