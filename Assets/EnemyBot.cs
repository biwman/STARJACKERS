using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBot : MonoBehaviourPun
{
    const string PlayerPlacedMineMarker = "player_gadget_mine";
    public const string ContainerShipMineMarker = "container_ship_mine";
    const string SummonedDroneMarker = "space_truck_summoned_drone";
    public const string PirateBaseLaunchedFighterMarker = "pirate_base_launched_fighter";
    const float PirateBaseLaunchAnimationDuration = 3f;
    const float PirateBaseLaunchStartScale = 0.3f;
    const float PirateBaseLaunchExitDistance = 1.15f;

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyBotBehaviorBase behavior;
    SpriteRenderer cachedRenderer;
    EnemyBotKind kind;
    bool hasInitialized;
    bool hasAppliedStats;
    bool hasDetonated;
    bool spawnTeleportVfxPlayed;
    bool isPlayerPlacedMine;
    bool isOwnedMine;
    bool isSummonedDrone;
    bool isPirateBaseLaunchedFighter;
    bool spaceTruckFirstHitHandled;
    bool spaceTruckHalfHpHandled;
    bool pirateBaseLaunchProtected;
    bool pirateBaseLaunchBodyStateStored;
    bool pirateBaseLaunchColliderStateStored;
    float forcedSpeedMultiplier;
    float forcedSpeedMultiplierUntil;
    float visualScaleMultiplier = 1f;
    float pirateBaseLaunchStartedAt;
    RigidbodyType2D pirateBaseLaunchPreviousBodyType;
    bool pirateBaseLaunchPreviousSimulated;
    Collider2D[] pirateBaseLaunchColliders;
    bool[] pirateBaseLaunchColliderStates;
    Vector3 pirateBaseLaunchStartPosition;
    Vector3 pirateBaseLaunchEndPosition;
    int mineOwnerViewId;
    int containerShipCargoVariantIndex;
    int pirateBaseLaunchTargetViewId;
    int pirateBaseLaunchSourceViewId;
    int cosmicWormSwallowZoomToken;
    float confusedUntil;
    float nextConfusedDirectionAt;
    float nextConfusedShotAt;
    Vector2 confusedMoveDirection = Vector2.up;

    public EnemyBotKind Kind => kind;
    public EnemyBotDefinition Definition => EnemyBotCatalog.GetDefinition(kind);
    public bool IsCorsair => kind == EnemyBotKind.Corsair;
    public bool IsSpaceMine => kind == EnemyBotKind.SpaceMine;
    public bool IsSpaceTruck => kind == EnemyBotKind.SpaceTruck;
    public bool IsRadarShip => kind == EnemyBotKind.RadarShip;
    public bool IsRescueShip => kind == EnemyBotKind.RescueShip;
    public bool IsPirateFighter => IsPirateFighterKind(kind);
    public bool IsMothership => kind == EnemyBotKind.Mothership;
    public bool IsPirateBase => kind == EnemyBotKind.PirateBase;
    public bool IsCosmicWorm => kind == EnemyBotKind.CosmicWorm;
    public bool IsRiftWarden => kind == EnemyBotKind.RiftWarden;
    public bool IsMilitaryVan => kind == EnemyBotKind.MilitaryVan;
    public bool IsPlayerPlacedMine => isPlayerPlacedMine;
    public bool IsOwnedMine => isOwnedMine;
    public bool IsSummonedDrone => isSummonedDrone;
    public bool IsPirateBaseLaunchedFighter => isPirateBaseLaunchedFighter;
    public bool IsPirateBaseLaunchProtected => pirateBaseLaunchProtected;
    public int MineOwnerViewId => mineOwnerViewId;
    public int ContainerShipCargoVariantIndex => Mathf.Clamp(containerShipCargoVariantIndex, 0, InventoryItemCatalog.BlueprintScrapContainerVariantCount - 1);
    public int PirateBaseLaunchTargetViewId => pirateBaseLaunchTargetViewId;
    public int PirateBaseLaunchSourceViewId => pirateBaseLaunchSourceViewId;
    public bool IsConfused => Time.time < confusedUntil;
    public float VisualTargetSize => Definition != null ? Definition.TargetSize : 1.04f;
    float EnemyVisualScaleMultiplier => RoomSettings.GetEnemyVisualScaleMultiplier();
    public float EffectiveMoveSpeed => Definition != null && Definition.Movement != null
        ? Definition.Movement.MoveSpeed * EffectiveSpeedMultiplier * NebulaSpeedMultiplier * ElectromagneticShockStatus.GetSpeedMultiplier(gameObject) * AtlasSuppressionStatus.GetSpeedMultiplier(gameObject)
        : 1f;
    public float EffectiveSpeedMultiplier => forcedSpeedMultiplier > 0f && (forcedSpeedMultiplierUntil <= 0f || Time.time < forcedSpeedMultiplierUntil)
        ? forcedSpeedMultiplier
        : RoomSettings.GetEnemySpeedMultiplier(kind);
    float NebulaSpeedMultiplier
    {
        get
        {
            HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>();
            return nebulaTarget != null ? nebulaTarget.CurrentNebulaSpeedMultiplier : 1f;
        }
    }

    public static bool IsPlayerControlledDamageSource(int attackerViewID)
    {
        if (attackerViewID <= 0)
            return false;

        PhotonView attackerView = PhotonView.Find(attackerViewID);
        if (attackerView == null)
            return false;

        PlayerHealth attackerHealth = attackerView.GetComponent<PlayerHealth>();
        if (attackerHealth != null &&
            !attackerHealth.IsBotControlled &&
            !attackerHealth.IsNeutralRiderControlled &&
            !attackerHealth.IsAstronautControlled &&
            attackerHealth.GetComponent<PlayerDeployableBase>() == null &&
            attackerHealth.GetComponent<LureBeaconDecoy>() == null)
        {
            return true;
        }

        PlayerDeployableBase deployable = attackerView.GetComponent<PlayerDeployableBase>();
        if (deployable != null)
            return deployable.OwnerShipViewId != attackerViewID &&
                   IsPlayerControlledDamageSource(deployable.OwnerShipViewId);

        EnemyBot attackerBot = attackerView.GetComponent<EnemyBot>();
        return attackerBot != null && attackerBot.Kind == EnemyBotKind.SpaceMine && attackerBot.IsPlayerPlacedMine;
    }

    public static bool IsPirateFighterKind(EnemyBotKind candidate)
    {
        return candidate == EnemyBotKind.PirateFighter ||
               candidate == EnemyBotKind.PirateFighterElite ||
               candidate == EnemyBotKind.PirateFighterAce;
    }

    public static bool IsSpaceAnimalKind(EnemyBotKind candidate)
    {
        return candidate == EnemyBotKind.SpaceManta ||
               candidate == EnemyBotKind.GravitySquid;
    }

    public void ActivateTemporarySpeedMultiplier(float multiplier, float duration)
    {
        forcedSpeedMultiplier = Mathf.Max(0f, multiplier);
        forcedSpeedMultiplierUntil = duration > 0f ? Time.time + duration : 0f;
    }

    public static bool IsBotObject(GameObject target)
    {
        return target != null && target.GetComponent<EnemyBot>() != null;
    }

    public static bool IsBotView(PhotonView targetView)
    {
        return targetView != null && targetView.GetComponent<EnemyBot>() != null;
    }

    public bool CanReceivePilotHostileEffect()
    {
        if (!hasInitialized)
            InitializeFromPhotonData();

        return health != null &&
               !health.IsWreck &&
               kind != EnemyBotKind.SpaceMine &&
               kind != EnemyBotKind.RescueShip &&
               !isOwnedMine &&
               !isSummonedDrone;
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
        ResolveContainerShipCargoVariant(view != null ? view.InstantiationData : null);
        ResolveSummonedDrone(view != null ? view.InstantiationData : null);
        ResolvePirateBaseLaunchedFighter(view != null ? view.InstantiationData : null);

        DisablePlayerOnlySystems();
        EnsureBehavior();
        ApplyBotVisuals();
        EnsureAnimatedVisual();
        PlaySpawnTeleportVfx();
        ConfigurePhysics();
        ConfigureColliderToVisual();
        ApplyMineOwnerCollisionIgnore();
        if (GetComponent<EnemyBotHealthBarUI>() == null)
        {
            gameObject.AddComponent<EnemyBotHealthBarUI>();
        }
        if (!hasAppliedStats)
        {
            ApplyBotStats();
            hasAppliedStats = true;
        }
        if (isPirateBaseLaunchedFighter)
            BeginPirateBaseLaunchAnimation();

        hasInitialized = true;
    }

    void PlaySpawnTeleportVfx()
    {
        if (spawnTeleportVfxPlayed || !IsGameStarted() || isOwnedMine || isPirateBaseLaunchedFighter || kind == EnemyBotKind.RescueShip)
            return;

        spawnTeleportVfxPlayed = true;
        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        float radius = VisualTargetSize * 0.62f;
        if (cachedRenderer != null)
            radius = Mathf.Max(radius, Mathf.Max(cachedRenderer.bounds.size.x, cachedRenderer.bounds.size.y) * 0.58f);

        EnemySpawnTeleportVfx.Spawn(transform.position, cachedRenderer, radius);
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
        UpdatePirateBaseLaunchAnimation();
        if (isOwnedMine)
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

        if (pirateBaseLaunchProtected)
        {
            UpdatePirateBaseLaunchAnimation();
            return;
        }

        if (IsConfused)
        {
            ApplyConfusedBehavior();
            return;
        }

        behavior?.TickBehavior();
        ApplyToxicBorderVelocityCorrection();
    }

    void ApplyToxicBorderVelocityCorrection()
    {
        if (!RoomSettings.AreToxicBordersEnabled() || rb == null || isOwnedMine || IsSpaceMine || IsPirateBase)
            return;

        Vector2 currentVelocity = rb.linearVelocity;
        Vector2 desiredDirection = currentVelocity.sqrMagnitude > 0.001f
            ? currentVelocity.normalized
            : (Vector2)transform.up;
        float bodyRadius = Mathf.Clamp(VisualTargetSize * 0.52f, 0.35f, 2.4f);
        float lookAhead = Mathf.Clamp(currentVelocity.magnitude * 0.55f, 1.1f, 4.4f);
        if (!RoomSettings.TryGetEnemyToxicBorderAvoidance(rb.position, desiredDirection, lookAhead, bodyRadius, out _, out float strength))
            return;

        float currentSpeed = currentVelocity.magnitude;
        float targetSpeed = Mathf.Max(currentSpeed, EffectiveMoveSpeed * Mathf.Lerp(0.42f, 1f, strength));
        Vector2 targetVelocity = RoomSettings.ApplyEnemyToxicBorderSteering(
            rb.position,
            desiredDirection,
            lookAhead,
            bodyRadius,
            1.2f) * targetSpeed;
        float blend = Mathf.Lerp(0.16f, 0.64f, strength);
        rb.linearVelocity = Vector2.Lerp(currentVelocity, targetVelocity, blend);
    }

    [PunRPC]
    public void ApplyConfusionRpc(float duration)
    {
        if (!CanReceivePilotHostileEffect() || duration <= 0f)
            return;

        confusedUntil = Mathf.Max(confusedUntil, Time.time + duration);
        nextConfusedDirectionAt = 0f;
        nextConfusedShotAt = 0f;
    }

    void ApplyConfusedBehavior()
    {
        if (rb == null)
            return;

        if (Time.time >= nextConfusedDirectionAt || confusedMoveDirection.sqrMagnitude <= 0.001f)
        {
            confusedMoveDirection = Random.insideUnitCircle;
            if (confusedMoveDirection.sqrMagnitude <= 0.001f)
                confusedMoveDirection = Vector2.up;

            confusedMoveDirection.Normalize();
            nextConfusedDirectionAt = Time.time + Random.Range(0.35f, 0.95f);
        }

        float speed = EffectiveMoveSpeed * Random.Range(0.45f, 1.1f);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, confusedMoveDirection * speed, 0.22f);
        float targetAngle = Mathf.Atan2(confusedMoveDirection.y, confusedMoveDirection.x) * Mathf.Rad2Deg - 90f;
        rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, targetAngle, 240f * Time.fixedDeltaTime));

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null && Time.time >= nextConfusedShotAt)
        {
            Vector2 shotDirection = Random.insideUnitCircle;
            if (shotDirection.sqrMagnitude <= 0.001f)
                shotDirection = confusedMoveDirection;

            shooting.TryFireBot(shotDirection.normalized);
            nextConfusedShotAt = Time.time + Random.Range(0.22f, 0.75f);
        }
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

    void ConfigureColliderToVisual()
    {
        if (kind != EnemyBotKind.Mothership && kind != EnemyBotKind.PirateBase && kind != EnemyBotKind.SpaceManta && kind != EnemyBotKind.GravitySquid && kind != EnemyBotKind.HunterLance && kind != EnemyBotKind.ContainerShip && kind != EnemyBotKind.CosmicWorm && kind != EnemyBotKind.RiftWarden)
            return;

        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        if (cachedRenderer == null)
            return;

        Bounds bounds = cachedRenderer.bounds;
        float visualScale = Mathf.Max(0.05f, EnemyVisualScaleMultiplier);
        Vector2 baseVisualSize = new Vector2(bounds.size.x / visualScale, bounds.size.y / visualScale);
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            Vector2 boxScale = kind == EnemyBotKind.SpaceManta
                ? new Vector2(baseVisualSize.x * 0.68f, baseVisualSize.y * 0.46f)
                : kind == EnemyBotKind.GravitySquid
                    ? new Vector2(baseVisualSize.x * 0.56f, baseVisualSize.y * 0.68f)
                : kind == EnemyBotKind.HunterLance
                    ? new Vector2(baseVisualSize.x * 0.48f, baseVisualSize.y * 0.84f)
                : kind == EnemyBotKind.PirateBase
                    ? new Vector2(baseVisualSize.x * 0.78f, baseVisualSize.y * 0.7f)
                : kind == EnemyBotKind.ContainerShip
                    ? new Vector2(baseVisualSize.x * 0.78f, baseVisualSize.y * 0.58f)
                : kind == EnemyBotKind.CosmicWorm
                    ? new Vector2(baseVisualSize.x * 0.74f, baseVisualSize.y * 0.5f)
                : kind == EnemyBotKind.RiftWarden
                    ? new Vector2(baseVisualSize.x * 0.58f, baseVisualSize.y * 0.58f)
                    : new Vector2(baseVisualSize.x * 0.82f, baseVisualSize.y * 0.72f);
            SetWorldBoxSize(boxCollider, boxScale);
        }

        CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
        if (circleCollider != null)
            circleCollider.enabled = false;
    }

    void SetWorldBoxSize(BoxCollider2D collider2D, Vector2 worldSize)
    {
        Vector3 scale = collider2D.transform.lossyScale;
        float safeX = Mathf.Abs(scale.x) > 0.0001f ? Mathf.Abs(scale.x) : 1f;
        float safeY = Mathf.Abs(scale.y) > 0.0001f ? Mathf.Abs(scale.y) : 1f;
        collider2D.size = new Vector2(worldSize.x / safeX, worldSize.y / safeY);
        collider2D.offset = Vector2.zero;
    }

    void ApplyBotStats()
    {
        if (health == null || Definition == null)
            return;

        health.ConfigureBaseStats(RoomSettings.GetEnemyHp(kind), RoomSettings.GetEnemyShield(kind));
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
                    case EnemyMovementModel.RouteExtractionZones:
                        behavior = gameObject.AddComponent<EnemySpaceTruckBehavior>();
                        break;
                    case EnemyMovementModel.RadarShip:
                        behavior = gameObject.AddComponent<EnemyRadarShipBehavior>();
                        break;
                    case EnemyMovementModel.RescueShip:
                        behavior = gameObject.AddComponent<EnemyRescueShipBehavior>();
                        break;
                    case EnemyMovementModel.PirateFighter:
                        behavior = gameObject.AddComponent<EnemyPirateFighterBehavior>();
                        break;
                    case EnemyMovementModel.PirateBase:
                        behavior = gameObject.AddComponent<EnemyPirateBaseBehavior>();
                        break;
                    case EnemyMovementModel.SpaceManta:
                        behavior = gameObject.AddComponent<EnemySpaceMantaBehavior>();
                        break;
                    case EnemyMovementModel.GravitySquid:
                        behavior = gameObject.AddComponent<EnemyGravitySquidBehavior>();
                        break;
                    case EnemyMovementModel.HunterLance:
                        behavior = gameObject.AddComponent<EnemyHunterLanceBehavior>();
                        break;
                    case EnemyMovementModel.ContainerShip:
                        behavior = gameObject.AddComponent<EnemyContainerShipBehavior>();
                        break;
                    case EnemyMovementModel.CosmicWorm:
                        behavior = gameObject.AddComponent<EnemyCosmicWormBehavior>();
                        break;
                    case EnemyMovementModel.RiftWarden:
                        behavior = gameObject.AddComponent<EnemyRiftWardenBehavior>();
                        break;
                    case EnemyMovementModel.MilitaryVan:
                        behavior = gameObject.AddComponent<EnemyMilitaryVanBehavior>();
                        break;
                    case EnemyMovementModel.Mothership:
                        behavior = gameObject.AddComponent<EnemyMothershipBehavior>();
                        break;
                    case EnemyMovementModel.NeutralFighter:
                        behavior = gameObject.AddComponent<EnemyNeutralFighterBehavior>();
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
            EnemyMovementModel.RouteExtractionZones => existingBehavior is EnemySpaceTruckBehavior,
            EnemyMovementModel.RadarShip => existingBehavior is EnemyRadarShipBehavior,
            EnemyMovementModel.RescueShip => existingBehavior is EnemyRescueShipBehavior,
            EnemyMovementModel.PirateFighter => existingBehavior is EnemyPirateFighterBehavior,
            EnemyMovementModel.PirateBase => existingBehavior is EnemyPirateBaseBehavior,
            EnemyMovementModel.SpaceManta => existingBehavior is EnemySpaceMantaBehavior,
            EnemyMovementModel.GravitySquid => existingBehavior is EnemyGravitySquidBehavior,
            EnemyMovementModel.HunterLance => existingBehavior is EnemyHunterLanceBehavior,
            EnemyMovementModel.ContainerShip => existingBehavior is EnemyContainerShipBehavior,
            EnemyMovementModel.CosmicWorm => existingBehavior is EnemyCosmicWormBehavior,
            EnemyMovementModel.RiftWarden => existingBehavior is EnemyRiftWardenBehavior,
            EnemyMovementModel.MilitaryVan => existingBehavior is EnemyMilitaryVanBehavior,
            EnemyMovementModel.Mothership => existingBehavior is EnemyMothershipBehavior,
            EnemyMovementModel.NeutralFighter => existingBehavior is EnemyNeutralFighterBehavior,
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

        BoosterBarUI boosterUi = GetComponent<BoosterBarUI>();
        if (boosterUi != null)
            boosterUi.enabled = false;
    }

    void ApplyBotVisuals()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = gameObject.AddComponent<SpriteRenderer>();

        cachedRenderer = renderer;
        Sprite sprite = GetVisualSprite();
        if (sprite == null)
            return;

        renderer.enabled = true;
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = GetEnemySpriteSortingOrder();
        FitRendererToTargetSize(renderer, VisualTargetSize);
    }

    void EnsureAnimatedVisual()
    {
        if (Definition == null || string.IsNullOrWhiteSpace(Definition.AnimationResourcePath))
            return;

        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        if (cachedRenderer == null)
            return;

        EnemySpriteFrameAnimator animator = GetComponent<EnemySpriteFrameAnimator>();
        if (animator == null)
            animator = gameObject.AddComponent<EnemySpriteFrameAnimator>();

        animator.Configure(
            cachedRenderer,
            Definition.AnimationResourcePath,
            Definition.AnimationFramesPerSecond > 0f ? Definition.AnimationFramesPerSecond : 7f);
    }

    bool UsesAnimatedVisual()
    {
        return Definition != null && !string.IsNullOrWhiteSpace(Definition.AnimationResourcePath);
    }

    void EnsureStableVisuals()
    {
        if (health != null && health.IsWreck)
            return;

        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        if (cachedRenderer == null)
            cachedRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (!UsesAnimatedVisual())
        {
            Sprite desiredSprite = GetVisualSprite();
            if (desiredSprite != null && cachedRenderer.sprite != desiredSprite)
                cachedRenderer.sprite = desiredSprite;
        }
        else if (cachedRenderer.sprite == null)
        {
            cachedRenderer.sprite = GetVisualSprite();
        }

        cachedRenderer.enabled = true;
        cachedRenderer.color = Color.white;
        cachedRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        cachedRenderer.sortingOrder = pirateBaseLaunchProtected ? ResolvePirateBaseLaunchSortingOrder() : GetEnemySpriteSortingOrder();
        FitRendererToTargetSize(cachedRenderer, VisualTargetSize);
    }

    int GetEnemySpriteSortingOrder()
    {
        return kind == EnemyBotKind.RiftWarden
            ? GameVisualTheme.EnemySortingOrder + 6
            : GameVisualTheme.EnemySortingOrder;
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
        if (hasDetonated || !CanRequestDetonation() || Definition == null || Definition.Explosion == null)
            return;

        EnemyExplosionProfile explosion = Definition.Explosion;
        hasDetonated = true;
        Vector3 detonationPosition = GetVisualCenterWorldPosition();
        SpaceObjectMotionSync.BroadcastSpaceMineDetonation(detonationPosition, explosion.TriggerRadius);
        DetonateNearbyTargets(explosion);
        if (PhotonNetwork.CurrentRoom != null && photonView != null)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    bool CanRequestDetonation()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return true;

        if (PhotonNetwork.IsMasterClient)
            return true;

        return isOwnedMine && view != null && view.IsMine;
    }

    void DetonateNearbyTargets(EnemyExplosionProfile explosion)
    {
        if (explosion == null)
            return;

        WeaponHitContext hitContext = new WeaponHitContext(explosion.DamageType, explosion.DeliveryMethod, explosion.DeliveryFlags, string.Empty);
        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            EnemyBot candidateBot = candidate.GetComponent<EnemyBot>();
            if (candidateBot != null && candidateBot.Kind == EnemyBotKind.SpaceMine)
                continue;

            if (ShouldIgnoreMineTriggerFor(candidate))
                continue;

            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            if (distance > explosion.TriggerRadius)
                continue;

            PhotonView targetView = candidate.GetComponent<PhotonView>();
            if (targetView != null)
                targetView.RPC(
                    nameof(PlayerHealth.TakeDamageWithContext),
                    RpcTarget.MasterClient,
                    RoomSettings.GetEnemyDamage(kind),
                    photonView.ViewID,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted || beacon.photonView == null)
                continue;

            float distance = Vector2.Distance(transform.position, beacon.transform.position);
            if (distance > explosion.TriggerRadius)
                continue;

            beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageAt), RpcTarget.MasterClient, RoomSettings.GetEnemyDamage(kind), photonView.ViewID, beacon.transform.position.x, beacon.transform.position.y);
        }

        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (deployable == null || !deployable.CanBeTargeted || deployable.photonView == null)
                continue;

            float distance = Vector2.Distance(transform.position, deployable.transform.position);
            if (distance > explosion.TriggerRadius)
                continue;

            int damage = RoomSettings.GetEnemyDamage(kind);
            deployable.photonView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                damage,
                damage,
                photonView.ViewID,
                deployable.transform.position.x,
                deployable.transform.position.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
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

        float scale = (targetSize * Mathf.Max(0.05f, visualScaleMultiplier) * EnemyVisualScaleMultiplier) / largestDimension;
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
        isOwnedMine = false;
        mineOwnerViewId = 0;

        if (kind != EnemyBotKind.SpaceMine || instantiationData == null || instantiationData.Length < 3)
            return;

        if (!(instantiationData[1] is string marker))
            return;

        bool playerMine = string.Equals(marker, PlayerPlacedMineMarker, System.StringComparison.Ordinal);
        bool containerShipMine = string.Equals(marker, ContainerShipMineMarker, System.StringComparison.Ordinal);
        if (!playerMine && !containerShipMine)
            return;

        if (instantiationData[2] is int ownerViewId && ownerViewId > 0)
        {
            isPlayerPlacedMine = playerMine;
            isOwnedMine = true;
            mineOwnerViewId = ownerViewId;
        }
    }

    void ResolveContainerShipCargoVariant(object[] instantiationData)
    {
        containerShipCargoVariantIndex = 0;
        if (kind != EnemyBotKind.ContainerShip)
            return;

        int variantCount = Mathf.Max(1, InventoryItemCatalog.BlueprintScrapContainerVariantCount);
        if (instantiationData != null && instantiationData.Length >= 2 && instantiationData[1] is int variantIndex)
        {
            containerShipCargoVariantIndex = Mathf.Clamp(variantIndex, 0, variantCount - 1);
            return;
        }

        int seed = view != null && view.ViewID > 0 ? view.ViewID : Mathf.RoundToInt(transform.position.sqrMagnitude * 1000f);
        containerShipCargoVariantIndex = Mathf.Abs(seed) % variantCount;
    }

    void ResolveSummonedDrone(object[] instantiationData)
    {
        isSummonedDrone = false;

        if (kind != EnemyBotKind.Drone || instantiationData == null || instantiationData.Length < 2)
            return;

        isSummonedDrone = instantiationData[1] is string marker &&
                          string.Equals(marker, SummonedDroneMarker, System.StringComparison.Ordinal);
    }

    void ResolvePirateBaseLaunchedFighter(object[] instantiationData)
    {
        isPirateBaseLaunchedFighter = false;
        pirateBaseLaunchTargetViewId = 0;
        pirateBaseLaunchSourceViewId = 0;

        if (!IsPirateFighterKind(kind) || instantiationData == null || instantiationData.Length < 2)
            return;

        if (!(instantiationData[1] is string marker) ||
            !string.Equals(marker, PirateBaseLaunchedFighterMarker, System.StringComparison.Ordinal))
        {
            return;
        }

        isPirateBaseLaunchedFighter = true;
        if (instantiationData.Length >= 3 && instantiationData[2] is int targetViewId)
            pirateBaseLaunchTargetViewId = Mathf.Max(0, targetViewId);
        if (instantiationData.Length >= 4 && instantiationData[3] is int sourceViewId)
            pirateBaseLaunchSourceViewId = Mathf.Max(0, sourceViewId);
    }

    void BeginPirateBaseLaunchAnimation()
    {
        if (pirateBaseLaunchProtected)
            return;

        pirateBaseLaunchProtected = true;
        visualScaleMultiplier = PirateBaseLaunchStartScale;
        pirateBaseLaunchStartedAt = Time.time;
        pirateBaseLaunchStartPosition = transform.position;
        Vector3 exitDirection = transform.up.sqrMagnitude > 0.001f ? transform.up.normalized : Vector3.up;
        pirateBaseLaunchEndPosition = pirateBaseLaunchStartPosition + exitDirection * PirateBaseLaunchExitDistance;
        StoreAndLockPirateBaseLaunchBody();
        StoreAndDisablePirateBaseLaunchColliders();
        ApplyPirateBaseLaunchRenderOrder();
    }

    void StoreAndLockPirateBaseLaunchBody()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        if (!pirateBaseLaunchBodyStateStored)
        {
            pirateBaseLaunchBodyStateStored = true;
            pirateBaseLaunchPreviousBodyType = rb.bodyType;
            pirateBaseLaunchPreviousSimulated = rb.simulated;
        }

        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    void StoreAndDisablePirateBaseLaunchColliders()
    {
        if (!pirateBaseLaunchColliderStateStored)
        {
            pirateBaseLaunchColliderStateStored = true;
            pirateBaseLaunchColliders = GetComponentsInChildren<Collider2D>(true);
            pirateBaseLaunchColliderStates = new bool[pirateBaseLaunchColliders.Length];
            for (int i = 0; i < pirateBaseLaunchColliders.Length; i++)
            {
                Collider2D launchCollider = pirateBaseLaunchColliders[i];
                pirateBaseLaunchColliderStates[i] = launchCollider != null && launchCollider.enabled;
            }
        }

        if (pirateBaseLaunchColliders == null)
            return;

        for (int i = 0; i < pirateBaseLaunchColliders.Length; i++)
        {
            Collider2D launchCollider = pirateBaseLaunchColliders[i];
            if (launchCollider != null)
                launchCollider.enabled = false;
        }
    }

    void UpdatePirateBaseLaunchAnimation()
    {
        if (!pirateBaseLaunchProtected)
            return;

        float elapsed = Time.time - pirateBaseLaunchStartedAt;
        float t = Mathf.Clamp01(elapsed / PirateBaseLaunchAnimationDuration);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        visualScaleMultiplier = Mathf.Lerp(PirateBaseLaunchStartScale, 1f, eased);
        if (cachedRenderer != null)
        {
            ApplyPirateBaseLaunchRenderOrder();
            FitRendererToTargetSize(cachedRenderer, VisualTargetSize);
        }

        Vector3 nextPosition = Vector3.Lerp(pirateBaseLaunchStartPosition, pirateBaseLaunchEndPosition, eased);

        StoreAndLockPirateBaseLaunchBody();
        StoreAndDisablePirateBaseLaunchColliders();
        if (rb != null && rb.bodyType == RigidbodyType2D.Kinematic)
            rb.MovePosition(nextPosition);
        else
            transform.position = nextPosition;

        if (t < 1f)
            return;

        FinishPirateBaseLaunchAnimation();
    }

    void FinishPirateBaseLaunchAnimation()
    {
        RestorePirateBaseLaunchColliders();
        pirateBaseLaunchProtected = false;
        visualScaleMultiplier = 1f;

        if (rb != null)
        {
            rb.simulated = pirateBaseLaunchBodyStateStored ? pirateBaseLaunchPreviousSimulated : true;
            rb.bodyType = pirateBaseLaunchBodyStateStored ? pirateBaseLaunchPreviousBodyType : RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        ForceCombatTarget(pirateBaseLaunchTargetViewId);
    }

    void ApplyPirateBaseLaunchRenderOrder()
    {
        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        if (cachedRenderer == null)
            return;

        cachedRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        cachedRenderer.sortingOrder = ResolvePirateBaseLaunchSortingOrder();
    }

    int ResolvePirateBaseLaunchSortingOrder()
    {
        int baseOrder = GameVisualTheme.EnemySortingOrder;
        if (pirateBaseLaunchSourceViewId <= 0)
            return baseOrder + 12;

        PhotonView sourceView = PhotonView.Find(pirateBaseLaunchSourceViewId);
        SpriteRenderer sourceRenderer = sourceView != null ? sourceView.GetComponent<SpriteRenderer>() : null;
        if (sourceRenderer != null)
            baseOrder = sourceRenderer.sortingOrder;

        return baseOrder + 12;
    }

    void RestorePirateBaseLaunchColliders()
    {
        if (!pirateBaseLaunchColliderStateStored || pirateBaseLaunchColliders == null || pirateBaseLaunchColliderStates == null)
            return;

        int count = Mathf.Min(pirateBaseLaunchColliders.Length, pirateBaseLaunchColliderStates.Length);
        for (int i = 0; i < count; i++)
        {
            Collider2D launchCollider = pirateBaseLaunchColliders[i];
            if (launchCollider != null)
                launchCollider.enabled = pirateBaseLaunchColliderStates[i];
        }
    }

    void ApplyMineOwnerCollisionIgnore()
    {
        if (!isOwnedMine || mineOwnerViewId <= 0)
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
        if (!isOwnedMine || candidate == null || mineOwnerViewId <= 0)
            return false;

        PhotonView candidateView = candidate.GetComponent<PhotonView>();
        return candidateView != null && candidateView.ViewID == mineOwnerViewId;
    }

    public void ConvertMothershipTurretsToWreckVisuals()
    {
        if (kind != EnemyBotKind.Mothership)
            return;

        EnemyMothershipBehavior mothershipBehavior = behavior as EnemyMothershipBehavior;
        if (mothershipBehavior == null)
            mothershipBehavior = GetComponent<EnemyMothershipBehavior>();

        mothershipBehavior?.ConvertTurretsToWreckVisuals();
    }

    public void HideContainerShipCargoVisual()
    {
        if (kind != EnemyBotKind.ContainerShip)
            return;

        EnemyContainerShipBehavior containerShipBehavior = behavior as EnemyContainerShipBehavior;
        if (containerShipBehavior == null)
            containerShipBehavior = GetComponent<EnemyContainerShipBehavior>();

        containerShipBehavior?.HideCargoVisual();
    }

    public void NotifyDamageTaken(int previousHp, int currentHp, int shieldDamage, int hpDamage, int attackerViewID)
    {
        if (!PhotonNetwork.IsMasterClient || health == null || health.IsWreck)
            return;

        bool wasHit = shieldDamage > 0 || hpDamage > 0;
        if (!wasHit)
            return;

        if (kind != EnemyBotKind.SpaceMine && kind != EnemyBotKind.RescueShip && kind != EnemyBotKind.PirateBase)
            EnemyBotManager.NotifyRescueShipSummonTrigger(this);

        if (kind == EnemyBotKind.Mothership && behavior is EnemyMothershipBehavior mothershipBehavior)
            mothershipBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.NeutralFighter && behavior is EnemyNeutralFighterBehavior neutralFighterBehavior)
            neutralFighterBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.RadarShip && behavior is EnemyRadarShipBehavior radarShipBehavior)
            radarShipBehavior.NotifyDamageSource(attackerViewID);

        if (IsPirateFighter && behavior is EnemyPirateFighterBehavior pirateFighterBehavior)
            pirateFighterBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.PirateBase && behavior is EnemyPirateBaseBehavior pirateBaseBehavior)
            pirateBaseBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.SpaceManta && behavior is EnemySpaceMantaBehavior spaceMantaBehavior)
            spaceMantaBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.GravitySquid && behavior is EnemyGravitySquidBehavior gravitySquidBehavior)
            gravitySquidBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.HunterLance && behavior is EnemyHunterLanceBehavior hunterLanceBehavior)
            hunterLanceBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.ContainerShip && behavior is EnemyContainerShipBehavior containerShipBehavior)
            containerShipBehavior.NotifyDamageTaken(attackerViewID);

        if (kind == EnemyBotKind.CosmicWorm && behavior is EnemyCosmicWormBehavior cosmicWormBehavior)
            cosmicWormBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.MilitaryVan && behavior is EnemyMilitaryVanBehavior militaryVanBehavior)
            militaryVanBehavior.NotifyDamageSource(attackerViewID);

        if (kind != EnemyBotKind.SpaceTruck)
            return;

        if (!spaceTruckFirstHitHandled)
        {
            spaceTruckFirstHitHandled = true;
            forcedSpeedMultiplier = 2f;
            TriggerSpaceTruckAlarmAndDrone();
        }

        int halfHp = Mathf.CeilToInt(health.maxHP * 0.5f);
        if (!spaceTruckHalfHpHandled && previousHp > halfHp && currentHp <= halfHp)
        {
            spaceTruckHalfHpHandled = true;
            TriggerSpaceTruckAlarmAndDrone();
        }
    }

    void TriggerSpaceTruckAlarmAndDrone()
    {
        Vector3 position = GetVisualCenterWorldPosition();
        photonView.RPC(nameof(PlaySpaceTruckAlert), RpcTarget.All, position.x, position.y, position.z);
        SpawnSummonedDroneNear(position);
    }

    void SpawnSummonedDroneNear(Vector3 sourcePosition)
    {
        EnemyBotDefinition droneDefinition = EnemyBotCatalog.GetDefinition(EnemyBotKind.Drone);
        if (droneDefinition == null)
            return;

        Vector2 offset = new Vector2(
            Mathf.Sin(Time.time * 3.7f + photonView.ViewID) > 0f ? 1.8f : -1.8f,
            1.25f);
        Vector3 spawnPosition = sourcePosition + (Vector3)offset;
        GameObject droneObject = PhotonNetwork.Instantiate("Player", spawnPosition, Quaternion.identity, 0, new object[] { droneDefinition.InstantiationMarker, SummonedDroneMarker });
        if (droneObject == null)
            return;

        EnemyBot drone = droneObject.GetComponent<EnemyBot>();
        if (drone == null)
            drone = droneObject.AddComponent<EnemyBot>();

        drone.InitializeFromPhotonData();
    }

    [PunRPC]
    void PlaySpaceTruckAlert(float x, float y, float z)
    {
        AudioManager.Instance.PlaySpaceTruckAlertAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void SpawnRadarStrikeMarkerRpc(float x, float y, float warningDuration, float radius)
    {
        RadarStrikeVfx.SpawnMarker(new Vector2(x, y), warningDuration, radius);
    }

    [PunRPC]
    public void PlayRadarShipShootRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayRadarShipShootAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void PlayRadarShipIncomingRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayRadarShipIncomingAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void SpawnRadarStrikeImpactRpc(float x, float y, float radius)
    {
        RadarStrikeVfx.SpawnImpact(new Vector2(x, y), radius);
    }

    [PunRPC]
    public void PlayRescueShipIncomingRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayRescueShipIncomingAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void PlaySpaceMantaWarningRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlaySpaceMantaWarningAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void PlayGravitySquidWarningRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayGravitySquidWarningAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void PlayHunterLanceLockRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayHunterLanceLockAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void PlayHunterLanceFireRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayHunterLanceFireAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void SpawnHunterLanceAimRpc(float originX, float originY, float directionX, float directionY, float range, float duration)
    {
        HunterLanceBeamVfx.SpawnAim(new Vector2(originX, originY), new Vector2(directionX, directionY), range, duration);
        DynamicCameraZoomController.Request(DynamicCameraZoomProfiles.HunterLanceLock, new Vector3(originX, originY, 0f), duration);
    }

    [PunRPC]
    public void SpawnHunterLanceShotRpc(float originX, float originY, float directionX, float directionY, float range)
    {
        HunterLanceBeamVfx.SpawnShot(new Vector2(originX, originY), new Vector2(directionX, directionY), range);
    }

    [PunRPC]
    public void PlayCosmicWormPhaseRpc(int phase, float x, float y, float z, float radius)
    {
        CosmicWormPhaseBurstVfx.Spawn(new Vector3(x, y, z), phase, radius);
        CosmicWormVisualController.AttachOrUpdate(this, phase, false);
    }

    [PunRPC]
    public void SpawnCosmicWormSpitVfxRpc(float x, float y, float directionX, float directionY, int phase)
    {
        CosmicWormSpitVfx.Spawn(new Vector2(x, y), new Vector2(directionX, directionY), phase);
    }

    [PunRPC]
    public void SpawnCosmicWormDashWarningRpc(float originX, float originY, float directionX, float directionY, float range, float duration)
    {
        CosmicWormDashWarningVfx.Spawn(new Vector2(originX, originY), new Vector2(directionX, directionY), range, duration);
        Vector2 direction = new Vector2(directionX, directionY);
        if (direction.sqrMagnitude > 0.001f)
            direction.Normalize();
        Vector2 midpoint = new Vector2(originX, originY) + direction * (Mathf.Max(0f, range) * 0.5f);
        DynamicCameraZoomController.Request(DynamicCameraZoomProfiles.CosmicWormDanger, new Vector3(midpoint.x, midpoint.y, 0f), duration);
    }

    [PunRPC]
    public void SpawnCosmicWormDashTrailRpc(float x, float y, float directionX, float directionY, float radius)
    {
        CosmicWormDashTrailVfx.Spawn(new Vector2(x, y), new Vector2(directionX, directionY), radius);
    }

    [PunRPC]
    public void StartCosmicWormSwallowRpc(float x, float y, float directionX, float directionY, float radius, float duration, int sourceViewId)
    {
        CosmicWormSwallowVfx.StartEffect(sourceViewId, new Vector2(x, y), new Vector2(directionX, directionY), radius, duration);
        cosmicWormSwallowZoomToken = DynamicCameraZoomController.Refresh(
            cosmicWormSwallowZoomToken,
            DynamicCameraZoomProfiles.CosmicWormDanger.WithMultiplier(1.2f),
            new Vector3(x, y, 0f),
            duration);
    }

    [PunRPC]
    public void StopCosmicWormSwallowRpc(int sourceViewId)
    {
        CosmicWormSwallowVfx.StopEffect(sourceViewId);
        DynamicCameraZoomController.Cancel(cosmicWormSwallowZoomToken);
        cosmicWormSwallowZoomToken = 0;
    }

    [PunRPC]
    public void PlayRiftWardenAwakenRpc(float x, float y, float z)
    {
        Vector3 position = new Vector3(x, y, z);
        RiftWardenVfx.PlayAwaken(position);
        if (IsLocalPlayerInSameMapInstance(position))
        {
            RoundMessageLayer.ShowStatusFeed(
                "RIFT WARDEN",
                "ALIEN SECURITY SYSTEM ONLINE",
                RoundMessagePriority.Warning,
                3.4f,
                new Color(0.24f, 1f, 0.86f, 1f));
        }
    }

    [PunRPC]
    public void PlayRiftWardenBeamRpc(float startX, float startY, float endX, float endY, float duration, bool blast)
    {
        RiftWardenVfx.PlayBeam(new Vector2(startX, startY), new Vector2(endX, endY), duration, blast);
    }

    [PunRPC]
    public void PlayRiftWardenTrackingBeamRpc(int sourceViewId, int targetViewId, float startX, float startY, float endX, float endY, float windupDuration, float activeDuration)
    {
        RiftWardenVfx.PlayTrackingBeam(
            sourceViewId,
            targetViewId,
            new Vector2(startX, startY),
            new Vector2(endX, endY),
            windupDuration,
            activeDuration);
    }

    [PunRPC]
    public void PlayRiftWardenRippleRpc(float x, float y, float radius, float warningDuration, float activeDuration)
    {
        RiftWardenVfx.PlayRipple(new Vector2(x, y), radius, warningDuration, activeDuration);
    }

    [PunRPC]
    public void PlayRiftWardenBlinkRpc(float fromX, float fromY, float toX, float toY)
    {
        RiftWardenVfx.PlayBlink(new Vector2(fromX, fromY), new Vector2(toX, toY));
    }

    [PunRPC]
    public void PlayRiftWardenLockdownRpc(int treasureViewId, float x, float y, float radius, float duration)
    {
        RiftWardenVfx.PlayLockdown(treasureViewId, new Vector2(x, y), radius, duration);
    }

    static bool IsLocalPlayerInSameMapInstance(Vector2 position)
    {
        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player == null || player.photonView == null || !player.photonView.IsMine)
                continue;

            return MapInstanceService.IsSameInstance(player.transform.position, position);
        }

        return true;
    }

    [PunRPC]
    public void StartGravitySquidTetherRpc(int targetViewId)
    {
        GravitySquidTetherVfx.StartBeam(photonView != null ? photonView.ViewID : 0, targetViewId);
    }

    [PunRPC]
    public void StopGravitySquidTetherRpc()
    {
        GravitySquidTetherVfx.StopBeam(photonView != null ? photonView.ViewID : 0);
    }

    [PunRPC]
    public void StartRescueShipBeamRpc(int targetViewId)
    {
        RescueShipBeamVfx.StartBeam(photonView != null ? photonView.ViewID : 0, targetViewId);
    }

    [PunRPC]
    public void StopRescueShipBeamRpc()
    {
        RescueShipBeamVfx.StopBeam(photonView != null ? photonView.ViewID : 0);
    }

    [PunRPC]
    public void StartPirateBaseCollectionBeamRpc(int targetViewId)
    {
        if (kind != EnemyBotKind.PirateBase)
            return;

        PirateBaseCollectionBeamVfx.StartBeam(photonView != null ? photonView.ViewID : 0, targetViewId);
    }

    [PunRPC]
    public void StopPirateBaseCollectionBeamRpc()
    {
        PirateBaseCollectionBeamVfx.StopBeam(photonView != null ? photonView.ViewID : 0);
    }

    [PunRPC]
    public void PlayPirateBaseLaunchVfxRpc(int fighterKindValue)
    {
        if (kind != EnemyBotKind.PirateBase)
            return;

        PirateBaseLaunchVfx.Play(this, (EnemyBotKind)fighterKindValue);
        DynamicCameraZoomController.Request(DynamicCameraZoomProfiles.PirateBaseLaunch, transform.position);
    }

    public void ForceCombatTarget(int targetViewId)
    {
        if (IsPirateFighter && behavior is EnemyPirateFighterBehavior pirateFighterBehavior)
            pirateFighterBehavior.NotifyForcedTarget(targetViewId);
    }

    public void DropPirateBaseCargoOnDeath()
    {
        if (kind != EnemyBotKind.PirateBase)
            return;

        EnemyPirateBaseBehavior pirateBaseBehavior = behavior as EnemyPirateBaseBehavior;
        if (pirateBaseBehavior == null)
            pirateBaseBehavior = GetComponent<EnemyPirateBaseBehavior>();

        pirateBaseBehavior?.DropCollectedCargoOnDeath();
    }

    public void DropContainerShipCargoOnDeath()
    {
        if (kind != EnemyBotKind.ContainerShip || !PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom)
            return;

        int variantIndex = ContainerShipCargoVariantIndex;
        string itemId = InventoryItemCatalog.GetBlueprintScrapContainerItemId(variantIndex);
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        Vector2 driftDirection = rb != null && rb.linearVelocity.sqrMagnitude > 0.04f
            ? rb.linearVelocity.normalized
            : (Vector2)transform.up;
        if (driftDirection.sqrMagnitude <= 0.001f)
            driftDirection = Vector2.up;

        Vector2 side = new Vector2(-driftDirection.y, driftDirection.x);
        float seed = (photonView != null ? photonView.ViewID : variantIndex + 1) * 0.173f;
        Vector2 drift = (driftDirection * 0.62f + side * Mathf.Lerp(-0.22f, 0.22f, Mathf.PerlinNoise(seed, seed + 9.3f))).normalized * 0.58f;
        Vector3 dropPosition = GetVisualCenterWorldPosition() - (Vector3)(driftDirection * 0.25f);
        GameObject cargo = PhotonNetwork.Instantiate("TreasureNetwork", dropPosition, Quaternion.identity, 0, new object[] { itemId });
        if (cargo != null)
        {
            Rigidbody2D cargoBody = cargo.GetComponent<Rigidbody2D>();
            if (cargoBody != null)
            {
                cargoBody.linearVelocity = drift;
                cargoBody.angularVelocity = Mathf.Lerp(-28f, 28f, Mathf.PerlinNoise(seed + 1.7f, seed + 4.1f));
            }
        }

        GameVisualTheme.RequestRuntimeRefresh(true);
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
        SpawnSpaceMineDetonationEffects(worldPosition, explosion != null ? explosion.TriggerRadius : 0f);
    }

    public static void SpawnSpaceMineDetonationEffects(Vector3 worldPosition, float radius)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(EnemyBotKind.SpaceMine);
        EnemyExplosionProfile explosion = definition != null ? definition.Explosion : null;
        if (explosion == null)
            return;

        float effectRadius = radius > 0.1f ? radius : explosion.TriggerRadius;
        if (effectRadius >= 4.5f)
            SpaceBombExplosionVfx.Spawn(worldPosition, effectRadius);
        else
            SpaceMineExplosionVfx.Spawn(worldPosition, effectRadius);

        if (explosion.SoundId == "space_mine_boom")
            AudioManager.Instance.PlaySpaceMineBoomAt(worldPosition);
        else
            AudioManager.Instance.PlayExplosionAt(worldPosition);
    }
}
