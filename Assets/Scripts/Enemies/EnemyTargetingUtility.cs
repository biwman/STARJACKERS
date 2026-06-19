using UnityEngine;

public static class EnemyTargetingUtility
{
    const float BeaconPriorityRangeMultiplier = 1.9f;

    public static Transform FindClosestTarget(Vector2 origin, PlayerHealth observerHealth, float maxDistance, bool requireNebulaVisibility, bool includeEnemyAstronauts = false)
    {
        Transform bestBeaconTarget = null;
        float bestBeaconDistance = float.MaxValue;

        float beaconRange = maxDistance * BeaconPriorityRangeMultiplier;
        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (!IsValidBeaconTarget(beacon, origin, beaconRange))
                continue;

            float distance = Vector2.Distance(origin, beacon.transform.position);
            if (distance >= bestBeaconDistance)
                continue;

            bestBeaconDistance = distance;
            bestBeaconTarget = beacon.transform;
        }

        if (bestBeaconTarget != null)
            return bestBeaconTarget;

        Transform bestDeployableTarget = null;
        float bestDeployableDistance = float.MaxValue;
        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (!IsValidDeployableTarget(deployable, origin, maxDistance))
                continue;

            float distance = Vector2.Distance(origin, deployable.transform.position);
            if (distance >= bestDeployableDistance)
                continue;

            bestDeployableDistance = distance;
            bestDeployableTarget = deployable.transform;
        }

        if (bestDeployableTarget != null)
            return bestDeployableTarget;

        Transform bestTarget = null;
        float bestDistance = float.MaxValue;

        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (!IsValidPlayerTarget(candidate, observerHealth, origin, maxDistance, requireNebulaVisibility, includeEnemyAstronauts))
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestTarget = candidate.transform;
        }

        return bestTarget;
    }

    public static bool IsTargetValid(Transform target, PlayerHealth observerHealth, Vector2 origin, float maxDistance, bool requireNebulaVisibility, bool includeEnemyAstronauts = false)
    {
        if (target == null)
            return false;

        PlayerHealth player = target.GetComponent<PlayerHealth>();
        if (player != null)
        {
            if (IsAnyBeaconAvailable(origin, maxDistance * BeaconPriorityRangeMultiplier))
                return false;

            if (IsAnyDeployableAvailable(origin, maxDistance))
                return false;

            return IsValidPlayerTarget(player, observerHealth, origin, maxDistance, requireNebulaVisibility, includeEnemyAstronauts);
        }

        PlayerDeployableBase deployable = target.GetComponent<PlayerDeployableBase>();
        if (deployable != null)
        {
            if (IsAnyBeaconAvailable(origin, maxDistance * BeaconPriorityRangeMultiplier))
                return false;

            return IsValidDeployableTarget(deployable, origin, maxDistance);
        }

        LureBeaconDecoy beacon = target.GetComponent<LureBeaconDecoy>();
        return IsValidBeaconTarget(beacon, origin, maxDistance * BeaconPriorityRangeMultiplier);
    }

    public static bool IsAnyTargetInRange(Vector2 origin, PlayerHealth observerHealth, float radius)
    {
        return FindClosestTarget(origin, observerHealth, radius, false) != null;
    }

    static bool IsValidPlayerTarget(PlayerHealth candidate, PlayerHealth observerHealth, Vector2 origin, float maxDistance, bool requireNebulaVisibility, bool includeEnemyAstronauts)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate == observerHealth || candidate.IsWreck || candidate.IsBotControlled || candidate.IsEvacuationAnimating)
            return false;

        if (includeEnemyAstronauts)
        {
            if (!ActorIdentity.CanBeTargetedByMonstersActor(candidate))
                return false;
        }
        else if (!ActorIdentity.CanBeTargetedByEnemyShipsActor(candidate))
        {
            return false;
        }

        if (candidate.GetComponent<LureBeaconDecoy>() != null)
            return false;

        if (!MapInstanceService.IsSameInstance(origin, candidate.transform.position))
            return false;

        if (Vector2.Distance(origin, candidate.transform.position) > maxDistance)
            return false;

        if (requireNebulaVisibility)
        {
            HideInNebulaTarget candidateNebulaState = candidate.GetComponent<HideInNebulaTarget>();
            HideInNebulaTarget observerNebulaState = observerHealth != null ? observerHealth.GetComponent<HideInNebulaTarget>() : null;
            if (candidateNebulaState != null && candidateNebulaState.IsHiddenFromObserver(observerNebulaState))
                return false;
        }

        return true;
    }

    static bool IsValidBeaconTarget(LureBeaconDecoy beacon, Vector2 origin, float maxDistance)
    {
        if (beacon == null || !beacon.CanBeTargeted)
            return false;

        if (!MapInstanceService.IsSameInstance(origin, beacon.transform.position))
            return false;

        return Vector2.Distance(origin, beacon.transform.position) <= maxDistance;
    }

    static bool IsValidDeployableTarget(PlayerDeployableBase deployable, Vector2 origin, float maxDistance)
    {
        if (deployable == null || !deployable.CanBeTargetedByEnemyBots)
            return false;

        if (!MapInstanceService.IsSameInstance(origin, deployable.transform.position))
            return false;

        return Vector2.Distance(origin, deployable.transform.position) <= maxDistance;
    }

    static bool IsAnyBeaconAvailable(Vector2 origin, float maxDistance)
    {
        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (IsValidBeaconTarget(beacon, origin, maxDistance))
                return true;
        }

        return false;
    }

    static bool IsAnyDeployableAvailable(Vector2 origin, float maxDistance)
    {
        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (IsValidDeployableTarget(deployable, origin, maxDistance))
                return true;
        }

        return false;
    }
}
