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

public class PlayerProfilePanelUI : MonoBehaviour
{
    static readonly ProfilerMarker SwitchScreenMarker = new ProfilerMarker("ProfileMenu.SwitchToScreen");
    static readonly ProfilerMarker ShopRefreshMarker = new ProfilerMarker("ProfileMenu.Shop.Refresh");
    static readonly ProfilerMarker CraftingRecipeRefreshMarker = new ProfilerMarker("ProfileMenu.CraftingRecipes.Refresh");
    static readonly ProfilerMarker CraftingBlueprintRefreshMarker = new ProfilerMarker("ProfileMenu.CraftingBlueprints.Refresh");
    static readonly ProfilerMarker ProjectsRefreshMarker = new ProfilerMarker("ProfileMenu.Projects.Refresh");
    static readonly ProfilerMarker ProjectDetailsRefreshMarker = new ProfilerMarker("ProfileMenu.ProjectDetails.Refresh");
    static readonly ProfilerMarker ShipSelectionRefreshMarker = new ProfilerMarker("ProfileMenu.ShipSelection.Refresh");
    static readonly ProfilerMarker PilotSelectionRefreshMarker = new ProfilerMarker("ProfileMenu.PilotSelection.Refresh");

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
        ShipSelection,
        PilotSelection,
        Projects,
        ProjectDetails
    }

    enum TraderShopKind
    {
        None,
        IronJoe,
        MissGadget,
        DirtySam,
        MissEnigma
    }

    enum ShopSortMode
    {
        Alphabetical,
        Price,
        Type,
        Rarity
    }

    enum PlayerInventoryFilterMode
    {
        All,
        Equipable,
        CustomEquipmentSlot
    }

    enum PlayerInventorySortMode
    {
        Alphabetical,
        Price,
        Rarity,
        Type
    }

    sealed class TraderDefinition
    {
        public readonly TraderShopKind Kind;
        public readonly string DisplayName;
        public readonly string ResourcePath;
        public readonly string EditorPreferredPath;
        public readonly string EditorFallbackPath;
        public readonly bool OpensShop;

        public TraderDefinition(
            TraderShopKind kind,
            string displayName,
            string resourcePath,
            string editorPreferredPath,
            string editorFallbackPath,
            bool opensShop)
        {
            Kind = kind;
            DisplayName = displayName;
            ResourcePath = resourcePath;
            EditorPreferredPath = editorPreferredPath;
            EditorFallbackPath = editorFallbackPath;
            OpensShop = opensShop;
        }
    }

    sealed class CraftingRecipeRowView
    {
        public string RecipeId;
        public GameObject Root;
        public Button ResultButton;
        public Image ResultButtonImage;
        public Outline ResultOutline;
        public Shadow ResultShadow;
        public RectTransform[] ResultFrameRects;
        public Image[] ResultFrameImages;
        public GameObject[] MissingIngredientFrames;
        public TMP_Text ResultPriceText;
        public TMP_Text ResultLockText;
    }

    sealed class ShopCardView
    {
        public GameObject Root;
        public Button CardButton;
        public Image CardImage;
        public Image IconImage;
        public TMP_Text NameText;
        public TMP_Text TypeText;
        public Image PriceIcon;
        public TMP_Text PriceText;
        public Button BuyButton;
        public TMP_Text BuyText;
    }

    sealed class ShopRowView
    {
        public GameObject Root;
        public ShopCardView Left;
        public ShopCardView Right;
    }

    sealed class ShopOfferViewModel
    {
        public InventoryItemDefinition Definition;
        public int Price;
        public bool CanAfford;
    }

    sealed class MissEnigmaOfferViewModel
    {
        public BlueprintTradeOffer Offer;
        public bool CanAfford;
        public InventoryItemDefinition BlueprintDefinition;
        public InventoryItemDefinition TargetDefinition;
        public int EstimatedTradeValue;
    }

    sealed class MissEnigmaCardView
    {
        public GameObject Root;
        public Button CardButton;
        public Image CardImage;
        public Image IconImage;
        public TMP_Text NameText;
        public TMP_Text CostText;
        public Button TradeButton;
        public TMP_Text TradeText;
    }

    sealed class MissEnigmaRowView
    {
        public GameObject Root;
        public MissEnigmaCardView Left;
        public MissEnigmaCardView Right;
    }

    static readonly string[] GameplayHudObjectNames =
    {
        "JoystickBG",
        "ShootJoystickBG",
        "CollectButton",
        "ShipInventoryButton",
        "ShipInventoryPanel",
        "ComplexAmmoBar",
        "SuperAttackJoystickBG",
        "TimerText",
        "HP_Bar",
        "Shield_Bar",
        "VitalsIconHud",
        "Booster_Bar",
        "ScoreText"
    };

    static readonly ShipType[] SelectableShipTypes =
    {
        ShipType.Explorer,
        ShipType.Viper,
        ShipType.Avenger,
        ShipType.Arrow,
        ShipType.Invader,
        ShipType.CargoTruck,
        ShipType.Pathfinder
    };

    static readonly Vector2 ShipPreviewImagePosition = new Vector2(0f, 22f);
    static readonly Vector2 PlayerInventoryGridPosition = new Vector2(-938f, -578f);
    static readonly Vector2 PlayerInventoryViewportSize = new Vector2(830f, 362f);
    static readonly Vector2 PlayerInventoryFilterButtonSize = new Vector2(206f, 56f);
    static readonly Vector2 PlayerInventorySortButtonSize = new Vector2(190f, 56f);
    static readonly Vector2 PlayerInventoryExtendButtonSize = new Vector2(172f, 56f);
    static readonly Vector2 ShipInventoryHeaderButtonSize = new Vector2(164f, 50f);
    static readonly Vector2 ItemPreviewInfoButtonSize = new Vector2(158f, 54f);
    const int PlayerInventoryGridColumns = 6;
    const float InventoryUtilityButtonLift = 24f;
    const float InventoryUtilityButtonGap = 16f;
    const float InventoryUtilityLabelGap = 8f;
    const float ShipInventoryHeaderGap = 18f;
    const float InventoryExtendButtonOffsetX = 0f;
    const float PlayerInventoryUtilityButtonFontSize = 18f;
    const float ShipInventoryHeaderFontSize = 22f;
    const float ShipInventoryHeaderButtonFontSize = 20f;
    const float BlueprintTabFontSize = 24f;
    const float InventoryDropTargetPadding = 28f;
    const string PlayerInventoryTitleText = "INVENTORY";
    static readonly Vector2[] EquipmentSlotLayoutPositions =
    {
        new Vector2(-520f, -28f),
        new Vector2(-386f, -28f),
        new Vector2(-520f, -144f),
        new Vector2(-386f, -144f),
        new Vector2(-520f, -260f),
        new Vector2(-386f, -260f),
        new Vector2(-520f, -608f),
        new Vector2(-386f, -608f),
        new Vector2(-520f, -376f),
        new Vector2(-386f, -376f),
        new Vector2(-520f, -492f),
        new Vector2(-386f, -492f)
    };
    const float EquipmentSlotPreviewSize = 104f;
    const float EquipmentSlotPreviewIconSize = 88f;
    const float EquipmentSlotPreviewFontSize = 17f;
    static readonly string[] ShipStatLabels =
    {
        "HP",
        "SHIELD",
        "SPEED",
        "TURN",
        "BOOST",
        "MAX BOOST",
        "CARGO",
        "SAFE",
        "DRIFT"
    };
    static readonly Vector2 PilotSelectionLeftPosition = new Vector2(-560f, -190f);
    static readonly Vector2 PilotSelectionCenterPosition = new Vector2(0f, -162f);
    static readonly Vector2 PilotSelectionRightPosition = new Vector2(560f, -190f);
    static readonly Vector2 PilotSelectionSideSize = new Vector2(440f, 540f);
    static readonly Vector2 PilotSelectionCenterSize = new Vector2(560f, 620f);
    static readonly Vector2 PilotSelectionSideImageOffsetMin = new Vector2(18f, 50f);
    static readonly Vector2 PilotSelectionSideImageOffsetMax = new Vector2(-18f, -24f);
    static readonly Vector2 PilotSelectionCenterImageOffsetMin = new Vector2(22f, 58f);
    static readonly Vector2 PilotSelectionCenterImageOffsetMax = new Vector2(-22f, -28f);
    static readonly Vector2 ShipSelectionLeftPosition = new Vector2(-590f, -88f);
    static readonly Vector2 ShipSelectionCenterPosition = new Vector2(0f, -72f);
    static readonly Vector2 ShipSelectionRightPosition = new Vector2(590f, -88f);
    static readonly Vector2 ShipSelectionSideSize = new Vector2(640f, 800f);
    static readonly Vector2 ShipSelectionCenterSize = new Vector2(920f, 920f);
    static readonly Vector2 ShipSelectionSideImagePosition = new Vector2(30f, -48f);
    static readonly Vector2 ShipSelectionCenterImagePosition = new Vector2(48f, -64f);
    static readonly Vector2 ShipSelectionSideImageSize = new Vector2(470f, 540f);
    static readonly Vector2 ShipSelectionCenterImageSize = new Vector2(680f, 760f);
    const float SelectionCarouselSnapDuration = 0.16f;
    const float SelectionCarouselAxisThreshold = 12f;
    const float SelectionCarouselClickCancelThreshold = 18f;
    const float SelectionCarouselSnapThreshold = 0.32f;
    const float SelectionCarouselEdgeResistance = 0.32f;
    const float SelectionCarouselMaxOffset = 1.08f;
    const int SelectionCarouselClickSuppressFrames = 2;

    static readonly TraderDefinition[] TraderDefinitions =
    {
        new TraderDefinition(
            TraderShopKind.IronJoe,
            "IRON JOE",
            "UI/Traders/military_trader",
            "Assets/Resources/UI/Traders/military_trader.png",
            "Assets/military_trader.png",
            true),
        new TraderDefinition(
            TraderShopKind.MissGadget,
            "MISS GADGET",
            "UI/Traders/gadget_trader",
            "Assets/Resources/UI/Traders/gadget_trader.png",
            "Assets/gadget_trader.png",
            true),
        new TraderDefinition(
            TraderShopKind.DirtySam,
            "DIRTY SAM",
            "UI/Traders/resources_trader",
            "Assets/Resources/UI/Traders/resources_trader.png",
            "Assets/resources_trader.png",
            false),
        new TraderDefinition(
            TraderShopKind.MissEnigma,
            "ENIGMA",
            "UI/Traders/exotic_trader",
            "Assets/Resources/UI/Traders/exotic_trader.png",
            "Assets/exotic_trader.png",
            true)
    };

    static PlayerProfilePanelUI instance;
    readonly Dictionary<string, GameObject> gameplayHudObjectsByName = new Dictionary<string, GameObject>();

    GameObject panelObject;
    Transform cachedCanvasTransform;
    bool profilePanelVisibilityInitialized;
    bool profilePanelVisible;
    bool profileDuplicatePanelsChecked;
    TMP_InputField nicknameInput;
    TMP_Text accountText;
    TMP_Text statusText;
    TMP_Text gamesPlayedText;
    TMP_Text totalXpText;
    TMP_Text astronsText;
    TMP_Text inventoryHintText;
    Button saveAndRunButton;
    Button shopButton;
    Button exitGameButton;
    Button[] shipTypeButtons;
    Button[] skinButtons;
    Button[] shipInventoryButtons;
    Button[] playerInventoryButtons;
    Button shipInventoryUnloadButton;
    Button playerInventoryExtendButton;
    Button playerInventoryFilterButton;
    Button playerInventorySortButton;
    TMP_Text[] shipInventoryTexts;
    TMP_Text[] playerInventoryTexts;
    Image[] shipInventoryIcons;
    Image[] playerInventoryIcons;
    ScrollRect playerInventoryScrollRect;
    RectTransform playerInventoryContentRect;
    GameObject playerInventoryScrollbarObject;
    int builtPlayerInventorySlotCount = -1;
    PlayerInventoryFilterMode playerInventoryFilterMode = PlayerInventoryFilterMode.All;
    PlayerInventorySortMode playerInventorySortMode = PlayerInventorySortMode.Alphabetical;
    int customPlayerInventoryEquipmentSlotIndex = -1;
    bool resetPlayerInventoryScrollOnNextRefresh;
    int[] visiblePlayerInventorySlotMap = Array.Empty<int>();
    TMP_Text shipTypeLabelText;
    TMP_Text shipSkinLabelText;
    TMP_Text shipInventoryLabelText;
    TMP_Text playerInventoryLabelText;
    TMP_Text playerInventoryCountText;
    GameObject playerInventoryExtendConfirmObject;
    TMP_Text playerInventoryExtendConfirmText;
    Button playerInventoryExtendConfirmButton;
    Button playerInventoryExtendCancelButton;
    GameObject shipInventoryStartConfirmObject;
    TMP_Text shipInventoryStartConfirmText;
    Button shipInventoryStartConfirmYesButton;
    Button shipInventoryStartConfirmNoButton;
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
    Button itemPreviewInfoButton;
    Button itemPreviewSellButton;
    Button itemPreviewSalvageButton;
    GameObject itemInfoOverlayObject;
    Image itemInfoIcon;
    TMP_Text itemInfoTitleText;
    TMP_Text itemInfoTypeText;
    TMP_Text itemInfoPriceText;
    TMP_Text itemInfoSalvageText;
    TMP_Text itemInfoRecipeText;
    TMP_Text itemInfoDescriptionText;
    Button itemInfoCloseButton;
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
    Button craftingRecipeAvailabilityButton;
    Button craftingRecipeBlueprintsButton;
    GameObject craftingBlueprintBrowserObject;
    ScrollRect craftingBlueprintScrollRect;
    RectTransform craftingBlueprintContentRect;
    RectTransform craftingBlueprintBrowserPanelRect;
    RectTransform craftingBlueprintViewportRect;
    RectTransform craftingBlueprintScrollbarRect;
    GridLayoutGroup craftingBlueprintGridLayout;
    TMP_Text craftingBlueprintTitleText;
    Button craftingBlueprintCloseButton;
    readonly List<GameObject> craftingRecipeRowObjects = new List<GameObject>();
    readonly List<Button> craftingRecipeResultButtons = new List<Button>();
    readonly List<GameObject> craftingBlueprintRowObjects = new List<GameObject>();
    readonly Dictionary<string, GameObject> craftingBlueprintRowsById = new Dictionary<string, GameObject>(StringComparer.Ordinal);
    readonly Dictionary<string, CraftingRecipeRowView> craftingRecipeRowsById = new Dictionary<string, CraftingRecipeRowView>(StringComparer.Ordinal);
    bool craftingRecipeShowAvailableOnly;
    bool resetCraftingRecipeScrollOnNextRefresh = true;
    bool craftingRecipeBrowserSignatureValid;
    string craftingRecipeBrowserSignature = string.Empty;
    GameObject shopBrowserObject;
    ScrollRect shopScrollRect;
    RectTransform shopContentRect;
    TMP_Text shopAstronsText;
    Button shopCloseButton;
    Button shopSortButton;
    readonly List<GameObject> shopRowObjects = new List<GameObject>();
    readonly List<ShopRowView> shopRowPool = new List<ShopRowView>();
    readonly List<MissEnigmaRowView> missEnigmaShopRowPool = new List<MissEnigmaRowView>();
    GameObject missEnigmaEmptyRowObject;
    TMP_Text missEnigmaEmptyText;
    TraderShopKind selectedTraderShop = TraderShopKind.None;
    readonly Dictionary<TraderShopKind, ShopSortMode> shopSortModesByTrader = new Dictionary<TraderShopKind, ShopSortMode>();
    readonly Dictionary<TraderShopKind, Button> traderButtonsByKind = new Dictionary<TraderShopKind, Button>();
    readonly Dictionary<TraderShopKind, Image> traderCardImagesByKind = new Dictionary<TraderShopKind, Image>();
    readonly Dictionary<TraderShopKind, Outline> traderOutlinesByKind = new Dictionary<TraderShopKind, Outline>();
    GameObject splashScreenObject;
    Image splashScreenImage;
    float splashHideTime;
    static bool splashShownOnce;
    int selectedSkin;
    ProfileScreen currentScreen = ProfileScreen.Home;
    GameObject topBarRootObject;
    GameObject topStatBannerObject;
    SharedPlayerTopBarUI sharedTopBar;
    GameObject leftNavigationRootObject;
    GameObject rightActionRootObject;
    GameObject homeViewRootObject;
    GameObject storageViewRootObject;
    GameObject shipWorkspaceRootObject;
    GameObject inventoryViewRootObject;
    GameObject craftingViewRootObject;
    GameObject traderViewRootObject;
    GameObject projectsViewRootObject;
    GameObject projectDetailsViewObject;
    GameObject shipSelectionViewObject;
    GameObject pilotSelectionViewObject;
    GameObject traderFuturePanelObject;
    Button navCraftingButton;
    Button navTraderButton;
    Button navInventoryButton;
    Button navBackButton;
    TMP_Text shipSelectionTitleText;
    TMP_Text shipSelectionSubtitleText;
    TMP_Text shipSelectionStatusText;
    Button shipSelectionDetailsButton;
    GameObject shipMissionDetailsPanelObject;
    TMP_Text shipMissionDetailsTitleText;
    TMP_Text shipMissionDetailsBodyText;
    RectTransform shipMissionDetailsContentRect;
    LayoutElement shipMissionDetailsTextLayoutElement;
    Button shipMissionDetailsCloseButton;
    Button shipSelectionBackButton;
    Button shipSelectionPrevButton;
    Button shipSelectionNextButton;
    Button[] shipSelectionSkinButtons;
    TMP_Text shipSelectionSkinLabelText;
    GameObject[] shipSelectionCardObjects;
    Image[] shipSelectionCardImages;
    TMP_Text[] shipSelectionCardTitles;
    TMP_Text[] shipSelectionCardLockTexts;
    Button[] shipSelectionCardDonateButtons;
    TMP_Text[][] shipSelectionCardStatLabelTexts;
    TMP_Text[][] shipSelectionCardStatValueTexts;
    Image[][] shipSelectionCardStatFillImages;
    GameObject[][] shipSelectionCardSlotObjects;
    int shipSelectionCenterIndex;
    ShipType shipSelectionCenterType = ShipType.Explorer;
    readonly Dictionary<ShipType, int> shipSelectionSkinByType = new Dictionary<ShipType, int>();
    bool shipSelectionDragActive;
    bool shipSelectionDragHorizontal;
    Vector2 shipSelectionDragStartLocal;
    float shipSelectionDragStartOffset;
    float shipSelectionVisualOffset;
    bool shipSelectionSnapActive;
    float shipSelectionSnapElapsed;
    float shipSelectionSnapStartOffset;
    float shipSelectionSnapTargetOffset;
    int shipSelectionSnapIndexDelta;
    int shipSelectionSuppressClickUntilFrame;
    GameObject pilotPortraitRootObject;
    Button pilotPortraitButton;
    Image pilotPortraitImage;
    TMP_Text pilotPortraitNameText;
    TMP_Text pilotPortraitCaptionText;
    GameObject projectsButtonRootObject;
    Button projectsButton;
    Image projectsButtonImage;
    TMP_Text projectsButtonCaptionText;
    TMP_Text pilotSelectionTitleText;
    TMP_Text pilotSelectionAbilitiesText;
    TMP_Text pilotSelectionStatusText;
    Button pilotSelectionBackButton;
    Button pilotSelectionPrevButton;
    Button pilotSelectionNextButton;
    GameObject[] pilotSelectionCardObjects;
    Image[] pilotSelectionCardImages;
    TMP_Text[] pilotSelectionCardNames;
    TMP_Text[] pilotSelectionCardLockTexts;
    int pilotSelectionCenterIndex;
    string selectedPilotId = PilotCatalog.JakeId;
    bool pilotSelectionDragActive;
    bool pilotSelectionDragHorizontal;
    Vector2 pilotSelectionDragStartLocal;
    float pilotSelectionDragStartOffset;
    float pilotSelectionVisualOffset;
    bool pilotSelectionSnapActive;
    float pilotSelectionSnapElapsed;
    float pilotSelectionSnapStartOffset;
    float pilotSelectionSnapTargetOffset;
    int pilotSelectionSnapIndexDelta;
    int pilotSelectionSuppressClickUntilFrame;
    readonly Dictionary<string, Sprite> spriteCacheByResourcePath = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    readonly HashSet<string> missingSpriteResources = new HashSet<string>(StringComparer.Ordinal);
    readonly Dictionary<string, Sprite> grayscalePilotPortraitCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    readonly List<Button> projectTileButtons = new List<Button>();
    readonly List<Button> projectStageTabButtons = new List<Button>();
    readonly List<Button> projectStepButtons = new List<Button>();
    readonly List<Image> projectStepIcons = new List<Image>();
    readonly List<TMP_Text> projectStepTexts = new List<TMP_Text>();
    readonly List<GameObject> projectStepCheckBoxes = new List<GameObject>();
    TMP_Text projectsTitleText;
    TMP_Text projectDetailsTitleText;
    GameObject projectDescriptionPanelObject;
    ScrollRect projectDescriptionScrollRect;
    RectTransform projectDescriptionContentRect;
    TMP_Text projectDescriptionText;
    LayoutElement projectDescriptionTextLayoutElement;
    string projectDescriptionScrollKey = string.Empty;
    bool projectDescriptionScrollResetPending;
    GameObject projectRewardsPanelObject;
    TMP_Text projectRewardsText;
    TMP_Text projectStatusText;
    GameObject projectCommitPanelObject;
    TMP_Text projectCommitTitleText;
    TMP_Text projectCommitAvailableText;
    TMP_Text projectCommitAmountText;
    Button projectCommitMinusButton;
    Button projectCommitPlusButton;
    Button projectCommitButton;
    Button projectRewardClaimButton;
    string selectedProjectId = ProjectCatalog.SupplyToSurviveId;
    int selectedProjectStageIndex;
    int selectedProjectStepIndex = -1;
    int projectCommitAmount;
    bool inventoryActionInProgress;
    bool inventoryControlsInteractable = true;
    bool preserveInventoryButtonVisualsDuringSave;
    bool suppressNextProfileChangedRefresh;
    bool suppressNextInventoryClick;
    bool dragInProgress;
    bool profileLayoutDirty = true;
    bool skinVisualsDirty = true;
    bool saveAndRunStyleDirty = true;
    bool itemPreviewLayoutDirty = true;
    bool shipSelectionDirty = true;
    bool pilotSelectionDirty = true;
    bool projectsDirty = true;
    bool projectDetailsDirty = true;
    bool lastSplashShowing;
    bool interactableStateInitialized;
    bool lastAppliedInteractable;
    bool lastAppliedInventoryActionInProgress;
    bool lastAppliedPreserveInventoryButtonVisualsDuringSave;
    GameObject dragVisualObject;
    Image dragVisualIcon;
    TMP_Text dragVisualLabel;
    ProfileItemSource previewSource = ProfileItemSource.None;
    int previewSlotIndex = -1;
    string previewItemId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("PlayerProfilePanelUI");
        instance = root.AddComponent<PlayerProfilePanelUI>();
        DontDestroyOnLoad(root);
    }

    public static async Task PrewarmAsync()
    {
        if (!Application.isPlaying)
            return;

        EnsureInstance();
        await PlayerProfileService.Instance.EnsureInitializedAsync();
        if (instance == null)
            return;

        instance.PrewarmProfileAssets();
        instance.EnsurePanel();
        instance.RefreshView();
        instance.MarkAllProfileUiDirty();
        instance.RefreshVisibility();
    }

    public static void RefreshOpenPanel()
    {
        if (instance == null)
            return;

        instance.MarkAllProfileUiDirty();
        instance.RefreshView();
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
        if (saveAndRunButton != null)
            saveAndRunButton.transform.SetParent(rightActionRootObject.transform, false);

        CreateTraderFuturePanel(traderViewRootObject.transform);
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

    void CreateTraderFuturePanel(Transform parent)
    {
        traderFuturePanelObject = new GameObject("TraderFuturePanel", typeof(RectTransform), typeof(Image));
        traderFuturePanelObject.transform.SetParent(parent, false);
        RectTransform rect = traderFuturePanelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(632f, -262f);
        rect.sizeDelta = new Vector2(420f, 736f);

        Image image = traderFuturePanelObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;

        traderButtonsByKind.Clear();
        traderCardImagesByKind.Clear();
        traderOutlinesByKind.Clear();

        for (int i = 0; i < TraderDefinitions.Length; i++)
        {
            TraderDefinition definition = TraderDefinitions[i];
            float x = (i % 2 == 0) ? -104f : 104f;
            float y = -8f - ((i / 2) * 346f);
            CreateTraderPortraitButton(definition, new Vector2(x, y), new Vector2(196f, 330f));
        }

        RefreshTraderSelectionVisuals();
    }

    void CreateTraderPortraitButton(TraderDefinition definition, Vector2 anchoredPosition, Vector2 size)
    {
        if (definition == null || traderFuturePanelObject == null)
            return;

        TraderShopKind capturedKind = definition.Kind;
        Button button = CreateButton(
            traderFuturePanelObject.transform,
            "TraderButton_" + definition.Kind,
            string.Empty,
            anchoredPosition,
            size,
            () => OnTraderPortraitClicked(capturedKind));
        StyleButton(button, new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0.1f));

        Image cardImage = button.GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.color = new Color(1f, 1f, 1f, 0f);
            cardImage.raycastTarget = true;
            traderCardImagesByKind[definition.Kind] = cardImage;
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.34f, 0.48f, 0.62f, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
        traderOutlinesByKind[definition.Kind] = outline;

        GameObject portraitObject = new GameObject("TraderPortrait", typeof(RectTransform), typeof(Image));
        portraitObject.transform.SetParent(button.transform, false);
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = Vector2.zero;
        portraitRect.anchorMax = Vector2.one;
        portraitRect.offsetMin = Vector2.zero;
        portraitRect.offsetMax = Vector2.zero;

        Image portrait = portraitObject.GetComponent<Image>();
        Sprite portraitSprite = LoadTraderPortraitSprite(definition);
        portrait.sprite = portraitSprite;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        portrait.color = portraitSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);

        TMP_Text nameText = CreateText(button.transform, "TraderName", definition.DisplayName, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(size.x - 10f, 30f), 18f, TextAlignmentOptions.Center);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 11f;
        nameText.fontSizeMax = 18f;
        nameText.characterSpacing = 1.5f;
        nameText.raycastTarget = false;
        Shadow nameShadow = nameText.gameObject.AddComponent<Shadow>();
        nameShadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
        nameShadow.effectDistance = new Vector2(2f, -2f);

        traderButtonsByKind[definition.Kind] = button;
    }

    Sprite LoadTraderPortraitSprite(TraderDefinition definition)
    {
        if (definition == null)
            return null;

        return LoadSpriteFromResourcesOrEditor(
            definition.ResourcePath,
            definition.EditorPreferredPath,
            definition.EditorFallbackPath);
    }

    TraderDefinition GetTraderDefinition(TraderShopKind kind)
    {
        for (int i = 0; i < TraderDefinitions.Length; i++)
        {
            TraderDefinition definition = TraderDefinitions[i];
            if (definition != null && definition.Kind == kind)
                return definition;
        }

        return null;
    }

    string GetTraderDisplayName(TraderShopKind kind)
    {
        TraderDefinition definition = GetTraderDefinition(kind);
        return definition != null ? definition.DisplayName : "TRADER";
    }

    bool TraderOpensShop(TraderShopKind kind)
    {
        TraderDefinition definition = GetTraderDefinition(kind);
        return definition != null && definition.OpensShop;
    }

    bool ShouldTraderSellDefinition(TraderShopKind kind, InventoryItemDefinition definition)
    {
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return false;

        switch (kind)
        {
            case TraderShopKind.IronJoe:
                return definition.Category == InventoryItemCategory.Weapon ||
                    definition.Category == InventoryItemCategory.Shield ||
                    definition.Category == InventoryItemCategory.Engine;
            case TraderShopKind.MissGadget:
                return definition.Category == InventoryItemCategory.Gadget ||
                    definition.Category == InventoryItemCategory.Support ||
                    definition.Category == InventoryItemCategory.Rescue;
            default:
                return false;
        }
    }

    void OnTraderPortraitClicked(TraderShopKind kind)
    {
        if (inventoryActionInProgress || dragInProgress)
            return;

        selectedTraderShop = kind;
        HideCraftingRecipeBrowser();
        HideItemPreview();
        RefreshTraderSelectionVisuals();

        if (!TraderOpensShop(selectedTraderShop))
        {
            RefreshShopBrowser();
            HideShopBrowser();
            SetStatus(GetTraderDisplayName(selectedTraderShop) + " coming later.");
            return;
        }

        SetStatus(string.Empty);
        RefreshShopBrowser();
        if (shopBrowserObject != null && currentScreen == ProfileScreen.Trader)
        {
            shopBrowserObject.SetActive(true);
            shopBrowserObject.transform.SetAsLastSibling();
        }

        if (traderFuturePanelObject != null)
            traderFuturePanelObject.transform.SetAsLastSibling();
    }

    void RefreshTraderSelectionVisuals()
    {
        for (int i = 0; i < TraderDefinitions.Length; i++)
        {
            TraderDefinition definition = TraderDefinitions[i];
            if (definition == null)
                continue;

            bool selected = definition.Kind == selectedTraderShop;
            bool interactable = inventoryControlsInteractable && !inventoryActionInProgress;

            if (traderButtonsByKind.TryGetValue(definition.Kind, out Button button) && button != null)
                button.interactable = interactable;

            if (traderCardImagesByKind.TryGetValue(definition.Kind, out Image cardImage) && cardImage != null)
            {
                cardImage.color = selected
                    ? new Color(1f, 0.82f, 0.22f, 0.1f)
                    : new Color(1f, 1f, 1f, 0f);
            }

            if (traderOutlinesByKind.TryGetValue(definition.Kind, out Outline outline) && outline != null)
            {
                outline.effectColor = selected
                    ? new Color(0.94f, 0.78f, 0.32f, 0.96f)
                    : new Color(0.34f, 0.48f, 0.62f, 0.72f);
                outline.effectDistance = selected ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
            }
        }
    }

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

        projectRewardsText = CreateText(parent, "ProjectRewards", string.Empty, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-292f, -274f), new Vector2(382f, 112f), 22f, TextAlignmentOptions.TopLeft);
        projectRewardsText.fontStyle = FontStyles.Normal;
        projectRewardsText.textWrappingMode = TextWrappingModes.Normal;

        projectRewardClaimButton = CreateButton(parent, "ProjectRewardClaimButton", "CLAIM", new Vector2(0f, 0f), new Vector2(190f, 56f), OnProjectRewardClaimClicked);
        StyleButton(projectRewardClaimButton, new Color(0.12f, 0.46f, 0.34f, 1f), new Color(0.16f, 0.62f, 0.44f, 1f));

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
            Button tab = CreateButton(parent, "ProjectStageTab_" + i, "STAGE " + (i + 1), Vector2.zero, new Vector2(184f, 54f), () =>
            {
                OnProjectStageTabClicked(capturedIndex);
            });
            StyleButton(tab, new Color(0.13f, 0.18f, 0.26f, 0.96f), new Color(0.24f, 0.42f, 0.54f, 1f));
            projectStageTabButtons.Add(tab);
        }

        projectStepButtons.Clear();
        projectStepIcons.Clear();
        projectStepTexts.Clear();
        projectStepCheckBoxes.Clear();
        for (int i = 0; i < maxSteps; i++)
        {
            int capturedIndex = i;
            Button stepButton = CreateButton(parent, "ProjectStep_" + i, string.Empty, Vector2.zero, new Vector2(470f, 126f), () =>
            {
                OnProjectStepClicked(capturedIndex);
            });
            StyleButton(stepButton, new Color(0.08f, 0.12f, 0.17f, 0.92f), new Color(0.14f, 0.22f, 0.31f, 1f));

            GameObject iconObject = new GameObject("ProjectStepIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(stepButton.transform, false);
            Image icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TMP_Text text = CreateText(stepButton.transform, "ProjectStepLabel", string.Empty, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 22f, TextAlignmentOptions.MidlineLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.margin = new Vector4(116f, 10f, 62f, 10f);
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
        rect.anchoredPosition = new Vector2(-42f, 0f);
        rect.sizeDelta = new Vector2(46f, 46f);

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

        projectCommitTitleText = CreateText(projectCommitPanelObject.transform, "ProjectCommitTitle", "SELECT STEP", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(330f, 32f), 22f, TextAlignmentOptions.Center);
        projectCommitAvailableText = CreateText(projectCommitPanelObject.transform, "ProjectCommitAvailable", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(330f, 42f), 24f, TextAlignmentOptions.Center);
        projectCommitAvailableText.fontStyle = FontStyles.Normal;
        projectCommitAvailableText.textWrappingMode = TextWrappingModes.Normal;

        projectCommitMinusButton = CreateButton(projectCommitPanelObject.transform, "ProjectCommitMinus", "-", new Vector2(-108f, -126f), new Vector2(74f, 52f), () => AdjustProjectCommitAmount(-1));
        projectCommitAmountText = CreateText(projectCommitPanelObject.transform, "ProjectCommitAmount", "0", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(120f, 46f), 28f, TextAlignmentOptions.Center);
        projectCommitPlusButton = CreateButton(projectCommitPanelObject.transform, "ProjectCommitPlus", "+", new Vector2(108f, -126f), new Vector2(74f, 52f), () => AdjustProjectCommitAmount(1));
        projectCommitButton = CreateButton(projectCommitPanelObject.transform, "ProjectCommitButton", "COMMIT", new Vector2(0f, -190f), new Vector2(220f, 52f), OnProjectCommitClicked);
        StyleButton(projectCommitMinusButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.24f, 0.34f, 0.46f, 1f));
        StyleButton(projectCommitPlusButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.24f, 0.34f, 0.46f, 1f));
        StyleButton(projectCommitButton, new Color(0.12f, 0.46f, 0.34f, 1f), new Color(0.16f, 0.62f, 0.44f, 1f));
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
        shipSelectionCardLockTexts = new TMP_Text[3];
        shipSelectionCardDonateButtons = new Button[3];
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
                out shipSelectionCardLockTexts[i],
                out shipSelectionCardDonateButtons[i],
                out shipSelectionCardStatLabelTexts[i],
                out shipSelectionCardStatValueTexts[i],
                out shipSelectionCardStatFillImages[i],
                out shipSelectionCardSlotObjects[i]);
        }

        shipSelectionStatusText = CreateText(shipSelectionViewObject.transform, "ShipSelectionStatus", string.Empty, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(720f, 28f), 18f, TextAlignmentOptions.Center);
        shipSelectionStatusText.fontStyle = FontStyles.Normal;
        shipSelectionStatusText.color = new Color(0.86f, 0.92f, 0.98f, 0.98f);

        shipSelectionDetailsButton = CreateButton(shipSelectionViewObject.transform, "ShipSelectionMissionDetailsButton", "MISSION DETAILS", new Vector2(0f, -984f), new Vector2(330f, 58f), ShowShipSelectionMissionDetails);
        StyleButton(shipSelectionDetailsButton, new Color(0.13f, 0.2f, 0.3f, 0.98f), new Color(0.22f, 0.34f, 0.48f, 1f));
        TMP_Text detailsButtonText = shipSelectionDetailsButton.GetComponentInChildren<TMP_Text>(true);
        if (detailsButtonText != null)
        {
            detailsButtonText.fontSize = 20f;
            detailsButtonText.characterSpacing = 1.2f;
            detailsButtonText.margin = new Vector4(12f, 4f, 12f, 4f);
        }

        CreateShipMissionDetailsPanel(shipSelectionViewObject.transform);

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

    void CreateShipMissionDetailsPanel(Transform parent)
    {
        shipMissionDetailsPanelObject = new GameObject("ShipMissionDetailsPanel", typeof(RectTransform), typeof(Image), typeof(Shadow));
        shipMissionDetailsPanelObject.transform.SetParent(parent, false);

        RectTransform panelRect = shipMissionDetailsPanelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, -12f);
        panelRect.sizeDelta = new Vector2(900f, 620f);

        Image panelImage = shipMissionDetailsPanelObject.GetComponent<Image>();
        panelImage.color = new Color(0.035f, 0.06f, 0.09f, 0.98f);
        panelImage.raycastTarget = true;

        Shadow shadow = shipMissionDetailsPanelObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.62f);
        shadow.effectDistance = new Vector2(8f, -8f);
        shadow.useGraphicAlpha = true;

        shipMissionDetailsTitleText = CreateText(shipMissionDetailsPanelObject.transform, "ShipMissionDetailsTitle", "MISSION DETAILS", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(720f, 46f), 34f, TextAlignmentOptions.Center);
        shipMissionDetailsTitleText.characterSpacing = 1.5f;
        shipMissionDetailsTitleText.raycastTarget = false;

        GameObject viewportObject = new GameObject("ShipMissionDetailsViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(shipMissionDetailsPanelObject.transform, false);

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.offsetMin = new Vector2(42f, 94f);
        viewportRect.offsetMax = new Vector2(-42f, -100f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = true;

        GameObject contentObject = new GameObject("ShipMissionDetailsContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        shipMissionDetailsContentRect = contentObject.GetComponent<RectTransform>();
        shipMissionDetailsContentRect.anchorMin = new Vector2(0f, 1f);
        shipMissionDetailsContentRect.anchorMax = new Vector2(1f, 1f);
        shipMissionDetailsContentRect.pivot = new Vector2(0.5f, 1f);
        shipMissionDetailsContentRect.anchoredPosition = Vector2.zero;
        shipMissionDetailsContentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = viewportObject.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 32f;
        scrollRect.viewport = viewportRect;
        scrollRect.content = shipMissionDetailsContentRect;

        shipMissionDetailsBodyText = CreateText(contentObject.transform, "ShipMissionDetailsBody", string.Empty, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 30f, TextAlignmentOptions.TopLeft);
        shipMissionDetailsBodyText.fontStyle = FontStyles.Normal;
        shipMissionDetailsBodyText.textWrappingMode = TextWrappingModes.Normal;
        shipMissionDetailsBodyText.overflowMode = TextOverflowModes.Overflow;
        shipMissionDetailsBodyText.lineSpacing = 8f;
        shipMissionDetailsBodyText.margin = Vector4.zero;
        shipMissionDetailsBodyText.raycastTarget = false;

        shipMissionDetailsTextLayoutElement = shipMissionDetailsBodyText.gameObject.AddComponent<LayoutElement>();
        shipMissionDetailsTextLayoutElement.flexibleWidth = 1f;
        shipMissionDetailsTextLayoutElement.minHeight = 1f;
        shipMissionDetailsTextLayoutElement.preferredHeight = 430f;

        shipMissionDetailsCloseButton = CreateButton(shipMissionDetailsPanelObject.transform, "ShipMissionDetailsCloseButton", "CLOSE", new Vector2(0f, -548f), new Vector2(220f, 54f), HideShipSelectionMissionDetails);
        StyleButton(shipMissionDetailsCloseButton, new Color(0.14f, 0.19f, 0.28f, 0.98f), new Color(0.22f, 0.3f, 0.42f, 1f));

        shipMissionDetailsPanelObject.SetActive(false);
    }

    void ShowShipSelectionMissionDetails()
    {
        if (!TryBuildShipSelectionMissionDetails(shipSelectionCenterType, out string title, out string body))
            return;

        UpdateShipSelectionMissionDetailsContent(title, body);

        if (shipMissionDetailsPanelObject != null)
        {
            shipMissionDetailsPanelObject.SetActive(true);
            shipMissionDetailsPanelObject.transform.SetAsLastSibling();
        }
    }

    void HideShipSelectionMissionDetails()
    {
        if (shipMissionDetailsPanelObject != null)
            shipMissionDetailsPanelObject.SetActive(false);
    }

    void RefreshShipSelectionMissionDetailsControls()
    {
        bool hasDetails = TryBuildShipSelectionMissionDetails(shipSelectionCenterType, out string title, out string body);

        if (shipSelectionDetailsButton != null)
        {
            shipSelectionDetailsButton.gameObject.SetActive(hasDetails);
            shipSelectionDetailsButton.interactable = hasDetails && inventoryControlsInteractable && !inventoryActionInProgress;
        }

        if (!hasDetails)
        {
            HideShipSelectionMissionDetails();
            return;
        }

        if (shipMissionDetailsPanelObject != null && shipMissionDetailsPanelObject.activeSelf)
            UpdateShipSelectionMissionDetailsContent(title, body);
    }

    bool TryBuildShipSelectionMissionDetails(ShipType shipType, out string title, out string body)
    {
        title = string.Empty;
        body = string.Empty;

        bool shipUnlocked = IsShipTypeUnlockedForUi(shipType);
        ViperRecoveryStage viperStage = shipType == ShipType.Viper && PlayerProfileService.HasInstance
            ? PlayerProfileService.Instance.GetViperRecoveryStage()
            : ViperRecoveryStage.Complete;
        bool hasActiveMission = !shipUnlocked || (shipType == ShipType.Viper && viperStage == ViperRecoveryStage.Testing);
        if (!hasActiveMission)
            return false;

        string details = BuildShipSelectionProgressText(shipType, shipUnlocked, viperStage);
        if (string.IsNullOrWhiteSpace(details))
            return false;

        title = ShipCatalog.GetShipTypeDisplayName(shipType).ToUpperInvariant() + " MISSION";
        body = details;
        return true;
    }

    void UpdateShipSelectionMissionDetailsContent(string title, string body)
    {
        if (shipMissionDetailsTitleText != null)
            shipMissionDetailsTitleText.text = title;

        if (shipMissionDetailsBodyText != null)
        {
            shipMissionDetailsBodyText.text = body;
            UpdateShipSelectionMissionDetailsBodyHeight(body);
        }
    }

    void UpdateShipSelectionMissionDetailsBodyHeight(string body)
    {
        if (shipMissionDetailsBodyText == null || shipMissionDetailsTextLayoutElement == null)
            return;

        float textWidth = 780f;
        if (shipMissionDetailsContentRect != null && shipMissionDetailsContentRect.rect.width > 1f)
            textWidth = Mathf.Max(1f, shipMissionDetailsContentRect.rect.width - 8f);

        float preferredHeight = shipMissionDetailsBodyText.GetPreferredValues(body ?? string.Empty, textWidth, Mathf.Infinity).y;
        shipMissionDetailsTextLayoutElement.preferredHeight = Mathf.Max(430f, preferredHeight + 8f);

        if (shipMissionDetailsContentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(shipMissionDetailsContentRect);
    }

    void CreatePilotSelectionView(Transform parent)
    {
        pilotSelectionViewObject = new GameObject("PilotSelectionView", typeof(RectTransform), typeof(Image));
        pilotSelectionViewObject.transform.SetParent(parent, false);

        RectTransform rootRect = pilotSelectionViewObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image overlay = pilotSelectionViewObject.GetComponent<Image>();
        overlay.color = new Color(0.03f, 0.05f, 0.08f, 0.96f);

        ProfilePilotSelectionSwipeHandler swipeHandler = pilotSelectionViewObject.AddComponent<ProfilePilotSelectionSwipeHandler>();
        swipeHandler.owner = this;

        pilotSelectionBackButton = CreateButton(pilotSelectionViewObject.transform, "PilotSelectionBackButton", "BACK", new Vector2(-116f, -106f), new Vector2(216f, 62f), () =>
        {
            SwitchToScreen(ProfileScreen.Home);
        });
        StyleButton(pilotSelectionBackButton, new Color(0.14f, 0.19f, 0.28f, 0.98f), new Color(0.22f, 0.3f, 0.42f, 1f));
        pilotSelectionBackButton.gameObject.SetActive(false);

        pilotSelectionTitleText = CreateText(pilotSelectionViewObject.transform, "PilotSelectionTitle", "JAKE", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(720f, 56f), 42f, TextAlignmentOptions.Center);
        pilotSelectionTitleText.raycastTarget = false;

        pilotSelectionPrevButton = CreateButton(pilotSelectionViewObject.transform, "PilotSelectionPrevButton", "<", new Vector2(-666f, -438f), new Vector2(92f, 92f), MovePilotSelectionLeft);
        pilotSelectionNextButton = CreateButton(pilotSelectionViewObject.transform, "PilotSelectionNextButton", ">", new Vector2(666f, -438f), new Vector2(92f, 92f), MovePilotSelectionRight);
        StyleButton(pilotSelectionPrevButton, new Color(0.16f, 0.22f, 0.3f, 0.95f), new Color(0.24f, 0.34f, 0.46f, 1f));
        StyleButton(pilotSelectionNextButton, new Color(0.16f, 0.22f, 0.3f, 0.95f), new Color(0.24f, 0.34f, 0.46f, 1f));

        pilotSelectionCardObjects = new GameObject[3];
        pilotSelectionCardImages = new Image[3];
        pilotSelectionCardNames = new TMP_Text[3];
        pilotSelectionCardLockTexts = new TMP_Text[3];
        Vector2[] positions =
        {
            new Vector2(-560f, -190f),
            new Vector2(0f, -162f),
            new Vector2(560f, -190f)
        };
        Vector2[] sizes =
        {
            new Vector2(440f, 540f),
            new Vector2(560f, 620f),
            new Vector2(440f, 540f)
        };

        for (int i = 0; i < pilotSelectionCardObjects.Length; i++)
        {
            pilotSelectionCardObjects[i] = CreatePilotSelectionCard(
                pilotSelectionViewObject.transform,
                i,
                positions[i],
                sizes[i],
                i == 1,
                out pilotSelectionCardImages[i],
                out pilotSelectionCardNames[i],
                out pilotSelectionCardLockTexts[i]);
        }

        pilotSelectionAbilitiesText = CreateText(pilotSelectionViewObject.transform, "PilotSelectionAbilities", string.Empty, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(1380f, 250f), 25f, TextAlignmentOptions.Center);
        pilotSelectionAbilitiesText.fontStyle = FontStyles.Normal;
        pilotSelectionAbilitiesText.textWrappingMode = TextWrappingModes.Normal;
        pilotSelectionAbilitiesText.color = new Color(0.86f, 0.92f, 0.98f, 0.98f);
        pilotSelectionAbilitiesText.raycastTarget = false;

        pilotSelectionStatusText = CreateText(pilotSelectionViewObject.transform, "PilotSelectionStatus", string.Empty, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(820f, 32f), 18f, TextAlignmentOptions.Center);
        pilotSelectionStatusText.fontStyle = FontStyles.Normal;
        pilotSelectionStatusText.color = new Color(0.98f, 0.82f, 0.5f, 0.98f);
        pilotSelectionStatusText.gameObject.SetActive(false);

        pilotSelectionViewObject.SetActive(false);
    }

    GameObject CreatePilotSelectionCard(Transform parent, int cardIndex, Vector2 anchoredPosition, Vector2 size, bool centerCard, out Image previewImage, out TMP_Text nameText, out TMP_Text lockText)
    {
        GameObject card = new GameObject("PilotSelectionCard" + cardIndex, typeof(RectTransform), typeof(Image), typeof(Button));
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
            : new Color(0.08f, 0.11f, 0.16f, 0.86f);

        Button button = card.GetComponent<Button>();
        int capturedCardIndex = cardIndex;
        button.onClick.AddListener(() =>
        {
            if (ConsumePilotSelectionClickSuppression())
                return;

            AudioManager.Instance?.PlayClick();
            OnPilotSelectionCardClicked(capturedCardIndex);
        });

        ProfilePilotSelectionSwipeHandler swipeHandler = card.AddComponent<ProfilePilotSelectionSwipeHandler>();
        swipeHandler.owner = this;

        GameObject imageObject = new GameObject("Preview", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(card.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = centerCard ? new Vector2(22f, 58f) : new Vector2(18f, 50f);
        imageRect.offsetMax = centerCard ? new Vector2(-22f, -28f) : new Vector2(-18f, -24f);
        previewImage = imageObject.GetComponent<Image>();
        previewImage.preserveAspect = true;
        previewImage.raycastTarget = false;

        nameText = CreateText(card.transform, "Name", "PILOT", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -28f), new Vector2(0f, 38f), centerCard ? 28f : 22f, TextAlignmentOptions.Center);
        nameText.raycastTarget = false;
        nameText.gameObject.SetActive(false);

        lockText = CreateText(card.transform, "LockText", string.Empty, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x - 56f, 118f), centerCard ? 22f : 16f, TextAlignmentOptions.Center);
        lockText.textWrappingMode = TextWrappingModes.Normal;
        lockText.color = new Color(1f, 0.92f, 0.74f, 1f);
        lockText.raycastTarget = false;

        return card;
    }

    GameObject CreateShipSelectionCard(Transform parent, int cardIndex, Vector2 anchoredPosition, Vector2 size, bool centerCard, out Image previewImage, out TMP_Text titleText, out TMP_Text lockText, out Button donateButton, out TMP_Text[] statLabelTexts, out TMP_Text[] statValueTexts, out Image[] statFillImages, out GameObject[] slotObjects)
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
            if (ConsumeShipSelectionClickSuppression())
                return;

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

        lockText = CreateText(card.transform, "LockText", string.Empty, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), centerCard ? new Vector2(18f, 18f) : new Vector2(10f, 12f), new Vector2(size.x - 80f, 118f), centerCard ? 28f : 20f, TextAlignmentOptions.Center);
        lockText.textWrappingMode = TextWrappingModes.Normal;
        lockText.color = new Color(1f, 0.92f, 0.72f, 0.98f);
        lockText.raycastTarget = false;

        donateButton = CreateButton(card.transform, "ViperDonateButton", "Repair with spare parts", centerCard ? new Vector2(18f, -218f) : new Vector2(10f, -162f), centerCard ? new Vector2(118f, 118f) : new Vector2(92f, 92f), OnViperRepairDonateClicked);
        ConfigureViperRepairButtonLabel(donateButton, centerCard);
        StyleButton(donateButton, new Color(0.12f, 0.38f, 0.22f, 0.98f), new Color(0.18f, 0.58f, 0.34f, 1f));
        donateButton.gameObject.SetActive(false);

        slotObjects = new GameObject[PlayerInventoryData.EquipmentSlotCount];
        Vector2[] slotLayout = BuildShipSelectionSlotLayout(centerCard);
        for (int i = 0; i < slotObjects.Length; i++)
        {
            GameObject slot = new GameObject("Slot" + i, typeof(RectTransform), typeof(Image), typeof(Outline));
            slot.transform.SetParent(card.transform, false);
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = slotLayout[i];
            slotRect.sizeDelta = GetShipSelectionSlotSize(centerCard);
            Image slotImage = slot.GetComponent<Image>();
            slotImage.color = GetShipSelectionSlotColor(i);
            slotImage.raycastTarget = false;
            Outline outline = slot.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = GetShipSelectionSlotOutlineColor(i);
                outline.effectDistance = new Vector2(2.2f, -2.2f);
            }

            TMP_Text slotText = CreateText(slot.transform, "SlotText", GetShipSelectionSlotLabel(i), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, EquipmentSlotPreviewFontSize, TextAlignmentOptions.Center);
            ApplyEquipmentSlotPreviewTextStyle(slotText);
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

    void ConfigureViperRepairButtonLabel(Button button, bool centerCard)
    {
        if (button == null)
            return;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
            return;

        text.text = "Repair with spare parts";
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableAutoSizing = true;
        text.fontSizeMin = centerCard ? 10f : 8f;
        text.fontSizeMax = centerCard ? 17f : 13f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.margin = centerCard
            ? new Vector4(8f, 8f, 8f, 8f)
            : new Vector4(6f, 6f, 6f, 6f);
    }

    Vector2[] BuildShipSelectionSlotLayout(bool centerCard)
    {
        return BuildShipSelectionSlotLayout(centerCard ? 1f : 0f);
    }

    Vector2[] BuildShipSelectionSlotLayout(float centerAmount)
    {
        centerAmount = Mathf.Clamp01(centerAmount);
        float leftColumnX = Mathf.Lerp(-250f, -346f, centerAmount);
        float rightColumnX = Mathf.Lerp(-130f, -208f, centerAmount);
        float topY = Mathf.Lerp(252f, 300f, centerAmount);
        float rowSpacing = EquipmentSlotPreviewSize + 12f;

        Vector2[] result = new Vector2[PlayerInventoryData.EquipmentSlotCount];
        int[][] rowOrder =
        {
            new[] { 0, 1 },
            new[] { 2, 3 },
            new[] { 4, 5 },
            new[] { 8, 9 },
            new[] { 10, 11 },
            new[] { 6, 7 }
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

    Vector2 GetShipSelectionSlotSize(bool centerCard)
    {
        float size = EquipmentSlotPreviewSize;
        return new Vector2(size, size);
    }

    string GetShipSelectionSlotLabel(int slotIndex)
    {
        return slotIndex switch
        {
            0 or 1 => "MAIN GUN",
            2 or 3 => "SHIELD",
            4 or 5 => "ENGINE",
            6 or 7 => "GADGET",
            8 or 9 => "SUPPORT",
            10 or 11 => "RESCUE",
            _ => "SLOT"
        };
    }

    Color GetShipSelectionSlotColor(int slotIndex)
    {
        return InventoryItemCatalog.GetEquipmentSlotCategory(slotIndex) switch
        {
            InventoryItemCategory.Weapon => new Color(0.19f, 0.22f, 0.34f, 0.96f),
            InventoryItemCategory.Shield => new Color(0.17f, 0.29f, 0.28f, 0.96f),
            InventoryItemCategory.Engine => new Color(0.24f, 0.29f, 0.18f, 0.96f),
            InventoryItemCategory.Support => new Color(0.13f, 0.31f, 0.32f, 0.96f),
            InventoryItemCategory.Rescue => new Color(0.34f, 0.19f, 0.2f, 0.96f),
            InventoryItemCategory.Gadget => new Color(0.24f, 0.21f, 0.31f, 0.96f),
            _ => new Color(0.17f, 0.22f, 0.28f, 0.96f)
        };
    }

    Color GetShipSelectionSlotOutlineColor(int slotIndex)
    {
        return InventoryItemCatalog.GetEquipmentSlotCategory(slotIndex) switch
        {
            InventoryItemCategory.Weapon => new Color(0.5f, 0.56f, 0.78f, 0.9f),
            InventoryItemCategory.Shield => new Color(0.43f, 0.68f, 0.64f, 0.9f),
            InventoryItemCategory.Engine => new Color(0.62f, 0.7f, 0.42f, 0.9f),
            InventoryItemCategory.Support => new Color(0.34f, 0.72f, 0.7f, 0.9f),
            InventoryItemCategory.Rescue => new Color(0.74f, 0.45f, 0.46f, 0.9f),
            InventoryItemCategory.Gadget => new Color(0.61f, 0.51f, 0.72f, 0.9f),
            _ => new Color(0.55f, 0.63f, 0.7f, 0.82f)
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
        equipmentSlotButtons[8] = CreateEquipmentSlotButton(previewRoot.transform, "SupportA", Vector2.zero, 8, "SUPPORT", out equipmentSlotPreviewTexts[8], out equipmentSlotPreviewIcons[8]);
        equipmentSlotButtons[9] = CreateEquipmentSlotButton(previewRoot.transform, "SupportB", Vector2.zero, 9, "SUPPORT", out equipmentSlotPreviewTexts[9], out equipmentSlotPreviewIcons[9]);
        equipmentSlotButtons[10] = CreateEquipmentSlotButton(previewRoot.transform, "RescueA", Vector2.zero, 10, "RESCUE", out equipmentSlotPreviewTexts[10], out equipmentSlotPreviewIcons[10]);
        equipmentSlotButtons[11] = CreateEquipmentSlotButton(previewRoot.transform, "RescueB", Vector2.zero, 11, "RESCUE", out equipmentSlotPreviewTexts[11], out equipmentSlotPreviewIcons[11]);

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

    void CreatePilotPortrait(Transform parent)
    {
        pilotPortraitRootObject = new GameObject("PilotPortraitRoot", typeof(RectTransform));
        pilotPortraitRootObject.transform.SetParent(parent, false);

        GameObject buttonObject = new GameObject("PilotPortraitButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(pilotPortraitRootObject.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(278f, 278f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.08f, 0.12f, 0.17f, 0.94f);

        pilotPortraitButton = buttonObject.GetComponent<Button>();
        pilotPortraitButton.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            OnPilotPortraitClicked();
        });

        GameObject portraitObject = new GameObject("PilotPortraitImage", typeof(RectTransform), typeof(Image));
        portraitObject.transform.SetParent(buttonObject.transform, false);
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = Vector2.zero;
        portraitRect.anchorMax = Vector2.one;
        portraitRect.offsetMin = new Vector2(8f, 8f);
        portraitRect.offsetMax = new Vector2(-8f, -8f);
        pilotPortraitImage = portraitObject.GetComponent<Image>();
        pilotPortraitImage.preserveAspect = true;
        pilotPortraitImage.raycastTarget = false;

        pilotPortraitNameText = CreateText(pilotPortraitRootObject.transform, "PilotPortraitName", "JAKE", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -42f), new Vector2(340f, 34f), 24f, TextAlignmentOptions.Center);
        pilotPortraitNameText.raycastTarget = false;

        pilotPortraitCaptionText = CreateText(pilotPortraitRootObject.transform, "PilotPortraitCaption", "PILOT", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 294f), new Vector2(260f, 26f), 18f, TextAlignmentOptions.Center);
        pilotPortraitCaptionText.fontStyle = FontStyles.Normal;
        pilotPortraitCaptionText.color = new Color(0.78f, 0.86f, 0.94f, 0.94f);
        pilotPortraitCaptionText.raycastTarget = false;
    }

    void CreateProjectsHomeButton(Transform parent)
    {
        projectsButtonRootObject = new GameObject("ProjectsButtonRoot", typeof(RectTransform));
        projectsButtonRootObject.transform.SetParent(parent, false);

        GameObject buttonObject = new GameObject("ProjectsScreenButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(projectsButtonRootObject.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(342f, 210f);

        Image buttonBackgroundImage = buttonObject.GetComponent<Image>();
        buttonBackgroundImage.color = new Color(0.02f, 0.04f, 0.07f, 1f);
        buttonBackgroundImage.raycastTarget = true;

        GameObject previewObject = new GameObject("ProjectsScreenButtonPreview", typeof(RectTransform), typeof(Image));
        previewObject.transform.SetParent(buttonObject.transform, false);
        RectTransform previewRect = previewObject.GetComponent<RectTransform>();
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.offsetMin = new Vector2(6f, 6f);
        previewRect.offsetMax = new Vector2(-6f, -6f);

        projectsButtonImage = previewObject.GetComponent<Image>();
        projectsButtonImage.sprite = LoadStandaloneSprite("PROJECTS_SCREEN.png");
        projectsButtonImage.color = projectsButtonImage.sprite != null ? Color.white : new Color(0.08f, 0.12f, 0.17f, 1f);
        projectsButtonImage.preserveAspect = false;
        projectsButtonImage.raycastTarget = false;

        projectsButton = buttonObject.GetComponent<Button>();
        projectsButton.targetGraphic = buttonBackgroundImage;
        ColorBlock buttonColors = projectsButton.colors;
        buttonColors.normalColor = new Color(0.02f, 0.04f, 0.07f, 1f);
        buttonColors.selectedColor = new Color(0.04f, 0.08f, 0.12f, 1f);
        buttonColors.highlightedColor = new Color(0.04f, 0.08f, 0.12f, 1f);
        buttonColors.pressedColor = new Color(0.01f, 0.025f, 0.045f, 1f);
        buttonColors.disabledColor = new Color(0.02f, 0.03f, 0.04f, 0.95f);
        projectsButton.colors = buttonColors;
        projectsButton.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            OnProjectsHomeButtonClicked();
        });

        projectsButtonCaptionText = CreateText(projectsButtonRootObject.transform, "ProjectsButtonCaption", "PROJECTS", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 226f), new Vector2(260f, 26f), 18f, TextAlignmentOptions.Center);
        projectsButtonCaptionText.fontStyle = FontStyles.Normal;
        projectsButtonCaptionText.color = new Color(0.78f, 0.86f, 0.94f, 0.94f);
        projectsButtonCaptionText.raycastTarget = false;
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
        panelRect.sizeDelta = new Vector2(504f, 180f);

        int cardCount = ShipStatLabels.Length;
        const int columns = 3;
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
        logoRect.sizeDelta = Vector2.zero;

        splashScreenImage = logoObject.GetComponent<Image>();
        splashScreenImage.color = Color.white;
        splashScreenImage.preserveAspect = false;
        splashScreenImage.raycastTarget = false;
        splashScreenImage.sprite = LoadStandaloneSprite("STAR_RAIDERS_screen.png");

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
        itemPreviewInfoButton = CreateButton(itemPreviewPanelObject.transform, "ItemPreviewInfoButton", "INFO", new Vector2(0f, 54f), ItemPreviewInfoButtonSize, OnItemPreviewInfoClicked);
        ConfigureNoBlinkInventoryActionButton(itemPreviewInfoButton);
        StyleReadableBackLikeButton(itemPreviewInfoButton, ShipInventoryHeaderButtonFontSize);
        itemPreviewSellButton = CreateButton(itemPreviewPanelObject.transform, "ItemPreviewSellButton", "SELL", new Vector2(-76f, -270f), new Vector2(136f, 50f), OnItemPreviewSellClicked);
        itemPreviewSalvageButton = CreateButton(itemPreviewPanelObject.transform, "ItemPreviewSalvageButton", "SALVAGE", new Vector2(76f, -270f), new Vector2(136f, 50f), OnItemPreviewSalvageClicked);
        ConfigureNoBlinkInventoryActionButton(itemPreviewSellButton);
        StyleCompactBackLikeButton(itemPreviewSellButton);
        ConfigureNoBlinkInventoryActionButton(itemPreviewSalvageButton);
        StyleCompactBackLikeButton(itemPreviewSalvageButton);
        itemPreviewPanelObject.SetActive(false);
    }

    void CreateItemInfoOverlay(Transform parent)
    {
        itemInfoOverlayObject = new GameObject("ItemInfoOverlay", typeof(RectTransform), typeof(Image));
        itemInfoOverlayObject.transform.SetParent(parent, false);
        RectTransform overlayRect = itemInfoOverlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = itemInfoOverlayObject.GetComponent<Image>();
        overlayImage.color = new Color(0.01f, 0.015f, 0.026f, 0.88f);
        overlayImage.raycastTarget = true;

        GameObject cardObject = new GameObject("ItemInfoCard", typeof(RectTransform), typeof(Image));
        cardObject.transform.SetParent(itemInfoOverlayObject.transform, false);
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(1160f, 820f);

        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = new Color(0.06f, 0.085f, 0.12f, 0.99f);

        itemInfoTitleText = CreateText(cardObject.transform, "ItemInfoTitle", "ITEM", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(1030f, 52f), 38f, TextAlignmentOptions.Center);
        itemInfoTitleText.enableAutoSizing = true;
        itemInfoTitleText.fontSizeMin = 22f;
        itemInfoTitleText.fontSizeMax = 38f;

        itemInfoTypeText = CreateText(cardObject.transform, "ItemInfoType", "Type", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(1030f, 30f), 22f, TextAlignmentOptions.Center);
        itemInfoTypeText.fontStyle = FontStyles.Normal;
        itemInfoTypeText.color = new Color(0.74f, 0.88f, 1f, 1f);

        GameObject imageCardObject = new GameObject("ItemInfoImageCard", typeof(RectTransform), typeof(Image));
        imageCardObject.transform.SetParent(cardObject.transform, false);
        RectTransform imageCardRect = imageCardObject.GetComponent<RectTransform>();
        imageCardRect.anchorMin = new Vector2(0.5f, 1f);
        imageCardRect.anchorMax = new Vector2(0.5f, 1f);
        imageCardRect.pivot = new Vector2(0.5f, 1f);
        imageCardRect.anchoredPosition = new Vector2(-310f, -152f);
        imageCardRect.sizeDelta = new Vector2(430f, 430f);

        Image imageCard = imageCardObject.GetComponent<Image>();
        imageCard.color = new Color(0.09f, 0.13f, 0.18f, 0.96f);

        GameObject iconObject = new GameObject("ItemInfoIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(imageCardObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(342f, 342f);
        itemInfoIcon = iconObject.GetComponent<Image>();
        itemInfoIcon.preserveAspect = true;
        itemInfoIcon.raycastTarget = false;

        itemInfoPriceText = CreateText(cardObject.transform, "ItemInfoPrice", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-310f, -608f), new Vector2(430f, 78f), 24f, TextAlignmentOptions.Center);
        itemInfoPriceText.fontStyle = FontStyles.Normal;
        itemInfoPriceText.textWrappingMode = TextWrappingModes.Normal;

        itemInfoSalvageText = CreateText(cardObject.transform, "ItemInfoSalvage", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(280f, -214f), new Vector2(560f, 130f), 22f, TextAlignmentOptions.TopLeft);
        itemInfoSalvageText.fontStyle = FontStyles.Normal;
        itemInfoSalvageText.textWrappingMode = TextWrappingModes.Normal;

        itemInfoRecipeText = CreateText(cardObject.transform, "ItemInfoRecipe", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(280f, -382f), new Vector2(560f, 166f), 22f, TextAlignmentOptions.TopLeft);
        itemInfoRecipeText.fontStyle = FontStyles.Normal;
        itemInfoRecipeText.textWrappingMode = TextWrappingModes.Normal;

        itemInfoDescriptionText = CreateText(cardObject.transform, "ItemInfoDescription", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(280f, -574f), new Vector2(560f, 180f), 23f, TextAlignmentOptions.TopLeft);
        itemInfoDescriptionText.fontStyle = FontStyles.Normal;
        itemInfoDescriptionText.textWrappingMode = TextWrappingModes.Normal;

        itemInfoCloseButton = CreateButton(cardObject.transform, "ItemInfoCloseButton", "CLOSE", new Vector2(0f, -738f), new Vector2(250f, 62f), HideItemInfoOverlay);
        StyleButton(itemInfoCloseButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.24f, 0.34f, 0.46f, 1f));

        itemInfoOverlayObject.SetActive(false);
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
        panelRect.sizeDelta = new Vector2(1120f, 480f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.07f, 0.1f, 0.14f, 0.98f);

        CreateText(panel.transform, "PlayerInventoryExtendTitle", "EXTEND INVENTORY", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1000f, 68f), 48f, TextAlignmentOptions.Center);
        playerInventoryExtendConfirmText = CreateText(panel.transform, "PlayerInventoryExtendText", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(1000f, 140f), 38f, TextAlignmentOptions.Center);
        playerInventoryExtendConfirmText.fontStyle = FontStyles.Normal;
        playerInventoryExtendConfirmText.textWrappingMode = TextWrappingModes.Normal;

        playerInventoryExtendConfirmButton = CreateButton(panel.transform, "PlayerInventoryExtendConfirmButton", "EXTEND", new Vector2(-232f, -332f), new Vector2(360f, 104f), OnPlayerInventoryExtendConfirmClicked);
        StyleReadableBackLikeButton(playerInventoryExtendConfirmButton, 36f);
        StyleButton(playerInventoryExtendConfirmButton, new Color(0.1f, 0.46f, 0.34f, 1f), new Color(0.16f, 0.62f, 0.44f, 1f));
        playerInventoryExtendCancelButton = CreateButton(panel.transform, "PlayerInventoryExtendCancelButton", "CANCEL", new Vector2(232f, -332f), new Vector2(360f, 104f), HidePlayerInventoryExtendConfirm);
        StyleReadableBackLikeButton(playerInventoryExtendCancelButton, 36f);
        StyleButton(playerInventoryExtendCancelButton, new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.22f, 0.3f, 0.4f, 1f));

        playerInventoryExtendConfirmObject.SetActive(false);
    }

    void CreateShipInventoryStartConfirm(Transform parent)
    {
        shipInventoryStartConfirmObject = new GameObject("ShipInventoryStartConfirm", typeof(RectTransform), typeof(Image));
        shipInventoryStartConfirmObject.transform.SetParent(parent, false);
        RectTransform overlayRect = shipInventoryStartConfirmObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = shipInventoryStartConfirmObject.GetComponent<Image>();
        overlayImage.color = new Color(0.01f, 0.02f, 0.035f, 0.76f);
        overlayImage.raycastTarget = true;

        GameObject panel = new GameObject("ShipInventoryStartConfirmPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(shipInventoryStartConfirmObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(640f, 274f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.07f, 0.1f, 0.14f, 0.99f);

        CreateText(panel.transform, "ShipInventoryStartConfirmTitle", "SHIP INVENTORY", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(560f, 36f), 26f, TextAlignmentOptions.Center);
        shipInventoryStartConfirmText = CreateText(panel.transform, "ShipInventoryStartConfirmText", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(560f, 82f), 22f, TextAlignmentOptions.Center);
        shipInventoryStartConfirmText.fontStyle = FontStyles.Normal;
        shipInventoryStartConfirmText.textWrappingMode = TextWrappingModes.Normal;

        shipInventoryStartConfirmNoButton = CreateButton(panel.transform, "ShipInventoryStartConfirmNoButton", "NO", new Vector2(-132f, -194f), new Vector2(210f, 58f), OnShipInventoryStartConfirmNoClicked);
        StyleButton(shipInventoryStartConfirmNoButton, new Color(0.18f, 0.25f, 0.34f, 1f), new Color(0.26f, 0.36f, 0.48f, 1f));
        shipInventoryStartConfirmYesButton = CreateButton(panel.transform, "ShipInventoryStartConfirmYesButton", "YES", new Vector2(132f, -194f), new Vector2(210f, 58f), OnShipInventoryStartConfirmYesClicked);
        StyleButton(shipInventoryStartConfirmYesButton, new Color(0.12f, 0.46f, 0.34f, 1f), new Color(0.16f, 0.62f, 0.44f, 1f));

        shipInventoryStartConfirmObject.SetActive(false);
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
        scrollbarRect.sizeDelta = new Vector2(68f, 592f);

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
        scrollbarRect.sizeDelta = new Vector2(58f, 836f);

        Image scrollbarBg = scrollbarObject.GetComponent<Image>();
        scrollbarBg.color = new Color(0.1f, 0.14f, 0.18f, 0.92f);

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
        handleImage.color = new Color(0.22f, 0.54f, 0.88f, 0.96f);

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;

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
        float scrollbarWidth = Mathf.Clamp(panelWidth * 0.035f, 30f, 58f);
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

        shopSortButton = CreateButton(panel.transform, "ShopSortButton", "SORT: A-Z", new Vector2(352f, -68f), new Vector2(246f, 52f), OnShopSortClicked);
        StyleButton(shopSortButton, new Color(0.14f, 0.19f, 0.28f, 0.98f), new Color(0.22f, 0.3f, 0.42f, 1f));
        TMP_Text sortText = shopSortButton.GetComponentInChildren<TMP_Text>(true);
        if (sortText != null)
        {
            sortText.fontSize = 18f;
            sortText.enableAutoSizing = true;
            sortText.fontSizeMin = 12f;
            sortText.fontSizeMax = 18f;
            sortText.margin = new Vector4(10f, 4f, 10f, 4f);
        }

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
        selectedTraderShop = TraderShopKind.IronJoe;
        RefreshTraderSelectionVisuals();
        RefreshShopBrowser();
        shopBrowserObject.SetActive(true);
        shopBrowserObject.transform.SetAsLastSibling();
    }

    void HideShopBrowser()
    {
        if (shopBrowserObject != null)
            shopBrowserObject.SetActive(false);
    }

    void OnShopSortClicked()
    {
        if (inventoryActionInProgress || dragInProgress || !TraderOpensShop(selectedTraderShop))
            return;

        shopSortModesByTrader[selectedTraderShop] = GetNextShopSortMode(GetShopSortMode(selectedTraderShop));
        HideItemPreview();
        RefreshShopBrowser();
    }

    ShopSortMode GetShopSortMode(TraderShopKind traderKind)
    {
        if (shopSortModesByTrader.TryGetValue(traderKind, out ShopSortMode mode))
            return mode;

        return ShopSortMode.Alphabetical;
    }

    ShopSortMode GetNextShopSortMode(ShopSortMode mode)
    {
        switch (mode)
        {
            case ShopSortMode.Alphabetical:
                return ShopSortMode.Price;
            case ShopSortMode.Price:
                return ShopSortMode.Type;
            case ShopSortMode.Type:
                return ShopSortMode.Rarity;
            default:
                return ShopSortMode.Alphabetical;
        }
    }

    string GetShopSortLabel(ShopSortMode mode)
    {
        switch (mode)
        {
            case ShopSortMode.Price:
                return "PRICE";
            case ShopSortMode.Type:
                return "TYPE";
            case ShopSortMode.Rarity:
                return "RARITY";
            default:
                return "A-Z";
        }
    }

    void RefreshShopSortButton()
    {
        if (shopSortButton == null)
            return;

        bool show = TraderOpensShop(selectedTraderShop);
        shopSortButton.gameObject.SetActive(show);
        shopSortButton.interactable = show && inventoryControlsInteractable && !inventoryActionInProgress && !dragInProgress;

        TMP_Text text = shopSortButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = "SORT: " + GetShopSortLabel(GetShopSortMode(selectedTraderShop));
    }

    void RefreshShopBrowser(bool resetScrollPosition = true)
    {
        using (ShopRefreshMarker.Auto())
        {
            if (shopBrowserObject == null || shopContentRect == null)
                return;

            float previousScrollPosition = shopScrollRect != null
                ? shopScrollRect.verticalNormalizedPosition
                : 1f;

            HideActiveShopRows();

            UpdateShopBrowserTitle();
            RefreshShopSortButton();
            if (!TraderOpensShop(selectedTraderShop))
            {
                shopBrowserObject.SetActive(false);
                return;
            }

            if (selectedTraderShop == TraderShopKind.MissEnigma)
            {
                RefreshMissEnigmaShopBrowser(resetScrollPosition, previousScrollPosition);
                return;
            }

            PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
            int astrons = profile != null ? profile.Astrons : 0;
            if (shopAstronsText != null)
                shopAstronsText.gameObject.SetActive(false);

            IReadOnlyList<InventoryItemDefinition> definitions = InventoryItemCatalog.GetAllDefinitions();
            List<ShopOfferViewModel> shopOffers = new List<ShopOfferViewModel>();

            for (int i = 0; i < definitions.Count; i++)
            {
                InventoryItemDefinition definition = definitions[i];
                if (!ShouldTraderSellDefinition(selectedTraderShop, definition))
                    continue;

                int price = PlayerProfileService.Instance.GetShopBuyPriceAstrons(definition.Id);
                if (price <= 0)
                    continue;

                shopOffers.Add(new ShopOfferViewModel
                {
                    Definition = definition,
                    Price = price,
                    CanAfford = astrons >= price
                });
            }

            SortShopOffers(shopOffers);

            for (int i = 0; i < shopOffers.Count; i += 2)
            {
                ShopOfferViewModel leftOffer = shopOffers[i];
                ShopOfferViewModel rightOffer = i + 1 < shopOffers.Count ? shopOffers[i + 1] : null;
                InventoryItemDefinition leftDefinition = leftOffer.Definition;
                int leftPrice = leftOffer.Price;
                bool leftCanAfford = leftOffer.CanAfford;
                InventoryItemDefinition rightDefinition = rightOffer != null ? rightOffer.Definition : null;
                int rightPrice = rightOffer != null ? rightOffer.Price : 0;
                bool rightCanAfford = rightOffer != null && rightOffer.CanAfford;

                GameObject row = CreateShopRow(shopRowObjects.Count, leftDefinition, leftPrice, leftCanAfford, rightDefinition, rightPrice, rightCanAfford);
                if (row != null)
                    shopRowObjects.Add(row);
            }

            ApplyShopScrollPosition(resetScrollPosition ? 1f : previousScrollPosition);
        }
    }

    void ApplyShopScrollPosition(float normalizedPosition)
    {
        Canvas.ForceUpdateCanvases();
        if (shopScrollRect == null)
            return;

        shopScrollRect.StopMovement();
        shopScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
    }

    void HideActiveShopRows()
    {
        for (int i = 0; i < shopRowObjects.Count; i++)
        {
            if (shopRowObjects[i] != null)
                shopRowObjects[i].SetActive(false);
        }

        shopRowObjects.Clear();
    }

    void RefreshMissEnigmaShopBrowser(bool resetScrollPosition, float previousScrollPosition)
    {
        BlueprintTradeOffer[] offers = BlueprintCatalog.GetMissEnigmaOffers();
        List<MissEnigmaOfferViewModel> visibleOffers = new List<MissEnigmaOfferViewModel>();
        InventoryItemDefinition avengerCodesDefinition = InventoryItemCatalog.GetDefinition(InventoryItemCatalog.AvengerStartingCodesId);
        bool showAvengerCodesOffer = avengerCodesDefinition != null &&
            PlayerProfileService.Instance.CanBuyAvengerStartingCodes();
        int avengerCodesPrice = showAvengerCodesOffer
            ? PlayerProfileService.Instance.GetShopBuyPriceAstrons(InventoryItemCatalog.AvengerStartingCodesId)
            : 0;

        for (int i = 0; i < offers.Length; i++)
        {
            BlueprintTradeOffer offer = offers[i];
            if (offer == null || string.IsNullOrWhiteSpace(offer.BlueprintItemId))
                continue;

            if (PlayerProfileService.Instance.IsBlueprintUnlocked(offer.BlueprintItemId))
                continue;

            if (PlayerProfileService.Instance.IsMissEnigmaBlueprintPurchased(offer.BlueprintItemId))
                continue;

            InventoryItemDefinition blueprintDefinition = InventoryItemCatalog.GetDefinition(offer.BlueprintItemId);
            if (blueprintDefinition == null)
                continue;

            string targetItemId = InventoryItemCatalog.GetBlueprintTargetItemId(offer.BlueprintItemId);
            InventoryItemDefinition targetDefinition = InventoryItemCatalog.GetDefinition(targetItemId);
            visibleOffers.Add(new MissEnigmaOfferViewModel
            {
                Offer = offer,
                CanAfford = PlayerProfileService.Instance.CanAffordItemTrade(offer.CostItemIds),
                BlueprintDefinition = blueprintDefinition,
                TargetDefinition = targetDefinition,
                EstimatedTradeValue = GetMissEnigmaTradeValue(offer.CostItemIds)
            });
        }

        if (visibleOffers.Count == 0 && !showAvengerCodesOffer)
        {
            GameObject rowObject = GetOrCreateMissEnigmaEmptyRow();
            rowObject.transform.SetParent(shopContentRect, false);
            rowObject.transform.SetSiblingIndex(0);
            rowObject.SetActive(true);
            shopRowObjects.Add(rowObject);
            ApplyShopScrollPosition(resetScrollPosition ? 1f : previousScrollPosition);
            return;
        }

        if (showAvengerCodesOffer && avengerCodesPrice > 0)
        {
            PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
            bool canAffordCodes = profile != null && profile.Astrons >= avengerCodesPrice;
            GameObject codesRow = CreateShopRow(shopRowObjects.Count, avengerCodesDefinition, avengerCodesPrice, canAffordCodes, null, 0, false);
            if (codesRow != null)
                shopRowObjects.Add(codesRow);
        }

        SortMissEnigmaOffers(visibleOffers);

        for (int i = 0; i < visibleOffers.Count; i += 2)
        {
            MissEnigmaOfferViewModel leftOffer = visibleOffers[i];
            MissEnigmaOfferViewModel rightOffer = i + 1 < visibleOffers.Count ? visibleOffers[i + 1] : null;

            GameObject row = CreateMissEnigmaShopRow(
                shopRowObjects.Count,
                leftOffer.Offer,
                leftOffer.CanAfford,
                rightOffer != null ? rightOffer.Offer : null,
                rightOffer != null && rightOffer.CanAfford);
            if (row != null)
                shopRowObjects.Add(row);
        }

        ApplyShopScrollPosition(resetScrollPosition ? 1f : previousScrollPosition);
    }

    void SortShopOffers(List<ShopOfferViewModel> offers)
    {
        if (offers == null || offers.Count <= 1)
            return;

        ShopSortMode mode = GetShopSortMode(selectedTraderShop);
        offers.Sort((a, b) => CompareShopOffers(a, b, mode));
    }

    int CompareShopOffers(ShopOfferViewModel a, ShopOfferViewModel b, ShopSortMode mode)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int result;
        switch (mode)
        {
            case ShopSortMode.Price:
                result = a.Price.CompareTo(b.Price);
                if (result != 0)
                    return result;
                break;
            case ShopSortMode.Type:
                result = GetItemSortCategory(a.Definition).CompareTo(GetItemSortCategory(b.Definition));
                if (result != 0)
                    return result;
                break;
            case ShopSortMode.Rarity:
                result = ((int)GetItemSortRarity(b.Definition)).CompareTo((int)GetItemSortRarity(a.Definition));
                if (result != 0)
                    return result;
                result = GetItemSortCategory(a.Definition).CompareTo(GetItemSortCategory(b.Definition));
                if (result != 0)
                    return result;
                break;
        }

        return CompareItemDefinitionNames(a.Definition, b.Definition);
    }

    void SortMissEnigmaOffers(List<MissEnigmaOfferViewModel> offers)
    {
        if (offers == null || offers.Count <= 1)
            return;

        ShopSortMode mode = GetShopSortMode(selectedTraderShop);
        offers.Sort((a, b) => CompareMissEnigmaOffers(a, b, mode));
    }

    int CompareMissEnigmaOffers(MissEnigmaOfferViewModel a, MissEnigmaOfferViewModel b, ShopSortMode mode)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        InventoryItemDefinition aDefinition = GetMissEnigmaSortDefinition(a);
        InventoryItemDefinition bDefinition = GetMissEnigmaSortDefinition(b);

        int result;
        switch (mode)
        {
            case ShopSortMode.Price:
                result = a.EstimatedTradeValue.CompareTo(b.EstimatedTradeValue);
                if (result != 0)
                    return result;
                break;
            case ShopSortMode.Type:
                result = GetItemSortCategory(aDefinition).CompareTo(GetItemSortCategory(bDefinition));
                if (result != 0)
                    return result;
                break;
            case ShopSortMode.Rarity:
                result = ((int)GetItemSortRarity(bDefinition)).CompareTo((int)GetItemSortRarity(aDefinition));
                if (result != 0)
                    return result;
                result = GetItemSortCategory(aDefinition).CompareTo(GetItemSortCategory(bDefinition));
                if (result != 0)
                    return result;
                break;
        }

        return CompareItemDefinitionNames(aDefinition, bDefinition);
    }

    InventoryItemDefinition GetMissEnigmaSortDefinition(MissEnigmaOfferViewModel offer)
    {
        if (offer == null)
            return null;

        return offer.TargetDefinition ?? offer.BlueprintDefinition;
    }

    int GetMissEnigmaTradeValue(string[] costItemIds)
    {
        if (costItemIds == null || costItemIds.Length == 0)
            return 0;

        int value = 0;
        for (int i = 0; i < costItemIds.Length; i++)
        {
            string itemId = costItemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            int itemValue = InventoryItemCatalog.GetSellValueAstrons(itemId);
            if (itemValue <= 0)
                itemValue = InventoryItemCatalog.GetShopBuyValueAstrons(itemId);
            value += Mathf.Max(1, itemValue);
        }

        return value;
    }

    InventoryItemCategory GetItemSortCategory(InventoryItemDefinition definition)
    {
        return definition != null ? definition.Category : InventoryItemCategory.Misc;
    }

    InventoryItemRarity GetItemSortRarity(InventoryItemDefinition definition)
    {
        return definition != null ? definition.Rarity : InventoryItemRarity.Common;
    }

    int CompareItemDefinitionNames(InventoryItemDefinition a, InventoryItemDefinition b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int result = string.Compare(a.DisplayName ?? string.Empty, b.DisplayName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        if (result != 0)
            return result;

        return string.CompareOrdinal(a.Id ?? string.Empty, b.Id ?? string.Empty);
    }

    GameObject GetOrCreateMissEnigmaEmptyRow()
    {
        if (missEnigmaEmptyRowObject != null)
            return missEnigmaEmptyRowObject;

        missEnigmaEmptyRowObject = new GameObject("ShopRow_MissEnigmaEmpty", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        missEnigmaEmptyRowObject.transform.SetParent(shopContentRect, false);
        missEnigmaEmptyRowObject.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 180f);
        missEnigmaEmptyRowObject.GetComponent<LayoutElement>().preferredHeight = 180f;
        missEnigmaEmptyRowObject.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.21f, 0.98f);
        missEnigmaEmptyText = CreateText(missEnigmaEmptyRowObject.transform, "MissEnigmaEmptyText", "ALL BLUEPRINTS SOLD", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 24f, TextAlignmentOptions.Center);
        missEnigmaEmptyText.fontStyle = FontStyles.Bold;
        missEnigmaEmptyText.color = new Color(0.84f, 0.9f, 0.96f, 0.96f);
        return missEnigmaEmptyRowObject;
    }

    GameObject CreateMissEnigmaShopRow(
        int rowIndex,
        BlueprintTradeOffer leftOffer,
        bool leftCanAfford,
        BlueprintTradeOffer rightOffer,
        bool rightCanAfford)
    {
        if (shopContentRect == null || leftOffer == null)
            return null;

        MissEnigmaRowView rowView = GetOrCreateMissEnigmaShopRowView(rowIndex);
        if (rowView == null || rowView.Root == null)
            return null;

        GameObject rowObject = rowView.Root;
        rowObject.name = "ShopRow_" + leftOffer.BlueprintItemId;
        rowObject.transform.SetParent(shopContentRect, false);
        rowObject.transform.SetSiblingIndex(rowIndex);
        rowObject.SetActive(true);
        rowObject.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 326f);
        rowObject.GetComponent<LayoutElement>().preferredHeight = 326f;
        rowObject.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.21f, 0.98f);

        UpdateMissEnigmaBlueprintCard(rowView.Left, leftOffer, leftCanAfford);
        UpdateMissEnigmaBlueprintCard(rowView.Right, rightOffer, rightCanAfford);

        return rowObject;
    }

    MissEnigmaRowView GetOrCreateMissEnigmaShopRowView(int rowIndex)
    {
        while (missEnigmaShopRowPool.Count <= rowIndex)
            missEnigmaShopRowPool.Add(null);

        MissEnigmaRowView rowView = missEnigmaShopRowPool[rowIndex];
        if (rowView != null && rowView.Root != null)
            return rowView;

        GameObject rowObject = new GameObject("ShopRow_MissEnigmaPool_" + rowIndex, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowObject.transform.SetParent(shopContentRect, false);

        rowView = new MissEnigmaRowView
        {
            Root = rowObject,
            Left = CreateMissEnigmaBlueprintCardView(rowObject.transform, "Left", -130f),
            Right = CreateMissEnigmaBlueprintCardView(rowObject.transform, "Right", 130f)
        };
        missEnigmaShopRowPool[rowIndex] = rowView;
        return rowView;
    }

    MissEnigmaCardView CreateMissEnigmaBlueprintCardView(Transform parent, string suffix, float centerX)
    {
        MissEnigmaCardView view = new MissEnigmaCardView();
        view.CardButton = CreateButton(parent, "ShopItemCardButton_MissEnigma_" + suffix, string.Empty, new Vector2(centerX, -12f), new Vector2(214f, 214f), null);
        view.Root = view.CardButton.gameObject;
        view.CardImage = view.CardButton.GetComponent<Image>();

        Outline itemCardOutline = view.CardButton.GetComponent<Outline>();
        if (itemCardOutline == null)
            itemCardOutline = view.CardButton.gameObject.AddComponent<Outline>();
        itemCardOutline.effectColor = new Color(0.08f, 0.76f, 0.94f, 0.38f);
        itemCardOutline.effectDistance = new Vector2(4f, 4f);

        GameObject iconObject = new GameObject("ShopItemIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(view.CardButton.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -12f);
        iconRect.sizeDelta = new Vector2(108f, 108f);
        view.IconImage = iconObject.GetComponent<Image>();
        view.IconImage.preserveAspect = true;
        view.IconImage.raycastTarget = false;

        view.NameText = CreateText(view.CardButton.transform, "ShopItemName", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(182f, 44f), 18f, TextAlignmentOptions.Center);
        view.NameText.enableAutoSizing = true;
        view.NameText.fontSizeMin = 11f;
        view.NameText.fontSizeMax = 18f;
        view.NameText.textWrappingMode = TextWrappingModes.Normal;
        view.NameText.overflowMode = TextOverflowModes.Truncate;
        view.NameText.raycastTarget = false;

        view.CostText = CreateText(view.CardButton.transform, "ShopItemCost", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(198f, 58f), 20f, TextAlignmentOptions.Center);
        view.CostText.enableAutoSizing = true;
        view.CostText.fontSizeMin = 14f;
        view.CostText.fontSizeMax = 20f;
        view.CostText.textWrappingMode = TextWrappingModes.Normal;
        view.CostText.raycastTarget = false;

        view.TradeButton = CreateButton(parent, "ShopTradeButton_MissEnigma_" + suffix, "TRADE", new Vector2(centerX, -258f), new Vector2(150f, 50f), null);
        view.TradeText = view.TradeButton.GetComponentInChildren<TMP_Text>(true);
        StyleButton(view.TradeButton, new Color(0.18f, 0.36f, 0.5f, 1f), new Color(0.26f, 0.52f, 0.68f, 1f));

        return view;
    }

    void UpdateMissEnigmaBlueprintCard(MissEnigmaCardView view, BlueprintTradeOffer offer, bool canAfford)
    {
        if (view == null)
            return;

        InventoryItemDefinition definition = offer != null
            ? InventoryItemCatalog.GetDefinition(offer.BlueprintItemId)
            : null;
        bool active = definition != null;
        if (view.Root != null)
            view.Root.SetActive(active);
        if (view.TradeButton != null)
            view.TradeButton.gameObject.SetActive(active);
        if (!active)
            return;

        string blueprintItemId = offer.BlueprintItemId;
        view.CardButton.onClick.RemoveAllListeners();
        view.CardButton.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            OnShopItemPreviewClicked(blueprintItemId);
        });
        view.CardButton.interactable = !inventoryActionInProgress;

        if (view.CardImage != null)
            view.CardImage.color = InventoryItemCatalog.GetRarityColor(definition.Rarity);
        if (view.IconImage != null)
            view.IconImage.sprite = definition.GetIcon();
        if (view.NameText != null)
            view.NameText.text = definition.DisplayName.ToUpperInvariant();
        if (view.CostText != null)
        {
            view.CostText.text = FormatTradeCost(offer.CostItemIds);
            view.CostText.color = canAfford ? new Color(0.94f, 0.84f, 0.44f, 1f) : new Color(0.78f, 0.46f, 0.46f, 1f);
        }

        view.TradeButton.onClick.RemoveAllListeners();
        view.TradeButton.onClick.AddListener(() =>
        {
            OnMissEnigmaTradeClicked(blueprintItemId);
        });
        view.TradeButton.interactable = canAfford && !inventoryActionInProgress;
        if (view.TradeText != null)
            view.TradeText.text = "TRADE";
    }

    void CreateMissEnigmaBlueprintCard(Transform parent, BlueprintTradeOffer offer, bool canAfford, float centerX)
    {
        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(offer.BlueprintItemId);
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
        itemCardOutline.effectColor = new Color(0.08f, 0.76f, 0.94f, 0.38f);
        itemCardOutline.effectDistance = new Vector2(4f, 4f);

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

        TMP_Text nameText = CreateText(itemCardButton.transform, "ShopItemName", definition.DisplayName.ToUpperInvariant(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(182f, 44f), 18f, TextAlignmentOptions.Center);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 11f;
        nameText.fontSizeMax = 18f;
        nameText.textWrappingMode = TextWrappingModes.Normal;
        nameText.overflowMode = TextOverflowModes.Truncate;
        nameText.raycastTarget = false;

        TMP_Text costText = CreateText(itemCardButton.transform, "ShopItemCost", FormatTradeCost(offer.CostItemIds), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(190f, 42f), 16f, TextAlignmentOptions.Center);
        costText.enableAutoSizing = true;
        costText.fontSizeMin = 11f;
        costText.fontSizeMax = 16f;
        costText.textWrappingMode = TextWrappingModes.Normal;
        costText.color = canAfford ? new Color(0.94f, 0.84f, 0.44f, 1f) : new Color(0.78f, 0.46f, 0.46f, 1f);
        costText.raycastTarget = false;

        Button tradeButton = CreateButton(parent, "ShopTradeButton_" + definition.Id, "TRADE", new Vector2(centerX, -258f), new Vector2(150f, 50f), () => OnMissEnigmaTradeClicked(offer.BlueprintItemId));
        StyleButton(tradeButton, new Color(0.18f, 0.36f, 0.5f, 1f), new Color(0.26f, 0.52f, 0.68f, 1f));
        tradeButton.interactable = canAfford && !inventoryActionInProgress;
    }

    string FormatTradeCost(string[] costItemIds)
    {
        if (costItemIds == null || costItemIds.Length == 0)
            return "FREE";

        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < costItemIds.Length; i++)
        {
            string itemId = costItemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            counts.TryGetValue(itemId, out int count);
            counts[itemId] = count + 1;
        }

        List<string> parts = new List<string>();
        foreach (KeyValuePair<string, int> entry in counts)
        {
            parts.Add(InventoryItemCatalog.GetDisplayName(entry.Key) + (entry.Value > 1 ? " x" + entry.Value : string.Empty));
        }

        parts.Sort(StringComparer.Ordinal);
        return string.Join("\n", parts);
    }

    void UpdateShopBrowserTitle()
    {
        TMP_Text title = shopBrowserObject?.transform.Find("ShopBrowserPanel/ShopBrowserTitle")?.GetComponent<TMP_Text>();
        if (title == null)
            return;

        title.text = TraderOpensShop(selectedTraderShop)
            ? GetTraderDisplayName(selectedTraderShop)
            : "TRADER";
    }

    GameObject CreateShopRow(
        int rowIndex,
        InventoryItemDefinition leftDefinition,
        int leftPrice,
        bool leftCanAfford,
        InventoryItemDefinition rightDefinition,
        int rightPrice,
        bool rightCanAfford)
    {
        if (shopContentRect == null || leftDefinition == null)
            return null;

        ShopRowView rowView = GetOrCreateShopRowView(rowIndex);
        if (rowView == null || rowView.Root == null)
            return null;

        GameObject rowObject = rowView.Root;
        rowObject.name = "ShopRow_" + leftDefinition.Id;
        rowObject.transform.SetParent(shopContentRect, false);
        rowObject.transform.SetSiblingIndex(rowIndex);
        rowObject.SetActive(true);

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 294f);

        LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 294f;

        Image rowImage = rowObject.GetComponent<Image>();
        rowImage.color = new Color(0.12f, 0.16f, 0.21f, 0.98f);

        UpdateShopCard(rowView.Left, leftDefinition, leftPrice, leftCanAfford);
        UpdateShopCard(rowView.Right, rightDefinition, rightPrice, rightCanAfford);

        return rowObject;
    }

    ShopRowView GetOrCreateShopRowView(int rowIndex)
    {
        while (shopRowPool.Count <= rowIndex)
            shopRowPool.Add(null);

        ShopRowView rowView = shopRowPool[rowIndex];
        if (rowView != null && rowView.Root != null)
            return rowView;

        GameObject rowObject = new GameObject("ShopRowPool_" + rowIndex, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowObject.transform.SetParent(shopContentRect, false);

        rowView = new ShopRowView
        {
            Root = rowObject,
            Left = CreateShopCardView(rowObject.transform, "Left", -130f),
            Right = CreateShopCardView(rowObject.transform, "Right", 130f)
        };
        shopRowPool[rowIndex] = rowView;
        return rowView;
    }

    ShopCardView CreateShopCardView(Transform parent, string suffix, float centerX)
    {
        ShopCardView view = new ShopCardView();
        view.CardButton = CreateButton(parent, "ShopItemCardButton_" + suffix, string.Empty, new Vector2(centerX, -12f), new Vector2(214f, 214f), null);
        view.Root = view.CardButton.gameObject;
        view.CardImage = view.CardButton.GetComponent<Image>();

        Outline itemCardOutline = view.CardButton.GetComponent<Outline>();
        if (itemCardOutline == null)
            itemCardOutline = view.CardButton.gameObject.AddComponent<Outline>();
        itemCardOutline.effectColor = new Color(0f, 0f, 0f, 0.28f);
        itemCardOutline.effectDistance = new Vector2(4f, 4f);

        Shadow itemCardShadow = view.CardButton.GetComponent<Shadow>();
        if (itemCardShadow == null)
            itemCardShadow = view.CardButton.gameObject.AddComponent<Shadow>();
        itemCardShadow.effectColor = new Color(0f, 0f, 0f, 0.2f);
        itemCardShadow.effectDistance = new Vector2(0f, -3f);

        GameObject iconObject = new GameObject("ShopItemIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(view.CardButton.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -12f);
        iconRect.sizeDelta = new Vector2(108f, 108f);
        view.IconImage = iconObject.GetComponent<Image>();
        view.IconImage.preserveAspect = true;
        view.IconImage.raycastTarget = false;

        view.NameText = CreateText(view.CardButton.transform, "ShopItemName", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(182f, 28f), 18f, TextAlignmentOptions.Center);
        view.NameText.enableAutoSizing = true;
        view.NameText.fontSizeMin = 12f;
        view.NameText.fontSizeMax = 18f;
        view.NameText.textWrappingMode = TextWrappingModes.Normal;
        view.NameText.overflowMode = TextOverflowModes.Truncate;
        view.NameText.raycastTarget = false;

        view.TypeText = CreateText(view.CardButton.transform, "ShopItemType", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(182f, 22f), 15f, TextAlignmentOptions.Center);
        view.TypeText.fontStyle = FontStyles.Bold;
        view.TypeText.color = new Color(0.92f, 0.96f, 1f, 0.96f);
        view.TypeText.raycastTarget = false;

        GameObject priceIconObject = new GameObject("ShopItemPriceIcon", typeof(RectTransform), typeof(Image));
        priceIconObject.transform.SetParent(view.CardButton.transform, false);
        RectTransform priceIconRect = priceIconObject.GetComponent<RectTransform>();
        priceIconRect.anchorMin = new Vector2(0.5f, 1f);
        priceIconRect.anchorMax = new Vector2(0.5f, 1f);
        priceIconRect.pivot = new Vector2(0.5f, 0.5f);
        priceIconRect.anchoredPosition = new Vector2(-42f, -176f);
        priceIconRect.sizeDelta = new Vector2(28f, 28f);
        view.PriceIcon = priceIconObject.GetComponent<Image>();
        view.PriceIcon.sprite = LoadSpriteFromResources("UI/icon_astrons_coin");
        view.PriceIcon.color = new Color(1f, 1f, 1f, 0.96f);
        view.PriceIcon.preserveAspect = true;
        view.PriceIcon.raycastTarget = false;

        view.PriceText = CreateText(view.CardButton.transform, "ShopItemPrice", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(28f, -176f), new Vector2(112f, 28f), 21f, TextAlignmentOptions.MidlineLeft);
        view.PriceText.fontStyle = FontStyles.Normal;
        view.PriceText.raycastTarget = false;

        view.BuyButton = CreateButton(parent, "ShopBuyButton_" + suffix, "BUY", new Vector2(centerX, -236f), new Vector2(150f, 50f), null);
        view.BuyText = view.BuyButton.GetComponentInChildren<TMP_Text>(true);
        StyleButton(view.BuyButton, new Color(0.12f, 0.46f, 0.34f, 1f), new Color(0.16f, 0.62f, 0.44f, 1f));

        return view;
    }

    void UpdateShopCard(ShopCardView view, InventoryItemDefinition definition, int price, bool canAfford)
    {
        if (view == null)
            return;

        bool active = definition != null;
        if (view.Root != null)
            view.Root.SetActive(active);
        if (view.BuyButton != null)
            view.BuyButton.gameObject.SetActive(active);
        if (!active)
            return;

        string itemId = definition.Id;
        view.CardButton.onClick.RemoveAllListeners();
        view.CardButton.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayClick();
            OnShopItemPreviewClicked(itemId);
        });
        view.CardButton.interactable = !inventoryActionInProgress;

        if (view.CardImage != null)
            view.CardImage.color = InventoryItemCatalog.GetRarityColor(definition.Rarity);
        if (view.IconImage != null)
            view.IconImage.sprite = definition.GetIcon();
        if (view.NameText != null)
            view.NameText.text = definition.DisplayName.ToUpperInvariant();
        if (view.TypeText != null)
            view.TypeText.text = InventoryItemCatalog.GetCategoryLabel(definition.Id);
        if (view.PriceText != null)
        {
            view.PriceText.text = price.ToString();
            view.PriceText.color = canAfford ? new Color(0.94f, 0.84f, 0.44f, 1f) : new Color(0.78f, 0.46f, 0.46f, 1f);
        }

        view.BuyButton.onClick.RemoveAllListeners();
        view.BuyButton.onClick.AddListener(() =>
        {
            OnShopBuyClicked(itemId);
        });
        view.BuyButton.interactable = canAfford && !inventoryActionInProgress;
        if (view.BuyText != null)
            view.BuyText.text = "BUY";
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
        priceIconRect.pivot = new Vector2(0.5f, 0.5f);
        priceIconRect.anchoredPosition = new Vector2(-42f, -176f);
        priceIconRect.sizeDelta = new Vector2(28f, 28f);

        Image priceIcon = priceIconObject.GetComponent<Image>();
        priceIcon.sprite = LoadSpriteFromResources("UI/icon_astrons_coin");
        priceIcon.color = new Color(1f, 1f, 1f, 0.96f);
        priceIcon.preserveAspect = true;
        priceIcon.raycastTarget = false;

        TMP_Text priceText = CreateText(itemCardButton.transform, "ShopItemPrice", price.ToString(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(28f, -176f), new Vector2(112f, 28f), 21f, TextAlignmentOptions.MidlineLeft);
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
                AudioManager.Instance?.PlayCash();
                RefreshView();
            }
            else
            {
                SetStatus("Not enough Astrons or inventory space.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Shop purchase failed: " + ex);
            SetStatus("Purchase failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInventoryInteractable(true);
            RefreshShopBrowser();
        }
    }

    async void OnMissEnigmaTradeClicked(string blueprintItemId)
    {
        if (inventoryActionInProgress || string.IsNullOrWhiteSpace(blueprintItemId))
            return;

        BlueprintTradeOffer offer = BlueprintCatalog.GetMissEnigmaOffer(blueprintItemId);
        if (offer == null)
            return;

        inventoryActionInProgress = true;
        SetInventoryInteractable(false);
        SetStatus("Trading for " + InventoryItemCatalog.GetDisplayName(blueprintItemId) + "...");

        try
        {
            bool traded = await PlayerProfileService.Instance.TryPurchaseMissEnigmaBlueprintAsync(blueprintItemId);
            if (traded)
            {
                SetStatus("Traded for " + InventoryItemCatalog.GetDisplayName(blueprintItemId) + ".");
                AudioManager.Instance?.PlayCash();
                RefreshView();
            }
            else
            {
                SetStatus("Missing trade items or inventory space.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Miss Enigma trade failed: " + ex);
            SetStatus("Trade failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInventoryInteractable(true);
            RefreshShopBrowser();
        }
    }

    void RefreshCraftingRecipeBrowser(bool forceRebuild = false)
    {
        using var marker = CraftingRecipeRefreshMarker.Auto();

        if (craftingRecipeBrowserObject == null || craftingRecipeContentRect == null)
            return;

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        PlayerInventoryData inventory = profile != null && profile.Inventory != null
            ? profile.Inventory.Clone()
            : PlayerInventoryData.Default();
        inventory.Normalize();

        string signature = BuildCraftingRecipeBrowserSignature(inventory);
        if (!forceRebuild &&
            craftingRecipeBrowserSignatureValid &&
            string.Equals(craftingRecipeBrowserSignature, signature, StringComparison.Ordinal))
        {
            RefreshCraftingRecipeAvailabilityButton();
            SetCraftingRecipeRowsInteractable(inventoryControlsInteractable);
            return;
        }

        float previousScrollPosition = craftingRecipeScrollRect != null
            ? craftingRecipeScrollRect.verticalNormalizedPosition
            : 1f;

        craftingRecipeRowObjects.Clear();
        craftingRecipeResultButtons.Clear();
        craftingRecipeBrowserSignature = signature;
        craftingRecipeBrowserSignatureValid = true;
        HashSet<string> visibleRecipeIds = new HashSet<string>(StringComparer.Ordinal);

        IReadOnlyList<PlayerProfileCraftingRecipe> recipes = PlayerProfileCraftingCatalog.GetAllRecipes();
        if (recipes == null)
            return;

        List<PlayerProfileCraftingRecipe> orderedRecipes = new List<PlayerProfileCraftingRecipe>(recipes.Count);
        for (int pass = 0; pass < 2; pass++)
        {
            bool unlockedPass = pass == 0;
            for (int i = 0; i < recipes.Count; i++)
            {
                PlayerProfileCraftingRecipe recipe = recipes[i];
                if (recipe == null)
                    continue;

                if (IsCraftingRecipeUnlocked(recipe) == unlockedPass)
                    orderedRecipes.Add(recipe);
            }
        }

        for (int i = 0; i < orderedRecipes.Count; i++)
        {
            PlayerProfileCraftingRecipe recipe = orderedRecipes[i];
            if (recipe == null)
                continue;

            bool craftable = CanPrepareCraftingRecipe(inventory, recipe);
            if (craftingRecipeShowAvailableOnly && !craftable)
                continue;

            if (!craftingRecipeRowsById.TryGetValue(recipe.Id, out CraftingRecipeRowView rowView) || rowView == null || rowView.Root == null)
            {
                rowView = CreateCraftingRecipeRow(recipe, craftable, inventory);
                if (rowView != null)
                    craftingRecipeRowsById[recipe.Id] = rowView;
            }

            if (rowView == null || rowView.Root == null)
                continue;

            UpdateCraftingRecipeRowView(rowView, recipe, craftable, inventory);
            rowView.Root.SetActive(true);
            rowView.Root.transform.SetSiblingIndex(craftingRecipeRowObjects.Count);
            visibleRecipeIds.Add(recipe.Id);
            craftingRecipeRowObjects.Add(rowView.Root);
            if (rowView.ResultButton != null)
                craftingRecipeResultButtons.Add(rowView.ResultButton);
        }

        foreach (KeyValuePair<string, CraftingRecipeRowView> entry in craftingRecipeRowsById)
        {
            CraftingRecipeRowView rowView = entry.Value;
            if (rowView != null && rowView.Root != null && !visibleRecipeIds.Contains(entry.Key))
                rowView.Root.SetActive(false);
        }

        RefreshCraftingRecipeAvailabilityButton();
        Canvas.ForceUpdateCanvases();
        if (craftingRecipeScrollRect != null)
        {
            craftingRecipeScrollRect.verticalNormalizedPosition = resetCraftingRecipeScrollOnNextRefresh
                ? 1f
                : Mathf.Clamp01(previousScrollPosition);
            resetCraftingRecipeScrollOnNextRefresh = false;
        }
    }

    string BuildCraftingRecipeBrowserSignature(PlayerInventoryData inventory)
    {
        StringBuilder builder = new StringBuilder(512);
        builder.Append(craftingRecipeShowAvailableOnly ? "available|" : "all|");
        AppendInventorySlotsSignature(builder, inventory != null ? inventory.PlayerSlots : null);
        builder.Append('|');
        AppendInventorySlotsSignature(builder, inventory != null ? inventory.ShipSlots : null);
        builder.Append('|');
        AppendInventorySlotsSignature(builder, inventory != null ? inventory.CraftingSlots : null);
        builder.Append('|');
        AppendInventorySlotsSignature(builder, PlayerProfileService.Instance.CurrentProfile != null ? PlayerProfileService.Instance.CurrentProfile.UnlockedBlueprintIds : null);
        return builder.ToString();
    }

    void AppendInventorySlotsSignature(StringBuilder builder, string[] slots)
    {
        if (builder == null)
            return;

        if (slots == null)
        {
            builder.Append("null");
            return;
        }

        builder.Append(slots.Length);
        builder.Append(':');
        for (int i = 0; i < slots.Length; i++)
        {
            builder.Append(i);
            builder.Append('=');
            builder.Append(slots[i] ?? string.Empty);
            builder.Append(';');
        }
    }

    void SetCraftingRecipeRowsInteractable(bool interactable)
    {
        bool canInteract = interactable && (preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress);
        for (int i = 0; i < craftingRecipeResultButtons.Count; i++)
        {
            Button button = craftingRecipeResultButtons[i];
            if (button != null)
                button.interactable = canInteract;
        }
    }

    CraftingRecipeRowView CreateCraftingRecipeRow(PlayerProfileCraftingRecipe recipe, bool craftable, PlayerInventoryData inventory)
    {
        if (craftingRecipeContentRect == null || recipe == null)
            return null;

        GameObject rowObject = new GameObject("CraftingRecipeRow_" + recipe.Id, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowObject.transform.SetParent(craftingRecipeContentRect, false);
        CraftingRecipeRowView rowView = new CraftingRecipeRowView
        {
            RecipeId = recipe.Id,
            Root = rowObject
        };

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 196f);

        LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 196f;

        Image rowImage = rowObject.GetComponent<Image>();
        rowImage.color = new Color(0.12f, 0.16f, 0.21f, 0.98f);

        Button resultButton = CreateButton(rowObject.transform, "RecipeResultButton", string.Empty, new Vector2(-228f, -10f), new Vector2(174f, 174f), () => OnCraftingRecipeSelected(recipe.Id));
        ConfigureNoBlinkInventoryActionButton(resultButton);
        resultButton.interactable = inventoryControlsInteractable && (preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress);
        rowView.ResultButton = resultButton;

        Image resultButtonImage = resultButton.GetComponent<Image>();
        rowView.ResultButtonImage = resultButtonImage;
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
        rowView.ResultOutline = resultOutline;

        resultOutline.effectColor = craftable
            ? new Color(0.23f, 0.92f, 0.49f, 0.95f)
            : new Color(0.28f, 0.36f, 0.44f, 0.8f);
        resultOutline.effectDistance = craftable ? new Vector2(4f, 4f) : new Vector2(2f, 2f);

        Shadow resultShadow = resultButton.GetComponent<Shadow>();
        if (resultShadow == null)
            resultShadow = resultButton.gameObject.AddComponent<Shadow>();
        rowView.ResultShadow = resultShadow;

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

        rowView.ResultFrameRects = new RectTransform[4];
        rowView.ResultFrameImages = new Image[4];

        void CreateFrameBar(int index, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
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
            if (index >= 0 && index < rowView.ResultFrameRects.Length)
            {
                rowView.ResultFrameRects[index] = barRect;
                rowView.ResultFrameImages[index] = barImage;
            }
        }

        CreateFrameBar(0, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -frameThickness), new Vector2(0f, 0f));
        CreateFrameBar(1, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, frameThickness));
        CreateFrameBar(2, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(frameThickness, 0f));
        CreateFrameBar(3, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-frameThickness, 0f), new Vector2(0f, 0f));

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
        rowView.ResultPriceText = resultPrice;

        TMP_Text resultLock = CreateText(resultInner.transform, "RecipeResultLock", "BLUEPRINT\nREQUIRED", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -142f), new Vector2(146f, 38f), 12f, TextAlignmentOptions.Center);
        resultLock.fontStyle = FontStyles.Bold;
        resultLock.color = new Color(1f, 0.66f, 0.36f, 0.98f);
        resultLock.textWrappingMode = TextWrappingModes.Normal;
        resultLock.gameObject.SetActive(false);
        rowView.ResultLockText = resultLock;

        bool[] missingIngredients = ResolveMissingRecipeIngredients(inventory, recipe);
        rowView.MissingIngredientFrames = new GameObject[recipe.Inputs.Length];
        float ingredientStartX = 16f;
        for (int i = 0; i < recipe.Inputs.Length; i++)
        {
            string itemId = recipe.Inputs[i];
            bool missingIngredient = missingIngredients != null && i < missingIngredients.Length && missingIngredients[i];
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

            GameObject missingFrame = CreateRecipeIngredientMissingFrame(ingredientObject.transform);
            if (missingFrame != null)
            {
                missingFrame.SetActive(missingIngredient);
                rowView.MissingIngredientFrames[i] = missingFrame;
            }
        }

        UpdateCraftingRecipeRowView(rowView, recipe, craftable, inventory);
        return rowView;
    }

    void UpdateCraftingRecipeRowView(CraftingRecipeRowView rowView, PlayerProfileCraftingRecipe recipe, bool craftable, PlayerInventoryData inventory)
    {
        if (rowView == null || recipe == null)
            return;

        bool unlocked = IsCraftingRecipeUnlocked(recipe);

        if (rowView.ResultButton != null)
            rowView.ResultButton.interactable = inventoryControlsInteractable && (preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress);

        if (rowView.ResultButtonImage != null)
        {
            Color rarityColor = InventoryItemCatalog.GetRarityColor(recipe.OutputItemId);
            rowView.ResultButtonImage.color = !unlocked
                ? Color.Lerp(rarityColor, new Color(0.08f, 0.1f, 0.13f, 1f), 0.72f)
                : craftable
                ? rarityColor
                : Color.Lerp(rarityColor, new Color(0.16f, 0.18f, 0.22f, 1f), 0.58f);
        }

        if (rowView.ResultOutline != null)
        {
            rowView.ResultOutline.effectColor = !unlocked
                ? new Color(0.82f, 0.5f, 0.22f, 0.86f)
                : craftable
                ? new Color(0.23f, 0.92f, 0.49f, 0.95f)
                : new Color(0.28f, 0.36f, 0.44f, 0.8f);
            rowView.ResultOutline.effectDistance = craftable || !unlocked ? new Vector2(4f, 4f) : new Vector2(2f, 2f);
        }

        if (rowView.ResultShadow != null)
        {
            rowView.ResultShadow.effectColor = craftable
                ? new Color(0.07f, 0.38f, 0.18f, 0.55f)
                : new Color(0f, 0f, 0f, 0.22f);
            rowView.ResultShadow.effectDistance = new Vector2(0f, -3f);
        }

        if (rowView.ResultPriceText != null)
            rowView.ResultPriceText.gameObject.SetActive(unlocked);
        if (rowView.ResultLockText != null)
            rowView.ResultLockText.gameObject.SetActive(!unlocked);

        UpdateCraftingRecipeResultFrame(rowView, craftable, unlocked);

        bool[] missingIngredients = ResolveMissingRecipeIngredients(inventory, recipe);
        if (rowView.MissingIngredientFrames == null)
            return;

        for (int i = 0; i < rowView.MissingIngredientFrames.Length; i++)
        {
            GameObject frame = rowView.MissingIngredientFrames[i];
            if (frame == null)
                continue;

            bool missing = missingIngredients != null && i < missingIngredients.Length && missingIngredients[i];
            frame.SetActive(missing);
        }
    }

    void UpdateCraftingRecipeResultFrame(CraftingRecipeRowView rowView, bool craftable, bool unlocked)
    {
        if (rowView == null || rowView.ResultFrameRects == null || rowView.ResultFrameImages == null)
            return;

        Color frameColor = !unlocked
            ? new Color(0.9f, 0.54f, 0.22f, 1f)
            : craftable
            ? new Color(0.24f, 0.94f, 0.5f, 1f)
            : new Color(0.18f, 0.24f, 0.3f, 0.92f);
        float thickness = craftable || !unlocked ? 6f : 4f;

        SetCraftingRecipeFrameBar(rowView, 0, frameColor, new Vector2(0f, -thickness), new Vector2(0f, 0f));
        SetCraftingRecipeFrameBar(rowView, 1, frameColor, new Vector2(0f, 0f), new Vector2(0f, thickness));
        SetCraftingRecipeFrameBar(rowView, 2, frameColor, new Vector2(0f, 0f), new Vector2(thickness, 0f));
        SetCraftingRecipeFrameBar(rowView, 3, frameColor, new Vector2(-thickness, 0f), new Vector2(0f, 0f));
    }

    void SetCraftingRecipeFrameBar(CraftingRecipeRowView rowView, int index, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rowView == null || index < 0)
            return;

        if (rowView.ResultFrameRects != null && index < rowView.ResultFrameRects.Length)
        {
            RectTransform rect = rowView.ResultFrameRects[index];
            if (rect != null)
            {
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }
        }

        if (rowView.ResultFrameImages != null && index < rowView.ResultFrameImages.Length)
        {
            Image image = rowView.ResultFrameImages[index];
            if (image != null)
                image.color = color;
        }
    }

    bool[] ResolveMissingRecipeIngredients(PlayerInventoryData inventory, PlayerProfileCraftingRecipe recipe)
    {
        if (inventory == null || recipe == null || recipe.Inputs == null)
            return Array.Empty<bool>();

        bool[] missing = new bool[recipe.Inputs.Length];
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        PlayerInventoryData normalized = inventory.Clone();
        normalized.Normalize();
        CountCombinedInventoryItems(normalized.PlayerSlots, counts);
        CountCombinedInventoryItems(normalized.ShipSlots, counts);
        CountCombinedInventoryItems(normalized.CraftingSlots, counts);

        for (int i = 0; i < recipe.Inputs.Length; i++)
        {
            string itemId = recipe.Inputs[i];
            if (string.IsNullOrWhiteSpace(itemId) ||
                !counts.TryGetValue(itemId, out int available) ||
                available <= 0)
            {
                missing[i] = true;
                continue;
            }

            counts[itemId] = available - 1;
        }

        return missing;
    }

    GameObject CreateRecipeIngredientMissingFrame(Transform parent)
    {
        if (parent == null)
            return null;

        Color frameColor = new Color(1f, 0.18f, 0.18f, 0.88f);
        float thickness = 4f;
        GameObject frameRoot = new GameObject("MissingFrame", typeof(RectTransform));
        frameRoot.transform.SetParent(parent, false);
        RectTransform frameRect = frameRoot.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        void CreateBar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject bar = new GameObject(name, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(frameRoot.transform, false);
            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image image = bar.GetComponent<Image>();
            image.color = frameColor;
            image.raycastTarget = false;
        }

        CreateBar("MissingTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
        CreateBar("MissingBottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
        CreateBar("MissingLeft", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
        CreateBar("MissingRight", new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero);
        return frameRoot;
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

        ShowItemPreview(ProfileItemSource.None, -1, recipe.OutputItemId);

        if (!IsCraftingRecipeUnlocked(recipe))
        {
            SetStatus("Blueprint required for " + InventoryItemCatalog.GetDisplayName(recipe.OutputItemId) + ".");
            return;
        }

        PlayerInventoryData workingInventory = profile.Inventory != null ? profile.Inventory.Clone() : PlayerInventoryData.Default();
        if (!TryPrepareCraftingRecipe(workingInventory, recipe, out string failureMessage))
        {
            SetStatus(string.IsNullOrWhiteSpace(failureMessage) ? "Missing ingredients for this recipe." : failureMessage);
            return;
        }

        inventoryActionInProgress = true;
        preserveInventoryButtonVisualsDuringSave = true;
        SetInteractable(false);

        try
        {
            suppressNextProfileChangedRefresh = true;
            await PlayerProfileService.Instance.SaveInventorySnapshotAsync(workingInventory);
            ShowItemPreview(ProfileItemSource.None, -1, recipe.OutputItemId);
            SetStatus("Crafting slots prepared for " + InventoryItemCatalog.GetDisplayName(recipe.OutputItemId) + ".");
        }
        catch (Exception ex)
        {
            Debug.LogError("Crafting recipe preparation failed: " + ex);
            SetStatus("Could not prepare crafting recipe.");
        }
        finally
        {
            suppressNextProfileChangedRefresh = false;
            inventoryActionInProgress = false;
            SetInteractable(true);
            preserveInventoryButtonVisualsDuringSave = false;
            RefreshProfileSummaryAndInventory();
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

        if (!IsCraftingRecipeUnlocked(recipe))
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

        int shipCapacity = GetActiveShipInventoryCapacity();
        for (int i = 0; i < previousCraftingItems.Count; i++)
        {
            string itemId = previousCraftingItems[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (inventory.TryAddToPlayer(itemId))
                continue;

            if (inventory.TryAddToShip(itemId, shipCapacity, GetActiveProfileShipSkinIndex()))
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

            int shipCapacity = GetActiveShipInventoryCapacity();
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
        GameObject slotObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        slotObject.transform.SetParent(parent, false);
        RectTransform rect = slotObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(120f, 120f);

        Image bg = slotObject.GetComponent<Image>();
        bg.color = GetShipSelectionSlotColor(slotIndex);
        bg.raycastTarget = true;

        Outline outline = slotObject.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = GetShipSelectionSlotOutlineColor(slotIndex);
            outline.effectDistance = new Vector2(2.2f, -2.2f);
            outline.useGraphicAlpha = true;
        }

        Button button = slotObject.GetComponent<Button>();
        ConfigureInventorySlotButtonTransition(button);
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
        iconRect.sizeDelta = new Vector2(EquipmentSlotPreviewIconSize, EquipmentSlotPreviewIconSize);
        icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.enabled = false;

        text = CreateText(slotObject.transform, name + "Text", label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, EquipmentSlotPreviewFontSize, TextAlignmentOptions.Center);
        ApplyEquipmentSlotPreviewTextStyle(text);
        icon.transform.SetAsLastSibling();
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
        scrollbarRect.anchoredPosition = new Vector2(anchoredPosition.x - 100f, anchoredPosition.y);
        scrollbarRect.sizeDelta = new Vector2(88f, viewportSize.y);

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
        ConfigureInventorySlotButtonTransition(button);
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
        ConfigureInventorySlotButtonTransition(button);
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

    void ConfigureInventorySlotButtonTransition(Button button)
    {
        if (button == null)
            return;

        button.transition = Selectable.Transition.None;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.fadeDuration = 0f;
        button.colors = colors;
    }

    void ConfigureNoBlinkInventoryActionButton(Button button)
    {
        if (button == null)
            return;

        button.transition = Selectable.Transition.None;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.fadeDuration = 0f;
        button.colors = colors;
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
            if (!ButtonClickSoundHook.ShouldSuppressClickSound(objectName, label))
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

    void StyleCompactInventoryUtilityButton(Button button)
    {
        if (button == null)
            return;

        StyleButton(button, new Color(0.14f, 0.19f, 0.28f, 0.98f), new Color(0.22f, 0.3f, 0.42f, 1f));

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.14f, 0.19f, 0.28f, 0.98f);
            image.raycastTarget = true;
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.56f, 0.68f, 0.82f, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        Shadow shadow = null;
        Shadow[] shadows = button.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null && shadows[i] is not Outline)
            {
                shadow = shadows[i];
                break;
            }
        }

        if (shadow == null)
            shadow = button.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        shadow.effectDistance = new Vector2(3f, -3f);
        shadow.useGraphicAlpha = true;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.fontSize = 17f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(0.92f, 0.98f, 1f, 1f);
            text.margin = new Vector4(8f, 4f, 8f, 4f);
        }
    }

    void StyleCompactBackLikeButton(Button button)
    {
        if (button == null)
            return;

        Color normal = new Color(0.14f, 0.19f, 0.28f, 0.98f);
        Color highlighted = new Color(0.22f, 0.3f, 0.42f, 1f);
        Color pressed = new Color(0.1f, 0.14f, 0.2f, 1f);

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = normal;
            image.raycastTarget = true;
        }

        button.transition = Selectable.Transition.None;
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.selectedColor = highlighted;
        colors.pressedColor = pressed;
        colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.45f);
        colors.fadeDuration = 0f;
        button.colors = colors;

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.34f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        Shadow shadow = null;
        Shadow[] shadows = button.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null && shadows[i] is not Outline)
            {
                shadow = shadows[i];
                break;
            }
        }

        if (shadow == null)
            shadow = button.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.38f);
        shadow.effectDistance = new Vector2(3f, -3f);
        shadow.useGraphicAlpha = true;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.fontSize = 16f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(0.94f, 0.97f, 1f, 1f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center;
            text.characterSpacing = 1.6f;
            text.margin = new Vector4(8f, 4f, 8f, 4f);
        }
    }

    void StyleReadableBackLikeButton(Button button, float fontSize)
    {
        StyleCompactBackLikeButton(button);

        TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (text == null)
            return;

        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(16f, fontSize - 3f);
        text.fontSizeMax = fontSize;
        text.characterSpacing = 1.2f;
        text.margin = new Vector4(10f, 5f, 10f, 5f);
        text.overflowMode = TextOverflowModes.Truncate;
    }

    void StylePlayerInventoryUtilityButton(Button button)
    {
        StyleCompactBackLikeButton(button);

        TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (text != null)
            text.fontSize = PlayerInventoryUtilityButtonFontSize;
    }

    void ConfigureShipInventoryLabelText()
    {
        if (shipInventoryLabelText == null)
            return;

        shipInventoryLabelText.fontSize = ShipInventoryHeaderFontSize;
        shipInventoryLabelText.enableAutoSizing = true;
        shipInventoryLabelText.fontSizeMin = 18f;
        shipInventoryLabelText.fontSizeMax = ShipInventoryHeaderFontSize;
        shipInventoryLabelText.fontStyle = FontStyles.Bold;
        shipInventoryLabelText.alignment = TextAlignmentOptions.MidlineLeft;
        shipInventoryLabelText.textWrappingMode = TextWrappingModes.NoWrap;
        shipInventoryLabelText.overflowMode = TextOverflowModes.Truncate;
        shipInventoryLabelText.characterSpacing = 0.6f;
        shipInventoryLabelText.margin = new Vector4(0f, 2f, 4f, 2f);
    }

    void ConfigureBlueprintsTabButtonText()
    {
        TMP_Text text = craftingRecipeBlueprintsButton != null
            ? craftingRecipeBlueprintsButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        if (text == null)
            return;

        text.text = "BLUEPRINTS";
        text.fontSize = BlueprintTabFontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 20f;
        text.fontSizeMax = BlueprintTabFontSize;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.characterSpacing = 1.2f;
        text.margin = new Vector4(12f, 5f, 12f, 5f);
    }

    void ConfigurePlayerInventoryLabelText()
    {
        if (playerInventoryLabelText == null)
            return;

        playerInventoryLabelText.text = PlayerInventoryTitleText;
        playerInventoryLabelText.fontSize = 18f;
        playerInventoryLabelText.enableAutoSizing = true;
        playerInventoryLabelText.fontSizeMin = 12f;
        playerInventoryLabelText.fontSizeMax = 18f;
        playerInventoryLabelText.fontStyle = FontStyles.Bold;
        playerInventoryLabelText.alignment = TextAlignmentOptions.MidlineLeft;
        playerInventoryLabelText.textWrappingMode = TextWrappingModes.NoWrap;
        playerInventoryLabelText.overflowMode = TextOverflowModes.Truncate;
        playerInventoryLabelText.margin = new Vector4(0f, 2f, 4f, 2f);
    }

    void ConfigurePlayerInventoryCountText()
    {
        if (playerInventoryCountText == null)
            return;

        playerInventoryCountText.fontSize = 20f;
        playerInventoryCountText.enableAutoSizing = true;
        playerInventoryCountText.fontSizeMin = 14f;
        playerInventoryCountText.fontSizeMax = 20f;
        playerInventoryCountText.fontStyle = FontStyles.Bold;
        playerInventoryCountText.alignment = TextAlignmentOptions.MidlineRight;
        playerInventoryCountText.textWrappingMode = TextWrappingModes.NoWrap;
        playerInventoryCountText.overflowMode = TextOverflowModes.Truncate;
        playerInventoryCountText.color = new Color(0.94f, 0.84f, 0.44f, 1f);
        playerInventoryCountText.margin = new Vector4(2f, 2f, 0f, 2f);
    }

    void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = label ?? string.Empty;
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

        int shipInventoryItemCount = CountShipInventoryItems();
        if (shipInventoryItemCount > 0)
        {
            ShowShipInventoryStartConfirm(shipInventoryItemCount);
            return;
        }

        ContinueSaveAndRun();
    }

    void ContinueSaveAndRun()
    {
        if (nicknameInput == null)
            return;

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

    int CountShipInventoryItems()
    {
        if (!PlayerProfileService.HasInstance || PlayerProfileService.Instance.CurrentProfile == null)
            return 0;

        PlayerInventoryData inventory = PlayerProfileService.Instance.CurrentProfile.Inventory;
        if (inventory == null || inventory.ShipSlots == null)
            return 0;

        inventory.Normalize();
        int count = 0;
        for (int i = 0; i < inventory.ShipSlots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(inventory.ShipSlots[i]))
                count++;
        }

        return count;
    }

    void ShowShipInventoryStartConfirm(int itemCount)
    {
        if (shipInventoryStartConfirmText != null)
        {
            string itemLabel = itemCount == 1 ? "item" : "items";
            shipInventoryStartConfirmText.text = "Your ship inventory contains " + itemCount + " " + itemLabel + ".\nAre you sure you want to continue?";
        }

        if (shipInventoryStartConfirmObject != null)
        {
            shipInventoryStartConfirmObject.SetActive(true);
            EnsureProfileModalLayering();
        }
    }

    void HideShipInventoryStartConfirm()
    {
        if (shipInventoryStartConfirmObject != null)
            shipInventoryStartConfirmObject.SetActive(false);
    }

    void OnShipInventoryStartConfirmYesClicked()
    {
        HideShipInventoryStartConfirm();
        ContinueSaveAndRun();
    }

    void OnShipInventoryStartConfirmNoClicked()
    {
        HideShipInventoryStartConfirm();
        SetStatus("Review ship inventory before launch.");
        SwitchToScreen(ProfileScreen.Inventory, false);
        RefreshView();
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
        selectedPilotId = PilotCatalog.NormalizePilotId(profile.SelectedPilotId);
        if (!PilotCatalog.IsPilotUnlocked(profile, selectedPilotId))
            selectedPilotId = PilotCatalog.JakeId;

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

        string accountLine = string.Empty;
        if (accountText != null || sharedTopBar != null)
        {
            string playerId = PlayerProfileService.Instance.PlayerId;
            if (string.IsNullOrWhiteSpace(playerId))
            {
                accountLine = PlayerProfileService.Instance.IsInitialized ? "Cloud linked" : "Connecting...";
            }
            else
            {
                string suffix = playerId.Length <= 8 ? playerId : playerId.Substring(playerId.Length - 8);
                accountLine = "ID: " + suffix.ToUpperInvariant();
            }

            if (accountText != null)
                accountText.text = accountLine;
        }

        if (sharedTopBar != null)
            sharedTopBar.SetProfile(profile, profile.Nickname, accountLine, nicknameInput == null || !nicknameInput.isFocused);

        UpdateShipTypeButtonVisuals();
        UpdateSkinButtonsForSelectedShip();
        UpdateSkinButtonVisuals();
        ApplySaveAndRunButtonStyle();
        RefreshShipPreview();
        RefreshPilotPortrait();
        RefreshInventoryView(profile.Inventory);
        if (currentScreen == ProfileScreen.Projects)
            RefreshProjectsView();
        else
            projectsDirty = true;
        if (currentScreen == ProfileScreen.ProjectDetails)
            RefreshProjectDetailsView();
        else
            projectDetailsDirty = true;
        if (craftingRecipeBrowserObject != null && craftingRecipeBrowserObject.activeSelf)
            RefreshCraftingRecipeBrowser();
        if (craftingBlueprintBrowserObject != null && craftingBlueprintBrowserObject.activeSelf)
            RefreshCraftingBlueprintBrowser();
        if (shopBrowserObject != null && shopBrowserObject.activeSelf)
            RefreshShopBrowser(false);
        ApplyProfileScreenLayoutAfterRefresh();
    }

    void RefreshProfileSummaryAndInventory()
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null)
            return;

        if (profile.Inventory != null)
            profile.Inventory.Normalize();

        selectedSkin = Mathf.Clamp(profile.ShipSkinIndex, 0, ShipCatalog.MaxShipSkinIndex);
        selectedPilotId = PilotCatalog.NormalizePilotId(profile.SelectedPilotId);
        if (!PilotCatalog.IsPilotUnlocked(profile, selectedPilotId))
            selectedPilotId = PilotCatalog.JakeId;

        if (gamesPlayedText != null)
            gamesPlayedText.text = "Games: " + profile.GamesPlayed;

        if (totalXpText != null)
            totalXpText.text = "Level: " + RoundXpBalance.GetLevelForTotalXp(profile.TotalXp) + "  XP: " + profile.TotalXp;

        if (astronsText != null)
            astronsText.text = "Astrons: " + profile.Astrons;

        string accountLine = accountText != null ? accountText.text : string.Empty;
        if (sharedTopBar != null)
            sharedTopBar.SetProfile(profile, profile.Nickname, accountLine, nicknameInput == null || !nicknameInput.isFocused);

        RefreshInventoryView(profile.Inventory);
        RefreshEquipmentSlotPreview();
        if (craftingRecipeBrowserObject != null && craftingRecipeBrowserObject.activeSelf)
            RefreshCraftingRecipeBrowser();
        if (craftingBlueprintBrowserObject != null && craftingBlueprintBrowserObject.activeSelf)
            RefreshCraftingBlueprintBrowser();
        if (shopBrowserObject != null && shopBrowserObject.activeSelf)
            RefreshShopBrowser(false);

        ApplyProfileScreenLayoutAfterRefresh();
    }

    void ApplyProfileScreenLayoutAfterRefresh()
    {
        ApplyProfileScreenLayout();
        profileLayoutDirty = false;
    }

    ShipType GetSelectedShipType()
    {
        return ShipCatalog.GetShipTypeFromSkinIndex(selectedSkin);
    }

    bool IsShipTypeUnlockedForUi(ShipType shipType)
    {
        return PlayerProfileService.HasInstance && PlayerProfileService.Instance.IsShipUnlocked(shipType);
    }

    int GetActiveProfileShipSkinIndex()
    {
        PlayerProfileData profile = PlayerProfileService.HasInstance ? PlayerProfileService.Instance.CurrentProfile : null;
        int skinIndex = profile != null ? profile.ShipSkinIndex : selectedSkin;
        return Mathf.Clamp(skinIndex, 0, ShipCatalog.MaxShipSkinIndex);
    }

    int GetActiveShipInventoryCapacity()
    {
        PlayerProfileData profile = PlayerProfileService.HasInstance ? PlayerProfileService.Instance.CurrentProfile : null;
        string[] equipmentSlots = profile != null && profile.Inventory != null ? profile.Inventory.EquipmentSlots : null;
        if (PlayerProfileService.HasInstance)
            return PlayerProfileService.Instance.GetShipInventoryCapacityForProfile(GetActiveProfileShipSkinIndex(), equipmentSlots);

        return PlayerProfileService.GetEffectiveShipInventoryCapacity(GetActiveProfileShipSkinIndex(), equipmentSlots);
    }

    bool IsEquipmentSlotEnabledForSelectedSkin(int slotIndex)
    {
        if (!PlayerProfileService.HasInstance)
            return ShipCatalog.IsEquipmentSlotEnabled(slotIndex, selectedSkin);

        return PlayerProfileService.Instance.IsEquipmentSlotEnabledForProfile(slotIndex, selectedSkin);
    }

    bool IsActiveShipSafePocketIndex(int slotIndex)
    {
        return PlayerProfileService.IsSafePocketIndex(GetActiveProfileShipSkinIndex(), slotIndex);
    }

    bool IsActiveShipAstronautCargoIndex(int slotIndex)
    {
        return PlayerProfileService.IsAstronautCargoIndex(GetActiveProfileShipSkinIndex(), GetActiveShipInventoryCapacity(), slotIndex);
    }

    async void SetSelectedShipType(ShipType shipType)
    {
        if (!IsShipTypeUnlockedForUi(shipType))
        {
            SetStatus(ShipCatalog.GetShipTypeDisplayName(shipType).ToUpperInvariant() + " locked.");
            RefreshView();
            return;
        }

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
        if (!IsShipTypeUnlockedForUi(GetSelectedShipType()))
            return;

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
                bool locked = i < SelectableShipTypes.Length && !IsShipTypeUnlockedForUi(SelectableShipTypes[i]);
                image.color = locked
                    ? new Color(0.13f, 0.14f, 0.16f, 0.86f)
                    : isSelected
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
        bool shipUnlocked = IsShipTypeUnlockedForUi(shipType);

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
            skinButtons[i].interactable = active && shipUnlocked && inventoryControlsInteractable && !inventoryActionInProgress;
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
        bool shipUnlocked = IsShipTypeUnlockedForUi(GetSelectedShipType());

        for (int i = 0; i < skinButtons.Length; i++)
        {
            if (skinButtons[i] == null)
                continue;

            if (i >= allowedSkins.Length)
                continue;

            Image image = skinButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = !shipUnlocked
                    ? new Color(0.12f, 0.13f, 0.15f, 0.82f)
                    : allowedSkins[i] == selectedSkin
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

        ApplyEquipmentSlotPreviewSizing();
    }

    void LayoutEquipmentSlotsColumn(float leftX, float rightX, float topY, float rowSpacing, float slotSize)
    {
        if (equipmentSlotRects == null || equipmentSlotRects.Length < PlayerInventoryData.EquipmentSlotCount)
            return;

        int[][] rowOrder =
        {
            new[] { 0, 1 },
            new[] { 2, 3 },
            new[] { 4, 5 },
            new[] { 8, 9 },
            new[] { 10, 11 },
            new[] { 6, 7 }
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

        ApplyEquipmentSlotPreviewSizing();
    }

    void ApplyEquipmentSlotPreviewSizing()
    {
        if (equipmentSlotPreviewIcons != null)
        {
            for (int i = 0; i < equipmentSlotPreviewIcons.Length; i++)
            {
                Image icon = equipmentSlotPreviewIcons[i];
                if (icon == null)
                    continue;

                RectTransform iconRect = icon.rectTransform;
                if (iconRect != null)
                    iconRect.sizeDelta = new Vector2(EquipmentSlotPreviewIconSize, EquipmentSlotPreviewIconSize);
            }
        }

        if (equipmentSlotPreviewTexts != null)
        {
            for (int i = 0; i < equipmentSlotPreviewTexts.Length; i++)
                ApplyEquipmentSlotPreviewTextStyle(equipmentSlotPreviewTexts[i]);
        }

        KeepEquipmentSlotItemsAboveLabels();
    }

    void KeepEquipmentSlotItemsAboveLabels()
    {
        if (equipmentSlotPreviewIcons == null)
            return;

        for (int i = 0; i < equipmentSlotPreviewIcons.Length; i++)
        {
            if (equipmentSlotPreviewIcons[i] != null)
                equipmentSlotPreviewIcons[i].transform.SetAsLastSibling();
        }
    }

    void ApplyEquipmentSlotPreviewTextStyle(TMP_Text text)
    {
        if (text == null)
            return;

        text.fontSize = EquipmentSlotPreviewFontSize;
        text.fontSizeMin = 9f;
        text.fontSizeMax = EquipmentSlotPreviewFontSize;
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.92f, 0.96f, 0.98f, 0.98f);
        text.margin = new Vector4(6f, 4f, 6f, 4f);
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

    void RefreshPilotPortrait()
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        selectedPilotId = PilotCatalog.NormalizePilotId(profile != null ? profile.SelectedPilotId : selectedPilotId);
        if (profile != null && !PilotCatalog.IsPilotUnlocked(profile, selectedPilotId))
            selectedPilotId = PilotCatalog.JakeId;

        PilotDefinition definition = PilotCatalog.GetDefinition(selectedPilotId);
        if (pilotPortraitImage != null)
        {
            pilotPortraitImage.sprite = LoadPilotPortraitSprite(definition);
            pilotPortraitImage.color = pilotPortraitImage.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        if (pilotPortraitNameText != null)
            pilotPortraitNameText.text = definition.DisplayName;

        if (pilotPortraitButton != null)
            pilotPortraitButton.interactable = !inventoryActionInProgress;
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
        SetShipStatCard(8, ShipStatLabels[8], definition.BrakingDriftLevel.ToString(), NormalizeShipStat(definition.BrakingDriftLevel, stat => stat.BrakingDriftLevel));
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
        rect.sizeDelta = new Vector2(340f, 330f);

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
        rect.sizeDelta = new Vector2(370f, 250f);
        projectsButtonRootObject.transform.SetAsLastSibling();
    }

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
            SetAnchoredRect(projectRewardsPanelObject.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-292f, -258f), new Vector2(430f, 196f));
        if (projectRewardsText != null)
            SetAnchoredRect(projectRewardsText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-292f, -278f), new Vector2(382f, 108f));
        if (projectRewardClaimButton != null)
            SetAnchoredRect(projectRewardClaimButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-292f, -398f), new Vector2(180f, 52f));

        float tabWidth = 174f;
        float tabSpacing = 14f;
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
                SetAnchoredRect(tab.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(tabStartX + i * (tabWidth + tabSpacing), -168f), new Vector2(tabWidth, 54f));
        }

        for (int i = 0; i < projectStepButtons.Count; i++)
        {
            Button step = projectStepButtons[i];
            if (step == null)
                continue;

            int column = i % 3;
            int row = i / 3;
            SetAnchoredRect(step.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-520f + column * 520f, 176f - row * 144f), new Vector2(470f, 126f));
            if (i < projectStepIcons.Count && projectStepIcons[i] != null)
                SetAnchoredRect(projectStepIcons[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(58f, 0f), new Vector2(86f, 86f));
        }

        if (projectCommitPanelObject != null)
            SetAnchoredRect(projectCommitPanelObject.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-292f, -472f), new Vector2(370f, 236f));
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

        LayoutEquipmentSlotsColumn(-520f, -386f, -28f, 116f, 104f);
        LayoutShipStatsVertical(486f, -52f, new Vector2(278f, 72f), 12f);
        ApplyEquipmentSlotPreviewSizing();
    }

    void LayoutStoragePanel(float centerX, float playerScrollWidth = 830f)
    {
        ConfigureStorageBackdrop(false, 0f, 0f, 0f, 0f);

        const float slotSize = 96f;
        const float slotSpacing = 8f;
        float shipWidth = (slotSize * 5f) + (slotSpacing * 4f);
        float shipLeftEdge = centerX - (shipWidth * 0.5f);
        LayoutShipInventoryHeader(shipLeftEdge, shipWidth, -180f + InventoryUtilityButtonLift);

        if (shipInventoryButtons != null)
        {
            float firstSlotX = centerX - (((slotSize * 5f) + (slotSpacing * 4f)) * 0.5f) + (slotSize * 0.5f);
            for (int i = 0; i < shipInventoryButtons.Length; i++)
            {
                if (shipInventoryButtons[i] == null)
                    continue;

                int row = i / 5;
                int col = i % 5;
                Vector2 position = new Vector2(firstSlotX + col * (slotSize + slotSpacing), -212f - row * (slotSize + slotSpacing));
                SetAnchoredRect(shipInventoryButtons[i].GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(slotSize, slotSize));
            }
        }

        float playerInventoryFilterButtonX = centerX - 320f;
        float playerInventoryExtendButtonX = centerX + 322f + InventoryExtendButtonOffsetX;
        float playerInventorySortButtonX = playerInventoryExtendButtonX - ((PlayerInventoryExtendButtonSize.x + PlayerInventorySortButtonSize.x) * 0.5f) - InventoryUtilityButtonGap;
        LayoutPlayerInventoryLabel(playerInventoryFilterButtonX, playerInventorySortButtonX);
        if (playerInventoryFilterButton != null)
            SetAnchoredRect(playerInventoryFilterButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerInventoryFilterButtonX, -544f + InventoryUtilityButtonLift), PlayerInventoryFilterButtonSize);
        if (playerInventorySortButton != null)
            SetAnchoredRect(playerInventorySortButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerInventorySortButtonX, -544f + InventoryUtilityButtonLift), PlayerInventorySortButtonSize);
        if (playerInventoryExtendButton != null)
            SetAnchoredRect(playerInventoryExtendButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerInventoryExtendButtonX, -544f + InventoryUtilityButtonLift), PlayerInventoryExtendButtonSize);

        if (playerInventoryScrollRect != null)
            SetAnchoredRect(playerInventoryScrollRect.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(centerX - 10f, -578f), new Vector2(playerScrollWidth, 362f));
        if (playerInventoryScrollbarObject != null)
            SetAnchoredRect(playerInventoryScrollbarObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(centerX + (playerScrollWidth * 0.5f) + 28f, -578f), new Vector2(56f, 362f));
    }

    void LayoutPlayerInventoryLabel(float filterButtonCenterX, float sortButtonCenterX)
    {
        if (playerInventoryLabelText == null && playerInventoryCountText == null)
            return;

        float leftEdge = filterButtonCenterX + PlayerInventoryFilterButtonSize.x * 0.5f + InventoryUtilityLabelGap;
        float rightEdge = sortButtonCenterX - PlayerInventorySortButtonSize.x * 0.5f - InventoryUtilityLabelGap;
        float totalWidth = Mathf.Max(96f, rightEdge - leftEdge);
        float countWidth = Mathf.Min(102f, Mathf.Max(84f, totalWidth * 0.42f));
        float labelWidth = Mathf.Max(44f, totalWidth - countWidth - 6f);
        float y = -544f + InventoryUtilityButtonLift;

        if (playerInventoryLabelText != null)
        {
            SetAnchoredRect(
                playerInventoryLabelText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(leftEdge + labelWidth * 0.5f, y),
                new Vector2(labelWidth, PlayerInventoryFilterButtonSize.y));
            ConfigurePlayerInventoryLabelText();
        }

        if (playerInventoryCountText != null)
        {
            SetAnchoredRect(
                playerInventoryCountText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(rightEdge - countWidth * 0.5f, y),
                new Vector2(countWidth, PlayerInventoryFilterButtonSize.y));
            ConfigurePlayerInventoryCountText();
        }
    }

    void LayoutShipInventoryHeader(float shipLeftEdge, float shipWidth, float y)
    {
        float safeShipWidth = Mathf.Max(ShipInventoryHeaderButtonSize.x + 220f, shipWidth);
        float labelWidth = Mathf.Max(260f, safeShipWidth - ShipInventoryHeaderButtonSize.x - ShipInventoryHeaderGap);
        float labelCenterX = shipLeftEdge + (labelWidth * 0.5f);
        float buttonCenterX = shipLeftEdge + safeShipWidth - (ShipInventoryHeaderButtonSize.x * 0.5f);

        if (shipInventoryLabelText != null)
        {
            SetAnchoredRect(
                shipInventoryLabelText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(labelCenterX, y),
                new Vector2(labelWidth, ShipInventoryHeaderButtonSize.y));
            ConfigureShipInventoryLabelText();
        }

        if (shipInventoryUnloadButton != null)
        {
            SetAnchoredRect(
                shipInventoryUnloadButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(buttonCenterX, y),
                ShipInventoryHeaderButtonSize);
            StyleReadableBackLikeButton(shipInventoryUnloadButton, ShipInventoryHeaderButtonFontSize);
        }
    }

    void LayoutCraftingStoragePanel()
    {
        const float leftEdge = -1018f;
        const float playerScrollWidth = 770f;
        const float shipSlotSize = 96f;
        const float shipSlotSpacing = 8f;
        float shipWidth = (shipSlotSize * 5f) + (shipSlotSpacing * 4f);
        float playerCenterX = leftEdge + (playerScrollWidth * 0.5f);
        float playerRightEdge = leftEdge + playerScrollWidth;

        ConfigureStorageBackdrop(false, 0f, 0f, 0f, 0f);

        LayoutShipInventoryHeader(leftEdge, shipWidth, -180f + InventoryUtilityButtonLift);

        if (shipInventoryButtons != null)
        {
            for (int i = 0; i < shipInventoryButtons.Length; i++)
            {
                if (shipInventoryButtons[i] == null)
                    continue;

                int row = i / 5;
                int col = i % 5;
                Vector2 position = new Vector2(leftEdge + (shipSlotSize * 0.5f) + col * (shipSlotSize + shipSlotSpacing), -212f - row * (shipSlotSize + shipSlotSpacing));
                SetAnchoredRect(shipInventoryButtons[i].GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(shipSlotSize, shipSlotSize));
            }
        }

        float playerInventoryFilterButtonX = leftEdge + 83f;
        float playerInventoryExtendButtonX = playerRightEdge - 66f + InventoryExtendButtonOffsetX;
        float playerInventorySortButtonX = playerInventoryExtendButtonX - ((PlayerInventoryExtendButtonSize.x + PlayerInventorySortButtonSize.x) * 0.5f) - InventoryUtilityButtonGap;
        LayoutPlayerInventoryLabel(playerInventoryFilterButtonX, playerInventorySortButtonX);

        if (playerInventoryFilterButton != null)
            SetAnchoredRect(playerInventoryFilterButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerInventoryFilterButtonX, -544f + InventoryUtilityButtonLift), PlayerInventoryFilterButtonSize);
        if (playerInventorySortButton != null)
            SetAnchoredRect(playerInventorySortButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerInventorySortButtonX, -544f + InventoryUtilityButtonLift), PlayerInventorySortButtonSize);

        if (playerInventoryExtendButton != null)
            SetAnchoredRect(playerInventoryExtendButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerInventoryExtendButtonX, -544f + InventoryUtilityButtonLift), PlayerInventoryExtendButtonSize);

        if (playerInventoryScrollRect != null)
            SetAnchoredRect(playerInventoryScrollRect.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerCenterX, -578f), new Vector2(playerScrollWidth, 362f));

        if (playerInventoryScrollbarObject != null)
            SetAnchoredRect(playerInventoryScrollbarObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(playerRightEdge + 28f, -578f), new Vector2(56f, 362f));
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
            title.gameObject.SetActive(false);

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
            scrollbarRect.anchoredPosition = new Vector2(386f, -6f);
            scrollbarRect.sizeDelta = new Vector2(56f, 610f);
        }

        if (craftingRecipeAvailabilityButton != null)
        {
            RectTransform rect = craftingRecipeAvailabilityButton.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(-286f, 42f);
                rect.sizeDelta = new Vector2(255f, 63f);
            }
        }

        if (craftingRecipeBlueprintsButton != null)
        {
            RectTransform rect = craftingRecipeBlueprintsButton.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(0f, 42f);
                rect.sizeDelta = new Vector2(285f, 68f);
            }

            ConfigureBlueprintsTabButtonText();
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
            rect.sizeDelta = new Vector2(420f, 736f);
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
            panelRect.anchoredPosition = new Vector2(192f, -156f);
            panelRect.sizeDelta = new Vector2(628f, 736f);
        }

        if (shopCloseButton != null)
            shopCloseButton.gameObject.SetActive(false);

        if (shopAstronsText != null)
            shopAstronsText.gameObject.SetActive(false);

        TMP_Text title = shopBrowserObject.transform.Find("ShopBrowserPanel/ShopBrowserTitle")?.GetComponent<TMP_Text>();
        if (title != null)
        {
            title.text = TraderOpensShop(selectedTraderShop) ? GetTraderDisplayName(selectedTraderShop) : "TRADER";
            RectTransform titleRect = title.rectTransform;
            titleRect.anchoredPosition = new Vector2(-92f, -32f);
            titleRect.sizeDelta = new Vector2(300f, 34f);
        }

        if (shopSortButton != null)
        {
            RectTransform sortRect = shopSortButton.GetComponent<RectTransform>();
            sortRect.anchorMin = new Vector2(0.5f, 1f);
            sortRect.anchorMax = new Vector2(0.5f, 1f);
            sortRect.pivot = new Vector2(0.5f, 1f);
            sortRect.anchoredPosition = new Vector2(186f, -28f);
            sortRect.sizeDelta = new Vector2(236f, 48f);
            shopSortButton.transform.SetAsLastSibling();
        }
        RefreshShopSortButton();

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

    void UpdateSelectionCarouselAnimations()
    {
        if (currentScreen == ProfileScreen.ShipSelection)
            UpdateShipSelectionSnap();
        else if (shipSelectionSnapActive || shipSelectionDragActive || Mathf.Abs(shipSelectionVisualOffset) > 0.001f)
            ResetShipSelectionCarouselMotion();

        if (currentScreen == ProfileScreen.PilotSelection)
            UpdatePilotSelectionSnap();
        else if (pilotSelectionSnapActive || pilotSelectionDragActive || Mathf.Abs(pilotSelectionVisualOffset) > 0.001f)
            ResetPilotSelectionCarouselMotion();
    }

    void ResetShipSelectionCarouselMotion()
    {
        shipSelectionDragActive = false;
        shipSelectionDragHorizontal = false;
        shipSelectionSnapActive = false;
        shipSelectionSnapElapsed = 0f;
        shipSelectionSnapIndexDelta = 0;
        shipSelectionVisualOffset = 0f;
    }

    void ResetPilotSelectionCarouselMotion()
    {
        pilotSelectionDragActive = false;
        pilotSelectionDragHorizontal = false;
        pilotSelectionSnapActive = false;
        pilotSelectionSnapElapsed = 0f;
        pilotSelectionSnapIndexDelta = 0;
        pilotSelectionVisualOffset = 0f;
    }

    public void BeginShipSelectionDrag(PointerEventData eventData, Vector2 startScreenPosition)
    {
        if (inventoryActionInProgress || currentScreen != ProfileScreen.ShipSelection || shipSelectionCardObjects == null)
            return;

        if (!TryGetSelectionLocalPoint(shipSelectionViewObject, eventData, startScreenPosition, out shipSelectionDragStartLocal))
            return;

        shipSelectionDragActive = true;
        shipSelectionDragHorizontal = false;
        shipSelectionSnapActive = false;
        shipSelectionDragStartOffset = shipSelectionVisualOffset;
    }

    public void UpdateShipSelectionDrag(PointerEventData eventData)
    {
        if (!shipSelectionDragActive || inventoryActionInProgress || currentScreen != ProfileScreen.ShipSelection)
            return;

        if (!TryGetSelectionLocalPoint(shipSelectionViewObject, eventData, eventData.position, out Vector2 localPoint))
            return;

        Vector2 delta = localPoint - shipSelectionDragStartLocal;
        if (!shipSelectionDragHorizontal)
        {
            if (delta.magnitude < SelectionCarouselAxisThreshold)
                return;

            if (Mathf.Abs(delta.x) <= Mathf.Abs(delta.y))
                return;

            shipSelectionDragHorizontal = true;
        }

        shipSelectionVisualOffset = shipSelectionDragStartOffset + delta.x / GetShipSelectionSwipeDistance();
        if (NormalizeContinuousSelectionDrag(ref shipSelectionCenterIndex, ref shipSelectionVisualOffset, ref shipSelectionDragStartOffset, SelectableShipTypes.Length))
        {
            shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];
            RefreshShipSelectionView();
            return;
        }

        ApplyShipSelectionCarouselVisuals();
    }

    public void EndShipSelectionDrag(PointerEventData eventData)
    {
        if (!shipSelectionDragActive)
            return;

        shipSelectionDragActive = false;
        Vector2 delta = Vector2.zero;
        if (TryGetSelectionLocalPoint(shipSelectionViewObject, eventData, eventData.position, out Vector2 localPoint))
            delta = localPoint - shipSelectionDragStartLocal;

        if (shipSelectionDragHorizontal && Mathf.Abs(delta.x) >= SelectionCarouselClickCancelThreshold)
            shipSelectionSuppressClickUntilFrame = Time.frameCount + SelectionCarouselClickSuppressFrames;

        int indexDelta = shipSelectionDragHorizontal
            ? ResolveSelectionCarouselSnapIndexDelta(shipSelectionVisualOffset, shipSelectionCenterIndex, SelectableShipTypes.Length)
            : 0;

        StartShipSelectionSnap(indexDelta);
    }

    public void BeginPilotSelectionDrag(PointerEventData eventData, Vector2 startScreenPosition)
    {
        if (inventoryActionInProgress || currentScreen != ProfileScreen.PilotSelection || pilotSelectionCardObjects == null)
            return;

        if (!TryGetSelectionLocalPoint(pilotSelectionViewObject, eventData, startScreenPosition, out pilotSelectionDragStartLocal))
            return;

        pilotSelectionDragActive = true;
        pilotSelectionDragHorizontal = false;
        pilotSelectionSnapActive = false;
        pilotSelectionDragStartOffset = pilotSelectionVisualOffset;
    }

    public void UpdatePilotSelectionDrag(PointerEventData eventData)
    {
        if (!pilotSelectionDragActive || inventoryActionInProgress || currentScreen != ProfileScreen.PilotSelection)
            return;

        if (!TryGetSelectionLocalPoint(pilotSelectionViewObject, eventData, eventData.position, out Vector2 localPoint))
            return;

        Vector2 delta = localPoint - pilotSelectionDragStartLocal;
        if (!pilotSelectionDragHorizontal)
        {
            if (delta.magnitude < SelectionCarouselAxisThreshold)
                return;

            if (Mathf.Abs(delta.x) <= Mathf.Abs(delta.y))
                return;

            pilotSelectionDragHorizontal = true;
        }

        pilotSelectionVisualOffset = pilotSelectionDragStartOffset + delta.x / GetPilotSelectionSwipeDistance();
        if (NormalizeContinuousSelectionDrag(ref pilotSelectionCenterIndex, ref pilotSelectionVisualOffset, ref pilotSelectionDragStartOffset, PilotCatalog.AllDefinitions.Count))
        {
            RefreshPilotSelectionView();
            return;
        }

        ApplyPilotSelectionCarouselVisuals();
    }

    public void EndPilotSelectionDrag(PointerEventData eventData)
    {
        if (!pilotSelectionDragActive)
            return;

        pilotSelectionDragActive = false;
        Vector2 delta = Vector2.zero;
        if (TryGetSelectionLocalPoint(pilotSelectionViewObject, eventData, eventData.position, out Vector2 localPoint))
            delta = localPoint - pilotSelectionDragStartLocal;

        if (pilotSelectionDragHorizontal && Mathf.Abs(delta.x) >= SelectionCarouselClickCancelThreshold)
            pilotSelectionSuppressClickUntilFrame = Time.frameCount + SelectionCarouselClickSuppressFrames;

        int indexDelta = pilotSelectionDragHorizontal
            ? ResolveSelectionCarouselSnapIndexDelta(pilotSelectionVisualOffset, pilotSelectionCenterIndex, PilotCatalog.AllDefinitions.Count)
            : 0;

        StartPilotSelectionSnap(indexDelta);
    }

    bool TryGetSelectionLocalPoint(GameObject viewObject, PointerEventData eventData, Vector2 screenPosition, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        RectTransform rect = viewObject != null ? viewObject.GetComponent<RectTransform>() : null;
        if (rect == null || eventData == null)
            return false;

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, eventCamera, out localPoint);
    }

    float GetShipSelectionSwipeDistance()
    {
        return Mathf.Max(1f, ShipSelectionRightPosition.x - ShipSelectionCenterPosition.x);
    }

    float GetPilotSelectionSwipeDistance()
    {
        return Mathf.Max(1f, PilotSelectionRightPosition.x - PilotSelectionCenterPosition.x);
    }

    float ApplySelectionCarouselDragBounds(float offset, int centerIndex, int itemCount)
    {
        if (offset > 0f && centerIndex <= 0)
            offset *= SelectionCarouselEdgeResistance;
        else if (offset < 0f && centerIndex >= itemCount - 1)
            offset *= SelectionCarouselEdgeResistance;

        return Mathf.Clamp(offset, -SelectionCarouselMaxOffset, SelectionCarouselMaxOffset);
    }

    bool NormalizeContinuousSelectionDrag(ref int centerIndex, ref float offset, ref float dragStartOffset, int itemCount)
    {
        bool changed = false;
        while (offset <= -1f && centerIndex < itemCount - 1)
        {
            centerIndex++;
            offset += 1f;
            dragStartOffset += 1f;
            changed = true;
        }

        while (offset >= 1f && centerIndex > 0)
        {
            centerIndex--;
            offset -= 1f;
            dragStartOffset -= 1f;
            changed = true;
        }

        offset = ApplySelectionCarouselDragBounds(offset, centerIndex, itemCount);
        return changed;
    }

    int ResolveSelectionCarouselSnapIndexDelta(float offset, int centerIndex, int itemCount)
    {
        if (Mathf.Abs(offset) < SelectionCarouselSnapThreshold)
            return 0;

        if (offset > 0f && centerIndex > 0)
            return -1;
        if (offset < 0f && centerIndex < itemCount - 1)
            return 1;

        return 0;
    }

    void StartShipSelectionSnap(int indexDelta)
    {
        shipSelectionSnapActive = true;
        shipSelectionSnapElapsed = 0f;
        shipSelectionSnapStartOffset = shipSelectionVisualOffset;
        shipSelectionSnapIndexDelta = indexDelta;
        shipSelectionSnapTargetOffset = indexDelta == 0 ? 0f : -indexDelta;
    }

    void StartPilotSelectionSnap(int indexDelta)
    {
        pilotSelectionSnapActive = true;
        pilotSelectionSnapElapsed = 0f;
        pilotSelectionSnapStartOffset = pilotSelectionVisualOffset;
        pilotSelectionSnapIndexDelta = indexDelta;
        pilotSelectionSnapTargetOffset = indexDelta == 0 ? 0f : -indexDelta;
    }

    void UpdateShipSelectionSnap()
    {
        if (!shipSelectionSnapActive || shipSelectionDragActive)
            return;

        shipSelectionSnapElapsed += Time.unscaledDeltaTime;
        float t = SelectionCarouselSnapDuration > 0f ? Mathf.Clamp01(shipSelectionSnapElapsed / SelectionCarouselSnapDuration) : 1f;
        float eased = t * t * (3f - 2f * t);
        shipSelectionVisualOffset = Mathf.Lerp(shipSelectionSnapStartOffset, shipSelectionSnapTargetOffset, eased);
        ApplyShipSelectionCarouselVisuals();

        if (t >= 1f)
            FinishShipSelectionSnap();
    }

    void UpdatePilotSelectionSnap()
    {
        if (!pilotSelectionSnapActive || pilotSelectionDragActive)
            return;

        pilotSelectionSnapElapsed += Time.unscaledDeltaTime;
        float t = SelectionCarouselSnapDuration > 0f ? Mathf.Clamp01(pilotSelectionSnapElapsed / SelectionCarouselSnapDuration) : 1f;
        float eased = t * t * (3f - 2f * t);
        pilotSelectionVisualOffset = Mathf.Lerp(pilotSelectionSnapStartOffset, pilotSelectionSnapTargetOffset, eased);
        ApplyPilotSelectionCarouselVisuals();

        if (t >= 1f)
            FinishPilotSelectionSnap();
    }

    void FinishShipSelectionSnap()
    {
        int indexDelta = shipSelectionSnapIndexDelta;
        shipSelectionSnapActive = false;
        shipSelectionVisualOffset = 0f;
        shipSelectionSnapIndexDelta = 0;

        if (indexDelta != 0)
        {
            shipSelectionCenterIndex = Mathf.Clamp(shipSelectionCenterIndex + indexDelta, 0, SelectableShipTypes.Length - 1);
            shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];
            RefreshShipSelectionView();
        }
        else
        {
            ApplyShipSelectionCarouselVisuals();
        }
    }

    void FinishPilotSelectionSnap()
    {
        int indexDelta = pilotSelectionSnapIndexDelta;
        pilotSelectionSnapActive = false;
        pilotSelectionVisualOffset = 0f;
        pilotSelectionSnapIndexDelta = 0;

        if (indexDelta != 0)
        {
            pilotSelectionCenterIndex = Mathf.Clamp(pilotSelectionCenterIndex + indexDelta, 0, PilotCatalog.AllDefinitions.Count - 1);
            RefreshPilotSelectionView();
        }
        else
        {
            ApplyPilotSelectionCarouselVisuals();
        }
    }

    bool ConsumeShipSelectionClickSuppression()
    {
        if (Time.frameCount > shipSelectionSuppressClickUntilFrame)
            return false;

        shipSelectionSuppressClickUntilFrame = 0;
        return true;
    }

    bool ConsumePilotSelectionClickSuppression()
    {
        if (Time.frameCount > pilotSelectionSuppressClickUntilFrame)
            return false;

        pilotSelectionSuppressClickUntilFrame = 0;
        return true;
    }

    void ApplyShipSelectionCarouselVisuals()
    {
        if (shipSelectionCardObjects == null)
            return;

        for (int i = 0; i < shipSelectionCardObjects.Length; i++)
        {
            if (shipSelectionCardObjects[i] == null || !shipSelectionCardObjects[i].activeSelf)
                continue;

            ApplyShipSelectionCardVisualState(i, (i - 1) + shipSelectionVisualOffset);
        }

        UpdateShipSelectionCardLayering();
    }

    void ApplyPilotSelectionCarouselVisuals()
    {
        if (pilotSelectionCardObjects == null)
            return;

        for (int i = 0; i < pilotSelectionCardObjects.Length; i++)
        {
            if (pilotSelectionCardObjects[i] == null || !pilotSelectionCardObjects[i].activeSelf)
                continue;

            ApplyPilotSelectionCardVisualState(i, (i - 1) + pilotSelectionVisualOffset);
        }

        UpdatePilotSelectionCardLayering();
    }

    void ApplyShipSelectionCardVisualState(int cardIndex, float slot)
    {
        GameObject cardObject = shipSelectionCardObjects != null && cardIndex >= 0 && cardIndex < shipSelectionCardObjects.Length
            ? shipSelectionCardObjects[cardIndex]
            : null;
        if (cardObject == null)
            return;

        float centerAmount = GetSelectionCarouselCenterAmount(slot);
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchoredPosition = EvaluateSelectionCarouselPosition(slot, ShipSelectionLeftPosition, ShipSelectionCenterPosition, ShipSelectionRightPosition);
            cardRect.sizeDelta = EvaluateSelectionCarouselSize(slot, ShipSelectionSideSize, ShipSelectionCenterSize);
        }

        Image previewImage = shipSelectionCardImages != null && cardIndex < shipSelectionCardImages.Length
            ? shipSelectionCardImages[cardIndex]
            : null;
        if (previewImage != null)
        {
            RectTransform imageRect = previewImage.rectTransform;
            imageRect.anchoredPosition = Vector2.Lerp(ShipSelectionSideImagePosition, ShipSelectionCenterImagePosition, centerAmount);
            imageRect.sizeDelta = Vector2.Lerp(ShipSelectionSideImageSize, ShipSelectionCenterImageSize, centerAmount);
        }

        Image cardImage = cardObject.GetComponent<Image>();
        if (cardImage != null)
            cardImage.color = Color.Lerp(new Color(0.07f, 0.1f, 0.15f, 0.68f), new Color(0.08f, 0.11f, 0.16f, 0.76f), centerAmount);

        LayoutShipSelectionStats(cardIndex, centerAmount);

        GameObject[] slotObjects = shipSelectionCardSlotObjects != null && cardIndex < shipSelectionCardSlotObjects.Length
            ? shipSelectionCardSlotObjects[cardIndex]
            : null;
        if (slotObjects == null)
            return;

        Vector2[] slotLayout = BuildShipSelectionSlotLayout(centerAmount);
        for (int i = 0; i < slotObjects.Length && i < slotLayout.Length; i++)
        {
            if (slotObjects[i] == null)
                continue;

            RectTransform slotRect = slotObjects[i].GetComponent<RectTransform>();
            if (slotRect == null)
                continue;

            slotRect.anchoredPosition = slotLayout[i];
            slotRect.sizeDelta = GetShipSelectionSlotSize(centerAmount >= 0.5f);
        }
    }

    void ApplyPilotSelectionCardVisualState(int cardIndex, float slot)
    {
        GameObject cardObject = pilotSelectionCardObjects != null && cardIndex >= 0 && cardIndex < pilotSelectionCardObjects.Length
            ? pilotSelectionCardObjects[cardIndex]
            : null;
        if (cardObject == null)
            return;

        float centerAmount = GetSelectionCarouselCenterAmount(slot);
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchoredPosition = EvaluateSelectionCarouselPosition(slot, PilotSelectionLeftPosition, PilotSelectionCenterPosition, PilotSelectionRightPosition);
            cardRect.sizeDelta = EvaluateSelectionCarouselSize(slot, PilotSelectionSideSize, PilotSelectionCenterSize);
        }

        Image previewImage = pilotSelectionCardImages != null && cardIndex < pilotSelectionCardImages.Length
            ? pilotSelectionCardImages[cardIndex]
            : null;
        if (previewImage != null)
        {
            RectTransform imageRect = previewImage.rectTransform;
            imageRect.offsetMin = Vector2.Lerp(PilotSelectionSideImageOffsetMin, PilotSelectionCenterImageOffsetMin, centerAmount);
            imageRect.offsetMax = Vector2.Lerp(PilotSelectionSideImageOffsetMax, PilotSelectionCenterImageOffsetMax, centerAmount);
        }

        int targetIndex = pilotSelectionCenterIndex + cardIndex - 1;
        bool selected = targetIndex >= 0 &&
                        targetIndex < PilotCatalog.AllDefinitions.Count &&
                        string.Equals(selectedPilotId, PilotCatalog.AllDefinitions[targetIndex].Id, StringComparison.Ordinal);
        Image cardImage = cardObject.GetComponent<Image>();
        if (cardImage != null)
        {
            Color sideColor = selected ? new Color(0.12f, 0.25f, 0.22f, 0.9f) : new Color(0.08f, 0.11f, 0.16f, 0.86f);
            Color centerColor = selected ? new Color(0.12f, 0.25f, 0.22f, 0.98f) : new Color(0.11f, 0.16f, 0.22f, 0.96f);
            cardImage.color = Color.Lerp(sideColor, centerColor, centerAmount);
        }

        TMP_Text lockText = pilotSelectionCardLockTexts != null && cardIndex < pilotSelectionCardLockTexts.Length
            ? pilotSelectionCardLockTexts[cardIndex]
            : null;
        if (lockText != null)
            lockText.fontSize = Mathf.Lerp(16f, 22f, centerAmount);
    }

    static Vector2 EvaluateSelectionCarouselPosition(float slot, Vector2 left, Vector2 center, Vector2 right)
    {
        if (slot <= -1f)
            return new Vector2(left.x - ((center.x - left.x) * (-slot - 1f)), left.y);
        if (slot < 0f)
            return Vector2.Lerp(left, center, slot + 1f);
        if (slot <= 1f)
            return Vector2.Lerp(center, right, slot);

        return new Vector2(right.x + ((right.x - center.x) * (slot - 1f)), right.y);
    }

    static Vector2 EvaluateSelectionCarouselSize(float slot, Vector2 side, Vector2 center)
    {
        return Vector2.Lerp(side, center, GetSelectionCarouselCenterAmount(slot));
    }

    static float GetSelectionCarouselCenterAmount(float slot)
    {
        return Mathf.Clamp01(1f - Mathf.Abs(slot));
    }

    static void SortSelectionCardsByCenter(GameObject[] cardObjects, float visualOffset)
    {
        if (cardObjects == null)
            return;

        int[] order = { 0, 1, 2 };
        Array.Sort(order, (a, b) =>
        {
            float aCenterAmount = GetSelectionCarouselCenterAmount((a - 1) + visualOffset);
            float bCenterAmount = GetSelectionCarouselCenterAmount((b - 1) + visualOffset);
            return aCenterAmount.CompareTo(bCenterAmount);
        });

        for (int i = 0; i < order.Length; i++)
        {
            int cardIndex = order[i];
            if (cardIndex >= 0 &&
                cardIndex < cardObjects.Length &&
                cardObjects[cardIndex] != null &&
                cardObjects[cardIndex].activeSelf)
            {
                cardObjects[cardIndex].transform.SetSiblingIndex(i);
            }
        }
    }

    void MovePilotSelectionLeft()
    {
        if (inventoryActionInProgress || pilotSelectionDragActive || pilotSelectionSnapActive)
            return;

        ResetPilotSelectionCarouselMotion();
        pilotSelectionCenterIndex = Mathf.Max(0, pilotSelectionCenterIndex - 1);
        RefreshPilotSelectionView();
    }

    void MovePilotSelectionRight()
    {
        if (inventoryActionInProgress || pilotSelectionDragActive || pilotSelectionSnapActive)
            return;

        ResetPilotSelectionCarouselMotion();
        pilotSelectionCenterIndex = Mathf.Min(PilotCatalog.AllDefinitions.Count - 1, pilotSelectionCenterIndex + 1);
        RefreshPilotSelectionView();
    }

    public void OnPilotSelectionSwiped(float horizontalDelta)
    {
        if (inventoryActionInProgress)
            return;

        if (Mathf.Abs(horizontalDelta) < 72f)
            return;

        if (horizontalDelta > 0f)
            MovePilotSelectionLeft();
        else
            MovePilotSelectionRight();
    }

    void OnPilotSelectionCardClicked(int cardIndex)
    {
        if (inventoryActionInProgress || pilotSelectionDragActive || pilotSelectionSnapActive || pilotSelectionCardObjects == null)
            return;

        if (cardIndex < 0 || cardIndex >= pilotSelectionCardObjects.Length)
            return;

        if (cardIndex == 1)
        {
            CommitPilotSelection(PilotCatalog.AllDefinitions[pilotSelectionCenterIndex].Id);
            return;
        }

        int direction = cardIndex == 0 ? -1 : 1;
        pilotSelectionCenterIndex = Mathf.Clamp(pilotSelectionCenterIndex + direction, 0, PilotCatalog.AllDefinitions.Count - 1);
        RefreshPilotSelectionView();
    }

    async void CommitPilotSelection(string pilotId)
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (!PilotCatalog.IsPilotUnlocked(profile, pilotId))
        {
            if (pilotSelectionStatusText != null)
                pilotSelectionStatusText.text = PilotCatalog.GetUnlockRequirementText(profile, pilotId);
            RefreshPilotSelectionView();
            return;
        }

        inventoryActionInProgress = true;
        SetInteractable(false);
        if (pilotSelectionStatusText != null)
            pilotSelectionStatusText.text = "Selecting pilot...";

        try
        {
            bool changed = await PlayerProfileService.Instance.TryChangePilotAsync(pilotId);
            if (!changed)
            {
                if (pilotSelectionStatusText != null)
                    pilotSelectionStatusText.text = PilotCatalog.GetUnlockRequirementText(PlayerProfileService.Instance.CurrentProfile, pilotId);
                return;
            }

            selectedPilotId = PilotCatalog.NormalizePilotId(pilotId);
            RefreshView();
            SwitchToScreen(ProfileScreen.Home);
        }
        catch (Exception ex)
        {
            Debug.LogError("Pilot selection failed: " + ex);
            if (pilotSelectionStatusText != null)
                pilotSelectionStatusText.text = "Pilot change failed.";
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshPilotSelectionView();
        }
    }

    void RefreshPilotSelectionView()
    {
        using (PilotSelectionRefreshMarker.Auto())
        {
            if (pilotSelectionViewObject == null || pilotSelectionCardObjects == null || PilotCatalog.AllDefinitions.Count <= 0)
                return;

            pilotSelectionCenterIndex = Mathf.Clamp(pilotSelectionCenterIndex, 0, PilotCatalog.AllDefinitions.Count - 1);
            PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
            PilotDefinition centerDefinition = PilotCatalog.AllDefinitions[pilotSelectionCenterIndex];
            bool centerUnlocked = PilotCatalog.IsPilotUnlocked(profile, centerDefinition.Id);

            if (pilotSelectionTitleText != null)
                pilotSelectionTitleText.text = centerDefinition.DisplayName;

            if (pilotSelectionBackButton != null)
                LayoutSharedProfileBackButton(pilotSelectionBackButton.GetComponent<RectTransform>());

            if (!inventoryActionInProgress && pilotSelectionStatusText != null)
            {
                pilotSelectionStatusText.text = centerUnlocked
                    ? string.Equals(selectedPilotId, centerDefinition.Id, StringComparison.Ordinal) ? "SELECTED" : "TAP CENTER PORTRAIT TO SELECT"
                    : PilotCatalog.GetUnlockRequirementText(profile, centerDefinition.Id);
            }

            if (pilotSelectionAbilitiesText != null)
                pilotSelectionAbilitiesText.text = BuildPilotAbilitiesText(centerDefinition);

            for (int i = 0; i < pilotSelectionCardObjects.Length; i++)
            {
                int offset = i - 1;
                int targetIndex = pilotSelectionCenterIndex + offset;
                bool visible = targetIndex >= 0 && targetIndex < PilotCatalog.AllDefinitions.Count;
                pilotSelectionCardObjects[i].SetActive(visible);
                if (!visible)
                    continue;

                UpdatePilotSelectionCard(i, PilotCatalog.AllDefinitions[targetIndex], i == 1);
            }

            if (pilotSelectionPrevButton != null)
                pilotSelectionPrevButton.gameObject.SetActive(pilotSelectionCenterIndex > 0);
            if (pilotSelectionNextButton != null)
                pilotSelectionNextButton.gameObject.SetActive(pilotSelectionCenterIndex < PilotCatalog.AllDefinitions.Count - 1);

            ApplyPilotSelectionCarouselVisuals();
            pilotSelectionDirty = false;
        }
    }

    void UpdatePilotSelectionCard(int cardIndex, PilotDefinition definition, bool centerCard)
    {
        if (definition == null || pilotSelectionCardObjects == null || cardIndex < 0 || cardIndex >= pilotSelectionCardObjects.Length)
            return;

        RectTransform cardRect = pilotSelectionCardObjects[cardIndex].GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = centerCard
                ? new Vector2(0f, -162f)
                : new Vector2(cardIndex == 0 ? -560f : 560f, -190f);
            cardRect.sizeDelta = centerCard
                ? new Vector2(560f, 620f)
                : new Vector2(440f, 540f);
        }

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        bool unlocked = PilotCatalog.IsPilotUnlocked(profile, definition.Id);
        bool selected = string.Equals(selectedPilotId, definition.Id, StringComparison.Ordinal);

        if (pilotSelectionCardImages != null && pilotSelectionCardImages[cardIndex] != null)
        {
            Sprite normalSprite = LoadPilotPortraitSprite(definition);
            pilotSelectionCardImages[cardIndex].sprite = unlocked ? normalSprite : GetGrayscalePilotPortraitSprite(definition, normalSprite);
            pilotSelectionCardImages[cardIndex].color = pilotSelectionCardImages[cardIndex].sprite != null
                ? unlocked ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f)
                : new Color(1f, 1f, 1f, 0f);
        }

        if (pilotSelectionCardNames != null && pilotSelectionCardNames[cardIndex] != null)
        {
            pilotSelectionCardNames[cardIndex].gameObject.SetActive(false);
            pilotSelectionCardNames[cardIndex].text = string.Empty;
            pilotSelectionCardNames[cardIndex].fontSize = centerCard ? 28f : 22f;
            pilotSelectionCardNames[cardIndex].color = selected
                ? new Color(0.58f, 0.9f, 0.78f, 1f)
                : new Color(0.94f, 0.97f, 1f, 1f);
        }

        if (pilotSelectionCardLockTexts != null && pilotSelectionCardLockTexts[cardIndex] != null)
        {
            pilotSelectionCardLockTexts[cardIndex].gameObject.SetActive(!unlocked);
            pilotSelectionCardLockTexts[cardIndex].text = PilotCatalog.GetUnlockRequirementText(profile, definition.Id);
            pilotSelectionCardLockTexts[cardIndex].fontSize = centerCard ? 22f : 16f;
        }

        Image cardImage = pilotSelectionCardObjects[cardIndex].GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.color = selected
                ? new Color(0.12f, 0.25f, 0.22f, centerCard ? 0.98f : 0.9f)
                : centerCard
                ? new Color(0.11f, 0.16f, 0.22f, 0.96f)
                : new Color(0.08f, 0.11f, 0.16f, 0.86f);
        }
    }

    void UpdatePilotSelectionCardLayering()
    {
        if (pilotSelectionCardObjects == null)
            return;

        SortSelectionCardsByCenter(pilotSelectionCardObjects, pilotSelectionVisualOffset);

        if (pilotSelectionBackButton != null)
            pilotSelectionBackButton.transform.SetAsLastSibling();
        if (pilotSelectionPrevButton != null && pilotSelectionPrevButton.gameObject.activeSelf)
            pilotSelectionPrevButton.transform.SetAsLastSibling();
        if (pilotSelectionNextButton != null && pilotSelectionNextButton.gameObject.activeSelf)
            pilotSelectionNextButton.transform.SetAsLastSibling();
        if (pilotSelectionAbilitiesText != null)
            pilotSelectionAbilitiesText.transform.SetAsLastSibling();
        if (pilotSelectionStatusText != null)
            pilotSelectionStatusText.transform.SetAsLastSibling();
    }

    string BuildPilotAbilitiesText(PilotDefinition definition)
    {
        if (definition == null)
            return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(definition.ActiveAbilityDescription))
        {
            builder.Append("<color=#72FF9B><b>ACTIVE - ");
            builder.Append(string.IsNullOrWhiteSpace(definition.ActiveAbilityName) ? "Pilot Ability" : definition.ActiveAbilityName);
            builder.Append(":</b></color> ");
            builder.Append(definition.ActiveAbilityDescription);
        }

        if (definition.AbilityDescriptions == null || definition.AbilityDescriptions.Length == 0)
            return builder.ToString();

        for (int i = 0; i < definition.AbilityDescriptions.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(definition.AbilityDescriptions[i]))
                continue;

            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append("<color=#AFC8FF><b>PASSIVE ");
            builder.Append(i + 1);
            builder.Append(":</b></color> ");
            builder.Append(definition.AbilityDescriptions[i]);
        }

        return builder.ToString();
    }

    void HideShipImageModal()
    {
        if (shipImageModalObject != null)
            shipImageModalObject.SetActive(false);
    }

    void MoveShipSelectionLeft()
    {
        if (inventoryActionInProgress || shipSelectionDragActive || shipSelectionSnapActive)
            return;

        HideShipSelectionMissionDetails();
        ResetShipSelectionCarouselMotion();
        shipSelectionCenterIndex = Mathf.Max(0, shipSelectionCenterIndex - 1);
        shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];
        RefreshShipSelectionView();
    }

    void MoveShipSelectionRight()
    {
        if (inventoryActionInProgress || shipSelectionDragActive || shipSelectionSnapActive)
            return;

        HideShipSelectionMissionDetails();
        ResetShipSelectionCarouselMotion();
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
        if (inventoryActionInProgress || shipSelectionDragActive || shipSelectionSnapActive)
            return;

        if (cardIndex < 0 || cardIndex >= shipSelectionCardObjects.Length)
            return;

        if (cardIndex == 1)
        {
            CommitShipSelection(shipSelectionCenterType);
            return;
        }

        int direction = cardIndex == 0 ? -1 : 1;
        HideShipSelectionMissionDetails();
        shipSelectionCenterIndex = Mathf.Clamp(shipSelectionCenterIndex + direction, 0, SelectableShipTypes.Length - 1);
        shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];
        RefreshShipSelectionView();
    }

    async void CommitShipSelection(ShipType shipType)
    {
        if (!IsShipTypeUnlockedForUi(shipType))
        {
            RefreshShipSelectionView();
            ShowShipSelectionMissionDetails();
            return;
        }

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
        if (!IsShipTypeUnlockedForUi(shipSelectionCenterType))
            return;

        int[] allowedSkins = ShipCatalog.GetSkinsForShipType(shipSelectionCenterType);
        if (buttonIndex < 0 || buttonIndex >= allowedSkins.Length)
            return;

        shipSelectionSkinByType[shipSelectionCenterType] = allowedSkins[buttonIndex];
        RefreshShipSelectionView();
    }

    async void OnViperRepairDonateClicked()
    {
        if (inventoryActionInProgress || !PlayerProfileService.HasInstance)
            return;

        inventoryActionInProgress = true;
        SetInteractable(false);
        if (shipSelectionStatusText != null)
            shipSelectionStatusText.text = "Repairing Viper...";

        try
        {
            bool donated = await PlayerProfileService.Instance.DonateViperRepairPartsAsync();
            if (shipSelectionStatusText != null)
                shipSelectionStatusText.text = donated ? "Viper systems ready for test flights." : "Spare parts required.";

            RefreshView();
        }
        catch (Exception ex)
        {
            Debug.LogError("Viper repair donation failed: " + ex);
            if (shipSelectionStatusText != null)
                shipSelectionStatusText.text = "Repair failed.";
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshShipSelectionView();
        }
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
        using (ShipSelectionRefreshMarker.Auto())
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
            bool centerShipUnlocked = IsShipTypeUnlockedForUi(shipSelectionCenterType);
            for (int i = 0; i < shipSelectionSkinButtons.Length; i++)
            {
                if (shipSelectionSkinButtons[i] == null)
                    continue;

                bool active = i < allowedSkins.Length;
                shipSelectionSkinButtons[i].gameObject.SetActive(active);
                shipSelectionSkinButtons[i].interactable = active && centerShipUnlocked && inventoryControlsInteractable && !inventoryActionInProgress;
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
                    !centerShipUnlocked ? new Color(0.11f, 0.12f, 0.15f, 0.84f) : selected ? new Color(0.2f, 0.38f, 0.58f, 0.98f) : new Color(0.16f, 0.2f, 0.27f, 0.95f),
                    !centerShipUnlocked ? new Color(0.16f, 0.17f, 0.2f, 0.9f) : selected ? new Color(0.28f, 0.5f, 0.74f, 1f) : new Color(0.22f, 0.3f, 0.42f, 1f));
            }

            RefreshShipSelectionMissionDetailsControls();
            ApplyShipSelectionCarouselVisuals();
            shipSelectionDirty = false;
        }
    }

    void UpdateShipSelectionCardLayering()
    {
        if (shipSelectionCardObjects == null)
            return;

        SortSelectionCardsByCenter(shipSelectionCardObjects, shipSelectionVisualOffset);

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
        if (shipSelectionCardDonateButtons != null)
        {
            for (int i = 0; i < shipSelectionCardDonateButtons.Length; i++)
            {
                if (shipSelectionCardDonateButtons[i] != null && shipSelectionCardDonateButtons[i].gameObject.activeSelf)
                    shipSelectionCardDonateButtons[i].transform.SetAsLastSibling();
            }
        }
        if (shipSelectionDetailsButton != null && shipSelectionDetailsButton.gameObject.activeSelf)
            shipSelectionDetailsButton.transform.SetAsLastSibling();
        if (shipSelectionStatusText != null)
            shipSelectionStatusText.transform.SetAsLastSibling();
        if (shipMissionDetailsPanelObject != null && shipMissionDetailsPanelObject.activeSelf)
            shipMissionDetailsPanelObject.transform.SetAsLastSibling();
    }

    void UpdateShipSelectionCard(int cardIndex, ShipType shipType, bool centerCard)
    {
        if (shipSelectionCardTitles == null || cardIndex < 0 || cardIndex >= shipSelectionCardTitles.Length)
            return;

        int skinIndex = GetShipSelectionDisplaySkin(shipType);
        PlayerShipDefinition definition = ShipCatalog.GetShipDefinition(shipType);
        bool shipUnlocked = IsShipTypeUnlockedForUi(shipType);
        ViperRecoveryStage viperStage = shipType == ShipType.Viper && PlayerProfileService.HasInstance
            ? PlayerProfileService.Instance.GetViperRecoveryStage()
            : ViperRecoveryStage.Complete;
        bool viperNeedsParts = shipType == ShipType.Viper && viperStage == ViperRecoveryStage.WreckRecovered;
        bool viperTesting = shipType == ShipType.Viper && viperStage == ViperRecoveryStage.Testing;
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
            image.color = image.sprite != null
                ? shipUnlocked ? Color.white : new Color(0.34f, 0.34f, 0.34f, centerCard ? 0.72f : 0.56f)
                : new Color(1f, 1f, 1f, 0f);
            RectTransform imageRect = image.rectTransform;
            imageRect.anchoredPosition = centerCard ? new Vector2(48f, -64f) : new Vector2(30f, -48f);
            imageRect.sizeDelta = centerCard ? new Vector2(680f, 760f) : new Vector2(470f, 540f);
        }

        TMP_Text lockText = shipSelectionCardLockTexts != null && cardIndex < shipSelectionCardLockTexts.Length
            ? shipSelectionCardLockTexts[cardIndex]
            : null;
        if (lockText != null)
        {
            bool showProgressText = !shipUnlocked || viperTesting;
            lockText.gameObject.SetActive(showProgressText);
            lockText.text = BuildShipSelectionCardStatusText(shipType, shipUnlocked, viperStage);
            lockText.enableAutoSizing = false;
            lockText.fontSize = centerCard ? 24f : 18f;
            lockText.fontSizeMin = lockText.fontSize;
            lockText.fontSizeMax = lockText.fontSize;
            lockText.lineSpacing = centerCard ? 4f : 2f;
            lockText.textWrappingMode = TextWrappingModes.Normal;
            lockText.overflowMode = TextOverflowModes.Truncate;
            lockText.margin = centerCard
                ? new Vector4(18f, 8f, 18f, 8f)
                : new Vector4(12f, 6f, 12f, 6f);
            RectTransform lockRect = lockText.rectTransform;
            lockRect.anchoredPosition = centerCard ? new Vector2(18f, -92f) : new Vector2(10f, -72f);
            lockRect.sizeDelta = centerCard ? new Vector2(660f, 154f) : new Vector2(430f, 118f);
            lockText.color = new Color(1f, 0.82f, 0.48f, 0.98f);
            lockText.transform.SetAsLastSibling();
        }

        Button donateButton = shipSelectionCardDonateButtons != null && cardIndex < shipSelectionCardDonateButtons.Length
            ? shipSelectionCardDonateButtons[cardIndex]
            : null;
        if (donateButton != null)
        {
            donateButton.gameObject.SetActive(viperNeedsParts);
            donateButton.interactable = viperNeedsParts &&
                                        centerCard &&
                                        inventoryControlsInteractable &&
                                        !inventoryActionInProgress &&
                                        PlayerProfileService.Instance.IsViperRepairPartsDonationAvailable();
            RectTransform donateRect = donateButton.GetComponent<RectTransform>();
            if (donateRect != null)
            {
                donateRect.anchoredPosition = centerCard ? new Vector2(18f, -238f) : new Vector2(10f, -184f);
                donateRect.sizeDelta = centerCard ? new Vector2(118f, 118f) : new Vector2(92f, 92f);
            }

            ConfigureViperRepairButtonLabel(donateButton, centerCard);
            StyleButton(
                donateButton,
                donateButton.interactable ? new Color(0.12f, 0.38f, 0.22f, 0.98f) : new Color(0.16f, 0.17f, 0.18f, 0.86f),
                donateButton.interactable ? new Color(0.18f, 0.58f, 0.34f, 1f) : new Color(0.22f, 0.24f, 0.25f, 0.9f));
        }

        Image cardImage = shipSelectionCardObjects[cardIndex].GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.color = !shipUnlocked
                ? new Color(0.045f, 0.05f, 0.06f, centerCard ? 0.82f : 0.72f)
                : centerCard
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
            SetShipSelectionStatCard(statLabels, statValues, statFills, 8, ShipStatLabels[8], definition.BrakingDriftLevel.ToString(), NormalizeShipStat(definition.BrakingDriftLevel, stat => stat.BrakingDriftLevel));
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

                bool slotDefined = ShipCatalog.IsEquipmentSlotEnabled(i, skinIndex);
                slotObjects[i].SetActive(slotDefined);
                if (!slotDefined)
                    continue;
                bool slotUnlocked = !viperTesting || PlayerProfileService.Instance.IsEquipmentSlotEnabledForProfile(i, skinIndex);

                RectTransform slotRect = slotObjects[i].GetComponent<RectTransform>();
                if (slotRect != null)
                {
                    slotRect.anchoredPosition = slotLayout[i];
                    slotRect.sizeDelta = GetShipSelectionSlotSize(centerCard);
                }

                Image slotImage = slotObjects[i].GetComponent<Image>();
                if (slotImage != null)
                    slotImage.color = slotUnlocked ? GetShipSelectionSlotColor(i) : new Color(0.32f, 0.06f, 0.08f, 0.94f);

                Outline outline = slotObjects[i].GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = slotUnlocked ? GetShipSelectionSlotOutlineColor(i) : new Color(0.98f, 0.16f, 0.18f, 0.96f);
                    outline.effectDistance = new Vector2(2.2f, -2.2f);
                }

                TMP_Text slotText = slotObjects[i].GetComponentInChildren<TMP_Text>(true);
                ApplyEquipmentSlotPreviewTextStyle(slotText);
                if (slotText != null && !slotUnlocked)
                {
                    slotText.text = "X";
                    slotText.fontSize = centerCard ? 30f : 24f;
                    slotText.fontSizeMax = slotText.fontSize;
                    slotText.color = new Color(1f, 0.24f, 0.24f, 1f);
                }
                else if (slotText != null)
                {
                    slotText.text = GetShipSelectionSlotLabel(i);
                }
            }
        }
    }

    string BuildShipSelectionCardStatusText(ShipType shipType, bool shipUnlocked, ViperRecoveryStage viperStage)
    {
        if (shipType == ShipType.Viper)
        {
            if (viperStage == ViperRecoveryStage.WreckRecovered)
                return "VIPER REPAIR\nParts checklist in details";

            if (viperStage == ViperRecoveryStage.Testing)
                return "VIPER TEST FLIGHTS\nStabilize locked systems";

            if (!shipUnlocked)
                return "LOCKED\nRecover Viper wreck";
        }

        if (shipType == ShipType.Avenger && !shipUnlocked)
            return "LOCKED\nRecover Avenger";

        if (shipType == ShipType.CargoTruck && !shipUnlocked && PlayerProfileService.HasInstance)
        {
            int delivered = PlayerProfileService.Instance.GetBisonIndustrialPartsDeliveredCount();
            return "BISON HAUL\nIndustrial parts " + delivered + "/" + PlayerProfileService.BisonIndustrialPartsRequired;
        }

        if (shipType == ShipType.Invader && !shipUnlocked && PlayerProfileService.HasInstance)
        {
            int imprints = PlayerProfileService.Instance.GetInvaderImprintsRecoveredCount();
            return "INVADER DATA\nAlien imprints " + imprints + "/" + PlayerProfileService.InvaderImprintsRequired;
        }

        if (shipType == ShipType.Pathfinder && !shipUnlocked && PlayerProfileService.HasInstance)
        {
            PathfinderResearchProgressData progress = PlayerProfileService.Instance.GetPathfinderResearchProgress();
            PathfinderResearchStage pathfinderStage = (PathfinderResearchStage)Mathf.Clamp(progress.Stage, (int)PathfinderResearchStage.Locked, (int)PathfinderResearchStage.Complete);
            int hackedCount = progress.HackedShipTypeIds != null
                ? Mathf.Clamp(progress.HackedShipTypeIds.Length, 0, PlayerProfileService.PathfinderHackedShipTypesRequired)
                : 0;
            int deliveredValuables = Mathf.Clamp(progress.DeliveredValuableItems, 0, PlayerProfileService.PathfinderValuableItemsRequired);

            if (pathfinderStage == PathfinderResearchStage.DocumentationReady)
                return "PATHFINDER RESEARCH\nDeliver documentation";

            if (pathfinderStage == PathfinderResearchStage.ResourcesRequired)
                return "PATHFINDER RESEARCH\nValuables: " + deliveredValuables + "/" + PlayerProfileService.PathfinderValuableItemsRequired;

            if (pathfinderStage == PathfinderResearchStage.FinalVisitRequired)
                return "PATHFINDER RESEARCH\nVisit Research Station";

            return "PATHFINDER RESEARCH\nShip data: " + hackedCount + "/" + PlayerProfileService.PathfinderHackedShipTypesRequired;
        }

        if (shipType == ShipType.Arrow && !shipUnlocked && PlayerProfileService.HasInstance)
        {
            ArrowLicenseProgressData progress = PlayerProfileService.Instance.GetArrowLicenseProgress();
            int qualificationCount = Mathf.Clamp(progress.QualifierChips, 0, PlayerProfileService.ArrowQualifierChipsRequired);
            int completedMapCount = PlayerProfileService.CountCompletedArrowRaceMaps(progress);
            return "ARROW LICENSE\nQualifiers " + qualificationCount + "/" + PlayerProfileService.ArrowQualifierChipsRequired +
                   ", maps " + completedMapCount + "/" + PlayerProfileService.ArrowMapRacesRequired;
        }

        return shipUnlocked ? string.Empty : "LOCKED";
    }

    string BuildShipSelectionProgressText(ShipType shipType, bool shipUnlocked, ViperRecoveryStage viperStage)
    {
        if (shipType == ShipType.Viper)
        {
            if (viperStage == ViperRecoveryStage.WreckRecovered)
            {
                int neutral = PlayerProfileService.Instance.CountViperRepairPartItem(InventoryItemCatalog.NeutralFighterSalvageId);
                int drones = PlayerProfileService.Instance.CountViperRepairPartItem(InventoryItemCatalog.DroidScrapId);
                int trucks = PlayerProfileService.Instance.CountViperRepairPartItem(InventoryItemCatalog.SpaceTruckWreckId);
                return "Repair the recovered Viper wreck before it can fly safely.\n\n" +
                       "Collect the required spare parts during missions. Keep them in your inventory or ship cargo, then return here and use the repair button when the checklist is complete.\n\n" +
                       "Parts checklist:\n" +
                       neutral + "/" + PlayerProfileService.ViperNeutralFighterWrecksRequired + " Neutral Fighter wrecks\n" +
                       drones + "/" + PlayerProfileService.ViperDroneWrecksRequired + " Drone wrecks\n" +
                       trucks + "/" + PlayerProfileService.ViperSpaceTruckWrecksRequired + " Space Truck wrecks";
            }

            if (viperStage == ViperRecoveryStage.Testing)
            {
                ViperRecoveryProgressData progress = PlayerProfileService.Instance.GetViperRecoveryProgress();
                int subsystemTotal = PlayerProfileService.GetAllViperTestSubsystemIds().Length;
                int unlockedSubsystems = progress.UnlockedSubsystemIds != null
                    ? Mathf.Clamp(progress.UnlockedSubsystemIds.Length, 0, subsystemTotal)
                    : 0;
                int remainingSubsystems = Mathf.Max(0, subsystemTotal - unlockedSubsystems);
                int estimatedFlights = Mathf.CeilToInt(remainingSubsystems / (float)PlayerProfileService.ViperTestFlightSubsystemUnlocksPerReturn);
                return "The Viper is repaired, but its systems still need flight testing.\n\n" +
                       "Start rounds with Viper and survive at least " + Mathf.RoundToInt(PlayerProfileService.ViperMinimumTestFlightSeconds) + " seconds before returning. Each valid return stabilizes up to " + PlayerProfileService.ViperTestFlightSubsystemUnlocksPerReturn + " locked systems.\n\n" +
                       "Systems stabilized: " + unlockedSubsystems + "/" + subsystemTotal + "\n" +
                       "Estimated successful test flights left: " + estimatedFlights;
            }

            if (!shipUnlocked)
                return "Recover the Viper wreck during a mission.\n\n" +
                       "After the wreck is recovered, this screen will show the repair checklist and the parts needed to rebuild it.";
        }

        if (shipType == ShipType.Avenger)
            return "Recover Avenger during its unlock mission.\n\n" +
                   "Bring it back safely to add it to your ship roster.";

        if (shipType == ShipType.CargoTruck && PlayerProfileService.HasInstance)
        {
            int delivered = PlayerProfileService.Instance.GetBisonIndustrialPartsDeliveredCount();
            return "Earn Bison by completing industrial haul missions.\n\n" +
                   "When the Industrial Zone event appears, pick up industrial parts and escape through the Extraction Zone while carrying them.\n\n" +
                   "Industrial parts delivered: " + delivered + "/" + PlayerProfileService.BisonIndustrialPartsRequired;
        }

        if (shipType == ShipType.Invader && PlayerProfileService.HasInstance)
        {
            int imprints = PlayerProfileService.Instance.GetInvaderImprintsRecoveredCount();
            return "Unlock Invader by recovering alien imprints from Invader events.\n\n" +
                   "Follow the active alien objective in the round, such as contact, stabilize, or sync. Escape after the objective is complete to preserve the imprint.\n\n" +
                   "Alien imprints recovered: " + imprints + "/" + PlayerProfileService.InvaderImprintsRequired;
        }

        if (shipType == ShipType.Pathfinder && PlayerProfileService.HasInstance)
        {
            PathfinderResearchProgressData progress = PlayerProfileService.Instance.GetPathfinderResearchProgress();
            PathfinderResearchStage pathfinderStage = (PathfinderResearchStage)Mathf.Clamp(progress.Stage, (int)PathfinderResearchStage.Locked, (int)PathfinderResearchStage.Complete);
            if (pathfinderStage == PathfinderResearchStage.Complete)
                return string.Empty;

            int hackedCount = progress.HackedShipTypeIds != null
                ? Mathf.Clamp(progress.HackedShipTypeIds.Length, 0, PlayerProfileService.PathfinderHackedShipTypesRequired)
                : 0;
            int deliveredValuables = Mathf.Clamp(progress.DeliveredValuableItems, 0, PlayerProfileService.PathfinderValuableItemsRequired);

            if (pathfinderStage == PathfinderResearchStage.DocumentationReady)
                return "You have enough ship data to prepare prototype documentation.\n\n" +
                       "Deliver Ship Prototype Documentation to a Research Station. Keep the package safe until the station accepts it.";

            if (pathfinderStage == PathfinderResearchStage.ResourcesRequired)
            {
                return "The Research Station needs valuable items to continue Pathfinder work.\n\n" +
                       "Deliver valuable cargo to a Research Station. Accepted examples include Legendary Asteroid, Cash Suitcase, and Pirate Case.\n\n" +
                       "Valuable items delivered: " + deliveredValuables + "/" + PlayerProfileService.PathfinderValuableItemsRequired;
            }

            if (pathfinderStage == PathfinderResearchStage.FinalVisitRequired)
                return "Pathfinder research is almost complete.\n\n" +
                       "Visit another Research Station to finalize the project and unlock the ship.";

            return "Collect research data by hacking different ship types during missions.\n\n" +
                   "Each unique ship type counts once, so look for variety instead of repeating the same target.\n\n" +
                   "Ship data collected: " + hackedCount + "/" + PlayerProfileService.PathfinderHackedShipTypesRequired;
        }

        if (shipType == ShipType.Arrow && PlayerProfileService.HasInstance)
        {
            ArrowLicenseProgressData progress = PlayerProfileService.Instance.GetArrowLicenseProgress();
            ArrowLicenseStage arrowStage = (ArrowLicenseStage)Mathf.Clamp(progress.Stage, (int)ArrowLicenseStage.Locked, (int)ArrowLicenseStage.Complete);
            if (arrowStage == ArrowLicenseStage.Complete)
                return string.Empty;

            int qualificationCount = Mathf.Clamp(progress.QualifierChips, 0, PlayerProfileService.ArrowQualifierChipsRequired);
            int completedMapCount = PlayerProfileService.CountCompletedArrowRaceMaps(progress);
            string partsProgress = "Ion Nozzle: " + FormatDelivered(progress.IonNozzleDelivered) + ", " +
                                   "Gyro Stabilizer: " + FormatDelivered(progress.GyroStabilizerDelivered) + ", " +
                                   "Race Transponder: " + FormatDelivered(progress.RaceTransponderDelivered);
            string bestRank = FormatArrowRank(progress.BestTimeTrialRank);

            return "Arrow Racing License\n" +
                   "Complete the racing license chain to unlock Arrow.\n\n" +
                   "1. Complete qualification races: " + qualificationCount + "/" + PlayerProfileService.ArrowQualifierChipsRequired + "\n" +
                   "2. Collect Arrow Race Tokens from AI players and race on different maps: " + completedMapCount + "/" + PlayerProfileService.ArrowMapRacesRequired + "\n" +
                   "3. Deliver racing parts: " + partsProgress + "\n" +
                   "4. Finish the time trial with rank " + ((ArrowTimeTrialRank)PlayerProfileService.ArrowTimeTrialMinimumRank).ToString() + " or better. Best rank: " + bestRank + "\n" +
                   "5. Win the ghost race: " + FormatDelivered(progress.GhostRaceWon) + "\n" +
                   "6. Finish the Final Race with Arrow and escape.";
        }

        return "LOCKED";
    }

    string FormatDelivered(bool delivered)
    {
        return delivered ? "OK" : "-";
    }

    string FormatArrowRank(int rank)
    {
        ArrowTimeTrialRank safeRank = (ArrowTimeTrialRank)Mathf.Clamp(rank, (int)ArrowTimeTrialRank.None, (int)ArrowTimeTrialRank.S);
        return safeRank == ArrowTimeTrialRank.None ? "-" : safeRank.ToString();
    }

    void LayoutShipSelectionStats(int cardIndex, bool centerCard)
    {
        LayoutShipSelectionStats(cardIndex, centerCard ? 1f : 0f);
    }

    void LayoutShipSelectionStats(int cardIndex, float centerAmount)
    {
        TMP_Text[] statLabels = shipSelectionCardStatLabelTexts != null && cardIndex < shipSelectionCardStatLabelTexts.Length
            ? shipSelectionCardStatLabelTexts[cardIndex]
            : null;
        if (statLabels == null)
            return;

        centerAmount = Mathf.Clamp01(centerAmount);
        float x = Mathf.Lerp(186f, 260f, centerAmount);
        float topY = Mathf.Lerp(-130f, -144f, centerAmount);
        Vector2 cardSize = Vector2.Lerp(new Vector2(182f, 46f), new Vector2(236f, 58f), centerAmount);
        float spacing = Mathf.Lerp(10f, 12f, centerAmount);
        float labelTop = Mathf.Lerp(-10f, -12f, centerAmount);
        float textHeight = Mathf.Lerp(16f, 20f, centerAmount);
        float statFontSize = Mathf.Lerp(14f, 18f, centerAmount);

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
            labelRect.anchoredPosition = new Vector2(12f, labelTop);
            labelRect.sizeDelta = new Vector2(cardSize.x * 0.48f, textHeight);
            label.fontSize = statFontSize;

            if (value != null)
            {
                RectTransform valueRect = value.rectTransform;
                valueRect.anchorMin = new Vector2(1f, 1f);
                valueRect.anchorMax = new Vector2(1f, 1f);
                valueRect.pivot = new Vector2(1f, 1f);
                valueRect.anchoredPosition = new Vector2(Mathf.Lerp(-8f, -10f, centerAmount), labelTop);
                valueRect.sizeDelta = new Vector2(cardSize.x * 0.42f, textHeight);
                value.fontSize = statFontSize;
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
                    barBgRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(8f, 10f, centerAmount));
                    barBgRect.sizeDelta = new Vector2(-18f, Mathf.Lerp(10f, 14f, centerAmount));
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
            bool enabled = inventory != null && IsEquipmentSlotEnabledForSelectedSkin(i);
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

        ApplyEquipmentSlotPreviewTextStyle(text);

        bool occupied = enabled && !string.IsNullOrWhiteSpace(itemId);
        Sprite itemSprite = occupied ? InventoryItemCatalog.GetIcon(itemId) : null;
        bool showDefaultWeaponPlaceholder = !occupied && InventoryItemCatalog.GetEquipmentSlotCategory(slotIndex) == InventoryItemCategory.Weapon;
        Sprite placeholderSprite = showDefaultWeaponPlaceholder ? WeaponAttackCatalog.GetWeaponIcon(WeaponAttackCatalog.SimpleGunId) : null;

        text.text = occupied && itemSprite == null ? InventoryItemCatalog.GetShortLabel(itemId) : label;
        text.color = new Color(0.92f, 0.96f, 0.98f, 0.98f);
        Image bg = text.transform.parent != null ? text.transform.parent.GetComponent<Image>() : null;
        if (bg != null)
            bg.color = GetShipSelectionSlotColor(slotIndex);

        Outline outline = text.transform.parent != null ? text.transform.parent.GetComponent<Outline>() : null;
        if (outline != null)
        {
            outline.effectColor = GetShipSelectionSlotOutlineColor(slotIndex);
            outline.effectDistance = new Vector2(2.2f, -2.2f);
            outline.enabled = true;
        }

        if (icon != null)
        {
            icon.sprite = occupied ? itemSprite : placeholderSprite;
            icon.enabled = occupied
                ? itemSprite != null
                : placeholderSprite != null;
            icon.color = occupied ? Color.white : new Color(1f, 1f, 1f, 0.28f);
            icon.transform.SetAsLastSibling();
        }

        if (button != null)
            button.interactable = preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress;
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

    Sprite LoadPilotPortraitSprite(PilotDefinition definition)
    {
        if (definition == null)
            definition = PilotCatalog.GetDefinition(PilotCatalog.JakeId);

        return LoadSpriteFromResourcesOrEditor(
            definition.PortraitResourcePath,
            definition.PortraitEditorResourcePath,
            definition.PortraitEditorFallbackPath);
    }

    Sprite GetGrayscalePilotPortraitSprite(PilotDefinition definition, Sprite source)
    {
        if (definition == null || source == null)
            return source;

        if (grayscalePilotPortraitCache.TryGetValue(definition.Id, out Sprite cached) && cached != null)
            return cached;

        Sprite grayscale = CreateGrayscaleSprite(source, definition.Id + "_locked");
        if (grayscale != null)
            grayscalePilotPortraitCache[definition.Id] = grayscale;

        return grayscale != null ? grayscale : source;
    }

    Sprite CreateGrayscaleSprite(Sprite source, string spriteName)
    {
        if (source == null || source.texture == null)
            return null;

        Rect rect = source.rect;
        RenderTexture previous = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(source.texture.width, source.texture.height, 0, RenderTextureFormat.ARGB32);

        try
        {
            Graphics.Blit(source.texture, renderTexture);
            RenderTexture.active = renderTexture;

            Texture2D readable = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), TextureFormat.RGBA32, false);
            readable.ReadPixels(rect, 0, 0);
            readable.Apply();

            Color[] pixels = readable.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float gray = (pixels[i].r * 0.299f) + (pixels[i].g * 0.587f) + (pixels[i].b * 0.114f);
                pixels[i] = new Color(gray, gray, gray, pixels[i].a);
            }

            readable.SetPixels(pixels);
            readable.Apply();
            readable.name = spriteName;
            return Sprite.Create(readable, new Rect(0f, 0f, readable.width, readable.height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to create grayscale pilot portrait: " + ex.Message);
            return null;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    void PrewarmProfileAssets()
    {
        UIRuntimeStyler.PrewarmRuntimeSprites();
        InventoryItemCatalog.PrewarmIcons();

        LoadStandaloneSprite("STAR_RAIDERS_screen.png");
        LoadStandaloneSprite("hangar1_2D.png");
        LoadStandaloneSprite("hangar1_2D_przesuniety.png");
        LoadStandaloneSprite("PROJECTS_SCREEN.png");
        LoadStandaloneSprite("SUPPLY_TO_SURVIVE_PROJECT.png");
        LoadStandaloneSprite("SPACE_MAYHEM.png");
        LoadStandaloneSprite("omerta_screen.png");

        for (int skinIndex = 0; skinIndex <= ShipCatalog.MaxShipSkinIndex; skinIndex++)
            LoadShipPreviewSprite(skinIndex);

        for (int i = 0; i < PilotCatalog.AllDefinitions.Count; i++)
        {
            PilotDefinition definition = PilotCatalog.AllDefinitions[i];
            Sprite portrait = LoadPilotPortraitSprite(definition);
            GetGrayscalePilotPortraitSprite(definition, portrait);
        }

        for (int i = 0; i < TraderDefinitions.Length; i++)
            LoadTraderPortraitSprite(TraderDefinitions[i]);

        IReadOnlyList<ProjectDefinition> projects = ProjectCatalog.AllProjects;
        for (int i = 0; i < projects.Count; i++)
        {
            ProjectDefinition project = projects[i];
            if (project == null)
                continue;

            LoadSpriteFromResources(project.TileResourcePath);
            if (!string.IsNullOrWhiteSpace(project.BackgroundResourcePath))
                LoadSpriteFromResources(project.BackgroundResourcePath);
        }

        LoadSpriteFromResources("UI/icon_astrons_coin");
    }

    Sprite LoadStandaloneSprite(string fileName)
    {
        string resourcesPath = fileName switch
        {
            "STAR_RAIDERS_screen.png" => "STAR_RAIDERS_screen",
            "hangar1_2D.png" => "UI/hangar1_2D_profile",
            "hangar1_2D_przesuniety.png" => "UI/hangar1_2D_przesuniety_profile",
            "ship1.png" => "Visuals/Ships/ship1_resource",
            "ship2.png" => "Visuals/Ships/ship2_resource",
            "ship3.png" => "Visuals/Ships/ship3_resource",
            "ship4.png" => "ship4_resource",
            "PROJECTS_SCREEN.png" => "PROJECTS_SCREEN",
            "SUPPLY_TO_SURVIVE_PROJECT.png" => "SUPPLY_TO_SURVIVE_PROJECT",
            "SPACE_MAYHEM.png" => "SPACE_MAYHEM",
            "omerta_screen.png" => "omerta_screen",
            _ => null
        };

        string editorResourcePath = fileName switch
        {
            "STAR_RAIDERS_screen.png" => "Assets/Resources/STAR_RAIDERS_screen.png",
            "hangar1_2D.png" => "Assets/Resources/UI/hangar1_2D_profile.png",
            "hangar1_2D_przesuniety.png" => "Assets/Resources/UI/hangar1_2D_przesuniety_profile.png",
            "ship1.png" => "Assets/Resources/Visuals/Ships/ship1_resource.png",
            "ship2.png" => "Assets/Resources/Visuals/Ships/ship2_resource.png",
            "ship3.png" => "Assets/Resources/Visuals/Ships/ship3_resource.png",
            "ship4.png" => "Assets/Resources/ship4_resource.png",
            "PROJECTS_SCREEN.png" => "Assets/Resources/PROJECTS_SCREEN.png",
            "SUPPLY_TO_SURVIVE_PROJECT.png" => "Assets/Resources/SUPPLY_TO_SURVIVE_PROJECT.png",
            "SPACE_MAYHEM.png" => "Assets/Resources/SPACE_MAYHEM.png",
            "omerta_screen.png" => "Assets/Resources/omerta_screen.png",
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
        {
            CacheLoadedSprite(resourcesPath, sprite);
            return sprite;
        }

        if (!string.IsNullOrWhiteSpace(editorFallbackPath))
        {
            sprite = LoadEditorSprite(editorFallbackPath);
            CacheLoadedSprite(resourcesPath, sprite);
            return sprite;
        }
#endif

        return null;
    }

    Sprite LoadSpriteFromResources(string resourcesPath)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath))
            return null;

        if (spriteCacheByResourcePath.TryGetValue(resourcesPath, out Sprite cachedSprite) && cachedSprite != null)
            return cachedSprite;
        if (missingSpriteResources.Contains(resourcesPath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
        {
            spriteCacheByResourcePath[resourcesPath] = sprite;
            return sprite;
        }

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcesPath);
        sprite = GetLargestSprite(sprites);
        if (sprite != null)
        {
            spriteCacheByResourcePath[resourcesPath] = sprite;
            return sprite;
        }

        Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
        if (texture == null)
        {
            missingSpriteResources.Add(resourcesPath);
            return null;
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
        sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        spriteCacheByResourcePath[resourcesPath] = sprite;
        return sprite;
    }

    void CacheLoadedSprite(string resourcesPath, Sprite sprite)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath) || sprite == null)
            return;

        spriteCacheByResourcePath[resourcesPath] = sprite;
        missingSpriteResources.Remove(resourcesPath);
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

    bool RefreshVisibility()
    {
        if (panelObject == null)
            return false;

        bool show = !PhotonNetwork.InRoom;
        bool changed = !profilePanelVisibilityInitialized || profilePanelVisible != show;
        profilePanelVisibilityInitialized = true;
        profilePanelVisible = show;

        if (!show)
        {
            if (panelObject.activeSelf)
                panelObject.SetActive(false);

            if (changed)
            {
                SetGameplayHudVisible(true);
                if (splashScreenObject != null)
                    splashScreenObject.SetActive(false);
                HideShipImageModal();
                HideCraftingRecipeBrowser();
                HideShopBrowser();
                HideShipInventoryStartConfirm();
                HideItemInfoOverlay();
            }

            return false;
        }

        if (!panelObject.activeSelf)
            panelObject.SetActive(true);

        if (changed)
        {
            SetGameplayHudVisible(false);
            MarkAllProfileUiDirty();
        }

        bool splashShowing = IsSplashShowing();
        if (splashScreenObject != null)
        {
            splashScreenObject.SetActive(splashShowing);
            if (splashShowing)
                splashScreenObject.transform.SetAsLastSibling();
        }

        bool browserVisible = SessionBrowserPanelUI.IsVisible;
        ApplyInteractableIfChanged(!NetworkManager.SessionRequested || !browserVisible);
        if (statusText != null &&
            (statusText.text == "Connecting..." || statusText.text == "Loading active rounds...") &&
            !NetworkManager.SessionRequested)
        {
            statusText.text = string.Empty;
        }

        return true;
    }

    bool IsCraftingRecipeUnlocked(PlayerProfileCraftingRecipe recipe)
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        return PlayerProfileCraftingCatalog.IsRecipeUnlocked(
            recipe,
            profile != null ? profile.UnlockedBlueprintIds : null);
    }

    void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = value;
            EnsureStatusTextLayering();
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
        {
            int addedSlots = PlayerInventoryData.PlayerSlotExtensionSize;
            string slotLabel = addedSlots == 1 ? " slot" : " slots";
            playerInventoryExtendConfirmText.text = "Do you want to extend player inventory by " + addedSlots + slotLabel + " for " + price + " Astrons?";
        }

        if (playerInventoryExtendConfirmObject != null)
        {
            playerInventoryExtendConfirmObject.SetActive(true);
            EnsureProfileModalLayering();
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
            {
                SetStatus("Player inventory extended by " + PlayerInventoryData.PlayerSlotExtensionSize + " slots.");
                AudioManager.Instance?.PlayCash();
            }
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

    void ApplyInteractableIfChanged(bool interactable)
    {
        if (interactableStateInitialized &&
            lastAppliedInteractable == interactable &&
            lastAppliedInventoryActionInProgress == inventoryActionInProgress &&
            lastAppliedPreserveInventoryButtonVisualsDuringSave == preserveInventoryButtonVisualsDuringSave)
        {
            return;
        }

        SetInteractable(interactable);
    }

    void InvalidateInteractableState()
    {
        interactableStateInitialized = false;
    }

    void RememberInteractableState(bool interactable)
    {
        interactableStateInitialized = true;
        lastAppliedInteractable = interactable;
        lastAppliedInventoryActionInProgress = inventoryActionInProgress;
        lastAppliedPreserveInventoryButtonVisualsDuringSave = preserveInventoryButtonVisualsDuringSave;
    }

    void SetInteractable(bool interactable)
    {
        if (nicknameInput != null)
            nicknameInput.interactable = interactable;

        if (saveAndRunButton != null)
            saveAndRunButton.interactable = interactable;

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

        if (skinButtons != null)
        {
            for (int i = 0; i < skinButtons.Length; i++)
            {
                if (skinButtons[i] != null)
                    skinButtons[i].interactable = interactable;
            }
        }

        if (shipSelectionBackButton != null)
            shipSelectionBackButton.interactable = interactable;
        if (shipSelectionPrevButton != null)
            shipSelectionPrevButton.interactable = interactable;
        if (shipSelectionNextButton != null)
            shipSelectionNextButton.interactable = interactable;
        if (shipSelectionDetailsButton != null)
            shipSelectionDetailsButton.interactable = interactable && shipSelectionDetailsButton.gameObject.activeSelf;
        if (shipSelectionSkinButtons != null)
        {
            for (int i = 0; i < shipSelectionSkinButtons.Length; i++)
            {
                if (shipSelectionSkinButtons[i] != null)
                    shipSelectionSkinButtons[i].interactable = interactable;
            }
        }

        if (pilotPortraitButton != null)
            pilotPortraitButton.interactable = interactable;
        if (projectsButton != null)
            projectsButton.interactable = interactable;
        SetProjectButtonsInteractable(interactable && !inventoryActionInProgress);
        if (pilotSelectionBackButton != null)
            pilotSelectionBackButton.interactable = interactable;
        if (pilotSelectionPrevButton != null)
            pilotSelectionPrevButton.interactable = interactable;
        if (pilotSelectionNextButton != null)
            pilotSelectionNextButton.interactable = interactable;

        SetInventoryInteractable(interactable && !inventoryActionInProgress);
        RememberInteractableState(interactable);
    }

    void SetProjectButtonsInteractable(bool interactable)
    {
        for (int i = 0; i < projectTileButtons.Count; i++)
        {
            if (projectTileButtons[i] != null)
                projectTileButtons[i].interactable = interactable;
        }

        for (int i = 0; i < projectStageTabButtons.Count; i++)
        {
            if (projectStageTabButtons[i] != null)
                projectStageTabButtons[i].interactable = interactable;
        }

        for (int i = 0; i < projectStepButtons.Count; i++)
        {
            if (projectStepButtons[i] != null)
                projectStepButtons[i].interactable = interactable;
        }

        if (projectCommitMinusButton != null)
            projectCommitMinusButton.interactable = interactable;
        if (projectCommitPlusButton != null)
            projectCommitPlusButton.interactable = interactable;
        if (projectCommitButton != null)
            projectCommitButton.interactable = interactable;
        if (projectRewardClaimButton != null)
            projectRewardClaimButton.interactable = interactable;
    }

    void SetInventoryInteractable(bool interactable)
    {
        inventoryControlsInteractable = interactable;
        bool visualInteractable = interactable || preserveInventoryButtonVisualsDuringSave;
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
        if (craftingRecipeAvailabilityButton != null)
            craftingRecipeAvailabilityButton.interactable = visualInteractable;
        if (craftingRecipeBlueprintsButton != null)
            craftingRecipeBlueprintsButton.interactable = visualInteractable;
        if (craftingBlueprintCloseButton != null)
            craftingBlueprintCloseButton.interactable = interactable;
        SetCraftingRecipeRowsInteractable(interactable);
        if (itemPreviewSellButton != null)
            itemPreviewSellButton.interactable = visualInteractable;
        if (itemPreviewSalvageButton != null)
            itemPreviewSalvageButton.interactable = visualInteractable;
        if (itemPreviewInfoButton != null)
            itemPreviewInfoButton.interactable = visualInteractable;
        if (itemInfoCloseButton != null)
            itemInfoCloseButton.interactable = interactable;
        if (craftButton != null)
            craftButton.interactable = visualInteractable;
        if (clearCraftButton != null)
            clearCraftButton.interactable = visualInteractable;
        if (playerInventoryExtendButton != null)
            playerInventoryExtendButton.interactable = visualInteractable;
        if (playerInventoryFilterButton != null)
            playerInventoryFilterButton.interactable = visualInteractable;
        if (playerInventorySortButton != null)
            playerInventorySortButton.interactable = visualInteractable;
        if (shipInventoryUnloadButton != null)
            shipInventoryUnloadButton.interactable = visualInteractable;
        if (playerInventoryExtendConfirmButton != null)
            playerInventoryExtendConfirmButton.interactable = interactable;
        if (playerInventoryExtendCancelButton != null)
            playerInventoryExtendCancelButton.interactable = interactable;
        if (shipInventoryStartConfirmYesButton != null)
            shipInventoryStartConfirmYesButton.interactable = interactable;
        if (shipInventoryStartConfirmNoButton != null)
            shipInventoryStartConfirmNoButton.interactable = interactable;
        if (shopCloseButton != null)
            shopCloseButton.interactable = interactable;
        RefreshShopSortButton();
        RefreshTraderSelectionVisuals();
    }

    void SetInventoryButtonState(Button[] buttons, bool interactable)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].interactable = interactable || preserveInventoryButtonVisualsDuringSave;
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
            if (currentScreen == ProfileScreen.Home && IsEquipmentSlotEnabledForSelectedSkin(slotIndex))
            {
                HideItemPreview();
                SetStatus(string.Empty);
                SwitchToScreen(ProfileScreen.Inventory);
                return;
            }

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
        if (!ResolveDropTarget(eventData, out ProfileItemSource targetSource, out int targetIndex))
            return;

        if (resolvedSourceIndex < 0 || targetIndex < 0)
        {
            if (targetSource != ProfileItemSource.None)
                SetStatus(GetMoveFailureMessage(targetSource));
            return;
        }

        await CompleteInventoryMoveAsync(source, resolvedSourceIndex, targetSource, targetIndex, "Inventory move failed: ");
    }

    public async void EndCraftingSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (!dragInProgress)
            return;

        dragInProgress = false;
        if (dragVisualObject != null)
            dragVisualObject.SetActive(false);

        if (!ResolveDropTarget(eventData, out ProfileItemSource targetSource, out int targetIndex))
            return;

        if (targetIndex < 0)
        {
            SetStatus(GetMoveFailureMessage(targetSource));
            return;
        }

        await CompleteInventoryMoveAsync(ProfileItemSource.CraftingSlot, slotIndex, targetSource, targetIndex, "Crafting move failed: ");
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
            ShowItemPreview(ProfileItemSource.EquipmentSlot, slotIndex, itemId);
            if (ApplyEquipmentSlotPlayerInventoryFilter(slotIndex))
                return;

            SetStatus("Equipment item selected.");
        }
        else
        {
            HideItemPreview();
            if (ApplyEquipmentSlotPlayerInventoryFilter(slotIndex))
                return;

            SetStatus(string.Empty);
        }
    }

    bool ApplyEquipmentSlotPlayerInventoryFilter(int slotIndex)
    {
        if (!IsEquipmentSlotEnabledForSelectedSkin(slotIndex))
            return false;

        SetPlayerInventoryFilter(PlayerInventoryFilterMode.CustomEquipmentSlot, slotIndex);
        resetPlayerInventoryScrollOnNextRefresh = true;
        InventoryItemCategory category = InventoryItemCatalog.GetEquipmentSlotCategory(slotIndex);
        SetStatus("Showing " + FormatInventoryFilterCategory(category) + " items.");

        if (currentScreen != ProfileScreen.Inventory)
        {
            SwitchToScreen(ProfileScreen.Inventory, false);
            RefreshView();
            return true;
        }

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile != null)
            RefreshInventoryView(profile.Inventory);

        return true;
    }

    string FormatInventoryFilterCategory(InventoryItemCategory category)
    {
        return category switch
        {
            InventoryItemCategory.Weapon => "weapon",
            InventoryItemCategory.Shield => "shield",
            InventoryItemCategory.Engine => "engine",
            InventoryItemCategory.Gadget => "gadget",
            InventoryItemCategory.Support => "support",
            InventoryItemCategory.Rescue => "rescue",
            _ => "compatible"
        };
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

        if (!ResolveDropTarget(eventData, out ProfileItemSource targetSource, out int targetIndex))
            return;

        if (targetIndex < 0)
        {
            SetStatus(GetMoveFailureMessage(targetSource));
            return;
        }

        await CompleteInventoryMoveAsync(ProfileItemSource.EquipmentSlot, slotIndex, targetSource, targetIndex, "Equipment move failed: ");
    }

    async Task CompleteInventoryMoveAsync(ProfileItemSource source, int sourceIndex, ProfileItemSource targetSource, int targetIndex, string errorPrefix)
    {
        inventoryActionInProgress = true;
        preserveInventoryButtonVisualsDuringSave = true;

        try
        {
            bool moved = await MoveItemToTargetAsync(source, sourceIndex, targetSource, targetIndex);
            SetStatus(moved ? GetMoveSuccessMessage(targetSource) : GetMoveFailureMessage(targetSource));
        }
        catch (Exception ex)
        {
            Debug.LogError(errorPrefix + ex);
            SetStatus("Inventory update failed.");
            RefreshProfileSummaryAndInventory();
        }
        finally
        {
            preserveInventoryButtonVisualsDuringSave = false;
            inventoryActionInProgress = false;
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
        bool isBlueprint = InventoryItemCatalog.IsBlueprintItem(itemId);
        bool blueprintUnlocked = isBlueprint && PlayerProfileService.Instance.IsBlueprintUnlocked(itemId);
        if (itemPreviewInfoButton != null)
            itemPreviewInfoButton.gameObject.SetActive(true);
        if (itemPreviewSellButton != null)
            itemPreviewSellButton.gameObject.SetActive(supportsInventoryActions);
        if (itemPreviewSalvageButton != null)
        {
            SetButtonLabel(itemPreviewSalvageButton, isBlueprint && !blueprintUnlocked ? "USE" : "SALVAGE");
            itemPreviewSalvageButton.gameObject.SetActive(supportsInventoryActions);
        }
    }

    void HideItemPreview()
    {
        if (itemPreviewPanelObject != null)
            itemPreviewPanelObject.SetActive(false);

        HideItemInfoOverlay();
        previewSource = ProfileItemSource.None;
        previewSlotIndex = -1;
        previewItemId = null;
    }

    void OnItemPreviewInfoClicked()
    {
        if (string.IsNullOrWhiteSpace(previewItemId))
            return;

        ShowItemInfoOverlay(previewItemId);
    }

    void ShowItemInfoOverlay(string itemId)
    {
        if (itemInfoOverlayObject == null || string.IsNullOrWhiteSpace(itemId))
            return;

        RefreshItemInfoOverlay(itemId);
        itemInfoOverlayObject.SetActive(true);
        EnsureProfileModalLayering();
    }

    void HideItemInfoOverlay()
    {
        if (itemInfoOverlayObject != null)
            itemInfoOverlayObject.SetActive(false);
    }

    void RefreshItemInfoOverlay(string itemId)
    {
        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        string displayName = definition != null ? definition.DisplayName : InventoryItemCatalog.GetDisplayName(itemId);

        if (itemInfoTitleText != null)
            itemInfoTitleText.text = displayName.ToUpperInvariant();

        if (itemInfoTypeText != null)
            itemInfoTypeText.text = InventoryItemCatalog.GetCategoryLabel(itemId) + "  |  " + InventoryItemCatalog.GetRarity(itemId).ToString();

        if (itemInfoIcon != null)
        {
            itemInfoIcon.sprite = InventoryItemCatalog.GetIcon(itemId);
            itemInfoIcon.enabled = itemInfoIcon.sprite != null;
        }

        if (itemInfoPriceText != null)
            itemInfoPriceText.text = BuildItemInfoPriceText(itemId, definition);

        if (itemInfoSalvageText != null)
        {
            if (InventoryItemCatalog.IsBlueprintItem(itemId))
            {
                string targetItemId = InventoryItemCatalog.GetBlueprintTargetItemId(itemId);
                bool unlocked = PlayerProfileService.Instance.IsBlueprintUnlocked(itemId);
                itemInfoSalvageText.text = "BLUEPRINT\n" + (unlocked
                    ? "Salvage into " + InventoryItemCatalog.GetDisplayName(InventoryItemCatalog.BlueprintScrapId) + "."
                    : "Use to unlock crafting for " + InventoryItemCatalog.GetDisplayName(targetItemId) + ".");
            }
            else
            {
                itemInfoSalvageText.text = "SALVAGE\n" + (InventoryItemCatalog.HasRandomSalvageOutputs(itemId)
                    ? InventoryItemCatalog.GetRandomSalvageDescription(itemId)
                    : FormatItemIdList(InventoryItemCatalog.GetSalvageOutputs(itemId), "No salvage output."));
            }
        }

        if (itemInfoRecipeText != null)
            itemInfoRecipeText.text = "RECIPE\n" + BuildItemInfoRecipeText(itemId);

        if (itemInfoDescriptionText != null)
            itemInfoDescriptionText.text = "DESCRIPTION\n" + BuildItemInfoDescription(itemId, definition);
    }

    string BuildItemInfoPriceText(string itemId, InventoryItemDefinition definition)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("Sell value: ");
        builder.Append(InventoryItemCatalog.GetSellValueAstrons(itemId));
        builder.Append(" Astrons");

        if (definition != null && definition.ItemType == InventoryItemType.Equipment)
        {
            int traderPrice = PlayerProfileService.HasInstance
                ? PlayerProfileService.Instance.GetShopBuyPriceAstrons(itemId)
                : InventoryItemCatalog.GetShopBuyValueAstrons(itemId);
            if (traderPrice > 0)
            {
                builder.Append('\n');
                builder.Append("Trader price: ");
                builder.Append(traderPrice);
                builder.Append(" Astrons");
            }
        }

        return builder.ToString();
    }

    string BuildItemInfoRecipeText(string itemId)
    {
        PlayerProfileCraftingRecipe recipe = PlayerProfileCraftingCatalog.GetRecipeForOutput(itemId);
        if (recipe == null || recipe.Inputs == null || recipe.Inputs.Length == 0)
            return "No crafting recipe.";

        StringBuilder builder = new StringBuilder();
        builder.Append(FormatItemIdList(recipe.Inputs, "No ingredients."));
        int outputCount = Mathf.Max(1, recipe.OutputCount);
        if (outputCount > 1)
        {
            builder.Append('\n');
            builder.Append("Output: ");
            builder.Append(outputCount);
            builder.Append("x ");
            builder.Append(InventoryItemCatalog.GetDisplayName(itemId));
        }

        return builder.ToString();
    }

    string FormatItemIdList(string[] itemIds, string emptyText)
    {
        if (itemIds == null || itemIds.Length == 0)
            return emptyText;

        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < itemIds.Length; i++)
        {
            string itemId = itemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            counts.TryGetValue(itemId, out int count);
            counts[itemId] = count + 1;
        }

        if (counts.Count == 0)
            return emptyText;

        StringBuilder builder = new StringBuilder();
        foreach (KeyValuePair<string, int> entry in counts)
        {
            if (builder.Length > 0)
                builder.Append('\n');

            if (entry.Value > 1)
            {
                builder.Append(entry.Value);
                builder.Append("x ");
            }

            builder.Append(InventoryItemCatalog.GetDisplayName(entry.Key));
        }

        return builder.ToString();
    }

    string BuildItemInfoDescription(string itemId, InventoryItemDefinition definition)
    {
        if (definition != null && definition.ItemType == InventoryItemType.Equipment)
            return AppendWeaponClassificationDescription(itemId, GetEquipmentGameplayDescription(itemId, definition.Category));

        string description = definition != null ? definition.Description : InventoryItemCatalog.GetDescription(itemId);
        return string.IsNullOrWhiteSpace(description) ? "No additional description." : description;
    }

    string AppendWeaponClassificationDescription(string itemId, string description)
    {
        string classification = WeaponAttackCatalog.BuildEquipmentClassificationSummary(itemId);
        if (string.IsNullOrWhiteSpace(classification))
            return description;

        if (string.IsNullOrWhiteSpace(description))
            return classification;

        return description + "\n\n" + classification;
    }

    string GetEquipmentGameplayDescription(string itemId, InventoryItemCategory category)
    {
        switch (itemId)
        {
            case InventoryItemCatalog.PlasmaGunId:
                return "A reliable combat cannon for direct pressure on hostile ships.";
            case InventoryItemCatalog.TripleGunId:
                return "A simple spread weapon that makes close and medium range aiming more forgiving.";
            case InventoryItemCatalog.GatlingGunId:
                return "A rotary burst weapon that rewards holding aim on one target through a full stream of tiny rounds.";
            case InventoryItemCatalog.ArtilleryGunId:
                return "A heavy launcher for hitting an area instead of a single precise target.";
            case InventoryItemCatalog.RocketLauncherId:
                return "An explosive launcher that can lock onto a target before firing a homing rocket.";
            case InventoryItemCatalog.DoubleRocketLauncherId:
                return "A twin launcher that fires paired rockets after the pilot locks a target.";
            case InventoryItemCatalog.RailGunId:
                return "A precision weapon for fast, piercing shots across open space.";
            case InventoryItemCatalog.DoubleIonizerId:
                return "A shield-focused weapon that pressures protected enemies with paired energy shots.";
            case InventoryItemCatalog.AstroCutterId:
                return "A cutting beam that helps carve through tough targets and space rock.";
            case InventoryItemCatalog.PulseDisruptorId:
                return "A slow shield disruptor that needs a short arming distance and releases a defensive EMP wave as its super.";
            case InventoryItemCatalog.PowerEngineId:
                return "A direct engine upgrade for better cruise speed and stronger booster top speed.";
            case InventoryItemCatalog.IonEngineId:
                return "A responsive engine that favors quick booster recovery and sharper turning.";
            case InventoryItemCatalog.FusionEngineId:
                return "A high-output engine that adds strong speed and faster booster recovery.";
            case InventoryItemCatalog.HybridEngineId:
                return "A balanced engine that blends speed, longer boost endurance, and quicker recovery.";
            case InventoryItemCatalog.DoubleEngineId:
                return "A twin-drive engine for extreme speed and boost output, with shorter boost endurance and heavier handling.";
            case InventoryItemCatalog.FuelTankId:
                return "An engine module for longer booster use during travel, chase, or escape.";
            case InventoryItemCatalog.SuperBoosterId:
                return "A burst engine system for sudden aggressive movement or emergency disengage.";
            case InventoryItemCatalog.AfterburnerStabilizerId:
                return "An engine stabilizer that sharpens turning and halves the delay before booster recovery starts.";
            case InventoryItemCatalog.BlackMarketThrusterId:
                return "An illegal thruster overdrive that adds strong speed and boost output, but reduces shield capacity and makes turning heavier.";
            case InventoryItemCatalog.GadgetMineId:
                return "A deployable trap for protecting an area or punishing pursuing enemies.";
            case InventoryItemCatalog.BatteryId:
                return "A support gadget that helps rebuild shields when the ship needs breathing room.";
            case InventoryItemCatalog.MagneticBeamId:
                return "A utility projector that pulls nearby resources toward the ship.";
            case InventoryItemCatalog.TractorBeamId:
                return "A focused beam for towing one collectible object while the ship keeps moving.";
            case InventoryItemCatalog.LootHookId:
                return "A short-range pirate hook that steals one cargo item from a nearby enemy ship without destroying it.";
            case InventoryItemCatalog.StasisBuoyId:
                return "A deployable buoy that pulses EMP shocks, heavily slowing enemy ships and delaying their fire rate inside its radius.";
            case InventoryItemCatalog.TetherHarpoonId:
                return "A combat tether that latches onto a nearby enemy ship, drags both ships toward tension range, and repeatedly shocks the target.";
            case InventoryItemCatalog.SpaceTorpedoId:
                return "A fast explosive gadget projectile for direct hits, small area bursts, and light asteroid damage.";
            case InventoryItemCatalog.BioTrapId:
                return "A capture net for hostile astronauts that converts one target into a valuable captive pod loot item.";
            case InventoryItemCatalog.AsteroidBreacherBombId:
                return "A breaching charge that detonates the nearest asteroid obstacle and damages ships caught around the blast.";
            case InventoryItemCatalog.LureBeaconId:
                return "A decoy gadget that draws enemy attention away from the pilot.";
            case InventoryItemCatalog.AutoTurretId:
                return "A deployable turret that supports the pilot by firing at nearby enemies.";
            case InventoryItemCatalog.RocketAutoTurretId:
                return "A deployable rocket turret that locks down an area with straight explosive shots.";
            case InventoryItemCatalog.GuidanceSystemId:
                return "A support system that points the pilot toward useful objectives and threats.";
            case InventoryItemCatalog.CloakDeviceId:
                return "A stealth gadget that hides the ship for a short time, but breaks immediately when the pilot fires.";
            case InventoryItemCatalog.LootingFriendId:
                return "A support drone that helps collect nearby loot while the pilot focuses on flying.";
            case InventoryItemCatalog.FiringFriendId:
                return "A support drone that follows the ship and fires short-range laser bursts at nearby enemies.";
            case InventoryItemCatalog.SpaceDrillId:
                return "A mining drone that extracts loot from a nearby asteroid and brings it back.";
            case InventoryItemCatalog.SpaceTrapId:
                return "A sabotage kit that turns a loot object into a dangerous surprise.";
            case InventoryItemCatalog.OverclockedMagazineId:
                return "An illegal ammo overclock that increases weapon capacity, but disables shields and lengthens reload downtime.";
            case InventoryItemCatalog.EmergencySuitBeaconId:
                return "A rescue beacon that gives the astronaut brief protection and a permanent speed boost after losing the ship.";
            case InventoryItemCatalog.EscapePodId:
                return "A rescue capsule that replaces the astronaut after losing the ship.";
            case InventoryItemCatalog.SalvageMagnetArrayId:
                return "A salvage aid that makes wreck loot and random salvage easier to collect.";
            case InventoryItemCatalog.ShieldReactorId:
                return "A defensive reactor that strengthens the ship's protective shield layer.";
            case InventoryItemCatalog.KineticDampenerId:
                return "A defensive module that halves kinetic and contact damage while softening explosive shocks.";
            case InventoryItemCatalog.PhaseShieldId:
                return "A last-moment defensive failsafe that gives the pilot a brief chance to recover.";
            case InventoryItemCatalog.CargoBayExtensionId:
                return "A cargo module installed in a shield slot to carry more ship inventory.";
            case InventoryItemCatalog.StrongPlatingId:
                return "A hull protection module for safer travel through dangerous environmental effects.";
            case InventoryItemCatalog.ShieldCapacitorId:
                return "A compact shield capacitor that adds a large protective energy reserve.";
            case InventoryItemCatalog.AegisBatteryId:
                return "A shield battery bank that makes Battery gadgets rebuild shields much faster.";
            case InventoryItemCatalog.RegenerativeShieldMatrixId:
                return "A regenerative shield module that slowly restores active shields after the ship avoids damage.";
            case InventoryItemCatalog.BulwarkProjectorId:
                return "A heavy shield projector that shrugs off laser and autocannon fire.";
            case InventoryItemCatalog.AlienAegisCoreId:
                return "An alien shield core that briefly hardens the ship after its shield collapses.";
        }

        return category switch
        {
            InventoryItemCategory.Weapon => "A weapon module that changes how the ship attacks enemies.",
            InventoryItemCategory.Engine => "An engine module that changes how the ship moves and escapes.",
            InventoryItemCategory.Shield => "A defensive module that changes how the ship survives damage.",
            InventoryItemCategory.Gadget => "A utility gadget that adds a special tactical action.",
            InventoryItemCategory.Support => "A support module that adds a tactical action or automatic helper system.",
            InventoryItemCategory.Rescue => "A rescue module that improves survival after the ship is lost.",
            _ => "An equipment module that changes the ship's capabilities."
        };
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

    bool ResolveDropTarget(PointerEventData eventData, out ProfileItemSource targetSource, out int targetIndex)
    {
        targetSource = ProfileItemSource.None;
        targetIndex = -1;

        GameObject hoveredObject = eventData != null ? eventData.pointerEnter : null;
        Transform current = hoveredObject != null ? hoveredObject.transform : null;
        while (current != null)
        {
            ProfileInventorySlotDragHandler slot = current.GetComponent<ProfileInventorySlotDragHandler>();
            if (slot != null)
            {
                targetSource = ProfileItemSourceFromInventory(slot.isPlayerInventory);
                targetIndex = targetSource == ProfileItemSource.PlayerInventory
                    ? ResolveVisiblePlayerInventorySlotIndex(slot.slotIndex)
                    : slot.slotIndex;
                if (targetIndex >= 0)
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

        if (TryResolveDropTargetFromScreenPosition(eventData, out targetSource, out targetIndex))
            return true;

        return false;
    }

    bool TryResolveDropTargetFromScreenPosition(PointerEventData eventData, out ProfileItemSource targetSource, out int targetIndex)
    {
        targetSource = ProfileItemSource.None;
        targetIndex = -1;

        if (eventData == null)
            return false;

        Camera eventCamera = eventData.pressEventCamera;
        Vector2 screenPosition = eventData.position;

        if (TryResolveInventoryButtonDrop(playerInventoryButtons, true, screenPosition, eventCamera, out targetSource, out targetIndex))
            return true;

        if (TryResolveInventoryButtonDrop(shipInventoryButtons, false, screenPosition, eventCamera, out targetSource, out targetIndex))
            return true;

        if (TryResolveIndexedButtonDrop(equipmentSlotButtons, ProfileItemSource.EquipmentSlot, screenPosition, eventCamera, out targetSource, out targetIndex))
            return true;

        if (TryResolveIndexedButtonDrop(craftingSlotButtons, ProfileItemSource.CraftingSlot, screenPosition, eventCamera, out targetSource, out targetIndex))
            return true;

        RectTransform craftingPanelRect = craftingPanelObject != null
            ? craftingPanelObject.GetComponent<RectTransform>()
            : null;
        if (craftingPanelRect != null && RectTransformUtility.RectangleContainsScreenPoint(craftingPanelRect, screenPosition, eventCamera))
        {
            targetSource = ProfileItemSource.CraftingSlot;
            targetIndex = FindFirstFreeCraftingSlot();
            return targetIndex >= 0;
        }

        RectTransform playerViewportRect = playerInventoryScrollRect != null
            ? playerInventoryScrollRect.GetComponent<RectTransform>()
            : null;
        if (playerViewportRect != null && RectTransformUtility.RectangleContainsScreenPoint(playerViewportRect, screenPosition, eventCamera))
        {
            targetSource = ProfileItemSource.PlayerInventory;
            targetIndex = FindFirstFreePlayerInventorySlot();
            return true;
        }

        return false;
    }

    bool TryResolveInventoryButtonDrop(Button[] buttons, bool isPlayerInventory, Vector2 screenPosition, Camera eventCamera, out ProfileItemSource targetSource, out int targetIndex)
    {
        targetSource = ProfileItemSource.None;
        targetIndex = -1;

        if (buttons == null)
            return false;

        int shipCapacity = GetActiveShipInventoryCapacity();
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !button.gameObject.activeInHierarchy)
                continue;

            if (!isPlayerInventory && i >= shipCapacity)
                continue;

            RectTransform rect = button.GetComponent<RectTransform>();
            if (!ExpandedRectangleContainsScreenPoint(rect, screenPosition, eventCamera, InventoryDropTargetPadding))
                continue;

            targetSource = ProfileItemSourceFromInventory(isPlayerInventory);
            targetIndex = isPlayerInventory ? ResolveVisiblePlayerInventorySlotIndex(i) : i;
            if (targetIndex >= 0)
                return true;
        }

        return false;
    }

    bool TryResolveIndexedButtonDrop(Button[] buttons, ProfileItemSource source, Vector2 screenPosition, Camera eventCamera, out ProfileItemSource targetSource, out int targetIndex)
    {
        targetSource = ProfileItemSource.None;
        targetIndex = -1;

        if (buttons == null)
            return false;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !button.gameObject.activeInHierarchy)
                continue;

            RectTransform rect = button.GetComponent<RectTransform>();
            if (!ExpandedRectangleContainsScreenPoint(rect, screenPosition, eventCamera, InventoryDropTargetPadding))
                continue;

            targetSource = source;
            targetIndex = i;
            return true;
        }

        return false;
    }

    bool ExpandedRectangleContainsScreenPoint(RectTransform rect, Vector2 screenPosition, Camera eventCamera, float padding)
    {
        if (rect == null)
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, eventCamera, out Vector2 localPoint))
            return false;

        Rect localRect = rect.rect;
        localRect.xMin -= padding;
        localRect.xMax += padding;
        localRect.yMin -= padding;
        localRect.yMax += padding;
        return localRect.Contains(localPoint);
    }

    int FindFirstFreePlayerInventorySlot()
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null || profile.Inventory.PlayerSlots == null)
            return -1;

        profile.Inventory.Normalize();
        for (int i = 0; i < profile.Inventory.PlayerSlots.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(profile.Inventory.PlayerSlots[i]))
                return i;
        }

        return -1;
    }

    int FindFirstFreeCraftingSlot()
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null || profile.Inventory == null || profile.Inventory.CraftingSlots == null)
            return -1;

        profile.Inventory.Normalize();
        for (int i = 0; i < profile.Inventory.CraftingSlots.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(profile.Inventory.CraftingSlots[i]))
                return i;
        }

        return -1;
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
            suppressNextProfileChangedRefresh = true;
            int value = InventoryItemCatalog.GetSellValueAstrons(itemId);
            bool sold = await PlayerProfileService.Instance.SellInventoryItemAsync(isShipInventory, previewSlotIndex);
            if (sold)
            {
                SetStatus("Sold for " + value + " Astrons.");
                AudioManager.Instance?.PlayCash();
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
            suppressNextProfileChangedRefresh = false;
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshProfileSummaryAndInventory();
        }
    }

    async void OnItemPreviewSalvageClicked()
    {
        if (inventoryActionInProgress || previewSource == ProfileItemSource.None || previewSource == ProfileItemSource.EquipmentSlot)
            return;

        bool isShipInventory = previewSource == ProfileItemSource.ShipInventory;
        string itemId = previewItemId;
        bool isBlueprint = InventoryItemCatalog.IsBlueprintItem(itemId);
        if (isBlueprint && !PlayerProfileService.Instance.IsBlueprintUnlocked(itemId))
        {
            await UsePreviewedBlueprintAsync(isShipInventory);
            return;
        }

        inventoryActionInProgress = true;
        SetInteractable(false);

        try
        {
            bool salvaged = await PlayerProfileService.Instance.SalvageInventoryItemAsync(isShipInventory, previewSlotIndex);
            if (salvaged)
            {
                SetStatus(isBlueprint
                    ? "Blueprint salvaged into " + InventoryItemCatalog.GetDisplayName(InventoryItemCatalog.BlueprintScrapId) + "."
                    : "Item salvaged.");
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

    async Task UsePreviewedBlueprintAsync(bool isShipInventory)
    {
        string blueprintItemId = previewItemId;
        if (string.IsNullOrWhiteSpace(blueprintItemId))
            return;

        if (PlayerProfileService.Instance.IsBlueprintUnlocked(blueprintItemId))
        {
            SetStatus("Blueprint already learned.");
            HideItemPreview();
            return;
        }

        inventoryActionInProgress = true;
        SetInteractable(false);

        try
        {
            bool used = await PlayerProfileService.Instance.UseBlueprintItemAsync(isShipInventory, previewSlotIndex);
            if (used)
            {
                string targetItemId = InventoryItemCatalog.GetBlueprintTargetItemId(blueprintItemId);
                SetStatus("Blueprint learned: " + InventoryItemCatalog.GetDisplayName(targetItemId) + ".");
                HideItemPreview();
            }
            else
            {
                SetStatus("Blueprint use failed.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Blueprint use failed: " + ex);
            SetStatus("Blueprint use failed.");
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshView();
            RefreshCraftingRecipeBrowser(true);
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

        suppressNextProfileChangedRefresh = true;
        try
        {
            Task saveTask = PlayerProfileService.Instance.SaveInventorySnapshotAsync(workingInventory);
            RefreshProfileSummaryAndInventory();
            await saveTask;
            return true;
        }
        finally
        {
            suppressNextProfileChangedRefresh = false;
        }
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
            ProfileItemSource.EquipmentSlot => IsEquipmentSlotEnabledForSelectedSkin(sourceIndex) ? inventory.RemoveFromEquipment(sourceIndex) : null,
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
            ProfileItemSource.EquipmentSlot => IsEquipmentSlotEnabledForSelectedSkin(sourceIndex) ? GetSlotItem(inventory.EquipmentSlots, sourceIndex) : null,
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
                if (source == ProfileItemSource.EquipmentSlot)
                    return TryPlaceEquipmentItemIntoPlayerInventory(inventory, targetIndex, itemId, sourceIndex);

                return TryPlaceItemIntoIndexedSlot(inventory.PlayerSlots, inventory.PlayerSlots != null ? inventory.PlayerSlots.Length : 0, targetIndex, itemId, inventory, source, sourceIndex);

            case ProfileItemSource.ShipInventory:
                if (!PlayerProfileService.CanStoreItemInShipSlot(itemId, GetActiveProfileShipSkinIndex(), targetIndex))
                    return false;
                return TryPlaceItemIntoIndexedSlot(inventory.ShipSlots, GetActiveShipInventoryCapacity(), targetIndex, itemId, inventory, source, sourceIndex);

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
                if (!IsEquipmentSlotEnabledForSelectedSkin(targetIndex))
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

    bool TryPlaceEquipmentItemIntoPlayerInventory(PlayerInventoryData inventory, int targetIndex, string itemId, int equipmentSlotIndex)
    {
        if (inventory == null ||
            inventory.PlayerSlots == null ||
            targetIndex < 0 ||
            targetIndex >= inventory.PlayerSlots.Length ||
            !IsEquipmentSlotEnabledForSelectedSkin(equipmentSlotIndex))
        {
            return false;
        }

        string targetItem = inventory.PlayerSlots[targetIndex];
        if (string.IsNullOrWhiteSpace(targetItem))
        {
            inventory.PlayerSlots[targetIndex] = itemId;
            return true;
        }

        if (TryReturnItemToSourceSlot(inventory, ProfileItemSource.EquipmentSlot, equipmentSlotIndex, targetItem))
        {
            inventory.PlayerSlots[targetIndex] = itemId;
            return true;
        }

        int fallbackIndex = inventory.GetFirstEmptyPlayerSlot();
        if (fallbackIndex < 0)
            return false;

        inventory.PlayerSlots[fallbackIndex] = itemId;
        return true;
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
                if (sourceIndex < 0 || sourceIndex >= GetActiveShipInventoryCapacity())
                    return false;
                if (!PlayerProfileService.CanStoreItemInShipSlot(itemId, GetActiveProfileShipSkinIndex(), sourceIndex))
                    return false;
                inventory.RestoreShip(sourceIndex, itemId);
                return true;

            case ProfileItemSource.CraftingSlot:
                if (sourceIndex < 0 || sourceIndex >= PlayerInventoryData.CraftingSlotCount)
                    return false;
                inventory.SetCrafting(sourceIndex, itemId);
                return true;

            case ProfileItemSource.EquipmentSlot:
                if (!IsEquipmentSlotEnabledForSelectedSkin(sourceIndex))
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

        if (!PlayerProfileCraftingCatalog.IsRecipeUnlocked(craftResult.Recipe, profile.UnlockedBlueprintIds))
        {
            SetStatus("Blueprint required for " + InventoryItemCatalog.GetDisplayName(craftResult.Recipe.OutputItemId) + ".");
            return;
        }

        inventoryActionInProgress = true;
        preserveInventoryButtonVisualsDuringSave = true;
        SetInteractable(false);

        try
        {
            int outputCount = Mathf.Max(1, craftResult.Recipe.OutputCount);
            if (CountFreePlayerInventorySlots(workingInventory) < outputCount)
            {
                SetStatus("No inventory space for crafted item.");
                return;
            }

            for (int i = 0; i < PlayerInventoryData.CraftingSlotCount; i++)
                workingInventory.SetCrafting(i, null);

            int firstOutputSlot = -1;
            for (int i = 0; i < outputCount; i++)
            {
                int targetSlot = workingInventory.GetFirstEmptyPlayerSlot();
                if (targetSlot < 0)
                {
                    SetStatus("No inventory space for crafted item.");
                    return;
                }

                if (firstOutputSlot < 0)
                    firstOutputSlot = targetSlot;

                workingInventory.PlayerSlots[targetSlot] = craftResult.Recipe.OutputItemId;
            }

            if (!ShouldShowPlayerInventoryItem(craftResult.Recipe.OutputItemId))
            {
                SetPlayerInventoryFilter(PlayerInventoryFilterMode.All, -1);
                resetPlayerInventoryScrollOnNextRefresh = true;
            }

            suppressNextProfileChangedRefresh = true;
            await PlayerProfileService.Instance.SaveInventorySnapshotAsync(workingInventory);
            ShowItemPreview(ProfileItemSource.PlayerInventory, firstOutputSlot, craftResult.Recipe.OutputItemId);
            SetStatus("Crafted " + InventoryItemCatalog.GetDisplayName(craftResult.Recipe.OutputItemId) + ".");
        }
        catch (Exception ex)
        {
            Debug.LogError("Crafting failed: " + ex);
            SetStatus("Crafting failed.");
        }
        finally
        {
            suppressNextProfileChangedRefresh = false;
            inventoryActionInProgress = false;
            SetInteractable(true);
            preserveInventoryButtonVisualsDuringSave = false;
            RefreshProfileSummaryAndInventory();
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
        preserveInventoryButtonVisualsDuringSave = true;
        SetInteractable(false);
        SetStatus("Clearing crafting slots...");

        try
        {
            PlayerInventoryData workingInventory = profile.Inventory.Clone();
            int shipCapacity = GetActiveShipInventoryCapacity();

            for (int i = 0; i < PlayerInventoryData.CraftingSlotCount; i++)
            {
                string itemId = workingInventory.RemoveFromCrafting(i);
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                if (workingInventory.TryAddToPlayer(itemId))
                    continue;

                if (workingInventory.TryAddToShip(itemId, shipCapacity, GetActiveProfileShipSkinIndex()))
                    continue;

                workingInventory.SetCrafting(i, itemId);
                SetStatus("No inventory space to clear crafting slots.");
                return false;
            }

            suppressNextProfileChangedRefresh = true;
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
            suppressNextProfileChangedRefresh = false;
            inventoryActionInProgress = false;
            bool finalInteractable = !NetworkManager.SessionRequested || !SessionBrowserPanelUI.IsVisible;
            SetInteractable(finalInteractable);
            preserveInventoryButtonVisualsDuringSave = false;
            if (!finalInteractable)
                SetInteractable(false);
            RefreshProfileSummaryAndInventory();
        }
    }

    int CountFreePlayerInventorySlots(PlayerInventoryData inventory)
    {
        if (inventory == null)
            return 0;

        inventory.Normalize();
        int count = 0;
        for (int i = 0; i < inventory.PlayerSlots.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(inventory.PlayerSlots[i]))
                count++;
        }

        return count;
    }

    int CountOccupiedPlayerInventorySlots(PlayerInventoryData inventory)
    {
        if (inventory == null || inventory.PlayerSlots == null)
            return 0;

        inventory.Normalize();
        int count = 0;
        for (int i = 0; i < inventory.PlayerSlots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(inventory.PlayerSlots[i]))
                count++;
        }

        return count;
    }

    int CountOccupiedShipInventorySlots(PlayerInventoryData inventory, int shipCapacity)
    {
        if (inventory == null || inventory.ShipSlots == null)
            return 0;

        inventory.Normalize();
        int count = 0;
        int capacity = Mathf.Clamp(shipCapacity, 0, inventory.ShipSlots.Length);
        for (int i = 0; i < capacity; i++)
        {
            if (!string.IsNullOrWhiteSpace(inventory.ShipSlots[i]))
                count++;
        }

        return count;
    }

    void RefreshInventoryView(PlayerInventoryData inventory)
    {
        PlayerInventoryData normalized = inventory != null ? inventory.Clone() : PlayerInventoryData.Default();
        normalized.Normalize();
        visiblePlayerInventorySlotMap = BuildVisiblePlayerInventorySlotMap(normalized);

        if (shipInventoryLabelText != null)
        {
            int shipCapacity = GetActiveShipInventoryCapacity();
            int occupiedShipSlots = CountOccupiedShipInventorySlots(normalized, shipCapacity);
            shipInventoryLabelText.text = "SHIP INVENTORY " + occupiedShipSlots + "/" + shipCapacity;
            ConfigureShipInventoryLabelText();
        }

        RebuildPlayerInventoryGrid(GetDisplayedPlayerInventorySlotCount(normalized));

        if (playerInventoryLabelText != null)
            ConfigurePlayerInventoryLabelText();
        if (playerInventoryCountText != null)
        {
            int occupiedPlayerSlots = CountOccupiedPlayerInventorySlots(normalized);
            int playerSlotCount = normalized.PlayerSlots != null ? normalized.PlayerSlots.Length : PlayerInventoryData.DefaultPlayerSlotCount;
            playerInventoryCountText.text = occupiedPlayerSlots + "/" + playerSlotCount;
            ConfigurePlayerInventoryCountText();
        }

        RefreshPlayerInventoryFilterButton();
        RefreshPlayerInventorySortButton();

        RefreshInventoryButtons(shipInventoryButtons, shipInventoryTexts, shipInventoryIcons, normalized.ShipSlots, true);
        RefreshInventoryButtons(playerInventoryButtons, playerInventoryTexts, playerInventoryIcons, normalized.PlayerSlots, false);
        RefreshCraftingButtons(craftingSlotButtons, craftingSlotTexts, craftingSlotIcons, normalized.CraftingSlots);

        if (resetPlayerInventoryScrollOnNextRefresh && playerInventoryScrollRect != null)
        {
            playerInventoryScrollRect.verticalNormalizedPosition = 1f;
            resetPlayerInventoryScrollOnNextRefresh = false;
        }
    }

    void OnPlayerInventoryFilterClicked()
    {
        if (inventoryActionInProgress || panelObject == null || !panelObject.activeSelf)
            return;

        if (playerInventoryFilterMode == PlayerInventoryFilterMode.CustomEquipmentSlot)
        {
            SetPlayerInventoryFilter(PlayerInventoryFilterMode.All, -1);
        }
        else
        {
            SetPlayerInventoryFilter(
                playerInventoryFilterMode == PlayerInventoryFilterMode.Equipable
                    ? PlayerInventoryFilterMode.All
                    : PlayerInventoryFilterMode.Equipable,
                -1);
        }

        resetPlayerInventoryScrollOnNextRefresh = true;
        HideItemPreview();
        RefreshView();
    }

    void OnPlayerInventorySortClicked()
    {
        if (inventoryActionInProgress || panelObject == null || !panelObject.activeSelf)
            return;

        playerInventorySortMode = GetNextPlayerInventorySortMode(playerInventorySortMode);
        resetPlayerInventoryScrollOnNextRefresh = true;
        HideItemPreview();
        RefreshView();
    }

    void SetPlayerInventoryFilter(PlayerInventoryFilterMode mode, int equipmentSlotIndex)
    {
        playerInventoryFilterMode = mode;
        customPlayerInventoryEquipmentSlotIndex = mode == PlayerInventoryFilterMode.CustomEquipmentSlot ? equipmentSlotIndex : -1;
    }

    PlayerInventorySortMode GetNextPlayerInventorySortMode(PlayerInventorySortMode mode)
    {
        switch (mode)
        {
            case PlayerInventorySortMode.Alphabetical:
                return PlayerInventorySortMode.Price;
            case PlayerInventorySortMode.Price:
                return PlayerInventorySortMode.Rarity;
            case PlayerInventorySortMode.Rarity:
                return PlayerInventorySortMode.Type;
            default:
                return PlayerInventorySortMode.Alphabetical;
        }
    }

    string GetPlayerInventorySortLabel(PlayerInventorySortMode mode)
    {
        switch (mode)
        {
            case PlayerInventorySortMode.Price:
                return "PRICE";
            case PlayerInventorySortMode.Rarity:
                return "RARITY";
            case PlayerInventorySortMode.Type:
                return "TYPE";
            default:
                return "A-Z";
        }
    }

    void RefreshPlayerInventorySortButton()
    {
        if (playerInventorySortButton == null)
            return;

        TMP_Text text = playerInventorySortButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = "SORT: " + GetPlayerInventorySortLabel(playerInventorySortMode);

        Image image = playerInventorySortButton.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.14f, 0.19f, 0.28f, 0.98f);
    }

    void RefreshPlayerInventoryFilterButton()
    {
        if (playerInventoryFilterButton == null)
            return;

        TMP_Text text = playerInventoryFilterButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = playerInventoryFilterMode switch
            {
                PlayerInventoryFilterMode.Equipable => "EQUIPABLE",
                PlayerInventoryFilterMode.CustomEquipmentSlot => "CUSTOM",
                _ => "ALL"
            };
        }

        Image image = playerInventoryFilterButton.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.14f, 0.19f, 0.28f, 0.98f);
    }

    int[] BuildVisiblePlayerInventorySlotMap(PlayerInventoryData inventory)
    {
        string[] slots = inventory != null ? inventory.PlayerSlots : null;
        if (slots == null)
            return Array.Empty<int>();

        List<int> visibleSlots = new List<int>();
        for (int i = 0; i < slots.Length; i++)
        {
            string itemId = slots[i];
            if (playerInventoryFilterMode == PlayerInventoryFilterMode.All || ShouldShowPlayerInventoryItem(itemId))
                visibleSlots.Add(i);
        }

        SortPlayerInventorySlotMap(visibleSlots, slots);
        return visibleSlots.ToArray();
    }

    void SortPlayerInventorySlotMap(List<int> slotIndices, string[] slots)
    {
        if (slotIndices == null || slotIndices.Count <= 1 || slots == null)
            return;

        slotIndices.Sort((a, b) => ComparePlayerInventorySlots(slots, a, b));
    }

    int ComparePlayerInventorySlots(string[] slots, int aIndex, int bIndex)
    {
        if (aIndex == bIndex)
            return 0;

        string aItemId = GetSlotItem(slots, aIndex);
        string bItemId = GetSlotItem(slots, bIndex);
        bool aOccupied = !string.IsNullOrWhiteSpace(aItemId);
        bool bOccupied = !string.IsNullOrWhiteSpace(bItemId);
        if (aOccupied != bOccupied)
            return aOccupied ? -1 : 1;

        if (!aOccupied)
            return aIndex.CompareTo(bIndex);

        int result;
        switch (playerInventorySortMode)
        {
            case PlayerInventorySortMode.Price:
                result = InventoryItemCatalog.GetSellValueAstrons(bItemId).CompareTo(InventoryItemCatalog.GetSellValueAstrons(aItemId));
                if (result != 0)
                    return result;
                break;
            case PlayerInventorySortMode.Rarity:
                result = ((int)InventoryItemCatalog.GetRarity(bItemId)).CompareTo((int)InventoryItemCatalog.GetRarity(aItemId));
                if (result != 0)
                    return result;
                break;
            case PlayerInventorySortMode.Type:
                result = InventoryItemCatalog.GetCategory(aItemId).CompareTo(InventoryItemCatalog.GetCategory(bItemId));
                if (result != 0)
                    return result;
                break;
        }

        result = string.Compare(InventoryItemCatalog.GetDisplayName(aItemId), InventoryItemCatalog.GetDisplayName(bItemId), StringComparison.OrdinalIgnoreCase);
        if (result != 0)
            return result;

        return aIndex.CompareTo(bIndex);
    }

    int GetDisplayedPlayerInventorySlotCount(PlayerInventoryData inventory)
    {
        if (playerInventoryFilterMode == PlayerInventoryFilterMode.All)
            return inventory != null && inventory.PlayerSlots != null ? inventory.PlayerSlots.Length : PlayerInventoryData.DefaultPlayerSlotCount;

        return Mathf.Max(1, visiblePlayerInventorySlotMap != null ? visiblePlayerInventorySlotMap.Length : 0);
    }

    bool ShouldShowPlayerInventoryItem(string itemId)
    {
        if (playerInventoryFilterMode == PlayerInventoryFilterMode.Equipable)
            return IsEquipableInventoryItem(itemId);

        if (playerInventoryFilterMode == PlayerInventoryFilterMode.CustomEquipmentSlot)
            return InventoryItemCatalog.IsCompatibleWithEquipmentSlot(itemId, customPlayerInventoryEquipmentSlotIndex);

        return true;
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
            InventoryItemCategory.Support => true,
            InventoryItemCategory.Rescue => true,
            _ => false
        };
    }

    void RefreshInventoryButtons(Button[] buttons, TMP_Text[] labels, Image[] icons, string[] slots, bool isShipInventory)
    {
        if (buttons == null || labels == null || icons == null || slots == null)
            return;

        int shipCapacity = GetActiveShipInventoryCapacity();

        for (int i = 0; i < buttons.Length; i++)
        {
            int slotIndex = isShipInventory ? i : ResolveVisiblePlayerInventorySlotIndex(i);
            bool visiblePlayerSlot = isShipInventory || slotIndex >= 0;
            bool withinShipCapacity = !isShipInventory || i < shipCapacity;
            if (buttons[i] != null)
                buttons[i].gameObject.SetActive(withinShipCapacity && visiblePlayerSlot);

            if (!withinShipCapacity || !visiblePlayerSlot || slotIndex >= slots.Length)
            {
                ClearInventoryButtonVisual(buttons, labels, icons, i);
                continue;
            }

            string itemId = slots[slotIndex];
            bool occupied = !string.IsNullOrWhiteSpace(itemId);
            bool isRadioactiveCargo = isShipInventory && occupied && InventoryItemCatalog.IsRadioactiveTreasure(itemId);
            bool isSafePocket = isShipInventory && IsActiveShipSafePocketIndex(i);
            bool isAstronautSlot = isShipInventory && IsActiveShipAstronautCargoIndex(i);
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
                else if (isAstronautSlot)
                {
                    labels[i].text = "ASTRO";
                    labels[i].color = occupied ? new Color(0f, 0f, 0f, 0f) : new Color(1f, 0.79f, 0.42f, 0.88f);
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
                image.color = GetInventorySlotColor(itemId, occupied, isSafePocket, isAstronautSlot, isRadioactiveCargo);
            }

            if (buttons[i] != null)
            {
                Outline outline = buttons[i].GetComponent<Outline>();
                if (isRadioactiveCargo)
                {
                    if (outline == null)
                        outline = buttons[i].gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0.34f, 1f, 0.2f, 0.98f);
                    outline.effectDistance = new Vector2(3f, 3f);
                    outline.enabled = true;
                }
                else if (isSafePocket)
                {
                    if (outline == null)
                        outline = buttons[i].gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0.38f, 0.98f, 0.88f, 0.95f);
                    outline.effectDistance = new Vector2(3f, 3f);
                    outline.enabled = true;
                }
                else if (isAstronautSlot)
                {
                    if (outline == null)
                        outline = buttons[i].gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(1f, 0.64f, 0.22f, 0.95f);
                    outline.effectDistance = new Vector2(3f, 3f);
                    outline.enabled = true;
                }
                else if (outline != null)
                {
                    outline.enabled = false;
                }
            }

            if (buttons[i] != null)
                buttons[i].interactable = preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress;
        }
    }

    void ClearInventoryButtonVisual(Button[] buttons, TMP_Text[] labels, Image[] icons, int index)
    {
        if (index < 0)
            return;

        if (labels != null && index < labels.Length && labels[index] != null)
        {
            labels[index].text = string.Empty;
            labels[index].color = new Color(0f, 0f, 0f, 0f);
        }

        if (icons != null && index < icons.Length && icons[index] != null)
        {
            icons[index].sprite = null;
            icons[index].enabled = false;
        }

        if (buttons != null && index < buttons.Length && buttons[index] != null)
        {
            Image image = buttons[index].GetComponent<Image>();
            if (image != null)
                image.color = new Color(0.12f, 0.16f, 0.21f, 0.96f);

            Outline outline = buttons[index].GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
        }
    }

    Color GetInventorySlotColor(string itemId, bool occupied, bool isSafePocket, bool isAstronautSlot, bool isRadioactiveCargo)
    {
        if (occupied)
        {
            Color baseColor = InventoryItemCatalog.GetRarityColor(itemId);
            if (isRadioactiveCargo)
                return Color.Lerp(baseColor, new Color(0.18f, 0.95f, 0.24f, baseColor.a), 0.58f);

            if (isSafePocket)
                return Color.Lerp(baseColor, new Color(0.24f, 0.74f, 0.66f, baseColor.a), 0.32f);

            return isAstronautSlot
                ? Color.Lerp(baseColor, new Color(1f, 0.67f, 0.28f, baseColor.a), 0.28f)
                : baseColor;
        }

        if (isSafePocket)
            return new Color(0.09f, 0.23f, 0.22f, 0.98f);

        return isAstronautSlot
            ? new Color(0.28f, 0.18f, 0.08f, 0.98f)
            : new Color(0.12f, 0.16f, 0.21f, 0.96f);
    }

    int ResolveVisiblePlayerInventorySlotIndex(int displayedSlotIndex)
    {
        if (visiblePlayerInventorySlotMap == null || displayedSlotIndex < 0)
            return playerInventoryFilterMode == PlayerInventoryFilterMode.All ? displayedSlotIndex : -1;

        if (displayedSlotIndex >= visiblePlayerInventorySlotMap.Length)
            return playerInventoryFilterMode == PlayerInventoryFilterMode.All ? displayedSlotIndex : -1;

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
                buttons[i].interactable = preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress;
        }

        if (craftButton != null)
            craftButton.interactable = preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress;
        if (clearCraftButton != null)
            clearCraftButton.interactable = preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress;
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
