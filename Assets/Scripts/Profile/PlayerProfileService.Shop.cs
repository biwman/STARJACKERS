using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public partial class PlayerProfileService
{
    public const int MissEnigmaUniqueItemRecoveryPriceAstrons = 100000;

    public int GetShopBuyPriceAstrons(string itemId)
    {
        if (CanRecoverMissEnigmaUniqueItem(itemId))
            return MissEnigmaUniqueItemRecoveryPriceAstrons;

        int basePrice = InventoryItemCatalog.GetShopBuyValueAstrons(itemId);
        if (basePrice <= 0)
            return 0;

        return basePrice;
    }

    public bool CanBuyAvengerStartingCodes()
    {
        EnsureInventory();
        EnsureShipUnlocks();

        if (IsShipUnlocked(ShipType.Avenger))
            return false;

        if (CurrentProfile.AvengerTheftAttempt != null && CurrentProfile.AvengerTheftAttempt.Active)
            return false;

        return !HasInventoryItem(InventoryItemCatalog.AvengerStartingCodesId);
    }

    public async Task<bool> TryBuyShopItemAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        if (definition == null ||
            (definition.ItemType != InventoryItemType.Equipment && definition.ItemType != InventoryItemType.Quest))
        {
            return false;
        }

        if (string.Equals(itemId, InventoryItemCatalog.AvengerStartingCodesId, StringComparison.Ordinal) &&
            !CanBuyAvengerStartingCodes())
        {
            return false;
        }

        int price = GetShopBuyPriceAstrons(itemId);
        if (price <= 0 || CurrentProfile.Astrons < price)
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        bool stored = workingInventory.TryAddToPlayer(itemId);
        if (!stored)
            stored = workingInventory.TryAddToShip(itemId, GetProfileShipInventoryCapacity(CurrentProfile.ShipSkinIndex, workingInventory.EquipmentSlots), CurrentProfile.ShipSkinIndex);

        if (!stored)
            return false;

        CurrentProfile.Astrons = Mathf.Max(0, CurrentProfile.Astrons - price);
        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryAndAstronsAsync();
        return true;
    }

    public bool HasInventoryItem(string itemId)
    {
        EnsureInventory();
        return CountItemInSlots(CurrentProfile.Inventory.PlayerSlots, CurrentProfile.Inventory.PlayerSlots.Length, itemId) > 0 ||
               CountItemInSlots(CurrentProfile.Inventory.ShipSlots, GetActiveShipInventoryCapacity(), itemId) > 0 ||
               CountItemInSlots(CurrentProfile.Inventory.EquipmentSlots, CurrentProfile.Inventory.EquipmentSlots.Length, itemId) > 0 ||
               CountItemInSlots(CurrentProfile.Inventory.CraftingSlots, CurrentProfile.Inventory.CraftingSlots.Length, itemId) > 0;
    }

    public bool CanRecoverMissEnigmaUniqueItem(string itemId)
    {
        EnsureInventory();
        EnsureMissEnigmaUniqueItemRecoveries();

        if (!InventoryItemCatalog.IsMissEnigmaRecoverableUniqueItem(itemId))
            return false;

        if (!IsMissEnigmaUniqueItemRecoveryRegistered(itemId))
            return false;

        return !HasInventoryItem(itemId);
    }

    public string[] GetMissEnigmaRecoverableUniqueItemIds()
    {
        EnsureInventory();
        EnsureMissEnigmaUniqueItemRecoveries();

        List<string> visibleItemIds = new List<string>();
        for (int i = 0; i < CurrentProfile.MissEnigmaRecoverableUniqueItemIds.Length; i++)
        {
            string itemId = CurrentProfile.MissEnigmaRecoverableUniqueItemIds[i];
            if (CanRecoverMissEnigmaUniqueItem(itemId))
                visibleItemIds.Add(itemId);
        }

        visibleItemIds.Sort(StringComparer.Ordinal);
        return visibleItemIds.ToArray();
    }

    bool IsMissEnigmaUniqueItemRecoveryRegistered(string itemId)
    {
        if (CurrentProfile?.MissEnigmaRecoverableUniqueItemIds == null || string.IsNullOrWhiteSpace(itemId))
            return false;

        for (int i = 0; i < CurrentProfile.MissEnigmaRecoverableUniqueItemIds.Length; i++)
        {
            if (string.Equals(CurrentProfile.MissEnigmaRecoverableUniqueItemIds[i], itemId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    void RegisterMissEnigmaUniqueItemLoss(string itemId)
    {
        if (!InventoryItemCatalog.IsMissEnigmaRecoverableUniqueItem(itemId))
            return;

        EnsureMissEnigmaUniqueItemRecoveries();
        HashSet<string> itemIds = new HashSet<string>(CurrentProfile.MissEnigmaRecoverableUniqueItemIds, StringComparer.Ordinal)
        {
            itemId
        };

        string[] normalized = new string[itemIds.Count];
        itemIds.CopyTo(normalized);
        CurrentProfile.MissEnigmaRecoverableUniqueItemIds = NormalizeMissEnigmaUniqueItemRecoveries(normalized);
    }

    void RegisterMissEnigmaUniqueItemLosses(string[] sourceSlots, string[] remainingSlots)
    {
        if (sourceSlots == null)
            return;

        for (int i = 0; i < sourceSlots.Length; i++)
        {
            string itemId = sourceSlots[i];
            if (!InventoryItemCatalog.IsMissEnigmaRecoverableUniqueItem(itemId) || ContainsExactItem(remainingSlots, itemId))
                continue;

            RegisterMissEnigmaUniqueItemLoss(itemId);
        }
    }

    static bool ContainsExactItem(string[] slots, string itemId)
    {
        if (slots == null || string.IsNullOrWhiteSpace(itemId))
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (string.Equals(slots[i], itemId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    void EnsureMissEnigmaUniqueItemRecoveries()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.MissEnigmaRecoverableUniqueItemIds = NormalizeMissEnigmaUniqueItemRecoveries(CurrentProfile.MissEnigmaRecoverableUniqueItemIds);
    }

    public static string[] NormalizeMissEnigmaUniqueItemRecoveries(string[] itemIds)
    {
        if (itemIds == null || itemIds.Length == 0)
            return Array.Empty<string>();

        HashSet<string> normalized = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < itemIds.Length; i++)
        {
            string itemId = itemIds[i];
            if (InventoryItemCatalog.IsMissEnigmaRecoverableUniqueItem(itemId))
                normalized.Add(itemId);
        }

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }
}
