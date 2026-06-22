using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PlayerHealth : MonoBehaviourPun
{
    const int DefaultPlayerHp = 50;
    const int DefaultPlayerShield = 50;
    const int BatteryShieldPerTick = 5;
    const int BatteryTickCount = 5;
    const float BatteryTickInterval = 1f;
    const float RegenerativeShieldDelay = 5f;
    const float RegenerativeShieldPerSecond = 2f;
    public const float EvacuationAnimationDurationSeconds = 4f;
    const float EvacuationAnimationDuration = EvacuationAnimationDurationSeconds;
    const float MinimumEvacuationScale = 0.01f;
    const float AstronautSpawnClearanceRadius = 0.34f;
    const float AstronautToxicBorderSpawnMargin = 1.35f;
    const float SpawnInvulnerabilityDuration = 5f;
    const float PhaseShieldInvulnerabilityDuration = 6f;
    const float PhaseShieldHpThresholdRatio = 0.35f;
    const float KineticDampenerDamageMultiplier = 0.5f;
    const float KineticDampenerExplosiveDamageMultiplier = 0.75f;
    const float StrongPlatingEnvironmentalDamageMultiplier = 0.5f;
    const float BulwarkProjectorLaserDamageMultiplier = 0.75f;
    const float AlienAegisBarrierDamageMultiplier = 0.5f;
    const float AlienAegisBarrierDuration = 3f;
    const float RadioactiveCargoDamageInterval = 1f;
    const float SpaceAnimalBonesDropChance = 0.15f;
    const string EnvironmentalDamageSource = "environmental";
    const string NebulaDamageSource = "nebula";
    public const string PilotDamageSourceRamming = "ramming";
    public const string DamageSourceExplosive = "explosive";
    public const string DamageSourceLaser = "laser";
    public const string DamageSourceRadioactiveCargo = "radioactive_cargo";
    public const string DamageSourceBioTrap = "bio_trap";

    public int maxHP = 50;
    public int maxShield = 50;

    int currentHP;
    int currentShield;
    Slider hpBar;
    bool isEvacuationAnimating;
    bool destroyRequested;
    bool deathHandled;
    bool astronautSpawnedAfterDestruction;
    bool spawnInvulnerabilityScheduled;
    bool hasPendingEvacuationSummary;
    bool jakeEmergencyRegenerationUsed;
    Coroutine jakeEmergencyRegenerationRoutine;
    int pendingEvacuationFinalScore;
    string pendingEvacuationOutcome = "extracted";
    float spawnInvulnerableUntil = -1f;
    float equipmentInvulnerableUntil = -1f;
    float alienAegisBarrierUntil = -1f;
    float lastDamageTakenTime = -999f;
    float shieldRegenAccumulator;
    float nextRadioactiveCargoDamageTime;
    bool phaseShieldTriggered;

    public int CurrentHP => currentHP;
    public int CurrentShield => currentShield;
    public int MaxShield => maxShield;
    public int RepairableMaxShield => GetRepairableShieldCap();
    public WeaponHitContext LastDamageContext { get; private set; }
    public float LastDamageShieldMultiplier { get; private set; } = 1f;
    public float LastDamageHpMultiplier { get; private set; } = 1f;
    public string LastDamageDebugSummary => WeaponDamageInteractionCatalog.BuildDebugSummary(LastDamageContext, LastDamageShieldMultiplier, LastDamageHpMultiplier);
    public bool HasFullVitals => currentHP >= maxHP && currentShield >= RepairableMaxShield;
    public bool HasBrokenShield => maxShield > 0 && currentShield <= 0;
    public bool IsWreck { get; private set; }
    public ActorIdentity Identity => ActorIdentity.Ensure(gameObject);
    public bool IsBotControlled => GetComponent<EnemyBot>() != null;
    public bool IsNeutralRiderControlled => NeutralRiderController.IsNeutralRider(gameObject) ||
                                           NeutralRiderController.IsNeutralRiderInstantiationData(photonView != null ? photonView.InstantiationData : null);
    public bool IsAstronautControlled => ActorIdentity.IsAstronautActor(gameObject);
    public bool IsEnemyAstronautControlled => ActorIdentity.IsEnemyAstronautActor(gameObject);
    public bool IsHumanShipControlled => !IsBotControlled && !IsNeutralRiderControlled && !IsAstronautControlled;
    public bool IsEvacuationAnimating => isEvacuationAnimating;
    public bool IsSpawnInvulnerable => !IsWreck &&
                                       !IsBotControlled &&
                                       !IsNeutralRiderControlled &&
                                       ((!IsAstronautControlled && Time.time < spawnInvulnerableUntil) ||
                                        Time.time < equipmentInvulnerableUntil);

    public bool CanActivateBatteryChargeLocally()
    {
        return !IsWreck && !isEvacuationAnimating && currentShield < GetRepairableShieldCap();
    }

    public void RequestBatteryShieldCharge()
    {
        if (!photonView.IsMine || !CanActivateBatteryChargeLocally())
            return;

        photonView.RPC(nameof(HandleBatteryShieldChargeRequest), RpcTarget.MasterClient);
    }

    public bool TryBeginBatteryShieldChargeAuthority()
    {
        if (!PhotonNetwork.IsMasterClient || IsWreck || isEvacuationAnimating || currentShield >= GetRepairableShieldCap())
            return false;

        RoundXpTracker.RecordBatterySuccess(photonView.Owner, GetRepairableShieldCap() - currentShield);
        photonView.RPC(nameof(PlayBatteryShieldChargeAudio), RpcTarget.All);
        StartCoroutine(ApplyBatteryShieldChargeRoutine());
        return true;
    }

    void Start()
    {
        if (ViperRecoveryPlotController.TryEnsureViperWreckRuntime(gameObject))
        {
            enabled = false;
            return;
        }

        ActorIdentity.Ensure(gameObject);

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

        if (IsHumanShipControlled)
        {
            int shipSkinIndex = RoomSettings.GetPlayerShipSkin(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, 0);
            maxHP = ShipCatalog.GetBaseHp(shipSkinIndex) + GetEquippedHpCapacityBonus(shipSkinIndex);
            maxShield = GetConfiguredMaxShield(shipSkinIndex);
        }

        currentHP = maxHP;
        currentShield = maxShield;
        lastDamageTakenTime = Time.time;

        if (IsHumanShipControlled && GetComponent<PlayerRepairDocking>() == null)
        {
            gameObject.AddComponent<PlayerRepairDocking>();
        }

        if (IsHumanShipControlled && GetComponent<PilotActiveAbilityController>() == null)
        {
            gameObject.AddComponent<PilotActiveAbilityController>();
        }

        if (IsHumanShipControlled && GetComponent<ShipDamageState>() == null)
        {
            gameObject.AddComponent<ShipDamageState>();
        }

        if (IsHumanShipControlled)
        {
            LowHpHullSparksVfx.AttachIfNeeded(gameObject);
        }

        if (IsHumanShipControlled)
        {
            BeginSpawnInvulnerability(SpawnInvulnerabilityDuration);
            StartCoroutine(InitialSpawnInvulnerabilityRoutine());
        }

        if (IsHumanShipControlled && GetComponent<RoundChatCommandUI>() == null)
        {
            gameObject.AddComponent<RoundChatCommandUI>();
        }

        if (photonView.IsMine && !IsBotControlled && !IsNeutralRiderControlled && !IsEnemyAstronautControlled)
        {
            PhotonNetwork.LocalPlayer.TagObject = gameObject;

            if (GetComponent<RoundVitalsIconHudUI>() == null)
            {
                gameObject.AddComponent<RoundVitalsIconHudUI>();
            }

            if (GetComponent<RoundPilotHudUI>() == null)
            {
                gameObject.AddComponent<RoundPilotHudUI>();
            }

            if (GetComponent<ValuableCargoCarrierOverlay>() == null)
            {
                gameObject.AddComponent<ValuableCargoCarrierOverlay>();
            }

            GameObject barObj = GameObject.Find("HP_Bar");
            if (barObj != null)
            {
                hpBar = barObj.GetComponent<Slider>();
                if (hpBar != null)
                {
                    hpBar.maxValue = maxHP;
                    hpBar.value = currentHP;
                }
            }

            RoundVitalsIconHudUI.HideLegacyVitalBars();
        }

        GameVisualTheme.ApplyPlayerVisual(this);
    }

    void Update()
    {
        if (IsSpawnInvulnerable && RoomSettings.AreVisualEffectsEnabled() && GetComponent<SpawnInvulnerabilityVfx>() == null)
            SpawnInvulnerabilityVfx.Attach(this);

        if (PhotonNetwork.IsMasterClient)
        {
            RegenerateShieldFromEquipment();
            ApplyRadioactiveCargoDamage();
        }
    }

    void OnDestroy()
    {
        RuntimeSceneQueryCache.InvalidateAll();

        if (photonView != null &&
            photonView.IsMine &&
            PhotonNetwork.LocalPlayer != null &&
            ReferenceEquals(PhotonNetwork.LocalPlayer.TagObject, gameObject))
        {
            PhotonNetwork.LocalPlayer.TagObject = null;
        }
    }

}
