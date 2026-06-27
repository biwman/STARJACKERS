using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
public partial class LobbyManager
{
    void EnsureDefaultRoomSettings()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        bool changed = false;
        string hostName = !string.IsNullOrWhiteSpace(PhotonNetwork.NickName) ? PhotonNetwork.NickName : "Pilot";
        bool roundStarted = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object startedValue) &&
                            startedValue is bool started &&
                            started;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SessionStateKey))
        {
            props[RoomSettings.SessionStateKey] = roundStarted ? RoomSettings.SessionStateInPlay : RoomSettings.SessionStateInLobby;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SessionLabelKey))
        {
            props[RoomSettings.SessionLabelKey] = hostName + "'s Round";
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SessionHostNameKey))
        {
            props[RoomSettings.SessionHostNameKey] = hostName;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SessionCreatedAtKey))
        {
            props[RoomSettings.SessionCreatedAtKey] = PhotonNetwork.Time;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.StartTimeKey))
        {
            props[RoomSettings.StartTimeKey] = -1d;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SelectedMapKey) &&
            PhotonNetwork.CurrentRoom.PlayerCount <= 1)
        {
            LobbyMapCatalog.ApplyToProperties(LobbyMapCatalog.GetDefault(), props);
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.RoundDurationKey))
        {
            props[RoomSettings.RoundDurationKey] = RoomSettings.DefaultRoundDuration;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ObstacleDensityKey))
        {
            props[RoomSettings.ObstacleDensityKey] = RoomSettings.DefaultObstacleDensity;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ObstacleDestroyEnabledKey))
        {
            props[RoomSettings.ObstacleDestroyEnabledKey] = RoomSettings.DefaultObstacleDestroyEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ObstacleHpKey))
        {
            props[RoomSettings.ObstacleHpKey] = RoomSettings.DefaultObstacleHp;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ObstacleSizePercentKey))
        {
            props[RoomSettings.ObstacleSizePercentKey] = RoomSettings.DefaultObstacleSizePercent;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ObstacleNoBordersKey))
        {
            props[RoomSettings.ObstacleNoBordersKey] = RoomSettings.DefaultObstacleNoBorders;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.MapSizeKey))
        {
            props[RoomSettings.MapSizeKey] = RoomSettings.DefaultMapSize;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ToxicBordersEnabledKey))
        {
            props[RoomSettings.ToxicBordersEnabledKey] = RoomSettings.DefaultToxicBordersEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.MapBackgroundKey))
        {
            props[RoomSettings.MapBackgroundKey] = RoomSettings.DefaultMapBackground;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.VisualEffectsEnabledKey))
        {
            props[RoomSettings.VisualEffectsEnabledKey] = RoomSettings.DefaultVisualEffectsEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.AdvancedSpawnVfxEnabledKey))
        {
            props[RoomSettings.AdvancedSpawnVfxEnabledKey] = RoomSettings.DefaultAdvancedSpawnVfxEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.LowHpHullSparksEnabledKey))
        {
            props[RoomSettings.LowHpHullSparksEnabledKey] = RoomSettings.DefaultLowHpHullSparksEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.BoomVfxEnabledKey))
        {
            props[RoomSettings.BoomVfxEnabledKey] = RoomSettings.DefaultBoomVfxEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.DynamicCameraZoomEnabledKey))
        {
            props[RoomSettings.DynamicCameraZoomEnabledKey] = RoomSettings.DefaultDynamicCameraZoomEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ParallaxBackgroundKey))
        {
            props[RoomSettings.ParallaxBackgroundKey] = LobbyMapCatalog.GetDefaultParallaxBackgroundId(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.BackgroundObjectKey))
        {
            props[RoomSettings.BackgroundObjectKey] = LobbyMapCatalog.GetDefaultBackgroundObjectId(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.GravityWellPhysicsEnabledKey))
        {
            props[RoomSettings.GravityWellPhysicsEnabledKey] = string.Equals(
                RoomSettings.GetSelectedLobbyMapId(),
                LobbyMapCatalog.GravityWellMapId,
                System.StringComparison.Ordinal);
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.EndDisasterModeKey))
        {
            props[RoomSettings.EndDisasterModeKey] = RoomSettings.DefaultEndDisasterMode;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.EndDisasterWarningSecondsKey))
        {
            props[RoomSettings.EndDisasterWarningSecondsKey] = RoomSettings.DefaultEndDisasterWarningSeconds;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.TreasureDensityKey))
        {
            props[RoomSettings.TreasureDensityKey] = RoomSettings.DefaultTreasureDensity;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.RadioactiveTreasureDensityKey))
        {
            props[RoomSettings.RadioactiveTreasureDensityKey] = LobbyMapCatalog.GetDefaultRadioactiveTreasureDensity(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.AlienSecretsDensityKey))
        {
            props[RoomSettings.AlienSecretsDensityKey] = LobbyMapCatalog.GetDefaultAlienSecretsDensity(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ResourceRichnessKey))
        {
            props[RoomSettings.ResourceRichnessKey] = RoomSettings.DefaultResourceRichness;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CrazyEnemiesModeKey))
        {
            props[RoomSettings.CrazyEnemiesModeKey] = RoomSettings.DefaultMapEffectMode;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CrazyEnemiesStartUtcMsKey))
        {
            props[RoomSettings.CrazyEnemiesStartUtcMsKey] = -1d;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CrazyEnemiesActiveKey))
        {
            props[RoomSettings.CrazyEnemiesActiveKey] = false;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.FogOfWarModeKey))
        {
            props[RoomSettings.FogOfWarModeKey] = RoomSettings.DefaultMapEffectMode;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.FogOfWarStartUtcMsKey))
        {
            props[RoomSettings.FogOfWarStartUtcMsKey] = -1d;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.FogOfWarActiveKey))
        {
            props[RoomSettings.FogOfWarActiveKey] = false;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.PirateBaseModeKey))
        {
            props[RoomSettings.PirateBaseModeKey] = RoomSettings.DefaultMapEffectMode;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.PirateBaseStartUtcMsKey))
        {
            props[RoomSettings.PirateBaseStartUtcMsKey] = -1d;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.PirateBaseActiveKey))
        {
            props[RoomSettings.PirateBaseActiveKey] = false;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.AsteroidShowerModeKey))
        {
            props[RoomSettings.AsteroidShowerModeKey] = RoomSettings.DefaultMapEffectMode;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.AsteroidShowerStartUtcMsKey))
        {
            props[RoomSettings.AsteroidShowerStartUtcMsKey] = -1d;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.AsteroidShowerActiveKey))
        {
            props[RoomSettings.AsteroidShowerActiveKey] = false;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CosmicWormModeKey))
        {
            props[RoomSettings.CosmicWormModeKey] = RoomSettings.DefaultMapEffectMode;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CosmicWormStartUtcMsKey))
        {
            props[RoomSettings.CosmicWormStartUtcMsKey] = -1d;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CosmicWormActiveKey))
        {
            props[RoomSettings.CosmicWormActiveKey] = false;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.MilitaryConvoyModeKey))
        {
            props[RoomSettings.MilitaryConvoyModeKey] = RoomSettings.DefaultMapEffectMode;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.MilitaryConvoyStartUtcMsKey))
        {
            props[RoomSettings.MilitaryConvoyStartUtcMsKey] = -1d;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.MilitaryConvoyActiveKey))
        {
            props[RoomSettings.MilitaryConvoyActiveKey] = false;
            changed = true;
        }

        if (EnsureDefaultMapEffectModeProperties(props))
            changed = true;

        if (EnsureDefaultMapEffectChanceProperties(props))
            changed = true;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SpaceJunkDensityKey))
        {
            props[RoomSettings.SpaceJunkDensityKey] = RoomSettings.DefaultSpaceJunkDensity;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ContainersDensityKey))
        {
            props[RoomSettings.ContainersDensityKey] = RoomSettings.DefaultContainersDensity;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ArtifactAsteroidsDensityKey))
        {
            props[RoomSettings.ArtifactAsteroidsDensityKey] = LobbyMapCatalog.GetDefaultArtifactAsteroidsDensity(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.RandomLootWreckCountKey))
        {
            props[RoomSettings.RandomLootWreckCountKey] = RoomSettings.DefaultRandomLootWreckCount;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.HiddenTreasureEnabledKey))
        {
            props[RoomSettings.HiddenTreasureEnabledKey] = LobbyMapCatalog.IsHiddenTreasureEnabledByDefault(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CosmicWormEnabledKey))
        {
            props[RoomSettings.CosmicWormEnabledKey] = RoomSettings.DefaultCosmicWormEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.AvengerPlotEnabledKey))
        {
            props[RoomSettings.AvengerPlotEnabledKey] = LobbyMapCatalog.IsAvengerPlotEnabledByDefault(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ViperPlotChancePercentKey))
        {
            props[RoomSettings.ViperPlotChancePercentKey] = LobbyMapCatalog.GetDefaultViperPlotChancePercent(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ArrowPlotChancePercentKey))
        {
            props[RoomSettings.ArrowPlotChancePercentKey] = LobbyMapCatalog.GetDefaultArrowPlotChancePercent(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.BisonPlotChancePercentKey))
        {
            props[RoomSettings.BisonPlotChancePercentKey] = LobbyMapCatalog.GetDefaultBisonPlotChancePercent(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.InvaderPlotChancePercentKey))
        {
            props[RoomSettings.InvaderPlotChancePercentKey] = LobbyMapCatalog.GetDefaultInvaderPlotChancePercent(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        LobbyMapDefinition selectedMapForNebulas = LobbyMapCatalog.Get(RoomSettings.GetSelectedLobbyMapId());
        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.NebulaDensityKey))
        {
            props[RoomSettings.NebulaDensityKey] = selectedMapForNebulas != null ? selectedMapForNebulas.NebulaDensity : "medium";
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.FireNebulaDensityKey))
        {
            props[RoomSettings.FireNebulaDensityKey] = selectedMapForNebulas != null ? selectedMapForNebulas.FireNebulaDensity : RoomSettings.DefaultFireNebulaDensity;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ToxicNebulaDensityKey))
        {
            props[RoomSettings.ToxicNebulaDensityKey] = selectedMapForNebulas != null ? selectedMapForNebulas.ToxicNebulaDensity : RoomSettings.DefaultToxicNebulaDensity;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.NebulaSizeKey))
        {
            props[RoomSettings.NebulaSizeKey] = selectedMapForNebulas != null ? selectedMapForNebulas.NebulaSize : RoomSettings.DefaultNebulaSize;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.FireNebulaSizeKey))
        {
            props[RoomSettings.FireNebulaSizeKey] = selectedMapForNebulas != null ? selectedMapForNebulas.FireNebulaSize : RoomSettings.DefaultFireNebulaSize;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ToxicNebulaSizeKey))
        {
            props[RoomSettings.ToxicNebulaSizeKey] = selectedMapForNebulas != null ? selectedMapForNebulas.ToxicNebulaSize : RoomSettings.DefaultToxicNebulaSize;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.AdvancedNebulaEnabledKey))
        {
            props[RoomSettings.AdvancedNebulaEnabledKey] = RoomSettings.DefaultAdvancedNebulaEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CloudsDensityKey))
        {
            props[RoomSettings.CloudsDensityKey] = RoomSettings.DefaultCloudsDensity;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CloudsSizeKey))
        {
            props[RoomSettings.CloudsSizeKey] = RoomSettings.DefaultCloudsSize;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ExtractionCountKey))
        {
            props[RoomSettings.ExtractionCountKey] = RoomSettings.DefaultExtractionCount;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ExtractionTypeKey))
        {
            props[RoomSettings.ExtractionTypeKey] = LobbyMapCatalog.GetDefaultExtractionType(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.RepairBayCountKey))
        {
            props[RoomSettings.RepairBayCountKey] = RoomSettings.DefaultRepairBayCount;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.SpaceFactoryCountKey))
        {
            props[RoomSettings.SpaceFactoryCountKey] = RoomSettings.DefaultSpaceFactoryCount;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ScienceStationCountKey))
        {
            props[RoomSettings.ScienceStationCountKey] = LobbyMapCatalog.GetDefaultScienceStationCount(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.BoosterSlowdownKey))
        {
            props[RoomSettings.BoosterSlowdownKey] = RoomSettings.DefaultBoosterSlowdownPercent;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.BoosterRecoveryDelayKey))
        {
            props[RoomSettings.BoosterRecoveryDelayKey] = RoomSettings.DefaultBoosterRecoveryDelay;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.AdvancedBoosterEnabledKey))
        {
            props[RoomSettings.AdvancedBoosterEnabledKey] = RoomSettings.DefaultAdvancedBoosterEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ShipDriftEnabledKey))
        {
            props[RoomSettings.ShipDriftEnabledKey] = RoomSettings.DefaultShipDriftLevel;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.LastShipTimerMultiplierKey))
        {
            props[RoomSettings.LastShipTimerMultiplierKey] = RoomSettings.DefaultLastShipTimerMultiplier;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.InventoryLossEnabledKey))
        {
            props[RoomSettings.InventoryLossEnabledKey] = RoomSettings.DefaultInventoryLossEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.EquipmentLossEnabledKey))
        {
            props[RoomSettings.EquipmentLossEnabledKey] = RoomSettings.DefaultEquipmentLossEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.MovingObjectsEnabledKey))
        {
            props[RoomSettings.MovingObjectsEnabledKey] = RoomSettings.DefaultMovingObjectsMode;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.EnemyBotsEnabledKey))
        {
            props[RoomSettings.EnemyBotsEnabledKey] = RoomSettings.DefaultEnemyBotsEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CorsairEnabledKey))
        {
            props[RoomSettings.CorsairEnabledKey] = RoomSettings.DefaultCorsairEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CorsairSpawnSecondKey))
        {
            props[RoomSettings.CorsairSpawnSecondKey] = RoomSettings.DefaultCorsairSpawnSecond;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CorsairHpKey))
        {
            props[RoomSettings.CorsairHpKey] = RoomSettings.DefaultCorsairHp;
            changed = true;
        }

        for (int i = 0; i < EnemyBotCatalog.AllDefinitions.Count; i++)
        {
            EnemyBotDefinition definition = EnemyBotCatalog.AllDefinitions[i];
            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.EnabledRoomKey))
            {
                props[definition.EnabledRoomKey] = definition.DefaultEnabled;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.CountRoomKey))
            {
                props[definition.CountRoomKey] = definition.DefaultCount;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.HpRoomKey))
            {
                props[definition.HpRoomKey] = definition.DefaultHp;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.ShieldRoomKey))
            {
                props[definition.ShieldRoomKey] = definition.DefaultShield;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.DamageRoomKey))
            {
                props[definition.DamageRoomKey] = definition.DefaultDamage;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.SpeedRoomKey))
            {
                props[definition.SpeedRoomKey] = definition.DefaultSpeedMultiplier;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.SpawnSecondRoomKey))
            {
                props[definition.SpawnSecondRoomKey] = definition.DefaultSpawnSecond;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.RespawnEnabledRoomKey))
            {
                props[definition.RespawnEnabledRoomKey] = RoomSettings.DefaultEnemyRespawnEnabled;
                changed = true;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(definition.RespawnIntervalRoomKey))
            {
                props[definition.RespawnIntervalRoomKey] = RoomSettings.DefaultEnemyRespawnIntervalSeconds;
                changed = true;
            }
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.BulletPushMultiplierKey))
        {
            props[RoomSettings.BulletPushMultiplierKey] = RoomSettings.DefaultBulletPushMultiplier;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.ObstacleWeightFactorKey))
        {
            props[RoomSettings.ObstacleWeightFactorKey] = RoomSettings.DefaultObstacleWeightFactor;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.TreasureWeightFactorKey))
        {
            props[RoomSettings.TreasureWeightFactorKey] = RoomSettings.DefaultTreasureWeightFactor;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.BatteringDamageKey))
        {
            props[RoomSettings.BatteringDamageKey] = RoomSettings.DefaultBatteringDamage;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.CollectKeepAliveRangeBonusPercentKey))
        {
            props[RoomSettings.CollectKeepAliveRangeBonusPercentKey] = RoomSettings.DefaultCollectKeepAliveRangeBonusPercent;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.HapticsEnabledKey))
        {
            props[RoomSettings.HapticsEnabledKey] = RoomSettings.DefaultHapticsEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.FpsCounterEnabledKey))
        {
            props[RoomSettings.FpsCounterEnabledKey] = RoomSettings.DefaultFpsCounterEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.DiagnosticsGcEnabledKey))
        {
            props[RoomSettings.DiagnosticsGcEnabledKey] = RoomSettings.DefaultDiagnosticsGcEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.DiagnosticsSceneCountsEnabledKey))
        {
            props[RoomSettings.DiagnosticsSceneCountsEnabledKey] = RoomSettings.DefaultDiagnosticsSceneCountsEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.DiagnosticsNetworkEnabledKey))
        {
            props[RoomSettings.DiagnosticsNetworkEnabledKey] = RoomSettings.DefaultDiagnosticsNetworkEnabled;
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.NeutralRidersEnabledKey))
        {
            props[RoomSettings.NeutralRidersEnabledKey] = LobbyMapCatalog.AreNeutralRidersEnabledByDefault(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.NeutralRidersCountKey))
        {
            props[RoomSettings.NeutralRidersCountKey] = LobbyMapCatalog.GetDefaultNeutralRiderCount(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomSettings.NeutralRidersAggressionKey))
        {
            props[RoomSettings.NeutralRidersAggressionKey] = LobbyMapCatalog.GetDefaultNeutralRiderAggression(RoomSettings.GetSelectedLobbyMapId());
            changed = true;
        }

        if (changed)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }
    }

    bool EnsureDefaultMapEffectChanceProperties(Hashtable props)
    {
        if (props == null || PhotonNetwork.CurrentRoom == null)
            return false;

        bool changed = false;
        bool migrateOldDefaults = !HasCurrentMapEffectChanceDefaultsVersion();
        string[] ruleIds =
        {
            RoomSettings.CrazyEnemiesRuleId,
            RoomSettings.FogOfWarRuleId,
            RoomSettings.PirateBaseRuleId,
            RoomSettings.AsteroidShowerRuleId,
            RoomSettings.CosmicWormRuleId,
            RoomSettings.MilitaryConvoyRuleId
        };

        IReadOnlyList<LobbyMapDefinition> maps = LobbyMapCatalog.AllMaps;
        for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
        {
            LobbyMapDefinition map = maps[mapIndex];
            if (map == null)
                continue;

            for (int ruleIndex = 0; ruleIndex < ruleIds.Length; ruleIndex++)
            {
                string key = RoomSettings.GetMapEffectChanceKey(map.Id, ruleIds[ruleIndex]);
                int defaultPercent = RoomSettings.GetDefaultMapEffectChancePercent(map.Id, ruleIds[ruleIndex]);
                if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(key))
                {
                    props[key] = defaultPercent;
                    changed = true;
                    continue;
                }

                if (migrateOldDefaults &&
                    PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object existingValue) &&
                    existingValue is int existingPercent &&
                    RoomSettings.IsLegacyMapEffectChanceDefault(map.Id, ruleIds[ruleIndex], existingPercent))
                {
                    props[key] = defaultPercent;
                    changed = true;
                }
            }
        }

        if (migrateOldDefaults)
        {
            props[RoomSettings.MapEffectChanceDefaultsVersionKey] = RoomSettings.MapEffectChanceDefaultsVersion;
            changed = true;
        }

        return changed;
    }

    bool EnsureDefaultMapEffectModeProperties(Hashtable props)
    {
        if (props == null || PhotonNetwork.CurrentRoom == null || HasCurrentMapEffectModeDefaultsVersion())
            return false;

        string[] modeKeys =
        {
            RoomSettings.CrazyEnemiesModeKey,
            RoomSettings.FogOfWarModeKey,
            RoomSettings.PirateBaseModeKey,
            RoomSettings.AsteroidShowerModeKey,
            RoomSettings.CosmicWormModeKey,
            RoomSettings.MilitaryConvoyModeKey
        };

        for (int i = 0; i < modeKeys.Length; i++)
        {
            string key = modeKeys[i];
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value) &&
                value is string mode &&
                RoomSettings.NormalizeMapEffectMode(mode) == RoomSettings.DefaultMapEffectMode)
            {
                continue;
            }

            props[key] = RoomSettings.DefaultMapEffectMode;
        }

        props[RoomSettings.MapEffectModeDefaultsVersionKey] = RoomSettings.MapEffectModeDefaultsVersion;
        return true;
    }

    bool HasCurrentMapEffectModeDefaultsVersion()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.MapEffectModeDefaultsVersionKey, out object value) &&
               value is int version &&
               version >= RoomSettings.MapEffectModeDefaultsVersion;
    }

    bool HasCurrentMapEffectChanceDefaultsVersion()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.MapEffectChanceDefaultsVersionKey, out object value) &&
               value is int version &&
               version >= RoomSettings.MapEffectChanceDefaultsVersion;
    }

    Button EnsureSettingButton(ref TMP_Text textField, Button existingButton, string buttonName, string textName, Vector2 anchoredPosition, UnityEngine.Events.UnityAction callback)
    {
        Button button = existingButton;

        if (button == null || !button.gameObject.scene.IsValid())
        {
            Transform existing = transform.Find(buttonName);
            if (existing != null)
            {
                button = existing.GetComponent<Button>();
            }
        }

        if (button == null)
        {
            GameObject buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);
            button = buttonObject.GetComponent<Button>();

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(320f, 60f);
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => PlayUiClickAndInvoke(callback));

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.16f, 0.2f, 0.27f, 0.95f);
            image.type = Image.Type.Sliced;
        }

        if (textField == null || !textField.gameObject.scene.IsValid())
        {
            Transform existingText = button.transform.Find(textName);
            if (existingText != null)
            {
                textField = existingText.GetComponent<TMP_Text>();
            }
        }

        if (textField == null)
        {
            GameObject textObject = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(button.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            textField = textObject.GetComponent<TextMeshProUGUI>();
            textField.fontSize = 17f;
            textField.fontStyle = FontStyles.Bold;
            textField.alignment = TextAlignmentOptions.Center;
            textField.color = Color.white;
            textField.textWrappingMode = TextWrappingModes.Normal;
        }

        return button;
    }
}
