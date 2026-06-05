using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

public sealed class NeutralRiderManager : MonoBehaviour
{
    const float ScanInterval = 0.35f;
    const float RuntimeComponentScanInterval = 1f;
    const string RuntimeStartTimeKey = "neutralRiders.runtime.startTime";
    const string RuntimeSpawnedCountKey = "neutralRiders.runtime.spawned";

    static NeutralRiderManager instance;

    double lastHandledStartTime = double.MinValue;
    float nextScanTime;
    float nextRuntimeScanTime;
    int spawnedThisRound;

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("NeutralRiderManager");
        instance = root.AddComponent<NeutralRiderManager>();
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
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            lastHandledStartTime = double.MinValue;
            spawnedThisRound = 0;
            return;
        }

        float now = Time.unscaledTime;
        if (now >= nextRuntimeScanTime)
        {
            nextRuntimeScanTime = now + RuntimeComponentScanInterval;
            EnsureRuntimeComponents();
        }

        if (now < nextScanTime)
            return;

        nextScanTime = now + ScanInterval;
        HandleLifecycle();
    }

    void EnsureRuntimeComponents()
    {
        PhotonView[] views = FindObjectsByType<PhotonView>(FindObjectsInactive.Exclude);
        for (int i = 0; i < views.Length; i++)
        {
            PhotonView view = views[i];
            if (view == null || view.gameObject == null)
                continue;

            if (!NeutralRiderController.IsNeutralRiderInstantiationData(view.InstantiationData))
                continue;

            NeutralRiderController rider = view.GetComponent<NeutralRiderController>();
            if (rider == null)
                rider = view.gameObject.AddComponent<NeutralRiderController>();

            rider.InitializeFromPhotonData();
            ActorIdentity.Ensure(view.gameObject);
        }
    }

    void HandleLifecycle()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        bool gameStarted = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object startedValue) &&
                           startedValue is bool started &&
                           started;

        if (!gameStarted)
        {
            lastHandledStartTime = double.MinValue;
            spawnedThisRound = 0;
            return;
        }

        double currentStartTime = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object startValue) &&
                                  startValue is double roomStart
            ? roomStart
            : 0d;

        if (currentStartTime != lastHandledStartTime)
        {
            lastHandledStartTime = currentStartTime;
            spawnedThisRound = SeedSpawnState(currentStartTime);
            EnsureRuntimeStartTime(currentStartTime);
        }

        if (!RoomSettings.AreNeutralRidersEnabled())
        {
            DestroyActiveNeutralRiders();
            spawnedThisRound = 0;
            SetSpawnedCount(0);
            return;
        }

        int desiredCount = RoomSettings.GetNeutralRiderCount();
        int activeCount = CountActiveNeutralRiders();
        int countToSpawn = Mathf.Max(0, desiredCount - Mathf.Max(activeCount, spawnedThisRound));
        for (int i = 0; i < countToSpawn; i++)
        {
            if (!SpawnNeutralRider(spawnedThisRound))
                break;

            spawnedThisRound++;
            SetSpawnedCount(spawnedThisRound);
        }
    }

    bool SpawnNeutralRider(int ordinal)
    {
        Vector2 spawn = GetSpawnPosition(ordinal);
        int skinIndex = ResolveShipSkin(ordinal);
        NeutralRiderArchetype archetype = ResolveArchetype(ordinal);
        string name = NeutralRiderController.GetGeneratedName(ordinal);
        object[] data = NeutralRiderController.BuildInstantiationData(skinIndex, archetype, name, ordinal);
        GameObject riderObject = PhotonNetwork.Instantiate("Player", spawn, Quaternion.identity, 0, data);
        if (riderObject == null)
            return false;

        NeutralRiderController rider = riderObject.GetComponent<NeutralRiderController>();
        if (rider == null)
            rider = riderObject.AddComponent<NeutralRiderController>();

        rider.InitializeFromPhotonData();
        ActorIdentity.Ensure(riderObject);
        GameVisualTheme.RequestRuntimeRefresh();
        return true;
    }

    Vector2 GetSpawnPosition(int ordinal)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(6f, mapSize.x * 0.5f - 5f);
        float halfY = Mathf.Max(6f, mapSize.y * 0.5f - 5f);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            int side = Mathf.Abs((ordinal + attempt) % 4);
            Vector2 candidate = side switch
            {
                0 => new Vector2(Random.Range(-halfX, halfX), halfY),
                1 => new Vector2(halfX, Random.Range(-halfY, halfY)),
                2 => new Vector2(Random.Range(-halfX, halfX), -halfY),
                _ => new Vector2(-halfX, Random.Range(-halfY, halfY))
            };

            if (IsSpawnClear(candidate))
                return candidate;
        }

        return new Vector2(Random.Range(-halfX, halfX), Random.Range(-halfY, halfY));
    }

    bool IsSpawnClear(Vector2 candidate)
    {
        int hitCount = Physics2DNonAllocQuery.OverlapCircle(candidate, 2.1f, out Collider2D[] hits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (hit.GetComponentInParent<PlayerHealth>() != null ||
                hit.GetComponentInParent<ObstacleChunk>() != null ||
                hit.GetComponentInParent<MovingSpaceObject>() != null)
            {
                return false;
            }
        }

        return true;
    }

    int ResolveShipSkin(int ordinal)
    {
        int[] skins =
        {
            ShipCatalog.ExplorerBasicSkinIndex,
            ShipCatalog.ViperStandardSkinIndex,
            ShipCatalog.AvengerDarkGreenSkinIndex,
            ShipCatalog.ArrowSmoothSkinIndex,
            ShipCatalog.InvaderCamoSkinIndex,
            ShipCatalog.CargoTruckGreenTruckSkinIndex
        };

        return skins[Mathf.Abs(ordinal * 3 + Mathf.RoundToInt(Time.time * 10f)) % skins.Length];
    }

    NeutralRiderArchetype ResolveArchetype(int ordinal)
    {
        NeutralRiderArchetype[] archetypes =
        {
            NeutralRiderArchetype.Scavenger,
            NeutralRiderArchetype.Raider,
            NeutralRiderArchetype.Hunter,
            NeutralRiderArchetype.Coward
        };

        return archetypes[Mathf.Abs(ordinal) % archetypes.Length];
    }

    int SeedSpawnState(double currentStartTime)
    {
        if (PhotonNetwork.CurrentRoom == null || currentStartTime <= 0d)
            return CountActiveNeutralRiders();

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RuntimeStartTimeKey, out object startValue) &&
            TryConvertToDouble(startValue, out double storedStartTime) &&
            Mathf.Abs((float)(storedStartTime - currentStartTime)) <= 0.001f &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RuntimeSpawnedCountKey, out object countValue) &&
            TryConvertToInt(countValue, out int storedCount))
        {
            return Mathf.Max(storedCount, CountActiveNeutralRiders());
        }

        return CountActiveNeutralRiders();
    }

    void EnsureRuntimeStartTime(double currentStartTime)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || currentStartTime <= 0d)
            return;

        bool hasMatchingStart = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RuntimeStartTimeKey, out object startValue) &&
                                TryConvertToDouble(startValue, out double storedStartTime) &&
                                Mathf.Abs((float)(storedStartTime - currentStartTime)) <= 0.001f;
        if (hasMatchingStart)
            return;

        Hashtable props = new Hashtable
        {
            [RuntimeStartTimeKey] = currentStartTime,
            [RuntimeSpawnedCountKey] = spawnedThisRound
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    void SetSpawnedCount(int value)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || lastHandledStartTime <= 0d)
            return;

        Hashtable props = new Hashtable
        {
            [RuntimeStartTimeKey] = lastHandledStartTime,
            [RuntimeSpawnedCountKey] = Mathf.Max(0, value)
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    int CountActiveNeutralRiders()
    {
        int count = 0;
        NeutralRiderController[] riders = FindObjectsByType<NeutralRiderController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < riders.Length; i++)
        {
            NeutralRiderController rider = riders[i];
            PlayerHealth riderHealth = rider != null ? rider.GetComponent<PlayerHealth>() : null;
            if (rider != null && riderHealth != null && !riderHealth.IsWreck && !rider.IsEvacuated)
                count++;
        }

        return count;
    }

    void DestroyActiveNeutralRiders()
    {
        NeutralRiderController[] riders = FindObjectsByType<NeutralRiderController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < riders.Length; i++)
        {
            NeutralRiderController rider = riders[i];
            if (rider == null || rider.photonView == null)
                continue;

            PlayerHealth riderHealth = rider.GetComponent<PlayerHealth>();
            if (riderHealth != null && riderHealth.IsWreck)
                continue;

            PhotonNetwork.Destroy(rider.gameObject);
        }
    }

    static bool TryConvertToInt(object value, out int result)
    {
        if (value is int intValue)
        {
            result = intValue;
            return true;
        }

        if (value is byte byteValue)
        {
            result = byteValue;
            return true;
        }

        if (value is short shortValue)
        {
            result = shortValue;
            return true;
        }

        result = 0;
        return false;
    }

    static bool TryConvertToDouble(object value, out double result)
    {
        if (value is double doubleValue)
        {
            result = doubleValue;
            return true;
        }

        if (value is float floatValue)
        {
            result = floatValue;
            return true;
        }

        if (value is int intValue)
        {
            result = intValue;
            return true;
        }

        result = 0d;
        return false;
    }
}
