using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

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
