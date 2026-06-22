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
    bool TryFireComplexAttack(WeaponAttackProfile profile, Vector2 direction, Vector2 targetPoint, bool consumeAmmo, bool isSuper, int homingTargetViewId = 0)
    {
        if (profile == null || Time.time < nextComplexAttackTime)
            return false;

        ComplexWeaponRuntimeState activeWeaponState = consumeAmmo ? GetActiveComplexWeaponState() : null;
        if (consumeAmmo)
        {
            if (activeWeaponState == null || activeWeaponState.CurrentAmmo <= 0)
                return false;
        }

        direction = ResolveSafeAimDirection(direction);
        if (consumeAmmo)
        {
            activeWeaponState.CurrentAmmo = Mathf.Max(0, activeWeaponState.CurrentAmmo - 1);
            if (activeWeaponState.CurrentAmmo <= 0)
            {
                ApplyAshEmergencyCapacitor(activeWeaponState, profile);
            }
            else if (activeWeaponState.CurrentAmmo < activeWeaponState.MaxAmmo && activeWeaponState.NextAmmoAt <= 0f)
            {
                activeWeaponState.AmmoReloadStartedAt = Time.time;
                activeWeaponState.NextAmmoAt = Time.time + GetAdjustedAmmoReloadTime(profile, false);
            }

            SyncActiveComplexAmmoMirror();
        }

        int ownerId = photonView != null ? photonView.ViewID : 0;
        if (IsPulseDisruptorSuperProfile(profile))
        {
            if (TryStartPulseDisruptorWave(profile, ownerId))
            {
                nextComplexAttackTime = Time.time + GetAdjustedAttackCooldown(profile);
                NotifyShotFiredForUseCancel();
                return true;
            }

            return false;
        }

        if (IsAstroCutterProfile(profile))
        {
            if (complexBurstRoutine != null)
            {
                StopCoroutine(complexBurstRoutine);
                complexBurstRoutine = null;
            }

            if (TryStartAstroCutterBeam(profile, direction, photonView != null ? photonView.ViewID : 0))
            {
                nextComplexAttackTime = Time.time + GetAdjustedAttackCooldown(profile);
                NotifyShotFiredForUseCancel();
                return true;
            }

            return false;
        }

        if (complexBurstRoutine != null)
            StopCoroutine(complexBurstRoutine);

        bool pilotBreachSalvo = TryConsumePilotBreachSalvo();
        complexBurstRoutine = StartCoroutine(FireComplexBurst(profile, direction, targetPoint, homingTargetViewId, pilotBreachSalvo));
        nextComplexAttackTime = Time.time + GetAdjustedAttackCooldown(profile);
        NotifyShotFiredForUseCancel();
        return true;
    }

    public bool TryFireVectorBarrage(string targetViewIds)
    {
        if (string.IsNullOrWhiteSpace(targetViewIds) || AreShipControlsBlocked())
            return false;

        WeaponAttackProfile profile = WeaponAttackCatalog.GetSuperAttack(WeaponAttackCatalog.GatlingGunId);
        if (profile == null)
            return false;

        string[] ids = targetViewIds.Split(',');
        int fired = 0;
        for (int i = 0; i < ids.Length; i++)
        {
            if (!int.TryParse(ids[i], out int targetViewId) || targetViewId <= 0)
                continue;

            PhotonView targetView = PhotonView.Find(targetViewId);
            PlayerHealth targetHealth = targetView != null ? targetView.GetComponent<PlayerHealth>() : null;
            if (targetHealth == null || targetHealth.IsWreck || targetHealth.CurrentHP <= 0)
                continue;

            Vector2 direction = targetHealth.transform.position - transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
                continue;

            Vector2 safeDirection = direction.normalized;
            Vector2 targetPoint = (Vector2)transform.position + (safeDirection * GetComplexRangeWorld(profile));
            StartCoroutine(FireComplexBurst(profile, safeDirection, targetPoint, 0, false));
            fired++;
        }

        if (fired > 0)
            NotifyShotFiredForUseCancel();

        return fired > 0;
    }

    void ApplyAshEmergencyCapacitor(ComplexWeaponRuntimeState state, WeaponAttackProfile profile)
    {
        if (state == null)
            return;

        bool applies = ShouldApplyAshEmergencyCapacitor(profile);
        state.AshEmergencyReloadPending = applies;

        if (state.NextAmmoAt <= 0f)
        {
            state.AmmoReloadStartedAt = Time.time;
            state.NextAmmoAt = Time.time + GetAdjustedAmmoReloadTime(profile, applies);
            return;
        }

        if (!applies)
            return;

        float remaining = Mathf.Max(0.001f, state.NextAmmoAt - Time.time);
        state.AmmoReloadStartedAt = Time.time;
        state.NextAmmoAt = Time.time + (remaining * AshEmergencyAmmoReloadMultiplier);
    }

    public void SwitchComplexWeapon()
    {
        if (!photonView.IsMine || !IsComplexShootingActive || complexWeaponStates.Count <= 1 || IsComplexWeaponSwitchLockedByDamage)
            return;

        activeComplexWeaponIndex = (activeComplexWeaponIndex + 1) % complexWeaponStates.Count;
        ResetComplexPressState();
        HideAimMarker();
        SyncActiveComplexAmmoMirror();
    }

    IEnumerator FireComplexBurst(WeaponAttackProfile profile, Vector2 direction, Vector2 targetPoint, int homingTargetViewId, bool pilotBreachSalvo = false)
    {
        if (profile == null)
            yield break;

        if (profile.StartDelay > 0f)
            yield return new WaitForSeconds(profile.StartDelay);

        int ownerId = photonView != null ? photonView.ViewID : 0;
        int count = Mathf.Max(1, profile.ProjectileCount);
        bool playOneSoundForBurst = ShouldPlayShotSoundOnceForBurst(profile);
        if (playOneSoundForBurst)
            photonView.RPC(nameof(PlayShotSfx), RpcTarget.All, profile.ShotSoundId ?? string.Empty);

        for (int i = 0; i < count; i++)
        {
            Vector2 shotDirection = IsDoubleRocketLauncherProfile(profile)
                ? ResolveDoubleRocketProjectileDirection(direction, i, count, homingTargetViewId)
                : ResolveComplexProjectileDirection(direction, profile.SpreadAngle, i, count);
            Vector2 projectileTargetPoint = ResolveComplexProjectileTargetPoint(profile, targetPoint, direction, i, count);
            Vector3 spawnPos = ResolveComplexProjectileSpawnPosition(profile, shotDirection, i, count);
            bool spawned = SpawnComplexBullet(profile, shotDirection.normalized, spawnPos, ownerId, projectileTargetPoint, homingTargetViewId, pilotBreachSalvo);
            if (spawned && !playOneSoundForBurst)
                photonView.RPC(nameof(PlayShotSfx), RpcTarget.All, profile.ShotSoundId ?? string.Empty);

            if (i < count - 1 && profile.ProjectileInterval > 0f)
                yield return new WaitForSeconds(profile.ProjectileInterval);
        }
    }

    bool ShouldPlayShotSoundOnceForBurst(WeaponAttackProfile profile)
    {
        return IsGatlingGunProfile(profile);
    }

    Vector2 ResolveComplexProjectileDirection(Vector2 baseDirection, float spreadAngle, int index, int count)
    {
        baseDirection = ResolveSafeAimDirection(baseDirection);
        if (count <= 1 || Mathf.Abs(spreadAngle) <= 0.01f)
            return baseDirection;

        float t = count == 1 ? 0f : index / (float)(count - 1);
        float angle = Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, t);
        return Quaternion.Euler(0f, 0f, angle) * baseDirection;
    }

    Vector2 ResolveDoubleRocketProjectileDirection(Vector2 baseDirection, int index, int count, int homingTargetViewId)
    {
        baseDirection = ResolveSafeAimDirection(baseDirection);
        if (homingTargetViewId > 0 || count <= 1)
            return baseDirection;

        float centered = index - ((count - 1) * 0.5f);
        float side = Mathf.Abs(centered) > 0.001f
            ? Mathf.Sign(centered)
            : (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        float edgeFactor = Mathf.Clamp01(Mathf.Abs(centered) / Mathf.Max(0.5f, (count - 1) * 0.5f));
        float magnitude = UnityEngine.Random.Range(DoubleRocketUnguidedMinDivergenceAngle, DoubleRocketUnguidedMaxDivergenceAngle);
        magnitude *= Mathf.Lerp(0.55f, 1f, edgeFactor);
        float jitter = UnityEngine.Random.Range(-0.9f, 0.9f);
        float angle = side * Mathf.Max(0.5f, magnitude + jitter);
        return Quaternion.Euler(0f, 0f, angle) * baseDirection;
    }

    Vector3 ResolveComplexProjectileSpawnPosition(WeaponAttackProfile profile, Vector2 shotDirection, int index, int count)
    {
        Vector2 safeDirection = ResolveSafeAimDirection(shotDirection);
        Vector2 spawnPos = (Vector2)transform.position + safeDirection * Mathf.Max(0.05f, muzzleOffsetDistance);
        if (IsDoubleRocketLauncherProfile(profile) && count > 1)
        {
            Vector2 perpendicular = new Vector2(-safeDirection.y, safeDirection.x);
            float centered = index - ((count - 1) * 0.5f);
            spawnPos += perpendicular * centered * ResolveDoubleRocketLaneSpacing(profile);
        }

        return spawnPos;
    }

    float ResolveDoubleRocketLaneSpacing(WeaponAttackProfile profile)
    {
        float sizeFactor = profile != null ? Mathf.Clamp(profile.ProjectileSize / 0.49f, 0.85f, 1.25f) : 1f;
        return DoubleRocketParallelLaneSpacing * sizeFactor;
    }

    Vector2 ResolveComplexProjectileTargetPoint(WeaponAttackProfile profile, Vector2 baseTargetPoint, Vector2 baseDirection, int index, int count)
    {
        if (!IsArcWeaponProfile(profile) || count <= 1)
            return baseTargetPoint;

        if (IsArtillerySuperProfile(profile))
        {
            if (index <= 0)
                return baseTargetPoint;

            float clusterRadius = Mathf.Max(0.08f, profile.AreaDamageRadius * 0.5f);
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * clusterRadius;
            return baseTargetPoint + randomOffset;
        }

        Vector2 source = transform.position;
        Vector2 sourceToTarget = baseTargetPoint - source;
        float targetDistance = sourceToTarget.magnitude;
        if (targetDistance <= 0.001f)
            return baseTargetPoint;

        Vector2 safeDirection = ResolveSafeAimDirection(baseDirection);
        float t = count == 1 ? 0f : index / (float)(count - 1);
        float centered = Mathf.Lerp(-1f, 1f, t);
        float angle = centered * profile.SpreadAngle * 0.75f;
        Vector2 rotatedDirection = (Quaternion.Euler(0f, 0f, angle) * safeDirection).normalized;

        float lateralSpacing = Mathf.Max(
            Mathf.Max(0.42f, profile.AreaDamageRadius * 1.5f),
            targetDistance * Mathf.Sin(Mathf.Abs(angle) * Mathf.Deg2Rad));

        Vector2 perpendicular = new Vector2(-safeDirection.y, safeDirection.x);
        float stagger = ((index % 2 == 0) ? -0.18f : 0.18f) * Mathf.Max(0.6f, profile.AreaDamageRadius);
        Vector2 landingPoint = source + rotatedDirection * targetDistance;
        landingPoint += perpendicular * centered * lateralSpacing * 0.28f;
        landingPoint += safeDirection * stagger;
        return landingPoint;
    }
}
