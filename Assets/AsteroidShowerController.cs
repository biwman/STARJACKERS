using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;

public sealed class AsteroidShowerController : MonoBehaviour, IOnEventCallback
{
    const byte AsteroidStrikeEventCode = 77;
    const float MinStrikeInterval = 7.5f;
    const float MaxStrikeInterval = 15f;
    const float ImpactRadius = 5.25f;
    const float NearPlayerStrikeChance = 0.35f;
    const float NearPlayerOffsetMultiplier = 0.65f;

    static AsteroidShowerController instance;

    float nextStrikeTime = -1f;

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject existing = GameObject.Find("AsteroidShowerController");
        if (existing != null && existing.TryGetComponent(out AsteroidShowerController controller))
        {
            instance = controller;
            return;
        }

        GameObject root = new GameObject("AsteroidShowerController");
        instance = root.AddComponent<AsteroidShowerController>();
        DontDestroyOnLoad(root);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    void Update()
    {
        if (!IsRoundActive())
        {
            nextStrikeTime = -1f;
            return;
        }

        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
            return;

        if (nextStrikeTime < 0f)
            ScheduleNextStrike();

        if (Time.time >= nextStrikeTime)
        {
            TriggerStrike();
            ScheduleNextStrike();
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent == null || photonEvent.Code != AsteroidStrikeEventCode)
            return;

        object[] payload = photonEvent.CustomData as object[];
        if (payload == null || payload.Length < 6)
            return;

        Vector2 center = new Vector2(ConvertToFloat(payload[0]), ConvertToFloat(payload[1]));
        float radius = Mathf.Max(0.25f, ConvertToFloat(payload[2]));
        int spriteIndex = ConvertToInt(payload[3]);
        int seed = ConvertToInt(payload[4]);
        double startTime = ConvertToDouble(payload[5]);
        AsteroidShowerStrikeVfx.Spawn(center, radius, spriteIndex, seed, startTime);
    }

    void ScheduleNextStrike()
    {
        nextStrikeTime = Time.time + Random.Range(MinStrikeInterval, MaxStrikeInterval);
    }

    void TriggerStrike()
    {
        Vector2 center = GetRandomStrikePoint(ImpactRadius);
        int seed = Random.Range(1, int.MaxValue);
        int spriteIndex = Mathf.Abs(seed) % Mathf.Max(1, AsteroidShowerStrikeVfx.ObstacleSpriteCount);
        double startTime = GetSynchronizedTime();

        if (!CanRaiseRoomEvent())
        {
            AsteroidShowerStrikeVfx.Spawn(center, ImpactRadius, spriteIndex, seed, startTime);
            return;
        }

        AsteroidShowerStrikeVfx.Spawn(center, ImpactRadius, spriteIndex, seed, startTime);

        object[] payload = { center.x, center.y, ImpactRadius, spriteIndex, seed, startTime };
        PhotonNetwork.RaiseEvent(
            AsteroidStrikeEventCode,
            payload,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
    }

    Vector2 GetRandomStrikePoint(float radius)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;

        if (Random.value < NearPlayerStrikeChance && TryGetRandomPlayerNearPoint(radius, halfX, halfY, out Vector2 nearPlayerPoint))
            return nearPlayerPoint;

        float margin = GetStrikeMargin(radius, halfX, halfY);
        halfX = Mathf.Max(margin, mapSize.x * 0.5f);
        halfY = Mathf.Max(margin, mapSize.y * 0.5f);
        float x = Random.Range(-halfX + margin, halfX - margin);
        float y = Random.Range(-halfY + margin, halfY - margin);
        return new Vector2(x, y);
    }

    bool TryGetRandomPlayerNearPoint(float radius, float halfX, float halfY, out Vector2 point)
    {
        point = Vector2.zero;
        PlayerHealth[] candidates = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        if (candidates == null || candidates.Length == 0)
            return false;

        List<PlayerHealth> activePlayers = new List<PlayerHealth>();
        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerHealth candidate = candidates[i];
            if (candidate == null || candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.IsBotControlled)
                continue;

            if (candidate.GetComponent<LureBeaconDecoy>() != null || candidate.GetComponent<PlayerDeployableBase>() != null)
                continue;

            activePlayers.Add(candidate);
        }

        if (activePlayers.Count == 0)
            return false;

        PlayerHealth target = activePlayers[Random.Range(0, activePlayers.Count)];
        Vector2 offset = Random.insideUnitCircle * (radius * NearPlayerOffsetMultiplier);
        Vector2 candidatePoint = (Vector2)target.transform.position + offset;
        float margin = GetStrikeMargin(radius, halfX, halfY);
        point = new Vector2(
            Mathf.Clamp(candidatePoint.x, -halfX + margin, halfX - margin),
            Mathf.Clamp(candidatePoint.y, -halfY + margin, halfY - margin));
        return true;
    }

    float GetStrikeMargin(float radius, float halfX, float halfY)
    {
        float desiredMargin = Mathf.Max(1.5f, radius + 0.7f);
        float maxFittingMargin = Mathf.Max(1.5f, Mathf.Min(halfX, halfY) - 0.25f);
        return Mathf.Min(desiredMargin, maxFittingMargin);
    }

    static bool CanRaiseRoomEvent()
    {
        return PhotonNetwork.IsConnected && PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null;
    }

    static bool IsRoundActive()
    {
        if (!RoomSettings.IsAsteroidShowerActive())
            return false;

        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started)
        {
            return started;
        }

        return false;
    }

    static double GetSynchronizedTime()
    {
        return PhotonNetwork.IsConnected ? PhotonNetwork.Time : Time.time;
    }

    static float ConvertToFloat(object value)
    {
        if (value is float floatValue)
            return floatValue;

        if (value is double doubleValue)
            return (float)doubleValue;

        if (value is int intValue)
            return intValue;

        return 0f;
    }

    static int ConvertToInt(object value)
    {
        if (value is int intValue)
            return intValue;

        if (value is short shortValue)
            return shortValue;

        if (value is byte byteValue)
            return byteValue;

        if (value is float floatValue)
            return Mathf.RoundToInt(floatValue);

        if (value is double doubleValue)
            return Mathf.RoundToInt((float)doubleValue);

        return 0;
    }

    static double ConvertToDouble(object value)
    {
        if (value is double doubleValue)
            return doubleValue;

        if (value is float floatValue)
            return floatValue;

        if (value is int intValue)
            return intValue;

        return 0d;
    }
}

public sealed class AsteroidShowerStrikeVfx : MonoBehaviour
{
    const float WarningDuration = 3f;
    const float FlightDuration = 6f;
    const float ImpactAfterFlightStart = 3f;
    const float TotalDuration = WarningDuration + FlightDuration;
    const float ImpactTime = WarningDuration + ImpactAfterFlightStart;
    const int Damage = 100;
    const float MovingObjectImpulse = 8.5f;
    const float ShipKnockbackImpulse = 6.8f;
    const float AsteroidZ = -0.29f;
    const float MarkerZ = -0.3f;
    const int MarkerSegments = 64;
    const int MarkerSortingOrder = 7100;
    const int AsteroidSortingOrder = 7090;

    static readonly string[] ObstacleSpriteResourcePaths =
    {
        "asteroida_1_clean_resource",
        "asteroida_2_clean_resource",
        "asteroida_3_clean_resource",
        "asteroida_podluzna_1_clean_resource",
        "asteroida_podluzna_2_clean_resource",
        "asteroida_nadkruszona_resource",
        "asteroida_ziemniak_resource",
        "obstacle_ice_2",
        "obstacle_ice_3",
        "obstacle_ice_4",
        "obstacle_ice_5",
        "obstacle_ice_6",
        "obstacle_ice_7",
        "obstacle_ice_8",
        "Visuals/Obstacles/asteroid_obstacle_resource"
    };

    static Material lineMaterial;

    Vector2 center;
    float radius;
    int spriteIndex;
    int seed;
    double startTime;
    float asteroidBaseWorldSize = 1f;
    float initialAsteroidWorldSize;
    float rotationSpeed;
    bool impactApplied;

    SpriteRenderer asteroidRenderer;
    LineRenderer markerGlow;
    LineRenderer markerCore;
    LineRenderer markerInner;

    public static int ObstacleSpriteCount => ObstacleSpriteResourcePaths.Length;

    public static void Spawn(Vector2 position, float configuredRadius, int configuredSpriteIndex, int configuredSeed, double configuredStartTime)
    {
        GameObject effect = new GameObject("AsteroidShowerStrikeVfx");
        AsteroidShowerStrikeVfx vfx = effect.AddComponent<AsteroidShowerStrikeVfx>();
        vfx.Initialize(position, configuredRadius, configuredSpriteIndex, configuredSeed, configuredStartTime);
    }

    void Initialize(Vector2 position, float configuredRadius, int configuredSpriteIndex, int configuredSeed, double configuredStartTime)
    {
        center = position;
        radius = Mathf.Max(0.25f, configuredRadius);
        spriteIndex = configuredSpriteIndex;
        seed = configuredSeed;
        startTime = configuredStartTime;
        transform.position = new Vector3(center.x, center.y, MarkerZ);

        CreateMarker();
        CreateAsteroid();
        UpdateVisuals(GetElapsed());
    }

    void Update()
    {
        float elapsed = GetElapsed();
        UpdateVisuals(elapsed);

        if (!impactApplied && elapsed >= ImpactTime)
        {
            impactApplied = true;
            PlayImpact();
            if (CanApplyAuthorityImpact())
                ApplyImpact();
        }

        if (elapsed >= TotalDuration)
            Destroy(gameObject);
    }

    void CreateMarker()
    {
        markerGlow = CreateLine("AsteroidMarkerGlow", MarkerSortingOrder, 0.2f, true);
        markerCore = CreateLine("AsteroidMarkerCore", MarkerSortingOrder + 1, 0.07f, true);
        markerInner = CreateLine("AsteroidMarkerInner", MarkerSortingOrder + 2, 0.045f, true);
    }

    void CreateAsteroid()
    {
        GameObject asteroidObject = new GameObject("AsteroidShowerVisual");
        asteroidObject.transform.SetParent(transform, false);
        asteroidObject.transform.localPosition = new Vector3(0f, 0f, AsteroidZ - MarkerZ);

        asteroidRenderer = asteroidObject.AddComponent<SpriteRenderer>();
        asteroidRenderer.sprite = LoadAsteroidSprite(spriteIndex);
        asteroidRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        asteroidRenderer.sortingOrder = AsteroidSortingOrder;
        asteroidRenderer.color = new Color(1f, 1f, 1f, 0f);

        if (asteroidRenderer.sprite != null)
            asteroidBaseWorldSize = Mathf.Max(0.01f, Mathf.Max(asteroidRenderer.sprite.bounds.size.x, asteroidRenderer.sprite.bounds.size.y));

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        initialAsteroidWorldSize = Mathf.Max(mapSize.x, mapSize.y) * 1.45f;
        rotationSpeed = (Hash01(seed, 31) < 0.5f ? -1f : 1f) * Mathf.Lerp(24f, 58f, Hash01(seed, 73));
        asteroidObject.transform.localRotation = Quaternion.Euler(0f, 0f, Hash01(seed, 11) * 360f);
    }

    void UpdateVisuals(float elapsed)
    {
        float markerProgress = Mathf.Clamp01(elapsed / TotalDuration);
        float pulse = Mathf.Sin(Time.time * 8.8f) * 0.5f + 0.5f;
        float fadeOut = elapsed > ImpactTime ? Mathf.Clamp01((TotalDuration - elapsed) / (TotalDuration - ImpactTime)) : 1f;
        Color warning = Color.Lerp(new Color(1f, 0.72f, 0.22f, 0.96f), new Color(1f, 0.14f, 0.08f, 1f), Mathf.Clamp01(elapsed / ImpactTime));
        Color glow = new Color(warning.r, warning.g * 0.86f, warning.b * 0.76f, Mathf.Lerp(0.22f, 0.5f, pulse) * fadeOut);
        Color core = new Color(1f, 0.96f, 0.82f, Mathf.Lerp(0.75f, 1f, pulse) * fadeOut);
        Color inner = new Color(warning.r, warning.g, warning.b, 0.86f * fadeOut);

        UpdateRing(markerGlow, radius * Mathf.Lerp(0.9f, 1.13f, pulse), glow, 0.04f);
        UpdateRing(markerCore, radius, core, 0.025f);
        UpdateRing(markerInner, radius * Mathf.Lerp(0.43f, 0.58f, markerProgress), inner, 0.035f);

        UpdateAsteroid(elapsed);
    }

    void UpdateAsteroid(float elapsed)
    {
        if (asteroidRenderer == null)
            return;

        if (elapsed < WarningDuration)
        {
            asteroidRenderer.color = new Color(1f, 1f, 1f, 0f);
            return;
        }

        float flightElapsed = Mathf.Clamp(elapsed - WarningDuration, 0f, FlightDuration);
        float targetWorldSize;
        if (flightElapsed <= ImpactAfterFlightStart)
        {
            float progress = Mathf.SmoothStep(0f, 1f, flightElapsed / ImpactAfterFlightStart);
            targetWorldSize = Mathf.Lerp(initialAsteroidWorldSize, radius * 2f, progress);
        }
        else
        {
            float progress = Mathf.SmoothStep(0f, 1f, (flightElapsed - ImpactAfterFlightStart) / (FlightDuration - ImpactAfterFlightStart));
            targetWorldSize = Mathf.Lerp(radius * 2f, 0.04f, progress);
        }

        float scale = targetWorldSize / asteroidBaseWorldSize;
        asteroidRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        asteroidRenderer.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime, Space.Self);

        float alpha = flightElapsed < 0.25f
            ? Mathf.Clamp01(flightElapsed / 0.25f)
            : Mathf.Clamp01((FlightDuration - flightElapsed) / 1.2f);
        asteroidRenderer.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.72f, 1f, alpha) * alpha);
    }

    void PlayImpact()
    {
        Vector3 world = new Vector3(center.x, center.y, 0f);
        AudioManager.Instance.PlayExplosionAt(world);
        SpaceMineExplosionVfx.Spawn(world, radius, asteroidRenderer);
    }

    void ApplyImpact()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        HashSet<int> processedHealthViews = new HashSet<int>();
        HashSet<string> processedMovingObjects = new HashSet<string>();
        HashSet<Rigidbody2D> processedBodies = new HashSet<Rigidbody2D>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || !hit.enabled)
                continue;

            ApplyHealthImpact(hit, processedHealthViews);
            ApplyMovingObjectPush(hit, processedMovingObjects);
            ApplyLooseBodyPush(hit, processedBodies);
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted || beacon.photonView == null)
                continue;

            if (Vector2.Distance(center, beacon.transform.position) > radius)
                continue;

            if (processedHealthViews.Add(beacon.photonView.ViewID))
                beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageProfileAt), RpcTarget.MasterClient, Damage, Damage, -1, center.x, center.y);
        }

        foreach (PlayerDeployableBase deployable in PlayerDeployableBase.GetActiveDeployables())
        {
            if (deployable == null || !deployable.CanBeTargeted || deployable.photonView == null)
                continue;

            if (Vector2.Distance(center, deployable.transform.position) > radius)
                continue;

            if (processedHealthViews.Add(deployable.photonView.ViewID))
                deployable.photonView.RPC(nameof(PlayerDeployableBase.TakeDeployableDamageAt), RpcTarget.MasterClient, Damage, Damage, -1, center.x, center.y);
        }
    }

    void ApplyHealthImpact(Collider2D hit, HashSet<int> processedHealthViews)
    {
        PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
        PhotonView targetView = health != null ? health.GetComponent<PhotonView>() : null;
        if (health == null || targetView == null || health.IsWreck || health.IsEvacuationAnimating)
            return;

        if (health.GetComponent<LureBeaconDecoy>() != null)
            return;

        if (!processedHealthViews.Add(targetView.ViewID))
            return;

        Vector2 position = health.transform.position;
        Vector2 direction = ResolveDirection(position);
        float falloff = GetFalloff(position);
        int damage = Mathf.Max(1, Mathf.RoundToInt(Damage * falloff));
        targetView.RPC(nameof(PlayerHealth.TakeDamageProfileAt), RpcTarget.MasterClient, damage, damage, -1, center.x, center.y);
        targetView.RPC(nameof(PlayerHealth.ApplyAsteroidKnockbackRpc), RpcTarget.All, direction.x, direction.y, ShipKnockbackImpulse * falloff);
    }

    void ApplyMovingObjectPush(Collider2D hit, HashSet<string> processedMovingObjects)
    {
        MovingSpaceObject moving = hit.GetComponentInParent<MovingSpaceObject>();
        if (moving == null || string.IsNullOrWhiteSpace(moving.StableId))
            return;

        if (!processedMovingObjects.Add(moving.StableId))
            return;

        Vector2 direction = ResolveDirection(moving.transform.position);
        float falloff = GetFalloff(moving.transform.position);
        moving.ApplyImpulse(direction * MovingObjectImpulse * falloff);
        moving.ForceBroadcastSnapshot();
    }

    void ApplyLooseBodyPush(Collider2D hit, HashSet<Rigidbody2D> processedBodies)
    {
        if (hit.GetComponentInParent<PlayerHealth>() != null || hit.GetComponentInParent<MovingSpaceObject>() != null)
            return;

        Rigidbody2D body = hit.attachedRigidbody != null ? hit.attachedRigidbody : hit.GetComponentInParent<Rigidbody2D>();
        if (body == null || !body.simulated || !processedBodies.Add(body))
            return;

        Vector2 direction = ResolveDirection(body.position);
        float falloff = GetFalloff(body.position);
        body.AddForce(direction * MovingObjectImpulse * falloff, ForceMode2D.Impulse);
    }

    bool CanApplyAuthorityImpact()
    {
        if (!PhotonNetwork.IsConnected)
            return true;

        return PhotonNetwork.IsMasterClient;
    }

    Vector2 ResolveDirection(Vector2 targetPosition)
    {
        Vector2 direction = targetPosition - center;
        if (direction.sqrMagnitude > 0.0001f)
            return direction.normalized;

        float angle = Hash01(seed, 127) * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    float GetFalloff(Vector2 targetPosition)
    {
        float distance = Vector2.Distance(center, targetPosition);
        return Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(distance / Mathf.Max(0.01f, radius)));
    }

    float GetElapsed()
    {
        double now = PhotonNetwork.IsConnected ? PhotonNetwork.Time : Time.time;
        return Mathf.Max(0f, (float)(now - startTime));
    }

    LineRenderer CreateLine(string lineName, int sortingOrder, float width, bool loop)
    {
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader)
            {
                name = "AsteroidShowerLineMaterial",
                color = Color.white
            };
            lineMaterial.renderQueue = 5000;
        }

        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.material = lineMaterial;
        line.useWorldSpace = true;
        line.loop = loop;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 10;
        line.numCornerVertices = 8;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = sortingOrder;
        line.startWidth = width;
        line.endWidth = width;
        line.positionCount = MarkerSegments;
        return line;
    }

    void UpdateRing(LineRenderer line, float ringRadius, Color color, float wobble)
    {
        if (line == null)
            return;

        line.enabled = color.a > 0.001f;
        line.startColor = color;
        line.endColor = color;
        Vector3 ringCenter = new Vector3(center.x, center.y, MarkerZ);

        for (int i = 0; i < MarkerSegments; i++)
        {
            float t = (i / (float)MarkerSegments) * Mathf.PI * 2f;
            float localRadius = ringRadius * (1f + Mathf.Sin(t * 5f + Time.time * 7.4f + seed * 0.001f) * wobble);
            line.SetPosition(i, ringCenter + new Vector3(Mathf.Cos(t) * localRadius, Mathf.Sin(t) * localRadius, 0f));
        }
    }

    Sprite LoadAsteroidSprite(int index)
    {
        if (ObstacleSpriteResourcePaths.Length == 0)
            return null;

        int startIndex = Mathf.Abs(index) % ObstacleSpriteResourcePaths.Length;
        for (int offset = 0; offset < ObstacleSpriteResourcePaths.Length; offset++)
        {
            int resolvedIndex = (startIndex + offset) % ObstacleSpriteResourcePaths.Length;
            Sprite sprite = Resources.Load<Sprite>(ObstacleSpriteResourcePaths[resolvedIndex]);
            if (sprite != null)
                return sprite;

            Sprite[] sprites = Resources.LoadAll<Sprite>(ObstacleSpriteResourcePaths[resolvedIndex]);
            sprite = GetLargestSprite(sprites);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            float area = sprite.rect.width * sprite.rect.height;
            if (area > bestArea)
            {
                best = sprite;
                bestArea = area;
            }
        }

        return best;
    }

    static float Hash01(int baseSeed, int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash ^= (uint)baseSeed;
            hash *= 16777619u;
            hash ^= (uint)salt;
            hash *= 16777619u;
            hash ^= hash >> 13;
            hash *= 1274126177u;
            return (hash & 0x00FFFFFF) / 16777215f;
        }
    }
}
