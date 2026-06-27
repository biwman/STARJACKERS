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
    public const string RoundStarterActorNumberKey = "roundStarter.actorNumber";
    public const string RoundStarterUserIdKey = "roundStarter.userId";
    public const string RoundStarterNicknameKey = "roundStarter.nickname";
    public const string RoundEndUtcMsKey = "roundEndUtcMs";
    public const string RoundDurationKey = "roundDuration";
    public const string ObstacleDensityKey = "obstacleDensity";
    public const string ObstacleDestroyEnabledKey = "obstacleDestroyEnabled";
    public const string ObstacleHpKey = "obstacleHp";
    public const string ObstacleSizePercentKey = "obstacleSizePercent";
    public const string ObstacleNoBordersKey = "obstacleNoBorders";
    public const string TreasureDensityKey = "treasureDensity";
    public const string RadioactiveTreasureDensityKey = "radioactiveTreasureDensity";
    public const string AlienSecretsDensityKey = "alienSecretsDensity";
    public const string ResourceRichnessKey = "resourceRichness";
    public const string RandomLootWreckCountKey = "randomLootWreckCount";
    public const string HiddenTreasureEnabledKey = "hiddenTreasureEnabled";
    public const string CosmicWormEnabledKey = "cosmicWormEnabled";
    public const string CosmicWormModeKey = "mapEffect.cosmicWorm.mode";
    public const string CosmicWormStartUtcMsKey = "mapEffect.cosmicWorm.startUtcMs";
    public const string CosmicWormActiveKey = "mapEffect.cosmicWorm.active";
    public const string CrazyEnemiesModeKey = "mapEffect.crazyEnemies.mode";
    public const string CrazyEnemiesStartUtcMsKey = "mapEffect.crazyEnemies.startUtcMs";
    public const string CrazyEnemiesActiveKey = "mapEffect.crazyEnemies.active";
    public const string FogOfWarModeKey = "mapEffect.fogOfWar.mode";
    public const string FogOfWarStartUtcMsKey = "mapEffect.fogOfWar.startUtcMs";
    public const string FogOfWarActiveKey = "mapEffect.fogOfWar.active";
    public const string PirateBaseModeKey = "mapEffect.pirateBase.mode";
    public const string PirateBaseStartUtcMsKey = "mapEffect.pirateBase.startUtcMs";
    public const string PirateBaseActiveKey = "mapEffect.pirateBase.active";
    public const string AsteroidShowerModeKey = "mapEffect.asteroidShower.mode";
    public const string AsteroidShowerStartUtcMsKey = "mapEffect.asteroidShower.startUtcMs";
    public const string AsteroidShowerActiveKey = "mapEffect.asteroidShower.active";
    public const string MilitaryConvoyModeKey = "mapEffect.militaryConvoy.mode";
    public const string MilitaryConvoyStartUtcMsKey = "mapEffect.militaryConvoy.startUtcMs";
    public const string MilitaryConvoyActiveKey = "mapEffect.militaryConvoy.active";
    public const string MapEffectModeDefaultsVersionKey = "mapEffectModeDefaultsVersion";
    public const int MapEffectModeDefaultsVersion = 1;
    public const string MapEffectChanceKeyPrefix = "mapEffectChance.";
    public const string MapEffectChanceDefaultsVersionKey = "mapEffectChanceDefaultsVersion";
    public const int MapEffectChanceDefaultsVersion = 4;
    public const string CrazyEnemiesRuleId = "CE";
    public const string FogOfWarRuleId = "FoW";
    public const string PirateBaseRuleId = "PB";
    public const string AsteroidShowerRuleId = "AS";
    public const string CosmicWormRuleId = "CW";
    public const string MilitaryConvoyRuleId = "MC";
    public const string SpaceJunkDensityKey = "spaceJunkDensity";
    public const string ContainersDensityKey = "containersDensity";
    public const string ArtifactAsteroidsDensityKey = "artifactAsteroidsDensity";
    public const string ArtifactAsteroidsStateKey = "artifactAsteroids.state";
    public const string NebulaDensityKey = "nebulaDensity";
    public const string FireNebulaDensityKey = "fireNebulaDensity";
    public const string ToxicNebulaDensityKey = "toxicNebulaDensity";
    public const string NebulaSizeKey = "nebulaSize";
    public const string FireNebulaSizeKey = "fireNebulaSize";
    public const string ToxicNebulaSizeKey = "toxicNebulaSize";
    public const string AdvancedNebulaEnabledKey = "advancedNebulaEnabled";
    public const string CloudsDensityKey = "cloudsDensity";
    public const string CloudsSizeKey = "cloudsSize";
    public const string ExtractionCountKey = "extractionCount";
    public const string ExtractionTypeKey = "extractionType";
    public const string RepairBayCountKey = "repairBayCount";
    public const string SpaceFactoryCountKey = "spaceFactoryCount";
    public const string ScienceStationCountKey = "scienceStationCount";
    public const string AvengerPlotEnabledKey = "avengerPlotEnabled";
    public const string ViperPlotChancePercentKey = "viperPlotChancePercent";
    public const string ArrowPlotChancePercentKey = "arrowPlotChancePercent";
    public const string BisonPlotChancePercentKey = "bisonPlotChancePercent";
    public const string InvaderPlotChancePercentKey = "invaderPlotChancePercent";
    public const string ShipUnlockPlotActiveKey = "shipUnlockPlot.active";
    public const string ShipUnlockPlotStartTimeKey = "shipUnlockPlot.startTime";
    public const string BoosterSlowdownKey = "boosterSlowdownPercent";
    public const string BoosterRecoveryDelayKey = "boosterRecoveryDelay";
    public const string AdvancedBoosterEnabledKey = "advancedBoosterEnabled";
    public const string ShipDriftEnabledKey = "shipDriftEnabled";
    public const string LastShipTimerMultiplierKey = "lastShipTimerMultiplier";
    public const string KillRewardPercentKey = "killRewardPercent";
    public const string DeathRetainPercentKey = "deathRetainPercent";
    public const string TimeUpRetainPercentKey = "timeUpRetainPercent";
    public const string InventoryLossEnabledKey = "inventoryLossEnabled";
    public const string EquipmentLossEnabledKey = "equipmentLossEnabled";
    public const string MapSizeKey = "mapSize";
    public const string ToxicBordersEnabledKey = "toxicBordersEnabled";
    public const string MapBackgroundKey = "mapBackground";
    public const string SelectedMapKey = "selectedMap";
    public const string VisualEffectsEnabledKey = "visualEffectsEnabled";
    public const string AdvancedSpawnVfxEnabledKey = "advancedSpawnVfxEnabled";
    public const string LowHpHullSparksEnabledKey = "lowHpHullSparksEnabled";
    public const string BoomVfxEnabledKey = "boomVfxEnabled";
    public const string DynamicCameraZoomEnabledKey = "dynamicCameraZoomEnabled";
    public const string ParallaxBackgroundKey = "parallaxBackground";
    public const string BackgroundObjectKey = "backgroundObject";
    public const string GravityWellPhysicsEnabledKey = "gravityWellPhysicsEnabled";
    public const string EndDisasterModeKey = "endDisasterMode";
    public const string EndDisasterWarningSecondsKey = "endDisasterWarningSeconds";
    public const string CollectKeepAliveRangeBonusPercentKey = "collectKeepAliveRangeBonusPercent";
    public const string HapticsEnabledKey = "hapticsEnabled";
    public const string FpsCounterEnabledKey = "fpsCounterEnabled";
    public const string DiagnosticsGcEnabledKey = "diagnostics.gcEnabled";
    public const string DiagnosticsSceneCountsEnabledKey = "diagnostics.sceneCountsEnabled";
    public const string DiagnosticsNetworkEnabledKey = "diagnostics.networkEnabled";
    public const string NeutralRidersEnabledKey = "neutralRiders.enabled";
    public const string NeutralRidersCountKey = "neutralRiders.count";
    public const string NeutralRidersAggressionKey = "neutralRiders.aggression";
    public const string EnemyDamageMultiplierPercentKey = "enemyDamageMultiplierPercent";
    public const string EnemyAttackWindupMultiplierPercentKey = "enemyAttackWindupMultiplierPercent";
    public const string EnemyAttackCooldownMultiplierPercentKey = "enemyAttackCooldownMultiplierPercent";
    public const string MovingObjectsEnabledKey = "movingObjectsEnabled";
    public const string EnemyBotsEnabledKey = "enemyBotsEnabled";
    public const string CorsairEnabledKey = "corsairEnabled";
    public const string CorsairSpawnSecondKey = "corsairSpawnSecond";
    public const string CorsairHpKey = "corsairHp";
    public const string BulletPushMultiplierKey = "bulletPushMultiplier";
    public const string ObstacleWeightFactorKey = "obstacleWeightFactor";
    public const string TreasureWeightFactorKey = "treasureWeightFactor";
    public const string BatteringDamageKey = "batteringDamage";
    public const string GunSetupKeyPrefix = "gunSetup.";
    public const string RoundResultsKey = "roundResultsSnapshot";
    public const string FinishedRoundResultsKey = "finishedRoundResults";
    public const string RoundEndReasonKey = "roundEndReason";
    public const string RoundWarmupTokenKey = "roundWarmup.token";
    public const string RoundWarmupStartedAtKey = "roundWarmup.startedAt";
    public const string RoundWarmupReadyPlayerKey = "roundWarmup.readyToken";
    public const string ShipSkinKey = "shipSkinIndex";
    public const string PilotIdKey = "pilotId";
    public const string ShipInventoryStateKey = "shipInventoryState";
    public const string EquipmentStateKey = "equipmentState";
    public const string GadgetChargesStateKey = "gadgetChargesState";
    public const string RepairBayOccupancyStateKey = "repairBayOccupancyState";
    public const string SpaceFactoryStateKey = "spaceFactoryState";
    public const string SpaceFactoryOccupancyStateKey = "spaceFactoryOccupancyState";
    public const string ScienceStationOccupancyStateKey = "scienceStationOccupancyState";
    public const string ScoreKey = "score";

    public const float DefaultRoundDuration = 240f;
    public const string DefaultObstacleDensity = "high";
    public const string TreasureDensityNone = "none";
    public const string TreasureDensityVeryLow = "very_low";
    public const string TreasureDensityLow = "low";
    public const string TreasureDensityMedium = "medium";
    public const string TreasureDensityHigh = "high";
    public const string DefaultTreasureDensity = TreasureDensityMedium;
    public const string RadioactiveTreasureDensityOff = "off";
    public const string RadioactiveTreasureDensityLow = "low";
    public const string RadioactiveTreasureDensityMedium = "medium";
    public const string RadioactiveTreasureDensityHigh = "high";
    public const string DefaultRadioactiveTreasureDensity = RadioactiveTreasureDensityOff;
    public const string AlienSecretsDensityNone = "none";
    public const string AlienSecretsDensityVeryLow = "very_low";
    public const string AlienSecretsDensityLow = "low";
    public const string AlienSecretsDensityMedium = "medium";
    public const string AlienSecretsDensityHigh = "high";
    public const string AlienSecretsDensityVeryHigh = "very_high";
    public const string DefaultAlienSecretsDensity = AlienSecretsDensityNone;
    public const bool DefaultObstacleDestroyEnabled = true;
    public const int DefaultObstacleHp = 400;
    public const int DefaultObstacleSizePercent = 100;
    public const bool DefaultObstacleNoBorders = false;
    public const int DefaultExtractionCount = 3;
    public const string ExtractionTypePortal = "portal";
    public const string ExtractionTypeCarrier = "carrier";
    public const string ExtractionTypeSpaceCity = "space_city";
    public const string ExtractionTypeAncientPortal = "ancient_portal";
    public const string DefaultExtractionType = ExtractionTypePortal;
    public const int DefaultRepairBayCount = 1;
    public const int DefaultSpaceFactoryCount = 0;
    public const int DefaultScienceStationCount = 0;
    public const bool DefaultAvengerPlotEnabled = false;
    public const int DefaultViperPlotChancePercent = 20;
    public const int DefaultArrowPlotChancePercent = 0;
    public const int DefaultBisonPlotChancePercent = 20;
    public const int DefaultInvaderPlotChancePercent = 0;
    public const int DefaultRandomLootWreckCount = 0;
    public const bool DefaultHiddenTreasureEnabled = false;
    public const bool DefaultCosmicWormEnabled = false;
    public const int DefaultBoosterSlowdownPercent = 40;
    public const int DefaultAmmoCount = 15;
    public const int DefaultBoosterRecoveryDelay = 5;
    public const bool DefaultAdvancedBoosterEnabled = true;
    public const int DefaultShipDriftLevel = 1;
    public const float DefaultLastShipTimerMultiplier = 1f;
    public const int DefaultKillRewardPercent = 50;
    public const int DefaultDeathRetainPercent = 25;
    public const int DefaultTimeUpRetainPercent = 25;
    public const bool DefaultInventoryLossEnabled = true;
    public const bool DefaultEquipmentLossEnabled = false;
    public const string DefaultMapSize = "medium";
    public const bool DefaultToxicBordersEnabled = true;
    public const float MapDimensionsScale = 1.2f;
    public const float ToxicBordersMapScale = 1.2f;
    public const float ToxicBordersEnemyAvoidanceBuffer = 3.25f;
    public const float ToxicBordersEnemyHardAvoidanceBuffer = 0.85f;
    public const int DefaultMapBackground = 5;
    public const int MaxMapBackground = 21;
    public const string DefaultLobbyMapId = "just_space";
    public const bool DefaultVisualEffectsEnabled = true;
    public const bool DefaultAdvancedSpawnVfxEnabled = false;
    public const bool DefaultLowHpHullSparksEnabled = true;
    public const bool DefaultBoomVfxEnabled = true;
    public const bool DefaultDynamicCameraZoomEnabled = true;
    public const string ParallaxBackgroundKosmos3 = "kosmos3";
    public const string ParallaxBackgroundKosmos6 = "kosmos6";
    public const string ParallaxBackgroundKosmos8 = "kosmos8";
    public const string ParallaxBackgroundKosmos9 = "kosmos9";
    public const string ParallaxBackgroundKosmos10 = "kosmos10";
    public const string ParallaxBackgroundKosmos11 = "kosmos11";
    public const string ParallaxBackgroundKosmos12 = "kosmos12";
    public const string ParallaxBackgroundKosmos13 = "kosmos13";
    public const string ParallaxBackgroundKosmos14 = "kosmos14";
    public const string ParallaxBackgroundKosmos15 = "kosmos15";
    public const string ParallaxBackgroundKosmos16 = "kosmos16";
    public const string DefaultParallaxBackground = ParallaxBackgroundKosmos9;
    public const string BackgroundObjectOff = "off";
    public const string BackgroundObject1 = "background_object1";
    public const string BackgroundObject2 = "background_object2";
    public const string BackgroundObject3 = "background_object3";
    public const string BackgroundObject4 = "background_object4";
    public const string BackgroundObject5 = "background_object5";
    public const string BackgroundObject6 = "background_object6";
    public const string BackgroundObject7 = "background_object7";
    public const string BackgroundObject8 = "background_object8";
    public const string BackgroundObject9 = "background_object9";
    public const string BackgroundObject10 = "background_object10";
    public const string BackgroundObject11 = "background_object11";
    public const string BackgroundObject12 = "background_object12";
    public const string BackgroundObject13 = "background_object13";
    public const string BackgroundObject14 = "background_object14";
    public const string DefaultBackgroundObject = BackgroundObjectOff;
    public const bool DefaultGravityWellPhysicsEnabled = false;
    public const int DefaultCollectKeepAliveRangeBonusPercent = 50;
    public const bool DefaultHapticsEnabled = true;
    public const bool DefaultFpsCounterEnabled = false;
    public const bool DefaultDiagnosticsGcEnabled = false;
    public const bool DefaultDiagnosticsSceneCountsEnabled = false;
    public const bool DefaultDiagnosticsNetworkEnabled = false;
    public const bool DefaultNeutralRidersEnabled = false;
    public const int DefaultNeutralRidersCount = 2;
    public const string NeutralRiderAggressionLow = "low";
    public const string NeutralRiderAggressionNormal = "normal";
    public const string NeutralRiderAggressionHigh = "high";
    public const string DefaultNeutralRiderAggression = NeutralRiderAggressionNormal;
    public const string EndDisasterOff = "off";
    public const string EndDisasterMeteor = "meteor";
    public const string DefaultEndDisasterMode = EndDisasterMeteor;
    public const int DefaultEndDisasterWarningSeconds = 30;
    public const int MinEndDisasterWarningSeconds = 10;
    public const int MaxEndDisasterWarningSeconds = 40;
    public const bool DefaultMovingObjectsEnabled = true;
    public const bool DefaultEnemyBotsEnabled = true;
    public const bool DefaultCorsairEnabled = true;
    public const int DefaultCorsairSpawnSecond = 0;
    public const int DefaultCorsairHp = 200;
    public const int DefaultBulletPushMultiplier = 1;
    public const int DefaultObstacleWeightFactor = 12;
    public const int DefaultTreasureWeightFactor = 6;
    public const int DefaultBatteringDamage = 20;
    public const int MaxObstacleWeightFactor = 999;
    public const bool DefaultEnemyRespawnEnabled = false;
    public const int DefaultEnemyRespawnIntervalSeconds = 60;
    public const int DefaultEnemyShield = 20;
    public const float DefaultEnemySpeedMultiplier = 1f;
    public const int DefaultEnemyDamageMultiplierPercent = 80;
    public const int DefaultEnemyAttackWindupMultiplierPercent = 120;
    public const int DefaultEnemyAttackCooldownMultiplierPercent = 115;
    public const string SessionStateInLobby = "in_lobby";
    public const string SessionStatePreparing = "preparing";
    public const string SessionStateInPlay = "in_play";
    public const string SessionStateClosingLobby = "closing_lobby";
    public const string SessionStateSummary = "summary";
    public const string MovingObjectsModeOn = "on";
    public const string MovingObjectsModeOff = "off";
    public const string MovingObjectsModeOnlyRotate = "only_rotate";
    public const string DefaultMovingObjectsMode = MovingObjectsModeOn;
    public const string ResourceRichnessExtremelyLow = "extremely_low";
    public const string ResourceRichnessVeryLow = "very_low";
    public const string ResourceRichnessLow = "low";
    public const string ResourceRichnessMedium = "medium";
    public const string ResourceRichnessHigh = "high";
    public const string ResourceRichnessVeryHigh = "very_high";
    public const string ResourceRichnessExtreme = "extreme";
    public const string DefaultResourceRichness = ResourceRichnessMedium;
    public const string MapEffectModeOff = "off";
    public const string MapEffectModeAlwaysOn = "always_on";
    public const string MapEffectModeUtcStart = "utc_start";
    public const string DefaultMapEffectMode = MapEffectModeAlwaysOn;
    public const int DefaultMapEffectChancePercent = 5;
    public const double MapEffectActivationWindowMs = 2d * 60d * 60d * 1000d;
    public const float CrazyEnemiesVisualScaleMultiplier = 1.3f;
    public const float CrazyEnemiesSpawnFrequencyMultiplier = 1.2f;
    public const string SpaceJunkDensityNone = "none";
    public const string SpaceJunkDensityLow = "low";
    public const string SpaceJunkDensityMedium = "medium";
    public const string SpaceJunkDensityHigh = "high";
    public const string DefaultSpaceJunkDensity = SpaceJunkDensityLow;
    public const string ContainersDensityNone = "none";
    public const string ContainersDensityVeryLow = "very_low";
    public const string ContainersDensityLow = "low";
    public const string ContainersDensityMedium = "medium";
    public const string ContainersDensityHigh = "high";
    public const string ContainersDensityVeryHigh = "very_high";
    public const string DefaultContainersDensity = ContainersDensityNone;
    public const string ArtifactAsteroidsDensityOff = "off";
    public const string ArtifactAsteroidsDensityVeryLow = "very_low";
    public const string ArtifactAsteroidsDensityLow = "low";
    public const string ArtifactAsteroidsDensityMedium = "medium";
    public const string ArtifactAsteroidsDensityHigh = "high";
    public const string DefaultArtifactAsteroidsDensity = ArtifactAsteroidsDensityOff;
    public const string DefaultFireNebulaDensity = SpaceJunkDensityNone;
    public const string DefaultToxicNebulaDensity = SpaceJunkDensityNone;
    public const string NebulaSizeVerySmall = "very_small";
    public const string NebulaSizeSmall = "small";
    public const string NebulaSizeNormal = "normal";
    public const string NebulaSizeBig = "big";
    public const string NebulaSizeVeryBig = "very_big";
    public const string DefaultNebulaSize = NebulaSizeNormal;
    public const string DefaultFireNebulaSize = NebulaSizeNormal;
    public const string DefaultToxicNebulaSize = NebulaSizeNormal;
    public const bool DefaultAdvancedNebulaEnabled = false;
    public const string DefaultCloudsDensity = SpaceJunkDensityNone;
    public const string DefaultCloudsSize = NebulaSizeNormal;

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

    public static int GetRoundStarterActorNumber()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoundStarterActorNumberKey, out object value))
        {
            return Mathf.Max(0, ConvertToInt(value, 0));
        }

        return 0;
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
        return GetInt(ObstacleHpKey, DefaultObstacleHp, 50, 500);
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

    public static string GetExtractionType()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ExtractionTypeKey, out object value) &&
            value is string extractionType)
        {
            return NormalizeExtractionType(extractionType);
        }

        return DefaultExtractionType;
    }

    public static string NormalizeExtractionType(string extractionType)
    {
        string normalized = string.IsNullOrWhiteSpace(extractionType)
            ? DefaultExtractionType
            : extractionType.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case ExtractionTypeCarrier:
                return ExtractionTypeCarrier;
            case ExtractionTypeSpaceCity:
                return ExtractionTypeSpaceCity;
            case ExtractionTypeAncientPortal:
                return ExtractionTypeAncientPortal;
            default:
                return ExtractionTypePortal;
        }
    }

    public static int GetRepairBayCount()
    {
        return GetInt(RepairBayCountKey, DefaultRepairBayCount, 0, 2);
    }

    public static int GetSpaceFactoryCount()
    {
        return GetInt(SpaceFactoryCountKey, DefaultSpaceFactoryCount, 0, 2);
    }

    public static int GetScienceStationCount()
    {
        return GetInt(ScienceStationCountKey, DefaultScienceStationCount, 0, 1);
    }

    public static int GetRandomLootWreckCount()
    {
        return GetInt(RandomLootWreckCountKey, DefaultRandomLootWreckCount, 0, 5);
    }

    public static bool IsHiddenTreasureEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(HiddenTreasureEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultHiddenTreasureEnabled;
    }

    public static bool IsCosmicWormEnabled()
    {
        if (IsCosmicWormActive())
            return true;

        if (PhotonNetwork.CurrentRoom != null &&
            !PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(CosmicWormActiveKey) &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CosmicWormEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultCosmicWormEnabled;
    }

    public static int GetBoosterSlowdownPercent()
    {
        return GetInt(BoosterSlowdownKey, DefaultBoosterSlowdownPercent, 30, 100);
    }

    public static int GetAmmoCount()
    {
        return DefaultAmmoCount;
    }

    public static int GetBoosterRecoveryDelay()
    {
        return GetInt(BoosterRecoveryDelayKey, DefaultBoosterRecoveryDelay, 0, 10);
    }

    public static bool IsAdvancedBoosterEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(AdvancedBoosterEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultAdvancedBoosterEnabled;
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

    public static bool IsInventoryLossEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(InventoryLossEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultInventoryLossEnabled;
    }

    public static bool IsEquipmentLossEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(EquipmentLossEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultEquipmentLossEnabled;
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

    public static bool AreToxicBordersEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ToxicBordersEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultToxicBordersEnabled;
    }

    public static int GetMapBackgroundIndex()
    {
        return GetInt(MapBackgroundKey, DefaultMapBackground, 1, MaxMapBackground);
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

    public static bool IsAdvancedSpawnVfxEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(AdvancedSpawnVfxEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultAdvancedSpawnVfxEnabled;
    }

    public static bool AreLowHpHullSparksEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(LowHpHullSparksEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultLowHpHullSparksEnabled;
    }

    public static bool AreBoomVfxEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(BoomVfxEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultBoomVfxEnabled;
    }

    public static bool IsDynamicCameraZoomEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DynamicCameraZoomEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultDynamicCameraZoomEnabled;
    }

    public static string GetParallaxBackgroundId()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ParallaxBackgroundKey, out object value) &&
            value is string backgroundId)
        {
            return NormalizeParallaxBackgroundId(backgroundId);
        }

        return DefaultParallaxBackground;
    }

    public static string NormalizeParallaxBackgroundId(string backgroundId)
    {
        switch (backgroundId)
        {
            case ParallaxBackgroundKosmos3:
            case ParallaxBackgroundKosmos6:
            case ParallaxBackgroundKosmos8:
            case ParallaxBackgroundKosmos9:
            case ParallaxBackgroundKosmos10:
            case ParallaxBackgroundKosmos11:
            case ParallaxBackgroundKosmos12:
            case ParallaxBackgroundKosmos13:
            case ParallaxBackgroundKosmos14:
            case ParallaxBackgroundKosmos15:
            case ParallaxBackgroundKosmos16:
                return backgroundId;
            default:
                return DefaultParallaxBackground;
        }
    }

    public static string GetBackgroundObjectId()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(BackgroundObjectKey, out object value) &&
            value is string objectId)
        {
            return NormalizeBackgroundObjectId(objectId);
        }

        return DefaultBackgroundObject;
    }

    public static string NormalizeBackgroundObjectId(string objectId)
    {
        switch (objectId)
        {
            case BackgroundObject1:
            case BackgroundObject2:
            case BackgroundObject3:
            case BackgroundObject4:
            case BackgroundObject5:
            case BackgroundObject6:
            case BackgroundObject7:
            case BackgroundObject8:
            case BackgroundObject9:
            case BackgroundObject10:
            case BackgroundObject11:
            case BackgroundObject12:
            case BackgroundObject13:
            case BackgroundObject14:
                return objectId;
            default:
                return DefaultBackgroundObject;
        }
    }

    public static bool IsGravityWellPhysicsEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(GravityWellPhysicsEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultGravityWellPhysicsEnabled;
    }

    public static bool IsAvengerPlotEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(AvengerPlotEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultAvengerPlotEnabled;
    }

    public static int GetViperPlotChancePercent()
    {
        return GetInt(ViperPlotChancePercentKey, DefaultViperPlotChancePercent, 0, 100);
    }

    public static int GetArrowPlotChancePercent()
    {
        return GetInt(ArrowPlotChancePercentKey, DefaultArrowPlotChancePercent, 0, 100);
    }

    public static int GetBisonPlotChancePercent()
    {
        return GetInt(BisonPlotChancePercentKey, DefaultBisonPlotChancePercent, 0, 100);
    }

    public static int GetInvaderPlotChancePercent()
    {
        return GetInt(InvaderPlotChancePercentKey, DefaultInvaderPlotChancePercent, 0, 100);
    }

    public static int GetCollectKeepAliveRangeBonusPercent()
    {
        return GetInt(CollectKeepAliveRangeBonusPercentKey, DefaultCollectKeepAliveRangeBonusPercent, 0, 200);
    }

    public static int GetEnemyDamageMultiplierPercent()
    {
        return GetInt(EnemyDamageMultiplierPercentKey, DefaultEnemyDamageMultiplierPercent, 50, 150);
    }

    public static float GetEnemyDamageMultiplier()
    {
        return GetEnemyDamageMultiplierPercent() / 100f;
    }

    public static int GetEnemyAttackWindupMultiplierPercent()
    {
        return GetInt(EnemyAttackWindupMultiplierPercentKey, DefaultEnemyAttackWindupMultiplierPercent, 50, 150);
    }

    public static float GetEnemyAttackWindupMultiplier()
    {
        return GetEnemyAttackWindupMultiplierPercent() / 100f;
    }

    public static int GetEnemyAttackCooldownMultiplierPercent()
    {
        return GetInt(EnemyAttackCooldownMultiplierPercentKey, DefaultEnemyAttackCooldownMultiplierPercent, 50, 150);
    }

    public static float GetEnemyAttackCooldownMultiplier()
    {
        return GetEnemyAttackCooldownMultiplierPercent() / 100f;
    }

    public static bool AreHapticsEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(HapticsEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultHapticsEnabled;
    }

    public static bool IsFpsCounterEnabled()
    {
        return GetBool(FpsCounterEnabledKey, DefaultFpsCounterEnabled);
    }

    public static bool IsDiagnosticsGcEnabled()
    {
        return GetBool(DiagnosticsGcEnabledKey, DefaultDiagnosticsGcEnabled);
    }

    public static bool IsDiagnosticsSceneCountsEnabled()
    {
        return GetBool(DiagnosticsSceneCountsEnabledKey, DefaultDiagnosticsSceneCountsEnabled);
    }

    public static bool IsDiagnosticsNetworkEnabled()
    {
        return GetBool(DiagnosticsNetworkEnabledKey, DefaultDiagnosticsNetworkEnabled);
    }

    public static bool IsAnyDiagnosticsOverlayEnabled()
    {
        return IsFpsCounterEnabled() ||
            IsDiagnosticsGcEnabled() ||
            IsDiagnosticsSceneCountsEnabled() ||
            IsDiagnosticsNetworkEnabled();
    }

    public static bool AreNeutralRidersEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NeutralRidersEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultNeutralRidersEnabled;
    }

    public static int GetNeutralRiderCount()
    {
        return GetInt(NeutralRidersCountKey, DefaultNeutralRidersCount, 1, 3);
    }

    public static string GetNeutralRiderAggression()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NeutralRidersAggressionKey, out object value) &&
            value is string aggression)
        {
            return NormalizeNeutralRiderAggression(aggression);
        }

        return DefaultNeutralRiderAggression;
    }

    public static string NormalizeNeutralRiderAggression(string aggression)
    {
        string normalized = string.IsNullOrWhiteSpace(aggression)
            ? DefaultNeutralRiderAggression
            : aggression.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case "medium":
                return NeutralRiderAggressionNormal;
            case NeutralRiderAggressionLow:
            case NeutralRiderAggressionNormal:
            case NeutralRiderAggressionHigh:
                return normalized;
            default:
                return DefaultNeutralRiderAggression;
        }
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

    public static int GetEndDisasterWarningSeconds()
    {
        return GetInt(
            EndDisasterWarningSecondsKey,
            DefaultEndDisasterWarningSeconds,
            MinEndDisasterWarningSeconds,
            MaxEndDisasterWarningSeconds);
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

    public static string GetBaseTreasureDensity()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TreasureDensityKey, out object value) &&
            value is string density)
        {
            return NormalizeTreasureDensity(density);
        }

        return DefaultTreasureDensity;
    }

    public static string GetTreasureDensity()
    {
        string density = GetBaseTreasureDensity();
        return IsCrazyEnemiesActive() ? IncreaseTreasureDensity(density) : density;
    }

    public static string GetRadioactiveTreasureDensity()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RadioactiveTreasureDensityKey, out object value) &&
            value is string density)
        {
            return NormalizeRadioactiveTreasureDensity(density);
        }

        return DefaultRadioactiveTreasureDensity;
    }

    public static string GetAlienSecretsDensity()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(AlienSecretsDensityKey, out object value) &&
            value is string density)
        {
            return NormalizeAlienSecretsDensity(density);
        }

        return DefaultAlienSecretsDensity;
    }

    public static string NormalizeTreasureDensity(string density)
    {
        string normalized = string.IsNullOrWhiteSpace(density)
            ? DefaultTreasureDensity
            : density.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case TreasureDensityNone:
            case TreasureDensityVeryLow:
            case TreasureDensityLow:
            case TreasureDensityMedium:
            case TreasureDensityHigh:
                return normalized;
            default:
                return DefaultTreasureDensity;
        }
    }

    public static string NormalizeRadioactiveTreasureDensity(string density)
    {
        string normalized = string.IsNullOrWhiteSpace(density)
            ? DefaultRadioactiveTreasureDensity
            : density.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case RadioactiveTreasureDensityOff:
            case "none":
                return RadioactiveTreasureDensityOff;
            case RadioactiveTreasureDensityLow:
            case RadioactiveTreasureDensityMedium:
            case RadioactiveTreasureDensityHigh:
                return normalized;
            default:
                return DefaultRadioactiveTreasureDensity;
        }
    }

    public static string NormalizeAlienSecretsDensity(string density)
    {
        string normalized = string.IsNullOrWhiteSpace(density)
            ? DefaultAlienSecretsDensity
            : density.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case AlienSecretsDensityNone:
            case AlienSecretsDensityVeryLow:
            case AlienSecretsDensityLow:
            case AlienSecretsDensityMedium:
            case AlienSecretsDensityHigh:
            case AlienSecretsDensityVeryHigh:
                return normalized;
            default:
                return DefaultAlienSecretsDensity;
        }
    }

    public static string IncreaseTreasureDensity(string density)
    {
        switch (NormalizeTreasureDensity(density))
        {
            case TreasureDensityNone:
                return TreasureDensityVeryLow;
            case TreasureDensityVeryLow:
                return TreasureDensityLow;
            case TreasureDensityLow:
                return TreasureDensityMedium;
            case TreasureDensityMedium:
                return TreasureDensityHigh;
            default:
                return TreasureDensityHigh;
        }
    }

    public static string GetBaseResourceRichness()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ResourceRichnessKey, out object value) &&
            value is string richness)
        {
            return NormalizeResourceRichness(richness);
        }

        return DefaultResourceRichness;
    }

    public static string GetResourceRichness()
    {
        string richness = GetBaseResourceRichness();
        if (IsFogOfWarActive())
            richness = IncreaseResourceRichness(richness);

        if (IsPirateBaseActive())
            richness = IncreaseResourceRichness(richness);

        if (IsAsteroidShowerActive())
            richness = IncreaseResourceRichness(richness);

        if (IsCosmicWormActive())
            richness = IncreaseResourceRichness(richness);

        return richness;
    }

    public static string NormalizeResourceRichness(string richness)
    {
        string normalized = string.IsNullOrWhiteSpace(richness)
            ? DefaultResourceRichness
            : richness.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case "extremaly_low":
                return ResourceRichnessExtremelyLow;
            case ResourceRichnessExtremelyLow:
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

    public static string IncreaseResourceRichness(string richness)
    {
        switch (NormalizeResourceRichness(richness))
        {
            case ResourceRichnessExtremelyLow:
                return ResourceRichnessVeryLow;
            case ResourceRichnessVeryLow:
                return ResourceRichnessLow;
            case ResourceRichnessLow:
                return ResourceRichnessMedium;
            case ResourceRichnessMedium:
                return ResourceRichnessHigh;
            case ResourceRichnessHigh:
                return ResourceRichnessVeryHigh;
            case ResourceRichnessVeryHigh:
                return ResourceRichnessExtreme;
            default:
                return ResourceRichnessExtreme;
        }
    }

    public static string GetSpaceJunkDensity()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(SpaceJunkDensityKey, out object value) &&
            value is string density)
        {
            return NormalizeSpaceJunkDensity(density);
        }

        return DefaultSpaceJunkDensity;
    }

    public static string NormalizeSpaceJunkDensity(string density)
    {
        string normalized = string.IsNullOrWhiteSpace(density)
            ? DefaultSpaceJunkDensity
            : density.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case "off":
                return SpaceJunkDensityNone;
            case SpaceJunkDensityNone:
            case SpaceJunkDensityLow:
            case SpaceJunkDensityMedium:
            case SpaceJunkDensityHigh:
                return normalized;
            default:
                return DefaultSpaceJunkDensity;
        }
    }

    public static string GetFireNebulaDensity()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(FireNebulaDensityKey, out object value) &&
            value is string density)
        {
            return NormalizeFireNebulaDensity(density);
        }

        return DefaultFireNebulaDensity;
    }

    public static string GetToxicNebulaDensity()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ToxicNebulaDensityKey, out object value) &&
            value is string density)
        {
            return NormalizeToxicNebulaDensity(density);
        }

        return DefaultToxicNebulaDensity;
    }

    public static string GetCloudsDensity()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CloudsDensityKey, out object value) &&
            value is string density)
        {
            return NormalizeCloudsDensity(density);
        }

        return DefaultCloudsDensity;
    }

    public static bool IsAdvancedNebulaEnabled()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(AdvancedNebulaEnabledKey, out object value) &&
            value is bool enabled)
        {
            return enabled;
        }

        return DefaultAdvancedNebulaEnabled;
    }

    public static string NormalizeFireNebulaDensity(string density)
    {
        string normalized = string.IsNullOrWhiteSpace(density)
            ? DefaultFireNebulaDensity
            : density.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case SpaceJunkDensityNone:
            case SpaceJunkDensityLow:
            case SpaceJunkDensityMedium:
            case SpaceJunkDensityHigh:
                return normalized;
            default:
                return DefaultFireNebulaDensity;
        }
    }

    public static string NormalizeToxicNebulaDensity(string density)
    {
        string normalized = string.IsNullOrWhiteSpace(density)
            ? DefaultToxicNebulaDensity
            : density.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case "off":
                return SpaceJunkDensityNone;
            case SpaceJunkDensityNone:
            case SpaceJunkDensityLow:
            case SpaceJunkDensityMedium:
            case SpaceJunkDensityHigh:
                return normalized;
            default:
                return DefaultToxicNebulaDensity;
        }
    }

    public static string NormalizeCloudsDensity(string density)
    {
        string normalized = string.IsNullOrWhiteSpace(density)
            ? DefaultCloudsDensity
            : density.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case SpaceJunkDensityNone:
            case SpaceJunkDensityLow:
            case SpaceJunkDensityMedium:
            case SpaceJunkDensityHigh:
                return normalized;
            default:
                return DefaultCloudsDensity;
        }
    }

    public static string GetNebulaSize()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NebulaSizeKey, out object value) &&
            value is string size)
        {
            return NormalizeNebulaSize(size);
        }

        return DefaultNebulaSize;
    }

    public static string GetFireNebulaSize()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(FireNebulaSizeKey, out object value) &&
            value is string size)
        {
            return NormalizeNebulaSize(size);
        }

        return DefaultFireNebulaSize;
    }

    public static string GetToxicNebulaSize()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ToxicNebulaSizeKey, out object value) &&
            value is string size)
        {
            return NormalizeNebulaSize(size);
        }

        return DefaultToxicNebulaSize;
    }

    public static string GetCloudsSize()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CloudsSizeKey, out object value) &&
            value is string size)
        {
            return NormalizeNebulaSize(size);
        }

        return DefaultCloudsSize;
    }

    public static string NormalizeNebulaSize(string size)
    {
        string normalized = string.IsNullOrWhiteSpace(size)
            ? NebulaSizeNormal
            : size.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case NebulaSizeVerySmall:
            case NebulaSizeSmall:
            case NebulaSizeNormal:
            case NebulaSizeBig:
            case NebulaSizeVeryBig:
                return normalized;
            default:
                return NebulaSizeNormal;
        }
    }

    public static float GetNebulaSizeMultiplier()
    {
        return GetNebulaSizeMultiplierForValue(GetNebulaSize());
    }

    public static float GetFireNebulaSizeMultiplier()
    {
        return GetNebulaSizeMultiplierForValue(GetFireNebulaSize());
    }

    public static float GetToxicNebulaSizeMultiplier()
    {
        return GetNebulaSizeMultiplierForValue(GetToxicNebulaSize());
    }

    public static float GetCloudsSizeMultiplier()
    {
        return GetNebulaSizeMultiplierForValue(GetCloudsSize());
    }

    public static float GetNebulaSizeMultiplierForValue(string size)
    {
        switch (NormalizeNebulaSize(size))
        {
            case NebulaSizeVerySmall:
                return 0.33f;
            case NebulaSizeSmall:
                return 0.66f;
            case NebulaSizeBig:
                return 1.5f;
            case NebulaSizeVeryBig:
                return 2f;
            default:
                return 1f;
        }
    }

    public static string GetContainersDensity()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ContainersDensityKey, out object value) &&
            value is string density)
        {
            return NormalizeContainersDensity(density);
        }

        return DefaultContainersDensity;
    }

    public static string NormalizeContainersDensity(string density)
    {
        string normalized = string.IsNullOrWhiteSpace(density)
            ? DefaultContainersDensity
            : density.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case ContainersDensityNone:
            case ContainersDensityVeryLow:
            case ContainersDensityLow:
            case ContainersDensityMedium:
            case ContainersDensityHigh:
            case ContainersDensityVeryHigh:
                return normalized;
            default:
                return DefaultContainersDensity;
        }
    }

    public static string GetArtifactAsteroidsDensity()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ArtifactAsteroidsDensityKey, out object value) &&
            value is string density)
        {
            return NormalizeArtifactAsteroidsDensity(density);
        }

        return DefaultArtifactAsteroidsDensity;
    }

    public static string NormalizeArtifactAsteroidsDensity(string density)
    {
        string normalized = string.IsNullOrWhiteSpace(density)
            ? DefaultArtifactAsteroidsDensity
            : density.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case "none":
                return ArtifactAsteroidsDensityOff;
            case ArtifactAsteroidsDensityOff:
            case ArtifactAsteroidsDensityVeryLow:
            case ArtifactAsteroidsDensityLow:
            case ArtifactAsteroidsDensityMedium:
            case ArtifactAsteroidsDensityHigh:
                return normalized;
            default:
                return DefaultArtifactAsteroidsDensity;
        }
    }

    public static int GetArtifactAsteroidsCount()
    {
        switch (GetArtifactAsteroidsDensity())
        {
            case ArtifactAsteroidsDensityVeryLow:
                return 1;
            case ArtifactAsteroidsDensityLow:
                return 3;
            case ArtifactAsteroidsDensityMedium:
                return 5;
            case ArtifactAsteroidsDensityHigh:
                return 8;
            default:
                return 0;
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

        if (kind == EnemyBotKind.CosmicWorm)
            return IsCosmicWormEnabled();

        if (kind == EnemyBotKind.PirateBase && IsPirateBaseEffectReadyOrActive())
            return true;

        if (kind == EnemyBotKind.MilitaryVan && IsMilitaryConvoyActive())
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

        if (kind == EnemyBotKind.CosmicWorm)
            return 1;

        int maxCount = kind == EnemyBotKind.SpaceMine ? 30 : 5;
        return GetInt(definition.CountRoomKey, definition.DefaultCount, 1, maxCount);
    }

    public static int GetEnemyHp(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return 100;

        int maxHp = Mathf.Max(200, definition.MaxHp > 0 ? definition.MaxHp : 200);
        int hp;
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.HpRoomKey))
        {
            hp = GetInt(definition.HpRoomKey, definition.DefaultHp, 20, maxHp);
        }
        else if (kind == EnemyBotKind.Corsair &&
            PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CorsairHpKey, out object legacyValue))
        {
            hp = Mathf.Clamp(ConvertToInt(legacyValue, definition.DefaultHp), 20, 200);
        }
        else
        {
            hp = Mathf.Clamp(definition.DefaultHp, 20, maxHp);
        }

        return IsCrazyEnemiesActive() ? Mathf.Max(1, Mathf.CeilToInt(hp * 0.5f)) : hp;
    }

    public static int GetEnemyShield(EnemyBotKind kind)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        if (definition == null)
            return DefaultEnemyShield;

        int maxShield = Mathf.Max(200, definition.MaxShield > 0 ? definition.MaxShield : 200);
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.ShieldRoomKey))
        {
            return GetInt(definition.ShieldRoomKey, definition.DefaultShield, 0, maxShield);
        }

        return Mathf.Clamp(definition.DefaultShield, 0, maxShield);
    }

    public static int GetEnemyBaseDamage(EnemyBotKind kind)
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

    public static int GetEnemyDamage(EnemyBotKind kind)
    {
        int damage = GetEnemyBaseDamage(kind);
        if (IsCrazyEnemiesActive())
            damage = Mathf.Max(0, damage * 2);

        if (damage <= 0)
            return 0;

        return Mathf.Max(1, Mathf.RoundToInt(damage * GetEnemyDamageMultiplier()));
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

        int spawnSecond;
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.SpawnSecondRoomKey))
        {
            spawnSecond = GetInt(definition.SpawnSecondRoomKey, definition.DefaultSpawnSecond, 0, 120);
        }
        else if (kind == EnemyBotKind.Corsair &&
            PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CorsairSpawnSecondKey, out object legacyValue))
        {
            spawnSecond = Mathf.Clamp(ConvertToInt(legacyValue, definition.DefaultSpawnSecond), 0, 120);
        }
        else
        {
            spawnSecond = Mathf.Clamp(definition.DefaultSpawnSecond, 0, 120);
        }

        return ApplyCrazyEnemiesSpawnFrequency(spawnSecond);
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
            return ApplyCrazyEnemiesSpawnFrequency(DefaultEnemyRespawnIntervalSeconds);

        return ApplyCrazyEnemiesSpawnFrequency(GetInt(definition.RespawnIntervalRoomKey, DefaultEnemyRespawnIntervalSeconds, 15, 150));
    }

    public static float GetEnemyVisualScaleMultiplier()
    {
        return IsCrazyEnemiesActive() ? CrazyEnemiesVisualScaleMultiplier : 1f;
    }

    static int ApplyCrazyEnemiesSpawnFrequency(int seconds)
    {
        if (!IsCrazyEnemiesActive() || seconds <= 0)
            return Mathf.Max(0, seconds);

        return Mathf.Max(1, Mathf.CeilToInt(seconds / CrazyEnemiesSpawnFrequencyMultiplier));
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

    public static string GetMapEffectMode(string modeKey)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(modeKey, out object value) &&
            value is string mode)
        {
            return NormalizeMapEffectMode(mode);
        }

        return DefaultMapEffectMode;
    }

    public static string NormalizeMapEffectMode(string mode)
    {
        string normalized = string.IsNullOrWhiteSpace(mode)
            ? DefaultMapEffectMode
            : mode.Trim().ToLowerInvariant().Replace(" ", "_");

        switch (normalized)
        {
            case MapEffectModeAlwaysOn:
            case MapEffectModeUtcStart:
                return normalized;
            default:
                return MapEffectModeOff;
        }
    }

    public static bool IsSpecialMapEffectModeKey(string key)
    {
        switch (key)
        {
            case CrazyEnemiesModeKey:
            case FogOfWarModeKey:
            case PirateBaseModeKey:
            case AsteroidShowerModeKey:
            case CosmicWormModeKey:
            case MilitaryConvoyModeKey:
                return true;
            default:
                return false;
        }
    }

    public static double GetMapEffectStartUtcMs(string startKey)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(startKey, out object value))
        {
            return ConvertToDouble(value, -1d);
        }

        return -1d;
    }

    public static string GetMapEffectChanceKey(string mapId, string ruleId)
    {
        return MapEffectChanceKeyPrefix +
               NormalizeMapEffectChanceKeyPart(mapId) +
               "." +
               NormalizeMapEffectChanceKeyPart(ruleId);
    }

    public static bool IsMapEffectChanceKey(string key)
    {
        return !string.IsNullOrWhiteSpace(key) &&
               key.StartsWith(MapEffectChanceKeyPrefix, System.StringComparison.Ordinal);
    }

    public static int GetMapEffectChancePercent(string mapId, string ruleId)
    {
        return GetInt(GetMapEffectChanceKey(mapId, ruleId), GetDefaultMapEffectChancePercent(mapId, ruleId), 0, 100);
    }

    public static int GetDefaultMapEffectChancePercent(string mapId, string ruleId)
    {
        string normalizedMapId = string.IsNullOrWhiteSpace(mapId)
            ? string.Empty
            : mapId.Trim().ToLowerInvariant();
        string normalizedRuleId = string.IsNullOrWhiteSpace(ruleId)
            ? string.Empty
            : ruleId.Trim();

        if (normalizedRuleId == MilitaryConvoyRuleId)
        {
            if (normalizedMapId == "just_space" || normalizedMapId == "hidden_dimension")
                return 0;

            return normalizedMapId == "noob_haven" ? 15 : 5;
        }

        switch (normalizedMapId)
        {
            case "just_space":
                return 0;
            case "noob_haven":
                return GetDefaultMapEffectChanceForRule(normalizedRuleId, 1, 1, 0, 5, 0);
            case "minefield":
                return GetDefaultMapEffectChanceForRule(normalizedRuleId, 5, 5, 0, 10, 1);
            case "snow_field":
                return GetDefaultMapEffectChanceForRule(normalizedRuleId, 5, 1, 1, 5, 10);
            case "deep_space":
                return GetDefaultMapEffectChanceForRule(normalizedRuleId, 5, 20, 5, 10, 10);
            case "hidden_dimension":
                return 0;
            case "pirate_bay":
                return GetDefaultMapEffectChanceForRule(normalizedRuleId, 5, 5, 35, 5, 5);
            case "ancient_space":
                return GetDefaultMapEffectChanceForRule(normalizedRuleId, 5, 10, 5, 10, 15);
            case "mothership":
                return GetDefaultMapEffectChanceForRule(normalizedRuleId, 5, 5, 1, 10, 1);
            case "toxic_area":
                return GetDefaultMapEffectChanceForRule(normalizedRuleId, 5, 1, 1, 5, 1);
            case "gravity_well":
                return 1;
            default:
                return DefaultMapEffectChancePercent;
        }
    }

    public static bool IsLegacyMapEffectChanceDefault(string mapId, string ruleId, int percent)
    {
        if (percent == GetDefaultMapEffectChancePercent(mapId, ruleId))
            return false;

        if (percent == DefaultMapEffectChancePercent)
            return true;

        string normalizedMapId = string.IsNullOrWhiteSpace(mapId)
            ? string.Empty
            : mapId.Trim().ToLowerInvariant();
        string normalizedRuleId = string.IsNullOrWhiteSpace(ruleId)
            ? string.Empty
            : ruleId.Trim();

        return normalizedMapId == "pirate_bay" &&
               normalizedRuleId == PirateBaseRuleId &&
               percent == 20;
    }

    static int GetDefaultMapEffectChanceForRule(string ruleId, int crazyEnemiesPercent, int fogOfWarPercent, int pirateBasePercent, int asteroidShowerPercent, int cosmicWormPercent)
    {
        switch (ruleId)
        {
            case CrazyEnemiesRuleId:
                return crazyEnemiesPercent;
            case FogOfWarRuleId:
                return fogOfWarPercent;
            case PirateBaseRuleId:
                return pirateBasePercent;
            case AsteroidShowerRuleId:
                return asteroidShowerPercent;
            case CosmicWormRuleId:
                return cosmicWormPercent;
            case MilitaryConvoyRuleId:
                return 5;
            default:
                return DefaultMapEffectChancePercent;
        }
    }

    public static int NormalizeMapEffectChancePercent(int percent)
    {
        return Mathf.Clamp(percent, 0, 100);
    }

    public static bool ShouldMapEffectActivate(string modeKey, string startKey, double roundStartUtcMs)
    {
        string mode = GetMapEffectMode(modeKey);
        if (mode == MapEffectModeAlwaysOn)
            return true;

        if (mode != MapEffectModeUtcStart)
            return false;

        double startUtcMs = GetMapEffectStartUtcMs(startKey);
        if (startUtcMs < 0d)
            return false;

        return roundStartUtcMs >= startUtcMs &&
               roundStartUtcMs <= startUtcMs + MapEffectActivationWindowMs;
    }

    public static bool ShouldMapEffectActivateForRound(string modeKey, string startKey, string mapId, string ruleId, double roundStartUtcMs)
    {
        string mode = GetMapEffectMode(modeKey);
        if (mode == MapEffectModeAlwaysOn)
        {
            int chancePercent = GetMapEffectChancePercent(mapId, ruleId);
            if (chancePercent <= 0)
                return false;

            if (chancePercent >= 100)
                return true;

            return UnityEngine.Random.Range(0, 100) < chancePercent;
        }

        if (mode != MapEffectModeUtcStart)
            return false;

        return ShouldMapEffectActivate(modeKey, startKey, roundStartUtcMs);
    }

    public static bool IsCrazyEnemiesActive()
    {
        return IsMapEffectActive(CrazyEnemiesActiveKey);
    }

    public static bool IsFogOfWarActive()
    {
        return IsMapEffectActive(FogOfWarActiveKey);
    }

    public static bool IsPirateBaseActive()
    {
        return IsMapEffectActive(PirateBaseActiveKey);
    }

    public static bool IsAsteroidShowerActive()
    {
        return IsMapEffectActive(AsteroidShowerActiveKey);
    }

    public static bool IsCosmicWormActive()
    {
        return IsMapEffectActive(CosmicWormActiveKey);
    }

    public static bool IsMilitaryConvoyActive()
    {
        return IsMapEffectActive(MilitaryConvoyActiveKey);
    }

    static bool IsPirateBaseEffectReadyOrActive()
    {
        if (IsPirateBaseActive())
            return true;

        if (GetSessionState() == SessionStateInPlay)
            return false;

        if (GetSessionState() != SessionStateInLobby)
            return false;

        if (GetMapEffectMode(PirateBaseModeKey) != MapEffectModeUtcStart)
            return false;

        return ShouldMapEffectActivate(
            PirateBaseModeKey,
            PirateBaseStartUtcMsKey,
            System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    static bool IsMapEffectActive(string activeKey)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(activeKey, out object value))
        {
            if (value is bool boolValue)
                return boolValue;

            if (value is int intValue)
                return intValue != 0;
        }

        return false;
    }

    static string NormalizeMapEffectChanceKeyPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        return value.Trim().Replace(" ", "_");
    }

    public static string GetGunSetupRoomKey(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            weaponId = "unknown";

        return GunSetupKeyPrefix + weaponId.Trim().ToLowerInvariant();
    }

    public static bool IsGunSetupKey(string key)
    {
        return !string.IsNullOrWhiteSpace(key) &&
               key.StartsWith(GunSetupKeyPrefix, System.StringComparison.Ordinal);
    }

    public static string GetGunSetupOverride(string weaponId)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(GetGunSetupRoomKey(weaponId), out object value) &&
            value is string raw)
        {
            return raw;
        }

        return string.Empty;
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

    public static Vector2 GetBaseMapDimensions()
    {
        Vector2 size;
        switch (GetMapSizeMode())
        {
            case "small":
                size = new Vector2(24f, 24f);
                break;
            case "large":
                size = new Vector2(38.4f, 38.4f);
                break;
            case "very_large":
                size = new Vector2(48f, 48f);
                break;
            case "super_large":
                size = new Vector2(60f, 60f);
                break;
            default:
                size = new Vector2(30f, 30f);
                break;
        }

        return size * MapDimensionsScale;
    }

    public static Vector2 GetMapDimensions()
    {
        Vector2 size = GetBaseMapDimensions();
        return AreToxicBordersEnabled() ? size * ToxicBordersMapScale : size;
    }

    public static Vector2 GetGameplayMapDimensions()
    {
        return AreToxicBordersEnabled() ? GetBaseMapDimensions() : GetMapDimensions();
    }

    public static Vector2 GetEnemyNavigableMapDimensions()
    {
        return GetGameplayMapDimensions();
    }

    public static Vector2 ClampToGameplayMapBounds(Vector2 position, float margin = 0f)
    {
        Vector2 size = GetGameplayMapDimensions();
        if (size.x <= 0f || size.y <= 0f)
            return position;

        float safeMargin = Mathf.Max(0f, margin);
        float halfX = Mathf.Max(0.5f, size.x * 0.5f - safeMargin);
        float halfY = Mathf.Max(0.5f, size.y * 0.5f - safeMargin);
        return new Vector2(
            Mathf.Clamp(position.x, -halfX, halfX),
            Mathf.Clamp(position.y, -halfY, halfY));
    }

    public static Vector2 ClampToEnemyNavigableBounds(Vector2 position, float margin = 0f)
    {
        return ClampToGameplayMapBounds(position, margin);
    }

    public static Vector2 ApplyEnemyToxicBorderSteering(Vector2 position, Vector2 desiredDirection, float lookAheadDistance, float bodyRadius, float blendScale = 1f)
    {
        if (!TryGetEnemyToxicBorderAvoidance(position, desiredDirection, lookAheadDistance, bodyRadius, out Vector2 steering, out float strength))
            return desiredDirection;

        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : steering;
        float outwardAmount = Mathf.Clamp01(-Vector2.Dot(desired, steering.normalized));
        float blend = Mathf.Clamp01((0.25f + strength * 0.75f + outwardAmount * 0.22f) * strength * Mathf.Max(0.05f, blendScale));
        Vector2 result = desired * (1f - blend) + steering * blend;
        return result.sqrMagnitude > 0.001f ? result.normalized : steering;
    }

    public static bool TryGetEnemyToxicBorderAvoidance(Vector2 position, Vector2 desiredDirection, float lookAheadDistance, float bodyRadius, out Vector2 steering, out float strength)
    {
        steering = Vector2.zero;
        strength = 0f;

        if (!AreToxicBordersEnabled())
            return false;

        Vector2 innerSize = GetBaseMapDimensions();
        Vector2 outerSize = GetMapDimensions();
        if (innerSize.x <= 0f || innerSize.y <= 0f || outerSize.x <= innerSize.x || outerSize.y <= innerSize.y)
            return false;

        float radius = Mathf.Max(0f, bodyRadius);
        float halfX = Mathf.Max(0.5f, innerSize.x * 0.5f - radius);
        float halfY = Mathf.Max(0.5f, innerSize.y * 0.5f - radius);
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.zero;
        Vector2 predicted = position + desired * Mathf.Max(0f, lookAheadDistance);
        Vector2 push = Vector2.zero;
        float maxStrength = 0f;

        AccumulateToxicBorderAvoidance(position, halfX, halfY, 1f, ref push, ref maxStrength);
        if (desired.sqrMagnitude > 0.001f)
            AccumulateToxicBorderAvoidance(predicted, halfX, halfY, 0.78f, ref push, ref maxStrength);

        if (push.sqrMagnitude <= 0.001f)
            return false;

        Vector2 inward = push.normalized;
        Vector2 tangent = desired.sqrMagnitude > 0.001f ? new Vector2(-inward.y, inward.x) : Vector2.zero;
        if (tangent.sqrMagnitude > 0.001f && Vector2.Dot(tangent, desired) < 0f)
            tangent = -tangent;

        float tangentWeight = Mathf.Lerp(0.3f, 0.04f, Mathf.Clamp01(maxStrength));
        Vector2 blended = inward + tangent * tangentWeight;
        steering = blended.sqrMagnitude > 0.001f ? blended.normalized : inward;
        strength = Mathf.Clamp01(maxStrength);
        return true;
    }

    static void AccumulateToxicBorderAvoidance(Vector2 point, float halfX, float halfY, float weight, ref Vector2 push, ref float maxStrength)
    {
        AccumulateToxicBorderAxis(halfX - point.x, Vector2.left, weight, ref push, ref maxStrength);
        AccumulateToxicBorderAxis(point.x + halfX, Vector2.right, weight, ref push, ref maxStrength);
        AccumulateToxicBorderAxis(halfY - point.y, Vector2.down, weight, ref push, ref maxStrength);
        AccumulateToxicBorderAxis(point.y + halfY, Vector2.up, weight, ref push, ref maxStrength);
    }

    static void AccumulateToxicBorderAxis(float distanceToSafeEdge, Vector2 inward, float weight, ref Vector2 push, ref float maxStrength)
    {
        if (distanceToSafeEdge >= ToxicBordersEnemyAvoidanceBuffer)
            return;

        float t = Mathf.InverseLerp(ToxicBordersEnemyAvoidanceBuffer, -ToxicBordersEnemyHardAvoidanceBuffer, distanceToSafeEdge);
        float axisStrength = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)) * Mathf.Clamp01(weight);
        if (axisStrength <= 0.001f)
            return;

        push += inward * axisStrength;
        maxStrength = Mathf.Max(maxStrength, axisStrength);
    }

    public static float GetMapAreaMultiplier()
    {
        Vector2 size = GetMapDimensions();
        const float baseArea = 25f * 25f;
        float area = size.x * size.y;
        return Mathf.Max(0.5f, area / baseArea);
    }

    public static float GetGameplayMapAreaMultiplier()
    {
        Vector2 size = GetGameplayMapDimensions();
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

    public static string GetPlayerPilotId(Photon.Realtime.Player player, string fallback)
    {
        if (player != null &&
            player.CustomProperties.TryGetValue(PilotIdKey, out object value) &&
            value is string pilotId)
        {
            return PilotCatalog.NormalizePilotId(pilotId);
        }

        return PilotCatalog.NormalizePilotId(fallback);
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

    static bool GetBool(string key, bool defaultValue)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value) &&
            value is bool enabled)
        {
            return enabled;
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

    static double ConvertToDouble(object value, double fallback)
    {
        if (value is double doubleValue)
            return doubleValue;

        if (value is float floatValue)
            return floatValue;

        if (value is int intValue)
            return intValue;

        if (value is long longValue)
            return longValue;

        if (value is byte byteValue)
            return byteValue;

        return fallback;
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
