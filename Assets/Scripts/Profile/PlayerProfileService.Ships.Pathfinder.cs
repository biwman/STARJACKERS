using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService
{
    public const int PathfinderHackedShipTypesRequired = 7;
    public const int PathfinderValuableItemsRequired = 5;

    public PathfinderResearchStage GetPathfinderResearchStage()
    {
        EnsureShipUnlocks();
        PathfinderResearchProgressData progress = CurrentProfile != null
            ? CurrentProfile.PathfinderResearchProgress
            : PathfinderResearchProgressData.Empty();
        return (PathfinderResearchStage)Mathf.Clamp(progress != null ? progress.Stage : 0, (int)PathfinderResearchStage.Locked, (int)PathfinderResearchStage.Complete);
    }

    public PathfinderResearchProgressData GetPathfinderResearchProgress()
    {
        EnsureShipUnlocks();
        PathfinderResearchProgressData progress = CurrentProfile != null
            ? NormalizePathfinderResearchProgress(CurrentProfile.PathfinderResearchProgress, CurrentProfile.UnlockedShipIds)
            : PathfinderResearchProgressData.Empty();
        return progress.Clone();
    }

    public int GetPathfinderHackedShipTypeCount()
    {
        PathfinderResearchProgressData progress = GetPathfinderResearchProgress();
        return progress.HackedShipTypeIds != null ? Mathf.Clamp(progress.HackedShipTypeIds.Length, 0, PathfinderHackedShipTypesRequired) : 0;
    }

    public bool ShouldShowPathfinderHackOpportunity()
    {
        EnsureInventory();
        EnsureShipUnlocks();

        if (IsShipUnlocked(ShipType.Pathfinder) || GetPathfinderResearchStage() != PathfinderResearchStage.Locked)
            return false;

        int shipSkinIndex = GetActiveShipSkinIndex();
        return InventoryItemCatalog.HasEquippedItem(CurrentProfile.Inventory.EquipmentSlots, shipSkinIndex, InventoryItemCatalog.HackingDeviceId);
    }

    public async Task<PathfinderHackRecordResult> RecordPathfinderHackedShipTypeAsync(string rawShipTypeId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureShipUnlocks();

        PathfinderHackRecordResult result = new PathfinderHackRecordResult();
        PathfinderResearchProgressData progress = NormalizePathfinderResearchProgress(CurrentProfile.PathfinderResearchProgress, CurrentProfile.UnlockedShipIds);
        result.Count = progress.HackedShipTypeIds != null ? progress.HackedShipTypeIds.Length : 0;

        if (IsShipUnlocked(ShipType.Pathfinder))
            return result;

        string shipTypeId = NormalizePathfinderHackedShipTypeId(rawShipTypeId);
        if (string.IsNullOrWhiteSpace(shipTypeId))
            return result;

        PathfinderResearchStage stage = (PathfinderResearchStage)progress.Stage;
        if (stage > PathfinderResearchStage.DocumentationReady)
            return result;

        if (stage == PathfinderResearchStage.DocumentationReady)
        {
            bool documentationPresent = TryEnsurePathfinderDocumentationItem();
            result.Count = PathfinderHackedShipTypesRequired;
            result.DocumentationReady = true;
            result.DocumentationCreated = documentationPresent;
            result.InventoryFull = !documentationPresent;

            if (documentationPresent)
                await SaveInventoryAndPathfinderResearchProgressAsync("save Pathfinder documentation item");
            return result;
        }

        HashSet<string> hackedTypes = new HashSet<string>(
            NormalizePathfinderHackedShipTypeIds(progress.HackedShipTypeIds),
            StringComparer.Ordinal);
        if (!hackedTypes.Add(shipTypeId))
        {
            result.Duplicate = true;
            result.Count = hackedTypes.Count;
            return result;
        }

        string[] hackedArray = new string[hackedTypes.Count];
        hackedTypes.CopyTo(hackedArray);
        Array.Sort(hackedArray, StringComparer.Ordinal);

        bool started = stage == PathfinderResearchStage.Locked && progress.HackedShipTypeIds.Length == 0;
        bool complete = hackedArray.Length >= PathfinderHackedShipTypesRequired;
        CurrentProfile.PathfinderResearchProgress = new PathfinderResearchProgressData
        {
            Stage = complete ? (int)PathfinderResearchStage.DocumentationReady : (int)PathfinderResearchStage.CollectingData,
            HackedShipTypeIds = ClampPathfinderHackedShipTypeIds(hackedArray),
            DeliveredValuableItems = 0,
            ResourceCompletionMapId = string.Empty
        };

        bool documentationCreated = false;
        bool inventoryFull = false;
        if (complete)
        {
            documentationCreated = TryEnsurePathfinderDocumentationItem();
            inventoryFull = !documentationCreated;
        }

        result.Started = started;
        result.Added = true;
        result.Count = Mathf.Clamp(hackedArray.Length, 0, PathfinderHackedShipTypesRequired);
        result.DocumentationReady = complete;
        result.DocumentationCreated = documentationCreated;
        result.InventoryFull = inventoryFull;

        if (documentationCreated)
            await SaveInventoryAndPathfinderResearchProgressAsync("save Pathfinder hack completion");
        else
            await SavePathfinderResearchProgressAsync("save Pathfinder hack progress");

        return result;
    }

    public async Task ResetPathfinderDocumentationLostAsync()
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        if (IsShipUnlocked(ShipType.Pathfinder))
            return;

        PathfinderResearchProgressData progress = NormalizePathfinderResearchProgress(CurrentProfile.PathfinderResearchProgress, CurrentProfile.UnlockedShipIds);
        PathfinderResearchStage stage = (PathfinderResearchStage)progress.Stage;
        if (stage < PathfinderResearchStage.DocumentationReady || stage >= PathfinderResearchStage.Complete)
            return;

        CurrentProfile.PathfinderResearchProgress = PathfinderResearchProgressData.Empty();
        await SavePathfinderResearchProgressAsync("reset Pathfinder documentation loss");
    }

    public async Task<PathfinderResearchStationResult> ProcessPathfinderResearchStationAsync(string mapId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureShipUnlocks();

        PathfinderResearchStationResult result = new PathfinderResearchStationResult();
        if (IsShipUnlocked(ShipType.Pathfinder))
            return result;

        PathfinderResearchProgressData progress = NormalizePathfinderResearchProgress(CurrentProfile.PathfinderResearchProgress, CurrentProfile.UnlockedShipIds);
        PathfinderResearchStage stage = (PathfinderResearchStage)progress.Stage;
        string normalizedMapId = LobbyMapUnlockCatalog.NormalizeMapId(mapId);

        if (stage == PathfinderResearchStage.DocumentationReady)
        {
            if (!RemoveFirstShipItemLocal(InventoryItemCatalog.ShipPrototypeDocumentationId))
                return result;

            CurrentProfile.PathfinderResearchProgress = new PathfinderResearchProgressData
            {
                Stage = (int)PathfinderResearchStage.ResourcesRequired,
                HackedShipTypeIds = ClampPathfinderHackedShipTypeIds(progress.HackedShipTypeIds),
                DeliveredValuableItems = 0,
                ResourceCompletionMapId = string.Empty
            };

            result.Status = PathfinderResearchStationStatus.DocumentationDelivered;
            await SaveInventoryAndPathfinderResearchProgressAsync("deliver Pathfinder documentation");
            return result;
        }

        if (stage == PathfinderResearchStage.ResourcesRequired)
        {
            int needed = Mathf.Clamp(PathfinderValuableItemsRequired - progress.DeliveredValuableItems, 0, PathfinderValuableItemsRequired);
            if (needed <= 0)
                return result;

            int deliveredNow = RemovePathfinderValuableResearchItemsLocal(needed);
            if (deliveredNow <= 0)
                return result;

            int deliveredTotal = Mathf.Clamp(progress.DeliveredValuableItems + deliveredNow, 0, PathfinderValuableItemsRequired);
            bool completedResources = deliveredTotal >= PathfinderValuableItemsRequired;
            CurrentProfile.PathfinderResearchProgress = new PathfinderResearchProgressData
            {
                Stage = completedResources ? (int)PathfinderResearchStage.FinalVisitRequired : (int)PathfinderResearchStage.ResourcesRequired,
                HackedShipTypeIds = ClampPathfinderHackedShipTypeIds(progress.HackedShipTypeIds),
                DeliveredValuableItems = deliveredTotal,
                ResourceCompletionMapId = completedResources ? normalizedMapId : string.Empty
            };

            result.Status = PathfinderResearchStationStatus.ValuableItemsDelivered;
            result.DeliveredValuableItemsNow = deliveredNow;
            result.DeliveredValuableItemsTotal = deliveredTotal;
            result.ResourcesCompleted = completedResources;
            await SaveInventoryAndPathfinderResearchProgressAsync("deliver Pathfinder research resources");
            return result;
        }

        if (stage == PathfinderResearchStage.FinalVisitRequired)
        {
            if (string.IsNullOrWhiteSpace(normalizedMapId) ||
                string.Equals(normalizedMapId, progress.ResourceCompletionMapId, StringComparison.Ordinal))
            {
                result.Status = PathfinderResearchStationStatus.DifferentMapRequired;
                return result;
            }

            CurrentProfile.PathfinderResearchProgress = PathfinderResearchProgressData.Complete();
            HashSet<string> ids = new HashSet<string>(CurrentProfile.UnlockedShipIds, StringComparer.Ordinal)
            {
                ShipCatalog.GetShipTypeId(ShipType.Pathfinder)
            };
            string[] unlockedIds = new string[ids.Count];
            ids.CopyTo(unlockedIds);
            CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIds(unlockedIds);

            result.Status = PathfinderResearchStationStatus.Finalized;
            await SavePathfinderResearchProgressAsync("finalize Pathfinder research");
            return result;
        }

        return result;
    }

    bool TryEnsurePathfinderDocumentationItem()
    {
        EnsureInventory();
        if (CountItemInSlots(CurrentProfile.Inventory.ShipSlots, GetActiveShipInventoryCapacity(), InventoryItemCatalog.ShipPrototypeDocumentationId) > 0)
            return true;

        int shipSkinIndex = GetActiveShipSkinIndex();
        return CurrentProfile.Inventory.TryAddToShip(InventoryItemCatalog.ShipPrototypeDocumentationId, GetActiveShipInventoryCapacity(), shipSkinIndex);
    }

    bool RemoveFirstShipItemLocal(string matchItemId)
    {
        if (string.IsNullOrWhiteSpace(matchItemId))
            return false;

        EnsureInventory();
        int capacity = GetActiveShipInventoryCapacity();
        CurrentProfile.Inventory.Normalize();
        for (int i = 0; i < CurrentProfile.Inventory.ShipSlots.Length && i < capacity; i++)
        {
            if (!string.Equals(CurrentProfile.Inventory.ShipSlots[i], matchItemId, StringComparison.Ordinal))
                continue;

            CurrentProfile.Inventory.ShipSlots[i] = null;
            return true;
        }

        return false;
    }

    int RemovePathfinderValuableResearchItemsLocal(int maxCount)
    {
        int targetCount = Mathf.Clamp(maxCount, 0, PathfinderValuableItemsRequired);
        if (targetCount <= 0)
            return 0;

        EnsureInventory();
        int removed = 0;
        int capacity = GetActiveShipInventoryCapacity();
        CurrentProfile.Inventory.Normalize();
        for (int i = 0; i < CurrentProfile.Inventory.ShipSlots.Length && i < capacity; i++)
        {
            string itemId = CurrentProfile.Inventory.ShipSlots[i];
            if (!IsPathfinderValuableResearchItem(itemId))
                continue;

            CurrentProfile.Inventory.ShipSlots[i] = null;
            removed++;
            if (removed >= targetCount)
                break;
        }

        return removed;
    }

    public static bool IsPathfinderValuableResearchItem(string itemId)
    {
        return string.Equals(itemId, InventoryItemCatalog.AsteroidLegendaryId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.CashSuitcaseId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.PirateCaseId, StringComparison.Ordinal);
    }

    async Task SavePathfinderResearchProgressAsync(string operationName)
    {
        try
        {
            IsBusy = true;
            EnsureShipUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudUnlockedShipsKey] = SerializeShipUnlocks(CurrentProfile.UnlockedShipIds),
                [CloudPathfinderResearchProgressKey] = SerializePathfinderResearchProgress(CurrentProfile.PathfinderResearchProgress)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                operationName);
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Pathfinder research save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveInventoryAndPathfinderResearchProgressAsync(string operationName)
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
                [CloudPathfinderResearchProgressKey] = SerializePathfinderResearchProgress(CurrentProfile.PathfinderResearchProgress),
                [CloudMissEnigmaRecoverableUniqueItemsKey] = SerializeMissEnigmaUniqueItemRecoveries(CurrentProfile.MissEnigmaRecoverableUniqueItemIds)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                operationName);
            InventoryRevision++;
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Pathfinder inventory/progress save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public static PathfinderResearchProgressData NormalizePathfinderResearchProgress(PathfinderResearchProgressData progress, string[] shipIds = null)
    {
        bool shipUnlocked = ContainsShipTypeId(shipIds, ShipType.Pathfinder);
        if (progress == null)
            return shipUnlocked ? PathfinderResearchProgressData.Complete() : PathfinderResearchProgressData.Empty();

        PathfinderResearchStage stage = (PathfinderResearchStage)Mathf.Clamp(
            progress.Stage,
            (int)PathfinderResearchStage.Locked,
            (int)PathfinderResearchStage.Complete);
        if (shipUnlocked)
            stage = PathfinderResearchStage.Complete;

        if (stage == PathfinderResearchStage.Complete)
            return PathfinderResearchProgressData.Complete();

        string[] hackedTypeIds = NormalizePathfinderHackedShipTypeIds(progress.HackedShipTypeIds);
        int deliveredValuables = Mathf.Clamp(progress.DeliveredValuableItems, 0, PathfinderValuableItemsRequired);
        string completionMapId = LobbyMapUnlockCatalog.NormalizeMapId(progress.ResourceCompletionMapId);

        if (stage == PathfinderResearchStage.Locked && hackedTypeIds.Length > 0)
            stage = PathfinderResearchStage.CollectingData;

        if (hackedTypeIds.Length >= PathfinderHackedShipTypesRequired && stage < PathfinderResearchStage.DocumentationReady)
            stage = PathfinderResearchStage.DocumentationReady;

        if (stage >= PathfinderResearchStage.DocumentationReady)
            hackedTypeIds = ClampPathfinderHackedShipTypeIds(hackedTypeIds);
        else
        {
            deliveredValuables = 0;
            completionMapId = string.Empty;
        }

        if (stage >= PathfinderResearchStage.ResourcesRequired)
        {
            if (deliveredValuables >= PathfinderValuableItemsRequired)
                stage = PathfinderResearchStage.FinalVisitRequired;
        }
        else
        {
            deliveredValuables = 0;
            completionMapId = string.Empty;
        }

        if (stage == PathfinderResearchStage.FinalVisitRequired && string.IsNullOrWhiteSpace(completionMapId))
            stage = PathfinderResearchStage.ResourcesRequired;

        if (stage < PathfinderResearchStage.FinalVisitRequired)
            completionMapId = string.Empty;

        return new PathfinderResearchProgressData
        {
            Stage = (int)stage,
            HackedShipTypeIds = hackedTypeIds,
            DeliveredValuableItems = deliveredValuables,
            ResourceCompletionMapId = completionMapId
        };
    }

    static string[] ClampPathfinderHackedShipTypeIds(string[] typeIds)
    {
        string[] normalized = NormalizePathfinderHackedShipTypeIds(typeIds);
        if (normalized.Length <= PathfinderHackedShipTypesRequired)
            return normalized;

        string[] clamped = new string[PathfinderHackedShipTypesRequired];
        Array.Copy(normalized, clamped, clamped.Length);
        return clamped;
    }

    static string[] NormalizePathfinderHackedShipTypeIds(string[] typeIds)
    {
        if (typeIds == null || typeIds.Length == 0)
            return Array.Empty<string>();

        HashSet<string> normalized = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < typeIds.Length; i++)
        {
            string typeId = NormalizePathfinderHackedShipTypeId(typeIds[i]);
            if (!string.IsNullOrWhiteSpace(typeId))
                normalized.Add(typeId);
        }

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    public static string NormalizePathfinderHackedShipTypeId(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            return string.Empty;

        string normalized = typeId.Trim().ToLowerInvariant();
        const string enemyPrefix = "enemy:";
        const string playerPrefix = "player:";

        if (normalized.StartsWith(enemyPrefix, StringComparison.Ordinal))
        {
            string rawKind = normalized.Substring(enemyPrefix.Length).Replace("_", string.Empty);
            if (Enum.TryParse(rawKind, true, out EnemyBotKind enemyKind) &&
                Enum.IsDefined(typeof(EnemyBotKind), enemyKind))
            {
                return BuildPathfinderEnemyHackTypeId(enemyKind);
            }

            return string.Empty;
        }

        if (normalized.StartsWith(playerPrefix, StringComparison.Ordinal))
        {
            string rawShipType = normalized.Substring(playerPrefix.Length);
            return ShipCatalog.TryGetShipTypeFromId(rawShipType, out ShipType shipType)
                ? BuildPathfinderPlayerHackTypeId(shipType)
                : string.Empty;
        }

        return string.Empty;
    }

    public static string BuildPathfinderEnemyHackTypeId(EnemyBotKind enemyKind)
    {
        return "enemy:" + enemyKind.ToString().ToLowerInvariant();
    }

    public static string BuildPathfinderPlayerHackTypeId(ShipType shipType)
    {
        return "player:" + ShipCatalog.GetShipTypeId(shipType);
    }

    static string[] NormalizeUnlockedShipIdsForPathfinderProgress(string[] shipIds, PathfinderResearchProgressData progress)
    {
        HashSet<string> normalized = new HashSet<string>(NormalizeUnlockedShipIds(shipIds), StringComparer.Ordinal);
        string pathfinderId = ShipCatalog.GetShipTypeId(ShipType.Pathfinder);
        PathfinderResearchStage stage = progress != null
            ? (PathfinderResearchStage)Mathf.Clamp(progress.Stage, (int)PathfinderResearchStage.Locked, (int)PathfinderResearchStage.Complete)
            : PathfinderResearchStage.Locked;

        if (stage == PathfinderResearchStage.Complete)
            normalized.Add(pathfinderId);
        else
            normalized.Remove(pathfinderId);

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }
}
