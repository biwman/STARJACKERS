using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public sealed class EnemyCosmicWormBehavior : EnemyBotBehaviorBase
{
    enum WormMode
    {
        Patrol,
        Stalk,
        SpitWindup,
        DashWindup,
        Dash,
        SwallowWindup,
        SwallowChannel,
        Recovery
    }

    const float PhaseTwoHpFraction = 0.66f;
    const float PhaseThreeHpFraction = 0.33f;
    const float SpitWindupDuration = 0.62f;
    const float DashWindupDuration = 0.9f;
    const float DashDuration = 0.76f;
    const float SwallowWindupDuration = 0.58f;
    const float SwallowChannelDuration = 2.45f;
    const float RecoveryDuration = 0.58f;
    const float PullTickInterval = 0.12f;
    const float BiteTickInterval = 0.5f;
    const float AvoidanceScanRadius = 3.4f;
    const float AvoidanceWeight = 0.38f;
    const float MapEdgeMargin = 4.6f;
    const float PatrolTurnIntervalMin = 1.3f;
    const float PatrolTurnIntervalMax = 2.7f;
    const float SwallowConeDegrees = 118f;

    readonly HashSet<int> damagedThisDash = new HashSet<int>();
    readonly HashSet<int> pulseDamageTargets = new HashSet<int>();

    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    Transform currentTarget;
    Vector2 patrolDirection = Vector2.right;
    Vector2 moveDirection = Vector2.right;
    Vector2 attackDirection = Vector2.right;
    float nextTargetRefreshTime;
    float nextRepathTime;
    float nextPatrolTurnTime;
    float nextAttackTime;
    float nextPullTickTime;
    float nextBiteTickTime;
    float nextDashTrailTime;
    float modeStartedAt;
    float orbitDirection = 1f;
    int phase = 1;
    WormMode mode = WormMode.Patrol;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        ConfigureWeapon(owner);
        ConfigureSeededMotion();
        phase = ResolvePhase();
        CosmicWormVisualController.AttachOrUpdate(owner, phase, true);
        nextAttackTime = Time.time + Random.Range(1.15f, 2.35f);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        UpdatePhase();
        RefreshTargetIfNeeded();
        CosmicWormVisualController.AttachOrUpdate(bot, phase, false);

        switch (mode)
        {
            case WormMode.SpitWindup:
                TickSpitWindup();
                break;
            case WormMode.DashWindup:
                TickDashWindup();
                break;
            case WormMode.Dash:
                TickDash();
                break;
            case WormMode.SwallowWindup:
                TickSwallowWindup();
                break;
            case WormMode.SwallowChannel:
                TickSwallowChannel();
                break;
            case WormMode.Recovery:
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
        if (attackerHealth != null && (attackerHealth.IsBotControlled || attackerHealth.IsAstronautControlled || attackerHealth.IsWreck))
            return;

        currentTarget = attackerView.transform;
        nextTargetRefreshTime = Time.time + 0.35f;
        nextAttackTime = Mathf.Min(nextAttackTime, Time.time + 0.45f);
        if (mode == WormMode.Patrol)
            mode = WormMode.Stalk;
    }

    void ConfigureWeapon(EnemyBot owner)
    {
        if (shooting == null || weapon == null)
            return;

        shooting.ConfigureWeaponProfile(
            weapon.FireRate,
            Mathf.Max(weapon.AmmoCount, 1),
            weapon.ReloadDuration,
            RoomSettings.GetEnemyDamage(owner.Kind),
            weapon.BulletScaleMultiplier,
            weapon.BulletColor,
            weapon.MuzzleOffsetDistance,
            true,
            weapon.BulletSpeed,
            weapon.ShotSoundId,
            weapon.Range,
            "cosmic_worm",
            7.5f,
            weapon.DamageType,
            weapon.DeliveryMethod,
            weapon.DeliveryFlags);
    }

    void ConfigureSeededMotion()
    {
        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.227f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        if (patrolDirection.sqrMagnitude <= 0.001f)
            patrolDirection = Vector2.right;

        moveDirection = patrolDirection;
        attackDirection = patrolDirection;
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
    }

    void TickPatrolOrStalk()
    {
        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            float distance = toTarget.magnitude;
            if (distance > 0.001f && distance <= ResolveAttackRange() && Time.time >= nextAttackTime)
            {
                BeginSelectedAttack(toTarget / distance, distance);
                return;
            }

            mode = WormMode.Stalk;
        }
        else
        {
            mode = WormMode.Patrol;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + Mathf.Max(0.12f, movement.RepathInterval);
            moveDirection = currentTarget != null ? ResolveStalkDirection() : ResolvePatrolDirection();
        }

        Vector2 desired = ApplyAvoidance(ApplyMapEdgeSteering(moveDirection));
        if (desired.sqrMagnitude <= 0.001f)
            desired = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection;

        float speedMultiplier = currentTarget != null ? ResolvePhaseMoveMultiplier() : 0.56f;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desired.normalized * bot.EffectiveMoveSpeed * speedMultiplier, currentTarget != null ? 0.16f : 0.09f);

        Vector2 lookDirection = currentTarget != null
            ? (Vector2)currentTarget.position - rb.position
            : rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : desired;
        RotateHeadToward(lookDirection);
    }

    void BeginSelectedAttack(Vector2 direction, float distance)
    {
        attackDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : ResolveForward();
        float roll = Random.value;

        if (phase >= 3 && distance <= ResolveSwallowRange() && roll < 0.34f)
        {
            BeginSwallowWindup();
            return;
        }

        if (phase >= 2 && distance <= ResolveDashRange() && roll < (phase >= 3 ? 0.72f : 0.42f))
        {
            BeginDashWindup();
            return;
        }

        BeginSpitWindup();
    }

    void BeginSpitWindup()
    {
        mode = WormMode.SpitWindup;
        modeStartedAt = Time.time;
        rb.linearVelocity *= 0.42f;
    }

    void TickSpitWindup()
    {
        RotateHeadToward(attackDirection);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, -attackDirection * bot.EffectiveMoveSpeed * 0.22f, 0.08f);

        if (Time.time - modeStartedAt < ResolveSpitWindupDuration())
            return;

        FireSpitVolley();
        BeginRecovery(ResolveSpitCooldown());
    }

    void FireSpitVolley()
    {
        if (shooting == null)
            return;

        Vector2 mouth = GetMouthPosition();
        int shotCount = phase >= 3 ? 7 : phase >= 2 ? 5 : 3;
        float spread = phase >= 3 ? 36f : phase >= 2 ? 25f : 16f;
        float step = shotCount > 1 ? spread / (shotCount - 1) : 0f;
        Vector2 side = new Vector2(-attackDirection.y, attackDirection.x);

        for (int i = 0; i < shotCount; i++)
        {
            float angle = -spread * 0.5f + step * i;
            Vector2 shotDirection = RotateVector(attackDirection, angle);
            Vector3 spawnPosition = mouth + side * Mathf.Lerp(-0.26f, 0.26f, shotCount <= 1 ? 0.5f : (float)i / (shotCount - 1));
            shooting.FireBotProjectileFromWorld(shotDirection, spawnPosition);
        }

        if (bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.SpawnCosmicWormSpitVfxRpc), RpcTarget.All, mouth.x, mouth.y, attackDirection.x, attackDirection.y, phase);
    }

    void BeginDashWindup()
    {
        mode = WormMode.DashWindup;
        modeStartedAt = Time.time;
        damagedThisDash.Clear();
        rb.linearVelocity *= 0.18f;

        Vector2 mouth = GetMouthPosition();
        if (bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.SpawnCosmicWormDashWarningRpc), RpcTarget.All, mouth.x, mouth.y, attackDirection.x, attackDirection.y, ResolveDashRange(), ResolveDashWindupDuration());
    }

    void TickDashWindup()
    {
        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            if (toTarget.sqrMagnitude > 0.001f)
                attackDirection = Vector2.Lerp(attackDirection, toTarget.normalized, 0.12f).normalized;
        }

        RotateHeadToward(attackDirection);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, 0.18f);

        if (Time.time - modeStartedAt < ResolveDashWindupDuration())
            return;

        mode = WormMode.Dash;
        modeStartedAt = Time.time;
        nextDashTrailTime = 0f;
        rb.linearVelocity = attackDirection * ResolveDashSpeed();
    }

    void TickDash()
    {
        RotateHeadToward(attackDirection);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, attackDirection * ResolveDashSpeed(), 0.42f);

        if (Time.time >= nextDashTrailTime)
        {
            nextDashTrailTime = Time.time + (MobilePerformanceSettings.UseReducedVfx ? 0.17f : 0.09f);
            Vector2 mouth = GetMouthPosition();
            if (bot.photonView != null)
                bot.photonView.RPC(nameof(EnemyBot.SpawnCosmicWormDashTrailRpc), RpcTarget.All, mouth.x, mouth.y, attackDirection.x, attackDirection.y, bot.VisualTargetSize * 0.28f);
        }

        ApplyDashDamage();

        if (Time.time - modeStartedAt >= DashDuration)
            BeginRecovery(ResolveDashCooldown());
    }

    void BeginSwallowWindup()
    {
        mode = WormMode.SwallowWindup;
        modeStartedAt = Time.time;
        rb.linearVelocity *= 0.28f;
    }

    void TickSwallowWindup()
    {
        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            if (toTarget.sqrMagnitude > 0.001f)
                attackDirection = Vector2.Lerp(attackDirection, toTarget.normalized, 0.1f).normalized;
        }

        RotateHeadToward(attackDirection);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, -attackDirection * bot.EffectiveMoveSpeed * 0.12f, 0.08f);

        if (Time.time - modeStartedAt < ResolveSwallowWindupDuration())
            return;

        BeginSwallowChannel();
    }

    void BeginSwallowChannel()
    {
        mode = WormMode.SwallowChannel;
        modeStartedAt = Time.time;
        nextPullTickTime = 0f;
        nextBiteTickTime = 0f;
        Vector2 mouth = GetMouthPosition();

        if (bot.photonView != null)
        {
            int sourceViewId = bot.photonView.ViewID;
            bot.photonView.RPC(
                nameof(EnemyBot.StartCosmicWormSwallowRpc),
                RpcTarget.All,
                mouth.x,
                mouth.y,
                attackDirection.x,
                attackDirection.y,
                ResolveSwallowRange(),
                SwallowChannelDuration,
                sourceViewId);
        }
    }

    void TickSwallowChannel()
    {
        RotateHeadToward(attackDirection);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, attackDirection * bot.EffectiveMoveSpeed * 0.18f, 0.06f);
        Vector2 mouth = GetMouthPosition();

        if (Time.time >= nextPullTickTime)
        {
            nextPullTickTime = Time.time + PullTickInterval;
            ApplySwallowPull(mouth);
        }

        if (Time.time >= nextBiteTickTime)
        {
            nextBiteTickTime = Time.time + BiteTickInterval;
            ApplySwallowBiteDamage(mouth);
        }

        if (Time.time - modeStartedAt >= SwallowChannelDuration)
        {
            StopSwallowVfx();
            BeginRecovery(ResolveSwallowCooldown());
        }
    }

    void TickRecovery()
    {
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, 0.1f);
        RotateHeadToward(rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : attackDirection);

        if (Time.time - modeStartedAt >= RecoveryDuration)
            mode = currentTarget != null ? WormMode.Stalk : WormMode.Patrol;
    }

    void BeginRecovery(float attackCooldown)
    {
        StopSwallowVfx();
        mode = WormMode.Recovery;
        modeStartedAt = Time.time;
        nextAttackTime = Time.time + Mathf.Max(0.25f, attackCooldown);
        rb.linearVelocity *= 0.48f;
    }

    void StopSwallowVfx()
    {
        if (bot != null && bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.StopCosmicWormSwallowRpc), RpcTarget.All, bot.photonView.ViewID);
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
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.48f, 0.48f);
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.right;
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
        float wave = Mathf.Sin(Time.time * (1.1f + phase * 0.16f) + (view != null ? view.ViewID : 1) * 0.17f) * 0.18f;

        if (distance > movement.PreferredDistance)
            return (toward * 0.78f + tangent * (0.3f + wave)).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.56f + tangent * 0.76f).normalized;

        return (tangent * 0.88f + toward * 0.14f).normalized;
    }

    Vector2 ApplyAvoidance(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.right;
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
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.right;
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.5f, bot.EffectiveMoveSpeed * 2.0f);
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

        return (desired * 0.4f + inward.normalized * 0.78f).normalized;
    }

    void ApplyDashDamage()
    {
        float hitRadius = Mathf.Max(1.2f, bot.VisualTargetSize * 0.24f);
        Vector2 hitCenter = GetMouthPosition() + attackDirection * Mathf.Max(0.2f, hitRadius * 0.2f);
        int damage = Mathf.Max(1, Mathf.RoundToInt(RoomSettings.GetEnemyDamage(bot.Kind) * (phase >= 3 ? 1.65f : 1.35f)));
        ApplyDamageInRadius(hitCenter, hitRadius, damage, damagedThisDash, true);
    }

    void ApplySwallowPull(Vector2 mouth)
    {
        float radius = ResolveSwallowRange();
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(mouth + attackDirection * radius * 0.46f, radius * 0.62f, out Collider2D[] hits);
        int sourceViewId = bot.photonView != null ? bot.photonView.ViewID : 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            Vector2 targetPosition = hit.attachedRigidbody != null ? hit.attachedRigidbody.position : (Vector2)hit.transform.position;
            if (!IsPointInsideSwallowCone(mouth, targetPosition, radius))
                continue;

            PlayerHealth candidate = hit.GetComponentInParent<PlayerHealth>();
            PhotonView targetView = candidate != null ? candidate.GetComponent<PhotonView>() : null;
            if (candidate != null && targetView != null && targetView.Owner != null && IsValidPlayerVictim(candidate))
            {
                targetView.RPC(
                    nameof(PlayerHealth.ApplyGravityTetherPullRpc),
                    targetView.Owner,
                    sourceViewId,
                    mouth.x,
                    mouth.y,
                    23f + phase * 4f,
                    7.2f + phase * 0.8f,
                    0.28f);
                continue;
            }

            MovingSpaceObject movingObject = hit.GetComponentInParent<MovingSpaceObject>();
            if (movingObject != null)
                movingObject.ApplyMagneticPull(mouth, 14f + phase * 3f, Time.fixedDeltaTime);
        }
    }

    void ApplySwallowBiteDamage(Vector2 mouth)
    {
        pulseDamageTargets.Clear();
        int damage = Mathf.Max(1, Mathf.RoundToInt(RoomSettings.GetEnemyDamage(bot.Kind) * 0.62f));
        ApplyDamageInRadius(mouth, Mathf.Max(1.6f, bot.VisualTargetSize * 0.2f), damage, pulseDamageTargets, true);
    }

    void ApplyDamageInRadius(Vector2 center, float radius, int damage, HashSet<int> processedViewIds, bool includeUtilities)
    {
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(center, radius, out Collider2D[] hits);
        int attackerViewId = bot.photonView != null ? bot.photonView.ViewID : 0;
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
            if (candidate == null || targetView == null || !IsValidPlayerVictim(candidate))
                continue;

            if (!processedViewIds.Add(targetView.ViewID))
                continue;

            Vector2 impact = ResolveImpactPoint(hit, center);
            targetView.RPC(
                nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                RpcTarget.MasterClient,
                damage,
                damage,
                attackerViewId,
                impact.x,
                impact.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }

        if (!includeUtilities)
            return;

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted || beacon.photonView == null)
                continue;

            if (Vector2.Distance(center, beacon.transform.position) > radius)
                continue;

            if (!processedViewIds.Add(beacon.photonView.ViewID))
                continue;

            beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageProfileAt), RpcTarget.MasterClient, damage, damage, attackerViewId, center.x, center.y);
        }

        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (deployable == null || !deployable.CanBeTargeted || deployable.photonView == null)
                continue;

            if (Vector2.Distance(center, deployable.transform.position) > radius)
                continue;

            if (!processedViewIds.Add(deployable.photonView.ViewID))
                continue;

            deployable.photonView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                damage,
                damage,
                attackerViewId,
                center.x,
                center.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }
    }

    bool IsValidPlayerVictim(PlayerHealth candidate)
    {
        if (candidate == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating)
            return false;

        if (!ActorIdentity.CanBeTargetedByMonstersActor(candidate))
            return false;

        return candidate.GetComponent<LureBeaconDecoy>() == null && candidate.GetComponent<PlayerDeployableBase>() == null;
    }

    bool IsPointInsideSwallowCone(Vector2 mouth, Vector2 point, float range)
    {
        Vector2 toPoint = point - mouth;
        float distance = toPoint.magnitude;
        if (distance > range || distance <= 0.05f)
            return distance <= Mathf.Max(1.2f, bot.VisualTargetSize * 0.18f);

        float halfConeCos = Mathf.Cos(SwallowConeDegrees * 0.5f * Mathf.Deg2Rad);
        return Vector2.Dot(attackDirection.normalized, toPoint / distance) >= halfConeCos;
    }

    Vector2 ResolveImpactPoint(Collider2D hit, Vector2 center)
    {
        if (hit == null)
            return center;

        return hit.ClosestPoint(center);
    }

    void UpdatePhase()
    {
        int newPhase = ResolvePhase();
        if (newPhase == phase)
            return;

        phase = newPhase;
        nextAttackTime = Mathf.Min(nextAttackTime, Time.time + 0.35f);
        if (bot.photonView != null)
        {
            float radius = Mathf.Max(2.2f, bot.VisualTargetSize * (0.34f + phase * 0.08f));
            bot.photonView.RPC(nameof(EnemyBot.PlayCosmicWormPhaseRpc), RpcTarget.All, phase, rb.position.x, rb.position.y, transform.position.z, radius);
        }
    }

    int ResolvePhase()
    {
        if (health == null || health.maxHP <= 0)
            return 1;

        float hpFraction = Mathf.Clamp01((float)health.CurrentHP / Mathf.Max(1, health.maxHP));
        if (hpFraction <= PhaseThreeHpFraction)
            return 3;

        if (hpFraction <= PhaseTwoHpFraction)
            return 2;

        return 1;
    }

    Vector2 GetMouthPosition()
    {
        return rb.position + ResolveForward() * Mathf.Max(0.72f, bot.VisualTargetSize * 0.43f);
    }

    Vector2 ResolveForward()
    {
        Vector2 forward = transform.right;
        if (forward.sqrMagnitude <= 0.001f)
            forward = attackDirection.sqrMagnitude > 0.001f ? attackDirection.normalized : Vector2.right;

        return forward.normalized;
    }

    void RotateHeadToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime * ResolvePhaseTurnMultiplier());
        rb.MoveRotation(nextAngle);
    }

    float ResolvePhaseMoveMultiplier()
    {
        return phase >= 3 ? 1.34f : phase >= 2 ? 1.16f : 1f;
    }

    float ResolvePhaseTurnMultiplier()
    {
        return phase >= 3 ? 1.25f : phase >= 2 ? 1.1f : 1f;
    }

    float ResolveAttackRange()
    {
        float weaponRange = weapon != null && weapon.Range > 0f ? weapon.Range : movement.ShootDistance;
        return Mathf.Max(weaponRange, ResolveDashRange());
    }

    float ResolveDashRange()
    {
        return Mathf.Max(11.5f, bot.VisualTargetSize * 1.42f + phase * 1.4f);
    }

    float ResolveSwallowRange()
    {
        return Mathf.Max(8.5f, bot.VisualTargetSize * 0.84f + phase * 0.75f);
    }

    float ResolveDashSpeed()
    {
        return Mathf.Max(bot.EffectiveMoveSpeed * (8.2f + phase * 0.85f), bot.EffectiveMoveSpeed + 7.8f + phase * 1.2f);
    }

    float ResolveSpitCooldown()
    {
        return ScaleEnemyAttackCooldown(phase >= 3 ? 1.55f : phase >= 2 ? 1.85f : 2.25f);
    }

    float ResolveDashCooldown()
    {
        return ScaleEnemyAttackCooldown(phase >= 3 ? 2.65f : 3.25f);
    }

    float ResolveSwallowCooldown()
    {
        return ScaleEnemyAttackCooldown(phase >= 3 ? 4.4f : 5.5f);
    }

    float ResolveSpitWindupDuration()
    {
        return ScaleEnemyAttackWindup(SpitWindupDuration);
    }

    float ResolveDashWindupDuration()
    {
        return ScaleEnemyAttackWindup(DashWindupDuration);
    }

    float ResolveSwallowWindupDuration()
    {
        return ScaleEnemyAttackWindup(SwallowWindupDuration);
    }

    static Vector2 RotateVector(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos).normalized;
    }
}

public sealed class CosmicWormVisualController : MonoBehaviour
{
    const int MaxSpinePointCount = 20;
    const int MobileSpinePointCount = 14;
    const int MaxSegmentBandCount = 11;
    const int MobileSegmentBandCount = 6;
    const int MaxMawTeethCount = 14;
    const int MobileMawTeethCount = 8;
    const int SortingOrder = 7270;

    static int SpinePointCount => MobilePerformanceSettings.UseReducedVfx ? MobileSpinePointCount : MaxSpinePointCount;
    static int SegmentBandCount => MobilePerformanceSettings.UseReducedVfx ? MobileSegmentBandCount : MaxSegmentBandCount;
    static int MawTeethCount => MobilePerformanceSettings.UseReducedVfx ? MobileMawTeethCount : MaxMawTeethCount;

    static Sprite mawSealSprite;
    static Sprite jawLidSprite;
    static Sprite mawVoidSprite;

    EnemyBot bot;
    PlayerHealth health;
    SpriteRenderer spriteRenderer;
    SpriteRenderer mawSealRenderer;
    SpriteRenderer upperJawRenderer;
    SpriteRenderer lowerJawRenderer;
    SpriteRenderer mawVoidRenderer;
    LineRenderer spineGlow;
    LineRenderer mawRing;
    LineRenderer[] segmentBands;
    LineRenderer[] mawTeeth;
    int phase = 1;
    bool shutdown;
    float nextVisualRefreshTime;

    public static void AttachOrUpdate(EnemyBot owner, int phase, bool immediate)
    {
        if (owner == null)
            return;

        CosmicWormVisualController controller = owner.GetComponent<CosmicWormVisualController>();
        if (controller == null)
            controller = owner.gameObject.AddComponent<CosmicWormVisualController>();

        controller.Configure(owner, phase, immediate);
    }

    public static void StopFor(EnemyBot owner)
    {
        if (owner == null)
            return;

        CosmicWormVisualController controller = owner.GetComponent<CosmicWormVisualController>();
        if (controller != null)
            controller.ShutdownForWreck();
    }

    void Configure(EnemyBot owner, int newPhase, bool immediate)
    {
        int clampedPhase = Mathf.Clamp(newPhase, 1, 3);
        bool ownerChanged = bot != owner;
        bool phaseChanged = phase != clampedPhase;

        bot = owner;
        health = owner.GetComponent<PlayerHealth>();
        phase = clampedPhase;
        if (spriteRenderer == null || ownerChanged)
            spriteRenderer = owner.GetComponent<SpriteRenderer>();

        if (ownerChanged || phaseChanged || !HasVisualObjectsForCurrentTier())
            EnsureVisualObjects();
        if (immediate)
            RefreshVisuals();
    }

    void LateUpdate()
    {
        if (shutdown)
            return;

        if (health != null && health.IsWreck)
        {
            ShutdownForWreck();
            return;
        }

        if (MobilePerformanceSettings.UseReducedVfx)
        {
            if (Time.unscaledTime < nextVisualRefreshTime)
                return;

            nextVisualRefreshTime = Time.unscaledTime + MobilePerformanceSettings.ReducedVfxFrameInterval;
        }

        RefreshVisuals();
    }

    void OnDisable()
    {
        RestoreRenderer();
        HideAllVfx();
    }

    void OnDestroy()
    {
        RestoreRenderer();
    }

    void ShutdownForWreck()
    {
        if (shutdown)
            return;

        shutdown = true;
        RestoreRenderer();
        DestroyVisualObject(spineGlow);
        DestroyVisualObject(mawRing);
        DestroyVisualObjects(segmentBands);
        DestroyVisualObjects(mawTeeth);
        DestroyRendererObject(mawSealRenderer);
        DestroyRendererObject(upperJawRenderer);
        DestroyRendererObject(lowerJawRenderer);
        DestroyRendererObject(mawVoidRenderer);
        enabled = false;
    }

    void RestoreRenderer()
    {
        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;
    }

    void RefreshVisuals()
    {
        if (bot == null || spriteRenderer == null)
            return;

        int visualSeed = bot.photonView != null ? bot.photonView.ViewID : 1;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (2.2f + phase * 0.65f) + visualSeed * 0.017f);
        Color phaseColor = CosmicWormVfxUtility.PhaseColor(phase, 1f);
        float tint = phase <= 1
            ? Mathf.Lerp(0.06f, 0.13f, pulse)
            : phase == 2
                ? Mathf.Lerp(0.18f, 0.34f, pulse)
                : Mathf.Lerp(0.26f, 0.48f, pulse);
        spriteRenderer.color = Color.Lerp(Color.white, phaseColor, tint);

        Bounds bounds = spriteRenderer.bounds;
        Vector3 center = bounds.center;
        Vector3 forward = transform.right.sqrMagnitude > 0.001f ? transform.right.normalized : Vector3.right;
        Vector3 side = transform.up.sqrMagnitude > 0.001f ? transform.up.normalized : Vector3.up;
        float length = Mathf.Max(0.5f, bounds.size.x);
        float height = Mathf.Max(0.35f, bounds.size.y);
        Vector3 mouth = center + forward * (length * 0.405f);

        UpdateMawSeal(mouth, length, height, phaseColor);
        UpdateMawAnimation(mouth, forward, side, length, height, phaseColor, visualSeed);
        UpdateSpineGlow(center, forward, side, length, height, pulse, phaseColor);
        UpdateSegmentBands(center, forward, side, length, height, phaseColor, visualSeed);
    }

    void EnsureVisualObjects()
    {
        if (spineGlow == null)
        {
            spineGlow = CosmicWormVfxUtility.CreateLine(transform, "CosmicWormSpineGlow", SortingOrder, 0.09f, false);
            spineGlow.positionCount = SpinePointCount;
        }
        else if (spineGlow.positionCount != SpinePointCount)
        {
            spineGlow.positionCount = SpinePointCount;
        }

        if (segmentBands == null || segmentBands.Length != SegmentBandCount)
        {
            DestroyVisualObjects(segmentBands);
            segmentBands = new LineRenderer[SegmentBandCount];
            for (int i = 0; i < segmentBands.Length; i++)
                segmentBands[i] = CosmicWormVfxUtility.CreateLine(transform, "CosmicWormSegmentBand_" + i, SortingOrder + 1 + i, 0.035f, false);
        }

        if (mawRing == null)
            mawRing = CosmicWormVfxUtility.CreateLine(transform, "CosmicWormMawRing", SortingOrder + 38, 0.055f, true);

        if (mawTeeth == null || mawTeeth.Length != MawTeethCount)
        {
            DestroyVisualObjects(mawTeeth);
            mawTeeth = new LineRenderer[MawTeethCount];
            for (int i = 0; i < mawTeeth.Length; i++)
                mawTeeth[i] = CosmicWormVfxUtility.CreateLine(transform, "CosmicWormMawTooth_" + i, SortingOrder + 39 + i, 0.026f, false);
        }

        if (mawSealRenderer == null)
            mawSealRenderer = CreateChildRenderer("CosmicWormClosedMawSeal", GetMawSealSprite(), SortingOrder + 34);
        if (upperJawRenderer == null)
            upperJawRenderer = CreateChildRenderer("CosmicWormUpperJawLid", GetJawLidSprite(), SortingOrder + 35);
        if (lowerJawRenderer == null)
            lowerJawRenderer = CreateChildRenderer("CosmicWormLowerJawLid", GetJawLidSprite(), SortingOrder + 35);
        if (mawVoidRenderer == null)
            mawVoidRenderer = CreateChildRenderer("CosmicWormMawVoidPulse", GetMawVoidSprite(), SortingOrder + 36);
    }

    bool HasVisualObjectsForCurrentTier()
    {
        return spineGlow != null &&
               spineGlow.positionCount == SpinePointCount &&
               segmentBands != null &&
               segmentBands.Length == SegmentBandCount &&
               mawRing != null &&
               mawTeeth != null &&
               mawTeeth.Length == MawTeethCount &&
               mawSealRenderer != null &&
               upperJawRenderer != null &&
               lowerJawRenderer != null &&
               mawVoidRenderer != null;
    }

    SpriteRenderer CreateChildRenderer(string childName, Sprite sprite, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = false;
        return renderer;
    }

    void UpdateMawSeal(Vector3 mouth, float length, float height, Color phaseColor)
    {
        if (mawSealRenderer == null)
            return;

        bool visible = phase <= 1;
        mawSealRenderer.enabled = visible;
        if (!visible)
            return;

        PositionRenderer(mawSealRenderer, mouth + transform.right.normalized * (length * 0.012f), length * 0.17f, height * 0.48f);
        Color sealColor = Color.Lerp(new Color(0.18f, 0.2f, 0.24f, 1f), phaseColor, 0.12f);
        sealColor.a = 0.98f;
        mawSealRenderer.color = sealColor;
    }

    void UpdateMawAnimation(Vector3 mouth, Vector3 forward, Vector3 side, float length, float height, Color phaseColor, int visualSeed)
    {
        bool visible = phase >= 2;
        SetRendererVisible(upperJawRenderer, visible);
        SetRendererVisible(lowerJawRenderer, visible);
        SetRendererVisible(mawVoidRenderer, visible);
        SetLineVisible(mawRing, visible);
        SetLinesVisible(mawTeeth, visible);
        if (!visible)
            return;

        float rawOpen = 0.5f + 0.5f * Mathf.Sin(Time.time * (2.9f + phase * 0.45f) + visualSeed * 0.043f);
        float open = Mathf.SmoothStep(0.08f, 1f, rawOpen);
        float lidAlpha = Mathf.Lerp(0.76f, 0.28f, open);
        float lidOffset = Mathf.Lerp(0.035f, 0.13f, open) * height;
        float lidWidth = length * Mathf.Lerp(0.18f, 0.145f, open);
        float lidHeight = height * 0.18f;
        Color lidColor = Color.Lerp(new Color(0.05f, 0.055f, 0.07f, lidAlpha), phaseColor, 0.16f);
        lidColor.a = lidAlpha;

        PositionRenderer(upperJawRenderer, mouth + side * lidOffset + forward * (length * 0.004f), lidWidth, lidHeight);
        PositionRenderer(lowerJawRenderer, mouth - side * lidOffset + forward * (length * 0.004f), lidWidth, lidHeight);
        upperJawRenderer.color = lidColor;
        lowerJawRenderer.color = lidColor;

        float voidWidth = length * Mathf.Lerp(0.052f, 0.09f, open);
        float voidHeight = height * Mathf.Lerp(0.135f, 0.225f, open);
        PositionRenderer(mawVoidRenderer, mouth + forward * (length * 0.002f), voidWidth, voidHeight);
        mawVoidRenderer.color = new Color(0.025f, 0.005f, 0.045f, Mathf.Lerp(0.46f, 0.68f, open));

        Color ringColor = phaseColor;
        ringColor.a = Mathf.Lerp(0.42f, 0.86f, open);
        SetEllipse(mawRing, mouth + Vector3.forward * -0.09f, forward, side, voidWidth * 0.62f, voidHeight * 0.62f, ringColor, 32, 0.08f);

        for (int i = 0; i < mawTeeth.Length; i++)
        {
            float angle = ((float)i / mawTeeth.Length) * Mathf.PI * 2f + Mathf.Sin(Time.time * 1.9f + i) * 0.045f;
            Vector3 radial = (forward * Mathf.Cos(angle) + side * Mathf.Sin(angle)).normalized;
            float inner = Mathf.Lerp(voidHeight * 0.21f, voidHeight * 0.34f, open);
            float outer = Mathf.Lerp(voidHeight * 0.45f, voidHeight * 0.65f, open);
            Color toothColor = Color.Lerp(new Color(0.96f, 0.86f, 1f, 0.78f), phaseColor, 0.28f);
            toothColor.a = Mathf.Lerp(0.42f, 0.92f, open);
            CosmicWormVfxUtility.SetLine(mawTeeth[i], mouth + radial * inner + Vector3.forward * -0.1f, mouth + radial * outer + Vector3.forward * -0.1f, toothColor);
        }
    }

    void UpdateSpineGlow(Vector3 center, Vector3 forward, Vector3 side, float length, float height, float pulse, Color phaseColor)
    {
        if (spineGlow == null || spriteRenderer == null)
            return;

        float spineLength = Mathf.Max(0.5f, length * 0.78f);
        float waveHeight = Mathf.Max(0.08f, height * 0.11f);
        spineGlow.enabled = true;
        spineGlow.startWidth = Mathf.Lerp(0.035f, 0.13f + phase * 0.018f, pulse);
        spineGlow.endWidth = spineGlow.startWidth * 0.72f;
        Color color = phaseColor;
        color.a = Mathf.Lerp(0.12f, 0.38f, pulse);
        spineGlow.startColor = color;
        spineGlow.endColor = new Color(color.r, color.g, color.b, color.a * 0.35f);

        int pointCount = SpinePointCount;
        for (int i = 0; i < pointCount; i++)
        {
            float t = pointCount <= 1 ? 0f : (float)i / (pointCount - 1);
            float x = Mathf.Lerp(-0.43f, 0.38f, t) * spineLength;
            float wave = Mathf.Sin(t * Mathf.PI * 3.8f + Time.time * (2.4f + phase * 0.5f)) * waveHeight;
            spineGlow.SetPosition(i, center + forward * x + side * wave + Vector3.forward * -0.08f);
        }
    }

    void UpdateSegmentBands(Vector3 center, Vector3 forward, Vector3 side, float length, float height, Color phaseColor, int visualSeed)
    {
        if (segmentBands == null)
            return;

        for (int i = 0; i < segmentBands.Length; i++)
        {
            LineRenderer band = segmentBands[i];
            if (band == null)
                continue;

            float t = (i + 0.5f) / segmentBands.Length;
            float wave = Mathf.Sin(Time.time * (2.8f + phase * 0.2f) - i * 0.72f + visualSeed * 0.01f);
            float ripple = Mathf.Sin(Time.time * (4.2f + phase * 0.25f) - i * 0.95f);
            float x = Mathf.Lerp(-0.42f, 0.34f, t) * length + ripple * length * 0.0065f;
            float bodyCurve = Mathf.Sin(t * Mathf.PI * 2.55f - 0.45f) * height * 0.095f;
            float halfWidth = height * Mathf.Lerp(0.09f, 0.31f, Mathf.Sin(t * Mathf.PI)) * Mathf.Lerp(0.86f, 1.12f, (wave + 1f) * 0.5f);
            Vector3 bandCenter = center + forward * x + side * (bodyCurve + wave * height * 0.026f);

            int pointCount = 7;
            band.enabled = true;
            band.positionCount = pointCount;
            band.startWidth = Mathf.Lerp(0.022f, 0.072f, (wave + 1f) * 0.5f) * (phase >= 2 ? 1.18f : 0.92f);
            band.endWidth = band.startWidth * 0.72f;
            Color color = phaseColor;
            color.a = Mathf.Lerp(0.08f, phase >= 2 ? 0.38f : 0.24f, (wave + 1f) * 0.5f);
            band.startColor = color;
            band.endColor = new Color(color.r, color.g, color.b, color.a * 0.42f);

            for (int p = 0; p < pointCount; p++)
            {
                float s = pointCount <= 1 ? 0f : (float)p / (pointCount - 1);
                float sideOffset = Mathf.Lerp(-halfWidth, halfWidth, s);
                float bend = Mathf.Sin(s * Mathf.PI) * height * 0.035f * Mathf.Sign(wave == 0f ? 1f : wave);
                band.SetPosition(p, bandCenter + side * sideOffset + forward * bend + Vector3.forward * -0.082f);
            }
        }
    }

    void PositionRenderer(SpriteRenderer renderer, Vector3 worldPosition, float worldWidth, float worldHeight)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        renderer.transform.localPosition = transform.InverseTransformPoint(worldPosition);
        renderer.transform.localRotation = Quaternion.identity;
        Bounds spriteBounds = renderer.sprite.bounds;
        if (spriteBounds.size.x <= 0.0001f || spriteBounds.size.y <= 0.0001f)
            return;

        Vector3 parentScale = transform.lossyScale;
        float safeX = Mathf.Abs(parentScale.x) > 0.0001f ? Mathf.Abs(parentScale.x) : 1f;
        float safeY = Mathf.Abs(parentScale.y) > 0.0001f ? Mathf.Abs(parentScale.y) : 1f;
        renderer.transform.localScale = new Vector3(
            worldWidth / spriteBounds.size.x / safeX,
            worldHeight / spriteBounds.size.y / safeY,
            1f);
    }

    void SetEllipse(LineRenderer line, Vector3 center, Vector3 forward, Vector3 side, float radiusX, float radiusY, Color color, int segments, float wobble)
    {
        if (line == null)
            return;

        int requestedSegments = MobilePerformanceSettings.UseReducedVfx ? Mathf.Min(segments, 18) : segments;
        int safeSegments = Mathf.Max(10, requestedSegments);
        line.enabled = color.a > 0.001f;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = Mathf.Max(0.018f, Mathf.Min(radiusX, radiusY) * 0.08f);
        line.endWidth = line.startWidth;
        line.positionCount = safeSegments;
        for (int i = 0; i < safeSegments; i++)
        {
            float angle = (float)i / safeSegments * Mathf.PI * 2f;
            float localWobble = 1f + Mathf.Sin(angle * 4f + Time.time * 7.5f + i * 0.17f) * wobble;
            line.SetPosition(i, center + forward * (Mathf.Cos(angle) * radiusX * localWobble) + side * (Mathf.Sin(angle) * radiusY * localWobble));
        }
    }

    void HideAllVfx()
    {
        SetLineVisible(spineGlow, false);
        SetLineVisible(mawRing, false);
        SetLinesVisible(segmentBands, false);
        SetLinesVisible(mawTeeth, false);
        SetRendererVisible(mawSealRenderer, false);
        SetRendererVisible(upperJawRenderer, false);
        SetRendererVisible(lowerJawRenderer, false);
        SetRendererVisible(mawVoidRenderer, false);
    }

    static void SetRendererVisible(SpriteRenderer renderer, bool visible)
    {
        if (renderer != null)
            renderer.enabled = visible;
    }

    static void SetLineVisible(LineRenderer line, bool visible)
    {
        if (line != null)
            line.enabled = visible;
    }

    static void SetLinesVisible(LineRenderer[] lines, bool visible)
    {
        if (lines == null)
            return;

        for (int i = 0; i < lines.Length; i++)
            SetLineVisible(lines[i], visible);
    }

    static void DestroyVisualObject(LineRenderer line)
    {
        if (line != null)
            Destroy(line.gameObject);
    }

    static void DestroyVisualObjects(LineRenderer[] lines)
    {
        if (lines == null)
            return;

        for (int i = 0; i < lines.Length; i++)
            DestroyVisualObject(lines[i]);
    }

    static void DestroyRendererObject(SpriteRenderer renderer)
    {
        if (renderer != null)
            Destroy(renderer.gameObject);
    }

    static Sprite GetMawSealSprite()
    {
        if (mawSealSprite != null)
            return mawSealSprite;

        int width = 128;
        int height = 96;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "GeneratedCosmicWormMawSeal",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float ny = ((y + 0.5f) / height) * 2f - 1f;
            for (int x = 0; x < width; x++)
            {
                float nx = ((x + 0.5f) / width) * 2f - 1f;
                float ellipse = (nx * nx) / 0.94f + (ny * ny) / 0.52f;
                float alpha = 1f - Mathf.SmoothStep(0.82f, 1.02f, ellipse);
                float ridge = Mathf.Abs(Mathf.Sin(nx * 11.7f + ny * 5.3f)) * 0.08f;
                Color color = Color.Lerp(new Color(0.08f, 0.09f, 0.115f, alpha), new Color(0.3f, 0.32f, 0.38f, alpha), Mathf.Clamp01(1f - ellipse + ridge));
                float crack = Mathf.SmoothStep(0.94f, 1f, Mathf.Abs(Mathf.Sin(nx * 18.2f - ny * 13.4f)));
                color = Color.Lerp(color, new Color(0.38f, 0.14f, 0.9f, alpha), crack * 0.14f);
                color.a = alpha;
                pixels[y * width + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        mawSealSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        mawSealSprite.name = texture.name;
        return mawSealSprite;
    }

    static Sprite GetJawLidSprite()
    {
        if (jawLidSprite != null)
            return jawLidSprite;

        int width = 128;
        int height = 64;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "GeneratedCosmicWormJawLid",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float ny = ((y + 0.5f) / height) * 2f - 1f;
            for (int x = 0; x < width; x++)
            {
                float nx = ((x + 0.5f) / width) * 2f - 1f;
                float taper = 1f - Mathf.Abs(nx) * 0.28f;
                float shape = (nx * nx) / 1.05f + (ny * ny) / Mathf.Max(0.12f, taper * 0.38f);
                float alpha = 1f - Mathf.SmoothStep(0.76f, 1.02f, shape);
                Color color = Color.Lerp(new Color(0.02f, 0.018f, 0.026f, alpha), new Color(0.18f, 0.18f, 0.22f, alpha), Mathf.Clamp01(1f - shape));
                color.a = alpha;
                pixels[y * width + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        jawLidSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        jawLidSprite.name = texture.name;
        return jawLidSprite;
    }

    static Sprite GetMawVoidSprite()
    {
        if (mawVoidSprite != null)
            return mawVoidSprite;

        int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "GeneratedCosmicWormMawVoid",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float ny = ((y + 0.5f) / size) * 2f - 1f;
            for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size) * 2f - 1f;
                float dist = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = 1f - Mathf.SmoothStep(0.78f, 1f, dist);
                Color color = Color.Lerp(new Color(0f, 0f, 0.01f, alpha), new Color(0.28f, 0.03f, 0.5f, alpha), Mathf.SmoothStep(0.55f, 0.05f, dist));
                color.a = alpha;
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        mawVoidSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        mawVoidSprite.name = texture.name;
        return mawVoidSprite;
    }
}

public sealed class CosmicWormPhaseBurstVfx : MonoBehaviour
{
    const float Duration = 0.78f;
    const int SortingOrder = 7340;

    Vector3 worldPosition;
    int phase;
    float radius;
    float startedAt;
    LineRenderer ring;
    LineRenderer shock;
    LineRenderer[] cracks;

    public static void Spawn(Vector3 worldPosition, int phase, float radius)
    {
        GameObject effectObject = new GameObject("CosmicWormPhaseBurstVfx");
        CosmicWormPhaseBurstVfx effect = effectObject.AddComponent<CosmicWormPhaseBurstVfx>();
        effect.Configure(worldPosition, phase, radius);
    }

    void Configure(Vector3 position, int newPhase, float newRadius)
    {
        worldPosition = position;
        worldPosition.z = -0.28f;
        phase = Mathf.Clamp(newPhase, 1, 3);
        radius = Mathf.Max(1.2f, newRadius);
        startedAt = Time.time;
        ring = CosmicWormVfxUtility.CreateLine(transform, "PhaseRing", SortingOrder, 0.08f, true);
        shock = CosmicWormVfxUtility.CreateLine(transform, "PhaseShock", SortingOrder + 1, 0.13f, true);
        cracks = new LineRenderer[8];
        for (int i = 0; i < cracks.Length; i++)
            cracks[i] = CosmicWormVfxUtility.CreateLine(transform, "PhaseCrack_" + i, SortingOrder + 2 + i, 0.05f, false);
    }

    void Update()
    {
        float age = Time.time - startedAt;
        float t = Mathf.Clamp01(age / Duration);
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        Color color = CosmicWormVfxUtility.PhaseColor(phase, (1f - t) * 0.78f);
        CosmicWormVfxUtility.SetRing(ring, worldPosition, Mathf.Lerp(radius * 0.2f, radius, t), color, 38, 0.12f);
        CosmicWormVfxUtility.SetRing(shock, worldPosition, Mathf.Lerp(radius * 0.45f, radius * 1.35f, t), new Color(color.r, color.g, color.b, color.a * 0.42f), 44, 0.2f);
        for (int i = 0; i < cracks.Length; i++)
        {
            float angle = ((360f / cracks.Length) * i + Mathf.Sin(i * 1.73f) * 12f) * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            float startDistance = radius * Mathf.Lerp(0.08f, 0.24f, t);
            float endDistance = radius * Mathf.Lerp(0.45f, 1.08f, Mathf.SmoothStep(0f, 1f, t));
            CosmicWormVfxUtility.SetLine(cracks[i], worldPosition + direction * startDistance, worldPosition + direction * endDistance, new Color(color.r, color.g, color.b, color.a * 0.66f));
        }
    }
}

public sealed class CosmicWormSpitVfx : MonoBehaviour
{
    const float Duration = 0.34f;
    const int SortingOrder = 7360;

    Vector2 origin;
    Vector2 direction;
    int phase;
    float startedAt;
    LineRenderer ring;
    LineRenderer[] streaks;

    public static void Spawn(Vector2 origin, Vector2 direction, int phase)
    {
        GameObject effectObject = new GameObject("CosmicWormSpitVfx");
        CosmicWormSpitVfx effect = effectObject.AddComponent<CosmicWormSpitVfx>();
        effect.Configure(origin, direction, phase);
    }

    void Configure(Vector2 newOrigin, Vector2 newDirection, int newPhase)
    {
        origin = newOrigin;
        direction = CosmicWormVfxUtility.SafeDirection(newDirection, Vector2.right);
        phase = Mathf.Clamp(newPhase, 1, 3);
        startedAt = Time.time;
        ring = CosmicWormVfxUtility.CreateLine(transform, "SpitRing", SortingOrder, 0.06f, true);
        streaks = new LineRenderer[5];
        for (int i = 0; i < streaks.Length; i++)
            streaks[i] = CosmicWormVfxUtility.CreateLine(transform, "SpitStreak_" + i, SortingOrder + 1 + i, 0.04f, false);
    }

    void Update()
    {
        float t = Mathf.Clamp01((Time.time - startedAt) / Duration);
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        Color color = CosmicWormVfxUtility.PhaseColor(phase, (1f - t) * 0.82f);
        Vector3 center = new Vector3(origin.x, origin.y, -0.31f);
        CosmicWormVfxUtility.SetRing(ring, center, Mathf.Lerp(0.16f, 0.82f, t), color, 18, 0.1f);
        Vector2 side = new Vector2(-direction.y, direction.x);
        for (int i = 0; i < streaks.Length; i++)
        {
            float offset = Mathf.Lerp(-0.42f, 0.42f, streaks.Length <= 1 ? 0.5f : (float)i / (streaks.Length - 1));
            Vector3 start = center + (Vector3)(side * offset * (1f - t));
            Vector3 end = start + (Vector3)(direction * Mathf.Lerp(0.45f, 1.65f, t));
            CosmicWormVfxUtility.SetLine(streaks[i], start, end, new Color(color.r, color.g, color.b, color.a * (1f - Mathf.Abs(offset))));
        }
    }
}

public sealed class CosmicWormDashWarningVfx : MonoBehaviour
{
    const int SortingOrder = 7310;

    Vector2 origin;
    Vector2 direction;
    float range;
    float duration;
    float startedAt;
    LineRenderer glow;
    LineRenderer core;
    LineRenderer leftEdge;
    LineRenderer rightEdge;

    public static void Spawn(Vector2 origin, Vector2 direction, float range, float duration)
    {
        GameObject effectObject = new GameObject("CosmicWormDashWarningVfx");
        CosmicWormDashWarningVfx effect = effectObject.AddComponent<CosmicWormDashWarningVfx>();
        effect.Configure(origin, direction, range, duration);
    }

    void Configure(Vector2 newOrigin, Vector2 newDirection, float newRange, float newDuration)
    {
        origin = newOrigin;
        direction = CosmicWormVfxUtility.SafeDirection(newDirection, Vector2.right);
        range = Mathf.Max(2f, newRange);
        duration = Mathf.Max(0.1f, newDuration);
        startedAt = Time.time;
        glow = CosmicWormVfxUtility.CreateLine(transform, "DashWarningGlow", SortingOrder, 0.24f, false);
        core = CosmicWormVfxUtility.CreateLine(transform, "DashWarningCore", SortingOrder + 1, 0.06f, false);
        leftEdge = CosmicWormVfxUtility.CreateLine(transform, "DashWarningLeft", SortingOrder + 2, 0.035f, false);
        rightEdge = CosmicWormVfxUtility.CreateLine(transform, "DashWarningRight", SortingOrder + 2, 0.035f, false);
    }

    void Update()
    {
        float t = Mathf.Clamp01((Time.time - startedAt) / duration);
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float alpha = Mathf.Lerp(0.18f, 0.76f, Mathf.PingPong(t * 5f, 1f)) * (1f - t * 0.18f);
        Color glowColor = new Color(0.62f, 0.34f, 1f, alpha * 0.45f);
        Color coreColor = new Color(0.93f, 0.78f, 1f, alpha);
        Vector3 start = new Vector3(origin.x, origin.y, -0.32f);
        Vector3 end = start + (Vector3)(direction * range);
        Vector2 side = new Vector2(-direction.y, direction.x);
        CosmicWormVfxUtility.SetLine(glow, start, end, glowColor);
        CosmicWormVfxUtility.SetLine(core, start, end, coreColor);
        CosmicWormVfxUtility.SetLine(leftEdge, start + (Vector3)(side * 0.45f), end + (Vector3)(side * 1.05f), new Color(coreColor.r, coreColor.g, coreColor.b, coreColor.a * 0.55f));
        CosmicWormVfxUtility.SetLine(rightEdge, start - (Vector3)(side * 0.45f), end - (Vector3)(side * 1.05f), new Color(coreColor.r, coreColor.g, coreColor.b, coreColor.a * 0.55f));
    }
}

public sealed class CosmicWormDashTrailVfx : MonoBehaviour
{
    const float Duration = 0.36f;
    const int SortingOrder = 7330;

    Vector2 origin;
    Vector2 direction;
    float radius;
    float startedAt;
    LineRenderer ring;
    LineRenderer wake;

    public static void Spawn(Vector2 origin, Vector2 direction, float radius)
    {
        GameObject effectObject = new GameObject("CosmicWormDashTrailVfx");
        CosmicWormDashTrailVfx effect = effectObject.AddComponent<CosmicWormDashTrailVfx>();
        effect.Configure(origin, direction, radius);
    }

    void Configure(Vector2 newOrigin, Vector2 newDirection, float newRadius)
    {
        origin = newOrigin;
        direction = CosmicWormVfxUtility.SafeDirection(newDirection, Vector2.right);
        radius = Mathf.Max(0.6f, newRadius);
        startedAt = Time.time;
        ring = CosmicWormVfxUtility.CreateLine(transform, "DashTrailRing", SortingOrder, 0.09f, true);
        wake = CosmicWormVfxUtility.CreateLine(transform, "DashTrailWake", SortingOrder + 1, 0.18f, false);
    }

    void Update()
    {
        float t = Mathf.Clamp01((Time.time - startedAt) / Duration);
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float alpha = (1f - t) * 0.56f;
        Color color = new Color(0.58f, 0.28f, 1f, alpha);
        Vector3 center = new Vector3(origin.x, origin.y, -0.33f);
        CosmicWormVfxUtility.SetRing(ring, center, Mathf.Lerp(radius * 0.45f, radius * 1.25f, t), color, 22, 0.18f);
        CosmicWormVfxUtility.SetLine(wake, center, center - (Vector3)(direction * Mathf.Lerp(radius * 0.8f, radius * 2.2f, t)), new Color(color.r, color.g, color.b, color.a * 0.58f));
    }
}

public sealed class CosmicWormSwallowVfx : MonoBehaviour
{
    const int SortingOrder = 7320;
    const int ArcCount = 5;

    static readonly Dictionary<int, CosmicWormSwallowVfx> ActiveEffects = new Dictionary<int, CosmicWormSwallowVfx>();

    int sourceViewId;
    Vector2 origin;
    Vector2 direction;
    float radius;
    float duration;
    float startedAt;
    LineRenderer leftEdge;
    LineRenderer rightEdge;
    LineRenderer centerStream;
    LineRenderer[] arcs;

    public static void StartEffect(int sourceViewId, Vector2 origin, Vector2 direction, float radius, float duration)
    {
        StopEffect(sourceViewId);
        GameObject effectObject = new GameObject("CosmicWormSwallowVfx_" + sourceViewId);
        CosmicWormSwallowVfx effect = effectObject.AddComponent<CosmicWormSwallowVfx>();
        effect.Configure(sourceViewId, origin, direction, radius, duration);
        ActiveEffects[sourceViewId] = effect;
    }

    public static void StopEffect(int sourceViewId)
    {
        if (!ActiveEffects.TryGetValue(sourceViewId, out CosmicWormSwallowVfx effect) || effect == null)
        {
            ActiveEffects.Remove(sourceViewId);
            return;
        }

        ActiveEffects.Remove(sourceViewId);
        Destroy(effect.gameObject);
    }

    void Configure(int newSourceViewId, Vector2 newOrigin, Vector2 newDirection, float newRadius, float newDuration)
    {
        sourceViewId = newSourceViewId;
        origin = newOrigin;
        direction = CosmicWormVfxUtility.SafeDirection(newDirection, Vector2.right);
        radius = Mathf.Max(2f, newRadius);
        duration = Mathf.Max(0.2f, newDuration);
        startedAt = Time.time;
        leftEdge = CosmicWormVfxUtility.CreateLine(transform, "SwallowLeftEdge", SortingOrder, 0.07f, false);
        rightEdge = CosmicWormVfxUtility.CreateLine(transform, "SwallowRightEdge", SortingOrder, 0.07f, false);
        centerStream = CosmicWormVfxUtility.CreateLine(transform, "SwallowCenterStream", SortingOrder + 1, 0.14f, false);
        arcs = new LineRenderer[ArcCount];
        for (int i = 0; i < arcs.Length; i++)
            arcs[i] = CosmicWormVfxUtility.CreateLine(transform, "SwallowArc_" + i, SortingOrder + 2 + i, 0.045f, false);
    }

    void OnDestroy()
    {
        if (ActiveEffects.TryGetValue(sourceViewId, out CosmicWormSwallowVfx effect) && effect == this)
            ActiveEffects.Remove(sourceViewId);
    }

    void Update()
    {
        float t = Mathf.Clamp01((Time.time - startedAt) / duration);
        if (t >= 1f)
        {
            StopEffect(sourceViewId);
            return;
        }

        Vector2 side = new Vector2(-direction.y, direction.x);
        Vector3 start = new Vector3(origin.x, origin.y, -0.34f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10.5f);
        Color color = new Color(0.72f, 0.42f, 1f, Mathf.Lerp(0.32f, 0.74f, pulse) * (1f - t * 0.25f));
        Vector2 left = Rotate(direction, 58f);
        Vector2 right = Rotate(direction, -58f);
        CosmicWormVfxUtility.SetLine(leftEdge, start, start + (Vector3)(left * radius), color);
        CosmicWormVfxUtility.SetLine(rightEdge, start, start + (Vector3)(right * radius), color);
        CosmicWormVfxUtility.SetLine(centerStream, start + (Vector3)(direction * radius), start, new Color(color.r, color.g, color.b, color.a * 0.72f));

        for (int i = 0; i < arcs.Length; i++)
        {
            float arcT = (i + 1f) / (arcs.Length + 1f);
            float distance = radius * Mathf.Repeat(arcT - t * 0.9f, 1f);
            float width = Mathf.Lerp(0.32f, 1.78f, Mathf.Clamp01(distance / radius));
            Vector3 center = start + (Vector3)(direction * distance);
            Vector3 a = center - (Vector3)(side * width);
            Vector3 b = center + (Vector3)(side * width);
            Color arcColor = new Color(color.r, color.g, color.b, color.a * Mathf.Lerp(0.95f, 0.2f, distance / radius));
            CosmicWormVfxUtility.SetArc(arcs[i], a, b, direction, arcColor, 9, Mathf.Sin(Time.time * 8f + i) * 0.18f);
        }
    }

    static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos).normalized;
    }
}

static class CosmicWormVfxUtility
{
    static Material sharedMaterial;

    public static Color PhaseColor(int phase, float alpha)
    {
        switch (Mathf.Clamp(phase, 1, 3))
        {
            case 3:
                return new Color(1f, 0.18f, 0.62f, alpha);
            case 2:
                return new Color(0.55f, 0.28f, 1f, alpha);
            default:
                return new Color(0.24f, 0.48f, 1f, alpha);
        }
    }

    public static LineRenderer CreateLine(Transform parent, string objectName, int sortingOrder, float width, bool loop)
    {
        if (sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            sharedMaterial = new Material(shader)
            {
                name = "CosmicWormVfxMaterial",
                color = Color.white
            };
            sharedMaterial.renderQueue = 5000;
        }

        GameObject lineObject = new GameObject(objectName);
        if (parent != null)
            lineObject.transform.SetParent(parent, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.material = sharedMaterial;
        line.useWorldSpace = true;
        line.loop = loop;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 10;
        line.numCornerVertices = 8;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = sortingOrder;
        line.startWidth = width;
        line.endWidth = width;
        line.positionCount = 0;
        return line;
    }

    public static Vector2 SafeDirection(Vector2 direction, Vector2 fallback)
    {
        if (direction.sqrMagnitude > 0.001f)
            return direction.normalized;

        return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector2.right;
    }

    public static void SetLine(LineRenderer line, Vector3 start, Vector3 end, Color color)
    {
        if (line == null)
            return;

        line.enabled = color.a > 0.001f;
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, color.a * 0.35f);
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    public static void SetRing(LineRenderer line, Vector3 center, float radius, Color color, int segments, float wobble)
    {
        if (line == null)
            return;

        int safeSegments = Mathf.Max(8, segments);
        line.enabled = color.a > 0.001f;
        line.startColor = color;
        line.endColor = color;
        line.positionCount = safeSegments;
        for (int i = 0; i < safeSegments; i++)
        {
            float angle = (float)i / safeSegments * Mathf.PI * 2f;
            float localRadius = radius * (1f + Mathf.Sin(angle * 3f + Time.time * 9f + i * 0.13f) * wobble);
            line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * localRadius, Mathf.Sin(angle) * localRadius, 0f));
        }
    }

    public static void SetArc(LineRenderer line, Vector3 start, Vector3 end, Vector2 bendDirection, Color color, int pointCount, float bend)
    {
        if (line == null)
            return;

        int safePointCount = Mathf.Max(3, pointCount);
        line.enabled = color.a > 0.001f;
        line.startColor = color;
        line.endColor = color;
        line.positionCount = safePointCount;
        Vector3 bendVector = new Vector3(bendDirection.x, bendDirection.y, 0f).normalized * bend;
        for (int i = 0; i < safePointCount; i++)
        {
            float t = (float)i / (safePointCount - 1);
            Vector3 point = Vector3.Lerp(start, end, t) + bendVector * Mathf.Sin(t * Mathf.PI);
            line.SetPosition(i, point);
        }
    }
}
