using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemyGravitySquidBehavior : EnemyBotBehaviorBase
{
    enum SquidMode
    {
        Patrol,
        Stalk,
        Windup,
        Channel,
        Recovery
    }

    const float WindupDuration = 1.05f;
    const float ChannelDuration = 2.45f;
    const float RecoveryDuration = 0.95f;
    const float BreakDistance = 13.2f;
    const float DamageTickInterval = 0.5f;
    const float PullTickInterval = 0.16f;
    const float PullEffectDuration = 0.28f;
    const float PullAcceleration = 12.2f;
    const float PullMaxSpeed = 8.2f;
    const float ChannelMoveSpeedMultiplier = 0.52f;
    const float AvoidanceScanRadius = 2.75f;
    const float AvoidanceWeight = 0.54f;
    const float MapEdgeMargin = 2.6f;
    const float PatrolTurnIntervalMin = 1.2f;
    const float PatrolTurnIntervalMax = 2.3f;

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    EnemySpriteFrameAnimator frameAnimator;
    Transform currentTarget;
    Vector2 patrolDirection = Vector2.up;
    int activeTargetViewId;
    float nextTargetRefreshTime;
    float nextRepathTime;
    float nextPatrolTurnTime;
    float nextAttackTime;
    float nextDamageTickTime;
    float nextPullTickTime;
    float modeStartedAt;
    float orbitDirection = 1f;
    SquidMode mode = SquidMode.Patrol;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;
        frameAnimator = owner.GetComponent<EnemySpriteFrameAnimator>();

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.217f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        nextAttackTime = Time.time + Random.Range(1.1f, 2.2f);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        if (frameAnimator == null)
            frameAnimator = GetComponent<EnemySpriteFrameAnimator>();

        switch (mode)
        {
            case SquidMode.Windup:
                TickWindup();
                break;
            case SquidMode.Channel:
                TickChannel();
                break;
            case SquidMode.Recovery:
                TickRecovery();
                break;
            default:
                RefreshTargetIfNeeded();
                TickPatrolOrStalk();
                break;
        }
    }

    void OnDisable()
    {
        StopActiveTetherVfx(false);
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView == null)
            return;

        PlayerHealth attackerHealth = attackerView.GetComponent<PlayerHealth>();
        if (attackerHealth != null && attackerHealth.IsAstronautControlled && !attackerHealth.IsEnemyAstronautControlled)
            return;

        currentTarget = attackerView.transform;
        nextTargetRefreshTime = Time.time + 0.35f;
        nextAttackTime = Mathf.Min(nextAttackTime, Time.time + 0.35f);
        if (mode == SquidMode.Patrol)
            mode = SquidMode.Stalk;
    }

    void TickPatrolOrStalk()
    {
        SetAnimationSpeed(currentTarget != null ? 1.08f : 0.82f);

        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            float distance = toTarget.magnitude;
            if (distance > 0.001f && distance <= ResolveTetherRange() && Time.time >= nextAttackTime)
            {
                BeginWindup(currentTarget);
                return;
            }

            mode = SquidMode.Stalk;
        }
        else
        {
            mode = SquidMode.Patrol;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + Mathf.Max(0.12f, movement.RepathInterval);
            patrolDirection = currentTarget != null ? ResolveStalkDirection() : ResolvePatrolDirection();
        }

        Vector2 desired = ApplyAvoidance(ApplyMapEdgeSteering(patrolDirection));
        if (desired.sqrMagnitude <= 0.001f)
            desired = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : Vector2.up;

        float speedMultiplier = currentTarget != null ? 1f : 0.5f;
        Vector2 desiredVelocity = desired.normalized * (bot.EffectiveMoveSpeed * speedMultiplier);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, currentTarget != null ? 0.16f : 0.09f);

        Vector2 lookDirection = currentTarget != null
            ? (Vector2)currentTarget.position - rb.position
            : rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : desired;
        RotateToward(lookDirection);
    }

    void BeginWindup(Transform target)
    {
        PhotonView targetView = target != null ? target.GetComponentInParent<PhotonView>() : null;
        if (targetView == null)
            return;

        activeTargetViewId = targetView.ViewID;
        mode = SquidMode.Windup;
        modeStartedAt = Time.time;
        rb.linearVelocity *= 0.28f;
        SetAnimationSpeed(1.55f);

        if (bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.PlayGravitySquidWarningRpc), RpcTarget.All, rb.position.x, rb.position.y, transform.position.z);
    }

    void TickWindup()
    {
        SetAnimationSpeed(1.65f);
        if (!IsActiveTargetStillValid(ResolveTetherRange() + 1.5f))
        {
            BeginRecovery();
            return;
        }

        Transform target = ResolveActiveTargetTransform();
        Vector2 toTarget = target != null ? (Vector2)target.position - rb.position : patrolDirection;
        RotateToward(toTarget);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, -toTarget.normalized * (bot.EffectiveMoveSpeed * 0.12f), 0.18f);

        if (Time.time - modeStartedAt >= ScaleEnemyAttackWindup(WindupDuration))
            BeginChannel();
    }

    void BeginChannel()
    {
        mode = SquidMode.Channel;
        modeStartedAt = Time.time;
        nextDamageTickTime = 0f;
        nextPullTickTime = 0f;
        SetAnimationSpeed(2.05f);

        if (bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.StartGravitySquidTetherRpc), RpcTarget.All, activeTargetViewId);
    }

    void TickChannel()
    {
        SetAnimationSpeed(2.05f);
        if (!IsActiveTargetStillValid(BreakDistance))
        {
            BeginRecovery();
            return;
        }

        Transform target = ResolveActiveTargetTransform();
        Vector2 toTarget = target != null ? (Vector2)target.position - rb.position : patrolDirection;
        RotateToward(toTarget);

        Vector2 channelDirection = ResolveChannelMoveDirection(target);
        rb.linearVelocity = Vector2.Lerp(
            rb.linearVelocity,
            channelDirection * (bot.EffectiveMoveSpeed * ChannelMoveSpeedMultiplier),
            0.13f);

        if (Time.time >= nextDamageTickTime)
        {
            nextDamageTickTime = Time.time + DamageTickInterval;
            ApplyTetherDamageTick();
        }

        if (Time.time >= nextPullTickTime)
        {
            nextPullTickTime = Time.time + PullTickInterval;
            ApplyTetherPullTick();
        }

        if (Time.time - modeStartedAt >= ChannelDuration)
            BeginRecovery();
    }

    void BeginRecovery()
    {
        StopActiveTetherVfx();
        mode = SquidMode.Recovery;
        modeStartedAt = Time.time;
        nextAttackTime = Time.time + ResolveTetherCooldown();
        SetAnimationSpeed(0.72f);
        rb.linearVelocity *= 0.55f;
        activeTargetViewId = 0;
    }

    void TickRecovery()
    {
        SetAnimationSpeed(0.72f);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, 0.065f);
        RotateToward(rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : patrolDirection);

        if (Time.time - modeStartedAt >= RecoveryDuration)
        {
            RefreshTargetIfNeeded();
            mode = currentTarget != null ? SquidMode.Stalk : SquidMode.Patrol;
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
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, rb.position, allowedRange, true, true))
            return currentTarget;

        return EnemyTargetingUtility.FindClosestTarget(rb.position, health, movement.DetectionRadius, true, true);
    }

    Vector2 ResolvePatrolDirection()
    {
        if (Time.time >= nextPatrolTurnTime)
        {
            nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.48f, 0.48f);
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
        float wave = Mathf.Sin(Time.time * 1.15f + (view != null ? view.ViewID : 1) * 0.17f) * 0.14f;

        if (distance > movement.PreferredDistance)
            return (toward * 0.74f + tangent * (0.34f + wave)).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.62f + tangent * 0.72f).normalized;

        return (tangent * 0.9f + toward * 0.1f).normalized;
    }

    Vector2 ResolveChannelMoveDirection(Transform target)
    {
        if (target == null)
            return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;

        Vector2 toTarget = (Vector2)target.position - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return Vector2.zero;

        Vector2 toward = toTarget / distance;
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-toward.y, toward.x)
            : new Vector2(toward.y, -toward.x);

        if (distance > movement.PreferredDistance + 1.2f)
            return (toward * 0.36f + tangent * 0.24f).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.54f + tangent * 0.32f).normalized;

        return (tangent * 0.42f + toward * 0.05f).normalized;
    }

    bool IsActiveTargetStillValid(float maxDistance)
    {
        PhotonView targetView = activeTargetViewId > 0 ? PhotonView.Find(activeTargetViewId) : null;
        if (targetView == null)
            return false;

        float distance = Vector2.Distance(rb.position, targetView.transform.position);
        if (distance > maxDistance)
            return false;

        LureBeaconDecoy beacon = targetView.GetComponent<LureBeaconDecoy>();
        if (beacon != null)
            return beacon.CanBeTargeted;

        PlayerDeployableBase deployable = targetView.GetComponent<PlayerDeployableBase>();
        if (deployable != null)
            return deployable.CanBeTargeted;

        PlayerHealth player = targetView.GetComponent<PlayerHealth>();
        return player != null && player != health && !player.IsWreck && !player.IsBotControlled && ActorIdentity.CanBeTargetedByMonstersActor(player) && !player.IsEvacuationAnimating;
    }

    Transform ResolveActiveTargetTransform()
    {
        PhotonView targetView = activeTargetViewId > 0 ? PhotonView.Find(activeTargetViewId) : null;
        return targetView != null ? targetView.transform : null;
    }

    void ApplyTetherDamageTick()
    {
        PhotonView targetView = activeTargetViewId > 0 ? PhotonView.Find(activeTargetViewId) : null;
        if (targetView == null)
            return;

        Vector2 impact = ResolveTargetImpactPoint(targetView.transform);
        int attackerViewId = bot.photonView != null ? bot.photonView.ViewID : 0;
        int baseDamagePerSecond = Mathf.Max(0, RoomSettings.GetEnemyDamage(bot.Kind));
        int shieldDamage = Mathf.Max(1, Mathf.CeilToInt(baseDamagePerSecond * DamageTickInterval));
        int hpDamage = Mathf.Max(1, Mathf.CeilToInt(baseDamagePerSecond * 0.5f * DamageTickInterval));
        WeaponHitContext hitContext = weapon != null
            ? new WeaponHitContext(weapon.DamageType, weapon.DeliveryMethod, weapon.DeliveryFlags, string.Empty)
            : WeaponHitContext.None;

        LureBeaconDecoy beacon = targetView.GetComponent<LureBeaconDecoy>();
        if (beacon != null)
        {
            if (beacon.CanBeTargeted)
                beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageProfileAt), RpcTarget.MasterClient, shieldDamage, hpDamage, attackerViewId, impact.x, impact.y);
            return;
        }

        PlayerDeployableBase deployable = targetView.GetComponent<PlayerDeployableBase>();
        if (deployable != null)
        {
            if (deployable.CanBeTargeted)
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
            return;
        }

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        if (targetHealth != null && targetHealth != health && !targetHealth.IsWreck && !targetHealth.IsBotControlled && ActorIdentity.CanBeTargetedByMonstersActor(targetHealth) && !targetHealth.IsEvacuationAnimating)
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

    void ApplyTetherPullTick()
    {
        PhotonView targetView = activeTargetViewId > 0 ? PhotonView.Find(activeTargetViewId) : null;
        if (targetView == null || targetView.Owner == null)
            return;

        if (targetView.GetComponent<LureBeaconDecoy>() != null || targetView.GetComponent<PlayerDeployableBase>() != null)
            return;

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        if (targetHealth == null || targetHealth == health || targetHealth.IsWreck || targetHealth.IsBotControlled || !ActorIdentity.CanBeTargetedByMonstersActor(targetHealth) || targetHealth.IsEvacuationAnimating)
            return;

        int sourceViewId = bot.photonView != null ? bot.photonView.ViewID : 0;
        targetView.RPC(
            nameof(PlayerHealth.ApplyGravityTetherPullRpc),
            targetView.Owner,
            sourceViewId,
            rb.position.x,
            rb.position.y,
            PullAcceleration,
            PullMaxSpeed,
            PullEffectDuration);
    }

    Vector2 ResolveTargetImpactPoint(Transform target)
    {
        if (target == null)
            return rb.position;

        Collider2D collider = target.GetComponent<Collider2D>();
        if (collider != null)
            return collider.ClosestPoint(rb.position);

        return target.position;
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

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.2f, bot.EffectiveMoveSpeed * 1.9f);
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

        return (desired * 0.36f + inward.normalized * 0.72f).normalized;
    }

    float ResolveTetherRange()
    {
        return weapon != null && weapon.Range > 0f ? weapon.Range : Mathf.Max(8f, movement.ShootDistance);
    }

    float ResolveTetherCooldown()
    {
        return ScaleEnemyAttackCooldown(weapon != null && weapon.FireRate > 0f ? weapon.FireRate : 7f);
    }

    void StopActiveTetherVfx()
    {
        StopActiveTetherVfx(true);
    }

    void StopActiveTetherVfx(bool broadcastToRoom)
    {
        if (view == null || activeTargetViewId <= 0 || bot == null || bot.photonView == null)
            return;

        int sourceViewId = bot.photonView.ViewID;
        GravitySquidTetherVfx.StopBeam(sourceViewId);

        if (!broadcastToRoom || !view.IsMine || !PhotonNetwork.InRoom || !gameObject.activeInHierarchy)
            return;

        bot.photonView.RPC(nameof(EnemyBot.StopGravitySquidTetherRpc), RpcTarget.Others);
    }

    void SetAnimationSpeed(float multiplier)
    {
        if (frameAnimator == null)
            frameAnimator = GetComponent<EnemySpriteFrameAnimator>();

        if (frameAnimator != null)
            frameAnimator.SetSpeedMultiplier(multiplier);
    }

    void RotateToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }
}

