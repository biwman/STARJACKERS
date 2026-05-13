using System.Collections.Generic;
using ExitGames.Client.Photon;

public sealed class LobbyEnemyMapPreset
{
    public EnemyBotKind Kind { get; }
    public bool Enabled { get; }
    public int Count { get; }
    public bool RespawnEnabled { get; }
    public int Hp { get; }
    public int Shield { get; }
    public int Damage { get; }
    public float SpeedMultiplier { get; }
    public int FirstRespawnSeconds { get; }
    public int RespawnLoopSeconds { get; }

    public LobbyEnemyMapPreset(EnemyBotKind kind, bool enabled, int count, bool respawnEnabled, int hp, int firstRespawnSeconds, int respawnLoopSeconds)
        : this(kind, enabled, count, respawnEnabled, hp, RoomSettings.DefaultEnemyShield, RoomSettings.DefaultEnemySpeedMultiplier, firstRespawnSeconds, respawnLoopSeconds)
    {
    }

    public LobbyEnemyMapPreset(EnemyBotKind kind, bool enabled, int count, bool respawnEnabled, int hp, int shield, float speedMultiplier, int firstRespawnSeconds, int respawnLoopSeconds, int damage = -1)
    {
        Kind = kind;
        Enabled = enabled;
        Count = count;
        RespawnEnabled = respawnEnabled;
        Hp = hp;
        Shield = shield;
        Damage = damage;
        SpeedMultiplier = speedMultiplier;
        FirstRespawnSeconds = firstRespawnSeconds;
        RespawnLoopSeconds = respawnLoopSeconds;
    }
}

public sealed class LobbyMapDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public float RoundDurationSeconds { get; }
    public string MapSize { get; }
    public float LoneShipTimerMultiplier { get; }
    public string ObstacleDensity { get; }
    public bool ObstaclesDestroyEnabled { get; }
    public int ObstacleHp { get; }
    public int ObstacleSizePercent { get; }
    public bool ObstaclesNoBorders { get; }
    public string ResourceDensity { get; }
    public string ResourceRichness { get; }
    public string NebulaDensity { get; }
    public string FireNebulaDensity { get; }
    public string NebulaSize { get; }
    public string FireNebulaSize { get; }
    public int ExtractionZoneCount { get; }
    public bool MovingObjectsEnabled { get; }
    public int ObstacleMassFactor { get; }
    public int TreasureMassFactor { get; }
    public int MapBackgroundIndex { get; }
    public bool VisualEffectsEnabled { get; }
    public bool InventoryLossEnabled { get; }
    public bool EquipmentLossEnabled { get; }
    public string SpaceJunkDensity { get; }
    public string ContainersDensity { get; }
    public int RandomLootWreckCount { get; }
    public int RepairBayCount { get; }
    public int SpaceFactoryCount { get; }
    public IReadOnlyList<LobbyEnemyMapPreset> EnemyPresets { get; }

    public LobbyMapDefinition(
        string id,
        string displayName,
        string description,
        float roundDurationSeconds,
        string mapSize,
        float loneShipTimerMultiplier,
        string obstacleDensity,
        bool obstaclesDestroyEnabled,
        int obstacleHp,
        int obstacleSizePercent,
        bool obstaclesNoBorders,
        string resourceDensity,
        string resourceRichness,
        string nebulaDensity,
        string fireNebulaDensity,
        string nebulaSize,
        string fireNebulaSize,
        int extractionZoneCount,
        bool movingObjectsEnabled,
        int obstacleMassFactor,
        int treasureMassFactor,
        int mapBackgroundIndex,
        bool visualEffectsEnabled,
        bool inventoryLossEnabled,
        bool equipmentLossEnabled,
        string spaceJunkDensity,
        string containersDensity,
        int randomLootWreckCount,
        int repairBayCount,
        int spaceFactoryCount,
        params LobbyEnemyMapPreset[] enemyPresets)
    {
        Id = id;
        DisplayName = displayName;
        Description = string.IsNullOrWhiteSpace(description) ? displayName : description;
        RoundDurationSeconds = roundDurationSeconds;
        MapSize = mapSize;
        LoneShipTimerMultiplier = loneShipTimerMultiplier;
        ObstacleDensity = obstacleDensity;
        ObstaclesDestroyEnabled = obstaclesDestroyEnabled;
        ObstacleHp = obstacleHp;
        ObstacleSizePercent = obstacleSizePercent;
        ObstaclesNoBorders = obstaclesNoBorders;
        ResourceDensity = resourceDensity;
        ResourceRichness = resourceRichness;
        NebulaDensity = nebulaDensity;
        FireNebulaDensity = string.IsNullOrWhiteSpace(fireNebulaDensity) ? RoomSettings.DefaultFireNebulaDensity : fireNebulaDensity;
        NebulaSize = RoomSettings.NormalizeNebulaSize(nebulaSize);
        FireNebulaSize = RoomSettings.NormalizeNebulaSize(fireNebulaSize);
        ExtractionZoneCount = extractionZoneCount;
        MovingObjectsEnabled = movingObjectsEnabled;
        ObstacleMassFactor = obstacleMassFactor;
        TreasureMassFactor = treasureMassFactor;
        MapBackgroundIndex = mapBackgroundIndex;
        VisualEffectsEnabled = visualEffectsEnabled;
        InventoryLossEnabled = inventoryLossEnabled;
        EquipmentLossEnabled = equipmentLossEnabled;
        SpaceJunkDensity = string.IsNullOrWhiteSpace(spaceJunkDensity) ? RoomSettings.DefaultSpaceJunkDensity : spaceJunkDensity;
        ContainersDensity = string.IsNullOrWhiteSpace(containersDensity) ? RoomSettings.DefaultContainersDensity : containersDensity;
        RandomLootWreckCount = randomLootWreckCount;
        RepairBayCount = repairBayCount;
        SpaceFactoryCount = spaceFactoryCount;
        EnemyPresets = enemyPresets ?? System.Array.Empty<LobbyEnemyMapPreset>();
    }
}

public static class LobbyMapCatalog
{
    static readonly LobbyMapDefinition[] Maps =
    {
        new LobbyMapDefinition(
            "just_space",
            "JUST SPACE",
            "Empty space. Or almost empty. Learn here how to shoot with other players.\n" +
            "No neutral ships. No resources.\n" +
            "No loss of inventory.\n" +
            "No loss of equipment.\n" +
            "Only one extraction zone.",
            240f,
            "large",
            1f,
            "low",
            true,
            400,
            100,
            false,
            "none",
            RoomSettings.ResourceRichnessVeryLow,
            "low",
            RoomSettings.DefaultFireNebulaDensity,
            RoomSettings.DefaultNebulaSize,
            RoomSettings.DefaultFireNebulaSize,
            1,
            true,
            12,
            6,
            1,
            true,
            false,
            false,
            RoomSettings.SpaceJunkDensityNone,
            RoomSettings.ContainersDensityNone,
            0,
            1,
            RoomSettings.DefaultSpaceFactoryCount,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, false, 1, false, 50, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, false, 1, false, 200, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, false, 30, false, 60, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, false, 1, false, 100, 50, 1.5f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, false, 2, false, 20, 20, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighter, false, 2, false, 50, 50, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterElite, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterAce, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.RadarShip, false, 1, false, 90, 110, 1.1f, 0, 90, 38),
            new LobbyEnemyMapPreset(EnemyBotKind.RescueShip, false, 1, false, 85, 95, 1.9f, 0, 90, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, false, 1, false, 200, 200, 1f, 0, 90)),
        new LobbyMapDefinition(
            "noob_haven",
            "NOOB HAVEN",
            "Loot your first asteroids. Beware of neutral pilots.\n" +
            "You lose your inventory if you die.\n" +
            "No loss of equipment.",
            300f,
            "very_large",
            1f,
            "medium",
            true,
            400,
            100,
            false,
            "low",
            RoomSettings.ResourceRichnessVeryLow,
            "low",
            RoomSettings.DefaultFireNebulaDensity,
            RoomSettings.DefaultNebulaSize,
            RoomSettings.DefaultFireNebulaSize,
            2,
            true,
            12,
            6,
            12,
            true,
            true,
            false,
            RoomSettings.SpaceJunkDensityLow,
            RoomSettings.ContainersDensityVeryLow,
            0,
            1,
            RoomSettings.DefaultSpaceFactoryCount,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, false, 1, false, 50, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, false, 1, false, 200, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, false, 30, false, 60, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, true, 1, false, 100, 60, 1.5f, 0, 90, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, true, 5, true, 20, 20, 1.5f, 0, 90, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighter, false, 2, false, 50, 50, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterElite, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterAce, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.RadarShip, false, 1, false, 90, 110, 1.1f, 0, 90, 38),
            new LobbyEnemyMapPreset(EnemyBotKind.RescueShip, false, 1, false, 85, 95, 1.9f, 0, 90, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, false, 1, false, 200, 200, 1f, 0, 90)),
        new LobbyMapDefinition(
            "minefield",
            "MINEFIELD",
            "If you avoid nasty mines You can find real treasures here.\n" +
            "You lose your inventory if you die.\n" +
            "No loss of equipment.",
            240f,
            "large",
            1f,
            "medium",
            true,
            400,
            100,
            false,
            "low",
            RoomSettings.ResourceRichnessLow,
            "low",
            RoomSettings.DefaultFireNebulaDensity,
            RoomSettings.DefaultNebulaSize,
            RoomSettings.DefaultFireNebulaSize,
            2,
            true,
            12,
            6,
            9,
            true,
            true,
            false,
            RoomSettings.SpaceJunkDensityLow,
            RoomSettings.ContainersDensityLow,
            1,
            1,
            RoomSettings.DefaultSpaceFactoryCount,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, true, 2, true, 50, 40, 1f, 0, 90, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, false, 1, false, 200, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, true, 30, true, 60, 20, 1f, 0, 90, 50),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, false, 1, false, 100, 50, 1.5f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, false, 2, false, 20, 20, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighter, false, 2, false, 50, 50, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterElite, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterAce, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.RadarShip, false, 1, false, 90, 110, 1.1f, 0, 90, 38),
            new LobbyEnemyMapPreset(EnemyBotKind.RescueShip, false, 1, false, 85, 95, 1.9f, 0, 90, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, false, 1, false, 200, 200, 1f, 0, 90)),
        new LobbyMapDefinition(
            "pirate_bay",
            "PIRATE BAY",
            "Real game starts here. Survive or die. Pirates are not happy to give you their treasures.\n" +
            "You lose your inventory and equipment here if you die",
            240f,
            "very_large",
            1f,
            "low",
            true,
            400,
            100,
            false,
            "low",
            RoomSettings.ResourceRichnessMedium,
            "low",
            RoomSettings.DefaultFireNebulaDensity,
            RoomSettings.DefaultNebulaSize,
            RoomSettings.DefaultFireNebulaSize,
            2,
            true,
            12,
            6,
            5,
            true,
            true,
            true,
            RoomSettings.SpaceJunkDensityMedium,
            RoomSettings.ContainersDensityMedium,
            1,
            1,
            RoomSettings.DefaultSpaceFactoryCount,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, true, 1, true, 50, 40, 1f, 0, 90, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, true, 1, true, 100, 100, 1f, 0, 120, 20),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, true, 4, true, 60, 20, 1f, 0, 150, 50),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, false, 1, false, 100, 50, 1.5f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, false, 2, false, 20, 20, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighter, true, 3, true, 50, 50, 2f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterElite, false, 3, true, 66, 66, 2f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterAce, false, 3, true, 66, 66, 2f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.RadarShip, false, 1, false, 90, 110, 1.1f, 0, 90, 38),
            new LobbyEnemyMapPreset(EnemyBotKind.RescueShip, true, 1, true, 50, 150, 2f, 0, 120, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, false, 1, false, 200, 200, 1f, 0, 90)),
        new LobbyMapDefinition(
            "ancient_space",
            "ANCIENT SPACE",
            "Mysteries and Secrets are hidden in this far corner of the galaxy.\n" +
            "You lose your inventory and equipment here if you die",
            300f,
            "very_large",
            1f,
            "medium",
            true,
            400,
            100,
            false,
            "low",
            RoomSettings.ResourceRichnessMedium,
            "none",
            RoomSettings.SpaceJunkDensityMedium,
            RoomSettings.DefaultNebulaSize,
            RoomSettings.DefaultFireNebulaSize,
            2,
            true,
            12,
            6,
            19,
            true,
            true,
            true,
            RoomSettings.SpaceJunkDensityLow,
            RoomSettings.ContainersDensityLow,
            2,
            0,
            1,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, true, 2, true, 50, 40, 1f, 0, 90, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, false, 1, false, 200, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, true, 10, true, 60, 20, 1f, 0, 90, 50),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, false, 1, false, 100, 50, 1.5f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, true, 8, true, 20, 20, 1.5f, 0, 90, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighter, false, 2, false, 50, 50, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterElite, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterAce, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateBase, false, 1, false, 500, 1000, 1f, 0, 90, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.RadarShip, false, 1, false, 90, 110, 1.1f, 0, 90, 38),
            new LobbyEnemyMapPreset(EnemyBotKind.RescueShip, false, 1, false, 85, 95, 1.9f, 0, 90, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, false, 1, false, 200, 200, 1f, 0, 90)),
        new LobbyMapDefinition(
            "mothership",
            "THE THREAT",
            "Something deadly is lurking for You. Run for your lives!\n" +
            "You lose your inventory and equipment here if you die. ",
            300f,
            "large",
            1f,
            "medium",
            true,
            400,
            100,
            false,
            "medium",
            RoomSettings.ResourceRichnessMedium,
            "low",
            RoomSettings.DefaultFireNebulaDensity,
            RoomSettings.DefaultNebulaSize,
            RoomSettings.DefaultFireNebulaSize,
            2,
            true,
            12,
            6,
            6,
            true,
            true,
            true,
            RoomSettings.SpaceJunkDensityLow,
            RoomSettings.ContainersDensityMedium,
            1,
            1,
            RoomSettings.DefaultSpaceFactoryCount,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, false, 3, true, 100, 20, 1f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, false, 1, true, 250, 20, 1f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, false, 10, false, 20, 20, 1f, 20, 120),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, false, 1, false, 100, 50, 1.5f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, false, 2, false, 20, 20, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighter, false, 2, false, 50, 50, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterElite, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterAce, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.RadarShip, true, 1, false, 90, 110, 1f, 0, 120, 38),
            new LobbyEnemyMapPreset(EnemyBotKind.RescueShip, true, 1, true, 50, 150, 2f, 0, 120, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, true, 1, false, 200, 200, 1f, 0, 90, 10))
    };

    static readonly Dictionary<string, LobbyMapDefinition> MapsById = BuildMapsById();

    public static IReadOnlyList<LobbyMapDefinition> AllMaps => Maps;

    public static LobbyMapDefinition GetDefault()
    {
        return Get(RoomSettings.DefaultLobbyMapId) ?? Maps[0];
    }

    public static LobbyMapDefinition Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return GetDefault();

        return MapsById.TryGetValue(id, out LobbyMapDefinition map) ? map : GetDefault();
    }

    public static void ApplyToProperties(LobbyMapDefinition map, Hashtable props)
    {
        if (map == null || props == null)
            return;

        props[RoomSettings.SelectedMapKey] = map.Id;
        props[RoomSettings.RoundDurationKey] = map.RoundDurationSeconds;
        props[RoomSettings.MapSizeKey] = map.MapSize;
        props[RoomSettings.LastShipTimerMultiplierKey] = map.LoneShipTimerMultiplier;
        props[RoomSettings.InventoryLossEnabledKey] = map.InventoryLossEnabled;
        props[RoomSettings.EquipmentLossEnabledKey] = map.EquipmentLossEnabled;
        props[RoomSettings.ObstacleDensityKey] = map.ObstacleDensity;
        props[RoomSettings.ObstacleDestroyEnabledKey] = map.ObstaclesDestroyEnabled;
        props[RoomSettings.ObstacleHpKey] = map.ObstacleHp;
        props[RoomSettings.ObstacleSizePercentKey] = map.ObstacleSizePercent;
        props[RoomSettings.ObstacleNoBordersKey] = map.ObstaclesNoBorders;
        props[RoomSettings.TreasureDensityKey] = map.ResourceDensity;
        props[RoomSettings.ResourceRichnessKey] = map.ResourceRichness;
        props[RoomSettings.SpaceJunkDensityKey] = map.SpaceJunkDensity;
        props[RoomSettings.ContainersDensityKey] = map.ContainersDensity;
        props[RoomSettings.RandomLootWreckCountKey] = map.RandomLootWreckCount;
        props[RoomSettings.NebulaDensityKey] = map.NebulaDensity;
        props[RoomSettings.FireNebulaDensityKey] = map.FireNebulaDensity;
        props[RoomSettings.NebulaSizeKey] = map.NebulaSize;
        props[RoomSettings.FireNebulaSizeKey] = map.FireNebulaSize;
        props[RoomSettings.ExtractionCountKey] = map.ExtractionZoneCount;
        props[RoomSettings.RepairBayCountKey] = map.RepairBayCount;
        props[RoomSettings.SpaceFactoryCountKey] = map.SpaceFactoryCount;
        props[RoomSettings.MovingObjectsEnabledKey] = map.MovingObjectsEnabled;
        props[RoomSettings.ObstacleWeightFactorKey] = map.ObstacleMassFactor;
        props[RoomSettings.TreasureWeightFactorKey] = map.TreasureMassFactor;
        props[RoomSettings.MapBackgroundKey] = map.MapBackgroundIndex;
        props[RoomSettings.VisualEffectsEnabledKey] = map.VisualEffectsEnabled;
        props[RoomSettings.EndDisasterModeKey] = RoomSettings.EndDisasterMeteor;
        props[RoomSettings.EndDisasterWarningSecondsKey] = RoomSettings.DefaultEndDisasterWarningSeconds;

        for (int i = 0; i < map.EnemyPresets.Count; i++)
        {
            LobbyEnemyMapPreset enemyPreset = map.EnemyPresets[i];
            EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(enemyPreset.Kind);
            if (definition == null)
                continue;

            props[definition.EnabledRoomKey] = enemyPreset.Enabled;
            props[definition.CountRoomKey] = enemyPreset.Count;
            props[definition.HpRoomKey] = enemyPreset.Hp;
            props[definition.ShieldRoomKey] = enemyPreset.Shield;
            props[definition.DamageRoomKey] = enemyPreset.Damage >= 0 ? enemyPreset.Damage : definition.DefaultDamage;
            props[definition.SpeedRoomKey] = enemyPreset.SpeedMultiplier;
            props[definition.SpawnSecondRoomKey] = enemyPreset.FirstRespawnSeconds;
            props[definition.RespawnEnabledRoomKey] = enemyPreset.RespawnEnabled;
            props[definition.RespawnIntervalRoomKey] = enemyPreset.RespawnLoopSeconds;

            if (enemyPreset.Kind == EnemyBotKind.Drone)
                props[RoomSettings.EnemyBotsEnabledKey] = enemyPreset.Enabled;
            else if (enemyPreset.Kind == EnemyBotKind.Corsair)
            {
                props[RoomSettings.CorsairEnabledKey] = enemyPreset.Enabled;
                props[RoomSettings.CorsairSpawnSecondKey] = enemyPreset.FirstRespawnSeconds;
                props[RoomSettings.CorsairHpKey] = enemyPreset.Hp;
            }
        }
    }

    static Dictionary<string, LobbyMapDefinition> BuildMapsById()
    {
        Dictionary<string, LobbyMapDefinition> maps = new Dictionary<string, LobbyMapDefinition>();
        for (int i = 0; i < Maps.Length; i++)
        {
            if (Maps[i] != null && !string.IsNullOrWhiteSpace(Maps[i].Id))
                maps[Maps[i].Id] = Maps[i];
        }

        return maps;
    }
}
