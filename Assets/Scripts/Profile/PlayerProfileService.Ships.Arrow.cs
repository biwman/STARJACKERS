using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService
{
    public const int ArrowQualifierChipsRequired = 2;
    public const int ArrowMapRacesRequired = 3;
    public const int ArrowTimeTrialMinimumRank = (int)ArrowTimeTrialRank.B;
    public const string ArrowIonNozzlePartId = "ion_nozzle";
    public const string ArrowGyroStabilizerPartId = "gyro_stabilizer";
    public const string ArrowRaceTransponderPartId = "race_transponder";

    public ArrowLicenseStage GetArrowLicenseStage()
    {
        ArrowLicenseProgressData progress = CurrentProfile != null
            ? NormalizeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress, CurrentProfile.UnlockedShipIds)
            : ArrowLicenseProgressData.Empty();
        return (ArrowLicenseStage)Mathf.Clamp(progress.Stage, (int)ArrowLicenseStage.Locked, (int)ArrowLicenseStage.Complete);
    }

    public ArrowLicenseProgressData GetArrowLicenseProgress()
    {
        ArrowLicenseProgressData progress = CurrentProfile != null
            ? NormalizeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress, CurrentProfile.UnlockedShipIds)
            : ArrowLicenseProgressData.Empty();
        return progress.Clone();
    }

    public bool IsArrowLicenseComplete()
    {
        return GetArrowLicenseStage() == ArrowLicenseStage.Complete;
    }

    public bool HasArrowRaceTokenInShipForMap(string mapId)
    {
        EnsureInventory();
        if (!InventoryItemCatalog.TryGetArrowRaceTokenForMap(mapId, out string tokenItemId))
            return false;

        return CountItemInSlots(CurrentProfile.Inventory.ShipSlots, GetActiveShipInventoryCapacity(), tokenItemId) > 0;
    }

    public async Task<bool> RecordArrowQualifierTrialAsync()
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        ArrowLicenseProgressData progress = NormalizeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress, CurrentProfile.UnlockedShipIds);
        ArrowLicenseStage stage = (ArrowLicenseStage)progress.Stage;
        if (stage != ArrowLicenseStage.Locked && stage != ArrowLicenseStage.Qualifying)
            return false;

        int chips = Mathf.Clamp(progress.QualifierChips + 1, 0, ArrowQualifierChipsRequired);
        CurrentProfile.ArrowLicenseProgress = new ArrowLicenseProgressData
        {
            Stage = chips >= ArrowQualifierChipsRequired ? (int)ArrowLicenseStage.TokenCollectionRequired : (int)ArrowLicenseStage.Qualifying,
            QualifierChips = chips,
            IonNozzleDelivered = progress.IonNozzleDelivered,
            GyroStabilizerDelivered = progress.GyroStabilizerDelivered,
            RaceTransponderDelivered = progress.RaceTransponderDelivered,
            BestTimeTrialRank = progress.BestTimeTrialRank,
            GhostRaceWon = progress.GhostRaceWon,
            CompletedRaceMapIds = progress.CompletedRaceMapIds,
            ActiveRaceMapId = string.Empty,
            FinalRunEntryAvailable = progress.FinalRunEntryAvailable,
            FinalRunActive = false,
            OriginalShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex
        };

        await SaveArrowLicenseProgressAsync("save Arrow qualifier");
        return true;
    }

    public async Task<string> BeginArrowMapRaceAsync(string mapId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureShipUnlocks();

        if (!InventoryItemCatalog.TryGetArrowRaceTokenForMap(mapId, out string tokenItemId))
            return string.Empty;

        ArrowLicenseProgressData progress = NormalizeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress, CurrentProfile.UnlockedShipIds);
        ArrowLicenseStage stage = (ArrowLicenseStage)progress.Stage;
        if (stage != ArrowLicenseStage.TokenCollectionRequired && stage != ArrowLicenseStage.MapRacesRequired)
            return string.Empty;

        string normalizedMapId = NormalizeArrowRaceMapId(mapId);
        if (IsArrowRaceMapCompleted(progress, normalizedMapId))
            return string.Empty;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        int slotIndex = FindItemInSlots(workingInventory.ShipSlots, GetActiveShipInventoryCapacity(), tokenItemId);
        if (slotIndex < 0)
            return string.Empty;

        workingInventory.ShipSlots[slotIndex] = null;
        progress.Stage = (int)ArrowLicenseStage.MapRacesRequired;
        progress.QualifierChips = ArrowQualifierChipsRequired;
        progress.ActiveRaceMapId = normalizedMapId;
        progress.FinalRunEntryAvailable = false;
        progress.FinalRunActive = false;
        progress.OriginalShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex;

        CurrentProfile.Inventory = workingInventory;
        CurrentProfile.ArrowLicenseProgress = progress;
        await SaveInventoryAndArrowLicenseProgressAsync("begin Arrow map race");
        return tokenItemId;
    }

    public async Task<bool> RecordArrowMapRaceCompletedAsync(string mapId)
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        string normalizedMapId = NormalizeArrowRaceMapId(mapId);
        if (string.IsNullOrWhiteSpace(normalizedMapId))
            return false;

        ArrowLicenseProgressData progress = NormalizeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress, CurrentProfile.UnlockedShipIds);
        ArrowLicenseStage stage = (ArrowLicenseStage)progress.Stage;
        if (stage != ArrowLicenseStage.TokenCollectionRequired && stage != ArrowLicenseStage.MapRacesRequired)
            return false;

        HashSet<string> completed = new HashSet<string>(NormalizeArrowCompletedRaceMapIds(progress.CompletedRaceMapIds), StringComparer.Ordinal)
        {
            normalizedMapId
        };
        string[] completedMaps = new string[completed.Count];
        completed.CopyTo(completedMaps);
        Array.Sort(completedMaps, StringComparer.Ordinal);

        progress.CompletedRaceMapIds = completedMaps;
        progress.ActiveRaceMapId = string.Empty;
        progress.QualifierChips = ArrowQualifierChipsRequired;
        progress.Stage = completedMaps.Length >= ArrowMapRacesRequired
            ? (int)ArrowLicenseStage.FinalRunReady
            : (int)ArrowLicenseStage.MapRacesRequired;
        progress.FinalRunEntryAvailable = completedMaps.Length >= ArrowMapRacesRequired;
        progress.FinalRunActive = false;
        progress.OriginalShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex;

        CurrentProfile.ArrowLicenseProgress = progress;
        await SaveArrowLicenseProgressAsync("save Arrow map race");
        return true;
    }

    public async Task<string> RecordArrowRacingPartAsync()
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        ArrowLicenseProgressData progress = NormalizeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress, CurrentProfile.UnlockedShipIds);
        if ((ArrowLicenseStage)progress.Stage != ArrowLicenseStage.PartsRequired)
            return string.Empty;

        string awardedPartId = string.Empty;
        if (!progress.IonNozzleDelivered)
        {
            progress.IonNozzleDelivered = true;
            awardedPartId = ArrowIonNozzlePartId;
        }
        else if (!progress.GyroStabilizerDelivered)
        {
            progress.GyroStabilizerDelivered = true;
            awardedPartId = ArrowGyroStabilizerPartId;
        }
        else if (!progress.RaceTransponderDelivered)
        {
            progress.RaceTransponderDelivered = true;
            awardedPartId = ArrowRaceTransponderPartId;
        }

        if (string.IsNullOrWhiteSpace(awardedPartId))
            return string.Empty;

        progress.Stage = AreArrowRacingPartsDelivered(progress)
            ? (int)ArrowLicenseStage.TimeTrialRequired
            : (int)ArrowLicenseStage.PartsRequired;
        progress.FinalRunActive = false;
        progress.OriginalShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex;
        CurrentProfile.ArrowLicenseProgress = progress;
        await SaveArrowLicenseProgressAsync("save Arrow racing part");
        return awardedPartId;
    }

    public async Task<ArrowTimeTrialRank> RecordArrowTimeTrialAsync(float elapsedSeconds)
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        ArrowLicenseProgressData progress = NormalizeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress, CurrentProfile.UnlockedShipIds);
        if ((ArrowLicenseStage)progress.Stage != ArrowLicenseStage.TimeTrialRequired)
            return ArrowTimeTrialRank.None;

        ArrowTimeTrialRank rank = ResolveArrowTimeTrialRank(elapsedSeconds);
        progress.BestTimeTrialRank = Mathf.Max(progress.BestTimeTrialRank, (int)rank);
        if ((int)rank >= ArrowTimeTrialMinimumRank)
            progress.Stage = (int)ArrowLicenseStage.GhostRaceRequired;

        progress.FinalRunActive = false;
        progress.OriginalShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex;
        CurrentProfile.ArrowLicenseProgress = progress;
        await SaveArrowLicenseProgressAsync("save Arrow time trial");
        return rank;
    }

    public async Task<bool> RecordArrowGhostRaceWonAsync()
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        ArrowLicenseProgressData progress = NormalizeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress, CurrentProfile.UnlockedShipIds);
        if ((ArrowLicenseStage)progress.Stage != ArrowLicenseStage.GhostRaceRequired)
            return false;

        progress.Stage = (int)ArrowLicenseStage.FinalRunReady;
        progress.GhostRaceWon = true;
        progress.FinalRunEntryAvailable = true;
        progress.FinalRunActive = false;
        progress.OriginalShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex;
        CurrentProfile.ArrowLicenseProgress = progress;
        await SaveArrowLicenseProgressAsync("save Arrow ghost race");
        return true;
    }

    public async Task<bool> BeginArrowFinalRunAsync(int originalShipSkinIndex)
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        ArrowLicenseProgressData progress = NormalizeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress, CurrentProfile.UnlockedShipIds);
        if ((ArrowLicenseStage)progress.Stage != ArrowLicenseStage.FinalRunReady || !progress.FinalRunEntryAvailable)
            return false;

        if (ShipCatalog.GetShipTypeFromSkinIndex(GetActiveShipSkinIndex()) != ShipType.Arrow)
            return false;

        progress.FinalRunEntryAvailable = false;
        progress.FinalRunActive = true;
        progress.OriginalShipSkinIndex = Mathf.Clamp(originalShipSkinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex);
        CurrentProfile.ArrowLicenseProgress = progress;
        await SaveArrowLicenseProgressAsync("begin Arrow final run");
        return true;
    }

    public async Task<bool> CompleteArrowFinalRunAsync(int returningShipSkinIndex, bool objectivesComplete)
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        ArrowLicenseProgressData progress = NormalizeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress, CurrentProfile.UnlockedShipIds);
        if (!progress.FinalRunActive)
            return false;

        if (!objectivesComplete || ShipCatalog.GetShipTypeFromSkinIndex(returningShipSkinIndex) != ShipType.Arrow)
        {
            await FailArrowFinalRunAsync();
            return false;
        }

        HashSet<string> ids = new HashSet<string>(CurrentProfile.UnlockedShipIds, StringComparer.Ordinal)
        {
            ShipCatalog.GetShipTypeId(ShipType.Arrow)
        };
        string[] unlockedIds = new string[ids.Count];
        ids.CopyTo(unlockedIds);
        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIds(unlockedIds);
        CurrentProfile.ShipSkinIndex = ShipCatalog.ArrowSmoothSkinIndex;
        CurrentProfile.ArrowLicenseProgress = ArrowLicenseProgressData.Complete();

        await SaveArrowLicenseProgressAsync("complete Arrow final run");
        return true;
    }

    public async Task FailArrowFinalRunAsync()
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        ArrowLicenseProgressData progress = NormalizeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress, CurrentProfile.UnlockedShipIds);
        if (!progress.FinalRunActive)
            return;

        progress.Stage = (int)ArrowLicenseStage.FinalRunReady;
        progress.GhostRaceWon = true;
        progress.FinalRunEntryAvailable = true;
        progress.FinalRunActive = false;
        progress.OriginalShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex;
        CurrentProfile.ArrowLicenseProgress = progress;
        await SaveArrowLicenseProgressAsync("fail Arrow final run");
    }

    async Task SaveInventoryAndArrowLicenseProgressAsync(string operationName)
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
                [CloudArrowLicenseProgressKey] = SerializeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress),
                [CloudInvaderImprintsRecoveredKey] = CurrentProfile.InvaderImprintsRecovered,
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
            Debug.LogError("PlayerProfileService Arrow inventory/progress save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveArrowLicenseProgressAsync(string operationName)
    {
        try
        {
            IsBusy = true;
            EnsureShipUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudShipSkinKey] = CurrentProfile.ShipSkinIndex,
                [CloudUnlockedShipsKey] = SerializeShipUnlocks(CurrentProfile.UnlockedShipIds),
                [CloudArrowLicenseProgressKey] = SerializeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress),
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
            Debug.LogError("PlayerProfileService Arrow license save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public static bool PlayerNeedsArrowLicense(Photon.Realtime.Player player)
    {
        if (player == null)
            return false;

        return GetPlayerArrowLicenseStage(player) != ArrowLicenseStage.Complete;
    }

    public static bool PlayerNeedsArrowQualification(Photon.Realtime.Player player)
    {
        ArrowLicenseStage stage = GetPlayerArrowLicenseStage(player);
        return stage == ArrowLicenseStage.Locked || stage == ArrowLicenseStage.Qualifying;
    }

    public static bool PlayerCanCollectArrowRaceTokens(Photon.Realtime.Player player)
    {
        ArrowLicenseStage stage = GetPlayerArrowLicenseStage(player);
        return stage == ArrowLicenseStage.TokenCollectionRequired || stage == ArrowLicenseStage.MapRacesRequired;
    }

    public static bool PlayerHasArrowRaceTokenForSelectedMap(Photon.Realtime.Player player)
    {
        if (player == null)
            return false;

        ArrowLicenseStage stage = GetPlayerArrowLicenseStage(player);
        if (stage != ArrowLicenseStage.TokenCollectionRequired && stage != ArrowLicenseStage.MapRacesRequired)
            return false;

        if (!InventoryItemCatalog.TryGetArrowRaceTokenForMap(RoomSettings.GetSelectedLobbyMapId(), out string tokenItemId))
            return false;

        string[] slots = GetPlayerShipInventorySlots(player);
        int capacity = GetPlayerShipInventoryCapacity(player);
        return CountItemInSlots(slots, capacity, tokenItemId) > 0;
    }

    public static bool PlayerIsArrowFinalRoundCandidate(Photon.Realtime.Player player)
    {
        if (player == null)
            return false;

        if (GetPlayerArrowLicenseStage(player) != ArrowLicenseStage.FinalRunReady ||
            !PlayerHasArrowFinalRunReady(player) ||
            !string.Equals(RoomSettings.GetSelectedLobbyMapId(), LobbyMapCatalog.AncientSpaceMapId, StringComparison.Ordinal))
        {
            return false;
        }

        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(player, ShipCatalog.ExplorerBasicSkinIndex);
        return ShipCatalog.GetShipTypeFromSkinIndex(shipSkinIndex) == ShipType.Arrow;
    }

    public static bool PlayerHasArrowFinalRunReady(Photon.Realtime.Player player)
    {
        if (player == null)
            return false;

        if (player.CustomProperties != null &&
            player.CustomProperties.TryGetValue(PlayerArrowFinalRunReadyKey, out object value) &&
            value is bool ready)
        {
            return ready;
        }

        return player == PhotonNetwork.LocalPlayer &&
               HasInstance &&
               Instance.IsInitialized &&
               Instance.GetArrowLicenseProgress().FinalRunEntryAvailable;
    }

    static ArrowLicenseStage GetPlayerArrowLicenseStage(Photon.Realtime.Player player)
    {
        if (player == null)
            return ArrowLicenseStage.Complete;

        if (player.CustomProperties != null &&
            player.CustomProperties.TryGetValue(PlayerArrowLicenseStageKey, out object value))
        {
            int stageValue = ConvertPlayerPropertyToInt(value, (int)ArrowLicenseStage.Complete);
            return (ArrowLicenseStage)Mathf.Clamp(stageValue, (int)ArrowLicenseStage.Locked, (int)ArrowLicenseStage.Complete);
        }

        if (player == PhotonNetwork.LocalPlayer && HasInstance && Instance.IsInitialized)
            return Instance.GetArrowLicenseStage();

        return ArrowLicenseStage.Complete;
    }

    public static ArrowLicenseProgressData NormalizeArrowLicenseProgress(ArrowLicenseProgressData progress, string[] shipIds = null)
    {
        bool shipUnlocked = ContainsShipTypeId(shipIds, ShipType.Arrow);
        if (progress == null)
            return shipUnlocked ? ArrowLicenseProgressData.Complete() : ArrowLicenseProgressData.Empty();

        ArrowLicenseStage stage = (ArrowLicenseStage)Mathf.Clamp(progress.Stage, (int)ArrowLicenseStage.Locked, (int)ArrowLicenseStage.Complete);
        if (shipUnlocked)
            stage = ArrowLicenseStage.Complete;

        if (stage == ArrowLicenseStage.Complete)
            return ArrowLicenseProgressData.Complete();

        int qualifierChips = Mathf.Clamp(progress.QualifierChips, 0, ArrowQualifierChipsRequired);
        if (stage == ArrowLicenseStage.Locked && qualifierChips > 0)
            stage = ArrowLicenseStage.Qualifying;

        if (stage == ArrowLicenseStage.Qualifying && qualifierChips <= 0)
            stage = ArrowLicenseStage.Locked;

        if (stage >= ArrowLicenseStage.TokenCollectionRequired)
            qualifierChips = ArrowQualifierChipsRequired;

        if (qualifierChips >= ArrowQualifierChipsRequired && stage < ArrowLicenseStage.TokenCollectionRequired)
            stage = ArrowLicenseStage.TokenCollectionRequired;

        if (stage == ArrowLicenseStage.GhostRaceRequired)
            stage = ArrowLicenseStage.MapRacesRequired;

        string[] completedRaceMapIds = stage >= ArrowLicenseStage.TokenCollectionRequired
            ? NormalizeArrowCompletedRaceMapIds(progress.CompletedRaceMapIds)
            : Array.Empty<string>();
        int completedRaceCount = completedRaceMapIds.Length;

        if (completedRaceCount >= ArrowMapRacesRequired)
            stage = ArrowLicenseStage.FinalRunReady;
        else if (completedRaceCount > 0 && stage == ArrowLicenseStage.TokenCollectionRequired)
            stage = ArrowLicenseStage.MapRacesRequired;

        bool finalActive = stage == ArrowLicenseStage.FinalRunReady && progress.FinalRunActive;
        bool finalEntryAvailable = stage == ArrowLicenseStage.FinalRunReady && !finalActive;

        ArrowLicenseProgressData normalized = new ArrowLicenseProgressData
        {
            Stage = (int)stage,
            QualifierChips = qualifierChips,
            IonNozzleDelivered = progress.IonNozzleDelivered,
            GyroStabilizerDelivered = progress.GyroStabilizerDelivered,
            RaceTransponderDelivered = progress.RaceTransponderDelivered,
            BestTimeTrialRank = Mathf.Clamp(progress.BestTimeTrialRank, (int)ArrowTimeTrialRank.None, (int)ArrowTimeTrialRank.S),
            GhostRaceWon = progress.GhostRaceWon,
            CompletedRaceMapIds = completedRaceMapIds,
            ActiveRaceMapId = stage == ArrowLicenseStage.MapRacesRequired ? NormalizeArrowRaceMapId(progress.ActiveRaceMapId) : string.Empty,
            FinalRunEntryAvailable = finalEntryAvailable,
            FinalRunActive = finalActive,
            OriginalShipSkinIndex = Mathf.Clamp(progress.OriginalShipSkinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex)
        };

        if ((ArrowLicenseStage)normalized.Stage >= ArrowLicenseStage.TokenCollectionRequired)
        {
            normalized.IonNozzleDelivered = true;
            normalized.GyroStabilizerDelivered = true;
            normalized.RaceTransponderDelivered = true;
        }

        if ((ArrowLicenseStage)normalized.Stage >= ArrowLicenseStage.FinalRunReady)
        {
            normalized.BestTimeTrialRank = Mathf.Max(normalized.BestTimeTrialRank, ArrowTimeTrialMinimumRank);
            normalized.GhostRaceWon = true;
        }

        if ((ArrowLicenseStage)normalized.Stage < ArrowLicenseStage.FinalRunReady)
        {
            normalized.FinalRunEntryAvailable = false;
            normalized.FinalRunActive = false;
        }

        if ((ArrowLicenseStage)normalized.Stage < ArrowLicenseStage.MapRacesRequired)
            normalized.GhostRaceWon = false;

        return normalized;
    }

    static string[] NormalizeUnlockedShipIdsForArrowProgress(string[] shipIds, ArrowLicenseProgressData progress)
    {
        HashSet<string> normalized = new HashSet<string>(NormalizeUnlockedShipIds(shipIds), StringComparer.Ordinal);
        string arrowId = ShipCatalog.GetShipTypeId(ShipType.Arrow);
        ArrowLicenseStage stage = progress != null
            ? (ArrowLicenseStage)Mathf.Clamp(progress.Stage, (int)ArrowLicenseStage.Locked, (int)ArrowLicenseStage.Complete)
            : ArrowLicenseStage.Locked;

        if (stage == ArrowLicenseStage.Complete)
            normalized.Add(arrowId);
        else
            normalized.Remove(arrowId);

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    static bool AreArrowRacingPartsDelivered(ArrowLicenseProgressData progress)
    {
        return progress != null &&
               progress.IonNozzleDelivered &&
               progress.GyroStabilizerDelivered &&
               progress.RaceTransponderDelivered;
    }

    public static int CountCompletedArrowRaceMaps(ArrowLicenseProgressData progress)
    {
        return progress != null ? NormalizeArrowCompletedRaceMapIds(progress.CompletedRaceMapIds).Length : 0;
    }

    public static bool IsArrowRaceMapCompleted(ArrowLicenseProgressData progress, string mapId)
    {
        string normalizedMapId = NormalizeArrowRaceMapId(mapId);
        if (progress == null || string.IsNullOrWhiteSpace(normalizedMapId))
            return false;

        string[] completed = NormalizeArrowCompletedRaceMapIds(progress.CompletedRaceMapIds);
        for (int i = 0; i < completed.Length; i++)
        {
            if (string.Equals(completed[i], normalizedMapId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static string[] NormalizeArrowCompletedRaceMapIds(string[] mapIds)
    {
        if (mapIds == null || mapIds.Length == 0)
            return Array.Empty<string>();

        HashSet<string> normalized = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < mapIds.Length; i++)
        {
            string mapId = NormalizeArrowRaceMapId(mapIds[i]);
            if (!string.IsNullOrWhiteSpace(mapId))
                normalized.Add(mapId);
        }

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    public static string NormalizeArrowRaceMapId(string mapId)
    {
        switch (mapId)
        {
            case LobbyMapCatalog.MinefieldMapId:
            case LobbyMapCatalog.SnowFieldMapId:
            case LobbyMapCatalog.DeepSpaceMapId:
            case LobbyMapCatalog.PirateBayMapId:
                return mapId;
            default:
                return string.Empty;
        }
    }

    static ArrowTimeTrialRank ResolveArrowTimeTrialRank(float elapsedSeconds)
    {
        if (elapsedSeconds <= 75f)
            return ArrowTimeTrialRank.S;
        if (elapsedSeconds <= 90f)
            return ArrowTimeTrialRank.A;
        if (elapsedSeconds <= 105f)
            return ArrowTimeTrialRank.B;
        return ArrowTimeTrialRank.C;
    }
}
