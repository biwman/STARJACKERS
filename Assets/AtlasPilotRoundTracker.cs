using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public static class AtlasPilotRoundTracker
{
    public const float BoosterDrainMultiplier = 0.7f;
    public const float ExtractionActivationMultiplier = 0.7f;

    static string trackedRoundKey = string.Empty;
    static readonly Dictionary<string, int> startingItemCounts = new Dictionary<string, int>(System.StringComparer.Ordinal);
    static int startingCargoValueAstrons;
    static bool hasStartingSnapshot;

    public static void RecordRoundStart(PlayerProfileData profile)
    {
        string roundKey = BuildRoundKey();
        if (hasStartingSnapshot && string.Equals(trackedRoundKey, roundKey, System.StringComparison.Ordinal))
            return;

        trackedRoundKey = roundKey;
        CaptureStartingCargo(profile);
    }

    public static int GetNetCargoValueAstrons(PlayerProfileData profile)
    {
        EnsureRoundScope(profile);
        return Mathf.Max(0, GetCurrentCargoValueAstrons(profile) - startingCargoValueAstrons);
    }

    public static bool HasCargoRunBonus(PlayerProfileData profile)
    {
        return GetNetCargoValueAstrons(profile) >= PilotCatalog.AtlasPassiveNetCargoAstrons;
    }

    public static bool CanReplaceLeastValuableRoundCargo(
        PlayerProfileData profile,
        string newItemId,
        out int slotIndex,
        out string replacedItemId)
    {
        EnsureRoundScope(profile);
        slotIndex = -1;
        replacedItemId = null;

        if (profile == null || profile.Inventory == null || string.IsNullOrWhiteSpace(newItemId))
            return false;

        int newValue = InventoryItemCatalog.GetSellValueAstrons(newItemId);
        if (newValue <= 0)
            return false;

        PlayerInventoryData inventory = profile.Inventory;
        inventory.Normalize();
        int shipSkinIndex = Mathf.Clamp(profile.ShipSkinIndex, 0, ShipCatalog.MaxShipSkinIndex);
        int capacity = Mathf.Clamp(
            PlayerProfileService.GetEffectiveShipInventoryCapacity(shipSkinIndex, inventory.EquipmentSlots),
            0,
            inventory.ShipSlots.Length);

        Dictionary<string, int> seenCounts = new Dictionary<string, int>(System.StringComparer.Ordinal);
        int leastValue = int.MaxValue;
        for (int i = 0; i < capacity; i++)
        {
            string itemId = inventory.ShipSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            int seen = 0;
            seenCounts.TryGetValue(itemId, out seen);
            seen++;
            seenCounts[itemId] = seen;

            int startingCount = 0;
            startingItemCounts.TryGetValue(itemId, out startingCount);
            if (seen <= startingCount)
                continue;

            if (!PlayerProfileService.CanStoreItemInShipSlot(newItemId, shipSkinIndex, i))
                continue;

            int itemValue = InventoryItemCatalog.GetSellValueAstrons(itemId);
            if (itemValue >= leastValue)
                continue;

            leastValue = itemValue;
            slotIndex = i;
            replacedItemId = itemId;
        }

        return slotIndex >= 0 && newValue > leastValue;
    }

    static void EnsureRoundScope(PlayerProfileData profile)
    {
        string roundKey = BuildRoundKey();
        if (hasStartingSnapshot && string.Equals(trackedRoundKey, roundKey, System.StringComparison.Ordinal))
            return;

        trackedRoundKey = roundKey;
        CaptureStartingCargo(profile);
    }

    static void CaptureStartingCargo(PlayerProfileData profile)
    {
        startingItemCounts.Clear();
        startingCargoValueAstrons = 0;
        hasStartingSnapshot = true;

        if (profile == null || profile.Inventory == null)
            return;

        PlayerInventoryData inventory = profile.Inventory;
        inventory.Normalize();
        int shipSkinIndex = Mathf.Clamp(profile.ShipSkinIndex, 0, ShipCatalog.MaxShipSkinIndex);
        int capacity = Mathf.Clamp(
            PlayerProfileService.GetEffectiveShipInventoryCapacity(shipSkinIndex, inventory.EquipmentSlots),
            0,
            inventory.ShipSlots.Length);

        for (int i = 0; i < capacity; i++)
        {
            string itemId = inventory.ShipSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!startingItemCounts.ContainsKey(itemId))
                startingItemCounts[itemId] = 0;

            startingItemCounts[itemId]++;
            startingCargoValueAstrons += InventoryItemCatalog.GetSellValueAstrons(itemId);
        }
    }

    static int GetCurrentCargoValueAstrons(PlayerProfileData profile)
    {
        if (profile == null || profile.Inventory == null)
            return 0;

        PlayerInventoryData inventory = profile.Inventory;
        inventory.Normalize();
        int shipSkinIndex = Mathf.Clamp(profile.ShipSkinIndex, 0, ShipCatalog.MaxShipSkinIndex);
        int capacity = Mathf.Clamp(
            PlayerProfileService.GetEffectiveShipInventoryCapacity(shipSkinIndex, inventory.EquipmentSlots),
            0,
            inventory.ShipSlots.Length);

        int total = 0;
        for (int i = 0; i < capacity; i++)
        {
            string itemId = inventory.ShipSlots[i];
            if (!string.IsNullOrWhiteSpace(itemId))
                total += InventoryItemCatalog.GetSellValueAstrons(itemId);
        }

        return Mathf.Max(0, total);
    }

    static string BuildRoundKey()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return "offline";

        string roomName = PhotonNetwork.CurrentRoom.Name ?? string.Empty;
        string startTime = "nostart";
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object value) && value != null)
            startTime = value.ToString();

        return roomName + "_" + startTime;
    }
}
