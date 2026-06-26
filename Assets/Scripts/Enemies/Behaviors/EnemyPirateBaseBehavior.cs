using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemyPirateBaseBehavior : EnemyBotBehaviorBase
{
    const float ArrivalDistance = 2.25f;
    const float CollectionDuration = 10f;
    const float CollectionBreakDistance = 4.6f;
    const float CargoDropRadius = 1.35f;
    const float MapEdgeMargin = 3f;
    const float LaunchSpawnDelay = 2f;
    const float LaunchStageDuration = 7f;
    static readonly List<Collider2D> CollectibleColliderScratch = new List<Collider2D>(8);
    static readonly List<SpriteRenderer> CollectibleRendererScratch = new List<SpriteRenderer>(4);

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    Transform currentTarget;
    int currentTargetViewId;
    float nextTargetRefreshTime;
    float initialRotation;
    bool launchSequenceRunning;
    bool defenseLaunchTriggered;
    int latestThreatTargetViewId;
    bool collectionRunning;
    float collectionStartedAt;
    int collectingTargetViewId;
    int collectingLootIndex = -1;
    string collectingItemId;
    readonly List<string> collectedCargoItemIds = new List<string>();

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        initialRotation = rb != null ? rb.rotation : owner.transform.eulerAngles.z;
    }

    void OnDisable()
    {
        CancelCollection(true);
    }

    void OnDestroy()
    {
        CancelCollection(true);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
        {
            CancelCollection(false);
            return;
        }

        if (collectionRunning)
        {
            TickCollection();
            return;
        }

        if (launchSequenceRunning)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.MoveRotation(initialRotation);
            return;
        }

        RefreshTargetIfNeeded();

        Vector2 desiredVelocity = Vector2.zero;
        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            float targetDistance = GetDistanceToCollectible(currentTarget);
            if (targetDistance <= ArrivalDistance)
            {
                BeginCollection();
                return;
            }

            if (targetDistance > 0.001f)
            {
                Vector2 desiredDirection = ApplyMapEdgeSteering(toTarget.normalized);
                desiredVelocity = desiredDirection * bot.EffectiveMoveSpeed;
            }
        }

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, desiredVelocity.sqrMagnitude > 0.001f ? 0.09f : 0.045f);
        rb.angularVelocity = 0f;
        rb.MoveRotation(initialRotation);
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        TryTriggerLaunchSequence(ResolveValidPlayerViewId(attackerViewID));
    }

    public static void NotifyCollectibleCollected(int collectibleViewId, int collectorViewId)
    {
        if (!PhotonNetwork.IsMasterClient || collectibleViewId <= 0 || collectorViewId <= 0)
            return;

        EnemyPirateBaseBehavior[] bases = FindObjectsByType<EnemyPirateBaseBehavior>(FindObjectsInactive.Exclude);
        for (int i = 0; i < bases.Length; i++)
        {
            EnemyPirateBaseBehavior pirateBase = bases[i];
            if (pirateBase == null)
                continue;

            pirateBase.NotifyTargetCollected(collectibleViewId, collectorViewId);
        }
    }

    void NotifyTargetCollected(int collectibleViewId, int collectorViewId)
    {
        if (health != null && health.IsWreck)
            return;

        if (collectibleViewId != currentTargetViewId)
            return;

        if (collectionRunning)
            CancelCollection(false);

        TryTriggerLaunchSequence(ResolveValidPlayerViewId(collectorViewId));
        currentTarget = null;
        currentTargetViewId = 0;
        nextTargetRefreshTime = 0f;
    }

    void RefreshTargetIfNeeded()
    {
        bool currentValid = IsCurrentTargetValid();
        if (currentValid && Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.18f, movement.TargetRefreshInterval);
        Transform nextTarget = ResolveMostValuableCollectible(out int nextTargetViewId);
        if (nextTarget != null)
        {
            currentTarget = nextTarget;
            currentTargetViewId = nextTargetViewId;
        }
        else if (!currentValid)
        {
            currentTarget = null;
            currentTargetViewId = 0;
        }
    }

    bool IsCurrentTargetValid()
    {
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy || currentTargetViewId <= 0)
            return false;

        PhotonView targetView = PhotonView.Find(currentTargetViewId);
        if (targetView == null)
            return false;

        Treasure treasure = targetView.GetComponent<Treasure>();
        if (treasure != null)
            return !treasure.isBeingCollected;

        DroppedCargoCrate crate = targetView.GetComponent<DroppedCargoCrate>();
        if (crate != null)
            return crate.HasLoot && !crate.isBeingCollected;

        ShipWreck wreck = targetView.GetComponent<ShipWreck>();
        return wreck != null && wreck.HasLoot && !wreck.isBeingCollected;
    }

    Transform ResolveMostValuableCollectible(out int targetViewId)
    {
        Transform bestTarget = null;
        targetViewId = 0;
        int bestValue = int.MinValue;
        float bestDistance = float.MaxValue;

        Treasure[] treasures = RuntimeSceneQueryCache.GetTreasures();
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || !treasure.gameObject.activeInHierarchy || treasure.isBeingCollected)
                continue;

            PhotonView targetView = treasure.GetComponent<PhotonView>();
            string itemId = InventoryItemCatalog.ResolveAlienSecretItemId(treasure.itemId, treasure.visualVariantIndex);
            ConsiderCollectible(treasure.transform, targetView, itemId, ref bestTarget, ref targetViewId, ref bestValue, ref bestDistance);
        }

        DroppedCargoCrate[] crates = RuntimeSceneQueryCache.GetDroppedCargoCrates();
        for (int i = 0; i < crates.Length; i++)
        {
            DroppedCargoCrate crate = crates[i];
            if (crate == null || !crate.gameObject.activeInHierarchy || !crate.HasLoot || crate.isBeingCollected)
                continue;

            PhotonView targetView = crate.GetComponent<PhotonView>();
            ConsiderCollectible(crate.transform, targetView, crate.StoredItemId, ref bestTarget, ref targetViewId, ref bestValue, ref bestDistance);
        }

        ShipWreck[] wrecks = RuntimeSceneQueryCache.GetShipWrecks();
        for (int i = 0; i < wrecks.Length; i++)
        {
            ShipWreck wreck = wrecks[i];
            if (wreck == null || !wreck.gameObject.activeInHierarchy || !wreck.HasLoot || wreck.isBeingCollected)
                continue;

            PhotonView targetView = wreck.GetComponent<PhotonView>();
            string itemId = wreck.GetLootItemAt(wreck.GetFirstLootIndex());
            ConsiderCollectible(wreck.transform, targetView, itemId, ref bestTarget, ref targetViewId, ref bestValue, ref bestDistance);
        }

        return bestTarget;
    }

    void ConsiderCollectible(Transform candidate, PhotonView targetView, string itemId, ref Transform bestTarget, ref int bestViewId, ref int bestValue, ref float bestDistance)
    {
        if (candidate == null || targetView == null)
            return;

        int value = Mathf.Max(0, InventoryItemCatalog.GetSellValueAstrons(itemId));
        float distance = GetDistanceToCollectible(candidate);
        if (value < bestValue)
            return;

        if (value == bestValue && distance >= bestDistance)
            return;

        bestValue = value;
        bestDistance = distance;
        bestTarget = candidate;
        bestViewId = targetView.ViewID;
    }

    void BeginCollection()
    {
        if (currentTargetViewId <= 0 || collectionRunning)
            return;

        PhotonView targetView = PhotonView.Find(currentTargetViewId);
        if (targetView == null || IsCollectibleReserved(targetView))
        {
            ClearCurrentTarget();
            return;
        }

        if (!TryResolveCollectibleLoot(targetView, out string itemId, out int lootIndex))
        {
            ClearCurrentTarget();
            return;
        }

        collectionRunning = true;
        collectionStartedAt = Time.time;
        collectingTargetViewId = currentTargetViewId;
        collectingItemId = itemId;
        collectingLootIndex = lootIndex;
        MarkCollectibleCollectionState(collectingTargetViewId, true);

        if (bot != null && bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.StartPirateBaseCollectionBeamRpc), RpcTarget.All, collectingTargetViewId);

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.MoveRotation(initialRotation);
    }

    void TickCollection()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.MoveRotation(initialRotation);

        if (!IsCollectingTargetStillValid())
        {
            CancelCollection(true);
            ClearCurrentTarget();
            return;
        }

        if (Time.time - collectionStartedAt >= CollectionDuration)
            CompleteCollection();
    }

    void CompleteCollection()
    {
        if (!IsCollectingTargetStillValid())
        {
            CancelCollection(true);
            ClearCurrentTarget();
            return;
        }

        if (!string.IsNullOrWhiteSpace(collectingItemId))
            collectedCargoItemIds.Add(collectingItemId);

        RemoveCollectedTarget(collectingTargetViewId, collectingLootIndex, collectingItemId);
        CancelCollection(false);
        ClearCurrentTarget();
    }

    void CancelCollection(bool releaseTarget)
    {
        if (!collectionRunning && collectingTargetViewId <= 0)
            return;

        int targetViewId = collectingTargetViewId;
        collectionRunning = false;
        collectionStartedAt = 0f;
        collectingTargetViewId = 0;
        collectingLootIndex = -1;
        collectingItemId = null;

        if (releaseTarget && targetViewId > 0)
            MarkCollectibleCollectionState(targetViewId, false);

        if (view != null && view.IsMine && bot != null && bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.StopPirateBaseCollectionBeamRpc), RpcTarget.All);
    }

    public void DropCollectedCargoOnDeath()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        CancelCollection(true);
        if (collectedCargoItemIds.Count == 0)
            return;

        Vector3 center = transform.position;
        int seed = view != null ? view.ViewID : Mathf.RoundToInt(center.sqrMagnitude * 1000f);
        for (int i = 0; i < collectedCargoItemIds.Count; i++)
        {
            string itemId = collectedCargoItemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            float angle = ((i / Mathf.Max(1f, collectedCargoItemIds.Count)) * Mathf.PI * 2f) + seed * 0.173f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (direction.sqrMagnitude <= 0.001f)
                direction = Vector2.up;

            float radius = Mathf.Lerp(0.45f, CargoDropRadius, Hash01(seed, i + 17));
            Vector3 dropPosition = center + (Vector3)(direction.normalized * radius);
            Vector2 tangent = new Vector2(-direction.y, direction.x) * Mathf.Lerp(-0.28f, 0.28f, Hash01(seed, i + 41));
            Vector2 drift = (direction.normalized * 0.78f + tangent).normalized * Mathf.Lerp(0.48f, 0.92f, Hash01(seed, i + 73));
            DroppedCargoManager.DropItemAtPosition(itemId, dropPosition, drift);
        }

        collectedCargoItemIds.Clear();
    }

    bool IsCollectingTargetStillValid()
    {
        if (collectingTargetViewId <= 0 || string.IsNullOrWhiteSpace(collectingItemId))
            return false;

        PhotonView targetView = PhotonView.Find(collectingTargetViewId);
        if (targetView == null)
            return false;

        if (!TryResolveCollectibleLoot(targetView, out string currentItemId, out int currentLootIndex))
            return false;

        if (!string.Equals(currentItemId, collectingItemId, System.StringComparison.Ordinal) || currentLootIndex != collectingLootIndex)
            return false;

        float distance = GetDistanceToCollectible(targetView.transform);
        return distance <= CollectionBreakDistance;
    }

    float GetDistanceToCollectible(Transform target)
    {
        if (target == null)
            return float.MaxValue;

        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        float bestDistance = float.MaxValue;

        CollectibleColliderScratch.Clear();
        target.GetComponentsInChildren(false, CollectibleColliderScratch);
        for (int i = 0; i < CollectibleColliderScratch.Count; i++)
        {
            Collider2D collider = CollectibleColliderScratch[i];
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;

            float distance = Vector2.Distance(origin, collider.ClosestPoint(origin));
            if (distance < bestDistance)
                bestDistance = distance;
        }

        if (bestDistance < float.MaxValue)
            return bestDistance;

        CollectibleRendererScratch.Clear();
        target.GetComponentsInChildren(false, CollectibleRendererScratch);
        for (int i = 0; i < CollectibleRendererScratch.Count; i++)
        {
            SpriteRenderer renderer = CollectibleRendererScratch[i];
            if (renderer == null || !renderer.enabled || renderer.sprite == null || !renderer.gameObject.activeInHierarchy)
                continue;

            Vector3 closest = renderer.bounds.ClosestPoint(origin);
            float distance = Vector2.Distance(origin, closest);
            if (distance < bestDistance)
                bestDistance = distance;
        }

        return bestDistance < float.MaxValue
            ? bestDistance
            : Vector2.Distance(origin, target.position);
    }

    bool TryResolveCollectibleLoot(PhotonView targetView, out string itemId, out int lootIndex)
    {
        itemId = null;
        lootIndex = -1;
        if (targetView == null)
            return false;

        Treasure treasure = targetView.GetComponent<Treasure>();
        if (treasure != null)
        {
            itemId = InventoryItemCatalog.ResolveAlienSecretItemId(treasure.itemId, treasure.visualVariantIndex);
            lootIndex = 0;
            return !string.IsNullOrWhiteSpace(itemId);
        }

        DroppedCargoCrate crate = targetView.GetComponent<DroppedCargoCrate>();
        if (crate != null && crate.HasLoot)
        {
            itemId = crate.StoredItemId;
            lootIndex = 0;
            return !string.IsNullOrWhiteSpace(itemId);
        }

        ShipWreck wreck = targetView.GetComponent<ShipWreck>();
        if (wreck != null && wreck.HasLoot)
        {
            lootIndex = wreck.GetFirstLootIndex();
            itemId = wreck.GetLootItemAt(lootIndex);
            return lootIndex >= 0 && !string.IsNullOrWhiteSpace(itemId);
        }

        return false;
    }

    bool IsCollectibleReserved(PhotonView targetView)
    {
        if (targetView == null)
            return false;

        Treasure treasure = targetView.GetComponent<Treasure>();
        if (treasure != null)
            return treasure.isBeingCollected;

        DroppedCargoCrate crate = targetView.GetComponent<DroppedCargoCrate>();
        if (crate != null)
            return crate.isBeingCollected;

        ShipWreck wreck = targetView.GetComponent<ShipWreck>();
        return wreck != null && wreck.isBeingCollected;
    }

    void MarkCollectibleCollectionState(int targetViewId, bool value)
    {
        if (!PhotonNetwork.IsMasterClient || targetViewId <= 0)
            return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null)
            return;

        Treasure treasure = targetView.GetComponent<Treasure>();
        if (treasure != null)
        {
            targetView.RPC(nameof(Treasure.SetBeingCollectedRpc), RpcTarget.All, value);
            return;
        }

        DroppedCargoCrate crate = targetView.GetComponent<DroppedCargoCrate>();
        if (crate != null)
        {
            targetView.RPC(nameof(DroppedCargoCrate.SetBeingCollectedRpc), RpcTarget.All, value);
            return;
        }

        ShipWreck wreck = targetView.GetComponent<ShipWreck>();
        if (wreck != null)
            targetView.RPC(nameof(ShipWreck.SetBeingCollectedRpc), RpcTarget.All, value);
    }

    void RemoveCollectedTarget(int targetViewId, int lootIndex, string itemId)
    {
        if (!PhotonNetwork.IsMasterClient || targetViewId <= 0)
            return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null)
            return;

        int collectorViewId = view != null ? view.ViewID : 0;
        Treasure treasure = targetView.GetComponent<Treasure>();
        if (treasure != null)
        {
            string resolvedTreasureItemId = InventoryItemCatalog.ResolveAlienSecretItemId(treasure.itemId, treasure.visualVariantIndex);
            if (!string.Equals(resolvedTreasureItemId, itemId, System.StringComparison.Ordinal))
                return;

            SpaceTrapTarget.DetonateIfArmed(targetViewId, collectorViewId);
            PhotonNetwork.Destroy(targetView.gameObject);
            return;
        }

        DroppedCargoCrate crate = targetView.GetComponent<DroppedCargoCrate>();
        if (crate != null)
        {
            if (!crate.HasLoot || !string.Equals(crate.StoredItemId, itemId, System.StringComparison.Ordinal))
                return;

            SpaceTrapTarget.DetonateIfArmed(targetViewId, collectorViewId);
            targetView.RPC(nameof(DroppedCargoCrate.ClearStoredItemRpc), RpcTarget.All);
            PhotonNetwork.Destroy(targetView.gameObject);
            return;
        }

        ShipWreck wreck = targetView.GetComponent<ShipWreck>();
        if (wreck == null || !wreck.HasLoot)
            return;

        string currentItemId = wreck.GetLootItemAt(lootIndex);
        if (!string.Equals(currentItemId, itemId, System.StringComparison.Ordinal))
            return;

        SpaceTrapTarget.DetonateIfArmed(targetViewId, collectorViewId);
        targetView.RPC(nameof(ShipWreck.RemoveLootAtIndexRpc), RpcTarget.All, lootIndex);
    }

    void ClearCurrentTarget()
    {
        currentTarget = null;
        currentTargetViewId = 0;
        nextTargetRefreshTime = 0f;
    }

    static float Hash01(int baseSeed, int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)baseSeed) * 16777619u;
            hash = (hash ^ (uint)salt) * 16777619u;
            hash ^= hash >> 13;
            hash *= 1274126177u;
            return (hash & 0xFFFFFF) / 16777215f;
        }
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.1f, bot.EffectiveMoveSpeed * 2f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= 1f;
        else if (predicted.x < -halfX)
            inward.x += 1f;

        if (predicted.y > halfY)
            inward.y -= 1f;
        else if (predicted.y < -halfY)
            inward.y += 1f;

        if (inward.sqrMagnitude <= 0.001f)
            return desired;

        return (desired * 0.42f + inward.normalized * 0.58f).normalized;
    }

    void TryTriggerLaunchSequence(int targetViewId)
    {
        targetViewId = ResolveValidPlayerViewId(targetViewId);
        if (targetViewId <= 0)
            targetViewId = FindNearestPlayerViewId(transform.position);

        if (targetViewId <= 0)
            return;

        if (collectionRunning)
            CancelCollection(true);

        latestThreatTargetViewId = targetViewId;
        if (launchSequenceRunning || defenseLaunchTriggered)
            return;

        defenseLaunchTriggered = true;
        StartCoroutine(LaunchDefenseSequence());
    }

    IEnumerator LaunchDefenseSequence()
    {
        launchSequenceRunning = true;
        EnemyBotKind[] launchOrder = { EnemyBotKind.PirateFighterElite, EnemyBotKind.PirateFighterAce };
        for (int i = 0; i < launchOrder.Length; i++)
        {
            if (health != null && health.IsWreck)
                break;

            EnemyBotKind fighterKind = launchOrder[i];
            int targetViewId = ResolveValidPlayerViewId(latestThreatTargetViewId);
            if (targetViewId <= 0)
                targetViewId = FindNearestPlayerViewId(transform.position);

            if (bot != null && bot.photonView != null)
                bot.photonView.RPC(nameof(EnemyBot.PlayPirateBaseLaunchVfxRpc), RpcTarget.All, (int)fighterKind);

            yield return new WaitForSeconds(LaunchSpawnDelay);
            SpawnLaunchedFighter(fighterKind, targetViewId);
            yield return new WaitForSeconds(Mathf.Max(0.05f, LaunchStageDuration - LaunchSpawnDelay));
        }

        launchSequenceRunning = false;
    }

    void SpawnLaunchedFighter(EnemyBotKind fighterKind, int targetViewId)
    {
        if (health != null && health.IsWreck)
            return;

        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(fighterKind);
        if (definition == null)
            return;

        Vector3 spawnPosition = transform.position;
        GameObject fighterObject = PhotonNetwork.Instantiate(
            "Player",
            spawnPosition,
            transform.rotation,
            0,
            new object[] { definition.InstantiationMarker, EnemyBot.PirateBaseLaunchedFighterMarker, targetViewId, view != null ? view.ViewID : 0 });

        if (fighterObject == null)
            return;

        EnemyBot fighter = fighterObject.GetComponent<EnemyBot>();
        if (fighter == null)
            fighter = fighterObject.AddComponent<EnemyBot>();

        fighter.InitializeFromPhotonData();
        fighter.ForceCombatTarget(targetViewId);
        GameVisualTheme.RequestRuntimeRefresh(fighterObject);
    }

    int ResolveValidPlayerViewId(int viewId)
    {
        if (viewId <= 0)
            return 0;

        PhotonView targetView = PhotonView.Find(viewId);
        if (targetView == null)
            return 0;

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        if (targetHealth == null || targetHealth.IsWreck || targetHealth.IsBotControlled || targetHealth.IsAstronautControlled || targetHealth.IsEvacuationAnimating)
            return 0;

        return targetView.ViewID;
    }

    static int FindNearestPlayerViewId(Vector2 origin)
    {
        int pirateCaseCarrierViewId = ValuableCargoCarrierUtility.FindBestPirateCaseCarrierViewId(origin, float.PositiveInfinity);
        if (pirateCaseCarrierViewId > 0)
            return pirateCaseCarrierViewId;

        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        int bestViewId = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.IsWreck || candidate.IsBotControlled || candidate.IsAstronautControlled || candidate.IsEvacuationAnimating)
                continue;

            PhotonView candidateView = candidate.GetComponent<PhotonView>();
            if (candidateView == null)
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestViewId = candidateView.ViewID;
        }

        return bestViewId;
    }
}

