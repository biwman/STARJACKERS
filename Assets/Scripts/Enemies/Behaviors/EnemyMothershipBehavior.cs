using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(EnemyBot))]
public class EnemyMothershipBehavior : EnemyBotBehaviorBase
{
    sealed class TurretRuntime
    {
        public Transform Root;
        public int Ammo;
        public float NextFireTime;
        public float ReloadFinishTime;
        public bool Reloading;
    }

    const float TurretFullTurnDuration = 4f;
    const float DamageSourceChaseDuration = 5f;
    const float ShieldRegenPerSecond = 2f;
    const float TurretPivotOffsetFromCenterFactor = 1f / 6f;
    const float TurretTargetSizeFactor = 0.285f;
    const float TurretMinimumTargetSize = 1.02f;

    static Sprite cachedTurretSprite;
    static Sprite cachedTurretWreckSprite;

    public static void PrewarmTurretAssets()
    {
        PrewarmSpriteTexture(LoadTurretSprite());
        PrewarmSpriteTexture(LoadTurretWreckSprite());
    }

    readonly TurretRuntime[] turrets = new TurretRuntime[6];
    readonly Vector2[] turretOffsetFactors =
    {
        new Vector2(0.28f, -0.01f),
        new Vector2(0.1f, 0.14f),
        new Vector2(0.11f, -0.13f),
        new Vector2(-0.16f, 0.23f),
        new Vector2(-0.25f, -0.01f),
        new Vector2(-0.16f, -0.23f)
    };

    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    SpriteRenderer mothershipRenderer;
    Vector2 orbitCenter;
    float orbitRadius;
    float orbitAngle;
    float orbitDirection = 1f;
    float nextTargetRefreshTime;
    float forcedDirectionUntil;
    float shieldRegenAccumulator;
    Transform currentTarget;
    Vector2 forcedMoveDirection;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        mothershipRenderer = owner.GetComponent<SpriteRenderer>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        orbitCenter = Vector2.zero;
        orbitRadius = Mathf.Max(7f, Mathf.Min(mapSize.x, mapSize.y) * (movement != null ? movement.OrbitRadiusFactor : 0.38f));
        int seed = view != null ? view.ViewID : 1;
        orbitAngle = Mathf.Abs(seed * 0.119f) % (Mathf.PI * 2f);
        orbitDirection = seed % 2 == 0 ? 1f : -1f;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                weapon.AmmoCount,
                weapon.ReloadDuration,
                RoomSettings.GetEnemyDamage(owner.Kind),
                weapon.BulletScaleMultiplier,
                weapon.BulletColor,
                weapon.MuzzleOffsetDistance,
                weapon.InfiniteAmmo,
                weapon.BulletSpeed,
                weapon.ShotSoundId,
                weapon.Range,
                string.Empty,
                10f,
                weapon.DamageType,
                weapon.DeliveryMethod,
                weapon.DeliveryFlags);
        }

        EnsureTurrets();
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        EnsureTurrets();
        RegenerateShield();

        if (Time.time >= nextTargetRefreshTime)
        {
            nextTargetRefreshTime = Time.time + Mathf.Max(0.15f, movement.TargetRefreshInterval);
            currentTarget = ResolveTarget();
        }

        Vector2 desiredDirection = ResolveMoveDirection();
        Vector2 desiredVelocity = desiredDirection * bot.EffectiveMoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.1f);
        RotateHullToward(desiredVelocity);
        TickTurrets();
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        Vector2 sourcePosition = rb != null ? rb.position - Vector2.right : (Vector2)transform.position - Vector2.right;
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView != null)
            sourcePosition = attackerView.transform.position;

        Vector2 fromShipToSource = sourcePosition - (Vector2)transform.position;
        if (fromShipToSource.sqrMagnitude < 0.001f)
            fromShipToSource = rb != null && rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : Vector2.right;

        forcedMoveDirection = fromShipToSource.normalized;
        forcedDirectionUntil = Time.time + DamageSourceChaseDuration;
    }

    public void ConvertTurretsToWreckVisuals()
    {
        Sprite wreckSprite = LoadTurretWreckSprite();
        if (wreckSprite == null)
            return;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child.name != "TurretVisual")
                continue;

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.sprite = wreckSprite;
        }
    }

    Vector2 ResolveMoveDirection()
    {
        if (Time.time < forcedDirectionUntil && forcedMoveDirection.sqrMagnitude > 0.001f)
            return forcedMoveDirection.normalized;

        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            if (toTarget.sqrMagnitude > 0.001f)
                return toTarget.normalized;
        }

        orbitAngle += orbitDirection * movement.OrbitAngularSpeed * Time.fixedDeltaTime;
        Vector2 fromCenter = rb.position - orbitCenter;
        if (fromCenter.sqrMagnitude < 0.01f)
            fromCenter = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle));

        Vector2 radial = fromCenter.normalized;
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-radial.y, radial.x)
            : new Vector2(radial.y, -radial.x);
        float radialError = orbitRadius - fromCenter.magnitude;
        Vector2 orbitVelocity = tangent + radial * Mathf.Clamp(radialError * 0.12f, -0.5f, 0.5f);
        return orbitVelocity.sqrMagnitude > 0.001f ? orbitVelocity.normalized : Vector2.right;
    }

    void RotateHullToward(Vector2 desiredVelocity)
    {
        if (desiredVelocity.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(desiredVelocity.y, desiredVelocity.x) * Mathf.Rad2Deg;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    Transform ResolveTarget()
    {
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, transform.position, movement.DisengageRadius, true))
            return currentTarget;

        return FindClosestVisibleHumanTarget(movement.DetectionRadius);
    }

    Transform FindClosestVisibleHumanTarget(float maxDistance)
    {
        return EnemyTargetingUtility.FindClosestTarget(transform.position, health, maxDistance, true);
    }

    bool IsValidVisibleTarget(PlayerHealth candidate, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(candidate != null ? candidate.transform : null, health, transform.position, maxDistance, true);
    }

    void RegenerateShield()
    {
        if (health == null || health.HasBrokenShield || health.CurrentShield >= health.MaxShield)
            return;

        shieldRegenAccumulator += ShieldRegenPerSecond * Time.fixedDeltaTime;
        if (shieldRegenAccumulator < 1f)
            return;

        float amount = Mathf.Floor(shieldRegenAccumulator);
        shieldRegenAccumulator -= amount;
        health.TryRestoreShieldAuthority(amount, true);
    }

    void EnsureTurrets()
    {
        Sprite turretSprite = LoadTurretSprite();
        Vector2 shipLocalSize = GetMothershipLocalSize();

        for (int i = 0; i < turrets.Length; i++)
        {
            if (turrets[i] == null)
                turrets[i] = new TurretRuntime { Ammo = weapon != null ? Mathf.Max(1, weapon.AmmoCount) : 10 };

            if (turrets[i].Root == null)
            {
                GameObject turretObject = new GameObject("MothershipTurret_" + i);
                turretObject.transform.SetParent(transform, false);
                turrets[i].Root = turretObject.transform;
            }

            EnsureTurretVisual(turrets[i].Root, turretSprite);
            turrets[i].Root.localScale = Vector3.one;
            turrets[i].Root.localPosition = new Vector3(
                turretOffsetFactors[i].x * shipLocalSize.x,
                turretOffsetFactors[i].y * shipLocalSize.y,
                0f);
            FitTurretVisual(turrets[i].Root);
        }
    }

    Vector2 GetMothershipLocalSize()
    {
        if (mothershipRenderer != null && mothershipRenderer.sprite != null)
            return mothershipRenderer.sprite.bounds.size;

        return new Vector2(7f, 3f);
    }

    void EnsureTurretVisual(Transform turretRoot, Sprite turretSprite)
    {
        if (turretRoot == null)
            return;

        SpriteRenderer rootRenderer = turretRoot.GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;

        Transform visual = turretRoot.Find("TurretVisual");
        if (visual == null)
        {
            GameObject visualObject = new GameObject("TurretVisual");
            visualObject.transform.SetParent(turretRoot, false);
            visual = visualObject.transform;
        }

        SpriteRenderer visualRenderer = visual.GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
            visualRenderer = visual.gameObject.AddComponent<SpriteRenderer>();

        visualRenderer.sprite = turretSprite;
        visualRenderer.color = Color.white;
        if (mothershipRenderer != null)
        {
            visualRenderer.sortingLayerID = mothershipRenderer.sortingLayerID;
            visualRenderer.sortingOrder = mothershipRenderer.sortingOrder + 1;
        }
    }

    void FitTurretVisual(Transform turretRoot)
    {
        Transform visual = turretRoot != null ? turretRoot.Find("TurretVisual") : null;
        SpriteRenderer renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (renderer == null || renderer.sprite == null)
            return;

        float largest = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        if (largest <= 0.001f)
            return;

        float targetSize = Mathf.Max(TurretMinimumTargetSize, GetMothershipLocalSize().x * TurretTargetSizeFactor);
        float scale = targetSize / largest;
        visual.localScale = new Vector3(scale, scale, 1f);
        float pivotOffset = renderer.sprite.bounds.size.x * scale * TurretPivotOffsetFromCenterFactor;
        visual.localPosition = new Vector3(-pivotOffset, 0f, 0f);
        visual.localRotation = Quaternion.identity;
    }

    void TickTurrets()
    {
        if (weapon == null || shooting == null)
            return;

        float turnSpeed = 360f / TurretFullTurnDuration;
        for (int i = 0; i < turrets.Length; i++)
        {
            TurretRuntime turret = turrets[i];
            if (turret == null || turret.Root == null)
                continue;

            UpdateTurretReload(turret);
            Transform target = FindClosestVisibleHumanTargetFrom(turret.Root.position, weapon.Range);
            if (target == null)
                continue;

            Vector2 toTarget = target.position - turret.Root.position;
            if (toTarget.sqrMagnitude <= 0.001f)
                continue;

            float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 180f;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
            turret.Root.rotation = Quaternion.RotateTowards(turret.Root.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);

            if (Time.time < turret.NextFireTime || turret.Reloading || turret.Ammo <= 0)
                continue;

            Vector2 muzzleDirection = -turret.Root.right;
            Vector3 muzzlePosition = turret.Root.position + (Vector3)(muzzleDirection * GetTurretMuzzleDistance(turret.Root));
            if (shooting.FireBotProjectileFromWorld(muzzleDirection, muzzlePosition))
            {
                turret.Ammo--;
                turret.NextFireTime = Time.time + Mathf.Max(0.05f, weapon.FireRate * ElectromagneticShockStatus.GetFireIntervalMultiplier(gameObject) * AtlasSuppressionStatus.GetFireIntervalMultiplier(gameObject) * RoomSettings.GetEnemyAttackCooldownMultiplier());
                if (turret.Ammo <= 0)
                {
                    turret.Reloading = true;
                    turret.ReloadFinishTime = Time.time + Mathf.Max(0f, weapon.ReloadDuration * AtlasSuppressionStatus.GetReloadMultiplier(gameObject));
                }
            }
        }
    }

    float GetTurretMuzzleDistance(Transform turretRoot)
    {
        Transform visual = turretRoot != null ? turretRoot.Find("TurretVisual") : null;
        SpriteRenderer renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (renderer == null || renderer.sprite == null)
            return Mathf.Max(0.18f, weapon != null ? weapon.MuzzleOffsetDistance : 0.18f);

        float visualWidth = renderer.sprite.bounds.size.x * Mathf.Abs(visual.lossyScale.x);
        return (visualWidth * (0.5f + TurretPivotOffsetFromCenterFactor)) + Mathf.Max(0.08f, weapon != null ? weapon.MuzzleOffsetDistance : 0.18f);
    }

    void UpdateTurretReload(TurretRuntime turret)
    {
        if (!turret.Reloading || weapon == null || Time.time < turret.ReloadFinishTime)
            return;

        turret.Reloading = false;
        turret.Ammo = Mathf.Max(1, weapon.AmmoCount);
    }

    Transform FindClosestVisibleHumanTargetFrom(Vector3 origin, float maxDistance)
    {
        return EnemyTargetingUtility.FindClosestTarget(origin, health, maxDistance, true);
    }

    static Sprite LoadTurretSprite()
    {
        if (cachedTurretSprite != null)
            return cachedTurretSprite;

        cachedTurretSprite = Resources.Load<Sprite>("wieza_mother_ship_resource");
        if (cachedTurretSprite != null)
            return cachedTurretSprite;

#if UNITY_EDITOR
        cachedTurretSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/wieza_mother_ship.png");
        if (cachedTurretSprite != null)
            return cachedTurretSprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/wieza_mother_ship.png");
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                cachedTurretSprite = sprite;
                return cachedTurretSprite;
            }
        }
#endif

        return null;
    }

    static Sprite LoadTurretWreckSprite()
    {
        if (cachedTurretWreckSprite != null)
            return cachedTurretWreckSprite;

        cachedTurretWreckSprite = Resources.Load<Sprite>("wieza_mother_ship_wrak_resource");
        if (cachedTurretWreckSprite != null)
            return cachedTurretWreckSprite;

#if UNITY_EDITOR
        cachedTurretWreckSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/wieza_mother_ship_wrak.png");
        if (cachedTurretWreckSprite != null)
            return cachedTurretWreckSprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/wieza_mother_ship_wrak.png");
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                cachedTurretWreckSprite = sprite;
                return cachedTurretWreckSprite;
            }
        }
#endif

        return null;
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }
}

