using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class FiringFriendController : MonoBehaviourPun
{
    const float FollowRightOffset = -0.72f;
    const float FollowBackOffset = -0.05f;
    const float VisualTargetWorldSize = GameVisualTheme.PlayerTargetSize * 0.22f;
    const float ScanInterval = 0.12f;
    const float FireInterval = 1f / 3f;
    const int ShotsPerBurst = 9;
    const float ReloadDuration = 3f;
    const float MaxConfiguredRange = 7f;
    const float BulletSpeed = 12.5f;
    const int BulletDamage = 6;
    const float BulletScale = 0.48f;
    const float MuzzleOffset = 0.34f;
    const float EquipmentRefreshInterval = 0.5f;
    const string BulletEffectId = "pirate_fighter";
    static readonly Color BulletColor = new Color(0.2f, 0.86f, 1f, 1f);

    PlayerHealth health;
    SpriteRenderer visualRenderer;
    SpriteRenderer cachedOwnerRenderer;
    Collider2D cachedOwnerCollider;
    Collider2D[] cachedOwnerColliders;
    Camera cachedCamera;
    float cachedOwnerLength = -1f;
    float nextScanTime;
    float nextShotTime;
    float reloadUntil;
    int shotsInBurst;
    bool visualActive;
    bool forcedForNeutralRider;
    bool cachedEquipped;
    float nextEquipmentRefreshTime;
    Vector2 recentAimDirection = Vector2.up;
    float recentAimUntil;

    void Start()
    {
        health = GetComponent<PlayerHealth>();
        CacheOwnerShape();
        EnsureVisual();
    }

    void Update()
    {
        if (!CanFiringFriendRun())
        {
            SetVisualActive(false);
            ResetCombatState();
            return;
        }

        bool equipped = IsFiringFriendEquipped();
        SetVisualActive(equipped);
        if (!equipped)
        {
            ResetCombatState();
            return;
        }

        UpdateVisualTransform();

        if (!photonView.IsMine || !IsGameStarted())
            return;

        if (Time.time < reloadUntil)
            return;

        if (shotsInBurst >= ShotsPerBurst)
        {
            BeginReload();
            return;
        }

        if (Time.time < nextShotTime || Time.time < nextScanTime)
            return;

        nextScanTime = Time.time + ScanInterval;
        float targetRange = ResolveEffectiveTargetRange();
        Transform target = FindNearestTarget(targetRange);
        if (target == null)
            return;

        Vector2 origin = ResolveMuzzleOrigin();
        Vector2 direction = (Vector2)target.position - origin;
        if (direction.sqrMagnitude <= 0.001f)
            return;

        direction.Normalize();
        FireSingleShot(direction, targetRange);
        recentAimDirection = direction;
        recentAimUntil = Time.time + 0.55f;
        shotsInBurst++;

        if (shotsInBurst >= ShotsPerBurst)
        {
            BeginReload();
        }
        else
        {
            nextShotTime = Time.time + FireInterval;
        }
    }

    public void DeactivateForShipLoss()
    {
        SetVisualActive(false);
        ResetCombatState();
        enabled = false;
    }

    public void SetForcedForNeutralRider(bool forced)
    {
        forcedForNeutralRider = forced;
        if (forced)
            enabled = true;
    }

    bool CanFiringFriendRun()
    {
        if (health == null)
            health = GetComponent<PlayerHealth>();

        return health != null &&
               health.isActiveAndEnabled &&
               !health.IsWreck &&
               !health.IsEvacuationAnimating &&
               !health.IsAstronautControlled;
    }

    bool IsFiringFriendEquipped()
    {
        if (forcedForNeutralRider)
            return true;

        if (Time.unscaledTime < nextEquipmentRefreshTime)
            return cachedEquipped;

        nextEquipmentRefreshTime = Time.unscaledTime + EquipmentRefreshInterval;
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipment = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        cachedEquipped = InventoryItemCatalog.HasEquippedItem(equipment, shipSkinIndex, InventoryItemCatalog.FiringFriendId);
        return cachedEquipped;
    }

    bool IsGameStarted()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started)
        {
            return started;
        }

        return PlayerShooting.gameStarted;
    }

    Transform FindNearestTarget(float maxRange)
    {
        Vector2 origin = visualRenderer != null ? (Vector2)visualRenderer.transform.position : (Vector2)transform.position;
        int ownerActorNumber = photonView != null ? photonView.OwnerActorNr : -1;
        PlayerHealth[] healths = RuntimeSceneQueryCache.GetPlayers();
        Transform best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (!IsValidTarget(candidate, ownerActorNumber))
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance > maxRange || distance >= bestDistance)
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

        if (photonView != null && candidate.photonView.ViewID == photonView.ViewID)
            return false;

        if (candidate.GetComponent<LureBeaconDecoy>() != null || candidate.GetComponent<PlayerDeployableBase>() != null)
            return false;

        EnemyBot enemyBot = candidate.GetComponent<EnemyBot>();
        return enemyBot != null ||
               candidate.IsBotControlled ||
               (ownerActorNumber > 0 && candidate.photonView.OwnerActorNr != ownerActorNumber);
    }

    void FireSingleShot(Vector2 direction, float targetRange)
    {
        int ownerId = photonView != null ? photonView.ViewID : 0;
        if (ownerId <= 0)
            return;

        Vector2 muzzle = ResolveMuzzleOrigin() + direction * MuzzleOffset;
        float rangeMultiplier = targetRange / ResolveOwnerLength();
        float flightTime = Mathf.Clamp(targetRange / BulletSpeed + 0.25f, 0.45f, 1.25f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        object[] data = Bullet.AppendWeaponMetadata(
            new object[]
            {
                ownerId,
                BulletDamage,
                BulletScale,
                BulletColor.r,
                BulletColor.g,
                BulletColor.b,
                BulletColor.a,
                rangeMultiplier,
                BulletDamage,
                BulletDamage,
                false,
                0f,
                BulletEffectId,
                flightTime
            },
            WeaponDamageType.Laser,
            WeaponDeliveryMethod.CompanionDrone,
            WeaponDeliveryFlags.Autonomous | WeaponDeliveryFlags.MultiStream);

        GameObject bullet = ProjectileSpawner.SpawnNetworkBullet(
            "Bullet",
            new Vector3(muzzle.x, muzzle.y, 0f),
            Quaternion.Euler(0f, 0f, angle),
            data,
            ownerId,
            direction * BulletSpeed,
            true);

        if (bullet == null)
            return;

        IgnoreOwnerCollisions(bullet);

        if (photonView != null)
            photonView.RPC(nameof(PlayFiringFriendShotRpc), RpcTarget.All, muzzle.x, muzzle.y, transform.position.z, direction.x, direction.y);
    }

    void BeginReload()
    {
        shotsInBurst = 0;
        reloadUntil = Time.time + ReloadDuration;
        nextShotTime = reloadUntil;
    }

    void ResetCombatState()
    {
        shotsInBurst = 0;
        nextShotTime = 0f;
        reloadUntil = 0f;
        nextScanTime = 0f;
    }

    float ResolveEffectiveTargetRange()
    {
        float range = MaxConfiguredRange;
        Camera mainCamera = ResolveCamera();
        if (mainCamera != null && mainCamera.orthographic)
        {
            float halfVertical = mainCamera.orthographicSize;
            float halfHorizontal = halfVertical * mainCamera.aspect;
            range = Mathf.Min(range, Mathf.Max(0.75f, Mathf.Min(halfVertical, halfHorizontal)));
        }

        return Mathf.Max(0.75f, range);
    }

    Camera ResolveCamera()
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            return cachedCamera;

        cachedCamera = Camera.main;
        return cachedCamera;
    }

    float ResolveOwnerLength()
    {
        if (cachedOwnerLength > 0f)
            return cachedOwnerLength;

        CacheOwnerShape();
        return cachedOwnerLength > 0f ? cachedOwnerLength : 1f;
    }

    void CacheOwnerShape()
    {
        cachedOwnerColliders = GetComponentsInChildren<Collider2D>(true);
        cachedOwnerRenderer = GetComponentInChildren<SpriteRenderer>();
        cachedOwnerCollider = GetComponentInChildren<Collider2D>();

        if (cachedOwnerRenderer != null)
        {
            cachedOwnerLength = Mathf.Max(0.25f, Mathf.Max(cachedOwnerRenderer.bounds.size.x, cachedOwnerRenderer.bounds.size.y));
            return;
        }

        if (cachedOwnerCollider != null)
        {
            cachedOwnerLength = Mathf.Max(0.25f, Mathf.Max(cachedOwnerCollider.bounds.size.x, cachedOwnerCollider.bounds.size.y));
            return;
        }

        cachedOwnerLength = 1f;
    }

    Vector2 ResolveMuzzleOrigin()
    {
        return visualRenderer != null ? (Vector2)visualRenderer.transform.position : (Vector2)transform.position;
    }

    void IgnoreOwnerCollisions(GameObject bullet)
    {
        if (bullet == null)
            return;

        if (cachedOwnerColliders == null || cachedOwnerColliders.Length == 0)
            CacheOwnerShape();

        ProjectileSpawner.IgnoreCollisions(bullet, cachedOwnerColliders);
    }

    void EnsureVisual()
    {
        if (visualRenderer != null)
            return;

        GameObject visual = new GameObject("FiringFriendVisual");
        visual.transform.SetParent(transform, false);
        visualRenderer = visual.AddComponent<SpriteRenderer>();
        visualRenderer.sprite = RuntimeSpriteUtility.LoadSprite("firing_friend_up_resource", "Assets/firing_friend_up.png");
        visualRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        visualRenderer.sortingOrder = 49;
        visualRenderer.color = Color.white;
        RuntimeSpriteUtility.FitRendererWorldSize(visualRenderer, VisualTargetWorldSize);
        visualActive = true;
    }

    void SetVisualActive(bool active)
    {
        EnsureVisual();
        if (visualRenderer != null)
            visualRenderer.enabled = active;
        visualActive = active;
    }

    void UpdateVisualTransform()
    {
        if (visualRenderer == null || !visualActive)
            return;

        RuntimeSpriteUtility.FitRendererWorldSize(visualRenderer, VisualTargetWorldSize);
        visualRenderer.transform.position = transform.position + transform.right * FollowRightOffset + transform.up * FollowBackOffset;
        Vector2 direction = Time.time < recentAimUntil && recentAimDirection.sqrMagnitude > 0.001f
            ? recentAimDirection.normalized
            : (Vector2)transform.up;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        visualRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    [PunRPC]
    void PlayFiringFriendShotRpc(float x, float y, float z, float directionX, float directionY)
    {
        Vector2 shotDirection = new Vector2(directionX, directionY);
        if (shotDirection.sqrMagnitude > 0.001f)
        {
            recentAimDirection = shotDirection.normalized;
            recentAimUntil = Time.time + 0.55f;
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAutoShootAt(new Vector3(x, y, z));
    }
}
