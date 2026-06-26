using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService : MonoBehaviour
{
    void EnsureProjectProgress()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.ProjectProgress = ProjectCatalog.NormalizeProgress(CurrentProfile.ProjectProgress);
    }

    public bool IsProjectComplete(string projectId)
    {
        EnsureProjectProgress();
        return ProjectCatalog.IsProjectComplete(CurrentProfile.ProjectProgress, projectId);
    }

    public bool IsProjectUnlocked(string projectId)
    {
        EnsureProjectProgress();
        return ProjectCatalog.IsProjectUnlocked(CurrentProfile.ProjectProgress, projectId);
    }

    public string GetProjectUnlockRequirementText(string projectId)
    {
        EnsureProjectProgress();
        return ProjectCatalog.GetProjectUnlockRequirementText(CurrentProfile.ProjectProgress, projectId);
    }

    public bool IsProjectStageUnlocked(string projectId, int stageIndex)
    {
        EnsureProjectProgress();
        return ProjectCatalog.IsStageUnlocked(CurrentProfile.ProjectProgress, projectId, stageIndex);
    }

    public bool IsProjectStageComplete(string projectId, int stageIndex)
    {
        EnsureProjectProgress();
        ProjectDefinition project = ProjectCatalog.Get(projectId);
        PlayerProjectProgressEntry progress = FindProjectEntry(CurrentProfile.ProjectProgress, projectId);
        return ProjectCatalog.IsStageComplete(project, progress, stageIndex);
    }

    public bool IsProjectStageRewardClaimed(string projectId, int stageIndex)
    {
        EnsureProjectProgress();
        PlayerProjectProgressEntry progress = FindProjectEntry(CurrentProfile.ProjectProgress, projectId);
        PlayerProjectStageProgress stage = progress != null ? progress.GetStage(stageIndex) : null;
        return stage != null && stage.RewardClaimed;
    }

    public int GetProjectStepDelivered(string projectId, int stageIndex, int stepIndex)
    {
        EnsureProjectProgress();
        PlayerProjectProgressEntry progress = FindProjectEntry(CurrentProfile.ProjectProgress, projectId);
        PlayerProjectStageProgress stage = progress != null ? progress.GetStage(stageIndex) : null;
        return stage != null ? stage.GetDelivered(stepIndex) : 0;
    }

    public int CountProjectRequirementAvailable(ProjectStepDefinition step)
    {
        EnsureInventory();
        return CountProjectRequirementAvailable(CurrentProfile.Inventory, step, GetActiveShipInventoryCapacity());
    }

    public async Task<ProjectCommitResult> CommitProjectStepAsync(string projectId, int stageIndex, int stepIndex, int requestedAmount)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureProjectProgress();

        ProjectCommitResult result = new ProjectCommitResult();
        ProjectDefinition project = ProjectCatalog.Get(projectId);
        if (project == null || project.Stages == null || stageIndex < 0 || stageIndex >= project.Stages.Length)
            return ProjectCommitFailed(result, "Project not found.");

        if (!ProjectCatalog.IsProjectUnlocked(CurrentProfile.ProjectProgress, projectId))
            return ProjectCommitFailed(result, "Project locked. " + ProjectCatalog.GetProjectUnlockRequirementText(CurrentProfile.ProjectProgress, projectId));

        ProjectStageDefinition stage = project.Stages[stageIndex];
        if (stage == null || stage.Steps == null || stepIndex < 0 || stepIndex >= stage.Steps.Length)
            return ProjectCommitFailed(result, "Step not found.");

        if (!ProjectCatalog.IsStageUnlocked(CurrentProfile.ProjectProgress, projectId, stageIndex))
            return ProjectCommitFailed(result, "Finish previous stage first.");

        int amount = Mathf.Max(0, requestedAmount);
        if (amount <= 0)
            return ProjectCommitFailed(result, "Choose amount first.");

        ProjectStepDefinition step = stage.Steps[stepIndex];
        PlayerProjectProgressEntry projectProgress = FindProjectEntry(CurrentProfile.ProjectProgress, projectId);
        PlayerProjectStageProgress stageProgress = projectProgress != null ? projectProgress.GetStage(stageIndex) : null;
        if (stageProgress == null)
            return ProjectCommitFailed(result, "Project progress unavailable.");

        int required = Mathf.Max(0, step.RequiredCount);
        int delivered = stageProgress.GetDelivered(stepIndex);
        int missing = Mathf.Max(0, required - delivered);
        if (missing <= 0)
            return ProjectCommitFailed(result, "This step is already complete.");

        int shipCapacity = GetActiveShipInventoryCapacity();
        int available = CountProjectRequirementAvailable(CurrentProfile.Inventory, step, shipCapacity);
        int committed = Mathf.Min(amount, missing, available);
        if (committed <= 0)
            return ProjectCommitFailed(result, "Required item is not available.");

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        if (!RemoveProjectRequirementItems(workingInventory, step, committed, shipCapacity))
            return ProjectCommitFailed(result, "Could not remove required items.");

        CurrentProfile.Inventory = workingInventory;
        stageProgress.SetDelivered(stepIndex, delivered + committed);
        result.Success = true;
        result.CommittedCount = committed;
        result.StageCompleted = ProjectCatalog.IsStageComplete(project, projectProgress, stageIndex);
        result.Message = "Committed " + committed + ".";

        if (result.StageCompleted && !stageProgress.RewardClaimed)
        {
            result.RewardClaimed = TryApplyProjectReward(stage.Reward, out string rewardFailure);
            if (result.RewardClaimed)
            {
                stageProgress.RewardClaimed = true;
                result.Message = "Stage complete. Reward claimed.";
            }
            else if (!string.IsNullOrWhiteSpace(rewardFailure))
            {
                result.Message = "Stage complete. " + rewardFailure;
            }
        }

        await SaveProjectsInventoryAndAstronsAsync();
        return result;
    }

    public async Task<ProjectCommitResult> TryClaimProjectStageRewardAsync(string projectId, int stageIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureProjectProgress();

        ProjectCommitResult result = new ProjectCommitResult();
        ProjectDefinition project = ProjectCatalog.Get(projectId);
        PlayerProjectProgressEntry projectProgress = FindProjectEntry(CurrentProfile.ProjectProgress, projectId);
        if (project == null || project.Stages == null || stageIndex < 0 || stageIndex >= project.Stages.Length || projectProgress == null)
            return ProjectCommitFailed(result, "Project not found.");

        if (!ProjectCatalog.IsProjectUnlocked(CurrentProfile.ProjectProgress, projectId))
            return ProjectCommitFailed(result, "Project locked. " + ProjectCatalog.GetProjectUnlockRequirementText(CurrentProfile.ProjectProgress, projectId));

        PlayerProjectStageProgress stageProgress = projectProgress.GetStage(stageIndex);
        if (stageProgress == null)
            return ProjectCommitFailed(result, "Stage not found.");

        if (!ProjectCatalog.IsStageComplete(project, projectProgress, stageIndex))
            return ProjectCommitFailed(result, "Stage is not complete.");

        if (stageProgress.RewardClaimed)
            return ProjectCommitFailed(result, "Reward already claimed.");

        if (!TryApplyProjectReward(project.Stages[stageIndex].Reward, out string failure))
            return ProjectCommitFailed(result, failure);

        stageProgress.RewardClaimed = true;
        result.Success = true;
        result.RewardClaimed = true;
        result.StageCompleted = true;
        result.Message = "Reward claimed.";
        await SaveProjectsInventoryAndAstronsAsync();
        return result;
    }

    public async Task UnlockAllProjectsAsync()
    {
        await EnsureInitializedAsync();
        EnsureProjectProgress();

        if (CurrentProfile.ProjectProgress.CheatUnlockAllProjects)
            return;

        PlayerProjectProgressData previousProgress = CurrentProfile.ProjectProgress.Clone();
        CurrentProfile.ProjectProgress.CheatUnlockAllProjects = true;

        try
        {
            await SaveProjectProgressAsync("save unlock all projects cheat");
        }
        catch (Exception ex)
        {
            CurrentProfile.ProjectProgress = previousProgress;
            Debug.LogError("PlayerProfileService unlock all projects cheat failed: " + ex);
            throw;
        }
    }

    public async Task LockAllProjectsAsync()
    {
        await EnsureInitializedAsync();
        EnsureProjectProgress();

        if (!CurrentProfile.ProjectProgress.CheatUnlockAllProjects)
            return;

        PlayerProjectProgressData previousProgress = CurrentProfile.ProjectProgress.Clone();
        CurrentProfile.ProjectProgress.CheatUnlockAllProjects = false;
        CurrentProfile.ProjectProgress = ProjectCatalog.NormalizeProgress(CurrentProfile.ProjectProgress);

        try
        {
            await SaveProjectProgressAsync("save lock all projects cheat");
        }
        catch (Exception ex)
        {
            CurrentProfile.ProjectProgress = previousProgress;
            Debug.LogError("PlayerProfileService lock all projects cheat failed: " + ex);
            throw;
        }
    }

    async Task SaveProjectsInventoryAndAstronsAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            EnsureProjectProgress();
            EnsureCareerStats();
            EnsureMissEnigmaUniqueItemRecoveries();

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudAstronsKey] = CurrentProfile.Astrons,
                [CloudProjectsKey] = SerializeProjectProgress(CurrentProfile.ProjectProgress),
                [CloudCareerStatsKey] = SerializeCareerStats(CurrentProfile.CareerStats),
                [CloudMissEnigmaRecoverableUniqueItemsKey] = SerializeMissEnigmaUniqueItemRecoveries(CurrentProfile.MissEnigmaRecoverableUniqueItemIds)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save projects");
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService project save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveProjectProgressAsync(string operationName)
    {
        try
        {
            IsBusy = true;
            EnsureProjectProgress();

            var data = new Dictionary<string, object>
            {
                [CloudProjectsKey] = SerializeProjectProgress(CurrentProfile.ProjectProgress)
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

    ProjectCommitResult ProjectCommitFailed(ProjectCommitResult result, string message)
    {
        result ??= new ProjectCommitResult();
        result.Success = false;
        result.Message = string.IsNullOrWhiteSpace(message) ? "Project action failed." : message;
        return result;
    }

    PlayerProjectProgressEntry FindProjectEntry(PlayerProjectProgressData progress, string projectId)
    {
        if (progress?.Entries == null || string.IsNullOrWhiteSpace(projectId))
            return null;

        for (int i = 0; i < progress.Entries.Length; i++)
        {
            PlayerProjectProgressEntry entry = progress.Entries[i];
            if (entry != null && string.Equals(entry.ProjectId, projectId, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    int CountProjectRequirementAvailable(PlayerInventoryData inventory, ProjectStepDefinition step, int shipCapacity)
    {
        if (inventory == null || step == null)
            return 0;

        inventory.Normalize();
        int count = CountMatchingItems(inventory.PlayerSlots, inventory.PlayerSlots.Length, step);
        count += CountMatchingItems(inventory.ShipSlots, Mathf.Clamp(shipCapacity, 0, inventory.ShipSlots.Length), step);
        return count;
    }

    int CountMatchingItems(string[] slots, int limit, ProjectStepDefinition step)
    {
        if (slots == null || step == null)
            return 0;

        int safeLimit = Mathf.Clamp(limit, 0, slots.Length);
        int count = 0;
        for (int i = 0; i < safeLimit; i++)
        {
            if (step.MatchesItem(slots[i]))
                count++;
        }

        return count;
    }

    bool RemoveProjectRequirementItems(PlayerInventoryData inventory, ProjectStepDefinition step, int amount, int shipCapacity)
    {
        if (inventory == null || step == null || amount <= 0)
            return false;

        inventory.Normalize();
        int remaining = amount;
        remaining = RemoveMatchingItemsFromSlots(inventory.PlayerSlots, inventory.PlayerSlots.Length, step, remaining);
        remaining = RemoveMatchingItemsFromSlots(inventory.ShipSlots, Mathf.Clamp(shipCapacity, 0, inventory.ShipSlots.Length), step, remaining);
        return remaining <= 0;
    }

    int RemoveMatchingItemsFromSlots(string[] slots, int limit, ProjectStepDefinition step, int remaining)
    {
        if (slots == null || step == null || remaining <= 0)
            return remaining;

        int safeLimit = Mathf.Clamp(limit, 0, slots.Length);
        for (int i = 0; i < safeLimit && remaining > 0; i++)
        {
            if (!step.MatchesItem(slots[i]))
                continue;

            slots[i] = null;
            remaining--;
        }

        return remaining;
    }

    bool TryApplyProjectReward(ProjectRewardDefinition reward, out string failure)
    {
        failure = string.Empty;
        if (reward == null)
            return true;

        PlayerInventoryData rewardInventory = CurrentProfile.Inventory != null
            ? CurrentProfile.Inventory.Clone()
            : PlayerInventoryData.Default();
        int shipSkinIndex = GetActiveShipSkinIndex();
        int shipCapacity = GetActiveShipInventoryCapacity();

        string[] itemIds = reward.ItemIds ?? Array.Empty<string>();
        for (int i = 0; i < itemIds.Length; i++)
        {
            string itemId = itemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            bool stored = rewardInventory.TryAddToPlayer(itemId);
            if (!stored)
                stored = rewardInventory.TryAddToShip(itemId, shipCapacity, shipSkinIndex);

            if (!stored)
            {
                failure = "No inventory space for reward.";
                return false;
            }
        }

        CurrentProfile.Inventory = rewardInventory;
        int rewardAstrons = Mathf.Max(0, reward.Astrons);
        EnsureCareerStats();
        long updatedAstrons = (long)Mathf.Max(0, CurrentProfile.Astrons) + rewardAstrons;
        CurrentProfile.Astrons = updatedAstrons > int.MaxValue ? int.MaxValue : (int)updatedAstrons;
        CurrentProfile.CareerStats.AstronsEarned = AddClamped(CurrentProfile.CareerStats.AstronsEarned, rewardAstrons);
        return true;
    }
}
