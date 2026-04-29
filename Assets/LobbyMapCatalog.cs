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
    public int ExtractionZoneCount { get; }
    public bool MovingObjectsEnabled { get; }
    public int ObstacleMassFactor { get; }
    public int TreasureMassFactor { get; }
    public int MapBackgroundIndex { get; }
    public bool VisualEffectsEnabled { get; }
    public IReadOnlyList<LobbyEnemyMapPreset> EnemyPresets { get; }

    public LobbyMapDefinition(
        string id,
        string displayName,
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
        int extractionZoneCount,
        bool movingObjectsEnabled,
        int obstacleMassFactor,
        int treasureMassFactor,
        int mapBackgroundIndex,
        bool visualEffectsEnabled,
        params LobbyEnemyMapPreset[] enemyPresets)
    {
        Id = id;
        DisplayName = displayName;
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
        ExtractionZoneCount = extractionZoneCount;
        MovingObjectsEnabled = movingObjectsEnabled;
        ObstacleMassFactor = obstacleMassFactor;
        TreasureMassFactor = treasureMassFactor;
        MapBackgroundIndex = mapBackgroundIndex;
        VisualEffectsEnabled = visualEffectsEnabled;
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
            180f,
            "very_large",
            1f,
            "medium",
            true,
            300,
            100,
            false,
            "medium",
            RoomSettings.DefaultResourceRichness,
            "low",
            2,
            true,
            RoomSettings.DefaultObstacleWeightFactor,
            RoomSettings.DefaultTreasureWeightFactor,
            1,
            true,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, true, 1, true, 50, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, true, 1, false, 200, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, false, 30, false, 60, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, false, 1, false, 100, 50, 1.5f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, false, 2, false, 20, 20, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, false, 1, false, 200, 200, 1f, 0, 90)),
        new LobbyMapDefinition(
            "noob_haven",
            "NOOB HAVEN",
            240f,
            "large",
            1f,
            "medium",
            false,
            300,
            100,
            false,
            "medium",
            RoomSettings.ResourceRichnessLow,
            "low",
            2,
            true,
            RoomSettings.DefaultObstacleWeightFactor,
            RoomSettings.DefaultTreasureWeightFactor,
            12,
            true,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, false, 1, false, 50, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, false, 1, false, 200, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, false, 30, false, 60, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, false, 1, false, 100, 50, 1.5f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, false, 2, false, 20, 20, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, false, 1, false, 200, 200, 1f, 0, 90)),
        new LobbyMapDefinition(
            "minefield",
            "MINEFIELD",
            180f,
            "medium",
            1.5f,
            "high",
            true,
            300,
            100,
            false,
            "high",
            RoomSettings.DefaultResourceRichness,
            "medium",
            3,
            true,
            RoomSettings.DefaultObstacleWeightFactor,
            RoomSettings.DefaultTreasureWeightFactor,
            9,
            true,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, false, 1, false, 50, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, false, 1, false, 200, 20, 1f, 0, 60),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, true, 30, true, 60, 20, 1f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, false, 1, false, 100, 50, 1.5f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, false, 2, false, 20, 20, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, false, 1, false, 200, 200, 1f, 0, 90)),
        new LobbyMapDefinition(
            "pirate_bay",
            "PIRATE BAY",
            180f,
            "large",
            1f,
            "medium",
            true,
            200,
            100,
            false,
            "high",
            RoomSettings.DefaultResourceRichness,
            "low",
            3,
            true,
            12,
            RoomSettings.DefaultTreasureWeightFactor,
            5,
            true,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, true, 3, true, 100, 20, 1f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, true, 1, true, 250, 20, 1f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, true, 10, false, 20, 20, 1f, 20, 120),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, false, 1, false, 100, 50, 1.5f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, false, 2, false, 20, 20, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, false, 1, false, 200, 200, 1f, 0, 90)),
        new LobbyMapDefinition(
            "mothership",
            "MOTHERSHIP",
            180f,
            "large",
            1f,
            "medium",
            true,
            200,
            100,
            false,
            "high",
            RoomSettings.DefaultResourceRichness,
            "low",
            2,
            true,
            12,
            RoomSettings.DefaultTreasureWeightFactor,
            6,
            true,
            new LobbyEnemyMapPreset(EnemyBotKind.Drone, false, 3, true, 100, 20, 1f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.Corsair, false, 1, true, 250, 20, 1f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceMine, false, 10, false, 20, 20, 1f, 20, 120),
            new LobbyEnemyMapPreset(EnemyBotKind.SpaceTruck, false, 1, false, 100, 50, 1.5f, 0, 90),
            new LobbyEnemyMapPreset(EnemyBotKind.NeutralFighter, false, 2, false, 20, 20, 1.5f, 0, 60, 10),
            new LobbyEnemyMapPreset(EnemyBotKind.Mothership, true, 1, false, 200, 200, 1f, 0, 90))
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
        props[RoomSettings.ObstacleDensityKey] = map.ObstacleDensity;
        props[RoomSettings.ObstacleDestroyEnabledKey] = map.ObstaclesDestroyEnabled;
        props[RoomSettings.ObstacleHpKey] = map.ObstacleHp;
        props[RoomSettings.ObstacleSizePercentKey] = map.ObstacleSizePercent;
        props[RoomSettings.ObstacleNoBordersKey] = map.ObstaclesNoBorders;
        props[RoomSettings.TreasureDensityKey] = map.ResourceDensity;
        props[RoomSettings.ResourceRichnessKey] = map.ResourceRichness;
        props[RoomSettings.NebulaDensityKey] = map.NebulaDensity;
        props[RoomSettings.ExtractionCountKey] = map.ExtractionZoneCount;
        props[RoomSettings.RepairBayCountKey] = RoomSettings.DefaultRepairBayCount;
        props[RoomSettings.MovingObjectsEnabledKey] = map.MovingObjectsEnabled;
        props[RoomSettings.ObstacleWeightFactorKey] = map.ObstacleMassFactor;
        props[RoomSettings.TreasureWeightFactorKey] = map.TreasureMassFactor;
        props[RoomSettings.MapBackgroundKey] = map.MapBackgroundIndex;
        props[RoomSettings.VisualEffectsEnabledKey] = map.VisualEffectsEnabled;
        props[RoomSettings.EndDisasterModeKey] = RoomSettings.EndDisasterMeteor;

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
