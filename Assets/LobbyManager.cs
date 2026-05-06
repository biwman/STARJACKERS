using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LobbyManager : MonoBehaviourPunCallbacks
{
    enum LobbyScreen
    {
        MapSelection,
        MapDetails,
        DeveloperSettings
    }

    const float BottomActionButtonsY = -422f;
    const float BottomActionReadyX = 350f;
    const float BottomActionBackX = 810f;
    const float BottomActionButtonWidth = 430f;
    const float BottomActionButtonHeight = 112f;
    const float LeftViewportWidth = 760f;
    const float LeftViewportHeight = 960f;
    const float LeftColumnX = -560f;
    const float LeftColumnTopY = -220f;
    const float RightTableWidth = 1120f;
    const float RightTableHeight = 620f;
    const float RightTableX = 330f;
    const float RightTableY = -290f;
    const float MapSelectionButtonX = 500f;
    const float MapSelectionButtonY = -270f;
    const float MapSelectionButtonWidth = 330f;
    const float MapSelectionButtonHeight = 180f;
    const float EnemyNameColumnWidth = 180f;
    const float EnemyColumnWidth = 98f;
    const float EnemyRowHeight = 62f;
    const float FullScreenSideMargin = 34f;
    const float FullScreenTopMargin = 24f;
    const float FullScreenBottomMargin = 34f;
    const float LobbyTopBarHeight = 70f;
    const float MapTileWidth = 520f;
    const float MapTileHeight = 250f;
    const float MapTileSpacingX = 48f;
    const float MapTileSpacingY = 36f;
    const float MapSelectionContentTopY = -140f;
    const float TopActionButtonWidth = 270f;
    const float TopActionButtonHeight = 72f;
    const float BottomWideButtonWidth = 430f;
    const float BottomWideButtonHeight = 88f;

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
        "WeaponSwitchButton",
        "TimerText",
        "HP_Bar",
        "Shield_Bar",
        "Booster_Bar",
        "ScoreText"
    };

    static readonly float[] RoundDurationOptions = { 60f, 90f, 120f, 150f, 180f, 210f, 240f, 270f, 300f, 330f, 360f };
    static readonly string[] DensityOptions = { "none", "low", "medium", "high" };
    static readonly string[] ResourceRichnessOptions =
    {
        RoomSettings.ResourceRichnessVeryLow,
        RoomSettings.ResourceRichnessLow,
        RoomSettings.ResourceRichnessMedium,
        RoomSettings.ResourceRichnessHigh,
        RoomSettings.ResourceRichnessVeryHigh,
        RoomSettings.ResourceRichnessExtreme
    };
    static readonly string[] SpaceJunkDensityOptions =
    {
        RoomSettings.SpaceJunkDensityNone,
        RoomSettings.SpaceJunkDensityLow,
        RoomSettings.SpaceJunkDensityMedium,
        RoomSettings.SpaceJunkDensityHigh
    };
    static readonly string[] MapSizeOptions = { "small", "medium", "large", "very_large", "super_large" };
    static readonly int[] MapBackgroundOptions = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
    static readonly int[] ObstacleHpOptions = { 50, 100, 150, 200, 250, 300, 350, 400, 450, 500 };
    static readonly int[] ObstacleSizePercentOptions = { 50, 100, 150, 200, 250, 300, 350, 400, 450, 500 };
    static readonly int[] ExtractionCountOptions = { 1, 2, 3, 4 };
    static readonly int[] RepairBayCountOptions = { 0, 1, 2 };
    static readonly int[] BoosterSlowdownOptions = { 30, 40, 50, 60, 70, 80, 90, 100 };
    static readonly int[] AmmoCountOptions = { 5, 10, 15, 20, 25, 30 };
    static readonly int[] BoosterRecoveryDelayOptions = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    static readonly float[] LastShipTimerMultiplierOptions = { 1f, 1.5f, 2f, 3f, 4f, 5f };
    static readonly string[] MovingObjectsModeOptions = { RoomSettings.MovingObjectsModeOn, RoomSettings.MovingObjectsModeOff, RoomSettings.MovingObjectsModeOnlyRotate };
    static readonly int[] EnemyCountOptions = { 1, 2, 3, 4, 5 };
    static readonly int[] SpaceMineCountOptions = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 };
    static readonly int[] EnemyHpOptions = { 20, 40, 60, 80, 100, 120, 140, 160, 180, 200 };
    static readonly int[] EnemyShieldOptions = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200 };
    static readonly int[] EnemyDamageOptions = { 0, 5, 10, 15, 20, 25, 30, 40, 50, 60, 80, 100, 150, 200 };
    static readonly float[] EnemySpeedOptions = { 0.25f, 0.5f, 1f, 1.5f, 2f };
    static readonly int[] EnemySpawnSecondOptions = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120 };
    static readonly int[] EnemyRespawnIntervalOptions = { 15, 30, 60, 90, 120, 150 };
    static readonly int[] BulletPushMultiplierOptions = { 1, 2, 3, 4, 5 };
    static readonly int[] ObstacleWeightFactorOptions = { 2, 6, 12, RoomSettings.MaxObstacleWeightFactor };
    static readonly int[] TreasureWeightFactorOptions = { 2, 6, 12 };
    static readonly int[] BatteringDamageOptions = { 0, 10, 20, 30, 40, 50 };
    static readonly string[] EndDisasterModeOptions = { RoomSettings.EndDisasterOff, RoomSettings.EndDisasterMeteor };
    static readonly int[] EndDisasterWarningSecondOptions = { 10, 15, 20, 25, 30, 35, 40 };
    static readonly string[] ShootingModelOptions = { RoomSettings.ShootingModelSimple, RoomSettings.ShootingModelComplex };

    public Button readyButton;
    public TMP_Text readyText;
    public TMP_Text playerStatusListText;
    public TMP_Text roundSettingText;
    public TMP_Text mapSizeSettingText;
    public TMP_Text mapBackgroundSettingText;
    public TMP_Text visualEffectsSettingText;
    public TMP_Text startingVfxSettingText;
    public TMP_Text endDisasterSettingText;
    public TMP_Text endDisasterTimeSettingText;
    public TMP_Text obstacleSettingText;
    public TMP_Text obstacleDestroySettingText;
    public TMP_Text obstacleHpValueSettingText;
    public TMP_Text obstacleSizeSettingText;
    public TMP_Text obstacleNoBordersSettingText;
    public TMP_Text treasureSettingText;
    public TMP_Text resourceRichnessSettingText;
    public TMP_Text spaceJunkSettingText;
    public TMP_Text nebulaSettingText;
    public TMP_Text extractionSettingText;
    public TMP_Text repairBaySettingText;
    public TMP_Text boosterSettingText;
    public TMP_Text ammoSettingText;
    public TMP_Text boosterDelaySettingText;
    public TMP_Text shipDriftSettingText;
    public TMP_Text deathTimerSettingText;
    public TMP_Text inventoryLossSettingText;
    public TMP_Text equipmentLossSettingText;
    public TMP_Text movingObjectsSettingText;
    public TMP_Text enemyBotsSettingText;
    public TMP_Text corsairSettingText;
    public TMP_Text corsairTimeSettingText;
    public TMP_Text corsairHpSettingText;
    public TMP_Text bulletPushSettingText;
    public TMP_Text obstacleWeightSettingText;
    public TMP_Text treasureWeightSettingText;
    public TMP_Text batteringSettingText;
    public TMP_Text shootingModelSettingText;
    public TMP_Text superAttackSettingText;
    public TMP_Text advancedShootingJoystickSettingText;
    public TMP_Text hapticsSettingText;
    public TMP_Text fpsCounterSettingText;
    public TMP_Text gunSetupSettingText;
    public Button roundSettingButton;
    public Button mapSizeSettingButton;
    public Button mapBackgroundSettingButton;
    public Button visualEffectsSettingButton;
    public Button startingVfxSettingButton;
    public Button endDisasterSettingButton;
    public Button endDisasterTimeSettingButton;
    public Button obstacleSettingButton;
    public Button obstacleDestroySettingButton;
    public Button obstacleHpValueSettingButton;
    public Button obstacleSizeSettingButton;
    public Button obstacleNoBordersSettingButton;
    public Button treasureSettingButton;
    public Button resourceRichnessSettingButton;
    public Button spaceJunkSettingButton;
    public Button nebulaSettingButton;
    public Button extractionSettingButton;
    public Button repairBaySettingButton;
    public Button boosterSettingButton;
    public Button ammoSettingButton;
    public Button boosterDelaySettingButton;
    public Button shipDriftSettingButton;
    public Button deathTimerSettingButton;
    public Button inventoryLossSettingButton;
    public Button equipmentLossSettingButton;
    public Button movingObjectsSettingButton;
    public Button bulletPushSettingButton;
    public Button obstacleWeightSettingButton;
    public Button treasureWeightSettingButton;
    public Button batteringSettingButton;
    public Button shootingModelSettingButton;
    public Button superAttackSettingButton;
    public Button advancedShootingJoystickSettingButton;
    public Button hapticsSettingButton;
    public Button fpsCounterSettingButton;
    public Button gunSetupSettingButton;
    public Button backToRoundsButton;
    public TMP_Text backToRoundsText;
    public Button mapSelectionButton;
    public TMP_Text mapSelectionText;

    bool isReady = false;
    bool hasRecordedCurrentRound = false;
    readonly Dictionary<string, Button> enemySettingButtons = new Dictionary<string, Button>();
    readonly Dictionary<string, TMP_Text> enemySettingTexts = new Dictionary<string, TMP_Text>();
    readonly Dictionary<string, GameObject> gameplayHudObjectsByName = new Dictionary<string, GameObject>();
    readonly Dictionary<string, RectTransform> leftSectionContainers = new Dictionary<string, RectTransform>();
    readonly Dictionary<string, TMP_Text> enemyRowLabels = new Dictionary<string, TMP_Text>();
    readonly Dictionary<string, TMP_Text> enemyHeaderLabels = new Dictionary<string, TMP_Text>();
    readonly Dictionary<int, Sprite> mapBackgroundPreviewCache = new Dictionary<int, Sprite>();
    readonly List<Button> fullscreenMapTileButtons = new List<Button>();
    ScrollRect leftSettingsScrollRect;
    RectTransform leftSettingsViewportRect;
    RectTransform leftSettingsContentRect;
    RectTransform enemyTableRootRect;
    RectTransform weaponSettingsRootRect;
    GameObject mapSelectionOverlayObject;
    TMP_Text mapSelectionOverlayTitleText;
    Button mapSelectionOverlayCloseButton;
    ScrollRect mapSelectionScrollRect;
    RectTransform mapSelectionContentRect;
    readonly List<Button> mapSelectionTileButtons = new List<Button>();
    bool leftSettingsScrollInitialized;
    LobbyScreen currentScreen = LobbyScreen.MapSelection;
    LobbyScreen previousMapScreenBeforeDeveloperSettings = LobbyScreen.MapSelection;
    string selectedMapId;
    GameObject lobbyTopBarRootObject;
    GameObject fullScreenLobbyRootObject;
    RectTransform fullScreenLobbyRootRect;
    TMP_Text lobbyTopBarNicknameText;
    TMP_Text lobbyTopBarGamesText;
    TMP_Text lobbyTopBarLevelXpText;
    TMP_Text lobbyTopBarAstronsText;
    GameObject lobbyTopStatBannerObject;
    Button exitLobbyButton;
    TMP_Text exitLobbyText;
    Button developerSettingsButton;
    TMP_Text developerSettingsText;
    Button launchButton;
    TMP_Text launchText;
    Button developerBackButton;
    TMP_Text developerBackText;
    Button developerGunSetupButton;
    TMP_Text developerGunSetupText;
    Button developerCheatButton;
    TMP_Text developerCheatText;
    GameObject developerCheatOverlayObject;
    TMP_Text developerCheatAstronsText;
    TMP_Text developerCheatStatusText;
    Button developerCheatAddMoneyButton;
    Button developerCheatCloseButton;
    GameObject mapSelectionRootObject;
    RectTransform mapSelectionTilesRootRect;
    TMP_Text mapSelectionScreenTitleText;
    GameObject mapDetailsRootObject;
    Image mapDetailsPreviewImage;
    TMP_Text mapDetailsNameText;
    TMP_Text mapDetailsDescriptionText;
    GameObject developerSettingsRootObject;

    CanvasGroup EnsureCanvasGroup()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = gameObject.AddComponent<CanvasGroup>();
        }

        return cg;
    }

    RectTransform EnsureFullScreenLobbyRoot()
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null)
            return transform as RectTransform;

        bool createdRoot = false;
        if (fullScreenLobbyRootObject == null || !fullScreenLobbyRootObject.scene.IsValid())
        {
            Transform existing = canvas.transform.Find("LobbyFullScreenRoot");
            if (existing != null)
                fullScreenLobbyRootObject = existing.gameObject;
            else
            {
                fullScreenLobbyRootObject = new GameObject("LobbyFullScreenRoot", typeof(RectTransform));
                createdRoot = true;
            }
        }

        if (fullScreenLobbyRootObject.transform.parent != canvas.transform)
            fullScreenLobbyRootObject.transform.SetParent(canvas.transform, false);

        fullScreenLobbyRootRect = fullScreenLobbyRootObject.GetComponent<RectTransform>();
        fullScreenLobbyRootRect.anchorMin = Vector2.zero;
        fullScreenLobbyRootRect.anchorMax = Vector2.one;
        fullScreenLobbyRootRect.pivot = new Vector2(0.5f, 0.5f);
        fullScreenLobbyRootRect.offsetMin = Vector2.zero;
        fullScreenLobbyRootRect.offsetMax = Vector2.zero;
        fullScreenLobbyRootRect.anchoredPosition = Vector2.zero;
        fullScreenLobbyRootObject.transform.SetAsLastSibling();
        if (createdRoot)
            fullScreenLobbyRootObject.SetActive(false);
        return fullScreenLobbyRootRect;
    }

    bool ShouldShowFullScreenLobby()
    {
        if (!PhotonNetwork.InRoom)
            return false;
        if (RoomSettings.GetSessionState() != RoomSettings.SessionStateInLobby)
            return false;

        CanvasGroup cg = EnsureCanvasGroup();
        return cg != null && cg.alpha > 0.01f && cg.interactable && cg.blocksRaycasts;
    }

    void EnsureLobbyRootFullScreen()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    void EnsureFullScreenLobbyUiExists()
    {
        RectTransform fullScreenRoot = EnsureFullScreenLobbyRoot();
        if (lobbyTopBarRootObject == null || !lobbyTopBarRootObject.scene.IsValid())
        {
            Transform misplaced = transform.Find("LobbyTopBarRoot");
            if (misplaced != null)
            {
                lobbyTopBarRootObject = misplaced.gameObject;
                if (fullScreenRoot != null)
                    lobbyTopBarRootObject.transform.SetParent(fullScreenRoot.transform, false);
            }
            else
            {
                lobbyTopBarRootObject = FindOrCreateChild(fullScreenRoot != null ? fullScreenRoot.gameObject : gameObject, "LobbyTopBarRoot", typeof(RectTransform), typeof(Image));
            }
            RectTransform rect = lobbyTopBarRootObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1440f, 120f);

            Image bg = lobbyTopBarRootObject.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = false;

            lobbyTopBarNicknameText = CreateTopBarText("LobbyTopBarNickname", new Vector2(34f, -42f), new Vector2(310f, 42f), TextAlignmentOptions.Left);
            lobbyTopBarGamesText = CreateTopBarText("LobbyTopBarGames", new Vector2(394f, -40f), new Vector2(170f, 42f), TextAlignmentOptions.Left);
            lobbyTopBarLevelXpText = CreateTopBarText("LobbyTopBarLevelXp", new Vector2(592f, -40f), new Vector2(420f, 42f), TextAlignmentOptions.Left);
            lobbyTopBarAstronsText = CreateTopBarText("LobbyTopBarAstrons", new Vector2(1112f, -40f), new Vector2(266f, 42f), TextAlignmentOptions.Left);
        }
        else if (fullScreenRoot != null && lobbyTopBarRootObject.transform.parent != fullScreenRoot.transform)
        {
            lobbyTopBarRootObject.transform.SetParent(fullScreenRoot.transform, false);
        }

        EnsureLobbyActionButtons();
        EnsureLobbyMapSelectionScreen();
        EnsureLobbyMapDetailsScreen();
        EnsureLobbyDeveloperSettingsRoot();
        EnsureLobbyCheatOverlay();
    }

    TMP_Text CreateTopBarText(string name, Vector2 anchoredPosition, Vector2 size, TextAlignmentOptions alignment)
    {
        Transform existing = lobbyTopBarRootObject.transform.Find(name);
        TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;
        if (text == null)
            text = CreateStandaloneLabel(lobbyTopBarRootObject.transform, name, string.Empty, anchoredPosition, size, 26f, alignment);

        text.fontSize = 26f;
        text.alignment = alignment;
        text.characterSpacing = 0f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = new Color(0.95f, 0.98f, 1f, 1f);
        return text;
    }

    void EnsureLobbyActionButtons()
    {
        RectTransform fullScreenRoot = EnsureFullScreenLobbyRoot();
        if ((exitLobbyButton == null || !exitLobbyButton.gameObject.scene.IsValid()) && fullScreenRoot != null)
        {
            Transform existing = fullScreenRoot.transform.Find("LobbyExitProfileBackButton");
            if (existing != null)
                exitLobbyButton = existing.GetComponent<Button>();
        }
        if ((developerSettingsButton == null || !developerSettingsButton.gameObject.scene.IsValid()) && fullScreenRoot != null)
        {
            Transform existing = fullScreenRoot.transform.Find("LobbyDeveloperSettingsWideProfileBackButton");
            if (existing == null)
                existing = fullScreenRoot.transform.Find("LobbyDeveloperSettingsProfileBackButton");
            if (existing != null)
                developerSettingsButton = existing.GetComponent<Button>();
        }
        if ((developerBackButton == null || !developerBackButton.gameObject.scene.IsValid()) && fullScreenRoot != null)
        {
            Transform existing = fullScreenRoot.transform.Find("LobbyDeveloperBackProfileBackButton");
            if (existing != null)
                developerBackButton = existing.GetComponent<Button>();
        }
        if ((launchButton == null || !launchButton.gameObject.scene.IsValid()) && fullScreenRoot != null)
        {
            Transform existing = fullScreenRoot.transform.Find("LobbyLaunchSaveAndRunButton");
            if (existing != null)
                launchButton = existing.GetComponent<Button>();
        }

        exitLobbyButton = EnsureTopActionButton(ref exitLobbyText, exitLobbyButton, "LobbyExitProfileBackButton", "LobbyExitProfileBackText", "EXIT LOBBY", OnExitLobbyClicked);
        developerSettingsButton = EnsureBottomActionButton(ref developerSettingsText, developerSettingsButton, "LobbyDeveloperSettingsWideProfileBackButton", "LobbyDeveloperSettingsWideProfileBackText", "DEVELOPER SETTINGS", OnDeveloperSettingsClicked);
        developerBackButton = EnsureTopActionButton(ref developerBackText, developerBackButton, "LobbyDeveloperBackProfileBackButton", "LobbyDeveloperBackProfileBackText", "BACK", OnDeveloperBackClicked);
        launchButton = EnsureBottomRightActionButton(ref launchText, launchButton, "LobbyLaunchSaveAndRunButton", "LobbyLaunchSaveAndRunText", "LAUNCH", OnLaunchClicked);
        developerGunSetupButton = EnsureTopActionButton(ref developerGunSetupText, developerGunSetupButton, "LobbyDeveloperGunSetupWideProfileBackButton", "LobbyDeveloperGunSetupWideProfileBackText", "GUN SETUP", OpenGunSetup);
        developerCheatButton = EnsureTopActionButton(ref developerCheatText, developerCheatButton, "LobbyDeveloperCheatWideProfileBackButton", "LobbyDeveloperCheatWideProfileBackText", "CHEAT", OnDeveloperCheatClicked);

        ApplyLobbyBackPalette(exitLobbyButton);
        ApplyLobbyBackPalette(developerSettingsButton);
        ApplyLobbyBackPalette(developerBackButton);
        ApplyLobbyBackPalette(developerGunSetupButton);
        ApplyLobbyBackPalette(developerCheatButton);

        if (fullScreenRoot != null)
        {
            if (exitLobbyButton != null && exitLobbyButton.transform.parent != fullScreenRoot.transform)
                exitLobbyButton.transform.SetParent(fullScreenRoot.transform, false);
            if (developerSettingsButton != null && developerSettingsButton.transform.parent != fullScreenRoot.transform)
                developerSettingsButton.transform.SetParent(fullScreenRoot.transform, false);
            if (developerBackButton != null && developerBackButton.transform.parent != fullScreenRoot.transform)
                developerBackButton.transform.SetParent(fullScreenRoot.transform, false);
            if (launchButton != null && launchButton.transform.parent != fullScreenRoot.transform)
                launchButton.transform.SetParent(fullScreenRoot.transform, false);
            if (developerGunSetupButton != null && developerGunSetupButton.transform.parent != fullScreenRoot.transform)
                developerGunSetupButton.transform.SetParent(fullScreenRoot.transform, false);
            if (developerCheatButton != null && developerCheatButton.transform.parent != fullScreenRoot.transform)
                developerCheatButton.transform.SetParent(fullScreenRoot.transform, false);
        }

        HideLegacyLobbyButtons();
    }

    Button EnsureTopActionButton(ref TMP_Text textField, Button existingButton, string buttonName, string textName, string label, UnityEngine.Events.UnityAction callback)
    {
        Button button = EnsureSettingButton(ref textField, existingButton, buttonName, textName, Vector2.zero, callback);
        if (button == null)
            return null;

        textField.text = label;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
        }

        return button;
    }

    Button EnsureBottomActionButton(ref TMP_Text textField, Button existingButton, string buttonName, string textName, string label, UnityEngine.Events.UnityAction callback)
    {
        Button button = EnsureSettingButton(ref textField, existingButton, buttonName, textName, Vector2.zero, callback);
        if (button == null)
            return null;

        textField.text = label;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
        }

        return button;
    }

    Button EnsureBottomRightActionButton(ref TMP_Text textField, Button existingButton, string buttonName, string textName, string label, UnityEngine.Events.UnityAction callback)
    {
        Button button = EnsureSettingButton(ref textField, existingButton, buttonName, textName, Vector2.zero, callback);
        if (button == null)
            return null;

        textField.text = label;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
        }

        return button;
    }

    void ApplyLobbyBackPalette(Button button)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.19f, 0.33f, 0.98f);
        colors.highlightedColor = new Color(0.18f, 0.28f, 0.46f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.08f, 0.12f, 0.22f, 1f);
        colors.disabledColor = new Color(0.18f, 0.24f, 0.32f, 0.48f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    void HideLegacyLobbyButtons()
    {
        foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (button == null || !button.gameObject.scene.IsValid())
                continue;

            if (button == developerSettingsButton)
                continue;

            if (button.gameObject.name == "LobbyDeveloperSettingsProfileBackButton")
                button.gameObject.SetActive(false);
        }
    }

    void EnsureLobbyMapSelectionScreen()
    {
        RectTransform fullScreenRoot = EnsureFullScreenLobbyRoot();
        if (mapSelectionRootObject == null || !mapSelectionRootObject.scene.IsValid())
        {
            Transform misplaced = transform.Find("LobbyMapSelectionScreen");
            if (misplaced != null)
            {
                mapSelectionRootObject = misplaced.gameObject;
                if (fullScreenRoot != null)
                    mapSelectionRootObject.transform.SetParent(fullScreenRoot.transform, false);
            }
            else
            {
                mapSelectionRootObject = FindOrCreateChild(fullScreenRoot != null ? fullScreenRoot.gameObject : gameObject, "LobbyMapSelectionScreen", typeof(RectTransform), typeof(Image));
            }
            RectTransform rootRect = mapSelectionRootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Image rootImage = mapSelectionRootObject.GetComponent<Image>();
            rootImage.color = new Color(0f, 0f, 0f, 0f);
            rootImage.raycastTarget = false;

            mapSelectionScreenTitleText = CreateStandaloneLabel(mapSelectionRootObject.transform, "MapSelectionHeader", "SELECT MAP", new Vector2(FullScreenSideMargin, -116f), new Vector2(420f, 34f), 30f, TextAlignmentOptions.Left);

            GameObject tilesRoot = FindOrCreateChild(mapSelectionRootObject, "MapSelectionTilesRoot", typeof(RectTransform));
            mapSelectionTilesRootRect = tilesRoot.GetComponent<RectTransform>();
            mapSelectionTilesRootRect.anchorMin = new Vector2(0f, 1f);
            mapSelectionTilesRootRect.anchorMax = new Vector2(0f, 1f);
            mapSelectionTilesRootRect.pivot = new Vector2(0f, 1f);
            mapSelectionTilesRootRect.anchoredPosition = new Vector2(FullScreenSideMargin, -160f);
            mapSelectionTilesRootRect.sizeDelta = new Vector2(1700f, 760f);

            fullscreenMapTileButtons.Clear();
            IReadOnlyList<LobbyMapDefinition> maps = LobbyMapCatalog.AllMaps;
            for (int i = 0; i < maps.Count; i++)
            {
                LobbyMapDefinition map = maps[i];
                GameObject tileObject = new GameObject("FullScreenMapTile_" + map.Id, typeof(RectTransform), typeof(Image), typeof(Button));
                tileObject.transform.SetParent(mapSelectionTilesRootRect, false);
                Image tileImage = tileObject.GetComponent<Image>();
                tileImage.color = new Color(0.1f, 0.16f, 0.24f, 0.96f);
                tileImage.type = Image.Type.Sliced;
                Button tileButton = tileObject.GetComponent<Button>();
                string mapId = map.Id;
                tileButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlayClick();
                    OnMapTileSelected(mapId);
                });
                fullscreenMapTileButtons.Add(tileButton);
            }
        }
        else if (fullScreenRoot != null && mapSelectionRootObject.transform.parent != fullScreenRoot.transform)
        {
            mapSelectionRootObject.transform.SetParent(fullScreenRoot.transform, false);
        }
    }

    void EnsureLobbyMapDetailsScreen()
    {
        RectTransform fullScreenRoot = EnsureFullScreenLobbyRoot();
        if (mapDetailsRootObject == null || !mapDetailsRootObject.scene.IsValid())
        {
            Transform misplaced = transform.Find("LobbyMapDetailsScreen");
            if (misplaced != null)
            {
                mapDetailsRootObject = misplaced.gameObject;
                if (fullScreenRoot != null)
                    mapDetailsRootObject.transform.SetParent(fullScreenRoot.transform, false);
            }
            else
            {
                mapDetailsRootObject = FindOrCreateChild(fullScreenRoot != null ? fullScreenRoot.gameObject : gameObject, "LobbyMapDetailsScreen", typeof(RectTransform), typeof(Image));
            }
            RectTransform rootRect = mapDetailsRootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Image rootImage = mapDetailsRootObject.GetComponent<Image>();
            rootImage.color = new Color(0f, 0f, 0f, 0f);
            rootImage.raycastTarget = false;

            GameObject previewObject = FindOrCreateChild(mapDetailsRootObject, "MapDetailsPreview", typeof(RectTransform), typeof(Image));
            RectTransform previewRect = previewObject.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0f, 1f);
            previewRect.anchorMax = new Vector2(0f, 1f);
            previewRect.pivot = new Vector2(0f, 1f);
            previewRect.anchoredPosition = new Vector2(FullScreenSideMargin, -170f);
            previewRect.sizeDelta = new Vector2(980f, 720f);
            mapDetailsPreviewImage = previewObject.GetComponent<Image>();
            mapDetailsPreviewImage.color = Color.white;
            mapDetailsPreviewImage.raycastTarget = false;

            mapDetailsNameText = CreateStandaloneLabel(mapDetailsRootObject.transform, "MapDetailsName", "MAP", new Vector2(1048f, -170f), new Vector2(420f, 40f), 30f, TextAlignmentOptions.Left);
            mapDetailsDescriptionText = CreateStandaloneLabel(mapDetailsRootObject.transform, "MapDetailsDescription", string.Empty, new Vector2(1048f, -234f), new Vector2(460f, 540f), 22f, TextAlignmentOptions.TopLeft);
            mapDetailsDescriptionText.fontStyle = FontStyles.Normal;
            mapDetailsDescriptionText.textWrappingMode = TextWrappingModes.Normal;
            mapDetailsDescriptionText.lineSpacing = 6f;
        }
        else if (fullScreenRoot != null && mapDetailsRootObject.transform.parent != fullScreenRoot.transform)
        {
            mapDetailsRootObject.transform.SetParent(fullScreenRoot.transform, false);
        }
    }

    void EnsureLobbyDeveloperSettingsRoot()
    {
        RectTransform fullScreenRoot = EnsureFullScreenLobbyRoot();
        if (developerSettingsRootObject == null || !developerSettingsRootObject.scene.IsValid())
        {
            Transform misplaced = transform.Find("LobbyDeveloperSettingsScreen");
            if (misplaced != null)
            {
                developerSettingsRootObject = misplaced.gameObject;
                if (fullScreenRoot != null)
                    developerSettingsRootObject.transform.SetParent(fullScreenRoot.transform, false);
            }
            else
            {
                developerSettingsRootObject = FindOrCreateChild(fullScreenRoot != null ? fullScreenRoot.gameObject : gameObject, "LobbyDeveloperSettingsScreen", typeof(RectTransform));
            }
            RectTransform rootRect = developerSettingsRootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }
        else if (fullScreenRoot != null && developerSettingsRootObject.transform.parent != fullScreenRoot.transform)
        {
            developerSettingsRootObject.transform.SetParent(fullScreenRoot.transform, false);
        }
    }

    void SwitchLobbyScreen(LobbyScreen screen)
    {
        currentScreen = screen;
        RefreshLobbyScreenVisibility();
        RefreshLobbyTopBar();
        RefreshLobbyScreenContent();
    }

    void RefreshLobbyTopBar()
    {
        EnsureFullScreenLobbyUiExists();

        PlayerProfileData profile = PlayerProfileService.Instance != null ? PlayerProfileService.Instance.CurrentProfile : null;
        string nickname = profile != null && !string.IsNullOrWhiteSpace(profile.Nickname)
            ? profile.Nickname
            : (!string.IsNullOrWhiteSpace(PhotonNetwork.NickName) ? PhotonNetwork.NickName : "Pilot");
        int games = profile != null ? profile.GamesPlayed : 0;
        int xp = profile != null ? profile.TotalXp : 0;
        int astrons = profile != null ? profile.Astrons : 0;
        int level = RoundXpBalance.GetLevelForTotalXp(xp);

        if (lobbyTopBarNicknameText != null)
            lobbyTopBarNicknameText.text = nickname;
        if (lobbyTopBarGamesText != null)
            lobbyTopBarGamesText.text = "Games: " + games;
        if (lobbyTopBarLevelXpText != null)
            lobbyTopBarLevelXpText.text = "Level: " + level + "  XP: " + xp;
        if (lobbyTopBarAstronsText != null)
            lobbyTopBarAstronsText.text = "Astrons: " + astrons;
    }

    void EnsureLobbyTopStatBanner(float rootWidth)
    {
        if (lobbyTopBarRootObject == null)
            return;

        if (lobbyTopStatBannerObject == null)
        {
            lobbyTopStatBannerObject = new GameObject("LobbyTopStatBanner", typeof(RectTransform), typeof(Image));
            lobbyTopStatBannerObject.transform.SetParent(lobbyTopBarRootObject.transform, false);

            GameObject innerPanelObject = new GameObject("InnerPanel", typeof(RectTransform), typeof(Image));
            innerPanelObject.transform.SetParent(lobbyTopStatBannerObject.transform, false);

            GameObject topAccentObject = new GameObject("TopAccent", typeof(RectTransform), typeof(Image));
            topAccentObject.transform.SetParent(lobbyTopStatBannerObject.transform, false);

            GameObject bottomAccentObject = new GameObject("BottomAccent", typeof(RectTransform), typeof(Image));
            bottomAccentObject.transform.SetParent(lobbyTopStatBannerObject.transform, false);

            GameObject leftAccentObject = new GameObject("LeftAccent", typeof(RectTransform), typeof(Image));
            leftAccentObject.transform.SetParent(lobbyTopStatBannerObject.transform, false);

            GameObject rightAccentObject = new GameObject("RightAccent", typeof(RectTransform), typeof(Image));
            rightAccentObject.transform.SetParent(lobbyTopStatBannerObject.transform, false);
        }

        float bannerX = 168f;
        float bannerWidth = Mathf.Clamp(rootWidth - bannerX - 270f, 620f, 1120f);

        RectTransform rect = lobbyTopStatBannerObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(bannerX, -40f);
        rect.sizeDelta = new Vector2(bannerWidth, 58f);

        Image frame = lobbyTopStatBannerObject.GetComponent<Image>();
        frame.color = new Color(0.33f, 0.39f, 0.47f, 0.94f);
        frame.raycastTarget = false;

        Transform innerPanel = lobbyTopStatBannerObject.transform.Find("InnerPanel");
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

        ConfigureLobbyTopBannerAccent("TopAccent", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(128f, 4f), new Color(0.35f, 0.82f, 1f, 0.32f));
        ConfigureLobbyTopBannerAccent("BottomAccent", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(96f, 3f), new Color(0.35f, 0.82f, 1f, 0.24f));
        ConfigureLobbyTopBannerAccent("LeftAccent", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(6f, 22f), new Color(0.35f, 0.82f, 1f, 0.78f));
        ConfigureLobbyTopBannerAccent("RightAccent", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(6f, 22f), new Color(0.35f, 0.82f, 1f, 0.78f));

        lobbyTopStatBannerObject.transform.SetAsFirstSibling();
    }

    void EnsureLobbyCheatOverlay()
    {
        RectTransform fullScreenRoot = EnsureFullScreenLobbyRoot();
        if (developerCheatOverlayObject != null && developerCheatOverlayObject.scene.IsValid())
        {
            if (fullScreenRoot != null && developerCheatOverlayObject.transform.parent != fullScreenRoot.transform)
                developerCheatOverlayObject.transform.SetParent(fullScreenRoot.transform, false);
            return;
        }

        GameObject overlayObject = new GameObject("LobbyDeveloperCheatOverlay", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(fullScreenRoot != null ? fullScreenRoot.transform : transform, false);
        developerCheatOverlayObject = overlayObject;

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlay = overlayObject.GetComponent<Image>();
        overlay.color = new Color(0.03f, 0.04f, 0.06f, 0.72f);

        GameObject panel = new GameObject("LobbyDeveloperCheatPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(overlayObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 6f);
        panelRect.sizeDelta = new Vector2(620f, 380f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.11f, 0.1f, 0.14f, 0.98f);

        TMP_Text title = CreateStandaloneLabel(panel.transform, "CheatBrowserTitle", "CHEAT", new Vector2(100f, -40f), new Vector2(420f, 34f), 30f, TextAlignmentOptions.Center);
        title.characterSpacing = 3f;
        title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);

        TMP_Text hint = CreateStandaloneLabel(panel.transform, "CheatBrowserHint", "This is temporary solution to speed up tests.", new Vector2(50f, -106f), new Vector2(520f, 64f), 19f, TextAlignmentOptions.Center);
        hint.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        hint.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        hint.rectTransform.pivot = new Vector2(0.5f, 1f);
        hint.fontStyle = FontStyles.Normal;
        hint.textWrappingMode = TextWrappingModes.Normal;
        hint.color = new Color(0.86f, 0.9f, 0.96f, 0.96f);

        developerCheatAstronsText = CreateStandaloneLabel(panel.transform, "CheatAstronsText", "Astrons: 0", new Vector2(90f, -166f), new Vector2(440f, 28f), 20f, TextAlignmentOptions.Center);
        developerCheatAstronsText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        developerCheatAstronsText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        developerCheatAstronsText.rectTransform.pivot = new Vector2(0.5f, 1f);
        developerCheatAstronsText.fontStyle = FontStyles.Normal;
        developerCheatAstronsText.color = new Color(0.94f, 0.84f, 0.44f, 1f);

        developerCheatAddMoneyButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatAddMoneyButton", "ADD MONEY", new Vector2(0f, -212f), new Vector2(260f, 62f), new Color(0.5f, 0.22f, 0.18f, 1f), new Color(0.7f, 0.3f, 0.22f, 1f), OnDeveloperCheatAddMoneyClicked);

        developerCheatStatusText = CreateStandaloneLabel(panel.transform, "CheatStatusText", string.Empty, new Vector2(60f, -292f), new Vector2(500f, 28f), 17f, TextAlignmentOptions.Center);
        developerCheatStatusText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        developerCheatStatusText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        developerCheatStatusText.rectTransform.pivot = new Vector2(0.5f, 1f);
        developerCheatStatusText.fontStyle = FontStyles.Normal;
        developerCheatStatusText.color = new Color(0.74f, 0.86f, 0.94f, 0.96f);

        developerCheatCloseButton = CreateLobbyOverlayButton(panel.transform, "LobbyDeveloperCheatCloseButton", "CLOSE", new Vector2(0f, -318f), new Vector2(220f, 52f), new Color(0.16f, 0.22f, 0.3f, 0.98f), new Color(0.22f, 0.3f, 0.4f, 1f), HideDeveloperCheatOverlay);

        developerCheatOverlayObject.SetActive(false);
    }

    Button CreateLobbyOverlayButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Color baseColor, Color highlightedColor, UnityEngine.Events.UnityAction callback)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = baseColor;
        image.type = Image.Type.Sliced;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => PlayUiClickAndInvoke(callback));
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = highlightedColor;
        colors.selectedColor = highlightedColor;
        colors.pressedColor = baseColor * 0.82f;
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TMP_Text text = CreateStandaloneLabel(buttonObject.transform, name + "Text", label, Vector2.zero, size, 24f, TextAlignmentOptions.Center);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.fontSize = 24f;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 2f;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        return button;
    }

    void PlayUiClickAndInvoke(UnityEngine.Events.UnityAction callback)
    {
        AudioManager.Instance?.PlayClick();
        callback?.Invoke();
    }

    void HideDeveloperCheatOverlay()
    {
        if (developerCheatOverlayObject != null)
            developerCheatOverlayObject.SetActive(false);
    }

    void HideFullScreenLobbyFlow(bool resetScreen = true)
    {
        HideDeveloperCheatOverlay();

        if (mapSelectionRootObject != null)
            mapSelectionRootObject.SetActive(false);
        if (mapDetailsRootObject != null)
            mapDetailsRootObject.SetActive(false);
        if (developerSettingsRootObject != null)
            developerSettingsRootObject.SetActive(false);
        if (lobbyTopBarRootObject != null)
            lobbyTopBarRootObject.SetActive(false);
        if (fullScreenLobbyRootObject != null)
            fullScreenLobbyRootObject.SetActive(false);

        if (exitLobbyButton != null)
            exitLobbyButton.gameObject.SetActive(false);
        if (developerSettingsButton != null)
            developerSettingsButton.gameObject.SetActive(false);
        if (launchButton != null)
            launchButton.gameObject.SetActive(false);
        if (developerBackButton != null)
            developerBackButton.gameObject.SetActive(false);
        if (developerGunSetupButton != null)
            developerGunSetupButton.gameObject.SetActive(false);
        if (developerCheatButton != null)
            developerCheatButton.gameObject.SetActive(false);

        if (leftSettingsViewportRect != null)
            leftSettingsViewportRect.gameObject.SetActive(false);
        if (enemyTableRootRect != null)
            enemyTableRootRect.gameObject.SetActive(false);
        if (weaponSettingsRootRect != null)
            weaponSettingsRootRect.gameObject.SetActive(false);

        if (resetScreen)
        {
            currentScreen = LobbyScreen.MapSelection;
            if (PhotonNetwork.InRoom)
                selectedMapId = RoomSettings.GetSelectedLobbyMapId();
        }
    }

    void RefreshDeveloperCheatOverlay(string statusMessage = null, bool busy = false)
    {
        if (developerCheatOverlayObject == null)
            return;

        PlayerProfileData profile = PlayerProfileService.Instance != null ? PlayerProfileService.Instance.CurrentProfile : null;
        int astrons = profile != null ? profile.Astrons : 0;
        if (developerCheatAstronsText != null)
            developerCheatAstronsText.text = "Astrons: " + astrons;

        if (statusMessage != null && developerCheatStatusText != null)
            developerCheatStatusText.text = statusMessage;

        if (developerCheatAddMoneyButton != null)
            developerCheatAddMoneyButton.interactable = !busy;
        if (developerCheatCloseButton != null)
            developerCheatCloseButton.interactable = !busy;
    }

    void ConfigureLobbyTopBannerAccent(string childName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        if (lobbyTopStatBannerObject == null)
            return;

        Transform child = lobbyTopStatBannerObject.transform.Find(childName);
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

    void RefreshLobbyScreenVisibility()
    {
        EnsureFullScreenLobbyUiExists();

        bool shouldShowFullScreen = ShouldShowFullScreenLobby();
        if (!shouldShowFullScreen)
        {
            if (mapSelectionRootObject != null)
                mapSelectionRootObject.SetActive(false);
            if (mapDetailsRootObject != null)
                mapDetailsRootObject.SetActive(false);
            if (developerSettingsRootObject != null)
                developerSettingsRootObject.SetActive(false);
            if (lobbyTopBarRootObject != null)
                lobbyTopBarRootObject.SetActive(false);
            if (fullScreenLobbyRootObject != null)
                fullScreenLobbyRootObject.SetActive(false);
            if (exitLobbyButton != null)
                exitLobbyButton.gameObject.SetActive(false);
            if (developerSettingsButton != null)
                developerSettingsButton.gameObject.SetActive(false);
            if (launchButton != null)
                launchButton.gameObject.SetActive(false);
            if (developerBackButton != null)
                developerBackButton.gameObject.SetActive(false);
            if (developerGunSetupButton != null)
                developerGunSetupButton.gameObject.SetActive(false);
            if (developerCheatButton != null)
                developerCheatButton.gameObject.SetActive(false);
            if (leftSettingsViewportRect != null)
                leftSettingsViewportRect.gameObject.SetActive(false);
            if (enemyTableRootRect != null)
                enemyTableRootRect.gameObject.SetActive(false);
            if (weaponSettingsRootRect != null)
                weaponSettingsRootRect.gameObject.SetActive(false);
            return;
        }

        bool showMapSelection = currentScreen == LobbyScreen.MapSelection;
        bool showMapDetails = currentScreen == LobbyScreen.MapDetails;
        bool showDeveloperSettings = currentScreen == LobbyScreen.DeveloperSettings;

        if (mapSelectionRootObject != null)
        {
            mapSelectionRootObject.SetActive(showMapSelection);
            mapSelectionRootObject.transform.SetAsFirstSibling();
        }
        if (mapDetailsRootObject != null)
        {
            mapDetailsRootObject.SetActive(showMapDetails);
            mapDetailsRootObject.transform.SetAsFirstSibling();
        }
        if (developerSettingsRootObject != null)
        {
            developerSettingsRootObject.SetActive(showDeveloperSettings);
            developerSettingsRootObject.transform.SetAsFirstSibling();
        }
        if (lobbyTopBarRootObject != null)
            lobbyTopBarRootObject.SetActive(true);
        if (fullScreenLobbyRootObject != null)
        {
            fullScreenLobbyRootObject.SetActive(showMapSelection || showMapDetails || showDeveloperSettings);
            fullScreenLobbyRootObject.transform.SetAsLastSibling();
        }

        if (exitLobbyButton != null)
            exitLobbyButton.gameObject.SetActive(showMapSelection || showDeveloperSettings);
        if (developerSettingsButton != null)
            developerSettingsButton.gameObject.SetActive(showMapSelection || showMapDetails);
        if (launchButton != null)
            launchButton.gameObject.SetActive(showMapDetails);
        if (developerBackButton != null)
            developerBackButton.gameObject.SetActive(showMapDetails || showDeveloperSettings);
        if (developerGunSetupButton != null)
            developerGunSetupButton.gameObject.SetActive(showDeveloperSettings);
        if (developerCheatButton != null)
            developerCheatButton.gameObject.SetActive(showDeveloperSettings);
        if (!showDeveloperSettings)
            HideDeveloperCheatOverlay();

        if (leftSettingsViewportRect != null)
            leftSettingsViewportRect.gameObject.SetActive(showDeveloperSettings);
        if (enemyTableRootRect != null)
            enemyTableRootRect.gameObject.SetActive(showDeveloperSettings);
        if (weaponSettingsRootRect != null)
            weaponSettingsRootRect.gameObject.SetActive(false);
        if (gunSetupSettingButton != null)
            gunSetupSettingButton.gameObject.SetActive(false);

        if (playerStatusListText != null)
            playerStatusListText.gameObject.SetActive(false);
        if (readyButton != null)
            readyButton.gameObject.SetActive(false);
        if (backToRoundsButton != null)
            backToRoundsButton.gameObject.SetActive(false);
        if (mapSelectionButton != null)
            mapSelectionButton.gameObject.SetActive(false);
        if (mapSelectionOverlayObject != null)
            mapSelectionOverlayObject.SetActive(false);

        LayoutFullScreenLobbyUi();
    }

    void RefreshLobbyScreenContent()
    {
        RefreshFullScreenMapSelectionUi();
        RefreshMapDetailsUi();
        RefreshDeveloperSettingsUi();
    }

    void LayoutFullScreenLobbyUi()
    {
        RectTransform canvasRect = EnsureFullScreenLobbyRoot();
        float canvasWidth = canvasRect != null && canvasRect.rect.width > 0f ? canvasRect.rect.width : 1920f;
        float canvasHeight = canvasRect != null && canvasRect.rect.height > 0f ? canvasRect.rect.height : 1080f;
        float contentTop = FullScreenTopMargin + LobbyTopBarHeight + 56f;
        float bottomReserved = BottomWideButtonHeight + FullScreenBottomMargin + 30f;
        float usableHeight = Mathf.Max(420f, canvasHeight - contentTop - bottomReserved);
        float tileHeight = Mathf.Min(MapTileHeight, (usableHeight - MapTileSpacingY) * 0.5f);
        float tileWidth = Mathf.Min(MapTileWidth, (canvasWidth - FullScreenSideMargin * 2f - MapTileSpacingX * 2f) / 3f);
        float previewWidth = Mathf.Min(980f, canvasWidth * 0.56f);
        float previewHeight = Mathf.Min(720f, usableHeight);
        float detailsStartX = FullScreenSideMargin + previewWidth + 34f;
        float detailsWidth = Mathf.Max(320f, canvasWidth - detailsStartX - FullScreenSideMargin);

        if (lobbyTopBarRootObject != null)
        {
            RectTransform rect = lobbyTopBarRootObject.GetComponent<RectTransform>();
            float rootWidth = Mathf.Max(820f, canvasWidth);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -FullScreenTopMargin);
            rect.sizeDelta = new Vector2(rootWidth, 110f);
            EnsureLobbyTopStatBanner(rootWidth);

            if (lobbyTopBarNicknameText != null)
            {
                RectTransform nicknameRect = lobbyTopBarNicknameText.rectTransform;
                nicknameRect.anchorMin = new Vector2(0f, 1f);
                nicknameRect.anchorMax = new Vector2(0f, 1f);
                nicknameRect.pivot = new Vector2(0f, 0.5f);
                nicknameRect.anchoredPosition = new Vector2(26f, -40f);
                nicknameRect.sizeDelta = new Vector2(140f, 42f);
                lobbyTopBarNicknameText.fontSize = 30f;
                lobbyTopBarNicknameText.enableAutoSizing = true;
                lobbyTopBarNicknameText.fontSizeMin = 22f;
                lobbyTopBarNicknameText.fontSizeMax = 30f;
            }
            if (lobbyTopBarGamesText != null)
            {
                float bannerX = 168f;
                RectTransform gamesRect = lobbyTopBarGamesText.rectTransform;
                gamesRect.anchorMin = new Vector2(0f, 1f);
                gamesRect.anchorMax = new Vector2(0f, 1f);
                gamesRect.pivot = new Vector2(0f, 0.5f);
                gamesRect.anchoredPosition = new Vector2(bannerX + 22f, -40f);
                gamesRect.sizeDelta = new Vector2(165f, 42f);
                lobbyTopBarGamesText.fontSize = 30f;
                lobbyTopBarGamesText.enableAutoSizing = true;
                lobbyTopBarGamesText.fontSizeMin = 22f;
                lobbyTopBarGamesText.fontSizeMax = 30f;
            }
            if (lobbyTopBarLevelXpText != null)
            {
                float bannerX = 168f;
                RectTransform xpRect = lobbyTopBarLevelXpText.rectTransform;
                xpRect.anchorMin = new Vector2(0f, 1f);
                xpRect.anchorMax = new Vector2(0f, 1f);
                xpRect.pivot = new Vector2(0f, 0.5f);
                xpRect.anchoredPosition = new Vector2(bannerX + 210f, -40f);
                xpRect.sizeDelta = new Vector2(380f, 42f);
                lobbyTopBarLevelXpText.fontSize = 30f;
                lobbyTopBarLevelXpText.enableAutoSizing = true;
                lobbyTopBarLevelXpText.fontSizeMin = 22f;
                lobbyTopBarLevelXpText.fontSizeMax = 30f;
            }
            if (lobbyTopBarAstronsText != null)
            {
                float bannerX = 168f;
                float bannerWidth = Mathf.Clamp(rootWidth - bannerX - 270f, 620f, 1120f);
                RectTransform astronsRect = lobbyTopBarAstronsText.rectTransform;
                astronsRect.anchorMin = new Vector2(0f, 1f);
                astronsRect.anchorMax = new Vector2(0f, 1f);
                astronsRect.pivot = new Vector2(0f, 0.5f);
                astronsRect.anchoredPosition = new Vector2(bannerX + bannerWidth - 220f, -40f);
                astronsRect.sizeDelta = new Vector2(210f, 42f);
                lobbyTopBarAstronsText.fontSize = 30f;
                lobbyTopBarAstronsText.enableAutoSizing = true;
                lobbyTopBarAstronsText.fontSizeMin = 22f;
                lobbyTopBarAstronsText.fontSizeMax = 30f;
            }
            lobbyTopBarRootObject.transform.SetAsLastSibling();
        }

        LayoutActionButton(exitLobbyButton, new Vector2(-FullScreenSideMargin, -FullScreenTopMargin), new Vector2(TopActionButtonWidth, TopActionButtonHeight));
        LayoutActionButton(developerBackButton, new Vector2(-FullScreenSideMargin, -FullScreenTopMargin), new Vector2(220f, TopActionButtonHeight));
        LayoutBottomButton(developerSettingsButton, new Vector2(FullScreenSideMargin, FullScreenBottomMargin), new Vector2(390f, 66f), false);
        LayoutBottomButton(launchButton, new Vector2(-FullScreenSideMargin, FullScreenBottomMargin), new Vector2(360f, 108f), true);
        LayoutActionButton(developerGunSetupButton, new Vector2(-FullScreenSideMargin - 240f, -FullScreenTopMargin), new Vector2(220f, TopActionButtonHeight));
        LayoutActionButton(developerCheatButton, new Vector2(-FullScreenSideMargin - 480f, -FullScreenTopMargin), new Vector2(220f, TopActionButtonHeight));

        if (mapSelectionTilesRootRect != null)
        {
            int columns = 3;
            float totalWidth = columns * tileWidth + (columns - 1) * MapTileSpacingX;
            float startX = Mathf.Max(0f, (canvasWidth - FullScreenSideMargin * 2f - totalWidth) * 0.5f);
            mapSelectionTilesRootRect.anchoredPosition = new Vector2(FullScreenSideMargin, -contentTop);
            mapSelectionTilesRootRect.sizeDelta = new Vector2(canvasWidth - FullScreenSideMargin * 2f, usableHeight);
            for (int i = 0; i < fullscreenMapTileButtons.Count; i++)
            {
                RectTransform tileRect = fullscreenMapTileButtons[i].GetComponent<RectTransform>();
                int column = i % columns;
                int row = i / columns;
                tileRect.anchorMin = new Vector2(0f, 1f);
                tileRect.anchorMax = new Vector2(0f, 1f);
                tileRect.pivot = new Vector2(0f, 1f);
                tileRect.anchoredPosition = new Vector2(startX + column * (tileWidth + MapTileSpacingX), -row * (tileHeight + MapTileSpacingY));
                tileRect.sizeDelta = new Vector2(tileWidth, tileHeight);
            }
            if (mapSelectionScreenTitleText != null)
                mapSelectionScreenTitleText.rectTransform.anchoredPosition = new Vector2(FullScreenSideMargin, -(contentTop - 30f));
        }

        if (mapDetailsRootObject != null)
        {
            RectTransform previewRect = mapDetailsPreviewImage != null ? mapDetailsPreviewImage.rectTransform : null;
            if (previewRect != null)
            {
                previewRect.anchoredPosition = new Vector2(FullScreenSideMargin, -contentTop);
                previewRect.sizeDelta = new Vector2(previewWidth, previewHeight);
            }

            if (mapDetailsNameText != null)
            {
                RectTransform nameRect = mapDetailsNameText.rectTransform;
                nameRect.anchoredPosition = new Vector2(detailsStartX, -contentTop);
                nameRect.sizeDelta = new Vector2(detailsWidth, 40f);
            }

            if (mapDetailsDescriptionText != null)
            {
                RectTransform descRect = mapDetailsDescriptionText.rectTransform;
                descRect.anchoredPosition = new Vector2(detailsStartX, -contentTop - 64f);
                descRect.sizeDelta = new Vector2(detailsWidth, Mathf.Max(300f, previewHeight - 80f));
            }
        }
    }

    void LayoutActionButton(Button button, Vector2 anchoredFromTopRight, Vector2 size)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = anchoredFromTopRight;
        rect.sizeDelta = size;
        button.transform.SetAsLastSibling();
    }

    void LayoutBottomButton(Button button, Vector2 anchoredFromCorner, Vector2 size, bool rightAnchored)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = rightAnchored ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
        rect.anchorMax = rightAnchored ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
        rect.pivot = rightAnchored ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredFromCorner;
        rect.sizeDelta = size;
        button.transform.SetAsLastSibling();
    }

    void RefreshFullScreenMapSelectionUi()
    {
        EnsureFullScreenLobbyUiExists();
        LobbyMapDefinition selectedMap = LobbyMapCatalog.Get(selectedMapId) ?? LobbyMapCatalog.Get(RoomSettings.GetSelectedLobbyMapId()) ?? LobbyMapCatalog.GetDefault();
        bool isHost = PhotonNetwork.IsMasterClient;

        for (int i = 0; i < fullscreenMapTileButtons.Count && i < LobbyMapCatalog.AllMaps.Count; i++)
        {
            LobbyMapDefinition map = LobbyMapCatalog.AllMaps[i];
            Button tileButton = fullscreenMapTileButtons[i];
            EnsureFullScreenMapTileVisual(tileButton, map);

            Image tileImage = tileButton.GetComponent<Image>();
            Image previewImage = tileButton.transform.Find("TilePreviewImage")?.GetComponent<Image>();
            if (previewImage != null)
            {
                previewImage.sprite = LoadLobbyBackgroundSprite(map.MapBackgroundIndex);
                previewImage.color = Color.white;
            }

            bool isSelected = selectedMap != null && selectedMap.Id == map.Id;
            tileButton.interactable = isHost;
            ColorBlock tileColors = tileButton.colors;
            tileColors.normalColor = isSelected ? new Color(0.3f, 0.78f, 0.98f, 1f) : new Color(0.1f, 0.16f, 0.24f, 0.96f);
            tileColors.highlightedColor = isSelected ? new Color(0.38f, 0.86f, 1f, 1f) : new Color(0.16f, 0.24f, 0.36f, 1f);
            tileColors.selectedColor = tileColors.highlightedColor;
            tileColors.pressedColor = new Color(0.08f, 0.12f, 0.18f, 1f);
            tileColors.disabledColor = new Color(0.16f, 0.18f, 0.22f, 0.72f);
            tileColors.colorMultiplier = 1f;
            tileButton.colors = tileColors;

            if (tileImage != null)
                tileImage.color = tileColors.normalColor;
        }

        if (mapSelectionScreenTitleText != null)
            mapSelectionScreenTitleText.text = "MAP SELECTION";
    }

    void EnsureFullScreenMapTileVisual(Button tileButton, LobbyMapDefinition map)
    {
        if (tileButton == null)
            return;

        Image rootImage = tileButton.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.raycastTarget = true;
            rootImage.sprite = null;
            rootImage.type = Image.Type.Sliced;
        }

        GameObject previewObject = FindOrCreateChild(tileButton.gameObject, "TilePreviewImage", typeof(RectTransform), typeof(Image));
        Image previewImage = previewObject.GetComponent<Image>();
        RectTransform previewRect = previewImage.rectTransform;
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.pivot = new Vector2(0.5f, 0.5f);
        previewRect.offsetMin = new Vector2(4f, 4f);
        previewRect.offsetMax = new Vector2(-4f, -4f);
        previewImage.type = Image.Type.Simple;
        previewImage.preserveAspect = false;
        previewImage.raycastTarget = false;
        previewImage.color = Color.white;

        GameObject labelBackdropObject = FindOrCreateChild(tileButton.gameObject, "TileLabelBackdrop", typeof(RectTransform), typeof(Image));
        Image labelBackdrop = labelBackdropObject.GetComponent<Image>();
        RectTransform labelBackdropRect = labelBackdrop.rectTransform;
        labelBackdropRect.anchorMin = new Vector2(0f, 1f);
        labelBackdropRect.anchorMax = new Vector2(1f, 1f);
        labelBackdropRect.pivot = new Vector2(0.5f, 1f);
        labelBackdropRect.anchoredPosition = Vector2.zero;
        labelBackdropRect.sizeDelta = new Vector2(0f, 58f);
        labelBackdrop.color = new Color(0.02f, 0.04f, 0.08f, 0.7f);
        labelBackdrop.raycastTarget = false;

        Transform existingTitle = tileButton.transform.Find("TileTitle");
        TMP_Text tileTitle = existingTitle != null
            ? existingTitle.GetComponent<TMP_Text>()
            : CreateStandaloneLabel(tileButton.transform, "TileTitle", map.DisplayName, new Vector2(0f, -14f), new Vector2(360f, 30f), 26f, TextAlignmentOptions.Center);
        if (tileTitle != null)
        {
            tileTitle.text = map.DisplayName;
            RectTransform titleRect = tileTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -14f);
            titleRect.sizeDelta = new Vector2(420f, 30f);
            tileTitle.fontSize = 26f;
            tileTitle.alignment = TextAlignmentOptions.Center;
        }

        Transform existingSubtitle = tileButton.transform.Find("TileSubtitle");
        if (existingSubtitle != null)
            existingSubtitle.gameObject.SetActive(false);
    }

    void RefreshMapDetailsUi()
    {
        EnsureFullScreenLobbyUiExists();
        LobbyMapDefinition selectedMap = LobbyMapCatalog.Get(selectedMapId) ?? LobbyMapCatalog.Get(RoomSettings.GetSelectedLobbyMapId()) ?? LobbyMapCatalog.GetDefault();
        if (selectedMap == null)
            return;

        if (mapDetailsPreviewImage != null)
        {
            mapDetailsPreviewImage.sprite = LoadLobbyBackgroundSprite(selectedMap.MapBackgroundIndex);
            mapDetailsPreviewImage.color = Color.white;
        }

        if (mapDetailsNameText != null)
            mapDetailsNameText.text = selectedMap.DisplayName;
        if (mapDetailsDescriptionText != null)
            mapDetailsDescriptionText.text = selectedMap.Description;

        if (launchButton != null)
            launchButton.interactable = PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom && RoomSettings.GetSessionState() == RoomSettings.SessionStateInLobby;
    }

    void RefreshDeveloperSettingsUi()
    {
        EnsureHostSettingsUiExists();
        EnsureWeaponSettingsPanel();
        LayoutDeveloperSettingsRoots();
        LayoutDeveloperWeaponButtons();
    }

    void EnsureWeaponSettingsPanel()
    {
        if (weaponSettingsRootRect != null && weaponSettingsRootRect.gameObject.scene.IsValid())
            return;

        GameObject panelObject = FindOrCreateChild(developerSettingsRootObject != null ? developerSettingsRootObject : gameObject, "WeaponSettingsPanel", typeof(RectTransform), typeof(Image));
        weaponSettingsRootRect = panelObject.GetComponent<RectTransform>();
        weaponSettingsRootRect.anchorMin = new Vector2(0.5f, 1f);
        weaponSettingsRootRect.anchorMax = new Vector2(0.5f, 1f);
        weaponSettingsRootRect.pivot = new Vector2(0.5f, 1f);

        Image bg = panelObject.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = false;

        Transform staleTitle = panelObject.transform.Find("WeaponSettingsTitle");
        if (staleTitle != null)
            staleTitle.gameObject.SetActive(false);
    }

    void LayoutDeveloperSettingsRoots()
    {
        if (developerSettingsRootObject != null)
            developerSettingsRootObject.transform.SetAsLastSibling();

        if (leftSettingsViewportRect != null)
        {
            if (leftSettingsViewportRect.transform.parent != developerSettingsRootObject.transform)
                leftSettingsViewportRect.transform.SetParent(developerSettingsRootObject.transform, false);
            leftSettingsViewportRect.anchorMin = new Vector2(0f, 1f);
            leftSettingsViewportRect.anchorMax = new Vector2(0f, 1f);
            leftSettingsViewportRect.pivot = new Vector2(0f, 1f);
            leftSettingsViewportRect.anchoredPosition = new Vector2(FullScreenSideMargin, -118f);
            leftSettingsViewportRect.sizeDelta = new Vector2(660f, 840f);
        }

        if (enemyTableRootRect != null)
        {
            if (enemyTableRootRect.transform.parent != developerSettingsRootObject.transform)
                enemyTableRootRect.transform.SetParent(developerSettingsRootObject.transform, false);
            enemyTableRootRect.anchorMin = new Vector2(0f, 1f);
            enemyTableRootRect.anchorMax = new Vector2(0f, 1f);
            enemyTableRootRect.pivot = new Vector2(0f, 1f);
            enemyTableRootRect.anchoredPosition = new Vector2(740f, -118f);
            enemyTableRootRect.sizeDelta = new Vector2(1120f, 620f);
        }

        if (weaponSettingsRootRect != null)
        {
            if (weaponSettingsRootRect.transform.parent != developerSettingsRootObject.transform)
                weaponSettingsRootRect.transform.SetParent(developerSettingsRootObject.transform, false);
            weaponSettingsRootRect.anchorMin = new Vector2(0f, 1f);
            weaponSettingsRootRect.anchorMax = new Vector2(0f, 1f);
            weaponSettingsRootRect.pivot = new Vector2(0f, 1f);
            weaponSettingsRootRect.anchoredPosition = new Vector2(740f, -760f);
            weaponSettingsRootRect.sizeDelta = new Vector2(1120f, 190f);
        }
    }

    void LayoutDeveloperWeaponButtons()
    {
        if (weaponSettingsRootRect != null)
            weaponSettingsRootRect.gameObject.SetActive(false);
    }

    void AttachWeaponSettingToPanel(Button button, float x, float y, float width)
    {
        if (button == null || weaponSettingsRootRect == null)
            return;

        button.transform.SetParent(weaponSettingsRootRect, false);
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, 60f);
    }

    void Start()
    {
        PlayerMovement.gameStarted = false;
        PlayerShooting.gameStarted = false;
        EnsureLobbyRootFullScreen();

        EnsurePlayerStatusListExists();
        EnsureHostSettingsUiExists();
        EnsureLobbyNavigationUiExists();
        EnsureFullScreenLobbyUiExists();

        if (PhotonNetwork.InRoom)
        {
            selectedMapId = RoomSettings.GetSelectedLobbyMapId();
            ShowLobby();
            EnsureDefaultRoomSettings();
        }
        else
        {
            CanvasGroup cg = EnsureCanvasGroup();
            cg.alpha = 0;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }

        if (readyText != null)
        {
            readyText.text = "NOT READY";
        }

        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(ToggleReady);
            readyButton.onClick.AddListener(() => PlayUiClickAndInvoke(ToggleReady));
        }

        if (PhotonNetwork.InRoom)
        {
            SetReady(false);
        }

        RefreshPlayerStatusList();
        RefreshHostSettingsUi();
        RefreshLobbyTopBar();

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started && started)
        {
            HideLobby();
        }

        EnsureBottomActionButtonsLayout();
    }

    void Update()
    {
        CanvasGroup cg = EnsureCanvasGroup();
        if (cg != null && cg.alpha > 0.01f && cg.blocksRaycasts)
            SetGameplayHudVisible(false);
    }

    void LateUpdate()
    {
        CanvasGroup cg = EnsureCanvasGroup();
        if (cg != null && cg.alpha > 0.01f && cg.blocksRaycasts)
            SetGameplayHudVisible(false);
    }

    public override void OnJoinedRoom()
    {
        hasRecordedCurrentRound = false;
        EnsureLobbyRootFullScreen();
        EnsurePlayerStatusListExists();
        EnsureHostSettingsUiExists();
        EnsureLobbyNavigationUiExists();
        EnsureFullScreenLobbyUiExists();
        EnsureBottomActionButtonsLayout();
        EnsureDefaultRoomSettings();
        selectedMapId = RoomSettings.GetSelectedLobbyMapId();

        bool started = PhotonNetwork.CurrentRoom != null &&
                       PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
                       value is bool startedValue &&
                       startedValue;

        if (started)
        {
            HideLobby();
        }
        else
        {
            ShowLobby();
            SetReady(false);
            RefreshPlayerStatusList();
            RefreshHostSettingsUi();
            RefreshLobbyTopBar();
        }
    }

    void ToggleReady()
    {
        isReady = !isReady;
        SetReady(isReady);
    }

    void SetReady(bool ready)
    {
        isReady = ready;

        Hashtable props = new Hashtable();
        props["ready"] = ready;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        if (readyText != null)
        {
            readyText.text = ready ? "READY" : "NOT READY";
        }

        RefreshPlayerStatusList();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey("ready"))
        {
            RefreshPlayerStatusList();
            CheckAllReady();
        }

        if (ContainsLobbySettingChange(changedProps))
        {
            RefreshHostSettingsUi();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshPlayerStatusList();
        RefreshHostSettingsUi();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshPlayerStatusList();
        RefreshHostSettingsUi();
    }

    void CheckAllReady()
    {
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (!p.CustomProperties.TryGetValue("ready", out object readyValue))
            {
                return;
            }

            if (!(bool)readyValue)
            {
                return;
            }
        }

        if (PhotonNetwork.IsMasterClient)
        {
            StartGame();
        }
    }

    void StartGame()
    {
        NetworkManager.RememberCurrentLobbySettings();
        GameTimer.StartGame();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (ContainsLobbySettingChange(propertiesThatChanged))
        {
            selectedMapId = RoomSettings.GetSelectedLobbyMapId();
            RefreshHostSettingsUi();
            RefreshLobbyScreenContent();
            NetworkManager.RememberCurrentLobbySettings();
        }

        if (!propertiesThatChanged.ContainsKey("gameStarted"))
            return;

        bool started = false;
        if (propertiesThatChanged["gameStarted"] is bool startedValue)
        {
            started = startedValue;
        }

        if (started)
        {
            HideLobby();
            if (!hasRecordedCurrentRound)
            {
                hasRecordedCurrentRound = true;
                _ = RecordStartedGameAsync();
            }
        }
        else
        {
            PlayerMovement.gameStarted = false;
            PlayerShooting.gameStarted = false;
            hasRecordedCurrentRound = false;
            if (ShouldKeepLobbyHiddenForSummary())
            {
                HideLobby();
            }
            else
            {
                ShowLobby();
                SetReady(false);
                RefreshPlayerStatusList();
                RefreshHostSettingsUi();
                RefreshLobbyTopBar();
            }
        }
    }

    bool ShouldKeepLobbyHiddenForSummary()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.RoundResultsKey, out object snapshotValue) &&
            snapshotValue is string rawSnapshot &&
            !string.IsNullOrWhiteSpace(rawSnapshot))
        {
            return true;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.RoundEndReasonKey, out object endReasonValue) &&
            endReasonValue is string rawEndReason &&
            !string.IsNullOrWhiteSpace(rawEndReason))
        {
            return true;
        }

        return false;
    }

    void HideLobby()
    {
        PlayerMovement.gameStarted = true;
        PlayerShooting.gameStarted = true;
        SetGameplayHudVisible(true);
        HideMapSelectionOverlay();
        HideFullScreenLobbyFlow();

        CanvasGroup cg = EnsureCanvasGroup();
        cg.alpha = 0;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    void ShowLobby()
    {
        SetGameplayHudVisible(false);
        EnsureLobbyRootFullScreen();
        EnsureFullScreenLobbyUiExists();
        if (fullScreenLobbyRootObject != null)
        {
            fullScreenLobbyRootObject.SetActive(true);
            fullScreenLobbyRootObject.transform.SetAsLastSibling();
        }
        if (string.IsNullOrWhiteSpace(selectedMapId))
            selectedMapId = RoomSettings.GetSelectedLobbyMapId();

        CanvasGroup cg = EnsureCanvasGroup();
        cg.alpha = 1;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        HideMapSelectionOverlay();
        EnsureBottomActionButtonsLayout();
        SwitchLobbyScreen(LobbyScreen.MapSelection);
    }

    void SetGameplayHudVisible(bool visible)
    {
        CacheGameplayHudObjects();
        foreach (GameObject hudObject in gameplayHudObjectsByName.Values)
        {
            if (hudObject != null)
                ApplyHudVisibility(hudObject, visible);
        }
    }

    void CacheGameplayHudObjects()
    {
        for (int i = 0; i < GameplayHudObjectNames.Length; i++)
        {
            string hudName = GameplayHudObjectNames[i];
            if (!gameplayHudObjectsByName.ContainsKey(hudName) || gameplayHudObjectsByName[hudName] == null)
                gameplayHudObjectsByName[hudName] = FindSceneObjectByName(hudName);
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

    void HideDeprecatedSettingButton(string buttonName, string textName)
    {
        GameObject buttonObject = FindSceneObjectByName(buttonName);
        if (buttonObject != null)
            buttonObject.SetActive(false);

        GameObject textObject = FindSceneObjectByName(textName);
        if (textObject != null)
            textObject.SetActive(false);
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

    void EnsurePlayerStatusListExists()
    {
        if (playerStatusListText != null && playerStatusListText.gameObject.scene.IsValid())
            return;

        Transform existing = transform.Find("RoomPlayersText");
        if (existing != null)
        {
            playerStatusListText = existing.GetComponent<TMP_Text>();
            if (playerStatusListText != null)
                return;
        }

        GameObject textObject = new GameObject("RoomPlayersText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -26f);
        rect.sizeDelta = new Vector2(390f, 145f);

        playerStatusListText = textObject.GetComponent<TextMeshProUGUI>();
        playerStatusListText.fontSize = 22f;
        playerStatusListText.fontStyle = FontStyles.Bold;
        playerStatusListText.alignment = TextAlignmentOptions.TopLeft;
        playerStatusListText.textWrappingMode = TextWrappingModes.NoWrap;
        playerStatusListText.color = new Color(0.94f, 0.97f, 1f, 1f);
        playerStatusListText.text = string.Empty;
    }

    void EnsureHostSettingsUiExists()
    {
        EnsureSettingsLayoutContainers();
        EnsureLobbyMapUiExists();

        roundSettingButton = EnsureSettingButton(ref roundSettingText, roundSettingButton, "RoundSettingButton", "RoundSettingText", Vector2.zero, CycleRoundDuration);
        mapSizeSettingButton = EnsureSettingButton(ref mapSizeSettingText, mapSizeSettingButton, "MapSizeSettingButton", "MapSizeSettingText", Vector2.zero, CycleMapSize);
        mapBackgroundSettingButton = EnsureSettingButton(ref mapBackgroundSettingText, mapBackgroundSettingButton, "MapBackgroundSettingButton", "MapBackgroundSettingText", Vector2.zero, CycleMapBackground);
        visualEffectsSettingButton = EnsureSettingButton(ref visualEffectsSettingText, visualEffectsSettingButton, "VisualEffectsSettingButton", "VisualEffectsSettingText", Vector2.zero, CycleVisualEffectsEnabled);
        startingVfxSettingButton = EnsureSettingButton(ref startingVfxSettingText, startingVfxSettingButton, "StartingVfxSettingButton", "StartingVfxSettingText", Vector2.zero, CycleStartingVfxEnabled);
        endDisasterSettingButton = EnsureSettingButton(ref endDisasterSettingText, endDisasterSettingButton, "EndDisasterSettingButton", "EndDisasterSettingText", Vector2.zero, CycleEndDisasterMode);
        endDisasterTimeSettingButton = EnsureSettingButton(ref endDisasterTimeSettingText, endDisasterTimeSettingButton, "EndDisasterTimeSettingButton", "EndDisasterTimeSettingText", Vector2.zero, CycleEndDisasterWarningSeconds);
        obstacleSettingButton = EnsureSettingButton(ref obstacleSettingText, obstacleSettingButton, "ObstacleSettingButton", "ObstacleSettingText", Vector2.zero, CycleObstacleDensity);
        obstacleDestroySettingButton = EnsureSettingButton(ref obstacleDestroySettingText, obstacleDestroySettingButton, "ObstacleDestroySettingButton", "ObstacleDestroySettingText", Vector2.zero, CycleObstacleDestroyEnabled);
        obstacleHpValueSettingButton = EnsureSettingButton(ref obstacleHpValueSettingText, obstacleHpValueSettingButton, "ObstacleHpValueSettingButton", "ObstacleHpValueSettingText", Vector2.zero, CycleObstacleHp);
        obstacleSizeSettingButton = EnsureSettingButton(ref obstacleSizeSettingText, obstacleSizeSettingButton, "ObstacleSizeSettingButton", "ObstacleSizeSettingText", Vector2.zero, CycleObstacleSizePercent);
        obstacleNoBordersSettingButton = EnsureSettingButton(ref obstacleNoBordersSettingText, obstacleNoBordersSettingButton, "ObstacleNoBordersSettingButton", "ObstacleNoBordersSettingText", Vector2.zero, CycleObstacleNoBorders);
        treasureSettingButton = EnsureSettingButton(ref treasureSettingText, treasureSettingButton, "TreasureSettingButton", "TreasureSettingText", Vector2.zero, CycleTreasureDensity);
        resourceRichnessSettingButton = EnsureSettingButton(ref resourceRichnessSettingText, resourceRichnessSettingButton, "ResourceRichnessSettingButton", "ResourceRichnessSettingText", Vector2.zero, CycleResourceRichness);
        spaceJunkSettingButton = EnsureSettingButton(ref spaceJunkSettingText, spaceJunkSettingButton, "SpaceJunkSettingButton", "SpaceJunkSettingText", Vector2.zero, CycleSpaceJunkDensity);
        nebulaSettingButton = EnsureSettingButton(ref nebulaSettingText, nebulaSettingButton, "NebulaSettingButton", "NebulaSettingText", Vector2.zero, CycleNebulaDensity);
        extractionSettingButton = EnsureSettingButton(ref extractionSettingText, extractionSettingButton, "ExtractionSettingButton", "ExtractionSettingText", Vector2.zero, CycleExtractionCount);
        repairBaySettingButton = EnsureSettingButton(ref repairBaySettingText, repairBaySettingButton, "RepairBaySettingButton", "RepairBaySettingText", Vector2.zero, CycleRepairBayCount);
        boosterSettingButton = EnsureSettingButton(ref boosterSettingText, boosterSettingButton, "BoosterSettingButton", "BoosterSettingText", Vector2.zero, CycleBoosterSlowdown);
        ammoSettingButton = EnsureSettingButton(ref ammoSettingText, ammoSettingButton, "AmmoSettingButton", "AmmoSettingText", Vector2.zero, CycleAmmoCount);
        boosterDelaySettingButton = EnsureSettingButton(ref boosterDelaySettingText, boosterDelaySettingButton, "BoosterDelaySettingButton", "BoosterDelaySettingText", Vector2.zero, CycleBoosterRecoveryDelay);
        HideDeprecatedSettingButton("MaxInputBoostSettingButton", "MaxInputBoostSettingText");
        shipDriftSettingButton = EnsureSettingButton(ref shipDriftSettingText, shipDriftSettingButton, "ShipDriftSettingButton", "ShipDriftSettingText", Vector2.zero, CycleShipDriftEnabled);
        deathTimerSettingButton = EnsureSettingButton(ref deathTimerSettingText, deathTimerSettingButton, "DeathTimerSettingButton", "DeathTimerSettingText", Vector2.zero, CycleLastShipTimerMultiplier);
        inventoryLossSettingButton = EnsureSettingButton(ref inventoryLossSettingText, inventoryLossSettingButton, "InventoryLossSettingButton", "InventoryLossSettingText", Vector2.zero, CycleInventoryLossEnabled);
        equipmentLossSettingButton = EnsureSettingButton(ref equipmentLossSettingText, equipmentLossSettingButton, "EquipmentLossSettingButton", "EquipmentLossSettingText", Vector2.zero, CycleEquipmentLossEnabled);
        shootingModelSettingButton = EnsureSettingButton(ref shootingModelSettingText, shootingModelSettingButton, "ShootingModelSettingButton", "ShootingModelSettingText", Vector2.zero, CycleShootingModel);
        superAttackSettingButton = EnsureSettingButton(ref superAttackSettingText, superAttackSettingButton, "SuperAttackSettingButton", "SuperAttackSettingText", Vector2.zero, CycleSuperAttackEnabled);
        advancedShootingJoystickSettingButton = EnsureSettingButton(ref advancedShootingJoystickSettingText, advancedShootingJoystickSettingButton, "AdvancedShootingJoystickSettingButton", "AdvancedShootingJoystickSettingText", Vector2.zero, CycleAdvancedShootingJoystickEnabled);
        hapticsSettingButton = EnsureSettingButton(ref hapticsSettingText, hapticsSettingButton, "HapticsSettingButton", "HapticsSettingText", Vector2.zero, CycleHapticsEnabled);
        fpsCounterSettingButton = EnsureSettingButton(ref fpsCounterSettingText, fpsCounterSettingButton, "FpsCounterSettingButton", "FpsCounterSettingText", Vector2.zero, CycleFpsCounterEnabled);
        gunSetupSettingButton = EnsureSettingButton(ref gunSetupSettingText, gunSetupSettingButton, "GunSetupSettingButton", "GunSetupSettingText", Vector2.zero, OpenGunSetup);
        movingObjectsSettingButton = EnsureSettingButton(ref movingObjectsSettingText, movingObjectsSettingButton, "MovingObjectsSettingButton", "MovingObjectsSettingText", Vector2.zero, CycleMovingObjectsEnabled);
        bulletPushSettingButton = EnsureSettingButton(ref bulletPushSettingText, bulletPushSettingButton, "BulletPushSettingButton", "BulletPushSettingText", Vector2.zero, CycleBulletPushMultiplier);
        obstacleWeightSettingButton = EnsureSettingButton(ref obstacleWeightSettingText, obstacleWeightSettingButton, "ObstacleWeightSettingButton", "ObstacleWeightSettingText", Vector2.zero, CycleObstacleWeightFactor);
        treasureWeightSettingButton = EnsureSettingButton(ref treasureWeightSettingText, treasureWeightSettingButton, "TreasureWeightSettingButton", "TreasureWeightSettingText", Vector2.zero, CycleTreasureWeightFactor);
        batteringSettingButton = EnsureSettingButton(ref batteringSettingText, batteringSettingButton, "BatteringSettingButton", "BatteringSettingText", Vector2.zero, CycleBatteringDamage);

        AttachLeftSectionButton(roundSettingButton, "ROUND RULES");
        AttachLeftSectionButton(mapSizeSettingButton, "ROUND RULES");
        AttachLeftSectionButton(deathTimerSettingButton, "ROUND RULES");
        AttachLeftSectionButton(inventoryLossSettingButton, "ROUND RULES");
        AttachLeftSectionButton(equipmentLossSettingButton, "ROUND RULES");

        AttachLeftSectionButton(obstacleSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(obstacleDestroySettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(obstacleHpValueSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(obstacleSizeSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(obstacleNoBordersSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(treasureSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(resourceRichnessSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(spaceJunkSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(nebulaSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(extractionSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(repairBaySettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(movingObjectsSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(obstacleWeightSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(treasureWeightSettingButton, "ENVIRONMENT");

        AttachLeftSectionButton(mapBackgroundSettingButton, "COSMETICS");
        AttachLeftSectionButton(visualEffectsSettingButton, "COSMETICS");
        AttachLeftSectionButton(startingVfxSettingButton, "COSMETICS");

        AttachLeftSectionButton(boosterSettingButton, "FEELING");
        AttachLeftSectionButton(ammoSettingButton, "FEELING");
        AttachLeftSectionButton(boosterDelaySettingButton, "FEELING");
        AttachLeftSectionButton(shipDriftSettingButton, "FEELING");
        AttachLeftSectionButton(bulletPushSettingButton, "FEELING");
        AttachLeftSectionButton(batteringSettingButton, "FEELING");
        AttachLeftSectionButton(endDisasterSettingButton, "FEELING");
        AttachLeftSectionButton(endDisasterTimeSettingButton, "FEELING");
        AttachLeftSectionButton(shootingModelSettingButton, "FEELING");
        AttachLeftSectionButton(superAttackSettingButton, "FEELING");
        AttachLeftSectionButton(advancedShootingJoystickSettingButton, "FEELING");
        AttachLeftSectionButton(hapticsSettingButton, "FEELING");

        AttachLeftSectionButton(fpsCounterSettingButton, "DIAGNOSTICS");

        LayoutLeftSectionButtons();
        EnsureEnemySettingsUiExists();
    }

    void EnsureLobbyNavigationUiExists()
    {
        backToRoundsButton = EnsureSettingButton(
            ref backToRoundsText,
            backToRoundsButton,
            "BackToRoundsButton",
            "BackToRoundsText",
            new Vector2(BottomActionBackX, BottomActionButtonsY),
            OnBackToRoundsClicked);

        if (backToRoundsButton != null)
        {
            RectTransform rect = backToRoundsButton.GetComponent<RectTransform>();
            if (rect != null)
                rect.sizeDelta = new Vector2(BottomActionButtonWidth, BottomActionButtonHeight);
        }

        EnsureBottomActionButtonsLayout();
    }

    void EnsureLobbyMapUiExists()
    {
        if (mapSelectionButton == null || !mapSelectionButton.gameObject.scene.IsValid())
        {
            GameObject buttonObject = FindOrCreateChild(gameObject, "LobbyMapSelectionButton", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(MapSelectionButtonX, MapSelectionButtonY);
            rect.sizeDelta = new Vector2(MapSelectionButtonWidth, MapSelectionButtonHeight);

            Image image = buttonObject.GetComponent<Image>();
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;

            mapSelectionButton = buttonObject.GetComponent<Button>();
            mapSelectionButton.transition = Selectable.Transition.ColorTint;
            mapSelectionButton.onClick.RemoveAllListeners();
            mapSelectionButton.onClick.AddListener(() => PlayUiClickAndInvoke(OnMapSelectionClicked));

            GameObject labelBackdropObject = FindOrCreateChild(buttonObject, "MapLabelBackdrop", typeof(RectTransform), typeof(Image));
            RectTransform labelBackdropRect = labelBackdropObject.GetComponent<RectTransform>();
            labelBackdropRect.anchorMin = new Vector2(0f, 1f);
            labelBackdropRect.anchorMax = new Vector2(1f, 1f);
            labelBackdropRect.pivot = new Vector2(0.5f, 1f);
            labelBackdropRect.anchoredPosition = Vector2.zero;
            labelBackdropRect.sizeDelta = new Vector2(0f, 54f);
            Image labelBackdropImage = labelBackdropObject.GetComponent<Image>();
            labelBackdropImage.color = new Color(0.02f, 0.04f, 0.07f, 0.78f);

            GameObject textObject = FindOrCreateChild(buttonObject, "LobbyMapSelectionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 12f);
            textRect.offsetMax = new Vector2(-18f, -12f);

            mapSelectionText = textObject.GetComponent<TextMeshProUGUI>();
            mapSelectionText.fontSize = 24f;
            mapSelectionText.fontStyle = FontStyles.Bold;
            mapSelectionText.alignment = TextAlignmentOptions.TopLeft;
            mapSelectionText.color = Color.white;
            mapSelectionText.textWrappingMode = TextWrappingModes.Normal;

            TMP_Text reference = FindAnyObjectByType<TMP_Text>();
            if (reference != null)
            {
                mapSelectionText.font = reference.font;
                mapSelectionText.fontSharedMaterial = reference.fontSharedMaterial;
            }
        }

        EnsureMapSelectionOverlayUiExists();
    }

    void EnsureMapSelectionOverlayUiExists()
    {
        if (mapSelectionOverlayObject != null && mapSelectionOverlayObject.scene.IsValid())
            return;

        mapSelectionOverlayObject = FindOrCreateChild(gameObject, "LobbyMapSelectionOverlay", typeof(RectTransform), typeof(Image));
        RectTransform overlayRect = mapSelectionOverlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = mapSelectionOverlayObject.GetComponent<Image>();
        overlayImage.color = new Color(0.01f, 0.02f, 0.04f, 0.58f);
        overlayImage.raycastTarget = true;

        GameObject panelObject = FindOrCreateChild(mapSelectionOverlayObject, "LobbyMapSelectionPanel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, -10f);
        panelRect.sizeDelta = new Vector2(1540f, 880f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.94f);

        mapSelectionOverlayTitleText = CreateStandaloneLabel(panelObject.transform, "LobbyMapSelectionTitle", "SELECT MAP", new Vector2(34f, -26f), new Vector2(400f, 34f), 28f, TextAlignmentOptions.Left);

        TMP_Text subtitle = CreateStandaloneLabel(panelObject.transform, "LobbyMapSelectionSubtitle", "Choose a preset map for the round.", new Vector2(36f, -68f), new Vector2(540f, 24f), 16f, TextAlignmentOptions.Left);
        subtitle.fontStyle = FontStyles.Normal;
        subtitle.color = new Color(0.78f, 0.84f, 0.91f, 0.92f);

        GameObject viewportObject = FindOrCreateChild(panelObject, "LobbyMapSelectionViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 1f);
        viewportRect.anchorMax = new Vector2(0.5f, 1f);
        viewportRect.pivot = new Vector2(0.5f, 1f);
        viewportRect.anchoredPosition = new Vector2(-18f, -116f);
        viewportRect.sizeDelta = new Vector2(1340f, 700f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.04f, 0.06f, 0.09f, 0.42f);
        viewportImage.raycastTarget = true;

        GameObject contentObject = FindOrCreateChild(viewportObject, "LobbyMapSelectionContent", typeof(RectTransform));
        mapSelectionContentRect = contentObject.GetComponent<RectTransform>();
        mapSelectionContentRect.anchorMin = new Vector2(0f, 1f);
        mapSelectionContentRect.anchorMax = new Vector2(0f, 1f);
        mapSelectionContentRect.pivot = new Vector2(0f, 1f);
        mapSelectionContentRect.anchoredPosition = Vector2.zero;

        GameObject scrollbarObject = FindOrCreateChild(panelObject, "LobbyMapSelectionScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(0.5f, 1f);
        scrollbarRect.anchorMax = new Vector2(0.5f, 1f);
        scrollbarRect.pivot = new Vector2(0f, 1f);
        scrollbarRect.anchoredPosition = new Vector2(680f, -116f);
        scrollbarRect.sizeDelta = new Vector2(42f, 700f);

        Image scrollbarBg = scrollbarObject.GetComponent<Image>();
        scrollbarBg.color = new Color(0.1f, 0.14f, 0.18f, 0.88f);

        GameObject slidingAreaObject = FindOrCreateChild(scrollbarObject, "Sliding Area", typeof(RectTransform));
        RectTransform slidingAreaRect = slidingAreaObject.GetComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(4f, 4f);
        slidingAreaRect.offsetMax = new Vector2(-4f, -4f);

        GameObject handleObject = FindOrCreateChild(slidingAreaObject, "Handle", typeof(RectTransform), typeof(Image));
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 1f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.pivot = new Vector2(0.5f, 1f);
        handleRect.sizeDelta = new Vector2(0f, 140f);

        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(0.23f, 0.74f, 0.62f, 0.95f);

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;

        mapSelectionScrollRect = viewportObject.GetComponent<ScrollRect>();
        mapSelectionScrollRect.horizontal = false;
        mapSelectionScrollRect.vertical = true;
        mapSelectionScrollRect.movementType = ScrollRect.MovementType.Clamped;
        mapSelectionScrollRect.viewport = viewportRect;
        mapSelectionScrollRect.content = mapSelectionContentRect;
        mapSelectionScrollRect.verticalScrollbar = scrollbar;
        mapSelectionScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        mapSelectionScrollRect.scrollSensitivity = 36f;

        GameObject closeButtonObject = FindOrCreateChild(panelObject, "LobbyMapSelectionCloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        mapSelectionOverlayCloseButton = closeButtonObject.GetComponent<Button>();
        if (mapSelectionOverlayCloseButton != null)
        {
            mapSelectionOverlayCloseButton.onClick.RemoveAllListeners();
            mapSelectionOverlayCloseButton.onClick.AddListener(() => PlayUiClickAndInvoke(HideMapSelectionOverlay));

            RectTransform closeRect = closeButtonObject.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-28f, -22f);
            closeRect.sizeDelta = new Vector2(190f, 52f);

            Image closeImage = closeButtonObject.GetComponent<Image>();
            closeImage.color = new Color(0.16f, 0.34f, 0.58f, 0.98f);

            Transform existingCloseText = closeButtonObject.transform.Find("LobbyMapSelectionCloseText");
            TMP_Text closeText = existingCloseText != null
                ? existingCloseText.GetComponent<TMP_Text>()
                : null;
            if (closeText == null)
            {
                GameObject closeTextObject = new GameObject("LobbyMapSelectionCloseText", typeof(RectTransform), typeof(TextMeshProUGUI));
                closeTextObject.transform.SetParent(closeButtonObject.transform, false);
                RectTransform closeTextRect = closeTextObject.GetComponent<RectTransform>();
                closeTextRect.anchorMin = Vector2.zero;
                closeTextRect.anchorMax = Vector2.one;
                closeTextRect.offsetMin = Vector2.zero;
                closeTextRect.offsetMax = Vector2.zero;
                closeText = closeTextObject.GetComponent<TMP_Text>();

                TMP_Text reference = FindAnyObjectByType<TMP_Text>();
                if (reference != null)
                {
                    closeText.font = reference.font;
                    closeText.fontSharedMaterial = reference.fontSharedMaterial;
                }
            }

            closeText.text = "CLOSE";
            closeText.fontSize = 22f;
            closeText.fontStyle = FontStyles.Bold;
            closeText.characterSpacing = 1.2f;
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.color = Color.white;
        }

        mapSelectionTileButtons.Clear();
        IReadOnlyList<LobbyMapDefinition> maps = LobbyMapCatalog.AllMaps;
        int rows = Mathf.CeilToInt(maps.Count / 2f);
        mapSelectionContentRect.sizeDelta = new Vector2(1280f, Mathf.Max(700f, rows * 280f + 20f));
        for (int i = 0; i < maps.Count; i++)
        {
            LobbyMapDefinition map = maps[i];
            GameObject tileObject = new GameObject("LobbyMapTile_" + map.Id, typeof(RectTransform), typeof(Image), typeof(Button));
            tileObject.transform.SetParent(mapSelectionContentRect, false);

            RectTransform tileRect = tileObject.GetComponent<RectTransform>();
            tileRect.anchorMin = new Vector2(0f, 1f);
            tileRect.anchorMax = new Vector2(0f, 1f);
            tileRect.pivot = new Vector2(0.5f, 1f);
            int column = i % 2;
            int row = i / 2;
            tileRect.anchoredPosition = new Vector2(290f + (column * 620f), -20f - (row * 280f));
            tileRect.sizeDelta = new Vector2(540f, 250f);

            Image tileImage = tileObject.GetComponent<Image>();
            tileImage.color = Color.white;

            Button tileButton = tileObject.GetComponent<Button>();
            string mapId = map.Id;
            tileButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                OnMapTileSelected(mapId);
            });
            mapSelectionTileButtons.Add(tileButton);

            GameObject tileLabelBackdrop = FindOrCreateChild(tileObject, "TileLabelBackdrop", typeof(RectTransform), typeof(Image));
            RectTransform tileLabelBackdropRect = tileLabelBackdrop.GetComponent<RectTransform>();
            tileLabelBackdropRect.anchorMin = new Vector2(0f, 1f);
            tileLabelBackdropRect.anchorMax = new Vector2(1f, 1f);
            tileLabelBackdropRect.pivot = new Vector2(0.5f, 1f);
            tileLabelBackdropRect.anchoredPosition = Vector2.zero;
            tileLabelBackdropRect.sizeDelta = new Vector2(0f, 54f);
            tileLabelBackdrop.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.07f, 0.78f);

            TMP_Text tileTitle = CreateStandaloneLabel(tileObject.transform, "TileTitle", map.DisplayName, new Vector2(18f, -14f), new Vector2(320f, 30f), 24f, TextAlignmentOptions.Left);
            TMP_Text tileSubtitle = CreateStandaloneLabel(tileObject.transform, "TileSubtitle", "PRESET MAP", new Vector2(18f, -46f), new Vector2(220f, 22f), 15f, TextAlignmentOptions.Left);
            tileSubtitle.fontStyle = FontStyles.Normal;
            tileSubtitle.color = new Color(0.82f, 0.88f, 0.94f, 0.9f);
        }

        if (mapSelectionScrollRect != null)
            mapSelectionScrollRect.verticalNormalizedPosition = 1f;

        mapSelectionOverlayObject.SetActive(false);
    }

    void EnsureBottomActionButtonsLayout()
    {
        PositionBottomActionButton(readyButton, BottomActionReadyX, BottomActionButtonsY, new Vector2(BottomActionButtonWidth, BottomActionButtonHeight));
        PositionBottomActionButton(backToRoundsButton, BottomActionBackX, BottomActionButtonsY, new Vector2(BottomActionButtonWidth, BottomActionButtonHeight));

        if (readyText != null)
        {
            readyText.fontSize = 34f;
            readyText.characterSpacing = 2.2f;
        }

        if (backToRoundsText != null)
        {
            backToRoundsText.fontSize = 30f;
            backToRoundsText.characterSpacing = 2.2f;
        }
    }

    void RefreshLobbyMapSelectionUi(bool isHost)
    {
        EnsureLobbyMapUiExists();

        LobbyMapDefinition selectedMap = LobbyMapCatalog.Get(RoomSettings.GetSelectedLobbyMapId());
        Sprite previewSprite = LoadLobbyBackgroundSprite(selectedMap.MapBackgroundIndex);

        if (mapSelectionButton != null)
        {
            mapSelectionButton.interactable = PhotonNetwork.InRoom && RoomSettings.GetSessionState() == RoomSettings.SessionStateInLobby;
            mapSelectionButton.transform.SetAsLastSibling();

            Image image = mapSelectionButton.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = previewSprite;
                image.color = Color.white;
            }

            ColorBlock colors = mapSelectionButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.96f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.84f, 0.88f, 0.94f, 1f);
            colors.disabledColor = new Color(0.35f, 0.38f, 0.42f, 0.85f);
            colors.colorMultiplier = 1f;
            mapSelectionButton.colors = colors;
        }

        if (mapSelectionText != null)
        {
            mapSelectionText.text = "MAP\n" + selectedMap.DisplayName;
            mapSelectionText.fontSize = 24f;
            mapSelectionText.characterSpacing = 1.2f;
        }

        for (int i = 0; i < mapSelectionTileButtons.Count && i < LobbyMapCatalog.AllMaps.Count; i++)
        {
            LobbyMapDefinition map = LobbyMapCatalog.AllMaps[i];
            Button tileButton = mapSelectionTileButtons[i];
            if (tileButton == null)
                continue;

            Image tileImage = tileButton.GetComponent<Image>();
            if (tileImage != null)
            {
                tileImage.sprite = LoadLobbyBackgroundSprite(map.MapBackgroundIndex);
                tileImage.color = Color.white;
            }

            bool isSelected = selectedMap.Id == map.Id;
            tileButton.interactable = isHost;
            ColorBlock tileColors = tileButton.colors;
            tileColors.normalColor = isSelected ? new Color(0.84f, 1f, 0.9f, 1f) : Color.white;
            tileColors.highlightedColor = new Color(0.92f, 0.96f, 1f, 1f);
            tileColors.selectedColor = tileColors.highlightedColor;
            tileColors.pressedColor = new Color(0.84f, 0.88f, 0.94f, 1f);
            tileColors.disabledColor = new Color(0.58f, 0.6f, 0.64f, 0.82f);
            tileColors.colorMultiplier = 1f;
            tileButton.colors = tileColors;
        }

        if (mapSelectionOverlayCloseButton != null)
            mapSelectionOverlayCloseButton.interactable = true;
    }

    void OnMapSelectionClicked()
    {
        if (!PhotonNetwork.InRoom || RoomSettings.GetSessionState() != RoomSettings.SessionStateInLobby)
            return;

        selectedMapId = RoomSettings.GetSelectedLobbyMapId();
        SwitchLobbyScreen(LobbyScreen.MapSelection);
    }

    void HideMapSelectionOverlay()
    {
        if (mapSelectionOverlayObject != null)
            mapSelectionOverlayObject.SetActive(false);
    }

    void OnMapTileSelected(string mapId)
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return;

        LobbyMapDefinition selectedMap = LobbyMapCatalog.Get(mapId);
        if (selectedMap == null)
            return;
        selectedMapId = selectedMap != null ? selectedMap.Id : selectedMapId;
        Hashtable props = new Hashtable();
        LobbyMapCatalog.ApplyToProperties(selectedMap, props);
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        HideMapSelectionOverlay();
        RefreshHostSettingsUi();
        SwitchLobbyScreen(LobbyScreen.MapDetails);
    }

    Sprite LoadLobbyBackgroundSprite(int backgroundIndex)
    {
        int clampedIndex = Mathf.Clamp(backgroundIndex, 1, 15);
        if (mapBackgroundPreviewCache.TryGetValue(clampedIndex, out Sprite cachedSprite) && cachedSprite != null)
            return cachedSprite;

        string resourcePath = "Visuals/Backgrounds/background" + clampedIndex + "_resource";
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

#if UNITY_EDITOR
        if (sprite == null)
        {
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Visuals/Backgrounds/background" + clampedIndex + "_resource.png");
            if (sprite == null)
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/tło" + clampedIndex + ".png");
        }
#endif

        if (sprite == null)
        {
            sprite = Resources.Load<Sprite>("Visuals/Backgrounds/background1_resource");
            if (sprite == null)
            {
                Texture2D fallbackTexture = Resources.Load<Texture2D>("Visuals/Backgrounds/background1_resource");
                if (fallbackTexture != null)
                    sprite = Sprite.Create(fallbackTexture, new Rect(0f, 0f, fallbackTexture.width, fallbackTexture.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        if (sprite == null)
            return null;

        mapBackgroundPreviewCache[clampedIndex] = sprite;
        return sprite;
    }

    void PositionBottomActionButton(Button button, float anchoredX, float anchoredY, Vector2 size)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(anchoredX, anchoredY);
        rect.sizeDelta = size;
    }

    void EnsureEnemySettingsUiExists()
    {
        EnsureEnemyTableUiExists();

        for (int i = 0; i < EnemyBotCatalog.AllDefinitions.Count; i++)
        {
            EnemyBotDefinition definition = EnemyBotCatalog.AllDefinitions[i];
            float rowY = -112f - (i * EnemyRowHeight);
            EnsureEnemyRowLabel(definition, new Vector2(26f, rowY));

            EnsureEnemySettingButton(definition, "enabled", GetEnemyCellPosition(0, rowY), () => CycleEnemyEnabled(definition.Kind));
            EnsureEnemySettingButton(definition, "count", GetEnemyCellPosition(1, rowY), () => CycleEnemyCount(definition.Kind));
            EnsureEnemySettingButton(definition, "respawn", GetEnemyCellPosition(2, rowY), () => CycleEnemyRespawnEnabled(definition.Kind));
            EnsureEnemySettingButton(definition, "hp", GetEnemyCellPosition(3, rowY), () => CycleEnemyHp(definition.Kind));
            EnsureEnemySettingButton(definition, "shield", GetEnemyCellPosition(4, rowY), () => CycleEnemyShield(definition.Kind));
            EnsureEnemySettingButton(definition, "damage", GetEnemyCellPosition(5, rowY), () => CycleEnemyDamage(definition.Kind));
            EnsureEnemySettingButton(definition, "speed", GetEnemyCellPosition(6, rowY), () => CycleEnemySpeed(definition.Kind));
            EnsureEnemySettingButton(definition, "time", GetEnemyCellPosition(7, rowY), () => CycleEnemySpawnSecond(definition.Kind));
            EnsureEnemySettingButton(definition, "respawnTime", GetEnemyCellPosition(8, rowY), () => CycleEnemyRespawnInterval(definition.Kind));
        }
    }

    void EnsureEnemySettingButton(EnemyBotDefinition definition, string suffix, Vector2 anchoredPosition, UnityEngine.Events.UnityAction callback)
    {
        string key = GetEnemySettingUiKey(definition.Kind, suffix);
        string buttonName = "EnemySettingButton_" + key;
        string textName = "EnemySettingText_" + key;

        enemySettingButtons.TryGetValue(key, out Button existingButton);
        enemySettingTexts.TryGetValue(key, out TMP_Text existingText);

        Button button = EnsureSettingButton(ref existingText, existingButton, buttonName, textName, anchoredPosition, callback);
        if (button != null && enemyTableRootRect != null)
        {
            button.transform.SetParent(enemyTableRootRect, false);
            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = new Vector2(EnemyColumnWidth - 8f, 42f);
            }
        }

        enemySettingButtons[key] = button;
        enemySettingTexts[key] = existingText;
    }

    string GetEnemySettingUiKey(EnemyBotKind kind, string suffix)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        string prefix = definition != null ? definition.Id : kind.ToString().ToLowerInvariant();
        return prefix + "_" + suffix;
    }

    Vector2 GetEnemyCellPosition(int columnIndex, float rowY)
    {
        return new Vector2(EnemyNameColumnWidth + 18f + (columnIndex * EnemyColumnWidth), rowY);
    }

    void EnsureSettingsLayoutContainers()
    {
        Transform desiredParent = developerSettingsRootObject != null ? developerSettingsRootObject.transform : transform;

        if (leftSettingsViewportRect != null && leftSettingsViewportRect.gameObject.scene.IsValid())
        {
            if (leftSettingsViewportRect.transform.parent != desiredParent)
                leftSettingsViewportRect.transform.SetParent(desiredParent, false);
            ApplyLeftSettingsViewportLayout();
            return;
        }

        GameObject viewportObject = FindOrCreateChild(developerSettingsRootObject != null ? developerSettingsRootObject : gameObject, "LobbySettingsViewport", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        leftSettingsViewportRect = viewportObject.GetComponent<RectTransform>();
        leftSettingsViewportRect.anchorMin = new Vector2(0.5f, 1f);
        leftSettingsViewportRect.anchorMax = new Vector2(0.5f, 1f);
        leftSettingsViewportRect.pivot = new Vector2(0.5f, 1f);
        ApplyLeftSettingsViewportLayout();

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.06f, 0.09f, 0.13f, 0.72f);
        viewportImage.raycastTarget = true;

        Mask mask = viewportObject.GetComponent<Mask>();
        mask.showMaskGraphic = true;

        leftSettingsScrollRect = viewportObject.GetComponent<ScrollRect>();
        leftSettingsScrollRect.horizontal = false;
        leftSettingsScrollRect.vertical = true;
        leftSettingsScrollRect.movementType = ScrollRect.MovementType.Clamped;
        leftSettingsScrollRect.scrollSensitivity = 36f;

        GameObject contentObject = FindOrCreateChild(viewportObject, "LobbySettingsContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        leftSettingsContentRect = contentObject.GetComponent<RectTransform>();
        leftSettingsContentRect.anchorMin = new Vector2(0f, 1f);
        leftSettingsContentRect.anchorMax = new Vector2(1f, 1f);
        leftSettingsContentRect.pivot = new Vector2(0.5f, 1f);
        leftSettingsContentRect.anchoredPosition = Vector2.zero;
        leftSettingsContentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(18, 18, 18, 170);
        contentLayout.spacing = 22f;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        leftSettingsScrollRect.viewport = leftSettingsViewportRect;
        leftSettingsScrollRect.content = leftSettingsContentRect;

        EnsureLeftSectionContainer("ROUND RULES");
        EnsureLeftSectionContainer("ENVIRONMENT");
        EnsureLeftSectionContainer("COSMETICS");
        EnsureLeftSectionContainer("FEELING");
        EnsureLeftSectionContainer("DIAGNOSTICS");
    }

    void ApplyLeftSettingsViewportLayout()
    {
        if (leftSettingsViewportRect == null)
            return;

        leftSettingsViewportRect.anchoredPosition = new Vector2(LeftColumnX, LeftColumnTopY);
        leftSettingsViewportRect.sizeDelta = new Vector2(LeftViewportWidth, LeftViewportHeight);
    }

    void EnsureEnemyTableUiExists()
    {
        if (enemyTableRootRect != null && enemyTableRootRect.gameObject.scene.IsValid())
        {
            if (developerSettingsRootObject != null && enemyTableRootRect.transform.parent != developerSettingsRootObject.transform)
                enemyTableRootRect.transform.SetParent(developerSettingsRootObject.transform, false);
        }
        else
        {
            GameObject tableObject = FindOrCreateChild(developerSettingsRootObject != null ? developerSettingsRootObject : gameObject, "EnemySettingsTable", typeof(RectTransform), typeof(Image));
            enemyTableRootRect = tableObject.GetComponent<RectTransform>();
        }

        enemyTableRootRect.anchorMin = new Vector2(0.5f, 1f);
        enemyTableRootRect.anchorMax = new Vector2(0.5f, 1f);
        enemyTableRootRect.pivot = new Vector2(0.5f, 1f);
        enemyTableRootRect.anchoredPosition = new Vector2(RightTableX, RightTableY);
        enemyTableRootRect.sizeDelta = new Vector2(RightTableWidth, RightTableHeight);

        Image bg = enemyTableRootRect.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = false;

        EnsureTableHeaderLabel("EnemyTableTitle", "ENEMIES", new Vector2(24f, -18f), new Vector2(220f, 30f), 24f, TextAlignmentOptions.Left);
        EnsureTableHeaderLabel("EnemyHeader_ACTIVE", "ACTIVE", GetEnemyHeaderPosition(0), new Vector2(EnemyColumnWidth - 8f, 26f), 14f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_COUNT", "COUNT", GetEnemyHeaderPosition(1), new Vector2(EnemyColumnWidth - 8f, 26f), 14f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_RESPAWN", "RESPAWN", GetEnemyHeaderPosition(2), new Vector2(EnemyColumnWidth - 8f, 26f), 14f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_HP", "HP", GetEnemyHeaderPosition(3), new Vector2(EnemyColumnWidth - 8f, 26f), 14f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_SHIELD", "SHIELD", GetEnemyHeaderPosition(4), new Vector2(EnemyColumnWidth - 8f, 26f), 14f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_DAMAGE", "DAMAGE", GetEnemyHeaderPosition(5), new Vector2(EnemyColumnWidth - 8f, 26f), 13f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_SPEED", "SPEED", GetEnemyHeaderPosition(6), new Vector2(EnemyColumnWidth - 8f, 26f), 14f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_FIRSTRESPAWN", "FIRST\nRESPAWN", GetEnemyHeaderPosition(7), new Vector2(EnemyColumnWidth - 8f, 38f), 12f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_RESPAWNLOOP", "RESPAWN\nLOOP", GetEnemyHeaderPosition(8), new Vector2(EnemyColumnWidth - 8f, 38f), 12f, TextAlignmentOptions.Center);
    }

    Vector2 GetEnemyHeaderPosition(int columnIndex)
    {
        return new Vector2(EnemyNameColumnWidth + 18f + (columnIndex * EnemyColumnWidth), -54f);
    }

    void EnsureEnemyRowLabel(EnemyBotDefinition definition, Vector2 anchoredPosition)
    {
        if (definition == null || enemyTableRootRect == null)
            return;

        if (!enemyRowLabels.TryGetValue(definition.Id, out TMP_Text label) || label == null || !label.gameObject.scene.IsValid())
        {
            label = CreateStandaloneLabel(enemyTableRootRect.transform, "EnemyRowLabel_" + definition.Id, definition.DisplayName.ToUpperInvariant(), anchoredPosition, new Vector2(EnemyNameColumnWidth - 12f, 28f), 16f, TextAlignmentOptions.Left);
            enemyRowLabels[definition.Id] = label;
        }

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
    }

    TMP_Text EnsureTableHeaderLabel(string key, string value, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        if (!enemyHeaderLabels.TryGetValue(key, out TMP_Text label) || label == null || !label.gameObject.scene.IsValid())
        {
            label = CreateStandaloneLabel(enemyTableRootRect.transform, key, value, anchoredPosition, size, fontSize, alignment);
            enemyHeaderLabels[key] = label;
        }

        label.text = value;
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return label;
    }

    RectTransform EnsureLeftSectionContainer(string sectionName)
    {
        if (leftSectionContainers.TryGetValue(sectionName, out RectTransform existing) && existing != null && existing.gameObject.scene.IsValid())
            return existing;

        string safeName = sectionName.Replace(" ", string.Empty);
        GameObject sectionObject = new GameObject("LobbySection_" + safeName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        sectionObject.transform.SetParent(leftSettingsContentRect, false);

        RectTransform rect = sectionObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = sectionObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = sectionObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        LayoutElement layoutElement = sectionObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = -1f;

        GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        headerObject.transform.SetParent(sectionObject.transform, false);
        RectTransform headerRect = headerObject.GetComponent<RectTransform>();
        headerRect.sizeDelta = new Vector2(0f, 34f);
        LayoutElement headerLayout = headerObject.GetComponent<LayoutElement>();
        headerLayout.preferredHeight = 34f;

        TMP_Text headerText = headerObject.GetComponent<TextMeshProUGUI>();
        headerText.text = sectionName;
        headerText.fontSize = 22f;
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.Left;
        headerText.color = new Color(0.86f, 0.95f, 1f, 0.96f);
        headerText.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference != null)
        {
            headerText.font = reference.font;
            headerText.fontSharedMaterial = reference.fontSharedMaterial;
        }

        leftSectionContainers[sectionName] = rect;
        return rect;
    }

    void AttachLeftSectionButton(Button button, string sectionName)
    {
        if (button == null)
            return;

        RectTransform sectionRect = EnsureLeftSectionContainer(sectionName);
        if (button.transform.parent != sectionRect)
            button.transform.SetParent(sectionRect, false);

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(0f, 64f);
        }

        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null)
            layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 64f;
        layout.flexibleWidth = 1f;
    }

    void LayoutLeftSectionButtons()
    {
        Canvas.ForceUpdateCanvases();
        if (leftSettingsScrollRect != null && !leftSettingsScrollInitialized)
        {
            leftSettingsScrollRect.verticalNormalizedPosition = 1f;
            leftSettingsScrollInitialized = true;
        }
    }

    TMP_Text CreateStandaloneLabel(Transform parent, string name, string value, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = new Color(0.94f, 0.97f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference != null)
        {
            text.font = reference.font;
            text.fontSharedMaterial = reference.fontSharedMaterial;
        }

        return text;
    }

    GameObject FindOrCreateChild(GameObject parent, string childName, params System.Type[] components)
    {
        Transform existing = parent.transform.Find(childName);
        if (existing != null)
            return existing.gameObject;

        GameObject child = new GameObject(childName, components);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    void RefreshPlayerStatusList()
    {
        EnsurePlayerStatusListExists();

        if (playerStatusListText == null)
            return;

        if (!PhotonNetwork.InRoom)
        {
            playerStatusListText.text = "Joining room...";
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("PLAYERS");

        foreach (Player player in PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber))
        {
            bool ready = player.CustomProperties.TryGetValue("ready", out object readyValue) && readyValue is bool readyBool && readyBool;

            builder.Append(GetDisplayName(player));
            if (player == PhotonNetwork.LocalPlayer)
                builder.Append(" (YOU)");

            builder.Append("  -  ");
            builder.Append(ready ? "READY" : "NOT READY");
            builder.AppendLine();
        }

        playerStatusListText.text = builder.ToString().TrimEnd();
    }

    public void ForceRefreshUi()
    {
        RefreshPlayerStatusList();
        RefreshHostSettingsUi();
    }

    async Task RecordStartedGameAsync()
    {
        await PlayerProfileService.Instance.RecordGameStartedAsync();
    }

    void EnsureDefaultRoomSettings()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        bool changed = false;
        string hostName = !string.IsNullOrWhiteSpace(PhotonNetwork.NickName) ? PhotonNetwork.NickName : "Pilot";
        bool roundStarted = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object startedValue) &&
                            startedValue is bool started &&
                            started;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SessionStateKey))
        {
            props[RoomSettings.SessionStateKey] = roundStarted ? RoomSettings.SessionStateInPlay : RoomSettings.SessionStateInLobby;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SessionLabelKey))
        {
            props[RoomSettings.SessionLabelKey] = hostName + "'s Round";
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SessionHostNameKey))
        {
            props[RoomSettings.SessionHostNameKey] = hostName;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SessionCreatedAtKey))
        {
            props[RoomSettings.SessionCreatedAtKey] = PhotonNetwork.Time;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.StartTimeKey))
        {
            props[RoomSettings.StartTimeKey] = -1d;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SelectedMapKey) &&
            PhotonNetwork.CurrentRoom.PlayerCount <= 1)
        {
            LobbyMapCatalog.ApplyToProperties(LobbyMapCatalog.GetDefault(), props);
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.RoundDurationKey))
        {
            props[RoomSettings.RoundDurationKey] = RoomSettings.DefaultRoundDuration;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ObstacleDensityKey))
        {
            props[RoomSettings.ObstacleDensityKey] = RoomSettings.DefaultObstacleDensity;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ObstacleDestroyEnabledKey))
        {
            props[RoomSettings.ObstacleDestroyEnabledKey] = RoomSettings.DefaultObstacleDestroyEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ObstacleHpKey))
        {
            props[RoomSettings.ObstacleHpKey] = RoomSettings.DefaultObstacleHp;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ObstacleSizePercentKey))
        {
            props[RoomSettings.ObstacleSizePercentKey] = RoomSettings.DefaultObstacleSizePercent;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ObstacleNoBordersKey))
        {
            props[RoomSettings.ObstacleNoBordersKey] = RoomSettings.DefaultObstacleNoBorders;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.MapSizeKey))
        {
            props[RoomSettings.MapSizeKey] = RoomSettings.DefaultMapSize;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.MapBackgroundKey))
        {
            props[RoomSettings.MapBackgroundKey] = RoomSettings.DefaultMapBackground;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.VisualEffectsEnabledKey))
        {
            props[RoomSettings.VisualEffectsEnabledKey] = RoomSettings.DefaultVisualEffectsEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.StartingVfxEnabledKey))
        {
            props[RoomSettings.StartingVfxEnabledKey] = RoomSettings.DefaultStartingVfxEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.EndDisasterModeKey))
        {
            props[RoomSettings.EndDisasterModeKey] = RoomSettings.DefaultEndDisasterMode;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.EndDisasterWarningSecondsKey))
        {
            props[RoomSettings.EndDisasterWarningSecondsKey] = RoomSettings.DefaultEndDisasterWarningSeconds;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.TreasureDensityKey))
        {
            props[RoomSettings.TreasureDensityKey] = "medium";
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ResourceRichnessKey))
        {
            props[RoomSettings.ResourceRichnessKey] = RoomSettings.DefaultResourceRichness;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SpaceJunkDensityKey))
        {
            props[RoomSettings.SpaceJunkDensityKey] = RoomSettings.DefaultSpaceJunkDensity;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.NebulaDensityKey))
        {
            props[RoomSettings.NebulaDensityKey] = "medium";
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ExtractionCountKey))
        {
            props[RoomSettings.ExtractionCountKey] = RoomSettings.DefaultExtractionCount;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.RepairBayCountKey))
        {
            props[RoomSettings.RepairBayCountKey] = RoomSettings.DefaultRepairBayCount;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.BoosterSlowdownKey))
        {
            props[RoomSettings.BoosterSlowdownKey] = RoomSettings.DefaultBoosterSlowdownPercent;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.AmmoCountKey))
        {
            props[RoomSettings.AmmoCountKey] = RoomSettings.DefaultAmmoCount;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.BoosterRecoveryDelayKey))
        {
            props[RoomSettings.BoosterRecoveryDelayKey] = RoomSettings.DefaultBoosterRecoveryDelay;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ShipDriftEnabledKey))
        {
            props[RoomSettings.ShipDriftEnabledKey] = RoomSettings.DefaultShipDriftLevel;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.LastShipTimerMultiplierKey))
        {
            props[RoomSettings.LastShipTimerMultiplierKey] = RoomSettings.DefaultLastShipTimerMultiplier;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.InventoryLossEnabledKey))
        {
            props[RoomSettings.InventoryLossEnabledKey] = RoomSettings.DefaultInventoryLossEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.EquipmentLossEnabledKey))
        {
            props[RoomSettings.EquipmentLossEnabledKey] = RoomSettings.DefaultEquipmentLossEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.MovingObjectsEnabledKey))
        {
            props[RoomSettings.MovingObjectsEnabledKey] = RoomSettings.DefaultMovingObjectsMode;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.EnemyBotsEnabledKey))
        {
            props[RoomSettings.EnemyBotsEnabledKey] = RoomSettings.DefaultEnemyBotsEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CorsairEnabledKey))
        {
            props[RoomSettings.CorsairEnabledKey] = RoomSettings.DefaultCorsairEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CorsairSpawnSecondKey))
        {
            props[RoomSettings.CorsairSpawnSecondKey] = RoomSettings.DefaultCorsairSpawnSecond;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CorsairHpKey))
        {
            props[RoomSettings.CorsairHpKey] = RoomSettings.DefaultCorsairHp;
            changed = true;
        }

        for (int i = 0; i < EnemyBotCatalog.AllDefinitions.Count; i++)
        {
            EnemyBotDefinition definition = EnemyBotCatalog.AllDefinitions[i];
            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.EnabledRoomKey))
            {
                props[definition.EnabledRoomKey] = definition.DefaultEnabled;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.CountRoomKey))
            {
                props[definition.CountRoomKey] = definition.DefaultCount;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.HpRoomKey))
            {
                props[definition.HpRoomKey] = definition.DefaultHp;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.ShieldRoomKey))
            {
                props[definition.ShieldRoomKey] = definition.DefaultShield;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.DamageRoomKey))
            {
                props[definition.DamageRoomKey] = definition.DefaultDamage;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.SpeedRoomKey))
            {
                props[definition.SpeedRoomKey] = definition.DefaultSpeedMultiplier;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.SpawnSecondRoomKey))
            {
                props[definition.SpawnSecondRoomKey] = definition.DefaultSpawnSecond;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.RespawnEnabledRoomKey))
            {
                props[definition.RespawnEnabledRoomKey] = RoomSettings.DefaultEnemyRespawnEnabled;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.RespawnIntervalRoomKey))
            {
                props[definition.RespawnIntervalRoomKey] = RoomSettings.DefaultEnemyRespawnIntervalSeconds;
                changed = true;
            }
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.BulletPushMultiplierKey))
        {
            props[RoomSettings.BulletPushMultiplierKey] = RoomSettings.DefaultBulletPushMultiplier;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ObstacleWeightFactorKey))
        {
            props[RoomSettings.ObstacleWeightFactorKey] = RoomSettings.DefaultObstacleWeightFactor;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.TreasureWeightFactorKey))
        {
            props[RoomSettings.TreasureWeightFactorKey] = RoomSettings.DefaultTreasureWeightFactor;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.BatteringDamageKey))
        {
            props[RoomSettings.BatteringDamageKey] = RoomSettings.DefaultBatteringDamage;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ShootingModelKey))
        {
            props[RoomSettings.ShootingModelKey] = RoomSettings.DefaultShootingModel;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SuperAttackEnabledKey))
        {
            props[RoomSettings.SuperAttackEnabledKey] = RoomSettings.DefaultSuperAttackEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.AdvancedShootingJoystickEnabledKey))
        {
            props[RoomSettings.AdvancedShootingJoystickEnabledKey] = RoomSettings.DefaultAdvancedShootingJoystickEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.HapticsEnabledKey))
        {
            props[RoomSettings.HapticsEnabledKey] = RoomSettings.DefaultHapticsEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.FpsCounterEnabledKey))
        {
            props[RoomSettings.FpsCounterEnabledKey] = RoomSettings.DefaultFpsCounterEnabled;
            changed = true;
        }

        if (changed)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }
    }

    Button EnsureSettingButton(ref TMP_Text textField, Button existingButton, string buttonName, string textName, Vector2 anchoredPosition, UnityEngine.Events.UnityAction callback)
    {
        Button button = existingButton;

        if (button == null || !button.gameObject.scene.IsValid())
        {
            Transform existing = transform.Find(buttonName);
            if (existing != null)
            {
                button = existing.GetComponent<Button>();
            }
        }

        if (button == null)
        {
            GameObject buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);
            button = buttonObject.GetComponent<Button>();

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(320f, 60f);
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => PlayUiClickAndInvoke(callback));

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.16f, 0.2f, 0.27f, 0.95f);
            image.type = Image.Type.Sliced;
        }

        if (textField == null || !textField.gameObject.scene.IsValid())
        {
            Transform existingText = button.transform.Find(textName);
            if (existingText != null)
            {
                textField = existingText.GetComponent<TMP_Text>();
            }
        }

        if (textField == null)
        {
            GameObject textObject = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(button.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            textField = textObject.GetComponent<TextMeshProUGUI>();
            textField.fontSize = 17f;
            textField.fontStyle = FontStyles.Bold;
            textField.alignment = TextAlignmentOptions.Center;
            textField.color = Color.white;
            textField.textWrappingMode = TextWrappingModes.Normal;
        }

        return button;
    }

    void CycleRoundDuration()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        float current = GetRoundDuration();
        int index = System.Array.FindIndex(RoundDurationOptions, option => Mathf.Abs(option - current) < 0.01f);
        if (index < 0)
            index = 0;

        int nextIndex = (index + 1) % RoundDurationOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.RoundDurationKey] = RoundDurationOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleObstacleDensity()
    {
        CycleDensitySetting(RoomSettings.ObstacleDensityKey, GetObstacleDensity());
    }

    void CycleObstacleDestroyEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.ObstacleDestroyEnabledKey] = !AreObstaclesDestructible();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleObstacleHp()
    {
        CycleIntSetting(RoomSettings.ObstacleHpKey, ObstacleHpOptions, GetObstacleHp(), RoomSettings.DefaultObstacleHp);
    }

    void CycleObstacleSizePercent()
    {
        CycleIntSetting(RoomSettings.ObstacleSizePercentKey, ObstacleSizePercentOptions, GetObstacleSizePercent(), RoomSettings.DefaultObstacleSizePercent);
    }

    void CycleObstacleNoBorders()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.ObstacleNoBordersKey] = !AreObstaclesBorderless();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleMapSize()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        string current = GetMapSize();
        int index = System.Array.IndexOf(MapSizeOptions, current);
        if (index < 0)
            index = 1;

        int nextIndex = (index + 1) % MapSizeOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.MapSizeKey] = MapSizeOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleMapBackground()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        int current = GetMapBackground();
        int index = System.Array.IndexOf(MapBackgroundOptions, current);
        if (index < 0)
            index = System.Array.IndexOf(MapBackgroundOptions, RoomSettings.DefaultMapBackground);
        if (index < 0)
            index = 0;

        int nextIndex = (index + 1) % MapBackgroundOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.MapBackgroundKey] = MapBackgroundOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleVisualEffectsEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.VisualEffectsEnabledKey] = !AreVisualEffectsEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleStartingVfxEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.StartingVfxEnabledKey] = !AreStartingVfxEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEndDisasterMode()
    {
        CycleStringSetting(
            RoomSettings.EndDisasterModeKey,
            EndDisasterModeOptions,
            GetEndDisasterMode(),
            RoomSettings.DefaultEndDisasterMode);
    }

    void CycleEndDisasterWarningSeconds()
    {
        CycleIntSetting(
            RoomSettings.EndDisasterWarningSecondsKey,
            EndDisasterWarningSecondOptions,
            GetEndDisasterWarningSeconds(),
            RoomSettings.DefaultEndDisasterWarningSeconds);
    }

    void CycleTreasureDensity()
    {
        CycleDensitySetting(RoomSettings.TreasureDensityKey, GetTreasureDensity());
    }

    void CycleResourceRichness()
    {
        CycleStringSetting(RoomSettings.ResourceRichnessKey, ResourceRichnessOptions, GetResourceRichness(), RoomSettings.DefaultResourceRichness);
    }

    void CycleSpaceJunkDensity()
    {
        CycleStringSetting(RoomSettings.SpaceJunkDensityKey, SpaceJunkDensityOptions, GetSpaceJunkDensity(), RoomSettings.DefaultSpaceJunkDensity);
    }

    void CycleNebulaDensity()
    {
        CycleDensitySetting(RoomSettings.NebulaDensityKey, GetNebulaDensity());
    }

    void CycleExtractionCount()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        int current = GetExtractionCount();
        int index = System.Array.IndexOf(ExtractionCountOptions, current);
        if (index < 0)
            index = 2;

        int nextIndex = (index + 1) % ExtractionCountOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.ExtractionCountKey] = ExtractionCountOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleRepairBayCount()
    {
        CycleIntSetting(
            RoomSettings.RepairBayCountKey,
            RepairBayCountOptions,
            GetRepairBayCount(),
            RoomSettings.DefaultRepairBayCount);
    }

    void CycleBoosterSlowdown()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        int current = GetBoosterSlowdownPercent();
        int index = System.Array.IndexOf(BoosterSlowdownOptions, current);
        if (index < 0)
            index = 0;

        int nextIndex = (index + 1) % BoosterSlowdownOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.BoosterSlowdownKey] = BoosterSlowdownOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleAmmoCount()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        int current = GetAmmoCount();
        int index = System.Array.IndexOf(AmmoCountOptions, current);
        if (index < 0)
            index = 1;

        int nextIndex = (index + 1) % AmmoCountOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.AmmoCountKey] = AmmoCountOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleBoosterRecoveryDelay()
    {
        CycleIntSetting(RoomSettings.BoosterRecoveryDelayKey, BoosterRecoveryDelayOptions, GetBoosterRecoveryDelay(), 0);
    }

    void CycleShipDriftEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.ShipDriftEnabledKey] = (GetShipDriftLevel() + 1) % 11;
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleLastShipTimerMultiplier()
    {
        CycleFloatSetting(RoomSettings.LastShipTimerMultiplierKey, LastShipTimerMultiplierOptions, GetLastShipTimerMultiplier(), RoomSettings.DefaultLastShipTimerMultiplier);
    }

    void CycleInventoryLossEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.InventoryLossEnabledKey] = !RoomSettings.IsInventoryLossEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEquipmentLossEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.EquipmentLossEnabledKey] = !RoomSettings.IsEquipmentLossEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleMovingObjectsEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        string current = GetMovingObjectsMode();
        int index = System.Array.IndexOf(MovingObjectsModeOptions, current);
        if (index < 0)
            index = 0;

        int nextIndex = (index + 1) % MovingObjectsModeOptions.Length;

        Hashtable props = new Hashtable();
        props[RoomSettings.MovingObjectsEnabledKey] = MovingObjectsModeOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEnemyBotsEnabled()
    {
        CycleEnemyEnabled(EnemyBotKind.Drone);
    }

    void CycleCorsairEnabled()
    {
        CycleEnemyEnabled(EnemyBotKind.Corsair);
    }

    void CycleEnemyEnabled(EnemyBotKind kind)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        Hashtable props = new Hashtable();
        bool nextValue = !RoomSettings.GetEnemyEnabled(kind);
        props[definition.EnabledRoomKey] = nextValue;
        if (kind == EnemyBotKind.Drone)
            props[RoomSettings.EnemyBotsEnabledKey] = nextValue;
        else if (kind == EnemyBotKind.Corsair)
            props[RoomSettings.CorsairEnabledKey] = nextValue;
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleCorsairSpawnSecond()
    {
        CycleEnemySpawnSecond(EnemyBotKind.Corsair);
    }

    void CycleCorsairHp()
    {
        CycleEnemyHp(EnemyBotKind.Corsair);
    }

    void CycleEnemyCount(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        int[] options = kind == EnemyBotKind.SpaceMine ? SpaceMineCountOptions : EnemyCountOptions;
        CycleIntSetting(definition.CountRoomKey, options, RoomSettings.GetEnemyCount(kind), definition.DefaultCount);
    }

    void CycleEnemyHp(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        int nextValue = GetNextOptionValue(EnemyHpOptions, RoomSettings.GetEnemyHp(kind), definition.DefaultHp);
        Hashtable props = new Hashtable();
        props[definition.HpRoomKey] = nextValue;
        if (kind == EnemyBotKind.Corsair)
            props[RoomSettings.CorsairHpKey] = nextValue;

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEnemyShield(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        CycleIntSetting(definition.ShieldRoomKey, EnemyShieldOptions, RoomSettings.GetEnemyShield(kind), definition.DefaultShield);
    }

    void CycleEnemyDamage(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        CycleIntSetting(definition.DamageRoomKey, EnemyDamageOptions, RoomSettings.GetEnemyDamage(kind), definition.DefaultDamage);
    }

    void CycleEnemySpeed(EnemyBotKind kind)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        float nextValue = GetNextOptionValue(EnemySpeedOptions, RoomSettings.GetEnemySpeedMultiplier(kind), definition.DefaultSpeedMultiplier);
        Hashtable props = new Hashtable();
        props[definition.SpeedRoomKey] = nextValue;
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEnemySpawnSecond(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        int nextValue = GetNextOptionValue(EnemySpawnSecondOptions, RoomSettings.GetEnemySpawnSecond(kind), definition.DefaultSpawnSecond);
        Hashtable props = new Hashtable();
        props[definition.SpawnSecondRoomKey] = nextValue;
        if (kind == EnemyBotKind.Corsair)
            props[RoomSettings.CorsairSpawnSecondKey] = nextValue;

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEnemyRespawnEnabled(EnemyBotKind kind)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        Hashtable props = new Hashtable();
        props[definition.RespawnEnabledRoomKey] = !RoomSettings.GetEnemyRespawnEnabled(kind);
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleEnemyRespawnInterval(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return;

        CycleIntSetting(
            definition.RespawnIntervalRoomKey,
            EnemyRespawnIntervalOptions,
            RoomSettings.GetEnemyRespawnIntervalSeconds(kind),
            RoomSettings.DefaultEnemyRespawnIntervalSeconds);
    }

    void CycleObstacleWeightFactor()
    {
        CycleIntSetting(RoomSettings.ObstacleWeightFactorKey, ObstacleWeightFactorOptions, GetObstacleWeightFactor(), RoomSettings.DefaultObstacleWeightFactor);
    }

    void CycleTreasureWeightFactor()
    {
        CycleIntSetting(RoomSettings.TreasureWeightFactorKey, TreasureWeightFactorOptions, GetTreasureWeightFactor(), RoomSettings.DefaultTreasureWeightFactor);
    }

    void CycleBulletPushMultiplier()
    {
        CycleIntSetting(
            RoomSettings.BulletPushMultiplierKey,
            BulletPushMultiplierOptions,
            GetBulletPushMultiplier(),
            RoomSettings.DefaultBulletPushMultiplier);
    }

    void CycleBatteringDamage()
    {
        CycleIntSetting(
            RoomSettings.BatteringDamageKey,
            BatteringDamageOptions,
            GetBatteringDamage(),
            RoomSettings.DefaultBatteringDamage);
    }

    void CycleShootingModel()
    {
        CycleStringSetting(
            RoomSettings.ShootingModelKey,
            ShootingModelOptions,
            GetShootingModel(),
            RoomSettings.DefaultShootingModel);
    }

    void OpenGunSetup()
    {
        if (!PhotonNetwork.InRoom || RoomSettings.GetSessionState() != RoomSettings.SessionStateInLobby)
            return;

        GunSetupOverlayUI.Show();
    }

    void CycleSuperAttackEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.SuperAttackEnabledKey] = !RoomSettings.IsSuperAttackToggleEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleAdvancedShootingJoystickEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.AdvancedShootingJoystickEnabledKey] = !RoomSettings.IsAdvancedShootingJoystickEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleHapticsEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.HapticsEnabledKey] = !RoomSettings.AreHapticsEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleFpsCounterEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.FpsCounterEnabledKey] = !RoomSettings.IsFpsCounterEnabled();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleIntSetting(string key, int[] options, int current, int fallbackIndexValue)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[key] = GetNextOptionValue(options, current, fallbackIndexValue);
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleFloatSetting(string key, float[] options, float current, float fallbackIndexValue)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[key] = GetNextOptionValue(options, current, fallbackIndexValue);
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    int GetNextOptionValue(int[] options, int current, int fallbackIndexValue)
    {
        int index = System.Array.IndexOf(options, current);
        if (index < 0)
        {
            index = System.Array.IndexOf(options, fallbackIndexValue);
            if (index < 0)
                index = GetNearestOptionIndex(options, current);
        }

        int nextIndex = (index + 1) % options.Length;
        return options[nextIndex];
    }

    float GetNextOptionValue(float[] options, float current, float fallbackIndexValue)
    {
        if (options == null || options.Length == 0)
            return fallbackIndexValue;

        int index = -1;
        for (int i = 0; i < options.Length; i++)
        {
            if (Mathf.Abs(options[i] - current) < 0.01f)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            float bestDistance = float.MaxValue;
            for (int i = 0; i < options.Length; i++)
            {
                float distance = Mathf.Abs(options[i] - current);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    index = i;
                }
            }
        }

        int nextIndex = (Mathf.Max(0, index) + 1) % options.Length;
        return options[nextIndex];
    }

    int GetNearestOptionIndex(int[] options, int target)
    {
        if (options == null || options.Length == 0)
            return 0;

        int bestIndex = 0;
        int bestDistance = Mathf.Abs(options[0] - target);
        for (int i = 1; i < options.Length; i++)
        {
            int distance = Mathf.Abs(options[i] - target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    void CycleDensitySetting(string key, string current)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        int index = System.Array.IndexOf(DensityOptions, current);
        if (index < 0)
            index = 0;

        int nextIndex = (index + 1) % DensityOptions.Length;

        Hashtable props = new Hashtable();
        props[key] = DensityOptions[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void CycleStringSetting(string key, string[] options, string current, string fallbackValue)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || options == null || options.Length == 0)
            return;

        int index = System.Array.IndexOf(options, current);
        if (index < 0)
        {
            index = System.Array.IndexOf(options, fallbackValue);
            if (index < 0)
                index = 0;
        }

        int nextIndex = (index + 1) % options.Length;

        Hashtable props = new Hashtable();
        props[key] = options[nextIndex];
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshHostSettingsUi();
    }

    void RefreshHostSettingsUi()
    {
        EnsureHostSettingsUiExists();
        EnsureLobbyNavigationUiExists();
        EnsureFullScreenLobbyUiExists();
        EnsureBottomActionButtonsLayout();

        bool isHost = PhotonNetwork.IsMasterClient;

        if (roundSettingText != null)
            roundSettingText.text = "ROUND TIME: " + FormatRoundDuration(GetRoundDuration());

        if (mapSizeSettingText != null)
            mapSizeSettingText.text = "MAP SIZE: " + FormatMapSize(GetMapSize());

        if (mapBackgroundSettingText != null)
            mapBackgroundSettingText.text = "MAP BACKGROUND: " + FormatMapBackground(GetMapBackground());

        if (visualEffectsSettingText != null)
            visualEffectsSettingText.text = "VISUALS: " + (AreVisualEffectsEnabled() ? "ON" : "OFF");

        if (startingVfxSettingText != null)
            startingVfxSettingText.text = "STARTING VFX: " + (AreStartingVfxEnabled() ? "ON" : "OFF");

        if (endDisasterSettingText != null)
            endDisasterSettingText.text = "END DISASTER: " + FormatEndDisasterMode(GetEndDisasterMode());

        if (endDisasterTimeSettingText != null)
            endDisasterTimeSettingText.text = "END DISASTER TIME: " + GetEndDisasterWarningSeconds() + "s";

        if (obstacleSettingText != null)
            obstacleSettingText.text = "OBSTACLES DENSITY: " + FormatDensity(GetObstacleDensity());

        if (obstacleDestroySettingText != null)
            obstacleDestroySettingText.text = "OBSTACLES DESTROY: " + (AreObstaclesDestructible() ? "ON" : "OFF");

        if (obstacleHpValueSettingText != null)
            obstacleHpValueSettingText.text = "OBSTACLE HP: " + GetObstacleHp();

        if (obstacleSizeSettingText != null)
            obstacleSizeSettingText.text = "OBSTACLE SIZE: " + GetObstacleSizePercent() + "%";

        if (obstacleNoBordersSettingText != null)
            obstacleNoBordersSettingText.text = "OBSTACLES NO BORDERS: " + (AreObstaclesBorderless() ? "YES" : "NO");

        if (treasureSettingText != null)
            treasureSettingText.text = "RESOURCES DENSITY: " + FormatDensity(GetTreasureDensity());

        if (resourceRichnessSettingText != null)
            resourceRichnessSettingText.text = "RESOURCES RICHNESS: " + FormatResourceRichness(GetResourceRichness());

        if (spaceJunkSettingText != null)
            spaceJunkSettingText.text = "SPACE JUNK: " + FormatSpaceJunkDensity(GetSpaceJunkDensity());

        if (nebulaSettingText != null)
            nebulaSettingText.text = "NEBULA DENSITY: " + FormatDensity(GetNebulaDensity());

        if (extractionSettingText != null)
            extractionSettingText.text = "EXTRACTION ZONES: " + GetExtractionCount();

        if (repairBaySettingText != null)
            repairBaySettingText.text = "REPAIR BAY: " + GetRepairBayCount();

        if (boosterSettingText != null)
            boosterSettingText.text = "EMPTY BOOSTER SLOWDOWN: " + GetBoosterSlowdownPercent() + "%";

        if (ammoSettingText != null)
            ammoSettingText.text = "AMMO: " + GetAmmoCount();

        if (boosterDelaySettingText != null)
            boosterDelaySettingText.text = "BOOST COOLDOWN: " + GetBoosterRecoveryDelay() + "s";

        if (shipDriftSettingText != null)
            shipDriftSettingText.text = "BRAKING DRIFT: " + GetShipDriftLevel();

        if (deathTimerSettingText != null)
            deathTimerSettingText.text = "LONE SHIP TIMER: " + FormatLastShipTimerMultiplier(GetLastShipTimerMultiplier());

        if (inventoryLossSettingText != null)
            inventoryLossSettingText.text = "INVENTORY LOSS: " + (RoomSettings.IsInventoryLossEnabled() ? "YES" : "NO");

        if (equipmentLossSettingText != null)
            equipmentLossSettingText.text = "EQUIPMENT LOSS: " + (RoomSettings.IsEquipmentLossEnabled() ? "YES" : "NO");

        if (movingObjectsSettingText != null)
            movingObjectsSettingText.text = "MOVING OBJECTS: " + FormatMovingObjectsMode(GetMovingObjectsMode());

        if (bulletPushSettingText != null)
            bulletPushSettingText.text = "BULLET PUSH: X" + GetBulletPushMultiplier();

        if (batteringSettingText != null)
            batteringSettingText.text = "BATTERING: " + FormatBatteringDamage(GetBatteringDamage());

        if (shootingModelSettingText != null)
            shootingModelSettingText.text = "SHOOTING MODEL: " + FormatShootingModel(GetShootingModel());

        if (superAttackSettingText != null)
            superAttackSettingText.text = "SUPERATTACK: " + (RoomSettings.IsSuperAttackToggleEnabled() ? "ON" : "OFF");

        if (advancedShootingJoystickSettingText != null)
            advancedShootingJoystickSettingText.text = "ADVANCED SHOOTING JOYSTICK: " + (RoomSettings.IsAdvancedShootingJoystickEnabled() ? "ON" : "OFF");

        if (hapticsSettingText != null)
            hapticsSettingText.text = "HAPTICS: " + (RoomSettings.AreHapticsEnabled() ? "ON" : "OFF");

        if (fpsCounterSettingText != null)
            fpsCounterSettingText.text = "FPS COUNTER: " + (RoomSettings.IsFpsCounterEnabled() ? "YES" : "NO");

        if (gunSetupSettingText != null)
            gunSetupSettingText.text = "GUN SETUP";

        if (obstacleWeightSettingText != null)
            obstacleWeightSettingText.text = "OBSTACLE MASS: " + RoomSettings.GetMassLabel(GetObstacleWeightFactor());

        if (treasureWeightSettingText != null)
            treasureWeightSettingText.text = "TREASURE MASS: " + RoomSettings.GetMassLabel(GetTreasureWeightFactor());

        SetSettingButtonState(roundSettingButton, isHost);
        SetSettingButtonState(mapSizeSettingButton, isHost);
        SetSettingButtonState(mapBackgroundSettingButton, isHost);
        SetSettingButtonState(visualEffectsSettingButton, isHost);
        SetSettingButtonState(startingVfxSettingButton, isHost);
        SetSettingButtonState(endDisasterSettingButton, isHost);
        SetSettingButtonState(endDisasterTimeSettingButton, isHost);
        SetSettingButtonState(obstacleSettingButton, isHost);
        SetSettingButtonState(obstacleDestroySettingButton, isHost);
        SetSettingButtonState(obstacleHpValueSettingButton, isHost);
        SetSettingButtonState(obstacleSizeSettingButton, isHost);
        SetSettingButtonState(obstacleNoBordersSettingButton, isHost);
        SetSettingButtonState(treasureSettingButton, isHost);
        SetSettingButtonState(resourceRichnessSettingButton, isHost);
        SetSettingButtonState(spaceJunkSettingButton, isHost);
        SetSettingButtonState(nebulaSettingButton, isHost);
        SetSettingButtonState(extractionSettingButton, isHost);
        SetSettingButtonState(repairBaySettingButton, isHost);
        SetSettingButtonState(boosterSettingButton, isHost);
        SetSettingButtonState(ammoSettingButton, isHost);
        SetSettingButtonState(boosterDelaySettingButton, isHost);
        SetSettingButtonState(shipDriftSettingButton, isHost);
        SetSettingButtonState(deathTimerSettingButton, isHost);
        SetSettingButtonState(inventoryLossSettingButton, isHost);
        SetSettingButtonState(equipmentLossSettingButton, isHost);
        SetSettingButtonState(movingObjectsSettingButton, isHost);
        SetSettingButtonState(bulletPushSettingButton, isHost);
        SetSettingButtonState(batteringSettingButton, isHost);
        SetSettingButtonState(shootingModelSettingButton, isHost);
        SetSettingButtonState(superAttackSettingButton, isHost);
        SetSettingButtonState(advancedShootingJoystickSettingButton, isHost);
        SetSettingButtonState(hapticsSettingButton, isHost);
        SetSettingButtonState(fpsCounterSettingButton, isHost);
        SetSettingButtonState(gunSetupSettingButton, isHost);
        if (developerGunSetupButton != null)
            developerGunSetupButton.interactable = isHost;
        SetSettingButtonState(obstacleWeightSettingButton, isHost);
        SetSettingButtonState(treasureWeightSettingButton, isHost);
        RefreshLobbyMapSelectionUi(isHost);
        RefreshLobbyNavigationButton();
        RefreshEnemySettingTexts(isHost);
        RefreshLobbyTopBar();
        RefreshLobbyScreenContent();
        if (ShouldShowFullScreenLobby())
            LayoutFullScreenLobbyUi();
    }

    void RefreshLobbyNavigationButton()
    {
        if (backToRoundsButton == null)
            return;

        if (ShouldShowFullScreenLobby())
        {
            backToRoundsButton.gameObject.SetActive(false);
            return;
        }

        bool inLobbyState = PhotonNetwork.InRoom && RoomSettings.GetSessionState() == RoomSettings.SessionStateInLobby;
        backToRoundsButton.interactable = inLobbyState;
        backToRoundsButton.gameObject.SetActive(true);
        backToRoundsButton.transform.SetAsLastSibling();

        Image image = backToRoundsButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = inLobbyState
                ? PhotonNetwork.IsMasterClient
                    ? new Color(0.58f, 0.18f, 0.18f, 0.98f)
                    : new Color(0.16f, 0.34f, 0.58f, 0.98f)
                : new Color(0.12f, 0.14f, 0.18f, 0.72f);
        }

        if (backToRoundsText != null)
        {
            backToRoundsText.text = PhotonNetwork.IsMasterClient ? "CLOSE LOBBY" : "BACK TO ROUNDS";
            backToRoundsText.fontSize = 30f;
            backToRoundsText.characterSpacing = 2.2f;
        }
    }

    void SetSettingButtonState(Button button, bool interactable)
    {
        if (button == null)
            return;

        button.interactable = interactable;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = interactable
                ? new Color(0.16f, 0.2f, 0.27f, 0.95f)
                : new Color(0.12f, 0.14f, 0.18f, 0.72f);
        }
    }

    void RefreshEnemySettingTexts(bool isHost)
    {
        for (int i = 0; i < EnemyBotCatalog.AllDefinitions.Count; i++)
        {
            EnemyBotDefinition definition = EnemyBotCatalog.AllDefinitions[i];
            SetEnemySettingText(definition.Kind, "enabled", RoomSettings.GetEnemyEnabled(definition.Kind) ? "ON" : "OFF");
            SetEnemySettingText(definition.Kind, "count", RoomSettings.GetEnemyCount(definition.Kind).ToString());
            SetEnemySettingText(definition.Kind, "respawn", RoomSettings.GetEnemyRespawnEnabled(definition.Kind) ? "YES" : "NO");
            SetEnemySettingText(definition.Kind, "hp", RoomSettings.GetEnemyHp(definition.Kind).ToString());
            SetEnemySettingText(definition.Kind, "shield", RoomSettings.GetEnemyShield(definition.Kind).ToString());
            SetEnemySettingText(definition.Kind, "damage", RoomSettings.GetEnemyDamage(definition.Kind).ToString());
            SetEnemySettingText(definition.Kind, "speed", FormatEnemySpeed(RoomSettings.GetEnemySpeedMultiplier(definition.Kind)));
            SetEnemySettingText(definition.Kind, "time", RoomSettings.GetEnemySpawnSecond(definition.Kind) + "s");
            SetEnemySettingText(definition.Kind, "respawnTime", RoomSettings.GetEnemyRespawnIntervalSeconds(definition.Kind) + "s");

            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "enabled"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "count"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "respawn"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "hp"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "shield"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "damage"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "speed"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "time"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "respawnTime"), isHost);
        }
    }

    string FormatEnemySpeed(float value)
    {
        if (Mathf.Abs(value - 0.25f) < 0.01f)
            return "x0.25";

        if (Mathf.Abs(value - 0.5f) < 0.01f)
            return "x0.5";

        if (Mathf.Abs(value - 1.5f) < 0.01f)
            return "x1.5";

        if (Mathf.Abs(value - 2f) < 0.01f)
            return "x2";

        return "x1";
    }

    string FormatLastShipTimerMultiplier(float value)
    {
        if (Mathf.Abs(value - Mathf.Round(value)) < 0.01f)
            return "X" + Mathf.RoundToInt(value);

        return "X" + value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }

    string FormatMovingObjectsMode(string mode)
    {
        switch (mode)
        {
            case RoomSettings.MovingObjectsModeOff:
                return "OFF";
            case RoomSettings.MovingObjectsModeOnlyRotate:
                return "ONLY ROTATE";
            default:
                return "ON";
        }
    }

    string FormatBatteringDamage(int damage)
    {
        return damage <= 0 ? "OFF" : damage.ToString();
    }

    string FormatEndDisasterMode(string mode)
    {
        return RoomSettings.NormalizeEndDisasterMode(mode) == RoomSettings.EndDisasterMeteor
            ? "METEOR"
            : "OFF";
    }

    void SetEnemySettingText(EnemyBotKind kind, string suffix, string text)
    {
        if (enemySettingTexts.TryGetValue(GetEnemySettingUiKey(kind, suffix), out TMP_Text textField) && textField != null)
            textField.text = text;
    }

    void OnBackToRoundsClicked()
    {
        if (!PhotonNetwork.InRoom || RoomSettings.GetSessionState() != RoomSettings.SessionStateInLobby)
            return;

        HideFullScreenLobbyFlow();
        HideMapSelectionOverlay();
        NetworkManager.ReturnToSessionBrowserFromLobby();
    }

    void OnExitLobbyClicked()
    {
        OnBackToRoundsClicked();
    }

    public override void OnLeftRoom()
    {
        HideFullScreenLobbyFlow();

        CanvasGroup cg = EnsureCanvasGroup();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    void OnDeveloperSettingsClicked()
    {
        previousMapScreenBeforeDeveloperSettings = currentScreen == LobbyScreen.MapDetails ? LobbyScreen.MapDetails : LobbyScreen.MapSelection;
        SwitchLobbyScreen(LobbyScreen.DeveloperSettings);
    }

    void OnDeveloperBackClicked()
    {
        if (currentScreen == LobbyScreen.MapDetails)
        {
            SwitchLobbyScreen(LobbyScreen.MapSelection);
            return;
        }

        SwitchLobbyScreen(previousMapScreenBeforeDeveloperSettings);
    }

    void OnDeveloperCheatClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        RefreshDeveloperCheatOverlay(string.Empty);
        developerCheatOverlayObject.SetActive(true);
        developerCheatOverlayObject.transform.SetAsLastSibling();
    }

    async void OnDeveloperCheatAddMoneyClicked()
    {
        if (PlayerProfileService.Instance == null || developerCheatOverlayObject == null)
            return;

        if (developerCheatAddMoneyButton != null)
            developerCheatAddMoneyButton.interactable = false;

        try
        {
            RefreshDeveloperCheatOverlay("Adding 5000 Astrons...", true);
            await PlayerProfileService.Instance.AddAstronsAsync(5000);
            RefreshLobbyTopBar();
            RefreshDeveloperCheatOverlay("Added 5000 Astrons.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Developer cheat add money failed: " + ex);
            RefreshDeveloperCheatOverlay("Could not add Astrons.");
        }
        finally
        {
            RefreshDeveloperCheatOverlay();
        }
    }

    void OnLaunchClicked()
    {
        if (!PhotonNetwork.InRoom || RoomSettings.GetSessionState() != RoomSettings.SessionStateInLobby)
            return;

        if (PhotonNetwork.IsMasterClient)
        {
            StartGame();
            return;
        }

        SetReady(true);
    }

    Button GetEnemySettingButton(EnemyBotKind kind, string suffix)
    {
        enemySettingButtons.TryGetValue(GetEnemySettingUiKey(kind, suffix), out Button button);
        return button;
    }

    bool ContainsEnemyRoomSettingChange(Hashtable changedProps)
    {
        foreach (System.Collections.DictionaryEntry entry in changedProps)
        {
            if (entry.Key is string key &&
                (key.StartsWith("enemy.", System.StringComparison.Ordinal) ||
                 key == RoomSettings.EnemyBotsEnabledKey ||
                 key == RoomSettings.CorsairEnabledKey ||
                 key == RoomSettings.CorsairSpawnSecondKey ||
                 key == RoomSettings.CorsairHpKey))
            {
                return true;
            }
        }

        return false;
    }

    bool ContainsLobbySettingChange(Hashtable changedProps)
    {
        if (changedProps == null)
            return false;

        return changedProps.ContainsKey(RoomSettings.RoundDurationKey) ||
               changedProps.ContainsKey(RoomSettings.MapSizeKey) ||
               changedProps.ContainsKey(RoomSettings.MapBackgroundKey) ||
               changedProps.ContainsKey(RoomSettings.SelectedMapKey) ||
               changedProps.ContainsKey(RoomSettings.VisualEffectsEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.StartingVfxEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.EndDisasterModeKey) ||
               changedProps.ContainsKey(RoomSettings.EndDisasterWarningSecondsKey) ||
               changedProps.ContainsKey(RoomSettings.ObstacleDensityKey) ||
               changedProps.ContainsKey(RoomSettings.ObstacleDestroyEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.ObstacleHpKey) ||
               changedProps.ContainsKey(RoomSettings.ObstacleSizePercentKey) ||
               changedProps.ContainsKey(RoomSettings.ObstacleNoBordersKey) ||
               changedProps.ContainsKey(RoomSettings.TreasureDensityKey) ||
               changedProps.ContainsKey(RoomSettings.ResourceRichnessKey) ||
               changedProps.ContainsKey(RoomSettings.SpaceJunkDensityKey) ||
               changedProps.ContainsKey(RoomSettings.NebulaDensityKey) ||
               changedProps.ContainsKey(RoomSettings.ExtractionCountKey) ||
               changedProps.ContainsKey(RoomSettings.RepairBayCountKey) ||
               changedProps.ContainsKey(RoomSettings.BoosterSlowdownKey) ||
               changedProps.ContainsKey(RoomSettings.AmmoCountKey) ||
               changedProps.ContainsKey(RoomSettings.BoosterRecoveryDelayKey) ||
               changedProps.ContainsKey(RoomSettings.ShipDriftEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.LastShipTimerMultiplierKey) ||
               changedProps.ContainsKey(RoomSettings.InventoryLossEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.EquipmentLossEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.MovingObjectsEnabledKey) ||
               ContainsEnemyRoomSettingChange(changedProps) ||
               changedProps.ContainsKey(RoomSettings.BulletPushMultiplierKey) ||
               changedProps.ContainsKey(RoomSettings.BatteringDamageKey) ||
               changedProps.ContainsKey(RoomSettings.ShootingModelKey) ||
               changedProps.ContainsKey(RoomSettings.SuperAttackEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.AdvancedShootingJoystickEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.HapticsEnabledKey) ||
               changedProps.ContainsKey(RoomSettings.FpsCounterEnabledKey) ||
               ContainsGunSetupRoomSettingChange(changedProps) ||
               changedProps.ContainsKey(RoomSettings.ObstacleWeightFactorKey) ||
               changedProps.ContainsKey(RoomSettings.TreasureWeightFactorKey);
    }

    bool ContainsGunSetupRoomSettingChange(Hashtable changedProps)
    {
        if (changedProps == null)
            return false;

        foreach (System.Collections.DictionaryEntry entry in changedProps)
        {
            if (entry.Key is string key && RoomSettings.IsGunSetupKey(key))
                return true;
        }

        return false;
    }

    float GetRoundDuration()
    {
        return RoomSettings.GetRoundDuration();
    }

    string GetObstacleDensity()
    {
        return GetDensitySetting(RoomSettings.ObstacleDensityKey);
    }

    bool AreObstaclesDestructible()
    {
        return RoomSettings.AreObstaclesDestructible();
    }

    int GetObstacleHp()
    {
        return RoomSettings.GetObstacleHp();
    }

    int GetObstacleSizePercent()
    {
        return RoomSettings.GetObstacleSizePercent();
    }

    bool AreObstaclesBorderless()
    {
        return RoomSettings.AreObstaclesBorderless();
    }

    string GetMapSize()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.MapSizeKey, out object value) &&
            value is string mode)
        {
            return mode;
        }

        return RoomSettings.DefaultMapSize;
    }

    int GetMapBackground()
    {
        return RoomSettings.GetMapBackgroundIndex();
    }

    bool AreVisualEffectsEnabled()
    {
        return RoomSettings.AreVisualEffectsEnabled();
    }

    bool AreStartingVfxEnabled()
    {
        return RoomSettings.AreStartingVfxEnabled();
    }

    string GetEndDisasterMode()
    {
        return RoomSettings.GetEndDisasterMode();
    }

    int GetEndDisasterWarningSeconds()
    {
        return RoomSettings.GetEndDisasterWarningSeconds();
    }

    string GetTreasureDensity()
    {
        return GetDensitySetting(RoomSettings.TreasureDensityKey);
    }

    string GetResourceRichness()
    {
        return RoomSettings.GetResourceRichness();
    }

    string GetSpaceJunkDensity()
    {
        return RoomSettings.GetSpaceJunkDensity();
    }

    string GetNebulaDensity()
    {
        return GetDensitySetting(RoomSettings.NebulaDensityKey);
    }

    int GetExtractionCount()
    {
        return RoomSettings.GetExtractionCount();
    }

    int GetRepairBayCount()
    {
        return RoomSettings.GetRepairBayCount();
    }

    int GetBoosterSlowdownPercent()
    {
        return RoomSettings.GetBoosterSlowdownPercent();
    }

    int GetAmmoCount()
    {
        return RoomSettings.GetAmmoCount();
    }

    int GetBoosterRecoveryDelay()
    {
        return RoomSettings.GetBoosterRecoveryDelay();
    }

    int GetShipDriftLevel()
    {
        return RoomSettings.GetShipDriftLevel();
    }

    float GetLastShipTimerMultiplier()
    {
        return RoomSettings.GetLastShipTimerMultiplier();
    }

    bool AreMovingObjectsEnabled()
    {
        return RoomSettings.AreMovingObjectsEnabled();
    }

    string GetMovingObjectsMode()
    {
        return RoomSettings.GetMovingObjectsMode();
    }

    bool AreEnemyBotsEnabled()
    {
        return RoomSettings.AreEnemyBotsEnabled();
    }

    bool AreCorsairsEnabled()
    {
        return RoomSettings.AreCorsairsEnabled();
    }

    int GetCorsairSpawnSecond()
    {
        return RoomSettings.GetCorsairSpawnSecond();
    }

    int GetCorsairHp()
    {
        return RoomSettings.GetCorsairHp();
    }

    int GetBulletPushMultiplier()
    {
        return RoomSettings.GetBulletPushMultiplier();
    }

    int GetBatteringDamage()
    {
        return RoomSettings.GetBatteringDamage();
    }

    string GetShootingModel()
    {
        return RoomSettings.GetShootingModel();
    }

    int GetObstacleWeightFactor()
    {
        return RoomSettings.GetObstacleWeightFactor();
    }

    int GetTreasureWeightFactor()
    {
        return RoomSettings.GetTreasureWeightFactor();
    }

    string GetDensitySetting(string key)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value) &&
            value is string density)
        {
            return density;
        }

        return "medium";
    }

    string FormatRoundDuration(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.RoundToInt(seconds % 60f);
        return minutes + ":" + secs.ToString("00");
    }

    string FormatDensity(string density)
    {
        return density.ToUpperInvariant();
    }

    string FormatResourceRichness(string richness)
    {
        return RoomSettings.NormalizeResourceRichness(richness).Replace("_", " ").ToUpperInvariant();
    }

    string FormatSpaceJunkDensity(string density)
    {
        return RoomSettings.NormalizeSpaceJunkDensity(density).ToUpperInvariant();
    }

    string FormatMapSize(string mapSize)
    {
        switch (mapSize)
        {
            case "very_large":
                return "VERY LARGE";
            case "super_large":
                return "SUPER LARGE";
            default:
                return mapSize.ToUpperInvariant();
        }
    }

    string FormatMapBackground(int backgroundIndex)
    {
        return "TLO " + Mathf.Clamp(backgroundIndex, 1, 15);
    }

    string FormatShootingModel(string model)
    {
        return RoomSettings.NormalizeShootingModel(model).ToUpperInvariant();
    }

    string GetDisplayName(Player player)
    {
        if (player == null)
            return "Unknown";

        if (!string.IsNullOrWhiteSpace(player.NickName))
            return player.NickName;

        return "Player " + player.ActorNumber;
    }
}
