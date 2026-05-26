using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviourPun
{
    static readonly List<Collider2D> ActiveBulletColliders = new List<Collider2D>();
    const string RocketProjectileResourcePath = "Items/rakieta";
    const float RocketProjectileBaseWorldLength = 0.58f;
    const float PulseDisruptorMinimumDamageMultiplier = 0.35f;
    static Sprite cachedRocketProjectileSprite;

    public int damage = 10;
    public int ownerViewID;
    public float rangeMultiplier = 15f;
    public float fallbackPlayerLength = 1f;
    public float safetyLifetime = 10f;
    public float minimumWorldRadius = 0.12f;

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
    bool isArcProjectile;
    bool isRocketProjectile;
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
    LineRenderer rocketTrailLine;
    SpriteRenderer rocketProjectileRenderer;
    SpriteRenderer projectileGlowRenderer;
    AudioSource rocketLoopSource;
    float rocketProjectileWorldLength = RocketProjectileBaseWorldLength;

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

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
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

        PlayerDeployableBase deployable = collision.gameObject.GetComponentInParent<PlayerDeployableBase>();
        if (deployable != null && deployable.photonView != null)
        {
            Vector2 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            ApplyDamageToDeployable(deployable, impactPoint, true);
        }

        PlayerHealth hp = collision.gameObject.GetComponentInParent<PlayerHealth>();
        if (deployable == null && hp != null && hp.GetComponent<LureBeaconDecoy>() == null && hp.photonView.ViewID != ownerViewID)
        {
            Vector2 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            ApplyDamageToHealth(hp, impactPoint, true);
        }

        LureBeaconDecoy beacon = collision.gameObject.GetComponentInParent<LureBeaconDecoy>();
        if (beacon != null && !CanDamageBeacon(beacon))
        {
            Collider2D bulletCollider = GetComponent<Collider2D>();
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
            SpaceObjectMotionSync.RequestObstacleDamage(obstacleChunk.StableId, GetPilotObstacleDamage(damage));
        }

        Vector2 vfxPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
        Vector2 vfxDirection = GetTravelDirection();
        if (!string.IsNullOrWhiteSpace(hitEffectId))
            photonView.RPC(nameof(PlayImpactVfx), RpcTarget.All, hitEffectId, vfxPoint.x, vfxPoint.y, vfxDirection.x, vfxDirection.y, ResolveImpactVfxScale());

        if (IsRocketProjectile())
            ApplyAreaDamage(vfxPoint, null);

        ApplyBulletPush(collision);

        if (!pierces)
            DestroyBullet();
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

        Collider2D[] hits = Physics2D.OverlapCircleAll(arcTargetPosition, Mathf.Max(0.2f, areaDamageRadius));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
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
                SpaceObjectMotionSync.RequestObstacleDamage(obstacleChunk.StableId, GetPilotObstacleDamage(damage));
            }
        }

        if (!string.IsNullOrWhiteSpace(hitEffectId))
            photonView.RPC(nameof(PlayImpactVfx), RpcTarget.All, hitEffectId, arcTargetPosition.x, arcTargetPosition.y, 0f, 1f, Mathf.Max(visualScaleMultiplier, areaDamageRadius));

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
        float damageMultiplier = ResolveImpactDamageMultiplier();
        if (useDamageProfile)
        {
            hp.photonView.RPC("TakeDamageProfileAt", RpcTarget.MasterClient, ScaleDamage(shieldDamage, damageMultiplier), ScaleDamage(hpDamage, damageMultiplier), ownerViewID, impactPoint.x, impactPoint.y);
        }
        else
        {
            hp.photonView.RPC("TakeDamageAt", RpcTarget.MasterClient, ScaleDamage(damage, damageMultiplier), ownerViewID, impactPoint.x, impactPoint.y);
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

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, areaDamageRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            PlayerDeployableBase deployable = hits[i] != null ? hits[i].GetComponentInParent<PlayerDeployableBase>() : null;
            if (deployable != null)
            {
                ApplyDamageToDeployable(deployable, deployable.transform.position, false);

                continue;
            }

            PlayerHealth hp = hits[i] != null ? hits[i].GetComponentInParent<PlayerHealth>() : null;
            if (hp == null || hp.GetComponent<LureBeaconDecoy>() != null || hp == directHit || hp.photonView == null || (hp.photonView.ViewID == ownerViewID && !canDamageOwnerInArea))
            {
                LureBeaconDecoy beacon = hits[i] != null ? hits[i].GetComponentInParent<LureBeaconDecoy>() : null;
                if (beacon != null && CanDamageBeacon(beacon))
                {
                    ApplyDamageToBeacon(beacon, beacon.transform.position, false);
                    continue;
                }

                continue;
            }

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
        if (useDamageProfile)
        {
            deployable.photonView.RPC(nameof(PlayerDeployableBase.TakeDeployableDamageAt), RpcTarget.MasterClient, ScaleDamage(shieldDamage, damageMultiplier), ScaleDamage(hpDamage, damageMultiplier), ownerViewID, impactPoint.x, impactPoint.y);
        }
        else
        {
            int scaledDamage = ScaleDamage(damage, damageMultiplier);
            deployable.photonView.RPC(nameof(PlayerDeployableBase.TakeDeployableDamageAt), RpcTarget.MasterClient, scaledDamage, scaledDamage, ownerViewID, impactPoint.x, impactPoint.y);
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

    int ScaleDamage(int baseDamage, float multiplier)
    {
        if (baseDamage <= 0)
            return 0;

        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * Mathf.Clamp01(multiplier)));
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

        PhotonView attackerView = ownerViewID > 0 ? PhotonView.Find(ownerViewID) : null;
        if (attackerView == null)
            return false;

        return attackerView.GetComponent<EnemyBot>() != null;
    }

    int GetPilotObstacleDamage(int baseDamage)
    {
        PhotonView ownerView = ownerViewID > 0 ? PhotonView.Find(ownerViewID) : null;
        if (ownerView != null && PilotCatalog.IsSelectedPilot(ownerView.Owner, PilotCatalog.SirNowitzkyId))
            return Mathf.CeilToInt(baseDamage * 1.5f);

        return baseDamage;
    }

    void NotifyOwnerComplexHit()
    {
        PhotonView ownerView = PhotonView.Find(ownerViewID);
        PlayerShooting shooting = ownerView != null ? ownerView.GetComponent<PlayerShooting>() : null;
        if (shooting != null && ownerView.IsMine)
            shooting.AddSuperChargeForDamage();
    }

    void ApplyBulletPush(Collision2D collision)
    {
        if (collision == null)
            return;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
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
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            return;

        Vector2 currentDirection = rb.linearVelocity.sqrMagnitude > 0.001f
            ? rb.linearVelocity.normalized
            : (Vector2)transform.up;
        float speed = rocketSpeed > 0.01f ? rocketSpeed : Mathf.Max(0.5f, rb.linearVelocity.magnitude);

        PhotonView targetView = homingTargetViewId > 0 ? PhotonView.Find(homingTargetViewId) : null;
        PlayerHealth targetHealth = targetView != null ? targetView.GetComponent<PlayerHealth>() : null;
        if (targetHealth != null && !targetHealth.IsWreck)
        {
            Vector2 toTarget = (Vector2)targetView.transform.position - (Vector2)transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                float maxRadians = Mathf.Max(0f, homingTurnRateDegrees) * Mathf.Deg2Rad * Time.deltaTime;
                Vector3 guided = Vector3.RotateTowards(currentDirection, toTarget.normalized, maxRadians, 0f);
                currentDirection = new Vector2(guided.x, guided.y).normalized;
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

        if (!string.IsNullOrWhiteSpace(hitEffectId) && photonView != null)
            photonView.RPC(nameof(PlayImpactVfx), RpcTarget.All, hitEffectId, impactPoint.x, impactPoint.y, direction.x, direction.y, ResolveImpactVfxScale());

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

        Collider2D collider2D = GetComponent<Collider2D>();
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

        if (data.Length >= 13)
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

        if (data.Length >= 18)
        {
            isArcProjectile = data[14] is bool configuredArcProjectile && configuredArcProjectile;
            arcTargetPosition = new Vector2(
                data[15] is float configuredTargetX ? configuredTargetX : transform.position.x,
                data[16] is float configuredTargetY ? configuredTargetY : transform.position.y);
            arcHeight = data[17] is float configuredArcHeight ? Mathf.Max(0.35f, configuredArcHeight) : 1f;
            arcTravelDuration = Mathf.Clamp(safetyLifetime, 0.2f, 30f);
        }

        if (data.Length >= 23)
        {
            string projectileMode = data[18] as string ?? string.Empty;
            isRocketProjectile = string.Equals(projectileMode, "rocket", System.StringComparison.OrdinalIgnoreCase);
            homingTargetViewId = data[19] is int targetViewId ? targetViewId : 0;
            homingTurnRateDegrees = data[20] is float turnRate ? Mathf.Max(0f, turnRate) : homingTurnRateDegrees;
            rocketSpeed = data[21] is float configuredRocketSpeed ? Mathf.Max(0.5f, configuredRocketSpeed) : rocketSpeed;
            canDamageOwnerInArea = data[22] is bool configuredOwnerDamage && configuredOwnerDamage;
        }
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
            else if (IsSmallRedPlasma())
            {
                EnsurePlasmaGlow(spriteRenderer);
            }
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
        return string.Equals(hitEffectId, "auto_turret", System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsGatlingProjectile()
    {
        return string.Equals(hitEffectId, "gatling", System.StringComparison.OrdinalIgnoreCase);
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
        rocketTrailLine.useWorldSpace = true;
        rocketTrailLine.positionCount = 2;
        rocketTrailLine.textureMode = LineTextureMode.Stretch;
        rocketTrailLine.alignment = LineAlignment.View;
        rocketTrailLine.numCapVertices = 3;
        rocketTrailLine.numCornerVertices = 2;
        rocketTrailLine.material = new Material(Shader.Find("Sprites/Default"));
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
            line.material = new Material(Shader.Find("Sprites/Default"));
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

        if (IsRocketProjectile())
            UpdateRocketProjectileVisual();
    }

    void UpdateRocketProjectileVisual()
    {
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
            line.material = new Material(Shader.Find("Sprites/Default"));
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
            line.material = new Material(Shader.Find("Sprites/Default"));
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
            line.material = new Material(Shader.Find("Sprites/Default"));
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
            line.material = new Material(Shader.Find("Sprites/Default"));
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

    Vector2 GetTravelDirection()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.001f)
            return rb.linearVelocity.normalized;

        Vector2 up = transform.up;
        return up.sqrMagnitude > 0.001f ? up.normalized : Vector2.up;
    }

    [PunRPC]
    void PlayImpactVfx(string effectId, float x, float y, float directionX, float directionY, float scale)
    {
        Vector2 direction = new Vector2(directionX, directionY);
        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.up;

        if (string.Equals(effectId, "rocket", System.StringComparison.OrdinalIgnoreCase))
            AudioManager.Instance.PlayRocketExplosionAt(new Vector3(x, y, transform.position.z));

        BulletImpactVfx.Spawn(effectId, new Vector3(x, y, transform.position.z - 0.04f), direction.normalized, scale);
    }

    float GetOwnerLength()
    {
        PhotonView ownerView = PhotonView.Find(ownerViewID);
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
