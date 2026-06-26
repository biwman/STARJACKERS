using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class DroppedCargoManager : MonoBehaviourPunCallbacks
{
    static DroppedCargoManager instance;
    float nextScanTime;

    public static DroppedCargoManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<DroppedCargoManager>();
                if (instance == null)
                {
                    GameObject root = new GameObject("DroppedCargoManager");
                    instance = root.AddComponent<DroppedCargoManager>();
                }
            }

            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (Time.unscaledTime < nextScanTime)
            return;

        nextScanTime = Time.unscaledTime + 0.15f;
        EnsureRuntimeComponents();
    }

    public static void EnsureExists()
    {
        _ = Instance;
    }

    public static void DropItemFromShip(string itemId, Transform shipTransform)
    {
        if (string.IsNullOrWhiteSpace(itemId) || shipTransform == null || !PhotonNetwork.InRoom)
            return;

        EnsureExists();

        Vector2 behindDirection = -(Vector2)shipTransform.up;
        float dropDistance = 0.9f;
        Vector3 dropPosition = shipTransform.position + (Vector3)(behindDirection * dropDistance);

        float spreadSeed = Mathf.Abs((itemId + PhotonNetwork.Time.ToString("F3")).GetHashCode());
        float spreadAngle = Mathf.Lerp(-22f, 22f, Mathf.PerlinNoise(spreadSeed * 0.001f, 0.17f));
        Vector2 driftDirection = Quaternion.Euler(0f, 0f, spreadAngle) * behindDirection;
        Vector2 driftVelocity = driftDirection.normalized * Mathf.Lerp(0.45f, 0.85f, Mathf.PerlinNoise(0.63f, spreadSeed * 0.0013f));

        SpawnDroppedCargoCrate(itemId, dropPosition, driftVelocity);
    }

    public static void DropItemAtPosition(string itemId, Vector3 dropPosition, Vector2 driftVelocity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !PhotonNetwork.InRoom)
            return;

        EnsureExists();

        if (driftVelocity.sqrMagnitude < 0.0001f)
            driftVelocity = Random.insideUnitCircle.normalized * 0.55f;

        if (driftVelocity.sqrMagnitude < 0.0001f)
            driftVelocity = Vector2.down * 0.55f;

        SpawnDroppedCargoCrate(itemId, dropPosition, Vector2.ClampMagnitude(driftVelocity, 1.25f));
    }

    static void SpawnDroppedCargoCrate(string itemId, Vector3 dropPosition, Vector2 driftVelocity)
    {
        GameObject crateObject = PhotonNetwork.Instantiate(
            "DroppedCargoCrate",
            dropPosition,
            Quaternion.identity,
            0,
            new object[] { itemId, driftVelocity.x, driftVelocity.y });

        if (crateObject != null)
        {
            PhotonView crateView = crateObject.GetComponent<PhotonView>();
            if (crateView != null && crateObject.GetComponent<DroppedCargoCrate>() == null)
            {
                DroppedCargoCrate crate = crateObject.AddComponent<DroppedCargoCrate>();
                crate.InitializeFromPhotonData();
            }

            GameVisualTheme.RequestRuntimeRefresh(crateObject);
        }
    }

    void EnsureRuntimeComponents()
    {
        PhotonView[] views = FindObjectsByType<PhotonView>(FindObjectsInactive.Exclude);
        for (int i = 0; i < views.Length; i++)
        {
            PhotonView view = views[i];
            if (view == null || view.IsRoomView || view.gameObject == null)
                continue;

            if (!view.gameObject.name.StartsWith("DroppedCargoCrate"))
                continue;

            DroppedCargoCrate crate = view.GetComponent<DroppedCargoCrate>();
            if (crate == null)
            {
                crate = view.gameObject.AddComponent<DroppedCargoCrate>();
            }

            crate.InitializeFromPhotonData();
        }
    }

    public override void OnLeftRoom()
    {
        if (instance == this)
            instance = null;
    }
}
