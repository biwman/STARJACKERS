using System;
using UnityEngine;

public static class RuntimeSceneQueryCache
{
    const float PlayerRefreshInterval = 0.18f;
    const float CollectibleRefreshInterval = 0.34f;
    const float ExtractionZoneRefreshInterval = 0.5f;
    const float DestinationRefreshInterval = 0.75f;

    static PlayerHealth[] players = Array.Empty<PlayerHealth>();
    static Treasure[] treasures = Array.Empty<Treasure>();
    static DroppedCargoCrate[] droppedCargoCrates = Array.Empty<DroppedCargoCrate>();
    static ShipWreck[] shipWrecks = Array.Empty<ShipWreck>();
    static ExtractionZone[] extractionZones = Array.Empty<ExtractionZone>();
    static ScienceStation[] scienceStations = Array.Empty<ScienceStation>();
    static RepairBay[] repairBays = Array.Empty<RepairBay>();
    static SpaceFactory[] spaceFactories = Array.Empty<SpaceFactory>();

    static float nextPlayerRefreshTime;
    static float nextTreasureRefreshTime;
    static float nextDroppedCargoCrateRefreshTime;
    static float nextShipWreckRefreshTime;
    static float nextExtractionZoneRefreshTime;
    static float nextScienceStationRefreshTime;
    static float nextRepairBayRefreshTime;
    static float nextSpaceFactoryRefreshTime;

    public static PlayerHealth[] GetPlayers()
    {
        return GetCached(ref players, ref nextPlayerRefreshTime, PlayerRefreshInterval);
    }

    public static Treasure[] GetTreasures()
    {
        return GetCached(ref treasures, ref nextTreasureRefreshTime, CollectibleRefreshInterval);
    }

    public static DroppedCargoCrate[] GetDroppedCargoCrates()
    {
        return GetCached(ref droppedCargoCrates, ref nextDroppedCargoCrateRefreshTime, CollectibleRefreshInterval);
    }

    public static ShipWreck[] GetShipWrecks()
    {
        return GetCached(ref shipWrecks, ref nextShipWreckRefreshTime, CollectibleRefreshInterval);
    }

    public static ExtractionZone[] GetExtractionZones()
    {
        return GetCached(ref extractionZones, ref nextExtractionZoneRefreshTime, ExtractionZoneRefreshInterval);
    }

    public static ScienceStation[] GetScienceStations()
    {
        return GetCached(ref scienceStations, ref nextScienceStationRefreshTime, DestinationRefreshInterval);
    }

    public static RepairBay[] GetRepairBays()
    {
        return GetCached(ref repairBays, ref nextRepairBayRefreshTime, DestinationRefreshInterval);
    }

    public static SpaceFactory[] GetSpaceFactories()
    {
        return GetCached(ref spaceFactories, ref nextSpaceFactoryRefreshTime, DestinationRefreshInterval);
    }

    public static void InvalidateAll()
    {
        players = Array.Empty<PlayerHealth>();
        treasures = Array.Empty<Treasure>();
        droppedCargoCrates = Array.Empty<DroppedCargoCrate>();
        shipWrecks = Array.Empty<ShipWreck>();
        extractionZones = Array.Empty<ExtractionZone>();
        scienceStations = Array.Empty<ScienceStation>();
        repairBays = Array.Empty<RepairBay>();
        spaceFactories = Array.Empty<SpaceFactory>();

        nextPlayerRefreshTime = 0f;
        nextTreasureRefreshTime = 0f;
        nextDroppedCargoCrateRefreshTime = 0f;
        nextShipWreckRefreshTime = 0f;
        nextExtractionZoneRefreshTime = 0f;
        nextScienceStationRefreshTime = 0f;
        nextRepairBayRefreshTime = 0f;
        nextSpaceFactoryRefreshTime = 0f;
    }

    static T[] GetCached<T>(ref T[] cache, ref float nextRefreshTime, float refreshInterval) where T : UnityEngine.Object
    {
        if (cache == null || Time.unscaledTime >= nextRefreshTime)
        {
            cache = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude);
            nextRefreshTime = Time.unscaledTime + refreshInterval;
        }

        return cache;
    }
}
