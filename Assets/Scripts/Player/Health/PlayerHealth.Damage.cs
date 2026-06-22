using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PlayerHealth : MonoBehaviourPun
{
    const float CharlieSmartPirateDamageMultiplier = 0.8f;

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

    public void ConsumeByGravityWell(float impactX, float impactY)
    {
        if (!PhotonNetwork.IsMasterClient || IsWreck || isEvacuationAnimating || currentHP <= 0)
            return;

        currentShield = 0;
        currentHP = 0;
        photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);
        RoundXpTracker.RecordKill(-1, this);
        HandleDeath(-1);
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
        if (absorbed > 0 || hpDamage > 0)
            BisonIndustrialPlotController.NotifyPlayerDamaged(this, hitContext);

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
        if (absorbed > 0 || hpDamage > 0)
            BisonIndustrialPlotController.NotifyPlayerDamaged(this, hitContext);

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
        if (absorbed > 0)
            BisonIndustrialPlotController.NotifyPlayerDamaged(this, hitContext);
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
        object[] instantiationData = photonView != null ? photonView.InstantiationData : null;
        return ViperRecoveryPlotController.IsViperWreckInstantiationData(instantiationData) ||
               PlayerDeployableRuntime.IsInstantiationData(instantiationData) ||
               GetComponent<ViperWreckTowTarget>() != null ||
               GetComponent<PlayerDeployableBase>() != null;
    }

    void RememberDamageContext(WeaponHitContext hitContext, WeaponDamageMultipliers damageMultipliers)
    {
        LastDamageContext = hitContext;
        LastDamageShieldMultiplier = damageMultipliers.ShieldMultiplier;
        LastDamageHpMultiplier = damageMultipliers.HpMultiplier;
    }

    int ApplyPilotDamageModifiers(int damage, int attackerViewID, WeaponHitContext hitContext)
    {
        string damageSource = hitContext.DamageSource ?? string.Empty;
        int result = Mathf.Max(0, damage);
        if (result <= 0)
            return 0;

        if (string.Equals(damageSource, DamageSourceRadioactiveCargo, System.StringComparison.Ordinal))
            return result;

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        if (!IsBotControlled &&
            !IsNeutralRiderControlled &&
            PilotCatalog.IsSelectedPilot(owner, PilotCatalog.CharlieSmartId) &&
            IsDamageFromPirateEnemy(attackerViewID))
        {
            result = Mathf.Max(1, Mathf.RoundToInt(result * CharlieSmartPirateDamageMultiplier));
        }

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

        if (IsHumanShipControlled && HasEquippedItem(InventoryItemCatalog.KineticDampenerId))
        {
            float kineticMultiplier = 1f;
            if (IsPhysicalImpactDamage(hitContext, attackerViewID, damageSource))
                kineticMultiplier = Mathf.Min(kineticMultiplier, KineticDampenerDamageMultiplier);

            if (IsExplosiveDamage(hitContext, damageSource))
                kineticMultiplier = Mathf.Min(kineticMultiplier, KineticDampenerExplosiveDamageMultiplier);

            if (kineticMultiplier < 1f)
                result = Mathf.Max(1, Mathf.RoundToInt(result * kineticMultiplier));
        }

        if (IsHumanShipControlled && HasEquippedItem(InventoryItemCatalog.StrongPlatingId) &&
            IsEnvironmentalDamage(hitContext, damageSource))
        {
            result = Mathf.Max(1, Mathf.RoundToInt(result * StrongPlatingEnvironmentalDamageMultiplier));
        }

        if (IsHumanShipControlled && HasEquippedItem(InventoryItemCatalog.BulwarkProjectorId) &&
            IsLaserWeaponDamage(hitContext, attackerViewID, damageSource))
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

    bool IsDamageFromPirateEnemy(int attackerViewID)
    {
        if (attackerViewID <= 0)
            return false;

        PhotonView attackerView = PhotonView.Find(attackerViewID);
        if (attackerView == null)
            return false;

        EnemyBot bot = attackerView.GetComponent<EnemyBot>();
        return bot != null && (EnemyBot.IsPirateFighterKind(bot.Kind) || bot.Kind == EnemyBotKind.PirateBase);
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

    bool IsLegacyPhysicalImpactDamage(int attackerViewID, string damageSource)
    {
        return string.Equals(damageSource, PilotDamageSourceRamming, System.StringComparison.Ordinal) ||
               IsDamageFromSpaceMine(attackerViewID) ||
               IsDamageFromEnemyKind(attackerViewID, EnemyBotKind.SpaceManta);
    }

    bool IsPhysicalImpactDamage(WeaponHitContext hitContext, int attackerViewID, string damageSource)
    {
        return hitContext.DamageType == WeaponDamageType.Kinetic ||
               hitContext.DeliveryMethod == WeaponDeliveryMethod.ContactDash ||
               IsLegacyPhysicalImpactDamage(attackerViewID, damageSource);
    }

    bool IsExplosiveDamage(WeaponHitContext hitContext, string damageSource)
    {
        return hitContext.DamageType == WeaponDamageType.Explosive ||
               IsExplosiveDamageSource(damageSource);
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

    bool IsEnvironmentalDamage(WeaponHitContext hitContext, string damageSource)
    {
        return hitContext.DamageType == WeaponDamageType.Environmental ||
               IsEnvironmentalDamageSource(damageSource);
    }

    bool IsLaserWeaponDamage(WeaponHitContext hitContext, int attackerViewID, string damageSource)
    {
        if (hitContext.DamageType == WeaponDamageType.Laser)
            return true;

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

    public void BeginBotSpawnInvulnerability(float duration)
    {
        if (duration <= 0f || IsWreck || !IsBotControlled)
            return;

        spawnInvulnerableUntil = Mathf.Max(spawnInvulnerableUntil, Time.time + duration);
        if (RoomSettings.AreVisualEffectsEnabled())
            SpawnInvulnerabilityVfx.Attach(this);
    }

    public void ClearBotSpawnInvulnerability()
    {
        if (!IsBotControlled)
            return;

        spawnInvulnerableUntil = Mathf.Min(spawnInvulnerableUntil, Time.time);
    }

    public void BeginMapTravelArrivalProtection()
    {
        BeginSpawnInvulnerability(1.25f);
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
}
