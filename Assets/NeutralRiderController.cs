using System;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;

public enum NeutralRiderArchetype
{
    Scavenger,
    Hunter,
    Raider,
    Coward
}

[DisallowMultipleComponent]
public sealed class NeutralRiderController : MonoBehaviourPun
{
    public const string InstantiationMarker = "neutral_rider";
    public const string AggressiveInstantiationMarker = "avenger_plot_aggressive";

    const float ThinkInterval = 0.22f;
    const float TargetRefreshInterval = 0.55f;
    const float ExtractionTargetRefreshInterval = 0.65f;
    const float TimerLookupInterval = 0.75f;
    const float CollectRange = 2.25f;
    const float CollectSeconds = 3f;
    const float WreckCollectSeconds = 3.25f;
    const float ExtractionUseSeconds = 2.5f;
    const float CombatRange = 10.5f;
    const float DetectionRange = 14f;
    const float PlayerRivalryRange = 11f;
    const float FleeHealthRatio = 0.34f;
    const float ExtractionCargoValue = 1850f;
    const float ExtractionEndgameSeconds = 28f;
    const float MinimumExtractionElapsedSeconds = 85f;
    const float FleeExtractionMinimumCargoValue = 700f;
    const float MapMargin = 4f;
    const float AvoidanceRadius = 1.65f;
    const float AvoidanceRefreshInterval = 0.1f;
    const float NameplateRefreshInterval = 0.12f;
    const int HumanOnlyDisablePassCount = 4;
    const float NameplateYOffset = 0.92f;

    static readonly HashSet<NeutralRiderController> ActiveRiders = new HashSet<NeutralRiderController>();

    struct WeaponLoadout
    {
        public float FireRate;
        public int MaxAmmo;
        public float ReloadDuration;
        public int Damage;
        public float BulletScale;
        public Color BulletColor;
        public float MuzzleOffset;
        public float BulletSpeed;
        public string ShotSoundId;
        public float RangeMultiplier;
        public float FlightTime;
    }

    enum Mode
    {
        Patrol,
        Scavenge,
        Combat,
        Flee,
        Extract,
        Evacuated
    }

    Rigidbody2D rb;
    PlayerHealth health;
    PlayerShooting shooting;
    SpriteRenderer spriteRenderer;
    TextMeshPro nameplate;
    PhotonView view;

    readonly List<string> cargo = new List<string>();
    Mode mode = Mode.Patrol;
    NeutralRiderArchetype archetype = NeutralRiderArchetype.Scavenger;
    Vector2 destination;
    Vector2 patrolDirection = Vector2.up;
    float nextThinkTime;
    float nextTargetRefreshTime;
    float nextExtractionTargetRefreshTime;
    float nextTimerLookupTime;
    float nextAvoidanceRefreshTime;
    float nextNameplateRefreshTime;
    float collectStartedAt = -1f;
    float extractionUseStartedAt = -1f;
    Vector2 cachedAvoidanceDirection = Vector2.zero;
    int targetViewId;
    int collectingBeamTargetViewId;
    int hostilePlayerViewId;
    int humanOnlyDisablePasses;
    float hostilePlayerUntil;
    int shipSkinIndex;
    int spawnOrdinal;
    string riderName = "Rider";
    GameTimer cachedTimer;
    bool initialized;
    bool wreckConverted;
    bool collectionTargetLocked;
    bool forcedAggressive;
    bool humanOnlyComponentsDisabled;
    bool registeredActiveRider;
    bool nameplateVisible;
    string lastNameplateText = string.Empty;
    int forcedAggressiveTargetViewId;

    public int ShipSkinIndex => shipSkinIndex;
    public bool IsEvacuated => mode == Mode.Evacuated;
    public bool HasCargoSpace => cargo.Count < Mathf.Max(1, ShipCatalog.GetShipInventoryCapacity(shipSkinIndex));
    public int CargoValueAstrons
    {
        get
        {
            int total = 0;
            for (int i = 0; i < cargo.Count; i++)
                total += InventoryItemCatalog.GetSellValueAstrons(cargo[i]);

            return total;
        }
    }

    public static bool IsNeutralRider(GameObject target)
    {
        return target != null && target.GetComponent<NeutralRiderController>() != null;
    }

    public static bool IsNeutralRider(Component target)
    {
        return target != null && IsNeutralRider(target.gameObject);
    }

    public static bool IsNeutralRiderInstantiationData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               string.Equals(marker, InstantiationMarker, StringComparison.Ordinal);
    }

    public static int GetShipSkinIndexFromInstantiationData(object[] data)
    {
        if (!IsNeutralRiderInstantiationData(data) || data.Length < 2)
            return ShipCatalog.ExplorerBasicSkinIndex;

        return ConvertToInt(data[1], ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex);
    }

    public static string GetNameFromInstantiationData(object[] data)
    {
        if (!IsNeutralRiderInstantiationData(data) || data.Length < 4 || data[3] is not string name || string.IsNullOrWhiteSpace(name))
            return "Rider";

        return name;
    }

    public static object[] BuildInstantiationData(int skinIndex, NeutralRiderArchetype archetype, string name, int ordinal)
    {
        return new object[]
        {
            InstantiationMarker,
            Mathf.Clamp(skinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex),
            (int)archetype,
            string.IsNullOrWhiteSpace(name) ? "Rider" : name,
            Mathf.Max(0, ordinal)
        };
    }

    public static object[] BuildAggressiveInstantiationData(int skinIndex, string name, int ordinal, int targetViewId)
    {
        return new object[]
        {
            InstantiationMarker,
            Mathf.Clamp(skinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex),
            (int)NeutralRiderArchetype.Hunter,
            string.IsNullOrWhiteSpace(name) ? "Rider" : name,
            Mathf.Max(0, ordinal),
            AggressiveInstantiationMarker,
            Mathf.Max(0, targetViewId)
        };
    }

    public static string GetGeneratedName(int ordinal)
    {
        return "AI_Raider_" + (Mathf.Max(0, ordinal) + 1);
    }

    public static int CountActiveRiders()
    {
        int count = 0;
        foreach (NeutralRiderController rider in ActiveRiders)
        {
            PlayerHealth riderHealth = rider != null ? rider.health : null;
            if (rider != null && riderHealth != null && !riderHealth.IsWreck && !rider.IsEvacuated)
                count++;
        }

        return count;
    }

    public static void CopyActiveRiders(List<NeutralRiderController> riders)
    {
        if (riders == null)
            return;

        riders.Clear();
        foreach (NeutralRiderController rider in ActiveRiders)
        {
            if (rider != null)
                riders.Add(rider);
        }
    }

    public void InitializeFromPhotonData()
    {
        if (initialized)
            return;

        view = photonView != null ? photonView : GetComponent<PhotonView>();
        if (!IsNeutralRiderInstantiationData(view != null ? view.InstantiationData : null))
            return;

        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
        shooting = GetComponent<PlayerShooting>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        object[] data = view.InstantiationData;
        shipSkinIndex = GetShipSkinIndexFromInstantiationData(data);
        archetype = data != null && data.Length > 2
            ? (NeutralRiderArchetype)Mathf.Clamp(ConvertToInt(data[2], 0, 0, 3), 0, 3)
            : NeutralRiderArchetype.Scavenger;
        riderName = GetNameFromInstantiationData(data);
        spawnOrdinal = data != null && data.Length > 4 ? ConvertToInt(data[4], 0, 0, 999) : 0;
        forcedAggressive = data != null &&
                           data.Length > 5 &&
                           data[5] is string extraMarker &&
                           string.Equals(extraMarker, AggressiveInstantiationMarker, StringComparison.Ordinal);
        forcedAggressiveTargetViewId = forcedAggressive && data.Length > 6
            ? ConvertToInt(data[6], 0, 0, int.MaxValue)
            : 0;

        ConfigurePhysics();
        ConfigureStats();
        ConfigureWeapon();
        DisableHumanOnlyComponents();
        EnsureNameplate();
        GameVisualTheme.ApplyPlayerVisual(health);

        int seed = view != null ? view.ViewID + spawnOrdinal * 41 : spawnOrdinal * 97;
        float angle = (Mathf.Abs(seed) * 0.371f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        destination = PickPatrolDestination();

        float tickJitter = Mathf.Abs(seed % 1000) / 1000f;
        nextThinkTime = Time.time + tickJitter * ThinkInterval;
        nextTargetRefreshTime = Time.time + tickJitter * TargetRefreshInterval;
        nextExtractionTargetRefreshTime = Time.time + tickJitter * ExtractionTargetRefreshInterval;
        nextTimerLookupTime = Time.unscaledTime + tickJitter * TimerLookupInterval;
        nextAvoidanceRefreshTime = Time.time + tickJitter * AvoidanceRefreshInterval;
        nextNameplateRefreshTime = Time.time + tickJitter * NameplateRefreshInterval;

        if (forcedAggressive && forcedAggressiveTargetViewId > 0)
        {
            hostilePlayerViewId = forcedAggressiveTargetViewId;
            hostilePlayerUntil = float.PositiveInfinity;
            targetViewId = forcedAggressiveTargetViewId;
            mode = Mode.Combat;
        }

        initialized = true;
        RegisterActiveRider();
    }

    void OnEnable()
    {
        if (initialized)
            RegisterActiveRider();
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
        InitializeFromPhotonData();
        DisableHumanOnlyComponents();
        UpdateNameplate();

        if (view == null || !view.IsMine || !IsGameStarted() || health == null || health.IsWreck || wreckConverted || mode == Mode.Evacuated)
            return;

        if (Time.time >= nextThinkTime)
        {
            nextThinkTime = Time.time + ThinkInterval;
            Think();
        }
    }

    void FixedUpdate()
    {
        if (view == null || !view.IsMine || !IsGameStarted() || health == null || health.IsWreck || wreckConverted || mode == Mode.Evacuated)
            return;

        TickMovement();
        TickAction();
    }

    void OnDisable()
    {
        ReleaseCollectionTarget();
        UnregisterActiveRider();
    }

    void OnDestroy()
    {
        ReleaseCollectionTarget();
        UnregisterActiveRider();
    }

    public void NotifyDamageTaken(int attackerViewId)
    {
        if (attackerViewId <= 0 || view == null || attackerViewId == view.ViewID)
            return;

        PhotonView attackerView = PhotonView.Find(attackerViewId);
        if (attackerView == null || NeutralRiderController.IsNeutralRider(attackerView))
            return;

        PlayerHealth attackerHealth = attackerView.GetComponent<PlayerHealth>();
        if (attackerHealth != null && !attackerHealth.IsWreck && !attackerHealth.IsAstronautControlled && !attackerHealth.IsNeutralRiderControlled && attackerHealth.GetComponent<PlayerDeployableBase>() == null)
        {
            hostilePlayerViewId = attackerViewId;
            hostilePlayerUntil = Time.time + ResolveRivalryDuration();
            AbandonCollectionTarget();
            targetViewId = attackerViewId;
            mode = ShouldFlee() ? Mode.Flee : Mode.Combat;
        }
    }

    public string BuildWreckLootJson()
    {
        if (cargo.Count <= 0)
            cargo.Add(InventoryItemCatalog.SpaceJunkStandardId);

        int capacity = Mathf.Max(cargo.Count, ShipCatalog.GetShipInventoryCapacity(shipSkinIndex));
        string[] slots = new string[capacity];
        for (int i = 0; i < cargo.Count && i < slots.Length; i++)
            slots[i] = cargo[i];

        return PlayerProfileService.SerializeShipInventorySlots(slots);
    }

    public void MarkWreckConverted()
    {
        wreckConverted = true;
        mode = Mode.Evacuated;
        if (nameplate != null)
            nameplate.gameObject.SetActive(false);
        UnregisterActiveRider();
        enabled = false;
    }

    void RegisterActiveRider()
    {
        if (registeredActiveRider || wreckConverted)
            return;

        ActiveRiders.Add(this);
        registeredActiveRider = true;
    }

    void UnregisterActiveRider()
    {
        if (!registeredActiveRider)
            return;

        ActiveRiders.Remove(this);
        registeredActiveRider = false;
    }

    void ConfigurePhysics()
    {
        if (rb == null)
            return;

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.mass = Mathf.Lerp(2.8f, 5.4f, Mathf.Clamp01(shipSkinIndex / (float)Mathf.Max(1, ShipCatalog.MaxShipSkinIndex)));
        rb.linearDamping = 0.12f;
        rb.angularDamping = 0.35f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void ConfigureStats()
    {
        if (health == null)
            return;

        int hp = Mathf.RoundToInt(ShipCatalog.GetBaseHp(shipSkinIndex) * 0.92f);
        int shield = Mathf.RoundToInt(ShipCatalog.GetBaseShield(shipSkinIndex) * 0.72f);
        health.ConfigureBaseStats(Mathf.Max(35, hp), Mathf.Max(10, shield));
    }

    void ConfigureWeapon()
    {
        if (shooting == null)
            return;

        WeaponLoadout loadout = ResolveWeaponLoadout();

        shooting.ConfigureWeaponProfile(
            loadout.FireRate,
            loadout.MaxAmmo,
            loadout.ReloadDuration,
            loadout.Damage,
            loadout.BulletScale,
            loadout.BulletColor,
            loadout.MuzzleOffset,
            false,
            loadout.BulletSpeed,
            loadout.ShotSoundId,
            loadout.RangeMultiplier,
            string.Empty,
            loadout.FlightTime);
    }

    WeaponLoadout ResolveWeaponLoadout()
    {
        int variant = Mathf.Abs((spawnOrdinal * 17) + shipSkinIndex) % 3;
        switch (archetype)
        {
            case NeutralRiderArchetype.Hunter:
                return variant == 0
                    ? Weapon(0.48f, 5, 3.8f, 18, 1.12f, new Color(1f, 0.42f, 0.14f, 1f), 0.58f, 12.8f, "lazer3", 14.5f, 10f)
                    : Weapon(0.34f, 7, 3.2f, 13, 0.96f, new Color(1f, 0.62f, 0.22f, 1f), 0.56f, 11.4f, "lazer2", 13.2f, 10f);

            case NeutralRiderArchetype.Raider:
                return variant == 1
                    ? Weapon(0.26f, 12, 3.7f, 7, 0.72f, new Color(0.42f, 1f, 0.58f, 1f), 0.5f, 10.8f, "shoot_small", 10.8f, 9f)
                    : Weapon(0.36f, 9, 3.1f, 10, 0.86f, new Color(0.28f, 1f, 0.88f, 1f), 0.52f, 11.2f, "lazer1", 11.8f, 9.5f);

            case NeutralRiderArchetype.Coward:
                return variant == 2
                    ? Weapon(0.42f, 8, 3.5f, 8, 0.78f, new Color(0.62f, 0.9f, 1f, 1f), 0.48f, 10f, "lazer1", 10.2f, 8.5f)
                    : Weapon(0.55f, 6, 3.8f, 9, 0.82f, new Color(0.72f, 0.82f, 1f, 1f), 0.5f, 9.4f, "shoot_small", 9.8f, 8.5f);

            default:
                return variant == 0
                    ? Weapon(0.5f, 8, 3.4f, 8, 0.8f, new Color(0.36f, 0.92f, 1f, 1f), 0.5f, 10.2f, "lazer1", 10.5f, 9f)
                    : Weapon(0.44f, 9, 3.2f, 7, 0.76f, new Color(0.22f, 1f, 0.68f, 1f), 0.48f, 10.8f, "shoot_small", 10.8f, 9f);
        }
    }

    static WeaponLoadout Weapon(float fireRate, int maxAmmo, float reloadDuration, int damage, float bulletScale, Color color, float muzzleOffset, float bulletSpeed, string shotSoundId, float rangeMultiplier, float flightTime)
    {
        return new WeaponLoadout
        {
            FireRate = fireRate,
            MaxAmmo = maxAmmo,
            ReloadDuration = reloadDuration,
            Damage = damage,
            BulletScale = bulletScale,
            BulletColor = color,
            MuzzleOffset = muzzleOffset,
            BulletSpeed = bulletSpeed,
            ShotSoundId = shotSoundId,
            RangeMultiplier = rangeMultiplier,
            FlightTime = flightTime
        };
    }

    void DisableHumanOnlyComponents()
    {
        if (humanOnlyComponentsDisabled)
            return;

        DisableComponent<TreasureCollector>();
        DisableComponent<PlayerRepairDocking>();
        DisableComponent<PilotActiveAbilityController>();
        DisableComponent<RoundChatCommandUI>();
        DisableComponent<HealthBarUI>();
        DisableComponent<ShieldBarUI>();
        DisableComponent<BoosterBarUI>();
        DisableComponent<ComplexAmmoBarUI>();
        DisableComponent<SuperAttackUI>();
        DisableComponent<WeaponSwitchButtonUI>();
        DisableComponent<GadgetButtonUI>();
        DisableComponent<ShipInventoryHudUI>();
        DisableComponent<AdvancedMoveInputZone>();
        DisableComponent<AdvancedShootInputZone>();

        humanOnlyDisablePasses++;
        humanOnlyComponentsDisabled = humanOnlyDisablePasses >= HumanOnlyDisablePassCount;
    }

    void DisableComponent<T>() where T : Behaviour
    {
        T component = GetComponent<T>();
        if (component != null)
            component.enabled = false;
    }

    void Think()
    {
        if (forcedAggressive)
        {
            if (forcedAggressiveTargetViewId > 0 && IsValidCombatTarget(forcedAggressiveTargetViewId))
            {
                hostilePlayerViewId = forcedAggressiveTargetViewId;
                hostilePlayerUntil = float.PositiveInfinity;
                targetViewId = forcedAggressiveTargetViewId;
                mode = Mode.Combat;
                return;
            }

            forcedAggressive = false;
        }

        if (ShouldExtract())
        {
            AbandonCollectionTarget();
            mode = Mode.Extract;
            RefreshExtractionTargetIfNeeded(targetViewId <= 0);
            return;
        }

        if (ShouldFlee())
        {
            AbandonCollectionTarget();
            mode = Mode.Flee;
            targetViewId = 0;
            if (destination == Vector2.zero || Vector2.Distance(rb.position, destination) < 1f)
                destination = PickPatrolDestinationAwayFromThreat();
            return;
        }

        if (Time.time >= nextTargetRefreshTime)
        {
            nextTargetRefreshTime = Time.time + TargetRefreshInterval;
            RefreshTarget();
        }

        if (targetViewId > 0)
            return;

        mode = Mode.Patrol;
        if (Vector2.Distance(rb.position, destination) < 1.5f)
            destination = PickPatrolDestination();
    }

    void RefreshTarget()
    {
        if (collectionTargetLocked || collectStartedAt >= 0f)
            return;

        targetViewId = 0;

        if (Time.time < hostilePlayerUntil && hostilePlayerViewId > 0 && IsValidCombatTarget(hostilePlayerViewId))
        {
            targetViewId = hostilePlayerViewId;
            mode = Mode.Combat;
            return;
        }

        int enemyTarget = FindBestEnemyTargetViewId();
        if (enemyTarget > 0)
        {
            targetViewId = enemyTarget;
            mode = Mode.Combat;
            return;
        }

        if (ShouldStartUnprovokedRivalry(out int playerTarget))
        {
            hostilePlayerViewId = playerTarget;
            hostilePlayerUntil = Time.time + ResolveRivalryDuration() * 0.65f;
            targetViewId = playerTarget;
            mode = Mode.Combat;
            return;
        }

        if (HasCargoSpace && FindBestCollectibleViewId(out int collectibleViewId))
        {
            targetViewId = collectibleViewId;
            mode = Mode.Scavenge;
        }
    }

    void TickMovement()
    {
        if (rb == null)
            return;

        Vector2 desired = ResolveDesiredDirection();
        desired = ApplyMapSteering(desired);
        desired = ApplyAvoidance(desired);
        if (desired.sqrMagnitude < 0.001f)
            desired = patrolDirection.sqrMagnitude > 0.001f ? patrolDirection : Vector2.up;

        float speed = ResolveSpeed();
        Vector2 targetVelocity = desired.normalized * speed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVelocity, mode == Mode.Combat ? 0.18f : 0.24f);
        rb.angularVelocity = 0f;

        Vector2 facing = ResolveFacingDirection(desired);
        float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90f;
        rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, angle, 540f * Time.fixedDeltaTime));
    }

    void TickAction()
    {
        if (mode == Mode.Combat)
        {
            TryShootTarget();
            return;
        }

        if (mode == Mode.Scavenge)
        {
            TryCollectTarget();
            return;
        }

        if (mode == Mode.Extract)
        {
            TryUseExtraction();
        }
    }

    Vector2 ResolveDesiredDirection()
    {
        if (mode == Mode.Combat && TryGetTargetPosition(targetViewId, out Vector2 combatPosition))
        {
            Vector2 toTarget = combatPosition - rb.position;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f)
                return patrolDirection;

            Vector2 toward = toTarget / distance;
            Vector2 tangent = spawnOrdinal % 2 == 0 ? new Vector2(-toward.y, toward.x) : new Vector2(toward.y, -toward.x);
            if (distance > 8f)
                return (toward * 0.85f + tangent * 0.18f).normalized;
            if (distance < 4.2f)
                return (-toward * 0.55f + tangent * 0.82f).normalized;

            return (tangent * 0.82f + toward * 0.2f).normalized;
        }

        if ((mode == Mode.Scavenge || mode == Mode.Extract) && TryGetTargetPosition(targetViewId, out Vector2 targetPosition))
            return (targetPosition - rb.position).normalized;

        if (mode == Mode.Flee)
            return ResolveFleeDirection();

        return (destination - rb.position).normalized;
    }

    Vector2 ResolveFacingDirection(Vector2 moveDirection)
    {
        if (mode == Mode.Combat && TryGetTargetPosition(targetViewId, out Vector2 targetPosition))
            return (targetPosition - rb.position).normalized;

        return moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : transform.up;
    }

    float ResolveSpeed()
    {
        float baseSpeed = ShipCatalog.GetBaseSpeed(shipSkinIndex);
        float multiplier = mode switch
        {
            Mode.Flee => 1.16f,
            Mode.Extract => 1.08f,
            Mode.Combat => 0.88f,
            Mode.Scavenge => 0.72f,
            _ => 0.78f
        };

        return Mathf.Clamp(baseSpeed * multiplier, 2.2f, 6.8f);
    }

    Vector2 ApplyAvoidance(Vector2 desired)
    {
        if (rb == null)
            return desired;

        if (Time.time < nextAvoidanceRefreshTime)
        {
            if (cachedAvoidanceDirection.sqrMagnitude <= 0.001f)
                return desired;

            return (desired.normalized + cachedAvoidanceDirection.normalized * 0.7f).normalized;
        }

        nextAvoidanceRefreshTime = Time.time + AvoidanceRefreshInterval;
        Vector2 avoidance = Vector2.zero;
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(rb.position, AvoidanceRadius, out Collider2D[] hits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb || hit.isTrigger)
                continue;

            if (hit.GetComponentInParent<Treasure>() != null ||
                hit.GetComponentInParent<DroppedCargoCrate>() != null)
            {
                continue;
            }

            Vector2 away = rb.position - hit.ClosestPoint(rb.position);
            if (away.sqrMagnitude < 0.001f)
                away = rb.position - (Vector2)hit.transform.position;
            if (away.sqrMagnitude > 0.001f)
                avoidance += away.normalized / Mathf.Max(0.25f, away.magnitude);
        }

        if (avoidance.sqrMagnitude <= 0.001f)
        {
            cachedAvoidanceDirection = Vector2.zero;
            return desired;
        }

        cachedAvoidanceDirection = avoidance.normalized;
        return (desired.normalized + cachedAvoidanceDirection * 0.7f).normalized;
    }

    Vector2 ApplyMapSteering(Vector2 desired)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(2f, mapSize.x * 0.5f - MapMargin);
        float halfY = Mathf.Max(2f, mapSize.y * 0.5f - MapMargin);
        Vector2 inward = Vector2.zero;
        Vector2 position = rb != null ? rb.position : (Vector2)transform.position;

        if (position.x > halfX)
            inward.x -= 1f;
        else if (position.x < -halfX)
            inward.x += 1f;

        if (position.y > halfY)
            inward.y -= 1f;
        else if (position.y < -halfY)
            inward.y += 1f;

        if (inward.sqrMagnitude <= 0.001f)
            return desired;

        return (desired.normalized * 0.35f + inward.normalized * 0.95f).normalized;
    }

    Vector2 ResolveFleeDirection()
    {
        if (hostilePlayerViewId > 0 && TryGetTargetPosition(hostilePlayerViewId, out Vector2 threat))
        {
            Vector2 away = rb.position - threat;
            if (away.sqrMagnitude > 0.001f)
                return away.normalized;
        }

        return (destination - rb.position).normalized;
    }

    void TryShootTarget()
    {
        if (!TryGetTargetPosition(targetViewId, out Vector2 targetPosition))
            return;

        Vector2 toTarget = targetPosition - (Vector2)transform.position;
        if (toTarget.sqrMagnitude > CombatRange * CombatRange)
            return;

        if (shooting != null)
            shooting.TryFireBot(toTarget.normalized);
    }

    void TryCollectTarget()
    {
        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null || !HasCargoSpace)
        {
            ResetCollection();
            return;
        }

        float range = Vector2.Distance(transform.position, targetView.transform.position);
        if (range > CollectRange)
        {
            ReleaseCollectionTarget(false);
            collectStartedAt = -1f;
            return;
        }

        if (collectStartedAt < 0f)
        {
            collectStartedAt = Time.time;
            collectionTargetLocked = true;
            SetCollectibleBusy(targetView, true);
            StartCollectionBeam(targetView.ViewID);
            return;
        }

        float required = targetView.GetComponent<ShipWreck>() != null ? WreckCollectSeconds : CollectSeconds;
        if (Time.time - collectStartedAt < required)
            return;

        ResolveCollectible(targetView);
        ResetCollection();
    }

    void TryUseExtraction()
    {
        PhotonView zoneView = PhotonView.Find(targetViewId);
        ExtractionZone zone = zoneView != null ? zoneView.GetComponent<ExtractionZone>() : null;
        if (zone == null)
        {
            targetViewId = 0;
            RefreshExtractionTargetIfNeeded(false);
            extractionUseStartedAt = -1f;
            return;
        }

        float distance = zone.GetInteractionDistanceToPoint(transform.position);
        if (distance > 1.25f)
        {
            extractionUseStartedAt = -1f;
            return;
        }

        if (extractionUseStartedAt < 0f)
        {
            extractionUseStartedAt = Time.time;
            return;
        }

        if (Time.time - extractionUseStartedAt < ExtractionUseSeconds)
            return;

        if (!zone.IsActive && !zone.IsTransitioning && zone.photonView != null)
        {
            zone.TryUseNeutralRider(view);
            extractionUseStartedAt = Time.time + 1.4f;
            return;
        }

        if (zone.IsActive)
            EvacuateAt(zone);
    }

    void EvacuateAt(ExtractionZone zone)
    {
        StopCollectionBeam();
        mode = Mode.Evacuated;
        wreckConverted = true;
        if (view != null)
            view.RPC(nameof(NeutralRiderEvacuatedRpc), RpcTarget.All, zone.transform.position.x, zone.transform.position.y);
    }

    [PunRPC]
    void NeutralRiderEvacuatedRpc(float targetX, float targetY)
    {
        StopCollectionBeam();
        mode = Mode.Evacuated;
        wreckConverted = true;
        StartCoroutine(EvacuateRoutine(new Vector2(targetX, targetY)));
    }

    System.Collections.IEnumerator EvacuateRoutine(Vector2 target)
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        float duration = 1.2f;
        float elapsed = 0f;
        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPosition, new Vector3(target.x, target.y, startPosition.z), t);
            transform.localScale = Vector3.Lerp(startScale, startScale * 0.05f, t);
            yield return null;
        }

        if (PhotonNetwork.IsMasterClient && view != null)
            PhotonNetwork.Destroy(gameObject);
    }

    bool ResolveCollectible(PhotonView targetView)
    {
        Treasure treasure = targetView.GetComponent<Treasure>();
        if (treasure != null && !string.IsNullOrWhiteSpace(treasure.itemId))
        {
            AddCargo(treasure.itemId);
            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.Destroy(treasure.gameObject);
            return true;
        }

        DroppedCargoCrate crate = targetView.GetComponent<DroppedCargoCrate>();
        if (crate != null && crate.HasLoot && !string.IsNullOrWhiteSpace(crate.StoredItemId))
        {
            AddCargo(crate.StoredItemId);
            targetView.RPC(nameof(DroppedCargoCrate.ClearStoredItemRpc), RpcTarget.All);
            return true;
        }

        ShipWreck wreck = targetView.GetComponent<ShipWreck>();
        if (wreck != null && wreck.HasLoot)
        {
            int lootIndex = wreck.GetFirstLootIndex();
            string itemId = wreck.GetLootItemAt(lootIndex);
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                AddCargo(itemId);
                targetView.RPC(nameof(ShipWreck.RemoveLootAtIndexRpc), RpcTarget.All, lootIndex);
                return true;
            }
        }

        return false;
    }

    void AddCargo(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !HasCargoSpace)
            return;

        cargo.Add(itemId);
    }

    void ResetCollection()
    {
        ReleaseCollectionTarget();

        collectStartedAt = -1f;
        mode = Mode.Patrol;
    }

    void AbandonCollectionTarget()
    {
        if (collectionTargetLocked || collectStartedAt >= 0f)
            ReleaseCollectionTarget();

        collectStartedAt = -1f;
    }

    void ReleaseCollectionTarget()
    {
        ReleaseCollectionTarget(true);
    }

    void ReleaseCollectionTarget(bool clearTarget)
    {
        if (targetViewId <= 0)
            return;

        int releasedViewId = targetViewId;
        if (clearTarget)
            targetViewId = 0;

        if (!collectionTargetLocked)
        {
            StopCollectionBeam();
            return;
        }

        collectionTargetLocked = false;
        StopCollectionBeam();
        if (PhotonNetwork.InRoom)
        {
            PhotonView targetView = PhotonView.Find(releasedViewId);
            if (targetView != null)
                SetCollectibleBusy(targetView, false);
        }
    }

    void StartCollectionBeam(int targetViewIdToCollect)
    {
        if (view == null || targetViewIdToCollect <= 0)
            return;

        collectingBeamTargetViewId = targetViewIdToCollect;
        view.RPC(nameof(StartNeutralRiderCollectionBeamRpc), RpcTarget.All, targetViewIdToCollect);
    }

    void StopCollectionBeam()
    {
        if (view == null || collectingBeamTargetViewId <= 0)
            return;

        collectingBeamTargetViewId = 0;
        view.RPC(nameof(StopNeutralRiderCollectionBeamRpc), RpcTarget.All);
    }

    [PunRPC]
    void StartNeutralRiderCollectionBeamRpc(int targetViewIdToCollect)
    {
        NeutralRiderCollectionBeamVfx.StartBeam(view != null ? view.ViewID : 0, targetViewIdToCollect);
    }

    [PunRPC]
    void StopNeutralRiderCollectionBeamRpc()
    {
        NeutralRiderCollectionBeamVfx.StopBeam(view != null ? view.ViewID : 0);
    }

    void SetCollectibleBusy(PhotonView targetView, bool busy)
    {
        if (targetView == null)
            return;

        Treasure treasure = targetView.GetComponent<Treasure>();
        if (treasure != null)
        {
            treasure.isBeingCollected = busy;
            targetView.RPC(nameof(Treasure.SetBeingCollectedRpc), RpcTarget.All, busy);
            return;
        }

        DroppedCargoCrate crate = targetView.GetComponent<DroppedCargoCrate>();
        if (crate != null)
        {
            crate.isBeingCollected = busy;
            targetView.RPC(nameof(DroppedCargoCrate.SetBeingCollectedRpc), RpcTarget.All, busy);
            return;
        }

        ShipWreck wreck = targetView.GetComponent<ShipWreck>();
        if (wreck != null)
        {
            wreck.isBeingCollected = busy;
            targetView.RPC(nameof(ShipWreck.SetBeingCollectedRpc), RpcTarget.All, busy);
        }
    }

    bool FindBestCollectibleViewId(out int viewId)
    {
        viewId = 0;
        float bestScore = float.MaxValue;
        Vector2 origin = transform.position;

        Treasure[] treasures = RuntimeSceneQueryCache.GetTreasures();
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || !treasure.gameObject.activeInHierarchy || treasure.isBeingCollected || string.IsNullOrWhiteSpace(treasure.itemId))
                continue;

            PhotonView candidateView = treasure.photonView;
            if (candidateView == null)
                continue;

            float score = Vector2.Distance(origin, treasure.transform.position) - Mathf.Clamp(InventoryItemCatalog.GetSellValueAstrons(treasure.itemId) / 240f, 0f, 5f);
            if (score < bestScore)
            {
                bestScore = score;
                viewId = candidateView.ViewID;
            }
        }

        DroppedCargoCrate[] crates = RuntimeSceneQueryCache.GetDroppedCargoCrates();
        for (int i = 0; i < crates.Length; i++)
        {
            DroppedCargoCrate crate = crates[i];
            if (crate == null || !crate.gameObject.activeInHierarchy || crate.isBeingCollected || !crate.HasLoot)
                continue;

            PhotonView candidateView = crate.photonView;
            if (candidateView == null)
                continue;

            float score = Vector2.Distance(origin, crate.transform.position) - 2.2f;
            if (score < bestScore)
            {
                bestScore = score;
                viewId = candidateView.ViewID;
            }
        }

        ShipWreck[] wrecks = RuntimeSceneQueryCache.GetShipWrecks();
        for (int i = 0; i < wrecks.Length; i++)
        {
            ShipWreck wreck = wrecks[i];
            if (wreck == null || !wreck.gameObject.activeInHierarchy || wreck.isBeingCollected || !wreck.HasLoot)
                continue;

            PhotonView candidateView = wreck.photonView;
            if (candidateView == null || candidateView.ViewID == (view != null ? view.ViewID : 0))
                continue;

            float score = Vector2.Distance(origin, wreck.transform.position) - 1.4f;
            if (score < bestScore)
            {
                bestScore = score;
                viewId = candidateView.ViewID;
            }
        }

        return viewId > 0;
    }

    int FindBestEnemyTargetViewId()
    {
        PlayerHealth[] candidates = RuntimeSceneQueryCache.GetPlayers();
        float bestScore = float.MaxValue;
        int bestViewId = 0;
        Vector2 origin = transform.position;

        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerHealth candidate = candidates[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating)
                continue;

            EnemyBot enemy = candidate.GetComponent<EnemyBot>();
            if (enemy == null || enemy.Kind == EnemyBotKind.SpaceMine || enemy.IsRescueShip)
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance > DetectionRange)
                continue;

            float score = distance + (enemy.IsPirateFighter ? -3f : 0f);
            if (score < bestScore)
            {
                bestScore = score;
                bestViewId = candidate.photonView != null ? candidate.photonView.ViewID : 0;
            }
        }

        return bestViewId;
    }

    bool ShouldStartUnprovokedRivalry(out int playerViewId)
    {
        playerViewId = 0;
        float chance = RoomSettings.GetNeutralRiderAggression() switch
        {
            RoomSettings.NeutralRiderAggressionHigh => archetype == NeutralRiderArchetype.Raider ? 0.34f : 0.18f,
            RoomSettings.NeutralRiderAggressionLow => archetype == NeutralRiderArchetype.Raider ? 0.08f : 0.02f,
            _ => archetype == NeutralRiderArchetype.Raider ? 0.18f : 0.06f
        };

        if (archetype == NeutralRiderArchetype.Coward || UnityEngine.Random.value > chance)
            return false;

        PlayerHealth player = FindNearestHumanPlayer(PlayerRivalryRange);
        if (player == null || player.photonView == null)
            return false;

        playerViewId = player.photonView.ViewID;
        return true;
    }

    PlayerHealth FindNearestHumanPlayer(float maxRange)
    {
        PlayerHealth[] candidates = RuntimeSceneQueryCache.GetPlayers();
        PlayerHealth best = null;
        float bestDistance = maxRange;
        Vector2 origin = transform.position;

        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerHealth candidate = candidates[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy || !ActorIdentity.IsHumanPlayerActor(candidate) || candidate.IsWreck || candidate.IsEvacuationAnimating)
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    int FindBestExtractionViewId()
    {
        ExtractionZone[] zones = RuntimeSceneQueryCache.GetExtractionZones();
        float bestDistance = float.MaxValue;
        int bestViewId = 0;
        Vector2 origin = transform.position;

        for (int i = 0; i < zones.Length; i++)
        {
            ExtractionZone zone = zones[i];
            if (zone == null || !zone.gameObject.activeInHierarchy || zone.IsEvacuating)
                continue;

            PhotonView zoneView = zone.photonView;
            if (zoneView == null)
                continue;

            float distance = Vector2.Distance(origin, zone.transform.position);
            if (zone.IsActive)
                distance -= 8f;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestViewId = zoneView.ViewID;
            }
        }

        return bestViewId;
    }

    bool IsValidCombatTarget(int candidateViewId)
    {
        PhotonView candidateView = PhotonView.Find(candidateViewId);
        PlayerHealth candidateHealth = candidateView != null ? candidateView.GetComponent<PlayerHealth>() : null;
        return candidateHealth != null &&
               candidateHealth != health &&
               !candidateHealth.IsWreck &&
               !candidateHealth.IsEvacuationAnimating &&
               !candidateHealth.IsNeutralRiderControlled;
    }

    bool TryGetTargetPosition(int candidateViewId, out Vector2 position)
    {
        PhotonView candidateView = PhotonView.Find(candidateViewId);
        if (candidateView != null)
        {
            position = candidateView.transform.position;
            return true;
        }

        position = Vector2.zero;
        return false;
    }

    bool ShouldExtract()
    {
        float elapsed = GetElapsedRoundSeconds();
        if (elapsed < MinimumExtractionElapsedSeconds && archetype != NeutralRiderArchetype.Coward)
            return false;

        int cargoValue = CargoValueAstrons;
        float cargoThreshold = ResolveExtractionCargoThreshold();
        if (cargoValue >= cargoThreshold)
            return true;

        if (ShouldFlee() && cargoValue >= FleeExtractionMinimumCargoValue)
            return true;

        GameTimer timer = ResolveGameTimer();
        return timer != null && timer.GetCurrentRemainingTime() <= ResolveExtractionEndgameSeconds() && cargoValue > 0;
    }

    float ResolveExtractionCargoThreshold()
    {
        return archetype switch
        {
            NeutralRiderArchetype.Coward => 1250f,
            NeutralRiderArchetype.Scavenger => ExtractionCargoValue,
            NeutralRiderArchetype.Raider => 2300f,
            NeutralRiderArchetype.Hunter => 2600f,
            _ => ExtractionCargoValue
        };
    }

    float ResolveExtractionEndgameSeconds()
    {
        return archetype switch
        {
            NeutralRiderArchetype.Coward => 36f,
            NeutralRiderArchetype.Scavenger => ExtractionEndgameSeconds,
            NeutralRiderArchetype.Raider => 24f,
            NeutralRiderArchetype.Hunter => 18f,
            _ => ExtractionEndgameSeconds
        };
    }

    float GetElapsedRoundSeconds()
    {
        GameTimer timer = ResolveGameTimer();
        if (timer == null)
            return 0f;

        return Mathf.Max(0f, RoomSettings.GetRoundDuration() - timer.GetCurrentRemainingTime());
    }

    void RefreshExtractionTargetIfNeeded(bool force)
    {
        if (!force && Time.time < nextExtractionTargetRefreshTime)
            return;

        nextExtractionTargetRefreshTime = Time.time + ExtractionTargetRefreshInterval;
        targetViewId = FindBestExtractionViewId();
    }

    GameTimer ResolveGameTimer()
    {
        if (cachedTimer != null)
            return cachedTimer;

        if (Time.unscaledTime < nextTimerLookupTime)
            return null;

        nextTimerLookupTime = Time.unscaledTime + TimerLookupInterval;
        cachedTimer = FindAnyObjectByType<GameTimer>();
        return cachedTimer;
    }

    bool ShouldFlee()
    {
        if (health == null || health.MaxShield + health.CurrentHP <= 0)
            return false;

        float ratio = health.CurrentHP / (float)Mathf.Max(1, ShipCatalog.GetBaseHp(shipSkinIndex));
        float threshold = archetype == NeutralRiderArchetype.Coward ? 0.58f : FleeHealthRatio;
        return ratio <= threshold;
    }

    float ResolveRivalryDuration()
    {
        return RoomSettings.GetNeutralRiderAggression() switch
        {
            RoomSettings.NeutralRiderAggressionHigh => 22f,
            RoomSettings.NeutralRiderAggressionLow => 8f,
            _ => 14f
        };
    }

    Vector2 PickPatrolDestination()
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(4f, mapSize.x * 0.5f - MapMargin);
        float halfY = Mathf.Max(4f, mapSize.y * 0.5f - MapMargin);
        return new Vector2(UnityEngine.Random.Range(-halfX, halfX), UnityEngine.Random.Range(-halfY, halfY));
    }

    Vector2 PickPatrolDestinationAwayFromThreat()
    {
        Vector2 destinationCandidate = PickPatrolDestination();
        if (hostilePlayerViewId <= 0 || !TryGetTargetPosition(hostilePlayerViewId, out Vector2 threat))
            return destinationCandidate;

        Vector2 away = ((Vector2)transform.position - threat).normalized;
        if (away.sqrMagnitude <= 0.001f)
            away = patrolDirection;

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(4f, mapSize.x * 0.5f - MapMargin);
        float halfY = Mathf.Max(4f, mapSize.y * 0.5f - MapMargin);
        Vector2 candidate = (Vector2)transform.position + away * 12f;
        candidate.x = Mathf.Clamp(candidate.x, -halfX, halfX);
        candidate.y = Mathf.Clamp(candidate.y, -halfY, halfY);
        return candidate;
    }

    void EnsureNameplate()
    {
        if (nameplate != null)
            return;

        GameObject labelObject = new GameObject("NeutralRiderNameplate", typeof(RectTransform), typeof(TextMeshPro));
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, NameplateYOffset, -0.05f);
        nameplate = labelObject.GetComponent<TextMeshPro>();
        nameplate.text = riderName;
        nameplate.fontSize = 2.4f;
        nameplate.alignment = TextAlignmentOptions.Center;
        nameplate.color = new Color(0.62f, 0.95f, 1f, 0.9f);
        nameplate.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        nameplate.sortingOrder = GameVisualTheme.PlayerSortingOrder + 6;
    }

    void UpdateNameplate()
    {
        if (nameplate == null)
            return;

        if (Time.time < nextNameplateRefreshTime)
            return;

        nextNameplateRefreshTime = Time.time + NameplateRefreshInterval;
        if (!string.Equals(lastNameplateText, riderName, StringComparison.Ordinal))
        {
            nameplate.text = riderName;
            lastNameplateText = riderName;
        }

        nameplate.transform.rotation = Quaternion.identity;
        bool visible = !wreckConverted && mode != Mode.Evacuated;
        if (nameplateVisible != visible)
        {
            nameplate.gameObject.SetActive(visible);
            nameplateVisible = visible;
        }
    }

    bool IsGameStarted()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
               value is bool started &&
               started;
    }

    static int ConvertToInt(object value, int fallback, int min, int max)
    {
        int result = fallback;
        if (value is int intValue)
            result = intValue;
        else if (value is byte byteValue)
            result = byteValue;
        else if (value is short shortValue)
            result = shortValue;
        else if (value is long longValue)
            result = longValue > int.MaxValue ? int.MaxValue : longValue < int.MinValue ? int.MinValue : (int)longValue;
        else if (value is float floatValue)
            result = Mathf.RoundToInt(floatValue);
        else if (value is double doubleValue)
            result = Mathf.RoundToInt((float)doubleValue);

        return Mathf.Clamp(result, min, max);
    }
}

public sealed class NeutralRiderCollectionBeamVfx : MonoBehaviour
{
    const int BeamPointCount = 13;
    const float BeamWidth = 0.11f;
    const float BeamJitterAmplitude = 0.085f;
    const float BeamJitterFrequency = 17f;
    const float BeamZOffset = -0.35f;
    const int SortingOrderOffset = 36;

    static readonly Dictionary<int, NeutralRiderCollectionBeamVfx> ActiveBySourceViewId = new Dictionary<int, NeutralRiderCollectionBeamVfx>();
    static Material sharedMaterial;

    Transform source;
    Transform target;
    LineRenderer beam;
    int sourceViewId;
    int targetViewId;
    int sortingLayerId;
    int sortingOrder = 50;

    public static void StartBeam(int sourcePhotonViewId, int targetPhotonViewId)
    {
        StopBeam(sourcePhotonViewId);

        PhotonView sourceView = PhotonView.Find(sourcePhotonViewId);
        PhotonView targetView = PhotonView.Find(targetPhotonViewId);
        if (sourceView == null || targetView == null)
            return;

        GameObject effect = new GameObject("NeutralRiderCollectionBeamVfx_" + sourcePhotonViewId);
        NeutralRiderCollectionBeamVfx vfx = effect.AddComponent<NeutralRiderCollectionBeamVfx>();
        vfx.Initialize(sourceView.transform, targetView.transform, sourcePhotonViewId, targetPhotonViewId);
        ActiveBySourceViewId[sourcePhotonViewId] = vfx;
    }

    public static void StopBeam(int sourcePhotonViewId)
    {
        if (!ActiveBySourceViewId.TryGetValue(sourcePhotonViewId, out NeutralRiderCollectionBeamVfx vfx))
            return;

        ActiveBySourceViewId.Remove(sourcePhotonViewId);
        if (vfx != null)
            Destroy(vfx.gameObject);
    }

    void Initialize(Transform sourceTransform, Transform targetTransform, int resolvedSourceViewId, int resolvedTargetViewId)
    {
        source = sourceTransform;
        target = targetTransform;
        sourceViewId = resolvedSourceViewId;
        targetViewId = resolvedTargetViewId;

        SpriteRenderer sourceRenderer = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
        if (sourceRenderer != null)
        {
            sortingLayerId = sourceRenderer.sortingLayerID;
            sortingOrder = sourceRenderer.sortingOrder + SortingOrderOffset;
        }

        if (source != null)
            gameObject.layer = source.gameObject.layer;

        beam = CreateBeam();
    }

    void Update()
    {
        if (source == null || target == null)
        {
            StopBeam(sourceViewId);
            return;
        }

        UpdateBeam();
    }

    void OnDestroy()
    {
        if (ActiveBySourceViewId.TryGetValue(sourceViewId, out NeutralRiderCollectionBeamVfx active) && active == this)
            ActiveBySourceViewId.Remove(sourceViewId);
    }

    void UpdateBeam()
    {
        if (beam == null)
            return;

        Vector2 start = GetSourcePoint();
        Vector2 end = GetTargetPoint(start);
        Vector2 delta = end - start;
        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : (Vector2)source.up;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float pulse = Mathf.Sin((Time.time * 14f) + targetViewId * 0.09f) * 0.5f + 0.5f;
        float alpha = Mathf.Lerp(0.72f, 1f, pulse);
        beam.enabled = true;
        beam.colorGradient = BuildCollectionBeamGradient(alpha);
        beam.widthMultiplier = Mathf.Lerp(BeamWidth * 0.72f, BeamWidth * 1.3f, pulse);

        for (int i = 0; i < beam.positionCount; i++)
        {
            float t = i / (float)(beam.positionCount - 1);
            Vector2 point = Vector2.Lerp(start, end, t);
            float taper = Mathf.Sin(t * Mathf.PI);
            float waveA = Mathf.Sin((t * Mathf.PI * 5f) + Time.time * BeamJitterFrequency);
            float waveB = Mathf.Sin((t * Mathf.PI * 11f) - Time.time * 13f) * 0.45f;
            float jitter = (waveA + waveB) * BeamJitterAmplitude * taper;
            point += perpendicular * jitter;
            beam.SetPosition(i, new Vector3(point.x, point.y, BeamZOffset));
        }
    }

    Vector2 GetSourcePoint()
    {
        float forwardOffset = 0.55f;
        SpriteRenderer renderer = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
        if (renderer != null)
            forwardOffset = Mathf.Max(0.4f, renderer.bounds.extents.y * 0.9f);

        return source != null ? (Vector2)source.position + (Vector2)source.up * forwardOffset : Vector2.zero;
    }

    Vector2 GetTargetPoint(Vector2 sourcePoint)
    {
        Collider2D collider = target != null ? target.GetComponent<Collider2D>() : null;
        if (collider != null)
            return collider.ClosestPoint(sourcePoint);

        return target != null ? (Vector2)target.position : sourcePoint;
    }

    LineRenderer CreateBeam()
    {
        GameObject beamObject = new GameObject("NeutralRiderCollectionBeam");
        beamObject.transform.SetParent(transform, false);
        if (source != null)
            beamObject.layer = source.gameObject.layer;

        LineRenderer line = beamObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.positionCount = BeamPointCount;
        line.widthMultiplier = BeamWidth;
        line.startWidth = BeamWidth;
        line.endWidth = BeamWidth * 0.55f;
        line.numCapVertices = 12;
        line.numCornerVertices = 10;
        line.material = GetMaterial();
        line.colorGradient = BuildCollectionBeamGradient(1f);
        line.widthCurve = BuildCollectionBeamWidthCurve();
        line.textureMode = LineTextureMode.Stretch;
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        line.enabled = false;
        return line;
    }

    static Gradient BuildCollectionBeamGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.96f, 1f, 0.86f), 0f),
                new GradientColorKey(new Color(0.28f, 1f, 0.66f), 0.38f),
                new GradientColorKey(new Color(0.1f, 0.74f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f * alpha, 0f),
                new GradientAlphaKey(0.72f * alpha, 0.55f),
                new GradientAlphaKey(0.18f * alpha, 1f)
            });
        return gradient;
    }

    static AnimationCurve BuildCollectionBeamWidthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.62f),
            new Keyframe(0.18f, 1.2f),
            new Keyframe(0.58f, 0.82f),
            new Keyframe(1f, 0.22f));
    }

    static Material GetMaterial()
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        sharedMaterial = new Material(shader)
        {
            name = "NeutralRiderCollectionBeamMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3350;
        return sharedMaterial;
    }
}
