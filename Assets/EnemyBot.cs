using Photon.Pun;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum EnemyBotKind
{
    Drone,
    Corsair,
    SpaceMine
}

public enum EnemyMovementModel
{
    GuardAndChase,
    OrbitMap,
    Drift
}

public enum EnemySpawnPattern
{
    SafePerimeter,
    WideCorners
}

public enum EnemyTrailVisualStyle
{
    None,
    OrangeSmall,
    RedLarge
}

[System.Serializable]
public class EnemyExplosionProfile
{
    Sprite[] cachedFrames;

    public int Damage;
    public float TriggerRadius;
    public float VisualTargetSize;
    public float VisualDuration;
    public int VisualStartFrame;
    public int VisualColumns;
    public int VisualRows;
    public string VisualResourcePath;
    public string EditorAssetPath;
    public string SoundId;

    public Sprite[] GetVisualFrames()
    {
        if (cachedFrames != null && cachedFrames.Length > 0)
            return cachedFrames;

        if (!string.IsNullOrWhiteSpace(VisualResourcePath))
        {
            cachedFrames = Resources.LoadAll<Sprite>(VisualResourcePath);
            if (cachedFrames != null && cachedFrames.Length > 0)
            {
                SortSpritesForAnimation(cachedFrames);
                return cachedFrames;
            }

            Sprite single = Resources.Load<Sprite>(VisualResourcePath);
            if (single != null)
            {
                cachedFrames = new[] { single };
                return cachedFrames;
            }
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(EditorAssetPath))
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(EditorAssetPath);
            System.Collections.Generic.List<Sprite> sprites = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                    sprites.Add(sprite);
            }

            if (sprites.Count > 0)
            {
                sprites.Sort(CompareSpritesForAnimation);
                cachedFrames = sprites.ToArray();
                return cachedFrames;
            }

            Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>(EditorAssetPath);
            if (single != null)
            {
                cachedFrames = new[] { single };
                return cachedFrames;
            }
        }
#endif

        return System.Array.Empty<Sprite>();
    }

    static void SortSpritesForAnimation(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length <= 1)
            return;

        System.Array.Sort(sprites, CompareSpritesForAnimation);
    }

    static int CompareSpritesForAnimation(Sprite a, Sprite b)
    {
        string nameA = a != null ? a.name : string.Empty;
        string nameB = b != null ? b.name : string.Empty;

        int indexA = ExtractTrailingNumber(nameA);
        int indexB = ExtractTrailingNumber(nameB);
        bool hasIndexA = indexA >= 0;
        bool hasIndexB = indexB >= 0;

        if (hasIndexA && hasIndexB && indexA != indexB)
            return indexA.CompareTo(indexB);

        if (hasIndexA != hasIndexB)
            return hasIndexA ? -1 : 1;

        return string.CompareOrdinal(nameA, nameB);
    }

    static int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
            return -1;

        int endIndex = value.Length - 1;
        if (!char.IsDigit(value[endIndex]))
            return -1;

        int startIndex = endIndex;
        while (startIndex >= 0 && char.IsDigit(value[startIndex]))
            startIndex--;

        string numberPart = value.Substring(startIndex + 1, endIndex - startIndex);
        return int.TryParse(numberPart, out int number) ? number : -1;
    }
}

[System.Serializable]
public class EnemyTrailProfile
{
    public Vector2 RootOffsetFactors;
    public float RootRotationZ;
    public Vector2[] TrailOffsetFactors;
    public float MinTrailTime;
    public float MaxTrailTime;
    public float MinTrailWidth;
    public float MaxTrailWidth;
    public float EmissionThreshold;
    public EnemyTrailVisualStyle VisualStyle;
}

[System.Serializable]
public class EnemyMovementProfile
{
    public EnemyMovementModel Model;
    public EnemySpawnPattern SpawnPattern;
    public float MoveSpeed;
    public float TurnResponsiveness;
    public float DetectionRadius;
    public float DisengageRadius;
    public float OrbitDistance;
    public float PreferredDistance;
    public float ShootDistance;
    public float RepathInterval;
    public float TargetRefreshInterval;
    public float IdleDriftTurnSpeed;
    public float OrbitRadiusFactor;
    public float OrbitAngularSpeed;
}

[System.Serializable]
public class EnemyWeaponProfile
{
    public int AmmoCount;
    public float ReloadDuration;
    public float FireRate;
    public int Damage;
    public float BulletScaleMultiplier;
    public Color BulletColor;
    public float BulletSpeed;
    public float MuzzleOffsetDistance;
    public bool InfiniteAmmo;
    public bool RotateTowardAim;
    public float Range;
    public string ShotSoundId;
}

[System.Serializable]
public class EnemyWreckProfile
{
    Sprite cachedSprite;

    public float Mass;
    public float LinearDamping;
    public float AngularDamping;
    public float DriftSpeed;
    public float AngularVelocityRange;
    public string RewardItemId;
    public bool DestroyWhenEmpty;
    public Color BaseColor;
    public string VisualResourcePath;
    public string EditorAssetPath;

    public Sprite GetVisualSprite()
    {
        if (cachedSprite != null)
            return cachedSprite;

        if (!string.IsNullOrWhiteSpace(VisualResourcePath))
        {
            cachedSprite = Resources.Load<Sprite>(VisualResourcePath);
            if (cachedSprite != null)
                return cachedSprite;

            Sprite[] sprites = Resources.LoadAll<Sprite>(VisualResourcePath);
            cachedSprite = GetLargestSprite(sprites);
            if (cachedSprite != null)
                return cachedSprite;

            Texture2D texture = Resources.Load<Texture2D>(VisualResourcePath);
            if (texture != null)
            {
                float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
                cachedSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
                return cachedSprite;
            }
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(EditorAssetPath))
        {
            cachedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EditorAssetPath);
            if (cachedSprite != null)
                return cachedSprite;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(EditorAssetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    cachedSprite = sprite;
                    return cachedSprite;
                }
            }
        }
#endif

        return null;
    }

    static Sprite GetLargestSprite(Sprite[] sprites)
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
}

[System.Serializable]
public class EnemyBotDefinition
{
    Sprite cachedSprite;

    public EnemyBotKind Kind;
    public string Id;
    public string DisplayName;
    public string InstantiationMarker;
    public string VisualResourcePath;
    public string EditorAssetPath;
    public float TargetSize;
    public float PhysicsMass;
    public float LinearDamping;
    public float AngularDamping;
    public int DefaultHp;
    public int DefaultShield;
    public bool DefaultEnabled;
    public int DefaultCount;
    public int DefaultSpawnSecond;
    public EnemyMovementProfile Movement;
    public EnemyWeaponProfile Weapon;
    public EnemyWreckProfile Wreck;
    public EnemyTrailProfile Trails;
    public EnemyExplosionProfile Explosion;

    public string EnabledRoomKey => $"enemy.{Id}.enabled";
    public string CountRoomKey => $"enemy.{Id}.count";
    public string HpRoomKey => $"enemy.{Id}.hp";
    public string SpawnSecondRoomKey => $"enemy.{Id}.spawnSecond";
    public string RespawnEnabledRoomKey => $"enemy.{Id}.respawnEnabled";
    public string RespawnIntervalRoomKey => $"enemy.{Id}.respawnInterval";

    public Sprite GetVisualSprite()
    {
        if (cachedSprite != null)
            return cachedSprite;

        if (!string.IsNullOrWhiteSpace(VisualResourcePath))
        {
            cachedSprite = Resources.Load<Sprite>(VisualResourcePath);
            if (cachedSprite != null)
                return cachedSprite;

            Sprite[] sprites = Resources.LoadAll<Sprite>(VisualResourcePath);
            cachedSprite = GetLargestSprite(sprites);
            if (cachedSprite != null)
                return cachedSprite;

            Texture2D texture = Resources.Load<Texture2D>(VisualResourcePath);
            if (texture != null)
            {
                float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
                cachedSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
                return cachedSprite;
            }
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(EditorAssetPath))
        {
            cachedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EditorAssetPath);
            if (cachedSprite != null)
                return cachedSprite;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(EditorAssetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    cachedSprite = sprite;
                    return cachedSprite;
                }
            }
        }
#endif

        return null;
    }

    static Sprite GetLargestSprite(Sprite[] sprites)
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
}

public static class EnemyBotCatalog
{
    static readonly EnemyBotDefinition[] Definitions =
    {
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.Drone,
            Id = "drone",
            DisplayName = "Drone",
            InstantiationMarker = "enemy_bot",
            VisualResourcePath = "droid1_resource",
            EditorAssetPath = "Assets/droid1.png",
            TargetSize = 1.04f,
            PhysicsMass = 2.8f,
            LinearDamping = 0.08f,
            AngularDamping = 0.22f,
            DefaultHp = 50,
            DefaultShield = 50,
            DefaultEnabled = true,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.GuardAndChase,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.1f,
                TurnResponsiveness = 300f,
                DetectionRadius = 10f,
                DisengageRadius = 20f,
                OrbitDistance = 5.5f,
                PreferredDistance = 7.5f,
                ShootDistance = 12f,
                RepathInterval = 0.35f,
                TargetRefreshInterval = 0.45f,
                IdleDriftTurnSpeed = 18f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 10,
                ReloadDuration = 6f,
                FireRate = 0.15f,
                Damage = 10,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 10f,
                MuzzleOffsetDistance = 0.5f,
                InfiniteAmmo = false,
                RotateTowardAim = true,
                Range = 12f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 4.6f,
                LinearDamping = 0.56f,
                AngularDamping = 0.72f,
                DriftSpeed = 0.12f,
                AngularVelocityRange = 4f,
                RewardItemId = InventoryItemCatalog.DroidScrapId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.2f, 0.23f, 0.26f, 0.94f)
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, 0.44f),
                RootRotationZ = 0f,
                TrailOffsetFactors = new[] { new Vector2(0f, 0.02f) },
                MinTrailTime = 0.22f,
                MaxTrailTime = 0.82f,
                MinTrailWidth = 0.03f,
                MaxTrailWidth = 0.16f,
                EmissionThreshold = 0.04f,
                VisualStyle = EnemyTrailVisualStyle.OrangeSmall
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.Corsair,
            Id = "corsair",
            DisplayName = "Corsair",
            InstantiationMarker = "enemy_bot_corsair",
            VisualResourcePath = "statek_duzy_resource",
            EditorAssetPath = "Assets/statek_duzy.png",
            TargetSize = 5.2f,
            PhysicsMass = 24f,
            LinearDamping = 0.16f,
            AngularDamping = 0.38f,
            DefaultHp = 200,
            DefaultShield = 0,
            DefaultEnabled = true,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.OrbitMap,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 1.9f,
                TurnResponsiveness = 150f,
                DetectionRadius = 7f,
                OrbitRadiusFactor = 0.43f,
                OrbitAngularSpeed = 0.32f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 9999,
                ReloadDuration = 0f,
                FireRate = 1f,
                Damage = 20,
                BulletScaleMultiplier = 2f,
                BulletColor = new Color(0.15f, 1f, 0.28f, 1f),
                BulletSpeed = 9f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 7f,
                ShotSoundId = "corsair"
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 22f,
                LinearDamping = 0.84f,
                AngularDamping = 1.05f,
                DriftSpeed = 0.07f,
                AngularVelocityRange = 1.5f,
                RewardItemId = InventoryItemCatalog.CorsairSalvageId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.17f, 0.15f, 0.16f, 0.96f),
                VisualResourcePath = "wrak_corsair_resource",
                EditorAssetPath = "Assets/wrak_corsair.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.62f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.76f, 0.08f),
                    new Vector2(0f, 0.2f),
                    new Vector2(0.76f, 0.08f)
                },
                MinTrailTime = 0.65f,
                MaxTrailTime = 1.55f,
                MinTrailWidth = 0.12f,
                MaxTrailWidth = 0.34f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.RedLarge
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.SpaceMine,
            Id = "space_mine",
            DisplayName = "Space Mine",
            InstantiationMarker = "enemy_bot_space_mine",
            VisualResourcePath = "space_mine_resource",
            EditorAssetPath = "Assets/space mine.png",
            TargetSize = 1.08f,
            PhysicsMass = 3.8f,
            LinearDamping = 0.18f,
            AngularDamping = 0.42f,
            DefaultHp = 20,
            DefaultShield = 0,
            DefaultEnabled = true,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.Drift,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 0.18f,
                TurnResponsiveness = 20f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 3.6f,
                LinearDamping = 0.78f,
                AngularDamping = 0.96f,
                DriftSpeed = 0.09f,
                AngularVelocityRange = 2.4f,
                RewardItemId = InventoryItemCatalog.SpaceMineWreckId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.19f, 0.21f, 0.24f, 0.96f),
                VisualResourcePath = "wrak_miny_resource",
                EditorAssetPath = "Assets/wrak_miny.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = Vector2.zero,
                RootRotationZ = 0f,
                TrailOffsetFactors = System.Array.Empty<Vector2>(),
                MinTrailTime = 0f,
                MaxTrailTime = 0f,
                MinTrailWidth = 0f,
                MaxTrailWidth = 0f,
                EmissionThreshold = 1f,
                VisualStyle = EnemyTrailVisualStyle.None
            },
            Explosion = new EnemyExplosionProfile
            {
                Damage = 50,
                TriggerRadius = 2.08f,
                VisualTargetSize = 4.1f,
                VisualDuration = 1.25f,
                VisualStartFrame = 2,
                VisualColumns = 4,
                VisualRows = 6,
                VisualResourcePath = "",
                EditorAssetPath = "",
                SoundId = "space_mine_boom"
            }
        }
    };

    static readonly System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition> DefinitionsByKind = BuildDefinitionsByKind();
    static readonly System.Collections.Generic.Dictionary<string, EnemyBotDefinition> DefinitionsByMarker = BuildDefinitionsByMarker();

    public static System.Collections.Generic.IReadOnlyList<EnemyBotDefinition> AllDefinitions => Definitions;

    public static EnemyBotDefinition GetDefinition(EnemyBotKind kind)
    {
        DefinitionsByKind.TryGetValue(kind, out EnemyBotDefinition definition);
        return definition;
    }

    public static EnemyBotDefinition GetDefinition(string marker)
    {
        if (string.IsNullOrWhiteSpace(marker))
            return null;

        DefinitionsByMarker.TryGetValue(marker, out EnemyBotDefinition definition);
        return definition;
    }

    static System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition> BuildDefinitionsByKind()
    {
        System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition> result = new System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition>();
        for (int i = 0; i < Definitions.Length; i++)
            result[Definitions[i].Kind] = Definitions[i];

        return result;
    }

    static System.Collections.Generic.Dictionary<string, EnemyBotDefinition> BuildDefinitionsByMarker()
    {
        System.Collections.Generic.Dictionary<string, EnemyBotDefinition> result = new System.Collections.Generic.Dictionary<string, EnemyBotDefinition>(System.StringComparer.Ordinal);
        for (int i = 0; i < Definitions.Length; i++)
            result[Definitions[i].InstantiationMarker] = Definitions[i];

        return result;
    }
}

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBot : MonoBehaviourPun
{
    const string PlayerPlacedMineMarker = "player_gadget_mine";

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyBotBehaviorBase behavior;
    SpriteRenderer cachedRenderer;
    EnemyBotKind kind;
    bool hasInitialized;
    bool hasAppliedStats;
    bool hasDetonated;
    bool isPlayerPlacedMine;
    int mineOwnerViewId;

    public EnemyBotKind Kind => kind;
    public EnemyBotDefinition Definition => EnemyBotCatalog.GetDefinition(kind);
    public bool IsCorsair => kind == EnemyBotKind.Corsair;
    public bool IsSpaceMine => kind == EnemyBotKind.SpaceMine;
    public bool IsPlayerPlacedMine => isPlayerPlacedMine;
    public int MineOwnerViewId => mineOwnerViewId;
    public float VisualTargetSize => Definition != null ? Definition.TargetSize : 1.04f;

    public static bool IsBotObject(GameObject target)
    {
        return target != null && target.GetComponent<EnemyBot>() != null;
    }

    public static bool IsBotView(PhotonView targetView)
    {
        return targetView != null && targetView.GetComponent<EnemyBot>() != null;
    }

    public static bool IsBotInstantiationData(object[] data)
    {
        return GetDefinitionFromInstantiationData(data) != null;
    }

    public static EnemyBotKind GetKindFromInstantiationData(object[] data)
    {
        EnemyBotDefinition definition = GetDefinitionFromInstantiationData(data);
        return definition != null ? definition.Kind : EnemyBotKind.Drone;
    }

    static EnemyBotDefinition GetDefinitionFromInstantiationData(object[] data)
    {
        if (data == null ||
            data.Length == 0 ||
            !(data[0] is string marker))
        {
            return null;
        }

        return EnemyBotCatalog.GetDefinition(marker);
    }

    public void InitializeFromPhotonData()
    {
        if (hasInitialized)
            return;

        view = GetComponent<PhotonView>();
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
        kind = GetKindFromInstantiationData(view != null ? view.InstantiationData : null);
        ResolveSpecialMineOwner(view != null ? view.InstantiationData : null);

        DisablePlayerOnlySystems();
        EnsureBehavior();
        ApplyBotVisuals();
        ConfigurePhysics();
        ApplyMineOwnerCollisionIgnore();
        if (!hasAppliedStats)
        {
            ApplyBotStats();
            hasAppliedStats = true;
        }

        hasInitialized = true;
    }

    void Awake()
    {
        InitializeFromPhotonData();
    }

    void Start()
    {
        InitializeFromPhotonData();
    }

    void Update()
    {
        EnsureStableVisuals();
        if (isPlayerPlacedMine)
            ApplyMineOwnerCollisionIgnore();

        if (!view.IsMine || !IsGameStarted() || health == null || health.IsWreck)
            return;

        if (behavior == null)
            EnsureBehavior();
    }

    void FixedUpdate()
    {
        if (!view.IsMine || !IsGameStarted() || health == null || health.IsWreck)
            return;

        if (!hasInitialized)
            InitializeFromPhotonData();

        if (behavior == null)
            EnsureBehavior();

        behavior?.TickBehavior();
    }

    void ConfigurePhysics()
    {
        if (rb == null || Definition == null)
            return;

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.mass = Definition.PhysicsMass;
        rb.linearDamping = Definition.LinearDamping;
        rb.angularDamping = Definition.AngularDamping;
    }

    void ApplyBotStats()
    {
        if (health == null || Definition == null)
            return;

        health.ConfigureBaseStats(RoomSettings.GetEnemyHp(kind), Definition.DefaultShield);
    }

    void EnsureBehavior()
    {
        behavior = GetComponent<EnemyBotBehaviorBase>();
        if (behavior == null || !IsMatchingBehavior(behavior))
        {
            if (behavior != null)
                Destroy(behavior);

            if (Definition != null && Definition.Movement != null)
            {
                switch (Definition.Movement.Model)
                {
                    case EnemyMovementModel.OrbitMap:
                        behavior = gameObject.AddComponent<EnemyCorsairBehavior>();
                        break;
                    case EnemyMovementModel.Drift:
                        behavior = gameObject.AddComponent<EnemyMineBehavior>();
                        break;
                    default:
                        behavior = gameObject.AddComponent<EnemyDroneBehavior>();
                        break;
                }
            }
            else
            {
                behavior = gameObject.AddComponent<EnemyDroneBehavior>();
            }
        }

        behavior.Initialize(this);
    }

    bool IsMatchingBehavior(EnemyBotBehaviorBase existingBehavior)
    {
        if (existingBehavior == null || Definition == null || Definition.Movement == null)
            return false;

        return Definition.Movement.Model switch
        {
            EnemyMovementModel.OrbitMap => existingBehavior is EnemyCorsairBehavior,
            EnemyMovementModel.Drift => existingBehavior is EnemyMineBehavior,
            _ => existingBehavior is EnemyDroneBehavior
        };
    }

    void DisablePlayerOnlySystems()
    {
        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.enabled = false;

        ShipInventoryHudUI cargoHud = GetComponent<ShipInventoryHudUI>();
        if (cargoHud != null)
            cargoHud.enabled = false;

        AmmoUI ammoUi = GetComponent<AmmoUI>();
        if (ammoUi != null)
            ammoUi.enabled = false;

        BoosterBarUI boosterUi = GetComponent<BoosterBarUI>();
        if (boosterUi != null)
            boosterUi.enabled = false;

        ReloadButtonUI reloadUi = GetComponent<ReloadButtonUI>();
        if (reloadUi != null)
            reloadUi.enabled = false;
    }

    void ApplyBotVisuals()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        cachedRenderer = renderer;
        Sprite sprite = GetVisualSprite();
        if (sprite == null)
            return;

        renderer.sprite = sprite;
        renderer.color = Color.white;
        FitRendererToTargetSize(renderer, VisualTargetSize);
    }

    void EnsureStableVisuals()
    {
        if (health != null && health.IsWreck)
            return;

        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        if (cachedRenderer == null)
            return;

        Sprite desiredSprite = GetVisualSprite();
        if (desiredSprite != null && cachedRenderer.sprite != desiredSprite)
            cachedRenderer.sprite = desiredSprite;

        cachedRenderer.color = Color.white;
        FitRendererToTargetSize(cachedRenderer, VisualTargetSize);
    }

    public Sprite GetVisualSprite()
    {
        return Definition != null ? Definition.GetVisualSprite() : null;
    }

    public bool HasCustomDeathExplosion()
    {
        return Definition != null && Definition.Explosion != null;
    }

    public void RequestDetonation()
    {
        if (hasDetonated || !PhotonNetwork.IsMasterClient || Definition == null || Definition.Explosion == null)
            return;

        hasDetonated = true;
        Vector3 detonationPosition = GetVisualCenterWorldPosition();
        DetonateNearbyTargets(Definition.Explosion);
        SpaceObjectMotionSync.BroadcastSpaceMineDetonation(photonView != null ? photonView.ViewID : 0, detonationPosition);
        PhotonNetwork.Destroy(gameObject);
    }

    void DetonateNearbyTargets(EnemyExplosionProfile explosion)
    {
        if (explosion == null)
            return;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating)
                continue;

            EnemyBot candidateBot = candidate.GetComponent<EnemyBot>();
            if (candidateBot != null && candidateBot.Kind == EnemyBotKind.SpaceMine)
                continue;

            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            if (distance > explosion.TriggerRadius)
                continue;

            PhotonView targetView = candidate.GetComponent<PhotonView>();
            if (targetView != null)
                targetView.RPC(nameof(PlayerHealth.TakeDamage), RpcTarget.MasterClient, explosion.Damage, photonView.ViewID);
        }
    }

    [PunRPC]
    void PlayMineDetonationEffects(float x, float y, float z)
    {
        SpawnSpaceMineDetonationEffects(new Vector3(x, y, z));
    }

    bool IsGameStarted()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started)
        {
            return started;
        }

        return false;
    }

    void FitRendererToTargetSize(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds spriteBounds = renderer.sprite.bounds;
        float largestDimension = Mathf.Max(spriteBounds.size.x, spriteBounds.size.y);
        if (largestDimension <= 0.0001f)
            return;

        float scale = targetSize / largestDimension;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    Vector3 GetVisualCenterWorldPosition()
    {
        if (cachedRenderer != null)
            return cachedRenderer.bounds.center;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
            return renderer.bounds.center;

        return transform.position;
    }

    void ResolveSpecialMineOwner(object[] instantiationData)
    {
        isPlayerPlacedMine = false;
        mineOwnerViewId = 0;

        if (kind != EnemyBotKind.SpaceMine || instantiationData == null || instantiationData.Length < 3)
            return;

        if (!(instantiationData[1] is string marker) ||
            !string.Equals(marker, PlayerPlacedMineMarker, System.StringComparison.Ordinal))
        {
            return;
        }

        if (instantiationData[2] is int ownerViewId && ownerViewId > 0)
        {
            isPlayerPlacedMine = true;
            mineOwnerViewId = ownerViewId;
        }
    }

    void ApplyMineOwnerCollisionIgnore()
    {
        if (!isPlayerPlacedMine || mineOwnerViewId <= 0)
            return;

        PhotonView ownerView = PhotonView.Find(mineOwnerViewId);
        if (ownerView == null)
            return;

        Collider2D[] mineColliders = GetComponentsInChildren<Collider2D>(true);
        Collider2D[] ownerColliders = ownerView.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < mineColliders.Length; i++)
        {
            Collider2D mineCollider = mineColliders[i];
            if (mineCollider == null)
                continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider2D ownerCollider = ownerColliders[j];
                if (ownerCollider == null)
                    continue;

                Physics2D.IgnoreCollision(mineCollider, ownerCollider, true);
            }
        }
    }

    public bool ShouldIgnoreMineTriggerFor(PlayerHealth candidate)
    {
        if (!isPlayerPlacedMine || candidate == null || mineOwnerViewId <= 0)
            return false;

        PhotonView candidateView = candidate.GetComponent<PhotonView>();
        return candidateView != null && candidateView.ViewID == mineOwnerViewId;
    }

    public static Vector3 ResolveSpaceMineDetonationPosition(int sourceViewId, Vector3 fallbackWorldPosition)
    {
        if (sourceViewId > 0)
        {
            PhotonView sourceView = PhotonView.Find(sourceViewId);
            if (sourceView != null)
            {
                EnemyBot bot = sourceView.GetComponent<EnemyBot>();
                if (bot != null)
                    return bot.GetVisualCenterWorldPosition();

                return sourceView.transform.position;
            }
        }

        return fallbackWorldPosition;
    }

    public static void SpawnSpaceMineDetonationEffects(Vector3 worldPosition)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(EnemyBotKind.SpaceMine);
        EnemyExplosionProfile explosion = definition != null ? definition.Explosion : null;
        if (explosion == null)
            return;

        if (explosion.SoundId == "space_mine_boom")
            AudioManager.Instance.PlaySpaceMineBoomAt(worldPosition);
        else
            AudioManager.Instance.PlayExplosionAt(worldPosition);
    }
}

public abstract class EnemyBotBehaviorBase : MonoBehaviour
{
    protected EnemyBot bot;

    public virtual void Initialize(EnemyBot owner)
    {
        bot = owner;
    }

    public abstract void TickBehavior();
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyDroneBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    float nextTargetRefreshTime;
    float nextRepathTime;
    Vector2 currentMoveDirection = Vector2.up;
    Transform currentTarget;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                Mathf.Max(weapon.AmmoCount, 1),
                weapon.ReloadDuration,
                weapon.Damage,
                weapon.BulletScaleMultiplier,
                weapon.BulletColor,
                weapon.MuzzleOffsetDistance,
                weapon.InfiniteAmmo,
                weapon.BulletSpeed,
                weapon.ShotSoundId,
                weapon.Range);
        }
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        if (Time.time >= nextTargetRefreshTime)
        {
            nextTargetRefreshTime = Time.time + movement.TargetRefreshInterval;
            currentTarget = ResolveTarget();
        }

        if (currentTarget == null)
        {
            ApplyIdleDrift();
            return;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + movement.RepathInterval;
            currentMoveDirection = CalculateMoveDirection(currentTarget.position);
        }

        Vector2 desiredVelocity = currentMoveDirection * movement.MoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.14f);

        Vector2 aimDirection = (Vector2)currentTarget.position - rb.position;
        if (weapon != null && weapon.RotateTowardAim && aimDirection.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg + 90f;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }

        TryShootAtTarget(aimDirection);
    }

    void ApplyIdleDrift()
    {
        if (rb.linearVelocity.sqrMagnitude < 0.05f)
        {
            Vector2 fallback = currentMoveDirection.sqrMagnitude > 0.001f ? currentMoveDirection : Vector2.up;
            rb.linearVelocity = fallback.normalized * (movement.MoveSpeed * 0.36f);
        }

        float spin = Mathf.Sin(Time.time * 0.45f + view.ViewID * 0.23f) * movement.IdleDriftTurnSpeed;
        rb.MoveRotation(rb.rotation + spin * Time.fixedDeltaTime);
    }

    Transform ResolveTarget()
    {
        if (currentTarget != null)
        {
            PlayerHealth currentHealth = currentTarget.GetComponent<PlayerHealth>();
            if (IsValidVisibleTarget(currentHealth, movement.DisengageRadius))
                return currentTarget;
        }

        return FindClosestVisibleHumanTarget(movement.DetectionRadius);
    }

    Transform FindClosestVisibleHumanTarget(float maxDistance)
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        Transform bestTarget = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate == null || candidate == health || candidate.IsWreck || candidate.IsBotControlled)
                continue;

            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            if (distance > maxDistance || distance >= bestDistance)
                continue;

            if (!IsValidVisibleTarget(candidate, maxDistance))
                continue;

            bestDistance = distance;
            bestTarget = candidate.transform;
        }

        return bestTarget;
    }

    bool IsValidVisibleTarget(PlayerHealth candidate, float maxDistance)
    {
        if (candidate == null || candidate == health || candidate.IsWreck || candidate.IsBotControlled)
            return false;

        HideInNebulaTarget nebulaState = candidate.GetComponent<HideInNebulaTarget>();
        if (nebulaState != null && nebulaState.IsHiddenForOthers)
            return false;

        float distance = Vector2.Distance(transform.position, candidate.transform.position);
        return distance <= maxDistance;
    }

    Vector2 CalculateMoveDirection(Vector2 targetPosition)
    {
        Vector2 toTarget = targetPosition - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return currentMoveDirection;

        Vector2 towardTarget = toTarget / distance;
        Vector2 orbitDirection = new Vector2(-towardTarget.y, towardTarget.x);
        if (Mathf.Sin(Time.time * 0.6f + view.ViewID * 0.27f) < 0f)
            orbitDirection *= -1f;

        Vector2 result;
        if (distance > movement.PreferredDistance)
            result = towardTarget * 0.84f + orbitDirection * 0.28f;
        else if (distance < movement.OrbitDistance)
            result = -towardTarget * 0.72f + orbitDirection * 0.52f;
        else
            result = orbitDirection * 0.85f + towardTarget * 0.18f;

        return result.normalized;
    }

    void TryShootAtTarget(Vector2 aimDirection)
    {
        if (shooting == null || weapon == null || aimDirection.sqrMagnitude <= 0.001f)
            return;

        if (aimDirection.magnitude > weapon.Range)
            return;

        if (weapon.RotateTowardAim)
        {
            Vector2 normalizedAim = aimDirection.normalized;
            float facingDot = Vector2.Dot(-transform.up, normalizedAim);
            if (facingDot < 0.9f)
                return;
        }

        shooting.TryFireBot(aimDirection.normalized);
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyCorsairBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    Vector2 orbitCenter;
    float orbitRadius;
    float orbitAngle;
    float orbitDirection;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        orbitCenter = Vector2.zero;
        orbitRadius = Mathf.Max(6f, Mathf.Min(mapSize.x, mapSize.y) * movement.OrbitRadiusFactor);
        int orbitSeed = view != null ? view.ViewID : 0;
        orbitAngle = Mathf.Abs(orbitSeed * 0.137f) % (Mathf.PI * 2f);
        orbitDirection = (orbitSeed % 2 == 0) ? 1f : -1f;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                9999,
                weapon.ReloadDuration,
                weapon.Damage,
                weapon.BulletScaleMultiplier,
                weapon.BulletColor,
                weapon.MuzzleOffsetDistance,
                weapon.InfiniteAmmo,
                weapon.BulletSpeed,
                weapon.ShotSoundId,
                weapon.Range);
        }
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        orbitAngle += orbitDirection * movement.OrbitAngularSpeed * Time.fixedDeltaTime;
        Vector2 fromCenter = rb.position - orbitCenter;
        if (fromCenter.sqrMagnitude < 0.01f)
            fromCenter = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle));

        Vector2 radialDirection = fromCenter.normalized;
        Vector2 tangentDirection = orbitDirection > 0f
            ? new Vector2(-radialDirection.y, radialDirection.x)
            : new Vector2(radialDirection.y, -radialDirection.x);

        float radialError = orbitRadius - fromCenter.magnitude;
        Vector2 desiredVelocity = tangentDirection * movement.MoveSpeed + radialDirection * (radialError * 1.35f);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.16f);

        if (desiredVelocity.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(desiredVelocity.y, desiredVelocity.x) * Mathf.Rad2Deg + 270f;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }

        TryShootNearestTarget();
    }

    void TryShootNearestTarget()
    {
        if (shooting == null || weapon == null)
            return;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        Transform bestTarget = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate == null || candidate == health || candidate.IsWreck || candidate.IsBotControlled)
                continue;

            HideInNebulaTarget nebulaState = candidate.GetComponent<HideInNebulaTarget>();
            if (nebulaState != null && nebulaState.IsHiddenForOthers)
                continue;

            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            if (distance > weapon.Range || distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestTarget = candidate.transform;
        }

        if (bestTarget == null)
            return;

        Vector2 shootDirection = bestTarget.position - transform.position;
        if (shootDirection.sqrMagnitude <= 0.01f)
            return;

        shooting.TryFireBot(shootDirection.normalized);
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyMineBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyExplosionProfile explosion;
    Vector2 driftDirection;
    float nextRetargetTime;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        explosion = owner.Definition != null ? owner.Definition.Explosion : null;

        if (driftDirection.sqrMagnitude <= 0.001f)
        {
            int seed = view != null ? view.ViewID : Random.Range(1, 9999);
            float angle = Mathf.Abs(seed * 0.173f) % (Mathf.PI * 2f);
            driftDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (driftDirection.sqrMagnitude <= 0.001f)
                driftDirection = Vector2.up;
        }
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null || explosion == null)
            return;

        if (health != null && health.IsWreck)
            return;

        Vector2 desiredVelocity = driftDirection.normalized * movement.MoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.08f);
        rb.angularVelocity = Mathf.Lerp(rb.angularVelocity, 8f, 0.04f);

        if (Time.time >= nextRetargetTime)
        {
            nextRetargetTime = Time.time + 0.12f;
            if (IsAnyTargetInRange(explosion.TriggerRadius))
                bot.RequestDetonation();
        }
    }

    bool IsAnyTargetInRange(float radius)
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating)
                continue;

            if (bot != null && bot.ShouldIgnoreMineTriggerFor(candidate))
                continue;

            EnemyBot candidateBot = candidate.GetComponent<EnemyBot>();
            if (candidateBot != null && candidateBot.Kind == EnemyBotKind.SpaceMine)
                continue;

            if (Vector2.Distance(transform.position, candidate.transform.position) <= radius)
                return true;
        }

        return false;
    }
}
