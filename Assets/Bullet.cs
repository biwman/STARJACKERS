using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

public class Bullet : MonoBehaviourPun
{
    static readonly List<Collider2D> ActiveBulletColliders = new List<Collider2D>();
    static readonly Collider2D[] AreaDamageHits = new Collider2D[96];
    static readonly ProfilerMarker RocketGuidanceMarker = new ProfilerMarker("Bullet.RocketGuidance");
    const string RocketProjectileResourcePath = "Items/rakieta";
    const string PilotBreachProjectileMarker = "sir_breach";
    const string WeaponMetadataMarker = "weapon_meta";
    const string ContainerAutoCannonEffectId = "container_auto_cannon";
    public const string SimpleBoltEffectId = "simple_bolt";
    public const string TripleBoltEffectId = "triple_bolt";
    public const string DroidBoltEffectId = "droid_bolt";
    public const string MilitaryVanTracerEffectId = "military_van_tracer";
    const float RocketProjectileBaseWorldLength = 0.58f;
    const float PulseDisruptorMinimumDamageMultiplier = 0.35f;
    const int PilotBreachTraceCount = 3;
    static Sprite cachedRocketProjectileSprite;
    static Material sharedProjectileLineMaterial;

    public int damage = 10;
    public int ownerViewID;
    public float rangeMultiplier = 15f;
    public float fallbackPlayerLength = 1f;
    public float safetyLifetime = 10f;
    public float minimumWorldRadius = 0.12f;
    public WeaponDamageType DamageType => damageType;
    public WeaponDeliveryMethod DeliveryMethod => deliveryMethod;
    public WeaponDeliveryFlags DeliveryFlags => deliveryFlags;

    Vector2 spawnPosition;
    float maxTravelDistance;
    float visualScaleMultiplier = 1f;
    Color visualColor = Color.white;
    bool destroyRequested;
    bool useDamageProfile;
    int shieldDamage;
    int hpDamage;
    bool pierces;
    float areaDamageRadius;
    string hitEffectId = string.Empty;
    WeaponDamageType damageType = WeaponDamageType.None;
    WeaponDeliveryMethod deliveryMethod = WeaponDeliveryMethod.None;
    WeaponDeliveryFlags deliveryFlags = WeaponDeliveryFlags.None;
    bool isArcProjectile;
    bool isRocketProjectile;
    bool pilotBreachProjectile;
    bool canDamageOwnerInArea;
    int homingTargetViewId;
    float homingTurnRateDegrees = 145f;
    float rocketSpeed;
    Vector2 arcStartPosition;
    Vector2 arcTargetPosition;
    float arcHeight = 1f;
    float arcStartedAt;
    float arcTravelDuration = 1f;
    bool arcImpactTriggered;
    readonly HashSet<int> damagedViewIds = new HashSet<int>();
    LineRenderer[] railSegments;
    LineRenderer[] ionBoltLines;
    LineRenderer[] pirateFighterStreakLines;
    LineRenderer[] autoTurretBoltLines;
    LineRenderer[] gatlingTracerLines;
    LineRenderer[] compactBoltLines;
    LineRenderer[] pilotBreachTraceLines;
    SpriteRenderer plasmaInnerRenderer;
    SpriteRenderer plasmaOuterRenderer;
    LineRenderer rocketTrailLine;
    SpriteRenderer rocketProjectileRenderer;
    SpriteRenderer projectileGlowRenderer;
    SpriteRenderer pilotBreachGlowRenderer;
    AudioSource rocketLoopSource;
    float rocketProjectileWorldLength = RocketProjectileBaseWorldLength;
    Rigidbody2D cachedRigidbody;
    Collider2D cachedCollider;
    PhotonView cachedOwnerView;
    PlayerShooting cachedOwnerShooting;
    EnemyBot cachedOwnerBot;
    PhotonView cachedHomingTargetView;
    PlayerHealth cachedHomingTargetHealth;
    int cachedOwnerViewId;
    int cachedHomingTargetViewId;
    bool hasResolvedOwnerShooting;
    bool hasResolvedOwnerBot;

    public static void PrewarmRoundAssets()
    {
        PrewarmSpriteTexture(LoadRocketProjectileSprite());
        GetProjectileLineMaterial();
    }

    public static object[] AppendWeaponMetadata(
        object[] data,
        WeaponDamageType configuredDamageType,
        WeaponDeliveryMethod configuredDeliveryMethod,
        WeaponDeliveryFlags configuredDeliveryFlags)
    {
        if (data == null)
            return null;

        object[] result = new object[data.Length + 4];
        data.CopyTo(result, 0);
        int start = data.Length;
        result[start] = WeaponMetadataMarker;
        result[start + 1] = (int)configuredDamageType;
        result[start + 2] = (int)configuredDeliveryMethod;
        result[start + 3] = (int)configuredDeliveryFlags;
        return result;
    }

    void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody2D>();
        cachedCollider = GetComponent<Collider2D>();
    }

    PhotonView GetCachedOwnerView()
    {
        if (ownerViewID <= 0)
            return null;

        if (cachedOwnerView == null || cachedOwnerViewId != ownerViewID)
        {
            cachedOwnerView = PhotonView.Find(ownerViewID);
            cachedOwnerViewId = ownerViewID;
            cachedOwnerShooting = null;
            cachedOwnerBot = null;
            hasResolvedOwnerShooting = false;
            hasResolvedOwnerBot = false;
        }

        return cachedOwnerView;
    }

    PlayerShooting GetCachedOwnerShooting()
    {
        PhotonView ownerView = GetCachedOwnerView();
        if (!hasResolvedOwnerShooting)
        {
            cachedOwnerShooting = ownerView != null ? ownerView.GetComponent<PlayerShooting>() : null;
            hasResolvedOwnerShooting = true;
        }

        return cachedOwnerShooting;
    }

    EnemyBot GetCachedOwnerBot()
    {
        PhotonView ownerView = GetCachedOwnerView();
        if (!hasResolvedOwnerBot)
        {
            cachedOwnerBot = ownerView != null ? ownerView.GetComponent<EnemyBot>() : null;
            hasResolvedOwnerBot = true;
        }

        return cachedOwnerBot;
    }

    PlayerHealth GetCachedHomingTargetHealth()
    {
        if (homingTargetViewId <= 0)
            return null;

        if (cachedHomingTargetView == null || cachedHomingTargetViewId != homingTargetViewId)
        {
            cachedHomingTargetView = PhotonView.Find(homingTargetViewId);
            cachedHomingTargetViewId = homingTargetViewId;
            cachedHomingTargetHealth = cachedHomingTargetView != null ? cachedHomingTargetView.GetComponent<PlayerHealth>() : null;
        }

        return cachedHomingTargetHealth;
    }

    void Start()
    {
        ApplyPhotonConfig();
        ApplyVisualConfig();

        spawnPosition = transform.position;
        arcStartPosition = spawnPosition;
        arcStartedAt = Time.time;
        maxTravelDistance = GetOwnerLength() * rangeMultiplier;

        if (GetComponent<HideInNebulaTarget>() == null)
        {
            gameObject.AddComponent<HideInNebulaTarget>();
        }

        Rigidbody2D rb = cachedRigidbody;
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.mass = 0.005f * RoomSettings.GetBulletPushMultiplier();
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            if (isArcProjectile)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.simulated = false;
            }
        }

        CircleCollider2D collider2D = GetComponent<CircleCollider2D>();
        if (collider2D != null)
        {
            if (isArcProjectile)
                collider2D.enabled = false;

            float worldRadius = GetWorldRadius(collider2D);
            if (worldRadius < minimumWorldRadius)
            {
                SetWorldRadius(collider2D, minimumWorldRadius);
            }

            for (int i = ActiveBulletColliders.Count - 1; i >= 0; i--)
            {
                Collider2D other = ActiveBulletColliders[i];
                if (other == null)
                {
                    ActiveBulletColliders.RemoveAt(i);
                    continue;
                }

                Physics2D.IgnoreCollision(collider2D, other, true);
            }

            ActiveBulletColliders.Add(collider2D);
        }

        if (maxTravelDistance <= 0f)
        {
            maxTravelDistance = fallbackPlayerLength * rangeMultiplier;
        }

        if (isRocketProjectile)
            StartRocketLoopAudio();

        StartCoroutine(DestroyAfterSafetyLifetime());
    }

    void Update()
    {
        UpdateStyledProjectileVisuals();

        if (!photonView.IsMine)
        {
            if (isArcProjectile)
                UpdateArcProjectileVisual();
            return;
        }

        if (isArcProjectile)
        {
            UpdateArcProjectileVisual();
            return;
        }

        if (isRocketProjectile)
            UpdateRocketGuidance();

        if (Vector2.Distance(spawnPosition, transform.position) >= maxTravelDistance)
        {
            if (isRocketProjectile)
                DetonateRocket(transform.position, GetTravelDirection());
            else
                DestroyBullet();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!photonView.IsMine)
            return;

        if (isArcProjectile)
            return;

        if (collision.gameObject.GetComponentInParent<Bullet>() != null)
            return;

        if (IsPlayerOnlyProjectile())
        {
            PlayerHealth playerTarget = collision.gameObject.GetComponentInParent<PlayerHealth>();
            if (!CanDamagePlayerOnlyTarget(playerTarget))
            {
                IgnoreCollision(collision);
                return;
            }

            Vector2 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            ApplyDamageToHealth(playerTarget, impactPoint, true);
            Vector2 playerOnlyVfxDirection = GetTravelDirection();
            if (!string.IsNullOrWhiteSpace(hitEffectId))
                BroadcastImpactVfx(hitEffectId, impactPoint, playerOnlyVfxDirection, ResolveImpactVfxScale());

            ApplyBulletPush(collision);
            if (!pierces)
                DestroyBullet();
            return;
        }

        PlayerDeployableBase deployable = collision.gameObject.GetComponentInParent<PlayerDeployableBase>();
        if (deployable != null && deployable.photonView != null)
        {
            Vector2 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            ApplyDamageToDeployable(deployable, impactPoint, true);
        }

        PlayerHealth hp = collision.gameObject.GetComponentInParent<PlayerHealth>();
        if (deployable == null && hp != null && hp.GetComponent<LureBeaconDecoy>() == null && hp.photonView.ViewID != ownerViewID)
        {
            if (ShouldIgnoreFriendlyEnemyTarget(hp))
            {
                IgnoreCollision(collision);
                return;
            }

            Vector2 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            ApplyDamageToHealth(hp, impactPoint, true);
        }

        LureBeaconDecoy beacon = collision.gameObject.GetComponentInParent<LureBeaconDecoy>();
        if (beacon != null && !CanDamageBeacon(beacon))
        {
            Collider2D bulletCollider = cachedCollider;
            if (bulletCollider != null && collision.collider != null)
                Physics2D.IgnoreCollision(bulletCollider, collision.collider);

            return;
        }

        if (beacon != null && beacon.photonView != null && beacon.photonView.ViewID != ownerViewID)
        {
            Vector2 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            ApplyDamageToBeacon(beacon, impactPoint, true);
        }

        ObstacleChunk obstacleChunk = collision.gameObject.GetComponentInParent<ObstacleChunk>();
        if (obstacleChunk != null && !string.IsNullOrWhiteSpace(obstacleChunk.StableId) && RoomSettings.AreObstaclesDestructible())
        {
            SpaceObjectMotionSync.RequestObstacleDamage(obstacleChunk.StableId, GetPilotObstacleDamage(damage, obstacleChunk.StableId));
        }

        Vector2 vfxPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
        Vector2 vfxDirection = GetTravelDirection();
        if (!string.IsNullOrWhiteSpace(hitEffectId))
            BroadcastImpactVfx(hitEffectId, vfxPoint, vfxDirection, ResolveImpactVfxScale());

        if (IsRocketProjectile())
            ApplyAreaDamage(vfxPoint, null);

        ApplyBulletPush(collision);

        if (!pierces)
            DestroyBullet();
    }

    void IgnoreCollision(Collision2D collision)
    {
        Collider2D bulletCollider = cachedCollider;
        if (bulletCollider != null && collision != null && collision.collider != null)
            Physics2D.IgnoreCollision(bulletCollider, collision.collider);
    }

    void UpdateArcProjectileVisual()
    {
        float duration = Mathf.Max(0.2f, arcTravelDuration);
        float elapsedTime = Time.time - arcStartedAt;
        float t = Mathf.Clamp01(elapsedTime / duration);
        Vector2 basePosition = Vector2.Lerp(arcStartPosition, arcTargetPosition, t);
        float verticalOffset = Mathf.Sin(t * Mathf.PI) * Mathf.Max(0.35f, arcHeight);
        Vector2 tangent = arcTargetPosition - arcStartPosition;
        Vector2 normal = tangent.sqrMagnitude > 0.001f ? new Vector2(-tangent.y, tangent.x).normalized : Vector2.up;
        Vector2 arcPosition = basePosition + normal * verticalOffset;
        transform.position = new Vector3(arcPosition.x, arcPosition.y, transform.position.z);

        Vector2 nextBase = Vector2.Lerp(arcStartPosition, arcTargetPosition, Mathf.Clamp01(t + 0.02f));
        float nextOffset = Mathf.Sin(Mathf.Clamp01(t + 0.02f) * Mathf.PI) * Mathf.Max(0.35f, arcHeight);
        Vector2 nextArcPosition = nextBase + normal * nextOffset;
        Vector2 direction = nextArcPosition - arcPosition;
        if (direction.sqrMagnitude > 0.0001f)
            transform.up = direction.normalized;

        if (photonView.IsMine && t >= 1f && !arcImpactTriggered)
            ApplyArcImpact();
    }

    void ApplyArcImpact()
    {
        if (arcImpactTriggered)
            return;

        arcImpactTriggered = true;
        transform.position = new Vector3(arcTargetPosition.x, arcTargetPosition.y, transform.position.z);

        int hitCount = OverlapCircleNonAlloc(arcTargetPosition, Mathf.Max(0.2f, areaDamageRadius), AreaDamageHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = AreaDamageHits[i];
            if (hit == null)
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null)
            {
                ApplyDamageToDeployable(deployable, arcTargetPosition, false);
                continue;
            }

            PlayerHealth hp = hit.GetComponentInParent<PlayerHealth>();
            if (hp != null && hp.GetComponent<LureBeaconDecoy>() == null)
            {
                ApplyDamageToHealth(hp, arcTargetPosition, false);
                continue;
            }

            LureBeaconDecoy beacon = hit.GetComponentInParent<LureBeaconDecoy>();
            if (beacon != null && CanDamageBeacon(beacon))
            {
                ApplyDamageToBeacon(beacon, arcTargetPosition, false);
                continue;
            }

            ObstacleChunk obstacleChunk = hit.GetComponentInParent<ObstacleChunk>();
            if (obstacleChunk != null && !string.IsNullOrWhiteSpace(obstacleChunk.StableId) && RoomSettings.AreObstaclesDestructible())
            {
                SpaceObjectMotionSync.RequestObstacleDamage(obstacleChunk.StableId, GetPilotObstacleDamage(damage, obstacleChunk.StableId));
            }
        }

        if (!string.IsNullOrWhiteSpace(hitEffectId))
            BroadcastImpactVfx(hitEffectId, arcTargetPosition, Vector2.up, Mathf.Max(visualScaleMultiplier, areaDamageRadius));

        DestroyBullet();
    }

    void ApplyDamageToHealth(PlayerHealth hp, Vector2 impactPoint, bool includeArea, bool allowOwnerDamage = false)
    {
        if (hp == null || hp.photonView == null)
            return;

        bool isOwner = hp.photonView.ViewID == ownerViewID;
        if (isOwner && !allowOwnerDamage)
            return;

        if (damagedViewIds.Contains(hp.photonView.ViewID))
            return;

        damagedViewIds.Add(hp.photonView.ViewID);
        float damageMultiplier = ResolveImpactDamageMultiplier() * ResolvePilotBreachTargetMultiplier(hp) * ResolveVectorWeakPointTargetMultiplier(hp);
        string damageSource = ResolveDamageSource();
        WeaponHitContext hitContext = CreateHitContext(damageSource);
        if (useDamageProfile)
        {
            hp.photonView.RPC(
                nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                RpcTarget.MasterClient,
                ScaleDamage(shieldDamage, damageMultiplier),
                ScaleDamage(hpDamage, damageMultiplier),
                ownerViewID,
                impactPoint.x,
                impactPoint.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }
        else
        {
            hp.photonView.RPC(
                nameof(PlayerHealth.TakeDamageWithContextAt),
                RpcTarget.MasterClient,
                ScaleDamage(damage, damageMultiplier),
                ownerViewID,
                impactPoint.x,
                impactPoint.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }

        if (!isOwner)
            NotifyOwnerComplexHit();
        if (includeArea)
            ApplyAreaDamage(impactPoint, hp);
    }

    void ApplyAreaDamage(Vector2 center, PlayerHealth directHit)
    {
        if (areaDamageRadius <= 0.01f)
            return;

        int hitCount = OverlapCircleNonAlloc(center, areaDamageRadius, AreaDamageHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = AreaDamageHits[i];
            if (IsPlayerOnlyProjectile())
            {
                PlayerHealth playerTarget = hit != null ? hit.GetComponentInParent<PlayerHealth>() : null;
                if (playerTarget != directHit && CanDamagePlayerOnlyTarget(playerTarget))
                    ApplyDamageToHealth(playerTarget, playerTarget.transform.position, false);

                continue;
            }

            PlayerDeployableBase deployable = hit != null ? hit.GetComponentInParent<PlayerDeployableBase>() : null;
            if (deployable != null)
            {
                ApplyDamageToDeployable(deployable, deployable.transform.position, false);

                continue;
            }

            PlayerHealth hp = hit != null ? hit.GetComponentInParent<PlayerHealth>() : null;
            if (hp == null || hp.GetComponent<LureBeaconDecoy>() != null || hp == directHit || hp.photonView == null || (hp.photonView.ViewID == ownerViewID && !canDamageOwnerInArea))
            {
                LureBeaconDecoy beacon = hit != null ? hit.GetComponentInParent<LureBeaconDecoy>() : null;
                if (beacon != null && CanDamageBeacon(beacon))
                {
                    ApplyDamageToBeacon(beacon, beacon.transform.position, false);
                    continue;
                }

                continue;
            }

            if (ShouldIgnoreFriendlyEnemyTarget(hp))
                continue;

            ApplyDamageToHealth(hp, hp.transform.position, false, canDamageOwnerInArea);
        }
    }

    void ApplyDamageToBeacon(LureBeaconDecoy beacon, Vector2 impactPoint, bool includeArea)
    {
        if (!CanDamageBeacon(beacon))
            return;

        if (damagedViewIds.Contains(beacon.photonView.ViewID))
            return;

        damagedViewIds.Add(beacon.photonView.ViewID);
        float damageMultiplier = ResolveImpactDamageMultiplier();
        if (useDamageProfile)
        {
            beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageProfileAt), RpcTarget.MasterClient, ScaleDamage(shieldDamage, damageMultiplier), ScaleDamage(hpDamage, damageMultiplier), ownerViewID, impactPoint.x, impactPoint.y);
        }
        else
        {
            beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageAt), RpcTarget.MasterClient, ScaleDamage(damage, damageMultiplier), ownerViewID, impactPoint.x, impactPoint.y);
        }

        if (includeArea)
            ApplyAreaDamage(impactPoint, null);
    }

    void ApplyDamageToDeployable(PlayerDeployableBase deployable, Vector2 impactPoint, bool includeArea)
    {
        if (deployable == null || deployable.photonView == null)
            return;

        if (damagedViewIds.Contains(deployable.photonView.ViewID))
            return;

        damagedViewIds.Add(deployable.photonView.ViewID);
        float damageMultiplier = ResolveImpactDamageMultiplier();
        WeaponHitContext hitContext = CreateHitContext(ResolveDamageSource());
        if (useDamageProfile)
        {
            deployable.photonView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                ScaleDamage(shieldDamage, damageMultiplier),
                ScaleDamage(hpDamage, damageMultiplier),
                ownerViewID,
                impactPoint.x,
                impactPoint.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }
        else
        {
            int scaledDamage = ScaleDamage(damage, damageMultiplier);
            deployable.photonView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                scaledDamage,
                scaledDamage,
                ownerViewID,
                impactPoint.x,
                impactPoint.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        }

        NotifyOwnerComplexHit();
        if (includeArea)
            ApplyAreaDamage(impactPoint, null);
    }

    float ResolveImpactDamageMultiplier()
    {
        if (!IsPulseDisruptorProjectile())
            return 1f;

        float armDistance = ResolveProjectileOwnLength();
        if (armDistance <= 0.001f)
            return 1f;

        float travelled = Vector2.Distance(spawnPosition, transform.position);
        float armed = Mathf.Clamp01(travelled / armDistance);
        return Mathf.Lerp(PulseDisruptorMinimumDamageMultiplier, 1f, armed);
    }

    float ResolveVectorWeakPointTargetMultiplier(PlayerHealth hp)
    {
        if (hp == null || hp.photonView == null || hp.GetComponent<EnemyBot>() == null)
            return 1f;

        return VectorWeakPointMemory.RegisterHitAndGetMultiplier(ownerViewID, "bot:" + hp.photonView.ViewID);
    }

    string ResolveDamageSource()
    {
        if (IsRocketProjectile() || string.Equals(hitEffectId, "rocket", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(hitEffectId, "artillery", System.StringComparison.OrdinalIgnoreCase))
        {
            return PlayerHealth.DamageSourceExplosive;
        }

        if (IsPirateFighterProjectile() || IsAutoTurretProjectile())
            return PlayerHealth.DamageSourceLaser;

        return string.Empty;
    }

    WeaponHitContext CreateHitContext(string damageSource)
    {
        return new WeaponHitContext(damageType, deliveryMethod, deliveryFlags, damageSource);
    }

    int ScaleDamage(int baseDamage, float multiplier)
    {
        if (baseDamage <= 0)
            return 0;

        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * Mathf.Max(0f, multiplier)));
    }

    float ResolvePilotBreachTargetMultiplier(PlayerHealth hp)
    {
        if (!pilotBreachProjectile || hp == null)
            return 1f;

        EnemyBot bot = hp.GetComponent<EnemyBot>();
        return bot != null && (bot.Kind == EnemyBotKind.Mothership || bot.Kind == EnemyBotKind.PirateBase)
            ? 2f
            : 1f;
    }

    float ResolveProjectileOwnLength()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        float bestLength = 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null || !renderer.enabled)
                continue;

            bestLength = Mathf.Max(bestLength, Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y));
        }

        if (bestLength > 0.001f)
            return bestLength;

        Collider2D collider2D = GetComponentInChildren<Collider2D>();
        if (collider2D != null)
            return Mathf.Max(collider2D.bounds.size.x, collider2D.bounds.size.y);

        return Mathf.Max(0.5f, visualScaleMultiplier);
    }

    bool CanDamageBeacon(LureBeaconDecoy beacon)
    {
        if (beacon == null || beacon.photonView == null || beacon.photonView.ViewID == ownerViewID)
            return false;

        return GetCachedOwnerBot() != null;
    }

    bool ShouldIgnoreFriendlyEnemyTarget(PlayerHealth hp)
    {
        if (hp == null)
            return false;

        EnemyBot targetBot = hp.GetComponent<EnemyBot>();
        if (targetBot == null)
            return false;

        return EnemyFriendlyFirePolicy.ShouldIgnoreProjectileHit(GetCachedOwnerBot(), targetBot, hitEffectId);
    }

    int GetPilotObstacleDamage(int baseDamage, string obstacleStableId)
    {
        PhotonView ownerView = GetCachedOwnerView();
        float multiplier = 1f;
        if (ownerView != null && PilotCatalog.IsSelectedPilot(ownerView.Owner, PilotCatalog.SirNowitzkyId))
            multiplier *= 1.5f;

        if (ownerView != null && !string.IsNullOrWhiteSpace(obstacleStableId) && PilotCatalog.IsSelectedPilot(ownerView.Owner, PilotCatalog.VectorId))
            multiplier *= VectorWeakPointMemory.RegisterHitAndGetMultiplier(ownerViewID, "obstacle:" + obstacleStableId);

        if (pilotBreachProjectile)
            multiplier *= 2f;

        return Mathf.CeilToInt(baseDamage * multiplier);
    }

    void NotifyOwnerComplexHit()
    {
        PhotonView ownerView = GetCachedOwnerView();
        PlayerShooting shooting = GetCachedOwnerShooting();
        if (shooting != null && ownerView != null && ownerView.IsMine)
            shooting.AddSuperChargeForDamage();
    }

    void ApplyBulletPush(Collision2D collision)
    {
        if (collision == null)
            return;

        Rigidbody2D rb = cachedRigidbody;
        Vector2 bulletVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
        if (bulletVelocity.sqrMagnitude < 0.0001f)
            return;

        float pushMultiplier = RoomSettings.GetBulletPushMultiplier();
        Vector2 impulse = bulletVelocity * (0.22f * pushMultiplier);

        MovingSpaceObject movingObject = collision.collider != null
            ? collision.collider.GetComponentInParent<MovingSpaceObject>()
            : null;
        if (movingObject != null && !string.IsNullOrWhiteSpace(movingObject.StableId))
        {
            SpaceObjectMotionSync.RequestImpulse(movingObject.StableId, impulse);
            return;
        }

        PlayerRepairDocking repairDocking = collision.collider != null
            ? collision.collider.GetComponentInParent<PlayerRepairDocking>()
            : null;
        if (repairDocking != null && repairDocking.IsDamageImmune)
            return;

        PlayerHealth hitHealth = collision.collider != null
            ? collision.collider.GetComponentInParent<PlayerHealth>()
            : null;
        if (hitHealth != null && hitHealth.IsEnemyAstronautControlled)
            return;

        Rigidbody2D hitBody = collision.rigidbody;
        if (hitBody == null || hitBody.bodyType != RigidbodyType2D.Dynamic)
            return;

        hitBody.AddForce(impulse, ForceMode2D.Impulse);
    }

    IEnumerator DestroyAfterSafetyLifetime()
    {
        yield return new WaitForSeconds(safetyLifetime);

        if (this == null || gameObject == null)
            yield break;

        if (isRocketProjectile && photonView != null && photonView.IsMine)
        {
            DetonateRocket(transform.position, GetTravelDirection());
            yield break;
        }

        if (PhotonNetwork.IsConnected && photonView != null)
        {
            if (photonView.IsMine)
            {
                PhotonNetwork.Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void UpdateRocketGuidance()
    {
        Rigidbody2D rb = cachedRigidbody;
        if (rb == null)
            return;

        Vector2 currentDirection = rb.linearVelocity.sqrMagnitude > 0.001f
            ? rb.linearVelocity.normalized
            : (Vector2)transform.up;
        float speed = rocketSpeed > 0.01f ? rocketSpeed : Mathf.Max(0.5f, rb.linearVelocity.magnitude);

        using (RocketGuidanceMarker.Auto())
        {
            PlayerHealth targetHealth = GetCachedHomingTargetHealth();
            if (targetHealth != null && !targetHealth.IsWreck)
            {
                Vector2 toTarget = (Vector2)targetHealth.transform.position - (Vector2)transform.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    float maxRadians = Mathf.Max(0f, homingTurnRateDegrees) * Mathf.Deg2Rad * Time.deltaTime;
                    Vector3 guided = Vector3.RotateTowards(currentDirection, toTarget.normalized, maxRadians, 0f);
                    currentDirection = new Vector2(guided.x, guided.y).normalized;
                }
            }
        }

        rb.linearVelocity = currentDirection * speed;
        if (currentDirection.sqrMagnitude > 0.0001f)
            transform.up = currentDirection;
    }

    void DetonateRocket(Vector2 impactPoint, Vector2 direction)
    {
        if (destroyRequested)
            return;

        if (!string.IsNullOrWhiteSpace(hitEffectId))
            BroadcastImpactVfx(hitEffectId, impactPoint, direction, ResolveImpactVfxScale());

        ApplyAreaDamage(impactPoint, null);
        DestroyBullet();
    }

    void DestroyBullet()
    {
        if (destroyRequested)
            return;

        destroyRequested = true;

        if (PhotonNetwork.IsConnected && photonView != null)
        {
            if (photonView.IsMine)
            {
                PhotonNetwork.Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    float ResolveImpactVfxScale()
    {
        return IsRocketProjectile()
            ? Mathf.Max(visualScaleMultiplier, areaDamageRadius)
            : visualScaleMultiplier;
    }

    void StartRocketLoopAudio()
    {
        AudioClip clip = AudioManager.Instance.RocketFlyLoopClip;
        if (clip == null || rocketLoopSource != null)
            return;

        rocketLoopSource = gameObject.AddComponent<AudioSource>();
        AudioManager.Instance.ConfigureSpatialSource(rocketLoopSource, 0.34f);
        rocketLoopSource.clip = clip;
        rocketLoopSource.loop = true;
        rocketLoopSource.Play();
    }

    void StopRocketLoopAudio()
    {
        if (rocketLoopSource != null)
            rocketLoopSource.Stop();
    }

    void OnDestroy()
    {
        StopRocketLoopAudio();

        Collider2D collider2D = cachedCollider;
        if (collider2D != null)
        {
            ActiveBulletColliders.Remove(collider2D);
        }
    }

    void ApplyPhotonConfig()
    {
        object[] data = photonView != null ? photonView.InstantiationData : null;
        if (data == null || data.Length < 7)
            return;

        ownerViewID = data[0] is int ownerId ? ownerId : ownerViewID;
        damage = data[1] is int configuredDamage ? configuredDamage : damage;
        visualScaleMultiplier = data[2] is float scale ? scale : visualScaleMultiplier;

        float r = data[3] is float colorR ? colorR : visualColor.r;
        float g = data[4] is float colorG ? colorG : visualColor.g;
        float b = data[5] is float colorB ? colorB : visualColor.b;
        float a = data[6] is float colorA ? colorA : visualColor.a;
        visualColor = new Color(r, g, b, a);

        if (data.Length >= 8 && data[7] is float configuredRangeMultiplier)
            rangeMultiplier = Mathf.Max(0.1f, configuredRangeMultiplier);

        if (data.Length >= 13 &&
            data[8] is int &&
            data[9] is int &&
            data[10] is bool &&
            data[11] is float &&
            data[12] is string)
        {
            shieldDamage = data[8] is int configuredShieldDamage ? Mathf.Max(0, configuredShieldDamage) : damage;
            hpDamage = data[9] is int configuredHpDamage ? Mathf.Max(0, configuredHpDamage) : damage;
            pierces = data[10] is bool configuredPierces && configuredPierces;
            areaDamageRadius = data[11] is float configuredAreaRadius ? Mathf.Max(0f, configuredAreaRadius) : 0f;
            hitEffectId = data[12] as string ?? string.Empty;
            useDamageProfile = true;
        }

        if (data.Length >= 14 && data[13] is float configuredFlightTime)
            safetyLifetime = Mathf.Clamp(configuredFlightTime, 0.2f, 30f);

        if (data.Length >= 18 && data[14] is bool)
        {
            isArcProjectile = data[14] is bool configuredArcProjectile && configuredArcProjectile;
            arcTargetPosition = new Vector2(
                data[15] is float configuredTargetX ? configuredTargetX : transform.position.x,
                data[16] is float configuredTargetY ? configuredTargetY : transform.position.y);
            arcHeight = data[17] is float configuredArcHeight ? Mathf.Max(0.35f, configuredArcHeight) : 1f;
            arcTravelDuration = Mathf.Clamp(safetyLifetime, 0.2f, 30f);
        }

        if (data.Length >= 23 && data[18] is string)
        {
            string projectileMode = data[18] as string ?? string.Empty;
            isRocketProjectile = string.Equals(projectileMode, "rocket", System.StringComparison.OrdinalIgnoreCase);
            homingTargetViewId = data[19] is int targetViewId ? targetViewId : 0;
            homingTurnRateDegrees = data[20] is float turnRate ? Mathf.Max(0f, turnRate) : homingTurnRateDegrees;
            rocketSpeed = data[21] is float configuredRocketSpeed ? Mathf.Max(0.5f, configuredRocketSpeed) : rocketSpeed;
            canDamageOwnerInArea = data[22] is bool configuredOwnerDamage && configuredOwnerDamage;
        }

        ApplyWeaponMetadata(data);
        pilotBreachProjectile = ContainsPayloadMarker(data, PilotBreachProjectileMarker);
    }

    void ApplyWeaponMetadata(object[] data)
    {
        if (data == null || data.Length < 4)
            return;

        for (int i = 0; i <= data.Length - 4; i++)
        {
            if (!string.Equals(data[i] as string, WeaponMetadataMarker, System.StringComparison.Ordinal))
                continue;

            damageType = ParseDamageType(data[i + 1], damageType);
            deliveryMethod = ParseDeliveryMethod(data[i + 2], deliveryMethod);
            deliveryFlags = ParseDeliveryFlags(data[i + 3], deliveryFlags);
            return;
        }
    }

    static bool ContainsPayloadMarker(object[] data, string marker)
    {
        if (data == null || string.IsNullOrWhiteSpace(marker))
            return false;

        for (int i = 0; i < data.Length; i++)
        {
            if (string.Equals(data[i] as string, marker, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static WeaponDamageType ParseDamageType(object value, WeaponDamageType fallback)
    {
        if (value is WeaponDamageType typedValue)
            return typedValue;

        if (value is int intValue && System.Enum.IsDefined(typeof(WeaponDamageType), intValue))
            return (WeaponDamageType)intValue;

        if (value is string raw && System.Enum.TryParse(raw, true, out WeaponDamageType parsedValue))
            return parsedValue;

        return fallback;
    }

    static WeaponDeliveryMethod ParseDeliveryMethod(object value, WeaponDeliveryMethod fallback)
    {
        if (value is WeaponDeliveryMethod typedValue)
            return typedValue;

        if (value is int intValue && System.Enum.IsDefined(typeof(WeaponDeliveryMethod), intValue))
            return (WeaponDeliveryMethod)intValue;

        if (value is string raw && System.Enum.TryParse(raw, true, out WeaponDeliveryMethod parsedValue))
            return parsedValue;

        return fallback;
    }

    static WeaponDeliveryFlags ParseDeliveryFlags(object value, WeaponDeliveryFlags fallback)
    {
        if (value is WeaponDeliveryFlags typedValue)
            return typedValue;

        if (value is int intValue && intValue >= 0)
            return (WeaponDeliveryFlags)intValue;

        if (value is string raw && System.Enum.TryParse(raw, true, out WeaponDeliveryFlags parsedValue))
            return parsedValue;

        return fallback;
    }

    void ApplyVisualConfig()
    {
        transform.localScale *= Mathf.Max(0.2f, visualScaleMultiplier);

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = visualColor;
            if (IsArtilleryProjectile())
            {
                EnsureArtilleryProjectileVisual(spriteRenderer);
            }
            else if (IsRocketProjectile())
            {
                EnsureRocketProjectileVisual(spriteRenderer);
            }
            else if (IsRailProjectile())
            {
                EnsureRailProjectileVisual(spriteRenderer);
            }
            else if (IsIonProjectile())
            {
                EnsureIonProjectileVisual(spriteRenderer);
            }
            else if (IsPirateFighterProjectile())
            {
                EnsurePirateFighterProjectileVisual(spriteRenderer);
            }
            else if (IsAutoTurretProjectile())
            {
                EnsureAutoTurretProjectileVisual(spriteRenderer);
            }
            else if (IsGatlingProjectile())
            {
                EnsureGatlingProjectileVisual(spriteRenderer);
            }
            else if (IsCompactBoltProjectile())
            {
                EnsureCompactBoltProjectileVisual(spriteRenderer);
            }
            else if (IsPlasmaProjectile())
            {
                EnsurePlasmaProjectileVisual(spriteRenderer);
            }
            else if (IsSmallRedPlasma())
            {
                EnsurePlasmaGlow(spriteRenderer);
            }

            if (pilotBreachProjectile)
                EnsurePilotBreachProjectileVisual(spriteRenderer);
        }
    }

    bool IsRailProjectile()
    {
        return string.Equals(hitEffectId, "rail", System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsIonProjectile()
    {
        return string.Equals(hitEffectId, "ion", System.StringComparison.OrdinalIgnoreCase) ||
               IsPulseDisruptorProjectile();
    }

    bool IsPulseDisruptorProjectile()
    {
        return string.Equals(hitEffectId, "pulse_disruptor", System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsPirateFighterProjectile()
    {
        return string.Equals(hitEffectId, "pirate_fighter", System.StringComparison.OrdinalIgnoreCase) ||
               IsPirateFighterRedProjectile();
    }

    bool IsPirateFighterRedProjectile()
    {
        return string.Equals(hitEffectId, "pirate_fighter_red", System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsAutoTurretProjectile()
    {
        return string.Equals(hitEffectId, "auto_turret", System.StringComparison.OrdinalIgnoreCase) ||
               IsContainerAutoCannonProjectile();
    }

    bool IsContainerAutoCannonProjectile()
    {
        return string.Equals(hitEffectId, ContainerAutoCannonEffectId, System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsPlayerOnlyProjectile()
    {
        return IsContainerAutoCannonProjectile();
    }

    bool CanDamagePlayerOnlyTarget(PlayerHealth hp)
    {
        return hp != null &&
               hp.photonView != null &&
               hp.photonView.ViewID != ownerViewID &&
               !hp.IsWreck &&
               !hp.IsEvacuationAnimating &&
               !hp.IsBotControlled &&
               !hp.IsNeutralRiderControlled &&
               !hp.IsAstronautControlled &&
               hp.GetComponent<PlayerDeployableBase>() == null &&
               hp.GetComponent<LureBeaconDecoy>() == null;
    }

    bool IsGatlingProjectile()
    {
        return string.Equals(hitEffectId, "gatling", System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsSimpleBoltProjectile()
    {
        return string.Equals(hitEffectId, SimpleBoltEffectId, System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsTripleBoltProjectile()
    {
        return string.Equals(hitEffectId, TripleBoltEffectId, System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsDroidBoltProjectile()
    {
        return string.Equals(hitEffectId, DroidBoltEffectId, System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsMilitaryVanTracerProjectile()
    {
        return string.Equals(hitEffectId, MilitaryVanTracerEffectId, System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsCompactBoltProjectile()
    {
        return IsSimpleBoltProjectile() ||
               IsTripleBoltProjectile() ||
               IsDroidBoltProjectile() ||
               IsMilitaryVanTracerProjectile();
    }

    bool IsPlasmaProjectile()
    {
        return string.Equals(hitEffectId, "plasma", System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsSmallRedPlasma()
    {
        return visualColor.r > 0.85f &&
               visualColor.g < 0.22f &&
               visualColor.b < 0.18f &&
               visualScaleMultiplier <= 0.75f;
    }

    bool IsArtilleryProjectile()
    {
        return string.Equals(hitEffectId, "artillery", System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsRocketProjectile()
    {
        return isRocketProjectile || string.Equals(hitEffectId, "rocket", System.StringComparison.OrdinalIgnoreCase);
    }

    void EnsurePlasmaGlow(SpriteRenderer coreRenderer)
    {
        EnsureProjectileGlow(coreRenderer, "RedPlasmaGlow", new Color(1f, 0.1f, 0.02f, 0.34f), 1.9f);
    }

    void EnsurePlasmaProjectileVisual(SpriteRenderer coreRenderer)
    {
        if (coreRenderer == null || coreRenderer.sprite == null)
            return;

        coreRenderer.color = new Color(0.72f, 1f, 0.48f, 0.95f);
        EnsureProjectileGlow(coreRenderer, "PlasmaAuraGlow", new Color(0.08f, 1f, 0.26f, 0.3f), 2.2f);

        plasmaInnerRenderer = EnsurePlasmaLayer(coreRenderer, "PlasmaHotCore", new Color(0.9f, 1f, 0.62f, 0.86f), 0.48f, coreRenderer.sortingOrder + 2);
        plasmaOuterRenderer = EnsurePlasmaLayer(coreRenderer, "PlasmaEmeraldShell", new Color(0.02f, 0.72f, 0.2f, 0.45f), 1.34f, coreRenderer.sortingOrder + 1);
        UpdatePlasmaProjectileVisual();
    }

    SpriteRenderer EnsurePlasmaLayer(SpriteRenderer coreRenderer, string objectName, Color color, float scale, int sortingOrder)
    {
        Transform layerTransform = transform.Find(objectName);
        SpriteRenderer layerRenderer = layerTransform != null
            ? layerTransform.GetComponent<SpriteRenderer>()
            : null;

        if (layerRenderer == null)
        {
            GameObject layerObject = new GameObject(objectName);
            layerObject.transform.SetParent(transform, false);
            layerTransform = layerObject.transform;
            layerRenderer = layerObject.AddComponent<SpriteRenderer>();
        }

        layerTransform.localPosition = Vector3.zero;
        layerTransform.localRotation = Quaternion.identity;
        layerTransform.localScale = Vector3.one * scale;

        layerRenderer.sprite = coreRenderer.sprite;
        layerRenderer.color = color;
        layerRenderer.sortingLayerID = coreRenderer.sortingLayerID;
        layerRenderer.sortingOrder = sortingOrder;
        layerRenderer.enabled = true;
        return layerRenderer;
    }

    void EnsureArtilleryProjectileVisual(SpriteRenderer coreRenderer)
    {
        if (coreRenderer == null)
            return;

        coreRenderer.color = new Color(1f, 0.72f, 0.2f, 0.96f);
        EnsureProjectileGlow(coreRenderer, "ArtilleryGlow", new Color(1f, 0.3f, 0.04f, 0.42f), 2.6f);
    }

    void EnsureRocketProjectileVisual(SpriteRenderer coreRenderer)
    {
        if (coreRenderer == null)
            return;

        Sprite rocketSprite = LoadRocketProjectileSprite();
        if (rocketSprite != null)
        {
            rocketProjectileRenderer = EnsureRocketSpriteRenderer(coreRenderer, rocketSprite);
            EnsureRocketSpriteGlow(rocketProjectileRenderer);
            coreRenderer.enabled = false;
        }
        else
        {
            coreRenderer.color = new Color(1f, 0.88f, 0.58f, 0.98f);
            EnsureProjectileGlow(coreRenderer, "RocketGlow", new Color(1f, 0.35f, 0.06f, 0.38f), 1.7f);
        }

        if (rocketTrailLine != null)
            return;

        SpriteRenderer sortingRenderer = rocketProjectileRenderer != null ? rocketProjectileRenderer : coreRenderer;
        GameObject trailObject = new GameObject("RocketEngineTrail");
        trailObject.transform.SetParent(transform, false);
        rocketTrailLine = trailObject.AddComponent<LineRenderer>();
        EngineTrailVisualUtility.ConfigureLineBase(rocketTrailLine);
        rocketTrailLine.useWorldSpace = true;
        rocketTrailLine.positionCount = 2;
        rocketTrailLine.textureMode = LineTextureMode.Stretch;
        rocketTrailLine.alignment = LineAlignment.View;
        rocketTrailLine.numCapVertices = 3;
        rocketTrailLine.numCornerVertices = 2;
        rocketTrailLine.sharedMaterial = GetProjectileLineMaterial();
        rocketTrailLine.startColor = new Color(1f, 0.9f, 0.42f, 0.95f);
        rocketTrailLine.endColor = new Color(1f, 0.18f, 0.02f, 0f);
        rocketTrailLine.widthMultiplier = 0.14f * Mathf.Max(0.7f, visualScaleMultiplier);
        rocketTrailLine.sortingLayerID = sortingRenderer.sortingLayerID;
        rocketTrailLine.sortingOrder = sortingRenderer.sortingOrder - 1;
    }

    static Sprite LoadRocketProjectileSprite()
    {
        if (cachedRocketProjectileSprite != null)
            return cachedRocketProjectileSprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(RocketProjectileResourcePath);
        if (sprites != null && sprites.Length > 0)
            cachedRocketProjectileSprite = sprites[0];

        if (cachedRocketProjectileSprite == null)
            cachedRocketProjectileSprite = Resources.Load<Sprite>(RocketProjectileResourcePath);

        return cachedRocketProjectileSprite;
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }

    SpriteRenderer EnsureRocketSpriteRenderer(SpriteRenderer coreRenderer, Sprite rocketSprite)
    {
        Transform spriteTransform = transform.Find("RocketProjectileSprite");
        SpriteRenderer spriteRenderer = spriteTransform != null
            ? spriteTransform.GetComponent<SpriteRenderer>()
            : null;

        if (spriteRenderer == null)
        {
            GameObject spriteObject = new GameObject("RocketProjectileSprite");
            spriteObject.transform.SetParent(transform, false);
            spriteTransform = spriteObject.transform;
            spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = rocketSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingLayerID = coreRenderer.sortingLayerID;
        spriteRenderer.sortingOrder = coreRenderer.sortingOrder + 1;
        spriteRenderer.enabled = true;

        spriteTransform.localPosition = Vector3.zero;
        spriteTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);

        float parentScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        parentScale = Mathf.Max(0.0001f, parentScale);
        float spriteLength = Mathf.Max(0.0001f, Mathf.Max(rocketSprite.bounds.size.x, rocketSprite.bounds.size.y));
        rocketProjectileWorldLength = RocketProjectileBaseWorldLength * Mathf.Clamp(visualScaleMultiplier, 0.7f, 1.15f);
        float localScale = rocketProjectileWorldLength / (spriteLength * parentScale);
        spriteTransform.localScale = Vector3.one * localScale;

        return spriteRenderer;
    }

    void EnsureRocketSpriteGlow(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null || spriteRenderer.transform.Find("RocketSpriteGlow") != null)
            return;

        GameObject glowObject = new GameObject("RocketSpriteGlow");
        glowObject.transform.SetParent(spriteRenderer.transform, false);
        glowObject.transform.localPosition = Vector3.zero;
        glowObject.transform.localRotation = Quaternion.identity;
        glowObject.transform.localScale = Vector3.one * 1.16f;

        SpriteRenderer glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = spriteRenderer.sprite;
        glowRenderer.color = new Color(1f, 0.46f, 0.08f, 0.2f);
        glowRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        glowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
    }

    void EnsureProjectileGlow(SpriteRenderer coreRenderer, string objectName, Color glowColor, float scale)
    {
        if (coreRenderer == null || coreRenderer.sprite == null || transform.Find(objectName) != null)
            return;

        GameObject glowObject = new GameObject(objectName);
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = Vector3.zero;
        glowObject.transform.localRotation = Quaternion.identity;
        glowObject.transform.localScale = Vector3.one * scale;

        SpriteRenderer glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = coreRenderer.sprite;
        glowRenderer.color = glowColor;
        glowRenderer.sortingLayerID = coreRenderer.sortingLayerID;
        glowRenderer.sortingOrder = coreRenderer.sortingOrder - 1;
        projectileGlowRenderer = glowRenderer;
    }

    void EnsurePilotBreachProjectileVisual(SpriteRenderer coreRenderer)
    {
        if (!pilotBreachProjectile || pilotBreachTraceLines != null)
            return;

        SpriteRenderer referenceRenderer = rocketProjectileRenderer != null ? rocketProjectileRenderer : coreRenderer;
        EnsurePilotBreachGlow(referenceRenderer);

        pilotBreachTraceLines = new LineRenderer[PilotBreachTraceCount];
        for (int i = 0; i < pilotBreachTraceLines.Length; i++)
        {
            GameObject lineObject = new GameObject("PilotBreachTrace_" + i);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.sharedMaterial = GetProjectileLineMaterial();
            line.startColor = new Color(1f, 0.98f, 0.72f, i == 1 ? 0.98f : 0.62f);
            line.endColor = new Color(1f, 0.44f, 0.08f, i == 1 ? 0.18f : 0.04f);
            line.widthMultiplier = 0.06f * Mathf.Max(0.8f, visualScaleMultiplier);

            if (referenceRenderer != null)
            {
                line.sortingLayerID = referenceRenderer.sortingLayerID;
                line.sortingOrder = referenceRenderer.sortingOrder + 6;
            }
            else
            {
                line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
                line.sortingOrder = GameVisualTheme.PlayerSortingOrder + 6;
            }

            pilotBreachTraceLines[i] = line;
        }

        UpdatePilotBreachProjectileVisual();
    }

    void EnsurePilotBreachGlow(SpriteRenderer referenceRenderer)
    {
        if (referenceRenderer == null || referenceRenderer.sprite == null)
            return;

        Transform existing = referenceRenderer.transform.Find("PilotBreachGlow");
        if (existing != null)
        {
            pilotBreachGlowRenderer = existing.GetComponent<SpriteRenderer>();
            return;
        }

        GameObject glowObject = new GameObject("PilotBreachGlow");
        glowObject.transform.SetParent(referenceRenderer.transform, false);
        glowObject.transform.localPosition = Vector3.zero;
        glowObject.transform.localRotation = Quaternion.identity;
        glowObject.transform.localScale = Vector3.one * 1.34f;

        SpriteRenderer glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = referenceRenderer.sprite;
        glowRenderer.color = new Color(1f, 0.92f, 0.36f, 0.36f);
        glowRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
        glowRenderer.sortingOrder = referenceRenderer.sortingOrder + 5;
        pilotBreachGlowRenderer = glowRenderer;
    }

    void EnsureRailProjectileVisual(SpriteRenderer coreRenderer)
    {
        if (railSegments != null && railSegments.Length > 0)
            return;

        railSegments = new LineRenderer[3];
        for (int i = 0; i < railSegments.Length; i++)
        {
            GameObject segmentObject = new GameObject("RailProjectileSegment_" + i);
            segmentObject.transform.SetParent(transform, false);

            LineRenderer line = segmentObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sharedMaterial = GetProjectileLineMaterial();
            line.startColor = i == 1 ? new Color(1f, 0.88f, 0.46f, 0.98f) : new Color(1f, 0.24f, 0.04f, 0.72f);
            line.endColor = i == 1 ? new Color(1f, 0.36f, 0.04f, 0.92f) : new Color(1f, 0.08f, 0.02f, 0.28f);
            line.widthMultiplier = (i == 1 ? 0.055f : 0.035f) * Mathf.Max(0.75f, visualScaleMultiplier);
            if (coreRenderer != null)
            {
                line.sortingLayerID = coreRenderer.sortingLayerID;
                line.sortingOrder = coreRenderer.sortingOrder + 2;
                coreRenderer.color = new Color(coreRenderer.color.r, coreRenderer.color.g, coreRenderer.color.b, 0.08f);
            }

            railSegments[i] = line;
        }

        UpdateStyledProjectileVisuals();
    }

    void UpdateStyledProjectileVisuals()
    {
        if (IsRailProjectile())
            UpdateRailProjectileVisual();

        if (IsIonProjectile())
            UpdateIonProjectileVisual();

        if (IsPirateFighterProjectile())
            UpdatePirateFighterProjectileVisual();

        if (IsAutoTurretProjectile())
            UpdateAutoTurretProjectileVisual();

        if (IsGatlingProjectile())
            UpdateGatlingProjectileVisual();

        if (IsCompactBoltProjectile())
            UpdateCompactBoltProjectileVisual();

        if (IsPlasmaProjectile())
            UpdatePlasmaProjectileVisual();

        if (IsRocketProjectile())
            UpdateRocketProjectileVisual();

        if (pilotBreachProjectile)
            UpdatePilotBreachProjectileVisual();
    }

    void UpdateRocketProjectileVisual()
    {
        AlignRocketVisualWithTravelDirection();

        if (rocketTrailLine == null)
            return;

        Vector2 direction = GetTravelDirection();
        float visualLength = Mathf.Max(0.18f, rocketProjectileWorldLength);
        Vector3 tail = transform.position - (Vector3)(direction * visualLength * 0.52f);
        Vector3 flameEnd = tail - (Vector3)(direction * 0.46f * Mathf.Clamp(visualScaleMultiplier, 0.7f, 1.15f));
        float flicker = 0.85f + Mathf.Sin(Time.time * 42f + (photonView != null ? photonView.ViewID * 0.19f : 0f)) * 0.15f;
        rocketTrailLine.widthMultiplier = 0.1f * Mathf.Clamp(visualScaleMultiplier, 0.7f, 1.15f) * flicker;
        rocketTrailLine.SetPosition(0, tail);
        rocketTrailLine.SetPosition(1, flameEnd);
    }

    void UpdatePlasmaProjectileVisual()
    {
        float seed = photonView != null ? photonView.ViewID * 0.13f : 0f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f + seed);

        if (projectileGlowRenderer != null)
        {
            projectileGlowRenderer.color = Color.Lerp(
                new Color(0.02f, 0.95f, 0.22f, 0.22f),
                new Color(0.42f, 1f, 0.12f, 0.42f),
                pulse);
            projectileGlowRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1.9f, 2.35f, pulse);
        }

        if (plasmaInnerRenderer != null)
        {
            plasmaInnerRenderer.color = Color.Lerp(
                new Color(0.82f, 1f, 0.48f, 0.78f),
                new Color(0.98f, 1f, 0.74f, 0.96f),
                pulse);
            plasmaInnerRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.38f, 0.56f, pulse);
        }

        if (plasmaOuterRenderer != null)
        {
            plasmaOuterRenderer.color = Color.Lerp(
                new Color(0.03f, 0.58f, 0.12f, 0.32f),
                new Color(0.12f, 1f, 0.34f, 0.5f),
                1f - pulse);
            plasmaOuterRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1.18f, 1.48f, 1f - pulse);
        }
    }

    void AlignRocketVisualWithTravelDirection()
    {
        if (isArcProjectile)
            return;

        Vector2 direction = GetTravelDirection();
        if (direction.sqrMagnitude > 0.001f)
            transform.up = direction.normalized;
    }

    void UpdateRailProjectileVisual()
    {
        if (railSegments == null || railSegments.Length == 0)
            return;

        Vector2 direction = GetTravelDirection();
        Vector3 center = transform.position;
        float length = 0.78f * Mathf.Max(0.75f, visualScaleMultiplier);
        float[] starts = { -0.52f, -0.12f, 0.25f };
        float[] ends = { -0.25f, 0.18f, 0.52f };
        for (int i = 0; i < railSegments.Length; i++)
        {
            LineRenderer line = railSegments[i];
            if (line == null)
                continue;

            Vector3 start = center + (Vector3)(direction * (starts[i] * length));
            Vector3 end = center + (Vector3)(direction * (ends[i] * length));
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
    }

    void EnsureIonProjectileVisual(SpriteRenderer coreRenderer)
    {
        if (ionBoltLines != null && ionBoltLines.Length > 0)
            return;

        ionBoltLines = new LineRenderer[3];
        for (int i = 0; i < ionBoltLines.Length; i++)
        {
            GameObject lineObject = new GameObject("IonBoltLine_" + i);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.sharedMaterial = GetProjectileLineMaterial();
            line.startColor = i == 0 ? new Color(0.84f, 0.98f, 1f, 0.96f) : new Color(0.18f, 0.72f, 1f, 0.56f);
            line.endColor = i == 0 ? new Color(0.16f, 0.74f, 1f, 0.92f) : new Color(0.08f, 0.25f, 1f, 0.18f);
            line.widthMultiplier = (i == 0 ? 0.075f : i == 1 ? 0.145f : 0.04f) * Mathf.Max(0.8f, visualScaleMultiplier);
            if (coreRenderer != null)
            {
                line.sortingLayerID = coreRenderer.sortingLayerID;
                line.sortingOrder = coreRenderer.sortingOrder + (i == 0 ? 3 : 2);
            }

            ionBoltLines[i] = line;
        }

        if (coreRenderer != null)
            coreRenderer.color = new Color(coreRenderer.color.r, coreRenderer.color.g, coreRenderer.color.b, 0.04f);

        UpdateIonProjectileVisual();
    }

    void UpdateIonProjectileVisual()
    {
        if (ionBoltLines == null || ionBoltLines.Length == 0)
            return;

        Vector2 direction = GetTravelDirection();
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        Vector3 center = transform.position;
        float length = 0.58f * Mathf.Max(0.85f, visualScaleMultiplier);

        for (int i = 0; i < ionBoltLines.Length; i++)
        {
            LineRenderer line = ionBoltLines[i];
            if (line == null)
                continue;

            float sideOffset = i == 2 ? Mathf.Sin(Time.time * 34f + photonView.ViewID * 0.17f) * 0.028f : 0f;
            Vector3 start = center - (Vector3)(direction * (length * 0.52f)) + (Vector3)(tangent * sideOffset);
            Vector3 end = center + (Vector3)(direction * (length * 0.52f)) - (Vector3)(tangent * sideOffset);
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
    }

    void EnsurePirateFighterProjectileVisual(SpriteRenderer coreRenderer)
    {
        if (pirateFighterStreakLines != null && pirateFighterStreakLines.Length > 0)
            return;

        bool redProjectile = IsPirateFighterRedProjectile();
        pirateFighterStreakLines = new LineRenderer[2];
        for (int i = 0; i < pirateFighterStreakLines.Length; i++)
        {
            GameObject lineObject = new GameObject("PirateFighterBolt_" + i);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
            line.numCornerVertices = 1;
            line.sharedMaterial = GetProjectileLineMaterial();
            line.startColor = ResolvePirateFighterBoltStartColor(i, redProjectile);
            line.endColor = ResolvePirateFighterBoltEndColor(i, redProjectile);
            line.widthMultiplier = (i == 0 ? 0.052f : 0.12f) * Mathf.Max(0.8f, visualScaleMultiplier);
            if (coreRenderer != null)
            {
                line.sortingLayerID = coreRenderer.sortingLayerID;
                line.sortingOrder = coreRenderer.sortingOrder + (i == 0 ? 3 : 2);
            }

            pirateFighterStreakLines[i] = line;
        }

        if (coreRenderer != null)
            coreRenderer.color = redProjectile
                ? new Color(1f, 0.08f, 0.02f, 0.06f)
                : new Color(0.08f, 0.58f, 1f, 0.05f);

        UpdatePirateFighterProjectileVisual();
    }

    Color ResolvePirateFighterBoltStartColor(int index, bool redProjectile)
    {
        if (redProjectile)
            return index == 0 ? new Color(1f, 0.86f, 0.62f, 0.98f) : new Color(1f, 0.18f, 0.08f, 0.45f);

        return index == 0 ? new Color(0.78f, 0.96f, 1f, 0.98f) : new Color(0.1f, 0.54f, 1f, 0.45f);
    }

    Color ResolvePirateFighterBoltEndColor(int index, bool redProjectile)
    {
        if (redProjectile)
            return index == 0 ? new Color(1f, 0.18f, 0.04f, 0.82f) : new Color(0.72f, 0.03f, 0.01f, 0.08f);

        return index == 0 ? new Color(0.12f, 0.72f, 1f, 0.8f) : new Color(0.03f, 0.18f, 1f, 0.06f);
    }

    void UpdatePirateFighterProjectileVisual()
    {
        if (pirateFighterStreakLines == null || pirateFighterStreakLines.Length == 0)
            return;

        Vector2 direction = GetTravelDirection();
        Vector3 center = transform.position;
        float length = 0.34f * Mathf.Max(0.75f, visualScaleMultiplier);
        for (int i = 0; i < pirateFighterStreakLines.Length; i++)
        {
            LineRenderer line = pirateFighterStreakLines[i];
            if (line == null)
                continue;

            float lead = i == 0 ? 0.2f : 0.12f;
            float tail = i == 0 ? 0.42f : 0.62f;
            Vector3 start = center + (Vector3)(direction * (lead * length));
            Vector3 end = center - (Vector3)(direction * (tail * length));
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
    }

    void EnsureAutoTurretProjectileVisual(SpriteRenderer coreRenderer)
    {
        if (autoTurretBoltLines != null && autoTurretBoltLines.Length > 0)
            return;

        if (coreRenderer != null)
        {
            coreRenderer.color = new Color(1f, 0.92f, 0.42f, 0.98f);
            EnsureProjectileGlow(coreRenderer, "AutoTurretBoltGlow", new Color(1f, 0.34f, 0.04f, 0.48f), 2.25f);
        }

        autoTurretBoltLines = new LineRenderer[3];
        for (int i = 0; i < autoTurretBoltLines.Length; i++)
        {
            GameObject lineObject = new GameObject("AutoTurretBolt_" + i);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.sharedMaterial = GetProjectileLineMaterial();
            line.startColor = i == 0
                ? new Color(1f, 0.98f, 0.62f, 0.95f)
                : new Color(1f, 0.32f, 0.04f, 0.42f);
            line.endColor = i == 0
                ? new Color(1f, 0.42f, 0.06f, 0.78f)
                : new Color(0.72f, 0.05f, 0.01f, 0.03f);
            line.widthMultiplier = (i == 0 ? 0.064f : 0.035f) * Mathf.Max(0.85f, visualScaleMultiplier);
            if (coreRenderer != null)
            {
                line.sortingLayerID = coreRenderer.sortingLayerID;
                line.sortingOrder = coreRenderer.sortingOrder + (i == 0 ? 3 : 2);
            }

            autoTurretBoltLines[i] = line;
        }

        UpdateAutoTurretProjectileVisual();
    }

    void UpdateAutoTurretProjectileVisual()
    {
        if (autoTurretBoltLines == null || autoTurretBoltLines.Length == 0)
            return;

        Vector2 direction = GetTravelDirection();
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        Vector3 center = transform.position;
        float length = 0.48f * Mathf.Max(0.85f, visualScaleMultiplier);
        for (int i = 0; i < autoTurretBoltLines.Length; i++)
        {
            LineRenderer line = autoTurretBoltLines[i];
            if (line == null)
                continue;

            float side = i == 1 ? 1f : i == 2 ? -1f : 0f;
            float sideOffset = side * 0.035f * Mathf.Max(0.8f, visualScaleMultiplier);
            Vector3 start = center + (Vector3)(direction * (length * 0.24f)) + (Vector3)(tangent * sideOffset);
            Vector3 end = center - (Vector3)(direction * (length * (i == 0 ? 0.62f : 0.45f))) - (Vector3)(tangent * sideOffset * 0.35f);
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
    }

    void EnsureGatlingProjectileVisual(SpriteRenderer coreRenderer)
    {
        if (gatlingTracerLines != null && gatlingTracerLines.Length > 0)
            return;

        if (coreRenderer != null)
        {
            coreRenderer.color = new Color(1f, 0.86f, 0.42f, 0.78f);
            EnsureProjectileGlow(coreRenderer, "GatlingRoundGlow", new Color(1f, 0.5f, 0.08f, 0.24f), 1.75f);
        }

        gatlingTracerLines = new LineRenderer[2];
        for (int i = 0; i < gatlingTracerLines.Length; i++)
        {
            GameObject lineObject = new GameObject("GatlingTracer_" + i);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
            line.numCornerVertices = 1;
            line.sharedMaterial = GetProjectileLineMaterial();
            line.startColor = i == 0
                ? new Color(1f, 0.98f, 0.68f, 0.94f)
                : new Color(1f, 0.46f, 0.08f, 0.34f);
            line.endColor = i == 0
                ? new Color(1f, 0.48f, 0.08f, 0.42f)
                : new Color(0.78f, 0.08f, 0.02f, 0f);
            line.widthMultiplier = (i == 0 ? 0.032f : 0.072f) * Mathf.Max(0.7f, visualScaleMultiplier);
            if (coreRenderer != null)
            {
                line.sortingLayerID = coreRenderer.sortingLayerID;
                line.sortingOrder = coreRenderer.sortingOrder + (i == 0 ? 3 : 2);
            }

            gatlingTracerLines[i] = line;
        }

        UpdateGatlingProjectileVisual();
    }

    void EnsureCompactBoltProjectileVisual(SpriteRenderer coreRenderer)
    {
        if (compactBoltLines != null && compactBoltLines.Length > 0)
            return;

        if (coreRenderer != null)
        {
            coreRenderer.color = ResolveCompactBoltCoreColor();
            EnsureProjectileGlow(coreRenderer, ResolveCompactBoltGlowName(), ResolveCompactBoltGlowColor(), ResolveCompactBoltGlowScale());
        }

        int lineCount = ResolveCompactBoltLineCount();
        compactBoltLines = new LineRenderer[lineCount];
        for (int i = 0; i < compactBoltLines.Length; i++)
        {
            GameObject lineObject = new GameObject(ResolveCompactBoltObjectPrefix() + "_" + i);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = IsMilitaryVanTracerProjectile() ? 3 : 2;
            line.numCornerVertices = 1;
            line.sharedMaterial = GetProjectileLineMaterial();
            line.startColor = ResolveCompactBoltStartColor(i);
            line.endColor = ResolveCompactBoltEndColor(i);
            line.widthMultiplier = ResolveCompactBoltLineWidth(i);
            if (coreRenderer != null)
            {
                line.sortingLayerID = coreRenderer.sortingLayerID;
                line.sortingOrder = coreRenderer.sortingOrder + (i == 0 ? 4 : 3);
            }

            compactBoltLines[i] = line;
        }

        UpdateCompactBoltProjectileVisual();
    }

    string ResolveCompactBoltGlowName()
    {
        if (IsTripleBoltProjectile())
            return "TripleBoltGlow";

        if (IsDroidBoltProjectile())
            return "DroidBoltGlow";

        if (IsMilitaryVanTracerProjectile())
            return "MilitaryVanTracerGlow";

        return "SimpleBoltGlow";
    }

    string ResolveCompactBoltObjectPrefix()
    {
        if (IsTripleBoltProjectile())
            return "TripleBoltTracer";

        if (IsDroidBoltProjectile())
            return "DroidSparkTracer";

        if (IsMilitaryVanTracerProjectile())
            return "MilitaryVanTracer";

        return "SimpleBoltTracer";
    }

    int ResolveCompactBoltLineCount()
    {
        return IsMilitaryVanTracerProjectile() ? 3 : 2;
    }

    Color ResolveCompactBoltCoreColor()
    {
        if (IsTripleBoltProjectile())
            return new Color(1f, 0.94f, 0.66f, 0.96f);

        if (IsDroidBoltProjectile())
            return new Color(1f, 0.26f, 0.08f, 0.96f);

        if (IsMilitaryVanTracerProjectile())
            return new Color(1f, 0.18f, 0.06f, 0.94f);

        return new Color(0.78f, 0.96f, 1f, 0.98f);
    }

    Color ResolveCompactBoltGlowColor()
    {
        if (IsTripleBoltProjectile())
            return new Color(1f, 0.68f, 0.16f, 0.26f);

        if (IsDroidBoltProjectile())
            return new Color(1f, 0.08f, 0.02f, 0.34f);

        if (IsMilitaryVanTracerProjectile())
            return new Color(1f, 0.08f, 0.02f, 0.3f);

        return new Color(0.18f, 0.78f, 1f, 0.3f);
    }

    float ResolveCompactBoltGlowScale()
    {
        if (IsMilitaryVanTracerProjectile())
            return 1.85f;

        if (IsDroidBoltProjectile())
            return 1.72f;

        return 1.65f;
    }

    Color ResolveCompactBoltStartColor(int index)
    {
        if (IsTripleBoltProjectile())
            return index == 0 ? new Color(1f, 0.98f, 0.72f, 0.96f) : new Color(1f, 0.58f, 0.12f, 0.34f);

        if (IsDroidBoltProjectile())
            return index == 0 ? new Color(1f, 0.84f, 0.55f, 0.95f) : new Color(1f, 0.14f, 0.02f, 0.44f);

        if (IsMilitaryVanTracerProjectile())
        {
            if (index == 0)
                return new Color(1f, 0.9f, 0.68f, 0.98f);

            return index == 1 ? new Color(1f, 0.16f, 0.04f, 0.52f) : new Color(1f, 0.55f, 0.12f, 0.32f);
        }

        return index == 0 ? new Color(0.88f, 0.98f, 1f, 0.98f) : new Color(0.12f, 0.68f, 1f, 0.38f);
    }

    Color ResolveCompactBoltEndColor(int index)
    {
        if (IsTripleBoltProjectile())
            return index == 0 ? new Color(1f, 0.54f, 0.1f, 0.58f) : new Color(0.85f, 0.18f, 0.02f, 0f);

        if (IsDroidBoltProjectile())
            return index == 0 ? new Color(1f, 0.18f, 0.04f, 0.62f) : new Color(0.75f, 0.02f, 0.01f, 0f);

        if (IsMilitaryVanTracerProjectile())
        {
            if (index == 0)
                return new Color(1f, 0.24f, 0.06f, 0.72f);

            return index == 1 ? new Color(0.8f, 0.02f, 0.01f, 0.04f) : new Color(0.9f, 0.18f, 0.02f, 0f);
        }

        return index == 0 ? new Color(0.16f, 0.74f, 1f, 0.66f) : new Color(0.04f, 0.2f, 1f, 0f);
    }

    float ResolveCompactBoltLineWidth(int index)
    {
        float scale = Mathf.Max(0.7f, visualScaleMultiplier);
        if (IsMilitaryVanTracerProjectile())
        {
            if (index == 0)
                return 0.045f * scale;

            return (index == 1 ? 0.096f : 0.024f) * scale;
        }

        if (IsDroidBoltProjectile())
            return (index == 0 ? 0.036f : 0.082f) * scale;

        if (IsTripleBoltProjectile())
            return (index == 0 ? 0.03f : 0.068f) * scale;

        return (index == 0 ? 0.034f : 0.078f) * scale;
    }

    float ResolveCompactBoltLength()
    {
        float scale = Mathf.Clamp(visualScaleMultiplier, 0.75f, 1.18f);
        if (IsMilitaryVanTracerProjectile())
            return 0.5f * scale;

        if (IsTripleBoltProjectile())
            return 0.36f * scale;

        if (IsDroidBoltProjectile())
            return 0.34f * scale;

        return 0.42f * scale;
    }

    void UpdateCompactBoltProjectileVisual()
    {
        if (compactBoltLines == null || compactBoltLines.Length == 0)
            return;

        Vector2 direction = GetTravelDirection();
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        Vector3 center = transform.position;
        float length = ResolveCompactBoltLength();
        float seed = photonView != null ? photonView.ViewID * 0.17f : 0f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (IsMilitaryVanTracerProjectile() ? 30f : 24f) + seed);

        for (int i = 0; i < compactBoltLines.Length; i++)
        {
            LineRenderer line = compactBoltLines[i];
            if (line == null)
                continue;

            float lead = i == 0 ? 0.22f : IsMilitaryVanTracerProjectile() ? 0.08f : 0.12f;
            float tail = i == 0 ? 0.62f : IsMilitaryVanTracerProjectile() ? 0.86f : 0.74f;
            float sideOffset = 0f;
            if (IsDroidBoltProjectile() && i == 1)
                sideOffset = Mathf.Sin(Time.time * 38f + seed) * 0.028f;
            else if (IsMilitaryVanTracerProjectile() && i == 2)
                sideOffset = Mathf.Sin(Time.time * 32f + seed) * 0.018f;

            Vector3 start = center + (Vector3)(direction * (length * lead)) + (Vector3)(tangent * sideOffset);
            Vector3 end = center - (Vector3)(direction * (length * tail)) - (Vector3)(tangent * sideOffset * 0.35f);
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.widthMultiplier = ResolveCompactBoltLineWidth(i) * Mathf.Lerp(0.9f, 1.12f, i == 0 ? pulse : 1f - pulse);
        }

        if (projectileGlowRenderer != null)
        {
            Color glow = ResolveCompactBoltGlowColor();
            glow.a *= Mathf.Lerp(0.72f, 1.18f, pulse);
            projectileGlowRenderer.color = glow;
            projectileGlowRenderer.transform.localScale = Vector3.one * ResolveCompactBoltGlowScale() * Mathf.Lerp(0.92f, 1.08f, pulse);
        }
    }

    void UpdateGatlingProjectileVisual()
    {
        if (gatlingTracerLines == null || gatlingTracerLines.Length == 0)
            return;

        Vector2 direction = GetTravelDirection();
        Vector3 center = transform.position;
        float length = 0.32f * Mathf.Max(0.7f, visualScaleMultiplier);
        for (int i = 0; i < gatlingTracerLines.Length; i++)
        {
            LineRenderer line = gatlingTracerLines[i];
            if (line == null)
                continue;

            float head = i == 0 ? 0.16f : 0.05f;
            float tail = i == 0 ? 0.58f : 0.78f;
            Vector3 start = center + (Vector3)(direction * (length * head));
            Vector3 end = center - (Vector3)(direction * (length * tail));
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
    }

    void UpdatePilotBreachProjectileVisual()
    {
        if (pilotBreachTraceLines == null || pilotBreachTraceLines.Length == 0)
            return;

        Vector2 direction = GetTravelDirection();
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        Vector3 center = transform.position;
        float length = ResolvePilotBreachVisualLength();
        float scale = Mathf.Max(0.8f, visualScaleMultiplier);
        float pulse = Mathf.Sin(Time.time * 24f + (photonView != null ? photonView.ViewID * 0.11f : 0f)) * 0.5f + 0.5f;

        for (int i = 0; i < pilotBreachTraceLines.Length; i++)
        {
            LineRenderer line = pilotBreachTraceLines[i];
            if (line == null)
                continue;

            float lane = i - 1;
            float lanePulse = Mathf.Sin(Time.time * 18f + i * 1.7f) * 0.5f + 0.5f;
            float sideOffset = lane * Mathf.Lerp(0.055f, 0.105f, lanePulse) * scale;
            float head = i == 1 ? 0.48f : 0.34f;
            float tail = i == 1 ? 0.72f : 0.58f;
            Vector3 start = center + (Vector3)(direction * length * head) + (Vector3)(tangent * sideOffset);
            Vector3 end = center - (Vector3)(direction * length * tail) + (Vector3)(tangent * sideOffset * 0.35f);
            line.SetPosition(0, start);
            line.SetPosition(1, end);

            float alpha = Mathf.Lerp(i == 1 ? 0.72f : 0.42f, i == 1 ? 1f : 0.72f, pulse);
            line.startColor = new Color(1f, 0.98f, 0.72f, alpha);
            line.endColor = new Color(1f, 0.42f, 0.04f, alpha * 0.16f);
            line.widthMultiplier = (i == 1 ? 0.105f : 0.055f) * scale * Mathf.Lerp(0.84f, 1.2f, pulse);
        }

        if (pilotBreachGlowRenderer != null)
        {
            pilotBreachGlowRenderer.color = new Color(1f, 0.94f, 0.42f, Mathf.Lerp(0.28f, 0.48f, pulse));
            pilotBreachGlowRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1.22f, 1.46f, pulse);
        }
    }

    float ResolvePilotBreachVisualLength()
    {
        if (IsRocketProjectile())
            return Mathf.Max(0.72f, rocketProjectileWorldLength * 1.25f);

        if (IsRailProjectile())
            return 0.95f * Mathf.Max(0.85f, visualScaleMultiplier);

        if (IsIonProjectile())
            return 0.76f * Mathf.Max(0.85f, visualScaleMultiplier);

        return 0.58f * Mathf.Max(0.9f, visualScaleMultiplier);
    }

    Vector2 GetTravelDirection()
    {
        Rigidbody2D rb = cachedRigidbody;
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.001f)
            return rb.linearVelocity.normalized;

        Vector2 up = transform.up;
        return up.sqrMagnitude > 0.001f ? up.normalized : Vector2.up;
    }

    static int OverlapCircleNonAlloc(Vector2 center, float radius, Collider2D[] results)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };
        return Physics2D.OverlapCircle(center, radius, filter, results);
    }

    static Material GetProjectileLineMaterial()
    {
        if (sharedProjectileLineMaterial != null)
            return sharedProjectileLineMaterial;

        sharedProjectileLineMaterial = EngineTrailVisualUtility.GetSpritesMaterial();
        return sharedProjectileLineMaterial;
    }

    void BroadcastImpactVfx(string effectId, Vector2 position, Vector2 direction, float scale)
    {
        BulletImpactVfxEventRouter.Broadcast(
            effectId,
            new Vector3(position.x, position.y, transform.position.z),
            direction,
            scale);
    }

    [PunRPC]
    void PlayImpactVfx(string effectId, float x, float y, float directionX, float directionY, float scale)
    {
        BulletImpactVfxEventRouter.SpawnLocal(
            effectId,
            new Vector3(x, y, transform.position.z),
            new Vector2(directionX, directionY),
            scale);
    }

    float GetOwnerLength()
    {
        PhotonView ownerView = GetCachedOwnerView();
        if (ownerView == null)
            return fallbackPlayerLength;

        SpriteRenderer spriteRenderer = ownerView.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            return Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y);
        }

        Collider2D collider2D = ownerView.GetComponentInChildren<Collider2D>();
        if (collider2D != null)
        {
            return Mathf.Max(collider2D.bounds.size.x, collider2D.bounds.size.y);
        }

        return fallbackPlayerLength;
    }

    float GetWorldRadius(CircleCollider2D collider2D)
    {
        Vector3 scale = collider2D.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        return collider2D.radius * maxScale;
    }

    void SetWorldRadius(CircleCollider2D collider2D, float worldRadius)
    {
        Vector3 scale = collider2D.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        if (maxScale <= 0.0001f)
            maxScale = 1f;

        collider2D.radius = worldRadius / maxScale;
    }
}

public sealed class BulletImpactVfxEventRouter : MonoBehaviour, IOnEventCallback
{
    const byte ImpactVfxEventCode = 85;

    static BulletImpactVfxEventRouter instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject existing = GameObject.Find("BulletImpactVfxEventRouter");
        if (existing != null && existing.TryGetComponent(out BulletImpactVfxEventRouter router))
        {
            instance = router;
            return;
        }

        GameObject root = new GameObject("BulletImpactVfxEventRouter");
        instance = root.AddComponent<BulletImpactVfxEventRouter>();
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

    void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent == null || photonEvent.Code != ImpactVfxEventCode)
            return;

        object[] payload = photonEvent.CustomData as object[];
        if (payload == null || payload.Length < 7)
            return;

        string effectId = payload[0] as string;
        Vector3 position = new Vector3(
            ConvertToFloat(payload[1]),
            ConvertToFloat(payload[2]),
            ConvertToFloat(payload[3]));
        Vector2 direction = new Vector2(ConvertToFloat(payload[4]), ConvertToFloat(payload[5]));
        float scale = ConvertToFloat(payload[6]);
        SpawnLocal(effectId, position, direction, scale);
    }

    public static void Broadcast(string effectId, Vector3 position, Vector2 direction, float scale)
    {
        if (string.IsNullOrWhiteSpace(effectId))
            return;

        EnsureExists();
        SpawnLocal(effectId, position, direction, scale);

        if (!CanRaiseRoomEvent())
            return;

        object[] payload = { effectId, position.x, position.y, position.z, direction.x, direction.y, scale };
        PhotonNetwork.RaiseEvent(
            ImpactVfxEventCode,
            payload,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendUnreliable);
    }

    public static void SpawnLocal(string effectId, Vector3 position, Vector2 direction, float scale)
    {
        if (string.IsNullOrWhiteSpace(effectId))
            return;

        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.up;

        if (string.Equals(effectId, "rocket", System.StringComparison.OrdinalIgnoreCase) && AudioManager.Instance != null)
            AudioManager.Instance.PlayRocketExplosionAt(position);

        BulletImpactVfx.Spawn(effectId, new Vector3(position.x, position.y, position.z - 0.04f), direction.normalized, scale);
    }

    static bool CanRaiseRoomEvent()
    {
        return PhotonNetwork.IsConnected && PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null;
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
