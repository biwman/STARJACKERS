using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public static class VectorWeakPointMemory
{
    const float ResetSeconds = 3f;
    const int MaxBonusStacks = 4;
    const float BonusPerStack = 0.05f;

    struct HitState
    {
        public string TargetKey;
        public int ConsecutiveHits;
        public float LastHitTime;
    }

    static readonly Dictionary<int, HitState> StatesByAttackerViewId = new Dictionary<int, HitState>();

    public static void ResetForSessionTransition()
    {
        StatesByAttackerViewId.Clear();
    }

    public static float RegisterHitAndGetMultiplier(int attackerViewId, string targetKey)
    {
        if (attackerViewId <= 0 || string.IsNullOrWhiteSpace(targetKey))
            return 1f;

        PruneExpiredOrMissingAttackers();

        PhotonView attackerView = PhotonView.Find(attackerViewId);
        if (attackerView == null || !PilotCatalog.IsSelectedPilot(attackerView.Owner, PilotCatalog.VectorId))
            return 1f;

        int consecutiveHits = 1;
        if (StatesByAttackerViewId.TryGetValue(attackerViewId, out HitState previous) &&
            string.Equals(previous.TargetKey, targetKey, System.StringComparison.Ordinal) &&
            Time.time <= previous.LastHitTime + ResetSeconds)
        {
            consecutiveHits = Mathf.Clamp(previous.ConsecutiveHits + 1, 1, MaxBonusStacks + 1);
        }

        StatesByAttackerViewId[attackerViewId] = new HitState
        {
            TargetKey = targetKey,
            ConsecutiveHits = consecutiveHits,
            LastHitTime = Time.time
        };

        int bonusStacks = Mathf.Clamp(consecutiveHits - 1, 0, MaxBonusStacks);
        return 1f + (bonusStacks * BonusPerStack);
    }

    static void PruneExpiredOrMissingAttackers()
    {
        if (StatesByAttackerViewId.Count == 0)
            return;

        List<int> expired = null;
        foreach (KeyValuePair<int, HitState> pair in StatesByAttackerViewId)
        {
            if (Time.time <= pair.Value.LastHitTime + ResetSeconds && PhotonView.Find(pair.Key) != null)
                continue;

            if (expired == null)
                expired = new List<int>();

            expired.Add(pair.Key);
        }

        if (expired == null)
            return;

        for (int i = 0; i < expired.Count; i++)
            StatesByAttackerViewId.Remove(expired[i]);
    }
}
