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
    public string CloudsDensity { get; }
    public string CloudsSize { get; }
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
        : this(
            id,
            displayName,
            description,
            roundDurationSeconds,
            mapSize,
            loneShipTimerMultiplier,
            obstacleDensity,
            obstaclesDestroyEnabled,
            obstacleHp,
            obstacleSizePercent,
            obstaclesNoBorders,
            resourceDensity,
            resourceRichness,
            nebulaDensity,
            fireNebulaDensity,
            nebulaSize,
            fireNebulaSize,
            RoomSettings.DefaultCloudsDensity,
            RoomSettings.DefaultCloudsSize,
            extractionZoneCount,
            movingObjectsEnabled,
            obstacleMassFactor,
            treasureMassFactor,
            mapBackgroundIndex,
            visualEffectsEnabled,
            inventoryLossEnabled,
            equipmentLossEnabled,
            spaceJunkDensity,
            containersDensity,
            randomLootWreckCount,
            repairBayCount,
            spaceFactoryCount,
            enemyPresets)
    {
    }

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
        string cloudsDensity,
        string cloudsSize,
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
        CloudsDensity = RoomSettings.NormalizeCloudsDensity(cloudsDensity);
        CloudsSize = RoomSettings.NormalizeNebulaSize(cloudsSize);
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
    public const string JustSpaceMapId = "just_space";
    public const string NoobHavenMapId = "noob_haven";
    public const string MinefieldMapId = "minefield";
    public const string SnowFieldMapId = "snow_field";
    public const string DeepSpaceMapId = "deep_space";
    public const string PirateBayMapId = "pirate_bay";
    public const string AncientSpaceMapId = "ancient_space";
    public const string TheThreatMapId = "mothership";
    public const string GravityWellMapId = "gravity_well";

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
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceManta, false, 1, false, 100, 0, 1f, 0, 90, 40),
            new LobbyEnemyMapPreset(EnemyBotKind.GravitySquid, false, 1, false, 80, 70, 1f, 0, 120, 8),
            new LobbyEnemyMapPreset(EnemyBotKind.HunterLance, false, 1, false, 85, 115, 1f, 0, 120, 36),
            new LobbyEnemyMapPreset(EnemyBotKind.RadarShip, false, 1, false, 90, 110, 1.1f, 0, 90, 38),
            new LobbyEnemyMapPreset(EnemyBotKind.RescueShip, false, 1, false, 85, 95, 1.9f, 0, 90, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, false, 1, false, 200, 200, 1f, 0, 90)),
        new LobbyMapDefinition(
            NoobHavenMapId,
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
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceManta, false, 1, false, 100, 0, 1f, 0, 90, 40),
            new LobbyEnemyMapPreset(EnemyBotKind.GravitySquid, false, 1, false, 80, 70, 1f, 0, 120, 8),
            new LobbyEnemyMapPreset(EnemyBotKind.HunterLance, false, 1, false, 85, 115, 1f, 0, 120, 36),
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
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceManta, false, 1, false, 100, 0, 1f, 0, 90, 40),
            new LobbyEnemyMapPreset(EnemyBotKind.GravitySquid, false, 1, false, 80, 70, 1f, 0, 120, 8),
            new LobbyEnemyMapPreset(EnemyBotKind.HunterLance, false, 1, false, 85, 115, 1f, 0, 120, 36),
            new LobbyEnemyMapPreset(EnemyBotKind.RadarShip, false, 1, false, 90, 110, 1.1f, 0, 90, 38),
            new LobbyEnemyMapPreset(EnemyBotKind.RescueShip, false, 1, false, 85, 95, 1.9f, 0, 90, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, false, 1, false, 200, 200, 1f, 0, 90)),
        new LobbyMapDefinition(
            SnowFieldMapId,
            "SNOW FIELD",
            "Frozen open field on a snow planet. Clouds drift across the battlefield and hide sudden manta charges.\n" +
            "You lose your inventory if you die.\n" +
            "No loss of equipment.",
            300f,
            "very_large",
            1f,
            "medium",
            true,
            450,
            110,
            false,
            "medium",
            RoomSettings.ResourceRichnessHigh,
            "none",
            RoomSettings.DefaultFireNebulaDensity,
            RoomSettings.DefaultNebulaSize,
            RoomSettings.DefaultFireNebulaSize,
            RoomSettings.SpaceJunkDensityMedium,
            RoomSettings.NebulaSizeNormal,
            1,
            true,
            12,
            6,
            14,
            true,
            true,
            false,
            RoomSettings.SpaceJunkDensityLow,
            RoomSettings.ContainersDensityVeryLow,
            1,
            1,
            0,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, false, 1, false, 50, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, false, 1, false, 200, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, true, 5, false, 60, 20, 1f, 0, 120, 50),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, false, 1, false, 100, 50, 1.5f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, true, 2, true, 20, 20, 1.35f, 0, 90, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighter, false, 2, false, 50, 50, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterElite, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterAce, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceManta, true, 3, true, 100, 0, 1.05f, 0, 90, 40),
            new LobbyEnemyMapPreset(EnemyBotKind.GravitySquid, true, 2, true, 80, 70, 1f, 0, 120, 8),
            new LobbyEnemyMapPreset(EnemyBotKind.HunterLance, false, 1, false, 85, 115, 1f, 0, 120, 36),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateBase, false, 1, false, 500, 1000, 1f, 0, 90, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.RadarShip, false, 1, false, 90, 110, 1.1f, 0, 120, 38),
            new LobbyEnemyMapPreset(EnemyBotKind.RescueShip, false, 1, false, 85, 95, 1.9f, 0, 90, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, false, 1, false, 200, 200, 1f, 0, 90)),
        new LobbyMapDefinition(
            DeepSpaceMapId,
            "DEEP SPACE",
            "Far beyond regular trade routes, deep space is rich with rare materials and hunted by long-range lance ships.\n" +
            "You lose your inventory and equipment here if you die.",
            330f,
            "super_large",
            1f,
            "low",
            true,
            500,
            90,
            false,
            "medium",
            RoomSettings.ResourceRichnessVeryHigh,
            "low",
            RoomSettings.SpaceJunkDensityNone,
            RoomSettings.DefaultNebulaSize,
            RoomSettings.DefaultFireNebulaSize,
            RoomSettings.SpaceJunkDensityNone,
            RoomSettings.NebulaSizeNormal,
            2,
            true,
            12,
            6,
            20,
            true,
            true,
            true,
            RoomSettings.SpaceJunkDensityMedium,
            RoomSettings.ContainersDensityMedium,
            2,
            0,
            1,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, false, 1, false, 50, 20, 1f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, false, 1, false, 200, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, true, 8, false, 60, 20, 1f, 0, 120, 50),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, false, 1, false, 100, 50, 1.5f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, true, 3, true, 20, 20, 1.5f, 0, 90, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighter, false, 2, false, 50, 50, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterElite, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterAce, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceManta, false, 1, false, 100, 0, 1f, 0, 90, 40),
            new LobbyEnemyMapPreset(EnemyBotKind.GravitySquid, false, 1, false, 80, 70, 1f, 0, 120, 8),
            new LobbyEnemyMapPreset(EnemyBotKind.HunterLance, true, 3, true, 85, 115, 1.05f, 0, 60, 36),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateBase, false, 1, false, 500, 1000, 1f, 0, 90, 0),
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
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceManta, false, 1, false, 100, 0, 1f, 0, 90, 40),
            new LobbyEnemyMapPreset(EnemyBotKind.GravitySquid, false, 1, false, 80, 70, 1f, 0, 120, 8),
            new LobbyEnemyMapPreset(EnemyBotKind.HunterLance, false, 1, false, 85, 115, 1f, 0, 120, 36),
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
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceManta, false, 1, false, 100, 0, 1f, 0, 90, 40),
            new LobbyEnemyMapPreset(EnemyBotKind.GravitySquid, false, 1, false, 80, 70, 1f, 0, 120, 8),
            new LobbyEnemyMapPreset(EnemyBotKind.HunterLance, false, 1, false, 85, 115, 1f, 0, 120, 36),
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
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceManta, false, 1, false, 100, 0, 1f, 0, 90, 40),
            new LobbyEnemyMapPreset(EnemyBotKind.GravitySquid, false, 1, false, 80, 70, 1f, 0, 120, 8),
            new LobbyEnemyMapPreset(EnemyBotKind.HunterLance, false, 1, false, 85, 115, 1f, 0, 120, 36),
            new LobbyEnemyMapPreset(EnemyBotKind.RadarShip, true, 1, false, 90, 110, 1f, 0, 120, 38),
            new LobbyEnemyMapPreset(EnemyBotKind.RescueShip, true, 1, true, 50, 150, 2f, 0, 120, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, true, 1, false, 200, 200, 1f, 0, 90, 10)),
        new LobbyMapDefinition(
            GravityWellMapId,
            "GRAVITY WELL",
            "A cracked singularity bends the whole sector into a dangerous orbit of dust, wreckage and rare resources.\n" +
            "You lose your inventory and equipment here if you die.",
            330f,
            "super_large",
            1f,
            "medium",
            true,
            300,
            150,
            false,
            "medium",
            RoomSettings.ResourceRichnessVeryHigh,
            "low",
            RoomSettings.SpaceJunkDensityLow,
            RoomSettings.NebulaSizeNormal,
            RoomSettings.NebulaSizeSmall,
            RoomSettings.SpaceJunkDensityNone,
            RoomSettings.NebulaSizeNormal,
            2,
            true,
            12,
            6,
            21,
            true,
            true,
            true,
            RoomSettings.SpaceJunkDensityMedium,
            RoomSettings.ContainersDensityLow,
            2,
            0,
            1,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, false, 1, false, 50, 20, 1f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, false, 1, false, 200, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, true, 6, false, 60, 20, 1f, 15, 120, 50),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, true, 1, true, 100, 60, 1.25f, 0, 120, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, false, 2, false, 20, 20, 1.5f, 0, 90, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighter, false, 2, false, 50, 50, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterElite, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateFighterAce, false, 2, false, 66, 66, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceManta, false, 1, false, 100, 0, 1f, 0, 90, 40),
            new LobbyEnemyMapPreset(EnemyBotKind.GravitySquid, true, 3, true, 80, 85, 1.05f, 0, 90, 8),
            new LobbyEnemyMapPreset(EnemyBotKind.HunterLance, false, 1, false, 85, 115, 1f, 0, 120, 36),
            new LobbyEnemyMapPreset(EnemyBotKind.PirateBase, false, 1, false, 500, 1000, 1f, 0, 90, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.RadarShip, true, 2, true, 90, 120, 1.05f, 20, 120, 38),
            new LobbyEnemyMapPreset(EnemyBotKind.RescueShip, false, 1, false, 85, 95, 1.9f, 0, 90, 0),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, false, 1, false, 200, 200, 1f, 0, 90))
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

    public static string GetDefaultParallaxBackgroundId(string mapId)
    {
        switch (mapId)
        {
            case NoobHavenMapId:
                return RoomSettings.ParallaxBackgroundKosmos6;
            case AncientSpaceMapId:
                return RoomSettings.ParallaxBackgroundKosmos8;
            case JustSpaceMapId:
                return RoomSettings.ParallaxBackgroundKosmos9;
            case MinefieldMapId:
                return RoomSettings.ParallaxBackgroundKosmos10;
            case PirateBayMapId:
                return RoomSettings.ParallaxBackgroundKosmos11;
            case SnowFieldMapId:
                return RoomSettings.ParallaxBackgroundKosmos12;
            case DeepSpaceMapId:
                return RoomSettings.ParallaxBackgroundKosmos3;
            case TheThreatMapId:
                return RoomSettings.ParallaxBackgroundKosmos13;
            case GravityWellMapId:
                return RoomSettings.ParallaxBackgroundKosmos14;
            default:
                return RoomSettings.DefaultParallaxBackground;
        }
    }

    public static string GetDefaultBackgroundObjectId(string mapId)
    {
        switch (mapId)
        {
            case NoobHavenMapId:
                return RoomSettings.BackgroundObject2;
            case AncientSpaceMapId:
                return RoomSettings.BackgroundObject9;
            case JustSpaceMapId:
                return RoomSettings.BackgroundObject7;
            case MinefieldMapId:
                return RoomSettings.BackgroundObject1;
            case PirateBayMapId:
                return RoomSettings.BackgroundObject3;
            case SnowFieldMapId:
                return RoomSettings.BackgroundObject12;
            case DeepSpaceMapId:
                return RoomSettings.BackgroundObject6;
            case TheThreatMapId:
                return RoomSettings.BackgroundObject8;
            case GravityWellMapId:
                return RoomSettings.BackgroundObject13;
            default:
                return RoomSettings.DefaultBackgroundObject;
        }
    }

    public static string GetDefaultExtractionType(string mapId)
    {
        switch (mapId)
        {
            case JustSpaceMapId:
            case NoobHavenMapId:
                return RoomSettings.ExtractionTypeSpaceCity;
            case AncientSpaceMapId:
            case DeepSpaceMapId:
            case TheThreatMapId:
                return RoomSettings.ExtractionTypeCarrier;
        }

        return RoomSettings.DefaultExtractionType;
    }

    public static int GetDefaultScienceStationCount(string mapId)
    {
        return string.Equals(mapId, DeepSpaceMapId, System.StringComparison.Ordinal)
            ? 1
            : RoomSettings.DefaultScienceStationCount;
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
        props[RoomSettings.CloudsDensityKey] = map.CloudsDensity;
        props[RoomSettings.CloudsSizeKey] = map.CloudsSize;
        props[RoomSettings.ExtractionCountKey] = map.ExtractionZoneCount;
        props[RoomSettings.ExtractionTypeKey] = GetDefaultExtractionType(map.Id);
        props[RoomSettings.RepairBayCountKey] = map.RepairBayCount;
        props[RoomSettings.SpaceFactoryCountKey] = map.SpaceFactoryCount;
        props[RoomSettings.ScienceStationCountKey] = GetDefaultScienceStationCount(map.Id);
        props[RoomSettings.MovingObjectsEnabledKey] = map.MovingObjectsEnabled;
        props[RoomSettings.ObstacleWeightFactorKey] = map.ObstacleMassFactor;
        props[RoomSettings.TreasureWeightFactorKey] = map.TreasureMassFactor;
        props[RoomSettings.MapBackgroundKey] = map.MapBackgroundIndex;
        props[RoomSettings.VisualEffectsEnabledKey] = map.VisualEffectsEnabled;
        props[RoomSettings.ParallaxBackgroundKey] = GetDefaultParallaxBackgroundId(map.Id);
        props[RoomSettings.BackgroundObjectKey] = GetDefaultBackgroundObjectId(map.Id);
        props[RoomSettings.GravityWellPhysicsEnabledKey] = map.Id == GravityWellMapId;
        props[RoomSettings.HiddenTreasureEnabledKey] = map.Id == GravityWellMapId;
        props[RoomSettings.CosmicWormEnabledKey] = RoomSettings.DefaultCosmicWormEnabled;

        props[RoomSettings.EndDisasterModeKey] = RoomSettings.EndDisasterMeteor;
        props[RoomSettings.EndDisasterWarningSecondsKey] = RoomSettings.DefaultEndDisasterWarningSeconds;

        ApplyEnemyPresetsToProperties(map, props);
    }

    public static void ApplyEnemyPresetsToProperties(LobbyMapDefinition map, Hashtable props, ISet<string> preservedKeys = null)
    {
        if (map == null || props == null)
            return;

        for (int i = 0; i < map.EnemyPresets.Count; i++)
        {
            LobbyEnemyMapPreset enemyPreset = map.EnemyPresets[i];
            EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(enemyPreset.Kind);
            if (definition == null)
                continue;

            SetPresetProperty(props, definition.EnabledRoomKey, enemyPreset.Enabled, preservedKeys);
            SetPresetProperty(props, definition.CountRoomKey, enemyPreset.Count, preservedKeys);
            SetPresetProperty(props, definition.HpRoomKey, enemyPreset.Hp, preservedKeys);
            SetPresetProperty(props, definition.ShieldRoomKey, enemyPreset.Shield, preservedKeys);
            SetPresetProperty(props, definition.DamageRoomKey, enemyPreset.Damage >= 0 ? enemyPreset.Damage : definition.DefaultDamage, preservedKeys);
            SetPresetProperty(props, definition.SpeedRoomKey, enemyPreset.SpeedMultiplier, preservedKeys);
            SetPresetProperty(props, definition.SpawnSecondRoomKey, enemyPreset.FirstRespawnSeconds, preservedKeys);
            SetPresetProperty(props, definition.RespawnEnabledRoomKey, enemyPreset.RespawnEnabled, preservedKeys);
            SetPresetProperty(props, definition.RespawnIntervalRoomKey, enemyPreset.RespawnLoopSeconds, preservedKeys);

            if (enemyPreset.Kind == EnemyBotKind.Drone)
                SetPresetProperty(props, RoomSettings.EnemyBotsEnabledKey, enemyPreset.Enabled, preservedKeys);
            else if (enemyPreset.Kind == EnemyBotKind.Corsair)
            {
                SetPresetProperty(props, RoomSettings.CorsairEnabledKey, enemyPreset.Enabled, preservedKeys);
                SetPresetProperty(props, RoomSettings.CorsairSpawnSecondKey, enemyPreset.FirstRespawnSeconds, preservedKeys);
                SetPresetProperty(props, RoomSettings.CorsairHpKey, enemyPreset.Hp, preservedKeys);
            }
        }
    }

    static void SetPresetProperty(Hashtable props, string key, object value, ISet<string> preservedKeys)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (preservedKeys != null && preservedKeys.Contains(key))
            return;

        props[key] = value;
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
