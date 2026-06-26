using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public partial class PlayerShooting
{
    void ShowAimMarker(WeaponAttackProfile profile, Vector2 direction, Vector2? explicitTargetPoint = null, Color? markerColorOverride = null)
    {
        if (profile == null)
            return;

        if (aimMarker == null)
            aimMarker = AimMarkerVfx.EnsureFor(gameObject);

        if (aimMarker == null)
            return;

        Vector2 resolvedDirection = ResolveSafeAimDirection(direction);
        float range = GetComplexRangeWorld(profile);
        Color markerColor = markerColorOverride ?? profile.MarkerColor;
        if (IsArcWeaponProfile(profile))
        {
            Vector3 landingPoint = explicitTargetPoint.HasValue
                ? (Vector3)explicitTargetPoint.Value
                : transform.position + (Vector3)(resolvedDirection * range);
            aimMarker.ShowArc(transform.position, landingPoint, markerColor, ResolveArcHeight(range));
            return;
        }

        aimMarker.ShowLine(transform.position, resolvedDirection, range, markerColor);
    }

    void HideAimMarker()
    {
        if (aimMarker != null)
            aimMarker.Hide();
    }

    float GetComplexRangeWorld(WeaponAttackProfile profile)
    {
        float multiplier = profile != null ? Mathf.Max(0.1f, profile.RangeMultiplier) : DefaultBulletRangeMultiplier;
        return GetOwnerLengthForRange() * multiplier;
    }

    float GetOwnerLengthForRange()
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            return Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y);

        Collider2D collider2D = GetComponentInChildren<Collider2D>();
        if (collider2D != null)
            return Mathf.Max(collider2D.bounds.size.x, collider2D.bounds.size.y);

        return 1f;
    }

    bool SpawnComplexBullet(WeaponAttackProfile profile, Vector2 direction, Vector3 spawnPos, int ownerId, Vector2 explicitTargetPoint, int homingTargetViewId = 0, bool pilotBreachProjectile = false)
    {
        if (bulletPrefab == null || profile == null)
            return false;

        float projectileSpeedMultiplier = GetProjectileSpeedMultiplier();
        float projectileSpeed = Mathf.Max(0.1f, profile.ProjectileSpeed * projectileSpeedMultiplier);
        float clampedFlightTime = Mathf.Clamp(profile.FlightTime / Mathf.Max(0.1f, projectileSpeedMultiplier), 0.2f, 30f);
        float range = GetComplexRangeWorld(profile);
        bool isArcProjectile = IsArcWeaponProfile(profile);
        bool isRocketProjectile = IsRocketWeaponProfile(profile);
        Vector2 targetPoint = isArcProjectile
            ? explicitTargetPoint
            : (Vector2)spawnPos + (direction.normalized * range);
        float arcHeight = ResolveArcHeight(range);

        object[] data = Bullet.AppendWeaponMetadata(
            new object[]
            {
                ownerId,
                Mathf.Max(profile.HpDamage, profile.ShieldDamage),
                profile.ProjectileSize,
                profile.ProjectileColor.r,
                profile.ProjectileColor.g,
                profile.ProjectileColor.b,
                profile.ProjectileColor.a,
                profile.RangeMultiplier,
                profile.ShieldDamage,
                profile.HpDamage,
                profile.Pierces,
                profile.AreaDamageRadius,
                profile.HitEffectId ?? string.Empty,
                clampedFlightTime,
                isArcProjectile,
                targetPoint.x,
                targetPoint.y,
                arcHeight,
                isRocketProjectile ? "rocket" : string.Empty,
                isRocketProjectile ? homingTargetViewId : 0,
                isRocketProjectile ? RocketHomingTurnRate : 0f,
                isRocketProjectile ? projectileSpeed : 0f,
                isRocketProjectile,
                pilotBreachProjectile ? PilotBreachProjectileMarker : string.Empty
            },
            profile.DamageType,
            profile.DeliveryMethod,
            profile.DeliveryFlags);

        Collider2D playerCollider = GetComponent<Collider2D>();
        GameObject bullet = ProjectileSpawner.SpawnNetworkBullet(
            bulletPrefab,
            spawnPos,
            Quaternion.identity,
            data,
            ownerId,
            direction.normalized * projectileSpeed,
            !isArcProjectile,
            playerCollider);

        if (bullet == null)
            return false;

        return true;
    }

    bool TryStartPulseDisruptorWave(WeaponAttackProfile profile, int ownerId)
    {
        if (profile == null || ownerId <= 0 || photonView == null)
            return false;

        Vector2 center = transform.position;
        float radius = Mathf.Max(0.5f, profile.AreaDamageRadius);
        float duration = Mathf.Clamp(profile.FlightTime, 0.2f, 2.5f);
        photonView.RPC(nameof(StartPulseDisruptorWaveRpc), RpcTarget.All, center.x, center.y, radius, duration);
        photonView.RPC(nameof(PlayShotSfx), RpcTarget.All, profile.ShotSoundId ?? string.Empty);
        StartCoroutine(PulseDisruptorWaveDamageRoutine(profile, center, radius, duration, ownerId));
        return true;
    }

    IEnumerator PulseDisruptorWaveDamageRoutine(WeaponAttackProfile profile, Vector2 center, float radius, float duration, int ownerId)
    {
        HashSet<int> damagedViews = new HashSet<int>();
        WaitForSeconds wait = new WaitForSeconds(PulseDisruptorWaveTickInterval);
        float startedAt = Time.time;
        float endAt = startedAt + Mathf.Max(0.05f, duration);

        while (Time.time < endAt)
        {
            float t = Mathf.Clamp01((Time.time - startedAt) / Mathf.Max(0.001f, duration));
            ApplyPulseDisruptorWaveDamage(profile, center, Mathf.Lerp(0.2f, radius, t), ownerId, damagedViews);
            yield return wait;
        }

        ApplyPulseDisruptorWaveDamage(profile, center, radius, ownerId, damagedViews);
    }

    void ApplyPulseDisruptorWaveDamage(WeaponAttackProfile profile, Vector2 center, float currentRadius, int ownerId, HashSet<int> damagedViews)
    {
        if (profile == null || damagedViews == null)
            return;

        int hitCount = Physics2D.OverlapCircle(center, Mathf.Max(0.05f, currentRadius), CreatePhysicsQueryFilter(), PulseDisruptorHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = PulseDisruptorHits[i];
            if (hit == null)
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable.photonView != null)
            {
                if (damagedViews.Add(deployable.photonView.ViewID))
                {
                    Vector2 point = deployable.transform.position;
                    deployable.photonView.RPC(
                        nameof(PlayerDeployableBase.TakeDeployableShieldOnlyDamageWithContextAt),
                        RpcTarget.MasterClient,
                        profile.ShieldDamage,
                        ownerId,
                        point.x,
                        point.y,
                        (int)profile.DamageType,
                        (int)profile.DeliveryMethod,
                        (int)profile.DeliveryFlags,
                        string.Empty);
                    AddSuperChargeForDamage();
                }

                continue;
            }

            LureBeaconDecoy beacon = hit.GetComponentInParent<LureBeaconDecoy>();
            if (beacon != null && beacon.photonView != null)
            {
                if (damagedViews.Add(beacon.photonView.ViewID))
                {
                    Vector2 point = beacon.transform.position;
                    beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconShieldOnlyDamageAt), RpcTarget.MasterClient, profile.ShieldDamage, ownerId, point.x, point.y);
                }

                continue;
            }

            PlayerHealth hp = hit.GetComponentInParent<PlayerHealth>();
            if (hp == null || hp.photonView == null || hp.IsWreck || hp.photonView.ViewID == ownerId)
                continue;

            if (damagedViews.Add(hp.photonView.ViewID))
            {
                Vector2 point = hp.transform.position;
                hp.photonView.RPC(
                    nameof(PlayerHealth.TakeShieldOnlyDamageWithContextAt),
                    RpcTarget.MasterClient,
                    profile.ShieldDamage,
                    ownerId,
                    point.x,
                    point.y,
                    (int)profile.DamageType,
                    (int)profile.DeliveryMethod,
                    (int)profile.DeliveryFlags,
                    string.Empty);
                AddSuperChargeForDamage();
            }
        }
    }

    [PunRPC]
    void StartPulseDisruptorWaveRpc(float x, float y, float radius, float duration)
    {
        PulseDisruptorWaveVfx.Spawn(new Vector3(x, y, transform.position.z - 0.05f), radius, duration);
    }

    bool TryStartAstroCutterBeam(WeaponAttackProfile profile, Vector2 direction, int ownerId)
    {
        if (profile == null || ownerId <= 0 || photonView == null)
            return false;

        direction = ResolveSafeAimDirection(direction);
        float range = GetComplexRangeWorld(profile);
        float duration = Mathf.Clamp(profile.FlightTime, 0.2f, 6f);
        bool isSuperBeam = IsAstroCutterSuperProfile(profile);

        if (astroCutterBeamRoutine != null)
            StopCoroutine(astroCutterBeamRoutine);

        photonView.RPC(nameof(StartAstroCutterBeamRpc), RpcTarget.All, ownerId, direction.x, direction.y, range, duration, isSuperBeam);
        astroCutterBeamRoutine = StartCoroutine(AstroCutterDamageRoutine(profile, direction, range, duration, ownerId, isSuperBeam));
        return true;
    }

    IEnumerator AstroCutterDamageRoutine(WeaponAttackProfile profile, Vector2 direction, float range, float duration, int ownerId, bool isSuperBeam)
    {
        if (profile == null)
            yield break;

        float endTime = Time.time + Mathf.Max(0.1f, duration);
        WaitForSeconds wait = new WaitForSeconds(AstroCutterTickInterval);
        while (Time.time < endTime)
        {
            ApplyAstroCutterDamageTick(profile, direction, range, ownerId, isSuperBeam);
            yield return wait;
        }

        astroCutterBeamRoutine = null;
    }

    void ApplyAstroCutterDamageTick(WeaponAttackProfile profile, Vector2 direction, float range, int ownerId, bool isSuperBeam)
    {
        Vector2 safeDirection = ResolveSafeAimDirection(direction);
        Vector2 start = (Vector2)transform.position + (Vector2)transform.up * Mathf.Max(0.05f, muzzleOffsetDistance);
        float beamRadius = isSuperBeam ? AstroCutterSuperBeamRadius : AstroCutterBeamRadius;
        int hitCount = Physics2D.CircleCast(start, beamRadius, safeDirection, CreatePhysicsQueryFilter(), AstroCutterHits, Mathf.Max(0.2f, range));
        float clippedRange = AstroCutterBeamBlocker.ResolveClippedRange(AstroCutterHits, hitCount, transform, ownerId, range);
        astroCutterDamagedViews.Clear();
        astroCutterDamagedObstacles.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hitInfo = AstroCutterHits[i];
            Collider2D hit = hitInfo.collider;
            if (hit == null)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            if (hitInfo.distance > clippedRange + 0.035f)
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable.photonView != null)
            {
                if (astroCutterDamagedViews.Add(deployable.photonView.ViewID))
                {
                    Vector2 point = hitInfo.point.sqrMagnitude > 0.001f ? hitInfo.point : (Vector2)deployable.transform.position;
                    deployable.photonView.RPC(
                        nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                        RpcTarget.MasterClient,
                        profile.ShieldDamage,
                        profile.HpDamage,
                        ownerId,
                        point.x,
                        point.y,
                        (int)profile.DamageType,
                        (int)profile.DeliveryMethod,
                        (int)profile.DeliveryFlags,
                        string.Empty);
                    AddSuperChargeForDamage();
                }

                continue;
            }

            PlayerHealth hp = hit.GetComponentInParent<PlayerHealth>();
            if (hp != null && hp.photonView != null && hp.GetComponent<LureBeaconDecoy>() == null && !hp.IsWreck && hp.photonView.ViewID != ownerId)
            {
                if (astroCutterDamagedViews.Add(hp.photonView.ViewID))
                {
                    Vector2 point = hitInfo.point.sqrMagnitude > 0.001f ? hitInfo.point : (Vector2)hp.transform.position;
                    hp.photonView.RPC(
                        nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                        RpcTarget.MasterClient,
                        profile.ShieldDamage,
                        profile.HpDamage,
                        ownerId,
                        point.x,
                        point.y,
                        (int)profile.DamageType,
                        (int)profile.DeliveryMethod,
                        (int)profile.DeliveryFlags,
                        string.Empty);
                    AddSuperChargeForDamage();
                }

                continue;
            }

            ObstacleChunk obstacleChunk = hit.GetComponentInParent<ObstacleChunk>();
            if (obstacleChunk != null &&
                !string.IsNullOrWhiteSpace(obstacleChunk.StableId) &&
                RoomSettings.AreObstaclesDestructible() &&
                astroCutterDamagedObstacles.Add(obstacleChunk.StableId))
            {
                int obstacleMultiplier = isSuperBeam ? AstroCutterSuperObstacleDamageMultiplier : AstroCutterObstacleDamageMultiplier;
                int obstacleDamage = Mathf.Max(1, profile.HpDamage * obstacleMultiplier);
                SpaceObjectMotionSync.RequestObstacleDamage(obstacleChunk.StableId, GetPilotAdjustedObstacleDamage(obstacleDamage));
            }
        }
    }

    int GetPilotAdjustedObstacleDamage(int baseDamage)
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        if (PilotCatalog.IsSelectedPilot(owner, PilotCatalog.SirNowitzkyId))
            return Mathf.CeilToInt(baseDamage * 1.5f);

        return baseDamage;
    }

    [PunRPC]
    void StartAstroCutterBeamRpc(int sourceViewId, float directionX, float directionY, float range, float duration, bool useFullWidth)
    {
        AstroCutterBeamVfx.StartBeam(sourceViewId, new Vector2(directionX, directionY), range, duration, useFullWidth);
    }

    public void AddSuperChargeForDamage()
    {
        if (!IsComplexShootingActive)
            return;

        superCharge = Mathf.Clamp01(superCharge + (SuperChargeOnComplexHit * GetSuperChargeDamageGainMultiplier()));
    }

    float GetSuperChargeGainMultiplier()
    {
        WeaponAttackProfile profile = activeComplexWeaponProfile ?? GetActiveComplexWeaponState()?.Profile;
        if (IsDoubleRocketLauncherProfile(profile))
            return DoubleRocketSuperChargeMultiplier;

        return IsAstroCutterProfile(profile) ? 0.5f : 1f;
    }

    float GetSuperChargeDamageGainMultiplier()
    {
        WeaponAttackProfile profile = activeComplexWeaponProfile ?? GetActiveComplexWeaponState()?.Profile;
        if (IsDoubleRocketLauncherProfile(profile))
            return DoubleRocketSuperChargeMultiplier;

        return IsAstroCutterProfile(profile) ? 0.25f : 1f;
    }

    Vector2 ResolveManualAimDirection()
    {
        simpleAutoAimTargetViewId = 0;
        simpleAutoAimHasTargetPoint = false;
        simpleAutoAimTargetPoint = default;
        Vector2 rawDirection = shootJoystick != null ? shootJoystick.inputVector : Vector2.zero;
        if (rawDirection.magnitude >= ManualAimThreshold)
            return rawDirection.normalized;

        return FindAutoAimDirection(out simpleAutoAimTargetViewId, out simpleAutoAimTargetPoint, out simpleAutoAimHasTargetPoint);
    }

    Vector2 FindAutoAimDirection()
    {
        return FindAutoAimDirection(out _);
    }

    Vector2 FindAutoAimDirection(out int targetViewId)
    {
        return FindAutoAimDirection(out targetViewId, out _, out _);
    }

    Vector2 FindAutoAimDirection(out int targetViewId, out Vector2 targetPoint, out bool hasTargetPoint)
    {
        targetViewId = 0;
        targetPoint = default;
        hasTargetPoint = false;
        float range = GetSimpleAutoAimRange();
        if (TryResolveAutoAimTarget(range, false, activeSimpleWeaponProfile, out AutoAimTargetResult target))
        {
            targetViewId = target.ViewId;
            targetPoint = target.AimPoint;
            hasTargetPoint = true;
            return ResolveSafeAimDirection(target.AimPoint - (Vector2)transform.position);
        }

        return transform.up;
    }

    float GetSimpleAutoAimRange()
    {
        float configuredRange = GetOwnerLengthForRange() * Mathf.Max(0.1f, bulletRangeMultiplier);
        return Mathf.Max(AutoAimRange, configuredRange);
    }

    bool Shoot(Vector2 direction, int homingTargetViewId = 0, bool hasAutoAimTargetPoint = false, Vector2 autoAimTargetPoint = default(Vector2))
    {
        if (bulletPrefab == null)
        {
            Debug.LogError("bulletPrefab NULL");
            return false;
        }

        PhotonView playerView = GetComponent<PhotonView>();
        int ownerId = playerView != null ? playerView.ViewID : 0;
        bool spawned = false;
        WeaponAttackProfile profile = activeSimpleWeaponProfile;

        if (IsAstroCutterProfile(profile))
        {
            bool firedBeam = TryStartAstroCutterBeam(profile, direction.normalized, ownerId);
            if (firedBeam)
                NotifyShotFiredForUseCancel();
            return firedBeam;
        }

        if (IsGatlingGunProfile(profile))
        {
            if (complexBurstRoutine != null)
                StopCoroutine(complexBurstRoutine);

            Vector2 safeDirection = ResolveSafeAimDirection(direction);
            Vector2 targetPoint = (Vector2)transform.position + (safeDirection * GetComplexRangeWorld(profile));
            bool pilotBreachSalvo = TryConsumePilotBreachSalvo();
            complexBurstRoutine = StartCoroutine(FireComplexBurst(profile, safeDirection, targetPoint, 0, pilotBreachSalvo));
            NotifyShotFiredForUseCancel();
            return true;
        }

        bool pilotBreachProjectile = TryConsumePilotBreachSalvo();
        if (IsRocketWeaponProfile(profile))
        {
            int count = Mathf.Max(1, profile.ProjectileCount);
            for (int i = 0; i < count; i++)
            {
                Vector2 shotDirection = IsDoubleRocketLauncherProfile(profile)
                    ? ResolveDoubleRocketProjectileDirection(direction.normalized, i, count, homingTargetViewId)
                    : ResolveComplexProjectileDirection(direction.normalized, profile.SpreadAngle, i, count);
                Vector3 spawnPos = ResolveComplexProjectileSpawnPosition(profile, shotDirection, i, count);
                Vector2 targetPoint = (Vector2)spawnPos + (shotDirection.normalized * GetComplexRangeWorld(profile));
                spawned |= SpawnComplexBullet(profile, shotDirection.normalized, spawnPos, ownerId, targetPoint, homingTargetViewId, pilotBreachProjectile);
            }
        }
        else if (ShouldUseArcSimpleShot(profile))
        {
            Vector3 spawnPos = transform.position + (transform.up * muzzleOffsetDistance);
            float range = GetComplexRangeWorld(profile);
            Vector2 targetPoint = hasAutoAimTargetPoint
                ? ClampAutoAimPointToRange((Vector2)transform.position, autoAimTargetPoint, range)
                : (Vector2)transform.position + (direction.normalized * range);
            spawned |= SpawnComplexBullet(profile, direction.normalized, spawnPos, ownerId, targetPoint, 0, pilotBreachProjectile);
        }
        else if (ShouldUseSimpleSpreadShot(profile))
        {
            Vector3 spawnPos = transform.position + (transform.up * muzzleOffsetDistance);
            int count = Mathf.Max(1, profile.ProjectileCount);
            for (int i = 0; i < count; i++)
            {
                Vector2 shotDirection = ResolveComplexProjectileDirection(direction.normalized, profile.SpreadAngle, i, count);
                spawned |= SpawnBullet(shotDirection, spawnPos, ownerId, pilotBreachProjectile);
            }
        }
        else if (ShouldFireFromDualWingMuzzles())
        {
            spawned |= SpawnBullet(direction, GetWingMuzzlePosition(-1f), ownerId, pilotBreachProjectile);
            spawned |= SpawnBullet(direction, GetWingMuzzlePosition(1f), ownerId, pilotBreachProjectile);
        }
        else
        {
            Vector3 spawnPos = transform.position + (transform.up * muzzleOffsetDistance);
            spawned |= SpawnBullet(direction, spawnPos, ownerId, pilotBreachProjectile);
        }

        if (spawned)
        {
            photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);
            NotifyShotFiredForUseCancel();
        }

        return spawned;
    }

    void NotifyShotFiredForUseCancel()
    {
        if (!photonView.IsMine || IsEnemyBotShip() || IsNeutralRiderShip())
            return;

        CancelCloakDeviceForLocalOwner();
        CancelHackingDeviceForLocalOwner();

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.CancelCollectionForShot();
    }

    void CancelHackingDeviceForLocalOwner()
    {
        if (photonView == null || !photonView.IsMine)
            return;

        photonView.RPC(nameof(RequestCancelHackingDevice), RpcTarget.MasterClient);
    }

    void CancelCloakDeviceForLocalOwner()
    {
        if (photonView == null || !photonView.IsMine)
            return;

        HideInNebulaTarget nebulaTarget = GetComponent<HideInNebulaTarget>();
        if (nebulaTarget == null || !nebulaTarget.IsCloaked)
            return;

        photonView.RPC(nameof(CancelCloakDeviceRpc), RpcTarget.All);
    }

    bool ShouldUseSimpleSpreadShot(WeaponAttackProfile profile)
    {
        return profile != null &&
               string.Equals(profile.Id, WeaponAttackCatalog.TripleGunId, StringComparison.Ordinal);
    }

    bool ShouldUseArcSimpleShot(WeaponAttackProfile profile)
    {
        return IsArcWeaponProfile(profile);
    }

    bool IsArcWeaponProfile(WeaponAttackProfile profile)
    {
        return profile != null &&
               (profile.MarkerType == ComplexAttackMarkerType.Arc ||
                string.Equals(profile.Id, WeaponAttackCatalog.ArtilleryGunId, StringComparison.Ordinal));
    }

    bool IsArtillerySuperProfile(WeaponAttackProfile profile)
    {
        return profile != null &&
               string.Equals(profile.Id, WeaponAttackCatalog.ArtilleryGunId + "_super", StringComparison.Ordinal);
    }

    bool IsAstroCutterProfile(WeaponAttackProfile profile)
    {
        return profile != null &&
               !string.IsNullOrWhiteSpace(profile.Id) &&
               profile.Id.StartsWith(WeaponAttackCatalog.AstroCutterId, StringComparison.Ordinal);
    }

    bool IsAstroCutterSuperProfile(WeaponAttackProfile profile)
    {
        return profile != null &&
               string.Equals(profile.Id, WeaponAttackCatalog.AstroCutterId + "_super", StringComparison.Ordinal);
    }

    Vector2 ResolveArcTargetPointFromInput(WeaponAttackProfile profile, Vector2 rawInput)
    {
        float maxRange = GetComplexRangeWorld(profile);
        if (rawInput.sqrMagnitude <= 0.001f)
            return (Vector2)transform.position + ((Vector2)transform.up * maxRange);

        float distance = Mathf.Clamp01(rawInput.magnitude) * maxRange;
        return (Vector2)transform.position + (rawInput.normalized * distance);
    }

    float ResolveArcHeight(float range)
    {
        return Mathf.Max(1.2f, range * 0.22f);
    }

    public bool TryFireBot(Vector2 direction, int homingTargetViewId = 0)
    {
        if (!IsEnemyBotShip() && !IsNeutralRiderShip())
            return false;

        if (!photonView.IsMine || !IsGameStarted())
            return false;

        UpdateReload();

        if (Time.time < nextFireTime || direction.sqrMagnitude < 0.04f)
            return false;

        if (!infiniteAmmo && (isReloading || currentAmmo <= 0))
            return false;

        if (!Shoot(direction.normalized, homingTargetViewId))
            return false;

        if (!infiniteAmmo)
            ConsumeAmmo();
        nextFireTime = Time.time + fireRate * GetFireIntervalMultiplier();
        return true;
    }

    public bool TryFireBotAtPoint(Vector2 direction, Vector2 targetPoint, int homingTargetViewId = 0)
    {
        if (!IsEnemyBotShip() && !IsNeutralRiderShip())
            return false;

        if (!photonView.IsMine || !IsGameStarted())
            return false;

        UpdateReload();

        if (Time.time < nextFireTime || direction.sqrMagnitude < 0.04f)
            return false;

        if (!infiniteAmmo && (isReloading || currentAmmo <= 0))
            return false;

        if (!Shoot(direction.normalized, homingTargetViewId, true, targetPoint))
            return false;

        if (!infiniteAmmo)
            ConsumeAmmo();
        nextFireTime = Time.time + fireRate * GetFireIntervalMultiplier();
        return true;
    }

    public bool TryFireHackedRandom(Vector2 direction)
    {
        if (!photonView.IsMine || !IsGameStarted() || direction.sqrMagnitude < 0.04f)
            return false;

        if (IsEnemyBotShip() || IsNeutralRiderShip())
            return TryFireBot(direction);

        if (AreShipControlsBlocked())
            return false;

        ResetComplexPressState();
        HideAimMarker();
        Vector2 safeDirection = ResolveSafeAimDirection(direction);

        if (IsComplexShootingActive && HasMainGunSlotsForCurrentShip())
        {
            WeaponAttackProfile profile = activeComplexWeaponProfile ?? SyncComplexWeaponProfile();
            if (profile == null)
                return false;

            UpdateComplexAmmoReload();
            Vector2 targetPoint = (Vector2)transform.position + (safeDirection * GetComplexRangeWorld(profile));
            return TryFireComplexAttack(profile, safeDirection, targetPoint, true, false, 0);
        }

        SyncEquippedWeaponProfile();
        SyncAmmoSetting();
        UpdateReload();

        if (Time.time < nextFireTime || isReloading || currentAmmo <= 0)
            return false;

        if (!Shoot(safeDirection))
            return false;

        ConsumeAmmo();
        nextFireTime = Time.time + fireRate * GetFireIntervalMultiplier();
        return true;
    }

    public bool TryFireBotFromWorld(Vector2 direction, Vector3 spawnPosition, float cooldownOffset = 0f)
    {
        if (!IsEnemyBotShip() && !IsNeutralRiderShip())
            return false;

        if (!photonView.IsMine || !IsGameStarted())
            return false;

        UpdateReload();

        if (Time.time < nextFireTime || direction.sqrMagnitude < 0.04f)
            return false;

        if (!infiniteAmmo && (isReloading || currentAmmo <= 0))
            return false;

        int ownerId = photonView != null ? photonView.ViewID : 0;
        bool spawned = SpawnBullet(direction.normalized, spawnPosition, ownerId);
        if (!spawned)
            return false;

        photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);
        if (!infiniteAmmo)
            ConsumeAmmo();
        nextFireTime = Time.time + (fireRate * GetFireIntervalMultiplier()) + Mathf.Max(0f, cooldownOffset);
        return true;
    }

    public bool TryFireBotVolleyFromWorld(Vector2 direction, Vector3[] spawnPositions, float cooldownOffset = 0f)
    {
        if (!IsEnemyBotShip() && !IsNeutralRiderShip())
            return false;

        if (!photonView.IsMine || !IsGameStarted())
            return false;

        UpdateReload();

        if (Time.time < nextFireTime || direction.sqrMagnitude < 0.04f || spawnPositions == null || spawnPositions.Length == 0)
            return false;

        if (!infiniteAmmo && (isReloading || currentAmmo <= 0))
            return false;

        int ownerId = photonView != null ? photonView.ViewID : 0;
        bool spawned = false;
        Vector2 normalizedDirection = direction.normalized;
        for (int i = 0; i < spawnPositions.Length; i++)
            spawned |= SpawnBullet(normalizedDirection, spawnPositions[i], ownerId);

        if (!spawned)
            return false;

        photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);
        if (!infiniteAmmo)
            ConsumeAmmo();
        nextFireTime = Time.time + (fireRate * GetFireIntervalMultiplier()) + Mathf.Max(0f, cooldownOffset);
        return true;
    }

    public bool TryFireBotVolleyAtPointFromWorld(Vector3[] spawnPositions, Vector2 targetPoint, float cooldownOffset = 0f)
    {
        if (!IsEnemyBotShip() && !IsNeutralRiderShip())
            return false;

        if (!photonView.IsMine || !IsGameStarted())
            return false;

        UpdateReload();

        if (Time.time < nextFireTime || spawnPositions == null || spawnPositions.Length == 0)
            return false;

        if (!infiniteAmmo && (isReloading || currentAmmo <= 0))
            return false;

        int ownerId = photonView != null ? photonView.ViewID : 0;
        bool spawned = false;
        for (int i = 0; i < spawnPositions.Length; i++)
        {
            Vector2 shotDirection = targetPoint - (Vector2)spawnPositions[i];
            if (shotDirection.sqrMagnitude < 0.04f)
                shotDirection = transform.up;

            spawned |= SpawnBullet(shotDirection.normalized, spawnPositions[i], ownerId);
        }

        if (!spawned)
            return false;

        photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);
        if (!infiniteAmmo)
            ConsumeAmmo();
        nextFireTime = Time.time + (fireRate * GetFireIntervalMultiplier()) + Mathf.Max(0f, cooldownOffset);
        return true;
    }

    public bool FireBotProjectileFromWorld(Vector2 direction, Vector3 spawnPosition)
    {
        if (!IsEnemyBotShip() && !IsNeutralRiderShip())
            return false;

        if (!photonView.IsMine || !IsGameStarted() || direction.sqrMagnitude < 0.04f)
            return false;

        int ownerId = photonView != null ? photonView.ViewID : 0;
        bool spawned = SpawnBullet(direction.normalized, spawnPosition, ownerId);
        if (spawned)
            photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);

        return spawned;
    }
}
