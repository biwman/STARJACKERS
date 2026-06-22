using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemyNeutralFighterBehavior : EnemyBotBehaviorBase
{
    enum FighterMode
    {
        Patrol,
        Combat,
        Flee
    }

    const float FleeDuration = 5f;
    const float AvoidanceScanRadius = 2.85f;
    const float AvoidanceWeight = 0.76f;
    const float MapEdgeSoftTurnMargin = 5.4f;
    const float MapEdgeHardTurnMargin = 1.15f;
    const float MapEdgeLookAheadSeconds = 1.2f;
    const float MapEdgeMinimumLookAhead = 1.1f;
    const float MapEdgeMaximumLookAhead = 3.4f;
    const float MapEdgeTurnTangentWeight = 0.62f;
    const float FireIntervalJitter = 0.1f;
    const float StuckVelocityThreshold = 0.16f;
    const float StuckDuration = 0.42f;
    const float AvoidanceSuppressionDuration = 0.85f;
    const float StuckEscapeDuration = 0.7f;
    const float MinimumMoveSpeedFraction = 0.28f;

    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    FighterMode mode = FighterMode.Patrol;
    Transform currentTarget;
    Vector2 patrolDirection = Vector2.up;
    Vector2 fleeDirection = Vector2.right;
    float nextTargetRefreshTime;
    float nextRepathTime;
    float nextPatrolTurnTime;
    float fleeUntil;
    float orbitDirection = 1f;
    float lowSpeedSince;
    float avoidanceSuppressedUntil;
    float stuckEscapeUntil;
    Vector2 stuckEscapeDirection = Vector2.up;
    float edgeAvoidanceStrength;
    Vector2 edgeInwardNormal;

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
        float angle = Mathf.Abs(seed * 0.211f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        orbitDirection = seed % 2 == 0 ? 1f : -1f;

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
                string.Empty,
                10f,
                weapon.DamageType,
                weapon.DeliveryMethod,
                weapon.DeliveryFlags);
        }
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        RefreshTargetIfNeeded();
        UpdateMode();

        edgeAvoidanceStrength = 0f;
        edgeInwardNormal = Vector2.zero;

        Vector2 desiredDirection = ResolveDesiredDirection();
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        desiredDirection = ApplyAvoidance(desiredDirection);
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        desiredDirection = ResolveStuckEscapeDirection(desiredDirection);
        if (desiredDirection.sqrMagnitude <= 0.001f)
            desiredDirection = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection.normalized;

        float speed = ResolveCurrentSpeed();
        Vector2 desiredVelocity = desiredDirection.normalized * speed;
        float velocityBlend = mode == FighterMode.Combat ? 0.2f : 0.13f;
        if (edgeAvoidanceStrength > 0.001f)
            velocityBlend = Mathf.Max(velocityBlend, Mathf.Lerp(0.22f, 0.54f, edgeAvoidanceStrength));

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, velocityBlend);
        ApplyMinimumMoveSpeed(desiredDirection, speed);

        Vector2 aimDirection = ResolveAimDirection(desiredDirection);
        if (edgeAvoidanceStrength > 0.58f &&
            edgeInwardNormal.sqrMagnitude > 0.001f &&
            aimDirection.sqrMagnitude > 0.001f &&
            Vector2.Dot(aimDirection.normalized, edgeInwardNormal.normalized) < -0.12f)
        {
            aimDirection = desiredDirection;
        }

        RotateNoseToward(aimDirection);

        if (mode == FighterMode.Combat)
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

        fleeDirection = away.normalized;
        fleeUntil = Time.time + FleeDuration;
        mode = FighterMode.Flee;
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.12f, movement.TargetRefreshInterval);
        currentTarget = ResolveTarget();
    }

    void UpdateMode()
    {
        if (mode == FighterMode.Flee)
        {
            if (Time.time < fleeUntil)
                return;

            if (currentTarget == null || !IsTargetWithin(currentTarget, movement.DetectionRadius))
                mode = FighterMode.Patrol;
            else
                mode = FighterMode.Combat;
            return;
        }

        if (currentTarget != null && IsTargetWithin(currentTarget, movement.DetectionRadius))
        {
            mode = FighterMode.Combat;
            return;
        }

        if (mode == FighterMode.Combat && (currentTarget == null || !IsTargetWithin(currentTarget, movement.DisengageRadius)))
            mode = FighterMode.Patrol;
    }

    Vector2 ResolveDesiredDirection()
    {
        switch (mode)
        {
            case FighterMode.Combat:
                return ResolveCombatDirection();
            case FighterMode.Flee:
                return fleeDirection.sqrMagnitude > 0.001f ? fleeDirection.normalized : -transform.up;
            default:
                return ResolvePatrolDirection();
        }
    }

    Vector2 ResolvePatrolDirection()
    {
        if (Time.time >= nextPatrolTurnTime)
        {
            nextPatrolTurnTime = Time.time + Random.Range(1.2f, 2.4f);
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.55f, 0.55f);
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        Vector2 boundarySteer = ResolveMapBoundarySteering(patrolDirection, out float boundaryStrength, out _);
        if (boundaryStrength > 0.001f)
        {
            float steerBlend = Mathf.Clamp01(0.28f + boundaryStrength * 0.62f);
            patrolDirection = (patrolDirection.normalized * (1f - steerBlend) + boundarySteer * steerBlend).normalized;
        }

        return patrolDirection.normalized;
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

        float slowOrbitWave = Mathf.Sin(Time.time * 1.8f + view.ViewID * 0.17f) * 0.18f;
        if (distance > movement.PreferredDistance + 0.6f)
            return (toward * 0.86f + tangent * (0.26f + slowOrbitWave)).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.72f + tangent * 0.68f).normalized;

        return (tangent * 0.88f + toward * 0.16f).normalized;
    }

    Vector2 ApplyAvoidance(Vector2 desiredDirection)
    {
        if (Time.time < avoidanceSuppressedUntil)
            return ResolveStuckEscapeDirection(desiredDirection);

        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f
            ? desiredDirection.normalized
            : rb.linearVelocity.sqrMagnitude > 0.001f
                ? rb.linearVelocity.normalized
                : patrolDirection.normalized;
        Vector2 avoidance = Vector2.zero;
        int closeAvoidedObjects = 0;
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(rb.position, AvoidanceScanRadius, out Collider2D[] hits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            if (!IsAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(rb.position);
            Vector2 toObstacle = closest - rb.position;
            if (toObstacle.sqrMagnitude > 0.0001f && Vector2.Dot(toObstacle.normalized, desired) < -0.2f)
                continue;

            Vector2 away = rb.position - closest;
            float distance = Mathf.Max(0.12f, away.magnitude);
            if (away.sqrMagnitude <= 0.0001f)
                away = rb.position - (Vector2)hit.transform.position;

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

        Vector2 blended = (desired + avoidance.normalized * AvoidanceWeight).normalized;
        if (Vector2.Dot(blended, desired) < 0.2f)
            blended = (desired * 0.82f + avoidance.normalized * 0.18f).normalized;

        return blended;
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

        return (desired * 0.32f + stuckEscapeDirection.normalized * 0.68f).normalized;
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

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = NormalizeMoveDirection(desiredDirection);
        Vector2 edgeSteer = ResolveMapBoundarySteering(desired, out float strength, out Vector2 inwardNormal);
        if (strength <= 0.001f)
            return desiredDirection;

        if (strength > edgeAvoidanceStrength)
        {
            edgeAvoidanceStrength = strength;
            edgeInwardNormal = inwardNormal;
        }

        float inwardDot = inwardNormal.sqrMagnitude > 0.001f ? Vector2.Dot(desired, inwardNormal.normalized) : 0f;
        float blend = Mathf.Clamp01(0.28f + strength * 0.72f);
        if (inwardDot < -0.05f)
            blend = Mathf.Clamp01(blend + 0.22f);

        Vector2 result = (desired * (1f - blend) + edgeSteer * blend).normalized;
        if (strength > 0.72f && inwardNormal.sqrMagnitude > 0.001f && Vector2.Dot(result, inwardNormal.normalized) < 0.18f)
            result = (result * 0.42f + inwardNormal.normalized * 0.92f).normalized;

        return result;
    }

    Vector2 NormalizeMoveDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
            return direction.normalized;

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.001f)
            return rb.linearVelocity.normalized;

        return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;
    }

    Vector2 ResolveMapBoundarySteering(Vector2 desiredDirection, out float strength, out Vector2 inwardNormal)
    {
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float bodyRadius = Mathf.Clamp(bot != null ? bot.VisualTargetSize * 0.58f : 0.55f, 0.35f, 1.2f);
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - bodyRadius);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - bodyRadius);
        Vector2 desired = NormalizeMoveDirection(desiredDirection);
        float lookAheadDistance = Mathf.Clamp(
            ResolveCurrentSpeed() * MapEdgeLookAheadSeconds,
            MapEdgeMinimumLookAhead,
            MapEdgeMaximumLookAhead);
        Vector2 predictedPosition = rb.position + desired * lookAheadDistance;
        Vector2 push = Vector2.zero;
        float maxStrength = 0f;

        float rightDistance = halfX - predictedPosition.x;
        if (rightDistance < MapEdgeSoftTurnMargin)
        {
            float axisStrength = CalculateMapEdgeStrength(rightDistance);
            push.x -= axisStrength;
            maxStrength = Mathf.Max(maxStrength, axisStrength);
        }

        float leftDistance = predictedPosition.x + halfX;
        if (leftDistance < MapEdgeSoftTurnMargin)
        {
            float axisStrength = CalculateMapEdgeStrength(leftDistance);
            push.x += axisStrength;
            maxStrength = Mathf.Max(maxStrength, axisStrength);
        }

        float topDistance = halfY - predictedPosition.y;
        if (topDistance < MapEdgeSoftTurnMargin)
        {
            float axisStrength = CalculateMapEdgeStrength(topDistance);
            push.y -= axisStrength;
            maxStrength = Mathf.Max(maxStrength, axisStrength);
        }

        float bottomDistance = predictedPosition.y + halfY;
        if (bottomDistance < MapEdgeSoftTurnMargin)
        {
            float axisStrength = CalculateMapEdgeStrength(bottomDistance);
            push.y += axisStrength;
            maxStrength = Mathf.Max(maxStrength, axisStrength);
        }

        if (push.sqrMagnitude <= 0.001f)
        {
            strength = 0f;
            inwardNormal = Vector2.zero;
            return Vector2.zero;
        }

        inwardNormal = push.normalized;
        strength = Mathf.Clamp01(maxStrength);

        bool affectsX = Mathf.Abs(push.x) > 0.001f;
        bool affectsY = Mathf.Abs(push.y) > 0.001f;
        Vector2 tangent = new Vector2(-inwardNormal.y, inwardNormal.x);
        if (Vector2.Dot(tangent, desired) < 0f)
            tangent = -tangent;

        float outwardAmount = Mathf.Clamp01(-Vector2.Dot(desired, inwardNormal));
        float tangentWeight = affectsX != affectsY
            ? Mathf.Lerp(MapEdgeTurnTangentWeight, 0.2f, outwardAmount)
            : Mathf.Lerp(0.28f, 0.12f, outwardAmount);

        Vector2 steering = inwardNormal + tangent * tangentWeight;
        return steering.sqrMagnitude > 0.001f ? steering.normalized : inwardNormal;
    }

    float CalculateMapEdgeStrength(float distanceToEdge)
    {
        float softStrength = Mathf.Clamp01((MapEdgeSoftTurnMargin - distanceToEdge) / MapEdgeSoftTurnMargin);
        if (distanceToEdge < MapEdgeHardTurnMargin)
        {
            float hardStrength = Mathf.InverseLerp(MapEdgeHardTurnMargin, -MapEdgeHardTurnMargin, distanceToEdge);
            softStrength = Mathf.Max(softStrength, 0.72f + hardStrength * 0.28f);
        }

        return Mathf.Clamp01(softStrength);
    }

    float ResolveCurrentSpeed()
    {
        float baseSpeed = bot != null ? bot.EffectiveMoveSpeed : 1f;
        return mode == FighterMode.Patrol ? baseSpeed * 0.5f : baseSpeed;
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

        if (mode == FighterMode.Combat && currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            if (toTarget.sqrMagnitude > 0.001f)
                return toTarget.normalized;
        }

        return moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : transform.up;
    }

    void RotateNoseToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    void TryShootAtTarget()
    {
        if (shooting == null || weapon == null || currentTarget == null)
            return;

        Vector2 aim = (Vector2)currentTarget.position - rb.position;
        float distance = aim.magnitude;
        if (distance <= 0.001f || distance > weapon.Range)
            return;

        Vector2 normalizedAim = aim / distance;
        if (Vector2.Dot(transform.up, normalizedAim) < 0.92f)
            return;

        Vector3 muzzle = transform.position + transform.up * Mathf.Max(0.1f, weapon.MuzzleOffsetDistance);
        float cooldownJitter = Random.Range(-FireIntervalJitter, FireIntervalJitter);
        shooting.TryFireBotFromWorld(normalizedAim, muzzle, cooldownJitter);
    }

    Transform ResolveTarget()
    {
        float allowedRange = mode == FighterMode.Combat ? movement.DisengageRadius : movement.DetectionRadius;
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, transform.position, allowedRange, true))
            return currentTarget;

        return FindClosestVisibleHumanTarget(movement.DetectionRadius);
    }

    Transform FindClosestVisibleHumanTarget(float maxDistance)
    {
        return EnemyTargetingUtility.FindClosestTarget(transform.position, health, maxDistance, true);
    }

    bool IsValidVisibleTarget(PlayerHealth candidate, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(candidate != null ? candidate.transform : null, health, transform.position, maxDistance, true);
    }

    bool IsTargetWithin(Transform target, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(target, health, transform.position, maxDistance, true);
    }
}

