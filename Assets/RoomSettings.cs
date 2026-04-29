using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public static class RoomSettings
{
    public const string SessionStateKey = "sessionState";
    public const string SessionLabelKey = "sessionLabel";
    public const string SessionHostNameKey = "sessionHostName";
    public const string SessionCreatedAtKey = "sessionCreatedAt";
    public const string StartTimeKey = "startTime";
    public const string RoundDurationKey = "roundDuration";
    public const string ObstacleDensityKey = "obstacleDensity";
    public const string ObstacleDestroyEnabledKey = "obstacleDestroyEnabled";
    public const string ObstacleHpKey = "obstacleHp";
    public const string ObstacleSizePercentKey = "obstacleSizePercent";
    public const string ObstacleNoBordersKey = "obstacleNoBorders";
    public const string TreasureDensityKey = "treasureDensity";
    public const string ResourceRichnessKey = "resourceRichness";
    public const string NebulaDensityKey = "nebulaDensity";
    public const string ExtractionCountKey = "extractionCount";
    public const string RepairBayCountKey = "repairBayCount";
    public const string BoosterSlowdownKey = "boosterSlowdownPercent";
    public const string AmmoCountKey = "ammoCount";
    public const string BoosterRecoveryDelayKey = "boosterRecoveryDelay";
    public const string MaxInputBoostPercentKey = "maxInputBoostPercent";
    public const string ShipDriftEnabledKey = "shipDriftEnabled";
    public const string LastShipTimerMultiplierKey = "lastShipTimerMultiplier";
    public const string KillRewardPercentKey = "killRewardPercent";
    public const string DeathRetainPercentKey = "deathRetainPercent";
    public const string TimeUpRetainPercentKey = "timeUpRetainPercent";
    public const string MapSizeKey = "mapSize";
    public const string MapBackgroundKey = "mapBackground";
    public const string SelectedMapKey = "selectedMap";
    public const string VisualEffectsEnabledKey = "visualEffectsEnabled";
    public const string StartingVfxEnabledKey = "startingVfxEnabled";
    public const string EndDisasterModeKey = "endDisasterMode";
    public const string MovingObjectsEnabledKey = "movingObjectsEnabled";
    public const string EnemyBotsEnabledKey = "enemyBotsEnabled";
    public const string CorsairEnabledKey = "corsairEnabled";
    public const string CorsairSpawnSecondKey = "corsairSpawnSecond";
    public const string CorsairHpKey = "corsairHp";
    public const string BulletPushMultiplierKey = "bulletPushMultiplier";
    public const string ObstacleWeightFactorKey = "obstacleWeightFactor";
    public const string TreasureWeightFactorKey = "treasureWeightFactor";
    public const string BatteringDamageKey = "batteringDamage";
    public const string RoundResultsKey = "roundResultsSnapshot";
    public const string RoundEndReasonKey = "roundEndReason";
    public const string ShipSkinKey = "shipSkinIndex";
    public const string ShipInventoryStateKey = "shipInventoryState";
    public const string EquipmentStateKey = "equipmentState";
    public const string GadgetChargesStateKey = "gadgetChargesState";
    public const string ScoreKey = "score";

    public const float DefaultRoundDuration = 180f;
    public const string DefaultObstacleDensity = "high";
    public const bool DefaultObstacleDestroyEnabled = false;
    public const int DefaultObstacleHp = 60;
    public const int DefaultObstacleSizePercent = 100;
    public const bool DefaultObstacleNoBorders = false;
    public const int DefaultExtractionCount = 3;
    public const int DefaultRepairBayCount = 1;
    public const int DefaultBoosterSlowdownPercent = 30;
    public const int DefaultAmmoCount = 15;
    public const int DefaultBoosterRecoveryDelay = 5;
    public const int DefaultMaxInputBoostPercent = 20;
    public const int DefaultShipDriftLevel = 1;
    public const float DefaultLastShipTimerMultiplier = 3f;
    public const int DefaultKillRewardPercent = 50;
    public const int DefaultDeathRetainPercent = 25;
    public const int DefaultTimeUpRetainPercent = 25;
    public const string DefaultMapSize = "medium";
    public const int DefaultMapBackground = 5;
    public const string DefaultLobbyMapId = "just_space";
    public const bool DefaultVisualEffectsEnabled = true;
    public const bool DefaultStartingVfxEnabled = true;
    public const string EndDisasterOff = "off";
    public const string EndDisasterMeteor = "meteor";
    public const string DefaultEndDisasterMode = EndDisasterMeteor;
    public const bool DefaultMovingObjectsEnabled = true;
    public const bool DefaultEnemyBotsEnabled = true;
    public const bool DefaultCorsairEnabled = true;
    public const int DefaultCorsairSpawnSecond = 0;
    public const int DefaultCorsairHp = 200;
    public const int DefaultBulletPushMultiplier = 1;
    public const int DefaultObstacleWeightFactor = 6;
    public const int DefaultTreasureWeightFactor = 6;
    public const int DefaultBatteringDamage = 0;
    public const int MaxObstacleWeightFactor = 999;
    public const bool DefaultEnemyRespawnEnabled = false;
    public const int DefaultEnemyRespawnIntervalSeconds = 60;
    public const int DefaultEnemyShield = 20;
    public const float DefaultEnemySpeedMultiplier = 1f;
    public const string SessionStateInLobby = "in_lobby";
    public const string SessionStateInPlay = "in_play";
    public const string SessionStateClosingLobby = "closing_lobby";
    public const string SessionStateSummary = "summary";
    public const string MovingObjectsModeOn = "on";
    public const string MovingObjectsModeOff = "off";
    public const string MovingObjectsModeOnlyRotate = "only_rotate";
    public const string DefaultMovingObjectsMode = MovingObjectsModeOn;
    public const string ResourceRichnessVeryLow = "very_low";
    public const string ResourceRichnessLow = "low";
    public const string ResourceRichnessMedium = "medium";
    public const string ResourceRichnessHigh = "high";
    public const string ResourceRichnessVeryHigh = "very_high";
    public const string ResourceRichnessExtreme = "extreme";
    public const string DefaultResourceRichness = ResourceRichnessMedium;

    public static float GetRoundDuration()
    {
        if (TryGetFloat(RoundDurationKey, out float value))
            return value;

        return DefaultRoundDuration;
    }

    public static string GetSessionState()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(SessionStateKey, out object value) &&
            value is string state &&
            !string.IsNullOrWhiteSpace(state))
        {
            return state;
        }

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object startedValue) &&
            startedValue is bool started &&
            started)
        {
            return SessionStateInPlay;
        }

        return SessionStateInLobby;
    }

    public static bool AreObstaclesDestructible()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ObstacleDestroyEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultObstacleDestroyEnabled;
    }

    public static int GetObstacleHp()
    {
        return GetInt(ObstacleHpKey, DefaultObstacleHp, 50, 300);
    }

    public static int GetObstacleSizePercent()
    {
        return GetInt(ObstacleSizePercentKey, DefaultObstacleSizePercent, 50, 500);
    }

    public static float GetObstacleSizeMultiplier()
    {
        return Mathf.Max(0.1f, GetObstacleSizePercent() / 100f);
    }

    public static int GetObstacleMaxSplitCount()
    {
        int sizePercent = GetObstacleSizePercent();
        if (sizePercent >= 400)
            return 6;

        if (sizePercent >= 200)
            return 5;

        return 4;
    }

    public static bool AreObstaclesBorderless()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ObstacleNoBordersKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultObstacleNoBorders;
    }

    public static int GetExtractionCount()
    {
        return GetInt(ExtractionCountKey, DefaultExtractionCount, 1, 4);
    }

    public static int GetRepairBayCount()
    {
        return GetInt(RepairBayCountKey, DefaultRepairBayCount, 0, 2);
    }

    public static int GetBoosterSlowdownPercent()
    {
        return GetInt(BoosterSlowdownKey, DefaultBoosterSlowdownPercent, 30, 100);
    }

    public static int GetAmmoCount()
    {
        return GetInt(AmmoCountKey, DefaultAmmoCount, 5, 30);
    }

    public static int GetBoosterRecoveryDelay()
    {
        return GetInt(BoosterRecoveryDelayKey, DefaultBoosterRecoveryDelay, 0, 10);
    }

    public static int GetMaxInputBoostPercent()
    {
        return GetInt(MaxInputBoostPercentKey, DefaultMaxInputBoostPercent, 0, 50);
    }

    public static int GetShipDriftLevel()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ShipDriftEnabledKey, out object value) &&
            value is int level)
        {
            return Mathf.Clamp(level, 0, 10);
        }

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ShipDriftEnabledKey, out value) &&
            value is bool enabled)
        {
            return enabled ? 1 : 0;
        }

        return DefaultShipDriftLevel;
    }

    public static bool IsShipDriftEnabled()
    {
        return GetShipDriftLevel() > 0;
    }

    public static float GetLastShipTimerMultiplier()
    {
        if (TryGetFloat(LastShipTimerMultiplierKey, out float value))
            return Mathf.Clamp(value, 1f, 5f);

        return DefaultLastShipTimerMultiplier;
    }

    public static int GetKillRewardPercent()
    {
        return GetInt(KillRewardPercentKey, DefaultKillRewardPercent, 0, 100);
    }

    public static int GetDeathRetainPercent()
    {
        return GetInt(DeathRetainPercentKey, DefaultDeathRetainPercent, 0, 100);
    }

    public static int GetTimeUpRetainPercent()
    {
        return GetInt(TimeUpRetainPercentKey, DefaultTimeUpRetainPercent, 0, 100);
    }

    public static string GetMapSizeMode()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MapSizeKey, out object value) &&
            value is string mode)
        {
            switch (mode)
            {
                case "small":
                case "medium":
                case "large":
                case "very_large":
                case "super_large":
                    return mode;
            }
        }

        return DefaultMapSize;
    }

    public static int GetMapBackgroundIndex()
    {
        return GetInt(MapBackgroundKey, DefaultMapBackground, 1, 12);
    }

    public static string GetSelectedLobbyMapId()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(SelectedMapKey, out object value) &&
            value is string mapId &&
            !string.IsNullOrWhiteSpace(mapId))
        {
            return mapId;
        }

        return DefaultLobbyMapId;
    }

    public static bool AreVisualEffectsEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(VisualEffectsEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultVisualEffectsEnabled;
    }

    public static bool AreStartingVfxEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(StartingVfxEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultStartingVfxEnabled;
    }

    public static string GetEndDisasterMode()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(EndDisasterModeKey, out object value) &&
            value is string mode)
        {
            return NormalizeEndDisasterMode(mode);
        }

        return DefaultEndDisasterMode;
    }

    public static bool IsEndDisasterMeteorEnabled()
    {
        return GetEndDisasterMode() == EndDisasterMeteor;
    }

    public static string NormalizeEndDisasterMode(string mode)
    {
        string normalized = string.IsNullOrWhiteSpace(mode)
            ? DefaultEndDisasterMode
            : mode.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case EndDisasterMeteor:
                return EndDisasterMeteor;
            default:
                return EndDisasterOff;
        }
    }

    public static string GetResourceRichness()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ResourceRichnessKey, out object value) &&
            value is string richness)
        {
            return NormalizeResourceRichness(richness);
        }

        return DefaultResourceRichness;
    }

    public static string NormalizeResourceRichness(string richness)
    {
        string normalized = string.IsNullOrWhiteSpace(richness)
            ? DefaultResourceRichness
            : richness.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case ResourceRichnessVeryLow:
            case ResourceRichnessLow:
            case ResourceRichnessMedium:
            case ResourceRichnessHigh:
            case ResourceRichnessVeryHigh:
            case ResourceRichnessExtreme:
                return normalized;
            default:
                return DefaultResourceRichness;
        }
    }

    public static bool AreMovingObjectsEnabled()
    {
        return GetMovingObjectsMode() != MovingObjectsModeOff;
    }

    public static bool ShouldMovingObjectsTranslate()
    {
        return GetMovingObjectsMode() == MovingObjectsModeOn;
    }

    public static bool ShouldMovingObjectsRotate()
    {
        return GetMovingObjectsMode() != MovingObjectsModeOff;
    }

    public static string GetMovingObjectsMode()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MovingObjectsEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled ? MovingObjectsModeOn : MovingObjectsModeOff;
        }

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MovingObjectsEnabledKey, out value) &&
            value is string mode)
        {
            switch (mode)
            {
                case MovingObjectsModeOn:
                case MovingObjectsModeOff:
                case MovingObjectsModeOnlyRotate:
                    return mode;
            }
        }

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MovingObjectsEnabledKey, out value) &&
            value is int modeIndex)
        {
            switch (modeIndex)
            {
                case 0:
                    return MovingObjectsModeOff;
                case 2:
                    return MovingObjectsModeOnlyRotate;
                default:
                    return MovingObjectsModeOn;
            }
        }

        return DefaultMovingObjectsMode;
    }

    public static int GetObstacleWeightFactor()
    {
        return GetInt(ObstacleWeightFactorKey, DefaultObstacleWeightFactor, 1, MaxObstacleWeightFactor);
    }

    public static int GetTreasureWeightFactor()
    {
        return GetInt(TreasureWeightFactorKey, DefaultTreasureWeightFactor, 1, 12);
    }

    public static bool IsObstacleMassMax()
    {
        return GetObstacleWeightFactor() >= MaxObstacleWeightFactor;
    }

    public static bool GetEnemyEnabled(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return true;

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(definition.EnabledRoomKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        if (kind == EnemyBotKind.Drone &&
            PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(EnemyBotsEnabledKey, out object legacyValue) &&
            legacyValue is bool legacyEnabled)
        {
            return legacyEnabled;
        }

        if (kind == EnemyBotKind.Corsair &&
            PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CorsairEnabledKey, out object legacyCorsairValue) &&
            legacyCorsairValue is bool legacyCorsairEnabled)
        {
            return legacyCorsairEnabled;
        }

        return definition.DefaultEnabled;
    }

    public static int GetEnemyCount(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return 1;

        int maxCount = kind == EnemyBotKind.SpaceMine ? 30 : 5;
        return GetInt(definition.CountRoomKey, definition.DefaultCount, 1, maxCount);
    }

    public static int GetEnemyHp(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return 100;

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.HpRoomKey))
        {
            return GetInt(definition.HpRoomKey, definition.DefaultHp, 20, 200);
        }

        if (kind == EnemyBotKind.Corsair &&
            PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CorsairHpKey, out object legacyValue))
        {
            return Mathf.Clamp(ConvertToInt(legacyValue, definition.DefaultHp), 20, 200);
        }

        return Mathf.Clamp(definition.DefaultHp, 20, 200);
    }

    public static int GetEnemyShield(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return DefaultEnemyShield;

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.ShieldRoomKey))
        {
            return GetInt(definition.ShieldRoomKey, definition.DefaultShield, 0, 200);
        }

        return Mathf.Clamp(definition.DefaultShield, 0, 200);
    }

    public static int GetEnemyDamage(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return 0;

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.DamageRoomKey))
        {
            return GetInt(definition.DamageRoomKey, definition.DefaultDamage, 0, 200);
        }

        return Mathf.Clamp(definition.DefaultDamage, 0, 200);
    }

    public static float GetEnemySpeedMultiplier(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return DefaultEnemySpeedMultiplier;

        if (TryGetFloat(definition.SpeedRoomKey, out float speed))
            return ClampEnemySpeedMultiplier(speed, definition.DefaultSpeedMultiplier);

        return ClampEnemySpeedMultiplier(definition.DefaultSpeedMultiplier, DefaultEnemySpeedMultiplier);
    }

    static float ClampEnemySpeedMultiplier(float speed, float fallback)
    {
        if (speed <= 0f)
            speed = fallback > 0f ? fallback : DefaultEnemySpeedMultiplier;

        if (speed <= 0.375f)
            return 0.25f;

        if (speed <= 0.75f)
            return 0.5f;

        if (speed <= 1.25f)
            return 1f;

        if (speed <= 1.75f)
            return 1.5f;

        return 2f;
    }

    public static int GetEnemySpawnSecond(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return 0;

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.SpawnSecondRoomKey))
        {
            return GetInt(definition.SpawnSecondRoomKey, definition.DefaultSpawnSecond, 0, 120);
        }

        if (kind == EnemyBotKind.Corsair &&
            PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CorsairSpawnSecondKey, out object legacyValue))
        {
            return Mathf.Clamp(ConvertToInt(legacyValue, definition.DefaultSpawnSecond), 0, 120);
        }

        return Mathf.Clamp(definition.DefaultSpawnSecond, 0, 120);
    }

    public static bool GetEnemyRespawnEnabled(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return DefaultEnemyRespawnEnabled;

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(definition.RespawnEnabledRoomKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultEnemyRespawnEnabled;
    }

    public static int GetEnemyRespawnIntervalSeconds(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return DefaultEnemyRespawnIntervalSeconds;

        return GetInt(definition.RespawnIntervalRoomKey, DefaultEnemyRespawnIntervalSeconds, 15, 150);
    }

    public static bool AreEnemyBotsEnabled()
    {
        return GetEnemyEnabled(EnemyBotKind.Drone);
    }

    public static bool AreCorsairsEnabled()
    {
        return GetEnemyEnabled(EnemyBotKind.Corsair);
    }

    public static int GetCorsairSpawnSecond()
    {
        return GetEnemySpawnSecond(EnemyBotKind.Corsair);
    }

    public static int GetCorsairHp()
    {
        return GetEnemyHp(EnemyBotKind.Corsair);
    }

    public static int GetBulletPushMultiplier()
    {
        return GetInt(BulletPushMultiplierKey, DefaultBulletPushMultiplier, 1, 5);
    }

    public static int GetBatteringDamage()
    {
        return GetInt(BatteringDamageKey, DefaultBatteringDamage, 0, 50);
    }

    public static string GetMassLabel(int mass)
    {
        if (mass >= MaxObstacleWeightFactor)
            return "MAX";

        if (mass <= 2)
            return "LIGHT";

        if (mass >= 12)
            return "HEAVY";

        return "MEDIUM";
    }

    public static Vector2 GetMapDimensions()
    {
        switch (GetMapSizeMode())
        {
            case "small":
                return new Vector2(20f, 20f);
            case "large":
                return new Vector2(32f, 32f);
            case "very_large":
                return new Vector2(40f, 40f);
            case "super_large":
                return new Vector2(50f, 50f);
            default:
                return new Vector2(25f, 25f);
        }
    }

    public static float GetMapAreaMultiplier()
    {
        Vector2 size = GetMapDimensions();
        const float baseArea = 25f * 25f;
        float area = size.x * size.y;
        return Mathf.Max(0.5f, area / baseArea);
    }

    public static int GetPlayerScore(Photon.Realtime.Player player)
    {
        if (player != null &&
            player.CustomProperties.TryGetValue(ScoreKey, out object value))
        {
            return ConvertToInt(value, 0);
        }

        return 0;
    }

    public static int GetPlayerRoundXp(Photon.Realtime.Player player)
    {
        return GetPlayerScore(player);
    }

    public static int GetPlayerShipSkin(Photon.Realtime.Player player, int fallback)
    {
        if (player != null &&
            player.CustomProperties.TryGetValue(ShipSkinKey, out object value))
        {
            return Mathf.Clamp(ConvertToInt(value, fallback), 0, ShipCatalog.MaxShipSkinIndex);
        }

        return Mathf.Clamp(fallback, 0, ShipCatalog.MaxShipSkinIndex);
    }

    static int GetInt(string key, int defaultValue, int min, int max)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value))
        {
            return Mathf.Clamp(ConvertToInt(value, defaultValue), min, max);
        }

        return defaultValue;
    }

    static bool TryGetFloat(string key, out float result)
    {
        result = DefaultRoundDuration;

        if (PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value))
        {
            return false;
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

        if (value is double doubleValue)
        {
            result = (float)doubleValue;
            return true;
        }

        return false;
    }

    static int ConvertToInt(object value, int fallback)
    {
        if (value is int intValue)
            return intValue;

        if (value is float floatValue)
            return Mathf.RoundToInt(floatValue);

        if (value is double doubleValue)
            return Mathf.RoundToInt((float)doubleValue);

        if (value is byte byteValue)
            return byteValue;

        return fallback;
    }
}

public enum ShipType
{
    Explorer = 0,
    Viper = 1,
    Avenger = 2,
    Arrow = 3
}

public sealed class PlayerShipDefinition
{
    public ShipType Type { get; }
    public string DisplayName { get; }
    public int[] SkinIndices { get; }
    public int CargoCapacity { get; }
    public int MainGunSlots { get; }
    public int ShieldSlots { get; }
    public int EngineSlots { get; }
    public int GadgetSlots { get; }
    public int BaseHp { get; }
    public int BaseShield { get; }
    public float BaseSpeed { get; }
    public float TurnRateMultiplier { get; }
    public float BoosterDuration { get; }
    public Vector2[] ThrusterOffsetFactors { get; }

    public PlayerShipDefinition(
        ShipType type,
        string displayName,
        int[] skinIndices,
        int cargoCapacity,
        int mainGunSlots,
        int shieldSlots,
        int engineSlots,
        int gadgetSlots,
        int baseHp,
        int baseShield,
        float baseSpeed,
        float turnRateMultiplier,
        float boosterDuration,
        Vector2[] thrusterOffsetFactors)
    {
        Type = type;
        DisplayName = displayName;
        SkinIndices = skinIndices;
        CargoCapacity = cargoCapacity;
        MainGunSlots = mainGunSlots;
        ShieldSlots = shieldSlots;
        EngineSlots = engineSlots;
        GadgetSlots = gadgetSlots;
        BaseHp = baseHp;
        BaseShield = baseShield;
        BaseSpeed = baseSpeed;
        TurnRateMultiplier = turnRateMultiplier;
        BoosterDuration = boosterDuration;
        ThrusterOffsetFactors = thrusterOffsetFactors;
    }
}

public static class ShipCatalog
{
    public const int ExplorerBasicSkinIndex = 0;
    public const int ExplorerGildedSkinIndex = 1;
    public const int ExplorerSilverSkinIndex = 2;
    public const int ViperStandardSkinIndex = 3;
    public const int ViperSnowSkinIndex = 4;
    public const int ViperNavySkinIndex = 5;
    public const int AvengerDarkGreenSkinIndex = 6;
    public const int AvengerMilitarySkinIndex = 7;
    public const int AvengerNasaSkinIndex = 8;
    public const int ArrowSmoothSkinIndex = 9;
    public const int ArrowSportySkinIndex = 10;
    public const int ArrowSharkSkinIndex = 11;
    public const int MaxShipSkinIndex = ArrowSharkSkinIndex;

    static readonly PlayerShipDefinition ExplorerDefinition = new PlayerShipDefinition(
        ShipType.Explorer,
        "Explorer",
        new[] { ExplorerBasicSkinIndex, ExplorerSilverSkinIndex, ExplorerGildedSkinIndex },
        8,
        1,
        1,
        1,
        1,
        50,
        50,
        5f,
        1f,
        5f,
        new[] { new Vector2(0f, 0.02f) });

    static readonly PlayerShipDefinition ViperDefinition = new PlayerShipDefinition(
        ShipType.Viper,
        "Viper",
        new[] { ViperStandardSkinIndex, ViperSnowSkinIndex, ViperNavySkinIndex },
        10,
        2,
        1,
        2,
        0,
        40,
        35,
        6f,
        1.2f,
        4f,
        new[]
        {
            new Vector2(-2.24f, 0.34f),
            new Vector2(2.24f, 0.34f)
        });

    static readonly PlayerShipDefinition AvengerDefinition = new PlayerShipDefinition(
        ShipType.Avenger,
        "Avenger",
        new[] { AvengerDarkGreenSkinIndex, AvengerMilitarySkinIndex, AvengerNasaSkinIndex },
        9,
        2,
        2,
        0,
        2,
        65,
        70,
        4.2f,
        0.82f,
        6f,
        new[] { new Vector2(0f, 0.02f) });

    static readonly PlayerShipDefinition ArrowDefinition = new PlayerShipDefinition(
        ShipType.Arrow,
        "Arrow",
        new[] { ArrowSmoothSkinIndex, ArrowSportySkinIndex, ArrowSharkSkinIndex },
        6,
        1,
        1,
        2,
        1,
        42,
        28,
        6.8f,
        1.45f,
        3.6f,
        new[]
        {
            new Vector2(-1.82f, 0.28f),
            new Vector2(0f, 0.2f),
            new Vector2(1.82f, 0.28f)
        });

    static readonly Dictionary<ShipType, PlayerShipDefinition> Definitions = new Dictionary<ShipType, PlayerShipDefinition>
    {
        { ShipType.Explorer, ExplorerDefinition },
        { ShipType.Viper, ViperDefinition },
        { ShipType.Avenger, AvengerDefinition },
        { ShipType.Arrow, ArrowDefinition }
    };

    public static PlayerShipDefinition GetShipDefinition(int skinIndex)
    {
        return GetShipDefinition(GetShipTypeFromSkinIndex(skinIndex));
    }

    public static PlayerShipDefinition GetShipDefinition(ShipType shipType)
    {
        return Definitions.TryGetValue(shipType, out PlayerShipDefinition definition) ? definition : ExplorerDefinition;
    }

    public static ShipType GetShipTypeFromSkinIndex(int skinIndex)
    {
        return skinIndex switch
        {
            >= ArrowSmoothSkinIndex => ShipType.Arrow,
            >= AvengerDarkGreenSkinIndex => ShipType.Avenger,
            >= ViperStandardSkinIndex => ShipType.Viper,
            _ => ShipType.Explorer
        };
    }

    public static string GetShipTypeDisplayName(ShipType shipType)
    {
        return GetShipDefinition(shipType).DisplayName;
    }

    public static int[] GetSkinsForShipType(ShipType shipType)
    {
        return GetShipDefinition(shipType).SkinIndices;
    }

    public static string GetSkinDisplayName(int skinIndex)
    {
        switch (skinIndex)
        {
            case ExplorerBasicSkinIndex: return "Basic";
            case ExplorerSilverSkinIndex: return "Silver Shine";
            case ExplorerGildedSkinIndex: return "Gilded Armour";
            case ViperStandardSkinIndex: return "Standard";
            case ViperSnowSkinIndex: return "Snow";
            case ViperNavySkinIndex: return "Navy";
            case AvengerDarkGreenSkinIndex: return "Darkgreen";
            case AvengerMilitarySkinIndex: return "Military";
            case AvengerNasaSkinIndex: return "NASA";
            case ArrowSmoothSkinIndex: return "Smooth";
            case ArrowSportySkinIndex: return "Sporty";
            case ArrowSharkSkinIndex: return "Shark";
            default: return "Skin";
        }
    }

    public static int GetShipInventoryCapacity(int skinIndex)
    {
        return GetShipDefinition(skinIndex).CargoCapacity;
    }

    public static int GetMainGunSlots(int skinIndex)
    {
        return GetShipDefinition(skinIndex).MainGunSlots;
    }

    public static int GetShieldSlots(int skinIndex)
    {
        return GetShipDefinition(skinIndex).ShieldSlots;
    }

    public static int GetEngineSlots(int skinIndex)
    {
        return GetShipDefinition(skinIndex).EngineSlots;
    }

    public static int GetGadgetSlots(int skinIndex)
    {
        return GetShipDefinition(skinIndex).GadgetSlots;
    }

    public static int GetBaseHp(int skinIndex)
    {
        return GetShipDefinition(skinIndex).BaseHp;
    }

    public static int GetBaseShield(int skinIndex)
    {
        return GetShipDefinition(skinIndex).BaseShield;
    }

    public static float GetBaseSpeed(int skinIndex)
    {
        return GetShipDefinition(skinIndex).BaseSpeed;
    }

    public static float GetTurnRateMultiplier(int skinIndex)
    {
        return GetShipDefinition(skinIndex).TurnRateMultiplier;
    }

    public static float GetBoosterDuration(int skinIndex)
    {
        return GetShipDefinition(skinIndex).BoosterDuration;
    }

    public static Vector2[] GetThrusterOffsetFactors(int skinIndex)
    {
        return GetShipDefinition(skinIndex).ThrusterOffsetFactors;
    }

    public static bool IsEquipmentSlotEnabled(int slotIndex, int shipSkinIndex)
    {
        return slotIndex switch
        {
            0 => GetMainGunSlots(shipSkinIndex) >= 1,
            1 => GetMainGunSlots(shipSkinIndex) >= 2,
            2 => GetShieldSlots(shipSkinIndex) >= 1,
            3 => GetShieldSlots(shipSkinIndex) >= 2,
            4 => GetEngineSlots(shipSkinIndex) >= 1,
            5 => GetEngineSlots(shipSkinIndex) >= 2,
            6 => GetGadgetSlots(shipSkinIndex) >= 1,
            7 => GetGadgetSlots(shipSkinIndex) >= 2,
            _ => false
        };
    }

    public static string GetEquipmentSlotLabel(int slotIndex)
    {
        return slotIndex switch
        {
            0 => "MAIN GUN",
            1 => "MAIN GUN",
            2 => "SHIELD",
            3 => "SHIELD",
            4 => "ENGINE",
            5 => "ENGINE",
            6 => "GADGET",
            7 => "GADGET",
            _ => "SLOT"
        };
    }

    public static string GetShipSkinResourcePath(int skinIndex)
    {
        return skinIndex switch
        {
            ExplorerBasicSkinIndex => "Visuals/Ships/ship1_resource",
            ExplorerGildedSkinIndex => "Visuals/Ships/ship2_resource",
            ExplorerSilverSkinIndex => "Visuals/Ships/ship3_resource",
            ViperStandardSkinIndex => "ship4_resource",
            ViperSnowSkinIndex => "Visuals/Ships/viper_snow_resource",
            ViperNavySkinIndex => "Visuals/Ships/viper_navy_clean_resource",
            AvengerDarkGreenSkinIndex => "Visuals/Ships/avenger_darkgreen_resource",
            AvengerMilitarySkinIndex => "Visuals/Ships/avenger_military_resource",
            AvengerNasaSkinIndex => "Visuals/Ships/avenger_nasa_resource",
            ArrowSmoothSkinIndex => "Visuals/Ships/arrow_skin_smooth_resource",
            ArrowSportySkinIndex => "Visuals/Ships/arrow_skin_sporty_resource",
            ArrowSharkSkinIndex => "Visuals/Ships/arrow_skin_shark_resource",
            _ => "Visuals/Ships/ship1_resource"
        };
    }

    public static string GetShipSkinEditorResourcePath(int skinIndex)
    {
        return skinIndex switch
        {
            ExplorerBasicSkinIndex => "Assets/Resources/Visuals/Ships/ship1_resource.png",
            ExplorerGildedSkinIndex => "Assets/Resources/Visuals/Ships/ship2_resource.png",
            ExplorerSilverSkinIndex => "Assets/Resources/Visuals/Ships/ship3_resource.png",
            ViperStandardSkinIndex => "Assets/Resources/ship4_resource.png",
            ViperSnowSkinIndex => "Assets/Resources/Visuals/Ships/viper_snow_resource.png",
            ViperNavySkinIndex => "Assets/Resources/Visuals/Ships/viper_navy_clean_resource.png",
            AvengerDarkGreenSkinIndex => "Assets/Resources/Visuals/Ships/avenger_darkgreen_resource.png",
            AvengerMilitarySkinIndex => "Assets/Resources/Visuals/Ships/avenger_military_resource.png",
            AvengerNasaSkinIndex => "Assets/Resources/Visuals/Ships/avenger_nasa_resource.png",
            ArrowSmoothSkinIndex => "Assets/Resources/Visuals/Ships/arrow_skin_smooth_resource.png",
            ArrowSportySkinIndex => "Assets/Resources/Visuals/Ships/arrow_skin_sporty_resource.png",
            ArrowSharkSkinIndex => "Assets/Resources/Visuals/Ships/arrow_skin_shark_resource.png",
            _ => "Assets/Resources/Visuals/Ships/ship1_resource.png"
        };
    }

    public static string GetShipSkinEditorFallbackPath(int skinIndex)
    {
        return skinIndex switch
        {
            ExplorerBasicSkinIndex => "Assets/ship1.png",
            ExplorerGildedSkinIndex => "Assets/ship2.png",
            ExplorerSilverSkinIndex => "Assets/ship3.png",
            ViperStandardSkinIndex => "Assets/ship4.png",
            ViperSnowSkinIndex => "Assets/Viper_skin_white.png",
            ViperNavySkinIndex => "Assets/Viper_skin_navy_clean_v2.png",
            AvengerDarkGreenSkinIndex => "Assets/Avenger_skin_darkgreen.png",
            AvengerMilitarySkinIndex => "Assets/Avenger_skin_military.png",
            AvengerNasaSkinIndex => "Assets/Avenger_skin_nasa.png",
            ArrowSmoothSkinIndex => "Assets/arrow_skin_smooth.png",
            ArrowSportySkinIndex => "Assets/arrow_skin_sporty.png",
            ArrowSharkSkinIndex => "Assets/arrow_skin_shark.png",
            _ => "Assets/ship1.png"
        };
    }

    public static string GetWreckResourcePathForSkin(int skinIndex)
    {
        return GetShipTypeFromSkinIndex(skinIndex) switch
        {
            ShipType.Viper => "wrak2_resource",
            ShipType.Avenger => "wrak3_resource",
            ShipType.Arrow => "Visuals/Ships/arrow_ship_wreck_resource",
            _ => "wrak1_resource"
        };
    }

    public static string GetWreckEditorResourcePathForSkin(int skinIndex)
    {
        return GetShipTypeFromSkinIndex(skinIndex) switch
        {
            ShipType.Viper => "Assets/Resources/wrak2_resource.png",
            ShipType.Avenger => "Assets/Resources/wrak3_resource.png",
            ShipType.Arrow => "Assets/Resources/Visuals/Ships/arrow_ship_wreck_resource.png",
            _ => "Assets/Resources/wrak1_resource.png"
        };
    }

    public static string GetWreckEditorFallbackPathForSkin(int skinIndex)
    {
        return GetShipTypeFromSkinIndex(skinIndex) switch
        {
            ShipType.Viper => "Assets/wrak2.png",
            ShipType.Avenger => "Assets/wrak3.png",
            ShipType.Arrow => "Assets/arrow_ship_wreck.png",
            _ => "Assets/wrak1.png"
        };
    }
}
