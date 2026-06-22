using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService
{
    public bool HasAncientGateKeyInShipSafePocket()
    {
        EnsureInventory();

        int shipSkinIndex = GetActiveShipSkinIndex();
        int capacity = GetActiveShipInventoryCapacity();
        string[] shipSlots = CurrentProfile.Inventory.ShipSlots;
        int limit = Mathf.Clamp(capacity, 0, shipSlots != null ? shipSlots.Length : 0);
        for (int i = 0; i < limit; i++)
        {
            if (!IsSafePocketIndex(shipSkinIndex, i))
                continue;

            if (string.Equals(shipSlots[i], InventoryItemCatalog.AncientGateKeyId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public async Task<bool> TryChangeShipSkinAsync(int newShipSkinIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int clampedSkin = Mathf.Clamp(newShipSkinIndex, 0, ShipCatalog.MaxShipSkinIndex);
        EnsureShipUnlocks();
        if (!IsShipSkinUnlocked(clampedSkin))
            return false;

        if (CurrentProfile.ShipSkinIndex == clampedSkin)
            return true;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        int newCapacity = GetProfileShipInventoryCapacity(clampedSkin, workingInventory.EquipmentSlots);

        for (int i = newCapacity; i < workingInventory.ShipSlots.Length; i++)
        {
            string itemId = workingInventory.ShipSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!workingInventory.TryAddToPlayer(itemId))
                return false;

            workingInventory.ShipSlots[i] = null;
        }

        if (!TryMoveSafePocketRestrictedItems(workingInventory, clampedSkin))
            return false;

        for (int i = 0; i < PlayerInventoryData.EquipmentSlotCount; i++)
        {
            if (IsEquipmentSlotEnabledForProfile(i, clampedSkin))
                continue;

            string itemId = workingInventory.EquipmentSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!workingInventory.TryAddToPlayer(itemId))
                return false;

            workingInventory.EquipmentSlots[i] = null;
        }

        CurrentProfile.ShipSkinIndex = clampedSkin;
        CurrentProfile.Inventory = workingInventory;
        await SaveShipSkinAndInventoryAsync();
        return true;
    }

    async Task SaveShipSkinAndInventoryAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();

            var data = new Dictionary<string, object>
            {
                [CloudShipSkinKey] = CurrentProfile.ShipSkinIndex,
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save ship skin and inventory");
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService ship/inventory save failed: " + ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
