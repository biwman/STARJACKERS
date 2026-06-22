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
            return;

        EnsureInventory();
        EnsurePilotDefaults();
        var props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.PilotIdKey] = CurrentProfile.SelectedPilotId,
            [RoomSettings.ShipInventoryStateKey] = SerializeShipInventorySlots(CurrentProfile.Inventory.ShipSlots),
            [RoomSettings.EquipmentStateKey] = SerializeEquipmentSlots(BuildRuntimeEquipmentSlotsForProfile(CurrentProfile.ShipSkinIndex, CurrentProfile.Inventory.EquipmentSlots)),
            [PlayerViperCargoUnlockedKey] = IsCargoUnlockedForProfile(CurrentProfile.ShipSkinIndex),
            [PlayerViperRecoveryStageKey] = (int)GetViperRecoveryStage(),
            [PlayerArrowLicenseStageKey] = (int)GetArrowLicenseStage(),
            [PlayerArrowFinalRunReadyKey] = GetArrowLicenseProgress().FinalRunEntryAvailable
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
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
