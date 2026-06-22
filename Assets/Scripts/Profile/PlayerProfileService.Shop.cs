using System;
using System.Threading.Tasks;
using UnityEngine;

public partial class PlayerProfileService
{
    public int GetShopBuyPriceAstrons(string itemId)
    {
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
               CountItemInSlots(CurrentProfile.Inventory.EquipmentSlots, CurrentProfile.Inventory.EquipmentSlots.Length, itemId) > 0;
    }
}
