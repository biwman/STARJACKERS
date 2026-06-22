using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemyHunterLanceBehavior : EnemyBotBehaviorBase
{
    enum LanceMode
    {
        Patrol,
        Stalk,
        LockOn,
        Recovery
    }

    const float LockOnDuration = 1.125f;
    const float RecoveryDuration = 0.82f;
    const float BeamRadius = 0.46f;
    const float HpDamageMultiplier = 0.48f;
    const float LockReverseSpeedMultiplier = 0.18f;
    const float AvoidanceScanRadius = 2.8f;
    const float AvoidanceWeight = 0.56f;
    const float MapEdgeMargin = 2.6f;
    const float PatrolTurnIntervalMin = 1.05f;
    const float PatrolTurnIntervalMax = 2.05f;

    readonly HashSet<int> damagedThisShot = new HashSet<int>();

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    Transform currentTarget;
    Vector2 patrolDirection = Vector2.up;
    Vector2 lockedDirection = Vector2.up;
    int lockedTargetViewId;
    float nextTargetRefreshTime;
    float nextRepathTime;
    float nextPatrolTurnTime;
    float nextShotTime;
    float modeStartedAt;
    float orbitDirection = 1f;
    LanceMode mode = LanceMode.Patrol;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.239f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        nextShotTime = Time.time + Random.Range(1.0f, 1.8f);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        switch (mode)
        {
            case LanceMode.LockOn:
                TickLockOn();
                break;
            case LanceMode.Recovery:
                TickRecovery();
                break;
            default:
                RefreshTargetIfNeeded();
                TickPatrolOrStalk();
                break;
        }
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView == null)
            return;

        currentTarget = attackerView.transform;
        nextTargetRefreshTime = Time.time + 0.35f;
        nextShotTime = Mathf.Min(nextShotTime, Time.time + 0.35f);
        if (mode == LanceMode.Patrol)
            mode = LanceMode.Stalk;
    }

    void TickPatrolOrStalk()
    {
        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            float distance = toTarget.magnitude;
            if (distance > 0.001f && distance <= ResolveBeamRange() && Time.time >= nextShotTime)
            {
                BeginLockOn(currentTarget);
                return;
            }

            mode = LanceMode.Stalk;
        }
        else
        {
            mode = LanceMode.Patrol;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + Mathf.Max(0.12f, movement.RepathInterval);
            patrolDirection = currentTarget != null ? ResolveStalkDirection() : ResolvePatrolDirection();
        }

        Vector2 desired = ApplyAvoidance(ApplyMapEdgeSteering(patrolDirection));
        if (desired.sqrMagnitude <= 0.001f)
            desired = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : Vector2.up;

        float speedMultiplier = currentTarget != null ? 1f : 0.58f;
        Vector2 desiredVelocity = desired.normalized * (bot.EffectiveMoveSpeed * speedMultiplier);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, currentTarget != null ? 0.17f : 0.1f);

        Vector2 lookDirection = currentTarget != null
            ? (Vector2)currentTarget.position - rb.position
            : rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : desired;
        RotateNoseToward(lookDirection);
    }

    void BeginLockOn(Transform target)
    {
        PhotonView targetView = target != null ? target.GetComponentInParent<PhotonView>() : null;
        if (targetView == null)
            return;

        lockedTargetViewId = targetView.ViewID;
        lockedDirection = ResolvePredictedAimDirection(target);
        if (lockedDirection.sqrMagnitude <= 0.001f)
            lockedDirection = transform.up;

        mode = LanceMode.LockOn;
        modeStartedAt = Time.time;
        rb.linearVelocity *= 0.24f;
        RotateNoseToward(lockedDirection);

        Vector2 origin = ResolveMuzzlePosition(lockedDirection);
        float range = ResolveBlockedBeamRange(origin, lockedDirection, ResolveBeamRange());
        if (bot.photonView != null)
        {
            bot.photonView.RPC(nameof(EnemyBot.PlayHunterLanceLockRpc), RpcTarget.All, rb.position.x, rb.position.y, transform.position.z);
            bot.photonView.RPC(nameof(EnemyBot.SpawnHunterLanceAimRpc), RpcTarget.All, origin.x, origin.y, lockedDirection.x, lockedDirection.y, range, ResolveLockOnDuration());
        }
    }

    void TickLockOn()
    {
        Transform target = ResolveLockedTargetTransform();
        if (!IsLockedTargetStillValid(ResolveBeamRange() + 2f))
        {
            BeginRecovery(0.45f);
            return;
        }

        RotateNoseToward(lockedDirection);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, -lockedDirection * (bot.EffectiveMoveSpeed * LockReverseSpeedMultiplier), 0.18f);

        float lockOnDuration = ResolveLockOnDuration();
        if (target != null && Time.time - modeStartedAt < lockOnDuration * 0.45f)
            lockedDirection = Vector2.Lerp(lockedDirection, ResolvePredictedAimDirection(target), 0.025f).normalized;

        if (Time.time - modeStartedAt >= lockOnDuration)
            FireLance();
    }

    void FireLance()
    {
        Vector2 direction = lockedDirection.sqrMagnitude > 0.001f ? lockedDirection.normalized : Vector2.up;
        Vector2 origin = ResolveMuzzlePosition(direction);
        float range = ResolveBlockedBeamRange(origin, direction, ResolveBeamRange());

        if (bot.photonView != null)
        {
            bot.photonView.RPC(nameof(EnemyBot.PlayHunterLanceFireRpc), RpcTarget.All, origin.x, origin.y, transform.position.z);
            bot.photonView.RPC(nameof(EnemyBot.SpawnHunterLanceShotRpc), RpcTarget.All, origin.x, origin.y, direction.x, direction.y, range);
        }

        ApplyLanceDamage(origin, direction, range);
        BeginRecovery(RecoveryDuration);
    }

    void BeginRecovery(float duration)
    {
        mode = LanceMode.Recovery;
        modeStartedAt = Time.time;
        nextShotTime = Time.time + ResolveShotCooldown();
        rb.linearVelocity *= 0.55f;
        lockedTargetViewId = 0;
        recoveryDuration = Mathf.Max(0.1f, duration);
    }

    float recoveryDuration = RecoveryDuration;

    void TickRecovery()
    {
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, 0.065f);
        RotateNoseToward(rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : lockedDirection);

        if (Time.time - modeStartedAt >= recoveryDuration)
        {
            RefreshTargetIfNeeded();
            mode = currentTarget != null ? LanceMode.Stalk : LanceMode.Patrol;
        }
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.12f, movement.TargetRefreshInterval);
        currentTarget = ResolveTarget();
    }

    Transform ResolveTarget()
    {
        float allowedRange = currentTarget != null ? movement.DisengageRadius : movement.DetectionRadius;
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, rb.position, allowedRange, true))
            return currentTarget;

        return EnemyTargetingUtility.FindClosestTarget(rb.position, health, movement.DetectionRadius, true);
    }

    Vector2 ResolvePatrolDirection()
    {
        if (Time.time >= nextPatrolTurnTime)
        {
            nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.46f, 0.46f);
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;
    }

    Vector2 ResolveStalkDirection()
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
        float wave = Mathf.Sin(Time.time * 1.05f + (view != null ? view.ViewID : 1) * 0.13f) * 0.12f;

        if (distance > movement.PreferredDistance)
            return (toward * 0.76f + tangent * (0.28f + wave)).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.66f + tangent * 0.58f).normalized;

        return (tangent * 0.86f + toward * 0.08f).normalized;
    }

    Vector2 ResolvePredictedAimDirection(Transform target)
    {
        Vector2 targetPosition = target != null ? (Vector2)target.position : rb.position + patrolDirection;
        Rigidbody2D targetBody = target != null ? target.GetComponent<Rigidbody2D>() : null;
        float distance = Vector2.Distance(rb.position, targetPosition);
        if (targetBody != null)
        {
            float leadTime = Mathf.Clamp(distance / 16f, 0.12f, 0.46f);
            targetPosition += targetBody.linearVelocity * leadTime;
        }

        Vector2 direction = targetPosition - rb.position;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : patrolDirection;
    }

    Transform ResolveLockedTargetTransform()
    {
        PhotonView targetView = lockedTargetViewId > 0 ? PhotonView.Find(lockedTargetViewId) : null;
        return targetView != null ? targetView.transform : null;
    }

    bool IsLockedTargetStillValid(float maxDistance)
    {
        PhotonView targetView = lockedTargetViewId > 0 ? PhotonView.Find(lockedTargetViewId) : null;
        if (targetView == null)
            return false;

        if (Vector2.Distance(rb.position, targetView.transform.position) > maxDistance)
            return false;

        LureBeaconDecoy beacon = targetView.GetComponent<LureBeaconDecoy>();
        if (beacon != null)
            return beacon.CanBeTargeted;

        PlayerDeployableBase deployable = targetView.GetComponent<PlayerDeployableBase>();
        if (deployable != null)
            return deployable.CanBeTargeted;

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        return targetHealth != null && targetHealth != health && !targetHealth.IsWreck && !targetHealth.IsBotControlled && !targetHealth.IsAstronautControlled && !targetHealth.IsEvacuationAnimating;
    }

    Vector2 ResolveMuzzlePosition(Vector2 direction)
    {
        Vector2 forward = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        return rb.position + forward * Mathf.Max(0.35f, bot.VisualTargetSize * 0.43f);
    }

    float ResolveBlockedBeamRange(Vector2 origin, Vector2 direction, float maxRange)
    {
        Vector2 normalized = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, BeamRadius * 0.92f, normalized, maxRange);
        float closestBlockDistance = maxRange;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            Collider2D hitCollider = hit.collider;
            if (hitCollider == null || hitCollider.attachedRigidbody == rb)
                continue;

            float hitDistance = hit.distance;
            if (hitDistance <= 0.04f || hitDistance >= closestBlockDistance)
                continue;

            if (!IsBeamBlockingObject(hitCollider))
                continue;

            closestBlockDistance = Mathf.Max(0.08f, hitDistance - 0.06f);
        }

        return closestBlockDistance;
    }

    void ApplyLanceDamage(Vector2 origin, Vector2 direction, float range)
    {
        damagedThisShot.Clear();
        int attackerViewId = bot.photonView != null ? bot.photonView.ViewID : 0;
        int baseDamage = Mathf.Max(0, RoomSettings.GetEnemyDamage(bot.Kind));
        int shieldDamage = Mathf.Max(1, baseDamage);
        int hpDamage = Mathf.Max(1, Mathf.CeilToInt(baseDamage * HpDamageMultiplier));
        WeaponHitContext hitContext = weapon != null
            ? new WeaponHitContext(weapon.DamageType, weapon.DeliveryMethod, weapon.DeliveryFlags, string.Empty)
            : WeaponHitContext.None;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, BeamRadius, direction.normalized, range);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.attachedRigidbody == rb)
                continue;

            PlayerHealth candidate = hitCollider.GetComponentInParent<PlayerHealth>();
            PhotonView targetView = candidate != null ? candidate.GetComponent<PhotonView>() : null;
            if (candidate == null || targetView == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating)
                continue;

            if (candidate.IsBotControlled || candidate.IsAstronautControlled || candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            if (!damagedThisShot.Add(targetView.ViewID))
                continue;

            Vector2 impact = hits[i].point.sqrMagnitude > 0.001f ? hits[i].point : candidate.transform.position;
            targetView.RPC(
                nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                RpcTarget.MasterClient,
                shieldDamage,
                hpDamage,
                attackerViewId,
                impact.x,
                impact.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted || beacon.photonView == null)
                continue;

            if (!IsPointInsideBeam(beacon.transform.position, origin, direction, range, BeamRadius))
                continue;

            if (!damagedThisShot.Add(beacon.photonView.ViewID))
                continue;

            Vector2 impact = ResolveClosestPointOnBeam(beacon.transform.position, origin, direction, range);
            beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageProfileAt), RpcTarget.MasterClient, shieldDamage, hpDamage, attackerViewId, impact.x, impact.y);
        }

        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (deployable == null || !deployable.CanBeTargeted || deployable.photonView == null)
                continue;

            if (!IsPointInsideBeam(deployable.transform.position, origin, direction, range, BeamRadius))
                continue;

            if (!damagedThisShot.Add(deployable.photonView.ViewID))
                continue;

            Vector2 impact = ResolveClosestPointOnBeam(deployable.transform.position, origin, direction, range);
            deployable.photonView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                shieldDamage,
                hpDamage,
                attackerViewId,
                impact.x,
                impact.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }
    }

    bool IsPointInsideBeam(Vector2 point, Vector2 origin, Vector2 direction, float range, float radius)
    {
        Vector2 closest = ResolveClosestPointOnBeam(point, origin, direction, range);
        return Vector2.Distance(point, closest) <= radius;
    }

    Vector2 ResolveClosestPointOnBeam(Vector2 point, Vector2 origin, Vector2 direction, float range)
    {
        Vector2 normalized = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        float projection = Vector2.Dot(point - origin, normalized);
        projection = Mathf.Clamp(projection, 0f, Mathf.Max(0.01f, range));
        return origin + normalized * projection;
    }

    Vector2 ApplyAvoidance(Vector2 desiredDirection)
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

        return (desired + avoidance.normalized * AvoidanceWeight).normalized;
    }

    bool IsAvoidedObject(Collider2D hit)
    {
        if (EnemyHazardAvoidanceUtility.IsMineThreat(hit, health))
            return true;

        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        return hit.GetComponentInParent<DroppedCargoCrate>() != null;
    }

    bool IsBeamBlockingObject(Collider2D hit)
    {
        return IsAvoidedObject(hit);
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.2f, bot.EffectiveMoveSpeed * 1.85f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + MapEdgeMargin, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - MapEdgeMargin, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + MapEdgeMargin, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - MapEdgeMargin, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desired * 0.34f + inward.normalized * 0.76f).normalized;
    }

    float ResolveBeamRange()
    {
        return weapon != null && weapon.Range > 0f ? weapon.Range : Mathf.Max(10f, movement.ShootDistance);
    }

    float ResolveLockOnDuration()
    {
        return ScaleEnemyAttackWindup(LockOnDuration);
    }

    float ResolveShotCooldown()
    {
        return ScaleEnemyAttackCooldown(weapon != null && weapon.FireRate > 0f ? weapon.FireRate : 4.6f);
    }

    void RotateNoseToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }
}

