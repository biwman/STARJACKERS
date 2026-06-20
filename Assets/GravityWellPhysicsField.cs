using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;

public sealed class GravityWellPhysicsField : MonoBehaviourPunCallbacks
{
    const float CacheRefreshInterval = 0.8f;
    const float InnerDeadRadius = 1.7f;
    const float PullAcceleration = 0.82f;
    const float TangentialAcceleration = 0.48f;
    const float MaxFieldAddedSpeed = 4.1f;
    const float PlayerScale = 1.16f;
    const float AstronautScale = 0.42f;
    const float WreckScale = 1.05f;
    const float TreasureScale = 1.55f;
    const float ObstacleErosionMinInterval = 6f;
    const float ObstacleErosionMaxInterval = 12f;
    const int ObstacleErosionMinDamage = 10;
    const int ObstacleErosionMaxDamage = 50;

    static GravityWellPhysicsField instance;

    Rigidbody2D[] cachedBodies = System.Array.Empty<Rigidbody2D>();
    readonly Dictionary<string, float> nextObstacleErosionById = new Dictionary<string, float>();
    float nextCacheRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("GravityWellPhysicsField");
        instance = root.AddComponent<GravityWellPhysicsField>();
        DontDestroyOnLoad(root);
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

    void FixedUpdate()
    {
        if (!ShouldRun())
        {
            if (cachedBodies.Length > 0)
                cachedBodies = System.Array.Empty<Rigidbody2D>();
            if (nextObstacleErosionById.Count > 0)
                nextObstacleErosionById.Clear();
            return;
        }

        if (Time.time >= nextCacheRefresh)
        {
            cachedBodies = FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Exclude);
            nextCacheRefresh = Time.time + CacheRefreshInterval;
        }

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float fieldRadius = Mathf.Max(10f, Mathf.Min(mapSize.x, mapSize.y) * 0.68f);
        for (int i = 0; i < cachedBodies.Length; i++)
            ApplyField(cachedBodies[i], fieldRadius);
    }

    bool ShouldRun()
    {
        return PhotonNetwork.InRoom &&
               RoomSettings.GetSessionState() == RoomSettings.SessionStateInPlay &&
               RoomSettings.IsGravityWellPhysicsEnabled();
    }

    void ApplyField(Rigidbody2D body, float fieldRadius)
    {
        if (body == null ||
            !body.simulated ||
            body.bodyType != RigidbodyType2D.Dynamic ||
            !TryResolveAuthorityAndScale(body, out float objectScale))
        {
            return;
        }

        Vector2 position = body.position;
        float distance = position.magnitude;
        if (distance <= InnerDeadRadius || distance > fieldRadius)
            return;

        float normalizedDistance = Mathf.InverseLerp(InnerDeadRadius, fieldRadius, distance);
        float strength = Mathf.Lerp(1.22f, 0.28f, Mathf.SmoothStep(0f, 1f, normalizedDistance));
        if (strength <= 0.001f)
            return;

        Vector2 toCenter = -position.normalized;
        Vector2 tangent = new Vector2(-toCenter.y, toCenter.x);
        float massFactor = 1f / Mathf.Sqrt(Mathf.Max(0.45f, body.mass));
        float orbitalStrength = Mathf.Lerp(1.18f, 0.5f, normalizedDistance);
        Vector2 acceleration =
            toCenter * (PullAcceleration * strength) +
            tangent * (TangentialAcceleration * orbitalStrength);

        Vector2 currentVelocity = body.linearVelocity;
        Vector2 nextVelocity = currentVelocity + acceleration * (objectScale * massFactor * Time.fixedDeltaTime);
        float speedCap = Mathf.Max(currentVelocity.magnitude, MaxFieldAddedSpeed * Mathf.Max(0.35f, objectScale));
        if (nextVelocity.sqrMagnitude > speedCap * speedCap)
            nextVelocity = nextVelocity.normalized * speedCap;

        body.linearVelocity = nextVelocity;
        TryApplyObstacleErosion(body, strength);
    }

    void TryApplyObstacleErosion(Rigidbody2D body, float fieldStrength)
    {
        if (!PhotonNetwork.IsMasterClient ||
            !RoomSettings.AreObstaclesDestructible() ||
            !RoomSettings.ShouldMovingObjectsRotate() ||
            fieldStrength <= 0.001f)
        {
            return;
        }

        MovingSpaceObject movingObject = body.GetComponentInParent<MovingSpaceObject>();
        if (movingObject == null || movingObject.ObjectType != MovingSpaceObject.SpaceObjectType.Obstacle)
            return;

        ObstacleChunk chunk = body.GetComponentInParent<ObstacleChunk>();
        if (chunk == null || string.IsNullOrWhiteSpace(chunk.StableId) || chunk.CurrentHealth <= 0)
            return;

        if (Mathf.Abs(body.angularVelocity) < 1f)
            return;

        float now = Time.time;
        if (!nextObstacleErosionById.TryGetValue(chunk.StableId, out float nextDamageTime))
        {
            nextObstacleErosionById[chunk.StableId] = now + Random.Range(ObstacleErosionMinInterval * 0.35f, ObstacleErosionMaxInterval);
            return;
        }

        if (now < nextDamageTime)
            return;

        int damage = Random.Range(ObstacleErosionMinDamage, ObstacleErosionMaxDamage + 1);
        nextObstacleErosionById[chunk.StableId] = now + Random.Range(ObstacleErosionMinInterval, ObstacleErosionMaxInterval);
        ObstacleSpawner.ApplyObstacleDamage(chunk.StableId, damage);

        if (ObstacleChunk.Find(chunk.StableId) == null)
            nextObstacleErosionById.Remove(chunk.StableId);
    }

    bool TryResolveAuthorityAndScale(Rigidbody2D body, out float objectScale)
    {
        objectScale = 1f;

        PlayerHealth health = body.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            if (health.IsBotControlled)
                return false;

            if (health.IsWreck)
            {
                objectScale = WreckScale;
                return PhotonNetwork.IsMasterClient;
            }

            PhotonView view = health.photonView;
            if (view != null && !view.IsMine)
                return false;

            objectScale = health.IsAstronautControlled ? AstronautScale : PlayerScale;
            return true;
        }

        if (body.GetComponentInParent<EnemyBot>() != null ||
            body.GetComponentInParent<PlayerDeployableBase>() != null ||
            body.GetComponentInParent<LureBeaconDecoy>() != null)
        {
            return false;
        }

        if (!PhotonNetwork.IsMasterClient)
            return false;

        if (body.GetComponentInParent<Treasure>() != null ||
            body.GetComponentInParent<DroppedCargoCrate>() != null)
        {
            objectScale = TreasureScale;
            return true;
        }

        if (body.GetComponentInParent<ShipWreck>() != null ||
            body.GetComponentInParent<MovingSpaceObject>() != null)
        {
            objectScale = WreckScale;
            return true;
        }

        return false;
    }
}
