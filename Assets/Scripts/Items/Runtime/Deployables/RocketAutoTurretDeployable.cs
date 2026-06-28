using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class RocketAutoTurretDeployable : PlayerDeployableBase
{
    const float FireInterval = 2.4f;
    const float TargetRange = 10.8f;
    const float RocketSpeed = 5.8f;
    const int Damage = 42;
    const float RocketScale = 0.74f;
    const float AreaDamageRadius = 1.05f;
    const float MuzzleForwardOffset = 0.64f;
    const float FireAngleTolerance = 14f;
    const float RotationSpeedDegreesPerSecond = 120f;
    const float TargetRefreshInterval = 0.1f;
    const string RocketEffectId = "rocket";
    static readonly Color RocketColor = new Color(1f, 0.58f, 0.18f, 1f);

    float nextShotTime;
    bool warBaseDefenseTurret;
    bool neutralRiderRocketTurret;
    PhotonView cachedTargetView;
    float nextTargetRefreshTime;
    Collider2D[] cachedOwnColliders;
    Collider2D[] cachedOwnerColliders;
    int cachedOwnerColliderViewId;

    public bool IsWarBaseDefenseTurret => warBaseDefenseTurret;
    protected override int MaxHp => warBaseDefenseTurret ? 85 : 64;
    protected override int MaxShield => warBaseDefenseTurret ? 90 : 55;
    protected override float VisualTargetSize => warBaseDefenseTurret ? 0.92f : 0.82f;
    protected override float CollisionRadius => warBaseDefenseTurret ? 0.42f : 0.36f;
    protected override string SpriteResourcePath => "Items/rocket_autoturret";
    protected override string EditorSpritePath => "Assets/Resources/Items/rocket_autoturret.png";

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
        warBaseDefenseTurret = PlayerDeployableRuntime.IsWarBaseRocketAutoTurretData(data);
        neutralRiderRocketTurret = PlayerDeployableRuntime.IsNeutralRiderRocketAutoTurretData(data);
        cachedTargetView = null;
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

        PhotonView targetView = ResolveTargetView();
        if (targetView == null)
            return;

        Vector2 direction = targetView.transform.position - transform.position;
        if (direction.sqrMagnitude < 0.001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, 0f, angle), RotationSpeedDegreesPerSecond * Time.deltaTime);

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
        FireRocket(muzzleDirection, targetView);
    }

    PhotonView ResolveTargetView()
    {
        if (Time.time >= nextTargetRefreshTime || !IsCachedTargetValid(cachedTargetView))
        {
            nextTargetRefreshTime = Time.time + TargetRefreshInterval;
            cachedTargetView = FindNearestTargetView();
        }

        return cachedTargetView;
    }

    bool IsCachedTargetValid(PhotonView targetView)
    {
        if (targetView == null)
            return false;

        PlayerHealth candidate = targetView.GetComponent<PlayerHealth>();
        if (!IsValidTarget(candidate, ResolveOwnerActorNumber()))
            return false;

        return Vector2.Distance(transform.position, targetView.transform.position) <= TargetRange;
    }

    PhotonView FindNearestTargetView()
    {
        int ownerActorNumber = ResolveOwnerActorNumber();
        PlayerHealth[] healths = RuntimeSceneQueryCache.GetPlayers();
        PhotonView best = null;
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
            best = candidate.photonView;
        }

        return best;
    }

    bool IsValidTarget(PlayerHealth candidate, int ownerActorNumber)
    {
        if (candidate == null || candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.photonView == null)
            return false;

        if (candidate.photonView.ViewID == ownerShipViewId || candidate.GetComponent<LureBeaconDecoy>() != null)
            return false;

        if (warBaseDefenseTurret || neutralRiderRocketTurret)
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
            (marker == PlayerDeployableRuntime.RocketAutoTurretMarker || marker == PlayerDeployableRuntime.NeutralRiderRocketAutoTurretMarker))
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

    void FireRocket(Vector2 direction, PhotonView targetView)
    {
        Vector3 spawnPosition = transform.position + (Vector3)(direction.normalized * MuzzleForwardOffset);
        Vector2 targetPoint = targetView != null ? (Vector2)targetView.transform.position : (Vector2)spawnPosition + direction.normalized * TargetRange;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        object[] data = Bullet.AppendWeaponMetadata(
            new object[]
            {
                ownerShipViewId,
                Damage,
                RocketScale,
                RocketColor.r,
                RocketColor.g,
                RocketColor.b,
                RocketColor.a,
                TargetRange,
                Damage,
                Damage,
                false,
                AreaDamageRadius,
                RocketEffectId,
                Mathf.Clamp(TargetRange / RocketSpeed, 1f, 8f),
                false,
                targetPoint.x,
                targetPoint.y,
                1f,
                "rocket",
                0,
                0f,
                RocketSpeed,
                false
            },
            WeaponDamageType.Explosive,
            WeaponDeliveryMethod.DeployableTurret,
            WeaponDeliveryFlags.Autonomous | WeaponDeliveryFlags.AreaDamage);

        GameObject rocket = ProjectileSpawner.SpawnNetworkBullet(
            "Bullet",
            spawnPosition,
            Quaternion.Euler(0f, 0f, angle),
            data,
            ownerShipViewId,
            direction.normalized * RocketSpeed,
            true);

        if (rocket == null)
            return;

        IgnoreProjectileCollisions(rocket);
        photonView.RPC(nameof(PlayRocketShotRpc), RpcTarget.All);
    }

    void IgnoreProjectileCollisions(GameObject projectile)
    {
        if (projectile == null)
            return;

        ProjectileSpawner.IgnoreCollisions(projectile, GetOwnColliders());
        ProjectileSpawner.IgnoreCollisions(projectile, GetOwnerColliders());
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
    void PlayRocketShotRpc()
    {
        AudioManager.Instance.PlayRocketLaunchAt(transform.position);
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
