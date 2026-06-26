using Photon.Pun;
using UnityEngine;

public static class EnemySpaceAnimalAlphaSpecimenUtility
{
    public static bool IsProtectedPlayer(PlayerHealth candidate)
    {
        if (candidate == null ||
            !candidate.gameObject.activeInHierarchy ||
            !candidate.IsHumanShipControlled ||
            candidate.IsWreck ||
            candidate.IsEvacuationAnimating ||
            candidate.photonView == null)
        {
            return false;
        }

        return PlayerProfileService.PlayerHasPreservedAlphaSpecimenInSafePocket(candidate.photonView.Owner);
    }

    public static bool IsProtectedPlayerTarget(Transform target)
    {
        PlayerHealth health = target != null ? target.GetComponent<PlayerHealth>() : null;
        return IsProtectedPlayer(health);
    }

    public static bool IsRetaliationTarget(PlayerHealth candidate, int retaliationTargetViewId)
    {
        return candidate != null &&
               retaliationTargetViewId > 0 &&
               candidate.photonView != null &&
               candidate.photonView.ViewID == retaliationTargetViewId;
    }

    public static bool CanMantaPassivelyTarget(PlayerHealth candidate, int retaliationTargetViewId)
    {
        return !IsProtectedPlayer(candidate) || IsRetaliationTarget(candidate, retaliationTargetViewId);
    }

    public static PlayerHealth FindClosestProtectedPlayer(
        Vector2 origin,
        PlayerHealth observerHealth,
        float maxDistance,
        bool requireNebulaVisibility,
        int ignoredViewId)
    {
        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        PlayerHealth best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (!IsProtectedPlayer(candidate))
                continue;

            if (ignoredViewId > 0 && candidate.photonView != null && candidate.photonView.ViewID == ignoredViewId)
                continue;

            if (candidate == observerHealth ||
                candidate.GetComponent<LureBeaconDecoy>() != null ||
                !ActorIdentity.CanBeTargetedByMonstersActor(candidate) ||
                !MapInstanceService.IsSameInstance(origin, candidate.transform.position))
            {
                continue;
            }

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance > maxDistance || distance >= bestDistance)
                continue;

            if (requireNebulaVisibility)
            {
                HideInNebulaTarget candidateNebulaState = candidate.GetComponent<HideInNebulaTarget>();
                HideInNebulaTarget observerNebulaState = observerHealth != null ? observerHealth.GetComponent<HideInNebulaTarget>() : null;
                if (candidateNebulaState != null && candidateNebulaState.IsHiddenFromObserver(observerNebulaState))
                    continue;
            }

            bestDistance = distance;
            best = candidate;
        }

        return best;
    }
}
