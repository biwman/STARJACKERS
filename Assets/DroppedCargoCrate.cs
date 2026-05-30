using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class DroppedCargoCrate : MonoBehaviourPun, IOnEventCallback
{
    const float BaseDriftSpeed = 0.65f;
    const float MaxDriftSpeed = 1.1f;
    const float DriftBouncePadding = 0.6f;
    const float TargetVisualSize = 0.42f;
    const string CrateSpriteFileName = "skrzynia_metal_clean.png";
    const byte SnapshotEventCode = 83;
    const byte ImpulseRequestEventCode = 84;
    const float SnapshotInterval = 0.05f;
    const float RemoteSmoothing = 15f;
    const float MaxAngularSpeed = 65f;
    const float ImpulseRequestCooldown = 0.035f;
    const float PushBoostDuration = 0.55f;
    const float PushBoostMaxSpeed = 5.4f;

    static Sprite cachedCrateSprite;

    SpriteRenderer spriteRenderer;
    BoxCollider2D bodyCollider;
    Rigidbody2D rb;
    Color defaultColor = Color.white;
    Vector2 driftVelocity;
    string storedItemId;
    bool initialized;
    bool isAuthority;
    float baseAngularSpeed;
    float nextSnapshotTime;
    float nextImpulseRequestTime;
    Vector2 networkPosition;
    Vector2 networkVelocity;
    float networkRotation;
    float networkAngularVelocity;
    bool hasNetworkState;
    Vector2 predictedLocalOffset;
    float pushBoostUntil;
    float pushBoostMaxSpeed;

    public bool isBeingCollected;
    public bool HasLoot => !string.IsNullOrWhiteSpace(storedItemId);
    public string StoredItemId => storedItemId;

    public void InitializeFromPhotonData()
    {
        if (initialized)
            return;

        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<BoxCollider2D>();
        rb = GetComponent<Rigidbody2D>();

        ApplyVisualSetup();
        ApplyDataFromPhoton();
        ApplySimulationMode();
        initialized = true;
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<BoxCollider2D>();
        rb = GetComponent<Rigidbody2D>();
        EnsureRigidBody();
    }

    void Start()
    {
        if (!initialized)
        {
            InitializeFromPhotonData();
        }
    }

    void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    void FixedUpdate()
    {
        if (!initialized)
            return;
        if (PhotonNetwork.CurrentRoom == null)
            return;

        ApplySimulationMode();

        if (isAuthority)
        {
            SimulateAuthorityMotion();
            BroadcastSnapshotIfNeeded();
        }
        else
        {
            FollowAuthoritySnapshot();
        }
    }

    void ApplyVisualSetup()
    {
        EnsureRigidBody();

        if (spriteRenderer != null)
        {
            Sprite crateSprite = LoadCrateSprite();
            if (crateSprite != null)
            {
                spriteRenderer.sprite = crateSprite;
                FitSpriteToTargetSize(spriteRenderer, TargetVisualSize);
            }

            spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            spriteRenderer.sortingOrder = GameVisualTheme.TreasureSortingOrder;
            spriteRenderer.color = defaultColor;
        }

        if (bodyCollider != null)
        {
            bodyCollider.isTrigger = false;
            bodyCollider.size = Vector2.one * 0.9f;
            bodyCollider.sharedMaterial = MovingSpaceObject.GetSharedBouncyMaterial();
        }

    }

    void ApplyDataFromPhoton()
    {
        object[] data = photonView != null ? photonView.InstantiationData : null;
        if (data == null || data.Length < 3)
            return;

        storedItemId = data[0] as string;
        driftVelocity = new Vector2(ConvertToFloat(data[1]), ConvertToFloat(data[2]));

        float speed = driftVelocity.magnitude;
        if (speed > MaxDriftSpeed)
            driftVelocity = driftVelocity.normalized * MaxDriftSpeed;
        else if (speed < 0.01f)
            driftVelocity = Vector2.down * BaseDriftSpeed;

        float seed = Mathf.Abs((storedItemId ?? "crate").GetHashCode() * 0.00019f) + photonView.ViewID * 0.013f;
        baseAngularSpeed = Mathf.Lerp(10f, 26f, Mathf.PerlinNoise(seed, seed + 3.7f));
        if (Mathf.PerlinNoise(seed + 5.4f, seed + 1.2f) < 0.5f)
            baseAngularSpeed *= -1f;

        if (rb != null)
        {
            rb.linearVelocity = driftVelocity;
            rb.angularVelocity = baseAngularSpeed;
        }
    }

    void EnsureRigidBody()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.simulated = true;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.None;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        rb.useFullKinematicContacts = true;
    }

    void ApplySimulationMode()
    {
        isAuthority = !PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient;

        if (rb == null)
            EnsureRigidBody();

        if (isAuthority)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.mass = 0.55f;
            rb.linearDamping = 0.04f;
            rb.angularDamping = 0.1f;
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    void SimulateAuthorityMotion()
    {
        if (rb == null)
            return;

        if (rb.linearVelocity.sqrMagnitude < 0.04f)
        {
            Vector2 fallbackDirection = driftVelocity.sqrMagnitude > 0.001f ? driftVelocity.normalized : Vector2.down;
            rb.linearVelocity = fallbackDirection * BaseDriftSpeed;
        }
        float maxSpeed = Mathf.Max(MaxDriftSpeed, GetPushBoostMaxSpeed());
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

        driftVelocity = rb.linearVelocity;

        if (Mathf.Abs(rb.angularVelocity) < Mathf.Abs(baseAngularSpeed) * 0.35f)
        {
            rb.angularVelocity = Mathf.MoveTowards(rb.angularVelocity, baseAngularSpeed, 18f * Time.fixedDeltaTime);
        }

        rb.angularVelocity = Mathf.Clamp(rb.angularVelocity, -MaxAngularSpeed, MaxAngularSpeed);
        BounceAgainstMapBounds();
    }

    void BroadcastSnapshotIfNeeded()
    {
        if (!PhotonNetwork.IsConnected || Time.time < nextSnapshotTime || rb == null)
            return;

        nextSnapshotTime = Time.time + SnapshotInterval;
        object[] payload = { photonView.ViewID, rb.position.x, rb.position.y, rb.linearVelocity.x, rb.linearVelocity.y, rb.rotation, rb.angularVelocity };
        PhotonNetwork.RaiseEvent(SnapshotEventCode, payload, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendUnreliable);
    }

    void FollowAuthoritySnapshot()
    {
        if (!hasNetworkState || rb == null)
            return;

        predictedLocalOffset = Vector2.Lerp(predictedLocalOffset, Vector2.zero, 1f - Mathf.Exp(-8f * Time.fixedDeltaTime));
        Vector2 predictedPosition = networkPosition + networkVelocity * SnapshotInterval + predictedLocalOffset;
        float smoothing = 1f - Mathf.Exp(-RemoteSmoothing * Time.fixedDeltaTime);

        if (Vector2.Distance(rb.position, predictedPosition) > 1.2f)
        {
            rb.position = predictedPosition;
            rb.rotation = networkRotation;
            predictedLocalOffset = Vector2.zero;
        }

        rb.MovePosition(Vector2.Lerp(rb.position, predictedPosition, smoothing));
        rb.MoveRotation(Mathf.LerpAngle(rb.rotation, networkRotation, smoothing));
        driftVelocity = networkVelocity;
    }

    void BounceAgainstMapBounds()
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        Vector2 position = rb != null ? rb.position : (Vector2)transform.position;
        bool bounced = false;

        float minX = -mapSize.x * 0.5f + DriftBouncePadding;
        float maxX = mapSize.x * 0.5f - DriftBouncePadding;
        float minY = -mapSize.y * 0.5f + DriftBouncePadding;
        float maxY = mapSize.y * 0.5f - DriftBouncePadding;

        if (position.x < minX)
        {
            position.x = minX;
            driftVelocity.x = Mathf.Abs(driftVelocity.x);
            bounced = true;
        }
        else if (position.x > maxX)
        {
            position.x = maxX;
            driftVelocity.x = -Mathf.Abs(driftVelocity.x);
            bounced = true;
        }

        if (position.y < minY)
        {
            position.y = minY;
            driftVelocity.y = Mathf.Abs(driftVelocity.y);
            bounced = true;
        }
        else if (position.y > maxY)
        {
            position.y = maxY;
            driftVelocity.y = -Mathf.Abs(driftVelocity.y);
            bounced = true;
        }

        if (bounced)
        {
            if (rb != null)
            {
                rb.position = position;
                rb.angularVelocity = Mathf.Clamp(rb.angularVelocity * -0.75f, -MaxAngularSpeed, MaxAngularSpeed);
            }
            else
            {
                transform.position = position;
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!initialized || PhotonNetwork.CurrentRoom == null)
            return;

        if (isAuthority)
            ApplyCollisionResponse(collision);
    }

    public bool TryRequestRemoteImpulse(Vector2 impulse)
    {
        if (!initialized || isAuthority || !PhotonNetwork.IsConnected || Time.time < nextImpulseRequestTime)
            return false;

        if (impulse.sqrMagnitude < 0.0001f)
            return false;

        nextImpulseRequestTime = Time.time + ImpulseRequestCooldown;
        ApplyRemotePushPrediction(impulse);
        RequestImpulseFromClient(impulse);
        return true;
    }

    void ApplyRemotePushPrediction(Vector2 impulse)
    {
        if (isAuthority || rb == null)
            return;

        Vector2 offset = impulse * 0.072f;
        predictedLocalOffset = Vector2.ClampMagnitude(predictedLocalOffset + offset, 1.05f);
    }

    void ApplyCollisionResponse(Collision2D collision)
    {
        if (rb == null || collision == null)
            return;

        float relativeSpeed = collision.relativeVelocity.magnitude;
        if (relativeSpeed < 0.06f)
            return;

        ContactPoint2D contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
        Vector2 tangent = new Vector2(-contact.normal.y, contact.normal.x);
        float tangentialSpeed = Vector2.Dot(collision.relativeVelocity, tangent);
        float addedAngular = Mathf.Clamp(tangentialSpeed * 16f, -24f, 24f);

        if (Mathf.Abs(addedAngular) < 4.5f)
        {
            addedAngular = Mathf.Sign(tangentialSpeed == 0f ? Random.value - 0.5f : tangentialSpeed) * 4.5f;
        }

        float nextAngular = rb.angularVelocity + addedAngular;
        if (Random.value < 0.22f)
            nextAngular *= -0.85f;

        rb.angularVelocity = Mathf.Clamp(nextAngular, -MaxAngularSpeed, MaxAngularSpeed);
        baseAngularSpeed = Mathf.Sign(rb.angularVelocity == 0f ? baseAngularSpeed : rb.angularVelocity) * Mathf.Clamp(Mathf.Abs(baseAngularSpeed), 10f, 26f);
        driftVelocity = rb.linearVelocity;
    }

    public void Highlight()
    {
        if (!HasLoot || spriteRenderer == null)
            return;

        spriteRenderer.color = Color.green;
    }

    public void Unhighlight()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.color = defaultColor;
    }

    [PunRPC]
    public void ClearStoredItemRpc()
    {
        storedItemId = null;
        isBeingCollected = false;

        if (spriteRenderer != null)
            spriteRenderer.color = new Color(0.45f, 0.45f, 0.45f, 0.72f);
    }

    [PunRPC]
    public void SetBeingCollectedRpc(bool value)
    {
        isBeingCollected = value;
    }

    void RequestImpulseFromClient(Vector2 impulse)
    {
        if (!PhotonNetwork.IsConnected)
            return;

        Player masterClient = PhotonNetwork.MasterClient;
        if (masterClient == null)
            return;

        object[] payload = { photonView.ViewID, impulse.x, impulse.y };
        PhotonNetwork.RaiseEvent(ImpulseRequestEventCode, payload, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendUnreliable);
    }

    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case SnapshotEventCode:
                ApplySnapshot(photonEvent.CustomData as object[]);
                break;
            case ImpulseRequestEventCode:
                ApplyImpulseRequest(photonEvent.CustomData as object[]);
                break;
        }
    }

    void ApplySnapshot(object[] payload)
    {
        if (isAuthority || payload == null || payload.Length < 7)
            return;

        int viewId = ConvertToInt(payload[0]);
        if (photonView == null || photonView.ViewID != viewId)
            return;

        networkPosition = new Vector2(ConvertToFloat(payload[1]), ConvertToFloat(payload[2]));
        networkVelocity = new Vector2(ConvertToFloat(payload[3]), ConvertToFloat(payload[4]));
        networkRotation = ConvertToFloat(payload[5]);
        networkAngularVelocity = ConvertToFloat(payload[6]);
        hasNetworkState = true;
        predictedLocalOffset *= 0.35f;

        if (rb != null && Vector2.Distance(rb.position, networkPosition) > 2f)
        {
            rb.position = networkPosition;
            rb.rotation = networkRotation;
            predictedLocalOffset = Vector2.zero;
        }
    }

    void ApplyImpulseRequest(object[] payload)
    {
        if (!isAuthority || payload == null || payload.Length < 3 || rb == null)
            return;

        int viewId = ConvertToInt(payload[0]);
        if (photonView == null || photonView.ViewID != viewId)
            return;

        Vector2 impulse = new Vector2(ConvertToFloat(payload[1]), ConvertToFloat(payload[2]));
        impulse = Vector2.ClampMagnitude(impulse, 12f);
        MarkPushBoost(impulse.magnitude);
        rb.linearVelocity += impulse;
        driftVelocity = rb.linearVelocity;
        rb.angularVelocity = Mathf.Clamp(rb.angularVelocity + impulse.magnitude * 9f * Mathf.Sign(Random.value - 0.5f), -MaxAngularSpeed, MaxAngularSpeed);

        if (PhotonNetwork.IsConnected)
        {
            nextSnapshotTime = Time.time + SnapshotInterval;
            object[] snapshot = { photonView.ViewID, rb.position.x, rb.position.y, rb.linearVelocity.x, rb.linearVelocity.y, rb.rotation, rb.angularVelocity };
            PhotonNetwork.RaiseEvent(SnapshotEventCode, snapshot, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendUnreliable);
        }
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

    void MarkPushBoost(float impulseMagnitude)
    {
        pushBoostMaxSpeed = Mathf.Max(
            pushBoostMaxSpeed,
            Mathf.Lerp(PushBoostMaxSpeed * 0.58f, PushBoostMaxSpeed, Mathf.Clamp01(impulseMagnitude / 7f)));
        pushBoostUntil = Time.time + PushBoostDuration;
    }

    float GetPushBoostMaxSpeed()
    {
        if (Time.time <= pushBoostUntil)
            return pushBoostMaxSpeed;

        pushBoostMaxSpeed = 0f;
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

        return 0;
    }

    static Sprite LoadCrateSprite()
    {
        if (cachedCrateSprite != null)
            return cachedCrateSprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>("skrzynia_metal_clean_resource");
        cachedCrateSprite = GetLargestSprite(sprites);
        if (cachedCrateSprite != null)
            return cachedCrateSprite;

        Texture2D texture = Resources.Load<Texture2D>("skrzynia_metal_clean_resource");
        if (texture != null)
        {
            float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
            cachedCrateSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
            return cachedCrateSprite;
        }

        cachedCrateSprite = Resources.Load<Sprite>("skrzynia_metal_clean_resource");
        if (cachedCrateSprite != null)
        {
            return cachedCrateSprite;
        }

#if UNITY_EDITOR
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/skrzynia_metal_clean_resource.png");
        Sprite[] editorSprites = new Sprite[assets.Length];
        for (int i = 0; i < assets.Length; i++)
            editorSprites[i] = assets[i] as Sprite;
        cachedCrateSprite = GetLargestSprite(editorSprites);
        if (cachedCrateSprite != null)
            return cachedCrateSprite;

        cachedCrateSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/skrzynia_metal_clean_resource.png");
        if (cachedCrateSprite != null)
            return cachedCrateSprite;

        cachedCrateSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/" + CrateSpriteFileName);
#endif

        return cachedCrateSprite;
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

    static void FitSpriteToTargetSize(SpriteRenderer renderer, float targetMaxWorldSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        float maxDimension = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        if (maxDimension <= 0.0001f)
            return;

        float scale = targetMaxWorldSize / maxDimension;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
