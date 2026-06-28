using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MovingSpaceObject : MonoBehaviour
{
    public enum SpaceObjectType
    {
        Obstacle,
        Treasure,
        Container
    }

    const float ObstacleBaseSpeed = 0.22f;
    const float TreasureBaseSpeed = 0.3f;
    const float ContainerBaseSpeed = 0.14f;
    const float VelocityDamping = 0.12f;
    const float CruiseAcceleration = 2.1f;
    const float MaxSpeedMultiplier = 3.1f;
    const float MinCruiseSpeedFactor = 0.78f;
    const float MinAngularSpeed = 12f;
    const float MaxAngularSpeed = 38f;
    const float MaxCollisionAngularSpeed = 55f;
    const float MaxBoundaryAngularSpeed = 48f;
    const float CollisionSpinFlipThreshold = 0.85f;
    const float CollisionSpinFlipChance = 0.45f;
    const float HighSnapshotInterval = 0.09f;
    const float BalancedSnapshotInterval = 0.12f;
    const float LowSnapshotInterval = 0.16f;
    const float SnapshotPositionDelta = 0.035f;
    const float SnapshotVelocityDelta = 0.04f;
    const float SnapshotRotationDelta = 1.25f;
    const float SnapshotAngularVelocityDelta = 2.5f;
    const float MinSnapshotHeartbeatInterval = 0.36f;
    const float RemoteSmoothing = 16f;
    const float RemotePredictionMaxOffset = 1.1f;
    const float RemotePredictionMaxTime = 0.24f;
    const float PushBoostDuration = 0.55f;
    const float TreasurePushMaxSpeed = 6.2f;
    const float ContainerPushMaxSpeed = 4.2f;
    const float ObstaclePushMaxSpeed = 4.2f;
    const float SimulationModeRefreshInterval = 0.25f;
    const float LowSimulationModeRefreshInterval = 0.35f;

    static readonly Dictionary<string, MovingSpaceObject> ObjectsById = new Dictionary<string, MovingSpaceObject>();
    static PhysicsMaterial2D sharedBouncyMaterial;
    static PhysicsMaterial2D sharedSoftBoundaryMaterial;
    static Collider2D[] cachedWallColliders;

    string stableId;
    SpaceObjectType objectType;
    Rigidbody2D rb;
    Vector2 cruiseDirection;
    float speedMultiplier = 1f;
    float baseAngularSpeed;
    float nextSnapshotTime;
    float lastBroadcastTime;
    bool isAuthority;
    bool movingEnabled;
    bool translateEnabled;
    bool rotateEnabled;
    bool hasBroadcastSnapshot;
    Vector2 lastBroadcastPosition;
    Vector2 lastBroadcastVelocity;
    float lastBroadcastRotation;
    float lastBroadcastAngularVelocity;
    Vector2 networkPosition;
    Vector2 networkVelocity;
    float networkRotation;
    float networkAngularVelocity;
    float lastNetworkStateTime;
    bool hasNetworkState;
    Vector2 predictedLocalOffset;
    bool configured;
    bool boundaryCollisionIgnoreInitialized;
    bool lastBoundaryIgnoreSetting;
    Collider2D[] cachedObjectColliders;
    float pushBoostUntil;
    float pushBoostMaxSpeed;
    bool simulationModeApplied;
    float nextSimulationModeRefreshTime;
    float cachedMassFactor = 1f;

    public string StableId => stableId;
    public SpaceObjectType ObjectType => objectType;

    public static PhysicsMaterial2D GetSharedBouncyMaterial()
    {
        if (sharedBouncyMaterial == null)
        {
            sharedBouncyMaterial = new PhysicsMaterial2D("MovingSpaceObjectBouncy")
            {
                friction = 0f,
                bounciness = 0.12f
            };
        }

        return sharedBouncyMaterial;
    }

    public static PhysicsMaterial2D GetSharedSoftBoundaryMaterial()
    {
        if (sharedSoftBoundaryMaterial == null)
        {
            sharedSoftBoundaryMaterial = new PhysicsMaterial2D("MapBoundarySoft")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        return sharedSoftBoundaryMaterial;
    }

    public static MovingSpaceObject Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        ObjectsById.TryGetValue(id, out MovingSpaceObject value);
        return value;
    }

    public void Configure(string id, SpaceObjectType type)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        if (!string.IsNullOrWhiteSpace(stableId))
        {
            ObjectsById.Remove(stableId);
        }

        stableId = id;
        objectType = type;
        EnsureRigidBody();
        ConfigureMotionFromId(id);
        ApplySimulationMode(true);
        ResetSnapshotTracking();

        ObjectsById[stableId] = this;
        configured = true;
    }

    void Awake()
    {
        EnsureRigidBody();
    }

    void FixedUpdate()
    {
        if (!configured)
            return;

        ApplySimulationMode();

        if (isAuthority)
        {
            SimulateAuthorityMotion();
            BroadcastSnapshotIfNeeded();
        }
        else
        {
            FollowAuthoritySnapshot();
        }
    }

    void OnDestroy()
    {
        if (!string.IsNullOrWhiteSpace(stableId))
        {
            ObjectsById.Remove(stableId);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!RoomSettings.AreMovingObjectsEnabled())
            return;

        PlayerMovement player = collision.collider.GetComponentInParent<PlayerMovement>();
        if (player != null && player.GetComponent<AstronautSurvivor>() == null)
        {
            if (objectType == SpaceObjectType.Obstacle && RoomSettings.IsObstacleMassMax())
            {
                if (isAuthority)
                    rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, GetBaseSpeed() * MaxSpeedMultiplier);

                return;
            }

            Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
            Vector2 playerVelocity = playerRb != null ? playerRb.linearVelocity : Vector2.zero;
            if (playerVelocity.sqrMagnitude >= 0.01f)
            {
                int weightFactor = objectType == SpaceObjectType.Obstacle
                    ? RoomSettings.GetObstacleWeightFactor()
                    : RoomSettings.GetTreasureWeightFactor();
                weightFactor = Mathf.Max(1, weightFactor);

                Vector2 impulse = (playerVelocity * 1.25f) / weightFactor;
                if (isAuthority)
                    ApplyImpulse(impulse * 0.82f);
            }
        }

        if (isAuthority)
        {
            TryFlipSpinOnCollision(collision);
            return;
        }
    }

    public void ApplyImpulse(Vector2 impulse)
    {
        if (rb == null)
            return;

        ApplySimulationMode();
        if (!isAuthority)
            return;

        if (!movingEnabled)
            return;

        MarkPushBoost(impulse.magnitude);
        if (translateEnabled)
            rb.linearVelocity += impulse * 0.7f;
        else
            rb.linearVelocity = Vector2.zero;

        if (rotateEnabled && Mathf.Abs(impulse.x) + Mathf.Abs(impulse.y) > 0.05f)
        {
            float torqueDirection = Mathf.Sign(Vector3.Cross(cruiseDirection, impulse).z);
            if (Mathf.Abs(torqueDirection) < 0.1f)
                torqueDirection = Random.value < 0.5f ? -1f : 1f;

            float addedAngular = torqueDirection * Mathf.Lerp(4f, 12f, Mathf.Clamp01(impulse.magnitude / 3f));
            rb.angularVelocity = Mathf.Clamp(rb.angularVelocity + addedAngular, -MaxCollisionAngularSpeed, MaxCollisionAngularSpeed);
        }
    }

    public void ApplyPlayerPush(Vector2 impulse, Vector2 playerPosition)
    {
        if (rb == null)
            return;

        ApplySimulationMode();
        if (!isAuthority || !movingEnabled || !translateEnabled)
            return;

        if (impulse.sqrMagnitude < 0.0001f)
            return;

        Vector2 correctedImpulse = impulse;
        Vector2 awayFromPlayer = rb.position - playerPosition;
        if (awayFromPlayer.sqrMagnitude > 0.0001f &&
            Vector2.Dot(correctedImpulse.normalized, awayFromPlayer.normalized) < -0.15f)
        {
            correctedImpulse = awayFromPlayer.normalized * correctedImpulse.magnitude;
        }

        float pushMultiplier = GetPlayerPushMultiplier();
        MarkPushBoost(correctedImpulse.magnitude * pushMultiplier);
        rb.linearVelocity += correctedImpulse * pushMultiplier;

        if (rb.linearVelocity.sqrMagnitude > 0.001f)
            cruiseDirection = rb.linearVelocity.normalized;

        if (rotateEnabled)
        {
            float torqueDirection = Mathf.Sign(Vector3.Cross(awayFromPlayer.sqrMagnitude > 0.0001f ? awayFromPlayer.normalized : cruiseDirection, correctedImpulse.normalized).z);
            if (Mathf.Abs(torqueDirection) < 0.1f)
                torqueDirection = Random.value < 0.5f ? -1f : 1f;

            rb.angularVelocity = Mathf.Clamp(
                rb.angularVelocity + torqueDirection * Mathf.Lerp(9f, 24f, Mathf.Clamp01(correctedImpulse.magnitude / 8f)),
                -MaxCollisionAngularSpeed,
                MaxCollisionAngularSpeed);
        }
    }

    public void ApplyMagneticPull(Vector2 sourcePosition, float strength, float deltaTime)
    {
        if (rb == null || strength <= 0f || deltaTime <= 0f)
            return;

        ApplySimulationMode();
        if (!isAuthority || !movingEnabled || !translateEnabled)
            return;

        Vector2 toSource = sourcePosition - rb.position;
        float distance = toSource.magnitude;
        if (distance <= 0.05f)
            return;

        float effectiveMass = Mathf.Clamp(GetMassFactor(), 1f, 12f);
        float falloff = Mathf.Clamp01(1f - (distance / 8f));
        float pullAcceleration = strength * Mathf.Lerp(0.45f, 1f, falloff) / effectiveMass;
        rb.linearVelocity += toSource.normalized * pullAcceleration * deltaTime;

        float maxMagneticSpeed = GetMagneticMaxSpeed();
        if (rb.linearVelocity.sqrMagnitude > maxMagneticSpeed * maxMagneticSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxMagneticSpeed;

        if (rb.linearVelocity.sqrMagnitude > 0.001f)
            cruiseDirection = rb.linearVelocity.normalized;
    }

    public void ApplyTractorTetherPull(Vector2 sourcePosition, float strength, float slackDistance, float deltaTime)
    {
        if (rb == null || strength <= 0f || deltaTime <= 0f)
            return;

        ApplySimulationMode();
        if (!isAuthority || !movingEnabled || !translateEnabled)
            return;

        Vector2 toSource = sourcePosition - rb.position;
        float distance = toSource.magnitude;
        if (distance <= 0.05f)
            return;

        float stretch = Mathf.Max(0f, distance - Mathf.Max(0.25f, slackDistance));
        float tetherRamp = Mathf.Clamp01(stretch / 4.25f);
        float effectiveMass = Mathf.Clamp(GetMassFactor(), 1f, 8f);
        float softenedMass = Mathf.Lerp(effectiveMass, 1.35f, tetherRamp * 0.72f);
        float pullAcceleration = strength * Mathf.Lerp(0.75f, 3.6f, tetherRamp) / Mathf.Max(1f, softenedMass);
        rb.linearVelocity += toSource.normalized * pullAcceleration * deltaTime;

        float maxTetherSpeed = Mathf.Lerp(
            Mathf.Max(GetMagneticMaxSpeed(), 6.2f),
            12.5f,
            tetherRamp);
        if (rb.linearVelocity.sqrMagnitude > maxTetherSpeed * maxTetherSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxTetherSpeed;

        if (rb.linearVelocity.sqrMagnitude > 0.001f)
            cruiseDirection = rb.linearVelocity.normalized;
    }

    public void ForceBroadcastSnapshot()
    {
        if (!isAuthority || rb == null || string.IsNullOrWhiteSpace(stableId))
            return;

        float now = Time.time;
        nextSnapshotTime = now + GetSnapshotInterval();
        BroadcastSnapshot(now);
    }

    public void ApplyNetworkState(Vector2 position, Vector2 velocity, float rotation, float angularVelocity)
    {
        if (isAuthority)
            return;

        networkPosition = position;
        networkVelocity = velocity;
        networkRotation = rotation;
        networkAngularVelocity = angularVelocity;
        lastNetworkStateTime = Time.time;
        hasNetworkState = true;
        predictedLocalOffset *= 0.35f;

        if (rb != null && Vector2.Distance(rb.position, position) > 2f)
        {
            rb.position = position;
            rb.rotation = rotation;
            predictedLocalOffset = Vector2.zero;
        }
    }

    public void ApplyRemotePushPrediction(Vector2 impulse)
    {
        if (isAuthority || rb == null || impulse.sqrMagnitude < 0.0001f)
            return;

        float predictionScale = GetRemotePredictionScale();
        Vector2 offset = impulse * predictionScale;
        float maxOffset = GetRemotePredictionMaxOffset();
        predictedLocalOffset = Vector2.ClampMagnitude(predictedLocalOffset + offset, maxOffset);
    }

    public void SetMotionState(Vector2 position, Vector2 velocity, float rotation, float angularVelocity, bool authorityState)
    {
        EnsureRigidBody();
        ApplySimulationMode(true);

        rb.position = position;
        rb.rotation = rotation;
        rb.linearVelocity = translateEnabled ? velocity : Vector2.zero;
        rb.angularVelocity = rotateEnabled ? angularVelocity : 0f;

        if (translateEnabled && velocity.sqrMagnitude > 0.0001f)
            cruiseDirection = velocity.normalized;

        networkPosition = position;
        networkVelocity = velocity;
        networkRotation = rotation;
        networkAngularVelocity = angularVelocity;
        lastNetworkStateTime = Time.time;
        hasNetworkState = true;
    }

    public void NotifyColliderShapeChanged()
    {
        cachedObjectColliders = GetComponents<Collider2D>();
        boundaryCollisionIgnoreInitialized = false;
    }

    void EnsureRigidBody()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        rb.simulated = true;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.None;
        ApplyRigidbodyRuntimeSettings();

        Collider2D[] colliders = GetComponents<Collider2D>();
        cachedObjectColliders = colliders;
        PhysicsMaterial2D material = GetSharedBouncyMaterial();
        foreach (Collider2D currentCollider in colliders)
        {
            if (currentCollider != null)
            {
                currentCollider.sharedMaterial = material;
            }
        }
    }

    void ConfigureMotionFromId(string id)
    {
        int hash = id.GetHashCode();
        float seedA = Mathf.Abs(hash * 0.00017f) + 4.7f;
        float seedB = Mathf.Abs(hash * 0.00031f) + 9.9f;
        float angle = Mathf.PerlinNoise(seedA, seedB) * Mathf.PI * 2f;
        cruiseDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        float minSpeedMultiplier = objectType == SpaceObjectType.Container ? 0.7f : 0.45f;
        float maxSpeedMultiplier = objectType == SpaceObjectType.Container ? 1.25f : 1.85f;
        speedMultiplier = Mathf.Lerp(minSpeedMultiplier, maxSpeedMultiplier, Mathf.PerlinNoise(seedB, seedA + 3.2f));
        float angularSeed = Mathf.PerlinNoise(seedA + 7.1f, seedB + 4.6f);
        float minAngularSpeed = objectType == SpaceObjectType.Container ? 6f : MinAngularSpeed;
        float maxAngularSpeed = objectType == SpaceObjectType.Container ? 18f : MaxAngularSpeed;
        baseAngularSpeed = Mathf.Lerp(minAngularSpeed, maxAngularSpeed, angularSeed);
        if (Mathf.PerlinNoise(seedB + 1.8f, seedA + 5.5f) < 0.5f)
            baseAngularSpeed *= -1f;
        if (cruiseDirection.sqrMagnitude < 0.0001f)
        {
            cruiseDirection = Vector2.right;
        }
    }

    void ApplySimulationMode(bool force = false)
    {
        if (rb == null)
            EnsureRigidBody();

        float now = Time.unscaledTime;
        if (!force && simulationModeApplied && now < nextSimulationModeRefreshTime)
            return;

        nextSimulationModeRefreshTime = now + GetSimulationModeRefreshInterval();

        string movingObjectsMode = RoomSettings.GetMovingObjectsMode();
        bool nextMovingEnabled = movingObjectsMode != RoomSettings.MovingObjectsModeOff;
        bool nextTranslateEnabled = movingObjectsMode == RoomSettings.MovingObjectsModeOn;
        bool nextRotateEnabled = movingObjectsMode != RoomSettings.MovingObjectsModeOff;
        bool nextAuthority = !PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient;
        float nextMassFactor = cachedMassFactor;
        if (nextAuthority && nextMovingEnabled)
            nextMassFactor = Mathf.Max(1f, GetMassFactor());

        bool bodySettingsChanged =
            force ||
            !simulationModeApplied ||
            movingEnabled != nextMovingEnabled ||
            translateEnabled != nextTranslateEnabled ||
            rotateEnabled != nextRotateEnabled ||
            isAuthority != nextAuthority ||
            !Mathf.Approximately(cachedMassFactor, nextMassFactor);

        movingEnabled = nextMovingEnabled;
        translateEnabled = nextTranslateEnabled;
        rotateEnabled = nextRotateEnabled;
        isAuthority = nextAuthority;
        cachedMassFactor = nextMassFactor;
        simulationModeApplied = true;

        ApplyRigidbodyRuntimeSettings();

        if (objectType == SpaceObjectType.Obstacle)
        {
            bool ignoreWalls = RoomSettings.AreObstaclesBorderless();
            if (!boundaryCollisionIgnoreInitialized || ignoreWalls != lastBoundaryIgnoreSetting)
                RefreshBoundaryCollisionIgnore(ignoreWalls);
        }

        if (!bodySettingsChanged)
            return;

        if (isAuthority && movingEnabled)
        {
            EnsureDynamicBody();
            if (!translateEnabled)
            {
                rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezePositionY;
                rb.linearVelocity = Vector2.zero;
            }

            if (!rotateEnabled)
                rb.angularVelocity = 0f;
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.constraints = RigidbodyConstraints2D.None;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    void RefreshBoundaryCollisionIgnore(bool ignoreWalls)
    {
        if (objectType != SpaceObjectType.Obstacle)
            return;

        Collider2D[] objectColliders = cachedObjectColliders != null && cachedObjectColliders.Length > 0
            ? cachedObjectColliders
            : GetComponents<Collider2D>();
        if (objectColliders == null || objectColliders.Length == 0)
            return;

        cachedObjectColliders = objectColliders;
        Collider2D[] wallColliders = GetWallColliders();
        for (int wallIndex = 0; wallIndex < wallColliders.Length; wallIndex++)
        {
            Collider2D wallCollider = wallColliders[wallIndex];
            if (wallCollider == null)
                continue;

            for (int i = 0; i < objectColliders.Length; i++)
            {
                Collider2D objectCollider = objectColliders[i];
                if (objectCollider != null)
                    Physics2D.IgnoreCollision(objectCollider, wallCollider, ignoreWalls);
            }
        }

        lastBoundaryIgnoreSetting = ignoreWalls;
        boundaryCollisionIgnoreInitialized = true;
    }

    static Collider2D[] GetWallColliders()
    {
        if (cachedWallColliders != null && cachedWallColliders.Length == 4)
        {
            bool allPresent = true;
            for (int i = 0; i < cachedWallColliders.Length; i++)
            {
                if (cachedWallColliders[i] == null)
                {
                    allPresent = false;
                    break;
                }
            }

            if (allPresent)
                return cachedWallColliders;
        }

        string[] wallNames = { "WallTop", "WallBottom", "WallLeft", "WallRight" };
        cachedWallColliders = new Collider2D[wallNames.Length];
        for (int i = 0; i < wallNames.Length; i++)
        {
            GameObject wall = GameObject.Find(wallNames[i]);
            cachedWallColliders[i] = wall != null ? wall.GetComponent<Collider2D>() : null;
        }

        return cachedWallColliders;
    }

    void EnsureDynamicBody()
    {
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.constraints = RigidbodyConstraints2D.None;
        rb.mass = cachedMassFactor;
        rb.linearDamping = VelocityDamping;
        rb.angularDamping = 0.18f;
    }

    void ApplyRigidbodyRuntimeSettings()
    {
        if (rb == null)
            return;

        rb.simulated = true;
        rb.gravityScale = 0f;

        RigidbodyInterpolation2D preferredInterpolation = GetPreferredInterpolation();
        if (rb.interpolation != preferredInterpolation)
            rb.interpolation = preferredInterpolation;

        CollisionDetectionMode2D preferredCollisionDetection = GetPreferredCollisionDetectionMode();
        if (rb.collisionDetectionMode != preferredCollisionDetection)
            rb.collisionDetectionMode = preferredCollisionDetection;

        RigidbodySleepMode2D preferredSleepMode = GetPreferredSleepMode();
        if (rb.sleepMode != preferredSleepMode)
            rb.sleepMode = preferredSleepMode;

        bool preferredFullKinematicContacts = GetPreferredFullKinematicContacts();
        if (rb.useFullKinematicContacts != preferredFullKinematicContacts)
            rb.useFullKinematicContacts = preferredFullKinematicContacts;
    }

    static float GetSimulationModeRefreshInterval()
    {
        if (!Application.isMobilePlatform)
            return SimulationModeRefreshInterval;

        return MobilePerformanceSettings.CurrentProfile == MobilePerformanceProfile.Low
            ? LowSimulationModeRefreshInterval
            : SimulationModeRefreshInterval;
    }

    static bool UseReducedMobileMovingBodyPhysics()
    {
        return Application.isMobilePlatform &&
            MobilePerformanceSettings.CurrentProfile != MobilePerformanceProfile.High;
    }

    static RigidbodyInterpolation2D GetPreferredInterpolation()
    {
        if (!UseReducedMobileMovingBodyPhysics())
            return RigidbodyInterpolation2D.Interpolate;

        return MobilePerformanceSettings.CurrentProfile == MobilePerformanceProfile.Low
            ? RigidbodyInterpolation2D.None
            : RigidbodyInterpolation2D.Interpolate;
    }

    static CollisionDetectionMode2D GetPreferredCollisionDetectionMode()
    {
        return UseReducedMobileMovingBodyPhysics()
            ? CollisionDetectionMode2D.Discrete
            : CollisionDetectionMode2D.Continuous;
    }

    static RigidbodySleepMode2D GetPreferredSleepMode()
    {
        return UseReducedMobileMovingBodyPhysics()
            ? RigidbodySleepMode2D.StartAwake
            : RigidbodySleepMode2D.NeverSleep;
    }

    static bool GetPreferredFullKinematicContacts()
    {
        return !UseReducedMobileMovingBodyPhysics();
    }

    void SimulateAuthorityMotion()
    {
        if (!movingEnabled || rb == null)
            return;

        if (!translateEnabled)
        {
            rb.linearVelocity = Vector2.zero;
            MaintainAuthoritySpin();
            return;
        }

        float baseSpeed = GetBaseSpeed();
        float minCruiseSpeed = baseSpeed * MinCruiseSpeedFactor;
        float maxSpeed = Mathf.Max(baseSpeed * MaxSpeedMultiplier, GetPushBoostMaxSpeed());

        if (rb.linearVelocity.sqrMagnitude > 0.0025f)
        {
            cruiseDirection = rb.linearVelocity.normalized;
        }
        else
        {
            rb.linearVelocity = cruiseDirection * baseSpeed;
        }

        if (rb.linearVelocity.magnitude < minCruiseSpeed)
        {
            rb.AddForce(cruiseDirection * CruiseAcceleration, ForceMode2D.Force);
        }

        if (rb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

        MaintainAuthoritySpin();

        KeepInsideMapBounds();
    }

    void MaintainAuthoritySpin()
    {
        if (!rotateEnabled || rb == null)
        {
            if (rb != null)
                rb.angularVelocity = 0f;
            return;
        }

        if (Mathf.Abs(rb.angularVelocity) < Mathf.Abs(baseAngularSpeed) * 0.82f)
        {
            float angularStep = Mathf.Sign(baseAngularSpeed) * 22f * Time.fixedDeltaTime;
            rb.angularVelocity += angularStep;
        }
    }

    void BroadcastSnapshotIfNeeded()
    {
        if (!PhotonNetwork.IsConnected || rb == null || string.IsNullOrWhiteSpace(stableId))
            return;

        float now = Time.time;
        float interval = GetSnapshotInterval();
        if (now < nextSnapshotTime)
            return;

        nextSnapshotTime = now + interval;
        if (!ShouldBroadcastSnapshot(now, interval))
            return;

        BroadcastSnapshot(now);
    }

    void FollowAuthoritySnapshot()
    {
        if (!hasNetworkState || rb == null)
            return;

        predictedLocalOffset = Vector2.Lerp(predictedLocalOffset, Vector2.zero, 1f - Mathf.Exp(-8f * Time.fixedDeltaTime));
        float predictionTime = Mathf.Clamp(Time.time - lastNetworkStateTime, 0f, RemotePredictionMaxTime);
        Vector2 predictedPosition = networkPosition + networkVelocity * predictionTime + predictedLocalOffset;
        float smoothing = 1f - Mathf.Exp(-RemoteSmoothing * Time.fixedDeltaTime);

        if (Vector2.Distance(rb.position, predictedPosition) > 1.6f)
        {
            rb.position = predictedPosition;
            rb.rotation = networkRotation;
        }

        Vector2 nextPosition = Vector2.Lerp(rb.position, predictedPosition, smoothing);
        float nextRotation = Mathf.LerpAngle(rb.rotation, networkRotation, smoothing);
        rb.MovePosition(nextPosition);
        rb.MoveRotation(nextRotation);
    }

    void ResetSnapshotTracking()
    {
        hasBroadcastSnapshot = false;
        lastBroadcastTime = 0f;
        nextSnapshotTime = Time.time + GetSnapshotStagger();
    }

    void BroadcastSnapshot(float now)
    {
        SpaceObjectMotionSync.BroadcastState(stableId, rb.position, rb.linearVelocity, rb.rotation, rb.angularVelocity);
        RememberBroadcastSnapshot(now);
    }

    void RememberBroadcastSnapshot(float now)
    {
        hasBroadcastSnapshot = true;
        lastBroadcastTime = now;
        lastBroadcastPosition = rb.position;
        lastBroadcastVelocity = rb.linearVelocity;
        lastBroadcastRotation = rb.rotation;
        lastBroadcastAngularVelocity = rb.angularVelocity;
    }

    bool ShouldBroadcastSnapshot(float now, float interval)
    {
        if (!hasBroadcastSnapshot)
            return true;

        float heartbeatInterval = Mathf.Max(interval * 3f, MinSnapshotHeartbeatInterval);
        if (now - lastBroadcastTime >= heartbeatInterval)
            return true;

        if ((rb.position - lastBroadcastPosition).sqrMagnitude >= SnapshotPositionDelta * SnapshotPositionDelta)
            return true;

        if ((rb.linearVelocity - lastBroadcastVelocity).sqrMagnitude >= SnapshotVelocityDelta * SnapshotVelocityDelta)
            return true;

        if (Mathf.Abs(Mathf.DeltaAngle(lastBroadcastRotation, rb.rotation)) >= SnapshotRotationDelta)
            return true;

        return Mathf.Abs(rb.angularVelocity - lastBroadcastAngularVelocity) >= SnapshotAngularVelocityDelta;
    }

    static float GetSnapshotInterval()
    {
        if (!Application.isMobilePlatform)
            return HighSnapshotInterval;

        switch (MobilePerformanceSettings.CurrentProfile)
        {
            case MobilePerformanceProfile.Low:
                return LowSnapshotInterval;
            case MobilePerformanceProfile.Balanced:
                return BalancedSnapshotInterval;
            default:
                return HighSnapshotInterval;
        }
    }

    float GetSnapshotStagger()
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return 0f;

        unchecked
        {
            uint hash = (uint)stableId.GetHashCode();
            return (hash % 1000) / 1000f * GetSnapshotInterval();
        }
    }

    void TryFlipSpinOnCollision(Collision2D collision)
    {
        if (rb == null || collision == null)
            return;

        float relativeSpeed = collision.relativeVelocity.magnitude;
        if (relativeSpeed < CollisionSpinFlipThreshold || Random.value > CollisionSpinFlipChance)
            return;

        float currentAngular = Mathf.Abs(rb.angularVelocity) > 0.1f ? rb.angularVelocity : baseAngularSpeed;
        float flippedAngular = -currentAngular * Mathf.Lerp(0.7f, 0.95f, Mathf.Clamp01(relativeSpeed / 4f));
        flippedAngular = Mathf.Clamp(flippedAngular, -MaxCollisionAngularSpeed, MaxCollisionAngularSpeed);
        rb.angularVelocity = flippedAngular;
        baseAngularSpeed = Mathf.Sign(flippedAngular) * Mathf.Min(Mathf.Abs(baseAngularSpeed), MaxAngularSpeed);
    }

    float GetBaseSpeed()
    {
        float baseSpeed = objectType switch
        {
            SpaceObjectType.Obstacle => ObstacleBaseSpeed,
            SpaceObjectType.Container => ContainerBaseSpeed,
            _ => TreasureBaseSpeed
        };
        return baseSpeed * speedMultiplier;
    }

    void MarkPushBoost(float impulseMagnitude)
    {
        float targetMaxSpeed = GetPushMaxSpeed();
        pushBoostMaxSpeed = Mathf.Max(
            pushBoostMaxSpeed,
            Mathf.Lerp(targetMaxSpeed * 0.62f, targetMaxSpeed, Mathf.Clamp01(impulseMagnitude / 8f)));
        pushBoostUntil = Time.time + PushBoostDuration;
    }

    float GetPushBoostMaxSpeed()
    {
        if (Time.time <= pushBoostUntil)
            return pushBoostMaxSpeed;

        pushBoostMaxSpeed = 0f;
        return 0f;
    }

    float GetPushMaxSpeed()
    {
        return objectType switch
        {
            SpaceObjectType.Treasure => TreasurePushMaxSpeed,
            SpaceObjectType.Container => ContainerPushMaxSpeed,
            _ => ObstaclePushMaxSpeed
        };
    }

    float GetPlayerPushMultiplier()
    {
        return objectType switch
        {
            SpaceObjectType.Treasure => 1.45f,
            SpaceObjectType.Container => 1.18f,
            _ => 1.1f
        };
    }

    float GetMagneticMaxSpeed()
    {
        return objectType switch
        {
            SpaceObjectType.Treasure => 5.8f,
            SpaceObjectType.Container => 4.2f,
            _ => 4.4f
        };
    }

    float GetRemotePredictionScale()
    {
        return objectType switch
        {
            SpaceObjectType.Treasure => 0.18f,
            SpaceObjectType.Container => 0.14f,
            _ => 0.105f
        };
    }

    float GetRemotePredictionMaxOffset()
    {
        return objectType switch
        {
            SpaceObjectType.Treasure => RemotePredictionMaxOffset * 1.45f,
            SpaceObjectType.Container => RemotePredictionMaxOffset * 1.15f,
            _ => RemotePredictionMaxOffset
        };
    }

    float GetMassFactor()
    {
        if (objectType == SpaceObjectType.Obstacle)
        {
            return RoomSettings.IsObstacleMassMax()
                ? 100000f
                : RoomSettings.GetObstacleWeightFactor();
        }

        return RoomSettings.GetTreasureWeightFactor();
    }

    void KeepInsideMapBounds()
    {
        if (rb == null)
            return;

        if (objectType == SpaceObjectType.Obstacle && RoomSettings.AreObstaclesBorderless())
        {
            WrapAcrossMapBounds();
            return;
        }

        Vector2 position = rb.position;
        MapInstanceService.TryGetBoundsForWorldPosition(position, out MapInstanceService.BoundsInfo bounds);
        Vector2 mapSize = bounds.Size;
        Vector2 center = bounds.Center;
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        float boundsRadius = GetBoundsRadius();

        Vector2 velocity = rb.linearVelocity;
        bool reflected = false;

        if (position.x > center.x + halfX - boundsRadius)
        {
            position.x = center.x + halfX - boundsRadius;
            velocity.x = -Mathf.Abs(velocity.x);
            reflected = true;
        }
        else if (position.x < center.x - halfX + boundsRadius)
        {
            position.x = center.x - halfX + boundsRadius;
            velocity.x = Mathf.Abs(velocity.x);
            reflected = true;
        }

        if (position.y > center.y + halfY - boundsRadius)
        {
            position.y = center.y + halfY - boundsRadius;
            velocity.y = -Mathf.Abs(velocity.y);
            reflected = true;
        }
        else if (position.y < center.y - halfY + boundsRadius)
        {
            position.y = center.y - halfY + boundsRadius;
            velocity.y = Mathf.Abs(velocity.y);
            reflected = true;
        }

        if (!reflected)
            return;

        rb.position = position;
        rb.linearVelocity = velocity;

        if (velocity.sqrMagnitude > 0.0001f)
        {
            cruiseDirection = velocity.normalized;
        }

        if (Random.value < CollisionSpinFlipChance)
        {
            rb.angularVelocity = Mathf.Clamp(-rb.angularVelocity * 0.6f, -MaxBoundaryAngularSpeed, MaxBoundaryAngularSpeed);
            baseAngularSpeed *= -1f;
        }
    }

    void WrapAcrossMapBounds()
    {
        Vector2 position = rb.position;
        MapInstanceService.TryGetBoundsForWorldPosition(position, out MapInstanceService.BoundsInfo bounds);
        Vector2 mapSize = bounds.Size;
        Vector2 center = bounds.Center;
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        float boundsRadius = GetBoundsRadius();
        bool wrapped = false;

        if (position.x > center.x + halfX + boundsRadius)
        {
            position.x = center.x - halfX - boundsRadius;
            wrapped = true;
        }
        else if (position.x < center.x - halfX - boundsRadius)
        {
            position.x = center.x + halfX + boundsRadius;
            wrapped = true;
        }

        if (position.y > center.y + halfY + boundsRadius)
        {
            position.y = center.y - halfY - boundsRadius;
            wrapped = true;
        }
        else if (position.y < center.y - halfY - boundsRadius)
        {
            position.y = center.y + halfY + boundsRadius;
            wrapped = true;
        }

        if (!wrapped)
            return;

        rb.position = position;
    }

    float GetBoundsRadius()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        float maxExtent = 0.5f;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D current = colliders[i];
            if (current == null || !current.enabled)
                continue;

            Bounds bounds = current.bounds;
            maxExtent = Mathf.Max(maxExtent, bounds.extents.x, bounds.extents.y);
        }

        return maxExtent;
    }
}
