using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.Core;
using UnityEngine;

public class PlayerProfileService : MonoBehaviour
{
    const int CloudRetryCount = 3;
    const int CloudRetryDelayMs = 1200;
    const int DeferredInventorySaveDelayMs = 750;
    const int PlayerInventoryExtendBasePrice = 1000;
    const int PlayerInventoryExtendMaxPrice = 64000;
    public const int DefaultStartingAstrons = 3000;
    public const int DefaultAstronautCargoSlotCount = 1;
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
    const string CloudMissEnigmaPurchasedBlueprintsKey = "profile_miss_enigma_purchased_blueprints";
    const string CloudPilotDroneKillsKey = "profile_pilot_drone_kills";
    const string CloudPilotSoldItemsAstronsKey = "profile_pilot_sold_items_astrons";
    const string CloudPilotPirateBayReturnsKey = "profile_pilot_pirate_bay_returns";
    const string CloudPilotAsteroidSalvageCountKey = "profile_pilot_asteroid_salvage_count";
    const string CloudPilotAshOverloadReturnsKey = "profile_pilot_ash_overload_returns";
    const string CloudPilotAtlasMapReturnsKey = "profile_pilot_atlas_map_returns";
    const string CloudMapUnlockProgressKey = "profile_map_unlock_progress";
    const string CloudProjectsKey = "profile_projects";
    const string PlayerViperCargoUnlockedKey = "profile_runtime_viper_cargo_unlocked";
    const string PlayerViperRecoveryStageKey = "profile_runtime_viper_recovery_stage";
    const string PlayerArrowLicenseStageKey = "profile_runtime_arrow_license_stage";
    const string PlayerArrowFinalRunReadyKey = "profile_runtime_arrow_final_ready";
    const string PlayerBisonIndustrialPartsDeliveredKey = "profile_runtime_bison_parts_delivered";
    const string PlayerInvaderImprintsRecoveredKey = "profile_runtime_invader_imprints";

    public const int ViperNeutralFighterWrecksRequired = 8;
    public const int ViperDroneWrecksRequired = 4;
    public const int ViperSpaceTruckWrecksRequired = 2;
    public const float ViperMinimumTestFlightSeconds = 60f;
    public const int ViperTestFlightSubsystemUnlocksPerReturn = 2;
    public const string ViperCargoSubsystemId = "cargo";
    public const int ArrowQualifierChipsRequired = 2;
    public const int ArrowMapRacesRequired = 3;
    public const int ArrowTimeTrialMinimumRank = (int)ArrowTimeTrialRank.B;
    public const string ArrowIonNozzlePartId = "ion_nozzle";
    public const string ArrowGyroStabilizerPartId = "gyro_stabilizer";
    public const string ArrowRaceTransponderPartId = "race_transponder";
    public const int BisonIndustrialPartsRequired = 6;
    public const int InvaderImprintsRequired = 4;

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
                await UnityServices.InitializeAsync();
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
            CloudMissEnigmaPurchasedBlueprintsKey,
            CloudPilotDroneKillsKey,
            CloudPilotSoldItemsAstronsKey,
            CloudPilotPirateBayReturnsKey,
            CloudPilotAsteroidSalvageCountKey,
            CloudPilotAshOverloadReturnsKey,
            CloudPilotAtlasMapReturnsKey,
            CloudMapUnlockProgressKey,
            CloudProjectsKey
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
        string[] missEnigmaPurchasedBlueprintIds = Array.Empty<string>();
        int pilotDroneKills = 0;
        int pilotSoldItemsAstrons = 0;
        int pilotPirateBayReturns = 0;
        int pilotAsteroidSalvageCount = 0;
        int pilotAshOverloadReturns = 0;
        string[] pilotAtlasMapReturns = Array.Empty<string>();
        PlayerMapUnlockProgressData mapUnlockProgress = NormalizeMapUnlockProgress(null);
        PlayerProjectProgressData projectProgress = ProjectCatalog.NormalizeProgress(null);

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

            if (data.TryGetValue(CloudMissEnigmaPurchasedBlueprintsKey, out Item missEnigmaPurchasedBlueprintsItem) && missEnigmaPurchasedBlueprintsItem?.Value != null)
                missEnigmaPurchasedBlueprintIds = DeserializeMissEnigmaBlueprintPurchases(missEnigmaPurchasedBlueprintsItem.Value.GetAsString());

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
            MissEnigmaPurchasedBlueprintIds = NormalizeMissEnigmaBlueprintPurchases(missEnigmaPurchasedBlueprintIds),
            PilotDroneKills = pilotDroneKills,
            PilotSoldItemsAstrons = pilotSoldItemsAstrons,
            PilotPirateBayReturns = pilotPirateBayReturns,
            PilotAsteroidSalvageCount = pilotAsteroidSalvageCount,
            PilotAshOverloadReturns = pilotAshOverloadReturns,
            PilotAtlasMapReturns = PilotCatalog.NormalizeAtlasMapReturnIds(pilotAtlasMapReturns),
            MapUnlockProgress = NormalizeMapUnlockProgress(mapUnlockProgress),
            ProjectProgress = ProjectCatalog.NormalizeProgress(projectProgress)
        };

        EnsurePilotDefaults();
        EnsureShipUnlocks();
        EnsureBlueprintUnlocks();
        EnsureMissEnigmaBlueprintPurchases();
        EnsureMapUnlockProgress();
        EnsureProjectProgress();

        ApplyProfileToPhoton();
        NotifyProfileChanged();
    }

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
            MissEnigmaPurchasedBlueprintIds = CurrentProfile != null ? NormalizeMissEnigmaBlueprintPurchases(CurrentProfile.MissEnigmaPurchasedBlueprintIds) : Array.Empty<string>(),
            PilotDroneKills = CurrentProfile != null ? Mathf.Max(0, CurrentProfile.PilotDroneKills) : 0,
            PilotSoldItemsAstrons = CurrentProfile != null ? Mathf.Max(0, CurrentProfile.PilotSoldItemsAstrons) : 0,
            PilotPirateBayReturns = CurrentProfile != null ? Mathf.Max(0, CurrentProfile.PilotPirateBayReturns) : 0,
            PilotAsteroidSalvageCount = CurrentProfile != null ? Mathf.Max(0, CurrentProfile.PilotAsteroidSalvageCount) : 0,
            PilotAshOverloadReturns = CurrentProfile != null ? Mathf.Max(0, CurrentProfile.PilotAshOverloadReturns) : 0,
            PilotAtlasMapReturns = CurrentProfile != null ? PilotCatalog.NormalizeAtlasMapReturnIds(CurrentProfile.PilotAtlasMapReturns) : Array.Empty<string>(),
            MapUnlockProgress = CurrentProfile != null ? NormalizeMapUnlockProgress(CurrentProfile.MapUnlockProgress) : NormalizeMapUnlockProgress(null),
            ProjectProgress = CurrentProfile != null ? ProjectCatalog.NormalizeProgress(CurrentProfile.ProjectProgress) : ProjectCatalog.NormalizeProgress(null)
        };

        EnsurePilotDefaults();
        EnsureShipUnlocks();
        EnsureBlueprintUnlocks();
        EnsureMissEnigmaBlueprintPurchases();
        EnsureMapUnlockProgress();
        EnsureProjectProgress();
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
            EnsureMapUnlockProgress();
            EnsureProjectProgress();

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
                [CloudMissEnigmaPurchasedBlueprintsKey] = SerializeMissEnigmaBlueprintPurchases(CurrentProfile.MissEnigmaPurchasedBlueprintIds),
                [CloudPilotDroneKillsKey] = CurrentProfile.PilotDroneKills,
                [CloudPilotSoldItemsAstronsKey] = CurrentProfile.PilotSoldItemsAstrons,
                [CloudPilotPirateBayReturnsKey] = CurrentProfile.PilotPirateBayReturns,
                [CloudPilotAsteroidSalvageCountKey] = CurrentProfile.PilotAsteroidSalvageCount,
                [CloudPilotAshOverloadReturnsKey] = CurrentProfile.PilotAshOverloadReturns,
                [CloudPilotAtlasMapReturnsKey] = SerializeAtlasMapReturns(CurrentProfile.PilotAtlasMapReturns),
                [CloudMapUnlockProgressKey] = SerializeMapUnlockProgress(CurrentProfile.MapUnlockProgress),
                [CloudProjectsKey] = SerializeProjectProgress(CurrentProfile.ProjectProgress)
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

    public async Task RecordGameStartedAsync()
    {
        await EnsureInitializedAsync();

        try
        {
            IsBusy = true;
            ClearPendingAstronautCargo();
            CurrentProfile.GamesPlayed = Mathf.Max(0, CurrentProfile.GamesPlayed + 1);

            var data = new Dictionary<string, object>
            {
                [CloudGamesPlayedKey] = CurrentProfile.GamesPlayed
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save games played");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService games played update failed: " + ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RecordRoundXpAsync(int roundXp, string matchToken)
    {
        await EnsureInitializedAsync();

        int normalizedXp = Mathf.Max(0, roundXp);
        if (normalizedXp <= 0)
            return;

        string token = string.IsNullOrWhiteSpace(matchToken)
            ? "match_" + DateTime.UtcNow.Ticks
            : matchToken;

        if (awardedMatchTokens.Contains(token))
            return;

        try
        {
            IsBusy = true;
            awardedMatchTokens.Add(token);
            CurrentProfile.TotalXp = Mathf.Max(0, CurrentProfile.TotalXp + normalizedXp);

            var data = new Dictionary<string, object>
            {
                [CloudTotalXpKey] = CurrentProfile.TotalXp
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save round xp");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            awardedMatchTokens.Remove(token);
            Debug.LogError("PlayerProfileService XP save failed: " + ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public LobbyMapUnlockStatus GetMapUnlockStatus(string mapId)
    {
        EnsureMapUnlockProgress();
        int totalXp = CurrentProfile != null ? CurrentProfile.TotalXp : 0;
        return LobbyMapUnlockCatalog.GetStatus(mapId, CurrentProfile != null ? CurrentProfile.MapUnlockProgress : null, totalXp);
    }

    public bool IsMapUnlocked(string mapId)
    {
        LobbyMapUnlockStatus status = GetMapUnlockStatus(mapId);
        return status != null && status.IsUnlocked;
    }

    public int GetMapSuccessfulReturnCount(string mapId)
    {
        EnsureMapUnlockProgress();
        return LobbyMapUnlockCatalog.GetReturnCount(CurrentProfile != null ? CurrentProfile.MapUnlockProgress : null, mapId);
    }

    public async Task<int> RecordMapSuccessfulReturnAsync(string mapId, string outcome, string matchToken)
    {
        await EnsureInitializedAsync();
        EnsureMapUnlockProgress();

        string normalizedMapId = LobbyMapUnlockCatalog.NormalizeMapId(mapId);
        if (string.IsNullOrWhiteSpace(normalizedMapId))
            return 0;

        if (!IsSuccessfulMapReturnOutcome(outcome))
            return LobbyMapUnlockCatalog.GetReturnCount(CurrentProfile.MapUnlockProgress, normalizedMapId);

        string token = BuildMapProgressToken(normalizedMapId, matchToken);
        if (awardedMapReturnTokens.Contains(token))
            return LobbyMapUnlockCatalog.GetReturnCount(CurrentProfile.MapUnlockProgress, normalizedMapId);

        PlayerMapUnlockProgressData previousProgress = CurrentProfile.MapUnlockProgress.Clone();
        int previousCount = LobbyMapUnlockCatalog.GetReturnCount(CurrentProfile.MapUnlockProgress, normalizedMapId);
        long updatedCount = (long)previousCount + 1;
        CurrentProfile.MapUnlockProgress.SetReturnCount(normalizedMapId, updatedCount > int.MaxValue ? int.MaxValue : (int)updatedCount);
        CurrentProfile.MapUnlockProgress = NormalizeMapUnlockProgress(CurrentProfile.MapUnlockProgress);
        awardedMapReturnTokens.Add(token);

        try
        {
            await SaveMapUnlockProgressAsync("save map return progress");
        }
        catch (Exception ex)
        {
            CurrentProfile.MapUnlockProgress = previousProgress;
            awardedMapReturnTokens.Remove(token);
            Debug.LogError("PlayerProfileService map return progress save failed: " + ex);
            throw;
        }

        return LobbyMapUnlockCatalog.GetReturnCount(CurrentProfile.MapUnlockProgress, normalizedMapId);
    }

    public async Task RecordMothershipKillAsync()
    {
        await EnsureInitializedAsync();
        EnsureMapUnlockProgress();

        if (CurrentProfile.MapUnlockProgress.MothershipKilled)
            return;

        PlayerMapUnlockProgressData previousProgress = CurrentProfile.MapUnlockProgress.Clone();
        CurrentProfile.MapUnlockProgress.MothershipKilled = true;

        try
        {
            await SaveMapUnlockProgressAsync("save Mothership map unlock progress");
        }
        catch (Exception ex)
        {
            CurrentProfile.MapUnlockProgress = previousProgress;
            Debug.LogError("PlayerProfileService Mothership progress save failed: " + ex);
            throw;
        }
    }

    public async Task UnlockAllMapsAsync()
    {
        await EnsureInitializedAsync();
        EnsureMapUnlockProgress();

        if (CurrentProfile.MapUnlockProgress.CheatUnlockAllMaps)
            return;

        PlayerMapUnlockProgressData previousProgress = CurrentProfile.MapUnlockProgress.Clone();
        CurrentProfile.MapUnlockProgress.CheatUnlockAllMaps = true;

        try
        {
            await SaveMapUnlockProgressAsync("save unlock all maps cheat");
        }
        catch (Exception ex)
        {
            CurrentProfile.MapUnlockProgress = previousProgress;
            Debug.LogError("PlayerProfileService unlock all maps cheat failed: " + ex);
            throw;
        }
    }

    public async Task LockAllMapsAsync()
    {
        await EnsureInitializedAsync();

        PlayerMapUnlockProgressData previousProgress = CurrentProfile.MapUnlockProgress != null
            ? CurrentProfile.MapUnlockProgress.Clone()
            : NormalizeMapUnlockProgress(null);
        CurrentProfile.MapUnlockProgress = NormalizeMapUnlockProgress(null);
        awardedMapReturnTokens.Clear();

        try
        {
            await SaveMapUnlockProgressAsync("save lock all maps cheat");
        }
        catch (Exception ex)
        {
            CurrentProfile.MapUnlockProgress = previousProgress;
            Debug.LogError("PlayerProfileService lock all maps cheat failed: " + ex);
            throw;
        }
    }

    async Task SaveMapUnlockProgressAsync(string operationName)
    {
        try
        {
            IsBusy = true;
            EnsureMapUnlockProgress();

            var data = new Dictionary<string, object>
            {
                [CloudMapUnlockProgressKey] = SerializeMapUnlockProgress(CurrentProfile.MapUnlockProgress)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                operationName);
            NotifyProfileChanged();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddCheatXpAsync(int amount)
    {
        await EnsureInitializedAsync();

        int normalizedXp = Mathf.Max(0, amount);
        if (normalizedXp <= 0)
            return;

        try
        {
            IsBusy = true;
            CurrentProfile.TotalXp = Mathf.Max(0, CurrentProfile.TotalXp + normalizedXp);

            var data = new Dictionary<string, object>
            {
                [CloudTotalXpKey] = CurrentProfile.TotalXp
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save cheat xp");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService cheat XP save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
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
                MissEnigmaPurchasedBlueprintIds = Array.Empty<string>(),
                PilotDroneKills = 0,
                PilotSoldItemsAstrons = 0,
                PilotPirateBayReturns = 0,
                PilotAsteroidSalvageCount = 0,
                PilotAshOverloadReturns = 0,
                PilotAtlasMapReturns = Array.Empty<string>(),
                MapUnlockProgress = NormalizeMapUnlockProgress(null),
                ProjectProgress = ProjectCatalog.NormalizeProgress(null)
            };

            awardedMatchTokens.Clear();
            awardedMapReturnTokens.Clear();
            EnsureInventory();
            EnsurePilotDefaults();
            EnsureShipUnlocks();
            EnsureBlueprintUnlocks();
            EnsureMissEnigmaBlueprintPurchases();
            EnsureMapUnlockProgress();
            EnsureProjectProgress();

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
                [CloudMissEnigmaPurchasedBlueprintsKey] = SerializeMissEnigmaBlueprintPurchases(CurrentProfile.MissEnigmaPurchasedBlueprintIds),
                [CloudPilotDroneKillsKey] = CurrentProfile.PilotDroneKills,
                [CloudPilotSoldItemsAstronsKey] = CurrentProfile.PilotSoldItemsAstrons,
                [CloudPilotPirateBayReturnsKey] = CurrentProfile.PilotPirateBayReturns,
                [CloudPilotAsteroidSalvageCountKey] = CurrentProfile.PilotAsteroidSalvageCount,
                [CloudPilotAshOverloadReturnsKey] = CurrentProfile.PilotAshOverloadReturns,
                [CloudPilotAtlasMapReturnsKey] = SerializeAtlasMapReturns(CurrentProfile.PilotAtlasMapReturns),
                [CloudMapUnlockProgressKey] = SerializeMapUnlockProgress(CurrentProfile.MapUnlockProgress),
                [CloudProjectsKey] = SerializeProjectProgress(CurrentProfile.ProjectProgress)
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

    public bool HasFreeShipInventorySlot(string itemId = null)
    {
        EnsureInventory();
        return CurrentProfile.Inventory.GetFirstEmptyShipSlot(GetActiveShipInventoryCapacity(), GetActiveShipSkinIndex(), itemId) >= 0;
    }

    public bool HasFreePlayerInventorySlot()
    {
        EnsureInventory();
        return CurrentProfile.Inventory.GetFirstEmptyPlayerSlot() >= 0;
    }

    public async Task ReplaceShipInventoryAsync(string[] newShipSlots)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        CurrentProfile.Inventory.SetShipSlots(newShipSlots);
        TryMoveSafePocketRestrictedItems(CurrentProfile.Inventory, GetActiveShipSkinIndex());
        await SaveInventoryOnlyAsync();
    }

    public async Task ApplyShipLossAsync(int shipSkinIndex, bool loseShipInventory, bool loseEquipment, string serializedAstronautCargo = null, string protectedEquipmentItemId = null, bool deferSave = false)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        bool changed = false;
        if (loseShipInventory)
        {
            int shipCapacity = GetActiveShipInventoryCapacity();
            StagePendingAstronautCargo(serializedAstronautCargo, CurrentProfile.Inventory.ShipSlots, shipSkinIndex, shipCapacity);
            CurrentProfile.Inventory.SetShipSlots(BuildPostLossShipInventory(CurrentProfile.Inventory.ShipSlots, shipSkinIndex));
            changed = true;
        }
        else
        {
            ClearPendingAstronautCargo();
        }

        if (loseEquipment)
        {
            StagePendingProtectedEquipment(protectedEquipmentItemId, CurrentProfile.Inventory.EquipmentSlots, shipSkinIndex);
            CurrentProfile.Inventory.EquipmentSlots = BuildPostLossEquipmentInventory(CurrentProfile.Inventory.EquipmentSlots, shipSkinIndex, ShouldPreserveEngineEquipmentOnLoss());
            changed = true;
        }
        else
        {
            ClearPendingProtectedEquipment();
        }

        if (!changed)
            return;

        if (deferSave)
        {
            MarkInventoryChangedDeferred();
        }
        else
        {
            await SaveInventoryOnlyAsync();
        }
    }

    public async Task<bool> RestorePendingAstronautCargoAsync()
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int shipSkinIndex = pendingAstronautCargoShipSkinIndex >= 0
            ? pendingAstronautCargoShipSkinIndex
            : pendingProtectedEquipmentShipSkinIndex >= 0
                ? pendingProtectedEquipmentShipSkinIndex
                : GetActiveShipSkinIndex();
        CurrentProfile.Inventory.SetShipSlots(BuildPostLossShipInventory(CurrentProfile.Inventory.ShipSlots, shipSkinIndex));
        bool changed = true;
        changed |= RestorePendingProtectedEquipment(shipSkinIndex);

        if (!hasPendingAstronautCargo || pendingAstronautCargoSlots == null)
        {
            await SaveInventoryOnlyAsync();
            return true;
        }

        string[] cargoSlots = NormalizeShipSlots(pendingAstronautCargoSlots);
        ClearPendingAstronautCargo();

        int capacity = GetActiveShipInventoryCapacity();
        for (int i = 0; i < cargoSlots.Length; i++)
        {
            string itemId = cargoSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (i < capacity &&
                string.IsNullOrWhiteSpace(CurrentProfile.Inventory.ShipSlots[i]) &&
                CanStoreItemInShipSlot(itemId, shipSkinIndex, i))
            {
                CurrentProfile.Inventory.ShipSlots[i] = itemId;
                changed = true;
                continue;
            }

            if (CurrentProfile.Inventory.TryAddToShip(itemId, capacity, shipSkinIndex))
            {
                changed = true;
            }
            else
            {
                Debug.LogWarning("Astronaut cargo could not be restored: " + itemId);
            }
        }

        if (changed)
        {
            ApplyInventoryToPhoton();
            await SaveInventoryOnlyAsync();
        }

        return changed;
    }

    public void DiscardPendingAstronautCargo()
    {
        ClearPendingAstronautCargo();
        ClearPendingProtectedEquipment();
    }

    public async Task<bool> MoveShipItemWithinShipAsync(int sourceIndex, int targetIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (sourceIndex == targetIndex)
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        workingInventory.Normalize();

        int capacity = GetActiveShipInventoryCapacity();
        if (sourceIndex < 0 || sourceIndex >= capacity || targetIndex < 0 || targetIndex >= capacity)
            return false;

        string sourceItem = workingInventory.ShipSlots[sourceIndex];
        if (string.IsNullOrWhiteSpace(sourceItem))
            return false;

        string targetItem = workingInventory.ShipSlots[targetIndex];
        if (!CanStoreItemInShipSlot(sourceItem, GetActiveShipSkinIndex(), targetIndex) ||
            !CanStoreItemInShipSlot(targetItem, GetActiveShipSkinIndex(), sourceIndex))
        {
            return false;
        }

        workingInventory.ShipSlots[targetIndex] = sourceItem;
        workingInventory.ShipSlots[sourceIndex] = targetItem;

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> MoveInventoryItemAsync(bool fromShipInventory, int sourceIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        string movedItem = fromShipInventory
            ? CurrentProfile.Inventory.RemoveFromShip(sourceIndex)
            : CurrentProfile.Inventory.RemoveFromPlayer(sourceIndex);

        if (string.IsNullOrWhiteSpace(movedItem))
            return false;

        bool moved = fromShipInventory
            ? CurrentProfile.Inventory.TryAddToPlayer(movedItem)
            : CurrentProfile.Inventory.TryAddToShip(movedItem, GetActiveShipInventoryCapacity(), CurrentProfile.ShipSkinIndex);

        if (!moved)
        {
            if (fromShipInventory)
                CurrentProfile.Inventory.RestoreShip(sourceIndex, movedItem);
            else
                CurrentProfile.Inventory.RestorePlayer(sourceIndex, movedItem);

            return false;
        }

        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<int> UnloadShipInventoryToPlayerAsync()
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        workingInventory.Normalize();

        int movedCount = 0;
        for (int i = 0; i < workingInventory.ShipSlots.Length; i++)
        {
            string itemId = workingInventory.ShipSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!workingInventory.TryAddToPlayer(itemId))
                break;

            workingInventory.ShipSlots[i] = null;
            movedCount++;
        }

        if (movedCount <= 0)
            return 0;

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return movedCount;
    }

    public int GetNextPlayerInventoryExtendPrice()
    {
        EnsureInventory();
        int extensions = Mathf.Max(0, (CurrentProfile.Inventory.PlayerSlots.Length - PlayerInventoryData.DefaultPlayerSlotCount) / PlayerInventoryData.PlayerSlotExtensionSize);
        int price = PlayerInventoryExtendBasePrice;
        for (int i = 0; i < extensions && price < PlayerInventoryExtendMaxPrice; i++)
            price = Mathf.Min(PlayerInventoryExtendMaxPrice, price * 2);

        return price;
    }

    public async Task<bool> TryExtendPlayerInventoryAsync()
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int price = GetNextPlayerInventoryExtendPrice();
        if (CurrentProfile.Astrons < price)
            return false;

        CurrentProfile.Astrons = Mathf.Max(0, CurrentProfile.Astrons - price);
        CurrentProfile.Inventory.ExtendPlayerSlots(PlayerInventoryData.PlayerSlotExtensionSize);
        await SaveInventoryAndAstronsAsync();
        return true;
    }

    public async Task<bool> SellInventoryItemAsync(bool fromShipInventory, int sourceIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        string soldItem = fromShipInventory
            ? CurrentProfile.Inventory.RemoveFromShip(sourceIndex)
            : CurrentProfile.Inventory.RemoveFromPlayer(sourceIndex);

        if (string.IsNullOrWhiteSpace(soldItem))
            return false;

        int value = InventoryItemCatalog.GetSellValueAstrons(soldItem);
        if (value <= 0)
        {
            RestoreInventorySource(fromShipInventory, sourceIndex, soldItem);
            return false;
        }

        CurrentProfile.Astrons = Mathf.Max(0, CurrentProfile.Astrons + value);
        long updatedSoldValue = (long)Mathf.Max(0, CurrentProfile.PilotSoldItemsAstrons) + value;
        CurrentProfile.PilotSoldItemsAstrons = updatedSoldValue > int.MaxValue ? int.MaxValue : (int)updatedSoldValue;

        await SaveInventoryAndAstronsAsync();
        return true;
    }

    public async Task AddAstronsAsync(int amount)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int normalizedAmount = Mathf.Max(0, amount);
        if (normalizedAmount <= 0)
            return;

        long updatedAstrons = (long)Mathf.Max(0, CurrentProfile.Astrons) + normalizedAmount;
        CurrentProfile.Astrons = updatedAstrons > int.MaxValue ? int.MaxValue : (int)updatedAstrons;
        await SaveInventoryAndAstronsAsync();
    }

    public int GetShopBuyPriceAstrons(string itemId)
    {
        int basePrice = InventoryItemCatalog.GetShopBuyValueAstrons(itemId);
        if (basePrice <= 0)
            return 0;

        return basePrice;
    }

    public bool CanBuyAvengerStartingCodes()
    {
        EnsureInventory();
        EnsureShipUnlocks();

        if (IsShipUnlocked(ShipType.Avenger))
            return false;

        if (CurrentProfile.AvengerTheftAttempt != null && CurrentProfile.AvengerTheftAttempt.Active)
            return false;

        return !HasInventoryItem(InventoryItemCatalog.AvengerStartingCodesId);
    }

    public async Task<bool> TryBuyShopItemAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        if (definition == null ||
            (definition.ItemType != InventoryItemType.Equipment && definition.ItemType != InventoryItemType.Quest))
        {
            return false;
        }

        if (string.Equals(itemId, InventoryItemCatalog.AvengerStartingCodesId, StringComparison.Ordinal) &&
            !CanBuyAvengerStartingCodes())
        {
            return false;
        }

        int price = GetShopBuyPriceAstrons(itemId);
        if (price <= 0 || CurrentProfile.Astrons < price)
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        bool stored = workingInventory.TryAddToPlayer(itemId);
        if (!stored)
            stored = workingInventory.TryAddToShip(itemId, GetProfileShipInventoryCapacity(CurrentProfile.ShipSkinIndex, workingInventory.EquipmentSlots), CurrentProfile.ShipSkinIndex);

        if (!stored)
            return false;

        CurrentProfile.Astrons = Mathf.Max(0, CurrentProfile.Astrons - price);
        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryAndAstronsAsync();
        return true;
    }

    public bool IsBlueprintUnlocked(string blueprintItemId)
    {
        EnsureBlueprintUnlocks();
        if (!InventoryItemCatalog.IsBlueprintItem(blueprintItemId))
            return false;

        for (int i = 0; i < CurrentProfile.UnlockedBlueprintIds.Length; i++)
        {
            if (string.Equals(CurrentProfile.UnlockedBlueprintIds[i], blueprintItemId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public bool IsBlueprintUnlockedForItem(string itemId)
    {
        string blueprintItemId = InventoryItemCatalog.GetBlueprintItemId(itemId);
        return IsBlueprintUnlocked(blueprintItemId);
    }

    public bool CanAffordItemTrade(string[] costItemIds)
    {
        EnsureInventory();

        Dictionary<string, int> counts = BuildItemCounts(CurrentProfile.Inventory);
        return HasRequiredItems(counts, costItemIds);
    }

    public bool IsMissEnigmaBlueprintPurchased(string blueprintItemId)
    {
        EnsureMissEnigmaBlueprintPurchases();
        if (BlueprintCatalog.GetMissEnigmaOffer(blueprintItemId) == null)
            return false;

        for (int i = 0; i < CurrentProfile.MissEnigmaPurchasedBlueprintIds.Length; i++)
        {
            if (string.Equals(CurrentProfile.MissEnigmaPurchasedBlueprintIds[i], blueprintItemId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public async Task<bool> TryPurchaseMissEnigmaBlueprintAsync(string blueprintItemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureMissEnigmaBlueprintPurchases();

        BlueprintTradeOffer offer = BlueprintCatalog.GetMissEnigmaOffer(blueprintItemId);
        if (offer == null || IsBlueprintUnlocked(blueprintItemId) || IsMissEnigmaBlueprintPurchased(blueprintItemId) || !CanAffordItemTrade(offer.CostItemIds))
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        if (!RemoveRequiredItems(workingInventory, offer.CostItemIds))
            return false;

        bool stored = workingInventory.TryAddToPlayer(blueprintItemId);
        if (!stored)
            stored = workingInventory.TryAddToShip(blueprintItemId, GetProfileShipInventoryCapacity(CurrentProfile.ShipSkinIndex, workingInventory.EquipmentSlots), CurrentProfile.ShipSkinIndex);

        if (!stored)
            return false;

        HashSet<string> purchased = new HashSet<string>(CurrentProfile.MissEnigmaPurchasedBlueprintIds, StringComparer.Ordinal)
        {
            blueprintItemId
        };
        string[] purchasedIds = new string[purchased.Count];
        purchased.CopyTo(purchasedIds);
        Array.Sort(purchasedIds, StringComparer.Ordinal);

        CurrentProfile.Inventory = workingInventory;
        CurrentProfile.MissEnigmaPurchasedBlueprintIds = NormalizeMissEnigmaBlueprintPurchases(purchasedIds);
        await SaveInventoryAndMissEnigmaBlueprintPurchasesAsync();
        return true;
    }

    public async Task<bool> TryTradeItemsForItemAsync(string outputItemId, string[] costItemIds)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (InventoryItemCatalog.GetDefinition(outputItemId) == null || !CanAffordItemTrade(costItemIds))
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        if (!RemoveRequiredItems(workingInventory, costItemIds))
            return false;

        bool stored = workingInventory.TryAddToPlayer(outputItemId);
        if (!stored)
            stored = workingInventory.TryAddToShip(outputItemId, GetProfileShipInventoryCapacity(CurrentProfile.ShipSkinIndex, workingInventory.EquipmentSlots), CurrentProfile.ShipSkinIndex);

        if (!stored)
            return false;

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> UseBlueprintItemAsync(bool fromShipInventory, int sourceIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureBlueprintUnlocks();

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        string blueprintItemId = fromShipInventory
            ? workingInventory.RemoveFromShip(sourceIndex)
            : workingInventory.RemoveFromPlayer(sourceIndex);

        if (!InventoryItemCatalog.IsBlueprintItem(blueprintItemId))
            return false;

        if (IsBlueprintUnlocked(blueprintItemId))
            return false;

        HashSet<string> unlocked = new HashSet<string>(CurrentProfile.UnlockedBlueprintIds, StringComparer.Ordinal)
        {
            blueprintItemId
        };
        string[] unlockedIds = new string[unlocked.Count];
        unlocked.CopyTo(unlockedIds);
        Array.Sort(unlockedIds, StringComparer.Ordinal);

        CurrentProfile.Inventory = workingInventory;
        CurrentProfile.UnlockedBlueprintIds = NormalizeUnlockedBlueprintIds(unlockedIds);
        await SaveInventoryAndBlueprintsAsync();
        return true;
    }

    public async Task UnlockAllBlueprintsAsync()
    {
        await EnsureInitializedAsync();
        EnsureBlueprintUnlocks();

        CurrentProfile.UnlockedBlueprintIds = NormalizeUnlockedBlueprintIds(InventoryItemCatalog.GetAllBlueprintItemIds());
        await SaveBlueprintsAsync();
    }

    public async Task LockAllBlueprintsAsync()
    {
        await EnsureInitializedAsync();
        EnsureBlueprintUnlocks();

        CurrentProfile.UnlockedBlueprintIds = NormalizeUnlockedBlueprintIds(BlueprintCatalog.GetStarterUnlockedBlueprintItemIds());
        await SaveBlueprintsAsync();
    }

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
        CurrentProfile.ShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex;
        await SaveShipUnlocksAsync();
    }

    public bool HasInventoryItem(string itemId)
    {
        EnsureInventory();
        return CountItemInSlots(CurrentProfile.Inventory.PlayerSlots, CurrentProfile.Inventory.PlayerSlots.Length, itemId) > 0 ||
               CountItemInSlots(CurrentProfile.Inventory.ShipSlots, GetActiveShipInventoryCapacity(), itemId) > 0 ||
               CountItemInSlots(CurrentProfile.Inventory.EquipmentSlots, CurrentProfile.Inventory.EquipmentSlots.Length, itemId) > 0;
    }

    public bool HasAvengerStartingCodesInShip()
    {
        EnsureInventory();
        return CountItemInSlots(CurrentProfile.Inventory.ShipSlots, GetActiveShipInventoryCapacity(), InventoryItemCatalog.AvengerStartingCodesId) > 0;
    }

    public async Task<bool> BeginAvengerTheftAttemptAsync(int originalShipSkinIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureShipUnlocks();

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        int safeOriginalSkinIndex = Mathf.Clamp(originalShipSkinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex);
        int shipCapacity = GetEffectiveShipInventoryCapacity(safeOriginalSkinIndex, workingInventory.EquipmentSlots);
        int cargoLimit = Mathf.Clamp(shipCapacity, 0, workingInventory.ShipSlots.Length);
        int codeSlotIndex = FindItemInSlots(workingInventory.ShipSlots, cargoLimit, InventoryItemCatalog.AvengerStartingCodesId);
        if (codeSlotIndex < 0)
            return false;

        int refundAstrons = 0;
        for (int i = 0; i < cargoLimit; i++)
        {
            string itemId = workingInventory.ShipSlots[i];
            if (!string.IsNullOrWhiteSpace(itemId) &&
                !string.Equals(itemId, InventoryItemCatalog.AvengerStartingCodesId, StringComparison.Ordinal))
            {
                refundAstrons = AddItemValueClamped(refundAstrons, itemId);
            }

            workingInventory.ShipSlots[i] = null;
        }

        for (int i = 0; i < workingInventory.EquipmentSlots.Length; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, safeOriginalSkinIndex))
                continue;

            string itemId = workingInventory.EquipmentSlots[i];
            if (!string.IsNullOrWhiteSpace(itemId))
                refundAstrons = AddItemValueClamped(refundAstrons, itemId);

            workingInventory.EquipmentSlots[i] = null;
        }

        CurrentProfile.Inventory = workingInventory;
        CurrentProfile.AvengerTheftAttempt = new AvengerTheftAttemptData
        {
            Active = true,
            RefundAstrons = Mathf.Max(0, refundAstrons),
            OriginalShipSkinIndex = safeOriginalSkinIndex
        };

        await SaveInventoryAndAvengerTheftAttemptAsync();
        return true;
    }

    public async Task<bool> CompleteAvengerTheftAttemptAsync(int returningShipSkinIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureShipUnlocks();

        AvengerTheftAttemptData attempt = NormalizeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt);
        if (!attempt.Active)
            return false;

        if (ShipCatalog.GetShipTypeFromSkinIndex(returningShipSkinIndex) != ShipType.Avenger)
        {
            CurrentProfile.AvengerTheftAttempt = AvengerTheftAttemptData.Empty();
            await SaveAvengerTheftAttemptOnlyAsync();
            return false;
        }

        int refund = Mathf.Max(0, attempt.RefundAstrons);
        if (refund > 0)
        {
            long updatedAstrons = (long)Mathf.Max(0, CurrentProfile.Astrons) + refund;
            CurrentProfile.Astrons = updatedAstrons > int.MaxValue ? int.MaxValue : (int)updatedAstrons;
        }

        HashSet<string> ids = new HashSet<string>(CurrentProfile.UnlockedShipIds, StringComparer.Ordinal)
        {
            ShipCatalog.GetShipTypeId(ShipType.Avenger)
        };
        string[] unlockedIds = new string[ids.Count];
        ids.CopyTo(unlockedIds);
        CurrentProfile.UnlockedShipIds = NormalizeUnlockedShipIds(unlockedIds);
        CurrentProfile.ShipSkinIndex = ShipCatalog.AvengerDarkGreenSkinIndex;
        CurrentProfile.AvengerTheftAttempt = AvengerTheftAttemptData.Empty();

        await SaveAvengerTheftCompletionAsync();
        return true;
    }

    public async Task FailAvengerTheftAttemptAsync()
    {
        await EnsureInitializedAsync();
        if (CurrentProfile == null || CurrentProfile.AvengerTheftAttempt == null || !CurrentProfile.AvengerTheftAttempt.Active)
            return;

        CurrentProfile.AvengerTheftAttempt = AvengerTheftAttemptData.Empty();
        await SaveAvengerTheftAttemptOnlyAsync();
    }

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

    public async Task<bool> SalvageInventoryItemAsync(bool fromShipInventory, int sourceIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureBlueprintUnlocks();

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        string sourceItemPreview = fromShipInventory
            ? GetSlotItem(workingInventory.ShipSlots, sourceIndex)
            : GetSlotItem(workingInventory.PlayerSlots, sourceIndex);

        string[] salvageOutputs = InventoryItemCatalog.IsBlueprintItem(sourceItemPreview)
            ? IsBlueprintUnlocked(sourceItemPreview)
                ? new[] { InventoryItemCatalog.BlueprintScrapId }
                : Array.Empty<string>()
            : InventoryItemCatalog.RollSalvageOutputs(sourceItemPreview);
        if (string.IsNullOrWhiteSpace(sourceItemPreview) || salvageOutputs == null || salvageOutputs.Length == 0)
            return false;

        string sourceItem = fromShipInventory
            ? workingInventory.RemoveFromShip(sourceIndex)
            : workingInventory.RemoveFromPlayer(sourceIndex);

        if (string.IsNullOrWhiteSpace(sourceItem))
            return false;

        for (int i = 0; i < salvageOutputs.Length; i++)
        {
            string output = salvageOutputs[i];
            bool added = fromShipInventory
                ? workingInventory.TryAddToShip(output, GetProfileShipInventoryCapacity(CurrentProfile.ShipSkinIndex, workingInventory.EquipmentSlots), CurrentProfile.ShipSkinIndex)
                : workingInventory.TryAddToPlayer(output);

            if (!added)
                return false;
        }

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> MoveInventoryItemToEquipmentAsync(bool fromShipInventory, int sourceIndex, int equipmentSlotIndex, int shipSkinIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (!IsEquipmentSlotEnabledForProfile(equipmentSlotIndex, shipSkinIndex))
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        string movedItem = fromShipInventory
            ? GetSlotItem(workingInventory.ShipSlots, sourceIndex)
            : GetSlotItem(workingInventory.PlayerSlots, sourceIndex);

        if (!InventoryItemCatalog.IsCompatibleWithEquipmentSlot(movedItem, equipmentSlotIndex))
            return false;

        if (IsSingleInstallItem(movedItem) &&
            IsItemAlreadyEquipped(workingInventory.EquipmentSlots, movedItem, equipmentSlotIndex))
        {
            return false;
        }

        movedItem = fromShipInventory
            ? workingInventory.RemoveFromShip(sourceIndex)
            : workingInventory.RemoveFromPlayer(sourceIndex);

        if (string.IsNullOrWhiteSpace(movedItem))
            return false;

        string replacedItem = workingInventory.RemoveFromEquipment(equipmentSlotIndex);
        workingInventory.SetEquipment(equipmentSlotIndex, movedItem);

        if (!TryMoveOverflowShipCargoToPlayer(workingInventory, shipSkinIndex))
            return false;

        if (!string.IsNullOrWhiteSpace(replacedItem))
        {
            bool restored = fromShipInventory
                ? workingInventory.TryAddToShip(replacedItem, GetProfileShipInventoryCapacity(shipSkinIndex, workingInventory.EquipmentSlots), shipSkinIndex)
                : workingInventory.TryAddToPlayer(replacedItem);

            if (!restored)
                return false;
        }

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return true;
    }

    string GetSlotItem(string[] slots, int index)
    {
        if (slots == null || index < 0 || index >= slots.Length)
            return null;

        return slots[index];
    }

    static bool IsItemAlreadyEquipped(string[] equipmentSlots, string itemId, int ignoredSlotIndex)
    {
        if (equipmentSlots == null || string.IsNullOrWhiteSpace(itemId))
            return false;

        for (int i = 0; i < equipmentSlots.Length; i++)
        {
            if (i == ignoredSlotIndex)
                continue;

            if (string.Equals(equipmentSlots[i], itemId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static bool IsSingleInstallItem(string itemId)
    {
        return string.Equals(itemId, InventoryItemCatalog.LootingFriendId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.FiringFriendId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.DropbotId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.EscapePodId, StringComparison.Ordinal);
    }

    bool TryMoveOverflowShipCargoToPlayer(PlayerInventoryData inventory, int shipSkinIndex)
    {
        if (inventory == null)
            return false;

        inventory.Normalize();
        int capacity = GetProfileShipInventoryCapacity(shipSkinIndex, inventory.EquipmentSlots);
        for (int i = capacity; i < inventory.ShipSlots.Length; i++)
        {
            string itemId = inventory.ShipSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!inventory.TryAddToPlayer(itemId))
                return false;

            inventory.ShipSlots[i] = null;
        }

        return true;
    }

    public async Task<bool> MoveEquipmentItemToInventoryAsync(int equipmentSlotIndex, bool toPlayerInventory, int shipSkinIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (!IsEquipmentSlotEnabledForProfile(equipmentSlotIndex, shipSkinIndex))
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        string movedItem = workingInventory.RemoveFromEquipment(equipmentSlotIndex);
        if (string.IsNullOrWhiteSpace(movedItem))
            return false;

        if (!TryMoveOverflowShipCargoToPlayer(workingInventory, shipSkinIndex))
            return false;

        bool moved = toPlayerInventory
            ? workingInventory.TryAddToPlayer(movedItem)
            : workingInventory.TryAddToShip(movedItem, GetProfileShipInventoryCapacity(shipSkinIndex, workingInventory.EquipmentSlots), shipSkinIndex);

        if (!moved)
            return false;

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> AddItemToShipAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int shipSkinIndex = GetActiveShipSkinIndex();
        if (!CurrentProfile.Inventory.TryAddToShip(itemId, GetActiveShipInventoryCapacity(), shipSkinIndex))
            return false;

        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> AddItemToShipDeferredSaveAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int shipSkinIndex = GetActiveShipSkinIndex();
        if (!CurrentProfile.Inventory.TryAddToShip(itemId, GetActiveShipInventoryCapacity(), shipSkinIndex))
            return false;

        MarkInventoryChangedDeferred();
        return true;
    }

    public async Task<bool> AddItemToPlayerDeferredSaveAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (!CurrentProfile.Inventory.TryAddToPlayer(itemId))
            return false;

        MarkInventoryChangedDeferred();
        return true;
    }

    public async Task<bool> AddRandomLootEquipmentAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        int shipSkinIndex = GetActiveShipSkinIndex();

        int equipmentSlot = GetFirstFreeEquipmentSlotByCategoryForProfile(workingInventory.EquipmentSlots, shipSkinIndex, definition.Category);
        if (equipmentSlot >= 0)
        {
            if (!IsSingleInstallItem(itemId) || !IsItemAlreadyEquipped(workingInventory.EquipmentSlots, itemId, equipmentSlot))
            {
                workingInventory.SetEquipment(equipmentSlot, itemId);
                CurrentProfile.Inventory = workingInventory;
                await SaveInventoryOnlyAsync();
                return true;
            }
        }

        if (!workingInventory.TryAddToShip(itemId, GetProfileShipInventoryCapacity(shipSkinIndex, workingInventory.EquipmentSlots), shipSkinIndex))
            return false;

        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> AddRandomLootEquipmentDeferredSaveAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        int shipSkinIndex = GetActiveShipSkinIndex();

        int equipmentSlot = GetFirstFreeEquipmentSlotByCategoryForProfile(workingInventory.EquipmentSlots, shipSkinIndex, definition.Category);
        if (equipmentSlot >= 0)
        {
            if (!IsSingleInstallItem(itemId) || !IsItemAlreadyEquipped(workingInventory.EquipmentSlots, itemId, equipmentSlot))
            {
                workingInventory.SetEquipment(equipmentSlot, itemId);
                CurrentProfile.Inventory = workingInventory;
                MarkInventoryChangedDeferred();
                return true;
            }
        }

        if (!workingInventory.TryAddToShip(itemId, GetProfileShipInventoryCapacity(shipSkinIndex, workingInventory.EquipmentSlots), shipSkinIndex))
            return false;

        CurrentProfile.Inventory = workingInventory;
        MarkInventoryChangedDeferred();
        return true;
    }

    public bool IsProjectComplete(string projectId)
    {
        EnsureProjectProgress();
        return ProjectCatalog.IsProjectComplete(CurrentProfile.ProjectProgress, projectId);
    }

    public bool IsProjectStageUnlocked(string projectId, int stageIndex)
    {
        EnsureProjectProgress();
        return ProjectCatalog.IsStageUnlocked(CurrentProfile.ProjectProgress, projectId, stageIndex);
    }

    public bool IsProjectStageComplete(string projectId, int stageIndex)
    {
        EnsureProjectProgress();
        ProjectDefinition project = ProjectCatalog.Get(projectId);
        PlayerProjectProgressEntry progress = FindProjectEntry(CurrentProfile.ProjectProgress, projectId);
        return ProjectCatalog.IsStageComplete(project, progress, stageIndex);
    }

    public bool IsProjectStageRewardClaimed(string projectId, int stageIndex)
    {
        EnsureProjectProgress();
        PlayerProjectProgressEntry progress = FindProjectEntry(CurrentProfile.ProjectProgress, projectId);
        PlayerProjectStageProgress stage = progress != null ? progress.GetStage(stageIndex) : null;
        return stage != null && stage.RewardClaimed;
    }

    public int GetProjectStepDelivered(string projectId, int stageIndex, int stepIndex)
    {
        EnsureProjectProgress();
        PlayerProjectProgressEntry progress = FindProjectEntry(CurrentProfile.ProjectProgress, projectId);
        PlayerProjectStageProgress stage = progress != null ? progress.GetStage(stageIndex) : null;
        return stage != null ? stage.GetDelivered(stepIndex) : 0;
    }

    public int CountProjectRequirementAvailable(ProjectStepDefinition step)
    {
        EnsureInventory();
        return CountProjectRequirementAvailable(CurrentProfile.Inventory, step, GetActiveShipInventoryCapacity());
    }

    public async Task<ProjectCommitResult> CommitProjectStepAsync(string projectId, int stageIndex, int stepIndex, int requestedAmount)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureProjectProgress();

        ProjectCommitResult result = new ProjectCommitResult();
        ProjectDefinition project = ProjectCatalog.Get(projectId);
        if (project == null || project.Stages == null || stageIndex < 0 || stageIndex >= project.Stages.Length)
            return ProjectCommitFailed(result, "Project not found.");

        ProjectStageDefinition stage = project.Stages[stageIndex];
        if (stage == null || stage.Steps == null || stepIndex < 0 || stepIndex >= stage.Steps.Length)
            return ProjectCommitFailed(result, "Step not found.");

        if (!ProjectCatalog.IsStageUnlocked(CurrentProfile.ProjectProgress, projectId, stageIndex))
            return ProjectCommitFailed(result, "Finish previous stage first.");

        int amount = Mathf.Max(0, requestedAmount);
        if (amount <= 0)
            return ProjectCommitFailed(result, "Choose amount first.");

        ProjectStepDefinition step = stage.Steps[stepIndex];
        PlayerProjectProgressEntry projectProgress = FindProjectEntry(CurrentProfile.ProjectProgress, projectId);
        PlayerProjectStageProgress stageProgress = projectProgress != null ? projectProgress.GetStage(stageIndex) : null;
        if (stageProgress == null)
            return ProjectCommitFailed(result, "Project progress unavailable.");

        int required = Mathf.Max(0, step.RequiredCount);
        int delivered = stageProgress.GetDelivered(stepIndex);
        int missing = Mathf.Max(0, required - delivered);
        if (missing <= 0)
            return ProjectCommitFailed(result, "This step is already complete.");

        int shipCapacity = GetActiveShipInventoryCapacity();
        int available = CountProjectRequirementAvailable(CurrentProfile.Inventory, step, shipCapacity);
        int committed = Mathf.Min(amount, missing, available);
        if (committed <= 0)
            return ProjectCommitFailed(result, "Required item is not available.");

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        if (!RemoveProjectRequirementItems(workingInventory, step, committed, shipCapacity))
            return ProjectCommitFailed(result, "Could not remove required items.");

        CurrentProfile.Inventory = workingInventory;
        stageProgress.SetDelivered(stepIndex, delivered + committed);
        result.Success = true;
        result.CommittedCount = committed;
        result.StageCompleted = ProjectCatalog.IsStageComplete(project, projectProgress, stageIndex);
        result.Message = "Committed " + committed + ".";

        if (result.StageCompleted && !stageProgress.RewardClaimed)
        {
            result.RewardClaimed = TryApplyProjectReward(stage.Reward, out string rewardFailure);
            if (result.RewardClaimed)
            {
                stageProgress.RewardClaimed = true;
                result.Message = "Stage complete. Reward claimed.";
            }
            else if (!string.IsNullOrWhiteSpace(rewardFailure))
            {
                result.Message = "Stage complete. " + rewardFailure;
            }
        }

        await SaveProjectsInventoryAndAstronsAsync();
        return result;
    }

    public async Task<ProjectCommitResult> TryClaimProjectStageRewardAsync(string projectId, int stageIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        EnsureProjectProgress();

        ProjectCommitResult result = new ProjectCommitResult();
        ProjectDefinition project = ProjectCatalog.Get(projectId);
        PlayerProjectProgressEntry projectProgress = FindProjectEntry(CurrentProfile.ProjectProgress, projectId);
        if (project == null || project.Stages == null || stageIndex < 0 || stageIndex >= project.Stages.Length || projectProgress == null)
            return ProjectCommitFailed(result, "Project not found.");

        PlayerProjectStageProgress stageProgress = projectProgress.GetStage(stageIndex);
        if (stageProgress == null)
            return ProjectCommitFailed(result, "Stage not found.");

        if (!ProjectCatalog.IsStageComplete(project, projectProgress, stageIndex))
            return ProjectCommitFailed(result, "Stage is not complete.");

        if (stageProgress.RewardClaimed)
            return ProjectCommitFailed(result, "Reward already claimed.");

        if (!TryApplyProjectReward(project.Stages[stageIndex].Reward, out string failure))
            return ProjectCommitFailed(result, failure);

        stageProgress.RewardClaimed = true;
        result.Success = true;
        result.RewardClaimed = true;
        result.StageCompleted = true;
        result.Message = "Reward claimed.";
        await SaveProjectsInventoryAndAstronsAsync();
        return result;
    }

    public async Task<bool> TryChangeShipSkinAsync(int newShipSkinIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int clampedSkin = Mathf.Clamp(newShipSkinIndex, 0, ShipCatalog.MaxShipSkinIndex);
        EnsureShipUnlocks();
        if (!IsShipSkinUnlocked(clampedSkin))
            return false;

        if (CurrentProfile.ShipSkinIndex == clampedSkin)
            return true;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        int newCapacity = GetProfileShipInventoryCapacity(clampedSkin, workingInventory.EquipmentSlots);

        for (int i = newCapacity; i < workingInventory.ShipSlots.Length; i++)
        {
            string itemId = workingInventory.ShipSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!workingInventory.TryAddToPlayer(itemId))
                return false;

            workingInventory.ShipSlots[i] = null;
        }

        if (!TryMoveSafePocketRestrictedItems(workingInventory, clampedSkin))
            return false;

        for (int i = 0; i < PlayerInventoryData.EquipmentSlotCount; i++)
        {
            if (IsEquipmentSlotEnabledForProfile(i, clampedSkin))
                continue;

            string itemId = workingInventory.EquipmentSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!workingInventory.TryAddToPlayer(itemId))
                return false;

            workingInventory.EquipmentSlots[i] = null;
        }

        CurrentProfile.ShipSkinIndex = clampedSkin;
        CurrentProfile.Inventory = workingInventory;
        await SaveShipSkinAndInventoryAsync();
        return true;
    }

    public bool IsPilotUnlocked(string pilotId)
    {
        EnsurePilotDefaults();
        return PilotCatalog.IsPilotUnlocked(CurrentProfile, pilotId);
    }

    public async Task<bool> TryChangePilotAsync(string pilotId)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        string normalizedPilotId = PilotCatalog.NormalizePilotId(pilotId);
        if (!PilotCatalog.IsPilotUnlocked(CurrentProfile, normalizedPilotId))
            return false;

        if (string.Equals(CurrentProfile.SelectedPilotId, normalizedPilotId, StringComparison.Ordinal))
            return true;

        CurrentProfile.SelectedPilotId = normalizedPilotId;
        await SavePilotStateAsync();
        return true;
    }

    public async Task<bool> UnlockPilotAsync(string pilotId)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        string normalizedPilotId = PilotCatalog.NormalizePilotId(pilotId);
        if (normalizedPilotId == PilotCatalog.JakeId)
            return false;

        if (PilotCatalog.IsPilotUnlocked(CurrentProfile, normalizedPilotId))
            return false;

        HashSet<string> ids = new HashSet<string>(PilotCatalog.NormalizeUnlockedPilotIds(CurrentProfile.UnlockedPilotIds), StringComparer.Ordinal);
        ids.Add(normalizedPilotId);
        CurrentProfile.UnlockedPilotIds = new string[ids.Count];
        ids.CopyTo(CurrentProfile.UnlockedPilotIds);
        Array.Sort(CurrentProfile.UnlockedPilotIds, StringComparer.Ordinal);

        await SavePilotStateAsync();
        return true;
    }

    public async Task<int> RecordPilotDroneKillAsync(int amount = 1)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        int increment = Mathf.Max(1, amount);
        CurrentProfile.PilotDroneKills = Mathf.Max(0, CurrentProfile.PilotDroneKills) + increment;

        try
        {
            IsBusy = true;
            var data = new Dictionary<string, object>
            {
                [CloudPilotDroneKillsKey] = CurrentProfile.PilotDroneKills
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save pilot drone kills");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            CurrentProfile.PilotDroneKills = Mathf.Max(0, CurrentProfile.PilotDroneKills - increment);
            Debug.LogError("PlayerProfileService pilot drone kill save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }

        return CurrentProfile.PilotDroneKills;
    }

    public async Task<int> RecordPilotPirateBayReturnAsync(int amount = 1)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        int increment = Mathf.Max(1, amount);
        int previousReturns = Mathf.Max(0, CurrentProfile.PilotPirateBayReturns);
        long updatedReturns = (long)previousReturns + increment;
        CurrentProfile.PilotPirateBayReturns = updatedReturns > int.MaxValue ? int.MaxValue : (int)updatedReturns;

        try
        {
            IsBusy = true;
            var data = new Dictionary<string, object>
            {
                [CloudPilotPirateBayReturnsKey] = CurrentProfile.PilotPirateBayReturns
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save pilot Pirate Bay returns");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            CurrentProfile.PilotPirateBayReturns = previousReturns;
            Debug.LogError("PlayerProfileService pilot Pirate Bay return save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }

        return CurrentProfile.PilotPirateBayReturns;
    }

    public async Task<int> RecordPilotAsteroidSalvageAsync(int amount = 1)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        int increment = Mathf.Max(1, amount);
        int previousCount = Mathf.Max(0, CurrentProfile.PilotAsteroidSalvageCount);
        long updatedCount = (long)previousCount + increment;
        CurrentProfile.PilotAsteroidSalvageCount = updatedCount > int.MaxValue ? int.MaxValue : (int)updatedCount;

        try
        {
            IsBusy = true;
            var data = new Dictionary<string, object>
            {
                [CloudPilotAsteroidSalvageCountKey] = CurrentProfile.PilotAsteroidSalvageCount
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save pilot asteroid salvage");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            CurrentProfile.PilotAsteroidSalvageCount = previousCount;
            Debug.LogError("PlayerProfileService pilot asteroid salvage save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }

        return CurrentProfile.PilotAsteroidSalvageCount;
    }

    public async Task<int> RecordPilotAshOverloadReturnAsync(int amount = 1)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        int increment = Mathf.Max(1, amount);
        int previousReturns = Mathf.Max(0, CurrentProfile.PilotAshOverloadReturns);
        long updatedReturns = (long)previousReturns + increment;
        CurrentProfile.PilotAshOverloadReturns = updatedReturns > int.MaxValue ? int.MaxValue : (int)updatedReturns;

        try
        {
            IsBusy = true;
            var data = new Dictionary<string, object>
            {
                [CloudPilotAshOverloadReturnsKey] = CurrentProfile.PilotAshOverloadReturns
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save pilot Ash overload returns");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            CurrentProfile.PilotAshOverloadReturns = previousReturns;
            Debug.LogError("PlayerProfileService pilot Ash overload return save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }

        return CurrentProfile.PilotAshOverloadReturns;
    }

    public async Task<string[]> RecordPilotAtlasMapReturnAsync(string mapId)
    {
        await EnsureInitializedAsync();
        EnsurePilotDefaults();

        string normalizedMapId = PilotCatalog.NormalizeAtlasMapId(mapId);
        if (string.IsNullOrWhiteSpace(normalizedMapId))
            return CurrentProfile.PilotAtlasMapReturns;

        string[] previousReturns = PilotCatalog.NormalizeAtlasMapReturnIds(CurrentProfile.PilotAtlasMapReturns);
        for (int i = 0; i < previousReturns.Length; i++)
        {
            if (string.Equals(previousReturns[i], normalizedMapId, StringComparison.Ordinal))
                return previousReturns;
        }

        string[] expanded = new string[previousReturns.Length + 1];
        Array.Copy(previousReturns, expanded, previousReturns.Length);
        expanded[expanded.Length - 1] = normalizedMapId;
        CurrentProfile.PilotAtlasMapReturns = PilotCatalog.NormalizeAtlasMapReturnIds(expanded);

        try
        {
            IsBusy = true;
            var data = new Dictionary<string, object>
            {
                [CloudPilotAtlasMapReturnsKey] = SerializeAtlasMapReturns(CurrentProfile.PilotAtlasMapReturns)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save pilot Atlas map returns");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            CurrentProfile.PilotAtlasMapReturns = previousReturns;
            Debug.LogError("PlayerProfileService pilot Atlas map return save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }

        return CurrentProfile.PilotAtlasMapReturns;
    }

    public async Task<string> RemoveShipItemAtAsync(int index)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        string removedItem = CurrentProfile.Inventory.RemoveFromShip(index);
        if (string.IsNullOrWhiteSpace(removedItem))
            return null;

        await SaveInventoryOnlyAsync();
        return removedItem;
    }

    public async Task<string> RemoveDropbotCargoItemDeferredSaveAsync(int slotIndex, string expectedItemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (string.IsNullOrWhiteSpace(expectedItemId) || !HasFreePlayerInventorySlot())
            return null;

        int shipSkinIndex = GetActiveShipSkinIndex();
        int capacity = GetActiveShipInventoryCapacity();
        if (!IsDropbotCargoIndex(shipSkinIndex, capacity, slotIndex, CurrentProfile.Inventory.EquipmentSlots))
            return null;

        if (slotIndex < 0 || slotIndex >= capacity || slotIndex >= CurrentProfile.Inventory.ShipSlots.Length)
            return null;

        string removedItem = CurrentProfile.Inventory.ShipSlots[slotIndex];
        if (!string.Equals(removedItem, expectedItemId, StringComparison.Ordinal))
            return null;

        CurrentProfile.Inventory.ShipSlots[slotIndex] = null;
        MarkInventoryChangedDeferred();
        return removedItem;
    }

    public async Task<string> ReplaceShipItemDeferredSaveAsync(int index, string newItemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (string.IsNullOrWhiteSpace(newItemId))
            return null;

        int capacity = GetActiveShipInventoryCapacity();
        int shipSkinIndex = GetActiveShipSkinIndex();
        if (index < 0 || index >= capacity || index >= CurrentProfile.Inventory.ShipSlots.Length)
            return null;

        if (!CanStoreItemInShipSlot(newItemId, shipSkinIndex, index))
            return null;

        string replacedItem = CurrentProfile.Inventory.ShipSlots[index];
        if (string.IsNullOrWhiteSpace(replacedItem))
            return null;

        CurrentProfile.Inventory.ShipSlots[index] = newItemId;
        MarkInventoryChangedDeferred();
        return replacedItem;
    }

    public async Task<string> RemoveFirstShipContainerAsync()
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int capacity = GetActiveShipInventoryCapacity();
        CurrentProfile.Inventory.Normalize();
        for (int i = 0; i < CurrentProfile.Inventory.ShipSlots.Length && i < capacity; i++)
        {
            string itemId = CurrentProfile.Inventory.ShipSlots[i];
            if (!InventoryItemCatalog.IsContainerItem(itemId))
                continue;

            CurrentProfile.Inventory.ShipSlots[i] = null;
            await SaveInventoryOnlyAsync();
            return itemId;
        }

        return null;
    }

    public async Task<string> RemoveFirstShipContainerDeferredSaveAsync()
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int capacity = GetActiveShipInventoryCapacity();
        CurrentProfile.Inventory.Normalize();
        for (int i = 0; i < CurrentProfile.Inventory.ShipSlots.Length && i < capacity; i++)
        {
            string itemId = CurrentProfile.Inventory.ShipSlots[i];
            if (!InventoryItemCatalog.IsContainerItem(itemId))
                continue;

            CurrentProfile.Inventory.ShipSlots[i] = null;
            MarkInventoryChangedDeferred();
            return itemId;
        }

        return null;
    }

    public async Task<string[]> RemoveShipContainersDeferredSaveAsync(int maxCount)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int targetCount = Mathf.Clamp(maxCount, 1, PlayerInventoryData.ShipSlotCount);
        List<string> removedItems = new List<string>(targetCount);
        int capacity = GetActiveShipInventoryCapacity();
        CurrentProfile.Inventory.Normalize();
        for (int i = 0; i < CurrentProfile.Inventory.ShipSlots.Length && i < capacity; i++)
        {
            string itemId = CurrentProfile.Inventory.ShipSlots[i];
            if (!InventoryItemCatalog.IsContainerItem(itemId))
                continue;

            CurrentProfile.Inventory.ShipSlots[i] = null;
            removedItems.Add(itemId);
            if (removedItems.Count >= targetCount)
                break;
        }

        if (removedItems.Count > 0)
            MarkInventoryChangedDeferred();

        return removedItems.ToArray();
    }

    public async Task<string> RemoveFirstShipItemDeferredSaveAsync(string matchItemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (string.IsNullOrWhiteSpace(matchItemId))
            return null;

        int capacity = GetActiveShipInventoryCapacity();
        CurrentProfile.Inventory.Normalize();
        for (int i = 0; i < CurrentProfile.Inventory.ShipSlots.Length && i < capacity; i++)
        {
            string itemId = CurrentProfile.Inventory.ShipSlots[i];
            if (!string.Equals(itemId, matchItemId, StringComparison.Ordinal))
                continue;

            CurrentProfile.Inventory.ShipSlots[i] = null;
            MarkInventoryChangedDeferred();
            return itemId;
        }

        return null;
    }

    public async Task<int> AddItemsToShipDeferredSaveAsync(string[] itemIds)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (itemIds == null || itemIds.Length == 0)
            return 0;

        int shipSkinIndex = GetActiveShipSkinIndex();
        int addedCount = 0;
        for (int i = 0; i < itemIds.Length; i++)
        {
            string itemId = itemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (CurrentProfile.Inventory.TryAddToShip(itemId, GetActiveShipInventoryCapacity(), shipSkinIndex))
                addedCount++;
        }

        if (addedCount > 0)
            MarkInventoryChangedDeferred();

        return addedCount;
    }

    public async Task RestoreShipItemAtAsync(int index, string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (string.IsNullOrWhiteSpace(itemId))
            return;

        int shipSkinIndex = GetActiveShipSkinIndex();
        if (CanStoreItemInShipSlot(itemId, shipSkinIndex, index))
            CurrentProfile.Inventory.RestoreShip(index, itemId);
        else if (!CurrentProfile.Inventory.TryAddToShip(itemId, GetActiveShipInventoryCapacity(), shipSkinIndex))
            return;

        await SaveInventoryOnlyAsync();
    }

    public async Task SaveInventorySnapshotAsync(PlayerInventoryData inventory)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        CurrentProfile.Inventory = inventory != null ? inventory.Clone() : PlayerInventoryData.Default();
        TryMoveSafePocketRestrictedItems(CurrentProfile.Inventory, GetActiveShipSkinIndex());
        await SaveInventoryOnlyAsync();
    }

    async Task SaveInventoryOnlyAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save inventory");
            InventoryRevision++;
            ApplyInventoryToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService inventory save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    void MarkInventoryChangedDeferred()
    {
        EnsureInventory();
        InventoryRevision++;
        ApplyInventoryToPhoton();
        ScheduleDeferredInventorySave();
    }

    void ScheduleDeferredInventorySave()
    {
        deferredInventorySavePending = true;
        int version = ++deferredInventorySaveVersion;
        _ = SaveInventoryAfterDebounceAsync(version);
    }

    async Task SaveInventoryAfterDebounceAsync(int version)
    {
        await Task.Delay(DeferredInventorySaveDelayMs);
        if (version != deferredInventorySaveVersion || !deferredInventorySavePending)
            return;

        deferredInventorySavePending = false;
        try
        {
            await SaveInventoryOnlyAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService deferred inventory save failed: " + ex);
        }
    }

    async Task SaveShipSkinAndInventoryAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();

            var data = new Dictionary<string, object>
            {
                [CloudShipSkinKey] = CurrentProfile.ShipSkinIndex,
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save ship skin and inventory");
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService ship/inventory save failed: " + ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SavePilotStateAsync()
    {
        try
        {
            IsBusy = true;
            EnsurePilotDefaults();

            var data = new Dictionary<string, object>
            {
                [CloudSelectedPilotKey] = CurrentProfile.SelectedPilotId,
                [CloudUnlockedPilotsKey] = SerializePilotUnlocks(CurrentProfile.UnlockedPilotIds),
                [CloudPilotDroneKillsKey] = CurrentProfile.PilotDroneKills,
                [CloudPilotSoldItemsAstronsKey] = CurrentProfile.PilotSoldItemsAstrons,
                [CloudPilotPirateBayReturnsKey] = CurrentProfile.PilotPirateBayReturns,
                [CloudPilotAsteroidSalvageCountKey] = CurrentProfile.PilotAsteroidSalvageCount,
                [CloudPilotAshOverloadReturnsKey] = CurrentProfile.PilotAshOverloadReturns,
                [CloudPilotAtlasMapReturnsKey] = SerializeAtlasMapReturns(CurrentProfile.PilotAtlasMapReturns)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save pilot state");
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService pilot save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveInventoryAndAstronsAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            EnsurePilotDefaults();

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudAstronsKey] = CurrentProfile.Astrons,
                [CloudPilotSoldItemsAstronsKey] = CurrentProfile.PilotSoldItemsAstrons
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save inventory and astrons");
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService sell save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveInventoryAndBlueprintsAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            EnsureBlueprintUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudUnlockedBlueprintsKey] = SerializeBlueprintUnlocks(CurrentProfile.UnlockedBlueprintIds)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save inventory and blueprints");
            InventoryRevision++;
            ApplyInventoryToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService blueprint use save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveInventoryAndMissEnigmaBlueprintPurchasesAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            EnsureMissEnigmaBlueprintPurchases();

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudMissEnigmaPurchasedBlueprintsKey] = SerializeMissEnigmaBlueprintPurchases(CurrentProfile.MissEnigmaPurchasedBlueprintIds)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save inventory and Miss Enigma blueprint purchases");
            InventoryRevision++;
            ApplyInventoryToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Miss Enigma blueprint purchase save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveBlueprintsAsync()
    {
        try
        {
            IsBusy = true;
            EnsureBlueprintUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudUnlockedBlueprintsKey] = SerializeBlueprintUnlocks(CurrentProfile.UnlockedBlueprintIds)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save blueprints");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService blueprint save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
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
                [CloudInvaderImprintsRecoveredKey] = CurrentProfile.InvaderImprintsRecovered
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

    async Task SaveInventoryAndAvengerTheftAttemptAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            CurrentProfile.AvengerTheftAttempt = NormalizeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt);

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudAvengerTheftAttemptKey] = SerializeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save Avenger theft attempt");
            InventoryRevision++;
            ApplyInventoryToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Avenger theft attempt save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveInventoryAndArrowLicenseProgressAsync(string operationName)
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            EnsureShipUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudUnlockedShipsKey] = SerializeShipUnlocks(CurrentProfile.UnlockedShipIds),
                [CloudArrowLicenseProgressKey] = SerializeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress),
                [CloudInvaderImprintsRecoveredKey] = CurrentProfile.InvaderImprintsRecovered
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

    async Task SaveViperRepairDonationAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            EnsureShipUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudUnlockedShipsKey] = SerializeShipUnlocks(CurrentProfile.UnlockedShipIds),
                [CloudViperRecoveryProgressKey] = SerializeViperRecoveryProgress(CurrentProfile.ViperRecoveryProgress),
                [CloudInvaderImprintsRecoveredKey] = CurrentProfile.InvaderImprintsRecovered
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

    async Task SaveAvengerTheftCompletionAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            EnsureShipUnlocks();

            var data = new Dictionary<string, object>
            {
                [CloudShipSkinKey] = CurrentProfile.ShipSkinIndex,
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudAstronsKey] = CurrentProfile.Astrons,
                [CloudUnlockedShipsKey] = SerializeShipUnlocks(CurrentProfile.UnlockedShipIds),
                [CloudAvengerTheftAttemptKey] = SerializeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt),
                [CloudViperRecoveryProgressKey] = SerializeViperRecoveryProgress(CurrentProfile.ViperRecoveryProgress),
                [CloudArrowLicenseProgressKey] = SerializeArrowLicenseProgress(CurrentProfile.ArrowLicenseProgress),
                [CloudBisonIndustrialPartsDeliveredKey] = CurrentProfile.BisonIndustrialPartsDelivered,
                [CloudInvaderImprintsRecoveredKey] = CurrentProfile.InvaderImprintsRecovered
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save Avenger theft completion");
            InventoryRevision++;
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Avenger theft completion save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
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

    async Task SaveAvengerTheftAttemptOnlyAsync()
    {
        try
        {
            IsBusy = true;
            CurrentProfile.AvengerTheftAttempt = NormalizeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt);

            var data = new Dictionary<string, object>
            {
                [CloudAvengerTheftAttemptKey] = SerializeAvengerTheftAttempt(CurrentProfile.AvengerTheftAttempt)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "clear Avenger theft attempt");
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService Avenger theft attempt clear failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SaveProjectsInventoryAndAstronsAsync()
    {
        try
        {
            IsBusy = true;
            EnsureInventory();
            EnsureProjectProgress();

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudAstronsKey] = CurrentProfile.Astrons,
                [CloudProjectsKey] = SerializeProjectProgress(CurrentProfile.ProjectProgress)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save projects");
            ApplyProfileToPhoton();
            NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            Debug.LogError("PlayerProfileService project save failed: " + ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    ProjectCommitResult ProjectCommitFailed(ProjectCommitResult result, string message)
    {
        result ??= new ProjectCommitResult();
        result.Success = false;
        result.Message = string.IsNullOrWhiteSpace(message) ? "Project action failed." : message;
        return result;
    }

    PlayerProjectProgressEntry FindProjectEntry(PlayerProjectProgressData progress, string projectId)
    {
        if (progress?.Entries == null || string.IsNullOrWhiteSpace(projectId))
            return null;

        for (int i = 0; i < progress.Entries.Length; i++)
        {
            PlayerProjectProgressEntry entry = progress.Entries[i];
            if (entry != null && string.Equals(entry.ProjectId, projectId, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    int CountProjectRequirementAvailable(PlayerInventoryData inventory, ProjectStepDefinition step, int shipCapacity)
    {
        if (inventory == null || step == null)
            return 0;

        inventory.Normalize();
        int count = CountMatchingItems(inventory.PlayerSlots, inventory.PlayerSlots.Length, step);
        count += CountMatchingItems(inventory.ShipSlots, Mathf.Clamp(shipCapacity, 0, inventory.ShipSlots.Length), step);
        return count;
    }

    int CountMatchingItems(string[] slots, int limit, ProjectStepDefinition step)
    {
        if (slots == null || step == null)
            return 0;

        int safeLimit = Mathf.Clamp(limit, 0, slots.Length);
        int count = 0;
        for (int i = 0; i < safeLimit; i++)
        {
            if (step.MatchesItem(slots[i]))
                count++;
        }

        return count;
    }

    bool RemoveProjectRequirementItems(PlayerInventoryData inventory, ProjectStepDefinition step, int amount, int shipCapacity)
    {
        if (inventory == null || step == null || amount <= 0)
            return false;

        inventory.Normalize();
        int remaining = amount;
        remaining = RemoveMatchingItemsFromSlots(inventory.PlayerSlots, inventory.PlayerSlots.Length, step, remaining);
        remaining = RemoveMatchingItemsFromSlots(inventory.ShipSlots, Mathf.Clamp(shipCapacity, 0, inventory.ShipSlots.Length), step, remaining);
        return remaining <= 0;
    }

    int RemoveMatchingItemsFromSlots(string[] slots, int limit, ProjectStepDefinition step, int remaining)
    {
        if (slots == null || step == null || remaining <= 0)
            return remaining;

        int safeLimit = Mathf.Clamp(limit, 0, slots.Length);
        for (int i = 0; i < safeLimit && remaining > 0; i++)
        {
            if (!step.MatchesItem(slots[i]))
                continue;

            slots[i] = null;
            remaining--;
        }

        return remaining;
    }

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
            if (string.Equals(slots[i], matchItemId, StringComparison.Ordinal))
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
            if (string.Equals(slots[i], matchItemId, StringComparison.Ordinal))
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
            if (!string.Equals(slots[i], itemId, StringComparison.Ordinal))
                continue;

            slots[i] = null;
            return true;
        }

        return false;
    }

    bool TryApplyProjectReward(ProjectRewardDefinition reward, out string failure)
    {
        failure = string.Empty;
        if (reward == null)
            return true;

        PlayerInventoryData rewardInventory = CurrentProfile.Inventory != null
            ? CurrentProfile.Inventory.Clone()
            : PlayerInventoryData.Default();
        int shipSkinIndex = GetActiveShipSkinIndex();
        int shipCapacity = GetActiveShipInventoryCapacity();

        string[] itemIds = reward.ItemIds ?? Array.Empty<string>();
        for (int i = 0; i < itemIds.Length; i++)
        {
            string itemId = itemIds[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            bool stored = rewardInventory.TryAddToPlayer(itemId);
            if (!stored)
                stored = rewardInventory.TryAddToShip(itemId, shipCapacity, shipSkinIndex);

            if (!stored)
            {
                failure = "No inventory space for reward.";
                return false;
            }
        }

        CurrentProfile.Inventory = rewardInventory;
        long updatedAstrons = (long)Mathf.Max(0, CurrentProfile.Astrons) + Mathf.Max(0, reward.Astrons);
        CurrentProfile.Astrons = updatedAstrons > int.MaxValue ? int.MaxValue : (int)updatedAstrons;
        return true;
    }

    public static bool PlayerHasFreeShipInventorySlot(Photon.Realtime.Player player)
    {
        return PlayerHasFreeShipInventorySlot(player, null);
    }

    public static bool PlayerHasFreeGadgetEquipmentSlot(Photon.Realtime.Player player)
    {
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        return GetFirstFreeEquipmentSlotByCategory(GetPlayerEquipmentSlots(player), shipSkinIndex, InventoryItemCategory.Gadget) >= 0;
    }

    public static bool PlayerHasFreeEquipmentSlotForItem(Photon.Realtime.Player player, string itemId)
    {
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        string[] equipmentSlots = GetPlayerEquipmentSlots(player);
        if (IsSingleInstallItem(itemId) && IsItemAlreadyEquipped(equipmentSlots, itemId, -1))
            return false;

        return GetFirstFreeEquipmentSlotForItem(equipmentSlots, shipSkinIndex, itemId) >= 0;
    }

    public static bool PlayerHasFreeUtilityEquipmentSlot(Photon.Realtime.Player player)
    {
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        string[] slots = GetPlayerEquipmentSlots(player);
        return GetFirstFreeEquipmentSlotByCategory(slots, shipSkinIndex, InventoryItemCategory.Gadget) >= 0 ||
               GetFirstFreeEquipmentSlotByCategory(slots, shipSkinIndex, InventoryItemCategory.Support) >= 0 ||
               GetFirstFreeEquipmentSlotByCategory(slots, shipSkinIndex, InventoryItemCategory.Rescue) >= 0;
    }

    public static string[] GetPlayerShipInventorySlots(Photon.Realtime.Player player)
    {
        if (player != null &&
            player.CustomProperties.TryGetValue(RoomSettings.ShipInventoryStateKey, out object value) &&
            value is string raw)
        {
            return DeserializeShipInventorySlots(raw);
        }

        return new string[PlayerInventoryData.ShipSlotCount];
    }

    public static bool PlayerHasAvengerStartingCodes(Photon.Realtime.Player player)
    {
        if (player == null)
            return false;

        string[] slots = GetPlayerShipInventorySlots(player);
        int capacity = GetPlayerShipInventoryCapacity(player);
        return CountItemInSlots(slots, capacity, InventoryItemCatalog.AvengerStartingCodesId) > 0;
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

    public static int GetPlayerShipInventoryCapacity(Photon.Realtime.Player player)
    {
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        if (IsPlayerViperCargoLocked(player, shipSkinIndex))
            return 0;

        return GetEffectiveShipInventoryCapacity(shipSkinIndex, GetPlayerEquipmentSlots(player));
    }

    static bool IsPlayerViperCargoLocked(Photon.Realtime.Player player, int shipSkinIndex)
    {
        if (ShipCatalog.GetShipTypeFromSkinIndex(shipSkinIndex) != ShipType.Viper || player == null)
            return false;

        if (player.CustomProperties != null &&
            player.CustomProperties.TryGetValue(PlayerViperCargoUnlockedKey, out object value) &&
            value is bool cargoUnlocked)
        {
            return !cargoUnlocked;
        }

        return false;
    }

    public static int GetEffectiveShipInventoryCapacity(int shipSkinIndex, string[] equipmentSlots)
    {
        int baseCapacity = ShipCatalog.GetShipInventoryCapacity(shipSkinIndex);
        int cargoExtensions = InventoryItemCatalog.CountEquippedItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.CargoBayExtensionId);
        return Mathf.Clamp(baseCapacity + cargoExtensions * 2, 0, PlayerInventoryData.ShipSlotCount);
    }

    public static int GetSafePocketSlotCount(int shipSkinIndex)
    {
        return ShipCatalog.GetSafePocketSlots(shipSkinIndex);
    }

    public static int GetSafePocketStartIndex(int shipSkinIndex)
    {
        int safePocketCount = GetSafePocketSlotCount(shipSkinIndex);
        return safePocketCount > 0 ? 0 : PlayerInventoryData.ShipSlotCount;
    }

    public static bool IsSafePocketIndex(int shipSkinIndex, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= PlayerInventoryData.ShipSlotCount)
            return false;

        int safePocketCount = GetSafePocketSlotCount(shipSkinIndex);
        if (safePocketCount <= 0)
            return false;

        int capacity = ShipCatalog.GetShipInventoryCapacity(shipSkinIndex);
        int safePocketEnd = Mathf.Min(capacity, safePocketCount);
        return slotIndex < safePocketEnd;
    }

    public static int GetAstronautCargoSlotCount(int shipSkinIndex)
    {
        return Mathf.Max(0, DefaultAstronautCargoSlotCount);
    }

    public static int GetPlayerAstronautCargoSlotCount(Photon.Realtime.Player player)
    {
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        return GetAstronautCargoSlotCount(shipSkinIndex);
    }

    public static bool IsAstronautCargoIndex(int shipSkinIndex, int shipCapacity, int slotIndex)
    {
        return IsAstronautCargoIndex(shipSkinIndex, shipCapacity, slotIndex, GetAstronautCargoSlotCount(shipSkinIndex));
    }

    public static bool IsAstronautCargoIndex(int shipSkinIndex, int shipCapacity, int slotIndex, int astronautCargoSlotCount)
    {
        if (slotIndex < 0 || slotIndex >= PlayerInventoryData.ShipSlotCount || astronautCargoSlotCount <= 0)
            return false;

        int clampedCapacity = Mathf.Clamp(shipCapacity, 0, PlayerInventoryData.ShipSlotCount);
        if (slotIndex >= clampedCapacity)
            return false;

        int astronautSlotsSeen = 0;
        for (int i = 0; i < clampedCapacity; i++)
        {
            if (IsSafePocketIndex(shipSkinIndex, i))
                continue;

            if (i == slotIndex)
                return astronautSlotsSeen < astronautCargoSlotCount;

            astronautSlotsSeen++;
            if (astronautSlotsSeen >= astronautCargoSlotCount)
                return false;
        }

        return false;
    }

    public static bool PlayerHasDropbotEquipped(Photon.Realtime.Player player)
    {
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        return InventoryItemCatalog.HasEquippedItem(GetPlayerEquipmentSlots(player), shipSkinIndex, InventoryItemCatalog.DropbotId);
    }

    public static int GetDropbotCargoSlotIndex(int shipSkinIndex, int shipCapacity, string[] equipmentSlots)
    {
        if (!InventoryItemCatalog.HasEquippedItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.DropbotId))
            return -1;

        int clampedCapacity = Mathf.Clamp(shipCapacity, 0, PlayerInventoryData.ShipSlotCount);
        for (int i = 0; i < clampedCapacity; i++)
        {
            if (IsSafePocketIndex(shipSkinIndex, i))
                continue;

            if (IsAstronautCargoIndex(shipSkinIndex, clampedCapacity, i))
                continue;

            return i;
        }

        return -1;
    }

    public static bool IsDropbotCargoIndex(int shipSkinIndex, int shipCapacity, int slotIndex, string[] equipmentSlots)
    {
        return slotIndex >= 0 && slotIndex == GetDropbotCargoSlotIndex(shipSkinIndex, shipCapacity, equipmentSlots);
    }

    public static bool CanStoreItemInShipSlot(string itemId, int shipSkinIndex, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return true;

        if (InventoryItemCatalog.RequiresSafePocket(itemId))
            return IsSafePocketIndex(shipSkinIndex, slotIndex) && InventoryItemCatalog.CanEnterSafePocket(itemId);

        return !IsSafePocketIndex(shipSkinIndex, slotIndex) || InventoryItemCatalog.CanEnterSafePocket(itemId);
    }

    public static string[] BuildLossWreckLoot(string[] sourceSlots, int shipSkinIndex, int shipCapacity = -1)
    {
        string[] normalized = NormalizeShipSlots(sourceSlots);
        int effectiveCapacity = shipCapacity >= 0
            ? Mathf.Clamp(shipCapacity, 0, PlayerInventoryData.ShipSlotCount)
            : ShipCatalog.GetShipInventoryCapacity(shipSkinIndex);
        for (int i = 0; i < normalized.Length; i++)
        {
            if (InventoryItemCatalog.RequiresSafePocket(normalized[i]))
                normalized[i] = null;
            else if (IsSafePocketIndex(shipSkinIndex, i) && InventoryItemCatalog.CanEnterSafePocket(normalized[i]))
                normalized[i] = null;
            else if (IsAstronautCargoIndex(shipSkinIndex, effectiveCapacity, i))
                normalized[i] = null;
        }

        return normalized;
    }

    public static string[] BuildAstronautCargoSnapshot(string[] sourceSlots, int shipSkinIndex, int shipCapacity, int astronautCargoSlotCount = -1)
    {
        string[] normalized = NormalizeShipSlots(sourceSlots);
        string[] snapshot = new string[PlayerInventoryData.ShipSlotCount];
        int slotCount = astronautCargoSlotCount >= 0
            ? astronautCargoSlotCount
            : GetAstronautCargoSlotCount(shipSkinIndex);

        for (int i = 0; i < normalized.Length; i++)
        {
            if (IsAstronautCargoIndex(shipSkinIndex, shipCapacity, i, slotCount) &&
                !string.IsNullOrWhiteSpace(normalized[i]))
            {
                snapshot[i] = normalized[i];
            }
        }

        return snapshot;
    }

    public static string[] BuildPostLossShipInventory(string[] sourceSlots, int shipSkinIndex)
    {
        string[] normalized = NormalizeShipSlots(sourceSlots);
        for (int i = 0; i < normalized.Length; i++)
        {
            bool keepSafePocketItem = IsSafePocketIndex(shipSkinIndex, i) && InventoryItemCatalog.CanEnterSafePocket(normalized[i]);
            bool keepRequiredSafePocketItem = InventoryItemCatalog.RequiresSafePocket(normalized[i]);
            if (!keepSafePocketItem && !keepRequiredSafePocketItem)
                normalized[i] = null;
        }

        return normalized;
    }

    static string[] BuildPostLossEquipmentInventory(string[] sourceSlots, int shipSkinIndex, bool preserveEngineSlots)
    {
        string[] normalized = NormalizeEquipmentSlots(sourceSlots);
        for (int i = 0; i < normalized.Length; i++)
        {
            bool keepEngineSlot =
                preserveEngineSlots &&
                ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex) &&
                InventoryItemCatalog.GetEquipmentSlotCategory(i) == InventoryItemCategory.Engine &&
                InventoryItemCatalog.IsCompatibleWithEquipmentSlot(normalized[i], i);

            if (!keepEngineSlot)
                normalized[i] = null;
        }

        return normalized;
    }

    public static bool PlayerHasFreeShipInventorySlot(Photon.Realtime.Player player, string itemId)
    {
        string[] slots = GetPlayerShipInventorySlots(player);
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        int capacity = GetPlayerShipInventoryCapacity(player);
        for (int i = 0; i < slots.Length && i < capacity; i++)
        {
            if (string.IsNullOrWhiteSpace(slots[i]) && CanStoreItemInShipSlot(itemId, shipSkinIndex, i))
                return true;
        }

        return false;
    }

    public static bool PlayerCanStoreShipItemOrAtlasAutoReplace(Photon.Realtime.Player player, string itemId)
    {
        if (PlayerHasFreeShipInventorySlot(player, itemId))
            return true;

        if (!PilotCatalog.IsSelectedPilot(player, PilotCatalog.AtlasId))
            return false;

        int newValue = InventoryItemCatalog.GetSellValueAstrons(itemId);
        if (newValue <= 0)
            return false;

        string[] slots = GetPlayerShipInventorySlots(player);
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        int capacity = GetPlayerShipInventoryCapacity(player);
        int leastValue = int.MaxValue;
        for (int i = 0; i < slots.Length && i < capacity; i++)
        {
            string currentItemId = slots[i];
            if (string.IsNullOrWhiteSpace(currentItemId))
                continue;

            if (!CanStoreItemInShipSlot(itemId, shipSkinIndex, i))
                continue;

            int currentValue = InventoryItemCatalog.GetSellValueAstrons(currentItemId);
            if (currentValue < leastValue)
                leastValue = currentValue;
        }

        return leastValue < int.MaxValue && newValue > leastValue;
    }

    public static string[] GetPlayerEquipmentSlots(Photon.Realtime.Player player)
    {
        if (player != null &&
            player.CustomProperties.TryGetValue(RoomSettings.EquipmentStateKey, out object value) &&
            value is string raw)
        {
            return DeserializeEquipmentSlots(raw);
        }

        return new string[PlayerInventoryData.EquipmentSlotCount];
    }

    string[] BuildRuntimeEquipmentSlotsForProfile(int shipSkinIndex, string[] sourceSlots)
    {
        string[] slots = NormalizeEquipmentSlots(sourceSlots);
        for (int i = 0; i < slots.Length; i++)
        {
            if (!IsEquipmentSlotEnabledForProfile(i, shipSkinIndex))
                slots[i] = null;
        }

        return slots;
    }

    static int GetFirstFreeEquipmentSlotForItem(string[] sourceSlots, int shipSkinIndex, string itemId)
    {
        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return -1;

        return GetFirstFreeEquipmentSlotByCategory(sourceSlots, shipSkinIndex, definition.Category);
    }

    int GetFirstFreeEquipmentSlotByCategoryForProfile(string[] sourceSlots, int shipSkinIndex, InventoryItemCategory category)
    {
        string[] slots = NormalizeEquipmentSlots(sourceSlots);
        for (int i = 0; i < slots.Length; i++)
        {
            if (InventoryItemCatalog.GetEquipmentSlotCategory(i) != category)
                continue;

            if (!IsEquipmentSlotEnabledForProfile(i, shipSkinIndex))
                continue;

            if (string.IsNullOrWhiteSpace(slots[i]))
                return i;
        }

        return -1;
    }

    static int GetFirstFreeEquipmentSlotByCategory(string[] sourceSlots, int shipSkinIndex, InventoryItemCategory category)
    {
        string[] slots = NormalizeEquipmentSlots(sourceSlots);
        for (int i = 0; i < slots.Length; i++)
        {
            if (InventoryItemCatalog.GetEquipmentSlotCategory(i) != category)
                continue;

            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            if (string.IsNullOrWhiteSpace(slots[i]))
                return i;
        }

        return -1;
    }

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

    void StagePendingAstronautCargo(string serializedAstronautCargo, string[] fallbackSourceSlots, int shipSkinIndex, int shipCapacity)
    {
        string[] snapshot = serializedAstronautCargo != null
            ? DeserializeShipInventorySlots(serializedAstronautCargo)
            : BuildAstronautCargoSnapshot(fallbackSourceSlots, shipSkinIndex, shipCapacity);

        if (!HasAnyShipSlotItem(snapshot))
        {
            ClearPendingAstronautCargo();
            return;
        }

        pendingAstronautCargoSlots = NormalizeShipSlots(snapshot);
        pendingAstronautCargoShipSkinIndex = shipSkinIndex;
        hasPendingAstronautCargo = true;
    }

    void StagePendingProtectedEquipment(string itemId, string[] sourceEquipmentSlots, int shipSkinIndex)
    {
        ClearPendingProtectedEquipment();
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        string[] slots = NormalizeEquipmentSlots(sourceEquipmentSlots);
        for (int i = 0; i < slots.Length; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            if (!string.Equals(slots[i], itemId, StringComparison.Ordinal))
                continue;

            pendingProtectedEquipmentItemId = itemId;
            pendingProtectedEquipmentSlotIndex = i;
            pendingProtectedEquipmentShipSkinIndex = shipSkinIndex;
            hasPendingProtectedEquipment = true;
            return;
        }
    }

    bool RestorePendingProtectedEquipment(int fallbackShipSkinIndex)
    {
        if (!hasPendingProtectedEquipment || string.IsNullOrWhiteSpace(pendingProtectedEquipmentItemId))
        {
            ClearPendingProtectedEquipment();
            return false;
        }

        string itemId = pendingProtectedEquipmentItemId;
        int preferredSlot = pendingProtectedEquipmentSlotIndex;
        int shipSkinIndex = pendingProtectedEquipmentShipSkinIndex >= 0
            ? pendingProtectedEquipmentShipSkinIndex
            : fallbackShipSkinIndex;
        ClearPendingProtectedEquipment();

        if (TryRestoreEquipmentToSlot(itemId, preferredSlot, shipSkinIndex))
            return true;

        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        int fallbackSlot = definition != null
            ? GetFirstFreeEquipmentSlotByCategoryForProfile(CurrentProfile.Inventory.EquipmentSlots, shipSkinIndex, definition.Category)
            : -1;
        if (TryRestoreEquipmentToSlot(itemId, fallbackSlot, shipSkinIndex))
            return true;

        int capacity = GetProfileShipInventoryCapacity(shipSkinIndex, CurrentProfile.Inventory.EquipmentSlots);
        if (CurrentProfile.Inventory.TryAddToShip(itemId, capacity, shipSkinIndex))
            return true;

        if (CurrentProfile.Inventory.TryAddToPlayer(itemId))
            return true;

        Debug.LogWarning("Protected equipment could not be restored after evacuation: " + itemId);
        return false;
    }

    bool ShouldPreserveEngineEquipmentOnLoss()
    {
        string pilotId = CurrentProfile != null ? CurrentProfile.SelectedPilotId : PilotCatalog.JakeId;
        return string.Equals(PilotCatalog.NormalizePilotId(pilotId), PilotCatalog.CovaxId, StringComparison.Ordinal);
    }

    bool TryRestoreEquipmentToSlot(string itemId, int slotIndex, int shipSkinIndex)
    {
        if (string.IsNullOrWhiteSpace(itemId) ||
            slotIndex < 0 ||
            slotIndex >= PlayerInventoryData.EquipmentSlotCount ||
            !IsEquipmentSlotEnabledForProfile(slotIndex, shipSkinIndex) ||
            !InventoryItemCatalog.IsCompatibleWithEquipmentSlot(itemId, slotIndex) ||
            !string.IsNullOrWhiteSpace(CurrentProfile.Inventory.EquipmentSlots[slotIndex]))
        {
            return false;
        }

        CurrentProfile.Inventory.SetEquipment(slotIndex, itemId);
        return true;
    }

    void ClearPendingAstronautCargo()
    {
        pendingAstronautCargoSlots = null;
        pendingAstronautCargoShipSkinIndex = -1;
        hasPendingAstronautCargo = false;
    }

    void ClearPendingProtectedEquipment()
    {
        pendingProtectedEquipmentItemId = null;
        pendingProtectedEquipmentSlotIndex = -1;
        pendingProtectedEquipmentShipSkinIndex = -1;
        hasPendingProtectedEquipment = false;
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

    PlayerProfileData BuildFallbackProfile()
    {
        string suffix = "0000";
        string playerId = TryGetAuthenticationPlayerId();

        if (!string.IsNullOrWhiteSpace(playerId))
        {
            suffix = playerId.Length <= 4 ? playerId : playerId.Substring(playerId.Length - 4);
        }

        return new PlayerProfileData
        {
            Nickname = "Pilot " + suffix.ToUpperInvariant(),
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
            MissEnigmaPurchasedBlueprintIds = Array.Empty<string>(),
            PilotDroneKills = 0,
            PilotSoldItemsAstrons = 0,
            PilotPirateBayReturns = 0,
            PilotAsteroidSalvageCount = 0,
            PilotAshOverloadReturns = 0,
            PilotAtlasMapReturns = Array.Empty<string>(),
            MapUnlockProgress = NormalizeMapUnlockProgress(null),
            ProjectProgress = ProjectCatalog.NormalizeProgress(null)
        };
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

    string SanitizeNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return BuildFallbackProfile().Nickname;

        string trimmed = nickname.Trim();
        if (trimmed.Length > 18)
            trimmed = trimmed.Substring(0, 18);

        return trimmed;
    }

    static bool IsSuccessfulMapReturnOutcome(string outcome)
    {
        return string.Equals(outcome, "extracted", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(outcome, "evacuated", StringComparison.OrdinalIgnoreCase);
    }

    static string BuildMapProgressToken(string mapId, string matchToken)
    {
        string normalizedMapId = LobbyMapUnlockCatalog.NormalizeMapId(mapId);
        string normalizedMatchToken = string.IsNullOrWhiteSpace(matchToken)
            ? "map_return_" + DateTime.UtcNow.Ticks
            : matchToken.Trim();
        return normalizedMatchToken + "_" + normalizedMapId;
    }

    void NotifyProfileChanged()
    {
        ProfileChanged?.Invoke(CurrentProfile);
    }

    void EnsureInventory()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        if (CurrentProfile.Inventory == null)
            CurrentProfile.Inventory = PlayerInventoryData.Default();

        CurrentProfile.Inventory.Normalize();
        TryMoveSafePocketRestrictedItems(CurrentProfile.Inventory, CurrentProfile.ShipSkinIndex);
        TryMoveIncompatibleEquipmentItems(CurrentProfile.Inventory, CurrentProfile.ShipSkinIndex);
    }

    bool TryMoveSafePocketRestrictedItems(PlayerInventoryData inventory, int shipSkinIndex)
    {
        if (inventory == null)
            return true;

        inventory.Normalize();
        int capacity = GetProfileShipInventoryCapacity(shipSkinIndex, inventory.EquipmentSlots);
        for (int i = 0; i < inventory.ShipSlots.Length && i < capacity; i++)
        {
            string itemId = inventory.ShipSlots[i];
            if (CanStoreItemInShipSlot(itemId, shipSkinIndex, i))
                continue;

            int targetSlot = inventory.GetFirstEmptyShipSlot(capacity, shipSkinIndex, itemId);
            if (targetSlot >= 0)
            {
                inventory.ShipSlots[targetSlot] = itemId;
                inventory.ShipSlots[i] = null;
                continue;
            }

            if (inventory.TryAddToPlayer(itemId))
            {
                inventory.ShipSlots[i] = null;
                continue;
            }

            return false;
        }

        return true;
    }

    bool TryMoveIncompatibleEquipmentItems(PlayerInventoryData inventory, int shipSkinIndex)
    {
        if (inventory == null)
            return true;

        inventory.Normalize();
        for (int i = 0; i < inventory.EquipmentSlots.Length; i++)
        {
            string itemId = inventory.EquipmentSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (IsEquipmentSlotEnabledForProfile(i, shipSkinIndex) &&
                InventoryItemCatalog.IsCompatibleWithEquipmentSlot(itemId, i))
            {
                continue;
            }

            inventory.EquipmentSlots[i] = null;
            int targetSlot = GetFirstFreeCompatibleEquipmentSlot(inventory.EquipmentSlots, shipSkinIndex, itemId);
            if (targetSlot >= 0)
            {
                inventory.EquipmentSlots[targetSlot] = itemId;
                continue;
            }

            if (inventory.TryAddToPlayer(itemId))
                continue;

            if (inventory.TryAddToShip(itemId, GetProfileShipInventoryCapacity(shipSkinIndex, inventory.EquipmentSlots), shipSkinIndex))
                continue;

            inventory.EquipmentSlots[i] = itemId;
            return false;
        }

        return true;
    }

    int GetFirstFreeCompatibleEquipmentSlot(string[] sourceSlots, int shipSkinIndex, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return -1;

        if (IsSingleInstallItem(itemId) && IsItemAlreadyEquipped(sourceSlots, itemId, -1))
            return -1;

        string[] slots = NormalizeEquipmentSlots(sourceSlots);
        for (int i = 0; i < slots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(slots[i]))
                continue;

            if (!IsEquipmentSlotEnabledForProfile(i, shipSkinIndex))
                continue;

            if (InventoryItemCatalog.IsCompatibleWithEquipmentSlot(itemId, i))
                return i;
        }

        return -1;
    }

    void EnsurePilotDefaults()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.UnlockedPilotIds = PilotCatalog.NormalizeUnlockedPilotIds(CurrentProfile.UnlockedPilotIds);
        CurrentProfile.PilotDroneKills = Mathf.Max(0, CurrentProfile.PilotDroneKills);
        CurrentProfile.PilotSoldItemsAstrons = Mathf.Max(0, CurrentProfile.PilotSoldItemsAstrons);
        CurrentProfile.PilotPirateBayReturns = Mathf.Max(0, CurrentProfile.PilotPirateBayReturns);
        CurrentProfile.PilotAsteroidSalvageCount = Mathf.Max(0, CurrentProfile.PilotAsteroidSalvageCount);
        CurrentProfile.PilotAshOverloadReturns = Mathf.Max(0, CurrentProfile.PilotAshOverloadReturns);
        CurrentProfile.PilotAtlasMapReturns = PilotCatalog.NormalizeAtlasMapReturnIds(CurrentProfile.PilotAtlasMapReturns);
        CurrentProfile.SelectedPilotId = PilotCatalog.NormalizePilotId(CurrentProfile.SelectedPilotId);
        if (!PilotCatalog.IsPilotUnlocked(CurrentProfile, CurrentProfile.SelectedPilotId))
            CurrentProfile.SelectedPilotId = PilotCatalog.JakeId;
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

    void EnsureBlueprintUnlocks()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.UnlockedBlueprintIds = NormalizeUnlockedBlueprintIds(CurrentProfile.UnlockedBlueprintIds);
    }

    void EnsureMissEnigmaBlueprintPurchases()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.MissEnigmaPurchasedBlueprintIds = NormalizeMissEnigmaBlueprintPurchases(CurrentProfile.MissEnigmaPurchasedBlueprintIds);
    }

    void EnsureMapUnlockProgress()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.MapUnlockProgress = NormalizeMapUnlockProgress(CurrentProfile.MapUnlockProgress);
    }

    void EnsureProjectProgress()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.ProjectProgress = ProjectCatalog.NormalizeProgress(CurrentProfile.ProjectProgress);
    }

    public static string[] NormalizeUnlockedBlueprintIds(string[] blueprintIds)
    {
        HashSet<string> normalized = new HashSet<string>(StringComparer.Ordinal);
        string[] starterBlueprintIds = BlueprintCatalog.GetStarterUnlockedBlueprintItemIds();
        for (int i = 0; i < starterBlueprintIds.Length; i++)
        {
            string starterBlueprintId = starterBlueprintIds[i];
            if (InventoryItemCatalog.IsBlueprintItem(starterBlueprintId))
                normalized.Add(starterBlueprintId);
        }

        if (blueprintIds != null)
        {
            for (int i = 0; i < blueprintIds.Length; i++)
            {
                string blueprintId = blueprintIds[i];
                if (InventoryItemCatalog.IsBlueprintItem(blueprintId))
                    normalized.Add(blueprintId);
            }
        }

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
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

    static AvengerTheftAttemptData NormalizeAvengerTheftAttempt(AvengerTheftAttemptData attempt)
    {
        if (attempt == null || !attempt.Active)
            return AvengerTheftAttemptData.Empty();

        return new AvengerTheftAttemptData
        {
            Active = true,
            RefundAstrons = Mathf.Max(0, attempt.RefundAstrons),
            OriginalShipSkinIndex = Mathf.Clamp(attempt.OriginalShipSkinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex)
        };
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

    static string[] NormalizeMissEnigmaBlueprintPurchases(string[] blueprintIds)
    {
        if (blueprintIds == null || blueprintIds.Length == 0)
            return Array.Empty<string>();

        HashSet<string> normalized = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < blueprintIds.Length; i++)
        {
            string blueprintId = blueprintIds[i];
            if (BlueprintCatalog.GetMissEnigmaOffer(blueprintId) != null)
                normalized.Add(blueprintId);
        }

        string[] result = new string[normalized.Count];
        normalized.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    public static PlayerMapUnlockProgressData NormalizeMapUnlockProgress(PlayerMapUnlockProgressData progress)
    {
        Dictionary<string, int> countsByMapId = new Dictionary<string, int>(StringComparer.Ordinal);
        if (progress != null && progress.ReturnCounts != null)
        {
            for (int i = 0; i < progress.ReturnCounts.Length; i++)
            {
                PlayerMapReturnCountEntry entry = progress.ReturnCounts[i];
                if (entry == null)
                    continue;

                string mapId = LobbyMapUnlockCatalog.NormalizeMapId(entry.MapId);
                if (string.IsNullOrWhiteSpace(mapId))
                    continue;

                int count = Mathf.Max(0, entry.Count);
                if (count <= 0)
                    continue;

                if (countsByMapId.TryGetValue(mapId, out int existingCount))
                {
                    long combined = (long)existingCount + count;
                    countsByMapId[mapId] = combined > int.MaxValue ? int.MaxValue : (int)combined;
                }
                else
                {
                    countsByMapId[mapId] = count;
                }
            }
        }

        List<PlayerMapReturnCountEntry> entries = new List<PlayerMapReturnCountEntry>();
        if (LobbyMapCatalog.AllMaps != null)
        {
            for (int i = 0; i < LobbyMapCatalog.AllMaps.Count; i++)
            {
                LobbyMapDefinition map = LobbyMapCatalog.AllMaps[i];
                if (map == null || string.IsNullOrWhiteSpace(map.Id))
                    continue;

                if (!countsByMapId.TryGetValue(map.Id, out int count) || count <= 0)
                    continue;

                entries.Add(new PlayerMapReturnCountEntry
                {
                    MapId = map.Id,
                    Count = count
                });
            }
        }

        return new PlayerMapUnlockProgressData
        {
            ReturnCounts = entries.ToArray(),
            MothershipKilled = progress != null && progress.MothershipKilled,
            CheatUnlockAllMaps = progress != null && progress.CheatUnlockAllMaps
        };
    }

    int GetActiveShipSkinIndex()
    {
        int fallbackSkin = CurrentProfile != null ? CurrentProfile.ShipSkinIndex : 0;
        if (PhotonNetwork.LocalPlayer != null &&
            PhotonNetwork.LocalPlayer.CustomProperties != null &&
            PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoomSettings.ShipSkinKey))
        {
            return RoomSettings.GetPlayerShipSkin(PhotonNetwork.LocalPlayer, fallbackSkin);
        }

        return fallbackSkin;
    }

    int GetActiveShipInventoryCapacity()
    {
        int activeSkin = GetActiveShipSkinIndex();
        if (!IsCargoUnlockedForProfile(activeSkin))
            return 0;

        int baseCapacity = GetEffectiveShipInventoryCapacity(
            activeSkin,
            CurrentProfile != null && CurrentProfile.Inventory != null
                ? BuildRuntimeEquipmentSlotsForProfile(activeSkin, CurrentProfile.Inventory.EquipmentSlots)
                : null);
        return ShipDamageState.GetLocalCargoAdjustedCapacity(baseCapacity);
    }

    int GetProfileShipInventoryCapacity(int shipSkinIndex, string[] equipmentSlots)
    {
        if (!IsCargoUnlockedForProfile(shipSkinIndex))
            return 0;

        return GetEffectiveShipInventoryCapacity(shipSkinIndex, BuildRuntimeEquipmentSlotsForProfile(shipSkinIndex, equipmentSlots));
    }

    public int GetShipInventoryCapacityForProfile(int shipSkinIndex, string[] equipmentSlots)
    {
        return GetProfileShipInventoryCapacity(shipSkinIndex, equipmentSlots);
    }

    void ApplyInventoryToPhoton()
    {
        if (CurrentProfile == null || PhotonNetwork.LocalPlayer == null)
            return;

        EnsureInventory();
        EnsurePilotDefaults();
        var props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.PilotIdKey] = CurrentProfile.SelectedPilotId,
            [RoomSettings.ShipInventoryStateKey] = SerializeShipInventorySlots(CurrentProfile.Inventory.ShipSlots),
            [RoomSettings.EquipmentStateKey] = SerializeEquipmentSlots(BuildRuntimeEquipmentSlotsForProfile(CurrentProfile.ShipSkinIndex, CurrentProfile.Inventory.EquipmentSlots)),
            [PlayerViperCargoUnlockedKey] = IsCargoUnlockedForProfile(CurrentProfile.ShipSkinIndex),
            [PlayerViperRecoveryStageKey] = (int)GetViperRecoveryStage(),
            [PlayerArrowLicenseStageKey] = (int)GetArrowLicenseStage(),
            [PlayerArrowFinalRunReadyKey] = GetArrowLicenseProgress().FinalRunEntryAvailable
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    public void SetActiveRoundShipSkin(int shipSkinIndex)
    {
        if (PhotonNetwork.LocalPlayer == null)
            return;

        int safeSkinIndex = Mathf.Clamp(shipSkinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex);
        var props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.ShipSkinKey] = safeSkinIndex,
            [PlayerViperCargoUnlockedKey] = IsCargoUnlockedForProfile(safeSkinIndex),
            [PlayerViperRecoveryStageKey] = (int)GetViperRecoveryStage(),
            [PlayerArrowLicenseStageKey] = (int)GetArrowLicenseStage(),
            [PlayerArrowFinalRunReadyKey] = GetArrowLicenseProgress().FinalRunEntryAvailable
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    void RestoreInventorySource(bool fromShipInventory, int sourceIndex, string itemId)
    {
        if (fromShipInventory)
            CurrentProfile.Inventory.RestoreShip(sourceIndex, itemId);
        else
            CurrentProfile.Inventory.RestorePlayer(sourceIndex, itemId);
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

    string SerializeInventory(PlayerInventoryData inventory)
    {
        PlayerInventoryData normalized = inventory != null ? inventory.Clone() : PlayerInventoryData.Default();
        normalized.Normalize();
        return JsonUtility.ToJson(normalized);
    }

    PlayerInventoryData DeserializeInventory(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return PlayerInventoryData.Default();

        try
        {
            PlayerInventoryData inventory = JsonUtility.FromJson<PlayerInventoryData>(json);
            if (inventory == null)
                return PlayerInventoryData.Default();

            inventory.Normalize();
            return inventory;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize inventory: " + ex.Message);
            return PlayerInventoryData.Default();
        }
    }

    string SerializeProjectProgress(PlayerProjectProgressData progress)
    {
        return JsonUtility.ToJson(ProjectCatalog.NormalizeProgress(progress));
    }

    PlayerProjectProgressData DeserializeProjectProgress(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ProjectCatalog.NormalizeProgress(null);

        try
        {
            PlayerProjectProgressData progress = JsonUtility.FromJson<PlayerProjectProgressData>(json);
            return ProjectCatalog.NormalizeProgress(progress);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize project progress: " + ex.Message);
            return ProjectCatalog.NormalizeProgress(null);
        }
    }

    string SerializePilotUnlocks(string[] pilotIds)
    {
        PilotUnlockSnapshot snapshot = new PilotUnlockSnapshot
        {
            pilotIds = PilotCatalog.NormalizeUnlockedPilotIds(pilotIds)
        };
        return JsonUtility.ToJson(snapshot);
    }

    string[] DeserializePilotUnlocks(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return PilotCatalog.GetDefaultUnlockedPilotIds();

        try
        {
            PilotUnlockSnapshot snapshot = JsonUtility.FromJson<PilotUnlockSnapshot>(json);
            return PilotCatalog.NormalizeUnlockedPilotIds(snapshot != null ? snapshot.pilotIds : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize pilot unlocks: " + ex.Message);
            return PilotCatalog.GetDefaultUnlockedPilotIds();
        }
    }

    string SerializeAtlasMapReturns(string[] mapIds)
    {
        AtlasMapReturnSnapshot snapshot = new AtlasMapReturnSnapshot
        {
            mapIds = PilotCatalog.NormalizeAtlasMapReturnIds(mapIds)
        };
        return JsonUtility.ToJson(snapshot);
    }

    string[] DeserializeAtlasMapReturns(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            AtlasMapReturnSnapshot snapshot = JsonUtility.FromJson<AtlasMapReturnSnapshot>(json);
            return PilotCatalog.NormalizeAtlasMapReturnIds(snapshot != null ? snapshot.mapIds : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize Atlas map returns: " + ex.Message);
            return Array.Empty<string>();
        }
    }

    string SerializeMapUnlockProgress(PlayerMapUnlockProgressData progress)
    {
        return JsonUtility.ToJson(NormalizeMapUnlockProgress(progress));
    }

    PlayerMapUnlockProgressData DeserializeMapUnlockProgress(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return NormalizeMapUnlockProgress(null);

        try
        {
            PlayerMapUnlockProgressData progress = JsonUtility.FromJson<PlayerMapUnlockProgressData>(json);
            return NormalizeMapUnlockProgress(progress);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize map unlock progress: " + ex.Message);
            return NormalizeMapUnlockProgress(null);
        }
    }

    string SerializeBlueprintUnlocks(string[] blueprintIds)
    {
        BlueprintUnlockSnapshot snapshot = new BlueprintUnlockSnapshot
        {
            blueprintIds = NormalizeUnlockedBlueprintIds(blueprintIds)
        };
        return JsonUtility.ToJson(snapshot);
    }

    string[] DeserializeBlueprintUnlocks(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            BlueprintUnlockSnapshot snapshot = JsonUtility.FromJson<BlueprintUnlockSnapshot>(json);
            return NormalizeUnlockedBlueprintIds(snapshot != null ? snapshot.blueprintIds : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize blueprint unlocks: " + ex.Message);
            return Array.Empty<string>();
        }
    }

    string SerializeShipUnlocks(string[] shipIds)
    {
        ShipUnlockSnapshot snapshot = new ShipUnlockSnapshot
        {
            shipIds = NormalizeUnlockedShipIds(shipIds)
        };
        return JsonUtility.ToJson(snapshot);
    }

    string[] DeserializeShipUnlocks(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ShipCatalog.GetDefaultUnlockedShipTypeIds();

        try
        {
            ShipUnlockSnapshot snapshot = JsonUtility.FromJson<ShipUnlockSnapshot>(json);
            return NormalizeUnlockedShipIds(snapshot != null ? snapshot.shipIds : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize ship unlocks: " + ex.Message);
            return ShipCatalog.GetDefaultUnlockedShipTypeIds();
        }
    }

    string SerializeAvengerTheftAttempt(AvengerTheftAttemptData attempt)
    {
        return JsonUtility.ToJson(NormalizeAvengerTheftAttempt(attempt));
    }

    AvengerTheftAttemptData DeserializeAvengerTheftAttempt(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return AvengerTheftAttemptData.Empty();

        try
        {
            AvengerTheftAttemptData attempt = JsonUtility.FromJson<AvengerTheftAttemptData>(json);
            return NormalizeAvengerTheftAttempt(attempt);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize Avenger theft attempt: " + ex.Message);
            return AvengerTheftAttemptData.Empty();
        }
    }

    string SerializeViperRecoveryProgress(ViperRecoveryProgressData progress)
    {
        return JsonUtility.ToJson(NormalizeViperRecoveryProgress(progress, CurrentProfile != null ? CurrentProfile.UnlockedShipIds : null));
    }

    ViperRecoveryProgressData DeserializeViperRecoveryProgress(string json, string[] shipIds)
    {
        if (string.IsNullOrWhiteSpace(json))
            return NormalizeViperRecoveryProgress(null, shipIds);

        try
        {
            ViperRecoveryProgressData progress = JsonUtility.FromJson<ViperRecoveryProgressData>(json);
            return NormalizeViperRecoveryProgress(progress, shipIds);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize Viper recovery progress: " + ex.Message);
            return NormalizeViperRecoveryProgress(null, shipIds);
        }
    }

    string SerializeArrowLicenseProgress(ArrowLicenseProgressData progress)
    {
        return JsonUtility.ToJson(NormalizeArrowLicenseProgress(progress, CurrentProfile != null ? CurrentProfile.UnlockedShipIds : null));
    }

    ArrowLicenseProgressData DeserializeArrowLicenseProgress(string json, string[] shipIds)
    {
        if (string.IsNullOrWhiteSpace(json))
            return NormalizeArrowLicenseProgress(null, shipIds);

        try
        {
            ArrowLicenseProgressData progress = JsonUtility.FromJson<ArrowLicenseProgressData>(json);
            return NormalizeArrowLicenseProgress(progress, shipIds);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize Arrow license progress: " + ex.Message);
            return NormalizeArrowLicenseProgress(null, shipIds);
        }
    }

    string SerializeMissEnigmaBlueprintPurchases(string[] blueprintIds)
    {
        BlueprintPurchaseSnapshot snapshot = new BlueprintPurchaseSnapshot
        {
            blueprintIds = NormalizeMissEnigmaBlueprintPurchases(blueprintIds)
        };
        return JsonUtility.ToJson(snapshot);
    }

    string[] DeserializeMissEnigmaBlueprintPurchases(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            BlueprintPurchaseSnapshot snapshot = JsonUtility.FromJson<BlueprintPurchaseSnapshot>(json);
            return NormalizeMissEnigmaBlueprintPurchases(snapshot != null ? snapshot.blueprintIds : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to deserialize Miss Enigma blueprint purchases: " + ex.Message);
            return Array.Empty<string>();
        }
    }

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

[Serializable]
public class ShipUnlockSnapshot
{
    public string[] shipIds;
}

[Serializable]
public class AvengerTheftAttemptData
{
    public bool Active;
    public int RefundAstrons;
    public int OriginalShipSkinIndex;

    public static AvengerTheftAttemptData Empty()
    {
        return new AvengerTheftAttemptData
        {
            Active = false,
            RefundAstrons = 0,
            OriginalShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex
        };
    }
}

public enum ViperRecoveryStage
{
    Locked = 0,
    WreckRecovered = 1,
    Testing = 2,
    Complete = 3
}

public enum ViperTestFlightResult
{
    NotApplicable = 0,
    TooShort = 1,
    SubsystemUnlocked = 2,
    Complete = 3
}

public enum ArrowLicenseStage
{
    Locked = 0,
    Qualifying = 1,
    TokenCollectionRequired = 2,
    PartsRequired = TokenCollectionRequired,
    MapRacesRequired = 3,
    TimeTrialRequired = MapRacesRequired,
    GhostRaceRequired = 4,
    FinalRunReady = 5,
    Complete = 6
}

public enum ArrowTimeTrialRank
{
    None = 0,
    C = 1,
    B = 2,
    A = 3,
    S = 4
}

[Serializable]
public class ViperRecoveryProgressData
{
    public int Stage;
    public string[] UnlockedSubsystemIds;

    public static ViperRecoveryProgressData Empty()
    {
        return new ViperRecoveryProgressData
        {
            Stage = (int)ViperRecoveryStage.Locked,
            UnlockedSubsystemIds = Array.Empty<string>()
        };
    }

    public static ViperRecoveryProgressData Complete()
    {
        return new ViperRecoveryProgressData
        {
            Stage = (int)ViperRecoveryStage.Complete,
            UnlockedSubsystemIds = PlayerProfileService.GetAllViperTestSubsystemIds()
        };
    }

    public ViperRecoveryProgressData Clone()
    {
        return new ViperRecoveryProgressData
        {
            Stage = Stage,
            UnlockedSubsystemIds = UnlockedSubsystemIds != null ? (string[])UnlockedSubsystemIds.Clone() : Array.Empty<string>()
        };
    }
}

[Serializable]
public class ArrowLicenseProgressData
{
    public int Stage;
    public int QualifierChips;
    public bool IonNozzleDelivered;
    public bool GyroStabilizerDelivered;
    public bool RaceTransponderDelivered;
    public int BestTimeTrialRank;
    public bool GhostRaceWon;
    public string[] CompletedRaceMapIds;
    public string ActiveRaceMapId;
    public bool FinalRunEntryAvailable;
    public bool FinalRunActive;
    public int OriginalShipSkinIndex;

    public static ArrowLicenseProgressData Empty()
    {
        return new ArrowLicenseProgressData
        {
            Stage = (int)ArrowLicenseStage.Locked,
            QualifierChips = 0,
            IonNozzleDelivered = false,
            GyroStabilizerDelivered = false,
            RaceTransponderDelivered = false,
            BestTimeTrialRank = (int)ArrowTimeTrialRank.None,
            GhostRaceWon = false,
            CompletedRaceMapIds = Array.Empty<string>(),
            ActiveRaceMapId = string.Empty,
            FinalRunEntryAvailable = false,
            FinalRunActive = false,
            OriginalShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex
        };
    }

    public static ArrowLicenseProgressData Complete()
    {
        return new ArrowLicenseProgressData
        {
            Stage = (int)ArrowLicenseStage.Complete,
            QualifierChips = PlayerProfileService.ArrowQualifierChipsRequired,
            IonNozzleDelivered = true,
            GyroStabilizerDelivered = true,
            RaceTransponderDelivered = true,
            BestTimeTrialRank = (int)ArrowTimeTrialRank.B,
            GhostRaceWon = true,
            CompletedRaceMapIds = new[]
            {
                LobbyMapCatalog.MinefieldMapId,
                LobbyMapCatalog.SnowFieldMapId,
                LobbyMapCatalog.DeepSpaceMapId,
                LobbyMapCatalog.PirateBayMapId
            },
            ActiveRaceMapId = string.Empty,
            FinalRunEntryAvailable = false,
            FinalRunActive = false,
            OriginalShipSkinIndex = ShipCatalog.ExplorerBasicSkinIndex
        };
    }

    public ArrowLicenseProgressData Clone()
    {
        return new ArrowLicenseProgressData
        {
            Stage = Stage,
            QualifierChips = QualifierChips,
            IonNozzleDelivered = IonNozzleDelivered,
            GyroStabilizerDelivered = GyroStabilizerDelivered,
            RaceTransponderDelivered = RaceTransponderDelivered,
            BestTimeTrialRank = BestTimeTrialRank,
            GhostRaceWon = GhostRaceWon,
            CompletedRaceMapIds = CompletedRaceMapIds != null ? (string[])CompletedRaceMapIds.Clone() : Array.Empty<string>(),
            ActiveRaceMapId = ActiveRaceMapId,
            FinalRunEntryAvailable = FinalRunEntryAvailable,
            FinalRunActive = FinalRunActive,
            OriginalShipSkinIndex = OriginalShipSkinIndex
        };
    }
}

[Serializable]
public class PlayerProfileData
{
    public string Nickname;
    public int ShipSkinIndex;
    public int GamesPlayed;
    public int TotalXp;
    public int Astrons;
    public PlayerInventoryData Inventory;
    public string SelectedPilotId;
    public string[] UnlockedPilotIds;
    public string[] UnlockedBlueprintIds;
    public string[] UnlockedShipIds;
    public AvengerTheftAttemptData AvengerTheftAttempt;
    public ViperRecoveryProgressData ViperRecoveryProgress;
    public ArrowLicenseProgressData ArrowLicenseProgress;
    public int BisonIndustrialPartsDelivered;
    public int InvaderImprintsRecovered;
    public string[] MissEnigmaPurchasedBlueprintIds;
    public int PilotDroneKills;
    public int PilotSoldItemsAstrons;
    public int PilotPirateBayReturns;
    public int PilotAsteroidSalvageCount;
    public int PilotAshOverloadReturns;
    public string[] PilotAtlasMapReturns;
    public PlayerMapUnlockProgressData MapUnlockProgress;
    public PlayerProjectProgressData ProjectProgress;

    public static PlayerProfileData Default()
    {
        return new PlayerProfileData
        {
            Nickname = "Pilot",
            ShipSkinIndex = 0,
            GamesPlayed = 0,
            TotalXp = 0,
            Astrons = PlayerProfileService.DefaultStartingAstrons,
            Inventory = PlayerInventoryData.Default(),
            SelectedPilotId = PilotCatalog.JakeId,
            UnlockedPilotIds = PilotCatalog.GetDefaultUnlockedPilotIds(),
            UnlockedBlueprintIds = PlayerProfileService.NormalizeUnlockedBlueprintIds(null),
            UnlockedShipIds = PlayerProfileService.NormalizeUnlockedShipIds(null),
            AvengerTheftAttempt = AvengerTheftAttemptData.Empty(),
            ViperRecoveryProgress = ViperRecoveryProgressData.Empty(),
            ArrowLicenseProgress = ArrowLicenseProgressData.Empty(),
            BisonIndustrialPartsDelivered = 0,
            InvaderImprintsRecovered = 0,
            MissEnigmaPurchasedBlueprintIds = Array.Empty<string>(),
            PilotDroneKills = 0,
            PilotSoldItemsAstrons = 0,
            PilotPirateBayReturns = 0,
            PilotAsteroidSalvageCount = 0,
            PilotAshOverloadReturns = 0,
            PilotAtlasMapReturns = Array.Empty<string>(),
            MapUnlockProgress = PlayerProfileService.NormalizeMapUnlockProgress(null),
            ProjectProgress = ProjectCatalog.NormalizeProgress(null)
        };
    }
}

[Serializable]
public class PlayerMapUnlockProgressData
{
    public PlayerMapReturnCountEntry[] ReturnCounts;
    public bool MothershipKilled;
    public bool CheatUnlockAllMaps;

    public PlayerMapUnlockProgressData Clone()
    {
        PlayerMapReturnCountEntry[] clonedCounts = ReturnCounts != null
            ? new PlayerMapReturnCountEntry[ReturnCounts.Length]
            : Array.Empty<PlayerMapReturnCountEntry>();

        for (int i = 0; i < clonedCounts.Length; i++)
        {
            PlayerMapReturnCountEntry source = ReturnCounts[i];
            clonedCounts[i] = source != null
                ? new PlayerMapReturnCountEntry { MapId = source.MapId, Count = source.Count }
                : null;
        }

        return new PlayerMapUnlockProgressData
        {
            ReturnCounts = clonedCounts,
            MothershipKilled = MothershipKilled,
            CheatUnlockAllMaps = CheatUnlockAllMaps
        };
    }

    public void SetReturnCount(string mapId, int count)
    {
        string normalizedMapId = LobbyMapUnlockCatalog.NormalizeMapId(mapId);
        if (string.IsNullOrWhiteSpace(normalizedMapId))
            return;

        count = Math.Max(0, count);
        List<PlayerMapReturnCountEntry> entries = ReturnCounts != null
            ? new List<PlayerMapReturnCountEntry>(ReturnCounts)
            : new List<PlayerMapReturnCountEntry>();

        for (int i = 0; i < entries.Count; i++)
        {
            PlayerMapReturnCountEntry entry = entries[i];
            if (entry == null)
                continue;

            if (!string.Equals(entry.MapId, normalizedMapId, StringComparison.Ordinal))
                continue;

            if (count <= 0)
                entries.RemoveAt(i);
            else
                entry.Count = count;
            ReturnCounts = entries.ToArray();
            return;
        }

        if (count > 0)
        {
            entries.Add(new PlayerMapReturnCountEntry
            {
                MapId = normalizedMapId,
                Count = count
            });
        }

        ReturnCounts = entries.ToArray();
    }
}

[Serializable]
public class PlayerMapReturnCountEntry
{
    public string MapId;
    public int Count;
}

[Serializable]
public class PilotUnlockSnapshot
{
    public string[] pilotIds;
}

[Serializable]
public class AtlasMapReturnSnapshot
{
    public string[] mapIds;
}

[Serializable]
public class BlueprintUnlockSnapshot
{
    public string[] blueprintIds;
}

[Serializable]
public class BlueprintPurchaseSnapshot
{
    public string[] blueprintIds;
}

[Serializable]
public class ShipInventorySnapshot
{
    public string[] slots;
}

[Serializable]
public class EquipmentSnapshot
{
    public string[] slots;
}

[Serializable]
public class PlayerInventoryData
{
    public const int DefaultPlayerSlotCount = 30;
    public const int PlayerSlotCount = DefaultPlayerSlotCount;
    public const int PlayerSlotExtensionSize = 20;
    public const int ShipSlotCount = 15;
    public const int EquipmentSlotCount = 12;
    public const int CraftingSlotCount = 4;
    public string[] PlayerSlots;
    public string[] ShipSlots;
    public string[] EquipmentSlots;
    public string[] CraftingSlots;

    public static PlayerInventoryData Default()
    {
        return new PlayerInventoryData
        {
            PlayerSlots = new string[DefaultPlayerSlotCount],
            ShipSlots = new string[ShipSlotCount],
            EquipmentSlots = new string[EquipmentSlotCount],
            CraftingSlots = new string[CraftingSlotCount]
        };
    }

    public PlayerInventoryData Clone()
    {
        Normalize();
        return new PlayerInventoryData
        {
            PlayerSlots = (string[])PlayerSlots.Clone(),
            ShipSlots = (string[])ShipSlots.Clone(),
            EquipmentSlots = (string[])EquipmentSlots.Clone(),
            CraftingSlots = (string[])CraftingSlots.Clone()
        };
    }

    public void Normalize()
    {
        int playerSlotCount = Mathf.Max(DefaultPlayerSlotCount, PlayerSlots != null ? PlayerSlots.Length : 0);
        if (PlayerSlots == null || PlayerSlots.Length != playerSlotCount)
        {
            string[] old = PlayerSlots;
            PlayerSlots = new string[playerSlotCount];
            CopyInto(old, PlayerSlots);
        }

        if (ShipSlots == null || ShipSlots.Length != ShipSlotCount)
        {
            string[] old = ShipSlots;
            ShipSlots = new string[ShipSlotCount];
            CopyInto(old, ShipSlots);
        }

        if (EquipmentSlots == null || EquipmentSlots.Length != EquipmentSlotCount)
        {
            string[] old = EquipmentSlots;
            EquipmentSlots = new string[EquipmentSlotCount];
            CopyInto(old, EquipmentSlots);
        }

        if (CraftingSlots == null || CraftingSlots.Length != CraftingSlotCount)
        {
            string[] old = CraftingSlots;
            CraftingSlots = new string[CraftingSlotCount];
            CopyInto(old, CraftingSlots);
        }
    }

    public int GetFirstEmptyPlayerSlot()
    {
        Normalize();
        return FindFirstEmpty(PlayerSlots);
    }

    public int GetFirstEmptyShipSlot()
    {
        Normalize();
        return FindFirstEmpty(ShipSlots);
    }

    public int GetFirstEmptyShipSlot(int capacity)
    {
        Normalize();
        return FindFirstEmpty(ShipSlots, capacity);
    }

    public int GetFirstEmptyShipSlot(int capacity, int shipSkinIndex, string itemId)
    {
        Normalize();
        int clampedCapacity = Mathf.Clamp(capacity, 0, ShipSlots.Length);
        for (int i = 0; i < clampedCapacity; i++)
        {
            if (!string.IsNullOrWhiteSpace(ShipSlots[i]))
                continue;

            if (PlayerProfileService.CanStoreItemInShipSlot(itemId, shipSkinIndex, i))
                return i;
        }

        return -1;
    }

    public bool TryAddToPlayer(string itemId)
    {
        Normalize();
        int slot = GetFirstEmptyPlayerSlot();
        if (slot < 0)
            return false;

        PlayerSlots[slot] = itemId;
        return true;
    }

    public bool TryAddToShip(string itemId)
    {
        Normalize();
        int slot = GetFirstEmptyShipSlot();
        if (slot < 0)
            return false;

        ShipSlots[slot] = itemId;
        return true;
    }

    public bool TryAddToShip(string itemId, int capacity)
    {
        Normalize();
        int slot = GetFirstEmptyShipSlot(capacity);
        if (slot < 0)
            return false;

        ShipSlots[slot] = itemId;
        return true;
    }

    public bool TryAddToShip(string itemId, int capacity, int shipSkinIndex)
    {
        Normalize();
        int slot = GetFirstEmptyShipSlot(capacity, shipSkinIndex, itemId);
        if (slot < 0)
            return false;

        ShipSlots[slot] = itemId;
        return true;
    }

    public string RemoveFromPlayer(int index)
    {
        Normalize();
        if (index < 0 || index >= PlayerSlots.Length)
            return null;

        string item = PlayerSlots[index];
        PlayerSlots[index] = null;
        return item;
    }

    public string RemoveFromShip(int index)
    {
        Normalize();
        if (index < 0 || index >= ShipSlots.Length)
            return null;

        string item = ShipSlots[index];
        ShipSlots[index] = null;
        return item;
    }

    public string RemoveFromEquipment(int index)
    {
        Normalize();
        if (index < 0 || index >= EquipmentSlots.Length)
            return null;

        string item = EquipmentSlots[index];
        EquipmentSlots[index] = null;
        return item;
    }

    public string RemoveFromCrafting(int index)
    {
        Normalize();
        if (index < 0 || index >= CraftingSlots.Length)
            return null;

        string item = CraftingSlots[index];
        CraftingSlots[index] = null;
        return item;
    }

    public void RestorePlayer(int index, string itemId)
    {
        Normalize();
        if (index >= 0 && index < PlayerSlots.Length)
            PlayerSlots[index] = itemId;
    }

    public void RestoreShip(int index, string itemId)
    {
        Normalize();
        if (index >= 0 && index < ShipSlots.Length)
            ShipSlots[index] = itemId;
    }

    public void SetEquipment(int index, string itemId)
    {
        Normalize();
        if (index >= 0 && index < EquipmentSlots.Length)
            EquipmentSlots[index] = itemId;
    }

    public void SetCrafting(int index, string itemId)
    {
        Normalize();
        if (index >= 0 && index < CraftingSlots.Length)
            CraftingSlots[index] = itemId;
    }

    public bool IsEquipmentSlotEnabled(int slotIndex, int shipSkinIndex)
    {
        Normalize();
        if (slotIndex < 0 || slotIndex >= EquipmentSlots.Length)
            return false;

        return ShipCatalog.IsEquipmentSlotEnabled(slotIndex, shipSkinIndex);
    }

    public void SetShipSlots(string[] source)
    {
        Normalize();
        ShipSlots = new string[ShipSlotCount];
        CopyInto(source, ShipSlots);
    }

    public void ExtendPlayerSlots(int extraSlots)
    {
        Normalize();
        int safeExtraSlots = Mathf.Max(0, extraSlots);
        if (safeExtraSlots == 0)
            return;

        string[] old = PlayerSlots;
        PlayerSlots = new string[old.Length + safeExtraSlots];
        CopyInto(old, PlayerSlots);
    }

    static int FindFirstEmpty(string[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(slots[i]))
                return i;
        }

        return -1;
    }

    static int FindFirstEmpty(string[] slots, int capacity)
    {
        int safeCapacity = Mathf.Clamp(capacity, 0, slots != null ? slots.Length : 0);
        for (int i = 0; i < safeCapacity; i++)
        {
            if (string.IsNullOrWhiteSpace(slots[i]))
                return i;
        }

        return -1;
    }

    static void CopyInto(string[] source, string[] destination)
    {
        if (source == null)
            return;

        int count = Math.Min(source.Length, destination.Length);
        for (int i = 0; i < count; i++)
        {
            destination[i] = source[i];
        }
    }
}
