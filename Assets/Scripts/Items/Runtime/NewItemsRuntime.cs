using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public static class NewItemsRuntime
{
    public const float SpaceTrapArmRange = 3.1f;
    static readonly Collider2D[] SpawnPositionHits = new Collider2D[48];

    public static bool TryDeployAutoTurret(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        Vector3 spawnPosition = ResolveRearSpawnPosition(owner.transform, 0.95f, 0.42f);
        int ownerActorNumber = ResolveOwnerActorNumber(owner.photonView);
        GameObject turretObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            owner.transform.rotation,
            0,
            new object[] { PlayerDeployableRuntime.AutoTurretMarker, owner.photonView.ViewID, ownerActorNumber });

        if (turretObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(turretObject);
        return true;
    }

    public static bool TryDeployRocketAutoTurret(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        Vector3 spawnPosition = ResolveRearSpawnPosition(owner.transform, 1.05f, 0.46f);
        int ownerActorNumber = ResolveOwnerActorNumber(owner.photonView);
        GameObject turretObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            owner.transform.rotation,
            0,
            new object[] { PlayerDeployableRuntime.RocketAutoTurretMarker, owner.photonView.ViewID, ownerActorNumber });

        if (turretObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(turretObject);
        return true;
    }

    static int ResolveOwnerActorNumber(PhotonView view)
    {
        if (view == null)
            return 0;

        if (view.Owner != null && view.Owner.ActorNumber > 0)
            return view.Owner.ActorNumber;

        return Mathf.Max(0, view.OwnerActorNr);
    }

    public static bool TryDeploySpaceBomb(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        Vector3 spawnPosition = ResolveRearSpawnPosition(owner.transform, 0.95f, 1.1f);
        Vector2 forward = owner.transform.up.sqrMagnitude > 0.001f ? (Vector2)owner.transform.up.normalized : Vector2.up;
        GameObject bombObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            Quaternion.LookRotation(Vector3.forward, forward),
            0,
            new object[] { PlayerDeployableRuntime.SpaceBombMarker, owner.photonView.ViewID, forward.x, forward.y });

        if (bombObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(bombObject);
        return true;
    }

    public static bool TryDeployStasisBuoy(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        Vector3 spawnPosition = ResolveRearSpawnPosition(owner.transform, 1.05f, 0.42f);
        GameObject buoyObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            Quaternion.identity,
            0,
            new object[] { PlayerDeployableRuntime.StasisBuoyMarker, owner.photonView.ViewID });

        if (buoyObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(buoyObject);
        return true;
    }

    public static bool TryDeployMetalDriftWall(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        Vector2 forward = owner.transform.up.sqrMagnitude > 0.001f ? (Vector2)owner.transform.up.normalized : Vector2.up;
        Vector3 spawnPosition = ResolveForwardSpawnPosition(owner.transform, 1.35f, 0.72f);
        Rigidbody2D ownerBody = owner.GetComponent<Rigidbody2D>();
        Vector2 inheritedVelocity = ownerBody != null ? ownerBody.linearVelocity * 0.28f : Vector2.zero;
        Vector2 initialVelocity = Vector2.ClampMagnitude(inheritedVelocity + forward * 0.55f, 2.15f);
        GameObject wallObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            Quaternion.LookRotation(Vector3.forward, forward),
            0,
            new object[] { PlayerDeployableRuntime.MetalDriftWallMarker, owner.photonView.ViewID, forward.x, forward.y, initialVelocity.x, initialVelocity.y });

        if (wallObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(wallObject);
        return true;
    }

    public static bool TryLaunchSpaceTorpedo(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        Vector2 direction = owner.transform.up.sqrMagnitude > 0.001f ? (Vector2)owner.transform.up.normalized : Vector2.up;
        Vector3 spawnPosition = owner.transform.position + (Vector3)(direction * 0.82f);
        GameObject torpedoObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            Quaternion.LookRotation(Vector3.forward, direction),
            0,
            new object[] { PlayerDeployableRuntime.SpaceTorpedoMarker, owner.photonView.ViewID, direction.x, direction.y });

        if (torpedoObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(torpedoObject);
        return true;
    }

    public static bool TryStartTetherHarpoon(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        PhotonView target = FindNearestEnemyShipTarget(owner, 7.2f);
        if (target == null)
            return false;

        owner.photonView.RPC("PlayTetherHarpoonFxRpc", RpcTarget.All, target.ViewID);
        owner.StartCoroutine(TetherHarpoonRoutine(owner, target.ViewID));
        return true;
    }

    public static bool TryFireBioTrap(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        PhotonView target = FindNearestAstronautTarget(owner, 6f);
        if (target == null)
            return false;

        PlayerHealth targetHealth = target.GetComponent<PlayerHealth>();
        if (IsBioTrapProtectedEscapePod(targetHealth))
            return false;

        Vector2 origin = owner.transform.position;
        Vector2 targetPosition = target.transform.position;
        Vector2 drift = targetPosition - origin;
        if (drift.sqrMagnitude <= 0.001f)
            drift = UnityEngine.Random.insideUnitCircle;
        if (drift.sqrMagnitude <= 0.001f)
            drift = Vector2.up;

        owner.photonView.RPC("PlayBioTrapFxRpc", RpcTarget.All, target.ViewID);
        DroppedCargoManager.DropItemAtPosition(
            InventoryItemCatalog.CaptiveAstronautPodId,
            new Vector3(targetPosition.x, targetPosition.y, 0f),
            drift.normalized * 0.65f);
        if (targetHealth != null && targetHealth.photonView != null)
        {
            targetHealth.photonView.RPC(
                nameof(PlayerHealth.ForceBioTrapAstronautDeathRpc),
                RpcTarget.MasterClient,
                owner.photonView.ViewID,
                targetPosition.x,
                targetPosition.y);
        }
        else
        {
            PhotonNetwork.Destroy(target.gameObject);
        }
        return true;
    }

    public static bool TryDetonateAsteroidBreacherBomb(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        ObstacleChunk target = FindNearestObstacle(owner.transform.position, 7.2f);
        if (target == null)
            return false;

        Vector2 targetPosition = target.transform.position;
        Vector2 direction = targetPosition - (Vector2)owner.transform.position;
        if (direction.sqrMagnitude <= 0.001f)
            direction = owner.transform.up.sqrMagnitude > 0.001f ? (Vector2)owner.transform.up : Vector2.up;
        direction.Normalize();

        Vector3 spawnPosition = owner.transform.position + (Vector3)(direction * 0.72f);
        GameObject bombObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            Quaternion.LookRotation(Vector3.forward, direction),
            0,
            new object[] { PlayerDeployableRuntime.AsteroidBreacherBombMarker, owner.photonView.ViewID, target.StableId ?? string.Empty, targetPosition.x, targetPosition.y });

        if (bombObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(bombObject);
        return true;
    }

    static PhotonView FindNearestEnemyShipTarget(PlayerShooting owner, float maxRange)
    {
        if (owner == null || owner.photonView == null)
            return null;

        int ownerActorNumber = owner.photonView.OwnerActorNr;
        Vector2 origin = owner.transform.position;
        PlayerHealth[] healths = UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        PhotonView best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (candidate == null ||
                candidate.IsWreck ||
                candidate.IsAstronautControlled ||
                candidate.IsEvacuationAnimating ||
                candidate.photonView == null ||
                candidate.photonView.ViewID == owner.photonView.ViewID ||
                candidate.GetComponent<LureBeaconDecoy>() != null ||
                candidate.GetComponent<PlayerDeployableBase>() != null)
            {
                continue;
            }

            EnemyBot bot = candidate.GetComponent<EnemyBot>();
            if (bot != null && !bot.CanReceivePilotHostileEffect())
                continue;

            bool computerTarget = candidate.IsBotControlled || candidate.IsNeutralRiderControlled;
            if (ownerActorNumber > 0 && candidate.photonView.OwnerActorNr == ownerActorNumber && !computerTarget)
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance > maxRange || distance >= bestDistance)
                continue;

            best = candidate.photonView;
            bestDistance = distance;
        }

        return best;
    }

    static PhotonView FindNearestAstronautTarget(PlayerShooting owner, float maxRange)
    {
        int ownerActorNumber = owner != null && owner.photonView != null ? owner.photonView.OwnerActorNr : -1;
        Vector2 origin = owner != null ? (Vector2)owner.transform.position : Vector2.zero;
        PlayerHealth[] healths = UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        PhotonView best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (candidate == null ||
                candidate.IsWreck ||
                candidate.photonView == null)
            {
                continue;
            }

            AstronautSurvivor survivor = candidate.GetComponent<AstronautSurvivor>();
            bool isAstronautTarget = candidate.IsAstronautControlled || candidate.IsEnemyAstronautControlled || survivor != null;
            if (!isAstronautTarget)
                continue;

            if (IsBioTrapProtectedEscapePod(candidate))
                continue;

            if (owner != null && owner.photonView != null && candidate.photonView.ViewID == owner.photonView.ViewID)
                continue;

            bool computerAstronaut = candidate.IsEnemyAstronautControlled || (survivor != null && survivor.IsEnemySurvivor);
            if (!computerAstronaut && ownerActorNumber > 0 && candidate.photonView.OwnerActorNr == ownerActorNumber)
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance > maxRange || distance >= bestDistance)
                continue;

            best = candidate.photonView;
            bestDistance = distance;
        }

        return best;
    }

    static bool IsBioTrapProtectedEscapePod(PlayerHealth candidate)
    {
        if (candidate == null)
            return false;

        AstronautSurvivor survivor = candidate.GetComponent<AstronautSurvivor>();
        if (survivor != null && survivor.IsEscapePodMode)
            return true;

        PhotonView view = candidate.photonView != null ? candidate.photonView : candidate.GetComponent<PhotonView>();
        return AstronautSurvivor.IsEscapePodInstantiationData(view != null ? view.InstantiationData : null);
    }

    static ObstacleChunk FindNearestObstacle(Vector2 origin, float maxRange)
    {
        ObstacleChunk[] obstacles = UnityEngine.Object.FindObjectsByType<ObstacleChunk>(FindObjectsInactive.Exclude);
        ObstacleChunk best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < obstacles.Length; i++)
        {
            ObstacleChunk candidate = obstacles[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.StableId))
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance > maxRange || distance >= bestDistance)
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    static IEnumerator TetherHarpoonRoutine(PlayerShooting owner, int targetViewId)
    {
        const float duration = 4.5f;
        const float pullStrength = 42f;
        const float selfPullStrength = 10f;
        const float slackDistance = 2.35f;
        const float shockTickInterval = 0.35f;

        if (owner == null || owner.photonView == null)
            yield break;

        int sourceViewId = owner.photonView.ViewID;
        owner.photonView.RPC("StartTractorBeamEffects", RpcTarget.All, sourceViewId, targetViewId);
        float elapsed = 0f;
        float nextShockTime = 0f;
        WaitForFixedUpdate wait = new WaitForFixedUpdate();
        while (elapsed < duration)
        {
            PhotonView target = PhotonView.Find(targetViewId);
            if (target == null || !IsValidTetherHarpoonTarget(owner, target))
                break;

            Vector2 sourcePosition = owner.transform.position;
            Rigidbody2D targetBody = target.GetComponent<Rigidbody2D>();
            if (targetBody != null)
            {
                Vector2 toSource = sourcePosition - targetBody.position;
                float distance = toSource.magnitude;
                if (distance > 0.08f)
                {
                    float stretch = Mathf.Max(0f, distance - slackDistance);
                    float ramp = Mathf.Clamp01(stretch / 3.5f);
                    targetBody.linearVelocity += toSource.normalized * pullStrength * Mathf.Lerp(0.45f, 1.35f, ramp) * Time.fixedDeltaTime;
                    targetBody.linearVelocity = Vector2.ClampMagnitude(targetBody.linearVelocity, Mathf.Lerp(5.5f, 10.5f, ramp));
                }
            }

            Rigidbody2D ownerBody = owner.GetComponent<Rigidbody2D>();
            if (ownerBody != null && targetBody != null)
            {
                Vector2 toTarget = targetBody.position - ownerBody.position;
                if (toTarget.sqrMagnitude > slackDistance * slackDistance)
                    ownerBody.linearVelocity += toTarget.normalized * selfPullStrength * Time.fixedDeltaTime;
            }

            if (Time.time >= nextShockTime)
            {
                nextShockTime = Time.time + shockTickInterval;
                PlayerHealth targetHealth = target.GetComponent<PlayerHealth>();
                if (targetHealth != null && targetHealth.photonView != null)
                    targetHealth.photonView.RPC(nameof(PlayerHealth.ApplyElectromagneticShockRpc), RpcTarget.All, 0.7f, 0.45f, 1.55f);
            }

            elapsed += Time.fixedDeltaTime;
            yield return wait;
        }

        if (owner != null && owner.photonView != null)
            owner.photonView.RPC("StopTractorBeamEffects", RpcTarget.All, sourceViewId);
    }

    static bool IsValidTetherHarpoonTarget(PlayerShooting owner, PhotonView target)
    {
        if (owner == null || owner.photonView == null || target == null)
            return false;

        PlayerHealth health = target.GetComponent<PlayerHealth>();
        return health != null &&
               !health.IsWreck &&
               !health.IsAstronautControlled &&
               !health.IsEvacuationAnimating &&
               target.GetComponent<LureBeaconDecoy>() == null &&
               target.GetComponent<PlayerDeployableBase>() == null &&
               target.ViewID != owner.photonView.ViewID &&
               CanReceiveHostileGadgetEffect(health);
    }

    static bool CanReceiveHostileGadgetEffect(PlayerHealth health)
    {
        if (health == null)
            return false;

        EnemyBot bot = health.GetComponent<EnemyBot>();
        return bot == null || bot.CanReceivePilotHostileEffect();
    }

    public static void ApplyBreacherExplosionDamage(Vector2 center, int ownerViewId)
    {
        const float radius = 1.65f;
        const int damage = 38;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        HashSet<int> damagedViews = new HashSet<int>();
        WeaponHitContext hitContext = new WeaponHitContext(
            WeaponDamageType.Explosive,
            WeaponDeliveryMethod.RemoteStrike,
            WeaponDeliveryFlags.AreaDamage,
            PlayerHealth.DamageSourceExplosive);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable.photonView != null && deployable.CanBeTargeted && damagedViews.Add(deployable.photonView.ViewID))
            {
                deployable.photonView.RPC(
                    nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                    RpcTarget.MasterClient,
                    damage,
                    damage,
                    ownerViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
                continue;
            }

            PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
            if (health != null && health.photonView != null && !health.IsWreck && health.photonView.ViewID != ownerViewId && damagedViews.Add(health.photonView.ViewID))
            {
                health.photonView.RPC(
                    nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                    RpcTarget.MasterClient,
                    damage,
                    damage,
                    ownerViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
            }
        }
    }

    public static bool TryLaunchSpaceDrill(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null || owner.photonView.Owner == null)
            return false;

        PhotonView target = SpaceDrillDeployable.FindNearestLootableAsteroid(owner.transform.position);
        if (target == null)
            return false;

        Treasure treasure = target.GetComponent<Treasure>();
        if (treasure == null || !PlayerProfileService.PlayerHasFreeShipInventorySlot(owner.photonView.Owner, treasure.itemId))
            return false;

        Vector3 spawnPosition = owner.transform.position + owner.transform.right * 0.75f - owner.transform.up * 0.15f;
        GameObject drillObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            owner.transform.rotation,
            0,
            new object[] { PlayerDeployableRuntime.SpaceDrillMarker, owner.photonView.ViewID, target.ViewID });

        if (drillObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(drillObject);
        return true;
    }

    public static bool TryLaunchDropbot(PlayerShooting owner, string itemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            owner == null ||
            owner.photonView == null ||
            owner.photonView.Owner == null ||
            string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (!DropbotDeployable.TryFindNearestExtraction(owner.transform.position, out Vector2 extractionPosition))
            return false;

        Vector3 spawnPosition = owner.transform.position + owner.transform.right * 0.68f - owner.transform.up * 0.12f;
        Vector2 direction = extractionPosition - (Vector2)spawnPosition;
        Quaternion rotation = direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(Vector3.forward, direction.normalized)
            : owner.transform.rotation;

        GameObject dropbotObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            rotation,
            0,
            new object[] { PlayerDeployableRuntime.DropbotMarker, owner.photonView.ViewID, itemId, extractionPosition.x, extractionPosition.y });

        if (dropbotObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(dropbotObject);
        return true;
    }

    public static bool TryArmSpaceTrap(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        PhotonView target = SpaceTrapTarget.FindClosestTrapTarget(owner.transform.position, SpaceTrapArmRange);
        if (target == null)
            return false;

        if (!SpaceTrapTarget.TryArmTarget(target, owner.photonView.ViewID))
            return false;

        owner.photonView.RPC("ArmSpaceTrapTargetRpc", RpcTarget.All, target.ViewID, owner.photonView.ViewID);
        return true;
    }

    static Vector3 ResolveRearSpawnPosition(Transform source, float distance, float clearanceRadius)
    {
        Vector2 origin = source != null ? (Vector2)source.position : Vector2.zero;
        Vector2 forward = source != null && source.up.sqrMagnitude > 0.001f ? (Vector2)source.up.normalized : Vector2.up;
        Vector2 right = source != null && source.right.sqrMagnitude > 0.001f ? (Vector2)source.right.normalized : Vector2.right;
        Vector2[] directions =
        {
            -forward,
            (-forward + right * 0.55f).normalized,
            (-forward - right * 0.55f).normalized,
            right,
            -right
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2 candidate = origin + directions[i] * distance;
            if (IsSpawnPositionFree(candidate, clearanceRadius, source))
                return new Vector3(candidate.x, candidate.y, 0f);
        }

        Vector2 fallback = origin - forward * distance;
        return new Vector3(fallback.x, fallback.y, 0f);
    }

    static Vector3 ResolveForwardSpawnPosition(Transform source, float distance, float clearanceRadius)
    {
        Vector2 origin = source != null ? (Vector2)source.position : Vector2.zero;
        Vector2 forward = source != null && source.up.sqrMagnitude > 0.001f ? (Vector2)source.up.normalized : Vector2.up;
        Vector2 right = source != null && source.right.sqrMagnitude > 0.001f ? (Vector2)source.right.normalized : Vector2.right;
        Vector2[] directions =
        {
            forward,
            (forward + right * 0.42f).normalized,
            (forward - right * 0.42f).normalized,
            right,
            -right
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2 candidate = origin + directions[i] * distance;
            if (IsSpawnPositionFree(candidate, clearanceRadius, source))
                return new Vector3(candidate.x, candidate.y, 0f);
        }

        Vector2 fallback = origin + forward * distance;
        return new Vector3(fallback.x, fallback.y, 0f);
    }

    static bool IsSpawnPositionFree(Vector2 position, float radius, Transform source)
    {
        int hitCount = Physics2D.OverlapCircle(position, radius, CreatePhysicsQueryFilter(), SpawnPositionHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = SpawnPositionHits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (source != null && (hit.transform == source || hit.transform.IsChildOf(source)))
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
}
