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
    bool TryBeginLootHookRequest(Photon.Realtime.Player owner, string itemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            photonView == null ||
            owner == null ||
            !string.Equals(itemId, InventoryItemCatalog.LootHookId, StringComparison.Ordinal))
        {
            return false;
        }

        if (authoritativeLootHookRoutine != null)
            return false;

        if (pendingLootHookActive)
        {
            if (Time.time <= pendingLootHookExpiresAt)
                return false;

            RestorePendingNeutralLootHookCargo();
            ClearPendingLootHook();
        }

        if (!TryFindLootHookTarget(owner, out PlayerShooting victim, out int slotIndex, out string stolenItemId))
        {
            if (TryBeginNeutralRiderLootHookRequest(owner))
                return true;

            return false;
        }

        return TryStartLootHookWindup(owner, victim.photonView, slotIndex, stolenItemId);
    }

    bool TryBeginNeutralRiderLootHookRequest(Photon.Realtime.Player owner)
    {
        if (!TryFindNeutralRiderLootHookTarget(owner, out NeutralRiderController neutralVictim, out string stolenItemId) ||
            neutralVictim == null ||
            neutralVictim.photonView == null)
        {
            return false;
        }

        return TryStartLootHookWindup(owner, neutralVictim.photonView, LootHookNeutralCargoSlotIndex, stolenItemId);
    }

    bool TryStartLootHookWindup(Photon.Realtime.Player owner, PhotonView targetView, int slotIndex, string stolenItemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            owner == null ||
            targetView == null ||
            string.IsNullOrWhiteSpace(stolenItemId) ||
            authoritativeLootHookRoutine != null ||
            !IsLootHookTargetStillValid(owner, targetView, slotIndex, stolenItemId, LootHookRange))
        {
            return false;
        }

        activeLootHookTargetViewId = targetView.ViewID;
        activeLootHookCargoSlotIndex = slotIndex;
        activeLootHookCargoItemId = stolenItemId;
        authoritativeLootHookRoutine = StartCoroutine(LootHookWindupRoutine(targetView.ViewID, slotIndex, stolenItemId));
        return true;
    }

    IEnumerator LootHookWindupRoutine(int targetViewId, int slotIndex, string stolenItemId)
    {
        float startedAt = Time.time;
        WaitForSeconds wait = new WaitForSeconds(LootHookValidationInterval);

        while (Time.time - startedAt < LootHookWindupDuration)
        {
            PhotonView targetView = PhotonView.Find(targetViewId);
            if (!IsActiveLootHookWindup(targetViewId, slotIndex, stolenItemId) ||
                !IsLootHookTargetStillValid(photonView != null ? photonView.Owner : null, targetView, slotIndex, stolenItemId, LootHookRange))
            {
                authoritativeLootHookRoutine = null;
                ClearActiveLootHookWindup();
                yield break;
            }

            yield return wait;
        }

        authoritativeLootHookRoutine = null;

        PhotonView completedTargetView = PhotonView.Find(targetViewId);
        if (IsActiveLootHookWindup(targetViewId, slotIndex, stolenItemId) &&
            IsLootHookTargetStillValid(photonView != null ? photonView.Owner : null, completedTargetView, slotIndex, stolenItemId, LootHookRange))
        {
            TryCompleteLootHookWindup(completedTargetView, slotIndex, stolenItemId);
        }

        ClearActiveLootHookWindup();
    }

    bool TryCompleteLootHookWindup(PhotonView targetView, int slotIndex, string stolenItemId)
    {
        if (!PhotonNetwork.IsMasterClient || targetView == null || photonView == null)
            return false;

        Photon.Realtime.Player owner = photonView.Owner;
        if (!IsLootHookTargetStillValid(owner, targetView, slotIndex, stolenItemId, LootHookRange))
            return false;

        if (slotIndex == LootHookNeutralCargoSlotIndex)
        {
            NeutralRiderController neutralVictim = targetView.GetComponent<NeutralRiderController>();
            return TryCompleteNeutralRiderLootHookRequest(owner, neutralVictim, stolenItemId);
        }

        PlayerShooting victim = targetView.GetComponent<PlayerShooting>();
        return BeginLootHookVictimCargoRequest(victim, slotIndex, stolenItemId);
    }

    bool BeginLootHookVictimCargoRequest(PlayerShooting victim, int slotIndex, string stolenItemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            victim == null ||
            victim.photonView == null ||
            victim.photonView.Owner == null ||
            string.IsNullOrWhiteSpace(stolenItemId))
        {
            return false;
        }

        pendingLootHookRequestId++;
        if (pendingLootHookRequestId <= 0)
            pendingLootHookRequestId = 1;

        pendingLootHookActive = true;
        pendingLootHookVictimViewId = victim.photonView.ViewID;
        pendingLootHookVictimActorNumber = victim.photonView.Owner.ActorNumber;
        pendingLootHookCargoSlotIndex = slotIndex;
        pendingLootHookCargoItemId = stolenItemId;
        pendingLootHookExpiresAt = Time.time + LootHookRequestTimeout;
        victim.photonView.RPC(nameof(PrepareLootHookVictimCargoRpc), victim.photonView.Owner, pendingLootHookRequestId, slotIndex, stolenItemId, photonView.ViewID);
        return true;
    }

    bool TryCompleteNeutralRiderLootHookRequest(Photon.Realtime.Player owner, NeutralRiderController neutralVictim, string stolenItemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            owner == null ||
            neutralVictim == null ||
            neutralVictim.photonView == null ||
            string.IsNullOrWhiteSpace(stolenItemId) ||
            !IsLootHookTargetStillValid(owner, neutralVictim.photonView, LootHookNeutralCargoSlotIndex, stolenItemId, LootHookRange))
        {
            return false;
        }

        if (!neutralVictim.TryRemoveLootHookCargo(stolenItemId, out string removedItemId) ||
            string.IsNullOrWhiteSpace(removedItemId))
        {
            return false;
        }

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(owner, removedItemId))
        {
            neutralVictim.RestoreLootHookCargo(removedItemId);
            return false;
        }

        pendingLootHookRequestId++;
        if (pendingLootHookRequestId <= 0)
            pendingLootHookRequestId = 1;

        pendingLootHookActive = true;
        pendingLootHookVictimViewId = neutralVictim.photonView.ViewID;
        pendingLootHookVictimActorNumber = 0;
        pendingLootHookCargoSlotIndex = LootHookNeutralCargoSlotIndex;
        pendingLootHookCargoItemId = removedItemId;
        pendingLootHookExpiresAt = Time.time + LootHookRequestTimeout;
        photonView.RPC(nameof(ReceiveLootHookStolenItemRpc), photonView.Owner, pendingLootHookRequestId, removedItemId, neutralVictim.photonView.ViewID, LootHookNeutralCargoSlotIndex);
        return true;
    }

    bool TryFindLootHookTarget(Photon.Realtime.Player owner, out PlayerShooting victim, out int slotIndex, out string itemId)
    {
        victim = null;
        slotIndex = -1;
        itemId = string.Empty;

        if (owner == null)
            return false;

        int ownerActorNumber = owner.ActorNumber;
        Vector2 sourcePosition = transform.position;
        float bestDistance = float.MaxValue;
        PlayerHealth[] healths = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidateHealth = healths[i];
            if (candidateHealth == null ||
                candidateHealth == GetComponent<PlayerHealth>() ||
                candidateHealth.IsWreck ||
                candidateHealth.IsBotControlled ||
                candidateHealth.IsNeutralRiderControlled ||
                candidateHealth.IsAstronautControlled ||
                candidateHealth.IsEvacuationAnimating ||
                candidateHealth.photonView == null ||
                candidateHealth.photonView.Owner == null ||
                candidateHealth.photonView.Owner.ActorNumber == ownerActorNumber)
            {
                continue;
            }

            float distance = Vector2.Distance(sourcePosition, candidateHealth.transform.position);
            if (distance > LootHookRange)
                continue;

            if (!TrySelectLootHookCargo(owner, candidateHealth.photonView.Owner, out int candidateSlot, out string candidateItemId))
                continue;

            bool better =
                victim == null ||
                distance < bestDistance - 0.35f;
            if (!better)
                continue;

            PlayerShooting candidateShooting = candidateHealth.GetComponent<PlayerShooting>();
            if (candidateShooting == null || candidateShooting.photonView == null)
                continue;

            victim = candidateShooting;
            slotIndex = candidateSlot;
            itemId = candidateItemId;
            bestDistance = distance;
        }

        return victim != null && slotIndex >= 0 && !string.IsNullOrWhiteSpace(itemId);
    }

    bool TryFindNeutralRiderLootHookTarget(Photon.Realtime.Player owner, out NeutralRiderController rider, out string itemId)
    {
        rider = null;
        itemId = string.Empty;

        if (owner == null)
            return false;

        Vector2 sourcePosition = transform.position;
        float bestDistance = float.MaxValue;
        NeutralRiderController[] riders = FindObjectsByType<NeutralRiderController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < riders.Length; i++)
        {
            NeutralRiderController candidate = riders[i];
            if (candidate == null ||
                candidate.gameObject == gameObject ||
                candidate.photonView == null)
            {
                continue;
            }

            PlayerHealth candidateHealth = candidate.GetComponent<PlayerHealth>();
            if (candidateHealth == null ||
                candidateHealth.IsWreck ||
                candidateHealth.IsEvacuationAnimating)
            {
                continue;
            }

            float distance = Vector2.Distance(sourcePosition, candidate.transform.position);
            if (distance > LootHookRange)
                continue;

            if (!candidate.TrySelectLootHookCargo(out string candidateItemId) ||
                !PlayerProfileService.PlayerHasFreeShipInventorySlot(owner, candidateItemId))
            {
                continue;
            }

            bool better =
                rider == null ||
                distance < bestDistance - 0.35f;
            if (!better)
                continue;

            rider = candidate;
            itemId = candidateItemId;
            bestDistance = distance;
        }

        return rider != null && !string.IsNullOrWhiteSpace(itemId);
    }

    bool IsActiveLootHookWindup(int targetViewId, int slotIndex, string itemId)
    {
        return activeLootHookTargetViewId == targetViewId &&
               activeLootHookCargoSlotIndex == slotIndex &&
               string.Equals(activeLootHookCargoItemId, itemId, StringComparison.Ordinal);
    }

    bool IsLootHookTargetStillValid(Photon.Realtime.Player owner, PhotonView targetView, int slotIndex, string itemId, float maxRange)
    {
        if (!IsLootHookSourceReady(owner) ||
            targetView == null ||
            string.IsNullOrWhiteSpace(itemId) ||
            GetLootHookDistance(targetView) > maxRange)
        {
            return false;
        }

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        if (targetHealth == null ||
            targetHealth.IsWreck ||
            targetHealth.IsEvacuationAnimating ||
            targetHealth.CurrentHP <= 0)
        {
            return false;
        }

        if (slotIndex == LootHookNeutralCargoSlotIndex)
        {
            NeutralRiderController neutralRider = targetView.GetComponent<NeutralRiderController>();
            return neutralRider != null &&
                   !neutralRider.IsEvacuated &&
                   PlayerProfileService.PlayerHasFreeShipInventorySlot(owner, itemId);
        }

        if (targetHealth == GetComponent<PlayerHealth>() ||
            targetHealth.IsBotControlled ||
            targetHealth.IsNeutralRiderControlled ||
            targetHealth.IsAstronautControlled ||
            targetView.Owner == null ||
            targetView.Owner.ActorNumber == owner.ActorNumber)
        {
            return false;
        }

        return IsLootHookCargoStillAvailable(owner, targetView.Owner, slotIndex, itemId);
    }

    bool IsLootHookSourceReady(Photon.Realtime.Player owner)
    {
        return PhotonNetwork.IsMasterClient &&
               IsGameStarted() &&
               photonView != null &&
               photonView.Owner != null &&
               owner != null &&
               photonView.Owner.ActorNumber == owner.ActorNumber &&
               !AreShipControlsBlocked();
    }

    float GetLootHookDistance(PhotonView targetView)
    {
        if (targetView == null)
            return float.MaxValue;

        return Vector2.Distance(transform.position, targetView.transform.position);
    }

    static bool IsLootHookCargoStillAvailable(Photon.Realtime.Player thief, Photon.Realtime.Player victim, int slotIndex, string itemId)
    {
        if (thief == null || victim == null || slotIndex < 0 || string.IsNullOrWhiteSpace(itemId))
            return false;

        string[] slots = PlayerProfileService.GetPlayerShipInventorySlots(victim);
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(victim, 0);
        int capacity = PlayerProfileService.GetPlayerShipInventoryCapacity(victim);
        if (slots == null ||
            slotIndex >= slots.Length ||
            slotIndex >= capacity ||
            PlayerProfileService.IsSafePocketIndex(shipSkinIndex, slotIndex) ||
            PlayerProfileService.IsAstronautCargoIndex(shipSkinIndex, capacity, slotIndex))
        {
            return false;
        }

        return string.Equals(slots[slotIndex], itemId, StringComparison.Ordinal) &&
               PlayerProfileService.PlayerHasFreeShipInventorySlot(thief, itemId);
    }

    static bool TrySelectLootHookCargo(Photon.Realtime.Player thief, Photon.Realtime.Player victim, out int slotIndex, out string itemId)
    {
        slotIndex = -1;
        itemId = string.Empty;

        if (thief == null || victim == null)
            return false;

        string[] slots = PlayerProfileService.GetPlayerShipInventorySlots(victim);
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(victim, 0);
        int capacity = PlayerProfileService.GetPlayerShipInventoryCapacity(victim);
        List<int> candidateSlots = new List<int>();
        for (int i = 0; i < slots.Length && i < capacity; i++)
        {
            if (PlayerProfileService.IsSafePocketIndex(shipSkinIndex, i) ||
                PlayerProfileService.IsAstronautCargoIndex(shipSkinIndex, capacity, i))
            {
                continue;
            }

            string candidateItemId = slots[i];
            if (string.IsNullOrWhiteSpace(candidateItemId))
                continue;

            if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(thief, candidateItemId))
                continue;

            candidateSlots.Add(i);
        }

        if (candidateSlots.Count <= 0)
            return false;

        slotIndex = candidateSlots[UnityEngine.Random.Range(0, candidateSlots.Count)];
        itemId = slots[slotIndex];
        return slotIndex >= 0;
    }

    [PunRPC]
    async void PrepareLootHookVictimCargoRpc(int requestId, int slotIndex, string expectedItemId, int attackerViewId)
    {
        if (photonView == null || !photonView.IsMine)
            return;

        string removedItemId = null;
        bool removed = false;
        try
        {
            removedItemId = await PlayerProfileService.Instance.RemoveLootHookCargoItemDeferredSaveAsync(slotIndex, expectedItemId);
            removed = string.Equals(removedItemId, expectedItemId, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Debug.LogError("Loot Hook victim cargo removal failed: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ConfirmLootHookVictimCargoRpc), RpcTarget.MasterClient, requestId, slotIndex, removedItemId ?? expectedItemId ?? string.Empty, removed, attackerViewId);
    }

    [PunRPC]
    void ConfirmLootHookVictimCargoRpc(int requestId, int slotIndex, string itemId, bool removed, int attackerViewId, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        PhotonView attackerView = PhotonView.Find(attackerViewId);
        PlayerShooting attacker = attackerView != null ? attackerView.GetComponent<PlayerShooting>() : null;
        if (attacker == null)
        {
            if (removed)
                photonView.RPC(nameof(RestoreLootHookCargoItemRpc), messageInfo.Sender, slotIndex, itemId);
            return;
        }

        attacker.ResolveLootHookVictimConfirmation(this, requestId, slotIndex, itemId, removed, messageInfo.Sender);
    }

    void ResolveLootHookVictimConfirmation(PlayerShooting victim, int requestId, int slotIndex, string itemId, bool removed, Photon.Realtime.Player sender)
    {
        if (!PhotonNetwork.IsMasterClient || victim == null || victim.photonView == null)
            return;

        bool valid =
            pendingLootHookActive &&
            requestId == pendingLootHookRequestId &&
            victim.photonView.ViewID == pendingLootHookVictimViewId &&
            slotIndex == pendingLootHookCargoSlotIndex &&
            sender != null &&
            sender.ActorNumber == pendingLootHookVictimActorNumber &&
            string.Equals(itemId, pendingLootHookCargoItemId, StringComparison.Ordinal);

        if (!valid)
        {
            if (removed && sender != null)
                victim.photonView.RPC(nameof(RestoreLootHookCargoItemRpc), sender, slotIndex, itemId);
            return;
        }

        if (!removed)
        {
            ClearPendingLootHook();
            return;
        }

        if (photonView.Owner == null || !PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, itemId))
        {
            victim.photonView.RPC(nameof(RestoreLootHookCargoItemRpc), sender, slotIndex, itemId);
            ClearPendingLootHook();
            return;
        }

        photonView.RPC(nameof(ReceiveLootHookStolenItemRpc), photonView.Owner, requestId, itemId, victim.photonView.ViewID, slotIndex);
    }

    [PunRPC]
    async void ReceiveLootHookStolenItemRpc(int requestId, string itemId, int victimViewId, int victimSlotIndex)
    {
        if (photonView == null || !photonView.IsMine)
            return;

        bool stored = false;
        try
        {
            stored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(itemId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Loot Hook stolen cargo store failed: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ConfirmLootHookStoredItemRpc), RpcTarget.MasterClient, requestId, itemId, victimViewId, victimSlotIndex, stored);
    }

    [PunRPC]
    void ConfirmLootHookStoredItemRpc(int requestId, string itemId, int victimViewId, int victimSlotIndex, bool stored, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null)
            return;

        bool valid =
            pendingLootHookActive &&
            requestId == pendingLootHookRequestId &&
            victimViewId == pendingLootHookVictimViewId &&
            victimSlotIndex == pendingLootHookCargoSlotIndex &&
            string.Equals(itemId, pendingLootHookCargoItemId, StringComparison.Ordinal) &&
            photonView.Owner != null &&
            messageInfo.Sender != null &&
            messageInfo.Sender.ActorNumber == photonView.Owner.ActorNumber;

        if (!valid)
            return;

        PhotonView victimView = PhotonView.Find(victimViewId);
        if (!stored)
        {
            RestoreLootHookSourceCargo(victimView, victimSlotIndex, itemId);
            ClearPendingLootHook();
            return;
        }

        Photon.Realtime.Player owner = photonView.Owner;
        int maxCharges = ResolveEquippedGadgetMaxCharges(owner, InventoryItemCatalog.LootHookId);
        int remainingCharges = owner != null ? GetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, InventoryItemCatalog.LootHookId, maxCharges) : 0;
        if (maxCharges > 0 && remainingCharges > 0)
            SetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, InventoryItemCatalog.LootHookId, remainingCharges - 1, maxCharges);

        RoundXpTracker.RecordGadgetSuccess(owner, InventoryItemCatalog.LootHookId);
        photonView.RPC(nameof(PlayLootHookFxRpc), RpcTarget.All, victimViewId, itemId);
        ClearPendingLootHook();
    }

    void RestorePendingNeutralLootHookCargo()
    {
        if (!PhotonNetwork.IsMasterClient || pendingLootHookCargoSlotIndex != LootHookNeutralCargoSlotIndex)
            return;

        PhotonView victimView = PhotonView.Find(pendingLootHookVictimViewId);
        RestoreLootHookSourceCargo(victimView, pendingLootHookCargoSlotIndex, pendingLootHookCargoItemId);
    }

    void RestoreLootHookSourceCargo(PhotonView victimView, int victimSlotIndex, string itemId)
    {
        if (victimView == null || string.IsNullOrWhiteSpace(itemId))
            return;

        if (victimSlotIndex == LootHookNeutralCargoSlotIndex)
        {
            NeutralRiderController neutralVictim = victimView.GetComponent<NeutralRiderController>();
            if (neutralVictim != null)
                neutralVictim.RestoreLootHookCargo(itemId);
            return;
        }

        PlayerShooting victim = victimView.GetComponent<PlayerShooting>();
        if (victim != null && victim.photonView != null && victim.photonView.Owner != null)
            victim.photonView.RPC(nameof(RestoreLootHookCargoItemRpc), victim.photonView.Owner, victimSlotIndex, itemId);
    }

    [PunRPC]
    async void RestoreLootHookCargoItemRpc(int slotIndex, string itemId)
    {
        if (photonView == null || !photonView.IsMine || string.IsNullOrWhiteSpace(itemId))
            return;

        try
        {
            await PlayerProfileService.Instance.RestoreShipItemAtAsync(slotIndex, itemId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Loot Hook cargo restore failed: " + ex);
        }
    }

    [PunRPC]
    void PlayLootHookFxRpc(int victimViewId, string itemId)
    {
        PhotonView victimView = PhotonView.Find(victimViewId);
        if (victimView != null)
            LootHookSnatchVfx.Spawn(transform, victimView.transform);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayLootHookAt(transform.position);
    }

    [PunRPC]
    void PlayBioTrapFxRpc(int targetViewId)
    {
        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView != null)
            BioTrapCaptureVfx.Spawn(transform.position, targetView.transform.position);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBioTrapCaptureAt(targetView != null ? targetView.transform.position : transform.position);
    }

    [PunRPC]
    void PlayTetherHarpoonFxRpc(int targetViewId)
    {
        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView != null)
            LootHookSnatchVfx.Spawn(transform, targetView.transform);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayTetherHarpoonAt(transform.position);
    }

    [PunRPC]
    void PlayAsteroidBreacherFxRpc(float x, float y)
    {
        Vector3 position = new Vector3(x, y, transform.position.z);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAsteroidBreacherAt(position);
    }

    void ClearPendingLootHook()
    {
        pendingLootHookActive = false;
        pendingLootHookVictimViewId = 0;
        pendingLootHookVictimActorNumber = 0;
        pendingLootHookCargoSlotIndex = -1;
        pendingLootHookCargoItemId = null;
        pendingLootHookExpiresAt = 0f;
    }

    void StopAuthoritativeLootHookWindup()
    {
        if (authoritativeLootHookRoutine != null)
        {
            StopCoroutine(authoritativeLootHookRoutine);
            authoritativeLootHookRoutine = null;
        }

        ClearActiveLootHookWindup();
    }

    void ClearActiveLootHookWindup()
    {
        activeLootHookTargetViewId = 0;
        activeLootHookCargoSlotIndex = -1;
        activeLootHookCargoItemId = null;
    }
}
