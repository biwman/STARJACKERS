using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class GravityWellPhysicsField : MonoBehaviourPunCallbacks, IOnEventCallback
{
    const float CacheRefreshInterval = 0.8f;
    const float FieldRadiusScale = 0.42f;
    const float InnerDeadRadius = 1.7f;
    const float ObstacleInnerOrbitRadius = 5.5f;
    const float PullAcceleration = 0.82f;
    const float TangentialAcceleration = 0.48f;
    const float MaxFieldAddedSpeed = 4.1f;
    const float ObstacleFieldScale = 2.2f;
    const float ObstacleTangentialScale = 0.75f;
    const float ObstacleInnerRepelScale = 1.35f;
    const float ObstacleMaxFieldSpeed = 1.35f;
    const float ShipDriftAcceleration = 0.22f;
    const float ShipTangentialAcceleration = 0.12f;
    const float ShipOutwardResistance = 0.34f;
    const float AstronautScale = 0.42f;
    const float WreckScale = 1.05f;
    const float TreasureScale = 1.55f;
    const int MaxObstacleBodiesPerStep = 10;
    const byte ConsumptionEventCode = 86;
    const float ConsumptionIntervalSeconds = 10f;
    const float ConsumptionRadius = 6.3f;
    const float ConsumptionAnimationDuration = 0.9f;

    static GravityWellPhysicsField instance;

    enum GravityFieldBodyRole
    {
        Standard,
        Obstacle
    }

    enum ConsumptionTargetKind
    {
        PhotonView = 1,
        Obstacle = 2
    }

    struct CachedFieldBody
    {
        public Rigidbody2D Body;
        public GravityFieldBodyRole Role;
        public float Scale;
    }

    struct ConsumptionCandidate
    {
        public ConsumptionTargetKind Kind;
        public int ViewId;
        public string StableId;
    }

    CachedFieldBody[] cachedBodies = System.Array.Empty<CachedFieldBody>();
    readonly List<ConsumptionCandidate> consumptionCandidates = new List<ConsumptionCandidate>(64);
    readonly HashSet<int> consumptionViewIds = new HashSet<int>();
    float nextCacheRefresh;
    float nextConsumptionTime;
    int cachedObstacleCount;
    int nextObstacleIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("GravityWellPhysicsField");
        instance = root.AddComponent<GravityWellPhysicsField>();
        DontDestroyOnLoad(root);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void FixedUpdate()
    {
        if (!ShouldRun())
        {
            if (cachedBodies.Length > 0)
                cachedBodies = System.Array.Empty<CachedFieldBody>();
            cachedObstacleCount = 0;
            nextObstacleIndex = 0;
            nextConsumptionTime = 0f;
            return;
        }

        if (Time.time >= nextCacheRefresh)
        {
            RebuildBodyCache();
            nextCacheRefresh = Time.time + CacheRefreshInterval;
        }

        Vector2 mapSize = RoomSettings.GetGameplayMapDimensions();
        float fieldRadius = Mathf.Max(10f, Mathf.Min(mapSize.x, mapSize.y) * FieldRadiusScale);
        for (int i = 0; i < cachedBodies.Length; i++)
        {
            if (cachedBodies[i].Role != GravityFieldBodyRole.Obstacle)
                ApplyField(cachedBodies[i], fieldRadius);
        }

        ApplyObstacleBatch(fieldRadius);
        TickConsumption();
    }

    bool ShouldRun()
    {
        return PhotonNetwork.InRoom &&
               RoomSettings.GetSessionState() == RoomSettings.SessionStateInPlay &&
               RoomSettings.IsGravityWellPhysicsEnabled();
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent == null || photonEvent.Code != ConsumptionEventCode)
            return;

        object[] payload = photonEvent.CustomData as object[];
        if (payload == null || payload.Length < 4)
            return;

        ConsumptionTargetKind kind = (ConsumptionTargetKind)ConvertToInt(payload[0]);
        int viewId = ConvertToInt(payload[1]);
        string stableId = payload[2] as string;
        float duration = Mathf.Max(0.05f, ConvertToFloat(payload[3]));
        PlayConsumptionVisual(kind, viewId, stableId, duration);
    }

    void TickConsumption()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (nextConsumptionTime <= 0f)
        {
            nextConsumptionTime = Time.time + ConsumptionIntervalSeconds;
            return;
        }

        if (Time.time < nextConsumptionTime)
            return;

        nextConsumptionTime = Time.time + ConsumptionIntervalSeconds;
        if (!TryPickConsumptionCandidate(out ConsumptionCandidate candidate))
            return;

        StartConsumption(candidate);
    }

    void StartConsumption(ConsumptionCandidate candidate)
    {
        object[] payload =
        {
            (int)candidate.Kind,
            candidate.ViewId,
            candidate.StableId ?? string.Empty,
            ConsumptionAnimationDuration
        };

        PlayConsumptionVisual(candidate.Kind, candidate.ViewId, candidate.StableId, ConsumptionAnimationDuration);
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.RaiseEvent(
                ConsumptionEventCode,
                payload,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                SendOptions.SendReliable);
        }

        StartCoroutine(ConsumeAfterAnimation(candidate));
    }

    IEnumerator ConsumeAfterAnimation(ConsumptionCandidate candidate)
    {
        yield return new WaitForSeconds(ConsumptionAnimationDuration);

        if (!PhotonNetwork.IsMasterClient)
            yield break;

        if (candidate.Kind == ConsumptionTargetKind.Obstacle)
        {
            ObstacleSpawner.ConsumeObstacle(candidate.StableId);
            yield break;
        }

        PhotonView view = candidate.ViewId > 0 ? PhotonView.Find(candidate.ViewId) : null;
        if (view == null)
            yield break;

        PlayerHealth health = view.GetComponent<PlayerHealth>();
        if (health != null && !health.IsWreck && !health.IsEvacuationAnimating && health.CurrentHP > 0)
        {
            Vector3 position = view.transform.position;
            health.ConsumeByGravityWell(position.x, position.y);
            yield break;
        }

        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(view.gameObject);
        else
            Destroy(view.gameObject);
    }

    bool TryPickConsumptionCandidate(out ConsumptionCandidate candidate)
    {
        candidate = default;
        consumptionCandidates.Clear();
        consumptionViewIds.Clear();

        float radiusSqr = ConsumptionRadius * ConsumptionRadius;
        AddHealthConsumptionCandidates(radiusSqr);
        AddPhotonConsumptionCandidates<Treasure>(radiusSqr);
        AddPhotonConsumptionCandidates<DroppedCargoCrate>(radiusSqr);
        AddPhotonConsumptionCandidates<ShipWreck>(radiusSqr);
        AddObstacleConsumptionCandidates(radiusSqr);

        if (consumptionCandidates.Count == 0)
            return false;

        candidate = consumptionCandidates[Random.Range(0, consumptionCandidates.Count)];
        return true;
    }

    void AddHealthConsumptionCandidates(float radiusSqr)
    {
        PlayerHealth[] healths = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth health = healths[i];
            if (health == null ||
                health.photonView == null ||
                health.IsWreck ||
                health.IsEvacuationAnimating ||
                health.CurrentHP <= 0 ||
                !IsInsideConsumptionRadius(health.transform.position, radiusSqr))
            {
                continue;
            }

            AddPhotonCandidate(health.photonView);
        }
    }

    void AddPhotonConsumptionCandidates<T>(float radiusSqr) where T : Component
    {
        T[] components = FindObjectsByType<T>(FindObjectsInactive.Exclude);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || !IsInsideConsumptionRadius(component.transform.position, radiusSqr))
                continue;

            PhotonView view = component.GetComponent<PhotonView>();
            if (view != null)
                AddPhotonCandidate(view);
        }
    }

    void AddPhotonCandidate(PhotonView view)
    {
        if (view == null || view.ViewID <= 0 || !consumptionViewIds.Add(view.ViewID))
            return;

        consumptionCandidates.Add(new ConsumptionCandidate
        {
            Kind = ConsumptionTargetKind.PhotonView,
            ViewId = view.ViewID,
            StableId = string.Empty
        });
    }

    void AddObstacleConsumptionCandidates(float radiusSqr)
    {
        ObstacleChunk[] chunks = FindObjectsByType<ObstacleChunk>(FindObjectsInactive.Exclude);
        for (int i = 0; i < chunks.Length; i++)
        {
            ObstacleChunk chunk = chunks[i];
            if (chunk == null ||
                string.IsNullOrWhiteSpace(chunk.StableId) ||
                chunk.CurrentHealth <= 0 ||
                !IsInsideConsumptionRadius(chunk.transform.position, radiusSqr))
            {
                continue;
            }

            consumptionCandidates.Add(new ConsumptionCandidate
            {
                Kind = ConsumptionTargetKind.Obstacle,
                ViewId = 0,
                StableId = chunk.StableId
            });
        }
    }

    static bool IsInsideConsumptionRadius(Vector3 position, float radiusSqr)
    {
        return ((Vector2)position).sqrMagnitude <= radiusSqr;
    }

    static void PlayConsumptionVisual(ConsumptionTargetKind kind, int viewId, string stableId, float duration)
    {
        Transform target = ResolveConsumptionTarget(kind, viewId, stableId);
        if (target != null)
            GravityWellConsumptionVfx.Play(target, duration);
    }

    static Transform ResolveConsumptionTarget(ConsumptionTargetKind kind, int viewId, string stableId)
    {
        if (kind == ConsumptionTargetKind.Obstacle)
        {
            ObstacleChunk chunk = ObstacleChunk.Find(stableId);
            return chunk != null ? chunk.transform : null;
        }

        PhotonView view = viewId > 0 ? PhotonView.Find(viewId) : null;
        return view != null ? view.transform : null;
    }

    public static void ApplyShipDrift(Rigidbody2D body, float effectMultiplier = 1f)
    {
        if (body == null ||
            !body.simulated ||
            body.bodyType != RigidbodyType2D.Dynamic ||
            !PhotonNetwork.InRoom ||
            RoomSettings.GetSessionState() != RoomSettings.SessionStateInPlay ||
            !RoomSettings.IsGravityWellPhysicsEnabled())
        {
            return;
        }

        Vector2 mapSize = RoomSettings.GetGameplayMapDimensions();
        float fieldRadius = Mathf.Max(10f, Mathf.Min(mapSize.x, mapSize.y) * FieldRadiusScale);
        Vector2 position = body.position;
        float distance = position.magnitude;
        if (distance <= InnerDeadRadius || distance > fieldRadius)
            return;

        float safeMultiplier = Mathf.Clamp(effectMultiplier, 0f, 2f);
        if (safeMultiplier <= 0.001f)
            return;

        float normalizedDistance = Mathf.InverseLerp(InnerDeadRadius, fieldRadius, distance);
        float strength = Mathf.Lerp(1f, 0.24f, Mathf.SmoothStep(0f, 1f, normalizedDistance)) * safeMultiplier;
        if (strength <= 0.001f)
            return;

        Vector2 outward = position / Mathf.Max(0.001f, distance);
        Vector2 toCenter = -outward;
        Vector2 tangent = new Vector2(-toCenter.y, toCenter.x);
        Vector2 velocity = body.linearVelocity;

        float outwardSpeed = Vector2.Dot(velocity, outward);
        if (outwardSpeed > 0f)
        {
            float resistance = 1f - Mathf.Exp(-ShipOutwardResistance * strength * Time.fixedDeltaTime);
            velocity -= outward * (outwardSpeed * resistance);
        }

        Vector2 acceleration =
            toCenter * (ShipDriftAcceleration * strength) +
            tangent * (ShipTangentialAcceleration * strength);
        body.linearVelocity = velocity + acceleration * Time.fixedDeltaTime;
    }

    void RebuildBodyCache()
    {
        Rigidbody2D[] bodies = FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Exclude);
        CachedFieldBody[] entries = new CachedFieldBody[bodies.Length];
        int count = 0;
        cachedObstacleCount = 0;

        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D body = bodies[i];
            if (body == null || !TryResolveAuthorityAndScale(body, out float objectScale, out GravityFieldBodyRole role))
                continue;

            entries[count++] = new CachedFieldBody
            {
                Body = body,
                Role = role,
                Scale = objectScale
            };

            if (role == GravityFieldBodyRole.Obstacle)
                cachedObstacleCount++;
        }

        System.Array.Resize(ref entries, count);
        cachedBodies = entries;
        if (cachedBodies.Length == 0)
            nextObstacleIndex = 0;
        else if (nextObstacleIndex >= cachedBodies.Length)
            nextObstacleIndex %= cachedBodies.Length;
    }

    void ApplyObstacleBatch(float fieldRadius)
    {
        if (cachedObstacleCount <= 0 || cachedBodies.Length == 0)
            return;

        int processed = 0;
        int inspected = 0;
        while (inspected < cachedBodies.Length && processed < MaxObstacleBodiesPerStep)
        {
            int index = (nextObstacleIndex + inspected) % cachedBodies.Length;
            inspected++;

            if (cachedBodies[index].Role != GravityFieldBodyRole.Obstacle)
                continue;

            ApplyField(cachedBodies[index], fieldRadius);
            processed++;
        }

        nextObstacleIndex = (nextObstacleIndex + inspected) % cachedBodies.Length;
    }

    void ApplyField(CachedFieldBody entry, float fieldRadius)
    {
        Rigidbody2D body = entry.Body;
        if (body == null ||
            !body.simulated ||
            body.bodyType != RigidbodyType2D.Dynamic)
        {
            return;
        }

        Vector2 position = body.position;
        float distance = position.magnitude;
        if (distance > fieldRadius ||
            (entry.Role != GravityFieldBodyRole.Obstacle && distance <= InnerDeadRadius))
            return;

        if (entry.Role == GravityFieldBodyRole.Obstacle)
        {
            ApplyObstacleField(body, position, distance, fieldRadius);
            return;
        }

        ApplyStandardField(body, position, distance, fieldRadius, entry.Scale);
    }

    void ApplyStandardField(Rigidbody2D body, Vector2 position, float distance, float fieldRadius, float objectScale)
    {
        float normalizedDistance = Mathf.InverseLerp(InnerDeadRadius, fieldRadius, distance);
        float strength = Mathf.Lerp(1.22f, 0.28f, Mathf.SmoothStep(0f, 1f, normalizedDistance));
        if (strength <= 0.001f)
            return;

        Vector2 toCenter = -position.normalized;
        Vector2 tangent = new Vector2(-toCenter.y, toCenter.x);
        float massFactor = 1f / Mathf.Sqrt(Mathf.Max(0.45f, body.mass));
        float orbitalStrength = Mathf.Lerp(1.18f, 0.5f, normalizedDistance);
        Vector2 acceleration =
            toCenter * (PullAcceleration * strength) +
            tangent * (TangentialAcceleration * orbitalStrength);

        Vector2 currentVelocity = body.linearVelocity;
        Vector2 nextVelocity = currentVelocity + acceleration * (objectScale * massFactor * Time.fixedDeltaTime);
        float speedCap = Mathf.Max(currentVelocity.magnitude, MaxFieldAddedSpeed * Mathf.Max(0.35f, objectScale));
        if (nextVelocity.sqrMagnitude > speedCap * speedCap)
            nextVelocity = nextVelocity.normalized * speedCap;

        body.linearVelocity = nextVelocity;
    }

    void ApplyObstacleField(Rigidbody2D body, Vector2 position, float distance, float fieldRadius)
    {
        if (distance <= 0.001f)
            return;

        Vector2 outward = position / distance;
        Vector2 toCenter = -outward;
        Vector2 tangent = new Vector2(-toCenter.y, toCenter.x);
        float massFactor = 1f / Mathf.Sqrt(Mathf.Max(0.45f, body.mass));
        float normalizedDistance = Mathf.InverseLerp(ObstacleInnerOrbitRadius, fieldRadius, distance);
        float strength = Mathf.Lerp(1.08f, 0.22f, Mathf.SmoothStep(0f, 1f, normalizedDistance));

        Vector2 acceleration;
        if (distance < ObstacleInnerOrbitRadius)
        {
            float repel = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ObstacleInnerOrbitRadius, InnerDeadRadius, distance));
            acceleration =
                outward * (PullAcceleration * ObstacleInnerRepelScale * repel) +
                tangent * (TangentialAcceleration * ObstacleTangentialScale);
        }
        else
        {
            float orbitalStrength = Mathf.Lerp(1.05f, 0.5f, normalizedDistance);
            acceleration =
                toCenter * (PullAcceleration * strength) +
                tangent * (TangentialAcceleration * ObstacleTangentialScale * orbitalStrength);
        }

        Vector2 nextVelocity = body.linearVelocity + acceleration * (ObstacleFieldScale * massFactor * Time.fixedDeltaTime);
        if (nextVelocity.sqrMagnitude > ObstacleMaxFieldSpeed * ObstacleMaxFieldSpeed)
            nextVelocity = nextVelocity.normalized * ObstacleMaxFieldSpeed;

        body.linearVelocity = nextVelocity;
    }

    bool TryResolveAuthorityAndScale(Rigidbody2D body, out float objectScale, out GravityFieldBodyRole role)
    {
        objectScale = 1f;
        role = GravityFieldBodyRole.Standard;

        PlayerHealth health = body.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            if (health.IsBotControlled)
                return false;

            if (health.IsWreck)
            {
                objectScale = WreckScale;
                return PhotonNetwork.IsMasterClient;
            }

            if (!health.IsAstronautControlled)
                return false;

            PhotonView view = health.photonView;
            if (view != null && !view.IsMine)
                return false;

            objectScale = AstronautScale;
            return true;
        }

        if (body.GetComponentInParent<EnemyBot>() != null ||
            body.GetComponentInParent<PlayerDeployableBase>() != null ||
            body.GetComponentInParent<LureBeaconDecoy>() != null)
        {
            return false;
        }

        if (!PhotonNetwork.IsMasterClient)
            return false;

        if (body.GetComponentInParent<Treasure>() != null ||
            body.GetComponentInParent<DroppedCargoCrate>() != null)
        {
            objectScale = TreasureScale;
            return true;
        }

        if (body.GetComponentInParent<ShipWreck>() != null)
        {
            objectScale = WreckScale;
            return true;
        }

        MovingSpaceObject movingObject = body.GetComponentInParent<MovingSpaceObject>();
        if (movingObject != null)
        {
            if (movingObject.ObjectType == MovingSpaceObject.SpaceObjectType.Obstacle)
            {
                role = GravityFieldBodyRole.Obstacle;
                objectScale = ObstacleFieldScale;
                return true;
            }

            objectScale = WreckScale;
            return true;
        }

        return false;
    }

    static int ConvertToInt(object value)
    {
        if (value is int intValue)
            return intValue;

        if (value is byte byteValue)
            return byteValue;

        if (value is short shortValue)
            return shortValue;

        if (value is float floatValue)
            return Mathf.RoundToInt(floatValue);

        if (value is double doubleValue)
            return Mathf.RoundToInt((float)doubleValue);

        return 0;
    }

    static float ConvertToFloat(object value)
    {
        if (value is float floatValue)
            return floatValue;

        if (value is double doubleValue)
            return (float)doubleValue;

        if (value is int intValue)
            return intValue;

        return 0f;
    }
}

public sealed class GravityWellConsumptionVfx : MonoBehaviour
{
    const float MinimumScale = 0.01f;

    Vector3 startScale;
    Vector3 endScale;
    float startedAt;
    float duration = 0.9f;

    public static void Play(Transform target, float animationDuration)
    {
        if (target == null)
            return;

        GravityWellConsumptionVfx vfx = target.GetComponent<GravityWellConsumptionVfx>();
        if (vfx == null)
            vfx = target.gameObject.AddComponent<GravityWellConsumptionVfx>();

        vfx.Initialize(Mathf.Max(0.05f, animationDuration));
    }

    void Initialize(float animationDuration)
    {
        duration = animationDuration;
        startedAt = Time.time;
        startScale = transform.localScale;
        endScale = new Vector3(
            ResolveEndScaleAxis(startScale.x),
            ResolveEndScaleAxis(startScale.y),
            startScale.z);
        enabled = true;
    }

    void Update()
    {
        float progress = Mathf.Clamp01((Time.time - startedAt) / Mathf.Max(0.05f, duration));
        float eased = progress * progress * (3f - 2f * progress);
        transform.localScale = Vector3.Lerp(startScale, endScale, eased);

        if (progress >= 1f)
            enabled = false;
    }

    static float ResolveEndScaleAxis(float value)
    {
        float sign = value < 0f ? -1f : 1f;
        return sign * MinimumScale;
    }
}
