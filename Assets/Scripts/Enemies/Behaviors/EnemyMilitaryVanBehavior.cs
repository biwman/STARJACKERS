using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemyMilitaryVanBehavior : EnemyBotBehaviorBase
{
    const float CalmSpeedMultiplier = 0.58f;
    const float DefenseSpeedMultiplier = 1.62f;
    const float DefenseDuration = 15f;
    const float EntryProtectionMaxSeconds = 12f;
    const float EntryProtectionRefreshSeconds = 1.25f;
    const float EscapeAnimationSeconds = 0.72f;
    const float EscapeFinalScale = 0.06f;
    const float ExtractionEscapeDistance = 0.85f;
    const float FormationWanderWeight = 0.12f;
    const float FriendlyLineOfFireRadius = 0.18f;

    static readonly RaycastHit2D[] FriendlyLineOfFireHits = new RaycastHit2D[24];

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    PlayerShooting shooting;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    ExtractionZone[] extractionZones = System.Array.Empty<ExtractionZone>();
    Transform currentTarget;
    int targetZoneIndex;
    int preferredTargetViewId;
    float nextZoneRefreshTime;
    float nextTargetRefreshTime;
    float defenseUntil;
    float entryProtectionReleaseAt;
    bool entryProtectionActive;
    bool hasChosenExtractionRoute;
    bool escapeHandled;
    Coroutine escapeVisualRoutine;

    bool IsDefending => Time.time < defenseUntil;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        shooting = owner.GetComponent<PlayerShooting>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;
        ConfigureWeapon();
        RefreshExtractionZones(true);
        BeginEntryProtection();
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null || escapeHandled)
            return;

        if (health != null && health.IsWreck)
            return;

        UpdateEntryProtection();

        if (Time.time >= nextZoneRefreshTime || extractionZones == null || extractionZones.Length == 0)
            RefreshExtractionZones(false);

        if (IsDefending)
            TickDefense();
        else
            TickCalmFlight();
    }

    public void NotifyDamageSource(int attackerViewId)
    {
        if (!EnemyFriendlyFirePolicy.ShouldReactToDamageSource(bot, attackerViewId))
            return;

        EnterDefenseMode(attackerViewId, transform.position, true);
    }

    public void EnterDefenseMode(int attackerViewId, Vector2 focusPosition, bool broadcast)
    {
        defenseUntil = Mathf.Max(defenseUntil, Time.time + DefenseDuration);
        preferredTargetViewId = Mathf.Max(0, attackerViewId);
        currentTarget = ResolvePreferredTarget();
        if (currentTarget == null)
            currentTarget = EnemyTargetingUtility.FindClosestTarget(focusPosition, health, movement != null ? movement.DetectionRadius : 17f, true);

        if (broadcast)
            EnemyBotManager.NotifyMilitaryVanAttacked(bot, attackerViewId);
    }

    void ConfigureWeapon()
    {
        if (shooting == null || weapon == null || bot == null)
            return;

        shooting.ConfigureWeaponProfile(
            weapon.FireRate,
            Mathf.Max(weapon.AmmoCount, 1),
            weapon.ReloadDuration,
            RoomSettings.GetEnemyDamage(bot.Kind),
            weapon.BulletScaleMultiplier,
            weapon.BulletColor,
            weapon.MuzzleOffsetDistance,
            weapon.InfiniteAmmo,
            weapon.BulletSpeed,
            weapon.ShotSoundId,
            weapon.Range,
            weapon.HitEffectId ?? string.Empty,
            10f,
            weapon.DamageType,
            weapon.DeliveryMethod,
            weapon.DeliveryFlags);
    }

    void BeginEntryProtection()
    {
        entryProtectionActive = health != null;
        entryProtectionReleaseAt = Time.time + EntryProtectionMaxSeconds;
        if (entryProtectionActive)
            health.BeginBotSpawnInvulnerability(EntryProtectionMaxSeconds);
    }

    void UpdateEntryProtection()
    {
        if (!entryProtectionActive || health == null)
            return;

        if (IsFullyInsideGameplayBounds())
        {
            entryProtectionActive = false;
            health.ClearBotSpawnInvulnerability();
            return;
        }

        if (RoomSettings.AreToxicBordersEnabled())
        {
            health.BeginBotSpawnInvulnerability(EntryProtectionRefreshSeconds);
            return;
        }

        if (Time.time >= entryProtectionReleaseAt)
        {
            entryProtectionActive = false;
            health.ClearBotSpawnInvulnerability();
        }
    }

    bool IsFullyInsideGameplayBounds()
    {
        Vector2 size = RoomSettings.GetGameplayMapDimensions();
        float halfX = Mathf.Max(0.1f, size.x * 0.5f);
        float halfY = Mathf.Max(0.1f, size.y * 0.5f);
        Bounds bounds = ResolveBodyBounds();
        return bounds.min.x >= -halfX &&
               bounds.max.x <= halfX &&
               bounds.min.y >= -halfY &&
               bounds.max.y <= halfY;
    }

    Bounds ResolveBodyBounds()
    {
        Collider2D bodyCollider = GetComponentInChildren<Collider2D>();
        if (bodyCollider != null)
            return bodyCollider.bounds;

        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
            return renderer.bounds;

        return new Bounds(rb != null ? rb.position : (Vector2)transform.position, Vector3.one * 0.1f);
    }

    void TickCalmFlight()
    {
        ExtractionZone targetZone = GetTargetExtractionZone();
        Vector2 desiredDirection = ResolveCalmDirection(targetZone);
        ApplyVelocityAndRotation(desiredDirection, CalmSpeedMultiplier, desiredDirection);

        if (!entryProtectionActive && targetZone != null && targetZone.GetInteractionDistanceToPoint(rb.position) <= ExtractionEscapeDistance)
            EscapeFromRound();
    }

    void TickDefense()
    {
        if (Time.time >= nextTargetRefreshTime)
        {
            nextTargetRefreshTime = Time.time + Mathf.Max(0.1f, movement.TargetRefreshInterval);
            currentTarget = ResolveCombatTarget();
        }

        if (currentTarget == null)
        {
            TickCalmFlight();
            return;
        }

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        Vector2 moveDirection = ResolveDefenseMoveDirection(toTarget);
        Vector2 aimDirection = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : moveDirection;
        ApplyVelocityAndRotation(moveDirection, DefenseSpeedMultiplier, aimDirection);
        TryShootAtTarget(aimDirection, toTarget.magnitude);
    }

    Vector2 ResolveCalmDirection(ExtractionZone targetZone)
    {
        if (targetZone == null)
            return rb.linearVelocity.sqrMagnitude > 0.01f ? rb.linearVelocity.normalized : Vector2.right;

        Vector2 toTarget = (Vector2)targetZone.transform.position - rb.position;
        if (toTarget.sqrMagnitude <= 0.001f)
            return Vector2.right;

        Vector2 direction = toTarget.normalized;
        Vector2 wander = new Vector2(
            Mathf.Sin(Time.time * 0.31f + view.ViewID * 0.13f),
            Mathf.Cos(Time.time * 0.27f + view.ViewID * 0.17f));
        return (direction + wander * FormationWanderWeight).normalized;
    }

    Vector2 ResolveDefenseMoveDirection(Vector2 toTarget)
    {
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return rb.linearVelocity.sqrMagnitude > 0.01f ? rb.linearVelocity.normalized : Vector2.right;

        Vector2 toward = toTarget / distance;
        Vector2 orbit = new Vector2(-toward.y, toward.x);
        if ((view.ViewID & 1) == 0)
            orbit *= -1f;

        if (distance > movement.PreferredDistance)
            return (toward * 0.9f + orbit * 0.18f).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.58f + orbit * 0.72f).normalized;

        return (orbit * 0.72f + toward * 0.22f).normalized;
    }

    void ApplyVelocityAndRotation(Vector2 moveDirection, float speedMultiplier, Vector2 facingDirection)
    {
        if (moveDirection.sqrMagnitude <= 0.001f)
            moveDirection = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : Vector2.right;

        Vector2 desiredVelocity = moveDirection.normalized * bot.EffectiveMoveSpeed * speedMultiplier;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.14f);

        Vector2 direction = facingDirection.sqrMagnitude > 0.001f ? facingDirection.normalized : moveDirection.normalized;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    void TryShootAtTarget(Vector2 aimDirection, float distance)
    {
        if (shooting == null || weapon == null || currentTarget == null || distance > weapon.Range || aimDirection.sqrMagnitude <= 0.001f)
            return;

        if (Vector2.Dot(GetForwardDirection(), aimDirection.normalized) < 0.74f)
            return;

        Vector2 targetPoint = ResolveTargetAimPoint();
        Vector3[] clearMuzzles = ResolveMuzzlesWithClearFriendlyLine(targetPoint);
        if (clearMuzzles.Length <= 0)
            return;

        shooting.TryFireBotVolleyAtPointFromWorld(clearMuzzles, targetPoint, Random.Range(-0.035f, 0.035f));
    }

    Vector3[] ResolveMuzzles()
    {
        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        float lateral = 0.72f;
        float forward = Mathf.Max(0.35f, weapon != null ? weapon.MuzzleOffsetDistance : 0.55f);
        if (renderer != null)
        {
            lateral = Mathf.Max(0.4f, renderer.bounds.extents.y * 0.62f);
            forward = Mathf.Max(forward, renderer.bounds.extents.x * 0.42f);
        }

        Vector3 center = transform.position + (Vector3)(GetForwardDirection() * forward);
        Vector3 side = transform.up;
        return new[]
        {
            center + side * lateral,
            center - side * lateral
        };
    }

    Vector3[] ResolveMuzzlesWithClearFriendlyLine(Vector2 targetPoint)
    {
        Vector3[] muzzles = ResolveMuzzles();
        List<Vector3> filtered = null;
        for (int i = 0; i < muzzles.Length; i++)
        {
            if (!IsFriendlyLineOfFireBlocked(muzzles[i], targetPoint))
            {
                if (filtered != null)
                    filtered.Add(muzzles[i]);

                continue;
            }

            if (filtered == null)
            {
                filtered = new List<Vector3>(muzzles.Length);
                for (int previous = 0; previous < i; previous++)
                    filtered.Add(muzzles[previous]);
            }
        }

        return filtered != null ? filtered.ToArray() : muzzles;
    }

    bool IsFriendlyLineOfFireBlocked(Vector2 origin, Vector2 targetPoint)
    {
        Vector2 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.05f)
            return false;

        int hitCount = Physics2D.CircleCast(
            origin,
            FriendlyLineOfFireRadius,
            toTarget / distance,
            CreateLineOfFireFilter(),
            FriendlyLineOfFireHits,
            distance);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = FriendlyLineOfFireHits[i].collider;
            if (hitCollider == null || hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
                continue;

            EnemyBot blockerBot = hitCollider.GetComponentInParent<EnemyBot>();
            if (blockerBot == null || blockerBot == bot)
                continue;

            PlayerHealth blockerHealth = blockerBot.GetComponent<PlayerHealth>();
            if (blockerHealth != null && blockerHealth.IsWreck)
                continue;

            if (EnemyFriendlyFirePolicy.ShouldBlockLineOfFire(bot, blockerBot, ResolveProjectileEffectId()))
                return true;
        }

        return false;
    }

    static ContactFilter2D CreateLineOfFireFilter()
    {
        return new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = false
        };
    }

    string ResolveProjectileEffectId()
    {
        return weapon != null ? weapon.HitEffectId ?? string.Empty : string.Empty;
    }

    Vector2 ResolveTargetAimPoint()
    {
        if (currentTarget == null)
            return transform.position + (Vector3)(GetForwardDirection() * (weapon != null ? weapon.Range : 8f));

        Collider2D targetCollider = currentTarget.GetComponentInChildren<Collider2D>();
        if (targetCollider != null)
            return targetCollider.bounds.center;

        SpriteRenderer targetRenderer = currentTarget.GetComponentInChildren<SpriteRenderer>();
        if (targetRenderer != null)
            return targetRenderer.bounds.center;

        return currentTarget.position;
    }

    Vector2 GetForwardDirection()
    {
        float radians = rb.rotation * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
    }

    Transform ResolveCombatTarget()
    {
        Transform preferred = ResolvePreferredTarget();
        if (EnemyTargetingUtility.IsTargetValid(preferred, health, rb.position, movement.DisengageRadius, true))
            return preferred;

        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, rb.position, movement.DisengageRadius, true))
            return currentTarget;

        return EnemyTargetingUtility.FindClosestTarget(rb.position, health, movement.DetectionRadius, true);
    }

    Transform ResolvePreferredTarget()
    {
        if (preferredTargetViewId <= 0)
            return null;

        PhotonView targetView = PhotonView.Find(preferredTargetViewId);
        return targetView != null ? targetView.transform : null;
    }

    void RefreshExtractionZones(bool preferLongRoute)
    {
        nextZoneRefreshTime = Time.time + Mathf.Max(0.3f, movement != null ? movement.TargetRefreshInterval : 0.45f);
        extractionZones = RuntimeSceneQueryCache.GetExtractionZones();
        if (extractionZones == null || extractionZones.Length == 0)
            return;

        if (preferLongRoute || !hasChosenExtractionRoute)
        {
            targetZoneIndex = FindFarthestZoneIndex();
            hasChosenExtractionRoute = true;
        }
        else
        {
            targetZoneIndex = Mathf.Clamp(targetZoneIndex, 0, extractionZones.Length - 1);
        }
    }

    ExtractionZone GetTargetExtractionZone()
    {
        if (extractionZones == null || extractionZones.Length == 0)
            return null;

        ExtractionZone targetZone = extractionZones[Mathf.Clamp(targetZoneIndex, 0, extractionZones.Length - 1)];
        if (targetZone != null && targetZone.gameObject.activeInHierarchy)
            return targetZone;

        targetZoneIndex = FindNearestZoneIndex();
        return extractionZones != null && extractionZones.Length > 0
            ? extractionZones[Mathf.Clamp(targetZoneIndex, 0, extractionZones.Length - 1)]
            : null;
    }

    int FindNearestZoneIndex()
    {
        if (extractionZones == null || extractionZones.Length == 0)
            return 0;

        int bestIndex = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < extractionZones.Length; i++)
        {
            ExtractionZone zone = extractionZones[i];
            if (zone == null || !zone.gameObject.activeInHierarchy)
                continue;

            float distance = Vector2.Distance(rb != null ? rb.position : (Vector2)transform.position, zone.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    int FindFarthestZoneIndex()
    {
        if (extractionZones == null || extractionZones.Length == 0)
            return 0;

        int bestIndex = 0;
        float bestDistance = float.MinValue;
        Vector2 position = rb != null ? rb.position : (Vector2)transform.position;
        for (int i = 0; i < extractionZones.Length; i++)
        {
            ExtractionZone zone = extractionZones[i];
            if (zone == null || !zone.gameObject.activeInHierarchy)
                continue;

            float distance = Vector2.Distance(position, zone.transform.position);
            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    void EscapeFromRound()
    {
        if (escapeHandled)
            return;

        escapeHandled = true;
        entryProtectionActive = false;
        if (health != null)
            health.BeginBotSpawnInvulnerability(EscapeAnimationSeconds + 0.35f);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        DisableCollidersForEscape();
        if (PhotonNetwork.CurrentRoom != null)
        {
            if (view != null)
                view.RPC(nameof(PlayMilitaryVanEscapeAnimationRpc), RpcTarget.All, EscapeAnimationSeconds);

            StartCoroutine(CompleteNetworkEscapeAfterAnimation(EscapeAnimationSeconds));
        }
        else
        {
            PlayMilitaryVanEscapeAnimationRpc(EscapeAnimationSeconds);
            StartCoroutine(CompleteLocalEscapeAfterAnimation(EscapeAnimationSeconds));
        }
    }

    [PunRPC]
    void PlayMilitaryVanEscapeAnimationRpc(float duration)
    {
        escapeHandled = true;
        DisableCollidersForEscape();
        if (escapeVisualRoutine != null)
            StopCoroutine(escapeVisualRoutine);

        escapeVisualRoutine = StartCoroutine(PlayEscapeVisualRoutine(Mathf.Max(0.08f, duration)));
    }

    IEnumerator PlayEscapeVisualRoutine(float duration)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        Color[] startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            startColors[i] = renderers[i] != null ? renderers[i].color : Color.white;

        Vector3 startScale = transform.localScale;
        float startedAt = Time.time;
        while (Time.time - startedAt < duration)
        {
            float t = Mathf.Clamp01((Time.time - startedAt) / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            ApplyEscapeVisuals(startScale, startColors, renderers, eased);
            yield return null;
        }

        ApplyEscapeVisuals(startScale, startColors, renderers, 1f);
        escapeVisualRoutine = null;
    }

    void ApplyEscapeVisuals(Vector3 startScale, Color[] startColors, SpriteRenderer[] renderers, float eased)
    {
        float scale = Mathf.Lerp(1f, EscapeFinalScale, Mathf.Clamp01(eased));
        transform.localScale = new Vector3(startScale.x * scale, startScale.y * scale, startScale.z);
        float alphaMultiplier = 1f - Mathf.Clamp01(eased);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Color color = i < startColors.Length ? startColors[i] : renderer.color;
            color.a *= alphaMultiplier;
            renderer.color = color;
        }
    }

    IEnumerator CompleteNetworkEscapeAfterAnimation(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0.08f, duration));
        EnemyBotManager.NotifyMilitaryVanEscaped(bot);
        if (PhotonNetwork.CurrentRoom != null)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    IEnumerator CompleteLocalEscapeAfterAnimation(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0.08f, duration));
        EnemyBotManager.NotifyMilitaryVanEscaped(bot);
        Destroy(gameObject);
    }

    void DisableCollidersForEscape()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }
}
