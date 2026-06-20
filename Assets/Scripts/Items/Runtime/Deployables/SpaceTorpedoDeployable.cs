using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class SpaceTorpedoDeployable : PlayerDeployableBase
{
    const float FlightSpeed = 9.4f;
    const float MaxFlightLifetime = 4.1f;
    const float ExplosionRange = 2.35f;
    const float ExplosionCoreRadius = 0.42f;
    const float ExplosionHalfAngleDegrees = 34f;
    const int ExplosionDamage = 82;
    const int ObstacleDamage = 115;
    static readonly Collider2D[] ExplosionHits = new Collider2D[128];

    Vector2 launchDirection = Vector2.up;
    float launchedAt;
    bool detonationStarted;
    TrailRenderer engineTrail;
    SpriteRenderer noseGlowRenderer;

    protected override int MaxHp => 30;
    protected override int MaxShield => 10;
    protected override float VisualTargetSize => 0.64f;
    protected override float CollisionRadius => 0.2f;
    protected override string SpriteResourcePath => "Items/space_torpedo_projectile";
    protected override string EditorSpritePath => "Assets/Resources/Items/space_torpedo_projectile.png";

    void Awake()
    {
        if (PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
    }

    void Start()
    {
        if (PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
        else
            enabled = false;
    }

    public void InitializeFromPhotonData()
    {
        if (initialized)
            return;

        object[] data = photonView != null ? photonView.InstantiationData : null;
        if (data != null && data.Length > 3)
        {
            launchDirection = new Vector2(ConvertToFloat(data[2]), ConvertToFloat(data[3]));
            if (launchDirection.sqrMagnitude <= 0.001f)
                launchDirection = Vector2.up;
            launchDirection.Normalize();
        }

        InitializeCommon();
        transform.up = launchDirection;
        launchedAt = Time.time;
        ConfigureEngineTrail();
        if (body != null)
            body.linearVelocity = launchDirection * FlightSpeed;

        AudioManager.Instance.PlayRocketLaunchAt(transform.position);
    }

    protected override void ConfigurePhysics()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (body != null)
        {
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.simulated = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.mass = 0.18f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null)
            circle = gameObject.AddComponent<CircleCollider2D>();

        circle.enabled = true;
        circle.isTrigger = false;
        SetWorldRadius(circle, CollisionRadius);

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
            box.enabled = false;
    }

    void Update()
    {
        if (!initialized && PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();

        if (!initialized || detonationStarted)
            return;

        transform.up = launchDirection;
        UpdateEngineVisuals();
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (body != null)
            body.linearVelocity = launchDirection * FlightSpeed;

        if (Time.time >= launchedAt + MaxFlightLifetime)
            DetonateOnMaster(transform.position);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!PhotonNetwork.IsMasterClient || detonationStarted || collision == null)
            return;

        Collider2D hit = collision.collider;
        if (!IsBlockingCollider(hit))
            return;

        Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
        DetonateOnMaster(point);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!PhotonNetwork.IsMasterClient || detonationStarted)
            return;

        if (IsBlockingCollider(other))
            DetonateOnMaster(transform.position);
    }

    bool IsBlockingCollider(Collider2D hit)
    {
        if (hit == null)
            return false;

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
            return false;

        if (hit.GetComponentInParent<Bullet>() != null)
            return false;

        PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
        if (health != null && health.photonView != null && health.photonView.ViewID == ownerShipViewId)
            return false;

        if (!hit.isTrigger)
            return true;

        return health != null ||
               hit.GetComponentInParent<PlayerDeployableBase>() != null ||
               hit.GetComponentInParent<ObstacleChunk>() != null ||
               hit.GetComponentInParent<MovingSpaceObject>() != null ||
               hit.GetComponentInParent<Treasure>() != null ||
               hit.GetComponentInParent<ShipWreck>() != null ||
               hit.GetComponentInParent<DroppedCargoCrate>() != null;
    }

    void DetonateOnMaster(Vector3 worldPosition)
    {
        if (!PhotonNetwork.IsMasterClient || detonationStarted)
            return;

        detonationStarted = true;
        destroyed = true;
        BroadcastDetonationEffectsAndDamage(worldPosition);
        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    protected override void OnDestroyedByDamage()
    {
        if (detonationStarted)
            return;

        detonationStarted = true;
        BroadcastDetonationEffectsAndDamage(transform.position);
    }

    void BroadcastDetonationEffectsAndDamage(Vector3 worldPosition)
    {
        photonView.RPC(
            nameof(PlaySpaceTorpedoConeExplosionRpc),
            RpcTarget.All,
            worldPosition.x,
            worldPosition.y,
            launchDirection.x,
            launchDirection.y);
        ApplyExplosionDamage(worldPosition);
    }

    [PunRPC]
    void PlaySpaceTorpedoConeExplosionRpc(float x, float y, float directionX, float directionY)
    {
        Vector2 direction = new Vector2(directionX, directionY);
        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.up;
        direction.Normalize();

        Vector3 worldPosition = new Vector3(x, y, transform.position.z);
        SpaceTorpedoConeExplosionVfx.Spawn(worldPosition, direction, ExplosionRange, ExplosionHalfAngleDegrees);
        AudioManager.Instance.PlaySpaceTorpedoExplosionAt(worldPosition);
    }

    void ApplyExplosionDamage(Vector2 center)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };

        int hitCount = Physics2D.OverlapCircle(center, ExplosionRange, filter, ExplosionHits);
        HashSet<int> damagedViews = new HashSet<int>();
        HashSet<string> damagedObstacles = new HashSet<string>(StringComparer.Ordinal);
        WeaponHitContext hitContext = new WeaponHitContext(
            WeaponDamageType.Explosive,
            WeaponDeliveryMethod.DirectProjectile,
            WeaponDeliveryFlags.AreaDamage,
            PlayerHealth.DamageSourceExplosive);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = ExplosionHits[i];
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            if (!IsInsideExplosionCone(hit, center))
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable != this && deployable.photonView != null && deployable.CanBeTargeted && damagedViews.Add(deployable.photonView.ViewID))
            {
                deployable.photonView.RPC(
                    nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                    RpcTarget.MasterClient,
                    ExplosionDamage,
                    ExplosionDamage,
                    ownerShipViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
                continue;
            }

            PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
            if (health != null && health.photonView != null && !health.IsWreck && health.photonView.ViewID != ownerShipViewId && damagedViews.Add(health.photonView.ViewID))
            {
                health.photonView.RPC(
                    nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                    RpcTarget.MasterClient,
                    ExplosionDamage,
                    ExplosionDamage,
                    ownerShipViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
                continue;
            }

            ObstacleChunk obstacle = hit.GetComponentInParent<ObstacleChunk>();
            if (obstacle != null && !string.IsNullOrWhiteSpace(obstacle.StableId) && damagedObstacles.Add(obstacle.StableId) && RoomSettings.AreObstaclesDestructible())
            {
                SpaceObjectMotionSync.RequestObstacleDamage(obstacle.StableId, ObstacleDamage);
                continue;
            }

            MovingSpaceObject movingObject = hit.GetComponentInParent<MovingSpaceObject>();
            if (movingObject != null && !string.IsNullOrWhiteSpace(movingObject.StableId) && damagedObstacles.Add(movingObject.StableId))
            {
                Vector2 push = launchDirection;
                if (push.sqrMagnitude <= 0.001f)
                    push = Vector2.up;
                SpaceObjectMotionSync.RequestImpulse(movingObject.StableId, push.normalized * 4.3f);
            }
        }
    }

    bool IsInsideExplosionCone(Collider2D hit, Vector2 center)
    {
        Vector2 closest = hit.ClosestPoint(center);
        Vector2 toHit = closest - center;
        float sqrDistance = toHit.sqrMagnitude;
        if (sqrDistance <= ExplosionCoreRadius * ExplosionCoreRadius)
            return true;

        if (sqrDistance > ExplosionRange * ExplosionRange)
            return false;

        Vector2 direction = launchDirection.sqrMagnitude > 0.001f ? launchDirection.normalized : Vector2.up;
        float dot = Vector2.Dot(direction, toHit.normalized);
        return dot >= Mathf.Cos(ExplosionHalfAngleDegrees * Mathf.Deg2Rad);
    }

    void ConfigureEngineTrail()
    {
        GameObject trailObject = new GameObject("SpaceTorpedoTrail");
        trailObject.transform.SetParent(transform, false);
        trailObject.transform.localPosition = new Vector3(0f, -0.24f, 0.05f);
        engineTrail = trailObject.AddComponent<TrailRenderer>();
        EngineTrailVisualUtility.ConfigureTrailBase(engineTrail);
        engineTrail.time = 0.34f;
        engineTrail.minVertexDistance = 0.015f;
        engineTrail.widthMultiplier = 0.08f;
        engineTrail.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        engineTrail.sortingOrder = 36;
        engineTrail.emitting = true;

        Gradient trailGradient = new Gradient();
        trailGradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.45f, 0.96f, 1f), 0f), new GradientColorKey(new Color(1f, 0.22f, 0.06f), 1f) },
            new[] { new GradientAlphaKey(0.92f, 0f), new GradientAlphaKey(0f, 1f) });
        engineTrail.colorGradient = trailGradient;

        GameObject glowObject = new GameObject("SpaceTorpedoNoseGlow");
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = new Vector3(0f, 0.34f, -0.02f);
        glowObject.transform.localScale = Vector3.one * 0.16f;
        noseGlowRenderer = glowObject.AddComponent<SpriteRenderer>();
        noseGlowRenderer.sprite = RuntimeSpriteUtility.CreateArrowSprite();
        noseGlowRenderer.color = new Color(0.42f, 0.96f, 1f, 0.52f);
        noseGlowRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        noseGlowRenderer.sortingOrder = 50;
    }

    void UpdateEngineVisuals()
    {
        if (noseGlowRenderer == null)
            return;

        float pulse = 0.82f + Mathf.Sin(Time.time * 19f) * 0.18f;
        noseGlowRenderer.transform.localScale = Vector3.one * (0.16f * pulse);
    }

    protected override void OnDestroy()
    {
        if (engineTrail != null)
            engineTrail.emitting = false;

        base.OnDestroy();
    }

    [PunRPC]
    public new void PlayDeployableHitRpc(bool shieldHit, float x, float y)
    {
        PlayDeployableHitFeedback(shieldHit, x, y);
    }

    [PunRPC]
    public new void PlayDeployableDestroyedRpc()
    {
        if (!detonationStarted)
            SpaceTorpedoConeExplosionVfx.Spawn(transform.position, launchDirection, ExplosionRange, ExplosionHalfAngleDegrees);
    }
}
