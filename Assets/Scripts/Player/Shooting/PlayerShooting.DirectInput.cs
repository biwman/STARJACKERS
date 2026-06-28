using UnityEngine;

public partial class PlayerShooting
{
    bool HandleDirectSimpleShooting()
    {
        if (!StarjackersInputModeManager.DirectShootingInputActive)
            return false;

        if (isReloading || currentAmmo <= 0)
            return true;

        if (!StarjackersInputModeManager.IsFireHeld())
            return true;

        Vector2 direction = ResolveDirectSimpleAimDirection();
        if (Time.time >= nextFireTime)
        {
            if (Shoot(direction.normalized, simpleAutoAimTargetViewId, simpleAutoAimHasTargetPoint, simpleAutoAimTargetPoint))
            {
                ConsumeAmmo();
                nextFireTime = Time.time + fireRate * GetFireIntervalMultiplier();
            }
        }

        return true;
    }

    Vector2 ResolveDirectSimpleAimDirection()
    {
        simpleAutoAimTargetViewId = 0;
        simpleAutoAimHasTargetPoint = false;
        simpleAutoAimTargetPoint = default;

        if (StarjackersInputModeManager.TryReadAimDirection(transform.position, out Vector2 direction, out Vector2 aimPoint, out bool hasWorldPoint))
        {
            if (hasWorldPoint)
            {
                simpleAutoAimHasTargetPoint = true;
                simpleAutoAimTargetPoint = aimPoint;
            }

            return ResolveSafeAimDirection(direction);
        }

        return FindAutoAimDirection(out simpleAutoAimTargetViewId, out simpleAutoAimTargetPoint, out simpleAutoAimHasTargetPoint);
    }

    void HandleDirectComplexShootingInput()
    {
        bool handledSuper = HandleDirectComplexSuperInput();
        if (!handledSuper)
            HandleDirectComplexNormalInput();
    }

    void HandleDirectComplexNormalInput()
    {
        WeaponAttackProfile profile = activeComplexWeaponProfile ?? SyncComplexWeaponProfile();
        if (profile == null)
            return;

        bool pressed = StarjackersInputModeManager.WasFirePressedThisFrame();
        bool held = StarjackersInputModeManager.IsFireHeld();
        bool released = StarjackersInputModeManager.WasFireReleasedThisFrame();

        if ((pressed || held) && !complexShootWasPressed)
            BeginDirectComplexNormalInput(profile);

        if (held && complexShootWasPressed)
        {
            UpdateDirectComplexNormalAim(profile);
            return;
        }

        if (!complexShootWasPressed)
            return;

        if (released || !held)
            ReleaseComplexNormalInput(profile);
    }

    void BeginDirectComplexNormalInput(WeaponAttackProfile profile)
    {
        complexShootWasPressed = true;
        complexShootPressStartedAt = Time.time - ComplexTapMaxDuration - 0.01f;
        complexShootMaxDragMagnitude = GetDirectManualMagnitude(profile);
        complexShootCanceledByCenteredAim = false;
        UpdateDirectComplexNormalAim(profile);
    }

    void UpdateDirectComplexNormalAim(WeaponAttackProfile profile)
    {
        ResolveDirectAimForProfile(profile, out Vector2 direction, out Vector2 targetPoint);
        complexLastAimDirection = direction;
        complexLastAimTargetPoint = targetPoint;
        complexShootMaxDragMagnitude = Mathf.Max(complexShootMaxDragMagnitude, GetDirectManualMagnitude(profile));

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

    bool HandleDirectComplexSuperInput()
    {
        WeaponAttackProfile normalProfile = activeComplexWeaponProfile ?? SyncComplexWeaponProfile();
        WeaponAttackProfile superProfile = WeaponAttackCatalog.GetSuperAttack(normalProfile != null ? normalProfile.Id : WeaponAttackCatalog.SimpleGunId);
        if (superProfile == null)
            return false;

        bool pressed = StarjackersInputModeManager.WasSuperPressedThisFrame();
        bool held = StarjackersInputModeManager.IsSuperHeld();
        bool released = StarjackersInputModeManager.WasSuperReleasedThisFrame();

        if (!IsSuperAttackReady)
        {
            if (complexSuperWasPressed)
            {
                complexSuperWasPressed = false;
                HideAimMarker();
            }

            return held || released;
        }

        if ((pressed || held) && !complexSuperWasPressed)
            BeginDirectComplexSuperInput(superProfile);

        if (held && complexSuperWasPressed)
        {
            UpdateDirectComplexSuperAim(superProfile);
            return true;
        }

        if (!complexSuperWasPressed)
            return held || released;

        if (released || !held)
            ReleaseDirectComplexSuperInput(superProfile);

        return true;
    }

    void BeginDirectComplexSuperInput(WeaponAttackProfile superProfile)
    {
        complexSuperWasPressed = true;
        complexSuperPressStartedAt = Time.time - ComplexTapMaxDuration - 0.01f;
        complexSuperMaxDragMagnitude = GetDirectManualMagnitude(superProfile);
        complexSuperCanceledByCenteredAim = false;
        UpdateDirectComplexSuperAim(superProfile);
    }

    void UpdateDirectComplexSuperAim(WeaponAttackProfile superProfile)
    {
        ResolveDirectAimForProfile(superProfile, out Vector2 direction, out Vector2 targetPoint);
        complexLastSuperAimDirection = direction;
        complexLastSuperAimTargetPoint = targetPoint;
        complexSuperMaxDragMagnitude = Mathf.Max(complexSuperMaxDragMagnitude, GetDirectManualMagnitude(superProfile));

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

    void ReleaseDirectComplexSuperInput(WeaponAttackProfile superProfile)
    {
        Vector2 direction = ResolveSafeAimDirection(complexLastSuperAimDirection);
        Vector2 targetPoint = complexLastSuperAimTargetPoint;
        int rocketTargetViewId = IsRocketWeaponProfile(superProfile) ? rocketLockedTargetViewId : 0;

        complexSuperWasPressed = false;
        complexSuperCanceledByCenteredAim = false;
        ResetRocketLockState();
        HideAimMarker();

        if (TryFireComplexAttack(superProfile, direction, targetPoint, false, true, rocketTargetViewId))
            superCharge = 0f;
    }

    void ResolveDirectAimForProfile(WeaponAttackProfile profile, out Vector2 direction, out Vector2 targetPoint)
    {
        Vector2 origin = transform.position;
        float range = GetComplexRangeWorld(profile);
        if (StarjackersInputModeManager.TryReadAimDirection(transform.position, out Vector2 inputDirection, out Vector2 worldPoint, out bool hasWorldPoint))
        {
            direction = ResolveSafeAimDirection(inputDirection);
            targetPoint = IsArcWeaponProfile(profile) && hasWorldPoint
                ? ClampAutoAimPointToRange(origin, worldPoint, range)
                : origin + (direction * range);
            return;
        }

        direction = ResolveSafeAimDirection(transform.up);
        targetPoint = origin + (direction * range);
    }

    float GetDirectManualMagnitude(WeaponAttackProfile profile)
    {
        return Mathf.Max(GetManualShootMarkerThreshold(profile), ComplexTapMaxDragMagnitude + 0.01f);
    }

    void HandleDirectUtilityInput()
    {
        if (!StarjackersInputModeManager.DirectGameplayInputActive)
            return;

        if (StarjackersInputModeManager.WasWeaponSwitchPressedThisFrame())
            SwitchComplexWeapon();

        HandleDirectGadgetInput();
    }

    void HandleDirectGadgetInput()
    {
        int count = Mathf.Min(activeGadgetItemIds.Count, StarjackersInputModeManager.GadgetHotkeyCount);
        for (int i = 0; i < count; i++)
        {
            string itemId = activeGadgetItemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (StarjackersInputModeManager.WasGadgetPressedThisFrame(i))
            {
                if (IsHoldGadget(itemId))
                    BeginGadgetUse(itemId);
                else
                    TriggerGadgetUse(itemId);
            }

            if (StarjackersInputModeManager.WasGadgetReleasedThisFrame(i) && IsHoldGadget(itemId))
                EndGadgetUse(itemId);
        }
    }
}
