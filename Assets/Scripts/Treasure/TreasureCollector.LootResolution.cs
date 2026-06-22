using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public partial class TreasureCollector
{
    [PunRPC]
    void RequestRandomLootWreck(int viewID)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null)
            return;

        PhotonView wreckView = PhotonView.Find(viewID);
        if (wreckView == null)
            return;

        Treasure wreck = wreckView.GetComponent<Treasure>();
        if (wreck == null || !InventoryItemCatalog.IsRandomLootWreckItem(wreck.itemId))
            return;

        if (ReservedRandomLootWrecks.TryGetValue(viewID, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return;

        string rewardItemId = RollRandomLootWreckReward();
        if (string.IsNullOrWhiteSpace(rewardItemId))
            return;

        bool canStoreReward = PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, rewardItemId) ||
                              PlayerProfileService.PlayerHasFreeEquipmentSlotForItem(photonView.Owner, rewardItemId);
        if (!canStoreReward)
            return;

        ReservedRandomLootWrecks[viewID] = photonView.OwnerActorNr;
        photonView.RPC(nameof(ReceivePendingRandomLootWreckRpc), photonView.Owner, viewID, rewardItemId);
    }

    [PunRPC]
    void RequestLootWreck(int viewID, string[] unlockedBlueprintIds)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        PhotonView wreckView = PhotonView.Find(viewID);
        if (wreckView == null)
            return;

        ShipWreck wreck = wreckView.GetComponent<ShipWreck>();
        if (wreck == null || !wreck.HasLoot || (wreck.isBeingCollected && currentWreck != wreck))
            return;

        if (ReservedWreckLoot.TryGetValue(viewID, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return;

        int lootIndex = wreck.GetFirstLootIndex();
        string itemId = wreck.GetLootItemAt(lootIndex);
        if (lootIndex < 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        if (!PlayerProfileService.PlayerCanStoreShipItemOrAtlasAutoReplace(photonView.Owner, itemId))
            return;

        string blueprintItemId = string.Empty;
        if (wreck.SourceShipSkinIndex < 0)
            BlueprintCatalog.TryRollWreckBlueprintDrop(itemId, wreck.SourceEnemyKindValue, unlockedBlueprintIds, out blueprintItemId);

        ReservedWreckLoot[viewID] = photonView.OwnerActorNr;
        photonView.RPC(nameof(ReceivePendingWreckLootRpc), photonView.Owner, viewID, lootIndex, itemId, blueprintItemId ?? string.Empty);
    }

    [PunRPC]
    void RequestLootDroppedCargo(int viewID)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        PhotonView crateView = PhotonView.Find(viewID);
        if (crateView == null)
            return;

        DroppedCargoCrate crate = crateView.GetComponent<DroppedCargoCrate>();
        if (crate == null || !crate.HasLoot || (crate.isBeingCollected && currentDroppedCargo != crate))
            return;

        if (ReservedDroppedCargoLoot.TryGetValue(viewID, out int reservedActor) && reservedActor != photonView.OwnerActorNr)
            return;

        string itemId = crate.StoredItemId;
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (!PlayerProfileService.PlayerCanStoreShipItemOrAtlasAutoReplace(photonView.Owner, itemId))
            return;

        ReservedDroppedCargoLoot[viewID] = photonView.OwnerActorNr;
        photonView.RPC(nameof(ReceivePendingDroppedCargoLootRpc), photonView.Owner, viewID, itemId);
    }

    string RollRandomLootWreckReward()
    {
        float roll = Random.value;
        InventoryItemCategory category;
        if (roll < 0.7f)
            return RollRandomUtilityEquipmentReward();
        else if (roll < 0.8f)
            category = InventoryItemCategory.Shield;
        else if (roll < 0.9f)
            category = InventoryItemCategory.Weapon;
        else
            category = InventoryItemCategory.Engine;

        string[] itemIds = InventoryItemCatalog.GetEquipmentItemIdsByCategory(category);
        if (itemIds == null || itemIds.Length == 0)
            return string.Empty;

        return itemIds[Random.Range(0, itemIds.Length)];
    }

    string RollRandomUtilityEquipmentReward()
    {
        List<string> itemIds = new List<string>();
        AddEquipmentItemIds(itemIds, InventoryItemCategory.Gadget);
        AddEquipmentItemIds(itemIds, InventoryItemCategory.Support);
        AddEquipmentItemIds(itemIds, InventoryItemCategory.Rescue);
        if (itemIds.Count == 0)
            return string.Empty;

        return itemIds[Random.Range(0, itemIds.Count)];
    }

    void AddEquipmentItemIds(List<string> itemIds, InventoryItemCategory category)
    {
        if (itemIds == null)
            return;

        string[] categoryItemIds = InventoryItemCatalog.GetEquipmentItemIdsByCategory(category);
        if (categoryItemIds == null)
            return;

        itemIds.AddRange(categoryItemIds);
    }


    [PunRPC]
    async void ReceiveLootedItemRpc(string itemId)
    {
        try
        {
            bool stored = await StoreItemToShipWithContainerDropsAsync(itemId);
            if (stored)
            {
                AddScore(RoundXpTracker.RecordWreckLooted(photonView.Owner, false));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to receive wreck loot item: " + ex);
        }
    }

    [PunRPC]
    async void ReceiveNovaTreasureRpc(int treasureViewId, string itemId)
    {
        bool stored = false;
        try
        {
            stored = await StoreItemToShipWithContainerDropsAsync(itemId, itemId, true, true);
            if (stored)
                AddScore(RoundXpTracker.RecordTreasureCollected(photonView.Owner, itemId));
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to store Nova burst treasure item: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ResolveReservedNovaTreasure), RpcTarget.MasterClient, treasureViewId, itemId, stored);
    }

    [PunRPC]
    async void ReceivePendingWreckLootRpc(int wreckViewId, int lootIndex, string itemId, string blueprintItemId)
    {
        bool playerWreck = false;
        PhotonView wreckView = PhotonView.Find(wreckViewId);
        if (wreckView != null)
        {
            ShipWreck wreck = wreckView.GetComponent<ShipWreck>();
            playerWreck = wreck != null && wreck.SourceShipSkinIndex >= 0;
        }

        bool itemStored = false;
        bool blueprintStored = false;
        try
        {
            bool hasBonusBlueprint = InventoryItemCatalog.IsBlueprintItem(blueprintItemId);
            if (hasBonusBlueprint)
            {
                blueprintStored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(blueprintItemId);
                if (blueprintStored)
                    ShowPickupToast(blueprintItemId);
            }

            if (hasBonusBlueprint)
            {
                itemStored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(itemId);
                if (itemStored)
                    ShowPickupToast(itemId);
            }
            else
            {
                itemStored = await StoreItemToShipWithContainerDropsAsync(itemId);
            }

            if (itemStored || blueprintStored)
            {
                AddScore(RoundXpTracker.RecordWreckLooted(photonView.Owner, playerWreck));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to store reserved wreck loot item: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ResolveReservedWreckLoot), RpcTarget.MasterClient, wreckViewId, lootIndex, itemId, blueprintItemId ?? string.Empty, itemStored, blueprintStored);
    }

    [PunRPC]
    async void ReceivePendingDroppedCargoLootRpc(int crateViewId, string itemId)
    {
        bool stored = false;
        try
        {
            stored = await StoreItemToShipWithContainerDropsAsync(itemId);
            if (stored)
            {
                AddScore(RoundXpTracker.RecordDroppedCargoLooted(photonView.Owner));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to store reserved dropped cargo item: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ResolveReservedDroppedCargoLoot), RpcTarget.MasterClient, crateViewId, itemId, stored);
    }

    [PunRPC]
    async void ReceivePendingRandomLootWreckRpc(int wreckViewId, string itemId)
    {
        bool stored = false;
        try
        {
            stored = await PlayerProfileService.Instance.AddRandomLootEquipmentDeferredSaveAsync(itemId);
            if (!stored)
                stored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(itemId);

            if (stored)
            {
                AddScore(RoundXpTracker.RecordWreckLooted(photonView.Owner, false));
                ShowPickupToast(itemId);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to store random loot wreck item: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ResolveReservedRandomLootWreck), RpcTarget.MasterClient, wreckViewId, itemId, stored);
    }

    [PunRPC]
    void ResolveReservedWreckLoot(int wreckViewId, int lootIndex, string itemId, string blueprintItemId, bool itemStored, bool blueprintStored)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!ReservedWreckLoot.TryGetValue(wreckViewId, out int reservedActor) || reservedActor != photonView.OwnerActorNr)
            return;

        ReservedWreckLoot.Remove(wreckViewId);
        PhotonView wreckView = PhotonView.Find(wreckViewId);
        ShipWreck wreck = wreckView != null ? wreckView.GetComponent<ShipWreck>() : null;
        if (wreck != null)
            wreck.isBeingCollected = false;

        if (!itemStored && !blueprintStored)
            return;

        if (wreckView == null || wreck == null || !wreck.HasLoot)
            return;

        string currentItemId = wreck.GetLootItemAt(lootIndex);
        if (!string.Equals(currentItemId, itemId, System.StringComparison.Ordinal))
            return;

        SpaceTrapTarget.DetonateIfArmed(wreckViewId, photonView != null ? photonView.ViewID : 0);
        NotifyPirateBasesAboutCollectedTarget(wreckViewId);
        if (blueprintStored && !itemStored)
            DropOverflowWreckLoot(wreck, itemId);

        wreckView.RPC(nameof(ShipWreck.RemoveLootAtIndexRpc), RpcTarget.All, lootIndex);
    }

    [PunRPC]
    void ResolveReservedNovaTreasure(int treasureViewId, string itemId, bool stored)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!ReservedTreasureCollections.TryGetValue(treasureViewId, out int reservedActor) || reservedActor != photonView.OwnerActorNr)
            return;

        ReservedTreasureCollections.Remove(treasureViewId);
        PhotonView treasureView = PhotonView.Find(treasureViewId);
        Treasure treasure = treasureView != null ? treasureView.GetComponent<Treasure>() : null;
        if (treasure != null)
            treasure.isBeingCollected = false;

        string resolvedTreasureItemId = treasure != null
            ? InventoryItemCatalog.ResolveAlienSecretItemId(treasure.itemId, treasure.visualVariantIndex)
            : null;
        if (!stored || treasureView == null || treasure == null || !string.Equals(resolvedTreasureItemId, itemId, System.StringComparison.Ordinal))
            return;

        SpaceTrapTarget.DetonateIfArmed(treasureViewId, photonView != null ? photonView.ViewID : 0);
        NotifyPirateBasesAboutCollectedTarget(treasureViewId);
        PhotonNetwork.Destroy(treasureView.gameObject);
    }

    void DropOverflowWreckLoot(ShipWreck wreck, string itemId)
    {
        if (wreck == null || string.IsNullOrWhiteSpace(itemId))
            return;

        Vector2 driftDirection = Random.insideUnitCircle.normalized;
        if (driftDirection.sqrMagnitude < 0.001f)
            driftDirection = Vector2.up;

        Vector3 dropPosition = wreck.transform.position + (Vector3)(driftDirection * 0.65f);
        Vector2 driftVelocity = driftDirection * Random.Range(0.45f, 0.85f);
        DroppedCargoManager.DropItemAtPosition(itemId, dropPosition, driftVelocity);
    }

    [PunRPC]
    void ResolveReservedDroppedCargoLoot(int crateViewId, string itemId, bool stored)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!ReservedDroppedCargoLoot.TryGetValue(crateViewId, out int reservedActor) || reservedActor != photonView.OwnerActorNr)
            return;

        ReservedDroppedCargoLoot.Remove(crateViewId);
        PhotonView crateView = PhotonView.Find(crateViewId);
        DroppedCargoCrate crate = crateView != null ? crateView.GetComponent<DroppedCargoCrate>() : null;
        if (crate != null)
            crate.isBeingCollected = false;

        if (!stored)
            return;

        if (crateView == null || crate == null || !crate.HasLoot || !string.Equals(crate.StoredItemId, itemId, System.StringComparison.Ordinal))
            return;

        SpaceTrapTarget.DetonateIfArmed(crateViewId, photonView != null ? photonView.ViewID : 0);
        NotifyPirateBasesAboutCollectedTarget(crateViewId);
        crateView.RPC(nameof(DroppedCargoCrate.ClearStoredItemRpc), RpcTarget.All);
        PhotonNetwork.Destroy(crateView.gameObject);
    }

    [PunRPC]
    void ResolveReservedRandomLootWreck(int wreckViewId, string itemId, bool stored)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!ReservedRandomLootWrecks.TryGetValue(wreckViewId, out int reservedActor) || reservedActor != photonView.OwnerActorNr)
            return;

        ReservedRandomLootWrecks.Remove(wreckViewId);
        PhotonView wreckView = PhotonView.Find(wreckViewId);
        Treasure wreck = wreckView != null ? wreckView.GetComponent<Treasure>() : null;
        if (wreck != null)
            wreck.isBeingCollected = false;

        if (!stored)
            return;

        if (wreckView == null || wreck == null || !InventoryItemCatalog.IsRandomLootWreckItem(wreck.itemId))
            return;

        SpaceTrapTarget.DetonateIfArmed(wreckViewId, photonView != null ? photonView.ViewID : 0);
        NotifyPirateBasesAboutCollectedTarget(wreckViewId);
        PhotonNetwork.Destroy(wreckView.gameObject);
    }

    async void StoreCollectedItem(string itemId, string sourceItemId = null)
    {
        try
        {
            await StoreItemToShipWithContainerDropsAsync(itemId, sourceItemId ?? itemId, true, true);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to store collected item: " + ex);
        }
    }

    bool CanAtlasAutoReplaceCargo(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !PlayerProfileService.HasInstance)
            return false;

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        if (!PilotCatalog.IsSelectedPilot(owner, PilotCatalog.AtlasId))
            return false;

        int slotIndex;
        string replacedItemId;
        return AtlasPilotRoundTracker.CanReplaceLeastValuableRoundCargo(
            PlayerProfileService.Instance.CurrentProfile,
            itemId,
            out slotIndex,
            out replacedItemId);
    }

    async System.Threading.Tasks.Task<bool> AddItemToShipWithAtlasAutoDropDeferredSaveAsync(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !PlayerProfileService.HasInstance)
            return false;

        bool stored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(itemId);
        if (stored)
            return true;

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        if (!PilotCatalog.IsSelectedPilot(owner, PilotCatalog.AtlasId))
            return false;

        int slotIndex;
        string replacedItemId;
        if (!AtlasPilotRoundTracker.CanReplaceLeastValuableRoundCargo(
                PlayerProfileService.Instance.CurrentProfile,
                itemId,
                out slotIndex,
                out replacedItemId))
        {
            return false;
        }

        string droppedItemId = await PlayerProfileService.Instance.ReplaceShipItemDeferredSaveAsync(slotIndex, itemId);
        if (string.IsNullOrWhiteSpace(droppedItemId))
            return false;

        DropAtlasAutoDroppedCargo(droppedItemId);
        return true;
    }

    void DropAtlasAutoDroppedCargo(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        Vector2 driftDirection = Random.insideUnitCircle;
        if (driftDirection.sqrMagnitude < 0.001f)
            driftDirection = Vector2.up;

        driftDirection.Normalize();
        Vector3 dropPosition = transform.position + (Vector3)(driftDirection * 0.72f);
        Vector2 driftVelocity = driftDirection * Random.Range(0.55f, 1.05f);
        DroppedCargoManager.DropItemAtPosition(itemId, dropPosition, driftVelocity);
    }

    async System.Threading.Tasks.Task<bool> StoreItemToShipWithContainerDropsAsync(
        string itemId,
        string sourceItemId = null,
        bool recordAsteroidProgress = false,
        bool allowNovaSpaceJunkBonus = false)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !PlayerProfileService.HasInstance)
            return false;

        bool isBlueprintScrapContainer =
            InventoryItemCatalog.IsBlueprintScrapContainerItem(itemId) ||
            InventoryItemCatalog.IsBlueprintScrapContainerItem(sourceItemId);
        if (isBlueprintScrapContainer)
        {
            bool stored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(InventoryItemCatalog.BlueprintScrapId);
            if (stored)
            {
                ShowPickupToast(InventoryItemCatalog.BlueprintScrapId);
                if (BlueprintCatalog.RollBlueprintScrapContainerBonus())
                {
                    bool bonusStored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(InventoryItemCatalog.BlueprintScrapId);
                    if (bonusStored)
                        ShowPickupToast(InventoryItemCatalog.BlueprintScrapId);
                }
            }

            return stored;
        }

        string storedItemId = BlueprintCatalog.ResolveContainerBlueprintDrop(itemId, GetLocalUnlockedBlueprintIdsForDropRoll());
        bool itemStored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(storedItemId);
        if (!itemStored)
            return false;

        ShowPickupToast(storedItemId);
        BroadcastValuableCargoCollected(storedItemId);

        if (recordAsteroidProgress)
            await RecordAsteroidSalvageProgressAsync(sourceItemId ?? itemId);

        if (allowNovaSpaceJunkBonus && IsNovaSpaceJunkBonusActive(storedItemId))
        {
            bool bonusStored = await AddItemToShipWithAtlasAutoDropDeferredSaveAsync(storedItemId);
            if (bonusStored)
                ShowPickupToast(storedItemId);
        }

        return true;
    }

    string[] GetLocalUnlockedBlueprintIdsForDropRoll()
    {
        return PlayerProfileService.HasInstance && PlayerProfileService.Instance.CurrentProfile != null
            ? PlayerProfileService.Instance.CurrentProfile.UnlockedBlueprintIds
            : new string[0];
    }

    void BroadcastValuableCargoCollected(string itemId)
    {
        if (!InventoryItemCatalog.IsTrackedValuableCargo(itemId) || photonView == null)
            return;

        photonView.RPC(nameof(ShowValuableCargoCollectedRpc), RpcTarget.All, photonView.OwnerActorNr, itemId);
    }

    [PunRPC]
    void ShowValuableCargoCollectedRpc(int actorNumber, string itemId)
    {
        if (!InventoryItemCatalog.IsTrackedValuableCargo(itemId))
            return;

        string playerName = ResolveAnnouncementPlayerName(actorNumber);
        string itemName = InventoryItemCatalog.GetDisplayName(itemId);
        bool pirateCase = string.Equals(itemId, InventoryItemCatalog.PirateCaseId, System.StringComparison.Ordinal);
        string message = pirateCase
            ? playerName + " secured " + itemName + " - pirates hunting!"
            : playerName + " secured " + itemName + ".";

        Color markerColor;
        if (!ValuableCargoCarrierUtility.TryGetTrackedCargoMarkerColor(itemId, out markerColor))
            markerColor = new Color(1f, 0.75f, 0.18f, 0.95f);

        RoundMessageLayer.ShowTopCenter(message, ValuableCargoAnnouncementDuration, markerColor);
    }

    static string ResolveAnnouncementPlayerName(int actorNumber)
    {
        Photon.Realtime.Player player = PhotonNetwork.CurrentRoom != null
            ? PhotonNetwork.CurrentRoom.GetPlayer(actorNumber)
            : null;

        if (player != null && !string.IsNullOrWhiteSpace(player.NickName))
            return player.NickName;

        return "Someone";
    }

    string ResolveCovaxAsteroidCargoItem(string itemId)
    {
        if (!InventoryItemCatalog.IsAsteroidResource(itemId))
            return itemId;

        if (!PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.CovaxId))
            return itemId;

        if (UnityEngine.Random.value >= 0.1f)
            return itemId;

        return InventoryItemCatalog.TryGetNextAsteroidRarityId(itemId, out string upgradedItemId)
            ? upgradedItemId
            : itemId;
    }

    async System.Threading.Tasks.Task RecordAsteroidSalvageProgressAsync(string itemId)
    {
        if (!InventoryItemCatalog.IsAsteroidResource(itemId) || !PlayerProfileService.HasInstance)
            return;

        await PlayerProfileService.Instance.RecordPilotAsteroidSalvageAsync();
    }

    bool IsNovaSpaceJunkBonusActive(string itemId)
    {
        return PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.NovaId) &&
               PilotCatalog.IsSpaceJunkItem(itemId);
    }

    static string BuildViewIdCsv(List<int> viewIds)
    {
        if (viewIds == null || viewIds.Count == 0)
            return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < viewIds.Count; i++)
        {
            if (viewIds[i] <= 0)
                continue;

            if (builder.Length > 0)
                builder.Append(',');

            builder.Append(viewIds[i]);
        }

        return builder.ToString();
    }

    void ShowPickupToast(string itemId)
    {
        if (!photonView.IsMine || string.IsNullOrWhiteSpace(itemId))
            return;

        Sprite icon = InventoryItemCatalog.GetIcon(itemId);
        string label = InventoryItemCatalog.GetDisplayName(itemId);
        if (string.IsNullOrWhiteSpace(label))
            label = InventoryItemCatalog.GetShortLabel(itemId);

        RoundMessageLayer.ShowLeftFeed("LOOT", label, icon, 2f);
    }
}
