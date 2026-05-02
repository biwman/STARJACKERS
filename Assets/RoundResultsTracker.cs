using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[Serializable]
public class RoundResultEntry
{
    public int actorNumber;
    public string playerId;
    public string nickname;
    public int finalScore;
    public int placement;
    public string outcome;
    public string roundToken;
    public double finishedAt;
}

[Serializable]
public class RoundResultsSnapshotData
{
    public string endReason;
    public RoundResultEntry[] entries;
}

[Serializable]
public class FinishedRoundResultsData
{
    public RoundResultEntry[] entries;
}

public static class RoundResultsTracker
{
    class TrackedRoundResult
    {
        public int ActorNumber;
        public string Nickname;
        public int Score;
        public bool HasScore;
        public string Outcome;
    }

    static readonly Dictionary<int, TrackedRoundResult> TrackedResults = new Dictionary<int, TrackedRoundResult>();
    static string trackedRoomName;

    public static void ResetForCurrentRoom()
    {
        EnsureRoomScope();
        TrackedResults.Clear();
        RoundXpTracker.ResetForCurrentRoom();
    }

    public static void RecordScore(Player player, int score)
    {
        if (player == null)
            return;

        EnsureRoomScope();
        TrackedRoundResult result = GetOrCreate(player);
        result.Score = Mathf.Max(0, score);
        result.HasScore = true;
    }

    public static int RecordOutcome(Player player, int finalScore, string outcome)
    {
        if (player == null)
            return Mathf.Max(0, finalScore);

        EnsureRoomScope();
        outcome = string.IsNullOrWhiteSpace(outcome) ? "active" : outcome;
        finalScore = RoundXpTracker.FinalizeRoundXp(player, finalScore, outcome);
        TrackedRoundResult result = GetOrCreate(player);
        result.Score = Mathf.Max(0, finalScore);
        result.HasScore = true;
        result.Outcome = outcome;
        PersistFinishedResult(player, result.Score, outcome);
        return result.Score;
    }

    public static int GetKnownScore(Player player, GameObject sceneObject = null)
    {
        if (player == null)
            return 0;

        EnsureRoomScope();

        if (TrackedResults.TryGetValue(player.ActorNumber, out TrackedRoundResult tracked) && tracked.HasScore)
        {
            return tracked.Score;
        }

        int sceneScore = GetSceneScore(player, sceneObject);
        if (sceneScore > 0)
            return sceneScore;

        return RoomSettings.GetPlayerScore(player);
    }

    public static RoundResultsSnapshotData BuildSnapshot(string endReason)
    {
        EnsureRoomScope();

        Dictionary<int, RoundResultEntry> entriesByActor = new Dictionary<int, RoundResultEntry>();

        RoundResultEntry[] finishedEntries = LoadFinishedRoundResultsFromRoom();
        for (int i = 0; i < finishedEntries.Length; i++)
        {
            UpsertEntry(entriesByActor, CloneEntry(finishedEntries[i]));
        }

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player == null)
                continue;

            TrackedResults.TryGetValue(player.ActorNumber, out TrackedRoundResult tracked);
            int score = tracked != null && tracked.HasScore
                ? tracked.Score
                : GetKnownScore(player);

            string outcome = tracked != null && !string.IsNullOrWhiteSpace(tracked.Outcome)
                ? tracked.Outcome
                : "active";

            score = RoundXpTracker.FinalizeRoundXp(player, score, outcome);

            UpsertEntry(entriesByActor, new RoundResultEntry
            {
                actorNumber = player.ActorNumber,
                playerId = player.UserId ?? string.Empty,
                nickname = GetDisplayName(player),
                finalScore = Mathf.Max(0, score),
                outcome = NormalizeOutcome(outcome),
                roundToken = BuildCurrentRoundToken(),
                finishedAt = 0d
            });
        }

        List<RoundResultEntry> ordered = entriesByActor.Values
            .OrderByDescending(entry => entry.finalScore)
            .ThenBy(entry => entry.nickname)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].placement = i + 1;
        }

        return new RoundResultsSnapshotData
        {
            endReason = string.IsNullOrWhiteSpace(endReason) ? "generic" : endReason,
            entries = ordered.ToArray()
        };
    }

    public static RoundResultsSnapshotData BuildFinishedSnapshotForEarlySummary(Player localPlayer, int localFinalScore, string localOutcome)
    {
        EnsureRoomScope();

        Dictionary<int, RoundResultEntry> entriesByActor = new Dictionary<int, RoundResultEntry>();
        string currentToken = BuildCurrentRoundToken();
        RoundResultEntry[] finishedEntries = LoadFinishedRoundResultsFromRoom();
        for (int i = 0; i < finishedEntries.Length; i++)
        {
            RoundResultEntry entry = finishedEntries[i];
            if (entry == null)
                continue;

            if (!string.IsNullOrWhiteSpace(entry.roundToken) &&
                !string.IsNullOrWhiteSpace(currentToken) &&
                !string.Equals(entry.roundToken, currentToken, StringComparison.Ordinal))
            {
                continue;
            }

            if (IsFinalOutcome(entry.outcome))
                UpsertEntry(entriesByActor, CloneEntry(entry));
        }

        if (localPlayer != null)
        {
            UpsertEntry(entriesByActor, new RoundResultEntry
            {
                actorNumber = localPlayer.ActorNumber,
                playerId = localPlayer.UserId ?? string.Empty,
                nickname = GetDisplayName(localPlayer),
                finalScore = Mathf.Max(0, localFinalScore),
                outcome = NormalizeOutcome(localOutcome),
                roundToken = currentToken,
                finishedAt = PhotonNetwork.Time
            });
        }

        List<RoundResultEntry> ordered = entriesByActor.Values
            .OrderByDescending(entry => entry.finalScore)
            .ThenBy(entry => entry.nickname)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].placement = i + 1;
        }

        return new RoundResultsSnapshotData
        {
            endReason = "early_finished",
            entries = ordered.ToArray()
        };
    }

    public static string SerializeSnapshot(RoundResultsSnapshotData snapshot)
    {
        if (snapshot == null)
            return string.Empty;

        return JsonUtility.ToJson(snapshot);
    }

    public static RoundResultsSnapshotData DeserializeSnapshot(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonUtility.FromJson<RoundResultsSnapshotData>(raw);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("RoundResultsTracker: failed to parse snapshot: " + ex.Message);
            return null;
        }
    }

    static TrackedRoundResult GetOrCreate(Player player)
    {
        if (!TrackedResults.TryGetValue(player.ActorNumber, out TrackedRoundResult result))
        {
            result = new TrackedRoundResult
            {
                ActorNumber = player.ActorNumber,
                Nickname = GetDisplayName(player),
                Outcome = "active"
            };
            TrackedResults[player.ActorNumber] = result;
        }

        result.Nickname = GetDisplayName(player);
        return result;
    }

    static void PersistFinishedResult(Player player, int finalScore, string outcome)
    {
        if (player == null || PhotonNetwork.CurrentRoom == null)
            return;

        outcome = NormalizeOutcome(outcome);
        if (!IsFinalOutcome(outcome))
            return;

        Dictionary<int, RoundResultEntry> entriesByActor = new Dictionary<int, RoundResultEntry>();
        RoundResultEntry[] existingEntries = LoadFinishedRoundResultsFromRoom();
        for (int i = 0; i < existingEntries.Length; i++)
        {
            UpsertEntry(entriesByActor, CloneEntry(existingEntries[i]));
        }

        UpsertEntry(entriesByActor, new RoundResultEntry
        {
            actorNumber = player.ActorNumber,
            playerId = player.UserId ?? string.Empty,
            nickname = GetDisplayName(player),
            finalScore = Mathf.Max(0, finalScore),
            outcome = outcome,
            roundToken = BuildCurrentRoundToken(),
            finishedAt = PhotonNetwork.Time
        });

        FinishedRoundResultsData data = new FinishedRoundResultsData
        {
            entries = entriesByActor.Values
                .OrderBy(entry => entry.actorNumber)
                .ToArray()
        };

        Hashtable props = new Hashtable
        {
            [RoomSettings.FinishedRoundResultsKey] = JsonUtility.ToJson(data)
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    static RoundResultEntry[] LoadFinishedRoundResultsFromRoom()
    {
        if (PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.FinishedRoundResultsKey, out object value) ||
            value is not string raw ||
            string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<RoundResultEntry>();
        }

        try
        {
            FinishedRoundResultsData data = JsonUtility.FromJson<FinishedRoundResultsData>(raw);
            return data?.entries ?? Array.Empty<RoundResultEntry>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("RoundResultsTracker: failed to parse finished results: " + ex.Message);
            return Array.Empty<RoundResultEntry>();
        }
    }

    static void UpsertEntry(Dictionary<int, RoundResultEntry> entriesByActor, RoundResultEntry incoming)
    {
        if (entriesByActor == null || incoming == null || incoming.actorNumber <= 0)
            return;

        incoming.outcome = NormalizeOutcome(incoming.outcome);
        incoming.finalScore = Mathf.Max(0, incoming.finalScore);
        if (string.IsNullOrWhiteSpace(incoming.nickname))
            incoming.nickname = "Player " + incoming.actorNumber;

        if (!entriesByActor.TryGetValue(incoming.actorNumber, out RoundResultEntry existing) || existing == null)
        {
            entriesByActor[incoming.actorNumber] = incoming;
            return;
        }

        bool existingFinal = IsFinalOutcome(existing.outcome);
        bool incomingFinal = IsFinalOutcome(incoming.outcome);

        if (existingFinal && !incomingFinal)
            return;

        if (!existingFinal && incomingFinal)
        {
            entriesByActor[incoming.actorNumber] = incoming;
            return;
        }

        if (incoming.finalScore >= existing.finalScore)
        {
            entriesByActor[incoming.actorNumber] = incoming;
        }
    }

    static RoundResultEntry CloneEntry(RoundResultEntry entry)
    {
        if (entry == null)
            return null;

        return new RoundResultEntry
        {
            actorNumber = entry.actorNumber,
            playerId = entry.playerId ?? string.Empty,
            nickname = entry.nickname ?? string.Empty,
            finalScore = Mathf.Max(0, entry.finalScore),
            placement = entry.placement,
            outcome = NormalizeOutcome(entry.outcome),
            roundToken = entry.roundToken ?? string.Empty,
            finishedAt = entry.finishedAt
        };
    }

    static bool IsFinalOutcome(string outcome)
    {
        outcome = NormalizeOutcome(outcome);
        return outcome == "dead" ||
               outcome == "lost_in_space" ||
               outcome == "time_up" ||
               outcome == "extracted" ||
               outcome == "evacuated";
    }

    static string NormalizeOutcome(string outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome))
            return "active";

        return outcome.Trim().ToLowerInvariant();
    }

    static string BuildCurrentRoundToken()
    {
        string roomName = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name ?? string.Empty : string.Empty;
        string startTime = "nostart";
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object value) &&
            value != null)
        {
            startTime = value.ToString();
        }

        return roomName + "_" + startTime;
    }

    static int GetSceneScore(Player player, GameObject sceneObject = null)
    {
        TreasureCollector directCollector = sceneObject != null ? sceneObject.GetComponent<TreasureCollector>() : null;
        if (directCollector != null)
            return directCollector.totalScore;

        PlayerHealth[] players = UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth current = players[i];
            if (current == null || current.photonView == null || current.photonView.Owner == null)
                continue;

            if (current.IsBotControlled)
                continue;

            if (current.photonView.Owner.ActorNumber != player.ActorNumber)
                continue;

            TreasureCollector collector = current.GetComponent<TreasureCollector>();
            if (collector != null)
                return collector.totalScore;
        }

        return 0;
    }

    static void EnsureRoomScope()
    {
        string currentRoomName = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null
            ? PhotonNetwork.CurrentRoom.Name ?? string.Empty
            : string.Empty;

        if (trackedRoomName == currentRoomName)
            return;

        trackedRoomName = currentRoomName;
        TrackedResults.Clear();
    }

    static string GetDisplayName(Player player)
    {
        if (player == null)
            return "Unknown";

        if (!string.IsNullOrWhiteSpace(player.NickName))
            return player.NickName;

        return "Player " + player.ActorNumber;
    }
}
