using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class GameVisualTheme : MonoBehaviour
{
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
    const float RefreshInterval = 0.75f;

    static GameVisualTheme instance;

    Sprite[] shipSprites;
    Sprite[] wreckSprites;
    Sprite enemyBotSprite;
    Sprite corsairSprite;
    Sprite astronautSprite;
    Sprite treasureSprite;
    Sprite goldTreasureSprite;
    Sprite rareTreasureSprite;
    Sprite[] obstacleSprites;
    Sprite extractionSprite;
    Sprite backgroundSprite;
    float nextRefreshTime;
    int lastRuntimeSignature = int.MinValue;
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
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (EditorApplication.timeSinceStartup < nextEditorRefreshTime)
                return;

            nextEditorRefreshTime = EditorApplication.timeSinceStartup + RefreshInterval;
            ApplyThemeInEditor();
            return;
        }
#endif

        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + RefreshInterval;
        int currentSignature = CalculateRuntimeSignature();
        if (currentSignature == lastRuntimeSignature)
            return;

        lastRuntimeSignature = currentSignature;
        LoadAssets();
        ApplyTheme();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LoadAssets();
        nextRefreshTime = 0f;
        lastRuntimeSignature = int.MinValue;
        ApplyTheme();
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
        shipSprites = new[]
        {
            LoadSpriteFromResourcesOrEditor("Visuals/Ships/ship1_resource", "Assets/Resources/Visuals/Ships/ship1_resource.png", "Assets/ship1.png"),
            LoadSpriteFromResourcesOrEditor("Visuals/Ships/ship2_resource", "Assets/Resources/Visuals/Ships/ship2_resource.png", "Assets/ship2.png"),
            LoadSpriteFromResourcesOrEditor("Visuals/Ships/ship3_resource", "Assets/Resources/Visuals/Ships/ship3_resource.png", "Assets/ship3.png"),
            LoadSpriteFromResourcesOrEditor("ship4_resource", "Assets/Resources/ship4_resource.png", "Assets/ship4.png")
        };
        wreckSprites = new[]
        {
            null,
            LoadSpriteFromResourcesOrEditor("wrak2_resource", "Assets/Resources/wrak2_resource.png", "Assets/wrak2.png"),
            null,
            null
        };
        enemyBotSprite = LoadSpriteFromResourcesOrEditor("droid1_resource", "Assets/Resources/droid1_resource.png", "Assets/droid1.png");
        corsairSprite = LoadSpriteFromResourcesOrEditor("statek_duzy_resource", "Assets/Resources/statek_duzy_resource.png", "Assets/statek_duzy.png");
        astronautSprite = LoadSpriteFromResourcesOrEditor("kosmonauta_resource", "Assets/Resources/kosmonauta_resource.png", "Assets/kosmonauta.png");

        treasureSprite = LoadSpriteFromResourcesOrEditor("Visuals/Treasures/asteroid_treasure_resource", "Assets/Resources/Visuals/Treasures/asteroid_treasure_resource.png", "Assets/asteroida_treasure.png");
        goldTreasureSprite = LoadSpriteFromResourcesOrEditor("asteroida_zloto_clean_resource", "Assets/Resources/asteroida_zloto_clean_resource.png", "Assets/asteroida_zloto_clean.png");
        rareTreasureSprite = LoadSpriteFromResourcesOrEditor("asteroida_rare_clean_resource", "Assets/Resources/asteroida_rare_clean_resource.png", "Assets/asteroida_rare_clean.png");
        obstacleSprites = new[]
        {
            LoadObstacleSprite("asteroida_1_clean_resource", "Assets/Resources/asteroida_1_clean_resource.png", "Assets/asteroida_1_clean.png"),
            LoadObstacleSprite("asteroida_2_clean_resource", "Assets/Resources/asteroida_2_clean_resource.png", "Assets/asteroida_2_clean.png"),
            LoadObstacleSprite("asteroida_3_clean_resource", "Assets/Resources/asteroida_3_clean_resource.png", "Assets/asteroida_3_clean.png"),
            LoadObstacleSprite("asteroida_podluzna_1_clean_resource", "Assets/Resources/asteroida_podluzna_1_clean_resource.png", "Assets/asteroida_podluzna_1_clean.png"),
            LoadObstacleSprite("asteroida_podluzna_2_clean_resource", "Assets/Resources/asteroida_podluzna_2_clean_resource.png", "Assets/asteroida_podluzna_2_clean.png")
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

            PolygonCollider2D polygonCollider = target.GetComponent<PolygonCollider2D>();
            BoxCollider2D boxCollider = target.GetComponent<BoxCollider2D>();

            Sprite obstacleSprite = GetStableObstacleSprite(target);
            if (obstacleSprite == null)
                continue;

            if (renderer.sprite != obstacleSprite)
            {
                renderer.sprite = obstacleSprite;
                renderer.color = Color.white;
            }
            float obstacleSize = ObstacleTargetSize * GetStableObstacleSizeMultiplier(target);
            FitSpriteToTargetSize(renderer, obstacleSize);

            if (polygonCollider == null)
            {
                polygonCollider = target.AddComponent<PolygonCollider2D>();
            }

            polygonCollider.isTrigger = false;
            polygonCollider.autoTiling = false;

            if (boxCollider != null)
            {
                boxCollider.enabled = false;
            }
        }
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

        MovingSpaceObject movingObject = target.GetComponent<MovingSpaceObject>();
        string stableKey = movingObject != null && !string.IsNullOrWhiteSpace(movingObject.StableId)
            ? movingObject.StableId
            : target.name;

        int hash = stableKey.GetHashCode();
        int index = Mathf.Abs(hash) % obstacleSprites.Length;
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

    int CalculateRuntimeSignature()
    {
        unchecked
        {
            int signature = 17;
            PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
            Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
            ExtractionZone[] extractionZones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
            ShipWreck[] wrecks = FindObjectsByType<ShipWreck>(FindObjectsInactive.Exclude);
            DroppedCargoCrate[] droppedCrates = FindObjectsByType<DroppedCargoCrate>(FindObjectsInactive.Exclude);

            signature = signature * 31 + players.Length;
            signature = signature * 31 + treasures.Length;
            signature = signature * 31 + extractionZones.Length;
            signature = signature * 31 + wrecks.Length;
            signature = signature * 31 + droppedCrates.Length;
            signature = signature * 31 + RoomSettings.GetMapBackgroundIndex();
            signature = signature * 31 + RoomSettings.GetMapSizeMode().GetHashCode();

            for (int i = 0; i < players.Length; i++)
            {
                PlayerHealth player = players[i];
                if (player == null)
                    continue;

                PhotonView view = player.GetComponent<PhotonView>();
                signature = signature * 31 + (view != null ? view.ViewID : 0);
                signature = signature * 31 + (player.IsWreck ? 1 : 0);
                signature = signature * 31 + (player.IsBotControlled ? 1 : 0);
                signature = signature * 31 + (player.IsAstronautControlled ? 1 : 0);

                if (view != null && view.Owner != null)
                    signature = signature * 31 + RoomSettings.GetPlayerShipSkin(view.Owner, 0);
            }

            for (int i = 0; i < treasures.Length; i++)
            {
                Treasure treasure = treasures[i];
                if (treasure == null)
                    continue;

                PhotonView view = treasure.GetComponent<PhotonView>();
                signature = signature * 31 + (view != null ? view.ViewID : treasure.GetHashCode());
                signature = signature * 31 + (string.IsNullOrWhiteSpace(treasure.itemId) ? 0 : treasure.itemId.GetHashCode());
            }

            for (int i = 0; i < wrecks.Length; i++)
            {
                ShipWreck wreck = wrecks[i];
                if (wreck == null)
                    continue;

                PhotonView view = wreck.GetComponent<PhotonView>();
                signature = signature * 31 + (view != null ? view.ViewID : wreck.GetHashCode());
                signature = signature * 31 + wreck.SourceShipSkinIndex;
                signature = signature * 31 + (wreck.HasLoot ? 1 : 0);
            }

            for (int i = 0; i < droppedCrates.Length; i++)
            {
                DroppedCargoCrate crate = droppedCrates[i];
                if (crate == null)
                    continue;

                PhotonView view = crate.GetComponent<PhotonView>();
                signature = signature * 31 + (view != null ? view.ViewID : crate.GetHashCode());
                signature = signature * 31 + (crate.HasLoot ? 1 : 0);
                signature = signature * 31 + (string.IsNullOrWhiteSpace(crate.StoredItemId) ? 0 : crate.StoredItemId.GetHashCode());
            }

            SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude);
            int obstacleCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].gameObject.name.StartsWith("Obstacle"))
                    obstacleCount++;
            }
            signature = signature * 31 + obstacleCount;
            return signature;
        }
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
        int clampedIndex = Mathf.Clamp(backgroundIndex, 1, 6);
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

