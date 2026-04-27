using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    const float BrowserJoinLobbyWatchdogSeconds = 8f;
    const float BrowserRecoveryPollSeconds = 1f;
    const float LeaveRoomRetryPollSeconds = 0.5f;
    const float LeaveRoomRetryTimeoutSeconds = 6f;
    const string RememberedLobbySettingsPrefsKey = "BrawlRaiders.LastLobbySettings.v1";

    enum PendingBrowserAction
    {
        None,
        CreateRoom,
        JoinRoom
    }

    public sealed class SessionRoomEntry
    {
        public string RoomName;
        public string DisplayName;
        public string State;
        public string HostName;
        public int PlayerCount;
        public int MaxPlayers;
        public double CreatedAt;
        public float? RemainingTimeSeconds;
        public bool CanJoin;
    }

    [Serializable]
    sealed class RememberedLobbySettingsData
    {
        public RememberedLobbySettingEntry[] entries;
    }

    [Serializable]
    sealed class RememberedLobbySettingEntry
    {
        public string key;
        public string type;
        public string stringValue;
        public int intValue;
        public float floatValue;
        public bool boolValue;
    }

    static readonly string[] LobbyVisibleRoomKeys =
    {
        RoomSettings.SessionStateKey,
        RoomSettings.SessionLabelKey,
        RoomSettings.SessionHostNameKey,
        RoomSettings.SessionCreatedAtKey,
        RoomSettings.RoundDurationKey,
        RoomSettings.StartTimeKey
    };

    static NetworkManager instance;

    readonly Dictionary<string, RoomInfo> roomListCache = new Dictionary<string, RoomInfo>(StringComparer.Ordinal);

    PendingBrowserAction pendingBrowserAction = PendingBrowserAction.None;
    string pendingRoomName;
    string latestBrowserStatus = string.Empty;
    bool returnToBrowserAfterLeave;
    string leaveRoomBrowserStatus = string.Empty;
    Coroutine sessionBrowserRecoveryRoutine;
    Coroutine leaveRoomRetryRoutine;
    float joiningLobbyStartedAt = -1f;
    bool reconnectingForBrowserRecovery;
    bool preservePendingBrowserActionAfterLeave;

    public static bool SessionRequested { get; private set; }
    public static event Action<IReadOnlyList<SessionRoomEntry>> SessionRoomListChanged;
    public static event Action<string> SessionBrowserStatusChanged;

    public static bool IsSessionBrowserReady =>
        instance != null &&
        PhotonNetwork.IsConnectedAndReady &&
        PhotonNetwork.InLobby &&
        !PhotonNetwork.InRoom;

    public static void RequestSessionStart()
    {
        SessionRequested = true;
        if (instance == null)
        {
            instance = FindAnyObjectByType<NetworkManager>();
        }

        if (instance != null)
        {
            instance.pendingBrowserAction = PendingBrowserAction.None;
            instance.pendingRoomName = null;
            if (PhotonNetwork.InRoom)
            {
                SessionBrowserPanelUI.ShowBrowser();
                instance.LeaveCurrentRoomToSessionBrowser("Returning to active rounds...");
                return;
            }

            instance.BeginSessionBrowserFlow();
        }
        else
        {
            Debug.LogWarning("NetworkManager instance not found when requesting session browser.");
        }
    }

    public static void CancelSessionStart()
    {
        SessionRequested = false;
        if (instance == null)
            return;

        instance.pendingBrowserAction = PendingBrowserAction.None;
        instance.pendingRoomName = null;
        instance.PublishBrowserStatus(string.Empty);
    }

    public static IReadOnlyList<SessionRoomEntry> GetSessionRooms()
    {
        if (instance == null)
            return Array.Empty<SessionRoomEntry>();

        return instance.BuildSessionRoomEntries();
    }

    public static string GetSessionBrowserStatus()
    {
        return instance != null ? instance.latestBrowserStatus : string.Empty;
    }

    public static void RefreshSessionBrowser()
    {
        RequestSessionStart();
    }

    public static void CreateNewRound()
    {
        SessionRequested = true;
        if (instance == null)
        {
            instance = FindAnyObjectByType<NetworkManager>();
        }

        if (instance == null)
        {
            Debug.LogWarning("NetworkManager instance not found when creating a room.");
            return;
        }

        instance.pendingBrowserAction = PendingBrowserAction.CreateRoom;
        instance.pendingRoomName = null;
        if (PhotonNetwork.InRoom)
        {
            SessionBrowserPanelUI.ShowBrowser();
            instance.LeaveCurrentRoomToSessionBrowser("Leaving current round...", true);
            return;
        }

        instance.BeginSessionBrowserFlow();
    }

    public static void JoinSession(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return;

        SessionRequested = true;
        if (instance == null)
        {
            instance = FindAnyObjectByType<NetworkManager>();
        }

        if (instance == null)
        {
            Debug.LogWarning("NetworkManager instance not found when joining a room.");
            return;
        }

        instance.pendingBrowserAction = PendingBrowserAction.JoinRoom;
        instance.pendingRoomName = roomName;
        if (PhotonNetwork.InRoom)
        {
            SessionBrowserPanelUI.ShowBrowser();
            instance.LeaveCurrentRoomToSessionBrowser("Leaving current round...", true);
            return;
        }

        instance.BeginSessionBrowserFlow();
    }

    public static void ReturnToSessionBrowserFromLobby()
    {
        RememberCurrentLobbySettings();

        SessionRequested = true;
        if (instance == null)
            instance = FindAnyObjectByType<NetworkManager>();

        if (instance == null)
        {
            Debug.LogWarning("NetworkManager instance not found when returning to the session browser.");
            return;
        }

        SessionBrowserPanelUI.ShowBrowser();
        instance.ReturnToSessionBrowserFromCurrentLobby();
    }

    public static void ReturnToSessionBrowserFromRound()
    {
        RememberCurrentLobbySettings();

        SessionRequested = true;
        if (instance == null)
            instance = FindAnyObjectByType<NetworkManager>();

        if (instance == null)
        {
            Debug.LogWarning("NetworkManager instance not found when returning from the round.");
            return;
        }

        SessionBrowserPanelUI.ShowBrowser();
        instance.LeaveCurrentRoomToSessionBrowser("Returning to active rounds...");
    }

    public static void RememberCurrentLobbySettings()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        List<RememberedLobbySettingEntry> entries = new List<RememberedLobbySettingEntry>();
        foreach (System.Collections.DictionaryEntry entry in PhotonNetwork.CurrentRoom.CustomProperties)
        {
            if (entry.Key is not string key || !IsRememberedLobbySettingKey(key))
                continue;

            if (TryCreateRememberedLobbySettingEntry(key, entry.Value, out RememberedLobbySettingEntry rememberedEntry))
                entries.Add(rememberedEntry);
        }

        if (entries.Count == 0)
            return;

        RememberedLobbySettingsData data = new RememberedLobbySettingsData
        {
            entries = entries.ToArray()
        };

        PlayerPrefs.SetString(RememberedLobbySettingsPrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    void Awake()
    {
        instance = this;
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            Debug.Log("Already in room, restoring scene state...");
            RestoreRoomStateAfterSceneLoad();
            return;
        }

        PhotonNetwork.GameVersion = Application.version;
        Debug.Log("Game Version: " + PhotonNetwork.GameVersion);

        if (SessionRequested)
        {
            BeginSessionBrowserFlow();
        }
    }

    void BeginSessionBrowserFlow()
    {
        EnsureSessionBrowserRecoveryRoutine();

        if (PhotonNetwork.InRoom)
            return;

        if (PhotonNetwork.IsConnectedAndReady)
        {
            if (PhotonNetwork.InLobby)
            {
                ExecutePendingBrowserActionIfNeeded();
                if (pendingBrowserAction == PendingBrowserAction.None)
                {
                    PublishBrowserStatus("Choose a round or create a new one.");
                    PublishRoomListChanged();
                }

                return;
            }

            if (PhotonNetwork.NetworkClientState == ClientState.JoiningLobby)
            {
                if (joiningLobbyStartedAt < 0f)
                    joiningLobbyStartedAt = Time.unscaledTime;

                PublishBrowserStatus("Loading active rounds...");
                return;
            }

            joiningLobbyStartedAt = Time.unscaledTime;
            PublishBrowserStatus("Loading active rounds...");
            PhotonNetwork.JoinLobby();
            return;
        }

        if (!PhotonNetwork.IsConnected)
        {
            PublishBrowserStatus("Connecting...");
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master");

        PhotonNetwork.AutomaticallySyncScene = true;
        PlayerProfileService.Instance.ApplyProfileToPhoton();

        if (SessionRequested)
        {
            if (PhotonNetwork.NetworkClientState == ClientState.JoiningLobby || PhotonNetwork.InLobby)
            {
                PublishBrowserStatus("Loading active rounds...");
                return;
            }

            PublishBrowserStatus("Loading active rounds...");
            PhotonNetwork.JoinLobby();
        }
    }

    public override void OnJoinedLobby()
    {
        joiningLobbyStartedAt = -1f;
        PublishBrowserStatus("Choose a round or create a new one.");
        PublishRoomListChanged();
        ExecutePendingBrowserActionIfNeeded();
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        if (roomList == null)
            return;

        for (int i = 0; i < roomList.Count; i++)
        {
            RoomInfo info = roomList[i];
            if (info == null || string.IsNullOrWhiteSpace(info.Name))
                continue;

            if (info.RemovedFromList || !info.IsVisible)
            {
                roomListCache.Remove(info.Name);
                continue;
            }

            roomListCache[info.Name] = info;
        }

        PublishRoomListChanged();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        pendingBrowserAction = PendingBrowserAction.None;
        pendingRoomName = null;
        PublishBrowserStatus("Creating a new round failed.");
        Debug.LogWarning("CreateRoom failed: " + message + " (" + returnCode + ")");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        pendingBrowserAction = PendingBrowserAction.None;
        pendingRoomName = null;
        PublishBrowserStatus("Joining the selected round failed.");
        Debug.LogWarning("JoinRoom failed: " + message + " (" + returnCode + ")");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        roomListCache.Clear();
        PublishRoomListChanged();
        returnToBrowserAfterLeave = false;
        leaveRoomBrowserStatus = string.Empty;
        preservePendingBrowserActionAfterLeave = false;
        joiningLobbyStartedAt = -1f;
        StopLeaveRoomRetryRoutine();

        if (reconnectingForBrowserRecovery && SessionRequested && !PhotonNetwork.InRoom)
        {
            reconnectingForBrowserRecovery = false;
            PublishBrowserStatus("Reconnecting to active rounds...");
            PhotonNetwork.ConnectUsingSettings();
            EnsureSessionBrowserRecoveryRoutine();
            return;
        }

        if (SessionRequested && !PhotonNetwork.InRoom)
        {
            SessionRequested = false;
            pendingBrowserAction = PendingBrowserAction.None;
            pendingRoomName = null;
            PublishBrowserStatus("Disconnected from multiplayer services.");
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined Room");
        SessionRequested = false;
        pendingBrowserAction = PendingBrowserAction.None;
        pendingRoomName = null;
        roomListCache.Clear();
        PublishRoomListChanged();
        PublishBrowserStatus(string.Empty);
        StopSessionBrowserRecoveryRoutine();
        StopLeaveRoomRetryRoutine();

        PlayerProfileService.Instance.ApplyProfileToPhoton();

        if (string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
            PhotonNetwork.NickName = "Player " + PhotonNetwork.LocalPlayer.ActorNumber;

        Hashtable props = new Hashtable();
        props[RoomSettings.ScoreKey] = 0;
        props[RoomSettings.ShipSkinKey] = PlayerProfileService.Instance.CurrentProfile.ShipSkinIndex;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        RestoreRoomStateAfterSceneLoad();
    }

    public override void OnLeftRoom()
    {
        roomListCache.Clear();
        PublishRoomListChanged();

        if (!returnToBrowserAfterLeave)
            return;

        returnToBrowserAfterLeave = false;
        StopLeaveRoomRetryRoutine();
        bool keepPendingAction = preservePendingBrowserActionAfterLeave;
        preservePendingBrowserActionAfterLeave = false;
        if (!keepPendingAction)
        {
            pendingBrowserAction = PendingBrowserAction.None;
            pendingRoomName = null;
        }

        PlayerMovement.gameStarted = false;
        PlayerShooting.gameStarted = false;
        PhotonNetwork.LocalPlayer.TagObject = null;
        HideRoundEndScreenIfPresent();
        HideRoundTransientUi();
        CleanupLocalRoundScene();

        SessionRequested = true;
        SessionBrowserPanelUI.ShowBrowser();
        PublishBrowserStatus(string.IsNullOrWhiteSpace(leaveRoomBrowserStatus) ? "Loading active rounds..." : leaveRoomBrowserStatus);
        leaveRoomBrowserStatus = string.Empty;
        BeginSessionBrowserFlow();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(RoomSettings.SessionStateKey))
        {
            string sessionState = RoomSettings.GetSessionState();
            if (sessionState == RoomSettings.SessionStateClosingLobby)
            {
                string status = PhotonNetwork.IsMasterClient
                    ? "Returning to active rounds..."
                    : "Host closed the lobby. Returning to active rounds...";
                LeaveCurrentRoomToSessionBrowser(status);
                return;
            }
        }

        if (!propertiesThatChanged.ContainsKey("gameStarted"))
            return;

        bool started = IsRoundStarted();
        EnsureDroppedCargoManagerExists();
        EnsureEnemyBotManagerExists();
        EnsureNebulaSpawnerExists();
        if (started)
        {
            SpawnPlayerIfNeeded();
        }

        if (PhotonNetwork.IsMasterClient)
        {
            EnsureTreasureSpawnerExists();
        }
    }

    void ExecutePendingBrowserActionIfNeeded()
    {
        switch (pendingBrowserAction)
        {
            case PendingBrowserAction.CreateRoom:
                pendingBrowserAction = PendingBrowserAction.None;
                pendingRoomName = null;
                CreateNewRoundInternal();
                break;
            case PendingBrowserAction.JoinRoom:
                string targetRoom = pendingRoomName;
                pendingBrowserAction = PendingBrowserAction.None;
                pendingRoomName = null;
                JoinSessionInternal(targetRoom);
                break;
        }
    }

    void CreateNewRoundInternal()
    {
        if (PhotonNetwork.InRoom)
        {
            LeaveCurrentRoomToSessionBrowser("Leaving current round...", true);
            return;
        }

        if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InLobby)
        {
            pendingBrowserAction = PendingBrowserAction.CreateRoom;
            BeginSessionBrowserFlow();
            return;
        }

        CleanupLocalRoundScene();

        string roomName = BuildRoomName();
        Hashtable roomProps = BuildInitialRoomProperties();
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 4,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = roomProps,
            CustomRoomPropertiesForLobby = LobbyVisibleRoomKeys
        };

        PublishBrowserStatus("Creating a new round...");
        PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
    }

    void JoinSessionInternal(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return;

        if (PhotonNetwork.InRoom)
        {
            LeaveCurrentRoomToSessionBrowser("Leaving current round...", true);
            return;
        }

        if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InLobby)
        {
            pendingBrowserAction = PendingBrowserAction.JoinRoom;
            pendingRoomName = roomName;
            BeginSessionBrowserFlow();
            return;
        }

        CleanupLocalRoundScene();

        PublishBrowserStatus("Joining selected round...");
        PhotonNetwork.JoinRoom(roomName);
    }

    string BuildRoomName()
    {
        string hostName = GetLocalDisplayName();
        string sanitizedHost = new string(hostName.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(sanitizedHost))
            sanitizedHost = "pilot";

        return "round_" + sanitizedHost.ToLowerInvariant() + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
    }

    Hashtable BuildInitialRoomProperties()
    {
        string hostName = GetLocalDisplayName();
        string sessionLabel = hostName + "'s Round";

        Hashtable props = new Hashtable
        {
            [RoomSettings.SessionStateKey] = RoomSettings.SessionStateInLobby,
            [RoomSettings.SessionLabelKey] = sessionLabel,
            [RoomSettings.SessionHostNameKey] = hostName,
            [RoomSettings.SessionCreatedAtKey] = PhotonNetwork.Time,
            [RoomSettings.RoundDurationKey] = RoomSettings.DefaultRoundDuration,
            [RoomSettings.ResourceRichnessKey] = RoomSettings.DefaultResourceRichness,
            [RoomSettings.StartTimeKey] = -1d,
            ["gameStarted"] = false,
            [RoomSettings.GadgetChargesStateKey] = string.Empty,
            [RoomSettings.RoundResultsKey] = string.Empty,
            [RoomSettings.RoundEndReasonKey] = string.Empty
        };

        ApplyRememberedLobbySettings(props);
        return props;
    }

    static void ApplyRememberedLobbySettings(Hashtable props)
    {
        if (props == null || !PlayerPrefs.HasKey(RememberedLobbySettingsPrefsKey))
            return;

        string raw = PlayerPrefs.GetString(RememberedLobbySettingsPrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return;

        RememberedLobbySettingsData data;
        try
        {
            data = JsonUtility.FromJson<RememberedLobbySettingsData>(raw);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("NetworkManager: failed to load remembered lobby settings: " + ex.Message);
            return;
        }

        if (data?.entries == null)
            return;

        for (int i = 0; i < data.entries.Length; i++)
        {
            RememberedLobbySettingEntry entry = data.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key) || !IsRememberedLobbySettingKey(entry.key))
                continue;

            if (TryGetRememberedLobbySettingValue(entry, out object value))
                props[entry.key] = value;
        }
    }

    static bool TryCreateRememberedLobbySettingEntry(string key, object value, out RememberedLobbySettingEntry entry)
    {
        entry = new RememberedLobbySettingEntry { key = key };

        switch (value)
        {
            case bool boolValue:
                entry.type = "bool";
                entry.boolValue = boolValue;
                return true;
            case int intValue:
                entry.type = "int";
                entry.intValue = intValue;
                return true;
            case float floatValue:
                entry.type = "float";
                entry.floatValue = floatValue;
                return true;
            case double doubleValue:
                entry.type = "float";
                entry.floatValue = (float)doubleValue;
                return true;
            case string stringValue:
                entry.type = "string";
                entry.stringValue = stringValue;
                return true;
            default:
                entry = null;
                return false;
        }
    }

    static bool TryGetRememberedLobbySettingValue(RememberedLobbySettingEntry entry, out object value)
    {
        value = null;
        switch (entry.type)
        {
            case "bool":
                value = entry.boolValue;
                return true;
            case "int":
                value = entry.intValue;
                return true;
            case "float":
                value = entry.floatValue;
                return true;
            case "string":
                value = entry.stringValue ?? string.Empty;
                return true;
            default:
                return false;
        }
    }

    static bool IsRememberedLobbySettingKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (key.StartsWith("enemy.", StringComparison.Ordinal))
            return true;

        switch (key)
        {
            case RoomSettings.SelectedMapKey:
            case RoomSettings.RoundDurationKey:
            case RoomSettings.MapSizeKey:
            case RoomSettings.MapBackgroundKey:
            case RoomSettings.VisualEffectsEnabledKey:
            case RoomSettings.ObstacleDensityKey:
            case RoomSettings.ObstacleDestroyEnabledKey:
            case RoomSettings.ObstacleHpKey:
            case RoomSettings.ObstacleSizePercentKey:
            case RoomSettings.ObstacleNoBordersKey:
            case RoomSettings.TreasureDensityKey:
            case RoomSettings.ResourceRichnessKey:
            case RoomSettings.NebulaDensityKey:
            case RoomSettings.ExtractionCountKey:
            case RoomSettings.BoosterSlowdownKey:
            case RoomSettings.AmmoCountKey:
            case RoomSettings.BoosterRecoveryDelayKey:
            case RoomSettings.MaxInputBoostPercentKey:
            case RoomSettings.ShipDriftEnabledKey:
            case RoomSettings.LastShipTimerMultiplierKey:
            case RoomSettings.MovingObjectsEnabledKey:
            case RoomSettings.EnemyBotsEnabledKey:
            case RoomSettings.CorsairEnabledKey:
            case RoomSettings.CorsairSpawnSecondKey:
            case RoomSettings.CorsairHpKey:
            case RoomSettings.BulletPushMultiplierKey:
            case RoomSettings.ObstacleWeightFactorKey:
            case RoomSettings.TreasureWeightFactorKey:
                return true;
            default:
                return false;
        }
    }

    string GetLocalDisplayName()
    {
        if (PlayerProfileService.HasInstance &&
            PlayerProfileService.Instance.CurrentProfile != null &&
            !string.IsNullOrWhiteSpace(PlayerProfileService.Instance.CurrentProfile.Nickname))
        {
            return PlayerProfileService.Instance.CurrentProfile.Nickname.Trim();
        }

        if (!string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
            return PhotonNetwork.NickName.Trim();

        return "Pilot";
    }

    IReadOnlyList<SessionRoomEntry> BuildSessionRoomEntries()
    {
        List<SessionRoomEntry> entries = new List<SessionRoomEntry>();
        foreach (RoomInfo info in roomListCache.Values)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.Name))
                continue;

            string state = GetSessionState(info);
            if (state == RoomSettings.SessionStateSummary || state == RoomSettings.SessionStateClosingLobby)
                continue;

            SessionRoomEntry entry = new SessionRoomEntry
            {
                RoomName = info.Name,
                DisplayName = GetSessionLabel(info),
                State = state,
                HostName = GetSessionHostName(info),
                PlayerCount = info.PlayerCount,
                MaxPlayers = info.MaxPlayers,
                CreatedAt = GetCreatedAt(info),
                RemainingTimeSeconds = GetRemainingTime(info, state),
                CanJoin = info.IsOpen && !info.RemovedFromList && info.PlayerCount < info.MaxPlayers
            };

            entries.Add(entry);
        }

        return entries
            .OrderBy(entry => GetSessionStateSortOrder(entry.State))
            .ThenByDescending(entry => entry.CreatedAt)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    void PublishRoomListChanged()
    {
        SessionRoomListChanged?.Invoke(BuildSessionRoomEntries());
    }

    void PublishBrowserStatus(string status)
    {
        latestBrowserStatus = status ?? string.Empty;
        SessionBrowserStatusChanged?.Invoke(latestBrowserStatus);
    }

    string GetSessionState(RoomInfo info)
    {
        if (info.CustomProperties.TryGetValue(RoomSettings.SessionStateKey, out object stateValue) &&
            stateValue is string rawState &&
            !string.IsNullOrWhiteSpace(rawState))
        {
            return rawState;
        }

        if (info.CustomProperties.TryGetValue("gameStarted", out object startedValue) &&
            startedValue is bool started &&
            started)
        {
            return RoomSettings.SessionStateInPlay;
        }

        return RoomSettings.SessionStateInLobby;
    }

    string GetSessionLabel(RoomInfo info)
    {
        if (info.CustomProperties.TryGetValue(RoomSettings.SessionLabelKey, out object value) &&
            value is string label &&
            !string.IsNullOrWhiteSpace(label))
        {
            return label;
        }

        string hostName = GetSessionHostName(info);
        if (!string.IsNullOrWhiteSpace(hostName))
            return hostName + "'s Round";

        return info.Name;
    }

    string GetSessionHostName(RoomInfo info)
    {
        if (info.CustomProperties.TryGetValue(RoomSettings.SessionHostNameKey, out object value) &&
            value is string hostName &&
            !string.IsNullOrWhiteSpace(hostName))
        {
            return hostName;
        }

        return "Unknown host";
    }

    double GetCreatedAt(RoomInfo info)
    {
        if (info.CustomProperties.TryGetValue(RoomSettings.SessionCreatedAtKey, out object value))
        {
            return ConvertToDouble(value, 0d);
        }

        return 0d;
    }

    float? GetRemainingTime(RoomInfo info, string state)
    {
        if (state != RoomSettings.SessionStateInPlay)
            return null;

        float roundDuration = RoomSettings.DefaultRoundDuration;
        if (info.CustomProperties.TryGetValue(RoomSettings.RoundDurationKey, out object durationValue))
        {
            roundDuration = Mathf.Max(1f, ConvertToFloat(durationValue, RoomSettings.DefaultRoundDuration));
        }

        if (!info.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object startValue))
            return null;

        double startTime = ConvertToDouble(startValue, -1d);
        if (startTime < 0d)
            return null;

        float remaining = Mathf.Max(0f, roundDuration - (float)(PhotonNetwork.Time - startTime));
        return remaining;
    }

    int GetSessionStateSortOrder(string state)
    {
        if (state == RoomSettings.SessionStateInLobby)
            return 0;

        if (state == RoomSettings.SessionStateInPlay)
            return 1;

        if (state == RoomSettings.SessionStateClosingLobby)
            return 2;

        return 3;
    }

    static float ConvertToFloat(object value, float fallback)
    {
        if (value is float floatValue)
            return floatValue;

        if (value is double doubleValue)
            return (float)doubleValue;

        if (value is int intValue)
            return intValue;

        return fallback;
    }

    static double ConvertToDouble(object value, double fallback)
    {
        if (value is double doubleValue)
            return doubleValue;

        if (value is float floatValue)
            return floatValue;

        if (value is int intValue)
            return intValue;

        return fallback;
    }

    void SpawnPlayer()
    {
        Debug.Log("Spawning player...");

        Vector3 spawnPos = GetSpawnPosition();
        PhotonNetwork.Instantiate("Player", spawnPos, Quaternion.identity);
    }

    public void RestoreRoomStateAfterSceneLoad()
    {
        PlayerProfileService.Instance.ApplyProfileToPhoton();
        EnsureDroppedCargoManagerExists();
        EnsureEnemyBotManagerExists();
        EnsureNebulaSpawnerExists();

        if (PhotonNetwork.LocalPlayer != null && string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
        {
            PhotonNetwork.NickName = "Player " + PhotonNetwork.LocalPlayer.ActorNumber;
        }

        if (IsRoundStarted())
        {
            SpawnPlayerIfNeeded();
        }

        EnsureTreasureSpawnerExists();
    }

    bool IsRoundStarted()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started)
        {
            return started;
        }

        return false;
    }

    void SpawnPlayerIfNeeded()
    {
        if (!PhotonNetwork.InRoom)
            return;

        if (PhotonNetwork.LocalPlayer.TagObject is GameObject taggedObject)
        {
            if (taggedObject != null && taggedObject.scene.IsValid())
                return;

            PhotonNetwork.LocalPlayer.TagObject = null;
        }

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        foreach (PlayerHealth player in players)
        {
            if (player != null &&
                !player.IsWreck &&
                !player.IsBotControlled &&
                player.photonView != null &&
                player.photonView.IsMine)
            {
                PhotonNetwork.LocalPlayer.TagObject = player.gameObject;
                return;
            }
        }

        SpawnPlayer();
    }

    Vector3 GetSpawnPosition()
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float spawnX = Mathf.Max(3f, mapSize.x * 0.34f);
        float spawnY = Mathf.Max(3f, mapSize.y * 0.34f);
        Vector2[] spawnCorners =
        {
            new Vector2(-spawnX, -spawnY),
            new Vector2(spawnX, spawnY),
            new Vector2(-spawnX, spawnY),
            new Vector2(spawnX, -spawnY)
        };

        Player[] playerOrder = PhotonNetwork.PlayerList
            .OrderBy(player => player.ActorNumber)
            .ToArray();
        int actorIndex = Array.FindIndex(playerOrder, player => player == PhotonNetwork.LocalPlayer);
        if (actorIndex < 0)
            actorIndex = 0;

        int rotationOffset = 0;

        if (PhotonNetwork.CurrentRoom != null && !string.IsNullOrWhiteSpace(PhotonNetwork.CurrentRoom.Name))
        {
            rotationOffset = Mathf.Abs(PhotonNetwork.CurrentRoom.Name.GetHashCode()) % spawnCorners.Length;
        }

        Vector2 baseCorner = spawnCorners[(actorIndex + rotationOffset) % spawnCorners.Length];
        int jitterSeed = actorIndex * 97 + rotationOffset * 31 + 11;
        float maxJitterX = Mathf.Min(2.2f, mapSize.x * 0.06f);
        float maxJitterY = Mathf.Min(2.2f, mapSize.y * 0.06f);
        float jitterX = Mathf.Lerp(-maxJitterX, maxJitterX, Mathf.PerlinNoise(jitterSeed, 0.17f));
        float jitterY = Mathf.Lerp(-maxJitterY, maxJitterY, Mathf.PerlinNoise(0.31f, jitterSeed));

        return new Vector3(baseCorner.x + jitterX, baseCorner.y + jitterY, 0f);
    }

    void EnsureTreasureSpawnerExists()
    {
        EnsureNebulaSpawnerExists();

        if (!PhotonNetwork.IsMasterClient)
            return;

        if (FindAnyObjectByType<TreasureSpawner>() != null)
        {
            return;
        }

        Debug.Log("Tworze TreasureSpawner");

        GameObject spawner = new GameObject("TreasureSpawner");
        spawner.AddComponent<TreasureSpawner>();
    }

    void EnsureNebulaSpawnerExists()
    {
        if (FindAnyObjectByType<NebulaSpawner>() != null)
            return;

        GameObject spawner = new GameObject("NebulaSpawner");
        spawner.AddComponent<NebulaSpawner>();
    }

    void EnsureDroppedCargoManagerExists()
    {
        DroppedCargoManager.EnsureExists();
    }

    void EnsureEnemyBotManagerExists()
    {
        EnemyBotManager.EnsureExists();
    }

    void ReturnToSessionBrowserFromCurrentLobby()
    {
        if (!PhotonNetwork.InRoom)
        {
            PublishBrowserStatus("Loading active rounds...");
            BeginSessionBrowserFlow();
            return;
        }

        if (RoomSettings.GetSessionState() != RoomSettings.SessionStateInLobby)
        {
            LeaveCurrentRoomToSessionBrowser("Returning to active rounds...");
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            Hashtable props = new Hashtable
            {
                [RoomSettings.SessionStateKey] = RoomSettings.SessionStateClosingLobby
            };

            PublishBrowserStatus("Closing lobby...");
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            return;
        }

        LeaveCurrentRoomToSessionBrowser("Returning to active rounds...");
    }

    void LeaveCurrentRoomToSessionBrowser(string status, bool keepPendingAction = false)
    {
        if (returnToBrowserAfterLeave)
        {
            if (keepPendingAction)
                preservePendingBrowserActionAfterLeave = true;

            if (!string.IsNullOrWhiteSpace(status))
            {
                leaveRoomBrowserStatus = status;
                PublishBrowserStatus(status);
            }

            if (PhotonNetwork.InRoom)
                EnsureLeaveRoomRetryRoutine();

            return;
        }

        SessionRequested = true;
        returnToBrowserAfterLeave = true;
        preservePendingBrowserActionAfterLeave = keepPendingAction;
        leaveRoomBrowserStatus = status ?? string.Empty;
        if (!keepPendingAction)
        {
            pendingBrowserAction = PendingBrowserAction.None;
            pendingRoomName = null;
        }

        if (PhotonNetwork.LocalPlayer != null)
            PhotonNetwork.LocalPlayer.TagObject = null;

        PublishBrowserStatus(leaveRoomBrowserStatus);

        if (!PhotonNetwork.InRoom)
        {
            returnToBrowserAfterLeave = false;
            BeginSessionBrowserFlow();
            return;
        }

        bool leaveStarted = PhotonNetwork.LeaveRoom(false);
        if (!leaveStarted)
        {
            Debug.LogWarning("LeaveRoom did not start while returning to active rounds. State: " + PhotonNetwork.NetworkClientState);

            if (!PhotonNetwork.InRoom)
            {
                returnToBrowserAfterLeave = false;
                BeginSessionBrowserFlow();
            }
            else
            {
                PublishBrowserStatus("Waiting to leave current round...");
                EnsureLeaveRoomRetryRoutine();
            }
        }
    }

    void EnsureLeaveRoomRetryRoutine()
    {
        if (leaveRoomRetryRoutine != null)
            return;

        leaveRoomRetryRoutine = StartCoroutine(LeaveRoomRetryLoop());
    }

    void StopLeaveRoomRetryRoutine()
    {
        if (leaveRoomRetryRoutine == null)
            return;

        StopCoroutine(leaveRoomRetryRoutine);
        leaveRoomRetryRoutine = null;
    }

    System.Collections.IEnumerator LeaveRoomRetryLoop()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(LeaveRoomRetryPollSeconds);
        float deadline = Time.unscaledTime + LeaveRoomRetryTimeoutSeconds;

        while (returnToBrowserAfterLeave && PhotonNetwork.InRoom && Time.unscaledTime < deadline)
        {
            yield return wait;

            if (!returnToBrowserAfterLeave || !PhotonNetwork.InRoom)
                break;

            ClientState state = PhotonNetwork.NetworkClientState;
            if (state == ClientState.Joined || state == ClientState.Disconnected || state == ClientState.ConnectedToMasterServer)
            {
                if (PhotonNetwork.LeaveRoom(false))
                    break;
            }
        }

        leaveRoomRetryRoutine = null;

        if (returnToBrowserAfterLeave && !PhotonNetwork.InRoom)
        {
            returnToBrowserAfterLeave = false;
            BeginSessionBrowserFlow();
        }
        else if (returnToBrowserAfterLeave && PhotonNetwork.InRoom)
        {
            PublishBrowserStatus("Still leaving current round. Please wait...");
        }
    }

    void EnsureSessionBrowserRecoveryRoutine()
    {
        if (sessionBrowserRecoveryRoutine != null || !SessionRequested)
            return;

        sessionBrowserRecoveryRoutine = StartCoroutine(SessionBrowserRecoveryLoop());
    }

    void StopSessionBrowserRecoveryRoutine()
    {
        if (sessionBrowserRecoveryRoutine != null)
        {
            StopCoroutine(sessionBrowserRecoveryRoutine);
            sessionBrowserRecoveryRoutine = null;
        }

        joiningLobbyStartedAt = -1f;
        reconnectingForBrowserRecovery = false;
    }

    System.Collections.IEnumerator SessionBrowserRecoveryLoop()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(BrowserRecoveryPollSeconds);

        while (SessionRequested && !PhotonNetwork.InRoom)
        {
            if (!PhotonNetwork.IsConnected)
            {
                PublishBrowserStatus("Connecting...");
                PhotonNetwork.ConnectUsingSettings();
                joiningLobbyStartedAt = -1f;
                yield return wait;
                continue;
            }

            if (PhotonNetwork.IsConnectedAndReady)
            {
                if (PhotonNetwork.InLobby)
                {
                    joiningLobbyStartedAt = -1f;
                    if (pendingBrowserAction == PendingBrowserAction.None)
                    {
                        PublishBrowserStatus("Choose a round or create a new one.");
                        PublishRoomListChanged();
                    }
                }
                else if (PhotonNetwork.NetworkClientState == ClientState.JoiningLobby)
                {
                    if (joiningLobbyStartedAt < 0f)
                        joiningLobbyStartedAt = Time.unscaledTime;

                    if (Time.unscaledTime - joiningLobbyStartedAt > BrowserJoinLobbyWatchdogSeconds)
                    {
                        reconnectingForBrowserRecovery = true;
                        PublishBrowserStatus("Refreshing multiplayer connection...");
                        PhotonNetwork.Disconnect();
                        yield return wait;
                        continue;
                    }
                }
                else
                {
                    joiningLobbyStartedAt = Time.unscaledTime;
                    PublishBrowserStatus("Loading active rounds...");
                    PhotonNetwork.JoinLobby();
                }
            }

            yield return wait;
        }

        sessionBrowserRecoveryRoutine = null;
        joiningLobbyStartedAt = -1f;
    }

    void HideRoundEndScreenIfPresent()
    {
        GameObject endScreen = GameObject.Find("EndScreen");
        if (endScreen != null)
            endScreen.SetActive(false);

        EndScreenUI endScreenUi = FindAnyObjectByType<EndScreenUI>();
        if (endScreenUi != null && endScreenUi.panel != null)
            endScreenUi.panel.SetActive(false);
    }

    void HideRoundTransientUi()
    {
        string[] transientNames =
        {
            "DeathMessage",
            "TimeUpMessage",
            "ExtractionMessage"
        };

        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            if (obj == null || !obj.scene.IsValid())
                continue;

            for (int nameIndex = 0; nameIndex < transientNames.Length; nameIndex++)
            {
                if (obj.name == transientNames[nameIndex])
                {
                    obj.SetActive(false);
                    break;
                }
            }
        }
    }

    void CleanupLocalRoundScene()
    {
        PlayerMovement.gameStarted = false;
        PlayerShooting.gameStarted = false;
        RoundResultsTracker.ResetForCurrentRoom();
        TreasureCollector.ResetRoundReservations();
        ObstacleSpawner.ResetForSessionTransition();

        HashSet<GameObject> queued = new HashSet<GameObject>();
        QueueDestroyFromComponents<PlayerHealth>(queued);
        QueueDestroyFromComponents<Treasure>(queued);
        QueueDestroyFromComponents<ShipWreck>(queued);
        QueueDestroyFromComponents<DroppedCargoCrate>(queued);
        QueueDestroyFromComponents<Bullet>(queued);
        QueueDestroyFromComponents<ObstacleChunk>(queued);
        QueueDestroyFromComponents<NebulaField>(queued);
        QueueDestroyFromComponents<TreasureSpawner>(queued);
        QueueDestroyFromComponents<NebulaSpawner>(queued);
        QueueDestroyFromComponents<EnemyBotManager>(queued);
        QueueDestroyFromComponents<DroppedCargoManager>(queued);

        ExtractionZone[] extractionZones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        for (int i = 0; i < extractionZones.Length; i++)
        {
            ExtractionZone zone = extractionZones[i];
            if (zone == null)
                continue;

            PhotonView view = zone.GetComponent<PhotonView>();
            if (view != null && view.IsRoomView)
                continue;

            QueueDestroyObject(zone.gameObject, queued);
        }

        PhotonView[] views = FindObjectsByType<PhotonView>(FindObjectsInactive.Exclude);
        for (int i = 0; i < views.Length; i++)
        {
            PhotonView view = views[i];
            if (view == null || view.IsRoomView)
                continue;

            if (view.GetComponent<NetworkManager>() != null)
                continue;

            QueueDestroyObject(view.gameObject, queued);
        }
    }

    void QueueDestroyFromComponents<T>(HashSet<GameObject> queued) where T : Component
    {
        T[] components = FindObjectsByType<T>(FindObjectsInactive.Exclude);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null)
                continue;

            QueueDestroyObject(component.gameObject, queued);
        }
    }

    void QueueDestroyObject(GameObject target, HashSet<GameObject> queued)
    {
        if (target == null || !target.scene.IsValid() || queued.Contains(target))
            return;

        queued.Add(target);
        Destroy(target);
    }
}
