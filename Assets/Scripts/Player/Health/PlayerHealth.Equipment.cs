using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PlayerHealth : MonoBehaviourPun
{
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

    int GetConfiguredMaxShield(int shipSkinIndex)
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        if (InventoryItemCatalog.HasShieldDisablingUpgrade(equipmentSlots, shipSkinIndex))
            return 0;

        int baseShield = ShipCatalog.GetBaseShield(shipSkinIndex);
        int bonus = GetEquippedShieldCapacityBonus(shipSkinIndex);
        int penalty = InventoryItemCatalog.GetEquippedShieldCapacityPenalty(equipmentSlots, shipSkinIndex);
        return Mathf.Max(0, baseShield + bonus - penalty);
    }

    int GetEquippedHpCapacityBonus(int shipSkinIndex)
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        return InventoryItemCatalog.GetEquippedHpCapacityBonus(equipmentSlots, shipSkinIndex);
    }
}
