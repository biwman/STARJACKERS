using System;
using System.Collections.Generic;
using UnityEngine;

public partial class PlayerProfileService
{
    Dictionary<string, int> BuildItemCounts(PlayerInventoryData inventory)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (inventory == null)
            return counts;

        inventory.Normalize();
        CountItems(inventory.PlayerSlots, inventory.PlayerSlots.Length, counts);
        CountItems(inventory.ShipSlots, GetActiveShipInventoryCapacity(), counts);
        return counts;
    }

    void CountItems(string[] slots, int limit, Dictionary<string, int> counts)
    {
        if (slots == null || counts == null)
            return;

        int safeLimit = Mathf.Clamp(limit, 0, slots.Length);
        for (int i = 0; i < safeLimit; i++)
        {
            string itemId = slots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            counts.TryGetValue(itemId, out int currentCount);
            counts[itemId] = currentCount + 1;

            string requirementKey = InventoryItemCatalog.GetRequirementCountKey(itemId);
            if (!string.IsNullOrWhiteSpace(requirementKey) &&
                !string.Equals(requirementKey, itemId, StringComparison.Ordinal))
            {
                counts.TryGetValue(requirementKey, out int currentRequirementCount);
                counts[requirementKey] = currentRequirementCount + 1;
            }
        }
    }

    static int CountItemInSlots(string[] slots, int limit, string matchItemId)
    {
        if (slots == null || string.IsNullOrWhiteSpace(matchItemId))
            return 0;

        int safeLimit = Mathf.Clamp(limit, 0, slots.Length);
        int count = 0;
        for (int i = 0; i < safeLimit; i++)
        {
            if (InventoryItemCatalog.MatchesItemRequirement(slots[i], matchItemId))
                count++;
        }

        return count;
    }

    static int FindItemInSlots(string[] slots, int limit, string matchItemId)
    {
        if (slots == null || string.IsNullOrWhiteSpace(matchItemId))
            return -1;

        int safeLimit = Mathf.Clamp(limit, 0, slots.Length);
        for (int i = 0; i < safeLimit; i++)
        {
            if (InventoryItemCatalog.MatchesItemRequirement(slots[i], matchItemId))
                return i;
        }

        return -1;
    }

    static int AddItemValueClamped(int currentValue, string itemId)
    {
        long combined = (long)Mathf.Max(0, currentValue) + InventoryItemCatalog.GetSellValueAstrons(itemId);
        return combined > int.MaxValue ? int.MaxValue : (int)combined;
    }

    bool HasRequiredItems(Dictionary<string, int> counts, string[] costItemIds)
    {
        if (costItemIds == null || costItemIds.Length == 0)
            return true;

        Dictionary<string, int> remaining = counts != null
            ? new Dictionary<string, int>(counts, StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < costItemIds.Length; i++)
        {
            string itemId = costItemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!remaining.TryGetValue(itemId, out int currentCount) || currentCount <= 0)
                return false;

            remaining[itemId] = currentCount - 1;
        }

        return true;
    }

    bool RemoveRequiredItems(PlayerInventoryData inventory, string[] costItemIds)
    {
        if (inventory == null)
            return false;

        inventory.Normalize();
        if (!HasRequiredItems(BuildItemCounts(inventory), costItemIds))
            return false;

        for (int i = 0; i < costItemIds.Length; i++)
        {
            string itemId = costItemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (RemoveFirstMatchingItem(inventory.PlayerSlots, inventory.PlayerSlots.Length, itemId))
                continue;

            if (RemoveFirstMatchingItem(inventory.ShipSlots, GetActiveShipInventoryCapacity(), itemId))
                continue;

            return false;
        }

        return true;
    }

    bool RemoveFirstMatchingItem(string[] slots, int limit, string itemId)
    {
        if (slots == null || string.IsNullOrWhiteSpace(itemId))
            return false;

        int safeLimit = Mathf.Clamp(limit, 0, slots.Length);
        for (int i = 0; i < safeLimit; i++)
        {
            if (!InventoryItemCatalog.MatchesItemRequirement(slots[i], itemId))
                continue;

            slots[i] = null;
            return true;
        }

        return false;
    }
}
