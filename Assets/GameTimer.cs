using UnityEngine;
using Photon.Pun;
using TMPro;
using ExitGames.Client.Photon;
using System.Collections.Generic;

public class GameTimer : MonoBehaviourPun
{
    const string ObstacleLayoutKey = "obstacleLayout";
    const string ExtractionLayoutKey = "extractionLayout";
    const string NebulaLayoutKey = "nebulaLayout";
    const string FireNebulaLayoutKey = NebulaSpawner.FireNebulaLayoutKey;
    const string CloudLayoutKey = NebulaSpawner.CloudLayoutKey;
    const string CloudDirectionKey = NebulaSpawner.CloudDirectionKey;
    const string RepairBayLayoutKey = "repairBayLayout";
    const string SpaceFactoryLayoutKey = SpaceFactorySpawner.LayoutKey;
    const string ScienceStationLayoutKey = ScienceStationSpawner.LayoutKey;
    const string MapSeedKey = "mapSeed";
    const string LoneShipModeStartTimeKey = "loneShipModeStartTime";
    const float TimeUpEvacuationGraceSeconds = 4.4f;
    public const string EvacuationPauseUntilKey = "evacPauseUntil";
    public const string EvacuationPauseRemainingKey = "evacPauseRemaining";

    public float roundTime = 180f;

    private TMP_Text timerText;
    private double startTime;
    bool isEndingRound;
    bool hasSeenMultipleActivePlayers;
    float pausedTimeRemaining;

    void Start()
    {
        EndDisasterMeteorVfx.EnsureExists();
        RepairBaySpawner.EnsureExists();
        SpaceJunkSpawner.EnsureExists();
        ContainerSpawner.EnsureExists();
        RandomLootWreckSpawner.EnsureExists();
        SpaceFactorySpawner.EnsureExists();
        ScienceStationSpawner.EnsureExists();
        FogOfWarOverlay.EnsureExists();
        NeutralRiderManager.EnsureExists();

        GameObject obj = GameObject.Find("TimerText");

        if (obj != null)
        {
            timerText = obj.GetComponent<TMP_Text>();
        }
        else
        {
            Debug.LogError("Nie znaleziono TimerText");
        }
    }

    void Update()
    {
        if (!IsGameStarted())
        {
            hasSeenMultipleActivePlayers = false;
            pausedTimeRemaining = 0f;
            return;
        }

        roundTime = GetConfiguredRoundTime();

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("startTime", out object value))
        {
            startTime = (double)value;
        }
        else
        {
            return;
        }

        if (TryGetPausedRemaining(out float pausedRemaining))
        {
            pausedTimeRemaining = pausedRemaining;
            UpdateTimerUI(pausedTimeRemaining);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            UpdateLoneShipTimerMode();
            if (CountActivePlayers() <= 0 && !HasActiveDropbotCargoInFlight() && !isEndingRound)
            {
                StartRoundEmptyFieldEnd();
                return;
            }
        }

        double elapsed = GetElapsedRoundTime();
        float remaining = roundTime - (float)elapsed;
        remaining = Mathf.Max(0f, remaining);

        UpdateTimerUI(remaining);

        if (remaining <= 0f && PhotonNetwork.IsMasterClient)
        {
            StartRoundTimeout();
        }
    }

    void UpdateTimerUI(float time)
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            timerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
        }
    }

    void StartRoundTimeout()
    {
        if (!IsGameStarted() || isEndingRound)
            return;

        isEndingRound = true;
        StartCoroutine(EndGameAfterTimeUpSync());
    }

    void StartRoundEmptyFieldEnd()
    {
        if (!IsGameStarted() || isEndingRound)
            return;

        isEndingRound = true;
        GameManager manager = FindAnyObjectByType<GameManager>();
        if (manager != null)
        {
            manager.EndGame("no_survivors");
        }
        isEndingRound = false;
    }

    System.Collections.IEnumerator EndGameAfterTimeUpSync()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        bool evacuationInProgress = false;
        foreach (PlayerHealth p in players)
        {
            if (p == null || p.IsWreck)
                continue;

            if (p.IsEvacuationAnimating)
            {
                evacuationInProgress = true;
                continue;
            }

            PhotonView pv = p.photonView;
            if (pv != null)
            {
                if (!p.IsBotControlled && !p.IsNeutralRiderControlled)
                {
                    if (!p.IsAstronautControlled)
                    {
                        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(pv.Owner, 0);
                        pv.RPC(
                            nameof(PlayerHealth.ApplyLocalShipLossForWreck),
                            pv.Owner,
                            shipSkinIndex,
                            RoomSettings.IsInventoryLossEnabled(),
                            RoomSettings.IsEquipmentLossEnabled(),
                            string.Empty,
                            string.Empty);
                    }

                    int currentScore = RoundResultsTracker.GetKnownScore(pv.Owner, p.gameObject);
                    RoundResultsTracker.RecordOutcome(pv.Owner, currentScore, "lost_in_space");
                }

                if (!p.IsNeutralRiderControlled)
                    pv.RPC("OnTimeUp", pv.Owner);
            }
        }

        yield return new WaitForSeconds(evacuationInProgress ? TimeUpEvacuationGraceSeconds : 1.8f);

        GameManager manager = FindAnyObjectByType<GameManager>();
        if (manager != null)
        {
            manager.EndGame("time_up");
        }

        isEndingRound = false;
    }

    public static void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        RoundWarmupService.BeginRoundStartPreparation();
    }

    void UpdateLoneShipTimerMode()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        int activePlayers = CountActivePlayers();
        if (activePlayers >= 2)
        {
            hasSeenMultipleActivePlayers = true;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(LoneShipModeStartTimeKey, out object existingValue) &&
            existingValue is double existingStartTime &&
            existingStartTime >= 0d)
        {
            return;
        }

        if (!hasSeenMultipleActivePlayers || activePlayers > 1)
            return;

        Hashtable props = new Hashtable();
        props[LoneShipModeStartTimeKey] = PhotonNetwork.Time;
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    double GetElapsedRoundTime()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return PhotonNetwork.Time - startTime;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(LoneShipModeStartTimeKey, out object value) &&
            value is double loneShipModeStartTime &&
            loneShipModeStartTime >= 0d)
        {
            double elapsedBefore = loneShipModeStartTime - startTime;
            double elapsedAfter = (PhotonNetwork.Time - loneShipModeStartTime) * RoomSettings.GetLastShipTimerMultiplier();
            return elapsedBefore + elapsedAfter;
        }

        return PhotonNetwork.Time - startTime;
    }

    int CountActivePlayers()
    {
        int count = 0;
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            if (!IsActiveRoundPlayer(players[i]))
                continue;

            count++;
        }

        return count;
    }

    bool HasActiveDropbotCargoInFlight()
    {
        IReadOnlyCollection<PlayerDeployableBase> deployables = PlayerDeployableBase.GetActiveDeployables();
        foreach (PlayerDeployableBase deployable in deployables)
        {
            if (deployable is DropbotDeployable dropbot && dropbot.HasCargoInFlight)
                return true;
        }

        return false;
    }

    public static bool IsActiveRoundPlayer(PlayerHealth player)
    {
        if (player == null ||
            !player.isActiveAndEnabled ||
            player.IsWreck ||
            player.IsBotControlled ||
            player.IsNeutralRiderControlled ||
            player.IsEvacuationAnimating ||
            player.photonView == null)
        {
            return false;
        }

        object[] instantiationData = player.photonView.InstantiationData;
        if (PlayerDeployableRuntime.IsInstantiationData(instantiationData) ||
            LureBeaconDecoy.IsInstantiationData(instantiationData) ||
            player.GetComponent<PlayerDeployableBase>() != null ||
            player.GetComponent<LureBeaconDecoy>() != null)
        {
            return false;
        }

        return true;
    }

    bool IsGameStarted()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value))
        {
            return (bool)value;
        }

        return false;
    }

    float GetConfiguredRoundTime()
    {
        return RoomSettings.GetRoundDuration();
    }

    bool TryGetPausedRemaining(out float remaining)
    {
        remaining = 0f;
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(EvacuationPauseUntilKey, out object untilValue) ||
            untilValue is not double pauseUntil ||
            pauseUntil < 0d ||
            PhotonNetwork.Time >= pauseUntil)
        {
            return false;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(EvacuationPauseRemainingKey, out object remainingValue))
            return false;

        remaining = remainingValue switch
        {
            float asFloat => asFloat,
            double asDouble => (float)asDouble,
            int asInt => asInt,
            _ => 0f
        };

        return remaining > 0f;
    }

    public float GetCurrentRemainingTime()
    {
        if (!IsGameStarted())
            return roundTime;

        double elapsed = GetElapsedRoundTime();
        return Mathf.Max(0f, roundTime - (float)elapsed);
    }

    public static void SetExtractionPause(float remainingTime, float durationSeconds)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable
        {
            [EvacuationPauseUntilKey] = PhotonNetwork.Time + durationSeconds,
            [EvacuationPauseRemainingKey] = remainingTime
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }
}
