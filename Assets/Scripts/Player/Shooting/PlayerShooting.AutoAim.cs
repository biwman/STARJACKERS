using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public partial class PlayerShooting
{
    void ResolveComplexAutoAim(WeaponAttackProfile profile, out Vector2 direction, out Vector2 targetPoint, out int targetViewId)
    {
        float range = GetComplexRangeWorld(profile);
        bool blockByObstacles = !IsArcWeaponProfile(profile);
        if (TryResolveAutoAimTarget(range, blockByObstacles, profile, out AutoAimTargetResult target))
        {
            direction = ResolveSafeAimDirection(target.AimPoint - (Vector2)transform.position);
            targetPoint = target.AimPoint;
            targetViewId = target.ViewId;
            return;
        }

        direction = ResolveSafeAimDirection(transform.up);
        targetPoint = (Vector2)transform.position + (direction * range);
        targetViewId = 0;
    }

    Vector2 ResolveComplexAutoAimDirection(WeaponAttackProfile profile)
    {
        ResolveComplexAutoAim(profile, out Vector2 direction, out _, out _);
        return direction;
    }

    Vector2 ResolveComplexAutoAimTargetPoint(WeaponAttackProfile profile)
    {
        ResolveComplexAutoAim(profile, out _, out Vector2 targetPoint, out _);
        return targetPoint;
    }

    bool TryResolveAutoAimTarget(float range, bool blockByObstacles, WeaponAttackProfile profile, out AutoAimTargetResult result)
    {
        result = default;
        Vector2 origin = transform.position;
        float maxDistance = Mathf.Max(0.1f, range) + AutoAimAcquireRangeBonus;
        PlayerHealth[] targets = RuntimeSceneQueryCache.GetPlayers();
        float bestDistance = float.MaxValue;

        for (int i = 0; i < targets.Length; i++)
        {
            PlayerHealth target = targets[i];
            if (!IsValidAutoAimTarget(target))
                continue;

            Vector2 baseAimPoint = GetAutoAimTargetPoint(target);
            Vector2 closestPoint = GetAutoAimClosestPoint(target, origin, baseAimPoint);
            float distance = Vector2.Distance(origin, closestPoint);
            if (distance > maxDistance || distance >= bestDistance)
                continue;

            if (blockByObstacles && IsLineBlockedByObstacle(origin, baseAimPoint, target.transform))
                continue;

            bestDistance = distance;
            result = new AutoAimTargetResult
            {
                Health = target,
                AimPoint = ResolveAutoAimLeadPoint(target, origin, baseAimPoint, range, profile),
                ViewId = target.photonView != null ? target.photonView.ViewID : 0
            };
        }

        return result.Health != null;
    }

    bool IsValidAutoAimTarget(PlayerHealth target)
    {
        if (target == null || target.IsWreck || target.IsEvacuationAnimating || target.CurrentHP <= 0 || target.photonView == null)
            return false;

        if (target.IsSpawnInvulnerable)
            return false;

        PlayerRepairDocking repairDocking = target.GetComponent<PlayerRepairDocking>();
        if (repairDocking != null && repairDocking.IsDamageImmune)
            return false;

        if (target.photonView.ViewID == (photonView != null ? photonView.ViewID : 0))
            return false;

        if (target.GetComponent<LureBeaconDecoy>() != null)
            return false;

        HideInNebulaTarget nebulaState = target.GetComponent<HideInNebulaTarget>();
        return nebulaState == null || !nebulaState.IsHiddenFromLocalPlayer();
    }

    Vector2 GetAutoAimTargetPoint(PlayerHealth target)
    {
        Collider2D targetCollider = target != null ? target.GetComponentInChildren<Collider2D>() : null;
        if (targetCollider != null)
            return targetCollider.bounds.center;

        return target != null ? (Vector2)target.transform.position : (Vector2)transform.position;
    }

    Vector2 ResolveAutoAimLeadPoint(PlayerHealth target, Vector2 origin, Vector2 basePoint, float range, WeaponAttackProfile profile)
    {
        if (target == null || ShouldSkipAutoAimLead(profile))
            return basePoint;

        Rigidbody2D targetBody = target.GetComponent<Rigidbody2D>();
        if (targetBody == null)
            targetBody = target.GetComponentInChildren<Rigidbody2D>();

        Vector2 velocity = targetBody != null ? targetBody.linearVelocity : Vector2.zero;
        if (velocity.sqrMagnitude < AutoAimLeadVelocityThreshold * AutoAimLeadVelocityThreshold)
            return basePoint;

        bool isArc = IsArcWeaponProfile(profile);
        float leadSeconds = isArc
            ? GetAutoAimArcLeadSeconds(profile)
            : GetAutoAimDirectLeadSeconds(origin, basePoint, profile);
        if (leadSeconds <= 0.001f)
            return basePoint;

        float leadScale = isArc ? AutoAimArcLeadScale : AutoAimDirectLeadScale;
        Vector2 leadPoint = basePoint + (velocity * leadSeconds * leadScale);
        return isArc ? ClampAutoAimPointToRange(origin, leadPoint, range) : leadPoint;
    }

    bool ShouldSkipAutoAimLead(WeaponAttackProfile profile)
    {
        return IsRocketWeaponProfile(profile) ||
               IsAstroCutterProfile(profile) ||
               IsPulseDisruptorSuperProfile(profile);
    }

    float GetAutoAimDirectLeadSeconds(Vector2 origin, Vector2 targetPoint, WeaponAttackProfile profile)
    {
        float speed = profile != null
            ? Mathf.Max(0.1f, profile.ProjectileSpeed * GetProjectileSpeedMultiplier())
            : Mathf.Max(0.1f, bulletSpeed * GetProjectileSpeedMultiplier());
        float distance = Vector2.Distance(origin, targetPoint);
        return Mathf.Clamp(distance / speed, 0f, AutoAimDirectLeadMaxSeconds);
    }

    float GetAutoAimArcLeadSeconds(WeaponAttackProfile profile)
    {
        float flightTime = profile != null ? Mathf.Max(0f, profile.FlightTime) : 0f;
        return Mathf.Clamp(flightTime, 0f, AutoAimArcLeadMaxSeconds);
    }

    Vector2 ClampAutoAimPointToRange(Vector2 origin, Vector2 point, float range)
    {
        float maxRange = Mathf.Max(0.1f, range);
        Vector2 offset = point - origin;
        if (offset.sqrMagnitude <= maxRange * maxRange)
            return point;

        return origin + (offset.normalized * maxRange);
    }

    Vector2 GetAutoAimClosestPoint(PlayerHealth target, Vector2 origin, Vector2 fallback)
    {
        Collider2D targetCollider = target != null ? target.GetComponentInChildren<Collider2D>() : null;
        if (targetCollider != null && targetCollider.enabled)
            return targetCollider.ClosestPoint(origin);

        return fallback;
    }

    bool IsLineBlockedByObstacle(Vector2 start, Vector2 end, Transform target)
    {
        int hitCount = Physics2D.Linecast(start, end, CreatePhysicsQueryFilter(), LineBlockHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = LineBlockHits[i].collider;
            if (hit == null || hit.isTrigger)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            if (target != null && (hit.transform == target || hit.transform.IsChildOf(target)))
                continue;

            if (hit.GetComponentInParent<Bullet>() != null)
                continue;

            if (hit.GetComponentInParent<ObstacleChunk>() != null)
                return true;
        }

        return false;
    }

    Vector2 ResolveSafeAimDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
            return direction.normalized;

        Vector2 forward = transform.up;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector2.up;
    }
}
