using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public class ObstacleSpawner : MonoBehaviourPunCallbacks
{
    const string EmptyLayoutSentinel = "__empty__";
    const string GameStartedKey = "gameStarted";
    const string ObstacleDensityKey = "obstacleDensity";
    const string MapSeedKey = "mapSeed";
    const string ObstacleLayoutKey = "obstacleLayout";
    const string ExtractionLayoutKey = "extractionLayout";
    const float BaseMinDistanceFromExtraction = 3.5f;
    const float ChildSpawnPadding = 0.18f;
    const float ChildSpawnGrowth = 0.18f;

    static ObstacleSpawner instance;

    int mapSeed;
    int dynamicObstacleSequence;
    public GameObject obstaclePrefab;
    public int obstacleCount = 10;

    public float mapSizeX = 25f;
    public float mapSizeY = 25f;

    public float margin = 2f;
    public float checkRadius = 1.5f;
    public float minObstacleDistance = 3.25f;

    bool layoutApplied = false;
    int ResolvedObstacleCount => Mathf.Max(0, Mathf.RoundToInt(obstacleCount * GetDensityMultiplier() * RoomSettings.GetMapAreaMultiplier()));

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        mapSizeX = mapSize.x;
        mapSizeY = mapSize.y;

        Debug.Log("ObstacleSpawner Start");
        StartCoroutine(InitializeWhenRoundStarts());
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!PhotonNetwork.IsMasterClient || !IsRoundStarted())
            return;

        StartCoroutine(SendRuntimeStateToPlayerNextFrame(newPlayer.ActorNumber));
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        RebuildDynamicObstacleSequenceFromScene();
    }

    IEnumerator SendRuntimeStateToPlayerNextFrame(int actorNumber)
    {
        yield return null;

        if (this == null || !PhotonNetwork.IsMasterClient || !IsRoundStarted())
            yield break;

        BroadcastRuntimeStateToPlayer(actorNumber);
    }

    IEnumerator InitializeWhenRoundStarts()
    {
        while (!PhotonNetwork.InRoom)
            yield return null;

        while (!IsRoundStarted())
            yield return null;

        while (!HasExtractionLayout())
            yield return null;

        if (layoutApplied)
            yield break;

        if (PhotonNetwork.IsMasterClient)
        {
            mapSeed = Random.Range(0, 100000);
            string layout = BuildObstacleLayout(mapSeed);

            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
            props[MapSeedKey] = mapSeed;
            props[ObstacleLayoutKey] = layout;
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);

            ApplyLayoutIfNeeded(layout);
        }
        else
        {
            yield return StartCoroutine(WaitForFreshLayout());
        }
    }

    IEnumerator WaitForFreshLayout()
    {
        while (!layoutApplied)
        {
            if (PhotonNetwork.CurrentRoom != null &&
                PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ObstacleLayoutKey, out object layoutValue) &&
                layoutValue is string layout &&
                !string.IsNullOrWhiteSpace(layout))
            {
                if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MapSeedKey, out object seedValue) && seedValue is int seed)
                    mapSeed = seed;

                ApplyLayoutIfNeeded(layout);
            }

            if (!layoutApplied)
                yield return null;
        }
    }

    void ApplyLayoutIfNeeded(string layout)
    {
        if (layoutApplied || string.IsNullOrWhiteSpace(layout))
            return;

        ApplyObstacleLayout(layout);
        layoutApplied = true;
    }

    string BuildObstacleLayout(int seed)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        mapSizeX = mapSize.x;
        mapSizeY = mapSize.y;

        Random.State previousState = Random.state;
        Random.InitState(seed);

        List<Vector2> positions = new List<Vector2>();
        List<Vector2> extractionPositions = ParseExtractionPositions();
        int spawned = 0;
        int attempts = 0;
        int targetCount = ResolvedObstacleCount;
        float scaledCheckRadius = GetScaledCheckRadius();
        float scaledObstacleDistance = GetScaledObstacleDistance();

        while (spawned < targetCount && attempts < 800)
        {
            attempts++;

            float x = Random.Range(-mapSizeX / 2 + margin, mapSizeX / 2 - margin);
            float y = Random.Range(-mapSizeY / 2 + margin, mapSizeY / 2 - margin);
            Vector2 pos = new Vector2(x, y);
            Collider2D hit = Physics2D.OverlapCircle(pos, scaledCheckRadius);

            if (hit == null &&
                IsFarEnoughFromOtherObstacles(pos, positions, scaledObstacleDistance) &&
                IsFarEnoughFromExtractionZones(pos, extractionPositions))
            {
                positions.Add(pos);
                spawned++;
            }
        }

        Random.state = previousState;

        if (positions.Count == 0)
            return EmptyLayoutSentinel;

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < positions.Count; i++)
        {
            if (i > 0)
                builder.Append(';');

            builder.Append(positions[i].x.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(positions[i].y.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    void ApplyObstacleLayout(string layout)
    {
        if (string.Equals(layout, EmptyLayoutSentinel, System.StringComparison.Ordinal))
            return;

        string[] entries = layout.Split(';');
        int obstacleIndex = 0;

        foreach (string entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            string[] parts = entry.Split(',');
            if (parts.Length != 2)
                continue;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
                continue;

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                continue;

            ObstacleChunk.RuntimeState initialState = CreateInitialRuntimeState(obstacleIndex, new Vector2(x, y));
            CreateOrUpdateObstacleFromState(initialState, PhotonNetwork.IsMasterClient);
            obstacleIndex++;
        }

        dynamicObstacleSequence = Mathf.Max(dynamicObstacleSequence, obstacleIndex);
        RebuildDynamicObstacleSequenceFromScene();
    }

    ObstacleChunk.RuntimeState CreateInitialRuntimeState(int obstacleIndex, Vector2 position)
    {
        string stableId = "obstacle_" + obstacleIndex;
        float sizeFactor = ObstacleChunk.ComputeStableSizeFactor(stableId);
        int spriteVariantIndex = ObstacleChunk.ComputeStableSpriteVariantIndex(stableId, GameVisualTheme.GetObstacleSpriteVariantCount());
        int hp = RoomSettings.GetObstacleHp();
        return new ObstacleChunk.RuntimeState(stableId, position, Vector2.zero, 0f, 0f, sizeFactor, hp, hp, 0, spriteVariantIndex);
    }

    void CreateOrUpdateObstacleFromState(ObstacleChunk.RuntimeState state, bool authorityState)
    {
        if (string.IsNullOrWhiteSpace(state.StableId))
            return;

        ObstacleChunk chunk = ObstacleChunk.Find(state.StableId);
        if (chunk == null)
        {
            if (obstaclePrefab == null)
                return;

            GameObject obstacle = Instantiate(obstaclePrefab, new Vector3(state.Position.x, state.Position.y, 0f), Quaternion.identity);
            obstacle.name = "Obstacle_" + state.StableId;

            MovingSpaceObject movingObject = obstacle.GetComponent<MovingSpaceObject>();
            if (movingObject == null)
                movingObject = obstacle.AddComponent<MovingSpaceObject>();

            movingObject.Configure(state.StableId, MovingSpaceObject.SpaceObjectType.Obstacle);

            chunk = obstacle.GetComponent<ObstacleChunk>();
            if (chunk == null)
                chunk = obstacle.AddComponent<ObstacleChunk>();
        }

        chunk.ApplyRuntimeState(state, authorityState);
        GameVisualTheme.ApplyObstacleVisual(chunk.gameObject);
    }

    public static void ApplyObstacleDamage(string stableId, int damage)
    {
        if (instance == null)
            return;

        instance.ApplyObstacleDamageInternal(stableId, damage);
    }

    void ApplyObstacleDamageInternal(string stableId, int damage)
    {
        if (!PhotonNetwork.IsMasterClient && PhotonNetwork.IsConnected)
            return;

        ObstacleChunk chunk = ObstacleChunk.Find(stableId);
        if (chunk == null)
            return;

        if (!chunk.ApplyDamageAuthority(damage))
            return;

        ResolveDestroyedObstacle(chunk);
    }

    void ResolveDestroyedObstacle(ObstacleChunk source)
    {
        if (source == null)
            return;

        string sourceStableId = source.StableId;
        ObstacleChunk.RuntimeState sourceState = source.CaptureRuntimeState();
        bool createdChildren = TryCreateSplitChildren(
            source,
            sourceState,
            out ObstacleChunk.RuntimeState childAState,
            out ObstacleChunk.RuntimeState childBState);

        if (source != null && source.gameObject != null)
            DestroyObstacleImmediately(source.gameObject);

        if (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient)
        {
            if (createdChildren)
            {
                SpaceObjectMotionSync.BroadcastObstacleSplit(
                    sourceStableId,
                    SerializeRuntimeState(childAState),
                    SerializeRuntimeState(childBState));
            }
            else
            {
                SpaceObjectMotionSync.BroadcastObstacleSplit(sourceStableId, string.Empty, string.Empty);
            }
        }
        else if (!PhotonNetwork.IsConnected)
        {
            string snapshot = CaptureRuntimeStateSnapshotInternal();
            ApplyRuntimeStateSnapshotInternal(snapshot);
        }
    }

    bool TryCreateSplitChildren(
        ObstacleChunk source,
        ObstacleChunk.RuntimeState sourceState,
        out ObstacleChunk.RuntimeState childA,
        out ObstacleChunk.RuntimeState childB)
    {
        childA = default;
        childB = default;

        if (source == null || !source.CanSplit)
            return false;

        float childSizeFactor = source.SizeFactor * 0.5f;
        if (childSizeFactor < ObstacleChunk.MinimumSizeFactor)
            return false;

        int childHp = Mathf.Max(1, Mathf.RoundToInt(source.MaxHealth * 0.5f));
        if (childHp <= 0)
            return false;

        if (!TryResolveChildPositions(source, sourceState, out Vector2 childPosA, out Vector2 childPosB))
            return false;

        BuildChildMotion(sourceState, out Vector2 velocityA, out Vector2 velocityB, out float angularVelocityA, out float angularVelocityB);

        childA = new ObstacleChunk.RuntimeState(
            AllocateDynamicObstacleId(),
            childPosA,
            velocityA,
            sourceState.Rotation,
            angularVelocityA,
            childSizeFactor,
            childHp,
            childHp,
            sourceState.SplitCount + 1,
            sourceState.SpriteVariantIndex);

        childB = new ObstacleChunk.RuntimeState(
            AllocateDynamicObstacleId(),
            childPosB,
            velocityB,
            sourceState.Rotation,
            angularVelocityB,
            childSizeFactor,
            childHp,
            childHp,
            sourceState.SplitCount + 1,
            sourceState.SpriteVariantIndex);

        CreateOrUpdateObstacleFromState(childA, true);
        CreateOrUpdateObstacleFromState(childB, true);
        return true;
    }

    void DestroyObstacleImmediately(GameObject obstacleObject)
    {
        if (obstacleObject == null)
            return;

        Collider2D[] colliders = obstacleObject.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        obstacleObject.SetActive(false);
        Destroy(obstacleObject);
    }

    void BuildChildMotion(
        ObstacleChunk.RuntimeState sourceState,
        out Vector2 velocityA,
        out Vector2 velocityB,
        out float angularVelocityA,
        out float angularVelocityB)
    {
        velocityA = Vector2.zero;
        velocityB = Vector2.zero;
        angularVelocityA = 0f;
        angularVelocityB = 0f;

        if (!RoomSettings.ShouldMovingObjectsRotate())
            return;

        float spinBase = Mathf.Max(14f, Mathf.Abs(sourceState.AngularVelocity));
        angularVelocityA = spinBase * Random.Range(0.75f, 1.1f) * (Random.value < 0.5f ? -1f : 1f);
        angularVelocityB = -angularVelocityA;

        if (!RoomSettings.ShouldMovingObjectsTranslate() || sourceState.Velocity.sqrMagnitude < 0.0001f)
            return;

        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Random.value < 0.5f ? Vector2.right : Vector2.up;

        float speed = Mathf.Max(0.35f, sourceState.Velocity.magnitude) * Random.Range(0.85f, 1.15f);
        velocityA = direction * speed;
        velocityB = -direction * speed;
    }

    bool TryResolveChildPositions(ObstacleChunk source, ObstacleChunk.RuntimeState sourceState, out Vector2 childPosA, out Vector2 childPosB)
    {
        childPosA = sourceState.Position;
        childPosB = sourceState.Position;

        float childRadius = Mathf.Max(0.28f, source.GetApproximateRadius() * 0.5f);
        float separation = childRadius + ChildSpawnPadding;
        float randomStartAngle = Random.Range(0f, 360f);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            float angle = randomStartAngle + attempt * 18f;
            Vector2 axis = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)).normalized;
            Vector2 candidateA = sourceState.Position + axis * separation;
            Vector2 candidateB = sourceState.Position - axis * separation;

            if (IsSpawnPositionClear(candidateA, childRadius, source) &&
                IsSpawnPositionClear(candidateB, childRadius, source) &&
                Vector2.Distance(candidateA, candidateB) >= childRadius * 2f)
            {
                childPosA = candidateA;
                childPosB = candidateB;
                return true;
            }

            separation += ChildSpawnGrowth;
        }

        return false;
    }

    bool IsSpawnPositionClear(Vector2 candidate, float radius, ObstacleChunk source)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        if (candidate.x > halfX - radius || candidate.x < -halfX + radius || candidate.y > halfY - radius || candidate.y < -halfY + radius)
            return false;

        Collider2D[] hits = Physics2D.OverlapCircleAll(candidate, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || !hit.enabled)
                continue;

            if (hit.GetComponentInParent<ExtractionZone>() != null || hit.GetComponentInParent<Bullet>() != null)
                continue;

            ObstacleChunk otherChunk = hit.GetComponentInParent<ObstacleChunk>();
            if (otherChunk != null && otherChunk == source)
                continue;

            if (hit.isTrigger)
                continue;

            return false;
        }

        return true;
    }

    string AllocateDynamicObstacleId()
    {
        return "obstacle_dynamic_" + dynamicObstacleSequence++;
    }

    public static string CaptureRuntimeStateSnapshot()
    {
        return instance != null ? instance.CaptureRuntimeStateSnapshotInternal() : EmptyLayoutSentinel;
    }

    string CaptureRuntimeStateSnapshotInternal()
    {
        ObstacleChunk[] chunks = FindObjectsByType<ObstacleChunk>(FindObjectsInactive.Exclude);
        if (chunks == null || chunks.Length == 0)
            return EmptyLayoutSentinel;

        System.Array.Sort(chunks, (left, right) => string.CompareOrdinal(left != null ? left.StableId : string.Empty, right != null ? right.StableId : string.Empty));

        StringBuilder builder = new StringBuilder();
        bool wroteEntry = false;
        for (int i = 0; i < chunks.Length; i++)
        {
            ObstacleChunk chunk = chunks[i];
            if (chunk == null || string.IsNullOrWhiteSpace(chunk.StableId))
                continue;

            if (chunk.CurrentHealth <= 0)
                continue;

            ObstacleChunk.RuntimeState state = chunk.CaptureRuntimeState();
            if (wroteEntry)
                builder.Append(';');

            wroteEntry = true;
            builder.Append(SerializeRuntimeState(state));
        }

        return wroteEntry ? builder.ToString() : EmptyLayoutSentinel;
    }

    public static void ApplyRuntimeStateSnapshot(string serializedState)
    {
        if (instance == null)
            return;

        instance.ApplyRuntimeStateSnapshotInternal(serializedState);
    }

    public static void ApplyObstacleSplitDelta(string sourceStableId, string childAState, string childBState)
    {
        if (instance == null)
            return;

        instance.ApplyObstacleSplitDeltaInternal(sourceStableId, childAState, childBState);
    }

    void ApplyObstacleSplitDeltaInternal(string sourceStableId, string childAState, string childBState)
    {
        if (!string.IsNullOrWhiteSpace(sourceStableId))
        {
            ObstacleChunk source = ObstacleChunk.Find(sourceStableId);
            if (source != null && source.gameObject != null)
                DestroyObstacleImmediately(source.gameObject);
        }

        ApplySingleRuntimeState(childAState, PhotonNetwork.IsMasterClient);
        ApplySingleRuntimeState(childBState, PhotonNetwork.IsMasterClient);
        RebuildDynamicObstacleSequenceFromScene();
        layoutApplied = true;
    }

    void ApplySingleRuntimeState(string serializedState, bool authorityState)
    {
        if (string.IsNullOrWhiteSpace(serializedState))
            return;

        if (TryParseRuntimeState(serializedState, out ObstacleChunk.RuntimeState state) && state.CurrentHealth > 0)
            CreateOrUpdateObstacleFromState(state, authorityState);
    }

    void ApplyRuntimeStateSnapshotInternal(string serializedState)
    {
        Dictionary<string, ObstacleChunk.RuntimeState> targetStates = ParseRuntimeStateSnapshot(serializedState);
        ObstacleChunk[] existingChunks = FindObjectsByType<ObstacleChunk>(FindObjectsInactive.Exclude);

        for (int i = 0; i < existingChunks.Length; i++)
        {
            ObstacleChunk existing = existingChunks[i];
            if (existing == null || string.IsNullOrWhiteSpace(existing.StableId))
                continue;

            if (!targetStates.ContainsKey(existing.StableId))
                Destroy(existing.gameObject);
        }

        foreach (KeyValuePair<string, ObstacleChunk.RuntimeState> entry in targetStates)
        {
            if (entry.Value.CurrentHealth > 0)
                CreateOrUpdateObstacleFromState(entry.Value, PhotonNetwork.IsMasterClient);
        }

        RebuildDynamicObstacleSequenceFromScene();
        layoutApplied = true;
    }

    void RebuildDynamicObstacleSequenceFromScene()
    {
        const string DynamicPrefix = "obstacle_dynamic_";

        ObstacleChunk[] chunks = FindObjectsByType<ObstacleChunk>(FindObjectsInactive.Exclude);
        int nextSequence = dynamicObstacleSequence;
        for (int i = 0; i < chunks.Length; i++)
        {
            ObstacleChunk chunk = chunks[i];
            if (chunk == null || string.IsNullOrWhiteSpace(chunk.StableId) || !chunk.StableId.StartsWith(DynamicPrefix, System.StringComparison.Ordinal))
                continue;

            string suffix = chunk.StableId.Substring(DynamicPrefix.Length);
            if (!int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sequenceValue))
                continue;

            nextSequence = Mathf.Max(nextSequence, sequenceValue + 1);
        }

        dynamicObstacleSequence = Mathf.Max(nextSequence, 0);
    }

    Dictionary<string, ObstacleChunk.RuntimeState> ParseRuntimeStateSnapshot(string serializedState)
    {
        Dictionary<string, ObstacleChunk.RuntimeState> parsed = new Dictionary<string, ObstacleChunk.RuntimeState>();
        if (string.IsNullOrWhiteSpace(serializedState) || string.Equals(serializedState, EmptyLayoutSentinel, System.StringComparison.Ordinal))
            return parsed;

        string[] entries = serializedState.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            if (TryParseRuntimeState(entry, out ObstacleChunk.RuntimeState state) && state.CurrentHealth > 0)
                parsed[state.StableId] = state;
        }

        return parsed;
    }

    static string SerializeRuntimeState(ObstacleChunk.RuntimeState state)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(state.StableId);
        builder.Append('|');
        builder.Append(state.Position.x.ToString(CultureInfo.InvariantCulture));
        builder.Append('|');
        builder.Append(state.Position.y.ToString(CultureInfo.InvariantCulture));
        builder.Append('|');
        builder.Append(state.Velocity.x.ToString(CultureInfo.InvariantCulture));
        builder.Append('|');
        builder.Append(state.Velocity.y.ToString(CultureInfo.InvariantCulture));
        builder.Append('|');
        builder.Append(state.Rotation.ToString(CultureInfo.InvariantCulture));
        builder.Append('|');
        builder.Append(state.AngularVelocity.ToString(CultureInfo.InvariantCulture));
        builder.Append('|');
        builder.Append(state.SizeFactor.ToString(CultureInfo.InvariantCulture));
        builder.Append('|');
        builder.Append(state.MaxHealth.ToString(CultureInfo.InvariantCulture));
        builder.Append('|');
        builder.Append(state.CurrentHealth.ToString(CultureInfo.InvariantCulture));
        builder.Append('|');
        builder.Append(state.SplitCount.ToString(CultureInfo.InvariantCulture));
        builder.Append('|');
        builder.Append(state.SpriteVariantIndex.ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    static bool TryParseRuntimeState(string serializedState, out ObstacleChunk.RuntimeState state)
    {
        state = default;
        if (string.IsNullOrWhiteSpace(serializedState))
            return false;

        string[] parts = serializedState.Split('|');
        if (parts.Length != 10 && parts.Length != 12)
            return false;

        string stableId = parts[0];
        if (string.IsNullOrWhiteSpace(stableId))
            return false;

        if (!TryParseFloat(parts[1], out float posX) ||
            !TryParseFloat(parts[2], out float posY) ||
            !TryParseFloat(parts[3], out float velX) ||
            !TryParseFloat(parts[4], out float velY) ||
            !TryParseFloat(parts[5], out float rotation) ||
            !TryParseFloat(parts[6], out float angularVelocity) ||
            !TryParseFloat(parts[7], out float sizeFactor) ||
            !int.TryParse(parts[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out int maxHealth) ||
            !int.TryParse(parts[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out int currentHealth))
        {
            return false;
        }

        int splitCount = 0;
        int spriteVariantIndex = ObstacleChunk.ComputeStableSpriteVariantIndex(stableId, GameVisualTheme.GetObstacleSpriteVariantCount());
        if (parts.Length >= 12)
        {
            if (!int.TryParse(parts[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out splitCount) ||
                !int.TryParse(parts[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out spriteVariantIndex))
            {
                return false;
            }
        }

        state = new ObstacleChunk.RuntimeState(
            stableId,
            new Vector2(posX, posY),
            new Vector2(velX, velY),
            rotation,
            angularVelocity,
            sizeFactor,
            maxHealth,
            currentHealth,
            splitCount,
            spriteVariantIndex);
        return true;
    }

    static bool TryParseFloat(string raw, out float value)
    {
        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public static void BroadcastRuntimeStateToPlayer(int actorNumber)
    {
        if (instance == null)
            return;

        if (!PhotonNetwork.IsConnected || !PhotonNetwork.IsMasterClient)
            return;

        string snapshot = instance.CaptureRuntimeStateSnapshotInternal();
        SpaceObjectMotionSync.BroadcastObstacleState(snapshot, new[] { actorNumber });
    }

    public static void ResetForSessionTransition()
    {
        if (instance == null)
            return;

        instance.ResetLocalRuntimeState();
    }

    void ResetLocalRuntimeState()
    {
        StopAllCoroutines();
        layoutApplied = false;
        mapSeed = 0;
        dynamicObstacleSequence = 0;

        ObstacleChunk[] chunks = FindObjectsByType<ObstacleChunk>(FindObjectsInactive.Exclude);
        for (int i = 0; i < chunks.Length; i++)
        {
            ObstacleChunk chunk = chunks[i];
            if (chunk != null && chunk.gameObject != null)
                Destroy(chunk.gameObject);
        }

        StartCoroutine(InitializeWhenRoundStarts());
    }

    void BroadcastCurrentRuntimeStateToOthers()
    {
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.IsMasterClient)
            return;

        string snapshot = CaptureRuntimeStateSnapshotInternal();
        SpaceObjectMotionSync.BroadcastObstacleState(snapshot);
    }

    bool IsFarEnoughFromOtherObstacles(Vector2 candidate, List<Vector2> positions, float minDistance)
    {
        for (int i = 0; i < positions.Count; i++)
        {
            if (Vector2.Distance(candidate, positions[i]) < minDistance)
                return false;
        }

        return true;
    }

    bool IsFarEnoughFromExtractionZones(Vector2 candidate, List<Vector2> extractionPositions)
    {
        float minDistanceFromExtraction = BaseMinDistanceFromExtraction * Mathf.Clamp(RoomSettings.GetObstacleSizeMultiplier(), 0.75f, 5f);
        for (int i = 0; i < extractionPositions.Count; i++)
        {
            if (Vector2.Distance(candidate, extractionPositions[i]) < minDistanceFromExtraction)
                return false;
        }

        return true;
    }

    List<Vector2> ParseExtractionPositions()
    {
        List<Vector2> positions = new List<Vector2>();

        if (PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ExtractionLayoutKey, out object value) ||
            value is not string layout ||
            string.IsNullOrWhiteSpace(layout))
        {
            return positions;
        }

        string[] entries = layout.Split(';');
        foreach (string entry in entries)
        {
            string[] parts = entry.Split(',');
            if (parts.Length != 2)
                continue;

            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                positions.Add(new Vector2(x, y));
            }
        }

        return positions;
    }

    float GetDensityMultiplier()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ObstacleDensityKey, out object value) &&
            value is string density)
        {
            switch (density)
            {
                case "none": return 0f;
                case "low": return 0.5f;
                case "high": return 2f;
                default: return 1f;
            }
        }

        return 1f;
    }

    float GetScaledCheckRadius()
    {
        return checkRadius * Mathf.Clamp(RoomSettings.GetObstacleSizeMultiplier(), 0.75f, 5f);
    }

    float GetScaledObstacleDistance()
    {
        return minObstacleDistance * Mathf.Clamp(RoomSettings.GetObstacleSizeMultiplier(), 0.75f, 5f);
    }

    bool IsRoundStarted()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(GameStartedKey, out object value) && value is bool started)
        {
            return started;
        }

        return false;
    }

    bool HasExtractionLayout()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ExtractionLayoutKey, out object value) &&
               value is string layout &&
               !string.IsNullOrWhiteSpace(layout);
    }
}
