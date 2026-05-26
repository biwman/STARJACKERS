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
    public const int DefaultAstronautCargoSlotCount = 1;
    const string CloudNicknameKey = "profile_nickname";
    const string CloudShipSkinKey = "profile_ship_skin";
    const string CloudGamesPlayedKey = "profile_games_played";
    const string CloudTotalXpKey = "profile_total_xp";
    const string CloudAstronsKey = "profile_astrons";
    const string CloudInventoryKey = "profile_inventory";
    const string CloudSelectedPilotKey = "profile_selected_pilot";
    const string CloudUnlockedPilotsKey = "profile_unlocked_pilots";
    const string CloudPilotDroneKillsKey = "profile_pilot_drone_kills";
    const string CloudPilotSoldItemsAstronsKey = "profile_pilot_sold_items_astrons";
    const string CloudPilotPirateBayReturnsKey = "profile_pilot_pirate_bay_returns";
    const string CloudProjectsKey = "profile_projects";

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
            CloudPilotDroneKillsKey,
            CloudPilotSoldItemsAstronsKey,
            CloudPilotPirateBayReturnsKey,
            CloudProjectsKey
        };
        Dictionary<string, Item> data = await RunCloudOperationWithRetryAsync(
            () => CloudSaveService.Instance.Data.Player.LoadAsync(keys),
            "load profile");

        string nickname = null;
        int shipSkinIndex = 0;
        int gamesPlayed = 0;
        int totalXp = 0;
        int astrons = 0;
        PlayerInventoryData inventory = PlayerInventoryData.Default();
        string selectedPilotId = PilotCatalog.JakeId;
        string[] unlockedPilotIds = PilotCatalog.GetDefaultUnlockedPilotIds();
        int pilotDroneKills = 0;
        int pilotSoldItemsAstrons = 0;
        int pilotPirateBayReturns = 0;
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

            if (data.TryGetValue(CloudPilotDroneKillsKey, out Item droneKillsItem) && droneKillsItem?.Value != null)
                pilotDroneKills = Mathf.Max(0, droneKillsItem.Value.GetAs<int>());

            if (data.TryGetValue(CloudPilotSoldItemsAstronsKey, out Item soldItemsAstronsItem) && soldItemsAstronsItem?.Value != null)
                pilotSoldItemsAstrons = Mathf.Max(0, soldItemsAstronsItem.Value.GetAs<int>());

            if (data.TryGetValue(CloudPilotPirateBayReturnsKey, out Item pirateBayReturnsItem) && pirateBayReturnsItem?.Value != null)
                pilotPirateBayReturns = Mathf.Max(0, pirateBayReturnsItem.Value.GetAs<int>());

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
            PilotDroneKills = pilotDroneKills,
            PilotSoldItemsAstrons = pilotSoldItemsAstrons,
            PilotPirateBayReturns = pilotPirateBayReturns,
            ProjectProgress = ProjectCatalog.NormalizeProgress(projectProgress)
        };

        EnsurePilotDefaults();
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
            Astrons = CurrentProfile != null ? CurrentProfile.Astrons : 0,
            Inventory = CurrentProfile != null && CurrentProfile.Inventory != null ? CurrentProfile.Inventory.Clone() : PlayerInventoryData.Default(),
            SelectedPilotId = CurrentProfile != null ? PilotCatalog.NormalizePilotId(CurrentProfile.SelectedPilotId) : PilotCatalog.JakeId,
            UnlockedPilotIds = CurrentProfile != null ? PilotCatalog.NormalizeUnlockedPilotIds(CurrentProfile.UnlockedPilotIds) : PilotCatalog.GetDefaultUnlockedPilotIds(),
            PilotDroneKills = CurrentProfile != null ? Mathf.Max(0, CurrentProfile.PilotDroneKills) : 0,
            PilotSoldItemsAstrons = CurrentProfile != null ? Mathf.Max(0, CurrentProfile.PilotSoldItemsAstrons) : 0,
            PilotPirateBayReturns = CurrentProfile != null ? Mathf.Max(0, CurrentProfile.PilotPirateBayReturns) : 0,
            ProjectProgress = CurrentProfile != null ? ProjectCatalog.NormalizeProgress(CurrentProfile.ProjectProgress) : ProjectCatalog.NormalizeProgress(null)
        };

        EnsurePilotDefaults();
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
                [CloudPilotDroneKillsKey] = CurrentProfile.PilotDroneKills,
                [CloudPilotSoldItemsAstronsKey] = CurrentProfile.PilotSoldItemsAstrons,
                [CloudPilotPirateBayReturnsKey] = CurrentProfile.PilotPirateBayReturns,
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

        var props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.ShipSkinKey] = CurrentProfile.ShipSkinIndex,
            [RoomSettings.PilotIdKey] = CurrentProfile.SelectedPilotId,
            [RoomSettings.ShipInventoryStateKey] = SerializeShipInventorySlots(CurrentProfile.Inventory.ShipSlots),
            [RoomSettings.EquipmentStateKey] = SerializeEquipmentSlots(CurrentProfile.Inventory.EquipmentSlots)
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
                Astrons = 0,
                Inventory = PlayerInventoryData.Default(),
                SelectedPilotId = PilotCatalog.JakeId,
                UnlockedPilotIds = PilotCatalog.GetDefaultUnlockedPilotIds(),
                PilotDroneKills = 0,
                PilotSoldItemsAstrons = 0,
                PilotPirateBayReturns = 0,
                ProjectProgress = ProjectCatalog.NormalizeProgress(null)
            };

            awardedMatchTokens.Clear();
            EnsureInventory();
            EnsurePilotDefaults();
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
                [CloudPilotDroneKillsKey] = CurrentProfile.PilotDroneKills,
                [CloudPilotSoldItemsAstronsKey] = CurrentProfile.PilotSoldItemsAstrons,
                [CloudPilotPirateBayReturnsKey] = CurrentProfile.PilotPirateBayReturns,
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

    public async Task ReplaceShipInventoryAsync(string[] newShipSlots)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        CurrentProfile.Inventory.SetShipSlots(newShipSlots);
        TryMoveSafePocketRestrictedItems(CurrentProfile.Inventory, GetActiveShipSkinIndex());
        await SaveInventoryOnlyAsync();
    }

    public async Task ApplyShipLossAsync(int shipSkinIndex, bool loseShipInventory, bool loseEquipment, string serializedAstronautCargo = null, string protectedEquipmentItemId = null)
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
            CurrentProfile.Inventory.EquipmentSlots = new string[PlayerInventoryData.EquipmentSlotCount];
            changed = true;
        }
        else
        {
            ClearPendingProtectedEquipment();
        }

        if (changed)
            await SaveInventoryOnlyAsync();
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

        EnsurePilotDefaults();
        if (CurrentProfile != null && string.Equals(CurrentProfile.SelectedPilotId, PilotCatalog.CharlieSmartId, StringComparison.Ordinal))
            return Mathf.Max(1, Mathf.CeilToInt(basePrice * 0.95f));

        return basePrice;
    }

    public async Task<bool> TryBuyShopItemAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return false;

        int price = GetShopBuyPriceAstrons(itemId);
        if (price <= 0 || CurrentProfile.Astrons < price)
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        bool stored = workingInventory.TryAddToPlayer(itemId);
        if (!stored)
            stored = workingInventory.TryAddToShip(itemId, GetEffectiveShipInventoryCapacity(CurrentProfile.ShipSkinIndex, workingInventory.EquipmentSlots), CurrentProfile.ShipSkinIndex);

        if (!stored)
            return false;

        CurrentProfile.Astrons = Mathf.Max(0, CurrentProfile.Astrons - price);
        CurrentProfile.Inventory = workingInventory;
        await SaveInventoryAndAstronsAsync();
        return true;
    }

    public async Task<bool> SalvageInventoryItemAsync(bool fromShipInventory, int sourceIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        string sourceItemPreview = fromShipInventory
            ? GetSlotItem(workingInventory.ShipSlots, sourceIndex)
            : GetSlotItem(workingInventory.PlayerSlots, sourceIndex);

        string[] salvageOutputs = InventoryItemCatalog.GetSalvageOutputs(sourceItemPreview);
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
                ? workingInventory.TryAddToShip(output, GetEffectiveShipInventoryCapacity(CurrentProfile.ShipSkinIndex, workingInventory.EquipmentSlots), CurrentProfile.ShipSkinIndex)
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

        if (!CurrentProfile.Inventory.IsEquipmentSlotEnabled(equipmentSlotIndex, shipSkinIndex))
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        string movedItem = fromShipInventory
            ? GetSlotItem(workingInventory.ShipSlots, sourceIndex)
            : GetSlotItem(workingInventory.PlayerSlots, sourceIndex);

        if (!InventoryItemCatalog.IsCompatibleWithEquipmentSlot(movedItem, equipmentSlotIndex))
            return false;

        if ((string.Equals(movedItem, InventoryItemCatalog.LootingFriendId, StringComparison.Ordinal) ||
             string.Equals(movedItem, InventoryItemCatalog.EscapePodId, StringComparison.Ordinal)) &&
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
                ? workingInventory.TryAddToShip(replacedItem, GetEffectiveShipInventoryCapacity(shipSkinIndex, workingInventory.EquipmentSlots), shipSkinIndex)
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

    bool IsItemAlreadyEquipped(string[] equipmentSlots, string itemId, int ignoredSlotIndex)
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

    bool TryMoveOverflowShipCargoToPlayer(PlayerInventoryData inventory, int shipSkinIndex)
    {
        if (inventory == null)
            return false;

        inventory.Normalize();
        int capacity = GetEffectiveShipInventoryCapacity(shipSkinIndex, inventory.EquipmentSlots);
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

        if (!CurrentProfile.Inventory.IsEquipmentSlotEnabled(equipmentSlotIndex, shipSkinIndex))
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        string movedItem = workingInventory.RemoveFromEquipment(equipmentSlotIndex);
        if (string.IsNullOrWhiteSpace(movedItem))
            return false;

        if (!TryMoveOverflowShipCargoToPlayer(workingInventory, shipSkinIndex))
            return false;

        bool moved = toPlayerInventory
            ? workingInventory.TryAddToPlayer(movedItem)
            : workingInventory.TryAddToShip(movedItem, GetEffectiveShipInventoryCapacity(shipSkinIndex, workingInventory.EquipmentSlots), shipSkinIndex);

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

    public async Task<bool> AddRandomLootEquipmentAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
        if (definition == null || definition.ItemType != InventoryItemType.Equipment)
            return false;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        int shipSkinIndex = GetActiveShipSkinIndex();

        if (definition.Category == InventoryItemCategory.Gadget)
        {
            int gadgetSlot = GetFirstFreeGadgetEquipmentSlot(workingInventory.EquipmentSlots, shipSkinIndex);
            if (gadgetSlot >= 0 &&
                (!string.Equals(itemId, InventoryItemCatalog.LootingFriendId, StringComparison.Ordinal) ||
                 !IsItemAlreadyEquipped(workingInventory.EquipmentSlots, itemId, gadgetSlot)))
            {
                workingInventory.SetEquipment(gadgetSlot, itemId);
                CurrentProfile.Inventory = workingInventory;
                await SaveInventoryOnlyAsync();
                return true;
            }
        }

        if (!workingInventory.TryAddToShip(itemId, GetEffectiveShipInventoryCapacity(shipSkinIndex, workingInventory.EquipmentSlots), shipSkinIndex))
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

        if (definition.Category == InventoryItemCategory.Gadget)
        {
            int gadgetSlot = GetFirstFreeGadgetEquipmentSlot(workingInventory.EquipmentSlots, shipSkinIndex);
            if (gadgetSlot >= 0 &&
                (!string.Equals(itemId, InventoryItemCatalog.LootingFriendId, StringComparison.Ordinal) ||
                 !IsItemAlreadyEquipped(workingInventory.EquipmentSlots, itemId, gadgetSlot)))
            {
                workingInventory.SetEquipment(gadgetSlot, itemId);
                CurrentProfile.Inventory = workingInventory;
                MarkInventoryChangedDeferred();
                return true;
            }
        }

        if (!workingInventory.TryAddToShip(itemId, GetEffectiveShipInventoryCapacity(shipSkinIndex, workingInventory.EquipmentSlots), shipSkinIndex))
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
        if (CurrentProfile.ShipSkinIndex == clampedSkin)
            return true;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        int newCapacity = GetEffectiveShipInventoryCapacity(clampedSkin, workingInventory.EquipmentSlots);

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
            if (workingInventory.IsEquipmentSlotEnabled(i, clampedSkin))
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
                [CloudPilotPirateBayReturnsKey] = CurrentProfile.PilotPirateBayReturns
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
        return GetFirstFreeGadgetEquipmentSlot(GetPlayerEquipmentSlots(player), shipSkinIndex) >= 0;
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

    public static int GetPlayerShipInventoryCapacity(Photon.Realtime.Player player)
    {
        int shipSkinIndex = player != null ? RoomSettings.GetPlayerShipSkin(player, 0) : 0;
        return GetEffectiveShipInventoryCapacity(shipSkinIndex, GetPlayerEquipmentSlots(player));
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

    public static bool CanStoreItemInShipSlot(string itemId, int shipSkinIndex, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return true;

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
            if (IsSafePocketIndex(shipSkinIndex, i) && InventoryItemCatalog.CanEnterSafePocket(normalized[i]))
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
            if (!IsSafePocketIndex(shipSkinIndex, i) || !InventoryItemCatalog.CanEnterSafePocket(normalized[i]))
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

    static int GetFirstFreeGadgetEquipmentSlot(string[] sourceSlots, int shipSkinIndex)
    {
        string[] slots = NormalizeEquipmentSlots(sourceSlots);
        for (int i = 0; i < slots.Length; i++)
        {
            if (InventoryItemCatalog.GetEquipmentSlotCategory(i) != InventoryItemCategory.Gadget)
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

        int fallbackSlot = GetFirstFreeGadgetEquipmentSlot(CurrentProfile.Inventory.EquipmentSlots, shipSkinIndex);
        if (TryRestoreEquipmentToSlot(itemId, fallbackSlot, shipSkinIndex))
            return true;

        int capacity = GetEffectiveShipInventoryCapacity(shipSkinIndex, CurrentProfile.Inventory.EquipmentSlots);
        if (CurrentProfile.Inventory.TryAddToShip(itemId, capacity, shipSkinIndex))
            return true;

        if (CurrentProfile.Inventory.TryAddToPlayer(itemId))
            return true;

        Debug.LogWarning("Protected equipment could not be restored after evacuation: " + itemId);
        return false;
    }

    bool TryRestoreEquipmentToSlot(string itemId, int slotIndex, int shipSkinIndex)
    {
        if (string.IsNullOrWhiteSpace(itemId) ||
            slotIndex < 0 ||
            slotIndex >= PlayerInventoryData.EquipmentSlotCount ||
            !ShipCatalog.IsEquipmentSlotEnabled(slotIndex, shipSkinIndex) ||
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
            Astrons = 0,
            Inventory = PlayerInventoryData.Default(),
            SelectedPilotId = PilotCatalog.JakeId,
            UnlockedPilotIds = PilotCatalog.GetDefaultUnlockedPilotIds(),
            PilotDroneKills = 0,
            PilotSoldItemsAstrons = 0,
            PilotPirateBayReturns = 0,
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
    }

    static bool TryMoveSafePocketRestrictedItems(PlayerInventoryData inventory, int shipSkinIndex)
    {
        if (inventory == null)
            return true;

        inventory.Normalize();
        int capacity = ShipCatalog.GetShipInventoryCapacity(shipSkinIndex);
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

    void EnsurePilotDefaults()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.UnlockedPilotIds = PilotCatalog.NormalizeUnlockedPilotIds(CurrentProfile.UnlockedPilotIds);
        CurrentProfile.PilotDroneKills = Mathf.Max(0, CurrentProfile.PilotDroneKills);
        CurrentProfile.PilotSoldItemsAstrons = Mathf.Max(0, CurrentProfile.PilotSoldItemsAstrons);
        CurrentProfile.PilotPirateBayReturns = Mathf.Max(0, CurrentProfile.PilotPirateBayReturns);
        CurrentProfile.SelectedPilotId = PilotCatalog.NormalizePilotId(CurrentProfile.SelectedPilotId);
        if (!PilotCatalog.IsPilotUnlocked(CurrentProfile, CurrentProfile.SelectedPilotId))
            CurrentProfile.SelectedPilotId = PilotCatalog.JakeId;
    }

    void EnsureProjectProgress()
    {
        if (CurrentProfile == null)
            CurrentProfile = PlayerProfileData.Default();

        CurrentProfile.ProjectProgress = ProjectCatalog.NormalizeProgress(CurrentProfile.ProjectProgress);
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
        return GetEffectiveShipInventoryCapacity(
            GetActiveShipSkinIndex(),
            CurrentProfile != null && CurrentProfile.Inventory != null ? CurrentProfile.Inventory.EquipmentSlots : null);
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
            [RoomSettings.EquipmentStateKey] = SerializeEquipmentSlots(CurrentProfile.Inventory.EquipmentSlots)
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
    public int PilotDroneKills;
    public int PilotSoldItemsAstrons;
    public int PilotPirateBayReturns;
    public PlayerProjectProgressData ProjectProgress;

    public static PlayerProfileData Default()
    {
        return new PlayerProfileData
        {
            Nickname = "Pilot",
            ShipSkinIndex = 0,
            GamesPlayed = 0,
            TotalXp = 0,
            Astrons = 0,
            Inventory = PlayerInventoryData.Default(),
            SelectedPilotId = PilotCatalog.JakeId,
            UnlockedPilotIds = PilotCatalog.GetDefaultUnlockedPilotIds(),
            PilotDroneKills = 0,
            PilotSoldItemsAstrons = 0,
            PilotPirateBayReturns = 0,
            ProjectProgress = ProjectCatalog.NormalizeProgress(null)
        };
    }
}

[Serializable]
public class PilotUnlockSnapshot
{
    public string[] pilotIds;
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
    public const int PlayerSlotExtensionSize = 10;
    public const int ShipSlotCount = 10;
    public const int EquipmentSlotCount = 8;
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
