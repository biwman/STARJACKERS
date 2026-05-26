using UnityEngine;
using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine.Rendering;

public class PlayerMovement : MonoBehaviourPun
{
    public static bool gameStarted = false;

    private Rigidbody2D rb;
    bool networkBodyAuthorityApplied;
    bool lastNetworkBodyAuthority;
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
    int maxBoostPercent = 30;
    bool baseSpeedCaptured;
    bool fusionEngineEquipped;
    int equippedFusionEngineCount;
    int equippedFuelTankCount;
    int equippedAfterburnerStabilizerCount;
    string lastAppliedEngineSignature = string.Empty;
    float pilotSpeedBoostMultiplier = 1f;
    float pilotSpeedBoostUntil = -1f;
    float superBoosterUntil = -1f;
    Vector2 superBoosterDirection = Vector2.up;
    const float BaseTurnDegreesPerSecond = 1080f;
    const float AccelerationResponsiveness = 18f;
    const float LowSpeedBrakeResponsiveness = 7.4f;
    const float HighSpeedBrakeResponsiveness = 1.15f;
    const float BrakeDriftResponsivenessMultiplier = 0.8f;
    const float MaxDriftInertiaSlowdown = 4.2f;
    const float MovingObjectImpulseRequestCooldown = 0.045f;
    const float BatteringRequiredBoosterSeconds = 2f;
    const float BatteringFullSpeedRatio = 0.9f;
    const float BatteringSpeedGraceSeconds = 0.25f;
    const float BatteringPairCooldown = 0.5f;
    const float BatteringProbeRadius = 0.82f;
    const float BatteringFrontDotThreshold = 0.34f;
    const float AvengerBatteringFrontDotThreshold = 0.24f;
    const float RemotePushNoseProbeRadius = 0.52f;
    const float RemotePushBodyProbeRadius = 0.38f;
    const float RemotePushContactTolerance = 0.12f;
    static PhysicsMaterial2D playerCollisionMaterial;
    static readonly Collider2D[] RemotePushProbeHits = new Collider2D[32];
    static readonly Collider2D[] BatteringProbeHits = new Collider2D[32];
    Collider2D[] remotePushPlayerColliders;
    float nextMovingObjectImpulseRequestTime;
    float continuousBoosterTime;
    float lastBatteringEligibleSpeedTime = -999f;
    readonly Dictionary<int, float> nextLocalBatteringRequestTimeByTargetView = new Dictionary<int, float>();
    readonly Dictionary<int, float> nextAuthoritativeBatteringTimeByTargetView = new Dictionary<int, float>();
    readonly Dictionary<string, float> nextLocalBatteringRequestTimeByObstacle = new Dictionary<string, float>();
    readonly Dictionary<string, float> nextAuthoritativeBatteringTimeByObstacle = new Dictionary<string, float>();

    public float BoosterNormalized => boosterCharge;
    public bool IsBoosterDepleted => boosterExhausted;
    public bool HasFusionEngineEquipped => fusionEngineEquipped;
    public float CurrentSpeedReference => speed;
    float CurrentDepletedSpeedMultiplier => 1f - (RoomSettings.GetBoosterSlowdownPercent() / 100f);

    void Start()
    {
        if (PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
        {
            PlayerDeployableRuntime.EnsureAttached(gameObject);
            enabled = false;
            return;
        }

        if (LureBeaconDecoy.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
        {
            LureBeaconDecoy.EnsureAttached(gameObject);
            enabled = false;
            return;
        }

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
        ConfigureNetworkRigidbodyAuthority();

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

        if (!isAstronaut && GetComponent<EnemyBot>() == null && GetComponent<FireNebulaShipSparksVfx>() == null)
        {
            gameObject.AddComponent<FireNebulaShipSparksVfx>();
        }

        if (!isAstronaut && GetComponent<EnemyBot>() == null && GetComponent<StartingShipEntryVfx>() == null)
        {
            gameObject.AddComponent<StartingShipEntryVfx>();
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
            cam.SetTargetAndSnap(transform);
        }

        ResolveJoysticks();

        if (GetComponent<AdvancedMoveInputZone>() == null)
        {
            gameObject.AddComponent<AdvancedMoveInputZone>();
        }

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
        ConfigureNetworkRigidbodyAuthority();

        if (GetComponent<EnemyBot>() != null)
        {
            UpdateEngineAudio();
            return;
        }

        RefreshPilotSpeedBoost();
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

            if (IsStartingEntryActive())
            {
                moveInput = Vector2.zero;
                shootInput = Vector2.zero;
                effectiveMoveInput = Vector2.zero;
                UpdateEngineAudio();
                return;
            }

            ResolveJoysticks();

            moveInput = joystick != null && joystick.IsPressed ? joystick.inputVector : Vector2.zero;
            if (moveInput == Vector2.zero)
                moveInput = GetKeyboardMoveInput();
            shootInput = shootJoystick != null && shootJoystick.IsPressed ? shootJoystick.inputVector : Vector2.zero;

            if (moveInput.sqrMagnitude < 0.0004f)
                moveInput = Vector2.zero;

            if (shootInput.magnitude < 0.3f)
                shootInput = Vector2.zero;

            effectiveMoveInput = GetEffectiveMoveInput(moveInput);

            if (IsSuperBoosterActive())
            {
                boosterRecoveryDelayTimer = 0f;
                continuousBoosterTime = 0f;
            }
            else
            {
                UpdateBooster(Time.deltaTime);
            }

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

        if (IsStartingEntryActive())
            return;

        float currentSpeed = GetCurrentMovementSpeed();
        if (rb != null)
        {
            if (IsSuperBoosterActive())
            {
                ApplySuperBoosterVelocity(currentSpeed);
                rb.angularVelocity = 0f;
                float superAngle = Mathf.Atan2(superBoosterDirection.y, superBoosterDirection.x) * Mathf.Rad2Deg - 90f;
                rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, superAngle, BaseTurnDegreesPerSecond * Time.fixedDeltaTime));
                return;
            }

            ApplyVelocity(currentSpeed);
            ClampExcessCollisionBoost(currentSpeed);
            UpdateBatteringSpeedEligibility();
            TryRequestBatteringProbe();
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
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!photonView.IsMine || rb == null)
            return;

        float currentSpeed = GetCurrentMovementSpeed();
        ClampExcessCollisionBoost(currentSpeed);
        TryRequestBatteringImpact(collision);
        TryRequestMovingObjectImpulse(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (!photonView.IsMine || rb == null)
            return;

        TryRequestBatteringImpact(collision);
        TryRequestMovingObjectImpulse(collision);
    }

    void TryRequestBatteringImpact(Collision2D collision)
    {
        int batteringDamage = RoomSettings.GetBatteringDamage();
        if (batteringDamage <= 0 || collision == null || rb == null || GetComponent<AstronautSurvivor>() != null)
            return;

        if (continuousBoosterTime < BatteringRequiredBoosterSeconds ||
            (!HasRecentBatteringSpeed() && !IsAtBatteringSpeed(rb.linearVelocity.magnitude)))
            return;

        PlayerHealth ownHealth = GetComponent<PlayerHealth>();
        if (ownHealth == null || ownHealth.IsWreck || ownHealth.IsEvacuationAnimating || ownHealth.CurrentHP <= 0)
            return;

        if (!TryGetFrontBatteringImpactPoint(collision, out Vector2 impactPoint))
            return;

        PlayerHealth targetHealth = collision.collider != null
            ? collision.collider.GetComponentInParent<PlayerHealth>()
            : null;
        if (!IsValidBatteringTarget(targetHealth))
        {
            TryRequestObstacleBatteringImpact(collision, impactPoint);
            return;
        }

        PhotonView targetView = targetHealth.photonView;
        if (targetView == null || targetView.ViewID == photonView.ViewID)
            return;

        if (nextLocalBatteringRequestTimeByTargetView.TryGetValue(targetView.ViewID, out float nextAllowedTime) && Time.time < nextAllowedTime)
            return;

        nextLocalBatteringRequestTimeByTargetView[targetView.ViewID] = Time.time + BatteringPairCooldown;
        photonView.RPC(
            nameof(RequestBatteringImpact),
            RpcTarget.MasterClient,
            targetView.ViewID,
            impactPoint.x,
            impactPoint.y,
            continuousBoosterTime,
            rb.linearVelocity.magnitude);
    }

    void TryRequestObstacleBatteringImpact(Collision2D collision, Vector2 impactPoint)
    {
        ObstacleChunk obstacle = collision != null && collision.collider != null
            ? collision.collider.GetComponentInParent<ObstacleChunk>()
            : null;
        if (!IsValidBatteringObstacle(obstacle))
            return;

        if (nextLocalBatteringRequestTimeByObstacle.TryGetValue(obstacle.StableId, out float nextAllowedTime) && Time.time < nextAllowedTime)
            return;

        nextLocalBatteringRequestTimeByObstacle[obstacle.StableId] = Time.time + BatteringPairCooldown;
        photonView.RPC(
            nameof(RequestObstacleBatteringImpact),
            RpcTarget.MasterClient,
            obstacle.StableId,
            impactPoint.x,
            impactPoint.y,
            continuousBoosterTime,
            rb != null ? rb.linearVelocity.magnitude : 0f);
    }

    void TryRequestBatteringProbe()
    {
        int batteringDamage = RoomSettings.GetBatteringDamage();
        if (batteringDamage <= 0 || rb == null || GetComponent<AstronautSurvivor>() != null)
            return;

        if (!HasRecentBatteringSpeed())
            return;

        PlayerHealth ownHealth = GetComponent<PlayerHealth>();
        if (ownHealth == null || ownHealth.IsWreck || ownHealth.IsEvacuationAnimating || ownHealth.CurrentHP <= 0)
            return;

        Vector2 probeCenter = rb.position + (Vector2)transform.up * GetBatteringProbeDistance();
        ContactFilter2D contactFilter = new ContactFilter2D { useTriggers = false };
        int hitCount = Physics2D.OverlapCircle(probeCenter, GetBatteringProbeRadius(), contactFilter, BatteringProbeHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = BatteringProbeHits[i];
            BatteringProbeHits[i] = null;
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            PlayerHealth targetHealth = hit.GetComponentInParent<PlayerHealth>();
            if (IsValidBatteringTarget(targetHealth))
            {
                PhotonView targetView = targetHealth.photonView;
                if (targetView != null && (!nextLocalBatteringRequestTimeByTargetView.TryGetValue(targetView.ViewID, out float nextAllowedTime) || Time.time >= nextAllowedTime))
                {
                    Vector2 impactPoint = hit.ClosestPoint(probeCenter);
                    if (!IsBatteringImpactInFront(impactPoint))
                        continue;

                    nextLocalBatteringRequestTimeByTargetView[targetView.ViewID] = Time.time + BatteringPairCooldown;
                    photonView.RPC(
                        nameof(RequestBatteringImpact),
                        RpcTarget.MasterClient,
                        targetView.ViewID,
                        impactPoint.x,
                        impactPoint.y,
                        continuousBoosterTime,
                        rb.linearVelocity.magnitude);
                    return;
                }
            }

            ObstacleChunk obstacle = hit.GetComponentInParent<ObstacleChunk>();
            if (IsValidBatteringObstacle(obstacle) &&
                (!nextLocalBatteringRequestTimeByObstacle.TryGetValue(obstacle.StableId, out float nextObstacleTime) || Time.time >= nextObstacleTime))
            {
                Vector2 impactPoint = hit.ClosestPoint(probeCenter);
                if (!IsBatteringImpactInFront(impactPoint))
                    continue;

                nextLocalBatteringRequestTimeByObstacle[obstacle.StableId] = Time.time + BatteringPairCooldown;
                photonView.RPC(
                    nameof(RequestObstacleBatteringImpact),
                    RpcTarget.MasterClient,
                    obstacle.StableId,
                    impactPoint.x,
                    impactPoint.y,
                    continuousBoosterTime,
                    rb.linearVelocity.magnitude);
                return;
            }
        }
    }

    [PunRPC]
    void RequestBatteringImpact(int targetViewId, float impactX, float impactY, float reportedBoosterSeconds, float reportedSpeed, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null || photonView.Owner == null || messageInfo.Sender == null)
            return;

        if (messageInfo.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        int batteringDamage = RoomSettings.GetBatteringDamage();
        if (batteringDamage <= 0 || reportedBoosterSeconds < BatteringRequiredBoosterSeconds)
            return;

        PlayerHealth ownHealth = GetComponent<PlayerHealth>();
        if (ownHealth == null || ownHealth.IsWreck || ownHealth.IsEvacuationAnimating || ownHealth.IsAstronautControlled || ownHealth.CurrentHP <= 0)
            return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null || targetView == photonView)
            return;

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        if (!IsValidBatteringTarget(targetHealth))
            return;

        float authoritySpeed = rb != null ? rb.linearVelocity.magnitude : 0f;
        if (!IsAtBatteringSpeed(Mathf.Max(reportedSpeed, authoritySpeed)) && reportedBoosterSeconds < BatteringRequiredBoosterSeconds + BatteringSpeedGraceSeconds)
            return;

        if (nextAuthoritativeBatteringTimeByTargetView.TryGetValue(targetViewId, out float nextAllowedTime) && Time.time < nextAllowedTime)
            return;

        nextAuthoritativeBatteringTimeByTargetView[targetViewId] = Time.time + BatteringPairCooldown;
        int targetDamage = batteringDamage * 2;
        Vector2 impactNormal = ResolveBatteringImpactNormal(new Vector2(impactX, impactY));
        photonView.RPC(nameof(PlayBatteringImpactVisual), RpcTarget.All, impactX, impactY, impactNormal.x, impactNormal.y);
        targetHealth.photonView.RPC(nameof(PlayerHealth.TakePilotDamageAt), RpcTarget.MasterClient, targetDamage, photonView.ViewID, impactX, impactY, PlayerHealth.PilotDamageSourceRamming);
        photonView.RPC(nameof(PlayerHealth.TakePilotDamageAt), RpcTarget.MasterClient, batteringDamage, -1, impactX, impactY, PlayerHealth.PilotDamageSourceRamming);
    }

    [PunRPC]
    void RequestObstacleBatteringImpact(string obstacleStableId, float impactX, float impactY, float reportedBoosterSeconds, float reportedSpeed, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null || photonView.Owner == null || messageInfo.Sender == null)
            return;

        if (messageInfo.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        int batteringDamage = RoomSettings.GetBatteringDamage();
        if (batteringDamage <= 0 || reportedBoosterSeconds < BatteringRequiredBoosterSeconds || string.IsNullOrWhiteSpace(obstacleStableId))
            return;

        PlayerHealth ownHealth = GetComponent<PlayerHealth>();
        if (ownHealth == null || ownHealth.IsWreck || ownHealth.IsEvacuationAnimating || ownHealth.IsAstronautControlled || ownHealth.CurrentHP <= 0)
            return;

        ObstacleChunk obstacle = ObstacleChunk.Find(obstacleStableId);
        if (!IsValidBatteringObstacle(obstacle))
            return;

        float authoritySpeed = rb != null ? rb.linearVelocity.magnitude : 0f;
        if (!IsAtBatteringSpeed(Mathf.Max(reportedSpeed, authoritySpeed)) && reportedBoosterSeconds < BatteringRequiredBoosterSeconds + BatteringSpeedGraceSeconds)
            return;

        if (nextAuthoritativeBatteringTimeByObstacle.TryGetValue(obstacleStableId, out float nextAllowedTime) && Time.time < nextAllowedTime)
            return;

        nextAuthoritativeBatteringTimeByObstacle[obstacleStableId] = Time.time + BatteringPairCooldown;
        Vector2 impactNormal = ResolveBatteringImpactNormal(new Vector2(impactX, impactY));
        photonView.RPC(nameof(PlayBatteringImpactVisual), RpcTarget.All, impactX, impactY, impactNormal.x, impactNormal.y);
        SpaceObjectMotionSync.RequestObstacleDamage(obstacleStableId, GetPilotObstacleDamage(batteringDamage * 2));
        photonView.RPC(nameof(PlayerHealth.TakePilotDamageAt), RpcTarget.MasterClient, batteringDamage, -1, impactX, impactY, PlayerHealth.PilotDamageSourceRamming);
    }

    int GetPilotObstacleDamage(int baseDamage)
    {
        if (baseDamage <= 0)
            return 0;

        if (PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.SirNowitzkyId))
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * 1.5f));

        return baseDamage;
    }

    [PunRPC]
    void PlayBatteringImpactVisual(float impactX, float impactY, float normalX, float normalY)
    {
        if (photonView != null && photonView.IsMine)
            HapticsManager.PlayBatteringImpact();

        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        BatteringImpactVfx.Spawn(
            new Vector3(impactX, impactY, 0f),
            new Vector2(normalX, normalY),
            renderer);
    }

    bool IsValidBatteringTarget(PlayerHealth targetHealth)
    {
        if (targetHealth == null || targetHealth.photonView == null || targetHealth.photonView == photonView)
            return false;

        if (targetHealth.IsWreck || targetHealth.IsEvacuationAnimating || targetHealth.CurrentHP <= 0)
            return false;

        EnemyBot targetBot = targetHealth.GetComponent<EnemyBot>();
        if (targetBot != null && targetBot.IsPirateBaseLaunchProtected)
            return false;

        return targetHealth.CurrentShield > 0 || targetHealth.CurrentHP > 0;
    }

    bool IsValidBatteringObstacle(ObstacleChunk obstacle)
    {
        return obstacle != null &&
               !string.IsNullOrWhiteSpace(obstacle.StableId) &&
               obstacle.CurrentHealth > 0 &&
               RoomSettings.AreObstaclesDestructible();
    }

    void UpdateBatteringSpeedEligibility()
    {
        if (continuousBoosterTime >= BatteringRequiredBoosterSeconds && rb != null && IsAtBatteringSpeed(rb.linearVelocity.magnitude))
            lastBatteringEligibleSpeedTime = Time.time;
    }

    bool HasRecentBatteringSpeed()
    {
        return continuousBoosterTime >= BatteringRequiredBoosterSeconds &&
               Time.time - lastBatteringEligibleSpeedTime <= BatteringSpeedGraceSeconds;
    }

    bool TryGetFrontBatteringImpactPoint(Collision2D collision, out Vector2 impactPoint)
    {
        impactPoint = transform.position;
        if (collision == null || collision.contactCount <= 0)
            return false;

        float bestDot = float.NegativeInfinity;
        Vector2 bestPoint = impactPoint;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 contactPoint = collision.GetContact(i).point;
            Vector2 toContact = contactPoint - (Vector2)transform.position;
            if (toContact.sqrMagnitude < 0.0001f)
                continue;

            float dot = Vector2.Dot(((Vector2)transform.up).normalized, toContact.normalized);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestPoint = contactPoint;
            }
        }

        impactPoint = bestPoint;
        return bestDot >= GetBatteringFrontDotThreshold();
    }

    bool IsBatteringImpactInFront(Vector2 impactPoint)
    {
        Vector2 toImpact = impactPoint - (Vector2)transform.position;
        if (toImpact.sqrMagnitude < 0.0001f)
            return false;

        return Vector2.Dot(((Vector2)transform.up).normalized, toImpact.normalized) >= GetBatteringFrontDotThreshold();
    }

    bool IsAtBatteringSpeed(float currentMagnitude)
    {
        return currentMagnitude >= GetBatteringFullSpeedThreshold();
    }

    float GetBatteringFullSpeedThreshold()
    {
        return Mathf.Max(0.1f, speed * GetShipMaxSpeedBoostMultiplier() * BatteringFullSpeedRatio);
    }

    float GetBatteringProbeDistance()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            return 0.9f;

        return Mathf.Clamp(renderer.bounds.extents.y * 0.95f, 0.55f, 1.5f);
    }

    float GetBatteringProbeRadius()
    {
        float radius = BatteringProbeRadius;
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
            radius = Mathf.Max(radius, renderer.bounds.extents.x * 0.66f);

        if (GetCurrentShipType() == ShipType.Avenger)
            radius = Mathf.Max(radius, 0.98f);

        return Mathf.Clamp(radius, BatteringProbeRadius, 1.16f);
    }

    float GetBatteringFrontDotThreshold()
    {
        return GetCurrentShipType() == ShipType.Avenger
            ? AvengerBatteringFrontDotThreshold
            : BatteringFrontDotThreshold;
    }

    ShipType GetCurrentShipType()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        return ShipCatalog.GetShipTypeFromSkinIndex(RoomSettings.GetPlayerShipSkin(owner, 0));
    }

    Vector2 ResolveBatteringImpactNormal(Vector2 impactPoint)
    {
        Vector2 normal = impactPoint - (Vector2)transform.position;
        if (normal.sqrMagnitude > 0.0001f)
            return normal.normalized;

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f)
            return rb.linearVelocity.normalized;

        return transform.up;
    }

    void ClampExcessCollisionBoost(float currentSpeed)
    {
        if (rb == null)
            return;

        float expectedTopSpeed = currentSpeed * Mathf.Max(1f, GetCurrentBoostSpeedMultiplier());
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
        if (RequestMovingObjectImpulse(movingObject, playerVelocity, 2.15f))
            return;

        DroppedCargoCrate crate = collision.collider != null
            ? collision.collider.GetComponentInParent<DroppedCargoCrate>()
            : null;
        RequestDroppedCargoImpulse(crate, playerVelocity, 1.75f);
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
        if (playerVelocity.sqrMagnitude < 0.01f && effectiveMoveInput.sqrMagnitude < 0.01f)
            return;

        Vector2 noseProbeCenter = rb.position + (Vector2)transform.up * GetRemotePushProbeDistance();
        MovingSpaceObject bestObject = null;
        DroppedCargoCrate bestCrate = null;
        float bestDistance = float.MaxValue;

        FindRemotePushTarget(noseProbeCenter, RemotePushNoseProbeRadius, ref bestObject, ref bestCrate, ref bestDistance);
        FindRemotePushTarget(rb.position, RemotePushBodyProbeRadius, ref bestObject, ref bestCrate, ref bestDistance);

        if (RequestMovingObjectImpulse(bestObject, playerVelocity, bestObject != null && bestObject.ObjectType == MovingSpaceObject.SpaceObjectType.Treasure ? 2.95f : 1.75f))
            return;

        RequestDroppedCargoImpulse(bestCrate, playerVelocity, 2.05f);
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

            if (!IsRemotePushContactClose(hit, out float contactDistance))
                continue;

            float probeDistance = Vector2.Distance(probeCenter, hit.ClosestPoint(probeCenter));
            float distance = contactDistance + probeDistance * 0.025f;

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

    bool IsRemotePushContactClose(Collider2D targetCollider, out float bestDistance)
    {
        bestDistance = float.MaxValue;
        if (targetCollider == null || targetCollider.isTrigger || !targetCollider.enabled)
            return false;

        Collider2D[] playerColliders = GetRemotePushPlayerColliders();
        if (playerColliders == null || playerColliders.Length == 0)
            return false;

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];
            if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger)
                continue;

            ColliderDistance2D distance = playerCollider.Distance(targetCollider);
            float resolvedDistance = distance.isOverlapped ? 0f : Mathf.Max(0f, distance.distance);
            if (resolvedDistance < bestDistance)
                bestDistance = resolvedDistance;
        }

        return bestDistance <= RemotePushContactTolerance;
    }

    Collider2D[] GetRemotePushPlayerColliders()
    {
        if (remotePushPlayerColliders == null || remotePushPlayerColliders.Length == 0)
            remotePushPlayerColliders = GetComponents<Collider2D>();

        return remotePushPlayerColliders;
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

        Vector2 pushVector = ResolveRemotePushVector(movingObject.transform.position, playerVelocity);
        if (pushVector.sqrMagnitude < 0.01f)
            return false;

        int weightFactor = movingObject.ObjectType == MovingSpaceObject.SpaceObjectType.Obstacle
            ? RoomSettings.GetObstacleWeightFactor()
            : RoomSettings.GetTreasureWeightFactor();
        weightFactor = Mathf.Max(1, weightFactor);

        float effectiveWeight = movingObject.ObjectType == MovingSpaceObject.SpaceObjectType.Treasure
            ? Mathf.Max(1f, weightFactor * 0.45f)
            : Mathf.Max(1f, weightFactor * 0.72f);
        Vector2 impulse = (pushVector * Mathf.Max(0.1f, strength)) / effectiveWeight;
        nextMovingObjectImpulseRequestTime = Time.time + MovingObjectImpulseRequestCooldown;
        movingObject.ApplyRemotePushPrediction(impulse);
        SpaceObjectMotionSync.RequestPlayerPush(movingObject.StableId, impulse, transform.position);
        return true;
    }

    bool RequestDroppedCargoImpulse(DroppedCargoCrate crate, Vector2 playerVelocity, float strength)
    {
        if (crate == null || Time.time < nextMovingObjectImpulseRequestTime)
            return false;

        Vector2 pushVector = ResolveRemotePushVector(crate.transform.position, playerVelocity);
        if (pushVector.sqrMagnitude < 0.01f)
            return false;

        if (!crate.TryRequestRemoteImpulse(pushVector * Mathf.Max(0.1f, strength)))
            return false;

        nextMovingObjectImpulseRequestTime = Time.time + MovingObjectImpulseRequestCooldown;
        return true;
    }

    Vector2 ResolveRemotePushVector(Vector2 targetPosition, Vector2 playerVelocity)
    {
        Vector2 desired = playerVelocity;
        Vector2 intendedInput = effectiveMoveInput.sqrMagnitude > 0.01f ? effectiveMoveInput.normalized * speed : Vector2.zero;
        if (intendedInput.sqrMagnitude > desired.sqrMagnitude)
            desired = intendedInput;

        if (desired.sqrMagnitude < 0.01f)
            desired = transform.up * Mathf.Max(1f, speed * 0.65f);

        Vector2 awayToTarget = targetPosition - (Vector2)transform.position;
        if (awayToTarget.sqrMagnitude > 0.0001f)
        {
            Vector2 awayDirection = awayToTarget.normalized;
            float desiredMagnitude = Mathf.Max(desired.magnitude, speed * 0.72f);
            float alignment = Vector2.Dot(desired.normalized, awayDirection);
            if (alignment < 0.15f)
                desired = Vector2.Lerp(desired.normalized, awayDirection, 0.78f).normalized * desiredMagnitude;
            else
                desired = (desired.normalized + awayDirection * 0.35f).normalized * desiredMagnitude;
        }

        return desired;
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

    public bool CanUseAdvancedMoveJoystick()
    {
        if (!enabled || photonView == null || !photonView.IsMine || GetComponent<EnemyBot>() != null)
            return false;

        if (!RoomSettings.IsAdvancedMovingJoystickEnabled())
            return false;

        if (!IsGameStarted() || IsStartingEntryActive())
            return false;

        PlayerHealth health = GetComponent<PlayerHealth>();
        return health == null || (!health.IsWreck && !health.IsEvacuationAnimating);
    }

    bool IsStartingEntryActive()
    {
        StartingShipEntryVfx entry = GetComponent<StartingShipEntryVfx>();
        return entry != null && entry.IsControllingMotion;
    }

    void UpdateBooster(float deltaTime)
    {
        if (GetComponent<AstronautSurvivor>() != null)
        {
            boosterCharge = 0f;
            boosterExhausted = false;
            boosterRecoveryDelayTimer = 0f;
            continuousBoosterTime = 0f;
            lastBatteringEligibleSpeedTime = -999f;
            return;
        }

        bool usingFullAcceleration = effectiveMoveInput.magnitude >= maxSpeedThreshold;
        bool usingBooster = usingFullAcceleration && !boosterExhausted;

        if (usingBooster)
        {
            boosterCharge -= deltaTime / boosterDuration;
            boosterRecoveryDelayTimer = GetCurrentBoosterRecoveryDelay();
            continuousBoosterTime += deltaTime;
        }
        else if (usingFullAcceleration)
        {
            boosterRecoveryDelayTimer = GetCurrentBoosterRecoveryDelay();
            continuousBoosterTime = 0f;
            lastBatteringEligibleSpeedTime = -999f;
        }
        else if (boosterRecoveryDelayTimer > 0f)
        {
            boosterRecoveryDelayTimer -= deltaTime;
            continuousBoosterTime = 0f;
            lastBatteringEligibleSpeedTime = -999f;
        }
        else
        {
            boosterCharge += deltaTime / (boosterDuration * 2f);
            continuousBoosterTime = 0f;
            lastBatteringEligibleSpeedTime = -999f;
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
            return rawInput.normalized * GetCurrentBoostSpeedMultiplier();

        return rawInput;
    }

    Vector2 GetKeyboardMoveInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        Vector2 keyboardInput = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            keyboardInput.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            keyboardInput.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            keyboardInput.y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            keyboardInput.y += 1f;
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            keyboardInput.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            keyboardInput.x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            keyboardInput.y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            keyboardInput.y += 1f;
#endif

        return keyboardInput.sqrMagnitude > 1f ? keyboardInput.normalized : keyboardInput;
#else
        return Vector2.zero;
#endif
    }

    float GetCurrentBoostSpeedMultiplier()
    {
        if (boosterCharge < 0.01f)
            return 1f;

        return GetShipMaxSpeedBoostMultiplier();
    }

    float GetShipMaxSpeedBoostMultiplier()
    {
        return 1f + (Mathf.Max(0, maxBoostPercent) / 100f);
    }

    float GetCurrentMovementSpeed()
    {
        float currentSpeed = IsBoosterDepleted ? speed * CurrentDepletedSpeedMultiplier : speed;
        HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>();
        if (nebulaTarget != null)
            currentSpeed *= nebulaTarget.CurrentNebulaSpeedMultiplier;

        return Mathf.Max(0.1f, currentSpeed);
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
            float maxDelta = brakeResponsiveness * currentSpeed * releaseDriftMultiplier * Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, maxDelta);
            return;
        }

        float accelerationDelta = AccelerationResponsiveness * currentSpeed * GetDriftSlowdownMultiplier(driftLevel) * Time.fixedDeltaTime;
        rb.linearVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, accelerationDelta);
    }

    float GetDriftSlowdownMultiplier(int driftLevel)
    {
        if (driftLevel <= 1)
            return 1f;

        float t = Mathf.InverseLerp(1f, 10f, driftLevel);
        float slowdown = Mathf.Lerp(1f, MaxDriftInertiaSlowdown, t);
        float response = 1f / slowdown;
        if (equippedAfterburnerStabilizerCount > 0)
            response = Mathf.Lerp(response, 1f, Mathf.Clamp01(0.38f * equippedAfterburnerStabilizerCount));

        return response;
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
        AstronautSurvivor astronaut = GetComponent<AstronautSurvivor>();
        if (astronaut != null && !astronaut.IsEscapePodMode)
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
                ConfigureMovementJoystickFeel(joystick);
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

        ConfigureMovementJoystickFeel(joystick);
    }

    void ConfigureMovementJoystickFeel(Joystick movementJoystick)
    {
        if (movementJoystick == null)
            return;

        movementJoystick.deadZone = 0.12f;
        movementJoystick.rescaleInputAfterDeadZone = true;
        movementJoystick.responseExponent = 1.08f;
        movementJoystick.recenterOnPointerDown = RoomSettings.IsAdvancedMovingJoystickEnabled();
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

    public void ActivatePilotSpeedBoost(float multiplier, float duration)
    {
        if (GetComponent<EnemyBot>() != null || GetComponent<AstronautSurvivor>() != null)
            return;

        pilotSpeedBoostMultiplier = Mathf.Max(pilotSpeedBoostMultiplier, Mathf.Max(1f, multiplier));
        pilotSpeedBoostUntil = Mathf.Max(pilotSpeedBoostUntil, Time.time + Mathf.Max(0f, duration));
        SyncEquippedEngineProfile(forceRefresh: true);
    }

    public void ActivateSuperBooster(float duration)
    {
        if (!photonView.IsMine || GetComponent<EnemyBot>() != null || GetComponent<AstronautSurvivor>() != null)
            return;

        Vector2 forward = transform.up;
        if (forward.sqrMagnitude < 0.001f)
            forward = lastFacingDirection.sqrMagnitude > 0.001f ? lastFacingDirection : Vector2.up;

        superBoosterDirection = forward.normalized;
        superBoosterUntil = Mathf.Max(superBoosterUntil, Time.time + Mathf.Max(0.05f, duration));
    }

    void RefreshPilotSpeedBoost()
    {
        if (pilotSpeedBoostMultiplier <= 1f || Time.time < pilotSpeedBoostUntil)
            return;

        pilotSpeedBoostMultiplier = 1f;
        pilotSpeedBoostUntil = -1f;
        SyncEquippedEngineProfile(forceRefresh: true);
    }

    float GetPilotSpeedMultiplier()
    {
        return Time.time < pilotSpeedBoostUntil ? Mathf.Max(1f, pilotSpeedBoostMultiplier) : 1f;
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
        int fuelTankCount = CountEquippedEngineItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.FuelTankId);
        int afterburnerStabilizerCount = CountEquippedEngineItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.AfterburnerStabilizerId);
        bool hasFusion = fusionCount > 0;
        string signature = shipSkinIndex + ":" + fusionCount + ":" + fuelTankCount + ":" + afterburnerStabilizerCount;
        if (!forceRefresh && signature == lastAppliedEngineSignature)
            return;

        float baseShipSpeed = ShipCatalog.GetBaseSpeed(shipSkinIndex);
        float baseShipBoosterDuration = ShipCatalog.GetBoosterDuration(shipSkinIndex);
        float baseShipTurnRate = ShipCatalog.GetTurnRateMultiplier(shipSkinIndex);
        int baseShipMaxBoostPercent = ShipCatalog.GetMaxBoostPercent(shipSkinIndex);

        equippedFusionEngineCount = fusionCount;
        equippedFuelTankCount = fuelTankCount;
        equippedAfterburnerStabilizerCount = afterburnerStabilizerCount;
        fusionEngineEquipped = hasFusion;
        baseSpeed = Mathf.Max(0.1f, baseShipSpeed);
        baseBoosterDuration = Mathf.Max(0.1f, baseShipBoosterDuration);
        turnRateMultiplier = Mathf.Max(0.1f, baseShipTurnRate * (1f + 0.25f * equippedAfterburnerStabilizerCount));
        maxBoostPercent = Mathf.Max(0, baseShipMaxBoostPercent);
        speed = baseSpeed * (1f + (0.15f * equippedFusionEngineCount)) * GetPilotSpeedMultiplier();
        boosterDuration = baseBoosterDuration * (1f + (0.5f * equippedFuelTankCount));
        lastAppliedEngineSignature = signature;
        SyncEngineAudioClip();

        EngineThrusterVFX thrusterVfx = GetComponent<EngineThrusterVFX>();
        if (thrusterVfx != null)
            thrusterVfx.RefreshMode();
    }

    int CountEquippedFusionEngines(string[] equipmentSlots, int shipSkinIndex)
    {
        return CountEquippedEngineItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.FusionEngineId);
    }

    int CountEquippedEngineItem(string[] equipmentSlots, int shipSkinIndex, string itemId)
    {
        if (equipmentSlots == null)
            return 0;

        int count = 0;
        if (ShipCatalog.IsEquipmentSlotEnabled(4, shipSkinIndex) &&
            string.Equals(GetEquipmentItem(equipmentSlots, 4), itemId, StringComparison.Ordinal))
        {
            count++;
        }

        if (ShipCatalog.IsEquipmentSlotEnabled(5, shipSkinIndex) &&
            string.Equals(GetEquipmentItem(equipmentSlots, 5), itemId, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    bool IsSuperBoosterActive()
    {
        return Time.time < superBoosterUntil;
    }

    void ApplySuperBoosterVelocity(float currentSpeed)
    {
        if (rb == null)
            return;

        Vector2 direction = superBoosterDirection.sqrMagnitude > 0.001f ? superBoosterDirection.normalized : Vector2.up;
        float boostedSpeed = Mathf.Max(0.1f, currentSpeed) * 3f;
        rb.linearVelocity = direction * boostedSpeed;
        targetRotationAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        lastFacingDirection = direction;
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
        float pilotBonus = PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.RobyId) ? 1f : 0f;
        return Mathf.Max(0f, baseDelay - equippedFusionEngineCount - pilotBonus);
    }

    AudioClip ResolveEngineAudioClip()
    {
        EnemyBot enemyBot = GetComponent<EnemyBot>();
        if (enemyBot != null && enemyBot.Kind == EnemyBotKind.RadarShip)
            return AudioManager.Instance.RadarShipEngineClip;

        if (enemyBot != null && enemyBot.Kind == EnemyBotKind.RescueShip)
            return AudioManager.Instance.RescueShipEngineClip;

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

    void ConfigureNetworkRigidbodyAuthority()
    {
        if (rb == null || photonView == null || !PhotonNetwork.InRoom)
            return;

        bool hasAuthority = photonView.IsMine;
        EnemyBot launchProtectedBot = GetComponent<EnemyBot>();
        if (launchProtectedBot != null && launchProtectedBot.IsPirateBaseLaunchProtected)
        {
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            return;
        }

        if (networkBodyAuthorityApplied && lastNetworkBodyAuthority == hasAuthority)
            return;

        networkBodyAuthorityApplied = true;
        lastNetworkBodyAuthority = hasAuthority;

        rb.bodyType = hasAuthority ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.angularVelocity = 0f;

        if (!hasAuthority)
            rb.linearVelocity = Vector2.zero;
    }
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class EngineThrusterVFX : MonoBehaviour
{
    const string ThrusterRootName = "EngineVFX";
    const float PlayerTrailMinVertexDistance = 0.07f;
    const float EnemyTrailMinVertexDistance = 0.08f;
    const float AstronautTrailMinVertexDistance = 0.06f;

    static Material sharedSpritesMaterial;

    PlayerMovement movement;
    HideInNebulaTarget nebulaTarget;
    PhotonView photonViewCache;
    Rigidbody2D rb;
    SpriteRenderer shipRenderer;
    TrailRenderer[] trailRenderers;
    float referenceSpeed = 5f;
    bool isEnemyBot;
    bool isAstronaut;
    bool isEscapePod;
    bool fusionTrailEquipped;
    EnemyTrailProfile enemyTrailProfile;
    Vector2[] playerThrusterOffsetFactors = new[] { new Vector2(0f, 0.02f) };

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        shipRenderer = GetComponent<SpriteRenderer>();
        movement = GetComponent<PlayerMovement>();
        nebulaTarget = GetComponent<HideInNebulaTarget>();
        photonViewCache = GetComponent<PhotonView>();
        referenceSpeed = Mathf.Max(1f, movement != null ? movement.speed : 5f);
        RefreshMode();
        CreateThrusterObjects();
        UpdateVisuals(0f);
    }

    public void RefreshMode()
    {
        bool previousIsAstronaut = isAstronaut;
        bool previousIsEscapePod = isEscapePod;
        int previousTrailCount = trailRenderers != null ? trailRenderers.Length : -1;
        EnemyBot enemyBot = GetComponent<EnemyBot>();
        isEnemyBot = enemyBot != null;
        enemyTrailProfile = enemyBot != null && enemyBot.Definition != null ? enemyBot.Definition.Trails : null;
        AstronautSurvivor astronaut = GetComponent<AstronautSurvivor>();
        isAstronaut = astronaut != null;
        isEscapePod = astronaut != null && astronaut.IsEscapePodMode;
        fusionTrailEquipped = movement != null && movement.HasFusionEngineEquipped && !isEnemyBot && !isAstronaut;

        if (photonViewCache == null)
            photonViewCache = GetComponent<PhotonView>();

        PhotonView view = photonViewCache;
        int skinIndex = view != null && view.Owner != null ? RoomSettings.GetPlayerShipSkin(view.Owner, 0) : 0;
        playerThrusterOffsetFactors = !isEnemyBot && !isAstronaut
            ? ShipCatalog.GetThrusterOffsetFactors(skinIndex)
            : isEscapePod
                ? new[] { new Vector2(-0.18f, 0.02f), new Vector2(0.18f, 0.02f) }
                : new[] { new Vector2(0f, 0.02f) };

        bool shouldRecreateTrails = trailRenderers != null &&
                                    (previousIsAstronaut != isAstronaut ||
                                     previousIsEscapePod != isEscapePod ||
                                     previousTrailCount != playerThrusterOffsetFactors.Length);
        if (shouldRecreateTrails)
            CreateThrusterObjects();
        else
            ReapplyTrailAppearance();
    }

    void Update()
    {
        if (rb == null)
            return;

        if (movement == null)
            movement = GetComponent<PlayerMovement>();

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
                ? new Vector3(0f, -shipHeight * (isEscapePod ? 0.42f : 0.34f), 0f)
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
        trail.time = isAstronaut ? (isEscapePod ? 0.36f : 0.2f) : enemyTrailProfile != null ? Mathf.Min(enemyTrailProfile.MaxTrailTime, 0.58f) : 0.36f;
        trail.minVertexDistance = isAstronaut ? (isEscapePod ? 0.05f : AstronautTrailMinVertexDistance) : enemyTrailProfile != null ? EnemyTrailMinVertexDistance : PlayerTrailMinVertexDistance;
        trail.widthMultiplier = isAstronaut ? (isEscapePod ? 0.085f : 0.04f) : enemyTrailProfile != null ? enemyTrailProfile.MaxTrailWidth : 0.08f;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.numCapVertices = 3;
        trail.numCornerVertices = 2;
        trail.sharedMaterial = GetSpritesMaterial();
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
        if (isAstronaut && isEscapePod)
        {
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.98f, 0.86f), 0f),
                    new GradientColorKey(new Color(1f, 0.78f, 0.28f), 0.18f),
                    new GradientColorKey(new Color(1f, 0.42f, 0.08f), 0.55f),
                    new GradientColorKey(new Color(0.32f, 0.06f, 0.01f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.66f, 0.24f),
                    new GradientAlphaKey(0.26f, 0.68f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
        else if (isAstronaut)
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
        else if (enemyTrailProfile != null && enemyTrailProfile.VisualStyle == EnemyTrailVisualStyle.BlueTwin)
        {
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.88f, 0.98f, 1f), 0f),
                    new GradientColorKey(new Color(0.32f, 0.78f, 1f), 0.18f),
                    new GradientColorKey(new Color(0.02f, 0.48f, 0.98f), 0.58f),
                    new GradientColorKey(new Color(0f, 0.1f, 0.26f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.96f, 0f),
                    new GradientAlphaKey(0.74f, 0.24f),
                    new GradientAlphaKey(0.28f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
        else if (enemyTrailProfile != null && enemyTrailProfile.VisualStyle == EnemyTrailVisualStyle.PurpleLarge)
        {
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.94f, 0.82f, 1f), 0f),
                    new GradientColorKey(new Color(0.62f, 0.18f, 1f), 0.18f),
                    new GradientColorKey(new Color(0.28f, 0.02f, 0.72f), 0.58f),
                    new GradientColorKey(new Color(0.07f, 0f, 0.2f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.98f, 0f),
                    new GradientAlphaKey(0.76f, 0.22f),
                    new GradientAlphaKey(0.32f, 0.68f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
        else if (enemyTrailProfile != null && enemyTrailProfile.VisualStyle == EnemyTrailVisualStyle.OrangeRedTwin)
        {
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.96f, 0.76f), 0f),
                    new GradientColorKey(new Color(1f, 0.48f, 0.12f), 0.2f),
                    new GradientColorKey(new Color(0.96f, 0.12f, 0.04f), 0.58f),
                    new GradientColorKey(new Color(0.32f, 0.02f, 0.01f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.98f, 0f),
                    new GradientAlphaKey(0.74f, 0.22f),
                    new GradientAlphaKey(0.3f, 0.68f),
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
        if (nebulaTarget == null)
            nebulaTarget = GetComponent<HideInNebulaTarget>();
        if (photonViewCache == null)
            photonViewCache = GetComponent<PhotonView>();

        bool hideForOthers = nebulaTarget != null && nebulaTarget.IsHiddenFromLocalPlayer() && photonViewCache != null && !photonViewCache.IsMine;
        if (hideForOthers)
        {
            DisableEmission();
            return;
        }

        float clamped = Mathf.Clamp01(speedNormalized);
        float intensity = Mathf.Lerp(isAstronaut ? (isEscapePod ? 0.36f : 0.28f) : 0.18f, 1f, clamped);

        if (trailRenderers == null)
            return;

        EnsureTrailSorting();

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trailRenderer = trailRenderers[i];
            if (trailRenderer == null)
                continue;

            if (isAstronaut)
            {
                if (isEscapePod)
                {
                    trailRenderer.time = Mathf.Lerp(0.22f, 0.44f, intensity);
                    trailRenderer.widthMultiplier = Mathf.Lerp(0.04f, 0.12f, intensity);
                    trailRenderer.emitting = clamped > 0.05f;
                }
                else
                {
                    trailRenderer.time = Mathf.Lerp(0.12f, 0.28f, intensity);
                    trailRenderer.widthMultiplier = Mathf.Lerp(0.015f, 0.055f, intensity);
                    trailRenderer.emitting = clamped > 0.08f;
                }
            }
            else if (enemyTrailProfile != null)
            {
                trailRenderer.time = Mathf.Lerp(enemyTrailProfile.MinTrailTime, Mathf.Min(enemyTrailProfile.MaxTrailTime, 0.58f), intensity);
                trailRenderer.widthMultiplier = Mathf.Lerp(enemyTrailProfile.MinTrailWidth, enemyTrailProfile.MaxTrailWidth, intensity);
                trailRenderer.emitting = clamped > enemyTrailProfile.EmissionThreshold;
            }
            else
            {
                float maxTrailTime = fusionTrailEquipped ? 0.72f : 0.58f;
                trailRenderer.time = Mathf.Lerp(0.18f, maxTrailTime, intensity);
                trailRenderer.widthMultiplier = Mathf.Lerp(0.028f, 0.12f, intensity);
                trailRenderer.emitting = clamped > 0.04f;
            }
        }
    }

    void EnsureTrailSorting()
    {
        if (trailRenderers == null || shipRenderer == null)
            return;

        int targetLayer = shipRenderer.sortingLayerID;
        int targetOrder = shipRenderer.sortingOrder - 2;
        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];
            if (trail == null)
                continue;

            if (trail.sortingLayerID != targetLayer)
                trail.sortingLayerID = targetLayer;

            if (trail.sortingOrder != targetOrder)
                trail.sortingOrder = targetOrder;
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

    static Material GetSpritesMaterial()
    {
        if (sharedSpritesMaterial != null)
            return sharedSpritesMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        sharedSpritesMaterial = new Material(shader)
        {
            name = "EngineThrusterVFXMaterial"
        };
        return sharedSpritesMaterial;
    }
}
