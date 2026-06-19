using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemyContainerShipBehavior : EnemyBotBehaviorBase
{
    const float SpeedBoostDuration = 5f;
    const float MineDropCooldown = 15f;
    const float AutoCannonChance = 0.15f;
    const float MineRearOffset = 1.05f;
    const float MineSideOffset = 0.48f;
    const float AutoCannonRearOffset = 1.08f;
    const float CargoVisualTargetSize = 1.08f;
    const float MapEdgeMargin = 3.1f;
    const string CargoVisualName = "ContainerShipCargoVisual";
    const string CargoTopResourcePath = "Enemies/ContainerShip/container_set2_top";

    static Sprite[] cachedCargoTopSprites;

    public static void PrewarmCargoSprites()
    {
        if (cachedCargoTopSprites == null || cachedCargoTopSprites.Length == 0)
            cachedCargoTopSprites = Resources.LoadAll<Sprite>(CargoTopResourcePath);

        if (cachedCargoTopSprites == null)
            return;

        for (int i = 0; i < cachedCargoTopSprites.Length; i++)
            PrewarmSpriteTexture(cachedCargoTopSprites[i]);
    }

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    Vector2 orbitCenter;
    float orbitRadius;
    float orbitAngle;
    float orbitDirection = 1f;
    float nextMineDropTime;
    Vector2 lastMoveDirection = Vector2.left;
    SpriteRenderer cargoRenderer;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;

        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        orbitCenter = Vector2.zero;
        orbitRadius = Mathf.Max(5.6f, Mathf.Min(mapSize.x, mapSize.y) * (movement != null ? movement.OrbitRadiusFactor : 0.38f));
        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        orbitAngle = Mathf.Abs(seed * 0.137f) % (Mathf.PI * 2f);
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        EnsureCargoVisual();
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        EnsureCargoVisual();

        orbitAngle += orbitDirection * movement.OrbitAngularSpeed * Time.fixedDeltaTime;
        Vector2 fromCenter = rb.position - orbitCenter;
        if (fromCenter.sqrMagnitude < 0.01f)
            fromCenter = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle));

        Vector2 radialDirection = fromCenter.normalized;
        Vector2 tangentDirection = orbitDirection > 0f
            ? new Vector2(-radialDirection.y, radialDirection.x)
            : new Vector2(radialDirection.y, -radialDirection.x);

        float radialError = orbitRadius - fromCenter.magnitude;
        Vector2 desiredVelocity = tangentDirection * bot.EffectiveMoveSpeed + radialDirection * (radialError * 1.12f);
        desiredVelocity = ApplyMapEdgeSteering(desiredVelocity);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.14f);

        if (desiredVelocity.sqrMagnitude > 0.001f)
        {
            lastMoveDirection = desiredVelocity.normalized;
            float targetAngle = Mathf.Atan2(lastMoveDirection.y, lastMoveDirection.x) * Mathf.Rad2Deg + 180f;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }
    }

    public void NotifyDamageTaken(int attackerViewID)
    {
        if (!PhotonNetwork.IsMasterClient || bot == null || health == null || health.IsWreck)
            return;

        if (!EnemyBot.IsPlayerControlledDamageSource(attackerViewID))
            return;

        bot.ActivateTemporarySpeedMultiplier(2f, SpeedBoostDuration);
        if (Time.time < nextMineDropTime)
            return;

        nextMineDropTime = Time.time + ScaleEnemyAttackCooldown(MineDropCooldown);
        if (Random.value < AutoCannonChance)
            SpawnDefensiveAutoCannon();
        else
            SpawnDefensiveMines();
    }

    void SpawnDefensiveMines()
    {
        if (!PhotonNetwork.InRoom || view == null)
            return;

        EnemyBotDefinition mineDefinition = EnemyBotCatalog.GetDefinition(EnemyBotKind.SpaceMine);
        if (mineDefinition == null)
            return;

        Vector2 forward = ResolveForwardDirection();
        Vector2 behind = -forward;
        Vector2 side = new Vector2(-forward.y, forward.x);

        for (int i = 0; i < 2; i++)
        {
            float sideSign = i == 0 ? -1f : 1f;
            Vector2 spawnOffset = behind * MineRearOffset + side * (MineSideOffset * sideSign);
            Vector2 driftDirection = (behind * 0.86f + side * (0.18f * sideSign)).normalized;
            Vector3 spawnPosition = transform.position + (Vector3)spawnOffset;
            GameObject mineObject = PhotonNetwork.Instantiate(
                "Player",
                spawnPosition,
                Quaternion.identity,
                0,
                new object[] { mineDefinition.InstantiationMarker, EnemyBot.ContainerShipMineMarker, view.ViewID, driftDirection.x, driftDirection.y });

            if (mineObject == null)
                continue;

            EnemyBot mine = mineObject.GetComponent<EnemyBot>();
            if (mine == null)
                mine = mineObject.AddComponent<EnemyBot>();

            mine.InitializeFromPhotonData();
            Rigidbody2D mineBody = mineObject.GetComponent<Rigidbody2D>();
            if (mineBody != null)
                mineBody.linearVelocity = driftDirection * Mathf.Max(0.25f, mine.EffectiveMoveSpeed);
        }
    }

    void SpawnDefensiveAutoCannon()
    {
        if (!PhotonNetwork.InRoom || view == null)
            return;

        Vector2 forward = ResolveForwardDirection();
        Vector2 behind = -forward;
        Vector2 side = new Vector2(-forward.y, forward.x);
        float sideOffset = Random.Range(-0.22f, 0.22f);
        Vector3 spawnPosition = transform.position + (Vector3)(behind * AutoCannonRearOffset + side * sideOffset);
        float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - 90f;
        GameObject cannonObject = PhotonNetwork.Instantiate(
            "Player",
            spawnPosition,
            Quaternion.Euler(0f, 0f, angle),
            0,
            new object[] { PlayerDeployableRuntime.ContainerShipAutoCannonMarker, view.ViewID });

        if (cannonObject != null)
            PlayerDeployableRuntime.EnsureAttached(cannonObject);
    }

    Vector2 ResolveForwardDirection()
    {
        if (lastMoveDirection.sqrMagnitude > 0.001f)
            return lastMoveDirection.normalized;

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.001f)
            return rb.linearVelocity.normalized;

        return Vector2.left;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredVelocity)
    {
        if (rb == null || desiredVelocity.sqrMagnitude <= 0.001f)
            return desiredVelocity;

        Vector2 desiredDirection = desiredVelocity.normalized;
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desiredDirection * Mathf.Max(1.6f, bot.EffectiveMoveSpeed * 2.2f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= 1f;
        else if (predicted.x < -halfX)
            inward.x += 1f;

        if (predicted.y > halfY)
            inward.y -= 1f;
        else if (predicted.y < -halfY)
            inward.y += 1f;

        if (inward.sqrMagnitude <= 0.001f)
            return desiredVelocity;

        Vector2 steered = (desiredDirection * 0.42f + inward.normalized * 0.58f).normalized;
        return steered * desiredVelocity.magnitude;
    }

    void EnsureCargoVisual()
    {
        if (bot == null)
            return;

        Sprite cargoSprite = GetCargoSprite(bot.ContainerShipCargoVariantIndex);
        if (cargoSprite == null)
            return;

        if (cargoRenderer == null)
        {
            Transform existing = transform.Find(CargoVisualName);
            GameObject cargoObject = existing != null ? existing.gameObject : new GameObject(CargoVisualName);
            cargoObject.transform.SetParent(transform, false);
            cargoObject.transform.localPosition = Vector3.zero;
            cargoObject.transform.localRotation = Quaternion.identity;
            cargoRenderer = cargoObject.GetComponent<SpriteRenderer>();
            if (cargoRenderer == null)
                cargoRenderer = cargoObject.AddComponent<SpriteRenderer>();
        }

        cargoRenderer.sprite = cargoSprite;
        cargoRenderer.color = Color.white;
        cargoRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        SpriteRenderer hullRenderer = bot.GetComponent<SpriteRenderer>();
        cargoRenderer.sortingOrder = hullRenderer != null ? hullRenderer.sortingOrder + 1 : GameVisualTheme.EnemySortingOrder + 1;
        FitCargoSpriteToTargetSize(cargoRenderer, CargoVisualTargetSize);
    }

    public void HideCargoVisual()
    {
        Transform cargoTransform = cargoRenderer != null ? cargoRenderer.transform : transform.Find(CargoVisualName);
        if (cargoTransform == null)
            return;

        SpriteRenderer[] renderers = cargoTransform.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }

        cargoTransform.gameObject.SetActive(false);
        Destroy(cargoTransform.gameObject);
        cargoRenderer = null;
    }

    static Sprite GetCargoSprite(int variantIndex)
    {
        if (cachedCargoTopSprites == null || cachedCargoTopSprites.Length == 0)
            cachedCargoTopSprites = Resources.LoadAll<Sprite>(CargoTopResourcePath);

        if (cachedCargoTopSprites == null || cachedCargoTopSprites.Length == 0)
            return null;

        string expectedName = "container_set2_top_" + Mathf.Clamp(variantIndex, 0, InventoryItemCatalog.BlueprintScrapContainerVariantCount - 1);
        for (int i = 0; i < cachedCargoTopSprites.Length; i++)
        {
            Sprite sprite = cachedCargoTopSprites[i];
            if (sprite != null && string.Equals(sprite.name, expectedName, System.StringComparison.Ordinal))
                return sprite;
        }

        int clampedIndex = Mathf.Clamp(variantIndex, 0, cachedCargoTopSprites.Length - 1);
        return cachedCargoTopSprites[clampedIndex];
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }

    static void FitCargoSpriteToTargetSize(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds spriteBounds = renderer.sprite.bounds;
        float largestDimension = Mathf.Max(spriteBounds.size.x, spriteBounds.size.y);
        if (largestDimension <= 0.0001f)
            return;

        Vector3 parentScale = renderer.transform.parent != null ? renderer.transform.parent.lossyScale : Vector3.one;
        float inheritedScale = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(parentScale.x), Mathf.Abs(parentScale.y)));
        float scale = targetSize / (largestDimension * inheritedScale);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }
}

