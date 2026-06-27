using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.Core;
using UnityEngine;

public partial class PlayerProfileService
{
    const int CloudRetryCount = 3;
    const int CloudRetryDelayMs = 1200;

    const string CloudNicknameKey = "profile_nickname";
    const string CloudShipSkinKey = "profile_ship_skin";
    const string CloudGamesPlayedKey = "profile_games_played";
    const string CloudTotalXpKey = "profile_total_xp";
    const string CloudAstronsKey = "profile_astrons";
    const string CloudInventoryKey = "profile_inventory";
    const string CloudSelectedPilotKey = "profile_selected_pilot";
    const string CloudUnlockedPilotsKey = "profile_unlocked_pilots";
    const string CloudUnlockedBlueprintsKey = "profile_unlocked_blueprints";
    const string CloudUnlockedShipsKey = "profile_unlocked_ships";
    const string CloudAvengerTheftAttemptKey = "profile_avenger_theft_attempt";
    const string CloudViperRecoveryProgressKey = "profile_viper_recovery_progress";
    const string CloudArrowLicenseProgressKey = "profile_arrow_license_progress";
    const string CloudBisonIndustrialPartsDeliveredKey = "profile_bison_industrial_parts_delivered";
    const string CloudInvaderImprintsRecoveredKey = "profile_invader_imprints_recovered";
    const string CloudPathfinderResearchProgressKey = "profile_pathfinder_research_progress";
    const string CloudMissEnigmaPurchasedBlueprintsKey = "profile_miss_enigma_purchased_blueprints";
    const string CloudMissEnigmaRecoverableUniqueItemsKey = "profile_miss_enigma_recoverable_unique_items";
    const string CloudPilotDroneKillsKey = "profile_pilot_drone_kills";
    const string CloudPilotSoldItemsAstronsKey = "profile_pilot_sold_items_astrons";
    const string CloudPilotPirateBayReturnsKey = "profile_pilot_pirate_bay_returns";
    const string CloudPilotAsteroidSalvageCountKey = "profile_pilot_asteroid_salvage_count";
    const string CloudPilotAshOverloadReturnsKey = "profile_pilot_ash_overload_returns";
    const string CloudPilotAtlasMapReturnsKey = "profile_pilot_atlas_map_returns";
    const string CloudMapUnlockProgressKey = "profile_map_unlock_progress";
    const string CloudProjectsKey = "profile_projects";
    const string CloudCareerStatsKey = "profile_career_stats";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("PlayerProfileService");
        instance = root.AddComponent<PlayerProfileService>();
        DontDestroyOnLoad(root);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    async void Start()
    {
        await EnsureInitializedAsync();
    }

    public Task EnsureInitializedAsync()
    {
        initializationTask ??= InitializeInternalAsync();
        return initializationTask;
    }

    async Task InitializeInternalAsync()
    {
        try
        {
            IsBusy = true;

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await StarjackersUnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            await LoadProfileAsync();
            initialized = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("PlayerProfileService init fallback: " + ex.Message);
            CurrentProfile = BuildFallbackProfile();
            ApplyProfileToPhoton();
            NotifyProfileChanged();
            initialized = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task LoadProfileAsync()
    {
        var keys = new HashSet<string>
        {
            CloudNicknameKey,
            CloudShipSkinKey,
            CloudGamesPlayedKey,
            CloudTotalXpKey,
            CloudAstronsKey,
            CloudInventoryKey,
            CloudSelectedPilotKey,
            CloudUnlockedPilotsKey,
            CloudUnlockedBlueprintsKey,
            CloudUnlockedShipsKey,
            CloudAvengerTheftAttemptKey,
            CloudViperRecoveryProgressKey,
            CloudArrowLicenseProgressKey,
            CloudBisonIndustrialPartsDeliveredKey,
            CloudInvaderImprintsRecoveredKey,
            CloudPathfinderResearchProgressKey,
            CloudMissEnigmaPurchasedBlueprintsKey,
            CloudMissEnigmaRecoverableUniqueItemsKey,
            CloudPilotDroneKillsKey,
            CloudPilotSoldItemsAstronsKey,
            CloudPilotPirateBayReturnsKey,
            CloudPilotAsteroidSalvageCountKey,
            CloudPilotAshOverloadReturnsKey,
            CloudPilotAtlasMapReturnsKey,
            CloudMapUnlockProgressKey,
            CloudProjectsKey,
            CloudCareerStatsKey
        };
        Dictionary<string, Item> data = await RunCloudOperationWithRetryAsync(
            () => CloudSaveService.Instance.Data.Player.LoadAsync(keys),
            "load profile");

        string nickname = null;
        int shipSkinIndex = 0;
        int gamesPlayed = 0;
        int totalXp = 0;
        int astrons = DefaultStartingAstrons;
        PlayerInventoryData inventory = PlayerInventoryData.Default();
        string selectedPilotId = PilotCatalog.JakeId;
        string[] unlockedPilotIds = PilotCatalog.GetDefaultUnlockedPilotIds();
        string[] unlockedBlueprintIds = BlueprintCatalog.GetStarterUnlockedBlueprintItemIds();
        string[] unlockedShipIds = ShipCatalog.GetDefaultUnlockedShipTypeIds();
        AvengerTheftAttemptData avengerTheftAttempt = AvengerTheftAttemptData.Empty();
        ViperRecoveryProgressData viperRecoveryProgress = ViperRecoveryProgressData.Empty();
        ArrowLicenseProgressData arrowLicenseProgress = ArrowLicenseProgressData.Empty();
        int bisonIndustrialPartsDelivered = 0;
        int invaderImprintsRecovered = 0;
        PathfinderResearchProgressData pathfinderResearchProgress = PathfinderResearchProgressData.Empty();
        string[] missEnigmaPurchasedBlueprintIds = Array.Empty<string>();
        string[] missEnigmaRecoverableUniqueItemIds = Array.Empty<string>();
        int pilotDroneKills = 0;
        int pilotSoldItemsAstrons = 0;
        int pilotPirateBayReturns = 0;
        int pilotAsteroidSalvageCount = 0;
        int pilotAshOverloadReturns = 0;
        string[] pilotAtlasMapReturns = Array.Empty<string>();
        PlayerMapUnlockProgressData mapUnlockProgress = NormalizeMapUnlockProgress(null);
        PlayerProjectProgressData projectProgress = ProjectCatalog.NormalizeProgress(null);
        PlayerCareerStatsData careerStats = PlayerCareerStatsData.Empty();

        if (data != null)
        {
            if (data.TryGetValue(CloudNicknameKey, out Item nicknameItem) && nicknameItem?.Value != null)
                nickname = nicknameItem.Value.GetAsString();

            if (data.TryGetValue(CloudShipSkinKey, out Item skinItem) && skinItem?.Value != null)
                shipSkinIndex = Mathf.Clamp(skinItem.Value.GetAs<int>(), 0, ShipCatalog.MaxShipSkinIndex);

            if (data.TryGetValue(CloudGamesPlayedKey, out Item gamesItem) && gamesItem?.Value != null)
                gamesPlayed = Mathf.Max(0, gamesItem.Value.GetAs<int>());

            if (data.TryGetValue(CloudTotalXpKey, out Item totalXpItem) && totalXpItem?.Value != null)
                totalXp = Mathf.Max(0, totalXpItem.Value.GetAs<int>());

            if (data.TryGetValue(CloudAstronsKey, out Item astronsItem) && astronsItem?.Value != null)
                astrons = Mathf.Max(0, astronsItem.Value.GetAs<int>());

            if (data.TryGetValue(CloudInventoryKey, out Item inventoryItem) && inventoryItem?.Value != null)
                inventory = DeserializeInventory(inventoryItem.Value.GetAsString());

            if (data.TryGetValue(CloudSelectedPilotKey, out Item selectedPilotItem) && selectedPilotItem?.Value != null)
                selectedPilotId = PilotCatalog.NormalizePilotId(selectedPilotItem.Value.GetAsString());

            if (data.TryGetValue(CloudUnlockedPilotsKey, out Item unlockedPilotsItem) && unlockedPilotsItem?.Value != null)
                unlockedPilotIds = DeserializePilotUnlocks(unlockedPilotsItem.Value.GetAsString());

            if (data.TryGetValue(CloudUnlockedBlueprintsKey, out Item unlockedBlueprintsItem) && unlockedBlueprintsItem?.Value != null)
                unlockedBlueprintIds = DeserializeBlueprintUnlocks(unlockedBlueprintsItem.Value.GetAsString());

            if (data.TryGetValue(CloudUnlockedShipsKey, out Item unlockedShipsItem) && unlockedShipsItem?.Value != null)
                unlockedShipIds = DeserializeShipUnlocks(unlockedShipsItem.Value.GetAsString());

            if (data.TryGetValue(CloudAvengerTheftAttemptKey, out Item avengerTheftAttemptItem) && avengerTheftAttemptItem?.Value != null)
                avengerTheftAttempt = DeserializeAvengerTheftAttempt(avengerTheftAttemptItem.Value.GetAsString());

            if (data.TryGetValue(CloudViperRecoveryProgressKey, out Item viperRecoveryProgressItem) && viperRecoveryProgressItem?.Value != null)
                viperRecoveryProgress = DeserializeViperRecoveryProgress(viperRecoveryProgressItem.Value.GetAsString(), unlockedShipIds);

            if (data.TryGetValue(CloudArrowLicenseProgressKey, out Item arrowLicenseProgressItem) && arrowLicenseProgressItem?.Value != null)
                arrowLicenseProgress = DeserializeArrowLicenseProgress(arrowLicenseProgressItem.Value.GetAsString(), unlockedShipIds);

            if (data.TryGetValue(CloudBisonIndustrialPartsDeliveredKey, out Item bisonIndustrialPartsItem) && bisonIndustrialPartsItem?.Value != null)
                bisonIndustrialPartsDelivered = bisonIndustrialPartsItem.Value.GetAs<int>();

            if (data.TryGetValue(CloudInvaderImprintsRecoveredKey, out Item invaderImprintsItem) && invaderImprintsItem?.Value != null)
                invaderImprintsRecovered = invaderImprintsItem.Value.GetAs<int>();

            if (data.TryGetValue(CloudPathfinderResearchProgressKey, out Item pathfinderResearchItem) && pathfinderResearchItem?.Value != null)
                pathfinderResearchProgress = DeserializePathfinderResearchProgress(pathfinderResearchItem.Value.GetAsString(), unlockedShipIds);

            if (data.TryGetValue(CloudMissEnigmaPurchasedBlueprintsKey, out Item missEnigmaPurchasedBlueprintsItem) && missEnigmaPurchasedBlueprintsItem?.Value != null)
                missEnigmaPurchasedBlueprintIds = DeserializeMissEnigmaBlueprintPurchases(missEnigmaPurchasedBlueprintsItem.Value.GetAsString());

            if (data.TryGetValue(CloudMissEnigmaRecoverableUniqueItemsKey, out Item missEnigmaRecoverableUniqueItemsItem) && missEnigmaRecoverableUniqueItemsItem?.Value != null)
                missEnigmaRecoverableUniqueItemIds = DeserializeMissEnigmaUniqueItemRecoveries(missEnigmaRecoverableUniqueItemsItem.Value.GetAsString());

            if (data.TryGetValue(CloudPilotDroneKillsKey, out Item droneKillsItem) && droneKillsItem?.Value != null)
                pilotDroneKills = Mathf.Max(0, droneKillsItem.Value.GetAs<int>());

            if (data.TryGetValue(CloudPilotSoldItemsAstronsKey, out Item soldItemsAstronsItem) && soldItemsAstronsItem?.Value != null)
                pilotSoldItemsAstrons = Mathf.Max(0, soldItemsAstronsItem.Value.GetAs<int>());

            if (data.TryGetValue(CloudPilotPirateBayReturnsKey, out Item pirateBayReturnsItem) && pirateBayReturnsItem?.Value != null)
                pilotPirateBayReturns = Mathf.Max(0, pirateBayReturnsItem.Value.GetAs<int>());

            if (data.TryGetValue(CloudPilotAsteroidSalvageCountKey, out Item asteroidSalvageItem) && asteroidSalvageItem?.Value != null)
                pilotAsteroidSalvageCount = Mathf.Max(0, asteroidSalvageItem.Value.GetAs<int>());

            if (data.TryGetValue(CloudPilotAshOverloadReturnsKey, out Item ashOverloadReturnsItem) && ashOverloadReturnsItem?.Value != null)
                pilotAshOverloadReturns = Mathf.Max(0, ashOverloadReturnsItem.Value.GetAs<int>());

            if (data.TryGetValue(CloudPilotAtlasMapReturnsKey, out Item atlasMapReturnsItem) && atlasMapReturnsItem?.Value != null)
                pilotAtlasMapReturns = DeserializeAtlasMapReturns(atlasMapReturnsItem.Value.GetAsString());

            if (data.TryGetValue(CloudMapUnlockProgressKey, out Item mapUnlockProgressItem) && mapUnlockProgressItem?.Value != null)
                mapUnlockProgress = DeserializeMapUnlockProgress(mapUnlockProgressItem.Value.GetAsString());

            if (data.TryGetValue(CloudProjectsKey, out Item projectsItem) && projectsItem?.Value != null)
                projectProgress = DeserializeProjectProgress(projectsItem.Value.GetAsString());

            if (data.TryGetValue(CloudCareerStatsKey, out Item careerStatsItem) && careerStatsItem?.Value != null)
                careerStats = DeserializeCareerStats(careerStatsItem.Value.GetAsString());
        }

        if (string.IsNullOrWhiteSpace(nickname))
            nickname = BuildFallbackProfile().Nickname;

        CurrentProfile = new PlayerProfileData
        {
            Nickname = SanitizeNickname(nickname),
            ShipSkinIndex = Mathf.Clamp(shipSkinIndex, 0, ShipCatalog.MaxShipSkinIndex),
            GamesPlayed = gamesPlayed,
            TotalXp = totalXp,
            Astrons = astrons,
            Inventory = inventory,
            SelectedPilotId = selectedPilotId,
            UnlockedPilotIds = PilotCatalog.NormalizeUnlockedPilotIds(unlockedPilotIds),
            UnlockedBlueprintIds = NormalizeUnlockedBlueprintIds(unlockedBlueprintIds),
            UnlockedShipIds = NormalizeUnlockedShipIds(unlockedShipIds),
            AvengerTheftAttempt = NormalizeAvengerTheftAttempt(avengerTheftAttempt),
            ViperRecoveryProgress = NormalizeViperRecoveryProgress(viperRecoveryProgress, unlockedShipIds),
            ArrowLicenseProgress = NormalizeArrowLicenseProgress(arrowLicenseProgress, unlockedShipIds),
            BisonIndustrialPartsDelivered = NormalizeBisonIndustrialPartsDelivered(bisonIndustrialPartsDelivered, unlockedShipIds),
            InvaderImprintsRecovered = NormalizeInvaderImprintsRecovered(invaderImprintsRecovered, unlockedShipIds),
            PathfinderResearchProgress = NormalizePathfinderResearchProgress(pathfinderResearchProgress, unlockedShipIds),
            MissEnigmaPurchasedBlueprintIds = NormalizeMissEnigmaBlueprintPurchases(missEnigmaPurchasedBlueprintIds),
            MissEnigmaRecoverableUniqueItemIds = NormalizeMissEnigmaUniqueItemRecoveries(missEnigmaRecoverableUniqueItemIds),
            PilotDroneKills = pilotDroneKills,
            PilotSoldItemsAstrons = pilotSoldItemsAstrons,
            PilotPirateBayReturns = pilotPirateBayReturns,
            PilotAsteroidSalvageCount = pilotAsteroidSalvageCount,
            PilotAshOverloadReturns = pilotAshOverloadReturns,
            PilotAtlasMapReturns = PilotCatalog.NormalizeAtlasMapReturnIds(pilotAtlasMapReturns),
            MapUnlockProgress = NormalizeMapUnlockProgress(mapUnlockProgress),
            ProjectProgress = ProjectCatalog.NormalizeProgress(projectProgress),
            CareerStats = NormalizeCareerStats(careerStats)
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


    public async Task SaveCurrentProfileToCloudAsync()
    {
        await EnsureInitializedAsync();

        try
        {
            IsBusy = true;
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
                "save profile");
        }
        finally
        {
            IsBusy = false;
        }
    }


    string TryGetAuthenticationPlayerId()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            return string.Empty;

        try
        {
            return AuthenticationService.Instance != null ? AuthenticationService.Instance.PlayerId : string.Empty;
        }
        catch (ServicesInitializationException)
        {
            return string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }


    async Task<T> RunCloudOperationWithRetryAsync<T>(Func<Task<T>> operation, string operationName)
    {
        Exception lastException = null;
        for (int attempt = 1; attempt <= CloudRetryCount; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt >= CloudRetryCount)
                    break;

                Debug.LogWarning("PlayerProfileService " + operationName + " retry " + attempt + "/" + CloudRetryCount + ": " + ex.Message);
                await Task.Delay(CloudRetryDelayMs);
            }
        }

        throw lastException ?? new Exception("Cloud operation failed: " + operationName);
    }

    async Task RunCloudOperationWithRetryAsync(Func<Task> operation, string operationName)
    {
        Exception lastException = null;
        for (int attempt = 1; attempt <= CloudRetryCount; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt >= CloudRetryCount)
                    break;

                Debug.LogWarning("PlayerProfileService " + operationName + " retry " + attempt + "/" + CloudRetryCount + ": " + ex.Message);
                await Task.Delay(CloudRetryDelayMs);
            }
        }

        throw lastException ?? new Exception("Cloud operation failed: " + operationName);
    }

}
