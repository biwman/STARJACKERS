using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public static class NewItemsRuntime
{
    public const float SpaceTrapArmRange = 3.1f;
    static readonly Collider2D[] SpawnPositionHits = new Collider2D[48];

    public static bool TryDeployAutoTurret(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        Vector3 spawnPosition = ResolveRearSpawnPosition(owner.transform, 0.95f, 0.42f);
        int ownerActorNumber = ResolveOwnerActorNumber(owner.photonView);
        GameObject turretObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            owner.transform.rotation,
            0,
            new object[] { PlayerDeployableRuntime.AutoTurretMarker, owner.photonView.ViewID, ownerActorNumber });

        if (turretObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(turretObject);
        return true;
    }

    public static bool TryDeployRocketAutoTurret(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        Vector3 spawnPosition = ResolveRearSpawnPosition(owner.transform, 1.05f, 0.46f);
        int ownerActorNumber = ResolveOwnerActorNumber(owner.photonView);
        GameObject turretObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            owner.transform.rotation,
            0,
            new object[] { PlayerDeployableRuntime.RocketAutoTurretMarker, owner.photonView.ViewID, ownerActorNumber });

        if (turretObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(turretObject);
        return true;
    }

    static int ResolveOwnerActorNumber(PhotonView view)
    {
        if (view == null)
            return 0;

        if (view.Owner != null && view.Owner.ActorNumber > 0)
            return view.Owner.ActorNumber;

        return Mathf.Max(0, view.OwnerActorNr);
    }

    public static bool TryDeploySpaceBomb(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        Vector3 spawnPosition = ResolveRearSpawnPosition(owner.transform, 0.95f, 1.1f);
        Vector2 forward = owner.transform.up.sqrMagnitude > 0.001f ? (Vector2)owner.transform.up.normalized : Vector2.up;
        GameObject bombObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            Quaternion.LookRotation(Vector3.forward, forward),
            0,
            new object[] { PlayerDeployableRuntime.SpaceBombMarker, owner.photonView.ViewID, forward.x, forward.y });

        if (bombObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(bombObject);
        return true;
    }

    public static bool TryDeployStasisBuoy(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        Vector3 spawnPosition = ResolveRearSpawnPosition(owner.transform, 1.05f, 0.42f);
        GameObject buoyObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            Quaternion.identity,
            0,
            new object[] { PlayerDeployableRuntime.StasisBuoyMarker, owner.photonView.ViewID });

        if (buoyObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(buoyObject);
        return true;
    }

    public static bool TryDeployMetalDriftWall(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        Vector2 forward = owner.transform.up.sqrMagnitude > 0.001f ? (Vector2)owner.transform.up.normalized : Vector2.up;
        Vector3 spawnPosition = ResolveForwardSpawnPosition(owner.transform, 1.35f, 0.72f);
        Rigidbody2D ownerBody = owner.GetComponent<Rigidbody2D>();
        Vector2 inheritedVelocity = ownerBody != null ? ownerBody.linearVelocity * 0.28f : Vector2.zero;
        Vector2 initialVelocity = Vector2.ClampMagnitude(inheritedVelocity + forward * 0.55f, 2.15f);
        GameObject wallObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            Quaternion.LookRotation(Vector3.forward, forward),
            0,
            new object[] { PlayerDeployableRuntime.MetalDriftWallMarker, owner.photonView.ViewID, forward.x, forward.y, initialVelocity.x, initialVelocity.y });

        if (wallObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(wallObject);
        return true;
    }

    public static bool TryLaunchSpaceTorpedo(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        Vector2 direction = owner.transform.up.sqrMagnitude > 0.001f ? (Vector2)owner.transform.up.normalized : Vector2.up;
        Vector3 spawnPosition = owner.transform.position + (Vector3)(direction * 0.82f);
        GameObject torpedoObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            Quaternion.LookRotation(Vector3.forward, direction),
            0,
            new object[] { PlayerDeployableRuntime.SpaceTorpedoMarker, owner.photonView.ViewID, direction.x, direction.y });

        if (torpedoObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(torpedoObject);
        return true;
    }

    public static bool TryStartTetherHarpoon(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        PhotonView target = FindNearestEnemyShipTarget(owner, 7.2f);
        if (target == null)
            return false;

        owner.photonView.RPC("PlayTetherHarpoonFxRpc", RpcTarget.All, target.ViewID);
        owner.StartCoroutine(TetherHarpoonRoutine(owner, target.ViewID));
        return true;
    }

    public static bool TryFireBioTrap(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        PhotonView target = FindNearestAstronautTarget(owner, 6f);
        if (target == null)
            return false;

        PlayerHealth targetHealth = target.GetComponent<PlayerHealth>();
        if (IsBioTrapProtectedEscapePod(targetHealth))
            return false;

        Vector2 origin = owner.transform.position;
        Vector2 targetPosition = target.transform.position;
        Vector2 drift = targetPosition - origin;
        if (drift.sqrMagnitude <= 0.001f)
            drift = UnityEngine.Random.insideUnitCircle;
        if (drift.sqrMagnitude <= 0.001f)
            drift = Vector2.up;

        owner.photonView.RPC("PlayBioTrapFxRpc", RpcTarget.All, target.ViewID);
        DroppedCargoManager.DropItemAtPosition(
            InventoryItemCatalog.CaptiveAstronautPodId,
            new Vector3(targetPosition.x, targetPosition.y, 0f),
            drift.normalized * 0.65f);
        if (targetHealth != null && targetHealth.photonView != null)
        {
            targetHealth.photonView.RPC(
                nameof(PlayerHealth.ForceBioTrapAstronautDeathRpc),
                RpcTarget.MasterClient,
                owner.photonView.ViewID,
                targetPosition.x,
                targetPosition.y);
        }
        else
        {
            PhotonNetwork.Destroy(target.gameObject);
        }
        return true;
    }

    public static bool TryDetonateAsteroidBreacherBomb(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        ObstacleChunk target = FindNearestObstacle(owner.transform.position, 7.2f);
        if (target == null)
            return false;

        Vector2 targetPosition = target.transform.position;
        Vector2 direction = targetPosition - (Vector2)owner.transform.position;
        if (direction.sqrMagnitude <= 0.001f)
            direction = owner.transform.up.sqrMagnitude > 0.001f ? (Vector2)owner.transform.up : Vector2.up;
        direction.Normalize();

        Vector3 spawnPosition = owner.transform.position + (Vector3)(direction * 0.72f);
        GameObject bombObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            Quaternion.LookRotation(Vector3.forward, direction),
            0,
            new object[] { PlayerDeployableRuntime.AsteroidBreacherBombMarker, owner.photonView.ViewID, target.StableId ?? string.Empty, targetPosition.x, targetPosition.y });

        if (bombObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(bombObject);
        return true;
    }

    static PhotonView FindNearestEnemyShipTarget(PlayerShooting owner, float maxRange)
    {
        if (owner == null || owner.photonView == null)
            return null;

        int ownerActorNumber = owner.photonView.OwnerActorNr;
        Vector2 origin = owner.transform.position;
        PlayerHealth[] healths = UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        PhotonView best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (candidate == null ||
                candidate.IsWreck ||
                candidate.IsAstronautControlled ||
                candidate.IsEvacuationAnimating ||
                candidate.photonView == null ||
                candidate.photonView.ViewID == owner.photonView.ViewID ||
                candidate.GetComponent<LureBeaconDecoy>() != null ||
                candidate.GetComponent<PlayerDeployableBase>() != null)
            {
                continue;
            }

            EnemyBot bot = candidate.GetComponent<EnemyBot>();
            if (bot != null && !bot.CanReceivePilotHostileEffect())
                continue;

            bool computerTarget = candidate.IsBotControlled || candidate.IsNeutralRiderControlled;
            if (ownerActorNumber > 0 && candidate.photonView.OwnerActorNr == ownerActorNumber && !computerTarget)
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance > maxRange || distance >= bestDistance)
                continue;

            best = candidate.photonView;
            bestDistance = distance;
        }

        return best;
    }

    static PhotonView FindNearestAstronautTarget(PlayerShooting owner, float maxRange)
    {
        int ownerActorNumber = owner != null && owner.photonView != null ? owner.photonView.OwnerActorNr : -1;
        Vector2 origin = owner != null ? (Vector2)owner.transform.position : Vector2.zero;
        PlayerHealth[] healths = UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        PhotonView best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (candidate == null ||
                candidate.IsWreck ||
                candidate.photonView == null)
            {
                continue;
            }

            AstronautSurvivor survivor = candidate.GetComponent<AstronautSurvivor>();
            bool isAstronautTarget = candidate.IsAstronautControlled || candidate.IsEnemyAstronautControlled || survivor != null;
            if (!isAstronautTarget)
                continue;

            if (IsBioTrapProtectedEscapePod(candidate))
                continue;

            if (owner != null && owner.photonView != null && candidate.photonView.ViewID == owner.photonView.ViewID)
                continue;

            bool computerAstronaut = candidate.IsEnemyAstronautControlled || (survivor != null && survivor.IsEnemySurvivor);
            if (!computerAstronaut && ownerActorNumber > 0 && candidate.photonView.OwnerActorNr == ownerActorNumber)
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance > maxRange || distance >= bestDistance)
                continue;

            best = candidate.photonView;
            bestDistance = distance;
        }

        return best;
    }

    static bool IsBioTrapProtectedEscapePod(PlayerHealth candidate)
    {
        if (candidate == null)
            return false;

        AstronautSurvivor survivor = candidate.GetComponent<AstronautSurvivor>();
        if (survivor != null && survivor.IsEscapePodMode)
            return true;

        PhotonView view = candidate.photonView != null ? candidate.photonView : candidate.GetComponent<PhotonView>();
        return AstronautSurvivor.IsEscapePodInstantiationData(view != null ? view.InstantiationData : null);
    }

    static ObstacleChunk FindNearestObstacle(Vector2 origin, float maxRange)
    {
        ObstacleChunk[] obstacles = UnityEngine.Object.FindObjectsByType<ObstacleChunk>(FindObjectsInactive.Exclude);
        ObstacleChunk best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < obstacles.Length; i++)
        {
            ObstacleChunk candidate = obstacles[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.StableId))
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance > maxRange || distance >= bestDistance)
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    static IEnumerator TetherHarpoonRoutine(PlayerShooting owner, int targetViewId)
    {
        const float duration = 4.5f;
        const float pullStrength = 42f;
        const float selfPullStrength = 10f;
        const float slackDistance = 2.35f;
        const float shockTickInterval = 0.35f;

        if (owner == null || owner.photonView == null)
            yield break;

        int sourceViewId = owner.photonView.ViewID;
        owner.photonView.RPC("StartTractorBeamEffects", RpcTarget.All, sourceViewId, targetViewId);
        float elapsed = 0f;
        float nextShockTime = 0f;
        WaitForFixedUpdate wait = new WaitForFixedUpdate();
        while (elapsed < duration)
        {
            PhotonView target = PhotonView.Find(targetViewId);
            if (target == null || !IsValidTetherHarpoonTarget(owner, target))
                break;

            Vector2 sourcePosition = owner.transform.position;
            Rigidbody2D targetBody = target.GetComponent<Rigidbody2D>();
            if (targetBody != null)
            {
                Vector2 toSource = sourcePosition - targetBody.position;
                float distance = toSource.magnitude;
                if (distance > 0.08f)
                {
                    float stretch = Mathf.Max(0f, distance - slackDistance);
                    float ramp = Mathf.Clamp01(stretch / 3.5f);
                    targetBody.linearVelocity += toSource.normalized * pullStrength * Mathf.Lerp(0.45f, 1.35f, ramp) * Time.fixedDeltaTime;
                    targetBody.linearVelocity = Vector2.ClampMagnitude(targetBody.linearVelocity, Mathf.Lerp(5.5f, 10.5f, ramp));
                }
            }

            Rigidbody2D ownerBody = owner.GetComponent<Rigidbody2D>();
            if (ownerBody != null && targetBody != null)
            {
                Vector2 toTarget = targetBody.position - ownerBody.position;
                if (toTarget.sqrMagnitude > slackDistance * slackDistance)
                    ownerBody.linearVelocity += toTarget.normalized * selfPullStrength * Time.fixedDeltaTime;
            }

            if (Time.time >= nextShockTime)
            {
                nextShockTime = Time.time + shockTickInterval;
                PlayerHealth targetHealth = target.GetComponent<PlayerHealth>();
                if (targetHealth != null && targetHealth.photonView != null)
                    targetHealth.photonView.RPC(nameof(PlayerHealth.ApplyElectromagneticShockRpc), RpcTarget.All, 0.7f, 0.45f, 1.55f);
            }

            elapsed += Time.fixedDeltaTime;
            yield return wait;
        }

        if (owner != null && owner.photonView != null)
            owner.photonView.RPC("StopTractorBeamEffects", RpcTarget.All, sourceViewId);
    }

    static bool IsValidTetherHarpoonTarget(PlayerShooting owner, PhotonView target)
    {
        if (owner == null || owner.photonView == null || target == null)
            return false;

        PlayerHealth health = target.GetComponent<PlayerHealth>();
        return health != null &&
               !health.IsWreck &&
               !health.IsAstronautControlled &&
               !health.IsEvacuationAnimating &&
               target.GetComponent<LureBeaconDecoy>() == null &&
               target.GetComponent<PlayerDeployableBase>() == null &&
               target.ViewID != owner.photonView.ViewID &&
               CanReceiveHostileGadgetEffect(health);
    }

    static bool CanReceiveHostileGadgetEffect(PlayerHealth health)
    {
        if (health == null)
            return false;

        EnemyBot bot = health.GetComponent<EnemyBot>();
        return bot == null || bot.CanReceivePilotHostileEffect();
    }

    public static void ApplyBreacherExplosionDamage(Vector2 center, int ownerViewId)
    {
        const float radius = 1.65f;
        const int damage = 38;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        HashSet<int> damagedViews = new HashSet<int>();
        WeaponHitContext hitContext = new WeaponHitContext(
            WeaponDamageType.Explosive,
            WeaponDeliveryMethod.RemoteStrike,
            WeaponDeliveryFlags.AreaDamage,
            PlayerHealth.DamageSourceExplosive);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable.photonView != null && deployable.CanBeTargeted && damagedViews.Add(deployable.photonView.ViewID))
            {
                deployable.photonView.RPC(
                    nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                    RpcTarget.MasterClient,
                    damage,
                    damage,
                    ownerViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
                continue;
            }

            PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
            if (health != null && health.photonView != null && !health.IsWreck && health.photonView.ViewID != ownerViewId && damagedViews.Add(health.photonView.ViewID))
            {
                health.photonView.RPC(
                    nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                    RpcTarget.MasterClient,
                    damage,
                    damage,
                    ownerViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
            }
        }
    }

    public static bool TryLaunchSpaceDrill(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null || owner.photonView.Owner == null)
            return false;

        PhotonView target = SpaceDrillDeployable.FindNearestLootableAsteroid(owner.transform.position);
        if (target == null)
            return false;

        Treasure treasure = target.GetComponent<Treasure>();
        if (treasure == null || !PlayerProfileService.PlayerHasFreeShipInventorySlot(owner.photonView.Owner, treasure.itemId))
            return false;

        Vector3 spawnPosition = owner.transform.position + owner.transform.right * 0.75f - owner.transform.up * 0.15f;
        GameObject drillObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            owner.transform.rotation,
            0,
            new object[] { PlayerDeployableRuntime.SpaceDrillMarker, owner.photonView.ViewID, target.ViewID });

        if (drillObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(drillObject);
        return true;
    }

    public static bool TryLaunchDropbot(PlayerShooting owner, string itemId)
    {
        if (!PhotonNetwork.IsMasterClient ||
            owner == null ||
            owner.photonView == null ||
            owner.photonView.Owner == null ||
            string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (!DropbotDeployable.TryFindNearestExtraction(owner.transform.position, out Vector2 extractionPosition))
            return false;

        Vector3 spawnPosition = owner.transform.position + owner.transform.right * 0.68f - owner.transform.up * 0.12f;
        Vector2 direction = extractionPosition - (Vector2)spawnPosition;
        Quaternion rotation = direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(Vector3.forward, direction.normalized)
            : owner.transform.rotation;

        GameObject dropbotObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            rotation,
            0,
            new object[] { PlayerDeployableRuntime.DropbotMarker, owner.photonView.ViewID, itemId, extractionPosition.x, extractionPosition.y });

        if (dropbotObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(dropbotObject);
        return true;
    }

    public static bool TryArmSpaceTrap(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        PhotonView target = SpaceTrapTarget.FindClosestTrapTarget(owner.transform.position, SpaceTrapArmRange);
        if (target == null)
            return false;

        if (!SpaceTrapTarget.TryArmTarget(target, owner.photonView.ViewID))
            return false;

        owner.photonView.RPC("ArmSpaceTrapTargetRpc", RpcTarget.All, target.ViewID, owner.photonView.ViewID);
        return true;
    }

    static Vector3 ResolveRearSpawnPosition(Transform source, float distance, float clearanceRadius)
    {
        Vector2 origin = source != null ? (Vector2)source.position : Vector2.zero;
        Vector2 forward = source != null && source.up.sqrMagnitude > 0.001f ? (Vector2)source.up.normalized : Vector2.up;
        Vector2 right = source != null && source.right.sqrMagnitude > 0.001f ? (Vector2)source.right.normalized : Vector2.right;
        Vector2[] directions =
        {
            -forward,
            (-forward + right * 0.55f).normalized,
            (-forward - right * 0.55f).normalized,
            right,
            -right
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2 candidate = origin + directions[i] * distance;
            if (IsSpawnPositionFree(candidate, clearanceRadius, source))
                return new Vector3(candidate.x, candidate.y, 0f);
        }

        Vector2 fallback = origin - forward * distance;
        return new Vector3(fallback.x, fallback.y, 0f);
    }

    static Vector3 ResolveForwardSpawnPosition(Transform source, float distance, float clearanceRadius)
    {
        Vector2 origin = source != null ? (Vector2)source.position : Vector2.zero;
        Vector2 forward = source != null && source.up.sqrMagnitude > 0.001f ? (Vector2)source.up.normalized : Vector2.up;
        Vector2 right = source != null && source.right.sqrMagnitude > 0.001f ? (Vector2)source.right.normalized : Vector2.right;
        Vector2[] directions =
        {
            forward,
            (forward + right * 0.42f).normalized,
            (forward - right * 0.42f).normalized,
            right,
            -right
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2 candidate = origin + directions[i] * distance;
            if (IsSpawnPositionFree(candidate, clearanceRadius, source))
                return new Vector3(candidate.x, candidate.y, 0f);
        }

        Vector2 fallback = origin + forward * distance;
        return new Vector3(fallback.x, fallback.y, 0f);
    }

    static bool IsSpawnPositionFree(Vector2 position, float radius, Transform source)
    {
        int hitCount = Physics2D.OverlapCircle(position, radius, CreatePhysicsQueryFilter(), SpawnPositionHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = SpawnPositionHits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (source != null && (hit.transform == source || hit.transform.IsChildOf(source)))
                continue;

            return false;
        }

        return true;
    }

    static ContactFilter2D CreatePhysicsQueryFilter()
    {
        return new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };
    }
}

public static class RuntimeSpriteUtility
{
    static readonly Dictionary<string, Sprite> SpriteCacheByKey = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    static Sprite arrowSprite;

    public static Sprite LoadSprite(string resourcePath, string editorAssetPath)
    {
        string cacheKey = BuildCacheKey(resourcePath, editorAssetPath);
        if (SpriteCacheByKey.TryGetValue(cacheKey, out Sprite cachedSprite))
            return cachedSprite;

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            sprite = GetLargestSprite(sprites);
        }

        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(100f, Mathf.Max(texture.width, texture.height)));
        }

#if UNITY_EDITOR
        if (sprite == null && !string.IsNullOrWhiteSpace(editorAssetPath))
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(editorAssetPath);
#endif

        SpriteCacheByKey[cacheKey] = sprite;
        PrewarmSpriteTexture(sprite);
        return sprite;
    }

    public static void PrewarmSprites(params string[] resourcePaths)
    {
        if (resourcePaths == null)
            return;

        for (int i = 0; i < resourcePaths.Length; i++)
            LoadSprite(resourcePaths[i], string.Empty);
    }

    public static void FitRenderer(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Vector2 size = renderer.sprite.bounds.size;
        float largest = Mathf.Max(size.x, size.y);
        if (largest <= 0.001f)
            return;

        float scale = Mathf.Max(0.01f, targetSize / largest);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    public static void FitRendererWorldSize(SpriteRenderer renderer, float targetWorldSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Transform parent = renderer.transform.parent;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        float parentMaxScale = Mathf.Max(Mathf.Abs(parentScale.x), Mathf.Abs(parentScale.y));
        if (parentMaxScale <= 0.0001f)
            parentMaxScale = 1f;

        FitRenderer(renderer, targetWorldSize / parentMaxScale);
    }

    public static Sprite CreateArrowSprite()
    {
        if (arrowSprite != null)
            return arrowSprite;

        Texture2D texture = new Texture2D(64, 64, TextureFormat.ARGB32, false);
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        for (int y = 6; y < 58; y++)
        {
            float t = y / 63f;
            float halfWidth = y < 34 ? Mathf.Lerp(6f, 24f, t / 0.54f) : Mathf.Lerp(16f, 7f, (t - 0.54f) / 0.38f);
            int minX = Mathf.RoundToInt(32f - halfWidth);
            int maxX = Mathf.RoundToInt(32f + halfWidth);
            for (int x = minX; x <= maxX; x++)
            {
                if (x >= 0 && x < texture.width)
                    texture.SetPixel(x, y, Color.white);
            }
        }

        texture.Apply();
        texture.name = "RuntimeGuidanceArrow";
        arrowSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 64f);
        return arrowSprite;
    }

    static string BuildCacheKey(string resourcePath, string editorAssetPath)
    {
        return (resourcePath ?? string.Empty) + "|" + (editorAssetPath ?? string.Empty);
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }

    static Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (candidate == null)
                continue;

            float area = candidate.rect.width * candidate.rect.height;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }
}

public static class AstroCutterBeamBlocker
{
    public static float ResolveClippedRange(RaycastHit2D[] hits, Transform ownerTransform, int ownerViewId, float maxRange)
    {
        return ResolveClippedRange(hits, hits != null ? hits.Length : 0, ownerTransform, ownerViewId, maxRange);
    }

    public static float ResolveClippedRange(RaycastHit2D[] hits, int hitCount, Transform ownerTransform, int ownerViewId, float maxRange)
    {
        float clippedRange = maxRange;
        if (hits == null)
            return clippedRange;

        int clampedHitCount = Mathf.Clamp(hitCount, 0, hits.Length);
        for (int i = 0; i < clampedHitCount; i++)
        {
            Collider2D hit = hits[i].collider;
            if (!IsBlockingHit(hit, ownerTransform, ownerViewId))
                continue;

            clippedRange = Mathf.Min(clippedRange, Mathf.Max(0.05f, hits[i].distance));
        }

        return clippedRange;
    }

    public static bool IsBlockingHit(Collider2D hit, Transform ownerTransform, int ownerViewId)
    {
        if (hit == null)
            return false;

        if (ownerTransform != null && (hit.transform == ownerTransform || hit.transform.IsChildOf(ownerTransform)))
            return false;

        PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
        if (health != null &&
            health.photonView != null &&
            !health.IsWreck &&
            health.photonView.ViewID != ownerViewId &&
            health.GetComponent<LureBeaconDecoy>() == null)
        {
            return true;
        }

        PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
        if (deployable != null && deployable.photonView != null)
            return true;

        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<MovingSpaceObject>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null ||
            hit.GetComponentInParent<ShipWreck>() != null ||
            hit.GetComponentInParent<DroppedCargoCrate>() != null)
        {
            return true;
        }

        return false;
    }
}

public sealed class AstroCutterBeamVfx : MonoBehaviour
{
    sealed class BeamEntry
    {
        public AstroCutterBeamVfx Beam;
    }

    static readonly Dictionary<int, BeamEntry> ActiveBeams = new Dictionary<int, BeamEntry>();

    Transform source;
    int sourceViewId;
    Vector2 direction;
    float range;
    bool fullWidth;
    float endTime;
    LineRenderer coreLine;
    LineRenderer glowLine;
    LineRenderer[] burnSparkLines;
    AudioSource loopAudio;
    readonly RaycastHit2D[] clippedRangeHits = new RaycastHit2D[96];

    public static void StartBeam(int sourceViewId, Vector2 beamDirection, float beamRange, float duration, bool useFullWidth)
    {
        StopBeam(sourceViewId);

        PhotonView sourceView = PhotonView.Find(sourceViewId);
        if (sourceView == null)
            return;

        GameObject beamObject = new GameObject("AstroCutterBeam_" + sourceViewId);
        AstroCutterBeamVfx beam = beamObject.AddComponent<AstroCutterBeamVfx>();
        beam.Initialize(sourceView.transform, sourceViewId, beamDirection, beamRange, duration, useFullWidth);
        ActiveBeams[sourceViewId] = new BeamEntry { Beam = beam };
    }

    public static void ResetForSessionTransition()
    {
        List<BeamEntry> active = new List<BeamEntry>(ActiveBeams.Values);
        ActiveBeams.Clear();
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i]?.Beam != null)
                Destroy(active[i].Beam.gameObject);
        }
    }

    public static void StopBeam(int sourceViewId)
    {
        if (!ActiveBeams.TryGetValue(sourceViewId, out BeamEntry entry))
            return;

        if (entry?.Beam != null)
            Destroy(entry.Beam.gameObject);

        ActiveBeams.Remove(sourceViewId);
    }

    void Initialize(Transform sourceTransform, int sourcePhotonViewId, Vector2 beamDirection, float beamRange, float duration, bool useFullWidth)
    {
        source = sourceTransform;
        sourceViewId = sourcePhotonViewId;
        direction = beamDirection.sqrMagnitude > 0.001f ? beamDirection.normalized : Vector2.up;
        range = Mathf.Max(0.2f, beamRange);
        fullWidth = useFullWidth;
        endTime = Time.time + Mathf.Max(0.1f, duration);
        float widthScale = fullWidth ? 1f : 0.5f;
        coreLine = CreateLine("Core", 0.09f * widthScale, 74);
        glowLine = CreateLine("Glow", 0.28f * widthScale, 73);
        CreateBurnSparks();
        ConfigureAudio();
        UpdateLine();
    }

    void Update()
    {
        if (source == null || Time.time >= endTime)
        {
            Destroy(gameObject);
            return;
        }

        UpdateLine();
    }

    void OnDestroy()
    {
        if (loopAudio != null)
            loopAudio.Stop();

        foreach (KeyValuePair<int, BeamEntry> pair in new List<KeyValuePair<int, BeamEntry>>(ActiveBeams))
        {
            if (pair.Value?.Beam == this)
                ActiveBeams.Remove(pair.Key);
        }
    }

    LineRenderer CreateLine(string objectName, float width, int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.positionCount = 14;
        line.widthMultiplier = width;
        line.numCapVertices = 10;
        line.numCornerVertices = 8;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.textureMode = LineTextureMode.Stretch;
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = sortingOrder;
        return line;
    }

    void UpdateLine()
    {
        Vector2 start = source.position + source.up * 0.58f;
        Vector2 safeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : (Vector2)source.up;
        float visibleRange = ResolveObstacleClippedRange(start, safeDirection, range);
        Vector2 perpendicular = new Vector2(-safeDirection.y, safeDirection.x);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
        float widthScale = fullWidth ? 1f : 0.5f;
        float jitterScale = fullWidth ? 1f : 0.5f;
        Gradient coreGradient = BuildGradient(new Color(0.95f, 0.72f, 1f, 0.95f), new Color(1f, 0.82f, 0.18f, 0.9f), 0.95f);
        Gradient glowGradient = BuildGradient(new Color(0.5f, 0.12f, 1f, 0.34f), new Color(1f, 0.74f, 0.05f, 0.28f), 0.55f + pulse * 0.25f);
        if (coreLine != null)
        {
            coreLine.colorGradient = coreGradient;
            coreLine.widthMultiplier = Mathf.Lerp(0.075f, 0.13f, pulse) * widthScale;
            SetLinePositions(coreLine, start, safeDirection, perpendicular, visibleRange, 0.045f * jitterScale);
        }

        if (glowLine != null)
        {
            glowLine.colorGradient = glowGradient;
            glowLine.widthMultiplier = Mathf.Lerp(0.22f, 0.34f, pulse) * widthScale;
            SetLinePositions(glowLine, start, safeDirection, perpendicular, visibleRange, 0.09f * jitterScale);
        }

        bool blocked = visibleRange < range - 0.08f;
        UpdateBurnSparks(blocked, start + safeDirection * visibleRange, safeDirection, perpendicular, pulse, jitterScale);

        if (loopAudio != null)
            loopAudio.transform.position = start;
    }

    float ResolveObstacleClippedRange(Vector2 start, Vector2 safeDirection, float maxRange)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };
        int hitCount = Physics2D.CircleCast(start, fullWidth ? 0.28f : 0.14f, safeDirection, filter, clippedRangeHits, Mathf.Max(0.2f, maxRange));
        return AstroCutterBeamBlocker.ResolveClippedRange(clippedRangeHits, hitCount, source, sourceViewId, maxRange);
    }

    void SetLinePositions(LineRenderer line, Vector2 start, Vector2 safeDirection, Vector2 perpendicular, float visibleRange, float jitterScale)
    {
        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (float)(line.positionCount - 1);
            float wave = Mathf.Sin(Time.time * 17f + t * Mathf.PI * 7f) * jitterScale;
            Vector2 point = start + safeDirection * (visibleRange * t) + perpendicular * wave;
            line.SetPosition(i, new Vector3(point.x, point.y, -0.42f));
        }
    }

    void CreateBurnSparks()
    {
        burnSparkLines = new LineRenderer[7];
        for (int i = 0; i < burnSparkLines.Length; i++)
        {
            GameObject sparkObject = new GameObject("BurnSpark_" + i);
            sparkObject.transform.SetParent(transform, false);
            LineRenderer spark = sparkObject.AddComponent<LineRenderer>();
            spark.useWorldSpace = true;
            spark.alignment = LineAlignment.View;
            spark.positionCount = 2;
            spark.widthMultiplier = 0.025f;
            spark.numCapVertices = 3;
            spark.numCornerVertices = 1;
            spark.material = new Material(Shader.Find("Sprites/Default"));
            spark.textureMode = LineTextureMode.Stretch;
            spark.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            spark.sortingOrder = 77 + i;
            spark.enabled = false;
            burnSparkLines[i] = spark;
        }
    }

    void UpdateBurnSparks(bool active, Vector2 endpoint, Vector2 safeDirection, Vector2 perpendicular, float pulse, float jitterScale)
    {
        if (burnSparkLines == null)
            return;

        float widthScale = fullWidth ? 1.2f : 0.85f;
        float time = Time.time;
        for (int i = 0; i < burnSparkLines.Length; i++)
        {
            LineRenderer spark = burnSparkLines[i];
            if (spark == null)
                continue;

            spark.enabled = active;
            if (!active)
                continue;

            float phase = time * (22f + i * 1.7f) + i * 1.37f;
            float flicker = 0.5f + 0.5f * Mathf.Sin(phase);
            float sideSign = i % 2 == 0 ? 1f : -1f;
            float sideAmount = sideSign * Mathf.Lerp(0.03f, 0.18f, (i % 4) / 3f) * jitterScale;
            Vector2 sparkDirection = (-safeDirection * Mathf.Lerp(0.32f, 0.78f, (i % 3) / 2f) + perpendicular * sideSign * Mathf.Lerp(0.25f, 0.75f, flicker)).normalized;
            Vector2 start = endpoint - safeDirection * 0.025f + perpendicular * sideAmount * 0.35f;
            Vector2 end = start + sparkDirection * Mathf.Lerp(0.08f, 0.32f, flicker) * widthScale;
            spark.SetPosition(0, new Vector3(start.x, start.y, -0.46f));
            spark.SetPosition(1, new Vector3(end.x, end.y, -0.46f));
            spark.widthMultiplier = Mathf.Lerp(0.012f, 0.045f, flicker) * widthScale;
            spark.startColor = i % 3 == 0
                ? new Color(1f, 0.96f, 0.48f, 0.94f)
                : new Color(1f, 0.52f, 0.08f, 0.78f);
            spark.endColor = i % 3 == 1
                ? new Color(0.62f, 0.12f, 1f, 0.02f)
                : new Color(1f, 0.12f, 0.03f, 0.02f);
        }
    }

    Gradient BuildGradient(Color a, Color b, float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(a, 0f),
                new GradientColorKey(b, 0.52f),
                new GradientColorKey(a, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(alpha, 0.08f),
                new GradientAlphaKey(alpha, 0.88f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    void ConfigureAudio()
    {
        AudioClip clip = AudioManager.Instance != null ? AudioManager.Instance.AstroCutterClip : null;
        if (clip == null)
            return;

        loopAudio = gameObject.AddComponent<AudioSource>();
        AudioManager.Instance.ConfigureSpatialSource(loopAudio, 0.62f);
        loopAudio.clip = clip;
        loopAudio.loop = true;
        loopAudio.Play();
    }
}

public sealed class GuidanceSystemOverlay : MonoBehaviourPun
{
    sealed class GuidanceArrow
    {
        public GameObject Root;
        public SpriteRenderer Renderer;
        public Color Color;
        public Func<Vector2?> TargetResolver;
    }

    const float ArrowDistance = 1.65f;
    const float GuidanceDuration = 9f;
    static Sprite arrowSprite;

    readonly List<GuidanceArrow> arrows = new List<GuidanceArrow>();
    float activeUntil;
    float nextSoundTime;

    public static GuidanceSystemOverlay EnsureFor(GameObject player)
    {
        if (player == null)
            return null;

        GuidanceSystemOverlay overlay = player.GetComponent<GuidanceSystemOverlay>();
        if (overlay == null)
            overlay = player.AddComponent<GuidanceSystemOverlay>();

        return overlay;
    }

    public void ActivateGuidance(float duration = GuidanceDuration)
    {
        if (photonView != null && !photonView.IsMine)
            return;

        activeUntil = Time.time + Mathf.Max(0.1f, duration);
        EnsureArrows();
        SetVisible(!GameplayHudVisibility.IsExtractionCinematicSuppressed);
        nextSoundTime = 0f;
    }

    void Update()
    {
        if (photonView != null && !photonView.IsMine)
            return;

        if (GameplayHudVisibility.IsExtractionCinematicSuppressed)
        {
            SetVisible(false);
            return;
        }

        if (Time.time >= activeUntil)
        {
            SetVisible(false);
            return;
        }

        EnsureArrows();
        UpdateArrows();
        if (Time.time >= nextSoundTime)
        {
            AudioManager.Instance.PlayGuidanceSystemAt(transform.position);
            float wait = AudioManager.Instance.GuidanceSystemClip != null
                ? Mathf.Clamp(AudioManager.Instance.GuidanceSystemClip.length, 0.35f, 1.35f)
                : 0.75f;
            nextSoundTime = Time.time + wait;
        }
    }

    void EnsureArrows()
    {
        if (arrows.Count > 0)
            return;

        arrowSprite ??= RuntimeSpriteUtility.CreateArrowSprite();
        arrows.Add(CreateArrow("GuidanceExtractionArrow", new Color(0.2f, 1f, 0.34f, 0.92f), ResolveClosestExtraction));
        arrows.Add(CreateArrow("GuidanceLootArrow", new Color(1f, 0.76f, 0.16f, 0.94f), ResolveMostValuableLoot));
        arrows.Add(CreateArrow("GuidanceEnemyArrow", new Color(1f, 0.18f, 0.12f, 0.94f), ResolveClosestHostile));
    }

    GuidanceArrow CreateArrow(string name, Color color, Func<Vector2?> resolver)
    {
        GameObject root = new GameObject(name);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = arrowSprite;
        renderer.color = color;
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = 92;
        root.transform.localScale = Vector3.one * 0.42f;
        return new GuidanceArrow { Root = root, Renderer = renderer, Color = color, TargetResolver = resolver };
    }

    void SetVisible(bool visible)
    {
        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i]?.Root != null)
                arrows[i].Root.SetActive(visible);
        }
    }

    void UpdateArrows()
    {
        for (int i = 0; i < arrows.Count; i++)
        {
            GuidanceArrow arrow = arrows[i];
            if (arrow == null || arrow.Root == null)
                continue;

            Vector2? target = arrow.TargetResolver?.Invoke();
            bool visible = target.HasValue;
            arrow.Root.SetActive(visible);
            if (!visible)
                continue;

            Vector2 direction = target.Value - (Vector2)transform.position;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.up;
            direction.Normalize();

            float sideOffset = (i - 1) * 0.42f;
            Vector2 tangent = new Vector2(-direction.y, direction.x);
            Vector2 position = (Vector2)transform.position + direction * ArrowDistance + tangent * sideOffset;
            arrow.Root.transform.position = new Vector3(position.x, position.y, -0.35f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            arrow.Root.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            float pulse = 0.82f + Mathf.Sin(Time.time * 6.5f + i) * 0.08f;
            arrow.Root.transform.localScale = Vector3.one * (0.42f * pulse);
        }
    }

    Vector2? ResolveClosestExtraction()
    {
        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        return ResolveClosestTransform(zones);
    }

    Vector2? ResolveMostValuableLoot()
    {
        Vector2 origin = transform.position;
        Vector2 bestPosition = Vector2.zero;
        int bestValue = -1;
        float bestDistance = float.MaxValue;

        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null)
                continue;

            ConsiderLoot(treasure.transform.position, treasure.itemId, origin, ref bestPosition, ref bestValue, ref bestDistance);
        }

        ShipWreck[] wrecks = FindObjectsByType<ShipWreck>(FindObjectsInactive.Exclude);
        for (int i = 0; i < wrecks.Length; i++)
        {
            ShipWreck wreck = wrecks[i];
            if (wreck == null || !wreck.HasLoot)
                continue;

            ConsiderLoot(wreck.transform.position, wreck.GetLootItemAt(wreck.GetFirstLootIndex()), origin, ref bestPosition, ref bestValue, ref bestDistance);
        }

        DroppedCargoCrate[] crates = FindObjectsByType<DroppedCargoCrate>(FindObjectsInactive.Exclude);
        for (int i = 0; i < crates.Length; i++)
        {
            DroppedCargoCrate crate = crates[i];
            if (crate == null || !crate.HasLoot)
                continue;

            ConsiderLoot(crate.transform.position, crate.StoredItemId, origin, ref bestPosition, ref bestValue, ref bestDistance);
        }

        return bestValue >= 0 ? bestPosition : null;
    }

    void ConsiderLoot(Vector2 position, string itemId, Vector2 origin, ref Vector2 bestPosition, ref int bestValue, ref float bestDistance)
    {
        int value = InventoryItemCatalog.GetSellValueAstrons(itemId);
        float distance = Vector2.Distance(origin, position);
        if (value < bestValue || (value == bestValue && distance >= bestDistance))
            return;

        bestValue = value;
        bestDistance = distance;
        bestPosition = position;
    }

    Vector2? ResolveClosestHostile()
    {
        Vector2 origin = transform.position;
        PlayerHealth[] healths = UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        PlayerHealth bestHuman = null;
        PlayerHealth bestBot = null;
        float bestHumanDistance = float.MaxValue;
        float bestBotDistance = float.MaxValue;

        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (candidate == null || candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.photonView == null || candidate.photonView == photonView)
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (candidate.IsBotControlled)
            {
                if (distance < bestBotDistance)
                {
                    bestBotDistance = distance;
                    bestBot = candidate;
                }
            }
            else if (distance < bestHumanDistance)
            {
                bestHumanDistance = distance;
                bestHuman = candidate;
            }
        }

        if (bestHuman != null)
            return bestHuman.transform.position;

        return bestBot != null ? bestBot.transform.position : null;
    }

    Vector2? ResolveClosestTransform(Component[] components)
    {
        if (components == null || components.Length == 0)
            return null;

        Vector2 origin = transform.position;
        Transform best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            float distance = Vector2.Distance(origin, component.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = component.transform;
        }

        return best != null ? best.position : null;
    }
}

public static class ValuableCargoCarrierUtility
{
    public static int CountCargoItem(Player player, string itemId)
    {
        if (player == null || string.IsNullOrWhiteSpace(itemId))
            return 0;

        string[] slots = PlayerProfileService.GetPlayerShipInventorySlots(player);
        int capacity = PlayerProfileService.GetPlayerShipInventoryCapacity(player);
        int count = 0;
        for (int i = 0; i < slots.Length && i < capacity; i++)
        {
            if (string.Equals(slots[i], itemId, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    public static bool TryGetTrackedCargoMarkerColor(string itemId, out Color color)
    {
        if (string.Equals(itemId, InventoryItemCatalog.PirateCaseId, StringComparison.Ordinal))
        {
            color = new Color(1f, 0.46f, 0.08f, 0.96f);
            return true;
        }

        if (string.Equals(itemId, InventoryItemCatalog.CashSuitcaseId, StringComparison.Ordinal))
        {
            color = new Color(1f, 0.86f, 0.18f, 0.96f);
            return true;
        }

        color = Color.white;
        return false;
    }

    public static PlayerHealth FindActiveHumanShipForPlayer(Player player)
    {
        if (player == null)
            return null;

        PlayerHealth[] healths = UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (!IsActiveHumanShip(candidate))
                continue;

            if (candidate.photonView != null && candidate.photonView.OwnerActorNr == player.ActorNumber)
                return candidate;
        }

        return null;
    }

    public static bool IsPirateCaseCarrier(PlayerHealth candidate)
    {
        return IsActiveHumanShip(candidate) &&
               CountCargoItem(candidate.photonView.Owner, InventoryItemCatalog.PirateCaseId) > 0;
    }

    public static PlayerHealth FindBestPirateCaseCarrier(Vector2 origin, float maxDistance, PlayerHealth observer = null)
    {
        PlayerHealth[] healths = UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        PlayerHealth best = null;
        int bestCaseCount = 0;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (!IsActiveHumanShip(candidate) || candidate == observer)
                continue;

            int caseCount = CountCargoItem(candidate.photonView.Owner, InventoryItemCatalog.PirateCaseId);
            if (caseCount <= 0)
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance > maxDistance)
                continue;

            if (best == null || caseCount > bestCaseCount || (caseCount == bestCaseCount && distance < bestDistance))
            {
                best = candidate;
                bestCaseCount = caseCount;
                bestDistance = distance;
            }
        }

        return best;
    }

    public static int FindBestPirateCaseCarrierViewId(Vector2 origin, float maxDistance, PlayerHealth observer = null)
    {
        PlayerHealth target = FindBestPirateCaseCarrier(origin, maxDistance, observer);
        return target != null && target.photonView != null ? target.photonView.ViewID : 0;
    }

    static bool IsActiveHumanShip(PlayerHealth candidate)
    {
        return candidate != null &&
               candidate.photonView != null &&
               !candidate.IsWreck &&
               !candidate.IsBotControlled &&
               !candidate.IsNeutralRiderControlled &&
               !candidate.IsAstronautControlled &&
               !candidate.IsEvacuationAnimating;
    }
}

public sealed class ValuableCargoCarrierOverlay : MonoBehaviourPun
{
    sealed class CargoMarkerTarget
    {
        public Transform Target;
        public Color Color;
    }

    sealed class CargoMarkerArrow
    {
        public GameObject Root;
        public SpriteRenderer Renderer;
    }

    const float RefreshInterval = 0.2f;
    const float ArrowDistance = 2.12f;
    const float BaseScale = 0.34f;
    static Sprite arrowSprite;

    readonly List<CargoMarkerTarget> markerTargets = new List<CargoMarkerTarget>();
    readonly List<CargoMarkerArrow> arrows = new List<CargoMarkerArrow>();
    float nextRefreshTime;

    void Update()
    {
        if (photonView != null && !photonView.IsMine)
        {
            SetAllVisible(false);
            return;
        }

        if (!PhotonNetwork.InRoom || GameplayHudVisibility.IsExtractionCinematicSuppressed)
        {
            SetAllVisible(false);
            return;
        }

        if (Time.time >= nextRefreshTime)
        {
            nextRefreshTime = Time.time + RefreshInterval;
            RefreshTargets();
        }

        UpdateArrows();
    }

    void RefreshTargets()
    {
        markerTargets.Clear();

        Player[] players = PhotonNetwork.PlayerList;
        Player localPlayer = PhotonNetwork.LocalPlayer;
        if (players == null || localPlayer == null)
            return;

        for (int i = 0; i < players.Length; i++)
        {
            Player player = players[i];
            if (player == null || player.ActorNumber == localPlayer.ActorNumber)
                continue;

            PlayerHealth carrier = ValuableCargoCarrierUtility.FindActiveHumanShipForPlayer(player);
            if (carrier == null)
                continue;

            AddCargoMarkersForPlayer(player, carrier.transform, InventoryItemCatalog.PirateCaseId);
            AddCargoMarkersForPlayer(player, carrier.transform, InventoryItemCatalog.CashSuitcaseId);
        }

        EnsureArrowCount(markerTargets.Count);
    }

    void AddCargoMarkersForPlayer(Player player, Transform target, string itemId)
    {
        int count = ValuableCargoCarrierUtility.CountCargoItem(player, itemId);
        if (count <= 0 || target == null)
            return;

        if (!ValuableCargoCarrierUtility.TryGetTrackedCargoMarkerColor(itemId, out Color color))
            return;

        for (int i = 0; i < count; i++)
            markerTargets.Add(new CargoMarkerTarget { Target = target, Color = color });
    }

    void EnsureArrowCount(int count)
    {
        arrowSprite ??= RuntimeSpriteUtility.CreateArrowSprite();
        while (arrows.Count < count)
        {
            GameObject root = new GameObject("ValuableCargoCarrierArrow_" + arrows.Count.ToString("00"));
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = arrowSprite;
            renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            renderer.sortingOrder = 94;
            root.transform.localScale = Vector3.one * BaseScale;
            root.SetActive(false);
            arrows.Add(new CargoMarkerArrow { Root = root, Renderer = renderer });
        }
    }

    void UpdateArrows()
    {
        EnsureArrowCount(markerTargets.Count);
        int targetCount = markerTargets.Count;
        Vector2 origin = transform.position;

        for (int i = 0; i < arrows.Count; i++)
        {
            CargoMarkerArrow arrow = arrows[i];
            if (arrow == null || arrow.Root == null)
                continue;

            bool visible = i < targetCount && markerTargets[i]?.Target != null;
            arrow.Root.SetActive(visible);
            if (!visible)
                continue;

            CargoMarkerTarget target = markerTargets[i];
            Vector2 direction = (Vector2)target.Target.position - origin;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.up;

            direction.Normalize();
            Vector2 tangent = new Vector2(-direction.y, direction.x);
            float sideIndex = i - (targetCount - 1) * 0.5f;
            float sideOffset = Mathf.Clamp(sideIndex * 0.28f, -1.15f, 1.15f);
            Vector2 position = origin + direction * (ArrowDistance + Mathf.Abs(sideIndex) * 0.015f) + tangent * sideOffset;

            arrow.Root.transform.position = new Vector3(position.x, position.y, -0.36f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            arrow.Root.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            float pulse = 0.88f + Mathf.Sin(Time.time * 6.2f + i * 0.73f) * 0.08f;
            arrow.Root.transform.localScale = Vector3.one * (BaseScale * pulse);

            if (arrow.Renderer != null)
                arrow.Renderer.color = target.Color;
        }
    }

    void SetAllVisible(bool visible)
    {
        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i]?.Root != null)
                arrows[i].Root.SetActive(visible);
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i]?.Root != null)
                Destroy(arrows[i].Root);
        }

        arrows.Clear();
        markerTargets.Clear();
    }
}

public static class PlayerDeployableRuntime
{
    public const string AutoTurretMarker = "player_auto_turret";
    public const string RocketAutoTurretMarker = "player_rocket_auto_turret";
    public const string WarBaseRocketAutoTurretMarker = "war_base_rocket_auto_turret";
    public const string ViperContainerHaulerMarker = "viper_container_hauler";
    public const string BisonIndustrialPartsMarker = "bison_industrial_parts";
    public const string ContainerShipAutoCannonMarker = "container_ship_auto_cannon";
    public const string SpaceBombMarker = "player_space_bomb";
    public const string StasisBuoyMarker = "player_stasis_buoy";
    public const string SpaceTorpedoMarker = "player_space_torpedo";
    public const string AsteroidBreacherBombMarker = "player_asteroid_breacher_bomb";
    public const string MetalDriftWallMarker = "player_metal_drift_wall";
    public const string SpaceDrillMarker = "player_space_drill";
    public const string DropbotMarker = "player_dropbot";

    public static void PrewarmRoundAssets()
    {
        RuntimeSpriteUtility.LoadSprite("auto_turret_top_down_resource", "Assets/auto_turret_top_down.png");
        RuntimeSpriteUtility.LoadSprite("Items/rocket_autoturret", "Assets/Resources/Items/rocket_autoturret.png");
        RuntimeSpriteUtility.LoadSprite("Items/space_bomb_gadget_bullet", "Assets/space_bomb_gadget_bullet.png");
        RuntimeSpriteUtility.LoadSprite("Items/stasis_buoy", "Assets/Resources/Items/stasis_buoy.png");
        RuntimeSpriteUtility.LoadSprite("Items/space_torpedo", "Assets/Resources/Items/space_torpedo.png");
        RuntimeSpriteUtility.LoadSprite("Items/space_torpedo_projectile", "Assets/Resources/Items/space_torpedo_projectile.png");
        RuntimeSpriteUtility.LoadSprite("Items/asteroid_breacher_bomb", "Assets/Resources/Items/asteroid_breacher_bomb.png");
        RuntimeSpriteUtility.LoadSprite("Items/metal_drift_wall", "Assets/Resources/Items/metal_drift_wall.png");
        RuntimeSpriteUtility.LoadSprite("space_drill_top_down_resource", "Assets/space_drill_top_down.png");
        RuntimeSpriteUtility.LoadSprite("space_trap_top_down_resource", "Assets/space_trap_top_down.png");
        RuntimeSpriteUtility.LoadSprite("looting_friend_top_down_resource", "Assets/looting_friend_top_down.png");
        RuntimeSpriteUtility.LoadSprite("firing_friend_up_resource", "Assets/firing_friend_up.png");
        RuntimeSpriteUtility.LoadSprite("dropbot_up_resource", "Assets/Resources/dropbot_up_resource.png");
        RuntimeSpriteUtility.LoadSprite("lure_beacon_onmap_resource", string.Empty);
        RuntimeSpriteUtility.LoadSprite("Items/escape_pod", string.Empty);
        RuntimeSpriteUtility.LoadSprite("Visuals/Bases/industrial_zone", "Assets/Resources/Visuals/Bases/industrial_zone.png");
        RuntimeSpriteUtility.PrewarmSprites(
            "Visuals/IndustrialParts/industrial_parts_01",
            "Visuals/IndustrialParts/industrial_parts_02",
            "Visuals/IndustrialParts/industrial_parts_03",
            "Visuals/IndustrialParts/industrial_parts_04",
            "Visuals/IndustrialParts/industrial_parts_05",
            "Visuals/IndustrialParts/industrial_parts_06");

        RuntimeSpriteUtility.CreateArrowSprite();
    }

    public static bool IsInstantiationData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               (marker == AutoTurretMarker || marker == RocketAutoTurretMarker || marker == WarBaseRocketAutoTurretMarker || marker == ViperContainerHaulerMarker || marker == BisonIndustrialPartsMarker || marker == ContainerShipAutoCannonMarker || marker == SpaceBombMarker || marker == StasisBuoyMarker || marker == SpaceTorpedoMarker || marker == AsteroidBreacherBombMarker || marker == MetalDriftWallMarker || marker == SpaceDrillMarker || marker == DropbotMarker);
    }

    public static bool IsContainerShipAutoCannonData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               marker == ContainerShipAutoCannonMarker;
    }

    public static bool IsComputerOwnedDeployableData(object[] data)
    {
        return IsContainerShipAutoCannonData(data) || IsWarBaseRocketAutoTurretData(data) || IsViperContainerHaulerData(data);
    }

    public static bool IsViperContainerHaulerData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               marker == ViperContainerHaulerMarker;
    }

    public static bool IsWarBaseRocketAutoTurretData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               marker == WarBaseRocketAutoTurretMarker;
    }

    public static bool IsBisonIndustrialPartsData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               marker == BisonIndustrialPartsMarker;
    }

    public static PlayerDeployableBase EnsureAttached(GameObject target)
    {
        if (target == null)
            return null;

        PhotonView view = target.GetComponent<PhotonView>();
        object[] data = view != null ? view.InstantiationData : null;
        if (data == null || data.Length == 0 || !(data[0] is string marker))
            return null;

        if (marker == AutoTurretMarker || marker == ContainerShipAutoCannonMarker)
        {
            AutoTurretDeployable turret = target.GetComponent<AutoTurretDeployable>();
            if (turret == null)
                turret = target.AddComponent<AutoTurretDeployable>();

            turret.InitializeFromPhotonData();
            return turret;
        }

        if (marker == RocketAutoTurretMarker || marker == WarBaseRocketAutoTurretMarker)
        {
            RocketAutoTurretDeployable turret = target.GetComponent<RocketAutoTurretDeployable>();
            if (turret == null)
                turret = target.AddComponent<RocketAutoTurretDeployable>();

            turret.InitializeFromPhotonData();
            return turret;
        }

        if (marker == ViperContainerHaulerMarker)
        {
            ViperContainerHaulerDeployable hauler = target.GetComponent<ViperContainerHaulerDeployable>();
            if (hauler == null)
                hauler = target.AddComponent<ViperContainerHaulerDeployable>();

            hauler.InitializeFromPhotonData();
            return hauler;
        }

        if (marker == BisonIndustrialPartsMarker)
        {
            IndustrialPartsHaulable parts = target.GetComponent<IndustrialPartsHaulable>();
            if (parts == null)
                parts = target.AddComponent<IndustrialPartsHaulable>();

            parts.InitializeFromPhotonData();
            return parts;
        }

        if (marker == SpaceBombMarker)
        {
            SpaceBombDeployable bomb = target.GetComponent<SpaceBombDeployable>();
            if (bomb == null)
                bomb = target.AddComponent<SpaceBombDeployable>();

            bomb.InitializeFromPhotonData();
            return bomb;
        }

        if (marker == StasisBuoyMarker)
        {
            StasisBuoyDeployable buoy = target.GetComponent<StasisBuoyDeployable>();
            if (buoy == null)
                buoy = target.AddComponent<StasisBuoyDeployable>();

            buoy.InitializeFromPhotonData();
            return buoy;
        }

        if (marker == SpaceTorpedoMarker)
        {
            SpaceTorpedoDeployable torpedo = target.GetComponent<SpaceTorpedoDeployable>();
            if (torpedo == null)
                torpedo = target.AddComponent<SpaceTorpedoDeployable>();

            torpedo.InitializeFromPhotonData();
            return torpedo;
        }

        if (marker == AsteroidBreacherBombMarker)
        {
            AsteroidBreacherBombDeployable bomb = target.GetComponent<AsteroidBreacherBombDeployable>();
            if (bomb == null)
                bomb = target.AddComponent<AsteroidBreacherBombDeployable>();

            bomb.InitializeFromPhotonData();
            return bomb;
        }

        if (marker == MetalDriftWallMarker)
        {
            MetalDriftWallDeployable wall = target.GetComponent<MetalDriftWallDeployable>();
            if (wall == null)
                wall = target.AddComponent<MetalDriftWallDeployable>();

            wall.InitializeFromPhotonData();
            return wall;
        }

        if (marker == SpaceDrillMarker)
        {
            SpaceDrillDeployable drill = target.GetComponent<SpaceDrillDeployable>();
            if (drill == null)
                drill = target.AddComponent<SpaceDrillDeployable>();

            drill.InitializeFromPhotonData();
            return drill;
        }

        if (marker == DropbotMarker)
        {
            DropbotDeployable dropbot = target.GetComponent<DropbotDeployable>();
            if (dropbot == null)
                dropbot = target.AddComponent<DropbotDeployable>();

            dropbot.InitializeFromPhotonData();
            return dropbot;
        }

        return null;
    }
}

public abstract class PlayerDeployableBase : MonoBehaviourPun
{
    static readonly HashSet<PlayerDeployableBase> ActiveDeployables = new HashSet<PlayerDeployableBase>();

    protected bool initialized;
    protected bool destroyed;
    protected int ownerShipViewId;
    protected int currentHp;
    protected int currentShield;
    protected SpriteRenderer spriteRenderer;
    protected Rigidbody2D body;

    public bool CanBeTargeted => initialized && !destroyed && currentHp > 0;
    public int OwnerShipViewId => ownerShipViewId;
    public bool IsComputerOwnedDeployable => PlayerDeployableRuntime.IsComputerOwnedDeployableData(photonView != null ? photonView.InstantiationData : null);
    public virtual bool CanBeTargetedByEnemyBots => CanBeTargeted && !IsComputerOwnedDeployable;
    public WeaponHitContext LastDamageContext { get; private set; }
    public float LastDamageShieldMultiplier { get; private set; } = 1f;
    public float LastDamageHpMultiplier { get; private set; } = 1f;
    public string LastDamageDebugSummary => WeaponDamageInteractionCatalog.BuildDebugSummary(LastDamageContext, LastDamageShieldMultiplier, LastDamageHpMultiplier);
    protected abstract int MaxHp { get; }
    protected abstract int MaxShield { get; }
    protected abstract float VisualTargetSize { get; }
    protected abstract float CollisionRadius { get; }
    protected abstract string SpriteResourcePath { get; }
    protected abstract string EditorSpritePath { get; }

    public static IReadOnlyCollection<PlayerDeployableBase> GetActiveDeployables()
    {
        return new List<PlayerDeployableBase>(ActiveDeployables);
    }

    protected void InitializeCommon()
    {
        if (initialized)
            return;

        initialized = true;
        currentHp = MaxHp;
        currentShield = MaxShield;
        object[] data = photonView != null ? photonView.InstantiationData : null;
        ownerShipViewId = data != null && data.Length > 1 ? ConvertToInt(data[1]) : 0;
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        ConfigureVisuals();
        ConfigurePhysics();
        IgnoreOwnerCollisions();
        DisablePlayerSpecificSystems();
        ActiveDeployables.Add(this);
    }

    protected virtual void ConfigurePhysics()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (body != null)
        {
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null)
            circle = gameObject.AddComponent<CircleCollider2D>();

        circle.isTrigger = false;
        SetWorldRadius(circle, CollisionRadius);

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
            box.enabled = false;
    }

    protected virtual void ConfigureVisuals()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            return;

        Sprite sprite = RuntimeSpriteUtility.LoadSprite(SpriteResourcePath, EditorSpritePath);
        if (sprite != null)
        {
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = Color.white;
            RuntimeSpriteUtility.FitRenderer(spriteRenderer, VisualTargetSize);
        }

        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = 40;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i] != spriteRenderer)
                renderers[i].enabled = false;
        }
    }

    [PunRPC]
    public void TakeDeployableDamageAt(int shieldDamage, int hpDamage, int attackerViewId, float impactX, float impactY)
    {
        TakeDeployableDamageAtInternal(shieldDamage, hpDamage, attackerViewId, impactX, impactY, WeaponHitContext.None);
    }

    [PunRPC]
    public void TakeDeployableDamageWithContextAt(
        int shieldDamage,
        int hpDamage,
        int attackerViewId,
        float impactX,
        float impactY,
        int damageType,
        int deliveryMethod,
        int deliveryFlags,
        string damageSource)
    {
        TakeDeployableDamageAtInternal(
            shieldDamage,
            hpDamage,
            attackerViewId,
            impactX,
            impactY,
            WeaponHitContext.FromRpc(damageType, deliveryMethod, deliveryFlags, damageSource));
    }

    void TakeDeployableDamageAtInternal(int shieldDamage, int hpDamage, int attackerViewId, float impactX, float impactY, WeaponHitContext hitContext)
    {
        if (!PhotonNetwork.IsMasterClient || !CanBeTargeted)
            return;

        if (IsComputerOwnedDeployable && attackerViewId > 0 && !EnemyBot.IsPlayerControlledDamageSource(attackerViewId))
            return;

        WeaponDamageMultipliers damageMultipliers = WeaponDamageInteractionCatalog.ResolveMultipliers(hitContext);
        RememberDamageContext(hitContext, damageMultipliers);
        int rawShieldDamage = WeaponDamageInteractionCatalog.ApplyMultiplier(Mathf.Max(0, shieldDamage), damageMultipliers.ShieldMultiplier);
        int rawHpDamage = WeaponDamageInteractionCatalog.ApplyMultiplier(Mathf.Max(0, hpDamage), damageMultipliers.HpMultiplier);
        WeaponDamageInteractionCatalog.LogDamageContext(this, "deployable-profile", hitContext, damageMultipliers, attackerViewId, rawShieldDamage, rawHpDamage);
        int absorbed = 0;
        int damage = 0;
        if (currentShield > 0)
        {
            if (rawShieldDamage > 0)
            {
                absorbed = Mathf.Min(currentShield, rawShieldDamage);
                currentShield -= absorbed;
                float overflowRatio = Mathf.Clamp01((rawShieldDamage - absorbed) / (float)rawShieldDamage);
                damage = Mathf.RoundToInt(rawHpDamage * overflowRatio);
            }
        }
        else
        {
            damage = rawHpDamage;
        }

        if (damage > 0)
            currentHp = Mathf.Max(0, currentHp - damage);

        if (absorbed <= 0 && damage <= 0)
            return;

        photonView.RPC(nameof(PlayDeployableHitRpc), RpcTarget.All, absorbed > 0, impactX, impactY);
        if (EnemyBot.IsPlayerControlledDamageSource(attackerViewId))
            OnDamageTakenByPlayer(attackerViewId);

        if (currentHp <= 0)
            DestroyOnMaster();
    }

    [PunRPC]
    public void TakeDeployableShieldOnlyDamageAt(int shieldDamage, int attackerViewId, float impactX, float impactY)
    {
        TakeDeployableShieldOnlyDamageAtInternal(shieldDamage, attackerViewId, impactX, impactY, WeaponHitContext.None);
    }

    [PunRPC]
    public void TakeDeployableShieldOnlyDamageWithContextAt(
        int shieldDamage,
        int attackerViewId,
        float impactX,
        float impactY,
        int damageType,
        int deliveryMethod,
        int deliveryFlags,
        string damageSource)
    {
        TakeDeployableShieldOnlyDamageAtInternal(
            shieldDamage,
            attackerViewId,
            impactX,
            impactY,
            WeaponHitContext.FromRpc(damageType, deliveryMethod, deliveryFlags, damageSource));
    }

    void TakeDeployableShieldOnlyDamageAtInternal(int shieldDamage, int attackerViewId, float impactX, float impactY, WeaponHitContext hitContext)
    {
        if (!PhotonNetwork.IsMasterClient || !CanBeTargeted)
            return;

        if (IsComputerOwnedDeployable && attackerViewId > 0 && !EnemyBot.IsPlayerControlledDamageSource(attackerViewId))
            return;

        WeaponDamageMultipliers damageMultipliers = WeaponDamageInteractionCatalog.ResolveMultipliers(hitContext);
        RememberDamageContext(hitContext, damageMultipliers);
        int damage = WeaponDamageInteractionCatalog.ApplyMultiplier(Mathf.Max(0, shieldDamage), damageMultipliers.ShieldMultiplier);
        WeaponDamageInteractionCatalog.LogDamageContext(this, "deployable-shield", hitContext, damageMultipliers, attackerViewId, damage, 0);
        if (damage <= 0 || currentShield <= 0)
            return;

        int absorbed = Mathf.Min(currentShield, damage);
        currentShield -= absorbed;
        photonView.RPC(nameof(PlayDeployableHitRpc), RpcTarget.All, true, impactX, impactY);
        if (EnemyBot.IsPlayerControlledDamageSource(attackerViewId))
            OnDamageTakenByPlayer(attackerViewId);
    }

    void RememberDamageContext(WeaponHitContext hitContext, WeaponDamageMultipliers damageMultipliers)
    {
        LastDamageContext = hitContext;
        LastDamageShieldMultiplier = damageMultipliers.ShieldMultiplier;
        LastDamageHpMultiplier = damageMultipliers.HpMultiplier;
    }

    public void PlayDeployableHitRpc(bool shieldHit, float x, float y)
    {
        PlayDeployableHitFeedback(shieldHit, x, y);
    }

    protected void PlayDeployableHitFeedback(bool shieldHit, float x, float y)
    {
        Vector3 point = new Vector3(x, y, transform.position.z);
        if (shieldHit)
            AudioManager.Instance.PlayShieldHitAt(point);
        else
            AudioManager.Instance.PlayHpHitAt(point);
    }

    protected void DestroyOnMaster()
    {
        if (destroyed)
            return;

        destroyed = true;
        OnDestroyedByDamage();
        photonView.RPC(nameof(PlayDeployableDestroyedRpc), RpcTarget.All);
        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    protected void DespawnOnMaster()
    {
        if (destroyed)
            return;

        destroyed = true;
        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    protected virtual void OnDestroyedByDamage()
    {
    }

    protected virtual void OnDamageTakenByPlayer(int attackerViewId)
    {
    }

    public void PlayDeployableDestroyedRpc()
    {
        PlayDeployableDestroyedFeedback();
    }

    protected void PlayDeployableDestroyedFeedback()
    {
        EnemyBot.SpawnSpaceMineDetonationEffects(transform.position, 0.9f);
    }

    void IgnoreOwnerCollisions()
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
                    Physics2D.IgnoreCollision(ownCollider, ownerCollider, true);
            }
        }
    }

    protected void DisablePlayerSpecificSystems()
    {
        DisableComponent<PlayerMovement>();
        DisableComponent<PlayerShooting>();
        DisableComponent<TreasureCollector>();
        DisableComponent<PlayerRepairDocking>();
        DisableComponent<PlayerHealth>();
        DisableComponent<HealthBarUI>();
        DisableComponent<ShieldBarUI>();
        DisableComponent<PlayerNicknameUI>();
        DisableComponent<BoosterBarUI>();
        DisableComponent<ShipInventoryHudUI>();
        DisableComponent<StartingShipEntryVfx>();
        DisableComponent<SpawnInvulnerabilityVfx>();
        DisableComponent<AstronautSurvivor>();

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            Destroy(thruster);
    }

    void DisableComponent<T>() where T : Behaviour
    {
        T component = GetComponent<T>();
        if (component != null && component != this)
            component.enabled = false;
    }

    protected void SetWorldRadius(CircleCollider2D circle, float radius)
    {
        if (circle == null)
            return;

        float scale = Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y), 0.001f);
        circle.radius = Mathf.Max(0.01f, radius / scale);
        circle.offset = Vector2.zero;
    }

    protected int ConvertToInt(object value)
    {
        if (value is int intValue)
            return intValue;

        if (value is float floatValue)
            return Mathf.RoundToInt(floatValue);

        if (value is double doubleValue)
            return Mathf.RoundToInt((float)doubleValue);

        return 0;
    }

    protected float ConvertToFloat(object value)
    {
        if (value is float floatValue)
            return floatValue;

        if (value is int intValue)
            return intValue;

        if (value is double doubleValue)
            return (float)doubleValue;

        return 0f;
    }

    protected virtual void OnDisable()
    {
        ActiveDeployables.Remove(this);
    }

    protected virtual void OnDestroy()
    {
        ActiveDeployables.Remove(this);
    }
}

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

public sealed class SpaceBombDeployable : PlayerDeployableBase
{
    const float ArmDelay = 1f;
    const float RearEjectDistance = 1.15f;
    const float FlightSpeed = 3.7f;
    const float MaxFlightLifetime = 7.5f;
    const float ExplosionRadius = 4.8f;
    const int ExplosionDamage = 170;
    const int ObstacleDamage = 260;
    const float OwnerCollisionEnablePadding = 0.05f;
    const float BombColliderWidth = 0.9f;
    const float BombColliderLength = 3.25f;
    static readonly Collider2D[] ExplosionHits = new Collider2D[160];

    Vector2 launchDirection = Vector2.up;
    Vector2 ejectStartPosition;
    Vector2 ejectEndPosition;
    float armedAt;
    float ejectStartedAt;
    float launchedAt;
    bool launched;
    bool detonationStarted;
    int dynamicZoomToken;
    TrailRenderer engineTrail;
    SpriteRenderer engineGlowRenderer;

    protected override int MaxHp => 80;
    protected override int MaxShield => 40;
    protected override float VisualTargetSize => 3.7f;
    protected override float CollisionRadius => 0.55f;
    protected override string SpriteResourcePath => "Items/space_bomb_gadget_bullet";
    protected override string EditorSpritePath => "Assets/space_bomb_gadget_bullet.png";

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
            launchDirection = new Vector2(ConvertToFloat(data[2]), ConvertToFloat(data[3]));
            if (launchDirection.sqrMagnitude <= 0.001f)
                launchDirection = Vector2.up;
            launchDirection.Normalize();
        }

        InitializeCommon();
        transform.up = launchDirection;
        ejectStartedAt = Time.time;
        armedAt = Time.time + ArmDelay;
        ejectStartPosition = transform.position;
        ejectEndPosition = ejectStartPosition - launchDirection * RearEjectDistance;
        ConfigureEngineTrail();
        SetEngineActive(false);
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
            body.mass = 0.32f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null)
            circle.enabled = false;

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider2D>();

        box.enabled = true;
        box.isTrigger = false;
        box.offset = Vector2.zero;
        SetWorldBoxSize(box, new Vector2(BombColliderWidth, BombColliderLength));
    }

    protected override void ConfigureVisuals()
    {
        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;

        Transform visualTransform = transform.Find("SpaceBombSprite");
        if (visualTransform == null)
        {
            GameObject visualObject = new GameObject("SpaceBombSprite");
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
            spriteRenderer.color = Color.white;
            RuntimeSpriteUtility.FitRenderer(spriteRenderer, VisualTargetSize);
        }

        visualTransform.localPosition = Vector3.zero;
        visualTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = 48;
    }

    void Update()
    {
        if (!initialized && PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();

        if (!initialized || detonationStarted)
            return;

        UpdateRearEjectionMotion();
        UpdateArmingVisual();
        if (launched && dynamicZoomToken > 0)
            DynamicCameraZoomController.UpdateWorldPosition(dynamicZoomToken, transform.position);

        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!launched && Time.time >= armedAt)
        {
            LaunchOnMaster();
            return;
        }

        if (launched && Time.time >= launchedAt + MaxFlightLifetime)
            DetonateOnMaster(transform.position);
    }

    void LaunchOnMaster()
    {
        if (launched || detonationStarted)
            return;

        launched = true;
        launchedAt = Time.time;
        SetOwnerCollisionIgnored(false);
        SetBombPosition(ejectEndPosition);

        if (body != null)
        {
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.linearVelocity = launchDirection * FlightSpeed;
        }

        transform.up = launchDirection;
        photonView.RPC(nameof(SetSpaceBombLaunchedRpc), RpcTarget.All, launchDirection.x, launchDirection.y, ejectEndPosition.x, ejectEndPosition.y);
    }

    [PunRPC]
    void SetSpaceBombLaunchedRpc(float directionX, float directionY, float launchX, float launchY)
    {
        launchDirection = new Vector2(directionX, directionY);
        if (launchDirection.sqrMagnitude <= 0.001f)
            launchDirection = Vector2.up;
        launchDirection.Normalize();
        SetBombPosition(new Vector2(launchX, launchY));
        transform.up = launchDirection;
        launched = true;
        launchedAt = Time.time;
        if (body != null)
        {
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.linearVelocity = launchDirection * FlightSpeed;
        }

        SetEngineActive(true);
        dynamicZoomToken = DynamicCameraZoomController.Request(DynamicCameraZoomProfiles.SpaceBombFlight, transform.position, MaxFlightLifetime + 0.4f);
        AudioManager.Instance.PlayRocketLaunchAt(transform.position);
    }

    void UpdateRearEjectionMotion()
    {
        if (launched)
            return;

        float t = Mathf.Clamp01((Time.time - ejectStartedAt) / Mathf.Max(0.001f, ArmDelay));
        float eased = Mathf.SmoothStep(0f, 1f, t);
        Vector2 position = Vector2.Lerp(ejectStartPosition, ejectEndPosition, eased);
        SetBombPosition(position);
        transform.up = launchDirection;
    }

    void SetBombPosition(Vector2 position)
    {
        if (body != null)
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
        }
        else
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!PhotonNetwork.IsMasterClient || !launched || detonationStarted || collision == null)
            return;

        Collider2D hit = collision.collider;
        if (!IsBlockingCollider(hit))
            return;

        Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
        DetonateOnMaster(point);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!PhotonNetwork.IsMasterClient || !launched || detonationStarted)
            return;

        if (IsBlockingCollider(other))
            DetonateOnMaster(transform.position);
    }

    bool IsBlockingCollider(Collider2D hit)
    {
        if (hit == null)
            return false;

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
            return false;

        if (hit.GetComponentInParent<Bullet>() != null)
            return false;

        if (!hit.isTrigger)
            return true;

        if (hit.GetComponentInParent<PlayerHealth>() != null)
            return false;

        return hit.GetComponentInParent<PlayerDeployableBase>() != null ||
               hit.GetComponentInParent<ObstacleChunk>() != null ||
               hit.GetComponentInParent<MovingSpaceObject>() != null ||
               hit.GetComponentInParent<Treasure>() != null ||
               hit.GetComponentInParent<ShipWreck>() != null ||
               hit.GetComponentInParent<DroppedCargoCrate>() != null;
    }

    static void SetWorldBoxSize(BoxCollider2D box, Vector2 worldSize)
    {
        if (box == null)
            return;

        float scaleX = Mathf.Max(Mathf.Abs(box.transform.lossyScale.x), 0.001f);
        float scaleY = Mathf.Max(Mathf.Abs(box.transform.lossyScale.y), 0.001f);
        box.size = new Vector2(
            Mathf.Max(0.01f, worldSize.x / scaleX),
            Mathf.Max(0.01f, worldSize.y / scaleY));
    }

    void DetonateOnMaster(Vector3 worldPosition)
    {
        if (!PhotonNetwork.IsMasterClient || detonationStarted)
            return;

        detonationStarted = true;
        destroyed = true;
        BroadcastDetonationEffectsAndDamage(worldPosition);
        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    protected override void OnDestroyedByDamage()
    {
        if (detonationStarted)
            return;

        detonationStarted = true;
        BroadcastDetonationEffectsAndDamage(transform.position);
    }

    void BroadcastDetonationEffectsAndDamage(Vector3 worldPosition)
    {
        SpaceObjectMotionSync.BroadcastSpaceMineDetonation(worldPosition, ExplosionRadius);
        ApplyExplosionDamage(worldPosition);
    }

    void ApplyExplosionDamage(Vector2 center)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };

        int hitCount = Physics2D.OverlapCircle(center, ExplosionRadius, filter, ExplosionHits);
        HashSet<int> damagedViews = new HashSet<int>();
        HashSet<string> damagedObstacles = new HashSet<string>(StringComparer.Ordinal);
        WeaponHitContext hitContext = new WeaponHitContext(
            WeaponDamageType.Explosive,
            WeaponDeliveryMethod.DirectProjectile,
            WeaponDeliveryFlags.AreaDamage | WeaponDeliveryFlags.Delayed,
            PlayerHealth.DamageSourceExplosive);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = ExplosionHits[i];
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable != this && deployable.photonView != null && deployable.CanBeTargeted && damagedViews.Add(deployable.photonView.ViewID))
            {
                deployable.photonView.RPC(
                    nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                    RpcTarget.MasterClient,
                    ExplosionDamage,
                    ExplosionDamage,
                    ownerShipViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
                continue;
            }

            PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
            if (health != null && health.photonView != null && !health.IsWreck && damagedViews.Add(health.photonView.ViewID))
            {
                int scaledDamage = Mathf.RoundToInt(ExplosionDamage * ResolveBossDamageMultiplier(health));
                health.photonView.RPC(
                    nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                    RpcTarget.MasterClient,
                    scaledDamage,
                    scaledDamage,
                    ownerShipViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
                continue;
            }

            ObstacleChunk obstacle = hit.GetComponentInParent<ObstacleChunk>();
            if (obstacle != null && !string.IsNullOrWhiteSpace(obstacle.StableId) && damagedObstacles.Add(obstacle.StableId) && RoomSettings.AreObstaclesDestructible())
            {
                SpaceObjectMotionSync.RequestObstacleDamage(obstacle.StableId, ObstacleDamage);
                continue;
            }

            MovingSpaceObject movingObject = hit.GetComponentInParent<MovingSpaceObject>();
            if (movingObject != null && !string.IsNullOrWhiteSpace(movingObject.StableId) && damagedObstacles.Add(movingObject.StableId))
            {
                Vector2 push = ((Vector2)movingObject.transform.position - center);
                if (push.sqrMagnitude <= 0.001f)
                    push = launchDirection;
                SpaceObjectMotionSync.RequestImpulse(movingObject.StableId, push.normalized * 5.8f);
            }
        }

        TryDamageSpecificView(ownerShipViewId, center, damagedViews, hitContext);
    }

    void TryDamageSpecificView(int targetViewId, Vector2 center, HashSet<int> damagedViews, WeaponHitContext hitContext)
    {
        if (targetViewId <= 0 || damagedViews.Contains(targetViewId))
            return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null || Vector2.Distance(center, targetView.transform.position) > ExplosionRadius + OwnerCollisionEnablePadding)
            return;

        PlayerHealth health = targetView.GetComponent<PlayerHealth>();
        if (health != null && health.photonView != null && !health.IsWreck && damagedViews.Add(health.photonView.ViewID))
            health.photonView.RPC(
                nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                RpcTarget.MasterClient,
                ExplosionDamage,
                ExplosionDamage,
                ownerShipViewId,
                center.x,
                center.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
    }

    static float ResolveBossDamageMultiplier(PlayerHealth health)
    {
        EnemyBot bot = health != null ? health.GetComponent<EnemyBot>() : null;
        if (bot == null)
            return 1f;

        switch (bot.Kind)
        {
            case EnemyBotKind.Mothership:
            case EnemyBotKind.PirateBase:
            case EnemyBotKind.SpaceManta:
            case EnemyBotKind.GravitySquid:
            case EnemyBotKind.HunterLance:
            case EnemyBotKind.CosmicWorm:
                return 2f;
            default:
                return 1f;
        }
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

    void ConfigureEngineTrail()
    {
        GameObject trailObject = new GameObject("SpaceBombEngineTrail");
        trailObject.transform.SetParent(transform, false);
        trailObject.transform.localPosition = new Vector3(0f, -0.42f, 0.05f);
        engineTrail = trailObject.AddComponent<TrailRenderer>();
        EngineTrailVisualUtility.ConfigureTrailBase(engineTrail);
        engineTrail.time = 0.62f;
        engineTrail.minVertexDistance = 0.02f;
        engineTrail.widthMultiplier = 0.16f;
        engineTrail.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        engineTrail.sortingOrder = 35;
        engineTrail.emitting = false;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.88f, 0.28f), 0f), new GradientColorKey(new Color(1f, 0.18f, 0.03f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
        engineTrail.colorGradient = gradient;

        GameObject glowObject = new GameObject("SpaceBombEngineGlow");
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = new Vector3(0f, -0.43f, -0.02f);
        glowObject.transform.localScale = Vector3.one * 0.3f;
        engineGlowRenderer = glowObject.AddComponent<SpriteRenderer>();
        engineGlowRenderer.sprite = RuntimeSpriteUtility.CreateArrowSprite();
        engineGlowRenderer.color = new Color(1f, 0.32f, 0.06f, 0.55f);
        engineGlowRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        engineGlowRenderer.sortingOrder = 47;
        engineGlowRenderer.enabled = false;
    }

    void SetEngineActive(bool active)
    {
        if (engineTrail != null)
            engineTrail.emitting = active;

        if (engineGlowRenderer != null)
            engineGlowRenderer.enabled = active;
    }

    void UpdateArmingVisual()
    {
        if (spriteRenderer == null)
            return;

        if (launched)
        {
            spriteRenderer.color = Color.white;
            if (engineGlowRenderer != null)
            {
                float pulse = 0.82f + Mathf.Sin(Time.time * 22f) * 0.18f;
                engineGlowRenderer.transform.localScale = Vector3.one * (0.3f * pulse);
            }
            return;
        }

        float t = Mathf.Clamp01(1f - ((armedAt - Time.time) / ArmDelay));
        float pulseAlpha = 0.55f + Mathf.Sin(Time.time * Mathf.Lerp(5f, 13f, t)) * 0.12f;
        spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.62f, 0.42f, 1f), Mathf.Clamp01(pulseAlpha * t));
    }

    protected override void OnDestroy()
    {
        DynamicCameraZoomController.Cancel(dynamicZoomToken);
        dynamicZoomToken = 0;
        SetEngineActive(false);
        base.OnDestroy();
    }

    [PunRPC]
    public new void PlayDeployableHitRpc(bool shieldHit, float x, float y)
    {
        PlayDeployableHitFeedback(shieldHit, x, y);
    }

    [PunRPC]
    public new void PlayDeployableDestroyedRpc()
    {
        if (!detonationStarted)
            EnemyBot.SpawnSpaceMineDetonationEffects(transform.position, ExplosionRadius);
    }
}

public sealed class StasisBuoyDeployable : PlayerDeployableBase
{
    const float Lifetime = 12.75f;
    const float PulseRadius = 4.25f;
    const float PulseInterval = 0.58f;
    const float ShockDuration = 0.85f;
    const float ShockSpeedMultiplier = 0.38f;
    const float ShockFireIntervalMultiplier = 1.75f;
    static readonly Collider2D[] PulseHits = new Collider2D[96];

    float deployedAt;
    float nextPulseTime;
    LineRenderer outerRing;
    LineRenderer innerRing;

    protected override int MaxHp => 55;
    protected override int MaxShield => 25;
    protected override float VisualTargetSize => 0.88f;
    protected override float CollisionRadius => 0.42f;
    protected override string SpriteResourcePath => "Items/stasis_buoy";
    protected override string EditorSpritePath => "Assets/Resources/Items/stasis_buoy.png";
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

        InitializeCommon();
        deployedAt = Time.time;
        nextPulseTime = Time.time + 0.12f;
        ConfigurePulseVisuals();
        AudioManager.Instance.PlayStasisBuoyAt(transform.position);
    }

    void Update()
    {
        if (!initialized && PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();

        if (!initialized || destroyed)
            return;

        UpdatePulseVisuals();
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (Time.time >= deployedAt + Lifetime)
        {
            DespawnOnMaster();
            return;
        }

        if (Time.time >= nextPulseTime)
        {
            nextPulseTime = Time.time + PulseInterval;
            PulseOnMaster();
        }
    }

    void PulseOnMaster()
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };

        int hitCount = Physics2D.OverlapCircle(transform.position, PulseRadius, filter, PulseHits);
        HashSet<int> shockedViews = new HashSet<int>();
        int ownerActorNumber = ResolveOwnerActorNumber();
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = PulseHits[i];
            PlayerHealth health = hit != null ? hit.GetComponentInParent<PlayerHealth>() : null;
            if (!IsValidShockTarget(health, ownerActorNumber))
                continue;

            if (!shockedViews.Add(health.photonView.ViewID))
                continue;

            health.photonView.RPC(
                nameof(PlayerHealth.ApplyElectromagneticShockRpc),
                RpcTarget.All,
                ShockDuration,
                ShockSpeedMultiplier,
                ShockFireIntervalMultiplier);
        }

        photonView.RPC(nameof(PlayStasisPulseRpc), RpcTarget.All);
    }

    bool IsValidShockTarget(PlayerHealth health, int ownerActorNumber)
    {
        if (health == null ||
            health.photonView == null ||
            health.IsWreck ||
            health.IsEvacuationAnimating ||
            health.GetComponent<LureBeaconDecoy>() != null ||
            health.GetComponent<PlayerDeployableBase>() != null)
        {
            return false;
        }

        if (health.photonView.ViewID == ownerShipViewId)
            return false;

        bool computerTarget = health.IsBotControlled || health.IsNeutralRiderControlled || health.IsEnemyAstronautControlled;
        return computerTarget || ownerActorNumber <= 0 || health.photonView.OwnerActorNr != ownerActorNumber;
    }

    int ResolveOwnerActorNumber()
    {
        PhotonView ownerView = ownerShipViewId > 0 ? PhotonView.Find(ownerShipViewId) : null;
        if (ownerView == null)
            return 0;

        if (ownerView.Owner != null && ownerView.Owner.ActorNumber > 0)
            return ownerView.Owner.ActorNumber;

        return Mathf.Max(0, ownerView.OwnerActorNr);
    }

    [PunRPC]
    void PlayStasisPulseRpc()
    {
        AudioManager.Instance.PlayStasisPulseAt(transform.position);
    }

    void ConfigurePulseVisuals()
    {
        outerRing = CreateRing("StasisOuterRing", 0.055f, 42);
        innerRing = CreateRing("StasisInnerRing", 0.025f, 43);
        UpdatePulseVisuals();
    }

    LineRenderer CreateRing(string objectName, float width, int sortingOrder)
    {
        GameObject ringObject = new GameObject(objectName);
        ringObject.transform.SetParent(transform, false);
        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 72;
        ring.widthMultiplier = width;
        ring.numCapVertices = 4;
        ring.numCornerVertices = 4;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        ring.sortingOrder = sortingOrder;
        return ring;
    }

    void UpdatePulseVisuals()
    {
        float phase = Mathf.Repeat((Time.time - deployedAt) / PulseInterval, 1f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10.5f);
        SetRing(outerRing, Mathf.Lerp(0.78f, PulseRadius, phase), new Color(0.32f, 0.95f, 1f, Mathf.Lerp(0.28f, 0.05f, phase)));
        SetRing(innerRing, Mathf.Lerp(0.48f, 1.12f, pulse), new Color(0.92f, 0.98f, 1f, 0.44f + pulse * 0.18f));

        if (spriteRenderer != null)
            spriteRenderer.color = Color.Lerp(Color.white, new Color(0.48f, 0.92f, 1f, 1f), pulse * 0.35f);
    }

    void SetRing(LineRenderer ring, float radius, Color color)
    {
        if (ring == null)
            return;

        ring.startColor = color;
        ring.endColor = new Color(color.r, color.g, color.b, color.a * 0.25f);
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i / (float)ring.positionCount * Mathf.PI * 2f;
            Vector2 point = (Vector2)transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            ring.SetPosition(i, new Vector3(point.x, point.y, -0.41f));
        }
    }

    protected override void OnDestroyedByDamage()
    {
        SpaceObjectMotionSync.BroadcastSpaceMineDetonation(transform.position, 1.45f);
    }

    [PunRPC]
    public new void PlayDeployableHitRpc(bool shieldHit, float x, float y)
    {
        PlayDeployableHitFeedback(shieldHit, x, y);
    }

    [PunRPC]
    public new void PlayDeployableDestroyedRpc()
    {
        EnemyBot.SpawnSpaceMineDetonationEffects(transform.position, 1.45f);
    }
}

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

public sealed class AsteroidBreacherBombDeployable : PlayerDeployableBase
{
    const float FlightSpeed = 11.5f;
    const float ArrivalDistance = 0.32f;
    const float CountdownDuration = 3f;
    const float MaxFlightLifetime = 4f;
    const float ExplosionRadius = 1.65f;
    const int ObstacleDamage = 10000;

    string targetStableId = string.Empty;
    Vector2 targetPosition;
    float launchedAt;
    float armedAt;
    float nextBeepTime;
    bool armed;
    bool detonationStarted;
    bool hasAttachmentOffset;
    Vector2 attachedLocalOffset;
    float attachedLocalRotationOffset;
    LineRenderer countdownRing;
    TrailRenderer flightTrail;

    protected override int MaxHp => 25;
    protected override int MaxShield => 0;
    protected override float VisualTargetSize => 0.62f;
    protected override float CollisionRadius => 0.22f;
    protected override string SpriteResourcePath => "Items/asteroid_breacher_bomb";
    protected override string EditorSpritePath => "Assets/Resources/Items/asteroid_breacher_bomb.png";

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
        targetStableId = data != null && data.Length > 2 ? data[2] as string ?? string.Empty : string.Empty;
        targetPosition = data != null && data.Length > 4
            ? new Vector2(ConvertToFloat(data[3]), ConvertToFloat(data[4]))
            : (Vector2)transform.position;

        InitializeCommon();
        launchedAt = Time.time;
        ConfigureFlightTrail();
        ConfigureCountdownRing();
        AudioManager.Instance.PlayRocketLaunchAt(transform.position);
    }

    protected override void ConfigurePhysics()
    {
        base.ConfigurePhysics();

        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null)
            circle.isTrigger = true;
    }

    void Update()
    {
        if (!initialized && PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();

        if (!initialized || detonationStarted)
            return;

        if (!armed)
        {
            ResolveLiveTargetPosition();
            MoveTowardTarget();
            UpdateFlightVisuals();

            if (PhotonNetwork.IsMasterClient &&
                (Vector2.Distance(transform.position, targetPosition) <= ArrivalDistance || Time.time >= launchedAt + MaxFlightLifetime))
            {
                ArmOnMaster();
            }

            return;
        }

        UpdateAttachedPosition();
        UpdateCountdownVisuals();
        TryPlayCountdownBeep();

        if (PhotonNetwork.IsMasterClient && Time.time >= armedAt + CountdownDuration)
            DetonateOnMaster(transform.position);
    }

    void ResolveLiveTargetPosition()
    {
        if (string.IsNullOrWhiteSpace(targetStableId))
            return;

        ObstacleChunk obstacle = FindTargetObstacle();
        if (obstacle != null)
            targetPosition = obstacle.transform.position;
    }

    ObstacleChunk FindTargetObstacle()
    {
        if (string.IsNullOrWhiteSpace(targetStableId))
            return null;

        return ObstacleChunk.Find(targetStableId);
    }

    void MoveTowardTarget()
    {
        Vector2 currentPosition = transform.position;
        Vector2 toTarget = targetPosition - currentPosition;
        if (toTarget.sqrMagnitude > 0.001f)
            transform.up = toTarget.normalized;

        Vector2 nextPosition = Vector2.MoveTowards(currentPosition, targetPosition, FlightSpeed * Time.deltaTime);
        if (body != null)
            body.MovePosition(nextPosition);
        else
            transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);
    }

    void ArmOnMaster()
    {
        if (!PhotonNetwork.IsMasterClient || armed || detonationStarted)
            return;

        Vector2 armPosition = transform.position;
        Vector2 localOffset = Vector2.zero;
        float localRotationOffset = 0f;
        int attachFlag = 0;

        ObstacleChunk obstacle = FindTargetObstacle();
        if (obstacle != null)
        {
            attachFlag = 1;
            localOffset = obstacle.transform.InverseTransformPoint(armPosition);
            localRotationOffset = Mathf.DeltaAngle(obstacle.transform.eulerAngles.z, transform.eulerAngles.z);
        }

        photonView.RPC(
            nameof(SetBreacherArmedRpc),
            RpcTarget.All,
            armPosition.x,
            armPosition.y,
            localOffset.x,
            localOffset.y,
            localRotationOffset,
            attachFlag);
    }

    [PunRPC]
    void SetBreacherArmedRpc(float x, float y, float localOffsetX, float localOffsetY, float localRotationOffset, int attachFlag)
    {
        targetPosition = new Vector2(x, y);
        attachedLocalOffset = new Vector2(localOffsetX, localOffsetY);
        attachedLocalRotationOffset = localRotationOffset;
        hasAttachmentOffset = attachFlag != 0;
        armed = true;
        armedAt = Time.time;
        nextBeepTime = Time.time;

        if (UpdateAttachedPosition())
        {
            if (flightTrail != null)
                flightTrail.emitting = false;
            return;
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.MovePosition(targetPosition);
        }
        else
        {
            transform.position = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
        }

        if (flightTrail != null)
            flightTrail.emitting = false;
    }

    bool UpdateAttachedPosition()
    {
        if (!hasAttachmentOffset)
            return false;

        ObstacleChunk obstacle = FindTargetObstacle();
        if (obstacle == null)
            return false;

        Vector3 worldPosition = obstacle.transform.TransformPoint(attachedLocalOffset);
        targetPosition = worldPosition;
        transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, obstacle.transform.eulerAngles.z + attachedLocalRotationOffset);

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = targetPosition;
            body.rotation = transform.eulerAngles.z;
        }

        return true;
    }

    void TryPlayCountdownBeep()
    {
        if (Time.time < nextBeepTime)
            return;

        nextBeepTime = Time.time + 0.5f;
        AudioManager.Instance.PlayRocketLockAt(transform.position);
    }

    void DetonateOnMaster(Vector3 worldPosition)
    {
        if (!PhotonNetwork.IsMasterClient || detonationStarted)
            return;

        detonationStarted = true;
        destroyed = true;
        BroadcastDetonationEffectsAndDamage(worldPosition);
        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    protected override void OnDestroyedByDamage()
    {
        if (detonationStarted)
            return;

        detonationStarted = true;
        BroadcastDetonationEffectsAndDamage(transform.position);
    }

    void BroadcastDetonationEffectsAndDamage(Vector3 worldPosition)
    {
        photonView.RPC(nameof(PlayBreacherExplosionRpc), RpcTarget.All, worldPosition.x, worldPosition.y);
        SpaceObjectMotionSync.BroadcastSpaceMineDetonation(worldPosition, ExplosionRadius);

        ObstacleChunk targetObstacle = FindTargetObstacle();
        if (targetObstacle != null && !string.IsNullOrWhiteSpace(targetObstacle.StableId) && RoomSettings.AreObstaclesDestructible())
            SpaceObjectMotionSync.RequestObstacleDamage(targetObstacle.StableId, ObstacleDamage);

        NewItemsRuntime.ApplyBreacherExplosionDamage(worldPosition, ownerShipViewId);
    }

    [PunRPC]
    void PlayBreacherExplosionRpc(float x, float y)
    {
        AudioManager.Instance.PlayAsteroidBreacherAt(new Vector3(x, y, transform.position.z));
    }

    void ConfigureFlightTrail()
    {
        GameObject trailObject = new GameObject("AsteroidBreacherFuseTrail");
        trailObject.transform.SetParent(transform, false);
        trailObject.transform.localPosition = new Vector3(0f, -0.18f, 0.04f);
        flightTrail = trailObject.AddComponent<TrailRenderer>();
        EngineTrailVisualUtility.ConfigureTrailBase(flightTrail);
        flightTrail.time = 0.28f;
        flightTrail.minVertexDistance = 0.02f;
        flightTrail.widthMultiplier = 0.075f;
        flightTrail.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        flightTrail.sortingOrder = 36;
        flightTrail.emitting = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.55f, 0.14f), 0f), new GradientColorKey(new Color(0.38f, 0.78f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.88f, 0f), new GradientAlphaKey(0f, 1f) });
        flightTrail.colorGradient = gradient;
    }

    void ConfigureCountdownRing()
    {
        GameObject ringObject = new GameObject("AsteroidBreacherCountdownRing");
        ringObject.transform.SetParent(transform, false);
        countdownRing = ringObject.AddComponent<LineRenderer>();
        countdownRing.useWorldSpace = true;
        countdownRing.loop = true;
        countdownRing.positionCount = 72;
        countdownRing.widthMultiplier = 0.035f;
        countdownRing.numCapVertices = 3;
        countdownRing.numCornerVertices = 3;
        countdownRing.material = new Material(Shader.Find("Sprites/Default"));
        countdownRing.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        countdownRing.sortingOrder = 42;
        countdownRing.enabled = false;
    }

    void UpdateFlightVisuals()
    {
        if (spriteRenderer == null)
            return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 16f);
        spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.58f, 0.22f, 1f), pulse * 0.22f);
    }

    void UpdateCountdownVisuals()
    {
        float t = Mathf.Clamp01((Time.time - armedAt) / CountdownDuration);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
        if (spriteRenderer != null)
            spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.22f, 0.08f, 1f), 0.25f + pulse * 0.45f);

        if (countdownRing == null)
            return;

        countdownRing.enabled = true;
        float radius = Mathf.Lerp(0.58f, ExplosionRadius, pulse * 0.3f + t * 0.7f);
        Color color = Color.Lerp(new Color(1f, 0.72f, 0.18f, 0.42f), new Color(1f, 0.08f, 0.02f, 0.78f), t);
        countdownRing.startColor = color;
        countdownRing.endColor = new Color(color.r, color.g, color.b, color.a * 0.32f);
        for (int i = 0; i < countdownRing.positionCount; i++)
        {
            float angle = i / (float)countdownRing.positionCount * Mathf.PI * 2f;
            Vector2 point = (Vector2)transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            countdownRing.SetPosition(i, new Vector3(point.x, point.y, -0.39f));
        }
    }

    protected override void OnDestroy()
    {
        if (flightTrail != null)
            flightTrail.emitting = false;

        base.OnDestroy();
    }

    [PunRPC]
    public new void PlayDeployableHitRpc(bool shieldHit, float x, float y)
    {
        PlayDeployableHitFeedback(shieldHit, x, y);
    }

    [PunRPC]
    public new void PlayDeployableDestroyedRpc()
    {
        if (!detonationStarted)
            EnemyBot.SpawnSpaceMineDetonationEffects(transform.position, 0.9f);
    }
}

public sealed class SpaceTorpedoDeployable : PlayerDeployableBase
{
    const float FlightSpeed = 9.4f;
    const float MaxFlightLifetime = 4.1f;
    const float ExplosionRange = 2.35f;
    const float ExplosionCoreRadius = 0.42f;
    const float ExplosionHalfAngleDegrees = 34f;
    const int ExplosionDamage = 82;
    const int ObstacleDamage = 115;
    static readonly Collider2D[] ExplosionHits = new Collider2D[128];

    Vector2 launchDirection = Vector2.up;
    float launchedAt;
    bool detonationStarted;
    TrailRenderer engineTrail;
    SpriteRenderer noseGlowRenderer;

    protected override int MaxHp => 30;
    protected override int MaxShield => 10;
    protected override float VisualTargetSize => 0.64f;
    protected override float CollisionRadius => 0.2f;
    protected override string SpriteResourcePath => "Items/space_torpedo_projectile";
    protected override string EditorSpritePath => "Assets/Resources/Items/space_torpedo_projectile.png";

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
            launchDirection = new Vector2(ConvertToFloat(data[2]), ConvertToFloat(data[3]));
            if (launchDirection.sqrMagnitude <= 0.001f)
                launchDirection = Vector2.up;
            launchDirection.Normalize();
        }

        InitializeCommon();
        transform.up = launchDirection;
        launchedAt = Time.time;
        ConfigureEngineTrail();
        if (body != null)
            body.linearVelocity = launchDirection * FlightSpeed;

        AudioManager.Instance.PlayRocketLaunchAt(transform.position);
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
            body.mass = 0.18f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null)
            circle = gameObject.AddComponent<CircleCollider2D>();

        circle.enabled = true;
        circle.isTrigger = false;
        SetWorldRadius(circle, CollisionRadius);

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
            box.enabled = false;
    }

    void Update()
    {
        if (!initialized && PlayerDeployableRuntime.IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();

        if (!initialized || detonationStarted)
            return;

        transform.up = launchDirection;
        UpdateEngineVisuals();
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (body != null)
            body.linearVelocity = launchDirection * FlightSpeed;

        if (Time.time >= launchedAt + MaxFlightLifetime)
            DetonateOnMaster(transform.position);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!PhotonNetwork.IsMasterClient || detonationStarted || collision == null)
            return;

        Collider2D hit = collision.collider;
        if (!IsBlockingCollider(hit))
            return;

        Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
        DetonateOnMaster(point);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!PhotonNetwork.IsMasterClient || detonationStarted)
            return;

        if (IsBlockingCollider(other))
            DetonateOnMaster(transform.position);
    }

    bool IsBlockingCollider(Collider2D hit)
    {
        if (hit == null)
            return false;

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
            return false;

        if (hit.GetComponentInParent<Bullet>() != null)
            return false;

        PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
        if (health != null && health.photonView != null && health.photonView.ViewID == ownerShipViewId)
            return false;

        if (!hit.isTrigger)
            return true;

        return health != null ||
               hit.GetComponentInParent<PlayerDeployableBase>() != null ||
               hit.GetComponentInParent<ObstacleChunk>() != null ||
               hit.GetComponentInParent<MovingSpaceObject>() != null ||
               hit.GetComponentInParent<Treasure>() != null ||
               hit.GetComponentInParent<ShipWreck>() != null ||
               hit.GetComponentInParent<DroppedCargoCrate>() != null;
    }

    void DetonateOnMaster(Vector3 worldPosition)
    {
        if (!PhotonNetwork.IsMasterClient || detonationStarted)
            return;

        detonationStarted = true;
        destroyed = true;
        BroadcastDetonationEffectsAndDamage(worldPosition);
        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    protected override void OnDestroyedByDamage()
    {
        if (detonationStarted)
            return;

        detonationStarted = true;
        BroadcastDetonationEffectsAndDamage(transform.position);
    }

    void BroadcastDetonationEffectsAndDamage(Vector3 worldPosition)
    {
        photonView.RPC(
            nameof(PlaySpaceTorpedoConeExplosionRpc),
            RpcTarget.All,
            worldPosition.x,
            worldPosition.y,
            launchDirection.x,
            launchDirection.y);
        ApplyExplosionDamage(worldPosition);
    }

    [PunRPC]
    void PlaySpaceTorpedoConeExplosionRpc(float x, float y, float directionX, float directionY)
    {
        Vector2 direction = new Vector2(directionX, directionY);
        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.up;
        direction.Normalize();

        Vector3 worldPosition = new Vector3(x, y, transform.position.z);
        SpaceTorpedoConeExplosionVfx.Spawn(worldPosition, direction, ExplosionRange, ExplosionHalfAngleDegrees);
        AudioManager.Instance.PlaySpaceTorpedoExplosionAt(worldPosition);
    }

    void ApplyExplosionDamage(Vector2 center)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };

        int hitCount = Physics2D.OverlapCircle(center, ExplosionRange, filter, ExplosionHits);
        HashSet<int> damagedViews = new HashSet<int>();
        HashSet<string> damagedObstacles = new HashSet<string>(StringComparer.Ordinal);
        WeaponHitContext hitContext = new WeaponHitContext(
            WeaponDamageType.Explosive,
            WeaponDeliveryMethod.DirectProjectile,
            WeaponDeliveryFlags.AreaDamage,
            PlayerHealth.DamageSourceExplosive);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = ExplosionHits[i];
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            if (!IsInsideExplosionCone(hit, center))
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable != this && deployable.photonView != null && deployable.CanBeTargeted && damagedViews.Add(deployable.photonView.ViewID))
            {
                deployable.photonView.RPC(
                    nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                    RpcTarget.MasterClient,
                    ExplosionDamage,
                    ExplosionDamage,
                    ownerShipViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
                continue;
            }

            PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
            if (health != null && health.photonView != null && !health.IsWreck && health.photonView.ViewID != ownerShipViewId && damagedViews.Add(health.photonView.ViewID))
            {
                health.photonView.RPC(
                    nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                    RpcTarget.MasterClient,
                    ExplosionDamage,
                    ExplosionDamage,
                    ownerShipViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
                continue;
            }

            ObstacleChunk obstacle = hit.GetComponentInParent<ObstacleChunk>();
            if (obstacle != null && !string.IsNullOrWhiteSpace(obstacle.StableId) && damagedObstacles.Add(obstacle.StableId) && RoomSettings.AreObstaclesDestructible())
            {
                SpaceObjectMotionSync.RequestObstacleDamage(obstacle.StableId, ObstacleDamage);
                continue;
            }

            MovingSpaceObject movingObject = hit.GetComponentInParent<MovingSpaceObject>();
            if (movingObject != null && !string.IsNullOrWhiteSpace(movingObject.StableId) && damagedObstacles.Add(movingObject.StableId))
            {
                Vector2 push = launchDirection;
                if (push.sqrMagnitude <= 0.001f)
                    push = Vector2.up;
                SpaceObjectMotionSync.RequestImpulse(movingObject.StableId, push.normalized * 4.3f);
            }
        }
    }

    bool IsInsideExplosionCone(Collider2D hit, Vector2 center)
    {
        Vector2 closest = hit.ClosestPoint(center);
        Vector2 toHit = closest - center;
        float sqrDistance = toHit.sqrMagnitude;
        if (sqrDistance <= ExplosionCoreRadius * ExplosionCoreRadius)
            return true;

        if (sqrDistance > ExplosionRange * ExplosionRange)
            return false;

        Vector2 direction = launchDirection.sqrMagnitude > 0.001f ? launchDirection.normalized : Vector2.up;
        float dot = Vector2.Dot(direction, toHit.normalized);
        return dot >= Mathf.Cos(ExplosionHalfAngleDegrees * Mathf.Deg2Rad);
    }

    void ConfigureEngineTrail()
    {
        GameObject trailObject = new GameObject("SpaceTorpedoTrail");
        trailObject.transform.SetParent(transform, false);
        trailObject.transform.localPosition = new Vector3(0f, -0.24f, 0.05f);
        engineTrail = trailObject.AddComponent<TrailRenderer>();
        EngineTrailVisualUtility.ConfigureTrailBase(engineTrail);
        engineTrail.time = 0.34f;
        engineTrail.minVertexDistance = 0.015f;
        engineTrail.widthMultiplier = 0.08f;
        engineTrail.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        engineTrail.sortingOrder = 36;
        engineTrail.emitting = true;

        Gradient trailGradient = new Gradient();
        trailGradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.45f, 0.96f, 1f), 0f), new GradientColorKey(new Color(1f, 0.22f, 0.06f), 1f) },
            new[] { new GradientAlphaKey(0.92f, 0f), new GradientAlphaKey(0f, 1f) });
        engineTrail.colorGradient = trailGradient;

        GameObject glowObject = new GameObject("SpaceTorpedoNoseGlow");
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = new Vector3(0f, 0.34f, -0.02f);
        glowObject.transform.localScale = Vector3.one * 0.16f;
        noseGlowRenderer = glowObject.AddComponent<SpriteRenderer>();
        noseGlowRenderer.sprite = RuntimeSpriteUtility.CreateArrowSprite();
        noseGlowRenderer.color = new Color(0.42f, 0.96f, 1f, 0.52f);
        noseGlowRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        noseGlowRenderer.sortingOrder = 50;
    }

    void UpdateEngineVisuals()
    {
        if (noseGlowRenderer == null)
            return;

        float pulse = 0.82f + Mathf.Sin(Time.time * 19f) * 0.18f;
        noseGlowRenderer.transform.localScale = Vector3.one * (0.16f * pulse);
    }

    protected override void OnDestroy()
    {
        if (engineTrail != null)
            engineTrail.emitting = false;

        base.OnDestroy();
    }

    [PunRPC]
    public new void PlayDeployableHitRpc(bool shieldHit, float x, float y)
    {
        PlayDeployableHitFeedback(shieldHit, x, y);
    }

    [PunRPC]
    public new void PlayDeployableDestroyedRpc()
    {
        if (!detonationStarted)
            SpaceTorpedoConeExplosionVfx.Spawn(transform.position, launchDirection, ExplosionRange, ExplosionHalfAngleDegrees);
    }
}

public sealed class SpaceTorpedoConeExplosionVfx : MonoBehaviour
{
    const float Lifetime = 0.68f;
    const float EffectZ = -0.36f;
    const int RayCount = 11;
    const int ArcSegments = 18;

    static Material sharedMaterial;

    readonly LineRenderer[] rays = new LineRenderer[RayCount];
    LineRenderer frontArc;
    LineRenderer coreJet;
    Vector3 center;
    Vector2 direction = Vector2.up;
    float range;
    float halfAngle;
    float age;

    public static void Spawn(Vector3 position, Vector2 forward, float coneRange, float coneHalfAngleDegrees)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        GameObject effectObject = new GameObject("SpaceTorpedoConeExplosionVfx");
        SpaceTorpedoConeExplosionVfx effect = effectObject.AddComponent<SpaceTorpedoConeExplosionVfx>();
        effect.Initialize(position, forward, coneRange, coneHalfAngleDegrees);
    }

    void Initialize(Vector3 position, Vector2 forward, float coneRange, float coneHalfAngleDegrees)
    {
        center = new Vector3(position.x, position.y, EffectZ);
        transform.position = center;
        direction = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector2.up;
        range = Mathf.Max(0.5f, coneRange);
        halfAngle = Mathf.Clamp(coneHalfAngleDegrees, 8f, 85f);
        age = 0f;

        for (int i = 0; i < rays.Length; i++)
            rays[i] = CreateLine("TorpedoConeRay_" + i.ToString("00"), 0.055f, false, 2, 6900 + i);

        frontArc = CreateLine("TorpedoConeFrontArc", 0.075f, false, ArcSegments, 6920);
        coreJet = CreateLine("TorpedoConeCoreJet", 0.18f, false, 2, 6925);
        UpdateVisuals(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Lifetime);
        UpdateVisuals(t);

        if (age >= Lifetime)
            Destroy(gameObject);
    }

    void UpdateVisuals(float t)
    {
        float expansion = 1f - Mathf.Pow(1f - t, 2.2f);
        float fade = Mathf.Pow(1f - t, 1.35f);
        Vector3 origin = center + (Vector3)(direction * Mathf.Lerp(0.04f, 0.18f, expansion));

        for (int i = 0; i < rays.Length; i++)
        {
            LineRenderer ray = rays[i];
            if (ray == null)
                continue;

            float normalized = rays.Length <= 1 ? 0.5f : i / (float)(rays.Length - 1);
            float angle = Mathf.Lerp(-halfAngle, halfAngle, normalized) + Mathf.Sin(i * 7.13f) * 2.5f;
            Vector2 rayDirection = Rotate(direction, angle);
            float length = range * Mathf.Lerp(0.72f, 1.08f, Hash01(i, 3)) * expansion;
            Vector3 end = center + (Vector3)(rayDirection * length);
            ray.SetPosition(0, origin);
            ray.SetPosition(1, end);

            Color start = new Color(1f, 0.88f, 0.42f, 0.88f * fade);
            Color finish = new Color(1f, 0.18f, 0.02f, 0f);
            ray.startColor = start;
            ray.endColor = finish;
            ray.widthMultiplier = Mathf.Lerp(0.08f, 0.012f, t) * Mathf.Lerp(0.78f, 1.22f, Hash01(i, 9));
        }

        UpdateFrontArc(expansion, fade);
        UpdateCoreJet(expansion, fade);
    }

    void UpdateFrontArc(float expansion, float fade)
    {
        if (frontArc == null)
            return;

        float currentRange = range * expansion;
        for (int i = 0; i < frontArc.positionCount; i++)
        {
            float normalized = frontArc.positionCount <= 1 ? 0.5f : i / (float)(frontArc.positionCount - 1);
            float angle = Mathf.Lerp(-halfAngle, halfAngle, normalized);
            Vector2 arcDirection = Rotate(direction, angle);
            Vector3 point = center + (Vector3)(arcDirection * currentRange);
            frontArc.SetPosition(i, point);
        }

        Color arcColor = new Color(0.52f, 0.96f, 1f, 0.55f * fade);
        frontArc.startColor = arcColor;
        frontArc.endColor = new Color(1f, 0.35f, 0.08f, 0.2f * fade);
        frontArc.widthMultiplier = Mathf.Lerp(0.12f, 0.018f, 1f - fade);
    }

    void UpdateCoreJet(float expansion, float fade)
    {
        if (coreJet == null)
            return;

        Vector3 start = center - (Vector3)(direction * 0.08f);
        Vector3 end = center + (Vector3)(direction * range * Mathf.Lerp(0.42f, 0.92f, expansion));
        coreJet.SetPosition(0, start);
        coreJet.SetPosition(1, end);
        coreJet.startColor = new Color(1f, 0.96f, 0.72f, 0.92f * fade);
        coreJet.endColor = new Color(0.4f, 0.86f, 1f, 0f);
        coreJet.widthMultiplier = Mathf.Lerp(0.22f, 0.035f, 1f - fade);
    }

    LineRenderer CreateLine(string objectName, float width, bool loop, int positionCount, int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = Mathf.Max(2, positionCount);
        line.widthMultiplier = width;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 5;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetMaterial();
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = sortingOrder;
        return line;
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
            name = "SpaceTorpedoConeExplosionVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 5000;
        return sharedMaterial;
    }

    static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }

    static float Hash01(int index, int salt)
    {
        return Mathf.Repeat(Mathf.Sin(index * 127.1f + salt * 311.7f) * 43758.5453f, 1f);
    }
}

public sealed class LootHookSnatchVfx : MonoBehaviour
{
    Transform source;
    Transform target;
    float endsAt;
    LineRenderer glowLine;
    LineRenderer coreLine;

    public static void Spawn(Transform sourceTransform, Transform targetTransform)
    {
        if (sourceTransform == null || targetTransform == null)
            return;

        GameObject effectObject = new GameObject("LootHookSnatchVfx");
        LootHookSnatchVfx effect = effectObject.AddComponent<LootHookSnatchVfx>();
        effect.Initialize(sourceTransform, targetTransform);
    }

    void Initialize(Transform sourceTransform, Transform targetTransform)
    {
        source = sourceTransform;
        target = targetTransform;
        endsAt = Time.time + 0.58f;
        glowLine = CreateLine("Glow", 0.19f, 76, new Color(1f, 0.68f, 0.12f, 0.34f));
        coreLine = CreateLine("Core", 0.045f, 77, new Color(1f, 0.96f, 0.58f, 0.92f));
        UpdateLines();
    }

    void Update()
    {
        if (source == null || target == null || Time.time >= endsAt)
        {
            Destroy(gameObject);
            return;
        }

        UpdateLines();
    }

    LineRenderer CreateLine(string objectName, float width, int sortingOrder, Color color)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 10;
        line.widthMultiplier = width;
        line.numCapVertices = 8;
        line.numCornerVertices = 4;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0f);
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = sortingOrder;
        return line;
    }

    void UpdateLines()
    {
        Vector2 start = source.position;
        Vector2 end = target.position;
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.up;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized;
        float tLife = Mathf.Clamp01((endsAt - Time.time) / 0.58f);
        SetHookLine(glowLine, start, end, normal, 0.18f * tLife);
        SetHookLine(coreLine, start, end, normal, 0.08f * tLife);
    }

    void SetHookLine(LineRenderer line, Vector2 start, Vector2 end, Vector2 normal, float amplitude)
    {
        if (line == null)
            return;

        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (float)(line.positionCount - 1);
            float wave = Mathf.Sin(t * Mathf.PI * 3f + Time.time * 26f) * amplitude;
            Vector2 point = Vector2.Lerp(start, end, t) + normal * wave;
            line.SetPosition(i, new Vector3(point.x, point.y, -0.44f));
        }
    }
}

public sealed class BioTrapCaptureVfx : MonoBehaviour
{
    const int StrandCount = 7;
    readonly LineRenderer[] strands = new LineRenderer[StrandCount];
    Vector2 source;
    Vector2 target;
    float startedAt;
    float duration;

    public static void Spawn(Vector3 sourcePosition, Vector3 targetPosition)
    {
        GameObject effectObject = new GameObject("BioTrapCaptureVfx");
        BioTrapCaptureVfx effect = effectObject.AddComponent<BioTrapCaptureVfx>();
        effect.Initialize(sourcePosition, targetPosition);
    }

    void Initialize(Vector3 sourcePosition, Vector3 targetPosition)
    {
        source = sourcePosition;
        target = targetPosition;
        startedAt = Time.time;
        duration = 0.68f;
        for (int i = 0; i < strands.Length; i++)
            strands[i] = CreateStrand(i);

        UpdateStrands();
    }

    void Update()
    {
        if (Time.time >= startedAt + duration)
        {
            Destroy(gameObject);
            return;
        }

        UpdateStrands();
    }

    LineRenderer CreateStrand(int index)
    {
        GameObject strandObject = new GameObject("BioTrapStrand_" + index.ToString("00"));
        strandObject.transform.SetParent(transform, false);
        LineRenderer strand = strandObject.AddComponent<LineRenderer>();
        strand.useWorldSpace = true;
        strand.positionCount = 5;
        strand.widthMultiplier = 0.025f;
        strand.numCapVertices = 4;
        strand.numCornerVertices = 3;
        strand.material = new Material(Shader.Find("Sprites/Default"));
        strand.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        strand.sortingOrder = 78 + index;
        return strand;
    }

    void UpdateStrands()
    {
        float age = Mathf.Clamp01((Time.time - startedAt) / Mathf.Max(0.001f, duration));
        Vector2 direction = target - source;
        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.up;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized;
        float netRadius = Mathf.Lerp(0.78f, 0.12f, age);
        Color color = Color.Lerp(new Color(0.38f, 1f, 0.72f, 0.88f), new Color(1f, 0.86f, 0.38f, 0.1f), age);
        for (int i = 0; i < strands.Length; i++)
        {
            LineRenderer strand = strands[i];
            if (strand == null)
                continue;

            float side = i - (strands.Length - 1) * 0.5f;
            Vector2 strandTarget = target + normal * (side * netRadius * 0.32f);
            for (int p = 0; p < strand.positionCount; p++)
            {
                float t = p / (float)(strand.positionCount - 1);
                float curve = Mathf.Sin(t * Mathf.PI) * netRadius * (0.25f + Mathf.Abs(side) * 0.04f);
                Vector2 point = Vector2.Lerp(source, strandTarget, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t + age * 0.24f))) + normal * curve * Mathf.Sign(side == 0f ? 1f : side);
                strand.SetPosition(p, new Vector3(point.x, point.y, -0.45f));
            }

            strand.startColor = color;
            strand.endColor = new Color(color.r, color.g, color.b, color.a * 0.18f);
        }
    }
}

public sealed class SpaceDrillDeployable : PlayerDeployableBase
{
    enum DrillState
    {
        ToTarget,
        Mining,
        Returning
    }

    const float MoveSpeed = 4.7f;
    const float ReturnSpeed = 5.3f;
    const float MiningRange = 0.72f;
    const float MiningDuration = 5f;
    const float DeliveryRange = 0.82f;

    DrillState state;
    int targetViewId;
    float miningStartedAt;
    string heldItemId;
    bool deliveryRequested;
    int deliveryRequestId;
    int pendingDeliveryRequestId;
    int pendingDeliveryActorNumber;
    string pendingDeliveryItemId;
    LineRenderer beam;
    TrailRenderer engineTrail;
    AudioSource miningAudioSource;

    protected override int MaxHp => 10;
    protected override int MaxShield => 20;
    protected override float VisualTargetSize => 0.62f;
    protected override float CollisionRadius => 0.27f;
    protected override string SpriteResourcePath => "space_drill_top_down_resource";
    protected override string EditorSpritePath => "Assets/space_drill_top_down.png";

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
        targetViewId = data != null && data.Length > 2 ? ConvertToInt(data[2]) : 0;
        InitializeCommon();
        ConfigureEngineTrail();
        state = DrillState.ToTarget;
    }

    public static PhotonView FindNearestLootableAsteroid(Vector2 origin)
    {
        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        PhotonView best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || treasure.isBeingCollected)
                continue;

            if (!IsLootableAsteroidItem(treasure.itemId))
                continue;

            PhotonView view = treasure.GetComponent<PhotonView>();
            if (view == null)
                continue;

            float distance = Vector2.Distance(origin, treasure.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = view;
        }

        return best;
    }

    static bool IsLootableAsteroidItem(string itemId)
    {
        return string.Equals(itemId, InventoryItemCatalog.AsteroidCommonId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidUncommonId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidRareId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidVeryRareId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidEpicId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidLegendaryId, StringComparison.Ordinal);
    }

    void Update()
    {
        if (!initialized || destroyed)
            return;

        UpdateBeamVisual();
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (state == DrillState.ToTarget)
            TickToTarget();
        else if (state == DrillState.Mining)
            TickMining();
        else
            TickReturning();
    }

    void TickToTarget()
    {
        PhotonView target = PhotonView.Find(targetViewId);
        Treasure treasure = target != null ? target.GetComponent<Treasure>() : null;
        if (target == null || treasure == null || treasure.isBeingCollected)
        {
            DespawnOnMaster();
            return;
        }

        MoveToward(target.transform.position, MoveSpeed);
        if (Vector2.Distance(transform.position, target.transform.position) <= MiningRange)
        {
            treasure.isBeingCollected = true;
            miningStartedAt = Time.time;
            state = DrillState.Mining;
            photonView.RPC(nameof(SetDrillBeamRpc), RpcTarget.All, targetViewId, true);
        }
    }

    void TickMining()
    {
        PhotonView target = PhotonView.Find(targetViewId);
        Treasure treasure = target != null ? target.GetComponent<Treasure>() : null;
        if (target == null || treasure == null)
        {
            photonView.RPC(nameof(SetDrillBeamRpc), RpcTarget.All, targetViewId, false);
            DespawnOnMaster();
            return;
        }

        if (Vector2.Distance(transform.position, target.transform.position) > Treasure.CollectRange + 0.35f)
        {
            treasure.isBeingCollected = false;
            photonView.RPC(nameof(SetDrillBeamRpc), RpcTarget.All, targetViewId, false);
            state = DrillState.ToTarget;
            return;
        }

        if (Time.time < miningStartedAt + MiningDuration)
            return;

        heldItemId = treasure.itemId;
        treasure.isBeingCollected = false;
        photonView.RPC(nameof(SetDrillBeamRpc), RpcTarget.All, targetViewId, false);
        SpaceTrapTarget.DetonateIfArmed(targetViewId, photonView != null ? photonView.ViewID : 0);
        PhotonNetwork.Destroy(target.gameObject);
        state = DrillState.Returning;
    }

    void TickReturning()
    {
        PhotonView ownerView = ownerShipViewId > 0 ? PhotonView.Find(ownerShipViewId) : null;
        if (ownerView == null || ownerView.Owner == null)
        {
            DespawnOnMaster();
            return;
        }

        MoveToward(ownerView.transform.position, ReturnSpeed);
        if (string.IsNullOrWhiteSpace(heldItemId))
        {
            DespawnOnMaster();
            return;
        }

        if (Vector2.Distance(transform.position, ownerView.transform.position) > DeliveryRange)
            return;

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(ownerView.Owner, heldItemId))
            return;

        if (deliveryRequested)
            return;

        deliveryRequested = true;
        pendingDeliveryRequestId = ++deliveryRequestId;
        pendingDeliveryActorNumber = ownerView.Owner != null ? ownerView.Owner.ActorNumber : 0;
        pendingDeliveryItemId = heldItemId;
        photonView.RPC(nameof(ReceiveSpaceDrillLootRpc), ownerView.Owner, pendingDeliveryRequestId, heldItemId);
    }

    void MoveToward(Vector3 target, float speed)
    {
        Vector2 current = transform.position;
        Vector2 next = Vector2.MoveTowards(current, target, Mathf.Max(0.1f, speed) * Time.deltaTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);

        Vector2 direction = (Vector2)target - current;
        if (direction.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, 0f, angle), 720f * Time.deltaTime);
        }
    }

    [PunRPC]
    async void ReceiveSpaceDrillLootRpc(int requestId, string itemId)
    {
        PhotonView cachedView = photonView;
        bool stored = false;
        try
        {
            string storedItemId = BlueprintCatalog.ResolveContainerBlueprintDrop(
                itemId,
                PlayerProfileService.HasInstance && PlayerProfileService.Instance.CurrentProfile != null
                    ? PlayerProfileService.Instance.CurrentProfile.UnlockedBlueprintIds
                    : new string[0]);
            stored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(storedItemId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Space Drill loot delivery failed: " + ex);
        }

        if (cachedView != null)
            cachedView.RPC(nameof(ResolveSpaceDrillDeliveryRpc), RpcTarget.MasterClient, requestId, itemId, stored);
    }

    [PunRPC]
    void ResolveSpaceDrillDeliveryRpc(int requestId, string itemId, bool stored, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!deliveryRequested ||
            requestId != pendingDeliveryRequestId ||
            messageInfo.Sender == null ||
            messageInfo.Sender.ActorNumber != pendingDeliveryActorNumber ||
            !string.Equals(itemId, pendingDeliveryItemId, StringComparison.Ordinal) ||
            !string.Equals(itemId, heldItemId, StringComparison.Ordinal))
        {
            return;
        }

        if (!stored)
        {
            deliveryRequested = false;
            ClearPendingDelivery();
            return;
        }

        heldItemId = null;
        ClearPendingDelivery();
        PhotonView ownerView = ownerShipViewId > 0 ? PhotonView.Find(ownerShipViewId) : null;
        BroadcastDeliverySound(ownerView, transform.position);
        SetMiningAudio(false);
        DespawnOnMaster();
    }

    void ClearPendingDelivery()
    {
        pendingDeliveryRequestId = 0;
        pendingDeliveryActorNumber = 0;
        pendingDeliveryItemId = null;
    }

    [PunRPC]
    void SetDrillBeamRpc(int targetId, bool active)
    {
        targetViewId = targetId;
        if (active)
        {
            EnsureBeam();
            SetMiningAudio(true);
        }
        else if (beam != null)
        {
            beam.enabled = false;
            SetMiningAudio(false);
        }
        else
        {
            SetMiningAudio(false);
        }
    }

    void EnsureBeam()
    {
        if (beam != null)
        {
            beam.enabled = true;
            return;
        }

        GameObject beamObject = new GameObject("SpaceDrillBeam");
        beamObject.transform.SetParent(transform, false);
        beam = beamObject.AddComponent<LineRenderer>();
        beam.useWorldSpace = true;
        beam.positionCount = 9;
        beam.widthMultiplier = 0.08f;
        beam.numCapVertices = 8;
        beam.material = new Material(Shader.Find("Sprites/Default"));
        beam.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        beam.sortingOrder = 76;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.86f, 0.2f), 0f), new GradientColorKey(new Color(1f, 0.96f, 0.55f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
        beam.colorGradient = gradient;
    }

    void UpdateBeamVisual()
    {
        if (beam == null || !beam.enabled)
            return;

        PhotonView target = PhotonView.Find(targetViewId);
        if (target == null)
        {
            beam.enabled = false;
            return;
        }

        Vector2 start = transform.position;
        Vector2 end = target.transform.position;
        Vector2 direction = end - start;
        Vector2 perpendicular = direction.sqrMagnitude > 0.001f ? new Vector2(-direction.y, direction.x).normalized : Vector2.right;
        for (int i = 0; i < beam.positionCount; i++)
        {
            float t = i / (float)(beam.positionCount - 1);
            float wave = Mathf.Sin(Time.time * 13f + t * Mathf.PI * 5f) * 0.04f;
            Vector2 point = Vector2.Lerp(start, end, t) + perpendicular * wave;
            beam.SetPosition(i, new Vector3(point.x, point.y, -0.36f));
        }
    }

    void ConfigureEngineTrail()
    {
        GameObject trailObject = new GameObject("SpaceDrillYellowTrail");
        trailObject.transform.SetParent(transform, false);
        trailObject.transform.localPosition = new Vector3(0f, -0.22f, 0.05f);
        engineTrail = trailObject.AddComponent<TrailRenderer>();
        EngineTrailVisualUtility.ConfigureTrailBase(engineTrail);
        engineTrail.time = 0.42f;
        engineTrail.minVertexDistance = 0.025f;
        engineTrail.widthMultiplier = 0.08f;
        engineTrail.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        engineTrail.sortingOrder = 30;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.82f, 0.18f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        engineTrail.colorGradient = gradient;
    }

    void SetupMiningAudio()
    {
        if (miningAudioSource != null || AudioManager.Instance.SpaceDrillMiningClip == null)
            return;

        GameObject audioObject = new GameObject("SpaceDrillMiningAudio");
        audioObject.transform.SetParent(transform, false);
        miningAudioSource = audioObject.AddComponent<AudioSource>();
        miningAudioSource.clip = AudioManager.Instance.SpaceDrillMiningClip;
        miningAudioSource.playOnAwake = false;
        AudioManager.Instance.ConfigureSpatialSource(miningAudioSource, 0.42f);
        miningAudioSource.loop = true;
    }

    void SetMiningAudio(bool active)
    {
        if (active)
            SetupMiningAudio();

        if (miningAudioSource == null || miningAudioSource.clip == null)
            return;

        if (active)
        {
            if (!miningAudioSource.isPlaying)
                miningAudioSource.Play();
        }
        else if (miningAudioSource.isPlaying)
        {
            miningAudioSource.Stop();
        }
    }

    void BroadcastDeliverySound(PhotonView ownerView, Vector3 position)
    {
        if (ownerView != null && ownerView.GetComponent<PlayerShooting>() != null)
        {
            ownerView.RPC("PlaySpaceDrillDeliverySoundRpc", RpcTarget.All, position.x, position.y, position.z);
            return;
        }

        AudioManager.Instance.PlaySpaceDrillDeliveryAt(position);
    }

    protected override void OnDestroyedByDamage()
    {
        PhotonView target = PhotonView.Find(targetViewId);
        Treasure treasure = target != null ? target.GetComponent<Treasure>() : null;
        if (treasure != null && string.IsNullOrWhiteSpace(heldItemId))
            treasure.isBeingCollected = false;
    }

    protected override void OnDestroy()
    {
        SetMiningAudio(false);
        base.OnDestroy();
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

public sealed class DropbotDeployable : PlayerDeployableBase
{
    const float MoveSpeed = 5f / 3f;
    const float EscapeRange = 0.72f;
    const float DeliveryRetryDelay = 0.85f;
    const float RotationSpeedDegreesPerSecond = 760f;

    string heldItemId;
    Vector2 extractionPosition;
    bool deliveryRequested;
    float nextDeliveryAttemptTime;
    int deliveryRequestId;
    int pendingDeliveryRequestId;
    int pendingDeliveryActorNumber;
    string pendingDeliveryItemId;
    TrailRenderer engineTrail;

    protected override int MaxHp => 35;
    protected override int MaxShield => 0;
    protected override float VisualTargetSize => 0.72f;
    protected override float CollisionRadius => 0.32f;
    protected override string SpriteResourcePath => "dropbot_up_resource";
    protected override string EditorSpritePath => "Assets/Resources/dropbot_up_resource.png";
    public bool HasCargoInFlight => initialized && !destroyed && !string.IsNullOrWhiteSpace(heldItemId);

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
        heldItemId = data != null && data.Length > 2 ? data[2] as string : null;
        float targetX = data != null && data.Length > 3 ? ConvertToFloat(data[3]) : transform.position.x;
        float targetY = data != null && data.Length > 4 ? ConvertToFloat(data[4]) : transform.position.y;
        extractionPosition = new Vector2(targetX, targetY);
        InitializeCommon();
        ConfigureEngineTrail();
    }

    public static bool TryFindNearestExtraction(Vector2 origin, out Vector2 position)
    {
        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        float bestDistance = float.MaxValue;
        position = Vector2.zero;
        bool found = false;
        for (int i = 0; i < zones.Length; i++)
        {
            ExtractionZone zone = zones[i];
            if (zone == null)
                continue;

            float distance = Vector2.Distance(origin, zone.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            position = zone.transform.position;
            found = true;
        }

        return found;
    }

    void Update()
    {
        if (!initialized || destroyed)
            return;

        if (!PhotonNetwork.IsMasterClient)
            return;

        if (string.IsNullOrWhiteSpace(heldItemId))
        {
            DespawnOnMaster();
            return;
        }

        MoveToward(extractionPosition);
        if (Vector2.Distance(transform.position, extractionPosition) <= EscapeRange)
            TryDeliverCargo();
    }

    void MoveToward(Vector2 target)
    {
        Vector2 current = transform.position;
        Vector2 next = Vector2.MoveTowards(current, target, MoveSpeed * Time.deltaTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);

        Vector2 direction = target - current;
        if (direction.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, 0f, angle), RotationSpeedDegreesPerSecond * Time.deltaTime);
        }
    }

    void TryDeliverCargo()
    {
        if (deliveryRequested || Time.time < nextDeliveryAttemptTime)
            return;

        PhotonView ownerView = ownerShipViewId > 0 ? PhotonView.Find(ownerShipViewId) : null;
        if (ownerView == null || ownerView.Owner == null)
        {
            DespawnOnMaster();
            return;
        }

        deliveryRequested = true;
        pendingDeliveryRequestId = ++deliveryRequestId;
        pendingDeliveryActorNumber = ownerView.Owner.ActorNumber;
        pendingDeliveryItemId = heldItemId;
        photonView.RPC(nameof(ReceiveDropbotRecoveredCargoRpc), ownerView.Owner, pendingDeliveryRequestId, heldItemId);
    }

    [PunRPC]
    async void ReceiveDropbotRecoveredCargoRpc(int requestId, string itemId)
    {
        PhotonView cachedView = photonView;
        bool stored = false;
        try
        {
            stored = await PlayerProfileService.Instance.AddItemToPlayerDeferredSaveAsync(itemId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Dropbot cargo delivery failed: " + ex);
        }

        if (cachedView != null)
            cachedView.RPC(nameof(ResolveDropbotDeliveryRpc), RpcTarget.MasterClient, requestId, itemId, stored);
    }

    [PunRPC]
    void ResolveDropbotDeliveryRpc(int requestId, string itemId, bool stored, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!deliveryRequested ||
            requestId != pendingDeliveryRequestId ||
            messageInfo.Sender == null ||
            messageInfo.Sender.ActorNumber != pendingDeliveryActorNumber ||
            !string.Equals(itemId, pendingDeliveryItemId, StringComparison.Ordinal) ||
            !string.Equals(itemId, heldItemId, StringComparison.Ordinal))
        {
            return;
        }

        if (!stored)
        {
            deliveryRequested = false;
            nextDeliveryAttemptTime = Time.time + DeliveryRetryDelay;
            ClearPendingDelivery();
            return;
        }

        heldItemId = null;
        Vector3 escapedAt = transform.position;
        photonView.RPC(nameof(PlayDropbotEscapedRpc), RpcTarget.All, escapedAt.x, escapedAt.y, escapedAt.z);
        ClearPendingDelivery();
        DespawnOnMaster();
    }

    void ClearPendingDelivery()
    {
        pendingDeliveryRequestId = 0;
        pendingDeliveryActorNumber = 0;
        pendingDeliveryItemId = null;
    }

    void ConfigureEngineTrail()
    {
        GameObject trailObject = new GameObject("DropbotEngineTrail");
        trailObject.transform.SetParent(transform, false);
        trailObject.transform.localPosition = new Vector3(0f, -0.24f, 0.05f);
        engineTrail = trailObject.AddComponent<TrailRenderer>();
        EngineTrailVisualUtility.ConfigureTrailBase(engineTrail);
        engineTrail.time = 0.34f;
        engineTrail.minVertexDistance = 0.025f;
        engineTrail.widthMultiplier = 0.075f;
        engineTrail.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        engineTrail.sortingOrder = 31;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.25f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.1f, 0.38f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        engineTrail.colorGradient = gradient;
    }

    [PunRPC]
    void PlayDropbotEscapedRpc(float x, float y, float z)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayExtractionSequenceAt(new Vector3(x, y, z));
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

public sealed class SpaceTrapTarget : MonoBehaviourPun
{
    const int TrapDamage = 100;
    const float TrapRadius = 3.12f;
    const float TrapOwnerDamagePadding = 0.75f;
    const float TrapCollectorDamagePadding = 0.75f;
    static readonly Dictionary<int, int> ArmedOwnerByTargetView = new Dictionary<int, int>();
    static readonly Collider2D[] TrapDamageHits = new Collider2D[96];

    int ownerViewId;
    bool armed;
    SpriteRenderer markerRenderer;

    public bool IsArmed => armed;

    public static void ResetForSessionTransition()
    {
        ArmedOwnerByTargetView.Clear();
        SpaceTrapTarget[] targets = FindObjectsByType<SpaceTrapTarget>(FindObjectsInactive.Exclude);
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].HideMarker();
        }
    }

    public static SpaceTrapTarget Attach(GameObject target)
    {
        if (target == null)
            return null;

        SpaceTrapTarget trap = target.GetComponent<SpaceTrapTarget>();
        if (trap == null)
            trap = target.AddComponent<SpaceTrapTarget>();

        return trap;
    }

    public static bool TryArmTarget(PhotonView target, int owner)
    {
        if (!PhotonNetwork.IsMasterClient || target == null || owner <= 0 || IsTargetArmed(target.ViewID))
            return false;

        ArmedOwnerByTargetView[target.ViewID] = owner;
        SpaceTrapTarget trap = Attach(target.gameObject);
        if (trap != null)
            trap.Arm(owner);

        return true;
    }

    public static PhotonView FindClosestTrapTarget(Vector2 origin, float range)
    {
        PhotonView best = null;
        float bestDistance = float.MaxValue;

        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || treasure.isBeingCollected)
                continue;

            ConsiderTrapCandidate(treasure.GetComponent<PhotonView>(), treasure.transform.position, origin, range, ref best, ref bestDistance);
        }

        ShipWreck[] wrecks = FindObjectsByType<ShipWreck>(FindObjectsInactive.Exclude);
        for (int i = 0; i < wrecks.Length; i++)
        {
            ShipWreck wreck = wrecks[i];
            if (wreck == null || !wreck.HasLoot || wreck.isBeingCollected)
                continue;

            ConsiderTrapCandidate(wreck.GetComponent<PhotonView>(), wreck.transform.position, origin, range, ref best, ref bestDistance);
        }

        DroppedCargoCrate[] crates = FindObjectsByType<DroppedCargoCrate>(FindObjectsInactive.Exclude);
        for (int i = 0; i < crates.Length; i++)
        {
            DroppedCargoCrate crate = crates[i];
            if (crate == null || !crate.HasLoot || crate.isBeingCollected)
                continue;

            ConsiderTrapCandidate(crate.GetComponent<PhotonView>(), crate.transform.position, origin, range, ref best, ref bestDistance);
        }

        return best;
    }

    static void ConsiderTrapCandidate(PhotonView view, Vector2 position, Vector2 origin, float range, ref PhotonView best, ref float bestDistance)
    {
        if (view == null || IsTargetArmed(view.ViewID))
            return;

        float distance = Vector2.Distance(origin, position);
        if (distance > range || distance >= bestDistance)
            return;

        bestDistance = distance;
        best = view;
    }

    public static bool DetonateIfArmed(int targetViewId)
    {
        return DetonateIfArmed(targetViewId, 0);
    }

    public static bool DetonateIfArmed(int targetViewId, int collectorViewId)
    {
        if (!PhotonNetwork.IsMasterClient)
            return false;

        PhotonView view = PhotonView.Find(targetViewId);
        SpaceTrapTarget trap = view != null ? view.GetComponent<SpaceTrapTarget>() : null;
        if (!TryGetArmedOwner(targetViewId, trap, out int armedOwnerViewId))
            return false;

        if (view == null)
        {
            ArmedOwnerByTargetView.Remove(targetViewId);
            return false;
        }

        ArmedOwnerByTargetView.Remove(targetViewId);
        if (trap != null)
            trap.HideMarker();

        BroadcastMarkerClear(armedOwnerViewId, collectorViewId, targetViewId);
        Vector3 point = view.transform.position;
        SpaceObjectMotionSync.BroadcastSpaceMineDetonation(point, TrapRadius);
        ApplyExplosionDamage(point, armedOwnerViewId, collectorViewId);
        return true;
    }

    static bool TryGetArmedOwner(int targetViewId, SpaceTrapTarget trap, out int armedOwnerViewId)
    {
        if (ArmedOwnerByTargetView.TryGetValue(targetViewId, out armedOwnerViewId) && armedOwnerViewId > 0)
            return true;

        if (trap != null && trap.armed && trap.ownerViewId > 0)
        {
            armedOwnerViewId = trap.ownerViewId;
            return true;
        }

        armedOwnerViewId = 0;
        return false;
    }

    static bool IsTargetArmed(int targetViewId)
    {
        if (targetViewId <= 0)
            return false;

        if (ArmedOwnerByTargetView.ContainsKey(targetViewId))
            return true;

        PhotonView view = PhotonView.Find(targetViewId);
        SpaceTrapTarget trap = view != null ? view.GetComponent<SpaceTrapTarget>() : null;
        return trap != null && trap.IsArmed;
    }

    static void BroadcastMarkerClear(int owner, int collectorViewId, int targetViewId)
    {
        PhotonView broadcaster = ResolvePlayerShootingView(owner);
        if (broadcaster == null)
            broadcaster = ResolvePlayerShootingView(collectorViewId);

        if (broadcaster != null)
            broadcaster.RPC("ClearSpaceTrapTargetRpc", RpcTarget.All, targetViewId);
    }

    static PhotonView ResolvePlayerShootingView(int viewId)
    {
        if (viewId <= 0)
            return null;

        PhotonView view = PhotonView.Find(viewId);
        return view != null && view.GetComponent<PlayerShooting>() != null ? view : null;
    }

    public static void ClearLocalMarker(int targetViewId)
    {
        ArmedOwnerByTargetView.Remove(targetViewId);
        PhotonView view = PhotonView.Find(targetViewId);
        SpaceTrapTarget trap = view != null ? view.GetComponent<SpaceTrapTarget>() : null;
        if (trap != null)
            trap.HideMarker();
    }

    public void Arm(int owner)
    {
        ownerViewId = owner;
        armed = true;
        if (photonView != null)
            ArmedOwnerByTargetView[photonView.ViewID] = ownerViewId;
        EnsureMarker();
    }

    [PunRPC]
    void ArmTrapRpc(int owner)
    {
        Arm(owner);
    }

    void Update()
    {
    }

    void OnDestroy()
    {
        if (photonView != null)
            ArmedOwnerByTargetView.Remove(photonView.ViewID);
    }

    void HideMarker()
    {
        armed = false;
        if (markerRenderer != null)
            markerRenderer.enabled = false;
    }

    void EnsureMarker()
    {
        if (markerRenderer != null)
        {
            markerRenderer.enabled = true;
            return;
        }

        GameObject marker = new GameObject("SpaceTrapMarker");
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        markerRenderer = marker.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = RuntimeSpriteUtility.LoadSprite("space_trap_top_down_resource", "Assets/space_trap_top_down.png");
        markerRenderer.color = new Color(1f, 0.88f, 0.18f, 0.78f);
        markerRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        markerRenderer.sortingOrder = 180;
        RuntimeSpriteUtility.FitRenderer(markerRenderer, 0.5f);
    }

    void Detonate()
    {
        if (!PhotonNetwork.IsMasterClient || !armed || photonView == null)
            return;

        DetonateIfArmed(photonView.ViewID);
    }

    [PunRPC]
    void PlayTrapExplosionRpc(float x, float y, float radius)
    {
        armed = false;
        if (markerRenderer != null)
            markerRenderer.enabled = false;

        EnemyBot.SpawnSpaceMineDetonationEffects(new Vector3(x, y, transform.position.z), radius);
    }

    static void ApplyExplosionDamage(Vector2 center, int attackerViewId, int collectorViewId)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };
        int hitCount = Physics2D.OverlapCircle(center, TrapRadius, filter, TrapDamageHits);
        HashSet<int> damagedViews = new HashSet<int>();
        WeaponHitContext hitContext = new WeaponHitContext(
            WeaponDamageType.Explosive,
            WeaponDeliveryMethod.Trap,
            WeaponDeliveryFlags.AreaDamage | WeaponDeliveryFlags.Delayed,
            PlayerHealth.DamageSourceExplosive);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = TrapDamageHits[i];
            if (hit == null)
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable.photonView != null && damagedViews.Add(deployable.photonView.ViewID))
            {
                deployable.photonView.RPC(
                    nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                    RpcTarget.MasterClient,
                    TrapDamage,
                    TrapDamage,
                    attackerViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
                continue;
            }

            PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
            if (health != null && health.photonView != null && !health.IsWreck && damagedViews.Add(health.photonView.ViewID))
            {
                health.photonView.RPC(
                    nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                    RpcTarget.MasterClient,
                    TrapDamage,
                    TrapDamage,
                    attackerViewId,
                    center.x,
                    center.y,
                    (int)hitContext.DamageType,
                    (int)hitContext.DeliveryMethod,
                    (int)hitContext.DeliveryFlags,
                    hitContext.DamageSource ?? string.Empty);
                continue;
            }
        }

        TryDamageViewIfClose(attackerViewId, center, attackerViewId, damagedViews, TrapOwnerDamagePadding, hitContext);
        TryDamageViewIfClose(collectorViewId, center, attackerViewId, damagedViews, TrapCollectorDamagePadding, hitContext);
    }

    static void TryDamageViewIfClose(int targetViewId, Vector2 center, int attackerViewId, HashSet<int> damagedViews, float padding, WeaponHitContext hitContext)
    {
        if (targetViewId <= 0)
            return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null || Vector2.Distance(center, targetView.transform.position) > TrapRadius + padding)
            return;

        PlayerDeployableBase deployable = targetView.GetComponent<PlayerDeployableBase>();
        if (deployable != null && deployable.photonView != null && deployable.CanBeTargeted && damagedViews.Add(deployable.photonView.ViewID))
        {
            deployable.photonView.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                TrapDamage,
                TrapDamage,
                attackerViewId,
                center.x,
                center.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
            return;
        }

        PlayerHealth health = targetView.GetComponent<PlayerHealth>();
        if (health != null && health.photonView != null && !health.IsWreck && damagedViews.Add(health.photonView.ViewID))
            health.photonView.RPC(
                nameof(PlayerHealth.TakeDamageProfileWithContextAt),
                RpcTarget.MasterClient,
                TrapDamage,
                TrapDamage,
                attackerViewId,
                center.x,
                center.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
    }
}

public sealed class LootingFriendController : MonoBehaviourPun
{
    const float FollowRightOffset = 0.72f;
    const float FollowBackOffset = -0.05f;
    const float CollectDuration = 6f;
    const float ScanInterval = 0.18f;
    const float CollectRangeMultiplier = 1.3f;
    const float VisualTargetWorldSize = GameVisualTheme.PlayerTargetSize * 0.22f;

    SpriteRenderer visualRenderer;
    LineRenderer beam;
    AudioSource collectAudioSource;
    Coroutine collectRoutine;
    PhotonView currentTarget;
    float nextScanTime;
    bool visualActive;
    bool forcedForNeutralRider;

    float CollectRange => Treasure.CollectRange * CollectRangeMultiplier;

    void Start()
    {
        EnsureVisual();
        SetupCollectAudio();
    }

    void Update()
    {
        if (!CanLootingFriendRun())
        {
            SetVisualActive(false);
            StopCollecting();
            return;
        }

        bool equipped = IsLootingFriendEquipped();
        SetVisualActive(equipped);
        if (!equipped)
        {
            StopCollecting();
            return;
        }

        UpdateVisualTransform();
        UpdateBeam();
        if (currentTarget != null && beam != null && beam.enabled)
            SetCollectAudio(true);

        if (forcedForNeutralRider)
            return;

        if (!photonView.IsMine)
            return;

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null && collector.IsCollectingAny)
        {
            StopCollecting();
            return;
        }

        if (collectRoutine != null || Time.time < nextScanTime)
            return;

        nextScanTime = Time.time + ScanInterval;
        PhotonView target = FindAutoLootTarget();
        if (target != null)
            collectRoutine = StartCoroutine(CollectRoutine(target));
    }

    public void DeactivateForShipLoss()
    {
        StopCollecting();
        SetVisualActive(false);
        enabled = false;
    }

    public void SetForcedForNeutralRider(bool forced)
    {
        forcedForNeutralRider = forced;
        if (forced)
            enabled = true;
        else
            StopCollecting();
    }

    public void SetNeutralRiderCollectFx(int targetViewId, bool active)
    {
        if (photonView != null)
        {
            photonView.RPC(nameof(SetLootingFriendCollectFxRpc), RpcTarget.All, targetViewId, active);
            return;
        }

        SetLootingFriendCollectFxRpc(targetViewId, active);
    }

    bool CanLootingFriendRun()
    {
        PlayerHealth health = GetComponent<PlayerHealth>();
        return health != null &&
               health.isActiveAndEnabled &&
               !health.IsWreck &&
               !health.IsEvacuationAnimating &&
               !health.IsAstronautControlled;
    }

    bool IsLootingFriendEquipped()
    {
        if (forcedForNeutralRider)
            return true;

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipment = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        return InventoryItemCatalog.HasEquippedItem(equipment, shipSkinIndex, InventoryItemCatalog.LootingFriendId);
    }

    PhotonView FindAutoLootTarget()
    {
        if (!CanLootingFriendRun())
            return null;

        if (!PlayerProfileService.Instance.HasFreeShipInventorySlot())
            return null;

        Vector2 origin = transform.position;
        PhotonView best = null;
        float bestDistance = float.MaxValue;

        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || treasure.isBeingCollected || InventoryItemCatalog.IsRandomLootWreckItem(treasure.itemId))
                continue;

            if (!PlayerProfileService.Instance.HasFreeShipInventorySlot(treasure.itemId))
                continue;

            ConsiderTarget(treasure.GetComponent<PhotonView>(), treasure.transform.position, origin, ref best, ref bestDistance);
        }

        ShipWreck[] wrecks = FindObjectsByType<ShipWreck>(FindObjectsInactive.Exclude);
        for (int i = 0; i < wrecks.Length; i++)
        {
            ShipWreck wreck = wrecks[i];
            if (wreck == null || !wreck.HasLoot || wreck.isBeingCollected)
                continue;

            string itemId = wreck.GetLootItemAt(wreck.GetFirstLootIndex());
            if (!PlayerProfileService.Instance.HasFreeShipInventorySlot(itemId))
                continue;

            ConsiderTarget(wreck.GetComponent<PhotonView>(), wreck.transform.position, origin, ref best, ref bestDistance);
        }

        DroppedCargoCrate[] crates = FindObjectsByType<DroppedCargoCrate>(FindObjectsInactive.Exclude);
        for (int i = 0; i < crates.Length; i++)
        {
            DroppedCargoCrate crate = crates[i];
            if (crate == null || !crate.HasLoot || crate.isBeingCollected)
                continue;

            PhotonView crateView = crate.GetComponent<PhotonView>();
            if (crateView != null && crateView.CreatorActorNr == photonView.OwnerActorNr)
                continue;

            if (!PlayerProfileService.Instance.HasFreeShipInventorySlot(crate.StoredItemId))
                continue;

            ConsiderTarget(crateView, crate.transform.position, origin, ref best, ref bestDistance);
        }

        return best;
    }

    void ConsiderTarget(PhotonView view, Vector2 position, Vector2 origin, ref PhotonView best, ref float bestDistance)
    {
        if (view == null)
            return;

        float distance = Vector2.Distance(origin, position);
        if (distance > CollectRange || distance >= bestDistance)
            return;

        bestDistance = distance;
        best = view;
    }

    IEnumerator CollectRoutine(PhotonView target)
    {
        if (!CanLootingFriendRun())
        {
            collectRoutine = null;
            yield break;
        }

        int targetViewId = target != null ? target.ViewID : 0;
        photonView.RPC(nameof(SetLootingFriendCollectFxRpc), RpcTarget.All, targetViewId, true);
        float startedAt = Time.time;
        while (Time.time < startedAt + CollectDuration)
        {
            if (!CanLootingFriendRun())
                break;

            TreasureCollector collector = GetComponent<TreasureCollector>();
            if (collector != null && collector.IsCollectingAny)
                break;

            if (!IsTargetStillCollectible(target))
                break;

            yield return null;
        }

        bool completed = target != null && Time.time >= startedAt + CollectDuration && IsTargetStillCollectible(target);
        photonView.RPC(nameof(SetLootingFriendCollectFxRpc), RpcTarget.All, targetViewId, false);
        collectRoutine = null;

        if (completed)
            RequestLoot(target);
    }

    bool IsTargetStillCollectible(PhotonView target)
    {
        if (target == null)
            return false;

        float distance = Vector2.Distance(transform.position, target.transform.position);
        if (distance > CollectRange)
            return false;

        Treasure treasure = target.GetComponent<Treasure>();
        if (treasure != null)
            return !treasure.isBeingCollected && PlayerProfileService.Instance.HasFreeShipInventorySlot(treasure.itemId);

        ShipWreck wreck = target.GetComponent<ShipWreck>();
        if (wreck != null)
            return wreck.HasLoot && !wreck.isBeingCollected && PlayerProfileService.Instance.HasFreeShipInventorySlot(wreck.GetLootItemAt(wreck.GetFirstLootIndex()));

        DroppedCargoCrate crate = target.GetComponent<DroppedCargoCrate>();
        if (crate != null)
        {
            if (target.CreatorActorNr == photonView.OwnerActorNr)
                return false;

            return crate.HasLoot && !crate.isBeingCollected && PlayerProfileService.Instance.HasFreeShipInventorySlot(crate.StoredItemId);
        }

        return false;
    }

    void RequestLoot(PhotonView target)
    {
        if (target == null || photonView == null || !CanLootingFriendRun())
            return;

        if (target.GetComponent<Treasure>() != null)
            photonView.RPC(nameof(RequestLootingFriendTreasureRpc), RpcTarget.MasterClient, target.ViewID);
        else if (target.GetComponent<ShipWreck>() != null)
            photonView.RPC(nameof(RequestLootingFriendWreckRpc), RpcTarget.MasterClient, target.ViewID);
        else if (target.GetComponent<DroppedCargoCrate>() != null)
            photonView.RPC(nameof(RequestLootingFriendCrateRpc), RpcTarget.MasterClient, target.ViewID);
    }

    [PunRPC]
    void RequestLootingFriendTreasureRpc(int targetViewId)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || !CanLootingFriendRun())
            return;

        PhotonView target = PhotonView.Find(targetViewId);
        Treasure treasure = target != null ? target.GetComponent<Treasure>() : null;
        if (treasure == null || treasure.isBeingCollected || InventoryItemCatalog.IsRandomLootWreckItem(treasure.itemId))
            return;

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, treasure.itemId))
            return;

        photonView.RPC(nameof(ReceiveLootingFriendItemRpc), photonView.Owner, targetViewId, treasure.itemId, true, -1);
    }

    [PunRPC]
    void RequestLootingFriendWreckRpc(int targetViewId)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || !CanLootingFriendRun())
            return;

        PhotonView target = PhotonView.Find(targetViewId);
        ShipWreck wreck = target != null ? target.GetComponent<ShipWreck>() : null;
        if (wreck == null || !wreck.HasLoot || wreck.isBeingCollected)
            return;

        int lootIndex = wreck.GetFirstLootIndex();
        string itemId = wreck.GetLootItemAt(lootIndex);
        if (lootIndex < 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        if (!PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, itemId))
            return;

        photonView.RPC(nameof(ReceiveLootingFriendItemRpc), photonView.Owner, targetViewId, itemId, false, lootIndex);
    }

    [PunRPC]
    void RequestLootingFriendCrateRpc(int targetViewId)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || !CanLootingFriendRun())
            return;

        PhotonView target = PhotonView.Find(targetViewId);
        DroppedCargoCrate crate = target != null ? target.GetComponent<DroppedCargoCrate>() : null;
        if (crate == null || !crate.HasLoot || crate.isBeingCollected || target.CreatorActorNr == photonView.OwnerActorNr)
            return;

        string itemId = crate.StoredItemId;
        if (string.IsNullOrWhiteSpace(itemId) || !PlayerProfileService.PlayerHasFreeShipInventorySlot(photonView.Owner, itemId))
            return;

        photonView.RPC(nameof(ReceiveLootingFriendCrateItemRpc), photonView.Owner, targetViewId, itemId);
    }

    [PunRPC]
    async void ReceiveLootingFriendItemRpc(int targetViewId, string itemId, bool treasure, int lootIndex)
    {
        if (!CanLootingFriendRun())
        {
            if (photonView != null)
                photonView.RPC(nameof(ResolveLootingFriendLootRpc), RpcTarget.MasterClient, targetViewId, itemId, treasure, lootIndex, false);
            return;
        }

        bool stored = false;
        try
        {
            string storedItemId = BlueprintCatalog.ResolveContainerBlueprintDrop(
                itemId,
                PlayerProfileService.HasInstance && PlayerProfileService.Instance.CurrentProfile != null
                    ? PlayerProfileService.Instance.CurrentProfile.UnlockedBlueprintIds
                    : new string[0]);
            stored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(storedItemId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Looting Friend failed to store item: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ResolveLootingFriendLootRpc), RpcTarget.MasterClient, targetViewId, itemId, treasure, lootIndex, stored);
    }

    [PunRPC]
    void ResolveLootingFriendLootRpc(int targetViewId, string itemId, bool treasure, int lootIndex, bool stored)
    {
        if (!PhotonNetwork.IsMasterClient || !stored || !CanLootingFriendRun())
            return;

        SpaceTrapTarget.DetonateIfArmed(targetViewId, photonView != null ? photonView.ViewID : 0);
        PhotonView target = PhotonView.Find(targetViewId);
        if (target == null)
            return;

        if (treasure)
        {
            Treasure treasureComponent = target.GetComponent<Treasure>();
            if (treasureComponent != null && string.Equals(treasureComponent.itemId, itemId, StringComparison.Ordinal))
                PhotonNetwork.Destroy(target.gameObject);
            return;
        }

        ShipWreck wreck = target.GetComponent<ShipWreck>();
        if (wreck != null && wreck.HasLoot && string.Equals(wreck.GetLootItemAt(lootIndex), itemId, StringComparison.Ordinal))
            target.RPC(nameof(ShipWreck.RemoveLootAtIndexRpc), RpcTarget.All, lootIndex);
    }

    [PunRPC]
    async void ReceiveLootingFriendCrateItemRpc(int targetViewId, string itemId)
    {
        if (!CanLootingFriendRun())
        {
            if (photonView != null)
                photonView.RPC(nameof(ResolveLootingFriendCrateLootRpc), RpcTarget.MasterClient, targetViewId, itemId, false);
            return;
        }

        bool stored = false;
        try
        {
            stored = await PlayerProfileService.Instance.AddItemToShipDeferredSaveAsync(itemId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Looting Friend failed to store crate item: " + ex);
        }

        if (photonView != null)
            photonView.RPC(nameof(ResolveLootingFriendCrateLootRpc), RpcTarget.MasterClient, targetViewId, itemId, stored);
    }

    [PunRPC]
    void ResolveLootingFriendCrateLootRpc(int targetViewId, string itemId, bool stored)
    {
        if (!PhotonNetwork.IsMasterClient || !stored || !CanLootingFriendRun())
            return;

        SpaceTrapTarget.DetonateIfArmed(targetViewId, photonView != null ? photonView.ViewID : 0);
        PhotonView target = PhotonView.Find(targetViewId);
        DroppedCargoCrate crate = target != null ? target.GetComponent<DroppedCargoCrate>() : null;
        if (crate == null || !crate.HasLoot || !string.Equals(crate.StoredItemId, itemId, StringComparison.Ordinal))
            return;

        target.RPC(nameof(DroppedCargoCrate.ClearStoredItemRpc), RpcTarget.All);
        PhotonNetwork.Destroy(target.gameObject);
    }

    void StopCollecting()
    {
        if (collectRoutine != null)
        {
            StopCoroutine(collectRoutine);
            collectRoutine = null;
        }

        if (photonView != null && photonView.IsMine && currentTarget != null)
            photonView.RPC(nameof(SetLootingFriendCollectFxRpc), RpcTarget.All, currentTarget.ViewID, false);

        currentTarget = null;
        SetBeamEnabled(false);
        SetCollectAudio(false);
    }

    void EnsureVisual()
    {
        if (visualRenderer != null)
            return;

        GameObject visual = new GameObject("LootingFriendVisual");
        visual.transform.SetParent(transform, false);
        visualRenderer = visual.AddComponent<SpriteRenderer>();
        visualRenderer.sprite = RuntimeSpriteUtility.LoadSprite("looting_friend_top_down_resource", "Assets/looting_friend_top_down.png");
        visualRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        visualRenderer.sortingOrder = 48;
        visualRenderer.color = Color.white;
        RuntimeSpriteUtility.FitRendererWorldSize(visualRenderer, VisualTargetWorldSize);
        visualActive = true;
        SetupCollectAudio();
    }

    void SetVisualActive(bool active)
    {
        EnsureVisual();
        if (visualRenderer != null)
            visualRenderer.enabled = active;
        visualActive = active;
        if (!active)
        {
            SetBeamEnabled(false);
            SetCollectAudio(false);
        }
    }

    void UpdateVisualTransform()
    {
        if (visualRenderer == null || !visualActive)
            return;

        RuntimeSpriteUtility.FitRendererWorldSize(visualRenderer, VisualTargetWorldSize);
        visualRenderer.transform.position = transform.position + transform.right * FollowRightOffset + transform.up * FollowBackOffset;
        visualRenderer.transform.rotation = transform.rotation;
    }

    void SetBeamEnabled(bool active)
    {
        if (active)
            EnsureBeam();

        if (beam != null)
            beam.enabled = active;
    }

    [PunRPC]
    void SetLootingFriendCollectFxRpc(int targetViewId, bool active)
    {
        EnsureVisual();
        PhotonView target = active && targetViewId > 0 ? PhotonView.Find(targetViewId) : null;
        currentTarget = target;
        SetBeamEnabled(active && target != null);
        SetCollectAudio(active && target != null);
    }

    void SetupCollectAudio()
    {
        if (collectAudioSource != null || visualRenderer == null || AudioManager.Instance.LootingFriendDrillClip == null)
            return;

        collectAudioSource = visualRenderer.gameObject.AddComponent<AudioSource>();
        collectAudioSource.clip = AudioManager.Instance.LootingFriendDrillClip;
        collectAudioSource.loop = true;
        collectAudioSource.playOnAwake = false;
        AudioManager.Instance.ConfigureSpatialSource(collectAudioSource, 0.42f);
    }

    void SetCollectAudio(bool active)
    {
        if (active)
            SetupCollectAudio();

        if (collectAudioSource == null || collectAudioSource.clip == null)
            return;

        if (active)
        {
            if (!collectAudioSource.isPlaying)
                collectAudioSource.Play();
        }
        else if (collectAudioSource.isPlaying)
        {
            collectAudioSource.Stop();
        }
    }

    void EnsureBeam()
    {
        if (beam != null)
            return;

        GameObject beamObject = new GameObject("LootingFriendBeam");
        beamObject.transform.SetParent(transform, false);
        beam = beamObject.AddComponent<LineRenderer>();
        beam.useWorldSpace = true;
        beam.positionCount = 11;
        beam.widthMultiplier = 0.07f;
        beam.numCapVertices = 8;
        beam.material = new Material(Shader.Find("Sprites/Default"));
        beam.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        beam.sortingOrder = 75;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.78f, 0.08f), 0f), new GradientColorKey(new Color(1f, 0.98f, 0.5f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.2f), new GradientAlphaKey(0f, 1f) });
        beam.colorGradient = gradient;
    }

    void UpdateBeam()
    {
        if (beam == null || !beam.enabled)
            return;

        if (currentTarget == null || visualRenderer == null)
        {
            SetBeamEnabled(false);
            SetCollectAudio(false);
            return;
        }

        Vector2 start = visualRenderer.transform.position;
        Vector2 end = currentTarget.transform.position;
        Vector2 direction = end - start;
        Vector2 perpendicular = direction.sqrMagnitude > 0.001f ? new Vector2(-direction.y, direction.x).normalized : Vector2.right;
        for (int i = 0; i < beam.positionCount; i++)
        {
            float t = i / (float)(beam.positionCount - 1);
            float wave = Mathf.Sin(Time.time * 11f + t * Mathf.PI * 6f) * 0.045f;
            Vector2 point = Vector2.Lerp(start, end, t) + perpendicular * wave;
            beam.SetPosition(i, new Vector3(point.x, point.y, -0.34f));
        }
    }
}

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
    const string BulletEffectId = "pirate_fighter";
    static readonly Color BulletColor = new Color(0.2f, 0.86f, 1f, 1f);

    SpriteRenderer visualRenderer;
    float nextScanTime;
    float nextShotTime;
    float reloadUntil;
    int shotsInBurst;
    bool visualActive;
    bool forcedForNeutralRider;
    Vector2 recentAimDirection = Vector2.up;
    float recentAimUntil;

    void Start()
    {
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
        PlayerHealth health = GetComponent<PlayerHealth>();
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

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipment = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        return InventoryItemCatalog.HasEquippedItem(equipment, shipSkinIndex, InventoryItemCatalog.FiringFriendId);
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
        PlayerHealth[] healths = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
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

        GameObject bullet = PhotonNetwork.Instantiate(
            "Bullet",
            new Vector3(muzzle.x, muzzle.y, 0f),
            Quaternion.Euler(0f, 0f, angle),
            0,
            data);

        if (bullet == null)
            return;

        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent != null)
            bulletComponent.ownerViewID = ownerId;

        Rigidbody2D bulletBody = bullet.GetComponent<Rigidbody2D>();
        if (bulletBody != null)
            bulletBody.linearVelocity = direction * BulletSpeed;

        Collider2D bulletCollider = bullet.GetComponent<Collider2D>();
        IgnoreOwnerCollisions(bulletCollider);

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
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.orthographic)
        {
            float halfVertical = mainCamera.orthographicSize;
            float halfHorizontal = halfVertical * mainCamera.aspect;
            range = Mathf.Min(range, Mathf.Max(0.75f, Mathf.Min(halfVertical, halfHorizontal)));
        }

        return Mathf.Max(0.75f, range);
    }

    float ResolveOwnerLength()
    {
        SpriteRenderer ownerRenderer = GetComponentInChildren<SpriteRenderer>();
        if (ownerRenderer != null)
            return Mathf.Max(0.25f, Mathf.Max(ownerRenderer.bounds.size.x, ownerRenderer.bounds.size.y));

        Collider2D ownerCollider = GetComponentInChildren<Collider2D>();
        if (ownerCollider != null)
            return Mathf.Max(0.25f, Mathf.Max(ownerCollider.bounds.size.x, ownerCollider.bounds.size.y));

        return 1f;
    }

    Vector2 ResolveMuzzleOrigin()
    {
        return visualRenderer != null ? (Vector2)visualRenderer.transform.position : (Vector2)transform.position;
    }

    void IgnoreOwnerCollisions(Collider2D bulletCollider)
    {
        if (bulletCollider == null)
            return;

        Collider2D[] ownerColliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            Collider2D ownerCollider = ownerColliders[i];
            if (ownerCollider != null)
                Physics2D.IgnoreCollision(ownerCollider, bulletCollider, true);
        }
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

public sealed class SpaceTrapLaunchVfx : MonoBehaviour
{
    const float TravelDuration = 0.28f;

    Vector3 start;
    Vector3 end;
    float startedAt;
    SpriteRenderer spriteRenderer;

    public static void Spawn(Vector3 from, Vector3 to)
    {
        GameObject effect = new GameObject("SpaceTrapLaunchVfx");
        SpaceTrapLaunchVfx vfx = effect.AddComponent<SpaceTrapLaunchVfx>();
        vfx.Initialize(from, to);
    }

    void Initialize(Vector3 from, Vector3 to)
    {
        start = new Vector3(from.x, from.y, -0.36f);
        end = new Vector3(to.x, to.y, -0.36f);
        startedAt = Time.time;
        transform.position = start;
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = RuntimeSpriteUtility.LoadSprite("space_trap_top_down_resource", "Assets/space_trap_top_down.png");
        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = 86;
        spriteRenderer.color = Color.white;
        RuntimeSpriteUtility.FitRenderer(spriteRenderer, 0.42f);
    }

    void Update()
    {
        float t = Mathf.Clamp01((Time.time - startedAt) / TravelDuration);
        Vector3 arc = Vector3.Lerp(start, end, t);
        Vector2 flatDirection = end - start;
        Vector2 perpendicular = flatDirection.sqrMagnitude > 0.001f ? new Vector2(-flatDirection.y, flatDirection.x).normalized : Vector2.up;
        float wobble = Mathf.Sin(t * Mathf.PI) * 0.18f;
        transform.position = arc + (Vector3)(perpendicular * wobble);

        Vector2 direction = (Vector2)(end - start);
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = Mathf.Lerp(1f, 0.15f, t);
            spriteRenderer.color = color;
        }

        if (t >= 1f)
            Destroy(gameObject);
    }
}
