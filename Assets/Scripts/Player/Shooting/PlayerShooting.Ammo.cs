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
    void ConsumeAmmo()
    {
        currentAmmo = Mathf.Max(0, currentAmmo - 1);
        if (currentAmmo <= 0)
        {
            StartReload(false);
        }
    }

    public int DrainInvaderAssimilationAmmo(float amount)
    {
        if (!photonView.IsMine || amount <= 0f || infiniteAmmo)
            return 0;

        invaderAssimilationAmmoDrainAccumulator += amount;
        int requested = Mathf.FloorToInt(invaderAssimilationAmmoDrainAccumulator);
        if (requested <= 0)
            return 0;

        int drained = 0;
        if (IsComplexShootingActive)
        {
            ComplexWeaponRuntimeState state = GetActiveComplexWeaponState();
            if (state != null && state.CurrentAmmo > 0)
            {
                drained = Mathf.Min(requested, state.CurrentAmmo);
                state.CurrentAmmo = Mathf.Max(0, state.CurrentAmmo - drained);
                if (state.CurrentAmmo <= 0 && state.NextAmmoAt <= 0f)
                {
                    state.AmmoReloadStartedAt = Time.time;
                    state.NextAmmoAt = Time.time + GetAdjustedAmmoReloadTime(state.Profile, false);
                }

                SyncActiveComplexAmmoMirror();
            }
        }
        else if (currentAmmo > 0)
        {
            drained = Mathf.Min(requested, currentAmmo);
            currentAmmo = Mathf.Max(0, currentAmmo - drained);
            if (currentAmmo <= 0)
                StartReload(false);
        }

        if (drained > 0)
            invaderAssimilationAmmoDrainAccumulator = Mathf.Max(0f, invaderAssimilationAmmoDrainAccumulator - drained);
        else if (CurrentAmmo <= 0)
            invaderAssimilationAmmoDrainAccumulator = Mathf.Min(invaderAssimilationAmmoDrainAccumulator, 0.95f);

        return drained;
    }

    void StartReload(bool playSound)
    {
        if (isReloading)
            return;

        isReloading = true;
        reloadFinishTime = Time.time + GetAdjustedSimpleReloadDuration();

        if (playSound)
        {
            photonView.RPC(nameof(PlayReloadSfx), RpcTarget.All);
        }
    }

    void UpdateReload()
    {
        if (!isReloading)
            return;

        if (Time.time < reloadFinishTime)
            return;

        isReloading = false;
        currentAmmo = maxAmmo;
    }

    void SyncAmmoSetting()
    {
        if (IsNeutralRiderShip() || (customAmmoProfileActive && IsEnemyBotShip()))
            return;

        int configuredAmmo = GetConfiguredMaxAmmo();
        if (configuredAmmo == maxAmmo)
            return;

        int previousMaxAmmo = maxAmmo;
        maxAmmo = configuredAmmo;

        if (isReloading)
            return;

        if (currentAmmo == previousMaxAmmo)
        {
            currentAmmo = maxAmmo;
        }
        else
        {
            currentAmmo = Mathf.Min(currentAmmo, maxAmmo);
        }
    }

    void CaptureBaseWeaponProfile()
    {
        if (baseWeaponProfileCaptured || IsEnemyBotShip() || IsNeutralRiderShip())
            return;

        baseBulletSpeed = bulletSpeed;
        baseFireRate = fireRate;
        baseReloadDuration = reloadDuration;
        baseBulletDamage = bulletDamage;
        baseBulletScaleMultiplier = bulletScaleMultiplier;
        baseBulletColor = bulletColor;
        baseMuzzleOffsetDistance = muzzleOffsetDistance;
        baseBulletRangeMultiplier = bulletRangeMultiplier;
        baseInfiniteAmmo = infiniteAmmo;
        baseShotSoundId = shotSoundId ?? string.Empty;
        baseWeaponProfileCaptured = true;
    }

    void SyncEquippedWeaponProfile()
    {
        if (!photonView.IsMine || IsEnemyBotShip() || IsNeutralRiderShip())
            return;

        CaptureBaseWeaponProfile();
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        string weaponId = WeaponAttackCatalog.GetPrimaryWeaponId(equipmentSlots, shipSkinIndex);
        int matchingWeaponCount = CountEquippedWeapon(equipmentSlots, shipSkinIndex, weaponId);
        string signature = shipSkinIndex + ":" + weaponId + "x" + matchingWeaponCount;
        if (signature == lastAppliedWeaponSignature)
            return;

        WeaponAttackProfile profile = WeaponAttackCatalog.GetDefaultNormalAttackByWeaponId(weaponId);
        ApplyPlayerWeaponProfile(profile, matchingWeaponCount);
        lastAppliedWeaponSignature = signature;
    }

    int CountEquippedWeapon(string[] equipmentSlots, int shipSkinIndex, string weaponId)
    {
        if (equipmentSlots == null || string.IsNullOrWhiteSpace(weaponId) || string.Equals(weaponId, WeaponAttackCatalog.SimpleGunId, StringComparison.Ordinal))
            return 0;

        int count = 0;
        for (int slot = 0; slot < 2; slot++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(slot, shipSkinIndex))
                continue;

            if (string.Equals(WeaponAttackCatalog.GetWeaponIdForItem(GetEquipmentItem(equipmentSlots, slot)), weaponId, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    static string GetEquipmentItem(string[] equipmentSlots, int index)
    {
        return equipmentSlots != null && index >= 0 && index < equipmentSlots.Length
            ? equipmentSlots[index]
            : null;
    }

    void ApplyPlayerWeaponProfile(WeaponAttackProfile profile, int matchingWeaponCount)
    {
        if (!baseWeaponProfileCaptured)
            return;

        activeSimpleWeaponProfile = profile;
        simpleUsesDamageProfile = false;
        simpleShieldDamage = baseBulletDamage;
        simpleHpDamage = baseBulletDamage;
        simplePierces = false;
        simpleAreaDamageRadius = 0f;
        simpleHitEffectId = string.Empty;
        simpleFlightTime = 10f;
        simpleDamageType = WeaponDamageType.Laser;
        simpleDeliveryMethod = WeaponDeliveryMethod.DirectProjectile;
        simpleDeliveryFlags = WeaponDeliveryFlags.None;

        if (profile != null && !string.Equals(profile.Id, WeaponAttackCatalog.SimpleGunId, StringComparison.Ordinal))
        {
            fireRate = Mathf.Max(0.05f, profile.AttackCooldown);
            reloadDuration = baseReloadDuration;
            bulletDamage = Mathf.Max(1, Mathf.Max(profile.HpDamage, profile.ShieldDamage));
            bulletScaleMultiplier = Mathf.Max(0.2f, profile.ProjectileSize);
            bulletColor = profile.ProjectileColor;
            muzzleOffsetDistance = baseMuzzleOffsetDistance;
            bulletRangeMultiplier = Mathf.Max(0.25f, profile.RangeMultiplier);
            infiniteAmmo = baseInfiniteAmmo;
            bulletSpeed = Mathf.Max(0.5f, profile.ProjectileSpeed);
            shotSoundId = profile.ShotSoundId ?? string.Empty;
            multiShotCount = ShouldUseDualMuzzles(profile, matchingWeaponCount) ? 2 : 1;
            simpleUsesDamageProfile = true;
            simpleShieldDamage = Mathf.Max(0, profile.ShieldDamage);
            simpleHpDamage = Mathf.Max(0, profile.HpDamage);
            simplePierces = profile.Pierces;
            simpleAreaDamageRadius = Mathf.Max(0f, profile.AreaDamageRadius);
            simpleHitEffectId = profile.HitEffectId ?? string.Empty;
            simpleFlightTime = Mathf.Clamp(profile.FlightTime, 0.2f, 30f);
            simpleDamageType = profile.DamageType;
            simpleDeliveryMethod = profile.DeliveryMethod;
            simpleDeliveryFlags = profile.DeliveryFlags;
            return;
        }

        fireRate = baseFireRate;
        reloadDuration = baseReloadDuration;
        bulletDamage = baseBulletDamage;
        bulletScaleMultiplier = baseBulletScaleMultiplier;
        bulletColor = baseBulletColor;
        muzzleOffsetDistance = baseMuzzleOffsetDistance;
        bulletRangeMultiplier = baseBulletRangeMultiplier;
        infiniteAmmo = baseInfiniteAmmo;
        bulletSpeed = baseBulletSpeed;
        shotSoundId = baseShotSoundId;
        multiShotCount = 1;
        simpleUsesDamageProfile = true;
        simpleShieldDamage = bulletDamage;
        simpleHpDamage = bulletDamage;
        simplePierces = false;
        simpleAreaDamageRadius = 0f;
        simpleHitEffectId = Bullet.SimpleBoltEffectId;
        simpleFlightTime = 10f;
        simpleDamageType = WeaponDamageType.Laser;
        simpleDeliveryMethod = WeaponDeliveryMethod.DirectProjectile;
        simpleDeliveryFlags = WeaponDeliveryFlags.None;
    }

    public void ConfigureBotWeaponAttackProfile(WeaponAttackProfile profile, float configuredMuzzleOffsetDistance, int muzzleStreamCount = 1, bool configuredInfiniteAmmo = false)
    {
        WeaponAttackProfile safeProfile = profile != null
            ? profile.Clone()
            : WeaponAttackCatalog.GetNormalAttackByWeaponId(WeaponAttackCatalog.SimpleGunId);

        activeSimpleWeaponProfile = safeProfile;
        customAmmoProfileActive = true;
        fireRate = Mathf.Max(0.05f, safeProfile.AttackCooldown);
        maxAmmo = Mathf.Max(1, safeProfile.MaxAmmo);
        reloadDuration = Mathf.Max(0f, safeProfile.AmmoReloadTime);
        bulletDamage = Mathf.Max(1, Mathf.Max(safeProfile.HpDamage, safeProfile.ShieldDamage));
        bulletScaleMultiplier = Mathf.Max(0.2f, safeProfile.ProjectileSize);
        bulletColor = safeProfile.ProjectileColor;
        muzzleOffsetDistance = Mathf.Max(0f, configuredMuzzleOffsetDistance);
        infiniteAmmo = configuredInfiniteAmmo;
        bulletSpeed = Mathf.Max(0.1f, safeProfile.ProjectileSpeed);
        bulletRangeMultiplier = Mathf.Max(0.25f, safeProfile.RangeMultiplier);
        shotSoundId = safeProfile.ShotSoundId ?? string.Empty;
        multiShotCount = Mathf.Clamp(muzzleStreamCount, 1, 2);

        simpleUsesDamageProfile = true;
        simpleShieldDamage = Mathf.Max(0, safeProfile.ShieldDamage);
        simpleHpDamage = Mathf.Max(0, safeProfile.HpDamage);
        simplePierces = safeProfile.Pierces;
        simpleAreaDamageRadius = Mathf.Max(0f, safeProfile.AreaDamageRadius);
        simpleHitEffectId = safeProfile.HitEffectId ?? string.Empty;
        simpleFlightTime = Mathf.Clamp(safeProfile.FlightTime, 0.2f, 30f);
        simpleDamageType = safeProfile.DamageType;
        simpleDeliveryMethod = safeProfile.DeliveryMethod;
        simpleDeliveryFlags = safeProfile.DeliveryFlags;

        isReloading = false;
        reloadFinishTime = 0f;
        currentAmmo = maxAmmo;
        nextFireTime = 0f;
    }

    public bool TryExecuteNeutralRiderBotItem(string itemId)
    {
        if (!IsNeutralRiderShip() || !photonView.IsMine || !IsGameStarted() || !PhotonNetwork.IsMasterClient || string.IsNullOrWhiteSpace(itemId))
            return false;

        if (string.Equals(itemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return TryDeployGadgetMine();

        if (string.Equals(itemId, InventoryItemCatalog.SpaceBombId, StringComparison.Ordinal))
            return NewItemsRuntime.TryDeploySpaceBomb(this);

        if (string.Equals(itemId, InventoryItemCatalog.StasisBuoyId, StringComparison.Ordinal))
            return NewItemsRuntime.TryDeployStasisBuoy(this);

        if (string.Equals(itemId, InventoryItemCatalog.SpaceTorpedoId, StringComparison.Ordinal))
            return NewItemsRuntime.TryLaunchSpaceTorpedo(this);

        if (string.Equals(itemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal))
            return NewItemsRuntime.TryDeployAutoTurret(this);

        if (string.Equals(itemId, InventoryItemCatalog.RocketAutoTurretId, StringComparison.Ordinal))
            return NewItemsRuntime.TryDeployRocketAutoTurret(this);

        return false;
    }

    bool ShouldUseDualMuzzles(WeaponAttackProfile profile, int matchingWeaponCount)
    {
        if (profile == null)
            return false;

        if (matchingWeaponCount >= 2)
            return true;

        return string.Equals(profile.Id, WeaponAttackCatalog.DoubleIonizerId, StringComparison.Ordinal);
    }

    void SyncEquippedGadgetProfile()
    {
        if (!photonView.IsMine || IsEnemyBotShip() || IsNeutralRiderShip())
            return;

        RefreshAuthoritativeGadgetChargeCache();
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        List<string> orderedItems = new List<string>();
        Dictionary<string, int> gadgetCounts = CollectEquippedGadgetCounts(equipmentSlots, shipSkinIndex, orderedItems);
        string signature = BuildGadgetSignature(shipSkinIndex, orderedItems, gadgetCounts);
        if (signature == lastAppliedGadgetSignature)
            return;

        activeGadgetItemIds.Clear();
        gadgetStates.Clear();
        for (int i = 0; i < orderedItems.Count; i++)
        {
            string itemId = orderedItems[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            int equippedCount = gadgetCounts.TryGetValue(itemId, out int count) ? count : 0;
            int maxCharges = ResolveGadgetMaxCharges(itemId, equippedCount);
            if (maxCharges <= 0)
                continue;

            int remainingCharges = authoritativeGadgetCharges.TryGetValue(itemId, out int authoritativeRemaining)
                ? Mathf.Clamp(authoritativeRemaining, 0, maxCharges)
                : maxCharges;

            GadgetRuntimeState state = new GadgetRuntimeState
            {
                ItemId = itemId,
                MaxCharges = maxCharges,
                RemainingCharges = remainingCharges,
                Cooldown = ResolveGadgetCooldown(itemId),
                NextUseTime = 0f
            };

            activeGadgetItemIds.Add(itemId);
            gadgetStates[itemId] = state;
        }

        lastAppliedGadgetSignature = signature;
    }

    void RefreshAuthoritativeGadgetRuntimeStates()
    {
        if (!photonView.IsMine || gadgetStates.Count == 0)
            return;

        RefreshAuthoritativeGadgetChargeCache();
        foreach (KeyValuePair<string, GadgetRuntimeState> pair in gadgetStates)
        {
            GadgetRuntimeState state = pair.Value;
            if (state == null)
                continue;

            if (authoritativeGadgetCharges.TryGetValue(pair.Key, out int remainingCharges))
                state.RemainingCharges = Mathf.Clamp(remainingCharges, 0, Mathf.Max(0, state.MaxCharges));
            else
                state.RemainingCharges = Mathf.Max(0, state.MaxCharges);
        }
    }

    void RefreshAuthoritativeGadgetChargeCache()
    {
        string rawState = GetAuthoritativeGadgetChargeStateRaw();
        if (string.Equals(rawState, lastAuthoritativeGadgetChargeStateRaw, StringComparison.Ordinal))
            return;

        lastAuthoritativeGadgetChargeStateRaw = rawState;
        authoritativeGadgetCharges.Clear();
        ParseAuthoritativeGadgetChargesForActor(
            rawState,
            photonView != null && photonView.Owner != null ? photonView.Owner.ActorNumber : (PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1),
            authoritativeGadgetCharges);
    }

    string GetAuthoritativeGadgetChargeStateRaw()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.GadgetChargesStateKey, out object value) &&
            value is string serializedState)
        {
            return serializedState;
        }

        return string.Empty;
    }

    Dictionary<string, int> CollectEquippedGadgetCounts(string[] equipmentSlots, int shipSkinIndex, List<string> orderedItems)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (equipmentSlots == null)
            return counts;

        for (int i = 0; i < equipmentSlots.Length; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            InventoryItemCategory category = InventoryItemCatalog.GetEquipmentSlotCategory(i);
            if (category != InventoryItemCategory.Gadget && category != InventoryItemCategory.Support)
                continue;

            string itemId = GetEquipmentItem(equipmentSlots, i);
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!counts.ContainsKey(itemId))
            {
                counts[itemId] = 0;
                orderedItems?.Add(itemId);
            }

            counts[itemId]++;
        }

        for (int i = 4; i <= 5; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            string itemId = GetEquipmentItem(equipmentSlots, i);
            if (!string.Equals(itemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
                continue;

            if (!counts.ContainsKey(itemId))
            {
                counts[itemId] = 0;
                orderedItems?.Add(itemId);
            }

            counts[itemId]++;
        }

        return counts;
    }

    static string BuildGadgetSignature(int shipSkinIndex, List<string> orderedItems, Dictionary<string, int> counts)
    {
        if (orderedItems == null || orderedItems.Count == 0)
            return shipSkinIndex + ":none";

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.Append(shipSkinIndex);
        builder.Append(':');
        for (int i = 0; i < orderedItems.Count; i++)
        {
            string itemId = orderedItems[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (builder[builder.Length - 1] != ':')
                builder.Append('|');

            builder.Append(itemId);
            builder.Append('x');
            builder.Append(counts != null && counts.TryGetValue(itemId, out int count) ? count : 0);
        }

        return builder.ToString();
    }

    bool ShouldFireFromDualWingMuzzles()
    {
        return multiShotCount >= 2;
    }

    Vector3 GetWingMuzzlePosition(float side)
    {
        float lateralOffset = GetWingMuzzleOffset();
        float forwardOffset = Mathf.Max(0.18f, muzzleOffsetDistance * 0.7f);
        return transform.position + (transform.up * forwardOffset) + (transform.right * (lateralOffset * side));
    }

    float GetWingMuzzleOffset()
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            float halfWidth = spriteRenderer.sprite.bounds.extents.x * Mathf.Abs(spriteRenderer.transform.lossyScale.x);
            return Mathf.Max(0.35f, halfWidth * 0.82f);
        }

        Collider2D collider2D = GetComponentInChildren<Collider2D>();
        if (collider2D != null)
            return Mathf.Max(0.35f, collider2D.bounds.extents.x * 0.72f);

        return 0.55f;
    }

    bool SpawnBullet(Vector2 direction, Vector3 spawnPos, int ownerId, bool pilotBreachProjectile = false)
    {
        float projectileSpeedMultiplier = GetProjectileSpeedMultiplier();
        float adjustedBulletSpeed = Mathf.Max(0.1f, bulletSpeed * projectileSpeedMultiplier);
        float adjustedSimpleFlightTime = simpleFlightTime / Mathf.Max(0.1f, projectileSpeedMultiplier);
        object[] data = simpleUsesDamageProfile
            ? new object[]
            {
                ownerId,
                bulletDamage,
                bulletScaleMultiplier,
                bulletColor.r,
                bulletColor.g,
                bulletColor.b,
                bulletColor.a,
                bulletRangeMultiplier,
                simpleShieldDamage,
                simpleHpDamage,
                simplePierces,
                simpleAreaDamageRadius,
                simpleHitEffectId ?? string.Empty,
                adjustedSimpleFlightTime,
                pilotBreachProjectile ? PilotBreachProjectileMarker : string.Empty
            }
            : new object[]
            {
                ownerId,
                bulletDamage,
                bulletScaleMultiplier,
                bulletColor.r,
                bulletColor.g,
                bulletColor.b,
                bulletColor.a,
                bulletRangeMultiplier,
                pilotBreachProjectile ? PilotBreachProjectileMarker : string.Empty
            };
        data = Bullet.AppendWeaponMetadata(data, simpleDamageType, simpleDeliveryMethod, simpleDeliveryFlags);

        Collider2D playerCollider = GetComponent<Collider2D>();
        GameObject bullet = ProjectileSpawner.SpawnNetworkBullet(
            bulletPrefab,
            spawnPos,
            Quaternion.identity,
            data,
            ownerId,
            direction * adjustedBulletSpeed,
            true,
            playerCollider);

        if (bullet == null)
        {
            Debug.LogError("Bullet failed to spawn");
            return false;
        }

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("Bullet is missing Rigidbody2D");
        }

        return true;
    }

    bool TryConsumePilotBreachSalvo()
    {
        PilotActiveAbilityController pilotAbility = GetComponent<PilotActiveAbilityController>();
        return pilotAbility != null && pilotAbility.TryConsumeSirNowitzkyBreachSalvo();
    }

    bool IsGameStarted()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value))
        {
            return (bool)value;
        }

        return false;
    }

    public bool CanUseAdvancedShootJoystick()
    {
        return photonView != null &&
               photonView.IsMine &&
               IsGameStarted() &&
               IsComplexShootingActive &&
               !RoundChatCommandUI.IsLocalChatMenuOpen &&
               !AreShipControlsBlocked();
    }

    public bool IsAdvancedShootJoystickEnabled()
    {
        return IsComplexShootingActive;
    }

    public bool TriggerAdvancedAutoAimShot()
    {
        if (!CanUseAdvancedShootJoystick())
            return false;

        WeaponAttackProfile profile = activeComplexWeaponProfile ?? SyncComplexWeaponProfile();
        if (profile == null)
            return false;

        complexShootWasPressed = false;
        complexShootMaxDragMagnitude = 0f;
        complexLastAimDirection = transform.up;
        complexLastAimTargetPoint = (Vector2)transform.position + ((Vector2)transform.up * GetComplexRangeWorld(profile));
        HideAimMarker();
        ResolveComplexAutoAim(profile, out Vector2 direction, out Vector2 targetPoint, out int targetViewId);
        int rocketTargetViewId = IsRocketWeaponProfile(profile) ? targetViewId : 0;
        return TryFireComplexAttack(profile, direction, targetPoint, true, false, rocketTargetViewId);
    }

    public bool ReleaseAdvancedFloatingAim()
    {
        if (!CanUseAdvancedShootJoystick())
            return false;

        if (!complexShootWasPressed)
            return false;

        WeaponAttackProfile profile = activeComplexWeaponProfile ?? SyncComplexWeaponProfile();
        if (profile == null)
            return false;

        ReleaseComplexNormalInput(profile);
        return true;
    }

    int GetConfiguredMaxAmmo()
    {
        if (IsNeutralRiderShip() || (customAmmoProfileActive && IsEnemyBotShip()))
            return maxAmmo;

        int configuredAmmo = RoomSettings.GetAmmoCount();
        if (ShouldApplyNovaNoEquipmentAmmoBonus())
            configuredAmmo *= 2;

        configuredAmmo = ApplyOverclockedMagazineAmmoBonus(configuredAmmo);
        return GetDamageAdjustedMaxAmmo(configuredAmmo);
    }

    bool ShouldApplyNovaNoEquipmentAmmoBonus()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        if (!PilotCatalog.IsSelectedPilot(owner, PilotCatalog.NovaId))
            return false;

        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        if (equipmentSlots == null)
            return true;

        for (int i = 0; i < equipmentSlots.Length; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            if (!string.IsNullOrWhiteSpace(equipmentSlots[i]))
                return false;
        }

        return true;
    }

    int GetDamageAdjustedMaxAmmo(int baseMaxAmmo)
    {
        ShipDamageState damageState = GetComponent<ShipDamageState>();
        return damageState != null ? damageState.GetAdjustedAmmoMax(baseMaxAmmo) : Mathf.Max(1, baseMaxAmmo);
    }

    string GetShipDamageAmmoSignature()
    {
        ShipDamageState damageState = GetComponent<ShipDamageState>();
        return damageState != null ? damageState.GetAmmoDamageSignature() : "0";
    }

    bool IsShipDamageActive(ShipDamageType type)
    {
        ShipDamageState damageState = GetComponent<ShipDamageState>();
        return damageState != null && damageState.HasDamage(type);
    }

    int GetPilotAdjustedWeaponMaxAmmo(WeaponAttackProfile profile)
    {
        int configuredAmmo = Mathf.Max(1, profile != null ? profile.MaxAmmo : RoomSettings.GetAmmoCount());
        if (ShouldApplyNovaNoEquipmentAmmoBonus())
            configuredAmmo *= 2;

        if (ShouldApplyCovaxRocketAmmoBonus(profile))
            configuredAmmo += 1;

        configuredAmmo = ApplyOverclockedMagazineAmmoBonus(configuredAmmo);
        return GetDamageAdjustedMaxAmmo(configuredAmmo);
    }

    bool ShouldApplyCovaxRocketAmmoBonus(WeaponAttackProfile profile)
    {
        if (!IsRocketWeaponProfile(profile))
            return false;

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return PilotCatalog.IsSelectedPilot(owner, PilotCatalog.CovaxId);
    }

    float GetAdjustedAttackCooldown(WeaponAttackProfile profile)
    {
        return Mathf.Max(0.05f, profile != null ? profile.AttackCooldown : fireRate) * GetFireIntervalMultiplier();
    }

    float GetFireIntervalMultiplier()
    {
        float multiplier = ElectromagneticShockStatus.GetFireIntervalMultiplier(gameObject) *
                           AtlasSuppressionStatus.GetFireIntervalMultiplier(gameObject) *
                           AshSuperchargeStatus.GetFireIntervalMultiplier(gameObject);

        if (IsEnemyBotShip())
            multiplier *= RoomSettings.GetEnemyAttackCooldownMultiplier();

        return multiplier;
    }

    float GetAdjustedSimpleReloadDuration()
    {
        return Mathf.Max(0.05f, reloadDuration * AtlasSuppressionStatus.GetReloadMultiplier(gameObject) * AshSuperchargeStatus.GetAmmoReloadMultiplier(gameObject) * GetOverclockedMagazineReloadMultiplier());
    }

    float GetAdjustedAmmoReloadTime(WeaponAttackProfile profile, bool ashEmergencyReload)
    {
        float reloadTime = Mathf.Max(0f, profile != null ? profile.AmmoReloadTime : reloadDuration);
        if (reloadTime <= 0f)
            return 0f;

        if (ShouldApplyAshCapacitorTuning(profile))
            reloadTime *= AshCapacitorAmmoReloadMultiplier;

        if (ashEmergencyReload && ShouldApplyAshEmergencyCapacitor(profile))
            reloadTime *= AshEmergencyAmmoReloadMultiplier;

        reloadTime *= AtlasSuppressionStatus.GetReloadMultiplier(gameObject) * AshSuperchargeStatus.GetAmmoReloadMultiplier(gameObject) * GetOverclockedMagazineReloadMultiplier();
        return Mathf.Max(0.001f, reloadTime);
    }

    int ApplyOverclockedMagazineAmmoBonus(int baseAmmo)
    {
        int count = GetEquippedOverclockedMagazineCount();
        if (count <= 0)
            return Mathf.Max(1, baseAmmo);

        return Mathf.Max(1, Mathf.CeilToInt(baseAmmo * Mathf.Pow(OverclockedMagazineAmmoMultiplier, count)));
    }

    float GetOverclockedMagazineReloadMultiplier()
    {
        int count = GetEquippedOverclockedMagazineCount();
        return count > 0 ? Mathf.Pow(OverclockedMagazineReloadMultiplier, count) : 1f;
    }

    int GetEquippedOverclockedMagazineCount()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        return InventoryItemCatalog.CountEquippedItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.OverclockedMagazineId);
    }

    float GetProjectileSpeedMultiplier()
    {
        return AtlasSuppressionStatus.GetProjectileSpeedMultiplier(gameObject);
    }

    bool ShouldApplyAshCapacitorTuning(WeaponAttackProfile profile)
    {
        if (!IsAshSelected())
            return false;

        return IsWeaponProfile(profile, WeaponAttackCatalog.PlasmaGunId) ||
               IsWeaponProfile(profile, WeaponAttackCatalog.RailGunId) ||
               IsWeaponProfile(profile, WeaponAttackCatalog.DoubleIonizerId);
    }

    bool ShouldApplyAshEmergencyCapacitor(WeaponAttackProfile profile)
    {
        return IsAshSelected() && profile != null && profile.AmmoReloadTime > 0f;
    }

    bool IsAshSelected()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return PilotCatalog.IsSelectedPilot(owner, PilotCatalog.AshId);
    }
}
