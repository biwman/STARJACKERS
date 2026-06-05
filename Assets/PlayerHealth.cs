using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviourPun
{
    const float DeathMessageDuration = 10f;
    const string BoosterBarObjectName = "Booster_Bar";
    const float DeathMessageBoosterGap = 8f;
    const float DeathMessageHeight = 44f;
    const float DeathMessageExtraWidth = 180f;
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
    const string EnvironmentalDamageSource = "environmental";
    const string NebulaDamageSource = "nebula";
    public const string PilotDamageSourceRamming = "ramming";
    public const string DamageSourceExplosive = "explosive";
    public const string DamageSourceLaser = "laser";
    public const string DamageSourceRadioactiveCargo = "radioactive_cargo";

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
            maxHP = ShipCatalog.GetBaseHp(shipSkinIndex);
            maxShield = ShipCatalog.GetBaseShield(shipSkinIndex) + GetEquippedShieldCapacityBonus(shipSkinIndex);
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

            if (GetComponent<HealthBarUI>() == null)
            {
                gameObject.AddComponent<HealthBarUI>();
            }

            if (GetComponent<ShieldBarUI>() == null)
            {
                gameObject.AddComponent<ShieldBarUI>();
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

    void ApplyRadioactiveCargoDamage()
    {
        if (!IsHumanShipControlled || IsWreck || isEvacuationAnimating || currentHP <= 0)
            return;

        if (Time.time < nextRadioactiveCargoDamageTime)
            return;

        nextRadioactiveCargoDamageTime = Time.time + RadioactiveCargoDamageInterval;

        int radioactiveCount = CountRadioactiveCargoItems();
        if (radioactiveCount <= 0)
            return;

        int shieldDamage = radioactiveCount * InventoryItemCatalog.RadioactiveTreasureShieldDamagePerSecond;
        int hpDamage = radioactiveCount * InventoryItemCatalog.RadioactiveTreasureHpDamagePerSecond;
        ApplyDamageProfileInternal(
            shieldDamage,
            hpDamage,
            -1,
            false,
            transform.position.x,
            transform.position.y,
            new WeaponHitContext(
                WeaponDamageType.Environmental,
                WeaponDeliveryMethod.AreaPulse,
                WeaponDeliveryFlags.Continuous,
                DamageSourceRadioactiveCargo));
    }

    int CountRadioactiveCargoItems()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        string[] shipSlots = PlayerProfileService.GetPlayerShipInventorySlots(owner);
        if (shipSlots == null)
            return 0;

        int count = 0;
        for (int i = 0; i < shipSlots.Length; i++)
        {
            if (InventoryItemCatalog.IsRadioactiveTreasure(shipSlots[i]))
                count++;
        }

        return count;
    }

    void RegenerateShieldFromEquipment()
    {
        if (!IsHumanShipControlled || IsWreck || isEvacuationAnimating || maxShield <= 0)
            return;

        if (!HasEquippedItem(InventoryItemCatalog.RegenerativeShieldMatrixId))
            return;

        int shieldCap = GetRepairableShieldCap();
        if (shieldCap <= 0 || currentShield >= shieldCap)
        {
            shieldRegenAccumulator = 0f;
            return;
        }

        if (Time.time < lastDamageTakenTime + RegenerativeShieldDelay)
            return;

        shieldRegenAccumulator += RegenerativeShieldPerSecond * Time.deltaTime;
        if (shieldRegenAccumulator < 1f)
            return;

        int amount = Mathf.FloorToInt(shieldRegenAccumulator);
        shieldRegenAccumulator -= amount;
        int previousShield = currentShield;
        currentShield = Mathf.Min(shieldCap, currentShield + amount);
        if (currentShield != previousShield)
            photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);
    }

    int GetEquippedShieldCapacityBonus(int shipSkinIndex)
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        int bonus = InventoryItemCatalog.GetEquippedShieldCapacityBonus(equipmentSlots, shipSkinIndex);
        if (bonus > 0 && PilotCatalog.IsSelectedPilot(owner, PilotCatalog.AtlasId))
            bonus = Mathf.CeilToInt(bonus * 1.3f);

        return bonus;
    }

    void OnDestroy()
    {
        if (photonView != null &&
            photonView.IsMine &&
            PhotonNetwork.LocalPlayer != null &&
            ReferenceEquals(PhotonNetwork.LocalPlayer.TagObject, gameObject))
        {
            PhotonNetwork.LocalPlayer.TagObject = null;
        }
    }

    [PunRPC]
    public void TakeDamage(int dmg, int attackerViewID)
    {
        if (TryForwardDamageToDeployable(dmg, dmg, attackerViewID, transform.position.x, transform.position.y))
            return;

        ApplyDamageInternal(dmg, attackerViewID, true, false, 0f, 0f);
    }

    [PunRPC]
    public void TakeDamageWithContext(
        int dmg,
        int attackerViewID,
        int damageType,
        int deliveryMethod,
        int deliveryFlags,
        string damageSource)
    {
        WeaponHitContext hitContext = WeaponHitContext.FromRpc(damageType, deliveryMethod, deliveryFlags, damageSource);
        if (TryForwardDamageToDeployable(dmg, dmg, attackerViewID, transform.position.x, transform.position.y, hitContext))
            return;

        ApplyDamageInternal(dmg, attackerViewID, true, false, 0f, 0f, hitContext);
    }

    [PunRPC]
    public void TakeDamageAt(int dmg, int attackerViewID, float impactX, float impactY)
    {
        if (TryForwardDamageToDeployable(dmg, dmg, attackerViewID, impactX, impactY))
            return;

        ApplyDamageInternal(dmg, attackerViewID, true, true, impactX, impactY);
    }

    [PunRPC]
    public void TakeDamageWithContextAt(
        int dmg,
        int attackerViewID,
        float impactX,
        float impactY,
        int damageType,
        int deliveryMethod,
        int deliveryFlags,
        string damageSource)
    {
        WeaponHitContext hitContext = WeaponHitContext.FromRpc(damageType, deliveryMethod, deliveryFlags, damageSource);
        if (TryForwardDamageToDeployable(dmg, dmg, attackerViewID, impactX, impactY, hitContext))
            return;

        ApplyDamageInternal(dmg, attackerViewID, true, true, impactX, impactY, hitContext);
    }

    [PunRPC]
    public void TakeDamageProfileAt(int shieldDmg, int hpDmg, int attackerViewID, float impactX, float impactY)
    {
        if (TryForwardDamageToDeployable(shieldDmg, hpDmg, attackerViewID, impactX, impactY))
            return;

        ApplyDamageProfileInternal(shieldDmg, hpDmg, attackerViewID, true, impactX, impactY);
    }

    [PunRPC]
    public void TakeDamageProfileWithSourceAt(int shieldDmg, int hpDmg, int attackerViewID, float impactX, float impactY, string damageSource)
    {
        WeaponHitContext hitContext = WeaponHitContext.FromDamageSource(damageSource);
        if (TryForwardDamageToDeployable(shieldDmg, hpDmg, attackerViewID, impactX, impactY, hitContext))
            return;

        ApplyDamageProfileInternal(shieldDmg, hpDmg, attackerViewID, true, impactX, impactY, hitContext);
    }

    [PunRPC]
    public void TakeDamageProfileWithContextAt(
        int shieldDmg,
        int hpDmg,
        int attackerViewID,
        float impactX,
        float impactY,
        int damageType,
        int deliveryMethod,
        int deliveryFlags,
        string damageSource)
    {
        WeaponHitContext hitContext = WeaponHitContext.FromRpc(damageType, deliveryMethod, deliveryFlags, damageSource);
        if (TryForwardDamageToDeployable(shieldDmg, hpDmg, attackerViewID, impactX, impactY, hitContext))
            return;

        ApplyDamageProfileInternal(shieldDmg, hpDmg, attackerViewID, true, impactX, impactY, hitContext);
    }

    [PunRPC]
    public void TakeShieldOnlyDamageAt(int shieldDmg, int attackerViewID, float impactX, float impactY)
    {
        if (TryForwardShieldOnlyDamageToDeployable(shieldDmg, attackerViewID, impactX, impactY))
            return;

        ApplyShieldOnlyDamageInternal(shieldDmg, attackerViewID, true, impactX, impactY);
    }

    [PunRPC]
    public void TakeShieldOnlyDamageWithContextAt(
        int shieldDmg,
        int attackerViewID,
        float impactX,
        float impactY,
        int damageType,
        int deliveryMethod,
        int deliveryFlags,
        string damageSource)
    {
        WeaponHitContext hitContext = WeaponHitContext.FromRpc(damageType, deliveryMethod, deliveryFlags, damageSource);
        if (TryForwardShieldOnlyDamageToDeployable(shieldDmg, attackerViewID, impactX, impactY, hitContext))
            return;

        ApplyShieldOnlyDamageInternal(shieldDmg, attackerViewID, true, impactX, impactY, hitContext);
    }

    [PunRPC]
    public void TakePilotDamageAt(int dmg, int attackerViewID, float impactX, float impactY, string damageSource)
    {
        WeaponHitContext hitContext = WeaponHitContext.FromDamageSource(damageSource);
        if (TryForwardDamageToDeployable(dmg, dmg, attackerViewID, impactX, impactY, hitContext))
            return;

        ApplyDamageInternal(dmg, attackerViewID, true, true, impactX, impactY, hitContext);
    }

    [PunRPC]
    public void ApplyGravityTetherPullRpc(int sourceViewId, float sourceX, float sourceY, float pullAcceleration, float maxSpeed, float duration)
    {
        if (!photonView.IsMine || IsWreck || IsBotControlled || isEvacuationAnimating)
            return;

        if (GetComponent<LureBeaconDecoy>() != null || GetComponent<PlayerDeployableBase>() != null)
            return;

        GravityTetherPullEffect effect = GetComponent<GravityTetherPullEffect>();
        if (effect == null)
            effect = gameObject.AddComponent<GravityTetherPullEffect>();

        effect.Configure(sourceViewId, new Vector2(sourceX, sourceY), pullAcceleration, maxSpeed, duration);
    }

    [PunRPC]
    public void ApplyElectromagneticShockRpc(float duration, float speedMultiplier, float fireIntervalMultiplier)
    {
        if (IsWreck || CurrentHP <= 0 || isEvacuationAnimating)
            return;

        if (IsAstronautControlled || GetComponent<LureBeaconDecoy>() != null || GetComponent<PlayerDeployableBase>() != null)
            return;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot != null && !bot.CanReceivePilotHostileEffect())
            return;

        ElectromagneticShockStatus.Apply(gameObject, duration, speedMultiplier, fireIntervalMultiplier);
    }

    [PunRPC]
    public void ApplyAtlasSuppressionRpc(float duration, float speedMultiplier, float fireIntervalMultiplier, float reloadMultiplier, float projectileSpeedMultiplier)
    {
        if (IsWreck || CurrentHP <= 0 || isEvacuationAnimating)
            return;

        if (IsAstronautControlled || GetComponent<LureBeaconDecoy>() != null || GetComponent<PlayerDeployableBase>() != null)
            return;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot != null && !bot.CanReceivePilotHostileEffect())
            return;

        AtlasSuppressionStatus.Apply(gameObject, duration, speedMultiplier, fireIntervalMultiplier, reloadMultiplier, projectileSpeedMultiplier);
    }

    [PunRPC]
    public void ApplyAsteroidKnockbackRpc(float directionX, float directionY, float impulse)
    {
        if (PhotonNetwork.IsConnected && photonView != null && !photonView.IsMine)
            return;

        if (IsWreck || isEvacuationAnimating || GetComponent<LureBeaconDecoy>() != null || GetComponent<PlayerDeployableBase>() != null)
            return;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null || !body.simulated)
            return;

        Vector2 direction = new Vector2(directionX, directionY);
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.up;

        body.AddForce(direction.normalized * Mathf.Clamp(impulse, 0f, 18f), ForceMode2D.Impulse);
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

    [PunRPC]
    public void TakeEnvironmentalDamage(int dmg)
    {
        ApplyDamageInternal(
            dmg,
            -1,
            false,
            false,
            0f,
            0f,
            new WeaponHitContext(
                WeaponDamageType.Environmental,
                WeaponDeliveryMethod.AreaPulse,
                WeaponDeliveryFlags.Continuous,
                EnvironmentalDamageSource));
    }

    [PunRPC]
    public void TakeNebulaDamage(int dmg)
    {
        if (PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.SirNowitzkyId))
        {
            ApplyShieldOnlyEnvironmentalDamage(dmg, NebulaDamageSource);
            return;
        }

        ApplyDamageInternal(
            dmg,
            -1,
            false,
            false,
            0f,
            0f,
            new WeaponHitContext(
                WeaponDamageType.Environmental,
                WeaponDeliveryMethod.AreaPulse,
                WeaponDeliveryFlags.Continuous,
                NebulaDamageSource));
    }

    void ApplyDamageInternal(int dmg, int attackerViewID, bool playImpactAudio, bool hasImpactPosition, float impactX, float impactY)
    {
        ApplyDamageInternal(dmg, attackerViewID, playImpactAudio, hasImpactPosition, impactX, impactY, string.Empty);
    }

    void ApplyDamageProfileInternal(int shieldDmg, int hpDmg, int attackerViewID, bool playImpactAudio, float impactX, float impactY)
    {
        ApplyDamageProfileInternal(shieldDmg, hpDmg, attackerViewID, playImpactAudio, impactX, impactY, string.Empty);
    }

    void ApplyDamageProfileInternal(int shieldDmg, int hpDmg, int attackerViewID, bool playImpactAudio, float impactX, float impactY, string damageSource)
    {
        ApplyDamageProfileInternal(shieldDmg, hpDmg, attackerViewID, playImpactAudio, impactX, impactY, WeaponHitContext.FromDamageSource(damageSource));
    }

    void ApplyDamageProfileInternal(int shieldDmg, int hpDmg, int attackerViewID, bool playImpactAudio, float impactX, float impactY, WeaponHitContext hitContext)
    {
        if (IsDeployableDamageProxy())
            return;

        if (!PhotonNetwork.IsMasterClient || IsWreck || isEvacuationAnimating)
            return;

        if (IsSpawnInvulnerable)
            return;

        EnemyBot launchProtectedBot = GetComponent<EnemyBot>();
        if (launchProtectedBot != null && launchProtectedBot.IsPirateBaseLaunchProtected)
            return;

        PlayerRepairDocking repairDocking = GetComponent<PlayerRepairDocking>();
        if (repairDocking != null && repairDocking.IsDamageImmune)
            return;

        WeaponDamageMultipliers damageMultipliers = WeaponDamageInteractionCatalog.ResolveMultipliers(hitContext);
        RememberDamageContext(hitContext, damageMultipliers);
        string damageSource = hitContext.DamageSource ?? string.Empty;
        int previousHp = currentHP;
        int previousShield = currentShield;
        int rawShieldDamage = Mathf.Max(0, shieldDmg);
        int rawHpDamage = Mathf.Max(0, hpDmg);
        int adjustedShieldDamage = WeaponDamageInteractionCatalog.ApplyMultiplier(ApplyPilotDamageModifiers(rawShieldDamage, attackerViewID, hitContext), damageMultipliers.ShieldMultiplier);
        int adjustedHpDamage = WeaponDamageInteractionCatalog.ApplyMultiplier(ApplyPilotDamageModifiers(rawHpDamage, attackerViewID, hitContext), damageMultipliers.HpMultiplier);
        WeaponDamageInteractionCatalog.LogDamageContext(this, "profile", hitContext, damageMultipliers, attackerViewID, adjustedShieldDamage, adjustedHpDamage);
        int absorbed = 0;
        int hpDamageToApply = 0;

        if (currentShield > 0)
        {
            if (adjustedShieldDamage > 0)
            {
                absorbed = Mathf.Min(currentShield, adjustedShieldDamage);
                currentShield -= absorbed;

                float overflowRatio = Mathf.Clamp01((adjustedShieldDamage - absorbed) / (float)adjustedShieldDamage);
                hpDamageToApply = Mathf.RoundToInt(adjustedHpDamage * overflowRatio);
            }
        }
        else
        {
            hpDamageToApply = adjustedHpDamage;
        }

        if (hpDamageToApply > 0)
            currentHP = Mathf.Max(0, currentHP - hpDamageToApply);

        TrackDamageTakenAndShieldBreak(previousHp, previousShield);
        TryTriggerPhaseShield(previousHp);
        TryRollShipDamageOnHalfHpCrossing(previousHp);

        int hpDamage = Mathf.Max(0, previousHp - currentHP);
        Vector2 impactPosition = Vector2.zero;
        bool hasResolvedImpactPosition = false;
        if (playImpactAudio && (absorbed > 0 || hpDamage > 0))
        {
            impactPosition = new Vector2(impactX, impactY);
            hasResolvedImpactPosition = true;
        }

        if (currentHP != previousHp || currentShield != previousShield)
            photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);

        TryStartJakeEmergencyRegeneration(previousHp);

        if (playImpactAudio && absorbed > 0)
        {
            photonView.RPC(nameof(PlayShieldHitAudio), RpcTarget.All);
            photonView.RPC(nameof(PlayShieldHitVisual), RpcTarget.All, impactPosition.x, impactPosition.y);
        }

        if (playImpactAudio && hpDamage > 0)
        {
            photonView.RPC(nameof(PlayHpHitAudio), RpcTarget.All);
            if (hasResolvedImpactPosition)
                photonView.RPC(nameof(PlayHpHitVisual), RpcTarget.All, impactPosition.x, impactPosition.y);
        }

        if (IsBotControlled)
        {
            EnemyBot damagedBot = GetComponent<EnemyBot>();
            if (damagedBot != null)
                damagedBot.NotifyDamageTaken(previousHp, currentHP, Mathf.Max(0, previousShield - currentShield), hpDamage, attackerViewID);
        }

        if (IsNeutralRiderControlled)
        {
            NeutralRiderController rider = GetComponent<NeutralRiderController>();
            if (rider != null)
                rider.NotifyDamageTaken(attackerViewID);
        }

        RoundXpTracker.RecordDamage(attackerViewID, photonView, absorbed, hpDamage);

        if (previousHp > 0 && currentHP <= 0)
            RoundXpTracker.RecordKill(attackerViewID, this);

        if (currentHP <= 0)
            HandleDeath(attackerViewID);
    }

    void ApplyDamageInternal(int dmg, int attackerViewID, bool playImpactAudio, bool hasImpactPosition, float impactX, float impactY, string damageSource)
    {
        ApplyDamageInternal(dmg, attackerViewID, playImpactAudio, hasImpactPosition, impactX, impactY, WeaponHitContext.FromDamageSource(damageSource));
    }

    void ApplyDamageInternal(int dmg, int attackerViewID, bool playImpactAudio, bool hasImpactPosition, float impactX, float impactY, WeaponHitContext hitContext)
    {
        if (IsDeployableDamageProxy())
            return;

        if (!PhotonNetwork.IsMasterClient || IsWreck || isEvacuationAnimating)
            return;

        if (IsSpawnInvulnerable)
            return;

        EnemyBot launchProtectedBot = GetComponent<EnemyBot>();
        if (launchProtectedBot != null && launchProtectedBot.IsPirateBaseLaunchProtected)
            return;

        PlayerRepairDocking repairDocking = GetComponent<PlayerRepairDocking>();
        if (repairDocking != null && repairDocking.IsDamageImmune)
            return;

        WeaponDamageMultipliers damageMultipliers = WeaponDamageInteractionCatalog.ResolveMultipliers(hitContext);
        RememberDamageContext(hitContext, damageMultipliers);
        string damageSource = hitContext.DamageSource ?? string.Empty;
        int previousHp = currentHP;
        int previousShield = currentShield;
        int pilotAdjustedDamage = ApplyPilotDamageModifiers(Mathf.Max(0, dmg), attackerViewID, hitContext);
        int adjustedShieldDamage = WeaponDamageInteractionCatalog.ApplyMultiplier(pilotAdjustedDamage, damageMultipliers.ShieldMultiplier);
        int adjustedHpDamage = WeaponDamageInteractionCatalog.ApplyMultiplier(pilotAdjustedDamage, damageMultipliers.HpMultiplier);
        WeaponDamageInteractionCatalog.LogDamageContext(this, "simple", hitContext, damageMultipliers, attackerViewID, adjustedShieldDamage, adjustedHpDamage);
        int absorbed = 0;
        int hpDamageToApply = 0;
        if (currentShield > 0)
        {
            if (adjustedShieldDamage > 0)
            {
                absorbed = Mathf.Min(currentShield, adjustedShieldDamage);
                currentShield -= absorbed;

                float overflowRatio = Mathf.Clamp01((adjustedShieldDamage - absorbed) / (float)adjustedShieldDamage);
                hpDamageToApply = Mathf.RoundToInt(adjustedHpDamage * overflowRatio);
            }
        }
        else
        {
            hpDamageToApply = adjustedHpDamage;
        }

        if (hpDamageToApply > 0)
            currentHP = Mathf.Max(0, currentHP - hpDamageToApply);

        TrackDamageTakenAndShieldBreak(previousHp, previousShield);
        TryTriggerPhaseShield(previousHp);
        TryRollShipDamageOnHalfHpCrossing(previousHp);

        int hpDamage = Mathf.Max(0, previousHp - currentHP);
        Vector2 impactPosition = Vector2.zero;
        bool hasResolvedImpactPosition = false;
        if (playImpactAudio && (absorbed > 0 || hpDamage > 0))
        {
            impactPosition = hasImpactPosition
                ? new Vector2(impactX, impactY)
                : ResolveDamageImpactPosition(attackerViewID);
            hasResolvedImpactPosition = true;
        }

        photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);

        TryStartJakeEmergencyRegeneration(previousHp);

        if (playImpactAudio && absorbed > 0)
        {
            photonView.RPC(nameof(PlayShieldHitAudio), RpcTarget.All);

            photonView.RPC(nameof(PlayShieldHitVisual), RpcTarget.All, impactPosition.x, impactPosition.y);
        }

        if (playImpactAudio && hpDamage > 0)
        {
            photonView.RPC(nameof(PlayHpHitAudio), RpcTarget.All);
            if (hasResolvedImpactPosition)
                photonView.RPC(nameof(PlayHpHitVisual), RpcTarget.All, impactPosition.x, impactPosition.y);
        }

        if (IsBotControlled)
        {
            EnemyBot damagedBot = GetComponent<EnemyBot>();
            if (damagedBot != null)
                damagedBot.NotifyDamageTaken(previousHp, currentHP, Mathf.Max(0, previousShield - currentShield), hpDamage, attackerViewID);
        }

        if (IsNeutralRiderControlled)
        {
            NeutralRiderController rider = GetComponent<NeutralRiderController>();
            if (rider != null)
                rider.NotifyDamageTaken(attackerViewID);
        }

        RoundXpTracker.RecordDamage(attackerViewID, photonView, absorbed, hpDamage);

        if (previousHp > 0 && currentHP <= 0)
            RoundXpTracker.RecordKill(attackerViewID, this);

        if (currentHP <= 0)
        {
            HandleDeath(attackerViewID);
        }
    }

    void ApplyShieldOnlyDamageInternal(int dmg, int attackerViewID, bool playImpactAudio, float impactX, float impactY)
    {
        ApplyShieldOnlyDamageInternal(dmg, attackerViewID, playImpactAudio, impactX, impactY, WeaponHitContext.None);
    }

    void ApplyShieldOnlyDamageInternal(int dmg, int attackerViewID, bool playImpactAudio, float impactX, float impactY, WeaponHitContext hitContext)
    {
        if (IsDeployableDamageProxy())
            return;

        if (!PhotonNetwork.IsMasterClient || IsWreck || isEvacuationAnimating)
            return;

        if (IsSpawnInvulnerable)
            return;

        EnemyBot launchProtectedBot = GetComponent<EnemyBot>();
        if (launchProtectedBot != null && launchProtectedBot.IsPirateBaseLaunchProtected)
            return;

        PlayerRepairDocking repairDocking = GetComponent<PlayerRepairDocking>();
        if (repairDocking != null && repairDocking.IsDamageImmune)
            return;

        WeaponDamageMultipliers damageMultipliers = WeaponDamageInteractionCatalog.ResolveMultipliers(hitContext);
        RememberDamageContext(hitContext, damageMultipliers);
        int shieldOnlyDamage = WeaponDamageInteractionCatalog.ApplyMultiplier(
            ApplyPilotDamageModifiers(Mathf.Max(0, dmg), attackerViewID, hitContext),
            damageMultipliers.ShieldMultiplier);
        WeaponDamageInteractionCatalog.LogDamageContext(this, "shield-only", hitContext, damageMultipliers, attackerViewID, shieldOnlyDamage, 0);
        if (shieldOnlyDamage <= 0 || currentShield <= 0)
            return;

        int previousShield = currentShield;
        int absorbed = Mathf.Min(currentShield, shieldOnlyDamage);
        currentShield -= absorbed;
        TrackDamageTakenAndShieldBreak(currentHP, previousShield);
        photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);

        if (playImpactAudio && absorbed > 0)
        {
            photonView.RPC(nameof(PlayShieldHitAudio), RpcTarget.All);
            photonView.RPC(nameof(PlayShieldHitVisual), RpcTarget.All, impactX, impactY);
        }

        if (IsBotControlled)
        {
            EnemyBot damagedBot = GetComponent<EnemyBot>();
            if (damagedBot != null)
                damagedBot.NotifyDamageTaken(currentHP, currentHP, Mathf.Max(0, previousShield - currentShield), 0, attackerViewID);
        }

        RoundXpTracker.RecordDamage(attackerViewID, photonView, absorbed, 0);
    }

    bool TryForwardDamageToDeployable(int shieldDmg, int hpDmg, int attackerViewID, float impactX, float impactY)
    {
        return TryForwardDamageToDeployable(shieldDmg, hpDmg, attackerViewID, impactX, impactY, WeaponHitContext.None);
    }

    bool TryForwardDamageToDeployable(int shieldDmg, int hpDmg, int attackerViewID, float impactX, float impactY, WeaponHitContext hitContext)
    {
        if (!IsDeployableDamageProxy())
            return false;

        PlayerDeployableBase deployable = GetComponent<PlayerDeployableBase>();
        if (deployable == null)
            deployable = PlayerDeployableRuntime.EnsureAttached(gameObject);

        if (deployable != null)
            deployable.TakeDeployableDamageWithContextAt(
                shieldDmg,
                hpDmg,
                attackerViewID,
                impactX,
                impactY,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);

        return true;
    }

    bool TryForwardShieldOnlyDamageToDeployable(int shieldDmg, int attackerViewID, float impactX, float impactY)
    {
        return TryForwardShieldOnlyDamageToDeployable(shieldDmg, attackerViewID, impactX, impactY, WeaponHitContext.None);
    }

    bool TryForwardShieldOnlyDamageToDeployable(int shieldDmg, int attackerViewID, float impactX, float impactY, WeaponHitContext hitContext)
    {
        if (!IsDeployableDamageProxy())
            return false;

        PlayerDeployableBase deployable = GetComponent<PlayerDeployableBase>();
        if (deployable == null)
            deployable = PlayerDeployableRuntime.EnsureAttached(gameObject);

        if (deployable != null)
            deployable.TakeDeployableShieldOnlyDamageWithContextAt(
                shieldDmg,
                attackerViewID,
                impactX,
                impactY,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);

        return true;
    }

    bool IsDeployableDamageProxy()
    {
        return PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null) ||
               GetComponent<PlayerDeployableBase>() != null;
    }

    void RememberDamageContext(WeaponHitContext hitContext, WeaponDamageMultipliers damageMultipliers)
    {
        LastDamageContext = hitContext;
        LastDamageShieldMultiplier = damageMultipliers.ShieldMultiplier;
        LastDamageHpMultiplier = damageMultipliers.HpMultiplier;
    }

    int ApplyPilotDamageModifiers(int damage, int attackerViewID, string damageSource)
    {
        int result = Mathf.Max(0, damage);
        if (result <= 0)
            return 0;

        if (string.Equals(damageSource, DamageSourceRadioactiveCargo, System.StringComparison.Ordinal))
            return result;

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        if (string.Equals(damageSource, PilotDamageSourceRamming, System.StringComparison.Ordinal) &&
            PilotCatalog.IsSelectedPilot(owner, PilotCatalog.JakeId))
        {
            result = Mathf.Max(1, Mathf.RoundToInt(result * 0.5f));
        }

        if (IsDamageFromSpaceMine(attackerViewID) &&
            PilotCatalog.IsSelectedPilot(owner, PilotCatalog.RoburId))
        {
            result = Mathf.Max(1, Mathf.RoundToInt(result * 0.5f));
        }

        if (IsHumanShipControlled && HasEquippedItem(InventoryItemCatalog.KineticDampenerId) &&
            IsPhysicalImpactDamage(attackerViewID, damageSource))
        {
            result = Mathf.Max(1, Mathf.RoundToInt(result * KineticDampenerDamageMultiplier));
        }

        if (IsHumanShipControlled && HasEquippedItem(InventoryItemCatalog.KineticDampenerId) &&
            IsExplosiveDamageSource(damageSource))
        {
            result = Mathf.Max(1, Mathf.RoundToInt(result * KineticDampenerExplosiveDamageMultiplier));
        }

        if (IsHumanShipControlled && HasEquippedItem(InventoryItemCatalog.StrongPlatingId) &&
            IsEnvironmentalDamageSource(damageSource))
        {
            result = Mathf.Max(1, Mathf.RoundToInt(result * StrongPlatingEnvironmentalDamageMultiplier));
        }

        if (IsHumanShipControlled && HasEquippedItem(InventoryItemCatalog.BulwarkProjectorId) &&
            IsLaserWeaponDamage(attackerViewID, damageSource))
        {
            result = Mathf.Max(1, Mathf.RoundToInt(result * BulwarkProjectorLaserDamageMultiplier));
        }

        EnemyBot targetBot = IsBotControlled ? GetComponent<EnemyBot>() : null;
        if (targetBot != null && targetBot.Kind == EnemyBotKind.Mothership && IsAttackerPilot(attackerViewID, PilotCatalog.SirNowitzkyId))
            result = Mathf.Max(1, Mathf.RoundToInt(result * 1.15f));

        PilotActiveAbilityController pilotAbility = GetComponent<PilotActiveAbilityController>();
        if (pilotAbility != null && pilotAbility.IsJakeBarrierActive)
            result = Mathf.Max(1, Mathf.RoundToInt(result * 0.5f));

        if (IsHumanShipControlled && Time.time < alienAegisBarrierUntil)
            result = Mathf.Max(1, Mathf.RoundToInt(result * AlienAegisBarrierDamageMultiplier));

        if (photonView != null && PilotActiveAbilityController.IsRoburMarked(photonView.ViewID))
            result = Mathf.Max(1, Mathf.RoundToInt(result * 1.5f));

        return result;
    }

    bool IsDamageFromSpaceMine(int attackerViewID)
    {
        return IsDamageFromEnemyKind(attackerViewID, EnemyBotKind.SpaceMine);
    }

    bool IsDamageFromEnemyKind(int attackerViewID, EnemyBotKind kind)
    {
        if (attackerViewID <= 0)
            return false;

        PhotonView attackerView = PhotonView.Find(attackerViewID);
        if (attackerView == null)
            return false;

        EnemyBot bot = attackerView.GetComponent<EnemyBot>();
        return bot != null && bot.Kind == kind;
    }

    bool IsPhysicalImpactDamage(int attackerViewID, string damageSource)
    {
        return string.Equals(damageSource, PilotDamageSourceRamming, System.StringComparison.Ordinal) ||
               IsDamageFromSpaceMine(attackerViewID) ||
               IsDamageFromEnemyKind(attackerViewID, EnemyBotKind.SpaceManta);
    }

    bool IsExplosiveDamageSource(string damageSource)
    {
        return string.Equals(damageSource, DamageSourceExplosive, System.StringComparison.Ordinal);
    }

    bool IsEnvironmentalDamageSource(string damageSource)
    {
        return string.Equals(damageSource, EnvironmentalDamageSource, System.StringComparison.Ordinal) ||
               string.Equals(damageSource, NebulaDamageSource, System.StringComparison.Ordinal);
    }

    bool IsLaserWeaponDamage(int attackerViewID, string damageSource)
    {
        if (string.Equals(damageSource, DamageSourceLaser, System.StringComparison.Ordinal))
            return true;

        return IsDamageFromEnemyKind(attackerViewID, EnemyBotKind.NeutralFighter) ||
               IsDamageFromEnemyKind(attackerViewID, EnemyBotKind.PirateFighter) ||
               IsDamageFromEnemyKind(attackerViewID, EnemyBotKind.PirateFighterElite) ||
               IsDamageFromEnemyKind(attackerViewID, EnemyBotKind.PirateFighterAce) ||
               IsDamageFromEnemyKind(attackerViewID, EnemyBotKind.ContainerShip);
    }

    bool HasEquippedItem(string itemId)
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        return InventoryItemCatalog.HasEquippedItem(equipmentSlots, shipSkinIndex, itemId);
    }

    bool TryTriggerPhaseShield(int previousHp)
    {
        if (phaseShieldTriggered || !IsHumanShipControlled || IsWreck || maxHP <= 0)
            return false;

        if (!HasEquippedItem(InventoryItemCatalog.PhaseShieldId))
            return false;

        int threshold = Mathf.Max(1, Mathf.CeilToInt(maxHP * PhaseShieldHpThresholdRatio));
        if (previousHp <= threshold || currentHP > threshold)
            return false;

        phaseShieldTriggered = true;
        currentHP = Mathf.Max(1, currentHP);
        BeginEquipmentInvulnerabilityAuthority(PhaseShieldInvulnerabilityDuration);
        return true;
    }

    void TryRollShipDamageOnHalfHpCrossing(int previousHp)
    {
        if (!PhotonNetwork.IsMasterClient || !IsHumanShipControlled || IsWreck || currentHP <= 0 || maxHP <= 0)
            return;

        float halfHp = maxHP * 0.5f;
        if (previousHp < halfHp || currentHP >= halfHp)
            return;

        ShipDamageState damageState = GetComponent<ShipDamageState>();
        if (damageState == null)
            damageState = gameObject.AddComponent<ShipDamageState>();

        damageState.TryRollBelowHalfHpDamageAuthority();
    }

    void BeginEquipmentInvulnerabilityAuthority(float duration)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null || duration <= 0f)
            return;

        photonView.RPC(nameof(BeginEquipmentInvulnerabilityRpc), RpcTarget.All, duration);
    }

    public void BeginEquipmentInvulnerabilityLocal(float duration)
    {
        if (duration <= 0f || IsWreck || IsBotControlled || IsNeutralRiderControlled)
            return;

        equipmentInvulnerableUntil = Mathf.Max(equipmentInvulnerableUntil, Time.time + duration);
        if (RoomSettings.AreVisualEffectsEnabled())
            SpawnInvulnerabilityVfx.Attach(this);
    }

    [PunRPC]
    void BeginEquipmentInvulnerabilityRpc(float duration)
    {
        BeginEquipmentInvulnerabilityLocal(duration);
    }

    bool IsAttackerPilot(int attackerViewID, string pilotId)
    {
        if (attackerViewID <= 0)
            return false;

        PhotonView attackerView = PhotonView.Find(attackerViewID);
        return attackerView != null && PilotCatalog.IsSelectedPilot(attackerView.Owner, pilotId);
    }

    void ApplyShieldOnlyEnvironmentalDamage(int dmg, string damageSource)
    {
        if (!PhotonNetwork.IsMasterClient || IsWreck || isEvacuationAnimating || IsSpawnInvulnerable)
            return;

        EnemyBot launchProtectedBot = GetComponent<EnemyBot>();
        if (launchProtectedBot != null && launchProtectedBot.IsPirateBaseLaunchProtected)
            return;

        WeaponHitContext hitContext = new WeaponHitContext(
            WeaponDamageType.Environmental,
            WeaponDeliveryMethod.AreaPulse,
            WeaponDeliveryFlags.Continuous,
            damageSource);
        WeaponDamageMultipliers damageMultipliers = WeaponDamageInteractionCatalog.ResolveMultipliers(hitContext);
        RememberDamageContext(hitContext, damageMultipliers);

        int damage = Mathf.Max(0, dmg);
        if (damage > 0 && IsHumanShipControlled && HasEquippedItem(InventoryItemCatalog.StrongPlatingId))
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * StrongPlatingEnvironmentalDamageMultiplier));

        damage = WeaponDamageInteractionCatalog.ApplyMultiplier(damage, damageMultipliers.ShieldMultiplier);
        WeaponDamageInteractionCatalog.LogDamageContext(this, "environmental-shield", hitContext, damageMultipliers, -1, damage, 0);

        if (damage <= 0 || currentShield <= 0)
            return;

        int previousShield = currentShield;
        currentShield = Mathf.Max(0, currentShield - damage);
        TrackDamageTakenAndShieldBreak(currentHP, previousShield);
        if (currentShield != previousShield)
            photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);
    }

    void TrackDamageTakenAndShieldBreak(int previousHp, int previousShield)
    {
        if (currentHP < previousHp || currentShield < previousShield)
        {
            lastDamageTakenTime = Time.time;
            shieldRegenAccumulator = 0f;
        }

        if (previousShield > 0 && currentShield <= 0)
            TryTriggerAlienAegisBarrier();
    }

    void TryTriggerAlienAegisBarrier()
    {
        if (!PhotonNetwork.IsMasterClient || !IsHumanShipControlled || IsWreck || currentHP <= 0)
            return;

        if (!HasEquippedItem(InventoryItemCatalog.AlienAegisCoreId))
            return;

        photonView.RPC(nameof(BeginAlienAegisBarrierRpc), RpcTarget.All, AlienAegisBarrierDuration);
    }

    [PunRPC]
    void BeginAlienAegisBarrierRpc(float duration)
    {
        if (duration <= 0f || IsWreck || !IsHumanShipControlled)
            return;

        alienAegisBarrierUntil = Mathf.Max(alienAegisBarrierUntil, Time.time + duration);
        if (RoomSettings.AreVisualEffectsEnabled())
            PilotBarrierVfx.Attach(gameObject, duration);
    }

    void TryStartJakeEmergencyRegeneration(int previousHp)
    {
        if (jakeEmergencyRegenerationUsed || currentHP <= 0 || currentHP >= maxHP)
            return;

        if (!IsHumanShipControlled || IsWreck)
            return;

        if (!PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.JakeId))
            return;

        float threshold = maxHP * 0.5f;
        if (previousHp > threshold && currentHP <= threshold)
        {
            jakeEmergencyRegenerationUsed = true;
            if (jakeEmergencyRegenerationRoutine == null)
                jakeEmergencyRegenerationRoutine = StartCoroutine(JakeEmergencyRegenerationRoutine());
        }
    }

    IEnumerator JakeEmergencyRegenerationRoutine()
    {
        const int ticks = 30;
        WaitForSeconds wait = new WaitForSeconds(1f);
        for (int i = 0; i < ticks; i++)
        {
            yield return wait;
            if (!PhotonNetwork.IsMasterClient || IsWreck || isEvacuationAnimating || currentHP <= 0)
                break;

            if (currentHP < maxHP)
            {
                currentHP = Mathf.Min(maxHP, currentHP + 1);
                photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);
            }
        }

        jakeEmergencyRegenerationRoutine = null;
    }

    IEnumerator InitialSpawnInvulnerabilityRoutine()
    {
        if (spawnInvulnerabilityScheduled)
            yield break;

        spawnInvulnerabilityScheduled = true;
        yield return null;

        StartingShipEntryVfx entry = GetComponent<StartingShipEntryVfx>();
        while (entry != null && entry.IsEntryActive)
        {
            yield return null;
            entry = GetComponent<StartingShipEntryVfx>();
        }

        BeginSpawnInvulnerability(SpawnInvulnerabilityDuration);
    }

    void BeginSpawnInvulnerability(float duration)
    {
        if (duration <= 0f || IsWreck || !IsHumanShipControlled)
            return;

        spawnInvulnerableUntil = Mathf.Max(spawnInvulnerableUntil, Time.time + duration);
        if (RoomSettings.AreVisualEffectsEnabled())
            SpawnInvulnerabilityVfx.Attach(this);
    }

    public void RepairVitalsAuthority(int amount)
    {
        if (!PhotonNetwork.IsMasterClient || IsWreck || isEvacuationAnimating || amount <= 0)
            return;

        int previousHp = currentHP;
        int previousShield = currentShield;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        int shieldCap = GetRepairableShieldCap();
        if (currentShield < shieldCap)
            currentShield = Mathf.Min(shieldCap, currentShield + amount);

        if (currentHP != previousHp || currentShield != previousShield)
            photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);
    }

    int GetRepairableShieldCap()
    {
        ShipDamageState damageState = GetComponent<ShipDamageState>();
        return damageState != null ? damageState.GetShieldRepairCap(maxShield) : maxShield;
    }

    Vector2 ResolveDamageImpactPosition(int attackerViewID)
    {
        Vector2 center = transform.position;
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView == null)
            return center;

        Vector2 attackerPosition = attackerView.transform.position;
        Collider2D ownCollider = GetComponentInChildren<Collider2D>();
        if (ownCollider != null)
            return ownCollider.ClosestPoint(attackerPosition);

        Vector2 direction = center - attackerPosition;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.up;

        return center - direction.normalized * 0.45f;
    }

    [PunRPC]
    public void BeginEvacuationSequence(float portalCenterX, float portalCenterY)
    {
        if (isEvacuationAnimating || IsWreck)
            return;

        StartCoroutine(EvacuationSequenceRoutine(new Vector2(portalCenterX, portalCenterY)));
    }

    [PunRPC]
    void SyncVitals(int newHP, int newShield)
    {
        int previousHp = currentHP;
        int previousShield = currentShield;
        currentHP = newHP;
        currentShield = newShield;

        if (photonView.IsMine && hpBar != null)
        {
            hpBar.maxValue = maxHP;
            hpBar.value = currentHP;
        }

        if (photonView.IsMine)
        {
            if (previousHp > 0 && newHP <= 0)
                HapticsManager.PlayDeath();
            else if (newHP < previousHp)
                HapticsManager.PlayHpHit();
            else if (newShield < previousShield)
                HapticsManager.PlayShieldHit();
        }
    }

    public bool CanRequestAstronautKillMeLocally()
    {
        return photonView != null &&
               photonView.IsMine &&
               CanApplyAstronautKillMeAuthority();
    }

    public void RequestLocalAstronautKillMe()
    {
        if (!CanRequestAstronautKillMeLocally())
            return;

        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && photonView != null)
        {
            photonView.RPC(nameof(RequestAstronautKillMe), RpcTarget.MasterClient);
            return;
        }

        ApplyAstronautKillMeAuthority();
    }

    [PunRPC]
    void RequestAstronautKillMe(PhotonMessageInfo info)
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
            return;

        if (photonView != null &&
            photonView.Owner != null &&
            info.Sender != null &&
            photonView.Owner.ActorNumber != info.Sender.ActorNumber)
        {
            return;
        }

        ApplyAstronautKillMeAuthority();
    }

    bool CanApplyAstronautKillMeAuthority()
    {
        return IsAstronautControlled &&
               !IsEnemyAstronautControlled &&
               !IsWreck &&
               !isEvacuationAnimating &&
               !deathHandled &&
               currentHP > 0;
    }

    void ApplyAstronautKillMeAuthority()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            return;

        if (!CanApplyAstronautKillMeAuthority())
            return;

        currentShield = 0;
        currentHP = 0;

        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && photonView != null)
            photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);
        else
            SyncVitals(currentHP, currentShield);

        HandleDeath(-1);
    }

    [PunRPC]
    void HandleBatteryShieldChargeRequest()
    {
        TryBeginBatteryShieldChargeAuthority();
    }

    System.Collections.IEnumerator ApplyBatteryShieldChargeRoutine()
    {
        int shieldPerTick = BatteryShieldPerTick * GetBatteryShieldChargeMultiplier();
        for (int i = 0; i < BatteryTickCount; i++)
        {
            if (IsWreck || isEvacuationAnimating)
                yield break;

            yield return new WaitForSeconds(BatteryTickInterval);

            int shieldCap = GetRepairableShieldCap();
            if (currentShield >= shieldCap)
                yield break;

            currentShield = Mathf.Min(shieldCap, currentShield + shieldPerTick);
            photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);
        }
    }

    int GetBatteryShieldChargeMultiplier()
    {
        return HasEquippedItem(InventoryItemCatalog.AegisBatteryId) ? 2 : 1;
    }

    public void ConfigureBaseStats(int hp, int shield)
    {
        maxHP = Mathf.Max(1, hp);
        maxShield = Mathf.Max(0, shield);
        currentHP = maxHP;
        currentShield = maxShield;

        if (photonView != null)
        {
            photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);
        }
    }

    public bool TryRestoreShieldAuthority(float amount, bool playFullPowerAudio)
    {
        int shieldCap = GetRepairableShieldCap();
        if (!PhotonNetwork.IsMasterClient || IsWreck || isEvacuationAnimating || maxShield <= 0 || shieldCap <= 0 || currentShield <= 0 || currentShield >= shieldCap)
            return false;

        int previousShield = currentShield;
        currentShield = Mathf.Min(shieldCap, currentShield + Mathf.Max(1, Mathf.RoundToInt(amount)));
        photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);

        if (playFullPowerAudio && previousShield < shieldCap && currentShield >= shieldCap)
            photonView.RPC(nameof(PlayShieldFullPowerAudio), RpcTarget.All);

        return currentShield > previousShield;
    }

    void HandleDeath(int attackerViewID)
    {
        if (deathHandled)
            return;

        deathHandled = true;

        PlayerMovement localMovement = GetComponent<PlayerMovement>();
        if (localMovement != null)
            localMovement.StopEngineAudioImmediately();

        EnemyBot bot = IsBotControlled ? GetComponent<EnemyBot>() : null;
        bool isEnemyAstronaut = IsEnemyAstronautControlled;
        bool useCustomBotDeath = bot != null && bot.HasCustomDeathExplosion();
        bool useSpaceAnimalDeath = bot != null && EnemyBot.IsSpaceAnimalKind(bot.Kind);
        if (!useCustomBotDeath && !useSpaceAnimalDeath)
            photonView.RPC(nameof(PlayDeathExplosion), RpcTarget.All);

        if (!IsBotControlled && !IsNeutralRiderControlled && !isEnemyAstronaut)
        {
            photonView.RPC(nameof(ShowDeathMessage), RpcTarget.All);
        }

        if (IsBotControlled)
        {
            if (PhotonNetwork.IsMasterClient && bot != null)
                AstronautSurvivor.SpawnEnemySurvivorsFrom(bot);

            if (useCustomBotDeath && bot != null)
            {
                if (ShouldConvertMineShotKillToWreck(bot, attackerViewID))
                {
                    photonView.RPC(nameof(BecomeEnemyWreck), RpcTarget.All, (int)bot.Kind);
                    return;
                }

                bot.RequestDetonation();
                return;
            }

            if (useSpaceAnimalDeath && bot != null)
            {
                StartCoroutine(DestroySpaceAnimalEnemyAfterDeathFrame(bot.Kind));
                return;
            }

            photonView.RPC(nameof(BecomeEnemyWreck), RpcTarget.All, bot != null ? (int)bot.Kind : (int)EnemyBotKind.Drone);
            return;
        }

        if (IsNeutralRiderControlled)
        {
            NeutralRiderController rider = GetComponent<NeutralRiderController>();
            string neutralWreckLoot = rider != null ? rider.BuildWreckLootJson() : PlayerProfileService.SerializeShipInventorySlots(new[] { InventoryItemCatalog.SpaceJunkStandardId });
            int neutralShipSkinIndex = rider != null ? rider.ShipSkinIndex : ShipCatalog.ExplorerBasicSkinIndex;
            photonView.RPC(nameof(BecomeNeutralRiderWreck), RpcTarget.All, neutralWreckLoot, neutralShipSkinIndex);
            return;
        }

        if (IsAstronautControlled)
        {
            if (isEnemyAstronaut)
            {
                if (photonView.Owner != null)
                    photonView.RPC(nameof(DestroySelf), photonView.Owner);
                else
                    DestroySelf();
                return;
            }

            int finalScore = GetCurrentRoundXp();
            if (!IsBotControlled)
            {
                finalScore = RoundResultsTracker.RecordOutcome(photonView.Owner, finalScore, "dead");
            }
            photonView.RPC(nameof(NotifyFinalDeath), photonView.Owner, finalScore);
            photonView.RPC(nameof(DestroySelf), photonView.Owner);
            return;
        }

        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(photonView.Owner, 0);
        string[] currentShipSlots = PlayerProfileService.GetPlayerShipInventorySlots(photonView.Owner);
        int shipCapacity = PlayerProfileService.GetPlayerShipInventoryCapacity(photonView.Owner);
        bool emergencySuitBeaconEquipped = HasEquippedItem(InventoryItemCatalog.EmergencySuitBeaconId);
        bool escapePodEquipped = HasEquippedItem(InventoryItemCatalog.EscapePodId);
        bool equipmentLossEnabled = RoomSettings.IsEquipmentLossEnabled();
        string[] wreckLootSlots = PlayerProfileService.BuildLossWreckLoot(currentShipSlots, shipSkinIndex, shipCapacity);
        string wreckLoot = PlayerProfileService.SerializeShipInventorySlots(wreckLootSlots);
        string[] astronautCargoSlots = PlayerProfileService.BuildAstronautCargoSnapshot(currentShipSlots, shipSkinIndex, shipCapacity);
        string astronautCargo = PlayerProfileService.SerializeShipInventorySlots(astronautCargoSlots);
        Vector3 astronautSpawnPosition = FindSafeAstronautSpawnPosition();
        RoundXpTracker.RecordPlayerShipDestroyed(photonView.Owner);
        string protectedEquipmentItemId = escapePodEquipped ? InventoryItemCatalog.EscapePodId : string.Empty;
        photonView.RPC(nameof(ApplyLocalShipLossForWreck), photonView.Owner, shipSkinIndex, true, equipmentLossEnabled, astronautCargo, protectedEquipmentItemId);
        photonView.RPC(nameof(SpawnAstronautAfterDestruction), photonView.Owner, astronautSpawnPosition.x, astronautSpawnPosition.y, transform.eulerAngles.z, emergencySuitBeaconEquipped, escapePodEquipped);
        photonView.RPC(nameof(BecomeWreck), RpcTarget.All, wreckLoot, shipSkinIndex);
    }

    IEnumerator DestroySpaceAnimalEnemyAfterDeathFrame(EnemyBotKind kind)
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot != null)
        {
            if (kind == EnemyBotKind.GravitySquid && bot.photonView != null)
                bot.photonView.RPC(nameof(EnemyBot.StopGravitySquidTetherRpc), RpcTarget.All);

            bot.enabled = false;
        }

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.enabled = false;

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.DisableAndClearTrails();

        if (PhotonNetwork.IsMasterClient)
            SpawnSpaceAnimalRemainsDrop();

        float visualTargetSize = bot != null ? bot.VisualTargetSize : 2.4f;
        photonView.RPC(
            nameof(PlaySpaceAnimalDeathAnimationRpc),
            RpcTarget.All,
            (int)kind,
            transform.position.x,
            transform.position.y,
            transform.position.z,
            transform.eulerAngles.z,
            visualTargetSize);

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        yield return new WaitForSeconds(0.08f);

        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    void SpawnSpaceAnimalRemainsDrop()
    {
        Vector2 driftDirection = Random.insideUnitCircle.normalized;
        if (driftDirection.sqrMagnitude < 0.001f)
            driftDirection = Vector2.up;

        GameObject drop = PhotonNetwork.Instantiate(
            "TreasureNetwork",
            transform.position,
            Quaternion.identity,
            0,
            new object[] { InventoryItemCatalog.SpaceAnimalRemainsId });

        if (drop == null)
            return;

        Rigidbody2D dropBody = drop.GetComponent<Rigidbody2D>();
        if (dropBody != null)
        {
            dropBody.linearVelocity = driftDirection * Random.Range(0.42f, 0.82f);
            dropBody.angularVelocity = Random.Range(-34f, 34f);
        }
    }

    [PunRPC]
    void PlaySpaceAnimalDeathAnimationRpc(int kindValue, float x, float y, float z, float rotationZ, float visualTargetSize)
    {
        SpaceAnimalDeathVfx.Play((EnemyBotKind)kindValue, new Vector3(x, y, z), rotationZ, visualTargetSize);

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }
    }

    IEnumerator DestroyEnemyWithoutWreckAfterDeathFrame()
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot != null)
            bot.enabled = false;

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        yield return null;

        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    Vector3 FindSafeAstronautSpawnPosition()
    {
        Vector2 origin = transform.position;
        Vector2 right = transform.right;
        Vector2 up = transform.up;
        if (right.sqrMagnitude < 0.001f)
            right = Vector2.right;
        if (up.sqrMagnitude < 0.001f)
            up = Vector2.up;

        Vector2[] offsets =
        {
            right * 0.85f,
            -right * 0.85f,
            up * 0.85f,
            -up * 0.85f,
            (right + up).normalized * 1.05f,
            (right - up).normalized * 1.05f,
            (-right + up).normalized * 1.05f,
            (-right - up).normalized * 1.05f,
            right * 1.25f,
            -right * 1.25f,
            up * 1.25f,
            -up * 1.25f,
            (right + up).normalized * 1.45f,
            (right - up).normalized * 1.45f,
            (-right + up).normalized * 1.45f,
            (-right - up).normalized * 1.45f
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2 candidate = origin + offsets[i];
            if (IsAstronautSpawnPositionFree(candidate))
                return new Vector3(candidate.x, candidate.y, 0f);
        }

        return new Vector3(origin.x, origin.y, 0f);
    }

    bool IsAstronautSpawnPositionFree(Vector2 candidate)
    {
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(candidate, AstronautSpawnClearanceRadius, out Collider2D[] hits);
        for (int i = 0; i < hitCount; i++)
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

    bool ShouldConvertMineShotKillToWreck(EnemyBot bot, int attackerViewID)
    {
        if (bot == null || bot.Kind != EnemyBotKind.SpaceMine || attackerViewID <= 0)
            return false;

        PhotonView attackerView = PhotonView.Find(attackerViewID);
        if (attackerView == null)
            return false;

        PlayerHealth attackerHealth = attackerView.GetComponent<PlayerHealth>();
        return attackerHealth != null &&
               !attackerHealth.IsBotControlled &&
               !attackerHealth.IsNeutralRiderControlled &&
               !attackerHealth.IsAstronautControlled &&
               !attackerHealth.IsWreck;
    }

    int GetCurrentRoundXp()
    {
        int propScore = RoomSettings.GetPlayerRoundXp(photonView.Owner);
        if (propScore > 0)
            return propScore;

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            return collector.totalScore;

        return 0;
    }

    [PunRPC]
    public void OnEvacuated(int amount)
    {
        if (!photonView.IsMine)
            return;

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
        {
            collector.AddScore(amount);
        }
    }

    [PunRPC]
    public async void NotifyFinalEvacuation(int finalScore, string outcome)
    {
        if (!photonView.IsMine)
            return;

        pendingEvacuationFinalScore = Mathf.Max(0, finalScore);
        pendingEvacuationOutcome = string.IsNullOrWhiteSpace(outcome) ? "extracted" : outcome;
        hasPendingEvacuationSummary = true;
        NetworkManager.MarkCurrentRoundEndedForLocalPlayer(pendingEvacuationOutcome);
        if (string.Equals(pendingEvacuationOutcome, "evacuated", System.StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await PlayerProfileService.Instance.RestorePendingAstronautCargoAsync();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Failed to restore astronaut cargo: " + ex);
            }
        }

        TryRecordExtractionPilotProgress(pendingEvacuationOutcome);
    }

    async void TryRecordExtractionPilotProgress(string outcome)
    {
        if (!string.Equals(outcome, "extracted", System.StringComparison.OrdinalIgnoreCase))
            return;

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        string[] shipSlots = profile != null && profile.Inventory != null ? profile.Inventory.ShipSlots : null;
        int cargoValue = PilotCatalog.GetCargoValueAstrons(shipSlots);
        if (cargoValue >= RoundXpBalance.RaiderCargoValueThreshold)
            await PlayerProfileService.Instance.UnlockPilotAsync(PilotCatalog.NovaId);

        if (AshPilotRoundTracker.MeetsOverloadReturnRequirements(profile, out _))
            await PlayerProfileService.Instance.RecordPilotAshOverloadReturnAsync();

        if (string.Equals(RoomSettings.GetSelectedLobbyMapId(), "pirate_bay", System.StringComparison.OrdinalIgnoreCase))
            await PlayerProfileService.Instance.RecordPilotPirateBayReturnAsync();

        if (AtlasPilotRoundTracker.GetNetCargoValueAstrons(profile) >= PilotCatalog.AtlasRequiredNetCargoAstrons)
            await PlayerProfileService.Instance.RecordPilotAtlasMapReturnAsync(RoomSettings.GetSelectedLobbyMapId());
    }

    [PunRPC]
    public async void AwardCharlieLastSecondExtractionBonus()
    {
        if (!photonView.IsMine)
            return;

        await PlayerProfileService.Instance.AddAstronsAsync(1000);
    }

    [PunRPC]
    public async void UnlockRoburPilotAfterHumanKill()
    {
        if (!photonView.IsMine)
            return;

        await PlayerProfileService.Instance.UnlockPilotAsync(PilotCatalog.RoburId);
    }

    [PunRPC]
    public async void RecordPilotDroneKillProgress()
    {
        if (!photonView.IsMine)
            return;

        await PlayerProfileService.Instance.RecordPilotDroneKillAsync();
    }

    [PunRPC]
    public async void RecordMothershipKillMapProgress()
    {
        if (!photonView.IsMine)
            return;

        await PlayerProfileService.Instance.RecordMothershipKillAsync();
    }

    [PunRPC]
    public void RecordAshComputerEnemyKillProgress()
    {
        if (!photonView.IsMine)
            return;

        AshPilotRoundTracker.RecordComputerEnemyKill();
    }

    [PunRPC]
    public void ApplyNovaKillSpeedBoost()
    {
        if (!photonView.IsMine)
            return;

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.ActivatePilotSpeedBoost(1.2f, 5f);
    }

    [PunRPC]
    public void DestroySelf()
    {
        if (!photonView.IsMine)
            return;

        TryDestroyOwnedPhotonObject();
    }

    [PunRPC]
    void NotifyFinalDeath(int finalScore)
    {
        if (!photonView.IsMine)
            return;

        PlayerProfileService.Instance.DiscardPendingAstronautCargo();
        EarlyRoundExitUI.ShowEndRoundButton(finalScore, "dead");
    }

    IEnumerator EvacuationSequenceRoutine(Vector2 portalCenter)
    {
        isEvacuationAnimating = true;
        if (photonView.IsMine)
            GameplayHudVisibility.SuppressForExtractionCinematic();

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
        {
            shooting.CancelActiveGadgetEffectsForShipLoss();
            shooting.enabled = false;
        }

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.enabled = false;

        LootingFriendController lootingFriend = GetComponent<LootingFriendController>();
        if (lootingFriend != null)
            lootingFriend.DeactivateForShipLoss();

        FiringFriendController firingFriend = GetComponent<FiringFriendController>();
        if (firingFriend != null)
            firingFriend.DeactivateForShipLoss();

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.DisableAndClearTrails();

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        AudioManager.Instance.PlayExtractionSequenceAt(transform.position);

        Vector3 startPosition = transform.position;
        Vector3 endPosition = new Vector3(portalCenter.x, portalCenter.y, startPosition.z);
        Vector3 startScale = transform.localScale;
        Vector3 endScale = new Vector3(
            Mathf.Sign(startScale.x) * MinimumEvacuationScale,
            Mathf.Sign(startScale.y) * MinimumEvacuationScale,
            startScale.z);
        if (Mathf.Abs(endScale.x) < MinimumEvacuationScale)
            endScale.x = MinimumEvacuationScale;
        if (Mathf.Abs(endScale.y) < MinimumEvacuationScale)
            endScale.y = MinimumEvacuationScale;

        float elapsed = 0f;
        while (elapsed < EvacuationAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / EvacuationAnimationDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.Lerp(startPosition, endPosition, eased);
            transform.localScale = Vector3.Lerp(startScale, endScale, eased);
            yield return null;
        }

        transform.position = endPosition;
        transform.localScale = endScale;

        if (photonView.IsMine)
        {
            if (hasPendingEvacuationSummary)
                EarlyRoundExitUI.ShowFinishedRoundSummary(pendingEvacuationFinalScore, pendingEvacuationOutcome);

            TryDestroyOwnedPhotonObject();
        }
    }

    [PunRPC]
    public void ClearLocalShipInventoryForWreck(int shipSkinIndex)
    {
        ApplyLocalShipLossForWreck(shipSkinIndex, true, false, string.Empty, string.Empty);
    }

    [PunRPC]
    public void ApplyLocalShipLossForWreck(int shipSkinIndex, bool loseShipInventory, bool loseEquipment, string serializedAstronautCargo)
    {
        ApplyLocalShipLossForWreck(shipSkinIndex, loseShipInventory, loseEquipment, serializedAstronautCargo, string.Empty);
    }

    [PunRPC]
    public async void ApplyLocalShipLossForWreck(int shipSkinIndex, bool loseShipInventory, bool loseEquipment, string serializedAstronautCargo, string protectedEquipmentItemId)
    {
        if (!photonView.IsMine)
            return;

        try
        {
            await PlayerProfileService.Instance.ApplyShipLossAsync(shipSkinIndex, loseShipInventory, loseEquipment, serializedAstronautCargo, protectedEquipmentItemId);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to apply local ship loss: " + ex);
        }
    }

    [PunRPC]
    void SpawnAstronautAfterDestruction(float x, float y, float rotationZ, bool emergencySuitBeaconEquipped, bool escapePodEquipped)
    {
        if (!photonView.IsMine)
            return;

        if (astronautSpawnedAfterDestruction)
            return;

        astronautSpawnedAfterDestruction = true;

        PhotonNetwork.LocalPlayer.TagObject = null;
        GameObject astronaut = PhotonNetwork.Instantiate(
            "Player",
            new Vector3(x, y, 0f),
            Quaternion.Euler(0f, 0f, rotationZ),
            0,
            new object[] { AstronautSurvivor.AstronautInstantiationMarker, emergencySuitBeaconEquipped, escapePodEquipped });

        if (astronaut != null)
        {
            AstronautSurvivor survivor = astronaut.GetComponent<AstronautSurvivor>();
            if (survivor == null)
                survivor = astronaut.AddComponent<AstronautSurvivor>();
            survivor.InitializeFromPhotonData();
            ActorIdentity.Ensure(astronaut);

            PlayerHealth astronautHealth = astronaut.GetComponent<PlayerHealth>();
            if (astronautHealth != null)
                GameVisualTheme.ApplyPlayerVisual(astronautHealth);
            GameVisualTheme.RequestRuntimeRefresh();

            PhotonNetwork.LocalPlayer.TagObject = astronaut;
        }
    }

    [PunRPC]
    void BecomeWreck(string serializedLoot, int shipSkinIndex)
    {
        IsWreck = true;
        ActorIdentity.Ensure(gameObject);

        if (photonView != null &&
            photonView.IsMine &&
            PhotonNetwork.LocalPlayer != null &&
            ReferenceEquals(PhotonNetwork.LocalPlayer.TagObject, gameObject))
        {
            PhotonNetwork.LocalPlayer.TagObject = null;
        }

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
        {
            shooting.CancelActiveGadgetEffectsForShipLoss();
            shooting.enabled = false;
        }

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
        {
            collector.ForceCancelCollectionForDeath();
            collector.enabled = false;
        }

        LootingFriendController lootingFriend = GetComponent<LootingFriendController>();
        if (lootingFriend != null)
            lootingFriend.DeactivateForShipLoss();

        FiringFriendController firingFriend = GetComponent<FiringFriendController>();
        if (firingFriend != null)
            firingFriend.DeactivateForShipLoss();

        HealthBarUI healthBarUi = GetComponent<HealthBarUI>();
        if (healthBarUi != null)
            Destroy(healthBarUi);

        ShieldBarUI shieldBarUi = GetComponent<ShieldBarUI>();
        if (shieldBarUi != null)
            Destroy(shieldBarUi);

        BoosterBarUI boosterBarUi = GetComponent<BoosterBarUI>();
        if (boosterBarUi != null)
            Destroy(boosterBarUi);

        ComplexAmmoBarUI complexAmmoBarUi = GetComponent<ComplexAmmoBarUI>();
        if (complexAmmoBarUi != null)
            Destroy(complexAmmoBarUi);

        SuperAttackUI superAttackUi = GetComponent<SuperAttackUI>();
        if (superAttackUi != null)
            Destroy(superAttackUi);

        WeaponSwitchButtonUI weaponSwitchUi = GetComponent<WeaponSwitchButtonUI>();
        if (weaponSwitchUi != null)
            Destroy(weaponSwitchUi);

        ShipInventoryHudUI cargoHudUi = GetComponent<ShipInventoryHudUI>();
        if (cargoHudUi != null)
            Destroy(cargoHudUi);

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.DisableAndClearTrails();

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = 6f;
            body.linearDamping = 0.62f;
            body.angularDamping = 0.78f;

            Vector2 driftDirection = Random.insideUnitCircle.normalized;
            if (driftDirection.sqrMagnitude < 0.001f)
                driftDirection = Vector2.left;

            body.linearVelocity = driftDirection * 0.14f;
            body.angularVelocity = Random.Range(-5f, 5f);
        }

        ShipWreck wreck = GetComponent<ShipWreck>();
        if (wreck == null)
            wreck = gameObject.AddComponent<ShipWreck>();

        wreck.InitializeFromLootJson(serializedLoot, shipSkinIndex);
        wreck.SetBaseColor(Color.white);

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            Sprite wreckSprite = LoadPlayerWreckSprite(shipSkinIndex);
            if (wreckSprite != null)
                renderer.sprite = wreckSprite;

            renderer.color = Color.white;
        }

        GameVisualTheme.ApplyPlayerVisual(this);
    }

    [PunRPC]
    void BecomeNeutralRiderWreck(string serializedLoot, int shipSkinIndex)
    {
        IsWreck = true;
        ActorIdentity.Ensure(gameObject);

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.enabled = false;

        NeutralRiderController rider = GetComponent<NeutralRiderController>();
        if (rider != null)
            rider.MarkWreckConverted();

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.DisableAndClearTrails();

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = true;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.simulated = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = 5.2f;
            body.linearDamping = 0.62f;
            body.angularDamping = 0.78f;

            Vector2 driftDirection = UnityEngine.Random.insideUnitCircle.normalized;
            if (driftDirection.sqrMagnitude < 0.001f)
                driftDirection = Vector2.left;

            body.linearVelocity = driftDirection * 0.16f;
            body.angularVelocity = UnityEngine.Random.Range(-5f, 5f);
        }

        ShipWreck wreck = GetComponent<ShipWreck>();
        if (wreck == null)
            wreck = gameObject.AddComponent<ShipWreck>();

        int safeSkinIndex = Mathf.Clamp(shipSkinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex);
        wreck.InitializeFromLootJson(serializedLoot, safeSkinIndex);
        wreck.SetBaseColor(new Color(0.82f, 0.96f, 1f, 0.96f));

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            Sprite wreckSprite = LoadPlayerWreckSprite(safeSkinIndex);
            if (wreckSprite != null)
                renderer.sprite = wreckSprite;

            renderer.color = Color.white;
        }

        GameVisualTheme.ApplyPlayerVisual(this);
    }

    [PunRPC]
    void BecomeEnemyWreck(int kindValue)
    {
        IsWreck = true;
        ActorIdentity.Ensure(gameObject);
        EnemyBotKind enemyKind = (EnemyBotKind)kindValue;

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot != null)
        {
            if (enemyKind == EnemyBotKind.RescueShip && bot.photonView != null)
                bot.photonView.RPC(nameof(EnemyBot.StopRescueShipBeamRpc), RpcTarget.All);

            if (enemyKind == EnemyBotKind.PirateBase)
            {
                bot.DropPirateBaseCargoOnDeath();
                if (bot.photonView != null)
                    bot.photonView.RPC(nameof(EnemyBot.StopPirateBaseCollectionBeamRpc), RpcTarget.All);
            }

            if (enemyKind == EnemyBotKind.ContainerShip)
            {
                bot.HideContainerShipCargoVisual();
                bot.DropContainerShipCargoOnDeath();
            }

            if (enemyKind == EnemyBotKind.Mothership)
                bot.ConvertMothershipTurretsToWreckVisuals();

            if (enemyKind == EnemyBotKind.CosmicWorm)
            {
                CosmicWormVisualController.StopFor(bot);
                if (photonView != null)
                    CosmicWormSwallowVfx.StopEffect(photonView.ViewID);
            }

            bot.enabled = false;
        }

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.enabled = false;

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.DisableAndClearTrails();

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = true;

        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(enemyKind);
        EnemyWreckProfile wreckProfile = definition != null ? definition.Wreck : null;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.simulated = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = wreckProfile != null ? wreckProfile.Mass : 4.6f;
            body.linearDamping = wreckProfile != null ? wreckProfile.LinearDamping : 0.56f;
            body.angularDamping = wreckProfile != null ? wreckProfile.AngularDamping : 0.72f;

            Vector2 driftDirection = Random.insideUnitCircle.normalized;
            if (driftDirection.sqrMagnitude < 0.001f)
                driftDirection = Vector2.left;

            body.linearVelocity = driftDirection * (wreckProfile != null ? wreckProfile.DriftSpeed : 0.12f);
            float angularRange = wreckProfile != null ? wreckProfile.AngularVelocityRange : 4f;
            body.angularVelocity = Random.Range(-angularRange, angularRange);
        }

        ShipWreck wreck = GetComponent<ShipWreck>();
        if (wreck == null)
            wreck = gameObject.AddComponent<ShipWreck>();

        string rewardItemId = wreckProfile != null ? wreckProfile.RewardItemId : InventoryItemCatalog.DroidScrapId;
        string serializedLoot = PlayerProfileService.SerializeShipInventorySlots(new[] { rewardItemId });
        wreck.InitializeFromLootJson(serializedLoot, -1, kindValue);
        wreck.SetDestroyWhenEmpty(wreckProfile == null || wreckProfile.DestroyWhenEmpty);

        Color baseColor = wreckProfile != null ? wreckProfile.BaseColor : new Color(0.2f, 0.23f, 0.26f, 0.94f);
        wreck.SetBaseColor(baseColor);
        TryDropPirateCaseNearEnemyWreck(enemyKind, body);

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            Sprite wreckSprite = wreckProfile != null ? wreckProfile.GetVisualSprite() : null;
            if (wreckSprite != null)
                renderer.sprite = wreckSprite;

            renderer.color = baseColor;
        }

        ConfigureEnemyWreckCollider(enemyKind, renderer);
        GameVisualTheme.ApplyPlayerVisual(this);
    }

    void TryDropPirateCaseNearEnemyWreck(EnemyBotKind enemyKind, Rigidbody2D wreckBody)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        float dropChance = GetPirateCaseDropChance(enemyKind);
        if (dropChance <= 0f || Random.value >= dropChance)
            return;

        Vector2 direction = wreckBody != null && wreckBody.linearVelocity.sqrMagnitude > 0.001f
            ? wreckBody.linearVelocity.normalized
            : Random.insideUnitCircle;

        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.up;

        direction.Normalize();
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        float distance = enemyKind == EnemyBotKind.PirateBase ? 1.65f : 0.92f;
        Vector3 dropPosition = transform.position + (Vector3)(direction * distance + tangent * Random.Range(-0.32f, 0.32f));
        Vector2 inheritedDrift = wreckBody != null ? wreckBody.linearVelocity * 0.35f : Vector2.zero;
        Vector2 driftVelocity = inheritedDrift + direction * Random.Range(0.45f, 0.9f);

        DroppedCargoManager.DropItemAtPosition(InventoryItemCatalog.PirateCaseId, dropPosition, driftVelocity);
    }

    static float GetPirateCaseDropChance(EnemyBotKind enemyKind)
    {
        switch (enemyKind)
        {
            case EnemyBotKind.PirateFighter:
                return 0.03f;
            case EnemyBotKind.Corsair:
                return 0.06f;
            case EnemyBotKind.PirateFighterElite:
                return 0.10f;
            case EnemyBotKind.PirateFighterAce:
            case EnemyBotKind.PirateBase:
                return 0.20f;
            default:
                return 0f;
        }
    }

    void ConfigureEnemyWreckCollider(EnemyBotKind enemyKind, SpriteRenderer renderer)
    {
        if (enemyKind != EnemyBotKind.CosmicWorm)
            return;

        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            Bounds bounds = renderer != null ? renderer.bounds : new Bounds(transform.position, new Vector3(2.6f, 1.8f, 0f));
            Vector2 compactSize = new Vector2(
                Mathf.Clamp(bounds.size.x * 0.22f, 1.7f, 2.9f),
                Mathf.Clamp(bounds.size.y * 0.34f, 1.15f, 2.2f));
            SetWorldBoxColliderSize(boxCollider, compactSize);
            boxCollider.offset = Vector2.zero;
            boxCollider.isTrigger = true;
        }

        CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
        if (circleCollider != null)
            circleCollider.enabled = false;
    }

    static void SetWorldBoxColliderSize(BoxCollider2D collider2D, Vector2 worldSize)
    {
        if (collider2D == null)
            return;

        Vector3 scale = collider2D.transform.lossyScale;
        float safeX = Mathf.Abs(scale.x) > 0.0001f ? Mathf.Abs(scale.x) : 1f;
        float safeY = Mathf.Abs(scale.y) > 0.0001f ? Mathf.Abs(scale.y) : 1f;
        collider2D.size = new Vector2(worldSize.x / safeX, worldSize.y / safeY);
    }

    [PunRPC]
    void ShowDeathMessage()
    {
        GameObject obj = FindObjectEvenIfDisabled("DeathMessage");
        if (obj != null)
        {
            SetDeathMessageText(obj);
            PositionDeathMessageBelowBooster(obj);
            obj.SetActive(true);
            DeathMessageAutoHide hider = obj.GetComponent<DeathMessageAutoHide>();
            if (hider == null)
                hider = obj.AddComponent<DeathMessageAutoHide>();

            hider.ShowFor(DeathMessageDuration);
        }
    }

    void SetDeathMessageText(GameObject obj)
    {
        TMP_Text text = obj.GetComponent<TMP_Text>();
        if (text == null)
            text = obj.GetComponentInChildren<TMP_Text>(true);

        if (text != null)
        {
            text.text = "Someone is dead...";
            text.alignment = TextAlignmentOptions.Center;
        }
    }

    void PositionDeathMessageBelowBooster(GameObject obj)
    {
        if (obj == null)
            return;

        RectTransform messageRect = obj.GetComponent<RectTransform>();
        TMP_Text text = obj.GetComponent<TMP_Text>() ?? obj.GetComponentInChildren<TMP_Text>(true);
        if (messageRect == null && text != null)
            messageRect = text.rectTransform;

        GameObject boosterObject = GameObject.Find(BoosterBarObjectName);
        RectTransform boosterRect = boosterObject != null ? boosterObject.GetComponent<RectTransform>() : null;
        if (messageRect == null || boosterRect == null)
            return;

        if (boosterRect.parent != null && messageRect.parent != boosterRect.parent)
            messageRect.SetParent(boosterRect.parent, false);

        float boosterWidth = boosterRect.rect.width > 0f ? boosterRect.rect.width : Mathf.Abs(boosterRect.sizeDelta.x);
        float boosterHeight = boosterRect.rect.height > 0f ? boosterRect.rect.height : Mathf.Abs(boosterRect.sizeDelta.y);
        float boosterBottomY = boosterRect.anchoredPosition.y - (boosterHeight * boosterRect.pivot.y);

        messageRect.anchorMin = boosterRect.anchorMin;
        messageRect.anchorMax = boosterRect.anchorMax;
        messageRect.pivot = new Vector2(0.5f, 1f);
        messageRect.anchoredPosition = new Vector2(boosterRect.anchoredPosition.x, boosterBottomY - DeathMessageBoosterGap);
        messageRect.sizeDelta = new Vector2(Mathf.Max(560f, boosterWidth + DeathMessageExtraWidth), DeathMessageHeight);
        messageRect.SetAsLastSibling();

        if (text != null)
        {
            if (text.rectTransform != messageRect)
            {
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = Vector2.zero;
                text.rectTransform.offsetMax = Vector2.zero;
            }

            text.alignment = TextAlignmentOptions.Center;
        }
    }

    [PunRPC]
    void PlayDeathExplosion()
    {
        AudioManager.Instance.PlayExplosionAt(transform.position);

        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (IsBotControlled)
        {
            EnemyBot bot = GetComponent<EnemyBot>();
            EnemyDeathBoomVfx.Spawn(transform.position, renderer, bot != null ? bot.VisualTargetSize : GameVisualTheme.PlayerTargetSize);
            return;
        }

        if (!IsNeutralRiderControlled && !IsAstronautControlled)
        {
            PlayerShipExplosionVfx.Spawn(transform.position, renderer);
        }
    }

    [PunRPC]
    void PlayShieldHitAudio()
    {
        AudioManager.Instance.PlayShieldHitAt(transform.position);
    }

    [PunRPC]
    void PlayShieldHitVisual(float x, float y)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        Vector2 visualPosition = ResolveVisibleShieldHitPosition(new Vector2(x, y), renderer);
        ShieldHitVfx.Spawn(new Vector3(visualPosition.x, visualPosition.y, 0f), renderer);
    }

    Vector2 ResolveVisibleShieldHitPosition(Vector2 requestedPosition, SpriteRenderer renderer)
    {
        Collider2D ownCollider = GetComponentInChildren<Collider2D>();
        if (ownCollider != null)
        {
            Vector2 closestPoint = ownCollider.ClosestPoint(requestedPosition);
            if (Vector2.Distance(closestPoint, transform.position) > 0.02f)
                return closestPoint;
        }

        if (renderer != null)
        {
            Vector3 closestPoint = renderer.bounds.ClosestPoint(requestedPosition);
            if (Vector2.Distance(closestPoint, transform.position) > 0.02f)
                return closestPoint;
        }

        return transform.position;
    }

    [PunRPC]
    void PlayBatteryShieldChargeAudio()
    {
        AudioManager.Instance.PlayShieldChargeAt(transform.position);
    }

    [PunRPC]
    void PlayShieldFullPowerAudio()
    {
        AudioManager.Instance.PlayShieldFullPowerAt(transform.position);
    }

    [PunRPC]
    void PlayHpHitAudio()
    {
        AudioManager.Instance.PlayHpHitAt(transform.position);
    }

    [PunRPC]
    void PlayHpHitVisual(float x, float y)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        Vector2 visualPosition = ResolveVisibleShieldHitPosition(new Vector2(x, y), renderer);
        HpHitSparksVfx.Spawn(new Vector3(visualPosition.x, visualPosition.y, 0f), transform.position, renderer);
    }

    [PunRPC]
    public void OnTimeUp()
    {
        if (!photonView.IsMine)
            return;

        PlayerProfileService.Instance.DiscardPendingAstronautCargo();
        ShowTimeUpMessage();
        StartCoroutine(DieAfterDelay());
    }

    void ShowTimeUpMessage()
    {
        GameObject obj = GameObject.Find("TimeUpMessage");
        if (obj != null)
        {
            obj.SetActive(true);
        }
    }

    IEnumerator DieAfterDelay()
    {
        yield return new WaitForSeconds(1.5f);

        if (photonView.IsMine)
        {
            TryDestroyOwnedPhotonObject();
        }
    }

    IEnumerator DestroyBotSafely()
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot != null)
            bot.enabled = false;

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.enabled = false;

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        yield return null;

        if (PhotonNetwork.IsConnected && photonView.IsMine)
            TryDestroyOwnedPhotonObject();
        else if (!PhotonNetwork.IsConnected)
            Destroy(gameObject);
    }

    void TryDestroyOwnedPhotonObject()
    {
        if (destroyRequested || gameObject == null)
            return;

        destroyRequested = true;

        if (PhotonNetwork.IsConnected && photonView != null)
        {
            if (photonView.IsMine)
                PhotonNetwork.Destroy(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    Sprite LoadPlayerWreckSprite(int shipSkinIndex)
    {
        string resourcePath = ShipCatalog.GetWreckResourcePathForSkin(shipSkinIndex);
        if (!string.IsNullOrWhiteSpace(resourcePath))
        {
            Sprite resourceSprite = Resources.Load<Sprite>(resourcePath);
            if (resourceSprite != null)
                return resourceSprite;
        }

#if UNITY_EDITOR
        string editorPath = ShipCatalog.GetWreckEditorResourcePathForSkin(shipSkinIndex);
        if (!string.IsNullOrWhiteSpace(editorPath))
        {
            UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(editorPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                    return sprite;
            }

            Sprite directSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(editorPath);
            if (directSprite != null)
                return directSprite;
        }

        string fallbackPath = ShipCatalog.GetWreckEditorFallbackPathForSkin(shipSkinIndex);
        if (!string.IsNullOrWhiteSpace(fallbackPath))
        {
            Sprite fallbackSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(fallbackPath);
            if (fallbackSprite != null)
                return fallbackSprite;
        }
#endif

        return null;
    }

    GameObject FindObjectEvenIfDisabled(string name)
    {
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject go in all)
        {
            if (go.name == name)
                return go;
        }

        return null;
    }
}

public sealed class GravityTetherPullEffect : MonoBehaviour
{
    Rigidbody2D body;
    PhotonView sourceView;
    int sourceViewId;
    Vector2 fallbackSourcePosition;
    float pullAcceleration;
    float maxSpeed;
    float expiresAt;

    public void Configure(int newSourceViewId, Vector2 sourcePosition, float acceleration, float speedLimit, float duration)
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        sourceViewId = newSourceViewId;
        fallbackSourcePosition = sourcePosition;
        pullAcceleration = Mathf.Max(0f, acceleration);
        maxSpeed = Mathf.Max(0.1f, speedLimit);
        expiresAt = Mathf.Max(expiresAt, Time.time + Mathf.Max(0.05f, duration));
        ResolveSourceView();
    }

    void FixedUpdate()
    {
        if (Time.time > expiresAt)
        {
            Destroy(this);
            return;
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
            if (body == null)
                return;
        }

        ResolveSourceView();
        Vector2 sourcePosition = sourceView != null
            ? (Vector2)sourceView.transform.position
            : fallbackSourcePosition;

        Vector2 toSource = sourcePosition - body.position;
        float distance = toSource.magnitude;
        if (distance < 0.12f)
        {
            body.linearVelocity *= 0.94f;
            return;
        }

        float ramp = Mathf.Clamp01(distance / 7.5f);
        float acceleration = pullAcceleration * Mathf.Lerp(0.72f, 1.35f, ramp);
        body.linearVelocity += toSource.normalized * acceleration * Time.fixedDeltaTime;

        if (body.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            body.linearVelocity = body.linearVelocity.normalized * maxSpeed;
    }

    void ResolveSourceView()
    {
        if (sourceView != null && sourceView.ViewID == sourceViewId)
            return;

        sourceView = sourceViewId > 0 ? PhotonView.Find(sourceViewId) : null;
    }
}

[RequireComponent(typeof(PhotonView))]
public sealed class LureBeaconDecoy : MonoBehaviourPun
{
    public const string InstantiationMarker = "lure_beacon_decoy";
    public const int DefaultHp = 100;
    public const int DefaultShield = 100;

    const float VisualTargetSize = 0.9f;
    const float IdleDriftSpeedMin = 0.18f;
    const float IdleDriftSpeedMax = 0.34f;
    const float AngularVelocityMin = -12f;
    const float AngularVelocityMax = 12f;
    const float CollisionRadius = 0.38f;
    const float PulseSpeed = 3.2f;
    const float PulseScaleAmount = 0.08f;
    static readonly Color PulseBaseColor = new Color(0.92f, 0.94f, 1f, 1f);
    static readonly Color PulseGlowColor = new Color(0.62f, 0.96f, 1f, 1f);

    static readonly HashSet<LureBeaconDecoy> ActiveBeacons = new HashSet<LureBeaconDecoy>();
    static Sprite cachedSprite;

    bool initialized;
    bool isDestroyed;
    int currentHp;
    int currentShield;
    int ownerShipViewId;
    Rigidbody2D body;
    SpriteRenderer cachedRenderer;
    AudioSource loopAudioSource;
    Vector3 visualBaseScale = Vector3.one;

    public bool CanBeTargeted => initialized && !isDestroyed && currentHp > 0;
    public int OwnerShipViewId => ownerShipViewId;

    public static bool IsInstantiationData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               marker == InstantiationMarker;
    }

    public static LureBeaconDecoy EnsureAttached(GameObject target)
    {
        if (target == null)
            return null;

        LureBeaconDecoy beacon = target.GetComponent<LureBeaconDecoy>();
        if (beacon == null)
            beacon = target.AddComponent<LureBeaconDecoy>();

        beacon.InitializeFromPhotonData();
        return beacon;
    }

    public static IReadOnlyCollection<LureBeaconDecoy> GetActiveBeacons()
    {
        return new List<LureBeaconDecoy>(ActiveBeacons);
    }

    void Awake()
    {
        if (IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
    }

    void Start()
    {
        if (IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
        else
            enabled = false;
    }

    void OnEnable()
    {
        if (initialized)
            ActiveBeacons.Add(this);
    }

    void OnDisable()
    {
        StopLoopAudio();
        ActiveBeacons.Remove(this);
    }

    void OnDestroy()
    {
        StopLoopAudio();
        ActiveBeacons.Remove(this);
    }

    void Update()
    {
        if (!initialized || cachedRenderer == null || isDestroyed)
            return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed + (photonView != null ? photonView.ViewID * 0.17f : 0f));
        float scale = 1f + pulse * PulseScaleAmount;
        cachedRenderer.transform.localScale = visualBaseScale * scale;
        cachedRenderer.color = Color.Lerp(PulseBaseColor, PulseGlowColor, pulse * 0.85f);
    }

    public void InitializeFromPhotonData()
    {
        if (initialized)
            return;

        initialized = true;
        currentHp = DefaultHp;
        currentShield = DefaultShield;
        ActiveBeacons.Add(this);

        object[] data = photonView != null ? photonView.InstantiationData : null;
        ownerShipViewId = data != null && data.Length > 1 ? TryConvertToInt(data[1]) : 0;
        Vector2 initialDirection = data != null && data.Length > 3
            ? new Vector2(TryConvertToFloat(data[2]), TryConvertToFloat(data[3]))
            : Random.insideUnitCircle.normalized;

        if (initialDirection.sqrMagnitude < 0.001f)
            initialDirection = Vector2.right;

        cachedRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        ConfigureVisuals();
        ConfigurePhysics(initialDirection.normalized);
        DisablePlayerSpecificSystems();
        AudioManager.Instance.PlayBeaconSignalAt(transform.position);
        StartLoopAudio();
    }

    [PunRPC]
    public void TakeBeaconDamageAt(int damage, int attackerViewId, float impactX, float impactY)
    {
        ApplyDamage(Mathf.Max(0, damage), attackerViewId, new Vector2(impactX, impactY));
    }

    [PunRPC]
    public void TakeBeaconDamageProfileAt(int shieldDamage, int hpDamage, int attackerViewId, float impactX, float impactY)
    {
        ApplyProfileDamage(Mathf.Max(0, shieldDamage), Mathf.Max(0, hpDamage), attackerViewId, new Vector2(impactX, impactY));
    }

    [PunRPC]
    public void TakeBeaconShieldOnlyDamageAt(int shieldDamage, int attackerViewId, float impactX, float impactY)
    {
        ApplyShieldOnlyDamage(Mathf.Max(0, shieldDamage), attackerViewId, new Vector2(impactX, impactY));
    }

    void ApplyDamage(int damage, int attackerViewId, Vector2 impactPoint)
    {
        if (!PhotonNetwork.IsMasterClient || !CanBeTargeted || damage <= 0)
            return;

        int remainingDamage = damage;
        int absorbed = 0;
        if (currentShield > 0)
        {
            absorbed = Mathf.Min(currentShield, remainingDamage);
            currentShield -= absorbed;
            remainingDamage -= absorbed;
        }

        if (remainingDamage > 0)
            currentHp = Mathf.Max(0, currentHp - remainingDamage);

        if (absorbed > 0)
            photonView.RPC(nameof(PlayShieldHitFeedback), RpcTarget.All, impactPoint.x, impactPoint.y);
        else
            photonView.RPC(nameof(PlayHpHitFeedback), RpcTarget.All, impactPoint.x, impactPoint.y);

        if (currentHp <= 0)
            DestroyOnMaster();
    }

    void ApplyProfileDamage(int shieldDamage, int hpDamage, int attackerViewId, Vector2 impactPoint)
    {
        if (!PhotonNetwork.IsMasterClient || !CanBeTargeted)
            return;

        int remainingDamage = 0;
        int absorbed = 0;
        if (currentShield > 0)
        {
            if (shieldDamage > 0)
            {
                absorbed = Mathf.Min(currentShield, shieldDamage);
                currentShield -= absorbed;
                float overflowRatio = Mathf.Clamp01((shieldDamage - absorbed) / (float)shieldDamage);
                remainingDamage = Mathf.RoundToInt(hpDamage * overflowRatio);
            }
        }
        else
        {
            remainingDamage = hpDamage;
        }

        if (remainingDamage > 0)
            currentHp = Mathf.Max(0, currentHp - remainingDamage);

        if (absorbed <= 0 && remainingDamage <= 0)
            return;

        if (absorbed > 0)
            photonView.RPC(nameof(PlayShieldHitFeedback), RpcTarget.All, impactPoint.x, impactPoint.y);
        else
            photonView.RPC(nameof(PlayHpHitFeedback), RpcTarget.All, impactPoint.x, impactPoint.y);

        if (currentHp <= 0)
            DestroyOnMaster();
    }

    void ApplyShieldOnlyDamage(int damage, int attackerViewId, Vector2 impactPoint)
    {
        if (!PhotonNetwork.IsMasterClient || !CanBeTargeted || damage <= 0 || currentShield <= 0)
            return;

        int absorbed = Mathf.Min(currentShield, damage);
        currentShield -= absorbed;
        photonView.RPC(nameof(PlayShieldHitFeedback), RpcTarget.All, impactPoint.x, impactPoint.y);
    }

    [PunRPC]
    void PlayShieldHitFeedback(float x, float y)
    {
        AudioManager.Instance.PlayShieldHitAt(new Vector3(x, y, transform.position.z));
    }

    [PunRPC]
    void PlayHpHitFeedback(float x, float y)
    {
        AudioManager.Instance.PlayHpHitAt(new Vector3(x, y, transform.position.z));
    }

    [PunRPC]
    void PlayDestroyedFeedback()
    {
        AudioManager.Instance.PlayExplosionAt(transform.position);
    }

    void DestroyOnMaster()
    {
        if (isDestroyed)
            return;

        isDestroyed = true;
        StopLoopAudio();
        photonView.RPC(nameof(PlayDestroyedFeedback), RpcTarget.All);
        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    void ConfigureVisuals()
    {
        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        if (cachedRenderer == null)
            return;

        DisableExtraSpriteRenderers();

        Sprite sprite = LoadBeaconSprite();
        if (sprite != null)
        {
            cachedRenderer.sprite = sprite;
            cachedRenderer.color = Color.white;
            FitRendererToTargetSize(cachedRenderer, VisualTargetSize);
            visualBaseScale = cachedRenderer.transform.localScale;
        }

        cachedRenderer.sortingOrder = Mathf.Max(cachedRenderer.sortingOrder, 38);
    }

    void StartLoopAudio()
    {
        AudioClip clip = AudioManager.Instance != null ? AudioManager.Instance.BeaconSignalClip : null;
        if (clip == null)
            return;

        if (loopAudioSource == null)
        {
            Transform existing = transform.Find("LureBeaconLoopAudio");
            GameObject sourceObject = existing != null ? existing.gameObject : new GameObject("LureBeaconLoopAudio");
            sourceObject.transform.SetParent(transform, false);
            loopAudioSource = sourceObject.GetComponent<AudioSource>();
            if (loopAudioSource == null)
                loopAudioSource = sourceObject.AddComponent<AudioSource>();
        }

        if (loopAudioSource == null)
            return;

        AudioManager.Instance.ConfigureSpatialSource(loopAudioSource, 0.52f);
        loopAudioSource.clip = clip;
        loopAudioSource.loop = true;

        if (!loopAudioSource.isPlaying)
            loopAudioSource.Play();
    }

    void StopLoopAudio()
    {
        if (loopAudioSource != null && loopAudioSource.isPlaying)
            loopAudioSource.Stop();
    }

    void ConfigurePhysics(Vector2 initialDirection)
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (body != null)
        {
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = 1.35f;
            body.linearDamping = 0.42f;
            body.angularDamping = 0.78f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = initialDirection * Random.Range(IdleDriftSpeedMin, IdleDriftSpeedMax);
            body.angularVelocity = Random.Range(AngularVelocityMin, AngularVelocityMax);
        }

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null)
            circle = gameObject.AddComponent<CircleCollider2D>();

        circle.isTrigger = false;
        SetWorldRadius(circle, CollisionRadius);
    }

    void DisablePlayerSpecificSystems()
    {
        DisableComponent<PlayerMovement>();
        DisableComponent<PlayerShooting>();
        DisableComponent<TreasureCollector>();
        DisableComponent<PlayerRepairDocking>();
        DisableComponent<HealthBarUI>();
        DisableComponent<ShieldBarUI>();
        DisableComponent<PlayerNicknameUI>();
        DisableComponent<BoosterBarUI>();
        DisableComponent<ShipInventoryHudUI>();
        DisableComponent<StartingShipEntryVfx>();
        DisableComponent<SpawnInvulnerabilityVfx>();
        DisableComponent<AstronautSurvivor>();
        DisableComponent<PlayerHealth>();

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            Destroy(thruster);
    }

    void DisableExtraSpriteRenderers()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer == cachedRenderer)
                continue;

            renderer.enabled = false;
        }
    }

    void DisableComponent<T>() where T : Behaviour
    {
        T component = GetComponent<T>();
        if (component != null && component != this)
            component.enabled = false;
    }

    static Sprite LoadBeaconSprite()
    {
        if (cachedSprite != null)
            return cachedSprite;

        cachedSprite = Resources.Load<Sprite>("lure_beacon_onmap_resource");
        if (cachedSprite != null)
            return cachedSprite;

#if UNITY_EDITOR
        cachedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/lure_beacon_onmap.png");
#endif
        return cachedSprite;
    }

    static void FitRendererToTargetSize(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds bounds = renderer.sprite.bounds;
        float maxExtent = Mathf.Max(bounds.size.x, bounds.size.y);
        if (maxExtent <= 0.0001f)
            return;

        float scale = targetSize / maxExtent;
        renderer.transform.localScale = Vector3.one * scale;
    }

    static void SetWorldRadius(CircleCollider2D circle, float worldRadius)
    {
        if (circle == null)
            return;

        Vector3 scale = circle.transform.lossyScale;
        float safeScaleX = Mathf.Abs(scale.x) > 0.0001f ? Mathf.Abs(scale.x) : 1f;
        float safeScaleY = Mathf.Abs(scale.y) > 0.0001f ? Mathf.Abs(scale.y) : 1f;
        circle.radius = worldRadius / Mathf.Max(safeScaleX, safeScaleY);
    }

    static int TryConvertToInt(object value)
    {
        if (value is int intValue)
            return intValue;
        if (value is byte byteValue)
            return byteValue;
        if (value is short shortValue)
            return shortValue;
        if (value is long longValue)
            return (int)longValue;
        if (value is float floatValue)
            return Mathf.RoundToInt(floatValue);
        if (value is double doubleValue)
            return Mathf.RoundToInt((float)doubleValue);

        int.TryParse(value != null ? value.ToString() : string.Empty, out int parsed);
        return parsed;
    }

    static float TryConvertToFloat(object value)
    {
        if (value is float floatValue)
            return floatValue;
        if (value is double doubleValue)
            return (float)doubleValue;
        if (value is int intValue)
            return intValue;
        if (value is long longValue)
            return longValue;

        float.TryParse(value != null ? value.ToString() : string.Empty, out float parsed);
        return parsed;
    }
}

public sealed class DeathMessageAutoHide : MonoBehaviour
{
    Coroutine hideRoutine;

    public void ShowFor(float seconds)
    {
        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        gameObject.SetActive(true);
        hideRoutine = StartCoroutine(HideAfterDelay(Mathf.Max(0.1f, seconds)));
    }

    IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        gameObject.SetActive(false);
        hideRoutine = null;
    }
}

[RequireComponent(typeof(PlayerHealth))]
public class ShieldBarUI : MonoBehaviourPun
{
    const string ShieldBarName = "Shield_Bar";
    const string ShieldLabelName = "ShieldLabel";
    const string ShieldValueName = "ShieldValue";
    const float BarWidth = 440f;
    const float BarHeight = 36f;
    const float VerticalSpacing = 18f;

    PlayerHealth health;
    Slider shieldBar;
    RectTransform shieldRect;
    Image fillImage;
    Image backgroundImage;
    Image handleImage;
    TMPro.TextMeshProUGUI labelText;
    TMPro.TextMeshProUGUI valueText;
    bool isVisible = true;

    void Start()
    {
        health = GetComponent<PlayerHealth>();

        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        CreateShieldBar();
        RefreshBar();
    }

    void Update()
    {
        if (shieldBar == null)
        {
            CreateShieldBar();
            RefreshBar();
        }

        UpdateVisibility();
        RefreshBar();
    }

    void OnDestroy()
    {
        if (shieldBar != null)
            Destroy(shieldBar.gameObject);
    }

    void CreateShieldBar()
    {
        GameObject existingBar = GameObject.Find(ShieldBarName);
        if (existingBar != null)
            Destroy(existingBar);

        GameObject hpBarObject = GameObject.Find("HP_Bar");
        if (hpBarObject == null)
            return;

        GameObject clone = Object.Instantiate(hpBarObject, hpBarObject.transform.parent);
        clone.name = ShieldBarName;

        RectTransform hpRect = hpBarObject.GetComponent<RectTransform>();
        shieldRect = clone.GetComponent<RectTransform>();
        ApplyLayout(hpRect);

        shieldBar = clone.GetComponent<Slider>();
        shieldBar.minValue = 0f;
        shieldBar.maxValue = health != null ? health.MaxShield : 50f;
        shieldBar.wholeNumbers = false;

        backgroundImage = FindImage(clone.transform, "Background");
        fillImage = FindImage(clone.transform, "Fill");
        handleImage = FindImage(clone.transform, "Handle");
        HideHandle();
        DestroyIfExists(clone.transform, "HealthLabel");
        DestroyIfExists(clone.transform, "HealthValue");
        DestroyIfExists(clone.transform, ShieldLabelName);
        DestroyIfExists(clone.transform, ShieldValueName);

        if (backgroundImage != null)
            backgroundImage.color = new Color(0.05f, 0.08f, 0.16f, 0.95f);

        if (fillImage != null)
            fillImage.color = new Color(0.24f, 0.62f, 1f, 1f);

        labelText = CreateText(clone.transform, ShieldLabelName, new Vector2(12f, 0f), TMPro.TextAlignmentOptions.Left, "SHIELD");
        valueText = CreateText(clone.transform, ShieldValueName, new Vector2(-12f, 0f), TMPro.TextAlignmentOptions.Right, string.Empty);
    }

    void RefreshBar()
    {
        if (health == null || shieldBar == null)
            return;

        ApplyLayout();

        shieldBar.maxValue = health.MaxShield;
        shieldBar.value = health.CurrentShield;

        if (valueText != null)
            valueText.text = health.CurrentShield + " / " + health.MaxShield;

        float normalized = shieldBar.maxValue > 0f ? shieldBar.value / shieldBar.maxValue : 0f;
        if (fillImage != null)
            fillImage.color = Color.Lerp(new Color(0.08f, 0.18f, 0.55f, 1f), new Color(0.36f, 0.78f, 1f, 1f), normalized);

        HideHandle();
    }

    void UpdateVisibility()
    {
        if (shieldBar == null)
            return;

        bool shouldBeVisible = health != null &&
                               health.MaxShield > 0 &&
                               PhotonNetwork.CurrentRoom != null &&
                               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
                               value is bool started &&
                               GameplayHudVisibility.IsGameplayHudVisible(started);
        if (isVisible == shouldBeVisible)
            return;

        isVisible = shouldBeVisible;
        shieldBar.gameObject.SetActive(shouldBeVisible);
    }

    void ApplyLayout()
    {
        if (shieldRect == null)
            return;

        GameObject hpBarObject = GameObject.Find("HP_Bar");
        RectTransform hpRect = hpBarObject != null ? hpBarObject.GetComponent<RectTransform>() : null;
        ApplyLayout(hpRect);
    }

    void ApplyLayout(RectTransform hpRect)
    {
        if (shieldRect == null || hpRect == null)
            return;

        shieldRect.sizeDelta = new Vector2(BarWidth, BarHeight);
        shieldRect.anchoredPosition = hpRect.anchoredPosition + new Vector2(0f, -VerticalSpacing);
    }

    TMPro.TextMeshProUGUI CreateText(Transform parent, string objectName, Vector2 anchoredPosition, TMPro.TextAlignmentOptions alignment, string initialText)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = anchoredPosition;

        TMPro.TextMeshProUGUI text = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
        text.text = initialText;
        text.fontSize = 14f;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        TMPro.TMP_Text referenceText = Object.FindAnyObjectByType<TMPro.TMP_Text>();
        if (referenceText != null)
        {
            text.font = referenceText.font;
            text.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        return text;
    }

    void DestroyIfExists(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            Destroy(existing.gameObject);
    }

    Image FindImage(Transform root, string objectName)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject.name == objectName)
                return image;
        }

        return null;
    }

    void HideHandle()
    {
        if (handleImage == null)
            return;

        handleImage.enabled = false;
        handleImage.raycastTarget = false;
    }
}

[RequireComponent(typeof(PhotonView))]
public class AstronautSurvivor : MonoBehaviourPun
{
    public const string AstronautInstantiationMarker = "astronaut_survivor";
    public const string EnemyAstronautInstantiationMarker = "enemy_astronaut_survivor";
    const float AstronautTargetSize = 0.56f;
    const float EnemyAstronautTargetSize = AstronautTargetSize * 0.9f;
    const float EscapePodTargetSize = 0.75f;
    const int EnemyAstronautHp = 14;
    const float PlayerAstronautReferenceMovementSpeed = 5f;
    const float PlayerAstronautSpeedDivisor = 3f;
    const float PlayerAstronautMinimumSpeed = 1.2f;
    static float EnemyAstronautSpeed => ResolvePlayerAstronautSpeed();
    const float EnemyAstronautArrivalDistance = 0.58f;
    const float EnemyAstronautAvoidPlayerRadius = 4.8f;
    const float EnemyAstronautAvoidPlayerWeight = 1.35f;
    const float EnemyAstronautAvoidObjectRadius = 1.35f;
    const float EnemyAstronautAvoidObjectWeight = 1.9f;
    const float EnemyAstronautMapEdgeMargin = 2.2f;
    const float EnemyAstronautEvacuationDuration = 0.85f;
    const float EnemyAstronautTargetJitterRadius = 0.62f;
    const float EnemyAstronautSpawnClearanceRadius = 0.28f;
    const float EnemyAstronautSpawnChancePerSlot = 0.5f;
    const float EnemyAstronautPhysicsMass = 0.035f;
    const string EnemyAstronautSpriteResourcePath = "Astronauts/enemy_astronauts_multiasset";
    const int EscapePodHpBonus = 50;
    const float EscapePodSpeedMultiplier = 3f;
    const float EmergencySuitBeaconProtectionDuration = 3f;
    const float EmergencySuitBeaconSpeedDuration = 5f;
    const float EmergencySuitBeaconSpeedMultiplier = 1.35f;

    static Sprite cachedAstronautSprite;
    static Sprite cachedEscapePodSprite;
    static Sprite[] cachedEnemyAstronautSprites;
    bool initialized;
    bool isEscapePod;
    bool isEnemySurvivor;
    bool hasEnemyTarget;
    bool enemyEvacuationStarted;
    int enemyVariantIndex;
    Vector2 enemyTargetPosition;
    SpriteRenderer cachedRenderer;
    Rigidbody2D cachedBody;
    Coroutine emergencySuitBeaconSpeedRoutine;
    Coroutine enemyEvacuationRoutine;

    public bool IsEscapePodMode => isEscapePod;
    public bool IsEnemySurvivor => isEnemySurvivor;
    public int EnemyVariantIndex => enemyVariantIndex;
    public float VisualTargetSize => isEnemySurvivor ? EnemyAstronautTargetSize : isEscapePod ? EscapePodTargetSize : AstronautTargetSize;

    public static bool IsAstronautInstantiationData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               (marker == AstronautInstantiationMarker || marker == EnemyAstronautInstantiationMarker);
    }

    public static bool IsEnemyAstronautInstantiationData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               marker == EnemyAstronautInstantiationMarker;
    }

    public static bool IsEnemyAstronaut(PlayerHealth health)
    {
        if (health == null)
            return false;

        AstronautSurvivor survivor = health.GetComponent<AstronautSurvivor>();
        if (survivor != null && survivor.IsEnemySurvivor)
            return true;

        PhotonView view = health.photonView != null ? health.photonView : health.GetComponent<PhotonView>();
        return IsEnemyAstronautInstantiationData(view != null ? view.InstantiationData : null);
    }

    public static bool IsEscapePodInstantiationData(object[] data)
    {
        return IsAstronautInstantiationData(data) && HasEscapePod(data);
    }

    public Sprite GetVisualSprite()
    {
        if (isEnemySurvivor)
            return LoadEnemyAstronautSprite(enemyVariantIndex);

        return isEscapePod ? LoadEscapePodSprite() : LoadAstronautSprite();
    }

    public void InitializeFromPhotonData()
    {
        if (initialized)
            return;

        initialized = true;
        object[] instantiationData = photonView != null ? photonView.InstantiationData : null;
        isEnemySurvivor = IsEnemyAstronautInstantiationData(instantiationData);
        bool hasEmergencySuitBeacon = !isEnemySurvivor && HasEmergencySuitBeacon(instantiationData);
        isEscapePod = !isEnemySurvivor && HasEscapePod(instantiationData);
        ResolveEnemyAstronautData(instantiationData);
        ActorIdentity.Ensure(gameObject);

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        cachedRenderer = renderer;
        if (renderer != null)
        {
            Sprite visualSprite = GetVisualSprite();
            if (visualSprite != null)
            {
                renderer.sprite = visualSprite;
                renderer.color = Color.white;
                FitRendererToTargetSize(renderer, VisualTargetSize);
            }
        }

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            if (isEnemySurvivor)
            {
                movement.StopEngineAudioImmediately();
                movement.enabled = false;
            }
            else
            {
                float astronautSpeed = Mathf.Max(PlayerAstronautMinimumSpeed, movement.speed / PlayerAstronautSpeedDivisor);
                float survivorSpeed = isEscapePod ? astronautSpeed * EscapePodSpeedMultiplier : astronautSpeed;
                movement.speed = survivorSpeed;
                movement.boosterDuration = 9999f;

                if (hasEmergencySuitBeacon)
                    emergencySuitBeaconSpeedRoutine = StartCoroutine(EmergencySuitBeaconSpeedRoutine(movement, survivorSpeed));
            }
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        if (isEnemySurvivor)
        {
            TreasureCollector collector = GetComponent<TreasureCollector>();
            if (collector != null)
                collector.DisableForEnemyAstronaut();
        }

        ShipInventoryHudUI cargoHud = GetComponent<ShipInventoryHudUI>();
        if (cargoHud != null)
            cargoHud.enabled = false;

        BoosterBarUI boosterUi = GetComponent<BoosterBarUI>();
        if (boosterUi != null)
            boosterUi.enabled = false;

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (isEnemySurvivor && thruster == null)
            thruster = gameObject.AddComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.RefreshMode();

        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            int astronautHp = isEnemySurvivor
                ? EnemyAstronautHp
                : PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.JakeId) ? 60 : 20;
            if (!isEnemySurvivor && isEscapePod)
                astronautHp += EscapePodHpBonus;

            health.ConfigureBaseStats(astronautHp, 0);
            if (hasEmergencySuitBeacon)
                health.BeginEquipmentInvulnerabilityLocal(EmergencySuitBeaconProtectionDuration);
        }

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        cachedBody = body;
        if (body != null)
        {
            body.mass = isEnemySurvivor ? EnemyAstronautPhysicsMass : 0.01f;
            body.linearDamping = isEnemySurvivor ? 1.85f : 0.9f;
            body.angularDamping = isEnemySurvivor ? 4f : 1f;
            if (isEnemySurvivor)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.simulated = true;
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }

        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null && renderer != null)
        {
            Vector2 spriteSize = renderer.bounds.size;
            Vector2 colliderFactor = isEscapePod ? new Vector2(0.52f, 0.68f) : new Vector2(0.42f, 0.58f);
            SetWorldBoxSize(boxCollider, new Vector2(spriteSize.x * colliderFactor.x, spriteSize.y * colliderFactor.y));
        }

        CircleCollider2D triggerCollider = GetComponent<CircleCollider2D>();
        if (triggerCollider != null)
            triggerCollider.enabled = false;

    }

    void ResolveEnemyAstronautData(object[] data)
    {
        enemyVariantIndex = 0;
        hasEnemyTarget = false;
        enemyTargetPosition = transform.position;

        if (!isEnemySurvivor)
            return;

        if (data != null && data.Length > 1 && TryConvertToInt(data[1], out int variant))
            enemyVariantIndex = Mathf.Abs(variant) % Mathf.Max(1, GetEnemyAstronautSpriteCount());

        if (data != null &&
            data.Length > 3 &&
            TryConvertToFloat(data[2], out float targetX) &&
            TryConvertToFloat(data[3], out float targetY))
        {
            enemyTargetPosition = new Vector2(targetX, targetY);
            hasEnemyTarget = true;
        }
    }

    void FixedUpdate()
    {
        if (!initialized || !isEnemySurvivor || enemyEvacuationStarted)
            return;

        if (PhotonNetwork.InRoom && photonView != null && !photonView.IsMine)
            return;

        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null && (health.IsWreck || health.IsEvacuationAnimating || health.CurrentHP <= 0))
            return;

        if (cachedBody == null)
            cachedBody = GetComponent<Rigidbody2D>();

        if (!hasEnemyTarget)
        {
            enemyTargetPosition = ResolveFallbackEnemyAstronautTarget(transform.position, enemyVariantIndex);
            hasEnemyTarget = true;
        }

        Vector2 currentPosition = cachedBody != null ? cachedBody.position : (Vector2)transform.position;
        if (Vector2.Distance(currentPosition, enemyTargetPosition) <= EnemyAstronautArrivalDistance)
        {
            RequestEnemyAstronautEvacuation(enemyTargetPosition);
            return;
        }

        Vector2 moveDirection = ResolveEnemyAstronautMoveDirection(currentPosition);
        if (moveDirection.sqrMagnitude <= 0.001f)
            return;

        Vector2 desiredVelocity = moveDirection.normalized * EnemyAstronautSpeed;
        if (cachedBody != null)
        {
            cachedBody.linearVelocity = desiredVelocity;
        }
        else
        {
            transform.position += (Vector3)(desiredVelocity * Time.fixedDeltaTime);
        }

        float targetAngle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg + 90f;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, 0f, targetAngle), 420f * Time.fixedDeltaTime);
    }

    Vector2 ResolveEnemyAstronautMoveDirection(Vector2 currentPosition)
    {
        Vector2 toTarget = enemyTargetPosition - currentPosition;
        Vector2 desired = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector2.up;
        Vector2 playerAvoidance = Vector2.zero;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player == null || player == GetComponent<PlayerHealth>() || player.IsWreck || player.IsBotControlled || player.IsNeutralRiderControlled || player.IsAstronautControlled || player.IsEvacuationAnimating)
                continue;

            Vector2 away = currentPosition - (Vector2)player.transform.position;
            float distance = away.magnitude;
            if (distance <= 0.001f || distance > EnemyAstronautAvoidPlayerRadius)
                continue;

            float strength = Mathf.Clamp01((EnemyAstronautAvoidPlayerRadius - distance) / EnemyAstronautAvoidPlayerRadius);
            playerAvoidance += away.normalized * strength;
        }

        Vector2 objectAvoidance = ResolveEnemyAstronautObjectAvoidance(currentPosition, desired);
        Vector2 result = playerAvoidance.sqrMagnitude > 0.001f
            ? (desired + playerAvoidance.normalized * EnemyAstronautAvoidPlayerWeight).normalized
            : desired;
        result = ApplyEnemyAstronautObjectAvoidance(result, desired, objectAvoidance);

        return ApplyEnemyAstronautMapEdgeSteering(currentPosition, result);
    }

    Vector2 ResolveEnemyAstronautObjectAvoidance(Vector2 currentPosition, Vector2 desired)
    {
        Vector2 avoidance = Vector2.zero;
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(currentPosition, EnemyAstronautAvoidObjectRadius, out Collider2D[] hits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger || hit.attachedRigidbody == cachedBody)
                continue;

            if (!IsEnemyAstronautAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(currentPosition);
            Vector2 toObject = closest - currentPosition;
            if (toObject.sqrMagnitude > 0.0001f && Vector2.Dot(toObject.normalized, desired) < -0.35f)
                continue;

            Vector2 away = currentPosition - closest;
            if (away.sqrMagnitude <= 0.0001f)
                away = currentPosition - (Vector2)hit.transform.position;

            if (away.sqrMagnitude <= 0.0001f)
                continue;

            float distance = Mathf.Max(0.08f, away.magnitude);
            float strength = Mathf.Clamp01((EnemyAstronautAvoidObjectRadius - distance) / EnemyAstronautAvoidObjectRadius);
            if (toObject.sqrMagnitude > 0.0001f)
                strength *= Mathf.Lerp(0.75f, 1.3f, Mathf.Clamp01(Vector2.Dot(toObject.normalized, desired)));

            avoidance += away.normalized * strength;
        }

        return avoidance;
    }

    Vector2 ApplyEnemyAstronautObjectAvoidance(Vector2 currentDirection, Vector2 desired, Vector2 objectAvoidance)
    {
        if (objectAvoidance.sqrMagnitude <= 0.001f)
            return currentDirection;

        Vector2 away = objectAvoidance.normalized;
        Vector2 result = (currentDirection.normalized + away * EnemyAstronautAvoidObjectWeight).normalized;
        if (Vector2.Dot(result, desired) >= 0.12f)
            return result;

        Vector2 tangent = new Vector2(-desired.y, desired.x);
        if (Vector2.Dot(tangent, away) < 0f)
            tangent = -tangent;

        return (desired * 0.38f + tangent.normalized * 0.62f).normalized;
    }

    static bool IsEnemyAstronautAvoidedObject(Collider2D hit)
    {
        if (hit == null)
            return false;

        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        if (hit.GetComponentInParent<DroppedCargoCrate>() != null)
            return true;

        return hit.GetComponentInParent<MovingSpaceObject>() != null;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        DeflectEnemyAstronautFromLooseObject(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        DeflectEnemyAstronautFromLooseObject(collision);
    }

    void DeflectEnemyAstronautFromLooseObject(Collision2D collision)
    {
        if (!initialized || !isEnemySurvivor || enemyEvacuationStarted || collision == null)
            return;

        if (PhotonNetwork.InRoom && photonView != null && !photonView.IsMine)
            return;

        Collider2D hit = collision.collider;
        if (!IsEnemyAstronautAvoidedObject(hit))
            return;

        if (cachedBody == null)
            cachedBody = GetComponent<Rigidbody2D>();
        if (cachedBody == null)
            return;

        Vector2 position = cachedBody.position;
        Vector2 away = position - hit.ClosestPoint(position);
        if (away.sqrMagnitude <= 0.0001f)
            away = position - (Vector2)hit.transform.position;
        if (away.sqrMagnitude <= 0.0001f)
            return;

        Vector2 toTarget = enemyTargetPosition - position;
        Vector2 desired = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : cachedBody.linearVelocity.normalized;
        if (desired.sqrMagnitude <= 0.001f)
            desired = Vector2.up;

        Vector2 awayNormal = away.normalized;
        Vector2 tangent = new Vector2(-awayNormal.y, awayNormal.x);
        if (Vector2.Dot(tangent, desired) < 0f)
            tangent = -tangent;

        cachedBody.linearVelocity = (desired * 0.32f + tangent.normalized * 0.68f).normalized * EnemyAstronautSpeed;
    }

    Vector2 ApplyEnemyAstronautMapEdgeSteering(Vector2 currentPosition, Vector2 desiredDirection)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        if (mapSize.x <= 0f || mapSize.y <= 0f)
            return desiredDirection;

        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - EnemyAstronautMapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - EnemyAstronautMapEdgeMargin);
        Vector2 predicted = currentPosition + desiredDirection.normalized * 1.5f;
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + 1f, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - 1f, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + 1f, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - 1f, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desiredDirection.normalized * 0.58f + inward.normalized * 0.42f).normalized;
    }

    void RequestEnemyAstronautEvacuation(Vector2 target)
    {
        if (enemyEvacuationStarted)
            return;

        enemyEvacuationStarted = true;
        if (photonView != null)
            photonView.RPC(nameof(BeginEnemyAstronautEvacuationRpc), RpcTarget.All, target.x, target.y);
        else
            StartEnemyAstronautEvacuation(target);
    }

    [PunRPC]
    void BeginEnemyAstronautEvacuationRpc(float targetX, float targetY)
    {
        if (!initialized)
            InitializeFromPhotonData();

        if (!isEnemySurvivor)
            return;

        StartEnemyAstronautEvacuation(new Vector2(targetX, targetY));
    }

    void StartEnemyAstronautEvacuation(Vector2 target)
    {
        if (enemyEvacuationRoutine != null)
            return;

        enemyEvacuationStarted = true;
        enemyEvacuationRoutine = StartCoroutine(EnemyAstronautEvacuationRoutine(target));
    }

    IEnumerator EnemyAstronautEvacuationRoutine(Vector2 target)
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        if (cachedBody == null)
            cachedBody = GetComponent<Rigidbody2D>();

        if (cachedBody != null)
        {
            cachedBody.linearVelocity = Vector2.zero;
            cachedBody.angularVelocity = 0f;
            cachedBody.simulated = false;
        }

        Vector3 startPosition = transform.position;
        Vector3 endPosition = new Vector3(target.x, target.y, startPosition.z);
        Vector3 startScale = transform.localScale;
        Vector3 endScale = new Vector3(
            Mathf.Sign(startScale.x) * 0.01f,
            Mathf.Sign(startScale.y) * 0.01f,
            startScale.z);
        float elapsed = 0f;
        while (elapsed < EnemyAstronautEvacuationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / EnemyAstronautEvacuationDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.Lerp(startPosition, endPosition, eased);
            transform.localScale = Vector3.Lerp(startScale, endScale, eased);
            yield return null;
        }

        transform.position = endPosition;
        transform.localScale = endScale;

        if (photonView != null && photonView.IsMine)
        {
            if (PhotonNetwork.InRoom)
                PhotonNetwork.Destroy(gameObject);
            else
                Destroy(gameObject);
        }
    }

    public static void SpawnEnemySurvivorsFrom(EnemyBot sourceBot)
    {
        if (!PhotonNetwork.IsMasterClient || sourceBot == null || !PhotonNetwork.InRoom)
            return;

        int maxCount = GetEnemySurvivorCount(sourceBot.Kind);
        if (maxCount <= 0)
            return;

        Vector2 origin = sourceBot.transform.position;
        Vector2[] targets = ResolveEnemySurvivorTargets(origin, maxCount);
        for (int i = 0; i < maxCount; i++)
        {
            if (Random.value > EnemyAstronautSpawnChancePerSlot)
                continue;

            Vector2 target = targets != null && i < targets.Length
                ? targets[i]
                : ResolveFallbackEnemyAstronautTarget(origin, i);
            Vector2 spawn = ResolveEnemySurvivorSpawnPosition(sourceBot, origin, i);
            int variantIndex = ResolveEnemyAstronautVariant(sourceBot.Kind, i);
            GameObject astronaut = PhotonNetwork.Instantiate(
                "Player",
                new Vector3(spawn.x, spawn.y, 0f),
                Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)),
                0,
                new object[] { EnemyAstronautInstantiationMarker, variantIndex, target.x, target.y, (int)sourceBot.Kind });

            if (astronaut == null)
                continue;

            AstronautSurvivor survivor = astronaut.GetComponent<AstronautSurvivor>();
            if (survivor == null)
                survivor = astronaut.AddComponent<AstronautSurvivor>();
            survivor.InitializeFromPhotonData();

            Rigidbody2D body = astronaut.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                Vector2 outward = spawn - origin;
                if (outward.sqrMagnitude <= 0.001f)
                    outward = Random.insideUnitCircle;
                if (outward.sqrMagnitude <= 0.001f)
                    outward = Vector2.up;

                body.linearVelocity = outward.normalized * Random.Range(0.45f, 0.95f);
            }
        }
    }

    public static int GetEnemySurvivorCount(EnemyBotKind kind)
    {
        switch (kind)
        {
            case EnemyBotKind.Drone:
            case EnemyBotKind.SpaceMine:
            case EnemyBotKind.SpaceManta:
            case EnemyBotKind.GravitySquid:
            case EnemyBotKind.CosmicWorm:
                return 0;
            case EnemyBotKind.NeutralFighter:
            case EnemyBotKind.PirateFighter:
            case EnemyBotKind.PirateFighterElite:
            case EnemyBotKind.PirateFighterAce:
            case EnemyBotKind.HunterLance:
                return 1;
            case EnemyBotKind.Mothership:
            case EnemyBotKind.PirateBase:
                return 3;
            default:
                return 2;
        }
    }

    static int ResolveEnemyAstronautVariant(EnemyBotKind kind, int ordinal)
    {
        if (EnemyBot.IsPirateFighterKind(kind) || kind == EnemyBotKind.PirateBase || kind == EnemyBotKind.Corsair)
            return 3;

        switch (kind)
        {
            case EnemyBotKind.NeutralFighter:
                return ordinal % 2 == 0 ? 2 : 0;
            case EnemyBotKind.RescueShip:
                return 1;
            case EnemyBotKind.RadarShip:
                return ordinal % 2 == 0 ? 4 : 6;
            case EnemyBotKind.SpaceTruck:
            case EnemyBotKind.ContainerShip:
                return ordinal % 2 == 0 ? 5 : 7;
            case EnemyBotKind.Mothership:
                return ordinal == 0 ? 8 : ordinal == 1 ? 4 : 5;
            case EnemyBotKind.HunterLance:
                return 8;
            default:
                return Mathf.Abs(((int)kind * 3) + ordinal) % Mathf.Max(1, GetEnemyAstronautSpriteCount());
        }
    }

    static float ResolvePlayerAstronautSpeed()
    {
        return Mathf.Max(PlayerAstronautMinimumSpeed, PlayerAstronautReferenceMovementSpeed / PlayerAstronautSpeedDivisor);
    }

    static Vector2[] ResolveEnemySurvivorTargets(Vector2 origin, int count)
    {
        List<Vector2> destinations = CollectEnemyAstronautDestinations();
        destinations.Sort((a, b) => Vector2.Distance(origin, a).CompareTo(Vector2.Distance(origin, b)));

        Vector2[] result = new Vector2[Mathf.Max(0, count)];
        for (int i = 0; i < result.Length; i++)
        {
            Vector2 baseTarget = destinations.Count > 0
                ? destinations[i % destinations.Count]
                : ResolveFallbackEnemyAstronautTarget(origin, i);
            result[i] = baseTarget + ResolveEnemyAstronautTargetJitter(i, destinations.Count > 1);
        }

        return result;
    }

    static List<Vector2> CollectEnemyAstronautDestinations()
    {
        List<Vector2> destinations = new List<Vector2>();

        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null)
                AddUniqueDestination(destinations, zones[i].transform.position);
        }

        ScienceStation[] scienceStations = FindObjectsByType<ScienceStation>(FindObjectsInactive.Exclude);
        for (int i = 0; i < scienceStations.Length; i++)
        {
            if (scienceStations[i] != null)
                AddUniqueDestination(destinations, scienceStations[i].transform.position);
        }

        RepairBay[] repairBays = FindObjectsByType<RepairBay>(FindObjectsInactive.Exclude);
        for (int i = 0; i < repairBays.Length; i++)
        {
            if (repairBays[i] != null)
                AddUniqueDestination(destinations, repairBays[i].transform.position);
        }

        SpaceFactory[] factories = FindObjectsByType<SpaceFactory>(FindObjectsInactive.Exclude);
        for (int i = 0; i < factories.Length; i++)
        {
            if (factories[i] != null)
                AddUniqueDestination(destinations, factories[i].transform.position);
        }

        return destinations;
    }

    static void AddUniqueDestination(List<Vector2> destinations, Vector2 position)
    {
        for (int i = 0; i < destinations.Count; i++)
        {
            if (Vector2.SqrMagnitude(destinations[i] - position) <= 0.64f)
                return;
        }

        destinations.Add(position);
    }

    static Vector2 ResolveEnemyAstronautTargetJitter(int ordinal, bool keepSmall)
    {
        float radius = keepSmall ? EnemyAstronautTargetJitterRadius * 0.45f : EnemyAstronautTargetJitterRadius;
        float angle = (ordinal * 137.50776f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    static Vector2 ResolveFallbackEnemyAstronautTarget(Vector2 origin, int ordinal)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        if (mapSize.x <= 0f || mapSize.y <= 0f)
            mapSize = new Vector2(64f, 48f);

        Vector2 direction = origin.sqrMagnitude > 0.001f ? origin.normalized : Vector2.up;
        float angle = Mathf.Atan2(direction.y, direction.x) + ordinal * 2.39996f;
        direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;

        float halfX = Mathf.Max(4f, mapSize.x * 0.5f - EnemyAstronautMapEdgeMargin);
        float halfY = Mathf.Max(4f, mapSize.y * 0.5f - EnemyAstronautMapEdgeMargin);
        return new Vector2(direction.x * halfX, direction.y * halfY);
    }

    static Vector2 ResolveEnemySurvivorSpawnPosition(EnemyBot sourceBot, Vector2 origin, int ordinal)
    {
        float baseRadius = Mathf.Clamp(sourceBot != null ? sourceBot.VisualTargetSize * 0.22f : 0.6f, 0.52f, 1.75f);
        for (int i = 0; i < 14; i++)
        {
            float angle = ((ordinal * 79f) + (i * 137.50776f)) * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (baseRadius + i * 0.08f);
            Vector2 candidate = origin + offset;
            if (IsEnemyAstronautSpawnFree(candidate, sourceBot != null ? sourceBot.transform : null))
                return candidate;
        }

        return origin + Random.insideUnitCircle.normalized * baseRadius;
    }

    static bool IsEnemyAstronautSpawnFree(Vector2 candidate, Transform sourceTransform)
    {
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(candidate, EnemyAstronautSpawnClearanceRadius, out Collider2D[] hits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (sourceTransform != null && (hit.transform == sourceTransform || hit.transform.IsChildOf(sourceTransform)))
                continue;

            return false;
        }

        return true;
    }

    static bool HasEmergencySuitBeacon(object[] data)
    {
        return data != null &&
               data.Length > 1 &&
               data[1] is bool enabled &&
               enabled;
    }

    static bool HasEscapePod(object[] data)
    {
        return data != null &&
               data.Length > 2 &&
               data[2] is bool enabled &&
               enabled;
    }

    static bool TryConvertToInt(object value, out int result)
    {
        if (value is int intValue)
        {
            result = intValue;
            return true;
        }

        if (value is short shortValue)
        {
            result = shortValue;
            return true;
        }

        if (value is byte byteValue)
        {
            result = byteValue;
            return true;
        }

        if (value is long longValue)
        {
            result = longValue > int.MaxValue ? int.MaxValue : longValue < int.MinValue ? int.MinValue : (int)longValue;
            return true;
        }

        result = 0;
        return false;
    }

    static bool TryConvertToFloat(object value, out float result)
    {
        if (value is float floatValue)
        {
            result = floatValue;
            return true;
        }

        if (value is double doubleValue)
        {
            result = (float)doubleValue;
            return true;
        }

        if (value is int intValue)
        {
            result = intValue;
            return true;
        }

        result = 0f;
        return false;
    }

    IEnumerator EmergencySuitBeaconSpeedRoutine(PlayerMovement movement, float baseAstronautSpeed)
    {
        if (movement == null)
            yield break;

        movement.speed = Mathf.Max(movement.speed, baseAstronautSpeed * EmergencySuitBeaconSpeedMultiplier);
        yield return new WaitForSeconds(EmergencySuitBeaconSpeedDuration);

        if (movement != null)
            movement.speed = baseAstronautSpeed;

        emergencySuitBeaconSpeedRoutine = null;
    }

    void LateUpdate()
    {
        if (cachedRenderer != null)
            cachedRenderer.color = Color.white;
    }

    Sprite LoadAstronautSprite()
    {
        if (cachedAstronautSprite != null)
            return cachedAstronautSprite;

        cachedAstronautSprite = Resources.Load<Sprite>("kosmonauta_resource");
        if (cachedAstronautSprite != null)
            return cachedAstronautSprite;

        Sprite[] resourceSprites = Resources.LoadAll<Sprite>("kosmonauta_resource");
        if (resourceSprites != null && resourceSprites.Length > 0)
        {
            cachedAstronautSprite = resourceSprites[0];
            return cachedAstronautSprite;
        }

        Texture2D resourceTexture = Resources.Load<Texture2D>("kosmonauta_resource");
        if (resourceTexture != null)
        {
            cachedAstronautSprite = Sprite.Create(
                resourceTexture,
                new Rect(0f, 0f, resourceTexture.width, resourceTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return cachedAstronautSprite;
        }

        string filePath = System.IO.Path.Combine(Application.dataPath, "kosmonauta.png");
        if (!System.IO.File.Exists(filePath))
            return null;

        byte[] bytes = System.IO.File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(bytes, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        cachedAstronautSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        return cachedAstronautSprite;
    }

    static int GetEnemyAstronautSpriteCount()
    {
        Sprite[] sprites = LoadEnemyAstronautSprites();
        return sprites != null && sprites.Length > 0 ? sprites.Length : 9;
    }

    static Sprite LoadEnemyAstronautSprite(int variantIndex)
    {
        Sprite[] sprites = LoadEnemyAstronautSprites();
        if (sprites == null || sprites.Length == 0)
            return null;

        return sprites[Mathf.Abs(variantIndex) % sprites.Length];
    }

    static Sprite[] LoadEnemyAstronautSprites()
    {
        if (cachedEnemyAstronautSprites != null && cachedEnemyAstronautSprites.Length > 0)
            return cachedEnemyAstronautSprites;

        cachedEnemyAstronautSprites = Resources.LoadAll<Sprite>(EnemyAstronautSpriteResourcePath);
        if (cachedEnemyAstronautSprites != null && cachedEnemyAstronautSprites.Length > 0)
            return cachedEnemyAstronautSprites;

        Texture2D texture = Resources.Load<Texture2D>(EnemyAstronautSpriteResourcePath);
        if (texture == null)
            return cachedEnemyAstronautSprites;

        cachedEnemyAstronautSprites = SliceEnemyAstronautTexture(texture);
        return cachedEnemyAstronautSprites;
    }

    static Sprite[] SliceEnemyAstronautTexture(Texture2D texture)
    {
        if (texture == null)
            return System.Array.Empty<Sprite>();

        const int columns = 3;
        const int rows = 3;
        int cellWidth = texture.width / columns;
        int cellHeight = texture.height / rows;
        Sprite[] sprites = new Sprite[columns * rows];
        int index = 0;
        for (int row = rows - 1; row >= 0; row--)
        {
            for (int column = 0; column < columns; column++)
            {
                Rect rect = new Rect(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
                sprites[index] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
                sprites[index].name = "enemy_astronaut_runtime_" + index;
                index++;
            }
        }

        return sprites;
    }

    Sprite LoadEscapePodSprite()
    {
        if (cachedEscapePodSprite != null)
            return cachedEscapePodSprite;

        cachedEscapePodSprite = Resources.Load<Sprite>("Items/escape_pod");
        if (cachedEscapePodSprite != null)
            return cachedEscapePodSprite;

        Texture2D resourceTexture = Resources.Load<Texture2D>("Items/escape_pod");
        if (resourceTexture != null)
        {
            cachedEscapePodSprite = Sprite.Create(
                resourceTexture,
                new Rect(0f, 0f, resourceTexture.width, resourceTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return cachedEscapePodSprite;
        }

        string filePath = System.IO.Path.Combine(Application.dataPath, "escape_pod.png");
        if (!System.IO.File.Exists(filePath))
            return null;

        byte[] bytes = System.IO.File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(bytes, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        cachedEscapePodSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        return cachedEscapePodSprite;
    }

    void SetWorldBoxSize(BoxCollider2D collider2D, Vector2 worldSize)
    {
        Vector3 scale = collider2D.transform.lossyScale;
        float safeX = Mathf.Abs(scale.x) > 0.0001f ? Mathf.Abs(scale.x) : 1f;
        float safeY = Mathf.Abs(scale.y) > 0.0001f ? Mathf.Abs(scale.y) : 1f;
        collider2D.size = new Vector2(worldSize.x / safeX, worldSize.y / safeY);
    }

    void FitRendererToTargetSize(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds spriteBounds = renderer.sprite.bounds;
        float largestDimension = Mathf.Max(spriteBounds.size.x, spriteBounds.size.y);
        if (largestDimension <= 0.0001f)
            return;

        float scale = targetSize / largestDimension;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
