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
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayerProfileService.Instance.ProfileChanged += OnProfileChanged;
    }

    async void Start()
    {
        await PlayerProfileService.Instance.EnsureInitializedAsync();
        EnsurePanel();
        RefreshView();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (PlayerProfileService.HasInstance)
            PlayerProfileService.Instance.ProfileChanged -= OnProfileChanged;

        if (instance == this)
            instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cachedCanvasTransform = null;
        profileDuplicatePanelsChecked = false;
        profilePanelVisibilityInitialized = false;
        gameplayHudObjectsByName.Clear();
        MarkAllProfileUiDirty();
        EnsurePanel();
        RefreshView();
        RefreshVisibility();
    }

    void OnProfileChanged(PlayerProfileData profile)
    {
        if (suppressNextProfileChangedRefresh)
            return;

        RefreshView();
        MarkAllProfileUiDirty();
        if (!NetworkManager.SessionRequested)
        {
            SetInteractable(true);
        }
        RefreshLobbyUi();
    }

    void Update()
    {
        EnsurePanel();
        if (!RefreshVisibility())
            return;

        bool splashShowing = IsSplashShowing();
        if (profileLayoutDirty || splashShowing || lastSplashShowing)
        {
            ApplyProfileScreenLayout();
            profileLayoutDirty = false;
        }
        lastSplashShowing = splashShowing;

        if (skinVisualsDirty)
        {
            UpdateSkinButtonVisuals();
            skinVisualsDirty = false;
        }

        if (saveAndRunStyleDirty)
        {
            ApplySaveAndRunButtonStyle();
            saveAndRunStyleDirty = false;
        }

        if (itemPreviewLayoutDirty)
        {
            ApplyItemPreviewLayout();
            itemPreviewLayoutDirty = false;
        }

        if (currentScreen == ProfileScreen.ShipSelection && shipSelectionDirty)
            RefreshShipSelectionView();
        if (currentScreen == ProfileScreen.PilotSelection && pilotSelectionDirty)
            RefreshPilotSelectionView();
        UpdateSelectionCarouselAnimations();
        if (currentScreen == ProfileScreen.Projects && projectsDirty)
            RefreshProjectsView();
        if (currentScreen == ProfileScreen.ProjectDetails && projectDetailsDirty)
            RefreshProjectDetailsView();
    }

    bool IsSplashShowing()
    {
        bool profileLoading = PlayerProfileService.HasInstance && !PlayerProfileService.Instance.IsInitialized;
        return splashScreenObject != null && (profileLoading || splashHideTime > 0f && Time.unscaledTime < splashHideTime);
    }

    void MarkAllProfileUiDirty()
    {
        profileLayoutDirty = true;
        skinVisualsDirty = true;
        saveAndRunStyleDirty = true;
        itemPreviewLayoutDirty = true;
        shipSelectionDirty = true;
        pilotSelectionDirty = true;
        projectsDirty = true;
        projectDetailsDirty = true;
        InvalidateInteractableState();
    }

    void MarkCurrentScreenDirty()
    {
        profileLayoutDirty = true;
        itemPreviewLayoutDirty = true;
        InvalidateInteractableState();

        switch (currentScreen)
        {
            case ProfileScreen.ShipSelection:
                shipSelectionDirty = true;
                break;
            case ProfileScreen.PilotSelection:
                pilotSelectionDirty = true;
                break;
            case ProfileScreen.Projects:
                projectsDirty = true;
                break;
            case ProfileScreen.ProjectDetails:
                projectDetailsDirty = true;
                break;
        }
    }

    void EnsurePanel()
    {
        Transform canvasTransform = GetCanvasTransform();
        if (canvasTransform == null)
            return;

        if (!profileDuplicatePanelsChecked)
        {
            DestroyDuplicateProfilePanels(canvasTransform);
            profileDuplicatePanelsChecked = true;
        }

        if (panelObject != null && panelObject.scene.IsValid())
        {
            if (panelObject.transform.parent != canvasTransform)
                panelObject.transform.SetParent(canvasTransform, false);

            return;
        }

        CreatePanel(canvasTransform);
        RefreshView();
    }

    Transform GetCanvasTransform()
    {
        if (cachedCanvasTransform != null && cachedCanvasTransform.gameObject.scene.IsValid())
            return cachedCanvasTransform;

        GameObject canvasObject = GameObject.Find("Canvas");
        cachedCanvasTransform = canvasObject != null ? canvasObject.transform : null;
        profileDuplicatePanelsChecked = false;
        return cachedCanvasTransform;
    }

    void DestroyDuplicateProfilePanels(Transform canvasTransform)
    {
        if (canvasTransform == null)
            return;

        List<GameObject> duplicates = new List<GameObject>();
        for (int i = 0; i < canvasTransform.childCount; i++)
        {
            Transform child = canvasTransform.GetChild(i);
            if (child == null || child.name != "ProfilePanel")
                continue;

            if (panelObject != null && child.gameObject == panelObject)
                continue;

            duplicates.Add(child.gameObject);
        }

        for (int i = 0; i < duplicates.Count; i++)
            Destroy(duplicates[i]);
    }

    void CreatePanel(Transform parent)
    {
        panelObject = new GameObject("ProfilePanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image background = panelObject.GetComponent<Image>();
        Sprite profileBackgroundSprite = LoadStandaloneSprite("hangar1_2D.png");
        if (profileBackgroundSprite != null)
        {
            background.sprite = profileBackgroundSprite;
            background.color = Color.white;
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
        }
        else
        {
            background.color = new Color(0.05f, 0.08f, 0.12f, 1f);
            background.type = Image.Type.Sliced;
        }

        CreateSplashScreen(panelObject.transform);

        shipTypeLabelText = CreateText(panelObject.transform, "ShipTypeLabel", "SHIP", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-304f, -176f), new Vector2(260f, 24f), 18f, TextAlignmentOptions.Left);

        shipTypeButtons = new Button[SelectableShipTypes.Length];
        shipTypeButtons[0] = CreateButton(panelObject.transform, "ExplorerShipButton", "EXPLORER", new Vector2(300f, -204f), new Vector2(108f, 40f), () =>
        {
            SetSelectedShipType(ShipType.Explorer);
        });
        shipTypeButtons[1] = CreateButton(panelObject.transform, "ViperShipButton", "VIPER", new Vector2(412f, -204f), new Vector2(104f, 40f), () =>
        {
            SetSelectedShipType(ShipType.Viper);
        });
        shipTypeButtons[2] = CreateButton(panelObject.transform, "AvengerShipButton", "AVENGER", new Vector2(524f, -204f), new Vector2(104f, 40f), () =>
        {
            SetSelectedShipType(ShipType.Avenger);
        });
        shipTypeButtons[3] = CreateButton(panelObject.transform, "ArrowShipButton", "ARROW", new Vector2(636f, -204f), new Vector2(104f, 40f), () =>
        {
            SetSelectedShipType(ShipType.Arrow);
        });
        shipTypeButtons[4] = CreateButton(panelObject.transform, "InvaderShipButton", "INVADER", new Vector2(748f, -204f), new Vector2(104f, 40f), () =>
        {
            SetSelectedShipType(ShipType.Invader);
        });
        shipTypeButtons[5] = CreateButton(panelObject.transform, "CargoTruckShipButton", "BISON", new Vector2(860f, -204f), new Vector2(104f, 40f), () =>
        {
            SetSelectedShipType(ShipType.CargoTruck);
        });
        shipTypeButtons[6] = CreateButton(panelObject.transform, "PathfinderShipButton", "PATHFINDER", new Vector2(982f, -204f), new Vector2(132f, 40f), () =>
        {
            SetSelectedShipType(ShipType.Pathfinder);
        });

        shipSkinLabelText = CreateText(panelObject.transform, "SkinLabel", "SHIP SKIN", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-304f, -256f), new Vector2(300f, 24f), 18f, TextAlignmentOptions.Left);

        skinButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int capturedIndex = i;
            skinButtons[i] = CreateButton(panelObject.transform, "ShipSkinButton" + i, "SKIN", new Vector2(346f + (146f * i), -284f), new Vector2(126f, 56f), () =>
            {
                ApplySkinChoiceByButtonIndex(capturedIndex);
            });
        }

        shipPreviewTitleText = CreateText(panelObject.transform, "ShipPreviewTitle", "SHIP", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-356f, -388f), new Vector2(520f, 38f), 28f, TextAlignmentOptions.Center);
        CreateShipPreview(panelObject.transform);
        CreateShipImageModal(panelObject.transform);
        CreatePilotPortrait(panelObject.transform);
        CreateProjectsHomeButton(panelObject.transform);

        inventoryHintText = CreateText(panelObject.transform, "InventoryHintText", "Tap to preview. Drag between inventories, loadout slots and crafting.", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(820f, 24f), 16f, TextAlignmentOptions.Center);
        inventoryHintText.fontStyle = FontStyles.Normal;

        shipInventoryLabelText = CreateText(panelObject.transform, "ShipInventoryLabel", "SHIP INVENTORY 0/0", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-646f, -222f + InventoryUtilityButtonLift), new Vector2(460f, ShipInventoryHeaderButtonSize.y), ShipInventoryHeaderFontSize, TextAlignmentOptions.MidlineLeft);
        ConfigureShipInventoryLabelText();
        shipInventoryUnloadButton = CreateButton(panelObject.transform, "ShipInventoryUnloadButton", "UNLOAD", new Vector2(-312f, -222f + InventoryUtilityButtonLift), ShipInventoryHeaderButtonSize, OnShipInventoryUnloadClicked);
        ConfigureNoBlinkInventoryActionButton(shipInventoryUnloadButton);
        StyleReadableBackLikeButton(shipInventoryUnloadButton, ShipInventoryHeaderButtonFontSize);
        LayoutShipInventoryHeader(-878f, (120f * 5f) + (12f * 4f), -222f + InventoryUtilityButtonLift);
        CreateInventoryGrid(panelObject.transform, false, new Vector2(-878f, -254f), PlayerInventoryData.ShipSlotCount, 5, out shipInventoryButtons, out shipInventoryTexts, out shipInventoryIcons);

        float initialPlayerInventoryExtendButtonX = -278f + InventoryExtendButtonOffsetX;
        float initialPlayerInventorySortButtonX = initialPlayerInventoryExtendButtonX - ((PlayerInventoryExtendButtonSize.x + PlayerInventorySortButtonSize.x) * 0.5f) - InventoryUtilityButtonGap;

        playerInventoryLabelText = CreateText(panelObject.transform, "PlayerInventoryLabel", PlayerInventoryTitleText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-574f, -546f), new Vector2(150f, 56f), 18f, TextAlignmentOptions.MidlineLeft);
        ConfigurePlayerInventoryLabelText();
        playerInventoryCountText = CreateText(panelObject.transform, "PlayerInventoryCount", "0/" + GetPlayerInventorySlotCount(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-444f, -546f), new Vector2(96f, 56f), 20f, TextAlignmentOptions.MidlineRight);
        ConfigurePlayerInventoryCountText();
        playerInventoryFilterButton = CreateButton(panelObject.transform, "PlayerInventoryFilterButton", "ALL", new Vector2(-902f, -542f + InventoryUtilityButtonLift), PlayerInventoryFilterButtonSize, OnPlayerInventoryFilterClicked);
        ConfigureNoBlinkInventoryActionButton(playerInventoryFilterButton);
        StylePlayerInventoryUtilityButton(playerInventoryFilterButton);
        playerInventorySortButton = CreateButton(panelObject.transform, "PlayerInventorySortButton", "SORT: A-Z", new Vector2(initialPlayerInventorySortButtonX, -542f + InventoryUtilityButtonLift), PlayerInventorySortButtonSize, OnPlayerInventorySortClicked);
        ConfigureNoBlinkInventoryActionButton(playerInventorySortButton);
        StylePlayerInventoryUtilityButton(playerInventorySortButton);
        playerInventoryExtendButton = CreateButton(panelObject.transform, "PlayerInventoryExtendButton", "EXTEND", new Vector2(initialPlayerInventoryExtendButtonX, -542f + InventoryUtilityButtonLift), PlayerInventoryExtendButtonSize, OnPlayerInventoryExtendClicked);
        ConfigureNoBlinkInventoryActionButton(playerInventoryExtendButton);
        StylePlayerInventoryUtilityButton(playerInventoryExtendButton);
        RebuildPlayerInventoryGrid(GetPlayerInventorySlotCount());

        CreateItemPreview(panelObject.transform);
        CreateItemInfoOverlay(panelObject.transform);
        CreatePlayerInventoryExtendConfirm(panelObject.transform);
        CreateShipInventoryStartConfirm(panelObject.transform);
        CreateCraftingPanel(panelObject.transform);
        CreateCraftingRecipeBrowser(panelObject.transform);
        CreateCraftingBlueprintBrowser(panelObject.transform);
        CreateShopBrowser(panelObject.transform);

        exitGameButton = CreateButton(panelObject.transform, "ExitGameButton", "EXIT GAME", new Vector2(820f, -72f), new Vector2(210f, 54f), OnExitGameClicked);
        StyleExitGameButton();
        shopButton = CreateButton(panelObject.transform, "ShopButton", "SHOP", new Vector2(224f, -668f), new Vector2(108f, 108f), OnShopButtonClicked);
        StyleButton(shopButton, new Color(0.16f, 0.38f, 0.48f, 0.98f), new Color(0.22f, 0.5f, 0.62f, 1f));
        TMP_Text shopText = shopButton.GetComponentInChildren<TMP_Text>(true);
        if (shopText != null)
        {
            shopText.fontSize = 30f;
            shopText.characterSpacing = 5f;
        }
        saveAndRunButton = CreateButton(panelObject.transform, "SaveAndRunButton", "PLAY", new Vector2(224f, -800f), new Vector2(108f, 108f), OnSaveAndRunClicked);
        ApplySaveAndRunButtonStyle();
        statusText = CreateText(panelObject.transform, "ProfileStatusText", string.Empty, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(320f, 24f), 16f, TextAlignmentOptions.Center);

        CreateProfileScreenScaffolding();
        RebuildProfileScreenHierarchy();
        CreateShipSelectionView(panelObject.transform);
        CreatePilotSelectionView(panelObject.transform);
        SwitchToScreen(ProfileScreen.Home, false);
        UIRuntimeStyler.RefreshStyles();
    }

    GameObject CreateSectionRoot(string name, Transform parent)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return root;
    }

    void CreateProfileScreenScaffolding()
    {
        topBarRootObject = CreateSectionRoot("ProfileTopBarRoot", panelObject.transform);
        ConfigureSharedTopBar();
        leftNavigationRootObject = CreateSectionRoot("ProfileLeftNavigationRoot", panelObject.transform);
        rightActionRootObject = CreateSectionRoot("ProfileRightActionRoot", panelObject.transform);
        homeViewRootObject = CreateSectionRoot("ProfileHomeViewRoot", panelObject.transform);
        storageViewRootObject = CreateSectionRoot("ProfileStorageViewRoot", panelObject.transform);
        shipWorkspaceRootObject = CreateSectionRoot("ProfileShipWorkspaceRoot", panelObject.transform);
        inventoryViewRootObject = CreateSectionRoot("ProfileInventoryViewRoot", panelObject.transform);
        craftingViewRootObject = CreateSectionRoot("ProfileCraftingViewRoot", panelObject.transform);
        traderViewRootObject = CreateSectionRoot("ProfileTraderViewRoot", panelObject.transform);
        playerViewRootObject = CreateSectionRoot("ProfilePlayerViewRoot", panelObject.transform);
        projectsViewRootObject = CreateSectionRoot("ProfileProjectsViewRoot", panelObject.transform);
        projectDetailsViewObject = CreateSectionRoot("ProfileProjectDetailsViewRoot", panelObject.transform);

        navBackButton = CreateButton(leftNavigationRootObject.transform, "ProfileBackButton", "BACK", new Vector2(-814f, -206f), new Vector2(168f, 48f), () =>
        {
            OnProfileBackClicked();
        });
        StyleButton(navBackButton, new Color(0.14f, 0.19f, 0.28f, 0.98f), new Color(0.22f, 0.3f, 0.42f, 1f));

        navCraftingButton = CreateButton(leftNavigationRootObject.transform, "ProfileCraftingNavButton", "CRAFTING", new Vector2(-804f, -338f), new Vector2(234f, 64f), () =>
        {
            SwitchToScreen(ProfileScreen.Crafting);
        });
        StyleButton(navCraftingButton, new Color(0.14f, 0.48f, 0.28f, 0.98f), new Color(0.19f, 0.62f, 0.36f, 1f));

        navInventoryButton = CreateButton(leftNavigationRootObject.transform, "ProfileInventoryNavButton", "INVENTORY", new Vector2(-804f, -498f), new Vector2(234f, 64f), () =>
        {
            SwitchToScreen(ProfileScreen.Inventory);
        });
        StyleButton(navInventoryButton, new Color(0.16f, 0.3f, 0.46f, 0.98f), new Color(0.22f, 0.42f, 0.62f, 1f));

        navPlayerButton = CreateButton(leftNavigationRootObject.transform, "ProfilePlayerNavButton", "PLAYER", new Vector2(-804f, -578f), new Vector2(234f, 64f), () =>
        {
            SwitchToScreen(ProfileScreen.Player);
        });
        StyleButton(navPlayerButton, new Color(0.2f, 0.28f, 0.38f, 0.98f), new Color(0.3f, 0.42f, 0.56f, 1f));

        if (shopButton != null)
        {
            shopButton.transform.SetParent(leftNavigationRootObject.transform, false);
            shopButton.onClick.RemoveAllListeners();
            shopButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                SwitchToScreen(ProfileScreen.Trader);
            });
            RectTransform rect = shopButton.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(-804f, -418f);
                rect.sizeDelta = new Vector2(234f, 64f);
            }

            TMP_Text text = shopButton.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = "TRADER";
                text.fontSize = 24f;
                text.characterSpacing = 3f;
            }

            StyleButton(shopButton, new Color(0.18f, 0.34f, 0.5f, 0.98f), new Color(0.24f, 0.46f, 0.66f, 1f));
        }

        if (exitGameButton != null)
        {
            exitGameButton.transform.SetParent(rightActionRootObject.transform, false);
            StyleExitGameButton();
        }
        if (saveAndRunButton != null)
            saveAndRunButton.transform.SetParent(rightActionRootObject.transform, false);

        CreateTraderFuturePanel(traderViewRootObject.transform);
        CreatePlayerView(playerViewRootObject.transform);
        CreateProjectsView(projectsViewRootObject.transform);
        CreateProjectDetailsView(projectDetailsViewObject.transform);
    }

    void ConfigureSharedTopBar()
    {
        if (topBarRootObject == null)
            return;

        sharedTopBar = SharedPlayerTopBarUI.Ensure(topBarRootObject, true);
        if (sharedTopBar == null)
            return;

        nicknameInput = sharedTopBar.NicknameInput;
        accountText = sharedTopBar.AccountText;
        gamesPlayedText = sharedTopBar.GamesText;
        totalXpText = sharedTopBar.LevelXpText;
        astronsText = sharedTopBar.AstronsText;
    }

    void RebuildProfileScreenHierarchy()
    {
        if (accountText != null)
            accountText.transform.SetParent(topBarRootObject.transform, false);
        if (gamesPlayedText != null)
            gamesPlayedText.transform.SetParent(topBarRootObject.transform, false);
        if (totalXpText != null)
            totalXpText.transform.SetParent(topBarRootObject.transform, false);
        if (astronsText != null)
            astronsText.transform.SetParent(topBarRootObject.transform, false);
        if (nicknameInput != null)
            nicknameInput.transform.SetParent(topBarRootObject.transform, false);

        if (shipPreviewTitleText != null)
            shipPreviewTitleText.transform.SetParent(shipWorkspaceRootObject.transform, false);
        if (shipPreviewRootRect != null)
            shipPreviewRootRect.transform.SetParent(shipWorkspaceRootObject.transform, false);
        if (shipStatsPanelObject != null)
            shipStatsPanelObject.transform.SetParent(shipWorkspaceRootObject.transform, false);
        if (pilotPortraitRootObject != null)
            pilotPortraitRootObject.transform.SetParent(homeViewRootObject.transform, false);
        if (projectsButtonRootObject != null)
            projectsButtonRootObject.transform.SetParent(homeViewRootObject.transform, false);

        if (shipInventoryLabelText != null)
            shipInventoryLabelText.transform.SetParent(storageViewRootObject.transform, false);
        if (shipInventoryUnloadButton != null)
            shipInventoryUnloadButton.transform.SetParent(storageViewRootObject.transform, false);
        if (playerInventoryLabelText != null)
            playerInventoryLabelText.transform.SetParent(storageViewRootObject.transform, false);
        if (playerInventoryCountText != null)
            playerInventoryCountText.transform.SetParent(storageViewRootObject.transform, false);
        if (playerInventoryFilterButton != null)
            playerInventoryFilterButton.transform.SetParent(storageViewRootObject.transform, false);
        if (playerInventorySortButton != null)
            playerInventorySortButton.transform.SetParent(storageViewRootObject.transform, false);
        if (playerInventoryExtendButton != null)
            playerInventoryExtendButton.transform.SetParent(storageViewRootObject.transform, false);
        if (playerInventoryScrollRect != null)
            playerInventoryScrollRect.transform.SetParent(storageViewRootObject.transform, false);
        if (playerInventoryScrollbarObject != null)
            playerInventoryScrollbarObject.transform.SetParent(storageViewRootObject.transform, false);

        if (shipInventoryButtons != null)
        {
            for (int i = 0; i < shipInventoryButtons.Length; i++)
            {
                if (shipInventoryButtons[i] != null)
                    shipInventoryButtons[i].transform.SetParent(storageViewRootObject.transform, false);
            }
        }

        if (craftingPanelObject != null)
            craftingPanelObject.transform.SetParent(craftingViewRootObject.transform, false);

        if (craftingRecipeBrowserObject != null)
            craftingRecipeBrowserObject.transform.SetParent(craftingViewRootObject.transform, false);

        if (shopBrowserObject != null)
            shopBrowserObject.transform.SetParent(traderViewRootObject.transform, false);

        if (itemPreviewPanelObject != null)
            itemPreviewPanelObject.transform.SetParent(panelObject.transform, false);
    }
}
