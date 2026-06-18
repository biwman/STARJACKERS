using ExitGames.Client.Photon;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public sealed class NeutralRiderManager : MonoBehaviour
{
    const float ScanInterval = 0.35f;
    const float RuntimeComponentScanInterval = 1f;
    const float RelaxedRuntimeComponentScanInterval = 5f;
    const string RuntimeStartTimeKey = "neutralRiders.runtime.startTime";
    const string RuntimeSpawnedCountKey = "neutralRiders.runtime.spawned";

    static NeutralRiderManager instance;
    static readonly List<NeutralRiderController> ActiveRiderBuffer = new List<NeutralRiderController>(8);

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
            bool performedRuntimeSetup = EnsureRuntimeComponents();
            nextRuntimeScanTime = now + (performedRuntimeSetup ? RuntimeComponentScanInterval : RelaxedRuntimeComponentScanInterval);
        }

        if (now < nextScanTime)
            return;

        nextScanTime = now + ScanInterval;
        HandleLifecycle();
    }

    bool EnsureRuntimeComponents()
    {
        bool performedRuntimeSetup = false;
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
            {
                rider = view.gameObject.AddComponent<NeutralRiderController>();
                performedRuntimeSetup = true;
            }

            rider.InitializeFromPhotonData();
            ActorIdentity.Ensure(view.gameObject);
        }

        return performedRuntimeSetup;
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
        NeutralRiderArchetype archetype = ResolveArchetype(ordinal);
        int loadoutSeed = ResolveRoundSeed(ordinal, 173);
        int skinIndex = ResolveShipSkin(ordinal, archetype, loadoutSeed);
        string name = NeutralRiderController.GetGeneratedName(ordinal);
        object[] data = NeutralRiderController.BuildInstantiationData(skinIndex, archetype, name, ordinal, loadoutSeed);
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
        Vector2 mapSize = RoomSettings.GetEnemyNavigableMapDimensions();
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

    int ResolveShipSkin(int ordinal, NeutralRiderArchetype archetype, int loadoutSeed)
    {
        int[] skins = archetype switch
        {
            NeutralRiderArchetype.Hunter => new[]
            {
                ShipCatalog.AvengerDarkGreenSkinIndex,
                ShipCatalog.AvengerMilitarySkinIndex,
                ShipCatalog.AvengerNasaSkinIndex,
                ShipCatalog.ArrowSharkSkinIndex,
                ShipCatalog.PathfinderClassicSkinIndex,
                ShipCatalog.InvaderGoldplateSkinIndex
            },
            NeutralRiderArchetype.Raider => new[]
            {
                ShipCatalog.ViperStandardSkinIndex,
                ShipCatalog.ViperSnowSkinIndex,
                ShipCatalog.ViperNavySkinIndex,
                ShipCatalog.InvaderCamoSkinIndex,
                ShipCatalog.InvaderVolcanicSkinIndex,
                ShipCatalog.ArrowSmoothSkinIndex,
                ShipCatalog.PathfinderAngelSkinIndex
            },
            NeutralRiderArchetype.Coward => new[]
            {
                ShipCatalog.ExplorerBasicSkinIndex,
                ShipCatalog.ExplorerSilverSkinIndex,
                ShipCatalog.ExplorerGildedSkinIndex,
                ShipCatalog.ArrowSmoothSkinIndex,
                ShipCatalog.PathfinderClassicSkinIndex,
                ShipCatalog.CargoTruckWhitePantherSkinIndex
            },
            _ => new[]
            {
                ShipCatalog.ExplorerGildedSkinIndex,
                ShipCatalog.ExplorerSilverSkinIndex,
                ShipCatalog.ArrowSportySkinIndex,
                ShipCatalog.PathfinderClassicSkinIndex,
                ShipCatalog.InvaderGoldplateSkinIndex,
                ShipCatalog.CargoTruckGreenTruckSkinIndex,
                ShipCatalog.CargoTruckSandSigmaSkinIndex
            }
        };

        return skins[Mathf.Abs(loadoutSeed + ordinal * 37 + (int)archetype * 11) % skins.Length];
    }

    NeutralRiderArchetype ResolveArchetype(int ordinal)
    {
        NeutralRiderArchetype[] archetypes = ordinal == 0
            ? new[]
            {
                NeutralRiderArchetype.Raider,
                NeutralRiderArchetype.Hunter,
                NeutralRiderArchetype.Raider,
                NeutralRiderArchetype.Scavenger
            }
            : new[]
        {
            NeutralRiderArchetype.Scavenger,
            NeutralRiderArchetype.Raider,
            NeutralRiderArchetype.Hunter,
            NeutralRiderArchetype.Coward,
            NeutralRiderArchetype.Raider,
            NeutralRiderArchetype.Hunter
        };

        int seed = ResolveRoundSeed(ordinal, 71);
        return archetypes[Mathf.Abs(seed) % archetypes.Length];
    }

    int ResolveRoundSeed(int ordinal, int salt)
    {
        long timeSeed = lastHandledStartTime > 0d
            ? (long)(lastHandledStartTime * 1000d)
            : PhotonNetwork.ServerTimestamp;

        unchecked
        {
            int hash = (int)(timeSeed ^ (timeSeed >> 32));
            hash = (hash * 397) ^ (ordinal * 92821);
            hash = (hash * 397) ^ (spawnedThisRound * 68917);
            hash = (hash * 397) ^ salt;
            return hash & int.MaxValue;
        }
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
        return NeutralRiderController.CountActiveRiders();
    }

    void DestroyActiveNeutralRiders()
    {
        NeutralRiderController.CopyActiveRiders(ActiveRiderBuffer);
        for (int i = 0; i < ActiveRiderBuffer.Count; i++)
        {
            NeutralRiderController rider = ActiveRiderBuffer[i];
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
