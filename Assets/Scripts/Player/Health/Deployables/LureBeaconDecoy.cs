using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PhotonView))]
public sealed class LureBeaconDecoy : MonoBehaviourPun
{
    public const string InstantiationMarker = "lure_beacon_decoy";
    public const int DefaultHp = 100;
    public const int DefaultShield = 100;

    const float VisualTargetSize = 0.9f;
    const float IdleDriftSpeedMin = 0.18f;
    const float IdleDriftSpeedMax = 0.34f;
    const float AngularVelocityMin = -12f;
    const float AngularVelocityMax = 12f;
    const float CollisionRadius = 0.38f;
    const float PulseSpeed = 3.2f;
    const float PulseScaleAmount = 0.08f;
    static readonly Color PulseBaseColor = new Color(0.92f, 0.94f, 1f, 1f);
    static readonly Color PulseGlowColor = new Color(0.62f, 0.96f, 1f, 1f);

    static readonly HashSet<LureBeaconDecoy> ActiveBeacons = new HashSet<LureBeaconDecoy>();
    static Sprite cachedSprite;

    bool initialized;
    bool isDestroyed;
    int currentHp;
    int currentShield;
    int ownerShipViewId;
    Rigidbody2D body;
    SpriteRenderer cachedRenderer;
    AudioSource loopAudioSource;
    Vector3 visualBaseScale = Vector3.one;

    public bool CanBeTargeted => initialized && !isDestroyed && currentHp > 0;
    public int OwnerShipViewId => ownerShipViewId;

    public static bool IsInstantiationData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               marker == InstantiationMarker;
    }

    public static LureBeaconDecoy EnsureAttached(GameObject target)
    {
        if (target == null)
            return null;

        LureBeaconDecoy beacon = target.GetComponent<LureBeaconDecoy>();
        if (beacon == null)
            beacon = target.AddComponent<LureBeaconDecoy>();

        beacon.InitializeFromPhotonData();
        return beacon;
    }

    public static IReadOnlyCollection<LureBeaconDecoy> GetActiveBeacons()
    {
        return ActiveBeacons;
    }

    void Awake()
    {
        if (IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
    }

    void Start()
    {
        if (IsInstantiationData(photonView != null ? photonView.InstantiationData : null))
            InitializeFromPhotonData();
        else
            enabled = false;
    }

    void OnEnable()
    {
        if (initialized)
            ActiveBeacons.Add(this);
    }

    void OnDisable()
    {
        StopLoopAudio();
        ActiveBeacons.Remove(this);
    }

    void OnDestroy()
    {
        StopLoopAudio();
        ActiveBeacons.Remove(this);
    }

    void Update()
    {
        if (!initialized || cachedRenderer == null || isDestroyed)
            return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed + (photonView != null ? photonView.ViewID * 0.17f : 0f));
        float scale = 1f + pulse * PulseScaleAmount;
        cachedRenderer.transform.localScale = visualBaseScale * scale;
        cachedRenderer.color = Color.Lerp(PulseBaseColor, PulseGlowColor, pulse * 0.85f);
    }

    public void InitializeFromPhotonData()
    {
        if (initialized)
            return;

        initialized = true;
        currentHp = DefaultHp;
        currentShield = DefaultShield;
        ActiveBeacons.Add(this);

        object[] data = photonView != null ? photonView.InstantiationData : null;
        ownerShipViewId = data != null && data.Length > 1 ? TryConvertToInt(data[1]) : 0;
        Vector2 initialDirection = data != null && data.Length > 3
            ? new Vector2(TryConvertToFloat(data[2]), TryConvertToFloat(data[3]))
            : Random.insideUnitCircle.normalized;

        if (initialDirection.sqrMagnitude < 0.001f)
            initialDirection = Vector2.right;

        cachedRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        ConfigureVisuals();
        ConfigurePhysics(initialDirection.normalized);
        DisablePlayerSpecificSystems();
        AudioManager.Instance.PlayBeaconSignalAt(transform.position);
        StartLoopAudio();
    }

    [PunRPC]
    public void TakeBeaconDamageAt(int damage, int attackerViewId, float impactX, float impactY)
    {
        ApplyDamage(Mathf.Max(0, damage), attackerViewId, new Vector2(impactX, impactY));
    }

    [PunRPC]
    public void TakeBeaconDamageProfileAt(int shieldDamage, int hpDamage, int attackerViewId, float impactX, float impactY)
    {
        ApplyProfileDamage(Mathf.Max(0, shieldDamage), Mathf.Max(0, hpDamage), attackerViewId, new Vector2(impactX, impactY));
    }

    [PunRPC]
    public void TakeBeaconShieldOnlyDamageAt(int shieldDamage, int attackerViewId, float impactX, float impactY)
    {
        ApplyShieldOnlyDamage(Mathf.Max(0, shieldDamage), attackerViewId, new Vector2(impactX, impactY));
    }

    void ApplyDamage(int damage, int attackerViewId, Vector2 impactPoint)
    {
        if (!PhotonNetwork.IsMasterClient || !CanBeTargeted || damage <= 0)
            return;

        int remainingDamage = damage;
        int absorbed = 0;
        if (currentShield > 0)
        {
            absorbed = Mathf.Min(currentShield, remainingDamage);
            currentShield -= absorbed;
            remainingDamage -= absorbed;
        }

        if (remainingDamage > 0)
            currentHp = Mathf.Max(0, currentHp - remainingDamage);

        if (absorbed > 0)
            photonView.RPC(nameof(PlayShieldHitFeedback), RpcTarget.All, impactPoint.x, impactPoint.y);
        else
            photonView.RPC(nameof(PlayHpHitFeedback), RpcTarget.All, impactPoint.x, impactPoint.y);

        if (currentHp <= 0)
            DestroyOnMaster();
    }

    void ApplyProfileDamage(int shieldDamage, int hpDamage, int attackerViewId, Vector2 impactPoint)
    {
        if (!PhotonNetwork.IsMasterClient || !CanBeTargeted)
            return;

        int remainingDamage = 0;
        int absorbed = 0;
        if (currentShield > 0)
        {
            if (shieldDamage > 0)
            {
                absorbed = Mathf.Min(currentShield, shieldDamage);
                currentShield -= absorbed;
                float overflowRatio = Mathf.Clamp01((shieldDamage - absorbed) / (float)shieldDamage);
                remainingDamage = Mathf.RoundToInt(hpDamage * overflowRatio);
            }
        }
        else
        {
            remainingDamage = hpDamage;
        }

        if (remainingDamage > 0)
            currentHp = Mathf.Max(0, currentHp - remainingDamage);

        if (absorbed <= 0 && remainingDamage <= 0)
            return;

        if (absorbed > 0)
            photonView.RPC(nameof(PlayShieldHitFeedback), RpcTarget.All, impactPoint.x, impactPoint.y);
        else
            photonView.RPC(nameof(PlayHpHitFeedback), RpcTarget.All, impactPoint.x, impactPoint.y);

        if (currentHp <= 0)
            DestroyOnMaster();
    }

    void ApplyShieldOnlyDamage(int damage, int attackerViewId, Vector2 impactPoint)
    {
        if (!PhotonNetwork.IsMasterClient || !CanBeTargeted || damage <= 0 || currentShield <= 0)
            return;

        int absorbed = Mathf.Min(currentShield, damage);
        currentShield -= absorbed;
        photonView.RPC(nameof(PlayShieldHitFeedback), RpcTarget.All, impactPoint.x, impactPoint.y);
    }

    [PunRPC]
    void PlayShieldHitFeedback(float x, float y)
    {
        AudioManager.Instance.PlayShieldHitAt(new Vector3(x, y, transform.position.z));
    }

    [PunRPC]
    void PlayHpHitFeedback(float x, float y)
    {
        AudioManager.Instance.PlayHpHitAt(new Vector3(x, y, transform.position.z));
    }

    [PunRPC]
    void PlayDestroyedFeedback()
    {
        AudioManager.Instance.PlayExplosionAt(transform.position);
    }

    void DestroyOnMaster()
    {
        if (isDestroyed)
            return;

        isDestroyed = true;
        StopLoopAudio();
        photonView.RPC(nameof(PlayDestroyedFeedback), RpcTarget.All);
        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    void ConfigureVisuals()
    {
        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        if (cachedRenderer == null)
            return;

        DisableExtraSpriteRenderers();

        Sprite sprite = LoadBeaconSprite();
        if (sprite != null)
        {
            cachedRenderer.sprite = sprite;
            cachedRenderer.color = Color.white;
            FitRendererToTargetSize(cachedRenderer, VisualTargetSize);
            visualBaseScale = cachedRenderer.transform.localScale;
        }

        cachedRenderer.sortingOrder = Mathf.Max(cachedRenderer.sortingOrder, 38);
    }

    void StartLoopAudio()
    {
        AudioClip clip = AudioManager.Instance != null ? AudioManager.Instance.BeaconSignalClip : null;
        if (clip == null)
            return;

        if (loopAudioSource == null)
        {
            Transform existing = transform.Find("LureBeaconLoopAudio");
            GameObject sourceObject = existing != null ? existing.gameObject : new GameObject("LureBeaconLoopAudio");
            sourceObject.transform.SetParent(transform, false);
            loopAudioSource = sourceObject.GetComponent<AudioSource>();
            if (loopAudioSource == null)
                loopAudioSource = sourceObject.AddComponent<AudioSource>();
        }

        if (loopAudioSource == null)
            return;

        AudioManager.Instance.ConfigureSpatialSource(loopAudioSource, 0.52f);
        loopAudioSource.clip = clip;
        loopAudioSource.loop = true;

        if (!loopAudioSource.isPlaying)
            loopAudioSource.Play();
    }

    void StopLoopAudio()
    {
        if (loopAudioSource != null && loopAudioSource.isPlaying)
            loopAudioSource.Stop();
    }

    void ConfigurePhysics(Vector2 initialDirection)
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (body != null)
        {
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = 1.35f;
            body.linearDamping = 0.42f;
            body.angularDamping = 0.78f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = initialDirection * Random.Range(IdleDriftSpeedMin, IdleDriftSpeedMax);
            body.angularVelocity = Random.Range(AngularVelocityMin, AngularVelocityMax);
        }

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null)
            circle = gameObject.AddComponent<CircleCollider2D>();

        circle.isTrigger = false;
        SetWorldRadius(circle, CollisionRadius);
    }

    void DisablePlayerSpecificSystems()
    {
        DisableComponent<PlayerMovement>();
        DisableComponent<PlayerShooting>();
        DisableComponent<TreasureCollector>();
        DisableComponent<PlayerRepairDocking>();
        DisableComponent<HealthBarUI>();
        DisableComponent<ShieldBarUI>();
        DisableComponent<RoundVitalsIconHudUI>();
        DisableComponent<PlayerNicknameUI>();
        DisableComponent<BoosterBarUI>();
        DisableComponent<ShipInventoryHudUI>();
        DisableComponent<StartingShipEntryVfx>();
        DisableComponent<SpawnInvulnerabilityVfx>();
        DisableComponent<AstronautSurvivor>();
        DisableComponent<PlayerHealth>();

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            Destroy(thruster);
    }

    void DisableExtraSpriteRenderers()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer == cachedRenderer)
                continue;

            renderer.enabled = false;
        }
    }

    void DisableComponent<T>() where T : Behaviour
    {
        T component = GetComponent<T>();
        if (component != null && component != this)
            component.enabled = false;
    }

    static Sprite LoadBeaconSprite()
    {
        if (cachedSprite != null)
            return cachedSprite;

        cachedSprite = Resources.Load<Sprite>("lure_beacon_onmap_resource");
        if (cachedSprite != null)
            return cachedSprite;

#if UNITY_EDITOR
        cachedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/lure_beacon_onmap.png");
#endif
        return cachedSprite;
    }

    static void FitRendererToTargetSize(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds bounds = renderer.sprite.bounds;
        float maxExtent = Mathf.Max(bounds.size.x, bounds.size.y);
        if (maxExtent <= 0.0001f)
            return;

        float scale = targetSize / maxExtent;
        renderer.transform.localScale = Vector3.one * scale;
    }

    static void SetWorldRadius(CircleCollider2D circle, float worldRadius)
    {
        if (circle == null)
            return;

        Vector3 scale = circle.transform.lossyScale;
        float safeScaleX = Mathf.Abs(scale.x) > 0.0001f ? Mathf.Abs(scale.x) : 1f;
        float safeScaleY = Mathf.Abs(scale.y) > 0.0001f ? Mathf.Abs(scale.y) : 1f;
        circle.radius = worldRadius / Mathf.Max(safeScaleX, safeScaleY);
    }

    static int TryConvertToInt(object value)
    {
        if (value is int intValue)
            return intValue;
        if (value is byte byteValue)
            return byteValue;
        if (value is short shortValue)
            return shortValue;
        if (value is long longValue)
            return (int)longValue;
        if (value is float floatValue)
            return Mathf.RoundToInt(floatValue);
        if (value is double doubleValue)
            return Mathf.RoundToInt((float)doubleValue);

        int.TryParse(value != null ? value.ToString() : string.Empty, out int parsed);
        return parsed;
    }

    static float TryConvertToFloat(object value)
    {
        if (value is float floatValue)
            return floatValue;
        if (value is double doubleValue)
            return (float)doubleValue;
        if (value is int intValue)
            return intValue;
        if (value is long longValue)
            return longValue;

        float.TryParse(value != null ? value.ToString() : string.Empty, out float parsed);
        return parsed;
    }
}
