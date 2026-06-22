using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PhotonView))]
public class AstronautSurvivor : MonoBehaviourPun
{
    public const string AstronautInstantiationMarker = "astronaut_survivor";
    public const string EnemyAstronautInstantiationMarker = "enemy_astronaut_survivor";
    const float AstronautTargetSize = 0.56f;
    const float EnemyAstronautTargetSize = AstronautTargetSize * 0.9f;
    const float EscapePodTargetSize = 0.75f;
    const int EnemyAstronautHp = 14;
    const float PlayerAstronautReferenceMovementSpeed = 5f;
    const float PlayerAstronautSpeedDivisor = 3f;
    const float PlayerAstronautMinimumSpeed = 1.2f;
    static float EnemyAstronautSpeed => ResolvePlayerAstronautSpeed();
    const float EnemyAstronautArrivalDistance = 0.58f;
    const float EnemyAstronautAvoidPlayerRadius = 4.8f;
    const float EnemyAstronautAvoidPlayerWeight = 1.35f;
    const float EnemyAstronautAvoidObjectRadius = 1.35f;
    const float EnemyAstronautAvoidObjectWeight = 1.9f;
    const float EnemyAstronautMapEdgeMargin = 2.2f;
    const float EnemyAstronautEvacuationDuration = 0.85f;
    const float EnemyAstronautTargetJitterRadius = 0.62f;
    const float EnemyAstronautSpawnClearanceRadius = 0.28f;
    const float EnemyAstronautSpawnChancePerSlot = 0.5f;
    const float EnemyAstronautPhysicsMass = 0.035f;
    const string EnemyAstronautSpriteResourcePath = "Astronauts/enemy_astronauts_multiasset";
    const int EscapePodHpBonus = 50;
    const float EscapePodSpeedMultiplier = 3f;
    const float SurvivorSpawnProtectionDuration = 1.25f;
    const float EmergencySuitBeaconProtectionDuration = 3f;
    const float EmergencySuitBeaconSpeedMultiplier = 1.35f;

    static Sprite cachedAstronautSprite;
    static Sprite cachedEscapePodSprite;
    static Sprite[] cachedEnemyAstronautSprites;
    bool initialized;
    bool isEscapePod;
    bool isEnemySurvivor;
    bool hasEnemyTarget;
    bool enemyEvacuationStarted;
    int enemyVariantIndex;
    Vector2 enemyTargetPosition;
    SpriteRenderer cachedRenderer;
    Rigidbody2D cachedBody;
    PlayerHealth cachedHealth;
    Coroutine enemyEvacuationRoutine;

    public bool IsEscapePodMode => isEscapePod;
    public bool IsEnemySurvivor => isEnemySurvivor;
    public int EnemyVariantIndex => enemyVariantIndex;
    public float VisualTargetSize => isEnemySurvivor ? EnemyAstronautTargetSize : isEscapePod ? EscapePodTargetSize : AstronautTargetSize;

    public static bool IsAstronautInstantiationData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               (marker == AstronautInstantiationMarker || marker == EnemyAstronautInstantiationMarker);
    }

    public static bool IsEnemyAstronautInstantiationData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               marker == EnemyAstronautInstantiationMarker;
    }

    public static bool IsEnemyAstronaut(PlayerHealth health)
    {
        if (health == null)
            return false;

        AstronautSurvivor survivor = health.GetComponent<AstronautSurvivor>();
        if (survivor != null && survivor.IsEnemySurvivor)
            return true;

        PhotonView view = health.photonView != null ? health.photonView : health.GetComponent<PhotonView>();
        return IsEnemyAstronautInstantiationData(view != null ? view.InstantiationData : null);
    }

    public static bool IsEscapePodInstantiationData(object[] data)
    {
        return IsAstronautInstantiationData(data) && HasEscapePod(data);
    }

    public Sprite GetVisualSprite()
    {
        if (isEnemySurvivor)
            return LoadEnemyAstronautSprite(enemyVariantIndex);

        return isEscapePod ? LoadEscapePodSprite() : LoadAstronautSprite();
    }

    public void InitializeFromPhotonData()
    {
        if (initialized)
            return;

        initialized = true;
        object[] instantiationData = photonView != null ? photonView.InstantiationData : null;
        isEnemySurvivor = IsEnemyAstronautInstantiationData(instantiationData);
        bool hasEmergencySuitBeacon = !isEnemySurvivor && HasEmergencySuitBeacon(instantiationData);
        isEscapePod = !isEnemySurvivor && HasEscapePod(instantiationData);
        ResolveEnemyAstronautData(instantiationData);
        ActorIdentity.Ensure(gameObject);

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        cachedRenderer = renderer;
        if (renderer != null)
        {
            Sprite visualSprite = GetVisualSprite();
            if (visualSprite != null)
            {
                renderer.sprite = visualSprite;
                renderer.color = Color.white;
                FitRendererToTargetSize(renderer, VisualTargetSize);
            }
        }

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            if (isEnemySurvivor)
            {
                movement.StopEngineAudioImmediately();
                movement.enabled = false;
            }
            else
            {
                float astronautSpeed = Mathf.Max(PlayerAstronautMinimumSpeed, movement.speed / PlayerAstronautSpeedDivisor);
                float survivorSpeed = isEscapePod ? astronautSpeed * EscapePodSpeedMultiplier : astronautSpeed;
                if (hasEmergencySuitBeacon)
                    survivorSpeed *= EmergencySuitBeaconSpeedMultiplier;

                movement.speed = survivorSpeed;
                movement.boosterDuration = 9999f;
            }
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        if (isEnemySurvivor)
        {
            TreasureCollector collector = GetComponent<TreasureCollector>();
            if (collector != null)
                collector.DisableForEnemyAstronaut();
        }

        ShipInventoryHudUI cargoHud = GetComponent<ShipInventoryHudUI>();
        if (cargoHud != null)
            cargoHud.enabled = false;

        BoosterBarUI boosterUi = GetComponent<BoosterBarUI>();
        if (boosterUi != null)
            boosterUi.enabled = false;

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster == null)
            thruster = gameObject.AddComponent<EngineThrusterVFX>();
        if (thruster != null)
            thruster.RefreshMode();

        cachedHealth = GetComponent<PlayerHealth>();
        if (cachedHealth != null)
        {
            int astronautHp = isEnemySurvivor
                ? EnemyAstronautHp
                : PilotCatalog.IsSelectedPilot(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer, PilotCatalog.JakeId) ? 60 : 20;
            if (!isEnemySurvivor && isEscapePod)
                astronautHp += EscapePodHpBonus;

            cachedHealth.ConfigureBaseStats(astronautHp, 0);
            float protectionDuration = isEnemySurvivor ? 0f : SurvivorSpawnProtectionDuration;
            if (hasEmergencySuitBeacon)
                protectionDuration = Mathf.Max(protectionDuration, EmergencySuitBeaconProtectionDuration);
            if (protectionDuration > 0f)
                cachedHealth.BeginEquipmentInvulnerabilityLocal(protectionDuration);
        }

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        cachedBody = body;
        if (body != null)
        {
            body.mass = isEnemySurvivor ? EnemyAstronautPhysicsMass : 0.01f;
            body.linearDamping = isEnemySurvivor ? 1.85f : 0.9f;
            body.angularDamping = isEnemySurvivor ? 4f : 1f;
            if (isEnemySurvivor)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.simulated = true;
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }

        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null && renderer != null)
        {
            Vector2 spriteSize = renderer.bounds.size;
            Vector2 colliderFactor = isEscapePod ? new Vector2(0.52f, 0.68f) : new Vector2(0.42f, 0.58f);
            SetWorldBoxSize(boxCollider, new Vector2(spriteSize.x * colliderFactor.x, spriteSize.y * colliderFactor.y));
        }

        CircleCollider2D triggerCollider = GetComponent<CircleCollider2D>();
        if (triggerCollider != null)
            triggerCollider.enabled = false;

    }

    void ResolveEnemyAstronautData(object[] data)
    {
        enemyVariantIndex = 0;
        hasEnemyTarget = false;
        enemyTargetPosition = transform.position;

        if (!isEnemySurvivor)
            return;

        if (data != null && data.Length > 1 && TryConvertToInt(data[1], out int variant))
            enemyVariantIndex = Mathf.Abs(variant) % Mathf.Max(1, GetEnemyAstronautSpriteCount());

        if (data != null &&
            data.Length > 3 &&
            TryConvertToFloat(data[2], out float targetX) &&
            TryConvertToFloat(data[3], out float targetY))
        {
            enemyTargetPosition = new Vector2(targetX, targetY);
            hasEnemyTarget = true;
        }
    }

    void FixedUpdate()
    {
        if (!initialized || !isEnemySurvivor || enemyEvacuationStarted)
            return;

        if (PhotonNetwork.InRoom && photonView != null && !photonView.IsMine)
            return;

        if (cachedHealth == null)
            cachedHealth = GetComponent<PlayerHealth>();
        if (cachedHealth != null && (cachedHealth.IsWreck || cachedHealth.IsEvacuationAnimating || cachedHealth.CurrentHP <= 0))
            return;

        if (cachedBody == null)
            cachedBody = GetComponent<Rigidbody2D>();

        if (!hasEnemyTarget)
        {
            enemyTargetPosition = ResolveFallbackEnemyAstronautTarget(transform.position, enemyVariantIndex);
            hasEnemyTarget = true;
        }

        Vector2 currentPosition = cachedBody != null ? cachedBody.position : (Vector2)transform.position;
        if (Vector2.Distance(currentPosition, enemyTargetPosition) <= EnemyAstronautArrivalDistance)
        {
            RequestEnemyAstronautEvacuation(enemyTargetPosition);
            return;
        }

        Vector2 moveDirection = ResolveEnemyAstronautMoveDirection(currentPosition);
        if (moveDirection.sqrMagnitude <= 0.001f)
            return;

        Vector2 desiredVelocity = moveDirection.normalized * EnemyAstronautSpeed;
        if (cachedBody != null)
        {
            cachedBody.linearVelocity = desiredVelocity;
        }
        else
        {
            transform.position += (Vector3)(desiredVelocity * Time.fixedDeltaTime);
        }

        float targetAngle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg + 90f;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, 0f, targetAngle), 420f * Time.fixedDeltaTime);
    }

    Vector2 ResolveEnemyAstronautMoveDirection(Vector2 currentPosition)
    {
        Vector2 toTarget = enemyTargetPosition - currentPosition;
        Vector2 desired = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector2.up;
        Vector2 playerAvoidance = Vector2.zero;

        PlayerHealth ownHealth = cachedHealth != null ? cachedHealth : GetComponent<PlayerHealth>();
        cachedHealth = ownHealth;
        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player == null || !player.gameObject.activeInHierarchy || player == ownHealth || player.IsWreck || player.IsBotControlled || player.IsNeutralRiderControlled || player.IsAstronautControlled || player.IsEvacuationAnimating)
                continue;

            Vector2 away = currentPosition - (Vector2)player.transform.position;
            float distance = away.magnitude;
            if (distance <= 0.001f || distance > EnemyAstronautAvoidPlayerRadius)
                continue;

            float strength = Mathf.Clamp01((EnemyAstronautAvoidPlayerRadius - distance) / EnemyAstronautAvoidPlayerRadius);
            playerAvoidance += away.normalized * strength;
        }

        Vector2 objectAvoidance = ResolveEnemyAstronautObjectAvoidance(currentPosition, desired);
        Vector2 result = playerAvoidance.sqrMagnitude > 0.001f
            ? (desired + playerAvoidance.normalized * EnemyAstronautAvoidPlayerWeight).normalized
            : desired;
        result = ApplyEnemyAstronautObjectAvoidance(result, desired, objectAvoidance);

        return ApplyEnemyAstronautMapEdgeSteering(currentPosition, result);
    }

    Vector2 ResolveEnemyAstronautObjectAvoidance(Vector2 currentPosition, Vector2 desired)
    {
        Vector2 avoidance = Vector2.zero;
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(currentPosition, EnemyAstronautAvoidObjectRadius, out Collider2D[] hits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger || hit.attachedRigidbody == cachedBody)
                continue;

            if (!IsEnemyAstronautAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(currentPosition);
            Vector2 toObject = closest - currentPosition;
            if (toObject.sqrMagnitude > 0.0001f && Vector2.Dot(toObject.normalized, desired) < -0.35f)
                continue;

            Vector2 away = currentPosition - closest;
            if (away.sqrMagnitude <= 0.0001f)
                away = currentPosition - (Vector2)hit.transform.position;

            if (away.sqrMagnitude <= 0.0001f)
                continue;

            float distance = Mathf.Max(0.08f, away.magnitude);
            float strength = Mathf.Clamp01((EnemyAstronautAvoidObjectRadius - distance) / EnemyAstronautAvoidObjectRadius);
            if (toObject.sqrMagnitude > 0.0001f)
                strength *= Mathf.Lerp(0.75f, 1.3f, Mathf.Clamp01(Vector2.Dot(toObject.normalized, desired)));

            avoidance += away.normalized * strength;
        }

        return avoidance;
    }

    Vector2 ApplyEnemyAstronautObjectAvoidance(Vector2 currentDirection, Vector2 desired, Vector2 objectAvoidance)
    {
        if (objectAvoidance.sqrMagnitude <= 0.001f)
            return currentDirection;

        Vector2 away = objectAvoidance.normalized;
        Vector2 result = (currentDirection.normalized + away * EnemyAstronautAvoidObjectWeight).normalized;
        if (Vector2.Dot(result, desired) >= 0.12f)
            return result;

        Vector2 tangent = new Vector2(-desired.y, desired.x);
        if (Vector2.Dot(tangent, away) < 0f)
            tangent = -tangent;

        return (desired * 0.38f + tangent.normalized * 0.62f).normalized;
    }

    static bool IsEnemyAstronautAvoidedObject(Collider2D hit)
    {
        if (hit == null)
            return false;

        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        if (hit.GetComponentInParent<DroppedCargoCrate>() != null)
            return true;

        return hit.GetComponentInParent<MovingSpaceObject>() != null;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        DeflectEnemyAstronautFromLooseObject(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        DeflectEnemyAstronautFromLooseObject(collision);
    }

    void DeflectEnemyAstronautFromLooseObject(Collision2D collision)
    {
        if (!initialized || !isEnemySurvivor || enemyEvacuationStarted || collision == null)
            return;

        if (PhotonNetwork.InRoom && photonView != null && !photonView.IsMine)
            return;

        Collider2D hit = collision.collider;
        if (!IsEnemyAstronautAvoidedObject(hit))
            return;

        if (cachedBody == null)
            cachedBody = GetComponent<Rigidbody2D>();
        if (cachedBody == null)
            return;

        Vector2 position = cachedBody.position;
        Vector2 away = position - hit.ClosestPoint(position);
        if (away.sqrMagnitude <= 0.0001f)
            away = position - (Vector2)hit.transform.position;
        if (away.sqrMagnitude <= 0.0001f)
            return;

        Vector2 toTarget = enemyTargetPosition - position;
        Vector2 desired = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : cachedBody.linearVelocity.normalized;
        if (desired.sqrMagnitude <= 0.001f)
            desired = Vector2.up;

        Vector2 awayNormal = away.normalized;
        Vector2 tangent = new Vector2(-awayNormal.y, awayNormal.x);
        if (Vector2.Dot(tangent, desired) < 0f)
            tangent = -tangent;

        cachedBody.linearVelocity = (desired * 0.32f + tangent.normalized * 0.68f).normalized * EnemyAstronautSpeed;
    }

    Vector2 ApplyEnemyAstronautMapEdgeSteering(Vector2 currentPosition, Vector2 desiredDirection)
    {
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        if (mapSize.x <= 0f || mapSize.y <= 0f)
            return desiredDirection;

        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - EnemyAstronautMapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - EnemyAstronautMapEdgeMargin);
        Vector2 predicted = currentPosition + desiredDirection.normalized * 1.5f;
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + 1f, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - 1f, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + 1f, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - 1f, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desiredDirection.normalized * 0.58f + inward.normalized * 0.42f).normalized;
    }

    void RequestEnemyAstronautEvacuation(Vector2 target)
    {
        if (enemyEvacuationStarted)
            return;

        enemyEvacuationStarted = true;
        if (photonView != null)
            photonView.RPC(nameof(BeginEnemyAstronautEvacuationRpc), RpcTarget.All, target.x, target.y);
        else
            StartEnemyAstronautEvacuation(target);
    }

    [PunRPC]
    void BeginEnemyAstronautEvacuationRpc(float targetX, float targetY)
    {
        if (!initialized)
            InitializeFromPhotonData();

        if (!isEnemySurvivor)
            return;

        StartEnemyAstronautEvacuation(new Vector2(targetX, targetY));
    }

    void StartEnemyAstronautEvacuation(Vector2 target)
    {
        if (enemyEvacuationRoutine != null)
            return;

        enemyEvacuationStarted = true;
        enemyEvacuationRoutine = StartCoroutine(EnemyAstronautEvacuationRoutine(target));
    }

    IEnumerator EnemyAstronautEvacuationRoutine(Vector2 target)
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        if (cachedBody == null)
            cachedBody = GetComponent<Rigidbody2D>();

        if (cachedBody != null)
        {
            cachedBody.linearVelocity = Vector2.zero;
            cachedBody.angularVelocity = 0f;
            cachedBody.simulated = false;
        }

        Vector3 startPosition = transform.position;
        Vector3 endPosition = new Vector3(target.x, target.y, startPosition.z);
        Vector3 startScale = transform.localScale;
        Vector3 endScale = new Vector3(
            Mathf.Sign(startScale.x) * 0.01f,
            Mathf.Sign(startScale.y) * 0.01f,
            startScale.z);
        float elapsed = 0f;
        while (elapsed < EnemyAstronautEvacuationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / EnemyAstronautEvacuationDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.Lerp(startPosition, endPosition, eased);
            transform.localScale = Vector3.Lerp(startScale, endScale, eased);
            yield return null;
        }

        transform.position = endPosition;
        transform.localScale = endScale;

        if (photonView != null && photonView.IsMine)
        {
            if (PhotonNetwork.InRoom)
                PhotonNetwork.Destroy(gameObject);
            else
                Destroy(gameObject);
        }
    }

    public static void SpawnEnemySurvivorsFrom(EnemyBot sourceBot)
    {
        if (!PhotonNetwork.IsMasterClient || sourceBot == null || !PhotonNetwork.InRoom)
            return;

        int maxCount = GetEnemySurvivorCount(sourceBot.Kind);
        if (maxCount <= 0)
            return;

        Vector2 origin = sourceBot.transform.position;
        Vector2[] targets = ResolveEnemySurvivorTargets(origin, maxCount);
        for (int i = 0; i < maxCount; i++)
        {
            if (Random.value > EnemyAstronautSpawnChancePerSlot)
                continue;

            Vector2 target = targets != null && i < targets.Length
                ? targets[i]
                : ResolveFallbackEnemyAstronautTarget(origin, i);
            Vector2 spawn = ResolveEnemySurvivorSpawnPosition(sourceBot, origin, i);
            int variantIndex = ResolveEnemyAstronautVariant(sourceBot.Kind, i);
            GameObject astronaut = PhotonNetwork.Instantiate(
                "Player",
                new Vector3(spawn.x, spawn.y, 0f),
                Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)),
                0,
                new object[] { EnemyAstronautInstantiationMarker, variantIndex, target.x, target.y, (int)sourceBot.Kind });

            if (astronaut == null)
                continue;

            AstronautSurvivor survivor = astronaut.GetComponent<AstronautSurvivor>();
            if (survivor == null)
                survivor = astronaut.AddComponent<AstronautSurvivor>();
            survivor.InitializeFromPhotonData();

            Rigidbody2D body = astronaut.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                Vector2 outward = spawn - origin;
                if (outward.sqrMagnitude <= 0.001f)
                    outward = Random.insideUnitCircle;
                if (outward.sqrMagnitude <= 0.001f)
                    outward = Vector2.up;

                body.linearVelocity = outward.normalized * Random.Range(0.45f, 0.95f);
            }
        }
    }

    public static int GetEnemySurvivorCount(EnemyBotKind kind)
    {
        switch (kind)
        {
            case EnemyBotKind.Drone:
            case EnemyBotKind.SpaceMine:
            case EnemyBotKind.SpaceManta:
            case EnemyBotKind.GravitySquid:
            case EnemyBotKind.CosmicWorm:
            case EnemyBotKind.RiftWarden:
                return 0;
            case EnemyBotKind.NeutralFighter:
            case EnemyBotKind.PirateFighter:
            case EnemyBotKind.PirateFighterElite:
            case EnemyBotKind.PirateFighterAce:
            case EnemyBotKind.HunterLance:
                return 1;
            case EnemyBotKind.Mothership:
            case EnemyBotKind.PirateBase:
                return 3;
            default:
                return 2;
        }
    }

    static int ResolveEnemyAstronautVariant(EnemyBotKind kind, int ordinal)
    {
        if (EnemyBot.IsPirateFighterKind(kind) || kind == EnemyBotKind.PirateBase || kind == EnemyBotKind.Corsair)
            return 3;

        switch (kind)
        {
            case EnemyBotKind.NeutralFighter:
                return ordinal % 2 == 0 ? 2 : 0;
            case EnemyBotKind.RescueShip:
                return 1;
            case EnemyBotKind.RadarShip:
                return ordinal % 2 == 0 ? 4 : 6;
            case EnemyBotKind.SpaceTruck:
            case EnemyBotKind.ContainerShip:
                return ordinal % 2 == 0 ? 5 : 7;
            case EnemyBotKind.Mothership:
                return ordinal == 0 ? 8 : ordinal == 1 ? 4 : 5;
            case EnemyBotKind.HunterLance:
                return 8;
            default:
                return Mathf.Abs(((int)kind * 3) + ordinal) % Mathf.Max(1, GetEnemyAstronautSpriteCount());
        }
    }

    static float ResolvePlayerAstronautSpeed()
    {
        return Mathf.Max(PlayerAstronautMinimumSpeed, PlayerAstronautReferenceMovementSpeed / PlayerAstronautSpeedDivisor);
    }

    static Vector2[] ResolveEnemySurvivorTargets(Vector2 origin, int count)
    {
        List<Vector2> destinations = CollectEnemyAstronautDestinations();
        destinations.Sort((a, b) => Vector2.Distance(origin, a).CompareTo(Vector2.Distance(origin, b)));

        Vector2[] result = new Vector2[Mathf.Max(0, count)];
        for (int i = 0; i < result.Length; i++)
        {
            Vector2 baseTarget = destinations.Count > 0
                ? destinations[i % destinations.Count]
                : ResolveFallbackEnemyAstronautTarget(origin, i);
            result[i] = baseTarget + ResolveEnemyAstronautTargetJitter(i, destinations.Count > 1);
        }

        return result;
    }

    static List<Vector2> CollectEnemyAstronautDestinations()
    {
        List<Vector2> destinations = new List<Vector2>();

        ExtractionZone[] zones = RuntimeSceneQueryCache.GetExtractionZones();
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && zones[i].gameObject.activeInHierarchy)
                AddUniqueDestination(destinations, zones[i].transform.position);
        }

        ScienceStation[] scienceStations = RuntimeSceneQueryCache.GetScienceStations();
        for (int i = 0; i < scienceStations.Length; i++)
        {
            if (scienceStations[i] != null && scienceStations[i].gameObject.activeInHierarchy)
                AddUniqueDestination(destinations, scienceStations[i].transform.position);
        }

        RepairBay[] repairBays = RuntimeSceneQueryCache.GetRepairBays();
        for (int i = 0; i < repairBays.Length; i++)
        {
            if (repairBays[i] != null && repairBays[i].gameObject.activeInHierarchy)
                AddUniqueDestination(destinations, repairBays[i].transform.position);
        }

        SpaceFactory[] factories = RuntimeSceneQueryCache.GetSpaceFactories();
        for (int i = 0; i < factories.Length; i++)
        {
            if (factories[i] != null && factories[i].gameObject.activeInHierarchy)
                AddUniqueDestination(destinations, factories[i].transform.position);
        }

        return destinations;
    }

    static void AddUniqueDestination(List<Vector2> destinations, Vector2 position)
    {
        for (int i = 0; i < destinations.Count; i++)
        {
            if (Vector2.SqrMagnitude(destinations[i] - position) <= 0.64f)
                return;
        }

        destinations.Add(position);
    }

    static Vector2 ResolveEnemyAstronautTargetJitter(int ordinal, bool keepSmall)
    {
        float radius = keepSmall ? EnemyAstronautTargetJitterRadius * 0.45f : EnemyAstronautTargetJitterRadius;
        float angle = (ordinal * 137.50776f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    static Vector2 ResolveFallbackEnemyAstronautTarget(Vector2 origin, int ordinal)
    {
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        if (mapSize.x <= 0f || mapSize.y <= 0f)
            mapSize = new Vector2(64f, 48f);

        Vector2 direction = origin.sqrMagnitude > 0.001f ? origin.normalized : Vector2.up;
        float angle = Mathf.Atan2(direction.y, direction.x) + ordinal * 2.39996f;
        direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;

        float halfX = Mathf.Max(4f, mapSize.x * 0.5f - EnemyAstronautMapEdgeMargin);
        float halfY = Mathf.Max(4f, mapSize.y * 0.5f - EnemyAstronautMapEdgeMargin);
        return new Vector2(direction.x * halfX, direction.y * halfY);
    }

    static Vector2 ResolveEnemySurvivorSpawnPosition(EnemyBot sourceBot, Vector2 origin, int ordinal)
    {
        float baseRadius = Mathf.Clamp(sourceBot != null ? sourceBot.VisualTargetSize * 0.22f : 0.6f, 0.52f, 1.75f);
        for (int i = 0; i < 14; i++)
        {
            float angle = ((ordinal * 79f) + (i * 137.50776f)) * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (baseRadius + i * 0.08f);
            Vector2 candidate = origin + offset;
            if (IsEnemyAstronautSpawnFree(candidate, sourceBot != null ? sourceBot.transform : null))
                return candidate;
        }

        return origin + Random.insideUnitCircle.normalized * baseRadius;
    }

    static bool IsEnemyAstronautSpawnFree(Vector2 candidate, Transform sourceTransform)
    {
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(candidate, EnemyAstronautSpawnClearanceRadius, out Collider2D[] hits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (sourceTransform != null && (hit.transform == sourceTransform || hit.transform.IsChildOf(sourceTransform)))
                continue;

            return false;
        }

        return true;
    }

    static bool HasEmergencySuitBeacon(object[] data)
    {
        return data != null &&
               data.Length > 1 &&
               data[1] is bool enabled &&
               enabled;
    }

    static bool HasEscapePod(object[] data)
    {
        return data != null &&
               data.Length > 2 &&
               data[2] is bool enabled &&
               enabled;
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
            result = longValue > int.MaxValue ? int.MaxValue : longValue < int.MinValue ? int.MinValue : (int)longValue;
            return true;
        }

        result = 0;
        return false;
    }

    static bool TryConvertToFloat(object value, out float result)
    {
        if (value is float floatValue)
        {
            result = floatValue;
            return true;
        }

        if (value is double doubleValue)
        {
            result = (float)doubleValue;
            return true;
        }

        if (value is int intValue)
        {
            result = intValue;
            return true;
        }

        result = 0f;
        return false;
    }

    void LateUpdate()
    {
        if (cachedRenderer != null)
            cachedRenderer.color = Color.white;
    }

    Sprite LoadAstronautSprite()
    {
        if (cachedAstronautSprite != null)
            return cachedAstronautSprite;

        cachedAstronautSprite = Resources.Load<Sprite>("kosmonauta_resource");
        if (cachedAstronautSprite != null)
            return cachedAstronautSprite;

        Sprite[] resourceSprites = Resources.LoadAll<Sprite>("kosmonauta_resource");
        if (resourceSprites != null && resourceSprites.Length > 0)
        {
            cachedAstronautSprite = resourceSprites[0];
            return cachedAstronautSprite;
        }

        Texture2D resourceTexture = Resources.Load<Texture2D>("kosmonauta_resource");
        if (resourceTexture != null)
        {
            cachedAstronautSprite = Sprite.Create(
                resourceTexture,
                new Rect(0f, 0f, resourceTexture.width, resourceTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return cachedAstronautSprite;
        }

        string filePath = System.IO.Path.Combine(Application.dataPath, "kosmonauta.png");
        if (!System.IO.File.Exists(filePath))
            return null;

        byte[] bytes = System.IO.File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(bytes, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        cachedAstronautSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        return cachedAstronautSprite;
    }

    static int GetEnemyAstronautSpriteCount()
    {
        Sprite[] sprites = LoadEnemyAstronautSprites();
        return sprites != null && sprites.Length > 0 ? sprites.Length : 9;
    }

    static Sprite LoadEnemyAstronautSprite(int variantIndex)
    {
        Sprite[] sprites = LoadEnemyAstronautSprites();
        if (sprites == null || sprites.Length == 0)
            return null;

        return sprites[Mathf.Abs(variantIndex) % sprites.Length];
    }

    static Sprite[] LoadEnemyAstronautSprites()
    {
        if (cachedEnemyAstronautSprites != null && cachedEnemyAstronautSprites.Length > 0)
            return cachedEnemyAstronautSprites;

        cachedEnemyAstronautSprites = Resources.LoadAll<Sprite>(EnemyAstronautSpriteResourcePath);
        if (cachedEnemyAstronautSprites != null && cachedEnemyAstronautSprites.Length > 0)
            return cachedEnemyAstronautSprites;

        Texture2D texture = Resources.Load<Texture2D>(EnemyAstronautSpriteResourcePath);
        if (texture == null)
            return cachedEnemyAstronautSprites;

        cachedEnemyAstronautSprites = SliceEnemyAstronautTexture(texture);
        return cachedEnemyAstronautSprites;
    }

    static Sprite[] SliceEnemyAstronautTexture(Texture2D texture)
    {
        if (texture == null)
            return System.Array.Empty<Sprite>();

        const int columns = 3;
        const int rows = 3;
        int cellWidth = texture.width / columns;
        int cellHeight = texture.height / rows;
        Sprite[] sprites = new Sprite[columns * rows];
        int index = 0;
        for (int row = rows - 1; row >= 0; row--)
        {
            for (int column = 0; column < columns; column++)
            {
                Rect rect = new Rect(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
                sprites[index] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
                sprites[index].name = "enemy_astronaut_runtime_" + index;
                index++;
            }
        }

        return sprites;
    }

    Sprite LoadEscapePodSprite()
    {
        if (cachedEscapePodSprite != null)
            return cachedEscapePodSprite;

        cachedEscapePodSprite = Resources.Load<Sprite>("Items/escape_pod");
        if (cachedEscapePodSprite != null)
            return cachedEscapePodSprite;

        Texture2D resourceTexture = Resources.Load<Texture2D>("Items/escape_pod");
        if (resourceTexture != null)
        {
            cachedEscapePodSprite = Sprite.Create(
                resourceTexture,
                new Rect(0f, 0f, resourceTexture.width, resourceTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return cachedEscapePodSprite;
        }

        string filePath = System.IO.Path.Combine(Application.dataPath, "escape_pod.png");
        if (!System.IO.File.Exists(filePath))
            return null;

        byte[] bytes = System.IO.File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(bytes, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        cachedEscapePodSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        return cachedEscapePodSprite;
    }

    void SetWorldBoxSize(BoxCollider2D collider2D, Vector2 worldSize)
    {
        Vector3 scale = collider2D.transform.lossyScale;
        float safeX = Mathf.Abs(scale.x) > 0.0001f ? Mathf.Abs(scale.x) : 1f;
        float safeY = Mathf.Abs(scale.y) > 0.0001f ? Mathf.Abs(scale.y) : 1f;
        collider2D.size = new Vector2(worldSize.x / safeX, worldSize.y / safeY);
    }

    void FitRendererToTargetSize(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds spriteBounds = renderer.sprite.bounds;
        float largestDimension = Mathf.Max(spriteBounds.size.x, spriteBounds.size.y);
        if (largestDimension <= 0.0001f)
            return;

        float scale = targetSize / largestDimension;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
