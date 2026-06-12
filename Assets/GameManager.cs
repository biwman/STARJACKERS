using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;
using ExitGames.Client.Photon;
using System;

public class GameManager : MonoBehaviourPunCallbacks
{
    const string ObstacleLayoutKey = "obstacleLayout";
    const string ExtractionLayoutKey = "extractionLayout";
    const string NebulaLayoutKey = "nebulaLayout";
    const string FireNebulaLayoutKey = NebulaSpawner.FireNebulaLayoutKey;
    const string ToxicNebulaLayoutKey = NebulaSpawner.ToxicNebulaLayoutKey;
    const string CloudLayoutKey = NebulaSpawner.CloudLayoutKey;
    const string CloudDirectionKey = NebulaSpawner.CloudDirectionKey;
    const string RepairBayLayoutKey = "repairBayLayout";
    const string SpaceFactoryLayoutKey = SpaceFactorySpawner.LayoutKey;
    const string ScienceStationLayoutKey = ScienceStationSpawner.LayoutKey;
    const string ArtifactAsteroidLayoutKey = ArtifactAsteroidSpawner.LayoutKey;
    const string MapSeedKey = "mapSeed";
    const string LoneShipModeStartTimeKey = "loneShipModeStartTime";
    const float RestartCleanupTimeout = 2.5f;
    bool restartInProgress;
    bool leavingRoomToProfile;

    public void StartGame()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;

        RoundWarmupService.BeginRoundStartPreparation();
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
                NetworkManager networkManager = FindAnyObjectByType<NetworkManager>();
                if (networkManager != null)
                {
                    networkManager.RestoreRoomStateAfterSceneLoad();
                }
            }

            return;
        }

        if (restartInProgress) return;

        StartCoroutine(RestartAfterCleanup());
    }

    public void EndGame(string endReason = "generic")
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;

        GameplayHudVisibility.SuppressForRoundSummary();
        EarlyRoundExitUI.HideAll();
        RoundPilotHudUI.DestroyAllRuntimeObjects();
        ShipDamageState.ClearAllRuntimeDamage();
        TreasureCollector.ResetRoundReservations();
        RoundWarmupService.ResetRoundTransientEffects();

        RoundResultsSnapshotData snapshot = RoundResultsTracker.BuildSnapshot(endReason);

        Hashtable props = new Hashtable();
        props["gameStarted"] = false;
        props[RoomSettings.RoundWarmupTokenKey] = string.Empty;
        props[RoomSettings.RoundWarmupStartedAtKey] = -1d;
        props[ObstacleLayoutKey] = string.Empty;
        props[ExtractionLayoutKey] = string.Empty;
        props[NebulaLayoutKey] = string.Empty;
        props[FireNebulaLayoutKey] = string.Empty;
        props[ToxicNebulaLayoutKey] = string.Empty;
        props[CloudLayoutKey] = string.Empty;
        props[CloudDirectionKey] = string.Empty;
        props[RepairBayLayoutKey] = string.Empty;
        props[SpaceFactoryLayoutKey] = string.Empty;
        props[ScienceStationLayoutKey] = string.Empty;
        props[ArtifactAsteroidLayoutKey] = string.Empty;
        props[RoomSettings.ArtifactAsteroidsStateKey] = string.Empty;
        props[MapSeedKey] = -1;
        props[RoomSettings.SessionStateKey] = RoomSettings.SessionStateSummary;
        props[RoomSettings.RoundEndUtcMsKey] = -1d;
        props[RoomSettings.CrazyEnemiesActiveKey] = false;
        props[RoomSettings.FogOfWarActiveKey] = false;
        props[RoomSettings.PirateBaseActiveKey] = false;
        props[RoomSettings.AsteroidShowerActiveKey] = false;
        props[RoomSettings.CosmicWormActiveKey] = false;
        props[LoneShipModeStartTimeKey] = -1d;
        props[GameTimer.EvacuationPauseUntilKey] = -1d;
        props[GameTimer.EvacuationPauseRemainingKey] = -1f;
        props[RoomSettings.GadgetChargesStateKey] = string.Empty;
        props[RoomSettings.RepairBayOccupancyStateKey] = string.Empty;
        props[RoomSettings.SpaceFactoryStateKey] = string.Empty;
        props[RoomSettings.SpaceFactoryOccupancyStateKey] = string.Empty;
        props[RoomSettings.ScienceStationOccupancyStateKey] = string.Empty;
        props[RoomSettings.RoundResultsKey] = RoundResultsTracker.SerializeSnapshot(snapshot);
        props[RoomSettings.RoundEndReasonKey] = snapshot != null ? snapshot.endReason : endReason;

        if (PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }
    }

    public void LeaveRoomToProfile()
    {
        if (leavingRoomToProfile)
            return;

        leavingRoomToProfile = true;
        GameplayHudVisibility.ResetSuppression();
        EarlyRoundExitUI.HideAll();

        PlayerMovement.gameStarted = false;
        PlayerShooting.gameStarted = false;
        ShipDamageState.ClearAllRuntimeDamage();
        TreasureCollector.ResetRoundReservations();
        RoundWarmupService.ResetRoundTransientEffects();

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
        EarlyRoundExitUI.HideAll();
        RoundPilotHudUI.DestroyAllRuntimeObjects();
        ShipDamageState.ClearAllRuntimeDamage();
        TreasureCollector.ResetRoundReservations();
        RoundWarmupService.ResetRoundTransientEffects();

        Hashtable props = new Hashtable();
        props["gameStarted"] = false;
        props[RoomSettings.RoundWarmupTokenKey] = string.Empty;
        props[RoomSettings.RoundWarmupStartedAtKey] = -1d;
        props[ObstacleLayoutKey] = string.Empty;
        props[ExtractionLayoutKey] = string.Empty;
        props[NebulaLayoutKey] = string.Empty;
        props[FireNebulaLayoutKey] = string.Empty;
        props[ToxicNebulaLayoutKey] = string.Empty;
        props[CloudLayoutKey] = string.Empty;
        props[CloudDirectionKey] = string.Empty;
        props[RepairBayLayoutKey] = string.Empty;
        props[SpaceFactoryLayoutKey] = string.Empty;
        props[ScienceStationLayoutKey] = string.Empty;
        props[ArtifactAsteroidLayoutKey] = string.Empty;
        props[RoomSettings.ArtifactAsteroidsStateKey] = string.Empty;
        props[MapSeedKey] = -1;
        props[RoomSettings.StartTimeKey] = -1d;
        props[RoomSettings.RoundEndUtcMsKey] = -1d;
        props[RoomSettings.SessionStateKey] = RoomSettings.SessionStateInLobby;
        props[RoomSettings.CrazyEnemiesActiveKey] = false;
        props[RoomSettings.FogOfWarActiveKey] = false;
        props[RoomSettings.PirateBaseActiveKey] = false;
        props[RoomSettings.AsteroidShowerActiveKey] = false;
        props[RoomSettings.CosmicWormActiveKey] = false;
        props[LoneShipModeStartTimeKey] = -1d;
        props[GameTimer.EvacuationPauseUntilKey] = -1d;
        props[GameTimer.EvacuationPauseRemainingKey] = -1f;
        props[RoomSettings.GadgetChargesStateKey] = string.Empty;
        props[RoomSettings.RepairBayOccupancyStateKey] = string.Empty;
        props[RoomSettings.SpaceFactoryStateKey] = string.Empty;
        props[RoomSettings.SpaceFactoryOccupancyStateKey] = string.Empty;
        props[RoomSettings.ScienceStationOccupancyStateKey] = string.Empty;
        props[RoomSettings.RoundResultsKey] = string.Empty;
        props[RoomSettings.FinishedRoundResultsKey] = string.Empty;
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
