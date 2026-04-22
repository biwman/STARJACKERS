using UnityEngine;
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
    const float AccelerationResponsiveness = 18f;
    const float LowSpeedBrakeResponsiveness = 7.4f;
    const float HighSpeedBrakeResponsiveness = 1.15f;
    static PhysicsMaterial2D playerCollisionMaterial;

    public float BoosterNormalized => boosterCharge;
    public bool IsBoosterDepleted => boosterExhausted;
    float CurrentDepletedSpeedMultiplier => 1f - (RoomSettings.GetBoosterSlowdownPercent() / 100f);

    void Start()
    {
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

        if (GetComponent<EngineThrusterVFX>() == null)
        {
            gameObject.AddComponent<EngineThrusterVFX>();
        }

        targetRotationAngle = transform.eulerAngles.z;

        SetupEngineAudio();
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
        if (GetComponent<EnemyBot>() != null)
        {
            UpdateEngineAudio();
            return;
        }

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
            rb.angularVelocity = 0f;
            rb.MoveRotation(targetRotationAngle);
        }
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

        bool usingBooster = effectiveMoveInput.magnitude >= maxSpeedThreshold && !boosterExhausted;

        if (usingBooster)
        {
            boosterCharge -= deltaTime / boosterDuration;
            boosterRecoveryDelayTimer = RoomSettings.GetBoosterRecoveryDelay();
        }
        else if (!boosterExhausted || boosterRecoveryDelayTimer <= 0f)
        {
            boosterCharge += deltaTime / boosterDuration;
        }
        else
        {
            boosterRecoveryDelayTimer -= deltaTime;
        }

        boosterCharge = Mathf.Clamp01(boosterCharge);

        if (!boosterExhausted && boosterCharge <= 0.001f)
        {
            boosterExhausted = true;
            boosterRecoveryDelayTimer = RoomSettings.GetBoosterRecoveryDelay();
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
        return 1f + (RoomSettings.GetMaxInputBoostPercent() / 100f);
    }

    void ApplyVelocity(float currentSpeed)
    {
        Vector2 targetVelocity = effectiveMoveInput * currentSpeed;

        if (!RoomSettings.IsShipDriftEnabled())
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
            float brakeResponsiveness = Mathf.Lerp(LowSpeedBrakeResponsiveness, HighSpeedBrakeResponsiveness, driftWeight);
            float releaseDriftMultiplier = effectiveMoveInput == Vector2.zero ? 0.86f : 1f;
            float maxDelta = brakeResponsiveness * speed * releaseDriftMultiplier * Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, maxDelta);
            return;
        }

        float accelerationDelta = AccelerationResponsiveness * speed * Time.fixedDeltaTime;
        rb.linearVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, accelerationDelta);
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
            transform.rotation = Quaternion.Euler(0f, 0f, targetRotationAngle);
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

        AudioClip engineClip = AudioManager.Instance.EngineClip;
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

        if (!engineAudioSource.isPlaying)
            engineAudioSource.Play();

        engineAudioSource.volume = Mathf.Lerp(0.12f, 0.42f, normalizedSpeed);
        engineAudioSource.pitch = Mathf.Lerp(0.88f, 1.24f, normalizedSpeed);
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
    bool isViper;
    EnemyTrailProfile enemyTrailProfile;

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

        PhotonView view = GetComponent<PhotonView>();
        int skinIndex = view != null && view.Owner != null ? RoomSettings.GetPlayerShipSkin(view.Owner, 0) : 0;
        isViper = !isEnemyBot && !isAstronaut && ShipCatalog.GetShipTypeFromSkinIndex(skinIndex) == ShipType.Viper;
    }

    void Update()
    {
        if (rb == null)
            return;

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
            : isViper
            ? new[]
            {
                new Vector3(-shipWidth * 0.60f, 0.34f, 0f),
                new Vector3(shipWidth * 0.60f, 0.34f, 0f)
            }
            : new[] { new Vector3(0f, 0.02f, 0f) };

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
        bool hideForOthers = nebulaTarget != null && nebulaTarget.IsHiddenForOthers && view != null && !view.IsMine;
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
                trailRenderer.time = Mathf.Lerp(0.22f, 0.82f, intensity);
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
