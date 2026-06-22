using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
public partial class PlayerProfilePanelUI
{
    void OnShipPreviewClicked()
    {
        if (inventoryActionInProgress || dragInProgress || shipPreviewImage == null || shipPreviewImage.sprite == null)
            return;

        SwitchToScreen(ProfileScreen.ShipSelection);
    }

    void OnPilotPortraitClicked()
    {
        if (inventoryActionInProgress || dragInProgress)
            return;

        pilotSelectionCenterIndex = PilotCatalog.GetPilotIndex(selectedPilotId);
        SwitchToScreen(ProfileScreen.PilotSelection);
    }

    void OnProjectsHomeButtonClicked()
    {
        if (inventoryActionInProgress || dragInProgress)
            return;

        selectedProjectStepIndex = -1;
        projectCommitAmount = 0;
        SwitchToScreen(ProfileScreen.Projects);
    }

    void OnProjectTileClicked(string projectId)
    {
        if (inventoryActionInProgress)
            return;

        ProjectDefinition project = ProjectCatalog.Get(projectId);
        if (project == null)
            return;

        selectedProjectId = project.Id;
        selectedProjectStageIndex = ResolveInitialProjectStageIndex(project);
        selectedProjectStepIndex = -1;
        projectCommitAmount = 0;
        projectDescriptionScrollResetPending = true;
        SwitchToScreen(ProfileScreen.ProjectDetails);
    }

    int ResolveInitialProjectStageIndex(ProjectDefinition project)
    {
        if (project?.Stages == null || !PlayerProfileService.HasInstance)
            return 0;

        for (int i = 0; i < project.Stages.Length; i++)
        {
            if (!PlayerProfileService.Instance.IsProjectStageComplete(project.Id, i))
                return i;
        }

        return Mathf.Max(0, project.Stages.Length - 1);
    }

    void OnProjectStageTabClicked(int stageIndex)
    {
        if (inventoryActionInProgress)
            return;

        selectedProjectStageIndex = stageIndex;
        selectedProjectStepIndex = -1;
        projectCommitAmount = 0;
        SetProjectStatus(string.Empty);
        RefreshProjectDetailsView();
    }

    void OnProjectStepClicked(int stepIndex)
    {
        if (inventoryActionInProgress)
            return;

        selectedProjectStepIndex = stepIndex;
        projectCommitAmount = 0;
        SetProjectStatus(string.Empty);
        RefreshProjectDetailsView();
        AdjustProjectCommitAmount(1);
    }

    void AdjustProjectCommitAmount(int delta)
    {
        ProjectDefinition project = ProjectCatalog.Get(selectedProjectId);
        ProjectStageDefinition stage = project?.Stages != null && selectedProjectStageIndex >= 0 && selectedProjectStageIndex < project.Stages.Length
            ? project.Stages[selectedProjectStageIndex]
            : null;
        ProjectStepDefinition step = stage?.Steps != null && selectedProjectStepIndex >= 0 && selectedProjectStepIndex < stage.Steps.Length
            ? stage.Steps[selectedProjectStepIndex]
            : null;
        if (step == null || !PlayerProfileService.HasInstance)
            return;

        int delivered = PlayerProfileService.Instance.GetProjectStepDelivered(project.Id, selectedProjectStageIndex, selectedProjectStepIndex);
        int missing = Mathf.Max(0, step.RequiredCount - delivered);
        int available = PlayerProfileService.Instance.CountProjectRequirementAvailable(step);
        int maxCommit = Mathf.Min(missing, available);
        projectCommitAmount = Mathf.Clamp(projectCommitAmount + delta, 0, maxCommit);
        RefreshProjectCommitPanel();
    }

    async void OnProjectCommitClicked()
    {
        if (inventoryActionInProgress || projectCommitAmount <= 0)
            return;

        try
        {
            inventoryActionInProgress = true;
            SetInteractable(false);
            SetProjectStatus("Committing...");
            ProjectCommitResult result = await PlayerProfileService.Instance.CommitProjectStepAsync(
                selectedProjectId,
                selectedProjectStageIndex,
                selectedProjectStepIndex,
                projectCommitAmount);

            SetProjectStatus(result.Message);
            projectCommitAmount = 0;
            RefreshView();
            RefreshProjectDetailsView();
        }
        catch (Exception ex)
        {
            Debug.LogError("Project commit failed: " + ex);
            SetProjectStatus("Project commit failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(!NetworkManager.SessionRequested);
            RefreshProjectDetailsView();
        }
    }

    async void OnProjectRewardClaimClicked()
    {
        if (inventoryActionInProgress)
            return;

        try
        {
            inventoryActionInProgress = true;
            SetInteractable(false);
            SetProjectStatus("Claiming reward...");
            ProjectCommitResult result = await PlayerProfileService.Instance.TryClaimProjectStageRewardAsync(selectedProjectId, selectedProjectStageIndex);
            SetProjectStatus(result.Message);
            RefreshView();
            RefreshProjectDetailsView();
        }
        catch (Exception ex)
        {
            Debug.LogError("Project reward claim failed: " + ex);
            SetProjectStatus("Reward claim failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(!NetworkManager.SessionRequested);
            RefreshProjectDetailsView();
        }
    }

    void SetProjectStatus(string value)
    {
        if (projectStatusText != null)
            projectStatusText.text = value ?? string.Empty;
    }
}
