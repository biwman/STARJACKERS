using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;
using ExitGames.Client.Photon;

public class GameManager : MonoBehaviourPunCallbacks
{
    const string ObstacleLayoutKey = "obstacleLayout";
    const string ExtractionLayoutKey = "extractionLayout";
    const string NebulaLayoutKey = "nebulaLayout";
    const string MapSeedKey = "mapSeed";
    const string LoneShipModeStartTimeKey = "loneShipModeStartTime";
    const float RestartCleanupTimeout = 2.5f;
    bool restartInProgress;
    bool leavingRoomToProfile;

    public void StartGame()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;

        Hashtable props = new Hashtable();
        props["gameStarted"] = true;
        props[RoomSettings.StartTimeKey] = PhotonNetwork.Time;
        props[RoomSettings.SessionStateKey] = RoomSettings.SessionStateInPlay;
        props[LoneShipModeStartTimeKey] = -1d;
        props[GameTimer.EvacuationPauseUntilKey] = -1d;
        props[GameTimer.EvacuationPauseRemainingKey] = -1f;
        props[RoomSettings.GadgetChargesStateKey] = string.Empty;
        props[RoomSettings.RoundResultsKey] = string.Empty;
        props[RoomSettings.RoundEndReasonKey] = string.Empty;

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RoundResultsTracker.ResetForCurrentRoom();
    }

    public void RestartGame()
    {
        if (!PhotonNetwork.IsConnected)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            if (IsRoundStopped())
            {
                Debug.Log("LOCAL READY AFTER HOST RESTART");
                NetworkManager networkManager = FindAnyObjectByType<NetworkManager>();
                if (networkManager != null)
                {
                    networkManager.RestoreRoomStateAfterSceneLoad();
                }
            }

            return;
        }

        if (restartInProgress) return;

        Debug.Log("RESTART GAME");
        StartCoroutine(RestartAfterCleanup());
    }

    public void EndGame(string endReason = "generic")
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;

        Debug.Log("GAME ENDED");

        RoundResultsSnapshotData snapshot = RoundResultsTracker.BuildSnapshot(endReason);

        Hashtable props = new Hashtable();
        props["gameStarted"] = false;
        props[ObstacleLayoutKey] = string.Empty;
        props[ExtractionLayoutKey] = string.Empty;
        props[NebulaLayoutKey] = string.Empty;
        props[MapSeedKey] = -1;
        props[RoomSettings.SessionStateKey] = RoomSettings.SessionStateSummary;
        props[LoneShipModeStartTimeKey] = -1d;
        props[GameTimer.EvacuationPauseUntilKey] = -1d;
        props[GameTimer.EvacuationPauseRemainingKey] = -1f;
        props[RoomSettings.GadgetChargesStateKey] = string.Empty;
        props[RoomSettings.RoundResultsKey] = RoundResultsTracker.SerializeSnapshot(snapshot);
        props[RoomSettings.RoundEndReasonKey] = snapshot != null ? snapshot.endReason : endReason;

        if (PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }
    }

    public void LeaveRoomToProfile()
    {
        if (leavingRoomToProfile)
            return;

        leavingRoomToProfile = true;

        PlayerMovement.gameStarted = false;
        PlayerShooting.gameStarted = false;

        if (!PhotonNetwork.IsConnected)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LocalPlayer.TagObject = null;
        }

        RoundResultsTracker.ResetForCurrentRoom();
        PhotonNetwork.Disconnect();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (!leavingRoomToProfile)
            return;

        leavingRoomToProfile = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    System.Collections.IEnumerator RestartAfterCleanup()
    {
        restartInProgress = true;

        Hashtable props = new Hashtable();
        props["gameStarted"] = false;
        props[ObstacleLayoutKey] = string.Empty;
        props[ExtractionLayoutKey] = string.Empty;
        props[NebulaLayoutKey] = string.Empty;
        props[MapSeedKey] = -1;
        props[RoomSettings.StartTimeKey] = -1d;
        props[RoomSettings.SessionStateKey] = RoomSettings.SessionStateInLobby;
        props[LoneShipModeStartTimeKey] = -1d;
        props[GameTimer.EvacuationPauseUntilKey] = -1d;
        props[GameTimer.EvacuationPauseRemainingKey] = -1f;
        props[RoomSettings.GadgetChargesStateKey] = string.Empty;
        props[RoomSettings.RoundResultsKey] = string.Empty;
        props[RoomSettings.RoundEndReasonKey] = string.Empty;

        if (PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.DestroyAll();
        }

        float deadline = Time.time + RestartCleanupTimeout;
        while (Time.time < deadline && HasRuntimeNetworkObjects())
        {
            yield return null;
        }

        PhotonNetwork.LoadLevel(SceneManager.GetActiveScene().name);
        RoundResultsTracker.ResetForCurrentRoom();
        restartInProgress = false;
    }

    bool IsRoundStopped()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return true;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started)
        {
            return !started;
        }

        return true;
    }

    bool HasRuntimeNetworkObjects()
    {
        PhotonView[] views = FindObjectsByType<PhotonView>(FindObjectsInactive.Exclude);
        for (int i = 0; i < views.Length; i++)
        {
            PhotonView view = views[i];
            if (view == null || view.IsRoomView)
                continue;

            return true;
        }

        return false;
    }
}
