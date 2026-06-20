using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
public partial class LobbyManager
{
    void Awake()
    {
        SetGameplayHudVisible(false, true);
    }

    void Start()
    {
        PlayerMovement.gameStarted = false;
        PlayerShooting.gameStarted = false;
        EnsureLobbyRootFullScreen();

        EnsurePlayerStatusListExists();
        EnsureHostSettingsUiExists();
        EnsureLobbyNavigationUiExists();
        EnsureFullScreenLobbyUiExists();

        if (PhotonNetwork.InRoom)
        {
            selectedMapId = RoomSettings.GetSelectedLobbyMapId();
            ShowLobby();
            EnsureDefaultRoomSettings();
        }
        else
        {
            CanvasGroup cg = EnsureCanvasGroup();
            cg.alpha = 0;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            SetLegacyLobbyUiActive(false);
        }

        if (readyText != null)
        {
            readyText.text = "NOT READY";
        }

        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(ToggleReady);
            readyButton.onClick.AddListener(() => PlayUiClickAndInvoke(ToggleReady));
        }

        if (PhotonNetwork.InRoom)
        {
            SetReady(false);
        }

        RefreshPlayerStatusList();
        RefreshHostSettingsUi();
        RefreshLobbyTopBar();

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started && started)
        {
            GameplayHudVisibility.ResetSuppression();
            HideLobby();
            RoundWarmupService.ShowRoundRuleStartAnnouncementIfNeeded();
        }
        else if (RoomSettings.GetSessionState() == RoomSettings.SessionStatePreparing)
        {
            RoundStartCurtainUI.ShowForRoundStart();
        }

        EnsureBottomActionButtonsLayout();
    }

    void Update()
    {
        KeepGameplayHudHiddenWhileLobbyVisible();
    }

    void LateUpdate()
    {
        KeepGameplayHudHiddenWhileLobbyVisible();
    }

    void KeepGameplayHudHiddenWhileLobbyVisible()
    {
        CanvasGroup cg = EnsureCanvasGroup();
        if (cg != null && cg.alpha > 0.01f && cg.blocksRaycasts)
            SetGameplayHudVisible(false);
    }

    public override void OnJoinedRoom()
    {
        hasRecordedCurrentRound = false;
        EnsureLobbyRootFullScreen();
        EnsurePlayerStatusListExists();
        EnsureHostSettingsUiExists();
        EnsureLobbyNavigationUiExists();
        EnsureFullScreenLobbyUiExists();
        EnsureBottomActionButtonsLayout();
        EnsureDefaultRoomSettings();
        selectedMapId = RoomSettings.GetSelectedLobbyMapId();

        bool started = PhotonNetwork.CurrentRoom != null &&
                       PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
                       value is bool startedValue &&
                       startedValue;
        bool preparing = RoomSettings.GetSessionState() == RoomSettings.SessionStatePreparing;

        if (started)
        {
            GameplayHudVisibility.ResetSuppression();
            HideLobby();
        }
        else
        {
            ShowLobby();
            SetReady(false);
            RefreshPlayerStatusList();
            RefreshHostSettingsUi();
            RefreshLobbyTopBar();
            if (preparing)
                RoundStartCurtainUI.ShowForRoundStart();
        }
    }

    void ToggleReady()
    {
        isReady = !isReady;
        SetReady(isReady);
    }

    void SetReady(bool ready)
    {
        isReady = ready;

        Hashtable props = new Hashtable();
        props["ready"] = ready;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        if (readyText != null)
        {
            readyText.text = ready ? "READY" : "NOT READY";
        }

        RefreshPlayerStatusList();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey("ready"))
        {
            RefreshPlayerStatusList();
            CheckAllReady();
        }

        if (ContainsLobbySettingChange(changedProps))
        {
            RefreshHostSettingsUi();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshPlayerStatusList();
        RefreshHostSettingsUi();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshPlayerStatusList();
        RefreshHostSettingsUi();
    }

    void CheckAllReady()
    {
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (!p.CustomProperties.TryGetValue("ready", out object readyValue))
            {
                return;
            }

            if (!(bool)readyValue)
            {
                return;
            }
        }

        if (PhotonNetwork.IsMasterClient)
        {
            if (RoomSettings.GetSessionState() != RoomSettings.SessionStateInLobby)
                return;

            StartGame();
        }
    }

    void StartGame()
    {
        bool arrowFinalLaunch = TryResolveArrowFinalRaceMap(out LobbyMapDefinition arrowFinalMap);
        if (arrowFinalLaunch && PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount > 1)
        {
            SetReady(false);
            RefreshPlayerStatusList();
            RefreshLobbyScreenContent();
            Debug.LogWarning("Arrow final race is solo only.");
            return;
        }

        LobbyMapDefinition selectedMap = arrowFinalLaunch
            ? arrowFinalMap
            : LobbyMapCatalog.Get(RoomSettings.GetSelectedLobbyMapId());

        if (!arrowFinalLaunch && !IsMapUnlockedForLocalPlayer(selectedMap))
        {
            selectedMapId = selectedMap != null ? selectedMap.Id : selectedMapId;
            RefreshHostSettingsUi();
            RefreshLobbyScreenContent();
            Debug.LogWarning("StartGame blocked because selected map is locked: " + (selectedMap != null ? selectedMap.Id : "null"));
            return;
        }

        if (arrowFinalLaunch && selectedMap != null && PhotonNetwork.CurrentRoom != null)
        {
            selectedMapId = selectedMap.Id;
            Hashtable props = new Hashtable();
            LobbyMapCatalog.ApplyToProperties(selectedMap, props);
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        NetworkManager.RememberCurrentLobbySettings();
        GameTimer.StartGame();
    }

    bool TryResolveArrowFinalRaceMap(out LobbyMapDefinition map)
    {
        map = null;
        if (!PlayerProfileService.HasInstance ||
            !PlayerProfileService.Instance.IsInitialized ||
            PlayerProfileService.Instance.CurrentProfile == null)
        {
            return false;
        }

        ArrowLicenseProgressData progress = PlayerProfileService.Instance.GetArrowLicenseProgress();
        ArrowLicenseStage stage = (ArrowLicenseStage)Mathf.Clamp(progress.Stage, (int)ArrowLicenseStage.Locked, (int)ArrowLicenseStage.Complete);
        if (stage != ArrowLicenseStage.FinalRunReady)
            return false;

        int selectedShipSkinIndex = PlayerProfileService.Instance.CurrentProfile.ShipSkinIndex;
        if (ShipCatalog.GetShipTypeFromSkinIndex(selectedShipSkinIndex) != ShipType.Arrow)
            return false;

        map = LobbyMapCatalog.Get(LobbyMapCatalog.AncientSpaceMapId);
        return map != null;
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (ContainsLobbySettingChange(propertiesThatChanged))
        {
            selectedMapId = RoomSettings.GetSelectedLobbyMapId();
            RefreshHostSettingsUi();
            RefreshLobbyScreenContent();
            NetworkManager.RememberCurrentLobbySettings();
        }

        if (propertiesThatChanged.ContainsKey(RoomSettings.ParallaxBackgroundKey) ||
            propertiesThatChanged.ContainsKey(RoomSettings.BackgroundObjectKey))
        {
            GameVisualTheme.RequestRuntimeRefresh();
        }

        if (RoomSettings.GetSessionState() == RoomSettings.SessionStatePreparing && !IsCurrentRoomGameStarted())
        {
            RoundStartCurtainUI.ShowForRoundStart();
            RefreshLobbyTopBar();
            return;
        }

        if (!propertiesThatChanged.ContainsKey("gameStarted"))
            return;

        bool started = false;
        if (propertiesThatChanged["gameStarted"] is bool startedValue)
        {
            started = startedValue;
        }

        if (started)
        {
            GameplayHudVisibility.ResetSuppression();
            HideLobby();
            RoundWarmupService.ShowRoundRuleStartAnnouncementIfNeeded();
            if (!hasRecordedCurrentRound)
            {
                hasRecordedCurrentRound = true;
                _ = RecordStartedGameAsync();
            }
        }
        else
        {
            PlayerMovement.gameStarted = false;
            PlayerShooting.gameStarted = false;
            hasRecordedCurrentRound = false;
            if (ShouldKeepLobbyHiddenForSummary())
            {
                HideLobbyForSummary();
            }
            else
            {
                ShowLobby();
                SetReady(false);
                RefreshPlayerStatusList();
                RefreshHostSettingsUi();
                RefreshLobbyTopBar();
            }
        }
    }

    bool ShouldKeepLobbyHiddenForSummary()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.RoundResultsKey, out object snapshotValue) &&
            snapshotValue is string rawSnapshot &&
            !string.IsNullOrWhiteSpace(rawSnapshot))
        {
            return true;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.RoundEndReasonKey, out object endReasonValue) &&
            endReasonValue is string rawEndReason &&
            !string.IsNullOrWhiteSpace(rawEndReason))
        {
            return true;
        }

        return false;
    }

    void HideLobby()
    {
        AudioManager.Instance.StopMenuMusic();
        PlayerMovement.gameStarted = true;
        PlayerShooting.gameStarted = true;
        SetGameplayHudVisible(true, true);
        HideMapSelectionOverlay();
        HideFullScreenLobbyFlow();
        SetLegacyLobbyUiActive(false);

        CanvasGroup cg = EnsureCanvasGroup();
        cg.alpha = 0;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    void HideLobbyForSummary()
    {
        AudioManager.Instance.StopMenuMusic();
        PlayerMovement.gameStarted = false;
        PlayerShooting.gameStarted = false;
        SetGameplayHudVisible(false, true);
        HideMapSelectionOverlay();
        HideFullScreenLobbyFlow();
        SetLegacyLobbyUiActive(false);
        GameplayHudVisibility.SuppressForRoundSummary();

        CanvasGroup cg = EnsureCanvasGroup();
        cg.alpha = 0;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    void ShowLobby()
    {
        AudioManager.Instance.PlayMenuMusic();
        SetLegacyLobbyUiActive(true);
        SetGameplayHudVisible(false, true);
        EnsureLobbyRootFullScreen();
        EnsureFullScreenLobbyUiExists();
        if (fullScreenLobbyRootObject != null)
        {
            fullScreenLobbyRootObject.SetActive(true);
            fullScreenLobbyRootObject.transform.SetAsLastSibling();
        }
        if (string.IsNullOrWhiteSpace(selectedMapId))
            selectedMapId = RoomSettings.GetSelectedLobbyMapId();

        CanvasGroup cg = EnsureCanvasGroup();
        cg.alpha = 1;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        HideMapSelectionOverlay();
        EnsureBottomActionButtonsLayout();
        SwitchLobbyScreen(LobbyScreen.MapSelection);
    }

}
