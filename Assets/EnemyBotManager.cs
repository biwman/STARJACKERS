using Photon.Pun;
using UnityEngine;

public class EnemyBotManager : MonoBehaviour
{
    const float ScanInterval = 0.2f;

    static EnemyBotManager instance;

    double lastHandledStartTime = double.MinValue;
    float nextScanTime;
    readonly System.Collections.Generic.Dictionary<EnemyBotKind, int> spawnedThisRound = new System.Collections.Generic.Dictionary<EnemyBotKind, int>();
    readonly System.Collections.Generic.Dictionary<EnemyBotKind, int> lastHandledRespawnTick = new System.Collections.Generic.Dictionary<EnemyBotKind, int>();
    bool rescueShipSummonUnlockedThisRound;
    bool hasRescueShipSummonFocus;
    Vector2 rescueShipSummonFocusPosition;
    int rescueShipSummonTargetViewId = -1;

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("EnemyBotManager");
        instance = root.AddComponent<EnemyBotManager>();
    }

    public static void NotifyRescueShipSummonTrigger(EnemyBot damagedBot)
    {
        if (instance == null || damagedBot == null)
            return;

        instance.RegisterRescueShipSummonTrigger(damagedBot);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (Time.unscaledTime < nextScanTime)
            return;

        nextScanTime = Time.unscaledTime + ScanInterval;
        EnsureRuntimeComponents();
        HandleSpawnLifecycle();
    }

    void EnsureRuntimeComponents()
    {
        PhotonView[] views = FindObjectsByType<PhotonView>(FindObjectsInactive.Exclude);
        for (int i = 0; i < views.Length; i++)
        {
            PhotonView view = views[i];
            if (view == null || view.gameObject == null || !view.gameObject.name.StartsWith("Player"))
                continue;

            if (EnemyBot.IsBotInstantiationData(view.InstantiationData))
            {
                EnemyBot bot = view.GetComponent<EnemyBot>();
                if (bot == null)
                    bot = view.gameObject.AddComponent<EnemyBot>();

                bot.InitializeFromPhotonData();
                continue;
            }

            if (AstronautSurvivor.IsAstronautInstantiationData(view.InstantiationData))
            {
                AstronautSurvivor astronaut = view.GetComponent<AstronautSurvivor>();
                if (astronaut == null)
                    astronaut = view.gameObject.AddComponent<AstronautSurvivor>();

                astronaut.InitializeFromPhotonData();
            }
        }
    }

    void HandleSpawnLifecycle()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        bool gameStarted = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object startedValue) &&
                           startedValue is bool started &&
                           started;

        if (!gameStarted)
        {
            lastHandledStartTime = double.MinValue;
            spawnedThisRound.Clear();
            lastHandledRespawnTick.Clear();
            ResetRescueShipSummonState();
            return;
        }

        double currentStartTime = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("startTime", out object startValue) && startValue is double roomStart
            ? roomStart
            : 0d;

        if (currentStartTime != lastHandledStartTime)
        {
            lastHandledStartTime = currentStartTime;
            spawnedThisRound.Clear();
            lastHandledRespawnTick.Clear();
            ResetRescueShipSummonState();
        }

        for (int i = 0; i < EnemyBotCatalog.AllDefinitions.Count; i++)
        {
            EnemyBotDefinition definition = EnemyBotCatalog.AllDefinitions[i];
            HandleEnemySpawn(definition, currentStartTime);
        }
    }

    void HandleEnemySpawn(EnemyBotDefinition definition, double currentStartTime)
    {
        if (definition == null)
            return;

        if (definition.Kind == EnemyBotKind.RescueShip)
        {
            HandleRescueShipSpawn(definition, currentStartTime);
            return;
        }

        if (!RoomSettings.GetEnemyEnabled(definition.Kind))
        {
            DestroyExistingBots(definition.Kind);
            spawnedThisRound[definition.Kind] = 0;
            lastHandledRespawnTick.Remove(definition.Kind);
            return;
        }

        double elapsed = currentStartTime > 0d ? PhotonNetwork.Time - currentStartTime : 0d;
        int spawnSecond = RoomSettings.GetEnemySpawnSecond(definition.Kind);
        if (currentStartTime > 0d)
        {
            if (elapsed < spawnSecond)
                return;
        }
        else if (spawnSecond > 0)
        {
            return;
        }

        int desiredCount = RoomSettings.GetEnemyCount(definition.Kind);
        int spawnedCount = GetSpawnedCount(definition.Kind);

        for (int i = spawnedCount; i < desiredCount; i++)
        {
            SpawnEnemy(definition, i);
            spawnedThisRound[definition.Kind] = i + 1;
        }

        if (!RoomSettings.GetEnemyRespawnEnabled(definition.Kind))
            return;

        int respawnInterval = RoomSettings.GetEnemyRespawnIntervalSeconds(definition.Kind);
        if (respawnInterval <= 0 || elapsed < respawnInterval || elapsed < spawnSecond)
            return;

        int currentRespawnTick = Mathf.FloorToInt((float)(elapsed / respawnInterval));
        int lastTick = lastHandledRespawnTick.TryGetValue(definition.Kind, out int storedTick) ? storedTick : 0;
        if (currentRespawnTick <= lastTick)
            return;

        lastHandledRespawnTick[definition.Kind] = currentRespawnTick;

        int activeCount = CountActiveNeutralBots(definition.Kind);
        int missingCount = Mathf.Max(0, desiredCount - activeCount);
        if (missingCount <= 0)
            return;

        int spawnBase = GetSpawnedCount(definition.Kind);
        for (int i = 0; i < missingCount; i++)
        {
            SpawnEnemy(definition, spawnBase + i);
        }

        spawnedThisRound[definition.Kind] = spawnBase + missingCount;
    }

    void HandleRescueShipSpawn(EnemyBotDefinition definition, double currentStartTime)
    {
        if (!RoomSettings.GetEnemyEnabled(definition.Kind))
        {
            DestroyExistingBots(definition.Kind);
            spawnedThisRound[definition.Kind] = 0;
            lastHandledRespawnTick.Remove(definition.Kind);
            ResetRescueShipSummonState();
            return;
        }

        double elapsed = currentStartTime > 0d ? PhotonNetwork.Time - currentStartTime : 0d;
        int spawnSecond = RoomSettings.GetEnemySpawnSecond(definition.Kind);
        if (currentStartTime > 0d)
        {
            if (elapsed < spawnSecond)
                return;
        }
        else if (spawnSecond > 0)
        {
            return;
        }

        if (!rescueShipSummonUnlockedThisRound)
            return;

        int desiredCount = RoomSettings.GetEnemyCount(definition.Kind);
        int spawnedCount = GetSpawnedCount(definition.Kind);
        bool hasSpawnFocus = TryGetRescueShipSpawnFocus(out Vector2 spawnFocus);

        for (int i = spawnedCount; i < desiredCount; i++)
        {
            SpawnEnemy(definition, i, true, hasSpawnFocus, spawnFocus);
            spawnedThisRound[definition.Kind] = i + 1;
        }

        if (!RoomSettings.GetEnemyRespawnEnabled(definition.Kind))
            return;

        int respawnInterval = RoomSettings.GetEnemyRespawnIntervalSeconds(definition.Kind);
        if (respawnInterval <= 0 || elapsed < respawnInterval || elapsed < spawnSecond)
            return;

        int currentRespawnTick = Mathf.FloorToInt((float)(elapsed / respawnInterval));
        int lastTick = lastHandledRespawnTick.TryGetValue(definition.Kind, out int storedTick) ? storedTick : 0;
        if (currentRespawnTick <= lastTick)
            return;

        lastHandledRespawnTick[definition.Kind] = currentRespawnTick;

        int activeCount = CountActiveNeutralBots(definition.Kind);
        int missingCount = Mathf.Max(0, desiredCount - activeCount);
        if (missingCount <= 0)
            return;

        int spawnBase = GetSpawnedCount(definition.Kind);
        for (int i = 0; i < missingCount; i++)
            SpawnEnemy(definition, spawnBase + i, true, hasSpawnFocus, spawnFocus);

        spawnedThisRound[definition.Kind] = spawnBase + missingCount;
    }

    void RegisterRescueShipSummonTrigger(EnemyBot damagedBot)
    {
        if (!PhotonNetwork.IsMasterClient || damagedBot == null || PhotonNetwork.CurrentRoom == null)
            return;

        bool gameStarted = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object startedValue) &&
                           startedValue is bool started &&
                           started;
        if (!gameStarted)
            return;

        if (damagedBot.Kind == EnemyBotKind.SpaceMine || damagedBot.Kind == EnemyBotKind.RescueShip)
            return;

        rescueShipSummonUnlockedThisRound = true;
        hasRescueShipSummonFocus = true;
        rescueShipSummonFocusPosition = damagedBot.transform.position;
        rescueShipSummonTargetViewId = damagedBot.photonView != null ? damagedBot.photonView.ViewID : -1;
    }

    void ResetRescueShipSummonState()
    {
        rescueShipSummonUnlockedThisRound = false;
        hasRescueShipSummonFocus = false;
        rescueShipSummonFocusPosition = Vector2.zero;
        rescueShipSummonTargetViewId = -1;
    }

    bool TryGetRescueShipSpawnFocus(out Vector2 focusPosition)
    {
        if (EnemyRescueShipBehavior.TryFindNearestDamagedAlly(
                hasRescueShipSummonFocus ? rescueShipSummonFocusPosition : Vector2.zero,
                null,
                out PlayerHealth damagedTarget) &&
            damagedTarget != null)
        {
            focusPosition = damagedTarget.transform.position;
            hasRescueShipSummonFocus = true;
            rescueShipSummonFocusPosition = focusPosition;
            PhotonView targetView = damagedTarget.GetComponent<PhotonView>();
            rescueShipSummonTargetViewId = targetView != null ? targetView.ViewID : -1;
            return true;
        }

        if (rescueShipSummonTargetViewId > 0)
        {
            PhotonView targetView = PhotonView.Find(rescueShipSummonTargetViewId);
            if (targetView != null)
            {
                focusPosition = targetView.transform.position;
                hasRescueShipSummonFocus = true;
                rescueShipSummonFocusPosition = focusPosition;
                return true;
            }
        }

        if (hasRescueShipSummonFocus)
        {
            focusPosition = rescueShipSummonFocusPosition;
            return true;
        }

        focusPosition = Vector2.zero;
        return false;
    }

    void SpawnEnemy(EnemyBotDefinition definition, int spawnOrdinal, bool forceOffscreen = false, bool hasEntryFocus = false, Vector2 entryFocusPosition = default)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        Vector2 spawn = forceOffscreen
            ? GetOffscreenSpawnPosition(definition, mapSize, hasEntryFocus, entryFocusPosition)
            : GetSafeSpawnPosition(definition, mapSize, spawnOrdinal);
        GameObject botObject = PhotonNetwork.Instantiate("Player", spawn, Quaternion.identity, 0, new object[] { definition.InstantiationMarker });
        if (botObject != null)
        {
            EnemyBot bot = botObject.GetComponent<EnemyBot>();
            if (bot == null)
                bot = botObject.AddComponent<EnemyBot>();

            bot.InitializeFromPhotonData();
            GameVisualTheme.RequestRuntimeRefresh();
            if (definition.Kind == EnemyBotKind.RescueShip)
            {
                StartCoroutine(PlayRescueShipIncomingAfterBootstrap(bot.photonView.ViewID, spawn));
            }
        }
    }

    System.Collections.IEnumerator PlayRescueShipIncomingAfterBootstrap(int viewId, Vector2 spawn)
    {
        yield return null;
        yield return null;

        PhotonView view = PhotonView.Find(viewId);
        if (view == null)
            yield break;

        EnemyBot bot = view.GetComponent<EnemyBot>();
        if (bot == null)
            yield break;

        view.RPC(nameof(EnemyBot.PlayRescueShipIncomingRpc), RpcTarget.All, spawn.x, spawn.y, 0f);
    }

    int GetSpawnedCount(EnemyBotKind kind)
    {
        return spawnedThisRound.TryGetValue(kind, out int count) ? count : 0;
    }

    int CountActiveNeutralBots(EnemyBotKind kind)
    {
        EnemyBot[] bots = FindObjectsByType<EnemyBot>(FindObjectsInactive.Exclude);
        int count = 0;
        for (int i = 0; i < bots.Length; i++)
        {
            EnemyBot bot = bots[i];
            if (bot == null || bot.Kind != kind || bot.IsPlayerPlacedMine || bot.IsSummonedDrone)
                continue;

            PlayerHealth health = bot.GetComponent<PlayerHealth>();
            if (health != null && health.IsWreck)
                continue;

            count++;
        }

        return count;
    }

    void DestroyExistingBots(EnemyBotKind kind)
    {
        EnemyBot[] bots = FindObjectsByType<EnemyBot>(FindObjectsInactive.Exclude);
        for (int i = 0; i < bots.Length; i++)
        {
            EnemyBot bot = bots[i];
            if (bot == null || bot.Kind != kind || bot.IsPlayerPlacedMine || bot.IsSummonedDrone)
                continue;

            PhotonView view = bot.GetComponent<PhotonView>();
            if (view != null && view.IsMine)
                PhotonNetwork.Destroy(bot.gameObject);
        }
    }

    Vector2 GetSafeSpawnPosition(EnemyBotDefinition definition, Vector2 mapSize, int spawnOrdinal)
    {
        Vector2[] candidates = BuildSpawnCandidates(definition, mapSize, spawnOrdinal);

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        EnemyBot[] bots = FindObjectsByType<EnemyBot>(FindObjectsInactive.Exclude);
        float bestScore = float.MinValue;
        Vector2 bestCandidate = candidates[0];

        for (int i = 0; i < candidates.Length; i++)
        {
            float nearestDistance = float.MaxValue;
            for (int j = 0; j < players.Length; j++)
            {
                PlayerHealth player = players[j];
                if (player == null || player.IsBotControlled || player.IsWreck)
                    continue;

                float distance = Vector2.Distance(candidates[i], player.transform.position);
                nearestDistance = Mathf.Min(nearestDistance, distance);
            }

            for (int j = 0; j < bots.Length; j++)
            {
                EnemyBot existingBot = bots[j];
                if (existingBot == null)
                    continue;

                float botDistance = Vector2.Distance(candidates[i], existingBot.transform.position);
                nearestDistance = Mathf.Min(nearestDistance, botDistance * 0.9f);
            }

            if (nearestDistance > bestScore)
            {
                bestScore = nearestDistance;
                bestCandidate = candidates[i];
            }
        }

        return bestCandidate;
    }

    Vector2 GetOffscreenSpawnPosition(EnemyBotDefinition definition, Vector2 mapSize, bool hasEntryFocus, Vector2 entryFocusPosition)
    {
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        bool isRescueShip = definition != null && definition.Kind == EnemyBotKind.RescueShip;
        float margin = isRescueShip
            ? Mathf.Max(1.2f, definition != null ? definition.TargetSize * 0.45f : 1.2f)
            : Mathf.Max(4.5f, definition != null ? definition.TargetSize * 1.85f : 4.5f);
        if (hasEntryFocus)
        {
            float clampedX = Mathf.Clamp(entryFocusPosition.x, -halfX + 1f, halfX - 1f);
            float clampedY = Mathf.Clamp(entryFocusPosition.y, -halfY + 1f, halfY - 1f);
            float topDistance = Mathf.Abs(halfY - clampedY);
            float bottomDistance = Mathf.Abs(-halfY - clampedY);
            float leftDistance = Mathf.Abs(-halfX - clampedX);
            float rightDistance = Mathf.Abs(halfX - clampedX);
            float nearestDistance = Mathf.Min(Mathf.Min(topDistance, bottomDistance), Mathf.Min(leftDistance, rightDistance));
            float lateralJitter = isRescueShip
                ? 0.18f
                : Mathf.Min(1.3f, Mathf.Max(0.35f, definition != null ? definition.TargetSize * 0.18f : 0.6f));

            if (Mathf.Approximately(nearestDistance, topDistance))
                return new Vector2(Mathf.Clamp(clampedX + Random.Range(-lateralJitter, lateralJitter), -halfX, halfX), halfY + margin);

            if (Mathf.Approximately(nearestDistance, bottomDistance))
                return new Vector2(Mathf.Clamp(clampedX + Random.Range(-lateralJitter, lateralJitter), -halfX, halfX), -halfY - margin);

            if (Mathf.Approximately(nearestDistance, leftDistance))
                return new Vector2(-halfX - margin, Mathf.Clamp(clampedY + Random.Range(-lateralJitter, lateralJitter), -halfY, halfY));

            return new Vector2(halfX + margin, Mathf.Clamp(clampedY + Random.Range(-lateralJitter, lateralJitter), -halfY, halfY));
        }

        int edge = Random.Range(0, 4);
        switch (edge)
        {
            case 0:
                return new Vector2(Random.Range(-halfX, halfX), halfY + margin);
            case 1:
                return new Vector2(Random.Range(-halfX, halfX), -halfY - margin);
            case 2:
                return new Vector2(-halfX - margin, Random.Range(-halfY, halfY));
            default:
                return new Vector2(halfX + margin, Random.Range(-halfY, halfY));
        }
    }

    Vector2[] BuildSpawnCandidates(EnemyBotDefinition definition, Vector2 mapSize, int spawnOrdinal)
    {
        const int candidateCount = 12;
        Vector2[] candidates = new Vector2[candidateCount];
        float baseRadiusFactor = definition.Movement != null && definition.Movement.SpawnPattern == EnemySpawnPattern.WideCorners ? 0.32f : 0.4f;
        float xRadius = mapSize.x * baseRadiusFactor;
        float yRadius = mapSize.y * baseRadiusFactor;
        float phaseOffset = (spawnOrdinal * 137.50776f + (int)definition.Kind * 23.5f) * Mathf.Deg2Rad;

        for (int i = 0; i < candidateCount; i++)
        {
            float angle = phaseOffset + ((Mathf.PI * 2f) / candidateCount) * i;
            float radialJitter = 0.88f + 0.08f * Mathf.Sin(angle * 3f + spawnOrdinal);
            candidates[i] = new Vector2(
                Mathf.Cos(angle) * xRadius * radialJitter,
                Mathf.Sin(angle) * yRadius * radialJitter);
        }

        return candidates;
    }
}
