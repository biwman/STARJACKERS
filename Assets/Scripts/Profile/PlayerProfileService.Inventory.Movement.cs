using System;
using System.Threading.Tasks;
using UnityEngine;

public partial class PlayerProfileService
{
    public async Task<bool> MoveShipItemWithinShipAsync(int sourceIndex, int targetIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (sourceIndex == targetIndex)
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        workingInventory.Normalize();

        int capacity = GetActiveShipInventoryCapacity();
        if (sourceIndex < 0 || sourceIndex >= capacity || targetIndex < 0 || targetIndex >= capacity)
            return false;

        string sourceItem = workingInventory.ShipSlots[sourceIndex];
        if (string.IsNullOrWhiteSpace(sourceItem))
            return false;

        string targetItem = workingInventory.ShipSlots[targetIndex];
        if (!CanStoreItemInShipSlot(sourceItem, GetActiveShipSkinIndex(), targetIndex) ||
            !CanStoreItemInShipSlot(targetItem, GetActiveShipSkinIndex(), sourceIndex))
        {
            return false;
        }

        workingInventory.ShipSlots[targetIndex] = sourceItem;
        workingInventory.ShipSlots[sourceIndex] = targetItem;

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> MoveInventoryItemAsync(bool fromShipInventory, int sourceIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        string movedItem = fromShipInventory
            ? CurrentProfile.Inventory.RemoveFromShip(sourceIndex)
            : CurrentProfile.Inventory.RemoveFromPlayer(sourceIndex);

        if (string.IsNullOrWhiteSpace(movedItem))
            return false;

        bool moved = fromShipInventory
            ? CurrentProfile.Inventory.TryAddToPlayer(movedItem)
            : CurrentProfile.Inventory.TryAddToShip(movedItem, GetActiveShipInventoryCapacity(), CurrentProfile.ShipSkinIndex);

        if (!moved)
        {
            if (fromShipInventory)
                CurrentProfile.Inventory.RestoreShip(sourceIndex, movedItem);
            else
                CurrentProfile.Inventory.RestorePlayer(sourceIndex, movedItem);

            return false;
        }

        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<int> UnloadShipInventoryToPlayerAsync()
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        workingInventory.Normalize();

        int movedCount = 0;
        for (int i = 0; i < workingInventory.ShipSlots.Length; i++)
        {
            string itemId = workingInventory.ShipSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!workingInventory.TryAddToPlayer(itemId))
                break;

            workingInventory.ShipSlots[i] = null;
            movedCount++;
        }

        if (movedCount <= 0)
            return 0;

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return movedCount;
    }

    public async Task<bool> MoveInventoryItemToEquipmentAsync(bool fromShipInventory, int sourceIndex, int equipmentSlotIndex, int shipSkinIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (!IsEquipmentSlotEnabledForProfile(equipmentSlotIndex, shipSkinIndex))
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        string movedItem = fromShipInventory
            ? GetSlotItem(workingInventory.ShipSlots, sourceIndex)
            : GetSlotItem(workingInventory.PlayerSlots, sourceIndex);

        if (!InventoryItemCatalog.IsCompatibleWithEquipmentSlot(movedItem, equipmentSlotIndex))
            return false;

        if (IsSingleInstallItem(movedItem) &&
            IsItemAlreadyEquipped(workingInventory.EquipmentSlots, movedItem, equipmentSlotIndex))
        {
            return false;
        }

        movedItem = fromShipInventory
            ? workingInventory.RemoveFromShip(sourceIndex)
            : workingInventory.RemoveFromPlayer(sourceIndex);

        if (string.IsNullOrWhiteSpace(movedItem))
            return false;

        string replacedItem = workingInventory.RemoveFromEquipment(equipmentSlotIndex);
        workingInventory.SetEquipment(equipmentSlotIndex, movedItem);

        if (!TryMoveOverflowShipCargoToPlayer(workingInventory, shipSkinIndex))
            return false;

        if (!string.IsNullOrWhiteSpace(replacedItem))
        {
            bool restored = fromShipInventory
                ? workingInventory.TryAddToShip(replacedItem, GetProfileShipInventoryCapacity(shipSkinIndex, workingInventory.EquipmentSlots), shipSkinIndex)
                : workingInventory.TryAddToPlayer(replacedItem);

            if (!restored)
                return false;
        }

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return true;
    }

    string GetSlotItem(string[] slots, int index)
    {
        if (slots == null || index < 0 || index >= slots.Length)
            return null;

        return slots[index];
    }

    static bool IsItemAlreadyEquipped(string[] equipmentSlots, string itemId, int ignoredSlotIndex)
    {
        if (equipmentSlots == null || string.IsNullOrWhiteSpace(itemId))
            return false;

        for (int i = 0; i < equipmentSlots.Length; i++)
        {
            if (i == ignoredSlotIndex)
                continue;

            if (string.Equals(equipmentSlots[i], itemId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static bool IsSingleInstallItem(string itemId)
    {
        return string.Equals(itemId, InventoryItemCatalog.LootingFriendId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.FiringFriendId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.DropbotId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.EscapePodId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.OverclockedMagazineId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.BlackMarketThrusterId, StringComparison.Ordinal);
    }

    bool TryMoveOverflowShipCargoToPlayer(PlayerInventoryData inventory, int shipSkinIndex)
    {
        if (inventory == null)
            return false;

        inventory.Normalize();
        int capacity = GetProfileShipInventoryCapacity(shipSkinIndex, inventory.EquipmentSlots);
        for (int i = capacity; i < inventory.ShipSlots.Length; i++)
        {
            string itemId = inventory.ShipSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!inventory.TryAddToPlayer(itemId))
                return false;

            inventory.ShipSlots[i] = null;
        }

        return true;
    }

    public async Task<bool> MoveEquipmentItemToInventoryAsync(int equipmentSlotIndex, bool toPlayerInventory, int shipSkinIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (!IsEquipmentSlotEnabledForProfile(equipmentSlotIndex, shipSkinIndex))
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        string movedItem = workingInventory.RemoveFromEquipment(equipmentSlotIndex);
        if (string.IsNullOrWhiteSpace(movedItem))
            return false;

        if (!TryMoveOverflowShipCargoToPlayer(workingInventory, shipSkinIndex))
            return false;

        bool moved = toPlayerInventory
            ? workingInventory.TryAddToPlayer(movedItem)
            : workingInventory.TryAddToShip(movedItem, GetProfileShipInventoryCapacity(shipSkinIndex, workingInventory.EquipmentSlots), shipSkinIndex);

        if (!moved)
            return false;

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return true;
    }

    bool TryMoveSafePocketRestrictedItems(PlayerInventoryData inventory, int shipSkinIndex)
    {
        if (inventory == null)
            return true;

        inventory.Normalize();
        int capacity = GetProfileShipInventoryCapacity(shipSkinIndex, inventory.EquipmentSlots);
        for (int i = 0; i < inventory.ShipSlots.Length && i < capacity; i++)
        {
            string itemId = inventory.ShipSlots[i];
            if (CanStoreItemInShipSlot(itemId, shipSkinIndex, i))
                continue;

            int targetSlot = inventory.GetFirstEmptyShipSlot(capacity, shipSkinIndex, itemId);
            if (targetSlot >= 0)
            {
                inventory.ShipSlots[targetSlot] = itemId;
                inventory.ShipSlots[i] = null;
                continue;
            }

            if (inventory.TryAddToPlayer(itemId))
            {
                inventory.ShipSlots[i] = null;
                continue;
            }

            return false;
        }

        return true;
    }

    bool TryMoveIncompatibleEquipmentItems(PlayerInventoryData inventory, int shipSkinIndex)
    {
        if (inventory == null)
            return true;

        inventory.Normalize();
        for (int i = 0; i < inventory.EquipmentSlots.Length; i++)
        {
            string itemId = inventory.EquipmentSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (IsEquipmentSlotEnabledForProfile(i, shipSkinIndex) &&
                InventoryItemCatalog.IsCompatibleWithEquipmentSlot(itemId, i))
            {
                continue;
            }

            inventory.EquipmentSlots[i] = null;
            int targetSlot = GetFirstFreeCompatibleEquipmentSlot(inventory.EquipmentSlots, shipSkinIndex, itemId);
            if (targetSlot >= 0)
            {
                inventory.EquipmentSlots[targetSlot] = itemId;
                continue;
            }

            if (inventory.TryAddToPlayer(itemId))
                continue;

            if (inventory.TryAddToShip(itemId, GetProfileShipInventoryCapacity(shipSkinIndex, inventory.EquipmentSlots), shipSkinIndex))
                continue;

            inventory.EquipmentSlots[i] = itemId;
            return false;
        }

        return true;
    }

    int GetFirstFreeCompatibleEquipmentSlot(string[] sourceSlots, int shipSkinIndex, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return -1;

        if (IsSingleInstallItem(itemId) && IsItemAlreadyEquipped(sourceSlots, itemId, -1))
            return -1;

        string[] slots = NormalizeEquipmentSlots(sourceSlots);
        for (int i = 0; i < slots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(slots[i]))
                continue;

            if (!IsEquipmentSlotEnabledForProfile(i, shipSkinIndex))
                continue;

            if (InventoryItemCatalog.IsCompatibleWithEquipmentSlot(itemId, i))
                return i;
        }

        return -1;
    }
}
