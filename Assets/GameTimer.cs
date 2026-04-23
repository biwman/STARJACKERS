using UnityEngine;
using Photon.Pun;
using TMPro;
using ExitGames.Client.Photon;

public class GameTimer : MonoBehaviourPun
{
    const string ObstacleLayoutKey = "obstacleLayout";
    const string ExtractionLayoutKey = "extractionLayout";
    const string NebulaLayoutKey = "nebulaLayout";
    const string MapSeedKey = "mapSeed";
    const string LoneShipModeStartTimeKey = "loneShipModeStartTime";
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
            if (CountActivePlayers() <= 0 && !isEndingRound)
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
        Debug.Log("KONIEC GRY");

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        foreach (PlayerHealth p in players)
        {
            if (p == null || p.IsWreck || p.IsEvacuationAnimating)
                continue;

            PhotonView pv = p.photonView;
            if (pv != null)
            {
                if (!p.IsBotControlled)
                {
                    if (!p.IsAstronautControlled)
                    {
                        pv.RPC(nameof(PlayerHealth.ClearLocalShipInventoryForWreck), pv.Owner);
                    }

                    int currentScore = RoundResultsTracker.GetKnownScore(pv.Owner, p.gameObject);
                    RoundResultsTracker.RecordOutcome(pv.Owner, currentScore, "lost_in_space");
                }
                pv.RPC("OnTimeUp", pv.Owner);
            }
        }

        yield return new WaitForSeconds(1.8f);

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

        Hashtable props = new Hashtable();
        props["gameStarted"] = true;
        props[RoomSettings.StartTimeKey] = PhotonNetwork.Time;
        props[RoomSettings.SessionStateKey] = RoomSettings.SessionStateInPlay;
        props[LoneShipModeStartTimeKey] = -1d;
        props[EvacuationPauseUntilKey] = -1d;
        props[EvacuationPauseRemainingKey] = -1f;
        props[RoomSettings.RoundResultsKey] = string.Empty;
        props[RoomSettings.RoundEndReasonKey] = string.Empty;
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RoundResultsTracker.ResetForCurrentRoom();
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
            if (players[i] == null || players[i].IsWreck || players[i].IsBotControlled)
                continue;

            count++;
        }

        return count;
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
