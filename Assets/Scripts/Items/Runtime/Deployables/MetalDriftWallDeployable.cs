using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class MetalDriftWallDeployable : PlayerDeployableBase
{
    const float OwnerCollisionGraceDuration = 0.58f;
    const float MaxDriftSpeed = 2.35f;
    const float MaxAngularSpeed = 58f;
    const float VisualSize = 3.15f;
    const float ColliderScale = 1.08f;

    Vector2 deployForward = Vector2.up;
    Vector2 initialVelocity = Vector2.up;
    float ownerCollisionRestoreAt;
    bool ownerCollisionRestored;

    protected override int MaxHp => 310;
    protected override int MaxShield => 85;
    protected override float VisualTargetSize => VisualSize;
    protected override float CollisionRadius => 1.25f;
    protected override string SpriteResourcePath => "Items/metal_drift_wall";
    protected override string EditorSpritePath => "Assets/Resources/Items/metal_drift_wall.png";
    public override bool CanBeTargetedByEnemyBots => false;

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
            deployForward = new Vector2(ConvertToFloat(data[2]), ConvertToFloat(data[3]));
            if (deployForward.sqrMagnitude <= 0.001f)
                deployForward = Vector2.up;
            deployForward.Normalize();
        }

        if (data != null && data.Length > 5)
        {
            initialVelocity = new Vector2(ConvertToFloat(data[4]), ConvertToFloat(data[5]));
            if (initialVelocity.sqrMagnitude <= 0.001f)
                initialVelocity = deployForward * 1.25f;
        }

        InitializeCommon();
        transform.up = deployForward;
        ownerCollisionRestoreAt = Time.time + OwnerCollisionGraceDuration;
        ownerCollisionRestored = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.ClampMagnitude(initialVelocity, MaxDriftSpeed);
            body.angularVelocity = 0f;
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayShieldChargeAt(transform.position);
    }

    protected override void ConfigureVisuals()
    {
        transform.localScale = Vector3.one;

        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;

        Transform visualTransform = transform.Find("MetalDriftWallSprite");
        if (visualTransform == null)
        {
            GameObject visualObject = new GameObject("MetalDriftWallSprite");
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
            RuntimeSpriteUtility.FitRenderer(spriteRenderer, VisualTargetSize);
        }

        visualTransform.localPosition = Vector3.zero;
        visualTransform.localRotation = Quaternion.identity;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = 47;
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
            body.mass = 14f;
            body.linearDamping = 0.9f;
            body.angularDamping = 3.6f;
            body.constraints = RigidbodyConstraints2D.None;
        }

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null)
            circle.enabled = false;

        BoxCollider2D rootBox = GetComponent<BoxCollider2D>();
        if (rootBox != null)
            rootBox.enabled = false;

        ConfigureColliderSegment("MetalDriftWallColliderCenter", new Vector2(0f, 0.24f), new Vector2(0.72f, 0.4f), 0f);
        ConfigureColliderSegment("MetalDriftWallColliderLeftMid", new Vector2(-0.55f, 0.14f), new Vector2(0.72f, 0.38f), 14f);
        ConfigureColliderSegment("MetalDriftWallColliderRightMid", new Vector2(0.55f, 0.14f), new Vector2(0.72f, 0.38f), -14f);
        ConfigureColliderSegment("MetalDriftWallColliderLeftEnd", new Vector2(-1.06f, -0.12f), new Vector2(0.7f, 0.46f), 33f);
        ConfigureColliderSegment("MetalDriftWallColliderRightEnd", new Vector2(1.06f, -0.12f), new Vector2(0.7f, 0.46f), -33f);
    }

    void ConfigureColliderSegment(string objectName, Vector2 localPosition, Vector2 size, float localAngle)
    {
        Transform segment = transform.Find(objectName);
        if (segment == null)
        {
            GameObject segmentObject = new GameObject(objectName);
            segmentObject.transform.SetParent(transform, false);
            segment = segmentObject.transform;
        }

        Vector2 scaledPosition = localPosition * ColliderScale;
        segment.localPosition = new Vector3(scaledPosition.x, scaledPosition.y, 0f);
        segment.localRotation = Quaternion.Euler(0f, 0f, localAngle);
        segment.localScale = Vector3.one;

        BoxCollider2D box = segment.GetComponent<BoxCollider2D>();
        if (box == null)
            box = segment.gameObject.AddComponent<BoxCollider2D>();

        box.enabled = true;
        box.isTrigger = false;
        box.offset = Vector2.zero;
        box.size = size * ColliderScale;
    }

    void Update()
    {
        if (!initialized && PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();

        if (!initialized || destroyed)
            return;

        if (!ownerCollisionRestored && Time.time >= ownerCollisionRestoreAt)
        {
            ownerCollisionRestored = true;
            SetOwnerCollisionIgnored(false);
        }

        UpdateDamageTint();
    }

    void FixedUpdate()
    {
        if (!initialized || destroyed || body == null)
            return;

        body.linearVelocity = Vector2.ClampMagnitude(body.linearVelocity, MaxDriftSpeed);
        body.angularVelocity = Mathf.Clamp(body.angularVelocity, -MaxAngularSpeed, MaxAngularSpeed);
    }

    void UpdateDamageTint()
    {
        if (spriteRenderer == null)
            return;

        float shieldPulse = currentShield > 0 ? 0.5f + Mathf.Sin(Time.time * 8f) * 0.5f : 0f;
        float hpRatio = Mathf.Clamp01(currentHp / (float)Mathf.Max(1, MaxHp));
        Color intactColor = Color.Lerp(Color.white, new Color(0.76f, 0.94f, 1f, 1f), shieldPulse * 0.12f);
        Color damagedColor = new Color(1f, 0.72f, 0.58f, 1f);
        spriteRenderer.color = Color.Lerp(damagedColor, intactColor, Mathf.Lerp(0.45f, 1f, hpRatio));
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

    protected override void OnDestroyedByDamage()
    {
        SpaceObjectMotionSync.BroadcastSpaceMineDetonation(transform.position, 1.05f);
    }

    [PunRPC]
    public new void PlayDeployableHitRpc(bool shieldHit, float x, float y)
    {
        PlayDeployableHitFeedback(shieldHit, x, y);
    }

    [PunRPC]
    public new void PlayDeployableDestroyedRpc()
    {
        EnemyBot.SpawnSpaceMineDetonationEffects(transform.position, 1.05f);
    }
}
