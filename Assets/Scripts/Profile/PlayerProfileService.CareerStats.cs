using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService
{
    public async Task RecordGameStartedAsync()
    {
        await EnsureInitializedAsync();

        try
        {
            IsBusy = true;
            ClearPendingAstronautCargo();
            CurrentProfile.GamesPlayed = Mathf.Max(0, CurrentProfile.GamesPlayed + 1);

            var data = new Dictionary<string, object>
            {
                [CloudGamesPlayedKey] = CurrentProfile.GamesPlayed
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save games played");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService games played update failed: " + ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RecordRoundXpAsync(int roundXp, string matchToken)
    {
        await EnsureInitializedAsync();

        int normalizedXp = Mathf.Max(0, roundXp);
        if (normalizedXp <= 0)
            return;

        string token = string.IsNullOrWhiteSpace(matchToken)
            ? "match_" + DateTime.UtcNow.Ticks
            : matchToken;

        if (awardedMatchTokens.Contains(token))
            return;

        try
        {
            IsBusy = true;
            awardedMatchTokens.Add(token);
            CurrentProfile.TotalXp = Mathf.Max(0, CurrentProfile.TotalXp + normalizedXp);

            var data = new Dictionary<string, object>
            {
                [CloudTotalXpKey] = CurrentProfile.TotalXp
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save round xp");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            awardedMatchTokens.Remove(token);
            Debug.LogError("PlayerProfileService XP save failed: " + ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RecordRoundCareerStatsAsync(string outcome, int returnedLootValueAstrons, string matchToken)
    {
        await EnsureInitializedAsync();
        EnsureCareerStats();

        string token = string.IsNullOrWhiteSpace(matchToken)
            ? "career_round_" + DateTime.UtcNow.Ticks
            : matchToken;

        if (awardedCareerRoundTokens.Contains(token))
            return;

        PlayerCareerStatsData previousStats = CurrentProfile.CareerStats.Clone();
        string normalizedOutcome = string.IsNullOrWhiteSpace(outcome)
            ? string.Empty
            : outcome.Trim().ToLowerInvariant();
        bool returnedWithShip = string.Equals(normalizedOutcome, "extracted", StringComparison.OrdinalIgnoreCase);
        bool returnedWithAstronaut = string.Equals(normalizedOutcome, "evacuated", StringComparison.OrdinalIgnoreCase);
        bool successfulReturn = returnedWithShip || returnedWithAstronaut;

        if (returnedWithShip)
            CurrentProfile.CareerStats.ShipEscapes = AddClamped(CurrentProfile.CareerStats.ShipEscapes, 1);
        else if (returnedWithAstronaut)
            CurrentProfile.CareerStats.AstronautEscapes = AddClamped(CurrentProfile.CareerStats.AstronautEscapes, 1);

        if (successfulReturn)
        {
            CurrentProfile.CareerStats.ReturnStreakWithoutDeath = AddClamped(CurrentProfile.CareerStats.ReturnStreakWithoutDeath, 1);
            CurrentProfile.CareerStats.BestReturnStreakWithoutDeath = Mathf.Max(
                CurrentProfile.CareerStats.BestReturnStreakWithoutDeath,
                CurrentProfile.CareerStats.ReturnStreakWithoutDeath);
            CurrentProfile.CareerStats.HighestLootReturnedAstrons = Mathf.Max(
                CurrentProfile.CareerStats.HighestLootReturnedAstrons,
                Mathf.Max(0, returnedLootValueAstrons));
        }
        else if (IsCareerDeathOutcome(normalizedOutcome))
        {
            CurrentProfile.CareerStats.ReturnStreakWithoutDeath = 0;
        }

        CurrentProfile.CareerStats = NormalizeCareerStats(CurrentProfile.CareerStats);
        awardedCareerRoundTokens.Add(token);

        try
        {
            await SaveCareerStatsOnlyAsync("save career round stats");
        }
        catch (Exception ex)
        {
            CurrentProfile.CareerStats = previousStats;
            awardedCareerRoundTokens.Remove(token);
            Debug.LogError("PlayerProfileService career round stats save failed: " + ex);
            throw;
        }
    }

    public async Task RecordCareerKillAsync(bool killedHumanPlayer, bool killedNeutralRaider)
    {
        await EnsureInitializedAsync();
        EnsureCareerStats();

        PlayerCareerStatsData previousStats = CurrentProfile.CareerStats.Clone();
        if (killedNeutralRaider)
            CurrentProfile.CareerStats.NeutralRaiderKills = AddClamped(CurrentProfile.CareerStats.NeutralRaiderKills, 1);
        else if (killedHumanPlayer)
            CurrentProfile.CareerStats.HumanPlayerKills = AddClamped(CurrentProfile.CareerStats.HumanPlayerKills, 1);
        else
            CurrentProfile.CareerStats.EnemyKills = AddClamped(CurrentProfile.CareerStats.EnemyKills, 1);

        CurrentProfile.CareerStats = NormalizeCareerStats(CurrentProfile.CareerStats);

        try
        {
            await SaveCareerStatsOnlyAsync("save career kill stats");
        }
        catch (Exception ex)
        {
            CurrentProfile.CareerStats = previousStats;
            Debug.LogError("PlayerProfileService career kill save failed: " + ex);
            throw;
        }
    }

    public async Task AddCheatXpAsync(int amount)
    {
        await EnsureInitializedAsync();

        int normalizedXp = Mathf.Max(0, amount);
        if (normalizedXp <= 0)
            return;

        try
        {
            IsBusy = true;
            CurrentProfile.TotalXp = Mathf.Max(0, CurrentProfile.TotalXp + normalizedXp);

            var data = new Dictionary<string, object>
            {
                [CloudTotalXpKey] = CurrentProfile.TotalXp
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save cheat xp");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService cheat XP save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveCareerStatsOnlyAsync(string operationName)
    {
        try
        {
            IsBusy = true;
            EnsureCareerStats();

            var data = new Dictionary<string, object>
            {
                [CloudCareerStatsKey] = SerializeCareerStats(CurrentProfile.CareerStats)
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

    static bool IsCareerDeathOutcome(string outcome)
    {
        return string.Equals(outcome, "dead", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(outcome, "destroyed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(outcome, "lost_in_space", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(outcome, "time_up", StringComparison.OrdinalIgnoreCase);
    }

    static int AddClamped(int current, int amount)
    {
        long combined = (long)Mathf.Max(0, current) + Mathf.Max(0, amount);
        return combined > int.MaxValue ? int.MaxValue : (int)combined;
    }

    void EnsureCareerStats()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.CareerStats = NormalizeCareerStats(CurrentProfile.CareerStats);
    }

    public static PlayerCareerStatsData NormalizeCareerStats(PlayerCareerStatsData stats)
    {
        PlayerCareerStatsData normalized = stats != null ? stats.Clone() : PlayerCareerStatsData.Empty();
        normalized.ShipEscapes = Mathf.Max(0, normalized.ShipEscapes);
        normalized.AstronautEscapes = Mathf.Max(0, normalized.AstronautEscapes);
        normalized.ReturnStreakWithoutDeath = Mathf.Max(0, normalized.ReturnStreakWithoutDeath);
        normalized.BestReturnStreakWithoutDeath = Mathf.Max(
            Mathf.Max(0, normalized.BestReturnStreakWithoutDeath),
            normalized.ReturnStreakWithoutDeath);
        normalized.EnemyKills = Mathf.Max(0, normalized.EnemyKills);
        normalized.NeutralRaiderKills = Mathf.Max(0, normalized.NeutralRaiderKills);
        normalized.HumanPlayerKills = Mathf.Max(0, normalized.HumanPlayerKills);
        normalized.AstronsEarned = Mathf.Max(0, normalized.AstronsEarned);
        normalized.HighestLootReturnedAstrons = Mathf.Max(0, normalized.HighestLootReturnedAstrons);
        return normalized;
    }
}
