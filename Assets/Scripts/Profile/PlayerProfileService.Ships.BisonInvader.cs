using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService
{
    public const int BisonIndustrialPartsRequired = 6;
    public const int InvaderImprintsRequired = 4;

    public int GetBisonIndustrialPartsDeliveredCount()
    {
        EnsureShipUnlocks();
        return CurrentProfile != null ? Mathf.Clamp(CurrentProfile.BisonIndustrialPartsDelivered, 0, BisonIndustrialPartsRequired) : 0;
    }

    public int GetInvaderImprintsRecoveredCount()
    {
        EnsureShipUnlocks();
        return CurrentProfile != null ? Mathf.Clamp(CurrentProfile.InvaderImprintsRecovered, 0, InvaderImprintsRequired) : 0;
    }

    public async Task<int> RecordBisonIndustrialPartsDeliveredAsync()
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        if (IsShipUnlocked(ShipType.CargoTruck))
        {
            CurrentProfile.BisonIndustrialPartsDelivered = BisonIndustrialPartsRequired;
            return CurrentProfile.BisonIndustrialPartsDelivered;
        }

        CurrentProfile.BisonIndustrialPartsDelivered = Mathf.Clamp(
            CurrentProfile.BisonIndustrialPartsDelivered + 1,
            0,
            BisonIndustrialPartsRequired);

        if (CurrentProfile.BisonIndustrialPartsDelivered >= BisonIndustrialPartsRequired)
        {
            HashSet<string> ids = new HashSet<string>(CurrentProfile.UnlockedShipIds, StringComparer.Ordinal)
            {
                ShipCatalog.GetShipTypeId(ShipType.CargoTruck)
            };
            string[] unlockedIds = new string[ids.Count];
            ids.CopyTo(unlockedIds);
            CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIds(unlockedIds);
            CurrentProfile.BisonIndustrialPartsDelivered = BisonIndustrialPartsRequired;
        }

        await SaveBisonIndustrialProgressAsync();
        return CurrentProfile.BisonIndustrialPartsDelivered;
    }

    public async Task<int> RecordInvaderImprintRecoveredAsync(int completedStage)
    {
        await EnsureInitializedAsync();
        EnsureShipUnlocks();

        if (IsShipUnlocked(ShipType.Invader))
        {
            CurrentProfile.InvaderImprintsRecovered = InvaderImprintsRequired;
            return CurrentProfile.InvaderImprintsRecovered;
        }

        int safeStage = Mathf.Clamp(completedStage, 1, InvaderImprintsRequired);
        CurrentProfile.InvaderImprintsRecovered = Mathf.Clamp(
            Mathf.Max(CurrentProfile.InvaderImprintsRecovered + 1, safeStage),
            0,
            InvaderImprintsRequired);

        if (CurrentProfile.InvaderImprintsRecovered >= InvaderImprintsRequired)
        {
            HashSet<string> ids = new HashSet<string>(CurrentProfile.UnlockedShipIds, StringComparer.Ordinal)
            {
                ShipCatalog.GetShipTypeId(ShipType.Invader)
            };
            string[] unlockedIds = new string[ids.Count];
            ids.CopyTo(unlockedIds);
            CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIds(unlockedIds);
            CurrentProfile.InvaderImprintsRecovered = InvaderImprintsRequired;
        }

        await SaveInvaderImprintProgressAsync();
        return CurrentProfile.InvaderImprintsRecovered;
    }

    async Task SaveBisonIndustrialProgressAsync()
    {
        try
        {
            IsBusy = true;
            EnsureShipUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudUnlockedShipsKey] = SerializeShipUnlocks(CurrentProfile.UnlockedShipIds),
                [CloudBisonIndustrialPartsDeliveredKey] = CurrentProfile.BisonIndustrialPartsDelivered,
                [CloudInvaderImprintsRecoveredKey] = CurrentProfile.InvaderImprintsRecovered
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save Bison industrial parts progress");
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Bison industrial progress save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveInvaderImprintProgressAsync()
    {
        try
        {
            IsBusy = true;
            EnsureShipUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudUnlockedShipsKey] = SerializeShipUnlocks(CurrentProfile.UnlockedShipIds),
                [CloudInvaderImprintsRecoveredKey] = CurrentProfile.InvaderImprintsRecovered
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save Invader imprint progress");
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Invader imprint progress save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public static bool PlayerNeedsBisonIndustrialParts(Photon.Realtime.Player player)
    {
        if (player == null)
            return false;

        if (player.CustomProperties != null &&
            player.CustomProperties.TryGetValue(PlayerBisonIndustrialPartsDeliveredKey, out object value))
        {
            return ConvertPlayerPropertyToInt(value, BisonIndustrialPartsRequired) < BisonIndustrialPartsRequired;
        }

        return player == PhotonNetwork.LocalPlayer &&
               HasInstance &&
               Instance.IsInitialized &&
               Instance.GetBisonIndustrialPartsDeliveredCount() < BisonIndustrialPartsRequired &&
               !Instance.IsShipUnlocked(ShipType.CargoTruck);
    }

    public static bool PlayerNeedsInvaderImprints(Photon.Realtime.Player player)
    {
        if (player == null)
            return false;

        if (player.CustomProperties != null &&
            player.CustomProperties.TryGetValue(PlayerInvaderImprintsRecoveredKey, out object value))
        {
            return ConvertPlayerPropertyToInt(value, InvaderImprintsRequired) < InvaderImprintsRequired;
        }

        return player == PhotonNetwork.LocalPlayer &&
               HasInstance &&
               Instance.IsInitialized &&
               Instance.GetInvaderImprintsRecoveredCount() < InvaderImprintsRequired &&
               !Instance.IsShipUnlocked(ShipType.Invader);
    }

    public static int GetPlayerInvaderImprintsRecovered(Photon.Realtime.Player player)
    {
        if (player == null)
            return InvaderImprintsRequired;

        if (player.CustomProperties != null &&
            player.CustomProperties.TryGetValue(PlayerInvaderImprintsRecoveredKey, out object value))
        {
            return ConvertPlayerPropertyToInt(value, InvaderImprintsRequired);
        }

        return player == PhotonNetwork.LocalPlayer &&
               HasInstance &&
               Instance.IsInitialized
            ? Instance.GetInvaderImprintsRecoveredCount()
            : InvaderImprintsRequired;
    }

    public static int NormalizeBisonIndustrialPartsDelivered(int deliveredCount, string[] shipIds = null)
    {
        if (ContainsShipTypeId(shipIds, ShipType.CargoTruck))
            return BisonIndustrialPartsRequired;

        return Mathf.Clamp(deliveredCount, 0, BisonIndustrialPartsRequired);
    }

    public static int NormalizeInvaderImprintsRecovered(int recoveredCount, string[] shipIds = null)
    {
        if (ContainsShipTypeId(shipIds, ShipType.Invader))
            return InvaderImprintsRequired;

        return Mathf.Clamp(recoveredCount, 0, InvaderImprintsRequired);
    }

    static string[] NormalizeUnlockedShipIdsForBisonProgress(string[] shipIds, int deliveredCount)
    {
        HashSet<string> normalized = new HashSet<string>(NormalizeUnlockedShipIds(shipIds), StringComparer.Ordinal);
        string bisonId = ShipCatalog.GetShipTypeId(ShipType.CargoTruck);
        if (deliveredCount >= BisonIndustrialPartsRequired)
            normalized.Add(bisonId);
        else
            normalized.Remove(bisonId);

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    static string[] NormalizeUnlockedShipIdsForInvaderProgress(string[] shipIds, int recoveredCount)
    {
        HashSet<string> normalized = new HashSet<string>(NormalizeUnlockedShipIds(shipIds), StringComparer.Ordinal);
        string invaderId = ShipCatalog.GetShipTypeId(ShipType.Invader);
        if (recoveredCount >= InvaderImprintsRequired)
            normalized.Add(invaderId);
        else
            normalized.Remove(invaderId);

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }
}
