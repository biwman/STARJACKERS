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

public partial class LobbyManager : MonoBehaviourPunCallbacks
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
    const float MapEffectChanceTableHeight = 270f;
    const float RightTableX = 330f;
    const float RightTableY = -290f;
    const float MapSelectionButtonX = 500f;
    const float MapSelectionButtonY = -270f;
    const float MapSelectionButtonWidth = 330f;
    const float MapSelectionButtonHeight = 180f;
    const float MapDeathLossBadgeWidth = 256f;
    const float MapDeathLossBadgeHeight = 244f;
    const float MapDeathLossBadgeIconWidth = 208f;
    const float MapDeathLossBadgeIconHeight = 164f;
    const float EnemyNameColumnWidth = 180f;
    const float EnemyColumnWidth = 98f;
    const float EnemyRowHeight = 56f;
    const float MapEffectChanceNameColumnWidth = 260f;
    const float MapEffectChanceColumnWidth = 128f;
    const float MapEffectChanceRowHeight = 46f;
    const float FullScreenSideMargin = 34f;
    const float FullScreenTopMargin = 24f;
    const float FullScreenBottomMargin = 34f;
    const float LobbyTopBarHeight = 70f;
    const float MapTileWidth = 520f;
    const float MapTileHeight = 250f;
    const float MapTileSpacingX = 48f;
    const float MapTileSpacingY = 36f;
    const float MapSelectionContentTopY = -140f;
    const float MapUnlockRequirementFullScreenFontSize = 20f;
    const float MapUnlockRequirementOverlayFontSize = 19f;
    const float MapUnlockRequirementBottomOffset = 10f;
    const float MapUnlockRequirementMinHeight = 86f;
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
        "ComplexAmmoBar",
        "SuperAttackJoystickBG",
        "WeaponSwitchButton",
        "TimerText",
        "HP_Bar",
        "Shield_Bar",
        "VitalsIconHud",
        "Booster_Bar",
        "ScoreText"
    };

    static readonly string[] DeprecatedLobbySettingObjectNames =
    {
        "AdvancedBackgroundSettingButton",
        "AdvancedBackgroundSettingText",
        "StartingVfxSettingButton",
        "StartingVfxSettingText",
        "AmmoSettingButton",
        "AmmoSettingText",
        "ShootingModelSettingButton",
        "ShootingModelSettingText",
        "SuperAttackSettingButton",
        "SuperAttackSettingText",
        "AdvancedMovingJoystickSettingButton",
        "AdvancedMovingJoystickSettingText",
        "AdvancedShootingJoystickSettingButton",
        "AdvancedShootingJoystickSettingText",
        "DynamicUseSettingButton",
        "DynamicUseSettingText",
        "MaxInputBoostSettingButton",
        "MaxInputBoostSettingText",
        "readyButton",
        "ReadyText"
    };

    static readonly float[] RoundDurationOptions = { 60f, 90f, 120f, 150f, 180f, 210f, 240f, 270f, 300f, 330f, 360f };
    static readonly string[] DensityOptions = { "none", "low", "medium", "high" };
    static readonly string[] TreasureDensityOptions =
    {
        RoomSettings.TreasureDensityNone,
        RoomSettings.TreasureDensityVeryLow,
        RoomSettings.TreasureDensityLow,
        RoomSettings.TreasureDensityMedium,
        RoomSettings.TreasureDensityHigh
    };
    static readonly string[] NebulaSizeOptions =
    {
        RoomSettings.NebulaSizeVerySmall,
        RoomSettings.NebulaSizeSmall,
        RoomSettings.NebulaSizeNormal,
        RoomSettings.NebulaSizeBig,
        RoomSettings.NebulaSizeVeryBig
    };
    static readonly string[] ResourceRichnessOptions =
    {
        RoomSettings.ResourceRichnessExtremelyLow,
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
    static readonly string[] ContainersDensityOptions =
    {
        RoomSettings.ContainersDensityNone,
        RoomSettings.ContainersDensityVeryLow,
        RoomSettings.ContainersDensityLow,
        RoomSettings.ContainersDensityMedium,
        RoomSettings.ContainersDensityHigh,
        RoomSettings.ContainersDensityVeryHigh
    };
    static readonly string[] RadioactiveTreasureDensityOptions =
    {
        RoomSettings.RadioactiveTreasureDensityOff,
        RoomSettings.RadioactiveTreasureDensityLow,
        RoomSettings.RadioactiveTreasureDensityMedium,
        RoomSettings.RadioactiveTreasureDensityHigh
    };
    static readonly string[] AlienSecretsDensityOptions =
    {
        RoomSettings.AlienSecretsDensityNone,
        RoomSettings.AlienSecretsDensityVeryLow,
        RoomSettings.AlienSecretsDensityLow,
        RoomSettings.AlienSecretsDensityMedium,
        RoomSettings.AlienSecretsDensityHigh,
        RoomSettings.AlienSecretsDensityVeryHigh
    };
    static readonly string[] ArtifactAsteroidsDensityOptions =
    {
        RoomSettings.ArtifactAsteroidsDensityOff,
        RoomSettings.ArtifactAsteroidsDensityVeryLow,
        RoomSettings.ArtifactAsteroidsDensityLow,
        RoomSettings.ArtifactAsteroidsDensityMedium,
        RoomSettings.ArtifactAsteroidsDensityHigh
    };
    static readonly string[] MapSizeOptions = { "small", "medium", "large", "very_large", "super_large" };
    static readonly int[] MapBackgroundOptions = Enumerable.Range(1, RoomSettings.MaxMapBackground).ToArray();
    static readonly int[] ObstacleHpOptions = { 50, 100, 150, 200, 250, 300, 350, 400, 450, 500 };
    static readonly int[] ObstacleSizePercentOptions = { 50, 100, 150, 200, 250, 300, 350, 400, 450, 500 };
    static readonly string[] MapEffectChanceRuleIds =
    {
        RoomSettings.CrazyEnemiesRuleId,
        RoomSettings.FogOfWarRuleId,
        RoomSettings.PirateBaseRuleId,
        RoomSettings.AsteroidShowerRuleId,
        RoomSettings.CosmicWormRuleId,
        RoomSettings.MilitaryConvoyRuleId
    };
    static readonly string[] MapEffectChanceColumnLabels = { "CE", "FoW", "PB", "AS", "CW", "MC" };
    static readonly int[] MapEffectChancePercentOptions =
    {
        0, 1, 5, 10, 15, 20, 25, 30, 35, 40, 45,
        50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100
    };
    static readonly int[] ExtractionCountOptions = { 1, 2, 3, 4 };
    static readonly string[] ExtractionTypeOptions =
    {
        RoomSettings.ExtractionTypePortal,
        RoomSettings.ExtractionTypeCarrier,
        RoomSettings.ExtractionTypeSpaceCity,
        RoomSettings.ExtractionTypeAncientPortal
    };
    static readonly int[] RepairBayCountOptions = { 0, 1, 2 };
    static readonly int[] SpaceFactoryCountOptions = { 0, 1, 2 };
    static readonly int[] ScienceStationCountOptions = { 0, 1 };
    static readonly int[] RandomLootWreckCountOptions = { 0, 1, 2, 3, 4, 5 };
    static readonly int[] ViperPlotChancePercentOptions = { 0, 20, 40, 60, 80, 100 };
    static readonly int[] ArrowPlotChancePercentOptions = { 0, 20, 40, 60, 80, 100 };
    static readonly int[] BisonPlotChancePercentOptions = { 0, 20, 40, 60, 80, 100 };
    static readonly int[] InvaderPlotChancePercentOptions = { 0, 20, 40, 60, 80, 100 };
    static readonly int[] BoosterSlowdownOptions = { 30, 40, 50, 60, 70, 80, 90, 100 };
    static readonly int[] BoosterRecoveryDelayOptions = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    static readonly float[] LastShipTimerMultiplierOptions = { 1f, 1.5f, 2f, 3f, 4f, 5f };
    static readonly string[] MovingObjectsModeOptions = { RoomSettings.MovingObjectsModeOn, RoomSettings.MovingObjectsModeOff, RoomSettings.MovingObjectsModeOnlyRotate };
    static readonly int[] NeutralRiderCountOptions = { 1, 2, 3 };
    static readonly string[] NeutralRiderAggressionOptions =
    {
        RoomSettings.NeutralRiderAggressionLow,
        RoomSettings.NeutralRiderAggressionNormal,
        RoomSettings.NeutralRiderAggressionHigh
    };
    static readonly int[] EnemyCountOptions = { 1, 2, 3, 4, 5 };
    static readonly int[] SpaceMineCountOptions = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 };
    static readonly int[] EnemyHpOptions = { 20, 40, 60, 80, 100, 120, 140, 160, 180, 200 };
    static readonly int[] HeavyEnemyHpOptions = { 20, 40, 60, 80, 100, 120, 140, 160, 180, 200, 300, 400, 500 };
    static readonly int[] EnemyShieldOptions = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200 };
    static readonly int[] HeavyEnemyShieldOptions = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200, 300, 500, 750, 1000 };
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
    static readonly string[] ParallaxBackgroundOptions =
    {
        RoomSettings.ParallaxBackgroundKosmos3,
        RoomSettings.ParallaxBackgroundKosmos6,
        RoomSettings.ParallaxBackgroundKosmos8,
        RoomSettings.ParallaxBackgroundKosmos9,
        RoomSettings.ParallaxBackgroundKosmos10,
        RoomSettings.ParallaxBackgroundKosmos11,
        RoomSettings.ParallaxBackgroundKosmos12,
        RoomSettings.ParallaxBackgroundKosmos13,
        RoomSettings.ParallaxBackgroundKosmos14,
        RoomSettings.ParallaxBackgroundKosmos15,
        RoomSettings.ParallaxBackgroundKosmos16
    };
    static readonly string[] BackgroundObjectOptions =
    {
        RoomSettings.BackgroundObjectOff,
        RoomSettings.BackgroundObject1,
        RoomSettings.BackgroundObject2,
        RoomSettings.BackgroundObject3,
        RoomSettings.BackgroundObject4,
        RoomSettings.BackgroundObject5,
        RoomSettings.BackgroundObject6,
        RoomSettings.BackgroundObject7,
        RoomSettings.BackgroundObject8,
        RoomSettings.BackgroundObject9,
        RoomSettings.BackgroundObject10,
        RoomSettings.BackgroundObject11,
        RoomSettings.BackgroundObject12,
        RoomSettings.BackgroundObject13,
        RoomSettings.BackgroundObject14
    };
    static readonly int[] CollectKeepAliveRangeBonusPercentOptions = Enumerable.Range(0, 21).Select(i => i * 10).ToArray();
    static readonly int[] EnemyBalancePercentOptions = Enumerable.Range(10, 21).Select(i => i * 5).ToArray();

    public Button readyButton;
    public TMP_Text readyText;
    public TMP_Text playerStatusListText;
    public TMP_Text roundSettingText;
    public TMP_Text mapSizeSettingText;
    public TMP_Text toxicBordersSettingText;
    public TMP_Text mapBackgroundSettingText;
    public TMP_Text visualEffectsSettingText;
    public TMP_Text advancedSpawnVfxSettingText;
    public TMP_Text lowHpHullSparksSettingText;
    public TMP_Text boomVfxSettingText;
    public TMP_Text dynamicCameraZoomSettingText;
    public TMP_Text advancedBackgroundSettingText;
    public TMP_Text parallaxBackgroundSettingText;
    public TMP_Text backgroundObjectSettingText;
    public TMP_Text gravityWellPhysicsSettingText;
    public TMP_Text startingVfxSettingText;
    public TMP_Text endDisasterSettingText;
    public TMP_Text endDisasterTimeSettingText;
    public TMP_Text obstacleSettingText;
    public TMP_Text obstacleDestroySettingText;
    public TMP_Text obstacleHpValueSettingText;
    public TMP_Text obstacleSizeSettingText;
    public TMP_Text obstacleNoBordersSettingText;
    public TMP_Text treasureSettingText;
    public TMP_Text radioactiveTreasureSettingText;
    public TMP_Text alienSecretsSettingText;
    public TMP_Text resourceRichnessSettingText;
    public TMP_Text spaceJunkSettingText;
    public TMP_Text containersSettingText;
    public TMP_Text artifactAsteroidsSettingText;
    public TMP_Text randomLootWreckSettingText;
    public TMP_Text hiddenTreasureSettingText;
    public TMP_Text nebulaSettingText;
    public TMP_Text fireNebulaSettingText;
    public TMP_Text toxicNebulaSettingText;
    public TMP_Text nebulaSizeSettingText;
    public TMP_Text fireNebulaSizeSettingText;
    public TMP_Text toxicNebulaSizeSettingText;
    public TMP_Text advancedNebulaSettingText;
    public TMP_Text cloudsSettingText;
    public TMP_Text cloudsSizeSettingText;
    public TMP_Text extractionSettingText;
    public TMP_Text extractionTypeSettingText;
    public TMP_Text repairBaySettingText;
    public TMP_Text spaceFactorySettingText;
    public TMP_Text scienceStationSettingText;
    public TMP_Text avengerPlotSettingText;
    public TMP_Text viperPlotChanceSettingText;
    public TMP_Text arrowPlotChanceSettingText;
    public TMP_Text bisonPlotChanceSettingText;
    public TMP_Text invaderPlotChanceSettingText;
    public TMP_Text boosterSettingText;
    public TMP_Text ammoSettingText;
    public TMP_Text boosterDelaySettingText;
    public TMP_Text advancedBoosterSettingText;
    public TMP_Text shipDriftSettingText;
    public TMP_Text deathTimerSettingText;
    public TMP_Text inventoryLossSettingText;
    public TMP_Text equipmentLossSettingText;
    public TMP_Text cosmicWormSettingText;
    public TMP_Text crazyEnemiesEffectSettingText;
    public TMP_Text fogOfWarEffectSettingText;
    public TMP_Text pirateBaseEffectSettingText;
    public TMP_Text asteroidShowerEffectSettingText;
    public TMP_Text movingObjectsSettingText;
    public TMP_Text enemyBotsSettingText;
    public TMP_Text corsairSettingText;
    public TMP_Text corsairTimeSettingText;
    public TMP_Text corsairHpSettingText;
    public TMP_Text bulletPushSettingText;
    public TMP_Text obstacleWeightSettingText;
    public TMP_Text treasureWeightSettingText;
    public TMP_Text batteringSettingText;
    public TMP_Text enemyDamageMultiplierSettingText;
    public TMP_Text enemyAttackWindupMultiplierSettingText;
    public TMP_Text enemyAttackCooldownMultiplierSettingText;
    public TMP_Text shootingModelSettingText;
    public TMP_Text superAttackSettingText;
    public TMP_Text advancedMovingJoystickSettingText;
    public TMP_Text advancedShootingJoystickSettingText;
    public TMP_Text dynamicUseSettingText;
    public TMP_Text collectKeepAliveRangeBonusSettingText;
    public TMP_Text hapticsSettingText;
    public TMP_Text fpsCounterSettingText;
    public TMP_Text diagnosticsGcSettingText;
    public TMP_Text diagnosticsSceneCountsSettingText;
    public TMP_Text diagnosticsNetworkSettingText;
    public TMP_Text neutralRidersEnabledSettingText;
    public TMP_Text neutralRidersCountSettingText;
    public TMP_Text neutralRidersAggressionSettingText;
    public TMP_Text gunSetupSettingText;
    public Button roundSettingButton;
    public Button mapSizeSettingButton;
    public Button toxicBordersSettingButton;
    public Button mapBackgroundSettingButton;
    public Button visualEffectsSettingButton;
    public Button advancedSpawnVfxSettingButton;
    public Button lowHpHullSparksSettingButton;
    public Button boomVfxSettingButton;
    public Button dynamicCameraZoomSettingButton;
    public Button advancedBackgroundSettingButton;
    public Button parallaxBackgroundSettingButton;
    public Button backgroundObjectSettingButton;
    public Button gravityWellPhysicsSettingButton;
    public Button startingVfxSettingButton;
    public Button endDisasterSettingButton;
    public Button endDisasterTimeSettingButton;
    public Button obstacleSettingButton;
    public Button obstacleDestroySettingButton;
    public Button obstacleHpValueSettingButton;
    public Button obstacleSizeSettingButton;
    public Button obstacleNoBordersSettingButton;
    public Button treasureSettingButton;
    public Button radioactiveTreasureSettingButton;
    public Button alienSecretsSettingButton;
    public Button resourceRichnessSettingButton;
    public Button spaceJunkSettingButton;
    public Button containersSettingButton;
    public Button artifactAsteroidsSettingButton;
    public Button randomLootWreckSettingButton;
    public Button hiddenTreasureSettingButton;
    public Button nebulaSettingButton;
    public Button fireNebulaSettingButton;
    public Button toxicNebulaSettingButton;
    public Button nebulaSizeSettingButton;
    public Button fireNebulaSizeSettingButton;
    public Button toxicNebulaSizeSettingButton;
    public Button advancedNebulaSettingButton;
    public Button cloudsSettingButton;
    public Button cloudsSizeSettingButton;
    public Button extractionSettingButton;
    public Button extractionTypeSettingButton;
    public Button repairBaySettingButton;
    public Button spaceFactorySettingButton;
    public Button scienceStationSettingButton;
    public Button avengerPlotSettingButton;
    public Button viperPlotChanceSettingButton;
    public Button arrowPlotChanceSettingButton;
    public Button bisonPlotChanceSettingButton;
    public Button invaderPlotChanceSettingButton;
    public Button boosterSettingButton;
    public Button ammoSettingButton;
    public Button boosterDelaySettingButton;
    public Button advancedBoosterSettingButton;
    public Button shipDriftSettingButton;
    public Button deathTimerSettingButton;
    public Button inventoryLossSettingButton;
    public Button equipmentLossSettingButton;
    public Button cosmicWormSettingButton;
    public Button crazyEnemiesEffectSettingButton;
    public Button fogOfWarEffectSettingButton;
    public Button pirateBaseEffectSettingButton;
    public Button asteroidShowerEffectSettingButton;
    public Button movingObjectsSettingButton;
    public Button bulletPushSettingButton;
    public Button obstacleWeightSettingButton;
    public Button treasureWeightSettingButton;
    public Button batteringSettingButton;
    public Button enemyDamageMultiplierSettingButton;
    public Button enemyAttackWindupMultiplierSettingButton;
    public Button enemyAttackCooldownMultiplierSettingButton;
    public Button shootingModelSettingButton;
    public Button superAttackSettingButton;
    public Button advancedMovingJoystickSettingButton;
    public Button advancedShootingJoystickSettingButton;
    public Button dynamicUseSettingButton;
    public Button collectKeepAliveRangeBonusSettingButton;
    public Button hapticsSettingButton;
    public Button fpsCounterSettingButton;
    public Button diagnosticsGcSettingButton;
    public Button diagnosticsSceneCountsSettingButton;
    public Button diagnosticsNetworkSettingButton;
    public Button neutralRidersEnabledSettingButton;
    public Button neutralRidersCountSettingButton;
    public Button neutralRidersAggressionSettingButton;
    public Button gunSetupSettingButton;
    public Button backToRoundsButton;
    public TMP_Text backToRoundsText;
    public Button mapSelectionButton;
    public TMP_Text mapSelectionText;

    bool isReady = false;
    bool hasRecordedCurrentRound = false;
    readonly Dictionary<string, Button> enemySettingButtons = new Dictionary<string, Button>();
    readonly Dictionary<string, TMP_Text> enemySettingTexts = new Dictionary<string, TMP_Text>();
    readonly Dictionary<string, Button> mapEffectChanceButtons = new Dictionary<string, Button>();
    readonly Dictionary<string, TMP_Text> mapEffectChanceTexts = new Dictionary<string, TMP_Text>();
    readonly Dictionary<string, GameObject> gameplayHudObjectsByName = new Dictionary<string, GameObject>();
    readonly Dictionary<string, RectTransform> leftSectionContainers = new Dictionary<string, RectTransform>();
    readonly Dictionary<string, TMP_Text> enemyRowLabels = new Dictionary<string, TMP_Text>();
    readonly Dictionary<string, TMP_Text> enemyHeaderLabels = new Dictionary<string, TMP_Text>();
    readonly Dictionary<string, TMP_Text> mapEffectChanceRowLabels = new Dictionary<string, TMP_Text>();
    readonly Dictionary<string, TMP_Text> mapEffectChanceHeaderLabels = new Dictionary<string, TMP_Text>();
    readonly Dictionary<int, Sprite> mapBackgroundPreviewCache = new Dictionary<int, Sprite>();
    readonly Dictionary<string, Sprite> mapSpecificPreviewCache = new Dictionary<string, Sprite>();
    readonly List<Button> fullscreenMapTileButtons = new List<Button>();
    ScrollRect leftSettingsScrollRect;
    RectTransform leftSettingsViewportRect;
    RectTransform leftSettingsContentRect;
    ScrollRect enemyTableScrollRect;
    RectTransform enemyTableViewportRect;
    RectTransform enemyTableRootRect;
    ScrollRect mapEffectChanceTableScrollRect;
    RectTransform mapEffectChanceTableViewportRect;
    RectTransform mapEffectChanceTableRootRect;
    RectTransform weaponSettingsRootRect;
    GameObject mapSelectionOverlayObject;
    TMP_Text mapSelectionOverlayTitleText;
    Button mapSelectionOverlayCloseButton;
    ScrollRect mapSelectionScrollRect;
    RectTransform mapSelectionContentRect;
    readonly List<Button> mapSelectionTileButtons = new List<Button>();
    bool leftSettingsScrollInitialized;
    bool enemyTableScrollInitialized;
    bool mapEffectChanceTableScrollInitialized;
    LobbyScreen currentScreen = LobbyScreen.MapSelection;
    LobbyScreen previousMapScreenBeforeDeveloperSettings = LobbyScreen.MapSelection;
    string selectedMapId;
    GameObject lobbyTopBarRootObject;
    SharedPlayerTopBarUI lobbyTopBar;
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
    Coroutine launchStartRecoveryRoutine;
    Button developerBackButton;
    TMP_Text developerBackText;
    Button developerGunSetupButton;
    TMP_Text developerGunSetupText;
    Button developerCheatButton;
    TMP_Text developerCheatText;
    GameObject developerCheatOverlayObject;
    TMP_Text developerCheatAstronsText;
    TMP_Text developerCheatXpText;
    TMP_Text developerCheatStatusText;
    Button developerCheatAddMoneyButton;
    Button developerCheatAddXpButton;
    Button developerCheatUnlockBlueprintsButton;
    Button developerCheatLockBlueprintsButton;
    Button developerCheatUnlockShipsButton;
    Button developerCheatLockShipsButton;
    Button developerCheatUnlockMapsButton;
    Button developerCheatLockMapsButton;
    Button developerCheatUnlockProjectsButton;
    Button developerCheatLockProjectsButton;
    Button developerCheatResetAccountButton;
    Button developerCheatCloseButton;
    GameObject developerCheatResetConfirmObject;
    Button developerCheatResetConfirmYesButton;
    Button developerCheatResetConfirmCancelButton;
    GameObject mapSelectionRootObject;
    ScrollRect fullScreenMapSelectionScrollRect;
    RectTransform fullScreenMapSelectionViewportRect;
    RectTransform mapSelectionTilesRootRect;
    Scrollbar fullScreenMapSelectionScrollbar;
    TMP_Text mapSelectionScreenTitleText;
    GameObject mapDetailsRootObject;
    Image mapDetailsPreviewImage;
    TMP_Text mapDetailsNameText;
    TMP_Text mapDetailsDescriptionText;
    TMP_Text mapDetailsLandingSitesText;
    GameObject mapDetailsLossBadgeObject;
    Image mapDetailsLossBadgeImage;
    Image mapDetailsLossSkullImage;
    TMP_Text mapDetailsLossSkullText;
    TMP_Text mapDetailsLossLabelText;
    Sprite mapDeathSkullBadgeSprite;
    Material grayscaleUiMaterial;
    GameObject developerSettingsRootObject;
    bool fullScreenMapSelectionScrollInitialized;
    bool legacyLobbyUiActive = true;
    bool gameplayHudVisibilityInitialized;
    bool lastGameplayHudVisible;

    CanvasGroup EnsureCanvasGroup()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = gameObject.AddComponent<CanvasGroup>();
        }

        return cg;
    }

}
