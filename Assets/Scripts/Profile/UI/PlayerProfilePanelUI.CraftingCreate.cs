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
    void CreateCraftingPanel(Transform parent)
    {
        craftingPanelObject = new GameObject("CraftingPanel", typeof(RectTransform), typeof(Image));
        craftingPanelObject.transform.SetParent(parent, false);

        RectTransform rect = craftingPanelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -198f);
        rect.sizeDelta = new Vector2(330f, 470f);

        Image background = craftingPanelObject.GetComponent<Image>();
        background.color = new Color(0.07f, 0.1f, 0.14f, 0.94f);

        craftingCatalogButton = null;

        craftingSlotButtons = new Button[PlayerInventoryData.CraftingSlotCount];
        craftingSlotTexts = new TMP_Text[PlayerInventoryData.CraftingSlotCount];
        craftingSlotIcons = new Image[PlayerInventoryData.CraftingSlotCount];

        Vector2[] positions =
        {
            new Vector2(-66f, -116f),
            new Vector2(66f, -116f),
            new Vector2(-66f, -248f),
            new Vector2(66f, -248f)
        };

        for (int i = 0; i < craftingSlotButtons.Length; i++)
        {
            craftingSlotButtons[i] = CreateCraftingSlotButton(
                craftingPanelObject.transform,
                "CraftSlot" + i,
                positions[i],
                i,
                out craftingSlotTexts[i],
                out craftingSlotIcons[i]);
        }

        craftButton = CreateButton(craftingPanelObject.transform, "CraftButton", "CRAFT", new Vector2(0f, -6f), new Vector2(285f, 69f), OnCraftButtonClicked);
        ConfigureNoBlinkInventoryActionButton(craftButton);
        clearCraftButton = CreateButton(craftingPanelObject.transform, "ClearCraftButton", "CLEAR", new Vector2(0f, -394f), new Vector2(190f, 46f), OnClearCraftingSlotsClicked);
        StyleButton(clearCraftButton, new Color(0.18f, 0.24f, 0.32f, 0.98f), new Color(0.24f, 0.32f, 0.42f, 1f));
        ConfigureNoBlinkInventoryActionButton(clearCraftButton);
    }

    void CreateCraftingRecipeBrowser(Transform parent)
    {
        craftingRecipeBrowserObject = new GameObject("CraftingRecipeBrowser", typeof(RectTransform), typeof(Image));
        craftingRecipeBrowserObject.transform.SetParent(parent, false);

        RectTransform overlayRect = craftingRecipeBrowserObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlay = craftingRecipeBrowserObject.GetComponent<Image>();
        overlay.color = new Color(0.03f, 0.04f, 0.06f, 0.72f);

        GameObject panel = new GameObject("CraftingRecipeBrowserPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(craftingRecipeBrowserObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 6f);
        panelRect.sizeDelta = new Vector2(1230f, 800f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.98f);

        TMP_Text title = CreateText(panel.transform, "CraftingRecipeBrowserTitle", "CRAFTABLE ITEMS", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(420f, 34f), 28f, TextAlignmentOptions.Center);
        title.characterSpacing = 2f;

        craftingRecipeAvailabilityButton = CreateButton(panel.transform, "CraftingRecipeAvailabilityButton", "ALL", new Vector2(-374f, -28f), new Vector2(190f, 44f), OnCraftingRecipeAvailabilityClicked);
        StyleCompactInventoryUtilityButton(craftingRecipeAvailabilityButton);
        ConfigureNoBlinkInventoryActionButton(craftingRecipeAvailabilityButton);
        RefreshCraftingRecipeAvailabilityButton();

        craftingRecipeBlueprintsButton = CreateButton(panel.transform, "CraftingRecipeBlueprintsButton", "BLUEPRINTS", new Vector2(0f, -28f), new Vector2(220f, 52f), OnCraftingBlueprintsClicked);
        StyleCompactInventoryUtilityButton(craftingRecipeBlueprintsButton);
        ConfigureNoBlinkInventoryActionButton(craftingRecipeBlueprintsButton);
        ConfigureBlueprintsTabButtonText();

        TMP_Text hint = CreateText(panel.transform, "CraftingRecipeBrowserHint", "Select a green recipe to auto-fill the crafting slots.", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(620f, 24f), 16f, TextAlignmentOptions.Center);
        hint.fontStyle = FontStyles.Normal;
        hint.color = new Color(0.78f, 0.86f, 0.94f, 0.92f);

        craftingRecipeCloseButton = CreateButton(panel.transform, "CraftingRecipeBrowserCloseButton", "CLOSE", new Vector2(0f, -722f), new Vector2(220f, 58f), HideCraftingRecipeBrowser);
        StyleButton(craftingRecipeCloseButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.22f, 0.3f, 0.4f, 1f));

        GameObject viewportObject = new GameObject("CraftingRecipeViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(panel.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = new Vector2(-18f, -12f);
        viewportRect.sizeDelta = new Vector2(1110f, 592f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.11f, 0.15f, 0.2f, 0.82f);

        GameObject contentObject = new GameObject("CraftingRecipeContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        craftingRecipeContentRect = contentObject.GetComponent<RectTransform>();
        craftingRecipeContentRect.anchorMin = new Vector2(0f, 1f);
        craftingRecipeContentRect.anchorMax = new Vector2(1f, 1f);
        craftingRecipeContentRect.pivot = new Vector2(0.5f, 1f);
        craftingRecipeContentRect.anchoredPosition = Vector2.zero;
        craftingRecipeContentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        GameObject scrollbarObject = new GameObject("CraftingRecipeScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarObject.transform.SetParent(panel.transform, false);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(0.5f, 0.5f);
        scrollbarRect.anchorMax = new Vector2(0.5f, 0.5f);
        scrollbarRect.pivot = new Vector2(0.5f, 0.5f);
        scrollbarRect.anchoredPosition = new Vector2(589f, -12f);
        scrollbarRect.sizeDelta = new Vector2(RuntimeScrollbarStyler.GetPreferredWidth(RuntimeScrollbarStyler.Size.Small), 592f);

        Scrollbar scrollbar = RuntimeScrollbarStyler.ApplyVertical(scrollbarObject, RuntimeScrollbarStyler.Size.Small, RuntimeScrollbarStyler.Tone.Mint);

        craftingRecipeScrollRect = viewportObject.GetComponent<ScrollRect>();
        craftingRecipeScrollRect.horizontal = false;
        craftingRecipeScrollRect.vertical = true;
        craftingRecipeScrollRect.movementType = ScrollRect.MovementType.Clamped;
        craftingRecipeScrollRect.viewport = viewportRect;
        craftingRecipeScrollRect.content = craftingRecipeContentRect;
        craftingRecipeScrollRect.verticalScrollbar = scrollbar;
        craftingRecipeScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        craftingRecipeScrollRect.scrollSensitivity = 30f;

        craftingRecipeBrowserObject.SetActive(false);
    }

    void CreateCraftingBlueprintBrowser(Transform parent)
    {
        craftingBlueprintBrowserObject = new GameObject("CraftingBlueprintBrowser", typeof(RectTransform), typeof(Image));
        craftingBlueprintBrowserObject.transform.SetParent(parent, false);

        RectTransform overlayRect = craftingBlueprintBrowserObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlay = craftingBlueprintBrowserObject.GetComponent<Image>();
        overlay.color = new Color(0.02f, 0.03f, 0.05f, 0.76f);
        overlay.raycastTarget = true;

        GameObject panel = new GameObject("CraftingBlueprintBrowserPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(craftingBlueprintBrowserObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        craftingBlueprintBrowserPanelRect = panelRect;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(1800f, 1080f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.99f);

        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.45f, 0.58f, 0.74f, 0.62f);
        panelOutline.effectDistance = new Vector2(4f, -4f);

        craftingBlueprintTitleText = CreateText(panel.transform, "CraftingBlueprintBrowserTitle", "BLUEPRINTS 0/0", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(520f, 44f), 32f, TextAlignmentOptions.Center);
        craftingBlueprintTitleText.characterSpacing = 2f;

        craftingBlueprintCloseButton = CreateButton(panel.transform, "CraftingBlueprintCloseButton", "CLOSE", new Vector2(650f, -62f), new Vector2(260f, 70f), HideCraftingBlueprintBrowser);
        StyleCompactBackLikeButton(craftingBlueprintCloseButton);
        TMP_Text closeText = craftingBlueprintCloseButton.GetComponentInChildren<TMP_Text>(true);
        if (closeText != null)
        {
            closeText.fontSize = 20f;
            closeText.characterSpacing = 2.2f;
        }

        GameObject viewportObject = new GameObject("CraftingBlueprintViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(panel.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        craftingBlueprintViewportRect = viewportRect;
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = new Vector2(-22f, -122f);
        viewportRect.sizeDelta = new Vector2(1630f, 836f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);

        GameObject contentObject = new GameObject("CraftingBlueprintContent", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        craftingBlueprintContentRect = contentObject.GetComponent<RectTransform>();
        craftingBlueprintContentRect.anchorMin = new Vector2(0f, 1f);
        craftingBlueprintContentRect.anchorMax = new Vector2(1f, 1f);
        craftingBlueprintContentRect.pivot = new Vector2(0.5f, 1f);
        craftingBlueprintContentRect.anchoredPosition = Vector2.zero;
        craftingBlueprintContentRect.sizeDelta = Vector2.zero;

        GridLayoutGroup layout = contentObject.GetComponent<GridLayoutGroup>();
        craftingBlueprintGridLayout = layout;
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = new Vector2(12f, 12f);
        layout.cellSize = new Vector2(520f, 480f);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        GameObject scrollbarObject = new GameObject("CraftingBlueprintScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarObject.transform.SetParent(panel.transform, false);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        craftingBlueprintScrollbarRect = scrollbarRect;
        scrollbarRect.anchorMin = new Vector2(0.5f, 0.5f);
        scrollbarRect.anchorMax = new Vector2(0.5f, 0.5f);
        scrollbarRect.pivot = new Vector2(0.5f, 0.5f);
        scrollbarRect.anchoredPosition = new Vector2(846f, -122f);
        scrollbarRect.sizeDelta = new Vector2(RuntimeScrollbarStyler.GetPreferredWidth(RuntimeScrollbarStyler.Size.Small), 836f);

        Scrollbar scrollbar = RuntimeScrollbarStyler.ApplyVertical(scrollbarObject, RuntimeScrollbarStyler.Size.Small, RuntimeScrollbarStyler.Tone.Blue);

        craftingBlueprintScrollRect = viewportObject.GetComponent<ScrollRect>();
        craftingBlueprintScrollRect.horizontal = false;
        craftingBlueprintScrollRect.vertical = true;
        craftingBlueprintScrollRect.movementType = ScrollRect.MovementType.Clamped;
        craftingBlueprintScrollRect.viewport = viewportRect;
        craftingBlueprintScrollRect.content = craftingBlueprintContentRect;
        craftingBlueprintScrollRect.verticalScrollbar = scrollbar;
        craftingBlueprintScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        craftingBlueprintScrollRect.scrollSensitivity = 30f;

        LayoutCraftingBlueprintBrowser();
        craftingBlueprintBrowserObject.SetActive(false);
    }

    void LayoutCraftingBlueprintBrowser()
    {
        if (craftingBlueprintBrowserObject == null || craftingBlueprintBrowserPanelRect == null)
            return;

        float availableWidth = 1920f;
        float availableHeight = 1080f;
        RectTransform overlayRect = craftingBlueprintBrowserObject.GetComponent<RectTransform>();
        if (overlayRect != null && overlayRect.rect.width > 1f && overlayRect.rect.height > 1f)
        {
            availableWidth = overlayRect.rect.width;
            availableHeight = overlayRect.rect.height;
        }
        else if (panelObject != null)
        {
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            if (panelRect != null && panelRect.rect.width > 1f && panelRect.rect.height > 1f)
            {
                availableWidth = panelRect.rect.width;
                availableHeight = panelRect.rect.height;
            }
        }

        float marginX = Mathf.Clamp(availableWidth * 0.035f, 18f, 54f);
        float marginY = Mathf.Clamp(availableHeight * 0.035f, 18f, 42f);
        float panelWidth = Mathf.Min(1800f, Mathf.Max(240f, availableWidth - marginX * 2f));
        float panelHeight = Mathf.Min(1080f, Mathf.Max(360f, availableHeight - marginY * 2f));

        craftingBlueprintBrowserPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        craftingBlueprintBrowserPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        craftingBlueprintBrowserPanelRect.pivot = new Vector2(0.5f, 0.5f);
        craftingBlueprintBrowserPanelRect.anchoredPosition = Vector2.zero;
        craftingBlueprintBrowserPanelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        float sidePadding = Mathf.Clamp(panelWidth * 0.045f, 20f, 56f);
        float topPadding = Mathf.Clamp(panelHeight * 0.13f, 82f, 142f);
        float bottomPadding = Mathf.Clamp(panelHeight * 0.05f, 22f, 54f);
        float scrollbarWidth = RuntimeScrollbarStyler.GetPreferredWidth(RuntimeScrollbarStyler.Size.Small);
        float gap = Mathf.Clamp(panelWidth * 0.012f, 10f, 18f);
        float viewportWidth = Mathf.Max(160f, panelWidth - sidePadding * 2f - scrollbarWidth - gap);
        float viewportHeight = Mathf.Max(140f, panelHeight - topPadding - bottomPadding);

        if (craftingBlueprintTitleText != null)
        {
            RectTransform titleRect = craftingBlueprintTitleText.rectTransform;
            float closeWidth = Mathf.Clamp(panelWidth * 0.2f, 140f, 260f);
            float titleWidth = Mathf.Min(520f, Mathf.Max(160f, panelWidth - closeWidth - sidePadding * 3f));
            float titleX = panelWidth < 980f ? -(closeWidth + sidePadding) * 0.22f : 0f;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(titleX, -Mathf.Clamp(panelHeight * 0.07f, 34f, 76f));
            titleRect.sizeDelta = new Vector2(titleWidth, 44f);
            craftingBlueprintTitleText.enableAutoSizing = true;
            craftingBlueprintTitleText.fontSizeMin = 20f;
            craftingBlueprintTitleText.fontSizeMax = 32f;
        }

        if (craftingBlueprintCloseButton != null)
        {
            RectTransform closeRect = craftingBlueprintCloseButton.GetComponent<RectTransform>();
            if (closeRect != null)
            {
                closeRect.anchorMin = new Vector2(1f, 1f);
                closeRect.anchorMax = new Vector2(1f, 1f);
                closeRect.pivot = new Vector2(1f, 1f);
                closeRect.anchoredPosition = new Vector2(-sidePadding, -Mathf.Clamp(panelHeight * 0.035f, 20f, 42f));
                closeRect.sizeDelta = new Vector2(Mathf.Clamp(panelWidth * 0.2f, 140f, 260f), Mathf.Clamp(panelHeight * 0.065f, 48f, 70f));
            }

            TMP_Text closeText = craftingBlueprintCloseButton.GetComponentInChildren<TMP_Text>(true);
            if (closeText != null)
            {
                closeText.enableAutoSizing = true;
                closeText.fontSizeMin = 14f;
                closeText.fontSizeMax = 20f;
            }
        }

        if (craftingBlueprintViewportRect != null)
        {
            craftingBlueprintViewportRect.anchorMin = new Vector2(0f, 1f);
            craftingBlueprintViewportRect.anchorMax = new Vector2(0f, 1f);
            craftingBlueprintViewportRect.pivot = new Vector2(0f, 1f);
            craftingBlueprintViewportRect.anchoredPosition = new Vector2(sidePadding, -topPadding);
            craftingBlueprintViewportRect.sizeDelta = new Vector2(viewportWidth, viewportHeight);
        }

        if (craftingBlueprintScrollbarRect != null)
        {
            craftingBlueprintScrollbarRect.anchorMin = new Vector2(1f, 1f);
            craftingBlueprintScrollbarRect.anchorMax = new Vector2(1f, 1f);
            craftingBlueprintScrollbarRect.pivot = new Vector2(1f, 1f);
            craftingBlueprintScrollbarRect.anchoredPosition = new Vector2(-sidePadding, -topPadding);
            craftingBlueprintScrollbarRect.sizeDelta = new Vector2(scrollbarWidth, viewportHeight);
        }

        if (craftingBlueprintGridLayout != null)
        {
            int columnCount = viewportWidth >= 1320f ? 3 : viewportWidth >= 760f ? 2 : 1;
            float gridPadding = Mathf.Clamp(viewportWidth * 0.018f, 10f, 18f);
            float gridSpacing = Mathf.Clamp(viewportWidth * 0.012f, 8f, 12f);
            float usableGridWidth = Mathf.Max(1f, viewportWidth - gridPadding * 2f - gridSpacing * (columnCount - 1));
            float cellWidth = Mathf.Clamp(Mathf.Floor(usableGridWidth / columnCount), 220f, 520f);
            float cellHeight = Mathf.Clamp(cellWidth * 0.92f, 260f, 480f);
            int roundedPadding = Mathf.RoundToInt(gridPadding);

            craftingBlueprintGridLayout.padding = new RectOffset(roundedPadding, roundedPadding, roundedPadding, roundedPadding);
            craftingBlueprintGridLayout.spacing = new Vector2(gridSpacing, gridSpacing);
            craftingBlueprintGridLayout.cellSize = new Vector2(cellWidth, cellHeight);
            craftingBlueprintGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            craftingBlueprintGridLayout.constraintCount = columnCount;
        }

        foreach (KeyValuePair<string, GameObject> row in craftingBlueprintRowsById)
            ApplyCraftingBlueprintRowLayout(row.Value);
    }

    Button CreateCraftingSlotButton(Transform parent, string objectName, Vector2 anchoredPosition, int slotIndex, out TMP_Text label, out Image icon)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(120f, 120f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.16f, 0.21f, 0.96f);

        Button button = buttonObject.GetComponent<Button>();
        ConfigureInventorySlotButtonTransition(button);
        button.onClick.AddListener(() => OnCraftingSlotClicked(slotIndex));

        ProfileCraftingSlotDragHandler dragHandler = buttonObject.AddComponent<ProfileCraftingSlotDragHandler>();
        dragHandler.owner = this;
        dragHandler.slotIndex = slotIndex;

        GameObject iconObject = new GameObject(objectName + "Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(buttonObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(86f, 86f);

        icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.enabled = false;

        label = CreateText(buttonObject.transform, objectName + "Text", string.Empty, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 22f, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.margin = new Vector4(6f, 6f, 6f, 6f);

        return button;
    }

    void OnCraftingCatalogClicked()
    {
        if (inventoryActionInProgress || dragInProgress || craftingRecipeBrowserObject == null)
            return;

        if (!craftingRecipeBrowserObject.activeSelf)
            resetCraftingRecipeScrollOnNextRefresh = true;

        RefreshCraftingRecipeBrowser();
        craftingRecipeBrowserObject.SetActive(true);
        craftingRecipeBrowserObject.transform.SetAsLastSibling();
    }

    void OnCraftingRecipeAvailabilityClicked()
    {
        if (inventoryActionInProgress || dragInProgress)
            return;

        craftingRecipeShowAvailableOnly = !craftingRecipeShowAvailableOnly;
        RefreshCraftingRecipeAvailabilityButton();
        RefreshCraftingRecipeBrowser(true);
    }

    void OnCraftingBlueprintsClicked()
    {
        if (inventoryActionInProgress || dragInProgress || craftingBlueprintBrowserObject == null)
            return;

        HideItemPreview();
        LayoutCraftingBlueprintBrowser();
        RefreshCraftingBlueprintBrowser();
        craftingBlueprintBrowserObject.SetActive(true);
        LayoutCraftingBlueprintBrowser();
        craftingBlueprintBrowserObject.transform.SetAsLastSibling();
    }

    void RefreshCraftingRecipeAvailabilityButton()
    {
        if (craftingRecipeAvailabilityButton == null)
            return;

        TMP_Text text = craftingRecipeAvailabilityButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = craftingRecipeShowAvailableOnly ? "Available" : "ALL";
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = 22f;
        }

        Image image = craftingRecipeAvailabilityButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = craftingRecipeShowAvailableOnly
                ? new Color(0.19f, 0.61f, 0.5f, 0.98f)
                : new Color(0.16f, 0.2f, 0.27f, 0.95f);
        }
    }

    void HideCraftingRecipeBrowser()
    {
        if (craftingRecipeBrowserObject != null)
            craftingRecipeBrowserObject.SetActive(false);
    }

    void HideCraftingBlueprintBrowser()
    {
        if (craftingBlueprintBrowserObject != null)
            craftingBlueprintBrowserObject.SetActive(false);
    }

    void RefreshCraftingBlueprintBrowser()
    {
        using (CraftingBlueprintRefreshMarker.Auto())
        {
            if (craftingBlueprintContentRect == null)
                return;

            LayoutCraftingBlueprintBrowser();
            ClearCraftingBlueprintRows();

            string[] blueprintItemIds = InventoryItemCatalog.GetAllBlueprintItemIds();
            if (blueprintItemIds == null)
                blueprintItemIds = Array.Empty<string>();

            Array.Sort(blueprintItemIds, CompareBlueprintItemNames);

            int unlockedBlueprintCount = 0;
            int totalBlueprintCount = 0;
            for (int i = 0; i < blueprintItemIds.Length; i++)
            {
                string blueprintItemId = blueprintItemIds[i];
                string targetItemId = InventoryItemCatalog.GetBlueprintTargetItemId(blueprintItemId);
                if (string.IsNullOrWhiteSpace(targetItemId))
                    continue;

                bool unlocked = PlayerProfileService.Instance.IsBlueprintUnlocked(blueprintItemId);
                totalBlueprintCount++;
                if (unlocked)
                    unlockedBlueprintCount++;

                GameObject rowObject = CreateCraftingBlueprintRow(blueprintItemId, targetItemId, unlocked);
                if (rowObject != null)
                {
                    rowObject.SetActive(true);
                    rowObject.transform.SetSiblingIndex(craftingBlueprintRowObjects.Count);
                    craftingBlueprintRowObjects.Add(rowObject);
                }
            }

            UpdateCraftingBlueprintTitle(unlockedBlueprintCount, totalBlueprintCount);

            if (craftingBlueprintScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                craftingBlueprintScrollRect.verticalNormalizedPosition = 1f;
            }
        }
    }

    void UpdateCraftingBlueprintTitle(int unlockedBlueprintCount, int totalBlueprintCount)
    {
        if (craftingBlueprintTitleText == null)
            return;

        craftingBlueprintTitleText.text = "BLUEPRINTS " +
                                          Mathf.Max(0, unlockedBlueprintCount) +
                                          "/" +
                                          Mathf.Max(0, totalBlueprintCount);
    }

    int CompareBlueprintItemNames(string leftBlueprintItemId, string rightBlueprintItemId)
    {
        string leftTargetItemId = InventoryItemCatalog.GetBlueprintTargetItemId(leftBlueprintItemId);
        string rightTargetItemId = InventoryItemCatalog.GetBlueprintTargetItemId(rightBlueprintItemId);
        string leftName = InventoryItemCatalog.GetDisplayName(leftTargetItemId);
        string rightName = InventoryItemCatalog.GetDisplayName(rightTargetItemId);
        int nameComparison = string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        return nameComparison != 0
            ? nameComparison
            : string.Compare(leftBlueprintItemId, rightBlueprintItemId, StringComparison.Ordinal);
    }

    void ClearCraftingBlueprintRows()
    {
        for (int i = 0; i < craftingBlueprintRowObjects.Count; i++)
        {
            GameObject rowObject = craftingBlueprintRowObjects[i];
            if (rowObject != null)
                rowObject.SetActive(false);
        }

        craftingBlueprintRowObjects.Clear();
    }

    Vector2 GetCraftingBlueprintCellSize()
    {
        if (craftingBlueprintGridLayout != null && craftingBlueprintGridLayout.cellSize.x > 1f && craftingBlueprintGridLayout.cellSize.y > 1f)
            return craftingBlueprintGridLayout.cellSize;

        return new Vector2(520f, 480f);
    }

    void ApplyCraftingBlueprintRowLayout(GameObject rowObject)
    {
        if (rowObject == null)
            return;

        Vector2 cellSize = GetCraftingBlueprintCellSize();

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        if (rowRect != null)
            rowRect.sizeDelta = cellSize;

        LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
        if (rowLayout != null)
        {
            rowLayout.preferredWidth = cellSize.x;
            rowLayout.preferredHeight = cellSize.y;
        }

        float labelHeight = Mathf.Clamp(cellSize.y * 0.105f, 30f, 44f);
        float iconWidth = Mathf.Max(160f, cellSize.x - 42f);
        float iconHeight = Mathf.Max(160f, cellSize.y - labelHeight - 18f);
        float iconSize = Mathf.Clamp(Mathf.Min(iconWidth, iconHeight), 160f, 444f);

        RectTransform iconRect = rowObject.transform.Find("BlueprintIcon")?.GetComponent<RectTransform>();
        if (iconRect != null)
        {
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -2f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        }

        TMP_Text nameText = rowObject.transform.Find("BlueprintItemName")?.GetComponent<TMP_Text>();
        if (nameText != null)
        {
            RectTransform nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0.5f, 0f);
            nameRect.anchorMax = new Vector2(0.5f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.anchoredPosition = new Vector2(0f, labelHeight * 0.5f);
            nameRect.sizeDelta = new Vector2(Mathf.Max(160f, cellSize.x - 36f), labelHeight);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 12f;
            nameText.fontSizeMax = Mathf.Clamp(cellSize.x * 0.05f, 16f, 26f);
        }
    }

    GameObject CreateCraftingBlueprintRow(string blueprintItemId, string targetItemId, bool unlocked)
    {
        if (craftingBlueprintRowsById.TryGetValue(blueprintItemId, out GameObject cachedRow) && cachedRow != null)
        {
            UpdateCraftingBlueprintRow(cachedRow, blueprintItemId, targetItemId, unlocked);
            return cachedRow;
        }

        GameObject rowObject = new GameObject("CraftingBlueprintTile_" + blueprintItemId, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowObject.transform.SetParent(craftingBlueprintContentRect, false);

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.sizeDelta = GetCraftingBlueprintCellSize();

        LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
        rowLayout.preferredWidth = rowRect.sizeDelta.x;
        rowLayout.preferredHeight = rowRect.sizeDelta.y;

        Image rowImage = rowObject.GetComponent<Image>();
        rowImage.color = new Color(0f, 0f, 0f, 0f);
        rowImage.raycastTarget = false;

        GameObject iconObject = new GameObject("BlueprintIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(rowObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -2f);
        iconRect.sizeDelta = new Vector2(444f, 444f);

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = InventoryItemCatalog.GetIcon(blueprintItemId);
        iconImage.enabled = iconImage.sprite != null;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.color = unlocked
            ? Color.white
            : new Color(0.38f, 0.38f, 0.38f, 0.9f);

        TMP_Text nameText = CreateText(rowObject.transform, "BlueprintItemName", InventoryItemCatalog.GetDisplayName(targetItemId).ToUpperInvariant(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(472f, 44f), 26f, TextAlignmentOptions.Center);
        nameText.fontStyle = FontStyles.Bold;
        nameText.textWrappingMode = TextWrappingModes.Normal;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 15f;
        nameText.fontSizeMax = 26f;
        ApplyCraftingBlueprintRowLayout(rowObject);
        UpdateCraftingBlueprintRow(rowObject, blueprintItemId, targetItemId, unlocked);

        craftingBlueprintRowsById[blueprintItemId] = rowObject;
        return rowObject;
    }

    void UpdateCraftingBlueprintRow(GameObject rowObject, string blueprintItemId, string targetItemId, bool unlocked)
    {
        if (rowObject == null)
            return;

        ApplyCraftingBlueprintRowLayout(rowObject);

        Image iconImage = rowObject.transform.Find("BlueprintIcon")?.GetComponent<Image>();
        if (iconImage != null)
        {
            iconImage.sprite = InventoryItemCatalog.GetIcon(blueprintItemId);
            iconImage.enabled = iconImage.sprite != null;
            iconImage.color = unlocked
                ? Color.white
                : new Color(0.38f, 0.38f, 0.38f, 0.9f);
        }

        TMP_Text nameText = rowObject.transform.Find("BlueprintItemName")?.GetComponent<TMP_Text>();
        if (nameText != null)
        {
            nameText.text = InventoryItemCatalog.GetDisplayName(targetItemId).ToUpperInvariant();
            nameText.color = unlocked
                ? new Color(0.78f, 0.9f, 1f, 1f)
                : new Color(0.58f, 0.58f, 0.58f, 0.95f);
        }
    }
}
