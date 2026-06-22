using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService
{
    public bool IsBlueprintUnlocked(string blueprintItemId)
    {
        EnsureBlueprintUnlocks();
        if (!InventoryItemCatalog.IsBlueprintItem(blueprintItemId))
            return false;

        for (int i = 0; i < CurrentProfile.UnlockedBlueprintIds.Length; i++)
        {
            if (string.Equals(CurrentProfile.UnlockedBlueprintIds[i], blueprintItemId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public bool IsBlueprintUnlockedForItem(string itemId)
    {
        string blueprintItemId = InventoryItemCatalog.GetBlueprintItemId(itemId);
        return IsBlueprintUnlocked(blueprintItemId);
    }

    public bool CanAffordItemTrade(string[] costItemIds)
    {
        EnsureInventory();

        Dictionary<string, int> counts = BuildItemCounts(CurrentProfile.Inventory);
        return HasRequiredItems(counts, costItemIds);
    }

    public bool IsMissEnigmaBlueprintPurchased(string blueprintItemId)
    {
        EnsureMissEnigmaBlueprintPurchases();
        if (BlueprintCatalog.GetMissEnigmaOffer(blueprintItemId) == null)
            return false;

        for (int i = 0; i < CurrentProfile.MissEnigmaPurchasedBlueprintIds.Length; i++)
        {
            if (string.Equals(CurrentProfile.MissEnigmaPurchasedBlueprintIds[i], blueprintItemId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public async Task<bool> TryPurchaseMissEnigmaBlueprintAsync(string blueprintItemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureMissEnigmaBlueprintPurchases();

        BlueprintTradeOffer offer = BlueprintCatalog.GetMissEnigmaOffer(blueprintItemId);
        if (offer == null || IsBlueprintUnlocked(blueprintItemId) || IsMissEnigmaBlueprintPurchased(blueprintItemId) || !CanAffordItemTrade(offer.CostItemIds))
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        if (!RemoveRequiredItems(workingInventory, offer.CostItemIds))
            return false;

        bool stored = workingInventory.TryAddToPlayer(blueprintItemId);
        if (!stored)
            stored = workingInventory.TryAddToShip(blueprintItemId, GetProfileShipInventoryCapacity(CurrentProfile.ShipSkinIndex, workingInventory.EquipmentSlots), CurrentProfile.ShipSkinIndex);

        if (!stored)
            return false;

        HashSet<string> purchased = new HashSet<string>(CurrentProfile.MissEnigmaPurchasedBlueprintIds, StringComparer.Ordinal)
        {
            blueprintItemId
        };
        string[] purchasedIds = new string[purchased.Count];
        purchased.CopyTo(purchasedIds);
        Array.Sort(purchasedIds, StringComparer.Ordinal);

        CurrentProfile.Inventory = workingInventory;
        CurrentProfile.MissEnigmaPurchasedBlueprintIds = NormalizeMissEnigmaBlueprintPurchases(purchasedIds);
        await SaveInventoryAndMissEnigmaBlueprintPurchasesAsync();
        return true;
    }

    public async Task<bool> TryTradeItemsForItemAsync(string outputItemId, string[] costItemIds)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (InventoryItemCatalog.GetDefinition(outputItemId) == null || !CanAffordItemTrade(costItemIds))
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        if (!RemoveRequiredItems(workingInventory, costItemIds))
            return false;

        bool stored = workingInventory.TryAddToPlayer(outputItemId);
        if (!stored)
            stored = workingInventory.TryAddToShip(outputItemId, GetProfileShipInventoryCapacity(CurrentProfile.ShipSkinIndex, workingInventory.EquipmentSlots), CurrentProfile.ShipSkinIndex);

        if (!stored)
            return false;

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> UseBlueprintItemAsync(bool fromShipInventory, int sourceIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureBlueprintUnlocks();

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        string blueprintItemId = fromShipInventory
            ? workingInventory.RemoveFromShip(sourceIndex)
            : workingInventory.RemoveFromPlayer(sourceIndex);

        if (!InventoryItemCatalog.IsBlueprintItem(blueprintItemId))
            return false;

        if (IsBlueprintUnlocked(blueprintItemId))
            return false;

        HashSet<string> unlocked = new HashSet<string>(CurrentProfile.UnlockedBlueprintIds, StringComparer.Ordinal)
        {
            blueprintItemId
        };
        string[] unlockedIds = new string[unlocked.Count];
        unlocked.CopyTo(unlockedIds);
        Array.Sort(unlockedIds, StringComparer.Ordinal);

        CurrentProfile.Inventory = workingInventory;
        CurrentProfile.UnlockedBlueprintIds = NormalizeUnlockedBlueprintIds(unlockedIds);
        await SaveInventoryAndBlueprintsAsync();
        return true;
    }

    public async Task UnlockAllBlueprintsAsync()
    {
        await EnsureInitializedAsync();
        EnsureBlueprintUnlocks();

        CurrentProfile.UnlockedBlueprintIds = NormalizeUnlockedBlueprintIds(InventoryItemCatalog.GetAllBlueprintItemIds());
        await SaveBlueprintsAsync();
    }

    public async Task LockAllBlueprintsAsync()
    {
        await EnsureInitializedAsync();
        EnsureBlueprintUnlocks();

        CurrentProfile.UnlockedBlueprintIds = NormalizeUnlockedBlueprintIds(BlueprintCatalog.GetStarterUnlockedBlueprintItemIds());
        await SaveBlueprintsAsync();
    }

    async Task SaveInventoryAndBlueprintsAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            EnsureBlueprintUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudUnlockedBlueprintsKey] = SerializeBlueprintUnlocks(CurrentProfile.UnlockedBlueprintIds)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save inventory and blueprints");
            InventoryRevision++;
            ApplyInventoryToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService blueprint use save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveInventoryAndMissEnigmaBlueprintPurchasesAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            EnsureMissEnigmaBlueprintPurchases();

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudMissEnigmaPurchasedBlueprintsKey] = SerializeMissEnigmaBlueprintPurchases(CurrentProfile.MissEnigmaPurchasedBlueprintIds)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save inventory and Miss Enigma blueprint purchases");
            InventoryRevision++;
            ApplyInventoryToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Miss Enigma blueprint purchase save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveBlueprintsAsync()
    {
        try
        {
            IsBusy = true;
            EnsureBlueprintUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudUnlockedBlueprintsKey] = SerializeBlueprintUnlocks(CurrentProfile.UnlockedBlueprintIds)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save blueprints");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService blueprint save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    void EnsureBlueprintUnlocks()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.UnlockedBlueprintIds = NormalizeUnlockedBlueprintIds(CurrentProfile.UnlockedBlueprintIds);
    }

    void EnsureMissEnigmaBlueprintPurchases()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.MissEnigmaPurchasedBlueprintIds = NormalizeMissEnigmaBlueprintPurchases(CurrentProfile.MissEnigmaPurchasedBlueprintIds);
    }

    public static string[] NormalizeUnlockedBlueprintIds(string[] blueprintIds)
    {
        HashSet<string> normalized = new HashSet<string>(StringComparer.Ordinal);
        string[] starterBlueprintIds = BlueprintCatalog.GetStarterUnlockedBlueprintItemIds();
        for (int i = 0; i < starterBlueprintIds.Length; i++)
        {
            string starterBlueprintId = starterBlueprintIds[i];
            if (InventoryItemCatalog.IsBlueprintItem(starterBlueprintId))
                normalized.Add(starterBlueprintId);
        }

        if (blueprintIds != null)
        {
            for (int i = 0; i < blueprintIds.Length; i++)
            {
                string blueprintId = blueprintIds[i];
                if (InventoryItemCatalog.IsBlueprintItem(blueprintId))
                    normalized.Add(blueprintId);
            }
        }

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    static string[] NormalizeMissEnigmaBlueprintPurchases(string[] blueprintIds)
    {
        if (blueprintIds == null || blueprintIds.Length == 0)
            return Array.Empty<string>();

        HashSet<string> normalized = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < blueprintIds.Length; i++)
        {
            string blueprintId = blueprintIds[i];
            if (BlueprintCatalog.GetMissEnigmaOffer(blueprintId) != null)
                normalized.Add(blueprintId);
        }

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }
}
