using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public partial class PlayerShooting
{
    bool TryBeginDropbotLaunchRequest(Photon.Realtime.Player owner, string itemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            photonView == null ||
            owner == null ||
            !string.Equals(itemId, InventoryItemCatalog.DropbotId, StringComparison.Ordinal))
        {
            return false;
        }

        if (pendingDropbotLaunchActive)
        {
            if (Time.time <= pendingDropbotLaunchExpiresAt)
                return false;

            ClearPendingDropbotLaunch();
        }

        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        int baseCapacity = PlayerProfileService.GetPlayerShipInventoryCapacity(owner);
        ShipDamageState damageState = GetComponent<ShipDamageState>();
        int shipCapacity = damageState != null ? damageState.GetAdjustedCargoCapacity(baseCapacity) : baseCapacity;
        int dropSlotIndex = PlayerProfileService.GetDropbotCargoSlotIndex(shipSkinIndex, shipCapacity, equipmentSlots);
        if (dropSlotIndex < 0)
            return false;

        string[] shipSlots = PlayerProfileService.GetPlayerShipInventorySlots(owner);
        if (dropSlotIndex >= shipSlots.Length)
            return false;

        string carriedItemId = shipSlots[dropSlotIndex];
        if (string.IsNullOrWhiteSpace(carriedItemId))
            return false;

        pendingDropbotLaunchRequestId++;
        if (pendingDropbotLaunchRequestId <= 0)
            pendingDropbotLaunchRequestId = 1;

        pendingDropbotLaunchActive = true;
        pendingDropbotCargoSlotIndex = dropSlotIndex;
        pendingDropbotCargoItemId = carriedItemId;
        pendingDropbotOwnerActorNumber = owner.ActorNumber;
        pendingDropbotLaunchExpiresAt = Time.time + DropbotLaunchRequestTimeout;
        photonView.RPC(nameof(PrepareDropbotCargoLaunchRpc), owner, pendingDropbotLaunchRequestId, dropSlotIndex, carriedItemId);
        return true;
    }

    [PunRPC]
    async void PrepareDropbotCargoLaunchRpc(int requestId, int slotIndex, string expectedItemId)
    {
        if (photonView == null || !photonView.IsMine)
            return;

        string removedItemId = null;
        bool removed = false;
        try
        {
            removedItemId = await PlayerProfileService.Instance.RemoveDropbotCargoItemDeferredSaveAsync(slotIndex, expectedItemId);
            removed = string.Equals(removedItemId, expectedItemId, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Debug.LogError("Dropbot cargo pickup failed: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ConfirmDropbotCargoLaunchRpc), RpcTarget.MasterClient, requestId, slotIndex, removedItemId ?? expectedItemId ?? string.Empty, removed);
    }

    [PunRPC]
    void ConfirmDropbotCargoLaunchRpc(int requestId, int slotIndex, string itemId, bool removed, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null)
            return;

        bool valid =
            pendingDropbotLaunchActive &&
            requestId == pendingDropbotLaunchRequestId &&
            slotIndex == pendingDropbotCargoSlotIndex &&
            messageInfo.Sender != null &&
            messageInfo.Sender.ActorNumber == pendingDropbotOwnerActorNumber &&
            string.Equals(itemId, pendingDropbotCargoItemId, StringComparison.Ordinal);

        if (!valid)
        {
            if (removed && messageInfo.Sender != null && !string.IsNullOrWhiteSpace(itemId))
                photonView.RPC(nameof(RestoreDropbotCargoItemRpc), messageInfo.Sender, slotIndex, itemId);
            return;
        }

        if (!removed)
        {
            ClearPendingDropbotLaunch();
            return;
        }

        Photon.Realtime.Player owner = photonView.Owner;
        int maxCharges = ResolveEquippedGadgetMaxCharges(owner, InventoryItemCatalog.DropbotId);
        int remainingCharges = owner != null ? GetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, InventoryItemCatalog.DropbotId, maxCharges) : 0;
        if (maxCharges <= 0 || remainingCharges <= 0)
        {
            photonView.RPC(nameof(RestoreDropbotCargoItemRpc), messageInfo.Sender, slotIndex, itemId);
            ClearPendingDropbotLaunch();
            return;
        }

        bool launched = NewItemsRuntime.TryLaunchDropbot(this, itemId);
        if (!launched)
        {
            photonView.RPC(nameof(RestoreDropbotCargoItemRpc), messageInfo.Sender, slotIndex, itemId);
            ClearPendingDropbotLaunch();
            return;
        }

        SetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, InventoryItemCatalog.DropbotId, remainingCharges - 1, maxCharges);
        RoundXpTracker.RecordGadgetSuccess(owner, InventoryItemCatalog.DropbotId);
        ClearPendingDropbotLaunch();
    }

    [PunRPC]
    async void RestoreDropbotCargoItemRpc(int slotIndex, string itemId)
    {
        if (photonView == null || !photonView.IsMine || string.IsNullOrWhiteSpace(itemId))
            return;

        try
        {
            await PlayerProfileService.Instance.RestoreShipItemAtAsync(slotIndex, itemId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Dropbot cargo restore failed: " + ex);
        }
    }

    void ClearPendingDropbotLaunch()
    {
        pendingDropbotLaunchActive = false;
        pendingDropbotCargoSlotIndex = -1;
        pendingDropbotOwnerActorNumber = 0;
        pendingDropbotCargoItemId = null;
        pendingDropbotLaunchExpiresAt = 0f;
    }
}
