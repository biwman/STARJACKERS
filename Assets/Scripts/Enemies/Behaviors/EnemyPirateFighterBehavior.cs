using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemyPirateFighterBehavior : EnemyBotBehaviorBase
{
    enum PirateMode
    {
        Patrol,
        AttackRun,
        BreakAway,
        CriticalFlee
    }

    const float CriticalHpFraction = 0.3f;
    const float AvoidanceScanRadius = 2.85f;
    const float AvoidanceWeight = 0.64f;
    const float MapEdgeMargin = 2.4f;
    const float MapEdgeSteerWeight = 0.82f;
    const float PatrolTurnIntervalMin = 0.95f;
    const float PatrolTurnIntervalMax = 1.85f;
    const float SquadronRefreshInterval = 0.34f;
    const float SquadronJoinDistance = 4.2f;
    const float SquadronComfortDistance = 2.35f;
    const float SquadronSeparationDistance = 1.18f;
    const float AttackRearOffset = 3.2f;
    const float AttackPointTolerance = 2.35f;
    const float RearArcDot = 0.25f;
    const float AttackBurstWindow = 0.88f;
    const float BreakAwayDuration = 1.35f;
    const float ForcedCombatDuration = 8f;
    const float FireIntervalJitter = 0.035f;
    const float BeaconPriorityRangeMultiplier = 1.9f;
    const float StuckVelocityThreshold = 0.16f;
    const float StuckDuration = 0.46f;
    const float AvoidanceSuppressionDuration = 0.85f;
    const float StuckEscapeDuration = 0.72f;
    const float MinimumMoveSpeedFraction = 0.3f;

    readonly Transform[] squadmates = new Transform[2];

    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    PirateMode mode = PirateMode.Patrol;
    Transform currentTarget;
    Vector2 patrolDirection = Vector2.up;
    Vector2 fallbackFleeDirection = Vector2.right;
    float nextTargetRefreshTime;
    float nextPatrolTurnTime;
    float nextSquadronRefreshTime;
    float breakAwayUntil;
    float forcedCombatUntil;
    int forcedCombatTargetViewId;
    float lowSpeedSince;
    float avoidanceSuppressedUntil;
    float stuckEscapeUntil;
    Vector2 stuckEscapeDirection = Vector2.up;
    float attackRunStartedAt;
    float lastSuccessfulShotTime = -10f;
    float orbitDirection = 1f;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.227f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        fallbackFleeDirection = patrolDirection;
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
        attackRunStartedAt = Time.time;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                weapon.AmmoCount,
                weapon.ReloadDuration,
                RoomSettings.GetEnemyDamage(owner.Kind),
                weapon.BulletScaleMultiplier,
                weapon.BulletColor,
                weapon.MuzzleOffsetDistance,
                weapon.InfiniteAmmo,
                weapon.BulletSpeed,
                weapon.ShotSoundId,
                weapon.Range,
                ResolveProjectileEffectId(owner.Kind),
                2.4f,
                weapon.DamageType,
                weapon.DeliveryMethod,
                weapon.DeliveryFlags);
        }

        if (owner.PirateBaseLaunchTargetViewId > 0)
            NotifyForcedTarget(owner.PirateBaseLaunchTargetViewId);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        RefreshTargetIfNeeded();
        RefreshSquadronIfNeeded();
        UpdateMode();

        Vector2 desiredDirection = ResolveDesiredDirection();
        desiredDirection = ApplySquadronSteering(desiredDirection);
        desiredDirection = ApplyAvoidance(desiredDirection);
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        desiredDirection = ResolveStuckEscapeDirection(desiredDirection);
        if (desiredDirection.sqrMagnitude <= 0.001f)
            desiredDirection = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection;

        float speed = ResolveCurrentSpeed();
        float velocityBlend = mode == PirateMode.AttackRun ? 0.22f : mode == PirateMode.CriticalFlee ? 0.28f : 0.16f;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredDirection.normalized * speed, velocityBlend);
        ApplyMinimumMoveSpeed(desiredDirection, speed);

        Vector2 aimDirection = ResolveAimDirection(desiredDirection);
        RotateNoseToward(aimDirection);

        if (mode == PirateMode.AttackRun)
            TryShootAtTarget();
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView != null)
            currentTarget = attackerView.transform;

        Vector2 threatPosition = attackerView != null ? (Vector2)attackerView.transform.position : rb != null ? rb.position - Vector2.right : (Vector2)transform.position - Vector2.right;
        Vector2 away = rb != null ? rb.position - threatPosition : (Vector2)transform.position - threatPosition;
        if (away.sqrMagnitude < 0.001f)
            away = rb != null && rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection;

        fallbackFleeDirection = away.normalized;
        forcedCombatUntil = Time.time + ForcedCombatDuration;
        forcedCombatTargetViewId = attackerViewID;
        if (!IsCriticalHealth())
            EnterAttackRun();
    }

    public void NotifyForcedTarget(int targetViewID)
    {
        PhotonView targetView = targetViewID > 0 ? PhotonView.Find(targetViewID) : null;
        if (targetView == null)
            return;

        Transform forcedTarget = targetView.transform;
        if (IsProtectedPirateSymbolTarget(forcedTarget))
        {
            if (currentTarget == forcedTarget)
                currentTarget = null;

            return;
        }

        NotifyDamageSource(targetViewID);
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
        float validRange = mode == PirateMode.Patrol && Time.time >= forcedCombatUntil
            ? movement.DetectionRadius
            : movement.DisengageRadius;

        if (IsValidPirateCaseCarrierTarget(currentTarget))
            return currentTarget;

        if (!IsProtectedPirateSymbolTarget(currentTarget) &&
            EnemyTargetingUtility.IsTargetValid(currentTarget, health, rb.position, validRange, true))
            return currentTarget;

        return FindClosestUnprotectedTarget(movement.DetectionRadius);
    }

    Transform FindClosestUnprotectedTarget(float maxDistance)
    {
        Transform bestBeaconTarget = null;
        float bestBeaconDistance = float.MaxValue;
        float beaconRange = maxDistance * BeaconPriorityRangeMultiplier;

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted)
                continue;

            float distance = Vector2.Distance(rb.position, beacon.transform.position);
            if (distance > beaconRange || distance >= bestBeaconDistance)
                continue;

            bestBeaconDistance = distance;
            bestBeaconTarget = beacon.transform;
        }

        if (bestBeaconTarget != null)
            return bestBeaconTarget;

        PlayerHealth pirateCaseCarrier = ValuableCargoCarrierUtility.FindBestPirateCaseCarrier(rb.position, float.PositiveInfinity, health);
        if (pirateCaseCarrier != null)
            return pirateCaseCarrier.transform;

        Transform bestTarget = null;
        float bestDistance = float.MaxValue;
        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (!IsValidUnprotectedPlayerTarget(candidate, maxDistance))
                continue;

            float distance = Vector2.Distance(rb.position, candidate.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestTarget = candidate.transform;
        }

        return bestTarget;
    }

    bool IsValidPirateCaseCarrierTarget(Transform target)
    {
        PlayerHealth targetHealth = target != null ? target.GetComponent<PlayerHealth>() : null;
        return ValuableCargoCarrierUtility.IsPirateCaseCarrier(targetHealth);
    }

    bool IsValidUnprotectedPlayerTarget(PlayerHealth candidate, float maxDistance)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate == health || candidate.IsWreck || candidate.IsBotControlled || candidate.IsAstronautControlled || candidate.IsEvacuationAnimating)
            return false;

        if (candidate.GetComponent<LureBeaconDecoy>() != null)
            return false;

        if (IsProtectedPirateSymbolTarget(candidate.transform))
            return false;

        if (Vector2.Distance(rb.position, candidate.transform.position) > maxDistance)
            return false;

        HideInNebulaTarget candidateNebulaState = candidate.GetComponent<HideInNebulaTarget>();
        HideInNebulaTarget observerNebulaState = health != null ? health.GetComponent<HideInNebulaTarget>() : null;
        return candidateNebulaState == null || !candidateNebulaState.IsHiddenFromObserver(observerNebulaState);
    }

    bool IsProtectedPirateSymbolTarget(Transform target)
    {
        PlayerHealth targetHealth = target != null ? target.GetComponent<PlayerHealth>() : null;
        if (targetHealth == null || targetHealth.photonView == null)
            return false;

        if (ValuableCargoCarrierUtility.IsPirateCaseCarrier(targetHealth))
            return false;

        if (!PlayerProfileService.PlayerHasPirateSymbolInSafePocket(targetHealth.photonView.Owner))
            return false;

        return Time.time >= forcedCombatUntil || targetHealth.photonView.ViewID != forcedCombatTargetViewId;
    }

    void RefreshSquadronIfNeeded()
    {
        if (Time.time < nextSquadronRefreshTime)
            return;

        nextSquadronRefreshTime = Time.time + SquadronRefreshInterval;
        squadmates[0] = null;
        squadmates[1] = null;
        float bestA = float.MaxValue;
        float bestB = float.MaxValue;

        EnemyBot[] bots = FindObjectsByType<EnemyBot>(FindObjectsInactive.Exclude);
        for (int i = 0; i < bots.Length; i++)
        {
            EnemyBot candidate = bots[i];
            if (candidate == null || candidate == bot || !EnemyBot.IsPirateFighterKind(candidate.Kind))
                continue;

            PlayerHealth candidateHealth = candidate.GetComponent<PlayerHealth>();
            if (candidateHealth != null && candidateHealth.IsWreck)
                continue;

            float distance = Vector2.Distance(rb.position, candidate.transform.position);
            if (distance < bestA)
            {
                bestB = bestA;
                squadmates[1] = squadmates[0];
                bestA = distance;
                squadmates[0] = candidate.transform;
            }
            else if (distance < bestB)
            {
                bestB = distance;
                squadmates[1] = candidate.transform;
            }
        }
    }

    void UpdateMode()
    {
        if (IsCriticalHealth())
        {
            mode = PirateMode.CriticalFlee;
            return;
        }

        if (mode == PirateMode.BreakAway)
        {
            if (Time.time < breakAwayUntil)
                return;

            EnterAttackRun();
            return;
        }

        if (IsProtectedPirateSymbolTarget(currentTarget))
            currentTarget = null;

        if (currentTarget != null &&
            (IsValidPirateCaseCarrierTarget(currentTarget) ||
             EnemyTargetingUtility.IsTargetValid(currentTarget, health, rb.position, movement.DisengageRadius, true)))
        {
            if (mode != PirateMode.AttackRun)
                EnterAttackRun();
            return;
        }

        if (Time.time < forcedCombatUntil && currentTarget != null)
        {
            if (mode != PirateMode.AttackRun)
                EnterAttackRun();
            return;
        }

        mode = PirateMode.Patrol;
    }

    void EnterAttackRun()
    {
        mode = PirateMode.AttackRun;
        attackRunStartedAt = Time.time;
    }

    Vector2 ResolveDesiredDirection()
    {
        switch (mode)
        {
            case PirateMode.AttackRun:
                return ResolveAttackRunDirection();
            case PirateMode.BreakAway:
                return ResolveBreakAwayDirection();
            case PirateMode.CriticalFlee:
                return ResolveCriticalFleeDirection();
            default:
                return ResolvePatrolDirection();
        }
    }

    Vector2 ResolvePatrolDirection()
    {
        if (Time.time >= nextPatrolTurnTime)
        {
            nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.5f, 0.5f);
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        return patrolDirection;
    }

    Vector2 ResolveAttackRunDirection()
    {
        if (currentTarget == null)
            return ResolvePatrolDirection();

        Vector2 attackPoint = ResolveRearAttackPoint(currentTarget);
        Vector2 toAttackPoint = attackPoint - rb.position;
        float distanceToAttackPoint = toAttackPoint.magnitude;
        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        Vector2 targetForward = ResolveTargetForward(currentTarget);
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-targetForward.y, targetForward.x)
            : new Vector2(targetForward.y, -targetForward.x);

        if (distanceToAttackPoint > AttackPointTolerance)
            return (toAttackPoint.normalized * 0.92f + tangent * 0.16f).normalized;

        if (toTarget.magnitude < movement.OrbitDistance)
            return (-toTarget.normalized * 0.78f + tangent * 0.42f).normalized;

        return (-targetForward * 0.42f + tangent * 0.58f).normalized;
    }

    Vector2 ResolveBreakAwayDirection()
    {
        if (currentTarget == null)
            return rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection;

        Vector2 away = rb.position - (Vector2)currentTarget.position;
        if (away.sqrMagnitude <= 0.001f)
            away = fallbackFleeDirection;

        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-away.y, away.x)
            : new Vector2(away.y, -away.x);

        return (away.normalized * 0.82f + tangent.normalized * 0.38f).normalized;
    }

    Vector2 ResolveCriticalFleeDirection()
    {
        Transform threat = currentTarget != null
            ? currentTarget
            : FindClosestUnprotectedTarget(movement.DisengageRadius);

        Vector2 away = fallbackFleeDirection;
        if (threat != null)
            away = rb.position - (Vector2)threat.position;

        if (away.sqrMagnitude <= 0.001f)
            away = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection;

        Vector2 centerPull = (-rb.position).sqrMagnitude > 0.001f ? (-rb.position).normalized : Vector2.zero;
        fallbackFleeDirection = (away.normalized * 0.84f + centerPull * 0.28f).normalized;
        return fallbackFleeDirection;
    }

    Vector2 ApplySquadronSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : patrolDirection;
        Vector2 cohesion = Vector2.zero;
        Vector2 separation = Vector2.zero;
        Vector2 alignment = Vector2.zero;
        int count = 0;

        for (int i = 0; i < squadmates.Length; i++)
        {
            Transform mate = squadmates[i];
            if (mate == null)
                continue;

            Vector2 toMate = (Vector2)mate.position - rb.position;
            float distance = toMate.magnitude;
            if (distance <= 0.001f)
                continue;

            count++;
            if (distance > SquadronComfortDistance && distance < SquadronJoinDistance)
                cohesion += toMate.normalized * Mathf.InverseLerp(SquadronComfortDistance, SquadronJoinDistance, distance);

            if (distance < SquadronSeparationDistance)
                separation -= toMate.normalized * Mathf.InverseLerp(SquadronSeparationDistance, 0.12f, distance);

            Rigidbody2D mateBody = mate.GetComponent<Rigidbody2D>();
            if (mateBody != null && mateBody.linearVelocity.sqrMagnitude > 0.001f)
                alignment += mateBody.linearVelocity.normalized;
        }

        if (count <= 0)
            return desired;

        Vector2 steering = desired;
        if (cohesion.sqrMagnitude > 0.001f)
            steering += cohesion.normalized * (mode == PirateMode.Patrol ? 0.42f : 0.2f);
        if (separation.sqrMagnitude > 0.001f)
            steering += separation.normalized * 0.72f;
        if (alignment.sqrMagnitude > 0.001f && mode == PirateMode.Patrol)
            steering += alignment.normalized * 0.16f;

        return steering.sqrMagnitude > 0.001f ? steering.normalized : desired;
    }

    Vector2 ApplyAvoidance(Vector2 desiredDirection)
    {
        if (Time.time < avoidanceSuppressedUntil)
            return ResolveStuckEscapeDirection(desiredDirection);

        Vector2 desired = NormalizeMoveDirection(desiredDirection);
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(rb.position, AvoidanceScanRadius, out Collider2D[] hits);
        Vector2 avoidance = Vector2.zero;
        int closeAvoidedObjects = 0;

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

            if (away.sqrMagnitude > 0.0001f)
            {
                closeAvoidedObjects++;
                avoidance += away.normalized * Mathf.Clamp01((AvoidanceScanRadius - distance) / AvoidanceScanRadius);
            }
        }

        if (avoidance.sqrMagnitude <= 0.001f)
            return desiredDirection;

        UpdateStuckSuppression(closeAvoidedObjects, avoidance, desired);
        if (Time.time < avoidanceSuppressedUntil)
            return ResolveStuckEscapeDirection(desired);

        Vector2 result = (desired + avoidance.normalized * AvoidanceWeight).normalized;
        return Vector2.Dot(result, desired) < 0.12f
            ? (desired * 0.8f + avoidance.normalized * 0.2f).normalized
            : result;
    }

    void UpdateStuckSuppression(int closeAvoidedObjects, Vector2 avoidance, Vector2 desired)
    {
        if (closeAvoidedObjects < 1 || rb.linearVelocity.magnitude > StuckVelocityThreshold)
        {
            lowSpeedSince = 0f;
            return;
        }

        if (lowSpeedSince <= 0f)
        {
            lowSpeedSince = Time.time;
            return;
        }

        if (Time.time - lowSpeedSince >= StuckDuration)
        {
            avoidanceSuppressedUntil = Time.time + AvoidanceSuppressionDuration;
            stuckEscapeUntil = Time.time + StuckEscapeDuration;
            stuckEscapeDirection = ResolveEscapeDirection(avoidance, desired);
            lowSpeedSince = 0f;
        }
    }

    Vector2 ResolveEscapeDirection(Vector2 avoidance, Vector2 desired)
    {
        Vector2 escape = avoidance.sqrMagnitude > 0.001f ? avoidance.normalized : Vector2.zero;
        if (escape.sqrMagnitude <= 0.001f)
            escape = desired.sqrMagnitude > 0.001f ? new Vector2(-desired.y, desired.x).normalized : patrolDirection.normalized;

        if (desired.sqrMagnitude > 0.001f && Vector2.Dot(escape, desired.normalized) < -0.45f)
        {
            Vector2 tangent = new Vector2(-desired.y, desired.x).normalized;
            escape = (escape * 0.55f + tangent * 0.45f).normalized;
        }

        return escape.sqrMagnitude > 0.001f ? escape.normalized : Vector2.up;
    }

    Vector2 ResolveStuckEscapeDirection(Vector2 desiredDirection)
    {
        Vector2 desired = NormalizeMoveDirection(desiredDirection);
        if (Time.time >= stuckEscapeUntil || stuckEscapeDirection.sqrMagnitude <= 0.001f)
            return desired;

        return (desired * 0.3f + stuckEscapeDirection.normalized * 0.7f).normalized;
    }

    Vector2 NormalizeMoveDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
            return direction.normalized;

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.001f)
            return rb.linearVelocity.normalized;

        return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;
    }

    bool IsAvoidedObject(Collider2D hit)
    {
        if (EnemyHazardAvoidanceUtility.IsMineThreat(hit, health))
            return true;

        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        if (hit.GetComponentInParent<DroppedCargoCrate>() != null)
            return true;

        EnemyBot otherBot = hit.GetComponentInParent<EnemyBot>();
        return otherBot != null && otherBot != bot;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : patrolDirection;
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.2f, bot.EffectiveMoveSpeed * 1.35f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + 1.5f, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - 1.5f, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + 1.5f, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - 1.5f, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        Vector2 tangent = new Vector2(-inward.y, inward.x);
        if (Vector2.Dot(tangent, desired) < 0f)
            tangent = -tangent;

        Vector2 edgeTurn = (inward.normalized * 0.88f + tangent.normalized * 0.28f).normalized;
        return (desired * (1f - MapEdgeSteerWeight) + edgeTurn * MapEdgeSteerWeight).normalized;
    }

    float ResolveCurrentSpeed()
    {
        float baseSpeed = bot != null ? bot.EffectiveMoveSpeed : 1f;
        switch (mode)
        {
            case PirateMode.Patrol:
                return baseSpeed * 0.72f;
            case PirateMode.BreakAway:
                return baseSpeed * 1.16f;
            case PirateMode.CriticalFlee:
                return baseSpeed * 1.32f;
            default:
                return baseSpeed;
        }
    }

    void ApplyMinimumMoveSpeed(Vector2 desiredDirection, float speed)
    {
        if (desiredDirection.sqrMagnitude <= 0.001f || speed <= 0f)
            return;

        float minimumSpeed = Mathf.Max(0.18f, speed * MinimumMoveSpeedFraction);
        if (rb.linearVelocity.magnitude >= minimumSpeed)
            return;

        rb.linearVelocity = desiredDirection.normalized * minimumSpeed;
    }

    Vector2 ResolveAimDirection(Vector2 moveDirection)
    {
        if (Time.time < stuckEscapeUntil && moveDirection.sqrMagnitude > 0.001f)
            return moveDirection.normalized;

        if ((mode == PirateMode.AttackRun || mode == PirateMode.BreakAway) && currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            if (toTarget.sqrMagnitude > 0.001f)
                return toTarget.normalized;
        }

        return moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : transform.up;
    }

    void TryShootAtTarget()
    {
        if (shooting == null || weapon == null || currentTarget == null)
            return;

        if (IsProtectedPirateSymbolTarget(currentTarget))
            return;

        Vector2 aim = (Vector2)currentTarget.position - rb.position;
        float distance = aim.magnitude;
        if (distance <= 0.001f || distance > weapon.Range)
            return;

        Vector2 normalizedAim = aim / distance;
        if (Vector2.Dot(transform.up, normalizedAim) < 0.9f)
            return;

        bool inRearArc = IsBehindTarget(currentTarget);
        bool closeToAttackPoint = Vector2.Distance(rb.position, ResolveRearAttackPoint(currentTarget)) <= AttackPointTolerance + 0.35f;
        if (!inRearArc && !closeToAttackPoint)
            return;

        Vector3[] muzzles = ResolveWingMuzzles();
        if (WouldHitOwnPirateBase(muzzles, currentTarget.position))
            return;

        bool fired = shooting.TryFireBotVolleyFromWorld(normalizedAim, muzzles, Random.Range(-FireIntervalJitter, FireIntervalJitter));
        if (fired)
        {
            lastSuccessfulShotTime = Time.time;
            return;
        }

        if (Time.time - attackRunStartedAt > AttackBurstWindow && Time.time - lastSuccessfulShotTime > 0.24f)
            EnterBreakAway();
    }

    bool WouldHitOwnPirateBase(Vector3[] muzzles, Vector3 targetPosition)
    {
        int sourceViewId = bot != null ? bot.PirateBaseLaunchSourceViewId : 0;
        if (sourceViewId <= 0 || muzzles == null || muzzles.Length == 0)
            return false;

        PhotonView sourceView = PhotonView.Find(sourceViewId);
        EnemyBot sourceBase = sourceView != null ? sourceView.GetComponent<EnemyBot>() : null;
        PlayerHealth sourceHealth = sourceBase != null ? sourceBase.GetComponent<PlayerHealth>() : null;
        if (sourceBase == null || sourceBase.Kind != EnemyBotKind.PirateBase || (sourceHealth != null && sourceHealth.IsWreck))
            return false;

        for (int i = 0; i < muzzles.Length; i++)
        {
            Vector2 origin = muzzles[i];
            Vector2 toTarget = (Vector2)targetPosition - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f)
                continue;

            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, toTarget / distance, distance);
            for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
            {
                Collider2D hitCollider = hits[hitIndex].collider;
                EnemyBot hitBot = hitCollider != null ? hitCollider.GetComponentInParent<EnemyBot>() : null;
                if (hitBot == null || hitBot.Kind != EnemyBotKind.PirateBase || hitBot.photonView == null)
                    continue;

                if (hitBot.photonView.ViewID == sourceViewId)
                    return true;
            }
        }

        return false;
    }

    Vector3[] ResolveWingMuzzles()
    {
        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        float lateral = 0.46f;
        float forward = Mathf.Max(0.16f, weapon != null ? weapon.MuzzleOffsetDistance : 0.34f);
        if (renderer != null)
        {
            lateral = Mathf.Max(0.28f, renderer.bounds.extents.x * 0.72f);
            forward = Mathf.Max(forward, renderer.bounds.extents.y * 0.08f);
        }

        int streamCount = Mathf.Clamp(weapon != null && weapon.MuzzleStreamCount > 0 ? weapon.MuzzleStreamCount : 2, 1, 4);
        Vector3 center = transform.position + transform.up * forward;
        if (streamCount == 1)
            return new[] { center };

        Vector3[] muzzles = new Vector3[streamCount];
        for (int i = 0; i < streamCount; i++)
        {
            float side = Mathf.Lerp(-1f, 1f, i / (float)(streamCount - 1));
            muzzles[i] = center + transform.right * (side * lateral);
        }

        return muzzles;
    }

    static string ResolveProjectileEffectId(EnemyBotKind kind)
    {
        return kind == EnemyBotKind.PirateFighterElite || kind == EnemyBotKind.PirateFighterAce
            ? "pirate_fighter_red"
            : "pirate_fighter";
    }

    void EnterBreakAway()
    {
        mode = PirateMode.BreakAway;
        breakAwayUntil = Time.time + BreakAwayDuration;
        if (currentTarget != null)
        {
            Vector2 away = rb.position - (Vector2)currentTarget.position;
            if (away.sqrMagnitude > 0.001f)
                fallbackFleeDirection = away.normalized;
        }
    }

    bool IsBehindTarget(Transform target)
    {
        if (target == null)
            return false;

        Vector2 fromTargetToFighter = rb.position - (Vector2)target.position;
        if (fromTargetToFighter.sqrMagnitude <= 0.001f)
            return false;

        Vector2 targetForward = ResolveTargetForward(target);
        return Vector2.Dot(fromTargetToFighter.normalized, -targetForward) >= RearArcDot;
    }

    Vector2 ResolveRearAttackPoint(Transform target)
    {
        Vector2 targetForward = ResolveTargetForward(target);
        Vector2 targetVelocity = ResolveTargetVelocity(target);
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-targetForward.y, targetForward.x)
            : new Vector2(targetForward.y, -targetForward.x);
        float wave = Mathf.Sin(Time.time * 1.7f + (view != null ? view.ViewID : 1) * 0.19f) * 0.55f;
        return (Vector2)target.position - targetForward * AttackRearOffset + targetVelocity * 0.28f + tangent * wave;
    }

    Vector2 ResolveTargetForward(Transform target)
    {
        if (target == null)
            return Vector2.up;

        Rigidbody2D targetBody = target.GetComponent<Rigidbody2D>();
        if (targetBody != null && targetBody.linearVelocity.sqrMagnitude > 0.2f)
            return targetBody.linearVelocity.normalized;

        Vector2 forward = target.up;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector2.up;
    }

    Vector2 ResolveTargetVelocity(Transform target)
    {
        Rigidbody2D targetBody = target != null ? target.GetComponent<Rigidbody2D>() : null;
        return targetBody != null ? targetBody.linearVelocity : Vector2.zero;
    }

    bool IsCriticalHealth()
    {
        if (health == null || health.maxHP <= 0)
            return false;

        return health.CurrentHP <= Mathf.CeilToInt(health.maxHP * CriticalHpFraction);
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

