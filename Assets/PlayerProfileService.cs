using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using Unity.Services.CloudSave;
using UnityEngine;

public partial class PlayerProfileService : MonoBehaviour
{
    static PlayerProfileService instance;
    Task initializationTask;
    bool initialized;
    int deferredInventorySaveVersion;
    bool deferredInventorySavePending;
    string[] pendingAstronautCargoSlots;
    int pendingAstronautCargoShipSkinIndex = -1;
    bool hasPendingAstronautCargo;
    string pendingProtectedEquipmentItemId;
    int pendingProtectedEquipmentSlotIndex = -1;
    int pendingProtectedEquipmentShipSkinIndex = -1;
    bool hasPendingProtectedEquipment;
    readonly HashSet<string> awardedMatchTokens = new HashSet<string>();
    readonly HashSet<string> awardedMapReturnTokens = new HashSet<string>();
    readonly HashSet<string> awardedCareerRoundTokens = new HashSet<string>();

    public static PlayerProfileService Instance
    {
        get
        {
            EnsureInstance();
            return instance;
        }
    }

    public static bool HasInstance => instance != null;

    public bool IsInitialized => initialized;
    public bool IsBusy { get; private set; }
    public int InventoryRevision { get; private set; }
    public string PlayerId => TryGetAuthenticationPlayerId();
    public PlayerProfileData CurrentProfile { get; private set; } = PlayerProfileData.Default();
    public int CurrentPlayerInventorySlotCount
    {
        get
        {
            EnsureInventory();
            return CurrentProfile.Inventory.PlayerSlots.Length;
        }
    }

    public event Action<PlayerProfileData> ProfileChanged;

    public async Task SaveProfileAsync(string nickname, int shipSkinIndex)
    {
        await EnsureInitializedAsync();

        try
        {
            IsBusy = true;
            SaveProfileLocally(nickname, shipSkinIndex);
            await SaveCurrentProfileToCloudAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SaveProfileLocally(string nickname, int shipSkinIndex)
    {
        EnsureInventory();
        EnsurePilotDefaults();

        CurrentProfile = new PlayerProfileData
        {
            Nickname = SanitizeNickname(nickname),
            ShipSkinIndex = Mathf.Clamp(shipSkinIndex, 0, ShipCatalog.MaxShipSkinIndex),
            GamesPlayed = CurrentProfile != null ? CurrentProfile.GamesPlayed : 0,
            TotalXp = CurrentProfile != null ? CurrentProfile.TotalXp : 0,
                Astrons = CurrentProfile != null ? CurrentProfile.Astrons : DefaultStartingAstrons,
            Inventory = CurrentProfile != null && CurrentProfile.Inventory != null ? CurrentProfile.Inventory.Clone() : PlayerInventoryData.Default(),
            SelectedPilotId = CurrentProfile != null ? PilotCatalog.NormalizePilotId(CurrentProfile.SelectedPilotId) : PilotCatalog.JakeId,
            UnlockedPilotIds = CurrentProfile != null ? PilotCatalog.NormalizeUnlockedPilotIds(CurrentProfile.UnlockedPilotIds) : PilotCatalog.GetDefaultUnlockedPilotIds(),
            UnlockedBlueprintIds = CurrentProfile != null ? NormalizeUnlockedBlueprintIds(CurrentProfile.UnlockedBlueprintIds) : NormalizeUnlockedBlueprintIds(null),
            UnlockedShipIds = CurrentProfile != null ? NormalizeUnlockedShipIds(CurrentProfile.UnlockedShipIds) : ShipCatalog.GetDefaultUnlockedShipTypeIds(),
            AvengerTheftAttempt = CurrentProfile != null ? NormalizeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt) : AvengerTheftAttemptData.Empty(),
            ViperRecoveryProgress = CurrentProfile != null ? NormalizeViperRecoveryProgress(CurrentProfile.ViperRecoveryProgress, CurrentProfile.UnlockedShipIds) : ViperRecoveryProgressData.Empty(),
            ArrowLicenseProgress = CurrentProfile != null ? NormalizeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress, CurrentProfile.UnlockedShipIds) : ArrowLicenseProgressData.Empty(),
            BisonIndustrialPartsDelivered = CurrentProfile != null ? NormalizeBisonIndustrialPartsDelivered(CurrentProfile.BisonIndustrialPartsDelivered, CurrentProfile.UnlockedShipIds) : 0,
            InvaderImprintsRecovered = CurrentProfile != null ? NormalizeInvaderImprintsRecovered(CurrentProfile.InvaderImprintsRecovered, CurrentProfile.UnlockedShipIds) : 0,
            PathfinderResearchProgress = CurrentProfile != null ? NormalizePathfinderResearchProgress(CurrentProfile.PathfinderResearchProgress, CurrentProfile.UnlockedShipIds) : PathfinderResearchProgressData.Empty(),
            MissEnigmaPurchasedBlueprintIds = CurrentProfile != null ? NormalizeMissEnigmaBlueprintPurchases(CurrentProfile.MissEnigmaPurchasedBlueprintIds) : Array.Empty<string>(),
            MissEnigmaRecoverableUniqueItemIds = CurrentProfile != null ? NormalizeMissEnigmaUniqueItemRecoveries(CurrentProfile.MissEnigmaRecoverableUniqueItemIds) : Array.Empty<string>(),
            PilotDroneKills = CurrentProfile != null ? Mathf.Max(0, CurrentProfile.PilotDroneKills) : 0,
            PilotSoldItemsAstrons = CurrentProfile != null ? Mathf.Max(0, CurrentProfile.PilotSoldItemsAstrons) : 0,
            PilotPirateBayReturns = CurrentProfile != null ? Mathf.Max(0, CurrentProfile.PilotPirateBayReturns) : 0,
            PilotAsteroidSalvageCount = CurrentProfile != null ? Mathf.Max(0, CurrentProfile.PilotAsteroidSalvageCount) : 0,
            PilotAshOverloadReturns = CurrentProfile != null ? Mathf.Max(0, CurrentProfile.PilotAshOverloadReturns) : 0,
            PilotAtlasMapReturns = CurrentProfile != null ? PilotCatalog.NormalizeAtlasMapReturnIds(CurrentProfile.PilotAtlasMapReturns) : Array.Empty<string>(),
            MapUnlockProgress = CurrentProfile != null ? NormalizeMapUnlockProgress(CurrentProfile.MapUnlockProgress) : NormalizeMapUnlockProgress(null),
            ProjectProgress = CurrentProfile != null ? ProjectCatalog.NormalizeProgress(CurrentProfile.ProjectProgress) : ProjectCatalog.NormalizeProgress(null),
            CareerStats = CurrentProfile != null ? NormalizeCareerStats(CurrentProfile.CareerStats) : PlayerCareerStatsData.Empty()
        };

        EnsurePilotDefaults();
        EnsureShipUnlocks();
        EnsureBlueprintUnlocks();
        EnsureMissEnigmaBlueprintPurchases();
        EnsureMissEnigmaUniqueItemRecoveries();
        EnsureMapUnlockProgress();
        EnsureProjectProgress();
        EnsureCareerStats();
        ApplyProfileToPhoton();
        NotifyProfileChanged();
    }

    public void ApplyProfileToPhoton()
    {
        if (CurrentProfile == null)
            return;

        if (!string.IsNullOrWhiteSpace(CurrentProfile.Nickname))
            PhotonNetwork.NickName = CurrentProfile.Nickname;

        if (PhotonNetwork.LocalPlayer == null)
            return;

        EnsureInventory();
        EnsurePilotDefaults();
        EnsureShipUnlocks();
        EnsureBlueprintUnlocks();
        EnsureMissEnigmaBlueprintPurchases();
        EnsureMissEnigmaUniqueItemRecoveries();

        var props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.ShipSkinKey] = CurrentProfile.ShipSkinIndex,
            [RoomSettings.PilotIdKey] = CurrentProfile.SelectedPilotId,
            [RoomSettings.ShipInventoryStateKey] = SerializeShipInventorySlots(CurrentProfile.Inventory.ShipSlots),
            [RoomSettings.EquipmentStateKey] = SerializeEquipmentSlots(BuildRuntimeEquipmentSlotsForProfile(CurrentProfile.ShipSkinIndex, CurrentProfile.Inventory.EquipmentSlots)),
            [PlayerViperCargoUnlockedKey] = IsCargoUnlockedForProfile(CurrentProfile.ShipSkinIndex),
            [PlayerViperRecoveryStageKey] = (int)GetViperRecoveryStage(),
            [PlayerArrowLicenseStageKey] = (int)GetArrowLicenseStage(),
            [PlayerArrowFinalRunReadyKey] = GetArrowLicenseProgress().FinalRunEntryAvailable,
            [PlayerBisonIndustrialPartsDeliveredKey] = GetBisonIndustrialPartsDeliveredCount(),
            [PlayerInvaderImprintsRecoveredKey] = GetInvaderImprintsRecoveredCount()
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    public async Task ResetAccountAsync()
    {
        await EnsureInitializedAsync();

        try
        {
            IsBusy = true;

            string nickname = CurrentProfile != null ? CurrentProfile.Nickname : BuildFallbackProfile().Nickname;
            CurrentProfile = new PlayerProfileData
            {
                Nickname = SanitizeNickname(nickname),
                ShipSkinIndex = 0,
                GamesPlayed = 0,
                TotalXp = 0,
                Astrons = DefaultStartingAstrons,
                Inventory = PlayerInventoryData.Default(),
                SelectedPilotId = PilotCatalog.JakeId,
                UnlockedPilotIds = PilotCatalog.GetDefaultUnlockedPilotIds(),
                UnlockedBlueprintIds = NormalizeUnlockedBlueprintIds(null),
                UnlockedShipIds = ShipCatalog.GetDefaultUnlockedShipTypeIds(),
                AvengerTheftAttempt = AvengerTheftAttemptData.Empty(),
                ViperRecoveryProgress = ViperRecoveryProgressData.Empty(),
                ArrowLicenseProgress = ArrowLicenseProgressData.Empty(),
                BisonIndustrialPartsDelivered = 0,
                InvaderImprintsRecovered = 0,
                PathfinderResearchProgress = PathfinderResearchProgressData.Empty(),
                MissEnigmaPurchasedBlueprintIds = Array.Empty<string>(),
                MissEnigmaRecoverableUniqueItemIds = Array.Empty<string>(),
                PilotDroneKills = 0,
                PilotSoldItemsAstrons = 0,
                PilotPirateBayReturns = 0,
                PilotAsteroidSalvageCount = 0,
                PilotAshOverloadReturns = 0,
                PilotAtlasMapReturns = Array.Empty<string>(),
                MapUnlockProgress = NormalizeMapUnlockProgress(null),
                ProjectProgress = ProjectCatalog.NormalizeProgress(null),
                CareerStats = PlayerCareerStatsData.Empty()
            };

            awardedMatchTokens.Clear();
            awardedMapReturnTokens.Clear();
            awardedCareerRoundTokens.Clear();
            EnsureInventory();
            EnsurePilotDefaults();
            EnsureShipUnlocks();
            EnsureBlueprintUnlocks();
            EnsureMissEnigmaBlueprintPurchases();
            EnsureMissEnigmaUniqueItemRecoveries();
            EnsureMapUnlockProgress();
            EnsureProjectProgress();
            EnsureCareerStats();

            var data = new Dictionary<string, object>
            {
                [CloudNicknameKey] = CurrentProfile.Nickname,
                [CloudShipSkinKey] = CurrentProfile.ShipSkinIndex,
                [CloudGamesPlayedKey] = CurrentProfile.GamesPlayed,
                [CloudTotalXpKey] = CurrentProfile.TotalXp,
                [CloudAstronsKey] = CurrentProfile.Astrons,
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudSelectedPilotKey] = CurrentProfile.SelectedPilotId,
                [CloudUnlockedPilotsKey] = SerializePilotUnlocks(CurrentProfile.UnlockedPilotIds),
                [CloudUnlockedBlueprintsKey] = SerializeBlueprintUnlocks(CurrentProfile.UnlockedBlueprintIds),
                [CloudUnlockedShipsKey] = SerializeShipUnlocks(CurrentProfile.UnlockedShipIds),
                [CloudAvengerTheftAttemptKey] = SerializeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt),
                [CloudViperRecoveryProgressKey] = SerializeViperRecoveryProgress(CurrentProfile.ViperRecoveryProgress),
                [CloudArrowLicenseProgressKey] = SerializeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress),
                [CloudBisonIndustrialPartsDeliveredKey] = CurrentProfile.BisonIndustrialPartsDelivered,
                [CloudInvaderImprintsRecoveredKey] = CurrentProfile.InvaderImprintsRecovered,
                [CloudPathfinderResearchProgressKey] = SerializePathfinderResearchProgress(CurrentProfile.PathfinderResearchProgress),
                [CloudMissEnigmaPurchasedBlueprintsKey] = SerializeMissEnigmaBlueprintPurchases(CurrentProfile.MissEnigmaPurchasedBlueprintIds),
                [CloudMissEnigmaRecoverableUniqueItemsKey] = SerializeMissEnigmaUniqueItemRecoveries(CurrentProfile.MissEnigmaRecoverableUniqueItemIds),
                [CloudPilotDroneKillsKey] = CurrentProfile.PilotDroneKills,
                [CloudPilotSoldItemsAstronsKey] = CurrentProfile.PilotSoldItemsAstrons,
                [CloudPilotPirateBayReturnsKey] = CurrentProfile.PilotPirateBayReturns,
                [CloudPilotAsteroidSalvageCountKey] = CurrentProfile.PilotAsteroidSalvageCount,
                [CloudPilotAshOverloadReturnsKey] = CurrentProfile.PilotAshOverloadReturns,
                [CloudPilotAtlasMapReturnsKey] = SerializeAtlasMapReturns(CurrentProfile.PilotAtlasMapReturns),
                [CloudMapUnlockProgressKey] = SerializeMapUnlockProgress(CurrentProfile.MapUnlockProgress),
                [CloudProjectsKey] = SerializeProjectProgress(CurrentProfile.ProjectProgress),
                [CloudCareerStatsKey] = SerializeCareerStats(CurrentProfile.CareerStats)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "reset account");
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService account reset failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    string SanitizeNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return BuildFallbackProfile().Nickname;

        string trimmed = nickname.Trim();
        if (trimmed.Length > 18)
            trimmed = trimmed.Substring(0, 18);

        return trimmed;
    }

    void NotifyProfileChanged()
    {
        ProfileChanged?.Invoke(CurrentProfile);
    }

}
