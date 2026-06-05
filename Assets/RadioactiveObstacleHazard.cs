using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RadioactiveObstacleHazard : MonoBehaviour
{
    const int DamagePerTick = 1;
    const float TickInterval = 1f;
    const float RadiusPadding = 1.15f;
    const float MinRadius = 1.65f;
    const float MaxRadius = 5.2f;
    const float MistPulseSpeed = 2.4f;
    const float MistPulseAmount = 0.12f;
    const float VisualUpdateInterval = 0.12f;
    const float ActiveStateCheckInterval = 0.35f;
    const int MaxHits = 96;

    static Sprite mistSprite;
    static readonly ContactFilter2D DamageContactFilter = new ContactFilter2D
    {
        useLayerMask = false,
        useTriggers = true
    };

    readonly Collider2D[] hits = new Collider2D[MaxHits];
    readonly HashSet<int> processedTargets = new HashSet<int>();

    ObstacleChunk chunk;
    SpriteRenderer mistRenderer;
    Transform mistTransform;
    float hazardRadius = MinRadius;
    float nextDamageTick;
    float nextVisualUpdate;
    float nextActiveStateCheck;
    float phase;
    bool toxicAreaActive;
    bool visualEffectsEnabled;

    void Awake()
    {
        chunk = GetComponent<ObstacleChunk>();
        phase = Mathf.Abs(Mathf.Sin(RuntimeHelpers.GetHashCode(this) * 0.173f)) * Mathf.PI * 2f;
        nextDamageTick = Time.time + Mathf.Repeat(phase, TickInterval);
        nextVisualUpdate = Time.time + Mathf.Repeat(phase * 0.37f, VisualUpdateInterval);
        RefreshFromObstacle();
    }

    void OnDestroy()
    {
        if (mistTransform != null)
            Destroy(mistTransform.gameObject);
    }

    public void RefreshFromObstacle()
    {
        chunk = chunk != null ? chunk : GetComponent<ObstacleChunk>();
        hazardRadius = ResolveHazardRadius();
        if (RoomSettings.AreVisualEffectsEnabled())
        {
            EnsureMistVisual();
            UpdateMistScale(0f);
        }
    }

    void Update()
    {
        RefreshActiveState(Time.time);

        if (!toxicAreaActive)
        {
            if (mistRenderer != null)
                mistRenderer.enabled = false;
            return;
        }

        UpdateMistVisual(Time.time);

        if (Time.time < nextDamageTick)
            return;

        nextDamageTick = Time.time + TickInterval;
        if (!CanApplyAuthorityDamage())
            return;

        ApplyDamageTick();
    }

    void RefreshActiveState(float time)
    {
        if (time < nextActiveStateCheck)
            return;

        nextActiveStateCheck = time + ActiveStateCheckInterval;
        toxicAreaActive = IsToxicAreaActive();
        visualEffectsEnabled = RoomSettings.AreVisualEffectsEnabled();
    }

    void UpdateMistVisual(float time)
    {
        if (!visualEffectsEnabled)
        {
            if (mistRenderer != null)
                mistRenderer.enabled = false;
            return;
        }

        if (mistRenderer == null)
            EnsureMistVisual();

        if (mistRenderer != null)
            mistRenderer.enabled = true;

        if (time < nextVisualUpdate)
            return;

        nextVisualUpdate = time + VisualUpdateInterval;
        UpdateMistScale(time);
    }

    float ResolveHazardRadius()
    {
        float obstacleRadius = chunk != null ? chunk.GetApproximateRadius() : 0f;
        return Mathf.Clamp(obstacleRadius + RadiusPadding, MinRadius, MaxRadius);
    }

    void EnsureMistVisual()
    {
        if (mistTransform == null)
        {
            Transform existing = transform.Find("RadioactiveObstacleMist");
            GameObject mistObject = existing != null ? existing.gameObject : new GameObject("RadioactiveObstacleMist", typeof(SpriteRenderer));
            mistTransform = mistObject.transform;
            mistTransform.SetParent(transform, false);
            mistTransform.localPosition = Vector3.zero;
            mistRenderer = mistObject.GetComponent<SpriteRenderer>();
        }
        else if (mistRenderer == null)
        {
            mistRenderer = mistTransform.GetComponent<SpriteRenderer>();
        }

        if (mistRenderer == null)
            return;

        mistRenderer.sprite = GetMistSprite();
        mistRenderer.color = new Color(0.38f, 1f, 0.08f, 0.34f);
        mistRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        mistRenderer.sortingOrder = GameVisualTheme.ObstacleSortingOrder - 1;
    }

    void UpdateMistScale(float time)
    {
        if (mistTransform == null || mistSprite == null)
            return;

        float pulse = 1f + Mathf.Sin(time * MistPulseSpeed + phase) * MistPulseAmount;
        float targetDiameter = hazardRadius * 2f * Mathf.Max(0.85f, pulse);
        float spriteSize = Mathf.Max(mistSprite.bounds.size.x, mistSprite.bounds.size.y);
        float scale = spriteSize > 0.001f ? targetDiameter / spriteSize : targetDiameter;
        mistTransform.localScale = new Vector3(scale, scale, 1f);

        if (mistRenderer != null)
        {
            float alphaPulse = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(time * MistPulseSpeed + phase));
            mistRenderer.color = new Color(0.34f, 1f, 0.05f, Mathf.Lerp(0.22f, 0.42f, alphaPulse));
        }
    }

    void ApplyDamageTick()
    {
        processedTargets.Clear();
        int hitCount = Physics2D.OverlapCircle(transform.position, hazardRadius, DamageContactFilter, hits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            if (TryDamageDeployable(hit))
                continue;

            if (TryDamageBeacon(hit))
                continue;

            TryDamagePlayerHealth(hit);
        }
    }

    bool TryDamageDeployable(Collider2D hit)
    {
        PlayerDeployableBase deployable = hit.GetComponentInParent<PlayerDeployableBase>();
        if (deployable == null || !deployable.CanBeTargeted)
            return false;

        PhotonView view = deployable.photonView;
        int targetKey = view != null && view.ViewID != 0 ? view.ViewID : RuntimeHelpers.GetHashCode(deployable);
        if (!processedTargets.Add(targetKey))
            return true;

        Vector3 position = deployable.transform.position;
        WeaponHitContext hitContext = new WeaponHitContext(
            WeaponDamageType.Environmental,
            WeaponDeliveryMethod.AreaPulse,
            WeaponDeliveryFlags.Continuous,
            string.Empty);
        if (view != null)
            view.RPC(
                nameof(PlayerDeployableBase.TakeDeployableDamageWithContextAt),
                RpcTarget.MasterClient,
                DamagePerTick,
                DamagePerTick,
                -1,
                position.x,
                position.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);
        else
            deployable.TakeDeployableDamageWithContextAt(
                DamagePerTick,
                DamagePerTick,
                -1,
                position.x,
                position.y,
                (int)hitContext.DamageType,
                (int)hitContext.DeliveryMethod,
                (int)hitContext.DeliveryFlags,
                hitContext.DamageSource ?? string.Empty);

        return true;
    }

    bool TryDamageBeacon(Collider2D hit)
    {
        LureBeaconDecoy beacon = hit.GetComponentInParent<LureBeaconDecoy>();
        if (beacon == null || !beacon.CanBeTargeted)
            return false;

        PhotonView view = beacon.photonView;
        int targetKey = view != null && view.ViewID != 0 ? view.ViewID : RuntimeHelpers.GetHashCode(beacon);
        if (!processedTargets.Add(targetKey))
            return true;

        Vector3 position = beacon.transform.position;
        if (view != null)
            view.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageAt), RpcTarget.MasterClient, DamagePerTick, -1, position.x, position.y);
        else
            beacon.TakeBeaconDamageAt(DamagePerTick, -1, position.x, position.y);

        return true;
    }

    bool TryDamagePlayerHealth(Collider2D hit)
    {
        PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
        if (health == null || health.IsWreck || health.IsEvacuationAnimating)
            return false;

        PhotonView view = health.photonView;
        int targetKey = view != null && view.ViewID != 0 ? view.ViewID : RuntimeHelpers.GetHashCode(health);
        if (!processedTargets.Add(targetKey))
            return true;

        if (view != null)
            view.RPC(nameof(PlayerHealth.TakeEnvironmentalDamage), RpcTarget.MasterClient, DamagePerTick);
        else
            health.TakeEnvironmentalDamage(DamagePerTick);

        return true;
    }

    bool CanApplyAuthorityDamage()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
            return false;

        if (PhotonNetwork.CurrentRoom == null)
            return !PhotonNetwork.IsConnected;

        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
               value is bool started &&
               started;
    }

    static bool IsToxicAreaActive()
    {
        return string.Equals(RoomSettings.GetSelectedLobbyMapId(), LobbyMapCatalog.ToxicAreaMapId, System.StringComparison.Ordinal);
    }

    static Sprite GetMistSprite()
    {
        if (mistSprite != null)
            return mistSprite;

        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RadioactiveObstacleMistTexture",
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float core = Mathf.Clamp01(1f - distance * 1.12f);
                float rim = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.7f) * 4.2f);
                float alpha = Mathf.Clamp01(core * core * 0.34f + rim * 0.3f);
                texture.SetPixel(x, y, new Color(0.72f, 1f, 0.08f, alpha));
            }
        }

        texture.Apply(false, true);
        mistSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        mistSprite.name = "RadioactiveObstacleMistSprite";
        return mistSprite;
    }
}
