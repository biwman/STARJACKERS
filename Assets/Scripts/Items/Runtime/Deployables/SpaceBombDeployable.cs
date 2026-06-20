using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class SpaceBombDeployable : PlayerDeployableBase
{
    const float ArmDelay = 1f;
    const float RearEjectDistance = 1.15f;
    const float FlightSpeed = 3.7f;
    const float MaxFlightLifetime = 7.5f;
    const float ExplosionRadius = 4.8f;
    const int ExplosionDamage = 170;
    const int ObstacleDamage = 260;
    const float OwnerCollisionEnablePadding = 0.05f;
    const float BombColliderWidth = 0.9f;
    const float BombColliderLength = 3.25f;
    static readonly Collider2D[] ExplosionHits = new Collider2D[160];

    Vector2 launchDirection = Vector2.up;
    Vector2 ejectStartPosition;
    Vector2 ejectEndPosition;
    float armedAt;
    float ejectStartedAt;
    float launchedAt;
    bool launched;
    bool detonationStarted;
    int dynamicZoomToken;
    TrailRenderer engineTrail;
    SpriteRenderer engineGlowRenderer;

    protected override int MaxHp => 80;
    protected override int MaxShield => 40;
    protected override float VisualTargetSize => 3.7f;
    protected override float CollisionRadius => 0.55f;
    protected override string SpriteResourcePath => "Items/space_bomb_gadget_bullet";
    protected override string EditorSpritePath => "Assets/space_bomb_gadget_bullet.png";

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
        ejectStartedAt = Time.time;
        armedAt = Time.time + ArmDelay;
        ejectStartPosition = transform.position;
        ejectEndPosition = ejectStartPosition - launchDirection * RearEjectDistance;
        ConfigureEngineTrail();
        SetEngineActive(false);
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
            body.mass = 0.32f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null)
            circle.enabled = false;

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider2D>();

        box.enabled = true;
        box.isTrigger = false;
        box.offset = Vector2.zero;
        SetWorldBoxSize(box, new Vector2(BombColliderWidth, BombColliderLength));
    }

    protected override void ConfigureVisuals()
    {
        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;

        Transform visualTransform = transform.Find("SpaceBombSprite");
        if (visualTransform == null)
        {
            GameObject visualObject = new GameObject("SpaceBombSprite");
            visualObject.transform.SetParent(transform, false);
            visualTransform = visualObject.transform;
        }

        spriteRenderer = visualTransform.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();

        Sprite sprite = RuntimeSpriteUtility.LoadSprite(SpriteResourcePath, EditorSpritePath);
        if (sprite != null)
        {
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = Color.white;
            RuntimeSpriteUtility.FitRenderer(spriteRenderer, VisualTargetSize);
        }

        visualTransform.localPosition = Vector3.zero;
        visualTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = 48;
    }

    void Update()
    {
        if (!initialized && PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();

        if (!initialized || detonationStarted)
            return;

        UpdateRearEjectionMotion();
        UpdateArmingVisual();
        if (launched && dynamicZoomToken > 0)
            DynamicCameraZoomController.UpdateWorldPosition(dynamicZoomToken, transform.position);

        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!launched && Time.time >= armedAt)
        {
            LaunchOnMaster();
            return;
        }

        if (launched && Time.time >= launchedAt + MaxFlightLifetime)
            DetonateOnMaster(transform.position);
    }

    void LaunchOnMaster()
    {
        if (launched || detonationStarted)
            return;

        launched = true;
        launchedAt = Time.time;
        SetOwnerCollisionIgnored(false);
        SetBombPosition(ejectEndPosition);

        if (body != null)
        {
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.linearVelocity = launchDirection * FlightSpeed;
        }

        transform.up = launchDirection;
        photonView.RPC(nameof(SetSpaceBombLaunchedRpc), RpcTarget.All, launchDirection.x, launchDirection.y, ejectEndPosition.x, ejectEndPosition.y);
    }

    [PunRPC]
    void SetSpaceBombLaunchedRpc(float directionX, float directionY, float launchX, float launchY)
    {
        launchDirection = new Vector2(directionX, directionY);
        if (launchDirection.sqrMagnitude <= 0.001f)
            launchDirection = Vector2.up;
        launchDirection.Normalize();
        SetBombPosition(new Vector2(launchX, launchY));
        transform.up = launchDirection;
        launched = true;
        launchedAt = Time.time;
        if (body != null)
        {
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.linearVelocity = launchDirection * FlightSpeed;
        }

        SetEngineActive(true);
        dynamicZoomToken = DynamicCameraZoomController.Request(DynamicCameraZoomProfiles.SpaceBombFlight, transform.position, MaxFlightLifetime + 0.4f);
        AudioManager.Instance.PlayRocketLaunchAt(transform.position);
    }

    void UpdateRearEjectionMotion()
    {
        if (launched)
            return;

        float t = Mathf.Clamp01((Time.time - ejectStartedAt) / Mathf.Max(0.001f, ArmDelay));
        float eased = Mathf.SmoothStep(0f, 1f, t);
        Vector2 position = Vector2.Lerp(ejectStartPosition, ejectEndPosition, eased);
        SetBombPosition(position);
        transform.up = launchDirection;
    }

    void SetBombPosition(Vector2 position)
    {
        if (body != null)
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
        }
        else
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!PhotonNetwork.IsMasterClient || !launched || detonationStarted || collision == null)
            return;

        Collider2D hit = collision.collider;
        if (!IsBlockingCollider(hit))
            return;

        Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
        DetonateOnMaster(point);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!PhotonNetwork.IsMasterClient || !launched || detonationStarted)
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

        if (!hit.isTrigger)
            return true;

        if (hit.GetComponentInParent<PlayerHealth>() != null)
            return false;

        return hit.GetComponentInParent<PlayerDeployableBase>() != null ||
               hit.GetComponentInParent<ObstacleChunk>() != null ||
               hit.GetComponentInParent<MovingSpaceObject>() != null ||
               hit.GetComponentInParent<Treasure>() != null ||
               hit.GetComponentInParent<ShipWreck>() != null ||
               hit.GetComponentInParent<DroppedCargoCrate>() != null;
    }

    static void SetWorldBoxSize(BoxCollider2D box, Vector2 worldSize)
    {
        if (box == null)
            return;

        float scaleX = Mathf.Max(Mathf.Abs(box.transform.lossyScale.x), 0.001f);
        float scaleY = Mathf.Max(Mathf.Abs(box.transform.lossyScale.y), 0.001f);
        box.size = new Vector2(
            Mathf.Max(0.01f, worldSize.x / scaleX),
            Mathf.Max(0.01f, worldSize.y / scaleY));
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
        SpaceObjectMotionSync.BroadcastSpaceMineDetonation(worldPosition, ExplosionRadius);
        ApplyExplosionDamage(worldPosition);
    }

    void ApplyExplosionDamage(Vector2 center)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };

        int hitCount = Physics2D.OverlapCircle(center, ExplosionRadius, filter, ExplosionHits);
        HashSet<int> damagedViews = new HashSet<int>();
        HashSet<string> damagedObstacles = new HashSet<string>(StringComparer.Ordinal);
        WeaponHitContext hitContext = new WeaponHitContext(
            WeaponDamageType.Explosive,
            WeaponDeliveryMethod.DirectProjectile,
            WeaponDeliveryFlags.AreaDamage | WeaponDeliveryFlags.Delayed,
            PlayerHealth.DamageSourceExplosive);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = ExplosionHits[i];
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
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
            if (health != null && health.photonView != null && !health.IsWreck && damagedViews.Add(health.photonView.ViewID))
            {
                int scaledDamage = Mathf.RoundToInt(ExplosionDamage * ResolveBossDamageMultiplier(health));
                health.photonView.RPC(
                    nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                    RpcTarget.MasterClient,
                    scaledDamage,
                    scaledDamage,
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
                Vector2 push = ((Vector2)movingObject.transform.position - center);
                if (push.sqrMagnitude <= 0.001f)
                    push = launchDirection;
                SpaceObjectMotionSync.RequestImpulse(movingObject.StableId, push.normalized * 5.8f);
            }
        }

        TryDamageSpecificView(ownerShipViewId, center, damagedViews, hitContext);
    }

    void TryDamageSpecificView(int targetViewId, Vector2 center, HashSet<int> damagedViews, WeaponHitContext hitContext)
    {
        if (targetViewId <= 0 || damagedViews.Contains(targetViewId))
            return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null || Vector2.Distance(center, targetView.transform.position) > ExplosionRadius + OwnerCollisionEnablePadding)
            return;

        PlayerHealth health = targetView.GetComponent<PlayerHealth>();
        if (health != null && health.photonView != null && !health.IsWreck && damagedViews.Add(health.photonView.ViewID))
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
    }

    static float ResolveBossDamageMultiplier(PlayerHealth health)
    {
        EnemyBot bot = health != null ? health.GetComponent<EnemyBot>() : null;
        if (bot == null)
            return 1f;

        switch (bot.Kind)
        {
            case EnemyBotKind.Mothership:
            case EnemyBotKind.PirateBase:
            case EnemyBotKind.SpaceManta:
            case EnemyBotKind.GravitySquid:
            case EnemyBotKind.HunterLance:
            case EnemyBotKind.CosmicWorm:
            case EnemyBotKind.RiftWarden:
                return 2f;
            default:
                return 1f;
        }
    }

    void SetOwnerCollisionIgnored(bool ignored)
    {
        if (ownerShipViewId <= 0)
            return;

        PhotonView ownerView = PhotonView.Find(ownerShipViewId);
        if (ownerView == null)
            return;

        Collider2D[] ownColliders = GetComponentsInChildren<Collider2D>(true);
        Collider2D[] ownerColliders = ownerView.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider2D ownCollider = ownColliders[i];
            if (ownCollider == null)
                continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider2D ownerCollider = ownerColliders[j];
                if (ownerCollider != null)
                    Physics2D.IgnoreCollision(ownCollider, ownerCollider, ignored);
            }
        }
    }

    void ConfigureEngineTrail()
    {
        GameObject trailObject = new GameObject("SpaceBombEngineTrail");
        trailObject.transform.SetParent(transform, false);
        trailObject.transform.localPosition = new Vector3(0f, -0.42f, 0.05f);
        engineTrail = trailObject.AddComponent<TrailRenderer>();
        EngineTrailVisualUtility.ConfigureTrailBase(engineTrail);
        engineTrail.time = 0.62f;
        engineTrail.minVertexDistance = 0.02f;
        engineTrail.widthMultiplier = 0.16f;
        engineTrail.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        engineTrail.sortingOrder = 35;
        engineTrail.emitting = false;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.88f, 0.28f), 0f), new GradientColorKey(new Color(1f, 0.18f, 0.03f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
        engineTrail.colorGradient = gradient;

        GameObject glowObject = new GameObject("SpaceBombEngineGlow");
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = new Vector3(0f, -0.43f, -0.02f);
        glowObject.transform.localScale = Vector3.one * 0.3f;
        engineGlowRenderer = glowObject.AddComponent<SpriteRenderer>();
        engineGlowRenderer.sprite = RuntimeSpriteUtility.CreateArrowSprite();
        engineGlowRenderer.color = new Color(1f, 0.32f, 0.06f, 0.55f);
        engineGlowRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        engineGlowRenderer.sortingOrder = 47;
        engineGlowRenderer.enabled = false;
    }

    void SetEngineActive(bool active)
    {
        if (engineTrail != null)
            engineTrail.emitting = active;

        if (engineGlowRenderer != null)
            engineGlowRenderer.enabled = active;
    }

    void UpdateArmingVisual()
    {
        if (spriteRenderer == null)
            return;

        if (launched)
        {
            spriteRenderer.color = Color.white;
            if (engineGlowRenderer != null)
            {
                float pulse = 0.82f + Mathf.Sin(Time.time * 22f) * 0.18f;
                engineGlowRenderer.transform.localScale = Vector3.one * (0.3f * pulse);
            }
            return;
        }

        float t = Mathf.Clamp01(1f - ((armedAt - Time.time) / ArmDelay));
        float pulseAlpha = 0.55f + Mathf.Sin(Time.time * Mathf.Lerp(5f, 13f, t)) * 0.12f;
        spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.62f, 0.42f, 1f), Mathf.Clamp01(pulseAlpha * t));
    }

    protected override void OnDestroy()
    {
        DynamicCameraZoomController.Cancel(dynamicZoomToken);
        dynamicZoomToken = 0;
        SetEngineActive(false);
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
            EnemyBot.SpawnSpaceMineDetonationEffects(transform.position, ExplosionRadius);
    }
}
