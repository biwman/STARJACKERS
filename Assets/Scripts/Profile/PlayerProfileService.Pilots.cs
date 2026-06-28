using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService : MonoBehaviour
{
    const int DeferredPilotAsteroidSalvageSaveDelayMs = 5000;

    public bool IsPilotUnlocked(string pilotId)
    {
        EnsurePilotDefaults();
        return PilotCatalog.IsPilotUnlocked(CurrentProfile, pilotId);
    }

    public async Task<bool> TryChangePilotAsync(string pilotId)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        string normalizedPilotId = PilotCatalog.NormalizePilotId(pilotId);
        if (!PilotCatalog.IsPilotUnlocked(CurrentProfile, normalizedPilotId))
            return false;

        if (string.Equals(CurrentProfile.SelectedPilotId, normalizedPilotId, StringComparison.Ordinal))
            return true;

        CurrentProfile.SelectedPilotId = normalizedPilotId;
        await SavePilotStateAsync();
        return true;
    }

    public async Task<bool> UnlockPilotAsync(string pilotId)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        string normalizedPilotId = PilotCatalog.NormalizePilotId(pilotId);
        if (normalizedPilotId == PilotCatalog.JakeId)
            return false;

        if (PilotCatalog.IsPilotUnlocked(CurrentProfile, normalizedPilotId))
            return false;

        HashSet<string> ids = new HashSet<string>(PilotCatalog.NormalizeUnlockedPilotIds(CurrentProfile.UnlockedPilotIds), StringComparer.Ordinal);
        ids.Add(normalizedPilotId);
        CurrentProfile.UnlockedPilotIds = new string[ids.Count];
        ids.CopyTo(CurrentProfile.UnlockedPilotIds);
        Array.Sort(CurrentProfile.UnlockedPilotIds, StringComparer.Ordinal);

        await SavePilotStateAsync();
        return true;
    }

    public async Task<int> RecordPilotDroneKillAsync(int amount = 1)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        int increment = Mathf.Max(1, amount);
        CurrentProfile.PilotDroneKills = Mathf.Max(0, CurrentProfile.PilotDroneKills) + increment;

        try
        {
            IsBusy = true;
            var data = new Dictionary<string, object>
            {
                [CloudPilotDroneKillsKey] = CurrentProfile.PilotDroneKills
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save pilot drone kills");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            CurrentProfile.PilotDroneKills = Mathf.Max(0, CurrentProfile.PilotDroneKills - increment);
            Debug.LogError("PlayerProfileService pilot drone kill save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }

        return CurrentProfile.PilotDroneKills;
    }

    public async Task<int> RecordPilotPirateBayReturnAsync(int amount = 1)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        int increment = Mathf.Max(1, amount);
        int previousReturns = Mathf.Max(0, CurrentProfile.PilotPirateBayReturns);
        long updatedReturns = (long)previousReturns + increment;
        CurrentProfile.PilotPirateBayReturns = updatedReturns > int.MaxValue ? int.MaxValue : (int)updatedReturns;

        try
        {
            IsBusy = true;
            var data = new Dictionary<string, object>
            {
                [CloudPilotPirateBayReturnsKey] = CurrentProfile.PilotPirateBayReturns
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save pilot Pirate Bay returns");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            CurrentProfile.PilotPirateBayReturns = previousReturns;
            Debug.LogError("PlayerProfileService pilot Pirate Bay return save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }

        return CurrentProfile.PilotPirateBayReturns;
    }

    public async Task<int> RecordPilotAsteroidSalvageAsync(int amount = 1)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        int increment = Mathf.Max(1, amount);
        int previousCount = Mathf.Max(0, CurrentProfile.PilotAsteroidSalvageCount);
        long updatedCount = (long)previousCount + increment;
        CurrentProfile.PilotAsteroidSalvageCount = updatedCount > int.MaxValue ? int.MaxValue : (int)updatedCount;

        if (IsGameplaySessionActive())
        {
            ScheduleDeferredPilotAsteroidSalvageSave();
            NotifyProfileChanged();
            return CurrentProfile.PilotAsteroidSalvageCount;
        }

        try
        {
            await SavePilotAsteroidSalvageCountOnlyAsync();
        }
        catch (Exception ex)
        {
            CurrentProfile.PilotAsteroidSalvageCount = previousCount;
            Debug.LogError("PlayerProfileService pilot asteroid salvage save failed: " + ex);
            throw;
        }

        return CurrentProfile.PilotAsteroidSalvageCount;
    }

    void ScheduleDeferredPilotAsteroidSalvageSave()
    {
        deferredPilotAsteroidSalvageSavePending = true;
        int version = ++deferredPilotAsteroidSalvageSaveVersion;
        _ = SavePilotAsteroidSalvageAfterDebounceAsync(version);
    }

    async Task SavePilotAsteroidSalvageAfterDebounceAsync(int version)
    {
        await Task.Delay(DeferredPilotAsteroidSalvageSaveDelayMs);
        if (version != deferredPilotAsteroidSalvageSaveVersion ||
            !deferredPilotAsteroidSalvageSavePending)
        {
            return;
        }

        await SaveDeferredPilotAsteroidSalvageAsync();
    }

    async Task SaveDeferredPilotAsteroidSalvageAsync()
    {
        if (!deferredPilotAsteroidSalvageSavePending)
            return;

        if (pilotAsteroidSalvageSaveInProgress)
        {
            ScheduleDeferredPilotAsteroidSalvageSave();
            return;
        }

        deferredPilotAsteroidSalvageSavePending = false;
        pilotAsteroidSalvageSaveInProgress = true;
        try
        {
            await SavePilotAsteroidSalvageCountOnlyAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService deferred pilot asteroid salvage save failed: " + ex);
            ScheduleDeferredPilotAsteroidSalvageSave();
        }
        finally
        {
            pilotAsteroidSalvageSaveInProgress = false;
        }
    }

    async Task SavePilotAsteroidSalvageCountOnlyAsync()
    {
        try
        {
            IsBusy = true;
            var data = new Dictionary<string, object>
            {
                [CloudPilotAsteroidSalvageCountKey] = CurrentProfile.PilotAsteroidSalvageCount
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save pilot asteroid salvage");
            NotifyProfileChanged();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<int> RecordPilotAshOverloadReturnAsync(int amount = 1)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        int increment = Mathf.Max(1, amount);
        int previousReturns = Mathf.Max(0, CurrentProfile.PilotAshOverloadReturns);
        long updatedReturns = (long)previousReturns + increment;
        CurrentProfile.PilotAshOverloadReturns = updatedReturns > int.MaxValue ? int.MaxValue : (int)updatedReturns;

        try
        {
            IsBusy = true;
            var data = new Dictionary<string, object>
            {
                [CloudPilotAshOverloadReturnsKey] = CurrentProfile.PilotAshOverloadReturns
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save pilot Ash overload returns");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            CurrentProfile.PilotAshOverloadReturns = previousReturns;
            Debug.LogError("PlayerProfileService pilot Ash overload return save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }

        return CurrentProfile.PilotAshOverloadReturns;
    }

    public async Task<string[]> RecordPilotAtlasMapReturnAsync(string mapId)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        string normalizedMapId = PilotCatalog.NormalizeAtlasMapId(mapId);
        if (string.IsNullOrWhiteSpace(normalizedMapId))
            return CurrentProfile.PilotAtlasMapReturns;

        string[] previousReturns = PilotCatalog.NormalizeAtlasMapReturnIds(CurrentProfile.PilotAtlasMapReturns);
        for (int i = 0; i < previousReturns.Length; i++)
        {
            if (string.Equals(previousReturns[i], normalizedMapId, StringComparison.Ordinal))
                return previousReturns;
        }

        string[] expanded = new string[previousReturns.Length + 1];
        Array.Copy(previousReturns, expanded, previousReturns.Length);
        expanded[expanded.Length - 1] = normalizedMapId;
        CurrentProfile.PilotAtlasMapReturns = PilotCatalog.NormalizeAtlasMapReturnIds(expanded);

        try
        {
            IsBusy = true;
            var data = new Dictionary<string, object>
            {
                [CloudPilotAtlasMapReturnsKey] = SerializeAtlasMapReturns(CurrentProfile.PilotAtlasMapReturns)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save pilot Atlas map returns");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            CurrentProfile.PilotAtlasMapReturns = previousReturns;
            Debug.LogError("PlayerProfileService pilot Atlas map return save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }

        return CurrentProfile.PilotAtlasMapReturns;
    }

    async Task SavePilotStateAsync()
    {
        try
        {
            IsBusy = true;
            EnsurePilotDefaults();

            var data = new Dictionary<string, object>
            {
                [CloudSelectedPilotKey] = CurrentProfile.SelectedPilotId,
                [CloudUnlockedPilotsKey] = SerializePilotUnlocks(CurrentProfile.UnlockedPilotIds),
                [CloudPilotDroneKillsKey] = CurrentProfile.PilotDroneKills,
                [CloudPilotSoldItemsAstronsKey] = CurrentProfile.PilotSoldItemsAstrons,
                [CloudPilotPirateBayReturnsKey] = CurrentProfile.PilotPirateBayReturns,
                [CloudPilotAsteroidSalvageCountKey] = CurrentProfile.PilotAsteroidSalvageCount,
                [CloudPilotAshOverloadReturnsKey] = CurrentProfile.PilotAshOverloadReturns,
                [CloudPilotAtlasMapReturnsKey] = SerializeAtlasMapReturns(CurrentProfile.PilotAtlasMapReturns)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save pilot state");
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService pilot save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    void EnsurePilotDefaults()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.UnlockedPilotIds = PilotCatalog.NormalizeUnlockedPilotIds(CurrentProfile.UnlockedPilotIds);
        CurrentProfile.PilotDroneKills = Mathf.Max(0, CurrentProfile.PilotDroneKills);
        CurrentProfile.PilotSoldItemsAstrons = Mathf.Max(0, CurrentProfile.PilotSoldItemsAstrons);
        CurrentProfile.PilotPirateBayReturns = Mathf.Max(0, CurrentProfile.PilotPirateBayReturns);
        CurrentProfile.PilotAsteroidSalvageCount = Mathf.Max(0, CurrentProfile.PilotAsteroidSalvageCount);
        CurrentProfile.PilotAshOverloadReturns = Mathf.Max(0, CurrentProfile.PilotAshOverloadReturns);
        CurrentProfile.PilotAtlasMapReturns = PilotCatalog.NormalizeAtlasMapReturnIds(CurrentProfile.PilotAtlasMapReturns);
        CurrentProfile.SelectedPilotId = PilotCatalog.NormalizePilotId(CurrentProfile.SelectedPilotId);
        if (!PilotCatalog.IsPilotUnlocked(CurrentProfile, CurrentProfile.SelectedPilotId))
            CurrentProfile.SelectedPilotId = PilotCatalog.JakeId;
    }
}
