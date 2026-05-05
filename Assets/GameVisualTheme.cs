using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class GameVisualTheme : MonoBehaviour
{
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

    const float PlayerTargetSize = 1.04f;
    const float AstronautTargetSize = 0.56f;
    const float TreasureTargetSize = 1.5f;
    const float TreasureColliderSizeMultiplier = 0.9f;
    const float PlayerBodyColliderWidthFactor = 0.46f;
    const float PlayerBodyColliderHeightFactor = 0.62f;
    const float PlayerPickupRadiusFactor = 0.8f;
    const float ObstacleTargetSize = 3.0f;
    const float ExtractionTargetSize = 4.3f;
    const float BackgroundTileWorldSize = 8f;
    const float DesktopRefreshInterval = 0.75f;
    const float RuntimeRefreshDelay = 0.08f;

    static GameVisualTheme instance;

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
    Sprite spaceJunkTrashSprite;
    Sprite spaceJunkStandardSprite;
    Sprite spaceJunkAsteroidSprite;
    Sprite[] obstacleSprites;
    Sprite extractionSprite;
    Sprite backgroundSprite;
    float nextRefreshTime;
    bool runtimeThemeDirty;
    bool runtimeReloadAssetsDirty;
    int lastRuntimeBackgroundIndex = int.MinValue;
    int lastRuntimeObstacleSizePercent = int.MinValue;
    string lastRuntimeMapSizeMode = string.Empty;
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

    public static void RequestRuntimeRefresh(bool reloadAssets = false)
    {
        EnsureInstance();
        if (instance == null)
            return;

        instance.MarkRuntimeThemeDirty(reloadAssets);
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
        if (!runtimeThemeDirty || Time.unscaledTime < nextRefreshTime)
            return;

        runtimeThemeDirty = false;
        if (runtimeReloadAssetsDirty)
        {
            runtimeReloadAssetsDirty = false;
            LoadAssets();
        }

        ApplyTheme();
        nextRefreshTime = 0f;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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

        float requestedRefreshTime = Time.unscaledTime + Mathf.Max(0f, delay);
        if (nextRefreshTime <= 0f || requestedRefreshTime < nextRefreshTime)
            nextRefreshTime = requestedRefreshTime;
    }

    void CaptureRuntimeSettingsSnapshot()
    {
        lastRuntimeBackgroundIndex = RoomSettings.GetMapBackgroundIndex();
        lastRuntimeMapSizeMode = RoomSettings.GetMapSizeMode();
        lastRuntimeObstacleSizePercent = RoomSettings.GetObstacleSizePercent();
    }

    void RefreshRuntimeSettingsIfNeeded()
    {
        int backgroundIndex = RoomSettings.GetMapBackgroundIndex();
        string mapSizeMode = RoomSettings.GetMapSizeMode();
        int obstacleSizePercent = RoomSettings.GetObstacleSizePercent();

        bool backgroundChanged = backgroundIndex != lastRuntimeBackgroundIndex;
        if (!backgroundChanged &&
            string.Equals(mapSizeMode, lastRuntimeMapSizeMode, System.StringComparison.Ordinal) &&
            obstacleSizePercent == lastRuntimeObstacleSizePercent)
        {
            return;
        }

        lastRuntimeBackgroundIndex = backgroundIndex;
        lastRuntimeMapSizeMode = mapSizeMode;
        lastRuntimeObstacleSizePercent = obstacleSizePercent;
        MarkRuntimeThemeDirty(backgroundChanged, 0f);
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

        treasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_white_common_resource", "Assets/Resources/treasure_asteroid_white_common_resource.png", "Assets/treasure_asteroid_white_common.png");
        goldTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_green_uncommon_resource", "Assets/Resources/treasure_asteroid_green_uncommon_resource.png", "Assets/treasure_asteroid_green_uncommon.png");
        rareTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_blue_rare_resource", "Assets/Resources/treasure_asteroid_blue_rare_resource.png", "Assets/treasure_asteroid_blue_rare.png");
        richTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_violet_rare_resource", "Assets/Resources/treasure_asteroid_violet_rare_resource.png", "Assets/treasure_asteroid_violet_rare.png");
        epicTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_burgundy_epic_resource", "Assets/Resources/treasure_asteroid_burgundy_epic_resource.png", "Assets/treasure_asteroid_burgundy_epic.png");
        legendaryTreasureSprite = LoadSpriteFromResourcesOrEditor("treasure_asteroid_gold_legendary_resource", "Assets/Resources/treasure_asteroid_gold_legendary_resource.png", "Assets/treasure_asteroid_gold_legendary.png");
        spaceJunkTrashSprite = LoadSpriteFromResourcesOrEditor("space_junk_trash", "Assets/Resources/space_junk_trash.png", "Assets/space_junk_trash.png");
        spaceJunkStandardSprite = LoadSpriteFromResourcesOrEditor("space_junk_standard", "Assets/Resources/space_junk_standard.png", "Assets/space_junk_standard.png");
        spaceJunkAsteroidSprite = LoadSpriteFromResourcesOrEditor("space_junk_asteroid", "Assets/Resources/space_junk_asteroid.png", "Assets/space_junk_asteroid.png");
        obstacleSprites = new[]
        {
            LoadObstacleSprite("asteroida_1_clean_resource", "Assets/Resources/asteroida_1_clean_resource.png", "Assets/asteroida_1_clean.png"),
            LoadObstacleSprite("asteroida_2_clean_resource", "Assets/Resources/asteroida_2_clean_resource.png", "Assets/asteroida_2_clean.png"),
            LoadObstacleSprite("asteroida_3_clean_resource", "Assets/Resources/asteroida_3_clean_resource.png", "Assets/asteroida_3_clean.png"),
            LoadObstacleSprite("asteroida_podluzna_1_clean_resource", "Assets/Resources/asteroida_podluzna_1_clean_resource.png", "Assets/asteroida_podluzna_1_clean.png"),
            LoadObstacleSprite("asteroida_podluzna_2_clean_resource", "Assets/Resources/asteroida_podluzna_2_clean_resource.png", "Assets/asteroida_podluzna_2_clean.png"),
            LoadObstacleSprite("asteroida_nadkruszona_resource", "Assets/Resources/asteroida_nadkruszona_resource.png", "Assets/asteroida_nadkruszona.png"),
            LoadObstacleSprite("asteroida_ziemniak_resource", "Assets/Resources/asteroida_ziemniak_resource.png", "Assets/asteroida_ziemniak.png")
        };
        extractionSprite = LoadSpriteFromResourcesOrEditor("Visuals/Bases/base1_resource", "Assets/Resources/Visuals/Bases/base1_resource.png", "Assets/baza1.png");
        backgroundSprite = LoadBackgroundSprite(RoomSettings.GetMapBackgroundIndex());
        backgroundSprite = LoadSpriteFromResourcesOrEditor("Visuals/Backgrounds/background5_resource", "Assets/Resources/Visuals/Backgrounds/background5_resource.png", "Assets/tło5.png");
        if (backgroundSprite == null)
        {
            backgroundSprite = LoadSpriteFromProjectOrResources("tło5.png", "Visuals/Backgrounds/background5_resource");
        }
        backgroundSprite = LoadBackgroundSprite(RoomSettings.GetMapBackgroundIndex());
    }

    void ApplyTheme()
    {
        ApplyGroundBackground();
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
        renderer.color = Color.white;
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
            collider.sharedMaterial = MovingSpaceObject.GetSharedBouncyMaterial();
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
            if (player == null)
                continue;

            if (player.GetComponent<LureBeaconDecoy>() != null)
                continue;

            PhotonView view = player.GetComponent<PhotonView>();
            SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();

            if (view == null || renderer == null || view.Owner == null)
                continue;

            EnemyBot enemyBot = player.GetComponent<EnemyBot>();
            bool isEnemyBot = player.IsBotControlled || EnemyBot.IsBotInstantiationData(view.InstantiationData);
            bool isAstronaut = player.GetComponent<AstronautSurvivor>() != null || AstronautSurvivor.IsAstronautInstantiationData(view.InstantiationData);

            Sprite sprite;
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
                }
            }
            else if (isAstronaut && astronautSprite != null)
            {
                sprite = astronautSprite;
                targetSize = AstronautTargetSize;
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
                int shipIndex = RoomSettings.GetPlayerShipSkin(view.Owner, fallbackIndex) % shipSprites.Length;
                sprite = shipSprites[shipIndex];
            }

            if (sprite == null)
                continue;

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
                : isEnemyBot ? EnemySortingOrder : PlayerSortingOrder;
            FitSpriteToTargetSize(renderer, targetSize);

            Vector2 spriteWorldSize = GetSpriteWorldSize(renderer);
            SetWorldBoxSize(bodyCollider, new Vector2(
                spriteWorldSize.x * PlayerBodyColliderWidthFactor,
                spriteWorldSize.y * PlayerBodyColliderHeightFactor));
            SetWorldCircleRadius(pickupCollider, Mathf.Max(spriteWorldSize.x, spriteWorldSize.y) * PlayerPickupRadiusFactor);
        }
    }

    void ApplyTreasureSprites()
    {
        if (treasureSprite == null)
            return;

        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        foreach (Treasure treasure in treasures)
        {
            if (treasure == null)
                continue;

            SpriteRenderer renderer = treasure.GetComponent<SpriteRenderer>();
            if (renderer == null)
                continue;

            BoxCollider2D triggerCollider = treasure.GetComponent<BoxCollider2D>();

            Sprite desiredSprite = GetTreasureSprite(treasure);
            if (desiredSprite == null)
                desiredSprite = treasureSprite;

            if (renderer.sprite != desiredSprite)
            {
                renderer.sprite = desiredSprite;
            }
            renderer.sortingLayerName = WorldSortingLayerName;
            renderer.sortingOrder = TreasureSortingOrder;
            FitSpriteToTargetSize(renderer, TreasureTargetSize);

            if (triggerCollider != null)
            {
                Vector2 spriteWorldSize = GetSpriteWorldSize(renderer);
                Vector2 colliderSize = spriteWorldSize * treasure.GetColliderSizeMultiplier();
                triggerCollider.isTrigger = false;
                SetWorldBoxSize(triggerCollider, colliderSize);
            }
        }
    }

    Sprite GetTreasureSprite(Treasure treasure)
    {
        if (treasure == null)
            return treasureSprite;

        switch (treasure.itemId)
        {
            case InventoryItemCatalog.AsteroidGoldId:
                return goldTreasureSprite != null ? goldTreasureSprite : treasureSprite;
            case InventoryItemCatalog.AsteroidRareId:
                return rareTreasureSprite != null ? rareTreasureSprite : treasureSprite;
            case InventoryItemCatalog.RichAsteroidId:
                return richTreasureSprite != null ? richTreasureSprite : treasureSprite;
            case InventoryItemCatalog.AsteroidEpicId:
                return epicTreasureSprite != null ? epicTreasureSprite : treasureSprite;
            case InventoryItemCatalog.AsteroidLegendaryId:
                return legendaryTreasureSprite != null ? legendaryTreasureSprite : treasureSprite;
            case InventoryItemCatalog.LegacySpaceJunkId:
            case InventoryItemCatalog.SpaceJunkStandardId:
                return spaceJunkStandardSprite != null ? spaceJunkStandardSprite : treasureSprite;
            case InventoryItemCatalog.SpaceJunkTrashId:
                return spaceJunkTrashSprite != null ? spaceJunkTrashSprite : treasureSprite;
            case InventoryItemCatalog.SpaceJunkAsteroidId:
                return spaceJunkAsteroidSprite != null ? spaceJunkAsteroidSprite : treasureSprite;
            default:
                return treasureSprite;
        }
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
            if (!target.name.StartsWith("Obstacle"))
                continue;

            if (target.GetComponent<PlayerHealth>() != null || target.GetComponent<Treasure>() != null)
                continue;

            ApplyObstacleVisualInternal(target, renderer);
        }
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
        if (target == null || renderer == null || obstacleSprites == null || obstacleSprites.Length == 0)
            return;

        PolygonCollider2D polygonCollider = target.GetComponent<PolygonCollider2D>();
        BoxCollider2D boxCollider = target.GetComponent<BoxCollider2D>();

        Sprite obstacleSprite = GetStableObstacleSprite(target);
        if (obstacleSprite == null)
            return;

        if (renderer.sprite != obstacleSprite)
        {
            renderer.sprite = obstacleSprite;
            renderer.color = Color.white;
        }
        renderer.sortingLayerName = WorldSortingLayerName;
        renderer.sortingOrder = ObstacleSortingOrder;

        float obstacleSize = ObstacleTargetSize * RoomSettings.GetObstacleSizeMultiplier() * GetStableObstacleSizeMultiplier(target);
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
    }

    void ApplyExtractionZoneSprites()
    {
        if (extractionSprite == null)
            return;

        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        foreach (ExtractionZone zone in zones)
        {
            if (zone == null)
                continue;

            SpriteRenderer renderer = zone.GetComponent<SpriteRenderer>();
            if (renderer == null)
                continue;

            CircleCollider2D triggerCollider = zone.GetComponent<CircleCollider2D>();
            float triggerWorldRadius = GetWorldCircleRadius(triggerCollider);

            if (renderer.sprite != extractionSprite)
            {
                renderer.sprite = extractionSprite;
            }
            renderer.sortingLayerName = WorldSortingLayerName;
            renderer.sortingOrder = ExtractionZoneSortingOrder;
            FitSpriteToTargetSize(renderer, ExtractionTargetSize);
            SetWorldCircleRadius(triggerCollider, triggerWorldRadius);
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

    Sprite GetStableObstacleSprite(GameObject target)
    {
        if (target == null || obstacleSprites == null || obstacleSprites.Length == 0)
            return null;

        ObstacleChunk chunk = target.GetComponent<ObstacleChunk>();
        if (chunk != null && chunk.SpriteVariantIndex >= 0)
        {
            int chunkIndex = Mathf.Clamp(chunk.SpriteVariantIndex, 0, obstacleSprites.Length - 1);
            return obstacleSprites[chunkIndex];
        }

        MovingSpaceObject movingObject = target.GetComponent<MovingSpaceObject>();
        string stableKey = movingObject != null && !string.IsNullOrWhiteSpace(movingObject.StableId)
            ? movingObject.StableId
            : target.name;

        int index = ObstacleChunk.ComputeStableSpriteVariantIndex(stableKey, obstacleSprites.Length);
        index = Mathf.Clamp(index, 0, obstacleSprites.Length - 1);
        return obstacleSprites[index];
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
        int clampedIndex = Mathf.Clamp(backgroundIndex, 1, 15);
        string resourcesPath = "Visuals/Backgrounds/background" + clampedIndex + "_resource";
        Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
        Sprite sprite = texture != null ? CreateSpriteFromTexture(texture) : null;
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        sprite = LoadEditorSprite("Assets/tło" + clampedIndex + ".png");
        if (sprite != null)
            return sprite;
#endif

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
}

