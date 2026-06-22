using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class EngineThrusterVFX : MonoBehaviour
{
    const string ThrusterRootName = "EngineVFX";
    const float PlayerTrailMinVertexDistance = 0.07f;
    const float EnemyTrailMinVertexDistance = 0.08f;
    const float AstronautTrailMinVertexDistance = 0.06f;
    const float PlayerAstronautMinTrailTime = 0.2f;
    const float PlayerAstronautMaxTrailTime = 0.42f;
    const float PlayerAstronautMinTrailWidth = 0.028f;
    const float PlayerAstronautMaxTrailWidth = 0.085f;
    const float EnemyAstronautMinTrailTime = 0.18f;
    const float EnemyAstronautMaxTrailTime = 0.36f;
    const float EnemyAstronautMinTrailWidth = 0.024f;
    const float EnemyAstronautMaxTrailWidth = 0.072f;
    const float EscapePodMinTrailTime = 0.28f;
    const float EscapePodMaxTrailTime = 0.52f;
    const float EscapePodMinTrailWidth = 0.06f;
    const float EscapePodMaxTrailWidth = 0.16f;
    const float BoostBurstDefaultDuration = 0.48f;

    struct TrailPalette
    {
        public Color Core;
        public Color Hot;
        public Color Body;
        public Color Tail;
        public Color Glow;

        public TrailPalette(Color core, Color hot, Color body, Color tail, Color glow)
        {
            Core = core;
            Hot = hot;
            Body = body;
            Tail = tail;
            Glow = glow;
        }
    }

    static Material sharedSpritesMaterial;

    PlayerMovement movement;
    HideInNebulaTarget nebulaTarget;
    PhotonView photonViewCache;
    Rigidbody2D rb;
    SpriteRenderer shipRenderer;
    TrailRenderer[] trailRenderers;
    SpriteRenderer[] nozzleGlowRenderers;
    float referenceSpeed = 5f;
    bool isEnemyBot;
    bool isAstronaut;
    bool isEnemyAstronaut;
    bool isEscapePod;
    string playerEngineTrailItemId = string.Empty;
    EnemyTrailProfile enemyTrailProfile;
    Vector2[] playerThrusterOffsetFactors = new[] { new Vector2(0f, 0.02f) };
    Vector3 lastVisualPosition;
    bool hasLastVisualPosition;
    float boostBurstStartedAt = -1f;
    float boostBurstUntil = -1f;
    float boostBurstDuration = BoostBurstDefaultDuration;
    float boostBurstStrength;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        shipRenderer = GetComponent<SpriteRenderer>();
        movement = GetComponent<PlayerMovement>();
        nebulaTarget = GetComponent<HideInNebulaTarget>();
        photonViewCache = GetComponent<PhotonView>();
        referenceSpeed = Mathf.Max(1f, movement != null ? movement.speed : 5f);
        lastVisualPosition = transform.position;
        hasLastVisualPosition = true;
        RefreshMode();
        CreateThrusterObjects();
        UpdateVisuals(0f);
    }

    public void RefreshMode()
    {
        bool previousIsAstronaut = isAstronaut;
        bool previousIsEnemyAstronaut = isEnemyAstronaut;
        bool previousIsEscapePod = isEscapePod;
        int previousTrailCount = trailRenderers != null ? trailRenderers.Length : -1;
        string previousEngineTrailItemId = playerEngineTrailItemId;
        EnemyBot enemyBot = GetComponent<EnemyBot>();
        isEnemyBot = enemyBot != null;
        enemyTrailProfile = enemyBot != null && enemyBot.Definition != null ? enemyBot.Definition.Trails : null;
        AstronautSurvivor astronaut = GetComponent<AstronautSurvivor>();
        isAstronaut = astronaut != null;
        isEnemyAstronaut = astronaut != null && astronaut.IsEnemySurvivor;
        isEscapePod = astronaut != null && astronaut.IsEscapePodMode;
        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        playerEngineTrailItemId = ResolvePlayerEngineTrailItemId();

        if (photonViewCache == null)
            photonViewCache = GetComponent<PhotonView>();

        PhotonView view = photonViewCache;
        int skinIndex = view != null && view.Owner != null ? RoomSettings.GetPlayerShipSkin(view.Owner, 0) : 0;
        playerThrusterOffsetFactors = !isEnemyBot && !isAstronaut
            ? ShipCatalog.GetThrusterOffsetFactors(skinIndex)
            : isEscapePod
                ? new[] { new Vector2(-0.18f, 0.02f), new Vector2(0.18f, 0.02f) }
                : new[] { new Vector2(0f, 0.02f) };

        int desiredTrailCount = ResolveDesiredTrailCount();
        bool shouldRecreateTrails = trailRenderers != null &&
                                    (previousIsAstronaut != isAstronaut ||
                                     previousIsEnemyAstronaut != isEnemyAstronaut ||
                                     previousIsEscapePod != isEscapePod ||
                                     previousTrailCount != desiredTrailCount);
        if (shouldRecreateTrails)
            CreateThrusterObjects();
        else
        {
            if (!string.Equals(previousEngineTrailItemId, playerEngineTrailItemId, StringComparison.Ordinal))
                RefreshNozzleGlowAppearance();
            ReapplyTrailAppearance();
        }
    }

    void Update()
    {
        if (rb == null)
            return;

        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (movement != null)
            referenceSpeed = Mathf.Max(1f, movement.CurrentSpeedReference);

        string desiredEngineTrailItemId = ResolvePlayerEngineTrailItemId();
        if (!string.Equals(desiredEngineTrailItemId, playerEngineTrailItemId, StringComparison.Ordinal))
        {
            int previousTrailCount = trailRenderers != null ? trailRenderers.Length : -1;
            playerEngineTrailItemId = desiredEngineTrailItemId;
            if (previousTrailCount >= 0 && previousTrailCount != ResolveDesiredTrailCount())
                CreateThrusterObjects();
            else
            {
                ReapplyTrailAppearance();
                RefreshNozzleGlowAppearance();
            }
        }

        float speedNormalized = Mathf.InverseLerp(0.02f, GetVisualReferenceSpeed(), ResolveVisualSpeedMagnitude());
        UpdateVisuals(speedNormalized);
    }

    float ResolveVisualSpeedMagnitude()
    {
        float rigidbodySpeed = rb != null ? rb.linearVelocity.magnitude : 0f;
        PhotonView view = photonViewCache != null ? photonViewCache : GetComponent<PhotonView>();
        photonViewCache = view;

        if (view == null || view.IsMine || !PhotonNetwork.InRoom)
        {
            lastVisualPosition = transform.position;
            hasLastVisualPosition = true;
            return rigidbodySpeed;
        }

        Vector3 currentPosition = transform.position;
        float transformSpeed = 0f;
        if (hasLastVisualPosition && Time.deltaTime > 0.0001f)
            transformSpeed = Vector2.Distance(currentPosition, lastVisualPosition) / Time.deltaTime;

        lastVisualPosition = currentPosition;
        hasLastVisualPosition = true;
        return Mathf.Max(rigidbodySpeed, transformSpeed);
    }

    void CreateThrusterObjects()
    {
        Transform existing = transform.Find(ThrusterRootName);
        GameObject rootObject = existing != null ? existing.gameObject : new GameObject(ThrusterRootName);
        rootObject.transform.SetParent(transform, false);

        for (int i = rootObject.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(rootObject.transform.GetChild(i).gameObject);
        }

        float shipHeight = shipRenderer != null ? shipRenderer.bounds.size.y : 1f;
        float shipWidth = shipRenderer != null ? shipRenderer.bounds.size.x : 1f;
        rootObject.transform.localPosition = enemyTrailProfile != null
            ? new Vector3(enemyTrailProfile.RootOffsetFactors.x * shipWidth, enemyTrailProfile.RootOffsetFactors.y * shipHeight, 0f)
            : isEnemyBot
                ? new Vector3(0f, shipHeight * 0.44f, 0f)
            : isAstronaut
                ? new Vector3(0f, shipHeight * (isEnemyAstronaut ? 0.34f : isEscapePod ? -0.42f : -0.34f), 0f)
                : new Vector3(0f, -shipHeight * 0.46f, 0f);
        rootObject.transform.localRotation = enemyTrailProfile != null
            ? Quaternion.Euler(0f, 0f, enemyTrailProfile.RootRotationZ)
            : isEnemyBot
                ? Quaternion.identity
                : isEnemyAstronaut
                    ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, 180f);

        Vector3[] offsets = enemyTrailProfile != null
            ? BuildEnemyTrailOffsets(enemyTrailProfile, shipWidth, shipHeight)
            : BuildPlayerTrailOffsets(shipWidth, shipHeight);

        trailRenderers = new TrailRenderer[offsets.Length];
        nozzleGlowRenderers = new SpriteRenderer[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject trailObject = new GameObject("EngineTrail" + i);
            trailObject.transform.SetParent(rootObject.transform, false);
            trailObject.transform.localPosition = offsets[i];
            trailRenderers[i] = trailObject.AddComponent<TrailRenderer>();
            ConfigureTrail(trailRenderers[i]);

            GameObject glowObject = new GameObject("EngineNozzleGlow" + i);
            glowObject.transform.SetParent(rootObject.transform, false);
            glowObject.transform.localPosition = offsets[i] + new Vector3(0f, 0f, -0.01f);
            SpriteRenderer glowRenderer = glowObject.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = EngineTrailVisualUtility.GetNozzleGlowSprite();
            glowRenderer.sharedMaterial = EngineTrailVisualUtility.GetSpritesMaterial();
            glowRenderer.enabled = false;
            nozzleGlowRenderers[i] = glowRenderer;
        }

        RefreshNozzleGlowAppearance();
    }

    void ConfigureTrail(TrailRenderer trail)
    {
        trail.time = ResolveInitialMaxTrailTime();
        trail.minVertexDistance = ResolveTrailMinVertexDistance();
        trail.widthMultiplier = ResolveInitialMaxTrailWidth();
        EngineTrailVisualUtility.ConfigureTrailBase(trail);
        ApplyTrailAppearance(trail);
    }

    Vector3[] BuildPlayerTrailOffsets(float shipWidth, float shipHeight)
    {
        Vector2[] factors = playerThrusterOffsetFactors != null && playerThrusterOffsetFactors.Length > 0
            ? playerThrusterOffsetFactors
            : new[] { new Vector2(0f, 0.02f) };

        if (IsDoubleEngineTrail())
        {
            List<Vector3> splitOffsets = new List<Vector3>(factors.Length * 2);
            for (int i = 0; i < factors.Length; i++)
            {
                float side = Mathf.Abs(factors[i].x) < 0.05f ? 0.13f : 0.075f * Mathf.Sign(factors[i].x);
                if (Mathf.Abs(factors[i].x) < 0.05f)
                {
                    splitOffsets.Add(new Vector3((factors[i].x - side) * shipWidth, factors[i].y * shipHeight, 0f));
                    splitOffsets.Add(new Vector3((factors[i].x + side) * shipWidth, factors[i].y * shipHeight, 0f));
                }
                else
                {
                    splitOffsets.Add(new Vector3(factors[i].x * shipWidth, factors[i].y * shipHeight, 0f));
                    splitOffsets.Add(new Vector3((factors[i].x + side) * shipWidth, factors[i].y * shipHeight, 0f));
                }
            }

            return splitOffsets.ToArray();
        }

        Vector3[] offsets = new Vector3[factors.Length];
        for (int i = 0; i < factors.Length; i++)
            offsets[i] = new Vector3(factors[i].x * shipWidth, factors[i].y * shipHeight, 0f);

        return offsets;
    }

    int ResolveDesiredTrailCount()
    {
        if (enemyTrailProfile != null && enemyTrailProfile.TrailOffsetFactors != null)
            return enemyTrailProfile.TrailOffsetFactors.Length;

        Vector2[] factors = playerThrusterOffsetFactors != null && playerThrusterOffsetFactors.Length > 0
            ? playerThrusterOffsetFactors
            : new[] { new Vector2(0f, 0.02f) };

        return IsDoubleEngineTrail() ? factors.Length * 2 : factors.Length;
    }

    bool IsDoubleEngineTrail()
    {
        return !isEnemyBot &&
               !isAstronaut &&
               string.Equals(playerEngineTrailItemId, InventoryItemCatalog.DoubleEngineId, StringComparison.Ordinal);
    }

    float ResolveInitialMaxTrailTime()
    {
        if (isAstronaut)
            return isEnemyAstronaut ? EnemyAstronautMaxTrailTime : isEscapePod ? EscapePodMaxTrailTime : PlayerAstronautMaxTrailTime;

        if (enemyTrailProfile != null)
            return Mathf.Max(enemyTrailProfile.MinTrailTime, enemyTrailProfile.MaxTrailTime);

        return ResolvePlayerMaxTrailTime();
    }

    float ResolveInitialMaxTrailWidth()
    {
        if (isAstronaut)
            return isEnemyAstronaut ? EnemyAstronautMaxTrailWidth : isEscapePod ? EscapePodMaxTrailWidth : PlayerAstronautMaxTrailWidth;

        if (enemyTrailProfile != null)
            return enemyTrailProfile.MaxTrailWidth;

        return ResolvePlayerMaxTrailWidth();
    }

    float ResolveTrailMinVertexDistance()
    {
        if (isAstronaut)
            return isEscapePod ? 0.05f : AstronautTrailMinVertexDistance;

        if (enemyTrailProfile != null)
            return EnemyTrailMinVertexDistance;

        return string.Equals(playerEngineTrailItemId, InventoryItemCatalog.IonEngineId, StringComparison.Ordinal)
            ? 0.055f
            : PlayerTrailMinVertexDistance;
    }

    string ResolvePlayerEngineTrailItemId()
    {
        if (isEnemyBot || isAstronaut || movement == null)
            return string.Empty;

        return movement.CurrentEngineTrailItemId ?? string.Empty;
    }

    bool HasPlayerEngineTrailOverride()
    {
        return !string.IsNullOrWhiteSpace(playerEngineTrailItemId);
    }

    float ResolvePlayerMinTrailTime()
    {
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.IonEngineId, StringComparison.Ordinal))
            return 0.13f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.AfterburnerStabilizerId, StringComparison.Ordinal))
            return 0.15f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.FuelTankId, StringComparison.Ordinal))
            return 0.24f;

        return 0.18f;
    }

    float ResolvePlayerMaxTrailTime()
    {
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.PowerEngineId, StringComparison.Ordinal))
            return 0.66f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.IonEngineId, StringComparison.Ordinal))
            return 0.48f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.FusionEngineId, StringComparison.Ordinal))
            return 0.92f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.HybridEngineId, StringComparison.Ordinal))
            return 0.74f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.DoubleEngineId, StringComparison.Ordinal))
            return 0.82f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.FuelTankId, StringComparison.Ordinal))
            return 0.96f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
            return 0.86f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.AfterburnerStabilizerId, StringComparison.Ordinal))
            return 0.58f;

        return 0.58f;
    }

    float ResolvePlayerMinTrailWidth()
    {
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.IonEngineId, StringComparison.Ordinal))
            return 0.022f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.DoubleEngineId, StringComparison.Ordinal))
            return 0.034f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.PowerEngineId, StringComparison.Ordinal))
            return 0.036f;

        return 0.028f;
    }

    float ResolvePlayerMaxTrailWidth()
    {
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.PowerEngineId, StringComparison.Ordinal))
            return 0.145f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.IonEngineId, StringComparison.Ordinal))
            return 0.09f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.FusionEngineId, StringComparison.Ordinal))
            return 0.15f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.HybridEngineId, StringComparison.Ordinal))
            return 0.13f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.DoubleEngineId, StringComparison.Ordinal))
            return 0.14f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.FuelTankId, StringComparison.Ordinal))
            return 0.12f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
            return 0.16f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.AfterburnerStabilizerId, StringComparison.Ordinal))
            return 0.12f;

        return 0.12f;
    }

    float ResolveGlowScale()
    {
        if (isEscapePod)
            return 0.24f;
        if (isAstronaut)
            return isEnemyAstronaut ? 0.12f : 0.13f;
        if (enemyTrailProfile != null)
            return Mathf.Clamp(enemyTrailProfile.MaxTrailWidth * 1.45f, 0.12f, 0.85f);
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
            return 0.24f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.FusionEngineId, StringComparison.Ordinal))
            return 0.21f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.DoubleEngineId, StringComparison.Ordinal))
            return 0.2f;

        return 0.18f;
    }

    float ResolveGlowPulseSpeed()
    {
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.AfterburnerStabilizerId, StringComparison.Ordinal))
            return 30f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.IonEngineId, StringComparison.Ordinal))
            return 22f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
            return 26f;
        if (enemyTrailProfile != null && enemyTrailProfile.MaxTrailTime > 1.2f)
            return 8f;

        return isAstronaut ? 18f : 14f;
    }

    float ResolveGlowPulseStrength()
    {
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.AfterburnerStabilizerId, StringComparison.Ordinal))
            return 0.26f;
        if (string.Equals(playerEngineTrailItemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
            return 0.22f;
        if (enemyTrailProfile != null && enemyTrailProfile.MaxTrailTime > 1.2f)
            return 0.08f;

        return 0.14f;
    }

    TrailPalette ResolveTrailPalette()
    {
        if (isAstronaut && isEnemyAstronaut)
        {
            return new TrailPalette(
                new Color(0.9f, 1f, 1f),
                new Color(0.42f, 0.88f, 1f),
                new Color(0.08f, 0.42f, 1f),
                new Color(0.02f, 0.08f, 0.28f),
                new Color(0.28f, 0.82f, 1f, 0.62f));
        }

        if (isAstronaut && isEscapePod)
        {
            return new TrailPalette(
                new Color(1f, 0.98f, 0.86f),
                new Color(1f, 0.78f, 0.28f),
                new Color(1f, 0.42f, 0.08f),
                new Color(0.32f, 0.06f, 0.01f),
                new Color(1f, 0.42f, 0.08f, 0.7f));
        }

        if (isAstronaut)
        {
            return new TrailPalette(
                new Color(1f, 0.94f, 0.76f),
                new Color(1f, 0.7f, 0.3f),
                new Color(0.96f, 0.42f, 0.12f),
                new Color(0.42f, 0.11f, 0.02f),
                new Color(1f, 0.55f, 0.16f, 0.58f));
        }

        if (enemyTrailProfile != null)
            return ResolveEnemyTrailPalette(enemyTrailProfile.VisualStyle);

        if (HasPlayerEngineTrailOverride())
            return ResolvePlayerTrailPalette(playerEngineTrailItemId);

        return new TrailPalette(
            new Color(1f, 1f, 1f),
            new Color(0.64f, 0.97f, 1f),
            new Color(0.2f, 0.8f, 1f),
            new Color(0.03f, 0.18f, 0.86f),
            new Color(0.3f, 0.84f, 1f, 0.62f));
    }

    TrailPalette ResolveEnemyTrailPalette(EnemyTrailVisualStyle style)
    {
        switch (style)
        {
            case EnemyTrailVisualStyle.RedLarge:
                return new TrailPalette(
                    new Color(1f, 0.94f, 0.9f),
                    new Color(1f, 0.32f, 0.22f),
                    new Color(0.9f, 0.04f, 0.04f),
                    new Color(0.34f, 0.01f, 0.01f),
                    new Color(1f, 0.18f, 0.08f, 0.7f));
            case EnemyTrailVisualStyle.GreenTwin:
                return new TrailPalette(
                    new Color(0.86f, 1f, 0.72f),
                    new Color(0.22f, 1f, 0.36f),
                    new Color(0.02f, 0.72f, 0.18f),
                    new Color(0f, 0.18f, 0.04f),
                    new Color(0.24f, 1f, 0.42f, 0.62f));
            case EnemyTrailVisualStyle.BlueTwin:
                return new TrailPalette(
                    new Color(0.88f, 0.98f, 1f),
                    new Color(0.32f, 0.78f, 1f),
                    new Color(0.02f, 0.48f, 0.98f),
                    new Color(0f, 0.1f, 0.26f),
                    new Color(0.22f, 0.76f, 1f, 0.62f));
            case EnemyTrailVisualStyle.PurpleLarge:
                return new TrailPalette(
                    new Color(0.94f, 0.82f, 1f),
                    new Color(0.62f, 0.18f, 1f),
                    new Color(0.28f, 0.02f, 0.72f),
                    new Color(0.07f, 0f, 0.2f),
                    new Color(0.64f, 0.2f, 1f, 0.68f));
            case EnemyTrailVisualStyle.OrangeRedTwin:
                return new TrailPalette(
                    new Color(1f, 0.96f, 0.76f),
                    new Color(1f, 0.48f, 0.12f),
                    new Color(0.96f, 0.12f, 0.04f),
                    new Color(0.32f, 0.02f, 0.01f),
                    new Color(1f, 0.34f, 0.08f, 0.66f));
            default:
                return new TrailPalette(
                    new Color(1f, 0.98f, 0.78f),
                    new Color(1f, 0.7f, 0.22f),
                    new Color(0.95f, 0.34f, 0.04f),
                    new Color(0.45f, 0.08f, 0.01f),
                    new Color(1f, 0.42f, 0.08f, 0.6f));
        }
    }

    TrailPalette ResolvePlayerTrailPalette(string engineItemId)
    {
        if (string.Equals(engineItemId, InventoryItemCatalog.PowerEngineId, StringComparison.Ordinal))
            return new TrailPalette(new Color(1f, 1f, 0.72f), new Color(1f, 0.78f, 0.14f), new Color(0.92f, 0.42f, 0.04f), new Color(0.38f, 0.12f, 0f), new Color(1f, 0.72f, 0.1f, 0.68f));
        if (string.Equals(engineItemId, InventoryItemCatalog.IonEngineId, StringComparison.Ordinal))
            return new TrailPalette(new Color(0.78f, 1f, 1f), new Color(0.16f, 0.88f, 1f), new Color(0.02f, 0.42f, 0.96f), new Color(0f, 0.08f, 0.34f), new Color(0.16f, 0.82f, 1f, 0.7f));
        if (string.Equals(engineItemId, InventoryItemCatalog.FusionEngineId, StringComparison.Ordinal))
            return new TrailPalette(new Color(0.84f, 0.72f, 1f), new Color(0.48f, 0.18f, 0.76f), new Color(0.22f, 0.04f, 0.42f), new Color(0.06f, 0.01f, 0.14f), new Color(0.64f, 0.22f, 1f, 0.76f));
        if (string.Equals(engineItemId, InventoryItemCatalog.HybridEngineId, StringComparison.Ordinal))
            return new TrailPalette(new Color(0.82f, 1f, 0.82f), new Color(0.22f, 1f, 0.68f), new Color(0.02f, 0.56f, 0.46f), new Color(0f, 0.16f, 0.12f), new Color(0.18f, 1f, 0.68f, 0.66f));
        if (string.Equals(engineItemId, InventoryItemCatalog.DoubleEngineId, StringComparison.Ordinal))
            return new TrailPalette(new Color(1f, 0.92f, 0.82f), new Color(1f, 0.18f, 0.1f), new Color(0.78f, 0f, 0f), new Color(0.24f, 0f, 0f), new Color(1f, 0.22f, 0.08f, 0.72f));
        if (string.Equals(engineItemId, InventoryItemCatalog.FuelTankId, StringComparison.Ordinal))
            return new TrailPalette(new Color(1f, 0.95f, 0.64f), new Color(1f, 0.55f, 0.08f), new Color(0.75f, 0.28f, 0.03f), new Color(0.26f, 0.08f, 0f), new Color(1f, 0.48f, 0.08f, 0.6f));
        if (string.Equals(engineItemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
            return new TrailPalette(new Color(0.86f, 0.9f, 1f), new Color(0.24f, 0.46f, 1f), new Color(0.1f, 0.16f, 0.9f), new Color(0.02f, 0.02f, 0.32f), new Color(0.24f, 0.44f, 1f, 0.82f));
        if (string.Equals(engineItemId, InventoryItemCatalog.AfterburnerStabilizerId, StringComparison.Ordinal))
            return new TrailPalette(new Color(1f, 0.82f, 1f), new Color(1f, 0.25f, 0.82f), new Color(0.66f, 0.04f, 0.45f), new Color(0.18f, 0f, 0.14f), new Color(1f, 0.22f, 0.84f, 0.72f));

        return new TrailPalette(
            new Color(1f, 1f, 1f),
            new Color(0.64f, 0.97f, 1f),
            new Color(0.2f, 0.8f, 1f),
            new Color(0.03f, 0.18f, 0.86f),
            new Color(0.3f, 0.84f, 1f, 0.62f));
    }

    void ApplyTrailAppearance(TrailRenderer trail)
    {
        if (trail == null)
            return;

        TrailPalette palette = ResolveTrailPalette();
        trail.colorGradient = EngineTrailVisualUtility.BuildEngineGradient(
            palette.Core,
            palette.Hot,
            palette.Body,
            palette.Tail);
        trail.widthCurve = isAstronaut
            ? EngineTrailVisualUtility.BuildSoftWidthCurve()
            : EngineTrailVisualUtility.BuildShipWidthCurve();

        if (shipRenderer != null)
        {
            trail.sortingLayerID = shipRenderer.sortingLayerID;
            trail.sortingOrder = shipRenderer.sortingOrder - 2;
        }
    }

    void SetPlayerEngineTrailGradient(Gradient gradient, string engineItemId)
    {
        if (gradient == null)
            return;

        if (string.Equals(engineItemId, InventoryItemCatalog.PowerEngineId, StringComparison.Ordinal))
        {
            SetTrailGradient(gradient,
                new Color(1f, 1f, 0.72f),
                new Color(1f, 0.78f, 0.14f),
                new Color(0.92f, 0.42f, 0.04f),
                new Color(0.38f, 0.12f, 0f));
            return;
        }

        if (string.Equals(engineItemId, InventoryItemCatalog.IonEngineId, StringComparison.Ordinal))
        {
            SetTrailGradient(gradient,
                new Color(0.78f, 1f, 1f),
                new Color(0.16f, 0.88f, 1f),
                new Color(0.02f, 0.42f, 0.96f),
                new Color(0f, 0.08f, 0.34f));
            return;
        }

        if (string.Equals(engineItemId, InventoryItemCatalog.FusionEngineId, StringComparison.Ordinal))
        {
            SetTrailGradient(gradient,
                new Color(0.84f, 0.72f, 1f),
                new Color(0.48f, 0.18f, 0.76f),
                new Color(0.22f, 0.04f, 0.42f),
                new Color(0.06f, 0.01f, 0.14f));
            return;
        }

        if (string.Equals(engineItemId, InventoryItemCatalog.HybridEngineId, StringComparison.Ordinal))
        {
            SetTrailGradient(gradient,
                new Color(0.82f, 1f, 0.82f),
                new Color(0.22f, 1f, 0.68f),
                new Color(0.02f, 0.56f, 0.46f),
                new Color(0f, 0.16f, 0.12f));
            return;
        }

        if (string.Equals(engineItemId, InventoryItemCatalog.DoubleEngineId, StringComparison.Ordinal))
        {
            SetTrailGradient(gradient,
                new Color(1f, 0.92f, 0.82f),
                new Color(1f, 0.18f, 0.1f),
                new Color(0.78f, 0f, 0f),
                new Color(0.24f, 0f, 0f));
            return;
        }

        if (string.Equals(engineItemId, InventoryItemCatalog.FuelTankId, StringComparison.Ordinal))
        {
            SetTrailGradient(gradient,
                new Color(1f, 0.95f, 0.64f),
                new Color(1f, 0.55f, 0.08f),
                new Color(0.75f, 0.28f, 0.03f),
                new Color(0.26f, 0.08f, 0f));
            return;
        }

        if (string.Equals(engineItemId, InventoryItemCatalog.SuperBoosterId, StringComparison.Ordinal))
        {
            SetTrailGradient(gradient,
                new Color(0.86f, 0.9f, 1f),
                new Color(0.24f, 0.46f, 1f),
                new Color(0.1f, 0.16f, 0.9f),
                new Color(0.02f, 0.02f, 0.32f));
            return;
        }

        if (string.Equals(engineItemId, InventoryItemCatalog.AfterburnerStabilizerId, StringComparison.Ordinal))
        {
            SetTrailGradient(gradient,
                new Color(1f, 0.82f, 1f),
                new Color(1f, 0.25f, 0.82f),
                new Color(0.66f, 0.04f, 0.45f),
                new Color(0.18f, 0f, 0.14f));
            return;
        }

        SetTrailGradient(gradient,
            new Color(1f, 1f, 1f),
            new Color(0.64f, 0.97f, 1f),
            new Color(0.2f, 0.8f, 1f),
            new Color(0.03f, 0.18f, 0.86f));
    }

    static void SetTrailGradient(Gradient gradient, Color core, Color hot, Color body, Color tail)
    {
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(core, 0f),
                new GradientColorKey(hot, 0.18f),
                new GradientColorKey(body, 0.54f),
                new GradientColorKey(tail, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.92f, 0f),
                new GradientAlphaKey(0.62f, 0.26f),
                new GradientAlphaKey(0.24f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
    }

    void ReapplyTrailAppearance()
    {
        if (trailRenderers == null)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            ApplyTrailAppearance(trailRenderers[i]);
        }
    }

    public void DisableAndClearTrails()
    {
        if (trailRenderers == null)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            if (trailRenderers[i] == null)
                continue;

            trailRenderers[i].emitting = false;
            trailRenderers[i].Clear();
            trailRenderers[i].gameObject.SetActive(false);
        }

        if (nozzleGlowRenderers != null)
        {
            for (int i = 0; i < nozzleGlowRenderers.Length; i++)
            {
                if (nozzleGlowRenderers[i] != null)
                    nozzleGlowRenderers[i].gameObject.SetActive(false);
            }
        }

        enabled = false;
    }

    public void TriggerBoostBurst(float duration = BoostBurstDefaultDuration, float strength = 1f)
    {
        boostBurstDuration = Mathf.Max(0.05f, duration);
        boostBurstStartedAt = Time.time;
        boostBurstUntil = Time.time + boostBurstDuration;
        boostBurstStrength = Mathf.Max(boostBurstStrength, Mathf.Max(0f, strength));
    }

    void UpdateVisuals(float speedNormalized)
    {
        if (nebulaTarget == null)
            nebulaTarget = GetComponent<HideInNebulaTarget>();
        if (photonViewCache == null)
            photonViewCache = GetComponent<PhotonView>();

        bool hideForOthers = nebulaTarget != null && nebulaTarget.IsHiddenFromLocalPlayer() && photonViewCache != null && !photonViewCache.IsMine;
        if (hideForOthers)
        {
            DisableEmission();
            return;
        }

        float clamped = Mathf.Clamp01(speedNormalized);
        float intensity = Mathf.Lerp(isAstronaut ? (isEscapePod ? 0.36f : 0.28f) : 0.18f, 1f, clamped);
        float burst = GetBoostBurstIntensity();
        float widthBurstMultiplier = 1f + burst * (isAstronaut ? 0.35f : 0.72f);
        float timeBurstMultiplier = 1f + burst * 0.18f;

        if (trailRenderers == null)
            return;

        EnsureTrailSorting();

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trailRenderer = trailRenderers[i];
            if (trailRenderer == null)
                continue;

            if (isAstronaut)
            {
                if (isEnemyAstronaut)
                {
                    trailRenderer.time = Mathf.Lerp(EnemyAstronautMinTrailTime, EnemyAstronautMaxTrailTime, intensity) * timeBurstMultiplier;
                    trailRenderer.widthMultiplier = Mathf.Lerp(EnemyAstronautMinTrailWidth, EnemyAstronautMaxTrailWidth, intensity) * widthBurstMultiplier;
                    trailRenderer.emitting = clamped > 0.025f || burst > 0.02f;
                }
                else if (isEscapePod)
                {
                    trailRenderer.time = Mathf.Lerp(EscapePodMinTrailTime, EscapePodMaxTrailTime, intensity) * timeBurstMultiplier;
                    trailRenderer.widthMultiplier = Mathf.Lerp(EscapePodMinTrailWidth, EscapePodMaxTrailWidth, intensity) * widthBurstMultiplier;
                    trailRenderer.emitting = clamped > 0.035f || burst > 0.02f;
                }
                else
                {
                    trailRenderer.time = Mathf.Lerp(PlayerAstronautMinTrailTime, PlayerAstronautMaxTrailTime, intensity) * timeBurstMultiplier;
                    trailRenderer.widthMultiplier = Mathf.Lerp(PlayerAstronautMinTrailWidth, PlayerAstronautMaxTrailWidth, intensity) * widthBurstMultiplier;
                    trailRenderer.emitting = clamped > 0.035f || burst > 0.02f;
                }
            }
            else if (enemyTrailProfile != null)
            {
                trailRenderer.time = Mathf.Lerp(enemyTrailProfile.MinTrailTime, enemyTrailProfile.MaxTrailTime, intensity) * timeBurstMultiplier;
                trailRenderer.widthMultiplier = Mathf.Lerp(enemyTrailProfile.MinTrailWidth, enemyTrailProfile.MaxTrailWidth, intensity) * widthBurstMultiplier;
                trailRenderer.emitting = clamped > enemyTrailProfile.EmissionThreshold;
            }
            else
            {
                trailRenderer.time = Mathf.Lerp(ResolvePlayerMinTrailTime(), ResolvePlayerMaxTrailTime(), intensity) * timeBurstMultiplier;
                trailRenderer.widthMultiplier = Mathf.Lerp(ResolvePlayerMinTrailWidth(), ResolvePlayerMaxTrailWidth(), intensity) * widthBurstMultiplier;
                trailRenderer.emitting = clamped > 0.04f || burst > 0.02f;
            }

            UpdateNozzleGlow(i, clamped, intensity, burst);
        }
    }

    float GetBoostBurstIntensity()
    {
        if (Time.time >= boostBurstUntil || boostBurstDuration <= 0.001f)
        {
            boostBurstStrength = 0f;
            return 0f;
        }

        float t = Mathf.Clamp01((Time.time - boostBurstStartedAt) / boostBurstDuration);
        float envelope = Mathf.Sin(t * Mathf.PI);
        return Mathf.Clamp01(envelope * Mathf.Max(0f, boostBurstStrength));
    }

    void UpdateNozzleGlow(int index, float speedNormalized, float intensity, float burst)
    {
        if (nozzleGlowRenderers == null || index < 0 || index >= nozzleGlowRenderers.Length)
            return;

        SpriteRenderer glow = nozzleGlowRenderers[index];
        if (glow == null)
            return;

        TrailPalette palette = ResolveTrailPalette();
        float pulse = 1f + Mathf.Sin(Time.time * ResolveGlowPulseSpeed() + index * 1.73f) * ResolveGlowPulseStrength() * Mathf.Lerp(0.35f, 1f, intensity);
        float baseScale = ResolveGlowScale();
        float scale = baseScale * Mathf.Lerp(0.68f, 1.28f, intensity) * (1f + burst * 0.9f) * pulse;
        glow.transform.localScale = new Vector3(scale * 1.15f, scale * 0.82f, 1f);

        Color color = palette.Glow;
        float idleAlpha = isAstronaut ? 0.18f : 0.22f;
        float activeAlpha = Mathf.Lerp(idleAlpha, 0.72f, Mathf.Max(speedNormalized, burst));
        color.a *= Mathf.Clamp01(activeAlpha + burst * 0.38f);
        glow.color = color;
        glow.enabled = true;
    }

    float GetVisualReferenceSpeed()
    {
        if (isEnemyAstronaut)
            return Mathf.Max(0.65f, referenceSpeed / 3f);

        return Mathf.Max(0.65f, referenceSpeed);
    }

    void EnsureTrailSorting()
    {
        if (trailRenderers == null || shipRenderer == null)
            return;

        int targetLayer = shipRenderer.sortingLayerID;
        int targetOrder = shipRenderer.sortingOrder - 2;
        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];
            if (trail == null)
                continue;

            if (trail.sortingLayerID != targetLayer)
                trail.sortingLayerID = targetLayer;

            if (trail.sortingOrder != targetOrder)
                trail.sortingOrder = targetOrder;
        }

        if (nozzleGlowRenderers == null)
            return;

        int glowOrder = shipRenderer.sortingOrder - 1;
        for (int i = 0; i < nozzleGlowRenderers.Length; i++)
        {
            SpriteRenderer glow = nozzleGlowRenderers[i];
            if (glow == null)
                continue;

            glow.sortingLayerID = targetLayer;
            glow.sortingOrder = glowOrder;
        }
    }

    Vector3[] BuildEnemyTrailOffsets(EnemyTrailProfile profile, float shipWidth, float shipHeight)
    {
        if (profile != null && profile.TrailOffsetFactors != null && profile.TrailOffsetFactors.Length == 0)
            return System.Array.Empty<Vector3>();

        if (profile == null || profile.TrailOffsetFactors == null)
            return new[] { Vector3.zero };

        Vector3[] offsets = new Vector3[profile.TrailOffsetFactors.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = new Vector3(
                profile.TrailOffsetFactors[i].x * shipWidth,
                profile.TrailOffsetFactors[i].y * shipHeight,
                0f);
        }

        return offsets;
    }

    void DisableEmission()
    {
        if (trailRenderers == null)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trailRenderer = trailRenderers[i];
            if (trailRenderer == null)
                continue;

            trailRenderer.emitting = false;
            trailRenderer.Clear();
        }

        if (nozzleGlowRenderers == null)
            return;

        for (int i = 0; i < nozzleGlowRenderers.Length; i++)
        {
            if (nozzleGlowRenderers[i] != null)
                nozzleGlowRenderers[i].enabled = false;
        }
    }

    void RefreshNozzleGlowAppearance()
    {
        if (nozzleGlowRenderers == null)
            return;

        TrailPalette palette = ResolveTrailPalette();
        for (int i = 0; i < nozzleGlowRenderers.Length; i++)
        {
            SpriteRenderer glow = nozzleGlowRenderers[i];
            if (glow == null)
                continue;

            glow.color = palette.Glow;
            if (shipRenderer != null)
            {
                glow.sortingLayerID = shipRenderer.sortingLayerID;
                glow.sortingOrder = shipRenderer.sortingOrder - 1;
            }
        }
    }

    static Material GetSpritesMaterial()
    {
        if (sharedSpritesMaterial != null)
            return sharedSpritesMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        sharedSpritesMaterial = new Material(shader)
        {
            name = "EngineThrusterVFXMaterial"
        };
        return sharedSpritesMaterial;
    }
}
