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
    EnemyBot GetCachedEnemyBot()
    {
        if (cachedEnemyBot == null && (!hasResolvedEnemyBot || EnemyBot.IsBotInstantiationData(photonView != null ? photonView.InstantiationData : null)))
            cachedEnemyBot = GetComponent<EnemyBot>();

        hasResolvedEnemyBot = true;
        return cachedEnemyBot;
    }

    bool IsEnemyBotShip()
    {
        return GetCachedEnemyBot() != null;
    }

    PlayerRepairDocking GetCachedRepairDocking()
    {
        if (cachedRepairDocking == null)
            cachedRepairDocking = GetComponent<PlayerRepairDocking>();

        return cachedRepairDocking;
    }

    bool IsNeutralRiderShip()
    {
        return NeutralRiderController.IsNeutralRider(gameObject) ||
               NeutralRiderController.IsNeutralRiderInstantiationData(photonView != null ? photonView.InstantiationData : null);
    }

    void Start()
    {
        if (ViperRecoveryPlotController.TryEnsureViperWreckRuntime(gameObject))
        {
            enabled = false;
            return;
        }

        if (PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
        {
            PlayerDeployableRuntime.EnsureAttached(gameObject);
            enabled = false;
            return;
        }

        if (LureBeaconDecoy.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
        {
            LureBeaconDecoy.EnsureAttached(gameObject);
            enabled = false;
            return;
        }

        EnsureBotBootstrap();
        EnsureNeutralRiderBootstrap();

        if (IsNeutralRiderShip())
            return;

        if (AstronautSurvivor.IsAstronautInstantiationData(photonView != null ? photonView.InstantiationData : null))
            return;

        CaptureBaseWeaponProfile();
        maxAmmo = GetConfiguredMaxAmmo();
        currentAmmo = maxAmmo;

        if (IsEnemyBotShip())
            return;

        if (GetComponent<LootingFriendController>() == null)
            gameObject.AddComponent<LootingFriendController>();

        if (GetComponent<FiringFriendController>() == null)
            gameObject.AddComponent<FiringFriendController>();

        if (GetComponent<PreservedAlphaSpecimenCompanionController>() == null)
            gameObject.AddComponent<PreservedAlphaSpecimenCompanionController>();

        if (GetComponent<GuidanceSystemOverlay>() == null)
            gameObject.AddComponent<GuidanceSystemOverlay>();

        if (GetComponent<TreasureScannerPingController>() == null)
            gameObject.AddComponent<TreasureScannerPingController>();

        if (!photonView.IsMine)
            return;

        if (GetComponent<ComplexAmmoBarUI>() == null)
        {
            gameObject.AddComponent<ComplexAmmoBarUI>();
        }

        if (GetComponent<SuperAttackUI>() == null)
        {
            gameObject.AddComponent<SuperAttackUI>();
        }

        if (GetComponent<WeaponSwitchButtonUI>() == null)
        {
            gameObject.AddComponent<WeaponSwitchButtonUI>();
        }

        if (GetComponent<GadgetButtonUI>() == null)
        {
            gameObject.AddComponent<GadgetButtonUI>();
        }

        if (GetComponent<AdvancedShootInputZone>() == null)
        {
            advancedShootInputZone = gameObject.AddComponent<AdvancedShootInputZone>();
        }

    }

    void Update()
    {
        EnsureBotBootstrap();
        EnsureNeutralRiderBootstrap();

        if (!IsGameStarted())
            return;

        if (photonView.IsMine &&
            !IsEnemyBotShip() &&
            !IsNeutralRiderShip() &&
            HackingStatus.IsActiveOn(gameObject))
        {
            if (HackingStatus.TryConsumeRandomShot(gameObject, out Vector2 hackedShotDirection))
                TryFireHackedRandom(hackedShotDirection);

            return;
        }

        if (IsEnemyBotShip())
        {
            UpdateReload();
            return;
        }

        if (IsNeutralRiderShip())
        {
            UpdateReload();
            return;
        }

        if (!photonView.IsMine)
            return;

        if (AreShipControlsBlocked())
        {
            ResetComplexPressState();
            HideAimMarker();
            return;
        }

        SyncEquippedGadgetProfile();
        RefreshAuthoritativeGadgetRuntimeStates();
        ClearExpiredInstantDeployLocks();

        if (!HasMainGunSlotsForCurrentShip())
        {
            ClearMainGunRuntime();
            HideAimMarker();
            return;
        }

        if (IsComplexShootingActive)
        {
            SyncComplexWeaponProfile();
            UpdateComplexAmmoReload();
            UpdateSuperCharge();
            if (RoundChatCommandUI.IsLocalChatMenuOpen)
            {
                ResetComplexPressState();
                HideAimMarker();
                return;
            }

            HandleComplexShootingInput();
            return;
        }

        HideAimMarker();
        SyncEquippedWeaponProfile();
        SyncAmmoSetting();
        UpdateReload();

        if (RoundChatCommandUI.IsLocalChatMenuOpen)
            return;

        EnsureShootJoystick();

        if (shootJoystick == null)
            return;

        if (isReloading || currentAmmo <= 0)
            return;

        if (!shootJoystick.IsPressed)
            return;

        Vector2 direction = ResolveManualAimDirection();

        if (Time.time >= nextFireTime)
        {
            if (Shoot(direction.normalized, simpleAutoAimTargetViewId, simpleAutoAimHasTargetPoint, simpleAutoAimTargetPoint))
            {
                ConsumeAmmo();
                nextFireTime = Time.time + fireRate * GetFireIntervalMultiplier();
            }
        }
    }

    void HandleComplexShootingInput()
    {
        EnsureShootJoystick();
        EnsureSuperJoystick();

        PlayerRepairDocking repairDocking = GetCachedRepairDocking();
        bool controlsLocked = repairDocking != null && repairDocking.IsBusy;
        if (controlsLocked)
        {
            ResetComplexPressState();
            HideAimMarker();
            return;
        }

        bool handledSuper = HandleComplexSuperInput();
        if (!handledSuper)
            HandleComplexNormalInput();
    }

    void EnsureShootJoystick()
    {
        if (shootJoystick == null)
        {
            GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
            if (shootJoystickObject != null)
                shootJoystick = shootJoystickObject.GetComponent<Joystick>();
        }

        ConfigureShootJoystickForAutoAim();
    }

    void ConfigureShootJoystickForAutoAim()
    {
        if (shootJoystick == null)
            return;

        shootJoystick.centerInputOnPointerDownInsideHandle = true;
    }

    void EnsureSuperJoystick()
    {
        if (superJoystick != null)
            return;

        GameObject superJoystickObject = GameObject.Find(SuperAttackUI.RootName);
        if (superJoystickObject != null)
            superJoystick = superJoystickObject.GetComponent<Joystick>();
    }
}
