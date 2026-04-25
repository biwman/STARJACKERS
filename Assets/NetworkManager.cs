using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class NetworkManager : MonoBehaviourPunCallbacks
{
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
        instance.BeginSessionBrowserFlow();
    }

    public static void ReturnToSessionBrowserFromLobby()
    {
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
                PublishBrowserStatus("Loading active rounds...");
                return;
            }

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
        pendingBrowserAction = PendingBrowserAction.None;
        pendingRoomName = null;

        PlayerMovement.gameStarted = false;
        PlayerShooting.gameStarted = false;
        PhotonNetwork.LocalPlayer.TagObject = null;

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
            return;

        if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InLobby)
        {
            pendingBrowserAction = PendingBrowserAction.CreateRoom;
            BeginSessionBrowserFlow();
            return;
        }

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
            return;

        if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InLobby)
        {
            pendingBrowserAction = PendingBrowserAction.JoinRoom;
            pendingRoomName = roomName;
            BeginSessionBrowserFlow();
            return;
        }

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
            [RoomSettings.StartTimeKey] = -1d,
            ["gameStarted"] = false,
            [RoomSettings.GadgetChargesStateKey] = string.Empty,
            [RoomSettings.RoundResultsKey] = string.Empty,
            [RoomSettings.RoundEndReasonKey] = string.Empty
        };

        return props;
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

    void LeaveCurrentRoomToSessionBrowser(string status)
    {
        if (returnToBrowserAfterLeave)
            return;

        SessionRequested = true;
        returnToBrowserAfterLeave = true;
        leaveRoomBrowserStatus = status ?? string.Empty;
        pendingBrowserAction = PendingBrowserAction.None;
        pendingRoomName = null;

        if (PhotonNetwork.LocalPlayer != null)
            PhotonNetwork.LocalPlayer.TagObject = null;

        PublishBrowserStatus(leaveRoomBrowserStatus);

        if (!PhotonNetwork.InRoom)
        {
            returnToBrowserAfterLeave = false;
            BeginSessionBrowserFlow();
            return;
        }

        PhotonNetwork.LeaveRoom(false);
    }
}
