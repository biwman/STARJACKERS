using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService
{
    public bool IsShipUnlocked(ShipType shipType)
    {
        EnsureShipUnlocks();
        if (shipType == ShipType.Explorer)
            return true;

        if (shipType == ShipType.Viper)
            return GetViperRecoveryStage() == ViperRecoveryStage.Testing ||
                   GetViperRecoveryStage() == ViperRecoveryStage.Complete;

        if (shipType == ShipType.Arrow)
        {
            ArrowLicenseStage stage = GetArrowLicenseStage();
            return stage == ArrowLicenseStage.FinalRunReady ||
                   stage == ArrowLicenseStage.Complete;
        }

        string shipTypeId = ShipCatalog.GetShipTypeId(shipType);
        for (int i = 0; i < CurrentProfile.UnlockedShipIds.Length; i++)
        {
            if (string.Equals(CurrentProfile.UnlockedShipIds[i], shipTypeId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public bool IsShipSkinUnlocked(int shipSkinIndex)
    {
        return IsShipUnlocked(ShipCatalog.GetShipTypeFromSkinIndex(shipSkinIndex));
    }

    public async Task<bool> UnlockShipAsync(ShipType shipType)
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        if (IsShipUnlocked(shipType))
            return false;

        if (shipType == ShipType.Viper)
            CurrentProfile.ViperRecoveryProgress = ViperRecoveryProgressData.Complete();
        else if (shipType == ShipType.Arrow)
            CurrentProfile.ArrowLicenseProgress = ArrowLicenseProgressData.Complete();
        else if (shipType == ShipType.CargoTruck)
            CurrentProfile.BisonIndustrialPartsDelivered = BisonIndustrialPartsRequired;
        else if (shipType == ShipType.Invader)
            CurrentProfile.InvaderImprintsRecovered = InvaderImprintsRequired;
        else if (shipType == ShipType.Pathfinder)
            CurrentProfile.PathfinderResearchProgress = PathfinderResearchProgressData.Complete();

        HashSet<string> ids = new HashSet<string>(CurrentProfile.UnlockedShipIds, StringComparer.Ordinal)
        {
            ShipCatalog.GetShipTypeId(shipType)
        };
        string[] unlockedIds = new string[ids.Count];
        ids.CopyTo(unlockedIds);
        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIds(unlockedIds);
        await SaveShipUnlocksAsync();
        return true;
    }

    public async Task<bool> LockShipAsync(ShipType shipType)
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        if (shipType == ShipType.Explorer || !IsShipUnlocked(shipType))
            return false;

        string shipTypeId = ShipCatalog.GetShipTypeId(shipType);
        List<string> remaining = new List<string>();
        for (int i = 0; i < CurrentProfile.UnlockedShipIds.Length; i++)
        {
            if (!string.Equals(CurrentProfile.UnlockedShipIds[i], shipTypeId, StringComparison.Ordinal))
                remaining.Add(CurrentProfile.UnlockedShipIds[i]);
        }

        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIds(remaining.ToArray());
        if (shipType == ShipType.Viper)
            CurrentProfile.ViperRecoveryProgress = ViperRecoveryProgressData.Empty();
        else if (shipType == ShipType.Arrow)
            CurrentProfile.ArrowLicenseProgress = ArrowLicenseProgressData.Empty();
        else if (shipType == ShipType.CargoTruck)
            CurrentProfile.BisonIndustrialPartsDelivered = 0;
        else if (shipType == ShipType.Invader)
            CurrentProfile.InvaderImprintsRecovered = 0;
        else if (shipType == ShipType.Pathfinder)
            CurrentProfile.PathfinderResearchProgress = PathfinderResearchProgressData.Empty();

        if (ShipCatalog.GetShipTypeFromSkinIndex(CurrentProfile.ShipSkinIndex) == shipType)
            CurrentProfile.ShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex;

        await SaveShipUnlocksAsync();
        return true;
    }

    public async Task UnlockAllShipsAsync()
    {
        await EnsureInitializedAsync();
        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIds(ShipCatalog.GetAllShipTypeIds());
        CurrentProfile.ViperRecoveryProgress = ViperRecoveryProgressData.Complete();
        CurrentProfile.ArrowLicenseProgress = ArrowLicenseProgressData.Complete();
        CurrentProfile.BisonIndustrialPartsDelivered = BisonIndustrialPartsRequired;
        CurrentProfile.InvaderImprintsRecovered = InvaderImprintsRequired;
        CurrentProfile.PathfinderResearchProgress = PathfinderResearchProgressData.Complete();
        await SaveShipUnlocksAsync();
    }

    public async Task LockAllShipsAsync()
    {
        await EnsureInitializedAsync();
        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIds(ShipCatalog.GetDefaultUnlockedShipTypeIds());
        CurrentProfile.ViperRecoveryProgress = ViperRecoveryProgressData.Empty();
        CurrentProfile.ArrowLicenseProgress = ArrowLicenseProgressData.Empty();
        CurrentProfile.BisonIndustrialPartsDelivered = 0;
        CurrentProfile.InvaderImprintsRecovered = 0;
        CurrentProfile.PathfinderResearchProgress = PathfinderResearchProgressData.Empty();
        CurrentProfile.ShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex;
        await SaveShipUnlocksAsync();
    }

    async Task SaveShipUnlocksAsync()
    {
        try
        {
            IsBusy = true;
            EnsureShipUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudShipSkinKey] = CurrentProfile.ShipSkinIndex,
                [CloudUnlockedShipsKey] = SerializeShipUnlocks(CurrentProfile.UnlockedShipIds),
                [CloudViperRecoveryProgressKey] = SerializeViperRecoveryProgress(CurrentProfile.ViperRecoveryProgress),
                [CloudArrowLicenseProgressKey] = SerializeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress),
                [CloudBisonIndustrialPartsDeliveredKey] = CurrentProfile.BisonIndustrialPartsDelivered,
                [CloudInvaderImprintsRecoveredKey] = CurrentProfile.InvaderImprintsRecovered,
                [CloudPathfinderResearchProgressKey] = SerializePathfinderResearchProgress(CurrentProfile.PathfinderResearchProgress)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save ship unlocks");
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService ship unlock save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    static int ConvertPlayerPropertyToInt(object value, int fallback)
    {
        switch (value)
        {
            case int i:
                return i;
            case short s:
                return s;
            case byte b:
                return b;
            case long l:
                return l < int.MinValue ? int.MinValue : l > int.MaxValue ? int.MaxValue : (int)l;
            case float f:
                return Mathf.RoundToInt(f);
            case double d:
                return d < int.MinValue ? int.MinValue : d > int.MaxValue ? int.MaxValue : (int)Math.Round(d);
            default:
                return fallback;
        }
    }

    void EnsureShipUnlocks()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIds(CurrentProfile.UnlockedShipIds);
        CurrentProfile.AvengerTheftAttempt = NormalizeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt);
        CurrentProfile.ViperRecoveryProgress = NormalizeViperRecoveryProgress(CurrentProfile.ViperRecoveryProgress, CurrentProfile.UnlockedShipIds);
        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIdsForViperProgress(CurrentProfile.UnlockedShipIds, CurrentProfile.ViperRecoveryProgress);
        CurrentProfile.ArrowLicenseProgress = NormalizeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress, CurrentProfile.UnlockedShipIds);
        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIdsForArrowProgress(CurrentProfile.UnlockedShipIds, CurrentProfile.ArrowLicenseProgress);
        CurrentProfile.BisonIndustrialPartsDelivered = NormalizeBisonIndustrialPartsDelivered(CurrentProfile.BisonIndustrialPartsDelivered, CurrentProfile.UnlockedShipIds);
        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIdsForBisonProgress(CurrentProfile.UnlockedShipIds, CurrentProfile.BisonIndustrialPartsDelivered);
        CurrentProfile.InvaderImprintsRecovered = NormalizeInvaderImprintsRecovered(CurrentProfile.InvaderImprintsRecovered, CurrentProfile.UnlockedShipIds);
        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIdsForInvaderProgress(CurrentProfile.UnlockedShipIds, CurrentProfile.InvaderImprintsRecovered);
        CurrentProfile.PathfinderResearchProgress = NormalizePathfinderResearchProgress(CurrentProfile.PathfinderResearchProgress, CurrentProfile.UnlockedShipIds);
        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIdsForPathfinderProgress(CurrentProfile.UnlockedShipIds, CurrentProfile.PathfinderResearchProgress);

        ShipType selectedShipType = ShipCatalog.GetShipTypeFromSkinIndex(CurrentProfile.ShipSkinIndex);
        string selectedShipId = ShipCatalog.GetShipTypeId(selectedShipType);
        ArrowLicenseStage arrowStage = (ArrowLicenseStage)Mathf.Clamp(
            CurrentProfile.ArrowLicenseProgress != null ? CurrentProfile.ArrowLicenseProgress.Stage : (int)ArrowLicenseStage.Locked,
            (int)ArrowLicenseStage.Locked,
            (int)ArrowLicenseStage.Complete);
        bool selectedShipAllowed = selectedShipType == ShipType.Explorer ||
                                   Array.IndexOf(CurrentProfile.UnlockedShipIds, selectedShipId) >= 0 ||
                                   (selectedShipType == ShipType.Arrow && arrowStage == ArrowLicenseStage.FinalRunReady);
        if (selectedShipType != ShipType.Explorer &&
            !selectedShipAllowed)
        {
            CurrentProfile.ShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex;
        }
    }

    public static string[] NormalizeUnlockedShipIds(string[] shipIds)
    {
        HashSet<string> normalized = new HashSet<string>(StringComparer.Ordinal)
        {
            ShipCatalog.GetShipTypeId(ShipType.Explorer)
        };

        if (shipIds != null)
        {
            for (int i = 0; i < shipIds.Length; i++)
            {
                string normalizedId = ShipCatalog.NormalizeShipTypeId(shipIds[i]);
                if (!string.IsNullOrWhiteSpace(normalizedId))
                    normalized.Add(normalizedId);
            }
        }

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    static bool ContainsShipTypeId(string[] shipIds, ShipType shipType)
    {
        if (shipIds == null)
            return false;

        string targetId = ShipCatalog.GetShipTypeId(shipType);
        for (int i = 0; i < shipIds.Length; i++)
        {
            if (string.Equals(ShipCatalog.NormalizeShipTypeId(shipIds[i]), targetId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
