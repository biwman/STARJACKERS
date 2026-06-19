using Photon.Pun;
using UnityEngine;

public sealed class MapInstanceToxicBorder : MonoBehaviour
{
    const int DamagePerTick = 8;
    const float DamageTickInterval = 0.65f;
    const int TextureSize = 512;
    const int LayerCount = 3;
    const float VisualPulseSpeed = 1.2f;

    static readonly float[] LayerAlpha = { 0.44f, 0.72f, 0.54f };
    static readonly float[] LayerPulse = { 0.68f, 1.08f, 1.62f };
    static readonly Color[] LayerDark =
    {
        new Color(0.03f, 0.22f, 0.1f, 1f),
        new Color(0.08f, 0.46f, 0.09f, 1f),
        new Color(0.2f, 0.72f, 0.12f, 1f)
    };
    static readonly Color[] LayerBright =
    {
        new Color(0.34f, 0.84f, 0.18f, 1f),
        new Color(0.78f, 1f, 0.16f, 1f),
        new Color(1f, 1f, 0.38f, 1f)
    };

    readonly SpriteRenderer[] renderers = new SpriteRenderer[LayerCount];
    readonly Transform[] layerTransforms = new Transform[LayerCount];

    string instanceId = MapInstanceService.MainInstanceId;
    Vector2 center;
    Vector2 innerSize;
    Vector2 outerSize;
    float nextDamageTick;
    float phase;
    bool configured;
    bool exposureOutlinesActive;

    public void Configure(string configuredInstanceId, Vector2 configuredCenter, Vector2 configuredInnerSize, Vector2 configuredOuterSize)
    {
        instanceId = string.IsNullOrWhiteSpace(configuredInstanceId) ? MapInstanceService.MainInstanceId : configuredInstanceId;
        center = configuredCenter;
        innerSize = configuredInnerSize;
        outerSize = configuredOuterSize;
        configured = innerSize.x > 0f && innerSize.y > 0f && outerSize.x > innerSize.x && outerSize.y > innerSize.y;
        phase = Mathf.Abs(Mathf.Sin(GetHashCode() * 0.173f)) * Mathf.PI * 2f;
        nextDamageTick = Time.time + Mathf.Repeat(phase, DamageTickInterval);
        EnsureVisuals();
        RefreshVisualLayout();
    }

    void Update()
    {
        if (!ShouldRun())
        {
            SetRenderersVisible(false);
            HideLocalWarningIfNeeded();
            return;
        }

        bool cameraInInstance = IsCurrentCameraInInstance();
        SetRenderersVisible(cameraInInstance);
        if (cameraInInstance)
        {
            UpdateVisualPulse();
            UpdateExposureOutlines();
        }
        else
        {
            HideLocalWarningIfNeeded();
        }

        if (Time.time < nextDamageTick)
            return;

        nextDamageTick = Time.time + DamageTickInterval;
        if (CanApplyAuthorityDamage())
            ApplyDamageTick();
    }

    bool ShouldRun()
    {
        if (!configured)
            return false;

        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return false;

        if (RoomSettings.GetSessionState() != RoomSettings.SessionStateInPlay)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) ||
            value is not bool started ||
            !started)
        {
            return false;
        }

        return !string.Equals(instanceId, MapInstanceService.HiddenDimensionInstanceId, System.StringComparison.Ordinal) ||
               MapInstanceService.IsHiddenDimensionActive();
    }

    bool IsCurrentCameraInInstance()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return true;

        return string.Equals(
            MapInstanceService.GetInstanceIdForWorldPosition(camera.transform.position),
            instanceId,
            System.StringComparison.Ordinal);
    }

    void SetRenderersVisible(bool visible)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled != visible)
                renderers[i].enabled = visible;
        }
    }

    static bool CanApplyAuthorityDamage()
    {
        return !PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient;
    }

    void EnsureVisuals()
    {
        for (int i = 0; i < LayerCount; i++)
        {
            if (renderers[i] != null)
                continue;

            GameObject layer = new GameObject("MapInstanceDeathZoneLayer_" + i, typeof(SpriteRenderer));
            layer.transform.SetParent(transform, false);
            layerTransforms[i] = layer.transform;

            SpriteRenderer renderer = layer.GetComponent<SpriteRenderer>();
            renderer.sprite = CreateBorderSprite(i);
            renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            renderer.sortingOrder = GameVisualTheme.PlayerSortingOrder + 3 + i;
            renderers[i] = renderer;
        }
    }

    void RefreshVisualLayout()
    {
        for (int i = 0; i < LayerCount; i++)
        {
            if (layerTransforms[i] == null)
                continue;

            layerTransforms[i].position = new Vector3(center.x, center.y, 0f);
            layerTransforms[i].localRotation = Quaternion.identity;
            layerTransforms[i].localScale = new Vector3(
                Mathf.Max(0.1f, outerSize.x),
                Mathf.Max(0.1f, outerSize.y),
                1f);
        }
    }

    void UpdateVisualPulse()
    {
        float borderThickness = Mathf.Max(0.5f, Mathf.Min(
            (outerSize.x - innerSize.x) * 0.5f,
            (outerSize.y - innerSize.y) * 0.5f));

        for (int i = 0; i < LayerCount; i++)
        {
            if (renderers[i] == null || layerTransforms[i] == null)
                continue;

            float pulse = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(Time.time * VisualPulseSpeed * LayerPulse[i] + phase + i * 1.31f));
            Color color = Color.Lerp(LayerDark[i], LayerBright[i], pulse);
            color.a = Mathf.Lerp(LayerAlpha[i] * 0.72f, LayerAlpha[i], pulse);
            renderers[i].color = color;

            float drift = borderThickness * (0.04f + i * 0.035f);
            float driftX = Mathf.Sin(Time.time * (0.16f + i * 0.07f) + phase + i * 2.17f) * drift;
            float driftY = Mathf.Cos(Time.time * (0.13f + i * 0.05f) + phase * 0.73f + i * 1.43f) * drift;
            float scalePulse = 1f + Mathf.Sin(Time.time * (0.22f + i * 0.09f) + phase + i) * (0.004f + i * 0.003f);
            layerTransforms[i].position = new Vector3(center.x + driftX, center.y + driftY, 0f);
            layerTransforms[i].localScale = new Vector3(
                Mathf.Max(0.1f, outerSize.x * scalePulse),
                Mathf.Max(0.1f, outerSize.y * scalePulse),
                1f);
        }
    }

    void ApplyDamageTick()
    {
        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth health = players[i];
            if (!CanDamageTarget(health))
                continue;

            Bounds bounds = ResolveTargetBounds(health);
            if (!IntersectsBorder(bounds))
                continue;

            DamageTarget(health);
        }
    }

    void UpdateExposureOutlines()
    {
        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        bool localPlayerInInstance = false;
        bool localPlayerExposed = false;
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth health = players[i];
            if (!CanDamageTarget(health))
                continue;

            bool localTarget = IsLocalPlayerTarget(health);
            if (localTarget)
                localPlayerInInstance = true;

            Bounds bounds = ResolveTargetBounds(health);
            if (!IntersectsBorder(bounds))
                continue;

            ToxicBorderExposureOutline outline = health.GetComponent<ToxicBorderExposureOutline>();
            if (outline == null)
                outline = health.gameObject.AddComponent<ToxicBorderExposureOutline>();

            outline.ShowForFrame();
            exposureOutlinesActive = true;
            if (localTarget)
                localPlayerExposed = true;
        }

        if (localPlayerExposed)
            ToxicBorderDeathZoneWarningUI.ShowForFrame();
        else if (localPlayerInInstance)
            ToxicBorderDeathZoneWarningUI.HideImmediate();
    }

    void HideLocalWarningIfNeeded()
    {
        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth health = players[i];
            if (health == null || !IsLocalPlayerTarget(health))
                continue;

            ToxicBorderDeathZoneWarningUI.HideImmediate();
            break;
        }

        if (!exposureOutlinesActive)
            return;

        ToxicBorderExposureOutline[] outlines = FindObjectsByType<ToxicBorderExposureOutline>(FindObjectsInactive.Include);
        for (int i = 0; i < outlines.Length; i++)
        {
            if (outlines[i] != null)
                outlines[i].HideNow();
        }

        exposureOutlinesActive = false;
    }

    bool CanDamageTarget(PlayerHealth health)
    {
        if (health == null ||
            !health.isActiveAndEnabled ||
            health.IsWreck ||
            health.IsEvacuationAnimating)
        {
            return false;
        }

        if (!string.Equals(MapInstanceService.GetInstanceIdForTransform(health.transform), instanceId, System.StringComparison.Ordinal))
            return false;

        return health.GetComponent<PlayerDeployableBase>() == null &&
               health.GetComponent<LureBeaconDecoy>() == null &&
               health.GetComponent<ViperWreckTowTarget>() == null;
    }

    Bounds ResolveTargetBounds(PlayerHealth health)
    {
        HideInNebulaTarget nebulaTarget = health.GetComponent<HideInNebulaTarget>();
        if (nebulaTarget != null)
            return nebulaTarget.GetNebulaBounds();

        Collider2D collider = health.GetComponentInChildren<Collider2D>();
        if (collider != null)
            return collider.bounds;

        SpriteRenderer renderer = health.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
            return renderer.bounds;

        return new Bounds(health.transform.position, Vector3.one);
    }

    bool IntersectsBorder(Bounds bounds)
    {
        float innerHalfX = innerSize.x * 0.5f;
        float innerHalfY = innerSize.y * 0.5f;
        float outerHalfX = outerSize.x * 0.5f;
        float outerHalfY = outerSize.y * 0.5f;

        bool overlapsOuter =
            bounds.max.x >= center.x - outerHalfX &&
            bounds.min.x <= center.x + outerHalfX &&
            bounds.max.y >= center.y - outerHalfY &&
            bounds.min.y <= center.y + outerHalfY;
        if (!overlapsOuter)
            return false;

        return bounds.min.x < center.x - innerHalfX ||
               bounds.max.x > center.x + innerHalfX ||
               bounds.min.y < center.y - innerHalfY ||
               bounds.max.y > center.y + innerHalfY;
    }

    static void DamageTarget(PlayerHealth health)
    {
        PhotonView view = health.photonView;
        if (view != null && view.ViewID != 0)
        {
            view.RPC(nameof(PlayerHealth.TakeEnvironmentalDamage), RpcTarget.MasterClient, DamagePerTick);
            return;
        }

        health.TakeEnvironmentalDamage(DamagePerTick);
    }

    static bool IsLocalPlayerTarget(PlayerHealth health)
    {
        if (health == null ||
            health.IsBotControlled ||
            health.IsNeutralRiderControlled ||
            health.IsEnemyAstronautControlled)
        {
            return false;
        }

        PhotonView view = health.photonView;
        if (view != null && view.ViewID != 0)
            return view.IsMine;

        return PhotonNetwork.LocalPlayer != null &&
               ReferenceEquals(PhotonNetwork.LocalPlayer.TagObject, health.gameObject);
    }

    Sprite CreateBorderSprite(int layerIndex)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "MapInstanceDeathZoneTexture_" + layerIndex,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float ratioX = outerSize.x > 0f ? Mathf.Clamp(innerSize.x / outerSize.x, 0.05f, 0.98f) : 0.84f;
        float ratioY = outerSize.y > 0f ? Mathf.Clamp(innerSize.y / outerSize.y, 0.05f, 0.98f) : 0.84f;
        float seed = 19.37f + layerIndex * 41.11f;
        float innerBleed = Mathf.Lerp(0.09f, 0.035f, layerIndex / 2f);
        float outerFeather = Mathf.Lerp(0.12f, 0.055f, layerIndex / 2f);

        for (int y = 0; y < TextureSize; y++)
        {
            float ny = ((y + 0.5f) / TextureSize) * 2f - 1f;
            for (int x = 0; x < TextureSize; x++)
            {
                float nx = ((x + 0.5f) / TextureSize) * 2f - 1f;
                texture.SetPixel(x, y, ResolveBorderPixel(nx, ny, ratioX, ratioY, innerBleed, outerFeather, seed, layerIndex));
            }
        }

        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        sprite.name = "MapInstanceDeathZoneSprite_" + layerIndex;
        return sprite;
    }

    static Color ResolveBorderPixel(float nx, float ny, float ratioX, float ratioY, float innerBleed, float outerFeather, float seed, int layerIndex)
    {
        float ax = Mathf.Abs(nx);
        float ay = Mathf.Abs(ny);
        float edgeNoise = (FractalNoise(nx * 3.2f + seed, ny * 3.2f - seed, seed) - 0.5f) * 0.055f;
        float over = Mathf.Max(ax - ratioX, ay - ratioY) + edgeNoise;
        if (over <= -innerBleed)
            return Color.clear;

        float outerDistance = 1f - Mathf.Max(ax, ay);
        float innerFade = SmoothStep01(-innerBleed, innerBleed * 0.65f, over);
        float outerFade = SmoothStep01(0f, outerFeather, outerDistance);
        float cloud = FractalNoise(nx * 6.1f + seed, ny * 5.7f - seed, seed + 13.7f);
        float alpha = innerFade * outerFade * Mathf.Lerp(0.34f, 1f, cloud) * LayerAlpha[layerIndex];
        if (alpha < 0.012f)
            return Color.clear;

        Color color = Color.Lerp(LayerDark[layerIndex], LayerBright[layerIndex], cloud);
        color.a = alpha;
        return color;
    }

    static float FractalNoise(float x, float y, float seed)
    {
        float a = Mathf.PerlinNoise(x + seed, y - seed * 0.37f);
        float b = Mathf.PerlinNoise(x * 2.17f - seed * 0.19f, y * 2.03f + seed * 0.23f);
        float c = Mathf.PerlinNoise(x * 4.11f + seed * 0.07f, y * 3.73f - seed * 0.11f);
        return Mathf.Clamp01(a * 0.58f + b * 0.29f + c * 0.13f);
    }

    static float SmoothStep01(float edge0, float edge1, float value)
    {
        if (Mathf.Abs(edge1 - edge0) <= 0.0001f)
            return value >= edge1 ? 1f : 0f;

        float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
