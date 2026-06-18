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
    const float HitAndRunBreakAwayDuration = 1.85f;
    const float HitAndRunMinimumAttackDuration = 0.95f;
    const float BotUtilityGlobalCooldownMin = 6f;
    const float BotUtilityGlobalCooldownMax = 11f;
    const float BotUtilityUseChance = 0.62f;
    const float BotLootingFriendRange = 4.25f;
    const float BotLootingFriendCollectSeconds = 4.8f;
    const float BotLootingFriendScanInterval = 0.55f;
    const float BotEscapePodHpRatio = 0.16f;

    static readonly HashSet<NeutralRiderController> ActiveRiders = new HashSet<NeutralRiderController>();

    enum CombatTactic
    {
        Orbit,
        HitAndRun,
        Kite,
        Brawler,
        Avoidant
    }

    sealed class BotUtilityRuntime
    {
        public string ItemId;
        public int Charges;
        public float Cooldown;
        public float NextUseTime;
    }

    struct NeutralRiderCombatLoadout
    {
        public string WeaponId;
        public WeaponAttackProfile WeaponProfile;
        public CombatTactic Tactic;
        public string[] GadgetItemIds;
        public bool HasFiringFriend;
        public bool HasLootingFriend;
        public bool HasEscapePod;
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
    LootingFriendController lootingFriend;
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
    int loadoutSeed;
    NeutralRiderCombatLoadout combatLoadout;
    readonly List<BotUtilityRuntime> botUtilities = new List<BotUtilityRuntime>();
    string riderName = "Rider";
    GameTimer cachedTimer;
    bool initialized;
    bool wreckConverted;
    bool collectionTargetLocked;
    bool forcedAggressive;
    bool humanOnlyComponentsDisabled;
    bool registeredActiveRider;
    bool nameplateVisible;
    bool botRescueUsed;
    bool fallbackJunkStolenByLootHook;
    string lastNameplateText = string.Empty;
    int forcedAggressiveTargetViewId;
    float hitAndRunBreakAwayUntil;
    float hitAndRunAttackStartedAt;
    Vector2 hitAndRunBreakAwayDirection = Vector2.up;
    float nextBotUtilityUseTime;
    int botLootingFriendTargetViewId;
    float botLootingFriendStartedAt = -1f;
    float nextBotLootingFriendScanTime;

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

    public bool TrySelectLootHookCargo(out string itemId)
    {
        itemId = string.Empty;
        if (!CanLoseLootHookCargo())
            return false;

        int cargoIndex = GetRandomLootHookCargoIndex();
        if (cargoIndex >= 0)
        {
            itemId = cargo[cargoIndex];
            return !string.IsNullOrWhiteSpace(itemId);
        }

        if (!fallbackJunkStolenByLootHook)
        {
            itemId = InventoryItemCatalog.SpaceJunkStandardId;
            return true;
        }

        return false;
    }

    public bool TryRemoveLootHookCargo(string expectedItemId, out string removedItemId)
    {
        removedItemId = string.Empty;
        if (!CanLoseLootHookCargo() || string.IsNullOrWhiteSpace(expectedItemId))
            return false;

        int cargoIndex = GetRandomLootHookCargoIndex(expectedItemId);
        if (cargoIndex >= 0)
        {
            removedItemId = cargo[cargoIndex];
            cargo.RemoveAt(cargoIndex);
            return !string.IsNullOrWhiteSpace(removedItemId);
        }

        if (!fallbackJunkStolenByLootHook &&
            string.Equals(expectedItemId, InventoryItemCatalog.SpaceJunkStandardId, StringComparison.Ordinal))
        {
            fallbackJunkStolenByLootHook = true;
            removedItemId = InventoryItemCatalog.SpaceJunkStandardId;
            return true;
        }

        return false;
    }

    public void RestoreLootHookCargo(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        InitializeFromPhotonData();
        if (string.Equals(itemId, InventoryItemCatalog.SpaceJunkStandardId, StringComparison.Ordinal) &&
            fallbackJunkStolenByLootHook)
        {
            fallbackJunkStolenByLootHook = false;
            if (cargo.Count <= 0)
                return;
        }

        AddCargo(itemId);
    }

    bool CanLoseLootHookCargo()
    {
        InitializeFromPhotonData();
        return initialized &&
               gameObject.activeInHierarchy &&
               !wreckConverted &&
               mode != Mode.Evacuated &&
               health != null &&
               !health.IsWreck;
    }

    int GetRandomLootHookCargoIndex(string requiredItemId = null)
    {
        List<int> candidateIndexes = new List<int>();
        for (int i = 0; i < cargo.Count; i++)
        {
            string candidateItemId = cargo[i];
            if (string.IsNullOrWhiteSpace(candidateItemId))
                continue;

            if (!string.IsNullOrWhiteSpace(requiredItemId) &&
                !string.Equals(candidateItemId, requiredItemId, StringComparison.Ordinal))
            {
                continue;
            }

            candidateIndexes.Add(i);
        }

        if (candidateIndexes.Count <= 0)
            return -1;

        return candidateIndexes[UnityEngine.Random.Range(0, candidateIndexes.Count)];
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

    public static object[] BuildInstantiationData(int skinIndex, NeutralRiderArchetype archetype, string name, int ordinal, int configuredLoadoutSeed = 0)
    {
        return new object[]
        {
            InstantiationMarker,
            Mathf.Clamp(skinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex),
            (int)archetype,
            string.IsNullOrWhiteSpace(name) ? "Rider" : name,
            Mathf.Max(0, ordinal),
            Mathf.Max(0, configuredLoadoutSeed)
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
        lootingFriend = GetComponent<LootingFriendController>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        object[] data = view.InstantiationData;
        shipSkinIndex = GetShipSkinIndexFromInstantiationData(data);
        archetype = data != null && data.Length > 2
            ? (NeutralRiderArchetype)Mathf.Clamp(ConvertToInt(data[2], 0, 0, 3), 0, 3)
            : NeutralRiderArchetype.Scavenger;
        riderName = GetNameFromInstantiationData(data);
        spawnOrdinal = data != null && data.Length > 4 ? ConvertToInt(data[4], 0, 0, 999) : 0;
        int fallbackLoadoutSeed = BuildFallbackLoadoutSeed(spawnOrdinal, shipSkinIndex, archetype);
        loadoutSeed = data != null && data.Length > 5 && data[5] is not string
            ? ConvertToInt(data[5], fallbackLoadoutSeed, 0, int.MaxValue)
            : fallbackLoadoutSeed;
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
            mode = CanUseMainWeapon() ? Mode.Combat : Mode.Flee;
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

        if (HackingStatus.TryGetForcedMotion(gameObject, out Vector2 hackedDirection, out float hackedSpeedMultiplier))
        {
            TickHackedOverride(hackedDirection, hackedSpeedMultiplier);
            return;
        }

        TickMovement();
        TickAction();
    }

    void OnDisable()
    {
        StopBotLootingFriendTarget(true);
        ReleaseCollectionTarget();
        UnregisterActiveRider();
    }

    void OnDestroy()
    {
        StopBotLootingFriendTarget(true);
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
            mode = ShouldFlee() || !CanUseMainWeapon() ? Mode.Flee : Mode.Combat;
        }
    }

    public string BuildWreckLootJson()
    {
        if (cargo.Count <= 0 && !fallbackJunkStolenByLootHook)
            cargo.Add(InventoryItemCatalog.SpaceJunkStandardId);

        List<string> lootItems = new List<string>(cargo);
        if (TryResolveArrowRaceTokenDrop(out string arrowTokenItemId))
            lootItems.Add(arrowTokenItemId);

        int capacity = Mathf.Max(lootItems.Count, ShipCatalog.GetShipInventoryCapacity(shipSkinIndex));
        string[] slots = new string[capacity];
        for (int i = 0; i < lootItems.Count && i < slots.Length; i++)
            slots[i] = lootItems[i];

        return PlayerProfileService.SerializeShipInventorySlots(slots);
    }

    bool TryResolveArrowRaceTokenDrop(out string tokenItemId)
    {
        tokenItemId = string.Empty;
        if (!InventoryItemCatalog.TryGetArrowRaceTokenForMap(RoomSettings.GetSelectedLobbyMapId(), out string mapTokenId))
            return false;

        ShipType shipType = ShipCatalog.GetShipTypeFromSkinIndex(shipSkinIndex);
        if (shipType == ShipType.Avenger || shipType == ShipType.CargoTruck)
            return false;

        Photon.Realtime.Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            if (!PlayerProfileService.PlayerCanCollectArrowRaceTokens(players[i]))
                continue;

            tokenItemId = mapTokenId;
            return true;
        }

        return false;
    }

    public void MarkWreckConverted()
    {
        StopBotLootingFriendTarget(true);
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

        combatLoadout = ResolveCombatLoadout();
        ConfigureBotUtilities();
        ConfigureBotSupport();

        if (!CanUseMainWeapon() || combatLoadout.WeaponProfile == null)
            return;

        shooting.ConfigureBotWeaponAttackProfile(
            combatLoadout.WeaponProfile,
            ResolveWeaponMuzzleOffset(),
            Mathf.Max(1, ShipCatalog.GetMainGunSlots(shipSkinIndex)),
            false);
    }

    NeutralRiderCombatLoadout ResolveCombatLoadout()
    {
        ShipType shipType = ShipCatalog.GetShipTypeFromSkinIndex(shipSkinIndex);
        if (shipType == ShipType.CargoTruck || ShipCatalog.GetMainGunSlots(shipSkinIndex) <= 0)
        {
            return new NeutralRiderCombatLoadout
            {
                WeaponId = string.Empty,
                WeaponProfile = null,
                Tactic = CombatTactic.Avoidant,
                GadgetItemIds = System.Array.Empty<string>(),
                HasFiringFriend = false,
                HasLootingFriend = archetype == NeutralRiderArchetype.Scavenger,
                HasEscapePod = false
            };
        }

        int seed = ResolveLoadoutSeed(17);
        string weaponId = ResolveBotWeaponId(shipType, seed);
        WeaponAttackProfile profile = WeaponAttackCatalog.GetNormalAttackByWeaponId(weaponId);

        return new NeutralRiderCombatLoadout
        {
            WeaponId = weaponId,
            WeaponProfile = profile,
            Tactic = ResolveCombatTactic(shipType, weaponId, seed),
            GadgetItemIds = ResolveBotGadgetItems(shipType, seed),
            HasFiringFriend = ShipCatalog.GetSupportSlots(shipSkinIndex) > 0 &&
                              (archetype == NeutralRiderArchetype.Hunter || archetype == NeutralRiderArchetype.Raider) &&
                              seed % 5 == 0,
            HasLootingFriend = ShipCatalog.GetSupportSlots(shipSkinIndex) > 0 &&
                               (archetype == NeutralRiderArchetype.Scavenger || seed % 7 == 0),
            HasEscapePod = ShipCatalog.GetRescueSlots(shipSkinIndex) > 0 &&
                           archetype != NeutralRiderArchetype.Hunter &&
                           seed % 4 == 0
        };
    }

    string ResolveBotWeaponId(ShipType shipType, int seed)
    {
        string[] pool;
        switch (shipType)
        {
            case ShipType.Viper:
                pool = new[]
                {
                    WeaponAttackCatalog.GatlingGunId,
                    WeaponAttackCatalog.DoubleIonizerId,
                    WeaponAttackCatalog.RocketLauncherId,
                    WeaponAttackCatalog.RailGunId,
                    WeaponAttackCatalog.PulseDisruptorId,
                    WeaponAttackCatalog.PlasmaGunId
                };
                break;
            case ShipType.Avenger:
                pool = new[]
                {
                    WeaponAttackCatalog.ArtilleryGunId,
                    WeaponAttackCatalog.DoubleRocketLauncherId,
                    WeaponAttackCatalog.RocketLauncherId,
                    WeaponAttackCatalog.RailGunId,
                    WeaponAttackCatalog.GatlingGunId,
                    WeaponAttackCatalog.PulseDisruptorId
                };
                break;
            case ShipType.Arrow:
                pool = new[]
                {
                    WeaponAttackCatalog.GatlingGunId,
                    WeaponAttackCatalog.TripleGunId,
                    WeaponAttackCatalog.PulseDisruptorId,
                    WeaponAttackCatalog.RocketLauncherId,
                    WeaponAttackCatalog.AstroCutterId,
                    WeaponAttackCatalog.PlasmaGunId
                };
                break;
            case ShipType.Invader:
                pool = new[]
                {
                    WeaponAttackCatalog.DoubleIonizerId,
                    WeaponAttackCatalog.PulseDisruptorId,
                    WeaponAttackCatalog.AstroCutterId,
                    WeaponAttackCatalog.DoubleRocketLauncherId,
                    WeaponAttackCatalog.RailGunId,
                    WeaponAttackCatalog.PlasmaGunId
                };
                break;
            case ShipType.Pathfinder:
                pool = new[]
                {
                    WeaponAttackCatalog.RailGunId,
                    WeaponAttackCatalog.PulseDisruptorId,
                    WeaponAttackCatalog.AstroCutterId,
                    WeaponAttackCatalog.DoubleIonizerId,
                    WeaponAttackCatalog.ArtilleryGunId,
                    WeaponAttackCatalog.RocketLauncherId
                };
                break;
            default:
                pool = new[]
                {
                    WeaponAttackCatalog.SimpleGunId,
                    WeaponAttackCatalog.PlasmaGunId,
                    WeaponAttackCatalog.TripleGunId,
                    WeaponAttackCatalog.GatlingGunId,
                    WeaponAttackCatalog.ArtilleryGunId,
                    WeaponAttackCatalog.RocketLauncherId,
                    WeaponAttackCatalog.RailGunId
                };
                break;
        }

        if (seed % 100 < 28)
        {
            string[] wildcardPool =
            {
                WeaponAttackCatalog.GatlingGunId,
                WeaponAttackCatalog.ArtilleryGunId,
                WeaponAttackCatalog.RocketLauncherId,
                WeaponAttackCatalog.DoubleRocketLauncherId,
                WeaponAttackCatalog.RailGunId,
                WeaponAttackCatalog.DoubleIonizerId,
                WeaponAttackCatalog.AstroCutterId,
                WeaponAttackCatalog.PulseDisruptorId,
                WeaponAttackCatalog.TripleGunId,
                WeaponAttackCatalog.PlasmaGunId
            };
            return wildcardPool[Mathf.Abs(seed / 7) % wildcardPool.Length];
        }

        return pool[Mathf.Abs(seed / 3) % pool.Length];
    }

    CombatTactic ResolveCombatTactic(ShipType shipType, string weaponId, int seed)
    {
        if (archetype == NeutralRiderArchetype.Coward)
            return CombatTactic.Avoidant;

        if (string.Equals(weaponId, WeaponAttackCatalog.ArtilleryGunId, StringComparison.Ordinal) ||
            string.Equals(weaponId, WeaponAttackCatalog.RailGunId, StringComparison.Ordinal) ||
            string.Equals(weaponId, WeaponAttackCatalog.RocketLauncherId, StringComparison.Ordinal) ||
            string.Equals(weaponId, WeaponAttackCatalog.DoubleRocketLauncherId, StringComparison.Ordinal))
        {
            return CombatTactic.Kite;
        }

        if (string.Equals(weaponId, WeaponAttackCatalog.GatlingGunId, StringComparison.Ordinal) ||
            string.Equals(weaponId, WeaponAttackCatalog.TripleGunId, StringComparison.Ordinal))
        {
            return seed % 2 == 0 ? CombatTactic.HitAndRun : CombatTactic.Brawler;
        }

        if (shipType == ShipType.Arrow || archetype == NeutralRiderArchetype.Raider)
            return CombatTactic.HitAndRun;

        if (archetype == NeutralRiderArchetype.Hunter)
            return seed % 3 == 0 ? CombatTactic.Kite : CombatTactic.Brawler;

        return seed % 2 == 0 ? CombatTactic.Orbit : CombatTactic.Kite;
    }

    string[] ResolveBotGadgetItems(ShipType shipType, int seed)
    {
        if (ShipCatalog.GetGadgetSlots(shipSkinIndex) <= 0 || shipType == ShipType.CargoTruck)
            return System.Array.Empty<string>();

        string[] pool = archetype switch
        {
            NeutralRiderArchetype.Hunter => new[]
            {
                InventoryItemCatalog.SpaceTorpedoId,
                InventoryItemCatalog.RocketAutoTurretId,
                InventoryItemCatalog.AutoTurretId,
                InventoryItemCatalog.StasisBuoyId
            },
            NeutralRiderArchetype.Raider => new[]
            {
                InventoryItemCatalog.SpaceTorpedoId,
                InventoryItemCatalog.GadgetMineId,
                InventoryItemCatalog.SpaceBombId,
                InventoryItemCatalog.AutoTurretId
            },
            NeutralRiderArchetype.Coward => new[]
            {
                InventoryItemCatalog.StasisBuoyId,
                InventoryItemCatalog.SpaceBombId,
                InventoryItemCatalog.GadgetMineId
            },
            _ => new[]
            {
                InventoryItemCatalog.StasisBuoyId,
                InventoryItemCatalog.GadgetMineId,
                InventoryItemCatalog.SpaceBombId,
                InventoryItemCatalog.AutoTurretId
            }
        };

        List<string> items = new List<string>(2);
        items.Add(pool[Mathf.Abs(seed) % pool.Length]);

        if (ShipCatalog.GetGadgetSlots(shipSkinIndex) > 1 && seed % 100 < 66)
        {
            string second = pool[Mathf.Abs(seed / 5 + 1) % pool.Length];
            if (string.Equals(second, items[0], StringComparison.Ordinal))
                second = pool[(Mathf.Abs(seed / 11) + 2) % pool.Length];

            if (!string.Equals(second, items[0], StringComparison.Ordinal))
                items.Add(second);
        }

        return items.ToArray();
    }

    float ResolveWeaponMuzzleOffset()
    {
        SpriteRenderer renderer = spriteRenderer != null ? spriteRenderer : GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
            return Mathf.Clamp(Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y) * 0.34f, 0.38f, 1.25f);

        return 0.55f;
    }

    void ConfigureBotUtilities()
    {
        botUtilities.Clear();
        string[] itemIds = combatLoadout.GadgetItemIds;
        if (itemIds == null || itemIds.Length == 0)
            return;

        int seed = ResolveLoadoutSeed(43);
        for (int i = 0; i < itemIds.Length; i++)
        {
            string itemId = itemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            botUtilities.Add(new BotUtilityRuntime
            {
                ItemId = itemId,
                Charges = ResolveBotUtilityCharges(itemId, seed + i),
                Cooldown = ResolveBotUtilityCooldown(itemId),
                NextUseTime = Time.time + 2.4f + ((seed + i * 11) % 4)
            });
        }

        nextBotUtilityUseTime = Time.time + 2.6f + (seed % 4);
    }

    void ConfigureBotSupport()
    {
        if (combatLoadout.HasLootingFriend)
        {
            if (lootingFriend == null)
                lootingFriend = GetComponent<LootingFriendController>();

            if (lootingFriend == null)
                lootingFriend = gameObject.AddComponent<LootingFriendController>();

            lootingFriend.SetForcedForNeutralRider(true);
        }

        if (!combatLoadout.HasFiringFriend)
            return;

        FiringFriendController firingFriend = GetComponent<FiringFriendController>();
        if (firingFriend == null)
            firingFriend = gameObject.AddComponent<FiringFriendController>();

        firingFriend.SetForcedForNeutralRider(true);
    }

    int ResolveBotUtilityCharges(string itemId, int seed)
    {
        if (string.Equals(itemId, InventoryItemCatalog.RocketAutoTurretId, StringComparison.Ordinal) ||
            string.Equals(itemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal))
        {
            return 1;
        }

        if (string.Equals(itemId, InventoryItemCatalog.SpaceTorpedoId, StringComparison.Ordinal))
            return seed % 3 == 0 ? 2 : 1;

        return 2;
    }

    float ResolveBotUtilityCooldown(string itemId)
    {
        if (string.Equals(itemId, InventoryItemCatalog.SpaceTorpedoId, StringComparison.Ordinal))
            return 13f;

        if (string.Equals(itemId, InventoryItemCatalog.RocketAutoTurretId, StringComparison.Ordinal) ||
            string.Equals(itemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal))
        {
            return 24f;
        }

        if (string.Equals(itemId, InventoryItemCatalog.StasisBuoyId, StringComparison.Ordinal))
            return 17f;

        return 12f;
    }

    int ResolveLoadoutSeed(int salt)
    {
        int seed = loadoutSeed != 0 ? loadoutSeed : BuildFallbackLoadoutSeed(spawnOrdinal, shipSkinIndex, archetype);
        unchecked
        {
            int hash = seed;
            hash = (hash * 397) ^ (spawnOrdinal * 92821);
            hash = (hash * 397) ^ (shipSkinIndex * 68917);
            hash = (hash * 397) ^ ((int)archetype * 19391);
            hash = (hash * 397) ^ salt;
            return hash & int.MaxValue;
        }
    }

    bool CanUseMainWeapon()
    {
        return ShipCatalog.GetMainGunSlots(shipSkinIndex) > 0 &&
               ShipCatalog.GetShipTypeFromSkinIndex(shipSkinIndex) != ShipType.CargoTruck;
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
        DisableComponent<RoundVitalsIconHudUI>();
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
        if (TryUseBotRescue())
            return;

        if (forcedAggressive)
        {
            if (CanUseMainWeapon() && forcedAggressiveTargetViewId > 0 && IsValidCombatTarget(forcedAggressiveTargetViewId))
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

        if (CanUseMainWeapon() && Time.time < hostilePlayerUntil && hostilePlayerViewId > 0 && IsValidCombatTarget(hostilePlayerViewId))
        {
            targetViewId = hostilePlayerViewId;
            mode = Mode.Combat;
            return;
        }

        if (CanUseMainWeapon())
        {
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
        desired = RoomSettings.ApplyEnemyToxicBorderSteering(
            rb.position,
            desired,
            Mathf.Clamp(speed * 0.65f, 1.1f, 4.2f),
            1.05f,
            1.15f);
        Vector2 targetVelocity = desired.normalized * speed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVelocity, mode == Mode.Combat ? 0.18f : 0.24f);
        rb.angularVelocity = 0f;

        Vector2 facing = ResolveFacingDirection(desired);
        float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90f;
        rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, angle, 540f * Time.fixedDeltaTime));
    }

    void TickAction()
    {
        TickBotUtilities();
        TickBotLootingFriend();

        if (mode == Mode.Combat)
        {
            TryShootTarget();
            return;
        }

        if (mode == Mode.Scavenge)
        {
            if (botLootingFriendTargetViewId > 0)
                return;

            TryCollectTarget();
            return;
        }

        if (mode == Mode.Extract)
        {
            TryUseExtraction();
        }
    }

    void TickHackedOverride(Vector2 direction, float speedMultiplier)
    {
        if (rb == null)
            return;

        Vector2 safeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : patrolDirection.sqrMagnitude > 0.001f ? patrolDirection : Vector2.up;
        float speed = Mathf.Clamp(ResolveSpeed() * Mathf.Clamp(speedMultiplier, 0.35f, 1.15f), 1.8f, 7.4f);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, safeDirection * speed, 0.3f);
        rb.angularVelocity = 0f;
        float angle = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg - 90f;
        rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, angle, 620f * Time.fixedDeltaTime));

        if (shooting != null && HackingStatus.TryConsumeRandomShot(gameObject, out Vector2 shotDirection))
            shooting.TryFireBot(shotDirection);
    }

    Vector2 ResolveDesiredDirection()
    {
        if (mode == Mode.Combat && TryGetTargetPosition(targetViewId, out Vector2 combatPosition))
        {
            combatPosition = RoomSettings.ClampToEnemyNavigableBounds(combatPosition, MapMargin * 0.55f);
            return ResolveCombatDesiredDirection(combatPosition);
        }

        if ((mode == Mode.Scavenge || mode == Mode.Extract) && TryGetTargetPosition(targetViewId, out Vector2 targetPosition))
        {
            targetPosition = RoomSettings.ClampToEnemyNavigableBounds(targetPosition, MapMargin * 0.55f);
            return (targetPosition - rb.position).normalized;
        }

        if (mode == Mode.Flee)
            return ResolveFleeDirection();

        Vector2 safeDestination = RoomSettings.ClampToEnemyNavigableBounds(destination, MapMargin * 0.55f);
        return (safeDestination - rb.position).normalized;
    }

    Vector2 ResolveCombatDesiredDirection(Vector2 combatPosition)
    {
        if (!CanUseMainWeapon())
            return ResolveFleeDirection();

        Vector2 toTarget = combatPosition - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;

        Vector2 toward = toTarget / distance;
        Vector2 tangent = spawnOrdinal % 2 == 0 ? new Vector2(-toward.y, toward.x) : new Vector2(toward.y, -toward.x);
        float weaponRange = ResolveCombatWeaponRange();
        float preferredDistance = ResolvePreferredCombatDistance(weaponRange);
        float minimumDistance = ResolveMinimumCombatDistance(weaponRange);

        if (combatLoadout.Tactic == CombatTactic.HitAndRun)
        {
            if (Time.time < hitAndRunBreakAwayUntil)
            {
                Vector2 breakAway = hitAndRunBreakAwayDirection.sqrMagnitude > 0.001f
                    ? hitAndRunBreakAwayDirection.normalized
                    : (-toward * 0.82f + tangent * 0.38f).normalized;
                return breakAway;
            }

            if (hitAndRunAttackStartedAt <= 0f)
                hitAndRunAttackStartedAt = Time.time;

            if (distance > preferredDistance)
                return (toward * 0.94f + tangent * 0.12f).normalized;

            if (Time.time - hitAndRunAttackStartedAt >= HitAndRunMinimumAttackDuration && distance < preferredDistance * 0.72f)
            {
                BeginHitAndRunBreakAway(toward, tangent);
                return hitAndRunBreakAwayDirection;
            }

            return (toward * 0.26f + tangent * 0.76f).normalized;
        }

        if (combatLoadout.Tactic == CombatTactic.Kite)
        {
            if (distance < minimumDistance)
                return (-toward * 0.92f + tangent * 0.38f).normalized;

            if (distance > preferredDistance)
                return (toward * 0.72f + tangent * 0.16f).normalized;

            return (tangent * 0.62f - toward * 0.18f).normalized;
        }

        if (combatLoadout.Tactic == CombatTactic.Brawler)
        {
            if (distance > preferredDistance)
                return (toward * 0.92f + tangent * 0.22f).normalized;

            if (distance < minimumDistance)
                return (-toward * 0.42f + tangent * 0.82f).normalized;

            return (toward * 0.34f + tangent * 0.74f).normalized;
        }

        if (combatLoadout.Tactic == CombatTactic.Avoidant)
        {
            if (distance < preferredDistance)
                return (-toward * 0.86f + tangent * 0.48f).normalized;

            if (distance > weaponRange * 0.92f)
                return (toward * 0.5f + tangent * 0.28f).normalized;

            return (tangent * 0.74f - toward * 0.28f).normalized;
        }

        if (distance > preferredDistance)
            return (toward * 0.78f + tangent * 0.24f).normalized;

        if (distance < minimumDistance)
            return (-toward * 0.64f + tangent * 0.58f).normalized;

        return (tangent * 0.84f + toward * 0.16f).normalized;
    }

    void BeginHitAndRunBreakAway(Vector2 towardTarget, Vector2 tangent)
    {
        hitAndRunBreakAwayDirection = (-towardTarget * 0.86f + tangent * 0.42f).normalized;
        hitAndRunBreakAwayUntil = Time.time + HitAndRunBreakAwayDuration;
        hitAndRunAttackStartedAt = Time.time + HitAndRunBreakAwayDuration;
    }

    float ResolveCombatWeaponRange()
    {
        WeaponAttackProfile profile = combatLoadout.WeaponProfile;
        float rangeMultiplier = profile != null ? Mathf.Max(0.1f, profile.RangeMultiplier) : 10.5f;
        float rawRange = ResolveOwnerLengthForCombatRange() * rangeMultiplier;
        return Mathf.Clamp(rawRange, 6.8f, 18f);
    }

    float ResolveOwnerLengthForCombatRange()
    {
        SpriteRenderer renderer = spriteRenderer != null ? spriteRenderer : GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
            return Mathf.Max(0.55f, Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y));

        Collider2D collider = GetComponentInChildren<Collider2D>();
        if (collider != null)
            return Mathf.Max(0.55f, Mathf.Max(collider.bounds.size.x, collider.bounds.size.y));

        return 1f;
    }

    float ResolvePreferredCombatDistance(float weaponRange)
    {
        float factor = combatLoadout.Tactic switch
        {
            CombatTactic.Kite => 0.78f,
            CombatTactic.HitAndRun => 0.58f,
            CombatTactic.Brawler => 0.46f,
            CombatTactic.Avoidant => 0.88f,
            _ => 0.64f
        };

        return Mathf.Clamp(weaponRange * factor, 3.4f, 14f);
    }

    float ResolveMinimumCombatDistance(float weaponRange)
    {
        float factor = combatLoadout.Tactic switch
        {
            CombatTactic.Kite => 0.48f,
            CombatTactic.HitAndRun => 0.34f,
            CombatTactic.Brawler => 0.24f,
            CombatTactic.Avoidant => 0.68f,
            _ => 0.38f
        };

        return Mathf.Clamp(weaponRange * factor, 2.2f, 9.5f);
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
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
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
        if (!CanUseMainWeapon())
            return;

        if (!TryGetTargetPosition(targetViewId, out Vector2 targetPosition))
            return;

        Vector2 toTarget = targetPosition - (Vector2)transform.position;
        float weaponRange = ResolveCombatWeaponRange();
        if (toTarget.sqrMagnitude > weaponRange * weaponRange)
            return;

        if (shooting == null)
            return;

        int homingTargetViewId = IsRocketWeapon(combatLoadout.WeaponId) ? targetViewId : 0;
        bool fired = shooting.TryFireBotAtPoint(toTarget.normalized, targetPosition, homingTargetViewId);
        if (!fired)
            return;

        if (combatLoadout.Tactic == CombatTactic.HitAndRun)
        {
            Vector2 toward = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : (Vector2)transform.up;
            Vector2 tangent = spawnOrdinal % 2 == 0 ? new Vector2(-toward.y, toward.x) : new Vector2(toward.y, -toward.x);
            BeginHitAndRunBreakAway(toward, tangent);
        }
    }

    bool IsRocketWeapon(string weaponId)
    {
        return string.Equals(weaponId, WeaponAttackCatalog.RocketLauncherId, StringComparison.Ordinal) ||
               string.Equals(weaponId, WeaponAttackCatalog.DoubleRocketLauncherId, StringComparison.Ordinal);
    }

    void TickBotUtilities()
    {
        if (botUtilities.Count == 0 || shooting == null || !PhotonNetwork.IsMasterClient || !view.IsMine || mode == Mode.Evacuated)
            return;

        if (mode != Mode.Combat && mode != Mode.Flee)
            return;

        if (Time.time < nextBotUtilityUseTime)
            return;

        if (UnityEngine.Random.value > BotUtilityUseChance)
        {
            nextBotUtilityUseTime = Time.time + UnityEngine.Random.Range(1.5f, 4.2f);
            return;
        }

        for (int i = 0; i < botUtilities.Count; i++)
        {
            BotUtilityRuntime utility = botUtilities[i];
            if (utility == null || utility.Charges <= 0 || Time.time < utility.NextUseTime)
                continue;

            if (!ShouldUseBotUtility(utility.ItemId))
                continue;

            if (!shooting.TryExecuteNeutralRiderBotItem(utility.ItemId))
                continue;

            utility.Charges--;
            utility.NextUseTime = Time.time + utility.Cooldown;
            nextBotUtilityUseTime = Time.time + UnityEngine.Random.Range(BotUtilityGlobalCooldownMin, BotUtilityGlobalCooldownMax);
            return;
        }

        nextBotUtilityUseTime = Time.time + UnityEngine.Random.Range(2f, 5f);
    }

    bool ShouldUseBotUtility(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        bool hasThreat = TryGetCurrentThreatPosition(out Vector2 threatPosition, out float threatDistance);
        if (string.Equals(itemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal) ||
            string.Equals(itemId, InventoryItemCatalog.SpaceBombId, StringComparison.Ordinal))
        {
            return hasThreat &&
                   threatDistance <= 7.8f &&
                   (mode == Mode.Flee || threatDistance <= 4.8f || Time.time < hitAndRunBreakAwayUntil);
        }

        if (string.Equals(itemId, InventoryItemCatalog.StasisBuoyId, StringComparison.Ordinal))
            return hasThreat &&
                   threatDistance <= 8.5f &&
                   (mode == Mode.Flee || threatDistance <= 5.5f || GetHealthRatio() <= 0.65f);

        if (string.Equals(itemId, InventoryItemCatalog.SpaceTorpedoId, StringComparison.Ordinal))
        {
            return mode == Mode.Combat &&
                   hasThreat &&
                   threatDistance >= 2.4f &&
                   threatDistance <= ResolveCombatWeaponRange() + 3f &&
                   IsPointAhead(threatPosition, 0.35f);
        }

        if (string.Equals(itemId, InventoryItemCatalog.AutoTurretId, StringComparison.Ordinal) ||
            string.Equals(itemId, InventoryItemCatalog.RocketAutoTurretId, StringComparison.Ordinal))
        {
            return mode == Mode.Combat && hasThreat && threatDistance <= 11f && GetHealthRatio() > 0.2f;
        }

        return false;
    }

    bool TryGetCurrentThreatPosition(out Vector2 position, out float distance)
    {
        int candidateViewId = mode == Mode.Flee && hostilePlayerViewId > 0 ? hostilePlayerViewId : targetViewId;
        if (candidateViewId > 0 && TryGetTargetPosition(candidateViewId, out position))
        {
            distance = Vector2.Distance(transform.position, position);
            return true;
        }

        position = Vector2.zero;
        distance = float.MaxValue;
        return false;
    }

    bool IsPointAhead(Vector2 point, float requiredDot)
    {
        Vector2 toPoint = point - (Vector2)transform.position;
        if (toPoint.sqrMagnitude <= 0.001f)
            return false;

        return Vector2.Dot(transform.up, toPoint.normalized) >= requiredDot;
    }

    float GetHealthRatio()
    {
        if (health == null)
            return 1f;

        return health.CurrentHP / (float)Mathf.Max(1, ShipCatalog.GetBaseHp(shipSkinIndex));
    }

    void TickBotLootingFriend()
    {
        if (!combatLoadout.HasLootingFriend || mode == Mode.Combat || mode == Mode.Flee || mode == Mode.Evacuated || !HasCargoSpace)
        {
            StopBotLootingFriendTarget(true);
            return;
        }

        if (collectionTargetLocked || collectStartedAt >= 0f)
            return;

        if (botLootingFriendTargetViewId > 0)
        {
            PhotonView activeTarget = PhotonView.Find(botLootingFriendTargetViewId);
            if (!IsBotLootingFriendTargetValid(activeTarget, true) ||
                Vector2.Distance(transform.position, activeTarget.transform.position) > BotLootingFriendRange + 0.65f)
            {
                StopBotLootingFriendTarget(true);
                return;
            }

            if (Time.time - botLootingFriendStartedAt < BotLootingFriendCollectSeconds)
                return;

            ResolveCollectible(activeTarget);
            StopBotLootingFriendTarget(false);
            nextBotLootingFriendScanTime = Time.time + BotLootingFriendScanInterval * 2f;
            return;
        }

        if (Time.time < nextBotLootingFriendScanTime)
            return;

        nextBotLootingFriendScanTime = Time.time + BotLootingFriendScanInterval;
        if (!FindBestBotLootingFriendTargetViewId(out int targetId))
            return;

        PhotonView targetView = PhotonView.Find(targetId);
        if (!IsBotLootingFriendTargetValid(targetView))
            return;

        botLootingFriendTargetViewId = targetId;
        botLootingFriendStartedAt = Time.time;
        SetCollectibleBusy(targetView, true);
        StartBotLootingFriendCollectFx(targetId);
    }

    bool FindBestBotLootingFriendTargetViewId(out int viewId)
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

            float distance = Vector2.Distance(origin, treasure.transform.position);
            if (distance > BotLootingFriendRange)
                continue;

            float score = distance - Mathf.Clamp(InventoryItemCatalog.GetSellValueAstrons(treasure.itemId) / 300f, 0f, 3.5f);
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

            float distance = Vector2.Distance(origin, crate.transform.position);
            if (distance > BotLootingFriendRange)
                continue;

            float score = distance - 1.2f;
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

            float distance = Vector2.Distance(origin, wreck.transform.position);
            if (distance > BotLootingFriendRange)
                continue;

            float score = distance - 0.9f;
            if (score < bestScore)
            {
                bestScore = score;
                viewId = candidateView.ViewID;
            }
        }

        return viewId > 0;
    }

    bool IsBotLootingFriendTargetValid(PhotonView targetView, bool allowBusy = false)
    {
        if (targetView == null || !targetView.gameObject.activeInHierarchy || !HasCargoSpace)
            return false;

        Treasure treasure = targetView.GetComponent<Treasure>();
        if (treasure != null)
            return (allowBusy || !treasure.isBeingCollected) && !string.IsNullOrWhiteSpace(treasure.itemId);

        DroppedCargoCrate crate = targetView.GetComponent<DroppedCargoCrate>();
        if (crate != null)
            return (allowBusy || !crate.isBeingCollected) && crate.HasLoot && !string.IsNullOrWhiteSpace(crate.StoredItemId);

        ShipWreck wreck = targetView.GetComponent<ShipWreck>();
        return wreck != null && (allowBusy || !wreck.isBeingCollected) && wreck.HasLoot;
    }

    void StopBotLootingFriendTarget(bool releaseBusy)
    {
        if (botLootingFriendTargetViewId <= 0)
            return;

        PhotonView targetView = PhotonView.Find(botLootingFriendTargetViewId);
        if (releaseBusy && targetView != null)
            SetCollectibleBusy(targetView, false);

        StopBotLootingFriendCollectFx();
        botLootingFriendTargetViewId = 0;
        botLootingFriendStartedAt = -1f;
    }

    void StartBotLootingFriendCollectFx(int targetViewIdToCollect)
    {
        if (lootingFriend != null)
        {
            lootingFriend.SetNeutralRiderCollectFx(targetViewIdToCollect, true);
            return;
        }

        StartCollectionBeam(targetViewIdToCollect);
    }

    void StopBotLootingFriendCollectFx()
    {
        if (lootingFriend != null)
        {
            lootingFriend.SetNeutralRiderCollectFx(0, false);
            return;
        }

        StopCollectionBeam();
    }

    bool TryUseBotRescue()
    {
        if (!combatLoadout.HasEscapePod || botRescueUsed || health == null || ShipCatalog.GetShipTypeFromSkinIndex(shipSkinIndex) == ShipType.CargoTruck)
            return false;

        if (GetHealthRatio() > BotEscapePodHpRatio)
            return false;

        botRescueUsed = true;
        AbandonCollectionTarget();
        StopBotLootingFriendTarget(true);
        mode = Mode.Evacuated;
        wreckConverted = true;
        if (view != null)
            view.RPC(nameof(NeutralRiderEvacuatedRpc), RpcTarget.All, transform.position.x, transform.position.y);

        return true;
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
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
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

        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
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

    static int BuildFallbackLoadoutSeed(int ordinal, int skinIndex, NeutralRiderArchetype archetype)
    {
        unchecked
        {
            int hash = 216613626;
            hash = (hash * 16777619) ^ Mathf.Max(0, ordinal);
            hash = (hash * 16777619) ^ Mathf.Clamp(skinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex);
            hash = (hash * 16777619) ^ (int)archetype;
            return hash & int.MaxValue;
        }
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
