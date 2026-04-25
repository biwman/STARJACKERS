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

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("EnemyBotManager");
        instance = root.AddComponent<EnemyBotManager>();
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

    void SpawnEnemy(EnemyBotDefinition definition, int spawnOrdinal)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        Vector2 spawn = GetSafeSpawnPosition(definition, mapSize, spawnOrdinal);
        GameObject botObject = PhotonNetwork.Instantiate("Player", spawn, Quaternion.identity, 0, new object[] { definition.InstantiationMarker });
        if (botObject != null)
        {
            EnemyBot bot = botObject.GetComponent<EnemyBot>();
            if (bot == null)
                bot = botObject.AddComponent<EnemyBot>();

            bot.InitializeFromPhotonData();
        }
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
