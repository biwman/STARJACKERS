using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemyRescueShipBehavior : EnemyBotBehaviorBase
{
    const float HealRange = 5.9f;
    const float BeamBreakRange = 7.7f;
    const float HealPerSecond = 10f;
    const float HealStandOffDistance = 4.3f;
    const float HealAnchorTolerance = 0.92f;
    const float MinimumHealLockDuration = 1f;
    const float PatrolTurnIntervalMin = 1.35f;
    const float PatrolTurnIntervalMax = 2.6f;
    const float PatrolSpeedMultiplier = 0.78f;
    const float EntrySpeedMultiplier = 2.45f;
    const float MapEdgeMargin = 2.6f;
    const float MapEdgeSteerWeight = 0.82f;
    const float RecoveryEdgeThreshold = 2.1f;
    const float AvoidanceScanRadius = 2.1f;
    const float AvoidanceWeight = 0.4f;

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    Collider2D bodyCollider;
    readonly System.Collections.Generic.List<Collider2D> wallColliders = new System.Collections.Generic.List<Collider2D>(4);
    PlayerHealth currentHealTarget;
    Vector2 patrolDirection = Vector2.up;
    float nextTargetRefreshTime;
    float nextPatrolTurnTime;
    float healAccumulator;
    float healLockEndTime;
    int activeBeamTargetViewId = -1;
    bool wallCollisionsIgnoredWhileEntering;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        bodyCollider = owner.GetComponent<Collider2D>();
        RefreshWallColliders();

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.193f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
    }

    void OnDisable()
    {
        SetWallCollisionIgnored(false);
        StopBeam();
    }

    void OnDestroy()
    {
        SetWallCollisionIgnored(false);
        StopBeam();
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
        {
            StopBeam();
            return;
        }

        RefreshTargetIfNeeded();

        bool enteringMap = !IsInsidePlayableBounds(rb.position);
        SetWallCollisionIgnored(enteringMap);
        bool hadActiveHealingState = currentHealTarget != null || activeBeamTargetViewId > 0 || healLockEndTime > Time.time;
        bool canHealCurrentTarget = IsHealableTarget(currentHealTarget, bot);
        Vector2 desiredDirection;
        bool withinHealRange = false;

        if (canHealCurrentTarget)
        {
            desiredDirection = ResolveHealDirection(currentHealTarget, out withinHealRange);
        }
        else
        {
            currentHealTarget = null;
            if (hadActiveHealingState)
                EnterPatrolMode(enteringMap);
            desiredDirection = enteringMap ? (-rb.position).normalized : ResolvePatrolDirection();
        }

        desiredDirection = ApplyAvoidance(desiredDirection);
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        if (!canHealCurrentTarget && IsNearMapEdge(rb.position, RecoveryEdgeThreshold))
            desiredDirection = Vector2.Lerp(desiredDirection.normalized, (-rb.position).normalized, 0.72f).normalized;
        if (desiredDirection.sqrMagnitude <= 0.001f)
            desiredDirection = patrolDirection.sqrMagnitude > 0.001f ? patrolDirection : Vector2.up;

        bool sustainHealLock = canHealCurrentTarget
            && activeBeamTargetViewId > 0
            && Time.time < healLockEndTime
            && Vector2.Distance(rb.position, GetTargetPoint(currentHealTarget)) <= BeamBreakRange;
        bool healingThisFrame = canHealCurrentTarget && (withinHealRange || sustainHealLock);

        float speedMultiplier = enteringMap ? EntrySpeedMultiplier : canHealCurrentTarget ? 1f : PatrolSpeedMultiplier;
        if (healingThisFrame)
            speedMultiplier = 0.16f;

        Vector2 desiredVelocity = desiredDirection.normalized * (bot.EffectiveMoveSpeed * speedMultiplier);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, healingThisFrame ? 0.24f : 0.14f);

        Vector2 aimDirection = canHealCurrentTarget
            ? (GetTargetPoint(currentHealTarget) - rb.position)
            : rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : desiredDirection;
        RotateHullToward(aimDirection);

        if (healingThisFrame)
        {
            PhotonView targetView = currentHealTarget != null ? currentHealTarget.GetComponent<PhotonView>() : null;
            int targetViewId = targetView != null ? targetView.ViewID : -1;
            bool startedNewBeam = targetViewId > 0 && activeBeamTargetViewId != targetViewId;
            StartBeam(currentHealTarget);
            if (startedNewBeam)
                healLockEndTime = Time.time + MinimumHealLockDuration;
            ApplyHealing(currentHealTarget);
        }
        else
        {
            healAccumulator = 0f;
            healLockEndTime = 0f;
            StopBeam();
        }
    }

    public static bool TryFindNearestDamagedAlly(Vector2 origin, EnemyBot selfBot, out PlayerHealth result)
    {
        result = null;
        float bestDistance = float.MaxValue;
        PlayerHealth[] candidates = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerHealth candidate = candidates[i];
            if (!IsHealableTarget(candidate, selfBot))
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            result = candidate;
        }

        return result != null;
    }

    static bool IsHealableTarget(PlayerHealth candidate, EnemyBot selfBot)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.IsWreck || candidate.IsEvacuationAnimating || !candidate.IsBotControlled)
            return false;

        EnemyBot candidateBot = candidate.GetComponent<EnemyBot>();
        if (candidateBot == null || candidateBot == selfBot)
            return false;

        if (candidateBot.Kind == EnemyBotKind.SpaceMine || candidateBot.Kind == EnemyBotKind.RescueShip)
            return false;

        return candidate.CurrentHP < candidate.maxHP || candidate.CurrentShield < candidate.MaxShield;
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < healLockEndTime && IsHealableTarget(currentHealTarget, bot))
            return;

        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.14f, movement.TargetRefreshInterval);
        TryFindNearestDamagedAlly(rb.position, bot, out currentHealTarget);
    }

    Vector2 ResolveHealDirection(PlayerHealth target, out bool withinRange)
    {
        Vector2 targetPoint = GetTargetPoint(target);
        Vector2 anchorPoint = GetHealAnchorPoint(targetPoint);
        Vector2 toAnchor = anchorPoint - rb.position;
        float anchorDistance = toAnchor.magnitude;
        float targetDistance = Vector2.Distance(rb.position, targetPoint);
        withinRange = targetDistance <= HealRange && anchorDistance <= HealAnchorTolerance;
        if (anchorDistance <= 0.001f)
            return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection : Vector2.up;

        if (targetDistance <= BeamBreakRange)
            return toAnchor / anchorDistance;

        return toAnchor / anchorDistance;
    }

    Vector2 ResolvePatrolDirection()
    {
        if (IsNearMapEdge(rb.position, RecoveryEdgeThreshold))
            return (-rb.position).normalized;

        if (Time.time >= nextPatrolTurnTime)
        {
            nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.5f, 0.5f);
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        return patrolDirection;
    }

    void EnterPatrolMode(bool enteringMap)
    {
        healAccumulator = 0f;
        healLockEndTime = 0f;
        StopBeam();

        Vector2 fallbackDirection;
        if (enteringMap || IsNearMapEdge(rb.position, RecoveryEdgeThreshold))
        {
            fallbackDirection = (-rb.position).sqrMagnitude > 0.001f ? (-rb.position).normalized : Vector2.up;
        }
        else if (rb.linearVelocity.sqrMagnitude > 0.001f)
        {
            fallbackDirection = rb.linearVelocity.normalized;
        }
        else
        {
            fallbackDirection = patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;
        }

        patrolDirection = fallbackDirection;
        nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
        rb.linearVelocity = patrolDirection * (bot.EffectiveMoveSpeed * Mathf.Max(PatrolSpeedMultiplier, 0.92f));
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

            float distance = Mathf.Max(0.1f, away.magnitude);
            if (distance > AvoidanceScanRadius)
                continue;

            avoidance += away.normalized * Mathf.Clamp01((AvoidanceScanRadius - distance) / AvoidanceScanRadius);
        }

        if (avoidance.sqrMagnitude <= 0.001f)
            return desiredDirection;

        Vector2 result = (desired + avoidance.normalized * AvoidanceWeight).normalized;
        return Vector2.Dot(result, desired) < 0.1f
            ? (desired * 0.76f + avoidance.normalized * 0.24f).normalized
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

        if (hit.GetComponentInParent<DroppedCargoCrate>() != null)
            return true;

        EnemyBot otherBot = hit.GetComponentInParent<EnemyBot>();
        return otherBot != null && otherBot != bot;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.2f, bot.EffectiveMoveSpeed * 1.8f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + 1.8f, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - 1.8f, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + 1.8f, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - 1.8f, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desired * (1f - MapEdgeSteerWeight) + inward.normalized * MapEdgeSteerWeight).normalized;
    }

    bool IsInsidePlayableBounds(Vector2 position)
    {
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        return Mathf.Abs(position.x) <= halfX && Mathf.Abs(position.y) <= halfY;
    }

    bool IsNearMapEdge(Vector2 position, float threshold)
    {
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        return halfX - Mathf.Abs(position.x) <= threshold || halfY - Mathf.Abs(position.y) <= threshold;
    }

    Vector2 GetHealAnchorPoint(Vector2 targetPoint)
    {
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        float leftClearance = targetPoint.x + halfX;
        float rightClearance = halfX - targetPoint.x;
        float bottomClearance = targetPoint.y + halfY;
        float topClearance = halfY - targetPoint.y;

        Vector2 inward = (-targetPoint).sqrMagnitude > 0.001f ? (-targetPoint).normalized : Vector2.up;
        float smallestClearance = Mathf.Min(Mathf.Min(leftClearance, rightClearance), Mathf.Min(bottomClearance, topClearance));
        if (smallestClearance == leftClearance)
            inward = Vector2.right;
        else if (smallestClearance == rightClearance)
            inward = Vector2.left;
        else if (smallestClearance == bottomClearance)
            inward = Vector2.up;
        else if (smallestClearance == topClearance)
            inward = Vector2.down;

        return targetPoint + inward.normalized * HealStandOffDistance;
    }

    void RefreshWallColliders()
    {
        wallColliders.Clear();
        string[] wallNames = { "WallTop", "WallBottom", "WallLeft", "WallRight" };
        for (int i = 0; i < wallNames.Length; i++)
        {
            GameObject wall = GameObject.Find(wallNames[i]);
            if (wall == null)
                continue;

            Collider2D wallCollider = wall.GetComponent<Collider2D>();
            if (wallCollider != null)
                wallColliders.Add(wallCollider);
        }
    }

    void SetWallCollisionIgnored(bool ignored)
    {
        if (bodyCollider == null)
            return;

        if (wallColliders.Count == 0 || wallColliders.TrueForAll(collider => collider == null))
            RefreshWallColliders();

        if (wallCollisionsIgnoredWhileEntering == ignored)
            return;

        wallCollisionsIgnoredWhileEntering = ignored;
        for (int i = 0; i < wallColliders.Count; i++)
        {
            Collider2D wallCollider = wallColliders[i];
            if (wallCollider == null)
                continue;

            Physics2D.IgnoreCollision(bodyCollider, wallCollider, ignored);
        }
    }

    Vector2 GetTargetPoint(PlayerHealth target)
    {
        if (target == null)
            return rb.position;

        Collider2D collider = target.GetComponentInChildren<Collider2D>();
        if (collider != null)
            return collider.ClosestPoint(rb.position);

        return target.transform.position;
    }

    void RotateHullToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    void StartBeam(PlayerHealth target)
    {
        PhotonView targetView = target != null ? target.GetComponent<PhotonView>() : null;
        if (targetView == null || bot == null || bot.photonView == null)
            return;

        if (activeBeamTargetViewId == targetView.ViewID)
            return;

        StopBeam();
        activeBeamTargetViewId = targetView.ViewID;
        bot.photonView.RPC(nameof(EnemyBot.StartRescueShipBeamRpc), RpcTarget.All, activeBeamTargetViewId);
    }

    void StopBeam()
    {
        if (activeBeamTargetViewId <= 0 || bot == null || bot.photonView == null)
        {
            activeBeamTargetViewId = -1;
            return;
        }

        bot.photonView.RPC(nameof(EnemyBot.StopRescueShipBeamRpc), RpcTarget.All);
        activeBeamTargetViewId = -1;
    }

    void ApplyHealing(PlayerHealth target)
    {
        if (target == null)
            return;

        healAccumulator += HealPerSecond * Time.fixedDeltaTime;
        int wholePoints = Mathf.FloorToInt(healAccumulator);
        if (wholePoints <= 0)
            return;

        healAccumulator -= wholePoints;
        target.RepairVitalsAuthority(wholePoints);
        if (target.HasFullVitals)
        {
            if (Time.time >= healLockEndTime)
            {
                currentHealTarget = null;
                nextTargetRefreshTime = 0f;
                healLockEndTime = 0f;
                StopBeam();
            }
        }
    }
}

