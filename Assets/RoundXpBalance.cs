using System;
using UnityEngine;

public static class RoundXpBalance
{
    public const int AsteroidCollectXp = 15;
    public const int ContainerCollectXp = 5;
    public const int OtherCollectibleCollectXp = 15;
    public const int FirstLootXp = 25;
    public const int Collector15Xp = 100;
    public const int DroppedCargoLootXp = 35;
    public const int EnemyWreckLootXp = 40;
    public const int PlayerWreckLootXp = 50;

    public const int ShieldHitXp = 1;
    public const int ShieldHitXpCapPerTarget = 50;
    public const int HpDamageChunk = 10;
    public const int HpDamageChunkXp = 2;

    public const int KillDroneXp = 35;
    public const int KillSpaceMineXp = 20;
    public const int KillCorsairXp = 120;
    public const int KillSpaceTruckXp = 180;
    public const int KillPirateFighterXp = 70;
    public const int KillPirateFighterEliteXp = 105;
    public const int KillPirateFighterAceXp = 140;
    public const int KillRadarShipXp = 220;
    public const int KillMothershipXp = 400;
    public const int KillCosmicWormXp = 650;
    public const int KillRiftWardenXp = 260;
    public const int KillPlayerShipXp = 150;
    public const int FirstBloodXp = 75;

    public const int MagneticBeamSuccessXp = 25;
    public const int TractorBeamSuccessXp = 30;
    public const int BatteryMinimumRestoreForXp = 15;
    public const int BatterySuccessXp = 20;

    public const float ParticipationMinSeconds = 60f;
    public const int ParticipationXp = 25;
    public const float ActiveLossMinSeconds = 120f;
    public const int ActiveLossXp = 40;
    public const int ShipExtractionXp = 100;
    public const int AstronautEvacuationXp = 150;
    public const int SurvivorXp = 150;
    public const int HeavyCargoXp = 100;
    public const int RaiderCargoValueThreshold = 1000;
    public const int RaiderXp = 150;

    public const int BaseNextLevelXp = 400;
    public const int NextLevelQuadraticStep = 35;

    public static int GetTreasureCollectXp(string itemId)
    {
        if (InventoryItemCatalog.IsContainerItem(itemId) || InventoryItemCatalog.IsBlueprintScrapContainerItem(itemId))
            return ContainerCollectXp;

        return IsAsteroidResource(itemId) ? AsteroidCollectXp : OtherCollectibleCollectXp;
    }

    public static bool IsAsteroidResource(string itemId)
    {
        return string.Equals(itemId, InventoryItemCatalog.AsteroidCommonId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidUncommonId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidRareId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidVeryRareId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidEpicId, StringComparison.Ordinal) ||
               string.Equals(itemId, InventoryItemCatalog.AsteroidLegendaryId, StringComparison.Ordinal);
    }

    public static int GetEnemyKillXp(EnemyBotKind kind)
    {
        switch (kind)
        {
            case EnemyBotKind.SpaceMine: return KillSpaceMineXp;
            case EnemyBotKind.Corsair: return KillCorsairXp;
            case EnemyBotKind.SpaceTruck: return KillSpaceTruckXp;
            case EnemyBotKind.ContainerShip: return KillSpaceTruckXp;
            case EnemyBotKind.PirateFighter: return KillPirateFighterXp;
            case EnemyBotKind.PirateFighterElite: return KillPirateFighterEliteXp;
            case EnemyBotKind.PirateFighterAce: return KillPirateFighterAceXp;
            case EnemyBotKind.RadarShip: return KillRadarShipXp;
            case EnemyBotKind.Mothership: return KillMothershipXp;
            case EnemyBotKind.CosmicWorm: return KillCosmicWormXp;
            case EnemyBotKind.RiftWarden: return KillRiftWardenXp;
            default: return KillDroneXp;
        }
    }

    public static float GetMapXpMultiplier(string mapId)
    {
        if (string.Equals(mapId, "noob_haven", StringComparison.OrdinalIgnoreCase))
            return 0.75f;

        if (string.Equals(mapId, "minefield", StringComparison.OrdinalIgnoreCase))
            return 1.15f;

        if (string.Equals(mapId, "pirate_bay", StringComparison.OrdinalIgnoreCase))
            return 1.25f;

        if (string.Equals(mapId, "mothership", StringComparison.OrdinalIgnoreCase))
            return 1.4f;

        return 1f;
    }

    public static int ApplyMapMultiplier(int xp, string mapId)
    {
        return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, xp) * GetMapXpMultiplier(mapId)));
    }

    public static int GetLevelForTotalXp(int totalXp)
    {
        totalXp = Mathf.Max(0, totalXp);
        int level = 1;
        int remaining = totalXp;

        while (remaining >= GetRequiredXpForNextLevel(level))
        {
            remaining -= GetRequiredXpForNextLevel(level);
            level++;
        }

        return level;
    }

    public static int GetRequiredXpForNextLevel(int level)
    {
        level = Mathf.Max(1, level);
        return BaseNextLevelXp + level * level * NextLevelQuadraticStep;
    }
}
