using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemyRadarShipBehavior : EnemyBotBehaviorBase
{
    const float StrikeWarningDuration = 2f;
    const float StrikeRadius = 1.35f;
    const float AvoidanceScanRadius = 2.2f;
    const float AvoidanceWeight = 0.34f;
    const float PatrolRefreshInterval = 1.15f;
    const float PatrolArrivalDistance = 1.9f;
    const float PatrolFallbackTurnIntervalMin = 1.4f;
    const float PatrolFallbackTurnIntervalMax = 2.5f;
    const float MapEdgeMargin = 2f;
    const float MapEdgeSteerWeight = 0.78f;

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    Transform currentTarget;
    Transform patrolCollectibleTarget;
    Vector2 fallbackPatrolDirection = Vector2.up;
    float nextTargetRefreshTime;
    float nextPatrolRefreshTime;
    float nextFallbackTurnTime;
    float nextStrikeTime;
    float orbitDirection = 1f;
    bool strikePending;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.171f) % (Mathf.PI * 2f);
        fallbackPatrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        nextStrikeTime = Time.time + Random.Range(0.5f, 1.2f);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        RefreshTargetIfNeeded();
        RefreshPatrolTargetIfNeeded();

        Vector2 desiredDirection = currentTarget != null
            ? ResolveCombatDirection()
            : ResolvePatrolDirection();

        desiredDirection = ApplyCollectibleAvoidance(desiredDirection);
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        if (desiredDirection.sqrMagnitude <= 0.001f)
            desiredDirection = fallbackPatrolDirection.sqrMagnitude > 0.001f ? fallbackPatrolDirection : Vector2.up;

        float speedMultiplier = currentTarget != null ? 1f : 0.82f;
        Vector2 desiredVelocity = desiredDirection.normalized * (bot.EffectiveMoveSpeed * speedMultiplier);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, currentTarget != null ? 0.16f : 0.11f);

        Vector2 aimDirection = currentTarget != null
            ? (Vector2)currentTarget.position - rb.position
            : rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : desiredDirection;
        RotateHullToward(aimDirection);

        if (currentTarget != null)
            TryCallRadarStrike();
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView != null)
        {
            currentTarget = attackerView.transform;
            nextTargetRefreshTime = Time.time + 0.4f;
            nextStrikeTime = Mathf.Min(nextStrikeTime, Time.time + 0.25f);
        }
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.18f, movement.TargetRefreshInterval);
        currentTarget = ResolvePlayerTarget();
    }

    void RefreshPatrolTargetIfNeeded()
    {
        if (currentTarget != null)
            return;

        if (patrolCollectibleTarget != null && patrolCollectibleTarget.gameObject.activeInHierarchy)
            return;

        if (Time.time < nextPatrolRefreshTime)
            return;

        nextPatrolRefreshTime = Time.time + PatrolRefreshInterval;
        patrolCollectibleTarget = ResolveBestCollectibleTarget();
    }

    Transform ResolvePlayerTarget()
    {
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, rb.position, movement.DisengageRadius, false))
            return currentTarget;

        return EnemyTargetingUtility.FindClosestTarget(rb.position, health, movement.DetectionRadius, false);
    }

    bool IsValidPlayerTarget(PlayerHealth candidate, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(candidate != null ? candidate.transform : null, health, rb.position, maxDistance, false);
    }

    Transform ResolveBestCollectibleTarget()
    {
        Transform bestTarget = null;
        float bestScore = float.MinValue;

        Treasure[] treasures = RuntimeSceneQueryCache.GetTreasures();
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || !treasure.gameObject.activeInHierarchy || treasure.isBeingCollected)
                continue;

            string itemId = InventoryItemCatalog.ResolveAlienSecretItemId(treasure.itemId, treasure.visualVariantIndex);
            float score = ScoreCollectible(treasure.transform.position, itemId, 1f);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = treasure.transform;
            }
        }

        ShipWreck[] wrecks = RuntimeSceneQueryCache.GetShipWrecks();
        for (int i = 0; i < wrecks.Length; i++)
        {
            ShipWreck wreck = wrecks[i];
            if (wreck == null || !wreck.gameObject.activeInHierarchy || !wreck.HasLoot || wreck.isBeingCollected)
                continue;

            string itemId = wreck.GetLootItemAt(wreck.GetFirstLootIndex());
            float score = ScoreCollectible(wreck.transform.position, itemId, 1.18f);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = wreck.transform;
            }
        }

        DroppedCargoCrate[] crates = RuntimeSceneQueryCache.GetDroppedCargoCrates();
        for (int i = 0; i < crates.Length; i++)
        {
            DroppedCargoCrate crate = crates[i];
            if (crate == null || !crate.gameObject.activeInHierarchy || !crate.HasLoot || crate.isBeingCollected)
                continue;

            float score = ScoreCollectible(crate.transform.position, crate.StoredItemId, 1.08f);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = crate.transform;
            }
        }

        return bestTarget;
    }

    float ScoreCollectible(Vector2 position, string itemId, float categoryMultiplier)
    {
        int sellValue = Mathf.Max(1, InventoryItemCatalog.GetSellValueAstrons(itemId));
        float distance = Vector2.Distance(rb.position, position);
        float rarityWeight = (int)InventoryItemCatalog.GetRarity(itemId) * 110f;
        return sellValue * categoryMultiplier + rarityWeight - distance * 38f;
    }

    Vector2 ResolvePatrolDirection()
    {
        if (patrolCollectibleTarget != null && patrolCollectibleTarget.gameObject.activeInHierarchy)
        {
            Vector2 toTarget = (Vector2)patrolCollectibleTarget.position - rb.position;
            if (toTarget.magnitude <= PatrolArrivalDistance)
            {
                patrolCollectibleTarget = null;
                nextPatrolRefreshTime = 0f;
            }
            else
            {
                return toTarget.normalized;
            }
        }

        if (Time.time >= nextFallbackTurnTime)
        {
            nextFallbackTurnTime = Time.time + Random.Range(PatrolFallbackTurnIntervalMin, PatrolFallbackTurnIntervalMax);
            float angle = Mathf.Atan2(fallbackPatrolDirection.y, fallbackPatrolDirection.x) + Random.Range(-0.52f, 0.52f);
            fallbackPatrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        return fallbackPatrolDirection;
    }

    Vector2 ResolveCombatDirection()
    {
        if (currentTarget == null)
            return ResolvePatrolDirection();

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return ResolvePatrolDirection();

        Vector2 toward = toTarget / distance;
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-toward.y, toward.x)
            : new Vector2(toward.y, -toward.x);

        float wobble = Mathf.Sin(Time.time * 1.35f + (view != null ? view.ViewID : 1) * 0.13f) * 0.14f;
        if (distance > movement.PreferredDistance + 0.5f)
            return (toward * 0.78f + tangent * (0.32f + wobble)).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.62f + tangent * 0.78f).normalized;

        return (tangent * 0.92f + toward * 0.12f).normalized;
    }

    Vector2 ApplyCollectibleAvoidance(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(rb.position, AvoidanceScanRadius, out Collider2D[] hits);
        Vector2 avoidance = Vector2.zero;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            if (!IsAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(rb.position);
            Vector2 away = rb.position - closest;
            if (away.sqrMagnitude <= 0.0001f)
                away = rb.position - (Vector2)hit.transform.position;

            float distance = Mathf.Max(0.12f, away.magnitude);
            if (distance > AvoidanceScanRadius)
                continue;

            avoidance += away.normalized * Mathf.Clamp01((AvoidanceScanRadius - distance) / AvoidanceScanRadius);
        }

        if (avoidance.sqrMagnitude <= 0.001f)
            return desiredDirection;

        Vector2 result = (desired + avoidance.normalized * AvoidanceWeight).normalized;
        return Vector2.Dot(result, desired) < 0.1f
            ? (desired * 0.78f + avoidance.normalized * 0.22f).normalized
            : result;
    }

    bool IsAvoidedObject(Collider2D hit)
    {
        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        return hit.GetComponentInParent<DroppedCargoCrate>() != null;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.4f, bot.EffectiveMoveSpeed * 1.8f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + 1.4f, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - 1.4f, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + 1.4f, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - 1.4f, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desired * (1f - MapEdgeSteerWeight) + inward.normalized * MapEdgeSteerWeight).normalized;
    }

    void RotateHullToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    void TryCallRadarStrike()
    {
        if (weapon == null || currentTarget == null || strikePending || Time.time < nextStrikeTime)
            return;

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        if (toTarget.magnitude > weapon.Range)
            return;

        Vector2 strikePoint = currentTarget.position;
        strikePending = true;
        nextStrikeTime = Time.time + ScaleEnemyAttackCooldown(Mathf.Max(0.2f, weapon.FireRate));
        float warningDuration = ResolveStrikeWarningDuration();

        if (bot.photonView != null)
        {
            bot.photonView.RPC(nameof(EnemyBot.PlayRadarShipIncomingRpc), RpcTarget.All, strikePoint.x, strikePoint.y, 0f);
            bot.photonView.RPC(nameof(EnemyBot.SpawnRadarStrikeMarkerRpc), RpcTarget.All, strikePoint.x, strikePoint.y, warningDuration, StrikeRadius);
        }

        StartCoroutine(ExecuteStrikeAfterDelay(strikePoint, warningDuration));
    }

    IEnumerator ExecuteStrikeAfterDelay(Vector2 strikePoint, float warningDuration)
    {
        yield return new WaitForSeconds(warningDuration);

        if (bot == null || bot.photonView == null)
        {
            strikePending = false;
            yield break;
        }

        bot.photonView.RPC(nameof(EnemyBot.PlayRadarShipShootRpc), RpcTarget.All, strikePoint.x, strikePoint.y, 0f);
        ApplyStrikeDamage(strikePoint);
        bot.photonView.RPC(nameof(EnemyBot.SpawnRadarStrikeImpactRpc), RpcTarget.All, strikePoint.x, strikePoint.y, StrikeRadius);
        strikePending = false;
    }

    float ResolveStrikeWarningDuration()
    {
        return ScaleEnemyAttackWindup(StrikeWarningDuration);
    }

    void ApplyStrikeDamage(Vector2 strikePoint)
    {
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(strikePoint, StrikeRadius, out Collider2D[] hits);
        HashSet<int> processedViewIds = new HashSet<int>();
        int attackerViewId = bot.photonView != null ? bot.photonView.ViewID : 0;
        int baseDamage = RoomSettings.GetEnemyDamage(bot.Kind);
        WeaponHitContext hitContext = weapon != null
            ? new WeaponHitContext(weapon.DamageType, weapon.DeliveryMethod, weapon.DeliveryFlags, string.Empty)
            : WeaponHitContext.None;

        for (int i = 0; i < hitCount; i++)
        {
            PlayerHealth candidate = hits[i] != null ? hits[i].GetComponentInParent<PlayerHealth>() : null;
            PhotonView targetView = candidate != null ? candidate.GetComponent<PhotonView>() : null;
            if (candidate == null || targetView == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating)
                continue;

            if (candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            if (!processedViewIds.Add(targetView.ViewID))
                continue;

            float distance = Vector2.Distance(strikePoint, candidate.transform.position);
            float falloff = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(distance / StrikeRadius));
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * falloff));
            targetView.RPC(
                nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                RpcTarget.MasterClient,
                damage,
                damage,
                attackerViewId,
                strikePoint.x,
                strikePoint.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted || beacon.photonView == null)
                continue;

            if (!processedViewIds.Add(beacon.photonView.ViewID))
                continue;

            float distance = Vector2.Distance(strikePoint, beacon.transform.position);
            if (distance > StrikeRadius)
                continue;

            float falloff = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(distance / StrikeRadius));
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * falloff));
            beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageProfileAt), RpcTarget.MasterClient, damage, damage, attackerViewId, strikePoint.x, strikePoint.y);
        }

        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (deployable == null || !deployable.CanBeTargeted || deployable.photonView == null)
                continue;

            if (!processedViewIds.Add(deployable.photonView.ViewID))
                continue;

            float distance = Vector2.Distance(strikePoint, deployable.transform.position);
            if (distance > StrikeRadius)
                continue;

            float falloff = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(distance / StrikeRadius));
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * falloff));
            deployable.photonView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                damage,
                damage,
                attackerViewId,
                strikePoint.x,
                strikePoint.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }
    }
}
