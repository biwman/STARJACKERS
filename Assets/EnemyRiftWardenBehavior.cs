using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemyRiftWardenBehavior : EnemyBotBehaviorBase
{
    const float SecretAwakenRadius = 5.6f;
    const float DormantOrbitRadius = 2.8f;
    const float MovementBlend = 0.14f;
    const float RotationBlendDegrees = 140f;
    const float BeamWindupSeconds = 1.08f;
    const float BeamFlashSeconds = 0.28f;
    const float BeamCooldownMin = 5.2f;
    const float BeamCooldownMax = 7.4f;
    const float RippleWarningSeconds = 1.18f;
    const float RippleActiveSeconds = 0.52f;
    const float RippleRadius = 3.55f;
    const float RippleCooldownMin = 6.8f;
    const float RippleCooldownMax = 9.6f;
    const float RippleSlowDuration = 2.8f;
    const float RippleSlowMultiplier = 0.45f;
    const float BlinkCooldownMin = 7.4f;
    const float BlinkCooldownMax = 10.5f;
    const float BlinkMinDistance = 5.4f;
    const float LockdownRadius = 2.65f;
    const float LockdownDuration = 4.75f;
    const float LockdownCooldownMin = 6.4f;
    const float LockdownCooldownMax = 9.2f;
    const float LockdownTriggerRadius = 4.8f;
    const string BeamDamageSource = "rift_warden_beam";
    const string RippleDamageSource = "rift_warden_time_ripple";

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    bool awakened;
    bool attacking;
    int initialAlienSecretCount = -1;
    float orbitAngle;
    float nextTargetRefreshTime;
    float nextBeamTime;
    float nextRippleTime;
    float nextBlinkTime;
    float nextLockdownTime;
    Transform currentTarget;
    Vector2 currentMoveDirection = Vector2.up;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        orbitAngle = view != null ? Mathf.Abs(view.ViewID * 0.173f) % (Mathf.PI * 2f) : Random.Range(0f, Mathf.PI * 2f);
        initialAlienSecretCount = CountAlienSecrets();
        ScheduleAllAttacks(1.2f);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        if (initialAlienSecretCount < 0)
            initialAlienSecretCount = CountAlienSecrets();

        RefreshTargetIfNeeded();

        if (!awakened && ShouldAwaken())
            Awaken();

        Move();

        if (!awakened || attacking)
            return;

        TryStartLockdown();
        TryStartRipple();
        TryStartBeam();
        TryBlink();
    }

    void ScheduleAllAttacks(float initialDelay)
    {
        nextBeamTime = Time.time + initialDelay + Random.Range(1.2f, 2.4f);
        nextRippleTime = Time.time + initialDelay + Random.Range(2.4f, 4.2f);
        nextBlinkTime = Time.time + initialDelay + Random.Range(4f, 6.2f);
        nextLockdownTime = Time.time + initialDelay + Random.Range(1.8f, 3.6f);
    }

    bool ShouldAwaken()
    {
        if (CountAlienSecrets() < initialAlienSecretCount)
            return true;

        if (currentTarget != null && IsTargetNearAnyAlienSecret(currentTarget.position, SecretAwakenRadius))
            return true;

        return false;
    }

    void Awaken()
    {
        awakened = true;
        if (bot != null && bot.photonView != null)
        {
            Vector3 position = transform.position;
            bot.photonView.RPC(nameof(EnemyBot.PlayRiftWardenAwakenRpc), RpcTarget.All, position.x, position.y, position.z);
        }

        ScheduleAllAttacks(0.35f);
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.12f, movement.TargetRefreshInterval);
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, transform.position, movement.DisengageRadius, true))
            return;

        currentTarget = EnemyTargetingUtility.FindClosestTarget(transform.position, health, movement.DetectionRadius, true);
    }

    void Move()
    {
        Vector2 anchor = ResolveGuardAnchor();
        Vector2 desired;

        if (awakened && currentTarget != null)
            desired = ResolveCombatMoveDirection(anchor);
        else
            desired = ResolveDormantMoveDirection(anchor);

        if (desired.sqrMagnitude <= 0.001f)
            desired = currentMoveDirection.sqrMagnitude > 0.001f ? currentMoveDirection : Vector2.up;

        currentMoveDirection = desired.normalized;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, currentMoveDirection * bot.EffectiveMoveSpeed, MovementBlend);
        RotateToward(currentMoveDirection);
    }

    Vector2 ResolveCombatMoveDirection(Vector2 anchor)
    {
        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return ResolveDormantMoveDirection(anchor);

        Vector2 toward = toTarget / distance;
        Vector2 tangent = new Vector2(-toward.y, toward.x);
        if (view != null && view.ViewID % 2 == 0)
            tangent *= -1f;

        if (distance > movement.PreferredDistance)
            return (toward * 0.78f + tangent * 0.28f).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.76f + tangent * 0.52f).normalized;

        Vector2 anchorPull = anchor - rb.position;
        Vector2 anchorDirection = anchorPull.sqrMagnitude > 0.001f ? anchorPull.normalized : Vector2.zero;
        return (tangent * 0.82f + toward * 0.12f + anchorDirection * 0.22f).normalized;
    }

    Vector2 ResolveDormantMoveDirection(Vector2 anchor)
    {
        orbitAngle += movement.OrbitAngularSpeed * Time.fixedDeltaTime;
        Vector2 orbitOffset = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle)) * DormantOrbitRadius;
        Vector2 desiredPosition = anchor + orbitOffset;
        Vector2 toDesired = desiredPosition - rb.position;
        if (toDesired.sqrMagnitude > 0.04f)
            return toDesired.normalized;

        return new Vector2(-orbitOffset.y, orbitOffset.x).normalized;
    }

    void RotateToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, RotationBlendDegrees * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    void TryStartBeam()
    {
        if (Time.time < nextBeamTime || currentTarget == null)
            return;

        if (Vector2.Distance(transform.position, currentTarget.position) > movement.ShootDistance)
            return;

        Transform target = currentTarget;
        nextBeamTime = Time.time + Random.Range(BeamCooldownMin, BeamCooldownMax);
        StartCoroutine(BeamRoutine(target));
    }

    IEnumerator BeamRoutine(Transform target)
    {
        attacking = true;
        Vector2 start = transform.position;
        Vector2 end = target != null ? (Vector2)target.position : start + (Vector2)transform.up * movement.ShootDistance;
        if (bot != null && bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.PlayRiftWardenBeamRpc), RpcTarget.All, start.x, start.y, end.x, end.y, BeamWindupSeconds, false);

        yield return new WaitForSeconds(ScaleEnemyAttackWindup(BeamWindupSeconds));

        start = transform.position;
        end = target != null ? (Vector2)target.position : end;
        if (bot != null && bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.PlayRiftWardenBeamRpc), RpcTarget.All, start.x, start.y, end.x, end.y, BeamFlashSeconds, true);

        DamageTarget(target, RoomSettings.GetEnemyDamage(bot.Kind), WeaponDamageType.Ion, WeaponDeliveryMethod.Beam, WeaponDeliveryFlags.ShieldFocused, BeamDamageSource);
        attacking = false;
    }

    void TryStartRipple()
    {
        if (Time.time < nextRippleTime || currentTarget == null)
            return;

        Vector2 center = currentTarget.position;
        if (Vector2.Distance(transform.position, center) > movement.DetectionRadius + 2f)
            return;

        nextRippleTime = Time.time + Random.Range(RippleCooldownMin, RippleCooldownMax);
        StartCoroutine(RippleRoutine(center));
    }

    IEnumerator RippleRoutine(Vector2 center)
    {
        attacking = true;
        if (bot != null && bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.PlayRiftWardenRippleRpc), RpcTarget.All, center.x, center.y, RippleRadius, RippleWarningSeconds, RippleActiveSeconds);

        yield return new WaitForSeconds(ScaleEnemyAttackWindup(RippleWarningSeconds));

        ApplyRippleDamage(center);
        attacking = false;
    }

    void TryBlink()
    {
        if (Time.time < nextBlinkTime)
            return;

        Vector2 destination = ResolveBlinkDestination();
        if (Vector2.Distance(rb.position, destination) < BlinkMinDistance)
            return;

        Vector2 from = rb.position;
        rb.position = destination;
        transform.position = new Vector3(destination.x, destination.y, transform.position.z);
        rb.linearVelocity *= 0.25f;
        nextBlinkTime = Time.time + Random.Range(BlinkCooldownMin, BlinkCooldownMax);

        if (bot != null && bot.photonView != null)
            bot.photonView.RPC(nameof(EnemyBot.PlayRiftWardenBlinkRpc), RpcTarget.All, from.x, from.y, destination.x, destination.y);
    }

    void TryStartLockdown()
    {
        if (Time.time < nextLockdownTime)
            return;

        Treasure secret = FindLockdownSecret();
        if (secret == null || secret.photonView == null)
            return;

        nextLockdownTime = Time.time + Random.Range(LockdownCooldownMin, LockdownCooldownMax);
        Vector3 position = secret.transform.position;
        if (bot != null && bot.photonView != null)
        {
            bot.photonView.RPC(
                nameof(EnemyBot.PlayRiftWardenLockdownRpc),
                RpcTarget.All,
                secret.photonView.ViewID,
                position.x,
                position.y,
                LockdownRadius,
                LockdownDuration);
        }
    }

    void ApplyRippleDamage(Vector2 center)
    {
        int damage = Mathf.Max(1, Mathf.RoundToInt(RoomSettings.GetEnemyDamage(bot.Kind) * 0.45f));
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(center, RippleRadius, out Collider2D[] hits);
        HashSet<int> hitViews = new HashSet<int>();
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            PhotonView targetView = hit.GetComponentInParent<PhotonView>();
            if (targetView == null || !hitViews.Add(targetView.ViewID))
                continue;

            Transform target = targetView.transform;
            if (!MapInstanceService.IsSameInstance(transform.position, target.position))
                continue;

            PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
            if (targetHealth != null && (targetHealth.IsWreck || targetHealth == health))
                continue;

            if (targetHealth != null)
            {
                targetView.RPC(
                    nameof(PlayerHealth.TakeShieldOnlyDamageWithContextAt),
                    RpcTarget.MasterClient,
                    damage,
                    view != null ? view.ViewID : 0,
                    target.position.x,
                    target.position.y,
                    (int)WeaponDamageType.Ion,
                    (int)WeaponDeliveryMethod.AreaPulse,
                    (int)(WeaponDeliveryFlags.AreaDamage | WeaponDeliveryFlags.Delayed | WeaponDeliveryFlags.ShieldFocused),
                    RippleDamageSource);
                targetView.RPC(nameof(PlayerHealth.ApplyElectromagneticShockRpc), RpcTarget.All, RippleSlowDuration, RippleSlowMultiplier, 1.35f);
                continue;
            }

            DamageNonPlayerTarget(targetView, damage, target.position, WeaponDamageType.Ion, WeaponDeliveryMethod.AreaPulse, WeaponDeliveryFlags.AreaDamage | WeaponDeliveryFlags.Delayed, RippleDamageSource);
        }
    }

    void DamageTarget(Transform target, int damage, WeaponDamageType damageType, WeaponDeliveryMethod deliveryMethod, WeaponDeliveryFlags flags, string source)
    {
        if (target == null || damage <= 0)
            return;

        if (!MapInstanceService.IsSameInstance(transform.position, target.position))
            return;

        PhotonView targetView = target.GetComponent<PhotonView>();
        if (targetView == null)
            return;

        PlayerHealth targetHealth = targetView.GetComponent<PlayerHealth>();
        if (targetHealth != null && (targetHealth.IsWreck || targetHealth == health))
            return;

        if (targetHealth != null)
        {
            targetView.RPC(
                nameof(PlayerHealth.TakeDamageWithContextAt),
                RpcTarget.MasterClient,
                damage,
                view != null ? view.ViewID : 0,
                target.position.x,
                target.position.y,
                (int)damageType,
                (int)deliveryMethod,
                (int)flags,
                source ?? string.Empty);
            return;
        }

        DamageNonPlayerTarget(targetView, damage, target.position, damageType, deliveryMethod, flags, source);
    }

    void DamageNonPlayerTarget(PhotonView targetView, int damage, Vector3 position, WeaponDamageType damageType, WeaponDeliveryMethod deliveryMethod, WeaponDeliveryFlags flags, string source)
    {
        if (targetView == null || damage <= 0)
            return;

        LureBeaconDecoy beacon = targetView.GetComponent<LureBeaconDecoy>();
        if (beacon != null && beacon.CanBeTargeted)
        {
            targetView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageAt), RpcTarget.MasterClient, damage, view != null ? view.ViewID : 0, position.x, position.y);
            return;
        }

        PlayerDeployableBase deployable = targetView.GetComponent<PlayerDeployableBase>();
        if (deployable != null && deployable.CanBeTargetedByEnemyBots)
        {
            targetView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                damage,
                damage,
                view != null ? view.ViewID : 0,
                position.x,
                position.y,
                (int)damageType,
                (int)deliveryMethod,
                (int)flags,
                source ?? string.Empty);
        }
    }

    Vector2 ResolveGuardAnchor()
    {
        Treasure secret = FindNearestAlienSecret(transform.position);
        if (secret != null)
            return secret.transform.position;

        ExtractionZone portal = FindNearestAncientPortal(transform.position);
        if (portal != null)
            return portal.transform.position;

        return TryGetHiddenBounds(out MapInstanceService.BoundsInfo bounds) ? bounds.Center : Vector2.zero;
    }

    Vector2 ResolveBlinkDestination()
    {
        Vector2 anchor = currentTarget != null ? (Vector2)currentTarget.position : ResolveGuardAnchor();
        Vector2 offset = Random.insideUnitCircle;
        if (offset.sqrMagnitude <= 0.001f)
            offset = Vector2.up;
        offset.Normalize();
        float distance = currentTarget != null ? Random.Range(5.2f, 7.2f) : Random.Range(2.8f, 4.8f);
        return ClampToHiddenInnerBounds(anchor + offset * distance, 3.2f);
    }

    Treasure FindLockdownSecret()
    {
        Treasure best = null;
        float bestDistance = float.MaxValue;
        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (!IsActiveAlienSecret(treasure) || RiftWardenLockdownField.IsTreasureLocked(treasure))
                continue;

            bool playerNearby = IsAnyPlayerNear(treasure.transform.position, LockdownTriggerRadius);
            float distance = Vector2.Distance(transform.position, treasure.transform.position);
            if (!playerNearby && distance > movement.DetectionRadius)
                continue;

            float score = playerNearby ? distance * 0.4f : distance;
            if (score < bestDistance)
            {
                bestDistance = score;
                best = treasure;
            }
        }

        return best;
    }

    Treasure FindNearestAlienSecret(Vector2 origin)
    {
        Treasure best = null;
        float bestDistance = float.MaxValue;
        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (!IsActiveAlienSecret(treasure))
                continue;

            float distance = Vector2.Distance(origin, treasure.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = treasure;
            }
        }

        return best;
    }

    ExtractionZone FindNearestAncientPortal(Vector2 origin)
    {
        ExtractionZone best = null;
        float bestDistance = float.MaxValue;
        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        for (int i = 0; i < zones.Length; i++)
        {
            ExtractionZone zone = zones[i];
            if (zone == null || !MapInstanceService.IsSameInstance(transform.position, zone.transform.position))
                continue;

            if (!MapInstanceService.TryGetExtractionTypeForObject(zone.gameObject, out string extractionType) ||
                !string.Equals(extractionType, RoomSettings.ExtractionTypeAncientPortal, System.StringComparison.Ordinal))
            {
                continue;
            }

            float distance = Vector2.Distance(origin, zone.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = zone;
            }
        }

        return best;
    }

    int CountAlienSecrets()
    {
        int count = 0;
        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            if (IsActiveAlienSecret(treasures[i]))
                count++;
        }

        return count;
    }

    bool IsActiveAlienSecret(Treasure treasure)
    {
        return treasure != null &&
               InventoryItemCatalog.IsAlienSecretItem(treasure.itemId) &&
               MapInstanceService.IsSameInstance(transform.position, treasure.transform.position);
    }

    bool IsTargetNearAnyAlienSecret(Vector2 position, float radius)
    {
        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (!IsActiveAlienSecret(treasure))
                continue;

            if (Vector2.Distance(position, treasure.transform.position) <= radius)
                return true;
        }

        return false;
    }

    bool IsAnyPlayerNear(Vector2 position, float radius)
    {
        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player == null || player.IsWreck || player.IsBotControlled || player.IsNeutralRiderControlled || player.IsAstronautControlled || player.IsEvacuationAnimating)
                continue;

            if (!MapInstanceService.IsSameInstance(transform.position, player.transform.position))
                continue;

            if (Vector2.Distance(position, player.transform.position) <= radius)
                return true;
        }

        return false;
    }

    bool TryGetHiddenBounds(out MapInstanceService.BoundsInfo bounds)
    {
        return MapInstanceService.TryGetBoundsForInstance(MapInstanceService.HiddenDimensionInstanceId, out bounds);
    }

    Vector2 ClampToHiddenInnerBounds(Vector2 position, float margin)
    {
        if (!TryGetHiddenBounds(out MapInstanceService.BoundsInfo bounds))
            return MapInstanceService.ClampToInstanceInnerBounds(position, margin);

        float safeMargin = Mathf.Max(0f, margin);
        float halfX = Mathf.Max(0.5f, bounds.InnerSize.x * 0.5f - safeMargin);
        float halfY = Mathf.Max(0.5f, bounds.InnerSize.y * 0.5f - safeMargin);
        return new Vector2(
            Mathf.Clamp(position.x, bounds.Center.x - halfX, bounds.Center.x + halfX),
            Mathf.Clamp(position.y, bounds.Center.y - halfY, bounds.Center.y + halfY));
    }
}

public static class RiftWardenLockdownField
{
    static readonly Dictionary<int, float> LockedUntilByTreasureViewId = new Dictionary<int, float>();

    public static void Register(int treasureViewId, float duration)
    {
        if (treasureViewId <= 0 || duration <= 0f)
            return;

        LockedUntilByTreasureViewId[treasureViewId] = Mathf.Max(
            LockedUntilByTreasureViewId.TryGetValue(treasureViewId, out float existingUntil) ? existingUntil : 0f,
            Time.time + duration);
    }

    public static bool IsTreasureLocked(Treasure treasure)
    {
        if (treasure == null || treasure.photonView == null || !InventoryItemCatalog.IsAlienSecretItem(treasure.itemId))
            return false;

        int viewId = treasure.photonView.ViewID;
        if (!LockedUntilByTreasureViewId.TryGetValue(viewId, out float lockedUntil))
            return false;

        if (Time.time <= lockedUntil)
            return true;

        LockedUntilByTreasureViewId.Remove(viewId);
        return false;
    }
}

public sealed class RiftWardenVfx : MonoBehaviour
{
    enum VfxMode
    {
        Beam,
        Ripple,
        Blink,
        Lockdown,
        Awaken
    }

    const int CircleSegments = 96;
    const int BeamSegments = 16;
    const float ZOffset = -0.09f;
    const int SortingOrder = GameVisualTheme.EnemySortingOrder + 28;

    static Material sharedMaterial;

    VfxMode mode;
    LineRenderer coreLine;
    LineRenderer glowLine;
    LineRenderer secondaryLine;
    Vector2 start;
    Vector2 end;
    float radius;
    float warningDuration;
    float activeDuration;
    float startedAt;
    float duration;
    bool blast;
    int followTreasureViewId;

    public static void Prewarm()
    {
        GetMaterial();
    }

    public static void PlayAwaken(Vector3 position)
    {
        RiftWardenVfx vfx = Create("RiftWardenAwakenVfx", VfxMode.Awaken);
        vfx.transform.position = new Vector3(position.x, position.y, position.z + ZOffset);
        vfx.radius = 3.2f;
        vfx.duration = 1.4f;
        vfx.InitializeCircleLines(0.12f, 0.38f);
    }

    public static void PlayBeam(Vector2 start, Vector2 end, float duration, bool blast)
    {
        RiftWardenVfx vfx = Create(blast ? "RiftWardenBeamBlastVfx" : "RiftWardenBeamAimVfx", VfxMode.Beam);
        vfx.start = start;
        vfx.end = end;
        vfx.duration = Mathf.Max(0.08f, duration);
        vfx.blast = blast;
        vfx.InitializeBeamLines(blast ? 0.13f : 0.07f, blast ? 0.42f : 0.24f);
    }

    public static void PlayRipple(Vector2 center, float radius, float warningDuration, float activeDuration)
    {
        RiftWardenVfx vfx = Create("RiftWardenTimeRippleVfx", VfxMode.Ripple);
        vfx.transform.position = new Vector3(center.x, center.y, ZOffset);
        vfx.radius = Mathf.Max(0.4f, radius);
        vfx.warningDuration = Mathf.Max(0.05f, warningDuration);
        vfx.activeDuration = Mathf.Max(0.05f, activeDuration);
        vfx.duration = vfx.warningDuration + vfx.activeDuration;
        vfx.InitializeCircleLines(0.07f, 0.24f);
    }

    public static void PlayBlink(Vector2 from, Vector2 to)
    {
        RiftWardenVfx vfx = Create("RiftWardenBlinkVfx", VfxMode.Blink);
        vfx.start = from;
        vfx.end = to;
        vfx.duration = 0.78f;
        vfx.radius = 1.55f;
        vfx.InitializeBeamLines(0.08f, 0.26f);
        vfx.secondaryLine = vfx.CreateLine("BlinkDestinationRing", 0.1f, SortingOrder + 2, true, CircleSegments);
    }

    public static void PlayLockdown(int treasureViewId, Vector2 center, float radius, float duration)
    {
        RiftWardenLockdownField.Register(treasureViewId, duration);

        RiftWardenVfx vfx = Create("RiftWardenSecretLockdownVfx", VfxMode.Lockdown);
        vfx.transform.position = new Vector3(center.x, center.y, ZOffset);
        vfx.followTreasureViewId = treasureViewId;
        vfx.radius = Mathf.Max(0.4f, radius);
        vfx.duration = Mathf.Max(0.1f, duration);
        vfx.InitializeCircleLines(0.075f, 0.27f);
    }

    static RiftWardenVfx Create(string name, VfxMode mode)
    {
        GameObject obj = new GameObject(name);
        RiftWardenVfx vfx = obj.AddComponent<RiftWardenVfx>();
        vfx.mode = mode;
        vfx.startedAt = Time.time;
        return vfx;
    }

    void InitializeBeamLines(float coreWidth, float glowWidth)
    {
        glowLine = CreateLine("Glow", glowWidth, SortingOrder, false, BeamSegments);
        coreLine = CreateLine("Core", coreWidth, SortingOrder + 1, false, BeamSegments);
    }

    void InitializeCircleLines(float coreWidth, float glowWidth)
    {
        glowLine = CreateLine("GlowRing", glowWidth, SortingOrder, true, CircleSegments);
        coreLine = CreateLine("CoreRing", coreWidth, SortingOrder + 1, true, CircleSegments);
    }

    LineRenderer CreateLine(string lineName, float width, int order, bool loop, int points)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = Mathf.Max(2, points);
        line.loop = loop;
        line.numCapVertices = 12;
        line.numCornerVertices = 8;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetMaterial();
        line.widthMultiplier = width;
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = order;
        return line;
    }

    void Update()
    {
        float t = duration > 0f ? Mathf.Clamp01((Time.time - startedAt) / duration) : 1f;
        switch (mode)
        {
            case VfxMode.Beam:
                UpdateBeam(t);
                break;
            case VfxMode.Blink:
                UpdateBlink(t);
                break;
            case VfxMode.Ripple:
                UpdateRipple(t);
                break;
            case VfxMode.Lockdown:
                UpdateLockdown(t);
                break;
            case VfxMode.Awaken:
                UpdateAwaken(t);
                break;
        }

        if (t >= 1f)
            Destroy(gameObject);
    }

    void UpdateBeam(float t)
    {
        float pulse = Mathf.PingPong(Time.time * (blast ? 8.5f : 4.5f), 1f);
        Vector2 delta = end - start;
        Vector2 normal = delta.sqrMagnitude > 0.001f ? new Vector2(-delta.y, delta.x).normalized : Vector2.right;
        UpdateSegmentedLine(glowLine, start, end, normal, blast ? 0.16f : 0.07f, pulse);
        UpdateSegmentedLine(coreLine, start, end, normal, blast ? 0.055f : 0.025f, pulse + 0.34f);
        float fade = blast ? Mathf.Lerp(1f, 0f, t) : Mathf.Lerp(0.28f, 0.72f, Mathf.PingPong(t * 3f, 1f));
        ApplyGradient(glowLine, new Color(0.04f, 0.85f, 0.78f), new Color(0.55f, 1f, 0.94f), fade * (blast ? 0.58f : 0.38f));
        ApplyGradient(coreLine, Color.white, new Color(0.25f, 1f, 0.9f), fade * (blast ? 0.95f : 0.72f));
    }

    void UpdateBlink(float t)
    {
        float fade = Mathf.Sin(t * Mathf.PI);
        Vector2 normal = (end - start).sqrMagnitude > 0.001f ? new Vector2(-(end - start).y, (end - start).x).normalized : Vector2.up;
        UpdateSegmentedLine(glowLine, start, end, normal, 0.2f * fade, t * 2f);
        UpdateSegmentedLine(coreLine, start, end, normal, 0.06f * fade, t * 4f);
        ApplyGradient(glowLine, new Color(0.04f, 0.9f, 0.82f), new Color(0.62f, 0.22f, 1f), fade * 0.48f);
        ApplyGradient(coreLine, Color.white, new Color(0.36f, 1f, 0.92f), fade * 0.88f);
        UpdateCircle(secondaryLine, end, radius * Mathf.Lerp(0.35f, 1.45f, t), t * 0.7f);
        ApplyGradient(secondaryLine, new Color(0.2f, 1f, 0.88f), Color.white, fade * 0.72f);
    }

    void UpdateRipple(float t)
    {
        float warningT = warningDuration > 0f ? Mathf.Clamp01((Time.time - startedAt) / warningDuration) : 1f;
        float activeT = Time.time > startedAt + warningDuration
            ? Mathf.Clamp01((Time.time - startedAt - warningDuration) / Mathf.Max(0.01f, activeDuration))
            : 0f;
        float currentRadius = Mathf.Lerp(radius * 0.18f, radius, warningT);
        if (activeT > 0f)
            currentRadius = Mathf.Lerp(radius * 0.92f, radius * 1.12f, activeT);

        float alpha = activeT > 0f ? Mathf.Lerp(0.95f, 0f, activeT) : Mathf.Lerp(0.18f, 0.72f, Mathf.PingPong(warningT * 5f, 1f));
        UpdateCircle(glowLine, transform.position, currentRadius, Time.time * 0.25f);
        UpdateCircle(coreLine, transform.position, currentRadius * 0.96f, -Time.time * 0.2f);
        ApplyGradient(glowLine, new Color(0.08f, 0.72f, 1f), new Color(0.42f, 1f, 0.9f), alpha * 0.42f);
        ApplyGradient(coreLine, Color.white, new Color(0.2f, 1f, 0.9f), alpha);
    }

    void UpdateLockdown(float t)
    {
        PhotonView treasureView = followTreasureViewId > 0 ? PhotonView.Find(followTreasureViewId) : null;
        if (treasureView != null)
            transform.position = new Vector3(treasureView.transform.position.x, treasureView.transform.position.y, ZOffset);

        float pulse = Mathf.PingPong(Time.time * 3.7f, 1f);
        float currentRadius = radius * Mathf.Lerp(0.9f, 1.08f, pulse);
        float fade = Mathf.Min(1f, 1f - Mathf.Clamp01((t - 0.82f) / 0.18f));
        UpdateCircle(glowLine, transform.position, currentRadius, Time.time * 0.2f);
        UpdateCircle(coreLine, transform.position, currentRadius * 0.82f, -Time.time * 0.32f);
        ApplyGradient(glowLine, new Color(0.05f, 0.75f, 0.72f), new Color(0.38f, 1f, 0.88f), fade * 0.44f);
        ApplyGradient(coreLine, Color.white, new Color(0.15f, 1f, 0.84f), fade * Mathf.Lerp(0.52f, 0.95f, pulse));
    }

    void UpdateAwaken(float t)
    {
        float fade = Mathf.Sin(t * Mathf.PI);
        float currentRadius = Mathf.Lerp(0.25f, radius, t);
        UpdateCircle(glowLine, transform.position, currentRadius, Time.time * 0.28f);
        UpdateCircle(coreLine, transform.position, currentRadius * 0.72f, -Time.time * 0.34f);
        ApplyGradient(glowLine, new Color(0.04f, 0.86f, 0.72f), new Color(0.5f, 1f, 0.92f), fade * 0.48f);
        ApplyGradient(coreLine, Color.white, new Color(0.22f, 1f, 0.86f), fade * 0.9f);
    }

    void UpdateSegmentedLine(LineRenderer line, Vector2 startPoint, Vector2 endPoint, Vector2 normal, float wave, float phase)
    {
        if (line == null)
            return;

        for (int i = 0; i < line.positionCount; i++)
        {
            float p = line.positionCount <= 1 ? 0f : i / (float)(line.positionCount - 1);
            Vector2 point = Vector2.Lerp(startPoint, endPoint, p);
            float taper = Mathf.Sin(p * Mathf.PI);
            float ripple = Mathf.Sin((p * Mathf.PI * 5f) + phase * Mathf.PI * 2f) * wave * taper;
            point += normal * ripple;
            line.SetPosition(i, new Vector3(point.x, point.y, ZOffset));
        }
    }

    void UpdateCircle(LineRenderer line, Vector2 center, float currentRadius, float phase)
    {
        if (line == null)
            return;

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = ((Mathf.PI * 2f) * i / line.positionCount) + phase;
            float notch = Mathf.Sin(angle * 4f + Time.time * 2.5f) * currentRadius * 0.035f;
            float r = currentRadius + notch;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            line.SetPosition(i, new Vector3(point.x, point.y, ZOffset));
        }
    }

    static void ApplyGradient(LineRenderer line, Color startColor, Color endColor, float alpha)
    {
        if (line == null)
            return;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(Color.Lerp(startColor, endColor, 0.65f), 0.45f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha * 0.08f, 0f),
                new GradientAlphaKey(alpha, 0.18f),
                new GradientAlphaKey(alpha * 0.86f, 0.82f),
                new GradientAlphaKey(alpha * 0.08f, 1f)
            });
        line.colorGradient = gradient;
    }

    static Material GetMaterial()
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        sharedMaterial = new Material(shader)
        {
            name = "RiftWardenVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3380;
        return sharedMaterial;
    }
}
