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
    const string RocketEffectId = "rocket";
    static readonly Color RocketColor = new Color(1f, 0.58f, 0.18f, 1f);

    float nextShotTime;
    bool warBaseDefenseTurret;

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
        warBaseDefenseTurret = PlayerDeployableRuntime.IsWarBaseRocketAutoTurretData(photonView != null ? photonView.InstantiationData : null);
        InitializeCommon();
    }

    void Update()
    {
        if (!initialized && PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();

        if (!initialized || destroyed || !PhotonNetwork.IsMasterClient)
            return;

        PhotonView targetView = FindNearestTargetView();
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

    PhotonView FindNearestTargetView()
    {
        int ownerActorNumber = ResolveOwnerActorNumber();
        PlayerHealth[] healths = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        PhotonView best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (candidate == null || candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.photonView == null)
                continue;

            if (candidate.photonView.ViewID == ownerShipViewId || candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            if (warBaseDefenseTurret)
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
            best = candidate.photonView;
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
            marker == PlayerDeployableRuntime.RocketAutoTurretMarker)
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

        GameObject rocket = PhotonNetwork.Instantiate(
            "Bullet",
            spawnPosition,
            Quaternion.Euler(0f, 0f, angle),
            0,
            data);

        if (rocket == null)
            return;

        Bullet bulletComponent = rocket.GetComponent<Bullet>();
        if (bulletComponent != null)
            bulletComponent.ownerViewID = ownerShipViewId;

        Rigidbody2D rocketBody = rocket.GetComponent<Rigidbody2D>();
        if (rocketBody != null)
            rocketBody.linearVelocity = direction.normalized * RocketSpeed;

        Collider2D rocketCollider = rocket.GetComponent<Collider2D>();
        IgnoreProjectileCollisions(rocketCollider);
        photonView.RPC(nameof(PlayRocketShotRpc), RpcTarget.All);
    }

    void IgnoreProjectileCollisions(Collider2D projectileCollider)
    {
        if (projectileCollider == null)
            return;

        Collider2D[] ownColliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] != null)
                Physics2D.IgnoreCollision(ownColliders[i], projectileCollider, true);
        }

        PhotonView ownerView = ownerShipViewId > 0 ? PhotonView.Find(ownerShipViewId) : null;
        if (ownerView == null)
            return;

        Collider2D[] ownerColliders = ownerView.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            if (ownerColliders[i] != null)
                Physics2D.IgnoreCollision(ownerColliders[i], projectileCollider, true);
        }
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
