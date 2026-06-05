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

    const float ThinkInterval = 0.22f;
    const float TargetRefreshInterval = 0.55f;
    const float CollectRange = 2.25f;
    const float CollectSeconds = 1.65f;
    const float WreckCollectSeconds = 2.15f;
    const float ExtractionUseSeconds = 2.5f;
    const float CombatRange = 10.5f;
    const float DetectionRange = 14f;
    const float PlayerRivalryRange = 11f;
    const float FleeHealthRatio = 0.34f;
    const float ExtractionCargoValue = 950f;
    const float ExtractionEndgameSeconds = 50f;
    const float MapMargin = 4f;
    const float AvoidanceRadius = 1.65f;
    const float NameplateYOffset = 0.92f;

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
    float collectStartedAt = -1f;
    float extractionUseStartedAt = -1f;
    int targetViewId;
    int hostilePlayerViewId;
    float hostilePlayerUntil;
    int shipSkinIndex;
    int spawnOrdinal;
    string riderName = "Rider";
    bool initialized;
    bool wreckConverted;
    bool collectionTargetLocked;

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

    public static string GetGeneratedName(int ordinal)
    {
        return "AI_Raider_" + (Mathf.Max(0, ordinal) + 1);
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
        initialized = true;
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
    }

    void OnDestroy()
    {
        ReleaseCollectionTarget();
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
        enabled = false;
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

        float fireRate = archetype == NeutralRiderArchetype.Hunter ? 0.32f : archetype == NeutralRiderArchetype.Raider ? 0.38f : 0.48f;
        int damage = archetype == NeutralRiderArchetype.Hunter ? 11 : 8;
        Color color = archetype == NeutralRiderArchetype.Hunter
            ? new Color(1f, 0.48f, 0.18f, 1f)
            : new Color(0.36f, 0.92f, 1f, 1f);

        shooting.ConfigureWeaponProfile(
            fireRate,
            8,
            3.2f,
            damage,
            0.85f,
            color,
            0.52f,
            false,
            10.5f,
            "lazer1",
            12f,
            string.Empty,
            10f);
    }

    void DisableHumanOnlyComponents()
    {
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
    }

    void DisableComponent<T>() where T : Behaviour
    {
        T component = GetComponent<T>();
        if (component != null)
            component.enabled = false;
    }

    void Think()
    {
        if (ShouldExtract())
        {
            AbandonCollectionTarget();
            mode = Mode.Extract;
            targetViewId = FindBestExtractionViewId();
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
            return desired;

        return (desired.normalized + avoidance.normalized * 0.7f).normalized;
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
            targetViewId = FindBestExtractionViewId();
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
        mode = Mode.Evacuated;
        wreckConverted = true;
        if (view != null)
            view.RPC(nameof(NeutralRiderEvacuatedRpc), RpcTarget.All, zone.transform.position.x, zone.transform.position.y);
    }

    [PunRPC]
    void NeutralRiderEvacuatedRpc(float targetX, float targetY)
    {
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
            return;

        collectionTargetLocked = false;
        if (PhotonNetwork.InRoom)
        {
            PhotonView targetView = PhotonView.Find(releasedViewId);
            if (targetView != null)
                SetCollectibleBusy(targetView, false);
        }
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

        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || treasure.isBeingCollected || string.IsNullOrWhiteSpace(treasure.itemId))
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

        DroppedCargoCrate[] crates = FindObjectsByType<DroppedCargoCrate>(FindObjectsInactive.Exclude);
        for (int i = 0; i < crates.Length; i++)
        {
            DroppedCargoCrate crate = crates[i];
            if (crate == null || crate.isBeingCollected || !crate.HasLoot)
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

        ShipWreck[] wrecks = FindObjectsByType<ShipWreck>(FindObjectsInactive.Exclude);
        for (int i = 0; i < wrecks.Length; i++)
        {
            ShipWreck wreck = wrecks[i];
            if (wreck == null || wreck.isBeingCollected || !wreck.HasLoot)
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
        PlayerHealth[] candidates = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        float bestScore = float.MaxValue;
        int bestViewId = 0;
        Vector2 origin = transform.position;

        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerHealth candidate = candidates[i];
            if (candidate == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating)
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
        PlayerHealth[] candidates = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        PlayerHealth best = null;
        float bestDistance = maxRange;
        Vector2 origin = transform.position;

        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerHealth candidate = candidates[i];
            if (!ActorIdentity.IsHumanPlayerActor(candidate) || candidate.IsWreck || candidate.IsEvacuationAnimating)
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
        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        float bestDistance = float.MaxValue;
        int bestViewId = 0;
        Vector2 origin = transform.position;

        for (int i = 0; i < zones.Length; i++)
        {
            ExtractionZone zone = zones[i];
            if (zone == null || zone.IsEvacuating)
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
        if (CargoValueAstrons >= ExtractionCargoValue)
            return true;

        if (ShouldFlee() && cargo.Count > 0)
            return true;

        GameTimer timer = FindAnyObjectByType<GameTimer>();
        return timer != null && timer.GetCurrentRemainingTime() <= ExtractionEndgameSeconds && cargo.Count > 0;
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

        if (nameplate.text != riderName)
            nameplate.text = riderName;

        nameplate.transform.rotation = Quaternion.identity;
        nameplate.gameObject.SetActive(!wreckConverted && mode != Mode.Evacuated);
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
