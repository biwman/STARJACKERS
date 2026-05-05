using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerProfilePanelUI : MonoBehaviour
{
    enum ProfileItemSource
    {
        None,
        PlayerInventory,
        ShipInventory,
        EquipmentSlot,
        CraftingSlot,
        ShopListing
    }

    enum ProfileScreen
    {
        Home,
        Crafting,
        Trader,
        Inventory,
        ShipSelection
    }

    static readonly string[] GameplayHudObjectNames =
    {
        "JoystickBG",
        "ShootJoystickBG",
        "CollectButton",
        "ShipInventoryButton",
        "ShipInventoryPanel",
        "ReloadButton",
        "ComplexAmmoBar",
        "SuperAttackJoystickBG",
        "TimerText",
        "HP_Bar",
        "Shield_Bar",
        "Booster_Bar",
        "ScoreText"
    };

    static readonly ShipType[] SelectableShipTypes =
    {
        ShipType.Explorer,
        ShipType.Viper,
        ShipType.Avenger,
        ShipType.Arrow,
        ShipType.Invader
    };

    static readonly Vector2 ShipPreviewImagePosition = new Vector2(0f, 22f);
    static readonly Vector2 PlayerInventoryGridPosition = new Vector2(-938f, -578f);
    static readonly Vector2 PlayerInventoryViewportSize = new Vector2(830f, 362f);
    const int PlayerInventoryGridColumns = 6;
    static readonly Vector2[] EquipmentSlotLayoutPositions =
    {
        new Vector2(-258f, -22f),
        new Vector2(258f, -22f),
        new Vector2(-258f, -138f),
        new Vector2(258f, -138f),
        new Vector2(-58f, -268f),
        new Vector2(58f, -268f),
        new Vector2(-258f, -254f),
        new Vector2(258f, -254f)
    };
    const float EquipmentSlotPreviewSize = 104f;
    static readonly string[] ShipStatLabels =
    {
        "HP",
        "SHIELD",
        "SPEED",
        "TURN",
        "BOOST",
        "MAX BOOST",
        "CARGO",
        "SAFE"
    };

    static PlayerProfilePanelUI instance;
    readonly Dictionary<string, GameObject> gameplayHudObjectsByName = new Dictionary<string, GameObject>();

    GameObject panelObject;
    TMP_InputField nicknameInput;
    TMP_Text accountText;
    TMP_Text statusText;
    TMP_Text gamesPlayedText;
    TMP_Text totalXpText;
    TMP_Text astronsText;
    TMP_Text inventoryHintText;
    Button saveAndRunButton;
    Button cheatButton;
    Button shopButton;
    Button exitGameButton;
    Button[] shipTypeButtons;
    Button[] skinButtons;
    Button[] shipInventoryButtons;
    Button[] playerInventoryButtons;
    Button shipInventoryUnloadButton;
    Button playerInventoryExtendButton;
    Button playerInventoryFilterButton;
    TMP_Text[] shipInventoryTexts;
    TMP_Text[] playerInventoryTexts;
    Image[] shipInventoryIcons;
    Image[] playerInventoryIcons;
    ScrollRect playerInventoryScrollRect;
    RectTransform playerInventoryContentRect;
    GameObject playerInventoryScrollbarObject;
    int builtPlayerInventorySlotCount = -1;
    bool playerInventoryEquipableOnly;
    int[] visiblePlayerInventorySlotMap = Array.Empty<int>();
    TMP_Text shipTypeLabelText;
    TMP_Text shipSkinLabelText;
    TMP_Text shipInventoryLabelText;
    TMP_Text playerInventoryLabelText;
    GameObject playerInventoryExtendConfirmObject;
    TMP_Text playerInventoryExtendConfirmText;
    Button playerInventoryExtendConfirmButton;
    Button playerInventoryExtendCancelButton;
    TMP_Text shipPreviewTitleText;
    GameObject shipStatsPanelObject;
    TMP_Text[] shipStatLabelTexts;
    TMP_Text[] shipStatValueTexts;
    Image[] shipStatFillImages;
    RectTransform shipPreviewRootRect;
    RectTransform[] equipmentSlotRects;
    Button[] equipmentSlotButtons;
    TMP_Text[] equipmentSlotPreviewTexts;
    Image[] equipmentSlotPreviewIcons;
    Image shipPreviewImage;
    Button shipPreviewButton;
    GameObject shipImageModalObject;
    Image shipImageModalImage;
    GameObject itemPreviewPanelObject;
    Image itemPreviewBackgroundImage;
    Image itemPreviewIcon;
    TMP_Text itemPreviewNameText;
    TMP_Text itemPreviewTypeText;
    TMP_Text itemPreviewPriceText;
    Button itemPreviewSellButton;
    Button itemPreviewSalvageButton;
    GameObject craftingPanelObject;
    Button[] craftingSlotButtons;
    TMP_Text[] craftingSlotTexts;
    Image[] craftingSlotIcons;
    Button craftingCatalogButton;
    Button craftButton;
    Button clearCraftButton;
    GameObject craftingRecipeBrowserObject;
    ScrollRect craftingRecipeScrollRect;
    RectTransform craftingRecipeContentRect;
    Button craftingRecipeCloseButton;
    readonly List<GameObject> craftingRecipeRowObjects = new List<GameObject>();
    GameObject shopBrowserObject;
    ScrollRect shopScrollRect;
    RectTransform shopContentRect;
    TMP_Text shopAstronsText;
    Button shopCloseButton;
    readonly List<GameObject> shopRowObjects = new List<GameObject>();
    GameObject cheatBrowserObject;
    TMP_Text cheatAstronsText;
    TMP_Text cheatStatusText;
    Button cheatAddMoneyButton;
    Button cheatCloseButton;
    GameObject splashScreenObject;
    Image splashScreenImage;
    float splashHideTime;
    static bool splashShownOnce;
    int selectedSkin;
    ProfileScreen currentScreen = ProfileScreen.Home;
    GameObject topBarRootObject;
    GameObject topStatBannerObject;
    GameObject leftNavigationRootObject;
    GameObject rightActionRootObject;
    GameObject homeViewRootObject;
    GameObject storageViewRootObject;
    GameObject shipWorkspaceRootObject;
    GameObject inventoryViewRootObject;
    GameObject craftingViewRootObject;
    GameObject traderViewRootObject;
    GameObject shipSelectionViewObject;
    GameObject traderFuturePanelObject;
    Button navCraftingButton;
    Button navTraderButton;
    Button navInventoryButton;
    Button navBackButton;
    TMP_Text profileTitleText;
    TMP_Text nicknameLabelText;
    TMP_Text shipSelectionTitleText;
    TMP_Text shipSelectionSubtitleText;
    TMP_Text shipSelectionStatusText;
    Button shipSelectionBackButton;
    Button shipSelectionPrevButton;
    Button shipSelectionNextButton;
    Button[] shipSelectionSkinButtons;
    TMP_Text shipSelectionSkinLabelText;
    GameObject[] shipSelectionCardObjects;
    Image[] shipSelectionCardImages;
    TMP_Text[] shipSelectionCardTitles;
    TMP_Text[][] shipSelectionCardStatLabelTexts;
    TMP_Text[][] shipSelectionCardStatValueTexts;
    Image[][] shipSelectionCardStatFillImages;
    GameObject[][] shipSelectionCardSlotObjects;
    int shipSelectionCenterIndex;
    ShipType shipSelectionCenterType = ShipType.Explorer;
    readonly Dictionary<ShipType, int> shipSelectionSkinByType = new Dictionary<ShipType, int>();
    bool inventoryActionInProgress;
    bool suppressNextInventoryClick;
    bool dragInProgress;
    GameObject dragVisualObject;
    Image dragVisualIcon;
    TMP_Text dragVisualLabel;
    ProfileItemSource previewSource = ProfileItemSource.None;
    int previewSlotIndex = -1;
    string previewItemId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("PlayerProfilePanelUI");
        instance = root.AddComponent<PlayerProfilePanelUI>();
        DontDestroyOnLoad(root);
    }

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
        EnsurePanel();
        RefreshView();
    }

    void OnProfileChanged(PlayerProfileData profile)
    {
        RefreshView();
        if (!NetworkManager.SessionRequested)
        {
            SetInteractable(true);
        }
        RefreshLobbyUi();
    }

    void Update()
    {
        EnsurePanel();
        RefreshVisibility();
        ApplyProfileScreenLayout();
        UpdateSkinButtonVisuals();
        ApplySaveAndRunButtonStyle();
        ApplyItemPreviewLayout();
        if (currentScreen == ProfileScreen.ShipSelection)
            RefreshShipSelectionView();
    }

    void EnsurePanel()
    {
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject == null)
            return;

        DestroyDuplicateProfilePanels(canvasObject.transform);

        if (panelObject != null && panelObject.scene.IsValid())
        {
            if (panelObject.transform.parent != canvasObject.transform)
                panelObject.transform.SetParent(canvasObject.transform, false);

            return;
        }

        CreatePanel(canvasObject.transform);
        RefreshView();
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

        profileTitleText = CreateText(panelObject.transform, "ProfileTitle", "PLAYER PROFILE", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -28f), new Vector2(360f, 40f), 34f, TextAlignmentOptions.Left);
        accountText = CreateText(panelObject.transform, "AccountText", "Connecting...", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-250f, -28f), new Vector2(320f, 24f), 16f, TextAlignmentOptions.Right);
        gamesPlayedText = CreateText(panelObject.transform, "GamesPlayedText", "Games: 0", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -92f), new Vector2(210f, 24f), 18f, TextAlignmentOptions.Left);
        totalXpText = CreateText(panelObject.transform, "TotalXpText", "Level: 1  XP: 0", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(420f, -92f), new Vector2(250f, 24f), 18f, TextAlignmentOptions.Left);
        astronsText = CreateText(panelObject.transform, "AstronsText", "Astrons: 0", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(665f, -92f), new Vector2(125f, 24f), 18f, TextAlignmentOptions.Left);

        nicknameLabelText = CreateText(panelObject.transform, "NicknameLabel", "NICKNAME", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1054f, -72f), new Vector2(260f, 24f), 18f, TextAlignmentOptions.Center);

        GameObject inputObject = new GameObject("NicknameInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObject.transform.SetParent(panelObject.transform, false);

        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 1f);
        inputRect.anchorMax = new Vector2(0f, 1f);
        inputRect.pivot = new Vector2(0f, 1f);
        inputRect.anchoredPosition = new Vector2(924f, -92f);
        inputRect.sizeDelta = new Vector2(260f, 42f);

        Image inputBackground = inputObject.GetComponent<Image>();
        inputBackground.color = new Color(0.15f, 0.2f, 0.27f, 0.98f);

        GameObject viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(inputObject.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(12f, 8f);
        viewportRect.offsetMax = new Vector2(-12f, -8f);

        TMP_Text placeholder = CreateText(viewport.transform, "Placeholder", "Nickname", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 20f, TextAlignmentOptions.Left);
        placeholder.color = new Color(0.74f, 0.79f, 0.86f, 0.5f);

        TMP_Text inputText = CreateText(viewport.transform, "Text", string.Empty, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 20f, TextAlignmentOptions.Left);
        inputText.color = new Color(0.96f, 0.98f, 1f, 1f);

        nicknameInput = inputObject.GetComponent<TMP_InputField>();
        nicknameInput.targetGraphic = inputBackground;
        nicknameInput.textViewport = viewportRect;
        nicknameInput.textComponent = inputText;
        nicknameInput.placeholder = placeholder;
        nicknameInput.lineType = TMP_InputField.LineType.SingleLine;
        nicknameInput.contentType = TMP_InputField.ContentType.Standard;
        nicknameInput.characterLimit = 18;

        CreateSplashScreen(panelObject.transform);

        shipTypeLabelText = CreateText(panelObject.transform, "ShipTypeLabel", "SHIP", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-304f, -176f), new Vector2(260f, 24f), 18f, TextAlignmentOptions.Left);

        shipTypeButtons = new Button[SelectableShipTypes.Length];
        shipTypeButtons[0] = CreateButton(panelObject.transform, "ExplorerShipButton", "EXPLORER", new Vector2(356f, -204f), new Vector2(136f, 40f), () =>
        {
            SetSelectedShipType(ShipType.Explorer);
        });
        shipTypeButtons[1] = CreateButton(panelObject.transform, "ViperShipButton", "VIPER", new Vector2(490f, -204f), new Vector2(126f, 40f), () =>
        {
            SetSelectedShipType(ShipType.Viper);
        });
        shipTypeButtons[2] = CreateButton(panelObject.transform, "AvengerShipButton", "AVENGER", new Vector2(630f, -204f), new Vector2(126f, 40f), () =>
        {
            SetSelectedShipType(ShipType.Avenger);
        });
        shipTypeButtons[3] = CreateButton(panelObject.transform, "ArrowShipButton", "ARROW", new Vector2(770f, -204f), new Vector2(126f, 40f), () =>
        {
            SetSelectedShipType(ShipType.Arrow);
        });
        shipTypeButtons[4] = CreateButton(panelObject.transform, "InvaderShipButton", "INVADER", new Vector2(910f, -204f), new Vector2(126f, 40f), () =>
        {
            SetSelectedShipType(ShipType.Invader);
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

        inventoryHintText = CreateText(panelObject.transform, "InventoryHintText", "Tap to preview. Drag between inventories, loadout slots and crafting.", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(820f, 24f), 16f, TextAlignmentOptions.Center);
        inventoryHintText.fontStyle = FontStyles.Normal;

        shipInventoryLabelText = CreateText(panelObject.transform, "ShipInventoryLabel", "SHIP INVENTORY", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-614f, -222f), new Vector2(420f, 24f), 18f, TextAlignmentOptions.Center);
        shipInventoryUnloadButton = CreateButton(panelObject.transform, "ShipInventoryUnloadButton", "UNLOAD", new Vector2(-278f, -222f), new Vector2(128f, 36f), OnShipInventoryUnloadClicked);
        StyleButton(shipInventoryUnloadButton, new Color(0.12f, 0.38f, 0.44f, 0.98f), new Color(0.16f, 0.52f, 0.62f, 1f));
        CreateInventoryGrid(panelObject.transform, false, new Vector2(-878f, -254f), 10, 5, out shipInventoryButtons, out shipInventoryTexts, out shipInventoryIcons);

        playerInventoryLabelText = CreateText(panelObject.transform, "PlayerInventoryLabel", "PLAYER INVENTORY (" + GetPlayerInventorySlotCount() + ")", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-574f, -546f), new Vector2(430f, 24f), 18f, TextAlignmentOptions.Center);
        playerInventoryFilterButton = CreateButton(panelObject.transform, "PlayerInventoryFilterButton", "ALL", new Vector2(-902f, -542f), new Vector2(166f, 36f), OnPlayerInventoryFilterClicked);
        StyleButton(playerInventoryFilterButton, new Color(0.16f, 0.2f, 0.27f, 0.95f), new Color(0.19f, 0.61f, 0.5f, 0.98f));
        playerInventoryExtendButton = CreateButton(panelObject.transform, "PlayerInventoryExtendButton", "EXTEND", new Vector2(-278f, -542f), new Vector2(128f, 36f), OnPlayerInventoryExtendClicked);
        StyleButton(playerInventoryExtendButton, new Color(0.12f, 0.38f, 0.44f, 0.98f), new Color(0.16f, 0.52f, 0.62f, 1f));
        RebuildPlayerInventoryGrid(GetPlayerInventorySlotCount());

        CreateItemPreview(panelObject.transform);
        CreatePlayerInventoryExtendConfirm(panelObject.transform);
        CreateCraftingPanel(panelObject.transform);
        CreateCraftingRecipeBrowser(panelObject.transform);
        CreateShopBrowser(panelObject.transform);
        CreateCheatBrowser(panelObject.transform);

        exitGameButton = CreateButton(panelObject.transform, "ExitGameButton", "EXIT GAME", new Vector2(820f, -72f), new Vector2(210f, 54f), OnExitGameClicked);
        cheatButton = CreateButton(panelObject.transform, "CheatButton", "CHEAT", new Vector2(224f, -536f), new Vector2(108f, 108f), OnCheatButtonClicked);
        StyleButton(cheatButton, new Color(0.52f, 0.2f, 0.18f, 0.98f), new Color(0.68f, 0.28f, 0.23f, 1f));
        TMP_Text cheatText = cheatButton.GetComponentInChildren<TMP_Text>(true);
        if (cheatText != null)
        {
            cheatText.fontSize = 26f;
            cheatText.characterSpacing = 3f;
        }
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
        SwitchToScreen(ProfileScreen.Home, false);
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
        leftNavigationRootObject = CreateSectionRoot("ProfileLeftNavigationRoot", panelObject.transform);
        rightActionRootObject = CreateSectionRoot("ProfileRightActionRoot", panelObject.transform);
        homeViewRootObject = CreateSectionRoot("ProfileHomeViewRoot", panelObject.transform);
        storageViewRootObject = CreateSectionRoot("ProfileStorageViewRoot", panelObject.transform);
        shipWorkspaceRootObject = CreateSectionRoot("ProfileShipWorkspaceRoot", panelObject.transform);
        inventoryViewRootObject = CreateSectionRoot("ProfileInventoryViewRoot", panelObject.transform);
        craftingViewRootObject = CreateSectionRoot("ProfileCraftingViewRoot", panelObject.transform);
        traderViewRootObject = CreateSectionRoot("ProfileTraderViewRoot", panelObject.transform);

        navBackButton = CreateButton(leftNavigationRootObject.transform, "ProfileBackButton", "BACK", new Vector2(-814f, -206f), new Vector2(168f, 48f), () =>
        {
            SwitchToScreen(ProfileScreen.Home);
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
            exitGameButton.transform.SetParent(rightActionRootObject.transform, false);
        if (cheatButton != null)
            cheatButton.transform.SetParent(rightActionRootObject.transform, false);
        if (saveAndRunButton != null)
            saveAndRunButton.transform.SetParent(rightActionRootObject.transform, false);

        CreateTraderFuturePanel(traderViewRootObject.transform);
    }

    void RebuildProfileScreenHierarchy()
    {
        if (profileTitleText != null)
            profileTitleText.transform.SetParent(topBarRootObject.transform, false);
        if (accountText != null)
            accountText.transform.SetParent(topBarRootObject.transform, false);
        if (gamesPlayedText != null)
            gamesPlayedText.transform.SetParent(topBarRootObject.transform, false);
        if (totalXpText != null)
            totalXpText.transform.SetParent(topBarRootObject.transform, false);
        if (astronsText != null)
            astronsText.transform.SetParent(topBarRootObject.transform, false);
        if (nicknameLabelText != null)
            nicknameLabelText.transform.SetParent(topBarRootObject.transform, false);
        if (nicknameInput != null)
            nicknameInput.transform.SetParent(topBarRootObject.transform, false);

        if (shipPreviewTitleText != null)
            shipPreviewTitleText.transform.SetParent(shipWorkspaceRootObject.transform, false);
        if (shipPreviewRootRect != null)
            shipPreviewRootRect.transform.SetParent(shipWorkspaceRootObject.transform, false);
        if (shipStatsPanelObject != null)
            shipStatsPanelObject.transform.SetParent(shipWorkspaceRootObject.transform, false);

        if (shipInventoryLabelText != null)
            shipInventoryLabelText.transform.SetParent(storageViewRootObject.transform, false);
        if (shipInventoryUnloadButton != null)
            shipInventoryUnloadButton.transform.SetParent(storageViewRootObject.transform, false);
        if (playerInventoryLabelText != null)
            playerInventoryLabelText.transform.SetParent(storageViewRootObject.transform, false);
        if (playerInventoryFilterButton != null)
            playerInventoryFilterButton.transform.SetParent(storageViewRootObject.transform, false);
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

    void CreateTraderFuturePanel(Transform parent)
    {
        traderFuturePanelObject = new GameObject("TraderFuturePanel", typeof(RectTransform), typeof(Image));
        traderFuturePanelObject.transform.SetParent(parent, false);
        RectTransform rect = traderFuturePanelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(632f, -262f);
        rect.sizeDelta = new Vector2(296f, 620f);

        Image image = traderFuturePanelObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;

        TMP_Text futureTitle = CreateText(traderFuturePanelObject.transform, "TraderFutureTitle", "SHOPS", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(240f, 30f), 24f, TextAlignmentOptions.Center);
        futureTitle.gameObject.SetActive(false);
        TMP_Text hint = CreateText(traderFuturePanelObject.transform, "TraderFutureHint", "Place for future store switches and extra trader functions.", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(236f, 180f), 18f, TextAlignmentOptions.Center);
        hint.fontStyle = FontStyles.Normal;
        hint.textWrappingMode = TextWrappingModes.Normal;
        hint.color = new Color(0.8f, 0.86f, 0.94f, 0.92f);
        hint.gameObject.SetActive(false);
    }

    void CreateShipSelectionView(Transform parent)
    {
        shipSelectionViewObject = new GameObject("ShipSelectionView", typeof(RectTransform), typeof(Image));
        shipSelectionViewObject.transform.SetParent(parent, false);

        RectTransform rootRect = shipSelectionViewObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image overlay = shipSelectionViewObject.GetComponent<Image>();
        overlay.color = new Color(0.03f, 0.05f, 0.08f, 0.96f);

        ProfileShipSelectionSwipeHandler swipeHandler = shipSelectionViewObject.AddComponent<ProfileShipSelectionSwipeHandler>();
        swipeHandler.owner = this;

        shipSelectionBackButton = CreateButton(shipSelectionViewObject.transform, "ShipSelectionBackButton", "BACK", new Vector2(-806f, -46f), new Vector2(214f, 62f), () =>
        {
            SwitchToScreen(ProfileScreen.Home);
        });
        StyleButton(shipSelectionBackButton, new Color(0.14f, 0.19f, 0.28f, 0.98f), new Color(0.22f, 0.3f, 0.42f, 1f));

        shipSelectionTitleText = CreateText(shipSelectionViewObject.transform, "ShipSelectionTitle", "CHOOSE SHIP", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(640f, 48f), 38f, TextAlignmentOptions.Center);
        shipSelectionTitleText.raycastTarget = false;
        shipSelectionSubtitleText = CreateText(shipSelectionViewObject.transform, "ShipSelectionSubtitle", "Swipe with arrows, pick a skin, then tap the centered ship.", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(760f, 28f), 17f, TextAlignmentOptions.Center);
        shipSelectionSubtitleText.fontStyle = FontStyles.Normal;
        shipSelectionSubtitleText.color = new Color(0.8f, 0.87f, 0.95f, 0.92f);
        shipSelectionSkinLabelText = CreateText(shipSelectionViewObject.transform, "ShipSelectionSkinLabel", "SKINS", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(420f, 24f), 20f, TextAlignmentOptions.Center);

        shipSelectionSkinButtons = new Button[3];
        for (int i = 0; i < shipSelectionSkinButtons.Length; i++)
        {
            int capturedIndex = i;
            shipSelectionSkinButtons[i] = CreateButton(shipSelectionViewObject.transform, "ShipSelectionSkinButton" + i, "SKIN", new Vector2(-248f + (248f * i), -56f), new Vector2(220f, 48f), () =>
            {
                SetShipSelectionSkinByButton(capturedIndex);
            });
            StyleButton(shipSelectionSkinButtons[i], new Color(0.16f, 0.2f, 0.27f, 0.95f), new Color(0.19f, 0.61f, 0.5f, 0.98f));
        }

        shipSelectionPrevButton = CreateButton(shipSelectionViewObject.transform, "ShipSelectionPrevButton", "<", new Vector2(-666f, -438f), new Vector2(92f, 92f), MoveShipSelectionLeft);
        shipSelectionNextButton = CreateButton(shipSelectionViewObject.transform, "ShipSelectionNextButton", ">", new Vector2(666f, -438f), new Vector2(92f, 92f), MoveShipSelectionRight);
        StyleButton(shipSelectionPrevButton, new Color(0.16f, 0.22f, 0.3f, 0.95f), new Color(0.24f, 0.34f, 0.46f, 1f));
        StyleButton(shipSelectionNextButton, new Color(0.16f, 0.22f, 0.3f, 0.95f), new Color(0.24f, 0.34f, 0.46f, 1f));

        shipSelectionCardObjects = new GameObject[3];
        shipSelectionCardImages = new Image[3];
        shipSelectionCardTitles = new TMP_Text[3];
        shipSelectionCardStatLabelTexts = new TMP_Text[3][];
        shipSelectionCardStatValueTexts = new TMP_Text[3][];
        shipSelectionCardStatFillImages = new Image[3][];
        shipSelectionCardSlotObjects = new GameObject[3][];
        Vector2[] positions =
        {
            new Vector2(-560f, -108f),
            new Vector2(0f, -86f),
            new Vector2(560f, -108f)
        };
        Vector2[] sizes =
        {
            new Vector2(500f, 860f),
            new Vector2(700f, 960f),
            new Vector2(500f, 860f)
        };

        for (int i = 0; i < shipSelectionCardObjects.Length; i++)
        {
            bool centerCard = i == 1;
            shipSelectionCardObjects[i] = CreateShipSelectionCard(
                shipSelectionViewObject.transform,
                i,
                positions[i],
                sizes[i],
                centerCard,
                out shipSelectionCardImages[i],
                out shipSelectionCardTitles[i],
                out shipSelectionCardStatLabelTexts[i],
                out shipSelectionCardStatValueTexts[i],
                out shipSelectionCardStatFillImages[i],
                out shipSelectionCardSlotObjects[i]);
        }

        shipSelectionStatusText = CreateText(shipSelectionViewObject.transform, "ShipSelectionStatus", string.Empty, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(720f, 28f), 18f, TextAlignmentOptions.Center);
        shipSelectionStatusText.fontStyle = FontStyles.Normal;
        shipSelectionStatusText.color = new Color(0.86f, 0.92f, 0.98f, 0.98f);

        if (shipSelectionSubtitleText != null)
            shipSelectionSubtitleText.gameObject.SetActive(false);
        if (shipSelectionSkinLabelText != null)
            shipSelectionSkinLabelText.gameObject.SetActive(false);
        if (shipSelectionPrevButton != null)
            shipSelectionPrevButton.gameObject.SetActive(false);
        if (shipSelectionNextButton != null)
            shipSelectionNextButton.gameObject.SetActive(false);

        shipSelectionViewObject.SetActive(false);
    }

    GameObject CreateShipSelectionCard(Transform parent, int cardIndex, Vector2 anchoredPosition, Vector2 size, bool centerCard, out Image previewImage, out TMP_Text titleText, out TMP_Text[] statLabelTexts, out TMP_Text[] statValueTexts, out Image[] statFillImages, out GameObject[] slotObjects)
    {
        GameObject card = new GameObject("ShipSelectionCard" + cardIndex, typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(parent, false);

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image cardImage = card.GetComponent<Image>();
        cardImage.color = centerCard
            ? new Color(0.11f, 0.16f, 0.22f, 0.96f)
            : new Color(0.09f, 0.13f, 0.18f, 0.9f);

        Button button = card.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        int capturedCardIndex = cardIndex;
        button.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            OnShipSelectionCardClicked(capturedCardIndex);
        });

        ProfileShipSelectionSwipeHandler swipeHandler = card.AddComponent<ProfileShipSelectionSwipeHandler>();
        swipeHandler.owner = this;

        titleText = CreateText(card.transform, "Title", "SHIP", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(size.x - 40f, 40f), centerCard ? 34f : 26f, TextAlignmentOptions.Center);
        titleText.raycastTarget = false;

        GameObject imageObject = new GameObject("Preview", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(card.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = centerCard ? new Vector2(18f, 36f) : new Vector2(10f, 26f);
        imageRect.sizeDelta = centerCard ? new Vector2(680f, 820f) : new Vector2(470f, 560f);
        previewImage = imageObject.GetComponent<Image>();
        previewImage.preserveAspect = true;
        previewImage.raycastTarget = false;

        slotObjects = new GameObject[PlayerInventoryData.EquipmentSlotCount];
        Vector2[] slotLayout = BuildShipSelectionSlotLayout(centerCard);
        for (int i = 0; i < slotObjects.Length; i++)
        {
            GameObject slot = new GameObject("Slot" + i, typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(card.transform, false);
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = slotLayout[i];
            slotRect.sizeDelta = centerCard ? new Vector2(82f, 82f) : new Vector2(64f, 64f);
            Image slotImage = slot.GetComponent<Image>();
            slotImage.color = new Color(0.18f, 0.24f, 0.31f, 0.92f);
            slotImage.raycastTarget = false;
            TMP_Text slotText = CreateText(slot.transform, "SlotText", GetShipSelectionSlotLabel(i), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, centerCard ? 9f : 8f, TextAlignmentOptions.Center);
            slotText.textWrappingMode = TextWrappingModes.Normal;
            slotText.margin = new Vector4(4f, 4f, 4f, 4f);
            slotText.enableAutoSizing = false;
            slotText.fontSize = centerCard ? 9f : 8f;
            slotText.raycastTarget = false;
            RectTransform slotTextRect = slotText.rectTransform;
            slotTextRect.anchorMin = Vector2.zero;
            slotTextRect.anchorMax = Vector2.one;
            slotTextRect.pivot = new Vector2(0.5f, 0.5f);
            slotTextRect.offsetMin = new Vector2(4f, 4f);
            slotTextRect.offsetMax = new Vector2(-4f, -4f);
            slotTextRect.anchoredPosition = Vector2.zero;
            slotObjects[i] = slot;
        }

        CreateShipSelectionStatCards(card.transform, out statLabelTexts, out statValueTexts, out statFillImages);

        return card;
    }

    Vector2[] BuildShipSelectionSlotLayout(bool centerCard)
    {
        float leftColumnX = centerCard ? -324f : -244f;
        float rightColumnX = centerCard ? -216f : -164f;
        float topY = centerCard ? 236f : 190f;
        float rowSpacing = centerCard ? 126f : 98f;

        Vector2[] result = new Vector2[PlayerInventoryData.EquipmentSlotCount];
        int[][] rowOrder =
        {
            new[] { 0, 1 },
            new[] { 2, 3 },
            new[] { 6, 7 },
            new[] { 4, 5 }
        };

        for (int row = 0; row < rowOrder.Length; row++)
        {
            for (int col = 0; col < rowOrder[row].Length; col++)
            {
                int slotIndex = rowOrder[row][col];
                result[slotIndex] = new Vector2(col == 0 ? leftColumnX : rightColumnX, topY - (row * rowSpacing));
            }
        }

        return result;
    }

    string GetShipSelectionSlotLabel(int slotIndex)
    {
        return slotIndex switch
        {
            0 or 1 => "GUN",
            2 or 3 => "SHLD",
            4 or 5 => "ENG",
            6 or 7 => "GAD",
            _ => "SLOT"
        };
    }

    void CreateShipSelectionStatCards(Transform parent, out TMP_Text[] labelTexts, out TMP_Text[] valueTexts, out Image[] fillImages)
    {
        int count = ShipStatLabels.Length;
        labelTexts = new TMP_Text[count];
        valueTexts = new TMP_Text[count];
        fillImages = new Image[count];

        for (int i = 0; i < count; i++)
        {
            GameObject cardObject = new GameObject("ShipSelectionStatCard_" + ShipStatLabels[i], typeof(RectTransform), typeof(Image));
            cardObject.transform.SetParent(parent, false);

            Image cardImage = cardObject.GetComponent<Image>();
            cardImage.color = new Color(0.11f, 0.15f, 0.2f, 0.84f);
            cardImage.raycastTarget = false;

            labelTexts[i] = CreateText(cardObject.transform, "Label", ShipStatLabels[i], new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -12f), new Vector2(88f, 18f), 14f, TextAlignmentOptions.Left);
            labelTexts[i].fontStyle = FontStyles.Bold;
            labelTexts[i].color = new Color(0.82f, 0.88f, 0.94f, 0.94f);
            labelTexts[i].raycastTarget = false;

            valueTexts[i] = CreateText(cardObject.transform, "Value", string.Empty, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-46f, -12f), new Vector2(76f, 18f), 14f, TextAlignmentOptions.Right);
            valueTexts[i].fontStyle = FontStyles.Bold;
            valueTexts[i].color = new Color(0.96f, 0.98f, 1f, 0.98f);
            valueTexts[i].raycastTarget = false;

            GameObject barBgObject = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            barBgObject.transform.SetParent(cardObject.transform, false);
            RectTransform barBgRect = barBgObject.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0f);
            barBgRect.anchorMax = new Vector2(1f, 0f);
            barBgRect.pivot = new Vector2(0.5f, 0f);
            barBgRect.anchoredPosition = new Vector2(0f, 8f);
            barBgRect.sizeDelta = new Vector2(-18f, 12f);

            Image barBgImage = barBgObject.GetComponent<Image>();
            barBgImage.color = new Color(0.07f, 0.09f, 0.12f, 0.98f);
            barBgImage.raycastTarget = false;

            GameObject barFillObject = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            barFillObject.transform.SetParent(barBgObject.transform, false);
            RectTransform barFillRect = barFillObject.GetComponent<RectTransform>();
            barFillRect.anchorMin = new Vector2(0f, 0f);
            barFillRect.anchorMax = new Vector2(0f, 1f);
            barFillRect.pivot = new Vector2(0f, 0.5f);
            barFillRect.anchoredPosition = Vector2.zero;
            barFillRect.sizeDelta = new Vector2(0f, 0f);

            fillImages[i] = barFillObject.GetComponent<Image>();
            fillImages[i].color = new Color(0.28f, 0.86f, 0.36f, 0.98f);
            fillImages[i].raycastTarget = false;
        }
    }

    void CreateShipPreview(Transform parent)
    {
        GameObject previewRoot = new GameObject("ShipPreviewRoot", typeof(RectTransform), typeof(Image));
        previewRoot.transform.SetParent(parent, false);

        RectTransform rootRect = previewRoot.GetComponent<RectTransform>();
        shipPreviewRootRect = rootRect;
        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = new Vector2(-40f, -556f);
        rootRect.sizeDelta = new Vector2(640f, 380f);

        Image rootImage = previewRoot.GetComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0f);
        rootImage.raycastTarget = false;
        rootImage.enabled = false;

        GameObject hitboxObject = new GameObject("ShipPreviewHitbox", typeof(RectTransform), typeof(Image), typeof(Button));
        hitboxObject.transform.SetParent(previewRoot.transform, false);
        RectTransform hitboxRect = hitboxObject.GetComponent<RectTransform>();
        hitboxRect.anchorMin = new Vector2(0.5f, 0.5f);
        hitboxRect.anchorMax = new Vector2(0.5f, 0.5f);
        hitboxRect.pivot = new Vector2(0.5f, 0.5f);
        hitboxRect.anchoredPosition = new Vector2(0f, 0f);
        hitboxRect.sizeDelta = new Vector2(430f, 280f);

        Image hitboxImage = hitboxObject.GetComponent<Image>();
        hitboxImage.color = new Color(1f, 1f, 1f, 0f);
        shipPreviewButton = hitboxObject.GetComponent<Button>();
        shipPreviewButton.transition = Selectable.Transition.None;
        shipPreviewButton.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            OnShipPreviewClicked();
        });

        GameObject imageObject = new GameObject("ShipPreviewImage", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(previewRoot.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = ShipPreviewImagePosition;
        imageRect.sizeDelta = new Vector2(294f, 188f);
        shipPreviewImage = imageObject.GetComponent<Image>();
        shipPreviewImage.preserveAspect = true;
        shipPreviewImage.raycastTarget = false;

        CreateShipStatsPanel(parent);

        equipmentSlotRects = new RectTransform[PlayerInventoryData.EquipmentSlotCount];
        equipmentSlotButtons = new Button[PlayerInventoryData.EquipmentSlotCount];
        equipmentSlotPreviewTexts = new TMP_Text[PlayerInventoryData.EquipmentSlotCount];
        equipmentSlotPreviewIcons = new Image[PlayerInventoryData.EquipmentSlotCount];
        equipmentSlotButtons[0] = CreateEquipmentSlotButton(previewRoot.transform, "MainGunA", Vector2.zero, 0, "MAIN GUN", out equipmentSlotPreviewTexts[0], out equipmentSlotPreviewIcons[0]);
        equipmentSlotButtons[1] = CreateEquipmentSlotButton(previewRoot.transform, "MainGunB", Vector2.zero, 1, "MAIN GUN", out equipmentSlotPreviewTexts[1], out equipmentSlotPreviewIcons[1]);
        equipmentSlotButtons[2] = CreateEquipmentSlotButton(previewRoot.transform, "ShieldA", Vector2.zero, 2, "SHIELD", out equipmentSlotPreviewTexts[2], out equipmentSlotPreviewIcons[2]);
        equipmentSlotButtons[3] = CreateEquipmentSlotButton(previewRoot.transform, "ShieldB", Vector2.zero, 3, "SHIELD", out equipmentSlotPreviewTexts[3], out equipmentSlotPreviewIcons[3]);
        equipmentSlotButtons[4] = CreateEquipmentSlotButton(previewRoot.transform, "EngineA", Vector2.zero, 4, "ENGINE", out equipmentSlotPreviewTexts[4], out equipmentSlotPreviewIcons[4]);
        equipmentSlotButtons[5] = CreateEquipmentSlotButton(previewRoot.transform, "EngineB", Vector2.zero, 5, "ENGINE", out equipmentSlotPreviewTexts[5], out equipmentSlotPreviewIcons[5]);
        equipmentSlotButtons[6] = CreateEquipmentSlotButton(previewRoot.transform, "GadgetA", Vector2.zero, 6, "GADGET", out equipmentSlotPreviewTexts[6], out equipmentSlotPreviewIcons[6]);
        equipmentSlotButtons[7] = CreateEquipmentSlotButton(previewRoot.transform, "GadgetB", Vector2.zero, 7, "GADGET", out equipmentSlotPreviewTexts[7], out equipmentSlotPreviewIcons[7]);

        for (int i = 0; i < equipmentSlotButtons.Length; i++)
        {
            if (equipmentSlotButtons[i] != null)
                equipmentSlotRects[i] = equipmentSlotButtons[i].GetComponent<RectTransform>();
        }

        UpdateEquipmentSlotLayout();
    }

    void CreateShipImageModal(Transform parent)
    {
        shipImageModalObject = new GameObject("ShipImageModal", typeof(RectTransform), typeof(Image));
        shipImageModalObject.transform.SetParent(parent, false);

        RectTransform rootRect = shipImageModalObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image overlay = shipImageModalObject.GetComponent<Image>();
        overlay.color = new Color(0.02f, 0.03f, 0.05f, 0.9f);

        GameObject panel = new GameObject("ShipImageModalPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(shipImageModalObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 6f);
        panelRect.sizeDelta = new Vector2(1080f, 760f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.98f);

        CreateText(panel.transform, "ShipImageModalTitle", "SHIP PREVIEW", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(360f, 36f), 28f, TextAlignmentOptions.Center);

        GameObject imageObject = new GameObject("ShipImageModalPreview", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(panel.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = new Vector2(0f, 18f);
        imageRect.sizeDelta = new Vector2(840f, 520f);

        shipImageModalImage = imageObject.GetComponent<Image>();
        shipImageModalImage.preserveAspect = true;

        Button closeButton = CreateButton(panel.transform, "ShipImageModalCloseButton", "CLOSE", new Vector2(0f, -660f), new Vector2(210f, 60f), HideShipImageModal);
        StyleButton(closeButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.22f, 0.3f, 0.4f, 1f));

        shipImageModalObject.SetActive(false);
    }

    void CreateShipStatsPanel(Transform parent)
    {
        shipStatsPanelObject = new GameObject("ShipStatsPanel", typeof(RectTransform));
        shipStatsPanelObject.transform.SetParent(parent, false);

        RectTransform panelRect = shipStatsPanelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-38f, -500f);
        panelRect.sizeDelta = new Vector2(668f, 128f);

        const int cardCount = 8;
        const int columns = 4;
        const float cardWidth = 156f;
        const float cardHeight = 52f;
        const float cardSpacingX = 12f;
        const float cardSpacingY = 12f;

        shipStatLabelTexts = new TMP_Text[cardCount];
        shipStatValueTexts = new TMP_Text[cardCount];
        shipStatFillImages = new Image[cardCount];

        for (int i = 0; i < cardCount; i++)
        {
            int row = i / columns;
            int col = i % columns;
            float x = col * (cardWidth + cardSpacingX);
            float y = -row * (cardHeight + cardSpacingY);

            GameObject cardObject = new GameObject("ShipStatCard_" + ShipStatLabels[i], typeof(RectTransform), typeof(Image));
            cardObject.transform.SetParent(shipStatsPanelObject.transform, false);

            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 1f);
            cardRect.anchorMax = new Vector2(0f, 1f);
            cardRect.pivot = new Vector2(0f, 1f);
            cardRect.anchoredPosition = new Vector2(x, y);
            cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);

            Image cardImage = cardObject.GetComponent<Image>();
            cardImage.color = new Color(0.11f, 0.15f, 0.2f, 0.84f);

            TMP_Text labelText = CreateText(cardObject.transform, "Label", ShipStatLabels[i], new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -12f), new Vector2(88f, 18f), 14f, TextAlignmentOptions.Left);
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = new Color(0.82f, 0.88f, 0.94f, 0.94f);
            shipStatLabelTexts[i] = labelText;

            TMP_Text valueText = CreateText(cardObject.transform, "Value", string.Empty, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-46f, -12f), new Vector2(76f, 18f), 14f, TextAlignmentOptions.Right);
            valueText.fontStyle = FontStyles.Bold;
            valueText.color = new Color(0.96f, 0.98f, 1f, 0.98f);
            shipStatValueTexts[i] = valueText;

            GameObject barBgObject = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            barBgObject.transform.SetParent(cardObject.transform, false);
            RectTransform barBgRect = barBgObject.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0f);
            barBgRect.anchorMax = new Vector2(1f, 0f);
            barBgRect.pivot = new Vector2(0.5f, 0f);
            barBgRect.anchoredPosition = new Vector2(0f, 8f);
            barBgRect.sizeDelta = new Vector2(-18f, 12f);

            Image barBgImage = barBgObject.GetComponent<Image>();
            barBgImage.color = new Color(0.07f, 0.09f, 0.12f, 0.98f);

            GameObject barFillObject = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            barFillObject.transform.SetParent(barBgObject.transform, false);
            RectTransform barFillRect = barFillObject.GetComponent<RectTransform>();
            barFillRect.anchorMin = new Vector2(0f, 0f);
            barFillRect.anchorMax = new Vector2(0f, 1f);
            barFillRect.pivot = new Vector2(0f, 0.5f);
            barFillRect.anchoredPosition = Vector2.zero;
            barFillRect.sizeDelta = new Vector2(0f, 0f);

            Image barFillImage = barFillObject.GetComponent<Image>();
            barFillImage.color = new Color(0.28f, 0.86f, 0.36f, 0.98f);
            shipStatFillImages[i] = barFillImage;
        }
    }

    void CreateSplashScreen(Transform parent)
    {
        splashScreenObject = new GameObject("StartupSplashScreen", typeof(RectTransform), typeof(Image));
        splashScreenObject.transform.SetParent(parent, false);

        RectTransform rect = splashScreenObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image splashBackground = splashScreenObject.GetComponent<Image>();
        splashBackground.color = Color.black;
        splashBackground.raycastTarget = false;

        GameObject logoObject = new GameObject("StartupSplashLogo", typeof(RectTransform), typeof(Image));
        logoObject.transform.SetParent(splashScreenObject.transform, false);
        RectTransform logoRect = logoObject.GetComponent<RectTransform>();
        logoRect.anchorMin = Vector2.zero;
        logoRect.anchorMax = Vector2.one;
        logoRect.offsetMin = Vector2.zero;
        logoRect.offsetMax = Vector2.zero;

        splashScreenImage = logoObject.GetComponent<Image>();
        splashScreenImage.color = Color.white;
        splashScreenImage.preserveAspect = true;
        splashScreenImage.raycastTarget = false;
        splashScreenImage.sprite = LoadStandaloneSprite("STAR_RAIDERS_ekran.png");

        if (!splashShownOnce)
        {
            splashHideTime = Time.unscaledTime + 3f;
            splashShownOnce = true;
        }
        else
        {
            splashHideTime = -1f;
            splashScreenObject.SetActive(false);
        }
    }

    void CreateItemPreview(Transform parent)
    {
        itemPreviewPanelObject = new GameObject("ItemPreviewPanel", typeof(RectTransform), typeof(Image));
        itemPreviewPanelObject.transform.SetParent(parent, false);

        RectTransform rect = itemPreviewPanelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 172f);
        rect.sizeDelta = new Vector2(304f, 326f);

        Image background = itemPreviewPanelObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0f);
        background.raycastTarget = false;

        GameObject backgroundCardObject = new GameObject("ItemPreviewBackground", typeof(RectTransform), typeof(Image));
        backgroundCardObject.transform.SetParent(itemPreviewPanelObject.transform, false);
        RectTransform backgroundCardRect = backgroundCardObject.GetComponent<RectTransform>();
        backgroundCardRect.anchorMin = new Vector2(0.5f, 1f);
        backgroundCardRect.anchorMax = new Vector2(0.5f, 1f);
        backgroundCardRect.pivot = new Vector2(0.5f, 1f);
        backgroundCardRect.anchoredPosition = new Vector2(0f, -4f);
        backgroundCardRect.sizeDelta = new Vector2(250f, 250f);
        itemPreviewBackgroundImage = backgroundCardObject.GetComponent<Image>();
        itemPreviewBackgroundImage.color = new Color(0.08f, 0.12f, 0.16f, 0.92f);
        itemPreviewBackgroundImage.raycastTarget = false;

        GameObject iconObject = new GameObject("ItemPreviewIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(itemPreviewPanelObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -20f);
        iconRect.sizeDelta = new Vector2(128f, 128f);
        itemPreviewIcon = iconObject.GetComponent<Image>();
        itemPreviewIcon.preserveAspect = true;

        itemPreviewNameText = CreateText(itemPreviewPanelObject.transform, "ItemPreviewNameText", "SELECT ITEM", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(228f, 30f), 20f, TextAlignmentOptions.Center);
        itemPreviewTypeText = CreateText(itemPreviewPanelObject.transform, "ItemPreviewTypeText", "Misc", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -188f), new Vector2(228f, 26f), 20f, TextAlignmentOptions.Center);
        itemPreviewTypeText.fontStyle = FontStyles.Bold;
        itemPreviewTypeText.color = new Color(0.72f, 0.86f, 1f, 1f);
        itemPreviewPriceText = CreateText(itemPreviewPanelObject.transform, "ItemPreviewPriceText", "0 Astrons", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -218f), new Vector2(228f, 26f), 20f, TextAlignmentOptions.Center);
        itemPreviewPriceText.fontStyle = FontStyles.Normal;
        itemPreviewSellButton = CreateButton(itemPreviewPanelObject.transform, "ItemPreviewSellButton", "SELL", new Vector2(-68f, -286f), new Vector2(108f, 42f), OnItemPreviewSellClicked);
        itemPreviewSalvageButton = CreateButton(itemPreviewPanelObject.transform, "ItemPreviewSalvageButton", "SALVAGE", new Vector2(68f, -286f), new Vector2(108f, 42f), OnItemPreviewSalvageClicked);
        itemPreviewPanelObject.SetActive(false);
    }

    void CreatePlayerInventoryExtendConfirm(Transform parent)
    {
        playerInventoryExtendConfirmObject = new GameObject("PlayerInventoryExtendConfirm", typeof(RectTransform), typeof(Image));
        playerInventoryExtendConfirmObject.transform.SetParent(parent, false);
        RectTransform overlayRect = playerInventoryExtendConfirmObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = playerInventoryExtendConfirmObject.GetComponent<Image>();
        overlayImage.color = new Color(0.01f, 0.02f, 0.035f, 0.72f);
        overlayImage.raycastTarget = true;

        GameObject panel = new GameObject("PlayerInventoryExtendConfirmPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(playerInventoryExtendConfirmObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(560f, 240f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.07f, 0.1f, 0.14f, 0.98f);

        CreateText(panel.transform, "PlayerInventoryExtendTitle", "EXTEND INVENTORY", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(500f, 34f), 24f, TextAlignmentOptions.Center);
        playerInventoryExtendConfirmText = CreateText(panel.transform, "PlayerInventoryExtendText", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(500f, 70f), 19f, TextAlignmentOptions.Center);
        playerInventoryExtendConfirmText.fontStyle = FontStyles.Normal;
        playerInventoryExtendConfirmText.textWrappingMode = TextWrappingModes.Normal;

        playerInventoryExtendConfirmButton = CreateButton(panel.transform, "PlayerInventoryExtendConfirmButton", "EXTEND", new Vector2(-116f, -166f), new Vector2(180f, 52f), OnPlayerInventoryExtendConfirmClicked);
        StyleButton(playerInventoryExtendConfirmButton, new Color(0.1f, 0.46f, 0.34f, 1f), new Color(0.16f, 0.62f, 0.44f, 1f));
        playerInventoryExtendCancelButton = CreateButton(panel.transform, "PlayerInventoryExtendCancelButton", "CANCEL", new Vector2(116f, -166f), new Vector2(180f, 52f), HidePlayerInventoryExtendConfirm);
        StyleButton(playerInventoryExtendCancelButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.22f, 0.3f, 0.4f, 1f));

        playerInventoryExtendConfirmObject.SetActive(false);
    }

    void CreateCraftingPanel(Transform parent)
    {
        craftingPanelObject = new GameObject("CraftingPanel", typeof(RectTransform), typeof(Image));
        craftingPanelObject.transform.SetParent(parent, false);

        RectTransform rect = craftingPanelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -198f);
        rect.sizeDelta = new Vector2(330f, 430f);

        Image background = craftingPanelObject.GetComponent<Image>();
        background.color = new Color(0.07f, 0.1f, 0.14f, 0.94f);

        craftingCatalogButton = CreateButton(craftingPanelObject.transform, "CraftingCatalogButton", "CRAFTING", new Vector2(0f, -14f), new Vector2(252f, 52f), OnCraftingCatalogClicked);
        StyleButton(craftingCatalogButton, new Color(0.12f, 0.48f, 0.28f, 1f), new Color(0.17f, 0.62f, 0.36f, 1f));
        TMP_Text title = craftingCatalogButton != null ? craftingCatalogButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (title != null)
        {
            title.characterSpacing = 3f;
            title.fontSize = 24f;
        }
        if (craftingCatalogButton != null)
        {
            Outline outline = craftingCatalogButton.GetComponent<Outline>();
            if (outline == null)
                outline = craftingCatalogButton.gameObject.AddComponent<Outline>();

            outline.effectColor = new Color(0.32f, 0.94f, 0.58f, 0.9f);
            outline.effectDistance = new Vector2(3f, 3f);

            Shadow shadow = craftingCatalogButton.GetComponent<Shadow>();
            if (shadow == null)
                shadow = craftingCatalogButton.gameObject.AddComponent<Shadow>();

            shadow.effectColor = new Color(0f, 0f, 0f, 0.34f);
            shadow.effectDistance = new Vector2(0f, -3f);
        }

        TMP_Text hint = CreateText(craftingPanelObject.transform, "CraftingHint", "Drop 4 matching resources here.", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(280f, 22f), 15f, TextAlignmentOptions.Center);
        hint.fontStyle = FontStyles.Normal;
        hint.color = new Color(0.82f, 0.88f, 0.95f, 0.84f);

        craftingSlotButtons = new Button[PlayerInventoryData.CraftingSlotCount];
        craftingSlotTexts = new TMP_Text[PlayerInventoryData.CraftingSlotCount];
        craftingSlotIcons = new Image[PlayerInventoryData.CraftingSlotCount];

        Vector2[] positions =
        {
            new Vector2(-66f, -98f),
            new Vector2(66f, -98f),
            new Vector2(-66f, -226f),
            new Vector2(66f, -226f)
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

        craftButton = CreateButton(craftingPanelObject.transform, "CraftButton", "CRAFT", new Vector2(0f, -336f), new Vector2(190f, 52f), OnCraftButtonClicked);
        clearCraftButton = CreateButton(craftingPanelObject.transform, "ClearCraftButton", "CLEAR", new Vector2(0f, -404f), new Vector2(190f, 46f), OnClearCraftingSlotsClicked);
        StyleButton(clearCraftButton, new Color(0.18f, 0.24f, 0.32f, 0.98f), new Color(0.24f, 0.32f, 0.42f, 1f));
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
        scrollbarRect.anchoredPosition = new Vector2(572f, -12f);
        scrollbarRect.sizeDelta = new Vector2(34f, 592f);

        Image scrollbarBg = scrollbarObject.GetComponent<Image>();
        scrollbarBg.color = new Color(0.1f, 0.14f, 0.18f, 0.9f);

        GameObject slidingAreaObject = new GameObject("Sliding Area", typeof(RectTransform));
        slidingAreaObject.transform.SetParent(scrollbarObject.transform, false);
        RectTransform slidingAreaRect = slidingAreaObject.GetComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(4f, 4f);
        slidingAreaRect.offsetMax = new Vector2(-4f, -4f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(slidingAreaObject.transform, false);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 1f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.pivot = new Vector2(0.5f, 1f);
        handleRect.sizeDelta = new Vector2(0f, 92f);

        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(0.22f, 0.74f, 0.62f, 0.96f);

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;

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

        RefreshCraftingRecipeBrowser();
        craftingRecipeBrowserObject.SetActive(true);
        craftingRecipeBrowserObject.transform.SetAsLastSibling();
    }

    void HideCraftingRecipeBrowser()
    {
        if (craftingRecipeBrowserObject != null)
            craftingRecipeBrowserObject.SetActive(false);
    }

    void CreateShopBrowser(Transform parent)
    {
        shopBrowserObject = new GameObject("ShopBrowser", typeof(RectTransform), typeof(Image));
        shopBrowserObject.transform.SetParent(parent, false);

        RectTransform overlayRect = shopBrowserObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlay = shopBrowserObject.GetComponent<Image>();
        overlay.color = new Color(0.03f, 0.04f, 0.06f, 0.72f);

        GameObject panel = new GameObject("ShopBrowserPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(shopBrowserObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 6f);
        panelRect.sizeDelta = new Vector2(1040f, 800f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.98f);

        TMP_Text title = CreateText(panel.transform, "ShopBrowserTitle", "SHOP", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(420f, 34f), 30f, TextAlignmentOptions.Center);
        title.characterSpacing = 3f;

        shopAstronsText = CreateText(panel.transform, "ShopAstronsText", "Astrons: 0", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(440f, 28f), 20f, TextAlignmentOptions.Center);
        shopAstronsText.fontStyle = FontStyles.Normal;
        shopAstronsText.color = new Color(0.94f, 0.84f, 0.44f, 1f);

        shopCloseButton = CreateButton(panel.transform, "ShopCloseButton", "CLOSE", new Vector2(0f, -722f), new Vector2(220f, 58f), HideShopBrowser);
        StyleButton(shopCloseButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.22f, 0.3f, 0.4f, 1f));

        GameObject viewportObject = new GameObject("ShopViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(panel.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = new Vector2(-18f, -22f);
        viewportRect.sizeDelta = new Vector2(920f, 578f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.11f, 0.15f, 0.2f, 0.82f);

        GameObject contentObject = new GameObject("ShopContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        shopContentRect = contentObject.GetComponent<RectTransform>();
        shopContentRect.anchorMin = new Vector2(0f, 1f);
        shopContentRect.anchorMax = new Vector2(1f, 1f);
        shopContentRect.pivot = new Vector2(0.5f, 1f);
        shopContentRect.anchoredPosition = Vector2.zero;
        shopContentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        GameObject scrollbarObject = new GameObject("ShopScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarObject.transform.SetParent(panel.transform, false);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(0.5f, 0.5f);
        scrollbarRect.anchorMax = new Vector2(0.5f, 0.5f);
        scrollbarRect.pivot = new Vector2(0.5f, 0.5f);
        scrollbarRect.anchoredPosition = new Vector2(472f, -22f);
        scrollbarRect.sizeDelta = new Vector2(34f, 578f);

        Image scrollbarBg = scrollbarObject.GetComponent<Image>();
        scrollbarBg.color = new Color(0.1f, 0.14f, 0.18f, 0.9f);

        GameObject slidingAreaObject = new GameObject("Sliding Area", typeof(RectTransform));
        slidingAreaObject.transform.SetParent(scrollbarObject.transform, false);
        RectTransform slidingAreaRect = slidingAreaObject.GetComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(4f, 4f);
        slidingAreaRect.offsetMax = new Vector2(-4f, -4f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(slidingAreaObject.transform, false);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 1f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.pivot = new Vector2(0.5f, 1f);
        handleRect.sizeDelta = new Vector2(0f, 92f);

        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(0.24f, 0.72f, 0.92f, 0.96f);

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;

        shopScrollRect = viewportObject.GetComponent<ScrollRect>();
        shopScrollRect.horizontal = false;
        shopScrollRect.vertical = true;
        shopScrollRect.movementType = ScrollRect.MovementType.Clamped;
        shopScrollRect.viewport = viewportRect;
        shopScrollRect.content = shopContentRect;
        shopScrollRect.verticalScrollbar = scrollbar;
        shopScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        shopScrollRect.scrollSensitivity = 30f;

        shopBrowserObject.SetActive(false);
    }

    void OnShopButtonClicked()
    {
        if (inventoryActionInProgress || dragInProgress || shopBrowserObject == null)
            return;

        HideCraftingRecipeBrowser();
        HideItemPreview();
        HideCheatBrowser();
        RefreshShopBrowser();
        shopBrowserObject.SetActive(true);
        shopBrowserObject.transform.SetAsLastSibling();
    }

    void HideShopBrowser()
    {
        if (shopBrowserObject != null)
            shopBrowserObject.SetActive(false);
    }

    void CreateCheatBrowser(Transform parent)
    {
        cheatBrowserObject = new GameObject("CheatBrowser", typeof(RectTransform), typeof(Image));
        cheatBrowserObject.transform.SetParent(parent, false);

        RectTransform overlayRect = cheatBrowserObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlay = cheatBrowserObject.GetComponent<Image>();
        overlay.color = new Color(0.03f, 0.04f, 0.06f, 0.72f);

        GameObject panel = new GameObject("CheatBrowserPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(cheatBrowserObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 6f);
        panelRect.sizeDelta = new Vector2(620f, 380f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.11f, 0.1f, 0.14f, 0.98f);

        TMP_Text title = CreateText(panel.transform, "CheatBrowserTitle", "CHEAT", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(420f, 34f), 30f, TextAlignmentOptions.Center);
        title.characterSpacing = 3f;

        TMP_Text hint = CreateText(panel.transform, "CheatBrowserHint", "This is temporary solution to speed up tests.", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(520f, 64f), 19f, TextAlignmentOptions.Center);
        hint.fontStyle = FontStyles.Normal;
        hint.textWrappingMode = TextWrappingModes.Normal;
        hint.color = new Color(0.86f, 0.9f, 0.96f, 0.96f);

        cheatAstronsText = CreateText(panel.transform, "CheatAstronsText", "Astrons: 0", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -166f), new Vector2(440f, 28f), 20f, TextAlignmentOptions.Center);
        cheatAstronsText.fontStyle = FontStyles.Normal;
        cheatAstronsText.color = new Color(0.94f, 0.84f, 0.44f, 1f);

        cheatAddMoneyButton = CreateButton(panel.transform, "CheatAddMoneyButton", "ADD MONEY", new Vector2(0f, -212f), new Vector2(260f, 62f), OnCheatAddMoneyClicked);
        StyleButton(cheatAddMoneyButton, new Color(0.5f, 0.22f, 0.18f, 1f), new Color(0.7f, 0.3f, 0.22f, 1f));

        cheatStatusText = CreateText(panel.transform, "CheatStatusText", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -292f), new Vector2(500f, 28f), 17f, TextAlignmentOptions.Center);
        cheatStatusText.fontStyle = FontStyles.Normal;
        cheatStatusText.color = new Color(0.74f, 0.86f, 0.94f, 0.96f);

        cheatCloseButton = CreateButton(panel.transform, "CheatCloseButton", "CLOSE", new Vector2(0f, -318f), new Vector2(220f, 52f), HideCheatBrowser);
        StyleButton(cheatCloseButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.22f, 0.3f, 0.4f, 1f));

        cheatBrowserObject.SetActive(false);
    }

    void OnCheatButtonClicked()
    {
        if (inventoryActionInProgress || dragInProgress || cheatBrowserObject == null)
            return;

        HideCraftingRecipeBrowser();
        HideItemPreview();
        HideShopBrowser();
        RefreshCheatBrowser(string.Empty);
        cheatBrowserObject.SetActive(true);
        cheatBrowserObject.transform.SetAsLastSibling();
    }

    void HideCheatBrowser()
    {
        if (cheatBrowserObject != null)
            cheatBrowserObject.SetActive(false);
    }

    void RefreshCheatBrowser(string statusMessage = null)
    {
        if (cheatBrowserObject == null)
            return;

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        int astrons = profile != null ? profile.Astrons : 0;
        if (cheatAstronsText != null)
            cheatAstronsText.text = "Astrons: " + astrons;

        if (statusMessage != null && cheatStatusText != null)
            cheatStatusText.text = statusMessage;

        if (cheatAddMoneyButton != null)
            cheatAddMoneyButton.interactable = !inventoryActionInProgress;
        if (cheatCloseButton != null)
            cheatCloseButton.interactable = !inventoryActionInProgress;
    }

    async void OnCheatAddMoneyClicked()
    {
        if (inventoryActionInProgress || cheatBrowserObject == null)
            return;

        inventoryActionInProgress = true;
        SetInventoryInteractable(false);
        SetStatus("Adding 5000 Astrons...");
        RefreshCheatBrowser("Adding 5000 Astrons...");

        try
        {
            await PlayerProfileService.Instance.AddAstronsAsync(5000);
            SetStatus("Added 5000 Astrons.");
            RefreshView();
            RefreshCheatBrowser("Added 5000 Astrons.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Cheat add money failed: " + ex);
            SetStatus("Cheat failed.");
            RefreshCheatBrowser("Could not add Astrons.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInventoryInteractable(true);
            RefreshCheatBrowser();
        }
    }

    void RefreshShopBrowser()
    {
        if (shopBrowserObject == null || shopContentRect == null)
            return;

        for (int i = 0; i < shopRowObjects.Count; i++)
        {
            if (shopRowObjects[i] != null)
                Destroy(shopRowObjects[i]);
        }

        shopRowObjects.Clear();

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        int astrons = profile != null ? profile.Astrons : 0;
        if (shopAstronsText != null)
            shopAstronsText.gameObject.SetActive(false);

        IReadOnlyList<InventoryItemDefinition> definitions = InventoryItemCatalog.GetAllDefinitions();
        List<InventoryItemDefinition> shopItems = new List<InventoryItemDefinition>();
        List<int> shopPrices = new List<int>();
        List<bool> affordability = new List<bool>();

        for (int i = 0; i < definitions.Count; i++)
        {
            InventoryItemDefinition definition = definitions[i];
            if (definition == null || definition.ItemType != InventoryItemType.Equipment)
                continue;

            int price = InventoryItemCatalog.GetShopBuyValueAstrons(definition.Id);
            if (price <= 0)
                continue;

            shopItems.Add(definition);
            shopPrices.Add(price);
            affordability.Add(astrons >= price);
        }

        for (int i = 0; i < shopItems.Count; i += 2)
        {
            InventoryItemDefinition leftDefinition = shopItems[i];
            int leftPrice = shopPrices[i];
            bool leftCanAfford = affordability[i];
            InventoryItemDefinition rightDefinition = i + 1 < shopItems.Count ? shopItems[i + 1] : null;
            int rightPrice = i + 1 < shopPrices.Count ? shopPrices[i + 1] : 0;
            bool rightCanAfford = i + 1 < affordability.Count && affordability[i + 1];

            GameObject row = CreateShopRow(leftDefinition, leftPrice, leftCanAfford, rightDefinition, rightPrice, rightCanAfford);
            if (row != null)
                shopRowObjects.Add(row);
        }

        Canvas.ForceUpdateCanvases();
        if (shopScrollRect != null)
            shopScrollRect.verticalNormalizedPosition = 1f;
    }

    GameObject CreateShopRow(
        InventoryItemDefinition leftDefinition,
        int leftPrice,
        bool leftCanAfford,
        InventoryItemDefinition rightDefinition,
        int rightPrice,
        bool rightCanAfford)
    {
        if (shopContentRect == null || leftDefinition == null)
            return null;

        GameObject rowObject = new GameObject("ShopRow_" + leftDefinition.Id, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowObject.transform.SetParent(shopContentRect, false);

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 294f);

        LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 294f;

        Image rowImage = rowObject.GetComponent<Image>();
        rowImage.color = new Color(0.12f, 0.16f, 0.21f, 0.98f);

        CreateShopCard(rowObject.transform, leftDefinition, leftPrice, leftCanAfford, -130f);
        if (rightDefinition != null)
            CreateShopCard(rowObject.transform, rightDefinition, rightPrice, rightCanAfford, 130f);

        return rowObject;
    }

    void CreateShopCard(Transform parent, InventoryItemDefinition definition, int price, bool canAfford, float centerX)
    {
        if (parent == null || definition == null)
            return;

        Button itemCardButton = CreateButton(parent, "ShopItemCardButton_" + definition.Id, string.Empty, new Vector2(centerX, -12f), new Vector2(214f, 214f), () => OnShopItemPreviewClicked(definition.Id));
        itemCardButton.interactable = !inventoryActionInProgress;

        Image itemCardImage = itemCardButton.GetComponent<Image>();
        if (itemCardImage != null)
            itemCardImage.color = InventoryItemCatalog.GetRarityColor(definition.Rarity);

        Outline itemCardOutline = itemCardButton.GetComponent<Outline>();
        if (itemCardOutline == null)
            itemCardOutline = itemCardButton.gameObject.AddComponent<Outline>();
        itemCardOutline.effectColor = new Color(0f, 0f, 0f, 0.28f);
        itemCardOutline.effectDistance = new Vector2(4f, 4f);

        Shadow itemCardShadow = itemCardButton.GetComponent<Shadow>();
        if (itemCardShadow == null)
            itemCardShadow = itemCardButton.gameObject.AddComponent<Shadow>();
        itemCardShadow.effectColor = new Color(0f, 0f, 0f, 0.2f);
        itemCardShadow.effectDistance = new Vector2(0f, -3f);

        GameObject iconObject = new GameObject("ShopItemIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(itemCardButton.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -12f);
        iconRect.sizeDelta = new Vector2(108f, 108f);

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = definition.GetIcon();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text nameText = CreateText(itemCardButton.transform, "ShopItemName", definition.DisplayName.ToUpperInvariant(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(182f, 28f), 18f, TextAlignmentOptions.Center);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 12f;
        nameText.fontSizeMax = 18f;
        nameText.textWrappingMode = TextWrappingModes.Normal;
        nameText.overflowMode = TextOverflowModes.Truncate;
        nameText.raycastTarget = false;

        TMP_Text typeText = CreateText(itemCardButton.transform, "ShopItemType", InventoryItemCatalog.GetCategoryLabel(definition.Id), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(182f, 22f), 15f, TextAlignmentOptions.Center);
        typeText.fontStyle = FontStyles.Bold;
        typeText.color = new Color(0.92f, 0.96f, 1f, 0.96f);
        typeText.raycastTarget = false;

        GameObject priceIconObject = new GameObject("ShopItemPriceIcon", typeof(RectTransform), typeof(Image));
        priceIconObject.transform.SetParent(itemCardButton.transform, false);
        RectTransform priceIconRect = priceIconObject.GetComponent<RectTransform>();
        priceIconRect.anchorMin = new Vector2(0.5f, 1f);
        priceIconRect.anchorMax = new Vector2(0.5f, 1f);
        priceIconRect.pivot = new Vector2(1f, 1f);
        priceIconRect.anchoredPosition = new Vector2(-10f, -176f);
        priceIconRect.sizeDelta = new Vector2(22f, 22f);

        Image priceIcon = priceIconObject.GetComponent<Image>();
        priceIcon.sprite = LoadSpriteFromResources("UI/icon_trader");
        priceIcon.color = new Color(0.94f, 0.84f, 0.44f, 1f);
        priceIcon.preserveAspect = true;
        priceIcon.raycastTarget = false;

        TMP_Text priceText = CreateText(itemCardButton.transform, "ShopItemPrice", price.ToString(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(24f, -176f), new Vector2(136f, 24f), 20f, TextAlignmentOptions.Center);
        priceText.fontStyle = FontStyles.Normal;
        priceText.color = canAfford ? new Color(0.94f, 0.84f, 0.44f, 1f) : new Color(0.78f, 0.46f, 0.46f, 1f);
        priceText.raycastTarget = false;

        Button buyButton = CreateButton(parent, "ShopBuyButton_" + definition.Id, "BUY", new Vector2(centerX, -236f), new Vector2(150f, 50f), () => OnShopBuyClicked(definition.Id));
        StyleButton(buyButton, new Color(0.12f, 0.46f, 0.34f, 1f), new Color(0.16f, 0.62f, 0.44f, 1f));
        buyButton.interactable = canAfford && !inventoryActionInProgress;
    }

    void OnShopItemPreviewClicked(string itemId)
    {
        if (inventoryActionInProgress || dragInProgress || string.IsNullOrWhiteSpace(itemId))
            return;

        if (IsPreviewingSameItem(ProfileItemSource.ShopListing, -1, itemId))
        {
            HideItemPreview();
            SetStatus(string.Empty);
            return;
        }

        ShowItemPreview(ProfileItemSource.ShopListing, -1, itemId);
        SetStatus("Shop item selected.");
    }

    async void OnShopBuyClicked(string itemId)
    {
        if (inventoryActionInProgress || string.IsNullOrWhiteSpace(itemId))
            return;

        inventoryActionInProgress = true;
        SetInventoryInteractable(false);
        SetStatus("Buying " + InventoryItemCatalog.GetDisplayName(itemId) + "...");

        try
        {
            bool bought = await PlayerProfileService.Instance.TryBuyShopItemAsync(itemId);
            if (bought)
            {
                SetStatus("Bought " + InventoryItemCatalog.GetDisplayName(itemId) + ".");
                RefreshView();
                RefreshShopBrowser();
            }
            else
            {
                SetStatus("Not enough Astrons or inventory space.");
                RefreshShopBrowser();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Shop purchase failed: " + ex);
            SetStatus("Purchase failed.");
            RefreshShopBrowser();
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInventoryInteractable(true);
        }
    }

    void RefreshCraftingRecipeBrowser()
    {
        if (craftingRecipeBrowserObject == null || craftingRecipeContentRect == null)
            return;

        for (int i = 0; i < craftingRecipeRowObjects.Count; i++)
        {
            if (craftingRecipeRowObjects[i] != null)
                Destroy(craftingRecipeRowObjects[i]);
        }

        craftingRecipeRowObjects.Clear();

        IReadOnlyList<PlayerProfileCraftingRecipe> recipes = PlayerProfileCraftingCatalog.GetAllRecipes();
        if (recipes == null)
            return;

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        PlayerInventoryData inventory = profile != null && profile.Inventory != null
            ? profile.Inventory.Clone()
            : PlayerInventoryData.Default();
        inventory.Normalize();

        for (int i = 0; i < recipes.Count; i++)
        {
            PlayerProfileCraftingRecipe recipe = recipes[i];
            if (recipe == null)
                continue;

            GameObject row = CreateCraftingRecipeRow(recipe, CanPrepareCraftingRecipe(inventory, recipe));
            if (row != null)
                craftingRecipeRowObjects.Add(row);
        }

        Canvas.ForceUpdateCanvases();
        if (craftingRecipeScrollRect != null)
            craftingRecipeScrollRect.verticalNormalizedPosition = 1f;
    }

    GameObject CreateCraftingRecipeRow(PlayerProfileCraftingRecipe recipe, bool craftable)
    {
        if (craftingRecipeContentRect == null || recipe == null)
            return null;

        GameObject rowObject = new GameObject("CraftingRecipeRow_" + recipe.Id, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowObject.transform.SetParent(craftingRecipeContentRect, false);

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 196f);

        LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 196f;

        Image rowImage = rowObject.GetComponent<Image>();
        rowImage.color = new Color(0.12f, 0.16f, 0.21f, 0.98f);

        Button resultButton = CreateButton(rowObject.transform, "RecipeResultButton", string.Empty, new Vector2(-228f, -10f), new Vector2(174f, 174f), () => OnCraftingRecipeSelected(recipe.Id));
        resultButton.interactable = craftable && !inventoryActionInProgress;

        Image resultButtonImage = resultButton.GetComponent<Image>();
        if (resultButtonImage != null)
        {
            Color rarityColor = InventoryItemCatalog.GetRarityColor(recipe.OutputItemId);
            resultButtonImage.type = Image.Type.Simple;
            resultButtonImage.color = craftable
                ? rarityColor
                : Color.Lerp(rarityColor, new Color(0.16f, 0.18f, 0.22f, 1f), 0.58f);
        }

        Outline resultOutline = resultButton.GetComponent<Outline>();
        if (resultOutline == null)
            resultOutline = resultButton.gameObject.AddComponent<Outline>();

        resultOutline.effectColor = craftable
            ? new Color(0.23f, 0.92f, 0.49f, 0.95f)
            : new Color(0.28f, 0.36f, 0.44f, 0.8f);
        resultOutline.effectDistance = craftable ? new Vector2(4f, 4f) : new Vector2(2f, 2f);

        Shadow resultShadow = resultButton.GetComponent<Shadow>();
        if (resultShadow == null)
            resultShadow = resultButton.gameObject.AddComponent<Shadow>();

        resultShadow.effectColor = craftable
            ? new Color(0.07f, 0.38f, 0.18f, 0.55f)
            : new Color(0f, 0f, 0f, 0.22f);
        resultShadow.effectDistance = new Vector2(0f, -3f);

        Color frameColor = craftable
            ? new Color(0.24f, 0.94f, 0.5f, 1f)
            : new Color(0.18f, 0.24f, 0.3f, 0.92f);
        float frameThickness = craftable ? 6f : 4f;

        GameObject resultFrame = new GameObject("RecipeResultFrame", typeof(RectTransform));
        resultFrame.transform.SetParent(resultButton.transform, false);
        RectTransform resultFrameRect = resultFrame.GetComponent<RectTransform>();
        resultFrameRect.anchorMin = Vector2.zero;
        resultFrameRect.anchorMax = Vector2.one;
        resultFrameRect.pivot = new Vector2(0.5f, 0.5f);
        resultFrameRect.offsetMin = Vector2.zero;
        resultFrameRect.offsetMax = Vector2.zero;

        void CreateFrameBar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject bar = new GameObject(name, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(resultFrame.transform, false);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = anchorMin;
            barRect.anchorMax = anchorMax;
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.offsetMin = offsetMin;
            barRect.offsetMax = offsetMax;

            Image barImage = bar.GetComponent<Image>();
            barImage.color = frameColor;
            barImage.raycastTarget = false;
        }

        CreateFrameBar("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -frameThickness), new Vector2(0f, 0f));
        CreateFrameBar("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, frameThickness));
        CreateFrameBar("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(frameThickness, 0f));
        CreateFrameBar("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-frameThickness, 0f), new Vector2(0f, 0f));

        GameObject resultInner = new GameObject("RecipeResultInner", typeof(RectTransform), typeof(Image));
        resultInner.transform.SetParent(resultButton.transform, false);
        RectTransform resultInnerRect = resultInner.GetComponent<RectTransform>();
        resultInnerRect.anchorMin = new Vector2(0.5f, 0.5f);
        resultInnerRect.anchorMax = new Vector2(0.5f, 0.5f);
        resultInnerRect.pivot = new Vector2(0.5f, 0.5f);
        resultInnerRect.anchoredPosition = Vector2.zero;
        resultInnerRect.sizeDelta = new Vector2(164f, 164f);

        Image resultInnerImage = resultInner.GetComponent<Image>();
        resultInnerImage.color = new Color(0f, 0f, 0f, 0f);
        resultInnerImage.raycastTarget = false;

        GameObject resultIconObject = new GameObject("RecipeResultIcon", typeof(RectTransform), typeof(Image));
        resultIconObject.transform.SetParent(resultInner.transform, false);
        RectTransform resultIconRect = resultIconObject.GetComponent<RectTransform>();
        resultIconRect.anchorMin = new Vector2(0f, 0.5f);
        resultIconRect.anchorMax = new Vector2(0f, 0.5f);
        resultIconRect.pivot = new Vector2(0f, 0.5f);
        resultIconRect.anchorMin = new Vector2(0.5f, 1f);
        resultIconRect.anchorMax = new Vector2(0.5f, 1f);
        resultIconRect.pivot = new Vector2(0.5f, 1f);
        resultIconRect.anchoredPosition = new Vector2(0f, -14f);
        resultIconRect.sizeDelta = new Vector2(108f, 108f);

        Image resultIcon = resultIconObject.GetComponent<Image>();
        resultIcon.color = new Color(0f, 0f, 0f, 0f);
        resultIcon.raycastTarget = false;

        GameObject resultIconSpriteObject = new GameObject("RecipeResultIconSprite", typeof(RectTransform), typeof(Image));
        resultIconSpriteObject.transform.SetParent(resultIconObject.transform, false);
        RectTransform resultIconSpriteRect = resultIconSpriteObject.GetComponent<RectTransform>();
        resultIconSpriteRect.anchorMin = new Vector2(0.5f, 0.5f);
        resultIconSpriteRect.anchorMax = new Vector2(0.5f, 0.5f);
        resultIconSpriteRect.pivot = new Vector2(0.5f, 0.5f);
        resultIconSpriteRect.anchoredPosition = Vector2.zero;
        resultIconSpriteRect.sizeDelta = new Vector2(84f, 84f);

        Image resultIconSprite = resultIconSpriteObject.GetComponent<Image>();
        resultIconSprite.sprite = InventoryItemCatalog.GetIcon(recipe.OutputItemId);
        resultIconSprite.preserveAspect = true;
        resultIconSprite.raycastTarget = false;

        TMP_Text resultName = CreateText(resultInner.transform, "RecipeResultName", InventoryItemCatalog.GetDisplayName(recipe.OutputItemId).ToUpperInvariant(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(146f, 28f), 18f, TextAlignmentOptions.Center);
        resultName.fontStyle = FontStyles.Bold;
        resultName.textWrappingMode = TextWrappingModes.Normal;
        resultName.enableAutoSizing = true;
        resultName.fontSizeMin = 11f;
        resultName.fontSizeMax = 18f;
        resultName.overflowMode = TextOverflowModes.Truncate;

        TMP_Text resultType = CreateText(resultInner.transform, "RecipeResultType", InventoryItemCatalog.GetCategoryLabel(recipe.OutputItemId), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Vector2(146f, 22f), 15f, TextAlignmentOptions.Center);
        resultType.fontStyle = FontStyles.Bold;
        resultType.color = new Color(0.92f, 0.96f, 1f, 0.96f);

        TMP_Text resultPrice = CreateText(resultInner.transform, "RecipeResultPrice", InventoryItemCatalog.GetSellValueAstrons(recipe.OutputItemId) + " Astrons", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -158f), new Vector2(146f, 22f), 13f, TextAlignmentOptions.Center);
        resultPrice.fontStyle = FontStyles.Normal;
        resultPrice.color = new Color(0.94f, 0.98f, 1f, 0.96f);

        float ingredientStartX = 16f;
        for (int i = 0; i < recipe.Inputs.Length; i++)
        {
            string itemId = recipe.Inputs[i];
            GameObject ingredientObject = new GameObject("RecipeIngredient_" + i, typeof(RectTransform), typeof(Image));
            ingredientObject.transform.SetParent(rowObject.transform, false);

            RectTransform ingredientRect = ingredientObject.GetComponent<RectTransform>();
            ingredientRect.anchorMin = new Vector2(0.5f, 1f);
            ingredientRect.anchorMax = new Vector2(0.5f, 1f);
            ingredientRect.pivot = new Vector2(0.5f, 1f);
            ingredientRect.anchoredPosition = new Vector2(ingredientStartX + (i * 110f), -34f);
            ingredientRect.sizeDelta = new Vector2(104f, 104f);

            Image ingredientBg = ingredientObject.GetComponent<Image>();
            ingredientBg.color = InventoryItemCatalog.GetRarityColor(itemId);

            GameObject ingredientIconObject = new GameObject("IngredientIcon", typeof(RectTransform), typeof(Image));
            ingredientIconObject.transform.SetParent(ingredientObject.transform, false);
            RectTransform ingredientIconRect = ingredientIconObject.GetComponent<RectTransform>();
            ingredientIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            ingredientIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            ingredientIconRect.pivot = new Vector2(0.5f, 0.5f);
            ingredientIconRect.anchoredPosition = new Vector2(0f, -8f);
            ingredientIconRect.sizeDelta = new Vector2(68f, 68f);

            Image ingredientIcon = ingredientIconObject.GetComponent<Image>();
            ingredientIcon.sprite = InventoryItemCatalog.GetIcon(itemId);
            ingredientIcon.preserveAspect = true;
            ingredientIcon.raycastTarget = false;

            TMP_Text ingredientLabel = CreateText(ingredientObject.transform, "IngredientLabel", InventoryItemCatalog.GetShortLabel(itemId), Vector2.zero, Vector2.one, new Vector2(0f, 26f), Vector2.zero, 15f, TextAlignmentOptions.Bottom);
            ingredientLabel.fontStyle = FontStyles.Bold;
            ingredientLabel.raycastTarget = false;
        }

        return rowObject;
    }

    void CreateRecipeArrow(Transform parent, Vector2 anchoredPosition, bool pointLeft)
    {
        GameObject arrowRoot = new GameObject("RecipeArrow", typeof(RectTransform));
        arrowRoot.transform.SetParent(parent, false);

        RectTransform rootRect = arrowRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = new Vector2(82f, 56f);

        Color shadowColor = new Color(0f, 0f, 0f, 0.28f);
        Color arrowColor = new Color(0.9f, 0.94f, 0.99f, 0.98f);
        float direction = pointLeft ? -1f : 1f;

        CreateArrowSegment(arrowRoot.transform, "ShaftShadow", new Vector2(-6f * direction, -30f), new Vector2(38f, 8f), 0f, shadowColor);
        CreateArrowSegment(arrowRoot.transform, "HeadTopShadow", new Vector2(18f * direction, -21f), new Vector2(22f, 8f), -38f * direction, shadowColor);
        CreateArrowSegment(arrowRoot.transform, "HeadBottomShadow", new Vector2(18f * direction, -39f), new Vector2(22f, 8f), 38f * direction, shadowColor);

        CreateArrowSegment(arrowRoot.transform, "Shaft", new Vector2(-6f * direction, -28f), new Vector2(38f, 8f), 0f, arrowColor);
        CreateArrowSegment(arrowRoot.transform, "HeadTop", new Vector2(18f * direction, -19f), new Vector2(22f, 8f), -38f * direction, arrowColor);
        CreateArrowSegment(arrowRoot.transform, "HeadBottom", new Vector2(18f * direction, -37f), new Vector2(22f, 8f), 38f * direction, arrowColor);
    }

    void CreateArrowSegment(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, float rotationZ, Color color)
    {
        GameObject segmentObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        segmentObject.transform.SetParent(parent, false);

        RectTransform rect = segmentObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

        Image image = segmentObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    async void OnCraftingRecipeSelected(string recipeId)
    {
        if (inventoryActionInProgress || dragInProgress)
            return;

        PlayerProfileCraftingRecipe recipe = FindCraftingRecipe(recipeId);
        if (recipe == null)
            return;

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null)
            return;

        PlayerInventoryData workingInventory = profile.Inventory != null ? profile.Inventory.Clone() : PlayerInventoryData.Default();
        if (!TryPrepareCraftingRecipe(workingInventory, recipe, out string failureMessage))
        {
            SetStatus(string.IsNullOrWhiteSpace(failureMessage) ? "Missing ingredients for this recipe." : failureMessage);
            return;
        }

        inventoryActionInProgress = true;
        SetInteractable(false);

        try
        {
            await PlayerProfileService.Instance.SaveInventorySnapshotAsync(workingInventory);
            HideCraftingRecipeBrowser();
            HideItemPreview();
            RefreshView();
            SetStatus("Crafting slots prepared for " + InventoryItemCatalog.GetDisplayName(recipe.OutputItemId) + ".");
        }
        catch (Exception ex)
        {
            Debug.LogError("Crafting recipe preparation failed: " + ex);
            SetStatus("Could not prepare crafting recipe.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshCraftingRecipeBrowser();
        }
    }

    PlayerProfileCraftingRecipe FindCraftingRecipe(string recipeId)
    {
        IReadOnlyList<PlayerProfileCraftingRecipe> recipes = PlayerProfileCraftingCatalog.GetAllRecipes();
        if (recipes == null || string.IsNullOrWhiteSpace(recipeId))
            return null;

        for (int i = 0; i < recipes.Count; i++)
        {
            if (recipes[i] != null && string.Equals(recipes[i].Id, recipeId, StringComparison.Ordinal))
                return recipes[i];
        }

        return null;
    }

    bool CanPrepareCraftingRecipe(PlayerInventoryData inventory, PlayerProfileCraftingRecipe recipe)
    {
        if (inventory == null || recipe == null || recipe.Inputs == null || recipe.Inputs.Length == 0)
            return false;

        if (recipe.Inputs.Length > PlayerInventoryData.CraftingSlotCount)
            return false;

        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        inventory.Normalize();

        CountCombinedInventoryItems(inventory.PlayerSlots, counts);
        CountCombinedInventoryItems(inventory.ShipSlots, counts);
        CountCombinedInventoryItems(inventory.CraftingSlots, counts);

        for (int i = 0; i < recipe.Inputs.Length; i++)
        {
            string itemId = recipe.Inputs[i];
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            if (!counts.TryGetValue(itemId, out int currentCount) || currentCount <= 0)
                return false;

            counts[itemId] = currentCount - 1;
        }

        return true;
    }

    void CountCombinedInventoryItems(string[] slots, Dictionary<string, int> counts)
    {
        if (slots == null || counts == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            string itemId = slots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            counts.TryGetValue(itemId, out int currentCount);
            counts[itemId] = currentCount + 1;
        }
    }

    bool TryPrepareCraftingRecipe(PlayerInventoryData inventory, PlayerProfileCraftingRecipe recipe, out string failureMessage)
    {
        failureMessage = null;

        if (inventory == null || recipe == null || recipe.Inputs == null || recipe.Inputs.Length == 0)
        {
            failureMessage = "Crafting recipe is unavailable.";
            return false;
        }

        if (recipe.Inputs.Length > PlayerInventoryData.CraftingSlotCount)
        {
            failureMessage = "Recipe uses more crafting slots than are available.";
            return false;
        }

        inventory.Normalize();

        List<(bool isPlayerInventory, int slotIndex, string itemId)> sourcePlan = new List<(bool, int, string)>();
        if (!TryBuildCraftingSourcePlan(inventory, recipe, sourcePlan))
        {
            failureMessage = "Missing ingredients in player, ship or crafting inventory.";
            return false;
        }

        for (int i = 0; i < sourcePlan.Count; i++)
        {
            var entry = sourcePlan[i];
            string removed;
            if (entry.isPlayerInventory)
            {
                removed = inventory.RemoveFromPlayer(entry.slotIndex);
            }
            else if (entry.slotIndex < 0)
            {
                removed = inventory.RemoveFromCrafting(~entry.slotIndex);
            }
            else
            {
                removed = inventory.RemoveFromShip(entry.slotIndex);
            }

            if (string.IsNullOrWhiteSpace(removed))
            {
                failureMessage = "Could not reserve ingredients for crafting.";
                return false;
            }
        }

        List<string> previousCraftingItems = new List<string>(PlayerInventoryData.CraftingSlotCount);
        for (int i = 0; i < PlayerInventoryData.CraftingSlotCount; i++)
        {
            string itemId = inventory.RemoveFromCrafting(i);
            if (!string.IsNullOrWhiteSpace(itemId))
                previousCraftingItems.Add(itemId);
        }

        for (int i = 0; i < PlayerInventoryData.CraftingSlotCount; i++)
            inventory.SetCrafting(i, i < recipe.Inputs.Length ? recipe.Inputs[i] : null);

        int shipCapacity = ShipCatalog.GetShipInventoryCapacity(selectedSkin);
        for (int i = 0; i < previousCraftingItems.Count; i++)
        {
            string itemId = previousCraftingItems[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (inventory.TryAddToPlayer(itemId))
                continue;

            if (inventory.TryAddToShip(itemId, shipCapacity))
                continue;

            failureMessage = "Not enough free inventory space to clear current crafting slots.";
            return false;
        }

        return true;
    }

    bool TryBuildCraftingSourcePlan(PlayerInventoryData inventory, PlayerProfileCraftingRecipe recipe, List<(bool isPlayerInventory, int slotIndex, string itemId)> sourcePlan)
    {
        if (inventory == null || recipe == null || recipe.Inputs == null || sourcePlan == null)
            return false;

        bool[] usedPlayerSlots = new bool[inventory.PlayerSlots.Length];
        bool[] usedShipSlots = new bool[inventory.ShipSlots.Length];
        bool[] usedCraftingSlots = new bool[inventory.CraftingSlots.Length];

        for (int i = 0; i < recipe.Inputs.Length; i++)
        {
            string requiredItemId = recipe.Inputs[i];
            if (string.IsNullOrWhiteSpace(requiredItemId))
                return false;

            bool found = false;

            for (int slotIndex = 0; slotIndex < inventory.PlayerSlots.Length; slotIndex++)
            {
                if (usedPlayerSlots[slotIndex] || !string.Equals(inventory.PlayerSlots[slotIndex], requiredItemId, StringComparison.Ordinal))
                    continue;

                usedPlayerSlots[slotIndex] = true;
                sourcePlan.Add((true, slotIndex, requiredItemId));
                found = true;
                break;
            }

            if (found)
                continue;

            int shipCapacity = ShipCatalog.GetShipInventoryCapacity(selectedSkin);
            for (int slotIndex = 0; slotIndex < inventory.ShipSlots.Length && slotIndex < shipCapacity; slotIndex++)
            {
                if (usedShipSlots[slotIndex] || !string.Equals(inventory.ShipSlots[slotIndex], requiredItemId, StringComparison.Ordinal))
                    continue;

                usedShipSlots[slotIndex] = true;
                sourcePlan.Add((false, slotIndex, requiredItemId));
                found = true;
                break;
            }

            if (found)
                continue;

            for (int slotIndex = 0; slotIndex < inventory.CraftingSlots.Length; slotIndex++)
            {
                if (usedCraftingSlots[slotIndex] || !string.Equals(inventory.CraftingSlots[slotIndex], requiredItemId, StringComparison.Ordinal))
                    continue;

                usedCraftingSlots[slotIndex] = true;
                sourcePlan.Add((false, ~slotIndex, requiredItemId));
                found = true;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }

    Button CreateEquipmentSlotButton(Transform parent, string name, Vector2 anchoredPosition, int slotIndex, string label, out TMP_Text text, out Image icon)
    {
        GameObject slotObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        slotObject.transform.SetParent(parent, false);
        RectTransform rect = slotObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(120f, 120f);

        Image bg = slotObject.GetComponent<Image>();
        bg.color = new Color(0.17f, 0.22f, 0.28f, 0.88f);

        Button button = slotObject.GetComponent<Button>();
        button.onClick.AddListener(() => OnEquipmentSlotClicked(slotIndex));

        ProfileEquipmentSlotDragHandler dragHandler = slotObject.AddComponent<ProfileEquipmentSlotDragHandler>();
        dragHandler.owner = this;
        dragHandler.slotIndex = slotIndex;

        GameObject iconObject = new GameObject(name + "Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(slotObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(86f, 86f);
        icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.enabled = false;

        text = CreateText(slotObject.transform, name + "Text", label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 12f, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.margin = new Vector4(4f, 4f, 4f, 4f);
        return button;
    }

    void CreateInventoryGrid(Transform parent, bool isPlayerInventory, Vector2 startPosition, int slotCount, int columns, out Button[] buttons, out TMP_Text[] labels, out Image[] icons)
    {
        buttons = new Button[slotCount];
        labels = new TMP_Text[slotCount];
        icons = new Image[slotCount];

        const float slotSize = 120f;
        const float slotSpacing = 12f;

        for (int index = 0; index < slotCount; index++)
        {
            int row = index / columns;
            int col = index % columns;
            Vector2 position = new Vector2(
                startPosition.x + col * (slotSize + slotSpacing),
                startPosition.y - row * (slotSize + slotSpacing));

            buttons[index] = CreateInventorySlot(parent, (isPlayerInventory ? "PlayerSlot" : "ShipSlot") + index, position, new Vector2(slotSize, slotSize), isPlayerInventory, index, out labels[index], out icons[index]);
        }
    }

    void RebuildPlayerInventoryGrid(int slotCount)
    {
        if (panelObject == null)
            return;

        int safeSlotCount = Mathf.Max(PlayerInventoryData.DefaultPlayerSlotCount, slotCount);
        if (builtPlayerInventorySlotCount == safeSlotCount && playerInventoryButtons != null)
            return;

        if (playerInventoryScrollRect != null)
        {
            Destroy(playerInventoryScrollRect.gameObject);
            playerInventoryScrollRect = null;
            playerInventoryContentRect = null;
        }

        if (playerInventoryScrollbarObject != null)
        {
            Destroy(playerInventoryScrollbarObject);
            playerInventoryScrollbarObject = null;
        }

        CreateScrollablePlayerInventoryGrid(
            panelObject.transform,
            PlayerInventoryGridPosition,
            PlayerInventoryViewportSize,
            safeSlotCount,
            PlayerInventoryGridColumns,
            out playerInventoryButtons,
            out playerInventoryTexts,
            out playerInventoryIcons);
        PlacePlayerInventoryGridInHierarchy();
        builtPlayerInventorySlotCount = safeSlotCount;
    }

    void PlacePlayerInventoryGridInHierarchy()
    {
        if (itemPreviewPanelObject == null || playerInventoryScrollRect == null)
            return;

        int targetIndex = itemPreviewPanelObject.transform.GetSiblingIndex();
        playerInventoryScrollRect.transform.SetSiblingIndex(targetIndex);
        if (playerInventoryScrollbarObject != null)
            playerInventoryScrollbarObject.transform.SetSiblingIndex(targetIndex + 1);
    }

    void CreateScrollablePlayerInventoryGrid(Transform parent, Vector2 anchoredPosition, Vector2 viewportSize, int slotCount, int columns, out Button[] buttons, out TMP_Text[] labels, out Image[] icons)
    {
        buttons = new Button[slotCount];
        labels = new TMP_Text[slotCount];
        icons = new Image[slotCount];

        const float slotSize = 120f;
        const float slotSpacing = 12f;
        int rows = Mathf.CeilToInt(slotCount / (float)columns);

        GameObject viewportObject = new GameObject("PlayerInventoryViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(parent, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 1f);
        viewportRect.anchorMax = new Vector2(0.5f, 1f);
        viewportRect.pivot = new Vector2(0f, 1f);
        viewportRect.anchoredPosition = anchoredPosition;
        viewportRect.sizeDelta = viewportSize;

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.08f, 0.11f, 0.15f, 0.26f);
        viewportImage.raycastTarget = true;

        GameObject contentObject = new GameObject("PlayerInventoryContent", typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);
        playerInventoryContentRect = contentObject.GetComponent<RectTransform>();
        playerInventoryContentRect.anchorMin = new Vector2(0f, 1f);
        playerInventoryContentRect.anchorMax = new Vector2(0f, 1f);
        playerInventoryContentRect.pivot = new Vector2(0f, 1f);
        playerInventoryContentRect.anchoredPosition = Vector2.zero;
        playerInventoryContentRect.sizeDelta = new Vector2(
            columns * slotSize + (columns - 1) * slotSpacing,
            rows * slotSize + (rows - 1) * slotSpacing);

        GameObject scrollbarObject = new GameObject("PlayerInventoryScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarObject.transform.SetParent(parent, false);
        playerInventoryScrollbarObject = scrollbarObject;
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(0.5f, 1f);
        scrollbarRect.anchorMax = new Vector2(0.5f, 1f);
        scrollbarRect.pivot = new Vector2(0f, 1f);
        scrollbarRect.anchoredPosition = new Vector2(anchoredPosition.x - 56f, anchoredPosition.y);
        scrollbarRect.sizeDelta = new Vector2(44f, viewportSize.y);

        Image scrollbarBg = scrollbarObject.GetComponent<Image>();
        scrollbarBg.color = new Color(0.1f, 0.14f, 0.18f, 0.88f);

        GameObject slidingAreaObject = new GameObject("Sliding Area", typeof(RectTransform));
        slidingAreaObject.transform.SetParent(scrollbarObject.transform, false);
        RectTransform slidingAreaRect = slidingAreaObject.GetComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(4f, 4f);
        slidingAreaRect.offsetMax = new Vector2(-4f, -4f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(slidingAreaObject.transform, false);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 1f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.pivot = new Vector2(0.5f, 1f);
        handleRect.sizeDelta = new Vector2(0f, 96f);

        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(0.23f, 0.74f, 0.62f, 0.95f);

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;

        playerInventoryScrollRect = viewportObject.GetComponent<ScrollRect>();
        playerInventoryScrollRect.horizontal = false;
        playerInventoryScrollRect.vertical = true;
        playerInventoryScrollRect.movementType = ScrollRect.MovementType.Clamped;
        playerInventoryScrollRect.viewport = viewportRect;
        playerInventoryScrollRect.content = playerInventoryContentRect;
        playerInventoryScrollRect.verticalScrollbar = scrollbar;
        playerInventoryScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        playerInventoryScrollRect.scrollSensitivity = 32f;

        for (int index = 0; index < slotCount; index++)
        {
            int row = index / columns;
            int col = index % columns;
            Vector2 position = new Vector2(col * (slotSize + slotSpacing), -row * (slotSize + slotSpacing));
            buttons[index] = CreateInventorySlotTopLeft(
                playerInventoryContentRect,
                "PlayerSlot" + index,
                position,
                new Vector2(slotSize, slotSize),
                true,
                index,
                out labels[index],
                out icons[index]);
        }
    }

    Button CreateInventorySlot(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, bool isPlayerInventory, int slotIndex, out TMP_Text label, out Image icon)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.16f, 0.21f, 0.96f);

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() => OnInventorySlotClicked(isPlayerInventory, slotIndex));

        ProfileInventorySlotDragHandler dragHandler = buttonObject.AddComponent<ProfileInventorySlotDragHandler>();
        dragHandler.owner = this;
        dragHandler.isPlayerInventory = isPlayerInventory;
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

    Button CreateInventorySlotTopLeft(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, bool isPlayerInventory, int slotIndex, out TMP_Text label, out Image icon)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.16f, 0.21f, 0.96f);

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() => OnInventorySlotClicked(isPlayerInventory, slotIndex));

        ProfileInventorySlotDragHandler dragHandler = buttonObject.AddComponent<ProfileInventorySlotDragHandler>();
        dragHandler.owner = this;
        dragHandler.isPlayerInventory = isPlayerInventory;
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

    Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, Vector2 size, Action onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.2f, 0.27f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            onClick?.Invoke();
        });

        TMP_Text text = CreateText(buttonObject.transform, objectName + "Text", label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 18f, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        if (objectName.StartsWith("ShipSkinButton", StringComparison.Ordinal))
        {
            text.fontSize = 15f;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.margin = new Vector4(6f, 4f, 6f, 4f);
        }

        return button;
    }

    void StyleButton(Button button, Color normalColor, Color highlightedColor)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = normalColor;

        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.selectedColor = highlightedColor;
        colors.highlightedColor = highlightedColor;
        colors.pressedColor = Color.Lerp(highlightedColor, Color.black, 0.15f);
        colors.disabledColor = new Color(0.26f, 0.28f, 0.31f, 0.8f);
        button.colors = colors;
    }

    TMP_Text CreateText(Transform parent, string objectName, string value, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.offsetMin = sizeDelta == Vector2.zero ? Vector2.zero : rect.offsetMin;
        rect.offsetMax = sizeDelta == Vector2.zero ? Vector2.zero : rect.offsetMax;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.94f, 0.97f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontStyle = FontStyles.Bold;

        TMP_Text reference = FindAnyObjectByType<TextMeshProUGUI>();
        if (reference != null)
        {
            text.font = reference.font;
            text.fontSharedMaterial = reference.fontSharedMaterial;
        }

        return text;
    }

    void OnSaveAndRunClicked()
    {
        if (nicknameInput == null)
            return;

        if (IsCraftingGridOccupied())
        {
            SetStatus("Empty crafting slots before starting.");
            return;
        }

        SetStatus("Preparing lobby...");
        SetInteractable(false);

        try
        {
            PlayerProfileService.Instance.SaveProfileLocally(nicknameInput.text, selectedSkin);
            SetStatus("Loading active rounds...");
            SessionBrowserPanelUI.ShowBrowser();
            NetworkManager.RequestSessionStart();
            _ = SaveProfileToCloudAfterPlayAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError("Profile save failed: " + ex);
            SetStatus("Save failed");
            SetInteractable(true);
        }
    }

    async Task SaveProfileToCloudAfterPlayAsync()
    {
        try
        {
            await PlayerProfileService.Instance.SaveCurrentProfileToCloudAsync();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("PLAY clicked: background cloud sync failed: " + ex.Message);
            if (currentScreen == ProfileScreen.Home && !SessionBrowserPanelUI.IsVisible)
                SetStatus("Cloud save delayed. Local profile kept.");
        }
    }

    void RefreshView()
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null)
            return;

        if (profile.Inventory != null)
            profile.Inventory.Normalize();

        selectedSkin = Mathf.Clamp(profile.ShipSkinIndex, 0, ShipCatalog.MaxShipSkinIndex);

        if (nicknameInput != null && !nicknameInput.isFocused)
        {
            nicknameInput.text = profile.Nickname;
        }

        if (gamesPlayedText != null)
        {
            gamesPlayedText.text = "Games: " + profile.GamesPlayed;
        }

        if (totalXpText != null)
        {
            totalXpText.text = "Level: " + RoundXpBalance.GetLevelForTotalXp(profile.TotalXp) + "  XP: " + profile.TotalXp;
        }

        if (astronsText != null)
        {
            astronsText.text = "Astrons: " + profile.Astrons;
        }

        if (accountText != null)
        {
            string playerId = PlayerProfileService.Instance.PlayerId;
            if (string.IsNullOrWhiteSpace(playerId))
            {
                accountText.text = PlayerProfileService.Instance.IsInitialized ? "Cloud linked" : "Connecting...";
            }
            else
            {
                string suffix = playerId.Length <= 8 ? playerId : playerId.Substring(playerId.Length - 8);
                accountText.text = "ID: " + suffix.ToUpperInvariant();
            }
        }

        UpdateShipTypeButtonVisuals();
        UpdateSkinButtonsForSelectedShip();
        UpdateSkinButtonVisuals();
        ApplySaveAndRunButtonStyle();
        RefreshShipPreview();
        RefreshInventoryView(profile.Inventory);
        if (craftingRecipeBrowserObject != null && craftingRecipeBrowserObject.activeSelf)
            RefreshCraftingRecipeBrowser();
        if (shopBrowserObject != null && shopBrowserObject.activeSelf)
            RefreshShopBrowser();
        if (cheatBrowserObject != null && cheatBrowserObject.activeSelf)
            RefreshCheatBrowser();
    }

    ShipType GetSelectedShipType()
    {
        return ShipCatalog.GetShipTypeFromSkinIndex(selectedSkin);
    }

    async void SetSelectedShipType(ShipType shipType)
    {
        int[] allowedSkins = ShipCatalog.GetSkinsForShipType(shipType);
        int targetSkin = System.Array.IndexOf(allowedSkins, selectedSkin) >= 0 ? selectedSkin : allowedSkins[0];
        if (inventoryActionInProgress)
            return;

        inventoryActionInProgress = true;
        SetInteractable(false);
        SetStatus("Switching ship...");

        try
        {
            bool changed = await PlayerProfileService.Instance.TryChangeShipSkinAsync(targetSkin);
            if (!changed)
            {
                SetStatus("No room in player inventory for extra cargo.");
                RefreshView();
                return;
            }

            selectedSkin = targetSkin;
            RefreshView();
            SetStatus("Ship changed.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Ship switch failed: " + ex);
            SetStatus("Ship change failed.");
            RefreshView();
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
        }
    }

    void OnExitGameClicked()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ApplySkinChoiceByButtonIndex(int buttonIndex)
    {
        int[] allowedSkins = ShipCatalog.GetSkinsForShipType(GetSelectedShipType());
        if (buttonIndex < 0 || buttonIndex >= allowedSkins.Length)
            return;

        selectedSkin = allowedSkins[buttonIndex];
        UpdateShipTypeButtonVisuals();
        UpdateSkinButtonsForSelectedShip();
        UpdateSkinButtonVisuals();
        RefreshShipPreview();
    }

    void UpdateShipTypeButtonVisuals()
    {
        if (shipTypeButtons == null)
            return;

        ShipType selectedType = GetSelectedShipType();
        for (int i = 0; i < shipTypeButtons.Length; i++)
        {
            if (shipTypeButtons[i] == null)
                continue;

            Image image = shipTypeButtons[i].GetComponent<Image>();
            if (image != null)
            {
                bool isSelected = i < SelectableShipTypes.Length && SelectableShipTypes[i] == selectedType;
                image.color = isSelected
                    ? new Color(0.19f, 0.61f, 0.5f, 0.98f)
                    : new Color(0.16f, 0.2f, 0.27f, 0.95f);
            }
        }
    }

    void UpdateSkinButtonsForSelectedShip()
    {
        if (skinButtons == null)
            return;

        ShipType shipType = GetSelectedShipType();
        int[] allowedSkins = ShipCatalog.GetSkinsForShipType(shipType);

        if (shipTypeLabelText != null)
            shipTypeLabelText.text = "SHIP: " + ShipCatalog.GetShipTypeDisplayName(shipType).ToUpperInvariant();

        if (shipSkinLabelText != null)
            shipSkinLabelText.text = "SHIP SKIN (" + ShipCatalog.GetShipTypeDisplayName(shipType).ToUpperInvariant() + ")";

        for (int i = 0; i < skinButtons.Length; i++)
        {
            if (skinButtons[i] == null)
                continue;

            bool active = i < allowedSkins.Length;
            skinButtons[i].gameObject.SetActive(active);
            if (!active)
                continue;

            TMP_Text text = skinButtons[i].GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = ShipCatalog.GetSkinDisplayName(allowedSkins[i]).ToUpperInvariant();
        }
    }

    void UpdateSkinButtonVisuals()
    {
        if (skinButtons == null)
            return;

        int[] allowedSkins = ShipCatalog.GetSkinsForShipType(GetSelectedShipType());

        for (int i = 0; i < skinButtons.Length; i++)
        {
            if (skinButtons[i] == null)
                continue;

            if (i >= allowedSkins.Length)
                continue;

            Image image = skinButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = allowedSkins[i] == selectedSkin
                    ? new Color(0.19f, 0.61f, 0.5f, 0.98f)
                    : new Color(0.16f, 0.2f, 0.27f, 0.95f);
            }
        }
    }

    void ApplySaveAndRunButtonStyle()
    {
        if (saveAndRunButton == null)
            return;

        Image image = saveAndRunButton.GetComponent<Image>();
        if (image != null)
            image.raycastTarget = true;

        TMP_Text text = saveAndRunButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = "PLAY";
        }

        UIRuntimeStyler.RefreshStyles();
    }

    void ApplyItemPreviewLayout()
    {
        if (itemPreviewPanelObject == null)
            return;

        RectTransform rect = itemPreviewPanelObject.GetComponent<RectTransform>();
        if (rect == null)
            return;

        Transform targetParent = panelObject.transform;
        Vector2 anchoredPosition = new Vector2(-366f, -108f);
        Vector2 size = new Vector2(304f, 326f);

        if (currentScreen == ProfileScreen.Crafting && craftingViewRootObject != null)
        {
            targetParent = craftingViewRootObject.transform;
            anchoredPosition = new Vector2(-24f, -158f);
            size = new Vector2(304f, 326f);
        }
        else if (currentScreen == ProfileScreen.Inventory && inventoryViewRootObject != null)
        {
            targetParent = inventoryViewRootObject.transform;
            anchoredPosition = new Vector2(-154f, -182f);
            size = new Vector2(304f, 326f);
        }
        else if (currentScreen == ProfileScreen.Trader && traderViewRootObject != null)
        {
            targetParent = traderViewRootObject.transform;
            anchoredPosition = new Vector2(-204f, -132f);
            size = new Vector2(304f, 326f);
        }

        if (itemPreviewPanelObject.transform.parent != targetParent)
            itemPreviewPanelObject.transform.SetParent(targetParent, false);

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    void UpdateEquipmentSlotLayout()
    {
        if (equipmentSlotRects == null || equipmentSlotRects.Length < PlayerInventoryData.EquipmentSlotCount)
            return;

        for (int i = 0; i < equipmentSlotRects.Length && i < EquipmentSlotLayoutPositions.Length; i++)
        {
            RectTransform rect = equipmentSlotRects[i];
            if (rect == null)
                continue;

            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = EquipmentSlotLayoutPositions[i];
            rect.sizeDelta = new Vector2(EquipmentSlotPreviewSize, EquipmentSlotPreviewSize);
        }
    }

    void LayoutEquipmentSlotsColumn(float leftX, float rightX, float topY, float rowSpacing, float slotSize)
    {
        if (equipmentSlotRects == null || equipmentSlotRects.Length < PlayerInventoryData.EquipmentSlotCount)
            return;

        int[][] rowOrder =
        {
            new[] { 0, 1 },
            new[] { 2, 3 },
            new[] { 6, 7 },
            new[] { 4, 5 }
        };

        for (int row = 0; row < rowOrder.Length; row++)
        {
            for (int col = 0; col < rowOrder[row].Length; col++)
            {
                int slotIndex = rowOrder[row][col];
                if (slotIndex < 0 || slotIndex >= equipmentSlotRects.Length)
                    continue;

                RectTransform rect = equipmentSlotRects[slotIndex];
                if (rect == null)
                    continue;

                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(col == 0 ? leftX : rightX, topY - (row * rowSpacing));
                rect.sizeDelta = new Vector2(slotSize, slotSize);
            }
        }
    }

    void LayoutShipStatsVertical(float x, float topY, Vector2 cardSize, float spacing)
    {
        if (shipStatLabelTexts == null || shipStatValueTexts == null)
            return;

        bool largeMode = cardSize.y >= 60f;

        for (int i = 0; i < shipStatLabelTexts.Length; i++)
        {
            TMP_Text label = shipStatLabelTexts[i];
            TMP_Text value = i < shipStatValueTexts.Length ? shipStatValueTexts[i] : null;
            if (label == null)
                continue;

            RectTransform cardRect = label.transform.parent != null ? label.transform.parent.GetComponent<RectTransform>() : null;
            if (cardRect == null)
                continue;

            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = new Vector2(x, topY - (i * (cardSize.y + spacing)));
            cardRect.sizeDelta = cardSize;

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = new Vector2(14f, largeMode ? -14f : -10f);
            labelRect.sizeDelta = new Vector2(cardSize.x * 0.48f, largeMode ? 26f : 16f);
            label.fontSize = largeMode ? 25f : 13f;

            if (value != null)
            {
                RectTransform valueRect = value.rectTransform;
                valueRect.anchorMin = new Vector2(1f, 1f);
                valueRect.anchorMax = new Vector2(1f, 1f);
                valueRect.pivot = new Vector2(1f, 1f);
                valueRect.anchoredPosition = new Vector2(-12f, largeMode ? -14f : -10f);
                valueRect.sizeDelta = new Vector2(cardSize.x * 0.44f, largeMode ? 26f : 16f);
                value.fontSize = largeMode ? 25f : 13f;
            }

            Transform barBgTransform = cardRect.Find("BarBg");
            if (barBgTransform != null)
            {
                RectTransform barBgRect = barBgTransform.GetComponent<RectTransform>();
                if (barBgRect != null)
                {
                    barBgRect.anchorMin = new Vector2(0f, 0f);
                    barBgRect.anchorMax = new Vector2(1f, 0f);
                    barBgRect.pivot = new Vector2(0.5f, 0f);
                    barBgRect.anchoredPosition = new Vector2(0f, largeMode ? 12f : 8f);
                    barBgRect.sizeDelta = new Vector2(-20f, largeMode ? 18f : 12f);
                }
            }
        }
    }

    void RefreshShipPreview()
    {
        PlayerShipDefinition definition = ShipCatalog.GetShipDefinition(selectedSkin);
        if (shipPreviewTitleText != null)
        {
            shipPreviewTitleText.text = definition.DisplayName.ToUpperInvariant();
        }

        RefreshShipStatCards(definition);

        UpdateEquipmentSlotLayout();

        if (shipPreviewImage != null)
        {
            shipPreviewImage.sprite = LoadShipPreviewSprite(selectedSkin);
            shipPreviewImage.color = shipPreviewImage.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        if (shipPreviewButton != null)
            shipPreviewButton.interactable = shipPreviewImage != null && shipPreviewImage.sprite != null && !inventoryActionInProgress;

        if (shipImageModalObject != null && shipImageModalObject.activeSelf && shipImageModalImage != null)
        {
            shipImageModalImage.sprite = shipPreviewImage != null ? shipPreviewImage.sprite : null;
            shipImageModalImage.color = shipImageModalImage.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        RefreshEquipmentSlotPreview();
    }

    void RefreshShipStatCards(PlayerShipDefinition definition)
    {
        if (definition == null || shipStatLabelTexts == null || shipStatValueTexts == null || shipStatFillImages == null)
            return;

        SetShipStatCard(0, ShipStatLabels[0], definition.BaseHp.ToString(), NormalizeShipStat(definition.BaseHp, stat => stat.BaseHp));
        SetShipStatCard(1, ShipStatLabels[1], definition.BaseShield.ToString(), NormalizeShipStat(definition.BaseShield, stat => stat.BaseShield));
        SetShipStatCard(2, ShipStatLabels[2], definition.BaseSpeed.ToString("0.0"), NormalizeShipStat(definition.BaseSpeed, stat => stat.BaseSpeed));
        SetShipStatCard(3, ShipStatLabels[3], "x" + definition.TurnRateMultiplier.ToString("0.00"), NormalizeShipStat(definition.TurnRateMultiplier, stat => stat.TurnRateMultiplier));
        SetShipStatCard(4, ShipStatLabels[4], definition.BoosterDuration.ToString("0.0") + "s", NormalizeShipStat(definition.BoosterDuration, stat => stat.BoosterDuration));
        SetShipStatCard(5, ShipStatLabels[5], "+" + definition.MaxBoostPercent + "%", NormalizeShipStat(definition.MaxBoostPercent, stat => stat.MaxBoostPercent));
        SetShipStatCard(6, ShipStatLabels[6], definition.CargoCapacity.ToString(), NormalizeShipStat(definition.CargoCapacity, stat => stat.CargoCapacity));
        SetShipStatCard(7, ShipStatLabels[7], definition.SafePocketSlots.ToString(), NormalizeSafePocketStat(definition.SafePocketSlots));
    }

    void SetShipStatCard(int index, string label, string valueText, float normalized)
    {
        if (index < 0 ||
            shipStatLabelTexts == null || index >= shipStatLabelTexts.Length ||
            shipStatValueTexts == null || index >= shipStatValueTexts.Length ||
            shipStatFillImages == null || index >= shipStatFillImages.Length)
        {
            return;
        }

        TMP_Text labelText = shipStatLabelTexts[index];
        TMP_Text value = shipStatValueTexts[index];
        Image fillImage = shipStatFillImages[index];
        if (labelText != null)
            labelText.text = label;
        if (value != null)
            value.text = valueText;
        if (fillImage != null)
        {
            float clamped = Mathf.Clamp01(normalized);
            RectTransform fillRect = fillImage.rectTransform;
            if (fillRect != null)
            {
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(clamped, 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }

            fillImage.color = EvaluateShipStatColor(clamped);
        }
    }

    float NormalizeShipStat(float value, Func<PlayerShipDefinition, float> selector)
    {
        if (selector == null)
            return 0f;

        float min = float.MaxValue;
        float max = float.MinValue;
        for (int i = 0; i < SelectableShipTypes.Length; i++)
        {
            PlayerShipDefinition definition = ShipCatalog.GetShipDefinition(SelectableShipTypes[i]);
            if (definition == null)
                continue;

            float candidate = selector(definition);
            min = Mathf.Min(min, candidate);
            max = Mathf.Max(max, candidate);
        }

        if (min >= float.MaxValue || max <= float.MinValue)
            return 0f;

        if (Mathf.Abs(max - min) <= 0.001f)
            return 1f;

        return Mathf.InverseLerp(min, max, value);
    }

    float NormalizeSafePocketStat(int safePocketSlots)
    {
        return Mathf.InverseLerp(0f, 3f, safePocketSlots);
    }

    Color EvaluateShipStatColor(float t)
    {
        t = Mathf.Clamp01(t);
        Color red = new Color(0.86f, 0.24f, 0.2f, 0.98f);
        Color orange = new Color(0.93f, 0.48f, 0.15f, 0.98f);
        Color yellow = new Color(0.94f, 0.8f, 0.2f, 0.98f);
        Color green = new Color(0.28f, 0.84f, 0.38f, 0.98f);

        if (t <= 0.33f)
            return Color.Lerp(red, orange, t / 0.33f);
        if (t <= 0.66f)
            return Color.Lerp(orange, yellow, (t - 0.33f) / 0.33f);

        return Color.Lerp(yellow, green, (t - 0.66f) / 0.34f);
    }

    async void SwitchToScreen(ProfileScreen screen, bool clearStatus = true)
    {
        if (!await TryClearCraftingSlotsBeforeLeavingAsync(screen))
            return;

        currentScreen = screen;
        if (clearStatus)
            SetStatus(string.Empty);

        HideItemPreview();
        HideCraftingRecipeBrowser();
        HideShopBrowser();
        HideShipImageModal();

        if (screen == ProfileScreen.ShipSelection)
        {
            shipSelectionCenterType = GetSelectedShipType();
            shipSelectionCenterIndex = Mathf.Clamp(Array.IndexOf(SelectableShipTypes, shipSelectionCenterType), 0, SelectableShipTypes.Length - 1);
            RefreshShipSelectionView();
        }

        ApplyProfileScreenLayout();

        if (screen == ProfileScreen.Crafting)
            RefreshCraftingRecipeBrowser();
        else if (screen == ProfileScreen.Trader)
            RefreshShopBrowser();

        UIRuntimeStyler.RefreshStyles();
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
        ConfigureEmbeddedTraderBrowser();

        bool splashShowing = splashScreenObject != null && splashHideTime > 0f && Time.unscaledTime < splashHideTime;

        bool showHome = currentScreen == ProfileScreen.Home;
        bool showInventory = currentScreen == ProfileScreen.Inventory;
        bool showCrafting = currentScreen == ProfileScreen.Crafting;
        bool showTrader = currentScreen == ProfileScreen.Trader;
        bool showShipSelection = currentScreen == ProfileScreen.ShipSelection;

        if (homeViewRootObject != null)
            homeViewRootObject.SetActive(showHome);
        if (inventoryViewRootObject != null)
            inventoryViewRootObject.SetActive(showInventory);
        if (craftingViewRootObject != null)
            craftingViewRootObject.SetActive(showCrafting);
        if (traderViewRootObject != null)
            traderViewRootObject.SetActive(showTrader);
        if (shipSelectionViewObject != null)
            shipSelectionViewObject.SetActive(showShipSelection);

        if (topBarRootObject != null)
            topBarRootObject.SetActive(!showShipSelection);
        if (leftNavigationRootObject != null)
            leftNavigationRootObject.SetActive(!showShipSelection);
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
        if (profileTitleText != null)
            profileTitleText.gameObject.SetActive(false);
        if (cheatButton != null)
            cheatButton.gameObject.SetActive(false);
        if (cheatBrowserObject != null)
            cheatBrowserObject.SetActive(false);
        if (nicknameLabelText != null)
            nicknameLabelText.gameObject.SetActive(false);
        if (shipTypeLabelText != null)
            shipTypeLabelText.gameObject.SetActive(false);
        if (shipSkinLabelText != null)
            shipSkinLabelText.gameObject.SetActive(false);
        if (inventoryHintText != null)
            inventoryHintText.gameObject.SetActive(false);
        if (shipImageModalObject != null && showShipSelection)
            shipImageModalObject.SetActive(false);

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
                panelObject.transform;
            shipWorkspaceRootObject.transform.SetParent(targetParent, false);
        }
        if (storageViewRootObject != null)
            storageViewRootObject.transform.SetParent((showInventory || showCrafting || showTrader) ? (showInventory ? inventoryViewRootObject.transform : showCrafting ? craftingViewRootObject.transform : traderViewRootObject.transform) : panelObject.transform, false);

        if (shipWorkspaceRootObject != null)
            shipWorkspaceRootObject.SetActive(showHome || showInventory || showCrafting || showTrader);
        if (storageViewRootObject != null)
            storageViewRootObject.SetActive(showInventory || showCrafting || showTrader);

        bool showStorage = showInventory || showCrafting || showTrader;
        if (shipInventoryLabelText != null)
            shipInventoryLabelText.gameObject.SetActive(showStorage);
        if (shipInventoryUnloadButton != null)
            shipInventoryUnloadButton.gameObject.SetActive(showStorage);
        if (playerInventoryLabelText != null)
            playerInventoryLabelText.gameObject.SetActive(showStorage);
        if (playerInventoryFilterButton != null)
            playerInventoryFilterButton.gameObject.SetActive(showStorage);
        if (playerInventoryExtendButton != null)
            playerInventoryExtendButton.gameObject.SetActive(showStorage);
        if (playerInventoryScrollRect != null)
            playerInventoryScrollRect.gameObject.SetActive(showStorage);
        if (playerInventoryScrollbarObject != null)
            playerInventoryScrollbarObject.SetActive(showStorage);
        if (shipInventoryButtons != null)
        {
            for (int i = 0; i < shipInventoryButtons.Length; i++)
            {
                if (shipInventoryButtons[i] != null)
                    shipInventoryButtons[i].gameObject.SetActive(showStorage);
            }
        }

        if (craftingPanelObject != null)
            craftingPanelObject.SetActive(showCrafting);
        if (craftingRecipeBrowserObject != null)
            craftingRecipeBrowserObject.SetActive(showCrafting);
        if (shopBrowserObject != null)
            shopBrowserObject.SetActive(showTrader);
        if (traderFuturePanelObject != null)
            traderFuturePanelObject.SetActive(showTrader);
        if (statusText != null)
            statusText.gameObject.SetActive(!showHome || NetworkManager.SessionRequested || !string.IsNullOrWhiteSpace(statusText.text));

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
            if (shipSelectionViewObject != null)
                shipSelectionViewObject.SetActive(false);
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
        LayoutInventoryScreen();
        LayoutCraftingScreen();
        LayoutTraderScreen();
        RefreshEquipmentSlotPreview();
        ApplyShipWorkspaceScreenMode(showHome || showInventory);
        EnsureShipPreviewBackgroundHidden();
        ApplyItemPreviewLayout();
    }

    void ApplyPanelBackgroundForCurrentScreen()
    {
        if (panelObject == null)
            return;

        Image background = panelObject.GetComponent<Image>();
        if (background == null)
            return;

        string assetName = currentScreen == ProfileScreen.Inventory
            ? "hangar1_2D_przesuniety.png"
            : "hangar1_2D.png";

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
        EnsureTopStatBanner();

        if (gamesPlayedText != null)
        {
            RectTransform rect = gamesPlayedText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(394f, -40f);
            rect.sizeDelta = new Vector2(170f, 42f);
            gamesPlayedText.fontSize = 34f;
            gamesPlayedText.enableAutoSizing = true;
            gamesPlayedText.fontSizeMin = 24f;
            gamesPlayedText.fontSizeMax = 34f;
        }

        if (totalXpText != null)
        {
            RectTransform rect = totalXpText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(592f, -40f);
            rect.sizeDelta = new Vector2(420f, 42f);
            totalXpText.fontSize = 34f;
            totalXpText.enableAutoSizing = true;
            totalXpText.fontSizeMin = 24f;
            totalXpText.fontSizeMax = 34f;
        }

        if (astronsText != null)
        {
            RectTransform rect = astronsText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(1112f, -40f);
            rect.sizeDelta = new Vector2(266f, 42f);
            astronsText.fontSize = 34f;
            astronsText.enableAutoSizing = true;
            astronsText.fontSizeMin = 24f;
            astronsText.fontSizeMax = 34f;
        }

        if (nicknameInput != null)
        {
            RectTransform rect = nicknameInput.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(34f, -42f);
            rect.sizeDelta = new Vector2(310f, 54f);
            if (nicknameInput.textComponent != null)
                nicknameInput.textComponent.fontSize = 30f;
            if (nicknameInput.placeholder is TMP_Text placeholder)
                placeholder.fontSize = 30f;
        }

        if (accountText != null)
        {
            RectTransform rect = accountText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(42f, -92f);
            rect.sizeDelta = new Vector2(360f, 28f);
            accountText.fontSize = 22f;
            accountText.alignment = TextAlignmentOptions.Left;
            accountText.enableAutoSizing = true;
            accountText.fontSizeMin = 18f;
            accountText.fontSizeMax = 22f;
        }
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
        }

        if (cheatButton != null)
        {
            RectTransform rect = cheatButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-258f, -38f);
            rect.sizeDelta = new Vector2(94f, 48f);
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

        if (showScreenButtons && navCraftingButton != null)
            SetAnchoredRect(navCraftingButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -290f), new Vector2(326f, 88f));
        if (showScreenButtons && shopButton != null)
            SetAnchoredRect(shopButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -418f), new Vector2(326f, 88f));
        if (showScreenButtons && navInventoryButton != null)
            SetAnchoredRect(navInventoryButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -546f), new Vector2(326f, 88f));

        if (showScreenButtons && navCraftingButton != null)
            SetNavigationButtonSelected(navCraftingButton, currentScreen == ProfileScreen.Crafting, new Color(0.16f, 0.3f, 0.46f, 0.98f), new Color(0.22f, 0.42f, 0.62f, 1f));
        if (showScreenButtons && shopButton != null)
            SetNavigationButtonSelected(shopButton, currentScreen == ProfileScreen.Trader, new Color(0.18f, 0.34f, 0.5f, 0.98f), new Color(0.24f, 0.46f, 0.66f, 1f));
        if (showScreenButtons && navInventoryButton != null)
            SetNavigationButtonSelected(navInventoryButton, currentScreen == ProfileScreen.Inventory, new Color(0.16f, 0.3f, 0.46f, 0.98f), new Color(0.22f, 0.42f, 0.62f, 1f));
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
            LayoutEquipmentSlotsColumn(-520f, -386f, -64f, 142f, 112f);

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
    }

    void LayoutInventoryScreen()
    {
        if (currentScreen != ProfileScreen.Inventory)
            return;

        if (storageViewRootObject != null && storageViewRootObject.activeSelf)
            LayoutCraftingStoragePanel();

        if (shipPreviewTitleText != null && shipWorkspaceRootObject != null && shipWorkspaceRootObject.activeSelf)
        {
            RectTransform rect = shipPreviewTitleText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(548f, -82f);
            rect.sizeDelta = new Vector2(620f, 44f);
            shipPreviewTitleText.fontSize = 34f;
        }

        if (shipPreviewRootRect != null && shipWorkspaceRootObject != null && shipWorkspaceRootObject.activeSelf)
        {
            shipPreviewRootRect.anchorMin = new Vector2(0.5f, 1f);
            shipPreviewRootRect.anchorMax = new Vector2(0.5f, 1f);
            shipPreviewRootRect.pivot = new Vector2(0.5f, 1f);
            shipPreviewRootRect.anchoredPosition = new Vector2(548f, -128f);
            shipPreviewRootRect.sizeDelta = new Vector2(1340f, 880f);
        }

        if (shipPreviewImage != null && shipWorkspaceRootObject != null && shipWorkspaceRootObject.activeSelf)
        {
            RectTransform imageRect = shipPreviewImage.rectTransform;
            imageRect.anchoredPosition = new Vector2(0f, 10f);
            imageRect.sizeDelta = new Vector2(980f, 756f);
        }

        Transform hitbox = shipPreviewRootRect != null ? shipPreviewRootRect.transform.Find("ShipPreviewHitbox") : null;
        if (hitbox != null && shipWorkspaceRootObject != null && shipWorkspaceRootObject.activeSelf)
        {
            RectTransform hitboxRect = hitbox.GetComponent<RectTransform>();
            if (hitboxRect != null)
                hitboxRect.sizeDelta = new Vector2(1140f, 780f);
        }

        if (shipStatsPanelObject != null && shipWorkspaceRootObject != null && shipWorkspaceRootObject.activeSelf)
        {
            RectTransform rect = shipStatsPanelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(548f, -128f);
            rect.sizeDelta = new Vector2(1340f, 880f);
        }

        LayoutEquipmentSlotsColumn(-470f, -320f, -54f, 164f, 128f);
        LayoutShipStatsVertical(486f, -52f, new Vector2(278f, 72f), 12f);

        if (equipmentSlotPreviewIcons != null)
        {
            for (int i = 0; i < equipmentSlotPreviewIcons.Length; i++)
            {
                Image icon = equipmentSlotPreviewIcons[i];
                if (icon == null)
                    continue;

                RectTransform iconRect = icon.rectTransform;
                iconRect.sizeDelta = new Vector2(112f, 112f);
            }
        }

        if (equipmentSlotPreviewTexts != null)
        {
            for (int i = 0; i < equipmentSlotPreviewTexts.Length; i++)
            {
                TMP_Text text = equipmentSlotPreviewTexts[i];
                if (text == null)
                    continue;

                text.fontSize = 22f;
                text.fontSizeMin = 12f;
                text.fontSizeMax = 22f;
            }
        }
    }

    void LayoutStoragePanel(float centerX, float playerScrollWidth = 830f)
    {
        ConfigureStorageBackdrop(false, 0f, 0f, 0f, 0f);

        if (shipInventoryLabelText != null)
            SetAnchoredRect(shipInventoryLabelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(centerX, -180f), new Vector2(420f, 24f));
        if (shipInventoryUnloadButton != null)
            SetAnchoredRect(shipInventoryUnloadButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(centerX + 334f, -180f), new Vector2(128f, 36f));

        if (shipInventoryButtons != null)
        {
            const float slotSize = 120f;
            const float slotSpacing = 12f;
            for (int i = 0; i < shipInventoryButtons.Length; i++)
            {
                if (shipInventoryButtons[i] == null)
                    continue;

                int row = i / 5;
                int col = i % 5;
                Vector2 position = new Vector2(centerX - 264f + col * (slotSize + slotSpacing), -212f - row * (slotSize + slotSpacing));
                SetAnchoredRect(shipInventoryButtons[i].GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(slotSize, slotSize));
            }
        }

        if (playerInventoryLabelText != null)
            SetAnchoredRect(playerInventoryLabelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(centerX, -508f), new Vector2(430f, 24f));
        if (playerInventoryFilterButton != null)
            SetAnchoredRect(playerInventoryFilterButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(centerX - 320f, -504f), new Vector2(166f, 36f));
        if (playerInventoryExtendButton != null)
            SetAnchoredRect(playerInventoryExtendButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(centerX + 322f, -504f), new Vector2(128f, 36f));

        if (playerInventoryScrollRect != null)
            SetAnchoredRect(playerInventoryScrollRect.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(centerX - 10f, -538f), new Vector2(playerScrollWidth, 362f));
        if (playerInventoryScrollbarObject != null)
            SetAnchoredRect(playerInventoryScrollbarObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(centerX + (playerScrollWidth * 0.5f) + 14f, -538f), new Vector2(28f, 362f));
    }

    void LayoutCraftingStoragePanel()
    {
        const float leftEdge = -1018f;
        const float playerScrollWidth = 770f;
        const float shipSlotSize = 120f;
        const float shipSlotSpacing = 12f;
        float shipWidth = (shipSlotSize * 5f) + (shipSlotSpacing * 4f);
        float shipCenterX = leftEdge + (shipWidth * 0.5f);
        float playerCenterX = leftEdge + (playerScrollWidth * 0.5f);
        float playerRightEdge = leftEdge + playerScrollWidth;
        float shipRightEdge = leftEdge + shipWidth;

        ConfigureStorageBackdrop(false, 0f, 0f, 0f, 0f);

        if (shipInventoryLabelText != null)
            SetAnchoredRect(shipInventoryLabelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(shipCenterX, -180f), new Vector2(420f, 24f));

        if (shipInventoryUnloadButton != null)
            SetAnchoredRect(shipInventoryUnloadButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(shipRightEdge - 64f, -180f), new Vector2(128f, 36f));

        if (shipInventoryButtons != null)
        {
            for (int i = 0; i < shipInventoryButtons.Length; i++)
            {
                if (shipInventoryButtons[i] == null)
                    continue;

                int row = i / 5;
                int col = i % 5;
                Vector2 position = new Vector2(leftEdge + 60f + col * (shipSlotSize + shipSlotSpacing), -212f - row * (shipSlotSize + shipSlotSpacing));
                SetAnchoredRect(shipInventoryButtons[i].GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(shipSlotSize, shipSlotSize));
            }
        }

        if (playerInventoryLabelText != null)
            SetAnchoredRect(playerInventoryLabelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerCenterX, -508f), new Vector2(430f, 24f));

        if (playerInventoryFilterButton != null)
            SetAnchoredRect(playerInventoryFilterButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(leftEdge + 83f, -504f), new Vector2(166f, 36f));

        if (playerInventoryExtendButton != null)
            SetAnchoredRect(playerInventoryExtendButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerRightEdge - 64f, -504f), new Vector2(128f, 36f));

        if (playerInventoryScrollRect != null)
            SetAnchoredRect(playerInventoryScrollRect.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerCenterX, -538f), new Vector2(playerScrollWidth, 362f));

        if (playerInventoryScrollbarObject != null)
            SetAnchoredRect(playerInventoryScrollbarObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerRightEdge + 14f, -538f), new Vector2(28f, 362f));
    }

    void ConfigureStorageBackdrop(bool visible, float centerX, float topY, float width, float height)
    {
        if (storageViewRootObject == null)
            return;

        Transform existing = storageViewRootObject.transform.Find("StorageBackdrop");
        Image backdrop = existing != null
            ? existing.GetComponent<Image>()
            : null;

        if (backdrop == null)
        {
            GameObject backdropObject = new GameObject("StorageBackdrop", typeof(RectTransform), typeof(Image));
            backdropObject.transform.SetParent(storageViewRootObject.transform, false);
            backdropObject.transform.SetAsFirstSibling();
            backdrop = backdropObject.GetComponent<Image>();
            backdrop.raycastTarget = false;
        }

        backdrop.gameObject.SetActive(visible);
        if (!visible)
            return;

        RectTransform rect = backdrop.rectTransform;
        SetAnchoredRect(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(centerX, topY), new Vector2(width, height));
        backdrop.color = new Color(0.05f, 0.08f, 0.12f, 0.92f);
        backdrop.transform.SetAsFirstSibling();
    }

    void LayoutCraftingScreen()
    {
        if (currentScreen != ProfileScreen.Crafting)
            return;

        if (storageViewRootObject != null && storageViewRootObject.activeSelf)
            LayoutCraftingStoragePanel();

        LayoutAmbientShipBackdrop();
        if (shipWorkspaceRootObject != null)
            shipWorkspaceRootObject.transform.SetAsFirstSibling();
        if (craftingViewRootObject != null)
            craftingViewRootObject.transform.SetAsLastSibling();
        if (storageViewRootObject != null)
            storageViewRootObject.transform.SetAsLastSibling();
        if (craftingRecipeBrowserObject != null)
            craftingRecipeBrowserObject.transform.SetAsLastSibling();
        if (craftingPanelObject != null)
            craftingPanelObject.transform.SetAsLastSibling();

        if (craftingPanelObject != null)
        {
            RectTransform rect = craftingPanelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(-18f, -470f);
            rect.sizeDelta = new Vector2(360f, 492f);
        }
    }

    void ConfigureEmbeddedCraftingRecipeBrowser()
    {
        if (craftingRecipeBrowserObject == null)
            return;

        Image overlay = craftingRecipeBrowserObject.GetComponent<Image>();
        if (overlay != null)
        {
            overlay.color = new Color(0f, 0f, 0f, 0f);
            overlay.raycastTarget = false;
        }

        Transform panel = craftingRecipeBrowserObject.transform.Find("CraftingRecipeBrowserPanel");
        if (panel != null)
        {
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(566f, -172f);
            panelRect.sizeDelta = new Vector2(812f, 698f);
        }

        TMP_Text title = craftingRecipeBrowserObject.transform.Find("CraftingRecipeBrowserPanel/CraftingRecipeBrowserTitle")?.GetComponent<TMP_Text>();
        if (title != null)
            title.rectTransform.anchoredPosition = new Vector2(0f, 18f);

        Transform hint = craftingRecipeBrowserObject.transform.Find("CraftingRecipeBrowserPanel/CraftingRecipeBrowserHint");
        if (hint != null)
            hint.gameObject.SetActive(false);

        RectTransform viewportRect = craftingRecipeBrowserObject.transform.Find("CraftingRecipeBrowserPanel/CraftingRecipeViewport")?.GetComponent<RectTransform>();
        if (viewportRect != null)
        {
            viewportRect.anchoredPosition = new Vector2(-22f, -6f);
            viewportRect.sizeDelta = new Vector2(708f, 610f);
        }

        RectTransform scrollbarRect = craftingRecipeBrowserObject.transform.Find("CraftingRecipeBrowserPanel/CraftingRecipeScrollbar")?.GetComponent<RectTransform>();
        if (scrollbarRect != null)
        {
            scrollbarRect.anchoredPosition = new Vector2(372f, -6f);
            scrollbarRect.sizeDelta = new Vector2(28f, 610f);
        }

        if (craftingRecipeCloseButton != null)
            craftingRecipeCloseButton.gameObject.SetActive(false);
    }

    void LayoutTraderScreen()
    {
        if (currentScreen != ProfileScreen.Trader)
            return;

        if (storageViewRootObject != null && storageViewRootObject.activeSelf)
            LayoutCraftingStoragePanel();

        LayoutAmbientShipBackdrop();
        if (shipWorkspaceRootObject != null)
            shipWorkspaceRootObject.transform.SetAsFirstSibling();
        if (traderViewRootObject != null)
            traderViewRootObject.transform.SetAsLastSibling();
        if (storageViewRootObject != null)
            storageViewRootObject.transform.SetAsLastSibling();
        if (shopBrowserObject != null)
            shopBrowserObject.transform.SetAsLastSibling();
        if (traderFuturePanelObject != null)
            traderFuturePanelObject.transform.SetAsLastSibling();

        if (traderFuturePanelObject != null)
        {
            RectTransform rect = traderFuturePanelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(786f, -156f);
            rect.sizeDelta = new Vector2(280f, 736f);
        }
    }

    void ConfigureEmbeddedTraderBrowser()
    {
        if (shopBrowserObject == null)
            return;

        Image overlay = shopBrowserObject.GetComponent<Image>();
        if (overlay != null)
        {
            overlay.color = new Color(0f, 0f, 0f, 0f);
            overlay.raycastTarget = false;
        }

        Transform panel = shopBrowserObject.transform.Find("ShopBrowserPanel");
        if (panel != null)
        {
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(262f, -156f);
            panelRect.sizeDelta = new Vector2(628f, 736f);
        }

        if (shopCloseButton != null)
            shopCloseButton.gameObject.SetActive(false);

        if (shopAstronsText != null)
            shopAstronsText.gameObject.SetActive(false);

        TMP_Text title = shopBrowserObject.transform.Find("ShopBrowserPanel/ShopBrowserTitle")?.GetComponent<TMP_Text>();
        if (title != null)
            title.text = "TRADER";

        RectTransform viewportRect = shopBrowserObject.transform.Find("ShopBrowserPanel/ShopViewport")?.GetComponent<RectTransform>();
        if (viewportRect != null)
        {
            viewportRect.anchoredPosition = new Vector2(-20f, -22f);
            viewportRect.sizeDelta = new Vector2(604f, 610f);
        }

        RectTransform scrollbarRect = shopBrowserObject.transform.Find("ShopBrowserPanel/ShopScrollbar")?.GetComponent<RectTransform>();
        if (scrollbarRect != null)
        {
            scrollbarRect.anchoredPosition = new Vector2(318f, -22f);
            scrollbarRect.sizeDelta = new Vector2(30f, 610f);
        }
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

    void OnShipPreviewClicked()
    {
        if (inventoryActionInProgress || dragInProgress || shipPreviewImage == null || shipPreviewImage.sprite == null)
            return;

        SwitchToScreen(ProfileScreen.ShipSelection);
    }

    void HideShipImageModal()
    {
        if (shipImageModalObject != null)
            shipImageModalObject.SetActive(false);
    }

    void MoveShipSelectionLeft()
    {
        if (inventoryActionInProgress)
            return;

        shipSelectionCenterIndex = Mathf.Max(0, shipSelectionCenterIndex - 1);
        shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];
        RefreshShipSelectionView();
    }

    void MoveShipSelectionRight()
    {
        if (inventoryActionInProgress)
            return;

        shipSelectionCenterIndex = Mathf.Min(SelectableShipTypes.Length - 1, shipSelectionCenterIndex + 1);
        shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];
        RefreshShipSelectionView();
    }

    public void OnShipSelectionSwiped(float horizontalDelta)
    {
        if (inventoryActionInProgress)
            return;

        if (Mathf.Abs(horizontalDelta) < 72f)
            return;

        if (horizontalDelta > 0f)
            MoveShipSelectionLeft();
        else
            MoveShipSelectionRight();
    }

    void OnShipSelectionCardClicked(int cardIndex)
    {
        if (inventoryActionInProgress)
            return;

        if (cardIndex < 0 || cardIndex >= shipSelectionCardObjects.Length)
            return;

        if (cardIndex == 1)
        {
            CommitShipSelection(shipSelectionCenterType);
            return;
        }

        int direction = cardIndex == 0 ? -1 : 1;
        shipSelectionCenterIndex = Mathf.Clamp(shipSelectionCenterIndex + direction, 0, SelectableShipTypes.Length - 1);
        shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];
        RefreshShipSelectionView();
    }

    async void CommitShipSelection(ShipType shipType)
    {
        int targetSkin = GetShipSelectionDisplaySkin(shipType);
        inventoryActionInProgress = true;
        SetInteractable(false);
        if (shipSelectionStatusText != null)
            shipSelectionStatusText.text = "Switching ship...";

        try
        {
            bool changed = await PlayerProfileService.Instance.TryChangeShipSkinAsync(targetSkin);
            if (!changed)
            {
                if (shipSelectionStatusText != null)
                    shipSelectionStatusText.text = "No room in player inventory for extra cargo.";
                RefreshView();
                return;
            }

            selectedSkin = targetSkin;
            shipSelectionSkinByType[shipType] = targetSkin;
            RefreshView();
            SwitchToScreen(ProfileScreen.Home);
        }
        catch (Exception ex)
        {
            Debug.LogError("Ship selection failed: " + ex);
            if (shipSelectionStatusText != null)
                shipSelectionStatusText.text = "Ship change failed.";
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshShipSelectionView();
        }
    }

    void SetShipSelectionSkinByButton(int buttonIndex)
    {
        int[] allowedSkins = ShipCatalog.GetSkinsForShipType(shipSelectionCenterType);
        if (buttonIndex < 0 || buttonIndex >= allowedSkins.Length)
            return;

        shipSelectionSkinByType[shipSelectionCenterType] = allowedSkins[buttonIndex];
        RefreshShipSelectionView();
    }

    int GetShipSelectionDisplaySkin(ShipType shipType)
    {
        if (shipSelectionSkinByType.TryGetValue(shipType, out int storedSkin) && ShipCatalog.GetShipTypeFromSkinIndex(storedSkin) == shipType)
            return storedSkin;

        if (ShipCatalog.GetShipTypeFromSkinIndex(selectedSkin) == shipType)
            return selectedSkin;

        int[] skins = ShipCatalog.GetSkinsForShipType(shipType);
        return skins != null && skins.Length > 0 ? skins[0] : selectedSkin;
    }

    void RefreshShipSelectionView()
    {
        if (shipSelectionViewObject == null || shipSelectionCardObjects == null)
            return;

        shipSelectionCenterIndex = Mathf.Clamp(shipSelectionCenterIndex, 0, SelectableShipTypes.Length - 1);
        shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];

        if (shipSelectionTitleText != null)
        {
            shipSelectionTitleText.gameObject.SetActive(true);
            shipSelectionTitleText.text = ShipCatalog.GetShipTypeDisplayName(shipSelectionCenterType).ToUpperInvariant();
            shipSelectionTitleText.transform.SetAsLastSibling();
        }

        if (shipSelectionBackButton != null)
            LayoutSharedProfileBackButton(shipSelectionBackButton.GetComponent<RectTransform>());

        if (!inventoryActionInProgress && shipSelectionStatusText != null)
            shipSelectionStatusText.text = string.Empty;

        for (int i = 0; i < shipSelectionCardObjects.Length; i++)
        {
            int offset = i - 1;
            int targetIndex = shipSelectionCenterIndex + offset;
            bool visible = targetIndex >= 0 && targetIndex < SelectableShipTypes.Length;
            shipSelectionCardObjects[i].SetActive(visible);
            if (!visible)
                continue;

            ShipType shipType = SelectableShipTypes[targetIndex];
            UpdateShipSelectionCard(i, shipType, i == 1);
        }

        int[] allowedSkins = ShipCatalog.GetSkinsForShipType(shipSelectionCenterType);
        for (int i = 0; i < shipSelectionSkinButtons.Length; i++)
        {
            if (shipSelectionSkinButtons[i] == null)
                continue;

            bool active = i < allowedSkins.Length;
            shipSelectionSkinButtons[i].gameObject.SetActive(active);
            if (!active)
                continue;

            bool selected = allowedSkins[i] == GetShipSelectionDisplaySkin(shipSelectionCenterType);
            TMP_Text text = shipSelectionSkinButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = ShipCatalog.GetSkinDisplayName(allowedSkins[i]).ToUpperInvariant();
                text.enableAutoSizing = true;
                text.fontSizeMin = 12f;
                text.fontSizeMax = 20f;
            }

            StyleButton(
                shipSelectionSkinButtons[i],
                selected ? new Color(0.2f, 0.38f, 0.58f, 0.98f) : new Color(0.16f, 0.2f, 0.27f, 0.95f),
                selected ? new Color(0.28f, 0.5f, 0.74f, 1f) : new Color(0.22f, 0.3f, 0.42f, 1f));
        }

        UpdateShipSelectionCardLayering();
    }

    void UpdateShipSelectionCardLayering()
    {
        if (shipSelectionCardObjects == null)
            return;

        if (shipSelectionCardObjects.Length > 0 && shipSelectionCardObjects[0] != null && shipSelectionCardObjects[0].activeSelf)
            shipSelectionCardObjects[0].transform.SetSiblingIndex(0);
        if (shipSelectionCardObjects.Length > 2 && shipSelectionCardObjects[2] != null && shipSelectionCardObjects[2].activeSelf)
            shipSelectionCardObjects[2].transform.SetSiblingIndex(1);
        if (shipSelectionCardObjects.Length > 1 && shipSelectionCardObjects[1] != null && shipSelectionCardObjects[1].activeSelf)
            shipSelectionCardObjects[1].transform.SetSiblingIndex(2);

        if (shipSelectionBackButton != null)
            shipSelectionBackButton.transform.SetAsLastSibling();
        if (shipSelectionSkinButtons != null)
        {
            for (int i = 0; i < shipSelectionSkinButtons.Length; i++)
            {
                if (shipSelectionSkinButtons[i] != null && shipSelectionSkinButtons[i].gameObject.activeSelf)
                    shipSelectionSkinButtons[i].transform.SetAsLastSibling();
            }
        }
        if (shipSelectionStatusText != null)
            shipSelectionStatusText.transform.SetAsLastSibling();
    }

    void UpdateShipSelectionCard(int cardIndex, ShipType shipType, bool centerCard)
    {
        if (shipSelectionCardTitles == null || cardIndex < 0 || cardIndex >= shipSelectionCardTitles.Length)
            return;

        int skinIndex = GetShipSelectionDisplaySkin(shipType);
        PlayerShipDefinition definition = ShipCatalog.GetShipDefinition(shipType);
        RectTransform cardRect = shipSelectionCardObjects[cardIndex] != null ? shipSelectionCardObjects[cardIndex].GetComponent<RectTransform>() : null;
        if (cardRect != null)
        {
            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = centerCard
                ? new Vector2(0f, -72f)
                : new Vector2(cardIndex == 0 ? -590f : 590f, -88f);
            cardRect.sizeDelta = centerCard
                ? new Vector2(920f, 920f)
                : new Vector2(640f, 800f);
        }

        TMP_Text title = shipSelectionCardTitles[cardIndex];
        if (title != null)
        {
            title.text = ShipCatalog.GetShipTypeDisplayName(shipType).ToUpperInvariant();
            title.gameObject.SetActive(false);
        }

        Image image = shipSelectionCardImages[cardIndex];
        if (image != null)
        {
            image.sprite = LoadShipPreviewSprite(skinIndex);
            image.color = image.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            RectTransform imageRect = image.rectTransform;
            imageRect.anchoredPosition = centerCard ? new Vector2(48f, -64f) : new Vector2(30f, -48f);
            imageRect.sizeDelta = centerCard ? new Vector2(680f, 760f) : new Vector2(470f, 540f);
        }

        Image cardImage = shipSelectionCardObjects[cardIndex].GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.color = centerCard
                ? new Color(0.08f, 0.11f, 0.16f, 0.76f)
                : new Color(0.07f, 0.1f, 0.15f, 0.68f);
        }

        TMP_Text[] statLabels = shipSelectionCardStatLabelTexts != null && cardIndex < shipSelectionCardStatLabelTexts.Length
            ? shipSelectionCardStatLabelTexts[cardIndex]
            : null;
        TMP_Text[] statValues = shipSelectionCardStatValueTexts != null && cardIndex < shipSelectionCardStatValueTexts.Length
            ? shipSelectionCardStatValueTexts[cardIndex]
            : null;
        Image[] statFills = shipSelectionCardStatFillImages != null && cardIndex < shipSelectionCardStatFillImages.Length
            ? shipSelectionCardStatFillImages[cardIndex]
            : null;

        if (statLabels != null && statValues != null && statFills != null)
        {
            LayoutShipSelectionStats(cardIndex, centerCard);
            SetShipSelectionStatCard(statLabels, statValues, statFills, 0, ShipStatLabels[0], definition.BaseHp.ToString(), NormalizeShipStat(definition.BaseHp, stat => stat.BaseHp));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 1, ShipStatLabels[1], definition.BaseShield.ToString(), NormalizeShipStat(definition.BaseShield, stat => stat.BaseShield));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 2, ShipStatLabels[2], definition.BaseSpeed.ToString("0.0"), NormalizeShipStat(definition.BaseSpeed, stat => stat.BaseSpeed));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 3, ShipStatLabels[3], "x" + definition.TurnRateMultiplier.ToString("0.00"), NormalizeShipStat(definition.TurnRateMultiplier, stat => stat.TurnRateMultiplier));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 4, ShipStatLabels[4], definition.BoosterDuration.ToString("0.0") + "s", NormalizeShipStat(definition.BoosterDuration, stat => stat.BoosterDuration));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 5, ShipStatLabels[5], "+" + definition.MaxBoostPercent + "%", NormalizeShipStat(definition.MaxBoostPercent, stat => stat.MaxBoostPercent));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 6, ShipStatLabels[6], definition.CargoCapacity.ToString(), NormalizeShipStat(definition.CargoCapacity, stat => stat.CargoCapacity));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 7, ShipStatLabels[7], definition.SafePocketSlots.ToString(), NormalizeSafePocketStat(definition.SafePocketSlots));
        }

        GameObject[] slotObjects = shipSelectionCardSlotObjects != null && cardIndex < shipSelectionCardSlotObjects.Length
            ? shipSelectionCardSlotObjects[cardIndex]
            : null;
        if (slotObjects != null)
        {
            Vector2[] slotLayout = BuildShipSelectionSlotLayout(centerCard);
            for (int i = 0; i < slotObjects.Length && i < slotLayout.Length; i++)
            {
                if (slotObjects[i] == null)
                    continue;

                bool slotEnabled = ShipCatalog.IsEquipmentSlotEnabled(i, skinIndex);
                slotObjects[i].SetActive(slotEnabled);
                if (!slotEnabled)
                    continue;

                RectTransform slotRect = slotObjects[i].GetComponent<RectTransform>();
                if (slotRect != null)
                {
                    slotRect.anchoredPosition = slotLayout[i];
                    slotRect.sizeDelta = centerCard ? new Vector2(92f, 92f) : new Vector2(72f, 72f);
                }
            }
        }
    }

    void LayoutShipSelectionStats(int cardIndex, bool centerCard)
    {
        TMP_Text[] statLabels = shipSelectionCardStatLabelTexts != null && cardIndex < shipSelectionCardStatLabelTexts.Length
            ? shipSelectionCardStatLabelTexts[cardIndex]
            : null;
        if (statLabels == null)
            return;

        float x = centerCard ? 260f : 186f;
        float topY = centerCard ? -144f : -130f;
        Vector2 cardSize = centerCard ? new Vector2(236f, 58f) : new Vector2(182f, 46f);
        float spacing = centerCard ? 12f : 10f;

        for (int i = 0; i < statLabels.Length; i++)
        {
            TMP_Text label = statLabels[i];
            TMP_Text value = shipSelectionCardStatValueTexts != null && cardIndex < shipSelectionCardStatValueTexts.Length && i < shipSelectionCardStatValueTexts[cardIndex].Length
                ? shipSelectionCardStatValueTexts[cardIndex][i]
                : null;
            if (label == null)
                continue;

            RectTransform cardRect = label.transform.parent != null ? label.transform.parent.GetComponent<RectTransform>() : null;
            if (cardRect == null)
                continue;

            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = new Vector2(x, topY - (i * (cardSize.y + spacing)));
            cardRect.sizeDelta = cardSize;

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = new Vector2(12f, centerCard ? -12f : -10f);
            labelRect.sizeDelta = new Vector2(cardSize.x * 0.48f, centerCard ? 20f : 16f);
            label.fontSize = centerCard ? 18f : 14f;

            if (value != null)
            {
                RectTransform valueRect = value.rectTransform;
                valueRect.anchorMin = new Vector2(1f, 1f);
                valueRect.anchorMax = new Vector2(1f, 1f);
                valueRect.pivot = new Vector2(1f, 1f);
                valueRect.anchoredPosition = new Vector2(centerCard ? -10f : -8f, centerCard ? -12f : -10f);
                valueRect.sizeDelta = new Vector2(cardSize.x * 0.42f, centerCard ? 20f : 16f);
                value.fontSize = centerCard ? 18f : 14f;
            }

            Transform barBgTransform = cardRect.Find("BarBg");
            if (barBgTransform != null)
            {
                RectTransform barBgRect = barBgTransform.GetComponent<RectTransform>();
                if (barBgRect != null)
                {
                    barBgRect.anchorMin = new Vector2(0f, 0f);
                    barBgRect.anchorMax = new Vector2(1f, 0f);
                    barBgRect.pivot = new Vector2(0.5f, 0f);
                    barBgRect.anchoredPosition = new Vector2(0f, centerCard ? 10f : 8f);
                    barBgRect.sizeDelta = new Vector2(-18f, centerCard ? 14f : 10f);
                }
            }
        }
    }

    void SetShipSelectionStatCard(TMP_Text[] labels, TMP_Text[] values, Image[] fills, int index, string label, string valueText, float normalized)
    {
        if (labels == null || values == null || fills == null || index < 0 || index >= labels.Length || index >= values.Length || index >= fills.Length)
            return;

        if (labels[index] != null)
            labels[index].text = label;
        if (values[index] != null)
            values[index].text = valueText;

        Image fillImage = fills[index];
        if (fillImage == null)
            return;

        float clamped = Mathf.Clamp01(normalized);
        RectTransform fillRect = fillImage.rectTransform;
        if (fillRect != null)
        {
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(clamped, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        fillImage.color = EvaluateShipStatColor(clamped);
    }

    void RefreshEquipmentSlotPreview()
    {
        if (equipmentSlotPreviewTexts == null || equipmentSlotPreviewTexts.Length < PlayerInventoryData.EquipmentSlotCount)
            return;

        PlayerInventoryData inventory = PlayerProfileService.Instance.CurrentProfile != null
            ? PlayerProfileService.Instance.CurrentProfile.Inventory
            : null;

        for (int i = 0; i < PlayerInventoryData.EquipmentSlotCount; i++)
        {
            bool enabled = inventory != null && inventory.IsEquipmentSlotEnabled(i, selectedSkin);
            string itemId = inventory != null && inventory.EquipmentSlots != null && i < inventory.EquipmentSlots.Length
                ? inventory.EquipmentSlots[i]
                : null;
            SetEquipmentSlotState(i, enabled, GetEquipmentSlotLabel(i), itemId);
        }
    }

    void SetEquipmentSlotState(int slotIndex, bool enabled, string label, string itemId)
    {
        TMP_Text text = equipmentSlotPreviewTexts != null && slotIndex >= 0 && slotIndex < equipmentSlotPreviewTexts.Length
            ? equipmentSlotPreviewTexts[slotIndex]
            : null;
        Image icon = equipmentSlotPreviewIcons != null && slotIndex >= 0 && slotIndex < equipmentSlotPreviewIcons.Length
            ? equipmentSlotPreviewIcons[slotIndex]
            : null;
        Button button = equipmentSlotButtons != null && slotIndex >= 0 && slotIndex < equipmentSlotButtons.Length
            ? equipmentSlotButtons[slotIndex]
            : null;

        if (button != null)
            button.gameObject.SetActive(enabled);

        if (!enabled)
        {
            if (text != null)
                text.text = string.Empty;

            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            return;
        }

        if (text == null)
            return;

        bool occupied = enabled && !string.IsNullOrWhiteSpace(itemId);
        Sprite itemSprite = occupied ? InventoryItemCatalog.GetIcon(itemId) : null;
        bool showDefaultWeaponPlaceholder = !occupied && InventoryItemCatalog.GetEquipmentSlotCategory(slotIndex) == InventoryItemCategory.Weapon;
        Sprite placeholderSprite = showDefaultWeaponPlaceholder ? WeaponAttackCatalog.GetWeaponIcon(WeaponAttackCatalog.SimpleGunId) : null;

        text.text = occupied && itemSprite == null ? InventoryItemCatalog.GetShortLabel(itemId) : label;
        text.color = Color.white;
        Image bg = text.transform.parent != null ? text.transform.parent.GetComponent<Image>() : null;
        if (bg != null)
        {
            bg.color = occupied
                ? InventoryItemCatalog.GetRarityColor(itemId)
                : new Color(0.17f, 0.22f, 0.28f, 0.88f);
        }

        if (icon != null)
        {
            icon.sprite = occupied ? itemSprite : placeholderSprite;
            icon.enabled = occupied
                ? itemSprite != null
                : placeholderSprite != null;
            icon.color = occupied ? Color.white : new Color(1f, 1f, 1f, 0.28f);
        }

        if (button != null)
            button.interactable = !inventoryActionInProgress;
    }

    string GetEquipmentSlotLabel(int slotIndex)
    {
        return ShipCatalog.GetEquipmentSlotLabel(slotIndex);
    }

    Sprite LoadShipPreviewSprite(int skinIndex)
    {
        return LoadSpriteFromResourcesOrEditor(
            ShipCatalog.GetShipSkinResourcePath(skinIndex),
            ShipCatalog.GetShipSkinEditorResourcePath(skinIndex),
            ShipCatalog.GetShipSkinEditorFallbackPath(skinIndex));
    }

    Sprite LoadStandaloneSprite(string fileName)
    {
        string resourcesPath = fileName switch
        {
            "STAR_RAIDERS_ekran.png" => "STAR_RAIDERS_ekran_resource",
            "hangar1_2D.png" => "UI/hangar1_2D_profile",
            "hangar1_2D_przesuniety.png" => "UI/hangar1_2D_przesuniety_profile",
            "ship1.png" => "Visuals/Ships/ship1_resource",
            "ship2.png" => "Visuals/Ships/ship2_resource",
            "ship3.png" => "Visuals/Ships/ship3_resource",
            "ship4.png" => "ship4_resource",
            _ => null
        };

        string editorResourcePath = fileName switch
        {
            "STAR_RAIDERS_ekran.png" => "Assets/Resources/STAR_RAIDERS_ekran_resource.png",
            "hangar1_2D.png" => "Assets/Resources/UI/hangar1_2D_profile.png",
            "hangar1_2D_przesuniety.png" => "Assets/Resources/UI/hangar1_2D_przesuniety_profile.png",
            "ship1.png" => "Assets/Resources/Visuals/Ships/ship1_resource.png",
            "ship2.png" => "Assets/Resources/Visuals/Ships/ship2_resource.png",
            "ship3.png" => "Assets/Resources/Visuals/Ships/ship3_resource.png",
            "ship4.png" => "Assets/Resources/ship4_resource.png",
            _ => null
        };

        string editorFallbackPath = string.IsNullOrWhiteSpace(fileName) ? null : "Assets/" + fileName;
        return LoadSpriteFromResourcesOrEditor(resourcesPath, editorResourcePath, editorFallbackPath);
    }

    Sprite LoadSpriteFromResourcesOrEditor(string resourcesPath, string editorPreferredPath, string editorFallbackPath = null)
    {
        Sprite sprite = LoadSpriteFromResources(resourcesPath);
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        sprite = LoadEditorSprite(editorPreferredPath);
        if (sprite != null)
            return sprite;

        if (!string.IsNullOrWhiteSpace(editorFallbackPath))
            return LoadEditorSprite(editorFallbackPath);
#endif

        return null;
    }

    Sprite LoadSpriteFromResources(string resourcesPath)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcesPath);
        sprite = GetLargestSprite(sprites);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
        if (texture == null)
            return null;

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (candidate == null)
                continue;

            float area = candidate.rect.width * candidate.rect.height;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }

#if UNITY_EDITOR
    Sprite LoadEditorSprite(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
            return sprite;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite loadedSprite)
                return loadedSprite;
        }

        return null;
    }
#endif

    void RefreshVisibility()
    {
        if (panelObject == null)
            return;

        bool splashShowing = splashScreenObject != null && splashHideTime > 0f && Time.unscaledTime < splashHideTime;
        if (splashScreenObject != null)
        {
            splashScreenObject.SetActive(splashShowing);
            if (splashShowing)
                splashScreenObject.transform.SetAsLastSibling();
        }

        bool show = !PhotonNetwork.InRoom;
        panelObject.SetActive(show);
        SetGameplayHudVisible(!show);

        if (!show)
        {
            HideShipImageModal();
            HideCraftingRecipeBrowser();
            HideShopBrowser();
            HideCheatBrowser();
        }

        if (show)
        {
            bool browserVisible = SessionBrowserPanelUI.IsVisible;
            SetInteractable(!NetworkManager.SessionRequested || !browserVisible);
            if (statusText != null &&
                (statusText.text == "Connecting..." || statusText.text == "Loading active rounds...") &&
                !NetworkManager.SessionRequested)
            {
                statusText.text = string.Empty;
            }
        }
    }

    void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = value;
        }
    }

    int GetPlayerInventorySlotCount()
    {
        if (!PlayerProfileService.HasInstance || PlayerProfileService.Instance.CurrentProfile == null)
            return PlayerInventoryData.DefaultPlayerSlotCount;

        PlayerInventoryData inventory = PlayerProfileService.Instance.CurrentProfile.Inventory;
        if (inventory == null || inventory.PlayerSlots == null)
            return PlayerInventoryData.DefaultPlayerSlotCount;

        return Mathf.Max(PlayerInventoryData.DefaultPlayerSlotCount, inventory.PlayerSlots.Length);
    }

    void OnPlayerInventoryExtendClicked()
    {
        if (inventoryActionInProgress || PlayerProfileService.Instance.CurrentProfile == null)
            return;

        int price = PlayerProfileService.Instance.GetNextPlayerInventoryExtendPrice();
        if (playerInventoryExtendConfirmText != null)
            playerInventoryExtendConfirmText.text = "Do you want to extend player inventory for " + price + " Astrons?";

        if (playerInventoryExtendConfirmObject != null)
        {
            playerInventoryExtendConfirmObject.SetActive(true);
            playerInventoryExtendConfirmObject.transform.SetAsLastSibling();
        }
    }

    async void OnShipInventoryUnloadClicked()
    {
        if (inventoryActionInProgress || PlayerProfileService.Instance.CurrentProfile == null)
            return;

        PlayerInventoryData inventory = PlayerProfileService.Instance.CurrentProfile.Inventory;
        if (!HasShipInventoryItems(inventory))
        {
            SetStatus("Ship inventory is empty.");
            return;
        }

        if (!HasFreePlayerInventorySlot(inventory))
        {
            SetStatus("No free player inventory slots.");
            return;
        }

        try
        {
            inventoryActionInProgress = true;
            SetInteractable(false);
            SetStatus("Unloading ship inventory...");

            int movedCount = await PlayerProfileService.Instance.UnloadShipInventoryToPlayerAsync();
            if (movedCount > 0)
            {
                HideItemPreview();
                SetStatus("Unloaded " + movedCount + (movedCount == 1 ? " item." : " items."));
            }
            else
            {
                SetStatus("No free player inventory slots.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Ship inventory unload failed: " + ex);
            SetStatus("Unload failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(!NetworkManager.SessionRequested);
            RefreshView();
        }
    }

    bool HasShipInventoryItems(PlayerInventoryData inventory)
    {
        if (inventory == null)
            return false;

        inventory.Normalize();
        for (int i = 0; i < inventory.ShipSlots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(inventory.ShipSlots[i]))
                return true;
        }

        return false;
    }

    bool HasFreePlayerInventorySlot(PlayerInventoryData inventory)
    {
        if (inventory == null)
            return false;

        inventory.Normalize();
        for (int i = 0; i < inventory.PlayerSlots.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(inventory.PlayerSlots[i]))
                return true;
        }

        return false;
    }

    void HidePlayerInventoryExtendConfirm()
    {
        if (playerInventoryExtendConfirmObject != null)
            playerInventoryExtendConfirmObject.SetActive(false);
    }

    async void OnPlayerInventoryExtendConfirmClicked()
    {
        if (inventoryActionInProgress)
            return;

        int price = PlayerProfileService.Instance.GetNextPlayerInventoryExtendPrice();
        try
        {
            inventoryActionInProgress = true;
            SetInteractable(false);
            bool extended = await PlayerProfileService.Instance.TryExtendPlayerInventoryAsync();
            HidePlayerInventoryExtendConfirm();
            if (extended)
                SetStatus("Player inventory extended by " + PlayerInventoryData.PlayerSlotExtensionSize + " slots.");
            else
                SetStatus("Not enough Astrons. Need " + price + ".");
        }
        catch (Exception ex)
        {
            Debug.LogError("Player inventory extend failed: " + ex);
            SetStatus("Inventory extension failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(!NetworkManager.SessionRequested);
            RefreshView();
        }
    }

    void SetInteractable(bool interactable)
    {
        if (nicknameInput != null)
            nicknameInput.interactable = interactable;

        if (saveAndRunButton != null)
            saveAndRunButton.interactable = interactable;

        if (cheatButton != null)
            cheatButton.interactable = interactable;

        if (shopButton != null)
            shopButton.interactable = interactable;

        if (navCraftingButton != null)
            navCraftingButton.interactable = interactable;

        if (navInventoryButton != null)
            navInventoryButton.interactable = interactable;

        if (navBackButton != null)
            navBackButton.interactable = interactable;

        if (exitGameButton != null)
            exitGameButton.interactable = interactable;

        if (shipTypeButtons != null)
        {
            for (int i = 0; i < shipTypeButtons.Length; i++)
            {
                if (shipTypeButtons[i] != null)
                    shipTypeButtons[i].interactable = interactable;
            }
        }

        if (skinButtons == null)
            return;

        for (int i = 0; i < skinButtons.Length; i++)
        {
            if (skinButtons[i] != null)
                skinButtons[i].interactable = interactable;
        }

        if (shipSelectionBackButton != null)
            shipSelectionBackButton.interactable = interactable;
        if (shipSelectionPrevButton != null)
            shipSelectionPrevButton.interactable = interactable;
        if (shipSelectionNextButton != null)
            shipSelectionNextButton.interactable = interactable;
        if (shipSelectionSkinButtons != null)
        {
            for (int i = 0; i < shipSelectionSkinButtons.Length; i++)
            {
                if (shipSelectionSkinButtons[i] != null)
                    shipSelectionSkinButtons[i].interactable = interactable;
            }
        }

        SetInventoryInteractable(interactable && !inventoryActionInProgress);
    }

    void SetInventoryInteractable(bool interactable)
    {
        SetInventoryButtonState(playerInventoryButtons, interactable);
        SetInventoryButtonState(shipInventoryButtons, interactable);
        SetInventoryButtonState(equipmentSlotButtons, interactable);
        SetInventoryButtonState(craftingSlotButtons, interactable);
        if (shipPreviewButton != null)
            shipPreviewButton.interactable = interactable && shipPreviewImage != null && shipPreviewImage.sprite != null;
        if (craftingCatalogButton != null)
            craftingCatalogButton.interactable = interactable;
        if (craftingRecipeCloseButton != null)
            craftingRecipeCloseButton.interactable = interactable;
        if (itemPreviewSellButton != null)
            itemPreviewSellButton.interactable = interactable;
        if (itemPreviewSalvageButton != null)
            itemPreviewSalvageButton.interactable = interactable;
        if (craftButton != null)
            craftButton.interactable = interactable;
        if (clearCraftButton != null)
            clearCraftButton.interactable = interactable;
        if (playerInventoryExtendButton != null)
            playerInventoryExtendButton.interactable = interactable;
        if (playerInventoryFilterButton != null)
            playerInventoryFilterButton.interactable = interactable;
        if (shipInventoryUnloadButton != null)
            shipInventoryUnloadButton.interactable = interactable;
        if (playerInventoryExtendConfirmButton != null)
            playerInventoryExtendConfirmButton.interactable = interactable;
        if (playerInventoryExtendCancelButton != null)
            playerInventoryExtendCancelButton.interactable = interactable;
        if (shopCloseButton != null)
            shopCloseButton.interactable = interactable;
        if (cheatAddMoneyButton != null)
            cheatAddMoneyButton.interactable = interactable && !inventoryActionInProgress;
        if (cheatCloseButton != null)
            cheatCloseButton.interactable = interactable && !inventoryActionInProgress;
    }

    void SetInventoryButtonState(Button[] buttons, bool interactable)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].interactable = interactable;
        }
    }

    void OnInventorySlotClicked(bool isPlayerInventory, int slotIndex)
    {
        if (suppressNextInventoryClick)
        {
            suppressNextInventoryClick = false;
            return;
        }

        if (inventoryActionInProgress || dragInProgress || !panelObject.activeSelf)
            return;

        int resolvedSlotIndex = ResolveInventorySlotIndex(isPlayerInventory, slotIndex);
        if (TryGetInventoryItemId(isPlayerInventory, slotIndex, out string itemId))
        {
            if (IsPreviewingSameItem(ProfileItemSourceFromInventory(isPlayerInventory), resolvedSlotIndex, itemId))
            {
                HideItemPreview();
                SetStatus(string.Empty);
                return;
            }

            ShowItemPreview(ProfileItemSourceFromInventory(isPlayerInventory), resolvedSlotIndex, itemId);
            SetStatus(isPlayerInventory ? "Player item selected." : "Ship item selected.");
        }
        else
        {
            HideItemPreview();
            SetStatus(string.Empty);
        }
    }

    void OnCraftingSlotClicked(int slotIndex)
    {
        if (suppressNextInventoryClick)
        {
            suppressNextInventoryClick = false;
            return;
        }

        if (inventoryActionInProgress || dragInProgress || !panelObject.activeSelf)
            return;

        if (TryGetCraftingItemId(slotIndex, out string itemId))
        {
            if (IsPreviewingSameItem(ProfileItemSource.CraftingSlot, slotIndex, itemId))
            {
                HideItemPreview();
                SetStatus(string.Empty);
                return;
            }

            ShowItemPreview(ProfileItemSource.CraftingSlot, slotIndex, itemId);
            SetStatus("Crafting item selected.");
        }
        else
        {
            HideItemPreview();
            SetStatus(string.Empty);
        }
    }

    public void BeginSlotDrag(bool isPlayerInventory, int slotIndex, PointerEventData eventData)
    {
        if (inventoryActionInProgress || !panelObject.activeSelf)
            return;

        int resolvedSlotIndex = ResolveInventorySlotIndex(isPlayerInventory, slotIndex);
        if (resolvedSlotIndex < 0 || !TryGetInventoryItemId(isPlayerInventory, slotIndex, out string itemId))
            return;

        dragInProgress = true;
        suppressNextInventoryClick = true;
        ShowItemPreview(ProfileItemSourceFromInventory(isPlayerInventory), resolvedSlotIndex, itemId);
        EnsureDragVisual();
        UpdateDragVisualContent(itemId);
        UpdateDragVisualPosition(eventData);
        dragVisualObject.SetActive(true);
    }

    public void BeginCraftingSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (inventoryActionInProgress || dragInProgress || !panelObject.activeSelf)
            return;

        if (!TryGetCraftingItemId(slotIndex, out string itemId))
            return;

        dragInProgress = true;
        suppressNextInventoryClick = true;
        ShowItemPreview(ProfileItemSource.CraftingSlot, slotIndex, itemId);
        EnsureDragVisual();
        UpdateDragVisualContent(itemId);
        UpdateDragVisualPosition(eventData);
        dragVisualObject.SetActive(true);
    }

    public void UpdateSlotDrag(bool isPlayerInventory, int slotIndex, PointerEventData eventData)
    {
        if (!dragInProgress || dragVisualObject == null)
            return;

        UpdateDragVisualPosition(eventData);
    }

    public void UpdateCraftingSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (!dragInProgress || dragVisualObject == null)
            return;

        UpdateDragVisualPosition(eventData);
    }

    public async void EndSlotDrag(bool isPlayerInventory, int slotIndex, PointerEventData eventData)
    {
        if (!dragInProgress)
            return;

        dragInProgress = false;
        if (dragVisualObject != null)
            dragVisualObject.SetActive(false);

        ProfileItemSource source = ProfileItemSourceFromInventory(isPlayerInventory);
        int resolvedSourceIndex = ResolveProfileSlotIndex(source, slotIndex);
        if (!ResolveDropTarget(eventData != null ? eventData.pointerEnter : null, out ProfileItemSource targetSource, out int targetIndex))
            return;

        int resolvedTargetIndex = ResolveProfileSlotIndex(targetSource, targetIndex);
        if (resolvedSourceIndex < 0 || resolvedTargetIndex < 0)
            return;

        inventoryActionInProgress = true;
        SetInteractable(false);

        try
        {
            bool moved = await MoveItemToTargetAsync(source, resolvedSourceIndex, targetSource, resolvedTargetIndex);
            if (moved)
            {
                SetStatus(GetMoveSuccessMessage(targetSource));
                RefreshView();
            }
            else
            {
                SetStatus(GetMoveFailureMessage(targetSource));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Inventory move failed: " + ex);
            SetStatus("Inventory update failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
        }
    }

    public async void EndCraftingSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (!dragInProgress)
            return;

        dragInProgress = false;
        if (dragVisualObject != null)
            dragVisualObject.SetActive(false);

        if (!ResolveDropTarget(eventData != null ? eventData.pointerEnter : null, out ProfileItemSource targetSource, out int targetIndex))
            return;

        inventoryActionInProgress = true;
        SetInteractable(false);

        try
        {
            bool moved = await MoveItemToTargetAsync(ProfileItemSource.CraftingSlot, slotIndex, targetSource, targetIndex);
            if (moved)
            {
                SetStatus(GetMoveSuccessMessage(targetSource));
                RefreshView();
            }
            else
            {
                SetStatus(GetMoveFailureMessage(targetSource));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Crafting move failed: " + ex);
            SetStatus("Inventory update failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
        }
    }

    public void BeginEquipmentSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (inventoryActionInProgress || dragInProgress || !panelObject.activeSelf)
            return;

        if (!TryGetEquipmentItemId(slotIndex, out string itemId))
            return;

        dragInProgress = true;
        suppressNextInventoryClick = true;
        ShowItemPreview(ProfileItemSource.EquipmentSlot, slotIndex, itemId);
        EnsureDragVisual();
        UpdateDragVisualContent(itemId);
        UpdateDragVisualPosition(eventData);
        dragVisualObject.SetActive(true);
    }

    bool TryGetInventoryItemId(bool isPlayerInventory, int slotIndex, out string itemId)
    {
        itemId = null;
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null)
            return false;

        int resolvedSlotIndex = ResolveInventorySlotIndex(isPlayerInventory, slotIndex);
        if (resolvedSlotIndex < 0)
            return false;

        string[] slots = isPlayerInventory ? profile.Inventory.PlayerSlots : profile.Inventory.ShipSlots;
        if (slots == null || resolvedSlotIndex < 0 || resolvedSlotIndex >= slots.Length)
            return false;

        itemId = slots[resolvedSlotIndex];
        return !string.IsNullOrWhiteSpace(itemId);
    }

    int ResolveInventorySlotIndex(bool isPlayerInventory, int slotIndex)
    {
        return isPlayerInventory ? ResolveVisiblePlayerInventorySlotIndex(slotIndex) : slotIndex;
    }

    int ResolveProfileSlotIndex(ProfileItemSource source, int slotIndex)
    {
        return source == ProfileItemSource.PlayerInventory
            ? ResolveVisiblePlayerInventorySlotIndex(slotIndex)
            : slotIndex;
    }

    bool TryGetEquipmentItemId(int slotIndex, out string itemId)
    {
        itemId = null;
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null || profile.Inventory.EquipmentSlots == null)
            return false;

        if (slotIndex < 0 || slotIndex >= profile.Inventory.EquipmentSlots.Length)
            return false;

        itemId = profile.Inventory.EquipmentSlots[slotIndex];
        return !string.IsNullOrWhiteSpace(itemId);
    }

    bool TryGetCraftingItemId(int slotIndex, out string itemId)
    {
        itemId = null;
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null)
            return false;

        profile.Inventory.Normalize();

        if (slotIndex < 0 || slotIndex >= profile.Inventory.CraftingSlots.Length)
            return false;

        itemId = profile.Inventory.CraftingSlots[slotIndex];
        return !string.IsNullOrWhiteSpace(itemId);
    }

    void OnEquipmentSlotClicked(int slotIndex)
    {
        if (suppressNextInventoryClick)
        {
            suppressNextInventoryClick = false;
            return;
        }

        if (inventoryActionInProgress || dragInProgress || !panelObject.activeSelf)
            return;

        if (TryGetEquipmentItemId(slotIndex, out string itemId))
        {
            if (IsPreviewingSameItem(ProfileItemSource.EquipmentSlot, slotIndex, itemId))
            {
                HideItemPreview();
                SetStatus(string.Empty);
                return;
            }

            ShowItemPreview(ProfileItemSource.EquipmentSlot, slotIndex, itemId);
            SetStatus("Equipment item selected.");
        }
        else
        {
            HideItemPreview();
            SetStatus(string.Empty);
        }
    }

    bool IsPreviewingSameItem(ProfileItemSource source, int slotIndex, string itemId)
    {
        return itemPreviewPanelObject != null &&
               itemPreviewPanelObject.activeSelf &&
               previewSource == source &&
               previewSlotIndex == slotIndex &&
               string.Equals(previewItemId, itemId, StringComparison.Ordinal);
    }

    public void UpdateEquipmentSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (!dragInProgress || dragVisualObject == null)
            return;

        UpdateDragVisualPosition(eventData);
    }

    public async void EndEquipmentSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (!dragInProgress)
            return;

        dragInProgress = false;
        if (dragVisualObject != null)
            dragVisualObject.SetActive(false);

        if (!ResolveDropTarget(eventData != null ? eventData.pointerEnter : null, out ProfileItemSource targetSource, out int targetIndex))
            return;

        inventoryActionInProgress = true;
        SetInteractable(false);

        try
        {
            bool moved = await MoveItemToTargetAsync(ProfileItemSource.EquipmentSlot, slotIndex, targetSource, targetIndex);
            if (moved)
            {
                SetStatus(GetMoveSuccessMessage(targetSource));
                RefreshView();
            }
            else
            {
                SetStatus(GetMoveFailureMessage(targetSource));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Equipment move failed: " + ex);
            SetStatus("Inventory update failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
        }
    }

    void ShowItemPreview(ProfileItemSource source, int slotIndex, string itemId)
    {
        if (itemPreviewPanelObject == null || string.IsNullOrWhiteSpace(itemId))
            return;

        ApplyItemPreviewLayout();
        itemPreviewPanelObject.SetActive(true);
        itemPreviewPanelObject.transform.SetAsLastSibling();
        previewSource = source;
        previewSlotIndex = slotIndex;
        previewItemId = itemId;
        itemPreviewIcon.sprite = InventoryItemCatalog.GetIcon(itemId);
        itemPreviewIcon.enabled = itemPreviewIcon.sprite != null;
        itemPreviewNameText.text = InventoryItemCatalog.GetDisplayName(itemId).ToUpperInvariant();
        if (itemPreviewTypeText != null)
            itemPreviewTypeText.text = InventoryItemCatalog.GetCategoryLabel(itemId);
        itemPreviewPriceText.text = InventoryItemCatalog.GetSellValueAstrons(itemId) + " Astrons";

        Image bg = itemPreviewBackgroundImage != null ? itemPreviewBackgroundImage : itemPreviewPanelObject.GetComponent<Image>();
        if (bg != null)
        {
            Color rarityColor = InventoryItemCatalog.GetRarityColor(itemId);
            bg.color = new Color(
                Mathf.Clamp01(rarityColor.r * 0.55f),
                Mathf.Clamp01(rarityColor.g * 0.55f),
                Mathf.Clamp01(rarityColor.b * 0.55f),
                0.95f);
        }

        bool supportsInventoryActions = source == ProfileItemSource.PlayerInventory || source == ProfileItemSource.ShipInventory;
        if (itemPreviewSellButton != null)
            itemPreviewSellButton.gameObject.SetActive(supportsInventoryActions);
        if (itemPreviewSalvageButton != null)
            itemPreviewSalvageButton.gameObject.SetActive(supportsInventoryActions);
    }

    void HideItemPreview()
    {
        if (itemPreviewPanelObject != null)
            itemPreviewPanelObject.SetActive(false);

        previewSource = ProfileItemSource.None;
        previewSlotIndex = -1;
        previewItemId = null;
    }

    void EnsureDragVisual()
    {
        if (dragVisualObject != null)
            return;

        dragVisualObject = new GameObject("ProfileDragVisual", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        dragVisualObject.transform.SetParent(panelObject.transform, false);

        RectTransform rect = dragVisualObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(72f, 72f);

        Image bg = dragVisualObject.GetComponent<Image>();
        bg.color = new Color(0.1f, 0.14f, 0.18f, 0.92f);

        CanvasGroup group = dragVisualObject.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0.94f;

        GameObject iconObject = new GameObject("DragIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(dragVisualObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(48f, 48f);
        dragVisualIcon = iconObject.GetComponent<Image>();
        dragVisualIcon.preserveAspect = true;

        dragVisualLabel = CreateText(dragVisualObject.transform, "DragLabel", string.Empty, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 13f, TextAlignmentOptions.Center);
        dragVisualLabel.fontStyle = FontStyles.Bold;
        dragVisualLabel.color = Color.white;

        dragVisualObject.SetActive(false);
    }

    void UpdateDragVisualContent(string itemId)
    {
        if (dragVisualObject == null)
            return;

        Image bg = dragVisualObject.GetComponent<Image>();
        if (bg != null)
            bg.color = InventoryItemCatalog.GetRarityColor(itemId);

        Sprite icon = InventoryItemCatalog.GetIcon(itemId);
        dragVisualIcon.sprite = icon;
        dragVisualIcon.enabled = icon != null;
        dragVisualLabel.text = icon == null ? InventoryItemCatalog.GetShortLabel(itemId) : string.Empty;
    }

    void UpdateDragVisualPosition(PointerEventData eventData)
    {
        if (dragVisualObject == null || panelObject == null || eventData == null)
            return;

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        RectTransform dragRect = dragVisualObject.GetComponent<RectTransform>();
        if (panelRect == null || dragRect == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            dragRect.anchoredPosition = localPoint;
    }

    bool ResolveDropTarget(GameObject hoveredObject, out ProfileItemSource targetSource, out int targetIndex)
    {
        targetSource = ProfileItemSource.None;
        targetIndex = -1;

        Transform current = hoveredObject != null ? hoveredObject.transform : null;
        while (current != null)
        {
            ProfileInventorySlotDragHandler slot = current.GetComponent<ProfileInventorySlotDragHandler>();
            if (slot != null)
            {
                targetSource = ProfileItemSourceFromInventory(slot.isPlayerInventory);
                targetIndex = slot.slotIndex;
                return true;
            }

            ProfileEquipmentSlotDragHandler equipmentSlot = current.GetComponent<ProfileEquipmentSlotDragHandler>();
            if (equipmentSlot != null)
            {
                targetSource = ProfileItemSource.EquipmentSlot;
                targetIndex = equipmentSlot.slotIndex;
                return true;
            }

            ProfileCraftingSlotDragHandler craftingSlot = current.GetComponent<ProfileCraftingSlotDragHandler>();
            if (craftingSlot != null)
            {
                targetSource = ProfileItemSource.CraftingSlot;
                targetIndex = craftingSlot.slotIndex;
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    async void OnItemPreviewSellClicked()
    {
        if (inventoryActionInProgress || previewSource == ProfileItemSource.None || previewSource == ProfileItemSource.EquipmentSlot)
            return;

        bool isShipInventory = previewSource == ProfileItemSource.ShipInventory;
        string itemId = previewItemId;
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        inventoryActionInProgress = true;
        SetInteractable(false);

        try
        {
            int value = InventoryItemCatalog.GetSellValueAstrons(itemId);
            bool sold = await PlayerProfileService.Instance.SellInventoryItemAsync(isShipInventory, previewSlotIndex);
            if (sold)
            {
                SetStatus("Sold for " + value + " Astrons.");
                HideItemPreview();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Inventory sell failed: " + ex);
            SetStatus("Sell failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshView();
        }
    }

    async void OnItemPreviewSalvageClicked()
    {
        if (inventoryActionInProgress || previewSource == ProfileItemSource.None || previewSource == ProfileItemSource.EquipmentSlot)
            return;

        bool isShipInventory = previewSource == ProfileItemSource.ShipInventory;
        inventoryActionInProgress = true;
        SetInteractable(false);

        try
        {
            bool salvaged = await PlayerProfileService.Instance.SalvageInventoryItemAsync(isShipInventory, previewSlotIndex);
            if (salvaged)
            {
                SetStatus("Item salvaged.");
                HideItemPreview();
            }
            else
            {
                SetStatus("No more free inventory slots");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Inventory salvage failed: " + ex);
            SetStatus("Salvage failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshView();
        }
    }

    ProfileItemSource ProfileItemSourceFromInventory(bool isPlayerInventory)
    {
        return isPlayerInventory ? ProfileItemSource.PlayerInventory : ProfileItemSource.ShipInventory;
    }

    async System.Threading.Tasks.Task<bool> MoveItemToTargetAsync(ProfileItemSource source, int sourceIndex, ProfileItemSource targetSource, int targetIndex)
    {
        if (source == targetSource &&
            source != ProfileItemSource.PlayerInventory &&
            source != ProfileItemSource.ShipInventory)
        {
            return false;
        }

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null)
            return false;

        PlayerInventoryData workingInventory = profile.Inventory != null ? profile.Inventory.Clone() : PlayerInventoryData.Default();
        if (!TryMoveCraftingAwareItem(workingInventory, source, sourceIndex, targetSource, targetIndex))
            return false;

        await PlayerProfileService.Instance.SaveInventorySnapshotAsync(workingInventory);
        return true;
    }

    string GetMoveSuccessMessage(ProfileItemSource targetSource)
    {
        return targetSource switch
        {
            ProfileItemSource.PlayerInventory => "Moved item to player inventory.",
            ProfileItemSource.ShipInventory => "Moved item to ship inventory.",
            ProfileItemSource.EquipmentSlot => "Moved item to loadout slot.",
            ProfileItemSource.CraftingSlot => "Moved item to crafting slot.",
            _ => "Item moved."
        };
    }

    string GetMoveFailureMessage(ProfileItemSource targetSource)
    {
        return targetSource switch
        {
            ProfileItemSource.PlayerInventory => "No free player slot for this item.",
            ProfileItemSource.ShipInventory => "No free ship slot for this item.",
            ProfileItemSource.EquipmentSlot => "No free compatible loadout slot.",
            ProfileItemSource.CraftingSlot => "Crafting slot is occupied.",
            _ => "Inventory update failed."
        };
    }

    bool TryMoveCraftingAwareItem(PlayerInventoryData inventory, ProfileItemSource source, int sourceIndex, ProfileItemSource targetSource, int targetIndex)
    {
        if (inventory == null)
            return false;

        inventory.Normalize();
        string candidateItem = PeekItemFromSource(inventory, source, sourceIndex);
        if (targetSource == ProfileItemSource.EquipmentSlot && !InventoryItemCatalog.IsCompatibleWithEquipmentSlot(candidateItem, targetIndex))
            return false;

        if (!TryTakeItemFromSource(inventory, source, sourceIndex, out string movedItem))
            return false;

        if (TryPlaceItemAtTarget(inventory, targetSource, targetIndex, movedItem, source, sourceIndex))
            return true;

        RestoreItemToSource(inventory, source, sourceIndex, movedItem);
        return false;
    }

    bool TryTakeItemFromSource(PlayerInventoryData inventory, ProfileItemSource source, int sourceIndex, out string itemId)
    {
        itemId = source switch
        {
            ProfileItemSource.PlayerInventory => inventory.RemoveFromPlayer(sourceIndex),
            ProfileItemSource.ShipInventory => inventory.RemoveFromShip(sourceIndex),
            ProfileItemSource.EquipmentSlot => inventory.IsEquipmentSlotEnabled(sourceIndex, selectedSkin) ? inventory.RemoveFromEquipment(sourceIndex) : null,
            ProfileItemSource.CraftingSlot => inventory.RemoveFromCrafting(sourceIndex),
            _ => null
        };

        return !string.IsNullOrWhiteSpace(itemId);
    }

    string PeekItemFromSource(PlayerInventoryData inventory, ProfileItemSource source, int sourceIndex)
    {
        if (inventory == null)
            return null;

        return source switch
        {
            ProfileItemSource.PlayerInventory => GetSlotItem(inventory.PlayerSlots, sourceIndex),
            ProfileItemSource.ShipInventory => GetSlotItem(inventory.ShipSlots, sourceIndex),
            ProfileItemSource.EquipmentSlot => inventory.IsEquipmentSlotEnabled(sourceIndex, selectedSkin) ? GetSlotItem(inventory.EquipmentSlots, sourceIndex) : null,
            ProfileItemSource.CraftingSlot => GetSlotItem(inventory.CraftingSlots, sourceIndex),
            _ => null
        };
    }

    string GetSlotItem(string[] slots, int index)
    {
        if (slots == null || index < 0 || index >= slots.Length)
            return null;

        return slots[index];
    }

    bool TryPlaceItemAtTarget(PlayerInventoryData inventory, ProfileItemSource targetSource, int targetIndex, string itemId, ProfileItemSource source, int sourceIndex)
    {
        switch (targetSource)
        {
            case ProfileItemSource.PlayerInventory:
                return TryPlaceItemIntoIndexedSlot(inventory.PlayerSlots, inventory.PlayerSlots != null ? inventory.PlayerSlots.Length : 0, targetIndex, itemId, inventory, source, sourceIndex);

            case ProfileItemSource.ShipInventory:
                return TryPlaceItemIntoIndexedSlot(inventory.ShipSlots, ShipCatalog.GetShipInventoryCapacity(selectedSkin), targetIndex, itemId, inventory, source, sourceIndex);

            case ProfileItemSource.CraftingSlot:
                if (targetIndex < 0 || targetIndex >= PlayerInventoryData.CraftingSlotCount)
                    return false;
                if (!string.IsNullOrWhiteSpace(inventory.CraftingSlots[targetIndex]))
                {
                    string replacedCraftingItem = inventory.CraftingSlots[targetIndex];
                    inventory.SetCrafting(targetIndex, itemId);
                    if (TryReturnItemToSourceSlot(inventory, source, sourceIndex, replacedCraftingItem))
                        return true;

                    inventory.SetCrafting(targetIndex, replacedCraftingItem);
                    return false;
                }
                inventory.SetCrafting(targetIndex, itemId);
                return true;

            case ProfileItemSource.EquipmentSlot:
                if (!inventory.IsEquipmentSlotEnabled(targetIndex, selectedSkin))
                    return false;
                if (!InventoryItemCatalog.IsCompatibleWithEquipmentSlot(itemId, targetIndex))
                    return false;

                string replacedItem = inventory.RemoveFromEquipment(targetIndex);
                inventory.SetEquipment(targetIndex, itemId);

                if (string.IsNullOrWhiteSpace(replacedItem))
                    return true;

                if (TryReturnItemToSourceSlot(inventory, source, sourceIndex, replacedItem))
                    return true;

                inventory.SetEquipment(targetIndex, replacedItem);
                return false;
        }

        return false;
    }

    bool TryPlaceItemIntoIndexedSlot(string[] slots, int capacity, int targetIndex, string itemId, PlayerInventoryData inventory, ProfileItemSource source, int sourceIndex)
    {
        if (slots == null || targetIndex < 0 || targetIndex >= slots.Length || targetIndex >= capacity)
            return false;

        string replacedItem = slots[targetIndex];
        slots[targetIndex] = itemId;

        if (string.IsNullOrWhiteSpace(replacedItem))
            return true;

        if (TryReturnItemToSourceSlot(inventory, source, sourceIndex, replacedItem))
            return true;

        slots[targetIndex] = replacedItem;
        return false;
    }

    bool TryReturnItemToSourceSlot(PlayerInventoryData inventory, ProfileItemSource source, int sourceIndex, string itemId)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(itemId))
            return false;

        switch (source)
        {
            case ProfileItemSource.PlayerInventory:
                if (sourceIndex < 0 || sourceIndex >= inventory.PlayerSlots.Length)
                    return false;
                inventory.RestorePlayer(sourceIndex, itemId);
                return true;

            case ProfileItemSource.ShipInventory:
                if (sourceIndex < 0 || sourceIndex >= ShipCatalog.GetShipInventoryCapacity(selectedSkin))
                    return false;
                inventory.RestoreShip(sourceIndex, itemId);
                return true;

            case ProfileItemSource.CraftingSlot:
                if (sourceIndex < 0 || sourceIndex >= PlayerInventoryData.CraftingSlotCount)
                    return false;
                inventory.SetCrafting(sourceIndex, itemId);
                return true;

            case ProfileItemSource.EquipmentSlot:
                if (!inventory.IsEquipmentSlotEnabled(sourceIndex, selectedSkin))
                    return false;
                if (!InventoryItemCatalog.IsCompatibleWithEquipmentSlot(itemId, sourceIndex))
                    return false;
                inventory.SetEquipment(sourceIndex, itemId);
                return true;
        }

        return false;
    }

    void RestoreItemToSource(PlayerInventoryData inventory, ProfileItemSource source, int sourceIndex, string itemId)
    {
        switch (source)
        {
            case ProfileItemSource.PlayerInventory:
                inventory.RestorePlayer(sourceIndex, itemId);
                break;
            case ProfileItemSource.ShipInventory:
                inventory.RestoreShip(sourceIndex, itemId);
                break;
            case ProfileItemSource.EquipmentSlot:
                inventory.SetEquipment(sourceIndex, itemId);
                break;
            case ProfileItemSource.CraftingSlot:
                inventory.SetCrafting(sourceIndex, itemId);
                break;
        }
    }

    bool IsCraftingGridOccupied()
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null)
            return false;

        profile.Inventory.Normalize();

        for (int i = 0; i < profile.Inventory.CraftingSlots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(profile.Inventory.CraftingSlots[i]))
                return true;
        }

        return false;
    }

    async void OnCraftButtonClicked()
    {
        if (inventoryActionInProgress || !panelObject.activeSelf)
            return;

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null)
            return;

        PlayerInventoryData workingInventory = profile.Inventory != null ? profile.Inventory.Clone() : PlayerInventoryData.Default();
        if (!PlayerProfileCraftingCatalog.TryCraft(workingInventory.CraftingSlots, out PlayerProfileCraftingResult craftResult) || craftResult.Recipe == null)
        {
            SetStatus("No matching crafting recipe.");
            return;
        }

        inventoryActionInProgress = true;
        SetInteractable(false);

        try
        {
            for (int i = 0; i < PlayerInventoryData.CraftingSlotCount; i++)
                workingInventory.SetCrafting(i, null);

            int outputCount = Mathf.Max(1, craftResult.Recipe.OutputCount);
            for (int i = 0; i < outputCount && i < PlayerInventoryData.CraftingSlotCount; i++)
                workingInventory.SetCrafting(i, craftResult.Recipe.OutputItemId);

            await PlayerProfileService.Instance.SaveInventorySnapshotAsync(workingInventory);
            ShowItemPreview(ProfileItemSource.CraftingSlot, 0, craftResult.Recipe.OutputItemId);
            SetStatus("Crafted " + InventoryItemCatalog.GetDisplayName(craftResult.Recipe.OutputItemId) + ".");
            RefreshView();
        }
        catch (Exception ex)
        {
            Debug.LogError("Crafting failed: " + ex);
            SetStatus("Crafting failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
        }
    }

    async void OnClearCraftingSlotsClicked()
    {
        if (inventoryActionInProgress)
            return;

        await ClearCraftingSlotsAsync(true, false);
    }

    async Task<bool> ClearCraftingSlotsAsync(bool showSuccessStatus, bool silentIfEmpty)
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null)
            return true;

        bool hasCraftingItems = false;
        for (int i = 0; i < PlayerInventoryData.CraftingSlotCount; i++)
        {
            if (!string.IsNullOrWhiteSpace(profile.Inventory.CraftingSlots[i]))
            {
                hasCraftingItems = true;
                break;
            }
        }

        if (!hasCraftingItems)
        {
            if (!silentIfEmpty)
                SetStatus("Crafting slots are already empty.");
            return true;
        }

        inventoryActionInProgress = true;
        SetInteractable(false);
        SetStatus("Clearing crafting slots...");

        try
        {
            PlayerInventoryData workingInventory = profile.Inventory.Clone();
            int shipCapacity = ShipCatalog.GetShipInventoryCapacity(selectedSkin);

            for (int i = 0; i < PlayerInventoryData.CraftingSlotCount; i++)
            {
                string itemId = workingInventory.RemoveFromCrafting(i);
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                if (workingInventory.TryAddToPlayer(itemId))
                    continue;

                if (workingInventory.TryAddToShip(itemId, shipCapacity))
                    continue;

                workingInventory.SetCrafting(i, itemId);
                SetStatus("No inventory space to clear crafting slots.");
                return false;
            }

            await PlayerProfileService.Instance.SaveInventorySnapshotAsync(workingInventory);
            if (showSuccessStatus)
                SetStatus("Crafting slots cleared.");
            else if (silentIfEmpty)
                SetStatus(string.Empty);

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Clear crafting slots failed: " + ex);
            SetStatus("Could not clear crafting slots.");
            return false;
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(!NetworkManager.SessionRequested || !SessionBrowserPanelUI.IsVisible);
            RefreshView();
        }
    }

    void RefreshInventoryView(PlayerInventoryData inventory)
    {
        PlayerInventoryData normalized = inventory != null ? inventory.Clone() : PlayerInventoryData.Default();
        normalized.Normalize();
        visiblePlayerInventorySlotMap = BuildVisiblePlayerInventorySlotMap(normalized);

        if (shipInventoryLabelText != null)
            shipInventoryLabelText.text = "SHIP INVENTORY (" + ShipCatalog.GetShipInventoryCapacity(selectedSkin) + ")";

        RebuildPlayerInventoryGrid(GetDisplayedPlayerInventorySlotCount(normalized));

        if (playerInventoryLabelText != null)
            playerInventoryLabelText.text = "PLAYER INVENTORY (" + normalized.PlayerSlots.Length + ")";

        RefreshPlayerInventoryFilterButton();

        RefreshInventoryButtons(shipInventoryButtons, shipInventoryTexts, shipInventoryIcons, normalized.ShipSlots, true);
        RefreshInventoryButtons(playerInventoryButtons, playerInventoryTexts, playerInventoryIcons, normalized.PlayerSlots, false);
        RefreshCraftingButtons(craftingSlotButtons, craftingSlotTexts, craftingSlotIcons, normalized.CraftingSlots);
    }

    void OnPlayerInventoryFilterClicked()
    {
        if (inventoryActionInProgress || panelObject == null || !panelObject.activeSelf)
            return;

        playerInventoryEquipableOnly = !playerInventoryEquipableOnly;
        HideItemPreview();
        RefreshView();
    }

    void RefreshPlayerInventoryFilterButton()
    {
        if (playerInventoryFilterButton == null)
            return;

        TMP_Text text = playerInventoryFilterButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = playerInventoryEquipableOnly ? "EQUIPABLE" : "ALL";

        Image image = playerInventoryFilterButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = playerInventoryEquipableOnly
                ? new Color(0.19f, 0.61f, 0.5f, 0.98f)
                : new Color(0.16f, 0.2f, 0.27f, 0.95f);
        }
    }

    int[] BuildVisiblePlayerInventorySlotMap(PlayerInventoryData inventory)
    {
        string[] slots = inventory != null ? inventory.PlayerSlots : null;
        if (slots == null)
            return Array.Empty<int>();

        if (!playerInventoryEquipableOnly)
        {
            int[] map = new int[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                map[i] = i;
            return map;
        }

        List<int> visibleSlots = new List<int>();
        for (int i = 0; i < slots.Length; i++)
        {
            string itemId = slots[i];
            if (IsEquipableInventoryItem(itemId))
                visibleSlots.Add(i);
        }

        return visibleSlots.ToArray();
    }

    int GetDisplayedPlayerInventorySlotCount(PlayerInventoryData inventory)
    {
        if (!playerInventoryEquipableOnly)
            return inventory != null && inventory.PlayerSlots != null ? inventory.PlayerSlots.Length : PlayerInventoryData.DefaultPlayerSlotCount;

        return Mathf.Max(1, visiblePlayerInventorySlotMap != null ? visiblePlayerInventorySlotMap.Length : 0);
    }

    bool IsEquipableInventoryItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (InventoryItemCatalog.GetItemType(itemId) != InventoryItemType.Equipment)
            return false;

        return InventoryItemCatalog.GetCategory(itemId) switch
        {
            InventoryItemCategory.Weapon => true,
            InventoryItemCategory.Shield => true,
            InventoryItemCategory.Engine => true,
            InventoryItemCategory.Gadget => true,
            _ => false
        };
    }

    void RefreshInventoryButtons(Button[] buttons, TMP_Text[] labels, Image[] icons, string[] slots, bool isShipInventory)
    {
        if (buttons == null || labels == null || icons == null || slots == null)
            return;

        int shipCapacity = ShipCatalog.GetShipInventoryCapacity(selectedSkin);

        for (int i = 0; i < buttons.Length; i++)
        {
            int slotIndex = isShipInventory ? i : ResolveVisiblePlayerInventorySlotIndex(i);
            bool visiblePlayerSlot = isShipInventory || slotIndex >= 0;
            bool withinShipCapacity = !isShipInventory || i < shipCapacity;
            if (buttons[i] != null)
                buttons[i].gameObject.SetActive(withinShipCapacity && visiblePlayerSlot);

            if (!withinShipCapacity || !visiblePlayerSlot || slotIndex >= slots.Length)
                continue;

            string itemId = slots[slotIndex];
            bool occupied = !string.IsNullOrWhiteSpace(itemId);
            bool isSafePocket = isShipInventory && PlayerProfileService.IsSafePocketIndex(selectedSkin, i);
            Image image = buttons[i] != null ? buttons[i].GetComponent<Image>() : null;
            Image icon = icons[i];
            Sprite itemSprite = occupied ? InventoryItemCatalog.GetIcon(itemId) : null;

            if (labels[i] != null)
            {
                bool useTextLabel = occupied && itemSprite == null;
                if (useTextLabel)
                {
                    labels[i].text = InventoryItemCatalog.GetShortLabel(itemId);
                    labels[i].color = new Color(0.97f, 0.99f, 1f, 1f);
                }
                else if (isSafePocket)
                {
                    labels[i].text = "SAFE";
                    labels[i].color = occupied ? new Color(0f, 0f, 0f, 0f) : new Color(0.56f, 1f, 0.95f, 0.86f);
                }
                else
                {
                    labels[i].text = string.Empty;
                    labels[i].color = new Color(0f, 0f, 0f, 0f);
                }
            }

            if (icon != null)
            {
                icon.sprite = itemSprite;
                icon.enabled = occupied && itemSprite != null;
            }

            if (image != null)
            {
                image.color = GetInventorySlotColor(itemId, occupied, isSafePocket);
            }

            if (buttons[i] != null)
            {
                Outline outline = buttons[i].GetComponent<Outline>();
                if (isSafePocket)
                {
                    if (outline == null)
                        outline = buttons[i].gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0.38f, 0.98f, 0.88f, 0.95f);
                    outline.effectDistance = new Vector2(3f, 3f);
                    outline.enabled = true;
                }
                else if (outline != null)
                {
                    outline.enabled = false;
                }
            }

            if (buttons[i] != null)
                buttons[i].interactable = !inventoryActionInProgress;
        }
    }

    Color GetInventorySlotColor(string itemId, bool occupied, bool isSafePocket)
    {
        if (occupied)
        {
            Color baseColor = InventoryItemCatalog.GetRarityColor(itemId);
            return isSafePocket
                ? Color.Lerp(baseColor, new Color(0.24f, 0.74f, 0.66f, baseColor.a), 0.32f)
                : baseColor;
        }

        return isSafePocket
            ? new Color(0.09f, 0.23f, 0.22f, 0.98f)
            : new Color(0.12f, 0.16f, 0.21f, 0.96f);
    }

    int ResolveVisiblePlayerInventorySlotIndex(int displayedSlotIndex)
    {
        if (!playerInventoryEquipableOnly)
            return displayedSlotIndex;

        if (visiblePlayerInventorySlotMap == null || displayedSlotIndex < 0 || displayedSlotIndex >= visiblePlayerInventorySlotMap.Length)
            return -1;

        return visiblePlayerInventorySlotMap[displayedSlotIndex];
    }

    void RefreshCraftingButtons(Button[] buttons, TMP_Text[] labels, Image[] icons, string[] slots)
    {
        if (buttons == null || labels == null || icons == null || slots == null)
            return;

        for (int i = 0; i < buttons.Length && i < slots.Length; i++)
        {
            string itemId = slots[i];
            bool occupied = !string.IsNullOrWhiteSpace(itemId);
            Image image = buttons[i] != null ? buttons[i].GetComponent<Image>() : null;
            Image icon = icons[i];
            Sprite itemSprite = occupied ? InventoryItemCatalog.GetIcon(itemId) : null;

            if (labels[i] != null)
            {
                bool useTextLabel = occupied && itemSprite == null;
                labels[i].text = useTextLabel ? InventoryItemCatalog.GetShortLabel(itemId) : string.Empty;
                labels[i].color = useTextLabel ? new Color(0.97f, 0.99f, 1f, 1f) : new Color(0f, 0f, 0f, 0f);
            }

            if (icon != null)
            {
                icon.sprite = itemSprite;
                icon.enabled = occupied && itemSprite != null;
            }

            if (image != null)
            {
                image.color = occupied
                    ? InventoryItemCatalog.GetRarityColor(itemId)
                    : new Color(0.12f, 0.16f, 0.21f, 0.96f);
            }

            if (buttons[i] != null)
                buttons[i].interactable = !inventoryActionInProgress;
        }

        if (craftButton != null)
            craftButton.interactable = !inventoryActionInProgress;
    }

    void RefreshLobbyUi()
    {
        LobbyManager lobby = FindAnyObjectByType<LobbyManager>();
        if (lobby != null)
        {
            lobby.ForceRefreshUi();
        }
    }

    void SetGameplayHudVisible(bool visible)
    {
        for (int i = 0; i < GameplayHudObjectNames.Length; i++)
        {
            string objectName = GameplayHudObjectNames[i];
            if (!gameplayHudObjectsByName.TryGetValue(objectName, out GameObject target) || target == null)
            {
                target = FindSceneObjectByName(objectName);
                if (target != null)
                    gameplayHudObjectsByName[objectName] = target;
            }

            if (target != null)
                ApplyHudVisibility(target, visible);
        }
    }

    GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject go = sceneObjects[i];
            if (go != null && go.name == objectName && go.scene.IsValid())
                return go;
        }

        return null;
    }

    void ApplyHudVisibility(GameObject hudObject, bool visible)
    {
        if (hudObject == null)
            return;

        RectTransform rectTransform = hudObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            CanvasGroup group = hudObject.GetComponent<CanvasGroup>();
            if (group == null)
                group = hudObject.AddComponent<CanvasGroup>();

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (!hudObject.activeSelf)
                hudObject.SetActive(true);
            return;
        }

        hudObject.SetActive(visible);
    }
}

public class SessionBrowserPanelUI : MonoBehaviour
{
    static SessionBrowserPanelUI instance;
    static bool visibleRequested;

    GameObject panelObject;
    TMP_Text statusText;
    TMP_Text emptyStateText;
    ScrollRect roomListScrollRect;
    RectTransform roomListContentRect;
    readonly List<GameObject> rowObjects = new List<GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("SessionBrowserPanelUI");
        instance = root.AddComponent<SessionBrowserPanelUI>();
        DontDestroyOnLoad(root);
    }

    public static bool IsVisible => visibleRequested && !PhotonNetwork.InRoom;

    public static void ShowBrowser()
    {
        visibleRequested = true;
        if (instance == null)
            return;

        instance.EnsurePanel();
        instance.RefreshStatus();
        instance.RebuildRoomList(NetworkManager.GetSessionRooms());
        instance.RefreshVisibility();
    }

    public static void HideBrowser()
    {
        visibleRequested = false;
        if (instance != null)
        {
            instance.RefreshVisibility();
        }
    }

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
        NetworkManager.SessionRoomListChanged += OnSessionRoomListChanged;
        NetworkManager.SessionBrowserStatusChanged += OnSessionBrowserStatusChanged;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        NetworkManager.SessionRoomListChanged -= OnSessionRoomListChanged;
        NetworkManager.SessionBrowserStatusChanged -= OnSessionBrowserStatusChanged;

        if (instance == this)
            instance = null;
    }

    void Update()
    {
        EnsurePanel();
        RefreshVisibility();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsurePanel();
        RefreshVisibility();
        if (IsVisible)
        {
            RebuildRoomList(NetworkManager.GetSessionRooms());
            RefreshStatus();
        }
    }

    void OnSessionRoomListChanged(IReadOnlyList<NetworkManager.SessionRoomEntry> rooms)
    {
        EnsurePanel();
        RebuildRoomList(rooms);
    }

    void OnSessionBrowserStatusChanged(string status)
    {
        EnsurePanel();
        RefreshStatus(status);
    }

    void EnsurePanel()
    {
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject == null)
            return;

        if (panelObject != null && panelObject.scene.IsValid())
        {
            if (panelObject.transform.parent != canvasObject.transform)
                panelObject.transform.SetParent(canvasObject.transform, false);

            return;
        }

        CreatePanel(canvasObject.transform);
        RefreshStatus();
        RebuildRoomList(NetworkManager.GetSessionRooms());
    }

    void CreatePanel(Transform parent)
    {
        panelObject = new GameObject("SessionBrowserPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image background = panelObject.GetComponent<Image>();
        background.color = new Color(0.03f, 0.05f, 0.08f, 0.98f);

        CreateText(panelObject.transform, "BrowserTitle", "ACTIVE ROUNDS", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(700f, 40f), 34f, TextAlignmentOptions.Center);
        CreateText(panelObject.transform, "BrowserSubtitle", "Choose an existing session or create a new multiplayer round.", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(920f, 28f), 18f, TextAlignmentOptions.Center);

        statusText = CreateText(panelObject.transform, "BrowserStatus", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -134f), new Vector2(900f, 26f), 17f, TextAlignmentOptions.Center);
        statusText.fontStyle = FontStyles.Normal;
        statusText.color = new Color(0.7f, 0.84f, 0.93f, 0.95f);

        Button backButton = CreateButton(panelObject.transform, "BrowserBackButton", "BACK", new Vector2(-320f, -154f), new Vector2(170f, 54f), OnBackClicked);
        StyleButton(backButton, new Color(0.18f, 0.22f, 0.29f, 0.95f), new Color(0.24f, 0.29f, 0.37f, 1f));

        Button refreshButton = CreateButton(panelObject.transform, "BrowserRefreshButton", "REFRESH", new Vector2(-120f, -154f), new Vector2(170f, 54f), OnRefreshClicked);
        StyleButton(refreshButton, new Color(0.16f, 0.36f, 0.44f, 0.95f), new Color(0.2f, 0.46f, 0.56f, 1f));

        Button newRoundButton = CreateButton(panelObject.transform, "BrowserNewRoundButton", "NEW ROUND", new Vector2(140f, -154f), new Vector2(250f, 60f), OnNewRoundClicked);
        StyleButton(newRoundButton, new Color(0.12f, 0.44f, 0.27f, 0.98f), new Color(0.16f, 0.56f, 0.34f, 1f));

        GameObject viewportObject = new GameObject("RoomListViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(panelObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = new Vector2(0f, -96f);
        viewportRect.sizeDelta = new Vector2(1240f, 610f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.08f, 0.11f, 0.15f, 0.9f);

        GameObject contentObject = new GameObject("RoomListContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        roomListContentRect = contentObject.GetComponent<RectTransform>();
        roomListContentRect.anchorMin = new Vector2(0f, 1f);
        roomListContentRect.anchorMax = new Vector2(1f, 1f);
        roomListContentRect.pivot = new Vector2(0.5f, 1f);
        roomListContentRect.anchoredPosition = Vector2.zero;
        roomListContentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        roomListScrollRect = viewportObject.GetComponent<ScrollRect>();
        roomListScrollRect.horizontal = false;
        roomListScrollRect.vertical = true;
        roomListScrollRect.movementType = ScrollRect.MovementType.Clamped;
        roomListScrollRect.viewport = viewportRect;
        roomListScrollRect.content = roomListContentRect;
        roomListScrollRect.scrollSensitivity = 38f;

        emptyStateText = CreateText(panelObject.transform, "RoomListEmpty", "No sessions are visible right now. Create a new round to start one.", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(760f, 40f), 20f, TextAlignmentOptions.Center);
        emptyStateText.fontStyle = FontStyles.Normal;
        emptyStateText.color = new Color(0.72f, 0.79f, 0.87f, 0.92f);
    }

    void RefreshVisibility()
    {
        if (panelObject == null)
            return;

        bool shouldShow = visibleRequested && !PhotonNetwork.InRoom;
        panelObject.SetActive(shouldShow);
        CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = shouldShow ? 1f : 0f;
            canvasGroup.interactable = shouldShow;
            canvasGroup.blocksRaycasts = shouldShow;
        }

        if (shouldShow)
        {
            panelObject.transform.SetAsLastSibling();
        }
    }

    void RefreshStatus()
    {
        RefreshStatus(NetworkManager.GetSessionBrowserStatus());
    }

    void RefreshStatus(string status)
    {
        if (statusText == null)
            return;

        statusText.text = string.IsNullOrWhiteSpace(status)
            ? "Choose a round or create a new one."
            : status;
    }

    void RebuildRoomList(IReadOnlyList<NetworkManager.SessionRoomEntry> rooms)
    {
        if (roomListContentRect == null)
            return;

        for (int i = 0; i < rowObjects.Count; i++)
        {
            if (rowObjects[i] != null)
                Destroy(rowObjects[i]);
        }

        rowObjects.Clear();

        bool hasRooms = rooms != null && rooms.Count > 0;
        if (emptyStateText != null)
            emptyStateText.gameObject.SetActive(!hasRooms);

        if (!hasRooms)
            return;

        for (int i = 0; i < rooms.Count; i++)
        {
            NetworkManager.SessionRoomEntry room = rooms[i];
            GameObject rowObject = CreateSessionRow(room);
            rowObjects.Add(rowObject);
        }

        Canvas.ForceUpdateCanvases();
        if (roomListScrollRect != null)
            roomListScrollRect.verticalNormalizedPosition = 1f;
    }

    GameObject CreateSessionRow(NetworkManager.SessionRoomEntry room)
    {
        GameObject rowObject = new GameObject("RoomRow_" + room.RoomName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        rowObject.transform.SetParent(roomListContentRect, false);

        RectTransform rect = rowObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 132f);

        LayoutElement layout = rowObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 132f;

        Image image = rowObject.GetComponent<Image>();
        image.color = room.CanJoin
            ? new Color(0.14f, 0.18f, 0.24f, 0.98f)
            : new Color(0.11f, 0.12f, 0.14f, 0.98f);

        Button button = rowObject.GetComponent<Button>();
        button.interactable = room.CanJoin;
        button.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            OnRoomClicked(room.RoomName);
        });

        TMP_Text titleText = CreateText(rowObject.transform, "RoomTitle", room.DisplayName, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(560f, 34f), 24f, TextAlignmentOptions.Left);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.pivot = new Vector2(0f, 0.5f);
        TMP_Text metaText = CreateText(rowObject.transform, "RoomMeta", BuildMetaLine(room), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -62f), new Vector2(720f, 28f), 16f, TextAlignmentOptions.Left);
        RectTransform metaRect = metaText.rectTransform;
        metaRect.pivot = new Vector2(0f, 0.5f);
        metaText.fontStyle = FontStyles.Normal;
        metaText.color = new Color(0.72f, 0.81f, 0.9f, 0.95f);

        TMP_Text stateText = CreateText(rowObject.transform, "RoomState", BuildStateLabel(room), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-220f, -26f), new Vector2(180f, 34f), 18f, TextAlignmentOptions.Center);
        RectTransform stateRect = stateText.rectTransform;
        stateRect.pivot = new Vector2(1f, 0.5f);
        stateText.color = room.BlockedByLocalDeath
            ? new Color(0.95f, 0.38f, 0.34f, 1f)
            : room.State == RoomSettings.SessionStateInPlay
            ? new Color(0.94f, 0.75f, 0.33f, 1f)
            : new Color(0.38f, 0.83f, 0.62f, 1f);

        TMP_Text joinText = CreateText(rowObject.transform, "RoomJoin", BuildJoinLabel(room), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-84f, -28f), new Vector2(112f, 40f), 18f, TextAlignmentOptions.Center);
        RectTransform joinRect = joinText.rectTransform;
        joinRect.pivot = new Vector2(1f, 0.5f);
        joinText.color = room.CanJoin
            ? new Color(0.95f, 0.98f, 1f, 1f)
            : new Color(0.62f, 0.64f, 0.68f, 1f);

        return rowObject;
    }

    string BuildMetaLine(NetworkManager.SessionRoomEntry room)
    {
        string meta = "Host: " + room.HostName + "    Players: " + room.PlayerCount + "/" + room.MaxPlayers;
        if (room.RemainingTimeSeconds.HasValue)
        {
            meta += "    Remaining: " + FormatDuration(room.RemainingTimeSeconds.Value);
        }

        if (room.BlockedByLocalDeath)
            meta += "    You cannot rejoin this round.";

        return meta;
    }

    string BuildStateLabel(NetworkManager.SessionRoomEntry room)
    {
        if (room.BlockedByLocalDeath)
            return string.IsNullOrWhiteSpace(room.BlockReason) ? "ENDED" : room.BlockReason;

        if (room.State == RoomSettings.SessionStateInPlay)
            return "IN PLAY";

        return "IN LOBBY";
    }

    string BuildJoinLabel(NetworkManager.SessionRoomEntry room)
    {
        if (room.BlockedByLocalDeath)
            return "ENDED";

        return room.CanJoin ? "JOIN" : "FULL";
    }

    string FormatDuration(float seconds)
    {
        int clamped = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = clamped / 60;
        int remainingSeconds = clamped % 60;
        return minutes.ToString("00") + ":" + remainingSeconds.ToString("00");
    }

    void OnBackClicked()
    {
        NetworkManager.CancelSessionStart();
        HideBrowser();
    }

    void OnRefreshClicked()
    {
        NetworkManager.RefreshSessionBrowser();
    }

    void OnNewRoundClicked()
    {
        NetworkManager.CreateNewRound();
    }

    void OnRoomClicked(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return;

        NetworkManager.JoinSession(roomName);
    }

    Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, Vector2 size, Action onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            onClick?.Invoke();
        });

        CreateText(buttonObject.transform, objectName + "Text", label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 18f, TextAlignmentOptions.Center);
        return button;
    }

    void StyleButton(Button button, Color normalColor, Color highlightedColor)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = normalColor;

        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.selectedColor = highlightedColor;
        colors.highlightedColor = highlightedColor;
        colors.pressedColor = Color.Lerp(highlightedColor, Color.black, 0.15f);
        colors.disabledColor = new Color(0.26f, 0.28f, 0.31f, 0.8f);
        button.colors = colors;
    }

    TMP_Text CreateText(Transform parent, string objectName, string value, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.94f, 0.97f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontStyle = FontStyles.Bold;

        TMP_Text reference = FindAnyObjectByType<TextMeshProUGUI>();
        if (reference != null)
        {
            text.font = reference.font;
            text.fontSharedMaterial = reference.fontSharedMaterial;
        }

        return text;
    }
}

public class ProfileInventorySlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public PlayerProfilePanelUI owner;
    public bool isPlayerInventory;
    public int slotIndex;

    public void OnBeginDrag(PointerEventData eventData)
    {
        owner?.BeginSlotDrag(isPlayerInventory, slotIndex, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner?.UpdateSlotDrag(isPlayerInventory, slotIndex, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.EndSlotDrag(isPlayerInventory, slotIndex, eventData);
    }
}

public class ProfileEquipmentSlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public PlayerProfilePanelUI owner;
    public int slotIndex;

    public void OnBeginDrag(PointerEventData eventData)
    {
        owner?.BeginEquipmentSlotDrag(slotIndex, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner?.UpdateEquipmentSlotDrag(slotIndex, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.EndEquipmentSlotDrag(slotIndex, eventData);
    }
}

public class ProfileCraftingSlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public PlayerProfilePanelUI owner;
    public int slotIndex;

    public void OnBeginDrag(PointerEventData eventData)
    {
        owner?.BeginCraftingSlotDrag(slotIndex, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner?.UpdateCraftingSlotDrag(slotIndex, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.EndCraftingSlotDrag(slotIndex, eventData);
    }
}

public class ProfileShipSelectionSwipeHandler : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    public PlayerProfilePanelUI owner;
    Vector2 dragStartPosition;

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragStartPosition = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - dragStartPosition;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            owner?.OnShipSelectionSwiped(delta.x);
    }
}
