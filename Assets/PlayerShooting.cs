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

public partial class PlayerShooting : MonoBehaviourPun
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
    const float InstantDeployLocalPendingSeconds = 1.25f;
    const float InstantDeployAuthoritativeDebounceSeconds = 1.4f;
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
    readonly Dictionary<string, float> localInstantDeployPendingUntil = new Dictionary<string, float>(StringComparer.Ordinal);
    readonly Dictionary<string, float> authoritativeInstantDeployDebounceUntil = new Dictionary<string, float>(StringComparer.Ordinal);
    string lastAuthoritativeGadgetChargeStateRaw = null;
    Coroutine authoritativeTractorBeamRoutine;
    EnemyBot cachedEnemyBot;
    PlayerRepairDocking cachedRepairDocking;
    bool hasResolvedEnemyBot;
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

}
