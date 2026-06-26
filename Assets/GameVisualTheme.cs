using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class GameVisualTheme : MonoBehaviour
{
    struct RuntimeThemeTarget
    {
        public readonly GameObject Target;
        public readonly EntityId Id;

        public RuntimeThemeTarget(GameObject target, EntityId id)
        {
            Target = target;
            Id = id;
        }
    }

    public const string WorldSortingLayerName = "Ground";
    public const int BackgroundSortingOrder = 0;
    public const int EndDisasterSortingOrder = 6;
    public const int ExtractionZoneSortingOrder = 8;
    public const int RepairBaySortingOrder = 9;
    public const int ObstacleSortingOrder = 20;
    public const int TreasureSortingOrder = 22;
    public const int WreckSortingOrder = 24;
    public const int EnemySortingOrder = 30;
    public const int PlayerSortingOrder = 32;

    public const float PlayerTargetSize = 1.04f;
    const float CargoTruckPlayerSizeMultiplier = 1.5f;
    const float AstronautTargetSize = 0.56f;
    const float TreasureTargetSize = 1.5f;
    const float ContainerTargetSize = 1.05f;
    const float RandomLootWreckTargetSize = 1.24f;
    const float SpaceAnimalRemainsTargetSize = 1.1f;
    const float SpaceAnimalBonesTargetSize = 1.08f;
    const float AlienSecretTargetSize = 1.18f;
    const float TreasureColliderSizeMultiplier = 0.9f;
    const float PlayerBodyColliderWidthFactor = 0.46f;
    const float PlayerBodyColliderHeightFactor = 0.62f;
    const float PlayerPickupRadiusFactor = 0.8f;
    const float ObstacleTargetSize = 3.0f;
    const float ExtractionTargetSize = 4.3f;
    const float CarrierExtractionTargetSize = 5.4f;
    const float SpaceCityExtractionTargetSize = 5.4f;
    const float AncientPortalExtractionTargetSize = 5.6f;
    const float BackgroundTileWorldSize = 8f;
    const float DesktopRefreshInterval = 0.75f;
    const float RuntimeRefreshDelay = 0.08f;
    const int RuntimeObjectRefreshBudgetPerFrame = 16;

    static GameVisualTheme instance;
    static readonly ProfilerMarker ApplyFullThemeMarker = new ProfilerMarker("GameVisualTheme.ApplyFull");
    static readonly ProfilerMarker ApplyObjectQueueMarker = new ProfilerMarker("GameVisualTheme.ApplyObjectQueue");

    Sprite[] shipSprites;
    Sprite[] wreckSprites;
    Sprite enemyBotSprite;
    Sprite corsairSprite;
    Sprite astronautSprite;
    Sprite treasureSprite;
    Sprite goldTreasureSprite;
    Sprite rareTreasureSprite;
    Sprite richTreasureSprite;
    Sprite epicTreasureSprite;
    Sprite legendaryTreasureSprite;
    Sprite hiddenDimensionTreasureSprite;
    Sprite hiddenDimensionGoldTreasureSprite;
    Sprite hiddenDimensionRareTreasureSprite;
    Sprite hiddenDimensionRichTreasureSprite;
    Sprite hiddenDimensionEpicTreasureSprite;
    Sprite hiddenDimensionLegendaryTreasureSprite;
    Sprite platinumChunkSprite;
    Sprite spaceJunkTrashSprite;
    Sprite spaceJunkStandardSprite;
    Sprite spaceJunkAsteroidSprite;
    Sprite spaceAnimalRemainsSprite;
    Sprite spaceAnimalBonesSprite;
    Sprite radioactiveTreasureSprite;
    Sprite[] containerSprites;
    Sprite[] blueprintScrapContainerSprites;
    Sprite[] randomLootWreckSprites;
    Sprite[] alienSecretSprites;
    Sprite[] obstacleSprites;
    Sprite[] hiddenDimensionObstacleSprites;
    Sprite portalExtractionSprite;
    Sprite carrierExtractionSprite;
    Sprite spaceCityExtractionSprite;
    Sprite ancientPortalExtractionSprite;
    Sprite backgroundSprite;
    float nextRefreshTime;
    float nextObjectRefreshTime;
    bool runtimeThemeDirty;
    bool runtimeReloadAssetsDirty;
    readonly Queue<RuntimeThemeTarget> runtimeDirtyObjects = new Queue<RuntimeThemeTarget>();
    readonly HashSet<EntityId> runtimeDirtyObjectIds = new HashSet<EntityId>();
    int lastRuntimeBackgroundIndex = int.MinValue;
    int lastRuntimeObstacleSizePercent = int.MinValue;
    bool lastRuntimeToxicBordersEnabled;
    string lastRuntimeMapSizeMode = string.Empty;
    string lastRuntimeSelectedMapId = string.Empty;
    string lastRuntimeExtractionType = string.Empty;
    string lastRuntimeParallaxBackgroundId = string.Empty;
    string lastRuntimeBackgroundObjectId = string.Empty;
#if UNITY_EDITOR
    double nextEditorRefreshTime;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    static void BootstrapInEditor()
    {
        EditorApplication.delayCall += EnsureEditorInstance;
    }
#endif

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = GameObject.Find("GameVisualTheme");
        if (root == null)
        {
            root = new GameObject("GameVisualTheme");
#if UNITY_EDITOR
            if (!Application.isPlaying)
                root.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
#endif
        }

        instance = root.GetComponent<GameVisualTheme>();
        if (instance == null)
            instance = root.AddComponent<GameVisualTheme>();

        if (Application.isPlaying)
            DontDestroyOnLoad(root);
    }

    public static int GetObstacleSpriteVariantCount()
    {
        EnsureInstance();
        if (instance == null)
            return 0;

        instance.LoadAssets();
        return instance.obstacleSprites != null ? instance.obstacleSprites.Length : 0;
    }

    public static void ApplyObstacleVisual(GameObject target)
    {
        EnsureInstance();
        if (instance == null || target == null)
            return;

        instance.LoadAssets();
        instance.ApplyObstacleVisualInternal(target);
    }

    public static void ApplyTreasureVisual(Treasure target)
    {
        EnsureInstance();
        if (instance == null || target == null)
            return;

        if (instance.treasureSprite == null)
            instance.LoadAssets();

        instance.ApplyTreasureVisualInternal(target);
    }

    public static void ApplyPlayerVisual(PlayerHealth target)
    {
        EnsureInstance();
        if (instance == null || target == null)
            return;

        if (instance.shipSprites == null || instance.shipSprites.Length == 0)
            instance.LoadAssets();

        instance.ApplyPlayerVisualInternal(target);
    }

    public static void RequestRuntimeRefresh(bool reloadAssets = false)
    {
        EnsureInstance();
        if (instance == null)
            return;

        instance.MarkRuntimeThemeDirty(reloadAssets);
    }

    public static void RequestRuntimeRefresh(GameObject target, bool reloadAssets = false)
    {
        EnsureInstance();
        if (instance == null)
            return;

        if (target == null || reloadAssets)
        {
            instance.MarkRuntimeThemeDirty(reloadAssets);
            return;
        }

        instance.QueueRuntimeThemeObject(target, RuntimeRefreshDelay);
    }

    public static void RequestRuntimeRefresh(Component target, bool reloadAssets = false)
    {
        RequestRuntimeRefresh(target != null ? target.gameObject : null, reloadAssets);
    }

#if UNITY_EDITOR
    static void EnsureEditorInstance()
    {
        if (Application.isPlaying)
            return;

        EnsureInstance();
        if (instance != null)
            instance.ApplyThemeInEditor();
    }
#endif

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        LoadAssets();
    }

    void OnEnable()
    {
        LoadAssets();
        nextRefreshTime = 0f;
        ApplyTheme();
        CaptureRuntimeSettingsSnapshot();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorApplication.hierarchyChanged += OnEditorHierarchyChanged;
#endif
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
#endif
        if (instance == this)
            instance = null;
    }

    void Start()
    {
        ApplyTheme();
        CaptureRuntimeSettingsSnapshot();
        MarkRuntimeThemeDirty(false);
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (EditorApplication.timeSinceStartup < nextEditorRefreshTime)
                return;

            nextEditorRefreshTime = EditorApplication.timeSinceStartup + DesktopRefreshInterval;
            ApplyThemeInEditor();
            return;
        }
#endif

        RefreshRuntimeSettingsIfNeeded();
        if (runtimeThemeDirty)
        {
            if (Time.unscaledTime < nextRefreshTime)
                return;

            runtimeThemeDirty = false;
            ClearRuntimeThemeObjectQueue();
            if (runtimeReloadAssetsDirty)
            {
                runtimeReloadAssetsDirty = false;
                LoadAssets();
            }

            using (ApplyFullThemeMarker.Auto())
            {
                ApplyTheme();
            }

            nextRefreshTime = 0f;
            return;
        }

        ProcessRuntimeThemeObjectQueue();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearRuntimeThemeObjectQueue();
        LoadAssets();
        nextRefreshTime = 0f;
        ApplyTheme();
        CaptureRuntimeSettingsSnapshot();
        MarkRuntimeThemeDirty(false);
    }

    void MarkRuntimeThemeDirty(bool reloadAssets, float delay = RuntimeRefreshDelay)
    {
        runtimeThemeDirty = true;
        runtimeReloadAssetsDirty |= reloadAssets;
        ClearRuntimeThemeObjectQueue();

        float requestedRefreshTime = Time.unscaledTime + Mathf.Max(0f, delay);
        if (nextRefreshTime <= 0f || requestedRefreshTime < nextRefreshTime)
            nextRefreshTime = requestedRefreshTime;
    }

    void QueueRuntimeThemeObject(GameObject target, float delay)
    {
        if (target == null)
            return;

        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            ApplyThemeInEditor();
#else
            LoadAssets();
            ApplyTheme();
#endif
            return;
        }

        EntityId id = target.GetEntityId();
        if (runtimeDirtyObjectIds.Add(id))
            runtimeDirtyObjects.Enqueue(new RuntimeThemeTarget(target, id));

        float requestedRefreshTime = Time.unscaledTime + Mathf.Max(0f, delay);
        if (nextObjectRefreshTime <= 0f || requestedRefreshTime < nextObjectRefreshTime)
            nextObjectRefreshTime = requestedRefreshTime;
    }

    void ClearRuntimeThemeObjectQueue()
    {
        runtimeDirtyObjects.Clear();
        runtimeDirtyObjectIds.Clear();
        nextObjectRefreshTime = 0f;
    }

    void ProcessRuntimeThemeObjectQueue()
    {
        if (runtimeDirtyObjects.Count == 0 || Time.unscaledTime < nextObjectRefreshTime)
            return;

        if (runtimeReloadAssetsDirty)
        {
            runtimeReloadAssetsDirty = false;
            LoadAssets();
        }

        EnsureRuntimeAssetsLoaded();

        using (ApplyObjectQueueMarker.Auto())
        {
            int budget = RuntimeObjectRefreshBudgetPerFrame;
            while (budget > 0 && runtimeDirtyObjects.Count > 0)
            {
                RuntimeThemeTarget queuedTarget = runtimeDirtyObjects.Dequeue();
                runtimeDirtyObjectIds.Remove(queuedTarget.Id);

                GameObject target = queuedTarget.Target;
                if (target == null || !target.activeInHierarchy)
                    continue;

                if (!ApplyThemeToObject(target))
                {
                    MarkRuntimeThemeDirty(false, 0f);
                    return;
                }

                budget--;
            }
        }

        nextObjectRefreshTime = runtimeDirtyObjects.Count > 0 ? Time.unscaledTime : 0f;
    }

    void EnsureRuntimeAssetsLoaded()
    {
        if (shipSprites == null ||
            shipSprites.Length == 0 ||
            treasureSprite == null ||
            obstacleSprites == null ||
            obstacleSprites.Length == 0 ||
            portalExtractionSprite == null)
        {
            LoadAssets();
        }
    }

    bool ApplyThemeToObject(GameObject target)
    {
        if (target == null)
            return true;

        PlayerHealth player = target.GetComponent<PlayerHealth>();
        if (player != null)
        {
            ApplyPlayerVisualInternal(player);
            return true;
        }

        Treasure treasure = target.GetComponent<Treasure>();
        if (treasure != null)
        {
            ApplyTreasureVisualInternal(treasure);
            return true;
        }

        ExtractionZone extractionZone = target.GetComponent<ExtractionZone>();
        if (extractionZone != null)
        {
            ApplyExtractionZoneVisualInternal(extractionZone);
            return true;
        }

        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer != null && IsObstacleVisualTarget(target))
        {
            ApplyObstacleVisualInternal(target, renderer);
            return true;
        }

        if (target.GetComponent<DroppedCargoCrate>() != null)
            return true;

        return false;
    }

    void CaptureRuntimeSettingsSnapshot()
    {
        lastRuntimeBackgroundIndex = RoomSettings.GetMapBackgroundIndex();
        lastRuntimeMapSizeMode = RoomSettings.GetMapSizeMode();
        lastRuntimeObstacleSizePercent = RoomSettings.GetObstacleSizePercent();
        lastRuntimeToxicBordersEnabled = RoomSettings.AreToxicBordersEnabled();
        lastRuntimeSelectedMapId = RoomSettings.GetSelectedLobbyMapId();
        lastRuntimeExtractionType = RoomSettings.GetExtractionType();
        lastRuntimeParallaxBackgroundId = RoomSettings.GetParallaxBackgroundId();
        lastRuntimeBackgroundObjectId = RoomSettings.GetBackgroundObjectId();
    }

    void RefreshRuntimeSettingsIfNeeded()
    {
        int backgroundIndex = RoomSettings.GetMapBackgroundIndex();
        string mapSizeMode = RoomSettings.GetMapSizeMode();
        int obstacleSizePercent = RoomSettings.GetObstacleSizePercent();
        bool toxicBordersEnabled = RoomSettings.AreToxicBordersEnabled();
        string selectedMapId = RoomSettings.GetSelectedLobbyMapId();
        string extractionType = RoomSettings.GetExtractionType();
        string parallaxBackgroundId = RoomSettings.GetParallaxBackgroundId();
        string backgroundObjectId = RoomSettings.GetBackgroundObjectId();
        bool backgroundChanged = backgroundIndex != lastRuntimeBackgroundIndex;
        bool selectedMapChanged = !string.Equals(selectedMapId, lastRuntimeSelectedMapId, System.StringComparison.Ordinal);
        bool extractionTypeChanged = !string.Equals(extractionType, lastRuntimeExtractionType, System.StringComparison.Ordinal);
        bool parallaxBackgroundChanged = !string.Equals(parallaxBackgroundId, lastRuntimeParallaxBackgroundId, System.StringComparison.Ordinal);
        bool backgroundObjectChanged = !string.Equals(backgroundObjectId, lastRuntimeBackgroundObjectId, System.StringComparison.Ordinal);
        if (!backgroundChanged &&
            !selectedMapChanged &&
            !extractionTypeChanged &&
            !parallaxBackgroundChanged &&
            !backgroundObjectChanged &&
            string.Equals(mapSizeMode, lastRuntimeMapSizeMode, System.StringComparison.Ordinal) &&
            toxicBordersEnabled == lastRuntimeToxicBordersEnabled &&
            obstacleSizePercent == lastRuntimeObstacleSizePercent)
        {
            return;
        }

        lastRuntimeBackgroundIndex = backgroundIndex;
        lastRuntimeMapSizeMode = mapSizeMode;
        lastRuntimeObstacleSizePercent = obstacleSizePercent;
        lastRuntimeToxicBordersEnabled = toxicBordersEnabled;
        lastRuntimeSelectedMapId = selectedMapId;
        lastRuntimeExtractionType = extractionType;
        lastRuntimeParallaxBackgroundId = parallaxBackgroundId;
        lastRuntimeBackgroundObjectId = backgroundObjectId;
        MarkRuntimeThemeDirty(backgroundChanged || selectedMapChanged, 0f);
    }

#if UNITY_EDITOR
    void OnEditorHierarchyChanged()
    {
        if (Application.isPlaying)
            return;

        ApplyThemeInEditor();
    }

    void ApplyThemeInEditor()
    {
        LoadAssets();
        ApplyTheme();
    }
#endif

    void LoadAssets()
    {
        bool snowFieldTheme = IsSnowFieldTheme();
        bool toxicAreaTheme = IsToxicAreaTheme();
        shipSprites = new Sprite[ShipCatalog.MaxShipSkinIndex + 1];
        wreckSprites = new Sprite[ShipCatalog.MaxShipSkinIndex + 1];
        for (int skinIndex = 0; skinIndex <= ShipCatalog.MaxShipSkinIndex; skinIndex++)
        {
            shipSprites[skinIndex] = LoadSpriteFromResourcesOrEditor(
                ShipCatalog.GetShipSkinResourcePath(skinIndex),
                ShipCatalog.GetShipSkinEditorResourcePath(skinIndex),
                ShipCatalog.GetShipSkinEditorFallbackPath(skinIndex));

            wreckSprites[skinIndex] = LoadSpriteFromResourcesOrEditor(
                ShipCatalog.GetWreckResourcePathForSkin(skinIndex),
                ShipCatalog.GetWreckEditorResourcePathForSkin(skinIndex),
                ShipCatalog.GetWreckEditorFallbackPathForSkin(skinIndex));
        }
        enemyBotSprite = LoadSpriteFromResourcesOrEditor("droid1_resource", "Assets/Resources/droid1_resource.png", "Assets/droid1.png");
        corsairSprite = LoadSpriteFromResourcesOrEditor("statek_duzy_resource", "Assets/Resources/statek_duzy_resource.png", "Assets/statek_duzy.png");
        astronautSprite = LoadSpriteFromResourcesOrEditor("kosmonauta_resource", "Assets/Resources/kosmonauta_resource.png", "Assets/kosmonauta.png");

        if (snowFieldTheme)
        {
            treasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_white_common_snow", "Assets/Resources/treasure_asteroid_white_common_snow.png", "Assets/treasure_asteroid_white_common.png");
            goldTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_green_uncommon_snow", "Assets/Resources/treasure_asteroid_green_uncommon_snow.png", "Assets/treasure_asteroid_green_uncommon.png");
            rareTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_blue_rare_snow", "Assets/Resources/treasure_asteroid_blue_rare_snow.png", "Assets/treasure_asteroid_blue_rare.png");
            richTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_violet_veryrare_snow", "Assets/Resources/treasure_asteroid_violet_veryrare_snow.png", "Assets/treasure_asteroid_violet_very_rare.png");
            epicTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_burgundy_epic_snow", "Assets/Resources/treasure_asteroid_burgundy_epic_snow.png", "Assets/treasure_asteroid_burgundy_epic.png");
            legendaryTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_gold_legendary_snow", "Assets/Resources/treasure_asteroid_gold_legendary_snow.png", "Assets/treasure_asteroid_gold_legendary.png");
        }
        else
        {
            treasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_white_common_resource", "Assets/Resources/treasure_asteroid_white_common_resource.png", "Assets/treasure_asteroid_white_common.png");
            goldTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_green_uncommon_resource", "Assets/Resources/treasure_asteroid_green_uncommon_resource.png", "Assets/treasure_asteroid_green_uncommon.png");
            rareTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_blue_rare_resource", "Assets/Resources/treasure_asteroid_blue_rare_resource.png", "Assets/treasure_asteroid_blue_rare.png");
            richTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_violet_very_rare_resource", "Assets/Resources/treasure_asteroid_violet_very_rare_resource.png", "Assets/treasure_asteroid_violet_very_rare.png");
            epicTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_burgundy_epic_resource", "Assets/Resources/treasure_asteroid_burgundy_epic_resource.png", "Assets/treasure_asteroid_burgundy_epic.png");
            legendaryTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_gold_legendary_resource", "Assets/Resources/treasure_asteroid_gold_legendary_resource.png", "Assets/treasure_asteroid_gold_legendary.png");
        }
        hiddenDimensionTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_white_common_resource", "Assets/Resources/treasure_asteroid_white_common_resource.png", "Assets/treasure_asteroid_white_common.png");
        hiddenDimensionGoldTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_green_uncommon_resource", "Assets/Resources/treasure_asteroid_green_uncommon_resource.png", "Assets/treasure_asteroid_green_uncommon.png");
        hiddenDimensionRareTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_blue_rare_resource", "Assets/Resources/treasure_asteroid_blue_rare_resource.png", "Assets/treasure_asteroid_blue_rare.png");
        hiddenDimensionRichTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_violet_very_rare_resource", "Assets/Resources/treasure_asteroid_violet_very_rare_resource.png", "Assets/treasure_asteroid_violet_very_rare.png");
        hiddenDimensionEpicTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_burgundy_epic_resource", "Assets/Resources/treasure_asteroid_burgundy_epic_resource.png", "Assets/treasure_asteroid_burgundy_epic.png");
        hiddenDimensionLegendaryTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_gold_legendary_resource", "Assets/Resources/treasure_asteroid_gold_legendary_resource.png", "Assets/treasure_asteroid_gold_legendary.png");
        platinumChunkSprite = LoadSpriteFromResourcesOrEditor("Items/platinum_chunk", "Assets/Resources/Items/platinum_chunk.png");
        spaceJunkTrashSprite = LoadSpriteFromResourcesOrEditor("space_junk_trash", "Assets/Resources/space_junk_trash.png", "Assets/space_junk_trash.png");
        spaceJunkStandardSprite = LoadSpriteFromResourcesOrEditor("space_junk_standard", "Assets/Resources/space_junk_standard.png", "Assets/space_junk_standard.png");
        spaceJunkAsteroidSprite = LoadSpriteFromResourcesOrEditor("space_junk_asteroid", "Assets/Resources/space_junk_asteroid.png", "Assets/space_junk_asteroid.png");
        spaceAnimalRemainsSprite = LoadSpriteFromResourcesOrEditor("space_animal_remains_resource", "Assets/Resources/space_animal_remains_resource.png", "Assets/Resources/space_animal_remains_resource.png");
        spaceAnimalBonesSprite = LoadSpriteFromResourcesOrEditor("Items/animal_bones", "Assets/Resources/Items/animal_bones.png");
        radioactiveTreasureSprite = LoadSpriteFromResourcesOrEditor("radioactive_treasure", "Assets/Resources/radioactive_treasure.png", "Assets/Resources/radioactive_treasure.png");
        containerSprites = LoadSpritesFromResourcesOrEditor("kontenery_9", "Assets/Resources/kontenery_9.png", "Assets/kontenery_9.png");
        blueprintScrapContainerSprites = LoadSpritesFromResourcesOrEditor("Enemies/ContainerShip/containers_set2", "Assets/Resources/Enemies/ContainerShip/containers_set2.png", "Assets/containers_set2.png");
        randomLootWreckSprites = LoadRandomLootWreckSprites();
        alienSecretSprites = LoadAlienSecretSprites();
        obstacleSprites = snowFieldTheme
            ? LoadSnowObstacleSprites()
            : toxicAreaTheme
                ? LoadRadioactiveObstacleSprites()
                : LoadDefaultObstacleSprites();
        hiddenDimensionObstacleSprites = LoadDefaultObstacleSprites();
        portalExtractionSprite = LoadSpriteFromResourcesOrEditor("Visuals/Bases/portal_nieaktywny_resource", "Assets/Resources/Visuals/Bases/portal_nieaktywny_resource.png", "Assets/portal_nieaktywny.png");
        carrierExtractionSprite = LoadSpriteFromResourcesOrEditor("Visuals/Bases/lotniskowiec_strefa_resource", "Assets/Resources/Visuals/Bases/lotniskowiec_strefa_resource.png", "Assets/lotniskowiec_strefa.png");
        spaceCityExtractionSprite = LoadSpriteFromResourcesOrEditor("Visuals/Bases/baza_strefa_resource", "Assets/Resources/Visuals/Bases/baza_strefa_resource.png", "Assets/baza_strefa.png");
        ancientPortalExtractionSprite = LoadSpriteFromResourcesOrEditor("Visuals/Bases/ancient_portal_resource", "Assets/Resources/Visuals/Bases/ancient_portal_resource.png");
        backgroundSprite = LoadBackgroundSprite(RoomSettings.GetMapBackgroundIndex());
    }

    bool IsSnowFieldTheme()
    {
        return string.Equals(RoomSettings.GetSelectedLobbyMapId(), LobbyMapCatalog.SnowFieldMapId, System.StringComparison.Ordinal);
    }

    bool IsToxicAreaTheme()
    {
        return string.Equals(RoomSettings.GetSelectedLobbyMapId(), LobbyMapCatalog.ToxicAreaMapId, System.StringComparison.Ordinal);
    }

    Sprite[] LoadDefaultObstacleSprites()
    {
        return new[]
        {
            LoadObstacleSprite("asteroida_1_clean_resource", "Assets/Resources/asteroida_1_clean_resource.png", "Assets/asteroida_1_clean.png"),
            LoadObstacleSprite("asteroida_2_clean_resource", "Assets/Resources/asteroida_2_clean_resource.png", "Assets/asteroida_2_clean.png"),
            LoadObstacleSprite("asteroida_3_clean_resource", "Assets/Resources/asteroida_3_clean_resource.png", "Assets/asteroida_3_clean.png"),
            LoadObstacleSprite("asteroida_podluzna_1_clean_resource", "Assets/Resources/asteroida_podluzna_1_clean_resource.png", "Assets/asteroida_podluzna_1_clean.png"),
            LoadObstacleSprite("asteroida_podluzna_2_clean_resource", "Assets/Resources/asteroida_podluzna_2_clean_resource.png", "Assets/asteroida_podluzna_2_clean.png"),
            LoadObstacleSprite("asteroida_nadkruszona_resource", "Assets/Resources/asteroida_nadkruszona_resource.png", "Assets/asteroida_nadkruszona.png"),
            LoadObstacleSprite("asteroida_ziemniak_resource", "Assets/Resources/asteroida_ziemniak_resource.png", "Assets/asteroida_ziemniak.png")
        };
    }

    Sprite[] LoadSnowObstacleSprites()
    {
        return new[]
        {
            LoadObstacleSprite("obstacle_ice_2", "Assets/Resources/obstacle_ice_2.png", "Assets/asteroida_1_clean.png"),
            LoadObstacleSprite("obstacle_ice_3", "Assets/Resources/obstacle_ice_3.png", "Assets/asteroida_2_clean.png"),
            LoadObstacleSprite("obstacle_ice_4", "Assets/Resources/obstacle_ice_4.png", "Assets/asteroida_3_clean.png"),
            LoadObstacleSprite("obstacle_ice_5", "Assets/Resources/obstacle_ice_5.png", "Assets/asteroida_podluzna_1_clean.png"),
            LoadObstacleSprite("obstacle_ice_6", "Assets/Resources/obstacle_ice_6.png", "Assets/asteroida_podluzna_2_clean.png"),
            LoadObstacleSprite("obstacle_ice_7", "Assets/Resources/obstacle_ice_7.png", "Assets/asteroida_nadkruszona.png"),
            LoadObstacleSprite("obstacle_ice_8", "Assets/Resources/obstacle_ice_8.png", "Assets/asteroida_ziemniak.png")
        };
    }

    Sprite[] LoadRadioactiveObstacleSprites()
    {
        Sprite[] sprites = LoadSpritesFromResourcesOrEditor(
            "Visuals/Obstacles/obstacles_radioactive",
            "Assets/Resources/Visuals/Obstacles/obstacles_radioactive.png",
            "Assets/obstacles_radioactive.png");
        return sprites != null && sprites.Length > 0 ? sprites : LoadDefaultObstacleSprites();
    }

    void ApplyTheme()
    {
        ApplyGroundBackground();
        AdvancedSpaceBackground.RefreshForCurrentSettings();
        ApplyMapBounds();
        ApplyPlayerSprites();
        ApplyTreasureSprites();
        ApplyObstacleSprites();
        ApplyExtractionZoneSprites();
    }

    void ApplyGroundBackground()
    {
        if (backgroundSprite == null)
            return;

        Vector2 mapSize = RoomSettings.GetMapDimensions();

        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
            return;

        SpriteRenderer renderer = ground.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        renderer.sprite = backgroundSprite;
        renderer.color = AdvancedSpaceBackground.GetGroundTintForCurrentSettings();
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = mapSize;
        renderer.sortingLayerName = WorldSortingLayerName;
        renderer.sortingOrder = BackgroundSortingOrder;
    }

    void ApplyMapBounds()
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        UpdateWall("WallTop", new Vector2(0f, mapSize.y / 2f), new Vector2(mapSize.x, 1f), true);
        UpdateWall("WallBottom", new Vector2(0f, -mapSize.y / 2f), new Vector2(mapSize.x, 1f), true);
        UpdateWall("WallLeft", new Vector2(-mapSize.x / 2f, 0f), new Vector2(1f, mapSize.y), false);
        UpdateWall("WallRight", new Vector2(mapSize.x / 2f, 0f), new Vector2(1f, mapSize.y), false);
    }

    void UpdateWall(string wallName, Vector2 position, Vector2 size, bool horizontal)
    {
        GameObject wall = GameObject.Find(wallName);
        if (wall == null)
            return;

        wall.transform.position = new Vector3(position.x, position.y, wall.transform.position.z);

        SpriteRenderer renderer = wall.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
        }

        BoxCollider2D collider = wall.GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            collider.offset = Vector2.zero;
            collider.size = size;
            collider.sharedMaterial = MovingSpaceObject.GetSharedSoftBoundaryMaterial();
        }

        wall.transform.localScale = horizontal
            ? new Vector3(1f, wall.transform.localScale.y, 1f)
            : new Vector3(wall.transform.localScale.x, 1f, 1f);
    }

    void ApplyPlayerSprites()
    {
        if (shipSprites == null || shipSprites.Length == 0)
            return;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        foreach (PlayerHealth player in players)
        {
            ApplyPlayerVisualInternal(player);
        }
    }

    void ApplyPlayerVisualInternal(PlayerHealth player)
    {
        if (player == null)
            return;

        if (player.GetComponent<LureBeaconDecoy>() != null)
            return;

        PhotonView view = player.GetComponent<PhotonView>();
        if (ViperRecoveryPlotController.IsViperWreckInstantiationData(view != null ? view.InstantiationData : null) ||
            player.GetComponent<ViperWreckTowTarget>() != null)
        {
            ViperRecoveryPlotController.TryEnsureViperWreckRuntime(player.gameObject);
            return;
        }

        if (PlayerDeployableRuntime.IsInstantiationData(view != null ? view.InstantiationData : null) ||
            player.GetComponent<PlayerDeployableBase>() != null)
        {
            PlayerDeployableRuntime.EnsureAttached(player.gameObject);
            return;
        }

        SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();

        if (view == null || renderer == null || view.Owner == null)
            return;

        EnemyBot enemyBot = player.GetComponent<EnemyBot>();
        ActorIdentity identity = ActorIdentity.Ensure(player.gameObject);
        AstronautSurvivor astronautSurvivor = player.GetComponent<AstronautSurvivor>();
        if (astronautSurvivor == null && AstronautSurvivor.IsAstronautInstantiationData(view.InstantiationData))
        {
            astronautSurvivor = player.gameObject.AddComponent<AstronautSurvivor>();
            astronautSurvivor.InitializeFromPhotonData();
            identity = ActorIdentity.Ensure(player.gameObject);
        }
        else if (astronautSurvivor != null)
        {
            astronautSurvivor.InitializeFromPhotonData();
        }

        bool isEnemyBot = (identity != null && identity.IsEnemy && identity.IsShip) || player.IsBotControlled || EnemyBot.IsBotInstantiationData(view.InstantiationData);
        bool isNeutralRider = (identity != null && identity.Team == ActorTeam.Neutral && identity.IsShip) ||
                              player.IsNeutralRiderControlled ||
                              NeutralRiderController.IsNeutralRiderInstantiationData(view.InstantiationData);
        bool isAstronaut = identity != null ? identity.IsAstronaut : astronautSurvivor != null || AstronautSurvivor.IsAstronautInstantiationData(view.InstantiationData);
        bool isEnemyAstronaut = identity != null ? identity.IsEnemy && identity.IsAstronaut : astronautSurvivor != null && astronautSurvivor.IsEnemySurvivor;
        bool isEscapePod = astronautSurvivor != null
            ? astronautSurvivor.IsEscapePodMode
            : AstronautSurvivor.IsEscapePodInstantiationData(view.InstantiationData);

        Sprite sprite = null;
        float targetSize = PlayerTargetSize;
        if (player.IsWreck)
        {
            ShipWreck wreck = player.GetComponent<ShipWreck>();
            bool isEnemyWreck = wreck != null && wreck.SourceShipSkinIndex < 0 && isEnemyBot;
            if (isEnemyWreck)
            {
                EnemyBotKind botKind = enemyBot != null ? enemyBot.Kind : EnemyBot.GetKindFromInstantiationData(view.InstantiationData);
                EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(botKind);
                Sprite wreckSprite = definition != null && definition.Wreck != null ? definition.Wreck.GetVisualSprite() : null;
                sprite = renderer.sprite != null
                    ? renderer.sprite
                    : wreckSprite != null
                        ? wreckSprite
                        : enemyBot != null
                            ? enemyBot.GetVisualSprite()
                            : definition != null
                                ? definition.GetVisualSprite()
                                : botKind == EnemyBotKind.Corsair ? corsairSprite : enemyBotSprite;
                targetSize = enemyBot != null
                    ? enemyBot.VisualTargetSize
                    : definition != null ? definition.TargetSize : botKind == EnemyBotKind.Corsair ? 5.2f : PlayerTargetSize;
            }
            else
            {
                int wreckSkinIndex = wreck != null ? wreck.SourceShipSkinIndex : RoomSettings.GetPlayerShipSkin(view.Owner, 0);
                sprite = GetMappedWreckSprite(wreckSkinIndex, renderer.sprite);
                if (wreckSkinIndex >= 0 && ShipCatalog.GetShipTypeFromSkinIndex(wreckSkinIndex) == ShipType.CargoTruck)
                    targetSize *= CargoTruckPlayerSizeMultiplier;
            }
        }
        else if (isAstronaut)
        {
            if (astronautSurvivor != null)
            {
                sprite = astronautSurvivor.GetVisualSprite();
                targetSize = astronautSurvivor.VisualTargetSize;
            }

            if (sprite == null && astronautSprite != null)
            {
                sprite = astronautSprite;
                targetSize = AstronautTargetSize;
            }
        }
        else if (isEnemyBot)
        {
            EnemyBotKind botKind = enemyBot != null ? enemyBot.Kind : EnemyBot.GetKindFromInstantiationData(view.InstantiationData);
            EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(botKind);
            sprite = enemyBot != null
                ? enemyBot.GetVisualSprite()
                : definition != null ? definition.GetVisualSprite() : botKind == EnemyBotKind.Corsair ? corsairSprite : enemyBotSprite;
            targetSize = enemyBot != null
                ? enemyBot.VisualTargetSize
                : definition != null ? definition.TargetSize : botKind == EnemyBotKind.Corsair ? 5.2f : PlayerTargetSize;
        }
        else
        {
            int fallbackIndex = Mathf.Abs(view.Owner.ActorNumber - 1) % shipSprites.Length;
            int shipIndex = isNeutralRider
                ? NeutralRiderController.GetShipSkinIndexFromInstantiationData(view.InstantiationData)
                : RoomSettings.GetPlayerShipSkin(view.Owner, fallbackIndex);
            shipIndex %= shipSprites.Length;
            sprite = shipSprites[shipIndex];
            if (ShipCatalog.GetShipTypeFromSkinIndex(shipIndex) == ShipType.CargoTruck)
                targetSize *= CargoTruckPlayerSizeMultiplier;
        }

        if (sprite == null)
            return;

        BoxCollider2D bodyCollider = player.GetComponent<BoxCollider2D>();
        CircleCollider2D pickupCollider = player.GetComponent<CircleCollider2D>();

        if (renderer.sprite != sprite)
        {
            renderer.sprite = sprite;
        }
        if (!player.IsWreck)
            renderer.color = Color.white;
        renderer.sortingLayerName = WorldSortingLayerName;
        renderer.sortingOrder = player.IsWreck
            ? WreckSortingOrder
            : isEnemyBot || isEnemyAstronaut ? EnemySortingOrder : PlayerSortingOrder;
        FitSpriteToTargetSize(renderer, targetSize);

        Vector2 spriteWorldSize = GetSpriteWorldSize(renderer);
        float bodyWidthFactor = isAstronaut ? (isEscapePod ? 0.52f : 0.42f) : PlayerBodyColliderWidthFactor;
        float bodyHeightFactor = isAstronaut ? (isEscapePod ? 0.68f : 0.58f) : PlayerBodyColliderHeightFactor;
        SetWorldBoxSize(bodyCollider, new Vector2(
            spriteWorldSize.x * bodyWidthFactor,
            spriteWorldSize.y * bodyHeightFactor));
        SetWorldCircleRadius(pickupCollider, Mathf.Max(spriteWorldSize.x, spriteWorldSize.y) * PlayerPickupRadiusFactor);
    }

    void ApplyTreasureSprites()
    {
        if (treasureSprite == null)
            return;

        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        foreach (Treasure treasure in treasures)
        {
            ApplyTreasureVisualInternal(treasure);
        }
    }

    void ApplyTreasureVisualInternal(Treasure treasure)
    {
        if (treasure == null)
            return;

        SpriteRenderer renderer = treasure.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        BoxCollider2D triggerCollider = treasure.GetComponent<BoxCollider2D>();

        Sprite desiredSprite = GetTreasureSprite(treasure);
        if (desiredSprite == null)
            desiredSprite = treasureSprite;

        if (renderer.sprite != desiredSprite)
        {
            renderer.sprite = desiredSprite;
        }
        renderer.color = GetTreasureTint(treasure);
        renderer.sortingLayerName = WorldSortingLayerName;
        renderer.sortingOrder = TreasureSortingOrder;
        FitSpriteToTargetSize(renderer, GetTreasureTargetSize(treasure));

        if (triggerCollider != null)
        {
            Vector2 spriteWorldSize = GetSpriteWorldSize(renderer);
            Vector2 colliderSize = spriteWorldSize * treasure.GetColliderSizeMultiplier();
            triggerCollider.isTrigger = false;
            SetWorldBoxSize(triggerCollider, colliderSize);
        }
    }

    Sprite GetTreasureSprite(Treasure treasure)
    {
        if (treasure == null)
            return treasureSprite;

        bool hiddenDimensionTarget = string.Equals(
            MapInstanceService.GetMapIdForTransform(treasure.transform),
            LobbyMapCatalog.HiddenDimensionMapId,
            System.StringComparison.Ordinal);
        Sprite activeTreasureSprite = hiddenDimensionTarget && hiddenDimensionTreasureSprite != null ? hiddenDimensionTreasureSprite : treasureSprite;
        Sprite activeGoldTreasureSprite = hiddenDimensionTarget && hiddenDimensionGoldTreasureSprite != null ? hiddenDimensionGoldTreasureSprite : goldTreasureSprite;
        Sprite activeRareTreasureSprite = hiddenDimensionTarget && hiddenDimensionRareTreasureSprite != null ? hiddenDimensionRareTreasureSprite : rareTreasureSprite;
        Sprite activeRichTreasureSprite = hiddenDimensionTarget && hiddenDimensionRichTreasureSprite != null ? hiddenDimensionRichTreasureSprite : richTreasureSprite;
        Sprite activeEpicTreasureSprite = hiddenDimensionTarget && hiddenDimensionEpicTreasureSprite != null ? hiddenDimensionEpicTreasureSprite : epicTreasureSprite;
        Sprite activeLegendaryTreasureSprite = hiddenDimensionTarget && hiddenDimensionLegendaryTreasureSprite != null ? hiddenDimensionLegendaryTreasureSprite : legendaryTreasureSprite;

        if (InventoryItemCatalog.IsAlienSecretItem(treasure.itemId))
        {
            int variantIndex = ResolveAlienSecretVariantIndex(treasure);
            if (alienSecretSprites != null && variantIndex >= 0 && variantIndex < alienSecretSprites.Length && alienSecretSprites[variantIndex] != null)
                return alienSecretSprites[variantIndex];

            return activeTreasureSprite;
        }

        if (InventoryItemCatalog.IsContainerItem(treasure.itemId))
        {
            int variantIndex = InventoryItemCatalog.GetContainerVariantIndex(treasure.itemId);
            if (containerSprites != null && variantIndex >= 0 && variantIndex < containerSprites.Length && containerSprites[variantIndex] != null)
                return containerSprites[variantIndex];

            return activeTreasureSprite;
        }

        if (InventoryItemCatalog.IsBlueprintScrapContainerItem(treasure.itemId))
        {
            int variantIndex = InventoryItemCatalog.GetBlueprintScrapContainerVariantIndex(treasure.itemId);
            if (blueprintScrapContainerSprites != null && variantIndex >= 0 && variantIndex < blueprintScrapContainerSprites.Length && blueprintScrapContainerSprites[variantIndex] != null)
                return blueprintScrapContainerSprites[variantIndex];

            return activeTreasureSprite;
        }

        if (InventoryItemCatalog.IsRandomLootWreckItem(treasure.itemId))
        {
            int variantIndex = InventoryItemCatalog.GetRandomLootWreckVariantIndex(treasure.itemId);
            if (randomLootWreckSprites != null && variantIndex >= 0 && variantIndex < randomLootWreckSprites.Length && randomLootWreckSprites[variantIndex] != null)
                return randomLootWreckSprites[variantIndex];

            return activeTreasureSprite;
        }

        switch (treasure.itemId)
        {
            case InventoryItemCatalog.PlatinumChunkId:
                return platinumChunkSprite != null ? platinumChunkSprite : activeLegendaryTreasureSprite != null ? activeLegendaryTreasureSprite : activeTreasureSprite;
            case InventoryItemCatalog.AsteroidGoldId:
                return activeGoldTreasureSprite != null ? activeGoldTreasureSprite : activeTreasureSprite;
            case InventoryItemCatalog.AsteroidRareId:
                return activeRareTreasureSprite != null ? activeRareTreasureSprite : activeTreasureSprite;
            case InventoryItemCatalog.RichAsteroidId:
                return activeRichTreasureSprite != null ? activeRichTreasureSprite : activeTreasureSprite;
            case InventoryItemCatalog.AsteroidEpicId:
                return activeEpicTreasureSprite != null ? activeEpicTreasureSprite : activeTreasureSprite;
            case InventoryItemCatalog.AsteroidLegendaryId:
                return activeLegendaryTreasureSprite != null ? activeLegendaryTreasureSprite : activeTreasureSprite;
            case InventoryItemCatalog.LegacySpaceJunkId:
            case InventoryItemCatalog.SpaceJunkStandardId:
                return spaceJunkStandardSprite != null ? spaceJunkStandardSprite : activeTreasureSprite;
            case InventoryItemCatalog.SpaceJunkTrashId:
                return spaceJunkTrashSprite != null ? spaceJunkTrashSprite : activeTreasureSprite;
            case InventoryItemCatalog.SpaceJunkAsteroidId:
                return spaceJunkAsteroidSprite != null ? spaceJunkAsteroidSprite : activeTreasureSprite;
            case InventoryItemCatalog.SpaceAnimalRemainsId:
                return spaceAnimalRemainsSprite != null ? spaceAnimalRemainsSprite : activeTreasureSprite;
            case InventoryItemCatalog.SpaceAnimalBonesId:
                if (spaceAnimalBonesSprite != null)
                    return spaceAnimalBonesSprite;

                return spaceAnimalRemainsSprite != null ? spaceAnimalRemainsSprite : activeTreasureSprite;
            case InventoryItemCatalog.RadioactiveTreasureId:
                return radioactiveTreasureSprite != null ? radioactiveTreasureSprite : activeTreasureSprite;
            default:
                return activeTreasureSprite;
        }
    }

    Color GetTreasureTint(Treasure treasure)
    {
        if (treasure != null && treasure.itemId == InventoryItemCatalog.PlatinumChunkId && platinumChunkSprite == null)
            return new Color(0.82f, 0.94f, 1f, 1f);

        return Color.white;
    }

    float GetTreasureTargetSize(Treasure treasure)
    {
        if (treasure != null && (InventoryItemCatalog.IsContainerItem(treasure.itemId) || InventoryItemCatalog.IsBlueprintScrapContainerItem(treasure.itemId)))
            return ContainerTargetSize;

        if (treasure != null && InventoryItemCatalog.IsRandomLootWreckItem(treasure.itemId))
            return RandomLootWreckTargetSize;

        if (treasure != null && InventoryItemCatalog.IsAlienSecretItem(treasure.itemId))
            return AlienSecretTargetSize;

        if (treasure != null && treasure.itemId == InventoryItemCatalog.SpaceAnimalRemainsId)
            return SpaceAnimalRemainsTargetSize;

        if (treasure != null && treasure.itemId == InventoryItemCatalog.SpaceAnimalBonesId)
            return SpaceAnimalBonesTargetSize;

        if (treasure != null && treasure.itemId == InventoryItemCatalog.PlatinumChunkId)
            return 1.28f;

        return TreasureTargetSize;
    }

    int ResolveAlienSecretVariantIndex(Treasure treasure)
    {
        if (treasure == null)
            return 0;

        if (treasure.visualVariantIndex >= 0)
            return InventoryItemCatalog.NormalizeAlienSecretVariantIndex(treasure.visualVariantIndex);

        int itemVariantIndex = InventoryItemCatalog.GetAlienSecretVariantIndex(treasure.itemId);
        if (itemVariantIndex >= 0)
            return itemVariantIndex;

        int hash = treasure.photonView != null ? treasure.photonView.ViewID : treasure.name.GetHashCode();
        return Mathf.Abs(hash) % InventoryItemCatalog.AlienSecretVariantCount;
    }

    Sprite[] LoadAlienSecretSprites()
    {
        Sprite[] sprites = new Sprite[InventoryItemCatalog.AlienSecretVariantCount];
        for (int i = 0; i < sprites.Length; i++)
        {
            string path = InventoryItemCatalog.GetAlienSecretSpriteResourcePath(i);
            string assetPath = "Assets/Resources/" + path + ".png";
            sprites[i] = LoadSpriteFromResourcesOrEditor(path, assetPath, assetPath);
        }

        return sprites;
    }

    Sprite[] LoadRandomLootWreckSprites()
    {
        Sprite[] sprites = new Sprite[InventoryItemCatalog.RandomLootWreckVariantCount];
        for (int i = 0; i < sprites.Length; i++)
        {
            string suffix = (i + 1).ToString("00");
            sprites[i] = LoadSpriteFromResourcesOrEditor(
                "random_loot_wreck_" + suffix,
                "Assets/Resources/random_loot_wreck_" + suffix + ".png",
                "Assets/random_loot_wrecks.png");
        }

        return sprites;
    }

    void ApplyObstacleSprites()
    {
        if (obstacleSprites == null || obstacleSprites.Length == 0)
            return;

        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            GameObject target = renderer.gameObject;
            if (!IsObstacleVisualTarget(target))
                continue;

            ApplyObstacleVisualInternal(target, renderer);
        }
    }

    bool IsObstacleVisualTarget(GameObject target)
    {
        return target != null &&
               target.name.StartsWith("Obstacle", System.StringComparison.Ordinal) &&
               target.GetComponent<PlayerHealth>() == null &&
               target.GetComponent<Treasure>() == null;
    }

    void ApplyObstacleVisualInternal(GameObject target)
    {
        if (target == null)
            return;

        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        ApplyObstacleVisualInternal(target, renderer);
    }

    void ApplyObstacleVisualInternal(GameObject target, SpriteRenderer renderer)
    {
        Sprite[] activeObstacleSprites = GetObstacleSpritesForTarget(target);
        if (target == null || renderer == null || activeObstacleSprites == null || activeObstacleSprites.Length == 0)
            return;

        PolygonCollider2D polygonCollider = target.GetComponent<PolygonCollider2D>();
        BoxCollider2D boxCollider = target.GetComponent<BoxCollider2D>();

        Sprite obstacleSprite = GetStableObstacleSprite(target, activeObstacleSprites);
        if (obstacleSprite == null)
            return;

        if (renderer.sprite != obstacleSprite)
        {
            renderer.sprite = obstacleSprite;
            renderer.color = Color.white;
        }
        renderer.sortingLayerName = WorldSortingLayerName;
        renderer.sortingOrder = ObstacleSortingOrder;

        float obstacleSize = ObstacleTargetSize * MapInstanceService.GetObstacleSizeMultiplierForPosition(target.transform.position) * GetStableObstacleSizeMultiplier(target);
        FitSpriteToTargetSize(renderer, obstacleSize);

        if (polygonCollider == null)
            polygonCollider = target.AddComponent<PolygonCollider2D>();

        polygonCollider.isTrigger = false;
        polygonCollider.autoTiling = false;

        if (boxCollider != null)
            boxCollider.enabled = false;

        MovingSpaceObject movingObject = target.GetComponent<MovingSpaceObject>();
        if (movingObject != null)
            movingObject.NotifyColliderShapeChanged();

        ConfigureRadioactiveObstacleHazard(target);
    }

    void ConfigureRadioactiveObstacleHazard(GameObject target)
    {
        if (!Application.isPlaying || target == null)
            return;

        RadioactiveObstacleHazard hazard = target.GetComponent<RadioactiveObstacleHazard>();
        bool toxicAreaTheme = string.Equals(
            MapInstanceService.GetMapIdForTransform(target.transform),
            LobbyMapCatalog.ToxicAreaMapId,
            System.StringComparison.Ordinal);
        if (toxicAreaTheme)
        {
            if (hazard == null)
                hazard = target.AddComponent<RadioactiveObstacleHazard>();

            hazard.RefreshFromObstacle();
        }
        else if (hazard != null)
        {
            Destroy(hazard);
        }
    }

    void ApplyExtractionZoneSprites()
    {
        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        foreach (ExtractionZone zone in zones)
        {
            ApplyExtractionZoneVisualInternal(zone);
        }
    }

    void ApplyExtractionZoneVisualInternal(ExtractionZone zone)
    {
        if (zone == null)
            return;

        string extractionType = MapInstanceService.TryGetExtractionTypeForObject(zone.gameObject, out string zoneExtractionType)
            ? zoneExtractionType
            : RoomSettings.GetExtractionType();
        Sprite extractionSprite = GetExtractionZoneSprite(extractionType);
        if (extractionSprite == null)
            return;

        float targetSize = GetExtractionZoneTargetSize(extractionType);

        SpriteRenderer renderer = zone.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        if (renderer.sprite != extractionSprite)
        {
            renderer.sprite = extractionSprite;
        }
        renderer.color = Color.white;
        renderer.sortingLayerName = WorldSortingLayerName;
        renderer.sortingOrder = ExtractionZoneSortingOrder;
        FitSpriteToTargetSize(renderer, targetSize);
        zone.RefreshInteractionCollider();
        if (Application.isPlaying)
            zone.RefreshExtractionVisual();
    }

    Sprite GetExtractionZoneSprite(string extractionType)
    {
        switch (RoomSettings.NormalizeExtractionType(extractionType))
        {
            case RoomSettings.ExtractionTypeCarrier:
                return carrierExtractionSprite != null ? carrierExtractionSprite : portalExtractionSprite;
            case RoomSettings.ExtractionTypeSpaceCity:
                return spaceCityExtractionSprite != null ? spaceCityExtractionSprite : portalExtractionSprite;
            case RoomSettings.ExtractionTypeAncientPortal:
                return ancientPortalExtractionSprite != null ? ancientPortalExtractionSprite : portalExtractionSprite;
        }

        return portalExtractionSprite;
    }

    float GetExtractionZoneTargetSize(string extractionType)
    {
        switch (RoomSettings.NormalizeExtractionType(extractionType))
        {
            case RoomSettings.ExtractionTypeCarrier:
                return CarrierExtractionTargetSize;
            case RoomSettings.ExtractionTypeSpaceCity:
                return SpaceCityExtractionTargetSize;
            case RoomSettings.ExtractionTypeAncientPortal:
                return AncientPortalExtractionTargetSize;
            default:
                return ExtractionTargetSize;
        }
    }

    void FitSpriteToTargetSize(SpriteRenderer renderer, float targetMaxWorldSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        float maxDimension = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        if (maxDimension <= 0f)
            return;

        float scale = targetMaxWorldSize / maxDimension;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    Vector2 GetSpriteWorldSize(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
            return Vector2.zero;

        Bounds bounds = renderer.bounds;
        return new Vector2(bounds.size.x, bounds.size.y);
    }

    float GetStableObstacleSizeMultiplier(GameObject target)
    {
        if (target == null)
            return 1f;

        ObstacleChunk chunk = target.GetComponent<ObstacleChunk>();
        if (chunk != null)
            return chunk.SizeFactor;

        MovingSpaceObject movingObject = target.GetComponent<MovingSpaceObject>();
        string stableKey = movingObject != null && !string.IsNullOrWhiteSpace(movingObject.StableId)
            ? movingObject.StableId
            : target.name;

        int hash = stableKey.GetHashCode();
        float sampleX = Mathf.Abs(hash * 0.00013f) + 17.3f;
        float sampleY = Mathf.Abs(hash * 0.00029f) + 29.7f;
        float noise = Mathf.PerlinNoise(sampleX, sampleY);
        return Mathf.Lerp(0.5f, 1.5f, noise);
    }

    Sprite[] GetObstacleSpritesForTarget(GameObject target)
    {
        if (target != null &&
            string.Equals(
                MapInstanceService.GetMapIdForTransform(target.transform),
                LobbyMapCatalog.HiddenDimensionMapId,
                System.StringComparison.Ordinal))
        {
            return hiddenDimensionObstacleSprites != null && hiddenDimensionObstacleSprites.Length > 0
                ? hiddenDimensionObstacleSprites
                : obstacleSprites;
        }

        return obstacleSprites;
    }

    Sprite GetStableObstacleSprite(GameObject target, Sprite[] activeObstacleSprites)
    {
        if (target == null || activeObstacleSprites == null || activeObstacleSprites.Length == 0)
            return null;

        ObstacleChunk chunk = target.GetComponent<ObstacleChunk>();
        if (chunk != null && chunk.SpriteVariantIndex >= 0)
        {
            int chunkIndex = Mathf.Clamp(chunk.SpriteVariantIndex, 0, activeObstacleSprites.Length - 1);
            return activeObstacleSprites[chunkIndex];
        }

        MovingSpaceObject movingObject = target.GetComponent<MovingSpaceObject>();
        string stableKey = movingObject != null && !string.IsNullOrWhiteSpace(movingObject.StableId)
            ? movingObject.StableId
            : target.name;

        int index = ObstacleChunk.ComputeStableSpriteVariantIndex(stableKey, activeObstacleSprites.Length);
        index = Mathf.Clamp(index, 0, activeObstacleSprites.Length - 1);
        return activeObstacleSprites[index];
    }

    Sprite GetMappedWreckSprite(int shipSkinIndex, Sprite fallback)
    {
        if (wreckSprites != null && shipSkinIndex >= 0 && shipSkinIndex < wreckSprites.Length && wreckSprites[shipSkinIndex] != null)
            return wreckSprites[shipSkinIndex];

        return fallback;
    }

    Vector2 GetWorldBoxSize(BoxCollider2D collider2D)
    {
        if (collider2D == null)
            return Vector2.zero;

        Vector3 scale = collider2D.transform.lossyScale;
        return new Vector2(
            Mathf.Abs(collider2D.size.x * scale.x),
            Mathf.Abs(collider2D.size.y * scale.y));
    }

    void SetWorldBoxSize(BoxCollider2D collider2D, Vector2 worldSize)
    {
        if (collider2D == null || worldSize == Vector2.zero)
            return;

        Vector3 scale = collider2D.transform.lossyScale;
        float safeX = Mathf.Abs(scale.x) > 0.0001f ? Mathf.Abs(scale.x) : 1f;
        float safeY = Mathf.Abs(scale.y) > 0.0001f ? Mathf.Abs(scale.y) : 1f;

        collider2D.size = new Vector2(worldSize.x / safeX, worldSize.y / safeY);
    }

    float GetWorldCircleRadius(CircleCollider2D collider2D)
    {
        if (collider2D == null)
            return 0f;

        Vector3 scale = collider2D.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        return collider2D.radius * maxScale;
    }

    void SetWorldCircleRadius(CircleCollider2D collider2D, float worldRadius)
    {
        if (collider2D == null || worldRadius <= 0f)
            return;

        Vector3 scale = collider2D.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        if (maxScale <= 0.0001f)
            maxScale = 1f;

        collider2D.radius = worldRadius / maxScale;
    }

    Sprite LoadSpriteFromProjectOrResources(string projectFileName, string resourcesPath)
    {
        string editorPath = string.IsNullOrWhiteSpace(projectFileName) ? null : "Assets/" + projectFileName;
        return LoadSpriteFromResourcesOrEditor(resourcesPath, editorPath);
    }

    Sprite LoadObstacleSprite(string resourcesPath, string editorPreferredPath, string editorFallbackPath)
    {
        Sprite sprite = LoadSpriteFromResourcesOrEditor(resourcesPath, editorPreferredPath, editorFallbackPath);
        if (sprite != null)
            return sprite;

        return LoadSpriteFromResourcesOrEditor("Visuals/Obstacles/asteroid_obstacle_resource", "Assets/Resources/Visuals/Obstacles/asteroid_obstacle_resource.png", "Assets/asteroida_obstacle.png");
    }

    Sprite LoadBackgroundSprite(int backgroundIndex)
    {
        int clampedIndex = Mathf.Clamp(backgroundIndex, 1, RoomSettings.MaxMapBackground);
        string resourcesPath = "Visuals/Backgrounds/background" + clampedIndex + "_resource";
        Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
        Sprite sprite = texture != null ? CreateSpriteFromTexture(texture) : null;
        if (sprite != null)
            return sprite;

        Texture2D fallbackTexture = Resources.Load<Texture2D>("Visuals/Backgrounds/background5_resource");
        return fallbackTexture != null ? CreateSpriteFromTexture(fallbackTexture) : LoadSpriteFromResources("Visuals/Backgrounds/background5_resource");
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

    Sprite[] LoadSpritesFromResourcesOrEditor(string resourcesPath, string editorPreferredPath, string editorFallbackPath = null)
    {
        Sprite[] sprites = LoadSpritesFromResources(resourcesPath);
        if (sprites != null && sprites.Length > 0)
            return sprites;

#if UNITY_EDITOR
        sprites = LoadEditorSprites(editorPreferredPath);
        if (sprites != null && sprites.Length > 0)
            return sprites;

        if (!string.IsNullOrWhiteSpace(editorFallbackPath))
            return LoadEditorSprites(editorFallbackPath);
#endif

        return System.Array.Empty<Sprite>();
    }

    Sprite[] LoadSpritesFromResources(string resourcesPath)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath))
            return System.Array.Empty<Sprite>();

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcesPath);
        return SortSpritesByName(sprites);
    }

    Sprite LoadSpriteFromResources(string resourcesPath)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
            return sprite;

        Sprite[] resourceSprites = Resources.LoadAll<Sprite>(resourcesPath);
        sprite = GetLargestSprite(resourceSprites);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
        if (texture == null)
            return null;

        return CreateSpriteFromTexture(texture);
    }

    Sprite CreateSpriteFromTexture(Texture2D texture)
    {
        if (texture == null)
            return null;

        float pixelsPerUnit = Mathf.Max(1f, texture.width / BackgroundTileWorldSize);
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);
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

            Rect rect = candidate.rect;
            float area = rect.width * rect.height;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }

#if UNITY_EDITOR
    Sprite[] LoadEditorSprites(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return System.Array.Empty<Sprite>();

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        List<Sprite> sprites = new List<Sprite>();
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite loadedSprite)
                sprites.Add(loadedSprite);
        }

        return SortSpritesByName(sprites.ToArray());
    }

    Sprite LoadEditorSprite(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
            return sprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite loadedSprite)
                return loadedSprite;
        }

        return null;
    }
#endif

    Sprite[] SortSpritesByName(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return System.Array.Empty<Sprite>();

        System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));
        return sprites;
    }
}

