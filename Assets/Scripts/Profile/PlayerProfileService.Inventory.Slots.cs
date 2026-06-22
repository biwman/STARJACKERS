using System;

public partial class PlayerProfileService
{
    static string[] NormalizeShipSlots(string[] source)
    {
        string[] normalized = new string[PlayerInventoryData.ShipSlotCount];
        if (source == null)
            return normalized;

        int count = Math.Min(source.Length, normalized.Length);
        for (int i = 0; i < count; i++)
        {
            normalized[i] = source[i];
        }

        return normalized;
    }

    static bool HasAnyShipSlotItem(string[] slots)
    {
        if (slots == null)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(slots[i]))
                return true;
        }

        return false;
    }

    static string[] NormalizeEquipmentSlots(string[] source)
    {
        string[] normalized = new string[PlayerInventoryData.EquipmentSlotCount];
        if (source == null)
            return normalized;

        int count = Math.Min(source.Length, normalized.Length);
        for (int i = 0; i < count; i++)
            normalized[i] = source[i];

        return normalized;
    }
}
