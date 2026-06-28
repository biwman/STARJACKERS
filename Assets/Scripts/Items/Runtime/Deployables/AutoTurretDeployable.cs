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
    const float TargetRefreshInterval = 0.1f;
    const string BulletEffectId = "auto_turret";
    const string ContainerShipBulletEffectId = "container_auto_cannon";
    static readonly Color BulletColor = new Color(1f, 0.62f, 0.12f, 1f);

    float nextShotTime;
    int shotsSinceBreak;
    float breakUntil;
    bool containerShipAutoCannon;
    bool neutralRiderAutoCannon;
    Transform cachedTarget;
    float nextTargetRefreshTime;
    Collider2D[] cachedOwnColliders;
    Collider2D[] cachedOwnerColliders;
    int cachedOwnerColliderViewId;

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
        object[] data = photonView != null ? photonView.InstantiationData : null;
        containerShipAutoCannon = PlayerDeployableRuntime.IsContainerShipAutoCannonData(data);
        neutralRiderAutoCannon = PlayerDeployableRuntime.IsNeutralRiderAutoTurretData(data);
        cachedTarget = null;
        cachedOwnColliders = null;
        cachedOwnerColliders = null;
        cachedOwnerColliderViewId = 0;
        InitializeCommon();
    }

    void Update()
    {
        if (!initialized && PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();

        if (!initialized || destroyed || !PhotonNetwork.IsMasterClient)
            return;

        Transform target = ResolveTarget();
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

    Transform ResolveTarget()
    {
        if (Time.time >= nextTargetRefreshTime || !IsCachedTargetValid(cachedTarget))
        {
            nextTargetRefreshTime = Time.time + TargetRefreshInterval;
            cachedTarget = FindNearestTarget();
        }

        return cachedTarget;
    }

    bool IsCachedTargetValid(Transform target)
    {
        if (target == null)
            return false;

        PlayerHealth candidate = target.GetComponent<PlayerHealth>();
        if (!IsValidTarget(candidate, ResolveOwnerActorNumber()))
            return false;

        return Vector2.Distance(transform.position, target.position) <= TargetRange;
    }

    Transform FindNearestTarget()
    {
        int ownerActorNumber = ResolveOwnerActorNumber();
        PlayerHealth[] healths = RuntimeSceneQueryCache.GetPlayers();
        Transform best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (!IsValidTarget(candidate, ownerActorNumber))
                continue;

            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            if (distance > TargetRange || distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = candidate.transform;
        }

        return best;
    }

    bool IsValidTarget(PlayerHealth candidate, int ownerActorNumber)
    {
        if (candidate == null || candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.photonView == null)
            return false;

        if (candidate.photonView.ViewID == ownerShipViewId || candidate.GetComponent<LureBeaconDecoy>() != null)
            return false;

        if (containerShipAutoCannon || neutralRiderAutoCannon)
            return IsPlayerShipTarget(candidate);

        EnemyBot enemyBot = candidate.GetComponent<EnemyBot>();
        return enemyBot != null ||
               candidate.IsBotControlled ||
               candidate.IsNeutralRiderControlled ||
               HasDifferentPhotonOwner(candidate, ownerActorNumber) ||
               IsOtherPlayerShipTarget(candidate, ownerActorNumber);
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
            (marker == PlayerDeployableRuntime.AutoTurretMarker || marker == PlayerDeployableRuntime.NeutralRiderAutoTurretMarker))
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

        GameObject bullet = ProjectileSpawner.SpawnNetworkBullet(
            "Bullet",
            position,
            Quaternion.Euler(0f, 0f, angle),
            data,
            ownerShipViewId,
            direction.normalized * BulletSpeed,
            true);

        if (bullet == null)
            return;

        IgnoreBulletCollisions(bullet);
    }

    void IgnoreBulletCollisions(GameObject bullet)
    {
        if (bullet == null)
            return;

        ProjectileSpawner.IgnoreCollisions(bullet, GetOwnColliders());
        ProjectileSpawner.IgnoreCollisions(bullet, GetOwnerColliders());
    }

    Collider2D[] GetOwnColliders()
    {
        if (cachedOwnColliders == null || cachedOwnColliders.Length == 0)
            cachedOwnColliders = GetComponentsInChildren<Collider2D>(true);

        return cachedOwnColliders;
    }

    Collider2D[] GetOwnerColliders()
    {
        if (ownerShipViewId <= 0)
            return null;

        if (cachedOwnerColliders != null && cachedOwnerColliderViewId == ownerShipViewId)
            return cachedOwnerColliders;

        cachedOwnerColliderViewId = ownerShipViewId;
        PhotonView ownerView = PhotonView.Find(ownerShipViewId);
        cachedOwnerColliders = ownerView != null ? ownerView.GetComponentsInChildren<Collider2D>(true) : null;
        return cachedOwnerColliders;
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
