using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public partial class TreasureCollector
{
    enum NovaPendingCollectibleKind
    {
        Treasure,
        RandomLootWreck,
        Wreck,
        DroppedCargo
    }

    sealed class NovaPendingCollectible
    {
        public NovaPendingCollectibleKind Kind;
        public int ViewId;
        public int LootIndex;
        public string ItemId;
        public string BlueprintItemId;
    }


    public bool TryNovaScavengerBurstOnMaster(float rangeMultiplier, float collectDelaySeconds, out string targetViewIds)
    {
        targetViewIds = string.Empty;
        if (!PhotonNetwork.IsMasterClient || photonView == null || photonView.Owner == null)
            return false;

        float maxRange = Treasure.CollectRange * Mathf.Max(1f, rangeMultiplier);
        Vector2 tipPosition = GetShipTipPosition();
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(tipPosition, maxRange + 0.85f, out Collider2D[] hits);
        HashSet<int> queuedViewIds = new HashSet<int>();
        List<int> visualTargetIds = new List<int>();
        List<NovaPendingCollectible> pendingCollectibles = new List<NovaPendingCollectible>();

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            Treasure treasure = hit.GetComponent<Treasure>() ?? hit.GetComponentInParent<Treasure>();
            if (treasure != null)
            {
                if (TryQueueNovaTreasureOnMaster(treasure, tipPosition, maxRange, queuedViewIds, pendingCollectibles))
                    visualTargetIds.Add(treasure.photonView.ViewID);

                continue;
            }

            ShipWreck wreck = hit.GetComponent<ShipWreck>() ?? hit.GetComponentInParent<ShipWreck>();
            if (wreck != null)
            {
                PhotonView wreckView = wreck.GetComponent<PhotonView>();
                if (TryQueueNovaWreckOnMaster(wreck, tipPosition, maxRange, queuedViewIds, pendingCollectibles) && wreckView != null)
                    visualTargetIds.Add(wreckView.ViewID);

                continue;
            }

            DroppedCargoCrate crate = hit.GetComponent<DroppedCargoCrate>() ?? hit.GetComponentInParent<DroppedCargoCrate>();
            if (crate != null)
            {
                PhotonView crateView = crate.GetComponent<PhotonView>();
                if (TryQueueNovaDroppedCargoOnMaster(crate, tipPosition, maxRange, queuedViewIds, pendingCollectibles) && crateView != null)
                    visualTargetIds.Add(crateView.ViewID);
            }
        }

        targetViewIds = BuildViewIdCsv(visualTargetIds);
        if (pendingCollectibles.Count > 0)
            StartCoroutine(ResolveNovaScavengerBurstAfterDelay(pendingCollectibles, collectDelaySeconds));

        return visualTargetIds.Count > 0;
    }


    bool TryQueueNovaTreasureOnMaster(Treasure treasure, Vector2 tipPosition, float maxRange, HashSet<int> queuedViewIds, List<NovaPendingCollectible> pendingCollectibles)
    {
        if (treasure == null || treasure.photonView == null || treasure.isBeingCollected)
            return false;

        int viewId = treasure.photonView.ViewID;
        if (viewId <= 0 || queuedViewIds.Contains(viewId))
            return false;

        if (GetDistanceFromTipToCollider(treasure.GetComponent<Collider2D>(), treasure.transform.position, tipPosition) > maxRange)
            return false;

        if (InventoryItemCatalog.IsRandomLootWreckItem(treasure.itemId))
            return TryQueueNovaRandomLootWreckOnMaster(treasure, queuedViewIds, pendingCollectibles);

        if (ReservedTreasureCollections.TryGetValue(viewId, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return false;

        string collectibleItemId = InventoryItemCatalog.ResolveAlienSecretItemId(treasure.itemId, treasure.visualVariantIndex);
        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, collectibleItemId))
            return false;

        queuedViewIds.Add(viewId);
        treasure.isBeingCollected = true;
        ReservedTreasureCollections[viewId] = photonView.OwnerActorNr;
        pendingCollectibles.Add(new NovaPendingCollectible
        {
            Kind = NovaPendingCollectibleKind.Treasure,
            ViewId = viewId,
            ItemId = collectibleItemId
        });
        return true;
    }

    bool TryQueueNovaRandomLootWreckOnMaster(Treasure treasure, HashSet<int> queuedViewIds, List<NovaPendingCollectible> pendingCollectibles)
    {
        if (treasure == null || treasure.photonView == null)
            return false;

        int viewId = treasure.photonView.ViewID;
        if (viewId <= 0 || queuedViewIds.Contains(viewId))
            return false;

        if (ReservedRandomLootWrecks.TryGetValue(viewId, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return false;

        string rewardItemId = RollRandomLootWreckReward();
        if (string.IsNullOrWhiteSpace(rewardItemId))
            return false;

        bool canStoreReward = PlayerProfileService.PlayerCanStoreShipItemOrAtlasAutoReplace(photonView.Owner, rewardItemId) ||
                              PlayerProfileService.PlayerHasFreeEquipmentSlotForItem(photonView.Owner, rewardItemId);
        if (!canStoreReward)
            return false;

        queuedViewIds.Add(viewId);
        treasure.isBeingCollected = true;
        ReservedRandomLootWrecks[viewId] = photonView.OwnerActorNr;
        pendingCollectibles.Add(new NovaPendingCollectible
        {
            Kind = NovaPendingCollectibleKind.RandomLootWreck,
            ViewId = viewId,
            ItemId = rewardItemId
        });
        return true;
    }

    bool TryQueueNovaWreckOnMaster(ShipWreck wreck, Vector2 tipPosition, float maxRange, HashSet<int> queuedViewIds, List<NovaPendingCollectible> pendingCollectibles)
    {
        PhotonView wreckView = wreck != null ? wreck.GetComponent<PhotonView>() : null;
        if (wreck == null || wreckView == null || !wreck.HasLoot || wreck.isBeingCollected)
            return false;

        int viewId = wreckView.ViewID;
        if (viewId <= 0 || queuedViewIds.Contains(viewId))
            return false;

        if (GetDistanceFromTipToCollider(wreck.GetComponent<Collider2D>(), wreck.transform.position, tipPosition) > maxRange)
            return false;

        if (ReservedWreckLoot.TryGetValue(viewId, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return false;

        int lootIndex = wreck.GetFirstLootIndex();
        string itemId = wreck.GetLootItemAt(lootIndex);
        if (lootIndex < 0 || string.IsNullOrWhiteSpace(itemId))
            return false;

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, itemId))
            return false;

        string blueprintItemId = string.Empty;
        if (wreck.SourceShipSkinIndex < 0)
            BlueprintCatalog.TryRollWreckBlueprintDrop(itemId, wreck.SourceEnemyKindValue, out blueprintItemId);

        queuedViewIds.Add(viewId);
        wreck.isBeingCollected = true;
        ReservedWreckLoot[viewId] = photonView.OwnerActorNr;
        pendingCollectibles.Add(new NovaPendingCollectible
        {
            Kind = NovaPendingCollectibleKind.Wreck,
            ViewId = viewId,
            LootIndex = lootIndex,
            ItemId = itemId,
            BlueprintItemId = blueprintItemId ?? string.Empty
        });
        return true;
    }

    bool TryQueueNovaDroppedCargoOnMaster(DroppedCargoCrate crate, Vector2 tipPosition, float maxRange, HashSet<int> queuedViewIds, List<NovaPendingCollectible> pendingCollectibles)
    {
        PhotonView crateView = crate != null ? crate.GetComponent<PhotonView>() : null;
        if (crate == null || crateView == null || !crate.HasLoot || crate.isBeingCollected)
            return false;

        int viewId = crateView.ViewID;
        if (viewId <= 0 || queuedViewIds.Contains(viewId))
            return false;

        if (GetDistanceFromTipToCollider(crate.GetComponent<Collider2D>(), crate.transform.position, tipPosition) > maxRange)
            return false;

        if (ReservedDroppedCargoLoot.TryGetValue(viewId, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return false;

        string itemId = crate.StoredItemId;
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, itemId))
            return false;

        queuedViewIds.Add(viewId);
        crate.isBeingCollected = true;
        ReservedDroppedCargoLoot[viewId] = photonView.OwnerActorNr;
        pendingCollectibles.Add(new NovaPendingCollectible
        {
            Kind = NovaPendingCollectibleKind.DroppedCargo,
            ViewId = viewId,
            ItemId = itemId
        });
        return true;
    }

    IEnumerator ResolveNovaScavengerBurstAfterDelay(List<NovaPendingCollectible> pendingCollectibles, float delaySeconds)
    {
        float delay = Mathf.Max(0f, delaySeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!PhotonNetwork.IsMasterClient || photonView == null || pendingCollectibles == null)
            yield break;

        if (photonView.Owner == null)
        {
            ReleaseNovaPendingReservations(pendingCollectibles);
            yield break;
        }

        for (int i = 0; i < pendingCollectibles.Count; i++)
            ResolveNovaPendingCollectible(pendingCollectibles[i]);
    }

    void ResolveNovaPendingCollectible(NovaPendingCollectible pending)
    {
        if (pending == null || pending.ViewId <= 0 || photonView == null)
            return;

        if (photonView.Owner == null)
        {
            ReleaseNovaPendingReservation(pending);
            return;
        }

        switch (pending.Kind)
        {
            case NovaPendingCollectibleKind.Treasure:
                ResolveNovaPendingTreasure(pending);
                break;
            case NovaPendingCollectibleKind.RandomLootWreck:
                ResolveNovaPendingRandomLootWreck(pending);
                break;
            case NovaPendingCollectibleKind.Wreck:
                ResolveNovaPendingWreck(pending);
                break;
            case NovaPendingCollectibleKind.DroppedCargo:
                ResolveNovaPendingDroppedCargo(pending);
                break;
        }
    }

    void ReleaseNovaPendingReservations(List<NovaPendingCollectible> pendingCollectibles)
    {
        if (pendingCollectibles == null)
            return;

        for (int i = 0; i < pendingCollectibles.Count; i++)
            ReleaseNovaPendingReservation(pendingCollectibles[i]);
    }

    void ReleaseNovaPendingReservation(NovaPendingCollectible pending)
    {
        if (pending == null || pending.ViewId <= 0)
            return;

        PhotonView targetView = PhotonView.Find(pending.ViewId);
        switch (pending.Kind)
        {
            case NovaPendingCollectibleKind.Treasure:
                ReleaseNovaTreasureReservation(pending.ViewId, targetView != null ? targetView.GetComponent<Treasure>() : null);
                break;
            case NovaPendingCollectibleKind.RandomLootWreck:
                ReleaseNovaRandomLootWreckReservation(pending.ViewId, targetView != null ? targetView.GetComponent<Treasure>() : null);
                break;
            case NovaPendingCollectibleKind.Wreck:
                ReleaseNovaWreckReservation(pending.ViewId, targetView != null ? targetView.GetComponent<ShipWreck>() : null);
                break;
            case NovaPendingCollectibleKind.DroppedCargo:
                ReleaseNovaDroppedCargoReservation(pending.ViewId, targetView != null ? targetView.GetComponent<DroppedCargoCrate>() : null);
                break;
        }
    }

    void ResolveNovaPendingTreasure(NovaPendingCollectible pending)
    {
        if (!IsReservationOwnedByCurrentPlayer(ReservedTreasureCollections, pending.ViewId))
            return;

        PhotonView targetView = PhotonView.Find(pending.ViewId);
        Treasure treasure = targetView != null ? targetView.GetComponent<Treasure>() : null;
        string resolvedTreasureItemId = treasure != null
            ? InventoryItemCatalog.ResolveAlienSecretItemId(treasure.itemId, treasure.visualVariantIndex)
            : null;
        if (treasure == null || string.IsNullOrWhiteSpace(pending.ItemId) ||
            !string.Equals(resolvedTreasureItemId, pending.ItemId, System.StringComparison.Ordinal))
        {
            ReleaseNovaTreasureReservation(pending.ViewId, treasure);
            return;
        }

        photonView.RPC(nameof(ReceiveNovaTreasureRpc), photonView.Owner, pending.ViewId, pending.ItemId);
    }

    void ResolveNovaPendingRandomLootWreck(NovaPendingCollectible pending)
    {
        if (!IsReservationOwnedByCurrentPlayer(ReservedRandomLootWrecks, pending.ViewId))
            return;

        PhotonView targetView = PhotonView.Find(pending.ViewId);
        Treasure wreck = targetView != null ? targetView.GetComponent<Treasure>() : null;
        if (wreck == null || string.IsNullOrWhiteSpace(pending.ItemId) || !InventoryItemCatalog.IsRandomLootWreckItem(wreck.itemId))
        {
            ReleaseNovaRandomLootWreckReservation(pending.ViewId, wreck);
            return;
        }

        photonView.RPC(nameof(ReceivePendingRandomLootWreckRpc), photonView.Owner, pending.ViewId, pending.ItemId);
    }

    void ResolveNovaPendingWreck(NovaPendingCollectible pending)
    {
        if (!IsReservationOwnedByCurrentPlayer(ReservedWreckLoot, pending.ViewId))
            return;

        PhotonView targetView = PhotonView.Find(pending.ViewId);
        ShipWreck wreck = targetView != null ? targetView.GetComponent<ShipWreck>() : null;
        string currentItemId = wreck != null ? wreck.GetLootItemAt(pending.LootIndex) : string.Empty;
        if (wreck == null || !wreck.HasLoot || string.IsNullOrWhiteSpace(pending.ItemId) ||
            !string.Equals(currentItemId, pending.ItemId, System.StringComparison.Ordinal))
        {
            ReleaseNovaWreckReservation(pending.ViewId, wreck);
            return;
        }

        photonView.RPC(nameof(ReceivePendingWreckLootRpc), photonView.Owner, pending.ViewId, pending.LootIndex, pending.ItemId, pending.BlueprintItemId ?? string.Empty);
    }

    void ResolveNovaPendingDroppedCargo(NovaPendingCollectible pending)
    {
        if (!IsReservationOwnedByCurrentPlayer(ReservedDroppedCargoLoot, pending.ViewId))
            return;

        PhotonView targetView = PhotonView.Find(pending.ViewId);
        DroppedCargoCrate crate = targetView != null ? targetView.GetComponent<DroppedCargoCrate>() : null;
        if (crate == null || !crate.HasLoot || string.IsNullOrWhiteSpace(pending.ItemId) ||
            !string.Equals(crate.StoredItemId, pending.ItemId, System.StringComparison.Ordinal))
        {
            ReleaseNovaDroppedCargoReservation(pending.ViewId, crate);
            return;
        }

        photonView.RPC(nameof(ReceivePendingDroppedCargoLootRpc), photonView.Owner, pending.ViewId, pending.ItemId);
    }

    bool IsReservationOwnedByCurrentPlayer(Dictionary<int, int> reservations, int viewId)
    {
        return photonView != null &&
               reservations != null &&
               reservations.TryGetValue(viewId, out int reservedActor) &&
               reservedActor == photonView.OwnerActorNr;
    }

    void ReleaseNovaTreasureReservation(int viewId, Treasure treasure)
    {
        if (IsReservationOwnedByCurrentPlayer(ReservedTreasureCollections, viewId))
            ReservedTreasureCollections.Remove(viewId);

        if (treasure != null)
            treasure.isBeingCollected = false;
    }

    void ReleaseNovaRandomLootWreckReservation(int viewId, Treasure wreck)
    {
        if (IsReservationOwnedByCurrentPlayer(ReservedRandomLootWrecks, viewId))
            ReservedRandomLootWrecks.Remove(viewId);

        if (wreck != null)
            wreck.isBeingCollected = false;
    }

    void ReleaseNovaWreckReservation(int viewId, ShipWreck wreck)
    {
        if (IsReservationOwnedByCurrentPlayer(ReservedWreckLoot, viewId))
            ReservedWreckLoot.Remove(viewId);

        if (wreck != null)
            wreck.isBeingCollected = false;
    }

    void ReleaseNovaDroppedCargoReservation(int viewId, DroppedCargoCrate crate)
    {
        if (IsReservationOwnedByCurrentPlayer(ReservedDroppedCargoLoot, viewId))
            ReservedDroppedCargoLoot.Remove(viewId);

        if (crate != null)
            crate.isBeingCollected = false;
    }

}
