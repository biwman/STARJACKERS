using Photon.Pun;
using UnityEngine;

public static class RoomSettings
{
    public const string RoundDurationKey = "roundDuration";
    public const string ObstacleDensityKey = "obstacleDensity";
    public const string TreasureDensityKey = "treasureDensity";
    public const string NebulaDensityKey = "nebulaDensity";
    public const string ExtractionCountKey = "extractionCount";
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
    public const string MovingObjectsEnabledKey = "movingObjectsEnabled";
    public const string EnemyBotsEnabledKey = "enemyBotsEnabled";
    public const string CorsairEnabledKey = "corsairEnabled";
    public const string CorsairSpawnSecondKey = "corsairSpawnSecond";
    public const string CorsairHpKey = "corsairHp";
    public const string BulletPushMultiplierKey = "bulletPushMultiplier";
    public const string ObstacleWeightFactorKey = "obstacleWeightFactor";
    public const string TreasureWeightFactorKey = "treasureWeightFactor";
    public const string RoundResultsKey = "roundResultsSnapshot";
    public const string RoundEndReasonKey = "roundEndReason";
    public const string ShipSkinKey = "shipSkinIndex";
    public const string ShipInventoryStateKey = "shipInventoryState";
    public const string EquipmentStateKey = "equipmentState";
    public const string ScoreKey = "score";

    public const float DefaultRoundDuration = 180f;
    public const string DefaultObstacleDensity = "high";
    public const int DefaultExtractionCount = 3;
    public const int DefaultBoosterSlowdownPercent = 30;
    public const int DefaultAmmoCount = 15;
    public const int DefaultBoosterRecoveryDelay = 5;
    public const int DefaultMaxInputBoostPercent = 20;
    public const bool DefaultShipDriftEnabled = true;
    public const int DefaultLastShipTimerMultiplier = 3;
    public const int DefaultKillRewardPercent = 50;
    public const int DefaultDeathRetainPercent = 25;
    public const int DefaultTimeUpRetainPercent = 25;
    public const string DefaultMapSize = "medium";
    public const int DefaultMapBackground = 5;
    public const bool DefaultMovingObjectsEnabled = true;
    public const bool DefaultEnemyBotsEnabled = true;
    public const bool DefaultCorsairEnabled = true;
    public const int DefaultCorsairSpawnSecond = 0;
    public const int DefaultCorsairHp = 200;
    public const int DefaultBulletPushMultiplier = 1;
    public const int DefaultObstacleWeightFactor = 6;
    public const int DefaultTreasureWeightFactor = 6;
    public const bool DefaultEnemyRespawnEnabled = false;
    public const int DefaultEnemyRespawnIntervalSeconds = 60;

    public static float GetRoundDuration()
    {
        if (TryGetFloat(RoundDurationKey, out float value))
            return value;

        return DefaultRoundDuration;
    }

    public static int GetExtractionCount()
    {
        return GetInt(ExtractionCountKey, DefaultExtractionCount, 1, 4);
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

    public static bool IsShipDriftEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ShipDriftEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultShipDriftEnabled;
    }

    public static int GetLastShipTimerMultiplier()
    {
        return GetInt(LastShipTimerMultiplierKey, DefaultLastShipTimerMultiplier, 1, 5);
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
        return GetInt(MapBackgroundKey, DefaultMapBackground, 1, 6);
    }

    public static bool AreMovingObjectsEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MovingObjectsEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultMovingObjectsEnabled;
    }

    public static int GetObstacleWeightFactor()
    {
        return GetInt(ObstacleWeightFactorKey, DefaultObstacleWeightFactor, 1, 12);
    }

    public static int GetTreasureWeightFactor()
    {
        return GetInt(TreasureWeightFactorKey, DefaultTreasureWeightFactor, 1, 12);
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

        return GetInt(definition.CountRoomKey, definition.DefaultCount, 1, 5);
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

    public static string GetMassLabel(int mass)
    {
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
            return Mathf.Clamp(ConvertToInt(value, fallback), 0, 3);
        }

        return Mathf.Clamp(fallback, 0, 3);
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
    Viper = 1
}

public static class ShipCatalog
{
    public static ShipType GetShipTypeFromSkinIndex(int skinIndex)
    {
        return skinIndex == 3 ? ShipType.Viper : ShipType.Explorer;
    }

    public static string GetShipTypeDisplayName(ShipType shipType)
    {
        return shipType == ShipType.Viper ? "Viper" : "Explorer";
    }

    public static int[] GetSkinsForShipType(ShipType shipType)
    {
        return shipType == ShipType.Viper
            ? new[] { 3 }
            : new[] { 0, 2, 1 };
    }

    public static string GetSkinDisplayName(int skinIndex)
    {
        switch (skinIndex)
        {
            case 0: return "Basic";
            case 2: return "Silver Shine";
            case 1: return "Gilded Armour";
            case 3: return "Standard";
            default: return "Skin";
        }
    }

    public static int GetShipInventoryCapacity(int skinIndex)
    {
        return GetShipTypeFromSkinIndex(skinIndex) == ShipType.Viper ? 10 : 8;
    }

    public static int GetMainGunSlots(int skinIndex)
    {
        return GetShipTypeFromSkinIndex(skinIndex) == ShipType.Viper ? 2 : 1;
    }

    public static int GetShieldSlots(int skinIndex)
    {
        return 1;
    }

    public static int GetEngineSlots(int skinIndex)
    {
        return GetShipTypeFromSkinIndex(skinIndex) == ShipType.Viper ? 2 : 1;
    }

    public static int GetGadgetSlots(int skinIndex)
    {
        return GetShipTypeFromSkinIndex(skinIndex) == ShipType.Viper ? 0 : 1;
    }
}
