using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemySpaceMantaBehavior : EnemyBotBehaviorBase
{
    enum MantaMode
    {
        Patrol,
        Stalk,
        ChargeWindup,
        Dash,
        Recovery
    }

    const float ChargeWindupDuration = 1.025f;
    const float DashDuration = 0.58f;
    const float RecoveryDuration = 0.68f;
    const float DashSpeedMultiplier = 5.9f;
    const float DashHitRadiusFactor = 0.42f;
    const float AvoidanceScanRadius = 2.65f;
    const float AvoidanceWeight = 0.58f;
    const float MapEdgeMargin = 2.4f;
    const float PatrolTurnIntervalMin = 1.2f;
    const float PatrolTurnIntervalMax = 2.4f;

    readonly HashSet<int> damagedThisDash = new HashSet<int>();

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    EnemySpriteFrameAnimator frameAnimator;
    Transform currentTarget;
    Vector2 patrolDirection = Vector2.up;
    Vector2 chargeDirection = Vector2.up;
    float nextTargetRefreshTime;
    float nextRepathTime;
    float nextPatrolTurnTime;
    float nextChargeTime;
    float modeStartedAt;
    float orbitDirection = 1f;
    MantaMode mode = MantaMode.Patrol;

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
        float angle = Mathf.Abs(seed * 0.193f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        nextChargeTime = Time.time + Random.Range(0.65f, 1.45f);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        if (frameAnimator == null)
            frameAnimator = GetComponent<EnemySpriteFrameAnimator>();

        RefreshTargetIfNeeded();

        switch (mode)
        {
            case MantaMode.ChargeWindup:
                TickChargeWindup();
                break;
            case MantaMode.Dash:
                TickDash();
                break;
            case MantaMode.Recovery:
                TickRecovery();
                break;
            default:
                TickPatrolOrStalk();
                break;
        }
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
        nextChargeTime = Mathf.Min(nextChargeTime, Time.time + 0.25f);
        if (mode == MantaMode.Patrol)
            mode = MantaMode.Stalk;
    }

    void TickPatrolOrStalk()
    {
        SetAnimationSpeed(1f);

        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            float distance = toTarget.magnitude;
            if (distance > 0.001f && distance <= ResolveChargeRange() && Time.time >= nextChargeTime)
            {
                BeginChargeWindup(toTarget / distance);
                return;
            }

            mode = MantaMode.Stalk;
        }
        else
        {
            mode = MantaMode.Patrol;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + Mathf.Max(0.12f, movement.RepathInterval);
            patrolDirection = currentTarget != null ? ResolveStalkDirection() : ResolvePatrolDirection();
        }

        Vector2 desired = ApplyAvoidance(ApplyMapEdgeSteering(patrolDirection));
        if (desired.sqrMagnitude <= 0.001f)
            desired = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : Vector2.up;

        float speedMultiplier = currentTarget != null ? 1f : 0.54f;
        Vector2 desiredVelocity = desired.normalized * (bot.EffectiveMoveSpeed * speedMultiplier);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, currentTarget != null ? 0.17f : 0.1f);

        Vector2 lookDirection = currentTarget != null
            ? (Vector2)currentTarget.position - rb.position
            : rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : desired;
        RotateNoseToward(lookDirection);
    }

    void BeginChargeWindup(Vector2 direction)
    {
        chargeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.up;
        mode = MantaMode.ChargeWindup;
        modeStartedAt = Time.time;
        rb.linearVelocity *= 0.28f;
        SetAnimationSpeed(1.75f);

        if (bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.PlaySpaceMantaWarningRpc), RpcTarget.All, rb.position.x, rb.position.y, transform.position.z);
    }

    void TickChargeWindup()
    {
        SetAnimationSpeed(1.8f);
        RefreshChargeDirection();
        RotateNoseToward(chargeDirection);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, -chargeDirection * (bot.EffectiveMoveSpeed * 0.18f), 0.2f);

        if (Time.time - modeStartedAt >= ScaleEnemyAttackWindup(ChargeWindupDuration))
            BeginDash();
    }

    void BeginDash()
    {
        mode = MantaMode.Dash;
        modeStartedAt = Time.time;
        damagedThisDash.Clear();
        SetAnimationSpeed(2.55f);
        rb.linearVelocity = chargeDirection * ResolveDashSpeed();
    }

    void TickDash()
    {
        SetAnimationSpeed(2.55f);
        RotateNoseToward(chargeDirection);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, chargeDirection * ResolveDashSpeed(), 0.36f);

        if (TryApplyDashDamage() || Time.time - modeStartedAt >= DashDuration)
            BeginRecovery();
    }

    void BeginRecovery()
    {
        mode = MantaMode.Recovery;
        modeStartedAt = Time.time;
        nextChargeTime = Time.time + ResolveChargeCooldown();
        SetAnimationSpeed(0.72f);
        rb.linearVelocity *= 0.42f;
    }

    void TickRecovery()
    {
        SetAnimationSpeed(0.72f);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, 0.08f);
        RotateNoseToward(rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : chargeDirection);

        if (Time.time - modeStartedAt >= RecoveryDuration)
            mode = currentTarget != null ? MantaMode.Stalk : MantaMode.Patrol;
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
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.58f, 0.58f);
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
        float wave = Mathf.Sin(Time.time * 1.35f + (view != null ? view.ViewID : 1) * 0.17f) * 0.16f;

        if (distance > movement.PreferredDistance)
            return (toward * 0.82f + tangent * (0.26f + wave)).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.5f + tangent * 0.78f).normalized;

        return (tangent * 0.88f + toward * 0.18f).normalized;
    }

    void RefreshChargeDirection()
    {
        if (currentTarget == null)
            return;

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        if (toTarget.sqrMagnitude <= 0.001f)
            return;

        chargeDirection = Vector2.Lerp(chargeDirection, toTarget.normalized, 0.18f).normalized;
    }

    bool TryApplyDashDamage()
    {
        float hitRadius = Mathf.Max(0.55f, bot.VisualTargetSize * DashHitRadiusFactor);
        Vector2 hitCenter = rb.position + chargeDirection * Mathf.Max(0.18f, hitRadius * 0.35f);
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(hitCenter, hitRadius, out Collider2D[] hits);
        bool appliedHit = false;
        int attackerViewId = bot.photonView != null ? bot.photonView.ViewID : 0;
        int damage = RoomSettings.GetEnemyDamage(bot.Kind);
        WeaponHitContext hitContext = weapon != null
            ? new WeaponHitContext(weapon.DamageType, weapon.DeliveryMethod, weapon.DeliveryFlags, string.Empty)
            : WeaponHitContext.None;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            PlayerHealth candidate = hit.GetComponentInParent<PlayerHealth>();
            PhotonView targetView = candidate != null ? candidate.GetComponent<PhotonView>() : null;
            if (candidate == null || targetView == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating)
                continue;

            if (!ActorIdentity.CanBeTargetedByMonstersActor(candidate) || candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            if (!damagedThisDash.Add(targetView.ViewID))
                continue;

            targetView.RPC(
                nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                RpcTarget.MasterClient,
                damage,
                damage,
                attackerViewId,
                hitCenter.x,
                hitCenter.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
            appliedHit = true;
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted || beacon.photonView == null)
                continue;

            if (Vector2.Distance(hitCenter, beacon.transform.position) > hitRadius)
                continue;

            if (!damagedThisDash.Add(beacon.photonView.ViewID))
                continue;

            beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageProfileAt), RpcTarget.MasterClient, damage, damage, attackerViewId, hitCenter.x, hitCenter.y);
            appliedHit = true;
        }

        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (deployable == null || !deployable.CanBeTargeted || deployable.photonView == null)
                continue;

            if (Vector2.Distance(hitCenter, deployable.transform.position) > hitRadius)
                continue;

            if (!damagedThisDash.Add(deployable.photonView.ViewID))
                continue;

            deployable.photonView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                damage,
                damage,
                attackerViewId,
                hitCenter.x,
                hitCenter.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
            appliedHit = true;
        }

        return appliedHit;
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
        Vector2 predicted = rb.position + desired * Mathf.Max(1.2f, bot.EffectiveMoveSpeed * 1.8f);
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

        return (desired * 0.38f + inward.normalized * 0.78f).normalized;
    }

    float ResolveChargeRange()
    {
        return weapon != null && weapon.Range > 0f ? weapon.Range : Mathf.Max(6f, movement.ShootDistance);
    }

    float ResolveChargeCooldown()
    {
        return ScaleEnemyAttackCooldown(weapon != null && weapon.FireRate > 0f ? weapon.FireRate : 3.4f);
    }

    float ResolveDashSpeed()
    {
        return Mathf.Max(bot.EffectiveMoveSpeed * DashSpeedMultiplier, bot.EffectiveMoveSpeed + 5.2f);
    }

    void SetAnimationSpeed(float multiplier)
    {
        if (frameAnimator == null)
            frameAnimator = GetComponent<EnemySpriteFrameAnimator>();

        if (frameAnimator != null)
            frameAnimator.SetSpeedMultiplier(multiplier);
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

