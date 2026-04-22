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
    const string CloudNicknameKey = "profile_nickname";
    const string CloudShipSkinKey = "profile_ship_skin";
    const string CloudGamesPlayedKey = "profile_games_played";
    const string CloudTotalXpKey = "profile_total_xp";
    const string CloudAstronsKey = "profile_astrons";
    const string CloudInventoryKey = "profile_inventory";

    static PlayerProfileService instance;
    Task initializationTask;
    bool initialized;
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
    public string PlayerId => AuthenticationService.Instance?.PlayerId;
    public PlayerProfileData CurrentProfile { get; private set; } = PlayerProfileData.Default();

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
            CloudInventoryKey
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

        if (data != null)
        {
            if (data.TryGetValue(CloudNicknameKey, out Item nicknameItem) && nicknameItem?.Value != null)
                nickname = nicknameItem.Value.GetAsString();

            if (data.TryGetValue(CloudShipSkinKey, out Item skinItem) && skinItem?.Value != null)
                shipSkinIndex = Mathf.Clamp(skinItem.Value.GetAs<int>(), 0, 3);

            if (data.TryGetValue(CloudGamesPlayedKey, out Item gamesItem) && gamesItem?.Value != null)
                gamesPlayed = Mathf.Max(0, gamesItem.Value.GetAs<int>());

            if (data.TryGetValue(CloudTotalXpKey, out Item totalXpItem) && totalXpItem?.Value != null)
                totalXp = Mathf.Max(0, totalXpItem.Value.GetAs<int>());

            if (data.TryGetValue(CloudAstronsKey, out Item astronsItem) && astronsItem?.Value != null)
                astrons = Mathf.Max(0, astronsItem.Value.GetAs<int>());

            if (data.TryGetValue(CloudInventoryKey, out Item inventoryItem) && inventoryItem?.Value != null)
                inventory = DeserializeInventory(inventoryItem.Value.GetAsString());
        }

        if (string.IsNullOrWhiteSpace(nickname))
            nickname = BuildFallbackProfile().Nickname;

        CurrentProfile = new PlayerProfileData
        {
            Nickname = SanitizeNickname(nickname),
            ShipSkinIndex = Mathf.Clamp(shipSkinIndex, 0, 3),
            GamesPlayed = gamesPlayed,
            TotalXp = totalXp,
            Astrons = astrons,
            Inventory = inventory
        };

        ApplyProfileToPhoton();
        NotifyProfileChanged();
    }

    public async Task SaveProfileAsync(string nickname, int shipSkinIndex)
    {
        await EnsureInitializedAsync();

        try
        {
            IsBusy = true;

            CurrentProfile = new PlayerProfileData
            {
                Nickname = SanitizeNickname(nickname),
                ShipSkinIndex = Mathf.Clamp(shipSkinIndex, 0, 3),
                GamesPlayed = CurrentProfile != null ? CurrentProfile.GamesPlayed : 0,
                TotalXp = CurrentProfile != null ? CurrentProfile.TotalXp : 0,
                Astrons = CurrentProfile != null ? CurrentProfile.Astrons : 0,
                Inventory = CurrentProfile != null && CurrentProfile.Inventory != null ? CurrentProfile.Inventory.Clone() : PlayerInventoryData.Default()
            };

            var data = new Dictionary<string, object>
            {
                [CloudNicknameKey] = CurrentProfile.Nickname,
                [CloudShipSkinKey] = CurrentProfile.ShipSkinIndex,
                [CloudGamesPlayedKey] = CurrentProfile.GamesPlayed,
                [CloudTotalXpKey] = CurrentProfile.TotalXp,
                [CloudAstronsKey] = CurrentProfile.Astrons,
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save profile");
            ApplyProfileToPhoton();
            NotifyProfileChanged();
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

    public void ApplyProfileToPhoton()
    {
        if (CurrentProfile == null)
            return;

        if (!string.IsNullOrWhiteSpace(CurrentProfile.Nickname))
            PhotonNetwork.NickName = CurrentProfile.Nickname;

        if (PhotonNetwork.LocalPlayer == null)
            return;

        EnsureInventory();

        var props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.ShipSkinKey] = CurrentProfile.ShipSkinIndex,
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

    public bool HasFreeShipInventorySlot()
    {
        EnsureInventory();
        return CurrentProfile.Inventory.GetFirstEmptyShipSlot(ShipCatalog.GetShipInventoryCapacity(CurrentProfile.ShipSkinIndex)) >= 0;
    }

    public async Task ReplaceShipInventoryAsync(string[] newShipSlots)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        CurrentProfile.Inventory.SetShipSlots(newShipSlots);
        await SaveInventoryOnlyAsync();
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
            : CurrentProfile.Inventory.TryAddToShip(movedItem, ShipCatalog.GetShipInventoryCapacity(CurrentProfile.ShipSkinIndex));

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
        CurrentProfile.Astrons = Mathf.Max(0, CurrentProfile.Astrons + value);
        await SaveInventoryAndAstronsAsync();
        return true;
    }

    public async Task<bool> SalvageInventoryItemAsync(bool fromShipInventory, int sourceIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        string sourceItem = fromShipInventory
            ? workingInventory.RemoveFromShip(sourceIndex)
            : workingInventory.RemoveFromPlayer(sourceIndex);

        if (string.IsNullOrWhiteSpace(sourceItem))
            return false;

        string[] salvageOutputs = InventoryItemCatalog.GetSalvageOutputs(sourceItem);
        if (salvageOutputs == null || salvageOutputs.Length == 0)
            return false;

        for (int i = 0; i < salvageOutputs.Length; i++)
        {
            string output = salvageOutputs[i];
            bool added = fromShipInventory
                ? workingInventory.TryAddToShip(output, ShipCatalog.GetShipInventoryCapacity(CurrentProfile.ShipSkinIndex))
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

        string movedItem = fromShipInventory
            ? CurrentProfile.Inventory.RemoveFromShip(sourceIndex)
            : CurrentProfile.Inventory.RemoveFromPlayer(sourceIndex);

        if (string.IsNullOrWhiteSpace(movedItem))
            return false;

        string replacedItem = CurrentProfile.Inventory.RemoveFromEquipment(equipmentSlotIndex);
        CurrentProfile.Inventory.SetEquipment(equipmentSlotIndex, movedItem);

        if (!string.IsNullOrWhiteSpace(replacedItem))
        {
            bool restored = fromShipInventory
                ? CurrentProfile.Inventory.TryAddToShip(replacedItem, ShipCatalog.GetShipInventoryCapacity(shipSkinIndex))
                : CurrentProfile.Inventory.TryAddToPlayer(replacedItem);

            if (!restored)
            {
                CurrentProfile.Inventory.SetEquipment(equipmentSlotIndex, replacedItem);
                RestoreInventorySource(fromShipInventory, sourceIndex, movedItem);
                return false;
            }
        }

        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> MoveEquipmentItemToInventoryAsync(int equipmentSlotIndex, bool toPlayerInventory, int shipSkinIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (!CurrentProfile.Inventory.IsEquipmentSlotEnabled(equipmentSlotIndex, shipSkinIndex))
            return false;

        string movedItem = CurrentProfile.Inventory.RemoveFromEquipment(equipmentSlotIndex);
        if (string.IsNullOrWhiteSpace(movedItem))
            return false;

        bool moved = toPlayerInventory
            ? CurrentProfile.Inventory.TryAddToPlayer(movedItem)
            : CurrentProfile.Inventory.TryAddToShip(movedItem, ShipCatalog.GetShipInventoryCapacity(shipSkinIndex));

        if (!moved)
        {
            CurrentProfile.Inventory.SetEquipment(equipmentSlotIndex, movedItem);
            return false;
        }

        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> AddItemToShipAsync(string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (!CurrentProfile.Inventory.TryAddToShip(itemId, ShipCatalog.GetShipInventoryCapacity(CurrentProfile.ShipSkinIndex)))
            return false;

        await SaveInventoryOnlyAsync();
        return true;
    }

    public async Task<bool> TryChangeShipSkinAsync(int newShipSkinIndex)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        int clampedSkin = Mathf.Clamp(newShipSkinIndex, 0, 3);
        if (CurrentProfile.ShipSkinIndex == clampedSkin)
            return true;

        PlayerInventoryData workingInventory = CurrentProfile.Inventory.Clone();
        int newCapacity = ShipCatalog.GetShipInventoryCapacity(clampedSkin);

        for (int i = newCapacity; i < workingInventory.ShipSlots.Length; i++)
        {
            string itemId = workingInventory.ShipSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!workingInventory.TryAddToPlayer(itemId))
                return false;

            workingInventory.ShipSlots[i] = null;
        }

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

    public async Task RestoreShipItemAtAsync(int index, string itemId)
    {
        await EnsureInitializedAsync();
        EnsureInventory();

        if (string.IsNullOrWhiteSpace(itemId))
            return;

        CurrentProfile.Inventory.RestoreShip(index, itemId);
        await SaveInventoryOnlyAsync();
    }

    public async Task SaveInventorySnapshotAsync(PlayerInventoryData inventory)
    {
        await EnsureInitializedAsync();
        EnsureInventory();
        CurrentProfile.Inventory = inventory != null ? inventory.Clone() : PlayerInventoryData.Default();
        await SaveInventoryOnlyAsync();
    }

    async Task SaveInventoryOnlyAsync()
    {
        try
        {
            IsBusy = true;

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory)
            };

            await RunCloudOperationWithRetryAsync(
                () => CloudSaveService.Instance.Data.Player.SaveAsync(data),
                "save inventory");
            ApplyProfileToPhoton();
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

    async Task SaveShipSkinAndInventoryAsync()
    {
        try
        {
            IsBusy = true;

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

    async Task SaveInventoryAndAstronsAsync()
    {
        try
        {
            IsBusy = true;

            var data = new Dictionary<string, object>
            {
                [CloudInventoryKey] = SerializeInventory(CurrentProfile.Inventory),
                [CloudAstronsKey] = CurrentProfile.Astrons
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

    public static bool PlayerHasFreeShipInventorySlot(Photon.Realtime.Player player)
    {
        string[] slots = GetPlayerShipInventorySlots(player);
        int capacity = player != null ? ShipCatalog.GetShipInventoryCapacity(RoomSettings.GetPlayerShipSkin(player, 0)) : PlayerInventoryData.ShipSlotCount;
        for (int i = 0; i < slots.Length && i < capacity; i++)
        {
            if (string.IsNullOrWhiteSpace(slots[i]))
                return true;
        }

        return false;
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

    PlayerProfileData BuildFallbackProfile()
    {
        string suffix = "0000";
        string playerId = AuthenticationService.Instance != null ? AuthenticationService.Instance.PlayerId : string.Empty;

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
            Inventory = PlayerInventoryData.Default()
        };
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

    public static PlayerProfileData Default()
    {
        return new PlayerProfileData
        {
            Nickname = "Pilot",
            ShipSkinIndex = 0,
            GamesPlayed = 0,
            TotalXp = 0,
            Astrons = 0,
            Inventory = PlayerInventoryData.Default()
        };
    }
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
    public const int PlayerSlotCount = 30;
    public const int ShipSlotCount = 10;
    public const int EquipmentSlotCount = 6;
    public const int CraftingSlotCount = 4;
    public string[] PlayerSlots;
    public string[] ShipSlots;
    public string[] EquipmentSlots;
    public string[] CraftingSlots;

    public static PlayerInventoryData Default()
    {
        return new PlayerInventoryData
        {
            PlayerSlots = new string[PlayerSlotCount],
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
        if (PlayerSlots == null || PlayerSlots.Length != PlayerSlotCount)
        {
            string[] old = PlayerSlots;
            PlayerSlots = new string[PlayerSlotCount];
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

        return slotIndex switch
        {
            0 => ShipCatalog.GetMainGunSlots(shipSkinIndex) >= 1,
            1 => ShipCatalog.GetMainGunSlots(shipSkinIndex) >= 2,
            2 => ShipCatalog.GetShieldSlots(shipSkinIndex) >= 1,
            3 => ShipCatalog.GetEngineSlots(shipSkinIndex) >= 1,
            4 => ShipCatalog.GetEngineSlots(shipSkinIndex) >= 2,
            5 => ShipCatalog.GetGadgetSlots(shipSkinIndex) >= 1,
            _ => false
        };
    }

    public void SetShipSlots(string[] source)
    {
        Normalize();
        ShipSlots = new string[ShipSlotCount];
        CopyInto(source, ShipSlots);
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
