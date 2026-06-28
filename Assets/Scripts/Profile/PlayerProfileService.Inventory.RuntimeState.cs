using System;
using Photon.Pun;
using UnityEngine;

public partial class PlayerProfileService
{
    int GetActiveShipSkinIndex()
    {
        int fallbackSkin = CurrentProfile != null ? CurrentProfile.ShipSkinIndex : 0;
        if (PhotonNetwork.LocalPlayer != null &&
            PhotonNetwork.LocalPlayer.CustomProperties != null &&
            PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoomSettings.ShipSkinKey))
        {
            return RoomSettings.GetPlayerShipSkin(PhotonNetwork.LocalPlayer, fallbackSkin);
        }

        return fallbackSkin;
    }

    int GetActiveShipInventoryCapacity()
    {
        int activeSkin = GetActiveShipSkinIndex();
        if (!IsCargoUnlockedForProfile(activeSkin))
            return 0;

        int baseCapacity = GetEffectiveShipInventoryCapacity(
            activeSkin,
            CurrentProfile != null && CurrentProfile.Inventory != null
                ? BuildRuntimeEquipmentSlotsForProfile(activeSkin, CurrentProfile.Inventory.EquipmentSlots)
                : null);
        return ShipDamageState.GetLocalCargoAdjustedCapacity(baseCapacity);
    }

    int GetProfileShipInventoryCapacity(int shipSkinIndex, string[] equipmentSlots)
    {
        if (!IsCargoUnlockedForProfile(shipSkinIndex))
            return 0;

        return GetEffectiveShipInventoryCapacity(shipSkinIndex, BuildRuntimeEquipmentSlotsForProfile(shipSkinIndex, equipmentSlots));
    }

    public int GetShipInventoryCapacityForProfile(int shipSkinIndex, string[] equipmentSlots)
    {
        return GetProfileShipInventoryCapacity(shipSkinIndex, equipmentSlots);
    }

    void ApplyInventoryToPhoton()
    {
        if (CurrentProfile == null || PhotonNetwork.LocalPlayer == null)
        {
            lastAppliedInventoryPhotonSignature = null;
            lastAppliedInventoryPhotonActorNumber = -1;
            return;
        }

        EnsureInventory();
        EnsurePilotDefaults();
        string shipInventoryState = SerializeShipInventorySlots(CurrentProfile.Inventory.ShipSlots);
        string equipmentState = SerializeEquipmentSlots(BuildRuntimeEquipmentSlotsForProfile(CurrentProfile.ShipSkinIndex, CurrentProfile.Inventory.EquipmentSlots));
        bool viperCargoUnlocked = IsCargoUnlockedForProfile(CurrentProfile.ShipSkinIndex);
        int viperRecoveryStage = (int)GetViperRecoveryStage();
        int arrowLicenseStage = (int)GetArrowLicenseStage();
        bool arrowFinalRunReady = GetArrowLicenseProgress().FinalRunEntryAvailable;
        int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        string signature = BuildInventoryPhotonSignature(
            CurrentProfile.SelectedPilotId,
            shipInventoryState,
            equipmentState,
            viperCargoUnlocked,
            viperRecoveryStage,
            arrowLicenseStage,
            arrowFinalRunReady);

        if (actorNumber == lastAppliedInventoryPhotonActorNumber &&
            string.Equals(signature, lastAppliedInventoryPhotonSignature, StringComparison.Ordinal))
        {
            return;
        }

        var props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.PilotIdKey] = CurrentProfile.SelectedPilotId,
            [RoomSettings.ShipInventoryStateKey] = shipInventoryState,
            [RoomSettings.EquipmentStateKey] = equipmentState,
            [PlayerViperCargoUnlockedKey] = viperCargoUnlocked,
            [PlayerViperRecoveryStageKey] = viperRecoveryStage,
            [PlayerArrowLicenseStageKey] = arrowLicenseStage,
            [PlayerArrowFinalRunReadyKey] = arrowFinalRunReady
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        lastAppliedInventoryPhotonActorNumber = actorNumber;
        lastAppliedInventoryPhotonSignature = signature;
    }

    static string BuildInventoryPhotonSignature(
        string pilotId,
        string shipInventoryState,
        string equipmentState,
        bool viperCargoUnlocked,
        int viperRecoveryStage,
        int arrowLicenseStage,
        bool arrowFinalRunReady)
    {
        return (pilotId ?? string.Empty) + "|" +
               (shipInventoryState ?? string.Empty) + "|" +
               (equipmentState ?? string.Empty) + "|" +
               (viperCargoUnlocked ? "1" : "0") + "|" +
               viperRecoveryStage + "|" +
               arrowLicenseStage + "|" +
               (arrowFinalRunReady ? "1" : "0");
    }

    public void SetActiveRoundShipSkin(int shipSkinIndex)
    {
        if (PhotonNetwork.LocalPlayer == null)
            return;

        int safeSkinIndex = Mathf.Clamp(shipSkinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex);
        var props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.ShipSkinKey] = safeSkinIndex,
            [PlayerViperCargoUnlockedKey] = IsCargoUnlockedForProfile(safeSkinIndex),
            [PlayerViperRecoveryStageKey] = (int)GetViperRecoveryStage(),
            [PlayerArrowLicenseStageKey] = (int)GetArrowLicenseStage(),
            [PlayerArrowFinalRunReadyKey] = GetArrowLicenseProgress().FinalRunEntryAvailable
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    void RestoreInventorySource(bool fromShipInventory, int sourceIndex, string itemId)
    {
        if (fromShipInventory)
            CurrentProfile.Inventory.RestoreShip(sourceIndex, itemId);
        else
            CurrentProfile.Inventory.RestorePlayer(sourceIndex, itemId);
    }
}
