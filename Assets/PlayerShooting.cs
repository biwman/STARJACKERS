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
    const float ManualAimThreshold = 0.35f;
    const float DefaultBulletRangeMultiplier = 15f;
    const float ComplexTapMaxDuration = 0.44f;
    const float ComplexTapMaxDragMagnitude = 0.24f;
    const float AdvancedShootMarkerRawThreshold = 0.05f;
    const float SuperChargeTimeSeconds = 24f;
    const float SuperChargeOnComplexHit = 0.08f;
    const float GadgetMinePlacementCooldown = 0.9f;
    const int GadgetMineDefaultCharges = 4;
    const int BatteryDefaultCharges = 3;
    const int MagneticBeamDefaultCharges = 3;
    const int TractorBeamDefaultCharges = 4;
    const int LureBeaconDefaultCharges = 2;
    const int AutoTurretDefaultCharges = 1;
    const int GuidanceSystemDefaultCharges = 2;
    const int SpaceDrillDefaultCharges = 2;
    const int SpaceTrapDefaultCharges = 1;
    const int SuperBoosterDefaultCharges = 2;
    const float MagneticBeamRadius = 8f;
    const float MagneticBeamDuration = 3f;
    const float MagneticBeamPullStrength = 24f;
    const float TractorBeamRadius = 8f;
    const float TractorBeamMaxDuration = 10f;
    const float TractorBeamPullStrength = 36f;
    const float LureBeaconDeployDistance = 1.15f;
    const float LureBeaconSpawnClearanceRadius = 0.42f;
    const float GuidanceSystemDuration = 9f;
    const float SuperBoosterDuration = 2f;
    const float AstroCutterTickInterval = 0.2f;
    const float AstroCutterBeamRadius = 0.14f;
    const float AstroCutterSuperBeamRadius = 0.28f;
    const int AstroCutterObstacleDamageMultiplier = 4;
    const int AstroCutterSuperObstacleDamageMultiplier = 6;
    static readonly Color PlasmaBulletColor = new Color(0.15f, 1f, 0.28f, 1f);

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

    public int CurrentAmmo => IsComplexShootingActive && GetActiveComplexWeaponState() != null ? GetActiveComplexWeaponState().CurrentAmmo : currentAmmo;
    public int MaxAmmo => IsComplexShootingActive && GetActiveComplexWeaponState() != null ? GetActiveComplexWeaponState().MaxAmmo : maxAmmo;
    public bool IsReloading => isReloading;
    public bool IsComplexShootingActive => RoomSettings.IsComplexShootingModel() && GetComponent<EnemyBot>() == null && !AstronautSurvivor.IsAstronautInstantiationData(photonView != null ? photonView.InstantiationData : null);
    public float ComplexAmmoReloadProgress => GetComplexAmmoReloadProgress();
    public float SuperChargeNormalized => Mathf.Clamp01(superCharge);
    public bool IsSuperAttackReady => IsComplexShootingActive && RoomSettings.IsSuperAttackEnabled() && superCharge >= 0.999f;
    public bool CanManualReload => photonView.IsMine && IsGameStarted() && !IsComplexShootingActive && !isReloading && currentAmmo > 0 && currentAmmo < maxAmmo;
    public int ComplexWeaponCount => complexWeaponStates.Count;
    public int ActiveComplexWeaponNumber => complexWeaponStates.Count > 0 ? activeComplexWeaponIndex + 1 : 1;
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

    void Start()
    {
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
        CaptureBaseWeaponProfile();
        maxAmmo = GetConfiguredMaxAmmo();
        currentAmmo = maxAmmo;

        if (AstronautSurvivor.IsAstronautInstantiationData(photonView.InstantiationData))
            return;

        if (GetComponent<EnemyBot>() != null)
            return;

        if (!AstronautSurvivor.IsAstronautInstantiationData(photonView.InstantiationData) && GetComponent<LootingFriendController>() == null)
            gameObject.AddComponent<LootingFriendController>();

        if (GetComponent<GuidanceSystemOverlay>() == null)
            gameObject.AddComponent<GuidanceSystemOverlay>();

        if (!photonView.IsMine)
            return;

        if (GetComponent<AmmoUI>() == null)
        {
            gameObject.AddComponent<AmmoUI>();
        }

        if (GetComponent<ReloadButtonUI>() == null)
        {
            gameObject.AddComponent<ReloadButtonUI>();
        }

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

        if (!IsGameStarted())
            return;

        if (GetComponent<EnemyBot>() != null)
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

        if (IsComplexShootingActive)
        {
            SyncComplexWeaponProfile();
            UpdateComplexAmmoReload();
            UpdateSuperCharge();
            HandleComplexShootingInput();
            return;
        }

        HideAimMarker();
        SyncEquippedWeaponProfile();
        SyncAmmoSetting();
        UpdateReload();

        if (shootJoystick == null)
        {
            GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
            if (shootJoystickObject != null)
            {
                shootJoystick = shootJoystickObject.GetComponent<Joystick>();
            }
        }

        if (shootJoystick == null)
            return;

        if (isReloading || currentAmmo <= 0)
            return;

        if (!shootJoystick.IsPressed)
            return;

        Vector2 direction = ResolveManualAimDirection();

        if (Time.time >= nextFireTime)
        {
            Shoot(direction.normalized);
            ConsumeAmmo();
            nextFireTime = Time.time + fireRate;
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
        if (shootJoystick != null)
            return;

        GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
        if (shootJoystickObject != null)
            shootJoystick = shootJoystickObject.GetComponent<Joystick>();
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

                ShowAimMarker(profile, complexLastAimDirection, IsArcWeaponProfile(profile) ? complexLastAimTargetPoint : (Vector2?)null);
            }
            else
            {
                if (complexShootMaxDragMagnitude >= markerThreshold)
                    complexShootCanceledByCenteredAim = true;
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
            HideAimMarker();
            return;
        }

        bool wasTap = IsAdvancedShootJoystickEnabled()
            ? complexShootMaxDragMagnitude <= ComplexTapMaxDragMagnitude
            : Time.time - complexShootPressStartedAt <= ComplexTapMaxDuration &&
              complexShootMaxDragMagnitude <= ComplexTapMaxDragMagnitude;

        Vector2 direction = wasTap
            ? ResolveComplexAutoAimDirection(profile)
            : ResolveSafeAimDirection(complexLastAimDirection);
        Vector2 targetPoint = wasTap && IsArcWeaponProfile(profile)
            ? ResolveComplexAutoAimTargetPoint(profile)
            : complexLastAimTargetPoint;

        complexShootWasPressed = false;
        complexShootCanceledByCenteredAim = false;
        HideAimMarker();
        TryFireComplexAttack(profile, direction, targetPoint, true, false);
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

    bool HandleComplexSuperInput()
    {
        if (superJoystick == null || !RoomSettings.IsSuperAttackEnabled())
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

                ShowAimMarker(superProfile, complexLastSuperAimDirection, IsArcWeaponProfile(superProfile) ? complexLastSuperAimTargetPoint : (Vector2?)null);
            }
            else
            {
                if (complexSuperMaxDragMagnitude >= ManualAimThreshold)
                    complexSuperCanceledByCenteredAim = true;
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
            HideAimMarker();
            return true;
        }

        bool wasTap = Time.time - complexSuperPressStartedAt <= ComplexTapMaxDuration &&
                      complexSuperMaxDragMagnitude <= ComplexTapMaxDragMagnitude;
        Vector2 direction = wasTap
            ? ResolveComplexAutoAimDirection(superProfile)
            : ResolveSafeAimDirection(complexLastSuperAimDirection);
        Vector2 targetPoint = wasTap && IsArcWeaponProfile(superProfile)
            ? ResolveComplexAutoAimTargetPoint(superProfile)
            : complexLastSuperAimTargetPoint;

        complexSuperWasPressed = false;
        complexSuperCanceledByCenteredAim = false;
        HideAimMarker();
        if (TryFireComplexAttack(superProfile, direction, targetPoint, false, true))
            superCharge = 0f;

        return true;
    }

    void ResetComplexPressState()
    {
        complexShootWasPressed = false;
        complexSuperWasPressed = false;
        complexShootCanceledByCenteredAim = false;
        complexSuperCanceledByCenteredAim = false;
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
        activeComplexWeaponProfile = GetActiveComplexWeaponState()?.Profile ?? WeaponAttackCatalog.GetNormalAttackByWeaponId(WeaponAttackCatalog.SimpleGunId);
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
                NextAmmoAt = previous != null ? previous.NextAmmoAt : 0f
            };
            complexWeaponStates.Add(state);
        }

        if (complexWeaponStates.Count == 0)
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

            float reloadTime = Mathf.Max(0f, state.Profile.AmmoReloadTime);
            if (reloadTime <= 0f)
                continue;

            if (state.NextAmmoAt <= 0f)
            {
                state.AmmoReloadStartedAt = Time.time;
                state.NextAmmoAt = Time.time + reloadTime;
            }

            while (state.CurrentAmmo < state.MaxAmmo && state.NextAmmoAt > 0f && Time.time >= state.NextAmmoAt)
            {
                state.CurrentAmmo++;
                if (state.CurrentAmmo >= state.MaxAmmo)
                {
                    state.NextAmmoAt = 0f;
                    state.AmmoReloadStartedAt = 0f;
                    break;
                }

                state.AmmoReloadStartedAt = state.NextAmmoAt;
                state.NextAmmoAt += reloadTime;
            }
        }

        SyncActiveComplexAmmoMirror();
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
        if (!RoomSettings.IsSuperAttackEnabled())
        {
            superCharge = 0f;
            return;
        }

        superCharge = Mathf.Clamp01(superCharge + (Time.deltaTime / SuperChargeTimeSeconds) * GetSuperChargeGainMultiplier());
    }

    bool TryFireComplexAttack(WeaponAttackProfile profile, Vector2 direction, Vector2 targetPoint, bool consumeAmmo, bool isSuper)
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
            if (activeWeaponState.CurrentAmmo < activeWeaponState.MaxAmmo && activeWeaponState.NextAmmoAt <= 0f)
            {
                activeWeaponState.AmmoReloadStartedAt = Time.time;
                activeWeaponState.NextAmmoAt = Time.time + Mathf.Max(0.001f, profile.AmmoReloadTime);
            }

            SyncActiveComplexAmmoMirror();
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
                nextComplexAttackTime = Time.time + Mathf.Max(0.05f, profile.AttackCooldown);
                return true;
            }

            return false;
        }

        if (complexBurstRoutine != null)
            StopCoroutine(complexBurstRoutine);

        complexBurstRoutine = StartCoroutine(FireComplexBurst(profile, direction, targetPoint));
        nextComplexAttackTime = Time.time + Mathf.Max(0.05f, profile.AttackCooldown);
        return true;
    }

    public void SwitchComplexWeapon()
    {
        if (!photonView.IsMine || !IsComplexShootingActive || complexWeaponStates.Count <= 1)
            return;

        activeComplexWeaponIndex = (activeComplexWeaponIndex + 1) % complexWeaponStates.Count;
        ResetComplexPressState();
        HideAimMarker();
        SyncActiveComplexAmmoMirror();
    }

    IEnumerator FireComplexBurst(WeaponAttackProfile profile, Vector2 direction, Vector2 targetPoint)
    {
        if (profile == null)
            yield break;

        if (profile.StartDelay > 0f)
            yield return new WaitForSeconds(profile.StartDelay);

        int ownerId = photonView != null ? photonView.ViewID : 0;
        int count = Mathf.Max(1, profile.ProjectileCount);
        for (int i = 0; i < count; i++)
        {
            Vector2 shotDirection = ResolveComplexProjectileDirection(direction, profile.SpreadAngle, i, count);
            Vector2 projectileTargetPoint = ResolveComplexProjectileTargetPoint(profile, targetPoint, direction, i, count);
            Vector3 spawnPos = transform.position + (Vector3)(shotDirection.normalized * Mathf.Max(0.05f, muzzleOffsetDistance));
            bool spawned = SpawnComplexBullet(profile, shotDirection.normalized, spawnPos, ownerId, projectileTargetPoint);
            if (spawned)
                photonView.RPC(nameof(PlayShotSfx), RpcTarget.All, profile.ShotSoundId ?? string.Empty);

            if (i < count - 1 && profile.ProjectileInterval > 0f)
                yield return new WaitForSeconds(profile.ProjectileInterval);
        }
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

    Vector2 ResolveComplexAutoAimDirection(WeaponAttackProfile profile)
    {
        float range = GetComplexRangeWorld(profile);
        PlayerHealth[] targets = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        Transform bestTarget = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < targets.Length; i++)
        {
            PlayerHealth target = targets[i];
            if (target == null || target.IsWreck || target.photonView == null || target.photonView.ViewID == photonView.ViewID)
                continue;

            if (target.GetComponent<LureBeaconDecoy>() != null)
                continue;

            HideInNebulaTarget nebulaState = target.GetComponent<HideInNebulaTarget>();
            if (nebulaState != null && nebulaState.IsHiddenFromLocalPlayer())
                continue;

            Vector2 targetPosition = target.transform.position;
            float distance = Vector2.Distance(transform.position, targetPosition);
            if (distance > range || distance >= bestDistance)
                continue;

            if (!IsArcWeaponProfile(profile) &&
                IsLineBlockedByObstacle(transform.position, targetPosition, target.transform))
                continue;

            bestDistance = distance;
            bestTarget = target.transform;
        }

        if (bestTarget != null)
            return ResolveSafeAimDirection(bestTarget.position - transform.position);

        return transform.up;
    }

    Vector2 ResolveComplexAutoAimTargetPoint(WeaponAttackProfile profile)
    {
        float range = GetComplexRangeWorld(profile);
        Vector2 direction = ResolveComplexAutoAimDirection(profile);
        PlayerHealth[] targets = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        Transform bestTarget = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < targets.Length; i++)
        {
            PlayerHealth target = targets[i];
            if (target == null || target.IsWreck || target.photonView == null || target.photonView.ViewID == photonView.ViewID)
                continue;

            if (target.GetComponent<LureBeaconDecoy>() != null)
                continue;

            HideInNebulaTarget nebulaState = target.GetComponent<HideInNebulaTarget>();
            if (nebulaState != null && nebulaState.IsHiddenFromLocalPlayer())
                continue;

            Vector2 targetPosition = target.transform.position;
            float distance = Vector2.Distance(transform.position, targetPosition);
            if (distance > range || distance >= bestDistance)
                continue;

            if (!IsArcWeaponProfile(profile) &&
                IsLineBlockedByObstacle(transform.position, targetPosition, target.transform))
                continue;

            bestDistance = distance;
            bestTarget = target.transform;
        }

        if (bestTarget != null)
            return bestTarget.position;

        return (Vector2)transform.position + (direction * range);
    }

    bool IsLineBlockedByObstacle(Vector2 start, Vector2 end, Transform target)
    {
        RaycastHit2D[] hits = Physics2D.LinecastAll(start, end);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
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

    void ShowAimMarker(WeaponAttackProfile profile, Vector2 direction, Vector2? explicitTargetPoint = null)
    {
        if (profile == null)
            return;

        if (aimMarker == null)
            aimMarker = AimMarkerVfx.EnsureFor(gameObject);

        if (aimMarker == null)
            return;

        Vector2 resolvedDirection = ResolveSafeAimDirection(direction);
        float range = GetComplexRangeWorld(profile);
        if (IsArcWeaponProfile(profile))
        {
            Vector3 landingPoint = explicitTargetPoint.HasValue
                ? (Vector3)explicitTargetPoint.Value
                : transform.position + (Vector3)(resolvedDirection * range);
            aimMarker.ShowArc(transform.position, landingPoint, profile.MarkerColor, ResolveArcHeight(range));
            return;
        }

        aimMarker.ShowLine(transform.position, resolvedDirection, range, profile.MarkerColor);
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

    bool SpawnComplexBullet(WeaponAttackProfile profile, Vector2 direction, Vector3 spawnPos, int ownerId, Vector2 explicitTargetPoint)
    {
        if (bulletPrefab == null || profile == null)
            return false;

        float clampedFlightTime = Mathf.Clamp(profile.FlightTime, 0.2f, 30f);
        float range = GetComplexRangeWorld(profile);
        bool isArcProjectile = IsArcWeaponProfile(profile);
        Vector2 targetPoint = isArcProjectile
            ? explicitTargetPoint
            : (Vector2)spawnPos + (direction.normalized * range);
        float arcHeight = ResolveArcHeight(range);

        GameObject bullet = PhotonNetwork.Instantiate(
            bulletPrefab.name,
            spawnPos,
            Quaternion.identity,
            0,
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
                arcHeight
            }
        );

        if (bullet == null)
            return false;

        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent != null)
            bulletComponent.ownerViewID = ownerId;

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null && !isArcProjectile)
            rb.linearVelocity = direction.normalized * profile.ProjectileSpeed;

        Collider2D playerCollider = GetComponent<Collider2D>();
        Collider2D bulletCollider = bullet.GetComponent<Collider2D>();
        if (bulletCollider != null && playerCollider != null)
            Physics2D.IgnoreCollision(bulletCollider, playerCollider);

        return true;
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
        RaycastHit2D[] hits = Physics2D.CircleCastAll(start, beamRadius, safeDirection, Mathf.Max(0.2f, range));
        float clippedRange = AstroCutterBeamBlocker.ResolveClippedRange(hits, transform, ownerId, range);
        HashSet<int> damagedViews = new HashSet<int>();
        HashSet<string> damagedObstacles = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (hit == null)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            if (hits[i].distance > clippedRange + 0.035f)
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable.photonView != null)
            {
                if (damagedViews.Add(deployable.photonView.ViewID))
                {
                    Vector2 point = hits[i].point.sqrMagnitude > 0.001f ? hits[i].point : (Vector2)deployable.transform.position;
                    deployable.photonView.RPC(nameof(PlayerDeployableBase.TakeDeployableDamageAt), RpcTarget.MasterClient, profile.ShieldDamage, profile.HpDamage, ownerId, point.x, point.y);
                    AddSuperChargeForDamage();
                }

                continue;
            }

            PlayerHealth hp = hit.GetComponentInParent<PlayerHealth>();
            if (hp != null && hp.photonView != null && hp.GetComponent<LureBeaconDecoy>() == null && !hp.IsWreck && hp.photonView.ViewID != ownerId)
            {
                if (damagedViews.Add(hp.photonView.ViewID))
                {
                    Vector2 point = hits[i].point.sqrMagnitude > 0.001f ? hits[i].point : (Vector2)hp.transform.position;
                    hp.photonView.RPC(nameof(PlayerHealth.TakeDamageProfileAt), RpcTarget.MasterClient, profile.ShieldDamage, profile.HpDamage, ownerId, point.x, point.y);
                    AddSuperChargeForDamage();
                }

                continue;
            }

            ObstacleChunk obstacleChunk = hit.GetComponentInParent<ObstacleChunk>();
            if (obstacleChunk != null &&
                !string.IsNullOrWhiteSpace(obstacleChunk.StableId) &&
                RoomSettings.AreObstaclesDestructible() &&
                damagedObstacles.Add(obstacleChunk.StableId))
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
        if (!IsComplexShootingActive || !RoomSettings.IsSuperAttackEnabled())
            return;

        superCharge = Mathf.Clamp01(superCharge + (SuperChargeOnComplexHit * GetSuperChargeDamageGainMultiplier()));
    }

    float GetSuperChargeGainMultiplier()
    {
        WeaponAttackProfile profile = activeComplexWeaponProfile ?? GetActiveComplexWeaponState()?.Profile;
        return IsAstroCutterProfile(profile) ? 0.5f : 1f;
    }

    float GetSuperChargeDamageGainMultiplier()
    {
        WeaponAttackProfile profile = activeComplexWeaponProfile ?? GetActiveComplexWeaponState()?.Profile;
        return IsAstroCutterProfile(profile) ? 0.25f : 1f;
    }

    Vector2 ResolveManualAimDirection()
    {
        Vector2 rawDirection = shootJoystick != null ? shootJoystick.inputVector : Vector2.zero;
        if (rawDirection.magnitude >= ManualAimThreshold)
            return rawDirection.normalized;

        return FindAutoAimDirection();
    }

    Vector2 FindAutoAimDirection()
    {
        PlayerHealth[] targets = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        Transform bestTarget = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < targets.Length; i++)
        {
            PlayerHealth target = targets[i];
            if (target == null || target.IsWreck || target.photonView == null || target.photonView.ViewID == photonView.ViewID)
                continue;

            if (target.GetComponent<LureBeaconDecoy>() != null)
                continue;

            HideInNebulaTarget nebulaState = target.GetComponent<HideInNebulaTarget>();
            if (nebulaState != null && nebulaState.IsHiddenFromLocalPlayer())
                continue;

            float distance = Vector2.Distance(transform.position, target.transform.position);
            if (distance > AutoAimRange || distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestTarget = target.transform;
        }

        if (bestTarget != null)
        {
            Vector2 aim = (bestTarget.position - transform.position);
            if (aim.sqrMagnitude > 0.001f)
                return aim.normalized;
        }

        return transform.up;
    }

    void Shoot(Vector2 direction)
    {
        if (bulletPrefab == null)
        {
            Debug.LogError("bulletPrefab NULL");
            return;
        }

        PhotonView playerView = GetComponent<PhotonView>();
        int ownerId = playerView != null ? playerView.ViewID : 0;
        bool spawned = false;
        WeaponAttackProfile profile = activeSimpleWeaponProfile;

        if (IsAstroCutterProfile(profile))
        {
            TryStartAstroCutterBeam(profile, direction.normalized, ownerId);
            return;
        }

        if (ShouldUseArcSimpleShot(profile))
        {
            Vector3 spawnPos = transform.position + (transform.up * muzzleOffsetDistance);
            Vector2 targetPoint = (Vector2)transform.position + (direction.normalized * GetComplexRangeWorld(profile));
            spawned |= SpawnComplexBullet(profile, direction.normalized, spawnPos, ownerId, targetPoint);
        }
        else if (ShouldUseSimpleSpreadShot(profile))
        {
            Vector3 spawnPos = transform.position + (transform.up * muzzleOffsetDistance);
            int count = Mathf.Max(1, profile.ProjectileCount);
            for (int i = 0; i < count; i++)
            {
                Vector2 shotDirection = ResolveComplexProjectileDirection(direction.normalized, profile.SpreadAngle, i, count);
                spawned |= SpawnBullet(shotDirection, spawnPos, ownerId);
            }
        }
        else if (ShouldFireFromDualWingMuzzles())
        {
            spawned |= SpawnBullet(direction, GetWingMuzzlePosition(-1f), ownerId);
            spawned |= SpawnBullet(direction, GetWingMuzzlePosition(1f), ownerId);
        }
        else
        {
            Vector3 spawnPos = transform.position + (transform.up * muzzleOffsetDistance);
            spawned |= SpawnBullet(direction, spawnPos, ownerId);
        }

        if (spawned)
            photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);
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

    public bool TryFireBot(Vector2 direction)
    {
        if (GetComponent<EnemyBot>() == null)
            return false;

        if (!photonView.IsMine || !IsGameStarted())
            return false;

        UpdateReload();

        if (Time.time < nextFireTime || direction.sqrMagnitude < 0.04f)
            return false;

        if (!infiniteAmmo && (isReloading || currentAmmo <= 0))
            return false;

        Shoot(direction.normalized);
        if (!infiniteAmmo)
            ConsumeAmmo();
        nextFireTime = Time.time + fireRate;
        return true;
    }

    public bool TryFireBotFromWorld(Vector2 direction, Vector3 spawnPosition, float cooldownOffset = 0f)
    {
        if (GetComponent<EnemyBot>() == null)
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
        nextFireTime = Time.time + fireRate + Mathf.Max(0f, cooldownOffset);
        return true;
    }

    public bool TryFireBotVolleyFromWorld(Vector2 direction, Vector3[] spawnPositions, float cooldownOffset = 0f)
    {
        if (GetComponent<EnemyBot>() == null)
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
        nextFireTime = Time.time + fireRate + Mathf.Max(0f, cooldownOffset);
        return true;
    }

    public bool FireBotProjectileFromWorld(Vector2 direction, Vector3 spawnPosition)
    {
        if (GetComponent<EnemyBot>() == null)
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
        reloadFinishTime = Time.time + reloadDuration;

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
        if (customAmmoProfileActive && GetComponent<EnemyBot>() != null)
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
        if (baseWeaponProfileCaptured || GetComponent<EnemyBot>() != null)
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
        if (!photonView.IsMine || GetComponent<EnemyBot>() != null)
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
        if (!photonView.IsMine || GetComponent<EnemyBot>() != null)
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

        Dictionary<string, int> previousCharges = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, GadgetRuntimeState> pair in gadgetStates)
        {
            if (pair.Value == null || string.IsNullOrWhiteSpace(pair.Key))
                continue;

            previousCharges[pair.Key] = pair.Value.RemainingCharges;
        }

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
                : previousCharges.TryGetValue(itemId, out int previousRemaining)
                    ? Mathf.Clamp(previousRemaining, 0, maxCharges)
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
                state.RemainingCharges = Mathf.Clamp(state.RemainingCharges, 0, Mathf.Max(0, state.MaxCharges));
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

        for (int i = 6; i <= 7; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
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

    bool SpawnBullet(Vector2 direction, Vector3 spawnPos, int ownerId)
    {
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
                simpleFlightTime
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
                bulletRangeMultiplier
            };

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
            rb.linearVelocity = direction * bulletSpeed;
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
               RoomSettings.IsAdvancedShootingJoystickEnabled() &&
               !AreShipControlsBlocked();
    }

    public bool IsAdvancedShootJoystickEnabled()
    {
        return RoomSettings.IsAdvancedShootingJoystickEnabled() && IsComplexShootingActive;
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
        return TryFireComplexAttack(profile, ResolveComplexAutoAimDirection(profile), ResolveComplexAutoAimTargetPoint(profile), true, false);
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
        if (customAmmoProfileActive && GetComponent<EnemyBot>() != null)
            return maxAmmo;

        int configuredAmmo = RoomSettings.GetAmmoCount();
        if (ShouldApplyNovaNoEquipmentAmmoBonus())
            configuredAmmo *= 2;

        return configuredAmmo;
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

    int GetPilotAdjustedWeaponMaxAmmo(WeaponAttackProfile profile)
    {
        int configuredAmmo = Mathf.Max(1, profile != null ? profile.MaxAmmo : RoomSettings.GetAmmoCount());
        if (ShouldApplyNovaNoEquipmentAmmoBonus())
            configuredAmmo *= 2;

        return configuredAmmo;
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

        if (PhotonNetwork.IsMasterClient)
        {
            StopAuthoritativeTractorBeam(true);
        }
        else if (photonView != null && photonView.IsMine)
        {
            photonView.RPC(nameof(RequestStopTractorBeam), RpcTarget.MasterClient, InventoryItemCatalog.TractorBeamId);
        }
    }

    public bool IsHoldGadget(string itemId)
    {
        return string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal);
    }

    public void ConfigureWeaponProfile(float configuredFireRate, int configuredMaxAmmo, float configuredReloadDuration, int configuredBulletDamage, float configuredBulletScaleMultiplier, Color configuredBulletColor, float configuredMuzzleOffsetDistance, bool configuredInfiniteAmmo, float configuredBulletSpeed = -1f, string configuredShotSoundId = "", float configuredRangeMultiplier = -1f, string configuredHitEffectId = "", float configuredFlightTime = 10f)
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
        Collider2D[] hits = Physics2D.OverlapCircleAll(candidate, 0.38f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
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

        return true;
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

        if (string.Equals(itemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
            return new Color(0.16f, 0.46f, 0.78f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.MagneticBeamId, StringComparison.Ordinal))
            return new Color(0.08f, 0.36f, 0.86f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
            return new Color(0.72f, 0.5f, 0.08f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.LureBeaconId, StringComparison.Ordinal))
            return new Color(0.68f, 0.2f, 0.72f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal))
            return new Color(0.72f, 0.22f, 0.18f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.GuidanceSystemId, StringComparison.Ordinal))
            return new Color(0.18f, 0.62f, 0.58f, 0.96f);

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

        return 0f;
    }

    int ResolveGadgetMaxCharges(string gadgetItemId, int equippedCount)
    {
        equippedCount = Mathf.Max(0, equippedCount);
        if (equippedCount <= 0)
            return 0;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return GadgetMineDefaultCharges * equippedCount;

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

        if (string.Equals(gadgetItemId, InventoryItemCatalog.LureBeaconId, StringComparison.Ordinal))
        {
            int charges = LureBeaconDefaultCharges * equippedCount;
            if (ShouldApplyRoburLureBeaconBonus())
                charges *= 2;

            return charges;
        }

        if (string.Equals(gadgetItemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal))
            return AutoTurretDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.GuidanceSystemId, StringComparison.Ordinal))
            return GuidanceSystemDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.SpaceDrillId, StringComparison.Ordinal))
            return SpaceDrillDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.SpaceTrapId, StringComparison.Ordinal))
            return SpaceTrapDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
            return SuperBoosterDefaultCharges * equippedCount;

        return 0;
    }

    bool ShouldApplyRoburLureBeaconBonus()
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

        if (!TryExecuteAuthoritativeGadgetUse(itemId))
            return;

        SetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, itemId, remainingCharges - 1, maxCharges);
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

        if (string.Equals(itemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
            return TryActivateBatteryCharge();

        if (string.Equals(itemId, InventoryItemCatalog.MagneticBeamId, StringComparison.Ordinal))
            return TryActivateMagneticBeam();

        if (string.Equals(itemId, InventoryItemCatalog.LureBeaconId, StringComparison.Ordinal))
            return TryDeployLureBeacon();

        if (string.Equals(itemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal))
        {
            bool deployed = NewItemsRuntime.TryDeployAutoTurret(this);
            if (deployed)
                RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);

            return deployed;
        }

        if (string.Equals(itemId, InventoryItemCatalog.GuidanceSystemId, StringComparison.Ordinal))
        {
            photonView.RPC(nameof(ActivateGuidanceSystemRpc), photonView.Owner, GuidanceSystemDuration);
            RoundXpTracker.RecordGadgetSuccess(photonView.Owner, itemId);
            return true;
        }

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
            photonView.RPC(nameof(ActivateSuperBoosterRpc), photonView.Owner, SuperBoosterDuration);
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
    void ActivateSuperBoosterRpc(float duration)
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.ActivateSuperBooster(duration);
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
        Collider2D[] hits = Physics2D.OverlapCircleAll(candidate, LureBeaconSpawnClearanceRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
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
            movingObject.ApplyMagneticPull(sourcePosition, TractorBeamPullStrength, deltaTime);
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

        float pullAcceleration = TractorBeamPullStrength * Mathf.Lerp(0.65f, 1.25f, Mathf.Clamp01(distance / TractorBeamRadius));
        targetBody.linearVelocity += toSource.normalized * pullAcceleration * deltaTime;
        float maxSpeed = 6.4f;
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
public class ReloadButtonUI : MonoBehaviourPun
{
    const string ReloadButtonName = "ReloadButton";

    PlayerShooting shooting;
    GameObject buttonObject;
    Button reloadButton;
    Image backgroundImage;
    TextMeshProUGUI buttonText;

    void Start()
    {
        shooting = GetComponent<PlayerShooting>();

        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        CreateButton();
        RefreshState();
    }

    void Update()
    {
        EnsureButton();
        RefreshState();
    }

    void OnDestroy()
    {
        if (buttonObject != null)
        {
            Destroy(buttonObject);
        }
    }

    void CreateButton()
    {
        GameObject existing = GameObject.Find(ReloadButtonName);
        if (existing != null)
        {
            Destroy(existing);
        }

        GameObject canvas = GameObject.Find("Canvas");
        GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
        if (canvas == null || shootJoystickObject == null)
            return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        RectTransform joystickRect = shootJoystickObject.GetComponent<RectTransform>();
        if (canvasRect == null || joystickRect == null)
            return;

        buttonObject = new GameObject(ReloadButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = joystickRect.anchorMin;
        rect.anchorMax = joystickRect.anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = joystickRect.anchoredPosition + new Vector2(0f, 216f);
        rect.sizeDelta = new Vector2(176f, 62f);

        backgroundImage = buttonObject.GetComponent<Image>();
        backgroundImage.color = new Color(0.23f, 0.56f, 0.9f, 0.96f);
        backgroundImage.type = Image.Type.Sliced;

        reloadButton = buttonObject.GetComponent<Button>();
        reloadButton.transition = Selectable.Transition.ColorTint;
        reloadButton.targetGraphic = backgroundImage;
        reloadButton.onClick.AddListener(HandleReloadClicked);

        GameObject textObject = new GameObject("ReloadButtonText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        buttonText = textObject.GetComponent<TextMeshProUGUI>();
        buttonText.text = "RELOAD";
        buttonText.fontSize = 26f;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.textWrappingMode = TextWrappingModes.NoWrap;
        buttonText.margin = new Vector4(12f, 6f, 12f, 6f);
        buttonText.color = Color.white;

        TMP_Text referenceText = FindAnyObjectByType<TMP_Text>();
        if (referenceText != null)
        {
            buttonText.font = referenceText.font;
            buttonText.fontSharedMaterial = referenceText.fontSharedMaterial;
        }
    }

    void EnsureButton()
    {
        if (!photonView.IsMine)
            return;

        if (buttonObject != null && reloadButton != null && backgroundImage != null && buttonText != null)
            return;

        CreateButton();
    }

    void RefreshState()
    {
        if (shooting == null || reloadButton == null || backgroundImage == null || buttonText == null)
            return;

        bool visible = !shooting.IsComplexShootingActive;
        if (buttonObject != null && buttonObject.activeSelf != visible)
            buttonObject.SetActive(visible);

        if (!visible)
            return;

        bool canReload = shooting.CanManualReload;
        reloadButton.interactable = canReload;
        backgroundImage.color = canReload
            ? new Color(0.23f, 0.56f, 0.9f, 0.96f)
            : new Color(0.14f, 0.18f, 0.24f, 0.78f);
        buttonText.color = canReload
            ? Color.white
            : new Color(0.82f, 0.86f, 0.91f, 0.82f);
    }

    void HandleReloadClicked()
    {
        if (shooting == null)
            return;

        shooting.TriggerManualReload();
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
    static Sprite circularButtonSprite;

    PlayerShooting shooting;
    GameObject rootObject;
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

        RectTransform joystickRect = shootJoystickObject.GetComponent<RectTransform>();
        if (joystickRect == null)
            return;

        GameObject existingRoot = GameObject.Find(GadgetButtonRootName);
        if (existingRoot != null)
            Destroy(existingRoot);

        rootObject = new GameObject(GadgetButtonRootName, typeof(RectTransform));
        rootObject.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = joystickRect.anchorMin;
        rootRect.anchorMax = joystickRect.anchorMax;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = joystickRect.anchoredPosition;
        rootRect.sizeDelta = Vector2.zero;

        for (int i = 0; i < itemIds.Count; i++)
        {
            GadgetButtonWidget widget = CreateWidget(itemIds[i], i);
            if (widget != null)
                widgets.Add(widget);
        }
    }

    GadgetButtonWidget CreateWidget(string itemId, int index)
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
        rect.anchoredPosition = new Vector2(0f, 392f + (index * 126f));
        rect.sizeDelta = new Vector2(112f, 112f);

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
            rootObject.SetActive(widgets.Count > 0);

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
