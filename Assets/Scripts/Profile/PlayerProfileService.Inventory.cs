using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService
{
    const int DeferredInventorySaveDelayMs = 750;
    const int PlayerInventoryExtendBasePrice = 1000;
    const int PlayerInventoryExtendMaxPrice = 64000;

    public bool HasFreeShipInventorySlot(string itemId = null)
    {
        EnsureInventory();
        return CurrentProfile.Inventory.GetFirstEmptyShipSlot(GetActiveShipInventoryCapacity(), GetActiveShipSkinIndex(), itemId) >= 0;
    }

    public bool HasFreePlayerInventorySlot()
    {
        EnsureInventory();
        return CurrentProfile.Inventory.GetFirstEmptyPlayerSlot() >= 0;
    }

    public async Task ReplaceShipInventoryAsync(string[] newShipSlots)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        CurrentProfile.Inventory.SetShipSlots(newShipSlots);
        TryMoveSafePocketRestrictedItems(CurrentProfile.Inventory, GetActiveShipSkinIndex());
        await SaveInventoryOnlyAsync();
    }

    public int GetNextPlayerInventoryExtendPrice()
    {
        EnsureInventory();
        int extensions = Mathf.Max(0, (CurrentProfile.Inventory.PlayerSlots.Length - PlayerInventoryData.DefaultPlayerSlotCount) / PlayerInventoryData.PlayerSlotExtensionSize);
        int price = PlayerInventoryExtendBasePrice;
        for (int i = 0; i < extensions && price < PlayerInventoryExtendMaxPrice; i++)
            price = Mathf.Min(PlayerInventoryExtendMaxPrice, price * 2);

        return price;
    }

    public async Task<bool> TryExtendPlayerInventoryAsync()
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int price = GetNextPlayerInventoryExtendPrice();
        if (CurrentProfile.Astrons < price)
            return false;

        CurrentProfile.Astrons = Mathf.Max(0, CurrentProfile.Astrons - price);
        CurrentProfile.Inventory.ExtendPlayerSlots(PlayerInventoryData.PlayerSlotExtensionSize);
        await SaveInventoryAndAstronsAsync();
        return true;
    }

    public async Task<bool> SellInventoryItemAsync(bool fromShipInventory, int sourceIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        string soldItem = fromShipInventory
            ? CurrentProfile.Inventory.RemoveFromShip(sourceIndex)
            : CurrentProfile.Inventory.RemoveFromPlayer(sourceIndex);

        if (string.IsNullOrWhiteSpace(soldItem))
            return false;

        int value = InventoryItemCatalog.GetSellValueAstrons(soldItem);
        if (value <= 0)
        {
            RestoreInventorySource(fromShipInventory, sourceIndex, soldItem);
            return false;
        }

        EnsureCareerStats();
        CurrentProfile.Astrons = Mathf.Max(0, CurrentProfile.Astrons + value);
        long updatedSoldValue = (long)Mathf.Max(0, CurrentProfile.PilotSoldItemsAstrons) + value;
        CurrentProfile.PilotSoldItemsAstrons = updatedSoldValue > int.MaxValue ? int.MaxValue : (int)updatedSoldValue;
        CurrentProfile.CareerStats.AstronsEarned = AddClamped(CurrentProfile.CareerStats.AstronsEarned, value);

        await SaveInventoryAndAstronsAsync();
        return true;
    }


    public async Task<bool> SalvageInventoryItemAsync(bool fromShipInventory, int sourceIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureBlueprintUnlocks();

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        string sourceItemPreview = fromShipInventory
            ? GetSlotItem(workingInventory.ShipSlots, sourceIndex)
            : GetSlotItem(workingInventory.PlayerSlots, sourceIndex);

        string[] salvageOutputs = InventoryItemCatalog.IsBlueprintItem(sourceItemPreview)
            ? IsBlueprintUnlocked(sourceItemPreview)
                ? new[] { InventoryItemCatalog.BlueprintScrapId }
                : Array.Empty<string>()
            : InventoryItemCatalog.RollSalvageOutputs(sourceItemPreview);
        if (string.IsNullOrWhiteSpace(sourceItemPreview) || salvageOutputs == null || salvageOutputs.Length == 0)
            return false;

        string sourceItem = fromShipInventory
            ? workingInventory.RemoveFromShip(sourceIndex)
            : workingInventory.RemoveFromPlayer(sourceIndex);

        if (string.IsNullOrWhiteSpace(sourceItem))
            return false;

        for (int i = 0; i < salvageOutputs.Length; i++)
        {
            string output = salvageOutputs[i];
            bool added = fromShipInventory
                ? workingInventory.TryAddToShip(output, GetProfileShipInventoryCapacity(CurrentProfile.ShipSkinIndex, workingInventory.EquipmentSlots), CurrentProfile.ShipSkinIndex)
                : workingInventory.TryAddToPlayer(output);

            if (!added)
                return false;
        }

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> AddItemToShipAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int shipSkinIndex = GetActiveShipSkinIndex();
        if (!CurrentProfile.Inventory.TryAddToShip(itemId, GetActiveShipInventoryCapacity(), shipSkinIndex))
            return false;

        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> AddItemToShipDeferredSaveAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int shipSkinIndex = GetActiveShipSkinIndex();
        if (!CurrentProfile.Inventory.TryAddToShip(itemId, GetActiveShipInventoryCapacity(), shipSkinIndex))
            return false;

        MarkInventoryChangedDeferred();
        return true;
    }

    public async Task<bool> AddItemToPlayerDeferredSaveAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (!CurrentProfile.Inventory.TryAddToPlayer(itemId))
            return false;

        MarkInventoryChangedDeferred();
        return true;
    }

    public async Task<bool> AddRandomLootEquipmentAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        int shipSkinIndex = GetActiveShipSkinIndex();

        int equipmentSlot = GetFirstFreeEquipmentSlotByCategoryForProfile(workingInventory.EquipmentSlots, shipSkinIndex, definition.Category);
        if (equipmentSlot >= 0)
        {
            if (!IsSingleInstallItem(itemId) || !IsItemAlreadyEquipped(workingInventory.EquipmentSlots, itemId, equipmentSlot))
            {
                workingInventory.SetEquipment(equipmentSlot, itemId);
                CurrentProfile.Inventory = workingInventory;
                await SaveInventoryOnlyAsync();
                return true;
            }
        }

        if (!workingInventory.TryAddToShip(itemId, GetProfileShipInventoryCapacity(shipSkinIndex, workingInventory.EquipmentSlots), shipSkinIndex))
            return false;

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> AddRandomLootEquipmentDeferredSaveAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        int shipSkinIndex = GetActiveShipSkinIndex();

        int equipmentSlot = GetFirstFreeEquipmentSlotByCategoryForProfile(workingInventory.EquipmentSlots, shipSkinIndex, definition.Category);
        if (equipmentSlot >= 0)
        {
            if (!IsSingleInstallItem(itemId) || !IsItemAlreadyEquipped(workingInventory.EquipmentSlots, itemId, equipmentSlot))
            {
                workingInventory.SetEquipment(equipmentSlot, itemId);
                CurrentProfile.Inventory = workingInventory;
                MarkInventoryChangedDeferred();
                return true;
            }
        }

        if (!workingInventory.TryAddToShip(itemId, GetProfileShipInventoryCapacity(shipSkinIndex, workingInventory.EquipmentSlots), shipSkinIndex))
            return false;

        CurrentProfile.Inventory = workingInventory;
        MarkInventoryChangedDeferred();
        return true;
    }


    public async Task<string> RemoveShipItemAtAsync(int index)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        string removedItem = CurrentProfile.Inventory.RemoveFromShip(index);
        if (string.IsNullOrWhiteSpace(removedItem))
            return null;

        if (string.Equals(removedItem, InventoryItemCatalog.ShipPrototypeDocumentationId, StringComparison.Ordinal))
        {
            CurrentProfile.PathfinderResearchProgress = PathfinderResearchProgressData.Empty();
            await SaveInventoryAndPathfinderResearchProgressAsync("drop Pathfinder documentation");
            return removedItem;
        }

        await SaveInventoryOnlyAsync();
        return removedItem;
    }

    public async Task<string> RemoveDropbotCargoItemDeferredSaveAsync(int slotIndex, string expectedItemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (string.IsNullOrWhiteSpace(expectedItemId) || !HasFreePlayerInventorySlot())
            return null;

        int shipSkinIndex = GetActiveShipSkinIndex();
        int capacity = GetActiveShipInventoryCapacity();
        if (!IsDropbotCargoIndex(shipSkinIndex, capacity, slotIndex, CurrentProfile.Inventory.EquipmentSlots))
            return null;

        if (slotIndex < 0 || slotIndex >= capacity || slotIndex >= CurrentProfile.Inventory.ShipSlots.Length)
            return null;

        string removedItem = CurrentProfile.Inventory.ShipSlots[slotIndex];
        if (!string.Equals(removedItem, expectedItemId, StringComparison.Ordinal))
            return null;

        CurrentProfile.Inventory.ShipSlots[slotIndex] = null;
        MarkInventoryChangedDeferred();
        return removedItem;
    }

    public async Task<string> RemoveLootHookCargoItemDeferredSaveAsync(int slotIndex, string expectedItemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (string.IsNullOrWhiteSpace(expectedItemId))
            return null;

        int shipSkinIndex = GetActiveShipSkinIndex();
        int capacity = GetActiveShipInventoryCapacity();
        if (slotIndex < 0 || slotIndex >= capacity || slotIndex >= CurrentProfile.Inventory.ShipSlots.Length)
            return null;

        if (IsSafePocketIndex(shipSkinIndex, slotIndex) || IsAstronautCargoIndex(shipSkinIndex, capacity, slotIndex))
            return null;

        string removedItem = CurrentProfile.Inventory.ShipSlots[slotIndex];
        if (!string.Equals(removedItem, expectedItemId, StringComparison.Ordinal))
            return null;

        CurrentProfile.Inventory.ShipSlots[slotIndex] = null;
        MarkInventoryChangedDeferred();
        return removedItem;
    }

    public async Task<string> ReplaceShipItemDeferredSaveAsync(int index, string newItemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (string.IsNullOrWhiteSpace(newItemId))
            return null;

        int capacity = GetActiveShipInventoryCapacity();
        int shipSkinIndex = GetActiveShipSkinIndex();
        if (index < 0 || index >= capacity || index >= CurrentProfile.Inventory.ShipSlots.Length)
            return null;

        if (!CanStoreItemInShipSlot(newItemId, shipSkinIndex, index))
            return null;

        string replacedItem = CurrentProfile.Inventory.ShipSlots[index];
        if (string.IsNullOrWhiteSpace(replacedItem))
            return null;

        CurrentProfile.Inventory.ShipSlots[index] = newItemId;
        MarkInventoryChangedDeferred();
        return replacedItem;
    }

    public async Task<string> RemoveFirstShipContainerAsync()
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int capacity = GetActiveShipInventoryCapacity();
        CurrentProfile.Inventory.Normalize();
        for (int i = 0; i < CurrentProfile.Inventory.ShipSlots.Length && i < capacity; i++)
        {
            string itemId = CurrentProfile.Inventory.ShipSlots[i];
            if (!InventoryItemCatalog.IsContainerItem(itemId))
                continue;

            CurrentProfile.Inventory.ShipSlots[i] = null;
            await SaveInventoryOnlyAsync();
            return itemId;
        }

        return null;
    }

    public async Task<string> RemoveFirstShipContainerDeferredSaveAsync()
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int capacity = GetActiveShipInventoryCapacity();
        CurrentProfile.Inventory.Normalize();
        for (int i = 0; i < CurrentProfile.Inventory.ShipSlots.Length && i < capacity; i++)
        {
            string itemId = CurrentProfile.Inventory.ShipSlots[i];
            if (!InventoryItemCatalog.IsContainerItem(itemId))
                continue;

            CurrentProfile.Inventory.ShipSlots[i] = null;
            MarkInventoryChangedDeferred();
            return itemId;
        }

        return null;
    }

    public async Task<string[]> RemoveShipContainersDeferredSaveAsync(int maxCount)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int targetCount = Mathf.Clamp(maxCount, 1, PlayerInventoryData.ShipSlotCount);
        List<string> removedItems = new List<string>(targetCount);
        int capacity = GetActiveShipInventoryCapacity();
        CurrentProfile.Inventory.Normalize();
        for (int i = 0; i < CurrentProfile.Inventory.ShipSlots.Length && i < capacity; i++)
        {
            string itemId = CurrentProfile.Inventory.ShipSlots[i];
            if (!InventoryItemCatalog.IsContainerItem(itemId))
                continue;

            CurrentProfile.Inventory.ShipSlots[i] = null;
            removedItems.Add(itemId);
            if (removedItems.Count >= targetCount)
                break;
        }

        if (removedItems.Count > 0)
            MarkInventoryChangedDeferred();

        return removedItems.ToArray();
    }

    public async Task<string> RemoveFirstShipItemDeferredSaveAsync(string matchItemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (string.IsNullOrWhiteSpace(matchItemId))
            return null;

        int capacity = GetActiveShipInventoryCapacity();
        CurrentProfile.Inventory.Normalize();
        for (int i = 0; i < CurrentProfile.Inventory.ShipSlots.Length && i < capacity; i++)
        {
            string itemId = CurrentProfile.Inventory.ShipSlots[i];
            if (!InventoryItemCatalog.MatchesItemRequirement(itemId, matchItemId))
                continue;

            CurrentProfile.Inventory.ShipSlots[i] = null;
            MarkInventoryChangedDeferred();
            return itemId;
        }

        return null;
    }

    public async Task<int> AddItemsToShipDeferredSaveAsync(string[] itemIds)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (itemIds == null || itemIds.Length == 0)
            return 0;

        int shipSkinIndex = GetActiveShipSkinIndex();
        int addedCount = 0;
        for (int i = 0; i < itemIds.Length; i++)
        {
            string itemId = itemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (CurrentProfile.Inventory.TryAddToShip(itemId, GetActiveShipInventoryCapacity(), shipSkinIndex))
                addedCount++;
        }

        if (addedCount > 0)
            MarkInventoryChangedDeferred();

        return addedCount;
    }

    public async Task RestoreShipItemAtAsync(int index, string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (string.IsNullOrWhiteSpace(itemId))
            return;

        int shipSkinIndex = GetActiveShipSkinIndex();
        if (CanStoreItemInShipSlot(itemId, shipSkinIndex, index))
            CurrentProfile.Inventory.RestoreShip(index, itemId);
        else if (!CurrentProfile.Inventory.TryAddToShip(itemId, GetActiveShipInventoryCapacity(), shipSkinIndex))
            return;

        await SaveInventoryOnlyAsync();
    }

    public async Task SaveInventorySnapshotAsync(PlayerInventoryData inventory)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        CurrentProfile.Inventory = inventory != null ? inventory.Clone() : PlayerInventoryData.Default();
        TryMoveSafePocketRestrictedItems(CurrentProfile.Inventory, GetActiveShipSkinIndex());
        await SaveInventoryOnlyAsync();
    }

    async Task SaveInventoryOnlyAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save inventory");
            InventoryRevision++;
            ApplyInventoryToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService inventory save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    void MarkInventoryChangedDeferred()
    {
        EnsureInventory();
        InventoryRevision++;
        ApplyInventoryToPhoton();
        ScheduleDeferredInventorySave();
    }

    void ScheduleDeferredInventorySave()
    {
        deferredInventorySavePending = true;
        int version = ++deferredInventorySaveVersion;
        _ = SaveInventoryAfterDebounceAsync(version);
    }

    async Task SaveInventoryAfterDebounceAsync(int version)
    {
        await Task.Delay(DeferredInventorySaveDelayMs);
        if (version != deferredInventorySaveVersion || !deferredInventorySavePending)
            return;

        deferredInventorySavePending = false;
        try
        {
            await SaveInventoryOnlyAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService deferred inventory save failed: " + ex);
        }
    }


    PlayerProfileData BuildFallbackProfile()
    {
        string suffix = "0000";
        string playerId = TryGetAuthenticationPlayerId();

        if (!string.IsNullOrWhiteSpace(playerId))
        {
            suffix = playerId.Length <= 4 ? playerId : playerId.Substring(playerId.Length - 4);
        }

        return new PlayerProfileData
        {
            Nickname = "Pilot " + suffix.ToUpperInvariant(),
            ShipSkinIndex = 0,
            GamesPlayed = 0,
            TotalXp = 0,
            Astrons = DefaultStartingAstrons,
            Inventory = PlayerInventoryData.Default(),
            SelectedPilotId = PilotCatalog.JakeId,
            UnlockedPilotIds = PilotCatalog.GetDefaultUnlockedPilotIds(),
            UnlockedBlueprintIds = NormalizeUnlockedBlueprintIds(null),
            UnlockedShipIds = ShipCatalog.GetDefaultUnlockedShipTypeIds(),
            AvengerTheftAttempt = AvengerTheftAttemptData.Empty(),
            ViperRecoveryProgress = ViperRecoveryProgressData.Empty(),
            ArrowLicenseProgress = ArrowLicenseProgressData.Empty(),
            PathfinderResearchProgress = PathfinderResearchProgressData.Empty(),
            MissEnigmaPurchasedBlueprintIds = Array.Empty<string>(),
            PilotDroneKills = 0,
            PilotSoldItemsAstrons = 0,
            PilotPirateBayReturns = 0,
            PilotAsteroidSalvageCount = 0,
            PilotAshOverloadReturns = 0,
            PilotAtlasMapReturns = Array.Empty<string>(),
            MapUnlockProgress = NormalizeMapUnlockProgress(null),
            ProjectProgress = ProjectCatalog.NormalizeProgress(null),
            CareerStats = PlayerCareerStatsData.Empty()
        };
    }


    void EnsureInventory()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        if (CurrentProfile.Inventory == null)
            CurrentProfile.Inventory = PlayerInventoryData.Default();

        CurrentProfile.Inventory.Normalize();
        TryMoveSafePocketRestrictedItems(CurrentProfile.Inventory, CurrentProfile.ShipSkinIndex);
        TryMoveIncompatibleEquipmentItems(CurrentProfile.Inventory, CurrentProfile.ShipSkinIndex);
    }


}
