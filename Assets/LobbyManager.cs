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
    const float BottomActionButtonsY = -422f;
    const float BottomActionReadyX = 350f;
    const float BottomActionBackX = 810f;
    const float BottomActionButtonWidth = 430f;
    const float BottomActionButtonHeight = 112f;
    const float LeftViewportWidth = 760f;
    const float LeftViewportHeight = 960f;
    const float LeftColumnX = -560f;
    const float LeftColumnTopY = -170f;
    const float RightTableWidth = 1240f;
    const float RightTableHeight = 760f;
    const float RightTableX = 330f;
    const float RightTableY = -290f;
    const float MapSelectionButtonX = 500f;
    const float MapSelectionButtonY = -270f;
    const float MapSelectionButtonWidth = 330f;
    const float MapSelectionButtonHeight = 180f;
    const float EnemyNameColumnWidth = 180f;
    const float EnemyColumnWidth = 118f;
    const float EnemyRowHeight = 96f;

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

    static readonly float[] RoundDurationOptions = { 60f, 90f, 120f, 150f, 180f, 210f, 240f };
    static readonly string[] DensityOptions = { "none", "low", "medium", "high" };
    static readonly string[] MapSizeOptions = { "small", "medium", "large", "very_large", "super_large" };
    static readonly int[] MapBackgroundOptions = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
    static readonly int[] ObstacleHpOptions = { 50, 100, 150, 200, 250, 300 };
    static readonly int[] ObstacleSizePercentOptions = { 50, 100, 150, 200, 250, 300, 350, 400, 450, 500 };
    static readonly int[] ExtractionCountOptions = { 1, 2, 3, 4 };
    static readonly int[] BoosterSlowdownOptions = { 30, 40, 50, 60, 70, 80, 90, 100 };
    static readonly int[] AmmoCountOptions = { 5, 10, 15, 20, 25, 30 };
    static readonly int[] BoosterRecoveryDelayOptions = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    static readonly int[] MaxInputBoostPercentOptions = { 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50 };
    static readonly int[] LastShipTimerMultiplierOptions = { 1, 2, 3, 4, 5 };
    static readonly int[] EnemyCountOptions = { 1, 2, 3, 4, 5 };
    static readonly int[] SpaceMineCountOptions = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 };
    static readonly int[] EnemyHpOptions = { 20, 40, 60, 80, 100, 120, 140, 160, 180, 200 };
    static readonly int[] EnemyShieldOptions = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200 };
    static readonly float[] EnemySpeedOptions = { 0.25f, 0.5f, 1f, 1.5f, 2f };
    static readonly int[] EnemySpawnSecondOptions = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120 };
    static readonly int[] EnemyRespawnIntervalOptions = { 15, 30, 60, 90, 120, 150 };
    static readonly int[] BulletPushMultiplierOptions = { 1, 2, 3, 4, 5 };
    static readonly int[] ObstacleWeightFactorOptions = { 2, 6, 12, RoomSettings.MaxObstacleWeightFactor };
    static readonly int[] TreasureWeightFactorOptions = { 2, 6, 12 };

    public Button readyButton;
    public TMP_Text readyText;
    public TMP_Text playerStatusListText;
    public TMP_Text roundSettingText;
    public TMP_Text mapSizeSettingText;
    public TMP_Text mapBackgroundSettingText;
    public TMP_Text visualEffectsSettingText;
    public TMP_Text obstacleSettingText;
    public TMP_Text obstacleDestroySettingText;
    public TMP_Text obstacleHpValueSettingText;
    public TMP_Text obstacleSizeSettingText;
    public TMP_Text obstacleNoBordersSettingText;
    public TMP_Text treasureSettingText;
    public TMP_Text nebulaSettingText;
    public TMP_Text extractionSettingText;
    public TMP_Text boosterSettingText;
    public TMP_Text ammoSettingText;
    public TMP_Text boosterDelaySettingText;
    public TMP_Text maxInputBoostSettingText;
    public TMP_Text shipDriftSettingText;
    public TMP_Text deathTimerSettingText;
    public TMP_Text movingObjectsSettingText;
    public TMP_Text enemyBotsSettingText;
    public TMP_Text corsairSettingText;
    public TMP_Text corsairTimeSettingText;
    public TMP_Text corsairHpSettingText;
    public TMP_Text bulletPushSettingText;
    public TMP_Text obstacleWeightSettingText;
    public TMP_Text treasureWeightSettingText;
    public Button roundSettingButton;
    public Button mapSizeSettingButton;
    public Button mapBackgroundSettingButton;
    public Button visualEffectsSettingButton;
    public Button obstacleSettingButton;
    public Button obstacleDestroySettingButton;
    public Button obstacleHpValueSettingButton;
    public Button obstacleSizeSettingButton;
    public Button obstacleNoBordersSettingButton;
    public Button treasureSettingButton;
    public Button nebulaSettingButton;
    public Button extractionSettingButton;
    public Button boosterSettingButton;
    public Button ammoSettingButton;
    public Button boosterDelaySettingButton;
    public Button maxInputBoostSettingButton;
    public Button shipDriftSettingButton;
    public Button deathTimerSettingButton;
    public Button movingObjectsSettingButton;
    public Button bulletPushSettingButton;
    public Button obstacleWeightSettingButton;
    public Button treasureWeightSettingButton;
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
    ScrollRect leftSettingsScrollRect;
    RectTransform leftSettingsViewportRect;
    RectTransform leftSettingsContentRect;
    RectTransform enemyTableRootRect;
    GameObject mapSelectionOverlayObject;
    TMP_Text mapSelectionOverlayTitleText;
    Button mapSelectionOverlayCloseButton;
    readonly List<Button> mapSelectionTileButtons = new List<Button>();
    bool leftSettingsScrollInitialized;

    CanvasGroup EnsureCanvasGroup()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = gameObject.AddComponent<CanvasGroup>();
        }

        return cg;
    }

    void Start()
    {
        PlayerMovement.gameStarted = false;
        PlayerShooting.gameStarted = false;

        EnsurePlayerStatusListExists();
        EnsureHostSettingsUiExists();
        EnsureLobbyNavigationUiExists();

        if (PhotonNetwork.InRoom)
        {
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
            readyButton.onClick.AddListener(ToggleReady);
        }

        if (PhotonNetwork.InRoom)
        {
            SetReady(false);
        }

        RefreshPlayerStatusList();
        RefreshHostSettingsUi();

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started && started)
        {
            Debug.Log("GAME ALREADY STARTED (Start)");
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
        EnsurePlayerStatusListExists();
        EnsureHostSettingsUiExists();
        EnsureLobbyNavigationUiExists();
        EnsureBottomActionButtonsLayout();
        EnsureDefaultRoomSettings();

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

        if (changedProps.ContainsKey(RoomSettings.RoundDurationKey) ||
            changedProps.ContainsKey(RoomSettings.MapSizeKey) ||
            changedProps.ContainsKey(RoomSettings.MapBackgroundKey) ||
            changedProps.ContainsKey(RoomSettings.SelectedMapKey) ||
            changedProps.ContainsKey(RoomSettings.VisualEffectsEnabledKey) ||
            changedProps.ContainsKey(RoomSettings.ObstacleDensityKey) ||
            changedProps.ContainsKey(RoomSettings.ObstacleDestroyEnabledKey) ||
            changedProps.ContainsKey(RoomSettings.ObstacleHpKey) ||
            changedProps.ContainsKey(RoomSettings.ObstacleSizePercentKey) ||
            changedProps.ContainsKey(RoomSettings.ObstacleNoBordersKey) ||
            changedProps.ContainsKey(RoomSettings.TreasureDensityKey) ||
            changedProps.ContainsKey(RoomSettings.NebulaDensityKey) ||
            changedProps.ContainsKey(RoomSettings.ExtractionCountKey) ||
            changedProps.ContainsKey(RoomSettings.BoosterSlowdownKey) ||
            changedProps.ContainsKey(RoomSettings.AmmoCountKey) ||
            changedProps.ContainsKey(RoomSettings.BoosterRecoveryDelayKey) ||
            changedProps.ContainsKey(RoomSettings.MaxInputBoostPercentKey) ||
            changedProps.ContainsKey(RoomSettings.ShipDriftEnabledKey) ||
            changedProps.ContainsKey(RoomSettings.LastShipTimerMultiplierKey) ||
            changedProps.ContainsKey(RoomSettings.MovingObjectsEnabledKey) ||
            ContainsEnemyRoomSettingChange(changedProps) ||
            changedProps.ContainsKey(RoomSettings.BulletPushMultiplierKey) ||
            changedProps.ContainsKey(RoomSettings.ObstacleWeightFactorKey) ||
            changedProps.ContainsKey(RoomSettings.TreasureWeightFactorKey))
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
        Debug.Log("SPRAWDZAM READY");

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

        Debug.Log("WSZYSCY GOTOWI");

        if (PhotonNetwork.IsMasterClient)
        {
            StartGame();
        }
    }

    void StartGame()
    {
        Debug.Log("START GRY");
        GameTimer.StartGame();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(RoomSettings.RoundDurationKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.MapSizeKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.MapBackgroundKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.SelectedMapKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.VisualEffectsEnabledKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.ObstacleDensityKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.ObstacleDestroyEnabledKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.ObstacleHpKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.ObstacleSizePercentKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.ObstacleNoBordersKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.TreasureDensityKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.NebulaDensityKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.ExtractionCountKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.BoosterSlowdownKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.AmmoCountKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.BoosterRecoveryDelayKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.MaxInputBoostPercentKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.ShipDriftEnabledKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.LastShipTimerMultiplierKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.MovingObjectsEnabledKey) ||
            ContainsEnemyRoomSettingChange(propertiesThatChanged) ||
            propertiesThatChanged.ContainsKey(RoomSettings.BulletPushMultiplierKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.ObstacleWeightFactorKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.TreasureWeightFactorKey))
        {
            RefreshHostSettingsUi();
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
            Debug.Log("GAME STARTED (ROOM PROP)");
            HideLobby();
            if (!hasRecordedCurrentRound)
            {
                hasRecordedCurrentRound = true;
                _ = RecordStartedGameAsync();
            }
        }
        else
        {
            Debug.Log("GAME RESET TO LOBBY");
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

        CanvasGroup cg = EnsureCanvasGroup();
        cg.alpha = 0;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    void ShowLobby()
    {
        SetGameplayHudVisible(false);

        CanvasGroup cg = EnsureCanvasGroup();
        cg.alpha = 1;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        HideMapSelectionOverlay();
        EnsureBottomActionButtonsLayout();
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
        obstacleSettingButton = EnsureSettingButton(ref obstacleSettingText, obstacleSettingButton, "ObstacleSettingButton", "ObstacleSettingText", Vector2.zero, CycleObstacleDensity);
        obstacleDestroySettingButton = EnsureSettingButton(ref obstacleDestroySettingText, obstacleDestroySettingButton, "ObstacleDestroySettingButton", "ObstacleDestroySettingText", Vector2.zero, CycleObstacleDestroyEnabled);
        obstacleHpValueSettingButton = EnsureSettingButton(ref obstacleHpValueSettingText, obstacleHpValueSettingButton, "ObstacleHpValueSettingButton", "ObstacleHpValueSettingText", Vector2.zero, CycleObstacleHp);
        obstacleSizeSettingButton = EnsureSettingButton(ref obstacleSizeSettingText, obstacleSizeSettingButton, "ObstacleSizeSettingButton", "ObstacleSizeSettingText", Vector2.zero, CycleObstacleSizePercent);
        obstacleNoBordersSettingButton = EnsureSettingButton(ref obstacleNoBordersSettingText, obstacleNoBordersSettingButton, "ObstacleNoBordersSettingButton", "ObstacleNoBordersSettingText", Vector2.zero, CycleObstacleNoBorders);
        treasureSettingButton = EnsureSettingButton(ref treasureSettingText, treasureSettingButton, "TreasureSettingButton", "TreasureSettingText", Vector2.zero, CycleTreasureDensity);
        nebulaSettingButton = EnsureSettingButton(ref nebulaSettingText, nebulaSettingButton, "NebulaSettingButton", "NebulaSettingText", Vector2.zero, CycleNebulaDensity);
        extractionSettingButton = EnsureSettingButton(ref extractionSettingText, extractionSettingButton, "ExtractionSettingButton", "ExtractionSettingText", Vector2.zero, CycleExtractionCount);
        boosterSettingButton = EnsureSettingButton(ref boosterSettingText, boosterSettingButton, "BoosterSettingButton", "BoosterSettingText", Vector2.zero, CycleBoosterSlowdown);
        ammoSettingButton = EnsureSettingButton(ref ammoSettingText, ammoSettingButton, "AmmoSettingButton", "AmmoSettingText", Vector2.zero, CycleAmmoCount);
        boosterDelaySettingButton = EnsureSettingButton(ref boosterDelaySettingText, boosterDelaySettingButton, "BoosterDelaySettingButton", "BoosterDelaySettingText", Vector2.zero, CycleBoosterRecoveryDelay);
        maxInputBoostSettingButton = EnsureSettingButton(ref maxInputBoostSettingText, maxInputBoostSettingButton, "MaxInputBoostSettingButton", "MaxInputBoostSettingText", Vector2.zero, CycleMaxInputBoostPercent);
        shipDriftSettingButton = EnsureSettingButton(ref shipDriftSettingText, shipDriftSettingButton, "ShipDriftSettingButton", "ShipDriftSettingText", Vector2.zero, CycleShipDriftEnabled);
        deathTimerSettingButton = EnsureSettingButton(ref deathTimerSettingText, deathTimerSettingButton, "DeathTimerSettingButton", "DeathTimerSettingText", Vector2.zero, CycleLastShipTimerMultiplier);
        movingObjectsSettingButton = EnsureSettingButton(ref movingObjectsSettingText, movingObjectsSettingButton, "MovingObjectsSettingButton", "MovingObjectsSettingText", Vector2.zero, CycleMovingObjectsEnabled);
        bulletPushSettingButton = EnsureSettingButton(ref bulletPushSettingText, bulletPushSettingButton, "BulletPushSettingButton", "BulletPushSettingText", Vector2.zero, CycleBulletPushMultiplier);
        obstacleWeightSettingButton = EnsureSettingButton(ref obstacleWeightSettingText, obstacleWeightSettingButton, "ObstacleWeightSettingButton", "ObstacleWeightSettingText", Vector2.zero, CycleObstacleWeightFactor);
        treasureWeightSettingButton = EnsureSettingButton(ref treasureWeightSettingText, treasureWeightSettingButton, "TreasureWeightSettingButton", "TreasureWeightSettingText", Vector2.zero, CycleTreasureWeightFactor);

        AttachLeftSectionButton(roundSettingButton, "ROUND RULES");
        AttachLeftSectionButton(mapSizeSettingButton, "ROUND RULES");
        AttachLeftSectionButton(deathTimerSettingButton, "ROUND RULES");

        AttachLeftSectionButton(obstacleSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(obstacleDestroySettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(obstacleHpValueSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(obstacleSizeSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(obstacleNoBordersSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(treasureSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(nebulaSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(extractionSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(movingObjectsSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(obstacleWeightSettingButton, "ENVIRONMENT");
        AttachLeftSectionButton(treasureWeightSettingButton, "ENVIRONMENT");

        AttachLeftSectionButton(mapBackgroundSettingButton, "COSMETICS");
        AttachLeftSectionButton(visualEffectsSettingButton, "COSMETICS");

        AttachLeftSectionButton(boosterSettingButton, "FEELING");
        AttachLeftSectionButton(ammoSettingButton, "FEELING");
        AttachLeftSectionButton(boosterDelaySettingButton, "FEELING");
        AttachLeftSectionButton(maxInputBoostSettingButton, "FEELING");
        AttachLeftSectionButton(shipDriftSettingButton, "FEELING");
        AttachLeftSectionButton(bulletPushSettingButton, "FEELING");

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
            mapSelectionButton.onClick.AddListener(OnMapSelectionClicked);

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
        panelRect.sizeDelta = new Vector2(1440f, 780f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.94f);

        mapSelectionOverlayTitleText = CreateStandaloneLabel(panelObject.transform, "LobbyMapSelectionTitle", "SELECT MAP", new Vector2(34f, -26f), new Vector2(400f, 34f), 28f, TextAlignmentOptions.Left);

        TMP_Text subtitle = CreateStandaloneLabel(panelObject.transform, "LobbyMapSelectionSubtitle", "Choose a preset map for the round.", new Vector2(36f, -68f), new Vector2(540f, 24f), 16f, TextAlignmentOptions.Left);
        subtitle.fontStyle = FontStyles.Normal;
        subtitle.color = new Color(0.78f, 0.84f, 0.91f, 0.92f);

        GameObject closeButtonObject = FindOrCreateChild(panelObject, "LobbyMapSelectionCloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        mapSelectionOverlayCloseButton = closeButtonObject.GetComponent<Button>();
        if (mapSelectionOverlayCloseButton != null)
        {
            mapSelectionOverlayCloseButton.onClick.RemoveAllListeners();
            mapSelectionOverlayCloseButton.onClick.AddListener(HideMapSelectionOverlay);

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
        for (int i = 0; i < maps.Count; i++)
        {
            LobbyMapDefinition map = maps[i];
            GameObject tileObject = new GameObject("LobbyMapTile_" + map.Id, typeof(RectTransform), typeof(Image), typeof(Button));
            tileObject.transform.SetParent(panelObject.transform, false);

            RectTransform tileRect = tileObject.GetComponent<RectTransform>();
            tileRect.anchorMin = new Vector2(0.5f, 0.5f);
            tileRect.anchorMax = new Vector2(0.5f, 0.5f);
            tileRect.pivot = new Vector2(0.5f, 0.5f);
            int column = i % 2;
            int row = i / 2;
            tileRect.anchoredPosition = new Vector2(-310f + (column * 620f), 110f - (row * 280f));
            tileRect.sizeDelta = new Vector2(540f, 250f);

            Image tileImage = tileObject.GetComponent<Image>();
            tileImage.color = Color.white;

            Button tileButton = tileObject.GetComponent<Button>();
            string mapId = map.Id;
            tileButton.onClick.AddListener(() => OnMapTileSelected(mapId));
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

        EnsureLobbyMapUiExists();
        RefreshLobbyMapSelectionUi(PhotonNetwork.IsMasterClient);

        if (mapSelectionOverlayObject != null)
        {
            mapSelectionOverlayObject.SetActive(true);
            mapSelectionOverlayObject.transform.SetAsLastSibling();
        }
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
        Hashtable props = new Hashtable();
        LobbyMapCatalog.ApplyToProperties(selectedMap, props);
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        HideMapSelectionOverlay();
        RefreshHostSettingsUi();
    }

    Sprite LoadLobbyBackgroundSprite(int backgroundIndex)
    {
        int clampedIndex = Mathf.Clamp(backgroundIndex, 1, 12);
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
            float rowY = -132f - (i * EnemyRowHeight);
            EnsureEnemyRowLabel(definition, new Vector2(26f, rowY));

            EnsureEnemySettingButton(definition, "enabled", GetEnemyCellPosition(0, rowY), () => CycleEnemyEnabled(definition.Kind));
            EnsureEnemySettingButton(definition, "count", GetEnemyCellPosition(1, rowY), () => CycleEnemyCount(definition.Kind));
            EnsureEnemySettingButton(definition, "respawn", GetEnemyCellPosition(2, rowY), () => CycleEnemyRespawnEnabled(definition.Kind));
            EnsureEnemySettingButton(definition, "hp", GetEnemyCellPosition(3, rowY), () => CycleEnemyHp(definition.Kind));
            EnsureEnemySettingButton(definition, "shield", GetEnemyCellPosition(4, rowY), () => CycleEnemyShield(definition.Kind));
            EnsureEnemySettingButton(definition, "speed", GetEnemyCellPosition(5, rowY), () => CycleEnemySpeed(definition.Kind));
            EnsureEnemySettingButton(definition, "time", GetEnemyCellPosition(6, rowY), () => CycleEnemySpawnSecond(definition.Kind));
            EnsureEnemySettingButton(definition, "respawnTime", GetEnemyCellPosition(7, rowY), () => CycleEnemyRespawnInterval(definition.Kind));
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
                rect.sizeDelta = new Vector2(EnemyColumnWidth - 8f, 58f);
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
        if (leftSettingsViewportRect != null && leftSettingsViewportRect.gameObject.scene.IsValid())
            return;

        GameObject viewportObject = FindOrCreateChild(gameObject, "LobbySettingsViewport", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        leftSettingsViewportRect = viewportObject.GetComponent<RectTransform>();
        leftSettingsViewportRect.anchorMin = new Vector2(0.5f, 1f);
        leftSettingsViewportRect.anchorMax = new Vector2(0.5f, 1f);
        leftSettingsViewportRect.pivot = new Vector2(0.5f, 1f);
        leftSettingsViewportRect.anchoredPosition = new Vector2(LeftColumnX, LeftColumnTopY);
        leftSettingsViewportRect.sizeDelta = new Vector2(LeftViewportWidth, LeftViewportHeight);

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
    }

    void EnsureEnemyTableUiExists()
    {
        if (enemyTableRootRect != null && enemyTableRootRect.gameObject.scene.IsValid())
            return;

        GameObject tableObject = FindOrCreateChild(gameObject, "EnemySettingsTable", typeof(RectTransform), typeof(Image));
        enemyTableRootRect = tableObject.GetComponent<RectTransform>();
        enemyTableRootRect.anchorMin = new Vector2(0.5f, 1f);
        enemyTableRootRect.anchorMax = new Vector2(0.5f, 1f);
        enemyTableRootRect.pivot = new Vector2(0.5f, 1f);
        enemyTableRootRect.anchoredPosition = new Vector2(RightTableX, RightTableY);
        enemyTableRootRect.sizeDelta = new Vector2(RightTableWidth, RightTableHeight);

        Image bg = tableObject.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.09f, 0.13f, 0.82f);

        EnsureTableHeaderLabel("EnemyTableTitle", "ENEMIES", new Vector2(24f, -18f), new Vector2(220f, 30f), 24f, TextAlignmentOptions.Left);
        EnsureTableHeaderLabel("EnemyHeader_ACTIVE", "ACTIVE", GetEnemyHeaderPosition(0), new Vector2(EnemyColumnWidth - 8f, 26f), 16f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_COUNT", "COUNT", GetEnemyHeaderPosition(1), new Vector2(EnemyColumnWidth - 8f, 26f), 16f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_RESPAWN", "RESPAWN", GetEnemyHeaderPosition(2), new Vector2(EnemyColumnWidth - 8f, 26f), 16f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_HP", "HP", GetEnemyHeaderPosition(3), new Vector2(EnemyColumnWidth - 8f, 26f), 16f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_SHIELD", "SHIELD", GetEnemyHeaderPosition(4), new Vector2(EnemyColumnWidth - 8f, 26f), 16f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_SPEED", "SPEED", GetEnemyHeaderPosition(5), new Vector2(EnemyColumnWidth - 8f, 26f), 16f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_FIRSTRESPAWN", "FIRST\nRESPAWN", GetEnemyHeaderPosition(6), new Vector2(EnemyColumnWidth - 8f, 42f), 15f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_RESPAWNLOOP", "RESPAWN\nLOOP", GetEnemyHeaderPosition(7), new Vector2(EnemyColumnWidth - 8f, 42f), 15f, TextAlignmentOptions.Center);
    }

    Vector2 GetEnemyHeaderPosition(int columnIndex)
    {
        return new Vector2(EnemyNameColumnWidth + 18f + (columnIndex * EnemyColumnWidth), -58f);
    }

    void EnsureEnemyRowLabel(EnemyBotDefinition definition, Vector2 anchoredPosition)
    {
        if (definition == null || enemyTableRootRect == null)
            return;

        if (!enemyRowLabels.TryGetValue(definition.Id, out TMP_Text label) || label == null || !label.gameObject.scene.IsValid())
        {
            label = CreateStandaloneLabel(enemyTableRootRect.transform, "EnemyRowLabel_" + definition.Id, definition.DisplayName.ToUpperInvariant(), anchoredPosition, new Vector2(EnemyNameColumnWidth - 12f, 32f), 18f, TextAlignmentOptions.Left);
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

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.TreasureDensityKey))
        {
            props[RoomSettings.TreasureDensityKey] = "medium";
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

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.MaxInputBoostPercentKey))
        {
            props[RoomSettings.MaxInputBoostPercentKey] = RoomSettings.DefaultMaxInputBoostPercent;
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

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.MovingObjectsEnabledKey))
        {
            props[RoomSettings.MovingObjectsEnabledKey] = RoomSettings.DefaultMovingObjectsEnabled;
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
        button.onClick.AddListener(callback);

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

    void CycleTreasureDensity()
    {
        CycleDensitySetting(RoomSettings.TreasureDensityKey, GetTreasureDensity());
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

    void CycleMaxInputBoostPercent()
    {
        CycleIntSetting(RoomSettings.MaxInputBoostPercentKey, MaxInputBoostPercentOptions, GetMaxInputBoostPercent(), RoomSettings.DefaultMaxInputBoostPercent);
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
        CycleIntSetting(RoomSettings.LastShipTimerMultiplierKey, LastShipTimerMultiplierOptions, GetLastShipTimerMultiplier(), RoomSettings.DefaultLastShipTimerMultiplier);
    }

    void CycleMovingObjectsEnabled()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        props[RoomSettings.MovingObjectsEnabledKey] = !AreMovingObjectsEnabled();
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

    void CycleIntSetting(string key, int[] options, int current, int fallbackIndexValue)
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

    void RefreshHostSettingsUi()
    {
        EnsureHostSettingsUiExists();
        EnsureLobbyNavigationUiExists();
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

        if (nebulaSettingText != null)
            nebulaSettingText.text = "NEBULA DENSITY: " + FormatDensity(GetNebulaDensity());

        if (extractionSettingText != null)
            extractionSettingText.text = "EXTRACTION ZONES: " + GetExtractionCount();

        if (boosterSettingText != null)
            boosterSettingText.text = "EMPTY BOOSTER SLOWDOWN: " + GetBoosterSlowdownPercent() + "%";

        if (ammoSettingText != null)
            ammoSettingText.text = "AMMO: " + GetAmmoCount();

        if (boosterDelaySettingText != null)
            boosterDelaySettingText.text = "BOOST COOLDOWN: " + GetBoosterRecoveryDelay() + "s";

        if (maxInputBoostSettingText != null)
            maxInputBoostSettingText.text = "MAX BOOST BONUS: +" + GetMaxInputBoostPercent() + "%";

        if (shipDriftSettingText != null)
            shipDriftSettingText.text = "BRAKING DRIFT: " + GetShipDriftLevel();

        if (deathTimerSettingText != null)
            deathTimerSettingText.text = "LONE SHIP TIMER: X" + GetLastShipTimerMultiplier();

        if (movingObjectsSettingText != null)
            movingObjectsSettingText.text = "MOVING OBJECTS: " + (AreMovingObjectsEnabled() ? "ON" : "OFF");

        if (bulletPushSettingText != null)
            bulletPushSettingText.text = "BULLET PUSH: X" + GetBulletPushMultiplier();

        if (obstacleWeightSettingText != null)
            obstacleWeightSettingText.text = "OBSTACLE MASS: " + RoomSettings.GetMassLabel(GetObstacleWeightFactor());

        if (treasureWeightSettingText != null)
            treasureWeightSettingText.text = "TREASURE MASS: " + RoomSettings.GetMassLabel(GetTreasureWeightFactor());

        SetSettingButtonState(roundSettingButton, isHost);
        SetSettingButtonState(mapSizeSettingButton, isHost);
        SetSettingButtonState(mapBackgroundSettingButton, isHost);
        SetSettingButtonState(visualEffectsSettingButton, isHost);
        SetSettingButtonState(obstacleSettingButton, isHost);
        SetSettingButtonState(obstacleDestroySettingButton, isHost);
        SetSettingButtonState(obstacleHpValueSettingButton, isHost);
        SetSettingButtonState(obstacleSizeSettingButton, isHost);
        SetSettingButtonState(obstacleNoBordersSettingButton, isHost);
        SetSettingButtonState(treasureSettingButton, isHost);
        SetSettingButtonState(nebulaSettingButton, isHost);
        SetSettingButtonState(extractionSettingButton, isHost);
        SetSettingButtonState(boosterSettingButton, isHost);
        SetSettingButtonState(ammoSettingButton, isHost);
        SetSettingButtonState(boosterDelaySettingButton, isHost);
        SetSettingButtonState(maxInputBoostSettingButton, isHost);
        SetSettingButtonState(shipDriftSettingButton, isHost);
        SetSettingButtonState(deathTimerSettingButton, isHost);
        SetSettingButtonState(movingObjectsSettingButton, isHost);
        SetSettingButtonState(bulletPushSettingButton, isHost);
        SetSettingButtonState(obstacleWeightSettingButton, isHost);
        SetSettingButtonState(treasureWeightSettingButton, isHost);
        RefreshLobbyMapSelectionUi(isHost);
        RefreshLobbyNavigationButton();
        RefreshEnemySettingTexts(isHost);
    }

    void RefreshLobbyNavigationButton()
    {
        if (backToRoundsButton == null)
            return;

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
            SetEnemySettingText(definition.Kind, "speed", FormatEnemySpeed(RoomSettings.GetEnemySpeedMultiplier(definition.Kind)));
            SetEnemySettingText(definition.Kind, "time", RoomSettings.GetEnemySpawnSecond(definition.Kind) + "s");
            SetEnemySettingText(definition.Kind, "respawnTime", RoomSettings.GetEnemyRespawnIntervalSeconds(definition.Kind) + "s");

            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "enabled"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "count"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "respawn"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "hp"), isHost);
            SetSettingButtonState(GetEnemySettingButton(definition.Kind, "shield"), isHost);
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

    void SetEnemySettingText(EnemyBotKind kind, string suffix, string text)
    {
        if (enemySettingTexts.TryGetValue(GetEnemySettingUiKey(kind, suffix), out TMP_Text textField) && textField != null)
            textField.text = text;
    }

    void OnBackToRoundsClicked()
    {
        if (!PhotonNetwork.InRoom || RoomSettings.GetSessionState() != RoomSettings.SessionStateInLobby)
            return;

        HideMapSelectionOverlay();
        NetworkManager.ReturnToSessionBrowserFromLobby();
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

    string GetTreasureDensity()
    {
        return GetDensitySetting(RoomSettings.TreasureDensityKey);
    }

    string GetNebulaDensity()
    {
        return GetDensitySetting(RoomSettings.NebulaDensityKey);
    }

    int GetExtractionCount()
    {
        return RoomSettings.GetExtractionCount();
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

    int GetMaxInputBoostPercent()
    {
        return RoomSettings.GetMaxInputBoostPercent();
    }

    int GetShipDriftLevel()
    {
        return RoomSettings.GetShipDriftLevel();
    }

    int GetLastShipTimerMultiplier()
    {
        return RoomSettings.GetLastShipTimerMultiplier();
    }

    bool AreMovingObjectsEnabled()
    {
        return RoomSettings.AreMovingObjectsEnabled();
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
        return "TLO " + Mathf.Clamp(backgroundIndex, 1, 12);
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
