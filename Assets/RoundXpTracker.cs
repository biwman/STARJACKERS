using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public static class RoundXpTracker
{
    class PlayerRoundXpState
    {
        public int Score;
        public int CollectedItems;
        public bool FirstLootAwarded;
        public bool Collector15Awarded;
        public bool ShipDestroyed;
        public bool Finalized;
        public readonly Dictionary<int, int> ShieldXpByTarget = new Dictionary<int, int>();
    }

    static readonly Dictionary<int, PlayerRoundXpState> PlayerStates = new Dictionary<int, PlayerRoundXpState>();
    static string trackedRoomName;
    static bool firstBloodAwarded;

    public static void ResetForCurrentRoom()
    {
        trackedRoomName = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null
            ? PhotonNetwork.CurrentRoom.Name ?? string.Empty
            : string.Empty;

        PlayerStates.Clear();
        firstBloodAwarded = false;
    }

    public static int RecordTreasureCollected(Player player, string itemId)
    {
        if (player == null)
            return 0;

        PlayerRoundXpState state = GetOrCreate(player);
        int xp = RoundXpBalance.GetTreasureCollectXp(itemId);
        state.CollectedItems++;

        if (!state.FirstLootAwarded)
        {
            state.FirstLootAwarded = true;
            xp += RoundXpBalance.FirstLootXp;
        }

        if (!state.Collector15Awarded && state.CollectedItems >= 15)
        {
            state.Collector15Awarded = true;
            xp += RoundXpBalance.Collector15Xp;
        }

        return xp;
    }

    public static int RecordWreckLooted(Player player, bool playerWreck)
    {
        if (player == null)
            return 0;

        PlayerRoundXpState state = GetOrCreate(player);
        int xp = playerWreck ? RoundXpBalance.PlayerWreckLootXp : RoundXpBalance.EnemyWreckLootXp;
        state.CollectedItems++;
        if (!state.FirstLootAwarded)
        {
            state.FirstLootAwarded = true;
            xp += RoundXpBalance.FirstLootXp;
        }

        return xp;
    }

    public static int RecordDroppedCargoLooted(Player player)
    {
        if (player == null)
            return 0;

        PlayerRoundXpState state = GetOrCreate(player);
        int xp = RoundXpBalance.DroppedCargoLootXp;
        state.CollectedItems++;
        if (!state.FirstLootAwarded)
        {
            state.FirstLootAwarded = true;
            xp += RoundXpBalance.FirstLootXp;
        }

        return xp;
    }

    public static void RecordDamage(int attackerViewId, PhotonView targetView, int shieldDamage, int hpDamage)
    {
        if (!PhotonNetwork.IsMasterClient || attackerViewId <= 0 || targetView == null || targetView.Owner == null)
            return;

        PhotonView attackerView = PhotonView.Find(attackerViewId);
        if (attackerView == null || attackerView.Owner == null)
            return;

        if (attackerView.Owner.ActorNumber == targetView.Owner.ActorNumber)
            return;

        int xp = 0;
        PlayerRoundXpState state = GetOrCreate(attackerView.Owner);
        if (shieldDamage > 0)
        {
            state.ShieldXpByTarget.TryGetValue(targetView.ViewID, out int awardedAgainstTarget);
            int remainingCap = Mathf.Max(0, RoundXpBalance.ShieldHitXpCapPerTarget - awardedAgainstTarget);
            int shieldXp = Mathf.Min(remainingCap, RoundXpBalance.ShieldHitXp);
            if (shieldXp > 0)
            {
                state.ShieldXpByTarget[targetView.ViewID] = awardedAgainstTarget + shieldXp;
                xp += shieldXp;
            }
        }

        if (hpDamage > 0)
            xp += (hpDamage / RoundXpBalance.HpDamageChunk) * RoundXpBalance.HpDamageChunkXp;

        if (xp > 0)
            AddXp(attackerView.Owner, xp);
    }

    public static void RecordKill(int attackerViewId, PlayerHealth victim)
    {
        if (!PhotonNetwork.IsMasterClient || attackerViewId <= 0 || victim == null || victim.photonView == null)
            return;

        PhotonView attackerView = PhotonView.Find(attackerViewId);
        if (attackerView == null || attackerView.Owner == null)
            return;

        if (victim.photonView.Owner != null && attackerView.Owner.ActorNumber == victim.photonView.Owner.ActorNumber)
            return;

        int xp = 0;
        if (victim.IsBotControlled)
        {
            EnemyBot bot = victim.GetComponent<EnemyBot>();
            xp += RoundXpBalance.GetEnemyKillXp(bot != null ? bot.Kind : EnemyBotKind.Drone);
        }
        else
        {
            xp += RoundXpBalance.KillPlayerShipXp;
            if (!firstBloodAwarded)
            {
                firstBloodAwarded = true;
                xp += RoundXpBalance.FirstBloodXp;
            }
        }

        AddXp(attackerView.Owner, xp);
    }

    public static void RecordPlayerShipDestroyed(Player player)
    {
        if (player == null)
            return;

        GetOrCreate(player).ShipDestroyed = true;
    }

    public static void RecordGadgetSuccess(Player player, string itemId)
    {
        if (player == null || string.IsNullOrWhiteSpace(itemId))
            return;

        if (string.Equals(itemId, InventoryItemCatalog.MagneticBeamId, StringComparison.Ordinal))
        {
            AddXp(player, RoundXpBalance.MagneticBeamSuccessXp);
            return;
        }

        if (string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
            AddXp(player, RoundXpBalance.TractorBeamSuccessXp);
    }

    public static void RecordBatterySuccess(Player player, int expectedRestore)
    {
        if (player == null || expectedRestore < RoundXpBalance.BatteryMinimumRestoreForXp)
            return;

        AddXp(player, RoundXpBalance.BatterySuccessXp);
    }

    public static int FinalizeRoundXp(Player player, int currentScore, string outcome)
    {
        if (player == null)
            return Mathf.Max(0, currentScore);

        PlayerRoundXpState state = GetOrCreate(player);
        if (state.Finalized)
            return Mathf.Max(0, state.Score);

        int score = Mathf.Max(Mathf.Max(0, currentScore), GetBestKnownScore(player));
        outcome = string.IsNullOrWhiteSpace(outcome) ? "active" : outcome;

        if (GetElapsedRoundSeconds() >= RoundXpBalance.ParticipationMinSeconds)
            score += RoundXpBalance.ParticipationXp;

        if (IsLossOutcome(outcome) && GetElapsedRoundSeconds() >= RoundXpBalance.ActiveLossMinSeconds)
            score += RoundXpBalance.ActiveLossXp;

        if (string.Equals(outcome, "extracted", StringComparison.OrdinalIgnoreCase))
            score += RoundXpBalance.ShipExtractionXp;

        if (string.Equals(outcome, "evacuated", StringComparison.OrdinalIgnoreCase))
            score += RoundXpBalance.AstronautEvacuationXp;

        if (IsSurvivalOutcome(outcome) && !state.ShipDestroyed)
            score += RoundXpBalance.SurvivorXp;

        if (IsSurvivalOutcome(outcome))
            score += GetCargoEndBonus(player);

        score = RoundXpBalance.ApplyMapMultiplier(score, RoomSettings.GetSelectedLobbyMapId());
        state.Score = score;
        state.Finalized = true;
        SetLocalScorePropertyIfMine(player, score);
        return score;
    }

    public static void AddXp(Player player, int amount)
    {
        if (player == null || amount <= 0)
            return;

        PlayerRoundXpState state = GetOrCreate(player);
        int score = Mathf.Max(state.Score, GetBestKnownScore(player)) + amount;
        state.Score = score;
        SetLocalScorePropertyIfMine(player, score);
        RoundResultsTracker.RecordScore(player, score);
    }

    static PlayerRoundXpState GetOrCreate(Player player)
    {
        EnsureRoomScope();

        if (!PlayerStates.TryGetValue(player.ActorNumber, out PlayerRoundXpState state))
        {
            state = new PlayerRoundXpState
            {
                Score = Mathf.Max(0, RoomSettings.GetPlayerScore(player))
            };
            PlayerStates[player.ActorNumber] = state;
        }

        return state;
    }

    static int GetBestKnownScore(Player player)
    {
        int roomScore = RoomSettings.GetPlayerScore(player);
        int trackedScore = 0;
        if (PlayerStates.TryGetValue(player.ActorNumber, out PlayerRoundXpState state))
            trackedScore = state.Score;

        return Mathf.Max(roomScore, trackedScore);
    }

    static void SetLocalScorePropertyIfMine(Player player, int score)
    {
        if (player == null || PhotonNetwork.LocalPlayer == null || player.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
            return;

        GameObject playerObject = player.TagObject as GameObject;
        if (playerObject != null)
        {
            TreasureCollector collector = playerObject.GetComponent<TreasureCollector>();
            if (collector != null)
                collector.SetScoreTotal(score);
        }

        Hashtable props = new Hashtable
        {
            [RoomSettings.ScoreKey] = Mathf.Max(0, score)
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    static int GetCargoEndBonus(Player player)
    {
        string[] shipSlots = PlayerProfileService.GetPlayerShipInventorySlots(player);
        if (shipSlots == null || shipSlots.Length == 0)
            return 0;

        int filled = 0;
        int cargoValue = 0;
        for (int i = 0; i < shipSlots.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(shipSlots[i]))
                continue;

            filled++;
            cargoValue += InventoryItemCatalog.GetSellValueAstrons(shipSlots[i]);
        }

        int xp = filled >= shipSlots.Length ? RoundXpBalance.HeavyCargoXp : 0;
        if (cargoValue >= RoundXpBalance.RaiderCargoValueThreshold)
            xp += RoundXpBalance.RaiderXp;

        return xp;
    }

    static bool IsSurvivalOutcome(string outcome)
    {
        return string.Equals(outcome, "extracted", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(outcome, "evacuated", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(outcome, "active", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsLossOutcome(string outcome)
    {
        return string.Equals(outcome, "dead", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(outcome, "lost_in_space", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(outcome, "time_up", StringComparison.OrdinalIgnoreCase);
    }

    static float GetElapsedRoundSeconds()
    {
        if (PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object rawStart) ||
            !(rawStart is double startTime) ||
            startTime < 0d)
        {
            return 0f;
        }

        return Mathf.Max(0f, (float)(PhotonNetwork.Time - startTime));
    }

    static void EnsureRoomScope()
    {
        string currentRoomName = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null
            ? PhotonNetwork.CurrentRoom.Name ?? string.Empty
            : string.Empty;

        if (trackedRoomName == currentRoomName)
            return;

        trackedRoomName = currentRoomName;
        PlayerStates.Clear();
        firstBloodAwarded = false;
    }
}
