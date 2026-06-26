using System;
using System.Threading.Tasks;
using UnityEngine;

public partial class PlayerProfileService
{
    public async Task ApplyShipLossAsync(int shipSkinIndex, bool loseShipInventory, bool loseEquipment, string serializedAstronautCargo = null, string protectedEquipmentItemId = null, bool deferSave = false)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        bool changed = false;
        if (loseShipInventory)
        {
            int shipCapacity = GetActiveShipInventoryCapacity();
            StagePendingAstronautCargo(serializedAstronautCargo, CurrentProfile.Inventory.ShipSlots, shipSkinIndex, shipCapacity);
            string[] preLossShipSlots = NormalizeShipSlots(CurrentProfile.Inventory.ShipSlots);
            string[] postLossShipSlots = BuildPostLossShipInventory(CurrentProfile.Inventory.ShipSlots, shipSkinIndex);
            RegisterMissEnigmaUniqueItemLosses(preLossShipSlots, postLossShipSlots);
            CurrentProfile.Inventory.SetShipSlots(postLossShipSlots);
            changed = true;
        }
        else
        {
            ClearPendingAstronautCargo();
        }

        if (loseEquipment)
        {
            StagePendingProtectedEquipment(protectedEquipmentItemId, CurrentProfile.Inventory.EquipmentSlots, shipSkinIndex);
            CurrentProfile.Inventory.EquipmentSlots = BuildPostLossEquipmentInventory(CurrentProfile.Inventory.EquipmentSlots, shipSkinIndex, ShouldPreserveEngineEquipmentOnLoss());
            changed = true;
        }
        else
        {
            ClearPendingProtectedEquipment();
        }

        if (!changed)
            return;

        if (deferSave)
        {
            MarkInventoryChangedDeferred();
        }
        else
        {
            await SaveInventoryOnlyAsync();
        }
    }

    public async Task<bool> RestorePendingAstronautCargoAsync()
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int shipSkinIndex = pendingAstronautCargoShipSkinIndex >= 0
            ? pendingAstronautCargoShipSkinIndex
            : pendingProtectedEquipmentShipSkinIndex >= 0
                ? pendingProtectedEquipmentShipSkinIndex
                : GetActiveShipSkinIndex();
        CurrentProfile.Inventory.SetShipSlots(BuildPostLossShipInventory(CurrentProfile.Inventory.ShipSlots, shipSkinIndex));
        bool changed = true;
        changed |= RestorePendingProtectedEquipment(shipSkinIndex);

        if (!hasPendingAstronautCargo || pendingAstronautCargoSlots == null)
        {
            await SaveInventoryOnlyAsync();
            return true;
        }

        string[] cargoSlots = NormalizeShipSlots(pendingAstronautCargoSlots);
        ClearPendingAstronautCargo();

        int capacity = GetActiveShipInventoryCapacity();
        for (int i = 0; i < cargoSlots.Length; i++)
        {
            string itemId = cargoSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (i < capacity &&
                string.IsNullOrWhiteSpace(CurrentProfile.Inventory.ShipSlots[i]) &&
                CanStoreItemInShipSlot(itemId, shipSkinIndex, i))
            {
                CurrentProfile.Inventory.ShipSlots[i] = itemId;
                changed = true;
                continue;
            }

            if (CurrentProfile.Inventory.TryAddToShip(itemId, capacity, shipSkinIndex))
            {
                changed = true;
            }
            else
            {
                Debug.LogWarning("Astronaut cargo could not be restored: " + itemId);
            }
        }

        if (changed)
        {
            ApplyInventoryToPhoton();
            await SaveInventoryOnlyAsync();
        }

        return changed;
    }

    public void DiscardPendingAstronautCargo()
    {
        ClearPendingAstronautCargo();
        ClearPendingProtectedEquipment();
    }

    public static string[] BuildLossWreckLoot(string[] sourceSlots, int shipSkinIndex, int shipCapacity = -1)
    {
        string[] normalized = NormalizeShipSlots(sourceSlots);
        int effectiveCapacity = shipCapacity >= 0
            ? Mathf.Clamp(shipCapacity, 0, PlayerInventoryData.ShipSlotCount)
            : ShipCatalog.GetShipInventoryCapacity(shipSkinIndex);
        for (int i = 0; i < normalized.Length; i++)
        {
            if (InventoryItemCatalog.RequiresSafePocket(normalized[i]))
                normalized[i] = null;
            else if (IsSafePocketIndex(shipSkinIndex, i) && InventoryItemCatalog.CanEnterSafePocket(normalized[i]))
                normalized[i] = null;
            else if (IsAstronautCargoIndex(shipSkinIndex, effectiveCapacity, i))
                normalized[i] = null;
        }

        return normalized;
    }

    public static string[] BuildAstronautCargoSnapshot(string[] sourceSlots, int shipSkinIndex, int shipCapacity, int astronautCargoSlotCount = -1)
    {
        string[] normalized = NormalizeShipSlots(sourceSlots);
        string[] snapshot = new string[PlayerInventoryData.ShipSlotCount];
        int slotCount = astronautCargoSlotCount >= 0
            ? astronautCargoSlotCount
            : GetAstronautCargoSlotCount(shipSkinIndex);

        for (int i = 0; i < normalized.Length; i++)
        {
            if (IsAstronautCargoIndex(shipSkinIndex, shipCapacity, i, slotCount) &&
                !string.IsNullOrWhiteSpace(normalized[i]))
            {
                snapshot[i] = normalized[i];
            }
        }

        return snapshot;
    }

    public static string[] BuildPostLossShipInventory(string[] sourceSlots, int shipSkinIndex)
    {
        string[] normalized = NormalizeShipSlots(sourceSlots);
        for (int i = 0; i < normalized.Length; i++)
        {
            bool keepSafePocketItem = IsSafePocketIndex(shipSkinIndex, i) && InventoryItemCatalog.CanEnterSafePocket(normalized[i]);
            bool keepRequiredSafePocketItem = InventoryItemCatalog.RequiresSafePocket(normalized[i]);
            if (!keepSafePocketItem && !keepRequiredSafePocketItem)
                normalized[i] = null;
        }

        return normalized;
    }

    static string[] BuildPostLossEquipmentInventory(string[] sourceSlots, int shipSkinIndex, bool preserveEngineSlots)
    {
        string[] normalized = NormalizeEquipmentSlots(sourceSlots);
        for (int i = 0; i < normalized.Length; i++)
        {
            bool keepEngineSlot =
                preserveEngineSlots &&
                ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex) &&
                InventoryItemCatalog.GetEquipmentSlotCategory(i) == InventoryItemCategory.Engine &&
                InventoryItemCatalog.IsCompatibleWithEquipmentSlot(normalized[i], i);

            if (!keepEngineSlot)
                normalized[i] = null;
        }

        return normalized;
    }

    void StagePendingAstronautCargo(string serializedAstronautCargo, string[] fallbackSourceSlots, int shipSkinIndex, int shipCapacity)
    {
        string[] snapshot = serializedAstronautCargo != null
            ? DeserializeShipInventorySlots(serializedAstronautCargo)
            : BuildAstronautCargoSnapshot(fallbackSourceSlots, shipSkinIndex, shipCapacity);

        if (!HasAnyShipSlotItem(snapshot))
        {
            ClearPendingAstronautCargo();
            return;
        }

        pendingAstronautCargoSlots = NormalizeShipSlots(snapshot);
        pendingAstronautCargoShipSkinIndex = shipSkinIndex;
        hasPendingAstronautCargo = true;
    }

    void StagePendingProtectedEquipment(string itemId, string[] sourceEquipmentSlots, int shipSkinIndex)
    {
        ClearPendingProtectedEquipment();
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        string[] slots = NormalizeEquipmentSlots(sourceEquipmentSlots);
        for (int i = 0; i < slots.Length; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            if (!string.Equals(slots[i], itemId, StringComparison.Ordinal))
                continue;

            pendingProtectedEquipmentItemId = itemId;
            pendingProtectedEquipmentSlotIndex = i;
            pendingProtectedEquipmentShipSkinIndex = shipSkinIndex;
            hasPendingProtectedEquipment = true;
            return;
        }
    }

    bool RestorePendingProtectedEquipment(int fallbackShipSkinIndex)
    {
        if (!hasPendingProtectedEquipment || string.IsNullOrWhiteSpace(pendingProtectedEquipmentItemId))
        {
            ClearPendingProtectedEquipment();
            return false;
        }

        string itemId = pendingProtectedEquipmentItemId;
        int preferredSlot = pendingProtectedEquipmentSlotIndex;
        int shipSkinIndex = pendingProtectedEquipmentShipSkinIndex >= 0
            ? pendingProtectedEquipmentShipSkinIndex
            : fallbackShipSkinIndex;
        ClearPendingProtectedEquipment();

        if (TryRestoreEquipmentToSlot(itemId, preferredSlot, shipSkinIndex))
            return true;

        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        int fallbackSlot = definition != null
            ? GetFirstFreeEquipmentSlotByCategoryForProfile(CurrentProfile.Inventory.EquipmentSlots, shipSkinIndex, definition.Category)
            : -1;
        if (TryRestoreEquipmentToSlot(itemId, fallbackSlot, shipSkinIndex))
            return true;

        int capacity = GetProfileShipInventoryCapacity(shipSkinIndex, CurrentProfile.Inventory.EquipmentSlots);
        if (CurrentProfile.Inventory.TryAddToShip(itemId, capacity, shipSkinIndex))
            return true;

        if (CurrentProfile.Inventory.TryAddToPlayer(itemId))
            return true;

        Debug.LogWarning("Protected equipment could not be restored after evacuation: " + itemId);
        return false;
    }

    bool ShouldPreserveEngineEquipmentOnLoss()
    {
        string pilotId = CurrentProfile != null ? CurrentProfile.SelectedPilotId : PilotCatalog.JakeId;
        return string.Equals(PilotCatalog.NormalizePilotId(pilotId), PilotCatalog.CovaxId, StringComparison.Ordinal);
    }

    bool TryRestoreEquipmentToSlot(string itemId, int slotIndex, int shipSkinIndex)
    {
        if (string.IsNullOrWhiteSpace(itemId) ||
            slotIndex < 0 ||
            slotIndex >= PlayerInventoryData.EquipmentSlotCount ||
            !IsEquipmentSlotEnabledForProfile(slotIndex, shipSkinIndex) ||
            !InventoryItemCatalog.IsCompatibleWithEquipmentSlot(itemId, slotIndex) ||
            !string.IsNullOrWhiteSpace(CurrentProfile.Inventory.EquipmentSlots[slotIndex]))
        {
            return false;
        }

        CurrentProfile.Inventory.SetEquipment(slotIndex, itemId);
        return true;
    }

    void ClearPendingAstronautCargo()
    {
        pendingAstronautCargoSlots = null;
        pendingAstronautCargoShipSkinIndex = -1;
        hasPendingAstronautCargo = false;
    }

    void ClearPendingProtectedEquipment()
    {
        pendingProtectedEquipmentItemId = null;
        pendingProtectedEquipmentSlotIndex = -1;
        pendingProtectedEquipmentShipSkinIndex = -1;
        hasPendingProtectedEquipment = false;
    }
}
