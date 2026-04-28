using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[Serializable]
public class RoundResultEntry
{
    public int actorNumber;
    public string nickname;
    public int finalScore;
    public int placement;
    public string outcome;
}

[Serializable]
public class RoundResultsSnapshotData
{
    public string endReason;
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

    public static void RecordOutcome(Player player, int finalScore, string outcome)
    {
        if (player == null)
            return;

        EnsureRoomScope();
        outcome = string.IsNullOrWhiteSpace(outcome) ? "active" : outcome;
        finalScore = RoundXpTracker.FinalizeRoundXp(player, finalScore, outcome);
        TrackedRoundResult result = GetOrCreate(player);
        result.Score = Mathf.Max(0, finalScore);
        result.HasScore = true;
        result.Outcome = outcome;
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

        List<RoundResultEntry> entries = new List<RoundResultEntry>();
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

            entries.Add(new RoundResultEntry
            {
                actorNumber = player.ActorNumber,
                nickname = GetDisplayName(player),
                finalScore = Mathf.Max(0, score),
                outcome = outcome
            });
        }

        List<RoundResultEntry> ordered = entries
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
