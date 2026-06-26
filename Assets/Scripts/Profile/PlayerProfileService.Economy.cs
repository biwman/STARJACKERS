using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService
{
    public const int DefaultStartingAstrons = 3000;

    public async Task AddAstronsAsync(int amount)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int normalizedAmount = Mathf.Max(0, amount);
        if (normalizedAmount <= 0)
            return;

        EnsureCareerStats();
        long updatedAstrons = (long)Mathf.Max(0, CurrentProfile.Astrons) + normalizedAmount;
        CurrentProfile.Astrons = updatedAstrons > int.MaxValue ? int.MaxValue : (int)updatedAstrons;
        CurrentProfile.CareerStats.AstronsEarned = AddClamped(CurrentProfile.CareerStats.AstronsEarned, normalizedAmount);
        await SaveInventoryAndAstronsAsync();
    }

    async Task SaveInventoryAndAstronsAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            EnsurePilotDefaults();
            EnsureCareerStats();
            EnsureMissEnigmaUniqueItemRecoveries();

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudAstronsKey] = CurrentProfile.Astrons,
                [CloudPilotSoldItemsAstronsKey] = CurrentProfile.PilotSoldItemsAstrons,
                [CloudCareerStatsKey] = SerializeCareerStats(CurrentProfile.CareerStats),
                [CloudMissEnigmaRecoverableUniqueItemsKey] = SerializeMissEnigmaUniqueItemRecoveries(CurrentProfile.MissEnigmaRecoverableUniqueItemIds)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save inventory and astrons");
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService sell save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
