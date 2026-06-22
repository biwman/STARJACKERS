using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService
{
    public LobbyMapUnlockStatus GetMapUnlockStatus(string mapId)
    {
        EnsureMapUnlockProgress();
        int totalXp = CurrentProfile != null ? CurrentProfile.TotalXp : 0;
        return LobbyMapUnlockCatalog.GetStatus(mapId, CurrentProfile != null ? CurrentProfile.MapUnlockProgress : null, totalXp);
    }

    public bool IsMapUnlocked(string mapId)
    {
        LobbyMapUnlockStatus status = GetMapUnlockStatus(mapId);
        return status != null && status.IsUnlocked;
    }

    public int GetMapSuccessfulReturnCount(string mapId)
    {
        EnsureMapUnlockProgress();
        return LobbyMapUnlockCatalog.GetReturnCount(CurrentProfile != null ? CurrentProfile.MapUnlockProgress : null, mapId);
    }

    public async Task<int> RecordMapSuccessfulReturnAsync(string mapId, string outcome, string matchToken)
    {
        await EnsureInitializedAsync();
        EnsureMapUnlockProgress();

        string normalizedMapId = LobbyMapUnlockCatalog.NormalizeMapId(mapId);
        if (string.IsNullOrWhiteSpace(normalizedMapId))
            return 0;

        if (!IsSuccessfulMapReturnOutcome(outcome))
            return LobbyMapUnlockCatalog.GetReturnCount(CurrentProfile.MapUnlockProgress, normalizedMapId);

        string token = BuildMapProgressToken(normalizedMapId, matchToken);
        if (awardedMapReturnTokens.Contains(token))
            return LobbyMapUnlockCatalog.GetReturnCount(CurrentProfile.MapUnlockProgress, normalizedMapId);

        PlayerMapUnlockProgressData previousProgress = CurrentProfile.MapUnlockProgress.Clone();
        int previousCount = LobbyMapUnlockCatalog.GetReturnCount(CurrentProfile.MapUnlockProgress, normalizedMapId);
        long updatedCount = (long)previousCount + 1;
        CurrentProfile.MapUnlockProgress.SetReturnCount(normalizedMapId, updatedCount > int.MaxValue ? int.MaxValue : (int)updatedCount);
        CurrentProfile.MapUnlockProgress = NormalizeMapUnlockProgress(CurrentProfile.MapUnlockProgress);
        awardedMapReturnTokens.Add(token);

        try
        {
            await SaveMapUnlockProgressAsync("save map return progress");
        }
        catch (Exception ex)
        {
            CurrentProfile.MapUnlockProgress = previousProgress;
            awardedMapReturnTokens.Remove(token);
            Debug.LogError("PlayerProfileService map return progress save failed: " + ex);
            throw;
        }

        return LobbyMapUnlockCatalog.GetReturnCount(CurrentProfile.MapUnlockProgress, normalizedMapId);
    }

    public async Task RecordMothershipKillAsync()
    {
        await EnsureInitializedAsync();
        EnsureMapUnlockProgress();

        if (CurrentProfile.MapUnlockProgress.MothershipKilled)
            return;

        PlayerMapUnlockProgressData previousProgress = CurrentProfile.MapUnlockProgress.Clone();
        CurrentProfile.MapUnlockProgress.MothershipKilled = true;

        try
        {
            await SaveMapUnlockProgressAsync("save Mothership map unlock progress");
        }
        catch (Exception ex)
        {
            CurrentProfile.MapUnlockProgress = previousProgress;
            Debug.LogError("PlayerProfileService Mothership progress save failed: " + ex);
            throw;
        }
    }

    public async Task UnlockAllMapsAsync()
    {
        await EnsureInitializedAsync();
        EnsureMapUnlockProgress();

        if (CurrentProfile.MapUnlockProgress.CheatUnlockAllMaps)
            return;

        PlayerMapUnlockProgressData previousProgress = CurrentProfile.MapUnlockProgress.Clone();
        CurrentProfile.MapUnlockProgress.CheatUnlockAllMaps = true;

        try
        {
            await SaveMapUnlockProgressAsync("save unlock all maps cheat");
        }
        catch (Exception ex)
        {
            CurrentProfile.MapUnlockProgress = previousProgress;
            Debug.LogError("PlayerProfileService unlock all maps cheat failed: " + ex);
            throw;
        }
    }

    public async Task LockAllMapsAsync()
    {
        await EnsureInitializedAsync();

        PlayerMapUnlockProgressData previousProgress = CurrentProfile.MapUnlockProgress != null
            ? CurrentProfile.MapUnlockProgress.Clone()
            : NormalizeMapUnlockProgress(null);
        CurrentProfile.MapUnlockProgress = NormalizeMapUnlockProgress(null);
        awardedMapReturnTokens.Clear();

        try
        {
            await SaveMapUnlockProgressAsync("save lock all maps cheat");
        }
        catch (Exception ex)
        {
            CurrentProfile.MapUnlockProgress = previousProgress;
            Debug.LogError("PlayerProfileService lock all maps cheat failed: " + ex);
            throw;
        }
    }

    async Task SaveMapUnlockProgressAsync(string operationName)
    {
        try
        {
            IsBusy = true;
            EnsureMapUnlockProgress();

            var data = new Dictionary<string, object>
            {
                [CloudMapUnlockProgressKey] = SerializeMapUnlockProgress(CurrentProfile.MapUnlockProgress)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                operationName);
            NotifyProfileChanged();
        }
        finally
        {
            IsBusy = false;
        }
    }

    static bool IsSuccessfulMapReturnOutcome(string outcome)
    {
        return string.Equals(outcome, "extracted", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(outcome, "evacuated", StringComparison.OrdinalIgnoreCase);
    }

    static string BuildMapProgressToken(string mapId, string matchToken)
    {
        string normalizedMapId = LobbyMapUnlockCatalog.NormalizeMapId(mapId);
        string normalizedMatchToken = string.IsNullOrWhiteSpace(matchToken)
            ? "map_return_" + DateTime.UtcNow.Ticks
            : matchToken.Trim();
        return normalizedMatchToken + "_" + normalizedMapId;
    }

    void EnsureMapUnlockProgress()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.MapUnlockProgress = NormalizeMapUnlockProgress(CurrentProfile.MapUnlockProgress);
    }

    public static PlayerMapUnlockProgressData NormalizeMapUnlockProgress(PlayerMapUnlockProgressData progress)
    {
        Dictionary<string, int> countsByMapId = new Dictionary<string, int>(StringComparer.Ordinal);
        if (progress != null && progress.ReturnCounts != null)
        {
            for (int i = 0; i < progress.ReturnCounts.Length; i++)
            {
                PlayerMapReturnCountEntry entry = progress.ReturnCounts[i];
                if (entry == null)
                    continue;

                string mapId = LobbyMapUnlockCatalog.NormalizeMapId(entry.MapId);
                if (string.IsNullOrWhiteSpace(mapId))
                    continue;

                int count = Mathf.Max(0, entry.Count);
                if (count <= 0)
                    continue;

                if (countsByMapId.TryGetValue(mapId, out int existingCount))
                {
                    long combined = (long)existingCount + count;
                    countsByMapId[mapId] = combined > int.MaxValue ? int.MaxValue : (int)combined;
                }
                else
                {
                    countsByMapId[mapId] = count;
                }
            }
        }

        List<PlayerMapReturnCountEntry> entries = new List<PlayerMapReturnCountEntry>();
        if (LobbyMapCatalog.AllMaps != null)
        {
            for (int i = 0; i < LobbyMapCatalog.AllMaps.Count; i++)
            {
                LobbyMapDefinition map = LobbyMapCatalog.AllMaps[i];
                if (map == null || string.IsNullOrWhiteSpace(map.Id))
                    continue;

                if (!countsByMapId.TryGetValue(map.Id, out int count) || count <= 0)
                    continue;

                entries.Add(new PlayerMapReturnCountEntry
                {
                    MapId = map.Id,
                    Count = count
                });
            }
        }

        return new PlayerMapUnlockProgressData
        {
            ReturnCounts = entries.ToArray(),
            MothershipKilled = progress != null && progress.MothershipKilled,
            CheatUnlockAllMaps = progress != null && progress.CheatUnlockAllMaps
        };
    }
}
