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
    void CreateProjectsView(Transform parent)
    {
        projectsTitleText = CreateText(parent, "ProjectsTitle", "PROJECTS", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -142f), new Vector2(620f, 48f), 38f, TextAlignmentOptions.Center);
        projectsTitleText.raycastTarget = false;

        projectTileButtons.Clear();
        IReadOnlyList<ProjectDefinition> projects = ProjectCatalog.AllProjects;
        for (int i = 0; i < projects.Count; i++)
        {
            int capturedIndex = i;
            ProjectDefinition project = projects[i];
            Button tile = CreateButton(parent, "ProjectTile_" + project.Id, string.Empty, Vector2.zero, new Vector2(520f, 300f), () =>
            {
                OnProjectTileClicked(ProjectCatalog.AllProjects[capturedIndex].Id);
            });
            StyleButton(tile, new Color(0.1f, 0.16f, 0.24f, 0.96f), new Color(0.16f, 0.24f, 0.36f, 1f));
            EnsureProjectTileVisual(tile, project);
            projectTileButtons.Add(tile);
        }

        projectsViewRootObject.SetActive(false);
    }

    void CreateProjectDetailsView(Transform parent)
    {
        projectDetailsTitleText = CreateText(parent, "ProjectDetailsTitle", "PROJECT", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(780f, 54f), 42f, TextAlignmentOptions.Center);

        projectDescriptionPanelObject = new GameObject("ProjectDescriptionPanel", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        projectDescriptionPanelObject.transform.SetParent(parent, false);
        Image descriptionPanelImage = projectDescriptionPanelObject.GetComponent<Image>();
        descriptionPanelImage.color = new Color(0.03f, 0.06f, 0.09f, 0.82f);
        descriptionPanelImage.raycastTarget = true;

        projectDescriptionScrollRect = projectDescriptionPanelObject.GetComponent<ScrollRect>();
        projectDescriptionScrollRect.horizontal = false;
        projectDescriptionScrollRect.vertical = true;
        projectDescriptionScrollRect.movementType = ScrollRect.MovementType.Clamped;
        projectDescriptionScrollRect.scrollSensitivity = 34f;
        projectDescriptionScrollRect.viewport = projectDescriptionPanelObject.GetComponent<RectTransform>();

        GameObject descriptionContentObject = new GameObject("ProjectDescriptionContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        descriptionContentObject.transform.SetParent(projectDescriptionPanelObject.transform, false);
        projectDescriptionContentRect = descriptionContentObject.GetComponent<RectTransform>();
        projectDescriptionContentRect.anchorMin = new Vector2(0f, 1f);
        projectDescriptionContentRect.anchorMax = new Vector2(1f, 1f);
        projectDescriptionContentRect.pivot = new Vector2(0.5f, 1f);
        projectDescriptionContentRect.anchoredPosition = Vector2.zero;
        projectDescriptionContentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup descriptionLayout = descriptionContentObject.GetComponent<VerticalLayoutGroup>();
        descriptionLayout.padding = new RectOffset(28, 28, 24, 28);
        descriptionLayout.childAlignment = TextAnchor.UpperLeft;
        descriptionLayout.childControlWidth = true;
        descriptionLayout.childControlHeight = true;
        descriptionLayout.childForceExpandWidth = true;
        descriptionLayout.childForceExpandHeight = false;

        ContentSizeFitter descriptionFitter = descriptionContentObject.GetComponent<ContentSizeFitter>();
        descriptionFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        descriptionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        projectDescriptionScrollRect.content = projectDescriptionContentRect;

        projectDescriptionText = CreateText(descriptionContentObject.transform, "ProjectDescription", string.Empty, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 28f, TextAlignmentOptions.TopLeft);
        projectDescriptionText.fontStyle = FontStyles.Normal;
        projectDescriptionText.textWrappingMode = TextWrappingModes.Normal;
        projectDescriptionText.enableAutoSizing = false;
        projectDescriptionText.fontSize = 28f;
        projectDescriptionText.lineSpacing = 7f;
        projectDescriptionText.overflowMode = TextOverflowModes.Overflow;
        projectDescriptionText.margin = Vector4.zero;
        projectDescriptionText.raycastTarget = true;

        projectDescriptionTextLayoutElement = projectDescriptionText.gameObject.AddComponent<LayoutElement>();
        projectDescriptionTextLayoutElement.flexibleWidth = 1f;
        projectDescriptionTextLayoutElement.minHeight = 1f;
        projectDescriptionTextLayoutElement.preferredHeight = 600f;

        ProjectDescriptionScrollDragForwarder textScrollForwarder = projectDescriptionText.gameObject.AddComponent<ProjectDescriptionScrollDragForwarder>();
        textScrollForwarder.scrollRect = projectDescriptionScrollRect;

        projectRewardsPanelObject = new GameObject("ProjectRewardsPanel", typeof(RectTransform), typeof(Image));
        projectRewardsPanelObject.transform.SetParent(parent, false);
        Image rewardsPanelImage = projectRewardsPanelObject.GetComponent<Image>();
        rewardsPanelImage.color = new Color(0.03f, 0.06f, 0.09f, 0.82f);
        rewardsPanelImage.raycastTarget = false;

        projectRewardsText = CreateText(parent, "ProjectRewards", string.Empty, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-292f, -274f), new Vector2(382f, 112f), 25f, TextAlignmentOptions.TopLeft);
        projectRewardsText.fontStyle = FontStyles.Normal;
        projectRewardsText.textWrappingMode = TextWrappingModes.Normal;

        projectRewardClaimButton = CreateButton(parent, "ProjectRewardClaimButton", "CLAIM", new Vector2(0f, 0f), new Vector2(220f, 64f), OnProjectRewardClaimClicked);
        StyleButton(projectRewardClaimButton, new Color(0.12f, 0.46f, 0.34f, 1f), new Color(0.16f, 0.62f, 0.44f, 1f));
        SetButtonTextSize(projectRewardClaimButton, 22f);

        int maxStages = 0;
        int maxSteps = 0;
        for (int i = 0; i < ProjectCatalog.AllProjects.Count; i++)
        {
            ProjectDefinition project = ProjectCatalog.AllProjects[i];
            int stageCount = project.Stages != null ? project.Stages.Length : 0;
            maxStages = Mathf.Max(maxStages, stageCount);
            for (int stageIndex = 0; stageIndex < stageCount; stageIndex++)
                maxSteps = Mathf.Max(maxSteps, project.Stages[stageIndex].Steps != null ? project.Stages[stageIndex].Steps.Length : 0);
        }

        projectStageTabButtons.Clear();
        for (int i = 0; i < maxStages; i++)
        {
            int capturedIndex = i;
            Button tab = CreateButton(parent, "ProjectStageTab_" + i, "STAGE " + (i + 1), Vector2.zero, new Vector2(204f, 62f), () =>
            {
                OnProjectStageTabClicked(capturedIndex);
            });
            StyleButton(tab, new Color(0.13f, 0.18f, 0.26f, 0.96f), new Color(0.24f, 0.42f, 0.54f, 1f));
            SetButtonTextSize(tab, 22f);
            projectStageTabButtons.Add(tab);
        }

        projectStepButtons.Clear();
        projectStepIcons.Clear();
        projectStepTexts.Clear();
        projectStepCheckBoxes.Clear();
        for (int i = 0; i < maxSteps; i++)
        {
            int capturedIndex = i;
            Button stepButton = CreateButton(parent, "ProjectStep_" + i, string.Empty, Vector2.zero, new Vector2(500f, 142f), () =>
            {
                OnProjectStepClicked(capturedIndex);
            });
            StyleButton(stepButton, new Color(0.08f, 0.12f, 0.17f, 0.92f), new Color(0.14f, 0.22f, 0.31f, 1f));

            GameObject iconObject = new GameObject("ProjectStepIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(stepButton.transform, false);
            Image icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TMP_Text text = CreateText(stepButton.transform, "ProjectStepLabel", string.Empty, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 26f, TextAlignmentOptions.MidlineLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.margin = new Vector4(126f, 12f, 72f, 12f);
            text.raycastTarget = false;

            GameObject checkBox = CreateProjectStepCheckBox(stepButton.transform);

            projectStepButtons.Add(stepButton);
            projectStepIcons.Add(icon);
            projectStepTexts.Add(text);
            projectStepCheckBoxes.Add(checkBox);
        }

        CreateProjectCommitPanel(parent);
        projectStatusText = CreateText(parent, "ProjectStatus", string.Empty, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(860f, 34f), 20f, TextAlignmentOptions.Center);
        projectStatusText.fontStyle = FontStyles.Normal;
        projectDetailsViewObject.SetActive(false);
    }

    GameObject CreateProjectStepCheckBox(Transform parent)
    {
        GameObject checkBox = new GameObject("ProjectStepCheckBox", typeof(RectTransform), typeof(Image), typeof(Outline));
        checkBox.transform.SetParent(parent, false);

        RectTransform rect = checkBox.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-48f, 0f);
        rect.sizeDelta = new Vector2(52f, 52f);

        Image boxImage = checkBox.GetComponent<Image>();
        boxImage.color = new Color(0.02f, 0.18f, 0.08f, 0.96f);
        boxImage.raycastTarget = false;

        Outline outline = checkBox.GetComponent<Outline>();
        outline.effectColor = new Color(0.34f, 1f, 0.48f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        CreateProjectStepCheckStroke(checkBox.transform, "ProjectStepCheckShortStroke", new Vector2(-8f, -3f), new Vector2(8f, 22f), 45f);
        CreateProjectStepCheckStroke(checkBox.transform, "ProjectStepCheckLongStroke", new Vector2(9f, 2f), new Vector2(8f, 34f), -45f);
        checkBox.SetActive(false);
        return checkBox;
    }

    void CreateProjectStepCheckStroke(Transform parent, string name, Vector2 position, Vector2 size, float rotationZ)
    {
        GameObject strokeObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        strokeObject.transform.SetParent(parent, false);

        RectTransform rect = strokeObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localEulerAngles = new Vector3(0f, 0f, rotationZ);

        Image image = strokeObject.GetComponent<Image>();
        image.color = new Color(0.62f, 1f, 0.36f, 1f);
        image.raycastTarget = false;
    }

    void CreateProjectCommitPanel(Transform parent)
    {
        projectCommitPanelObject = new GameObject("ProjectCommitPanel", typeof(RectTransform), typeof(Image));
        projectCommitPanelObject.transform.SetParent(parent, false);

        Image panelImage = projectCommitPanelObject.GetComponent<Image>();
        panelImage.color = new Color(0.04f, 0.07f, 0.1f, 0.9f);

        projectCommitTitleText = CreateText(projectCommitPanelObject.transform, "ProjectCommitTitle", "SELECT STEP", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(390f, 36f), 25f, TextAlignmentOptions.Center);
        projectCommitAvailableText = CreateText(projectCommitPanelObject.transform, "ProjectCommitAvailable", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(390f, 50f), 27f, TextAlignmentOptions.Center);
        projectCommitAvailableText.fontStyle = FontStyles.Normal;
        projectCommitAvailableText.textWrappingMode = TextWrappingModes.Normal;

        projectCommitMinusButton = CreateButton(projectCommitPanelObject.transform, "ProjectCommitMinus", "-", new Vector2(-128f, -142f), new Vector2(88f, 64f), () => AdjustProjectCommitAmount(-1));
        projectCommitAmountText = CreateText(projectCommitPanelObject.transform, "ProjectCommitAmount", "0", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -144f), new Vector2(138f, 58f), 34f, TextAlignmentOptions.Center);
        projectCommitPlusButton = CreateButton(projectCommitPanelObject.transform, "ProjectCommitPlus", "+", new Vector2(128f, -142f), new Vector2(88f, 64f), () => AdjustProjectCommitAmount(1));
        projectCommitButton = CreateButton(projectCommitPanelObject.transform, "ProjectCommitButton", "COMMIT", new Vector2(0f, -220f), new Vector2(252f, 64f), OnProjectCommitClicked);
        StyleButton(projectCommitMinusButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.24f, 0.34f, 0.46f, 1f));
        StyleButton(projectCommitPlusButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.24f, 0.34f, 0.46f, 1f));
        StyleButton(projectCommitButton, new Color(0.12f, 0.46f, 0.34f, 1f), new Color(0.16f, 0.62f, 0.44f, 1f));
        SetButtonTextSize(projectCommitMinusButton, 34f);
        SetButtonTextSize(projectCommitPlusButton, 34f);
        SetButtonTextSize(projectCommitButton, 24f);
    }

    void SetButtonTextSize(Button button, float fontSize)
    {
        TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (text == null)
            return;

        text.fontSize = fontSize;
        text.fontSizeMin = Mathf.Max(12f, fontSize - 5f);
        text.fontSizeMax = fontSize;
        text.enableAutoSizing = true;
    }
}
