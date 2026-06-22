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
    async void SwitchToScreen(ProfileScreen screen, bool clearStatus = true)
    {
        if (!await TryClearCraftingSlotsBeforeLeavingAsync(screen))
            return;

        using (SwitchScreenMarker.Auto())
        {
            currentScreen = screen;
            MarkCurrentScreenDirty();
            skinVisualsDirty = true;
            saveAndRunStyleDirty = true;
            if (clearStatus)
                SetStatus(string.Empty);

            HideItemPreview();
            HideCraftingRecipeBrowser();
            HideCraftingBlueprintBrowser();
            HideShopBrowser();
            HideShipImageModal();

            if (screen == ProfileScreen.Trader)
            {
                selectedTraderShop = TraderShopKind.None;
                RefreshTraderSelectionVisuals();
            }

            if (screen == ProfileScreen.ShipSelection)
            {
                ResetShipSelectionCarouselMotion();
                shipSelectionCenterType = GetSelectedShipType();
                shipSelectionCenterIndex = Mathf.Clamp(Array.IndexOf(SelectableShipTypes, shipSelectionCenterType), 0, SelectableShipTypes.Length - 1);
                RefreshShipSelectionView();
            }
            else if (screen == ProfileScreen.PilotSelection)
            {
                ResetPilotSelectionCarouselMotion();
                pilotSelectionCenterIndex = PilotCatalog.GetPilotIndex(selectedPilotId);
                RefreshPilotSelectionView();
            }
            else if (screen == ProfileScreen.Projects)
            {
                RefreshProjectsView();
            }
            else if (screen == ProfileScreen.ProjectDetails)
            {
                RefreshProjectDetailsView();
            }
            else
            {
                ResetShipSelectionCarouselMotion();
                ResetPilotSelectionCarouselMotion();
            }

            ApplyProfileScreenLayout();
            profileLayoutDirty = false;

            if (screen == ProfileScreen.Crafting)
                RefreshCraftingRecipeBrowser();
            else if (screen == ProfileScreen.Trader)
                RefreshShopBrowser();

        }
    }

    void OnProfileBackClicked()
    {
        if (currentScreen == ProfileScreen.ProjectDetails)
        {
            SwitchToScreen(ProfileScreen.Projects);
            return;
        }

        SwitchToScreen(ProfileScreen.Home);
    }

    async Task<bool> TryClearCraftingSlotsBeforeLeavingAsync(ProfileScreen nextScreen)
    {
        if (currentScreen != ProfileScreen.Crafting || nextScreen == ProfileScreen.Crafting)
            return true;

        return await ClearCraftingSlotsAsync(false, true);
    }

    void ApplyProfileScreenLayout()
    {
        if (panelObject == null)
            return;

        ApplyPanelBackgroundForCurrentScreen();
        LayoutTopBar();
        LayoutRightActions();
        LayoutLeftNavigation();
        ConfigureEmbeddedCraftingRecipeBrowser();
        LayoutCraftingBlueprintBrowser();
        ConfigureEmbeddedTraderBrowser();

        bool splashShowing = IsSplashShowing();

        bool showHome = currentScreen == ProfileScreen.Home;
        bool showInventory = currentScreen == ProfileScreen.Inventory;
        bool showCrafting = currentScreen == ProfileScreen.Crafting;
        bool showTrader = currentScreen == ProfileScreen.Trader;
        bool showPlayer = currentScreen == ProfileScreen.Player;
        bool showShipSelection = currentScreen == ProfileScreen.ShipSelection;
        bool showPilotSelection = currentScreen == ProfileScreen.PilotSelection;
        bool showProjects = currentScreen == ProfileScreen.Projects;
        bool showProjectDetails = currentScreen == ProfileScreen.ProjectDetails;
        bool showFullscreenSelection = showShipSelection || showPilotSelection;
        bool showSharedNavigation = !showFullscreenSelection || showPilotSelection;

        if (homeViewRootObject != null)
            homeViewRootObject.SetActive(showHome);
        if (inventoryViewRootObject != null)
            inventoryViewRootObject.SetActive(showInventory);
        if (craftingViewRootObject != null)
            craftingViewRootObject.SetActive(showCrafting);
        if (traderViewRootObject != null)
            traderViewRootObject.SetActive(showTrader);
        if (playerViewRootObject != null)
            playerViewRootObject.SetActive(showPlayer);
        if (projectsViewRootObject != null)
            projectsViewRootObject.SetActive(showProjects);
        if (projectDetailsViewObject != null)
            projectDetailsViewObject.SetActive(showProjectDetails);
        if (shipSelectionViewObject != null)
            shipSelectionViewObject.SetActive(showShipSelection);
        if (pilotSelectionViewObject != null)
            pilotSelectionViewObject.SetActive(showPilotSelection);
        if (!showShipSelection)
            HideShipSelectionMissionDetails();

        if (topBarRootObject != null)
            topBarRootObject.SetActive(!showFullscreenSelection);
        if (leftNavigationRootObject != null)
            leftNavigationRootObject.SetActive(showSharedNavigation);
        if (rightActionRootObject != null)
            rightActionRootObject.SetActive(showHome);
        if (homeViewRootObject != null)
            homeViewRootObject.transform.SetAsFirstSibling();
        if (topBarRootObject != null)
            topBarRootObject.transform.SetAsLastSibling();
        if (leftNavigationRootObject != null)
            leftNavigationRootObject.transform.SetAsLastSibling();
        if (rightActionRootObject != null)
            rightActionRootObject.transform.SetAsLastSibling();
        if (shipTypeLabelText != null)
            shipTypeLabelText.gameObject.SetActive(false);
        if (shipSkinLabelText != null)
            shipSkinLabelText.gameObject.SetActive(false);
        if (inventoryHintText != null)
            inventoryHintText.gameObject.SetActive(false);
        if (shipImageModalObject != null && showShipSelection)
            shipImageModalObject.SetActive(false);
        if (shipImageModalObject != null && showPilotSelection)
            shipImageModalObject.SetActive(false);
        if (pilotPortraitRootObject != null)
            pilotPortraitRootObject.SetActive(showHome);
        if (projectsButtonRootObject != null)
            projectsButtonRootObject.SetActive(showHome);

        if (shipTypeButtons != null)
        {
            for (int i = 0; i < shipTypeButtons.Length; i++)
            {
                if (shipTypeButtons[i] != null)
                    shipTypeButtons[i].gameObject.SetActive(false);
            }
        }

        if (skinButtons != null)
        {
            for (int i = 0; i < skinButtons.Length; i++)
            {
                if (skinButtons[i] != null)
                    skinButtons[i].gameObject.SetActive(false);
            }
        }

        if (shipWorkspaceRootObject != null)
        {
            Transform targetParent =
                showHome ? homeViewRootObject.transform :
                showInventory ? inventoryViewRootObject.transform :
                showPlayer ? playerViewRootObject.transform :
                panelObject.transform;
            shipWorkspaceRootObject.transform.SetParent(targetParent, false);
        }
        if (storageViewRootObject != null)
            storageViewRootObject.transform.SetParent((showInventory || showCrafting || showTrader) ? (showInventory ? inventoryViewRootObject.transform : showCrafting ? craftingViewRootObject.transform : traderViewRootObject.transform) : panelObject.transform, false);

        if (shipWorkspaceRootObject != null)
            shipWorkspaceRootObject.SetActive(showHome || showInventory || showCrafting || showTrader || showPlayer);
        if (storageViewRootObject != null)
            storageViewRootObject.SetActive(showInventory || showCrafting || showTrader);

        bool showStorage = showInventory || showCrafting || showTrader;
        if (shipInventoryLabelText != null)
            shipInventoryLabelText.gameObject.SetActive(showStorage);
        if (shipInventoryUnloadButton != null)
            shipInventoryUnloadButton.gameObject.SetActive(showStorage);
        if (playerInventoryLabelText != null)
            playerInventoryLabelText.gameObject.SetActive(showStorage);
        if (playerInventoryCountText != null)
            playerInventoryCountText.gameObject.SetActive(showStorage);
        if (playerInventoryFilterButton != null)
            playerInventoryFilterButton.gameObject.SetActive(showStorage);
        if (playerInventorySortButton != null)
            playerInventorySortButton.gameObject.SetActive(showStorage);
        if (playerInventoryExtendButton != null)
            playerInventoryExtendButton.gameObject.SetActive(showStorage);
        if (playerInventoryScrollRect != null)
            playerInventoryScrollRect.gameObject.SetActive(showStorage);
        if (playerInventoryScrollbarObject != null)
            playerInventoryScrollbarObject.SetActive(showStorage);
        if (shipInventoryButtons != null)
        {
            int shipCapacity = GetActiveShipInventoryCapacity();
            for (int i = 0; i < shipInventoryButtons.Length; i++)
            {
                if (shipInventoryButtons[i] != null)
                    shipInventoryButtons[i].gameObject.SetActive(showStorage && i < shipCapacity);
            }
        }

        if (craftingPanelObject != null)
            craftingPanelObject.SetActive(showCrafting);
        if (craftingRecipeBrowserObject != null)
            craftingRecipeBrowserObject.SetActive(showCrafting);
        if (craftingBlueprintBrowserObject != null && !showCrafting)
            craftingBlueprintBrowserObject.SetActive(false);
        if (shopBrowserObject != null)
            shopBrowserObject.SetActive(showTrader && TraderOpensShop(selectedTraderShop));
        if (traderFuturePanelObject != null)
            traderFuturePanelObject.SetActive(showTrader);
        if (statusText != null)
            statusText.gameObject.SetActive((!showHome && !showProjects && !showProjectDetails) || NetworkManager.SessionRequested || !string.IsNullOrWhiteSpace(statusText.text));

        if (splashShowing)
        {
            if (topBarRootObject != null)
                topBarRootObject.SetActive(false);
            if (leftNavigationRootObject != null)
                leftNavigationRootObject.SetActive(false);
            if (rightActionRootObject != null)
                rightActionRootObject.SetActive(false);
            if (homeViewRootObject != null)
                homeViewRootObject.SetActive(false);
            if (inventoryViewRootObject != null)
                inventoryViewRootObject.SetActive(false);
            if (craftingViewRootObject != null)
                craftingViewRootObject.SetActive(false);
            if (traderViewRootObject != null)
                traderViewRootObject.SetActive(false);
            if (playerViewRootObject != null)
                playerViewRootObject.SetActive(false);
            if (projectsViewRootObject != null)
                projectsViewRootObject.SetActive(false);
            if (projectDetailsViewObject != null)
                projectDetailsViewObject.SetActive(false);
            if (shipSelectionViewObject != null)
                shipSelectionViewObject.SetActive(false);
            if (pilotSelectionViewObject != null)
                pilotSelectionViewObject.SetActive(false);
            if (shipWorkspaceRootObject != null)
                shipWorkspaceRootObject.SetActive(false);
            if (storageViewRootObject != null)
                storageViewRootObject.SetActive(false);
            if (statusText != null)
                statusText.gameObject.SetActive(false);
            splashScreenObject.transform.SetAsLastSibling();
            return;
        }

        LayoutHomeScreen();
        LayoutProjectsScreen();
        LayoutProjectDetailsScreen();
        LayoutInventoryScreen();
        LayoutCraftingScreen();
        LayoutTraderScreen();
        LayoutPlayerScreen();
        RefreshEquipmentSlotPreview();
        ApplyShipWorkspaceScreenMode(showHome || showInventory);
        EnsureShipPreviewBackgroundHidden();
        ApplyItemPreviewLayout();
        EnsureStatusTextLayering();
        EnsureProfileModalLayering();
    }

    void EnsureStatusTextLayering()
    {
        if (panelObject == null || statusText == null || !statusText.gameObject.activeSelf)
            return;

        statusText.transform.SetParent(panelObject.transform, false);
        statusText.transform.SetAsLastSibling();
    }

    void EnsureProfileModalLayering()
    {
        if (panelObject == null)
            return;

        if (shipImageModalObject != null && shipImageModalObject.activeSelf)
        {
            shipImageModalObject.transform.SetParent(panelObject.transform, false);
            shipImageModalObject.transform.SetAsLastSibling();
        }

        if (playerInventoryExtendConfirmObject != null && playerInventoryExtendConfirmObject.activeSelf)
        {
            playerInventoryExtendConfirmObject.transform.SetParent(panelObject.transform, false);
            playerInventoryExtendConfirmObject.transform.SetAsLastSibling();
        }

        if (shipInventoryStartConfirmObject != null && shipInventoryStartConfirmObject.activeSelf)
        {
            shipInventoryStartConfirmObject.transform.SetParent(panelObject.transform, false);
            shipInventoryStartConfirmObject.transform.SetAsLastSibling();
        }

        if (itemInfoOverlayObject != null && itemInfoOverlayObject.activeSelf)
        {
            itemInfoOverlayObject.transform.SetParent(panelObject.transform, false);
            itemInfoOverlayObject.transform.SetAsLastSibling();
        }

        if (craftingBlueprintBrowserObject != null && craftingBlueprintBrowserObject.activeSelf)
        {
            craftingBlueprintBrowserObject.transform.SetParent(panelObject.transform, false);
            craftingBlueprintBrowserObject.transform.SetAsLastSibling();
        }

    }

    void ApplyPanelBackgroundForCurrentScreen()
    {
        if (panelObject == null)
            return;

        Image background = panelObject.GetComponent<Image>();
        if (background == null)
            return;

        string assetName = currentScreen switch
        {
            ProfileScreen.Inventory => "hangar1_2D_przesuniety.png",
            ProfileScreen.Projects => "PROJECTS_SCREEN.png",
            ProfileScreen.ProjectDetails => GetSelectedProjectBackgroundAssetName(),
            _ => "hangar1_2D.png"
        };

        Sprite sprite = LoadStandaloneSprite(assetName);
        if (sprite != null)
        {
            background.sprite = sprite;
            background.color = Color.white;
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
        }
        else
        {
            background.sprite = null;
            background.color = new Color(0.05f, 0.08f, 0.12f, 1f);
            background.type = Image.Type.Simple;
        }
    }

    string GetSelectedProjectBackgroundAssetName()
    {
        ProjectDefinition project = ProjectCatalog.Get(selectedProjectId) ?? ProjectCatalog.GetDefault();
        string resourcePath = project != null && !string.IsNullOrWhiteSpace(project.BackgroundResourcePath)
            ? project.BackgroundResourcePath
            : "PROJECTS_SCREEN";
        return resourcePath + ".png";
    }

    void ApplyShipWorkspaceScreenMode(bool showFullDetails)
    {
        if (shipPreviewTitleText != null)
            shipPreviewTitleText.gameObject.SetActive(showFullDetails);

        if (shipStatsPanelObject != null)
            shipStatsPanelObject.SetActive(showFullDetails);

        if (equipmentSlotButtons != null && !showFullDetails)
        {
            for (int i = 0; i < equipmentSlotButtons.Length; i++)
            {
                if (equipmentSlotButtons[i] != null)
                    equipmentSlotButtons[i].gameObject.SetActive(false);
            }
        }

        if (shipPreviewButton != null)
            shipPreviewButton.interactable = showFullDetails && shipPreviewImage != null && shipPreviewImage.sprite != null && !inventoryActionInProgress;

        Transform hitbox = shipPreviewRootRect != null ? shipPreviewRootRect.transform.Find("ShipPreviewHitbox") : null;
        if (hitbox != null)
        {
            Image image = hitbox.GetComponent<Image>();
            if (image != null)
                image.raycastTarget = showFullDetails;
        }
    }

    void EnsureShipPreviewBackgroundHidden()
    {
        if (shipPreviewRootRect == null)
            return;

        Image rootImage = shipPreviewRootRect.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.sprite = null;
            rootImage.color = new Color(0f, 0f, 0f, 0f);
            rootImage.raycastTarget = false;
            rootImage.enabled = false;
        }

        if (shipPreviewButton != null)
        {
            shipPreviewButton.transition = Selectable.Transition.None;
            shipPreviewButton.targetGraphic = null;
        }

        for (int i = 0; i < shipPreviewRootRect.childCount; i++)
        {
            Transform child = shipPreviewRootRect.GetChild(i);
            if (child == null)
                continue;

            Image image = child.GetComponent<Image>();
            if (image == null)
                continue;

        if (child.name == "ShipPreviewImage")
        {
            Color previewColor = Color.white;
            if (currentScreen == ProfileScreen.Crafting || currentScreen == ProfileScreen.Trader)
                previewColor = new Color(0.42f, 0.46f, 0.52f, 1f);

            image.enabled = shipPreviewImage != null;
            image.color = shipPreviewImage != null && shipPreviewImage.sprite != null
                ? previewColor
                : new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            continue;
        }

            if (child.name == "ShipPreviewHitbox")
            {
                image.sprite = null;
                image.color = new Color(1f, 1f, 1f, 0f);
                image.enabled = true;
                image.raycastTarget = currentScreen == ProfileScreen.Home || currentScreen == ProfileScreen.Inventory;
                continue;
            }

            RectTransform childRect = child as RectTransform;
            if (childRect != null &&
                childRect.sizeDelta.x >= shipPreviewRootRect.sizeDelta.x * 0.55f &&
                childRect.sizeDelta.y >= shipPreviewRootRect.sizeDelta.y * 0.55f)
            {
                image.sprite = null;
                image.color = new Color(0f, 0f, 0f, 0f);
                image.enabled = false;
                image.raycastTarget = false;
            }
        }
    }

    void LayoutTopBar()
    {
        if (topBarRootObject == null)
            return;

        if (sharedTopBar == null)
            ConfigureSharedTopBar();

        if (sharedTopBar == null)
            return;

        RectTransform rootRect = topBarRootObject.GetComponent<RectTransform>();
        float rootWidth = rootRect != null && rootRect.rect.width > 0f ? rootRect.rect.width : 1440f;
        sharedTopBar.Layout(rootWidth);
    }

    void EnsureTopStatBanner()
    {
        if (topBarRootObject == null)
            return;

        if (topStatBannerObject == null)
        {
            topStatBannerObject = new GameObject("ProfileTopStatBanner", typeof(RectTransform), typeof(Image));
            topStatBannerObject.transform.SetParent(topBarRootObject.transform, false);

            GameObject innerPanelObject = new GameObject("InnerPanel", typeof(RectTransform), typeof(Image));
            innerPanelObject.transform.SetParent(topStatBannerObject.transform, false);

            GameObject topAccentObject = new GameObject("TopAccent", typeof(RectTransform), typeof(Image));
            topAccentObject.transform.SetParent(topStatBannerObject.transform, false);

            GameObject bottomAccentObject = new GameObject("BottomAccent", typeof(RectTransform), typeof(Image));
            bottomAccentObject.transform.SetParent(topStatBannerObject.transform, false);

            GameObject leftAccentObject = new GameObject("LeftAccent", typeof(RectTransform), typeof(Image));
            leftAccentObject.transform.SetParent(topStatBannerObject.transform, false);

            GameObject rightAccentObject = new GameObject("RightAccent", typeof(RectTransform), typeof(Image));
            rightAccentObject.transform.SetParent(topStatBannerObject.transform, false);
        }

        RectTransform rect = topStatBannerObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(370f, -40f);
        rect.sizeDelta = new Vector2(1048f, 58f);

        Image frame = topStatBannerObject.GetComponent<Image>();
        frame.color = new Color(0.33f, 0.39f, 0.47f, 0.94f);
        frame.raycastTarget = false;

        Transform innerPanel = topStatBannerObject.transform.Find("InnerPanel");
        if (innerPanel != null)
        {
            RectTransform innerRect = innerPanel.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.pivot = new Vector2(0.5f, 0.5f);
            innerRect.offsetMin = new Vector2(8f, 8f);
            innerRect.offsetMax = new Vector2(-8f, -8f);

            Image innerImage = innerPanel.GetComponent<Image>();
            innerImage.color = new Color(0.05f, 0.09f, 0.13f, 0.78f);
            innerImage.raycastTarget = false;
        }

        ConfigureTopBannerAccent("TopAccent", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(128f, 4f), new Color(0.35f, 0.82f, 1f, 0.32f));
        ConfigureTopBannerAccent("BottomAccent", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(96f, 3f), new Color(0.35f, 0.82f, 1f, 0.24f));
        ConfigureTopBannerAccent("LeftAccent", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(6f, 22f), new Color(0.35f, 0.82f, 1f, 0.78f));
        ConfigureTopBannerAccent("RightAccent", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(6f, 22f), new Color(0.35f, 0.82f, 1f, 0.78f));

        topStatBannerObject.transform.SetAsFirstSibling();
    }

    void ConfigureTopBannerAccent(string childName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        if (topStatBannerObject == null)
            return;

        Transform child = topStatBannerObject.transform.Find(childName);
        if (child == null)
            return;

        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    void LayoutRightActions()
    {
        if (exitGameButton != null)
        {
            RectTransform rect = exitGameButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-44f, -34f);
            rect.sizeDelta = new Vector2(194f, 54f);
            StyleExitGameButton();
        }

        if (saveAndRunButton != null)
        {
            RectTransform rect = saveAndRunButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-40f, 36f);
            saveAndRunButton.transform.SetAsLastSibling();
        }
    }

    void LayoutLeftNavigation()
    {
        bool showScreenButtons = currentScreen == ProfileScreen.Home;

        if (navBackButton != null)
        {
            navBackButton.gameObject.SetActive(currentScreen != ProfileScreen.Home);
            LayoutSharedProfileBackButton(navBackButton.GetComponent<RectTransform>());
        }

        if (navCraftingButton != null)
            navCraftingButton.gameObject.SetActive(showScreenButtons);
        if (shopButton != null)
            shopButton.gameObject.SetActive(showScreenButtons);
        if (navInventoryButton != null)
            navInventoryButton.gameObject.SetActive(showScreenButtons);
        if (navPlayerButton != null)
            navPlayerButton.gameObject.SetActive(showScreenButtons);

        if (showScreenButtons && navCraftingButton != null)
            SetAnchoredRect(navCraftingButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -290f), new Vector2(326f, 88f));
        if (showScreenButtons && shopButton != null)
            SetAnchoredRect(shopButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -418f), new Vector2(326f, 88f));
        if (showScreenButtons && navInventoryButton != null)
            SetAnchoredRect(navInventoryButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -546f), new Vector2(326f, 88f));
        if (showScreenButtons && navPlayerButton != null)
            SetAnchoredRect(navPlayerButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -674f), new Vector2(326f, 88f));

        if (showScreenButtons && navCraftingButton != null)
            SetNavigationButtonSelected(navCraftingButton, currentScreen == ProfileScreen.Crafting, new Color(0.16f, 0.3f, 0.46f, 0.98f), new Color(0.22f, 0.42f, 0.62f, 1f));
        if (showScreenButtons && shopButton != null)
            SetNavigationButtonSelected(shopButton, currentScreen == ProfileScreen.Trader, new Color(0.18f, 0.34f, 0.5f, 0.98f), new Color(0.24f, 0.46f, 0.66f, 1f));
        if (showScreenButtons && navInventoryButton != null)
            SetNavigationButtonSelected(navInventoryButton, currentScreen == ProfileScreen.Inventory, new Color(0.16f, 0.3f, 0.46f, 0.98f), new Color(0.22f, 0.42f, 0.62f, 1f));
        if (showScreenButtons && navPlayerButton != null)
            SetNavigationButtonSelected(navPlayerButton, currentScreen == ProfileScreen.Player, new Color(0.2f, 0.28f, 0.38f, 0.98f), new Color(0.3f, 0.42f, 0.56f, 1f));
    }

    void LayoutSharedProfileBackButton(RectTransform rect)
    {
        if (rect == null)
            return;

        SetAnchoredRect(
            rect,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-116f, -106f),
            new Vector2(216f, 62f));
    }

    void SetNavigationButtonSelected(Button button, bool selected, Color normalColor, Color highlightedColor)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = selected ? highlightedColor : normalColor;

        ColorBlock colors = button.colors;
        colors.normalColor = selected ? highlightedColor : normalColor;
        colors.selectedColor = selected ? highlightedColor : normalColor;
        colors.highlightedColor = selected ? highlightedColor : Color.Lerp(normalColor, Color.white, 0.08f);
        colors.pressedColor = selected ? Color.Lerp(highlightedColor, Color.black, 0.08f) : Color.Lerp(normalColor, Color.black, 0.16f);
        colors.disabledColor = new Color(0.26f, 0.28f, 0.31f, 0.8f);
        button.colors = colors;
    }

    void LayoutHomeScreen()
    {
        if (currentScreen != ProfileScreen.Home)
            return;

        if (shipPreviewTitleText != null)
        {
            RectTransform rect = shipPreviewTitleText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-62f, 392f);
            rect.sizeDelta = new Vector2(620f, 44f);
            shipPreviewTitleText.fontSize = 34f;
        }

        if (shipPreviewRootRect != null)
        {
            shipPreviewRootRect.anchorMin = new Vector2(0.5f, 0.5f);
            shipPreviewRootRect.anchorMax = new Vector2(0.5f, 0.5f);
            shipPreviewRootRect.pivot = new Vector2(0.5f, 0.5f);
            shipPreviewRootRect.anchoredPosition = new Vector2(-62f, -18f);
            shipPreviewRootRect.sizeDelta = new Vector2(1340f, 880f);
            Image background = shipPreviewRootRect.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0f, 0f, 0f, 0f);
                background.enabled = false;
            }
        }

        if (shipPreviewImage != null)
        {
            RectTransform imageRect = shipPreviewImage.rectTransform;
            imageRect.anchoredPosition = new Vector2(0f, 10f);
            imageRect.sizeDelta = new Vector2(980f, 756f);
        }

        Transform hitbox = shipPreviewRootRect != null ? shipPreviewRootRect.transform.Find("ShipPreviewHitbox") : null;
        if (hitbox != null)
        {
            RectTransform hitboxRect = hitbox.GetComponent<RectTransform>();
            if (hitboxRect != null)
                hitboxRect.sizeDelta = new Vector2(1140f, 780f);
        }

        if (equipmentSlotRects != null)
            LayoutEquipmentSlotsColumn(-520f, -386f, -28f, 116f, 104f);

        if (shipStatsPanelObject != null)
        {
            RectTransform rect = shipStatsPanelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -18f);
            rect.sizeDelta = new Vector2(1340f, 880f);
        }

        LayoutShipStatsVertical(484f, -52f, new Vector2(278f, 72f), 12f);
        LayoutPilotPortrait();
        LayoutProjectsHomeButton();
    }

    void LayoutPilotPortrait()
    {
        if (pilotPortraitRootObject == null)
            return;

        RectTransform rect = pilotPortraitRootObject.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-70f, 246f);
        rect.sizeDelta = new Vector2(340f, 350f);

        if (pilotPortraitRootObject != null)
            pilotPortraitRootObject.transform.SetAsLastSibling();
    }

    void LayoutProjectsHomeButton()
    {
        if (projectsButtonRootObject == null)
            return;

        RectTransform rect = projectsButtonRootObject.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-28f, -184f);
        rect.sizeDelta = new Vector2(370f, 280f);
        projectsButtonRootObject.transform.SetAsLastSibling();
    }

    void LayoutAmbientShipBackdrop()
    {
        if (shipWorkspaceRootObject == null || !shipWorkspaceRootObject.activeSelf)
            return;

        shipWorkspaceRootObject.transform.SetAsFirstSibling();

        if (shipPreviewRootRect != null)
        {
            shipPreviewRootRect.anchorMin = new Vector2(0.5f, 0.5f);
            shipPreviewRootRect.anchorMax = new Vector2(0.5f, 0.5f);
            shipPreviewRootRect.pivot = new Vector2(0.5f, 0.5f);
            shipPreviewRootRect.anchoredPosition = new Vector2(-62f, -18f);
            shipPreviewRootRect.sizeDelta = new Vector2(1340f, 880f);
        }

        if (shipPreviewImage != null)
        {
            RectTransform imageRect = shipPreviewImage.rectTransform;
            imageRect.anchoredPosition = new Vector2(0f, 10f);
            imageRect.sizeDelta = new Vector2(980f, 756f);
        }

        Transform hitbox = shipPreviewRootRect != null ? shipPreviewRootRect.transform.Find("ShipPreviewHitbox") : null;
        if (hitbox != null)
        {
            RectTransform hitboxRect = hitbox.GetComponent<RectTransform>();
            if (hitboxRect != null)
                hitboxRect.sizeDelta = new Vector2(1140f, 780f);
        }
    }

    void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }
}
