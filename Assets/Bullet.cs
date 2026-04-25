using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviourPun
{
    static readonly List<Collider2D> ActiveBulletColliders = new List<Collider2D>();

    public int damage = 10;
    public int ownerViewID;
    public float rangeMultiplier = 15f;
    public float fallbackPlayerLength = 1f;
    public float safetyLifetime = 10f;
    public float minimumWorldRadius = 0.12f;

    Vector2 spawnPosition;
    float maxTravelDistance;
    float visualScaleMultiplier = 1f;
    Color visualColor = Color.white;
    bool destroyRequested;

    void Start()
    {
        ApplyPhotonConfig();
        ApplyVisualConfig();

        spawnPosition = transform.position;
        maxTravelDistance = GetOwnerLength() * rangeMultiplier;

        if (GetComponent<HideInNebulaTarget>() == null)
        {
            gameObject.AddComponent<HideInNebulaTarget>();
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.mass = 0.005f * RoomSettings.GetBulletPushMultiplier();
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
        }

        CircleCollider2D collider2D = GetComponent<CircleCollider2D>();
        if (collider2D != null)
        {
            float worldRadius = GetWorldRadius(collider2D);
            if (worldRadius < minimumWorldRadius)
            {
                SetWorldRadius(collider2D, minimumWorldRadius);
            }

            for (int i = ActiveBulletColliders.Count - 1; i >= 0; i--)
            {
                Collider2D other = ActiveBulletColliders[i];
                if (other == null)
                {
                    ActiveBulletColliders.RemoveAt(i);
                    continue;
                }

                Physics2D.IgnoreCollision(collider2D, other, true);
            }

            ActiveBulletColliders.Add(collider2D);
        }

        if (maxTravelDistance <= 0f)
        {
            maxTravelDistance = fallbackPlayerLength * rangeMultiplier;
        }

        StartCoroutine(DestroyAfterSafetyLifetime());
    }

    void Update()
    {
        if (!photonView.IsMine)
            return;

        if (Vector2.Distance(spawnPosition, transform.position) >= maxTravelDistance)
        {
            DestroyBullet();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!photonView.IsMine)
            return;

        if (collision.gameObject.GetComponentInParent<Bullet>() != null)
            return;

        PlayerHealth hp = collision.gameObject.GetComponentInParent<PlayerHealth>();
        if (hp != null && hp.photonView.ViewID != ownerViewID)
        {
            Vector2 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            hp.photonView.RPC("TakeDamageAt", RpcTarget.MasterClient, damage, ownerViewID, impactPoint.x, impactPoint.y);
        }

        ObstacleChunk obstacleChunk = collision.gameObject.GetComponentInParent<ObstacleChunk>();
        if (obstacleChunk != null && !string.IsNullOrWhiteSpace(obstacleChunk.StableId) && RoomSettings.AreObstaclesDestructible())
        {
            SpaceObjectMotionSync.RequestObstacleDamage(obstacleChunk.StableId, damage);
        }

        ApplyBulletPush(collision);

        DestroyBullet();
    }

    void ApplyBulletPush(Collision2D collision)
    {
        if (collision == null)
            return;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        Vector2 bulletVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
        if (bulletVelocity.sqrMagnitude < 0.0001f)
            return;

        float pushMultiplier = RoomSettings.GetBulletPushMultiplier();
        Vector2 impulse = bulletVelocity * (0.22f * pushMultiplier);

        MovingSpaceObject movingObject = collision.collider != null
            ? collision.collider.GetComponentInParent<MovingSpaceObject>()
            : null;
        if (movingObject != null && !string.IsNullOrWhiteSpace(movingObject.StableId))
        {
            SpaceObjectMotionSync.RequestImpulse(movingObject.StableId, impulse);
            return;
        }

        Rigidbody2D hitBody = collision.rigidbody;
        if (hitBody == null || hitBody.bodyType != RigidbodyType2D.Dynamic)
            return;

        hitBody.AddForce(impulse, ForceMode2D.Impulse);
    }

    IEnumerator DestroyAfterSafetyLifetime()
    {
        yield return new WaitForSeconds(safetyLifetime);

        if (this == null || gameObject == null)
            yield break;

        if (PhotonNetwork.IsConnected && photonView != null)
        {
            if (photonView.IsMine)
            {
                PhotonNetwork.Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void DestroyBullet()
    {
        if (destroyRequested)
            return;

        destroyRequested = true;

        if (PhotonNetwork.IsConnected && photonView != null)
        {
            if (photonView.IsMine)
            {
                PhotonNetwork.Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
        {
            ActiveBulletColliders.Remove(collider2D);
        }
    }

    void ApplyPhotonConfig()
    {
        object[] data = photonView != null ? photonView.InstantiationData : null;
        if (data == null || data.Length < 7)
            return;

        ownerViewID = data[0] is int ownerId ? ownerId : ownerViewID;
        damage = data[1] is int configuredDamage ? configuredDamage : damage;
        visualScaleMultiplier = data[2] is float scale ? scale : visualScaleMultiplier;

        float r = data[3] is float colorR ? colorR : visualColor.r;
        float g = data[4] is float colorG ? colorG : visualColor.g;
        float b = data[5] is float colorB ? colorB : visualColor.b;
        float a = data[6] is float colorA ? colorA : visualColor.a;
        visualColor = new Color(r, g, b, a);

        if (data.Length >= 8 && data[7] is float configuredRangeMultiplier)
            rangeMultiplier = Mathf.Max(0.1f, configuredRangeMultiplier);
    }

    void ApplyVisualConfig()
    {
        transform.localScale *= Mathf.Max(0.2f, visualScaleMultiplier);

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            spriteRenderer.color = visualColor;
    }

    float GetOwnerLength()
    {
        PhotonView ownerView = PhotonView.Find(ownerViewID);
        if (ownerView == null)
            return fallbackPlayerLength;

        SpriteRenderer spriteRenderer = ownerView.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            return Mathf.Max(spriteRenderer.bounds.size.x, spriteRenderer.bounds.size.y);
        }

        Collider2D collider2D = ownerView.GetComponentInChildren<Collider2D>();
        if (collider2D != null)
        {
            return Mathf.Max(collider2D.bounds.size.x, collider2D.bounds.size.y);
        }

        return fallbackPlayerLength;
    }

    float GetWorldRadius(CircleCollider2D collider2D)
    {
        Vector3 scale = collider2D.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        return collider2D.radius * maxScale;
    }

    void SetWorldRadius(CircleCollider2D collider2D, float worldRadius)
    {
        Vector3 scale = collider2D.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        if (maxScale <= 0.0001f)
            maxScale = 1f;

        collider2D.radius = worldRadius / maxScale;
    }
}
