using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService
{
    public bool HasAvengerStartingCodesInShip()
    {
        EnsureInventory();
        return CountItemInSlots(CurrentProfile.Inventory.ShipSlots, GetActiveShipInventoryCapacity(), InventoryItemCatalog.AvengerStartingCodesId) > 0;
    }

    public async Task<bool> BeginAvengerTheftAttemptAsync(int originalShipSkinIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureShipUnlocks();

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        int safeOriginalSkinIndex = Mathf.Clamp(originalShipSkinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex);
        int shipCapacity = GetEffectiveShipInventoryCapacity(safeOriginalSkinIndex, workingInventory.EquipmentSlots);
        int cargoLimit = Mathf.Clamp(shipCapacity, 0, workingInventory.ShipSlots.Length);
        int codeSlotIndex = FindItemInSlots(workingInventory.ShipSlots, cargoLimit, InventoryItemCatalog.AvengerStartingCodesId);
        if (codeSlotIndex < 0)
            return false;

        int refundAstrons = 0;
        for (int i = 0; i < cargoLimit; i++)
        {
            string itemId = workingInventory.ShipSlots[i];
            if (!string.IsNullOrWhiteSpace(itemId) &&
                !string.Equals(itemId, InventoryItemCatalog.AvengerStartingCodesId, StringComparison.Ordinal))
            {
                refundAstrons = AddItemValueClamped(refundAstrons, itemId);
            }

            workingInventory.ShipSlots[i] = null;
        }

        for (int i = 0; i < workingInventory.EquipmentSlots.Length; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, safeOriginalSkinIndex))
                continue;

            string itemId = workingInventory.EquipmentSlots[i];
            if (!string.IsNullOrWhiteSpace(itemId))
                refundAstrons = AddItemValueClamped(refundAstrons, itemId);

            workingInventory.EquipmentSlots[i] = null;
        }

        CurrentProfile.Inventory = workingInventory;
        CurrentProfile.AvengerTheftAttempt = new AvengerTheftAttemptData
        {
            Active = true,
            RefundAstrons = Mathf.Max(0, refundAstrons),
            OriginalShipSkinIndex = safeOriginalSkinIndex
        };

        await SaveInventoryAndAvengerTheftAttemptAsync();
        return true;
    }

    public async Task<bool> CompleteAvengerTheftAttemptAsync(int returningShipSkinIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureShipUnlocks();

        AvengerTheftAttemptData attempt = NormalizeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt);
        if (!attempt.Active)
            return false;

        if (ShipCatalog.GetShipTypeFromSkinIndex(returningShipSkinIndex) != ShipType.Avenger)
        {
            CurrentProfile.AvengerTheftAttempt = AvengerTheftAttemptData.Empty();
            await SaveAvengerTheftAttemptOnlyAsync();
            return false;
        }

        int refund = Mathf.Max(0, attempt.RefundAstrons);
        if (refund > 0)
        {
            long updatedAstrons = (long)Mathf.Max(0, CurrentProfile.Astrons) + refund;
            CurrentProfile.Astrons = updatedAstrons > int.MaxValue ? int.MaxValue : (int)updatedAstrons;
        }

        HashSet<string> ids = new HashSet<string>(CurrentProfile.UnlockedShipIds, StringComparer.Ordinal)
        {
            ShipCatalog.GetShipTypeId(ShipType.Avenger)
        };
        string[] unlockedIds = new string[ids.Count];
        ids.CopyTo(unlockedIds);
        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIds(unlockedIds);
        CurrentProfile.ShipSkinIndex = ShipCatalog.AvengerDarkGreenSkinIndex;
        CurrentProfile.AvengerTheftAttempt = AvengerTheftAttemptData.Empty();

        await SaveAvengerTheftCompletionAsync();
        return true;
    }

    public async Task FailAvengerTheftAttemptAsync()
    {
        await EnsureInitializedAsync();
        if (CurrentProfile == null || CurrentProfile.AvengerTheftAttempt == null || !CurrentProfile.AvengerTheftAttempt.Active)
            return;

        CurrentProfile.AvengerTheftAttempt = AvengerTheftAttemptData.Empty();
        await SaveAvengerTheftAttemptOnlyAsync();
    }

    async Task SaveInventoryAndAvengerTheftAttemptAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            CurrentProfile.AvengerTheftAttempt = NormalizeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt);

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudAvengerTheftAttemptKey] = SerializeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save Avenger theft attempt");
            InventoryRevision++;
            ApplyInventoryToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Avenger theft attempt save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveAvengerTheftCompletionAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            EnsureShipUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudShipSkinKey] = CurrentProfile.ShipSkinIndex,
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudAstronsKey] = CurrentProfile.Astrons,
                [CloudUnlockedShipsKey] = SerializeShipUnlocks(CurrentProfile.UnlockedShipIds),
                [CloudAvengerTheftAttemptKey] = SerializeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt),
                [CloudViperRecoveryProgressKey] = SerializeViperRecoveryProgress(CurrentProfile.ViperRecoveryProgress),
                [CloudArrowLicenseProgressKey] = SerializeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress),
                [CloudBisonIndustrialPartsDeliveredKey] = CurrentProfile.BisonIndustrialPartsDelivered,
                [CloudInvaderImprintsRecoveredKey] = CurrentProfile.InvaderImprintsRecovered
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save Avenger theft completion");
            InventoryRevision++;
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Avenger theft completion save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveAvengerTheftAttemptOnlyAsync()
    {
        try
        {
            IsBusy = true;
            CurrentProfile.AvengerTheftAttempt = NormalizeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt);

            var data = new Dictionary<string, object>
            {
                [CloudAvengerTheftAttemptKey] = SerializeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "clear Avenger theft attempt");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Avenger theft attempt clear failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    static AvengerTheftAttemptData NormalizeAvengerTheftAttempt(AvengerTheftAttemptData attempt)
    {
        if (attempt == null || !attempt.Active)
            return AvengerTheftAttemptData.Empty();

        return new AvengerTheftAttemptData
        {
            Active = true,
            RefundAstrons = Mathf.Max(0, attempt.RefundAstrons),
            OriginalShipSkinIndex = Mathf.Clamp(attempt.OriginalShipSkinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex)
        };
    }
}
