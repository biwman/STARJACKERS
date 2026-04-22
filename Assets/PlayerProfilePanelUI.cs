using System;
using System.Collections.Generic;
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
        CraftingSlot
    }

    static readonly string[] GameplayHudObjectNames =
    {
        "JoystickBG",
        "ShootJoystickBG",
        "CollectButton",
        "ShipInventoryButton",
        "ShipInventoryPanel",
        "ReloadButton",
        "TimerText",
        "HP_Bar",
        "Shield_Bar",
        "Booster_Bar",
        "ScoreText"
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
    Button exitGameButton;
    Button[] shipTypeButtons;
    Button[] skinButtons;
    Button[] shipInventoryButtons;
    Button[] playerInventoryButtons;
    TMP_Text[] shipInventoryTexts;
    TMP_Text[] playerInventoryTexts;
    Image[] shipInventoryIcons;
    Image[] playerInventoryIcons;
    ScrollRect playerInventoryScrollRect;
    RectTransform playerInventoryContentRect;
    TMP_Text shipTypeLabelText;
    TMP_Text shipSkinLabelText;
    TMP_Text shipInventoryLabelText;
    TMP_Text playerInventoryLabelText;
    TMP_Text shipPreviewTitleText;
    RectTransform shipPreviewRootRect;
    RectTransform[] equipmentSlotRects;
    Button[] equipmentSlotButtons;
    TMP_Text[] equipmentSlotPreviewTexts;
    Image[] equipmentSlotPreviewIcons;
    Image shipPreviewImage;
    GameObject itemPreviewPanelObject;
    Image itemPreviewIcon;
    TMP_Text itemPreviewNameText;
    TMP_Text itemPreviewPriceText;
    Button itemPreviewSellButton;
    Button itemPreviewSalvageButton;
    GameObject craftingPanelObject;
    Button[] craftingSlotButtons;
    TMP_Text[] craftingSlotTexts;
    Image[] craftingSlotIcons;
    Button craftButton;
    GameObject splashScreenObject;
    Image splashScreenImage;
    float splashHideTime;
    static bool splashShownOnce;
    int selectedSkin;
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
        UpdateSkinButtonVisuals();
        ApplySaveAndRunButtonStyle();
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
        RefreshView();
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
        background.color = new Color(0.05f, 0.08f, 0.12f, 1f);
        background.type = Image.Type.Sliced;

        CreateText(panelObject.transform, "ProfileTitle", "PLAYER PROFILE", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -28f), new Vector2(360f, 40f), 34f, TextAlignmentOptions.Left);
        accountText = CreateText(panelObject.transform, "AccountText", "Connecting...", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-250f, -28f), new Vector2(320f, 24f), 16f, TextAlignmentOptions.Right);
        gamesPlayedText = CreateText(panelObject.transform, "GamesPlayedText", "Games Played: 0", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -92f), new Vector2(210f, 24f), 18f, TextAlignmentOptions.Left);
        totalXpText = CreateText(panelObject.transform, "TotalXpText", "Total XP: 0", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(420f, -92f), new Vector2(190f, 24f), 18f, TextAlignmentOptions.Left);
        astronsText = CreateText(panelObject.transform, "AstronsText", "Astrons: 0", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(610f, -92f), new Vector2(180f, 24f), 18f, TextAlignmentOptions.Left);

        CreateText(panelObject.transform, "NicknameLabel", "NICKNAME", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(790f, -92f), new Vector2(130f, 24f), 18f, TextAlignmentOptions.Left);

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

        shipTypeLabelText = CreateText(panelObject.transform, "ShipTypeLabel", "SHIP", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-304f, -214f), new Vector2(260f, 24f), 18f, TextAlignmentOptions.Left);

        shipTypeButtons = new Button[2];
        shipTypeButtons[0] = CreateButton(panelObject.transform, "ExplorerShipButton", "EXPLORER", new Vector2(404f, -242f), new Vector2(156f, 40f), () =>
        {
            SetSelectedShipType(ShipType.Explorer);
        });
        shipTypeButtons[1] = CreateButton(panelObject.transform, "ViperShipButton", "VIPER", new Vector2(580f, -242f), new Vector2(156f, 40f), () =>
        {
            SetSelectedShipType(ShipType.Viper);
        });

        shipSkinLabelText = CreateText(panelObject.transform, "SkinLabel", "SHIP SKIN", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-304f, -294f), new Vector2(300f, 24f), 18f, TextAlignmentOptions.Left);

        skinButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int capturedIndex = i;
            skinButtons[i] = CreateButton(panelObject.transform, "ShipSkinButton" + i, "SKIN", new Vector2(346f + (146f * i), -322f), new Vector2(126f, 56f), () =>
            {
                ApplySkinChoiceByButtonIndex(capturedIndex);
            });
        }

        shipPreviewTitleText = CreateText(panelObject.transform, "ShipPreviewTitle", "SHIP LOADOUT", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-304f, -506f), new Vector2(360f, 24f), 18f, TextAlignmentOptions.Left);
        CreateShipPreview(panelObject.transform);

        inventoryHintText = CreateText(panelObject.transform, "InventoryHintText", "Tap to preview. Drag between inventories, loadout slots and crafting.", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(820f, 24f), 16f, TextAlignmentOptions.Center);
        inventoryHintText.fontStyle = FontStyles.Normal;

        shipInventoryLabelText = CreateText(panelObject.transform, "ShipInventoryLabel", "SHIP INVENTORY", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-614f, -262f), new Vector2(420f, 24f), 18f, TextAlignmentOptions.Center);
        CreateInventoryGrid(panelObject.transform, false, new Vector2(-878f, -294f), 10, 5, out shipInventoryButtons, out shipInventoryTexts, out shipInventoryIcons);

        playerInventoryLabelText = CreateText(panelObject.transform, "PlayerInventoryLabel", "PLAYER INVENTORY (" + PlayerInventoryData.PlayerSlotCount + ")", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-560f, -546f), new Vector2(520f, 24f), 18f, TextAlignmentOptions.Center);
        CreateScrollablePlayerInventoryGrid(panelObject.transform, new Vector2(-938f, -578f), new Vector2(830f, 362f), PlayerInventoryData.PlayerSlotCount, 6, out playerInventoryButtons, out playerInventoryTexts, out playerInventoryIcons);

        CreateItemPreview(panelObject.transform);
        CreateCraftingPanel(panelObject.transform);

        exitGameButton = CreateButton(panelObject.transform, "ExitGameButton", "EXIT GAME", new Vector2(820f, -72f), new Vector2(210f, 54f), OnExitGameClicked);
        saveAndRunButton = CreateButton(panelObject.transform, "SaveAndRunButton", "SAVE & RUN", new Vector2(296f, -816f), new Vector2(294f, 84f), OnSaveAndRunClicked);
        ApplySaveAndRunButtonStyle();
        statusText = CreateText(panelObject.transform, "ProfileStatusText", string.Empty, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(320f, 24f), 16f, TextAlignmentOptions.Center);
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
        rootRect.sizeDelta = new Vector2(560f, 330f);

        Image rootImage = previewRoot.GetComponent<Image>();
        rootImage.color = new Color(0.12f, 0.16f, 0.2f, 0.7f);

        GameObject imageObject = new GameObject("ShipPreviewImage", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(previewRoot.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = new Vector2(0f, 0f);
        imageRect.sizeDelta = new Vector2(220f, 140f);
        shipPreviewImage = imageObject.GetComponent<Image>();
        shipPreviewImage.preserveAspect = true;

        equipmentSlotRects = new RectTransform[PlayerInventoryData.EquipmentSlotCount];
        equipmentSlotButtons = new Button[PlayerInventoryData.EquipmentSlotCount];
        equipmentSlotPreviewTexts = new TMP_Text[PlayerInventoryData.EquipmentSlotCount];
        equipmentSlotPreviewIcons = new Image[PlayerInventoryData.EquipmentSlotCount];
        equipmentSlotButtons[0] = CreateEquipmentSlotButton(previewRoot.transform, "MainGunA", Vector2.zero, 0, "MAIN GUN", out equipmentSlotPreviewTexts[0], out equipmentSlotPreviewIcons[0]);
        equipmentSlotButtons[1] = CreateEquipmentSlotButton(previewRoot.transform, "MainGunB", Vector2.zero, 1, "MAIN GUN", out equipmentSlotPreviewTexts[1], out equipmentSlotPreviewIcons[1]);
        equipmentSlotButtons[2] = CreateEquipmentSlotButton(previewRoot.transform, "ShieldSlot", Vector2.zero, 2, "SHIELD", out equipmentSlotPreviewTexts[2], out equipmentSlotPreviewIcons[2]);
        equipmentSlotButtons[3] = CreateEquipmentSlotButton(previewRoot.transform, "EngineA", Vector2.zero, 3, "ENGINE", out equipmentSlotPreviewTexts[3], out equipmentSlotPreviewIcons[3]);
        equipmentSlotButtons[4] = CreateEquipmentSlotButton(previewRoot.transform, "EngineB", Vector2.zero, 4, "ENGINE", out equipmentSlotPreviewTexts[4], out equipmentSlotPreviewIcons[4]);
        equipmentSlotButtons[5] = CreateEquipmentSlotButton(previewRoot.transform, "GadgetSlot", Vector2.zero, 5, "GADGET", out equipmentSlotPreviewTexts[5], out equipmentSlotPreviewIcons[5]);

        for (int i = 0; i < equipmentSlotButtons.Length; i++)
        {
            if (equipmentSlotButtons[i] != null)
                equipmentSlotRects[i] = equipmentSlotButtons[i].GetComponent<RectTransform>();
        }

        UpdateEquipmentSlotLayout();
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
        rect.anchoredPosition = new Vector2(0f, 112f);
        rect.sizeDelta = new Vector2(300f, 320f);

        Image background = itemPreviewPanelObject.GetComponent<Image>();
        background.color = new Color(0.08f, 0.12f, 0.16f, 0.92f);

        GameObject iconObject = new GameObject("ItemPreviewIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(itemPreviewPanelObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -18f);
        iconRect.sizeDelta = new Vector2(136f, 136f);
        itemPreviewIcon = iconObject.GetComponent<Image>();
        itemPreviewIcon.preserveAspect = true;

        itemPreviewNameText = CreateText(itemPreviewPanelObject.transform, "ItemPreviewNameText", "SELECT ITEM", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(250f, 32f), 22f, TextAlignmentOptions.Center);
        itemPreviewPriceText = CreateText(itemPreviewPanelObject.transform, "ItemPreviewPriceText", "Value: 0 Astrons", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(250f, 24f), 18f, TextAlignmentOptions.Center);
        itemPreviewPriceText.fontStyle = FontStyles.Normal;
        itemPreviewSellButton = CreateButton(itemPreviewPanelObject.transform, "ItemPreviewSellButton", "SELL", new Vector2(-72f, -254f), new Vector2(116f, 44f), OnItemPreviewSellClicked);
        itemPreviewSalvageButton = CreateButton(itemPreviewPanelObject.transform, "ItemPreviewSalvageButton", "SALVAGE", new Vector2(72f, -254f), new Vector2(116f, 44f), OnItemPreviewSalvageClicked);
        itemPreviewPanelObject.SetActive(false);
    }

    void CreateCraftingPanel(Transform parent)
    {
        craftingPanelObject = new GameObject("CraftingPanel", typeof(RectTransform), typeof(Image));
        craftingPanelObject.transform.SetParent(parent, false);

        RectTransform rect = craftingPanelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -234f);
        rect.sizeDelta = new Vector2(330f, 350f);

        Image background = craftingPanelObject.GetComponent<Image>();
        background.color = new Color(0.07f, 0.1f, 0.14f, 0.94f);

        TMP_Text title = CreateText(craftingPanelObject.transform, "CraftingTitle", "CRAFTING", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(220f, 30f), 24f, TextAlignmentOptions.Center);
        title.characterSpacing = 3f;

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
            new Vector2(-66f, -230f),
            new Vector2(66f, -230f)
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

        craftButton = CreateButton(craftingPanelObject.transform, "CraftButton", "CRAFT", new Vector2(0f, -300f), new Vector2(190f, 52f), OnCraftButtonClicked);
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
        button.onClick.AddListener(() => onClick?.Invoke());

        TMP_Text text = CreateText(buttonObject.transform, objectName + "Text", label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 18f, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        if (objectName.StartsWith("ShipSkinButton", StringComparison.Ordinal))
        {
            text.fontSize = 15f;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.margin = new Vector4(6f, 4f, 6f, 4f);
        }

        return button;
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

    async void OnSaveAndRunClicked()
    {
        if (nicknameInput == null)
            return;

        if (IsCraftingGridOccupied())
        {
            SetStatus("Empty crafting slots before starting.");
            return;
        }

        SetStatus("Saving profile...");
        SetInteractable(false);

        try
        {
            await PlayerProfileService.Instance.SaveProfileAsync(nicknameInput.text, selectedSkin);
            SetStatus("Connecting...");
            NetworkManager.RequestSessionStart();
        }
        catch (Exception ex)
        {
            Debug.LogError("Profile save failed: " + ex);
            SetStatus("Save failed");
            SetInteractable(true);
        }
    }

    void RefreshView()
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (profile == null)
            return;

        if (profile.Inventory != null)
            profile.Inventory.Normalize();

        selectedSkin = Mathf.Clamp(profile.ShipSkinIndex, 0, 3);

        if (nicknameInput != null && !nicknameInput.isFocused)
        {
            nicknameInput.text = profile.Nickname;
        }

        if (gamesPlayedText != null)
        {
            gamesPlayedText.text = "Games Played: " + profile.GamesPlayed;
        }

        if (totalXpText != null)
        {
            totalXpText.text = "Total XP: " + profile.TotalXp;
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
                bool isSelected = (ShipType)i == selectedType;
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
            shipSkinLabelText.text = shipType == ShipType.Viper ? "SHIP SKIN (VIPER)" : "SHIP SKIN (EXPLORER)";

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

        RectTransform rect = saveAndRunButton.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(296f, -816f);
            rect.sizeDelta = new Vector2(294f, 84f);
        }

        Image image = saveAndRunButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.08f, 0.58f, 0.18f, 1f);
            image.raycastTarget = true;
        }

        ColorBlock colors = saveAndRunButton.colors;
        colors.normalColor = new Color(0.08f, 0.58f, 0.18f, 1f);
        colors.highlightedColor = new Color(0.12f, 0.68f, 0.23f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.05f, 0.42f, 0.13f, 1f);
        colors.disabledColor = new Color(0.07f, 0.34f, 0.12f, 0.72f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        saveAndRunButton.colors = colors;
        saveAndRunButton.transition = Selectable.Transition.ColorTint;

        TMP_Text text = saveAndRunButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.color = Color.white;
            text.fontSize = 30f;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 5f;
            text.alignment = TextAlignmentOptions.Center;
        }
    }

    void UpdateEquipmentSlotLayout()
    {
        if (equipmentSlotRects == null || equipmentSlotRects.Length < PlayerInventoryData.EquipmentSlotCount)
            return;

        ShipType shipType = GetSelectedShipType();
        Vector2[] positions = shipType == ShipType.Viper
            ? new[]
            {
                new Vector2(-184f, -34f),
                new Vector2(184f, -34f),
                new Vector2(0f, -248f),
                new Vector2(-184f, -170f),
                new Vector2(184f, -170f),
                new Vector2(0f, -102f)
            }
            : new[]
            {
                new Vector2(0f, -34f),
                new Vector2(184f, -34f),
                new Vector2(-184f, -102f),
                new Vector2(0f, -248f),
                new Vector2(184f, -170f),
                new Vector2(-184f, -170f)
            };

        for (int i = 0; i < equipmentSlotRects.Length && i < positions.Length; i++)
        {
            RectTransform rect = equipmentSlotRects[i];
            if (rect == null)
                continue;

            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = positions[i];
            rect.sizeDelta = new Vector2(120f, 120f);
        }
    }

    void RefreshShipPreview()
    {
        if (shipPreviewTitleText != null)
        {
            int capacity = ShipCatalog.GetShipInventoryCapacity(selectedSkin);
            shipPreviewTitleText.text = ShipCatalog.GetShipTypeDisplayName(GetSelectedShipType()).ToUpperInvariant() + " LOADOUT  |  CARGO " + capacity;
        }

        UpdateEquipmentSlotLayout();

        if (shipPreviewImage != null)
        {
            shipPreviewImage.sprite = LoadShipPreviewSprite(selectedSkin);
            shipPreviewImage.color = shipPreviewImage.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        RefreshEquipmentSlotPreview();
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
        if (text == null)
            return;

        Image icon = equipmentSlotPreviewIcons != null && slotIndex >= 0 && slotIndex < equipmentSlotPreviewIcons.Length
            ? equipmentSlotPreviewIcons[slotIndex]
            : null;
        Button button = equipmentSlotButtons != null && slotIndex >= 0 && slotIndex < equipmentSlotButtons.Length
            ? equipmentSlotButtons[slotIndex]
            : null;
        bool occupied = enabled && !string.IsNullOrWhiteSpace(itemId);
        Sprite itemSprite = occupied ? InventoryItemCatalog.GetIcon(itemId) : null;

        text.text = enabled
            ? (occupied && itemSprite == null ? InventoryItemCatalog.GetShortLabel(itemId) : label)
            : "NO SLOT";
        text.color = enabled ? Color.white : new Color(0.58f, 0.62f, 0.68f, 0.82f);
        Image bg = text.transform.parent != null ? text.transform.parent.GetComponent<Image>() : null;
        if (bg != null)
        {
            bg.color = occupied
                ? InventoryItemCatalog.GetRarityColor(itemId)
                : enabled ? new Color(0.17f, 0.22f, 0.28f, 0.88f) : new Color(0.1f, 0.12f, 0.16f, 0.55f);
        }

        if (icon != null)
        {
            icon.sprite = itemSprite;
            icon.enabled = occupied && itemSprite != null;
        }

        if (button != null)
            button.interactable = !inventoryActionInProgress && enabled;
    }

    string GetEquipmentSlotLabel(int slotIndex)
    {
        return slotIndex switch
        {
            0 => "MAIN GUN",
            1 => "MAIN GUN",
            2 => "SHIELD",
            3 => "ENGINE",
            4 => "ENGINE",
            5 => "GADGET",
            _ => "SLOT"
        };
    }

    Sprite LoadShipPreviewSprite(int skinIndex)
    {
        string resourcesPath = skinIndex switch
        {
            1 => "Visuals/Ships/ship2_resource",
            2 => "Visuals/Ships/ship3_resource",
            3 => "ship4_resource",
            _ => "Visuals/Ships/ship1_resource"
        };

        string editorPath = skinIndex switch
        {
            1 => "Assets/Resources/Visuals/Ships/ship2_resource.png",
            2 => "Assets/Resources/Visuals/Ships/ship3_resource.png",
            3 => "Assets/Resources/ship4_resource.png",
            _ => "Assets/Resources/Visuals/Ships/ship1_resource.png"
        };

        string editorFallbackPath = skinIndex switch
        {
            1 => "Assets/ship2.png",
            2 => "Assets/ship3.png",
            3 => "Assets/ship4.png",
            _ => "Assets/ship1.png"
        };

        return LoadSpriteFromResourcesOrEditor(resourcesPath, editorPath, editorFallbackPath);
    }

    Sprite LoadStandaloneSprite(string fileName)
    {
        string resourcesPath = fileName switch
        {
            "STAR_RAIDERS_ekran.png" => "STAR_RAIDERS_ekran_resource",
            "ship1.png" => "Visuals/Ships/ship1_resource",
            "ship2.png" => "Visuals/Ships/ship2_resource",
            "ship3.png" => "Visuals/Ships/ship3_resource",
            "ship4.png" => "ship4_resource",
            _ => null
        };

        string editorResourcePath = fileName switch
        {
            "STAR_RAIDERS_ekran.png" => "Assets/Resources/STAR_RAIDERS_ekran_resource.png",
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

        if (show)
        {
            SetInteractable(!NetworkManager.SessionRequested);
            if (statusText != null && statusText.text == "Connecting..." && !NetworkManager.SessionRequested)
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

    void SetInteractable(bool interactable)
    {
        if (nicknameInput != null)
            nicknameInput.interactable = interactable;

        if (saveAndRunButton != null)
            saveAndRunButton.interactable = interactable;

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

        SetInventoryInteractable(interactable && !inventoryActionInProgress);
    }

    void SetInventoryInteractable(bool interactable)
    {
        SetInventoryButtonState(playerInventoryButtons, interactable);
        SetInventoryButtonState(shipInventoryButtons, interactable);
        SetInventoryButtonState(equipmentSlotButtons, interactable);
        SetInventoryButtonState(craftingSlotButtons, interactable);
        if (itemPreviewSellButton != null)
            itemPreviewSellButton.interactable = interactable;
        if (itemPreviewSalvageButton != null)
            itemPreviewSalvageButton.interactable = interactable;
        if (craftButton != null)
            craftButton.interactable = interactable;
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

        if (TryGetInventoryItemId(isPlayerInventory, slotIndex, out string itemId))
        {
            ShowItemPreview(ProfileItemSourceFromInventory(isPlayerInventory), slotIndex, itemId);
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

        if (!TryGetInventoryItemId(isPlayerInventory, slotIndex, out string itemId))
            return;

        dragInProgress = true;
        suppressNextInventoryClick = true;
        ShowItemPreview(ProfileItemSourceFromInventory(isPlayerInventory), slotIndex, itemId);
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
        if (!ResolveDropTarget(eventData != null ? eventData.pointerEnter : null, out ProfileItemSource targetSource, out int targetIndex))
            return;

        inventoryActionInProgress = true;
        SetInteractable(false);

        try
        {
            bool moved = await MoveItemToTargetAsync(source, slotIndex, targetSource, targetIndex);
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

        string[] slots = isPlayerInventory ? profile.Inventory.PlayerSlots : profile.Inventory.ShipSlots;
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
            return false;

        itemId = slots[slotIndex];
        return !string.IsNullOrWhiteSpace(itemId);
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
            SetStatus("Equipment item selected.");
        }
        else
        {
            HideItemPreview();
            SetStatus(string.Empty);
        }
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

        itemPreviewPanelObject.SetActive(true);
        previewSource = source;
        previewSlotIndex = slotIndex;
        previewItemId = itemId;
        itemPreviewIcon.sprite = InventoryItemCatalog.GetIcon(itemId);
        itemPreviewIcon.enabled = itemPreviewIcon.sprite != null;
        itemPreviewNameText.text = InventoryItemCatalog.GetDisplayName(itemId).ToUpperInvariant();
        itemPreviewPriceText.text = "Value: " + InventoryItemCatalog.GetSellValueAstrons(itemId) + " Astrons";

        Image bg = itemPreviewPanelObject.GetComponent<Image>();
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
        if (source == targetSource)
            return false;

        bool involvesCrafting = source == ProfileItemSource.CraftingSlot || targetSource == ProfileItemSource.CraftingSlot;
        if (!involvesCrafting)
        {
            switch (source)
            {
                case ProfileItemSource.PlayerInventory:
                case ProfileItemSource.ShipInventory:
                {
                    bool fromShipInventory = source == ProfileItemSource.ShipInventory;
                    if (targetSource == ProfileItemSource.PlayerInventory || targetSource == ProfileItemSource.ShipInventory)
                        return await PlayerProfileService.Instance.MoveInventoryItemAsync(fromShipInventory, sourceIndex);

                    if (targetSource == ProfileItemSource.EquipmentSlot)
                        return await PlayerProfileService.Instance.MoveInventoryItemToEquipmentAsync(fromShipInventory, sourceIndex, targetIndex, selectedSkin);

                    break;
                }
                case ProfileItemSource.EquipmentSlot:
                {
                    if (targetSource == ProfileItemSource.PlayerInventory)
                        return await PlayerProfileService.Instance.MoveEquipmentItemToInventoryAsync(sourceIndex, true, selectedSkin);

                    if (targetSource == ProfileItemSource.ShipInventory)
                        return await PlayerProfileService.Instance.MoveEquipmentItemToInventoryAsync(sourceIndex, false, selectedSkin);

                    break;
                }
            }

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

    bool TryPlaceItemAtTarget(PlayerInventoryData inventory, ProfileItemSource targetSource, int targetIndex, string itemId, ProfileItemSource source, int sourceIndex)
    {
        switch (targetSource)
        {
            case ProfileItemSource.PlayerInventory:
                return inventory.TryAddToPlayer(itemId);

            case ProfileItemSource.ShipInventory:
                return inventory.TryAddToShip(itemId, ShipCatalog.GetShipInventoryCapacity(selectedSkin));

            case ProfileItemSource.CraftingSlot:
                if (targetIndex < 0 || targetIndex >= PlayerInventoryData.CraftingSlotCount)
                    return false;
                if (!string.IsNullOrWhiteSpace(inventory.CraftingSlots[targetIndex]))
                    return false;
                inventory.SetCrafting(targetIndex, itemId);
                return true;

            case ProfileItemSource.EquipmentSlot:
                if (!inventory.IsEquipmentSlotEnabled(targetIndex, selectedSkin))
                    return false;

                string replacedItem = inventory.RemoveFromEquipment(targetIndex);
                inventory.SetEquipment(targetIndex, itemId);

                if (string.IsNullOrWhiteSpace(replacedItem))
                    return true;

                if (source == ProfileItemSource.CraftingSlot)
                {
                    inventory.SetCrafting(sourceIndex, replacedItem);
                    return true;
                }

                bool restored = source switch
                {
                    ProfileItemSource.PlayerInventory => inventory.TryAddToPlayer(replacedItem),
                    ProfileItemSource.ShipInventory => inventory.TryAddToShip(replacedItem, ShipCatalog.GetShipInventoryCapacity(selectedSkin)),
                    _ => false
                };

                if (restored)
                    return true;

                inventory.SetEquipment(targetIndex, replacedItem);
                return false;
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

    void RefreshInventoryView(PlayerInventoryData inventory)
    {
        PlayerInventoryData normalized = inventory != null ? inventory.Clone() : PlayerInventoryData.Default();
        normalized.Normalize();

        if (shipInventoryLabelText != null)
            shipInventoryLabelText.text = "SHIP INVENTORY (" + ShipCatalog.GetShipInventoryCapacity(selectedSkin) + ")";

        if (playerInventoryLabelText != null)
            playerInventoryLabelText.text = "PLAYER INVENTORY (" + PlayerInventoryData.PlayerSlotCount + ")";

        RefreshInventoryButtons(shipInventoryButtons, shipInventoryTexts, shipInventoryIcons, normalized.ShipSlots, true);
        RefreshInventoryButtons(playerInventoryButtons, playerInventoryTexts, playerInventoryIcons, normalized.PlayerSlots, false);
        RefreshCraftingButtons(craftingSlotButtons, craftingSlotTexts, craftingSlotIcons, normalized.CraftingSlots);
    }

    void RefreshInventoryButtons(Button[] buttons, TMP_Text[] labels, Image[] icons, string[] slots, bool isShipInventory)
    {
        if (buttons == null || labels == null || icons == null || slots == null)
            return;

        for (int i = 0; i < buttons.Length && i < slots.Length; i++)
        {
            string itemId = slots[i];
            bool occupied = !string.IsNullOrWhiteSpace(itemId);
            int shipCapacity = ShipCatalog.GetShipInventoryCapacity(selectedSkin);
            bool withinShipCapacity = !isShipInventory || i < shipCapacity;
            Image image = buttons[i] != null ? buttons[i].GetComponent<Image>() : null;
            Image icon = icons[i];
            Sprite itemSprite = occupied ? InventoryItemCatalog.GetIcon(itemId) : null;

            if (buttons[i] != null)
                buttons[i].gameObject.SetActive(withinShipCapacity);

            if (!withinShipCapacity)
                continue;

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
