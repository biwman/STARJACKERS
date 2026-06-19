using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemyCorsairBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    Vector2 orbitCenter;
    float orbitRadius;
    float orbitAngle;
    float orbitDirection;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        orbitCenter = Vector2.zero;
        orbitRadius = Mathf.Max(6f, Mathf.Min(mapSize.x, mapSize.y) * movement.OrbitRadiusFactor);
        int orbitSeed = view != null ? view.ViewID : 0;
        orbitAngle = Mathf.Abs(orbitSeed * 0.137f) % (Mathf.PI * 2f);
        orbitDirection = (orbitSeed % 2 == 0) ? 1f : -1f;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                9999,
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

        orbitAngle += orbitDirection * movement.OrbitAngularSpeed * Time.fixedDeltaTime;
        Vector2 fromCenter = rb.position - orbitCenter;
        if (fromCenter.sqrMagnitude < 0.01f)
            fromCenter = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle));

        Vector2 radialDirection = fromCenter.normalized;
        Vector2 tangentDirection = orbitDirection > 0f
            ? new Vector2(-radialDirection.y, radialDirection.x)
            : new Vector2(radialDirection.y, -radialDirection.x);

        float radialError = orbitRadius - fromCenter.magnitude;
        Vector2 desiredVelocity = tangentDirection * bot.EffectiveMoveSpeed + radialDirection * (radialError * 1.35f);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.16f);

        if (desiredVelocity.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(desiredVelocity.y, desiredVelocity.x) * Mathf.Rad2Deg + 270f;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }

        TryShootNearestTarget();
    }

    void TryShootNearestTarget()
    {
        if (shooting == null || weapon == null)
            return;

        Transform bestTarget = EnemyTargetingUtility.FindClosestTarget(transform.position, health, weapon.Range, true);

        if (bestTarget == null)
            return;

        Vector2 shootDirection = bestTarget.position - transform.position;
        if (shootDirection.sqrMagnitude <= 0.01f)
            return;

        shooting.TryFireBot(shootDirection.normalized);
    }
}

