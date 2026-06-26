using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService
{
    public const int ViperNeutralFighterWrecksRequired = 8;
    public const int ViperDroneWrecksRequired = 4;
    public const int ViperSpaceTruckWrecksRequired = 2;
    public const float ViperMinimumTestFlightSeconds = 60f;
    public const int ViperTestFlightSubsystemUnlocksPerReturn = 2;
    public const string ViperCargoSubsystemId = "cargo";

    public ViperRecoveryStage GetViperRecoveryStage()
    {
        ViperRecoveryProgressData progress = CurrentProfile != null
            ? NormalizeViperRecoveryProgress(CurrentProfile.ViperRecoveryProgress, CurrentProfile.UnlockedShipIds)
            : ViperRecoveryProgressData.Empty();
        return (ViperRecoveryStage)Mathf.Clamp(progress.Stage, (int)ViperRecoveryStage.Locked, (int)ViperRecoveryStage.Complete);
    }

    public ViperRecoveryProgressData GetViperRecoveryProgress()
    {
        ViperRecoveryProgressData progress = CurrentProfile != null
            ? NormalizeViperRecoveryProgress(CurrentProfile.ViperRecoveryProgress, CurrentProfile.UnlockedShipIds)
            : ViperRecoveryProgressData.Empty();
        return progress.Clone();
    }

    public bool IsViperRecoveryComplete()
    {
        return GetViperRecoveryStage() == ViperRecoveryStage.Complete;
    }

    public bool IsViperRepairPartsDonationAvailable()
    {
        EnsureInventory();
        return GetViperRecoveryStage() == ViperRecoveryStage.WreckRecovered &&
               CountViperRepairPartItem(InventoryItemCatalog.NeutralFighterSalvageId) >= ViperNeutralFighterWrecksRequired &&
               CountViperRepairPartItem(InventoryItemCatalog.DroidScrapId) >= ViperDroneWrecksRequired &&
               CountViperRepairPartItem(InventoryItemCatalog.SpaceTruckWreckId) >= ViperSpaceTruckWrecksRequired;
    }

    public int CountViperRepairPartItem(string itemId)
    {
        EnsureInventory();
        return CountItemInSlots(CurrentProfile.Inventory.PlayerSlots, CurrentProfile.Inventory.PlayerSlots.Length, itemId) +
               CountItemInSlots(CurrentProfile.Inventory.ShipSlots, GetActiveShipInventoryCapacity(), itemId);
    }

    public async Task<bool> RecordViperWreckRecoveredAsync()
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        if (GetViperRecoveryStage() != ViperRecoveryStage.Locked)
            return false;

        CurrentProfile.ViperRecoveryProgress = new ViperRecoveryProgressData
        {
            Stage = (int)ViperRecoveryStage.WreckRecovered,
            UnlockedSubsystemIds = Array.Empty<string>()
        };
        await SaveViperRecoveryProgressAsync("save Viper wreck recovery");
        return true;
    }

    public async Task<bool> DonateViperRepairPartsAsync()
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureShipUnlocks();

        if (GetViperRecoveryStage() != ViperRecoveryStage.WreckRecovered)
            return false;

        string[] costItemIds = BuildViperRepairCostItemIds();
        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        if (!RemoveRequiredItems(workingInventory, costItemIds))
            return false;

        HashSet<string> ids = new HashSet<string>(CurrentProfile.UnlockedShipIds, StringComparer.Ordinal)
        {
            ShipCatalog.GetShipTypeId(ShipType.Viper)
        };
        string[] unlockedIds = new string[ids.Count];
        ids.CopyTo(unlockedIds);

        CurrentProfile.Inventory = workingInventory;
        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIds(unlockedIds);
        CurrentProfile.ViperRecoveryProgress = new ViperRecoveryProgressData
        {
            Stage = (int)ViperRecoveryStage.Testing,
            UnlockedSubsystemIds = Array.Empty<string>()
        };

        await SaveViperRepairDonationAsync();
        return true;
    }

    public async Task<ViperTestFlightResult> RecordViperTestFlightReturnAsync(float elapsedSeconds)
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        if (ShipCatalog.GetShipTypeFromSkinIndex(GetActiveShipSkinIndex()) != ShipType.Viper ||
            GetViperRecoveryStage() != ViperRecoveryStage.Testing)
        {
            return ViperTestFlightResult.NotApplicable;
        }

        if (elapsedSeconds < ViperMinimumTestFlightSeconds)
            return ViperTestFlightResult.TooShort;

        string[] lockedSubsystems = GetLockedViperSubsystemIds(CurrentProfile.ViperRecoveryProgress);
        if (lockedSubsystems.Length == 0)
        {
            CurrentProfile.ViperRecoveryProgress = ViperRecoveryProgressData.Complete();
            await SaveViperRecoveryProgressAsync("save Viper test completion");
            return ViperTestFlightResult.Complete;
        }

        HashSet<string> unlocked = new HashSet<string>(
            NormalizeViperSubsystemIds(CurrentProfile.ViperRecoveryProgress.UnlockedSubsystemIds),
            StringComparer.Ordinal);
        List<string> candidates = new List<string>(lockedSubsystems);
        int unlockCount = Mathf.Min(ViperTestFlightSubsystemUnlocksPerReturn, candidates.Count);
        for (int i = 0; i < unlockCount; i++)
        {
            int index = UnityEngine.Random.Range(0, candidates.Count);
            unlocked.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        string[] unlockedArray = new string[unlocked.Count];
        unlocked.CopyTo(unlockedArray);

        CurrentProfile.ViperRecoveryProgress = new ViperRecoveryProgressData
        {
            Stage = (int)ViperRecoveryStage.Testing,
            UnlockedSubsystemIds = NormalizeViperSubsystemIds(unlockedArray)
        };

        bool completed = GetLockedViperSubsystemIds(CurrentProfile.ViperRecoveryProgress).Length == 0;
        if (completed)
            CurrentProfile.ViperRecoveryProgress = ViperRecoveryProgressData.Complete();

        await SaveViperRecoveryProgressAsync("save Viper test flight");
        return completed ? ViperTestFlightResult.Complete : ViperTestFlightResult.SubsystemUnlocked;
    }

    async Task SaveViperRecoveryProgressAsync(string operationName)
    {
        try
        {
            IsBusy = true;
            EnsureShipUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudUnlockedShipsKey] = SerializeShipUnlocks(CurrentProfile.UnlockedShipIds),
                [CloudViperRecoveryProgressKey] = SerializeViperRecoveryProgress(CurrentProfile.ViperRecoveryProgress),
                [CloudInvaderImprintsRecoveredKey] = CurrentProfile.InvaderImprintsRecovered
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                operationName);
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Viper recovery save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveViperRepairDonationAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            EnsureShipUnlocks();
            EnsureMissEnigmaUniqueItemRecoveries();

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudUnlockedShipsKey] = SerializeShipUnlocks(CurrentProfile.UnlockedShipIds),
                [CloudViperRecoveryProgressKey] = SerializeViperRecoveryProgress(CurrentProfile.ViperRecoveryProgress),
                [CloudInvaderImprintsRecoveredKey] = CurrentProfile.InvaderImprintsRecovered,
                [CloudMissEnigmaRecoverableUniqueItemsKey] = SerializeMissEnigmaUniqueItemRecoveries(CurrentProfile.MissEnigmaRecoverableUniqueItemIds)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save Viper repair donation");
            InventoryRevision++;
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Viper repair donation save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public static bool PlayerNeedsViperRecovery(Photon.Realtime.Player player)
    {
        if (player == null)
            return false;

        if (player.CustomProperties != null &&
            player.CustomProperties.TryGetValue(PlayerViperRecoveryStageKey, out object value))
        {
            return ConvertPlayerPropertyToInt(value, (int)ViperRecoveryStage.Complete) == (int)ViperRecoveryStage.Locked;
        }

        return player == PhotonNetwork.LocalPlayer &&
               HasInstance &&
               Instance.IsInitialized &&
               Instance.GetViperRecoveryStage() == ViperRecoveryStage.Locked;
    }

    public static ViperRecoveryProgressData NormalizeViperRecoveryProgress(ViperRecoveryProgressData progress, string[] shipIds = null)
    {
        bool shipUnlocked = ContainsShipTypeId(shipIds, ShipType.Viper);
        if (progress == null)
            return shipUnlocked ? ViperRecoveryProgressData.Complete() : ViperRecoveryProgressData.Empty();

        ViperRecoveryStage stage = (ViperRecoveryStage)Mathf.Clamp(progress.Stage, (int)ViperRecoveryStage.Locked, (int)ViperRecoveryStage.Complete);
        if (stage == ViperRecoveryStage.Locked && shipUnlocked)
            stage = ViperRecoveryStage.Complete;

        if (stage == ViperRecoveryStage.Complete)
            return ViperRecoveryProgressData.Complete();

        if (stage == ViperRecoveryStage.Locked || stage == ViperRecoveryStage.WreckRecovered)
        {
            return new ViperRecoveryProgressData
            {
                Stage = (int)stage,
                UnlockedSubsystemIds = Array.Empty<string>()
            };
        }

        string[] unlockedSubsystemIds = NormalizeViperSubsystemIds(progress.UnlockedSubsystemIds);
        ViperRecoveryProgressData normalized = new ViperRecoveryProgressData
        {
            Stage = (int)ViperRecoveryStage.Testing,
            UnlockedSubsystemIds = unlockedSubsystemIds
        };

        return GetLockedViperSubsystemIds(normalized).Length == 0
            ? ViperRecoveryProgressData.Complete()
            : normalized;
    }

    static string[] NormalizeUnlockedShipIdsForViperProgress(string[] shipIds, ViperRecoveryProgressData progress)
    {
        HashSet<string> normalized = new HashSet<string>(NormalizeUnlockedShipIds(shipIds), StringComparer.Ordinal);
        string viperId = ShipCatalog.GetShipTypeId(ShipType.Viper);
        ViperRecoveryStage stage = progress != null
            ? (ViperRecoveryStage)Mathf.Clamp(progress.Stage, (int)ViperRecoveryStage.Locked, (int)ViperRecoveryStage.Complete)
            : ViperRecoveryStage.Locked;

        if (stage == ViperRecoveryStage.Testing || stage == ViperRecoveryStage.Complete)
            normalized.Add(viperId);
        else
            normalized.Remove(viperId);

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    public bool IsEquipmentSlotEnabledForProfile(int slotIndex, int shipSkinIndex)
    {
        if (!ShipCatalog.IsEquipmentSlotEnabled(slotIndex, shipSkinIndex))
            return false;

        if (ShipCatalog.GetShipTypeFromSkinIndex(shipSkinIndex) != ShipType.Viper)
            return true;

        ViperRecoveryStage stage = GetViperRecoveryStage();
        if (stage == ViperRecoveryStage.Complete)
            return true;

        if (slotIndex == 0)
            return stage == ViperRecoveryStage.Testing;

        if (stage != ViperRecoveryStage.Testing)
            return false;

        return IsViperSubsystemUnlocked(GetViperEquipmentSubsystemId(slotIndex));
    }

    public bool IsCargoUnlockedForProfile(int shipSkinIndex)
    {
        if (ShipCatalog.GetShipTypeFromSkinIndex(shipSkinIndex) != ShipType.Viper)
            return true;

        ViperRecoveryStage stage = GetViperRecoveryStage();
        return stage == ViperRecoveryStage.Complete ||
               (stage == ViperRecoveryStage.Testing && IsViperSubsystemUnlocked(ViperCargoSubsystemId));
    }

    public bool IsViperSubsystemUnlocked(string subsystemId)
    {
        if (string.IsNullOrWhiteSpace(subsystemId))
            return false;

        ViperRecoveryProgressData progress = CurrentProfile != null
            ? NormalizeViperRecoveryProgress(CurrentProfile.ViperRecoveryProgress, CurrentProfile.UnlockedShipIds)
            : ViperRecoveryProgressData.Empty();
        ViperRecoveryStage stage = (ViperRecoveryStage)Mathf.Clamp(progress.Stage, (int)ViperRecoveryStage.Locked, (int)ViperRecoveryStage.Complete);
        if (stage == ViperRecoveryStage.Complete)
            return IsValidViperSubsystemId(subsystemId);

        if (stage != ViperRecoveryStage.Testing)
            return false;

        string normalizedId = NormalizeViperSubsystemId(subsystemId);
        if (string.IsNullOrWhiteSpace(normalizedId))
            return false;

        if (string.Equals(normalizedId, GetViperEquipmentSubsystemId(0), StringComparison.Ordinal))
            return true;

        string[] unlocked = NormalizeViperSubsystemIds(progress.UnlockedSubsystemIds);
        for (int i = 0; i < unlocked.Length; i++)
        {
            if (string.Equals(unlocked[i], normalizedId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static string GetViperEquipmentSubsystemId(int slotIndex)
    {
        return "equipment_" + Mathf.Clamp(slotIndex, 0, PlayerInventoryData.EquipmentSlotCount - 1);
    }

    public static string[] GetAllViperTestSubsystemIds()
    {
        List<string> ids = new List<string>();
        for (int i = 1; i < PlayerInventoryData.EquipmentSlotCount; i++)
        {
            if (ShipCatalog.IsEquipmentSlotEnabled(i, ShipCatalog.ViperStandardSkinIndex))
                ids.Add(GetViperEquipmentSubsystemId(i));
        }

        ids.Add(ViperCargoSubsystemId);
        return ids.ToArray();
    }

    static string[] GetLockedViperSubsystemIds(ViperRecoveryProgressData progress)
    {
        HashSet<string> unlocked = new HashSet<string>(
            NormalizeViperSubsystemIds(progress != null ? progress.UnlockedSubsystemIds : null),
            StringComparer.Ordinal);
        string[] all = GetAllViperTestSubsystemIds();
        List<string> locked = new List<string>();
        for (int i = 0; i < all.Length; i++)
        {
            if (!unlocked.Contains(all[i]))
                locked.Add(all[i]);
        }

        return locked.ToArray();
    }

    static string[] NormalizeViperSubsystemIds(string[] subsystemIds)
    {
        if (subsystemIds == null || subsystemIds.Length == 0)
            return Array.Empty<string>();

        HashSet<string> normalized = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < subsystemIds.Length; i++)
        {
            string subsystemId = NormalizeViperSubsystemId(subsystemIds[i]);
            if (!string.IsNullOrWhiteSpace(subsystemId))
                normalized.Add(subsystemId);
        }

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    static string NormalizeViperSubsystemId(string subsystemId)
    {
        if (string.IsNullOrWhiteSpace(subsystemId))
            return string.Empty;

        string normalized = subsystemId.Trim().ToLowerInvariant();
        return IsValidViperSubsystemId(normalized) ? normalized : string.Empty;
    }

    static bool IsValidViperSubsystemId(string subsystemId)
    {
        if (string.Equals(subsystemId, ViperCargoSubsystemId, StringComparison.Ordinal))
            return true;

        for (int i = 0; i < PlayerInventoryData.EquipmentSlotCount; i++)
        {
            if (string.Equals(subsystemId, GetViperEquipmentSubsystemId(i), StringComparison.Ordinal) &&
                ShipCatalog.IsEquipmentSlotEnabled(i, ShipCatalog.ViperStandardSkinIndex))
            {
                return true;
            }
        }

        return false;
    }

    static string[] BuildViperRepairCostItemIds()
    {
        List<string> items = new List<string>();
        AddRepeated(items, InventoryItemCatalog.NeutralFighterSalvageId, ViperNeutralFighterWrecksRequired);
        AddRepeated(items, InventoryItemCatalog.DroidScrapId, ViperDroneWrecksRequired);
        AddRepeated(items, InventoryItemCatalog.SpaceTruckWreckId, ViperSpaceTruckWrecksRequired);
        return items.ToArray();
    }

    static void AddRepeated(List<string> items, string itemId, int count)
    {
        for (int i = 0; i < count; i++)
            items.Add(itemId);
    }
}
