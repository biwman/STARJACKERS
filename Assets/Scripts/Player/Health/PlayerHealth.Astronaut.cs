using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PlayerHealth : MonoBehaviourPun
{
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

    [PunRPC]
    public void ForceBioTrapAstronautDeathRpc(int attackerViewID, float impactX, float impactY)
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            return;

        if (!IsAstronautControlled || IsWreck || isEvacuationAnimating || deathHandled || currentHP <= 0)
            return;

        AstronautSurvivor survivor = GetComponent<AstronautSurvivor>();
        if ((survivor != null && survivor.IsEscapePodMode) ||
            AstronautSurvivor.IsEscapePodInstantiationData(photonView != null ? photonView.InstantiationData : null))
        {
            return;
        }

        WeaponHitContext hitContext = new WeaponHitContext(
            WeaponDamageType.Ion,
            WeaponDeliveryMethod.Trap,
            WeaponDeliveryFlags.None,
            DamageSourceBioTrap);
        RememberDamageContext(hitContext, WeaponDamageMultipliers.Neutral);

        int previousHp = currentHP;
        int previousShield = currentShield;
        currentShield = 0;
        currentHP = 0;

        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && photonView != null)
            photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);
        else
            SyncVitals(currentHP, currentShield);

        if (previousShield > 0)
        {
            photonView.RPC(nameof(PlayShieldHitAudio), RpcTarget.All);
            photonView.RPC(nameof(PlayShieldHitVisual), RpcTarget.All, impactX, impactY);
        }

        if (previousHp > 0)
        {
            photonView.RPC(nameof(PlayHpHitAudio), RpcTarget.All);
            photonView.RPC(nameof(PlayHpHitVisual), RpcTarget.All, impactX, impactY);
            RoundXpTracker.RecordDamage(attackerViewID, photonView, previousShield, previousHp);
            RoundXpTracker.RecordKill(attackerViewID, this);
        }

        HandleDeath(attackerViewID);
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
}
