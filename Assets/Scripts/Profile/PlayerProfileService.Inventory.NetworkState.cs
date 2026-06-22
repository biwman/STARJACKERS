using System;
using Photon.Pun;
using UnityEngine;

public partial class PlayerProfileService
{
    public const int DefaultAstronautCargoSlotCount = 1;

    const string PlayerViperCargoUnlockedKey = "profile_runtime_viper_cargo_unlocked";
    const string PlayerViperRecoveryStageKey = "profile_runtime_viper_recovery_stage";
    const string PlayerArrowLicenseStageKey = "profile_runtime_arrow_license_stage";
    const string PlayerArrowFinalRunReadyKey = "profile_runtime_arrow_final_ready";
    const string PlayerBisonIndustrialPartsDeliveredKey = "profile_runtime_bison_parts_delivered";
    const string PlayerInvaderImprintsRecoveredKey = "profile_runtime_invader_imprints";

    public static bool PlayerHasFreeShipInventorySlot(Photon.Realtime.Player player)
    {
        return PlayerHasFreeShipInventorySlot(player, null);
    }

    public static bool PlayerHasFreeGadgetEquipmentSlot(Photon.Realtime.Player player)
    {
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        return GetFirstFreeEquipmentSlotByCategory(GetPlayerEquipmentSlots(player), shipSkinIndex, InventoryItemCategory.Gadget) >= 0;
    }

    public static bool PlayerHasFreeEquipmentSlotForItem(Photon.Realtime.Player player, string itemId)
    {
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        string[] equipmentSlots = GetPlayerEquipmentSlots(player);
        if (IsSingleInstallItem(itemId) && IsItemAlreadyEquipped(equipmentSlots, itemId, -1))
            return false;

        return GetFirstFreeEquipmentSlotForItem(equipmentSlots, shipSkinIndex, itemId) >= 0;
    }

    public static bool PlayerHasFreeUtilityEquipmentSlot(Photon.Realtime.Player player)
    {
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        string[] slots = GetPlayerEquipmentSlots(player);
        return GetFirstFreeEquipmentSlotByCategory(slots, shipSkinIndex, InventoryItemCategory.Gadget) >= 0 ||
               GetFirstFreeEquipmentSlotByCategory(slots, shipSkinIndex, InventoryItemCategory.Support) >= 0 ||
               GetFirstFreeEquipmentSlotByCategory(slots, shipSkinIndex, InventoryItemCategory.Rescue) >= 0;
    }

    public static string[] GetPlayerShipInventorySlots(Photon.Realtime.Player player)
    {
        if (player != null &&
            player.CustomProperties.TryGetValue(RoomSettings.ShipInventoryStateKey, out object value) &&
            value is string raw)
        {
            return DeserializeShipInventorySlots(raw);
        }

        return new string[PlayerInventoryData.ShipSlotCount];
    }

    public static bool PlayerHasAvengerStartingCodes(Photon.Realtime.Player player)
    {
        if (player == null)
            return false;

        string[] slots = GetPlayerShipInventorySlots(player);
        int capacity = GetPlayerShipInventoryCapacity(player);
        return CountItemInSlots(slots, capacity, InventoryItemCatalog.AvengerStartingCodesId) > 0;
    }

    public static bool PlayerHasPirateSymbolInSafePocket(Photon.Realtime.Player player)
    {
        if (player == null)
            return false;

        string[] slots = GetPlayerShipInventorySlots(player);
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(player, 0);
        int capacity = GetPlayerShipInventoryCapacity(player);
        int limit = Mathf.Clamp(capacity, 0, slots != null ? slots.Length : 0);
        for (int i = 0; i < limit; i++)
        {
            if (!IsSafePocketIndex(shipSkinIndex, i))
                continue;

            if (string.Equals(slots[i], InventoryItemCatalog.PirateSymbolId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }


    public static int GetPlayerShipInventoryCapacity(Photon.Realtime.Player player)
    {
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        if (IsPlayerViperCargoLocked(player, shipSkinIndex))
            return 0;

        return GetEffectiveShipInventoryCapacity(shipSkinIndex, GetPlayerEquipmentSlots(player));
    }

    static bool IsPlayerViperCargoLocked(Photon.Realtime.Player player, int shipSkinIndex)
    {
        if (ShipCatalog.GetShipTypeFromSkinIndex(shipSkinIndex) != ShipType.Viper || player == null)
            return false;

        if (player.CustomProperties != null &&
            player.CustomProperties.TryGetValue(PlayerViperCargoUnlockedKey, out object value) &&
            value is bool cargoUnlocked)
        {
            return !cargoUnlocked;
        }

        return false;
    }

    public static int GetEffectiveShipInventoryCapacity(int shipSkinIndex, string[] equipmentSlots)
    {
        int baseCapacity = ShipCatalog.GetShipInventoryCapacity(shipSkinIndex);
        int cargoExtensions = InventoryItemCatalog.CountEquippedItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.CargoBayExtensionId);
        return Mathf.Clamp(baseCapacity + cargoExtensions * 2, 0, PlayerInventoryData.ShipSlotCount);
    }

    public static int GetSafePocketSlotCount(int shipSkinIndex)
    {
        return ShipCatalog.GetSafePocketSlots(shipSkinIndex);
    }

    public static int GetSafePocketStartIndex(int shipSkinIndex)
    {
        int safePocketCount = GetSafePocketSlotCount(shipSkinIndex);
        return safePocketCount > 0 ? 0 : PlayerInventoryData.ShipSlotCount;
    }

    public static bool IsSafePocketIndex(int shipSkinIndex, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= PlayerInventoryData.ShipSlotCount)
            return false;

        int safePocketCount = GetSafePocketSlotCount(shipSkinIndex);
        if (safePocketCount <= 0)
            return false;

        int capacity = ShipCatalog.GetShipInventoryCapacity(shipSkinIndex);
        int safePocketEnd = Mathf.Min(capacity, safePocketCount);
        return slotIndex < safePocketEnd;
    }

    public static int GetAstronautCargoSlotCount(int shipSkinIndex)
    {
        return Mathf.Max(0, DefaultAstronautCargoSlotCount);
    }

    public static int GetPlayerAstronautCargoSlotCount(Photon.Realtime.Player player)
    {
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        return GetAstronautCargoSlotCount(shipSkinIndex);
    }

    public static bool IsAstronautCargoIndex(int shipSkinIndex, int shipCapacity, int slotIndex)
    {
        return IsAstronautCargoIndex(shipSkinIndex, shipCapacity, slotIndex, GetAstronautCargoSlotCount(shipSkinIndex));
    }

    public static bool IsAstronautCargoIndex(int shipSkinIndex, int shipCapacity, int slotIndex, int astronautCargoSlotCount)
    {
        if (slotIndex < 0 || slotIndex >= PlayerInventoryData.ShipSlotCount || astronautCargoSlotCount <= 0)
            return false;

        int clampedCapacity = Mathf.Clamp(shipCapacity, 0, PlayerInventoryData.ShipSlotCount);
        if (slotIndex >= clampedCapacity)
            return false;

        int astronautSlotsSeen = 0;
        for (int i = 0; i < clampedCapacity; i++)
        {
            if (IsSafePocketIndex(shipSkinIndex, i))
                continue;

            if (i == slotIndex)
                return astronautSlotsSeen < astronautCargoSlotCount;

            astronautSlotsSeen++;
            if (astronautSlotsSeen >= astronautCargoSlotCount)
                return false;
        }

        return false;
    }

    public static bool PlayerHasDropbotEquipped(Photon.Realtime.Player player)
    {
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        return InventoryItemCatalog.HasEquippedItem(GetPlayerEquipmentSlots(player), shipSkinIndex, InventoryItemCatalog.DropbotId);
    }

    public static int GetDropbotCargoSlotIndex(int shipSkinIndex, int shipCapacity, string[] equipmentSlots)
    {
        if (!InventoryItemCatalog.HasEquippedItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.DropbotId))
            return -1;

        int clampedCapacity = Mathf.Clamp(shipCapacity, 0, PlayerInventoryData.ShipSlotCount);
        for (int i = 0; i < clampedCapacity; i++)
        {
            if (IsSafePocketIndex(shipSkinIndex, i))
                continue;

            if (IsAstronautCargoIndex(shipSkinIndex, clampedCapacity, i))
                continue;

            return i;
        }

        return -1;
    }

    public static bool IsDropbotCargoIndex(int shipSkinIndex, int shipCapacity, int slotIndex, string[] equipmentSlots)
    {
        return slotIndex >= 0 && slotIndex == GetDropbotCargoSlotIndex(shipSkinIndex, shipCapacity, equipmentSlots);
    }

    public static bool CanStoreItemInShipSlot(string itemId, int shipSkinIndex, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return true;

        if (InventoryItemCatalog.RequiresSafePocket(itemId))
            return IsSafePocketIndex(shipSkinIndex, slotIndex) && InventoryItemCatalog.CanEnterSafePocket(itemId);

        return !IsSafePocketIndex(shipSkinIndex, slotIndex) || InventoryItemCatalog.CanEnterSafePocket(itemId);
    }

    public static bool PlayerHasFreeShipInventorySlot(Photon.Realtime.Player player, string itemId)
    {
        string[] slots = GetPlayerShipInventorySlots(player);
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        int capacity = GetPlayerShipInventoryCapacity(player);
        for (int i = 0; i < slots.Length && i < capacity; i++)
        {
            if (string.IsNullOrWhiteSpace(slots[i]) && CanStoreItemInShipSlot(itemId, shipSkinIndex, i))
                return true;
        }

        return false;
    }

    public static bool PlayerCanStoreShipItemOrAtlasAutoReplace(Photon.Realtime.Player player, string itemId)
    {
        if (PlayerHasFreeShipInventorySlot(player, itemId))
            return true;

        if (!PilotCatalog.IsSelectedPilot(player, PilotCatalog.AtlasId))
            return false;

        int newValue = InventoryItemCatalog.GetSellValueAstrons(itemId);
        if (newValue <= 0)
            return false;

        string[] slots = GetPlayerShipInventorySlots(player);
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        int capacity = GetPlayerShipInventoryCapacity(player);
        int leastValue = int.MaxValue;
        for (int i = 0; i < slots.Length && i < capacity; i++)
        {
            string currentItemId = slots[i];
            if (string.IsNullOrWhiteSpace(currentItemId))
                continue;

            if (!CanStoreItemInShipSlot(itemId, shipSkinIndex, i))
                continue;

            int currentValue = InventoryItemCatalog.GetSellValueAstrons(currentItemId);
            if (currentValue < leastValue)
                leastValue = currentValue;
        }

        return leastValue < int.MaxValue && newValue > leastValue;
    }

    public static string[] GetPlayerEquipmentSlots(Photon.Realtime.Player player)
    {
        if (player != null &&
            player.CustomProperties.TryGetValue(RoomSettings.EquipmentStateKey, out object value) &&
            value is string raw)
        {
            return DeserializeEquipmentSlots(raw);
        }

        return new string[PlayerInventoryData.EquipmentSlotCount];
    }

    string[] BuildRuntimeEquipmentSlotsForProfile(int shipSkinIndex, string[] sourceSlots)
    {
        string[] slots = NormalizeEquipmentSlots(sourceSlots);
        for (int i = 0; i < slots.Length; i++)
        {
            if (!IsEquipmentSlotEnabledForProfile(i, shipSkinIndex))
                slots[i] = null;
        }

        return slots;
    }

    static int GetFirstFreeEquipmentSlotForItem(string[] sourceSlots, int shipSkinIndex, string itemId)
    {
        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return -1;

        return GetFirstFreeEquipmentSlotByCategory(sourceSlots, shipSkinIndex, definition.Category);
    }

    int GetFirstFreeEquipmentSlotByCategoryForProfile(string[] sourceSlots, int shipSkinIndex, InventoryItemCategory category)
    {
        string[] slots = NormalizeEquipmentSlots(sourceSlots);
        for (int i = 0; i < slots.Length; i++)
        {
            if (InventoryItemCatalog.GetEquipmentSlotCategory(i) != category)
                continue;

            if (!IsEquipmentSlotEnabledForProfile(i, shipSkinIndex))
                continue;

            if (string.IsNullOrWhiteSpace(slots[i]))
                return i;
        }

        return -1;
    }

    static int GetFirstFreeEquipmentSlotByCategory(string[] sourceSlots, int shipSkinIndex, InventoryItemCategory category)
    {
        string[] slots = NormalizeEquipmentSlots(sourceSlots);
        for (int i = 0; i < slots.Length; i++)
        {
            if (InventoryItemCatalog.GetEquipmentSlotCategory(i) != category)
                continue;

            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            if (string.IsNullOrWhiteSpace(slots[i]))
                return i;
        }

        return -1;
    }
}
