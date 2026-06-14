using Photon.Pun;
using ExitGames.Client.Photon;
using UnityEngine;

public class EnemyBotManager : MonoBehaviour
{
    const float ScanInterval = 0.2f;
    const float RuntimeComponentScanInterval = 1f;
    const string SpawnStateStartTimeRoomKey = "enemyRuntime.spawnStateStartTime";
    const string SpawnedCountRoomKeyPrefix = "enemyRuntime.spawned.";

    static EnemyBotManager instance;
    static readonly Collider2D[] SpawnCandidateOverlapHits = new Collider2D[64];

    double lastHandledStartTime = double.MinValue;
    float nextScanTime;
    float nextRuntimeComponentScanTime;
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
        float now = Time.unscaledTime;
        if (now >= nextRuntimeComponentScanTime)
        {
            nextRuntimeComponentScanTime = now + RuntimeComponentScanInterval;
            EnsureRuntimeComponents();
        }

        if (now < nextScanTime)
            return;

        nextScanTime = now + ScanInterval;
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

            if (PlayerDeployableRuntime.IsInstantiationData(view.InstantiationData))
            {
                PlayerDeployableRuntime.EnsureAttached(view.gameObject);
                continue;
            }

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
            if (!SeedSpawnStateFromRoomProperties(currentStartTime))
                SeedSpawnStateFromActiveRound(currentStartTime);
            EnsureSpawnStateRoomStart(currentStartTime);
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
            SetSpawnedCount(definition.Kind, 0);
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
        int spawnedTotal = spawnedCount;
        bool spawnedAny = false;

        for (int i = spawnedCount; i < desiredCount; i++)
        {
            if (!SpawnEnemy(definition, i))
                break;

            spawnedTotal++;
            spawnedAny = true;
        }

        if (spawnedAny)
        {
            SetSpawnedCount(definition.Kind, spawnedTotal);
            GameVisualTheme.RequestRuntimeRefresh();
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
        int spawnedSuccessCount = 0;
        for (int i = 0; i < missingCount; i++)
        {
            if (!SpawnEnemy(definition, spawnBase + i))
                break;

            spawnedSuccessCount++;
        }

        SetSpawnedCount(definition.Kind, spawnBase + spawnedSuccessCount);
        if (spawnedSuccessCount > 0)
            GameVisualTheme.RequestRuntimeRefresh();
    }

    void HandleRescueShipSpawn(EnemyBotDefinition definition, double currentStartTime)
    {
        if (!RoomSettings.GetEnemyEnabled(definition.Kind))
        {
            DestroyExistingBots(definition.Kind);
            SetSpawnedCount(definition.Kind, 0);
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
        int spawnedTotal = spawnedCount;
        bool hasSpawnFocus = TryGetRescueShipSpawnFocus(out Vector2 spawnFocus);
        bool spawnedAny = false;

        for (int i = spawnedCount; i < desiredCount; i++)
        {
            if (!SpawnEnemy(definition, i, true, hasSpawnFocus, spawnFocus))
                break;

            spawnedTotal++;
            spawnedAny = true;
        }

        if (spawnedAny)
        {
            SetSpawnedCount(definition.Kind, spawnedTotal);
            GameVisualTheme.RequestRuntimeRefresh();
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
        int spawnedSuccessCount = 0;
        for (int i = 0; i < missingCount; i++)
        {
            if (!SpawnEnemy(definition, spawnBase + i, true, hasSpawnFocus, spawnFocus))
                break;

            spawnedSuccessCount++;
        }

        SetSpawnedCount(definition.Kind, spawnBase + spawnedSuccessCount);
        if (spawnedSuccessCount > 0)
            GameVisualTheme.RequestRuntimeRefresh();
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

    bool SpawnEnemy(EnemyBotDefinition definition, int spawnOrdinal, bool forceOffscreen = false, bool hasEntryFocus = false, Vector2 entryFocusPosition = default)
    {
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        Vector2 spawn;
        bool allowOffscreenEntry = forceOffscreen && !RoomSettings.AreToxicBordersEnabled();
        if (allowOffscreenEntry)
        {
            spawn = GetOffscreenSpawnPosition(definition, mapSize, hasEntryFocus, entryFocusPosition);
        }
        else if (!TryGetSafeSpawnPosition(definition, mapSize, spawnOrdinal, out spawn))
        {
            return false;
        }

        object[] instantiationData = BuildEnemyInstantiationData(definition, spawnOrdinal);
        GameObject botObject = PhotonNetwork.Instantiate("Player", spawn, Quaternion.identity, 0, instantiationData);
        if (botObject != null)
        {
            EnemyBot bot = botObject.GetComponent<EnemyBot>();
            if (bot == null)
                bot = botObject.AddComponent<EnemyBot>();

            bot.InitializeFromPhotonData();
            if (definition.Kind == EnemyBotKind.RescueShip)
            {
                StartCoroutine(PlayRescueShipIncomingAfterBootstrap(bot.photonView.ViewID, spawn));
            }
        }

        return botObject != null;
    }

    object[] BuildEnemyInstantiationData(EnemyBotDefinition definition, int spawnOrdinal)
    {
        if (definition != null && definition.Kind == EnemyBotKind.ContainerShip)
        {
            int variantCount = Mathf.Max(1, InventoryItemCatalog.BlueprintScrapContainerVariantCount);
            int variantIndex = Mathf.Abs((spawnOrdinal * 37) + Mathf.RoundToInt(Time.time * 1000f)) % variantCount;
            return new object[] { definition.InstantiationMarker, variantIndex };
        }

        return new object[] { definition != null ? definition.InstantiationMarker : string.Empty };
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

    void SetSpawnedCount(EnemyBotKind kind, int count)
    {
        int clamped = Mathf.Max(0, count);
        spawnedThisRound[kind] = clamped;

        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || lastHandledStartTime <= 0d)
            return;

        Hashtable props = new Hashtable
        {
            [SpawnStateStartTimeRoomKey] = lastHandledStartTime,
            [GetSpawnedCountRoomKey(kind)] = clamped
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    bool SeedSpawnStateFromRoomProperties(double currentStartTime)
    {
        if (PhotonNetwork.CurrentRoom == null || currentStartTime <= 0d)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(SpawnStateStartTimeRoomKey, out object startValue) ||
            !TryConvertToDouble(startValue, out double storedStartTime) ||
            Mathf.Abs((float)(storedStartTime - currentStartTime)) > 0.001f)
        {
            return false;
        }

        bool seededAny = false;
        for (int i = 0; i < EnemyBotCatalog.AllDefinitions.Count; i++)
        {
            EnemyBotDefinition definition = EnemyBotCatalog.AllDefinitions[i];
            if (definition == null)
                continue;

            string key = GetSpawnedCountRoomKey(definition.Kind);
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object countValue) &&
                TryConvertToInt(countValue, out int count))
            {
                spawnedThisRound[definition.Kind] = Mathf.Max(0, count);
                seededAny = true;
            }
        }

        return seededAny;
    }

    void EnsureSpawnStateRoomStart(double currentStartTime)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || currentStartTime <= 0d)
            return;

        bool hasMatchingStart = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(SpawnStateStartTimeRoomKey, out object startValue) &&
                                TryConvertToDouble(startValue, out double storedStartTime) &&
                                Mathf.Abs((float)(storedStartTime - currentStartTime)) <= 0.001f;
        if (hasMatchingStart)
            return;

        Hashtable props = new Hashtable
        {
            [SpawnStateStartTimeRoomKey] = currentStartTime
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    static string GetSpawnedCountRoomKey(EnemyBotKind kind)
    {
        return SpawnedCountRoomKeyPrefix + kind.ToString().ToLowerInvariant();
    }

    static bool TryConvertToInt(object value, out int result)
    {
        if (value is int intValue)
        {
            result = intValue;
            return true;
        }

        if (value is short shortValue)
        {
            result = shortValue;
            return true;
        }

        if (value is byte byteValue)
        {
            result = byteValue;
            return true;
        }

        if (value is long longValue)
        {
            result = longValue > int.MaxValue ? int.MaxValue : longValue < 0L ? 0 : (int)longValue;
            return true;
        }

        result = 0;
        return false;
    }

    static bool TryConvertToDouble(object value, out double result)
    {
        if (value is double doubleValue)
        {
            result = doubleValue;
            return true;
        }

        if (value is float floatValue)
        {
            result = floatValue;
            return true;
        }

        if (value is int intValue)
        {
            result = intValue;
            return true;
        }

        result = 0d;
        return false;
    }

    void SeedSpawnStateFromActiveRound(double currentStartTime)
    {
        if (currentStartTime <= 0d)
            return;

        double elapsed = PhotonNetwork.Time - currentStartTime;
        if (elapsed < 0.75d)
            return;

        for (int i = 0; i < EnemyBotCatalog.AllDefinitions.Count; i++)
        {
            EnemyBotDefinition definition = EnemyBotCatalog.AllDefinitions[i];
            if (definition == null || !RoomSettings.GetEnemyEnabled(definition.Kind))
                continue;

            int activeCount = CountActiveNeutralBots(definition.Kind);
            int spawnSecond = RoomSettings.GetEnemySpawnSecond(definition.Kind);
            bool initialSpawnWindowPassed = elapsed >= spawnSecond + 0.75d;
            int seededCount = activeCount;

            if (activeCount > 0 && definition.Kind != EnemyBotKind.RescueShip && initialSpawnWindowPassed)
                seededCount = Mathf.Max(seededCount, RoomSettings.GetEnemyCount(definition.Kind));

            if (seededCount > 0)
                SetSpawnedCount(definition.Kind, seededCount);

            int respawnInterval = RoomSettings.GetEnemyRespawnIntervalSeconds(definition.Kind);
            if (RoomSettings.GetEnemyRespawnEnabled(definition.Kind) &&
                respawnInterval > 0 &&
                elapsed >= respawnInterval &&
                elapsed >= spawnSecond)
            {
                lastHandledRespawnTick[definition.Kind] = Mathf.FloorToInt((float)(elapsed / respawnInterval));
            }
        }
    }

    int CountActiveNeutralBots(EnemyBotKind kind)
    {
        EnemyBot[] bots = FindObjectsByType<EnemyBot>(FindObjectsInactive.Exclude);
        int count = 0;
        for (int i = 0; i < bots.Length; i++)
        {
            EnemyBot bot = bots[i];
            if (bot == null || bot.Kind != kind || bot.IsOwnedMine || bot.IsSummonedDrone || bot.IsPirateBaseLaunchedFighter)
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
            if (bot == null || bot.Kind != kind || bot.IsOwnedMine || bot.IsSummonedDrone || bot.IsPirateBaseLaunchedFighter)
                continue;

            PhotonView view = bot.GetComponent<PhotonView>();
            if (view != null && view.IsMine)
                PhotonNetwork.Destroy(bot.gameObject);
        }
    }

    bool TryGetSafeSpawnPosition(EnemyBotDefinition definition, Vector2 mapSize, int spawnOrdinal, out Vector2 spawn)
    {
        Vector2[] candidates = BuildSpawnCandidates(definition, mapSize, spawnOrdinal);

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        EnemyBot[] bots = FindObjectsByType<EnemyBot>(FindObjectsInactive.Exclude);
        float bestScore = float.MinValue;
        float bestSafeScore = float.MinValue;
        Vector2 bestCandidate = candidates[0];
        Vector2 bestSafeCandidate = candidates[0];
        bool hasSafeCandidate = false;

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

            if (nearestDistance > bestSafeScore && IsSpawnCandidateSafe(definition, candidates[i], players, bots))
            {
                bestSafeScore = nearestDistance;
                bestSafeCandidate = candidates[i];
                hasSafeCandidate = true;
            }
        }

        if (hasSafeCandidate)
        {
            spawn = bestSafeCandidate;
            return true;
        }

        spawn = bestCandidate;
        return definition == null || definition.Kind != EnemyBotKind.SpaceMine;
    }

    bool IsSpawnCandidateSafe(EnemyBotDefinition definition, Vector2 candidate, PlayerHealth[] players, EnemyBot[] bots)
    {
        float targetSize = definition != null ? Mathf.Max(0.4f, definition.TargetSize) : 1f;
        float playerClearance = definition != null && definition.Kind == EnemyBotKind.SpaceMine && definition.Explosion != null
            ? definition.Explosion.TriggerRadius + 2.4f
            : Mathf.Max(3.2f, targetSize * 1.85f);

        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player == null || player.IsBotControlled || player.IsWreck || player.IsEvacuationAnimating)
                continue;

            if (Vector2.Distance(candidate, player.transform.position) < playerClearance)
                return false;
        }

        float botClearance = Mathf.Max(1.4f, targetSize * 1.2f);
        for (int i = 0; i < bots.Length; i++)
        {
            EnemyBot existingBot = bots[i];
            if (existingBot == null)
                continue;

            if (Vector2.Distance(candidate, existingBot.transform.position) < botClearance)
                return false;
        }

        float overlapRadius = Mathf.Max(0.45f, targetSize * 0.34f);
        int overlapCount = Physics2D.OverlapCircle(candidate, overlapRadius, CreatePhysicsQueryFilter(), SpawnCandidateOverlapHits);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D hit = SpawnCandidateOverlapHits[i];
            if (hit == null || hit.isTrigger)
                continue;

            PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
            if (player != null && !player.IsBotControlled && !player.IsWreck)
                return false;
        }

        return true;
    }

    static ContactFilter2D CreatePhysicsQueryFilter()
    {
        return new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };
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
