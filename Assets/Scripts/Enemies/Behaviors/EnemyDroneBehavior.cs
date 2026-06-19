using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemyDroneBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    float nextTargetRefreshTime;
    float nextRepathTime;
    Vector2 currentMoveDirection = Vector2.up;
    Transform currentTarget;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                Mathf.Max(weapon.AmmoCount, 1),
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

        if (Time.time >= nextTargetRefreshTime)
        {
            nextTargetRefreshTime = Time.time + movement.TargetRefreshInterval;
            currentTarget = ResolveTarget();
        }

        if (currentTarget == null)
        {
            ApplyIdleDrift();
            return;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + movement.RepathInterval;
            currentMoveDirection = CalculateMoveDirection(currentTarget.position);
        }

        Vector2 desiredVelocity = currentMoveDirection * bot.EffectiveMoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.14f);

        Vector2 aimDirection = (Vector2)currentTarget.position - rb.position;
        if (weapon != null && weapon.RotateTowardAim && aimDirection.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg + 90f;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }

        TryShootAtTarget(aimDirection);
    }

    void ApplyIdleDrift()
    {
        if (rb.linearVelocity.sqrMagnitude < 0.05f)
        {
            Vector2 fallback = currentMoveDirection.sqrMagnitude > 0.001f ? currentMoveDirection : Vector2.up;
            rb.linearVelocity = fallback.normalized * (bot.EffectiveMoveSpeed * 0.36f);
        }

        float spin = Mathf.Sin(Time.time * 0.45f + view.ViewID * 0.23f) * movement.IdleDriftTurnSpeed;
        rb.MoveRotation(rb.rotation + spin * Time.fixedDeltaTime);
    }

    Transform ResolveTarget()
    {
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, transform.position, movement.DisengageRadius, true))
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

    Vector2 CalculateMoveDirection(Vector2 targetPosition)
    {
        Vector2 toTarget = targetPosition - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return currentMoveDirection;

        Vector2 towardTarget = toTarget / distance;
        Vector2 orbitDirection = new Vector2(-towardTarget.y, towardTarget.x);
        if (Mathf.Sin(Time.time * 0.6f + view.ViewID * 0.27f) < 0f)
            orbitDirection *= -1f;

        Vector2 result;
        if (distance > movement.PreferredDistance)
            result = towardTarget * 0.84f + orbitDirection * 0.28f;
        else if (distance < movement.OrbitDistance)
            result = -towardTarget * 0.72f + orbitDirection * 0.52f;
        else
            result = orbitDirection * 0.85f + towardTarget * 0.18f;

        return result.normalized;
    }

    void TryShootAtTarget(Vector2 aimDirection)
    {
        if (shooting == null || weapon == null || aimDirection.sqrMagnitude <= 0.001f)
            return;

        if (aimDirection.magnitude > weapon.Range)
            return;

        if (weapon.RotateTowardAim)
        {
            Vector2 normalizedAim = aimDirection.normalized;
            float facingDot = Vector2.Dot(-transform.up, normalizedAim);
            if (facingDot < 0.9f)
                return;
        }

        shooting.TryFireBot(aimDirection.normalized);
    }
}

