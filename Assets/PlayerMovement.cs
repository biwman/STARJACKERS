using UnityEngine;
using System;
using Photon.Pun;
using UnityEngine.Rendering;

public class PlayerMovement : MonoBehaviourPun
{
    public static bool gameStarted = false;

    private Rigidbody2D rb;
    public float speed = 5f;
    public float depletedSpeedMultiplier = 0.7f;
    public float boosterDuration = 5f;
    public float maxSpeedThreshold = 0.9f;
    public float fullSpeedSnapThreshold = 0.92f;
    public float boosterRecoveryThreshold = 0.2f;

    public Joystick joystick;
    public Joystick shootJoystick;

    private Vector2 moveInput;
    private Vector2 shootInput;
    private Vector2 effectiveMoveInput;
    private Vector2 lastFacingDirection = Vector2.up;
    private float boosterCharge = 1f;
    private bool boosterExhausted = false;
    private float targetRotationAngle = 0f;
    private float boosterRecoveryDelayTimer = 0f;
    private AudioSource engineAudioSource;
    private Vector3 lastAudioPosition;
    float baseSpeed = 5f;
    float baseBoosterDuration = 5f;
    float turnRateMultiplier = 1f;
    bool baseSpeedCaptured;
    bool fusionEngineEquipped;
    string lastAppliedEngineSignature = string.Empty;
    const float BaseTurnDegreesPerSecond = 1080f;
    const float AccelerationResponsiveness = 18f;
    const float LowSpeedBrakeResponsiveness = 7.4f;
    const float HighSpeedBrakeResponsiveness = 1.15f;
    const float BrakeDriftResponsivenessMultiplier = 0.8f;
    const float MaxDriftInertiaSlowdown = 4.2f;
    const float MovingObjectImpulseRequestCooldown = 0.08f;
    const float RemotePushNoseProbeRadius = 0.9f;
    const float RemotePushBodyProbeRadius = 0.68f;
    static PhysicsMaterial2D playerCollisionMaterial;
    static readonly Collider2D[] RemotePushProbeHits = new Collider2D[32];
    float nextMovingObjectImpulseRequestTime;

    public float BoosterNormalized => boosterCharge;
    public bool IsBoosterDepleted => boosterExhausted;
    public bool HasFusionEngineEquipped => fusionEngineEquipped;
    public float CurrentSpeedReference => speed;
    float CurrentDepletedSpeedMultiplier => 1f - (RoomSettings.GetBoosterSlowdownPercent() / 100f);

    void Start()
    {
        EnsureBotBootstrap();

        bool isAstronaut = GetComponent<AstronautSurvivor>() != null || AstronautSurvivor.IsAstronautInstantiationData(photonView.InstantiationData);
        if (isAstronaut)
        {
            AstronautSurvivor astronaut = GetComponent<AstronautSurvivor>();
            if (astronaut == null)
                astronaut = gameObject.AddComponent<AstronautSurvivor>();

            astronaut.InitializeFromPhotonData();
        }

        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.angularVelocity = 0f;
        }

        ApplyPlayerCollisionMaterial();

        if (GetComponent<HideInNebulaTarget>() == null)
        {
            gameObject.AddComponent<HideInNebulaTarget>();
        }

        if (!isAstronaut && GetComponent<EnemyBot>() == null && GetComponent<PlayerNicknameUI>() == null)
        {
            gameObject.AddComponent<PlayerNicknameUI>();
        }

        if (GetComponent<EngineThrusterVFX>() == null)
        {
            gameObject.AddComponent<EngineThrusterVFX>();
        }

        targetRotationAngle = transform.eulerAngles.z;

        CaptureBaseMovementProfile();
        SetupEngineAudio();
        SyncEquippedEngineProfile(forceRefresh: true);
        lastAudioPosition = transform.position;

        if (GetComponent<EnemyBot>() != null)
            return;

        if (!photonView.IsMine)
            return;

        CameraFollow cam = FindAnyObjectByType<CameraFollow>();
        if (cam != null)
        {
            cam.target = transform;
        }

        ResolveJoysticks();

        if (!isAstronaut && GetComponent<BoosterBarUI>() == null)
        {
            gameObject.AddComponent<BoosterBarUI>();
        }

        if (!isAstronaut && GetComponent<ShipInventoryHudUI>() == null)
        {
            gameObject.AddComponent<ShipInventoryHudUI>();
        }
    }

    void Update()
    {
        EnsureBotBootstrap();

        if (GetComponent<EnemyBot>() != null)
        {
            UpdateEngineAudio();
            return;
        }

        SyncEquippedEngineProfile();

        if (photonView.IsMine)
        {
            if (!IsGameStarted())
            {
                moveInput = Vector2.zero;
                shootInput = Vector2.zero;
                effectiveMoveInput = Vector2.zero;
                if (rb != null)
                {
                    rb.angularVelocity = 0f;
                }
                UpdateEngineAudio();
                return;
            }

            ResolveJoysticks();

            moveInput = joystick != null && joystick.IsPressed ? joystick.inputVector : Vector2.zero;
            shootInput = shootJoystick != null && shootJoystick.IsPressed ? shootJoystick.inputVector : Vector2.zero;

            if (moveInput.magnitude < 0.2f)
                moveInput = Vector2.zero;

            if (shootInput.magnitude < 0.3f)
                shootInput = Vector2.zero;

            effectiveMoveInput = GetEffectiveMoveInput(moveInput);

            UpdateBooster(Time.deltaTime);
            UpdateFacingDirection();
        }

        UpdateEngineAudio();
    }

    void FixedUpdate()
    {
        if (GetComponent<EnemyBot>() != null)
            return;

        if (!photonView.IsMine)
            return;

        if (!IsGameStarted())
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            return;
        }

        float currentSpeed = IsBoosterDepleted ? speed * CurrentDepletedSpeedMultiplier : speed;
        if (rb != null)
        {
            ApplyVelocity(currentSpeed);
            ClampExcessCollisionBoost(currentSpeed);
            TryRequestNearbyMovingObjectImpulse();
            rb.angularVelocity = 0f;
            float maxTurnDelta = BaseTurnDegreesPerSecond * Mathf.Max(0.1f, turnRateMultiplier) * Time.fixedDeltaTime;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetRotationAngle, maxTurnDelta);
            rb.MoveRotation(nextAngle);
        }
    }

    void OnDisable()
    {
        StopEngineAudioImmediately();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!photonView.IsMine)
            return;

        Debug.Log("DOTKNALEM: " + other.name);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!photonView.IsMine || rb == null)
            return;

        float currentSpeed = IsBoosterDepleted ? speed * CurrentDepletedSpeedMultiplier : speed;
        ClampExcessCollisionBoost(currentSpeed);
        TryRequestMovingObjectImpulse(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (!photonView.IsMine || rb == null)
            return;

        TryRequestMovingObjectImpulse(collision);
    }

    void ClampExcessCollisionBoost(float currentSpeed)
    {
        if (rb == null)
            return;

        float expectedTopSpeed = currentSpeed * Mathf.Max(1f, GetMaxInputSpeedBoostMultiplier());
        float hardCap = expectedTopSpeed * 1.22f;
        float currentMagnitude = rb.linearVelocity.magnitude;

        if (currentMagnitude <= hardCap || currentMagnitude <= 0.001f)
            return;

        rb.linearVelocity = rb.linearVelocity.normalized * hardCap;
    }

    void TryRequestMovingObjectImpulse(Collision2D collision)
    {
        if (PhotonNetwork.IsMasterClient || collision == null || GetComponent<AstronautSurvivor>() != null)
            return;

        MovingSpaceObject movingObject = collision.collider != null
            ? collision.collider.GetComponentInParent<MovingSpaceObject>()
            : null;

        Vector2 playerVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
        if (RequestMovingObjectImpulse(movingObject, playerVelocity, 1.25f))
            return;

        DroppedCargoCrate crate = collision.collider != null
            ? collision.collider.GetComponentInParent<DroppedCargoCrate>()
            : null;
        RequestDroppedCargoImpulse(crate, playerVelocity, 0.65f);
    }

    void TryRequestNearbyMovingObjectImpulse()
    {
        if (!PhotonNetwork.IsConnected ||
            PhotonNetwork.IsMasterClient ||
            rb == null ||
            GetComponent<AstronautSurvivor>() != null ||
            Time.time < nextMovingObjectImpulseRequestTime)
        {
            return;
        }

        Vector2 playerVelocity = rb.linearVelocity;
        if (playerVelocity.sqrMagnitude < 0.01f)
            return;

        Vector2 noseProbeCenter = rb.position + (Vector2)transform.up * GetRemotePushProbeDistance();
        MovingSpaceObject bestObject = null;
        DroppedCargoCrate bestCrate = null;
        float bestDistance = float.MaxValue;

        FindRemotePushTarget(noseProbeCenter, RemotePushNoseProbeRadius, ref bestObject, ref bestCrate, ref bestDistance);
        FindRemotePushTarget(rb.position, RemotePushBodyProbeRadius, ref bestObject, ref bestCrate, ref bestDistance);

        if (RequestMovingObjectImpulse(bestObject, playerVelocity, bestObject != null && bestObject.ObjectType == MovingSpaceObject.SpaceObjectType.Treasure ? 1.45f : 0.95f))
            return;

        RequestDroppedCargoImpulse(bestCrate, playerVelocity, 0.75f);
    }

    void FindRemotePushTarget(
        Vector2 probeCenter,
        float probeRadius,
        ref MovingSpaceObject bestObject,
        ref DroppedCargoCrate bestCrate,
        ref float bestDistance)
    {
        ContactFilter2D contactFilter = new ContactFilter2D { useTriggers = false };
        int hitCount = Physics2D.OverlapCircle(probeCenter, probeRadius, contactFilter, RemotePushProbeHits);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = RemotePushProbeHits[i];
            RemotePushProbeHits[i] = null;
            if (hit == null || hit.isTrigger)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            float distance = Vector2.Distance(probeCenter, hit.ClosestPoint(probeCenter));

            MovingSpaceObject movingObject = hit.GetComponentInParent<MovingSpaceObject>();
            if (movingObject != null &&
                !string.IsNullOrWhiteSpace(movingObject.StableId) &&
                !(movingObject.ObjectType == MovingSpaceObject.SpaceObjectType.Obstacle && RoomSettings.IsObstacleMassMax()) &&
                distance < bestDistance)
            {
                bestDistance = distance;
                bestObject = movingObject;
                bestCrate = null;
                continue;
            }

            DroppedCargoCrate crate = hit.GetComponentInParent<DroppedCargoCrate>();
            if (crate != null && distance < bestDistance)
            {
                bestDistance = distance;
                bestObject = null;
                bestCrate = crate;
            }
        }
    }

    float GetRemotePushProbeDistance()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            return 0.78f;

        return Mathf.Clamp(renderer.bounds.extents.y * 0.92f, 0.45f, 1.35f);
    }

    bool RequestMovingObjectImpulse(MovingSpaceObject movingObject, Vector2 playerVelocity, float strength)
    {
        if (movingObject == null || string.IsNullOrWhiteSpace(movingObject.StableId))
            return false;

        if (Time.time < nextMovingObjectImpulseRequestTime)
            return false;

        if (movingObject.ObjectType == MovingSpaceObject.SpaceObjectType.Obstacle && RoomSettings.IsObstacleMassMax())
            return false;

        if (playerVelocity.sqrMagnitude < 0.01f)
            return false;

        int weightFactor = movingObject.ObjectType == MovingSpaceObject.SpaceObjectType.Obstacle
            ? RoomSettings.GetObstacleWeightFactor()
            : RoomSettings.GetTreasureWeightFactor();
        weightFactor = Mathf.Max(1, weightFactor);

        Vector2 impulse = (playerVelocity * Mathf.Max(0.1f, strength)) / weightFactor;
        nextMovingObjectImpulseRequestTime = Time.time + MovingObjectImpulseRequestCooldown;
        movingObject.ApplyRemotePushPrediction(impulse);
        SpaceObjectMotionSync.RequestImpulse(movingObject.StableId, impulse);
        return true;
    }

    bool RequestDroppedCargoImpulse(DroppedCargoCrate crate, Vector2 playerVelocity, float strength)
    {
        if (crate == null || Time.time < nextMovingObjectImpulseRequestTime)
            return false;

        if (playerVelocity.sqrMagnitude < 0.01f)
            return false;

        if (!crate.TryRequestRemoteImpulse(playerVelocity * Mathf.Max(0.1f, strength)))
            return false;

        nextMovingObjectImpulseRequestTime = Time.time + MovingObjectImpulseRequestCooldown;
        return true;
    }

    bool IsGameStarted()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value))
        {
            return (bool)value;
        }

        return false;
    }

    void UpdateBooster(float deltaTime)
    {
        if (GetComponent<AstronautSurvivor>() != null)
        {
            boosterCharge = 0f;
            boosterExhausted = false;
            boosterRecoveryDelayTimer = 0f;
            return;
        }

        bool usingFullAcceleration = effectiveMoveInput.magnitude >= maxSpeedThreshold;
        bool usingBooster = usingFullAcceleration && !boosterExhausted;

        if (usingBooster)
        {
            boosterCharge -= deltaTime / boosterDuration;
            boosterRecoveryDelayTimer = GetCurrentBoosterRecoveryDelay();
        }
        else if (usingFullAcceleration)
        {
            boosterRecoveryDelayTimer = GetCurrentBoosterRecoveryDelay();
        }
        else if (boosterRecoveryDelayTimer > 0f)
        {
            boosterRecoveryDelayTimer -= deltaTime;
        }
        else
        {
            boosterCharge += deltaTime / (boosterDuration * 2f);
        }

        boosterCharge = Mathf.Clamp01(boosterCharge);

        if (!boosterExhausted && boosterCharge <= 0.001f)
        {
            boosterExhausted = true;
            boosterRecoveryDelayTimer = GetCurrentBoosterRecoveryDelay();
        }
        else if (boosterExhausted && boosterCharge >= boosterRecoveryThreshold)
        {
            boosterExhausted = false;
        }
    }

    Vector2 GetEffectiveMoveInput(Vector2 rawInput)
    {
        if (rawInput == Vector2.zero)
            return Vector2.zero;

        if (rawInput.magnitude >= fullSpeedSnapThreshold)
            return rawInput.normalized * GetMaxInputSpeedBoostMultiplier();

        return rawInput;
    }

    float GetMaxInputSpeedBoostMultiplier()
    {
        if (boosterCharge < 0.01f)
            return 1f;

        return 1f + (RoomSettings.GetMaxInputBoostPercent() / 100f);
    }

    void ApplyVelocity(float currentSpeed)
    {
        Vector2 targetVelocity = effectiveMoveInput * currentSpeed;
        int driftLevel = RoomSettings.GetShipDriftLevel();

        if (driftLevel <= 0)
        {
            rb.linearVelocity = targetVelocity;
            return;
        }

        Vector2 currentVelocity = rb.linearVelocity;
        float currentMagnitude = currentVelocity.magnitude;
        float targetMagnitude = targetVelocity.magnitude;
        float speedRatio = Mathf.Clamp01(currentMagnitude / Mathf.Max(currentSpeed, 0.001f));

        bool braking = targetMagnitude + 0.01f < currentMagnitude || effectiveMoveInput == Vector2.zero;

        if (braking)
        {
            float driftWeight = speedRatio * speedRatio;
            float driftSlowdown = GetDriftSlowdownMultiplier(driftLevel);
            float brakeResponsiveness = Mathf.Lerp(LowSpeedBrakeResponsiveness, HighSpeedBrakeResponsiveness, driftWeight) *
                                        BrakeDriftResponsivenessMultiplier *
                                        driftSlowdown;
            float releaseDriftMultiplier = effectiveMoveInput == Vector2.zero ? 0.86f : 1f;
            float maxDelta = brakeResponsiveness * speed * releaseDriftMultiplier * Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, maxDelta);
            return;
        }

        float accelerationDelta = AccelerationResponsiveness * speed * GetDriftSlowdownMultiplier(driftLevel) * Time.fixedDeltaTime;
        rb.linearVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, accelerationDelta);
    }

    float GetDriftSlowdownMultiplier(int driftLevel)
    {
        if (driftLevel <= 1)
            return 1f;

        float t = Mathf.InverseLerp(1f, 10f, driftLevel);
        float slowdown = Mathf.Lerp(1f, MaxDriftInertiaSlowdown, t);
        return 1f / slowdown;
    }

    void UpdateFacingDirection()
    {
        Vector2 desiredDirection = Vector2.zero;

        if (shootInput.sqrMagnitude > 0.09f)
        {
            desiredDirection = shootInput.normalized;
        }
        else if (moveInput.sqrMagnitude > 0.09f)
        {
            desiredDirection = moveInput.normalized;
        }

        if (desiredDirection == Vector2.zero)
            return;

        lastFacingDirection = desiredDirection;

        float angle = Mathf.Atan2(lastFacingDirection.y, lastFacingDirection.x) * Mathf.Rad2Deg;
        if (GetComponent<AstronautSurvivor>() != null)
            angle += 180f;
        targetRotationAngle = angle - 90f;

        if (rb == null)
        {
            float maxTurnDelta = BaseTurnDegreesPerSecond * Mathf.Max(0.1f, turnRateMultiplier) * Time.deltaTime;
            float nextAngle = Mathf.MoveTowardsAngle(transform.eulerAngles.z, targetRotationAngle, maxTurnDelta);
            transform.rotation = Quaternion.Euler(0f, 0f, nextAngle);
        }
    }

    void ResolveJoysticks()
    {
        if (joystick == null)
        {
            GameObject movementJoystick = GameObject.Find("JoystickBG");
            if (movementJoystick != null)
            {
                joystick = movementJoystick.GetComponent<Joystick>();
            }
        }

        if (shootJoystick == null)
        {
            GameObject shootingJoystick = GameObject.Find("ShootJoystickBG");
            if (shootingJoystick != null)
            {
                shootJoystick = shootingJoystick.GetComponent<Joystick>();
            }
        }
    }

    void SetupEngineAudio()
    {
        if (GetComponent<AstronautSurvivor>() != null)
            return;

        AudioClip engineClip = ResolveEngineAudioClip();
        if (engineClip == null)
            return;

        engineAudioSource = GetComponent<AudioSource>();
        if (engineAudioSource == null)
        {
            engineAudioSource = gameObject.AddComponent<AudioSource>();
        }

        engineAudioSource.clip = engineClip;
        engineAudioSource.loop = true;
        engineAudioSource.playOnAwake = false;
        AudioManager.Instance.ConfigureSpatialSource(engineAudioSource, 0f);
        engineAudioSource.loop = true;
        engineAudioSource.playOnAwake = false;
        engineAudioSource.volume = 0f;
        engineAudioSource.pitch = 0.85f;
    }

    void UpdateEngineAudio()
    {
        if (engineAudioSource == null)
            return;

        if (!IsGameStarted())
        {
            if (engineAudioSource.isPlaying)
                engineAudioSource.Stop();

            return;
        }

        float speedReference = photonView.IsMine && IsBoosterDepleted ? speed * CurrentDepletedSpeedMultiplier : speed;
        if (speedReference <= 0.001f)
            speedReference = speed;

        float normalizedSpeed = GetAudioSpeedRatio(speedReference);

        SyncEngineAudioClip();

        if (!engineAudioSource.isPlaying)
            engineAudioSource.Play();

        engineAudioSource.volume = Mathf.Lerp(0.12f, 0.42f, normalizedSpeed);
        engineAudioSource.pitch = Mathf.Lerp(0.88f, 1.24f, normalizedSpeed);
    }

    public void StopEngineAudioImmediately()
    {
        if (engineAudioSource != null && engineAudioSource.isPlaying)
            engineAudioSource.Stop();
    }

    float GetAudioSpeedRatio(float speedReference)
    {
        if (photonView.IsMine)
        {
            if (rb != null && speedReference > 0.001f)
            {
                return Mathf.Clamp01(rb.linearVelocity.magnitude / speedReference);
            }

            return 0f;
        }

        float delta = Time.unscaledDeltaTime > 0.0001f
            ? Vector3.Distance(transform.position, lastAudioPosition) / Time.unscaledDeltaTime
            : 0f;
        lastAudioPosition = transform.position;
        return Mathf.Clamp01(delta / speedReference);
    }

    void EnsureBotBootstrap()
    {
        if (!EnemyBot.IsBotInstantiationData(photonView != null ? photonView.InstantiationData : null))
            return;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot == null)
            bot = gameObject.AddComponent<EnemyBot>();

        bot.InitializeFromPhotonData();
    }

    void CaptureBaseMovementProfile()
    {
        if (baseSpeedCaptured)
            return;

        baseSpeed = Mathf.Max(0.1f, speed);
        baseBoosterDuration = Mathf.Max(0.1f, boosterDuration);
        baseSpeedCaptured = true;
    }

    void SyncEquippedEngineProfile(bool forceRefresh = false)
    {
        if (GetComponent<EnemyBot>() != null || GetComponent<AstronautSurvivor>() != null)
            return;

        CaptureBaseMovementProfile();

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        int fusionCount = CountEquippedFusionEngines(equipmentSlots, shipSkinIndex);
        bool hasFusion = fusionCount > 0;
        string signature = shipSkinIndex + ":" + fusionCount;
        if (!forceRefresh && signature == lastAppliedEngineSignature)
            return;

        float baseShipSpeed = ShipCatalog.GetBaseSpeed(shipSkinIndex);
        float baseShipBoosterDuration = ShipCatalog.GetBoosterDuration(shipSkinIndex);
        float baseShipTurnRate = ShipCatalog.GetTurnRateMultiplier(shipSkinIndex);

        fusionEngineEquipped = hasFusion;
        baseSpeed = Mathf.Max(0.1f, baseShipSpeed);
        baseBoosterDuration = Mathf.Max(0.1f, baseShipBoosterDuration);
        turnRateMultiplier = Mathf.Max(0.1f, baseShipTurnRate);
        speed = baseSpeed * (fusionEngineEquipped ? 1.2f : 1f);
        boosterDuration = baseBoosterDuration;
        lastAppliedEngineSignature = signature;
        SyncEngineAudioClip();

        EngineThrusterVFX thrusterVfx = GetComponent<EngineThrusterVFX>();
        if (thrusterVfx != null)
            thrusterVfx.RefreshMode();
    }

    int CountEquippedFusionEngines(string[] equipmentSlots, int shipSkinIndex)
    {
        if (equipmentSlots == null)
            return 0;

        int count = 0;
        if (ShipCatalog.IsEquipmentSlotEnabled(4, shipSkinIndex) &&
            string.Equals(GetEquipmentItem(equipmentSlots, 4), InventoryItemCatalog.FusionEngineId, StringComparison.Ordinal))
        {
            count++;
        }

        if (ShipCatalog.IsEquipmentSlotEnabled(5, shipSkinIndex) &&
            string.Equals(GetEquipmentItem(equipmentSlots, 5), InventoryItemCatalog.FusionEngineId, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    static string GetEquipmentItem(string[] equipmentSlots, int index)
    {
        return equipmentSlots != null && index >= 0 && index < equipmentSlots.Length
            ? equipmentSlots[index]
            : null;
    }

    float GetCurrentBoosterRecoveryDelay()
    {
        float baseDelay = RoomSettings.GetBoosterRecoveryDelay();
        return Mathf.Max(0f, baseDelay - (fusionEngineEquipped ? 2f : 0f));
    }

    AudioClip ResolveEngineAudioClip()
    {
        EnemyBot enemyBot = GetComponent<EnemyBot>();
        if (enemyBot != null && enemyBot.Kind == EnemyBotKind.Mothership)
            return AudioManager.Instance.MothershipEngineClip;

        return fusionEngineEquipped ? AudioManager.Instance.FusionEngineClip : AudioManager.Instance.EngineClip;
    }

    void SyncEngineAudioClip()
    {
        if (engineAudioSource == null)
            return;

        AudioClip desiredClip = ResolveEngineAudioClip();
        if (desiredClip == null || engineAudioSource.clip == desiredClip)
            return;

        bool wasPlaying = engineAudioSource.isPlaying;
        engineAudioSource.Stop();
        engineAudioSource.clip = desiredClip;
        if (wasPlaying)
            engineAudioSource.Play();
    }

    void ApplyPlayerCollisionMaterial()
    {
        if (playerCollisionMaterial == null)
        {
            playerCollisionMaterial = new PhysicsMaterial2D("PlayerShipCollision")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D currentCollider = colliders[i];
            if (currentCollider != null)
            {
                currentCollider.sharedMaterial = playerCollisionMaterial;
            }
        }
    }
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class EngineThrusterVFX : MonoBehaviour
{
    const string ThrusterRootName = "EngineVFX";

    Rigidbody2D rb;
    SpriteRenderer shipRenderer;
    TrailRenderer[] trailRenderers;
    float referenceSpeed = 5f;
    bool isEnemyBot;
    bool isAstronaut;
    bool fusionTrailEquipped;
    EnemyTrailProfile enemyTrailProfile;
    Vector2[] playerThrusterOffsetFactors = new[] { new Vector2(0f, 0.02f) };

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        shipRenderer = GetComponent<SpriteRenderer>();
        PlayerMovement movement = GetComponent<PlayerMovement>();
        referenceSpeed = Mathf.Max(1f, movement != null ? movement.speed : 5f);
        RefreshMode();
        CreateThrusterObjects();
        UpdateVisuals(0f);
    }

    public void RefreshMode()
    {
        EnemyBot enemyBot = GetComponent<EnemyBot>();
        isEnemyBot = enemyBot != null;
        enemyTrailProfile = enemyBot != null && enemyBot.Definition != null ? enemyBot.Definition.Trails : null;
        isAstronaut = GetComponent<AstronautSurvivor>() != null;
        PlayerMovement movement = GetComponent<PlayerMovement>();
        fusionTrailEquipped = movement != null && movement.HasFusionEngineEquipped && !isEnemyBot && !isAstronaut;

        PhotonView view = GetComponent<PhotonView>();
        int skinIndex = view != null && view.Owner != null ? RoomSettings.GetPlayerShipSkin(view.Owner, 0) : 0;
        playerThrusterOffsetFactors = !isEnemyBot && !isAstronaut
            ? ShipCatalog.GetThrusterOffsetFactors(skinIndex)
            : new[] { new Vector2(0f, 0.02f) };

        ReapplyTrailAppearance();
    }

    void Update()
    {
        if (rb == null)
            return;

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
            referenceSpeed = Mathf.Max(1f, movement.CurrentSpeedReference);

        bool desiredFusionTrail = movement != null && movement.HasFusionEngineEquipped && !isEnemyBot && !isAstronaut;
        if (desiredFusionTrail != fusionTrailEquipped)
        {
            fusionTrailEquipped = desiredFusionTrail;
            ReapplyTrailAppearance();
        }

        float speedNormalized = Mathf.InverseLerp(0.02f, referenceSpeed, rb.linearVelocity.magnitude);
        UpdateVisuals(speedNormalized);
    }

    void CreateThrusterObjects()
    {
        Transform existing = transform.Find(ThrusterRootName);
        GameObject rootObject = existing != null ? existing.gameObject : new GameObject(ThrusterRootName);
        rootObject.transform.SetParent(transform, false);

        for (int i = rootObject.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(rootObject.transform.GetChild(i).gameObject);
        }

        float shipHeight = shipRenderer != null ? shipRenderer.bounds.size.y : 1f;
        float shipWidth = shipRenderer != null ? shipRenderer.bounds.size.x : 1f;
        rootObject.transform.localPosition = enemyTrailProfile != null
            ? new Vector3(enemyTrailProfile.RootOffsetFactors.x * shipWidth, enemyTrailProfile.RootOffsetFactors.y * shipHeight, 0f)
            : isEnemyBot
                ? new Vector3(0f, shipHeight * 0.44f, 0f)
            : isAstronaut
                ? new Vector3(0f, -shipHeight * 0.34f, 0f)
                : new Vector3(0f, -shipHeight * 0.46f, 0f);
        rootObject.transform.localRotation = enemyTrailProfile != null
            ? Quaternion.Euler(0f, 0f, enemyTrailProfile.RootRotationZ)
            : isEnemyBot
                ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, 180f);

        Vector3[] offsets = enemyTrailProfile != null
            ? BuildEnemyTrailOffsets(enemyTrailProfile, shipWidth, shipHeight)
            : BuildPlayerTrailOffsets(shipWidth, shipHeight);

        trailRenderers = new TrailRenderer[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject trailObject = new GameObject("EngineTrail" + i);
            trailObject.transform.SetParent(rootObject.transform, false);
            trailObject.transform.localPosition = offsets[i];
            trailRenderers[i] = trailObject.AddComponent<TrailRenderer>();
            ConfigureTrail(trailRenderers[i]);
        }
    }

    void ConfigureTrail(TrailRenderer trail)
    {
        trail.time = isAstronaut ? 0.24f : enemyTrailProfile != null ? enemyTrailProfile.MaxTrailTime : 0.42f;
        trail.minVertexDistance = 0.01f;
        trail.widthMultiplier = isAstronaut ? 0.04f : enemyTrailProfile != null ? enemyTrailProfile.MaxTrailWidth : 0.08f;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.numCapVertices = 12;
        trail.numCornerVertices = 8;
        trail.material = CreateSpritesMaterial();
        trail.generateLightingData = false;
        ApplyTrailAppearance(trail);
    }

    Vector3[] BuildPlayerTrailOffsets(float shipWidth, float shipHeight)
    {
        Vector2[] factors = playerThrusterOffsetFactors != null && playerThrusterOffsetFactors.Length > 0
            ? playerThrusterOffsetFactors
            : new[] { new Vector2(0f, 0.02f) };

        Vector3[] offsets = new Vector3[factors.Length];
        for (int i = 0; i < factors.Length; i++)
        {
            offsets[i] = new Vector3(factors[i].x * shipWidth, factors[i].y * shipHeight, 0f);
        }

        return offsets;
    }

    void ApplyTrailAppearance(TrailRenderer trail)
    {
        if (trail == null)
            return;

        Gradient gradient = new Gradient();
        if (isAstronaut)
        {
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.94f, 0.76f), 0f),
                    new GradientColorKey(new Color(1f, 0.7f, 0.3f), 0.2f),
                    new GradientColorKey(new Color(0.96f, 0.42f, 0.12f), 0.55f),
                    new GradientColorKey(new Color(0.42f, 0.11f, 0.02f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.78f, 0f),
                    new GradientAlphaKey(0.44f, 0.26f),
                    new GradientAlphaKey(0.18f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
        else if (enemyTrailProfile != null && enemyTrailProfile.VisualStyle == EnemyTrailVisualStyle.RedLarge)
        {
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.94f, 0.9f), 0f),
                    new GradientColorKey(new Color(1f, 0.32f, 0.22f), 0.18f),
                    new GradientColorKey(new Color(0.9f, 0.04f, 0.04f), 0.58f),
                    new GradientColorKey(new Color(0.34f, 0.01f, 0.01f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.96f, 0f),
                    new GradientAlphaKey(0.72f, 0.24f),
                    new GradientAlphaKey(0.28f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
        else if (enemyTrailProfile != null && enemyTrailProfile.VisualStyle == EnemyTrailVisualStyle.GreenTwin)
        {
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.86f, 1f, 0.72f), 0f),
                    new GradientColorKey(new Color(0.22f, 1f, 0.36f), 0.18f),
                    new GradientColorKey(new Color(0.02f, 0.72f, 0.18f), 0.58f),
                    new GradientColorKey(new Color(0f, 0.18f, 0.04f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.96f, 0f),
                    new GradientAlphaKey(0.72f, 0.24f),
                    new GradientAlphaKey(0.28f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
        else if (fusionTrailEquipped)
        {
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.84f, 0.72f, 1f), 0f),
                    new GradientColorKey(new Color(0.48f, 0.18f, 0.76f), 0.18f),
                    new GradientColorKey(new Color(0.22f, 0.04f, 0.42f), 0.54f),
                    new GradientColorKey(new Color(0.06f, 0.01f, 0.14f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.92f, 0f),
                    new GradientAlphaKey(0.62f, 0.26f),
                    new GradientAlphaKey(0.24f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
        else if (isEnemyBot)
        {
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.98f, 0.78f), 0f),
                    new GradientColorKey(new Color(1f, 0.7f, 0.22f), 0.18f),
                    new GradientColorKey(new Color(0.95f, 0.34f, 0.04f), 0.52f),
                    new GradientColorKey(new Color(0.45f, 0.08f, 0.01f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.94f, 0f),
                    new GradientAlphaKey(0.7f, 0.24f),
                    new GradientAlphaKey(0.24f, 0.68f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
        else
        {
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(0.64f, 0.97f, 1f), 0.14f),
                    new GradientColorKey(new Color(0.2f, 0.8f, 1f), 0.45f),
                    new GradientColorKey(new Color(0.03f, 0.18f, 0.86f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.66f, 0.2f),
                    new GradientAlphaKey(0.26f, 0.62f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
        trail.colorGradient = gradient;
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.12f, 0.82f),
            new Keyframe(0.6f, 0.3f),
            new Keyframe(1f, 0f));

        if (shipRenderer != null)
        {
            trail.sortingLayerID = shipRenderer.sortingLayerID;
            trail.sortingOrder = shipRenderer.sortingOrder - 2;
        }
    }

    void ReapplyTrailAppearance()
    {
        if (trailRenderers == null)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            ApplyTrailAppearance(trailRenderers[i]);
        }
    }

    public void DisableAndClearTrails()
    {
        if (trailRenderers == null)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            if (trailRenderers[i] == null)
                continue;

            trailRenderers[i].emitting = false;
            trailRenderers[i].Clear();
            trailRenderers[i].gameObject.SetActive(false);
        }

        enabled = false;
    }

    void UpdateVisuals(float speedNormalized)
    {
        HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>();
        PhotonView view = GetComponent<PhotonView>();
        bool hideForOthers = nebulaTarget != null && nebulaTarget.IsHiddenFromLocalPlayer() && view != null && !view.IsMine;
        if (hideForOthers)
        {
            DisableEmission();
            return;
        }

        float clamped = Mathf.Clamp01(speedNormalized);
        float intensity = Mathf.Lerp(isAstronaut ? 0.28f : 0.18f, 1f, clamped);

        if (trailRenderers == null)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trailRenderer = trailRenderers[i];
            if (trailRenderer == null)
                continue;

            if (isAstronaut)
            {
                trailRenderer.time = Mathf.Lerp(0.12f, 0.28f, intensity);
                trailRenderer.widthMultiplier = Mathf.Lerp(0.015f, 0.055f, intensity);
                trailRenderer.emitting = clamped > 0.08f;
            }
            else if (enemyTrailProfile != null)
            {
                trailRenderer.time = Mathf.Lerp(enemyTrailProfile.MinTrailTime, enemyTrailProfile.MaxTrailTime, intensity);
                trailRenderer.widthMultiplier = Mathf.Lerp(enemyTrailProfile.MinTrailWidth, enemyTrailProfile.MaxTrailWidth, intensity);
                trailRenderer.emitting = clamped > enemyTrailProfile.EmissionThreshold;
            }
            else
            {
                float trailLengthMultiplier = fusionTrailEquipped ? 1.5f : 1f;
                trailRenderer.time = Mathf.Lerp(0.22f, 0.82f, intensity) * trailLengthMultiplier;
                trailRenderer.widthMultiplier = Mathf.Lerp(0.03f, 0.16f, intensity);
                trailRenderer.emitting = clamped > 0.04f;
            }
        }
    }

    Vector3[] BuildEnemyTrailOffsets(EnemyTrailProfile profile, float shipWidth, float shipHeight)
    {
        if (profile != null && profile.TrailOffsetFactors != null && profile.TrailOffsetFactors.Length == 0)
            return System.Array.Empty<Vector3>();

        if (profile == null || profile.TrailOffsetFactors == null)
            return new[] { Vector3.zero };

        Vector3[] offsets = new Vector3[profile.TrailOffsetFactors.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = new Vector3(
                profile.TrailOffsetFactors[i].x * shipWidth,
                profile.TrailOffsetFactors[i].y * shipHeight,
                0f);
        }

        return offsets;
    }

    void DisableEmission()
    {
        if (trailRenderers == null)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trailRenderer = trailRenderers[i];
            if (trailRenderer == null)
                continue;

            trailRenderer.emitting = false;
            trailRenderer.Clear();
        }
    }

    Material CreateSpritesMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        material.name = "EngineThrusterVFXMaterial";
        return material;
    }
}
