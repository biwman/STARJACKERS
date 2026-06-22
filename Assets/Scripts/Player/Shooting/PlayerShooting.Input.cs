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
    void HandleComplexNormalInput()
    {
        if (shootJoystick == null)
            return;

        WeaponAttackProfile profile = activeComplexWeaponProfile ?? SyncComplexWeaponProfile();
        if (profile == null)
            return;

        if (shootJoystick.IsPressed)
        {
            if (!complexShootWasPressed)
            {
                complexShootWasPressed = true;
                complexShootPressStartedAt = Time.time;
                complexShootMaxDragMagnitude = 0f;
                complexShootCanceledByCenteredAim = false;
                complexLastAimDirection = transform.up;
                complexLastAimTargetPoint = (Vector2)transform.position + ((Vector2)transform.up * GetComplexRangeWorld(profile));
            }

            Vector2 raw = GetCurrentShootRawInput();
            float markerThreshold = GetManualShootMarkerThreshold(profile);
            complexShootMaxDragMagnitude = Mathf.Max(complexShootMaxDragMagnitude, raw.magnitude);
            if (raw.magnitude >= markerThreshold)
            {
                complexShootCanceledByCenteredAim = false;
                complexLastAimDirection = raw.normalized;
                if (IsArcWeaponProfile(profile))
                    complexLastAimTargetPoint = ResolveArcTargetPointFromInput(profile, raw);

                Color? markerColorOverride = null;
                if (IsRocketWeaponProfile(profile))
                {
                    UpdateRocketLockState(profile, complexLastAimDirection);
                    if (rocketLockedTargetViewId > 0)
                        markerColorOverride = RocketLockedMarkerColor;
                }
                else
                {
                    ResetRocketLockState();
                }

                ShowAimMarker(profile, complexLastAimDirection, IsArcWeaponProfile(profile) ? complexLastAimTargetPoint : (Vector2?)null, markerColorOverride);
            }
            else
            {
                if (complexShootMaxDragMagnitude >= markerThreshold)
                    complexShootCanceledByCenteredAim = true;
                ResetRocketLockState();
                HideAimMarker();
            }
            return;
        }

        if (!complexShootWasPressed)
            return;

        ReleaseComplexNormalInput(profile);
    }

    void ReleaseComplexNormalInput(WeaponAttackProfile profile)
    {
        if (complexShootCanceledByCenteredAim)
        {
            complexShootWasPressed = false;
            complexShootCanceledByCenteredAim = false;
            ResetRocketLockState();
            HideAimMarker();
            return;
        }

        float markerThreshold = GetManualShootMarkerThreshold(profile);
        bool hadMarker = complexShootMaxDragMagnitude >= markerThreshold;
        bool wasTap = IsAdvancedShootJoystickEnabled()
            ? complexShootMaxDragMagnitude <= ComplexTapMaxDragMagnitude
            : Time.time - complexShootPressStartedAt <= ComplexTapMaxDuration &&
              complexShootMaxDragMagnitude <= ComplexTapMaxDragMagnitude;
        bool useAutoAim = !hadMarker || wasTap;

        Vector2 direction;
        Vector2 targetPoint;
        int autoAimTargetViewId = 0;
        if (useAutoAim)
        {
            ResolveComplexAutoAim(profile, out direction, out targetPoint, out autoAimTargetViewId);
        }
        else
        {
            direction = ResolveSafeAimDirection(complexLastAimDirection);
            targetPoint = complexLastAimTargetPoint;
        }

        int rocketTargetViewId = IsRocketWeaponProfile(profile)
            ? (useAutoAim ? autoAimTargetViewId : rocketLockedTargetViewId)
            : 0;

        complexShootWasPressed = false;
        complexShootCanceledByCenteredAim = false;
        ResetRocketLockState();
        HideAimMarker();
        TryFireComplexAttack(profile, direction, targetPoint, true, false, rocketTargetViewId);
    }

    Vector2 GetCurrentShootRawInput()
    {
        if (shootJoystick == null)
            return Vector2.zero;

        return shootJoystick.rawInputVector.sqrMagnitude > 0.0001f
            ? shootJoystick.rawInputVector
            : shootJoystick.inputVector;
    }

    Vector2 GetCurrentSuperRawInput()
    {
        if (superJoystick == null)
            return Vector2.zero;

        return superJoystick.rawInputVector.sqrMagnitude > 0.0001f
            ? superJoystick.rawInputVector
            : superJoystick.inputVector;
    }

    float GetManualShootMarkerThreshold(WeaponAttackProfile profile)
    {
        if (!IsAdvancedShootJoystickEnabled())
            return ManualAimThreshold;

        if (IsArcWeaponProfile(profile))
            return AdvancedShootMarkerRawThreshold;

        return ComplexTapMaxDragMagnitude;
    }

    bool IsRocketWeaponProfile(WeaponAttackProfile profile)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.Id))
            return false;

        return string.Equals(profile.Id, WeaponAttackCatalog.RocketLauncherId, StringComparison.Ordinal) ||
               string.Equals(profile.Id, WeaponAttackCatalog.DoubleRocketLauncherId, StringComparison.Ordinal) ||
               profile.Id.StartsWith(WeaponAttackCatalog.RocketLauncherId + "_", StringComparison.Ordinal) ||
               profile.Id.StartsWith(WeaponAttackCatalog.DoubleRocketLauncherId + "_", StringComparison.Ordinal);
    }

    bool IsDoubleRocketLauncherProfile(WeaponAttackProfile profile)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.Id))
            return false;

        return string.Equals(profile.Id, WeaponAttackCatalog.DoubleRocketLauncherId, StringComparison.Ordinal) ||
               profile.Id.StartsWith(WeaponAttackCatalog.DoubleRocketLauncherId + "_", StringComparison.Ordinal);
    }

    bool IsPulseDisruptorSuperProfile(WeaponAttackProfile profile)
    {
        return profile != null &&
               string.Equals(profile.Id, WeaponAttackCatalog.PulseDisruptorId + "_super", StringComparison.Ordinal);
    }

    bool IsGatlingGunProfile(WeaponAttackProfile profile)
    {
        return profile != null &&
               !string.IsNullOrWhiteSpace(profile.Id) &&
               (string.Equals(profile.Id, WeaponAttackCatalog.GatlingGunId, StringComparison.Ordinal) ||
                profile.Id.StartsWith(WeaponAttackCatalog.GatlingGunId + "_", StringComparison.Ordinal));
    }

    void UpdateRocketLockState(WeaponAttackProfile profile, Vector2 direction)
    {
        if (!IsRocketWeaponProfile(profile))
        {
            ResetRocketLockState();
            return;
        }

        direction = ResolveSafeAimDirection(direction);
        if (rocketLockedTargetViewId > 0)
        {
            if (IsRocketLockStillValid(profile, direction, rocketLockedTargetViewId))
                return;

            ResetRocketLockState();
        }

        int candidateViewId = FindRocketLockCandidateViewId(profile, direction);
        if (candidateViewId <= 0)
        {
            ResetRocketLockState();
            return;
        }

        if (candidateViewId != rocketLockCandidateViewId)
        {
            rocketLockCandidateViewId = candidateViewId;
            rocketLockStartedAt = Time.time;
            rocketLockFeedbackPlayed = false;
            return;
        }

        if (Time.time - rocketLockStartedAt < GetAdjustedRocketLockDuration())
            return;

        rocketLockedTargetViewId = candidateViewId;
        if (!rocketLockFeedbackPlayed)
        {
            rocketLockFeedbackPlayed = true;
            AudioManager.Instance.PlayRocketLockAt(transform.position);
        }
    }

    int FindRocketLockCandidateViewId(WeaponAttackProfile profile, Vector2 direction)
    {
        direction = ResolveSafeAimDirection(direction);
        float range = GetComplexRangeWorld(profile);
        Vector2 origin = transform.position;
        PlayerHealth[] targets = RuntimeSceneQueryCache.GetPlayers();
        PlayerHealth bestTarget = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < targets.Length; i++)
        {
            PlayerHealth target = targets[i];
            if (!IsValidRocketLockTarget(target))
                continue;

            Vector2 targetPoint = GetAutoAimTargetPoint(target);
            Vector2 toTarget = targetPoint - origin;
            float projection = Vector2.Dot(toTarget, direction);
            if (projection < 0.35f || projection > range)
                continue;

            float lateralDistance = Mathf.Abs((direction.x * toTarget.y) - (direction.y * toTarget.x));
            float lockRadius = ResolveRocketLockRadius(target);
            if (lateralDistance > lockRadius)
                continue;

            if (IsLineBlockedByObstacle(origin, targetPoint, target.transform))
                continue;

            float score = (lateralDistance * 4f) + (projection * 0.035f);
            if (score >= bestScore)
                continue;

            bestScore = score;
            bestTarget = target;
        }

        return bestTarget != null && bestTarget.photonView != null ? bestTarget.photonView.ViewID : 0;
    }

    bool IsRocketLockStillValid(WeaponAttackProfile profile, Vector2 direction, int targetViewId)
    {
        PhotonView targetView = targetViewId > 0 ? PhotonView.Find(targetViewId) : null;
        PlayerHealth target = targetView != null ? targetView.GetComponent<PlayerHealth>() : null;
        if (!IsValidRocketLockTarget(target))
            return false;

        Vector2 targetPoint = GetAutoAimTargetPoint(target);
        Vector2 toTarget = targetPoint - (Vector2)transform.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f || distance > GetComplexRangeWorld(profile) * 1.15f)
            return false;

        float angle = Vector2.Angle(ResolveSafeAimDirection(direction), toTarget / distance);
        if (angle > RocketLockBreakAngle)
            return false;

        return !IsLineBlockedByObstacle(transform.position, targetPoint, target.transform);
    }

    bool IsValidRocketLockTarget(PlayerHealth target)
    {
        return IsValidAutoAimTarget(target);
    }

    float ResolveRocketLockRadius(PlayerHealth target)
    {
        Collider2D targetCollider = target != null ? target.GetComponentInChildren<Collider2D>() : null;
        if (targetCollider == null)
            return RocketLockLineRadius;

        Bounds bounds = targetCollider.bounds;
        return Mathf.Max(RocketLockLineRadius, Mathf.Max(bounds.extents.x, bounds.extents.y) * 0.9f);
    }

    float GetAdjustedRocketLockDuration()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return RocketLockDuration * (PilotCatalog.IsSelectedPilot(owner, PilotCatalog.VectorId) ? VectorRocketLockDurationMultiplier : 1f);
    }

    void ResetRocketLockState()
    {
        rocketLockCandidateViewId = 0;
        rocketLockedTargetViewId = 0;
        rocketLockStartedAt = 0f;
        rocketLockFeedbackPlayed = false;
    }

    bool HandleComplexSuperInput()
    {
        if (superJoystick == null)
            return false;

        WeaponAttackProfile normalProfile = activeComplexWeaponProfile ?? SyncComplexWeaponProfile();
        WeaponAttackProfile superProfile = WeaponAttackCatalog.GetSuperAttack(normalProfile != null ? normalProfile.Id : WeaponAttackCatalog.SimpleGunId);
        if (superProfile == null)
            return false;

        if (!IsSuperAttackReady)
        {
            if (complexSuperWasPressed)
            {
                complexSuperWasPressed = false;
                HideAimMarker();
            }
            return false;
        }

        if (superJoystick.IsPressed)
        {
            if (!complexSuperWasPressed)
            {
                complexSuperWasPressed = true;
                complexSuperPressStartedAt = Time.time;
                complexSuperMaxDragMagnitude = 0f;
                complexSuperCanceledByCenteredAim = false;
                complexLastSuperAimDirection = transform.up;
                complexLastSuperAimTargetPoint = (Vector2)transform.position + ((Vector2)transform.up * GetComplexRangeWorld(superProfile));
            }

            Vector2 raw = GetCurrentSuperRawInput();
            complexSuperMaxDragMagnitude = Mathf.Max(complexSuperMaxDragMagnitude, raw.magnitude);
            if (raw.magnitude >= ManualAimThreshold)
            {
                complexSuperCanceledByCenteredAim = false;
                complexLastSuperAimDirection = raw.normalized;
                if (IsArcWeaponProfile(superProfile))
                    complexLastSuperAimTargetPoint = ResolveArcTargetPointFromInput(superProfile, raw);

                Color? markerColorOverride = null;
                if (IsRocketWeaponProfile(superProfile))
                {
                    UpdateRocketLockState(superProfile, complexLastSuperAimDirection);
                    if (rocketLockedTargetViewId > 0)
                        markerColorOverride = RocketLockedMarkerColor;
                }
                else
                {
                    ResetRocketLockState();
                }

                ShowAimMarker(superProfile, complexLastSuperAimDirection, IsArcWeaponProfile(superProfile) ? complexLastSuperAimTargetPoint : (Vector2?)null, markerColorOverride);
            }
            else
            {
                if (complexSuperMaxDragMagnitude >= ManualAimThreshold)
                    complexSuperCanceledByCenteredAim = true;
                ResetRocketLockState();
                HideAimMarker();
            }
            return true;
        }

        if (!complexSuperWasPressed)
            return false;

        if (complexSuperCanceledByCenteredAim)
        {
            complexSuperWasPressed = false;
            complexSuperCanceledByCenteredAim = false;
            ResetRocketLockState();
            HideAimMarker();
            return true;
        }

        bool hadMarker = complexSuperMaxDragMagnitude >= ManualAimThreshold;
        bool wasTap = Time.time - complexSuperPressStartedAt <= ComplexTapMaxDuration &&
                      complexSuperMaxDragMagnitude <= ComplexTapMaxDragMagnitude;
        bool useAutoAim = !hadMarker || wasTap;

        Vector2 direction;
        Vector2 targetPoint;
        int autoAimTargetViewId = 0;
        if (useAutoAim)
        {
            ResolveComplexAutoAim(superProfile, out direction, out targetPoint, out autoAimTargetViewId);
        }
        else
        {
            direction = ResolveSafeAimDirection(complexLastSuperAimDirection);
            targetPoint = complexLastSuperAimTargetPoint;
        }

        int rocketTargetViewId = IsRocketWeaponProfile(superProfile)
            ? (useAutoAim ? autoAimTargetViewId : rocketLockedTargetViewId)
            : 0;

        complexSuperWasPressed = false;
        complexSuperCanceledByCenteredAim = false;
        ResetRocketLockState();
        HideAimMarker();
        if (TryFireComplexAttack(superProfile, direction, targetPoint, false, true, rocketTargetViewId))
            superCharge = 0f;

        return true;
    }

    void ResetComplexPressState()
    {
        complexShootWasPressed = false;
        complexSuperWasPressed = false;
        complexShootCanceledByCenteredAim = false;
        complexSuperCanceledByCenteredAim = false;
        ResetRocketLockState();
    }

    WeaponAttackProfile SyncComplexWeaponProfile()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        string signature = BuildComplexWeaponSignature(equipmentSlots, shipSkinIndex);
        if (signature == lastAppliedComplexWeaponSignature && activeComplexWeaponProfile != null)
        {
            SyncActiveComplexAmmoMirror();
            return activeComplexWeaponProfile;
        }

        List<ComplexWeaponRuntimeState> previousStates = new List<ComplexWeaponRuntimeState>(complexWeaponStates);
        complexWeaponStates.Clear();
        BuildComplexWeaponStates(equipmentSlots, shipSkinIndex, previousStates);
        activeComplexWeaponIndex = Mathf.Clamp(activeComplexWeaponIndex, 0, Mathf.Max(0, complexWeaponStates.Count - 1));
        activeComplexWeaponProfile = GetActiveComplexWeaponState()?.Profile;
        lastAppliedComplexWeaponSignature = signature;
        isReloading = false;
        reloadFinishTime = 0f;
        SyncActiveComplexAmmoMirror();
        return activeComplexWeaponProfile;
    }
}
