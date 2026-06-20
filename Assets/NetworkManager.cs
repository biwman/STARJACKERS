using System;
using System.Collections.Generic;
using System.Globalization;
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
    const float LateJoinBlockThresholdSeconds = 60f;
    const float GameplayRejoinRetryDelaySeconds = 1f;
    const float GameplayRejoinAttemptTimeoutSeconds = 12f;
    const int GameplayRejoinMaxAttempts = 3;
    const int RoomRejoinGracePeriodMilliseconds = 120000;
    const string ObstacleLayoutKey = "obstacleLayout";
    const string ExtractionLayoutKey = "extractionLayout";
    const string RepairBayLayoutKey = "repairBayLayout";
    const string SpaceFactoryLayoutKey = SpaceFactorySpawner.LayoutKey;
    const string ScienceStationLayoutKey = ScienceStationSpawner.LayoutKey;
    const string EmptyLayoutSentinel = "__empty__";
    const float PlayerSpawnClearanceRadius = 2.85f;
    const float PlayerSpawnLayoutClearance = 4.25f;
    const string PhotonUserIdPrefsKey = "BrawlRaiders.PhotonUserId.v1";
    const string RememberedLobbySettingsPrefsKey = "BrawlRaiders.LastLobbySettings.v2";
    const string FinishedRoundsPrefsKey = "BrawlRaiders.FinishedRounds.v1";
    const string RandomRoundRulesBrowserLabel = "Round rules: random";
    const int MaxRememberedFinishedRounds = 64;

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
        public string MapName;
        public string ActiveEffectsLabel;
        public float? RemainingTimeSeconds;
        public bool CanJoin;
        public bool BlockedByLocalDeath;
        public string BlockReason;
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
        public double doubleValue;
        public bool boolValue;
    }

    [Serializable]
    sealed class FinishedRoundRoomsData
    {
        public FinishedRoundRoomEntry[] entries;
    }

    [Serializable]
    sealed class FinishedRoundRoomEntry
    {
        public string roomName;
        public string roundToken;
        public string outcome;
        public double markedAt;
    }

    static readonly string[] LobbyVisibleRoomKeys =
    {
        RoomSettings.SessionStateKey,
        RoomSettings.SessionLabelKey,
        RoomSettings.SessionHostNameKey,
        RoomSettings.SessionCreatedAtKey,
        RoomSettings.RoundWarmupTokenKey,
        RoomSettings.RoundDurationKey,
        RoomSettings.StartTimeKey,
        RoomSettings.RoundStarterActorNumberKey,
        RoomSettings.RoundEndUtcMsKey,
        RoomSettings.SelectedMapKey,
        RoomSettings.CrazyEnemiesModeKey,
        RoomSettings.CrazyEnemiesStartUtcMsKey,
        RoomSettings.CrazyEnemiesActiveKey,
        RoomSettings.FogOfWarModeKey,
        RoomSettings.FogOfWarStartUtcMsKey,
        RoomSettings.FogOfWarActiveKey,
        RoomSettings.PirateBaseModeKey,
        RoomSettings.PirateBaseStartUtcMsKey,
        RoomSettings.PirateBaseActiveKey,
        RoomSettings.AsteroidShowerModeKey,
        RoomSettings.AsteroidShowerStartUtcMsKey,
        RoomSettings.AsteroidShowerActiveKey,
        RoomSettings.CosmicWormModeKey,
        RoomSettings.CosmicWormStartUtcMsKey,
        RoomSettings.CosmicWormActiveKey,
        RoomSettings.MilitaryConvoyModeKey,
        RoomSettings.MilitaryConvoyStartUtcMsKey,
        RoomSettings.MilitaryConvoyActiveKey,
        MapInstanceService.MapTravelLockedKey
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
    Coroutine gameplayRejoinRoutine;
    float joiningLobbyStartedAt = -1f;
    bool reconnectingForBrowserRecovery;
    bool preservePendingBrowserActionAfterLeave;
    bool rejoiningLastRoom;
    string lastJoinedRoomName;

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

    public static void ReturnToSessionBrowserFromFinishedRound()
    {
        MarkCurrentRoundEndedForLocalPlayer();
        ReturnToSessionBrowserFromRound();
    }

    public static void MarkCurrentRoundEndedForLocalPlayer(string outcome = null)
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        RememberFinishedRound(PhotonNetwork.CurrentRoom.Name, BuildCurrentRoundToken(), outcome);
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
        EnsurePhotonConnectionSettings();
    }

    void EnsurePhotonConnectionSettings()
    {
        Application.runInBackground = true;
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.KeepAliveInBackground = RoomRejoinGracePeriodMilliseconds / 1000f;

        if (PhotonNetwork.AuthValues == null)
            PhotonNetwork.AuthValues = new AuthenticationValues();

        if (string.IsNullOrWhiteSpace(PhotonNetwork.AuthValues.UserId))
            PhotonNetwork.AuthValues.UserId = GetOrCreatePhotonUserId();

        PhotonNetwork.GameVersion = Application.version;

        ServerSettings photonSettings = PhotonNetwork.PhotonServerSettings;
        if (photonSettings != null && photonSettings.AppSettings != null)
            photonSettings.AppSettings.AppVersion = Application.version;
    }

    string GetOrCreatePhotonUserId()
    {
        string storedUserId = PlayerPrefs.GetString(PhotonUserIdPrefsKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(storedUserId))
            return storedUserId;

        string generatedUserId = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(PhotonUserIdPrefsKey, generatedUserId);
        PlayerPrefs.Save();
        return generatedUserId;
    }

    void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            RestoreRoomStateAfterSceneLoad();
            return;
        }

        PhotonNetwork.GameVersion = Application.version;

        if (SessionRequested)
        {
            BeginSessionBrowserFlow();
        }
    }

    void BeginSessionBrowserFlow()
    {
        EnsurePhotonConnectionSettings();
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
        EnsurePhotonConnectionSettings();
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

        if (gameplayRejoinRoutine != null)
            return;

        if (!SessionRequested && IsTransientDisconnect(cause) && CanAttemptLastRoomRejoin())
        {
            StartGameplayRejoin(cause);
            return;
        }

        if (SessionRequested && !PhotonNetwork.InRoom && IsTransientDisconnect(cause))
        {
            pendingBrowserAction = PendingBrowserAction.None;
            pendingRoomName = null;
            reconnectingForBrowserRecovery = true;
            PublishBrowserStatus("Reconnecting to multiplayer services...");
            EnsurePhotonConnectionSettings();
            PhotonNetwork.ConnectUsingSettings();
            EnsureSessionBrowserRecoveryRoutine();
            return;
        }

        if (reconnectingForBrowserRecovery && SessionRequested && !PhotonNetwork.InRoom)
        {
            reconnectingForBrowserRecovery = false;
            PublishBrowserStatus("Reconnecting to active rounds...");
            EnsurePhotonConnectionSettings();
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

    void OnApplicationPause(bool paused)
    {
        if (!paused)
            RecoverSessionBrowserConnectionIfNeeded();
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused)
            RecoverSessionBrowserConnectionIfNeeded();
    }

    void RecoverSessionBrowserConnectionIfNeeded()
    {
        if (!SessionRequested || PhotonNetwork.InRoom || PhotonNetwork.IsConnected)
            return;

        reconnectingForBrowserRecovery = true;
        PublishBrowserStatus("Reconnecting to multiplayer services...");
        EnsurePhotonConnectionSettings();
        PhotonNetwork.ConnectUsingSettings();
        EnsureSessionBrowserRecoveryRoutine();
    }

    bool IsTransientDisconnect(DisconnectCause cause)
    {
        switch (cause)
        {
            case DisconnectCause.ServerTimeout:
            case DisconnectCause.ClientTimeout:
            case DisconnectCause.Exception:
            case DisconnectCause.ExceptionOnConnect:
            case DisconnectCause.SendException:
            case DisconnectCause.ReceiveException:
            case DisconnectCause.DisconnectByServerReasonUnknown:
                return true;
            default:
                return false;
        }
    }

    bool CanAttemptLastRoomRejoin()
    {
        return !string.IsNullOrWhiteSpace(lastJoinedRoomName) && !returnToBrowserAfterLeave;
    }

    void StartGameplayRejoin(DisconnectCause cause)
    {
        rejoiningLastRoom = true;
        gameplayRejoinRoutine = StartCoroutine(GameplayRejoinLoop(cause, lastJoinedRoomName));
    }

    void StopGameplayRejoinRoutine()
    {
        if (gameplayRejoinRoutine != null)
        {
            StopCoroutine(gameplayRejoinRoutine);
            gameplayRejoinRoutine = null;
        }

        rejoiningLastRoom = false;
    }

    System.Collections.IEnumerator GameplayRejoinLoop(DisconnectCause cause, string roomName)
    {
        Debug.LogWarning("Photon transient disconnect while in room '" + roomName + "': " + cause + ". Trying to reconnect and rejoin.");

        WaitForSecondsRealtime retryWait = new WaitForSecondsRealtime(GameplayRejoinRetryDelaySeconds);
        for (int attempt = 1; attempt <= GameplayRejoinMaxAttempts && !PhotonNetwork.InRoom; attempt++)
        {
            EnsurePhotonConnectionSettings();
            PublishBrowserStatus("Reconnecting to round...");

            bool started = PhotonNetwork.ReconnectAndRejoin();
            if (!started)
            {
                Debug.LogWarning("ReconnectAndRejoin did not start on attempt " + attempt + " for room '" + roomName + "'.");
                yield return retryWait;
                continue;
            }

            float deadline = Time.unscaledTime + GameplayRejoinAttemptTimeoutSeconds;
            while (!PhotonNetwork.InRoom && Time.unscaledTime < deadline)
                yield return null;

            if (PhotonNetwork.InRoom)
            {
                gameplayRejoinRoutine = null;
                rejoiningLastRoom = false;
                PublishBrowserStatus(string.Empty);
                yield break;
            }

            yield return retryWait;
        }

        Debug.LogWarning("Failed to reconnect and rejoin room '" + roomName + "'. Returning to active rounds browser.");
        gameplayRejoinRoutine = null;
        rejoiningLastRoom = false;
        lastJoinedRoomName = null;
        pendingBrowserAction = PendingBrowserAction.None;
        pendingRoomName = null;
        SessionRequested = true;
        SessionBrowserPanelUI.ShowBrowser();
        PublishBrowserStatus("Connection lost. Returning to active rounds...");
        BeginSessionBrowserFlow();
    }

    public override void OnJoinedRoom()
    {
        lastJoinedRoomName = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : null;
        StopGameplayRejoinRoutine();
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

        if (PhotonNetwork.CurrentRoom != null && IsRoomLocallyFinished(PhotonNetwork.CurrentRoom.Name, BuildCurrentRoundToken()))
        {
            PublishBrowserStatus("You already ended this round.");
            SessionRequested = true;
            SessionBrowserPanelUI.ShowBrowser();
            LeaveCurrentRoomToSessionBrowser("Returning to active rounds...");
            return;
        }

        Hashtable props = new Hashtable();
        props[RoomSettings.ScoreKey] = 0;
        props[RoomSettings.ShipSkinKey] = PlayerProfileService.Instance.CurrentProfile.ShipSkinIndex;
        props[RoomSettings.PilotIdKey] = PlayerProfileService.Instance.CurrentProfile.SelectedPilotId;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        RestoreRoomStateAfterSceneLoad();
    }

    public override void OnLeftRoom()
    {
        if (!rejoiningLastRoom)
            lastJoinedRoomName = null;

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

            if (sessionState == RoomSettings.SessionStatePreparing)
            {
                RoundStartCurtainUI.ShowForRoundStart();
                return;
            }
        }

        if (!propertiesThatChanged.ContainsKey("gameStarted"))
            return;

        bool started = IsRoundStarted();
        if (started)
        {
            AtlasPilotRoundTracker.RecordRoundStart(PlayerProfileService.Instance.CurrentProfile);
            RoundStartCurtainUI.ShowForRoundStart();
            RoundWarmupService.ShowRoundRuleStartAnnouncementIfNeeded();
        }
        else
        {
            RoundStartCurtainUI.HideImmediate();
            RoundPilotHudUI.HideAllRuntimeObjects();
        }

        EnsureDroppedCargoManagerExists();
        EnsureEnemyBotManagerExists();
        EnsureNeutralRiderManagerExists();
        EnsureNebulaSpawnerExists();
        RepairBaySpawner.EnsureExists();
        SpaceJunkSpawner.EnsureExists();
        ContainerSpawner.EnsureExists();
        RandomLootWreckSpawner.EnsureExists();
        SpaceFactorySpawner.EnsureExists();
        ScienceStationSpawner.EnsureExists();
        ArtifactAsteroidSpawner.EnsureExists();
        FogOfWarOverlay.EnsureExists();
        AsteroidShowerController.EnsureExists();
        ToxicBorderController.EnsureExists();
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
            MaxPlayers = 8,
            IsVisible = true,
            IsOpen = true,
            CleanupCacheOnLeave = false,
            PlayerTtl = RoomRejoinGracePeriodMilliseconds,
            EmptyRoomTtl = RoomRejoinGracePeriodMilliseconds,
            PublishUserId = true,
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

        if (IsRoomLocallyFinished(roomName, ResolveCachedRoomToken(roomName)))
        {
            PublishBrowserStatus("You already ended this round.");
            PublishRoomListChanged();
            return;
        }

        if (TryGetJoinBlockReason(roomName, out string blockReason))
        {
            PublishBrowserStatus(blockReason);
            PublishRoomListChanged();
            return;
        }

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

    bool TryGetJoinBlockReason(string roomName, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(roomName) || !roomListCache.TryGetValue(roomName, out RoomInfo info) || info == null)
            return false;

        string state = GetSessionState(info);
        if (state == RoomSettings.SessionStatePreparing)
        {
            reason = "ROUND STARTING";
            return true;
        }

        if (TryGetRoomBool(info, MapInstanceService.MapTravelLockedKey, out bool travelLocked) && travelLocked)
        {
            reason = "SPACE-TIME DISTORTIONS DETECTED";
            return true;
        }

        float? remainingTime = GetRemainingTime(info, state);
        if (state == RoomSettings.SessionStateInPlay &&
            remainingTime.HasValue &&
            remainingTime.Value < LateJoinBlockThresholdSeconds)
        {
            reason = "TOO LATE TO JOIN";
            return true;
        }

        if (!info.IsOpen || info.RemovedFromList || info.PlayerCount >= info.MaxPlayers)
        {
            reason = "This round is full or closed.";
            return true;
        }

        return false;
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
            [RoomSettings.EndDisasterWarningSecondsKey] = RoomSettings.DefaultEndDisasterWarningSeconds,
            [RoomSettings.TreasureDensityKey] = RoomSettings.DefaultTreasureDensity,
            [RoomSettings.RadioactiveTreasureDensityKey] = RoomSettings.DefaultRadioactiveTreasureDensity,
            [RoomSettings.AlienSecretsDensityKey] = RoomSettings.DefaultAlienSecretsDensity,
            [RoomSettings.ResourceRichnessKey] = RoomSettings.DefaultResourceRichness,
            [RoomSettings.CrazyEnemiesModeKey] = RoomSettings.DefaultMapEffectMode,
            [RoomSettings.CrazyEnemiesStartUtcMsKey] = -1d,
            [RoomSettings.CrazyEnemiesActiveKey] = false,
            [RoomSettings.FogOfWarModeKey] = RoomSettings.DefaultMapEffectMode,
            [RoomSettings.FogOfWarStartUtcMsKey] = -1d,
            [RoomSettings.FogOfWarActiveKey] = false,
            [RoomSettings.PirateBaseModeKey] = RoomSettings.DefaultMapEffectMode,
            [RoomSettings.PirateBaseStartUtcMsKey] = -1d,
            [RoomSettings.PirateBaseActiveKey] = false,
            [RoomSettings.AsteroidShowerModeKey] = RoomSettings.DefaultMapEffectMode,
            [RoomSettings.AsteroidShowerStartUtcMsKey] = -1d,
            [RoomSettings.AsteroidShowerActiveKey] = false,
            [RoomSettings.CosmicWormModeKey] = RoomSettings.DefaultMapEffectMode,
            [RoomSettings.CosmicWormStartUtcMsKey] = -1d,
            [RoomSettings.CosmicWormActiveKey] = false,
            [RoomSettings.MilitaryConvoyModeKey] = RoomSettings.DefaultMapEffectMode,
            [RoomSettings.MilitaryConvoyStartUtcMsKey] = -1d,
            [RoomSettings.MilitaryConvoyActiveKey] = false,
            [RoomSettings.MapEffectModeDefaultsVersionKey] = RoomSettings.MapEffectModeDefaultsVersion,
            [RoomSettings.SpaceJunkDensityKey] = RoomSettings.DefaultSpaceJunkDensity,
            [RoomSettings.ContainersDensityKey] = RoomSettings.DefaultContainersDensity,
            [RoomSettings.NebulaSizeKey] = RoomSettings.DefaultNebulaSize,
            [RoomSettings.FireNebulaDensityKey] = RoomSettings.DefaultFireNebulaDensity,
            [RoomSettings.FireNebulaSizeKey] = RoomSettings.DefaultFireNebulaSize,
            [RoomSettings.ToxicNebulaDensityKey] = RoomSettings.DefaultToxicNebulaDensity,
            [RoomSettings.ToxicNebulaSizeKey] = RoomSettings.DefaultToxicNebulaSize,
            [RoomSettings.CloudsDensityKey] = RoomSettings.DefaultCloudsDensity,
            [RoomSettings.CloudsSizeKey] = RoomSettings.DefaultCloudsSize,
            [RoomSettings.RandomLootWreckCountKey] = RoomSettings.DefaultRandomLootWreckCount,
            [RoomSettings.SpaceFactoryCountKey] = RoomSettings.DefaultSpaceFactoryCount,
            [RoomSettings.ScienceStationCountKey] = RoomSettings.DefaultScienceStationCount,
            [RoomSettings.StartTimeKey] = -1d,
            [RoomSettings.RoundStarterActorNumberKey] = 0,
            [RoomSettings.RoundStarterUserIdKey] = string.Empty,
            [RoomSettings.RoundStarterNicknameKey] = string.Empty,
            [RoomSettings.RoundEndUtcMsKey] = -1d,
            [RoomSettings.RoundWarmupTokenKey] = string.Empty,
            [RoomSettings.RoundWarmupStartedAtKey] = -1d,
            [RoomSettings.ShipUnlockPlotStartTimeKey] = -1d,
            [RoomSettings.ShipUnlockPlotActiveKey] = ShipUnlockPlotCoordinator.GetPlotId(ShipUnlockPlotType.None),
            ["gameStarted"] = false,
            [RoomSettings.GadgetChargesStateKey] = string.Empty,
            [RoomSettings.RepairBayOccupancyStateKey] = string.Empty,
            [RoomSettings.SpaceFactoryStateKey] = string.Empty,
            [RoomSettings.SpaceFactoryOccupancyStateKey] = string.Empty,
            [RoomSettings.ScienceStationOccupancyStateKey] = string.Empty,
            [RoomSettings.RoundResultsKey] = string.Empty,
            [RoomSettings.FinishedRoundResultsKey] = string.Empty,
            [RoomSettings.RoundEndReasonKey] = string.Empty,
            [RoomSettings.InventoryLossEnabledKey] = RoomSettings.DefaultInventoryLossEnabled,
            [RoomSettings.EquipmentLossEnabledKey] = RoomSettings.DefaultEquipmentLossEnabled,
            [RoomSettings.ToxicBordersEnabledKey] = RoomSettings.DefaultToxicBordersEnabled,
            [RoomSettings.LowHpHullSparksEnabledKey] = RoomSettings.DefaultLowHpHullSparksEnabled,
            [RoomSettings.BoomVfxEnabledKey] = RoomSettings.DefaultBoomVfxEnabled,
            [RoomSettings.ParallaxBackgroundKey] = RoomSettings.DefaultParallaxBackground,
            [RoomSettings.BackgroundObjectKey] = RoomSettings.DefaultBackgroundObject,
            [RoomSettings.HapticsEnabledKey] = RoomSettings.DefaultHapticsEnabled,
            [RoomSettings.FpsCounterEnabledKey] = RoomSettings.DefaultFpsCounterEnabled,
            [RoomSettings.NeutralRidersEnabledKey] = RoomSettings.DefaultNeutralRidersEnabled,
            [RoomSettings.NeutralRidersCountKey] = RoomSettings.DefaultNeutralRidersCount,
            [RoomSettings.NeutralRidersAggressionKey] = RoomSettings.DefaultNeutralRiderAggression,
            [RoomSettings.EnemyDamageMultiplierPercentKey] = RoomSettings.DefaultEnemyDamageMultiplierPercent,
            [RoomSettings.EnemyAttackWindupMultiplierPercentKey] = RoomSettings.DefaultEnemyAttackWindupMultiplierPercent,
            [RoomSettings.EnemyAttackCooldownMultiplierPercentKey] = RoomSettings.DefaultEnemyAttackCooldownMultiplierPercent
        };

        MapInstanceService.AppendClearRoundProperties(props);
        AddDefaultMapEffectChanceProperties(props);
        HashSet<string> rememberedKeys = ApplyRememberedLobbySettings(props);
        ApplyRememberedMapPresetDefaults(props, rememberedKeys);
        return props;
    }

    static void AddDefaultMapEffectChanceProperties(Hashtable props)
    {
        if (props == null)
            return;

        if (!props.ContainsKey(RoomSettings.MapEffectChanceDefaultsVersionKey))
            props[RoomSettings.MapEffectChanceDefaultsVersionKey] = RoomSettings.MapEffectChanceDefaultsVersion;

        string[] ruleIds =
        {
            RoomSettings.CrazyEnemiesRuleId,
            RoomSettings.FogOfWarRuleId,
            RoomSettings.PirateBaseRuleId,
            RoomSettings.AsteroidShowerRuleId,
            RoomSettings.CosmicWormRuleId,
            RoomSettings.MilitaryConvoyRuleId
        };

        IReadOnlyList<LobbyMapDefinition> maps = LobbyMapCatalog.AllMaps;
        for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
        {
            LobbyMapDefinition map = maps[mapIndex];
            if (map == null)
                continue;

            for (int ruleIndex = 0; ruleIndex < ruleIds.Length; ruleIndex++)
            {
                string key = RoomSettings.GetMapEffectChanceKey(map.Id, ruleIds[ruleIndex]);
                if (!props.ContainsKey(key))
                    props[key] = RoomSettings.GetDefaultMapEffectChancePercent(map.Id, ruleIds[ruleIndex]);
            }
        }
    }

    static HashSet<string> ApplyRememberedLobbySettings(Hashtable props)
    {
        HashSet<string> appliedKeys = new HashSet<string>(StringComparer.Ordinal);
        if (props == null || !PlayerPrefs.HasKey(RememberedLobbySettingsPrefsKey))
            return appliedKeys;

        string raw = PlayerPrefs.GetString(RememberedLobbySettingsPrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return appliedKeys;

        RememberedLobbySettingsData data;
        try
        {
            data = JsonUtility.FromJson<RememberedLobbySettingsData>(raw);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("NetworkManager: failed to load remembered lobby settings: " + ex.Message);
            return appliedKeys;
        }

        if (data?.entries == null)
            return appliedKeys;

        for (int i = 0; i < data.entries.Length; i++)
        {
            RememberedLobbySettingEntry entry = data.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key) || !IsRememberedLobbySettingKey(entry.key))
                continue;

            if (TryGetRememberedLobbySettingValue(entry, out object value))
            {
                if (entry.key == RoomSettings.ParallaxBackgroundKey)
                {
                    if (value is not string rememberedBackgroundId)
                        continue;

                    string normalizedBackgroundId = RoomSettings.NormalizeParallaxBackgroundId(rememberedBackgroundId);
                    if (!string.Equals(normalizedBackgroundId, rememberedBackgroundId, StringComparison.Ordinal))
                        continue;

                    value = normalizedBackgroundId;
                }
                else if (entry.key == RoomSettings.BackgroundObjectKey)
                {
                    if (value is not string rememberedObjectId)
                        continue;

                    string normalizedObjectId = RoomSettings.NormalizeBackgroundObjectId(rememberedObjectId);
                    if (!string.Equals(normalizedObjectId, rememberedObjectId, StringComparison.Ordinal))
                        continue;

                    value = normalizedObjectId;
                }
                else if (ShouldSkipRememberedMapEffectChanceDefault(entry.key, value))
                {
                    continue;
                }
                else if (ShouldSkipRememberedSpecialMapEffectMode(entry.key, value))
                {
                    continue;
                }

                props[entry.key] = value;
                appliedKeys.Add(entry.key);
            }
        }

        return appliedKeys;
    }

    static bool ShouldSkipRememberedSpecialMapEffectMode(string key, object value)
    {
        return RoomSettings.IsSpecialMapEffectModeKey(key) &&
               value is string mode &&
               RoomSettings.NormalizeMapEffectMode(mode) != RoomSettings.DefaultMapEffectMode;
    }

    static bool ShouldSkipRememberedMapEffectChanceDefault(string key, object value)
    {
        if (!RoomSettings.IsMapEffectChanceKey(key) || value is not int percent)
            return false;

        if (!TryGetMapEffectChanceParts(key, out string mapId, out string ruleId))
            return false;

        return RoomSettings.IsLegacyMapEffectChanceDefault(mapId, ruleId, percent);
    }

    static bool TryGetMapEffectChanceParts(string key, out string mapId, out string ruleId)
    {
        mapId = string.Empty;
        ruleId = string.Empty;
        if (!RoomSettings.IsMapEffectChanceKey(key))
            return false;

        string suffix = key.Substring(RoomSettings.MapEffectChanceKeyPrefix.Length);
        int separatorIndex = suffix.LastIndexOf('.');
        if (separatorIndex <= 0 || separatorIndex >= suffix.Length - 1)
            return false;

        mapId = suffix.Substring(0, separatorIndex);
        ruleId = suffix.Substring(separatorIndex + 1);
        return true;
    }

    static void ApplyRememberedMapPresetDefaults(Hashtable props, HashSet<string> rememberedKeys)
    {
        if (props == null ||
            !props.TryGetValue(RoomSettings.SelectedMapKey, out object selectedMapValue) ||
            selectedMapValue is not string selectedMapId ||
            string.IsNullOrWhiteSpace(selectedMapId))
        {
            return;
        }

        LobbyMapDefinition map = LobbyMapCatalog.Get(selectedMapId);
        if (map == null)
            return;

        if (rememberedKeys == null)
            rememberedKeys = new HashSet<string>(StringComparer.Ordinal);

        props[RoomSettings.ToxicBordersEnabledKey] = map.ToxicBordersEnabled;

        if (!rememberedKeys.Contains(RoomSettings.SpaceFactoryCountKey))
            props[RoomSettings.SpaceFactoryCountKey] = map.SpaceFactoryCount;

        if (!rememberedKeys.Contains(RoomSettings.ScienceStationCountKey))
            props[RoomSettings.ScienceStationCountKey] = LobbyMapCatalog.GetDefaultScienceStationCount(map.Id);

        if (!rememberedKeys.Contains(RoomSettings.NebulaDensityKey))
            props[RoomSettings.NebulaDensityKey] = map.NebulaDensity;

        if (!rememberedKeys.Contains(RoomSettings.FireNebulaDensityKey))
            props[RoomSettings.FireNebulaDensityKey] = map.FireNebulaDensity;

        if (!rememberedKeys.Contains(RoomSettings.ToxicNebulaDensityKey))
            props[RoomSettings.ToxicNebulaDensityKey] = map.ToxicNebulaDensity;

        if (!rememberedKeys.Contains(RoomSettings.NebulaSizeKey))
            props[RoomSettings.NebulaSizeKey] = map.NebulaSize;

        if (!rememberedKeys.Contains(RoomSettings.FireNebulaSizeKey))
            props[RoomSettings.FireNebulaSizeKey] = map.FireNebulaSize;

        if (!rememberedKeys.Contains(RoomSettings.ToxicNebulaSizeKey))
            props[RoomSettings.ToxicNebulaSizeKey] = map.ToxicNebulaSize;

        if (!rememberedKeys.Contains(RoomSettings.CloudsDensityKey))
            props[RoomSettings.CloudsDensityKey] = map.CloudsDensity;

        if (!rememberedKeys.Contains(RoomSettings.CloudsSizeKey))
            props[RoomSettings.CloudsSizeKey] = map.CloudsSize;

        if (!rememberedKeys.Contains(RoomSettings.AlienSecretsDensityKey))
            props[RoomSettings.AlienSecretsDensityKey] = LobbyMapCatalog.GetDefaultAlienSecretsDensity(map.Id);

        if (!rememberedKeys.Contains(RoomSettings.ParallaxBackgroundKey))
            props[RoomSettings.ParallaxBackgroundKey] = LobbyMapCatalog.GetDefaultParallaxBackgroundId(map.Id);

        if (!rememberedKeys.Contains(RoomSettings.BackgroundObjectKey))
            props[RoomSettings.BackgroundObjectKey] = LobbyMapCatalog.GetDefaultBackgroundObjectId(map.Id);

        if (!rememberedKeys.Contains(RoomSettings.GravityWellPhysicsEnabledKey))
            props[RoomSettings.GravityWellPhysicsEnabledKey] = map.Id == LobbyMapCatalog.GravityWellMapId;

        LobbyMapCatalog.ApplyEnemyPresetsToProperties(map, props, rememberedKeys);
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
                entry.type = "double";
                entry.doubleValue = doubleValue;
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
            case "double":
                value = entry.doubleValue;
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

        if (RoomSettings.IsGunSetupKey(key))
            return true;

        if (RoomSettings.IsMapEffectChanceKey(key))
            return true;

        switch (key)
        {
            case RoomSettings.SelectedMapKey:
            case RoomSettings.RoundDurationKey:
            case RoomSettings.MapSizeKey:
            case RoomSettings.MapBackgroundKey:
            case RoomSettings.VisualEffectsEnabledKey:
            case RoomSettings.LowHpHullSparksEnabledKey:
            case RoomSettings.BoomVfxEnabledKey:
            case RoomSettings.ParallaxBackgroundKey:
            case RoomSettings.BackgroundObjectKey:
            case RoomSettings.EndDisasterModeKey:
            case RoomSettings.EndDisasterWarningSecondsKey:
            case RoomSettings.ObstacleDensityKey:
            case RoomSettings.ObstacleDestroyEnabledKey:
            case RoomSettings.ObstacleHpKey:
            case RoomSettings.ObstacleSizePercentKey:
            case RoomSettings.ObstacleNoBordersKey:
            case RoomSettings.TreasureDensityKey:
            case RoomSettings.RadioactiveTreasureDensityKey:
            case RoomSettings.AlienSecretsDensityKey:
            case RoomSettings.ResourceRichnessKey:
            case RoomSettings.CrazyEnemiesModeKey:
            case RoomSettings.CrazyEnemiesStartUtcMsKey:
            case RoomSettings.FogOfWarModeKey:
            case RoomSettings.FogOfWarStartUtcMsKey:
            case RoomSettings.PirateBaseModeKey:
            case RoomSettings.PirateBaseStartUtcMsKey:
            case RoomSettings.AsteroidShowerModeKey:
            case RoomSettings.AsteroidShowerStartUtcMsKey:
            case RoomSettings.CosmicWormModeKey:
            case RoomSettings.CosmicWormStartUtcMsKey:
            case RoomSettings.MilitaryConvoyModeKey:
            case RoomSettings.MilitaryConvoyStartUtcMsKey:
            case RoomSettings.SpaceJunkDensityKey:
            case RoomSettings.ContainersDensityKey:
            case RoomSettings.FireNebulaDensityKey:
            case RoomSettings.ToxicNebulaDensityKey:
            case RoomSettings.NebulaSizeKey:
            case RoomSettings.FireNebulaSizeKey:
            case RoomSettings.ToxicNebulaSizeKey:
            case RoomSettings.CloudsDensityKey:
            case RoomSettings.CloudsSizeKey:
            case RoomSettings.RandomLootWreckCountKey:
            case RoomSettings.NebulaDensityKey:
            case RoomSettings.ExtractionCountKey:
            case RoomSettings.RepairBayCountKey:
            case RoomSettings.SpaceFactoryCountKey:
            case RoomSettings.ScienceStationCountKey:
            case RoomSettings.BoosterSlowdownKey:
            case RoomSettings.BoosterRecoveryDelayKey:
            case RoomSettings.AdvancedBoosterEnabledKey:
            case RoomSettings.ShipDriftEnabledKey:
            case RoomSettings.LastShipTimerMultiplierKey:
            case RoomSettings.InventoryLossEnabledKey:
            case RoomSettings.EquipmentLossEnabledKey:
            case RoomSettings.MovingObjectsEnabledKey:
            case RoomSettings.EnemyBotsEnabledKey:
            case RoomSettings.CorsairEnabledKey:
            case RoomSettings.CorsairSpawnSecondKey:
            case RoomSettings.CorsairHpKey:
            case RoomSettings.BulletPushMultiplierKey:
            case RoomSettings.ObstacleWeightFactorKey:
            case RoomSettings.TreasureWeightFactorKey:
            case RoomSettings.HapticsEnabledKey:
            case RoomSettings.FpsCounterEnabledKey:
            case RoomSettings.NeutralRidersEnabledKey:
            case RoomSettings.NeutralRidersCountKey:
            case RoomSettings.NeutralRidersAggressionKey:
            case RoomSettings.EnemyDamageMultiplierPercentKey:
            case RoomSettings.EnemyAttackWindupMultiplierPercentKey:
            case RoomSettings.EnemyAttackCooldownMultiplierPercentKey:
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

            string roundToken = BuildRoundToken(info);
            string localFinishReason = GetLocalFinishedRoundBlockReason(info.Name, roundToken);
            bool blockedByLocalDeath = !string.IsNullOrWhiteSpace(localFinishReason);
            float? remainingTime = GetRemainingTime(info, state);
            bool tooLateToJoin = state == RoomSettings.SessionStateInPlay &&
                                 remainingTime.HasValue &&
                                 remainingTime.Value < LateJoinBlockThresholdSeconds;
            bool roundStarting = state == RoomSettings.SessionStatePreparing;
            bool travelLocked = TryGetRoomBool(info, MapInstanceService.MapTravelLockedKey, out bool locked) && locked;
            bool baseCanJoin = info.IsOpen && !info.RemovedFromList && info.PlayerCount < info.MaxPlayers;
            SessionRoomEntry entry = new SessionRoomEntry
            {
                RoomName = info.Name,
                DisplayName = GetSessionLabel(info),
                State = state,
                HostName = GetSessionHostName(info),
                PlayerCount = info.PlayerCount,
                MaxPlayers = info.MaxPlayers,
                CreatedAt = GetCreatedAt(info),
                MapName = GetMapDisplayName(info),
                ActiveEffectsLabel = GetActiveEffectsLabel(info, state),
                RemainingTimeSeconds = remainingTime,
                CanJoin = baseCanJoin && !blockedByLocalDeath && !tooLateToJoin && !roundStarting && !travelLocked,
                BlockedByLocalDeath = blockedByLocalDeath,
                BlockReason = roundStarting ? "ROUND STARTING" : travelLocked ? "SPACE-TIME DISTORTIONS DETECTED" : tooLateToJoin ? "TOO LATE TO JOIN" : localFinishReason
            };

            entries.Add(entry);
        }

        return entries
            .OrderBy(entry => GetSessionStateSortOrder(entry.State))
            .ThenByDescending(entry => entry.CreatedAt)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    string GetMapDisplayName(RoomInfo info)
    {
        string mapId = RoomSettings.DefaultLobbyMapId;
        if (info.CustomProperties.TryGetValue(RoomSettings.SelectedMapKey, out object value) &&
            value is string selectedMapId &&
            !string.IsNullOrWhiteSpace(selectedMapId))
        {
            mapId = selectedMapId;
        }

        LobbyMapDefinition definition = LobbyMapCatalog.Get(mapId);
        return definition != null ? definition.DisplayName : mapId;
    }

    string GetActiveEffectsLabel(RoomInfo info, string state)
    {
        if (state == RoomSettings.SessionStateInLobby || state == RoomSettings.SessionStatePreparing)
            return RandomRoundRulesBrowserLabel;

        List<string> labels = new List<string>();
        AddActiveEffectLabel(labels, info, state, "CRAZY ENEMIES", RoomSettings.CrazyEnemiesActiveKey);
        AddActiveEffectLabel(labels, info, state, "FOG OF WAR", RoomSettings.FogOfWarActiveKey);
        AddActiveEffectLabel(labels, info, state, "PIRATE BASE", RoomSettings.PirateBaseActiveKey);
        AddActiveEffectLabel(labels, info, state, "ASTEROID SHOWER", RoomSettings.AsteroidShowerActiveKey);
        AddActiveEffectLabel(labels, info, state, "COSMIC WORM", RoomSettings.CosmicWormActiveKey);
        AddActiveEffectLabel(labels, info, state, "MILITARY CONVOY", RoomSettings.MilitaryConvoyActiveKey);
        return labels.Count > 0 ? string.Join(", ", labels) : string.Empty;
    }

    void AddActiveEffectLabel(List<string> labels, RoomInfo info, string state, string label, string activeKey)
    {
        if (IsRoomEffectActive(info, state, activeKey))
            labels.Add(label);
    }

    bool IsRoomEffectActive(RoomInfo info, string state, string activeKey)
    {
        if (info == null)
            return false;

        return state == RoomSettings.SessionStateInPlay &&
               TryGetRoomBool(info, activeKey, out bool active) &&
               active;
    }

    bool TryGetRoomBool(RoomInfo info, string key, out bool result)
    {
        result = false;
        if (info == null || !info.CustomProperties.TryGetValue(key, out object value))
            return false;

        if (value is bool boolValue)
        {
            result = boolValue;
            return true;
        }

        if (value is int intValue)
        {
            result = intValue != 0;
            return true;
        }

        return false;
    }

    double GetRoomDouble(RoomInfo info, string key, double fallback)
    {
        if (info != null && info.CustomProperties.TryGetValue(key, out object value))
            return ConvertToDouble(value, fallback);

        return fallback;
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

        if (info.CustomProperties.TryGetValue(RoomSettings.RoundEndUtcMsKey, out object endUtcValue))
        {
            double endUtcMs = ConvertToDouble(endUtcValue, -1d);
            if (endUtcMs > 0d)
            {
                double nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                return Mathf.Max(0f, (float)((endUtcMs - nowUtcMs) / 1000d));
            }
        }

        return null;
    }

    string ResolveCachedRoomToken(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return string.Empty;

        if (roomListCache.TryGetValue(roomName, out RoomInfo info) && info != null)
            return BuildRoundToken(info);

        return string.Empty;
    }

    static string BuildCurrentRoundToken()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return string.Empty;

        string startValue = "nostart";
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object value) && value != null)
            startValue = value.ToString();

        return PhotonNetwork.CurrentRoom.Name + "_" + startValue;
    }

    static string BuildRoundToken(RoomInfo info)
    {
        if (info == null || string.IsNullOrWhiteSpace(info.Name))
            return string.Empty;

        string startValue = "nostart";
        if (info.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object value) && value != null)
            startValue = value.ToString();

        return info.Name + "_" + startValue;
    }

    static bool IsRoomLocallyFinished(string roomName, string roundToken)
    {
        return FindFinishedRoundEntry(roomName, roundToken) != null;
    }

    static string GetLocalFinishedRoundBlockReason(string roomName, string roundToken)
    {
        FinishedRoundRoomEntry entry = FindFinishedRoundEntry(roomName, roundToken);
        if (entry == null)
            return string.Empty;

        return FormatLocalFinishedOutcome(entry.outcome);
    }

    static FinishedRoundRoomEntry FindFinishedRoundEntry(string roomName, string roundToken)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return null;

        FinishedRoundRoomsData data = LoadFinishedRoundsData();
        if (data?.entries == null)
            return null;

        for (int i = 0; i < data.entries.Length; i++)
        {
            FinishedRoundRoomEntry entry = data.entries[i];
            if (entry == null || !string.Equals(entry.roomName, roomName, StringComparison.Ordinal))
                continue;

            if (string.IsNullOrWhiteSpace(entry.roundToken) || string.IsNullOrWhiteSpace(roundToken))
                return entry;

            if (string.Equals(entry.roundToken, roundToken, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    static void RememberFinishedRound(string roomName, string roundToken, string outcome = null)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return;

        FinishedRoundRoomsData data = LoadFinishedRoundsData() ?? new FinishedRoundRoomsData();
        List<FinishedRoundRoomEntry> entries = data.entries != null
            ? data.entries.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.roomName)).ToList()
            : new List<FinishedRoundRoomEntry>();

        FinishedRoundRoomEntry previousEntry = entries.FirstOrDefault(entry =>
            string.Equals(entry.roomName, roomName, StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(roundToken) ||
             string.IsNullOrWhiteSpace(entry.roundToken) ||
             string.Equals(entry.roundToken, roundToken, StringComparison.Ordinal)));
        string normalizedOutcome = NormalizeLocalFinishedOutcome(outcome);
        if (string.IsNullOrWhiteSpace(normalizedOutcome) && previousEntry != null)
            normalizedOutcome = NormalizeLocalFinishedOutcome(previousEntry.outcome);
        if (string.IsNullOrWhiteSpace(normalizedOutcome))
            normalizedOutcome = "finished";

        entries.RemoveAll(entry =>
            string.Equals(entry.roomName, roomName, StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(roundToken) ||
             string.IsNullOrWhiteSpace(entry.roundToken) ||
             string.Equals(entry.roundToken, roundToken, StringComparison.Ordinal)));

        entries.Add(new FinishedRoundRoomEntry
        {
            roomName = roomName,
            roundToken = string.IsNullOrWhiteSpace(roundToken) ? roomName : roundToken,
            outcome = normalizedOutcome,
            markedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        entries = entries
            .OrderByDescending(entry => entry.markedAt)
            .Take(MaxRememberedFinishedRounds)
            .ToList();

        PlayerPrefs.SetString(FinishedRoundsPrefsKey, JsonUtility.ToJson(new FinishedRoundRoomsData { entries = entries.ToArray() }));
        PlayerPrefs.Save();
    }

    static string NormalizeLocalFinishedOutcome(string outcome)
    {
        return string.IsNullOrWhiteSpace(outcome) ? string.Empty : outcome.Trim().ToLowerInvariant();
    }

    static string FormatLocalFinishedOutcome(string outcome)
    {
        switch (NormalizeLocalFinishedOutcome(outcome))
        {
            case "dead":
                return "YOU DIED";
            case "lost_in_space":
                return "LOST";
            case "time_up":
                return "TIME UP";
            case "extracted":
                return "EXTRACTED";
            case "evacuated":
                return "EVACUATED";
            default:
                return "FINISHED";
        }
    }

    static FinishedRoundRoomsData LoadFinishedRoundsData()
    {
        if (!PlayerPrefs.HasKey(FinishedRoundsPrefsKey))
            return new FinishedRoundRoomsData { entries = Array.Empty<FinishedRoundRoomEntry>() };

        string raw = PlayerPrefs.GetString(FinishedRoundsPrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return new FinishedRoundRoomsData { entries = Array.Empty<FinishedRoundRoomEntry>() };

        try
        {
            FinishedRoundRoomsData data = JsonUtility.FromJson<FinishedRoundRoomsData>(raw);
            if (data == null)
                data = new FinishedRoundRoomsData();

            data.entries ??= Array.Empty<FinishedRoundRoomEntry>();
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("NetworkManager: failed to load finished round list: " + ex.Message);
            return new FinishedRoundRoomsData { entries = Array.Empty<FinishedRoundRoomEntry>() };
        }
    }

    int GetSessionStateSortOrder(string state)
    {
        if (state == RoomSettings.SessionStateInLobby)
            return 0;

        if (state == RoomSettings.SessionStatePreparing)
            return 1;

        if (state == RoomSettings.SessionStateInPlay)
            return 2;

        if (state == RoomSettings.SessionStateClosingLobby)
            return 3;

        return 4;
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
        Vector3 spawnPos = GetSpawnPosition();
        PhotonNetwork.Instantiate("Player", spawnPos, Quaternion.identity);
        GameVisualTheme.RequestRuntimeRefresh();
    }

    public void RestoreRoomStateAfterSceneLoad()
    {
        PlayerProfileService.Instance.ApplyProfileToPhoton();
        if (RoomSettings.GetSessionState() == RoomSettings.SessionStatePreparing)
        {
            RoundStartCurtainUI.ShowForRoundStart();
            return;
        }

        if (IsRoundStarted())
        {
            AtlasPilotRoundTracker.RecordRoundStart(PlayerProfileService.Instance.CurrentProfile);
            RoundStartCurtainUI.ShowForRoundStart();
            RoundWarmupService.ShowRoundRuleStartAnnouncementIfNeeded();
        }

        EnsureDroppedCargoManagerExists();
        EnsureEnemyBotManagerExists();
        EnsureNeutralRiderManagerExists();
        EnsureNebulaSpawnerExists();
        RepairBaySpawner.EnsureExists();
        SpaceJunkSpawner.EnsureExists();
        ContainerSpawner.EnsureExists();
        RandomLootWreckSpawner.EnsureExists();
        SpaceFactorySpawner.EnsureExists();
        ScienceStationSpawner.EnsureExists();
        ArtifactAsteroidSpawner.EnsureExists();
        FogOfWarOverlay.EnsureExists();
        AsteroidShowerController.EnsureExists();
        ToxicBorderController.EnsureExists();
        MapTravelService.EnsureExists();

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
            PlayerHealth taggedHealth = taggedObject != null ? taggedObject.GetComponent<PlayerHealth>() : null;
            if (taggedObject != null &&
                taggedObject.scene.IsValid() &&
                taggedHealth != null &&
                ActorIdentity.IsLocalHumanPlayerActor(taggedHealth))
            {
                return;
            }

            PhotonNetwork.LocalPlayer.TagObject = null;
        }

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        foreach (PlayerHealth player in players)
        {
            if (ActorIdentity.IsLocalHumanPlayerActor(player))
            {
                PhotonNetwork.LocalPlayer.TagObject = player.gameObject;
                return;
            }
        }

        SpawnPlayer();
    }

    Vector3 GetSpawnPosition()
    {
        Vector2 mapSize = RoomSettings.GetGameplayMapDimensions();
        float spawnX = Mathf.Max(3f, mapSize.x * 0.34f);
        float spawnY = Mathf.Max(3f, mapSize.y * 0.34f);
        Vector2[] spawnCorners =
        {
            new Vector2(-spawnX, -spawnY),
            new Vector2(spawnX, spawnY),
            new Vector2(-spawnX, spawnY),
            new Vector2(spawnX, -spawnY),
            new Vector2(0f, spawnY),
            new Vector2(0f, -spawnY),
            new Vector2(-spawnX, 0f),
            new Vector2(spawnX, 0f)
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

        Vector2 preferredPosition = new Vector2(baseCorner.x + jitterX, baseCorner.y + jitterY);
        Vector2 safePosition = ResolveSafePlayerSpawnPosition(preferredPosition, mapSize, actorIndex, rotationOffset);
        return new Vector3(safePosition.x, safePosition.y, 0f);
    }

    Vector2 ResolveSafePlayerSpawnPosition(Vector2 preferredPosition, Vector2 mapSize, int actorIndex, int rotationOffset)
    {
        List<Vector2> blockedLayoutPositions = GetSpawnBlockedLayoutPositions();
        Vector2[] candidates = BuildPlayerSpawnCandidates(preferredPosition, mapSize, actorIndex, rotationOffset);
        Vector2 bestCandidate = ClampSpawnPositionToMap(preferredPosition, mapSize);
        float bestScore = ScoreSpawnCandidate(bestCandidate, blockedLayoutPositions);
        bool foundClearCandidate = IsPlayerSpawnAreaClear(bestCandidate, blockedLayoutPositions);
        if (!foundClearCandidate)
            bestScore = float.MinValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            Vector2 candidate = ClampSpawnPositionToMap(candidates[i], mapSize);
            float score = ScoreSpawnCandidate(candidate, blockedLayoutPositions);
            bool isClear = IsPlayerSpawnAreaClear(candidate, blockedLayoutPositions);
            if (isClear && (!foundClearCandidate || score >= bestScore))
            {
                bestCandidate = candidate;
                bestScore = score;
                foundClearCandidate = true;
            }
            else if (!foundClearCandidate && score > bestScore)
            {
                bestCandidate = candidate;
                bestScore = score;
            }
        }

        return bestCandidate;
    }

    Vector2[] BuildPlayerSpawnCandidates(Vector2 preferredPosition, Vector2 mapSize, int actorIndex, int rotationOffset)
    {
        List<Vector2> candidates = new List<Vector2> { preferredPosition };
        float phase = (actorIndex * 53f + rotationOffset * 29f) * Mathf.Deg2Rad;
        float maxRadius = Mathf.Min(mapSize.x, mapSize.y) * 0.26f;

        for (int ring = 1; ring <= 5; ring++)
        {
            float radius = Mathf.Min(maxRadius, ring * 2.35f);
            int count = 10 + ring * 2;
            for (int i = 0; i < count; i++)
            {
                float angle = phase + ((Mathf.PI * 2f) / count) * i;
                candidates.Add(preferredPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        return candidates.ToArray();
    }

    Vector2 ClampSpawnPositionToMap(Vector2 candidate, Vector2 mapSize)
    {
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        float margin = Mathf.Max(2.5f, PlayerSpawnClearanceRadius);
        return new Vector2(
            Mathf.Clamp(candidate.x, -halfX + margin, halfX - margin),
            Mathf.Clamp(candidate.y, -halfY + margin, halfY - margin));
    }

    float ScoreSpawnCandidate(Vector2 candidate, List<Vector2> blockedLayoutPositions)
    {
        float nearest = 999f;
        for (int i = 0; i < blockedLayoutPositions.Count; i++)
            nearest = Mathf.Min(nearest, Vector2.Distance(candidate, blockedLayoutPositions[i]));

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player == null || player.IsWreck)
                continue;

            nearest = Mathf.Min(nearest, Vector2.Distance(candidate, player.transform.position));
        }

        return nearest;
    }

    bool IsPlayerSpawnAreaClear(Vector2 candidate, List<Vector2> blockedLayoutPositions)
    {
        for (int i = 0; i < blockedLayoutPositions.Count; i++)
        {
            if (Vector2.Distance(candidate, blockedLayoutPositions[i]) < PlayerSpawnLayoutClearance)
                return false;
        }

        int hitCount = Physics2DNonAllocQuery.OverlapCircle(candidate, PlayerSpawnClearanceRadius, out Collider2D[] hits);
        for (int i = 0; i < hitCount; i++)
        {
            if (IsBlockingPlayerSpawnCollider(hits[i]))
                return false;
        }

        return true;
    }

    bool IsBlockingPlayerSpawnCollider(Collider2D hit)
    {
        if (hit == null)
            return false;

        GameObject hitObject = hit.gameObject;
        if (hitObject == null || hitObject.name == "Ground")
            return false;

        if (hitObject.name.StartsWith("Wall", StringComparison.Ordinal))
            return true;

        return hit.GetComponentInParent<PlayerHealth>() != null ||
               hit.GetComponentInParent<ObstacleChunk>() != null ||
               hit.GetComponentInParent<MovingSpaceObject>() != null ||
               hit.GetComponentInParent<ExtractionZone>() != null ||
               hit.GetComponentInParent<RepairBay>() != null ||
               hit.GetComponentInParent<SpaceFactory>() != null ||
               hit.GetComponentInParent<ScienceStation>() != null;
    }

    List<Vector2> GetSpawnBlockedLayoutPositions()
    {
        List<Vector2> positions = new List<Vector2>();
        AppendLayoutPositions(positions, ObstacleLayoutKey);
        AppendLayoutPositions(positions, ExtractionLayoutKey);
        AppendLayoutPositions(positions, RepairBayLayoutKey);
        AppendLayoutPositions(positions, SpaceFactoryLayoutKey);
        AppendLayoutPositions(positions, ScienceStationLayoutKey);
        return positions;
    }

    void AppendLayoutPositions(List<Vector2> positions, string key)
    {
        if (positions == null ||
            PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value) ||
            value is not string layout ||
            string.IsNullOrWhiteSpace(layout) ||
            string.Equals(layout, EmptyLayoutSentinel, StringComparison.Ordinal))
        {
            return;
        }

        string[] entries = layout.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            if (TryParseLayoutPosition(entries[i], out Vector2 position))
                positions.Add(position);
        }
    }

    bool TryParseLayoutPosition(string entry, out Vector2 position)
    {
        position = Vector2.zero;
        if (string.IsNullOrWhiteSpace(entry))
            return false;

        string[] parts = entry.Split(',');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                position = new Vector2(x, y);
                return true;
            }
        }

        return false;
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

    void EnsureNeutralRiderManagerExists()
    {
        NeutralRiderManager.EnsureExists();
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
        RoundMessageLayer.ClearAll();

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
                EnsurePhotonConnectionSettings();
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
        GameplayHudVisibility.ResetSuppression();
        PlayerMovement.gameStarted = false;
        PlayerShooting.gameStarted = false;
        EarlyRoundExitUI.HideAll();
        RoundPilotHudUI.DestroyAllRuntimeObjects();
        ShipDamageState.ClearAllRuntimeDamage();
        RoundWarmupService.ResetRoundTransientEffects();
        RoundMessageLayer.ClearAll();
        RoundResultsTracker.ResetForCurrentRoom();
        TreasureCollector.ResetRoundReservations();
        ObstacleSpawner.ResetForSessionTransition();
        RepairBaySpawner.ResetForSessionTransition();
        SpaceJunkSpawner.ResetForSessionTransition();
        ContainerSpawner.ResetForSessionTransition();
        RandomLootWreckSpawner.ResetForSessionTransition();
        SpaceFactorySpawner.ResetForSessionTransition();
        ScienceStationSpawner.ResetForSessionTransition();
        ArtifactAsteroidSpawner.ResetForSessionTransition();
        MapTravelService.ResetLocalRuntimeState();

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
        QueueDestroyFromComponents<RepairBay>(queued);
        QueueDestroyFromComponents<SpaceFactory>(queued);
        QueueDestroyFromComponents<ScienceStation>(queued);
        QueueDestroyFromComponents<ArtifactAsteroid>(queued);
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
