using UnityEngine;
using System;
using System.Collections.Generic;
using Photon.Pun;

public class PlayerMovement : MonoBehaviourPun
{
    public static bool gameStarted = false;

    private Rigidbody2D rb;
    bool networkBodyAuthorityApplied;
    bool lastNetworkBodyAuthority;
    public float speed = 5f;
    public float depletedSpeedMultiplier = 0.7f;
    public float boosterDuration = 10f;
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
    float baseBoosterDuration = 10f;
    float turnRateMultiplier = 1f;
    int maxBoostPercent = 30;
    bool baseSpeedCaptured;
    bool fusionEngineEquipped;
    int equippedPowerEngineCount;
    int equippedIonEngineCount;
    int equippedFusionEngineCount;
    int equippedFuelTankCount;
    int equippedHybridEngineCount;
    int equippedDoubleEngineCount;
    int equippedSuperBoosterCount;
    int equippedAfterburnerStabilizerCount;
    int equippedBlackMarketThrusterCount;
    string lastAppliedEngineSignature = string.Empty;
    string equippedEngineTrailItemId = string.Empty;
    float pilotSpeedBoostMultiplier = 1f;
    float pilotSpeedBoostUntil = -1f;
    float superBoosterUntil = -1f;
    Vector2 superBoosterDirection = Vector2.up;
    float invaderResonanceDriftUntil = -1f;
    const float BaseTurnDegreesPerSecond = 1080f;
    const float AccelerationResponsiveness = 18f;
    const float LowSpeedBrakeResponsiveness = 7.4f;
    const float HighSpeedBrakeResponsiveness = 1.15f;
    const float BrakeDriftResponsivenessMultiplier = 0.8f;
    const float MaxDriftInertiaSlowdown = 4.2f;
    const float MovingObjectImpulseRequestCooldown = 0.045f;
    const float EngineAudioIdleVolume = 0.12f;
    const float EngineAudioFullVolume = 0.42f;
    const float EngineAudioVolumeMultiplier = 1.5f;
    const float BatteringRequiredBoosterSeconds = 2f;
    const float BatteringFullSpeedRatio = 0.9f;
    const float BatteringSpeedGraceSeconds = 0.25f;
    const float BatteringPairCooldown = 0.5f;
    const float BatteringProbeRadius = 0.82f;
    const float BatteringFrontDotThreshold = 0.34f;
    const float AvengerBatteringFrontDotThreshold = 0.24f;
    public const float AdvancedBoosterOuterInputLimit = 1.25f;
    public const float AdvancedBoosterActivationThreshold = 1.23f;
    const float AdvancedBoosterVisualWakeThreshold = 1f;
    const float MaxEngineSpeedBonus = 0.35f;
    const int MaxEngineBoostPercentBonus = 20;
    const float PowerEngineSpeedBonus = 0.1f;
    const int PowerEngineBoostPercentBonus = 5;
    const float IonEngineSpeedBonus = 0.06f;
    const float IonEngineRecoveryDelayBonus = 1.25f;
    const float IonEngineTurnRateBonus = 0.1f;
    const float FusionEngineSpeedBonus = 0.14f;
    const float FusionEngineRecoveryDelayBonus = 1f;
    const float FuelTankBoosterDurationBonus = 1f;
    const float HybridEngineSpeedBonus = 0.08f;
    const float HybridEngineBoosterDurationBonus = 0.5f;
    const float HybridEngineRecoveryDelayBonus = 0.5f;
    const float DoubleEngineSpeedBonus = 0.2f;
    const int DoubleEngineBoostPercentBonus = 10;
    const float DoubleEngineBoosterDurationPenalty = 0.15f;
    const float DoubleEngineTurnRatePenalty = 0.1f;
    const float BlackMarketThrusterSpeedBonus = 0.28f;
    const int BlackMarketThrusterBoostPercentBonus = 15;
    const float BlackMarketThrusterTurnRatePenalty = 0.08f;
    const float AshCleanBurnBoosterDrainMultiplier = 0.88f;
    const float AfterburnerTurnRateBonus = 0.25f;
    const float AfterburnerBoosterRecoveryDelayMultiplier = 0.5f;
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
    bool keyboardBoosterRequested;
    bool advancedBoosterRequested;
    bool boosterActiveThisFrame;
    float advancedBoosterInputRatio;

    public static float LocalAdvancedBoosterInputRatio { get; private set; }
    public static bool LocalAdvancedBoosterActive { get; private set; }
    public static bool LocalAdvancedBoosterAvailable { get; private set; }
    public static bool LocalAdvancedBoosterEnabled { get; private set; }
    public static float AdvancedBoosterVisualThreshold => AdvancedBoosterVisualWakeThreshold;

    public float BoosterNormalized => boosterCharge;
    public bool IsBoosterDepleted => boosterExhausted;
    public bool HasFusionEngineEquipped => fusionEngineEquipped;
    public string CurrentEngineTrailItemId => equippedEngineTrailItemId;
    public float CurrentSpeedReference => speed;
    float CurrentDepletedSpeedMultiplier => 1f - (RoomSettings.GetBoosterSlowdownPercent() / 100f);

    void Start()
    {
        if (ViperRecoveryPlotController.TryEnsureViperWreckRuntime(gameObject))
        {
            StopEngineAudioImmediately();
            enabled = false;
            return;
        }

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
        EnsureNeutralRiderBootstrap();

        ActorIdentity identity = ActorIdentity.Ensure(gameObject);
        AstronautSurvivor astronaut = null;
        bool isAstronaut = identity != null
            ? identity.IsAstronaut
            : GetComponent<AstronautSurvivor>() != null || AstronautSurvivor.IsAstronautInstantiationData(photonView != null ? photonView.InstantiationData : null);
        bool isNeutralRider = NeutralRiderController.IsNeutralRider(gameObject);
        if (isAstronaut)
        {
            astronaut = GetComponent<AstronautSurvivor>();
            if (astronaut == null)
                astronaut = gameObject.AddComponent<AstronautSurvivor>();

            astronaut.InitializeFromPhotonData();
            identity = ActorIdentity.Ensure(gameObject);
            if (astronaut.IsEnemySurvivor)
            {
                StopEngineAudioImmediately();
                enabled = false;
                return;
            }
        }
        bool isEscapePod = astronaut != null && astronaut.IsEscapePodMode;

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

        if (!isAstronaut && GetComponent<EnemyBot>() == null && !isNeutralRider && GetComponent<PlayerNicknameUI>() == null)
        {
            gameObject.AddComponent<PlayerNicknameUI>();
        }

        if (GetComponent<EngineThrusterVFX>() == null)
        {
            gameObject.AddComponent<EngineThrusterVFX>();
        }

        if (!isAstronaut && GetComponent<EnemyBot>() == null && !isNeutralRider && GetComponent<FireNebulaShipSparksVfx>() == null)
        {
            gameObject.AddComponent<FireNebulaShipSparksVfx>();
        }

        if (!isAstronaut && GetComponent<EnemyBot>() == null && !isNeutralRider && GetComponent<StartingShipEntryVfx>() == null)
        {
            gameObject.AddComponent<StartingShipEntryVfx>();
        }

        targetRotationAngle = transform.eulerAngles.z;

        CaptureBaseMovementProfile();
        if (!isAstronaut)
        {
            SetupEngineAudio();
            SyncEquippedEngineProfile(forceRefresh: true);
        }
        lastAudioPosition = transform.position;

        if (GetComponent<EnemyBot>() != null || isNeutralRider)
            return;

        if (!photonView.IsMine)
            return;

        CameraFollow cam = FindAnyObjectByType<CameraFollow>();
        if (cam != null)
        {
            cam.SetTargetAndSnap(transform);
        }

        ResolveJoysticks();

        if (!isAstronaut && GetComponent<AdvancedMoveInputZone>() == null)
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
        EnsureNeutralRiderBootstrap();
        ConfigureNetworkRigidbodyAuthority();

        if (GetComponent<EnemyBot>() != null)
        {
            UpdateEngineAudio();
            return;
        }

        if (NeutralRiderController.IsNeutralRider(gameObject))
        {
            UpdateEngineAudio();
            return;
        }

        if (!IsAstronautMovementActor())
        {
            RefreshPilotSpeedBoost();
            SyncEquippedEngineProfile();
        }

        if (photonView.IsMine)
        {
            if (!IsGameStarted())
            {
                moveInput = Vector2.zero;
                shootInput = Vector2.zero;
                effectiveMoveInput = Vector2.zero;
                ResetAdvancedBoosterInputState();
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
                ResetAdvancedBoosterInputState();
                UpdateEngineAudio();
                return;
            }

            ResolveJoysticks();

            keyboardBoosterRequested = false;
            moveInput = joystick != null && joystick.IsPressed ? joystick.inputVector : Vector2.zero;
            if (moveInput == Vector2.zero)
                moveInput = GetKeyboardMoveInput();
            shootInput = shootJoystick != null && shootJoystick.IsPressed ? shootJoystick.inputVector : Vector2.zero;

            if (moveInput.sqrMagnitude < 0.0004f)
                moveInput = Vector2.zero;

            if (shootInput.magnitude < 0.3f)
                shootInput = Vector2.zero;

            UpdateAdvancedBoosterInputState();
            effectiveMoveInput = GetEffectiveMoveInput(moveInput);

            if (IsSuperBoosterActive())
            {
                boosterRecoveryDelayTimer = 0f;
                continuousBoosterTime = 0f;
                boosterActiveThisFrame = false;
                PublishAdvancedBoosterVisualState();
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
        if (GetComponent<EnemyBot>() != null || NeutralRiderController.IsNeutralRider(gameObject))
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
            if (HackingStatus.TryGetForcedMotion(gameObject, out Vector2 hackedDirection, out float hackedSpeedMultiplier))
            {
                ApplyHackedOverrideVelocity(currentSpeed, hackedDirection, hackedSpeedMultiplier);
                return;
            }

            if (IsSuperBoosterActive())
            {
                ApplySuperBoosterVelocity(currentSpeed);
                rb.angularVelocity = 0f;
                float superAngle = Mathf.Atan2(superBoosterDirection.y, superBoosterDirection.x) * Mathf.Rad2Deg - 90f;
                rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, superAngle, BaseTurnDegreesPerSecond * Time.fixedDeltaTime));
                return;
            }

            ApplyVelocity(currentSpeed);
            ApplyGravityWellShipDrift(1f);
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

    void ApplyHackedOverrideVelocity(float currentSpeed, Vector2 direction, float speedMultiplier)
    {
        if (rb == null)
            return;

        Vector2 safeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        float hackedSpeed = Mathf.Max(0.1f, currentSpeed * Mathf.Clamp(speedMultiplier, 0.35f, 1.15f));
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, safeDirection * hackedSpeed, 0.28f);
        ApplyGravityWellShipDrift(0.45f);
        rb.angularVelocity = 0f;
        float angle = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg - 90f;
        rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, angle, BaseTurnDegreesPerSecond * 0.82f * Time.fixedDeltaTime));

        boosterActiveThisFrame = false;
        continuousBoosterTime = 0f;
        lastBatteringEligibleSpeedTime = -999f;
        PublishAdvancedBoosterVisualState();
    }

    void OnDisable()
    {
        ResetAdvancedBoosterInputState();
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
            return;

        PhotonView targetView = targetHealth.photonView;
        if (targetView == null || targetView.ViewID == photonView.ViewID)
            return;

        if (nextLocalBatteringRequestTimeByTargetView.TryGetValue(targetView.ViewID, out float nextAllowedTime) && Time.time < nextAllowedTime)
            return;

        float reportedBoosterSeconds = continuousBoosterTime;
        nextLocalBatteringRequestTimeByTargetView[targetView.ViewID] = Time.time + BatteringPairCooldown;
        photonView.RPC(
            nameof(RequestBatteringImpact),
            RpcTarget.MasterClient,
            targetView.ViewID,
            impactPoint.x,
            impactPoint.y,
            reportedBoosterSeconds,
            rb.linearVelocity.magnitude);
        ResetBatteringCharge();
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
                    float reportedBoosterSeconds = continuousBoosterTime;
                    photonView.RPC(
                        nameof(RequestBatteringImpact),
                        RpcTarget.MasterClient,
                        targetView.ViewID,
                        impactPoint.x,
                        impactPoint.y,
                        reportedBoosterSeconds,
                        rb.linearVelocity.magnitude);
                    ResetBatteringCharge();
                    return;
                }
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
        WeaponHitContext hitContext = new WeaponHitContext(
            WeaponDamageType.Kinetic,
            WeaponDeliveryMethod.ContactDash,
            WeaponDeliveryFlags.None,
            PlayerHealth.PilotDamageSourceRamming);
        photonView.RPC(nameof(PlayBatteringImpactVisual), RpcTarget.All, impactX, impactY, impactNormal.x, impactNormal.y);
        targetHealth.photonView.RPC(
            nameof(PlayerHealth.TakeDamageWithContextAt),
            RpcTarget.MasterClient,
            targetDamage,
            photonView.ViewID,
            impactX,
            impactY,
            (int)hitContext.DamageType,
            (int)hitContext.DeliveryMethod,
            (int)hitContext.DeliveryFlags,
            hitContext.DamageSource ?? string.Empty);
        photonView.RPC(
            nameof(PlayerHealth.TakeDamageWithContextAt),
            RpcTarget.MasterClient,
            batteringDamage,
            -1,
            impactX,
            impactY,
            (int)hitContext.DamageType,
            (int)hitContext.DeliveryMethod,
            (int)hitContext.DeliveryFlags,
            hitContext.DamageSource ?? string.Empty);
    }

    [PunRPC]
    void RequestObstacleBatteringImpact(string obstacleStableId, float impactX, float impactY, float reportedBoosterSeconds, float reportedSpeed, PhotonMessageInfo messageInfo)
    {
        // Kept as a no-op so stale RPCs cannot damage obstacles or the ramming player.
        return;
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

    void UpdateBatteringSpeedEligibility()
    {
        if (continuousBoosterTime >= BatteringRequiredBoosterSeconds && rb != null && IsAtBatteringSpeed(rb.linearVelocity.magnitude))
            lastBatteringEligibleSpeedTime = Time.time;
    }

    void ResetBatteringCharge()
    {
        continuousBoosterTime = 0f;
        lastBatteringEligibleSpeedTime = -999f;
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

    int GetCurrentBrakingDriftLevel()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        return ShipCatalog.GetBrakingDriftLevel(shipSkinIndex);
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
        if (!enabled || photonView == null || !photonView.IsMine || GetComponent<EnemyBot>() != null || NeutralRiderController.IsNeutralRider(gameObject))
            return false;

        if (!IsGameStarted() || IsStartingEntryActive())
            return false;

        PlayerHealth health = GetComponent<PlayerHealth>();
        return health == null ||
               (!health.IsWreck &&
                !health.IsEvacuationAnimating &&
                !health.IsAstronautControlled &&
                health.CurrentHP > 0);
    }

    bool IsAstronautMovementActor()
    {
        ActorIdentity identity = GetComponent<ActorIdentity>();
        return (identity != null && identity.IsAstronaut) ||
               GetComponent<AstronautSurvivor>() != null ||
               AstronautSurvivor.IsAstronautInstantiationData(photonView != null ? photonView.InstantiationData : null);
    }

    bool IsBoosterSuppressedForLocalActor()
    {
        if (IsAstronautMovementActor())
            return true;

        PlayerHealth health = GetComponent<PlayerHealth>();
        return health != null &&
               (health.IsWreck ||
                health.IsEvacuationAnimating ||
                health.IsAstronautControlled ||
                health.CurrentHP <= 0);
    }

    bool IsStartingEntryActive()
    {
        StartingShipEntryVfx entry = GetComponent<StartingShipEntryVfx>();
        return entry != null && entry.IsControllingMotion;
    }

    void UpdateAdvancedBoosterInputState()
    {
        bool advancedEnabled = RoomSettings.IsAdvancedBoosterEnabled() && !IsBoosterSuppressedForLocalActor();
        advancedBoosterInputRatio = advancedEnabled ? GetAdvancedBoosterInputRatio() : 0f;
        advancedBoosterRequested = advancedEnabled && advancedBoosterInputRatio >= AdvancedBoosterActivationThreshold;
        boosterActiveThisFrame = false;
        PublishAdvancedBoosterVisualState();
    }

    void ResetAdvancedBoosterInputState()
    {
        keyboardBoosterRequested = false;
        advancedBoosterRequested = false;
        boosterActiveThisFrame = false;
        advancedBoosterInputRatio = 0f;
        PublishAdvancedBoosterVisualState();
    }

    float GetAdvancedBoosterInputRatio()
    {
        if (joystick != null && joystick.IsPressed)
            return joystick.rawInputVector.magnitude;

        return keyboardBoosterRequested && moveInput.sqrMagnitude > 0.0004f
            ? AdvancedBoosterOuterInputLimit
            : 0f;
    }

    void PublishAdvancedBoosterVisualState()
    {
        if (photonView == null || !photonView.IsMine)
            return;

        bool advancedEnabled = RoomSettings.IsAdvancedBoosterEnabled() && !IsBoosterSuppressedForLocalActor();
        LocalAdvancedBoosterEnabled = advancedEnabled;
        LocalAdvancedBoosterInputRatio = advancedEnabled ? advancedBoosterInputRatio : 0f;
        LocalAdvancedBoosterActive = advancedEnabled && boosterActiveThisFrame;
        LocalAdvancedBoosterAvailable = advancedEnabled && !boosterExhausted && boosterCharge > 0.001f;
    }

    void UpdateBooster(float deltaTime)
    {
        ShipDamageState damageState = GetComponent<ShipDamageState>();
        float boosterLimit = damageState != null ? damageState.GetBoosterChargeLimit() : 1f;
        boosterActiveThisFrame = false;
        if (damageState != null && damageState.IsBoosterDisabled())
        {
            boosterCharge = 0f;
            boosterExhausted = true;
            boosterRecoveryDelayTimer = 0f;
            continuousBoosterTime = 0f;
            lastBatteringEligibleSpeedTime = -999f;
            PublishAdvancedBoosterVisualState();
            return;
        }

        if (IsBoosterSuppressedForLocalActor())
        {
            boosterCharge = 0f;
            boosterExhausted = false;
            boosterRecoveryDelayTimer = 0f;
            continuousBoosterTime = 0f;
            lastBatteringEligibleSpeedTime = -999f;
            ResetAdvancedBoosterInputState();
            return;
        }

        bool advancedBoosterEnabled = RoomSettings.IsAdvancedBoosterEnabled();
        bool usingFullAcceleration = advancedBoosterEnabled
            ? advancedBoosterRequested
            : effectiveMoveInput.magnitude >= maxSpeedThreshold;
        bool usingBooster = usingFullAcceleration && !boosterExhausted;

        if (usingBooster)
        {
            boosterActiveThisFrame = true;
            boosterCharge -= (deltaTime * GetBoosterDrainMultiplier()) / boosterDuration;
            boosterRecoveryDelayTimer = GetCurrentBoosterRecoveryDelay();
            continuousBoosterTime += deltaTime;
            AshPilotRoundTracker.RecordBoosterSeconds(deltaTime);
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

        boosterCharge = Mathf.Clamp(boosterCharge, 0f, boosterLimit);

        if (!boosterExhausted && boosterCharge <= 0.001f)
        {
            boosterExhausted = true;
            boosterRecoveryDelayTimer = GetCurrentBoosterRecoveryDelay();
        }
        else if (boosterExhausted && boosterCharge >= boosterRecoveryThreshold)
        {
            boosterExhausted = false;
        }

        PublishAdvancedBoosterVisualState();
    }

    Vector2 GetEffectiveMoveInput(Vector2 rawInput)
    {
        if (rawInput == Vector2.zero)
            return Vector2.zero;

        ShipDamageState damageState = GetComponent<ShipDamageState>();
        if (damageState != null)
            rawInput = damageState.ApplySteeringDamage(rawInput);

        if (IsBoosterSuppressedForLocalActor())
            return rawInput;

        if (RoomSettings.IsAdvancedBoosterEnabled())
        {
            return advancedBoosterRequested && rawInput.magnitude >= fullSpeedSnapThreshold
                ? rawInput.normalized * GetCurrentBoostSpeedMultiplier()
                : rawInput;
        }

        if (rawInput.magnitude >= fullSpeedSnapThreshold)
        {
            return rawInput.normalized * GetCurrentBoostSpeedMultiplier();
        }

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
        keyboardBoosterRequested = keyboardInput.sqrMagnitude > 0.0001f &&
                                   (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            keyboardInput.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            keyboardInput.x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            keyboardInput.y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            keyboardInput.y += 1f;
        keyboardBoosterRequested = keyboardInput.sqrMagnitude > 0.0001f &&
                                   (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
#endif

        return keyboardInput.sqrMagnitude > 1f ? keyboardInput.normalized : keyboardInput;
#else
        return Vector2.zero;
#endif
    }

    float GetCurrentBoostSpeedMultiplier()
    {
        ShipDamageState damageState = GetComponent<ShipDamageState>();
        if (damageState != null && damageState.IsBoosterDisabled())
            return 1f;

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
        ShipDamageState damageState = GetComponent<ShipDamageState>();
        if (damageState != null)
            currentSpeed *= damageState.GetEngineSpeedMultiplier();

        HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>();
        if (nebulaTarget != null)
            currentSpeed *= nebulaTarget.CurrentNebulaSpeedMultiplier;

        if (photonView != null)
            currentSpeed *= BisonIndustrialPlotController.GetHaulSpeedMultiplier(photonView.ViewID);

        return Mathf.Max(0.1f, currentSpeed);
    }

    void ApplyVelocity(float currentSpeed)
    {
        Vector2 targetVelocity = effectiveMoveInput * currentSpeed;
        int driftLevel = GetCurrentBrakingDriftLevel();

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

    void ApplyGravityWellShipDrift(float effectMultiplier)
    {
        if (rb == null || IsAstronautMovementActor())
            return;

        GravityWellPhysicsField.ApplyShipDrift(rb, effectMultiplier);
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
        if (Time.time < invaderResonanceDriftUntil)
            response *= 0.42f;

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
        movementJoystick.recenterOnPointerDown = true;
        movementJoystick.maxRawInputMagnitude = RoomSettings.IsAdvancedBoosterEnabled() && !IsBoosterSuppressedForLocalActor()
            ? AdvancedBoosterOuterInputLimit
            : 1f;
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

        float engineVolume = Mathf.Lerp(EngineAudioIdleVolume, EngineAudioFullVolume, normalizedSpeed);
        engineAudioSource.volume = Mathf.Clamp01(engineVolume * EngineAudioVolumeMultiplier);
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

    void EnsureNeutralRiderBootstrap()
    {
        if (!NeutralRiderController.IsNeutralRiderInstantiationData(photonView != null ? photonView.InstantiationData : null))
            return;

        NeutralRiderController rider = GetComponent<NeutralRiderController>();
        if (rider == null)
            rider = gameObject.AddComponent<NeutralRiderController>();

        rider.InitializeFromPhotonData();
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
        if (GetComponent<EnemyBot>() != null || NeutralRiderController.IsNeutralRider(gameObject) || GetComponent<AstronautSurvivor>() != null)
            return;

        pilotSpeedBoostMultiplier = Mathf.Max(pilotSpeedBoostMultiplier, Mathf.Max(1f, multiplier));
        pilotSpeedBoostUntil = Mathf.Max(pilotSpeedBoostUntil, Time.time + Mathf.Max(0f, duration));
        SyncEquippedEngineProfile(forceRefresh: true);
    }

    public void ActivateSuperBooster(float duration)
    {
        if (!photonView.IsMine || GetComponent<EnemyBot>() != null || NeutralRiderController.IsNeutralRider(gameObject) || GetComponent<AstronautSurvivor>() != null)
            return;

        ShipDamageState damageState = GetComponent<ShipDamageState>();
        if (damageState != null && damageState.IsBoosterDisabled())
            return;

        Vector2 forward = transform.up;
        if (forward.sqrMagnitude < 0.001f)
            forward = lastFacingDirection.sqrMagnitude > 0.001f ? lastFacingDirection : Vector2.up;

        superBoosterDirection = forward.normalized;
        superBoosterUntil = Mathf.Max(superBoosterUntil, Time.time + Mathf.Max(0.05f, duration));
        DynamicCameraZoomController.RequestGlobal(DynamicCameraZoomProfiles.SuperBooster, duration);

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.TriggerBoostBurst(Mathf.Min(0.72f, Mathf.Max(0.24f, duration)), 1f);
    }

    public void RefillBooster()
    {
        if (!photonView.IsMine || GetComponent<EnemyBot>() != null || NeutralRiderController.IsNeutralRider(gameObject) || GetComponent<AstronautSurvivor>() != null)
            return;

        ShipDamageState damageState = GetComponent<ShipDamageState>();
        float boosterLimit = damageState != null ? damageState.GetBoosterChargeLimit() : 1f;
        boosterCharge = boosterLimit;
        boosterExhausted = boosterLimit <= 0.001f;
        boosterRecoveryDelayTimer = 0f;
        continuousBoosterTime = 0f;
    }

    public float DrainInvaderAssimilationBooster(float normalizedAmount)
    {
        if (!photonView.IsMine || normalizedAmount <= 0f || GetComponent<EnemyBot>() != null || NeutralRiderController.IsNeutralRider(gameObject) || GetComponent<AstronautSurvivor>() != null)
            return 0f;

        ShipDamageState damageState = GetComponent<ShipDamageState>();
        if (damageState != null && damageState.IsBoosterDisabled())
            return 0f;

        float previousCharge = boosterCharge;
        boosterCharge = Mathf.Max(0f, boosterCharge - normalizedAmount);
        if (boosterCharge <= 0.001f)
        {
            boosterExhausted = true;
            boosterRecoveryDelayTimer = GetCurrentBoosterRecoveryDelay();
        }

        PublishAdvancedBoosterVisualState();
        return Mathf.Max(0f, previousCharge - boosterCharge);
    }

    public void ApplyInvaderResonanceDrift(float duration, Vector2 impulse)
    {
        if (!photonView.IsMine || GetComponent<EnemyBot>() != null || NeutralRiderController.IsNeutralRider(gameObject) || GetComponent<AstronautSurvivor>() != null)
            return;

        invaderResonanceDriftUntil = Mathf.Max(invaderResonanceDriftUntil, Time.time + Mathf.Max(0f, duration));
        if (rb != null && impulse.sqrMagnitude > 0.0001f)
            rb.AddForce(Vector2.ClampMagnitude(impulse, 5.5f), ForceMode2D.Impulse);
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

    float GetBoosterDrainMultiplier()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        float multiplier = PilotCatalog.IsSelectedPilot(owner, PilotCatalog.AshId) ? AshCleanBurnBoosterDrainMultiplier : 1f;
        if (PilotCatalog.IsSelectedPilot(owner, PilotCatalog.AtlasId) &&
            PlayerProfileService.HasInstance &&
            AtlasPilotRoundTracker.HasCargoRunBonus(PlayerProfileService.Instance.CurrentProfile))
        {
            multiplier *= AtlasPilotRoundTracker.BoosterDrainMultiplier;
        }

        return multiplier;
    }

    void SyncEquippedEngineProfile(bool forceRefresh = false)
    {
        if (GetComponent<EnemyBot>() != null || NeutralRiderController.IsNeutralRider(gameObject) || GetComponent<AstronautSurvivor>() != null)
            return;

        CaptureBaseMovementProfile();

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        int powerCount = CountEquippedEngineItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.PowerEngineId);
        int ionCount = CountEquippedEngineItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.IonEngineId);
        int fusionCount = CountEquippedEngineItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.FusionEngineId);
        int fuelTankCount = CountEquippedEngineItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.FuelTankId);
        int hybridCount = CountEquippedEngineItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.HybridEngineId);
        int doubleEngineCount = CountEquippedEngineItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.DoubleEngineId);
        int superBoosterCount = CountEquippedEngineItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.SuperBoosterId);
        int afterburnerStabilizerCount = CountEquippedEngineItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.AfterburnerStabilizerId);
        int blackMarketThrusterCount = CountEquippedEngineItem(equipmentSlots, shipSkinIndex, InventoryItemCatalog.BlackMarketThrusterId);
        int shieldSpeedPenaltyPercent = InventoryItemCatalog.GetEquippedShieldSpeedPenaltyPercent(equipmentSlots, shipSkinIndex);
        string engineTrailItemId = ResolveEngineTrailItemId(equipmentSlots, shipSkinIndex);
        bool hasFusion = fusionCount > 0;
        float shockSpeedMultiplier = ElectromagneticShockStatus.GetSpeedMultiplier(gameObject);
        float atlasSuppressionSpeedMultiplier = AtlasSuppressionStatus.GetSpeedMultiplier(gameObject);
        float ashSuperchargeSpeedMultiplier = AshSuperchargeStatus.GetSpeedMultiplier(gameObject);
        string signature = shipSkinIndex + ":" +
                           powerCount + ":" +
                           ionCount + ":" +
                           fusionCount + ":" +
                           fuelTankCount + ":" +
                           hybridCount + ":" +
                           doubleEngineCount + ":" +
                           superBoosterCount + ":" +
                           afterburnerStabilizerCount + ":" +
                           blackMarketThrusterCount + ":" +
                           shieldSpeedPenaltyPercent + ":" +
                           engineTrailItemId + ":" +
                           Mathf.RoundToInt(shockSpeedMultiplier * 1000f) + ":" +
                           Mathf.RoundToInt(atlasSuppressionSpeedMultiplier * 1000f) + ":" +
                           Mathf.RoundToInt(ashSuperchargeSpeedMultiplier * 1000f);
        if (!forceRefresh && signature == lastAppliedEngineSignature)
            return;

        float baseShipSpeed = ShipCatalog.GetBaseSpeed(shipSkinIndex);
        float baseShipBoosterDuration = ShipCatalog.GetBoosterDuration(shipSkinIndex);
        float baseShipTurnRate = ShipCatalog.GetTurnRateMultiplier(shipSkinIndex);
        int baseShipMaxBoostPercent = ShipCatalog.GetMaxBoostPercent(shipSkinIndex);

        equippedPowerEngineCount = powerCount;
        equippedIonEngineCount = ionCount;
        equippedFusionEngineCount = fusionCount;
        equippedFuelTankCount = fuelTankCount;
        equippedHybridEngineCount = hybridCount;
        equippedDoubleEngineCount = doubleEngineCount;
        equippedSuperBoosterCount = superBoosterCount;
        equippedAfterburnerStabilizerCount = afterburnerStabilizerCount;
        equippedBlackMarketThrusterCount = blackMarketThrusterCount;
        equippedEngineTrailItemId = engineTrailItemId;
        fusionEngineEquipped = hasFusion;
        baseSpeed = Mathf.Max(0.1f, baseShipSpeed);
        baseBoosterDuration = Mathf.Max(0.1f, baseShipBoosterDuration);
        float engineSpeedBonus = Mathf.Min(MaxEngineSpeedBonus,
            PowerEngineSpeedBonus * equippedPowerEngineCount +
            IonEngineSpeedBonus * equippedIonEngineCount +
            FusionEngineSpeedBonus * equippedFusionEngineCount +
            HybridEngineSpeedBonus * equippedHybridEngineCount +
            DoubleEngineSpeedBonus * equippedDoubleEngineCount +
            BlackMarketThrusterSpeedBonus * equippedBlackMarketThrusterCount);
        float engineTurnRateBonus =
            AfterburnerTurnRateBonus * equippedAfterburnerStabilizerCount +
            IonEngineTurnRateBonus * equippedIonEngineCount -
            DoubleEngineTurnRatePenalty * equippedDoubleEngineCount -
            BlackMarketThrusterTurnRatePenalty * equippedBlackMarketThrusterCount;
        int engineBoostPercentBonus = Mathf.Min(MaxEngineBoostPercentBonus,
            PowerEngineBoostPercentBonus * equippedPowerEngineCount +
            DoubleEngineBoostPercentBonus * equippedDoubleEngineCount +
            BlackMarketThrusterBoostPercentBonus * equippedBlackMarketThrusterCount);
        float engineBoosterDurationBonus =
            FuelTankBoosterDurationBonus * equippedFuelTankCount +
            HybridEngineBoosterDurationBonus * equippedHybridEngineCount -
            DoubleEngineBoosterDurationPenalty * equippedDoubleEngineCount;
        float shieldSpeedMultiplier = Mathf.Clamp(1f - shieldSpeedPenaltyPercent / 100f, 0.25f, 1f);

        turnRateMultiplier = Mathf.Max(0.1f, baseShipTurnRate * Mathf.Max(0.1f, 1f + engineTurnRateBonus));
        maxBoostPercent = Mathf.Max(0, baseShipMaxBoostPercent + engineBoostPercentBonus);
        speed = baseSpeed * (1f + engineSpeedBonus) * GetPilotSpeedMultiplier() * shockSpeedMultiplier * atlasSuppressionSpeedMultiplier * ashSuperchargeSpeedMultiplier * shieldSpeedMultiplier;
        boosterDuration = baseBoosterDuration * Mathf.Max(0.25f, 1f + engineBoosterDurationBonus);
        lastAppliedEngineSignature = signature;
        SyncEngineAudioClip();

        EngineThrusterVFX thrusterVfx = GetComponent<EngineThrusterVFX>();
        if (thrusterVfx != null)
            thrusterVfx.RefreshMode();
    }

    string ResolveEngineTrailItemId(string[] equipmentSlots, int shipSkinIndex)
    {
        string selectedItemId = string.Empty;
        int selectedPriority = 0;
        for (int i = 4; i <= 5; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            string itemId = GetEquipmentItem(equipmentSlots, i);
            int priority = GetEngineTrailPriority(itemId);
            if (priority > selectedPriority)
            {
                selectedPriority = priority;
                selectedItemId = itemId;
            }
        }

        return selectedItemId ?? string.Empty;
    }

    int GetEngineTrailPriority(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return 0;

        if (string.Equals(itemId, InventoryItemCatalog.DoubleEngineId, StringComparison.Ordinal))
            return 80;
        if (string.Equals(itemId, InventoryItemCatalog.HybridEngineId, StringComparison.Ordinal))
            return 70;
        if (string.Equals(itemId, InventoryItemCatalog.FusionEngineId, StringComparison.Ordinal))
            return 60;
        if (string.Equals(itemId, InventoryItemCatalog.PowerEngineId, StringComparison.Ordinal))
            return 50;
        if (string.Equals(itemId, InventoryItemCatalog.IonEngineId, StringComparison.Ordinal))
            return 45;
        if (string.Equals(itemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
            return 40;
        if (string.Equals(itemId, InventoryItemCatalog.AfterburnerStabilizerId, StringComparison.Ordinal))
            return 30;
        if (string.Equals(itemId, InventoryItemCatalog.FuelTankId, StringComparison.Ordinal))
            return 20;

        return 0;
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
        ShipDamageState damageState = GetComponent<ShipDamageState>();
        if (damageState != null && damageState.IsBoosterDisabled())
            return false;

        return Time.time < superBoosterUntil;
    }

    void ApplySuperBoosterVelocity(float currentSpeed)
    {
        if (rb == null)
            return;

        Vector2 direction = superBoosterDirection.sqrMagnitude > 0.001f ? superBoosterDirection.normalized : Vector2.up;
        float boostedSpeed = Mathf.Max(0.1f, currentSpeed) * 3f;
        rb.linearVelocity = direction * boostedSpeed;
        ApplyGravityWellShipDrift(0.25f);
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
        float engineBonus =
            FusionEngineRecoveryDelayBonus * equippedFusionEngineCount +
            IonEngineRecoveryDelayBonus * equippedIonEngineCount +
            HybridEngineRecoveryDelayBonus * equippedHybridEngineCount;
        float delay = Mathf.Max(0f, baseDelay - engineBonus - pilotBonus);
        if (equippedAfterburnerStabilizerCount > 0)
            delay *= Mathf.Pow(AfterburnerBoosterRecoveryDelayMultiplier, equippedAfterburnerStabilizerCount);

        return Mathf.Max(0f, delay);
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

        if (equippedBlackMarketThrusterCount > 0)
            return AudioManager.Instance.BlackMarketThrusterEngineClip;

        if (equippedDoubleEngineCount > 0)
            return AudioManager.Instance.DoubleEngineClip;

        if (equippedHybridEngineCount > 0)
            return AudioManager.Instance.HybridEngineClip;

        if (equippedFusionEngineCount > 0)
            return AudioManager.Instance.FusionEngineClip;

        if (equippedPowerEngineCount > 0)
            return AudioManager.Instance.PowerEngineClip;

        if (equippedIonEngineCount > 0)
            return AudioManager.Instance.IonEngineClip;

        if (equippedSuperBoosterCount > 0)
            return AudioManager.Instance.SuperBoosterEngineClip;

        return AudioManager.Instance.EngineClip;
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

    [PunRPC]
    public void TeleportToMapInstanceRpc(float x, float y, string instanceId)
    {
        Vector2 target = new Vector2(x, y);
        MapInstanceService.ConfigureMember(gameObject, instanceId);

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.position = target;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        transform.position = new Vector3(target.x, target.y, transform.position.z);
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null)
            health.BeginMapTravelArrivalProtection();

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.TriggerBoostBurst(0.42f, 1.6f);

        CameraFollow camera = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (camera != null && photonView != null && photonView.IsMine)
            camera.SnapToTarget();
    }
}
