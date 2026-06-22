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

public partial class PlayerProfilePanelUI : MonoBehaviour
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
        Player,
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
    static readonly string[] PlayerCareerStatLabels =
    {
        "Escapes with ship",
        "Escapes with astronaut",
        "Rounds played",
        "Series of returns without death",
        "Enemies killed",
        "Neutral Raiders killed",
        "Human Players killed",
        "Astrons earned",
        "Highest loot returned",
        "Projects finished",
        "Maps unlocked",
        "Pilots unlocked",
        "Ships unlocked",
        "Unique items found"
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
    GameObject playerViewRootObject;
    GameObject projectsViewRootObject;
    GameObject projectDetailsViewObject;
    GameObject shipSelectionViewObject;
    GameObject pilotSelectionViewObject;
    GameObject traderFuturePanelObject;
    Button navCraftingButton;
    Button navTraderButton;
    Button navInventoryButton;
    Button navPlayerButton;
    Button navBackButton;
    GameObject playerStatsPanelObject;
    TMP_Text playerStatsTitleText;
    TMP_Text[] playerStatLabelTexts;
    TMP_Text[] playerStatValueTexts;
    GameObject playerQuestItemsPanelObject;
    TMP_Text playerQuestItemsText;
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

}
