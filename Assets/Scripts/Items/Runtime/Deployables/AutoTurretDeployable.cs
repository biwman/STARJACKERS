using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class AutoTurretDeployable : PlayerDeployableBase
{
    const float FireInterval = 0.333f;
    const int ShotsBeforeBreak = 5;
    const float BreakDuration = 3f;
    const float TargetRange = 8.2f;
    const float BulletSpeed = 11.5f;
    const int Damage = 10;
    const float BulletScale = 0.72f;
    const float MuzzleForwardOffset = 0.72f;
    const float MuzzleSideOffset = 0.18f;
    const float FireAngleTolerance = 10f;
    const float RotationSpeedDegreesPerSecond = 180f;
    const string BulletEffectId = "auto_turret";
    const string ContainerShipBulletEffectId = "container_auto_cannon";
    static readonly Color BulletColor = new Color(1f, 0.62f, 0.12f, 1f);

    float nextShotTime;
    int shotsSinceBreak;
    float breakUntil;
    bool containerShipAutoCannon;

    protected override int MaxHp => 50;
    protected override int MaxShield => 50;
    protected override float VisualTargetSize => 0.86f;
    protected override float CollisionRadius => 0.38f;
    protected override string SpriteResourcePath => "auto_turret_top_down_resource";
    protected override string EditorSpritePath => "Assets/auto_turret_top_down.png";

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
        containerShipAutoCannon = PlayerDeployableRuntime.IsContainerShipAutoCannonData(photonView != null ? photonView.InstantiationData : null);
        InitializeCommon();
    }

    void Update()
    {
        if (!initialized && PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();

        if (!initialized || destroyed || !PhotonNetwork.IsMasterClient)
            return;

        Transform target = FindNearestTarget();
        if (target == null)
            return;

        Vector2 direction = target.position - transform.position;
        if (direction.sqrMagnitude < 0.001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, 0f, angle), RotationSpeedDegreesPerSecond * Time.deltaTime);

        if (Time.time < breakUntil)
            return;

        if (Time.time < nextShotTime)
            return;

        Vector2 muzzleDirection = transform.up;
        if (muzzleDirection.sqrMagnitude < 0.001f)
            muzzleDirection = direction.normalized;
        else
            muzzleDirection.Normalize();

        if (Vector2.Angle(muzzleDirection, direction.normalized) > FireAngleTolerance)
            return;

        nextShotTime = Time.time + FireInterval;
        FirePair(muzzleDirection);
        shotsSinceBreak++;
        if (shotsSinceBreak >= ShotsBeforeBreak)
        {
            shotsSinceBreak = 0;
            breakUntil = Time.time + BreakDuration;
            nextShotTime = breakUntil;
        }
    }

    Transform FindNearestTarget()
    {
        int ownerActorNumber = ResolveOwnerActorNumber();
        PlayerHealth[] healths = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        Transform best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (candidate == null || candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.photonView == null)
                continue;

            if (candidate.photonView.ViewID == ownerShipViewId || candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            if (containerShipAutoCannon)
            {
                if (!IsPlayerShipTarget(candidate))
                    continue;
            }
            else
            {
                EnemyBot enemyBot = candidate.GetComponent<EnemyBot>();
                bool hostile = enemyBot != null ||
                               candidate.IsBotControlled ||
                               HasDifferentPhotonOwner(candidate, ownerActorNumber) ||
                               IsOtherPlayerShipTarget(candidate, ownerActorNumber);
                if (!hostile)
                    continue;
            }

            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            if (distance > TargetRange || distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = candidate.transform;
        }

        return best;
    }

    int ResolveOwnerActorNumber()
    {
        PhotonView ownerView = ownerShipViewId > 0 ? PhotonView.Find(ownerShipViewId) : null;
        if (ownerView != null && ownerView.OwnerActorNr > 0)
            return ownerView.OwnerActorNr;

        object[] data = photonView != null ? photonView.InstantiationData : null;
        if (data != null &&
            data.Length > 2 &&
            data[0] is string marker &&
            marker == PlayerDeployableRuntime.AutoTurretMarker)
        {
            return Mathf.Max(0, ConvertToInt(data[2]));
        }

        return 0;
    }

    static bool HasDifferentPhotonOwner(PlayerHealth candidate, int ownerActorNumber)
    {
        return ownerActorNumber > 0 &&
               candidate != null &&
               candidate.photonView != null &&
               candidate.photonView.OwnerActorNr != ownerActorNumber;
    }

    static bool IsOtherPlayerShipTarget(PlayerHealth candidate, int ownerActorNumber)
    {
        if (!IsPlayerShipTarget(candidate))
            return false;

        int candidateActorNumber = candidate.photonView.OwnerActorNr;
        return ownerActorNumber <= 0 ||
               candidateActorNumber <= 0 ||
               candidateActorNumber != ownerActorNumber;
    }

    static bool IsPlayerShipTarget(PlayerHealth candidate)
    {
        return candidate != null &&
               candidate.photonView != null &&
               !candidate.IsBotControlled &&
               !candidate.IsNeutralRiderControlled &&
               !candidate.IsAstronautControlled &&
               candidate.GetComponent<PlayerDeployableBase>() == null &&
               candidate.GetComponent<LureBeaconDecoy>() == null;
    }

    void FirePair(Vector2 direction)
    {
        Vector2 right = transform.right;
        Vector3 center = transform.position + (Vector3)(direction * MuzzleForwardOffset);
        SpawnBullet(direction, center - (Vector3)(right * MuzzleSideOffset));
        SpawnBullet(direction, center + (Vector3)(right * MuzzleSideOffset));
        photonView.RPC(nameof(PlayTurretShotRpc), RpcTarget.All);
    }

    void SpawnBullet(Vector2 direction, Vector3 position)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        object[] data = Bullet.AppendWeaponMetadata(
            new object[]
            {
                ownerShipViewId,
                Damage,
                BulletScale,
                BulletColor.r,
                BulletColor.g,
                BulletColor.b,
                BulletColor.a,
                TargetRange,
                Damage,
                Damage,
                false,
                0f,
                containerShipAutoCannon ? ContainerShipBulletEffectId : BulletEffectId,
                10f
            },
            WeaponDamageType.Laser,
            WeaponDeliveryMethod.DeployableTurret,
            WeaponDeliveryFlags.Autonomous | WeaponDeliveryFlags.Paired);

        GameObject bullet = PhotonNetwork.Instantiate(
            "Bullet",
            position,
            Quaternion.Euler(0f, 0f, angle),
            0,
            data);

        if (bullet == null)
            return;

        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent != null)
            bulletComponent.ownerViewID = ownerShipViewId;

        Rigidbody2D bulletBody = bullet.GetComponent<Rigidbody2D>();
        if (bulletBody != null)
            bulletBody.linearVelocity = direction.normalized * BulletSpeed;

        Collider2D bulletCollider = bullet.GetComponent<Collider2D>();
        IgnoreBulletCollisions(bulletCollider);
    }

    void IgnoreBulletCollisions(Collider2D bulletCollider)
    {
        if (bulletCollider == null)
            return;

        Collider2D[] ownColliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] != null)
                Physics2D.IgnoreCollision(ownColliders[i], bulletCollider, true);
        }

        PhotonView ownerView = ownerShipViewId > 0 ? PhotonView.Find(ownerShipViewId) : null;
        if (ownerView == null)
            return;

        Collider2D[] ownerColliders = ownerView.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            if (ownerColliders[i] != null)
                Physics2D.IgnoreCollision(ownerColliders[i], bulletCollider, true);
        }
    }

    [PunRPC]
    void PlayTurretShotRpc()
    {
        AudioManager.Instance.PlayAutoShootAt(transform.position);
    }

    [PunRPC]
    public new void PlayDeployableHitRpc(bool shieldHit, float x, float y)
    {
        PlayDeployableHitFeedback(shieldHit, x, y);
    }

    [PunRPC]
    public new void PlayDeployableDestroyedRpc()
    {
        PlayDeployableDestroyedFeedback();
    }
}
