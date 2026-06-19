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

public class PlayerShooting : MonoBehaviourPun
{
    const float AutoAimRange = 13f;
    const float AutoAimAcquireRangeBonus = 1.25f;
    const float AutoAimDirectLeadScale = 0.55f;
    const float AutoAimArcLeadScale = 0.5f;
    const float AutoAimDirectLeadMaxSeconds = 0.7f;
    const float AutoAimArcLeadMaxSeconds = 1f;
    const float AutoAimLeadVelocityThreshold = 0.08f;
    const float ManualAimThreshold = 0.35f;
    const float DefaultBulletRangeMultiplier = 15f;
    const float ComplexTapMaxDuration = 0.44f;
    const float ComplexTapMaxDragMagnitude = 0.24f;
    const float AdvancedShootMarkerRawThreshold = ComplexTapMaxDragMagnitude;
    const float SuperChargeTimeSeconds = 24f;
    const float SuperChargeOnComplexHit = 0.08f;
    const float GadgetMinePlacementCooldown = 0.9f;
    const float DropbotLaunchRequestTimeout = 5f;
    const float LootHookRequestTimeout = 5f;
    const float LootHookRange = 5.8f;
    const float LootHookWindupDuration = 3f;
    const float LootHookValidationInterval = 0.05f;
    const int LootHookNeutralCargoSlotIndex = -1001;
    const int GadgetMineDefaultCharges = 4;
    const int SpaceBombDefaultCharges = 1;
    const int DropbotDefaultCharges = 1;
    const int BatteryDefaultCharges = 3;
    const int MagneticBeamDefaultCharges = 3;
    const int TractorBeamDefaultCharges = 4;
    const int LureBeaconDefaultCharges = 2;
    const int AutoTurretDefaultCharges = 1;
    const int RocketAutoTurretDefaultCharges = 1;
    const int GuidanceSystemDefaultCharges = 2;
    const int ShortScannerDefaultCharges = 3;
    const int CloakDeviceDefaultCharges = 2;
    const int HackingDeviceDefaultCharges = 2;
    const int SpaceDrillDefaultCharges = 2;
    const int SpaceTrapDefaultCharges = 1;
    const int SuperBoosterDefaultCharges = 2;
    const int LootHookDefaultCharges = 2;
    const int StasisBuoyDefaultCharges = 2;
    const int TetherHarpoonDefaultCharges = 2;
    const int SpaceTorpedoDefaultCharges = 2;
    const int BioTrapDefaultCharges = 1;
    const int AsteroidBreacherBombDefaultCharges = 2;
    const int MetalDriftWallDefaultCharges = 2;
    const float OverclockedMagazineAmmoMultiplier = 1.6f;
    const float OverclockedMagazineReloadMultiplier = 1.25f;
    const float MagneticBeamRadius = 8f;
    const float MagneticBeamDuration = 3f;
    const float MagneticBeamPullStrength = 24f;
    const float TractorBeamRadius = 8f;
    const float TractorBeamMaxDuration = 10f;
    const float TractorBeamPullStrength = 36f;
    const float TractorBeamSlackDistance = 2.65f;
    const float TractorBeamTetherRampDistance = 4.25f;
    const float LureBeaconDeployDistance = 1.15f;
    const float LureBeaconSpawnClearanceRadius = 0.42f;
    const float GuidanceSystemDuration = 9f;
    const float ShortScannerRevealDuration = 15f;
    const float CloakDeviceDuration = 7f;
    const float HackingDeviceRange = 7.2f;
    const float HackingDeviceKeepAliveRange = 8.4f;
    const float HackingDeviceWindupDuration = 3f;
    const float HackingDevicePlayerDuration = 8f;
    const float HackingDeviceComputerMinDuration = 5f;
    const float HackingDeviceComputerMaxDuration = 20f;
    const float HackingDeviceBehindDotThreshold = -0.52f;
    const float HackingDeviceValidationInterval = 0.05f;
    const float SuperBoosterDuration = 2f;
    const float AstroCutterTickInterval = 0.2f;
    const float AstroCutterBeamRadius = 0.14f;
    const float AstroCutterSuperBeamRadius = 0.28f;
    const int AstroCutterObstacleDamageMultiplier = 4;
    const int AstroCutterSuperObstacleDamageMultiplier = 6;
    const float RocketLockDuration = 2f;
    const float VectorRocketLockDurationMultiplier = 0.5f;
    const float VectorGuidanceSystemDurationBonus = 15f;
    const float VectorShortScannerRevealDurationBonus = 10f;
    const float RocketLockLineRadius = 0.64f;
    const float RocketLockBreakAngle = 26f;
    const float RocketHomingTurnRate = 145f;
    const float DoubleRocketParallelLaneSpacing = 0.42f;
    const float DoubleRocketUnguidedMinDivergenceAngle = 2.5f;
    const float DoubleRocketUnguidedMaxDivergenceAngle = 7.5f;
    const float DoubleRocketSuperChargeMultiplier = 0.7f;
    const float AshCapacitorAmmoReloadMultiplier = 0.9f;
    const float AshEmergencyAmmoReloadMultiplier = 0.75f;
    const float PulseDisruptorWaveTickInterval = 0.035f;
    const string PilotBreachProjectileMarker = "sir_breach";
    static readonly Collider2D[] PulseDisruptorHits = new Collider2D[128];
    static readonly Collider2D[] SpawnClearanceHits = new Collider2D[48];
    static readonly RaycastHit2D[] LineBlockHits = new RaycastHit2D[64];
    static readonly RaycastHit2D[] AstroCutterHits = new RaycastHit2D[128];
    static readonly Color PlasmaBulletColor = new Color(0.15f, 1f, 0.28f, 1f);
    static readonly Color RocketLockedMarkerColor = new Color(0.2f, 1f, 0.35f, 1f);

    sealed class GadgetRuntimeState
    {
        public string ItemId;
        public int MaxCharges;
        public int RemainingCharges;
        public float Cooldown;
        public float NextUseTime;
    }

    sealed class ComplexWeaponRuntimeState
    {
        public int SlotIndex;
        public string WeaponId;
        public WeaponAttackProfile Profile;
        public int CurrentAmmo;
        public int MaxAmmo;
        public float AmmoReloadStartedAt;
        public float NextAmmoAt;
        public bool AshEmergencyReloadPending;
    }

    struct AutoAimTargetResult
    {
        public PlayerHealth Health;
        public Vector2 AimPoint;
        public int ViewId;
    }

    public Joystick shootJoystick;
    public GameObject bulletPrefab;
    public float bulletSpeed = 10f;
    public float fireRate = 0.3f;
    public int maxAmmo = 10;
    public float reloadDuration = 4f;
    public int bulletDamage = 10;
    public float bulletScaleMultiplier = 1f;
    public Color bulletColor = Color.white;
    public float muzzleOffsetDistance = 0.5f;
    public float bulletRangeMultiplier = DefaultBulletRangeMultiplier;
    public bool infiniteAmmo;
    public string shotSoundId = string.Empty;
    public static bool gameStarted = false;

    float nextFireTime = 0f;
    int currentAmmo;
    bool isReloading;
    float reloadFinishTime;
    bool customAmmoProfileActive;
    bool baseWeaponProfileCaptured;
    float baseBulletSpeed;
    float baseFireRate;
    float baseReloadDuration;
    int baseBulletDamage;
    float baseBulletScaleMultiplier;
    Color baseBulletColor;
    float baseMuzzleOffsetDistance;
    float baseBulletRangeMultiplier;
    bool baseInfiniteAmmo;
    string baseShotSoundId = string.Empty;
    int multiShotCount = 1;
    string lastAppliedWeaponSignature = string.Empty;
    string lastAppliedComplexWeaponSignature = string.Empty;
    string lastAppliedGadgetSignature = string.Empty;
    readonly List<string> activeGadgetItemIds = new List<string>();
    readonly Dictionary<string, GadgetRuntimeState> gadgetStates = new Dictionary<string, GadgetRuntimeState>(StringComparer.Ordinal);
    readonly Dictionary<string, int> authoritativeGadgetCharges = new Dictionary<string, int>(StringComparer.Ordinal);
    string lastAuthoritativeGadgetChargeStateRaw = null;
    Coroutine authoritativeTractorBeamRoutine;
    int activeTractorBeamTargetViewId;
    string activeTractorBeamItemId;
    Coroutine authoritativeHackingDeviceRoutine;
    int activeHackingDeviceTargetViewId;
    string activeHackingDeviceItemId;
    bool pendingDropbotLaunchActive;
    int pendingDropbotLaunchRequestId;
    int pendingDropbotCargoSlotIndex = -1;
    int pendingDropbotOwnerActorNumber;
    string pendingDropbotCargoItemId;
    float pendingDropbotLaunchExpiresAt;
    bool pendingLootHookActive;
    int pendingLootHookRequestId;
    int pendingLootHookVictimViewId;
    int pendingLootHookVictimActorNumber;
    int pendingLootHookCargoSlotIndex = -1;
    string pendingLootHookCargoItemId;
    float pendingLootHookExpiresAt;
    Coroutine authoritativeLootHookRoutine;
    int activeLootHookTargetViewId;
    int activeLootHookCargoSlotIndex = -1;
    string activeLootHookCargoItemId;
    WeaponAttackProfile activeSimpleWeaponProfile;
    WeaponAttackProfile activeComplexWeaponProfile;
    readonly List<ComplexWeaponRuntimeState> complexWeaponStates = new List<ComplexWeaponRuntimeState>();
    int activeComplexWeaponIndex;
    AimMarkerVfx aimMarker;
    Coroutine complexBurstRoutine;
    float nextComplexAttackTime;
    bool complexShootWasPressed;
    bool complexSuperWasPressed;
    float complexShootPressStartedAt;
    float complexSuperPressStartedAt;
    float complexShootMaxDragMagnitude;
    float complexSuperMaxDragMagnitude;
    bool complexShootCanceledByCenteredAim;
    bool complexSuperCanceledByCenteredAim;
    int rocketLockCandidateViewId;
    int rocketLockedTargetViewId;
    int simpleAutoAimTargetViewId;
    bool simpleAutoAimHasTargetPoint;
    Vector2 simpleAutoAimTargetPoint;
    float rocketLockStartedAt;
    bool rocketLockFeedbackPlayed;
    Vector2 complexLastAimDirection = Vector2.up;
    Vector2 complexLastSuperAimDirection = Vector2.up;
    Vector2 complexLastAimTargetPoint;
    Vector2 complexLastSuperAimTargetPoint;
    float complexAmmoReloadStartedAt;
    float complexNextAmmoAt;
    float superCharge;
    Coroutine astroCutterBeamRoutine;
    Joystick superJoystick;
    AdvancedShootInputZone advancedShootInputZone;
    bool simpleUsesDamageProfile;
    int simpleShieldDamage;
    int simpleHpDamage;
    bool simplePierces;
    float simpleAreaDamageRadius;
    string simpleHitEffectId = string.Empty;
    float simpleFlightTime = 10f;
    WeaponDamageType simpleDamageType = WeaponDamageType.Laser;
    WeaponDeliveryMethod simpleDeliveryMethod = WeaponDeliveryMethod.DirectProjectile;
    WeaponDeliveryFlags simpleDeliveryFlags = WeaponDeliveryFlags.None;
    readonly HashSet<int> astroCutterDamagedViews = new HashSet<int>();
    readonly HashSet<string> astroCutterDamagedObstacles = new HashSet<string>(StringComparer.Ordinal);

    public int CurrentAmmo => IsComplexShootingActive && GetActiveComplexWeaponState() != null ? GetActiveComplexWeaponState().CurrentAmmo : currentAmmo;
    public int MaxAmmo => IsComplexShootingActive && GetActiveComplexWeaponState() != null ? GetActiveComplexWeaponState().MaxAmmo : maxAmmo;
    public bool IsReloading => isReloading;
    public bool IsComplexShootingActive => GetComponent<EnemyBot>() == null &&
                                           !IsNeutralRiderShip() &&
                                           !AstronautSurvivor.IsAstronautInstantiationData(photonView != null ? photonView.InstantiationData : null) &&
                                           HasMainGunSlotsForCurrentShip();
    public float ComplexAmmoReloadProgress => GetComplexAmmoReloadProgress();
    public float SuperChargeNormalized => Mathf.Clamp01(superCharge);
    public bool IsSuperAttackReady => IsComplexShootingActive && superCharge >= 0.999f;
    public bool CanManualReload => photonView.IsMine && IsGameStarted() && !IsComplexShootingActive && !isReloading && currentAmmo > 0 && currentAmmo < maxAmmo;
    public int ComplexWeaponCount => complexWeaponStates.Count;
    public int ActiveComplexWeaponNumber => complexWeaponStates.Count > 0 ? activeComplexWeaponIndex + 1 : 1;
    public bool IsComplexWeaponSwitchLockedByDamage => IsShipDamageActive(ShipDamageType.SecondaryWeapon);
    public Sprite NextComplexWeaponIcon
    {
        get
        {
            ComplexWeaponRuntimeState state = GetNextComplexWeaponState();
            return state != null ? WeaponAttackCatalog.GetWeaponIcon(state.WeaponId) : null;
        }
    }
    public string NextComplexWeaponLabel
    {
        get
        {
            ComplexWeaponRuntimeState state = GetNextComplexWeaponState();
            return state?.Profile?.DisplayName ?? "SIMPLE GUN";
        }
    }
    public string ActiveComplexWeaponLabel
    {
        get
        {
            ComplexWeaponRuntimeState state = GetActiveComplexWeaponState();
            return state?.Profile?.DisplayName ?? "SIMPLE GUN";
        }
    }
    public IReadOnlyList<string> ActiveGadgetItemIds => activeGadgetItemIds;
    public string CurrentGadgetItemId => activeGadgetItemIds.Count > 0 ? activeGadgetItemIds[0] : null;
    public Sprite CurrentGadgetIcon => !string.IsNullOrWhiteSpace(CurrentGadgetItemId) ? InventoryItemCatalog.GetIcon(CurrentGadgetItemId) : null;
    public int RemainingGadgetCharges => GetRemainingGadgetCharges(CurrentGadgetItemId);
    public int MaxGadgetCharges => GetMaxGadgetCharges(CurrentGadgetItemId);
    public float ReloadProgress
    {
        get
        {
            if (!isReloading || reloadDuration <= 0f)
                return 0f;

            float remaining = Mathf.Max(0f, reloadFinishTime - Time.time);
            return 1f - Mathf.Clamp01(remaining / reloadDuration);
        }
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

        if (GetComponent<EnemyBot>() != null)
            return;

        if (GetComponent<LootingFriendController>() == null)
            gameObject.AddComponent<LootingFriendController>();

        if (GetComponent<FiringFriendController>() == null)
            gameObject.AddComponent<FiringFriendController>();

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
            GetComponent<EnemyBot>() == null &&
            !IsNeutralRiderShip() &&
            HackingStatus.IsActiveOn(gameObject))
        {
            if (HackingStatus.TryConsumeRandomShot(gameObject, out Vector2 hackedShotDirection))
                TryFireHackedRandom(hackedShotDirection);

            return;
        }

        if (GetComponent<EnemyBot>() != null)
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

        PlayerRepairDocking repairDocking = GetComponent<PlayerRepairDocking>();
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
        PlayerHealth[] targets = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
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

    string BuildComplexWeaponSignature(string[] equipmentSlots, int shipSkinIndex)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(shipSkinIndex);
        builder.Append(':');
        for (int slot = 0; slot < 2; slot++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(slot, shipSkinIndex))
                continue;

            if (builder[builder.Length - 1] != ':')
                builder.Append(',');

            builder.Append(slot);
            builder.Append('=');
            builder.Append(GetWeaponIdForEquipmentSlot(equipmentSlots, slot));
        }

        builder.Append('|');
        builder.Append(WeaponAttackCatalog.GetRoomSetupSignature());
        builder.Append("|ammoDamage=");
        builder.Append(GetShipDamageAmmoSignature());
        return builder.ToString();
    }

    void BuildComplexWeaponStates(string[] equipmentSlots, int shipSkinIndex, List<ComplexWeaponRuntimeState> previousStates)
    {
        for (int slot = 0; slot < 2; slot++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(slot, shipSkinIndex))
                continue;

            string weaponId = GetWeaponIdForEquipmentSlot(equipmentSlots, slot);
            WeaponAttackProfile profile = WeaponAttackCatalog.GetNormalAttackByWeaponId(weaponId);
            ComplexWeaponRuntimeState previous = FindPreviousComplexWeaponState(previousStates, slot, weaponId);
            int max = GetPilotAdjustedWeaponMaxAmmo(profile);
            ComplexWeaponRuntimeState state = new ComplexWeaponRuntimeState
            {
                SlotIndex = slot,
                WeaponId = weaponId,
                Profile = profile,
                MaxAmmo = max,
                CurrentAmmo = previous != null ? Mathf.Clamp(previous.CurrentAmmo, 0, max) : max,
                AmmoReloadStartedAt = previous != null ? previous.AmmoReloadStartedAt : 0f,
                NextAmmoAt = previous != null ? previous.NextAmmoAt : 0f,
                AshEmergencyReloadPending = previous != null && previous.AshEmergencyReloadPending
            };
            complexWeaponStates.Add(state);
        }

        if (complexWeaponStates.Count == 0 && ShipCatalog.GetMainGunSlots(shipSkinIndex) > 0)
        {
            WeaponAttackProfile profile = WeaponAttackCatalog.GetNormalAttackByWeaponId(WeaponAttackCatalog.SimpleGunId);
            int max = GetPilotAdjustedWeaponMaxAmmo(profile);
            complexWeaponStates.Add(new ComplexWeaponRuntimeState
            {
                SlotIndex = 0,
                WeaponId = WeaponAttackCatalog.SimpleGunId,
                Profile = profile,
                MaxAmmo = max,
                CurrentAmmo = max
            });
        }
    }

    ComplexWeaponRuntimeState FindPreviousComplexWeaponState(List<ComplexWeaponRuntimeState> previousStates, int slot, string weaponId)
    {
        if (previousStates == null)
            return null;

        for (int i = 0; i < previousStates.Count; i++)
        {
            ComplexWeaponRuntimeState state = previousStates[i];
            if (state != null &&
                state.SlotIndex == slot &&
                string.Equals(state.WeaponId, weaponId, StringComparison.Ordinal))
            {
                return state;
            }
        }

        return null;
    }

    string GetWeaponIdForEquipmentSlot(string[] equipmentSlots, int slotIndex)
    {
        return WeaponAttackCatalog.GetWeaponIdForItem(GetEquipmentItem(equipmentSlots, slotIndex));
    }

    bool HasMainGunSlotsForCurrentShip()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        return ShipCatalog.GetMainGunSlots(shipSkinIndex) > 0;
    }

    void ClearMainGunRuntime()
    {
        if (complexWeaponStates.Count > 0)
            complexWeaponStates.Clear();

        activeComplexWeaponProfile = null;
        activeSimpleWeaponProfile = null;
        currentAmmo = 0;
        maxAmmo = 0;
        isReloading = false;
        reloadFinishTime = 0f;
        nextComplexAttackTime = 0f;
        superCharge = 0f;
        ResetComplexPressState();
    }

    ComplexWeaponRuntimeState GetActiveComplexWeaponState()
    {
        if (complexWeaponStates.Count == 0)
            return null;

        activeComplexWeaponIndex = Mathf.Clamp(activeComplexWeaponIndex, 0, complexWeaponStates.Count - 1);
        return complexWeaponStates[activeComplexWeaponIndex];
    }

    ComplexWeaponRuntimeState GetNextComplexWeaponState()
    {
        if (complexWeaponStates.Count == 0)
            return null;

        int activeIndex = Mathf.Clamp(activeComplexWeaponIndex, 0, complexWeaponStates.Count - 1);
        int nextIndex = (activeIndex + 1) % complexWeaponStates.Count;
        return complexWeaponStates[nextIndex];
    }

    void SyncActiveComplexAmmoMirror()
    {
        ComplexWeaponRuntimeState state = GetActiveComplexWeaponState();
        if (state == null)
            return;

        activeComplexWeaponProfile = state.Profile;
        maxAmmo = Mathf.Max(1, state.MaxAmmo);
        currentAmmo = Mathf.Clamp(state.CurrentAmmo, 0, maxAmmo);
        complexAmmoReloadStartedAt = state.AmmoReloadStartedAt;
        complexNextAmmoAt = state.NextAmmoAt;
    }

    void UpdateComplexAmmoReload()
    {
        if (complexWeaponStates.Count == 0)
            SyncComplexWeaponProfile();

        for (int i = 0; i < complexWeaponStates.Count; i++)
        {
            ComplexWeaponRuntimeState state = complexWeaponStates[i];
            if (state == null || state.Profile == null)
                continue;

            if (state.CurrentAmmo >= state.MaxAmmo)
            {
                state.NextAmmoAt = 0f;
                state.AmmoReloadStartedAt = 0f;
                continue;
            }

            float reloadTime = GetAdjustedAmmoReloadTime(state.Profile, state.AshEmergencyReloadPending);
            if (reloadTime <= 0f)
                continue;

            if (state.NextAmmoAt <= 0f)
            {
                state.AmmoReloadStartedAt = Time.time;
                state.NextAmmoAt = Time.time + reloadTime;
            }
            else
            {
                ShortenActiveAmmoReloadIfFaster(state, reloadTime);
            }

            while (state.CurrentAmmo < state.MaxAmmo && state.NextAmmoAt > 0f && Time.time >= state.NextAmmoAt)
            {
                state.CurrentAmmo++;
                state.AshEmergencyReloadPending = false;
                if (state.CurrentAmmo >= state.MaxAmmo)
                {
                    state.NextAmmoAt = 0f;
                    state.AmmoReloadStartedAt = 0f;
                    break;
                }

                reloadTime = GetAdjustedAmmoReloadTime(state.Profile, false);
                state.AmmoReloadStartedAt = state.NextAmmoAt;
                state.NextAmmoAt += reloadTime;
            }
        }

        SyncActiveComplexAmmoMirror();
    }

    void ShortenActiveAmmoReloadIfFaster(ComplexWeaponRuntimeState state, float desiredReloadTime)
    {
        if (state == null || desiredReloadTime <= 0f || state.NextAmmoAt <= 0f)
            return;

        float scheduledDuration = state.NextAmmoAt - state.AmmoReloadStartedAt;
        if (scheduledDuration <= 0.001f || desiredReloadTime >= scheduledDuration - 0.01f)
            return;

        float progress = Mathf.Clamp01((Time.time - state.AmmoReloadStartedAt) / scheduledDuration);
        state.AmmoReloadStartedAt = Time.time - (desiredReloadTime * progress);
        state.NextAmmoAt = state.AmmoReloadStartedAt + desiredReloadTime;
    }

    float GetComplexAmmoReloadProgress()
    {
        ComplexWeaponRuntimeState state = GetActiveComplexWeaponState();
        if (!IsComplexShootingActive || state == null || state.CurrentAmmo >= state.MaxAmmo || state.NextAmmoAt <= 0f)
            return 0f;

        float duration = Mathf.Max(0.001f, state.NextAmmoAt - state.AmmoReloadStartedAt);
        return Mathf.Clamp01((Time.time - state.AmmoReloadStartedAt) / duration);
    }

    void UpdateSuperCharge()
    {
        superCharge = Mathf.Clamp01(superCharge + (Time.deltaTime / SuperChargeTimeSeconds) * GetSuperChargeGainMultiplier());
    }

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
        PlayerHealth[] targets = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
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

    void ShowAimMarker(WeaponAttackProfile profile, Vector2 direction, Vector2? explicitTargetPoint = null, Color? markerColorOverride = null)
    {
        if (profile == null)
            return;

        if (aimMarker == null)
            aimMarker = AimMarkerVfx.EnsureFor(gameObject);

        if (aimMarker == null)
            return;

        Vector2 resolvedDirection = ResolveSafeAimDirection(direction);
        float range = GetComplexRangeWorld(profile);
        Color markerColor = markerColorOverride ?? profile.MarkerColor;
        if (IsArcWeaponProfile(profile))
        {
            Vector3 landingPoint = explicitTargetPoint.HasValue
                ? (Vector3)explicitTargetPoint.Value
                : transform.position + (Vector3)(resolvedDirection * range);
            aimMarker.ShowArc(transform.position, landingPoint, markerColor, ResolveArcHeight(range));
            return;
        }

        aimMarker.ShowLine(transform.position, resolvedDirection, range, markerColor);
    }

    void HideAimMarker()
    {
        if (aimMarker != null)
            aimMarker.Hide();
    }

    float GetComplexRangeWorld(WeaponAttackProfile profile)
    {
        float multiplier = profile != null ? Mathf.Max(0.1f, profile.RangeMultiplier) : DefaultBulletRangeMultiplier;
        return GetOwnerLengthForRange() * multiplier;
    }

    float GetOwnerLengthForRange()
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            return Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y);

        Collider2D collider2D = GetComponentInChildren<Collider2D>();
        if (collider2D != null)
            return Mathf.Max(collider2D.bounds.size.x, collider2D.bounds.size.y);

        return 1f;
    }

    bool SpawnComplexBullet(WeaponAttackProfile profile, Vector2 direction, Vector3 spawnPos, int ownerId, Vector2 explicitTargetPoint, int homingTargetViewId = 0, bool pilotBreachProjectile = false)
    {
        if (bulletPrefab == null || profile == null)
            return false;

        float projectileSpeedMultiplier = GetProjectileSpeedMultiplier();
        float projectileSpeed = Mathf.Max(0.1f, profile.ProjectileSpeed * projectileSpeedMultiplier);
        float clampedFlightTime = Mathf.Clamp(profile.FlightTime / Mathf.Max(0.1f, projectileSpeedMultiplier), 0.2f, 30f);
        float range = GetComplexRangeWorld(profile);
        bool isArcProjectile = IsArcWeaponProfile(profile);
        bool isRocketProjectile = IsRocketWeaponProfile(profile);
        Vector2 targetPoint = isArcProjectile
            ? explicitTargetPoint
            : (Vector2)spawnPos + (direction.normalized * range);
        float arcHeight = ResolveArcHeight(range);

        object[] data = Bullet.AppendWeaponMetadata(
            new object[]
            {
                ownerId,
                Mathf.Max(profile.HpDamage, profile.ShieldDamage),
                profile.ProjectileSize,
                profile.ProjectileColor.r,
                profile.ProjectileColor.g,
                profile.ProjectileColor.b,
                profile.ProjectileColor.a,
                profile.RangeMultiplier,
                profile.ShieldDamage,
                profile.HpDamage,
                profile.Pierces,
                profile.AreaDamageRadius,
                profile.HitEffectId ?? string.Empty,
                clampedFlightTime,
                isArcProjectile,
                targetPoint.x,
                targetPoint.y,
                arcHeight,
                isRocketProjectile ? "rocket" : string.Empty,
                isRocketProjectile ? homingTargetViewId : 0,
                isRocketProjectile ? RocketHomingTurnRate : 0f,
                isRocketProjectile ? projectileSpeed : 0f,
                isRocketProjectile,
                pilotBreachProjectile ? PilotBreachProjectileMarker : string.Empty
            },
            profile.DamageType,
            profile.DeliveryMethod,
            profile.DeliveryFlags);

        GameObject bullet = PhotonNetwork.Instantiate(
            bulletPrefab.name,
            spawnPos,
            Quaternion.identity,
            0,
            data
        );

        if (bullet == null)
            return false;

        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent != null)
            bulletComponent.ownerViewID = ownerId;

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null && !isArcProjectile)
            rb.linearVelocity = direction.normalized * projectileSpeed;

        Collider2D playerCollider = GetComponent<Collider2D>();
        Collider2D bulletCollider = bullet.GetComponent<Collider2D>();
        if (bulletCollider != null && playerCollider != null)
            Physics2D.IgnoreCollision(bulletCollider, playerCollider);

        return true;
    }

    bool TryStartPulseDisruptorWave(WeaponAttackProfile profile, int ownerId)
    {
        if (profile == null || ownerId <= 0 || photonView == null)
            return false;

        Vector2 center = transform.position;
        float radius = Mathf.Max(0.5f, profile.AreaDamageRadius);
        float duration = Mathf.Clamp(profile.FlightTime, 0.2f, 2.5f);
        photonView.RPC(nameof(StartPulseDisruptorWaveRpc), RpcTarget.All, center.x, center.y, radius, duration);
        photonView.RPC(nameof(PlayShotSfx), RpcTarget.All, profile.ShotSoundId ?? string.Empty);
        StartCoroutine(PulseDisruptorWaveDamageRoutine(profile, center, radius, duration, ownerId));
        return true;
    }

    IEnumerator PulseDisruptorWaveDamageRoutine(WeaponAttackProfile profile, Vector2 center, float radius, float duration, int ownerId)
    {
        HashSet<int> damagedViews = new HashSet<int>();
        WaitForSeconds wait = new WaitForSeconds(PulseDisruptorWaveTickInterval);
        float startedAt = Time.time;
        float endAt = startedAt + Mathf.Max(0.05f, duration);

        while (Time.time < endAt)
        {
            float t = Mathf.Clamp01((Time.time - startedAt) / Mathf.Max(0.001f, duration));
            ApplyPulseDisruptorWaveDamage(profile, center, Mathf.Lerp(0.2f, radius, t), ownerId, damagedViews);
            yield return wait;
        }

        ApplyPulseDisruptorWaveDamage(profile, center, radius, ownerId, damagedViews);
    }

    void ApplyPulseDisruptorWaveDamage(WeaponAttackProfile profile, Vector2 center, float currentRadius, int ownerId, HashSet<int> damagedViews)
    {
        if (profile == null || damagedViews == null)
            return;

        int hitCount = Physics2D.OverlapCircle(center, Mathf.Max(0.05f, currentRadius), CreatePhysicsQueryFilter(), PulseDisruptorHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = PulseDisruptorHits[i];
            if (hit == null)
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable.photonView != null)
            {
                if (damagedViews.Add(deployable.photonView.ViewID))
                {
                    Vector2 point = deployable.transform.position;
                    deployable.photonView.RPC(
                        nameof(PlayerDeployableBase.TakeDeployableShieldOnlyDamageWithContextAt),
                        RpcTarget.MasterClient,
                        profile.ShieldDamage,
                        ownerId,
                        point.x,
                        point.y,
                        (int)profile.DamageType,
                        (int)profile.DeliveryMethod,
                        (int)profile.DeliveryFlags,
                        string.Empty);
                    AddSuperChargeForDamage();
                }

                continue;
            }

            LureBeaconDecoy beacon = hit.GetComponentInParent<LureBeaconDecoy>();
            if (beacon != null && beacon.photonView != null)
            {
                if (damagedViews.Add(beacon.photonView.ViewID))
                {
                    Vector2 point = beacon.transform.position;
                    beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconShieldOnlyDamageAt), RpcTarget.MasterClient, profile.ShieldDamage, ownerId, point.x, point.y);
                }

                continue;
            }

            PlayerHealth hp = hit.GetComponentInParent<PlayerHealth>();
            if (hp == null || hp.photonView == null || hp.IsWreck || hp.photonView.ViewID == ownerId)
                continue;

            if (damagedViews.Add(hp.photonView.ViewID))
            {
                Vector2 point = hp.transform.position;
                hp.photonView.RPC(
                    nameof(PlayerHealth.TakeShieldOnlyDamageWithContextAt),
                    RpcTarget.MasterClient,
                    profile.ShieldDamage,
                    ownerId,
                    point.x,
                    point.y,
                    (int)profile.DamageType,
                    (int)profile.DeliveryMethod,
                    (int)profile.DeliveryFlags,
                    string.Empty);
                AddSuperChargeForDamage();
            }
        }
    }

    [PunRPC]
    void StartPulseDisruptorWaveRpc(float x, float y, float radius, float duration)
    {
        PulseDisruptorWaveVfx.Spawn(new Vector3(x, y, transform.position.z - 0.05f), radius, duration);
    }

    bool TryStartAstroCutterBeam(WeaponAttackProfile profile, Vector2 direction, int ownerId)
    {
        if (profile == null || ownerId <= 0 || photonView == null)
            return false;

        direction = ResolveSafeAimDirection(direction);
        float range = GetComplexRangeWorld(profile);
        float duration = Mathf.Clamp(profile.FlightTime, 0.2f, 6f);
        bool isSuperBeam = IsAstroCutterSuperProfile(profile);

        if (astroCutterBeamRoutine != null)
            StopCoroutine(astroCutterBeamRoutine);

        photonView.RPC(nameof(StartAstroCutterBeamRpc), RpcTarget.All, ownerId, direction.x, direction.y, range, duration, isSuperBeam);
        astroCutterBeamRoutine = StartCoroutine(AstroCutterDamageRoutine(profile, direction, range, duration, ownerId, isSuperBeam));
        return true;
    }

    IEnumerator AstroCutterDamageRoutine(WeaponAttackProfile profile, Vector2 direction, float range, float duration, int ownerId, bool isSuperBeam)
    {
        if (profile == null)
            yield break;

        float endTime = Time.time + Mathf.Max(0.1f, duration);
        WaitForSeconds wait = new WaitForSeconds(AstroCutterTickInterval);
        while (Time.time < endTime)
        {
            ApplyAstroCutterDamageTick(profile, direction, range, ownerId, isSuperBeam);
            yield return wait;
        }

        astroCutterBeamRoutine = null;
    }

    void ApplyAstroCutterDamageTick(WeaponAttackProfile profile, Vector2 direction, float range, int ownerId, bool isSuperBeam)
    {
        Vector2 safeDirection = ResolveSafeAimDirection(direction);
        Vector2 start = (Vector2)transform.position + (Vector2)transform.up * Mathf.Max(0.05f, muzzleOffsetDistance);
        float beamRadius = isSuperBeam ? AstroCutterSuperBeamRadius : AstroCutterBeamRadius;
        int hitCount = Physics2D.CircleCast(start, beamRadius, safeDirection, CreatePhysicsQueryFilter(), AstroCutterHits, Mathf.Max(0.2f, range));
        float clippedRange = AstroCutterBeamBlocker.ResolveClippedRange(AstroCutterHits, hitCount, transform, ownerId, range);
        astroCutterDamagedViews.Clear();
        astroCutterDamagedObstacles.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hitInfo = AstroCutterHits[i];
            Collider2D hit = hitInfo.collider;
            if (hit == null)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            if (hitInfo.distance > clippedRange + 0.035f)
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable.photonView != null)
            {
                if (astroCutterDamagedViews.Add(deployable.photonView.ViewID))
                {
                    Vector2 point = hitInfo.point.sqrMagnitude > 0.001f ? hitInfo.point : (Vector2)deployable.transform.position;
                    deployable.photonView.RPC(
                        nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                        RpcTarget.MasterClient,
                        profile.ShieldDamage,
                        profile.HpDamage,
                        ownerId,
                        point.x,
                        point.y,
                        (int)profile.DamageType,
                        (int)profile.DeliveryMethod,
                        (int)profile.DeliveryFlags,
                        string.Empty);
                    AddSuperChargeForDamage();
                }

                continue;
            }

            PlayerHealth hp = hit.GetComponentInParent<PlayerHealth>();
            if (hp != null && hp.photonView != null && hp.GetComponent<LureBeaconDecoy>() == null && !hp.IsWreck && hp.photonView.ViewID != ownerId)
            {
                if (astroCutterDamagedViews.Add(hp.photonView.ViewID))
                {
                    Vector2 point = hitInfo.point.sqrMagnitude > 0.001f ? hitInfo.point : (Vector2)hp.transform.position;
                    hp.photonView.RPC(
                        nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                        RpcTarget.MasterClient,
                        profile.ShieldDamage,
                        profile.HpDamage,
                        ownerId,
                        point.x,
                        point.y,
                        (int)profile.DamageType,
                        (int)profile.DeliveryMethod,
                        (int)profile.DeliveryFlags,
                        string.Empty);
                    AddSuperChargeForDamage();
                }

                continue;
            }

            ObstacleChunk obstacleChunk = hit.GetComponentInParent<ObstacleChunk>();
            if (obstacleChunk != null &&
                !string.IsNullOrWhiteSpace(obstacleChunk.StableId) &&
                RoomSettings.AreObstaclesDestructible() &&
                astroCutterDamagedObstacles.Add(obstacleChunk.StableId))
            {
                int obstacleMultiplier = isSuperBeam ? AstroCutterSuperObstacleDamageMultiplier : AstroCutterObstacleDamageMultiplier;
                int obstacleDamage = Mathf.Max(1, profile.HpDamage * obstacleMultiplier);
                SpaceObjectMotionSync.RequestObstacleDamage(obstacleChunk.StableId, GetPilotAdjustedObstacleDamage(obstacleDamage));
            }
        }
    }

    int GetPilotAdjustedObstacleDamage(int baseDamage)
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        if (PilotCatalog.IsSelectedPilot(owner, PilotCatalog.SirNowitzkyId))
            return Mathf.CeilToInt(baseDamage * 1.5f);

        return baseDamage;
    }

    [PunRPC]
    void StartAstroCutterBeamRpc(int sourceViewId, float directionX, float directionY, float range, float duration, bool useFullWidth)
    {
        AstroCutterBeamVfx.StartBeam(sourceViewId, new Vector2(directionX, directionY), range, duration, useFullWidth);
    }

    public void AddSuperChargeForDamage()
    {
        if (!IsComplexShootingActive)
            return;

        superCharge = Mathf.Clamp01(superCharge + (SuperChargeOnComplexHit * GetSuperChargeDamageGainMultiplier()));
    }

    float GetSuperChargeGainMultiplier()
    {
        WeaponAttackProfile profile = activeComplexWeaponProfile ?? GetActiveComplexWeaponState()?.Profile;
        if (IsDoubleRocketLauncherProfile(profile))
            return DoubleRocketSuperChargeMultiplier;

        return IsAstroCutterProfile(profile) ? 0.5f : 1f;
    }

    float GetSuperChargeDamageGainMultiplier()
    {
        WeaponAttackProfile profile = activeComplexWeaponProfile ?? GetActiveComplexWeaponState()?.Profile;
        if (IsDoubleRocketLauncherProfile(profile))
            return DoubleRocketSuperChargeMultiplier;

        return IsAstroCutterProfile(profile) ? 0.25f : 1f;
    }

    Vector2 ResolveManualAimDirection()
    {
        simpleAutoAimTargetViewId = 0;
        simpleAutoAimHasTargetPoint = false;
        simpleAutoAimTargetPoint = default;
        Vector2 rawDirection = shootJoystick != null ? shootJoystick.inputVector : Vector2.zero;
        if (rawDirection.magnitude >= ManualAimThreshold)
            return rawDirection.normalized;

        return FindAutoAimDirection(out simpleAutoAimTargetViewId, out simpleAutoAimTargetPoint, out simpleAutoAimHasTargetPoint);
    }

    Vector2 FindAutoAimDirection()
    {
        return FindAutoAimDirection(out _);
    }

    Vector2 FindAutoAimDirection(out int targetViewId)
    {
        return FindAutoAimDirection(out targetViewId, out _, out _);
    }

    Vector2 FindAutoAimDirection(out int targetViewId, out Vector2 targetPoint, out bool hasTargetPoint)
    {
        targetViewId = 0;
        targetPoint = default;
        hasTargetPoint = false;
        float range = GetSimpleAutoAimRange();
        if (TryResolveAutoAimTarget(range, false, activeSimpleWeaponProfile, out AutoAimTargetResult target))
        {
            targetViewId = target.ViewId;
            targetPoint = target.AimPoint;
            hasTargetPoint = true;
            return ResolveSafeAimDirection(target.AimPoint - (Vector2)transform.position);
        }

        return transform.up;
    }

    float GetSimpleAutoAimRange()
    {
        float configuredRange = GetOwnerLengthForRange() * Mathf.Max(0.1f, bulletRangeMultiplier);
        return Mathf.Max(AutoAimRange, configuredRange);
    }

    bool Shoot(Vector2 direction, int homingTargetViewId = 0, bool hasAutoAimTargetPoint = false, Vector2 autoAimTargetPoint = default(Vector2))
    {
        if (bulletPrefab == null)
        {
            Debug.LogError("bulletPrefab NULL");
            return false;
        }

        PhotonView playerView = GetComponent<PhotonView>();
        int ownerId = playerView != null ? playerView.ViewID : 0;
        bool spawned = false;
        WeaponAttackProfile profile = activeSimpleWeaponProfile;

        if (IsAstroCutterProfile(profile))
        {
            bool firedBeam = TryStartAstroCutterBeam(profile, direction.normalized, ownerId);
            if (firedBeam)
                NotifyShotFiredForUseCancel();
            return firedBeam;
        }

        if (IsGatlingGunProfile(profile))
        {
            if (complexBurstRoutine != null)
                StopCoroutine(complexBurstRoutine);

            Vector2 safeDirection = ResolveSafeAimDirection(direction);
            Vector2 targetPoint = (Vector2)transform.position + (safeDirection * GetComplexRangeWorld(profile));
            bool pilotBreachSalvo = TryConsumePilotBreachSalvo();
            complexBurstRoutine = StartCoroutine(FireComplexBurst(profile, safeDirection, targetPoint, 0, pilotBreachSalvo));
            NotifyShotFiredForUseCancel();
            return true;
        }

        bool pilotBreachProjectile = TryConsumePilotBreachSalvo();
        if (IsRocketWeaponProfile(profile))
        {
            int count = Mathf.Max(1, profile.ProjectileCount);
            for (int i = 0; i < count; i++)
            {
                Vector2 shotDirection = IsDoubleRocketLauncherProfile(profile)
                    ? ResolveDoubleRocketProjectileDirection(direction.normalized, i, count, homingTargetViewId)
                    : ResolveComplexProjectileDirection(direction.normalized, profile.SpreadAngle, i, count);
                Vector3 spawnPos = ResolveComplexProjectileSpawnPosition(profile, shotDirection, i, count);
                Vector2 targetPoint = (Vector2)spawnPos + (shotDirection.normalized * GetComplexRangeWorld(profile));
                spawned |= SpawnComplexBullet(profile, shotDirection.normalized, spawnPos, ownerId, targetPoint, homingTargetViewId, pilotBreachProjectile);
            }
        }
        else if (ShouldUseArcSimpleShot(profile))
        {
            Vector3 spawnPos = transform.position + (transform.up * muzzleOffsetDistance);
            float range = GetComplexRangeWorld(profile);
            Vector2 targetPoint = hasAutoAimTargetPoint
                ? ClampAutoAimPointToRange((Vector2)transform.position, autoAimTargetPoint, range)
                : (Vector2)transform.position + (direction.normalized * range);
            spawned |= SpawnComplexBullet(profile, direction.normalized, spawnPos, ownerId, targetPoint, 0, pilotBreachProjectile);
        }
        else if (ShouldUseSimpleSpreadShot(profile))
        {
            Vector3 spawnPos = transform.position + (transform.up * muzzleOffsetDistance);
            int count = Mathf.Max(1, profile.ProjectileCount);
            for (int i = 0; i < count; i++)
            {
                Vector2 shotDirection = ResolveComplexProjectileDirection(direction.normalized, profile.SpreadAngle, i, count);
                spawned |= SpawnBullet(shotDirection, spawnPos, ownerId, pilotBreachProjectile);
            }
        }
        else if (ShouldFireFromDualWingMuzzles())
        {
            spawned |= SpawnBullet(direction, GetWingMuzzlePosition(-1f), ownerId, pilotBreachProjectile);
            spawned |= SpawnBullet(direction, GetWingMuzzlePosition(1f), ownerId, pilotBreachProjectile);
        }
        else
        {
            Vector3 spawnPos = transform.position + (transform.up * muzzleOffsetDistance);
            spawned |= SpawnBullet(direction, spawnPos, ownerId, pilotBreachProjectile);
        }

        if (spawned)
        {
            photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);
            NotifyShotFiredForUseCancel();
        }

        return spawned;
    }

    void NotifyShotFiredForUseCancel()
    {
        if (!photonView.IsMine || GetComponent<EnemyBot>() != null || IsNeutralRiderShip())
            return;

        CancelCloakDeviceForLocalOwner();
        CancelHackingDeviceForLocalOwner();

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.CancelCollectionForShot();
    }

    void CancelHackingDeviceForLocalOwner()
    {
        if (photonView == null || !photonView.IsMine)
            return;

        photonView.RPC(nameof(RequestCancelHackingDevice), RpcTarget.MasterClient);
    }

    void CancelCloakDeviceForLocalOwner()
    {
        if (photonView == null || !photonView.IsMine)
            return;

        HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>();
        if (nebulaTarget == null || !nebulaTarget.IsCloaked)
            return;

        photonView.RPC(nameof(CancelCloakDeviceRpc), RpcTarget.All);
    }

    bool ShouldUseSimpleSpreadShot(WeaponAttackProfile profile)
    {
        return profile != null &&
               string.Equals(profile.Id, WeaponAttackCatalog.TripleGunId, StringComparison.Ordinal);
    }

    bool ShouldUseArcSimpleShot(WeaponAttackProfile profile)
    {
        return IsArcWeaponProfile(profile);
    }

    bool IsArcWeaponProfile(WeaponAttackProfile profile)
    {
        return profile != null &&
               (profile.MarkerType == ComplexAttackMarkerType.Arc ||
                string.Equals(profile.Id, WeaponAttackCatalog.ArtilleryGunId, StringComparison.Ordinal));
    }

    bool IsArtillerySuperProfile(WeaponAttackProfile profile)
    {
        return profile != null &&
               string.Equals(profile.Id, WeaponAttackCatalog.ArtilleryGunId + "_super", StringComparison.Ordinal);
    }

    bool IsAstroCutterProfile(WeaponAttackProfile profile)
    {
        return profile != null &&
               !string.IsNullOrWhiteSpace(profile.Id) &&
               profile.Id.StartsWith(WeaponAttackCatalog.AstroCutterId, StringComparison.Ordinal);
    }

    bool IsAstroCutterSuperProfile(WeaponAttackProfile profile)
    {
        return profile != null &&
               string.Equals(profile.Id, WeaponAttackCatalog.AstroCutterId + "_super", StringComparison.Ordinal);
    }

    Vector2 ResolveArcTargetPointFromInput(WeaponAttackProfile profile, Vector2 rawInput)
    {
        float maxRange = GetComplexRangeWorld(profile);
        if (rawInput.sqrMagnitude <= 0.001f)
            return (Vector2)transform.position + ((Vector2)transform.up * maxRange);

        float distance = Mathf.Clamp01(rawInput.magnitude) * maxRange;
        return (Vector2)transform.position + (rawInput.normalized * distance);
    }

    float ResolveArcHeight(float range)
    {
        return Mathf.Max(1.2f, range * 0.22f);
    }

    public bool TryFireBot(Vector2 direction, int homingTargetViewId = 0)
    {
        if (GetComponent<EnemyBot>() == null && !IsNeutralRiderShip())
            return false;

        if (!photonView.IsMine || !IsGameStarted())
            return false;

        UpdateReload();

        if (Time.time < nextFireTime || direction.sqrMagnitude < 0.04f)
            return false;

        if (!infiniteAmmo && (isReloading || currentAmmo <= 0))
            return false;

        if (!Shoot(direction.normalized, homingTargetViewId))
            return false;

        if (!infiniteAmmo)
            ConsumeAmmo();
        nextFireTime = Time.time + fireRate * GetFireIntervalMultiplier();
        return true;
    }

    public bool TryFireBotAtPoint(Vector2 direction, Vector2 targetPoint, int homingTargetViewId = 0)
    {
        if (GetComponent<EnemyBot>() == null && !IsNeutralRiderShip())
            return false;

        if (!photonView.IsMine || !IsGameStarted())
            return false;

        UpdateReload();

        if (Time.time < nextFireTime || direction.sqrMagnitude < 0.04f)
            return false;

        if (!infiniteAmmo && (isReloading || currentAmmo <= 0))
            return false;

        if (!Shoot(direction.normalized, homingTargetViewId, true, targetPoint))
            return false;

        if (!infiniteAmmo)
            ConsumeAmmo();
        nextFireTime = Time.time + fireRate * GetFireIntervalMultiplier();
        return true;
    }

    public bool TryFireHackedRandom(Vector2 direction)
    {
        if (!photonView.IsMine || !IsGameStarted() || direction.sqrMagnitude < 0.04f)
            return false;

        if (GetComponent<EnemyBot>() != null || IsNeutralRiderShip())
            return TryFireBot(direction);

        if (AreShipControlsBlocked())
            return false;

        ResetComplexPressState();
        HideAimMarker();
        Vector2 safeDirection = ResolveSafeAimDirection(direction);

        if (IsComplexShootingActive && HasMainGunSlotsForCurrentShip())
        {
            WeaponAttackProfile profile = activeComplexWeaponProfile ?? SyncComplexWeaponProfile();
            if (profile == null)
                return false;

            UpdateComplexAmmoReload();
            Vector2 targetPoint = (Vector2)transform.position + (safeDirection * GetComplexRangeWorld(profile));
            return TryFireComplexAttack(profile, safeDirection, targetPoint, true, false, 0);
        }

        SyncEquippedWeaponProfile();
        SyncAmmoSetting();
        UpdateReload();

        if (Time.time < nextFireTime || isReloading || currentAmmo <= 0)
            return false;

        if (!Shoot(safeDirection))
            return false;

        ConsumeAmmo();
        nextFireTime = Time.time + fireRate * GetFireIntervalMultiplier();
        return true;
    }

    public bool TryFireBotFromWorld(Vector2 direction, Vector3 spawnPosition, float cooldownOffset = 0f)
    {
        if (GetComponent<EnemyBot>() == null && !IsNeutralRiderShip())
            return false;

        if (!photonView.IsMine || !IsGameStarted())
            return false;

        UpdateReload();

        if (Time.time < nextFireTime || direction.sqrMagnitude < 0.04f)
            return false;

        if (!infiniteAmmo && (isReloading || currentAmmo <= 0))
            return false;

        int ownerId = photonView != null ? photonView.ViewID : 0;
        bool spawned = SpawnBullet(direction.normalized, spawnPosition, ownerId);
        if (!spawned)
            return false;

        photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);
        if (!infiniteAmmo)
            ConsumeAmmo();
        nextFireTime = Time.time + (fireRate * GetFireIntervalMultiplier()) + Mathf.Max(0f, cooldownOffset);
        return true;
    }

    public bool TryFireBotVolleyFromWorld(Vector2 direction, Vector3[] spawnPositions, float cooldownOffset = 0f)
    {
        if (GetComponent<EnemyBot>() == null && !IsNeutralRiderShip())
            return false;

        if (!photonView.IsMine || !IsGameStarted())
            return false;

        UpdateReload();

        if (Time.time < nextFireTime || direction.sqrMagnitude < 0.04f || spawnPositions == null || spawnPositions.Length == 0)
            return false;

        if (!infiniteAmmo && (isReloading || currentAmmo <= 0))
            return false;

        int ownerId = photonView != null ? photonView.ViewID : 0;
        bool spawned = false;
        Vector2 normalizedDirection = direction.normalized;
        for (int i = 0; i < spawnPositions.Length; i++)
            spawned |= SpawnBullet(normalizedDirection, spawnPositions[i], ownerId);

        if (!spawned)
            return false;

        photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);
        if (!infiniteAmmo)
            ConsumeAmmo();
        nextFireTime = Time.time + (fireRate * GetFireIntervalMultiplier()) + Mathf.Max(0f, cooldownOffset);
        return true;
    }

    public bool TryFireBotVolleyAtPointFromWorld(Vector3[] spawnPositions, Vector2 targetPoint, float cooldownOffset = 0f)
    {
        if (GetComponent<EnemyBot>() == null && !IsNeutralRiderShip())
            return false;

        if (!photonView.IsMine || !IsGameStarted())
            return false;

        UpdateReload();

        if (Time.time < nextFireTime || spawnPositions == null || spawnPositions.Length == 0)
            return false;

        if (!infiniteAmmo && (isReloading || currentAmmo <= 0))
            return false;

        int ownerId = photonView != null ? photonView.ViewID : 0;
        bool spawned = false;
        for (int i = 0; i < spawnPositions.Length; i++)
        {
            Vector2 shotDirection = targetPoint - (Vector2)spawnPositions[i];
            if (shotDirection.sqrMagnitude < 0.04f)
                shotDirection = transform.up;

            spawned |= SpawnBullet(shotDirection.normalized, spawnPositions[i], ownerId);
        }

        if (!spawned)
            return false;

        photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);
        if (!infiniteAmmo)
            ConsumeAmmo();
        nextFireTime = Time.time + (fireRate * GetFireIntervalMultiplier()) + Mathf.Max(0f, cooldownOffset);
        return true;
    }

    public bool FireBotProjectileFromWorld(Vector2 direction, Vector3 spawnPosition)
    {
        if (GetComponent<EnemyBot>() == null && !IsNeutralRiderShip())
            return false;

        if (!photonView.IsMine || !IsGameStarted() || direction.sqrMagnitude < 0.04f)
            return false;

        int ownerId = photonView != null ? photonView.ViewID : 0;
        bool spawned = SpawnBullet(direction.normalized, spawnPosition, ownerId);
        if (spawned)
            photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);

        return spawned;
    }

    void ConsumeAmmo()
    {
        currentAmmo = Mathf.Max(0, currentAmmo - 1);
        if (currentAmmo <= 0)
        {
            StartReload(false);
        }
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
        if (IsNeutralRiderShip() || (customAmmoProfileActive && GetComponent<EnemyBot>() != null))
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
        if (baseWeaponProfileCaptured || GetComponent<EnemyBot>() != null || IsNeutralRiderShip())
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
        if (!photonView.IsMine || GetComponent<EnemyBot>() != null || IsNeutralRiderShip())
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
        if (!photonView.IsMine || GetComponent<EnemyBot>() != null || IsNeutralRiderShip())
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

        GameObject bullet = PhotonNetwork.Instantiate(
            bulletPrefab.name,
            spawnPos,
            Quaternion.identity,
            0,
            data
        );

        if (bullet == null)
        {
            Debug.LogError("Bullet failed to spawn");
            return false;
        }

        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent != null)
            bulletComponent.ownerViewID = ownerId;

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = direction * adjustedBulletSpeed;
        }
        else
        {
            Debug.LogError("Bullet is missing Rigidbody2D");
        }

        Collider2D playerCollider = GetComponent<Collider2D>();
        Collider2D bulletCollider = bullet.GetComponent<Collider2D>();
        if (bulletCollider != null && playerCollider != null)
            Physics2D.IgnoreCollision(bulletCollider, playerCollider);

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
        if (IsNeutralRiderShip() || (customAmmoProfileActive && GetComponent<EnemyBot>() != null))
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

        if (GetComponent<EnemyBot>() != null)
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

    static bool IsWeaponProfile(WeaponAttackProfile profile, string weaponId)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(weaponId))
            return false;

        return string.Equals(profile.Id, weaponId, StringComparison.Ordinal) ||
               profile.Id.StartsWith(weaponId + "_", StringComparison.Ordinal);
    }

    public void TriggerManualReload()
    {
        if (!CanManualReload)
            return;

        StartReload(true);
    }

    public void TriggerGadgetUse()
    {
        TriggerGadgetUse(CurrentGadgetItemId);
    }

    public void TriggerGadgetUse(string itemId)
    {
        if (!CanUseGadget(itemId))
            return;

        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (IsHoldGadget(itemId))
        {
            BeginGadgetUse(itemId);
            return;
        }

        if (gadgetStates.TryGetValue(itemId, out GadgetRuntimeState state) && state != null && state.Cooldown > 0f)
            state.NextUseTime = Time.time + state.Cooldown;

        photonView.RPC(nameof(RequestAuthoritativeGadgetUse), RpcTarget.MasterClient, itemId);
    }

    public void BeginGadgetUse(string itemId)
    {
        if (!CanUseGadget(itemId) || string.IsNullOrWhiteSpace(itemId))
            return;

        if (string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
        {
            photonView.RPC(nameof(RequestStartTractorBeam), RpcTarget.MasterClient, itemId);
            return;
        }

        TriggerGadgetUse(itemId);
    }

    public void EndGadgetUse(string itemId)
    {
        if (string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
            photonView.RPC(nameof(RequestStopTractorBeam), RpcTarget.MasterClient, itemId);
    }

    public void CancelActiveGadgetEffectsForShipLoss()
    {
        ResetComplexPressState();
        HideAimMarker();
        CancelCloakDeviceForLocalOwner();

        if (PhotonNetwork.IsMasterClient)
        {
            StopAuthoritativeTractorBeam(true);
            StopAuthoritativeHackingDevice(true);
            StopAuthoritativeLootHookWindup();
        }
        else if (photonView != null && photonView.IsMine)
        {
            photonView.RPC(nameof(RequestStopTractorBeam), RpcTarget.MasterClient, InventoryItemCatalog.TractorBeamId);
            photonView.RPC(nameof(RequestCancelHackingDevice), RpcTarget.MasterClient);
        }
    }

    public bool IsHoldGadget(string itemId)
    {
        return string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal);
    }

    public void ConfigureWeaponProfile(
        float configuredFireRate,
        int configuredMaxAmmo,
        float configuredReloadDuration,
        int configuredBulletDamage,
        float configuredBulletScaleMultiplier,
        Color configuredBulletColor,
        float configuredMuzzleOffsetDistance,
        bool configuredInfiniteAmmo,
        float configuredBulletSpeed = -1f,
        string configuredShotSoundId = "",
        float configuredRangeMultiplier = -1f,
        string configuredHitEffectId = "",
        float configuredFlightTime = 10f,
        WeaponDamageType configuredDamageType = WeaponDamageType.Laser,
        WeaponDeliveryMethod configuredDeliveryMethod = WeaponDeliveryMethod.DirectProjectile,
        WeaponDeliveryFlags configuredDeliveryFlags = WeaponDeliveryFlags.None)
    {
        customAmmoProfileActive = true;
        fireRate = Mathf.Max(0.05f, configuredFireRate);
        maxAmmo = Mathf.Max(1, configuredMaxAmmo);
        reloadDuration = Mathf.Max(0f, configuredReloadDuration);
        bulletDamage = Mathf.Max(0, configuredBulletDamage);
        bulletScaleMultiplier = Mathf.Max(0.25f, configuredBulletScaleMultiplier);
        bulletColor = configuredBulletColor;
        muzzleOffsetDistance = Mathf.Max(0f, configuredMuzzleOffsetDistance);
        infiniteAmmo = configuredInfiniteAmmo;
        shotSoundId = configuredShotSoundId ?? string.Empty;

        if (configuredBulletSpeed > 0f)
            bulletSpeed = configuredBulletSpeed;

        if (configuredRangeMultiplier > 0f)
            bulletRangeMultiplier = configuredRangeMultiplier;

        simpleUsesDamageProfile = !string.IsNullOrWhiteSpace(configuredHitEffectId);
        simpleShieldDamage = bulletDamage;
        simpleHpDamage = bulletDamage;
        simplePierces = false;
        simpleAreaDamageRadius = 0f;
        simpleHitEffectId = configuredHitEffectId ?? string.Empty;
        simpleFlightTime = Mathf.Clamp(configuredFlightTime, 0.2f, 30f);
        simpleDamageType = configuredDamageType;
        simpleDeliveryMethod = configuredDeliveryMethod;
        simpleDeliveryFlags = configuredDeliveryFlags;

        isReloading = false;
        reloadFinishTime = 0f;
        currentAmmo = maxAmmo;
    }

    [PunRPC]
    void PlayLaserSfx()
    {
        PlayShotSfx(shotSoundId ?? string.Empty);
    }

    [PunRPC]
    void PlayShotSfx(string soundId)
    {
        if (soundId == "shoot_small")
        {
            AudioManager.Instance.PlayShootSmallAt(transform.position);
            return;
        }

        if (soundId == "artillery")
        {
            AudioManager.Instance.PlayArtilleryGunAt(transform.position);
            return;
        }

        if (soundId == "gatling")
        {
            AudioManager.Instance.PlayGatlingGunAt(transform.position);
            return;
        }

        if (soundId == "corsair")
        {
            AudioManager.Instance.PlayCorsairLaserAt(transform.position);
            return;
        }

        if (soundId == "lazer1")
        {
            AudioManager.Instance.PlayLazer1At(transform.position);
            return;
        }

        if (soundId == "lazer2")
        {
            AudioManager.Instance.PlayLazer2At(transform.position);
            return;
        }

        if (soundId == "lazer3")
        {
            AudioManager.Instance.PlayLazer3At(transform.position);
            return;
        }

        if (soundId == "pirate_fighter")
        {
            AudioManager.Instance.PlayPirateFighterShotAt(transform.position);
            return;
        }

        if (soundId == "astro_cutter")
        {
            AudioManager.Instance.PlayAstroCutterAt(transform.position);
            return;
        }

        if (soundId == "rocket")
        {
            AudioManager.Instance.PlayRocketLaunchAt(transform.position);
            return;
        }

        if (soundId == "cosmic_worm")
        {
            AudioManager.Instance.PlayCosmicWormShotAt(transform.position);
            return;
        }

        AudioManager.Instance.PlayLaserAt(transform.position);
    }

    [PunRPC]
    void PlayReloadSfx()
    {
        AudioManager.Instance.PlayReloadAt(transform.position);
    }

    bool TryDeployGadgetMine()
    {
        EnemyBotDefinition mineDefinition = EnemyBotCatalog.GetDefinition(EnemyBotKind.SpaceMine);
        if (mineDefinition == null)
            return false;

        Vector3 spawnPosition = ResolveGadgetMineSpawnPosition();
        GameObject mineObject = PhotonNetwork.Instantiate(
            "Player",
            spawnPosition,
            transform.rotation,
            0,
            new object[]
            {
                mineDefinition.InstantiationMarker,
                "player_gadget_mine",
                photonView != null ? photonView.ViewID : 0
            });

        if (mineObject != null)
        {
            EnemyBot bot = mineObject.GetComponent<EnemyBot>();
            if (bot == null)
                bot = mineObject.AddComponent<EnemyBot>();

            bot.InitializeFromPhotonData();
        }

        return true;
    }

    Vector3 ResolveGadgetMineSpawnPosition()
    {
        float baseDistance = GetGadgetMinePlacementDistance();
        Vector2 forward = transform.up.sqrMagnitude > 0.001f ? (Vector2)transform.up.normalized : Vector2.up;
        Vector2 right = transform.right.sqrMagnitude > 0.001f ? (Vector2)transform.right.normalized : Vector2.right;
        Vector2 origin = transform.position;
        Vector2[] directions =
        {
            -forward,
            right,
            -right,
            (-forward + right).normalized,
            (-forward - right).normalized,
            forward
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2 candidate = origin + (directions[i] * baseDistance);
            if (IsMineSpawnPositionFree(candidate))
                return new Vector3(candidate.x, candidate.y, 0f);
        }

        Vector2 fallback = origin - (forward * baseDistance);
        return new Vector3(fallback.x, fallback.y, 0f);
    }

    float GetGadgetMinePlacementDistance()
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            float shipExtent = Mathf.Max(spriteRenderer.bounds.extents.x, spriteRenderer.bounds.extents.y);
            return Mathf.Max(0.9f, shipExtent + 0.55f);
        }

        Collider2D collider2D = GetComponentInChildren<Collider2D>();
        if (collider2D != null)
            return Mathf.Max(0.9f, Mathf.Max(collider2D.bounds.extents.x, collider2D.bounds.extents.y) + 0.55f);

        return 1.15f;
    }

    bool IsMineSpawnPositionFree(Vector2 candidate)
    {
        int hitCount = Physics2D.OverlapCircle(candidate, 0.38f, CreatePhysicsQueryFilter(), SpawnClearanceHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = SpawnClearanceHits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }

    bool TryActivateBatteryCharge()
    {
        PlayerHealth health = GetComponent<PlayerHealth>();
        return health != null && health.TryBeginBatteryShieldChargeAuthority();
    }

    public bool CanUseGadget(string itemId)
    {
        if (!photonView.IsMine || !IsGameStarted() || string.IsNullOrWhiteSpace(itemId))
            return false;

        if (AreShipControlsBlocked())
            return false;

        if (!gadgetStates.TryGetValue(itemId, out GadgetRuntimeState state) || state == null)
            return false;

        if (state.RemainingCharges <= 0 || Time.time < state.NextUseTime)
            return false;

        if (string.Equals(itemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            return health != null && health.CanActivateBatteryChargeLocally();
        }

        if (string.Equals(itemId, InventoryItemCatalog.DropbotId, StringComparison.Ordinal))
            return HasUsableDropbotCargoLocally();

        if (string.Equals(itemId, InventoryItemCatalog.LootHookId, StringComparison.Ordinal))
            return PlayerProfileService.HasInstance && PlayerProfileService.Instance.HasFreeShipInventorySlot();

        if (string.Equals(itemId, InventoryItemCatalog.ShortScannerId, StringComparison.Ordinal) &&
            ShortScannerRevealStatus.IsActive)
        {
            return false;
        }

        if (string.Equals(itemId, InventoryItemCatalog.CloakDeviceId, StringComparison.Ordinal))
        {
            HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>();
            return nebulaTarget == null || !nebulaTarget.IsCloaked;
        }

        if (string.Equals(itemId, InventoryItemCatalog.HackingDeviceId, StringComparison.Ordinal))
            return HasHackingDeviceCandidate();

        if (string.Equals(itemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal) &&
            IsShipDamageActive(ShipDamageType.Booster))
        {
            ShipDamageState damageState = GetComponent<ShipDamageState>();
            if (damageState != null && damageState.IsBoosterDisabled())
                return false;
        }

        return true;
    }

    bool HasUsableDropbotCargoLocally()
    {
        if (!PlayerProfileService.HasInstance)
            return false;

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        PlayerInventoryData inventory = profile != null ? profile.Inventory : null;
        if (inventory == null || !PlayerProfileService.Instance.HasFreePlayerInventorySlot())
            return false;

        inventory.Normalize();
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        int baseCapacity = PlayerProfileService.GetEffectiveShipInventoryCapacity(shipSkinIndex, inventory.EquipmentSlots);
        ShipDamageState damageState = GetComponent<ShipDamageState>();
        int shipCapacity = damageState != null ? damageState.GetAdjustedCargoCapacity(baseCapacity) : ShipDamageState.GetLocalCargoAdjustedCapacity(baseCapacity);
        int dropSlotIndex = PlayerProfileService.GetDropbotCargoSlotIndex(shipSkinIndex, shipCapacity, inventory.EquipmentSlots);
        return dropSlotIndex >= 0 &&
               dropSlotIndex < inventory.ShipSlots.Length &&
               !string.IsNullOrWhiteSpace(inventory.ShipSlots[dropSlotIndex]);
    }

    bool AreShipControlsBlocked()
    {
        if (GetComponent<EnemyBot>() != null)
            return false;

        if (AstronautSurvivor.IsAstronautInstantiationData(photonView != null ? photonView.InstantiationData : null) ||
            GetComponent<AstronautSurvivor>() != null)
        {
            return true;
        }

        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null && (health.IsWreck || health.IsAstronautControlled || health.IsEvacuationAnimating || health.CurrentHP <= 0))
            return true;

        PlayerRepairDocking repairDocking = GetComponent<PlayerRepairDocking>();
        return repairDocking != null && repairDocking.IsBusy;
    }

    public int GetRemainingGadgetCharges(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !gadgetStates.TryGetValue(itemId, out GadgetRuntimeState state) || state == null)
            return 0;

        return Mathf.Max(0, state.RemainingCharges);
    }

    public int GetMaxGadgetCharges(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !gadgetStates.TryGetValue(itemId, out GadgetRuntimeState state) || state == null)
            return 0;

        return Mathf.Max(0, state.MaxCharges);
    }

    public Sprite GetGadgetIcon(string itemId)
    {
        return string.IsNullOrWhiteSpace(itemId) ? null : InventoryItemCatalog.GetIcon(itemId);
    }

    public string GetGadgetButtonLabel(string itemId)
    {
        return InventoryItemCatalog.GetShortLabel(itemId);
    }

    public Color GetGadgetButtonColor(string itemId)
    {
        if (string.Equals(itemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return new Color(0.14f, 0.5f, 0.28f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.SpaceBombId, StringComparison.Ordinal))
            return new Color(0.78f, 0.22f, 0.12f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.DropbotId, StringComparison.Ordinal))
            return new Color(0.08f, 0.48f, 0.72f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
            return new Color(0.16f, 0.46f, 0.78f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.MagneticBeamId, StringComparison.Ordinal))
            return new Color(0.08f, 0.36f, 0.86f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
            return new Color(0.72f, 0.5f, 0.08f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.LootHookId, StringComparison.Ordinal))
            return new Color(0.76f, 0.42f, 0.08f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.StasisBuoyId, StringComparison.Ordinal))
            return new Color(0.2f, 0.62f, 0.88f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.TetherHarpoonId, StringComparison.Ordinal))
            return new Color(0.86f, 0.68f, 0.12f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.SpaceTorpedoId, StringComparison.Ordinal))
            return new Color(0.84f, 0.28f, 0.14f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.BioTrapId, StringComparison.Ordinal))
            return new Color(0.34f, 0.74f, 0.32f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.AsteroidBreacherBombId, StringComparison.Ordinal))
            return new Color(0.82f, 0.44f, 0.12f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.MetalDriftWallId, StringComparison.Ordinal))
            return new Color(0.34f, 0.42f, 0.48f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.LureBeaconId, StringComparison.Ordinal))
            return new Color(0.68f, 0.2f, 0.72f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal))
            return new Color(0.72f, 0.22f, 0.18f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.RocketAutoTurretId, StringComparison.Ordinal))
            return new Color(0.62f, 0.24f, 0.12f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.GuidanceSystemId, StringComparison.Ordinal))
            return new Color(0.18f, 0.62f, 0.58f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.ShortScannerId, StringComparison.Ordinal))
            return new Color(0.1f, 0.54f, 0.78f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.CloakDeviceId, StringComparison.Ordinal))
            return new Color(0.22f, 0.34f, 0.5f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.HackingDeviceId, StringComparison.Ordinal))
            return new Color(0.1f, 0.58f, 0.72f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.LootingFriendId, StringComparison.Ordinal))
            return new Color(0.74f, 0.58f, 0.12f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.SpaceDrillId, StringComparison.Ordinal))
            return new Color(0.86f, 0.54f, 0.12f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.SpaceTrapId, StringComparison.Ordinal))
            return new Color(0.62f, 0.18f, 0.18f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
            return new Color(0.18f, 0.48f, 0.86f, 0.96f);

        return new Color(0.22f, 0.3f, 0.4f, 0.94f);
    }

    float ResolveGadgetCooldown(string gadgetItemId)
    {
        if (string.Equals(gadgetItemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return GadgetMinePlacementCooldown;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.ShortScannerId, StringComparison.Ordinal))
            return GetAdjustedShortScannerRevealDuration();

        if (string.Equals(gadgetItemId, InventoryItemCatalog.HackingDeviceId, StringComparison.Ordinal))
            return HackingDeviceWindupDuration;

        return 0f;
    }

    int ResolveGadgetMaxCharges(string gadgetItemId, int equippedCount)
    {
        equippedCount = Mathf.Max(0, equippedCount);
        if (equippedCount <= 0)
            return 0;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return GadgetMineDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.SpaceBombId, StringComparison.Ordinal))
            return SpaceBombDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.DropbotId, StringComparison.Ordinal))
            return DropbotDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
        {
            int charges = BatteryDefaultCharges * equippedCount;
            if (ShouldApplyRobyBatteryBonus())
                charges += equippedCount;

            return charges;
        }

        if (string.Equals(gadgetItemId, InventoryItemCatalog.MagneticBeamId, StringComparison.Ordinal))
            return MagneticBeamDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
            return TractorBeamDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.LootHookId, StringComparison.Ordinal))
            return LootHookDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.StasisBuoyId, StringComparison.Ordinal))
            return StasisBuoyDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.TetherHarpoonId, StringComparison.Ordinal))
            return TetherHarpoonDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.SpaceTorpedoId, StringComparison.Ordinal))
            return SpaceTorpedoDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.BioTrapId, StringComparison.Ordinal))
            return BioTrapDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.AsteroidBreacherBombId, StringComparison.Ordinal))
            return AsteroidBreacherBombDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.MetalDriftWallId, StringComparison.Ordinal))
            return MetalDriftWallDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.LureBeaconId, StringComparison.Ordinal))
        {
            int charges = LureBeaconDefaultCharges * equippedCount;
            if (ShouldApplyRoburTrapGadgetBonus())
                charges *= 2;

            return charges;
        }

        if (string.Equals(gadgetItemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal))
            return AutoTurretDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.RocketAutoTurretId, StringComparison.Ordinal))
            return RocketAutoTurretDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.GuidanceSystemId, StringComparison.Ordinal))
            return GuidanceSystemDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.ShortScannerId, StringComparison.Ordinal))
            return ShortScannerDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.CloakDeviceId, StringComparison.Ordinal))
            return CloakDeviceDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.HackingDeviceId, StringComparison.Ordinal))
            return HackingDeviceDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.SpaceDrillId, StringComparison.Ordinal))
            return SpaceDrillDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.SpaceTrapId, StringComparison.Ordinal))
        {
            int charges = SpaceTrapDefaultCharges * equippedCount;
            if (ShouldApplyRoburTrapGadgetBonus())
                charges *= 2;

            return charges;
        }

        if (string.Equals(gadgetItemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
            return SuperBoosterDefaultCharges * equippedCount;

        return 0;
    }

    public bool TryRestoreOneMissingGadgetChargeOnMaster()
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null || photonView.Owner == null)
            return false;

        Photon.Realtime.Player owner = photonView.Owner;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        List<string> orderedItems = new List<string>();
        Dictionary<string, int> gadgetCounts = CollectEquippedGadgetCounts(equipmentSlots, shipSkinIndex, orderedItems);
        if (orderedItems.Count == 0)
            return false;

        List<string> missingChargeItems = new List<string>();
        for (int i = 0; i < orderedItems.Count; i++)
        {
            string itemId = orderedItems[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            int equippedCount = gadgetCounts.TryGetValue(itemId, out int count) ? count : 0;
            int maxCharges = ResolveGadgetMaxCharges(itemId, equippedCount);
            if (maxCharges <= 0)
                continue;

            int remainingCharges = GetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, itemId, maxCharges);
            if (remainingCharges < maxCharges)
                missingChargeItems.Add(itemId);
        }

        if (missingChargeItems.Count == 0)
            return false;

        string selectedItemId = missingChargeItems.Count == 1
            ? missingChargeItems[0]
            : missingChargeItems[UnityEngine.Random.Range(0, missingChargeItems.Count)];

        int selectedEquippedCount = gadgetCounts.TryGetValue(selectedItemId, out int selectedCount) ? selectedCount : 0;
        int selectedMaxCharges = ResolveGadgetMaxCharges(selectedItemId, selectedEquippedCount);
        int selectedRemainingCharges = GetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, selectedItemId, selectedMaxCharges);
        if (selectedMaxCharges <= 0 || selectedRemainingCharges >= selectedMaxCharges)
            return false;

        SetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, selectedItemId, selectedRemainingCharges + 1, selectedMaxCharges);
        return true;
    }

    bool ShouldApplyRoburTrapGadgetBonus()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return PilotCatalog.IsSelectedPilot(owner, PilotCatalog.RoburId);
    }

    bool ShouldApplyRobyBatteryBonus()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return PilotCatalog.IsSelectedPilot(owner, PilotCatalog.RobyId);
    }

    [PunRPC]
    void RequestAuthoritativeGadgetUse(string itemId, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || !IsGameStarted() || string.IsNullOrWhiteSpace(itemId))
            return;

        if (AreShipControlsBlocked())
            return;

        if (photonView == null || photonView.Owner == null || messageInfo.Sender == null || messageInfo.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        Photon.Realtime.Player owner = photonView.Owner;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        List<string> orderedItems = new List<string>();
        Dictionary<string, int> gadgetCounts = CollectEquippedGadgetCounts(equipmentSlots, shipSkinIndex, orderedItems);
        int equippedCount = gadgetCounts.TryGetValue(itemId, out int count) ? count : 0;
        int maxCharges = ResolveGadgetMaxCharges(itemId, equippedCount);
        if (maxCharges <= 0)
            return;

        int remainingCharges = GetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, itemId, maxCharges);
        if (remainingCharges <= 0)
            return;

        if (string.Equals(itemId, InventoryItemCatalog.DropbotId, StringComparison.Ordinal))
        {
            TryBeginDropbotLaunchRequest(owner, itemId);
            return;
        }

        if (string.Equals(itemId, InventoryItemCatalog.LootHookId, StringComparison.Ordinal))
        {
            TryBeginLootHookRequest(owner, itemId);
            return;
        }

        if (!TryExecuteAuthoritativeGadgetUse(itemId))
            return;

        SetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, itemId, remainingCharges - 1, maxCharges);
    }

    bool TryBeginDropbotLaunchRequest(Photon.Realtime.Player owner, string itemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            photonView == null ||
            owner == null ||
            !string.Equals(itemId, InventoryItemCatalog.DropbotId, StringComparison.Ordinal))
        {
            return false;
        }

        if (pendingDropbotLaunchActive)
        {
            if (Time.time <= pendingDropbotLaunchExpiresAt)
                return false;

            ClearPendingDropbotLaunch();
        }

        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        int baseCapacity = PlayerProfileService.GetPlayerShipInventoryCapacity(owner);
        ShipDamageState damageState = GetComponent<ShipDamageState>();
        int shipCapacity = damageState != null ? damageState.GetAdjustedCargoCapacity(baseCapacity) : baseCapacity;
        int dropSlotIndex = PlayerProfileService.GetDropbotCargoSlotIndex(shipSkinIndex, shipCapacity, equipmentSlots);
        if (dropSlotIndex < 0)
            return false;

        string[] shipSlots = PlayerProfileService.GetPlayerShipInventorySlots(owner);
        if (dropSlotIndex >= shipSlots.Length)
            return false;

        string carriedItemId = shipSlots[dropSlotIndex];
        if (string.IsNullOrWhiteSpace(carriedItemId))
            return false;

        pendingDropbotLaunchRequestId++;
        if (pendingDropbotLaunchRequestId <= 0)
            pendingDropbotLaunchRequestId = 1;

        pendingDropbotLaunchActive = true;
        pendingDropbotCargoSlotIndex = dropSlotIndex;
        pendingDropbotCargoItemId = carriedItemId;
        pendingDropbotOwnerActorNumber = owner.ActorNumber;
        pendingDropbotLaunchExpiresAt = Time.time + DropbotLaunchRequestTimeout;
        photonView.RPC(nameof(PrepareDropbotCargoLaunchRpc), owner, pendingDropbotLaunchRequestId, dropSlotIndex, carriedItemId);
        return true;
    }

    [PunRPC]
    async void PrepareDropbotCargoLaunchRpc(int requestId, int slotIndex, string expectedItemId)
    {
        if (photonView == null || !photonView.IsMine)
            return;

        string removedItemId = null;
        bool removed = false;
        try
        {
            removedItemId = await PlayerProfileService.Instance.RemoveDropbotCargoItemDeferredSaveAsync(slotIndex, expectedItemId);
            removed = string.Equals(removedItemId, expectedItemId, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Debug.LogError("Dropbot cargo pickup failed: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ConfirmDropbotCargoLaunchRpc), RpcTarget.MasterClient, requestId, slotIndex, removedItemId ?? expectedItemId ?? string.Empty, removed);
    }

    [PunRPC]
    void ConfirmDropbotCargoLaunchRpc(int requestId, int slotIndex, string itemId, bool removed, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null)
            return;

        bool valid =
            pendingDropbotLaunchActive &&
            requestId == pendingDropbotLaunchRequestId &&
            slotIndex == pendingDropbotCargoSlotIndex &&
            messageInfo.Sender != null &&
            messageInfo.Sender.ActorNumber == pendingDropbotOwnerActorNumber &&
            string.Equals(itemId, pendingDropbotCargoItemId, StringComparison.Ordinal);

        if (!valid)
        {
            if (removed && messageInfo.Sender != null && !string.IsNullOrWhiteSpace(itemId))
                photonView.RPC(nameof(RestoreDropbotCargoItemRpc), messageInfo.Sender, slotIndex, itemId);
            return;
        }

        if (!removed)
        {
            ClearPendingDropbotLaunch();
            return;
        }

        Photon.Realtime.Player owner = photonView.Owner;
        int maxCharges = ResolveEquippedGadgetMaxCharges(owner, InventoryItemCatalog.DropbotId);
        int remainingCharges = owner != null ? GetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, InventoryItemCatalog.DropbotId, maxCharges) : 0;
        if (maxCharges <= 0 || remainingCharges <= 0)
        {
            photonView.RPC(nameof(RestoreDropbotCargoItemRpc), messageInfo.Sender, slotIndex, itemId);
            ClearPendingDropbotLaunch();
            return;
        }

        bool launched = NewItemsRuntime.TryLaunchDropbot(this, itemId);
        if (!launched)
        {
            photonView.RPC(nameof(RestoreDropbotCargoItemRpc), messageInfo.Sender, slotIndex, itemId);
            ClearPendingDropbotLaunch();
            return;
        }

        SetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, InventoryItemCatalog.DropbotId, remainingCharges - 1, maxCharges);
        RoundXpTracker.RecordGadgetSuccess(owner, InventoryItemCatalog.DropbotId);
        ClearPendingDropbotLaunch();
    }

    [PunRPC]
    async void RestoreDropbotCargoItemRpc(int slotIndex, string itemId)
    {
        if (photonView == null || !photonView.IsMine || string.IsNullOrWhiteSpace(itemId))
            return;

        try
        {
            await PlayerProfileService.Instance.RestoreShipItemAtAsync(slotIndex, itemId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Dropbot cargo restore failed: " + ex);
        }
    }

    void ClearPendingDropbotLaunch()
    {
        pendingDropbotLaunchActive = false;
        pendingDropbotCargoSlotIndex = -1;
        pendingDropbotOwnerActorNumber = 0;
        pendingDropbotCargoItemId = null;
        pendingDropbotLaunchExpiresAt = 0f;
    }

    bool TryBeginLootHookRequest(Photon.Realtime.Player owner, string itemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            photonView == null ||
            owner == null ||
            !string.Equals(itemId, InventoryItemCatalog.LootHookId, StringComparison.Ordinal))
        {
            return false;
        }

        if (authoritativeLootHookRoutine != null)
            return false;

        if (pendingLootHookActive)
        {
            if (Time.time <= pendingLootHookExpiresAt)
                return false;

            RestorePendingNeutralLootHookCargo();
            ClearPendingLootHook();
        }

        if (!TryFindLootHookTarget(owner, out PlayerShooting victim, out int slotIndex, out string stolenItemId))
        {
            if (TryBeginNeutralRiderLootHookRequest(owner))
                return true;

            return false;
        }

        return TryStartLootHookWindup(owner, victim.photonView, slotIndex, stolenItemId);
    }

    bool TryBeginNeutralRiderLootHookRequest(Photon.Realtime.Player owner)
    {
        if (!TryFindNeutralRiderLootHookTarget(owner, out NeutralRiderController neutralVictim, out string stolenItemId) ||
            neutralVictim == null ||
            neutralVictim.photonView == null)
        {
            return false;
        }

        return TryStartLootHookWindup(owner, neutralVictim.photonView, LootHookNeutralCargoSlotIndex, stolenItemId);
    }

    bool TryStartLootHookWindup(Photon.Realtime.Player owner, PhotonView targetView, int slotIndex, string stolenItemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            owner == null ||
            targetView == null ||
            string.IsNullOrWhiteSpace(stolenItemId) ||
            authoritativeLootHookRoutine != null ||
            !IsLootHookTargetStillValid(owner, targetView, slotIndex, stolenItemId, LootHookRange))
        {
            return false;
        }

        activeLootHookTargetViewId = targetView.ViewID;
        activeLootHookCargoSlotIndex = slotIndex;
        activeLootHookCargoItemId = stolenItemId;
        authoritativeLootHookRoutine = StartCoroutine(LootHookWindupRoutine(targetView.ViewID, slotIndex, stolenItemId));
        return true;
    }

    IEnumerator LootHookWindupRoutine(int targetViewId, int slotIndex, string stolenItemId)
    {
        float startedAt = Time.time;
        WaitForSeconds wait = new WaitForSeconds(LootHookValidationInterval);

        while (Time.time - startedAt < LootHookWindupDuration)
        {
            PhotonView targetView = PhotonView.Find(targetViewId);
            if (!IsActiveLootHookWindup(targetViewId, slotIndex, stolenItemId) ||
                !IsLootHookTargetStillValid(photonView != null ? photonView.Owner : null, targetView, slotIndex, stolenItemId, LootHookRange))
            {
                authoritativeLootHookRoutine = null;
                ClearActiveLootHookWindup();
                yield break;
            }

            yield return wait;
        }

        authoritativeLootHookRoutine = null;

        PhotonView completedTargetView = PhotonView.Find(targetViewId);
        if (IsActiveLootHookWindup(targetViewId, slotIndex, stolenItemId) &&
            IsLootHookTargetStillValid(photonView != null ? photonView.Owner : null, completedTargetView, slotIndex, stolenItemId, LootHookRange))
        {
            TryCompleteLootHookWindup(completedTargetView, slotIndex, stolenItemId);
        }

        ClearActiveLootHookWindup();
    }

    bool TryCompleteLootHookWindup(PhotonView targetView, int slotIndex, string stolenItemId)
    {
        if (!PhotonNetwork.IsMasterClient || targetView == null || photonView == null)
            return false;

        Photon.Realtime.Player owner = photonView.Owner;
        if (!IsLootHookTargetStillValid(owner, targetView, slotIndex, stolenItemId, LootHookRange))
            return false;

        if (slotIndex == LootHookNeutralCargoSlotIndex)
        {
            NeutralRiderController neutralVictim = targetView.GetComponent<NeutralRiderController>();
            return TryCompleteNeutralRiderLootHookRequest(owner, neutralVictim, stolenItemId);
        }

        PlayerShooting victim = targetView.GetComponent<PlayerShooting>();
        return BeginLootHookVictimCargoRequest(victim, slotIndex, stolenItemId);
    }

    bool BeginLootHookVictimCargoRequest(PlayerShooting victim, int slotIndex, string stolenItemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            victim == null ||
            victim.photonView == null ||
            victim.photonView.Owner == null ||
            string.IsNullOrWhiteSpace(stolenItemId))
        {
            return false;
        }

        pendingLootHookRequestId++;
        if (pendingLootHookRequestId <= 0)
            pendingLootHookRequestId = 1;

        pendingLootHookActive = true;
        pendingLootHookVictimViewId = victim.photonView.ViewID;
        pendingLootHookVictimActorNumber = victim.photonView.Owner.ActorNumber;
        pendingLootHookCargoSlotIndex = slotIndex;
        pendingLootHookCargoItemId = stolenItemId;
        pendingLootHookExpiresAt = Time.time + LootHookRequestTimeout;
        victim.photonView.RPC(nameof(PrepareLootHookVictimCargoRpc), victim.photonView.Owner, pendingLootHookRequestId, slotIndex, stolenItemId, photonView.ViewID);
        return true;
    }

    bool TryCompleteNeutralRiderLootHookRequest(Photon.Realtime.Player owner, NeutralRiderController neutralVictim, string stolenItemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            owner == null ||
            neutralVictim == null ||
            neutralVictim.photonView == null ||
            string.IsNullOrWhiteSpace(stolenItemId) ||
            !IsLootHookTargetStillValid(owner, neutralVictim.photonView, LootHookNeutralCargoSlotIndex, stolenItemId, LootHookRange))
        {
            return false;
        }

        if (!neutralVictim.TryRemoveLootHookCargo(stolenItemId, out string removedItemId) ||
            string.IsNullOrWhiteSpace(removedItemId))
        {
            return false;
        }

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(owner, removedItemId))
        {
            neutralVictim.RestoreLootHookCargo(removedItemId);
            return false;
        }

        pendingLootHookRequestId++;
        if (pendingLootHookRequestId <= 0)
            pendingLootHookRequestId = 1;

        pendingLootHookActive = true;
        pendingLootHookVictimViewId = neutralVictim.photonView.ViewID;
        pendingLootHookVictimActorNumber = 0;
        pendingLootHookCargoSlotIndex = LootHookNeutralCargoSlotIndex;
        pendingLootHookCargoItemId = removedItemId;
        pendingLootHookExpiresAt = Time.time + LootHookRequestTimeout;
        photonView.RPC(nameof(ReceiveLootHookStolenItemRpc), photonView.Owner, pendingLootHookRequestId, removedItemId, neutralVictim.photonView.ViewID, LootHookNeutralCargoSlotIndex);
        return true;
    }

    bool TryFindLootHookTarget(Photon.Realtime.Player owner, out PlayerShooting victim, out int slotIndex, out string itemId)
    {
        victim = null;
        slotIndex = -1;
        itemId = string.Empty;

        if (owner == null)
            return false;

        int ownerActorNumber = owner.ActorNumber;
        Vector2 sourcePosition = transform.position;
        float bestDistance = float.MaxValue;
        PlayerHealth[] healths = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidateHealth = healths[i];
            if (candidateHealth == null ||
                candidateHealth == GetComponent<PlayerHealth>() ||
                candidateHealth.IsWreck ||
                candidateHealth.IsBotControlled ||
                candidateHealth.IsNeutralRiderControlled ||
                candidateHealth.IsAstronautControlled ||
                candidateHealth.IsEvacuationAnimating ||
                candidateHealth.photonView == null ||
                candidateHealth.photonView.Owner == null ||
                candidateHealth.photonView.Owner.ActorNumber == ownerActorNumber)
            {
                continue;
            }

            float distance = Vector2.Distance(sourcePosition, candidateHealth.transform.position);
            if (distance > LootHookRange)
                continue;

            if (!TrySelectLootHookCargo(owner, candidateHealth.photonView.Owner, out int candidateSlot, out string candidateItemId))
                continue;

            bool better =
                victim == null ||
                distance < bestDistance - 0.35f;
            if (!better)
                continue;

            PlayerShooting candidateShooting = candidateHealth.GetComponent<PlayerShooting>();
            if (candidateShooting == null || candidateShooting.photonView == null)
                continue;

            victim = candidateShooting;
            slotIndex = candidateSlot;
            itemId = candidateItemId;
            bestDistance = distance;
        }

        return victim != null && slotIndex >= 0 && !string.IsNullOrWhiteSpace(itemId);
    }

    bool TryFindNeutralRiderLootHookTarget(Photon.Realtime.Player owner, out NeutralRiderController rider, out string itemId)
    {
        rider = null;
        itemId = string.Empty;

        if (owner == null)
            return false;

        Vector2 sourcePosition = transform.position;
        float bestDistance = float.MaxValue;
        NeutralRiderController[] riders = FindObjectsByType<NeutralRiderController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < riders.Length; i++)
        {
            NeutralRiderController candidate = riders[i];
            if (candidate == null ||
                candidate.gameObject == gameObject ||
                candidate.photonView == null)
            {
                continue;
            }

            PlayerHealth candidateHealth = candidate.GetComponent<PlayerHealth>();
            if (candidateHealth == null ||
                candidateHealth.IsWreck ||
                candidateHealth.IsEvacuationAnimating)
            {
                continue;
            }

            float distance = Vector2.Distance(sourcePosition, candidate.transform.position);
            if (distance > LootHookRange)
                continue;

            if (!candidate.TrySelectLootHookCargo(out string candidateItemId) ||
                !PlayerProfileService.PlayerHasFreeShipInventorySlot(owner, candidateItemId))
            {
                continue;
            }

            bool better =
                rider == null ||
                distance < bestDistance - 0.35f;
            if (!better)
                continue;

            rider = candidate;
            itemId = candidateItemId;
            bestDistance = distance;
        }

        return rider != null && !string.IsNullOrWhiteSpace(itemId);
    }

    bool IsActiveLootHookWindup(int targetViewId, int slotIndex, string itemId)
    {
        return activeLootHookTargetViewId == targetViewId &&
               activeLootHookCargoSlotIndex == slotIndex &&
               string.Equals(activeLootHookCargoItemId, itemId, StringComparison.Ordinal);
    }

    bool IsLootHookTargetStillValid(Photon.Realtime.Player owner, PhotonView targetView, int slotIndex, string itemId, float maxRange)
    {
        if (!IsLootHookSourceReady(owner) ||
            targetView == null ||
            string.IsNullOrWhiteSpace(itemId) ||
            GetLootHookDistance(targetView) > maxRange)
        {
            return false;
        }

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        if (targetHealth == null ||
            targetHealth.IsWreck ||
            targetHealth.IsEvacuationAnimating ||
            targetHealth.CurrentHP <= 0)
        {
            return false;
        }

        if (slotIndex == LootHookNeutralCargoSlotIndex)
        {
            NeutralRiderController neutralRider = targetView.GetComponent<NeutralRiderController>();
            return neutralRider != null &&
                   !neutralRider.IsEvacuated &&
                   PlayerProfileService.PlayerHasFreeShipInventorySlot(owner, itemId);
        }

        if (targetHealth == GetComponent<PlayerHealth>() ||
            targetHealth.IsBotControlled ||
            targetHealth.IsNeutralRiderControlled ||
            targetHealth.IsAstronautControlled ||
            targetView.Owner == null ||
            targetView.Owner.ActorNumber == owner.ActorNumber)
        {
            return false;
        }

        return IsLootHookCargoStillAvailable(owner, targetView.Owner, slotIndex, itemId);
    }

    bool IsLootHookSourceReady(Photon.Realtime.Player owner)
    {
        return PhotonNetwork.IsMasterClient &&
               IsGameStarted() &&
               photonView != null &&
               photonView.Owner != null &&
               owner != null &&
               photonView.Owner.ActorNumber == owner.ActorNumber &&
               !AreShipControlsBlocked();
    }

    float GetLootHookDistance(PhotonView targetView)
    {
        if (targetView == null)
            return float.MaxValue;

        return Vector2.Distance(transform.position, targetView.transform.position);
    }

    static bool IsLootHookCargoStillAvailable(Photon.Realtime.Player thief, Photon.Realtime.Player victim, int slotIndex, string itemId)
    {
        if (thief == null || victim == null || slotIndex < 0 || string.IsNullOrWhiteSpace(itemId))
            return false;

        string[] slots = PlayerProfileService.GetPlayerShipInventorySlots(victim);
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(victim, 0);
        int capacity = PlayerProfileService.GetPlayerShipInventoryCapacity(victim);
        if (slots == null ||
            slotIndex >= slots.Length ||
            slotIndex >= capacity ||
            PlayerProfileService.IsSafePocketIndex(shipSkinIndex, slotIndex) ||
            PlayerProfileService.IsAstronautCargoIndex(shipSkinIndex, capacity, slotIndex))
        {
            return false;
        }

        return string.Equals(slots[slotIndex], itemId, StringComparison.Ordinal) &&
               PlayerProfileService.PlayerHasFreeShipInventorySlot(thief, itemId);
    }

    static bool TrySelectLootHookCargo(Photon.Realtime.Player thief, Photon.Realtime.Player victim, out int slotIndex, out string itemId)
    {
        slotIndex = -1;
        itemId = string.Empty;

        if (thief == null || victim == null)
            return false;

        string[] slots = PlayerProfileService.GetPlayerShipInventorySlots(victim);
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(victim, 0);
        int capacity = PlayerProfileService.GetPlayerShipInventoryCapacity(victim);
        List<int> candidateSlots = new List<int>();
        for (int i = 0; i < slots.Length && i < capacity; i++)
        {
            if (PlayerProfileService.IsSafePocketIndex(shipSkinIndex, i) ||
                PlayerProfileService.IsAstronautCargoIndex(shipSkinIndex, capacity, i))
            {
                continue;
            }

            string candidateItemId = slots[i];
            if (string.IsNullOrWhiteSpace(candidateItemId))
                continue;

            if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(thief, candidateItemId))
                continue;

            candidateSlots.Add(i);
        }

        if (candidateSlots.Count <= 0)
            return false;

        slotIndex = candidateSlots[UnityEngine.Random.Range(0, candidateSlots.Count)];
        itemId = slots[slotIndex];
        return slotIndex >= 0;
    }

    [PunRPC]
    async void PrepareLootHookVictimCargoRpc(int requestId, int slotIndex, string expectedItemId, int attackerViewId)
    {
        if (photonView == null || !photonView.IsMine)
            return;

        string removedItemId = null;
        bool removed = false;
        try
        {
            removedItemId = await PlayerProfileService.Instance.RemoveLootHookCargoItemDeferredSaveAsync(slotIndex, expectedItemId);
            removed = string.Equals(removedItemId, expectedItemId, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Debug.LogError("Loot Hook victim cargo removal failed: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ConfirmLootHookVictimCargoRpc), RpcTarget.MasterClient, requestId, slotIndex, removedItemId ?? expectedItemId ?? string.Empty, removed, attackerViewId);
    }

    [PunRPC]
    void ConfirmLootHookVictimCargoRpc(int requestId, int slotIndex, string itemId, bool removed, int attackerViewId, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        PhotonView attackerView = PhotonView.Find(attackerViewId);
        PlayerShooting attacker = attackerView != null ? attackerView.GetComponent<PlayerShooting>() : null;
        if (attacker == null)
        {
            if (removed)
                photonView.RPC(nameof(RestoreLootHookCargoItemRpc), messageInfo.Sender, slotIndex, itemId);
            return;
        }

        attacker.ResolveLootHookVictimConfirmation(this, requestId, slotIndex, itemId, removed, messageInfo.Sender);
    }

    void ResolveLootHookVictimConfirmation(PlayerShooting victim, int requestId, int slotIndex, string itemId, bool removed, Photon.Realtime.Player sender)
    {
        if (!PhotonNetwork.IsMasterClient || victim == null || victim.photonView == null)
            return;

        bool valid =
            pendingLootHookActive &&
            requestId == pendingLootHookRequestId &&
            victim.photonView.ViewID == pendingLootHookVictimViewId &&
            slotIndex == pendingLootHookCargoSlotIndex &&
            sender != null &&
            sender.ActorNumber == pendingLootHookVictimActorNumber &&
            string.Equals(itemId, pendingLootHookCargoItemId, StringComparison.Ordinal);

        if (!valid)
        {
            if (removed && sender != null)
                victim.photonView.RPC(nameof(RestoreLootHookCargoItemRpc), sender, slotIndex, itemId);
            return;
        }

        if (!removed)
        {
            ClearPendingLootHook();
            return;
        }

        if (photonView.Owner == null || !PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, itemId))
        {
            victim.photonView.RPC(nameof(RestoreLootHookCargoItemRpc), sender, slotIndex, itemId);
            ClearPendingLootHook();
            return;
        }

        photonView.RPC(nameof(ReceiveLootHookStolenItemRpc), photonView.Owner, requestId, itemId, victim.photonView.ViewID, slotIndex);
    }

    [PunRPC]
    async void ReceiveLootHookStolenItemRpc(int requestId, string itemId, int victimViewId, int victimSlotIndex)
    {
        if (photonView == null || !photonView.IsMine)
            return;

        bool stored = false;
        try
        {
            stored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(itemId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Loot Hook stolen cargo store failed: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ConfirmLootHookStoredItemRpc), RpcTarget.MasterClient, requestId, itemId, victimViewId, victimSlotIndex, stored);
    }

    [PunRPC]
    void ConfirmLootHookStoredItemRpc(int requestId, string itemId, int victimViewId, int victimSlotIndex, bool stored, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null)
            return;

        bool valid =
            pendingLootHookActive &&
            requestId == pendingLootHookRequestId &&
            victimViewId == pendingLootHookVictimViewId &&
            victimSlotIndex == pendingLootHookCargoSlotIndex &&
            string.Equals(itemId, pendingLootHookCargoItemId, StringComparison.Ordinal) &&
            photonView.Owner != null &&
            messageInfo.Sender != null &&
            messageInfo.Sender.ActorNumber == photonView.Owner.ActorNumber;

        if (!valid)
            return;

        PhotonView victimView = PhotonView.Find(victimViewId);
        if (!stored)
        {
            RestoreLootHookSourceCargo(victimView, victimSlotIndex, itemId);
            ClearPendingLootHook();
            return;
        }

        Photon.Realtime.Player owner = photonView.Owner;
        int maxCharges = ResolveEquippedGadgetMaxCharges(owner, InventoryItemCatalog.LootHookId);
        int remainingCharges = owner != null ? GetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, InventoryItemCatalog.LootHookId, maxCharges) : 0;
        if (maxCharges > 0 && remainingCharges > 0)
            SetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, InventoryItemCatalog.LootHookId, remainingCharges - 1, maxCharges);

        RoundXpTracker.RecordGadgetSuccess(owner, InventoryItemCatalog.LootHookId);
        photonView.RPC(nameof(PlayLootHookFxRpc), RpcTarget.All, victimViewId, itemId);
        ClearPendingLootHook();
    }

    void RestorePendingNeutralLootHookCargo()
    {
        if (!PhotonNetwork.IsMasterClient || pendingLootHookCargoSlotIndex != LootHookNeutralCargoSlotIndex)
            return;

        PhotonView victimView = PhotonView.Find(pendingLootHookVictimViewId);
        RestoreLootHookSourceCargo(victimView, pendingLootHookCargoSlotIndex, pendingLootHookCargoItemId);
    }

    void RestoreLootHookSourceCargo(PhotonView victimView, int victimSlotIndex, string itemId)
    {
        if (victimView == null || string.IsNullOrWhiteSpace(itemId))
            return;

        if (victimSlotIndex == LootHookNeutralCargoSlotIndex)
        {
            NeutralRiderController neutralVictim = victimView.GetComponent<NeutralRiderController>();
            if (neutralVictim != null)
                neutralVictim.RestoreLootHookCargo(itemId);
            return;
        }

        PlayerShooting victim = victimView.GetComponent<PlayerShooting>();
        if (victim != null && victim.photonView != null && victim.photonView.Owner != null)
            victim.photonView.RPC(nameof(RestoreLootHookCargoItemRpc), victim.photonView.Owner, victimSlotIndex, itemId);
    }

    [PunRPC]
    async void RestoreLootHookCargoItemRpc(int slotIndex, string itemId)
    {
        if (photonView == null || !photonView.IsMine || string.IsNullOrWhiteSpace(itemId))
            return;

        try
        {
            await PlayerProfileService.Instance.RestoreShipItemAtAsync(slotIndex, itemId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Loot Hook cargo restore failed: " + ex);
        }
    }

    [PunRPC]
    void PlayLootHookFxRpc(int victimViewId, string itemId)
    {
        PhotonView victimView = PhotonView.Find(victimViewId);
        if (victimView != null)
            LootHookSnatchVfx.Spawn(transform, victimView.transform);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayLootHookAt(transform.position);
    }

    [PunRPC]
    void PlayBioTrapFxRpc(int targetViewId)
    {
        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView != null)
            BioTrapCaptureVfx.Spawn(transform.position, targetView.transform.position);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBioTrapCaptureAt(targetView != null ? targetView.transform.position : transform.position);
    }

    [PunRPC]
    void PlayTetherHarpoonFxRpc(int targetViewId)
    {
        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView != null)
            LootHookSnatchVfx.Spawn(transform, targetView.transform);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayTetherHarpoonAt(transform.position);
    }

    [PunRPC]
    void PlayAsteroidBreacherFxRpc(float x, float y)
    {
        Vector3 position = new Vector3(x, y, transform.position.z);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAsteroidBreacherAt(position);
    }

    void ClearPendingLootHook()
    {
        pendingLootHookActive = false;
        pendingLootHookVictimViewId = 0;
        pendingLootHookVictimActorNumber = 0;
        pendingLootHookCargoSlotIndex = -1;
        pendingLootHookCargoItemId = null;
        pendingLootHookExpiresAt = 0f;
    }

    void StopAuthoritativeLootHookWindup()
    {
        if (authoritativeLootHookRoutine != null)
        {
            StopCoroutine(authoritativeLootHookRoutine);
            authoritativeLootHookRoutine = null;
        }

        ClearActiveLootHookWindup();
    }

    void ClearActiveLootHookWindup()
    {
        activeLootHookTargetViewId = 0;
        activeLootHookCargoSlotIndex = -1;
        activeLootHookCargoItemId = null;
    }

    bool HasHackingDeviceCandidate()
    {
        return FindClosestHackingDeviceTarget(HackingDeviceRange) != null;
    }

    bool TryStartHackingDevice(string itemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            !IsGameStarted() ||
            !string.Equals(itemId, InventoryItemCatalog.HackingDeviceId, StringComparison.Ordinal) ||
            photonView == null ||
            authoritativeHackingDeviceRoutine != null)
        {
            return false;
        }

        PhotonView targetView = FindClosestHackingDeviceTarget(HackingDeviceRange);
        if (targetView == null)
            return false;

        activeHackingDeviceTargetViewId = targetView.ViewID;
        activeHackingDeviceItemId = itemId;
        photonView.RPC(nameof(StartHackingDeviceEffects), RpcTarget.All, photonView.ViewID, targetView.ViewID, HackingDeviceWindupDuration);
        authoritativeHackingDeviceRoutine = StartCoroutine(HackingDeviceRoutine(targetView.ViewID));
        return true;
    }

    IEnumerator HackingDeviceRoutine(int targetViewId)
    {
        float startedAt = Time.time;
        WaitForSeconds wait = new WaitForSeconds(HackingDeviceValidationInterval);
        while (Time.time - startedAt < HackingDeviceWindupDuration)
        {
            PhotonView targetView = PhotonView.Find(targetViewId);
            PlayerHealth targetHealth = targetView != null ? targetView.GetComponent<PlayerHealth>() : null;
            if (!IsValidHackingDeviceTarget(targetHealth, HackingDeviceKeepAliveRange, true))
                break;

            yield return wait;
        }

        bool completed = Time.time - startedAt >= HackingDeviceWindupDuration;
        authoritativeHackingDeviceRoutine = null;

        PhotonView completedTargetView = PhotonView.Find(targetViewId);
        PlayerHealth completedTargetHealth = completedTargetView != null ? completedTargetView.GetComponent<PlayerHealth>() : null;
        if (completed && IsValidHackingDeviceTarget(completedTargetHealth, HackingDeviceKeepAliveRange, true))
        {
            ApplyCompletedHackingDeviceEffect(completedTargetView);
            RoundXpTracker.RecordGadgetSuccess(photonView.Owner, InventoryItemCatalog.HackingDeviceId);
            NotifyPathfinderHackCompleted(completedTargetView);
        }

        StopAuthoritativeHackingDevice(true);
    }

    void ApplyCompletedHackingDeviceEffect(PhotonView targetView)
    {
        if (!PhotonNetwork.IsMasterClient || targetView == null)
            return;

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        if (targetHealth == null || targetHealth.IsWreck)
            return;

        float duration = ResolveHackingDeviceEffectDuration(targetHealth);
        int sourceViewId = photonView != null ? photonView.ViewID : 0;

        PlayerShooting targetShooting = targetView.GetComponent<PlayerShooting>();
        if (targetShooting != null)
            targetView.RPC(nameof(ApplyHackedStatusRpc), RpcTarget.All, duration, sourceViewId);

        EnemyBot targetBot = targetView.GetComponent<EnemyBot>();
        if (targetBot != null)
            targetView.RPC(nameof(EnemyBot.ApplyConfusionRpc), RpcTarget.All, duration);
    }

    void NotifyPathfinderHackCompleted(PhotonView targetView)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null || photonView.Owner == null || targetView == null)
            return;

        string hackedTypeId = ResolvePathfinderHackedShipTypeId(targetView);
        if (string.IsNullOrWhiteSpace(hackedTypeId))
            return;

        photonView.RPC(nameof(RecordPathfinderHackProgressRpc), photonView.Owner, hackedTypeId);
    }

    string ResolvePathfinderHackedShipTypeId(PhotonView targetView)
    {
        if (targetView == null)
            return string.Empty;

        EnemyBot targetBot = targetView.GetComponent<EnemyBot>();
        if (targetBot != null)
            return PlayerProfileService.BuildPathfinderEnemyHackTypeId(targetBot.Kind);

        NeutralRiderController neutralRider = targetView.GetComponent<NeutralRiderController>();
        if (neutralRider != null)
            return PlayerProfileService.BuildPathfinderPlayerHackTypeId(ShipCatalog.GetShipTypeFromSkinIndex(neutralRider.ShipSkinIndex));

        if (targetView.Owner != null)
        {
            int shipSkinIndex = RoomSettings.GetPlayerShipSkin(targetView.Owner, ShipCatalog.ExplorerBasicSkinIndex);
            return PlayerProfileService.BuildPathfinderPlayerHackTypeId(ShipCatalog.GetShipTypeFromSkinIndex(shipSkinIndex));
        }

        return string.Empty;
    }

    [PunRPC]
    async void RecordPathfinderHackProgressRpc(string hackedTypeId)
    {
        if (!photonView.IsMine || string.IsNullOrWhiteSpace(hackedTypeId) || !PlayerProfileService.HasInstance)
            return;

        try
        {
            PathfinderHackRecordResult result = await PlayerProfileService.Instance.RecordPathfinderHackedShipTypeAsync(hackedTypeId);
            if (result == null || (!result.Started && !result.Added && !result.DocumentationReady && !result.InventoryFull))
                return;

            if (result.InventoryFull)
            {
                RoundAnnouncementUI.Show("Ship Prototype Documentation ready - make room in inventory.", 3.2f);
                return;
            }

            if (result.DocumentationReady && result.DocumentationCreated)
            {
                RoundAnnouncementUI.Show("Ship Prototype Documentation acquired.", 3.2f);
                return;
            }

            if (result.Started)
            {
                RoundAnnouncementUI.Show("Documentation copied - Pathfinder project initiated. Hack more data from different ship types", 4f);
                return;
            }

            if (result.Added)
                RoundAnnouncementUI.Show("Pathfinder ship data copied: " + result.Count + "/" + PlayerProfileService.PathfinderHackedShipTypesRequired, 2.8f);
        }
        catch (Exception ex)
        {
            Debug.LogError("Pathfinder hack progress failed: " + ex);
        }
    }

    float ResolveHackingDeviceEffectDuration(PlayerHealth targetHealth)
    {
        EnemyBot targetBot = targetHealth != null ? targetHealth.GetComponent<EnemyBot>() : null;
        if (targetBot == null)
            return HackingDevicePlayerDuration;

        float size = Mathf.Max(0.6f, targetBot.VisualTargetSize);
        float sizeT = Mathf.InverseLerp(0.82f, 5.6f, size);
        return Mathf.Lerp(HackingDeviceComputerMaxDuration, HackingDeviceComputerMinDuration, sizeT);
    }

    void StopAuthoritativeHackingDevice(bool notifyClients)
    {
        if (authoritativeHackingDeviceRoutine != null)
        {
            StopCoroutine(authoritativeHackingDeviceRoutine);
            authoritativeHackingDeviceRoutine = null;
        }

        if (notifyClients && photonView != null)
            photonView.RPC(nameof(StopHackingDeviceEffects), RpcTarget.All, photonView.ViewID);

        activeHackingDeviceTargetViewId = 0;
        activeHackingDeviceItemId = null;
    }

    [PunRPC]
    void RequestCancelHackingDevice(PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null || photonView.Owner == null || messageInfo.Sender == null)
            return;

        if (messageInfo.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        StopAuthoritativeHackingDevice(true);
    }

    [PunRPC]
    void StartHackingDeviceEffects(int sourceViewId, int targetViewId, float windupDuration)
    {
        AudioManager.Instance.PlayHackingDeviceAt(transform.position);
        HackingBeamVfx.StartBeam(sourceViewId, targetViewId, windupDuration);
    }

    [PunRPC]
    void StopHackingDeviceEffects(int sourceViewId)
    {
        HackingBeamVfx.StopBeam(sourceViewId);
    }

    [PunRPC]
    void ApplyHackedStatusRpc(float duration, int sourceViewId)
    {
        AudioManager.Instance.PlayHackingDeviceAt(transform.position);
        HackingStatus.Attach(gameObject, duration, sourceViewId);
    }

    PhotonView FindClosestHackingDeviceTarget(float maxRange)
    {
        Vector2 sourcePosition = transform.position;
        PhotonView bestView = null;
        float bestDistance = float.MaxValue;

        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (!IsValidHackingDeviceTarget(candidate, maxRange, true))
                continue;

            float distance = GetHackingDeviceDistance(candidate, sourcePosition);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestView = candidate.photonView;
        }

        return bestView;
    }

    bool IsValidHackingDeviceTarget(PlayerHealth candidate, float maxRange, bool requireTargetBlind)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate == GetComponent<PlayerHealth>())
            return false;

        if (candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.CurrentHP <= 0)
            return false;

        PhotonView candidateView = candidate.photonView;
        if (candidateView == null || candidateView == photonView)
            return false;

        if (candidate.GetComponent<PlayerDeployableBase>() != null || candidate.GetComponent<LureBeaconDecoy>() != null)
            return false;

        ActorIdentity identity = ActorIdentity.Ensure(candidate.gameObject);
        if (identity == null || !identity.IsShip)
            return false;

        if (!candidate.IsBotControlled && !candidate.IsNeutralRiderControlled && !candidate.IsHumanShipControlled)
            return false;

        if (candidate.IsHumanShipControlled &&
            photonView != null &&
            photonView.Owner != null &&
            candidateView.Owner != null &&
            candidateView.Owner.ActorNumber == photonView.Owner.ActorNumber)
        {
            return false;
        }

        EnemyBot targetBot = candidate.GetComponent<EnemyBot>();
        if (targetBot != null && !targetBot.CanReceivePilotHostileEffect())
            return false;

        if (HackingStatus.IsActiveOn(candidate.gameObject))
            return false;

        if (GetHackingDeviceDistance(candidate, transform.position) > maxRange)
            return false;

        return !requireTargetBlind || IsHackingTargetUnableToSeeSource(candidate);
    }

    float GetHackingDeviceDistance(PlayerHealth candidate, Vector2 sourcePosition)
    {
        if (candidate == null)
            return float.MaxValue;

        Collider2D collider = candidate.GetComponent<Collider2D>();
        if (collider == null)
            collider = candidate.GetComponentInChildren<Collider2D>();

        Vector2 targetPoint = collider != null ? collider.ClosestPoint(sourcePosition) : (Vector2)candidate.transform.position;
        return Vector2.Distance(sourcePosition, targetPoint);
    }

    bool IsHackingTargetUnableToSeeSource(PlayerHealth target)
    {
        return IsSourceHiddenFromHackingTarget(target) || IsSourceBehindHackingTarget(target);
    }

    bool IsSourceHiddenFromHackingTarget(PlayerHealth target)
    {
        HideInNebulaTarget sourceNebulaState = GetComponent<HideInNebulaTarget>();
        if (sourceNebulaState == null)
            return false;

        HideInNebulaTarget targetNebulaState = target != null ? target.GetComponent<HideInNebulaTarget>() : null;
        return sourceNebulaState.IsHiddenFromObserver(targetNebulaState);
    }

    bool IsSourceBehindHackingTarget(PlayerHealth target)
    {
        if (target == null)
            return false;

        Vector2 toSource = (Vector2)transform.position - (Vector2)target.transform.position;
        if (toSource.sqrMagnitude <= 0.001f)
            return false;

        Vector2 targetForward = ResolveHackingTargetForward(target);
        if (targetForward.sqrMagnitude <= 0.001f)
            return false;

        return Vector2.Dot(targetForward.normalized, toSource.normalized) <= HackingDeviceBehindDotThreshold;
    }

    Vector2 ResolveHackingTargetForward(PlayerHealth target)
    {
        if (target == null)
            return Vector2.up;

        if (target.GetComponent<EnemyBot>() != null)
            return -target.transform.up;

        return target.transform.up;
    }

    [PunRPC]
    void RequestStartTractorBeam(string itemId, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || !IsGameStarted() || !string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
            return;

        if (AreShipControlsBlocked())
            return;

        if (photonView == null || photonView.Owner == null || messageInfo.Sender == null || messageInfo.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        Photon.Realtime.Player owner = photonView.Owner;
        int maxCharges = ResolveEquippedGadgetMaxCharges(owner, itemId);
        if (maxCharges <= 0)
            return;

        int remainingCharges = GetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, itemId, maxCharges);
        if (remainingCharges <= 0)
            return;

        PhotonView targetView = FindClosestTractorBeamTarget();
        if (targetView == null)
            return;

        StopAuthoritativeTractorBeam(true);
        SetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, itemId, remainingCharges - 1, maxCharges);
        RoundXpTracker.RecordGadgetSuccess(owner, itemId);
        activeTractorBeamTargetViewId = targetView.ViewID;
        activeTractorBeamItemId = itemId;
        photonView.RPC(nameof(StartTractorBeamEffects), RpcTarget.All, photonView.ViewID, targetView.ViewID);
        authoritativeTractorBeamRoutine = StartCoroutine(TractorBeamPullRoutine(targetView.ViewID));
    }

    [PunRPC]
    void RequestStopTractorBeam(string itemId, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || !string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
            return;

        if (photonView == null || photonView.Owner == null || messageInfo.Sender == null || messageInfo.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        StopAuthoritativeTractorBeam(true);
    }

    int ResolveEquippedGadgetMaxCharges(Photon.Realtime.Player owner, string itemId)
    {
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        List<string> orderedItems = new List<string>();
        Dictionary<string, int> gadgetCounts = CollectEquippedGadgetCounts(equipmentSlots, shipSkinIndex, orderedItems);
        int equippedCount = gadgetCounts.TryGetValue(itemId, out int count) ? count : 0;
        return ResolveGadgetMaxCharges(itemId, equippedCount);
    }

    void StopAuthoritativeTractorBeam(bool notifyClients)
    {
        if (authoritativeTractorBeamRoutine != null)
        {
            StopCoroutine(authoritativeTractorBeamRoutine);
            authoritativeTractorBeamRoutine = null;
        }

        if (notifyClients && photonView != null)
            photonView.RPC(nameof(StopTractorBeamEffects), RpcTarget.All, photonView.ViewID);

        activeTractorBeamTargetViewId = 0;
        activeTractorBeamItemId = null;
    }

    bool TryExecuteAuthoritativeGadgetUse(string itemId)
    {
        if (string.Equals(itemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return TryDeployGadgetMine();

        if (string.Equals(itemId, InventoryItemCatalog.SpaceBombId, StringComparison.Ordinal))
        {
            bool deployed = NewItemsRuntime.TryDeploySpaceBomb(this);
            if (deployed)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return deployed;
        }

        if (string.Equals(itemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
            return TryActivateBatteryCharge();

        if (string.Equals(itemId, InventoryItemCatalog.MagneticBeamId, StringComparison.Ordinal))
            return TryActivateMagneticBeam();

        if (string.Equals(itemId, InventoryItemCatalog.StasisBuoyId, StringComparison.Ordinal))
        {
            bool deployed = NewItemsRuntime.TryDeployStasisBuoy(this);
            if (deployed)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return deployed;
        }

        if (string.Equals(itemId, InventoryItemCatalog.TetherHarpoonId, StringComparison.Ordinal))
        {
            bool tethered = NewItemsRuntime.TryStartTetherHarpoon(this);
            if (tethered)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return tethered;
        }

        if (string.Equals(itemId, InventoryItemCatalog.SpaceTorpedoId, StringComparison.Ordinal))
        {
            bool launched = NewItemsRuntime.TryLaunchSpaceTorpedo(this);
            if (launched)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return launched;
        }

        if (string.Equals(itemId, InventoryItemCatalog.BioTrapId, StringComparison.Ordinal))
        {
            bool captured = NewItemsRuntime.TryFireBioTrap(this);
            if (captured)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return captured;
        }

        if (string.Equals(itemId, InventoryItemCatalog.AsteroidBreacherBombId, StringComparison.Ordinal))
        {
            bool breached = NewItemsRuntime.TryDetonateAsteroidBreacherBomb(this);
            if (breached)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return breached;
        }

        if (string.Equals(itemId, InventoryItemCatalog.MetalDriftWallId, StringComparison.Ordinal))
        {
            bool deployed = NewItemsRuntime.TryDeployMetalDriftWall(this);
            if (deployed)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return deployed;
        }

        if (string.Equals(itemId, InventoryItemCatalog.LureBeaconId, StringComparison.Ordinal))
            return TryDeployLureBeacon();

        if (string.Equals(itemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal))
        {
            bool deployed = NewItemsRuntime.TryDeployAutoTurret(this);
            if (deployed)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return deployed;
        }

        if (string.Equals(itemId, InventoryItemCatalog.RocketAutoTurretId, StringComparison.Ordinal))
        {
            bool deployed = NewItemsRuntime.TryDeployRocketAutoTurret(this);
            if (deployed)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return deployed;
        }

        if (string.Equals(itemId, InventoryItemCatalog.GuidanceSystemId, StringComparison.Ordinal))
        {
            photonView.RPC(nameof(ActivateGuidanceSystemRpc), photonView.Owner, GetAdjustedGuidanceSystemDuration());
            RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);
            return true;
        }

        if (string.Equals(itemId, InventoryItemCatalog.ShortScannerId, StringComparison.Ordinal))
        {
            photonView.RPC(nameof(ActivateShortScannerRevealRpc), photonView.Owner, GetAdjustedShortScannerRevealDuration());
            RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);
            return true;
        }

        if (string.Equals(itemId, InventoryItemCatalog.CloakDeviceId, StringComparison.Ordinal))
        {
            HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>();
            if (nebulaTarget != null && nebulaTarget.IsCloaked)
                return false;

            photonView.RPC(nameof(ActivateCloakDeviceRpc), RpcTarget.All, CloakDeviceDuration);
            RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);
            return true;
        }

        if (string.Equals(itemId, InventoryItemCatalog.HackingDeviceId, StringComparison.Ordinal))
            return TryStartHackingDevice(itemId);

        if (string.Equals(itemId, InventoryItemCatalog.SpaceDrillId, StringComparison.Ordinal))
        {
            bool launched = NewItemsRuntime.TryLaunchSpaceDrill(this);
            if (launched)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return launched;
        }

        if (string.Equals(itemId, InventoryItemCatalog.SpaceTrapId, StringComparison.Ordinal))
        {
            bool armed = NewItemsRuntime.TryArmSpaceTrap(this);
            if (armed)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return armed;
        }

        if (string.Equals(itemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
        {
            ShipDamageState damageState = GetComponent<ShipDamageState>();
            if (damageState != null && damageState.IsBoosterDisabled())
                return false;

            photonView.RPC(nameof(ActivateSuperBoosterRpc), photonView.Owner, SuperBoosterDuration);
            photonView.RPC(nameof(PlaySuperBoosterSfxRpc), RpcTarget.All);
            RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);
            return true;
        }

        return false;
    }

    [PunRPC]
    void ActivateGuidanceSystemRpc(float duration)
    {
        GuidanceSystemOverlay.EnsureFor(gameObject)?.ActivateGuidance(duration);
    }

    [PunRPC]
    void ActivateShortScannerRevealRpc(float duration)
    {
        AudioManager.Instance.PlayShortScannerAt(transform.position);
        ShortScannerRevealStatus.EnsureFor(gameObject)?.ActivateReveal(duration);
    }

    [PunRPC]
    void ActivateCloakDeviceRpc(float duration)
    {
        AudioManager.Instance.PlayCloakActivationAt(transform.position);
        HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>() ?? gameObject.AddComponent<HideInNebulaTarget>();
        nebulaTarget.ActivateCloak(duration);
    }

    [PunRPC]
    void CancelCloakDeviceRpc()
    {
        HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>();
        if (nebulaTarget != null)
            nebulaTarget.CancelCloak();
    }

    float GetAdjustedGuidanceSystemDuration()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return GuidanceSystemDuration + (PilotCatalog.IsSelectedPilot(owner, PilotCatalog.VectorId) ? VectorGuidanceSystemDurationBonus : 0f);
    }

    float GetAdjustedShortScannerRevealDuration()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return ShortScannerRevealDuration + (PilotCatalog.IsSelectedPilot(owner, PilotCatalog.VectorId) ? VectorShortScannerRevealDurationBonus : 0f);
    }

    [PunRPC]
    void ActivateSuperBoosterRpc(float duration)
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.ActivateSuperBooster(duration);
    }

    [PunRPC]
    void PlaySuperBoosterSfxRpc()
    {
        AudioManager.Instance.PlaySuperBoosterAt(transform.position);
    }

    [PunRPC]
    void PlaySpaceDrillDeliverySoundRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlaySpaceDrillDeliveryAt(new Vector3(x, y, z));
    }

    [PunRPC]
    void ArmSpaceTrapTargetRpc(int targetViewId, int ownerViewId)
    {
        PhotonView target = PhotonView.Find(targetViewId);
        SpaceTrapTarget trap = target != null ? SpaceTrapTarget.Attach(target.gameObject) : null;
        if (trap != null)
        {
            SpaceTrapLaunchVfx.Spawn(transform.position, target.transform.position);
            trap.Arm(ownerViewId);
        }
    }

    [PunRPC]
    void ClearSpaceTrapTargetRpc(int targetViewId)
    {
        SpaceTrapTarget.ClearLocalMarker(targetViewId);
    }

    bool TryDeployLureBeacon()
    {
        if (!PhotonNetwork.IsMasterClient)
            return false;

        Vector2 deployDirection = -(Vector2)transform.up;
        if (deployDirection.sqrMagnitude < 0.001f)
            deployDirection = Vector2.down;
        else
            deployDirection = deployDirection.normalized;

        Vector3 spawnPosition = ResolveLureBeaconSpawnPosition(deployDirection);
        GameObject beaconObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)),
            0,
            new object[]
            {
                LureBeaconDecoy.InstantiationMarker,
                photonView != null ? photonView.ViewID : 0,
                deployDirection.x,
                deployDirection.y
            });

        if (beaconObject == null)
            return false;

        LureBeaconDecoy.EnsureAttached(beaconObject);
        return true;
    }

    Vector3 ResolveLureBeaconSpawnPosition(Vector2 preferredDirection)
    {
        Vector2 origin = transform.position;
        Vector2 fallbackDirection = preferredDirection.sqrMagnitude > 0.001f ? preferredDirection.normalized : Vector2.down;
        for (int attempt = 0; attempt < 14; attempt++)
        {
            float jitter = attempt == 0 ? 0f : UnityEngine.Random.Range(-26f, 26f);
            Vector2 candidateDirection = Quaternion.Euler(0f, 0f, jitter) * fallbackDirection;
            if (candidateDirection.sqrMagnitude < 0.001f)
                candidateDirection = fallbackDirection;

            Vector2 candidate = origin + candidateDirection.normalized * LureBeaconDeployDistance;
            if (IsLureBeaconSpawnPositionFree(candidate))
                return new Vector3(candidate.x, candidate.y, 0f);
        }

        Vector2 fallback = origin + fallbackDirection * LureBeaconDeployDistance;
        return new Vector3(fallback.x, fallback.y, 0f);
    }

    bool IsLureBeaconSpawnPositionFree(Vector2 candidate)
    {
        int hitCount = Physics2D.OverlapCircle(candidate, LureBeaconSpawnClearanceRadius, CreatePhysicsQueryFilter(), SpawnClearanceHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = SpawnClearanceHits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            if (hit.GetComponentInParent<LureBeaconDecoy>() != null)
                continue;

            return false;
        }

        return true;
    }

    static ContactFilter2D CreatePhysicsQueryFilter()
    {
        return new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };
    }

    bool TryActivateMagneticBeam()
    {
        if (CountMagneticBeamTargets() > 0)
            RoundXpTracker.RecordGadgetSuccess(photonView.Owner, InventoryItemCatalog.MagneticBeamId);

        photonView.RPC(nameof(PlayMagneticBeamEffects), RpcTarget.All);
        StartCoroutine(MagneticBeamPullRoutine());
        return true;
    }

    int CountMagneticBeamTargets()
    {
        int count = 0;
        Vector2 sourcePosition = transform.position;
        MovingSpaceObject[] objects = FindObjectsByType<MovingSpaceObject>(FindObjectsInactive.Exclude);
        for (int i = 0; i < objects.Length; i++)
        {
            MovingSpaceObject movingObject = objects[i];
            if (movingObject == null)
                continue;

            if (Vector2.Distance(sourcePosition, movingObject.transform.position) <= MagneticBeamRadius)
                count++;
        }

        return count;
    }

    IEnumerator MagneticBeamPullRoutine()
    {
        float elapsed = 0f;
        WaitForFixedUpdate wait = new WaitForFixedUpdate();
        while (elapsed < MagneticBeamDuration)
        {
            ApplyMagneticBeamPull(Time.fixedDeltaTime);
            elapsed += Time.fixedDeltaTime;
            yield return wait;
        }
    }

    void ApplyMagneticBeamPull(float deltaTime)
    {
        Vector2 sourcePosition = transform.position;
        MovingSpaceObject[] objects = FindObjectsByType<MovingSpaceObject>(FindObjectsInactive.Exclude);
        for (int i = 0; i < objects.Length; i++)
        {
            MovingSpaceObject movingObject = objects[i];
            if (movingObject == null)
                continue;

            float distance = Vector2.Distance(sourcePosition, movingObject.transform.position);
            if (distance > MagneticBeamRadius)
                continue;

            movingObject.ApplyMagneticPull(sourcePosition, MagneticBeamPullStrength, deltaTime);
        }
    }

    [PunRPC]
    void PlayMagneticBeamEffects()
    {
        AudioManager.Instance.PlayMagneticBeamAt(transform.position);
        MagneticBeamVfx.Spawn(transform);
    }

    PhotonView FindClosestTractorBeamTarget()
    {
        Vector2 sourcePosition = transform.position;
        PhotonView bestView = null;
        float bestDistance = float.MaxValue;
        PhotonView[] views = FindObjectsByType<PhotonView>(FindObjectsInactive.Exclude);
        for (int i = 0; i < views.Length; i++)
        {
            PhotonView candidateView = views[i];
            if (candidateView == null || candidateView == photonView)
                continue;

            if (!IsValidTractorBeamTarget(candidateView))
                continue;

            Collider2D collider = candidateView.GetComponent<Collider2D>();
            Vector2 closest = collider != null ? collider.ClosestPoint(sourcePosition) : (Vector2)candidateView.transform.position;
            float distance = Vector2.Distance(sourcePosition, closest);
            if (distance > TractorBeamRadius || distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestView = candidateView;
        }

        return bestView;
    }

    bool IsValidTractorBeamTarget(PhotonView candidateView)
    {
        if (candidateView == null)
            return false;

        Treasure treasure = candidateView.GetComponent<Treasure>();
        if (treasure != null)
            return !treasure.isBeingCollected;

        ShipWreck wreck = candidateView.GetComponent<ShipWreck>();
        if (wreck != null)
            return wreck.HasLoot && !wreck.isBeingCollected;

        DroppedCargoCrate crate = candidateView.GetComponent<DroppedCargoCrate>();
        if (crate != null)
            return crate.HasLoot && !crate.isBeingCollected;

        return false;
    }

    IEnumerator TractorBeamPullRoutine(int targetViewId)
    {
        float elapsed = 0f;
        WaitForFixedUpdate wait = new WaitForFixedUpdate();
        while (elapsed < TractorBeamMaxDuration)
        {
            PhotonView targetView = PhotonView.Find(targetViewId);
            if (targetView == null || !IsValidTractorBeamTarget(targetView))
                break;

            ApplyTractorBeamPull(targetView, Time.fixedDeltaTime);
            elapsed += Time.fixedDeltaTime;
            yield return wait;
        }

        authoritativeTractorBeamRoutine = null;
        StopAuthoritativeTractorBeam(true);
    }

    void ApplyTractorBeamPull(PhotonView targetView, float deltaTime)
    {
        if (targetView == null || deltaTime <= 0f)
            return;

        Vector2 sourcePosition = transform.position;
        MovingSpaceObject movingObject = targetView.GetComponent<MovingSpaceObject>();
        if (movingObject != null)
        {
            movingObject.ApplyTractorTetherPull(sourcePosition, TractorBeamPullStrength, TractorBeamSlackDistance, deltaTime);
            return;
        }

        Rigidbody2D targetBody = targetView.GetComponent<Rigidbody2D>();
        if (targetBody == null)
            return;

        Vector2 toSource = sourcePosition - targetBody.position;
        float distance = toSource.magnitude;
        if (distance < 0.08f)
        {
            targetBody.linearVelocity *= 0.88f;
            return;
        }

        float stretch = Mathf.Max(0f, distance - TractorBeamSlackDistance);
        float tetherRamp = Mathf.Clamp01(stretch / TractorBeamTetherRampDistance);
        float pullAcceleration = TractorBeamPullStrength * Mathf.Lerp(0.78f, 3.2f, tetherRamp);
        targetBody.linearVelocity += toSource.normalized * pullAcceleration * deltaTime;
        float maxSpeed = Mathf.Lerp(6.4f, 11.5f, tetherRamp);
        if (targetBody.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            targetBody.linearVelocity = targetBody.linearVelocity.normalized * maxSpeed;
    }

    [PunRPC]
    void StartTractorBeamEffects(int sourceViewId, int targetViewId)
    {
        TractorBeamVfx.StartBeam(sourceViewId, targetViewId);
    }

    [PunRPC]
    void StopTractorBeamEffects(int sourceViewId)
    {
        TractorBeamVfx.StopBeam(sourceViewId);
    }

    int GetAuthoritativeRemainingChargesOnMaster(int actorNumber, string itemId, int maxCharges)
    {
        if (actorNumber <= 0 || string.IsNullOrWhiteSpace(itemId))
            return 0;

        Dictionary<int, Dictionary<string, int>> chargesByActor = DeserializeAuthoritativeGadgetChargeState(GetAuthoritativeGadgetChargeStateRaw());
        if (chargesByActor.TryGetValue(actorNumber, out Dictionary<string, int> actorCharges) &&
            actorCharges != null &&
            actorCharges.TryGetValue(itemId, out int remainingCharges))
        {
            return Mathf.Clamp(remainingCharges, 0, maxCharges);
        }

        return maxCharges;
    }

    void SetAuthoritativeRemainingChargesOnMaster(int actorNumber, string itemId, int remainingCharges, int maxCharges)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || actorNumber <= 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        Dictionary<int, Dictionary<string, int>> chargesByActor = DeserializeAuthoritativeGadgetChargeState(GetAuthoritativeGadgetChargeStateRaw());
        if (!chargesByActor.TryGetValue(actorNumber, out Dictionary<string, int> actorCharges) || actorCharges == null)
        {
            actorCharges = new Dictionary<string, int>(StringComparer.Ordinal);
            chargesByActor[actorNumber] = actorCharges;
        }

        if (remainingCharges >= maxCharges)
            actorCharges.Remove(itemId);
        else
            actorCharges[itemId] = Mathf.Max(0, remainingCharges);

        if (actorCharges.Count == 0)
            chargesByActor.Remove(actorNumber);

        string serializedState = SerializeAuthoritativeGadgetChargeState(chargesByActor);
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.GadgetChargesStateKey] = serializedState
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        lastAuthoritativeGadgetChargeStateRaw = null;
    }

    static void ParseAuthoritativeGadgetChargesForActor(string serializedState, int actorNumber, Dictionary<string, int> destination)
    {
        if (destination == null)
            return;

        destination.Clear();
        if (string.IsNullOrWhiteSpace(serializedState) || actorNumber <= 0)
            return;

        Dictionary<int, Dictionary<string, int>> chargesByActor = DeserializeAuthoritativeGadgetChargeState(serializedState);
        if (!chargesByActor.TryGetValue(actorNumber, out Dictionary<string, int> actorCharges) || actorCharges == null)
            return;

        foreach (KeyValuePair<string, int> pair in actorCharges)
            destination[pair.Key] = Mathf.Max(0, pair.Value);
    }

    static Dictionary<int, Dictionary<string, int>> DeserializeAuthoritativeGadgetChargeState(string serializedState)
    {
        Dictionary<int, Dictionary<string, int>> chargesByActor = new Dictionary<int, Dictionary<string, int>>();
        if (string.IsNullOrWhiteSpace(serializedState))
            return chargesByActor;

        string[] actorEntries = serializedState.Split(';');
        for (int i = 0; i < actorEntries.Length; i++)
        {
            string actorEntry = actorEntries[i];
            if (string.IsNullOrWhiteSpace(actorEntry))
                continue;

            int separatorIndex = actorEntry.IndexOf('#');
            if (separatorIndex <= 0)
                continue;

            string actorRaw = actorEntry.Substring(0, separatorIndex);
            if (!int.TryParse(actorRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int actorNumber) || actorNumber <= 0)
                continue;

            string itemsRaw = actorEntry.Substring(separatorIndex + 1);
            if (string.IsNullOrWhiteSpace(itemsRaw))
                continue;

            Dictionary<string, int> actorCharges = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] itemEntries = itemsRaw.Split(',');
            for (int itemIndex = 0; itemIndex < itemEntries.Length; itemIndex++)
            {
                string itemEntry = itemEntries[itemIndex];
                if (string.IsNullOrWhiteSpace(itemEntry))
                    continue;

                int itemSeparatorIndex = itemEntry.IndexOf('=');
                if (itemSeparatorIndex <= 0 || itemSeparatorIndex >= itemEntry.Length - 1)
                    continue;

                string itemId = itemEntry.Substring(0, itemSeparatorIndex);
                string remainingRaw = itemEntry.Substring(itemSeparatorIndex + 1);
                if (string.IsNullOrWhiteSpace(itemId) ||
                    !int.TryParse(remainingRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int remainingCharges))
                {
                    continue;
                }

                actorCharges[itemId] = Mathf.Max(0, remainingCharges);
            }

            if (actorCharges.Count > 0)
                chargesByActor[actorNumber] = actorCharges;
        }

        return chargesByActor;
    }

    static string SerializeAuthoritativeGadgetChargeState(Dictionary<int, Dictionary<string, int>> chargesByActor)
    {
        if (chargesByActor == null || chargesByActor.Count == 0)
            return string.Empty;

        List<int> actorNumbers = new List<int>(chargesByActor.Keys);
        actorNumbers.Sort();

        StringBuilder builder = new StringBuilder();
        for (int actorIndex = 0; actorIndex < actorNumbers.Count; actorIndex++)
        {
            int actorNumber = actorNumbers[actorIndex];
            if (!chargesByActor.TryGetValue(actorNumber, out Dictionary<string, int> actorCharges) || actorCharges == null || actorCharges.Count == 0)
                continue;

            List<string> itemIds = new List<string>(actorCharges.Keys);
            itemIds.Sort(StringComparer.Ordinal);

            StringBuilder actorBuilder = new StringBuilder();
            for (int itemIndex = 0; itemIndex < itemIds.Count; itemIndex++)
            {
                string itemId = itemIds[itemIndex];
                if (string.IsNullOrWhiteSpace(itemId) || !actorCharges.TryGetValue(itemId, out int remainingCharges))
                    continue;

                if (actorBuilder.Length > 0)
                    actorBuilder.Append(',');

                actorBuilder.Append(itemId);
                actorBuilder.Append('=');
                actorBuilder.Append(Mathf.Max(0, remainingCharges).ToString(CultureInfo.InvariantCulture));
            }

            if (actorBuilder.Length == 0)
                continue;

            if (builder.Length > 0)
                builder.Append(';');

            builder.Append(actorNumber.ToString(CultureInfo.InvariantCulture));
            builder.Append('#');
            builder.Append(actorBuilder);
        }

        return builder.ToString();
    }

    void EnsureBotBootstrap()
    {
        if (!EnemyBot.IsBotInstantiationData(photonView != null ? photonView.InstantiationData : null))
            return;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot == null)
            bot = gameObject.AddComponent<EnemyBot>();

        bot.InitializeFromPhotonData();
    }

    void EnsureNeutralRiderBootstrap()
    {
        if (!NeutralRiderController.IsNeutralRiderInstantiationData(photonView != null ? photonView.InstantiationData : null))
            return;

        NeutralRiderController rider = GetComponent<NeutralRiderController>();
        if (rider == null)
            rider = gameObject.AddComponent<NeutralRiderController>();

        rider.InitializeFromPhotonData();
    }
}

public sealed class AdvancedShootInputZone : MonoBehaviourPun
{
    const string ZoneObjectName = "AdvancedShootInputZone";
    const float HoldToFloatDelay = 0.24f;
    const float DragToFloatPixels = 16f;
    static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>(16);

    PlayerShooting shooting;
    Joystick shootJoystick;
    GameObject zoneObject;
    RectTransform zoneRect;
    Image zoneImage;
    AdvancedShootInputZoneSurface surface;
    bool pointerHeld;
    bool floatingJoystickActive;
    bool waitForFreshPointerRelease = true;
    int pointerId = int.MinValue;
    int pointerDownFrame;
    float pointerDownAt;
    Vector2 pointerDownScreenPosition;
    Vector2 latestPointerScreenPosition;
    Camera pointerCamera;

    void Start()
    {
        shooting = GetComponent<PlayerShooting>();
        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        EnsureZone();
        RefreshState();
    }

    void Update()
    {
        if (!photonView.IsMine)
            return;

        EnsureZone();
        RefreshState();
        RestoreShootJoystickHomeIfIdle();
        UpdatePolledPointerInput();

        if (pointerHeld && Time.frameCount > pointerDownFrame && !IsTrackedPointerStillDown())
        {
            HandleLostPointerRelease();
            return;
        }

        if (!pointerHeld || floatingJoystickActive || shooting == null || !shooting.CanUseAdvancedShootJoystick())
            return;

        if (Time.time - pointerDownAt >= HoldToFloatDelay)
            ActivateFloatingJoystick(pointerDownScreenPosition, latestPointerScreenPosition, pointerCamera);

        if (floatingJoystickActive && shootJoystick != null)
            shootJoystick.UpdateExternalControl(latestPointerScreenPosition, pointerCamera);
    }

    void OnDisable()
    {
        CancelCurrentPress(true);
    }

    void OnDestroy()
    {
        CancelCurrentPress(true);
        if (zoneObject != null)
            Destroy(zoneObject);
    }

    public void HandlePointerDown(PointerEventData eventData)
    {
        if (eventData == null || shooting == null || !shooting.CanUseAdvancedShootJoystick())
            return;

        BeginPointerPress(eventData.pointerId, eventData.position, eventData.pressEventCamera);
    }

    public void HandleDrag(PointerEventData eventData)
    {
        if (!IsMatchingPointer(eventData) || shooting == null || !shooting.CanUseAdvancedShootJoystick())
            return;

        latestPointerScreenPosition = eventData.position;

        if (!floatingJoystickActive)
        {
            if (Vector2.Distance(pointerDownScreenPosition, eventData.position) >= DragToFloatPixels)
                ActivateFloatingJoystick(pointerDownScreenPosition, latestPointerScreenPosition, pointerCamera);
            else
                return;
        }

        if (shootJoystick != null)
            shootJoystick.UpdateExternalControl(eventData.position, pointerCamera);
    }

    public void HandlePointerUp(PointerEventData eventData)
    {
        if (!IsMatchingPointer(eventData))
            return;

        ReleaseCurrentPress();
    }

    void ReleaseCurrentPress()
    {
        if (!pointerHeld)
            return;

        bool triggerTapShot = pointerHeld && !floatingJoystickActive;
        bool triggerFloatingShot = pointerHeld && floatingJoystickActive;
        bool floatingShotReleased = triggerFloatingShot && shooting != null && shooting.ReleaseAdvancedFloatingAim();
        CancelCurrentPress(true);

        if (triggerTapShot && shooting != null)
            shooting.TriggerAdvancedAutoAimShot();
        else if (triggerFloatingShot && !floatingShotReleased && shooting != null)
            shooting.TriggerAdvancedAutoAimShot();
    }

    void UpdatePolledPointerInput()
    {
        if (shooting == null || !shooting.CanUseAdvancedShootJoystick())
            return;

        if (waitForFreshPointerRelease)
        {
            if (IsAnyPointerCurrentlyDown())
                return;

            waitForFreshPointerRelease = false;
        }

        if (!pointerHeld)
        {
            TryBeginPolledPointerPress();
            return;
        }

        if (pointerId == -1)
        {
            if (TryGetMousePointer(out Vector2 mousePosition, out bool mousePressed, out _, out bool mouseReleased))
            {
                if (mousePressed)
                {
                    latestPointerScreenPosition = mousePosition;
                    if (floatingJoystickActive && shootJoystick != null)
                        shootJoystick.UpdateExternalControl(latestPointerScreenPosition, pointerCamera);
                }

                if (mouseReleased)
                    ReleaseCurrentPress();
            }

            return;
        }

        if (pointerId < 0)
            return;

        if (TryGetTrackedTouch(pointerId, out Vector2 touchPosition, out bool touchActive, out bool touchReleased))
        {
            if (touchActive)
            {
                latestPointerScreenPosition = touchPosition;
                if (floatingJoystickActive && shootJoystick != null)
                    shootJoystick.UpdateExternalControl(latestPointerScreenPosition, pointerCamera);
            }

            if (touchReleased)
                ReleaseCurrentPress();
        }
    }

    void TryBeginPolledPointerPress()
    {
        if (TryGetNewTouchPress(out int touchPointerId, out Vector2 touchPosition))
        {
            if (ShouldBeginPolledShootPress(touchPosition))
                BeginPointerPress(touchPointerId, touchPosition, null);

            return;
        }

        if (!TryGetMousePointer(out Vector2 mousePosition, out _, out bool mousePressedThisFrame, out _) || !mousePressedThisFrame)
            return;

        if (!ShouldBeginPolledShootPress(mousePosition))
            return;

        BeginPointerPress(-1, mousePosition, null);
    }

    void BeginPointerPress(int newPointerId, Vector2 screenPosition, Camera eventCamera)
    {
        pointerHeld = true;
        floatingJoystickActive = false;
        pointerId = newPointerId;
        pointerDownFrame = Time.frameCount;
        pointerDownAt = Time.time;
        pointerDownScreenPosition = screenPosition;
        latestPointerScreenPosition = screenPosition;
        pointerCamera = eventCamera;
    }

    void EnsureZone()
    {
        if (zoneObject != null && zoneRect != null && zoneImage != null && surface != null)
        {
            if (shootJoystick == null)
                shootJoystick = FindShootJoystick();
            zoneImage.raycastTarget = false;
            PlaceZoneBehindShootJoystick();
            return;
        }

        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find(ZoneObjectName);
        if (existing != null)
            zoneObject = existing.gameObject;
        else
            zoneObject = new GameObject(ZoneObjectName, typeof(RectTransform), typeof(Image), typeof(AdvancedShootInputZoneSurface));

        if (zoneObject.transform.parent != canvas.transform)
            zoneObject.transform.SetParent(canvas.transform, false);

        zoneRect = zoneObject.GetComponent<RectTransform>();
        zoneRect.anchorMin = new Vector2(0.5f, 0f);
        zoneRect.anchorMax = new Vector2(1f, 1f);
        zoneRect.offsetMin = Vector2.zero;
        zoneRect.offsetMax = Vector2.zero;

        zoneImage = zoneObject.GetComponent<Image>();
        zoneImage.color = new Color(1f, 1f, 1f, 0.001f);
        zoneImage.raycastTarget = false;

        surface = zoneObject.GetComponent<AdvancedShootInputZoneSurface>();
        surface.Owner = this;
        shootJoystick = FindShootJoystick();
        PlaceZoneBehindShootJoystick();
    }

    void RefreshState()
    {
        if (zoneObject == null || shooting == null)
            return;

        bool active = shooting.CanUseAdvancedShootJoystick();
        if (zoneObject.activeSelf != active)
        {
            zoneObject.SetActive(active);
            if (active)
                waitForFreshPointerRelease = true;
        }

        if (!active)
        {
            CancelCurrentPress(true);
            waitForFreshPointerRelease = true;
        }
    }

    void ActivateFloatingJoystick(Vector2 baseScreenPosition, Vector2 controlScreenPosition, Camera eventCamera)
    {
        if (floatingJoystickActive)
            return;

        shootJoystick = shootJoystick != null ? shootJoystick : FindShootJoystick();
        if (shootJoystick == null)
            return;

        floatingJoystickActive = true;
        shootJoystick.BeginExternalControl(baseScreenPosition, eventCamera, true);
        shootJoystick.UpdateExternalControl(controlScreenPosition, eventCamera);
    }

    void CancelCurrentPress(bool restoreJoystick)
    {
        if (floatingJoystickActive && shootJoystick != null)
            shootJoystick.EndExternalControl(restoreJoystick);

        pointerHeld = false;
        floatingJoystickActive = false;
        pointerId = int.MinValue;
        latestPointerScreenPosition = Vector2.zero;
    }

    static bool IsAnyPointerCurrentlyDown()
    {
        if (TryGetMousePointer(out _, out bool mousePressed, out _, out _) && mousePressed)
            return true;

        if (IsAnyTouchCurrentlyDown())
            return true;

        return false;
    }

    static bool TryGetMousePointer(out Vector2 position, out bool pressed, out bool pressedThisFrame, out bool releasedThisFrame)
    {
        position = Vector2.zero;
        pressed = false;
        pressedThisFrame = false;
        releasedThisFrame = false;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            position = mouse.position.ReadValue();
            pressed = mouse.leftButton.isPressed;
            pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            releasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        position = Input.mousePosition;
        pressed = Input.GetMouseButton(0);
        pressedThisFrame = Input.GetMouseButtonDown(0);
        releasedThisFrame = Input.GetMouseButtonUp(0);
        return true;
#else
        return false;
#endif
    }

    static bool TryGetNewTouchPress(out int pointerId, out Vector2 position)
    {
        pointerId = int.MinValue;
        position = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Touchscreen touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (UnityEngine.InputSystem.Controls.TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.wasPressedThisFrame)
                    continue;

                pointerId = touch.touchId.ReadValue();
                position = touch.position.ReadValue();
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
                continue;

            pointerId = touch.fingerId;
            position = touch.position;
            return true;
        }
#endif

        return false;
    }

    static bool TryGetTrackedTouch(int pointerId, out Vector2 position, out bool active, out bool released)
    {
        position = Vector2.zero;
        active = false;
        released = false;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Touchscreen touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (UnityEngine.InputSystem.Controls.TouchControl touch in touchscreen.touches)
            {
                if (touch.touchId.ReadValue() != pointerId)
                    continue;

                UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
                position = touch.position.ReadValue();
                active = touch.press.isPressed || phase == UnityEngine.InputSystem.TouchPhase.Began || phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary;
                released = touch.press.wasReleasedThisFrame || phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled;
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != pointerId)
                continue;

            position = touch.position;
            active = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
            released = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            return true;
        }
#endif

        return false;
    }

    static bool IsAnyTouchCurrentlyDown()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Touchscreen touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (UnityEngine.InputSystem.Controls.TouchControl touch in touchscreen.touches)
            {
                UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
                if (touch.press.isPressed || phase == UnityEngine.InputSystem.TouchPhase.Began || phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                    return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < Input.touchCount; i++)
        {
            TouchPhase phase = Input.GetTouch(i).phase;
            if (phase != TouchPhase.Ended && phase != TouchPhase.Canceled)
                return true;
        }
#endif

        return false;
    }

    void HandleLostPointerRelease()
    {
        ReleaseCurrentPress();
        RestoreShootJoystickHomeIfIdle();
    }

    void RestoreShootJoystickHomeIfIdle()
    {
        shootJoystick = shootJoystick != null ? shootJoystick : FindShootJoystick();
        if (shootJoystick == null || pointerHeld || floatingJoystickActive || shootJoystick.IsPressed || shootJoystick.IsExternalControlActive)
            return;

        if (!shootJoystick.IsAtDefaultBackgroundPosition())
            shootJoystick.RestoreDefaultBackgroundPosition();
    }

    bool IsTrackedPointerStillDown()
    {
        if (pointerId == -1)
            return TryGetMousePointer(out _, out bool mousePressed, out _, out _) && mousePressed;
        if (pointerId < 0)
            return IsAnyPointerCurrentlyDown();

        return TryGetTrackedTouch(pointerId, out _, out bool touchActive, out bool touchReleased) && touchActive && !touchReleased;
    }

    bool IsMatchingPointer(PointerEventData eventData)
    {
        return pointerHeld && eventData != null && eventData.pointerId == pointerId;
    }

    bool ShouldBeginPolledShootPress(Vector2 screenPosition)
    {
        if (screenPosition.x < Screen.width * 0.5f)
            return false;

        return !IsPointerOverBlockedUi(screenPosition);
    }

    bool IsPointerOverBlockedUi(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        UiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, UiRaycastResults);

        for (int i = 0; i < UiRaycastResults.Count; i++)
        {
            GameObject target = UiRaycastResults[i].gameObject;
            if (target == null)
                continue;

            if (target.GetComponentInParent<AdvancedShootInputZoneSurface>() != null)
                continue;

            Joystick joystick = target.GetComponentInParent<Joystick>();
            if (joystick != null)
            {
                if (IsShootJoystick(joystick))
                    return IsInsideJoystickCircle(joystick, screenPosition);

                return true;
            }

            if (target.GetComponentInParent<Selectable>() != null)
                return true;
        }

        return false;
    }

    bool IsShootJoystick(Joystick joystick)
    {
        if (joystick == null)
            return false;

        shootJoystick = shootJoystick != null ? shootJoystick : FindShootJoystick();
        if (joystick == shootJoystick)
            return true;

        if (joystick.name == "ShootJoystickBG")
            return true;

        return joystick.background != null && joystick.background.name == "ShootJoystickBG";
    }

    static bool IsInsideJoystickCircle(Joystick joystick, Vector2 screenPosition)
    {
        RectTransform rect = joystick != null ? joystick.background : null;
        if (rect == null)
            return false;

        Camera eventCamera = null;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, eventCamera, out Vector2 localPoint))
            return false;

        float radius = Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f;
        return localPoint.sqrMagnitude <= radius * radius;
    }

    void PlaceZoneBehindShootJoystick()
    {
        if (zoneObject == null || zoneObject.transform.parent == null)
            return;

        RectTransform joystickRect = FindShootJoystickRectTransform();
        if (joystickRect == null || joystickRect.parent != zoneObject.transform.parent)
            return;

        int joystickIndex = joystickRect.GetSiblingIndex();
        zoneObject.transform.SetSiblingIndex(Mathf.Max(0, joystickIndex));
    }

    static RectTransform FindShootJoystickRectTransform()
    {
        Joystick joystick = FindShootJoystick();
        if (joystick == null)
            return null;

        if (joystick.background != null)
            return joystick.background;

        return joystick.GetComponent<RectTransform>();
    }

    static Joystick FindShootJoystick()
    {
        GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
        Joystick joystick = shootJoystickObject != null ? shootJoystickObject.GetComponent<Joystick>() : null;
        if (joystick != null)
            return joystick;

        Joystick[] candidates = FindObjectsByType<Joystick>(FindObjectsInactive.Exclude);
        foreach (Joystick candidate in candidates)
        {
            if (candidate == null)
                continue;

            if (candidate.name == "ShootJoystickBG")
                return candidate;

            if (candidate.background != null && candidate.background.name == "ShootJoystickBG")
                return candidate;
        }

        return null;
    }
}

public sealed class AdvancedShootInputZoneSurface : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public AdvancedShootInputZone Owner;

    public void OnPointerDown(PointerEventData eventData)
    {
        Owner?.HandlePointerDown(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Owner?.HandleDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Owner?.HandlePointerUp(eventData);
    }
}

[RequireComponent(typeof(PlayerShooting))]
public class GadgetButtonUI : MonoBehaviourPun
{
    sealed class GadgetButtonWidget
    {
        public string ItemId;
        public GameObject Root;
        public Button Button;
        public Image Background;
        public Image Icon;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Charges;
    }

    const string GadgetButtonRootName = "GadgetButtonsRoot";
    const float GadgetButtonSize = 112f;
    const float GadgetButtonBaseOffsetY = 392f;
    const float GadgetButtonPitch = 126f;
    const float GadgetButtonTopPadding = 24f;
    static Sprite circularButtonSprite;

    PlayerShooting shooting;
    GameObject rootObject;
    RectTransform rootRect;
    RectTransform canvasRect;
    RectTransform shootJoystickRect;
    readonly List<GadgetButtonWidget> widgets = new List<GadgetButtonWidget>();
    string lastWidgetSignature = string.Empty;

    void Start()
    {
        shooting = GetComponent<PlayerShooting>();

        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        RebuildButtonsIfNeeded();
        RefreshState();
    }

    void Update()
    {
        RebuildButtonsIfNeeded();
        RefreshRuntimeLayout();
        RefreshState();
    }

    void OnDestroy()
    {
        DestroyAllButtons();
    }

    void RebuildButtonsIfNeeded()
    {
        if (!photonView.IsMine || shooting == null)
            return;

        IReadOnlyList<string> itemIds = shooting.ActiveGadgetItemIds;
        string signature = BuildWidgetSignature(itemIds);
        if (signature == lastWidgetSignature && rootObject != null && widgets.Count == itemIds.Count)
            return;

        DestroyAllButtons();
        CreateButtons(itemIds);
        lastWidgetSignature = signature;
    }

    void CreateButtons(IReadOnlyList<string> itemIds)
    {
        GameObject canvas = GameObject.Find("Canvas");
        GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
        if (canvas == null || shootJoystickObject == null)
            return;

        canvasRect = canvas.GetComponent<RectTransform>();
        shootJoystickRect = shootJoystickObject.GetComponent<RectTransform>();
        if (canvasRect == null || shootJoystickRect == null)
            return;

        GameObject existingRoot = GameObject.Find(GadgetButtonRootName);
        if (existingRoot != null)
            Destroy(existingRoot);

        rootObject = new GameObject(GadgetButtonRootName, typeof(RectTransform));
        rootObject.transform.SetParent(canvas.transform, false);

        rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = Vector2.zero;
        ApplyRootLayoutFromJoystick();

        int rowsPerColumn = CalculateRowsPerColumn(itemIds.Count);

        for (int i = 0; i < itemIds.Count; i++)
        {
            GadgetButtonWidget widget = CreateWidget(itemIds[i], i, rowsPerColumn);
            if (widget != null)
                widgets.Add(widget);
        }
    }

    GadgetButtonWidget CreateWidget(string itemId, int index, int rowsPerColumn)
    {
        if (rootObject == null || string.IsNullOrWhiteSpace(itemId))
            return null;

        GadgetButtonWidget widget = new GadgetButtonWidget();
        widget.ItemId = itemId;
        widget.Root = new GameObject("GadgetButton_" + itemId, typeof(RectTransform), typeof(Image), typeof(Button));
        widget.Root.transform.SetParent(rootObject.transform, false);

        RectTransform rect = widget.Root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = GetWidgetAnchoredPosition(index, rowsPerColumn);
        rect.sizeDelta = new Vector2(GadgetButtonSize, GadgetButtonSize);

        widget.Background = widget.Root.GetComponent<Image>();
        widget.Background.sprite = GetCircularButtonSprite();
        widget.Background.type = Image.Type.Simple;

        widget.Button = widget.Root.GetComponent<Button>();
        widget.Button.transition = Selectable.Transition.ColorTint;
        widget.Button.targetGraphic = widget.Background;
        string capturedItemId = itemId;
        if (shooting != null && shooting.IsHoldGadget(capturedItemId))
            ConfigureHoldGadgetInput(widget.Button, capturedItemId);
        else
            widget.Button.onClick.AddListener(() => HandleGadgetClicked(capturedItemId));

        TMP_Text referenceText = FindAnyObjectByType<TMP_Text>();

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(widget.Root.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 8f);
        iconRect.sizeDelta = new Vector2(60f, 60f);
        widget.Icon = iconObject.GetComponent<Image>();
        widget.Icon.preserveAspect = true;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(widget.Root.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0f);
        labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 14f);
        labelRect.sizeDelta = new Vector2(90f, 24f);
        widget.Label = labelObject.GetComponent<TextMeshProUGUI>();
        widget.Label.fontSize = 18f;
        widget.Label.fontStyle = FontStyles.Bold;
        widget.Label.alignment = TextAlignmentOptions.Center;
        widget.Label.textWrappingMode = TextWrappingModes.NoWrap;
        if (referenceText != null)
        {
            widget.Label.font = referenceText.font;
            widget.Label.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        GameObject chargesObject = new GameObject("Charges", typeof(RectTransform), typeof(TextMeshProUGUI));
        chargesObject.transform.SetParent(widget.Root.transform, false);
        RectTransform chargesRect = chargesObject.GetComponent<RectTransform>();
        chargesRect.anchorMin = new Vector2(0.5f, 0f);
        chargesRect.anchorMax = new Vector2(0.5f, 0f);
        chargesRect.pivot = new Vector2(0.5f, 0f);
        chargesRect.anchoredPosition = new Vector2(0f, -8f);
        chargesRect.sizeDelta = new Vector2(72f, 24f);
        widget.Charges = chargesObject.GetComponent<TextMeshProUGUI>();
        widget.Charges.fontSize = 20f;
        widget.Charges.fontStyle = FontStyles.Bold;
        widget.Charges.alignment = TextAlignmentOptions.Center;
        widget.Charges.textWrappingMode = TextWrappingModes.NoWrap;
        if (referenceText != null)
        {
            widget.Charges.font = referenceText.font;
            widget.Charges.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        return widget;
    }

    void RefreshRuntimeLayout()
    {
        if (rootObject == null || rootRect == null || widgets.Count == 0)
            return;

        if (shootJoystickRect == null)
        {
            GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
            shootJoystickRect = shootJoystickObject != null ? shootJoystickObject.GetComponent<RectTransform>() : null;
        }

        if (canvasRect == null)
        {
            Transform parent = rootObject.transform.parent;
            canvasRect = parent != null ? parent.GetComponent<RectTransform>() : null;
        }

        ApplyRootLayoutFromJoystick();
        int rowsPerColumn = CalculateRowsPerColumn(widgets.Count);
        for (int i = 0; i < widgets.Count; i++)
        {
            GadgetButtonWidget widget = widgets[i];
            RectTransform widgetRect = widget?.Root != null ? widget.Root.GetComponent<RectTransform>() : null;
            if (widgetRect != null)
                widgetRect.anchoredPosition = GetWidgetAnchoredPosition(i, rowsPerColumn);
        }
    }

    void ApplyRootLayoutFromJoystick()
    {
        if (rootRect == null || shootJoystickRect == null)
            return;

        rootRect.anchorMin = shootJoystickRect.anchorMin;
        rootRect.anchorMax = shootJoystickRect.anchorMax;
        rootRect.anchoredPosition = shootJoystickRect.anchoredPosition;
    }

    int CalculateRowsPerColumn(int buttonCount)
    {
        if (buttonCount <= 0)
            return 1;

        if (rootRect == null || canvasRect == null)
            return buttonCount;

        float maxChildCenterY = canvasRect.rect.yMax - GadgetButtonTopPadding - (GadgetButtonSize * 0.5f) - GetRootCenterY();
        int fittingRows = Mathf.FloorToInt((maxChildCenterY - GadgetButtonBaseOffsetY) / GadgetButtonPitch) + 1;
        return Mathf.Clamp(fittingRows, 1, buttonCount);
    }

    float GetRootCenterY()
    {
        if (rootRect == null || canvasRect == null)
            return 0f;

        Vector2 anchorCenter = (rootRect.anchorMin + rootRect.anchorMax) * 0.5f;
        float anchorY = Mathf.Lerp(canvasRect.rect.yMin, canvasRect.rect.yMax, anchorCenter.y);
        return anchorY + rootRect.anchoredPosition.y;
    }

    static Vector2 GetWidgetAnchoredPosition(int index, int rowsPerColumn)
    {
        rowsPerColumn = Mathf.Max(1, rowsPerColumn);
        int column = Mathf.Max(0, index) / rowsPerColumn;
        int row = Mathf.Max(0, index) % rowsPerColumn;
        return new Vector2(-column * GadgetButtonPitch, GadgetButtonBaseOffsetY + (row * GadgetButtonPitch));
    }

    void ConfigureHoldGadgetInput(Button button, string itemId)
    {
        if (button == null)
            return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        trigger.triggers = new List<EventTrigger.Entry>();
        AddGadgetTrigger(trigger, EventTriggerType.PointerDown, () => shooting?.BeginGadgetUse(itemId));
        AddGadgetTrigger(trigger, EventTriggerType.PointerUp, () => shooting?.EndGadgetUse(itemId));
        AddGadgetTrigger(trigger, EventTriggerType.PointerExit, () => shooting?.EndGadgetUse(itemId));
        AddGadgetTrigger(trigger, EventTriggerType.Cancel, () => shooting?.EndGadgetUse(itemId));
    }

    void AddGadgetTrigger(EventTrigger trigger, EventTriggerType eventType, System.Action callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };
        entry.callback.AddListener(_ => callback?.Invoke());
        trigger.triggers.Add(entry);
    }

    void RefreshState()
    {
        if (shooting == null)
            return;

        if (rootObject != null)
            rootObject.SetActive(GameplayHudVisibility.IsGameplayHudVisible(widgets.Count > 0));

        for (int i = 0; i < widgets.Count; i++)
        {
            GadgetButtonWidget widget = widgets[i];
            if (widget == null || widget.Root == null)
                continue;

            int remaining = shooting.GetRemainingGadgetCharges(widget.ItemId);
            int max = shooting.GetMaxGadgetCharges(widget.ItemId);
            bool canUse = shooting.CanUseGadget(widget.ItemId);
            bool depleted = max > 0 && remaining <= 0;

            widget.Icon.sprite = shooting.GetGadgetIcon(widget.ItemId);
            widget.Icon.enabled = widget.Icon.sprite != null;
            widget.Label.text = shooting.GetGadgetButtonLabel(widget.ItemId);
            widget.Charges.text = max > 0 ? remaining.ToString() : string.Empty;

            widget.Button.interactable = canUse;
            widget.Background.color = canUse
                ? shooting.GetGadgetButtonColor(widget.ItemId)
                : new Color(0.15f, 0.19f, 0.24f, 0.82f);
            widget.Label.color = canUse
                ? Color.white
                : new Color(0.82f, 0.86f, 0.91f, 0.82f);
            widget.Charges.color = canUse
                ? Color.white
                : new Color(0.78f, 0.8f, 0.82f, 0.82f);
            widget.Icon.color = depleted
                ? new Color(0.6f, 0.6f, 0.6f, 0.72f)
                : canUse
                    ? new Color(1f, 1f, 1f, 0.88f)
                    : new Color(0.82f, 0.86f, 0.91f, 0.54f);
        }
    }

    void HandleGadgetClicked(string itemId)
    {
        if (shooting == null)
            return;

        shooting.TriggerGadgetUse(itemId);
    }

    void DestroyAllButtons()
    {
        for (int i = 0; i < widgets.Count; i++)
        {
            if (widgets[i]?.Root != null)
                Destroy(widgets[i].Root);
        }

        widgets.Clear();
        lastWidgetSignature = string.Empty;

        if (rootObject != null)
        {
            Destroy(rootObject);
            rootObject = null;
        }

        rootRect = null;
        canvasRect = null;
        shootJoystickRect = null;
    }

    static string BuildWidgetSignature(IReadOnlyList<string> itemIds)
    {
        if (itemIds == null || itemIds.Count == 0)
            return string.Empty;

        return string.Join("|", itemIds);
    }

    static Sprite GetCircularButtonSprite()
    {
        if (circularButtonSprite != null)
            return circularButtonSprite;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "GadgetButtonCircle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        float feather = size * 0.06f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.InverseLerp(radius - feather, radius, distance);
                pixels[(y * size) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        circularButtonSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return circularButtonSprite;
    }
}
