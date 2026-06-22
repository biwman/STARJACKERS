using System;
using UnityEngine;

public partial class PlayerProfileService
{
    public static string SerializeShipInventorySlots(string[] slots)
    {
        ShipInventorySnapshot snapshot = new ShipInventorySnapshot
        {
            slots = NormalizeShipSlots(slots)
        };
        return JsonUtility.ToJson(snapshot);
    }

    public static string[] DeserializeShipInventorySlots(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new string[PlayerInventoryData.ShipSlotCount];

        try
        {
            ShipInventorySnapshot snapshot = JsonUtility.FromJson<ShipInventorySnapshot>(raw);
            return NormalizeShipSlots(snapshot != null ? snapshot.slots : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize ship inventory snapshot: " + ex.Message);
            return new string[PlayerInventoryData.ShipSlotCount];
        }
    }

    public static string SerializeEquipmentSlots(string[] slots)
    {
        EquipmentSnapshot snapshot = new EquipmentSnapshot
        {
            slots = NormalizeEquipmentSlots(slots)
        };
        return JsonUtility.ToJson(snapshot);
    }

    public static string[] DeserializeEquipmentSlots(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new string[PlayerInventoryData.EquipmentSlotCount];

        try
        {
            EquipmentSnapshot snapshot = JsonUtility.FromJson<EquipmentSnapshot>(raw);
            return NormalizeEquipmentSlots(snapshot != null ? snapshot.slots : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize equipment snapshot: " + ex.Message);
            return new string[PlayerInventoryData.EquipmentSlotCount];
        }
    }
}
