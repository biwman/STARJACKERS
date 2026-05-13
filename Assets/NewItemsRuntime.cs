using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public static class NewItemsRuntime
{
    public const float SpaceTrapArmRange = 3.1f;

    public static bool TryDeployAutoTurret(PlayerShooting owner)
    {
        if (!PhotonNetwork.IsMasterClient || owner == null || owner.photonView == null)
            return false;

        Vector3 spawnPosition = ResolveRearSpawnPosition(owner.transform, 0.95f, 0.42f);
        GameObject turretObject = PhotonNetwork.InstantiateRoomObject(
            "Player",
            spawnPosition,
            owner.transform.rotation,
            0,
            new object[] { PlayerDeployableRuntime.AutoTurretMarker, owner.photonView.ViewID });

        if (turretObject == null)
            return false;

        PlayerDeployableRuntime.EnsureAttached(turretObject);
        return true;
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

    static bool IsSpawnPositionFree(Vector2 position, float radius, Transform source)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (source != null && (hit.transform == source || hit.transform.IsChildOf(source)))
                continue;

            return false;
        }

        return true;
    }
}

public static class RuntimeSpriteUtility
{
    public static Sprite LoadSprite(string resourcePath, string editorAssetPath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        sprite = GetLargestSprite(sprites);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture != null)
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(100f, Mathf.Max(texture.width, texture.height)));

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(editorAssetPath))
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(editorAssetPath);
#endif
        return sprite;
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

    public static Sprite CreateArrowSprite()
    {
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
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 64f);
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
        float clippedRange = maxRange;
        if (hits == null)
            return clippedRange;

        for (int i = 0; i < hits.Length; i++)
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
        RaycastHit2D[] hits = Physics2D.CircleCastAll(start, fullWidth ? 0.28f : 0.14f, safeDirection, Mathf.Max(0.2f, maxRange));
        return AstroCutterBeamBlocker.ResolveClippedRange(hits, source, sourceViewId, maxRange);
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
        SetVisible(true);
        nextSoundTime = 0f;
    }

    void Update()
    {
        if (photonView != null && !photonView.IsMine)
            return;

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
        PlayerHealth[] healths = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
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

public static class PlayerDeployableRuntime
{
    public const string AutoTurretMarker = "player_auto_turret";
    public const string SpaceDrillMarker = "player_space_drill";

    public static bool IsInstantiationData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               (marker == AutoTurretMarker || marker == SpaceDrillMarker);
    }

    public static PlayerDeployableBase EnsureAttached(GameObject target)
    {
        if (target == null)
            return null;

        PhotonView view = target.GetComponent<PhotonView>();
        object[] data = view != null ? view.InstantiationData : null;
        if (data == null || data.Length == 0 || !(data[0] is string marker))
            return null;

        if (marker == AutoTurretMarker)
        {
            AutoTurretDeployable turret = target.GetComponent<AutoTurretDeployable>();
            if (turret == null)
                turret = target.AddComponent<AutoTurretDeployable>();

            turret.InitializeFromPhotonData();
            return turret;
        }

        if (marker == SpaceDrillMarker)
        {
            SpaceDrillDeployable drill = target.GetComponent<SpaceDrillDeployable>();
            if (drill == null)
                drill = target.AddComponent<SpaceDrillDeployable>();

            drill.InitializeFromPhotonData();
            return drill;
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
        if (!PhotonNetwork.IsMasterClient || !CanBeTargeted)
            return;

        int damage = currentShield > 0 ? Mathf.Max(0, shieldDamage) : Mathf.Max(0, hpDamage);
        if (damage <= 0)
            return;

        int absorbed = 0;
        if (currentShield > 0)
        {
            absorbed = Mathf.Min(currentShield, damage);
            currentShield -= absorbed;
            damage -= absorbed;
        }

        if (damage > 0)
            currentHp = Mathf.Max(0, currentHp - damage);

        photonView.RPC(nameof(PlayDeployableHitRpc), RpcTarget.All, absorbed > 0, impactX, impactY);
        if (currentHp <= 0)
            DestroyOnMaster();
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
    static readonly Color BulletColor = new Color(1f, 0.62f, 0.12f, 1f);

    float nextShotTime;
    int shotsSinceBreak;
    float breakUntil;

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
        InitializeCommon();
    }

    void Update()
    {
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
        PhotonView ownerView = ownerShipViewId > 0 ? PhotonView.Find(ownerShipViewId) : null;
        int ownerActorNumber = ownerView != null ? ownerView.OwnerActorNr : -1;
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

            bool hostile = candidate.IsBotControlled || (ownerActorNumber > 0 && candidate.photonView.OwnerActorNr != ownerActorNumber);
            if (!hostile)
                continue;

            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            if (distance > TargetRange || distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = candidate.transform;
        }

        return best;
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
        GameObject bullet = PhotonNetwork.Instantiate(
            "Bullet",
            position,
            Quaternion.Euler(0f, 0f, angle),
            0,
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
                BulletEffectId,
                10f
            });

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
        AudioManager.Instance.PlayShootSmallAt(transform.position);
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
            stored = await PlayerProfileService.Instance.AddItemToShipAsync(itemId);
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
        engineTrail.time = 0.42f;
        engineTrail.minVertexDistance = 0.025f;
        engineTrail.widthMultiplier = 0.08f;
        engineTrail.material = new Material(Shader.Find("Sprites/Default"));
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
        if (miningAudioSource != null || AudioManager.Instance.DrillingClip == null)
            return;

        GameObject audioObject = new GameObject("SpaceDrillMiningAudio");
        audioObject.transform.SetParent(transform, false);
        miningAudioSource = audioObject.AddComponent<AudioSource>();
        miningAudioSource.clip = AudioManager.Instance.DrillingClip;
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

public sealed class SpaceTrapTarget : MonoBehaviourPun
{
    const int TrapDamage = 100;
    const float TrapRadius = 3.12f;
    const float TrapOwnerDamagePadding = 0.75f;
    const float TrapCollectorDamagePadding = 0.75f;
    static readonly Dictionary<int, int> ArmedOwnerByTargetView = new Dictionary<int, int>();

    int ownerViewId;
    bool armed;
    SpriteRenderer markerRenderer;

    public bool IsArmed => armed;

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
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, TrapRadius);
        HashSet<int> damagedViews = new HashSet<int>();
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
            if (deployable != null && deployable.photonView != null && damagedViews.Add(deployable.photonView.ViewID))
            {
                deployable.photonView.RPC(nameof(PlayerDeployableBase.TakeDeployableDamageAt), RpcTarget.MasterClient, TrapDamage, TrapDamage, attackerViewId, center.x, center.y);
                continue;
            }

            PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
            if (health != null && health.photonView != null && !health.IsWreck && damagedViews.Add(health.photonView.ViewID))
            {
                health.photonView.RPC(nameof(PlayerHealth.TakeDamageProfileAt), RpcTarget.MasterClient, TrapDamage, TrapDamage, attackerViewId, center.x, center.y);
                continue;
            }
        }

        TryDamageViewIfClose(attackerViewId, center, attackerViewId, damagedViews, TrapOwnerDamagePadding);
        TryDamageViewIfClose(collectorViewId, center, attackerViewId, damagedViews, TrapCollectorDamagePadding);
    }

    static void TryDamageViewIfClose(int targetViewId, Vector2 center, int attackerViewId, HashSet<int> damagedViews, float padding)
    {
        if (targetViewId <= 0)
            return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        if (targetView == null || Vector2.Distance(center, targetView.transform.position) > TrapRadius + padding)
            return;

        PlayerDeployableBase deployable = targetView.GetComponent<PlayerDeployableBase>();
        if (deployable != null && deployable.photonView != null && deployable.CanBeTargeted && damagedViews.Add(deployable.photonView.ViewID))
        {
            deployable.photonView.RPC(nameof(PlayerDeployableBase.TakeDeployableDamageAt), RpcTarget.MasterClient, TrapDamage, TrapDamage, attackerViewId, center.x, center.y);
            return;
        }

        PlayerHealth health = targetView.GetComponent<PlayerHealth>();
        if (health != null && health.photonView != null && !health.IsWreck && damagedViews.Add(health.photonView.ViewID))
            health.photonView.RPC(nameof(PlayerHealth.TakeDamageProfileAt), RpcTarget.MasterClient, TrapDamage, TrapDamage, attackerViewId, center.x, center.y);
    }
}

public sealed class LootingFriendController : MonoBehaviourPun
{
    const float FollowRightOffset = 0.72f;
    const float FollowBackOffset = -0.05f;
    const float CollectDuration = 6f;
    const float ScanInterval = 0.18f;
    const float CollectRangeMultiplier = 1.3f;
    const float VisualTargetSize = 1.08f;

    SpriteRenderer visualRenderer;
    LineRenderer beam;
    AudioSource collectAudioSource;
    Coroutine collectRoutine;
    PhotonView currentTarget;
    float nextScanTime;
    bool visualActive;

    float CollectRange => Treasure.CollectRange * CollectRangeMultiplier;

    void Start()
    {
        EnsureVisual();
        SetupCollectAudio();
    }

    void Update()
    {
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

    bool IsLootingFriendEquipped()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipment = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        if (equipment == null)
            return false;

        for (int i = 6; i <= 7; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            if (i < equipment.Length && string.Equals(equipment[i], InventoryItemCatalog.LootingFriendId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    PhotonView FindAutoLootTarget()
    {
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
        int targetViewId = target != null ? target.ViewID : 0;
        photonView.RPC(nameof(SetLootingFriendCollectFxRpc), RpcTarget.All, targetViewId, true);
        float startedAt = Time.time;
        while (Time.time < startedAt + CollectDuration)
        {
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
        if (target == null || photonView == null)
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
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null)
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
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null)
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
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null)
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
        bool stored = false;
        try
        {
            stored = await PlayerProfileService.Instance.AddItemToShipAsync(itemId);
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
        if (!PhotonNetwork.IsMasterClient || !stored)
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
        bool stored = false;
        try
        {
            stored = await PlayerProfileService.Instance.AddItemToShipAsync(itemId);
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
        if (!PhotonNetwork.IsMasterClient || !stored)
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
        RuntimeSpriteUtility.FitRenderer(visualRenderer, VisualTargetSize);
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
        if (collectAudioSource != null || visualRenderer == null || AudioManager.Instance.DrillingClip == null)
            return;

        collectAudioSource = visualRenderer.gameObject.AddComponent<AudioSource>();
        collectAudioSource.clip = AudioManager.Instance.DrillingClip;
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
