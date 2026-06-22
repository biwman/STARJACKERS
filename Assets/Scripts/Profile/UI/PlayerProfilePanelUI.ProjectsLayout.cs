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
    void LayoutProjectsScreen()
    {
        if (currentScreen != ProfileScreen.Projects)
            return;

        if (projectsTitleText != null)
            SetAnchoredRect(projectsTitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(620f, 52f));

        float tileWidth = 540f;
        float tileHeight = 316f;
        float spacingX = 56f;
        float spacingY = 46f;
        int columns = 3;
        float startX = 360f;
        float startY = -230f;
        for (int i = 0; i < projectTileButtons.Count; i++)
        {
            Button tile = projectTileButtons[i];
            if (tile == null)
                continue;

            int column = i % columns;
            int row = i / columns;
            SetAnchoredRect(tile.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(startX + column * (tileWidth + spacingX), startY - row * (tileHeight + spacingY)), new Vector2(tileWidth, tileHeight));
        }
    }

    void LayoutProjectDetailsScreen()
    {
        if (currentScreen != ProfileScreen.ProjectDetails)
            return;

        if (projectDetailsTitleText != null)
            SetAnchoredRect(projectDetailsTitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(780f, 54f));
        if (projectDescriptionPanelObject != null)
            SetAnchoredRect(projectDescriptionPanelObject.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(720f, -236f), new Vector2(1200f, 600f));
        if (projectDescriptionContentRect != null)
        {
            projectDescriptionContentRect.anchorMin = new Vector2(0f, 1f);
            projectDescriptionContentRect.anchorMax = new Vector2(1f, 1f);
            projectDescriptionContentRect.pivot = new Vector2(0.5f, 1f);
            projectDescriptionContentRect.anchoredPosition = Vector2.zero;
            projectDescriptionContentRect.sizeDelta = Vector2.zero;
        }
        if (projectDescriptionText != null)
        {
            projectDescriptionText.fontSize = 28f;
            projectDescriptionText.enableAutoSizing = false;
        }
        if (projectRewardsPanelObject != null)
            SetAnchoredRect(projectRewardsPanelObject.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-304f, -258f), new Vector2(470f, 220f));
        if (projectRewardsText != null)
            SetAnchoredRect(projectRewardsText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-304f, -282f), new Vector2(420f, 132f));
        if (projectRewardClaimButton != null)
            SetAnchoredRect(projectRewardClaimButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-304f, -428f), new Vector2(220f, 64f));

        float tabWidth = 204f;
        float tabSpacing = 16f;
        ProjectDefinition project = ProjectCatalog.Get(selectedProjectId);
        int stageCount = project?.Stages != null ? project.Stages.Length : 0;
        float totalTabWidth = stageCount * tabWidth + Mathf.Max(0, stageCount - 1) * tabSpacing;
        float tabStartX = -totalTabWidth * 0.5f + tabWidth * 0.5f;
        for (int i = 0; i < projectStageTabButtons.Count; i++)
        {
            Button tab = projectStageTabButtons[i];
            if (tab == null)
                continue;

            bool active = i < stageCount;
            tab.gameObject.SetActive(active);
            if (active)
                SetAnchoredRect(tab.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(tabStartX + i * (tabWidth + tabSpacing), -168f), new Vector2(tabWidth, 62f));
        }

        for (int i = 0; i < projectStepButtons.Count; i++)
        {
            Button step = projectStepButtons[i];
            if (step == null)
                continue;

            int column = i % 3;
            int row = i / 3;
            SetAnchoredRect(step.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-540f + column * 540f, 168f - row * 158f), new Vector2(500f, 142f));
            if (i < projectStepIcons.Count && projectStepIcons[i] != null)
                SetAnchoredRect(projectStepIcons[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 0f), new Vector2(96f, 96f));
        }

        if (projectCommitPanelObject != null)
            SetAnchoredRect(projectCommitPanelObject.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-304f, -506f), new Vector2(430f, 276f));
    }

    void EnsureProjectTileVisual(Button tileButton, ProjectDefinition project)
    {
        if (tileButton == null || project == null)
            return;

        Image rootImage = tileButton.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.raycastTarget = true;
            rootImage.type = Image.Type.Sliced;
        }

        GameObject previewObject = FindOrCreateProfileChild(tileButton.gameObject, "ProjectTilePreview", typeof(RectTransform), typeof(Image));
        Image previewImage = previewObject.GetComponent<Image>();
        SetAnchoredRect(previewImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        previewImage.rectTransform.offsetMin = new Vector2(4f, 4f);
        previewImage.rectTransform.offsetMax = new Vector2(-4f, -4f);
        previewImage.preserveAspect = false;
        previewImage.raycastTarget = false;

        GameObject labelBackdropObject = FindOrCreateProfileChild(tileButton.gameObject, "ProjectTileLabelBackdrop", typeof(RectTransform), typeof(Image));
        Image labelBackdrop = labelBackdropObject.GetComponent<Image>();
        SetAnchoredRect(labelBackdrop.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 64f));
        labelBackdrop.color = new Color(0.02f, 0.04f, 0.08f, 0.72f);
        labelBackdrop.raycastTarget = false;

        TMP_Text title = tileButton.transform.Find("ProjectTileTitle")?.GetComponent<TMP_Text>();
        if (title == null)
            title = CreateText(tileButton.transform, "ProjectTileTitle", project.DisplayName, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(460f, 34f), 26f, TextAlignmentOptions.Center);

        title.text = project.DisplayName;
        title.raycastTarget = false;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(460f, 34f));

        const string ProjectCompleteMarker = "V";
        TMP_Text check = tileButton.transform.Find("ProjectTileCheck")?.GetComponent<TMP_Text>();
        if (check == null)
        {
            check = CreateText(tileButton.transform, "ProjectTileCheck", ProjectCompleteMarker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 70f), 58f, TextAlignmentOptions.Center);
            check.raycastTarget = false;
        }

        check.text = ProjectCompleteMarker;
        check.fontStyle = FontStyles.Bold;
        check.color = new Color(0.28f, 1f, 0.45f, 0.98f);
    }

    void SetProjectStageSelectionFrame(Button tab, bool selected)
    {
        if (tab == null)
            return;

        const float thickness = 8f;
        const float outwardOffset = 5f;
        Color frameColor = new Color(0f, 0.22f, 0.08f, 1f);
        GameObject frameObject = FindOrCreateProfileChild(tab.gameObject, "ProjectStageSelectedFrame", typeof(RectTransform));
        frameObject.SetActive(selected);
        frameObject.transform.SetAsLastSibling();

        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.offsetMin = new Vector2(-outwardOffset, -outwardOffset);
        frameRect.offsetMax = new Vector2(outwardOffset, outwardOffset);

        ConfigureProjectStageFrameStrip(frameObject.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, thickness), frameColor);
        ConfigureProjectStageFrameStrip(frameObject.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, thickness), frameColor);
        ConfigureProjectStageFrameStrip(frameObject.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(thickness, 0f), frameColor);
        ConfigureProjectStageFrameStrip(frameObject.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(thickness, 0f), frameColor);
    }

    void ConfigureProjectStageFrameStrip(Transform frame, string stripName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        GameObject stripObject = FindOrCreateProfileChild(frame.gameObject, stripName, typeof(RectTransform), typeof(Image));
        RectTransform stripRect = stripObject.GetComponent<RectTransform>();
        stripRect.anchorMin = anchorMin;
        stripRect.anchorMax = anchorMax;
        stripRect.pivot = pivot;
        stripRect.anchoredPosition = anchoredPosition;
        stripRect.sizeDelta = sizeDelta;

        Image stripImage = stripObject.GetComponent<Image>();
        stripImage.color = color;
        stripImage.raycastTarget = false;
    }

    GameObject FindOrCreateProfileChild(GameObject parent, string name, params Type[] components)
    {
        Transform existing = parent != null ? parent.transform.Find(name) : null;
        if (existing != null)
            return existing.gameObject;

        GameObject child = new GameObject(name, components);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    void RefreshProjectsView()
    {
        using (ProjectsRefreshMarker.Auto())
        {
            if (projectsViewRootObject == null || !projectsViewRootObject.activeSelf)
                return;

            IReadOnlyList<ProjectDefinition> projects = ProjectCatalog.AllProjects;
            for (int i = 0; i < projectTileButtons.Count; i++)
            {
                Button tile = projectTileButtons[i];
                bool active = i < projects.Count;
                if (tile == null)
                    continue;

                tile.gameObject.SetActive(active);
                if (!active)
                    continue;

                ProjectDefinition project = projects[i];
                EnsureProjectTileVisual(tile, project);
                Image preview = tile.transform.Find("ProjectTilePreview")?.GetComponent<Image>();
                if (preview != null)
                {
                    preview.sprite = LoadSpriteFromResources(project.TileResourcePath);
                    preview.color = preview.sprite != null ? Color.white : new Color(0.12f, 0.16f, 0.22f, 1f);
                }

                bool complete = PlayerProfileService.HasInstance && PlayerProfileService.Instance.IsProjectComplete(project.Id);
                tile.interactable = !inventoryActionInProgress;
                Image image = tile.GetComponent<Image>();
                if (image != null)
                    image.color = complete ? new Color(0.18f, 0.18f, 0.18f, 0.92f) : new Color(0.1f, 0.16f, 0.24f, 0.96f);

                TMP_Text check = tile.transform.Find("ProjectTileCheck")?.GetComponent<TMP_Text>();
                if (check != null)
                    check.gameObject.SetActive(complete);

                if (preview != null && complete)
                    preview.color = new Color(0.45f, 0.45f, 0.45f, 0.72f);
            }

            projectsDirty = false;
        }
    }

    void RefreshProjectDetailsView()
    {
        using var marker = ProjectDetailsRefreshMarker.Auto();

        if (projectDetailsViewObject == null || !projectDetailsViewObject.activeSelf)
            return;

        ProjectDefinition project = ProjectCatalog.Get(selectedProjectId) ?? ProjectCatalog.GetDefault();
        if (project == null)
            return;

        selectedProjectId = project.Id;
        int stageCount = project.Stages != null ? project.Stages.Length : 0;
        selectedProjectStageIndex = Mathf.Clamp(selectedProjectStageIndex, 0, Mathf.Max(0, stageCount - 1));
        ProjectStageDefinition stage = stageCount > 0 ? project.Stages[selectedProjectStageIndex] : null;
        bool stageUnlocked = PlayerProfileService.HasInstance && PlayerProfileService.Instance.IsProjectStageUnlocked(project.Id, selectedProjectStageIndex);
        bool stageComplete = PlayerProfileService.HasInstance && PlayerProfileService.Instance.IsProjectStageComplete(project.Id, selectedProjectStageIndex);
        bool rewardClaimed = PlayerProfileService.HasInstance && PlayerProfileService.Instance.IsProjectStageRewardClaimed(project.Id, selectedProjectStageIndex);

        if (projectDetailsTitleText != null)
            projectDetailsTitleText.text = project.DisplayName;
        RefreshProjectDescriptionText(project);
        if (projectRewardsText != null)
            projectRewardsText.text = BuildProjectRewardText(stage, rewardClaimed);

        for (int i = 0; i < projectStageTabButtons.Count; i++)
        {
            Button tab = projectStageTabButtons[i];
            if (tab == null)
                continue;

            bool active = i < stageCount;
            tab.gameObject.SetActive(active);
            if (!active)
                continue;

            bool unlocked = PlayerProfileService.HasInstance && PlayerProfileService.Instance.IsProjectStageUnlocked(project.Id, i);
            bool complete = PlayerProfileService.HasInstance && PlayerProfileService.Instance.IsProjectStageComplete(project.Id, i);
            bool selectedStage = i == selectedProjectStageIndex;
            TMP_Text text = tab.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = "STAGE " + (i + 1);
                text.color = complete
                    ? new Color(0.02f, 0.17f, 0.06f, 1f)
                    : Color.white;
            }

            tab.interactable = !inventoryActionInProgress;
            Color normal = complete
                ? new Color(0.62f, 1f, 0.42f, 1f)
                : unlocked ? new Color(0.13f, 0.18f, 0.26f, 0.96f) : new Color(0.48f, 0.08f, 0.08f, 0.86f);
            Color hover = complete
                ? new Color(0.72f, 1f, 0.52f, 1f)
                : unlocked ? new Color(0.3f, 0.52f, 0.66f, 1f) : new Color(0.62f, 0.12f, 0.12f, 0.96f);
            StyleButton(tab, normal, hover);

            Outline outline = tab.GetComponent<Outline>();
            if (outline == null)
                outline = tab.gameObject.AddComponent<Outline>();
            outline.effectColor = selectedStage
                ? new Color(0f, 0.24f, 0.08f, 1f)
                : new Color(0f, 0f, 0f, 0f);
            outline.effectDistance = selectedStage ? new Vector2(6f, -6f) : Vector2.zero;
            outline.useGraphicAlpha = true;
            outline.enabled = selectedStage;

            SetProjectStageSelectionFrame(tab, selectedStage);
        }

        int stepCount = stage?.Steps != null ? stage.Steps.Length : 0;
        for (int i = 0; i < projectStepButtons.Count; i++)
        {
            Button stepButton = projectStepButtons[i];
            if (stepButton == null)
                continue;

            bool active = i < stepCount;
            stepButton.gameObject.SetActive(active);
            if (!active)
                continue;

            ProjectStepDefinition step = stage.Steps[i];
            int delivered = PlayerProfileService.HasInstance ? PlayerProfileService.Instance.GetProjectStepDelivered(project.Id, selectedProjectStageIndex, i) : 0;
            int required = Mathf.Max(0, step.RequiredCount);
            bool complete = delivered >= required;
            bool selected = i == selectedProjectStepIndex;

            if (i < projectStepTexts.Count && projectStepTexts[i] != null)
                projectStepTexts[i].text = step.DisplayName.ToUpperInvariant() + "\n" + delivered + "/" + required;
            if (i < projectStepIcons.Count && projectStepIcons[i] != null)
            {
                projectStepIcons[i].sprite = InventoryItemCatalog.GetIcon(step.ResolveIconItemId());
                projectStepIcons[i].enabled = projectStepIcons[i].sprite != null;
                projectStepIcons[i].color = complete ? new Color(0.65f, 0.72f, 0.74f, 0.72f) : Color.white;
            }
            if (i < projectStepCheckBoxes.Count && projectStepCheckBoxes[i] != null)
                projectStepCheckBoxes[i].SetActive(complete);

            stepButton.interactable = !inventoryActionInProgress && stageUnlocked;
            Color normal = complete
                ? new Color(0.14f, 0.16f, 0.17f, 0.72f)
                : selected ? new Color(0.18f, 0.32f, 0.42f, 0.98f)
                : stageUnlocked ? new Color(0.08f, 0.12f, 0.17f, 0.92f) : new Color(0.48f, 0.08f, 0.08f, 0.84f);
            StyleButton(stepButton, normal, selected ? new Color(0.24f, 0.44f, 0.56f, 1f) : stageUnlocked ? new Color(0.14f, 0.22f, 0.31f, 1f) : new Color(0.62f, 0.12f, 0.12f, 0.96f));
        }

        if (projectRewardClaimButton != null)
        {
            projectRewardClaimButton.gameObject.SetActive(stageComplete && !rewardClaimed);
            projectRewardClaimButton.interactable = !inventoryActionInProgress;
        }

        RefreshProjectCommitPanel();
        projectDetailsDirty = false;
    }

    void RefreshProjectDescriptionText(ProjectDefinition project)
    {
        if (projectDescriptionText == null || project == null)
            return;

        string description = !string.IsNullOrWhiteSpace(project.Description)
            ? project.Description
            : "TU powinien byc opis do " + project.DisplayName;
        string scrollKey = project.Id + "|" + description;
        bool changed = !string.Equals(projectDescriptionScrollKey, scrollKey, StringComparison.Ordinal);
        if (changed)
        {
            projectDescriptionScrollKey = scrollKey;
            projectDescriptionText.text = description;
            projectDescriptionScrollResetPending = true;
        }

        UpdateProjectDescriptionContentHeight(description);

        if (projectDescriptionScrollResetPending && projectDescriptionScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            projectDescriptionScrollRect.StopMovement();
            projectDescriptionScrollRect.verticalNormalizedPosition = 1f;
            projectDescriptionScrollResetPending = false;
        }
    }

    void UpdateProjectDescriptionContentHeight(string description)
    {
        if (projectDescriptionText == null || projectDescriptionContentRect == null)
            return;

        RectTransform viewport = projectDescriptionScrollRect != null ? projectDescriptionScrollRect.viewport : null;
        float viewportWidth = viewport != null && viewport.rect.width > 0f ? viewport.rect.width : 1200f;
        float viewportHeight = viewport != null && viewport.rect.height > 0f ? viewport.rect.height : 600f;
        float textWidth = Mathf.Max(200f, viewportWidth - 56f);
        float preferredHeight = projectDescriptionText.GetPreferredValues(description ?? string.Empty, textWidth, Mathf.Infinity).y;
        float contentHeight = Mathf.Max(viewportHeight + 1f, preferredHeight + 52f);

        if (projectDescriptionTextLayoutElement != null)
            projectDescriptionTextLayoutElement.preferredHeight = Mathf.Max(1f, preferredHeight);

        projectDescriptionContentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(projectDescriptionContentRect);
    }

    string BuildProjectRewardText(ProjectStageDefinition stage, bool rewardClaimed)
    {
        if (stage?.Reward == null)
            return "REWARD\nNone";

        List<string> lines = new List<string> { "REWARD" };
        if (stage.Reward.Astrons > 0)
            lines.Add(stage.Reward.Astrons + " Astrons");

        string[] itemIds = stage.Reward.ItemIds ?? Array.Empty<string>();
        List<string> orderedItemIds = new List<string>();
        Dictionary<string, int> itemCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < itemIds.Length; i++)
        {
            string itemId = itemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (itemCounts.ContainsKey(itemId))
            {
                itemCounts[itemId]++;
            }
            else
            {
                orderedItemIds.Add(itemId);
                itemCounts[itemId] = 1;
            }
        }

        for (int i = 0; i < orderedItemIds.Count; i++)
        {
            string itemId = orderedItemIds[i];
            int count = itemCounts.TryGetValue(itemId, out int storedCount) ? storedCount : 1;
            string itemName = InventoryItemCatalog.GetDisplayName(itemId);
            lines.Add(count > 1 ? count + "x " + itemName : itemName);
        }

        if (rewardClaimed)
            lines.Add("CLAIMED");

        return string.Join("\n", lines);
    }

    void RefreshProjectCommitPanel()
    {
        if (projectCommitPanelObject == null)
            return;

        ProjectDefinition project = ProjectCatalog.Get(selectedProjectId);
        ProjectStageDefinition stage = project?.Stages != null && selectedProjectStageIndex >= 0 && selectedProjectStageIndex < project.Stages.Length
            ? project.Stages[selectedProjectStageIndex]
            : null;
        ProjectStepDefinition step = stage?.Steps != null && selectedProjectStepIndex >= 0 && selectedProjectStepIndex < stage.Steps.Length
            ? stage.Steps[selectedProjectStepIndex]
            : null;

        bool hasStep = step != null;
        projectCommitPanelObject.SetActive(hasStep);
        if (!hasStep)
            return;

        int delivered = PlayerProfileService.HasInstance ? PlayerProfileService.Instance.GetProjectStepDelivered(project.Id, selectedProjectStageIndex, selectedProjectStepIndex) : 0;
        int required = Mathf.Max(0, step.RequiredCount);
        int missing = Mathf.Max(0, required - delivered);
        int available = PlayerProfileService.HasInstance ? PlayerProfileService.Instance.CountProjectRequirementAvailable(step) : 0;
        int maxCommit = Mathf.Min(missing, available);
        projectCommitAmount = Mathf.Clamp(projectCommitAmount, 0, maxCommit);

        if (projectCommitTitleText != null)
            projectCommitTitleText.text = step.DisplayName.ToUpperInvariant();
        if (projectCommitAvailableText != null)
            projectCommitAvailableText.text = "Available: " + available;
        if (projectCommitAmountText != null)
            projectCommitAmountText.text = projectCommitAmount.ToString();

        bool canCommit = maxCommit > 0 && projectCommitAmount > 0 && !inventoryActionInProgress;
        if (projectCommitMinusButton != null)
            projectCommitMinusButton.interactable = projectCommitAmount > 0 && !inventoryActionInProgress;
        if (projectCommitPlusButton != null)
            projectCommitPlusButton.interactable = projectCommitAmount < maxCommit && !inventoryActionInProgress;
        if (projectCommitButton != null)
            projectCommitButton.interactable = canCommit;
    }
}
